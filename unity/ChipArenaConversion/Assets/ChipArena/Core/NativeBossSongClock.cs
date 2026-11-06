namespace ChipArena.UnityConversion
{
    // Standalone PvE freezes match time during intros; split Expedition keeps its shared clock running.
    // In either mode the hold countdown is the sole intro clock. Preserve its endpoint on release.
    public static class NativeBossSongClock
    {
        public static double Age(float now,float started,float lead,bool intro,float duration,float left)
            =>started<0?-1:intro?duration-left:now-started+lead;
        public static float ReleaseLead(float now,float started,float duration)=>duration-(now-started);
    }
}
