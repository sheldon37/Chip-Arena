using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NQuaternion=System.Numerics.Quaternion;

namespace ChipArena.UnityConversion
{
    // The Central Station's enemies in the game (docs/EXPEDITION-CENTRAL-STATION.md sections 7, 8 and 10; owner 2026-10-16):
    // the Linesman, the Conductor, the Undertow and the Pileup on the horde's kind presentation (NativeHordeKindPresentation.cs
    // draws every add; its hooks call in here for the four). Presentation only: everything comes from what host, guests and the
    // killcam all hold (the add's replicated kind, LastAttack, position, health and hits; its NativeStationView once Astra
    // replicates it, StationViewOf) and the presentation clock. No hit, collider, timing, damage, movement or network state is
    // read back or changed.
    //  - Models: Core/NativeStationModels.cs (bodies on the zombie kit; the Conductor's lantern and the Pileup's door as props
    //    queued on the hand and the chest each frame, dropped when the hand is severed and in the death).
    //  - Motion: the gait on each kind's takes (NativeZombieMotion), then Core/NativeStationMotion's own set over it (the arc,
    //    the kneel, the whistle, the last call, the swim, surfacing, the drag, the shove, the door's crush), and the deaths.
    //  - Effects (DrawStationFx, budgeted like the horde's, nearest first): the Linesman's headlamp, battery and coil arcs, the
    //    floor crackle and sparks when grounded, the arc's bolt chained to the right pilots and their jolts; the Conductor's
    //    lantern light and swing trail, the whistle's ring, the bell's rings, the buffed adds' red flash and trail, the shattered
    //    glass; the Undertow's eyes, ripples, converging bubbles, splash, drips and water trail; the Pileup's window light (it
    //    flickers when hit), sparks off the door from the front, the brace's scrape, the shove's dust, the split's burst.
    //  - Audio (NativeAudio below): the pve.station.<kind>.<cue> keys of section 10 at their moments, each with a fitting
    //    existing stand-in where one exists (NativeStationKinds.StandIn), else silent until the owner's clips.
    //  - The DEV preview: the Dev Sandbox's minion gallery shows the four in a second row looping their whole set from
    //    gallery-made views (presentation only, nothing spawned through the authority).
    // Allocation free in steady state: per-add states, jobs and buffers are pooled.
    public sealed partial class ArenaPresentation
    {
        // ================================================================================================================
        // The binding (Astra): an add's replicated station fields (Core NativeStationView, named as section 8). One line once
        // NativePveAdd carries them, e.g.  internal static NativeStationView StationViewOf(NativeMinion m)=>m.Pve?.Station;
        // Until then no add has one (null): every station kind draws its gait, melee, hits and death from today's fields.
        // ================================================================================================================
        internal static NativeStationView StationViewOf(NativeMinion m)=>null;

        private sealed class StationAdd
        {
            public readonly NativeStationMotion Motion=new NativeStationMotion();public StationMotionOutput Out;public int Id,Kind=-1,Frame;public float Last=float.NaN,SeenAt;
            public bool WasBroken,WasGrounded,WasHolding;public float HitAt=-100;public Vector3 HitDir;
            // The prop's last world place (a severed hand or the death drops it from there), the lantern's swing trail.
            public Matrix4x4 PropWorld;public bool PropKnown,PropDropped;public NativeHordeModels.StationProp PropKind;
            public readonly Vector3[] Trail=new Vector3[10];public int TrailCount;public float TrailAt;
            public void Reset(int id){Id=id;Kind=-1;Last=float.NaN;Motion.Reset();WasBroken=WasGrounded=WasHolding=false;HitAt=-100;PropKnown=PropDropped=false;TrailCount=0;TrailAt=-100;}
        }
        private readonly Dictionary<int,StationAdd> stationAdds=new Dictionary<int,StationAdd>();private readonly Stack<StationAdd> stationSpare=new Stack<StationAdd>();private readonly List<int> stationStale=new List<int>();
        private StationAdd StationAddOf(int id,float now){if(!stationAdds.TryGetValue(id,out var s)){s=stationSpare.Count>0?stationSpare.Pop():new StationAdd();s.Reset(id);stationAdds[id]=s;}s.SeenAt=now;return s;}
        private readonly StationMotionInput[] stationInput=new StationMotionInput[1];

        // ---- (a) the special the replicated LastAttack started: the gait shows no melee for it ----
        private static void StationAttack(NativeMinion m,int kind,float now,ref float age)
        {
            if(!NativeStationKinds.Is(kind))return;var v=StationViewOf(m);var sp=NativeStationKinds.SpecialOf(kind,v?.attack);if(sp==NativeStationKinds.Special.None||sp==NativeStationKinds.Special.Grab)return;
            float a=now-m.LastAttack;if(a>=0&&a<NativeStationKinds.SpecialEnd(sp)+.3f)age=float.NaN;
        }
        // ---- (b) the gait's input: the Conductor strikes with the lantern hand (swipe-l), its other arm gone or not ----
        private static void StationInput(ref HordeMotionInput f,int kind){if(kind==NativeStationKinds.Conductor&&!NativeHordeKinds.LeftArmless(f.Lost))f.Side=-1;}
        // ---- (c) the kind's own motion over the gait's pose, the sink, the effects job ----
        private void StationPosed(NativeArena arena,NativeMinion m,HordeKindState s,int kind,float now,float ground,in HordeMotionInput f,ref Vector3 pos)
        {
            if(!NativeStationKinds.Is(kind))return;var a=m.Pve;var v=StationViewOf(m);var st=StationAddOf(m.Id,now);st.Kind=kind;st.Frame=hordeKindFrame;
            var sp=NativeStationKinds.SpecialOf(kind,v?.attack);float spAge=sp!=NativeStationKinds.Special.None?now-m.LastAttack:float.NaN;if(!(spAge>=0&&spAge<NativeStationKinds.SpecialEnd(sp)+.3f))spAge=float.NaN;
            if(sp==NativeStationKinds.Special.Grab)spAge=float.NaN;
            // A sump's entrance surfaces out of the water (the authority's entry; the walk-in's gait under it).
            bool sump=a!=null&&now<a.ActiveAt&&a.Entry!=null&&a.Entry.kind=="sump";float surface=sump?now-a.SpawnAt:v!=null&&now>=v.surfaceAt?now-v.surfaceAt:float.NaN;
            NativePilot aimAt=null;if(v?.arcTargets!=null&&v.arcTargets.Length>0)aimAt=HordePilot(arena,v.arcTargets[0]);if(aimAt==null)aimAt=HordeTarget(arena,pos);
            NativePilot held=v!=null&&v.Holding(now)?HordePilot(arena,v.grabTarget):null;
            StationPose(st,s,kind,m.Id,now,f,sp,spAge,v,aimAt!=null?aimAt.Body.position+Vector3.up*.35f:(Vector3?)null,held!=null?held.Body.position+Vector3.down*.75f:(Vector3?)null,surface,sump,ref pos);
            StationCollect(st,s,kind,m.Id,now,ground,pos,sp,spAge,v,arena,a?.Lost??0);
        }
        // The overlay for an add or a gallery rig: the input from the view's fields and the gait, the pose, the sink.
        private void StationPose(StationAdd st,HordeKindState s,int kind,int id,float now,in HordeMotionInput f,NativeStationKinds.Special sp,float spAge,NativeStationView v,Vector3? aim,Vector3? hold,float surface,bool sump,ref Vector3 pos)
        {
            float inv=1/s.Scale;var g=s.Gait;
            ref var i=ref stationInput[0];
            i=new StationMotionInput{Kind=kind,Id=id,Seed=NativeHordeKinds.Seed(id),Time=now,Dt=float.IsNaN(st.Last)?0:now-st.Last,Special=sp,SpecialAge=spAge,MeleeAge=f.AttackAge,MeleeStrike=f.Strike,Melee=f.Attack,Speed=g.Speed,
                Velocity=g.ToLocal(f.Root+f.Velocity,f.Root),Grounded=v!=null&&v.grounded,LanternBroken=v!=null&&v.lanternBroken,Submerged=v!=null&&v.submerged||sump&&surface<0,
                LastCallAge=v!=null&&now>=v.lastCallAt?now-v.lastCallAt:float.NaN,SurfaceAge=surface,HoldAge=v!=null&&v.Holding(now)?now-v.grabAt:float.NaN,ReleaseAge=v!=null&&now>=v.releaseAt&&v.releaseAt>v.grabAt?now-v.releaseAt:float.NaN,
                HasAim=aim.HasValue,Aim=aim.HasValue?g.ToLocal(ToN(aim.Value)*inv,f.Root):default,HasHold=hold.HasValue,Hold=hold.HasValue?g.ToLocal(ToN(hold.Value)*inv,f.Root):default};
            st.Last=now;st.Motion.Pose(i,minionPose,ref st.Out);pos.y-=st.Out.Sink*s.Scale;
        }
        // ---- (d) the glow: the Linesman grounded and charging, the Conductor's flare; any buffed add's red-white flash ----
        private void StationGlow(NativeMinion m,int kind,float now,ref Vector4 glow)
        {
            if(NativeStationKinds.Is(kind)&&stationAdds.TryGetValue(m.Id,out var st)&&st.Kind==kind){float k=kind==NativeStationKinds.Conductor?1:st.Out.Glow;glow.x*=k;glow.y*=k;glow.z*=k;}
            var v=StationViewOf(m);if(v!=null&&v.Aboard(now)){float fl=NativeStationFx.AboardFlash(now-(v.aboardUntil-NativeStationKinds.AboardSeconds));glow.x*=1+2.2f*fl;glow.y*=1+.25f*fl;glow.z*=1+.25f*fl;}
        }
        // ---- (e) the prop on its hand (dropped when the hand is gone) ----
        private Matrix4x4 stationPropRoot;
        private void StationQueue(CapturedAnimation.Player rig,NativeMinion m,int kind,int tier,int lost,Vector4 glow,float flash,float now)
        {
            if(!NativeStationKinds.Is(kind)||!stationAdds.TryGetValue(m.Id,out var st)||!st.Out.Prop)return;var v=StationViewOf(m);bool whistle=NativeStationKinds.SpecialOf(kind,v?.attack)==NativeStationKinds.Special.Whistle;
            StationQueueProp(rig,st,tier,lost,glow,flash,now,StationPropGlow(st,kind,whistle?now-m.LastAttack:float.NaN,v!=null&&now>=v.lastCallAt?now-v.lastCallAt:float.NaN,now));
        }
        private void StationQueueProp(CapturedAnimation.Player rig,StationAdd st,int tier,int lost,Vector4 glow,float flash,float now,float k)
        {
            var world=rig.Root.transform.localToWorldMatrix*Matrix4x4.TRS(V(st.Out.PropAt),Q(st.Out.PropTurn),Vector3.one);
            // A severed hand lets go: the prop falls from where it was (once).
            if(!HordeJointShown(lost,st.Out.PropJoint)){if(!st.PropDropped&&st.PropKnown){st.PropDropped=true;StationDrop(st.PropKind,st.PropWorld,now,NativeHordeKinds.Seed(st.Id));}return;}
            st.PropWorld=world;st.PropKnown=true;st.PropKind=st.Out.PropKind;
            string id=NativeHordeModels.StationPropId(st.Out.PropKind,Math.Min(tier<0?1:tier,NativeHordeModels.Impostor));if(!sourceModels.Has(id))return;
            sourceModels.Queue(id,world,null,null,glow:new Vector4(k,k,k,glow.w),hitFlash:flash);
        }
        // The lantern's flare (the whistle's inhale and blast, the last call's ringing), the window's dying tube (a hit stutters it).
        private static float StationPropGlow(StationAdd st,int kind,float whistleAge,float lastCallAge,float now)
            =>kind==NativeStationKinds.Conductor?NativeStationMotion.LanternFlare(whistleAge,lastCallAge):NativeStationFx.WindowFlicker(NativeHordeKinds.Seed(st.Id),now,now-st.HitAt);
        private static Vector3 V(NVector v)=>new Vector3(v.X,v.Y,v.Z);
        private static Quaternion Q(NQuaternion q)=>new Quaternion(q.X,q.Y,q.Z,q.W);

