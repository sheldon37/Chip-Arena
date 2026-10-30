using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NQuaternion=System.Numerics.Quaternion;

namespace ChipArena.UnityConversion
{
    // The City Ironworks' five enemies on the PvE horde (docs/EXPEDITION-CITY-IRONWORKS.md 6 and 10; owner 2026-10-16: "Go with
    // your picks, add the rigger as well"). Presentation only, in the horde kinds' pipeline (Runtime/NativeHordeKindPresentation.cs
    // hands them over at its hooks): their sculpts (Core/NativeIronworksModels) on the gait with the Ironworks layer
    // (Core/NativeIronworksMotion), their props (the rake, the ladle, the crane hook on its chain, the lamp's lens, the slagback's
    // crust), their effects (Core/NativeIronworksFx: the telegraphs cream and cyan, never the environment's orange), their deaths
    // and their cue routing (pve.ironworks.<kind>.<cue>, played when the owner's bank has the clips). Everything is read from what
    // every peer holds: the add's replicated kind, position, yaw, LastAttack, entry, SpawnAt/ActiveAt and hits, plus the view the
    // rules will replicate (IronworksView; IronworksViewOf is the one-line binding: until Astra fills it, each kind plays its
    // attack and entrance only, and the DEV gallery plays the rest). No hit, collider, timing, damage, movement or network state
    // is read back or changed. Also the zone tint: the brute's seams cream while the Ironworks is the map.
    // Allocation free in steady state (fixed buffers, pooled states).
    public sealed partial class ArenaPresentation
    {
        /// <summary>
        /// The add's Ironworks view: the fields the rules will replicate (docs/EXPEDITION-CITY-IRONWORKS.md 10.4), named as there.
        /// The binding is this one line: when the network frame carries them, return them here, e.g.
        /// <c>new IronworksView{pullTarget=a.PullTarget,pullAt=a.PullAt,shedAt=a.ShedAt,pourAt=a.PourAt,pourYaw=a.PourYaw,spilledAt=a.SpilledAt,hang=a.Hang,lockAt=a.LockAt,lockX=a.LockX,lockZ=a.LockZ,climbAt=a.ClimbAt,climbTo=a.ClimbTo,chainCutAt=a.ChainCutAt}</c>.
        /// </summary>
        internal static IronworksView IronworksViewOf(NativeMinion m)=>m.Pve?.Zone.Ironworks??IronworksView.None;

        // ---- the zone tint: the brute's cream seams while the City Ironworks is the map (NativeHordeKinds.SeamOf, the cream models) ----
        private void IronworksZone(NativeArena arena){NativeIronworksKinds.Zone=arena!=null&&NativeIronworksKinds.ZoneOf(arena.MapId);}

        private static Vector3 IronU(NVector v)=>new Vector3(v.X,v.Y,v.Z);
        private static Quaternion IronU(NQuaternion q)=>new Quaternion(q.X,q.Y,q.Z,q.W);
        /// <summary>A source-frame point of the posed rig in the world (as the bones: x mirrored under the rig root).</summary>
        private static Vector3 IronWorld(CapturedAnimation.Player rig,NVector source)=>rig.Root.transform.TransformPoint(new Vector3(-source.X,source.Y,source.Z));
        private static Matrix4x4 IronMatrix(CapturedAnimation.Player rig,NVector at,NQuaternion turn,float scale)
        {
            var m=CapturedRig.Mirror(turn);return rig.Root.transform.localToWorldMatrix*Matrix4x4.TRS(new Vector3(-at.X,at.Y,at.Z),new Quaternion(m.X,m.Y,m.Z,m.W),Vector3.one*scale);
        }
        private PveEntry IronworksEntry(NativeArena arena,int index)
        {
            if(index<0)return null;var list=arena?.Pve?.Content?.entries??NativeArena.StageContent?.entries;if(list==null)return null;
            for(int i=0;i<list.Length;i++)if(list[i]!=null&&list[i].e==index)return list[i];return null;
        }

