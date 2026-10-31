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
        public ContentMap Map;
        public ExpeditionAreaLayout Area;
        public ExpeditionHubLayout Hub;
        public ExpeditionDeploymentState Blueprint;
        public string Compatibility;
    }
    public static class NativeExpeditionGeneratedWorld
    {
        public const string Version="deployment-world-blockout-1";
        public const string MapId="expedition-generated-city";
        private const float NavStep=2;
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
            var roads=new HashSet<string>(state.cells.Where(c=>c.road).Select(c=>Key(c.x,c.z)));
            // A clear apron joins any seeded street to the four actual sanctuary gates.
            for(int x=-3;x<=3;x++)for(int z=-3;z<=3;z++)roads.Add(Key(x,z));
            // Final boss has its own northern court, independent of the three selected entrances.
            for(int z=3;z<=8;z++)roads.Add(Key(0,z));
            for(int x=-2;x<=2;x++)for(int z=6;z<=8;z++)roads.Add(Key(x,z));
            foreach(var d in state.districts)
                for(int x=d.x-1;x<=d.x+1;x++)for(int z=d.z-1;z<=d.z+1;z++)roads.Add(Key(x,z));
            var blocks=new List<ContentBlock>{Block("floor",0,0,250,190,1,"foundation",-1)};
            foreach(var c in state.cells.Where(c=>!roads.Contains(Key(c.x,c.z))))
            {
                // Filled blocks have no inaccessible alleys or unsupported visual floors. Tops are excluded from pilots.
                int style=(int)((state.worldSeed+unchecked((uint)(c.x+12)*31+(uint)(c.z+9)*17))%4);
                blocks.Add(Block("building-"+Key(c.x,c.z),c.x*10,c.z*10,10,10,8+style*2));
            }
            var gates=new List<ExpeditionHubGate>();var fences=new List<ExpeditionHubFence>();
            foreach(var direction in new[]{(x:0,z:1,side:"north",yaw:0f),(x:1,z:0,side:"east",yaw:90f),(x:0,z:-1,side:"south",yaw:180f),(x:-1,z:0,side:"west",yaw:270f)})
            {
                var d=direction;string id="generated-gate-"+d.side;var ids=new List<string>();
                foreach(int sign in new[]{-1,1})
                {
                    var b=Block("fence-"+d.side+I(sign),d.x*25+d.z*15*sign,d.z*25-d.x*15*sign,d.x==0?20:.4f,d.x==0?.4f:20,3.2f,"metal");
                    blocks.Add(b);ids.Add(b.id);fences.Add(new ExpeditionHubFence{Block=b.id,Kind="metal",Side=d.side,Gate=id});
                }
                gates.Add(new ExpeditionHubGate{Id=id,Kind="metal",Motion="slide",Side=d.side,X=d.x*25,Z=d.z*25,Yaw=d.yaw,Width=10,Height=3.2f,
                    Closure=Volume(d.x*25,d.z*25,d.x==0?10:.4f,d.x==0?.4f:10,3.2f),
                    Interact=Volume(d.x*25,d.z*25,10,10),Sensor=Volume(d.x*25,d.z*25,12,12),Inside=Point(d.x*22,d.z*22),Outside=Point(d.x*28,d.z*28),Fence=ids.ToArray()});
            }
            var anchors=new List<ExpeditionWorldAnchor>();var doors=new List<ExpeditionDoorLayout>();
            foreach(var d in state.districts)
            {
                float x=d.x*10,z=d.z*10,ix=d.side=="east"?-1:d.side=="west"?1:0,iz=d.side=="south"?1:0;
                float yaw=d.side=="east"?270:d.side=="west"?90:0;
                string id="exp-city:door:"+d.instance;
                anchors.Add(new ExpeditionWorldAnchor{id=id,area="exp-city",kind="door",building=d.instance,x=x,z=z,yaw=yaw});
                foreach(int sign in new[]{-1,1})blocks.Add(Block("entrance-"+d.instance+I(sign),x+iz*4*sign,z-ix*4*sign,ix==0?2:1,ix==0?1:2,6,"metal"));
                blocks.Add(Block("lintel-"+d.instance,x,z,ix==0?10:1,ix==0?1:10,1,"metal",6));
                doors.Add(new ExpeditionDoorLayout{anchor=id,interact=Volume(x+ix*2,z+iz*2,5,5),closure=Volume(x,z,ix==0?6:1,ix==0?1:6),
                    approach=Point(x+ix*4,z+iz*4,yaw),join=new ExpeditionJoinCylinder{x=x+ix*8,z=z+iz*8,r=3,h=3},entries=Array.Empty<ExpeditionPoint>(),
                    returns=Enumerable.Range(0,8).Select(i=>Point(x+ix*(6+i/4*2)+iz*((i%4)*2-3),z+iz*(6+i/4*2)-ix*((i%4)*2-3),yaw)).ToArray()});
            }
            // A closed north court has one 10 m entrance. Its gate is controlled by the existing final-boss rules.
            blocks.Add(Block("final-west",-25,70,.4f,30,6));blocks.Add(Block("final-east",25,70,.4f,30,6));
            blocks.Add(Block("final-back",0,85,50,.4f,6));
            blocks.Add(Block("final-front-west",-15,55,20,.4f,6));blocks.Add(Block("final-front-east",15,55,20,.4f,6));
            var slots=Enumerable.Range(0,8).Select(i=>Point(i%4*2-3,i/4*3-2)).ToArray();
            var spawns=new List<ExpeditionSpawnLayout>();var zones=new List<ExpeditionHubZone>();var interactions=new List<ExpeditionInteractionLayout>();
            // Use the seeded trunks (not the courts/apron) for encounters. Reserve door and final approaches.
            foreach(var c in state.cells.Where(c=>c.road&&!c.sanctuary).OrderBy(c=>c.x).ThenBy(c=>c.z))
            {
                float x=c.x*10,z=c.z*10;
                if(Math.Max(Math.Abs(x),Math.Abs(z))<40||z>=45||doors.Any(d=>(new V3(x,0,z)-d.approach.Position).Length<20))continue;
                int n=spawns.Count;var point=new PvePoint{x=x,z=z};
                spawns.Add(new ExpeditionSpawnLayout{entry=new PveEntry{e=n,id="generated-spawn-"+I(n),kind="door",from=point,@exit=new PvePoint{x=x,z=z},clear=3},allow=new[]{"shambler","runner","spitter","brute"}});
                string ring=Math.Max(Math.Abs(x),Math.Abs(z))<55?"near":Math.Max(Math.Abs(x),Math.Abs(z))<85?"mid":"far";
                zones.Add(new ExpeditionHubZone{Id="generated-zone-"+I(n),Ring=ring,Volume=Volume(x,z,9,9),Spawns=new[]{n}});
                if(n%7==0)
                {
                    string id="exp-city:cache:"+I(n);
                    anchors.Add(new ExpeditionWorldAnchor{id=id,area="exp-city",kind="chest",x=x+2,z=z});
                    interactions.Add(new ExpeditionInteractionLayout{anchor=id,x=x+2,z=z,range=2.5f});
                }
            }
            var map=new ContentMap{id=MapId,name="Generated Expedition",tag="Expedition",theme="expedition",ambience="",look="expedition-city",sky=2761800,floor=2894384,accent=16747084,unity=true,
                bounds=new MapBounds{minX=-125,maxX=125,minZ=-95,maxZ=95,ceiling=40,killY=-8},blocks=blocks.ToArray(),
                spawns=new MapSpawns{ffa=slots.Select(p=>new MapSpawn{pos=p.Position+new V3(0,1.1f,0),yaw=p.yaw}).ToArray(),
                    teams=new[]{0,1}.Select(side=>new MapTeamSpawns{side=side,points=slots.Skip(side*4).Take(4).Select(p=>new MapSpawn{pos=p.Position+new V3(0,1.1f,0)}).ToArray()}).ToArray()},
                hazards=Array.Empty<MapHazard>(),ramps=Array.Empty<MapRamp>(),segments=Array.Empty<StructureDefinition>(),
                pilotBlocks=blocks.Where(b=>b.id.Contains("-building-")).Select(b=>new MapPilotBlock{id=b.id+"-column",center=b.pos,size=new V3(b.size.x,40,b.size.z),bottom=0}).ToArray()};
            var area=new ExpeditionAreaLayout{id="exp-city",role="world",map=MapId,anchors=anchors.ToArray(),doors=doors.ToArray(),interactions=interactions.ToArray(),
                gates=new[]{new ExpeditionGateLayout{id="exp-city:final-gate",opens="final",closure=Volume(0,55,10,.4f,6)}},
                slots=slots,sanctuary=new[]{Volume(0,0,49.6f,49.6f)},spawns=spawns.ToArray(),bossPoints=new[]{Point(0,72,180)},bossTrigger=Volume(0,66,40,18)};
            map.nav=Navigation(map);
            NativeMapRules.Validate(map);
            var result=new ExpeditionGeneratedWorld{Map=map,Area=area,Blueprint=state,Hub=new ExpeditionHubLayout{Area=area.id,Fence=fences.ToArray(),Gates=gates.ToArray(),Zones=zones.ToArray()}};
            // Generated street shape is exposed to the theme renderer, including the reserved courts and gate apron.
            foreach(var c in result.Blueprint.cells)c.road=roads.Contains(Key(c.x,c.z));
            Routes(result);
            Validate(result);
            result.Compatibility=Digest(room.Ticket,result);
            return result;
        }
        // Parse afresh to keep every authored interior detached and unchanged. The runtime must still load
        // this manifest plus Hub and Map as one per-run bundle; hardcoded resource readers cannot consume it yet.
        public static ExpeditionLayout ComposeLayout(ExpeditionGeneratedWorld world,string authoredJson)
        {
            if(world==null||world.Compatibility==null)throw new ArgumentException("Generated world required.");
            var layout=NativeExpeditionLayout.Parse(authoredJson);
            if(layout.world!=world.Area.id||world.Blueprint.instances.Any(id=>layout.Area(id)?.role!="instance"))
                throw new ArgumentException("Generated roster does not match the authored layout.");
            layout.instances=(string[])world.Blueprint.instances.Clone();
            layout.areas=new[]{world.Area}.Concat(layout.instances.Select(layout.Area)).ToArray();
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
                float dx=Math.Max(0,Math.Abs(p.x-block.pos.x)-block.size.x/2),dz=Math.Max(0,Math.Abs(p.z-block.pos.z)-block.size.z/2);
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
                .Concat(a.interactions.Select(i=>Point(i.x,i.z))).Concat(a.spawns.Select(s=>Point(s.entry.@exit.x,s.entry.@exit.z)))
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
            foreach(var b in world.Map.blocks){text.Append('\n').Append(b.id).Append('|').Append(b.material);F(b.pos.x);F(b.pos.y);F(b.pos.z);F(b.size.x);F(b.size.y);F(b.size.z);}
            foreach(var n in world.Map.nav.nodes){text.Append('\n').Append(n.id);F(n.pos.x);F(n.pos.y);F(n.pos.z);}
            foreach(var l in world.Map.nav.links)text.Append('\n').Append(l.id).Append('|').Append(l.a).Append('|').Append(l.b);
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