        // ================================================================================================================
        // Effects jobs (collected while posing, drawn in DrawStationFx within the budgets)
        // ================================================================================================================
        private struct StationFxJob
        {
            public int Id,Kind,Lost;public uint Seed;public float Distance,SpecialAge,Charge,LastCall,Surface,Hold,Aboard,Since;public NativeStationKinds.Special Special;public bool Grounded,Submerged,Broken,HasAim;
            public Vector3 Root,Floor,Forward,Hand,Coil,Elbow,Shoulder,Pack,Lamp,LampDir,Eyes,Mouth,Chest,Prop,PropUp,DoorFoot,Aim;public Vector3 A0,A1,A2;public int Arcs;public float ArcAt;
        }
        private StationFxJob[] stationJobs=new StationFxJob[16];private int stationJobCount;
        private void StationCollect(StationAdd st,HordeKindState s,int kind,int id,float now,float ground,Vector3 root,NativeStationKinds.Special sp,float spAge,NativeStationView v,NativeArena arena,int lost)
        {
            // (Drawn and emptied each frame by DrawStationFx; a frame without effects keeps at most 128.)
            if(stationJobCount==stationJobs.Length){if(stationJobs.Length>=128)return;Array.Resize(ref stationJobs,stationJobCount*2);}
            var p=minionPose;p.World();var g=s.Gait;float sc=s.Scale;Vector3 W(int b,NVector local){var l=p.WorldPosition[b]+System.Numerics.Vector3.Transform(local,p.WorldRotation[b]);var w=g.ToWorld(l*sc,ToN(root));return new Vector3(w.X,w.Y,w.Z);}
            Vector3 Dir(int b,NVector local){var a=W(b,NVector.Zero);return (W(b,local)-a).normalized;}
            float yr=g.Yaw*Mathf.Deg2Rad;var fwd=new Vector3(Mathf.Sin(yr),0,Mathf.Cos(yr));
            // (Source-frame joint offsets: +x the add's left.)
            var j=new StationFxJob{Id=id,Kind=kind,Lost=lost,Seed=NativeHordeKinds.Seed(id),Distance=s.Distance,Special=sp,SpecialAge=spAge,Charge=st.Out.Charge,Root=root,Floor=new Vector3(root.x,ground,root.z),Forward=fwd,
                Grounded=v!=null&&v.grounded,Submerged=st.Out.Sink>.2f&&kind==NativeStationKinds.Undertow,Broken=v!=null&&v.lanternBroken,LastCall=v!=null&&now>=v.lastCallAt?now-v.lastCallAt:float.NaN,
                Surface=v!=null&&now>=v.surfaceAt?now-v.surfaceAt:float.NaN,Hold=v!=null&&v.Holding(now)?now-v.grabAt:float.NaN,Aboard=v!=null&&v.Aboard(now)?now-(v.aboardUntil-NativeStationKinds.AboardSeconds):float.NaN,Since=now-st.HitAt,
                Hand=W(CapturedRig.RHand,new NVector(0,-.12f,.04f)),Coil=W(CapturedRig.RElbow,new NVector(-.12f,-.05f,0)),Elbow=W(CapturedRig.RElbow,NVector.Zero),Shoulder=W(CapturedRig.RArm,new NVector(0,-.05f,.05f)),
                Pack=W(CapturedRig.Chest,new NVector(0,.15f,-.34f)),Lamp=W(CapturedRig.Head,new NVector(0,.305f+NativeHordeModels.HeadDrop[NativeStationKinds.Linesman],.18f)),LampDir=Dir(CapturedRig.Head,new NVector(0,-.1f,1)),
                Eyes=W(CapturedRig.Head,new NVector(0,.176f,.18f)),Mouth=W(CapturedRig.Head,new NVector(0,.075f,.2f)),Chest=W(CapturedRig.Chest,new NVector(0,.15f,0))};
            if(st.Out.Prop)
            {
                // The prop's points in the world: the lantern's glass, the window's centre and the door's foot.
                var pm=Matrix4x4.TRS(V(st.Out.PropAt),Q(st.Out.PropTurn),Vector3.one);NVector Rig(Vector3 l){var u=pm.MultiplyPoint3x4(l);var src=new NVector(-u.x,u.y,u.z);var w=g.ToWorld(src*sc,ToN(root));return w;}
                var c=kind==NativeStationKinds.Conductor?new Vector3(0,-NativeHordeModels.LanternDrop,0):new Vector3(0,NativeHordeModels.DoorWindowY,.04f);var cw=Rig(c);j.Prop=new Vector3(cw.X,cw.Y,cw.Z);
                var foot=Rig(new Vector3(0,-NativeHordeModels.DoorHeight*.5f,0));j.DoorFoot=new Vector3(foot.X,foot.Y,foot.Z);var up=Rig(c+Vector3.up);j.PropUp=(new Vector3(up.X,up.Y,up.Z)-j.Prop).normalized;
            }
            // The arc's targets (replicated seat ids; the gallery's own), its strike (arcAt, else LastAttack + the wind-up).
            if(sp==NativeStationKinds.Special.Arc&&v!=null)
            {
                j.ArcAt=v.arcAt>-50?v.arcAt:now-spAge+NativeStationKinds.ArcWindup;int n=0;
                if(v.arcTargets!=null)for(int k=0;k<v.arcTargets.Length&&n<3;k++){var t=arena!=null?HordePilot(arena,v.arcTargets[k]):null;var at=t!=null?t.Body.position+Vector3.up*.3f:stationGalleryTargets!=null&&k<stationGalleryTargets.Length?stationGalleryTargets[k]:Vector3.zero;if(t==null&&stationGalleryTargets==null)continue;if(n==0)j.A0=at;else if(n==1)j.A1=at;else j.A2=at;n++;}
                j.Arcs=n;
            }
            stationJobs[stationJobCount++]=j;
        }
        private Vector3[] stationGalleryTargets;

