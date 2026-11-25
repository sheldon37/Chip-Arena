namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Magic Missiles visual trial (Claude C2, 2026-11-25; Astra's revamp brief §4: "a separate visual trial for one existing PvP
    /// spell. Preserve its projectile trajectory, impact timing and damage rules"): the fire staff's primary bolt drawn as the
    /// Magic Missiles pack's FireMissile (a flame core with its fire and smoke trails streaming behind, a light-free copy) and its hits
    /// as the pack's FireExplosion, both scaled to the game's bolt. The missile rides the bolt's own replicated head each frame
    /// (NativeShotViews: the authority's path, the guest's carried-on head), so its trajectory is the bolt's; the explosion plays at
    /// the same Impact or direct Hit event, on the same frame, the pack sprite burst would have (NativeMagicPackPresentation), so
    /// impact timing is unchanged. While the trial draws a bolt, that bolt's energy core, pack head, trail sprites and ribbon are
    /// not drawn (the trial is its look); every other school and the fire staff's abilities keep theirs.
    /// <para>The effects are the pack's particle systems on Unity's built-in particle shader (no pack shader or script; the lights
    /// dropped), in the git-ignored effects folder (Assets/LocalAssets/ChipArenaFx, Resources/<see cref="Resource"/>: its
    /// <see cref="Missile"/> and <see cref="Explosion"/>), shipped with the separate effects bank. Without them, or on the low
    /// graphics tier, the bolt draws exactly as today. Engine-free; presentation only: no rule, timing, hitbox, damage or network
    /// message changes.</para>
    /// </summary>
    public static class NativeMissileTrial
    {
        public const string Resource="ChipArenaMissileFx",Missile="missile",Explosion="explosion";
        /// <summary>The trial's switch (a DEV and capture comparison; the game leaves it on when the effects are installed).</summary>
        public static bool On=true;
        /// <summary>The trial's spell: the fire staff's primary bolt.</summary>
        public const NativeMagicSchool School=NativeMagicSchool.Fire;
        /// <summary>The pack's missile is about 1.5 m across, its explosion 4 m (its flame turning to smoke): scaled to the game's bolt (its glow
        /// about 0.5 m) and to a hit about a metre across.</summary>
        public const float MissileScale=.32f,ExplosionScale=.24f;
        /// <summary>A missile whose bolt left the frame stops emitting; its trails live out this long, then it is put away.</summary>
        public const float TrailFade=.8f;
        /// <summary>An explosion plays this long, then it is put away; an event older than <see cref="Stale"/> plays none.</summary>
        public const float ExplosionLife=1.6f,Stale=.3f;
        public const int MaxMissiles=16,MaxExplosions=8;
        /// <summary>Whether the trial draws this bolt (the fire school's primary bolt; NativeMagicPackFx.Bolt).</summary>
        public static bool Draws(in NativeShotView v)=>On&&NativeMagicPackFx.Bolt(v)==School;
        /// <summary>Whether the trial draws the hit of a bolt of this school.</summary>
        public static bool DrawsHit(NativeMagicSchool school)=>On&&school==School;
    }
}