        // ================================================================================================================
        // The live add: the gait's view before the pose, the props and effects after it, the Rigger off the ground
        // ================================================================================================================
        /// <summary>HordeKindAdd, before the gait poses an add: the view onto its gait (and the pilot the Stoker's hook holds).</summary>
        private void IronworksInput(NativeArena arena,HordeKindState s,NativeMinion m)
        {
            if(!NativeIronworksKinds.Is(s.Kind)){s.Gait.Iron=IronworksView.None;return;}
            var v=IronworksViewOf(m);s.Gait.Iron=v;var hooked=v.pullTarget>=0?HordePilot(arena,v.pullTarget):null;s.Gait.IronTargetKnown=hooked?.Body!=null;
            if(hooked?.Body!=null){var p=hooked.Body.position/s.Scale;s.Gait.IronTarget=new NVector(p.x,p.y,p.z);}
        }
        /// <summary>HordeKindAdd, after the body is queued: the props on their matrices and the frame's effects job.</summary>
        private void IronworksQueue(NativeArena arena,CapturedAnimation.Player rig,HordeKindState s,NativeMinion m,float ground,Vector4 glow,float flash)
        {
            // (only the gait's own frame of this add: an entrance pose has no props yet. The gait's input carries the horde clock
            // HordeKindAdd poses on, hordeNow; matched against arena.Time this skipped every live add on the host, guests and the
            // killcam, which then drew no rake, ladle, hook, crust, lamp or effects.)
            int z=NativeIronworksKinds.Local(s.Kind);ref var f=ref hordeInput[0];if(z<0||f.Id!=m.Id||f.Time!=hordeNow)return;
            s.Gait.PlaceProps(f,minionPose);IronworksProps(rig,s.Gait,s.Tier,glow,flash,s.Scale);bool crusted=z==NativeIronworksKinds.Slagback&&NativeIronworksKinds.Crusted(s.Gait.Iron.shedAt,f.Time);
            if(crusted)IronworksCrust(rig,s.Look,s.Tier,m.Pve?.Lost??0,glow,flash);
            // What it carries, for its death: the held prop where it is now, the crust still on.
            var g=s.Gait;ironCarry[m.Id]=new IronCarry{Has=g.PropCount>0,Prop=g.PropCount>0?g.Props[0].Prop:-1,M=g.PropCount>0?IronMatrix(rig,g.Props[0].At,g.Props[0].Turn,g.Props[0].Scale):Matrix4x4.identity,Crusted=crusted,Look=s.Look,Seen=f.Time};
            if(z==NativeIronworksKinds.Rigger&&NativeIronworksKinds.Lamp(s.Gait.Iron.lockAt,f.Time,false))IronworksLamp(rig);
            var target=HordeTarget(arena,rig.Root.transform.position);var hooked=s.Gait.Iron.pullTarget>=0?HordePilot(arena,s.Gait.Iron.pullTarget):null;
            IronworksJob(rig,s,z,f,ground,(hooked??target)?.Body!=null?(hooked??target).Body.position:rig.Root.transform.position,(hooked??target)?.Body!=null,false,Vector3.zero,Vector3.zero);
        }
        private void IronworksProps(CapturedAnimation.Player rig,NativeHordeGait g,int tier,Vector4 glow,float flash,float scale)
        {
            int t=tier==NativeHordeModels.Full?0:1;
            for(int i=0;i<g.PropCount;i++){var p=g.Props[i];sourceModels.Queue(NativeHordeModels.PropId(p.Prop,t),IronMatrix(rig,p.At,p.Turn,p.Scale),glow:glow,hitFlash:flash);}
            for(int i=0;i<g.ChainCount;i++){var c=g.Chains[i];IronworksChain(IronWorld(rig,c.From),IronWorld(rig,c.To),c.Sag*scale,t);}
        }
        /// <summary>A chain of links from a to b (world), sagging at its middle: at most 48 links (a long chain's links stretched).</summary>
        private void IronworksChain(Vector3 a,Vector3 b,float sag,int tier)
        {
            float L=(b-a).magnitude;if(L<.02f)return;int n=Mathf.Clamp(Mathf.RoundToInt(L/NativeHordeModels.LinkPitch),1,48);float stretch=L/(n*NativeHordeModels.LinkPitch);string id=NativeHordeModels.PropId(NativeHordeModels.PropLink,tier);
            for(int i=0;i<n;i++)
            {
                float u0=i/(float)n,u1=(i+1f)/n;Vector3 p0=Vector3.Lerp(a,b,u0)+Vector3.down*(sag*4*u0*(1-u0)),p1=Vector3.Lerp(a,b,u1)+Vector3.down*(sag*4*u1*(1-u1));var d=p1-p0;if(d.sqrMagnitude<1e-8f)continue;
                var rot=Quaternion.LookRotation(d,Mathf.Abs(d.normalized.y)>.9f?Vector3.right:Vector3.up)*Quaternion.Euler(0,0,(i&1)*90);
                sourceModels.Queue(id,Matrix4x4.TRS((p0+p1)*.5f,rot,new Vector3(1,1,stretch)));
            }
        }
        private void IronworksCrust(CapturedAnimation.Player rig,int look,int tier,int lost,Vector4 glow,float flash)
        {
            hordeQueueRig=rig;hordeQueueLost=lost;hordeQueueHidden=0;hordeQueueVisible??=b=>HordeJointShown(hordeQueueLost,b)&&(hordeQueueHidden==0||!HordeHiddenJoint(hordeQueueHidden,b));
            ironBone??=b=>hordeQueueRig.Bones[b].localToWorldMatrix;
            sourceModels.Queue(IronworksCrustId(look,tier),rig.Root.transform.localToWorldMatrix,ironBone,hordeQueueVisible,glow:glow,hitFlash:flash);
        }
        private Func<int,Matrix4x4> ironBone;
        /// <summary>HordeCorpsesDraw: a slagback corpse's crust (crust look `look`) on its kept pose.</summary>
        private void IronworksCorpseCrust(int look,int tier,Matrix4x4 root,Func<int,Matrix4x4> bone,Func<int,bool> visible,Vector4 glow,float alpha)
        {
            string id=IronworksCrustId(look,tier);if(!sourceModels.Has(id))return;var body=Color.white;body.a=alpha;sourceModels.Queue(id,root,bone,visible,body:body,glow:glow);
        }
        private void IronworksLamp(CapturedAnimation.Player rig)=>sourceModels.Queue(NativeHordeModels.PropId(NativeHordeModels.PropLamp,0),rig.Bones[CapturedRig.Head].localToWorldMatrix*Matrix4x4.Translate(new Vector3(0,.335f,.2f)));
        private static string[] ironCrustIds;
        private static string IronworksCrustId(int look,int tier){if(ironCrustIds==null){var a=new string[2*NativeHordeModels.Tiers];for(int l=0;l<2;l++)for(int t=0;t<NativeHordeModels.Tiers;t++)a[l*NativeHordeModels.Tiers+t]="iw-slagback-crust-"+l+"-"+NativeHordeModels.TierNames[t];ironCrustIds=a;}return ironCrustIds[Mathf.Clamp(look,0,1)*NativeHordeModels.Tiers+Mathf.Clamp(tier,0,NativeHordeModels.Tiers-1)];}

