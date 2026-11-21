using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Hulk's roar and the horde's hinged jaws (owner 2026-10-13: "Add a Hulk zombie too, similar to brute but much bigger
    /// model size, huge knockback, very slow, very high hp"; and the spitter's "windup animation"). Presentation only, a pure
    /// function of what every peer holds (the add's id seed, replicated ActiveAt and LastAttack, the clock), so host, guests and the
    /// killcam roar at the same moments: the first just after it turns active (out of its jump-in or its breach), then every
    /// 12-16 s, never through a blow (a wind-up cuts it off).
    /// </summary>
    public static class NativeHordeRoar
    {
        public const float Seconds=1.9f,Period=12f,Jitter=4f;
        private static float Sm(float x)=>MeleeMath.Smooth(x);
        /// <summary>The roar's age (s) at `time` for a Hulk active since `activeAt` (NaN: not roaring), cut by an attack in progress
        /// (`attackAge` from LastAttack, NaN none, until `recover` + .25 s).</summary>
        public static float Age(int kind,uint seed,float time,float activeAt,float attackAge,float recover)
        {
            if(kind!=NativeHordeKinds.Hulk||float.IsNaN(time)||float.IsNaN(activeAt))return float.NaN;
            if(!float.IsNaN(attackAge)&&attackAge>=0&&attackAge<recover+.25f)return float.NaN;
            float t=time-activeAt-.1f;if(t<0)return float.NaN;
            int k=(int)Math.Floor(t/Period);
            for(int j=k;j>=Math.Max(0,k-1);j--){float start=j*Period+(j==0?0:Jitter*NativeHordeKinds.Hash(seed,700+j));float age=t-start;if(age>=0&&age<Seconds)return age;}
            return float.NaN;
        }
        /// <summary>The roar's weight over the body (0..1): in over .25 s, held, out over the last .45 s.</summary>
        public static float Weight(float age)=>float.IsNaN(age)||age<0||age>=Seconds?0:Sm(age/.25f)*(1-Sm((age-(Seconds-.45f))/.45f));
    }

    /// <summary>
    /// The hinged jaws (the spitter's and the Hulk's are their own model, turned about the skull's hinge;
    /// NativeHordeModels.JawHinge): how far open (rad past the model's slack hang) at a frame.
    /// </summary>
    public static class NativeHordeJaw
    {
        private static float Sm(float x)=>MeleeMath.Smooth(x);
        /// <summary>Kinds whose jaw opens on its own model (the others' slack jaws are part of the head).</summary>
        public static bool Hinged(int kind)=>kind==NativeHordeKinds.Spitter||kind==NativeHordeKinds.Hulk;
        /// <summary>
        /// The jaw's opening: the spitter's unhinges through its wind-up (wide at the strike, held as the glob leaves, then shut);
        /// cancelled (`cancelAge` s since the interrupt, NaN none) it snaps half shut. The Hulk's gapes with its roar and bellows
        /// through its smash's wind-up. A slow idle chew otherwise.
        /// </summary>
        public static float Open(int kind,NativeHordeKinds.Attack attack,float age,float strike,float cancelAge,float roarAge,float time,uint seed)
        {
            float chew=.06f*(.5f+.5f*(float)Math.Sin(time*2.1f+NativeHordeKinds.Hash(seed,5)*6.28f));
            if(kind==NativeHordeKinds.Spitter)
            {
                float open=0;
                if(!float.IsNaN(age)&&age>=0&&attack==NativeHordeKinds.Attack.Spit)
                {
                    open=age<strike?.95f*Sm(age/Math.Max(.05f,strike*.8f))+.05f*(float)Math.Sin(age*40)*Sm(age/strike):.95f*(1-Sm((age-strike-.18f)/.3f));
                    if(!float.IsNaN(cancelAge)&&cancelAge>=0)open*=1-.6f*Sm(cancelAge/.12f);
                }
                return Math.Max(chew,open);
            }
            if(kind==NativeHordeKinds.Hulk)
            {
                float open=float.IsNaN(roarAge)?0:1.05f*NativeHordeRoar.Weight(roarAge)+.06f*(float)Math.Sin(roarAge*29)*NativeHordeRoar.Weight(roarAge);
                if(!float.IsNaN(age)&&age>=0&&age<strike+.4f&&attack==NativeHordeKinds.Attack.Smash)open=Math.Max(open,age<strike?.55f*Sm(age/strike):.55f*(1-Sm((age-strike)/.4f)));
                return Math.Max(chew,open);
            }
            return 0;
        }
    }
}
