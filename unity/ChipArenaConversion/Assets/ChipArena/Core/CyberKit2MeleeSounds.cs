using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Kit v2's melee and shared audio routing onto EXISTING NativeSoundBank keys only (no new key or file), each with a clip
    /// that already ships as its fallback (NativeCyberLordSounds.Clips), as v1's: the view's moments (the string's swings and
    /// the palm, the cable's shot, latch, zip, arrival and yank, the Overclock's burst and fade, the clones' split and bursts,
    /// the cut's coil, dash and detonation, the meteor's leap, hover, dive and impact) and the v2 events (Kit2Hit). A cut cast
    /// is silent past its cut. Presentation only.
    /// </summary>
    public static class CyberKit2MeleeSounds
    {
        public const int Swing=0,Palm=1,UplinkFire=2,UplinkLatch=3,UplinkZip=4,UplinkArrive=5,UplinkYank=6,OverclockBurst=7,OverclockFade=8,ForkSplit=9,ForkBurst1=10,ForkBurst2=11,ForkBurst3=12,
            CutCoil=13,CutDash=14,CutDetonate=15,MeteorLeap=16,MeteorHover=17,MeteorDive=18,MeteorImpact=19,Slots=20;
        private static int S(NativeKitSound[] o,int n,string key,string clip,float gain){if(n<o.Length)o[n]=new NativeKitSound{Key=key,Clip=clip,Gain=gain};return Math.Min(o.Length,n+1);}
        /// <summary>When v2 state cue `slot` is due for this view (NaN: never), and its sounds into `o` (returns the count).</summary>
        public static int State(CyberLordView view,int slot,NativeKitSound[] o,out float at)
        {
            at=float.NaN;int n=0;var k=view.Kit2;if(!k.Live)return 0;
            int Q=CyberKit2Spec.Q,C=CyberKit2Spec.Cut,M=CyberKit2Spec.Meteor;bool uplinkOn=k.At[Q]>-90&&!k.Cut[Q];
            switch(slot)
            {
                case Swing:if(k.ComboAt>-90){at=k.ComboAt;int s=k.ComboStep;n=s==2?S(o,n,"melee.greatsword.spin","dash",.42f):S(o,n,"dev.fist.swing","arcade-punch",s==4?.45f:s==3?.42f:.38f);}break;
                case Palm:if(k.ComboStep==4&&k.ComboHit>-90){at=k.ComboHit;n=S(o,n,"impact.energy","electric-hook",.45f);n=S(o,n,"blast.kinetic","explosion",.25f);}break;
                case UplinkFire:if(k.At[Q]>-90&&k.Until[Q]>k.Release[Q]){at=k.Release[Q];n=S(o,n,"magic.shackles","electric-hook",.5f);n=S(o,n,"dev.leet.glitch","darkness",.25f);}break;
                case UplinkLatch:if(uplinkOn&&k.UplinkLatchAt>-90){at=k.UplinkLatchAt;n=S(o,n,"chip.bomb.attach","metal",.45f);}break;
                case UplinkZip:if(uplinkOn&&k.UplinkOutcome==CyberKit2Melee.Zip&&k.UplinkLatchAt>-90){at=k.UplinkLatchAt+.02f;n=S(o,n,"move.roll","dash",.55f);n=S(o,n,"structure.whoosh","dash",.35f);}break;
                case UplinkArrive:if(uplinkOn&&k.UplinkOutcome==CyberKit2Melee.Zip&&k.UplinkArriveAt>-90){at=k.UplinkArriveAt;n=S(o,n,"move.land","stone",.55f);n=S(o,n,"impact.energy","electric-hook",.3f);}break;
                case UplinkYank:if(uplinkOn&&k.UplinkOutcome==CyberKit2Melee.Yank&&k.UplinkLatchAt>-90){at=k.UplinkLatchAt+.02f;n=S(o,n,"magic.shackles","electric-hook",.55f);n=S(o,n,"status.shock-zap","lightning",.35f);}break;
                case OverclockBurst:if(k.OverclockAt>-90){at=k.OverclockAt;n=S(o,n,"chip.delayed.charge","electric-hook",.55f);n=S(o,n,"blast.static","explosion",.45f);}break;
                case OverclockFade:if(k.OverclockUntil>-90){at=k.OverclockUntil;n=S(o,n,"dev.leet.glitch","darkness",.3f);}break;
                case ForkSplit:if(k.ForkAt>-90){at=k.ForkAt;n=S(o,n,"chip.split","demon-step",.55f);n=S(o,n,"chip.summon","darkness",.3f);}break;
                case ForkBurst1:case ForkBurst2:case ForkBurst3:{int i=slot-ForkBurst1;if(k.BurstAt[i]>-90){at=k.BurstAt[i];n=S(o,n,"blast.static","explosion",.5f);n=S(o,n,"dev.leet.glitch","metal",.25f);}break;}
                case CutCoil:if(k.At[C]>-90){at=k.At[C];n=S(o,n,"dev.leet.glitch","darkness",.35f);}break;
                case CutDash:if(k.At[C]>-90&&k.Until[C]>k.Release[C]){at=k.Release[C];n=S(o,n,"magic.blink-strike","demon-step",.6f);n=S(o,n,"melee.daggers.swing","dash",.4f);}break;
                case CutDetonate:if(k.At[C]>-90&&!k.Cut[C]&&k.DetonateAt>-90&&k.MarkedCount>0){at=k.DetonateAt;n=S(o,n,"blast.shock","lightning",.7f);n=S(o,n,"dev.leet.glitch","metal",.4f);}break;
                case MeteorLeap:if(k.At[M]>-90&&!(k.Cut[M]&&k.Until[M]<CyberKit2Melee.LeapAt(k.At[M]))){at=CyberKit2Melee.LeapAt(k.At[M]);n=S(o,n,"move.jump","dash",.55f);n=S(o,n,"impact.energy","electric-hook",.3f);}break;
                case MeteorHover:if(k.At[M]>-90&&!(k.Cut[M]&&k.Until[M]<CyberKit2Melee.HoverAt(k.At[M]))){at=CyberKit2Melee.HoverAt(k.At[M]);n=S(o,n,"magic.meteor.incoming","darkness",.55f);}break;
                case MeteorDive:if(k.At[M]>-90&&!(k.Cut[M]&&k.Until[M]<CyberKit2Melee.DiveAt(k.At[M]))){at=CyberKit2Melee.DiveAt(k.At[M]);n=S(o,n,"structure.whoosh","dash",.55f);}break;
                case MeteorImpact:if(k.LandAt>-90&&!k.Cut[M]){at=k.LandAt;n=S(o,n,"blast.meteor","explosion",.85f);n=S(o,n,"blast.quake","stone",.6f);}break;
            }
            return n;
        }
        /// <summary>A v2 event's cues (the targets already hear their hurt).</summary>
        public static int Event(EventKind kind,Kit2Hit hit,bool targetMe,NativeKitSound[] o)
        {
            int n=0;bool strike=kind==EventKind.Hit||kind==EventKind.AbilityHit;if(!strike)return 0;
            switch(hit)
            {
                case Kit2Hit.Spin:if(!targetMe)n=S(o,n,"dev.fist.hit","arcade-kick",.6f);break;
                case Kit2Hit.Rise:if(!targetMe){n=S(o,n,"dev.fist.hit","arcade-heavy",.65f);}break;
                case Kit2Hit.Overclock:n=S(o,n,"status.shock-zap","lightning",.4f);break;
                case Kit2Hit.ForkStrike:if(!targetMe)n=S(o,n,"dev.fist.hit","arcade-punch",.4f);break;
                case Kit2Hit.UplinkZip:case Kit2Hit.UplinkYank:if(!targetMe)n=S(o,n,"impact.energy","arcade-punch",.35f);break;
                case Kit2Hit.GlitchMark:n=S(o,n,"dev.leet.glitch","metal",.25f);break;
                case Kit2Hit.ForkBurst:case Kit2Hit.GlitchDetonate:case Kit2Hit.MeteorImpact:if(!targetMe)n=S(o,n,"impact.energy","stone",.35f);break;
            }
            return n;
        }
        /// <summary>The bank keys this routing may play (each an existing NativeSoundBank key; the clips NativeCyberLordSounds.Clips').</summary>
        public static readonly string[] Keys={"dev.fist.swing","melee.greatsword.spin","impact.energy","blast.kinetic","magic.shackles","dev.leet.glitch","chip.bomb.attach","move.roll","structure.whoosh",
            "move.land","status.shock-zap","chip.delayed.charge","blast.static","chip.split","chip.summon","magic.blink-strike","melee.daggers.swing","blast.shock","move.jump","magic.meteor.incoming",
            "blast.meteor","blast.quake","dev.fist.hit"};
    }
}
