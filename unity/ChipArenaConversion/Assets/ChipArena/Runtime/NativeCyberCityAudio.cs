using UnityEngine;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Cyber City's sound (Core NativeCyberCityAmbience): the rain bed while a Cyber City round is on screen, and each hazard
    /// state's cue once per cycle (the maglev's chime, horn and rush, the couplings' hum and crack) through the effects mix. Inert
    /// on every other map and in the menu. The billboards' falls use the destruction kit's structure cues.
    /// </summary>
    public sealed partial class NativeAudio
    {
        private AudioSource metroBed;private AudioClip metroBedClip;private bool metroBedLoaded;private MapHazard[] metroHazards;private string metroMap;private string[][] metroOnce,metroName;
        private static readonly string[] MetroCueKeys={"maglev.warn","maglev.horn","maglev.pass","grid.charge","grid.live"};
        private void CyberCityCues(NativeArena arena,bool menu)
        {
            bool on=arena!=null&&!menu&&NativeCyberCityLooks.Active(arena.MapId)&&!Muted&&Effects>0;
            if(!on){if(metroBed!=null&&metroBed.isPlaying)metroBed.Stop();return;}
            if(metroMap!=arena.MapId)
            {
                // The cue keys, built once per map (no strings are made per frame).
                metroMap=arena.MapId;metroHazards=arena.Map.hazards??System.Array.Empty<MapHazard>();metroOnce=new string[metroHazards.Length][];metroName=new string[metroHazards.Length][];
                for(int i=0;i<metroHazards.Length;i++){metroOnce[i]=new string[MetroCueKeys.Length];metroName[i]=new string[MetroCueKeys.Length];for(int k=0;k<MetroCueKeys.Length;k++){metroOnce[i][k]="cybercity:"+metroHazards[i].id+":"+MetroCueKeys[k];metroName[i][k]="hazard."+MetroCueKeys[k];}}
            }
            if(!metroBedLoaded){metroBedLoaded=true;metroBedClip=Clip("audio/"+NativeCyberCityAmbience.Bed);}var bed=metroBedClip;
            if(bed!=null&&bed.length>=NativeCyberCityAmbience.BedMinSeconds)
            {
                if(metroBed==null){metroBed=gameObject.AddComponent<AudioSource>();metroBed.playOnAwake=false;metroBed.loop=true;metroBed.spatialBlend=0;}
                if(metroBed.clip!=bed)metroBed.clip=bed;metroBed.volume=NativeCyberCityAmbience.BedGain*Effects;if(!metroBed.isPlaying)metroBed.Play();
            }
            var match=arena.Match;int round=NativeStructureFx.RoundIndex(match.Set,match.Round);
            for(int i=0;i<metroHazards.Length;i++)
            {
                var h=metroHazards[i];var s=NativeCyberCityHazards.At(h,match.Phase,round,match.FightAt,arena.Time);
                string key=NativeCyberCityAmbience.Cue(h,s,out string clip,out float gain,out float rate,out bool positional);if(key==null)continue;
                int k=System.Array.IndexOf(MetroCueKeys,key);if(k<0||!Once(metroOnce[i][k],(float)(arena.Time-s.Since)))continue;
                var at=NativeCyberCityAmbience.Where(h,s);Cue(metroName[i][k],clip,positional?new Vector3(at.x,at.y,at.z):(Vector3?)null,gain,rate);
            }
        }
    }
}
