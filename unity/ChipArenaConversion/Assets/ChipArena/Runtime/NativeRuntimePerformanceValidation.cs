#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidateRuntimePerformance()
        {
            int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception("Runtime performance: "+why);}
            var p=Pilots[0];var chips=p.Seat.chips.ToArray();var gauges=p.ChipGauges;
            string[] keys={"wild-planted","wild-opening-move","wild-doomsday-clock","wild-spin-up","wild-haymaker","wild-charged-reply","wild-spare-body"};
            try
            {
                for(int mask=0;mask<128;mask++)
                {
                    p.Seat.chips.Clear();for(int i=0;i<keys.Length;i++)if((mask&(1<<i))!=0)p.Seat.chips[keys[i]]=1;
                    UpdateChipGauges(p);var buffer=p.ChipGauges;var saved=(NativeChipGauge[])buffer.Clone();
                    Check(buffer.Select(g=>g.Kind).SequenceEqual(keys.Where((k,i)=>(mask&(1<<i))!=0)),"gauge order and membership "+mask);
                    UpdateChipGauges(p);Check(ReferenceEquals(buffer,p.ChipGauges),"stable loadout reuses buffer");
                    Check(saved.SequenceEqual(p.ChipGauges),"stable values remain identical");
                    var expected=new[]{p.ExpansionStill<0?0:Mathf.Clamp01(Time-p.ExpansionStill),Time-p.ExpansionLastHit>=4?1f:0,p.ExpansionDoomAt<0?0:Mathf.Clamp01((Time-p.ExpansionDoomAt)/3),p.Wild.Spin,Time>=p.Wild.HayAt?1f:0,p.Wild.ReplyUntil>Time?1f:0,p.Wild.DownUntil>Time?0f:1};
                    Check(buffer.Select(g=>g.Value).SequenceEqual(expected.Where((v,i)=>(mask&(1<<i))!=0)),"same gauge values as original formulas");
                    var snapshot=Snapshot();var captured=snapshot.Actors.First(a=>a.Id==p.Seat.id).ChipGauges;
                    if(buffer.Length>0){buffer[0].Value=.12345f;Check(captured[0].Value==saved[0].Value,"snapshot owns detached gauges");}
                }
                NetworkFrame();var old=frameProps.ToArray();frameProps["perf-removed-prop"]=new NativePropFrame{id="perf-removed-prop"};PruneFrameProps();
                Check(!frameProps.ContainsKey("perf-removed-prop"),"removed prop pruned");
                foreach(var x in old)Check(frameProps.TryGetValue(x.Key,out var value)&&ReferenceEquals(value,x.Value),"live immutable prop record retained");
                Debug.Log("PASS runtime performance: "+checks+" checks (128 gauge combinations, buffer reuse, snapshot ownership, prop pruning)");
            }
            finally{p.Seat.chips.Clear();foreach(var x in chips)p.Seat.chips[x.Key]=x.Value;p.ChipGauges=gauges;}
        }
    }
}
#endif
