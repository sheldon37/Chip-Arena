using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // Source draft-stage.ts pose/look and draft-timeline.ts timing, in source coordinates.
    public static class NativeDraftRules
    {
        public static float Reach(float age)=>MathUtil.Smooth((age-.14f)/.34f);
        public static float Fly(float age)=>MathUtil.Smooth((age-.10f)/.38f);
        public static float Fade(float age)=>MathUtil.Smooth((age-.40f)/.08f);
        // After the contact the source pulled the chip up to the chest (kept for the timing parity check); the native stage
        // plants it into the other wrist instead (NativeDraftPlant, which also sizes the held chip).
        public static float Pull(float age)=>MathUtil.Smooth((age-.48f)/.37f);
        public static void Pose(CapturedRig.Pose p,float time)
        {
            p.Reset();float b=(float)Math.Sin(time*1.65f),s=(float)Math.Sin(time*.72f),d=(float)Math.Sin(time*.41f+1.3f);
            p.Position[0]=new Vector3(0,.002f*b,0);p.Rotation[0]=CapturedRig.Euler(0,0,.006f*s);
            p.Rotation[2]=CapturedRig.Euler(-.015f+.006f*b);p.Rotation[3]=CapturedRig.Euler(-.045f+.014f*b,.015f*d,-.005f*s);p.Rotation[4]=CapturedRig.Euler(.04f);p.Rotation[5]=CapturedRig.Euler(.03f-.006f*b,.04f*d,.012f*s);
            p.Rotation[6]=CapturedRig.Euler(0,.06f,.025f+.012f*b);p.Rotation[10]=CapturedRig.Euler(0,-.06f,-.025f-.012f*b);p.Rotation[7]=CapturedRig.Euler(.04f,0,.09f+.006f*b);p.Rotation[11]=CapturedRig.Euler(.04f,0,-.09f-.006f*b);
            p.Rotation[8]=p.Rotation[12]=CapturedRig.Euler(-.24f);p.Rotation[9]=CapturedRig.Euler(0,.12f);p.Rotation[13]=CapturedRig.Euler(0,-.12f);p.Rotation[14]=CapturedRig.Euler(0,0,.05f);p.Rotation[18]=CapturedRig.Euler(0,0,-.05f);p.Rotation[15]=p.Rotation[19]=CapturedRig.Euler(.03f);p.Rotation[16]=p.Rotation[20]=CapturedRig.Euler(-.03f);
        }
        public static void Look(CapturedRig.Pose p,Vector3 target,float weight)
        {
            p.World();var eye=p.WorldPosition[5]+Vector3.UnitY*.165f;var delta=target-eye;float distance=Math.Max(.3f,delta.Length()),yaw=MathUtil.Clamp((float)Math.Atan2(delta.X,delta.Z),-1.05f,1.05f),pitch=MathUtil.Clamp((float)Math.Atan2(delta.Y,Math.Sqrt(delta.X*delta.X+delta.Z*delta.Z)),-.8f,.4f);
            target=eye+new Vector3((float)(Math.Sin(yaw)*Math.Cos(pitch)),(float)Math.Sin(pitch),(float)(Math.Cos(yaw)*Math.Cos(pitch)))*distance;
            Share(p,3,target,.36f*weight,0);Share(p,4,target,.42f*weight,0);Share(p,5,target,weight,.165f);
        }
        private static void Share(CapturedRig.Pose p,int bone,Vector3 target,float weight,float up)
        {p.World();var q=p.WorldRotation[bone];var direction=target-(p.WorldPosition[bone]+Vector3.Transform(Vector3.UnitY*up,q));if(direction.LengthSquared()<1e-8f)return;direction=Vector3.Normalize(direction);var change=Quaternion.Slerp(Quaternion.Identity,CapturedRig.FromTo(Vector3.Transform(Vector3.UnitZ,q),direction),weight);p.Rotation[bone]=Quaternion.Inverse(p.WorldRotation[CapturedRig.Bones[bone].Parent])*change*q;}
    }
}
