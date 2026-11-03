using System;
using System.Collections.Generic;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // Offline counterpart of ExpeditionSupported/ExpeditionReachable for the generated, axis-aligned
    // Backrooms collision. Unlike the old dry-only/graph-only checks, capsule bottoms have no step allowance.
    internal static class BackroomsRouteChecks
    {
        private sealed class Probe
        {
            readonly ContentMap map; readonly ContentBlock[] boxes;
            readonly Dictionary<(float,float),MapNavNode[]> clearNodes=new Dictionary<(float,float),MapNavNode[]>();
            public MapNavNode Nearest(V3 p,float radius,float height)
            {if(!clearNodes.TryGetValue((radius,height),out var nodes))clearNodes[(radius,height)]=nodes=map.nav.nodes.Where(n=>Supported(n.pos-new V3(0,.85f,0),radius,height)).ToArray();return nodes.OrderBy(n=>(n.pos-p-new V3(0,.85f,0)).Length).FirstOrDefault();}
            readonly Dictionary<(int,int),List<ContentBlock>> grid=new Dictionary<(int,int),List<ContentBlock>>();
            IEnumerable<ContentBlock> Near(V3 p)=>grid.TryGetValue(((int)Math.Floor(p.x/4),(int)Math.Floor(p.z/4)),out var list)?list:Enumerable.Empty<ContentBlock>();
            public Probe(ContentMap m){map=m;var off=new HashSet<string>((m.ramps??Array.Empty<MapRamp>()).SelectMany(r=>r.blocks));boxes=m.blocks.Where(b=>!off.Contains(b.id)).ToArray();foreach(var b in boxes)for(int x=(int)Math.Floor((b.pos.x-b.size.x/2-1)/4);x<=(int)Math.Floor((b.pos.x+b.size.x/2+1)/4);x++)for(int z=(int)Math.Floor((b.pos.z-b.size.z/2-1)/4);z<=(int)Math.Floor((b.pos.z+b.size.z/2+1)/4);z++){if(!grid.TryGetValue((x,z),out var list))grid[(x,z)]=list=new List<ContentBlock>();list.Add(b);}}
            static float Dot(V3 a,V3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
            static bool Surface(MapRamp r,V3 p,out float y,out float normal)
            {
                var d=r.high-r.low;float length=(float)Math.Sqrt(d.x*d.x+d.z*d.z),along=((p.x-r.low.x)*d.x+(p.z-r.low.z)*d.z)/length,across=(-(p.x-r.low.x)*d.z+(p.z-r.low.z)*d.x)/length;
                y=r.low.y+d.y*along/length;normal=length/(float)Math.Sqrt(length*length+d.y*d.y);
                return along>=0&&along<=length&&Math.Abs(across)<=r.width/2;
            }
            public bool Ground(V3 p,float up,float down,out V3 floor,out float normal)
            {
                float y=float.NegativeInfinity;normal=1;
                foreach(var b in Near(p)){float top=b.pos.y+b.size.y/2;if(Math.Abs(p.x-b.pos.x)<=b.size.x/2&&Math.Abs(p.z-b.pos.z)<=b.size.z/2&&top<=p.y+up+.00001f&&top>=p.y+up-down-.00001f&&top>y){y=top;normal=1;}}
                foreach(var r in map.ramps??Array.Empty<MapRamp>())if(Surface(r,p,out float top,out float n)&&top<=p.y+up+.00001f&&top>=p.y+up-down-.00001f&&top>y){y=top;normal=n;}
                floor=new V3(p.x,y,p.z);return !float.IsNegativeInfinity(y);
            }
            public bool Supported(V3 p,float radius,float height)
            {
                if(!Ground(p,.35f,.45f,out _,out float normal)||normal<.65f)return false;
                float lift=.04f+radius*(1/normal-1),lo=p.y+radius+lift,hi=p.y+height-radius+lift;
                foreach(var b in Near(p))
                {
                    float dx=Math.Max(0,Math.Abs(p.x-b.pos.x)-b.size.x/2),dz=Math.Max(0,Math.Abs(p.z-b.pos.z)-b.size.z/2),dy=Math.Max(0,Math.Max(b.pos.y-b.size.y/2-hi,lo-(b.pos.y+b.size.y/2)));
                    if(dx*dx+dy*dy+dz*dz<radius*radius-.000001f)return false;
                }
                foreach(var r in map.ramps??Array.Empty<MapRamp>())
                {
                    // Closest point on the finite sloping rectangle to the capsule's bottom sphere.
                    var d=r.high-r.low;float run=(float)Math.Sqrt(d.x*d.x+d.z*d.z);var across=new V3(-d.z/run,0,d.x/run);var centre=new V3(p.x,lo,p.z);
                    float t=Math.Max(0,Math.Min(1,Dot(centre-r.low,d)/Dot(d,d))),u=Math.Max(-r.width/2,Math.Min(r.width/2,Dot(centre-r.low,across)));
                    var delta=centre-(r.low+d*t+across*u);if(Dot(delta,delta)<radius*radius-.000001f)return false;
                }
                return true;
            }
            public HashSet<string> Flood(V3 origin,float radius,float height)
            {
                var nodes=map.nav.nodes.ToDictionary(n=>n.id);var clear=nodes.Values.ToDictionary(n=>n.id,n=>Supported(n.pos-new V3(0,.85f,0),radius,height));
                var edges=nodes.Keys.ToDictionary(id=>id,id=>new List<string>());
                foreach(var link in map.nav.links)
                {
                    if(!clear[link.a]||!clear[link.b])continue;var a=nodes[link.a].pos-new V3(0,.85f,0);var b=nodes[link.b].pos-new V3(0,.85f,0);bool ok=true;int steps=Math.Max(1,(int)Math.Ceiling((b-a).Length/.4));
                    for(int i=1;i<steps&&ok;i++){var p=a+(b-a)*(i/(float)steps);ok=Ground(p,.2f,.4f,out var floor,out float normal)&&normal>=.65f&&Supported(floor,radius,height);}
                    // Generated links cross one cell edge only; wall crossings must also pass the raised LOS.
                    for(int i=0;i<=steps*2&&ok;i++){var p=a+(b-a)*(i/(float)(steps*2))+new V3(0,radius+.4f,0);ok=!Near(p).Any(box=>Math.Abs(p.x-box.pos.x)<box.size.x/2-.00001f&&Math.Abs(p.y-box.pos.y)<box.size.y/2-.00001f&&Math.Abs(p.z-box.pos.z)<box.size.z/2-.00001f);}
                    if(ok){edges[link.a].Add(link.b);edges[link.b].Add(link.a);}
                }
                var start=nodes.Values.Where(n=>clear[n.id]).OrderBy(n=>(n.pos-origin-new V3(0,.85f,0)).Length).First();var seen=new HashSet<string>{start.id};var q=new Queue<string>(seen);
                while(q.Count>0)foreach(var next in edges[q.Dequeue()])if(seen.Add(next))q.Enqueue(next);return seen;
            }
        }
        public static void Run()
        {
            int anchors=0;var failures=new List<string>();
            var seeds=new HashSet<int>(new[]{-1,int.MinValue,int.MaxValue,647032}.Concat(Enumerable.Range(0,32)));
            // Include the actual seed derivation used by the Windows deployment acceptance gate.
            foreach(uint origin in new uint[]{1,17,0x80000000u,0xffffffffu})
            {
                var deployment=new NativeExpeditionDeployment("route-check","host",origin);
                var catalog=new[]{"ward","pool","backrooms"}.Select(id=>new ExpeditionInstanceTheme{id=id,theme=id}).ToArray();
                for(int roll=0;roll<2;roll++){deployment.RollInstances("host",catalog);uint world=deployment.Snapshot().worldSeed;seeds.Add(unchecked((int)(world^0xbac00001u)));seeds.Add(unchecked((int)(world^0xbac00002u)));}
            }
            foreach(int seed in seeds)
            {
                var pool=NativePoolroomsGenerator.Build(seed,true);var level=NativeBackroomsGenerator.Build(seed,true);
                foreach(var pair in new[]{(pool.Map,NativePoolroomsExpedition.Area(pool)),(level.Map,NativeBackroomsExpedition.Area(level))})
                {
                    var map=pair.Item1;var area=pair.Item2;var probe=new Probe(map);var floods=new Dictionary<string,HashSet<string>>();
                    bool Connected(V3 from,V3 to,float radius,float height)
                    {
                        string key=radius+":"+height;if(!floods.TryGetValue(key,out var seen))floods[key]=seen=probe.Flood(area.doors[0].entries[0].Position,radius,height);
                        var a=probe.Nearest(from,radius,height);var b=probe.Nearest(to,radius,height);
                        return a!=null&&b!=null&&(a.pos-from-new V3(0,.85f,0)).Length<=4&&(b.pos-to-new V3(0,.85f,0)).Length<=4&&seen.Contains(a.id)&&seen.Contains(b.id);
                    }
                    int before=failures.Count;
                    foreach(var spawn in area.spawns)
                    {
                        anchors++;float radius=.45f,height=1.8f;foreach(var kind in spawn.allow){var k=NativeHordeZones.Of(NativeHordeKinds.Resolve(kind));if(k!=null){radius=Math.Max(radius,k.Radius);height=Math.Max(height,k.Height);}}
                        string key=radius+":"+height;if(!floods.TryGetValue(key,out var seen))floods[key]=seen=probe.Flood(area.doors[0].entries[0].Position,radius,height);
                        var p=spawn.entry.@exit.Position();var nearest=probe.Nearest(p,radius,height);
                        if(!probe.Supported(p,radius,height)||nearest==null||(nearest.pos-p-new V3(0,.85f,0)).Length>4||!seen.Contains(nearest.id))failures.Add(seed+" "+map.id+" "+spawn.entry.id);
                        if(spawn.allow.Contains("drowned")&&NativePoolroomsWater.Depth(pool.Layout,p)<=.8f)throw new Exception("Drowned moved out of deep water");
                    }
                    if(failures.Count==before)NativeExpeditionLayout.ValidateGeometry(new ExpeditionLayout{areas=new[]{area}},new ConversionContent{maps=new[]{map}},null,
                        (_,p,r,h)=>probe.Supported(p,r,h),(_,a,b,r,h)=>Connected(a,b,r,h));
                    if(seed==-1&&map.id==NativePoolroomsGenerator.InstanceMap&&map.ramps.Length>0)
                    {
                        // Negative control: restoring the old box-only stairs must disconnect a deep spawn.
                        var ramps=map.ramps;map.ramps=Array.Empty<MapRamp>();
                        try{var old=new Probe(map);var seen=old.Flood(area.doors[0].entries[0].Position,.45f,1.8f);var deep=area.spawns.First(a=>a.allow.Contains("drowned")).entry.@exit.Position();var n=old.Nearest(deep,.45f,1.8f);if(n!=null&&seen.Contains(n.id))throw new Exception("Regression probe accepted the old blocked pool stairs");}
                        finally{map.ramps=ramps;}
                    }
                }
            }
            if(failures.Count>0)throw new Exception("Backrooms collision routes: "+failures.Count+" / "+anchors+" anchors fail: "+string.Join(", ",failures.Take(12)));
            Console.WriteLine("PASS Backrooms collision routes: "+anchors+" anchors, "+seeds.Count+" seeds, both maps");
        }
    }
}
