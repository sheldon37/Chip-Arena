using System;

namespace ChipArena.UnityConversion
{
    /// <summary>One melee Q/E cue: a bank key (LocalAssets) and today's built-in fallback clip (Resources/Audio), its gain.</summary>
    public struct NativeKitSound{public string Key,Clip;public float Gain;}

    /// <summary>
    /// The melee Q/E kit's audio routing onto existing NativeSoundBank keys, each with a built-in clip that already ships
    /// (sword, daggers, shield, greatsword, dash, demon-step, jump, stone, metal, parry, explosion), so the kit is heard with or
    /// without the owner's LocalAssets. Two sources, both replicated: the kit events (AbilityCast, AbilityHit, Teleport,
    /// Recast, Mark, Absorb, Bounce, Land, FissureStart with the ability id in Detail) and the cast's release moment
    /// (MeleeKit.Release, the dash, throw, slash or leap leaving), which no event marks. No new audio files.
    /// </summary>
    public static class NativeMeleeKitSounds
    {
        public const int Max=2;
        private static int S(NativeKitSound[] o,int n,string key,string clip,float gain){if(n<o.Length)o[n]=new NativeKitSound{Key=key,Clip=clip,Gain=gain};return Math.Min(o.Length,n+1);}
        /// <summary>The release moment of a cast (recast: the Skyfall plunge or the Kunai arrival).</summary>
        public static int Release(string kind,bool recast,NativeKitSound[] o)
        {
            int n=0;
            switch(kind)
            {
                case "flash-step":n=S(o,n,"move.roll","dash",.55f);n=S(o,n,"melee.sword.swing","sword",.4f);break;
                case "rising-dragon":n=recast?S(o,n,"melee.sword.swing","sword",.55f):S(o,n,"melee.sword.swing","sword",.6f);break;
                case "shadow-step":break;// the Teleport event carries the blink
                case "kunai-mark":n=recast?S(o,n,"melee.daggers.swing","daggers",.5f):S(o,n,"melee.daggers.throw","daggers",.6f);break;
            }
            return n;
        }
        /// <summary>A kit event's cues. targetMe: the local pilot is the event's target (it already hears its hurt).</summary>
        public static int Event(EventKind kind,string fx,float amount,bool targetMe,NativeKitSound[] o)
        {
            int n=0;string spec=MeleeKitMotion.Spec(fx);if(spec==null)return 0;
            switch(kind)
            {
                // The telegraph's tell: a short draw (the cast starts).
                case EventKind.AbilityCast:n=S(o,n,"melee.miss",null,.3f);break;
                // The weapon's own impact on top of the routed (proc) hit tick, for everyone but the victim.
                case EventKind.AbilityHit:if(!targetMe)n=S(o,n,NativeSoundCues.BodyImpact(spec),spec,.55f);if(fx=="bulwark-charge"||fx=="earthsplitter")n=S(o,n,"impact.ground","stone",.4f);break;
                case EventKind.Teleport:n=S(o,n,"magic.blink-strike",fx=="shadow-step"?"demon-step":"dash",.55f);break;
                case EventKind.Recast:n=fx=="rising-dragon"?S(o,n,"move.jump","jump",.5f):S(o,n,"magic.blink-strike","demon-step",.45f);break;
                case EventKind.Mark:if(amount>0)n=S(o,n,"gun.orbital.mark","metal",.35f);break;
                case EventKind.Land:n=S(o,n,"blast.quake","stone",fx=="wolf-knight"?.75f:.6f);n=S(o,n,"move.land","explosion",fx=="wolf-knight"?.35f:.25f);break;
            }
            return n;
        }
    }
}