        // ---- the Rigger off the ground: hanging, the drop, sliding, the landing, the climb, the fall ----
        private static readonly NativeHordeDrop.DropProfile IronRiggerLand=new NativeHordeDrop.DropProfile(.1f,.1f,NativeIronworksKinds.DropRecover,24,.1f,2,NativeIronworksKinds.DropRadius,8,.04f,0,false);
        /// <summary>HordeKindAdd, before the zombie entrances and the gait: a Rigger on its chain (a hang point from the view, or its
        /// ceiling entrance: the same drop onto the entry's exit) is posed here; false hands every other add back.</summary>
        private bool IronworksAdd(NativeArena arena,NativeMinion m,CapturedAnimation.Player rig,HordeKindState s,int kind,float now)
        {
            if(NativeIronworksKinds.Local(kind)!=NativeIronworksKinds.Rigger)return false;var a=m.Pve;var v=IronworksViewOf(m);
            bool entering=!a.Travelling&&now<a.ActiveAt;PveEntry hang=null;Vector3 spot;
            if(entering&&a.Entry!=null&&(a.Entry.surface=="ceiling"||NativeHordeDrop.Is(a.Entry))){hang=a.Entry;v.hang=a.Entry.e;v.lockAt=NativeIronworksKinds.EntryLock(a.ActiveAt);spot=NativeArena.Vec(a.Entry.@exit.Position(0));v.lockX=spot.x;v.lockZ=spot.z;if(now<a.SpawnAt)return true;}
            else if(v.hang>=0){hang=IronworksEntry(arena,v.hang);if(hang==null)return false;spot=NativeArena.Vec(hang.@exit.Position(0));}
            else if(!float.IsNaN(v.climbAt)&&v.climbTo>=0){hang=IronworksEntry(arena,v.climbTo);if(hang==null)return false;spot=m.Body.position;spot.y-=NativeHordeKinds.RootHeight(kind,false);}
            else if(!float.IsNaN(v.chainCutAt)&&now<v.chainCutAt+3){spot=m.Body.position;spot.y-=NativeHordeKinds.RootHeight(kind,false);}
            else return false;
            var ph=NativeIronworksKinds.Drop(v.lockAt,now,out float into);
            // Landed and recovering on the ground: the gait takes over once the landing's crouch is up.
            if(float.IsNaN(v.chainCutAt)&&float.IsNaN(v.climbAt)&&ph==NativeIronworksKinds.DropPhase.Done)return false;
            s.Gait.Iron=v;float sc=s.Scale;uint seed=NativeHordeKinds.Seed(m.Id);float feet=NativeIronworksKinds.HangHeight(hang);var land=new Vector3(v.lockX,spot.y,v.lockZ);if(float.IsNaN(v.lockAt))land=spot;
            float yaw=m.Yaw;var anchor=(hang!=null?NativeArena.Vec(hang.@exit.Position(0)):spot)+Vector3.up*(feet+NativeIronworksKinds.GirderAbove);
            Vector3 root=spot;bool lampOn=NativeIronworksKinds.Lamp(v.lockAt,now,true);float sway=.12f*Mathf.Sin(now*Mathf.PI*2/NativeIronworksKinds.SwayPeriod+NativeHordeKinds.Hash(seed,330)*6);NativeHordeGait.RiggerState state=NativeHordeGait.RiggerState.Hang;float u=now;bool onFloor=false;
            float climbed=NativeIronworksKinds.Climbed(v.climbAt,now,feet,out bool throwing);
            if(!float.IsNaN(v.chainCutAt)&&now>=v.chainCutAt)
            {
                float from=feet*(float.IsNaN(v.climbAt)?1:NativeIronworksKinds.Climbed(v.climbAt,v.chainCutAt,feet,out _));float h=NativeIronworksKinds.Fallen(v.chainCutAt,now,from,out bool landed,out float since);
                state=landed?NativeHordeGait.RiggerState.Stun:NativeHordeGait.RiggerState.Fall;u=landed?since:now-v.chainCutAt;root=spot+Vector3.up*(h+NativeHordeKinds.DrawRoot(kind,false));onFloor=landed;lampOn=!landed||since<.15f;
                if(landed&&since>NativeIronworksKinds.FallStun)return false;
            }
            else if(!float.IsNaN(v.climbAt))
            {
                state=throwing?NativeHordeGait.RiggerState.Throw:climbed>=1?NativeHordeGait.RiggerState.Hang:NativeHordeGait.RiggerState.Climb;u=now-v.climbAt;
                var under=new Vector3(anchor.x,spot.y,anchor.z);root=Vector3.Lerp(spot,under,climbed)+Vector3.up*(feet*climbed+NativeHordeKinds.DrawRoot(kind,false));lampOn=false;
            }
            if(ph==NativeIronworksKinds.DropPhase.Slide&&float.IsNaN(v.climbAt)&&!(!float.IsNaN(v.chainCutAt)&&now>=v.chainCutAt)){state=NativeHordeGait.RiggerState.Slide;u=NativeIronworksKinds.Slid(v.lockAt,now);var top=new Vector3(anchor.x,spot.y+feet,anchor.z);root=Vector3.Lerp(top,land,u)+Vector3.up*NativeHordeKinds.DrawRoot(kind,false);}
            bool posed=false;
            if(ph==NativeIronworksKinds.DropPhase.Land&&float.IsNaN(v.chainCutAt)&&float.IsNaN(v.climbAt))
            {posed=true;
                // the landing's crouch at the light profile (no Hulk-class shake), its dust ring at .6 of the brute's
                NativeHordeGait.Drop(minionPose,seed,10-NativeIronworksKinds.DropRecover+into,10,2,IronRiggerLand,out float lift,out _);root=land+Vector3.up*(NativeHordeKinds.DrawRoot(kind,false)+lift);onFloor=true;
                HordeImpactAt(m.Id,land,v.lockAt+NativeIronworksKinds.DropLand,seed^0x71u,NativeZombieFx.Landing(NativeHordeDrop.Brute).WithRadius(NativeIronworksKinds.DropRadius),NativeIronworksKinds.Cream);
            }
            else if(!(!float.IsNaN(v.chainCutAt)&&now>=v.chainCutAt)&&float.IsNaN(v.climbAt)&&ph!=NativeIronworksKinds.DropPhase.Slide)root=new Vector3(anchor.x,spot.y+feet,anchor.z)+Vector3.up*NativeHordeKinds.DrawRoot(kind,false);
            if(!posed)NativeHordeGait.RiggerHang(minionPose,seed,state,u,now,state==NativeHordeGait.RiggerState.Hang?sway:sway*.4f);
            s.Gait.Reset();NativePoseWriter.Write(rig.Bones,minionPose,"a horde rig");rig.Root.transform.SetPositionAndRotation(root,Quaternion.Euler(0,yaw,0));rig.Root.transform.localScale=Vector3.one*sc;
            var glow=HordeGlowFor(m.Id,s,m,now);float flash=HitFlashCode(m.Id,now);
            HordeQueue(rig,NativeHordeModels.Id(kind,s.Look,s.Tier),m.Id,a.Lost,0,0,glow,flash);if(lampOn)IronworksLamp(rig);
            // the chain from the girder through its fists; in the drop on down to the hook over its spot (swaying), the hook
            var fists=(rig.Bones[CapturedRig.LHand].position+rig.Bones[CapturedRig.RHand].position)*.5f;int tier=s.Tier==NativeHordeModels.Full?0:1;
            if(!onFloor&&state!=NativeHordeGait.RiggerState.Fall)IronworksChain(anchor,fists+Vector3.up*.05f,0,tier);
            Vector3 hook;
            if(ph==NativeIronworksKinds.DropPhase.Lowered||ph==NativeIronworksKinds.DropPhase.Slide)
            {
                float low=NativeIronworksKinds.HookLowered(now-v.lockAt);float r=yaw*Mathf.Deg2Rad;var side=new Vector3(Mathf.Cos(r),0,-Mathf.Sin(r));
                hook=Vector3.Lerp(fists,land+Vector3.up*(NativeIronworksKinds.HookHeight+NativeHordeModels.HookDrop),low)+side*NativeIronworksKinds.Sway(now-v.lockAt,seed);IronworksChain(fists,hook,0,tier);
            }
            else if(state==NativeHordeGait.RiggerState.Throw||state==NativeHordeGait.RiggerState.Climb){hook=anchor;}
            else hook=fists+Vector3.down*(onFloor?.3f:1.3f);
            if(state!=NativeHordeGait.RiggerState.Fall&&state!=NativeHordeGait.RiggerState.Stun)sourceModels.Queue(NativeHordeModels.PropId(NativeHordeModels.PropHook,tier),Matrix4x4.TRS(hook,Quaternion.Euler(0,yaw,0),Vector3.one));
            HordeAdd(rig,m.Id,float.NaN,float.NaN,spot.y,spot.y,float.NaN,Vector3.zero,a.Lost,true,kind*8+s.Look);
            ref var f=ref hordeInput[0];f=new HordeMotionInput{Kind=kind,Look=s.Look,Id=m.Id,Seed=seed,Time=now,Yaw=yaw,Ground=spot.y/sc,Root=new NVector(root.x,root.y,root.z)/sc,Health=m.MaxHp>0?m.Hp/m.MaxHp:1,AttackAge=float.NaN};
            IronworksJob(rig,s,NativeIronworksKinds.Rigger,f,spot.y,land,false,!onFloor,anchor,land,hook);
            return true;
        }

