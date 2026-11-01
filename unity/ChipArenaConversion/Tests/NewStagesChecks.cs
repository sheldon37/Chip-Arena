using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class NewStagesChecks
{
    public static void Run(string root)
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("M1 stages: "+why);}
        var o=new JsonSerializerOptions{IncludeFields=true};
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
        var maps=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),o);
        foreach(var map in maps.maps.Where(m=>new[]{"breakwater","harbor","skyframe","rig","polar","training"}.Contains(m.id)))
        {
            NativeMapRules.Validate(map);Check(!map.rotation,"rotation "+map.id);
            var nodes=map.nav.nodes.ToDictionary(n=>n.id);var adj=nodes.Keys.ToDictionary(k=>k,k=>new List<string>());
            foreach(var l in map.nav.links){adj[l.a].Add(l.b);adj[l.b].Add(l.a);}
            string Near(V3 p)=>nodes.Values.OrderBy(n=>(n.pos-p).Length).First().id;
            var first=Near(map.spawns.ffa[0].pos);var seen=new HashSet<string>{first};var q=new Queue<string>();q.Enqueue(first);
            while(q.Count>0)foreach(var n in adj[q.Dequeue()])if(seen.Add(n))q.Enqueue(n);
            foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))Check(seen.Contains(Near(p.pos)),"nav spawn "+map.id+" "+p.pos.x+","+p.pos.y+","+p.pos.z);
            foreach(var h in map.hazards)
            {
                for(int round=0;round<2;round++)for(double t=0;t<4;t+=.05)
                {
                    if(NativeStageHazards.City(h)||NativeStageHazards.Ward(h))Check(NativeWardZeroHazards.At(h,NativePhase.Fight,round,0,t).Phase==NativeHazardPhase.Idle,"arm delay "+h.id);
                    else Check(NativeFactoryHazards.State(h,round,NativeFactoryHazards.Clock(NativePhase.Fight,0,t)).Phase==NativeFactoryHazards.Parked,"arm delay "+h.id);
                }
            }
            if(map.id=="training")
            {
                Check(!NativeMapRules.ArenaSelectable(map),"not PvP");
                Check(!NativeDevGate.PanelAvailable(NativeDevContext.Of(true,true,false,false,false,false,false)),"public training grants no DEV tools");
                foreach(var p in map.trainingPads)Check(seen.Contains(Near(p.pos)),"pad nav "+p.id);
                foreach(var p in new[]{new V3(-40,.9f,-8),new V3(-40,4.5f,-10),new V3(-40,8.1f,-7),new V3(-44,2.9f,-25)})Check(seen.Contains(Near(p)),"training tower/platform nav");
                var room=new NativeRoomOptions{training=true,cooperative=true,expedition=true,teams=true,bots=7};room.Normalize(content);Check(!room.cooperative&&!room.expedition&&!room.teams&&room.bots==0,"public co-op room isolation");
                var match=new NativeMatch(content){Training=true,Teams=true,ShowIntros=false};match.Seats.Add(new NativeSeat{id=1,name="Tester",team=0});NativeTrainingRules.AddDummies(match,map);
                Check(match.Start(),"solo training starts with authored dummies");Check(!match.CanEarnProgression(1),"no farming progression");
                var meter=new TrainingDamageMeter(map.trainingPads[0],1000);meter.Hit(1,30,true,1,"rifle:Hit",10);meter.Hit(3,20,false,1,"pistol:Hit",20);
                var row=meter.Read(5);Check(row.total==50&&row.dps==10&&row.last==20&&!row.headshot&&row.distance==20,"meter hit fields");
                Check(meter.Read(6).dps==4&&meter.Read(8).dps==0&&meter.Read(8).total==50,"rolling expiry");
                var rail=map.trainingPads.Single(p=>p.kind=="strafe");var prev=NativeTrainingRules.Position(rail,0);
                for(double t=.01;t<30;t+=.01){var pos=NativeTrainingRules.Position(rail,t);Check((pos-prev).Length<=.0251&&pos.z>=rail.rail.a.z-.001&&pos.z<=rail.rail.b.z+.001,"smooth bounded rail");prev=pos;}
                var rows=map.trainingPads.Select((p,i)=>new TrainingDamageMeter(p,1000+i).Read(0)).ToArray();var seats=match.Seats.Select(s=>(s.id,s.bot,s.team)).ToArray();
                Check(NativeTrainingRules.ValidFrame(map,seats,rows),"replicated target contract");
                var full=Enumerable.Range(1,8).Select(i=>(i,false,0)).Concat(seats.Where(s=>s.id>=1000)).ToArray();
                Check(NativeTrainingRules.ValidFrame(map,full,rows),"eight humans and 28 targets");Check(!NativeTrainingRules.ValidFrame(map,full.Concat(new[]{(9,false,0)}).ToArray(),rows),"ninth human rejected");
                rows[0].dps=float.NaN;Check(!NativeTrainingRules.ValidFrame(map,seats,rows),"bad readout rejected");
            }
        }
        var crane=maps.maps.Single(m=>m.id=="skyframe").hazards.Single();
        for(int z=0;z<crane.zones.Length;z++)
        {
            double impact=crane.start+crane.offset+z*crane.period+crane.travel+crane.warn+crane.rise;
            var s=NativeFactoryHazards.State(crane,0,impact+.01);Check(s.Landed&&s.Lethal&&Math.Abs(s.LoadY-crane.zones[z].center.y)<.001,"landed elevation "+z);
            s=NativeFactoryHazards.State(crane,0,impact+.1);Check(!s.Lethal&&s.Landed,"impact grace "+z);
            s=NativeFactoryHazards.State(crane,0,impact+crane.dwell+NativeFactoryHazards.RegripSeconds+crane.hoist/2);Check(s.LoadY>crane.zones[z].center.y&&s.LoadY<crane.carry,"hoist elevation "+z);
        }
        var burn=maps.maps.Single(m=>m.id=="rig").hazards.First(h=>h.kind=="burner");
        Check(NativeStageHazards.DamageTicks(burn,1,0,burn.first+burn.warn-.01,burn.first+burn.warn+burn.live-.01)*burn.damage==80,"Deepwell full burst 80 HP");
        Check(NativeTrainingRules.RoomFits("breakwater",Enumerable.Range(1,4).Select(i=>new NativeSeat{id=i,team=i%2}),true),"2v2");
        Check(!NativeTrainingRules.RoomFits("breakwater",Enumerable.Range(1,5).Select(i=>new NativeSeat{id=i}),false),"FFA cap");
        Console.WriteLine("PASS M1 stages and training: "+count+" checks");
    }
}
