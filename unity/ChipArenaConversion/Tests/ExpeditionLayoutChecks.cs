using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The Expedition layout drafts (docs/EXPEDITION-LAYOUT.md; written by docs/arenas/scripts/expedition_layout.py --fixtures):
    // Tests/expedition-city-draft.json, Tests/expedition-ward-zero-draft.json and Tests/expedition-layout-draft.json. Not game
    // content yet. Checks: both maps pass the UnityMaps validator and import beside today's maps through NativeMapRules.Merge
    // (a simulated import; Resources are not touched), stay out of the random rotation, and the instance keeps Siren's
    // blocks; the town's spawns follow the existing entry rules (NativeWardEntries) and Siren's entries validate against the
    // instance ("inherit:siren"); manifest ids pass ExpeditionValidation.Id, anchors keep the public kinds, records point at
    // anchors of their own area; NativeExpeditionLayout.Parse reads the manifest and its ValidateGeometry gate passes with
    // a block-based capsule probe (the town fully; the instance save Siren's two gantry breach exits, which are not Hulk-clear);
    // each area's blockHash is reproduced here from float fields; the expedition-city look validates and is the town's.
    // Static data checks only: no scene, PhysX or network claim.
    internal static class ExpeditionLayoutChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Expedition layout: "+what);count++;}
#pragma warning disable CS0649
        private sealed class Manifest{public int schema;public string revision,world;public string[] instances;public Area[] areas;}
        private sealed class Area{public string id,role,map,blockHash;public Anchor[] anchors;public Door[] doors;public Interaction[] interactions;public Sanctuary sanctuary;public Gate[] gates;public JsonElement spawns;public Boss boss;}
        private sealed class Anchor{public string id,kind,building;public float x,y,z,yaw;}
        private sealed class Vol{public float x,y,z,sx,sy,sz,yaw;}
        private sealed class Slot{public float x,y,z,yaw;}
        private sealed class Door{public string anchor;public Vol interact,closure;public Slot approach;public Slot[] returns,entries;}
        private sealed class Interaction{public string anchor;public float x,y,z,range;}
        private sealed class Sanctuary{public Vol[] volumes;public Slot[] slots;}
        private sealed class Gate{public string id,opens;public Vol closure;}
        private sealed class Boss{public Vol trigger;public Slot[] points;}
        private sealed class Allow{public string id;public string[] allow;}
