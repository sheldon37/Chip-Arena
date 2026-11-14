namespace ChipArena.UnityConversion
{
    // When the chip install cue (NativeEventSounds' Install route) plays: on the draft stage's wrist plant
    // (NativeDraftPlant.SoundAt after the pick), not on the pick click. Presentation only: the pick, its event and its timing
    // are unchanged; nothing else plays an install sound on the pick (one cue per pick).
    // PvP draft: from the replicated pick time, once per pick. Siren / Ward Zero pick: my own Install event is held by the
    // local follow-through (NativeDraftFollow) and played once, at the plant, or when the follow-through ends before it; an
    // event the follow-through did not hold (another pilot's pick, or one that arrives after it ended) plays as it arrives.
    public sealed class NativeInstallCue
    {
        public const float Fresh=.4f;
        public static bool DraftDue(double chosenAt,double time){double at=chosenAt+NativeDraftPlant.SoundAt;return time>=at&&time-at<Fresh;}
        private string chip;private float key=float.NaN;private bool heard,played;
        // Each frame, before the events: the follow-through's chip (null when none), its start time and age. Returns the
        // held chip to play now, or null.
        public string Step(string followChip,float startedAt,float age)
        {
            if(followChip!=null)
            {
                if(chip!=followChip||key!=startedAt){chip=followChip;key=startedAt;heard=played=false;}
                if(heard&&!played&&age>=NativeDraftPlant.SoundAt){played=true;return chip;}
                return null;
            }
            if(chip==null)return null;string held=heard&&!played?chip:null;chip=null;heard=played=false;return held;
        }
        // My own Install event: true when the running follow-through takes it (one event per pick).
        public bool Hold(string chipId){if(chip==null||heard||chipId!=chip)return false;heard=true;return true;}
        public void Clear(){chip=null;key=float.NaN;heard=played=false;}
    }
}
