using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public IEnumerable<NativeMarkView> Marks()
        {if(Replica)return replicaFrame?.marks??Array.Empty<NativeMarkView>();return Pilots.Where(p=>p.Siren?.Move?.Rule.Id=="mark"&&p.Siren.Move.Target!=null).Select(p=>new NativeMarkView{id=p.Siren.Move.Id,owner=p.Seat.id,target=p.Siren.Move.Target.Seat.id,lockAt=p.Siren.Move.At+1.2f,throwAt=p.Siren.Move.HitAt});}
        // Marks() for the per-frame presentation without allocating (Runtime/NativeDevEffects.cs): a replica's frame records,
        // or the authority's live mark moves in reused views. Read only; Marks() still feeds the network frame.
        private readonly List<NativeMarkView> liveMarks=new List<NativeMarkView>(),markViews=new List<NativeMarkView>();
        public List<NativeMarkView> LiveMarks()
        {
            liveMarks.Clear();
            if(Replica){var frame=replicaFrame?.marks;if(frame!=null)foreach(var m in frame)if(m!=null)liveMarks.Add(m);return liveMarks;}
            foreach(var p in Pilots)
            {
                var m=p.Siren?.Move;if(m?.Rule==null||m.Rule.Id!="mark"||m.Target==null)continue;
                if(markViews.Count==liveMarks.Count)markViews.Add(new NativeMarkView());var v=markViews[liveMarks.Count];
                v.id=m.Id;v.owner=p.Seat.id;v.target=m.Target.Seat.id;v.lockAt=m.At+1.2f;v.throwAt=m.HitAt;liveMarks.Add(v);
            }
            return liveMarks;
        }
        public float BossSongAt=-1,BossSongLead;private NativeBossTrack bossTrack;
        public NativeBossTrack BossTrack=>bossTrack??(bossTrack=JsonUtility.FromJson<NativePveContent>(Resources.Load<TextAsset>("PveContent").text).bossMusic);
        public double BossSongTime=>BossSongAt<0?-1:Time-BossSongAt+BossSongLead+(Pve?.Holding==true&&Pve.Cinematic=="intro"?Pve.HoldDuration-Pve.HoldLeft:0);
        public static readonly Vector3[] StageVents={new Vector3(-10,0,-9),new Vector3(10,0,9),new Vector3(-10,0,9),new Vector3(10,0,-9),new Vector3(0,0,-17),new Vector3(0,0,17),new Vector3(-21,0,-12),new Vector3(21,0,12)};
        private void StepSirenStage()
        {
            if(MapId!="siren"||Match.Phase!=NativePhase.Fight||BossSongTime<0)return;
            for(int i=0;i<StageVents.Length;i++)
            {
                if(!BossTrack.Vent(BossSongTime,i%2).active)continue;Vector3 vent=StageVents[i];
                foreach(var p in Pilots)
                {
                    if(p.Hp<=0||!p.Seat.connected)continue;Vector3 pos=p.Body.position;string key="vent:"+i+":"+p.Seat.id;
                    if(hazardHits.TryGetValue(key,out float until)&&Time<until||Math.Abs(pos.x-vent.x)>1.5f||Math.Abs(pos.z-vent.z)>1.5f||pos.y-.85f>2.2f||pos.y<-.5f)continue;
                    hazardHits[key]=Time+.65f;float actual=Damage(p,null,14,Vector3.up,0,false,direct:false,stage:true,damageKind:"stage-vent");
                    if(actual>0&&!SirenArmor(p)){p.Knockback.y=6.5f-p.Body.linearVelocity.y;p.Grounded=false;}Fx(vent,1.2f,4);
                }
            }
        }
    }
}
