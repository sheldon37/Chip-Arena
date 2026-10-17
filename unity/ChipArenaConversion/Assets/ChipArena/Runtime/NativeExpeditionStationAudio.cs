using UnityEngine;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Central Station's sounds (docs/EXPEDITION-CENTRAL-STATION.md section 10) on the bank keys the owner will record
    /// (Core NativeExpeditionStation.CueKeys), from the view the picture drew (NativeStationLive.Shown, on its clock): the
    /// express's warning bell, approach (the rails singing), pass and recede; the PA's chime and lines (2D; silent until
    /// recorded); the levers, the rail's cut; the glass's creak and fall; the cabinets' fizz and arc and the steam main's burst
    /// as their phases start. Until a key has clips each plays today's nearest stand-in (or nothing for the PA's voice).
    /// Inert on every other map and in the menu.
    /// </summary>
    public sealed partial class NativeAudio
    {
        private readonly byte[] stationCueKinds=new byte[24];private readonly double[] stationCueTimes=new double[24];private readonly float[] stationCuePlaces=new float[72];
        private double stationTime=double.NaN;private readonly NativeHazardPhase[] stationCuePhases=new NativeHazardPhase[3];
        private void StationCues(NativeArena arena,bool menu)
        {
            StationBossCues(arena,menu);
            var v=NativeStationLive.Shown;if(menu||arena==null||!NativeExpeditionStation.Active(arena.MapId)||v==null){stationTime=double.NaN;return;}
            double t=NativeStationLive.ShownClock;
            if(!double.IsNaN(stationTime)&&t>stationTime)
            {
                int n=NativeExpeditionStation.Cues(v,stationTime,t,stationCueKinds,stationCueTimes,stationCuePlaces);
                for(int i=0;i<n;i++)StationCue(stationCueKinds[i],new Vector3(stationCuePlaces[i*3],stationCuePlaces[i*3+1],stationCuePlaces[i*3+2]));
                // The cabinets' and the steam main's phases as they change.
                for(int c=0;c<2;c++){var ph=v.Cabinets[c].Phase;if(ph!=stationCuePhases[c]){if(ph==NativeHazardPhase.Warn)StationCue(NativeExpeditionStation.CueSparkFizz,CabinetAt(c));else if(ph==NativeHazardPhase.Active)StationCue(NativeExpeditionStation.CueSparkArc,CabinetAt(c));stationCuePhases[c]=ph;}}
                var sp=v.Steam.Phase;if(sp!=stationCuePhases[2]){if(sp==NativeHazardPhase.Active)StationCue(NativeExpeditionStation.CueSteam,new Vector3(-6,3.4f,-59.5f));stationCuePhases[2]=sp;}
            }
            stationTime=t;
        }
        private static Vector3 CabinetAt(int c)=>c==0?new Vector3(-13.4f,.8f,-16.2f):new Vector3(30.6f,.8f,-33.2f);
        private void StationCue(byte kind,Vector3 at)
        {
            string key=NativeExpeditionStation.CueKeys[kind];Vector3? place=NativeExpeditionStation.CueFlat(kind)?(Vector3?)null:at;
            switch(kind)
            {
                case NativeExpeditionStation.CueWarn:Cue(key,"countdown",place,1.2f,.8f);break;
                case NativeExpeditionStation.CueApproach:Cue(key,"dash",place,1.4f,.55f);break;
                case NativeExpeditionStation.CuePass:Cue(key,"explosion",place,1.6f,.6f);break;
                case NativeExpeditionStation.CueRecede:Cue(key,"dash",place,.9f,.45f);break;
                case NativeExpeditionStation.CueLever:Cue(key,"metal",place,1f,.7f);break;
                case NativeExpeditionStation.CueCut:Cue(key,"lightning",place,.8f,.5f);break;
                case NativeExpeditionStation.CueSparkFizz:Cue(key,"spike-rise",place,.5f,1.6f);break;
                case NativeExpeditionStation.CueSparkArc:Cue(key,"lightning",place,.9f,1.2f);break;
                case NativeExpeditionStation.CueSteam:Cue(key,"water",place,.9f,.6f);break;
                case NativeExpeditionStation.CueGlassCreak:Cue(key,"metal",place,.5f,.4f);break;
                case NativeExpeditionStation.CueGlassFall:Cue(key,"stone",place,1f,1.3f);break;
                case NativeExpeditionStation.CueChime:Cue(key,"ui",place,.6f,.7f);break;
                case NativeExpeditionStation.CueBoardFlip:Cue(key,"ui",place,.4f,1.4f);break;
                default:Cue(key,null,place);break;
            }
        }
    }
}
