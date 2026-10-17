#if ARENA_TESTS
using System;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    internal static class ZoneBossRulesChecks
    {
        public static void Run()
        {
            int count=0;void Check(bool ok,string why){if(!ok)throw new Exception("Zone boss rules: "+why);count++;}
            var options=new JsonSerializerOptions{IncludeFields=true};
            foreach(string boss in new[]{NativeZoneBossRules.Station,NativeZoneBossRules.Ironworks})
            {
                float first=boss==NativeZoneBossRules.Station?.65f:.7f,last=boss==NativeZoneBossRules.Station?.3f:.35f;
                Check(NativeZoneBossRules.Phase(boss,6000,6000)==1,"initial phase");Check(NativeZoneBossRules.Phase(boss,6000*first,6000)==2,"first threshold inclusive");Check(NativeZoneBossRules.Phase(boss,6000*last,6000)==3,"last threshold inclusive");
                var f=new NativeZoneBossFrame{archetype=boss,actor=10001,phase=3,phaseAt=1};
                string[] actions=boss==NativeZoneBossRules.Station?NativeStationBoss.Moves.Select(m=>m.Id).ToArray():NativeIronworksBoss.Actions.Select(a=>a.Id).ToArray();
                foreach(var id in actions)
                {
                    NativeZoneBossRules.Start(f,id,20,2,4,5,90);Check(f.Valid(),id+" valid");Check(f.hitAt>=20&&f.hitAt<=20+f.duration+.001,"absolute hit clock");
                    var decoded=JsonSerializer.Deserialize<NativeZoneBossFrame>(JsonSerializer.Serialize(f,options),options);Check(decoded.Valid()&&decoded.actionId==f.actionId&&decoded.hitAt==f.hitAt,"wire round trip");
                    var copy=f.Copy();copy.partAt[0]=55;copy.spillIds=new[]{10003};Check(f.partAt[0]==-100&&f.spillIds.Length==0,"snapshot arrays detached");
                }
                f.hazards=new[]{new NativeZoneBossHazardFrame{id=301,kind="pour",x=1,y=0,z=2,radius=3,hitAt=22,until=26}};
                Check(f.Valid(),"persistent hazard valid");var isolated=f.Copy();isolated.hazards[0].until=99;Check(f.hazards[0].until==26&&!isolated.Valid(),"hazard copy detached and lifetime bounded");
                var broken=f.Copy();broken.hazards[0].x=float.NaN;Check(!broken.Valid(),"nonfinite hazard rejected");broken=f.Copy();broken.hazards=new[]{broken.hazards[0],broken.hazards[0]};Check(!broken.Valid(),"duplicate hazards rejected");
                f.hazards=Array.Empty<NativeZoneBossHazardFrame>();
                if(boss==NativeZoneBossRules.Station){NativeZoneBossRules.Start(f,"alteration",20,-1,0,0,0);Check(Math.Abs(f.hitAt-21.8f)<.00001,"glass hitAt is pane contact");}
                NativeZoneBossRules.Start(f,"arrive",20,-1,0,0,0);Check(Math.Abs(f.duration-9.0678f)<.000001,"intro stays pinned");
                foreach(var field in typeof(NativeZoneBossFrame).GetFields().Where(x=>x.FieldType==typeof(float)))
                {var bad=f.Copy();field.SetValue(bad,float.NaN);Check(!bad.Valid(),field.Name+" rejects NaN");field.SetValue(bad,float.PositiveInfinity);Check(!bad.Valid(),field.Name+" rejects infinity");}
                var invalid=f.Copy();invalid.partAt=new[]{0f};Check(!invalid.Valid(),"bounded part array");invalid=f.Copy();invalid.spillIds=new int[100];Check(!invalid.Valid(),"bounded spill array");invalid=f.Copy();invalid.action="invented";Check(!invalid.Valid(),"unknown action refused");
            }
            var h=new NativeZoneHazardFrame{running=true,boss=true,converterAt=10,converterMask=1,overflowAt=-100};
            foreach(var pair in new[]{(9f,NativeHazardPhase.Idle),(10f,NativeHazardPhase.Warn),(12.49f,NativeHazardPhase.Warn),(12.5f,NativeHazardPhase.Active),(16.5f,NativeHazardPhase.Clear),(18.5f,NativeHazardPhase.Idle)})
            {h.clock=pair.Item1;Check(NativeIronworksHazards.At(NativeIronworksHazards.Converter,h.Ironworks).Phase==pair.Item2,"converter boundary "+pair.Item1);Check(!NativeIronworksHazards.At(NativeIronworksHazards.ConverterSouth,h.Ironworks).Live,"unselected vessel off");}
            h.converterMask=3;h.clock=13;Check(NativeIronworksHazards.At(NativeIronworksHazards.ConverterSouth,h.Ironworks).Live,"both vessels in phase three");Check(NativeIronworksHazards.At(NativeIronworksHazards.Ladle,h.Ironworks).Parked,"ladle remains parked");
            h.overflowAt=20;foreach(float t in new[]{0f,7.99f,8f,13.99f,14f}){h.clock=20+t;Check(NativeIronworksHazards.At(NativeIronworksHazards.PitOverflow,h.Ironworks).Live==(t%14<8),"overflow eight live / six cool");}
            for(int i=0;i<28;i++){NativeZoneBossRules.Droplet(7,i,3,out float x,out float z,out float delay);NativeZoneBossRules.Droplet(7,i,3,out float xx,out float zz,out float dd);Check(x==xx&&z==zz&&delay==dd&&x*x+z*z>=15.99f&&x*x+z*z<=110.26f&&delay>=2&&delay<3.8,"deterministic bounded rain");}
            Console.WriteLine("PASS zone boss rules ("+count+" checks)");
        }
    }
}
#endif