        // ================================================================================================================
        // Effects: one job per drawn add, emitted and drawn after every rig is posed (DrawHordeKindFx)
        // ================================================================================================================
        private struct IronJob{public IronFxInput In;public float Distance;public int Id;}
        private IronJob[] ironJobs=new IronJob[32];private int ironJobCount;private readonly IronFxBuffer ironFx=new IronFxBuffer(4096);
        private void IronworksJob(CapturedAnimation.Player rig,HordeKindState s,int z,in HordeMotionInput f,float ground,Vector3 target,bool hasTarget,bool hanging,Vector3 anchor,Vector3 landing,Vector3? hookAt=null)
        {
            if(ironJobCount==ironJobs.Length)Array.Resize(ref ironJobs,ironJobCount*2);var g=s.Gait;
            Vector3 B(int b)=>rig.Bones[b].position;var floor=new Vector3(rig.Root.transform.position.x,ground,rig.Root.transform.position.z);
            var hook=hookAt??(g.PropCount>0||g.ChainCount>0?IronWorld(rig,g.HookTip):B(CapturedRig.RHand));var lip=z==NativeIronworksKinds.Ladleman&&g.PropCount>0?IronWorld(rig,g.Lip):B(CapturedRig.Chest);
            ironJobs[ironJobCount++]=new IronJob{Id=f.Id,Distance=s.Distance,In=new IronFxInput{Kind=z,Seed=f.Seed,Time=f.Time,Yaw=hanging?f.Yaw:g.Yaw,Scale=s.Scale,Speed=g.Speed*s.Scale,Floor=ToN(floor),Chest=ToN(B(CapturedRig.Chest)),Head=ToN(B(CapturedRig.Head)),
                HandL=ToN(B(CapturedRig.LHand)),HandR=ToN(B(CapturedRig.RHand)),Hook=ToN(hook),Lip=ToN(lip),Target=ToN(target),HasTarget=hasTarget,Hanging=hanging,AttackAge=f.AttackAge,Strike=f.Strike,Recover=f.Recover,View=g.Iron,Anchor=ToN(anchor),Landing=ToN(landing)}};
            // The Slagback's slam lands as the zombie impact (debris, cracks, dust) at the authority's disc, its ink the fissure's.
            if(z==NativeIronworksKinds.Slagback&&!float.IsNaN(f.AttackAge)&&f.AttackAge>=f.Strike&&f.AttackAge<f.Strike+.5f)
            {
                float yr=g.Yaw*Mathf.Deg2Rad;var c=floor+new Vector3(Mathf.Sin(yr),0,Mathf.Cos(yr))*NativeHordeKinds.SmashAhead;float born=f.Time-(f.AttackAge-f.Strike);
                HordeImpactAt(f.Id,c,born,f.Seed^0x5a3u,NativeZombieFx.Smash(NativeHordeKinds.Brute),NativeIronworksKinds.Fissure);if(f.AttackAge-f.Strike<.12f)HordeSmashShake(c,born,f.Seed^(uint)f.Id,NativeHordeKinds.SmashRadius);
            }
        }
        // the deaths' and the hits' effects that outlive the add (a cinderling's pop, a crust shed off a corpse, a deflect, a chain snap)
        private struct IronLate{public int Kind,Id;public Vector3 At,Floor,Normal;public float Born;public uint Seed;public bool Pops;}
        private readonly IronLate[] ironLate=new IronLate[24];private int ironLateNext;
        private void IronworksLate(int kind,int id,Vector3 at,Vector3 floor,Vector3 normal,float born,uint seed,bool pops)
        {
            for(int i=0;i<ironLate.Length;i++)if(ironLate[i].Kind==kind&&ironLate[i].Id==id&&Mathf.Abs(ironLate[i].Born-born)<.05f)return;
            ironLate[ironLateNext++%ironLate.Length]=new IronLate{Kind=kind,Id=id,At=at,Floor=floor,Normal=normal,Born=born,Seed=seed,Pops=pops};
        }
        /// <summary>DrawHordeKindFx: the frame's Ironworks effects (budgeted like the minion effects: halved past 24 m, none past 40 m).</summary>
        private void DrawIronworksFx(NativeFxDraw draw,NativeZombieFxDraw zfx,Vector3 eye,Vector3 right,Vector3 up,float quality,float now)
        {
            ironFx.Clear();IronworksForget(now);
            for(int i=0;i<ironJobCount;i++){var j=ironJobs[i];double band=NativeMinionFx.Band(j.Distance,quality);if(band<0)continue;NativeIronworksFx.Emit(ironFx,j.In,band>=.5&&!LowGraphics);}
            for(int i=0;i<ironLate.Length;i++)
            {
                var l=ironLate[i];if(l.Born==0&&l.Id==0)continue;float since=now-l.Born;if(since<0||since>(l.Kind==4?6:4)){continue;}bool full=!LowGraphics&&quality>=.5f;
                switch(l.Kind)
                {
                    case 0:NativeIronworksFx.CinderlingDeath(ironFx,ToN(l.At),ToN(l.Floor),since,l.Pops,l.Seed,full);break;
                    case 1:NativeIronworksFx.DeflectSparks(ironFx,ToN(l.At),ToN(l.Normal),since,l.Seed,full);break;
                    case 2:NativeIronworksFx.ChainSnap(ironFx,ToN(l.At),since,l.Seed,full);break;
                    case 4:NativeIronworksFx.LingeringPool(ironFx,ToN(l.Floor),l.Born,l.Normal.x,now,l.Seed,full);break;
                    case 3:NativeIronworksFx.Spill(ironFx,ToN(l.At)+new NVector(0,.3f,0),ToN(l.Floor),ToN(l.Normal),since,l.Seed,full);break;
                }
            }
            ironJobCount=0;
            var x=ironFx.Items;var decals=zfx?.Decals??draw;
            for(int i=0;i<ironFx.Count;i++)
            {
                ref var e=ref x[i];var col=new Color(e.Color.X,e.Color.Y,e.Color.Z,e.Alpha);
                switch(e.Kind)
                {
                    case IronFxKind.Glow:draw.Sprite(draw.Effects,IronU(e.A),right,up,e.Size,Alpha(col,e.Alpha),NativeFxDraw.Authored+NativeFxDraw.Additive);break;
                    case IronFxKind.Streak:HordeStreak(draw,IronU(e.A),IronU(e.B),e.Size*.5f,Alpha(col,e.Alpha),eye);break;
                    case IronFxKind.Decal:decals.Quad(decals.Effects,IronU(e.A),IronU(e.B),IronU(e.C),IronU(e.D),Alpha(col,e.Alpha),NativeFxDraw.Authored+(e.Additive?NativeFxDraw.Additive:0));break;
                    case IronFxKind.Chunk:vfx?.Rubble(IronU(e.A),IronU(e.Turn),e.Size,(int)e.Seed,new Color(e.Color.X,e.Color.Y,e.Color.Z,0),new Color(e.Emit.X,e.Emit.Y,e.Emit.Z,0));break;
                    case IronFxKind.Smoke:vfx?.Smoke(IronU(e.A),new Color(e.Color.X,e.Color.Y,e.Color.Z,0),e.Alpha,e.Size,.5f,e.Seed,NativeVfxDraw.NoFloor);break;
                }
            }
        }

