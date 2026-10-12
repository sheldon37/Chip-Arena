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
        private AudioSource cyberBed;private AudioClip cyberBedClip;private bool cyberBedLoaded;private MapHazard[] cyberHazards;private string cyberMap;private string[][] cyberOnce,cyberName;
        private static readonly string[] CyberCueKeys={"maglev.warn","maglev.horn","maglev.pass","grid.charge","grid.live"};
        private void CyberCityCues(NativeArena arena,bool menu)
        {
            bool on=arena!=null&&!menu&&NativeCyberCityLooks.Active(arena.MapId)&&!Muted&&Effects>0;
            if(!on){if(cyberBed!=null&&cyberBed.isPlaying)cyberBed.Stop();return;}
            if(cyberMap!=arena.MapId)
            {
                // The cue keys, built once per map (no strings are made per frame).
                cyberMap=arena.MapId;cyberHazards=NativeCyberCityHazardSource.For(arena.MapId);cyberOnce=new string[cyberHazards.Length][];cyberName=new string[cyberHazards.Length][];
                for(int i=0;i<cyberHazards.Length;i++){cyberOnce[i]=new string[CyberCueKeys.Length];cyberName[i]=new string[CyberCueKeys.Length];for(int k=0;k<CyberCueKeys.Length;k++){cyberOnce[i][k]="cybercity:"+cyberHazards[i].id+":"+CyberCueKeys[k];cyberName[i][k]="hazard."+CyberCueKeys[k];}}
            }
            if(!cyberBedLoaded){cyberBedLoaded=true;cyberBedClip=Clip("audio/"+NativeCyberCityAmbience.Bed);}var bed=cyberBedClip;
            if(bed!=null&&bed.length>=NativeCyberCityAmbience.BedMinSeconds)
            {
                if(cyberBed==null){cyberBed=gameObject.AddComponent<AudioSource>();cyberBed.playOnAwake=false;cyberBed.loop=true;cyberBed.spatialBlend=0;}
                if(cyberBed.clip!=bed)cyberBed.clip=bed;cyberBed.volume=NativeCyberCityAmbience.BedGain*Effects;if(!cyberBed.isPlaying)cyberBed.Play();
            }
            var match=arena.Match;int round=NativeStructureFx.RoundIndex(match.Set,match.Round);
            for(int i=0;i<cyberHazards.Length;i++)
            {
                var h=cyberHazards[i];var s=NativeCyberCityHazards.At(h,match.Phase,round,match.FightAt,arena.Time);
                string key=NativeCyberCityAmbience.Cue(h,s,out string clip,out float gain,out float rate,out bool positional);if(key==null)continue;
                int k=System.Array.IndexOf(CyberCueKeys,key);if(k<0||!Once(cyberOnce[i][k],(float)(arena.Time-s.Since)))continue;
                var at=NativeCyberCityAmbience.Where(h,s);Cue(cyberName[i][k],clip,positional?new Vector3(at.x,at.y,at.z):(Vector3?)null,gain,rate);
            }
        }
    }
}
