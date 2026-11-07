using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The zone boss songs' maps (Resources/ZoneBossMusic.json through Core NativeZoneBossMusic), read once and shared by the music
    /// (Runtime/NativeAudio.cs: the intro edit and the loop) and the stage (the station's and the Ironworks' presentation). A
    /// missing or unreadable map leaves both on their old behaviour: the zone track loops plainly and the stage does not react.
    /// </summary>
    public static class NativeZoneBossSongs
    {
        private static NativeZoneBossMusic music;private static bool read;
        private static readonly Dictionary<string,bool> installed=new Dictionary<string,bool>();
        /// <summary>Whether music track `id` ("music/central-station-boss") is in the build (asked once per id).</summary>
        public static readonly Func<string,bool> Installed=id=>{if(id==null)return false;if(!installed.TryGetValue(id,out bool ok)){ok=Resources.Load<AudioClip>("Audio/"+id)!=null;installed[id]=ok;}return ok;};
        public static NativeZoneBossMusic Music
        {
            get
            {
                if(read)return music;read=true;
                try{var a=Resources.Load<TextAsset>(NativeZoneBossMusic.ResourceName);if(a!=null)music=NativeZoneBossMusic.Parse(a.text);else Debug.LogWarning("Zone boss music map missing; zone boss tracks loop plainly");}
                catch(Exception e){Debug.LogWarning("Zone boss music map unreadable ("+e.Message+"); zone boss tracks loop plainly");music=null;}
                return music;
            }
        }
        /// <summary>The song the boss on map `mapId` plays (its file installed and its map read), else null.</summary>
        public static NativeZoneBossSong ForMap(string mapId)=>Music?.ForMap(mapId,Installed);
    }

    public sealed partial class ArenaPresentation
    {
        /// <summary>
        /// This frame's zone boss song for the stage on `arena`'s map (docs/ZONE-BOSS-MUSIC.md): Off before the boss arrives
        /// (NativeArena.BossSongTime below 0), on maps without a zone song and when its file is missing. On the shared boss clock, so
        /// the stage matches what NativeAudio plays and every peer agrees; the flash mode is the settings' (<see cref="KitFlash"/>:
        /// Reduced effects or alarm lights Reduced / Calm), as the hazard kit's lamps use. Allocation free after the first read.
        /// </summary>
        private NativeZoneBossBeat ZoneBossBeat(NativeArena arena)
        {
            if(arena==null)return default;double song=arena.BossSongTime;if(!(song>=0))return default;
            var s=NativeZoneBossSongs.ForMap(arena.MapId);return s==null?default:s.Evaluate(song,KitFlash);
        }
    }
}
