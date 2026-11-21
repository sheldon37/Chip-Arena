using System;

namespace ChipArena.UnityConversion
{
    // Owner's 2026-10-12 feel values. Keep NativeMeleeRules.Begin as the pinned source-parity baseline.
    public static class NativeMeleeFeel
    {
        public const float BufferSeconds=.4f,ComboPause=.35f;
        public static float ChainSeconds(string spec)=>spec=="sword"?.50f:spec=="daggers"?.32f:spec=="shield"?.60f:.95f;
        public static float TurnRate(string spec)=>spec=="sword"?720:spec=="daggers"?540:spec=="shield"?450:300;
        public static float DefaultStep(string spec)=>spec=="sword"?.9f:spec=="daggers"?.8f:spec=="shield"?.75f:1;
        public static float StepCap(string spec,bool chained)=>spec=="greatsword"?(chained?1.6f:2.4f):spec=="sword"?(chained?1.4f:2.2f):(chained?1.2f:2);
        public static float Distance(string spec,bool chained,float targetDistance)=>Math.Min(StepCap(spec,chained),Math.Max(0,targetDistance-1.15f));
        public static float Cost(ContentWeapon weapon,ChipStats stats)=>(float)((weapon.cost+stats["extraStrikes"]*2)*stats["resourceCost"]);
        public static NativeMeleeSwing Begin(ContentWeapon weapon,ChipStats stats,float at,int index,float yaw)
        {
            var s=NativeMeleeRules.Begin(weapon,stats,at,index,yaw);
            s.Distance=DefaultStep(weapon.id);s.ChainAt=Math.Max(s.HitUntil,Math.Min(s.Until,at+ChainSeconds(weapon.id)/(float)stats["attackRate"]));return s;
        }
        public static float Turn(float yawRadians,float desiredDegrees,string spec,float dt)
        {
            float yaw=yawRadians*180/(float)Math.PI,delta=(desiredDegrees-yaw)%360;
            if(delta>180)delta-=360;if(delta<-180)delta+=360;
            return (yaw+MathUtil.Clamp(delta,-TurnRate(spec)*dt,TurnRate(spec)*dt))*(float)Math.PI/180;
        }
        public static bool SprintAttack(InputFrame input,bool sprinting)=>sprinting||input.Sprint&&V3.Dot(new V3(input.X,0,input.Z),V3.Forward(input.Yaw))>=.6f;
    }
}
