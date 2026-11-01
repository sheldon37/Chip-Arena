using System;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NVector2=System.Numerics.Vector2;
using C=ChipArena.UnityConversion.NativeCyberTheme;

namespace ChipArena.UnityConversion
{
    // DEV preview (DEV panel · Previews · Zombie gore, "preview-zombie-gore"): five zombies in a row before the local pilot, each
    // looping a scripted string of hits through the same gore code the live horde uses (NativeHordeGorePresentation.cs), with the
    // local pilot as the shooter: presentation only, nothing is sent and no authority add exists.
    //  1 shambler: body shots (chest, gut, arm, leg), a pistol head shot, then the killing shot: a corpse over its pool.
    //  2 runner: one rifle head shot: the head bursts, the neck fountains, the body falls.
    //  3 brute: a body shot, then a blast at its feet: a gib (its limbs thrown, the trunk in chunks, a wide pool).
    //  4 shambler (hooded look): two sword gashes, then a greatsword kill that slices it along the swing.
    //  5 spitter: a machine-gun burst across its chest and arms, then a corpse.
    // The loop restarts every GoreLoop s; each lane's wounds, corpse, chunks and pool go at the restart.
    public sealed partial class ArenaPresentation
    {
        /// <summary>The DEV preview's switch (presentation only).</summary>
        public bool ZombieGoreGallery;
        public const int GoreLanes=5;public const float GoreLoop=16;
        private static readonly int[] GoreLaneKind={NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Brute,NativeHordeKinds.Shambler,NativeHordeKinds.Spitter},GoreLaneLook={0,0,0,1,0};
        // A scripted hit: when, its cause, where it is aimed (0 chest, 1 head, 2 arm, 3 leg, 4 gut), its damage and whether it kills.
        private struct GoreBeat{public float At,Amount;public NativeHordeGore.Cause Cause;public int Aim;public bool Kill,Heavy;public GoreBeat(float at,NativeHordeGore.Cause c,int aim,float amount,bool kill=false,bool heavy=false){At=at;Cause=c;Aim=aim;Amount=amount;Kill=kill;Heavy=heavy;}}
        private static readonly GoreBeat[][] GoreScript=MakeGoreScript();
        private static GoreBeat[][] MakeGoreScript()
        {
            const NativeHordeGore.Cause B=NativeHordeGore.Cause.Bullet,M=NativeHordeGore.Cause.Melee;
            var mg=new GoreBeat[12];for(int i=0;i<11;i++)mg[i]=new GoreBeat(1+i*.12f,B,i%3==0?2:i%3==1?0:4,8);mg[11]=new GoreBeat(2.6f,B,0,8,true);
            return new[]{
                new[]{new GoreBeat(1,B,0,14),new GoreBeat(1.5f,B,4,14),new GoreBeat(2,B,2,14),new GoreBeat(2.6f,B,3,14),new GoreBeat(3.4f,B,1,23),new GoreBeat(5,B,0,20,true)},
                new[]{new GoreBeat(2.2f,NativeHordeGore.Cause.Heavy,1,38,true)},
                new[]{new GoreBeat(1.2f,B,0,23),new GoreBeat(3,NativeHordeGore.Cause.Blast,0,217,true)},
                new[]{new GoreBeat(1,M,0,30),new GoreBeat(1.8f,M,4,30),new GoreBeat(3,M,0,52,true,true)},
                mg};
        }
        private readonly CapturedAnimation.Player[] goreGalleryRigs=new CapturedAnimation.Player[GoreLanes];private readonly HordeKindState[] goreGalleryStates=new HordeKindState[GoreLanes];
        private readonly int[] goreGallerySlice=new int[GoreLanes],goreGalleryFountain=new int[GoreLanes];private readonly NativeHordeGore.Fate[] goreGalleryFate=new NativeHordeGore.Fate[GoreLanes];
        private float goreGalleryStart=float.NaN,goreGalleryLast;private int goreGalleryRound=-1;private Vector3 goreGalleryAt;private float goreGalleryYaw;
        private readonly SlicePlan goreGalleryPlan=new SlicePlan();
        private static int GoreLaneId(int lane)=>995000+lane;
        /// <summary>The preview's lane `lane`'s kill time in its loop (s), or +inf.</summary>
        private static float GoreKillAt(int lane){foreach(var b in GoreScript[lane])if(b.Kill)return b.At;return float.PositiveInfinity;}

