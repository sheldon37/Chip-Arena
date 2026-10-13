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
        var map=set.maps.Single(m=>m.id=="siren");var search=new NativeNavSearch(map.nav);var result=new List<string>(map.nav.nodes.Length);
        Func<MapNavLink,bool> all=l=>true,closedHazards=l=>(l.hazards?.Length??0)==0;
        var nodes=map.nav.nodes.ToDictionary(n=>n.id);var edges=map.nav.nodes.ToDictionary(n=>n.id,n=>new List<MapNavLink>());
        foreach(var l in map.nav.links){edges[l.a].Add(l);if(l.kind!="drop")edges[l.b].Add(l);}
        Legacy(nodes,edges,map.nav.nodes[0].id,map.nav.nodes[map.nav.nodes.Length-1].id,all);
        long oldBytes=0,newBytes=0;double oldMs=0,newMs=0;
        for(int i=0;i<16;i++)
        {
            string a=map.nav.nodes[i*379%map.nav.nodes.Length].id,b=map.nav.nodes[(map.nav.nodes.Length-1-i*211)%map.nav.nodes.Length].id;var active=i%2==0?all:closedHazards;
            long before=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();var expected=Legacy(nodes,edges,a,b,active);watch.Stop();oldMs+=watch.Elapsed.TotalMilliseconds;oldBytes+=GC.GetAllocatedBytesForCurrentThread()-before;
            search.Find(a,b,active,result);Check(result.SequenceEqual(expected),"Ward route/tie equivalence "+i);
            before=GC.GetAllocatedBytesForCurrentThread();watch.Restart();search.Find(a,b,active,result);watch.Stop();newMs+=watch.Elapsed.TotalMilliseconds;newBytes+=GC.GetAllocatedBytesForCurrentThread()-before;
        }
        Check(newBytes==0,"warmed route searches allocate "+newBytes+" bytes");
        Func<MapNavNode,bool> usable=n=>!n.safe;
        for(int i=0;i<8;i++){var point=map.spawns.ffa[i].pos;Check(search.Nearest(point,usable).id==map.nav.nodes.OrderBy(n=>V3.Dot(n.pos-point,n.pos-point)).First(usable).id,"nearest equivalence");}
        var graph=new MapNav{nodes=new[]{new MapNavNode{id="upper",pos=new V3(0,6,0)},new MapNavNode{id="lower",pos=new V3(0,0,0)},new MapNavNode{id="isolated",pos=new V3(5,0,0)}},links=new[]{new MapNavLink{id="drop",a="upper",b="lower",kind="drop"}}};
        var small=new NativeNavSearch(graph);small.Find("upper","lower",all,result);Check(result.SequenceEqual(new[]{"upper","lower"}),"directed drop down");small.Find("lower","upper",all,result);Check(result.Count==0,"no reverse drop");small.Find("upper","isolated",all,result);Check(result.Count==0,"unreachable clears old path");small.Find("upper","upper",all,result);Check(result.SequenceEqual(new[]{"upper"}),"start equals end");small.Find("upper","lower",l=>false,result);Check(result.Count==0,"changed gate invalidates next search");
        var box=new MapVolume{center=new V3(1,2,3),size=new V3(2,4,6)};
        for(int i=0;i<100;i++){var a=new V3(i*.1f-5,2,3);var b=new V3(0,i*.1f-5,4);Check(NativeStageHazards.Cross(a,b,box.center,box.size)==NativeStructureRules.Crosses(a,b,box,.45f),"box overload");}
        long allocation=GC.GetAllocatedBytesForCurrentThread();bool hit=false;for(int i=0;i<10000;i++)hit|=NativeStageHazards.Cross(new V3(-2,2,3),new V3(2,2,3),box.center,box.size);Check(GC.GetAllocatedBytesForCurrentThread()==allocation&&hit,"hazard geometry allocates no temporary volume");
        Console.WriteLine($"PASS navigation: Ward {map.nav.nodes.Length} nodes/{map.nav.links.Length} links, 16 legacy-equivalent routes/gates/ties, directed drops, unreachable, nearest, zero-allocation warm searches/cross tests; legacy {oldMs:F1} ms/{oldBytes} bytes, new {newMs:F1} ms/{newBytes} bytes (managed only; both exclude one-time adjacency setup and PhysX)");
    }
}
