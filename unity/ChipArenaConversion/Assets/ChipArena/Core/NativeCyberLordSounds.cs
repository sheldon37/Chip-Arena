using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Cyber Lord's audio routing onto EXISTING NativeSoundBank keys only (no new key, no new file), each with a built-in
    /// clip that already ships as its fallback (arcade-punch / -kick / -heavy, electric-hook, dash, demon-step, lightning,
    /// darkness, parry, stone, explosion), so the kit is heard with or without the owner's LocalAssets. Two replicated sources:
    /// the view's windows (the combo swing's start, each ability's release, the wall's collapse, the link's end, the Kernel
    /// Panic connect and step starts: moments no event marks) and the cyber events (their Detail: hits, absorbs, heals).
    /// Presentation only.
    /// </summary>
    public static class NativeCyberLordSounds
    {
        public const int Max=3;
        /// <summary>A due state cue: its moment (each cue once; NativeAudio keys it by its slot and moment) and its sounds.</summary>
        public struct Due{public int Slot;public float At;public int Count;}
        private static int S(NativeKitSound[] o,int n,string key,string clip,float gain){if(n<o.Length)o[n]=new NativeKitSound{Key=key,Clip=clip,Gain=gain};return Math.Min(o.Length,n+1);}
        /// <summary>The state cue slots NativeAudio walks each frame (Count): their moments and sounds from the view.</summary>
        public const int Swing=0,Palm=1,QRelease=2,ERelease=3,WallEnd=4,OneRelease=5,TwoRelease=6,LinkEnd=7,ThreeCast=8,ThreeSlam=9,FourCast=10,FourLunge=11,FourWhiff=12,
            Connect=13,Blink2=14,Blink3=15,ForkBomb=16,Reappear=17,Clone1=18,Clone2=19,Clone3=20,Slots=21;
        /// <summary>When state cue `slot` is due for this view (NaN: never), and its sounds into `o` (returns the count).</summary>
        public static int State(CyberLordView v,int slot,NativeKitSound[] o,out float at)
        {
            at=float.NaN;int n=0;float age(int k)=>v.PanicStart+KernelPanicPath.Steps[k];
            switch(slot)
            {
                case Swing:if(v.ComboAt>-90){at=v.ComboAt;n=S(o,n,"dev.fist.swing","arcade-punch",v.ComboStep==2?.45f:.38f);}break;
                case Palm:if(v.ComboStep==2&&v.ComboHit>-90){at=v.ComboHit;n=S(o,n,"impact.energy","electric-hook",.4f);}break;
                case QRelease:if(v.Release[CyberLordSpec.Q]>-90){at=v.Release[CyberLordSpec.Q];n=S(o,n,"move.roll","dash",.5f);n=S(o,n,"dev.leet.glitch","darkness",.3f);}break;
                case ERelease:if(v.WallBorn>-90){at=v.WallBorn;n=S(o,n,"field.repulsion","parry",.55f);}break;
                case WallEnd:if(v.WallUntil>-90){at=v.WallUntil;n=S(o,n,"dev.leet.glitch","metal",.3f);}break;
                case OneRelease:if(v.Release[CyberLordSpec.One]>-90){at=v.Release[CyberLordSpec.One];n=S(o,n,"dev.leet.glitch","lightning",.5f);}break;
                case TwoRelease:if(v.LinkAt>-90&&v.LinkTarget!=0){at=v.LinkAt;n=S(o,n,"magic.shackles","electric-hook",.45f);}break;
                case LinkEnd:if(v.LinkUntil>-90&&v.LinkTarget!=0){at=v.LinkUntil;n=S(o,n,"status.shock-zap","lightning",.3f);}break;
                case ThreeCast:if(v.At[CyberLordSpec.Three]>-90){at=v.At[CyberLordSpec.Three];n=S(o,n,"dev.leet.glitch","darkness",.35f);}break;
                case ThreeSlam:if(v.CrashBorn>-90){at=v.CrashBorn;n=S(o,n,"blast.quake","stone",.7f);n=S(o,n,"blast.static","explosion",.45f);}break;
                case FourCast:if(v.At[CyberLordSpec.Four]>-90){at=v.At[CyberLordSpec.Four];n=S(o,n,"dev.leet.glitch","electric-hook",.4f);}break;
                case FourLunge:if(v.Release[CyberLordSpec.Four]>-90){at=v.Release[CyberLordSpec.Four];n=S(o,n,"move.roll","dash",.55f);}break;
                case FourWhiff:if(v.PanicOutcome==CyberLordSpec.Whiff&&v.Until[CyberLordSpec.Four]>-90){at=v.Until[CyberLordSpec.Four];n=S(o,n,"melee.miss",null,.4f);}break;
                case Connect:if(v.PanicConnect>-90&&(v.PanicOutcome==CyberLordSpec.Connect||v.PanicOutcome==CyberLordSpec.EndedEarly)){at=v.PanicConnect;n=S(o,n,"blast.implosion","darkness",.6f);n=S(o,n,"dev.fist.hit","arcade-heavy",.7f);}break;
                case Blink2:if(v.PanicStart>-90){at=age(1);n=S(o,n,"magic.blink-strike","demon-step",.6f);}break;
                case Blink3:if(v.PanicStart>-90){at=age(2);n=S(o,n,"magic.blink-strike","demon-step",.6f);}break;
                case ForkBomb:if(v.PanicStart>-90){at=age(3);n=S(o,n,"chip.split","demon-step",.5f);n=S(o,n,"dev.leet.glitch","darkness",.35f);}break;
                case Reappear:if(v.PanicStart>-90){at=age(4)-.1f;n=S(o,n,"magic.blink-strike","demon-step",.55f);}break;
                // The fork bomb's three clone strikes on their staggered beats (the host's three step-4 hits land together).
                case Clone1:case Clone2:case Clone3:if(v.PanicStart>-90){at=v.PanicStart+KernelPanicPath.Clones[slot-Clone1];n=S(o,n,"dev.fist.hit",slot==Clone2?"arcade-kick":"arcade-punch",.6f);}break;
            }
            // An early end cuts the rest of the choreography's cues.
            if(slot>=Blink2&&v.PanicOutcome==CyberLordSpec.EndedEarly&&v.PanicEndedAt>-90&&at>=v.PanicEndedAt){at=float.NaN;n=0;}
            return n;
        }
        /// <summary>A cyber event's cues (Hit / AbilityHit / Absorb with a cyber Detail). targetMe: the local pilot is its
        /// target (it already hears its hurt).</summary>
        public static int Event(EventKind kind,CyberHit hit,bool targetMe,NativeKitSound[] o)
        {
            int n=0;bool strike=kind==EventKind.Hit||kind==EventKind.AbilityHit;
            switch(hit)
            {
                case CyberHit.Jab:case CyberHit.Cross:if(strike&&!targetMe)n=S(o,n,"dev.fist.hit","arcade-punch",.55f);break;
                case CyberHit.Palm:if(strike&&!targetMe)n=S(o,n,"dev.fist.hit","arcade-heavy",.6f);break;
                case CyberHit.Dash:if(strike&&!targetMe)n=S(o,n,"impact.energy","arcade-punch",.4f);break;
                case CyberHit.Firewall:if(kind==EventKind.Absorb)n=S(o,n,"melee.shield.block","parry",.45f);else if(strike)n=S(o,n,"status.burn-tick","burn-tick",.3f);break;
                case CyberHit.Burst:if(strike&&!targetMe)n=S(o,n,"impact.energy","lightning",.25f);break;
                case CyberHit.Siphon:break; // the hook and the end sound from the view (TwoRelease, LinkEnd); the caster's heal is the generic Heal cue
                case CyberHit.Crash:if(strike)n=S(o,n,"status.shock-zap","lightning",.4f);break;
                case CyberHit.Panic1:case CyberHit.Panic2:case CyberHit.Panic3:if(strike)n=S(o,n,"dev.fist.hit",hit==CyberHit.Panic2?"arcade-kick":"arcade-heavy",.9f);break;
                case CyberHit.Panic4:break; // the clone cues (state slots Clone1-3) sound the step's three hits on their beats
                case CyberHit.PanicWave:if(strike&&!targetMe)n=S(o,n,"impact.energy","stone",.45f);break;
                case CyberHit.Panic5:if(strike){n=S(o,n,"blast.leet-nuke","explosion",1);n=S(o,n,"blast.quake","stone",.8f);}break;
            }
            return n;
        }
        /// <summary>The bank keys this routing may play (Tests/CyberLordChecks: every one is an existing bank key).</summary>
        public static readonly string[] Keys={"dev.fist.swing","impact.energy","move.roll","dev.leet.glitch","field.repulsion","magic.shackles","status.shock-zap","blast.quake","blast.static",
            "melee.miss","blast.implosion","dev.fist.hit","magic.blink-strike","chip.split","melee.shield.block","status.burn-tick","blast.leet-nuke"};
        public static readonly string[] Clips={"arcade-punch","electric-hook","dash","darkness","parry","metal","lightning","stone","explosion","arcade-heavy","demon-step","arcade-kick","burn-tick"};
    }
}
