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
                case "bulwark-charge":n=S(o,n,"move.roll","dash",.55f);break;
                case "aegis-ricochet":n=S(o,n,"melee.shield.throw","shield",.6f);break;
                case "earthsplitter":break;// FissureStart carries the slam
                case "whirlwind":n=S(o,n,"melee.greatsword.spin","greatsword",.6f);break;// the first pulse's sweep; Spin carries the rest
            }
            return n;
        }
        /// <summary>
        /// The Whirlwind's cues between the release and the stop, from the replicated clock: a whoosh each time the blade comes
        /// round the front (every pulse after the first, which the release carries) and, as the turn stops, the heavy blade's
        /// weight coming down. at: the cue's moment (each cue once; NativeAudio keys it by this). 0 when nothing is due within
        /// `fresh` seconds. Better cues for the owner's bank: a looping spin whoosh and a heavy metal scrape-stop.
        /// </summary>
        public static int Spin(Actor a,float time,float fresh,NativeKitSound[] o,out float at)
        {
            at=float.NaN;if(!MeleeKitMotion.Clock(a,time,out var c)||c.Kind!="whirlwind")return 0;int n=0;
            int k=MeleeKitMotion.Pulse(c,time,out float pulse);
            if(k>=1&&time-pulse<fresh){at=pulse;n=S(o,n,"melee.greatsword.swing","greatsword",.42f);return n;}
            if(c.Part==KitPart.Recovery){var w=MeleeKitMotion.Spin(c,time);if(time>=w.Stop&&time-w.Stop<fresh){at=w.Stop;n=S(o,n,"impact.ground","stone",.45f);n=S(o,n,"melee.greatsword.hit","greatsword",.22f);}}
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
                // The Whirlwind's early end has no tell of its own: its stop (Spin) is heard as the blade comes down.
                case EventKind.Recast:n=fx=="whirlwind"?0:fx=="rising-dragon"?S(o,n,"move.jump","jump",.5f):S(o,n,"magic.blink-strike","demon-step",.45f);break;
                case EventKind.Mark:if(amount>0)n=S(o,n,"gun.orbital.mark","metal",.35f);break;
                case EventKind.Absorb:n=S(o,n,"melee.shield.block","parry",.5f);break;
                case EventKind.Bounce:n=S(o,n,"impact.metal","metal",.5f);break;
                case EventKind.Land:n=S(o,n,"blast.quake","stone",.6f);n=S(o,n,"move.land","explosion",.25f);break;
                case EventKind.FissureStart:n=S(o,n,"blast.quake","stone",.8f);n=S(o,n,"melee.greatsword.hit","greatsword",.5f);break;
            }
            return n;
        }
    }
}