        // ---- the dead: their falls' props and bursts (kept for the death's effects) ----
        private struct StationDeadJob{public int Id,Kind;public uint Seed;public float Age;public Vector3 Root,Floor,Forward,Chest,Hand;public Matrix4x4 Rig;public bool HadProp;public NVector PropAt;public float SplitAge;}
        private readonly StationDeadJob[] stationDead=new StationDeadJob[8];private int stationDeadCount;
        /// <summary>(f) A station kind's death pose (over the shared death's, before the bones are written): the world sink.</summary>
        private float StationDeathPose(int kind,uint seed,float fx)
        {
            if(!NativeStationKinds.Is(kind))return 0;NativeStationMotion.Death(minionPose,kind,seed,Mathf.Min(Mathf.Max(0,fx),(float)NativeMinionFx.DeathSeconds),out float sink);return sink*NativeHordeKinds.Scale[kind];
        }
        /// <summary>(g) The death's props (the door laid flat, the lantern dropped and shattered) and its effects job.</summary>
        private void StationDeathFx(NativeDeath death,int kind,uint seed,float fx,CapturedAnimation.Player rig,float floorY)
        {
            if(!NativeStationKinds.Is(kind)||rig==null)return;var root=rig.Root.transform.localToWorldMatrix;stationAdds.TryGetValue(death.id,out var st);
            if(kind==NativeStationKinds.Pileup&&fx<NativeMinionFx.DeathSeconds+.6f&&(st==null||!st.PropDropped))
            {NativeStationMotion.DoorFall(fx,out var at,out var turn);StationPropAt(NativeHordeModels.StationProp.Door,root*Matrix4x4.TRS(new Vector3(-at.X,at.Y,at.Z),Q(CapturedRig.Mirror(turn)),Vector3.one),1-Mathf.Clamp01((fx-(float)NativeMinionFx.DeathSeconds)/.6f));}
            if(kind==NativeStationKinds.Conductor&&st!=null&&!st.PropDropped&&fx<NativeMinionFx.DeathSeconds+.6f)
            {
                var from=st.Out.Prop?st.Out.PropAt:new NVector(-.36f,-.25f,.2f);NativeStationMotion.LanternFall(fx,from,out var at,out var turn);bool whole=fx<NativeStationMotion.LanternShatterAt&&!st.Out.PropKind.Equals(NativeHordeModels.StationProp.BrokenLantern);
                StationPropAt(whole?NativeHordeModels.StationProp.Lantern:NativeHordeModels.StationProp.BrokenLantern,root*Matrix4x4.TRS(V(at),Q(turn),Vector3.one),whole?1:0);
            }
            if(stationDeadCount>=stationDead.Length||fx>NativeMinionFx.DeathFxSeconds)return;var p=minionPose;p.World();
            Vector3 Wr(int b,NVector l){var w=p.WorldPosition[b]+NVector.Transform(l,p.WorldRotation[b]);return root.MultiplyPoint3x4(new Vector3(-w.X,w.Y,w.Z));}
            float yr=death.yaw*Mathf.Deg2Rad;
            stationDead[stationDeadCount++]=new StationDeadJob{Id=death.id,Kind=kind,Seed=seed,Age=fx,Root=death.position,Floor=new Vector3(death.position.x,floorY,death.position.z),Forward=new Vector3(Mathf.Sin(yr),0,Mathf.Cos(yr)),
                Chest=Wr(CapturedRig.Chest,new NVector(0,.15f,-.3f)),Hand=Wr(CapturedRig.RHand,NVector.Zero),Rig=root,SplitAge=fx};
        }
        private void StationPropAt(NativeHordeModels.StationProp prop,Matrix4x4 world,float glow)
        {
            string id=NativeHordeModels.StationPropId(prop,NativeHordeModels.Full);if(sourceModels==null||!sourceModels.Has(id))return;sourceModels.Queue(id,world,null,null,glow:new Vector4(glow,glow,glow,1));
        }
        // ---- props dropped by a severed hand: they fall, tumble and rest (the lantern shatters), then fade with the body ----
        private struct StationDropped{public NativeHordeModels.StationProp Prop;public Matrix4x4 From;public float At;public uint Seed;public bool Live;}
        private readonly StationDropped[] stationDrops=new StationDropped[6];private int stationDropNext;
        private void StationDrop(NativeHordeModels.StationProp prop,Matrix4x4 from,float at,uint seed){stationDrops[stationDropNext++%stationDrops.Length]=new StationDropped{Prop=prop,From=from,At=at,Seed=seed,Live=true};}
        private void StationDropsDraw(float now)
        {
            for(int i=0;i<stationDrops.Length;i++)
            {
                ref var d=ref stationDrops[i];if(!d.Live)continue;float age=now-d.At;if(age<0||age>NativeMinionFx.DeathFxSeconds){d.Live=false;continue;}
                Vector3 p=d.From.GetColumn(3);float fall=Mathf.Min(age,.45f);float drop=Mathf.Min(p.y-.05f-(p.y-1.2f),4.9f*fall*fall);var turn=Quaternion.Euler(age<.45f?age*300:135,(d.Seed%360),age<.45f?age*120:54);
                var world=Matrix4x4.TRS(p+Vector3.down*Mathf.Min(drop,1.1f),turn*d.From.rotation,Vector3.one);var prop=d.Prop==NativeHordeModels.StationProp.Lantern&&age>.45f?NativeHordeModels.StationProp.BrokenLantern:d.Prop;
                StationPropAt(prop,world,1-Mathf.Clamp01((age-2.4f)/.6f));
            }
        }

        // ================================================================================================================
        // Replicated hits on station adds (the authority's events): the weak point's sparks, the door's sparks and window stutter
        // ================================================================================================================
        private struct StationHitBurst{public Vector3 At,Dir;public float Time;public int Kind;public uint Seed;}
        private readonly StationHitBurst[] stationHits=new StationHitBurst[12];private int stationHitNext,stationEvent;
        private void StationHits(NativeArena arena)
        {
            var events=arena.Events;int newest=stationEvent;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(e.Id<=stationEvent)continue;if(e.Id>newest)newest=e.Id;if((e.Kind!=EventKind.Hit&&e.Kind!=EventKind.Headshot)||e.Target<10000||!(e.Amount>0))continue;
                if(!stationAdds.TryGetValue(e.Target,out var st)||(st.Kind!=NativeStationKinds.Linesman&&st.Kind!=NativeStationKinds.Pileup))continue;var m=HordeMinion(arena,e.Target);if(m==null)continue;
                var dir=NativeArena.Vec(e.Normal);dir.y=0;if(dir.sqrMagnitude<1e-4f)continue;dir.Normalize();float yr=m.Yaw*Mathf.Deg2Rad;var fwd=new Vector3(Mathf.Sin(yr),0,Mathf.Cos(yr));
                // The push runs along the shot: a shot from the front pushes back (its direction against the facing).
                float facing=Vector3.Dot(-dir,fwd);var at=e.Position.x==0&&e.Position.z==0?m.Body.position:NativeArena.Vec(e.Position);
                bool front=facing>=Mathf.Cos(NativeStationKinds.FrontCone*.5f*Mathf.Deg2Rad),back=facing<=-Mathf.Cos((360-NativeStationKinds.BackCone)*.5f*Mathf.Deg2Rad);
                if(st.Kind==NativeStationKinds.Pileup&&front){st.HitAt=e.Time;stationHits[stationHitNext++%stationHits.Length]=new StationHitBurst{At=at,Dir=-dir,Time=e.Time,Kind=st.Kind,Seed=(uint)e.Id*2654435761u};}
                if(st.Kind==NativeStationKinds.Linesman&&back){stationHits[stationHitNext++%stationHits.Length]=new StationHitBurst{At=m.Body.position+Vector3.up*.45f-fwd*.35f,Dir=-fwd,Time=e.Time,Kind=st.Kind,Seed=(uint)e.Id*2654435761u};}
            }
            if(newest<stationEvent){stationEvent=0;for(int i=0;i<stationHits.Length;i++)stationHits[i].Time=-100;}else stationEvent=newest;
        }

