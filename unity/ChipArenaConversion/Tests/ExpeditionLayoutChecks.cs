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
    // anchors of their own area; each area's blockHash is reproduced here from float fields (the recipe Astra's loader uses);
    // the expedition-city look validates and is the town's. Static data checks only: no scene, PhysX or network claim.
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
            // The look.
            var set=JsonSerializer.Deserialize<MapLookSet>(R("Assets/Conversion/Resources/MapLooks.json"),o);NativeMapLooks.Validate(set);
            var look=NativeMapLooks.ByLook(set,town.look);
            Check(look!=null&&look.id=="expedition-city"&&NativeMapLooks.Find(set,"expedition-city")==look&&look.sky.variant=="dusk"&&look.key.shadows!="none"&&look.lights.Length<=NativeMapLooks.MaxLights&&NativeBlockLooks.Dressed(look,false),"the expedition-city look: dusk, shadows, practical lights, dressed");
            Check(NativeMapLooks.ByLook(set,inst.look)?.id=="siren-quarantine","the instance shares the quarantine look");
            int listed=0;foreach(var b in town.blocks){var c=NativeBlockLooks.Resolve(look.blockLooks,null,b.id,b.material,0);if(c.Listed)listed++;}
            Check(listed==town.blocks.Length,"every town block has a look ("+listed+"/"+town.blocks.Length+")");
            Console.WriteLine("PASS expedition layout ("+count+" checks: both drafts validate and import beside today's maps outside the rotation; Siren's geometry kept; manifest ids, anchors, doors, interactions, sanctuary, gate, boss; town spawns within the entry rules, Siren's entries valid on the instance; blockHash reproduced; the dusk look)");
        }
    }
}
