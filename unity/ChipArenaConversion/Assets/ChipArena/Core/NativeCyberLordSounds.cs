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
            Connect=13,Blink2=14,Blink3=15,ForkBomb=16,Reappear=17,Clone1=18,Clone2=19,Clone3=20,
            // The ranged mode (Core/CyberLordRanged.cs; the view's mode and R* windows): the reconfiguration, each bolt, the
            // lance's charge and shot, the field's throw, landing and collapse, the swap's blink, its mine's arm, beep and burst,
            // Root Access's charge, its telegraph's warning (the target's counterplay cue) and the beam.
            Switch=21,BoltShot=22,LanceCharge=23,LanceFire=24,FieldThrow=25,FieldLand=26,FieldEnd=27,SwapBlink=28,MineArm=29,MineBeep=30,MineBurst=31,
            RootCharge=32,RootTell=33,RootFire=34,Slots=35;
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
                case Switch:if(v.ModeAt>-90){at=v.ModeAt;n=S(o,n,"dev.leet.glitch","darkness",.4f);n=S(o,n,"chip.delayed.charge","electric-hook",v.Ranged?.35f:.22f);}break;
                case BoltShot:if(v.RReleased(CyberRangedSpec.Bolt)){at=v.RRelease[CyberRangedSpec.Bolt];n=S(o,n,"magic.lightning.shot","lightning",.28f);}break;
                case LanceCharge:if(v.RAt[CyberRangedSpec.Lance]>-90){at=v.RAt[CyberRangedSpec.Lance];n=S(o,n,"chip.delayed.charge","electric-hook",.3f);}break;
                case LanceFire:if(v.RReleased(CyberRangedSpec.Lance)){at=v.RRelease[CyberRangedSpec.Lance];n=S(o,n,"magic.flame-lance","lightning",.5f);n=S(o,n,"dev.leet.glitch","metal",.2f);}break;
                case FieldThrow:if(v.RReleased(CyberRangedSpec.Field)){at=v.RRelease[CyberRangedSpec.Field];n=S(o,n,"gun.frag.throw","dash",.45f);}break;
                case FieldLand:if(v.FieldLand>-90){at=v.FieldLand;n=S(o,n,"field.time","darkness",.55f);n=S(o,n,"dev.leet.glitch","metal",.3f);}break;
                case FieldEnd:if(v.FieldUntil>-90){at=v.FieldUntil;n=S(o,n,"dev.leet.glitch","metal",.25f);}break;
                case SwapBlink:if(v.RReleased(CyberRangedSpec.Swap)){at=v.RRelease[CyberRangedSpec.Swap];n=S(o,n,"chip.teleport","demon-step",.55f);n=S(o,n,"dev.leet.glitch","darkness",.3f);}break;
                case MineArm:if(v.MineAt>-90){at=v.MineAt-CyberRangedSpec.MineFuse+.05f;n=S(o,n,"chip.mine.arm","parry",.35f);}break;
                case MineBeep:if(v.MineAt>-90){at=v.MineAt-CyberRangedSpec.MineFuse*.45f;n=S(o,n,"chip.bomb.fuse-beep","metal",.35f);}break;
                case MineBurst:if(v.MineAt>-90){at=v.MineAt;n=S(o,n,"blast.mine","explosion",.6f);n=S(o,n,"impact.energy","electric-hook",.3f);}break;
                case RootCharge:if(v.RAt[CyberRangedSpec.Root]>-90){at=v.RAt[CyberRangedSpec.Root];n=S(o,n,"chip.delayed.charge","electric-hook",.5f);n=S(o,n,"dev.leet.glitch","darkness",.3f);}break;
                case RootTell:if(v.RAt[CyberRangedSpec.Root]>-90&&v.RUntil[CyberRangedSpec.Root]>v.RAt[CyberRangedSpec.Root]+CyberRangedSpec.TellFrom){at=v.RAt[CyberRangedSpec.Root]+CyberRangedSpec.TellFrom;n=S(o,n,"chip.bomb.fuse-beep","metal",.45f);}break;
                case RootFire:if(v.RReleased(CyberRangedSpec.Root)){at=v.RRelease[CyberRangedSpec.Root];n=S(o,n,"blast.shock","lightning",.8f);n=S(o,n,"magic.lightning.shot","lightning",.55f);n=S(o,n,"blast.static","explosion",.4f);}break;
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
                // The ranged set (the shot and cast sounds come from the view's windows; these are the landings).
                case CyberHit.Bolt:if(kind==EventKind.Absorb)n=S(o,n,"melee.shield.block","parry",.3f);else if(strike&&!targetMe)n=S(o,n,"impact.energy","lightning",.35f);else if(kind==EventKind.Impact)n=S(o,n,"impact.energy","electric-hook",.15f);break;
                case CyberHit.Lance:if(kind==EventKind.Absorb)n=S(o,n,"melee.shield.block","parry",.4f);else if(strike)n=S(o,n,"status.shock-zap","lightning",.45f);break;
                case CyberHit.Field:if(strike)n=S(o,n,"status.burn-tick","burn-tick",.22f);break;
                case CyberHit.Swap:if(strike&&!targetMe)n=S(o,n,"impact.energy","stone",.4f);break;
                case CyberHit.Root:if(kind==EventKind.Absorb)n=S(o,n,"melee.shield.block","parry",.6f);else if(strike){n=S(o,n,"dev.fist.hit","arcade-heavy",.8f);n=S(o,n,"blast.nova","explosion",.5f);}break;
            }
            return n;
        }
        /// <summary>The second jump's hardlight boost (Core/CyberLordRun.cs; NativeAudio's movement feel, on the jump track's air
        /// jump): the Data Dash's whoosh, lighter, and a glitch tick.</summary>
        public static int AirBoost(NativeKitSound[] o){int n=0;n=S(o,n,"move.roll","dash",.35f);n=S(o,n,"dev.leet.glitch","darkness",.22f);return n;}
        /// <summary>The bank keys this routing may play (Tests/CyberLordChecks: every one is an existing bank key).</summary>
        public static readonly string[] Keys={"dev.fist.swing","impact.energy","move.roll","dev.leet.glitch","field.repulsion","magic.shackles","status.shock-zap","blast.quake","blast.static",
            "melee.miss","blast.implosion","dev.fist.hit","magic.blink-strike","chip.split","melee.shield.block","status.burn-tick","blast.leet-nuke",
            "chip.delayed.charge","magic.lightning.shot","magic.flame-lance","gun.frag.throw","field.time","chip.teleport","chip.mine.arm","chip.bomb.fuse-beep","blast.mine","blast.shock","blast.nova"};
        public static readonly string[] Clips={"arcade-punch","electric-hook","dash","darkness","parry","metal","lightning","stone","explosion","arcade-heavy","demon-step","arcade-kick","burn-tick"};
    }
}
