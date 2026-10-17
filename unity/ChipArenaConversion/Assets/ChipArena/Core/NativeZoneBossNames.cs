using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The zone bosses' names on the HUD (the boss plate and the arrival banner: NativeExpeditionStates.BossName routes through
    /// here) and when each one's banner shows. A zone boss's archetype names its plate (`station-647`: THE 6:47, doc section 8);
    /// every other archetype keeps today's name (the placeholder Siren reads SIREN). The banner of an arrival with a long
    /// presentation (THE 6:47 bursts through the gate on the drop) waits for that moment; everyone else's shows at once.
    /// </summary>
    public static class NativeZoneBossNames
    {
        /// <summary>The plate name of a zone boss archetype, or null (not a zone boss: the caller names it as before).</summary>
        public static string Name(string archetype)=>archetype==NativeStationBoss.Archetype?NativeStationBoss.Name:null;
        /// <summary>Seconds after the arrival's `spawned` its banner waits for (0 for every archetype but the zone bosses').</summary>
        public static double BannerDelay(string archetype)=>archetype==NativeStationBoss.Archetype?NativeStationBossBeats.Current.Drop:0;
    }
}
