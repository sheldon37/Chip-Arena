namespace ChipArena.UnityConversion
{
    // The follow-through after a pilot's own chip pick on the draft stage outside the PvP draft (the Deathmatch's starting chip
    // and its comeback chips; Runtime/NativeDeathmatchPickStage.cs): the PvP draft's pick (the others shatter, the card flies to
    // the palm, the pilot plants it into the other wrist) on a snapshot of the offers, as the Siren pick's follow-through plays
    // it. Presentation only: the pick is sent at once and the rules never wait for this. It ends at NativeInstallShot.ShortEnd (1.95 s),
    // or at once on a key or click (after Skip, so the pick's own press does not end it), an attack on the pilot, the pilot's
    // respawn (owner rule: respawn is never delayed for presentation, so the live view returns with it) or when its screen's
    // step is gone (the opening's reveal, the match leaving the fight).
    public static class NativePickFollow
    {
        public const float Skip=.12f;
        public static bool Plays(float age,bool key,bool attacked,bool respawned,bool gone)=>
            age>=0&&age<=NativeInstallShot.ShortEnd&&!(key&&age>Skip)&&!attacked&&!respawned&&!gone;
    }
}
