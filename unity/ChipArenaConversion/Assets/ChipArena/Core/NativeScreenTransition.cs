using System;

namespace ChipArena.UnityConversion
{
    public enum ScreenTransitionPhase{Idle,Cover,Hold,Reveal}

    /// <summary>
    /// The menu screen transition (owner 2026-10-15: "When navigating rooms from menu, show a screen transition that matches our
    /// style of the game"): seven chamfered shutter bands slam in from one side, top band first, so their chevron tips form a
    /// diagonal shard front with a bright scan edge; the screen is swapped while they cover it; the destination's title flashes
    /// on the middle band with a brief chromatic glitch; then the bands run out the other side, revealing the new screen as its
    /// own panel-in plays. Back navigation runs mirrored. Reduced effects: a plain fade through the void, no motion, no glitch.
    /// Timing (unscaled seconds): cover .2, hold .07, reveal .3 (.57 in all); reduced .12 / .03 / .18. Input is held only while
    /// the screen is covered (cover and hold), never during the reveal. Pure timing and layout: Runtime NativeScreenTransitionHud
    /// draws it. Nothing here allocates.
    /// </summary>
    public sealed class NativeScreenTransition
    {
        public const float CoverSeconds=.2f,HoldSeconds=.07f,RevealSeconds=.3f;
        public const float ReducedCover=.12f,ReducedHold=.03f,ReducedReveal=.18f;
        public const int Bands=7;
        /// <summary>The delay between one band and the next, top first (the shard front's slope).</summary>
        public const float Stagger=.014f;
        /// <summary>The title flash leads the swap by this much (it rides the middle band in as that band lands).</summary>
        public const float TitleLead=.06f;
        /// <summary>The chromatic glitch: from GlitchLead before the swap to GlitchTail after it.</summary>
        public const float GlitchLead=.02f,GlitchTail=.07f;

        private float start=float.NegativeInfinity;
        public float StartedAt=>start;
        public bool Reduced{get;private set;}
        /// <summary>Mirrored: the bands come in from the left and leave to the right (navigating back toward the home screen).</summary>
        public bool Back{get;private set;}
        /// <summary>The swap ran (the destination screen is the one drawn under the shutters).</summary>
        public bool Swapped{get;private set;}

        public float CoverTime=>Reduced?ReducedCover:CoverSeconds;
        public float HoldTime=>Reduced?ReducedHold:HoldSeconds;
        public float RevealTime=>Reduced?ReducedReveal:RevealSeconds;
        public float TotalTime=>CoverTime+HoldTime+RevealTime;
        public float SwapAt=>start+CoverTime;
        public float RevealAt=>SwapAt+HoldTime;
        public float EndAt=>RevealAt+RevealTime;

        /// <summary>
        /// Starts a transition at <paramref name="now"/>. A transition still covering keeps running (a new destination only
        /// retargets the swap). One in its hold stays covered and swaps again at once. One in its reveal closes again from the
        /// coverage it shows, from the side its bands are on (the bands turn round rather than jump), so nothing pops.
        /// Returns whether a fresh cover began.
        /// </summary>
        public bool Begin(float now,bool reduced,bool back=false)
        {
            var phase=Phase(now);
            if(phase==ScreenTransitionPhase.Cover&&!Swapped)return false;
            float covered=phase==ScreenTransitionPhase.Idle?0:phase==ScreenTransitionPhase.Hold?1:Coverage(now);
            bool mirror=phase==ScreenTransitionPhase.Reveal?!Back:back;
            Reduced=reduced;Back=mirror;Swapped=false;start=now-Clamp01(covered)*CoverTime;return true;
        }
        /// <summary>Stops at once (an immediate screen change took over).</summary>
        public void Cancel(){start=float.NegativeInfinity;Swapped=false;}
        public void MarkSwapped()=>Swapped=true;

