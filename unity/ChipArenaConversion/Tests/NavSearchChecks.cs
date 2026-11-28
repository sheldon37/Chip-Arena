using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;

internal static class NavSearchChecks
{
    // Independent pre-fix algorithm, retained only as an equivalence/allocation benchmark.
    private static List<string> Legacy(Dictionary<string,MapNavNode> nodes,Dictionary<string,List<MapNavLink>> edges,string start,string end,Func<MapNavLink,bool> active)
    {
        var distance=new Dictionary<string,float>{{start,0}};var previous=new Dictionary<string,string>();var open=new List<string>{start};var closed=new HashSet<string>();
        while(open.Count>0)
        {
            string at=open.OrderBy(id=>distance[id]+(nodes[id].pos-nodes[end].pos).Length).ThenBy(id=>id,StringComparer.Ordinal).First();open.Remove(at);if(at==end)break;if(!closed.Add(at))continue;
            foreach(var l in edges[at]){string next=l.a==at?l.b:l.a;if(closed.Contains(next)||!active(l))continue;float cost=distance[at]+(nodes[next].pos-nodes[at].pos).Length;if(!distance.TryGetValue(next,out float old)||cost<old){distance[next]=cost;previous[next]=at;if(!open.Contains(next))open.Add(next);}}
        }
        var path=new List<string>();if(distance.ContainsKey(end)){string n=end;path.Add(n);while(n!=start){n=previous[n];path.Add(n);}path.Reverse();}return path;
    }
    public static void Run(string root)
    {
        void Check(bool ok,string why){if(!ok)throw new Exception("Navigation search: "+why);}
        var set=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),new JsonSerializerOptions{IncludeFields=true});
        CheckLocalNearest(set,Check);
        CheckHazardBroadPhase(set,Check);
        var map=set.maps.Single(m=>m.id=="siren");var search=new NativeNavSearch(map.nav);var result=new List<string>(map.nav.nodes.Length);
        Func<MapNavLink,bool> all=l=>true,closedHazards=l=>(l.hazards?.Length??0)==0;
        var nodes=map.nav.nodes.ToDictionary(n=>n.id);var edges=map.nav.nodes.ToDictionary(n=>n.id,n=>new List<MapNavLink>());
        foreach(var l in map.nav.links){edges[l.a].Add(l);if(l.kind!="drop")edges[l.b].Add(l);}
        Legacy(nodes,edges,map.nav.nodes[0].id,map.nav.nodes[map.nav.nodes.Length-1].id,all);
        int oldLinkChecks=0,newLinkChecks=0;
        long oldBytes=0,newBytes=0;double oldMs=0,newMs=0;
        for(int i=0;i<16;i++)
        {
            string a=map.nav.nodes[i*379%map.nav.nodes.Length].id,b=map.nav.nodes[(map.nav.nodes.Length-1-i*211)%map.nav.nodes.Length].id;var active=i%2==0?all:closedHazards;
            Func<MapNavLink,bool> oldActive=l=>{oldLinkChecks++;return active(l);},newActive=l=>{newLinkChecks++;return active(l);};
            long before=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();var expected=Legacy(nodes,edges,a,b,oldActive);watch.Stop();oldMs+=watch.Elapsed.TotalMilliseconds;oldBytes+=GC.GetAllocatedBytesForCurrentThread()-before;
            search.Find(a,b,newActive,result);Check(result.SequenceEqual(expected),"Ward route/tie equivalence "+i);
            before=GC.GetAllocatedBytesForCurrentThread();watch.Restart();search.Find(a,b,active,result);watch.Stop();newMs+=watch.Elapsed.TotalMilliseconds;newBytes+=GC.GetAllocatedBytesForCurrentThread()-before;
        }
        Check(newLinkChecks<oldLinkChecks,"reject non-improving links before live checks");
        Console.WriteLine($"PASS Ward live link queries: legacy {oldLinkChecks}, optimized {newLinkChecks} across 16 identical routes/gates; fixed graph endpoints/costs cached, live hazard/physics results never cached");
        Check(newBytes==0,"warmed route searches allocate "+newBytes+" bytes");
        Func<MapNavNode,bool> usable=n=>!n.safe;
        for(int i=0;i<8;i++){var point=map.spawns.ffa[i].pos;Check(search.Nearest(point,usable).id==map.nav.nodes.OrderBy(n=>V3.Dot(n.pos-point,n.pos-point)).First(usable).id,"nearest equivalence");}
        // Reproduce the emergency escape query: filter-before-sort evaluates
        // visibility across the entire floor, even when a nearby escape is clear.
        int oldEscapeChecks=0,newEscapeChecks=0,escapeCases=0;
        foreach(var escapeMap in set.maps.Where(m=>m.nav!=null))
        {
            var escapeSearch=new NativeNavSearch(escapeMap.nav);
            foreach(var spawn in escapeMap.spawns.ffa.Take(8))foreach(bool pocket in new[]{false,true})foreach(int blocked in new[]{0,3,int.MaxValue})
            {
                var point=spawn.pos;var ordered=escapeMap.nav.nodes.OrderBy(n=>V3.Dot(n.pos-point,n.pos-point)).ToArray();
                var obstruction=new HashSet<MapNavNode>(ordered.Take(blocked));
                bool Eligible(MapNavNode n)=>(!pocket||n.safe)&&Math.Abs(n.pos.y-point.y)<1.2;
                bool OldUsable(MapNavNode n){if(!Eligible(n))return false;oldEscapeChecks++;return !obstruction.Contains(n);}
                bool NewUsable(MapNavNode n){if(!Eligible(n))return false;newEscapeChecks++;return !obstruction.Contains(n);}
                var expected=escapeMap.nav.nodes.Where(OldUsable).OrderBy(n=>V3.Dot(n.pos-point,n.pos-point)).FirstOrDefault();
                Check(escapeSearch.Nearest(point,NewUsable)==expected,"escape destination equivalence "+escapeMap.id);escapeCases++;
            }
        }
        Check(newEscapeChecks<oldEscapeChecks,"escape query reduces visibility checks");
        Console.WriteLine($"PASS escape navigation: {escapeCases} clear/blocked/unreachable floor/pocket cases; visibility predicates {oldEscapeChecks} -> {newEscapeChecks}, same destinations (simulated obstruction, not PhysX)");
        var graph=new MapNav{nodes=new[]{new MapNavNode{id="upper",pos=new V3(0,6,0)},new MapNavNode{id="lower",pos=new V3(0,0,0)},new MapNavNode{id="isolated",pos=new V3(5,0,0)}},links=new[]{new MapNavLink{id="drop",a="upper",b="lower",kind="drop"}}};
        var small=new NativeNavSearch(graph);small.Find("upper","lower",all,result);Check(result.SequenceEqual(new[]{"upper","lower"}),"directed drop down");small.Find("lower","upper",all,result);Check(result.Count==0,"no reverse drop");small.Find("upper","isolated",all,result);Check(result.Count==0,"unreachable clears old path");small.Find("upper","upper",all,result);Check(result.SequenceEqual(new[]{"upper"}),"start equals end");small.Find("upper","lower",l=>false,result);Check(result.Count==0,"changed gate invalidates next search");
        small.BuildFlow("lower",all);small.TraceFlow("upper",result);Check(result.SequenceEqual(new[]{"upper","lower"}),"flow follows drop downward");
        small.BuildFlow("upper",all);small.TraceFlow("lower",result);Check(result.Count==0,"flow cannot reverse drop");small.TraceFlow("upper",result);Check(result.SequenceEqual(new[]{"upper"}),"flow at goal");
        small.BuildFlow("lower",l=>false);small.TraceFlow("upper",result);Check(result.Count==0,"flow refresh closes gate");
        small.BuildFlow("lower",all);small.TraceFlow("upper",result);Check(result.Count==2,"flow refresh reopens gate");small.TraceFlow("isolated",result);Check(result.Count==0,"flow unreachable clears stale route");
        float Cost(List<string> path){float cost=0;for(int i=1;i<path.Count;i++)cost+=(nodes[path[i]].pos-nodes[path[i-1]].pos).Length;return cost;}
        var flow=new NativeNavSearch(map.nav);var expectedPath=new List<string>(map.nav.nodes.Length);
        for(int gate=0;gate<2;gate++)
        {
            var active=gate==0?all:closedHazards;string end=map.nav.nodes[map.nav.nodes.Length-1].id;flow.BuildFlow(end,active);
            for(int i=0;i<16;i++)
            {
                string start=map.nav.nodes[i*379%map.nav.nodes.Length].id;search.Find(start,end,active,expectedPath);flow.TraceFlow(start,result);
                Check((result.Count==0)==(expectedPath.Count==0),"flow reachability "+i);
                Check(Math.Abs(Cost(result)-Cost(expectedPath))<.01f,"flow shortest cost "+i);
                for(int j=1;j<result.Count;j++)Check(edges[result[j-1]].Any(l=>(l.a==result[j]||l.b==result[j])&&active(l)),"flow directed/live edge");
            }
        }
        // Budgeted reverse search must produce the same trees as an atomic build.
        var incremental=new NativeNavSearch(map.nav);var partial=new List<string>(map.nav.nodes.Length);
        int slices=0,peakQueries=0;
        foreach(var active in new[]{all,closedHazards})
        {
            string end=map.nav.nodes[map.nav.nodes.Length-1].id;
            flow.BuildFlow(end,active);incremental.BeginFlow(end);
            int calls=0;Func<MapNavLink,bool> measured=l=>{calls++;return active(l);};
            do
            {
                calls=0;incremental.StepFlow(measured,128);slices++;peakQueries=Math.Max(peakQueries,calls);
                Check(calls<=128,"incremental flow respects physics-query budget");
                for(int i=0;i<16;i++)
                {
                    string start=map.nav.nodes[i*379%map.nav.nodes.Length].id;
                    if(incremental.TraceSettledFlow(start,partial))
                    {flow.TraceFlow(start,result);Check(partial.SequenceEqual(result),"partial settled tree matches complete tree");}
                }
                Check(slices<1000,"incremental flow terminates");
            }while(!incremental.FlowComplete);
            foreach(var node in map.nav.nodes)
            {incremental.TraceFlow(node.id,partial);flow.TraceFlow(node.id,result);Check(partial.SequenceEqual(result),"complete budgeted tree equivalence");}
        }
        // Restart discards partially processed edges and an old target/gate state.
        small.BeginFlow("lower");small.StepFlow(all,1);small.BeginFlow("upper");
        while(!small.StepFlow(all,1)){}small.TraceFlow("lower",result);Check(result.Count==0,"restart retains directed drops");
        small.BeginFlow("lower");while(!small.StepFlow(l=>false,1)){}small.TraceFlow("upper",result);Check(result.Count==0,"restart observes closed gate");
        Console.WriteLine($"PASS budgeted crowd navigation: {slices} slices, at most {peakQueries}/128 live queries per slice; all Ward nodes and settled partial paths match atomic searches");
        // Maximum hidden-phase load, with both destinations and dynamic gate variants.
        var crowdWatch=new Stopwatch();long crowdBytes=GC.GetAllocatedBytesForCurrentThread();crowdWatch.Start();
        for(int goal=0;goal<2;goal++)
        {
            flow.BuildFlow(map.nav.nodes[map.nav.nodes.Length-1-goal*211].id,goal==0?all:closedHazards);
            for(int i=0;i<192;i++)flow.TraceFlow(map.nav.nodes[i*31%map.nav.nodes.Length].id,result);
        }
        crowdWatch.Stop();crowdBytes=GC.GetAllocatedBytesForCurrentThread()-crowdBytes;
        Check(crowdBytes==0,"192-mob shared flow refresh allocates "+crowdBytes);
        foreach(float floor in new[]{0f,6f})foreach(float height in new[]{1.7f,NativeHordeKinds.HulkHeight,.88f})
        {
            var center=new V3(3,floor+height/2+.025f,5);var anchor=NativeNavSearch.BodyAnchor(center,height/2);
            Check(Math.Abs(anchor.y-floor-.875f)<.00001f&&anchor.x==3&&anchor.z==5,"feet-relative navigation anchor");
            Check(anchor.y+.3f-floor<1.65f,"support ray reaches floor at every body height");
            Check((anchor-new V3(3,floor+.9f,5)).Length<.65f,"waypoint reach at every body height");
            if(height==NativeHordeKinds.HulkHeight)Check(center.y+.3f-floor>1.65f&&(center-new V3(3,floor+.9f,5)).Length>.65f,"reproduces old Hulk support and waypoint failures");
        }
        Console.WriteLine($"PASS crowd navigation: 2 shared flow builds + 384 routes (192 mobs/destination), {crowdWatch.Elapsed.TotalMilliseconds:F1} ms/{crowdBytes} bytes; shortest costs, directed drops, changing gates, Hulk/crawler floor anchors (managed, excludes PhysX)");
        var box=new MapVolume{center=new V3(1,2,3),size=new V3(2,4,6)};
        for(int i=0;i<100;i++){var a=new V3(i*.1f-5,2,3);var b=new V3(0,i*.1f-5,4);Check(NativeStageHazards.Cross(a,b,box.center,box.size)==NativeStructureRules.Crosses(a,b,box,.45f),"box overload");}
        long allocation=GC.GetAllocatedBytesForCurrentThread();bool hit=false;for(int i=0;i<10000;i++)hit|=NativeStageHazards.Cross(new V3(-2,2,3),new V3(2,2,3),box.center,box.size);Check(GC.GetAllocatedBytesForCurrentThread()==allocation&&hit,"hazard geometry allocates no temporary volume");
        Console.WriteLine($"PASS navigation: Ward {map.nav.nodes.Length} nodes/{map.nav.links.Length} links, 16 legacy-equivalent routes/gates/ties, directed drops, unreachable, nearest, zero-allocation warm searches/cross tests; legacy {oldMs:F1} ms/{oldBytes} bytes, new {newMs:F1} ms/{newBytes} bytes (managed only; both exclude one-time adjacency setup and PhysX)");
    }
    private static bool LegacyEnvelope(MapHazard h,V3 a,V3 b)
    {
        if(NativeStageHazards.City(h)||NativeStageHazards.Ward(h))
        {
            if(h.kind=="burner")return (h.zones??Array.Empty<MapVolume>()).Any(z=>NativeStageHazards.Cross(a,b,z.center,z.size));
            return NativeStageHazards.Cross(a,b,h.center,h.size);
        }
        if(h.kind=="vent"&&h.damage<=0)return false;
        if(h.kind=="crane")return (h.zones??Array.Empty<MapVolume>()).Any(z=>NativeStageHazards.Cross(a,b,z.center,z.size));
        V3 c=(h.a+h.b)*.5f,size=new V3(Math.Abs(h.a.x-h.b.x)+h.size.x,Math.Abs(h.a.y-h.b.y)+h.size.y,Math.Abs(h.a.z-h.b.z)+h.size.z);
        if(h.kind=="vent"){c=h.a+new V3(0,h.size.y/2,0);size=h.size;}
        return NativeStageHazards.Cross(a,b,c,size);
    }
    private static void CheckLocalNearest(UnityMapContent set,Action<bool,string> check)
    {
        int cases=0,baselineCalls=0,boundedCalls=0;
        foreach(var map in set.maps.Where(m=>m.nav!=null))
        {
            var search=new NativeNavSearch(map.nav);
            var points=map.spawns.ffa.Select(p=>p.pos).Concat(map.nav.nodes.Where((n,i)=>i%79==0).Select(n=>n.pos)).ToArray();
            foreach(var point in points)foreach(int blocked in new[]{0,3,16,64})
            {
                var ordered=map.nav.nodes.OrderBy(n=>V3.Dot(n.pos-point,n.pos-point)).ToArray();
                var rejected=new HashSet<MapNavNode>(ordered.Take(blocked));
                var expected=ordered.Where(n=>V3.Dot(n.pos-point,n.pos-point)<=64).Take(64).FirstOrDefault(n=>!rejected.Contains(n));
                int calls=0;var actual=search.NearestLocal(point,n=>{calls++;return !rejected.Contains(n);});
                check(ReferenceEquals(actual,expected),"local nearest exhaustive oracle "+map.id);
                check(calls<=64,"local attachment predicate budget");cases++;
                if(blocked==0)check(actual!=null,"authored spawns/nodes have a local attachment "+map.id);
            }
            var at=points[0];
            search.Nearest(at,n=>{baselineCalls++;return false;});
            search.NearestLocal(at,n=>{boundedCalls++;return false;});
            int outside=0;check(search.NearestLocal(new V3(100000,100000,100000),n=>{outside++;return true;})==null&&outside==0,"off-map returns without any world checks");
            Func<MapNavNode,bool> reject=n=>false;search.NearestLocal(at,reject);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<100;i++)search.NearestLocal(at,reject);
            check(GC.GetAllocatedBytesForCurrentThread()==before,"local attachment allocation free");
        }
        check(boundedCalls<baselineCalls,"blocked origin no longer scans the map");
        Console.WriteLine($"PASS bounded nearest: {cases} exhaustive-oracle cases, authored spawn/node coverage, zero warm allocations; blocked predicates {baselineCalls} -> {boundedCalls}");
    }

    private static void CheckHazardBroadPhase(UnityMapContent set,Action<bool,string> check)
    {
        int comparisons=0;double oldMs=0,newMs=0;
        foreach(var map in set.maps.Where(m=>m.nav!=null&&m.hazards!=null))
        {
            var index=new NativeNavHazards(map.nav,map.hazards);var nodes=map.nav.nodes.ToDictionary(n=>n.id);
            bool Old(V3 a,V3 b,NativePhase phase,int round,double now)
            {foreach(var h in map.hazards)if(LegacyEnvelope(h,a,b)&&!NativeStageHazards.LinkOpen(h,phase,round,10,now,a,b))return true;return false;}
            foreach(var phase in new[]{NativePhase.Countdown,NativePhase.Fight,NativePhase.Result})foreach(int round in new[]{1,2})foreach(double now in new[]{10d,13.5,19,28,43,75})
            {
                foreach(var l in map.nav.links){var a=nodes[l.a].pos;var b=nodes[l.b].pos;check(index.Unsafe(l,a,b,phase,round,10,now)==Old(a,b,phase,round,now),"hazard link broad phase "+map.id+" "+l.id);comparisons++;}
                foreach(var n in map.nav.nodes){check(index.Unsafe(n,phase,round,10,now)==Old(n.pos,n.pos,phase,round,now),"hazard node broad phase "+map.id+" "+n.id);comparisons++;}
            }
            var watch=new Stopwatch();int oldCount=0,newCount=0;
            for(int warm=0;warm<2;warm++)
            {
                watch.Restart();foreach(var l in map.nav.links)if(Old(nodes[l.a].pos,nodes[l.b].pos,NativePhase.Fight,2,19))oldCount++;watch.Stop();if(warm==1)oldMs+=watch.Elapsed.TotalMilliseconds;
                long bytes=GC.GetAllocatedBytesForCurrentThread();watch.Restart();foreach(var l in map.nav.links)if(index.Unsafe(l,nodes[l.a].pos,nodes[l.b].pos,NativePhase.Fight,2,10,19))newCount++;watch.Stop();if(warm==1){newMs+=watch.Elapsed.TotalMilliseconds;check(GC.GetAllocatedBytesForCurrentThread()==bytes,"hazard broad phase warm allocation");}
            }
            check(oldCount==newCount,"hazard benchmark result equivalence");
        }
        Console.WriteLine($"PASS hazard broad phase: {comparisons} node/link comparisons across maps, live clocks/rounds/phases, zero warm allocations; whole-graph queries legacy {oldMs:F2} ms, cached candidates {newMs:F2} ms (managed, excludes PhysX and initialization)");
    }

}
