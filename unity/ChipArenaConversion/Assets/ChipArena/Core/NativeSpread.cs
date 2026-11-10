using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The guns' spread (owner 2026-11-10: "I think we need a reactive crosshair and spread. Currently I think we don't have initial spread on
    /// weapons"). Each gun fires inside a cone: its hip cone at rest, opened by moving (by the share of a run) and by being in the air, and by
    /// bloom: every shot adds its kick (up to a cap), which starts to recover a moment after the last shot. Aiming down the sights narrows the
    /// whole cone by the aim row's spread as it settles (the rifle's scope to nothing). The rules (Runtime NativeProjectiles: a shot lands
    /// uniformly inside the cone) and the crosshair (Runtime NativeFightHud: its arms sit on the cone's edge) read the same numbers. Degrees,
    /// half-angles. The pistol, the machine gun and the rifle; the bow, the Bombadier, the fan and the rest keep their own.
    /// </summary>
    public static class NativeSpread
    {
        public readonly struct Profile
        {
            /// <summary>Hip: the cone at rest. Move: added at a full run. Air: added off the ground. Kick: each shot's bloom, up to Cap.
            /// Delay: how long the bloom holds after a shot. Recover: degrees a second it then falls.</summary>
            public readonly float Hip,Move,Air,Kick,Cap,Delay,Recover;
            public Profile(float hip,float move,float air,float kick,float cap,float delay,float recover){Hip=hip;Move=move;Air=air;Kick=kick;Cap=cap;Delay=delay;Recover=recover;}
        }
        // The pistol (a shot every .42 s) blooms a little when it is fired as fast as it goes; the machine gun (.085 s) climbs to its cap over a
        // second of fire and settles in about half a second; the rifle (1.15 s) is wide from the hip and exact through the scope.
        public static readonly Profile Pistol=new Profile(1f,1.2f,2.5f,.9f,3f,.3f,5f),MachineGun=new Profile(1.6f,1.4f,3f,.22f,3.2f,.15f,6f),Rifle=new Profile(2.5f,2f,3.5f,2.5f,4f,.2f,3f);
        /// <summary>A full run (Runtime NativeArena: 6.8 m/s at speed 1).</summary>
        public const float RunSpeed=6.8f;
        public static bool Has(string spec)=>spec=="pistol"||spec=="machinegun"||spec=="rifle";
        public static Profile Of(string spec)=>spec=="pistol"?Pistol:spec=="machinegun"?MachineGun:spec=="rifle"?Rifle:default;
        /// <summary>The bloom left `since` seconds after it was last set.</summary>
        public static float Decay(float bloom,float since,in Profile p)=>Math.Max(0,bloom-p.Recover*Math.Max(0,since-p.Delay));
        /// <summary>The bloom after a shot.</summary>
        public static float Kick(float bloom,in Profile p)=>Math.Min(p.Cap,Math.Max(0,bloom)+p.Kick);
        /// <summary>The aim's narrowing: 1 from the hip, easing to the aim row's spread over its settle time.</summary>
        public static float Aim(bool aiming,double adsSpread,double settle,float since)=>aiming?1+((float)adsSpread-1)*Math.Max(0,Math.Min(1,since/(float)Math.Max(.01,settle))):1;
        /// <summary>The cone now: the hip cone, the move's share of a run, the air, the bloom, narrowed by the aim.</summary>
        public static float Cone(in Profile p,float moveShare,bool airborne,float bloom,float aim)=>(p.Hip+p.Move*Math.Max(0,Math.Min(1,moveShare))+(airborne?p.Air:0)+Math.Max(0,bloom))*Math.Max(0,aim);
        /// <summary>The cone as a shot's spread value (NativeProjectiles: the offset off the aim is up to half of it): twice the cone's tangent.</summary>
        public static double Value(float cone)=>2*Math.Tan(Math.Max(0,Math.Min(30,cone))*Math.PI/180);
        /// <summary>A point uniform in the unit disc from two uniform draws (the shot's offset off the aim, in the cone's radius).</summary>
        public static void Disc(double u,double v,out float x,out float y){double r=Math.Sqrt(Math.Max(0,Math.Min(1,u))),a=2*Math.PI*v;x=(float)(r*Math.Cos(a));y=(float)(r*Math.Sin(a));}
        /// <summary>The cone's edge on the screen: pixels from the centre for a camera of this vertical field of view and height.</summary>
        public static float Pixels(float cone,float fov,float height)=>(float)(Math.Tan(Math.Max(0,Math.Min(30,cone))*Math.PI/180)/Math.Tan(Math.Max(1,Math.Min(170,fov))*Math.PI/360)*height*.5);
    }
}