        private void GoreGallery(NativeArena arena)
        {
            var me=arena.Local;if(me?.Body==null||!HordeKindsReady)return;float now=hordeClock;
            if(float.IsNaN(goreGalleryStart)||goreGalleryRigs[0]==null){goreGalleryStart=now;goreGalleryYaw=me.Yaw;float r=goreGalleryYaw*Mathf.Deg2Rad;goreGalleryAt=me.Body.position+new Vector3(Mathf.Sin(r),0,Mathf.Cos(r))*5.5f;goreGalleryAt.y=me.Body.position.y;goreGalleryRound=-1;goreGalleryLast=0;}
            int round=(int)Mathf.Floor((now-goreGalleryStart)/GoreLoop);float t=now-goreGalleryStart-round*GoreLoop,loopAt=goreGalleryStart+round*GoreLoop;
            if(round!=goreGalleryRound){goreGalleryRound=round;goreGalleryLast=0;for(int k=0;k<GoreLanes;k++)GoreLaneReset(k);}
            float yawToMe=goreGalleryYaw+180,yr=goreGalleryYaw*Mathf.Deg2Rad;var across=new Vector3(Mathf.Cos(yr),0,-Mathf.Sin(yr));float ground=goreGalleryAt.y-.85f;var shooter=me.Body.position+Vector3.up*.55f;
            for(int k=0;k<GoreLanes;k++)
            {
                int id=GoreLaneId(k),kind=GoreLaneKind[k],look=GoreLaneLook[k];
                if(goreGalleryRigs[k]==null){goreGalleryRigs[k]=captured.Create(transform,id);goreGalleryStates[k]=new HordeKindState();goreGalleryStates[k].Reset(id);goreGalleryStates[k].Kind=kind;goreGalleryStates[k].Look=look;goreGalleryStates[k].React.Bind(kind);goreGalleryStates[k].Tier=NativeHordeModels.Full;}
                var rig=goreGalleryRigs[k];var s=goreGalleryStates[k];var st=NativeHordeKinds.ProposedRow(kind);float sc=NativeHordeKinds.Scale[kind];uint seed=NativeHordeKinds.Seed(id);
                var slot=goreGalleryAt+across*((k-(GoreLanes-1)*.5f)*2.2f);var pos=new Vector3(slot.x,ground+NativeHordeKinds.DrawRoot(kind,false),slot.z);float killAt=GoreKillAt(k);string model=NativeHordeModels.Id(kind,look,NativeHordeModels.Full);
                // The beats crossed since the last frame: each a hit through the live code (its felt hit and its gore).
                foreach(var b in GoreScript[k])if(b.At>goreGalleryLast&&b.At<=t)GoreGalleryHit(k,b,loopAt+b.At,rig,shooter,pos,ground,model);
                if(t<killAt)
                {
                    ref var f=ref hordeInput[0];
                    f=new HordeMotionInput{Kind=kind,Look=look,Id=id,Seed=seed,Time=now,Yaw=yawToMe,Ground=ground/sc,Root=ToN(pos)/sc,Velocity=default,AttackAge=float.NaN,Strike=NativeHordeKinds.Strike(st,false,0),Recover=NativeHordeKinds.Recover(st,false,0),Attack=NativeHordeKinds.AttackOf(kind,st.attack,false),Side=1,Health=1,HasTarget=true,Target=ToN(me.Body.position)/sc};
                    s.Gait.Pose(f,minionPose,s.React);NativePoseWriter.Write(rig.Bones,minionPose,"a gore preview rig");rig.Root.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,s.Gait.Yaw,0));rig.Root.transform.localScale=Vector3.one*sc;
                    float flash=1;foreach(var b in GoreScript[k])if(t>=b.At&&t-b.At<.12f)flash=NativeHitFlash.Code(t-b.At,b.Aim==1,EffectsQuality);
                    HordeQueue(rig,model,id,0,0,0,HordeGlow(id),flash);HordeQueueJaw(rig,kind,look,NativeHordeModels.Full,.2f,HordeGlow(id),flash);GoreWoundsJob(id,rig);continue;
                }
                // Dead: the fall on its kind's take, then the corpse (or nothing of the body for a gib).
                float fall=t-killAt;var gib=goreGalleryFate[k]==NativeHordeGore.Fate.Gib||goreGalleryFate[k]==NativeHordeGore.Fate.Crush;
                NativeHordeGait.Death(minionPose,kind,seed,Mathf.Min(fall,(float)NativeMinionFx.DeathSeconds),new NVector2(0,-1));NativePoseWriter.Write(rig.Bones,minionPose,"a gore preview rig");
                rig.Root.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,yawToMe,0));rig.Root.transform.localScale=Vector3.one*sc;
                if(!gib)
                {
                    hordeQueueLost=0;hordeQueueHidden=goreGallerySlice[k];hordeQueueVisible??=b=>HordeJointShown(hordeQueueLost,b)&&(hordeQueueHidden==0||!HordeHiddenJoint(hordeQueueHidden,b));
                    if(HordeGoreSettle(id,loopAt+killAt,fall,rig,0,goreGallerySlice[k],ground)){HordeDeathBody(rig,model,id,0,fall,hordeQueueVisible);HordeDeathJaw(rig,kind,look,id,hordeQueueVisible);GoreWoundsJob(id,rig);}
                }
                if(goreGalleryFate[k]==NativeHordeGore.Fate.HeadGib){int fo=goreGalleryFountain[k];HordeGoreFountain(id,loopAt+killAt,now,rig,ref fo);goreGalleryFountain[k]=fo;}
            }
            goreGalleryLast=t;
        }
        /// <summary>One scripted hit on lane `k` at `at` (the presentation clock) from the local pilot: the felt hit, the gore and, for the
        /// killing beat, the death's fate (a blade's kill sliced along a planned swing).</summary>
        private void GoreGalleryHit(int k,in GoreBeat b,float at,CapturedAnimation.Player rig,Vector3 shooter,Vector3 body,float ground,string model)
        {
            var s=goreGalleryStates[k];int id=GoreLaneId(k),kind=GoreLaneKind[k];var st=NativeHordeKinds.ProposedRow(kind);float maxHp=st.hp;
            int aimBone=b.Aim==1?CapturedRig.Head:b.Aim==2?CapturedRig.LArm:b.Aim==3?CapturedRig.LKnee:b.Aim==4?CapturedRig.Spine:CapturedRig.Chest;
            var target=rig.Bones[aimBone].position+(b.Aim==2?Vector3.down*.08f:Vector3.zero);
            bool blast=b.Cause==NativeHordeGore.Cause.Blast;var point=blast?new Vector3(body.x,ground+.15f,body.z)+(shooter-body).normalized*.9f:target+(shooter-target).normalized*.25f;
            var dir=blast?(body-point):(target-shooter);dir.y=blast?0:dir.y*.3f;dir.Normalize();
            bool melee=b.Cause==NativeHordeGore.Cause.Melee;var w=melee?(b.Heavy?StaggerWeight.Heavy:StaggerWeight.Step):StaggerWeight.Flinch;
            if(!b.Kill)s.React.Add(at,new NVector2(dir.x,dir.z),w,b.Amount,melee,b.Aim==1,b.Amount>=maxHp*.25f,st.stagger);
            var sweep=melee?(Vector3.Cross(Vector3.up,dir).normalized+Vector3.up*(b.Heavy?-.4f:.35f)).normalized:Vector3.zero;
            var h=new GoreHit{Id=id,Kind=kind,Look=s.Look,Index=0,Time=at,Amount=b.Amount,MaxHp=maxHp,Cause=b.Cause,Kill=b.Kill,Point=GoreV(point),Dir=GoreV(dir),Sweep=GoreV(sweep),Shot=GoreV(blast||melee?Vector3.zero:(target-shooter).normalized)};
            hordeGore.Hit(h,GoreBones(rig),ground,out bool head);
            if(!b.Kill)return;
            // The kill: a blade's planned slice (its joints thrown as the model's own parts), else the gore's fate.
            int sliced=0;
            if(melee&&NativeMeleeSlice.Plan(default,false,"greatsword",null,NativeArena.Vec(body),goreGalleryYaw+180,id,goreGalleryPlan))
            {
                var tr=goreGalleryPlan.Travel;var along=new NVector2(tr.X,tr.Z);if(along.LengthSquared()<1e-4f)along=new NVector2(dir.x,dir.z);
                for(int i=0;i<goreGalleryPlan.Count;i++){int j=goreGalleryPlan.Joints[i];sliced|=1<<j;HordeLaunch(rig,model,kind,id,j,at,along,goreGalleryPlan.Kick*.35f,Mathf.Max(.05f,rig.Bones[j].position.y-ground));}
            }
            int slice=sliced;goreGalleryFate[k]=HordeGoreDeath(id,kind,GoreLaneLook[k],0,at,id+goreGalleryRound*7,b.Cause,b.Aim==1&&head,b.Amount,maxHp,blast?.9f:99,b.Heavy,dir,rig,ground,ref slice,model,sliced);goreGallerySlice[k]=slice;
        }
        private void GoreLaneReset(int k)
        {
            int id=GoreLaneId(k);hordeGore.Forget(id);hordeGore.Drop(id);goreGallerySlice[k]=0;goreGalleryFountain[k]=0;goreGalleryFate[k]=NativeHordeGore.Fate.Corpse;
            for(int i=0;i<hordeParts.Length;i++)if(hordeParts[i].Id==id)hordeParts[i].Live=false;
            if(goreGalleryStates[k]!=null){var s=goreGalleryStates[k];int kind=s.Kind,look=s.Look;s.Reset(id);s.Kind=kind;s.Look=look;s.React.Bind(kind);s.Tier=NativeHordeModels.Full;}
        }
        private void ClearGoreGallery()
        {
            for(int k=0;k<GoreLanes;k++){if(goreGalleryRigs[k]!=null){hordeGore.Forget(GoreLaneId(k));hordeGore.Drop(GoreLaneId(k));}goreGalleryRigs[k]?.Dispose();goreGalleryRigs[k]=null;goreGalleryStates[k]=null;}
            goreGalleryStart=float.NaN;goreGalleryRound=-1;
        }
    }

    public sealed partial class ConversionGame
    {
        private void ZombieGorePreviewToggle()
        {
            if(presentation==null)return;presentation.ZombieGoreGallery=!presentation.ZombieGoreGallery;
            DevSay(presentation.ZombieGoreGallery?"Zombie gore preview · on (presentation only: five zombies before you loop body shots, a head shot, a big gun's head shot, a blast gib, a blade's slice and a machine-gun burst)":"Zombie gore preview · off",false);
        }
        /// <summary>DEV panel · Previews: the zombie gore preview's switch and what each lane shows.</summary>
        private float ZombieGorePreviewPanel(float x,float y,float w)
        {
            y=DevHead(x,y,w,"ZOMBIE GORE (DEV PREVIEW)");DevToggleAct(new Rect(x,y,w,32),"preview-zombie-gore",presentation!=null&&presentation.ZombieGoreGallery);y+=38;
            return DevNote(x,y,w,"Five zombies 5.5 m ahead, left to right, every 16 s: body shots, a head shot and a kill (corpse and pool); a rifle head shot (the head bursts); a blast (a gib); two gashes and a greatsword slice; a machine-gun burst. Presentation only: no add, hit or damage exists. Reduced effects and the low graphics tier show the tiers.",C.Muted());
        }
    }
}
