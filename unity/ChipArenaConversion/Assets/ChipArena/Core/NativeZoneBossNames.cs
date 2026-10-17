using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The zone bosses' names on the HUD (the boss plate and the arrival banner: NativeExpeditionStates.BossName routes through
    /// here) and when each one's banner shows. A zone boss's archetype names its plate (`ironworks-converter`: THE CONVERTER, doc
    /// section 9); every other archetype keeps today's name (the placeholder Siren reads SIREN). THE CONVERTER's banner shows from
    /// the arrival's `spawned`, as today's do (doc section 7.1: it plays over the rumble and the crack).
    /// </summary>
    public static class NativeZoneBossNames
    {
        /// <summary>The plate name of a zone boss archetype, or null (not a zone boss: the caller names it as before).</summary>
        public static string Name(string archetype)=>archetype==NativeIronworksBoss.Archetype?NativeIronworksBoss.PlateName:null;
        /// <summary>Seconds after the arrival's `spawned` its banner waits for (0 for every archetype but the zone bosses').</summary>
        public static double BannerDelay(string archetype)=>0;
    }
}
