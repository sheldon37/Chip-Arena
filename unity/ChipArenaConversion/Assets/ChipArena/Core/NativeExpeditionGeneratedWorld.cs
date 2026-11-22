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
        public const string Version="deployment-world-blockout-6";
        public const string MapId="expedition-generated-city";
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
                var row=new MapSpawn{pos=new V3(i%4*2-3,1.1f,i/4*3+9)};
                city.Map.spawns.ffa[i]=row;city.Map.spawns.teams[i/4].points[i%4]=row;
            }
            var machines=NativeExpeditionHubPlan.Machines();
            city.Map.blocks=city.Map.blocks.Concat(NativeExpeditionHubPlan.Blocks()).ToArray();
            city.Map.nav=NativeMeridianNavigation.Build(city.Map,machines);
            var gates=new List<ExpeditionHubGate>();var fences=new List<ExpeditionHubFence>();
            foreach(var direction in new[]{(x:0,z:1,side:"north",yaw:0f),(x:1,z:0,side:"east",yaw:90f),(x:0,z:-1,side:"south",yaw:180f),(x:-1,z:0,side:"west",yaw:270f)})
            {
                var d=direction;string id="generated-gate-"+d.side;var ids=new List<string>();
                foreach(int sign in new[]{-1,1})
                {
                    string block="camp-fence-"+I(gates.Count)+"-"+I(sign);
                    ids.Add(block);fences.Add(new ExpeditionHubFence{Block=block,Kind="metal",Side=d.side,Gate=id});
                }
                // On sloped exits the street can already be below the plaza three metres past the gate.
                var outside=Point(d.x*28,d.z*28);
                var street=city.Map.nav.nodes.Where(n=>n.pos.x==outside.x&&n.pos.z==outside.z)
                    .OrderBy(n=>Math.Abs(n.pos.y-.9f)).First();
                outside.y=street.pos.y-.9f;
                gates.Add(new ExpeditionHubGate{Id=id,Kind="metal",Motion="slide",Side=d.side,X=d.x*25,Z=d.z*25,Yaw=d.yaw,Width=10,Height=3.2f,
                    Closure=Volume(d.x*25,d.z*25,d.x==0?10:.4f,d.x==0?.4f:10,3.2f),
                    Interact=Volume(d.x*25,d.z*25,10,10),Sensor=Volume(d.x*25,d.z*25,12,12),Inside=Point(d.x*22,d.z*22),Outside=outside,Fence=ids.ToArray()});
            }
            var area=new ExpeditionAreaLayout{id="exp-city",role="world",map=MapId,machines=machines,slots=NativeExpeditionHubPlan.LandingField(state.worldSeed).Select(NativeExpeditionHubPlan.RampFoot).ToArray(),
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
        private static string Digest(ExpeditionDeploymentTicket ticket,ExpeditionGeneratedWorld world)
        {
            var text=new StringBuilder(Version).Append('\n').Append(ticket.compatibility).Append('\n').Append(world.Blueprint.worldSeed.ToString(CultureInfo.InvariantCulture));
            // Exact generated collision/nav receipt, independent of current culture and JSON/Unity serialization.
            void F(float v)=>text.Append('|').Append(v.ToString("R",CultureInfo.InvariantCulture));
            foreach(var b in world.Map.blocks){text.Append('\n').Append(b.id).Append('|').Append(b.material);F(b.pos.x);F(b.pos.y);F(b.pos.z);F(b.size.x);F(b.size.y);F(b.size.z);F(b.rotation.y);F(b.rotation.w);}
            text.Append('|').Append(NativeCityLayout.Version).Append('|').Append(I(ticket.cityPlan));
            foreach(var ramp in world.Map.ramps){text.Append(ramp.id).Append('|').Append(ramp.kind);F(ramp.low.x);F(ramp.low.y);F(ramp.low.z);F(ramp.high.x);F(ramp.high.y);F(ramp.high.z);F(ramp.width);F(ramp.bottom);}
            foreach(var column in world.Map.pilotBlocks){
                text.Append(column.id);F(column.center.x);F(column.center.y);F(column.center.z);
                F(column.size.x);F(column.size.y);F(column.size.z);F(column.bottom);
            }
            foreach(var slot in world.HallSlots)text.Append('|').Append(slot);
            foreach(var route in world.City.Routes){
                text.Append(route.Name);
                foreach(var point in route.Points){F(point.x);F(point.y);F(point.z);}
                foreach(var width in route.Widths)F(width);
                foreach(var kind in route.Kinds)text.Append('|').Append(kind);
            }
            foreach(var n in world.Map.nav.nodes){text.Append('\n').Append(n.id);F(n.pos.x);F(n.pos.y);F(n.pos.z);text.Append('|').Append(I(n.layer));}
            foreach(var l in world.Map.nav.links)text.Append('\n').Append(l.id).Append('|').Append(l.a).Append('|').Append(l.b).Append('|').Append(l.kind);
            foreach(var d in world.Blueprint.districts)text.Append('\n').Append(d.instance).Append('|').Append(d.theme).Append('|').Append(d.side);
            void P(ExpeditionPoint p){F(p.x);F(p.y);F(p.z);F(p.yaw);}
            void V(ExpeditionVolume v){F(v.x);F(v.y);F(v.z);F(v.sx);F(v.sy);F(v.sz);F(v.yaw);}
            var a=world.Area;var hub=world.Hub;
            text.Append("\narea|").Append(a.id).Append('|').Append(a.map);
            foreach(var v in a.sanctuary)V(v);foreach(var p in a.slots)P(p);foreach(var p in a.bossPoints)P(p);V(a.bossTrigger);
            foreach(var anchor in a.anchors){text.Append("\nanchor|").Append(anchor.id).Append('|').Append(anchor.kind).Append('|').Append(anchor.building);F(anchor.x);F(anchor.y);F(anchor.z);F(anchor.yaw);}
            foreach(var machine in a.machines)
            {
                text.Append("\nmachine|").Append(machine.id).Append('|').Append(machine.kind);
                F(machine.x);F(machine.y);F(machine.z);F(machine.yaw);
                foreach(var point in new[]{machine.front,machine.dispense,machine.chipDrop}){F(point.x);F(point.y);F(point.z);}
            }
            foreach(var door in a.doors){text.Append("\ndoor|").Append(door.anchor);V(door.interact);V(door.closure);P(door.approach);F(door.join.x);F(door.join.y);F(door.join.z);F(door.join.r);F(door.join.h);foreach(var p in door.returns)P(p);}
            foreach(var g in a.gates){text.Append("\ngate|").Append(g.id).Append('|').Append(g.opens);V(g.closure);}
            foreach(var i in a.interactions){text.Append("\ninteract|").Append(i.anchor);F(i.x);F(i.y);F(i.z);F(i.range);}
            foreach(var spawn in a.spawns){var e=spawn.entry;text.Append("\nspawn|").Append(e.id).Append('|').Append(e.kind).Append('|').Append(I(e.e));F(e.from.x);F(e.from.y);F(e.from.z);F(e.@exit.x);F(e.@exit.y);F(e.@exit.z);F(e.clear);F(e.height);text.Append(e.surface).Append(I(e.level));foreach(var k in spawn.allow)text.Append('|').Append(k);}
            foreach(var g in hub.Gates){text.Append("\nhub-gate|").Append(g.Id);V(g.Closure);V(g.Interact);V(g.Sensor);P(g.Inside);P(g.Outside);}
            foreach(var z in hub.Zones){text.Append("\nzone|").Append(z.Id).Append('|').Append(z.Ring);V(z.Volume);F(z.Near);F(z.Far);foreach(int e in z.Spawns)text.Append('|').Append(I(e));}
            foreach(var r in hub.Routes){text.Append("\nroute|").Append(r.To).Append('|').Append(r.Gate);F(r.Path);P(r.Approach);foreach(var p in r.Via)P(p);}
            foreach(var c in world.Blueprint.cells){text.Append("\ncell|").Append(Key(c.x,c.z)).Append(c.road?'R':'B');foreach(int v in c.influences)text.Append('|').Append(I(v));}
            return NativeMapRules.Revision(text.ToString());
        }
    }
}
