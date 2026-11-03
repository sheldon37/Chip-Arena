using System;
using System.Linq;
using System.Collections.Generic;
namespace ChipArena.UnityConversion
{
    internal static class PoolroomsRulesChecks
    {
        public static void Run()
        {
            int checks=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception("Poolrooms rules: "+message);}
            foreach(int seed in new[]{647032,0,1,2,7,42,int.MinValue,int.MaxValue})foreach(bool expedition in new[]{false,true})
            {
                var g=NativePoolroomsGenerator.Build(seed,expedition);var l=g.Layout;NativeMapRules.Validate(g.Map);
                Check(g.Identity==NativePoolroomsGenerator.Build(seed,expedition).Identity,"deterministic geometry identity");
                var culture=System.Globalization.CultureInfo.CurrentCulture;try{var other=(System.Globalization.CultureInfo)culture.Clone();other.NumberFormat.NegativeSign="minus";other.NumberFormat.NumberDecimalSeparator=",";System.Globalization.CultureInfo.CurrentCulture=other;Check(g.Identity==NativePoolroomsGenerator.Build(seed,expedition).Identity,"culture-independent ticket identity");}finally{System.Globalization.CultureInfo.CurrentCulture=culture;}
                var deck=NativePoolroomsGenerator.Distances(l,g.Players[0]);
                Check(Enumerable.Range(0,l.Floor.Length).All(c=>l.Floor[c]!=PoolroomsLayout.Deck||deck[c]>=0),"every deck reachable");
                Check(g.Players.Distinct().Count()==8&&g.Players.All(c=>deck[c]>=0&&l.Floor[c]==0),"eight connected dry spawns");
                Check(g.Enemies.Length>0&&g.Enemies.All(c=>g.Players.All(p=>(NativePoolroomsGenerator.Position(l,c)-NativePoolroomsGenerator.Position(l,p)).Length>=12)),"enemy separation");
                // Independent dry-route clearance against the generated collision boxes.
                var closure=NativePoolroomsExpedition.Area(g).doors[0].closure;
                var door=new ContentBlock{pos=new V3(closure.x,closure.y,closure.z),size=new V3(closure.sx,closure.sy,closure.sz)};
                var obstacles=g.Map.blocks.Concat(expedition?new[]{door}:Array.Empty<ContentBlock>()).Where(b=>b.pos.y+b.size.y/2>.01f&&b.pos.y-b.size.y/2<1.8f).ToArray();
                bool Clear(V3 p){foreach(var b in obstacles){float dx=Math.Max(0,Math.Abs(p.x-b.pos.x)-b.size.x/2),dz=Math.Max(0,Math.Abs(p.z-b.pos.z)-b.size.z/2);if(dx*dx+dz*dz<.45f*.45f)return false;}return true;}
                Check(g.Players.Concat(g.Enemies).All(c=>Clear(NativePoolroomsGenerator.Position(l,c))),"spawn collision clearance");
                var nav=g.Map.nav;var positions=nav.nodes.ToDictionary(n=>n.id,n=>n.pos);
                Check(nav.links.Where(e=>Math.Abs(positions[e.a].y-.85f)<.001f&&Math.Abs(positions[e.b].y-.85f)<.001f).All(e=>Enumerable.Range(0,7).All(k=>Clear(positions[e.a]+(positions[e.b]-positions[e.a])*(k/6f)))),"dry routes clear with return door closed");
                var seen=new HashSet<string>{"pool:"+g.Players[0]+":0"};var q=new Queue<string>(seen);var edges=new Dictionary<string,List<string>>();foreach(var n in nav.nodes)edges[n.id]=new List<string>();foreach(var e in nav.links){edges[e.a].Add(e.b);edges[e.b].Add(e.a);}while(q.Count>0)foreach(var n in edges[q.Dequeue()])if(seen.Add(n))q.Enqueue(n);
                Check(seen.Count==nav.nodes.Length,"every pool and step has a route out: "+seed+"/"+expedition+" "+seen.Count+"/"+nav.nodes.Length);
                foreach(int c in Enumerable.Range(0,l.Floor.Length))
                {
                    var feet=NativePoolroomsGenerator.Position(l,c);float depth=NativePoolroomsWater.Depth(l,feet);
                    Check(Math.Abs(depth-Math.Max(0,-.15f-feet.y))<.0001f,"water depth matches collision floor");
                    if(l.Floor[c]==PoolroomsLayout.Steps)Check(g.Map.blocks.Count(b=>b.id.Contains(":step-"+c+"-"))==4,"four collision treads");
                }
                var area=NativePoolroomsExpedition.Area(g);Check(area.spawns.All(s=>!s.allow.Contains("hulk")),"uncertified Hulk routes excluded");
                Check(g.Players.All(c=>Clear(NativePoolroomsGenerator.Position(l,c))),"party slots clear");
                Check(g.Map.blocks.All(b=>b.role=="static"),"pool shells cannot be breached");
            }
            Check(Math.Abs(NativePoolroomsWater.Speed(.25f)-.8913043f)<.0001f&&NativePoolroomsWater.Speed(1.15f)==.5f,"shallow and deep drag");
            var layout=new PoolroomsLayout(0,4,4);layout.Floor[0]=PoolroomsLayout.Deep;var actor=new Actor();NativePoolroomsWater.Pose(actor,layout,new V3(1,-1.3f,1));Check(actor.Wading&&!actor.Swimming&&Math.Abs(actor.WaterDepth-1.15f)<.0001f&&actor.Waterline==-.15f,"honest chest-deep pose hook");NativePoolroomsWater.Pose(actor,null,new V3());Check(!actor.Wading&&!actor.Swimming&&actor.WaterDepth==0,"water state clears on exit/map transition");
            Check(NativePoolroomsGenerator.SpawnKind(NativePoolroomsGenerator.InstanceMap,NativeExpeditionMutators.Kind(new[]{"hulkamania"},"runner"))=="brute"&&NativePoolroomsGenerator.SpawnKind("siren","hulk")=="hulk","Hulkamania cannot stall the pool quota or alter other maps");
            Check(NativeStageMusic.Theme(NativePoolroomsGenerator.ArenaId,"")==NativeStageMusic.PoolroomsTrack,"Poolrooms music");
            Console.WriteLine("PASS Poolrooms rules: "+checks+" checks");
        }
    }
}
