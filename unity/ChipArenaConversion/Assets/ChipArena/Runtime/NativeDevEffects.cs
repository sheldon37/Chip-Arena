using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeSourceModels
    {
        private Material inkMaterial;
        /// <summary>
        /// first-person.ts: the standard pilot recoloured with the Siren's InkMaterial (Lambert #0a080e, ash rim .35 power 3,
        /// rising streaks .03), for the Siren's first-person arms.
        /// </summary>
        public string Ink(string model)
        {
            string id="ink-"+model;if(models.ContainsKey(id))return id;if(!models.TryGetValue(model,out var source))return model;
            if(inkMaterial==null)
            {
                inkMaterial=new Material(Resources.Load<Shader>("NativeSourceLit")){enableInstancing=true,name="Native Siren ink"};
                inkMaterial.SetFloat("_Ink",1);inkMaterial.SetFloat("_InkFill",1);inkMaterial.SetFloat("_InkRim",.35f);inkMaterial.SetFloat("_InkFlow",.03f);inkMaterial.SetColor("_InkColor",NativeArena.ColorOf(NativeSirenFx.Ink));materials.Add(inkMaterial);
            }
            var parts=new Part[source.Length];for(int i=0;i<parts.Length;i++)parts[i]=new Part{Name=source[i].Name,Bone=source[i].Bone,Mesh=source[i].Mesh,Material=inkMaterial,NoShadow=true};
            models[id]=parts;return id;
        }
    }

    // C12 part 1: the Siren's effects (client/siren/fx.ts, flames.ts, motion.ts emitFlames and first-person.ts) on the pure
    // Core port (Core/NativeSirenFx.cs). Everything is derived from what host, guests and the killcam replay all hold: the
    // replicated pose (SourceAction/SourceAt/SourceSide/SourceRanged/RigScale), Siren bullets with their replicated
    // SirenKind, the Siren fields and the Hit events from a Siren seat. A one-shot fires on the first frame at or past its
    // source time, is born at that time, and is skipped when more than .4 s late (the source renderer's event cutoff).
    // Presentation only: nothing here touches hit timing, hitboxes, damage, cooldowns, movement or networking.
    // Part 2 (Emperor, Leet, Ansatsuken) is DevFighters (NativeDevFighterEffects.cs) on the same NativeFxDraw layers.
    public sealed partial class ArenaPresentation
    {
        private sealed class SirenTrack
        {
            public string Action;public float At,ModeAt=-100,Lean,CaughtAt=-100,CrushAt=-100;public int Side=1,Fired,Victim,Frame;public bool Ranged,Crushed;
            public readonly NativeSirenBody Body=new NativeSirenBody();
        }
        private struct SirenShotMemo{public int Owner;public float Reach;public NativeSirenShotKind Kind;}
        private NativeSirenFx sirenFx;private NativeFxDraw fxDraw;private System.Random fxRandom;private Func<double> fxNext;private NativeFlameField viewFlames;
        private readonly Dictionary<int,SirenTrack> sirenTracks=new Dictionary<int,SirenTrack>();private readonly Dictionary<int,float> sirenChar=new Dictionary<int,float>();
        private readonly Dictionary<int,Vector2Int> sirenFieldFlags=new Dictionary<int,Vector2Int>();private readonly Dictionary<int,SirenShotMemo> sirenShots=new Dictionary<int,SirenShotMemo>();
        private readonly List<int> sirenStale=new List<int>();private readonly double[] fxJoints=new double[22*16],fxRoot=new double[16],fxLeft=new double[16],fxRight=new double[16],fxMatrix=new double[16];
        private NativeArena fxArena;private int fxGeneration=-1,fxSet,fxRound,fxEvent,fxFrame;private float fxLast=float.NegativeInfinity,viewLast=float.NegativeInfinity;private double viewAcc;
        private Color sirenCharColor=Color.white;private Func<int,Color> sirenCharTint;private string sirenInk;
        private readonly NativeSirenMarkTracker markTracker=new NativeSirenMarkTracker();
        private static readonly Color InkRaw=Raw(NativeSirenFx.Ink),AshRaw=Raw(NativeSirenFx.Ash),WarnRaw=Raw(NativeSirenFx.Warn),SeekerCore=Raw(0x020103),ClawInk=NativeArena.ColorOf(0x07060a),AshAuthored=NativeArena.ColorOf(NativeSirenFx.Ash);
        // A ShaderMaterial colour as three holds it: the hex converted to linear (NativeSirenFx.shader "raw").
        private static Color Raw(int hex){NativeBlastSim.Linear(hex,out double r,out double g,out double b);return new Color((float)r,(float)g,(float)b,1);}
        private static Color Alpha(Color c,double a){c.a=(float)a;return c;}
        private void EnsureSirenFx(){if(sirenFx!=null)return;fxRandom=new System.Random();fxNext=fxRandom.NextDouble;sirenFx=new NativeSirenFx(fxNext);}
        /// <summary>The shared world flame pool (Ward Zero gore flames also burn in it).</summary>
        private NativeFlameField WorldFlames(){EnsureSirenFx();return sirenFx.Fire;}
        /// <summary>How far the Last Laugh burn has charred this pilot (0 when not held).</summary>
        private float SirenChar(int seat)=>sirenChar.TryGetValue(seat,out float burn)?burn:0;
        private Func<int,Color> SirenCharTint(int seat)
        {float burn=SirenChar(seat);if(burn<=0)return null;sirenCharColor=Color.white*(1-.85f*burn);sirenCharColor.a=1;return sirenCharTint??(sirenCharTint=_=>sirenCharColor);}
        private void ClearDevEffects()
        {
            sirenFx?.Clear();viewFlames?.Clear();fxDraw?.Clear();ClearDevFighters();sirenTracks.Clear();sirenChar.Clear();sirenFieldFlags.Clear();sirenShots.Clear();markTracker.Clear();
            fxArena=null;fxGeneration=-1;fxEvent=0;fxLast=viewLast=float.NegativeInfinity;viewAcc=0;
        }
        private void DisposeDevEffects(){ClearDevEffects();DisposeSirenLooks();fxDraw?.Dispose();fxDraw=null;sirenInk=null;ghostsLoaded=false;ghostPilot=ghostGloves=null;ghostHats.Clear();}
        private static Vector3 Src(Vector3 v)=>new Vector3(-v.x,v.y,v.z);
        // A Unity transform matrix in the source frame (x mirrored): S·M·S, row-major into o at offset.
        private static void Source(Matrix4x4 m,double[] o,int offset)
        {for(int r=0;r<4;r++)for(int c=0;c<4;c++)o[offset+r*4+c]=((r==0)^(c==0))?-m[r,c]:m[r,c];}
        private static Quaternion MirrorEuler(double x,double y,double z){NativeFxMath.EulerQuaternion(x,y,z,out double qx,out double qy,out double qz,out double qw);return new Quaternion((float)qx,-(float)qy,-(float)qz,(float)qw);}
        private static bool Once(SirenTrack t,int bit,float now,float at){if((t.Fired&bit)!=0||now<at)return false;t.Fired|=bit;return now-at<.4f;}
        private double SirenRanged(int seat,bool ranged,float now)
        {if(!sirenTracks.TryGetValue(seat,out var t))return ranged?1:0;return NativeSirenView.Ranged(ranged,now,t.Ranged==ranged?t.ModeAt:now);}

        /// <summary>C12 entry point, after the rigs are posed (Render) and the shot tracker has begun (RenderGlow).</summary>
        private void DevEffects(NativeArena arena,Camera camera,float now)
        {
            if(sourceModels==null||arena==null||camera==null)return;sirenLookCamera=camera;EnsureSirenFx();if(fxDraw==null)fxDraw=new NativeFxDraw();
            if(arena!=fxArena||arena.Generation!=fxGeneration||arena.Match.Set!=fxSet||arena.Match.Round!=fxRound||now<fxLast-.25f)
            {ClearDevEffects();fxArena=arena;fxGeneration=arena.Generation;fxSet=arena.Match.Set;fxRound=arena.Match.Round;}
            fxLast=now;fxFrame++;var fx=sirenFx;fx.Begin(now,BlastQuality);fxDraw.Clear();sirenChar.Clear();
            SirenPilots(arena,now);SirenHits(arena,now);SirenShots(arena,camera,now);SirenFields(arena,now);fx.Tick();DrawSirenFx(camera);SirenMarks(arena,camera,now);SirenViewModel(arena,now);DevFighters(arena,camera,now);
            fxDraw.Draw(camera,ViewCamera,now);
        }
        private NativePilot SeatPilot(NativeArena arena,int seat){var list=arena.Pilots;for(int i=0;i<list.Count;i++)if(list[i].Seat.id==seat)return list[i];return null;}
        // The held victim's explicit identity survives snapshots, joins and replays; nearby bystanders are never guessed.
        private NativePilot SirenVictim(NativeArena arena,NativePilot p,SirenTrack t,Vector3 body,Quaternion rot,float k,float now)
        {
            if(!arena.Replica&&p.Siren?.Move?.Caught!=null)return p.Siren.Move.Caught;
            foreach(var q in arena.Pilots)if(q.Seat.connected&&q.Pose?.GrabOwner==p.Seat.id&&NativeSirenVictimPose.Active(q.Pose,now))return q;
            return null;
        }
        private void SirenPilots(NativeArena arena,float now)
        {
            var fx=sirenFx;double dt=fx.Dt,q=BlastQuality;var view=arena.ViewPilot;
            foreach(var p in arena.Pilots)
            {
                var a=p.Pose;if(a==null||a.SourceSpec!="siren"||!p.Seat.connected)continue;int seat=p.Seat.id;
                if(!sirenTracks.TryGetValue(seat,out var t)){t=new SirenTrack{Ranged=a.SourceRanged};sirenTracks.Add(seat,t);}
                t.Frame=fxFrame;t.Ranged=a.SourceRanged;t.ModeAt=a.SourceModeAt;
                rigs.TryGetValue(seat,out var rig);bool own=FirstPerson&&view!=null&&view.Seat.id==seat;
                Vector3 body=rig!=null&&!own?rig.Root.transform.position:p.Body.position;float k=Mathf.Max(.1f,a.RigScale);Quaternion rot=Quaternion.Euler(0,a.Yaw,0);
                string action=string.IsNullOrEmpty(a.SourceAction)?null:a.SourceAction;
                if(action!=t.Action||a.SourceAt!=t.At)
                {
                    // The previous move ended: a hold that lasted to the crush; a laugh that caught someone.
                    if(t.Action=="grab"&&!t.Crushed&&now>=t.CrushAt-.1f){t.Crushed=true;var v=SeatPilot(arena,t.Victim);if(v!=null&&now-(t.CrushAt)<.4f){Vector3 h=v.Body.position+Vector3.up*.65f;fx.Event(NativeSirenEvent.Crush,-h.x,h.y,h.z,k:k,born:t.CrushAt);}}
                    bool caught=t.Action=="laugh"&&action=="grab";t.Action=action;t.At=a.SourceAt;t.Fired=0;t.Side=a.SourceSide>=0?1:-1;
                    if(action=="grab"){t.CaughtAt=a.SourceAt;t.CrushAt=a.SourceAt+3.2f;t.Crushed=false;t.Victim=0;var v=SirenVictim(arena,p,t,body,rot,k,now);t.Victim=v?.Seat.id??0;if(v?.Pose?.GrabOwner==p.Seat.id)t.CrushAt=v.Pose.GrabCrushAt;
                        if(caught&&v!=null&&now-t.CaughtAt<.4f){Vector3 c=v.Body.position;fx.Event(NativeSirenEvent.Catch,-c.x,c.y,c.z,k:k,born:t.CaughtAt);}}
                }
                SirenMove(arena,p,t,action,body,rot,k,now);
                // motion.ts: the body's black fire (none while dead; the first-person Siren hides its own body).
                if(p.Hp<=0||rig==null||own)continue;
                for(int b=0;b<rig.Bones.Length&&b<22;b++)Source(rig.Bones[b].localToWorldMatrix,fxJoints,b*16);
                Vector3 vel=NativeArena.Vec(a.Velocity);float fwd=Vector3.Dot(new Vector3(vel.x,0,vel.z),rot*Vector3.forward);t.Lean+=(Mathf.Clamp(fwd/6.8f,-1,1)-t.Lean)*(1-Mathf.Exp(-(float)dt*18));
                double flare=action=="hellfire"?NativeSirenBody.Flare(now,t.At+.16):action=="damnation"?NativeSirenBody.Flare(now,t.At+.55):0;
                if(action=="damnation"){double dm=NativeSirenBody.Charge(now,t.At,t.At+.55);if(dm>=0){Source(rig.Root.transform.localToWorldMatrix,fxRoot,0);NativeSirenBody.ChargeFlames(fx.Fire,fxRoot,dm,dt,q);}}
                t.Body.Emit(fx.Fire,fxJoints,dt,q,-vel.x,vel.z,action=="grab",flare,NativeSirenView.Ranged(t.Ranged,now,t.ModeAt),now,t.Lean);
            }
            sirenStale.Clear();foreach(var pair in sirenTracks)if(pair.Value.Frame!=fxFrame)sirenStale.Add(pair.Key);foreach(int seat in sirenStale)sirenTracks.Remove(seat);
        }
        // fx.ts event table, from the move's rule timings (Core/NativeSirenRules), at the Siren's centre unless noted.
        private void SirenMove(NativeArena arena,NativePilot p,SirenTrack t,string action,Vector3 body,Quaternion rot,float k,float now)
        {
            var fx=sirenFx;if(action==null)return;float at=t.At;
            switch(action)
            {
                case "swipe":case "rend":
                {
                    // The crescent pivots .38 m above the Siren's centre and turns with its facing at the hit (the source's own
                    // frame-displacement yaw collapses to world +z when the Siren stands still; DEV_LOG C12).
                    bool rend=action=="rend";float hit=at+(rend?.42f:.12f);
                    if(Once(t,1,now,hit)){Vector3 pivot=body+Vector3.up*.38f;fx.Slash(rend,t.Side,-pivot.x,pivot.y,pivot.z,-p.Pose.Yaw*Mathf.Deg2Rad,hit);}
                    break;
                }
                case "slam":if(Once(t,1,now,at+.38f))fx.Event(NativeSirenEvent.Slam,-body.x,body.y-.865f,body.z,3.6*k,born:at+.38f);break;
                case "rush":if(Once(t,1,now,at+.14f))fx.Event(NativeSirenEvent.Rush,-body.x,body.y,body.z,k:k,born:at+.14f);break;
                case "laugh":
                    if(Once(t,1,now,at))fx.Event(NativeSirenEvent.Laugh,-body.x,body.y,body.z,k:k,born:at);
                    if(now>=at&&now<at+2)fx.Aura(p.Seat.id,-body.x,body.y-.83*k,body.z,k);
                    if(Once(t,2,now,at+2))fx.Event(NativeSirenEvent.Lunge,-body.x,body.y,body.z,k:k,born:at+2);
                    if(Once(t,4,now,at+2.35f))fx.Event(NativeSirenEvent.Whiff,-body.x,body.y,body.z,k:k,born:at+2.35f);
                    break;
                case "mark":
                    if(Once(t,1,now,at))fx.Event(NativeSirenEvent.Laugh,-body.x,body.y,body.z,k:k,born:at);
                    if(Once(t,2,now,at+2.4f)){Vector3 s=body+rot*new Vector3(-.3f,.7f,.7f)*k;fx.Event(NativeSirenEvent.Cast,-s.x,s.y,s.z,born:at+2.4f);}
                    break;
                case "hellfire":if(Once(t,1,now,at+.16f)){Vector3 s=body+rot*new Vector3(t.Side==-1?.36f:-.36f,.62f,.52f)*k;fx.Event(NativeSirenEvent.Cast,-s.x,s.y,s.z,born:at+.16f);}break;
                case "damnation":if(Once(t,1,now,at+.55f)){Vector3 s=body+rot*new Vector3(0,1.42f,.12f)*k;fx.Event(NativeSirenEvent.Cast,-s.x,s.y,s.z,damnation:true,born:at+.55f);}break;
                case "grab":
                {
                    // Last Laugh: black flame climbs over the held victim (its rig) and chars it until the crush.
                    var v=SirenVictim(arena,p,t,body,rot,k,now)??(now>=t.CrushAt?SeatPilot(arena,t.Victim):null);if(v!=null)t.Victim=v.Seat.id;
                    if(v!=null&&v.Hp>0&&now<t.CrushAt&&rigs.TryGetValue(v.Seat.id,out var vr)&&!(FirstPerson&&arena.ViewPilot==v))
                    {for(int b=0;b<vr.Bones.Length&&b<22;b++)Source(vr.Bones[b].localToWorldMatrix,fxJoints,b*16);fx.Grab(v.Seat.id,now-t.CaughtAt,fxJoints);sirenChar[v.Seat.id]=(float)NativeSirenFx.Burn(now-t.CaughtAt);}
                    if(Once(t,8,now,t.CrushAt)&&v!=null){t.Crushed=true;Vector3 h=v.Body.position+Vector3.up*.65f;fx.Event(NativeSirenEvent.Crush,-h.x,h.y,h.z,k:k,born:t.CrushAt);}
                    else if(now>=t.CrushAt)t.Crushed=true;
                    break;
                }
            }
        }
        // Damage from a Siren seat: the source's siren-hit burst at the target.
        private void SirenHits(NativeArena arena,float now)
        {
            int newest=0;foreach(var e in arena.Events)newest=Math.Max(newest,e.Id);if(newest<fxEvent)fxEvent=0;
            foreach(var e in arena.Events)
            {
                if(e.Id<=fxEvent||e.Kind!=EventKind.Hit&&e.Kind!=EventKind.Headshot||now-e.Time>=.4f||!sirenTracks.ContainsKey(e.Source))continue;
                sirenFx.Event(NativeSirenEvent.Hit,-e.Position.x,e.Position.y,e.Position.z,born:e.Time);
            }
            fxEvent=Math.Max(fxEvent,newest);
        }
        private static NativeSirenShotKind ShotKind(string kind)=>kind=="damnation"?NativeSirenShotKind.Damnation:kind=="seeker"?NativeSirenShotKind.Seeker:kind=="drop"?NativeSirenShotKind.Drop:NativeSirenShotKind.Hellfire;
        private static float KindRadius(NativeSirenShotKind kind)=>kind==NativeSirenShotKind.Damnation?.42f:kind==NativeSirenShotKind.Seeker?.32f:kind==NativeSirenShotKind.Drop?.12f:.18f;
        // fx.ts updateShots: trails, coronas, the orb cores and halos, the drop marker; impacts when a shot is gone.
        private void SirenShots(NativeArena arena,Camera camera,float now)
        {
            var fx=sirenFx;Vector3 right=camera.transform.right,up=camera.transform.up;var draw=fxDraw;
            foreach(var b in arena.Bullets)
            {
                if(b.SirenKind==null&&b.Spec!="siren")continue;var kind=ShotKind(b.SirenKind);NativeShotViews.Bullet(arena,b,shotTracker,now,out var v);
                if(!sirenShots.ContainsKey(b.Id))sirenShots[b.Id]=new SirenShotMemo{Owner=b.Owner,Kind=kind,Reach=b.Radius/KindRadius(kind)};
                fx.Shot(b.Id,kind,b.Born,-v.X,v.Y,v.Z,-v.VX,v.VY,v.VZ);
                Vector3 at=new Vector3((float)v.X,(float)v.Y,(float)v.Z),vel=new Vector3((float)v.VX,(float)v.VY,(float)v.VZ);double pulse=NativeSirenFx.ShotPulse(kind,now);float age=now-b.Born;
                if(kind==NativeSirenShotKind.Seeker)
                {
                    draw.Sprite(draw.Effects,at,right,up,(float)(2.6+.7*pulse),Alpha(Color.white,.95),NativeFxDraw.Authored+NativeFxDraw.Additive);
                    draw.Sprite(draw.Effects,at,right,up,1.3f,Alpha(NativeArena.ColorOf(NativeSirenFx.Warn),.55),NativeFxDraw.Authored+NativeFxDraw.Additive);
                    draw.Orb(draw.Solid,Matrix4x4.TRS(at,MirrorEuler(0,age*2,0),Vector3.one*(float)(.42*(1+.08*pulse))),SeekerCore,Color.white,3.2f);
                }
                else if(kind==NativeSirenShotKind.Drop)
                {
                    draw.Sprite(draw.Effects,at,right,up,.6f,Alpha(NativeArena.ColorOf(0xd6ccff),.3),NativeFxDraw.Authored+NativeFxDraw.Additive);
                    var along=vel.sqrMagnitude>0?Quaternion.FromToRotation(Vector3.up,-vel.normalized):Quaternion.identity;draw.Orb(draw.Solid,Matrix4x4.TRS(at,along,new Vector3(.09f,.24f,.09f)),InkRaw,AshRaw,1.8f);
                    // The landing marker on the rain's floor (drops fall straight down at 10 m/s).
                    if(RainFloor(arena,b.Owner,at,out float floor)){float u=Mathf.Clamp01(1-(at.y-floor)/9f);draw.Ring(draw.Effects,new Vector3(at.x,floor+.05f,at.z),.55f,.75f-.5f*u,Alpha(WarnRaw,.35f+.6f*u),0);}
                }
                else
                {
                    bool big=kind==NativeSirenShotKind.Damnation;
                    draw.Sprite(draw.Effects,at,right,up,(float)((big?3.4:1.5)*(1+.15*pulse)),Alpha(NativeArena.ColorOf(0x8f78e8),.55),NativeFxDraw.Authored+NativeFxDraw.Additive);
                    draw.Orb(draw.Solid,Matrix4x4.TRS(at,MirrorEuler(age*6,age*4,0),Vector3.one*(float)((big?.52:.27)*(1+.1*pulse))),InkRaw,AshRaw,1.8f);
                }
            }
            // A shot gone this frame: Damnation explodes, a drop splashes, a seeker bursts on its target (or puffs out).
            for(int i=0;i<fx.Shots.Count;i++)
            {
                var s=fx.Shots[i];if(s.Seen==fx.Frame||!sirenShots.TryGetValue((int)s.Id,out var memo))continue;var owner=SeatPilot(arena,memo.Owner);if(owner==null||owner.Hp<=0)continue;
                if(s.Kind==NativeSirenShotKind.Damnation)fx.Event(NativeSirenEvent.Explode,s.LastX,s.LastY,s.LastZ,3.5*memo.Reach);
                else if(s.Kind==NativeSirenShotKind.Drop)fx.Event(NativeSirenEvent.Drop,s.LastX,s.LastY,s.LastZ);
                else if(s.Kind==NativeSirenShotKind.Seeker)fx.Event(SeekerLanded(arena,memo.Owner,new Vector3(-(float)s.LastX,(float)s.LastY,(float)s.LastZ),now)?NativeSirenEvent.SeekerHit:NativeSirenEvent.SeekerOut,s.LastX,s.LastY,s.LastZ);
            }
            fx.EndShots();
            sirenStale.Clear();foreach(var pair in sirenShots){bool live=false;for(int i=0;i<fx.Shots.Count;i++)if(fx.Shots[i].Id==pair.Key){live=true;break;}if(!live)sirenStale.Add(pair.Key);}
            foreach(int id in sirenStale)sirenShots.Remove(id);
        }
        private static bool SeekerLanded(NativeArena arena,int owner,Vector3 at,float now)
        {foreach(var e in arena.Events)if(e.Source==owner&&(e.Kind==EventKind.Hit||e.Kind==EventKind.Headshot)&&now-e.Time<.3f&&(NativeArena.Vec(e.Position)-at).sqrMagnitude<4)return true;return false;}
        private static bool RainFloor(NativeArena arena,int owner,Vector3 at,out float floor)
        {
            floor=0;float best=float.MaxValue;
            foreach(var f in arena.SirenFields){if(f.Kind!="rain"||f.Owner?.Seat.id!=owner)continue;float d=new Vector2(f.Position.x-at.x,f.Position.z-at.z).sqrMagnitude;if(d<best){best=d;floor=f.Position.y;}}
            return best<float.MaxValue&&at.y>=floor;
        }
        // fx.ts updatePatches/updateEruptions/updateRains: the flames; C9 draws the scorch decals and the warning ring/fill.
        private void SirenFields(NativeArena arena,float now)
        {
            var fx=sirenFx;
            foreach(var f in arena.SirenFields)
            {
                bool first=!sirenFieldFlags.TryGetValue(f.Id,out var flags);double x=-f.Position.x,y=f.Position.y,z=f.Position.z;int bits=first?0:flags.x;
                switch(f.Kind)
                {
                    case "hellfire":case "trail":fx.Patch(f.Id,f.Kind=="trail"?1:0,x,y,z,f.Radius,f.At,f.Until);break;
                    case "eruption":
                    {
                        if(first&&now-f.At<.4f)fx.Event(NativeSirenEvent.Cast,x,y,z,born:f.At);
                        var e=fx.Eruption(f.Id,x,y,z,f.Radius,f.At,f.At+NativeFieldRules.EruptionWarning,f.Until);
                        if((bits&2)==0&&now>=f.At+NativeFieldRules.EruptionWarning){bits|=2;if(now-f.At-NativeFieldRules.EruptionWarning<.4f)fx.Event(NativeSirenEvent.Eruption,x,y,z,f.Radius,born:f.At+(float)NativeFieldRules.EruptionWarning);}
                        if(e.Column){NativeFxMath.Compose(fxMatrix,x,y,z,0,e.Spin,0,e.Width,e.Height,e.Width);fxDraw.Shell(fxDraw.Effects,fxMatrix,true,(float)e.Fade,1.6f,InkRaw,AshRaw);}
                        break;
                    }
                    case "rain":if(first&&now-f.At<.4f)fx.Event(NativeSirenEvent.Cast,x,y,z,born:f.At);fx.Rain(f.Id,x,y,z,f.Radius,f.At,f.Until);break;
                }
                sirenFieldFlags[f.Id]=new Vector2Int(bits|1,fxFrame);
            }
            fx.EndPatches();fx.EndEruptions();fx.EndRains();
            sirenStale.Clear();foreach(var pair in sirenFieldFlags)if(pair.Value.y!=fxFrame)sirenStale.Add(pair.Key);foreach(int id in sirenStale)sirenFieldFlags.Remove(id);
        }
        // The flames (unlit opaque tongues), then the one-shot pieces: floor rings, flashes, cracks, slashes, flame shells.
        private void DrawSirenFx(Camera camera)
        {
            var fx=sirenFx;var f=fx.Fire;var draw=fxDraw;bool flat=HaloReduced;float rim=(float)NativeSirenLook.FlameRim;
            for(int i=0;i<f.Capacity;i++)
            {
                if(!f.Pose(i,out double x,out double y,out double z,out double qx,out double qy,out double qz,out double qw,out double sx,out double sy,out double sz,out double r,out double g,out double b))continue;
                Vector3 at=new Vector3(-(float)x,(float)y,(float)z),size=new Vector3((float)sx,(float)sy,(float)sz);var turn=new Quaternion((float)qx,-(float)qy,-(float)qz,(float)qw);var color=new Color((float)r,(float)g,(float)b,1);
                // Black flame with a faint ash rim (NativeSirenLook.FlameRim), so it reads on dark maps; reduced effects draw it flat.
                if(flat)draw.Cube(draw.Solid,at,turn,size,color);else draw.FlameCube(draw.Solid,at,turn,size,color,AshRaw,rim);
            }
            Vector3 right=camera.transform.right,up=camera.transform.up;
            for(int i=0;i<fx.Count;i++)
            {
                var t=fx[i];double u=Math.Max(0,(fx.Time-t.Born)/t.Life);if(u>=1)continue;Vector3 at=new Vector3(-(float)t.X,(float)t.Y,(float)t.Z);
                switch(t.Kind)
                {
                    case NativeSirenFxKind.Shock:{NativeSirenFx.ShockLook(t,u,out double s,out double o);draw.Ring(draw.Effects,at+Vector3.up*.04f,t.Dark?.55f:.86f,(float)s,t.Dark?new Color(0,0,0,(float)o):Alpha(AshRaw,o),t.Dark?0:NativeFxDraw.Additive);break;}
                    case NativeSirenFxKind.Flash:{NativeSirenFx.FlashLook(t,u,out double s,out double o);draw.Sprite(draw.Effects,at,right,up,(float)s,Alpha(NativeArena.ColorOf(NativeSirenFx.FlashColor),o),NativeFxDraw.Authored+NativeFxDraw.Additive);break;}
                    case NativeSirenFxKind.Slash:{NativeSirenFx.SlashUniforms(t.Rend,u,out double sweep,out double fade);fx.SlashMatrix(t,fxMatrix);draw.Crescent(draw.Effects,fxMatrix,(float)sweep,(float)fade,InkRaw,AshRaw);break;}
                    case NativeSirenFxKind.Explosion:{NativeSirenFx.ExplosionLook(t,u,out double s,out double turn,out double fade);NativeFxMath.Compose(fxMatrix,t.X,t.Y+.2,t.Z,0,turn,0,s,s*.85,s);draw.Shell(draw.Effects,fxMatrix,false,(float)fade,2.2f,InkRaw,AshRaw);break;}
                    case NativeSirenFxKind.Cracks:
                    {
                        NativeSirenFx.CrackLook(u,out double s,out double o);var black=new Color(0,0,0,(float)o);
                        for(int c=0;c<NativeSirenFx.Cracks;c++)
                        {
                            int k=(t.Crack*NativeSirenFx.Cracks+c)*3;double a=fx.CrackBoxes[k],len=fx.CrackBoxes[k+1]*s,w=fx.CrackBoxes[k+2]*s,sin=Math.Sin(a),cos=Math.Cos(a);
                            // A box turned by a about y, centred half its length out; its top face (.01·scale up) is what shows.
                            double cx=t.X+sin*len/2,cz=t.Z+cos*len/2;float top=(float)(t.Y+.01*s);
                            Vector3 Corner(double lx,double lz)=>new Vector3(-(float)(cx+lx*cos+lz*sin),top,(float)(cz-lx*sin+lz*cos));
                            draw.Quad(draw.Effects,Corner(-w/2,-len/2),Corner(w/2,-len/2),Corner(w/2,len/2),Corner(-w/2,len/2),black,NativeFxDraw.Authored);
                        }
                        break;
                    }
                }
            }
        }
        // fx.ts updateMarks (NativeSirenMark): Marked's "You...." crosshair on the marked player from lockAt to the throw, then
        // only while its seeker flies (gone on impact, not at the move's recovery end; NativeSirenMarkTracker.Grace), in the
        // warn red over everything (the overlay layer), turned with the camera. The records are fed before the seekers. The marked
        // player's own first-person view holds it in front of the eye (NativeSirenMark.ScreenReach). Host, guests and the
        // killcam read the same mark records (NativeArena.LiveMarks) and seekers (NativeSirenMarkTracker); every viewer sees it.
        private void SirenMarks(NativeArena arena,Camera camera,float now)
        {
            var tracker=markTracker;tracker.Begin();var marks=arena.LiveMarks();
            for(int i=0;i<marks.Count;i++){var m=marks[i];tracker.Record(m.owner,m.target,m.lockAt,m.throwAt,Markable(SeatPilot(arena,m.target)),now);}
            foreach(var b in arena.Bullets)
            {
                if(b.SirenKind!="seeker")continue;
                int target=b.SirenTarget!=0?b.SirenTarget:tracker.Remembered(b.Owner);if(target==0)target=SeekerTarget(arena,b);
                tracker.Seeker(b.Owner,target,b.Born,Markable(target!=0?SeatPilot(arena,target):null),now);
            }
            tracker.End();if(tracker.Count==0)return;
            var cam=camera.transform;var view=arena.ViewPilot;var layer=fxDraw.Overlay;
            for(int i=0;i<tracker.Count;i++)
            {
                var m=tracker[i];var target=SeatPilot(arena,m.Target);if(target==null)continue;
                NativeSirenMark.Look(m.LockAt,m.ThrowAt,now,out double scale,out double spin,out double turn,out double opacity,out bool white);
                Vector3 center;float k=(float)scale;
                if(FirstPerson&&view!=null&&view.Seat.id==m.Target){center=cam.position+cam.forward*(float)NativeSirenMark.ScreenDistance;k*=(float)NativeSirenMark.ScreenUnit(camera.fieldOfView);}
                else center=(rigs.TryGetValue(m.Target,out var rig)&&rig!=null?rig.Root.transform.position:target.Body.position)+Vector3.up*(float)NativeSirenMark.Lift;
                Vector3 right=cam.right*k,up=cam.up*k;Color color=Alpha(white?Color.white:WarnRaw,opacity);
                fxDraw.Annulus(layer,center,right,up,(float)NativeSirenMark.RingInner,1,NativeSirenMark.RingSegments,(float)spin,color,0);
                fxDraw.Annulus(layer,center,right,up,(float)NativeSirenMark.SquareInner,(float)NativeSirenMark.SquareOuter,NativeSirenMark.SquareSegments,(float)NativeSirenMark.SquareTurn,color,0);
                for(int n=0;n<NativeSirenMark.TickCount;n++)
                {
                    NativeSirenMark.Tick(n,turn,out double x,out double y,out double a);float s=(float)Math.Sin(a),c=(float)Math.Cos(a);
                    Vector3 at=center+right*(float)x+up*(float)y,along=(right*s+up*c)*(float)(NativeSirenMark.TickLength/2),across=(right*c-up*s)*(float)(NativeSirenMark.TickWidth/2);
                    // The TICK box's front and back faces both blend (DoubleSide), so each tick is laid twice.
                    for(int face=0;face<2;face++)fxDraw.Quad(layer,at-along-across,at-along+across,at+along+across,at+along-across,color,0);
                }
            }
        }
        private static bool Markable(NativePilot p)=>p!=null&&p.Hp>0&&p.Seat.connected;
        // A seeker with no record seen (a killcam that opens after the throw, a late join): the live player it flies at.
        private static int SeekerTarget(NativeArena arena,NativeBullet b)
        {
            int best=0;double near=NativeSirenMark.AimCos;
            foreach(var p in arena.Pilots)
            {
                if(p.Seat.id==b.Owner||!Markable(p))continue;Vector3 aim=p.Body.position+Vector3.up*(float)NativeSirenMark.SeekerAim;
                double cos=NativeSirenMark.Aim(b.Position.x,b.Position.y,b.Position.z,b.Velocity.x,b.Velocity.y,b.Velocity.z,aim.x,aim.y,aim.z);if(cos>near){near=cos;best=p.Seat.id;}
            }
            return best;
        }
        // first-person.ts: inked arms (NativeSourcePresentation), claws that retract into burning palm orbs, hand flames
        // riding with the view rig; restored to the normal view model for anyone else.
        private void SirenViewModel(NativeArena arena,float now)
        {
            var p=arena.ViewPilot;bool on=FirstPerson&&viewRig!=null&&ViewCamera!=null&&!ScopeActive&&p!=null&&p.Hp>0&&p.Pose?.SourceSpec=="siren";
            float dt=Mathf.Clamp(now-viewLast,0,.05f);viewLast=now;
            if(!on){if(viewFlames!=null&&viewFlames.Live>0)viewFlames.Clear();viewAcc=0;return;}
            if(viewFlames==null)viewFlames=new NativeFlameField(NativeSirenFx.ViewCapacity,fxNext);
            double ranged=SirenRanged(p.Seat.id,p.Pose.SourceRanged,now);var root=viewRig.Root.transform;var inverse=root.worldToLocalMatrix;var draw=fxDraw;
            Source(inverse*viewRig.Bones[CapturedRig.LHand].localToWorldMatrix,fxLeft,0);Source(inverse*viewRig.Bones[CapturedRig.RHand].localToWorldMatrix,fxRight,0);
            viewAcc=NativeSirenView.Flames(viewFlames,viewAcc,dt,ranged,fxLeft,fxRight);viewFlames.Update(dt);
            float scale=root.lossyScale.x;
            for(int i=0;i<viewFlames.Capacity;i++)
            {
                if(!viewFlames.Pose(i,out double x,out double y,out double z,out double qx,out double qy,out double qz,out double qw,out double sx,out double sy,out double sz,out double r,out double g,out double b))continue;
                draw.Cube(draw.ViewSolid,root.TransformPoint(new Vector3(-(float)x,(float)y,(float)z)),root.rotation*new Quaternion((float)qx,-(float)qy,-(float)qz,(float)qw),new Vector3((float)sx,(float)sy,(float)sz)*scale,new Color((float)r,(float)g,(float)b,1));
            }
            for(int side=1;side>=-1;side-=2)
            {
                var hand=viewRig.Bones[side>0?CapturedRig.LHand:CapturedRig.RHand].localToWorldMatrix;
                if(ranged<.95)
                {
                    var group=hand*Matrix4x4.TRS(new Vector3(0,-.1f,.06f),Quaternion.identity,new Vector3(1,1-.75f*(float)ranged,1));
                    foreach(double cx in NativeSirenView.ClawX)
                    {
                        var turn=MirrorEuler(.35,0,cx*2.2*side);float x=-(float)(cx*1.2);
                        draw.Box(draw.ViewSolid,group*Matrix4x4.TRS(new Vector3(x,-.2f,.06f),turn,new Vector3(.03f,.42f,.018f)),ClawInk,NativeFxDraw.OrbMode,NativeFxDraw.Authored,new Vector4(0,0,0,.8f),new Vector4(AshAuthored.r,AshAuthored.g,AshAuthored.b,0));
                        draw.Box(draw.ViewSolid,group*Matrix4x4.TRS(new Vector3(x,-.2f,.072f),turn,new Vector3(.006f,.4f,.004f)),AshRaw*.55f,NativeFxDraw.Flat,0,Vector4.zero,Vector4.zero);
                    }
                }
                if(ranged>.05)draw.Orb(draw.ViewSolid,hand*Matrix4x4.TRS(new Vector3(0,-.13f,.07f),Quaternion.identity,Vector3.one*(float)NativeSirenView.Orb(side>0?0:1,now,ranged)),InkRaw,AshRaw,1.6f);
            }
        }
        /// <summary>first-person.ts pose: forearms forward and wrists cocked so the claws (or burning palms) point ahead.</summary>
        private void SirenViewArms(Actor a,float time)
        {
            double ranged=SirenRanged(a.Id,a.SourceRanged,time);
            for(int side=1;side>=-1;side-=2)
            {
                NativeSirenView.Arm(side,time,ranged,out double ax,out double az,out double ex,out double hx,out double hz);
                viewRig.Bones[side>0?CapturedRig.LArm:CapturedRig.RArm].localRotation=MirrorEuler(ax,0,az);
                viewRig.Bones[side>0?CapturedRig.LElbow:CapturedRig.RElbow].localRotation=MirrorEuler(ex,0,0);
                viewRig.Bones[side>0?CapturedRig.LHand:CapturedRig.RHand].localRotation=MirrorEuler(hx,0,hz);
            }
        }
    }
}

