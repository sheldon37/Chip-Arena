using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // Source: client/siren/fx.ts updateGrabs. Only the visible rig is moved; the held collider stays authoritative.
    public static class NativeSirenVictimPose
    {
        public static bool Active(Actor a,float time)=>a!=null&&a.Alive&&a.GrabOwner>0&&time<a.GrabCrushAt&&time<a.GrabUntil;
        private static float Smooth(float x){x=MathUtil.Clamp(x,0,1);return x*x*(3-2*x);}
        public static float Lift(float age)=>Smooth(age/.5f);
        public static float Burn(float age)=>(float)NativeSirenFx.Burn(age);
        public static void Sample(CapturedRig.Pose pose,float time,float caughtAt,float crushAt)
        {
            pose.Reset();float age=Math.Max(0,time-caughtAt),k=time*9;
            float struggle=Smooth((age-.15f)/.4f)*(1-Smooth((time-(crushAt-.3f))/.3f)*.6f),burn=Burn(age);
            float Sin(float x)=>(float)Math.Sin(x);
            void R(int b,float x,float y=0,float z=0)=>pose.Rotation[b]=CapturedRig.Euler(x,y,z);
            R(CapturedRig.Body,.12f*Sin(k*.7f)*struggle,0,.1f*Sin(k*.5f)*struggle);
            R(CapturedRig.Spine,-.15f+.08f*Sin(k)*struggle);R(CapturedRig.Chest,-.1f,0,.06f*Sin(k*1.3f)*struggle);
            R(CapturedRig.Neck,-.25f);R(CapturedRig.Head,-.35f+.2f*burn,.25f*Sin(k*.8f)*struggle*(1-burn));
            R(CapturedRig.LArm,-2.5f+.25f*Sin(k*1.7f)*struggle,0,.25f);R(CapturedRig.RArm,-2.4f+.25f*Sin(k*1.9f+1)*struggle,0,-.25f);
            R(CapturedRig.LElbow,-1.6f+.3f*Sin(k*2.1f)*struggle);R(CapturedRig.RElbow,-1.7f+.3f*Sin(k*2.3f)*struggle);
            R(CapturedRig.LHip,-.35f+.75f*Sin(k*1.1f)*struggle,0,.08f);R(CapturedRig.RHip,-.35f+.75f*Sin(k*1.1f+2.2f)*struggle,0,-.08f);
            R(CapturedRig.LKnee,.5f+.5f*Math.Max(0,Sin(k*1.1f+1))*struggle);R(CapturedRig.RKnee,.5f+.5f*Math.Max(0,Sin(k*1.1f+3))*struggle);
        }
    }

    // Source: client/pve/downed.ts. Shared by the body scan, rebuilding voxels and the standing pose.
    public static class NativeReviveRules
    {
        public const float Scan=.8f,Stand=.6f,Seconds=Scan+Stand,Settle=.05f,Lead=1,PlaceRadius=1.6f,Dissolve=.7f;
        public static float Clamp(float x)=>MathUtil.Clamp(x,0,1);
        public static float Smooth(float x){x=Clamp(x);return x*x*(3-2*x);}
        public static bool Active(float time,float upAt)=>upAt>=0&&MathUtil.Finite(upAt)&&time-upAt>-Lead&&time-upAt<Seconds;
        public static float Front(float age)=>Clamp(age/(Scan*.8f));
        public static float Standing(float age)=>Clamp((age-Scan)/Stand);
        public static float Glow(float age)=>age<Scan?Smooth(age/.15f):1-Smooth(Standing(age));
        public static bool InPlace(V3 body,V3 at)=>Math.Sqrt((body.x-at.x)*(body.x-at.x)+(body.z-at.z)*(body.z-at.z))<=PlaceRadius;
        public static float Arrival(float coordinate)=>.1f+Clamp(coordinate)*Scan*.8f;
        public static float Flight(float age,float coordinate)=>Clamp((age-(Arrival(coordinate)-.3f))/.3f);
        public static Vector3 Scatter(uint seed,int index)
        {
            uint s=seed^unchecked((uint)(index+1)*0x9e3779b1u);
            double Next(){s=unchecked(s*1664525u+1013904223u);return s/4294967296.0;}
            double a=Next()*Math.PI*2,u=Next()*2-1,k=.4+.8*Next(),h=Math.Sqrt(1-u*u);
            return new Vector3((float)(Math.Cos(a)*h*k),(float)(Math.Abs(u)*k*.8+.15),(float)(Math.Sin(a)*h*k));
        }
    }
}
