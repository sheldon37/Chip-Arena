using System;

namespace ChipArena.UnityConversion
{
    // The draft tray's highlight timeline: the holo scan NativeChipArt draws on the card under the pointer, else the keyboard
    // focus, or the stage's held card across a gap (NativeDraftHover), and the pick's lock. Engine-free so the managed checks
    // cover it. Ages are seconds since the card's highlight engaged (the pick, for the lock); lengths are virtual px. A card
    // re-engages (replays the trace, the brackets' snap, the scan's one pass and the die's charge) only from fully off, so a
    // quick out-and-back or a hop across a gap never replays it. Calm is Reduced effects: everything rests where it ends (no
    // trace, comet, snap travel, scan pass, swell, blink, die rise or flare). Without Calm the only repeating motion is the
    // comet's lap and the halo's swell.
    public static class NativeHoloScan
    {
        // Levels: 1 highlighted, Held while held across a gap. In / Out: s per full swing of the level. Off: the level a card
        // re-engages from.
        public const float Held=.7f,In=.15f,Out=.22f,Off=.02f;
        // Trace: the lit edge drawn both ways from the top-left corner. Snap: the brackets' travel (from SnapAt). Scan: the
        // line's single pass down the face (from ScanAt, ScanTime long). Orbit: one comet lap. Breath: the halo's swell (by
        // SwellBy). Lock: the pick's clamp.
        public const float Trace=.32f,SnapAt=.04f,Snap=.2f,ScanAt=.09f,ScanTime=.62f,Orbit=3.2f,Breath=3.4f,SwellBy=.10f,Lock=.3f;
        // The brackets' rest offset outside the card, the snap's extra travel and the release drift (px); where the lock clamps
        // them to.
        public const float BracketRest=6,BracketTravel=10,BracketDrift=6,BracketLock=2.5f;
        // Veil: the others' darkening while a card is highlighted. Ghost: the keyboard focus's still brackets while the pointer
        // is on another card. DieRest: the die's settled charge. Flare: the pick's die flare. Ramp: how far past a leg the trace
        // runs before the leg is fully lit (px). LiftHeight: the highlighted card's rise (px).
        public const float Veil=.15f,Ghost=.45f,DieRest=.6f,Flare=.4f,Ramp=12,LiftHeight=10;
        public static float Target(bool lit,bool held)=>lit?1:held?Held:0;
        public static float Step(float level,float target,float dt)
        {
            float rate=Math.Max(0,dt)/(target>level?In:Out);return level<target?Math.Min(target,level+rate):Math.Max(target,level-rate);
        }
        public static bool Engages(float level,float target)=>target>0&&level<=Off;
        static float Clamp01(float v)=>v<0?0:v>1?1:v;
        // The card's rise at `level` (px up, negative y).
        public static float Lift(float level)=>-LiftHeight*NativeUiMotion.Bezier(.2f,.8f,.2f,1,level);
        // The trace's reach 0..1 of each arm (calm: whole at once).
        public static float TraceReach(float age,bool calm)=>calm?1:NativeUiMotion.Bezier(.3f,.7f,.25f,1,age/Trace);
        // The brackets: offset outside the card (a snap with a little overshoot, drifting out as the level falls) and opacity
        // (L44T's blink once as they boot).
        public static float BracketOffset(float age,float level,bool calm)
        {
            float snap=calm?1:NativeUiMotion.Bezier(.2f,.9f,.3f,1.35f,(age-SnapAt)/Snap);return BracketRest+BracketTravel*(1-snap)+BracketDrift*(1-Clamp01(level));
        }
        public static float BracketOpacity(float age,bool calm,bool blink)
        {
            if(calm)return 1;float o=Clamp01((age-SnapAt)/.08f);return blink&&age>=.1f&&age<.16f?o*.25f:o;
        }
        // The scan line's one pass: false outside it (and calm); else its eased progress down the face.
        public static bool ScanPass(float age,bool calm,out float progress)
        {
            progress=0;if(calm||age<ScanAt||age>=ScanAt+ScanTime)return false;progress=NativeUiMotion.Bezier(.45f,0,.25f,1,(age-ScanAt)/ScanTime);return true;
        }
        // The comet after the trace: its head as a fraction of the outline past the trace's start (it leaves where the trace
        // closed, half a lap round, the bottom-right corner) and its tail length as a fraction of the outline (.24, growing in
        // over .35 s). False before (and calm).
        public static bool Comet(float age,bool calm,out float head,out float tail)
        {
            head=0;tail=0;if(calm||age<Trace)return false;float o=age-Trace;head=.5f+o/Orbit;head-=(float)Math.Floor(head);tail=.24f*Clamp01(o/.35f);return true;
        }
        // The halo's breath on the screen clock `t` (calm: still).
        public static float Swell(float t,bool calm)=>calm?1:1+SwellBy*(float)Math.Sin(t*2*Math.PI/Breath);
        // The die's charge 0..1 over its resting light: up in .05 s, a little back, settled at DieRest by .16 s (calm: settled).
        static readonly float[] ChargeAt={0,.05f,.1f,.16f},ChargeValue={0,1,.78f,DieRest};
        public static float DieCharge(float age,bool calm)=>calm?DieRest:NativeUiMotion.Keys(age,ChargeAt,ChargeValue);
        // How lit a leg is 0..1 as the trace runs both ways from the middle of the top-left arc, `reach` of each arm (half the
        // outline) gone: the top row on the clockwise arm, the bottom row on the anticlockwise one (down the left side). `w` x `h`
        // the card, `R` its corner radius, `legX` the leg's centre from the card's left edge; fully lit Ramp px behind the trace.
        public static float LegLit(float reach,float w,float h,float R,float legX,bool bottom)
        {
            float P=2*(w+h)-(8-2*(float)Math.PI)*R,arc=(float)Math.PI*R/4,d=bottom?arc+(h-2*R)+2*arc+(legX-R):arc+(legX-R);return Clamp01((Clamp01(reach)*P/2-d)/Ramp);
        }
        // A card's veil while another is highlighted: the most highlighted other card's level, less its own.
        public static float Recede(float own,float maxOther)=>Clamp01(maxOther)*(1-Clamp01(own));
        // The pick's lock (age since the pick): the brackets clamp from their rest onto the card (calm: they stay), and the die's
        // flare fading over Lock (calm: none).
        public static float LockOffset(float age,bool calm)=>calm?BracketRest:BracketRest+(BracketLock-BracketRest)*NativeUiMotion.OutCubic(Math.Min(1,Math.Max(0,age)/Lock*1.6f));
        public static float LockFlare(float age,bool calm)=>calm||age<0||age>=Lock?0:Flare*(1-age/Lock);
    }
}
