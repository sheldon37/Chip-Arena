using System;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NVector2=System.Numerics.Vector2;

namespace ChipArena.UnityConversion
{
    // The Dev Sandbox gallery's Hulk and wall breach (owner 2026-10-13: "Add a Hulk zombie too ... The jump in too ... They can
    // also break through the walls to enter (Stage will eventually holographically recover broken pieces)"). Presentation only:
    // rigs posed on a script before the local pilot, no authority adds, the same poses and effects the live adds play from their
    // replicated entries.
    //  - Behind the minion row the Hulk jumps in on its profile (the rumbling warning, the cratering landing), roars, plods a
    //    circle shaking the floor, lumbers, winds up its smash (the shockwave and the shove), roars again, shrugs off a
    //    greatsword and crashes down dead.
    //  - Further back a wall segment of the gallery's own (named as a map names a breach wall, "breach:gallery-breach") is
    //    pounded from behind, bursts, a Hulk charges through and roars, and the stage rebuilds the wall in hardlight.
    public sealed partial class ArenaPresentation
    {
        public const float GalleryHulkLoop=22,GalleryHulkDrop=4.6f,GalleryBreachLoop=21;
        private CapturedAnimation.Player galleryBreachRig;private HordeKindState galleryBreachState;private GameObject galleryWall;private PveEntry galleryBreachEntry;
        private Func<int,Matrix4x4> hordeDeathJawBone;

        /// <summary>A dead hinged kind's jaw, burning away with the body (HordeDeathBody's bones, turned at the hinge).</summary>
        private void HordeDeathJaw(CapturedAnimation.Player rig,int kind,int look,int id,Func<int,bool> visible)
        {
            if(!NativeHordeJaw.Hinged(kind)||hordeDeathBone==null)return;string jaw=NativeHordeModels.JawId(kind,look,NativeHordeModels.Full);if(!sourceModels.Has(jaw))return;
            hordeJawKind=kind;hordeJawOpen=.4f;
            hordeDeathJawBone??=b=>{var m=hordeDeathBone(b);if(b!=CapturedRig.Head)return m;var h=NativeHordeModels.JawHinge(hordeJawKind);var hinge=new Vector3(h.X,h.Y,h.Z);
                return m*Matrix4x4.Translate(hinge)*Matrix4x4.Rotate(Quaternion.Euler(hordeJawOpen*Mathf.Rad2Deg,0,0))*Matrix4x4.Translate(-hinge);};
            sourceModels.Queue(jaw,rig.Root.transform.localToWorldMatrix,hordeDeathJawBone,visible,partTint:hordeDeathTint,glow:HordeGlow(id));
        }
        private static Vector3 GalleryDir(float yaw){float r=yaw*Mathf.Deg2Rad;return new Vector3(Mathf.Sin(r),0,Mathf.Cos(r));}

