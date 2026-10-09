using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The source CharacterRig as the captured-melee port poses it: CapturedRig's 22 joints in the source frame, each with
    // three's paired quaternion/Euler state (a quaternion write re-derives the XYZ Euler; an Euler write rebuilds the
    // quaternion), so `rotation.y += turn` style layers behave exactly as they do over three's Object3D. "Group" is the
    // rig's root frame (the parent of motion-root). The weapons sit on their wrists at the grip (rig-ik gripWeapon).
    public sealed class MeleeRig
    {
        public const int N=22;
        public readonly Quaternion[] Q=new Quaternion[N];
        public readonly Vector3[] E=new Vector3[N],P=new Vector3[N];
        // Group: source-frame world position and heading (rotation.y).
        public Vector3 GroupPosition;public float GroupYaw;
        // Per-rig scalars of the source rig (motion-quality): stride blend, stride phase, ground weight, landing dip.
        public float Motion,Stride,GroundWeight=1,Land;public bool WasGrounded=true;
        public bool LegsAuthored,LegsLayered,MocapPosed;
        public bool WeaponVisible=true;
        // Both weapons' length scale along their +z (character-animation.ts:57 scales r.weapon and r.offWeapon by
        // reach / range before the melee layers run), so the blade tip, blade end, pommel and support grip the layers
        // measure are the stretched ones the renderer draws.
        public float WeaponReach=1;
        public Vector3 Reached(Vector3 weaponLocal)=>new Vector3(weaponLocal.X,weaponLocal.Y,weaponLocal.Z*WeaponReach);
        public MeleeRig(){Reset();}
        public void Reset(){for(int i=0;i<N;i++){P[i]=CapturedRig.Bones[i].Position;Q[i]=Quaternion.Identity;E[i]=Vector3.Zero;}}
        public static int Parent(int i)=>CapturedRig.Bones[i].Parent;
        public void SetQ(int i,Quaternion q){Q[i]=q;E[i]=MeleeMath.ToEuler(q);}
        public void SetE(int i,float x,float y,float z){E[i]=new Vector3(x,y,z);Q[i]=MeleeMath.Euler(x,y,z);}
        public void SetE(int i,Vector3 e)=>SetE(i,e.X,e.Y,e.Z);
        public void AddE(int i,float x,float y,float z)=>SetE(i,E[i].X+x,E[i].Y+y,E[i].Z+z);
        public void Slerp(int i,Quaternion q,float w)=>SetQ(i,MeleeMath.Slerp(Q[i],q,w));
        public void Multiply(int i,Quaternion q)=>SetQ(i,Q[i]*q);
        public void Premultiply(int i,Quaternion q)=>SetQ(i,q*Q[i]);
        // rig-ik chainQuat / chainPoint: node's frame and origin in `root`'s frame (-1: the group).
        public Quaternion ChainQuat(int node,int root){Quaternion q=Quaternion.Identity;for(int n=node;n>=0&&n!=root;n=Parent(n))q=Q[n]*q;return q;}
        public Vector3 ChainPoint(int node,int root){Vector3 p=Vector3.Zero;for(int n=node;n>=0&&n!=root;n=Parent(n))p=Vector3.Transform(p,Q[n])+P[n];return p;}
        public void Copy(MeleeRig o){Array.Copy(o.Q,Q,N);Array.Copy(o.E,E,N);Array.Copy(o.P,P,N);}
        public void WritePose(CapturedRig.Pose pose){for(int i=0;i<N;i++){pose.Position[i]=P[i];pose.Rotation[i]=Q[i];}}
        public void ReadPose(CapturedRig.Pose pose){for(int i=0;i<N;i++){P[i]=pose.Position[i];SetQ(i,pose.Rotation[i]);}}

        // ---- rig-ik.ts ----
        private const float UPPER=.27f,FORE=.25f,THIGH=.25f,ARM_MIN=.20f,ARM_MAX=.515f,SUPPORT_MIN=.10f,LEG_MIN=.1f,LEG_MAX=.485f;
        public const float FLOOR=-.795f;
        private static Vector3 BendDirection(Vector3 dir,Vector3 p)
        {
            p-=dir*Vector3.Dot(p,dir);
            if(p.LengthSquared()<1e-10f){p=new Vector3(0,0,1)-dir*dir.Z;if(p.LengthSquared()<1e-8f)p=new Vector3(1,0,0)-dir*dir.X;}
            return Vector3.Normalize(p);
        }
        private static int Arm(bool left)=>left?CapturedRig.LArm:CapturedRig.RArm;
        private static int Elbow(bool left)=>left?CapturedRig.LElbow:CapturedRig.RElbow;
        private static int Wrist(bool left)=>left?CapturedRig.LHand:CapturedRig.RHand;
        private static int Clavicle(bool left)=>left?CapturedRig.LClav:CapturedRig.RClav;
        private void SolveArm(bool left,Vector3 end,Vector3 pole,float weight,float min)
        {
            int arm=Arm(left),elbow=Elbow(left);Vector3 s=P[arm],d=end-s;float raw=d.Length();d=raw<1e-6f?new Vector3(0,-1,0):d/raw;float dd=MeleeMath.Clamp(raw,min,ARM_MAX);
            float along=(UPPER*UPPER-FORE*FORE+dd*dd)/(2*dd),height=(float)Math.Sqrt(Math.Max(0,UPPER*UPPER-along*along));Vector3 p=BendDirection(d,MeleeMath.Unit(pole));
            float cos=along/UPPER,sin=height/UPPER;Vector3 u=d*cos+p*sin,w=d*sin-p*cos,y=-u,x=Vector3.Cross(y,w);
            Quaternion upper=MeleeMath.Basis(x,y,w);Vector3 f=d*dd-u*UPPER;Quaternion lower=MeleeMath.AxisAngle(MeleeMath.X,-(float)Math.Atan2(Vector3.Dot(f,w),Vector3.Dot(f,u)));
            if(weight>=1){SetQ(arm,upper);SetQ(elbow,lower);}else{Slerp(arm,upper,weight);Slerp(elbow,lower,weight);}
        }
        private void OrientWrist(bool left,Quaternion orientation,float weight)
        {
            int wrist=Wrist(left);Quaternion q=MeleeMath.Inverse(ChainQuat(Parent(wrist),CapturedRig.Chest))*orientation;if(weight>=1)SetQ(wrist,q);else Slerp(wrist,q,weight);
        }
        private Vector3 ChestToClavicle(bool left,Vector3 target){int c=Clavicle(left);return Vector3.Transform(target-P[c],MeleeMath.Inverse(Q[c]));}
        // The weapon's support grip (weapon-visuals: sword, daggers and greatsword at (0,0,-.11) in the weapon frame).
        public static readonly Vector3 SupportGrip=new Vector3(0,0,-.11f);
        public void SupportHand(float weight)
        {
            if(weight<=.001f)return;int c=CapturedRig.LClav;Quaternion hq=ChainQuat(CapturedRig.RHand,-1);Vector3 grip=ChainPoint(CapturedRig.RHand,-1)+Vector3.Transform(Reached(SupportGrip),hq);
            Vector3 e=Vector3.Transform(grip-ChainPoint(c,-1),MeleeMath.Inverse(ChainQuat(c,-1)));
            SolveArm(true,e,new Vector3(1,-.8f,-.25f),weight,SUPPORT_MIN);
            Slerp(CapturedRig.LHand,MeleeMath.Inverse(ChainQuat(CapturedRig.LElbow,-1))*hq,weight);
        }
        public void BraceWeapon(Vector3 target,Quaternion rotation,float weight,Vector3? pole=null,bool support=true)
        {
            if(weight<.001f)return;
            SolveArm(false,ChestToClavicle(false,target),pole??new Vector3(-1,-.7f,-.2f),weight,ARM_MIN);OrientWrist(false,rotation,weight);
            if(support)SupportHand(weight);
        }
        public void Hand(bool left,Vector3 target,Quaternion orientation,float weight,Vector3? pole=null)
        {
            SolveArm(left,ChestToClavicle(left,target),pole??(left?new Vector3(1,-.65f,-.3f):new Vector3(-1,-.65f,-.3f)),weight,ARM_MIN);OrientWrist(left,orientation,weight);
        }
        private static int Hip(bool left)=>left?CapturedRig.LHip:CapturedRig.RHip;
        // rig-ik foot(): planted foot on the floor while the pelvis turns and drops; marks the legs authored.
        public void Foot(bool left,Vector3 target,float yaw,float heel,float weight,Vector3? from=null,float maxReach=LEG_MAX)
        {
            LegsAuthored=true;int hip=Hip(left),knee=hip+1,ankle=hip+2,toe=hip+3;
            Quaternion q=ChainQuat(CapturedRig.Pelvis,-1),q2=MeleeMath.Inverse(q);Vector3 k=ChainPoint(CapturedRig.Pelvis,-1);
            Vector3 placed=MeleeMath.Lerp(from??ChainPoint(ankle,-1),target,weight);placed.Y=Math.Max(FLOOR,placed.Y);
            placed=Vector3.Transform(placed-k,q2);Vector3 s=P[hip],d=placed-s;float raw=d.Length();d=raw<1e-6f?new Vector3(0,-1,0):d/raw;float dd=MeleeMath.Clamp(raw,LEG_MIN,maxReach);
            float reach=Vector3.Transform(d,q).Z;Vector3 pole=Vector3.Transform(new Vector3(0,.6f*MeleeMath.SmoothStep(reach,.2f,.65f),1),q2);
            Vector3 bend=BendDirection(d,MeleeMath.Unit(pole));float half=dd/2,outward=(float)Math.Sqrt(Math.Max(0,THIGH*THIGH-half*half));
            Vector3 u=Vector3.Normalize(d*half+bend*outward);SetQ(hip,MeleeMath.FromTo(new Vector3(0,-1,0),u));
            Vector3 f=Vector3.Transform(Vector3.Normalize(d*dd-u*THIGH),MeleeMath.Inverse(Q[hip]));SetQ(knee,MeleeMath.FromTo(new Vector3(0,-1,0),f));
            Slerp(ankle,MeleeMath.Inverse(q*Q[hip]*Q[knee])*MeleeMath.Euler(heel,yaw,0),weight);
            SetE(toe,-heel*.45f*weight,E[toe].Y,E[toe].Z);
        }

        // ---- motion-quality.ts leg solves ----
        public void PelvisFrame(out Quaternion pq,out Vector3 pp){pq=Q[CapturedRig.Body]*Q[CapturedRig.Pelvis];pp=Vector3.Transform(P[CapturedRig.Pelvis],Q[CapturedRig.Body])+P[CapturedRig.Body];}
        public Vector3 HipInGroup(bool left){PelvisFrame(out var pq,out var pp);return Vector3.Transform(P[Hip(left)],pq)+pp;}
        // Two-bone leg in group space: the knee hinges toward the foot heading, the foot takes a group heading/pitch or `orient`.
        public void SolveLeg(bool left,Vector3 target,float yaw,float pitch,float toe,Quaternion? orient=null,Vector3? knee0=null)
        {
            int hip=Hip(left),knee=hip+1,ankle=hip+2,toeJ=hip+3;PelvisFrame(out var pq,out var pp);Vector3 h=Vector3.Transform(P[hip],pq)+pp;
            Vector3 dir=target-h;float d=dir.Length();if(d<1e-6f){dir=new Vector3(0,-1,0);d=.1f;}else dir/=d;d=MeleeMath.Clamp(d,.1f,.497f);
            Vector3 fwd=Vector3.Transform(MeleeMath.Z,pq),pole=knee0.HasValue?knee0.Value-h:new Vector3((float)Math.Sin(yaw),0,(float)Math.Cos(yaw))*.7f+fwd*.3f;
            Vector3 perp=pole-dir*Vector3.Dot(pole,dir);if(perp.LengthSquared()<1e-8f)perp=fwd-dir*Vector3.Dot(fwd,dir);perp=Vector3.Normalize(perp);
            float half=d/2;Vector3 kn=h+dir*half+perp*(float)Math.Sqrt(Math.Max(0,.0625f-half*half));
            Vector3 t=Vector3.Normalize(kn-h),y=-t,z=Vector3.Normalize(perp-t*Vector3.Dot(perp,t)),x=Vector3.Cross(y,z);
            Quaternion qh=MeleeMath.Basis(x,y,z);SetQ(hip,MeleeMath.Inverse(pq)*qh);
            Vector3 sh=Vector3.Transform(h+dir*d-kn,MeleeMath.Inverse(qh));SetQ(knee,MeleeMath.AxisAngle(MeleeMath.X,(float)Math.Atan2(-sh.Z,-sh.Y)));
            Quaternion qf=orient??MeleeMath.EulerYXZ(pitch,yaw,0);
            SetQ(ankle,MeleeMath.Inverse(qh*Q[knee])*qf);if(!orient.HasValue)SetE(toeJ,toe,0,0);
        }
        // Current ankle position, foot orientation and knee position in group space.
        public Vector3 ReadAnkle(bool left,out Quaternion q,out Vector3 knee)
        {
            int hip=Hip(left);PelvisFrame(out var pq,out var pp);q=pq*Q[hip];Vector3 o=Vector3.Transform(P[hip],pq)+pp;
            o+=Vector3.Transform(P[hip+1],q);knee=o;q*=Q[hip+1];o+=Vector3.Transform(P[hip+2],q);q*=Q[hip+2];return o;
        }
        public Vector3 AnkleInGroup(bool left)=>ReadAnkle(left,out _,out _);
        // layerLeg (no pole): a leg solved exactly as the gait solves its own, marked layered for the ground pass.
        public void LayerLeg(bool left,Vector3 target,float yaw,float pitch,float toe){SolveLeg(left,target,yaw,pitch,toe);LegsLayered=true;}
        // holdAnkles: keep both ankles (and foot orientation) where they are while `change` moves the body.
        public void HoldAnkles(Action change,float weight)
        {
            if(weight<=1e-4f){change();return;}
            var hold=new Vector3[2];for(int i=0;i<2;i++)hold[i]=ReadAnkle(i==0,out _,out _);change();
            var keep=new Quaternion[4];
            for(int i=0;i<2;i++)
            {
                int hip=Hip(i==0);ReadAnkle(i==0,out Quaternion aq,out Vector3 k);if(weight<1)for(int b=0;b<4;b++)keep[b]=Q[hip+b];
                hold[i].Y=Math.Max(hold[i].Y,FLOOR);SolveLeg(i==0,hold[i],0,0,0,aq,k); // the changed leg's foot orientation and knee
                if(weight<1)for(int b=0;b<4;b++)SetQ(hip+b,MeleeMath.Slerp(keep[b],Q[hip+b],weight));
            }
        }
    }
}