        // ================================================================================================================
        // Hits and deaths
        // ================================================================================================================
        /// <summary>HordeHit: a shot glancing off the crusted Slagback's front sparks (and tinks: the cue's); a hit cutting the
        /// Stoker's pull in its wind-up stops the shown pull as the rule stops it.</summary>
        private void IronworksHit(NativeMinion m,HordeKindState s,in ArenaEvent e,Vector3 push,bool heavyMelee)
        {
            if(m==null||s==null)return;int z=NativeIronworksKinds.Local(s.Kind);if(z<0)return;var v=s.Gait.Iron;
            if(z==NativeIronworksKinds.Slagback&&NativeIronworksKinds.Crusted(v.shedAt,e.Time)&&NativeIronworksKinds.Frontal(new System.Numerics.Vector2(push.x,push.z),m.Yaw))
            {var at=e.Position.x==0&&e.Position.z==0?m.Body.position+Vector3.up*.4f:NativeArena.Vec(e.Position);IronworksLate(1,m.Id,at,at,-push,e.Time,NativeHordeKinds.Seed(m.Id)^(uint)BitConverter.SingleToInt32Bits(e.Time),false);}
            if(z==NativeIronworksKinds.Stoker&&NativeIronworksKinds.Pull(v.pullAt,e.Time,false,out _)==NativeIronworksKinds.PullPhase.Windup&&NativeIronworksKinds.PullInterrupted(e.Amount,m.MaxHp,heavyMelee)){v.pullAt=float.NaN;s.Gait.Iron=v;}
        }
        /// <summary>HordeKindDeath: the cinderling's fuse and pop on a melee kill (a crumble of ash otherwise); a crusted slagback keeps
        /// its crust as it chars; what it carried falls and lies by the body (the rake, the ladle spilling, the hook clattering away)
        /// and the Rigger's lamp flickers out, all gone with the body.</summary>
        private void IronworksDeath(NativeDeath death,int kind,bool melee,CapturedAnimation.Player rig,float fx,float floorY)
        {
            int z=NativeIronworksKinds.Local(kind);if(z<0)return;uint seed=NativeHordeKinds.Seed(death.id)^(uint)death.seed;var floor=new Vector3(death.position.x,floorY,death.position.z);
            if(z==NativeIronworksKinds.Ladleman&&death.zone!=null&&death.zone.pourAt>=0&&death.at>=death.zone.pourAt+1)IronworksLate(4,death.id,death.position,floor,new Vector3(death.zone.pourYaw,0,0),death.zone.pourAt,seed,false);
            if(z==NativeIronworksKinds.Cinderling)IronworksLate(0,death.id,rig.Bones[CapturedRig.Chest].position,floor,Vector3.up,death.at,seed,melee);
            if(!ironDropped.TryGetValue(death.id,out var drop))
            {
                drop=new IronDrop{Born=death.at,Prop=-1};
                if(ironCarry.TryGetValue(death.id,out var c)&&death.at-c.Seen<1)
                {
                    drop.Crusted=c.Crusted;drop.Look=c.Look;
                    if(c.Has)
                    {
                        drop.Prop=c.Prop==NativeHordeModels.PropLadle?NativeHordeModels.PropLadleEmpty:c.Prop;drop.From=c.M.GetColumn(3);drop.FromRot=c.M.rotation;
                        var along=c.M.MultiplyVector(Vector3.forward);along.y=0;if(along.sqrMagnitude<1e-4f)along=Vector3.forward;along.Normalize();
                        drop.To=new Vector3(drop.From.x,floorY+(c.Prop==NativeHordeModels.PropRake?.03f:c.Prop==NativeHordeModels.PropHook?.06f:.2f),drop.From.z)+along*(c.Prop==NativeHordeModels.PropHook?.6f:.15f);
                        drop.ToRot=Quaternion.LookRotation(along,Vector3.up)*(c.Prop==NativeHordeModels.PropRake?Quaternion.identity:Quaternion.Euler(0,0,c.Prop==NativeHordeModels.PropHook?90:70));
                        if(c.Prop==NativeHordeModels.PropLadle)IronworksLate(3,death.id,drop.To,floor,along,death.at+.35f,seed,false);
                    }
                    ironCarry.Remove(death.id);
                }
                ironDropped[death.id]=drop;
            }
            // The corpse keeps the crust and what it dropped (NativeHordeGorePresentation.cs HordeCorpsesDraw), once it has settled.
            var remains=hordeGore.RemainsOf(death.id);if(remains!=null&&Mathf.Abs(remains.At-death.at)<.05f){remains.Extra=drop.Crusted?drop.Look:-1;remains.Prop=drop.Prop;if(drop.Prop>=0)remains.PropMatrix=GoreN(Matrix4x4.TRS(drop.To,drop.ToRot,Vector3.one));}
            bool body=!NativeMinionFx.Gone(fx)&&(remains==null||!remains.Posed||remains.Frame>=hordeKindFrame);if(!body)return;
            if(drop.Crusted){HordeDeathBody(rig,IronworksCrustId(drop.Look,NativeHordeModels.Full),death.id,death.lost,fx,hordeQueueVisible);}
            if(drop.Prop>=0){float u=Mathf.Clamp01(fx/.45f);float bounce=u<1?0:Mathf.Max(0,.06f*Mathf.Sin((fx-.45f)*18)*Mathf.Exp(-(fx-.45f)*6));
                sourceModels.Queue(NativeHordeModels.PropId(drop.Prop,0),Matrix4x4.TRS(Vector3.Lerp(drop.From,drop.To,u*u)+Vector3.up*bounce,Quaternion.Slerp(drop.FromRot,drop.ToRot,Mathf.SmoothStep(0,1,u)),Vector3.one));}
            if(z==NativeIronworksKinds.Rigger&&fx<.5f&&((int)(fx*23)&1)==0)IronworksLamp(rig);
        }
        /// <summary>HordeKindDeath: a cinderling killed by a melee blow is gone once its fuse pops (its embers fly instead).</summary>
        private static bool IronworksPopped(int kind,bool melee,float fx)=>melee&&NativeIronworksKinds.Local(kind)==NativeIronworksKinds.Cinderling&&fx>=NativeIronworksKinds.PopFuse;
        private struct IronCarry{public bool Has,Crusted;public int Prop,Look;public Matrix4x4 M;public float Seen;}
        private struct IronDrop{public int Prop,Look;public bool Crusted;public Vector3 From,To;public Quaternion FromRot,ToRot;public float Born;}
        private readonly Dictionary<int,IronCarry> ironCarry=new Dictionary<int,IronCarry>();private readonly Dictionary<int,IronDrop> ironDropped=new Dictionary<int,IronDrop>();private readonly List<int> ironStale=new List<int>();
        /// <summary>Forgets what the long dead and the long unseen carried (each frame, no allocation once the maps have grown).</summary>
        private void IronworksForget(float now)
        {
            ironStale.Clear();foreach(var p in ironCarry)if(now-p.Value.Seen>2)ironStale.Add(p.Key);foreach(int id in ironStale)ironCarry.Remove(id);
            ironStale.Clear();foreach(var p in ironDropped)if(now-p.Value.Born>NativeMinionFx.DeathFxSeconds+1||now<p.Value.Born-1)ironStale.Add(p.Key);foreach(int id in ironStale)ironDropped.Remove(id);
        }

