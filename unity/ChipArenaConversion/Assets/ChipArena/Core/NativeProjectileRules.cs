using System;
namespace ChipArena.UnityConversion
{
    public static class NativeProjectileRules
    {
        public static float Damage(float amount,ChipStats stats,float health,float maxHealth,float distance,bool melee,float behind,float chaosRoll,float criticalRoll)
        {
            amount*=1+(health<maxHealth*.3f?(float)stats["execute"]:0)+(distance<5?(float)stats["closeDamage"]:0)+(distance>=20?(float)stats["farDamage"]:0)+(melee&&behind<-.35f?(float)stats["backstab"]:0);
            if(stats["chaos"]>0)amount*=.5f+chaosRoll*2;if(criticalRoll<stats["critical"])amount*=2;return Math.Min(1000,amount);
        }
        public static int Volley(ChipStats stats,bool pong)=>pong?1:1+(int)stats["extraProjectiles"];
        public static float Fan(int index,int count,float degrees)=>count>1?(index/(float)(count-1)-.5f)*degrees:0;
        public static float Falloff(float distance,float radius)=>distance>=radius+.4f?0:1-MathUtil.Clamp(distance/(radius+.4f),0,.85f);
        public static V3 Steer(V3 velocity,V3 toward,float radians)
        {
            float speed=velocity.Length;if(speed==0||toward.Length==0||radians<=0)return velocity;
            var a=velocity.Normalized;var b=toward.Normalized;float angle=(float)Math.Acos(MathUtil.Clamp(V3.Dot(a,b),-1,1));if(angle<=radians)return b*speed;
            var axis=Cross(a,b);if(axis.Length<1e-7f)axis=Cross(a,Math.Abs(a.y)<.9f?new V3(0,1,0):new V3(1,0,0));axis=axis.Normalized;
            return (a*(float)Math.Cos(radians)+Cross(axis,a)*(float)Math.Sin(radians)).Normalized*speed;
        }
        private static V3 Cross(V3 a,V3 b)=>new V3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
        public static V3 Return(V3 velocity,V3 toward,float radians)
        {
            float speed=Math.Min(60,velocity.Length),yaw=(float)Math.Atan2(velocity.x,velocity.z),target=(float)Math.Atan2(toward.x,toward.z),delta=(float)Math.Atan2(Math.Sin(target-yaw),Math.Cos(target-yaw));
            yaw+=MathUtil.Clamp(delta,-radians,radians);float pitch=(float)Math.Atan2(velocity.y,Math.Sqrt(velocity.x*velocity.x+velocity.z*velocity.z)),goal=(float)Math.Atan2(toward.y,Math.Sqrt(toward.x*toward.x+toward.z*toward.z));pitch+=MathUtil.Clamp(goal-pitch,-radians,radians);
            return new V3((float)Math.Sin(yaw)*(float)Math.Cos(pitch)*speed,(float)Math.Sin(pitch)*speed,(float)Math.Cos(yaw)*(float)Math.Cos(pitch)*speed);
        }
    }
}
