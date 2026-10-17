using System;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Expedition zone boss music (owner 2026-10-17: "Wire the zone music now, the names work"). Each zone's normal-play music
    /// is routed per map in Runtime/NativeAudio.cs (Astra's PR #263: Foundry of the Dead for City Ironworks, Subway Arrival
    /// Chime for Central Station). When the zone's boss arrives (NativeArena.BossSongTime &gt;= 0), the zone's own boss track
    /// plays if its file is installed (Resources/Audio/music/&lt;name&gt;.mp3). Until then the Siren's song plays, as before.
    /// Ward Zero keeps the Siren's song, whose beat map also drives its vents.
    /// </summary>
    public static class NativeZoneMusic
    {
        public const string SirenSong="music/black-siren-protocol",StationBoss="music/central-station-boss",IronworksBoss="music/city-ironworks-boss";
        /// <summary>The zone boss track named for a map (null: the map has none and the Siren's song plays). City Ironworks
        /// answers to the live id and the design draft's name.</summary>
        public static string ZoneBoss(string mapId)=>mapId=="expedition-transit"?StationBoss:mapId=="expedition-foundry"||mapId=="expedition-ironworks"?IronworksBoss:null;
        /// <summary>The boss track this map plays: its zone track when `installed` finds the file, else the Siren's song.</summary>
        public static string BossTrack(string mapId,Func<string,bool> installed){var zone=ZoneBoss(mapId);return zone!=null&&installed!=null&&installed(zone)?zone:SirenSong;}
        /// <summary>Where a zone boss track plays at boss-song time `songTime` (s): a plain loop of the clip, so every peer hears the
        /// same bar from the shared boss clock. The Siren's song keeps its own intro/loop map (NativeBossTrack.Looped).</summary>
        public static double LoopClock(double songTime,double length)=>songTime<=0?0:length>0?songTime%length:songTime;
    }
}