        // ================================================================================================================
        // The Dev Sandbox gallery: the five in a row behind the Siren kinds' (NativeIronworksGallery's script; no authority add)
        // ================================================================================================================
        private readonly CapturedAnimation.Player[] ironGalleryRigs=new CapturedAnimation.Player[NativeIronworksKinds.KindCount];private readonly HordeKindState[] ironGalleryStates=new HordeKindState[NativeIronworksKinds.KindCount];private float ironGalleryStart=float.NaN;
        private void IronworksGallery(NativeArena arena)
        {
            var me=arena.Local;if(me?.Body==null)return;float now=hordeClock;if(float.IsNaN(ironGalleryStart)||ironGalleryRigs[0]==null)ironGalleryStart=now;
            float baseYaw=me.Yaw,yr=baseYaw*Mathf.Deg2Rad;var fwd=new Vector3(Mathf.Sin(yr),0,Mathf.Cos(yr));var across=new Vector3(Mathf.Cos(yr),0,-Mathf.Sin(yr));var row=me.Body.position+fwd*10.5f;float ground=me.Body.position.y-.85f;float yawToMe=baseYaw+180;
            for(int z=0;z<NativeIronworksKinds.KindCount;z++)
            {
                int kind=NativeIronworksKinds.Kind(z),id=991000+z;if(ironGalleryRigs[z]==null){ironGalleryRigs[z]=captured.Create(transform,id);ironGalleryStates[z]=new HordeKindState();ironGalleryStates[z].Reset(id);ironGalleryStates[z].Kind=kind;ironGalleryStates[z].Look=0;ironGalleryStates[z].React.Bind(kind);ironGalleryStates[z].Tier=NativeHordeModels.Full;}
                var rig=ironGalleryRigs[z];var s=ironGalleryStates[z];float sc=NativeHordeKinds.Scale[kind];s.Scale=sc;uint seed=NativeHordeKinds.Seed(id);
                float loop=NativeIronworksGallery.Loop,start=ironGalleryStart+Mathf.Floor((now-ironGalleryStart+z*.41f)/loop)*loop-z*.41f,g=now-start;
                var slot=new Vector3(row.x,ground,row.z)+across*((z-2)*2.6f);var fr=NativeIronworksGallery.At(z,g,start,5,yawToMe,slot.x,slot.z);var st=NativeHordeKinds.ProposedRow(kind);s.Gait.Iron=fr.View;
                s.Gait.IronTarget=new NVector(me.Body.position.x,me.Body.position.y,me.Body.position.z)/sc;s.Gait.IronTargetKnown=true;s.Distance=Vector3.Distance(slot,hordeEye);
                if(fr.State==NativeIronworksGallery.State.Death)
                {
                    NativeHordeGait.Death(minionPose,kind,seed,Mathf.Min(fr.DeathAge,2.3f),new System.Numerics.Vector2(0,-1));NativePoseWriter.Write(rig.Bones,minionPose,"a gallery rig");rig.Root.transform.SetPositionAndRotation(slot+Vector3.up*NativeHordeKinds.DrawRoot(kind,false),Quaternion.Euler(0,yawToMe,0));rig.Root.transform.localScale=Vector3.one*sc;
                    bool body=!NativeMinionFx.Gone(fr.DeathAge);if(body)HordeDeathBody(rig,NativeHordeModels.Id(kind,0,NativeHordeModels.Full),id,0,fr.DeathAge,null,z==NativeIronworksKinds.Cinderling);HordeAdd(rig,id,float.NaN,fr.DeathAge,ground,ground,float.NaN,Vector3.zero,0,body,kind*8);
                    if(z==NativeIronworksKinds.Cinderling)IronworksLate(0,id,rig.Bones[CapturedRig.Chest].position,slot,Vector3.up,start+NativeIronworksGallery.DeathAt,seed,true);s.Gait.Reset();continue;
                }
                if(fr.State!=NativeIronworksGallery.State.Ground)
                {
                    var stt=fr.State==NativeIronworksGallery.State.Slide?NativeHordeGait.RiggerState.Slide:fr.State==NativeIronworksGallery.State.Throw?NativeHordeGait.RiggerState.Throw:fr.State==NativeIronworksGallery.State.Climb?NativeHordeGait.RiggerState.Climb:NativeHordeGait.RiggerState.Hang;
                    NativeHordeGait.RiggerHang(minionPose,seed,stt,stt==NativeHordeGait.RiggerState.Slide?fr.SlideU:g,now,.12f*Mathf.Sin(now*Mathf.PI*2/NativeIronworksKinds.SwayPeriod));
                    var at=slot+Vector3.up*(fr.Height+NativeHordeKinds.DrawRoot(kind,false));NativePoseWriter.Write(rig.Bones,minionPose,"a gallery rig");rig.Root.transform.SetPositionAndRotation(at,Quaternion.Euler(0,yawToMe,0));rig.Root.transform.localScale=Vector3.one*sc;
                    HordeQueue(rig,NativeHordeModels.Id(kind,0,NativeHordeModels.Full),id,0,0,0,HordeGlow(id),1);if(NativeIronworksKinds.Lamp(fr.View.lockAt,now,true))IronworksLamp(rig);
                    var anchor=slot+Vector3.up*(5+NativeIronworksKinds.GirderAbove);var fists=(rig.Bones[CapturedRig.LHand].position+rig.Bones[CapturedRig.RHand].position)*.5f;IronworksChain(anchor,fists,0,0);
                    var ph=NativeIronworksKinds.Drop(fr.View.lockAt,now,out _);Vector3 hook=fists+Vector3.down*1.3f;
                    if(ph==NativeIronworksKinds.DropPhase.Lowered||ph==NativeIronworksKinds.DropPhase.Slide){hook=Vector3.Lerp(fists,slot+Vector3.up*(NativeIronworksKinds.HookHeight+NativeHordeModels.HookDrop),NativeIronworksKinds.HookLowered(now-fr.View.lockAt))+across*NativeIronworksKinds.Sway(now-fr.View.lockAt,seed);IronworksChain(fists,hook,0,0);}
                    sourceModels.Queue(NativeHordeModels.PropId(NativeHordeModels.PropHook,0),Matrix4x4.TRS(hook,Quaternion.Euler(0,yawToMe,0),Vector3.one));HordeAdd(rig,id,float.NaN,float.NaN,ground,ground,float.NaN,Vector3.zero,0,true,kind*8);
                    ref var fh=ref hordeInput[0];fh=new HordeMotionInput{Kind=kind,Id=id,Seed=seed,Time=now,Yaw=yawToMe,Ground=ground/sc,Root=new NVector(at.x,at.y,at.z)/sc,Health=1,AttackAge=float.NaN};
                    IronworksJob(rig,s,z,fh,ground,me.Body.position,true,true,anchor,slot,hook);s.Gait.Reset();continue;
                }
                var turn=Quaternion.Euler(0,yawToMe,0);var pos=slot+turn*IronU(fr.Offset);var vel=turn*IronU(fr.Velocity);float yaw=vel.sqrMagnitude>.01f?Mathf.Atan2(vel.x,vel.z)*Mathf.Rad2Deg:yawToMe;
                float strike=NativeHordeKinds.Strike(st,false,0),recover=NativeHordeKinds.Recover(st,false,0);var attack=NativeHordeKinds.AttackOf(kind,st.attack,false);float rootY=ground+NativeHordeKinds.DrawRoot(kind,false);
                ref var f=ref hordeInput[0];
                f=new HordeMotionInput{Kind=kind,Look=0,Id=id,Seed=seed,Time=now,Yaw=yaw,Ground=ground/sc,Root=new NVector(pos.x,rootY,pos.z)/sc,Velocity=new NVector(vel.x,vel.y,vel.z)/sc,AttackAge=fr.AttackAge,Strike=strike,Recover=recover,Attack=attack,Side=1,Health=1,HasTarget=!fr.Entering,Target=new NVector(me.Body.position.x,me.Body.position.y,me.Body.position.z)/sc,Entering=fr.Entering,EntryAge=g,EntryWalk=NativeIronworksGallery.Entrance};
                s.Gait.Pose(f,minionPose,s.React);NativePoseWriter.Write(rig.Bones,minionPose,"a gallery rig");rig.Root.transform.SetPositionAndRotation(new Vector3(pos.x,rootY,pos.z),Quaternion.Euler(0,s.Gait.Yaw,0));rig.Root.transform.localScale=Vector3.one*sc;
                float gk=NativeHordeFx.Brighten(fr.AttackAge,strike,recover);var glow=HordeGlow(id);var gl=new Vector4(gk,gk,gk,glow.w);
                HordeQueue(rig,NativeHordeModels.Id(kind,0,NativeHordeModels.Full),id,0,0,0,gl,1);HordeAdd(rig,id,float.NaN,float.NaN,ground,ground,float.NaN,vel,0,true,kind*8);
                s.Gait.PlaceProps(f,minionPose);IronworksProps(rig,s.Gait,NativeHordeModels.Full,gl,1,sc);
                if(z==NativeIronworksKinds.Slagback&&NativeIronworksKinds.Crusted(fr.View.shedAt,now))IronworksCrust(rig,0,NativeHordeModels.Full,0,gl,1);
                if(z==NativeIronworksKinds.Rigger)IronworksLamp(rig);
                IronworksJob(rig,s,z,f,ground,me.Body.position,true,false,Vector3.zero,slot);
            }
        }
        private void ClearIronworksGallery(){for(int z=0;z<ironGalleryRigs.Length;z++){ironGalleryRigs[z]?.Dispose();ironGalleryRigs[z]=null;ironGalleryStates[z]=null;}ironGalleryStart=float.NaN;}
        /// <summary>A new arena, generation or rewind: the jobs and the effects that outlive their adds.</summary>
        private void ClearIronworks(){ironJobCount=0;for(int i=0;i<ironLate.Length;i++)ironLate[i]=default;ironCarry.Clear();ironDropped.Clear();}
    }

