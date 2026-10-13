using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using ChipArena.UnityConversion;
internal static class StageHazardChecks
{
    public static void Run(string root)
    {
        void Check(bool ok,string reason){if(!ok)throw new Exception("Stage simulation: "+reason);}
        var opt=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};string json=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"));
        var set=JsonSerializer.Deserialize<UnityMapContent>(json,opt);var city=set.maps.Single(m=>m.id=="cybercity");var factory=set.maps.Single(m=>m.id=="factory");
        foreach(var map in new[]{city,factory})
        {
            NativeMapRules.Validate(map);Check(!map.rotation,"excluded from random");Check(map.hazards.Length==(map==city?3:11),"hazard inventory");Check(map.blocks.Length==(map==city?702:353),"original block inventory");
            var nodes=map.nav.nodes.ToDictionary(n=>n.id);var adj=nodes.Keys.ToDictionary(id=>id,id=>new List<string>());
            // With every hazard-tagged link shut, the safe ground routes still connect every spawn.
            foreach(var l in map.nav.links.Where(l=>(l.hazards?.Length??0)==0)){adj[l.a].Add(l.b);adj[l.b].Add(l.a);}
            string Nearest(V3 p)=>nodes.Values.OrderBy(n=>(n.pos-p).Length).First().id;
            var first=Nearest(map.spawns.ffa[0].pos);var seen=new HashSet<string>{first};var queue=new Queue<string>();queue.Enqueue(first);
            while(queue.Count>0)foreach(var id in adj[queue.Dequeue()])if(seen.Add(id))queue.Enqueue(id);
            foreach(var spawn in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))Check(seen.Contains(Nearest(spawn.pos)),"closed-hazard spawn connectivity "+map.id);
            foreach(var h in map.hazards)Check(map.nav.links.Any(l=>l.hazards.Contains(h.id)),"nav coverage "+h.id);
            var bad=JsonSerializer.Deserialize<ContentMap>(JsonSerializer.Serialize(map,opt),opt);bad.hazards[0].period=float.NaN;
            bool rejected=false;try{NativeMapRules.Validate(bad);}catch(InvalidOperationException){rejected=true;}Check(rejected,"NaN refused");
            bad=JsonSerializer.Deserialize<ContentMap>(JsonSerializer.Serialize(map,opt),opt);bad.nav.links[0].hazards=new[]{"unknown"};rejected=false;try{NativeMapRules.Validate(bad);}catch(InvalidOperationException){rejected=true;}Check(rejected,"unknown nav hazard refused");
        }
        Check(city.segments.Length==2&&city.segments.All(s=>s.mode=="tear-hinge"&&s.rubble.Length==3),"billboard/rubble contract");
        Check(city.nav.nodes.Count(n=>n.safe)>=12,"alcove/exit nodes");
        foreach(var flight in NativeCyberCityStairs.Flights(city))
        {
            Check(flight.Steps.Length==16&&Math.Abs(flight.Width-1.8f)<.001f,"preserved flight treads/width "+flight.Id);
            Check(Math.Abs(flight.Low.y+4)<.001f&&Math.Abs(flight.High.y)<.001f,"floor-to-street ramp "+flight.Id);
            float run=Math.Abs(flight.High.x-flight.Low.x),slope=(flight.High.y-flight.Low.y)/run;
            Check(Math.Abs(run-8.8f)<.001f&&1/Math.Sqrt(1+slope*slope)>=NativeLocomotionRules.MinGroundY,"walkable ramp "+flight.Id);
            foreach(var step in flight.Steps)
            {
                Check(NativeCyberCityStairs.IsTread(city.id,step.id),"cosmetic corpse keeps tread support "+step.id);
                float top=step.pos.y+step.size.y*.5f;
                float ramp=flight.Low.y+Math.Abs(step.pos.x-flight.Low.x)*slope;
                Check(Math.Abs(step.size.x-.55f)<.001f&&ramp>=top-.001f&&ramp-top<=.251f,"ramp hugs original treads "+step.id);
            }
            // Restrict the graph to THIS flight: reaching street via another flight must not satisfy the check.
            float minX=Math.Min(flight.Landing.x,flight.Street.x)-.6f,maxX=Math.Max(flight.Landing.x,flight.Street.x)+.6f;
            var nodes=city.nav.nodes.Where(n=>n.pos.x>=minX&&n.pos.x<=maxX&&Math.Abs(n.pos.z-flight.Low.z)<.6f).ToDictionary(n=>n.id);
            var adj=nodes.Keys.ToDictionary(id=>id,id=>new List<string>());
            foreach(var l in city.nav.links.Where(l=>nodes.ContainsKey(l.a)&&nodes.ContainsKey(l.b)&&(l.hazards?.Length??0)==0))
            {adj[l.a].Add(l.b);adj[l.b].Add(l.a);}
            string Near(V3 p)=>nodes.Values.OrderBy(n=>(n.pos-p-new V3(0,.9f,0)).Length).First().id;
            string start=Near(flight.Landing),end=Near(flight.Street);var seen=new HashSet<string>{start};var queue=new Queue<string>();queue.Enqueue(start);
            while(queue.Count>0)foreach(var id in adj[queue.Dequeue()])if(seen.Add(id))queue.Enqueue(id);
            Check(seen.Contains(end),"independent landing-to-street nav route "+flight.Id);
            Check(city.nav.links.Any(l=>nodes.ContainsKey(l.a)&&nodes.ContainsKey(l.b)&&
                ((nodes[l.a].pos.y<.85f&&nodes[l.b].pos.y>=.85f)||(nodes[l.b].pos.y<.85f&&nodes[l.a].pos.y>=.85f))),"top step connects to promenade "+flight.Id);
        }
        var mag=city.hazards.Single(h=>h.kind=="maglev");var grid=city.hazards.First(h=>h.kind=="grid");
        Check(!NativeStageHazards.TrainSweep(mag,0,10,27.9,27.99,out _,out _),"before arrival");
        Check(NativeStageHazards.TrainSweep(mag,0,10,28.01,28.03,out var c,out var z),"entry sweep");
        Check(NativeStageHazards.Touch(new V3(-39.4f,-3.15f,0),c,z),"swept pilot caught");
        Check(!NativeStageHazards.Touch(new V3(-39.4f,-3.15f,3.8f),c,z)&&!NativeStageHazards.Touch(new V3(-39.4f,.9f,0),c,z),"alcove and bridge safe");
        double exit=28+(80+42)/44d;Check(NativeStageHazards.TrainSweep(mag,0,10,exit-.01,exit+.01,out c,out z)&&z.x>0,"last swept slice after exit");
        int total=0;for(int i=1;i<=1800;i++)total+=NativeStageHazards.DamageTicks(grid,1,10,(i-1)/60d,i/60d);Check(total==8,"exactly eight coupling ticks (including first, excluding end)");
        Check(NativeStageHazards.DamageTicks(grid,1,10,24.99,25)==1&&NativeStageHazards.DamageTicks(grid,1,10,26.99,27)==0,"grid boundaries");
        Check(NativeStageHazards.GridTouch(grid,new V3(-24,.85f,0))&&!NativeStageHazards.GridTouch(grid,new V3(-24,1.96f,0)),"grid feet/jump clearance");
        Check(NativeStaggerRules.Classify(null,"maglev",100)==StaggerWeight.None&&NativeStaggerRules.Classify(null,"grid",13)==StaggerWeight.None,"no hazard stagger");
        foreach(var kind in new[]{"maglev","grid","hazard","collapse",null,""})Check(NativeStageHazards.DeathKind(kind),"wire kind "+kind);Check(!NativeStageHazards.DeathKind("made-up"),"unknown wire kind");
        var crane=factory.hazards.Single(h=>h.kind=="crane");double impact=crane.start+crane.offset+crane.travel+crane.warn+crane.rise;
        var zone=crane.zones[NativeFactoryHazards.ZoneOf(crane,0,0)];
        foreach(double t in new[]{crane.start+.1,crane.start+crane.travel+.1,impact-.01,impact+.1,impact+crane.dwell+.1})Check(!NativeStageHazards.LinkOpen(crane,NativePhase.Fight,0,10,14+t,zone.center,zone.center),"crane nav blocks travel, warning, fall, landed and regrip");
        foreach(double t in new[]{impact+.1,impact+crane.dwell+.1})Check(NativeStageHazards.Landed(NativeFactoryHazards.State(crane,0,t)),"landed/regrip collider");
        Check(!NativeStageHazards.Landed(NativeFactoryHazards.State(crane,0,impact+crane.dwell+1.501)),"hoist removes collider");
        foreach(var h in factory.hazards)Check(!NativeFactoryHazards.State(h,0,NativeFactoryHazards.Clock(NativePhase.Fight,10,13.999)).Lethal,"4 second grace "+h.id);
        var saw=factory.hazards.First(h=>h.kind=="saw-slot");var state=NativeFactoryHazards.State(saw,0,saw.start+saw.warn+saw.rise+1);
        Check(NativeStageHazards.FactoryBox(saw,state,out c,out z)&&Math.Abs(c.y-z.y/2)<.0001,"slot clips to floor");
        Check(NativeStageHazards.Touch(new V3(c.x,.85f,c.z),c,z)&&!NativeStageHazards.Touch(new V3(c.x,.85f,c.z+.6f),c,z),"saw lethal line / .6 m aside");
        var legacy=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),opt);foreach(var m in legacy.maps)m.hazards=new[]{mag};NativeMapRules.Merge(legacy,set,json);Check(legacy.maps.Where(m=>!m.unity).All(m=>m.hazards==null),"legacy inline hazards normalized");
        Check(!NativeMapRules.Compatible(NativeConnection.Protocol,legacy.sourceCommit,NativeMapRules.Revision(json+" "),legacy),"exact content revision mismatch refused");
        Console.WriteLine("PASS stage simulation: both authored layouts, strict hazard schema, closed-hazard spawn routes, four cut ramps and independent landing-to-street nav routes, swept maglev, safe pockets/bridges, eight grid ticks, jumps, saw clearance, crane collider clock, legacy defaults and revision refusal");
    }
}
