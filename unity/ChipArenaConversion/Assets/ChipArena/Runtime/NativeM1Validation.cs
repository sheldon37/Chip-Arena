#if UNITY_EDITOR || ARENA_VALIDATE
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidateM1Runtime()
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("M1 runtime "+MapId+": "+why);}
            ReplicaScene=root.scene;
            foreach(var h in Map.hazards.Where(h=>h.kind=="crane"))
            {
                for(int i=0;i<h.zones.Length;i++)
                {
                    double at=4+h.start+h.offset+i*h.period+h.travel+h.warn+h.rise+.1;
                    Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=at,fightAt=0,round=1,set=1});
                    // StageRound is deliberately read here rather than assuming how sets map to crane zones.
                    var state=NativeFactoryHazards.State(h,StageRound,NativeFactoryHazards.Clock(Match.Phase,Match.FightAt,at));
                    PoseStageLoads(at);Check(stageLoads[h.id].enabled&&Math.Abs(stageLoads[h.id].transform.position.y-h.size.y/2-state.LoadY)<.001,"landed collider at zone height");
                }
            }
            if(Training)
            {
                Check(Pilots.Count==30,"two humans plus 28 dummies");
                Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10,fightAt=0,teams=true,round=1,set=1});
                var target=Pilots.First(TrainingDummy);target.ShieldUntil=0;float hp=target.Hp;
                float dealt=Damage(target,Local,17,Vector3.zero,0,true,forced:true,direct:false,primary:false);
                Check(dealt>0&&TrainingReadouts().Single(r=>r.actor==target.Seat.id).last==dealt,"damage routed to meter");
                var wire=NativeFrameCodec.Clone(NetworkFrame());Check(wire.Valid(Content)&&wire.trainingReadouts.Length==28,"real JsonUtility 30-actor training frame");
                Damage(target,Local,100000,Vector3.zero,0,false,forced:true,direct:false,primary:false);
                Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=16,fightAt=0,teams=true,round=1,set=1});TrainingStep();
                Check(target.Hp==hp&&target.Death==null,"dummy respawn");
                Check(ResetTraining()&&TrainingReadouts().All(r=>r.total==0)&&AllBlocks.Count()==Map.blocks.Length&&Pilots.All(p=>p.Seat.alive==p.Seat.connected),"reset restores props, living flags and readouts");
            }
            Debug.Log("PASS M1 runtime "+MapId+": elevated loads / training authority where applicable");
        }
    }
}
#endif