    public sealed partial class NativeSourceModels
    {
        /// <summary>The Ironworks props (two tiers), the Slagback's crust (its looks and tiers) and the cream brute (the Ironworks'
        /// seams: every brute look and tier) in the bank, beside the horde kinds' models (HordeKindModels).</summary>
        private void IronworksModels(Shader shader)
        {
            var material=new Material(shader){name="Native Ironworks props",enableInstancing=true};material.SetInt("_Cull",2);material.SetFloat("_Ink",1);material.SetFloat(InkRimId,0);material.SetFloat(InkFlowId,0);material.SetFloat(MinionGlowId,1);materials.Add(material);
            for(int p=0;p<NativeHordeModels.PropCount;p++)for(int t=0;t<NativeHordeModels.PropTiers;t++)Register(NativeHordeModels.PropId(p,t),NativeHordeModels.IronworksProp(p,t),material,t!=0);
            var crust=materials.Find(m=>m.name=="Native Siren minion / slagback")??material;
            for(int l=0;l<2;l++)for(int t=0;t<NativeHordeModels.Tiers;t++)Register("iw-slagback-crust-"+l+"-"+NativeHordeModels.TierNames[t],NativeHordeModels.IronworksCrust(l,t),crust,t!=NativeHordeModels.Full);
            var brute=materials.Find(m=>m.name=="Native Siren minion / brute")??material;
            for(int l=0;l<NativeHordeKinds.Looks[NativeHordeKinds.Brute];l++)for(int t=0;t<NativeHordeModels.Tiers;t++)Register(NativeHordeModels.CreamId(l,t),NativeHordeModels.Build(NativeHordeKinds.Brute,l,t,NativeIronworksKinds.BruteSeam).Mesh,brute,t!=NativeHordeModels.Full);
        }
        private void Register(string id,WeaponMesh mesh,Material material,bool noShadow)
        {
            var parts=new List<Part>();foreach(var s in mesh.Sections){var m=BodyMesh(s,id+" / joint "+s.Bone);if(m!=null)parts.Add(new Part{Name=id,Bone=s.Bone,Mesh=m,Material=material,NoShadow=noShadow});}models[id]=parts.ToArray();
        }
    }

