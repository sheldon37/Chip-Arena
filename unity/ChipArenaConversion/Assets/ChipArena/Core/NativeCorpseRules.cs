namespace ChipArena.UnityConversion
{
    /// <summary>Body lifetime is separate from the five-second kill effect and PvE's revivable downed state.
    /// Pilot deaths and match phase already travel in snapshots, including late join and killcam frames.</summary>
    public static class NativeCorpseRules
    {
        public const float SettleSeconds=10,WorldRefreshSeconds=.1f,WorldRadius=8;
        public static bool Retain(bool pilot,bool cooperative)=>pilot&&!cooperative;
        public static bool RoundEnded(bool cooperative,NativePhase phase)=>!cooperative&&phase!=NativePhase.Fight;
        public static bool Expired(float age,bool kept,bool retain)=>age>NativeKillEffectRules.DeathSeconds&&!kept&&!retain;
        public static bool EffectsFinished(float age)=>age>=NativeKillEffectRules.DeathSeconds;
    }
}
