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
            if(menu||arena==null||!NativeExpeditionCityKit.Active(arena.MapId)){townTime=double.NaN;hubTime=double.NaN;return;}
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
            townTime=time;ExpeditionHubCues();
        }
        // The hub gates' sounds (the state the picture drew, NativeExpeditionHubLive.Shown, on its own clock): the latch's clank,
        // the wooden leaves' creak or the metal's rumble, the stop, the slam and the latch dropping, on existing keys.
        private readonly int[] hubKinds=new int[8];private readonly double[] hubTimes=new double[8];private double hubTime=double.NaN;
        private void ExpeditionHubCues()
        {
            var v=NativeExpeditionHubLive.Shown;var l=NativeExpeditionHubLive.Layout;double t=NativeExpeditionHubLive.ShownClock;
            if(v==null||l==null){hubTime=double.NaN;return;}
            if(!double.IsNaN(hubTime)&&t>hubTime&&t-hubTime<1)
                foreach(var g in l.Gates)
                {
                    if(!v.Find(g.Id,out var s))continue;int n=NativeExpeditionHubGates.Cues(g,s,hubTime,t,l,hubKinds,hubTimes);var at=new Vector3(g.X,g.Y+1.4f,g.Z);
                    for(int i=0;i<n;i++)switch(hubKinds[i])
                    {
                        case NativeExpeditionHubGates.CueLatch:Cue("hub.latch",g.Wood?"reload-end":"reload-start",at,.8f,g.Wood?.7f:.8f);break;
                        case NativeExpeditionHubGates.CueCreak:Cue("hub.creak",g.Wood?"wood":"stone",at,g.Wood?.5f:.45f,g.Wood?.42f:.5f);break;
                        case NativeExpeditionHubGates.CueStop:Cue("hub.stop",g.Wood?"wood":"hit",at,.35f,g.Wood?.8f:.6f);break;
                        case NativeExpeditionHubGates.CueSlam:Cue("hub.slam",g.Wood?"wood":"hit",at,.95f,g.Wood?.62f:.5f);Cue("hub.slam-thud","stone",at,.45f,.55f);break;
                        case NativeExpeditionHubGates.CueBolt:Cue("hub.bolt","reload-end",at,.75f,g.Wood?.55f:.7f);break;
                    }
                }
            hubTime=t;
        }
    }
}
