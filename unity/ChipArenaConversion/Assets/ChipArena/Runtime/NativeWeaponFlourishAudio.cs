namespace ChipArena.UnityConversion
{
    public sealed partial class NativeAudio
    {
        // The Blind Pick weapon flourishes' sounds (NativeWeaponCard.Sound; Core NativeWeaponFlourishPlan): a screen cue like the
        // pick screen's own (the effects bank's key, else today's clip), or one of the Top Dog's toon sounds. Short and quiet;
        // the local pilot's own screen only.
        private void FlourishHeard(NativeWeaponFlourishPlan.Cue c)
        {
            if(c.Toon!=null){Toon(c.Toon,null,c.Gain,true);return;}
            if(c.Key!=null||c.Clip!=null)UiCue(c.Key,c.Clip,c.Gain);
        }
    }
}
