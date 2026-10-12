using System;
using System.Collections.Generic;
using UnityEngine;
using NVector3=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeSourceModels
    {
        private Material afterimageMaterial;private bool afterimageFailed;
        /// <summary>
        /// A model drawn as the roll afterimage (Resources/NativeWeaponGhost.shader: an additive fresnel ghost; colour and opacity
        /// in the body tint), sharing the source meshes; null when the model or the shader is missing.
        /// </summary>
        public string Afterimage(string model)
        {
            if(string.IsNullOrEmpty(model))return null;string id="afterimage-"+model;if(models.ContainsKey(id))return id;if(!models.TryGetValue(model,out var source))return null;
            if(afterimageMaterial==null)
            {
                if(afterimageFailed)return null;var shader=Resources.Load<Shader>("NativeWeaponGhost");if(shader==null||!shader.isSupported){afterimageFailed=true;return null;}
                afterimageMaterial=new Material(shader){enableInstancing=true,name="Native roll afterimage"};materials.Add(afterimageMaterial);
            }
            var parts=new Part[source.Length];for(int i=0;i<parts.Length;i++)parts[i]=new Part{Name=source[i].Name,Bone=source[i].Bone,Mesh=source[i].Mesh,Material=afterimageMaterial,NoShadow=true};
            models[id]=parts;return id;
        }
    }

    // The weapons milestone's arena hooks (Core/NativeWeaponFx.cs for the rules, NativeWeaponFxDraw.cs for the geometry): muzzle
    // stars (WeaponDressing), slash crescents and roll afterimages (after the rigs are posed, RenderMeleeTrails), the hit flash
    // (SourceCharacter), the spell charge streaks and rings (BodyDressing), and the tracers, energy trails and impacts
    // (RenderGlow, which submits the drawer). Everything reads the replicated actors, shots, events and seat stats every peer
    // holds, on the presentation clocks the other effects use; hit timing, hitboxes, damage, cooldowns, movement, animation
    // timing and the network frame are untouched.
    public sealed partial class ArenaPresentation
    {
        private sealed class SlashState{public readonly NativeMeleeSwing Swing=new NativeMeleeSwing();public string Spec;public float Range;public int Color;public Vector3 At;public int Id;}
        private sealed class Emitter
        {
            public Vector3 Origin,Last;public float LastAt,Carry;public long Emitted;public int Frame;public Color Halo;public float Radius;public long Seed;
            public readonly Vector3[] Points=new Vector3[NativeTrailFx.RibbonSamples];public readonly float[] Times=new float[NativeTrailFx.RibbonSamples];public int Count;
            public void Push(Vector3 at,float time){if(Count==Points.Length){Array.Copy(Points,1,Points,0,Count-1);Array.Copy(Times,1,Times,0,Count-1);Count--;}Points[Count]=at;Times[Count]=time;Count++;}
        }
        private struct TrailSprite{public Vector3 At,Drift;public float Born,Life,Size;public Color Color;}
        private struct ImpactMark{public Vector3 At,Normal;public float Born;public long Seed;public Color Tint;}
        private NativeWeaponFxDraw weaponFx;private Snapshot weaponSnapshot;private int weaponFrame;
        private readonly Dictionary<long,SlashState> slashes=new Dictionary<long,SlashState>();private readonly Stack<SlashState> spareSlashes=new Stack<SlashState>();private readonly List<long> staleSlashes=new List<long>();
        private readonly Dictionary<int,(float At,bool Head)> hitFlashes=new Dictionary<int,(float,bool)>();private int hitFlashEvent;
        private readonly NativeAfterimages afterimages=new NativeAfterimages();private GhostPose[] afterimagePoses;private readonly Color[] afterimageTints=new Color[NativeAfterimages.Slots];
        private readonly string[] afterimageWeapon=new string[NativeAfterimages.Slots];private readonly Dictionary<string,string> afterimageModels=new Dictionary<string,string>();
        private readonly Dictionary<long,Emitter> emitters=new Dictionary<long,Emitter>();private readonly Stack<Emitter> spareEmitters=new Stack<Emitter>();private readonly List<long> staleEmitters=new List<long>();
        private TrailSprite[] trailSprites=new TrailSprite[512];private int trailSpriteCount;
        private readonly ImpactMark[] impactMarks=new ImpactMark[32];private int impactNext,impactEvent;private NativeArena impactArena;private int impactGeneration=-1;
        private NativeArena emitterArena;private int emitterGeneration=-1;private float emitterClock=-1;
        private NativeWeaponFxDraw WeaponFxDraw=>weaponFx??=sourceModels!=null?new NativeWeaponFxDraw():null;

        /// <summary>Called first in Render: an empty drawer queue for the frame and this frame's hit events for the flash.</summary>
        private void BeginWeaponFx(Snapshot s)
        {
            weaponFx?.Begin();weaponSnapshot=s;weaponFrame++;if(s?.Events==null)return;
            int newest=0;foreach(var e in s.Events)newest=Math.Max(newest,e.Id);if(newest<hitFlashEvent){hitFlashEvent=0;hitFlashes.Clear();}
            foreach(var e in s.Events)
            {
                if(e.Id<=hitFlashEvent)continue;
                if((e.Kind==EventKind.Hit||e.Kind==EventKind.Headshot)&&e.Target>0&&NativeEventFeedback.SourceHit(e.Kind,e.Amount)&&e.Detail!="burn"&&e.Detail!="poison")
                {if(!hitFlashes.TryGetValue(e.Target,out var h)||e.Time>=h.At)hitFlashes[e.Target]=(e.Time,e.Kind==EventKind.Headshot);}
            }
            hitFlashEvent=Math.Max(hitFlashEvent,newest);
        }
        /// <summary>The struck pilot's flash code for its body parts this frame (1: none). Third-person rigs only.</summary>
        private float HitFlashCode(int id,float time)=>hitFlashes.TryGetValue(id,out var h)?NativeHitFlash.Code(time-h.At,h.Head,WeaponEffectsQuality):1;

        /// <summary>
        /// One gun rig's muzzle star at the 'muzzle' socket of its right hand (the source flash's frame), on the rig's camera
        /// (first person: the view-model camera, sized with the view weapon). Dims with the rig's block cooldown, as the source
        /// flash did. Each live star offers a light pulse to the combat glow's free projectile lights.
        /// </summary>
        private void MuzzleFx(CapturedAnimation.Player rig,Actor a,float time,bool first)
        {
            var draw=WeaponFxDraw;if(draw==null||!NativeMuzzleFx.Look(a.SourceSpec,out var look))return;double age=time-a.LastShot;if(!(age>=0&&age<NativeMuzzleFx.Span))return;
            var hand=rig.Bones[CapturedRig.RHand].localToWorldMatrix;Vector3 forward=hand.MultiplyVector(Vector3.forward);float scale=forward.magnitude;if(!(scale>1e-4f))return;
            Vector3 z=forward/scale,x=hand.MultiplyVector(Vector3.right).normalized,y=Vector3.Cross(z,x).normalized;
            Vector3 muzzle=hand.MultiplyPoint3x4(new Vector3(0,0,NativeWeaponDressing.MuzzleZ(a.SourceSpec)+.02f));
            float pulse=NativeWeaponFxShapes.Muzzle(draw,first,muzzle,x,y,z,scale,look,NativeWeaponFx.Seed(a.LastShot,a.Id),age,WeaponEffectsQuality,LowGraphics,rigFactor);
            if(pulse>0)combatGlow?.Pulse(muzzle,pulse);
        }

        // Slash crescents and roll afterimages, after the rigs are posed (RenderMeleeTrails). Both follow the trails setting, as
        // the swept ribbon the crescents replace does.
        private void WeaponFxFrame(float time)
        {
            var s=weaponSnapshot;if(s?.Actors==null||sourceModels==null)return;var draw=WeaponFxDraw;if(draw==null)return;
            if(!TrailsEnabled){foreach(var st in slashes.Values)spareSlashes.Push(st);slashes.Clear();afterimages.Clear();return;}
            foreach(var a in s.Actors)
            {
                if(a==null||!a.Alive||!NativeSlashFx.Covers(a.SourceSpec)||!(a.SwingAt>-90))continue;
                long key=(long)a.Id<<32|(uint)Mathf.RoundToInt(a.SwingAt*4096);if(slashes.ContainsKey(key))continue;
                var state=spareSlashes.Count>0?spareSlashes.Pop():new SlashState();SlashBegin(state,a);slashes.Add(key,state);
            }
            staleSlashes.Clear();float q=WeaponEffectsQuality;
            foreach(var pair in slashes)
            {
                var st=pair.Value;if(time<st.Swing.HitAt-1||time>st.Swing.HitUntil+1||!NativeSlashFx.Look(st.Spec,out var look)){staleSlashes.Add(pair.Key);continue;}
                if(visualPositions.TryGetValue(st.Id,out var at))st.At=at;
                bool any=NativeWeaponFxShapes.Crescent(draw,st.At,st.Swing,st.Range,st.Color,look,time,q,false);
                if(look.Twin)any|=NativeWeaponFxShapes.Crescent(draw,st.At,st.Swing,st.Range,st.Color,look,time,q,true);
                if(!any&&time>st.Swing.HitUntil)staleSlashes.Add(pair.Key);
            }
            foreach(long key in staleSlashes){spareSlashes.Push(slashes[key]);slashes.Remove(key);}
            Afterimages(s,time);
        }
        // The swing by the rules (NativeMeleeRules.Begin: arc, reach) from the replicated strike: its committed yaw, attack index
        // and hit window. Without the seat's stats (a pilot missing from the loadout arena) the weapon's own arc and range.
        private void SlashBegin(SlashState st,Actor a)
        {
            var pilot=LoadoutPilot(a.Id);var w=pilot?.Weapon;var sw=st.Swing;float yaw=a.CommitYaw*Mathf.Deg2Rad;int index=a.Combo+1;
            if(w!=null&&pilot.Stats!=null){var b=NativeMeleeRules.Begin(w,pilot.Stats,a.AttackAt,index,yaw);sw.Arc=b.Arc;st.Range=b.Range;}
            else{var cw=WeaponLoadout?.Content?.Weapon(a.SourceSpec);sw.Arc=cw!=null?(float)(a.SourceSpec=="sword"&&index%3==0?.8:cw.arc):1.6f;st.Range=cw!=null?(float)cw.range:2.5f;}
            sw.Spec=a.SourceSpec;sw.Index=index;sw.Yaw=yaw;sw.HitAt=a.SwingAt;sw.HitUntil=a.SwingUntil;sw.At=a.AttackAt;
            st.Spec=a.SourceSpec;st.Id=a.Id;st.At=Position(a);st.Color=NativeWeaponDressing.SweepColor(pilot?.Seat?.chips);
        }
        // Roll afterimages: a snapshot of each rolling third-person rig every NativeAfterimages.Spacing metres, drawn for its life
        // in the pilot's accent colour (body, and the weapon it holds).
        private void Afterimages(Snapshot s,float time)
        {
            string pilot=afterimageModels.TryGetValue("pilot",out var id)?id:(afterimageModels["pilot"]=sourceModels.Afterimage("pilot"));if(pilot==null)return;
            if(afterimagePoses==null){afterimagePoses=new GhostPose[NativeAfterimages.Slots];for(int i=0;i<afterimagePoses.Length;i++)afterimagePoses[i]=new GhostPose();}
            float q=WeaponEffectsQuality;
            foreach(var a in s.Actors)
            {
                if(!NativeAfterimages.Eligible(a,time)||FirstPerson&&a.Id==LocalId||!rigs.TryGetValue(a.Id,out var rig))continue;
                var root=rig.Root.transform.position;var at=new NVector3(root.x,root.y,root.z);
                if(!afterimages.Due(a.Id,at,a.RollAt,time,q))continue;int slot=afterimages.Take(a.Id,at,a.RollAt,time,q);var pose=afterimagePoses[slot];
                pose.Root=rig.Root.transform.localToWorldMatrix;for(int b=0;b<22&&b<rig.Bones.Length;b++)pose.Bones[b]=rig.Bones[b].localToWorldMatrix;
                string weapon=null;if(!a.WeaponAway&&NativeWeaponDressing.SocketShown(a.SourceSpec)){string w=SkinnedHeld(a);if(!afterimageModels.TryGetValue(w,out weapon))afterimageModels[w]=weapon=sourceModels.Afterimage(w);}
                afterimageWeapon[slot]=weapon;pose.Armed=weapon!=null;
                if(pose.Armed){float reach=WeaponReach(rig,a,time);for(int b=0;b<22&&b<rig.Bones.Length;b++)pose.Weapon[b]=SourceWeaponBone(rig,a,time,false,b,reach);}
                afterimageTints[slot]=NativeWeaponFxShapes.Fx(NativeAfterimages.Color(a.AccentColor));
            }
            for(int k=0;k<NativeAfterimages.Slots;k++)
            {
                if(!afterimages.Live(k,time))continue;float opacity=(float)NativeAfterimages.Fade(time-afterimages.At[k]);if(opacity<=.002f)continue;
                var pose=afterimagePoses[k];var tint=afterimageTints[k];
                sourceModels.Queue(pilot,pose.Root,pose.Bone,null,0,null,tint,null,opacity);
                if(pose.Armed)sourceModels.Queue(afterimageWeapon[k],pose.Root,pose.WeaponBone,null,0,null,tint,null,opacity);
            }
        }
        /// <summary>The charge orb's converging streaks and shrinking rings round the socket (the orb's own frame, so they follow
        /// its growth and the view model's size), in the school colour, on the rig's camera.</summary>
        private void ChargeFx(Matrix4x4 socket,Actor a,int color,float time,bool first)
        {
            var draw=WeaponFxDraw;if(draw==null||color<0)return;float scale=socket.MultiplyVector(Vector3.right).magnitude;if(!(scale>1e-4f))return;
            NativeWeaponFxShapes.Charge(draw,first,socket.MultiplyPoint3x4(Vector3.zero),scale,NativeWeaponFxShapes.Lin(color),a.Id*7919L+(long)Mathf.Round(a.AttackAt*64),time,BlastQuality,LowGraphics);
        }

        /// <summary>
        /// The tracers, energy trails and impacts on the field clock the shots are drawn at; then the drawer is submitted to the
        /// match camera and, in first person, the view-model camera. RenderGlow calls it on both of its paths.
        /// </summary>
        private void WeaponFx(NativeArena arena,Camera camera,float now)
        {
            var draw=WeaponFxDraw;if(draw==null)return;draw.Low=LowGraphics;
            if(!ReferenceEquals(emitterArena,arena)||emitterGeneration!=arena.Generation||now<emitterClock-.25f){ClearEmitters();emitterArena=arena;emitterGeneration=arena.Generation;}
            emitterClock=Math.Max(emitterClock,now);
            float q=BlastQuality;bool low=LowGraphics,trails=TrailsEnabled;Vector3 eye=camera!=null?camera.transform.position:Vector3.zero;
            foreach(var b in arena.Bullets){NativeShotViews.Bullet(arena,b,shotTracker,now,out var v);if(!NativeBowWire.Is(v.Spec))ShotFx(draw,v,now,q,low,trails,eye,GunKitTracer(b,v));}
            foreach(var s in arena.AbilityShots){NativeShotViews.Ability(arena,s,shotTracker,now,out var v);ShotFx(draw,v,now,q,low,trails,eye);}
            staleEmitters.Clear();foreach(var pair in emitters)if(pair.Value.Frame!=weaponFrame)staleEmitters.Add(pair.Key);
            foreach(long key in staleEmitters){spareEmitters.Push(emitters[key]);emitters.Remove(key);}
            // The world-space trail sprites outlive their shot.
            for(int i=0;i<trailSpriteCount;)
            {
                var p=trailSprites[i];float age=now-p.Born;
                if(!(age<p.Life)||age<-.5f){trailSprites[i]=trailSprites[--trailSpriteCount];continue;}
                NativeWeaponFxShapes.Sprite(draw,p.At,p.Drift,p.Color,p.Size,p.Life,age);i++;
            }
            // The pistol / machine-gun Q kit (NativeGunKitPresentation.cs) on the pose clock, before the drawer is submitted.
            GunKitFx();BowFx(arena,now);
            ImpactFx(draw,arena,now,q,low);
            draw.Submit(camera,FirstPerson&&!ScopeActive?ViewCamera:null);
        }
        private void ShotFx(NativeWeaponFxDraw draw,in NativeShotView v,float now,float q,bool low,bool trails,Vector3 eye,NativeTracerLook look=default)
        {
            // The darkness orb draws its own core, tail and lens (NativeMagicFx); its energy trail here is skipped.
            if(MagicLooks&&NativeMagicFx.Owns(v))return;
            bool tracer=NativeTracerFx.Traced(v),energy=NativeTrailFx.Energy(v);if(!tracer&&!energy)return;
            long key=NativeShotScene.Key(v.Kind,v.Id);var at=new Vector3((float)v.X,(float)v.Y,(float)v.Z);
            if(!emitters.TryGetValue(key,out var e))
            {
                e=spareEmitters.Count>0?spareEmitters.Pop():new Emitter();e.Origin=e.Last=at;e.LastAt=now;e.Carry=0;e.Emitted=0;e.Count=0;e.Seed=key*2654435761L+17;
                e.Halo=NativeWeaponFxShapes.Lin(NativeShotRules.HaloColor(v,now));e.Radius=NativeTrailFx.Radius(v.Radius);emitters.Add(key,e);
            }
            e.Frame=weaponFrame;var velocity=new Vector3((float)v.VX,(float)v.VY,(float)v.VZ);
            if(tracer){if(trails){float flown=Vector3.Distance(at,e.Origin)+.25f;if(look.Kit)KitTracer(draw,at,velocity,flown,look);else NativeWeaponFxShapes.Tracer(draw,at,velocity,flown,v.Spec);}e.Last=at;e.LastAt=now;return;}
            // Energy: the core and glow on the model, then the trail sprites every Spacing metres flown and the ribbon.
            NativeWeaponFxShapes.EnergyCore(draw,at,eye,e.Halo,e.Radius,v.Spec,.93f+.07f*Mathf.Sin(now*23+(e.Seed&63)));
            if(trails)
            {
                float spacing=NativeTrailFx.Spacing(q,low);Vector3 step=at-e.Last;float distance=step.magnitude;
                if(distance>1e-4f&&distance<20)
                {
                    float travelled=-e.Carry;var color=NativeWeaponFxShapes.SpriteColor(e.Halo);
                    while(travelled+spacing<=distance)
                    {
                        travelled+=spacing;float f=travelled/distance;
                        NativeTrailFx.Sprite(e.Seed,e.Emitted++,e.Radius,out float life,out float size,out var offset,out var drift);
                        if(trailSpriteCount==trailSprites.Length)Array.Resize(ref trailSprites,trailSprites.Length*2);
                        trailSprites[trailSpriteCount++]=new TrailSprite{At=e.Last+step*f+NativeWeaponFxShapes.V(offset),Drift=NativeWeaponFxShapes.V(drift)+velocity*.06f,Born=Mathf.Lerp(e.LastAt,now,f),Life=life,Size=size,Color=color};
                    }
                    e.Carry=distance-travelled;
                }
                if(e.Count==0||Vector3.Distance(e.Points[e.Count-1],at)>.02f)e.Push(at,now);else{e.Points[e.Count-1]=at;e.Times[e.Count-1]=now;}
                int first=0;while(first<e.Count-1&&now-e.Times[first]>NativeTrailFx.RibbonLife)first++;
                NativeWeaponFxShapes.Ribbon(draw,e.Points,e.Times,first,e.Count,now,eye,e.Halo,e.Radius);
            }
            e.Last=at;e.LastAt=now;
        }
        // Hard-surface impacts: each surface Impact event (a prop hit with a normal) throws its curved shards, cooling sparks and
        // quick glow, seeded by the replicated event id.
        private void ImpactFx(NativeWeaponFxDraw draw,NativeArena arena,float now,float q,bool low)
        {
            if(!ReferenceEquals(impactArena,arena)||impactGeneration!=arena.Generation){impactArena=arena;impactGeneration=arena.Generation;impactEvent=0;for(int i=0;i<impactMarks.Length;i++)impactMarks[i].Born=-100;}
            int newest=0;foreach(var e in arena.Events)newest=Math.Max(newest,e.Id);if(newest<impactEvent)impactEvent=0;
            foreach(var e in arena.Events)
            {
                if(e.Id<=impactEvent||e.Kind!=EventKind.Impact||string.IsNullOrEmpty(e.SurfaceId))continue;
                var normal=NativeArena.Vec(e.Normal);if(normal.sqrMagnitude<.25f||now-e.Time>=NativeImpactFx.Life)continue;
                impactMarks[impactNext]=new ImpactMark{At=NativeArena.Vec(e.Position),Normal=normal.normalized,Born=e.Time,Seed=e.Id*1000003L+7,Tint=Color.Lerp(NativeWeaponFxShapes.Lin(e.Color!=0?e.Color:0xffedb8),Color.white,.55f)};
                impactNext=(impactNext+1)%impactMarks.Length;
            }
            impactEvent=Math.Max(impactEvent,newest);
            int shards=NativeImpactFx.ShardCount(q,low),sparks=NativeImpactFx.SparkCount(q,low);
            for(int i=0;i<impactMarks.Length;i++){var m=impactMarks[i];NativeWeaponFxShapes.Impact(draw,m.At,m.Normal,m.Seed,m.Tint,now-m.Born,shards,sparks);}
        }
        private void ClearEmitters(){foreach(var e in emitters.Values)spareEmitters.Push(e);emitters.Clear();trailSpriteCount=0;emitterClock=-1;}
        private void ClearWeaponFx()
        {
            weaponFx?.Begin();weaponSnapshot=null;ClearEmitters();emitterArena=null;emitterGeneration=-1;
            foreach(var st in slashes.Values)spareSlashes.Push(st);slashes.Clear();hitFlashes.Clear();hitFlashEvent=0;afterimages.Clear();
            impactArena=null;impactGeneration=-1;impactEvent=0;for(int i=0;i<impactMarks.Length;i++)impactMarks[i].Born=-100;
        }
        private void DisposeWeaponFx(){ClearWeaponFx();weaponFx?.Dispose();weaponFx=null;}
    }
}
