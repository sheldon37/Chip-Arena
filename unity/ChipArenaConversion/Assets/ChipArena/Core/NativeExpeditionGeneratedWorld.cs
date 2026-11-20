using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ChipArena.UnityConversion
{
    // Engine-free collision blockout, never a substitute for native physics acceptance or Claude's dressing.
    // Construct once for a roll, before creating any PvE session. No resource/global content is mutated.
    public sealed class ExpeditionGeneratedWorld
    {
        public NativeLayeredCityWalk City;
        public NativeCityBleed Bleed;
        public string[] HallSlots;
        public ContentMap Map;
        public ExpeditionAreaLayout Area;
        public ExpeditionHubLayout Hub;
        public ExpeditionDeploymentState Blueprint;
        public string Compatibility;
    }
    public static partial class NativeExpeditionGeneratedWorld
    {
        public const string Version="deployment-world-blockout-4";
        /// <summary>Each instance's building behind its entrance frame, and the walls closing any street at the floor's edge.</summary>
        public const float HallHeight=18,EdgeHeight=6;
        public const string MapId="expedition-generated-city";
        private const float NavStep=2;
        internal static V3 CacheForward(float yaw)
        {
            double angle=yaw*Math.PI/180;
            return new V3((float)Math.Sin(angle),0,(float)Math.Cos(angle));
        }
        // The Hulk stands within the 2 m interaction radius, clear of the solid locker.
        public static ExpeditionPoint CacheApproach(ExpeditionWorldAnchor a)
        {
            var f=CacheForward(a.yaw);
            return new ExpeditionPoint{x=a.x+1.7f*f.x,y=a.y,z=a.z+1.7f*f.z,yaw=a.yaw};
        }
        // For a yawed overhead box, the shorter distance to an outside face is the overhang depth
        // above the column axis. Supported lockers reserve an opening to the sky.
        public static bool CacheColumnClear(ExpeditionWorldAnchor a,IEnumerable<ContentBlock> blocks)
        {
            foreach(var block in blocks)
            {
                if(block.pos.y-block.size.y/2<a.y+.62f)continue;
                float dx=a.x-block.pos.x,dz=a.z-block.pos.z;
                double angle=2*Math.Atan2(block.rotation.y,block.rotation.w),c=Math.Cos(angle),s=Math.Sin(angle);
                double depthX=block.size.x/2-Math.Abs(dx*c-dz*s);
                double depthZ=block.size.z/2-Math.Abs(dx*s+dz*c);
                if(Math.Min(depthX,depthZ)>.3)return false;
            }
            return true;
        }
        public static ContentBlock CacheBlock(ExpeditionWorldAnchor a)
        {
            double half=a.yaw*Math.PI/360;
            return new ContentBlock{id=a.id+"-block",role="static",material="metal",
                pos=new V3(a.x,a.y+.31f,a.z),size=new V3(.92f,.62f,.58f),
                rotation=new ContentRotation{y=(float)Math.Sin(half),w=(float)Math.Cos(half)}};
        }
        internal static ExpeditionInteractionLayout CacheInteraction(ExpeditionWorldAnchor a)
        {
            var f=CacheForward(a.yaw);
            return new ExpeditionInteractionLayout{anchor=a.id,x=a.x+.9f*f.x,y=a.y,z=a.z+.9f*f.z,range=2};
        }
        private static ExpeditionPoint Point(float x,float z,float yaw=0)=>new ExpeditionPoint{x=x,z=z,yaw=yaw};
        private static ExpeditionVolume Volume(float x,float z,float sx,float sz,float h=4)=>new ExpeditionVolume{x=x,z=z,y=h/2,sx=sx,sy=h,sz=sz};
        private static string I(int value)=>value.ToString(CultureInfo.InvariantCulture);
        private static string Key(int x,int z)=>x.ToString(CultureInfo.InvariantCulture)+":"+z.ToString(CultureInfo.InvariantCulture);
        private static ContentBlock Block(string id,float x,float z,float sx,float sz,float h,string material="stone",float bottom=0)=>
            new ContentBlock{id=MapId+"-"+id,role="static",material=material,pos=new V3(x,bottom+h/2,z),size=new V3(sx,h,sz),rotation=new ContentRotation()};

        // Only a room reconstructed against its locally validated catalog can supply this roll.
        public static ExpeditionGeneratedWorld Build(ExpeditionDeploymentRoom room)
        {
            if(room==null)throw new ArgumentNullException(nameof(room));
            var state=room.Snapshot();
            if(!state.worldRolled)throw new InvalidOperationException("Roll instances before generating a world.");
            var plan=NativeCityLayout.Plans[room.Ticket.cityPlan];
            var city=new NativeLayeredCityWalk(plan,state.worldSeed);
            city.Map.id=MapId;
            city.Map.tag="Expedition";
            city.Map.look="expedition-night";
            for(int i=0;i<8;i++){
                var row=new MapSpawn{pos=new V3(i%4*2-3,1.1f,i/4*3-2)};
                city.Map.spawns.ffa[i]=row;city.Map.spawns.teams[i/4].points[i%4]=row;
            }
            city.Map.nav=NativeMeridianNavigation.Build(city.Map);
            var gates=new List<ExpeditionHubGate>();var fences=new List<ExpeditionHubFence>();
            foreach(var direction in new[]{(x:0,z:1,side:"north",yaw:0f),(x:1,z:0,side:"east",yaw:90f),(x:0,z:-1,side:"south",yaw:180f),(x:-1,z:0,side:"west",yaw:270f)})
            {
                var d=direction;string id="generated-gate-"+d.side;var ids=new List<string>();
                foreach(int sign in new[]{-1,1})
                {
                    string block="camp-fence-"+gates.Count+"-"+sign;
                    ids.Add(block);fences.Add(new ExpeditionHubFence{Block=block,Kind="metal",Side=d.side,Gate=id});
                }
                gates.Add(new ExpeditionHubGate{Id=id,Kind="metal",Motion="slide",Side=d.side,X=d.x*25,Z=d.z*25,Yaw=d.yaw,Width=10,Height=3.2f,
                    Closure=Volume(d.x*25,d.z*25,d.x==0?10:.4f,d.x==0?.4f:10,3.2f),
                    Interact=Volume(d.x*25,d.z*25,10,10),Sensor=Volume(d.x*25,d.z*25,12,12),Inside=Point(d.x*22,d.z*22),Outside=Point(d.x*28,d.z*28),Fence=ids.ToArray()});
            }
            var area=new ExpeditionAreaLayout{id="exp-city",role="world",map=MapId,slots=NativeExpeditionHubPlan.LandingField(state.worldSeed),
                sanctuary=new[]{Volume(0,0,49.6f,49.6f)}};
            var result=new ExpeditionGeneratedWorld{Area=area,Blueprint=state,Hub=new ExpeditionHubLayout{Area=area.id,Fence=fences.ToArray(),Gates=gates.ToArray()}};
            PopulateMeridian(result,city);
            NativeMapRules.Validate(result.Map);
            result.Compatibility=Digest(room.Ticket,result);
            return result;
        }
        // Parse afresh to keep every authored interior detached and unchanged. The runtime must still load
        // this manifest plus Hub and Map as one per-run bundle; hardcoded resource readers cannot consume it yet.
        public static ExpeditionLayout ComposeLayout(ExpeditionGeneratedWorld world,string authoredJson,ExpeditionAreaLayout generatedInstance=null,ExpeditionAreaLayout poolrooms=null)
        {
            if(world==null||world.Compatibility==null)throw new ArgumentException("Generated world required.");
            var layout=NativeExpeditionLayout.Parse(authoredJson);
            if(generatedInstance!=null)layout.areas=layout.areas.Concat(new[]{generatedInstance}).ToArray();
            if(poolrooms!=null)layout.areas=layout.areas.Concat(new[]{poolrooms}).ToArray();
            if(layout.world!=world.Area.id||world.Blueprint.instances.Any(id=>layout.Area(id)?.role!="instance"))
                throw new ArgumentException("Generated roster does not match the authored layout.");
            layout.instances=(string[])world.Blueprint.instances.Clone();
            layout.areas=new[]{world.Area}.Concat(layout.instances.Select(layout.Area)).ToArray();
            foreach(var area in layout.areas)
                foreach(var chest in area.anchors.Where(a=>area.CacheCandidates.ContainsKey(a.id)))
                {
                    var candidates=area.CacheCandidates[chest.id];
                    var rng=new NativeExpeditionDeployment.Roller(NativePveRandom.Hash(world.Blueprint.worldSeed,chest.id));
                    var spot=candidates[rng.Index(candidates.Length)];
                    chest.x=spot.x;chest.y=spot.y;chest.z=spot.z;chest.yaw=spot.yaw;
                    int i=Array.FindIndex(area.interactions,it=>it.anchor==chest.id);
                    area.interactions[i]=CacheInteraction(chest);
                }
            layout.revision=Version;layout.compatibility=NativeMapRules.Revision(authoredJson+"\n"+world.Compatibility);
            return layout;
        }
        // Axis-aligned, ground-level generator only. Runtime must also validate in its real physics scene.
        public static bool Supported(ContentMap map,V3 p,float radius,float height)
        {
            var b=map.bounds;
            if(!NativeMapRules.Finite(p)||!MathUtil.Finite(radius)||!MathUtil.Finite(height)||radius<=0||height<=0||Math.Abs(p.y)>.01f||
                p.x-radius<b.minX||p.x+radius>b.maxX||p.z-radius<b.minZ||p.z+radius>b.maxZ||height>b.ceiling)return false;
            bool floor=false;
            foreach(var block in map.blocks)
            {
                if(Math.Abs(block.pos.y+block.size.y/2-p.y)<=.01f&&Math.Abs(p.x-block.pos.x)+radius<=block.size.x/2&&Math.Abs(p.z-block.pos.z)+radius<=block.size.z/2)floor=true;
                if(block.pos.y+block.size.y/2<=.01f||block.pos.y-block.size.y/2>=height)continue;
                float bx=Math.Abs(p.x-block.pos.x),bz=Math.Abs(p.z-block.pos.z);
                // Generated lockers are cardinal, including quarter turns on the east/west facades.
                if(block.rotation!=null&&Math.Abs(2*block.rotation.y*block.rotation.w)>.5f)(bx,bz)=(bz,bx);
                float dx=Math.Max(0,bx-block.size.x/2),dz=Math.Max(0,bz-block.size.z/2);
                if(dx*dx+dz*dz<radius*radius)return false;
            }
            return floor;
        }
        private static bool Clear(ContentMap map,V3 a,V3 b,float radius,float height)
        {
            int steps=Math.Max(1,(int)Math.Ceiling((b-a).Length/.25));
            for(int i=0;i<=steps;i++)if(!Supported(map,a+(b-a)*(i/(float)steps),radius,height))return false;
            return true;
        }
        private static MapNav Navigation(ContentMap map)
        {
            var nodes=new Dictionary<(int x,int z),MapNavNode>();
            for(int x=-62;x<=62;x++)for(int z=-47;z<=47;z++)
            {
                var p=new V3(x*NavStep,0,z*NavStep);
                // One graph is navigable even by the largest actor; smaller pilots also have the full streets.
                if(Supported(map,p,NativeHordeKinds.HulkRadius,NativeHordeKinds.HulkHeight))nodes.Add((x,z),new MapNavNode{id="g:"+Key(x,z),pos=p+new V3(0,.9f,0)});
            }
            var links=new List<MapNavLink>();
            foreach(var pair in nodes.OrderBy(p=>p.Key.x).ThenBy(p=>p.Key.z))foreach(var d in new[]{(1,0),(0,1)})
                if(nodes.TryGetValue((pair.Key.x+d.Item1,pair.Key.z+d.Item2),out var to)&&
                    Clear(map,pair.Value.pos-new V3(0,.9f,0),to.pos-new V3(0,.9f,0),NativeHordeKinds.HulkRadius,NativeHordeKinds.HulkHeight))
                    links.Add(new MapNavLink{id="l:"+I(links.Count),a=pair.Value.id,b=to.id});
            return new MapNav{nodes=nodes.Values.OrderBy(n=>n.id,StringComparer.Ordinal).ToArray(),links=links.ToArray()};
        }
        private static void Routes(ExpeditionGeneratedWorld world)
        {
            var nodes=world.Map.nav.nodes.ToDictionary(n=>n.id);var adj=nodes.Keys.ToDictionary(k=>k,k=>new List<string>());
            foreach(var l in world.Map.nav.links){adj[l.a].Add(l.b);adj[l.b].Add(l.a);}
            string Near(V3 p)=>nodes.Values.Where(n=>(n.pos-new V3(0,.9f,0)-p).Length<=3&&Clear(world.Map,p,n.pos-new V3(0,.9f,0),NativeHordeKinds.HulkRadius,NativeHordeKinds.HulkHeight))
                .OrderBy(n=>(n.pos-new V3(0,.9f,0)-p).Length).ThenBy(n=>n.id,StringComparer.Ordinal).First().id;
            var distances=new Dictionary<string,float>();var routes=new List<ExpeditionHubRoute>();
            foreach(var gate in world.Hub.Gates)
            {
                string start=Near(gate.Outside.Position);var seen=new Dictionary<string,string>{{start,null}};
                var dist=new Dictionary<string,float>{{start,0}};var q=new Queue<string>();q.Enqueue(start);
                while(q.Count>0){string from=q.Dequeue();foreach(string to in adj[from])if(!seen.ContainsKey(to)){seen[to]=from;dist[to]=dist[from]+NavStep;q.Enqueue(to);}}
                foreach(var pair in dist)if(!distances.TryGetValue(pair.Key,out var old)||pair.Value<old)distances[pair.Key]=pair.Value;
                foreach(var district in world.Blueprint.districts.Where(d=>d.side==gate.Side))
                {
                    var door=world.Area.doors.Single(d=>d.anchor=="exp-city:door:"+district.instance);string end=Near(door.approach.Position);
                    var path=new List<ExpeditionPoint>();for(string n=end;n!=null;n=seen[n])path.Add(Point(nodes[n].pos.x,nodes[n].pos.z));path.Reverse();
                    var turns=new List<ExpeditionPoint>();for(int i=1;i<path.Count-1;i++)if((path[i].Position-path[i-1].Position).x!=(path[i+1].Position-path[i].Position).x||
                        (path[i].Position-path[i-1].Position).z!=(path[i+1].Position-path[i].Position).z)turns.Add(path[i]);
                    float length=dist[end]+(nodes[start].pos-new V3(0,.9f,0)-gate.Outside.Position).Length+(nodes[end].pos-new V3(0,.9f,0)-door.approach.Position).Length;
                    routes.Add(new ExpeditionHubRoute{To=district.instance,Gate=gate.Id,Path=length,Straight=(door.approach.Position-gate.Outside.Position).Length,Run=length/6.8f,Sprint=length/(6.8f*1.35f),Approach=door.approach,X=door.approach.x,Z=door.approach.z,Via=turns.ToArray()});
                }
            }
            world.Hub.Routes=routes.ToArray();
            foreach(var zone in world.Hub.Zones)
            {
                var at=world.Area.spawns.Single(s=>s.entry.e==zone.Spawns[0]).entry.@exit.Position();float d=distances[Near(at)];
                zone.Near=d;zone.Far=d+9;zone.Ring=d<35?"near":d<65?"mid":"far";
            }
        }
        public static void Validate(ExpeditionGeneratedWorld world)
        {
            var m=world.Map;var a=world.Area;var nodes=m.nav.nodes.ToDictionary(n=>n.id);var adj=nodes.Keys.ToDictionary(k=>k,k=>new List<string>());
            foreach(var l in m.nav.links){adj[l.a].Add(l.b);adj[l.b].Add(l.a);}
            string Near(V3 p)
            {
                var n=nodes.Values.Where(v=>(v.pos-new V3(0,.9f,0)-p).Length<=3&&Clear(m,p,v.pos-new V3(0,.9f,0),NativeHordeKinds.HulkRadius,NativeHordeKinds.HulkHeight))
                    .OrderBy(v=>(v.pos-new V3(0,.9f,0)-p).Length).FirstOrDefault();
                return n?.id??throw new InvalidOperationException("Generated navigation cannot attach at "+p.x+", "+p.z);
            }
            string start=Near(a.slots[0].Position);var seen=new HashSet<string>{start};var queue=new Queue<string>();queue.Enqueue(start);
            while(queue.Count>0)foreach(var id in adj[queue.Dequeue()])if(seen.Add(id))queue.Enqueue(id);
            if(seen.Count!=nodes.Count)throw new InvalidOperationException("Generated world has an isolated navigation pocket.");
            var points=a.slots.Concat(a.bossPoints).Concat(a.doors.SelectMany(d=>d.returns.Concat(new[]{d.approach,Point(d.join.x,d.join.z)})))
                .Concat(a.anchors.Where(c=>c.kind=="chest").Select(CacheApproach)).Concat(a.spawns.Select(s=>Point(s.entry.@exit.x,s.entry.@exit.z)))
                .Concat(world.Hub.Gates.SelectMany(g=>new[]{g.Inside,g.Outside}));
            foreach(var p in points)if(!Supported(m,p.Position,NativeHordeKinds.HulkRadius,NativeHordeKinds.HulkHeight)||!seen.Contains(Near(p.Position)))
                throw new InvalidOperationException("Generated world has an unsupported or unreachable gameplay point.");
            if(a.spawns.Length==0||a.spawns.Length>256||a.spawns.Any(s=>a.sanctuary.Any(v=>v.Contains(s.entry.@exit.Position()))))
                throw new InvalidOperationException("Generated encounter placement is invalid.");
        }
        private static string Digest(ExpeditionDeploymentTicket ticket,ExpeditionGeneratedWorld world)
        {
            var text=new StringBuilder(Version).Append('\n').Append(ticket.compatibility).Append('\n').Append(world.Blueprint.worldSeed.ToString(CultureInfo.InvariantCulture));
            // Exact generated collision/nav receipt, independent of current culture and JSON/Unity serialization.
            void F(float v)=>text.Append('|').Append(v.ToString("R",CultureInfo.InvariantCulture));
            foreach(var b in world.Map.blocks){text.Append('\n').Append(b.id).Append('|').Append(b.material);F(b.pos.x);F(b.pos.y);F(b.pos.z);F(b.size.x);F(b.size.y);F(b.size.z);F(b.rotation.y);F(b.rotation.w);}
            text.Append('|').Append(NativeCityLayout.Version).Append('|').Append(I(ticket.cityPlan));
            foreach(var ramp in world.Map.ramps){text.Append(ramp.id).Append('|').Append(ramp.kind);F(ramp.low.x);F(ramp.low.y);F(ramp.low.z);F(ramp.high.x);F(ramp.high.y);F(ramp.high.z);F(ramp.width);F(ramp.bottom);}
            foreach(var n in world.Map.nav.nodes){text.Append('\n').Append(n.id);F(n.pos.x);F(n.pos.y);F(n.pos.z);text.Append('|').Append(I(n.layer));}
            foreach(var l in world.Map.nav.links)text.Append('\n').Append(l.id).Append('|').Append(l.a).Append('|').Append(l.b).Append('|').Append(l.kind);
            foreach(var d in world.Blueprint.districts)text.Append('\n').Append(d.instance).Append('|').Append(d.theme).Append('|').Append(d.side);
            void P(ExpeditionPoint p){F(p.x);F(p.y);F(p.z);F(p.yaw);}
            void V(ExpeditionVolume v){F(v.x);F(v.y);F(v.z);F(v.sx);F(v.sy);F(v.sz);F(v.yaw);}
            var a=world.Area;var hub=world.Hub;
            text.Append("\narea|").Append(a.id).Append('|').Append(a.map);
            foreach(var v in a.sanctuary)V(v);foreach(var p in a.slots)P(p);foreach(var p in a.bossPoints)P(p);V(a.bossTrigger);
            foreach(var anchor in a.anchors){text.Append("\nanchor|").Append(anchor.id).Append('|').Append(anchor.kind).Append('|').Append(anchor.building);F(anchor.x);F(anchor.y);F(anchor.z);F(anchor.yaw);}
            foreach(var door in a.doors){text.Append("\ndoor|").Append(door.anchor);V(door.interact);V(door.closure);P(door.approach);F(door.join.x);F(door.join.y);F(door.join.z);F(door.join.r);F(door.join.h);foreach(var p in door.returns)P(p);}
            foreach(var g in a.gates){text.Append("\ngate|").Append(g.id).Append('|').Append(g.opens);V(g.closure);}
            foreach(var i in a.interactions){text.Append("\ninteract|").Append(i.anchor);F(i.x);F(i.y);F(i.z);F(i.range);}
            foreach(var spawn in a.spawns){var e=spawn.entry;text.Append("\nspawn|").Append(e.id).Append('|').Append(e.kind).Append('|').Append(I(e.e));F(e.from.x);F(e.from.y);F(e.from.z);F(e.@exit.x);F(e.@exit.y);F(e.@exit.z);F(e.clear);foreach(var k in spawn.allow)text.Append('|').Append(k);}
            foreach(var g in hub.Gates){text.Append("\nhub-gate|").Append(g.Id);V(g.Closure);V(g.Interact);V(g.Sensor);P(g.Inside);P(g.Outside);}
            foreach(var z in hub.Zones){text.Append("\nzone|").Append(z.Id).Append('|').Append(z.Ring);V(z.Volume);F(z.Near);F(z.Far);foreach(int e in z.Spawns)text.Append('|').Append(I(e));}
            foreach(var r in hub.Routes){text.Append("\nroute|").Append(r.To).Append('|').Append(r.Gate);F(r.Path);P(r.Approach);foreach(var p in r.Via)P(p);}
            foreach(var c in world.Blueprint.cells){text.Append("\ncell|").Append(Key(c.x,c.z)).Append(c.road?'R':'B');foreach(int v in c.influences)text.Append('|').Append(I(v));}
            return NativeMapRules.Revision(text.ToString());
        }
    }
}
