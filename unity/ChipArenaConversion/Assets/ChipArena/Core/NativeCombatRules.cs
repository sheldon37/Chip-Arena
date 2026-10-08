using System;
namespace ChipArena.UnityConversion
{
    public static class NativeCombatRules
    {
        public static bool Enemy(NativeSeat a,NativeSeat b,bool teams)=>a!=null&&b!=null&&a.connected&&b.connected&&a.id!=b.id&&(!teams||a.team!=b.team);
        public static bool CanHit(NativeSeat a,NativeSeat b,bool teams,bool friendlyFire)=>a!=null&&b!=null&&a.connected&&b.connected&&a.id!=b.id&&(friendlyFire||!teams||a.team!=b.team);
        public static bool Headshot(V3 position,float yaw,V3 origin,V3 velocity,float radius,bool down)
        {
            if(down||velocity.Length<.00001f)return false;var direction=velocity.Normalized;var center=position+new V3(0,.64f,0);float along=V3.Dot(center-origin,direction);if(along<0)return false;
            var closest=origin+direction*along-center;float angle=yaw*(float)Math.PI/180,pad=Math.Min(.06f,Math.Max(0,radius));
            float x=closest.x*(float)Math.Cos(angle)-closest.z*(float)Math.Sin(angle),z=closest.x*(float)Math.Sin(angle)+closest.z*(float)Math.Cos(angle);
            return Math.Abs(closest.y)<=.17f+pad&&Math.Abs(x)<=.17f+pad&&Math.Abs(z)<=.17f+pad;
        }
        public static float Overtime(double elapsed,double limit,bool cooperative,bool sandbox)=>!cooperative&&!sandbox&&elapsed>limit?18f/60:0;
    }
}
