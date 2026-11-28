using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// What Substation 128's lights hear (docs/arenas/CLUB.md "Music reactivity"): the music voice's spectrum, taken each frame
    /// the club is on screen and the music plays audibly (NativeAudio's own music AudioSource, so only the song is sampled,
    /// never the effects), for NativeClubDraw to analyse into the show's pulse (Core NativeClubPulse). Presentation only:
    /// nothing here reaches the simulation, the network or a hazard's timing. Stale after a frame: the lights fall back to the
    /// steady 128 bpm clock when nothing was heard (no song, muted, the volume at 0, the menu).
    /// </summary>
    public static class NativeClubMusic
    {
        public static readonly float[] Spectrum=new float[NativeClubPulse.Bins];
        public static int HeardFrame=-10;public static double Rate=48000;
        /// <summary>Whether this frame (or the last) carried the song's spectrum.</summary>
        public static bool Fresh=>Time.frameCount-HeardFrame<=1;
    }
    /// <summary>
    /// Substation 128's sound: the music's spectrum for the lights (above), and each hazard state's cue once per cycle on the
    /// effects mix with a "club." bank key the owner's effects bank may fill (the laser wall's warning chime, its alarm at the
    /// danger and the sweep's rush; the charged floor's hum as it charges and its crack when it goes live, once per pattern;
    /// the drop's riser and its boom, once for both stacks). Cues are pure functions of the replicated hazard state, so host,
    /// guests and the killcam hear the same schedule. Inert on every other map and in the menu. Allocation free per frame.
    /// </summary>
    public sealed partial class NativeAudio
    {
        private const string ClubLaserWarn="club:laser:warn",ClubLaserDanger="club:laser:danger",ClubLaserPass="club:laser:pass",ClubGridCharge="club:grid:charge",ClubGridLive="club:grid:live",ClubDropRise="club:drop:rise",ClubDropBoom="club:drop:boom";
        private void ClubFrame(NativeArena arena,bool menu)
        {
            if(arena==null||menu||!NativeClubLooks.Active(arena.MapId))return;
            bool heard=music!=null&&music.clip!=null&&music.isPlaying&&!music.mute&&music.volume>.001f&&!Muted;
            if(heard){music.GetSpectrumData(NativeClubMusic.Spectrum,0,FFTWindow.BlackmanHarris);NativeClubMusic.HeardFrame=Time.frameCount;NativeClubMusic.Rate=AudioSettings.outputSampleRate;}
            if(Muted||Effects<=0)return;
            var match=arena.Match;int round=NativeStructureFx.RoundIndex(match.Set,match.Round);float now=arena.Time;
            foreach(var h in arena.Map.hazards??System.Array.Empty<MapHazard>())
            {
                if(h==null)continue;
                if(h.kind=="maglev")
                {
                    var s=NativeCyberCityHazards.At(h,match.Phase,round,match.FightAt,now);if(s.Since>.25f)continue;float at=(float)(now-s.Since);
                    if(s.Phase==NativeHazardPhase.Warn&&Once(ClubLaserWarn,at))Cue("club.laser.warn","countdown",null,.45f,.9f);
                    else if(s.Phase==NativeHazardPhase.Danger&&Once(ClubLaserDanger,at))Cue("club.laser.danger","spike-rise",null,.75f,.7f);
                    else if(s.Phase==NativeHazardPhase.Active&&Once(ClubLaserPass,at)){float x=(float)(h.center.x-s.Direction*h.size.x/2);Cue("club.laser.pass","dash",new Vector3(x,.4f,h.center.z),.95f,.62f);}
                }
                else if(h.kind=="grid")
                {
                    var s=NativeCyberCityHazards.At(h,match.Phase,round,match.FightAt,now);if(s.Since>.25f)continue;float at=(float)(now-s.Since);
                    if(s.Phase==NativeHazardPhase.Warn&&Once(ClubGridCharge,at))Cue("club.floor.charge","electric-hook",new Vector3(0,.5f,0),.6f,.8f);
                    else if(s.Phase==NativeHazardPhase.Active&&Once(ClubGridLive,at))Cue("club.floor.live","lightning",new Vector3(0,.5f,0),.7f,1.05f);
                }
                else if(h.kind=="vent")
                {
                    var s=NativeFactoryHazards.State(h,round,NativeFactoryHazards.Clock(match.Phase,match.FightAt,now));double since=s.Phase==NativeFactoryHazards.Bursting?s.Age-h.warn:s.Age;
                    if(s.Phase==NativeFactoryHazards.Parked||since<0||since>.25)continue;float at=(float)(now-since);
                    var where=new Vector3(0,2,(float)NativeClubLooks.SpeakerZ);
                    if(s.Phase==NativeFactoryHazards.Warning&&Once(ClubDropRise,at))Cue("club.drop.rise","spike-rise",null,.7f,.35f);
                    else if(s.Phase==NativeFactoryHazards.Bursting&&Once(ClubDropBoom,at)){Cue("club.drop.boom","explosion",where,1f,.42f);Cue(null,"metal",where,.5f,.4f);}
                }
            }
        }
    }
}
