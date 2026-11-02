using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // Claude's first-person-view.ts tuning, expressed in Unity's forward +Z camera frame.
    public static class NativeFirstPersonPose
    {
        public const float Scale=.78f,StaffScale=.85f,StaffRoll=20;
        // The visual body's view model (owner 2026-11-01: "Also need to fix first person, weapons looks super small"): with a
        // life-size weapon model in hand, its forearms, hands and that model draw larger about the right hand's weapon frame
        // (ModelScale: a gun ModelView, a blade, bow or staff ModelBlade, as their models already reach the game's tip), and the
        // hold rises into view (ModelLift, camera frame, m; aiming down the sights still centres the sight). The hand stays on
        // the weapon frame; the weapon grows out of it, the forearm back off screen. Presentation only: aim, muzzle, reach and
        // hit volumes are the game's.
        public const float ModelView=1.7f,ModelBlade=1.3f;
        public static readonly Vector3 ModelPistolLift=new Vector3(-.02f,.10f,0),ModelGunLift=new Vector3(0,.05f,0),ModelBladeLift=new Vector3(0,0,0);
        public static float ModelScale(bool gun)=>gun?ModelView:ModelBlade;
        public static Vector3 ModelLift(bool gun,bool pistol,bool blade)=>gun?pistol?ModelPistolLift:ModelGunLift:blade?ModelBladeLift:Vector3.Zero;
        // Captured native torso sits lower than the source rig; a 5 cm lift keeps the crystal visible during casts.
        public const float NativeRigLift=.05f;
        public static readonly Vector3 MagicOffset=new Vector3(.20f,-.50f+NativeRigLift,.20f);
        // The native bone blend sweeps upward between carry and guard; dip the presentation rig through that transition.
        public static Vector3 MagicViewOffset(float guard)=>MagicOffset-Vector3.UnitY*(.24f*4*guard*(1-guard));
        public static Vector3 EulerXYZ(Quaternion q)
        {
            float m13=2*(q.X*q.Z+q.Y*q.W),y=(float)Math.Asin(MathUtil.Clamp(m13,-1,1));
            if(Math.Abs(m13)<.9999999f)return new Vector3((float)Math.Atan2(2*(q.X*q.W-q.Y*q.Z),1-2*(q.X*q.X+q.Y*q.Y)),y,(float)Math.Atan2(2*(q.Z*q.W-q.X*q.Y),1-2*(q.Y*q.Y+q.Z*q.Z)));
            return new Vector3((float)Math.Atan2(2*(q.Y*q.Z+q.X*q.W),1-2*(q.X*q.X+q.Z*q.Z)),y,0);
        }
        private static readonly int[] SettleBones={CapturedRig.Body,CapturedRig.Pelvis,CapturedRig.Spine};
        public static void SettleTorso(CapturedRig.Pose pose)
        {
            foreach(int bone in SettleBones)
            {var e=EulerXYZ(pose.Rotation[bone]);pose.Rotation[bone]=CapturedRig.Euler(e.X,e.Y*.2f,e.Z);}
            Vector3 p=pose.Position[CapturedRig.Body];pose.Position[CapturedRig.Body]=new Vector3(MathUtil.Clamp(p.X,-.03f,.03f),p.Y*.5f,MathUtil.Clamp(p.Z,-.03f,.03f));
            pose.World();
        }
        public static float StaffSize(float guard)=>StaffScale+(1-StaffScale)*MathUtil.Clamp(guard,0,1);
        public static Quaternion StaffRotation(Quaternion wrist,Vector3 cameraForward,float guard)=>Quaternion.CreateFromAxisAngle(cameraForward,StaffRoll*(1-MathUtil.Clamp(guard,0,1))*(float)Math.PI/180)*wrist;
        public static void Spring(ref float value,ref float velocity,float target,float omega,float zeta,float dt)
        {
            int steps=Math.Max(1,(int)Math.Ceiling(dt*240));float h=dt/steps;
            for(int i=0;i<steps;i++){velocity+=(-(value-target)*omega*omega-2*zeta*omega*velocity)*h;value+=velocity*h;}
        }
    }
    public sealed class NativeGuardMotion
    {
        private float at=-100,velocity;public float Weight {get;private set;}
        public float Sample(float time,bool guarding)
        {
            float dt=time-at,target=guarding?1:0,value=Weight;
            if(dt<0||dt>.35f){value=target;velocity=0;}
            else NativeFirstPersonPose.Spring(ref value,ref velocity,target,target>value?17:14,1,Math.Min(.05f,dt));
            at=time;return Weight=MathUtil.Clamp(value,0,1);
        }
    }
}