        public ScreenTransitionPhase Phase(float now)
        {
            if(float.IsNegativeInfinity(start)||now<start||now>=EndAt)return ScreenTransitionPhase.Idle;
            if(now<SwapAt)return ScreenTransitionPhase.Cover;
            if(now<RevealAt)return ScreenTransitionPhase.Hold;
            return ScreenTransitionPhase.Reveal;
        }
        public bool Active(float now)=>Phase(now)!=ScreenTransitionPhase.Idle;
        /// <summary>Clicks and keys are held only while the screen is covered: never in the reveal, never past CoverTime+HoldTime.</summary>
        public bool BlocksInput(float now){var p=Phase(now);return p==ScreenTransitionPhase.Cover||p==ScreenTransitionPhase.Hold;}
        /// <summary>The screen is fully covered and the swap has not run: run it now (on an IMGUI layout event).</summary>
        public bool SwapDue(float now)=>!Swapped&&!float.IsNegativeInfinity(start)&&now>=SwapAt;

        public float CoverProgress(float now)=>Clamp01((now-start)/CoverTime);
        public float RevealProgress(float now)=>Clamp01((now-RevealAt)/RevealTime);

        /// <summary>Band <paramref name="i"/>'s own progress through its slide (0..1, not eased) at overall phase progress
        /// <paramref name="phase"/>, each band lagging the one above by <paramref name="lagShare"/> of the phase.</summary>
        public static float BandLocal(int i,float phase,float lagShare)
        {
            i=Math.Max(0,Math.Min(Bands-1,i));lagShare=Math.Max(0,Math.Min(.9f/(Bands-1),lagShare));
            return Clamp01((phase-i*lagShare)/(1-(Bands-1)*lagShare));
        }
        /// <summary>
        /// Band <paramref name="i"/>'s covered span across the canvas width as fractions (0 left, 1 right) at overall phase progress
        /// <paramref name="phase"/>: a hard out-cubic slam while covering, an ease-in-out run while revealing. <paramref name="overshoot"/>
        /// is the band's chevron cut as a fraction of the width, so a band that lands covers the whole width and one that leaves
        /// takes its tip off the canvas. <paramref name="back"/> mirrors it (in from the left, out to the right). The stage
        /// loading screen shares it with its own progress.
        /// </summary>
        public static void Span(int i,float phase,float lagShare,bool revealing,float overshoot,bool back,out float left,out float right)
        {
            float k=Math.Max(0,overshoot),u=BandLocal(i,phase,lagShare);
            if(!revealing){float p=OutCubic(u);left=1-p*(1+k);right=1+k;}
            else{float p=InOut(u);left=-k;right=1+k-p*(1+2*k);}
            if(right<left)right=left;
            if(back){float l=1-right,r=1-left;left=l;right=r;}
        }
        /// <summary>How far band <paramref name="i"/>'s content rides with it (a fraction of the width), so its text slides in and out with it.</summary>
        public static float ShiftOf(int i,float phase,float lagShare,bool revealing,float overshoot,bool back)
        {
            float k=Math.Max(0,overshoot),u=BandLocal(i,phase,lagShare);
            float dx=!revealing?(1+k)*(1-OutCubic(u)):-InOut(u)*(1+2*k);return back?-dx:dx;
        }
        private bool Revealing(float now)=>now>=RevealAt;
        private float PhaseOf(float now)=>Revealing(now)?RevealProgress(now):CoverProgress(now);
        private float LagOf(float now)=>Stagger/(Revealing(now)?RevealTime:CoverTime);
        /// <summary>Band <paramref name="i"/>'s covered span at <paramref name="now"/> (Span); idle: empty; reduced: the whole width while active.</summary>
        public void Band(int i,float now,float overshoot,out float left,out float right)
        {
            if(!Active(now)){left=right=0;return;}if(Reduced){left=0;right=1;return;}
            Span(i,PhaseOf(now),LagOf(now),Revealing(now),overshoot,Back,out left,out right);
        }
        /// <summary>How fast band <paramref name="i"/> moves now, 0..1 (its slide's remaining share: 1 as it starts, 0 landed or gone):
        /// the scan edge's trail.</summary>
        public static float MotionOf(int i,float phase,float lagShare){float u=BandLocal(i,phase,lagShare);return u<=0||u>=1?0:1-u;}
        public float Motion(int i,float now)=>Reduced||!Active(now)||Phase(now)==ScreenTransitionPhase.Hold?0:MotionOf(i,PhaseOf(now),LagOf(now));
        /// <summary>The lead edge is the band's left one (chamfered and lit) while it slams in, its right one while it leaves; mirrored back.</summary>
        public bool LeadsLeft(float now)=>!Revealing(now)!=Back;
        /// <summary>How far band <paramref name="i"/>'s content rides with it at <paramref name="now"/> (ShiftOf).</summary>
        public float Shift(int i,float now,float overshoot)=>Reduced||!Active(now)?0:ShiftOf(i,PhaseOf(now),LagOf(now),Revealing(now),overshoot,Back);
        /// <summary>The covered share of the canvas (the bands' mean span, or the fade), for closing again from where it stands.</summary>
        public float Coverage(float now)
        {
            if(!Active(now))return 0;if(Reduced)return Fade(now);float sum=0;
            for(int i=0;i<Bands;i++){Band(i,now,0,out float l,out float r);sum+=Clamp01(r)-Clamp01(l);}
            return Clamp01(sum/Bands);
        }
        /// <summary>The reduced variant's void alpha: eased up over the cover, held, eased down over the reveal.</summary>
        public float Fade(float now)
        {
            var p=Phase(now);if(p==ScreenTransitionPhase.Idle)return 0;if(p==ScreenTransitionPhase.Hold)return 1;
            if(p==ScreenTransitionPhase.Cover)return Smooth(CoverProgress(now));return 1-Smooth(RevealProgress(now));
        }
        /// <summary>The destination title's alpha: it flashes in over the last TitleLead of the cover and rides the middle band out
        /// (reduced: it follows the fade). Zero until the swap is due, so the old screen never shows the new title early.</summary>
        public float TitleAlpha(float now)
        {
            var p=Phase(now);if(p==ScreenTransitionPhase.Idle)return 0;
            if(Reduced)return p==ScreenTransitionPhase.Cover?0:Fade(now);
            if(p==ScreenTransitionPhase.Cover)return Clamp01((now-(SwapAt-TitleLead))/TitleLead);
            return 1;
        }
        /// <summary>The chromatic glitch's strength around the swap (1 at the swap, falling off), 0 with reduced effects.</summary>
        public float Glitch(float now)
        {
            if(Reduced||!Active(now))return 0;float t=now-SwapAt;
            if(t<-GlitchLead||t>GlitchTail)return 0;return t<0?1+t/GlitchLead*.5f:1-t/GlitchTail;
        }
        /// <summary>The hold's scan line, top (0) to bottom (1), or -1 outside the hold / with reduced effects.</summary>
        public float Scan(float now)=>Reduced||Phase(now)!=ScreenTransitionPhase.Hold?-1:Clamp01((now-SwapAt)/HoldTime);

