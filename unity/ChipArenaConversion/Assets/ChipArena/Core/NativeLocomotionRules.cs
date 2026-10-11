using System;

namespace ChipArena.UnityConversion
{
    // Shared host/prediction dimensions. Existing stairs reach 48 degrees; walls are never ground.
    public static class NativeLocomotionRules
    {
        public const float Radius=.42f,HalfHeight=.85f,Skin=.02f,StepHeight=.35f,GroundReach=.20f;
        public const float MinGroundY=.6427876f; // cos(50 degrees)
        public static bool Walkable(V3 normal)=>normal.y>=MinGroundY;
        // Preserve the requested horizontal speed, adding the rise/fall of the support plane.
        public static V3 AlongGround(V3 motion,V3 normal)=>Walkable(normal)?new V3(motion.x,-(motion.x*normal.x+motion.z*normal.z)/normal.y,motion.z):motion;
        public static bool Attach(bool grounded,bool attached,float separatingSpeed,bool jump,bool swimming,bool launched,float impulseY)
            =>grounded&&(attached||separatingSpeed<=1)&&!jump&&!swimming&&!launched&&Math.Abs(impulseY)<.01f;
        // A stationary wall/ramp stopping voluntary movement is not a new attack impulse. Only a moving dynamic
        // prop approaching the pilot can carry the solver's positive response into next tick's knockback.
        public static V3 ContactPush(V3 before,V3 after,V3 surface,V3 normal,bool dynamic)
        {
            if(!dynamic||V3.Dot(surface,normal)<=.1f)return default;
            float push=Math.Max(0,V3.Dot(after-before,normal));return new V3(normal.x*push,0,normal.z*push);
        }
    }
}
