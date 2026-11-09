using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The Top Dog's mallet by hand on the model (Core NativeDogMallet; docs/TOP-DOG.md "The mallet's moves"): over the legs' clip, the
    // chest turned, bent and leaned along the spine, the body squashed, stretched, lifted and its legs tucked, both fists closed on the handle
    // by reach (the right one carrying the mallet, the left below it), the barrel across the handle along the head's travel so the flat ends
    // lead, a slam's head brought down onto the floor, and the mallet stretched through a smear and squashed on an impact. In the arena it
    // goes on before the weapon follows the right hand (VisualMotionPose); in Order Up! the rig's hand is put on the mallet's frame after it.
    // Presentation only: nothing here reaches the rules, hit timing, hitboxes, damage or movement.
    public sealed partial class ArenaPresentation
    {
        private sealed class DogLimbs
        {
            public Transform Spine,Chest,Upper,Neck,RArm,RFore,RHand,LArm,LFore,LHand,LIndex,LLittle;public readonly Transform[] Thigh=new Transform[2],Shin=new Transform[2];
            public Vector3 BaseScale,BasePos,SetScale,SetPos;public Quaternion BaseTurn=Quaternion.identity,SetTurn=Quaternion.identity;public bool Held;
        }
        private sealed class DogSwingMemory{public int Move;public float From=-100,BlendAt=-100,Seen=-100;public DogMalletPose Last,Start;public Vector3 Face;public bool HasFace;}
        private readonly Dictionary<VisualBody,DogLimbs> dogLimbs=new Dictionary<VisualBody,DogLimbs>();
        private readonly Dictionary<int,DogSwingMemory> dogSwings=new Dictionary<int,DogSwingMemory>();
        // The drawn mallet's toon scale this frame (its stretch through a smear, its squash on an impact), per pilot (NativeSourcePresentation).
        private readonly Dictionary<int,(int frame,Vector3 scale)> dogMalletToon=new Dictionary<int,(int,Vector3)>();
        private const float DogCrossBlend=.08f,DogLetGo=.12f,DogNeckKeep=.4f;
        private static readonly float[] DogSpineShare={.3f,.35f,.35f};

        private DogLimbs DogLimbsOf(VisualBody body)
        {
            if(body?.Root==null)return null;if(dogLimbs.TryGetValue(body,out var l))return l;
            var skeleton=body.Root.transform.Find("VisualSkeleton");if(skeleton==null){dogLimbs[body]=null;return null;}
            var an=skeleton.GetComponent<Animator>();bool human=an!=null&&an.avatar!=null&&an.isHuman;
            var named=new Dictionary<string,Transform>(System.StringComparer.Ordinal);foreach(var t in skeleton.GetComponentsInChildren<Transform>(true))if(!named.ContainsKey(t.name))named[t.name]=t;
            Transform B(HumanBodyBones b,string name){var t=human?an.GetBoneTransform(b):null;return t!=null?t:named.TryGetValue(name,out var n)?n:null;}
            l=new DogLimbs{Spine=B(HumanBodyBones.Spine,"spine_01"),Chest=B(HumanBodyBones.Chest,"spine_02"),Upper=B(HumanBodyBones.UpperChest,"spine_03"),Neck=B(HumanBodyBones.Neck,"neck_01"),
                RArm=B(HumanBodyBones.RightUpperArm,"upperarm_r"),RFore=B(HumanBodyBones.RightLowerArm,"lowerarm_r"),RHand=B(HumanBodyBones.RightHand,"hand_r"),
                LArm=B(HumanBodyBones.LeftUpperArm,"upperarm_l"),LFore=B(HumanBodyBones.LeftLowerArm,"lowerarm_l"),LHand=B(HumanBodyBones.LeftHand,"hand_l"),
                LIndex=B(HumanBodyBones.LeftIndexProximal,"index_01_l"),LLittle=B(HumanBodyBones.LeftLittleProximal,"pinky_01_l")};
            l.Thigh[0]=B(HumanBodyBones.LeftUpperLeg,"thigh_l");l.Thigh[1]=B(HumanBodyBones.RightUpperLeg,"thigh_r");l.Shin[0]=B(HumanBodyBones.LeftLowerLeg,"calf_l");l.Shin[1]=B(HumanBodyBones.RightLowerLeg,"calf_r");
            if(l.RArm==null||l.RFore==null||l.RHand==null||l.LArm==null||l.LFore==null||l.LHand==null||l.Spine==null)l=null;
            dogLimbs[body]=l;return l;
        }
        /// <summary>The body's root scale as it was before a squash (TopDogBody's own squashes keep to it).</summary>
        private Vector3 DogRootBase(VisualBody body)=>dogLimbs.TryGetValue(body,out var l)&&l!=null&&l.Held?l.BaseScale:body.Root.transform.localScale;
        // The root's squash and lift: taken from where the root is unless it is still where this layer last put it.
        private static void DogRootHold(VisualBody body,DogLimbs l)
        {
            var root=body.Root.transform;
            if(!l.Held||root.localScale!=l.SetScale)l.BaseScale=root.localScale;
            if(!l.Held||root.localPosition!=l.SetPos)l.BasePos=root.localPosition;
            if(!l.Held||root.localRotation!=l.SetTurn)l.BaseTurn=root.localRotation;
            l.Held=true;
        }
        private static void DogRootLetGo(VisualBody body,DogLimbs l)
        {
            if(l==null||!l.Held)return;var root=body.Root.transform;
            if(root.localScale==l.SetScale)root.localScale=l.BaseScale;if(root.localPosition==l.SetPos)root.localPosition=l.BasePos;if(root.localRotation==l.SetTurn)root.localRotation=l.BaseTurn;l.Held=false;
        }

        /// <summary>The arena's Top Dog: the mallet move its kit is in at `time`, blended from the last one, onto the body (the motion's
        /// `hold`: the hand-to-weapon turn). `share` is the motion's own weight. Any other pilot only lets a held squash go.</summary>
        private void DogMalletLayer(VisualBody body,Actor a,float time,Quaternion hold,float share)
        {
            var l=DogLimbsOf(body);if(l==null)return;
            bool glaive=a.SourceSpec==NativeGlaive.Spec;if(a.SourceSpec!=NativeTopDog.Spec&&!glaive){DogRootLetGo(body,l);dogSwings.Remove(a.Id);return;}
            // The Glaive's moves are keyed the same way (Core NativeGlaiveMotion), one-handed.
            bool on;DogMalletPose pose;int move;float from;
            if(glaive){on=NativeGlaiveMotion.Sample(NativeGlaiveView.Of(a),time,out pose,out var gm,out from);move=100+(int)gm;}
            else{on=NativeDogMallet.Sample(NativeDogView.Of(a),time,out pose,out var dm,out from);move=(int)dm;}
            if(!dogSwings.TryGetValue(a.Id,out var mem)){mem=new DogSwingMemory();dogSwings[a.Id]=mem;}
            if(time<mem.Seen-.25f){mem.Move=0;mem.HasFace=false;mem.Last=default;}
            mem.Seen=time;
            if(on&&(move!=mem.Move||Mathf.Abs(from-mem.From)>1e-3f)){mem.Start=mem.Last;mem.BlendAt=time;mem.Move=move;mem.From=from;}
            if(!on)
            {
                // Let go: the last pose fades out (a cast cut short, an ability taking the hands).
                mem.Move=0;
                if(mem.Last.Weight<=.001f){DogRootLetGo(body,l);mem.HasFace=false;return;}
                pose=mem.Last;pose.Weight=Mathf.MoveTowards(mem.Last.Weight,0,Time.deltaTime/DogLetGo);
            }
            else if(time-mem.BlendAt<DogCrossBlend&&mem.Start.Weight>.01f)
            {
                float u=Mathf.SmoothStep(0,1,(time-mem.BlendAt)/DogCrossBlend);float w=Mathf.Lerp(mem.Start.Weight,1,u);pose=DogMalletPose.Lerp(mem.Start,pose,u);pose.Weight=Mathf.Min(w,1);
            }
            mem.Last=pose;
            var moving=new Vector2(a.Velocity.x,a.Velocity.z).magnitude>.6f;
            var p=pose;p.Weight*=Mathf.Clamp01(share);if(a.Grounded&&moving)p.Tuck=0;
            if(p.Weight<=.001f){DogRootLetGo(body,l);return;}
            float scale=glaive?1:Mathf.Max(.2f,TopDogMallet(a,time));
            DogMalletPoseBody(body,l,p,scale,hold,mem,out _,out _);
            if(!glaive)dogMalletToon[a.Id]=(Time.frameCount,DogToonScale(p));
        }
        /// <summary>Order Up!'s slam on the intro's body (Core NativeDogMallet.Intro), at intro time `t`; the rig's right hand then on the
        /// mallet's frame (the intro's mallet draws there).</summary>
        private void DogMalletIntro(VisualBody body,CapturedAnimation.Player rig,float t)
        {
            var l=DogLimbsOf(body);if(l==null)return;
            if(!NativeDogMallet.Intro(t,out var pose)){DogRootLetGo(body,l);dogSwings.Remove(-1);return;}
            if(!dogSwings.TryGetValue(-1,out var mem)){mem=new DogSwingMemory();dogSwings[-1]=mem;}
            var rh=rig.Bones[CapturedRig.RHand];if(!mem.HasFace){mem.Face=rh.up;mem.HasFace=true;}
            // The visual hand to the rig's hand as bound (the turn the mallet's frame keeps to the fist).
            var hold=Quaternion.Inverse(l.RHand.rotation)*rh.rotation;var keep=rh.rotation;var keepAt=rh.position;
            DogMalletPoseBody(body,l,pose,Mathf.Max(.2f,dogMalletScale),hold,mem,out var grip,out var turn);
            rh.SetPositionAndRotation(Vector3.Lerp(keepAt,grip,pose.Weight),Quaternion.Slerp(keep,turn,pose.Weight));
            dogMalletToon[-1]=(Time.frameCount,DogToonScale(pose));
        }
        private static Vector3 DogToonScale(in DogMalletPose p)
        {
            float w=p.Weight,st=.16f*p.Smear*w,sq=.16f*p.Squish*w;return new Vector3(1-.25f*st+sq,1-.25f*st+sq,1+st-.15f*sq);
        }
        /// <summary>The pose onto the body: squash and lift, the spine's turn, the legs' tuck, both arms on the handle; out: the mallet's
        /// frame (the right palm on its grip, its handle and barrel).</summary>
        private static void DogMalletPoseBody(VisualBody body,DogLimbs l,in DogMalletPose p,float scale,Quaternion hold,DogSwingMemory mem,out Vector3 grip,out Quaternion turn)
        {
            var root=body.Root.transform;float w=Mathf.Clamp01(p.Weight);
            // Squashed or stretched about the feet (the width the other way), lifted for a hop.
            DogRootHold(body,l);float sq=Mathf.Lerp(1,Mathf.Max(.3f,p.Squash),w),wide=1/Mathf.Sqrt(sq);
            root.localScale=Vector3.Scale(l.BaseScale,new Vector3(wide,sq,wide));root.localPosition=l.BasePos;float floorY=root.position.y;
            // A whirl turns the whole body about its feet (the turn's share wrapped, so a full turn hands back without unwinding).
            root.localRotation=l.BaseTurn*Quaternion.AngleAxis(Mathf.DeltaAngle(0,p.Spin)*w,Vector3.up);l.SetTurn=root.localRotation;
            if(p.Lift*w>1e-4f)root.position+=Vector3.up*(p.Lift*w);l.SetScale=root.localScale;l.SetPos=root.localPosition;
            var up=root.up;var right=root.right;var fwd=root.forward;var facing=root.rotation;
            // The spine: the chest's turn shared down it; the neck takes some of the twist back (the face stays on the target).
            var q=facing*Quaternion.AngleAxis(p.Twist*w,Vector3.up)*Quaternion.AngleAxis(p.Bend*w,Vector3.right)*Quaternion.AngleAxis(p.Lean*w,Vector3.forward)*Quaternion.Inverse(facing);
            var spine=new[]{l.Spine,l.Chest,l.Upper};for(int i=0;i<3;i++)if(spine[i]!=null)spine[i].rotation=Quaternion.Slerp(Quaternion.identity,q,DogSpineShare[i])*spine[i].rotation;
            if(l.Neck!=null)l.Neck.rotation=Quaternion.AngleAxis(-p.Twist*w*DogNeckKeep,up)*l.Neck.rotation;
            // The legs drawn up (a hop's tuck): the thighs forward, the knees bent.
            if(p.Tuck*w>1e-3f)for(int s=0;s<2;s++){if(l.Thigh[s]!=null)l.Thigh[s].rotation=Quaternion.AngleAxis(-62*p.Tuck*w,right)*l.Thigh[s].rotation;if(l.Shin[s]!=null)l.Shin[s].rotation=Quaternion.AngleAxis(105*p.Tuck*w,right)*l.Shin[s].rotation;}
            // Where the mallet goes: the grip from the shoulders' middle and the handle, in the facing turned by the twist.
            var frame=facing*Quaternion.AngleAxis(p.Twist,Vector3.up);var mid=(l.RArm.position+l.LArm.position)*.5f;
            grip=mid+frame*MV(p.Grip);var handle=frame*MV(p.Handle);var face=frame*MV(p.Face);
            if(p.Floor>1e-3f){var ahead=frame*Vector3.forward;var d=NativeDogMallet.FloorHandle(new NVector(0,grip.y-floorY,0),new NVector(handle.x,handle.y,handle.z),new NVector(ahead.x,ahead.y,ahead.z),p.Floor,scale);handle=new Vector3(d.X,d.Y,d.Z);}
            handle=handle.sqrMagnitude>1e-8f?handle.normalized:fwd;face-=handle*Vector3.Dot(face,handle);face=face.sqrMagnitude>1e-8f?face.normalized:Vector3.Cross(handle,right).normalized;
            // The barrel's way kept from frame to frame (the two faces alike: a turn of the wrist never flips).
            if(mem.HasFace&&Vector3.Dot(face,mem.Face)<0)face=-face;mem.Face=face;mem.HasFace=true;
            turn=Quaternion.LookRotation(handle,face);
            // The right fist: its palm on the grip, the hand turned so the weapon's frame (hand times hold) is the mallet's.
            var handTurn=turn*Quaternion.Inverse(hold);var wrist=grip-handTurn*PalmR;
            var keepR=l.RHand.rotation;ArmReachTo(l.RArm,l.RFore,l.RHand,Vector3.Lerp(l.RHand.position,wrist,w),right-up*.7f);l.RHand.rotation=Quaternion.Slerp(keepR,handTurn,w);
            // The left fist below it on the handle, its knuckles along it (the index toward the head).
            var support=grip+turn*new Vector3(0,0,NativeDogMallet.Support);
            if(l.LIndex!=null&&l.LLittle!=null){var knuckles=l.LIndex.position-l.LLittle.position;if(knuckles.sqrMagnitude>1e-8f)l.LHand.rotation=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(knuckles,handle),w*(1-Mathf.Clamp01(p.Free)))*l.LHand.rotation;}
            var leftTurn=l.LHand.rotation;var palm=l.LHand.TransformPoint(PalmL);float lw=w*(1-Mathf.Clamp01(p.Free));
            if(lw>1e-3f){ArmReachTo(l.LArm,l.LFore,l.LHand,l.LHand.position+(support-palm)*lw,-right-up*.7f);l.LHand.rotation=leftTurn;}
        }
        private static Vector3 MV(NVector v)=>new Vector3(v.X,v.Y,v.Z);
        /// <summary>The drawn mallet's toon scale for pilot `id` this frame (1: none).</summary>
        private Vector3 DogToon(int id)=>dogMalletToon.TryGetValue(id,out var t)&&t.frame==Time.frameCount?t.scale:Vector3.one;
    }
}