        // ---- layout ----------------------------------------------------------------------------------------------------
        /// <summary>Band <paramref name="i"/>'s top and height on a canvas <paramref name="height"/> tall, overlapping the next by
        /// <paramref name="seam"/> (one physical pixel) so no gap shows between bands at any resolution.</summary>
        public static void BandRow(int i,float height,float seam,out float top,out float h)
        {
            float step=height/Bands;top=i*step;h=step+(i<Bands-1?Math.Max(0,seam):0);
        }
        /// <summary>The chevron cut (canvas units) of a band <paramref name="bandHeight"/> tall at UI scale <paramref name="scale"/>: 40% of
        /// the band, never more than the 96 physical pixels the chrome bakes its corners at.</summary>
        public static float Chamfer(float bandHeight,float scale)=>Math.Max(2,Math.Min(bandHeight*.4f,96f/Math.Max(.01f,scale)));

        public static float Clamp01(float v)=>v<0?0:v>1?1:v;
        public static float OutCubic(float t){t=Clamp01(t);float u=1-t;return 1-u*u*u;}
        public static float InOut(float t){t=Clamp01(t);return t<.5f?4*t*t*t:1-(float)Math.Pow(-2*t+2,3)/2;}
        public static float Smooth(float t){t=Clamp01(t);return t*t*(3-2*t);}
    }
}