        // ================================================================================================================
        // (h) Drawing: the shared effects renderer (NativeVfxDraw: glows, streaks, puffs, drops) and the effect layers (NativeFxDraw:
        // floor rings, strips, solid shards), nearest adds first within the horde's budgets.
        // ================================================================================================================
        private readonly NVector[] stationBolt=new NVector[NativeStationFx.BoltPoints];private float[] stationRank=new float[16];private int[] stationOrder=new int[16];
        private static Color Lin(int hex,float k){var c=NativeVfxDraw.Linear(hex)*k;c.a=0;return c;}
        private static Color FxC(int hex,float a){var c=NativeArena.ColorOf(hex);c.a=a;return c;}
        private const float StationGlowing=NativeFxDraw.Authored+NativeFxDraw.Additive;
        private void DrawStationFx(NativeFxDraw draw,NativeArena arena,float now,Vector3 eye,Vector3 right,Vector3 up,bool low)
        {
            if(arena!=null)StationHits(arena);StationDropsDraw(now);
            if(vfx==null){stationJobCount=0;stationDeadCount=0;return;}float quality=Mathf.Clamp01(EffectsQuality);
            if(stationRank.Length<stationJobCount){stationRank=new float[stationJobs.Length];stationOrder=new int[stationJobs.Length];}
            for(int n=0;n<stationJobCount;n++){stationOrder[n]=n;stationRank[n]=Vector3.Distance(stationJobs[n].Root,eye);}Array.Sort(stationRank,stationOrder,0,stationJobCount);int budget=NativeMinionFx.FullRigs;
            for(int r=0;r<stationJobCount;r++)
            {
                ref var j=ref stationJobs[stationOrder[r]];float dist=stationRank[r];double band=NativeMinionFx.Band(dist,quality);
                // The identity lights read past the effects' band (they are the kinds' silhouettes at 30 m and more): kept to 60 m.
                StationIdentity(draw,j,now,eye,right,up,dist);if(band<0||budget<=0)continue;budget--;bool half=band<.5||low;
                switch(j.Kind)
                {
                    case NativeStationKinds.Linesman:StationLinesmanFx(draw,j,now,eye,half);break;
                    case NativeStationKinds.Conductor:StationConductorFx(draw,j,now,eye,right,up,half);break;
                    case NativeStationKinds.Undertow:StationUndertowFx(draw,j,now,eye,half);break;
                    case NativeStationKinds.Pileup:StationPileupFx(draw,j,now,eye,half);break;
                }
                if(!float.IsNaN(j.Aboard))StationAboardFx(draw,j,now,eye,half);
            }
            // Buffed adds of other kinds (the Conductor's aura on the horde): their red flash and trail.
            if(arena!=null)foreach(var m in arena.Minions){if(m.Pve==null||m.Hp<=0)continue;var v=StationViewOf(m);if(v==null||!v.Aboard(now)||NativeStationKinds.Is(NativeHordeKinds.Resolve(m.Pve.Stats?.id)))continue;
                if((m.Body.position-eye).sqrMagnitude>40*40)continue;var st=StationAddOf(m.Id,now);st.Frame=hordeKindFrame;var j=new StationFxJob{Id=m.Id,Seed=NativeHordeKinds.Seed(m.Id),Root=m.Body.position,Chest=m.Body.position+Vector3.up*.45f,Aboard=now-(v.aboardUntil-NativeStationKinds.AboardSeconds)};
                float yr=m.Yaw*Mathf.Deg2Rad;j.Forward=new Vector3(Mathf.Sin(yr),0,Mathf.Cos(yr));StationAboardFx(draw,j,now,eye,low);}
            for(int i=0;i<stationDeadCount;i++)StationDeadFx(draw,stationDead[i],now,eye,low);
            for(int i=0;i<stationHits.Length;i++){var h=stationHits[i];float age=now-h.Time;if(age<0||age>.5f)continue;
                int hex=h.Kind==NativeStationKinds.Pileup?0xFFD890:NativeStationKinds.LinesmanBlue;int n=low?8:16;
                for(int k=0;k<n;k++)if(NativeStationFx.Spark(h.Seed,k,age,h.Kind==NativeStationKinds.Pileup?5:3.5f,ToN(h.Dir+Vector3.up*.3f),.6f,out var off,out var vel,out float life))vfx.Streak(h.At+V(off),V(vel),Lin(hex,5*life),.012f,.04f,.025f,.6f);
                if(age<.1f)vfx.Glow(h.At,Lin(hex,4*(1-age/.1f)),.45f,NativeVfxDraw.CoreGlow);}
            stationJobCount=0;stationDeadCount=0;
        }
        // The identity lights (section 7): the headlamp's halo and beam, the battery; the lantern; the aqua eyes; the window.
        private void StationIdentity(NativeFxDraw draw,in StationFxJob j,float now,Vector3 eye,Vector3 right,Vector3 up,float dist)
        {
            if(dist>60)return;float far=NativeStationFx.Far(dist),size,gain,wash,washGain;
            switch(j.Kind)
            {
                case NativeStationKinds.Linesman:
                {
                    int hex=j.Charge>.15f?NativeStationKinds.LinesmanBlue:0xEAF4FF;NativeStationFx.Lamp(j.Charge,far,out size,out gain);vfx.Glow(j.Lamp,Lin(hex,gain),size,NativeVfxDraw.CoreGlow);
                    // The beam: a cone of camera-facing strips widening ahead of the lamp, fading out by its length.
                    // It stops at the floor (a pool of light where it lands) and fades as the camera looks down it.
                    float L=NativeStationFx.BeamReach(j.Lamp.y-j.Floor.y,-j.LampDir.y,out bool lands),al=NativeStationFx.BeamAlpha(j.Charge,Vector3.Dot(j.LampDir,(eye-j.Lamp).normalized));
                    if(al>.004f)for(int k=0;k<6;k++){float u0=k/6f,u1=(k+1)/6f;var a=j.Lamp+j.LampDir*(.12f+u0*L);var b=j.Lamp+j.LampDir*(.12f+u1*L);StationStrip(draw,a,b,.05f+NativeStationFx.BeamWidth*u0,.05f+NativeStationFx.BeamWidth*u1,FxC(hex,al*(1-u0)*(1-u0)),eye);}
                    if(lands){NativeStationFx.BeamPool(j.Charge,L,out size,out gain);vfx.Glow(j.Lamp+j.LampDir*L+Vector3.up*.03f,Lin(hex,gain),size,NativeVfxDraw.CoreGlow);}
                    NativeStationFx.Pack(j.Grounded,j.Charge,far,out size,out gain);vfx.Glow(j.Pack,Lin(NativeStationKinds.LinesmanBlue,gain),size,NativeVfxDraw.CoreGlow);
                    break;
                }
                case NativeStationKinds.Conductor:
                    if(!j.Broken&&j.Prop!=Vector3.zero){float flare=NativeStationMotion.LanternFlare(j.Special==NativeStationKinds.Special.Whistle?j.SpecialAge:float.NaN,j.LastCall);NativeStationFx.Lantern(flare,far,out size,out gain,out wash,out washGain);
                        vfx.Glow(j.Prop,Lin(NativeStationKinds.LanternRed,gain),size,NativeVfxDraw.CoreGlow);vfx.Glow(j.Prop,Lin(0xFF3A22,washGain),wash,NativeVfxDraw.Mote);}
                    break;
                case NativeStationKinds.Undertow:
                {
                    float tele=j.Special==NativeStationKinds.Special.Grab&&!float.IsNaN(j.SpecialAge)&&j.SpecialAge<NativeStationKinds.GrabTelegraph?j.SpecialAge/NativeStationKinds.GrabTelegraph:0;
                    NativeStationFx.Eyes(j.Submerged,tele,far,out size,out gain);vfx.Glow(j.Eyes,Lin(NativeStationKinds.UndertowAqua,gain),size,NativeVfxDraw.CoreGlow);break;
                }
                case NativeStationKinds.Pileup:
                    if(j.Prop!=Vector3.zero){NativeStationFx.Window(NativeStationFx.WindowFlicker(j.Seed,now,j.Since),far,out size,out gain,out wash,out washGain);vfx.Glow(j.Prop+j.Forward*.06f,Lin(NativeStationKinds.WindowTube,gain),size,NativeVfxDraw.CoreGlow);vfx.Glow(j.Prop+j.Forward*.1f,Lin(NativeStationKinds.WindowTube,washGain),wash,NativeVfxDraw.Mote);}
                    break;
            }
        }
        // A camera-facing strip from a to b, w0 wide at a and w1 at b.
        private static void StationStrip(NativeFxDraw draw,Vector3 a,Vector3 b,float w0,float w1,Color color,Vector3 eye)
        {
            Vector3 d=b-a,s=Vector3.Cross(d,eye-a);if(s.sqrMagnitude<1e-10f)return;s.Normalize();draw.Quad(draw.Effects,a-s*w0*.5f,b-s*w1*.5f,b+s*w1*.5f,a+s*w0*.5f,color,StationGlowing);
        }
        // A jagged electric bolt from a to b (Core NativeStationFx.Bolt) as hot streaks with a coloured sheath, and its forks.
        private void StationBoltFx(uint seed,int strand,float now,Vector3 a,Vector3 b,float amp,float width,int hex,float k,int forks,int points=NativeStationFx.BoltPoints)
        {
            int n=NativeStationFx.Bolt(seed,strand,now,ToN(a),ToN(b),amp,stationBolt,points);
            for(int i=0;i+1<n;i++){var p0=V(stationBolt[i]);var p1=V(stationBolt[i+1]);vfx.Streak(p1,p1-p0,Lin(0xF4FCFF,6*k),width,(p1-p0).magnitude,1,0);vfx.Streak(p1,p1-p0,Lin(hex,1.5f*k),width*4,(p1-p0).magnitude,1,.2f);}
            for(int f=0;f<forks;f++)if(NativeStationFx.Fork(seed,strand,f,now,stationBolt,n,out var fa,out var fb)){var a0=V(fa);var b0=V(fb);vfx.Streak(b0,b0-a0,Lin(0xBFF0FF,4*k),width*.6f,(b0-a0).magnitude,1,.6f);}
        }
        private void StationLinesmanFx(NativeFxDraw draw,in StationFxJob j,float now,Vector3 eye,bool half)
        {
            uint s=j.Seed;float c=j.Charge;
            // The coil's arcs crawling to the hand (and up the raised arm and into the pack in the wind-up or grounded).
            int strands=1+(int)(2*c)+(j.Grounded?1:0);if(half)strands=Math.Min(strands,2);
            for(int k=0;k<strands;k++){var from=k==2?j.Shoulder:j.Coil;var to=k==0?j.Hand:k==1?j.Shoulder:j.Pack;StationBoltFx(s,k,now,from,to,.05f+.05f*c,.012f+.008f*c,NativeStationKinds.LinesmanBlue,.6f+.9f*c,0,8);}
            vfx.Glow(j.Hand,Lin(NativeStationKinds.LinesmanBlue,1.6f+3*c),.3f+.45f*c,NativeVfxDraw.CoreGlow);
            // Grounded or charging: the floor crackles under him; grounded, sparks off his feet and a blue ring on the floor.
            if(j.Grounded||c>.3f)for(int k=0;k<(half?2:c>.3f?5:3);k++){float ang=k*72+now*40;var d=new Vector3(Mathf.Sin(ang*Mathf.Deg2Rad),0,Mathf.Cos(ang*Mathf.Deg2Rad));var f0=j.Floor+Vector3.up*.03f;StationBoltFx(s,20+k,now,f0+d*.25f,f0+d*(.8f+.6f*c),.05f,.01f,NativeStationKinds.LinesmanBlue,.8f,0,7);}
            if(j.Grounded){draw.Ring(draw.Effects,j.Floor+Vector3.up*.02f,.82f,1,FxC(NativeStationKinds.LinesmanBlue,.35f),StationGlowing);
                uint burst=s^(uint)((int)(now*4)*977);float age=now*4%1*.25f;for(int i=0;i<(half?5:10);i++)if(NativeStationFx.Spark(burst,i,age,2.5f,NVector.UnitY,.8f,out var off,out var vel,out float life))vfx.Streak(j.Floor+Vector3.up*.05f+V(off),V(vel),Lin(0xBFF0FF,4*life),.01f,.03f,.03f,.6f);}
            // The bolt (the strike at arcAt) chained through the struck pilots, then their jolts.
            if(j.Special==NativeStationKinds.Special.Arc&&j.Arcs>0)
            {
                float age=now-j.ArcAt,life=NativeStationFx.BoltLife(age);var from=j.Hand;
                for(int k=0;k<j.Arcs;k++){var to=k==0?j.A0:k==1?j.A1:j.A2;if(life>0){StationBoltFx(s,60+k,now,from,to,.11f,k==0?.05f:.032f,NativeStationKinds.LinesmanBlue,life*(k==0?1.4f:1),half?1:NativeStationFx.BoltForks);vfx.Glow(to,Lin(NativeStationKinds.LinesmanBlue,4*life),1.2f*life+.3f,NativeVfxDraw.CoreGlow);}
                    float jolt=NativeStationFx.Jolt(age);if(jolt>0)for(int q=0;q<(half?1:3);q++)StationBoltFx(s,80+k*4+q,now,to+Vector3.down*.5f,to+new Vector3((q-1)*.3f,.35f,.2f),.06f,.01f,NativeStationKinds.LinesmanBlue,jolt,0,6);from=to;}
                if(life>0)vfx.Glow(j.Hand,Lin(0xF4FCFF,4*life),1.1f*life,NativeVfxDraw.CoreGlow);
            }
        }
        private void StationConductorFx(NativeFxDraw draw,in StationFxJob j,float now,Vector3 eye,Vector3 right,Vector3 up,bool half)
        {
            // The whistle: a white flash at the mouth on the blast and its ring racing out to the aura's 14 m.
            if(j.Special==NativeStationKinds.Special.Whistle&&!float.IsNaN(j.SpecialAge))
            {
                float wa=j.SpecialAge-NativeStationKinds.WhistleWindup;if(wa>=0&&wa<.3f)vfx.Glow(j.Mouth,Lin(0xFFFFFF,3*(1-wa/.3f)),1.1f*(1-wa/.3f),NativeVfxDraw.CoreGlow);
                if(NativeStationFx.WhistleRing(wa,out float rad,out float al)){draw.Ring(draw.Effects,j.Floor+Vector3.up*.04f,Mathf.Max(0,1-.35f/Mathf.Max(.35f,rad)),rad,FxC(0xF4F8FF,.7f*al),StationGlowing);
                    if(!half)draw.Annulus(draw.Effects,j.Floor+Vector3.up*.9f,Vector3.right*rad*.85f,Vector3.forward*rad*.85f,.97f,1,48,0,FxC(0xFFFFFF,.45f*al),StationGlowing);}
            }
            // The last call: red rings off the lantern as it is rung.
            if(!float.IsNaN(j.LastCall)&&j.Prop!=Vector3.zero)for(int k=0;k<6;k++)if(NativeStationFx.BellRing(j.LastCall,k,out float rr,out float al))draw.Annulus(draw.Effects,j.Prop,right*rr,up*rr,.9f,1,32,0,FxC(NativeStationKinds.LanternRed,al),StationGlowing);
        }
        private void StationUndertowFx(NativeFxDraw draw,in StationFxJob j,float now,Vector3 eye,bool half)
        {
            uint s=j.Seed;float water=j.Floor.y+.35f;
            if(j.Submerged){for(int k=0;k<(half?2:3);k++)if(NativeStationFx.Ripple(k,now,.7f,1.3f,out float rr,out float al))draw.Ring(draw.Effects,new Vector3(j.Eyes.x,water,j.Eyes.z),1-.04f/Mathf.Max(.04f,rr),rr,FxC(0x7AD8C0,.45f*al),StationGlowing);}
            if(!float.IsNaN(j.Surface)&&j.Surface<1.2f){var c=new Vector3(j.Floor.x,water,j.Floor.z)+j.Forward*.3f;for(int i=0;i<(half?18:40);i++)if(NativeStationFx.Splash(s,i,j.Surface,out var off,out float size,out float al))vfx.Drop(c+V(off),Vector3.down*size*2,new Color(.55f,.75f,.72f,.85f*al),size,j.Surface,i);
                for(int k=0;k<3;k++)if(NativeStationFx.Ripple(k,j.Surface,.15f,1.8f,out float rr,out float al2))draw.Ring(draw.Effects,c,1-.05f/Mathf.Max(.05f,rr),rr,FxC(0x9AE8D8,.6f*al2),StationGlowing);}
            // The grab's telegraph: bubbles and ripples converging on the target's feet for .6 s, the eyes lighting.
            if(j.Special==NativeStationKinds.Special.Grab&&!float.IsNaN(j.SpecialAge)&&j.SpecialAge<NativeStationKinds.GrabTelegraph&&j.HasAim)
            {
                float u=j.SpecialAge/NativeStationKinds.GrabTelegraph;var to=new Vector3(j.Aim.x,water,j.Aim.z);var from=new Vector3(j.Eyes.x,water,j.Eyes.z);
                for(int i=0;i<(half?14:30);i++)if(NativeStationFx.Converge(s,i,u,ToN(from),ToN(to),out var at,out float size))vfx.Glow(V(at),Lin(0xBFF8EE,1.1f),size*2.2f,NativeVfxDraw.Ring);
                for(int k=0;k<3;k++){float rr=(1.4f-k*.4f)*(1-u)+.15f;draw.Ring(draw.Effects,to,1-.04f/rr,rr,FxC(0x7AD8C0,.7f*u),StationGlowing);}
            }
            // The drag: spray and a wake behind the held pilot and the Undertow.
            if(!float.IsNaN(j.Hold))for(int i=0;i<(half?6:12);i++){float u=i/12f;var p=Vector3.Lerp(j.Floor,j.Aim,u)-j.Forward*u*.6f;vfx.Smoke(new Vector3(p.x,water+.05f,p.z),new Color(.55f,.66f,.64f),.35f*(1-u),.35f+.2f*u,(now*1.7f+u)%1,s+(uint)i,j.Floor.y);}
            // Drips off a wet body on land.
            if(!j.Submerged&&!half)for(int i=0;i<3;i++)if(NativeStationFx.Drip(s^(uint)i,i,now,out float fall,out float al)){var d=(i==0?j.Hand:i==1?j.Eyes:j.Chest)+Vector3.down*(.08f+fall);vfx.Drop(d,Vector3.down*.06f,new Color(.6f,.82f,.78f,.8f*al),.012f,0,i);}
        }
        private void StationPileupFx(NativeFxDraw draw,in StationFxJob j,float now,Vector3 eye,bool half)
        {
            uint s=j.Seed;
            if(j.Special!=NativeStationKinds.Special.Shove||float.IsNaN(j.SpecialAge))return;
            NativeStationMotion.ShoveCurve(j.SpecialAge,out float brace,out _,out _,out _);
            // The brace: the door's foot scraping the floor in sparks; the shove: dust out along its cone and a flash.
            if(brace>.3f){uint burst=s^(uint)((int)(now*10)*131);float age=now*10%1*.1f;for(int i=0;i<(half?5:10);i++)if(NativeStationFx.Spark(burst,i,age,2.2f,ToN(-j.Forward+Vector3.up*.6f),.5f,out var off,out var vel,out float life))vfx.Streak(j.DoorFoot+V(off),V(vel),Lin(0xFFC070,5*life),.008f,.03f,.035f,.6f);}
            float da=j.SpecialAge-NativeStationKinds.ShoveWindup;var rot=Quaternion.LookRotation(j.Forward);
            for(int i=0;i<(half?8:16);i++)if(NativeStationFx.ShoveDust(s,i,da,out var off2,out float size,out float al))vfx.Smoke(j.Floor+rot*V(off2),new Color(.42f,.4f,.37f),al*.7f,size,Mathf.Clamp01(da/.9f),s+(uint)i,j.Floor.y);
            if(da>=0&&da<.12f)vfx.Glow(j.Prop+j.Forward*.3f,Lin(0xFFFFFF,2*(1-da/.12f)),1.6f*(1-da/.12f),NativeVfxDraw.CoreGlow);
        }
        // A buffed add: its seams flash red-white at the whistle, then a faint red ribbon trails it for the buff.
        private void StationAboardFx(NativeFxDraw draw,in StationFxJob j,float now,Vector3 eye,bool half)
        {
            float fl=NativeStationFx.AboardFlash(j.Aboard),tr=NativeStationFx.AboardTrail(j.Aboard);if(fl>0)vfx.Glow(j.Chest,Lin(0xFFD0D0,3*fl),1.3f*fl,NativeVfxDraw.CoreGlow);
            if(tr<=0||!stationAdds.TryGetValue(j.Id,out var st))return;
            if(now-st.TrailAt>.05f){for(int i=st.Trail.Length-1;i>0;i--)st.Trail[i]=st.Trail[i-1];st.Trail[0]=j.Chest;st.TrailCount=Math.Min(st.TrailCount+1,st.Trail.Length);st.TrailAt=now;}
            for(int i=0;i+1<st.TrailCount;i+=half?2:1){float u=i/(float)st.Trail.Length;StationStrip(draw,st.Trail[i],st.Trail[i+1],.07f*(1-u),.07f*(1-u-.1f),FxC(0xFF2A1A,.55f*tr*(1-u)),eye);}
        }
        private void StationDeadFx(NativeFxDraw draw,in StationDeadJob d,float now,Vector3 eye,bool low)
        {
            float a=d.Age;
            switch(d.Kind)
            {
                case NativeStationKinds.Linesman:
                    // The battery bursts: blue sparks and a flash out of the pack, the last arcs crawling off the body.
                    if(a>.25f&&a<.9f){for(int i=0;i<(low?12:26);i++)if(NativeStationFx.Spark(d.Seed,i,a-.3f,4.5f,ToN(Vector3.up*.6f-d.Forward*.6f),.9f,out var off,out var vel,out float life))vfx.Streak(d.Chest+V(off),V(vel),Lin(NativeStationKinds.LinesmanBlue,5*life),.012f,.04f,.03f,.5f);
                        if(a<.5f)vfx.Glow(d.Chest,Lin(NativeStationKinds.LinesmanBlue,3*(1-(a-.25f)/.25f)),2.2f*(1-(a-.25f)/.25f),NativeVfxDraw.CoreGlow);}
                    if(a<1.2f)for(int k=0;k<(low?1:3);k++)StationBoltFx(d.Seed,40+k,now,d.Chest,d.Chest+new Vector3((k-1)*.5f,-.5f,.3f),.08f,.012f,NativeStationKinds.LinesmanBlue,1-a/1.2f,0,8);
                    break;
                case NativeStationKinds.Conductor:
                {
                    // The lantern's red glass bursting where it hit the floor.
                    float sa=a-NativeStationMotion.LanternShatterAt;var c=d.Rig.MultiplyPoint3x4(new Vector3(.36f+.12f,NativeHordeGait.Floor+.1f,.38f));
                    for(int i=0;i<(low?7:14);i++)if(NativeStationFx.Shard(d.Seed,i,sa,out var off,out float size,out float spin))draw.Cube(draw.Solid,c+V(off),Quaternion.Euler(spin*57,i*40,spin*30),Vector3.one*size,new Color(.75f,.06f,.04f),NativeFxDraw.Authored);
                    if(sa>=0&&sa<.25f)vfx.Glow(c,Lin(NativeStationKinds.LanternRed,3*(1-sa/.25f)),1.4f*(1-sa/.25f),NativeVfxDraw.CoreGlow);
                    break;
                }
                case NativeStationKinds.Undertow:
                {
                    // It sinks: rings spreading over the water and bubbles where it went down.
                    float water=d.Floor.y+.35f;for(int k=0;k<3;k++)if(NativeStationFx.Ripple(k,a,.35f,1.6f,out float rr,out float al))draw.Ring(draw.Effects,new Vector3(d.Floor.x,water,d.Floor.z),1-.05f/Mathf.Max(.05f,rr),rr,FxC(0x7AD8C0,.4f*al),StationGlowing);
                    break;
                }
                case NativeStationKinds.Pileup:
                {
                    // The split: the fused body bursts (gore and ink), the door's clang throws dust off the floor.
                    float sa=d.SplitAge-.2f;var c=d.Floor+Vector3.up*.3f+d.Forward*.2f;
                    for(int i=0;i<(low?14:28);i++)if(NativeStationFx.SplitChunk(d.Seed,i,sa,out var off,out float size,out float spin))draw.Cube(draw.Solid,c+V(off),Quaternion.Euler(spin*57,i*33,spin*20),Vector3.one*size,new Color(.12f,.04f,.07f),NativeFxDraw.Authored);
                    if(sa>=0&&sa<1)for(int i=0;i<(low?4:8);i++)vfx.Smoke(c+new Vector3((i%3-1)*.4f,.1f+.15f*(i%2),(i/3-1)*.4f)*(1+sa),new Color(.16f,.13f,.19f),.55f*(1-sa),.6f+sa,sa,d.Seed+(uint)i,d.Floor.y);
                    float clang=a-.5f;if(clang>=0&&clang<.6f)for(int i=0;i<(low?4:8);i++)vfx.Smoke(d.Floor+d.Forward*(.9f+.1f*i)+Vector3.up*.08f,new Color(.42f,.4f,.37f),.5f*(1-clang/.6f),.5f+clang,clang/.6f,d.Seed+(uint)(20+i),d.Floor.y);
                    break;
                }
            }
        }

