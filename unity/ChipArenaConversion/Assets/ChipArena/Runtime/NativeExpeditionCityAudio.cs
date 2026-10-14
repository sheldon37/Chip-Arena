using UnityEngine;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Expedition town's sound (Core NativeExpeditionCityAmbience): distant shots, bursts and explosions out in the dead city
    /// and the crackle of the sanctuary's brazier and the burning bus, on the existing audio keys, through the effects mix (the distant ones loud at the source, so the usual fall-off leaves them faint), while
    /// the town is on screen. Inert on every other map and in the menu.
    /// </summary>
    public sealed partial class NativeAudio
    {
        private readonly NativeCityCue[] townCues=new NativeCityCue[16];private double townTime=double.NaN;private int townGeneration=-1;
        private void ExpeditionCityCues(NativeArena arena,bool menu)
        {
            if(menu||arena==null||!NativeExpeditionCityKit.Active(arena.MapId)){townTime=double.NaN;return;}
            if(townGeneration!=arena.Generation){townGeneration=arena.Generation;townTime=double.NaN;}
            double time=arena.Time;
            if(!double.IsNaN(townTime)&&time>townTime)
            {
                int n=NativeExpeditionCityAmbience.Between(townTime,time,townCues);
                for(int i=0;i<n;i++)
                {
                    var c=townCues[i];var at=new Vector3(c.At.x,c.At.y,c.At.z);
                    switch(c.Kind)
                    {
                        case NativeExpeditionCityAmbience.Shot:Cue("town.shot","rifle",at,1.4f,.62f);break;
                        case NativeExpeditionCityAmbience.Burst:Cue("town.burst","machinegun",at,1.2f,.7f);break;
                        case NativeExpeditionCityAmbience.Boom:Cue("town.boom","explosion",at,2.2f,.42f);break;
                        case NativeExpeditionCityAmbience.Crackle:Cue("town.crackle","fire",at,.3f,.75f+.1f*(c.Cycle%4));break;
                    }
                }
            }
            townTime=time;
        }
    }
}
