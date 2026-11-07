using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The Factory's hazard cues and ambience on NativeAudio's existing voices and clips (FACTORY_PROPOSAL.md 3.11): Core
    // NativeFactoryHazards.Cues for the window since the last frame on the hazard clock (spin-up, emerge, grind, sink; rail
    // warning, swing and return; press hiss, slam and lift; crane move, two horns, release, impact, hoist; vent hiss and
    // burst), Core NativeFactoryAmbience on the arena clock. Positioned and dulled with distance by Play like every world cue,
    // each with a "factory." bank key the owner's effects bank may fill. Map "factory" only; a new round, a rewind or another
    // map just restarts the window (nothing is played twice: each cue lies in exactly one window). Allocation free.
    public sealed partial class NativeAudio
    {
        private readonly FactoryCue[] factoryCues=new FactoryCue[64];private double factoryClock=double.NaN,factoryTime=double.NaN;private int factoryGeneration=-1;
        private void FactoryCues(NativeArena arena,bool menu)
        {
            if(menu||arena==null||!NativeFactoryHazards.Active(arena.MapId)){factoryClock=factoryTime=double.NaN;return;}
            var match=arena.Match;double clock=NativeFactoryHazards.Clock(match.Phase,match.FightAt,arena.Time),time=arena.Time;
            if(factoryGeneration!=arena.Generation){factoryGeneration=arena.Generation;factoryClock=factoryTime=double.NaN;}
            var hz=NativeFactoryStage.Hazards(arena.MapId);int round=NativeStructureFx.RoundIndex(match.Set,match.Round);
            if(hz!=null&&!double.IsNaN(factoryClock)&&clock>factoryClock&&clock-factoryClock<1)
            {int n=NativeFactoryHazards.Cues(hz,round,factoryClock,clock,factoryCues);for(int i=0;i<n;i++)FactoryCue(factoryCues[i]);}
            factoryClock=double.IsInfinity(clock)?double.NaN:clock;
            if(!double.IsNaN(factoryTime)&&time>factoryTime){int n=NativeFactoryAmbience.Between(factoryTime,time,factoryCues);for(int i=0;i<n;i++)FactoryCue(factoryCues[i]);}
            factoryTime=time;
        }
        private void FactoryCue(in FactoryCue c)
        {
            var at=new Vector3(c.At.x,c.At.y,c.At.z);
            switch(c.Kind)
            {
                case NativeFactoryHazards.CueSpinUp:Cue("factory.saw.spin","saw-cut",at,.45f,.62f);break;
                case NativeFactoryHazards.CueEmerge:Cue("factory.saw.emerge","spike-rise",at,.7f,1.25f);break;
                case NativeFactoryHazards.CueGrind:Cue("factory.saw.grind","saw-cut",at,.4f,.94f+.12f*(c.Cycle%3),"factory.grind");break;
                case NativeFactoryHazards.CueSink:Cue("factory.saw.sink","metal",at,.35f,1.5f);break;
                case NativeFactoryHazards.CueRailWarn:Cue("factory.rail.warn","saw-cut",at,.4f,.7f);break;
                case NativeFactoryHazards.CueRailOut:Cue("factory.rail.swing","dash",at,.55f,.8f);Cue(null,"saw-cut",at,.5f,1.15f);break;
                case NativeFactoryHazards.CueRailBack:Cue("factory.rail.back","dash",at,.4f,.7f);break;
                case NativeFactoryHazards.CuePressHiss:Cue("factory.press.hiss","water",at,.4f,1.7f);break;
                case NativeFactoryHazards.CuePressSlam:Cue("factory.press.slam","metal",at,1f,.5f);Cue(null,"stone",at,.7f,.6f);break;
                case NativeFactoryHazards.CuePressLift:Cue("factory.press.lift","water",at,.25f,1.3f);break;
                case NativeFactoryHazards.CueCraneMove:Cue("factory.crane.move","metal",at,.22f,1.8f);break;
                case NativeFactoryHazards.CueHorn:Cue("factory.crane.horn","countdown",at,.8f,.55f);break;
                case NativeFactoryHazards.CueRelease:Cue("factory.crane.release","metal",at,.6f,1.3f);break;
                case NativeFactoryHazards.CueImpact:StructureCue(NativeStructureSound.Land,at,NativeStructureMaterial.Metal,.9f);Cue("factory.crane.impact","explosion",at,.45f,.55f);break;
                case NativeFactoryHazards.CueHoist:Cue("factory.crane.hoist","metal",at,.3f,.9f);break;
                case NativeFactoryHazards.CueVentHiss:Cue("factory.vent.hiss","water",at,.3f,1.9f);break;
                case NativeFactoryHazards.CueVentBurst:Cue("factory.vent.burst","water",at,.65f,1.15f);Cue(null,"fire",at,.25f,.5f);break;
                case NativeFactoryAmbience.Clank:Cue("factory.ambience.clank","metal",at,.12f,.8f+.1f*(c.Cycle%5),"factory.ambience");break;
                case NativeFactoryAmbience.Hammer:Cue("factory.ambience.hammer","stone",at,.25f,.55f,"factory.ambience");break;
                case NativeFactoryAmbience.Crackle:Cue("factory.ambience.crackle","fire",at,.2f,.45f,"factory.ambience");break;
                case NativeFactoryAmbience.Arc:Cue("factory.ambience.arc","lightning",at,.08f,1.8f,"factory.ambience");break;
                case NativeFactoryAmbience.Hiss:Cue("factory.ambience.hiss","water",at,.15f,1.6f,"factory.ambience");break;
                case NativeFactoryAmbience.Horn:Cue("factory.ambience.horn","countdown",at,.3f,.42f,"factory.ambience");break;
            }
        }
    }
}
