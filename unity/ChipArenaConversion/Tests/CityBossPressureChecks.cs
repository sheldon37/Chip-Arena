using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class CityBossPressureChecks
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception("City boss pressure: "+why);}
        foreach(var boss in new[]{NativeZoneBossRules.Station,NativeZoneBossRules.Ironworks})
        for(int phase=1;phase<=3;phase++)for(int y=0;y<=16;y+=2)for(int x=-50;x<=50;x+=5)for(int z=-60;z<=40;z+=5)
        {
            var clock=new NativeCityBossPressure();var feet=new V3(x,y,z);
            Check(!clock.Schedule(boss,1,0,feet,out _),"initial grace");Check(!clock.Schedule(boss,1,5.99f,feet,out _),"grace not early");
            Check(clock.Schedule(boss,1,6,feet,out var h)&&h.Valid(),"bounded response anywhere");
            Check(h.hitAt==7.5f&&h.hitAt<=NativeCityBossPressure.Deadline&&NativeCityBossPressure.Contact(h,1,feet,.42f),"standing target hit by deadline on own floor");
            Check(!NativeCityBossPressure.Contact(h,2,feet,.42f),"stacked pilots do not multiply damage");
            Check(!NativeCityBossPressure.Contact(h,1,new V3(x+4,y,z),.42f),"locked area can be dodged");
            Check(!NativeCityBossPressure.Contact(h,1,new V3(x,y-4,z),.42f)&&!NativeCityBossPressure.Contact(h,1,new V3(x,y+4,z),.42f),"other floors safe from this impact");
        }
        foreach(var boss in new[]{NativeZoneBossRules.Station,NativeZoneBossRules.Ironworks})
        {
            var moving=new NativeCityBossPressure();int warnings=0;float last=0;
            for(int tick=0;tick<=3600;tick++)
            {
                float now=tick/60f;var position=new V3((float)(45*Math.Cos(now)),8,(float)(35*Math.Sin(now)));
                if(moving.Schedule(boss,1,now,position,out var h)){warnings++;Check(now-last<6.1f,"moving or circling target receives bounded threats");last=now;Check(NativeCityBossPressure.Contact(h,1,position,.42f),"moving target threatened at lock position");}
            }
            Check(warnings==10,"no moving-player starvation over one minute");
        }
        var party=new NativeCityBossPressure();for(int seat=1;seat<=8;seat++)party.Schedule(NativeZoneBossRules.Station,seat,0,new V3(seat*3,6,0),out _);
        var hazards=new NativeZoneBossHazardFrame[8];for(int seat=1;seat<=8;seat++)Check(party.Schedule(NativeZoneBossRules.Station,seat,6,new V3(seat*3,6,0),out hazards[seat-1]),"every party member gets independent warning");
        Check(hazards.Select(h=>h.id).Distinct().Count()==8,"unique pressure IDs");
        var frame=new NativeZoneBossFrame{archetype=NativeZoneBossRules.Station,actor=10000,hazards=hazards};
        var wire=JsonSerializer.Deserialize<NativeZoneBossFrame>(JsonSerializer.Serialize(frame,new JsonSerializerOptions{IncludeFields=true}),new JsonSerializerOptions{IncludeFields=true});
        Check(wire.Valid()&&wire.hazards[7].targetSeat==8&&wire.hazards[7].y==6,"late join/replay contract retains floor and target");
        var copy=wire.Copy();copy.hazards[0].y=0;Check(wire.hazards[0].y==6,"snapshot ownership");copy.hazards[0].targetSeat=-1;Check(!copy.Valid(),"pressure requires target");
        party.Remove(1);Check(!party.Schedule(NativeZoneBossRules.Station,1,100,new V3(),out _),"returning pilot gets fresh warning cycle");
        party.Reset();Check(!party.Schedule(NativeZoneBossRules.Station,2,100,new V3(),out _),"new encounter resets timers");
        var route=new NativeConverterRoute.Path();var at=new Vector3(-35,0,0);var target=new Vector3(20,0,0);
        bool Clear(Vector3 a,Vector3 b){for(int i=0;i<=20;i++){var p=Vector3.Lerp(a,b,i/20f);if(p.X>0&&p.X<8&&Math.Abs(p.Z)<3)return false;}return true;}
        Check(route.Plan(at,target,Clear),"converter can route around casting-floor obstacle");int steps=0;bool detour=false;
        while(route.Next(at,out var goal)&&steps++<500){Check(Clear(at,goal)&&NativeConverterRoute.Contains(goal.X,goal.Z),"footprint-checked segment");detour|=Math.Abs(goal.Z)>=4;at=goal;}
        Check(detour&&at.X>=18&&steps<500,"bounded detour reaches target side");Check(!route.Plan(at,target,(a,b)=>false)&&!route.Next(at,out _),"closed door discards stale route");
        Console.WriteLine("PASS city boss pressure: "+checks+" assertions; 23814 positions/phases, all 8 seats, fixed warning, dodge/floor separation, wire and blocked-route fallback contract");
    }
}
