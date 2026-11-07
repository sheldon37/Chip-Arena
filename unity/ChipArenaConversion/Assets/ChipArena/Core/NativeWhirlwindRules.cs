using System;

namespace ChipArena.UnityConversion
{
    public static class NativeWhirlwindRules
    {
        public const float Windup=.3f,Duration=3,Recovery=.5f,MinimumSpin=.5f,Interval=.45f,Damage=8.5f,Radius=2.6f,MoveScale=.8f,Stagger=.12f;
        public const int MaxTicks=7;
        public static NativeMeleeAction Begin(float now,float yaw)
            =>new NativeMeleeAction{Kind="whirlwind",Fx="whirlwind",Phase="windup",At=now,Release=now+Windup,Until=now+Windup+Duration,RecoverAt=now+Windup+Duration,RecoverUntil=now+Windup+Duration+Recovery,Yaw=yaw};
        public static bool Active(NativeMeleeAction a,float now)=>a?.Fx=="whirlwind"&&now>=a.Release&&now<a.Until;
        public static bool Active(NativeMeleeKit k,float now)=>k?.Kind=="whirlwind"&&now>=k.Release&&now<k.Until;
        public static bool Busy(NativeMeleeKit k,float now)=>k?.Kind=="whirlwind"&&now<k.RecoverUntil;
        public static bool CanRecast(NativeMeleeAction a,float now)=>Active(a,now)&&now>=a.Release+MinimumSpin;
        public static void Advance(NativeMeleeAction a,float now)
        {a.Released=now>=a.Release;a.Phase=now<a.Release?"windup":now<a.Until?"active":"recovery";}
        public static bool Recast(NativeMeleeAction a,float now)
        {if(!CanRecast(a,now))return false;a.Until=a.RecoverAt=now;a.RecoverUntil=now+Recovery;a.Phase="recovery";return true;}
        // Tick counts emitted pulses, including misses. A target can be hit once per pulse, never more than seven times.
        public static bool NextTick(NativeMeleeAction a,float now)
        {if(!Active(a,now)||a.Tick>=MaxTicks||now+1e-5f<a.Release+a.Tick*Interval)return false;a.Tick++;return true;}
        public static bool Contact(V3 delta,float reach=1)=>Math.Abs(delta.y)<=1.8f&&delta.x*delta.x+delta.z*delta.z<=Radius*Radius*reach*reach;
    }
}