#pragma warning restore CS0649
        /// <summary>A capsule probe over the map's authored collision (blocks minus collisionOff, plus ramp wedges), as the
        /// layout generators model it: floor under the point, nothing above the step or the capsule's spherical bottom.</summary>
        private static bool Supported(ContentMap m,V3 p,float r,float h)
        {
            var off=new HashSet<string>(m.navHints?.collisionOff??Array.Empty<string>());bool floor=false;
            double Surface(double d)=>p.y+r-Math.Sqrt(Math.Max(r*r-d*d,0))+.03;double step=p.y+.36,top=p.y+h+.05;
            foreach(var b in m.blocks)
            {
                if(off.Contains(b.id))continue;double yaw=2*Math.Atan2(b.rotation.y,b.rotation.w),c=Math.Cos(yaw),s=Math.Sin(yaw),dx=p.x-b.pos.x,dz=p.z-b.pos.z;
                double lx=dx*c-dz*s,lz=dx*s+dz*c,ex=Math.Max(Math.Abs(lx)-b.size.x/2,0),ez=Math.Max(Math.Abs(lz)-b.size.z/2,0),d=Math.Sqrt(ex*ex+ez*ez),y0=b.pos.y-b.size.y/2,y1=b.pos.y+b.size.y/2;
                if(d<=1e-6&&Math.Abs(y1-p.y)<=.05)floor=true;
                if(d<r&&y0<top&&y1>Math.Max(step,Surface(d)))return false;
            }
            double? Wedge(MapRamp w,double x,double z){double ax=w.high.x-w.low.x,az=w.high.z-w.low.z,L=Math.Sqrt(ax*ax+az*az),t=((x-w.low.x)*ax+(z-w.low.z)*az)/L,u=(-(x-w.low.x)*az+(z-w.low.z)*ax)/L;return t<0||t>L||Math.Abs(u)>w.width/2?(double?)null:w.low.y+(w.high.y-w.low.y)*t/L;}
            foreach(var w in m.ramps??Array.Empty<MapRamp>())
            {
                var at=Wedge(w,p.x,p.z);if(at.HasValue&&Math.Abs(at.Value-p.y)<=.05)floor=true;
                for(int k=0;k<8;k++)foreach(var f in new[]{.5,1.0}){double a=k*Math.PI/4;var hh=Wedge(w,p.x+Math.Sin(a)*r*f,p.z+Math.Cos(a)*r*f);if(hh.HasValue&&hh.Value>Math.Max(step,Surface(r*f)))return false;}
            }
            return floor;
        }
        private static string I(float v,double k)=>((long)Math.Round(v*k)).ToString(CultureInfo.InvariantCulture);
        /// <summary>The manifest's blockHash recipe (docs/EXPEDITION-LAYOUT.md "Block hash"), from the map's float fields.</summary>
        public static string BlockHash(ContentMap m)
        {
            var sb=new StringBuilder();
            foreach(var b in m.blocks)sb.Append(string.Join("|",new[]{"B",b.id,b.material,b.role,I(b.hp,1e4),I(b.pos.x,1e4),I(b.pos.y,1e4),I(b.pos.z,1e4),I(b.size.x,1e4),I(b.size.y,1e4),I(b.size.z,1e4),
                I(b.rotation.x,1e6),I(b.rotation.y,1e6),I(b.rotation.z,1e6),I(b.rotation.w,1e6)})).Append('\n');
            foreach(var r in m.ramps??Array.Empty<MapRamp>())sb.Append(string.Join("|",new[]{"R",r.id,r.kind,I(r.low.x,1e4),I(r.low.y,1e4),I(r.low.z,1e4),I(r.high.x,1e4),I(r.high.y,1e4),I(r.high.z,1e4),I(r.width,1e4),I(r.bottom,1e4),string.Join(",",r.blocks)})).Append('\n');
            foreach(var c in m.navHints?.collisionOff??Array.Empty<string>())sb.Append("C|").Append(c).Append('\n');
            var k=m.bounds;sb.Append(string.Join("|",new[]{"K",I(k.minX,1e4),I(k.maxX,1e4),I(k.minZ,1e4),I(k.maxZ,1e4),I(k.ceiling,1e4),I(k.killY,1e4)})).Append('\n');
            using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-","").ToLowerInvariant();
        }
        public static void Run(string root)
        {
            var o=new JsonSerializerOptions{IncludeFields=true};string R(string p)=>File.ReadAllText(Path.Combine(root,p));
            var town=JsonSerializer.Deserialize<UnityMapContent>(R("Tests/expedition-city-draft.json"),o).maps.Single();
            var inst=JsonSerializer.Deserialize<UnityMapContent>(R("Tests/expedition-ward-zero-draft.json"),o).maps.Single();
            var manifest=JsonSerializer.Deserialize<Manifest>(R("Tests/expedition-layout-draft.json"),o);
            string unityText=R("Assets/Conversion/Resources/UnityMaps.json");var unity=JsonSerializer.Deserialize<UnityMapContent>(unityText,o);
            var siren=unity.maps.Single(m=>m.id=="siren");var pve=JsonSerializer.Deserialize<NativePveContent>(R("Assets/Conversion/Resources/PveContent.json"),o);
            // The maps: the UnityMaps validator, then a simulated import beside today's maps (nothing is written).
            Check(town.id=="expedition-city"&&town.look=="expedition-city"&&inst.id=="expedition-ward-zero"&&inst.look=="quarantine","map ids and looks");
            Check(!town.rotation&&!inst.rotation,"neither draft joins the random rotation");
            NativeMapRules.Validate(town);NativeMapRules.Validate(inst);count+=2;
            var content=JsonSerializer.Deserialize<ConversionContent>(R("Assets/Conversion/Resources/GameContent.json"),o);int rotation=content.maps.Count(m=>m.rotation);
            NativeMapRules.Merge(content,new UnityMapContent{schema=1,materials=unity.materials,maps=unity.maps.Concat(new[]{town,inst}).ToArray()},unityText);
            Check(content.Map("expedition-city")!=null&&content.Map("expedition-ward-zero")!=null&&content.Map("siren").blocks.Length==siren.blocks.Length&&content.maps.Count(m=>m.rotation)==rotation,"the drafts import beside today's maps; Ward Zero and the rotation unchanged");
            Check(town.blocks.All(b=>b.id.StartsWith("expedition-city-",StringComparison.Ordinal)&&b.role=="static"&&new[]{"stone","brick","metal","wood","foundation"}.Contains(b.material)),"town blocks: own ids, static, existing materials");
            Check(town.blocks.Length>=100&&town.blocks.Length<=400&&town.pilotBlocks.Length>0,"town block budget and its exclusion columns");
            Check(inst.blocks.Take(siren.blocks.Length).Select(b=>b.id).SequenceEqual(siren.blocks.Select(b=>b.id))&&inst.blocks.Skip(siren.blocks.Length).All(b=>b.id.StartsWith("expedition-ward-zero-",StringComparison.Ordinal))&&inst.ramps.Length==siren.ramps.Length,"the instance is Siren's blocks and ramps plus its own overlay");
            Check(BlockHash(siren)==BlockHash(new ContentMap{blocks=inst.blocks.Take(siren.blocks.Length).ToArray(),ramps=inst.ramps,navHints=inst.navHints,bounds=inst.bounds}),"Siren's geometry copied unchanged");
            // The manifest.
            Check(manifest.schema==1&&manifest.revision.StartsWith("exp-layout-",StringComparison.Ordinal)&&manifest.world=="exp-city"&&manifest.instances.SequenceEqual(new[]{"exp-ward-zero"}),"schema 1, revision, world and instance list");
            var areas=manifest.areas.ToDictionary(a=>a.id);Check(areas.Count==2&&areas["exp-city"].role=="world"&&areas["exp-city"].map==town.id&&areas["exp-ward-zero"].role=="instance"&&areas["exp-ward-zero"].map==inst.id,"areas and their maps");
            Check(areas["exp-city"].blockHash==BlockHash(town)&&areas["exp-ward-zero"].blockHash==BlockHash(inst),"each area's blockHash reproduced from the map's float fields");
            var ids=new List<string>();
            foreach(var a in manifest.areas)
            {
                var anchors=a.anchors.ToDictionary(x=>x.id);ids.AddRange(a.anchors.Select(x=>x.id));
                foreach(var x in a.anchors)
                {
                    var w=new ExpeditionWorldAnchor{id=x.id,area=a.id,kind=x.kind,building=x.building,x=x.x,y=x.y,z=x.z,yaw=x.yaw};
                    Check(ExpeditionValidation.Id(w.id)&&w.id.StartsWith(a.id+":",StringComparison.Ordinal)&&new[]{"door","machine","chest"}.Contains(w.kind)&&(w.kind=="door")==!string.IsNullOrEmpty(w.building)&&w.yaw>=0&&w.yaw<360,"anchor "+x.id);
                }
                foreach(var d in a.doors)
                {
                    Check(anchors.TryGetValue(d.anchor,out var da)&&da.kind=="door","door record "+d.anchor);var slots=a.role=="world"?d.returns:d.entries;
                    Check(slots!=null&&slots.Length==8&&(a.role=="world"?d.entries==null:d.returns==null)&&new[]{d.interact,d.closure}.All(v=>v.sx>0&&v.sy>0&&v.sz>0&&v.yaw>=0&&v.yaw<360),"door volumes and 8 slots "+d.anchor);
                }
                foreach(var x in a.anchors.Where(x=>x.kind!="door"))Check(a.interactions.Count(i=>i.anchor==x.id)==1,"one interaction per machine and cache: "+x.id);
                Check(a.interactions.All(i=>anchors.ContainsKey(i.anchor)&&i.range>0),"interactions point at anchors of "+a.id);
                Check(a.boss?.points!=null&&a.boss.points.Length>0,"boss points in "+a.id);
            }
            var w0=areas["exp-city"];ids.AddRange(w0.gates.Select(g=>g.id));
            Check(w0.anchors.Count(x=>x.kind=="door"&&x.building=="exp-ward-zero"&&x.id=="exp-city:door:exp-ward-zero")==1&&areas["exp-ward-zero"].anchors.Count(x=>x.kind=="door"&&x.id=="exp-ward-zero:door:exit"&&x.building=="exp-ward-zero")==1,"one town door and one exit for the instance");
            Check(w0.sanctuary.volumes.Length==1&&w0.sanctuary.slots.Length==8&&w0.gates.Length==1&&w0.gates[0].id=="exp-city:gate:final"&&w0.gates[0].opens=="final"&&w0.boss.trigger!=null,"sanctuary, 8 slots, the final gate and the boss trigger");
            // Spawns: the town's own (the PveEntry shape plus allow) and Siren's for the instance.
            var spawns=JsonSerializer.Deserialize<PveEntry[]>(w0.spawns.GetRawText(),o);var allow=JsonSerializer.Deserialize<Allow[]>(w0.spawns.GetRawText(),o);ids.AddRange(spawns.Select(e=>e.id));
            var adds=new HashSet<string>(pve.adds.Select(x=>x.id));
            for(int i=0;i<spawns.Length;i++)
            {
                var e=spawns[i];Check(e.e==i&&ExpeditionValidation.Id(e.id)&&new[]{"vent","door","drop","breach"}.Contains(e.kind)&&e.level==0&&e.song==-1&&!e.pit&&MathUtil.Finite(e.yaw)&&e.clear>0&&NativeMapRules.Finite(e.from.Position())&&NativeMapRules.Finite(e.@exit.Position()),"spawn fields "+e.id);
                if(e.kind=="vent")Check(e.surface=="floor","vent surface "+e.id);
                if(NativeWardEntries.Drop(e))Check(e.height>0&&e.clear>=(e.hulk?4.5f:2.8f),"drop height and clear radius "+e.id);
                if(e.kind=="breach")Check(town.blocks.Any(b=>b.id==e.block&&b.role=="static"&&NativeMapRules.Box(e.center,e.size)&&Math.Abs(b.pos.x-e.center.x)<1e-3&&Math.Abs(b.pos.z-e.center.z)<1e-3)&&Math.Abs(e.normal.Length-1)<.01&&e.normal.y==0,"breach panel "+e.id);
                Check(allow[i].allow!=null&&allow[i].allow.Length>0&&allow[i].allow.All(k=>adds.Contains(k)&&NativeWardEntries.Allows(e,k)),"allow list within the entry rules "+e.id);
            }
            Check(areas["exp-ward-zero"].spawns.ValueKind==JsonValueKind.String&&areas["exp-ward-zero"].spawns.GetString()=="inherit:siren","the instance inherits Siren's entries");
            NativeWardEntries.Validate(pve,inst);count++;
            Check(ids.Distinct(StringComparer.Ordinal).Count()==ids.Count&&ids.All(ExpeditionValidation.Id),"manifest ids unique and valid");
            // Astra's own reader and geometry gate (Core NativeExpeditionLayout on this base), with the capsule probe above.
            string manifestText=R("Tests/expedition-layout-draft.json");var parsed=NativeExpeditionLayout.Parse(manifestText);
            Check(parsed.areas.Length==2&&parsed.Area("exp-city").spawns.Length==spawns.Length&&parsed.Area("exp-ward-zero").spawnSource=="inherit:siren"&&parsed.Area("exp-city").bossTrigger!=null&&parsed.Area("exp-city").slots.Length==8,"NativeExpeditionLayout.Parse reads the draft manifest");
            Check(parsed.Area("exp-city").slots.All(p=>parsed.Area("exp-city").sanctuary.Any(v=>v.Contains(p.Position))),"every sanctuary slot inside the sanctuary volume (ExpeditionVolume.Contains)");
            bool Probe(string id,V3 p,float r,float h)=>Supported(content.Map(id),p,r,h);
            NativeExpeditionLayout.ValidateGeometry(new ExpeditionLayout{schema=1,revision=parsed.revision,world=parsed.world,instances=parsed.instances,areas=new[]{parsed.Area("exp-city")}},content,pve,Probe);count++;
            // The instance inherits Siren's entries. Two of Siren's Hulk exits (the gantry breaches) sit 0.65 m from the gantry
            // rail, so the Hulk probe refuses them as authored; every other inherited exit and every authored record passes.
            var known=new[]{"breach-gantry-e","breach-gantry-w"};var instOnly=new ExpeditionLayout{schema=1,revision=parsed.revision,world=parsed.world,instances=parsed.instances,areas=new[]{parsed.Area("exp-ward-zero")}};
            bool refused=false;try{NativeExpeditionLayout.ValidateGeometry(instOnly,content,pve,Probe);}catch(FormatException){refused=true;}
            var tight=pve.entries.Where(e=>NativeWardEntries.Allows(e,"hulk")&&!Supported(inst,e.@exit.Position(),NativeHordeKinds.HulkRadius,NativeHordeKinds.HulkHeight)).Select(e=>e.id).OrderBy(x=>x).ToArray();
            Check(refused&&tight.SequenceEqual(known),"the gate refuses exactly Siren's two gantry breach exits for a Hulk: "+string.Join(",",tight));
            var rest=new NativePveContent{entries=pve.entries.Where(e=>!known.Contains(e.id)).ToArray()};NativeExpeditionLayout.ValidateGeometry(instOnly,content,rest,Probe);count++;
            // The look.
            var set=JsonSerializer.Deserialize<MapLookSet>(R("Assets/Conversion/Resources/MapLooks.json"),o);NativeMapLooks.Validate(set);
            var look=NativeMapLooks.ByLook(set,town.look);
            Check(look!=null&&look.id=="expedition-city"&&NativeMapLooks.Find(set,"expedition-city")==look&&look.sky.variant=="dusk"&&look.key.shadows!="none"&&look.lights.Length<=NativeMapLooks.MaxLights&&NativeBlockLooks.Dressed(look,false),"the expedition-city look: dusk, shadows, practical lights, dressed");
            Check(NativeMapLooks.ByLook(set,inst.look)?.id=="siren-quarantine","the instance shares the quarantine look");
            int listed=0;foreach(var b in town.blocks){var c=NativeBlockLooks.Resolve(look.blockLooks,null,b.id,b.material,0);if(c.Listed)listed++;}
            Check(listed==town.blocks.Length,"every town block has a look ("+listed+"/"+town.blocks.Length+")");
            Console.WriteLine("PASS expedition layout ("+count+" checks: both drafts validate and import beside today's maps outside the rotation; Siren's geometry kept; manifest ids, anchors, doors, interactions, sanctuary, gate, boss; NativeExpeditionLayout parses it and its geometry gate passes the town and the instance save Siren's two gantry breach exits; town spawns within the entry rules, Siren's entries valid on the instance; blockHash reproduced; the dusk look)");
        }
    }
}
