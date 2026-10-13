namespace ChipArena.UnityConversion
{
    // The final killcam's slow motion on the presentation side (Runtime/NativeFinalKillcamPresentation.cs). Everything drawn
    // from the replay clock already slows with it; these are the pieces that step by the render frame or by the 20 Hz
    // frame clock instead. Both stay at their defaults (1, false) outside the final killcam, so nothing else changes.
    public sealed partial class ArenaPresentation
    {
        private float clockRate=1;
        /// <summary>Playback rate of the replay: the blast and projectile particle sims and the spell hand motes, which
        /// advance by the render frame's wall time, advance by it times this rate.</summary>
        public float ClockRate
        {
            get=>clockRate;
            set{clockRate=value>0&&value<1?value:1;if(blasts!=null)blasts.Rate=clockRate;if(shots!=null)shots.Rate=clockRate;}
        }
        /// <summary>The death bodies step their fixed 1/60 s physics on the continuous presentation clock (not the 20 Hz
        /// frame clock), and present a blend of their last two steps, so a ragdoll stays smooth at a quarter speed.</summary>
        public bool SlowBodies;
    }
}
