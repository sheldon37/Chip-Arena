using System;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    internal static class ZoneCombatChecks
    {
        public static void Run(string root)
        {
            int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("Zone combat: "+why);}
            var legacy=new PveEntry{kind="vent",surface="wall",allowed=Array.Empty<string>()};Check(NativeWardEntries.Allows(legacy,"shambler")&&!NativeWardEntries.Allows(legacy,"hulk"),"Unity empty array preserves legacy entry rules");legacy.allowed=new[]{"stoker"};Check(NativeWardEntries.Allows(legacy,"stoker")&&!NativeWardEntries.Allows(legacy,"shambler"),"authored nonempty allow list remains restrictive");
            var layout=NativeExpeditionLayout.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ExpeditionLayout.json")));
            var options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
            foreach(var map in new[]{"expedition-transit","expedition-foundry"})
            {
                var data=JsonSerializer.Deserialize<NativePveContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/PveContent.json")),options);
                int original=data.adds.Length;var area=layout.areas.Single(a=>a.map==map);NativeExpeditionLayout.BindContent(area,data);
                var kinds=NativeZoneCombat.Station(map)?NativeStationKinds.Kinds:NativeIronworksKinds.Kinds;
                Check(data.adds.Length==original+kinds.Length,"zone rows bound exactly once");
                NativeExpeditionLayout.BindContent(area,data);Check(data.adds.Length==original+kinds.Length,"binding idempotent");
                foreach(var k in kinds){Check(k.Shipped&&NativeHordeKinds.Known(k.Id)&&data.adds.Any(a=>a.id==k.Id&&a.hp>0),"shipped row "+k.Id);Check(data.entries.Any(e=>NativeWardEntries.Allows(e,k.Id)),"entry for "+k.Id);}
                for(int wave=1;wave<=4;wave++)foreach(var row in NativeZoneCombat.Encounter(map,wave,false))
                {Check(row.value>0&&data.adds.Any(a=>a.id==row.name),"mix resolves");Check(NativeZoneCombat.Cap(row.name,wave,false)>0,"mix respects first wave");}
                foreach(var e in data.entries)Check(e.allowed.SequenceEqual(area.spawns.Single(s=>s.entry.e==e.e).allow),"authored entry restrictions preserved");
            }
            Check(NativeZoneCombat.Wave(14,100)==1&&NativeZoneCombat.Wave(15,100)==2&&NativeZoneCombat.Wave(50,100)==3&&NativeZoneCombat.Wave(85,100)==4,"quota phase boundaries");
            Check(NativeZoneCombat.Cap("rigger",1,false)==0&&NativeZoneCombat.Cap("rigger",2,false)==1&&NativeZoneCombat.Cap("rigger",3,false)==2,"rigger caps");
            Check(NativeZoneCombat.Cap("ladleman",2,false)==0&&NativeZoneCombat.Cap("ladleman",3,false)==1&&NativeZoneCombat.Cap("ladleman",4,false)==2,"ladleman caps");
            var iron=layout.Area("exp-foundry");Check(iron.spawns.Count(e=>e.allow.Contains("rigger"))==8&&iron.gates.Single().opens=="boss","eight hangs and boss shutter");
            var f=new NativeZoneEnemyFrame();Check(f.Valid()&&float.IsNaN(f.Station.arcAt)&&float.IsNaN(f.Ironworks.shedAt)&&NativeIronworksKinds.Crusted(f.Ironworks.shedAt,10),"finite empty wire preserves visual sentinels");
            f.arcAt=5;f.arcTargets=new[]{1,2,3};f.grabTarget=2;f.grabAt=6;f.releaseAt=7;f.shedAt=8;f.hang=45;f.lockAt=9;
            var copy=JsonSerializer.Deserialize<NativeZoneEnemyFrame>(JsonSerializer.Serialize(f,options),options);Check(copy.Valid()&&copy.Station.Holding(6.5f)&&!copy.Station.Holding(7)&&copy.Ironworks.hang==45,"roundtrip events and hold end");
            var clone=f.Copy();clone.arcTargets[0]=7;Check(f.arcTargets[0]==1,"detached event arrays");
            f.arcTargets=new[]{1,1};Check(!f.Valid(),"duplicate chain targets rejected");f.arcTargets=new[]{1};f.lockAt=float.NaN;Check(!f.Valid(),"NaN rejected");f.lockAt=9;f.attack="unknown";Check(!f.Valid(),"unknown special rejected");
            var h=new NativeZoneHazardFrame{running=true,start=0,clock=56,trainAt=60};var view=new NativeStationView();h.Fill(view,56);var state=new NativeStationState();state.Evaluate(view,56);Check(state.Express==NativeStationState.ExpressWarn,"full four-second express warning");state.Evaluate(view,60);Check(state.Express==NativeStationState.ExpressPass,"express starts on clock");
            h.leverAt=58;h.leverUntil=118;h.trainAt=122;h.Fill(view,100);state.Evaluate(view,100);Check(state.Express==NativeStationState.ExpressHalted&&state.Rails==0&&!state.Flood,"lever cuts hazards");h.Fill(view,118);state.Evaluate(view,118);Check(state.Express==NativeStationState.ExpressWarn&&state.Rails==31,"lever resumes full warning");
            h.clock=4;h.wave=1;Check(NativeIronworksHazards.At(NativeIronworksHazards.Ladle,h.Ironworks).Live,"wave one pour after 3.5 warning");h.boss=true;Check(NativeIronworksHazards.At(NativeIronworksHazards.Ladle,h.Ironworks).Parked&&!NativeIronworksHazards.At(NativeIronworksHazards.Ladle,h.Ironworks).Live,"boss parks pour");
            var other=new NativeZoneHazardFrame();Check(!other.running&&other.clock==0&&other.trainAt<0,"instance clocks independent");h.glassAt=new[]{3f};Check(!h.Valid(),"mismatched glass arrays rejected");
            Console.WriteLine("PASS zone combat: "+count+" checks (content, authored spawns, quota mixes/caps, finite state, copy isolation, warning and lever clocks)");
        }
    }
}
