using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The Expedition city grown (Claude, 2026-10-20; docs/EXPEDITION-HUB.md "The city grown", exp-layout-7). Owner: "make the
    // expedition city still a little bigger (I want players to walk further still.)". The east, west and south arms moved 24 m
    // out past the city's edge, with an outer quarter in each band (a cross street that ends against a landmark, a lane round it,
    // a square before the checkpoint). Checks, on the imported map and its navigation (Resources/UnityMaps.json, the graph the
    // authority loads) and the live manifest (Resources/ExpeditionLayout.json):
    //  - every navigation node is reached from the sanctuary's first slot, and so is every entrance, join circle, return, the
    //    machine and the caches;
    //  - the walk on that graph (any-angle: the shortest path over its links, cut straight wherever the graph's own floor sees
    //    through, Basic Theta*)
    //    from the sanctuary slots and from each route's gate to each join circle grew by 35-50% over exp-layout-6 (pinned below,
    //    measured by Measure on shared d555363's resources);
    //  - the hub zones hold navigation (the roaming zombies' wander points) and none of it in the sanctuary; their spawns stand
    //    on reached nodes;
    //  - the join circles, the doors' volumes and the hub gates lie in the bounds with no block in them;
    //  - no two visible faces of one facing share a plane in the grown geometry, nor anywhere in the town (the kit built with
    //    the hub record, as the runtime builds it; SurfaceChecks' audit and threshold);
    //  - the per-frame cost: the kit's parts the runtime draws from points along each route (NativeExpeditionCityKit.Drawn: detail
    //    to 90 m, the rest to the fog's end) stay within exp-layout-6's, which drew every part; its draws, instances and vertices;
    //  - the selection allocates nothing per frame.
    // Static data and pure-code checks only: no Unity scene, PhysX, GPU or gameplay claim.
    internal static class ExpeditionCityChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Expedition city: "+what);count++;}
        public const double RunSpeed=6.8,SprintSpeed=6.8*1.35;
        // ---- exp-layout-6, before the city grew: Measure(root, cull false) on shared d555363 (2026-10-20) ----
        internal static readonly Dictionary<string,(double slotJoin,double gateJoin,double slotDoor)> Before=new Dictionary<string,(double,double,double)>
        {
            ["exp-ward-zero"]=(BeforeWardSlot,BeforeWardGate,BeforeWardDoor),["exp-transit"]=(BeforeStationSlot,BeforeStationGate,BeforeStationDoor),["exp-foundry"]=(BeforeFoundrySlot,BeforeFoundryGate,BeforeFoundryDoor),
        };
        // Metres on the navigation (the mean over the eight slots to the circle's centre, the route gate's outer threshold to it,
        // the slots to the doorway's front), the graph, the kit.
        internal const double BeforeWardSlot=80.950,BeforeWardGate=67.073,BeforeWardDoor=83.616,BeforeStationSlot=78.966,BeforeStationGate=65.071,BeforeStationDoor=82.945,BeforeFoundrySlot=80.201,BeforeFoundryGate=66.324,BeforeFoundryDoor=83.574;
        internal const int BeforeNodes=9505,BeforeParts=12783,BeforeLow=7711,BeforeCasters=462;internal const double BeforeArea=29952;
        /// <summary>exp-layout-6's kit instances drawn per frame from the five points along the Ward Zero route (gate, 25%, 50%,
        /// 75%, the circle) and in the sanctuary: every part (the runtime had no range then); the low tier drew every non-detail part.</summary>
        internal static readonly int[] BeforeView={12783,12783,12783,12783,12783,12783},BeforeViewLow={7711,7711,7711,7711,7711,7711};

        // ---- the town's navigation as the authority loads it ----
        internal sealed class Nav
        {
            public readonly MapNavNode[] Nodes;public readonly List<(int to,double cost)>[] Adj;
            private readonly Dictionary<(int,int),int> cell=new Dictionary<(int,int),int>();private readonly HashSet<(int,int,int,int)> linked=new HashSet<(int,int,int,int)>();
            public Nav(MapNav nav)
            {
                Nodes=nav.nodes;var index=new Dictionary<string,int>();Adj=new List<(int,double)>[Nodes.Length];
                for(int i=0;i<Nodes.Length;i++){index[Nodes[i].id]=i;Adj[i]=new List<(int,double)>();cell[Key(Nodes[i].pos.x,Nodes[i].pos.z)]=i;}
                foreach(var l in nav.links)
                {
                    int a=index[l.a],b=index[l.b];double c=(Nodes[b].pos-Nodes[a].pos).Length;Adj[a].Add((b,c));if(l.kind!="drop")Adj[b].Add((a,c));
                    var ka=Key(Nodes[a].pos.x,Nodes[a].pos.z);var kb=Key(Nodes[b].pos.x,Nodes[b].pos.z);linked.Add((ka.Item1,ka.Item2,kb.Item1,kb.Item2));linked.Add((kb.Item1,kb.Item2,ka.Item1,ka.Item2));
                }
            }
            private static (int,int) Key(double x,double z)=>((int)Math.Floor(x),(int)Math.Floor(z));
            /// <summary>The node nearest (x, z) within `r` m (the town has one street level), or -1.</summary>
            public int Near(double x,double z,double r=1.5)
            {
                int best=-1;double bd=r*r;var k=Key(x,z);
                for(int di=-2;di<=2;di++)for(int dj=-2;dj<=2;dj++)if(cell.TryGetValue((k.Item1+di,k.Item2+dj),out int n)){double dx=Nodes[n].pos.x-x,dz=Nodes[n].pos.z-z,d=dx*dx+dz*dz;if(d<=bd){bd=d;best=n;}}
                return best;
            }
            /// <summary>Shortest distances over the links from node `s`, and each node's predecessor.</summary>
            public double[] From(int s,out int[] prev)
            {
                var dist=new double[Nodes.Length];prev=new int[Nodes.Length];for(int i=0;i<dist.Length;i++){dist[i]=double.PositiveInfinity;prev[i]=-1;}
                var q=new PriorityQueue<int,double>();dist[s]=0;q.Enqueue(s,0);
                while(q.TryDequeue(out int n,out double d)){if(d>dist[n])continue;foreach(var (m,c) in Adj[n]){double nd=d+c;if(nd<dist[m]){dist[m]=nd;prev[m]=n;q.Enqueue(m,nd);}}}
                return dist;
            }
            // Whether the straight line a-b stays on the graph's floor: every cell it crosses holds a node, each step to the next
            // cell is a link (a diagonal step through one of its two orthogonal neighbours).
            private bool Step((int,int) c,(int,int) d)=>linked.Contains((c.Item1,c.Item2,d.Item1,d.Item2));
            public bool Sees(double ax,double az,double bx,double bz)
            {
                double len=Math.Sqrt((bx-ax)*(bx-ax)+(bz-az)*(bz-az));int n=Math.Max(1,(int)Math.Ceiling(len/.2));var c=Key(ax,az);if(!cell.ContainsKey(c))return false;
                for(int k=1;k<=n;k++)
                {
                    var d=Key(ax+(bx-ax)*k/n,az+(bz-az)*k/n);if(d==c)continue;if(!cell.ContainsKey(d))return false;int di=d.Item1-c.Item1,dj=d.Item2-c.Item2;
                    if(Math.Abs(di)+Math.Abs(dj)==1){if(!Step(c,d))return false;}
                    else if(Math.Abs(di)==1&&Math.Abs(dj)==1){var p=(c.Item1+di,c.Item2);var r=(c.Item1,c.Item2+dj);if(!(cell.ContainsKey(p)&&Step(c,p)&&Step(p,d)||cell.ContainsKey(r)&&Step(c,r)&&Step(r,d)))return false;}
                    else return false;
                    c=d;
                }
                return true;
            }
            private double Gap(int a,int b){var p=Nodes[a].pos;var q=Nodes[b].pos;return Math.Sqrt((p.x-q.x)*(p.x-q.x)+(p.z-q.z)*(p.z-q.z));}
            /// <summary>Any-angle walking distances from node `s` (Basic Theta*, one source to every node: over the links, a node takes
            /// its predecessor's own parent wherever the graph's floor sees from that parent to it), and each node's parent: the
            /// walk's turning points back to `s`.</summary>
            public double[] Walk(int s,out int[] parent)
            {
                var dist=new double[Nodes.Length];parent=new int[Nodes.Length];var shut=new bool[Nodes.Length];for(int i=0;i<dist.Length;i++){dist[i]=double.PositiveInfinity;parent[i]=-1;}
                var q=new PriorityQueue<int,double>();dist[s]=0;parent[s]=s;q.Enqueue(s,0);
                while(q.TryDequeue(out int n,out double d))
                {
                    if(shut[n]||d>dist[n])continue;shut[n]=true;int p=parent[n];
                    foreach(var (m,c) in Adj[n])
                    {
                        if(shut[m])continue;
                        if(p!=n&&Sees(Nodes[p].pos.x,Nodes[p].pos.z,Nodes[m].pos.x,Nodes[m].pos.z)){double nd=dist[p]+Gap(p,m);if(nd<dist[m]){dist[m]=nd;parent[m]=p;q.Enqueue(m,nd);}}
                        else{double nd=d+c;if(nd<dist[m]){dist[m]=nd;parent[m]=n;q.Enqueue(m,nd);}}
                    }
                }
                return dist;
            }
            /// <summary>The turning points of the walk to `t` (Walk's parents), the source and `t` not counted.</summary>
            public static int Turns(int[] parent,int t){int n=0;for(int k=parent[t];k>=0&&parent[k]!=k;k=parent[k])n++;return n;}
        }

        internal sealed class Metrics
        {
            public string Revision;public double Width,Depth,Area;public int Nodes,Links,Reached,Blocks;
            public readonly Dictionary<string,(double slotJoin,double slotJoinMin,double slotJoinMax,double gateJoin,double slotDoor,double rawSlotJoin,int turns)> Doors=new Dictionary<string,(double,double,double,double,double,double,int)>();
            public double WardStation,StationFoundry,SlotFinal;public double[] Caches=Array.Empty<double>();
            public int Parts,Low,Casters,MeshVertices,Backdrop;public int[] View=Array.Empty<int>(),ViewDraws=Array.Empty<int>(),ViewLow=Array.Empty<int>();public float Far;
        }
        private static ContentMap Town(string root,JsonSerializerOptions o)=>JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),o).maps.Single(m=>m.id=="expedition-city");
        /// <summary>The town's walks, area and per-frame dressing cost from a project root's resources. `cull`: whether that root's
        /// runtime leaves far parts undrawn (NativeExpeditionCityKit.Drawn; false for exp-layout-6, which drew every part).</summary>
        internal static Metrics Measure(string root,bool cull)
        {
            var o=new JsonSerializerOptions{IncludeFields=true};string R(string f)=>File.ReadAllText(Path.Combine(root,f));
            var town=Town(root,o);string text=R("Assets/Conversion/Resources/ExpeditionLayout.json");var layout=NativeExpeditionLayout.Parse(text);var area=layout.Area(layout.world);var hub=ExpeditionHubLayout.Parse(text,area.id);
            var m=new Metrics{Revision=layout.revision,Width=town.bounds.maxX-town.bounds.minX,Depth=town.bounds.maxZ-town.bounds.minZ,Blocks=town.blocks.Length};m.Area=m.Width*m.Depth;
            var nav=new Nav(town.nav);m.Nodes=nav.Nodes.Length;m.Links=town.nav.links.Length;
            var slots=area.slots.Select(s=>nav.Near(s.x,s.z)).ToArray();var raw0=nav.From(slots[0],out _);m.Reached=raw0.Count(d=>!double.IsInfinity(d));
            var fromSlot=slots.Select(s=>{var d=nav.Walk(s,out var p);return (d,p);}).ToArray();
            int Node(double x,double z){int n=nav.Near(x,z);if(n<0)throw new Exception("Expedition city: no navigation node at "+x+", "+z);return n;}
            var joins=new Dictionary<string,int>();
            foreach(var d in area.doors)
            {
                var a=area.anchors.Single(x=>x.id==d.anchor);var j=Node(d.join.x,d.join.z);joins[a.building]=j;double yaw=a.yaw*Math.PI/180;int front=Node(a.x+Math.Sin(yaw)*1.0,a.z+Math.Cos(yaw)*1.0);
                var per=fromSlot.Select(f=>f.d[j]).ToArray();
                var route=hub.Routes.Single(r=>r.To==a.building);var g=hub.Gate(route.Gate);var fg=nav.Walk(Node(g.Outside.x,g.Outside.z),out _);
                m.Doors[a.building]=(per.Average(),per.Min(),per.Max(),fg[j],fromSlot.Average(f=>f.d[front]),raw0[j],Nav.Turns(fromSlot[0].p,j));
            }
            var fw=nav.Walk(joins["exp-ward-zero"],out _);m.WardStation=fw[joins["exp-transit"]];var fs=nav.Walk(joins["exp-transit"],out _);m.StationFoundry=fs[joins["exp-foundry"]];
            var gate=area.gates.Single(g=>g.opens=="final").closure;m.SlotFinal=fromSlot[0].d[Node(gate.x,gate.z-1.5)];
            m.Caches=area.interactions.Select(i=>fromSlot[0].d[Node(i.x,i.z)]).ToArray();
            // The dressing: the kit as the runtime builds it, and what it draws from points along the Ward Zero route.
            var kit=NativeExpeditionCityKit.Build(town.blocks,area,hub,town.bounds);m.Parts=kit.Parts.Length;m.Low=kit.Parts.Count(p=>!p.Detail);m.Casters=kit.Parts.Count(p=>p.Shadow);
            m.MeshVertices=NativeBlockLooks.Build(0).Vertices;m.Backdrop=NativeExpeditionCityScenery.Build(out _).Length;
            var set=JsonSerializer.Deserialize<MapLookSet>(R("Assets/Conversion/Resources/MapLooks.json"),o);var look=NativeMapLooks.ByLook(set,town.look);m.Far=NativeExpeditionCityKit.FarRange(look);
            var eyes=Eyes(hub,area);var spots=Spots(kit,town.blocks);var view=new List<int>();var draws=new List<int>();var low=new List<int>();
            foreach(var e in eyes){Drawn(kit,spots,e,cull,m.Far,false,out int cast,out int trim);view.Add(cast+trim);draws.Add(Chunks(cast)+Chunks(trim));Drawn(kit,spots,e,cull,m.Far,true,out cast,out trim);low.Add(cast+trim);}
            m.View=view.ToArray();m.ViewDraws=draws.ToArray();m.ViewLow=low.ToArray();return m;
        }
        private static int Chunks(int n)=>(n+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk;
        /// <summary>The eye points: along the Ward Zero route (its gate, 25%, 50%, 75% and its join circle) at head height, then the
        /// sanctuary's centre.</summary>
        internal static (float x,float y,float z)[] Eyes(ExpeditionHubLayout hub,ExpeditionAreaLayout area)
        {
            var r=hub.Routes.Single(x=>x.To=="exp-ward-zero");var line=NativeExpeditionCityKit.RouteLine(r,hub.Gate(r.Gate));var join=area.doors.Single(d=>area.anchors.Single(a=>a.id==d.anchor).building=="exp-ward-zero").join;
            var o=new List<(float,float,float)>();foreach(double f in new[]{0,.25,.5,.75}){line.At(line.Length*f,out double x,out double z,out _,out _);o.Add(((float)x,1.7f,(float)z));}
            o.Add((join.x,1.7f,join.z));o.Add((0,1.7f,0));return o.ToArray();
        }
        private static float[] Spots(NativeExpeditionCityKit kit,ContentBlock[] blocks)
        {
            var s=new float[kit.Parts.Length*3];for(int i=0;i<kit.Parts.Length;i++){var p=kit.Parts[i];var b=blocks[p.Parent];s[i*3]=b.pos.x+p.X;s[i*3+1]=b.pos.y+p.Y;s[i*3+2]=b.pos.z+p.Z;}return s;
        }
        /// <summary>The runtime's selection (Runtime NativeExpeditionCityPresentation.DrawTownKit): the parts drawn from eye e, casting
        /// and not; `lean` the low tier. No allocation.</summary>
        internal static void Drawn(NativeExpeditionCityKit kit,float[] spots,(float x,float y,float z) e,bool cull,float far,bool lean,out int cast,out int trim)
        {
            cast=trim=0;var parts=kit.Parts;
            for(int i=0;i<parts.Length;i++)
            {
                if(parts[i].Detail&&lean)continue;
                if(cull){float dx=spots[i*3]-e.x,dy=spots[i*3+1]-e.y,dz=spots[i*3+2]-e.z;if(!NativeExpeditionCityKit.Drawn(parts[i],dx*dx+dy*dy+dz*dz,far))continue;}
                if(parts[i].Shadow)cast++;else trim++;
            }
        }

        public static void Run(string root)
        {
            count=0;var o=new JsonSerializerOptions{IncludeFields=true};string R(string f)=>File.ReadAllText(Path.Combine(root,f));
            var m=Measure(root,true);var town=Town(root,o);string text=R("Assets/Conversion/Resources/ExpeditionLayout.json");var layout=NativeExpeditionLayout.Parse(text);var area=layout.Area(layout.world);var hub=ExpeditionHubLayout.Parse(text,area.id);
            Check(m.Revision=="exp-layout-7"&&m.Width==240&&m.Depth==180,"exp-layout-7: the town is "+m.Width+" x "+m.Depth+" m ("+m.Area+" m2; exp-layout-6 "+BeforeArea+" m2)");
            // ---------------- reachability on the navigation ----------------
            var nav=new Nav(town.nav);var d0=nav.From(nav.Near(area.slots[0].x,area.slots[0].z),out _);
            Check(m.Reached==m.Nodes,"every one of the "+m.Nodes+" navigation nodes is reached from the sanctuary's first slot ("+m.Reached+")");
            var targets=new List<(string what,double x,double z)>();
            foreach(var d in area.doors){var a=area.anchors.Single(x=>x.id==d.anchor);double yaw=a.yaw*Math.PI/180;targets.Add((a.building+" doorway",a.x+Math.Sin(yaw),a.z+Math.Cos(yaw)));targets.Add((a.building+" join circle",d.join.x,d.join.z));foreach(var p in d.returns)targets.Add((a.building+" return",p.x,p.z));}
            foreach(var i in area.interactions)targets.Add((i.anchor,i.x,i.z));foreach(var g in hub.Gates){targets.Add((g.Id+" inside",g.Inside.x,g.Inside.z));targets.Add((g.Id+" outside",g.Outside.x,g.Outside.z));}
            foreach(var t in targets){int n=nav.Near(t.x,t.z);Check(n>=0&&!double.IsInfinity(d0[n]),t.what+" ("+t.x.ToString("0.0")+", "+t.z.ToString("0.0")+") stands on a reached navigation node");}
            // ---------------- the walk ----------------
            string Walk(double d)=>d.ToString("0.0")+" m ("+(d/RunSpeed).ToString("0.0")+" s running, "+(d/SprintSpeed).ToString("0.0")+" s sprinting)";
            foreach(var kv in m.Doors)
            {
                var b=Before[kv.Key];double rs=kv.Value.slotJoin/b.slotJoin,rg=kv.Value.gateJoin/b.gateJoin,rd=kv.Value.slotDoor/b.slotDoor;
                Check(rs>=1.35&&rs<=1.50&&rg>=1.35&&rg<=1.50,kv.Key+": from the sanctuary slots to its join circle "+Walk(b.slotJoin)+" -> "+Walk(kv.Value.slotJoin)+" (x"+rs.ToString("0.00")+"), from its gate "+Walk(b.gateJoin)+" -> "+Walk(kv.Value.gateJoin)+" (x"+rg.ToString("0.00")+"); target x1.35-1.50");
                Check(rd>=1.30&&kv.Value.turns>=3,kv.Key+": to its doorway "+b.slotDoor.ToString("0.0")+" -> "+kv.Value.slotDoor.ToString("0.0")+" m (x"+rd.ToString("0.00")+"), the walk from slot 0 turns "+kv.Value.turns+" times");
            }
            // ---------------- the hub zones (the roaming zombies' wander points) ----------------
            var sanct=area.sanctuary[0];var spawns=area.spawns.Select(s=>s.entry).ToArray();
            foreach(var z in hub.Zones)
            {
                var v=z.Volume;int inZone=0,inSafe=0;foreach(var n in nav.Nodes){if(!v.Contains(new V3(n.pos.x,v.y,n.pos.z)))continue;inZone++;if(sanct.Contains(new V3(n.pos.x,sanct.y,n.pos.z)))inSafe++;}
                bool spawnsOn=z.Spawns.All(e=>{int n=nav.Near(spawns[e].@exit.x,spawns[e].@exit.z,2.0);return n>=0&&!double.IsInfinity(d0[n]);});
                Check(inZone>=30&&inSafe==0&&spawnsOn,z.Id+" ("+z.Ring+"): "+inZone+" navigation nodes to wander, none in the sanctuary, its "+z.Spawns.Length+" spawns on reached floor");
            }
            foreach(var arm in new[]{"east","west","south"}){var q=hub.Zones.SingleOrDefault(z=>z.Id=="exp-city:zone:"+arm+"-quarter");Check(q!=null&&q.Ring=="mid"&&q.Spawns.Length>=5,"the "+arm+" quarter is a mid-ring zone with its own spawns ("+(q?.Spawns.Length??0)+")");}
            // ---------------- circles, doors and gates in the bounds and clear ----------------
            var bnd=town.bounds;bool In(double x,double z,double pad)=>x-pad>=bnd.minX&&x+pad<=bnd.maxX&&z-pad>=bnd.minZ&&z+pad<=bnd.maxZ;
            var solid=town.blocks.Where(b=>{var s=NativeExpeditionCityKit.Stem(b.id);return !(s.StartsWith("slab-",StringComparison.Ordinal)||s.EndsWith("-paving",StringComparison.Ordinal));}).ToArray();
            bool Meets(ContentBlock b,double x,double z,double r,double y0,double y1)
            {
                if(b.pos.y+b.size.y/2<=y0||b.pos.y-b.size.y/2>=y1)return false;double yaw=2*Math.Atan2(b.rotation?.y??0,b.rotation?.w??1),c=Math.Cos(yaw),s=Math.Sin(yaw),dx=x-b.pos.x,dz=z-b.pos.z,lx=dx*c-dz*s,lz=dx*s+dz*c;
                double ex=Math.Max(0,Math.Abs(lx)-b.size.x/2),ez=Math.Max(0,Math.Abs(lz)-b.size.z/2);return ex*ex+ez*ez<r*r;
            }
            foreach(var d in area.doors)
            {
                var j=d.join;var hit=solid.Where(b=>Meets(b,j.x,j.z,j.r,j.y+.01,j.y+j.h)).Select(b=>b.id).ToArray();
                Check(In(j.x,j.z,j.r)&&hit.Length==0,d.anchor+": its join circle ("+j.x+", "+j.z+", r "+j.r+", h "+j.h+") lies in the bounds with no block in it"+(hit.Length>0?" ("+string.Join(", ",hit)+")":""));
                foreach(var (v,name) in new[]{(d.interact,"interaction volume"),(d.closure,"closure")})
                {
                    var corners=Enumerable.Range(0,4).Select(k=>{double a=v.yaw*Math.PI/180,u=((k&1)==0?-1:1)*v.sx/2,w=((k&2)==0?-1:1)*v.sz/2;return (v.x+u*Math.Cos(a)+w*Math.Sin(a),v.z-u*Math.Sin(a)+w*Math.Cos(a));}).ToArray();
                    // Inset 2 cm: the closure meets the doorway's face.
                    var cx=v.x;var cz=v.z;var inner=solid.Where(b=>Meets(b,cx,cz,Math.Max(0,Math.Min(v.sx,v.sz)/2-.02),.36,1.85)).Select(b=>b.id).ToArray();
                    Check(corners.All(c=>In(c.Item1,c.Item2,.01))&&inner.Length==0,d.anchor+": its "+name+" lies in the bounds, unobstructed"+(inner.Length>0?" ("+string.Join(", ",inner)+")":""));
                }
            }
            foreach(var g in hub.Gates)
            {
                var hit=solid.Where(b=>Meets(b,g.Closure.x,g.Closure.z,Math.Min(g.Closure.sx,g.Closure.sz)/2-.02,.01,g.Closure.sy-.01)).Select(b=>b.id).ToArray();
                Check(In(g.X,g.Z,g.Width)&&hit.Length==0,g.Id+": in the bounds, its opening unobstructed");
            }
            // ---------------- no shared planes in the grown geometry ----------------
            // The audit's own threshold: closer than 90% of NativeSurfaceLayers.Step is one plane (SurfaceChecks.Run).
            var boxes=SurfaceChecks.Boxes(root,town,true,true);var clashes=SurfaceChecks.Clashes(boxes,town.bounds,NativeSurfaceLayers.Step*.9f);
            bool Grown(System.Numerics.Vector3 p)=>Math.Abs(p.X)>44.01||p.Z<-44.01;
            int grown=clashes.Count(c=>Grown(c.At)),blocksIn=boxes.Count(b=>!b.Kit&&Grown(b.C)),partsIn=boxes.Count(b=>b.Kit&&Grown(b.C));
            Check(grown==0&&clashes.Count==0,"no two visible faces of one facing share a plane beyond the city's edge (the grown geometry: "+blocksIn+" blocks, "+partsIn+" kit parts with the hub record; "+grown+" clashes"+(grown>0?": "+string.Join("; ",clashes.Where(c=>Grown(c.At)).Take(4).Select(c=>boxes[c.A].Id+" / "+boxes[c.B].Id+" at "+c.At.X.ToString("0.0")+","+c.At.Y.ToString("0.0")+","+c.At.Z.ToString("0.0"))):"")+"; the whole town "+clashes.Count+")");
            // ---------------- the per-frame cost ----------------
            int Verts(int n)=>n*m.MeshVertices;
            string[] eyeNames={"the east gate","25% along the Ward Zero route","50%","75%","the Ward Zero circle","the sanctuary"};
            for(int i=0;i<m.View.Length;i++)Check(m.View[i]<=BeforeView[i]&&m.ViewLow[i]<=BeforeViewLow[i]*1.10,"from "+eyeNames[i]+" the runtime draws "+m.View[i]+" kit parts in "+m.ViewDraws[i]+" draws ("+Verts(m.View[i])+" vertices), no more than exp-layout-6's "+BeforeView[i]+" ("+Verts(BeforeView[i])+"); the low tier "+m.ViewLow[i]+" (exp-layout-6 "+BeforeViewLow[i]+", at most 10% more)");
            Check(m.Parts<=ExpeditionDressingChecks.MaxParts&&m.Low<=ExpeditionDressingChecks.MaxLowParts&&m.Casters<=ExpeditionDressingChecks.MaxCasters,"the kit: "+m.Parts+" parts (exp-layout-6 "+BeforeParts+"), "+m.Low+" low tier ("+BeforeLow+"), "+m.Casters+" casting ("+BeforeCasters+"), within the dressing budgets");
            // ---------------- no allocation per frame ----------------
            var kit=NativeExpeditionCityKit.Build(town.blocks,area,hub,town.bounds);var spots=Spots(kit,town.blocks);var eyes=Eyes(hub,area);
            void Frame(int f){var e=eyes[f%eyes.Length];Drawn(kit,spots,(e.x+(float)Math.Sin(f*.1),e.y,e.z),true,m.Far,(f&1)!=0,out _,out _);}
            for(int f=0;f<6;f++)Frame(f);long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<120;f++)Frame(f);long spent=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(spent==0,"the range selection allocates nothing per frame ("+spent+" bytes over 120 frames)");
            var w=m.Doors["exp-ward-zero"];var bw=Before["exp-ward-zero"];
            Console.WriteLine("PASS expedition city ("+count+" checks: exp-layout-7, "+m.Width+" x "+m.Depth+" m, "+m.Nodes+" navigation nodes all reached (exp-layout-6 "+BeforeNodes+"); slots to the Ward Zero circle "+bw.slotJoin.ToString("0.0")+" -> "+w.slotJoin.ToString("0.0")+" m (x"+(w.slotJoin/bw.slotJoin).ToString("0.00")+"), gate to circle "+bw.gateJoin.ToString("0.0")+" -> "+w.gateJoin.ToString("0.0")+" m (x"+(w.gateJoin/bw.gateJoin).ToString("0.00")+"), the station and the foundry alike; "
                +hub.Zones.Length+" zones on reached floor; circles, doors and gates in bounds and clear; 0 shared planes in the grown geometry; kit drawn per frame at most "+m.View.Max()+" parts in "+m.ViewDraws.Max()+" draws, low tier "+m.ViewLow.Max()+" (exp-layout-6 "+BeforeView.Max()+" in 26, low "+BeforeViewLow.Max()+"); 0 bytes per frame)");
        }
    }
}
