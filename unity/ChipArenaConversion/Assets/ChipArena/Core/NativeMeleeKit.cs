using System;

namespace ChipArena.UnityConversion
{
    // Authority-owned state. The snapshot copy is also the late-join / killcam / presentation contract.
    [Serializable] public sealed class NativeMeleeKit
    {
        public string Kind,Phase,RecastKind;
        public float At=-100,Release,Until,RecoverUntil,RecoverAt=-1,ShieldUntil,Yaw,RecastUntil;
        public int Target,MarkTarget,Absorbed,Tick;
        public float MarkUntil,AfterimageUntil,EmpowerUntil,RisingCooldownAt;
        public V3 Afterimage,ShieldPosition,FissureStart,FissureDirection;
        public int[] ShieldChain=Array.Empty<int>();
        public bool ShieldOut,SuperArmour;
        public float FissureAt,FissureUntil,FissureSpeed,FissureLength;
        public NativeMeleeKit ViewCopy(){var copy=(NativeMeleeKit)MemberwiseClone();copy.ShieldChain=(int[])(ShieldChain??Array.Empty<int>()).Clone();return copy;}
        public bool Valid()
        {
            bool F(float v)=>MathUtil.Finite(v)&&Math.Abs(v)<10000000;
            bool V(V3 v)=>F(v.x)&&F(v.y)&&F(v.z)&&v.Length<100000;
            bool S(string v)=>v==null||v.Length<=32;
            return S(Kind)&&S(Phase)&&S(RecastKind)&&F(At)&&F(Release)&&F(Until)&&F(RecoverUntil)&&F(RecoverAt)&&F(ShieldUntil)&&F(Yaw)&&F(RecastUntil)&&F(RisingCooldownAt)&&F(MarkUntil)&&F(AfterimageUntil)&&F(EmpowerUntil)&&F(FissureAt)&&F(FissureUntil)&&F(FissureSpeed)&&F(FissureLength)&&V(Afterimage)&&V(ShieldPosition)&&V(FissureStart)&&V(FissureDirection)&&Absorbed>=0&&Absorbed<=3&&Tick>=0&&Tick<=NativeWhirlwindRules.MaxTicks&&(ShieldChain==null||ShieldChain.Length<=3)&&
                (Kind!="whirlwind"||Release>=At&&Until>=Release&&RecoverUntil>=Until&&(Phase=="windup"||Phase=="active"||Phase=="recovery"));
        }
    }
    public static class NativeMeleeKitRules
    {
        // Low ballistic arc to the locked body point. Unreachable targets keep their true direction;
        // do not invent speed, remove gravity or steer the projectile after release.
        public static V3 KunaiAim(V3 origin,V3 target,float speed,float gravity)
        {
            var delta=target-origin;float horizontal=(float)Math.Sqrt(delta.x*delta.x+delta.z*delta.z);
            if(horizontal<.001f||speed<=0||gravity<=0)return delta.Normalized;
            double squared=(double)speed*speed,disc=squared*squared-gravity*(gravity*horizontal*horizontal+2*delta.y*squared);
            if(disc<0)return delta.Normalized;
            return GunRules.Direction((float)(Math.Atan2(delta.x,delta.z)*180/Math.PI),GunRules.BallisticPitch(horizontal,delta.y,speed,gravity));
        }
        public static float Windup(ContentAbility ability,ChipStats stats)=>ability.id=="whirlwind"?NativeWhirlwindRules.Windup:Math.Max(.15f,NativeAbilityRules.CastTime(ability.windup,stats));
        public static float End(NativeMeleeAction action)=>action==null?0:Math.Max(action.Until,action.RecoverUntil);
        public static float ChainAt(NativeMeleeSwing swing)=>swing.ChainAt>0?swing.ChainAt:swing.HitUntil+(swing.Until-swing.HitUntil)*.5f;
        public static bool BufferOpen(NativeMeleeSwing swing,float now)=>swing!=null&&swing.Remaining==0&&now>=swing.HitAt&&now<swing.Until;
        public static bool ChainReady(NativeMeleeSwing swing,float now)=>BufferOpen(swing,now)&&now>=ChainAt(swing);
        public static bool LightArmour(NativeMeleeKit kit,float now,float damage)=>kit!=null&&kit.SuperArmour&&now>=kit.Release&&now<kit.Until&&damage<25;
    }
}
