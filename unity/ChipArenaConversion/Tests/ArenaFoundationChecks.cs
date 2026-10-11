using System;
using System.Linq;
using System.IO;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class ArenaFoundationChecks
{
    private static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Arena foundation: "+message);}
    private static T Copy<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value,Json),Json);
    public static void Run(string root,ConversionContent legacy)
    {
        var data=Copy(legacy);string before=JsonSerializer.Serialize(data.maps,Json);
        NativeMapRules.Append(data,JsonSerializer.Deserialize<MapCatalog>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ArenaMaps.json")),Json));
        Check(data.maps.Length==legacy.maps.Length+1&&JsonSerializer.Serialize(data.maps.Take(legacy.maps.Length),Json)==before,"legacy maps byte-equivalent data");
        foreach(var m in legacy.maps)
        {
            var bounds=NativeMapRules.Bounds(m);Check(bounds.minX==-32&&bounds.maxX==32&&bounds.minZ==-28&&bounds.maxZ==28&&bounds.killY==-6&&bounds.cleanupY==-15&&bounds.ceiling==24,"legacy footprint "+m.id);
            for(int i=0;i<8;i++)Check(Same(NativeMapRules.Spawn(data,m,i,i,0,true),data.spawns[i]),"legacy spawn "+m.id);
        }
        var map=data.Map("arena-1-lab");Check(!NativeMapRules.Rotation(data).Any(m=>m.experimental),"experimental random exclusion");
        var room=new NativeRoomOptions{map=map.id};room.Normalize(data);Check(room.map!=map.id,"option required");room.experimentalMaps=true;room.map=map.id;room.Normalize(data);Check(room.map==map.id,"explicit experimental opt-in");
        var boundsNew=NativeMapRules.Bounds(map);Check(Same(boundsNew.Clamp(new V3(90,4,-90)),new V3(39,4,-17)),"asymmetric aim clamp");
        Check(NativeSpecialRules.Ceiling(new V3(36,26,0),new V3(0,4,0),.1f,out _,out var ceiling,boundsNew)&&Math.Abs(ceiling.y-27.885f)<.001,"per-map ceiling footprint");
        Check(!NativeSpecialRules.Ceiling(new V3(36,26,0),new V3(0,4,0),.1f,out _,out _),"legacy ceiling preserved");
        Reject(data,map,m=>m.gameplay.ffa[0]=new V3(100,.9f,0),"out-of-bounds spawn");
        Reject(data,map,m=>m.gameplay.ffa[0]=new V3(-12,.9f,0),"spawn collapse zone");
        Reject(data,map,m=>m.gameplay.maxFalling=1,"oversize section");
        Reject(data,map,m=>m.blocks.First(b=>b.id=="west-pillar-0").anchor=false,"unanchored pillar");
        Reject(data,map,m=>m.blocks.First(b=>b.id=="west-pillar-0").support=new[]{"east-pillar-0"},"cross-building support");
        Reject(data,map,m=>m.blocks.First(b=>b.id=="west-pillar-0").rotation.y=.2f,"rotated gameplay box");
        var teamMap=Copy(map);teamMap.gameplay.teams=Enumerable.Range(0,8).Select(t=>new MapTeamSpawns{team=t,points=map.gameplay.ffa.Reverse().ToArray()}).ToArray();NativeMapRules.Validate(data,teamMap);Check(Same(NativeMapRules.Spawn(data,teamMap,0,3,2,true),map.gameplay.ffa.Reverse().ElementAt(2)),"team-specific spawn and seat index");
        var a=new NativeStructure(data,map,72);var b=new NativeStructure(data,map,72);var initial=a.Capture();
        a.Damage("west-pillar-0",30,new V3(),new V3(0,1,0),new V3(0,0,4),"explosion",1,0);Check(a.Find("west-pillar-0").damageStage==1,"material durability damage stage");
        Check(initial.blocks.First(x=>x.id=="west-pillar-0").health==85,"snapshot immutability");
        a=new NativeStructure(data,map,72);
        for(int t=0;t<360;t++)
        {
            if(t==1||t==4){foreach(var s in new[]{a,b})foreach(var id in map.blocks.Where(d=>d.anchor&&d.group.StartsWith(t==1?"west":"east")).Select(d=>d.id).Reverse())s.Damage(id,1000,new V3(1,2,3),new V3(0,1,0),new V3(5,2,0),"explosion",1,t);}
            a.Step(t);b.Step(t);Check(JsonSerializer.Serialize(a.Capture(),Json)==JsonSerializer.Serialize(b.Capture(),Json),"determinism tick "+t);
            Check(a.Falling<=map.gameplay.maxFalling&&a.ActiveGroups<=map.gameplay.maxCollapses,"caps tick "+t);
            if(t==2)Check(a.Blocks.Any(s=>s.state==StructureState.Telegraph)&&a.Falling==0,"warning before falling");
            if(t%11==0){var wire=Copy(a.Capture());Check(NativeStructure.Valid(data,map,wire),"wire validity");var guest=new NativeStructure(data,map);guest.Restore(wire);Check(JsonSerializer.Serialize(guest.Capture(),Json)==JsonSerializer.Serialize(a.Capture(),Json),"guest/late join restore at "+t);}
        }
        Check(a.Blocks.Count(s=>s.state==StructureState.Settled)==10,"all disconnected dependants settle");
        Check(a.Events.Any(e=>e.kind==StructureEventKind.CollapseStart)&&a.Events.Any(e=>e.kind==StructureEventKind.CollapseSettled),"visual event contract");
        Check(a.Events.Where(e=>e.kind==StructureEventKind.CollapseStart).All(e=>a.Events.Any(t=>t.kind==StructureEventKind.CollapseTelegraph&&t.group==e.group&&e.tick-t.tick>=45)),"full telegraph on queued sections");
        Check(a.Blocks.Where(s=>s.state==StructureState.Settled).All(s=>!NativeMapRules.Protected(data,map,s.position,s.size.x/2,s.size.z/2)),"settled rubble spawn exclusion");
        var poisoned=Copy(a.Capture());poisoned.blocks[0].position.x=float.NaN;Check(!NativeStructure.Valid(data,map,poisoned),"bad wire rejected");
        var replay=new NativeStructure(data,map);replay.Restore(initial);Check(replay.Blocks.All(s=>s.state==StructureState.Intact),"replay rewinds destruction");replay.Restore(Copy(a.Capture()));Check(replay.Blocks.Count(s=>s.state==StructureState.Settled)==10,"replay final cover restored");
        var occupied=new NativeStructure(data,map);foreach(var id in map.blocks.Where(d=>d.anchor).Select(d=>d.id))occupied.Damage(id,1000,new V3(),new V3(),new V3(),"area",1,0);
        for(int t=0;t<240;t++)occupied.Step(t,(pos,size)=>true);Check(occupied.Falling>0&&!occupied.Blocks.Any(s=>s.state==StructureState.Settled),"occupied landing never enables rubble");
        for(int t=240;t<480;t++)occupied.Step(t,(pos,size)=>false);Check(occupied.Blocks.Count(s=>s.state==StructureState.Settled)==10,"landing resumes after clearance");
        var cycle=Copy(map);var x=cycle.blocks.First(d=>d.id=="west-deck-0");x.support=new[]{"west-upper-box"};cycle.blocks.First(d=>d.id=="west-upper-box").support=new[]{x.id};bool rejected=false;try{NativeMapRules.Validate(data,cycle);}catch(InvalidOperationException){rejected=true;}Check(rejected,"cycle cannot support itself");
        var hazardMap=Copy(map);hazardMap.gameplay.hazards=new[]{new MapHazard{id="test",kind="heat",position=new V3(0,0,8),size=new V3(2,1,2),off=1,warning=.5f,active=1}};var hs=new NativeStructure(data,hazardMap);hs.Step(0);hs.Step(60);hs.Step(90);Check(hs.Hazards.Single().phase==2&&hs.Events.Count(e=>e.kind==StructureEventKind.HazardPhaseChange)==3,"hazard phase replication");
        var nav=new NativeArenaNavigation(map);var fresh=new NativeStructure(data,map);for(int i=0;i<40&&!nav.Ready;i++)nav.Update(fresh);Check(nav.Ready,"bounded nav rebuild");
        Check(nav.Direction(new V3(0,.9f,-10),new V3(0,.9f,10),out _).z>0,"open route");
        int rev=nav.Revision;for(int i=0;i<40&&nav.Revision!=a.Revision;i++)nav.Update(a);Check(nav.Revision>rev,"rubble invalidates routes even without prop count change");
        Check(nav.Direction(new V3(-12,5.2f,0),new V3(-12,5.2f,2),out _).Length<.01f,"removed upper floor not navigable");
        var cappedMap=Copy(map);cappedMap.gameplay.maxFalling=4;var capped=new NativeStructure(data,cappedMap);
        foreach(var id in cappedMap.blocks.Where(d=>d.anchor).Select(d=>d.id))capped.Damage(id,1000,new V3(),new V3(),new V3(),"explosion",1,0);
        bool queued=false;for(int t=0;t<600;t++){capped.Step(t);Check(capped.Falling<=4&&capped.ActiveGroups<=2,"hard cap under queued whole-building collapse");queued|=capped.Falling==4&&capped.Blocks.Any(x=>x.state==StructureState.Telegraph);}
        Check(queued&&capped.Blocks.Count(x=>x.state==StructureState.Settled)==10,"budget queue drains");
        var burst=new NativeStructure(data,map);for(int i=0;i<1000;i++)burst.Damage("west-pillar-0",.001f,new V3(),new V3(),new V3(),"area",1,0);
        Check(burst.Events.Count()==NativeStructure.EventCapacity&&burst.Capture().sequence==1000,"bounded visual events retain full block state");
        var mismatched=Copy(map);mismatched.blocks.First(d=>d.id=="west-pillar-0").mass+=1;Check(!NativeStructure.Valid(data,mismatched,a.Capture()),"different gameplay catalogs reject frame");
        var visualOnly=Copy(map);visualOnly.visual.json="arbitrary scenery";Check(NativeMapRules.Fingerprint(data,map)==NativeMapRules.Fingerprint(data,visualOnly),"presentation never enters simulation identity");
        WireRoundTrip(data,map,initial,a.Capture());
        Console.WriteLine("PASS arena foundation: legacy bounds/spawns, experimental gating, validation, deterministic collapse, wire/late join/replay, caps, safe landings, hazards and navigation");
    }
    private static void WireRoundTrip(ConversionContent data,ContentMap map,params StructureSnapshot[] snapshots)
    {
        using(var listener=new NativeListener(0))
        foreach(var snapshot in snapshots)
        using(var guest=NativeConnection.Connect("127.0.0.1",listener.Port,System.Threading.CancellationToken.None).GetAwaiter().GetResult())
        {
            Check(System.Threading.SpinWait.SpinUntil(()=>!listener.Accepted.IsEmpty,3000),"structure TCP connection");listener.Accepted.TryDequeue(out var host);
            using(host){host.Send(JsonSerializer.Serialize(snapshot,Json));Check(System.Threading.SpinWait.SpinUntil(()=>!guest.Received.IsEmpty,3000),"structure TCP delivery");guest.Received.TryDequeue(out var json);var state=JsonSerializer.Deserialize<StructureSnapshot>(json,Json);var replica=new NativeStructure(data,map);replica.Restore(state);Check(JsonSerializer.Serialize(replica.Capture(),Json)==JsonSerializer.Serialize(snapshot,Json),"real framed transport / fresh late join collision state");}
        }
    }
    private static bool Same(V3 a,V3 b)=>a.x==b.x&&a.y==b.y&&a.z==b.z;
    private static void Reject(ConversionContent data,ContentMap map,Action<ContentMap> edit,string why)
    {var value=Copy(map);edit(value);bool rejected=false;try{NativeMapRules.Validate(data,value);}catch(InvalidOperationException){rejected=true;}Check(rejected,"validation rejects "+why);}
}
