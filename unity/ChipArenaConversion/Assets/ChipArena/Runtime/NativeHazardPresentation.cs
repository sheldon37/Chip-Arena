using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation
    {
        private static readonly Vector4 SpikeActive=Linear(0xff263b),SpikeWarning=Linear(0xffb21e),SpikeIdle=Linear(0x122527),LavaGlow=Linear(0xff3910);
        // three.js converts hex colours to linear working space; the exported vertex colours and emission are linear too.
        private static Vector4 Linear(int hex){var c=NativeArena.ColorOf(hex).linear;return new Vector4(c.r,c.g,c.b,1);}
        // Port of client/stage-hazards.ts update(): the shared hazard clock (the simulation's damage source) places and
        // animates the exported source parts every rendered frame, so the visuals match the authoritative contact volumes.
        // The smoothed render clock runs at most one fixed step ahead of the simulation clock; anything else falls back to it.
        private float hazardClock;
        private float PresentationClock(NativeArena arena)=>hazardClock>=arena.Time&&hazardClock-arena.Time<.1f?hazardClock:arena.Time;
        private void HazardPresentation(NativeArena arena)
        {
            float time=PresentationClock(arena);
            foreach(var h in NativeHazards.At(arena.MapId,time))
            {
                var root=Matrix4x4.Translate(new Vector3(h.x,h.y,h.z));
                if(h.kind=="saw"){sourceModels.Queue("hazard-saw",root*Matrix4x4.Rotate(Quaternion.Euler(0,0,-(time*12%(2*Mathf.PI))*Mathf.Rad2Deg)));sourceModels.Queue("hazard-saw-track",Matrix4x4.Translate(new Vector3(0,0,h.z)));continue;}
                if(h.kind=="spikes")
                {
                    sourceModels.Queue("hazard-spikes",root);sourceModels.Queue("hazard-spikes-cones",root*Matrix4x4.Scale(new Vector3(1,.025f+h.extension*.975f,1)));
                    float intensity=h.warning?.4f+Mathf.Sin(time*18)*.3f:h.active?.7f:.2f;
                    sourceModels.Queue("hazard-spikes-surface",root,glow:(h.active?SpikeActive:h.warning?SpikeWarning:SpikeIdle)*intensity);continue;
                }
                sourceModels.Queue("hazard-lava",root);sourceModels.Queue("hazard-lava-surface",root,glow:LavaGlow*(.85f+Mathf.Sin(time*3+h.x)*.12f));
                sourceModels.Queue("hazard-lava-currents",Matrix4x4.Translate(new Vector3(h.x,h.y,h.z+Mathf.Sin(time*.7f+h.x)*.3f)));
                for(int i=0;i<28;i++)
                {
                    float phase=(time*.65f+i*.137f)%1,size=Mathf.Sin(phase*Mathf.PI)*.14f;
                    sourceModels.Queue(i%2==0?"hazard-lava-bubble-0":"hazard-lava-bubble-1",Matrix4x4.TRS(new Vector3(h.x+Mathf.Sin(i*7)*1.6f,h.y+.05f+phase*1.4f,h.z-16+(i*3.71f)%32),Quaternion.identity,Vector3.one*size));
                }
            }
        }
    }
}