    public sealed partial class NativeAudio
    {
        // ---------- the Ironworks enemies' cues (NativeIronworksKinds.CueKeys: docs/EXPEDITION-CITY-IRONWORKS.md section 8) ----------
        // Bank only: each plays when the owner's LocalAssets bank has its clips (silent otherwise; the zombie voices and the attack
        // layers keep playing for the kind as drawn). A zone cue that plays takes the attack layer's slot, so the two never double.
        /// <summary>HordeCues, per living Ironworks add before its attack layers: the wind-up and strike in the zone's keys (when the
        /// bank has them), the specials from the view, the hanging Rigger's rattle and lamp.</summary>
        private void IronworksCues(NativeArena arena,NativeMinion m,int kind,float strike,Vector3 pos,float t)
        {
            int z=NativeIronworksKinds.Local(kind);if(z<0)return;var a=m.Pve;var v=ArenaPresentation.IronworksViewOf(m);string K(string cue)=>NativeIronworksKinds.Cue(z,cue);
            bool Cue(string cue,float at,string slot,float gain=.8f){if(t<at||t-at>=.12f)return false;var key=K(cue);if(key==null||!bank.Has(key))return false;if(!Once(slot,at))return true;BankPlay(key,pos,gain);return true;}
            // the attack: its wind-up and strike in the zone's keys take the attack layer's slots (HordeCues' "w" and "s")
            string wind=z==0?"flare":z==1?null:z==2?"slam.windup":z==3?null:"swing.windup",hit=z==0?"screech":z==1?"rake":z==2?"slam":z==3?"swing":"swing";
            if(wind!=null)Cue(wind,m.LastAttack,"add"+m.Id+"w",.75f);Cue(hit,m.LastAttack+strike,"add"+m.Id+"s",.8f);
            switch(z)
            {
                case NativeIronworksKinds.Stoker:Cue("hook.windup",v.pullAt,"iw"+m.Id+"pw");Cue("hook.throw",v.pullAt+NativeIronworksKinds.PullWindup,"iw"+m.Id+"pt");if(v.pullTarget>=0)Cue("hook.yank",v.pullAt+NativeIronworksKinds.PullWindup+NativeIronworksKinds.HookFlight,"iw"+m.Id+"py");break;
                case NativeIronworksKinds.Slagback:Cue("shed",v.shedAt,"iw"+m.Id+"sh",.9f);if(a.Entry!=null&&a.Entry.pit)Cue("emerge",a.SpawnAt,"iw"+m.Id+"em");break;
                case NativeIronworksKinds.Ladleman:Cue("pour.windup",v.pourAt,"iw"+m.Id+"lw");Cue("pour",v.pourAt+NativeIronworksKinds.PourWindup,"iw"+m.Id+"lp",.9f);Cue("spill",v.spilledAt,"iw"+m.Id+"ls",.95f);break;
                case NativeIronworksKinds.Rigger:
                {
                    float lockAt=v.lockAt;if(t<a.ActiveAt&&a.Entry!=null&&(a.Entry.surface=="ceiling"||NativeHordeDrop.Is(a.Entry)))lockAt=NativeIronworksKinds.EntryLock(a.ActiveAt);
                    // the rattle is the warning: loud and high (the hang point's), above the furnace bed
                    Cue("rattle",lockAt-NativeIronworksKinds.DropRattle,"iw"+m.Id+"rr",1);Cue("lamp",lockAt,"iw"+m.Id+"rl",.6f);Cue("lower",lockAt,"iw"+m.Id+"rw",.9f);
                    Cue("slide",lockAt+NativeIronworksKinds.DropSlide,"iw"+m.Id+"rs",.85f);Cue("shriek",lockAt+NativeIronworksKinds.DropSlide,"iw"+m.Id+"rk",.8f);Cue("land",lockAt+NativeIronworksKinds.DropLand,"iw"+m.Id+"rd",.9f);
                    Cue("throw",v.climbAt,"iw"+m.Id+"rt");Cue("snap",v.chainCutAt,"iw"+m.Id+"rc",.9f);break;
                }
            }
        }
        /// <summary>A cinderling's pop on a melee kill (its death, after the fuse).</summary>
        private void IronworksDeathCues(NativeArena arena,float t)
        {
            var deaths=arena.MinionDeaths;for(int i=0;i<deaths.Count;i++){var d=deaths[i];if(d==null)continue;int kind=zombieVoices.KindOf(d.id);if(NativeIronworksKinds.Local(kind)!=NativeIronworksKinds.Cinderling)continue;float at=d.at+NativeIronworksKinds.PopFuse;
                if(t>=at&&t-at<.12f){var key=NativeIronworksKinds.Cue(NativeIronworksKinds.Cinderling,"pop");if(bank.Has(key)&&Once("iw"+d.id+"pop",at))BankPlay(key,d.position,.85f);}}
        }
    }
}
