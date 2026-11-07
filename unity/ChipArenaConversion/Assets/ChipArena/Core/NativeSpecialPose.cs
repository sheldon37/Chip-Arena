using System;
using System.Numerics;
using static ChipArena.UnityConversion.CapturedRig;

namespace ChipArena.UnityConversion
{
    // Source special phrases on the captured native guard. Recovery uses source pose tables and
    // timestamps with stateless native floor clearance; it does not feed rendered joints into history.
    public static class NativeSpecialPose
    {
        public static float Ease(float x){x=MathUtil.Clamp(x,0,1);return x*x*x*(x*(x*6-15)+10);}
        public static bool Active(Actor a,float time)=>a.SpecialKind!=null&&time>=a.SpecialAt&&time<a.SpecialUntil;
        public static bool Down(Actor a,float time)=>a.DownAt>=0&&time>=a.DownAt&&time<a.DownUntil;
        public static void Envelope(Actor a,float time,out float coil,out float drive,out float recovery,out float weight)
        {
            float age=time-a.SpecialAt,wind=Math.Max(.025f,a.SpecialRelease-a.SpecialAt);
            drive=Ease((age-wind*.5f)/(wind*.5f));coil=Ease(age/(wind*.65f))*(1-drive);drive+=Ease((age-wind)/.22f)*.12f;
            float settle=a.SpecialRelease+.06f;recovery=a.SpecialKind=="wolf-spin"?1:1-Ease((time-settle)/Math.Max(.12f,a.SpecialUntil-settle));weight=Ease(age/.055f)*recovery;
        }
        private static void Add(Pose p,int bone,float x=0,float y=0,float z=0)
        {var e=NativeFirstPersonPose.EulerXYZ(p.Rotation[bone]);p.Rotation[bone]=Euler(e.X+x,e.Y+y,e.Z+z);}
        public static Quaternion HeldRotation(Actor a,float time,bool left=false)
        {
            // The melee Q/E layer aims the weapon with its grips (Core/MeleeKitMotion).
            if(MeleeKitMotion.Owns(a,time))return MeleeKitMotion.Held(a,time,left);
            if(!Active(a,time)||a.SpecialKind=="wolf-spin")return Quaternion.Identity;
            Envelope(a,time,out float coil,out _,out _,out float weight);
            float x=left?.95f:a.SpecialKind=="sword-launch"?.95f:a.SpecialKind=="shield-throw"?1.15f:1;
            return Quaternion.Slerp(Quaternion.Identity,Euler(x,0,!left&&a.SpecialKind=="shield-throw"?.5f*coil:0),weight);
        }
        public static void Special(Pose p,Actor a,float time,bool firstPerson)
        {
            // A melee Q/E cast (replicated MeleeKit) has its own per-ability phrases (Core/MeleeKitMotion).
            if(MeleeKitMotion.Apply(p,a,time,firstPerson))return;
            if(!Active(a,time))return;
            Envelope(a,time,out float coil,out float d,out float recovery,out float weight);
            void Blend(int b,float x,float y=0,float z=0)=>p.Rotation[b]=Quaternion.Slerp(p.Rotation[b],Euler(x,y,z),weight);
            if(a.SpecialKind=="wolf-spin")
            {
                float age=time-a.SpecialAt,air=Math.Max(0,time-a.SpecialRelease),land=a.SpecialLanded<0?0:Ease((time-a.SpecialLanded)/.55f),flight=Ease(air/.76f),squat=a.SpecialReleased?0:coil;
                // Full-body somersault belongs to the world rig. The view arms stay braced against the camera.
                p.Rotation[Body]=Euler(firstPerson?0:a.SpecialLanded<0?flight*(float)Math.PI*2:(float)Math.PI*2+.24f*(1-land));
                p.Position[Body]+=V(0,-squat*.18f-(a.SpecialLanded>=0?.25f*(1-land):0),a.SpecialLanded>=0?.16f*(1-land):0);
                p.Rotation[Pelvis]=Euler(0,-.25f*coil);p.Rotation[Spine]=Euler(-.18f*coil);p.Rotation[Chest]=Euler(0,.16f*coil);p.Rotation[Head]=Euler(-.15f);
                float tuck=a.SpecialReleased&&a.SpecialLanded<0?(float)Math.Sin(Math.PI*flight):0;
                Blend(LHip,-.65f*squat-1.15f*tuck-(1-land)*.2f);Blend(RHip,-.5f*squat-.8f*tuck);
                Blend(LKnee,.95f*squat+1.55f*tuck+(a.SpecialLanded>=0?.7f*(1-land):0));Blend(RKnee,.9f*squat+1.4f*tuck);
                float impact=a.SpecialLanded>=0?1:Math.Max(0,(flight-.72f)/.28f),settle=a.SpecialLanded>=0?land:0;
                var grip=V(-.06f,.42f*(1-impact)-.19f*impact+.24f*settle,.12f+.28f*impact);
                var wrist=Euler(-1.75f*(1-impact)+.7f*impact-.85f*settle,0,-.08f);
                // Native wrist axes already hold the blade along +z; both hands share one rigid grip.
                void Brace(bool left,Vector3 target)
                {
                    int b=left?LArm:RArm;var q0=p.Rotation[b];var q1=p.Rotation[b+1];var q2=p.Rotation[b+2];
                    Arm(p,left,target,wrist,V(left?1:-1,-.2f,0));float w=Ease(age/.06f);
                    p.Rotation[b]=Quaternion.Slerp(q0,p.Rotation[b],w);p.Rotation[b+1]=Quaternion.Slerp(q1,p.Rotation[b+1],w);p.Rotation[b+2]=Quaternion.Slerp(q2,p.Rotation[b+2],w);
                }
                Brace(false,grip);Brace(true,grip+Vector3.Transform(V(0,0,-.16f),wrist));
                return;
            }
            float torque=(-.65f*coil+.95f*d)*recovery;
            Add(p,Pelvis,y:torque*.45f);Add(p,Spine,y:torque*.5f);Add(p,Chest,y:torque*.35f);Add(p,Head,y:-torque*.65f);
            p.Position[Body]+=V(0,-.07f*coil*weight,(-.08f*coil+.16f*d)*weight);Add(p,Body,(-.08f*coil+.16f*d)*weight);
            Blend(LHip,-.3f-.25f*d);Blend(LKnee,.42f+.25f*d);Blend(RHip,.15f+.2f*d);Blend(RKnee,.32f+(a.SpecialKind=="dagger-throw"?.18f*d:0));
            if(a.SpecialKind=="sword-launch")
            {
                Blend(RArm,-.1f-2.3f*d,-.65f*coil+.1f*d,-.2f-.15f*coil);Blend(RElbow,-1+.45f*d);Blend(RHand,-.15f+.35f*d,0,-.12f);
                p.Position[Body].Y+=(-.16f*coil+.13f*d)*weight;Add(p,Body,-.23f*d*weight);Add(p,Spine,-.12f*d*weight);Add(p,Chest,-.06f*d*weight);
                Blend(LArm,.3f-.9f*d,.1f,.4f);Blend(LElbow,-.85f);Blend(LHand,.1f);Add(p,RFoot,.3f*d*weight);Add(p,LFoot,.35f*d*weight);
            }
            else
            {
                bool shield=a.SpecialKind=="shield-throw";float follow=Ease((time-a.SpecialRelease)/.18f)*(shield?.45f:.3f);
                Blend(RArm,shield?-.3f-.08f*coil-1.1f*d:-1.25f-.3f*coil-.1f*d,(-1.15f*coil+1.1f*d)*(shield?1:.7f)+follow,-.45f-(shield?.05f:.25f)*coil+follow*.4f);
                Blend(RElbow,-(shield?1.1f:1.7f)*coil-.25f*d-.15f);Blend(RHand,-.15f*coil+.3f*d,-.35f*coil+.5f*d,shield?.15f:-.3f);
                Blend(LArm,-.3f-.65f*d,.4f-.9f*d,.4f);Blend(LElbow,-1.1f);Blend(LHand,.1f);
                if(!shield){Add(p,RFoot,.4f*d*weight);p.Position[Body].Z+=.04f*d*weight;}
            }
        }
        private static readonly int[] Joints={Pelvis,Spine,Chest,Neck,Head,LArm,RArm,LElbow,RElbow,LHand,RHand,LHip,RHip,LKnee,RKnee,LFoot,RFoot,LToe,RToe};
        private static readonly Vector3[] Flat={V(0,0),V(-.04f,0),V(0,0),V(.05f,0),V(.14f,0),V(-.08f,0,.95f),V(-.08f,0,-.95f),V(-.32f,0),V(-.38f,0),V(0,0,.1f),V(0,0,-.1f),V(-.06f,0,.16f),V(-.1f,0,-.13f),V(.14f,0),V(.22f,0),V(.5f,.28f),V(.45f,-.3f),V(0,0),V(0,0)};
        private static readonly Vector3[] Tuck={V(0,0),V(.3f,0),V(.28f,0),V(.2f,0),V(.45f,0),V(-2.3f,0,.45f),V(-1.25f,0,-.15f),V(-1.9f,0),V(-2,0),V(.3f,0),V(.3f,0),V(-1.95f,0,.1f),V(-1.8f,0,-.1f),V(2.2f,0),V(2.1f,0),V(.45f,0),V(.45f,0),V(0,0),V(0,0)};
        private static readonly Vector3[] Crouch={V(0,0),V(.1f,0),V(.06f,0),V(-.05f,0),V(-.1f,0),V(-.95f,0,.22f),V(-1.05f,0,-.12f),V(-.35f,0),V(-1.45f,0),V(0,0),V(0,0),V(-2.083f,.019f,-.011f),V(-.092f,.007f,-.152f),V(1.607f,.072f,.069f),V(1.842f,.049f,.037f),V(.173f,-.068f,-.04f),V(-2.545f,-.042f,.114f),V(0,0),V(.225f,0)};
        private static void Mix(Pose p,Vector3[] table,float weight)
        {for(int i=0;i<Joints.Length;i++){var v=table[i];p.Rotation[Joints[i]]=Quaternion.Slerp(p.Rotation[Joints[i]],Euler(v.X,v.Y,v.Z),MathUtil.Clamp(weight,0,1));}}
        private static float AirTilt(float age)=>-1.3f*(1-(float)Math.Pow(1-MathUtil.Clamp(age/.5f,0,1),3));
        private static float Bump(float u)=>u<=0||u>=1?0:(float)Math.Sin(Math.PI*u);
        private static void Air(Pose p,float age,float vy)
        {
            float w=Ease(age/.12f),lift=MathUtil.Clamp(-vy*.07f,-.45f,.6f)*Ease(age/.14f),decay=(float)Math.Exp(-age*2)*.22f;
            float F(float offset)=>(float)Math.Sin(age*9+offset)*decay;
            void Set(int b,float x,float z=0)=>p.Rotation[b]=Quaternion.Slerp(p.Rotation[b],Euler(x,0,z),w);
            Set(Spine,.1f);Set(Chest,.08f,-.05f);Set(Head,.34f*age/.09f*(float)Math.Exp(1-age/.09f)-.18f*Ease(age/.45f),.05f);
            Set(LArm,-.55f-.45f*lift+F(0),.7f+.35f*lift+F(.6f)*.6f);Set(RArm,-.45f-.4f*lift+F(.55f),-.78f-.32f*lift-F(1.2f)*.6f);
            Set(LElbow,-.45f-.35f*lift+F(1.6f)*.5f);Set(RElbow,-.55f-.3f*lift+F(2.1f)*.5f);
            Set(LHip,-.6f-.25f*lift+F(.8f),.14f);Set(RHip,.2f-.2f*lift-F(1.4f),-.12f);Set(LKnee,.8f+.3f*lift+F(2.4f)*.5f);Set(RKnee,.35f+.35f*lift-F(2.9f)*.5f);
            p.Rotation[Body]=Euler(AirTilt(age),0,(float)Math.Sin(age*5)*.12f*Ease(age/.2f));p.Position[Body]=V(0,-.64f*Ease((-vy-.5f)/12)*w);
        }
        public static void Recover(Pose p,Actor a,float time)
        {
            if(!Down(a,time))return;
            float age=Math.Max(0,time-a.DownAt),landAge=Math.Max(0,a.DownLanded-a.DownAt),lt=time-a.DownLanded;
            string phase=NativeSpecialRules.Phase(a.DownLanded,a.DownPickup,a.DownRoll,a.DownRollEnd,a.DownUsed,time);
            if(phase=="air")Air(p,age,a.Velocity.y);
            else if(phase=="roll"||phase=="rise")
            {
                float span=Math.Max(.01f,a.DownRollEnd-a.DownRoll),u=MathUtil.Clamp((time-a.DownRoll)/span,0,1),s=u-.9f*(float)Math.Sin(2*Math.PI*u)/(2*(float)Math.PI);
                float psi=a.DownDirection.Length<.0001f?0:(float)Math.Atan2(a.DownDirection.x,a.DownDirection.z)+(float)Math.PI-a.Yaw*(float)Math.PI/180;
                psi=(float)Math.Atan2(Math.Sin(psi),Math.Cos(psi));float yaw=psi*Ease(u/(.25f+.45f*Math.Abs(psi)/(float)Math.PI))*(1-Ease((time-a.DownRoll-.72f*span)/Math.Max(.01f,a.DownUntil-a.DownRoll-.72f*span)));
                if(phase=="roll")
                {
                    Mix(p,Flat,1);Mix(p,Tuck,Ease((time-a.DownRoll+.08f)/.2f));Mix(p,Crouch,Ease((u-.45f)/.55f));
                    p.Rotation[Body]=Rot(Y,yaw)*Euler(-(float)Math.PI/2+(.3f-1.5f*(float)Math.PI)*s);p.Position[Body]=V(0,-.64f+.32f*Ease(s)+.38f*Bump(s/.85f)+.07f*Bump((s-.6f)/.4f));
                }
                else
                {
                    float v=MathUtil.Clamp((time-a.DownRollEnd)/Math.Max(.01f,a.DownUntil-a.DownRollEnd),0,1),hips=1-(1-v)*(1-v),torso=Ease((v-.1f)/.9f);
                    var stand=p.Rotation[Body];var pos=p.Position[Body];Mix(p,Crouch,1-torso);
                    p.Rotation[Body]=Rot(Y,yaw)*Quaternion.Slerp(Euler(.3f),stand,torso);p.Position[Body]=Vector3.Lerp(V(0,-.32f),pos,hips);
                }
            }
            else
            {
                if(lt<.2f)Air(p,landAge,-23*landAge/2);
                float drop=p.Position[Body].Y;Mix(p,Flat,Ease(lt/.15f));float legs=!a.DownUsed?Ease(lt/.12f)*(1-Ease((time-a.DownPickup)/.12f)):0;
                void LegUp(int b,float x,float z=0)=>p.Rotation[b]=Quaternion.Slerp(p.Rotation[b],Euler(x,0,z),legs);
                LegUp(LHip,-1.15f,.12f);LegUp(RHip,-.9f,-.12f);LegUp(LKnee,1.25f);LegUp(RKnee,1.1f);LegUp(LFoot,.3f);LegUp(RFoot,.3f);
                if(a.DownRoll>=0)Mix(p,Tuck,Ease((time-a.DownRoll+.08f)/.2f));
                float turn=Ease(lt/.2f),fall=Math.Min(.16f,Math.Max(.05f,(float)Math.Sqrt(2*Math.Abs(-.64f-drop)/35))),hips=MathUtil.Clamp(lt/fall,0,1);
                p.Rotation[Body]=Euler(AirTilt(landAge)+(-(float)Math.PI/2-AirTilt(landAge))*turn);p.Position[Body]=V(0,drop+(-.64f-drop)*hips*hips);
            }
            // Clearance includes the native voxel boxes, not just the source's ankle/hand points.
            float low=p.Lowest();if(low<-.865f)p.Position[Body].Y+=-.865f-low;
        }
    }
}