        // ================================================================================================================
        // (i) The Dev Sandbox's minion gallery: the four station kinds in a second row, looping their whole set from gallery views
        // ================================================================================================================
        private readonly CapturedAnimation.Player[] stationGalleryRigs=new CapturedAnimation.Player[4];private readonly HordeKindState[] stationGalleryStates=new HordeKindState[4];
        private readonly StationAdd[] stationGalleryAdds=new StationAdd[4];private readonly NativeStationView[] stationGalleryViews=new NativeStationView[4];private readonly Vector3[] stationTargets=new Vector3[3];
        /// <summary>The station gallery's loop (s): idle, a walk, the special, the melee, a hit, the kind's state (grounded, the last
        /// call, submerged and surfacing, the shove), the death.</summary>
        public const float StationGalleryLoop=16;
        private void StationGallery(NativePilot me,float now,Vector3 at,Vector3 across,float yawToMe,float ground)
        {
            float yr=yawToMe*Mathf.Deg2Rad;var back=-new Vector3(Mathf.Sin(yr),0,Mathf.Cos(yr));
            for(int i=0;i<4;i++)
            {
                int kind=NativeStationKinds.First+i,id=991000+i;if(stationGalleryRigs[i]==null){stationGalleryRigs[i]=captured.Create(transform,id);stationGalleryStates[i]=new HordeKindState();stationGalleryStates[i].Reset(id);stationGalleryStates[i].Kind=kind;stationGalleryStates[i].React.Bind(kind);stationGalleryAdds[i]=new StationAdd();stationGalleryAdds[i].Reset(id);stationGalleryViews[i]=new NativeStationView();}
                var rig=stationGalleryRigs[i];var s=stationGalleryStates[i];var st=stationGalleryAdds[i];var v=stationGalleryViews[i];var row=NativeHordeKinds.ProposedRow(kind);float sc=NativeHordeKinds.Scale[kind];s.Scale=sc;
                float loopStart=now-Mathf.Repeat(now+i*.53f,StationGalleryLoop),t=now-loopStart;var slot=at+back*3.2f+across*((i-1.5f)*2.6f);var pos=slot;float yaw=yawToMe;var vel=Vector3.zero;
                v.Clear();float lastAttack=-100;float melee=float.NaN;var attack=NativeHordeKinds.AttackOf(kind,row.attack,false);float strike=NativeHordeKinds.Strike(row,false,0),recover=NativeHordeKinds.Recover(row,false,0);
                // 2-5 s: a walk round a circle; 6 s: the special; 9.5 s: the melee; 11.2 s: a sword hit; then the kind's state; 13.6 s: the death.
                if(t>=2&&t<5){float speed=row.walk,rad=.7f,ang=(t-2)*speed/rad;pos=slot+new Vector3(Mathf.Cos(ang)-1,0,Mathf.Sin(ang))*rad;vel=new Vector3(-Mathf.Sin(ang),0,Mathf.Cos(ang))*speed;yaw=Mathf.Atan2(vel.x,vel.z)*Mathf.Rad2Deg;}
                string special=kind==NativeStationKinds.Linesman?"arc":kind==NativeStationKinds.Conductor?"whistle":kind==NativeStationKinds.Undertow?"grab":"shove";
                if(t>=6&&t<9.5f){lastAttack=loopStart+6;v.attack=special;}else if(t>=9.5f&&t<9.5f+recover){lastAttack=loopStart+9.5f;melee=t-9.5f;}
                var target=me.Body.position;stationTargets[0]=target+Vector3.up*.3f;stationTargets[1]=target+across*1.6f+Vector3.up*.3f;stationTargets[2]=target-across*1.4f+Vector3.up*.3f;
                if(kind==NativeStationKinds.Linesman){v.arcTargets=stationArcSeats;v.arcAt=loopStart+6+NativeStationKinds.ArcWindup;v.grounded=t>=11.8f&&t<13.6f;}
                if(kind==NativeStationKinds.Conductor){v.lastCallAt=loopStart+11.9f;v.lanternBroken=t>=13.2f;}
                if(kind==NativeStationKinds.Undertow){v.submerged=t<1.6f||t>=12f;v.surfaceAt=t<6?loopStart+1.6f:loopStart+100;if(t>=6.6f&&t<8){v.grabTarget=-1;v.grabAt=loopStart+6.6f;v.releaseAt=loopStart+7.8f;}}
                if(t>=13.6f)
                {
                    float d=t-13.6f;NativeHordeGait.Death(minionPose,kind,NativeHordeKinds.Seed(id),Mathf.Min(d,2.3f),new System.Numerics.Vector2(0,-1));float sink=StationDeathPose(kind,NativeHordeKinds.Seed(id),d);
                    NativePoseWriter.Write(rig.Bones,minionPose,"a gallery rig");rig.Root.transform.SetPositionAndRotation(new Vector3(slot.x,ground+NativeHordeKinds.DrawRoot(kind,false)-sink,slot.z),Quaternion.Euler(0,yaw,0));rig.Root.transform.localScale=Vector3.one*sc;
                    bool body=!NativeMinionFx.Gone(d);if(body)HordeDeathBody(rig,NativeHordeModels.Id(kind,0,NativeHordeModels.Full),id,0,d,null);
                    stationGalleryDeath.id=id;stationGalleryDeath.position=slot+Vector3.up*NativeHordeKinds.RootHeight(kind,false);stationGalleryDeath.yaw=yaw;StationDeathFx(stationGalleryDeath,kind,NativeHordeKinds.Seed(id),d,rig,ground);
                    HordeAdd(rig,id,float.NaN,d,ground,ground,float.NaN,Vector3.zero,0,body,kind*8);st.Motion.Reset();s.Gait.Reset();continue;
                }
                if(t>=11.2f&&s.React.Newest<loopStart+11.2f-.01f){float hy=yawToMe*Mathf.Deg2Rad;s.React.Add(loopStart+11.2f,new System.Numerics.Vector2(-Mathf.Sin(hy),-Mathf.Cos(hy)),StaggerWeight.Step,26,true,false,false,row.stagger);st.HitAt=loopStart+11.2f;}
                ref var f=ref hordeInput[0];float inv=1/sc;
                f=new HordeMotionInput{Kind=kind,Look=0,Id=id,Seed=NativeHordeKinds.Seed(id),Time=now,Yaw=yaw,Ground=ground*inv,Root=ToN(new Vector3(pos.x,ground+NativeHordeKinds.DrawRoot(kind,false),pos.z))*inv,Velocity=ToN(vel)*inv,Treadmill=false,
                    AttackAge=melee,Strike=strike,Recover=recover,Attack=attack,Side=kind==NativeStationKinds.Conductor?-1:1,Health=1,HasTarget=true,Target=ToN(me.Body.position)*inv};
                if(kind==NativeStationKinds.Undertow&&!string.IsNullOrEmpty(v.attack)&&t>=6){f.AttackAge=t-6;f.Attack=NativeHordeKinds.Attack.Lunge;}
                s.Gait.Pose(f,minionPose,s.React);s.Distance=Vector3.Distance(pos,hordeEye);
                var sp=NativeStationKinds.SpecialOf(kind,v.attack);float spAge=sp!=NativeStationKinds.Special.None?now-lastAttack:float.NaN;
                pos.y=ground+NativeHordeKinds.DrawRoot(kind,false);bool sumping=kind==NativeStationKinds.Undertow&&!float.IsNaN(t)&&t>=1.6f&&t<1.6f+NativeStationMotion.SurfaceSeconds;
                stationGalleryTargets=stationTargets;
                StationPose(st,s,kind,id,now,f,sp==NativeStationKinds.Special.Grab?NativeStationKinds.Special.Grab:sp,spAge,v,stationTargets[0],v.Holding(now)?target+Vector3.down*.75f:(Vector3?)null,kind==NativeStationKinds.Undertow&&t>=1.6f&&t<6?t-1.6f:float.NaN,false,ref pos);
                StationCollect(st,s,kind,id,now,ground,pos,sp,spAge,v,null,0);stationGalleryTargets=null;
                NativePoseWriter.Write(rig.Bones,minionPose,"a gallery rig");rig.Root.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,s.Gait.Yaw,0));rig.Root.transform.localScale=Vector3.one*sc;
                float gk=NativeHordeFx.Brighten(melee,strike,recover);var glow=HordeGlow(id);glow=new Vector4(gk,gk,gk,glow.w);float k0=st.Out.Glow;glow.x*=k0;glow.y*=k0;glow.z*=k0;
                float flash=now-st.HitAt<.12f?NativeHitFlash.Code(now-st.HitAt,false,EffectsQuality):1;
                HordeQueue(rig,NativeHordeModels.Id(kind,0,NativeHordeModels.Full),id,0,0,0,glow,flash);
                if(st.Out.Prop)StationQueueProp(rig,st,NativeHordeModels.Full,0,glow,flash,now,StationPropGlow(st,kind,sp==NativeStationKinds.Special.Whistle?spAge:float.NaN,now>=v.lastCallAt?now-v.lastCallAt:float.NaN,now));
                HordeAdd(rig,id,float.NaN,float.NaN,ground,ground,float.NaN,vel,0,true,kind*8);
            }
        }
        private static readonly int[] stationArcSeats={-1,-2,-3};private readonly NativeDeath stationGalleryDeath=new NativeDeath();
        private void ClearStationGallery(){for(int i=0;i<stationGalleryRigs.Length;i++){stationGalleryRigs[i]?.Dispose();stationGalleryRigs[i]=null;stationGalleryStates[i]=null;stationGalleryAdds[i]=null;}}
        /// <summary>A new arena, generation or rewind: forget every station add's state, the dead and the effects in flight.</summary>
        private void ClearStationKinds(){foreach(var s in stationAdds.Values)stationSpare.Push(s);stationAdds.Clear();stationJobCount=0;stationDeadCount=0;stationEvent=0;for(int i=0;i<stationHits.Length;i++)stationHits[i].Time=-100;for(int i=0;i<stationDrops.Length;i++)stationDrops[i].Live=false;}
        /// <summary>HordeKindsEnd: drop the states of adds gone for longer than their death's effects.</summary>
        private void StationKindsEnd(float now)
        {
            stationStale.Clear();foreach(var p in stationAdds)if(now-p.Value.SeenAt>NativeMinionFx.DeathFxSeconds+.5f||now<p.Value.SeenAt-.25f)stationStale.Add(p.Key);
            foreach(int id in stationStale){stationSpare.Push(stationAdds[id]);stationAdds.Remove(id);}
        }
    }

    public sealed partial class NativeAudio
    {
        // ---------- the station kinds' cues (docs/EXPEDITION-CENTRAL-STATION.md section 10; Core NativeStationKinds keys) ----------
        // Each cue plays its pve.station key's bank clips, else its fitting existing stand-in, else nothing (CreatureCue), at the
        // moments the presentation shows them: from the replicated LastAttack, the view's times, hits and deaths. Bank-only.
        private readonly Dictionary<int,int> stationKinds=new Dictionary<int,int>();
        private void StationCues(NativeMinion m,int kind,Vector3 pos,float t)
        {
            if(!NativeStationKinds.Is(kind))return;stationKinds[m.Id]=kind;var v=ArenaPresentation.StationViewOf(m);var sp=NativeStationKinds.SpecialOf(kind,v?.attack);uint seed=NativeHordeKinds.Seed(m.Id);
            float special=sp!=NativeStationKinds.Special.None?m.LastAttack:-100,strike=special+NativeStationKinds.SpecialStrike(sp);
            switch(kind)
            {
                case NativeStationKinds.Linesman:
                    if(sp==NativeStationKinds.Special.Arc){StationCue(m.Id,"c",special,t,NativeStationKinds.LinesmanCharge,pos,.8f);StationCue(m.Id,"a",v!=null&&v.arcAt>-50?v.arcAt:strike,t,NativeStationKinds.LinesmanArc,pos,.9f);}
                    if(v!=null&&v.grounded)StationCue(m.Id,"g",StationEdge(m.Id,1,t),t,NativeStationKinds.LinesmanGround,pos,.75f);else StationEdgeClear(m.Id,1);
                    StationCall(m.Id,seed,t,7.5f,NativeStationKinds.LinesmanIdle,pos,.55f);break;
                case NativeStationKinds.Conductor:
                    if(sp==NativeStationKinds.Special.Whistle){StationCue(m.Id,"i",special,t,NativeStationKinds.ConductorInhale,pos,.6f);StationCue(m.Id,"w",strike,t,NativeStationKinds.ConductorWhistle,pos,1f);}
                    if(v!=null&&v.lastCallAt>-50)StationCue(m.Id,"b",v.lastCallAt,t,NativeStationKinds.ConductorBell,pos,.9f);
                    if(v!=null&&v.lanternBroken)StationCue(m.Id,"l",StationEdge(m.Id,2,t),t,NativeStationKinds.ConductorLantern,pos,.85f);else StationEdgeClear(m.Id,2);
                    StationCall(m.Id,seed,t,8.5f,NativeStationKinds.ConductorMutter,pos,.5f);break;
                case NativeStationKinds.Undertow:
                    if(v!=null&&v.submerged)StationCall(m.Id,seed,t,2.1f,NativeStationKinds.UndertowBubble,pos,.45f);
                    if(v!=null&&v.surfaceAt>-50)StationCue(m.Id,"s",v.surfaceAt,t,NativeStationKinds.UndertowSurface,pos,.85f);
                    if(v!=null&&v.grabAt>-50){StationCue(m.Id,"g",v.grabAt,t,NativeStationKinds.UndertowGrab,pos,.85f);if(v.Holding(t)){float n=Mathf.Floor((t-v.grabAt)/1.4f);StationCue(m.Id,"d",v.grabAt+n*1.4f,t,NativeStationKinds.UndertowDrag,pos,.6f);}}
                    break;
                case NativeStationKinds.Pileup:
                    if(sp==NativeStationKinds.Special.Shove){StationCue(m.Id,"r",special,t,NativeStationKinds.PileupBrace,pos,.75f);StationCue(m.Id,"v",strike,t,NativeStationKinds.PileupShove,pos,.9f);}
                    StationCall(m.Id,seed,t,6.5f,NativeStationKinds.PileupMoan,pos,.6f);break;
            }
        }
        // A cue at `at` (once per add and moment, within .12 s of it).
        private void StationCue(int id,string tag,float at,float t,string key,Vector3 pos,float gain){if(at<-50||t<at||t-at>=.12f)return;if(!Once("st"+id+tag,at))return;CreatureCue(key,NativeStationKinds.StandIn(key),1,pos,gain,1,NativeSoundPriority.Normal);}
        // A seeded call every `period` s or so (the idle buzz, the mutter, the bubbles, the moan).
        private void StationCall(int id,uint seed,float t,float period,string key,Vector3 pos,float gain){float k=(t+period*NativeHordeKinds.Hash(seed,90))/period;float at=(Mathf.Floor(k)+.3f*NativeHordeKinds.Hash(seed^(uint)Mathf.Floor(k),91))*period-period*NativeHordeKinds.Hash(seed,90);StationCue(id,"p",at,t,key,pos,gain);}
        // An edge of a view flag (grounded, the lantern broken): the time it turned on, NaN-free (-100 while off).
        private readonly Dictionary<long,float> stationEdges=new Dictionary<long,float>();
        private float StationEdge(int id,int which,float t){long key=((long)id<<4)|(long)which;if(!stationEdges.TryGetValue(key,out float at)){at=t;stationEdges[key]=at;}return at;}
        private void StationEdgeClear(int id,int which){stationEdges.Remove(((long)id<<4)|(long)which);}
        /// <summary>The station kinds' hurts (the Linesman's static pop over the zombie's grunt), the Pileup's ricochets off its
        /// door from the front, and their deaths (the battery bursting, the lantern's clatter, the drowning, the split).</summary>
        private void StationEvents(NativeArena arena,float t)
        {
            var events=arena.Events;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(t-e.Time>.3f||t<e.Time||(e.Kind!=EventKind.Hit&&e.Kind!=EventKind.Headshot)||e.Target<10000||!(e.Amount>0)||!stationKinds.TryGetValue(e.Target,out int kind))continue;
                var m=ZombieOf(arena,e.Target);if(m==null||m.Body==null)continue;string hurt=NativeStationKinds.HurtCue(kind);if(hurt!=null)StationCue(m.Id,"h"+(e.Id&7),e.Time,t,hurt,m.Body.position,.55f);
                if(kind==NativeStationKinds.Pileup){var dir=NativeArena.Vec(e.Normal);dir.y=0;float yr=m.Yaw*Mathf.Deg2Rad;if(dir.sqrMagnitude>1e-4f&&Vector3.Dot(-dir.normalized,new Vector3(Mathf.Sin(yr),0,Mathf.Cos(yr)))>=Mathf.Cos(NativeStationKinds.FrontCone*.5f*Mathf.Deg2Rad))StationCue(m.Id,"x"+(e.Id&7),e.Time,t,NativeStationKinds.PileupRicochet,m.Body.position,.7f);}
            }
            var deaths=arena.MinionDeaths;
            for(int i=0;i<deaths.Count;i++){var d=deaths[i];if(d==null||!stationKinds.TryGetValue(d.id,out int kind))continue;string key=NativeStationKinds.DeathCue(kind);if(key!=null)StationCue(d.id,"z",d.at+(kind==NativeStationKinds.Linesman?.3f:kind==NativeStationKinds.Pileup?.2f:0),t,key,d.position,.9f);
                if(kind==NativeStationKinds.Conductor)StationCue(d.id,"y",d.at+NativeStationMotion.LanternShatterAt,t,NativeStationKinds.ConductorLantern,d.position,.8f);}
        }
        private void StationCuesClear(){stationKinds.Clear();stationEdges.Clear();}
        /// <summary>Whether the add's LastAttack started a station special (its melee's strike cue then stays silent: the special has its own).</summary>
        private static bool StationSpecialNow(NativeMinion m,int kind){if(!NativeStationKinds.Is(kind))return false;var sp=NativeStationKinds.SpecialOf(kind,ArenaPresentation.StationViewOf(m)?.attack);return sp!=NativeStationKinds.Special.None&&sp!=NativeStationKinds.Special.Grab;}
    }

    public sealed partial class NativeSourceModels
    {
        /// <summary>The station kinds' props (the Conductor's lantern, the Pileup's door), each tier, built with the horde kinds.</summary>
        private void StationPropModels(Shader shader)
        {
            var material=new Material(shader){name="Native station props",enableInstancing=true};material.SetInt("_Cull",2);material.SetFloat("_Ink",0);material.SetFloat(MinionGlowId,1);materials.Add(material);
            foreach(NativeHordeModels.StationProp prop in Enum.GetValues(typeof(NativeHordeModels.StationProp)))for(int tier=0;tier<NativeHordeModels.Tiers;tier++)
            {
                string id=NativeHordeModels.StationPropId(prop,tier);var built=NativeHordeModels.BuildStationProp(prop,tier);var parts=new List<Part>();
                foreach(var s in built.Sections){var mesh=BodyMesh(s,id+" / joint "+s.Bone);if(mesh!=null)parts.Add(new Part{Name=id,Bone=-1,Mesh=mesh,Material=material,NoShadow=tier!=NativeHordeModels.Full});}
                models[id]=parts.ToArray();
            }
        }
    }
}
