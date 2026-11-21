namespace ChipArena.UnityConversion
{
    /// <summary>Body lifetime is separate from the five-second kill effect and PvE's revivable downed state.
    /// Pilot deaths and match phase already travel in snapshots, including late join and killcam frames.</summary>
    public static class NativeCorpseRules
    {
        public const float SettleSeconds=10,WorldRefreshSeconds=.1f,WorldRadius=8;
        // Budget the first sight of an old corpse too: replay/late join must not run 600 PhysX steps in one frame.
        public const int MaxStepsPerFrame=8;
        public static int CatchUpSteps(float clock,float target)=>target<=clock?0:(int)System.Math.Min(MaxStepsPerFrame,System.Math.Floor((target-clock)*60+1e-3f));
        public static bool Retain(bool pilot,bool cooperative)=>pilot&&!cooperative;
        // The winning hit enters Result in the same simulation tick. Keep its death animation and all
        // retained bodies through that five-second viewing period; wipe when results actually finish.
        public static bool RoundEnded(bool cooperative,NativePhase phase)=>!cooperative&&phase!=NativePhase.Fight&&phase!=NativePhase.Result;
        public static bool Expired(float age,bool kept,bool retain)=>age>NativeKillEffectRules.DeathSeconds&&!kept&&!retain;
        public static bool EffectsFinished(float age)=>age>=NativeKillEffectRules.DeathSeconds;
    }
}