        /// <summary>The gallery's Hulk, behind the row (its own loop: the jump-in, roar, plod, smash, roar, a greatsword, its death).</summary>
        private void HordeGalleryHulk(NativePilot me,float now,Vector3 across,float yawToMe,float ground)
        {
            int k=NativeHordeKinds.Hulk,id=990000+k;
            if(galleryRigs[k]==null){galleryRigs[k]=captured.Create(transform,id);galleryStates[k]=new HordeKindState();galleryStates[k].Reset(id);galleryStates[k].Kind=k;galleryStates[k].Look=0;galleryStates[k].React.Bind(k);}
            var rig=galleryRigs[k];var s=galleryStates[k];var st=NativeHordeKinds.ProposedRow(k);uint seed=NativeHordeKinds.Seed(id);float sc=NativeHordeKinds.Scale[k];s.Scale=sc;
            float t=Mathf.Repeat(now-galleryStart,GalleryHulkLoop);Vector3 away=-GalleryDir(yawToMe),slot=galleryAt+away*4.2f;slot.y=ground;
            var attack=NativeHordeKinds.Attack.Smash;float strike=NativeHordeKinds.Strike(st.hit,false,0),recover=NativeHordeKinds.Recover(st.hit,st.cycle,false,0);
            if(t<GalleryHulkDrop)
            {
                var p=NativeHordeDrop.Hulk;bool shown=NativeHordeGait.Drop(minionPose,seed,t,GalleryHulkDrop,p.DefaultHeight,p,out float lift,out _);HordeDropJob(id,k,slot,yawToMe,p.DefaultHeight,t,GalleryHulkDrop,seed);
                s.Gait.Reset();if(!shown)return;NativePoseWriter.Write(rig.Bones,minionPose,"a gallery rig");rig.Root.transform.SetPositionAndRotation(slot+Vector3.up*(NativeHordeKinds.DrawRoot(k,false)+lift),Quaternion.Euler(0,yawToMe,0));rig.Root.transform.localScale=Vector3.one*sc;
                HordeQueue(rig,NativeHordeModels.Id(k,0,NativeHordeModels.Full),id,0,0,0,HordeGlow(id),1);HordeQueueJaw(rig,k,0,NativeHordeModels.Full,.15f,HordeGlow(id),1);HordeAdd(rig,id,float.NaN,float.NaN,ground,ground,float.NaN,Vector3.zero,0,true,k*8);return;
            }
            float g=t-GalleryHulkDrop,age=float.NaN,roar=float.NaN;Vector3 pos=slot+Vector3.up*NativeHordeKinds.DrawRoot(k,false),vel=Vector3.zero;float yaw=yawToMe;
            if(g>=.15f&&g<.15f+NativeHordeRoar.Seconds)roar=g-.15f;else if(g>=11.6f&&g<11.6f+NativeHordeRoar.Seconds)roar=g-11.6f;
            if(g>=2.2f&&g<7.6f){float speed=g<6.2f?st.walk:st.run,rad=1.8f,ang=g<6.2f?(g-2.2f)*st.walk/rad:4*st.walk/rad+(g-6.2f)*st.run/rad;pos=slot+Vector3.up*NativeHordeKinds.DrawRoot(k,false)+(across*(Mathf.Cos(ang)-1)+away*Mathf.Sin(ang))*rad;vel=(-across*Mathf.Sin(ang)+away*Mathf.Cos(ang))*speed;yaw=Mathf.Atan2(vel.x,vel.z)*Mathf.Rad2Deg;}
            else if(g>=7.9f&&g<7.9f+recover)age=g-7.9f;
            // A greatsword from me: the Hulk barely flinches.
            {float at=now-(g-13.6f);if(g>=13.6f&&s.React.Newest<at-.01f)s.React.Add(at,new NVector2(-away.x,-away.z),StaggerWeight.Heavy,48,true,false,NativeHordeKinds.Staggers(k,48,st.hp,true),st.stagger);}
            if(g>=14.2f)
            {
                float d=g-14.2f;NativeHordeGait.Death(minionPose,k,seed,Mathf.Min(d,2.3f),new NVector2(0,-1));NativePoseWriter.Write(rig.Bones,minionPose,"a gallery rig");
                rig.Root.transform.SetPositionAndRotation(slot+Vector3.up*NativeHordeKinds.DrawRoot(k,false),Quaternion.Euler(0,yaw,0));rig.Root.transform.localScale=Vector3.one*sc;bool body=!NativeMinionFx.Gone(d);
                if(d>=NativeHordeGait.DownAt&&d<NativeHordeGait.DownAt+.5f){var chest=rig.Bones[CapturedRig.Chest].position;HordeCrash(id,new Vector3(chest.x,slot.y,chest.z),now-(d-NativeHordeGait.DownAt),seed);}
                if(body){HordeDeathBody(rig,NativeHordeModels.Id(k,0,NativeHordeModels.Full),id,0,d,null);HordeDeathJaw(rig,k,0,id,null);}HordeAdd(rig,id,float.NaN,d,ground,ground,float.NaN,Vector3.zero,0,body,k*8);
                s.Gait.Reset();return;
            }
            ref var f=ref hordeInput[0];float inv=1/sc;
            f=new HordeMotionInput{Kind=k,Look=0,Id=id,Seed=seed,Time=now,Yaw=yaw,Ground=ground*inv,Root=ToN(pos)*inv,Velocity=ToN(vel)*inv,AttackAge=age,Strike=strike,Recover=recover,Attack=attack,Side=1,Health=1,HasTarget=true,Target=ToN(me.Body.position)*inv,Roaring=!float.IsNaN(roar),RoarAge=roar};
            s.Gait.Pose(f,minionPose,s.React);s.Distance=Vector3.Distance(pos,hordeEye);
            HordeKindJob(s,id,k,attack,age,strike,recover,pos,yaw,ground,st,me,rig,now);if(s.Gait.Landed!=0)HordeDust(s,k,ground,now);
            s.Jaw=NativeHordeJaw.Open(k,attack,age,strike,float.NaN,roar,now,seed);
            NativePoseWriter.Write(rig.Bones,minionPose,"a gallery rig");rig.Root.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,s.Gait.Yaw,0));rig.Root.transform.localScale=Vector3.one*sc;
            float gk=NativeHordeFx.Brighten(age,strike,recover);var glow=HordeGlow(id);var lit=new Vector4(gk,gk,gk,glow.w);
            HordeQueue(rig,NativeHordeModels.Id(k,0,NativeHordeModels.Full),id,0,0,0,lit,1);HordeQueueJaw(rig,k,0,NativeHordeModels.Full,s.Jaw,lit,1);HordeAdd(rig,id,float.NaN,float.NaN,ground,ground,float.NaN,vel,0,true,k*8);
        }

        /// <summary>The gallery's wall breach, far behind the row: a segment of its own, pounded, burst, charged through and rebuilt.</summary>
        private void HordeGalleryBreach(NativeArena arena,NativePilot me,float now,float yawToMe,float ground)
        {
            int k=NativeHordeKinds.Hulk,id=990010;Vector3 toMe=GalleryDir(yawToMe),away=-toMe;var center=galleryAt+away*10;center.y=ground+2.1f;
            if(galleryWall==null)
            {
                galleryWall=GameObject.CreatePrimitive(PrimitiveType.Cube);galleryWall.name=NativeBreach.ObjectName("gallery-breach");Destroy(galleryWall.GetComponent<Collider>());
                galleryWall.transform.SetPositionAndRotation(center,Quaternion.LookRotation(toMe));galleryWall.transform.localScale=new Vector3(6,4.2f,.5f);
                // The arena's own block material when it has one (a stage-coloured wall).
                Material m=null;var props=arena.Props.Count>0?arena.Props:arena.StaticBlocks;if(props.Count>0&&props[0]!=null){var r=props[0].GetComponent<Renderer>();if(r!=null)m=r.sharedMaterial;}if(m!=null)galleryWall.GetComponent<Renderer>().sharedMaterial=m;
                var from=center+away*1.8f;var exit=center-away*2.8f;
                galleryBreachEntry=new PveEntry{id="gallery-breach",kind=NativeBreach.Kind,from=new PvePoint{x=from.x,z=from.z},@exit=new PvePoint{x=exit.x,z=exit.z},yaw=yawToMe*Mathf.Deg2Rad};
            }
            if(galleryBreachRig==null){galleryBreachRig=captured.Create(transform,id);galleryBreachState=new HordeKindState();galleryBreachState.Reset(id);galleryBreachState.Kind=k;galleryBreachState.React.Bind(k);}
            var rig=galleryBreachRig;var s=galleryBreachState;var st=NativeHordeKinds.ProposedRow(k);uint seed=NativeHordeKinds.Seed(id);float sc=NativeHordeKinds.Scale[k];s.Scale=sc;
            float u=Mathf.Repeat(now-galleryStart,GalleryBreachLoop),spawnAt=now-u+.5f,breakAt=spawnAt+NativeBreach.Knock,activeAt=breakAt+NativeBreach.Through;
            Vector3 fromP=new Vector3(galleryBreachEntry.from.x,ground,galleryBreachEntry.from.z),exitP=new Vector3(galleryBreachEntry.@exit.x,ground,galleryBreachEntry.@exit.z);
            HordeBreachJob(arena,galleryBreachEntry,spawnAt,breakAt,fromP,exitP,k,seed,galleryWall);
            if(now<spawnAt){s.Gait.Reset();return;}
            float root=NativeHordeKinds.DrawRoot(k,false),age=float.NaN,strike=.42f,recover=.6f,roar=float.NaN;Vector3 pos=fromP+Vector3.up*root,vel=Vector3.zero;float yaw=yawToMe;bool entering=now<activeAt;
            if(now<breakAt)age=NativeBreach.KnockAge(now-spawnAt,strike);
            else if(now<activeAt){float w=(now-breakAt)/NativeBreach.Through;pos=Vector3.Lerp(fromP,exitP,w)+Vector3.up*root;vel=(exitP-fromP)/NativeBreach.Through;}
            else
            {
                pos=exitP+Vector3.up*root;float since=now-activeAt;if(since>=.1f&&since<.1f+NativeHordeRoar.Seconds)roar=since-.1f;
                // Then it lumbers aside, out of the hole's way.
                var side=Vector3.Cross(Vector3.up,toMe);float w=Mathf.Clamp01((since-2.3f)/3.5f);pos+=side*3.2f*w;if(since>=2.3f&&since<5.8f){vel=side*(3.2f/3.5f);yaw=Mathf.Atan2(vel.x,vel.z)*Mathf.Rad2Deg;}
            }
            ref var f=ref hordeInput[0];float inv=1/sc;
            f=new HordeMotionInput{Kind=k,Look=1,Id=id,Seed=seed,Time=now,Yaw=yaw,Ground=ground*inv,Root=ToN(pos)*inv,Velocity=ToN(vel)*inv,AttackAge=age,Strike=strike,Recover=recover,Attack=NativeHordeKinds.Attack.Smash,Side=1,Health=1,HasTarget=!entering,Target=ToN(me.Body.position)*inv,
                Entering=entering,EntryAge=now-spawnAt,EntryWalk=breakAt-spawnAt,Roaring=!float.IsNaN(roar),RoarAge=roar};
            s.Gait.Pose(f,minionPose,s.React);s.Distance=Vector3.Distance(pos,hordeEye);if(s.Gait.Landed!=0)HordeDust(s,k,ground,now);
            s.Jaw=NativeHordeJaw.Open(k,NativeHordeKinds.Attack.Smash,age,strike,float.NaN,roar,now,seed);
            NativePoseWriter.Write(rig.Bones,minionPose,"a gallery rig");rig.Root.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,s.Gait.Yaw,0));rig.Root.transform.localScale=Vector3.one*sc;
            var glow=HordeGlow(id);HordeQueue(rig,NativeHordeModels.Id(k,1,NativeHordeModels.Full),id,0,0,0,glow,1);HordeQueueJaw(rig,k,1,NativeHordeModels.Full,s.Jaw,glow,1);HordeAdd(rig,id,float.NaN,float.NaN,ground,ground,float.NaN,vel,0,true,k*8+1);
        }
        /// <summary>The gallery closes: its Hulk demos and its wall go (the breach gives nothing back to the map: the wall was its own).</summary>
        private void ClearHordeHulkGallery()
        {
            galleryBreachRig?.Dispose();galleryBreachRig=null;galleryBreachState=null;
            if(galleryWall!=null){for(int i=0;i<hordeBreaches.Length;i++)if(hordeBreaches[i].Live&&hordeBreaches[i].Entry==galleryBreachEntry)HordeBreachRelease(hordeBreaches[i]);Destroy(galleryWall);galleryWall=null;}galleryBreachEntry=null;
        }
    }
}
