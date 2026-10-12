namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Cyber City's ambience and hazard cues (docs/arenas/CYBERCITY_PROPOSAL.md §10), mapped onto the existing audio keys
    /// (Resources/Audio/audio) until the owner supplies the map's own beds and cues. MapLooks.json names the ambience id
    /// "cybercity"; <see cref="Bed"/> is its loop, <see cref="Cue"/> the one-shot a hazard state starts. Pure: a cue is a function
    /// of the hazard's state alone (it fires in the first <see cref="Window"/> s of the phase it belongs to; NativeAudio keeps it
    /// to once per cycle), so host, guests and the killcam hear the same schedule.
    /// </summary>
    public static class NativeCyberCityAmbience
    {
        public const string Id="cybercity";
        /// <summary>The rain bed: the existing water clip, looped quietly (only when the clip is long enough to loop, 3 s).</summary>
        public const string Bed="water";public const float BedGain=.16f,BedMinSeconds=3,Window=.25f;
        /// <summary>
        /// The cue a hazard's state starts, or null: the maglev's warning chime (countdown, slowed), its horn at the danger
        /// (spike-rise, low), the pass's rush (dash, an octave down); a coupling's charge hum (electric-hook) and its live crack
        /// (lightning). `positional` says whether it is heard from the hazard (else the whole map hears it, as the warning must).
        /// </summary>
        public static string Cue(MapHazard h,in NativeHazardState s,out string clip,out float gain,out float rate,out bool positional)
        {
            clip=null;gain=0;rate=1;positional=true;if(h==null||s.Since>Window)return null;
            if(NativeCyberCityHazards.IsMaglev(h))switch(s.Phase)
            {
                case NativeHazardPhase.Warn:clip="countdown";gain=.5f;rate=.8f;positional=false;return "maglev.warn";
                case NativeHazardPhase.Danger:clip="spike-rise";gain=.85f;rate=.55f;positional=false;return "maglev.horn";
                case NativeHazardPhase.Active:clip="dash";gain=.95f;rate=.5f;return "maglev.pass";
            }
            if(NativeCyberCityHazards.IsGrid(h))switch(s.Phase)
            {
                case NativeHazardPhase.Warn:clip="electric-hook";gain=.5f;rate=.85f;return "grid.charge";
                case NativeHazardPhase.Active:clip="lightning";gain=.6f;rate=1.1f;return "grid.live";
            }
            return null;
        }
        /// <summary>Where a cue is heard from: the maglev's near portal while it warns, its head during the pass, a grid's deck.</summary>
        public static V3 Where(MapHazard h,in NativeHazardState s)
        {
            if(NativeCyberCityHazards.IsMaglev(h)){double x=s.Phase==NativeHazardPhase.Active&&!double.IsNaN(s.Head)?s.Head:h.center.x-s.Direction*h.size.x/2;return new V3((float)System.Math.Max(-h.size.x/2,System.Math.Min(h.size.x/2,x)),h.center.y,h.center.z);}
            return h.center;
        }
    }
}
