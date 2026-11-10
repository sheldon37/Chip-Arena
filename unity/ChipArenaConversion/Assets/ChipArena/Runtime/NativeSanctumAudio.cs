using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The Hollow Sanctum's hazard cues on NativeAudio's existing voices and clips (docs/arenas/SANCTUM.md "Hazards"): Core
    // NativeSanctumCues for the window since the last frame on the arena clock (the chandeliers' creak, slips, snap, crash and
    // winch; the censer's flare and whooshes; the holy fire's kindling, roar and hiss; the bell's three strokes and the crypt
    // gates' fall, slam and rise). Positioned and dulled with distance by Play like every world cue, each with a "sanctum." bank
    // key the owner's effects bank may fill. Map "sanctum" only; a new round, a rewind or another map restarts the window.
    // Allocation free.
    public sealed partial class NativeAudio
    {
        private readonly FactoryCue[] sanctumCues=new FactoryCue[64];private double sanctumTime=double.NaN;private int sanctumGeneration=-1;
        private void SanctumCues(NativeArena arena,bool menu)
        {
            if(menu||arena==null||!NativeSanctumLooks.Active(arena.MapId)){sanctumTime=double.NaN;return;}
            var match=arena.Match;double time=arena.Time;if(sanctumGeneration!=arena.Generation){sanctumGeneration=arena.Generation;sanctumTime=double.NaN;}
            if(!double.IsNaN(sanctumTime)&&time>sanctumTime&&time-sanctumTime<1)
            {int n=NativeSanctumCues.Between(arena.Map.hazards,NativeStructureFx.RoundIndex(match.Set,match.Round),match.Phase,match.FightAt,sanctumTime,time,sanctumCues);for(int i=0;i<n;i++)SanctumCue(sanctumCues[i]);}
            sanctumTime=time;
        }
        private void SanctumCue(in FactoryCue c)
        {
            var at=new Vector3(c.At.x,c.At.y,c.At.z);
            switch(c.Kind)
            {
                case NativeSanctumCues.Creak:Cue("sanctum.chandelier.creak","metal",at,.55f,.42f);Cue(null,"wood",at,.35f,.5f);break;
                case NativeSanctumCues.Slip:Cue("sanctum.chandelier.slip","metal",at,.6f,1.35f);break;
                case NativeSanctumCues.Snap:Cue("sanctum.chandelier.snap","metal",at,.8f,1.7f);Cue(null,"sword",at,.4f,.6f);break;
                case NativeSanctumCues.Crash:StructureCue(NativeStructureSound.Land,at,NativeStructureMaterial.Metal,1f);Cue("sanctum.chandelier.crash","explosion",at,.5f,.5f);Cue(null,"stone",at,.6f,.8f);break;
                case NativeSanctumCues.Winch:Cue("sanctum.chandelier.winch","metal",at,.3f,.85f);break;
                case NativeSanctumCues.CenserFlare:Cue("sanctum.censer.flare","fire",at,.45f,.6f);break;
                case NativeSanctumCues.CenserOut:Cue("sanctum.censer.swing","dash",at,.7f,.5f);Cue(null,"fire",at,.35f,.8f);break;
                case NativeSanctumCues.CenserBack:Cue("sanctum.censer.back","dash",at,.6f,.45f);break;
                case NativeSanctumCues.FireKindle:Cue("sanctum.fire.kindle","fire",at,.4f,.75f);break;
                case NativeSanctumCues.FireRoar:Cue("sanctum.fire.roar","fire",at,.85f,1.05f);Cue(null,"burn-tick",at,.3f,.6f);break;
                case NativeSanctumCues.FireHiss:Cue("sanctum.fire.hiss","water",at,.25f,1.6f);break;
                case NativeSanctumCues.Toll:Cue("sanctum.bell.toll","metal",at,1f,.3f);Cue(null,"stone",at,.5f,.35f);break;
                case NativeSanctumCues.GateFall:Cue("sanctum.gate.fall","metal",at,.55f,.7f);break;
                case NativeSanctumCues.GateSlam:Cue("sanctum.gate.slam","metal",at,.95f,.5f);Cue(null,"stone",at,.6f,.6f);break;
                case NativeSanctumCues.GateRise:Cue("sanctum.gate.rise","metal",at,.35f,1.1f);break;
            }
        }
    }
}
