using System;

namespace ChipArena.UnityConversion
{
    public sealed class NativeFanBurst
    {
        public float At;public int Limit,Shots;
        public bool Due(float time)=>Shots<Limit&&time+0.00001f>=At+Shots*NativeRangedRules.FanInterval;
    }
    public static class NativeRangedRules
    {
        public const int FanRounds=4;
        public const float FanInterval=.075f,FanDamage=.8f,FanSpread=2.2f;
        public const float OverdriveDuration=4,VentDuration=.8f,OverdriveRate=1.6f,OverdriveMove=.8f;
        public static bool Q(string id)=>id=="fan-hammer"||id=="overdrive";
        // Shared by the keyboard adapter and managed input regression checks.
        public static bool KeyboardQ(ConversionContent data,string spec,bool keyDown)=>keyDown&&(spec==CyberLordSpec.Id||data.Weapon(spec).@class=="magic"||NativeMeleeRules.Standard(spec)||Q(data.Ability(spec,"q")?.id)||data.Ability(spec,"q")?.id=="volley");
        public static bool Scope(string spec)=>spec=="rifle";
        public static NativeFanBurst Fan(float time,int ammo)=>new NativeFanBurst{At=time,Limit=Math.Min(FanRounds,Math.Max(0,ammo))};
        public static bool Overdrive(float time,float at,float until)=>until>at&&time>=at&&time<until;
        public static bool Vent(float time,float until,float ventUntil)=>ventUntil>until&&time>=until&&time<ventUntil;
        // Zeroes also represent old snapshots and a pilot that has never activated the buff.
        public static bool Valid(float at,float until,float ventUntil)=>MathUtil.Finite(at)&&MathUtil.Finite(until)&&MathUtil.Finite(ventUntil)&&at>=0&&until>=at&&ventUntil>=until;
        public static float Interval(double interval,double rate,bool overdrive)=>(float)(interval/rate)/(overdrive?OverdriveRate:1);
        // Overdrive keeps the base hip-fire cone, without accumulated bloom. Fan multiplies that cone.
        public static float Spread(double spread,bool fan)=>(float)spread*(fan?FanSpread:1);
    }
}
