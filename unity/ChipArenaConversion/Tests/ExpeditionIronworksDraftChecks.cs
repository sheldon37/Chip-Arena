using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace ChipArena.UnityConversion
{
    // The City Ironworks blockout draft (docs/EXPEDITION-CITY-IRONWORKS.md; written by docs/arenas/scripts/expedition_ironworks.py
    // --fixtures): Tests/expedition-ironworks-draft.json (the map `expedition-foundry`, UnityMaps.json shape),
    // Tests/expedition-ironworks-layout-draft.json (the live exp-layout-3 manifest plus the town's foundry door and the exp-foundry
    // area) and Tests/expedition-ironworks-look-draft.json (a MapLooks.json-shape look). NOT LIVE: nothing under Assets/ names them,
    // the runtime loads Resources/ExpeditionLayout.json, UnityMaps.json and MapLooks.json, and none of those holds the foundry.
    // Run from ExpeditionLayoutChecks (its block capsule probe). Checks:
    //  - the live manifest is exp-layout-3's bytes (the join draft's pin) and still lists only Ward Zero; no live map or look is the
    //    foundry's;
    //  - the draft map passes the UnityMaps validator once it carries navigation built the importer's way (a 1 m grid of pilot
    //    probes on the authored collision, links sampled along each step, the map's drop hints), and imports beside today's maps
    //    through NativeMapRules.Merge, isolated from the arena lists and the rotation;
    //  - Astra's parser (NativeExpeditionLayout.Parse) and geometry gate (ValidateGeometry) accept the draft manifest with that
    //    probe and navigation: the town with its new foundry door, Ward Zero, and the foundry instance (entries, Hulk-clear slots,
    //    pilot and Hulk routes, every one of its own spawn exits routed back with its capsule);
    //  - the draft manifest is the live one plus exactly the instance id, the town's door anchor and record, the area and the
    //    revision; the area's blockHash is the map's (this recipe), its spawns keep the entry rules;
    //  - the look validates (alone and beside today's looks), names a block look for every block, and keeps eight lights at most.
    // Static data checks only: no scene, PhysX, network or director claim.
    internal static class ExpeditionIronworksDraftChecks
    {
        internal const string MapPath="Tests/expedition-ironworks-draft.json",LayoutPath="Tests/expedition-ironworks-layout-draft.json",LookPath="Tests/expedition-ironworks-look-draft.json";
        internal const string MapId="expedition-foundry",AreaId="exp-foundry";
        private static readonly string[] TextKinds={".cs",".json",".shader",".cginc",".hlsl",".asset",".unity",".prefab",".mat",".asmdef",".txt",".md"};
        private static int count;
#pragma warning disable CS0649
        private sealed class AllowList{public string id;public string[] allow;}
#pragma warning restore CS0649
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Expedition Ironworks draft: "+what);count++;}
        // ---- the importer's navigation (tools/generate_expedition.py navigation()), rebuilt on the block probe ----
        private static IEnumerable<float> Surfaces(ContentMap m,float x,float z)
        {
            var off=new HashSet<string>(m.navHints?.collisionOff??Array.Empty<string>());var tops=new SortedSet<float>();
            foreach(var b in m.blocks)
            {
                if(off.Contains(b.id)||b.size.y<=.02f)continue;double yaw=2*Math.Atan2(b.rotation.y,b.rotation.w),c=Math.Cos(yaw),s=Math.Sin(yaw),dx=x-b.pos.x,dz=z-b.pos.z;
                if(Math.Abs(dx*c-dz*s)<=b.size.x/2+1e-6&&Math.Abs(dx*s+dz*c)<=b.size.z/2+1e-6)tops.Add((float)Math.Round(b.pos.y+b.size.y/2,3));
            }
            foreach(var w in m.ramps??Array.Empty<MapRamp>())
            {
                double ax=w.high.x-w.low.x,az=w.high.z-w.low.z,L=Math.Sqrt(ax*ax+az*az),t=((x-w.low.x)*ax+(z-w.low.z)*az)/L,u=(-(x-w.low.x)*az+(z-w.low.z)*ax)/L;
                if(t>=0&&t<=L&&Math.Abs(u)<=w.width/2)tops.Add((float)Math.Round(w.low.y+(w.high.y-w.low.y)*t/L,3));
            }
            return tops;
        }
        internal static MapNav Navigation(ContentMap m,Func<ContentMap,V3,float,float,bool> supported,string prefix)
        {
            var nodes=new List<MapNavNode>();var cells=new Dictionary<(int,int),List<MapNavNode>>();var links=new List<MapNavLink>();
            for(int x=(int)Math.Ceiling(m.bounds.minX);x<(int)Math.Floor(m.bounds.maxX);x++)for(int z=(int)Math.Ceiling(m.bounds.minZ);z<(int)Math.Floor(m.bounds.maxZ);z++)
            {
                float px=x+.5f,pz=z+.5f;
                foreach(var y in Surfaces(m,px,pz))
                {
                    if(y<-.01f||y>m.bounds.ceiling-1||!supported(m,new V3(px,y,pz),.45f,1.8f))continue;
                    var n=new MapNavNode{id=prefix+"-n"+nodes.Count.ToString(CultureInfo.InvariantCulture),pos=new V3(px,y+.9f,pz),layer=y>=5.99f?1:0};nodes.Add(n);
                    if(!cells.TryGetValue((x,z),out var list))cells[(x,z)]=list=new List<MapNavNode>();list.Add(n);
                }
            }
            bool Supported(MapNavNode a,MapNavNode b)
            {
                if(Math.Abs(a.pos.y-b.pos.y)>.6f)return false;
                for(int i=0;i<9;i++)
                {
                    float t=i/8f,x=a.pos.x+(b.pos.x-a.pos.x)*t,y=a.pos.y+(b.pos.y-a.pos.y)*t-.9f,z=a.pos.z+(b.pos.z-a.pos.z)*t;bool any=false;
                    foreach(var top in Surfaces(m,x,z))if(Math.Abs(top-y)<.18f&&supported(m,new V3(x,top,z),.45f,1.8f)){any=true;break;}
                    if(!any)return false;
                }
                return true;
            }
            void Link(MapNavNode a,MapNavNode b,string kind,string id=null)=>links.Add(new MapNavLink{id=id??prefix+"-l"+links.Count.ToString(CultureInfo.InvariantCulture),a=a.id,b=b.id,kind=kind,requires=Array.Empty<string>(),after=Array.Empty<string>(),hazards=Array.Empty<string>()});
            foreach(var kv in cells.OrderBy(k=>k.Key.Item1).ThenBy(k=>k.Key.Item2))foreach(var a in kv.Value)foreach(var (dx,dz) in new[]{(1,0),(0,1)})
                if(cells.TryGetValue((kv.Key.Item1+dx,kv.Key.Item2+dz),out var next))foreach(var b in next)if(Supported(a,b))Link(a,b,Math.Abs(a.pos.y-b.pos.y)>.05f?"stair":"walk");
            MapNavNode Nearest(V3 p)
            {
                var n=nodes.Where(q=>Math.Abs(q.pos.y-.9f-p.y)<.6f).OrderBy(q=>(q.pos-new V3(0,.9f,0)-p).Length).FirstOrDefault();
                if(n==null||Math.Sqrt((n.pos.x-p.x)*(n.pos.x-p.x)+(n.pos.z-p.z)*(n.pos.z-p.z))>2)throw new Exception("Expedition Ironworks draft: drop hint off the navigation "+p.x+", "+p.y+", "+p.z);return n;
            }
            foreach(var d in m.navHints?.drops??Array.Empty<MapDrop>())Link(Nearest(d.a),Nearest(d.b),"drop",d.id);
            return new MapNav{nodes=nodes.ToArray(),links=links.ToArray()};
        }
        internal static void Run(string root,Func<ContentMap,V3,float,float,bool> supported)
        {
            count=0;var o=new JsonSerializerOptions{IncludeFields=true};string R(string p)=>File.ReadAllText(Path.Combine(root,p));
            // ---------------- the live files: no foundry anywhere ----------------
            string liveText=R(ExpeditionJoinDraftChecks.LivePath);string sha;using(var h=SHA256.Create())sha=BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(Path.Combine(root,ExpeditionJoinDraftChecks.LivePath)))).Replace("-","").ToLowerInvariant();
            Check(sha==ExpeditionJoinDraftChecks.LiveSha256,"the live manifest is exp-layout-3's bytes (sha256 "+sha+")");
            var live=NativeExpeditionLayout.Parse(liveText);Check(live.instances.SequenceEqual(new[]{"exp-ward-zero"})&&live.areas.All(a=>a.map!=MapId),"the live manifest lists Ward Zero only and no area uses "+MapId);
            string unityText=R("Assets/Conversion/Resources/UnityMaps.json");var unity=JsonSerializer.Deserialize<UnityMapContent>(unityText,o);
            var looksLive=JsonSerializer.Deserialize<MapLookSet>(R("Assets/Conversion/Resources/MapLooks.json"),o);
            Check(unity.maps.All(m=>m.id!=MapId&&m.look!="ironworks")&&looksLive.maps.All(l=>l.id!=MapId&&l.look!="ironworks"),"no live map or look is the foundry's");
            // ---------------- the map: the validator, then a simulated import beside today's maps ----------------
            var map=JsonSerializer.Deserialize<UnityMapContent>(R(MapPath),o).maps.Single();
            Check(map.id==MapId&&map.look=="ironworks"&&!map.rotation&&!NativeMapRules.ArenaSelectable(map)&&map.nav.nodes.Length==0,"the draft map is "+MapId+" (look ironworks), outside the rotation and the arena lists, with navigation left to the importer");
            Check(map.blocks.All(b=>b.id.StartsWith(MapId+"-",StringComparison.Ordinal)&&b.role=="static"&&new[]{"stone","brick","metal","wood","foundation"}.Contains(b.material))&&map.blocks.Length>=200&&map.blocks.Length<=700,"blocks: own ids, static, existing materials, 200-700 ("+map.blocks.Length+")");
            Check(map.hazards.Length==0&&map.ramps.Length==5&&map.ramps.All(r=>r.kind=="stair")&&map.pilotBlocks.Length>0,"no stage hazards (they are proposals), five stairs, exclusion columns");
            Check(ExpeditionLayoutChecks.BlockHash(map)==JsonNode.Parse(R(LayoutPath))["areas"].AsArray().Single(a=>(string)a["id"]==AreaId)["blockHash"].GetValue<string>(),"the area's blockHash is the map's (the C# recipe)");
            var t0=DateTime.UtcNow;map.nav=Navigation(map,supported,"ef");int navMs=(int)(DateTime.UtcNow-t0).TotalMilliseconds;
            Check(map.nav.nodes.Length>3000&&map.nav.links.Length>map.nav.nodes.Length&&map.nav.nodes.Any(n=>n.layer==1)&&map.nav.links.Count(l=>l.kind=="drop")==map.navHints.drops.Length,"importer-style navigation: "+map.nav.nodes.Length+" nodes, "+map.nav.links.Length+" links, both levels, every drop hint linked");
            NativeMapRules.Validate(map);count++;
            var content=JsonSerializer.Deserialize<ConversionContent>(R("Assets/Conversion/Resources/GameContent.json"),o);int rotation=content.maps.Count(m=>m.rotation);
            NativeMapRules.Merge(content,new UnityMapContent{schema=1,materials=unity.materials,maps=unity.maps.Concat(new[]{map}).ToArray()},unityText);
            Check(content.Map(MapId)==map&&content.maps.Count(m=>m.rotation)==rotation&&content.Map("expedition-city").nav.nodes.Length>0&&content.Map("siren").blocks.Length==unity.maps.Single(m=>m.id=="siren").blocks.Length,"the draft imports beside today's maps (Ward Zero, the town and the rotation unchanged)");
            var normalized=new NativeRoomOptions{map=MapId};normalized.Normalize(content);Check(normalized.map!=MapId,"room normalization refuses the foundry like every Expedition map");
            // ---------------- Astra's parser and geometry gate on the draft manifest ----------------
            string draftText=R(LayoutPath);var draft=NativeExpeditionLayout.Parse(draftText);
            var pve=JsonSerializer.Deserialize<NativePveContent>(R("Assets/Conversion/Resources/PveContent.json"),o);
            Check(draft.revision=="exp-layout-3-ironworks-draft"&&draft.instances.SequenceEqual(new[]{"exp-ward-zero",AreaId})&&draft.areas.Length==3,"the draft manifest: exp-layout-3-ironworks-draft, instances Ward Zero and the foundry, three areas");
            var f=draft.Area(AreaId);var town=draft.Area(draft.world);var door=f.doors.Single();var tdoor=town.doors.Single(d=>town.anchors.Single(a=>a.id==d.anchor).building==AreaId);
            Check(f.role=="instance"&&f.map==MapId&&f.spawnSource==null&&f.spawns.Length>=36&&f.bossPoints.Length==3&&door.entries.Length==8&&door.returns.Length==0&&f.anchors.Count(a=>a.kind=="chest")==3&&f.interactions.Length==3,"the foundry area: its own "+f.spawns.Length+" spawns, 3 boss points, 8 entries, 3 caches with interactions");
            Check(tdoor.returns.Length==8&&tdoor.join==null&&town.anchors.Single(a=>a.id==tdoor.anchor).x==-83.4f&&tdoor.anchor=="exp-city:door:exp-foundry","the town's foundry door at the reserved doorway, eight returns, no join (exp-layout-4's join stays a separate draft)");
            var searches=new Dictionary<string,NativeNavSearch>();var nodeClear=new Dictionary<string,bool>();var edgeClear=new Dictionary<string,bool>();
            bool Probe(string id,V3 p,float r,float h)=>supported(content.Map(id),p,r,h);
            bool Reach(string id,V3 from,V3 to,float radius,float height)
            {
                var m=content.Map(id);if(!searches.TryGetValue(id,out var search))searches[id]=search=new NativeNavSearch(m.nav);
                string prefix=id+":"+radius.ToString(CultureInfo.InvariantCulture)+":";
                V3 Floor(MapNavNode n)=>n.pos-new V3(0,.9f,0);
                bool Clear(MapNavNode n){string key=prefix+n.id;if(!nodeClear.TryGetValue(key,out bool ok))nodeClear[key]=ok=supported(m,Floor(n),radius,height);return ok;}
                bool Near(MapNavNode n,V3 p)=>Math.Abs(Floor(n).y-p.y)<.6f&&(Floor(n)-p).Length<1.5f&&Clear(n);
                var a=search.Nearest(from+new V3(0,.9f,0),n=>Near(n,from));var b=search.Nearest(to+new V3(0,.9f,0),n=>Near(n,to));if(a==null||b==null)return false;
                var index=m.nav.nodes.ToDictionary(n=>n.id);
                bool Active(MapNavLink e)
                {
                    string key=prefix+e.id;if(edgeClear.TryGetValue(key,out bool ok))return ok;
                    var p=index[e.a];var q=index[e.b];ok=Clear(p)&&Clear(q);
                    if(e.kind!="drop")for(int k=1;ok&&k<8;k++)ok=supported(m,Floor(p)+(Floor(q)-Floor(p))*(k/8f),radius,height);
                    edgeClear[key]=ok;return ok;
                }
                var path=new List<string>();search.Find(a.id,b.id,Active,path);return path.Count>0;
            }
            NativeExpeditionLayout.ValidateGeometry(draft,content,pve,Probe,Reach);count++;
            // The live manifest still passes the same gate with this content (the foundry map's presence changes nothing for it).
            NativeExpeditionLayout.ValidateGeometry(live,content,pve,Probe,Reach);count++;
            // Exactly the instance id, the town's door anchor and record, the area and the revision differ from the live manifest.
            var dn=JsonNode.Parse(draftText);var ln=JsonNode.Parse(liveText);
            var ta=dn["areas"][0]["anchors"].AsArray();var td=dn["areas"][0]["doors"].AsArray();
            ta.Remove(ta.Single(a=>(string)a["id"]=="exp-city:door:exp-foundry"));td.Remove(td.Single(d=>(string)d["anchor"]=="exp-city:door:exp-foundry"));
            var areas=dn["areas"].AsArray();areas.Remove(areas.Single(a=>(string)a["id"]==AreaId));var inst=dn["instances"].AsArray();inst.Remove(inst.Single(i=>(string)i==AreaId));dn["revision"]=ln["revision"].DeepClone();
            Check(JsonNode.DeepEquals(dn,ln),"the draft manifest is the live one plus the instance id, the town's foundry door (anchor and record), the area and the revision (Ward Zero, the hub record and the rest unchanged)");
            // Spawns within the entry rules; breach panels on static blocks of the map; the additive record ignored by the parser.
            var adds=new HashSet<string>(pve.adds.Select(x=>x.id));
            // The rules read the authored records (the PveEntry shape plus allow), as ExpeditionLayoutChecks reads the town's.
            var areaNode=JsonNode.Parse(draftText)["areas"].AsArray().Single(a=>(string)a["id"]==AreaId);string spawnJson=areaNode["spawns"].ToJsonString();
            var raw=JsonSerializer.Deserialize<PveEntry[]>(spawnJson,o);var allow=JsonSerializer.Deserialize<AllowList[]>(spawnJson,o);
            Check(raw.Length==f.spawns.Length&&raw.Select(e=>e.id).SequenceEqual(f.spawns.Select(s=>s.entry.id))&&raw.Select((e,i)=>e.e==i).All(x=>x),"the parsed spawns are the authored ones, e 0.."+(raw.Length-1));
            for(int i=0;i<raw.Length;i++)
            {
                var e=raw[i];
                Check(allow[i].allow.All(k=>adds.Contains(k)&&NativeWardEntries.Allows(e,k))&&(e.kind!="breach"||map.blocks.Any(b=>b.id==e.block&&b.role=="static"&&Math.Abs(b.pos.x-e.center.x)<1e-3&&Math.Abs(b.pos.z-e.center.z)<1e-3)&&Math.Abs(e.normal.Length-1)<.01f)
                    &&(!NativeWardEntries.Drop(e)||e.height>0&&e.clear>=(e.hulk?4.5f:e.kind=="vent"?1.6f:2.8f))&&(e.kind!="vent"||e.surface=="floor"||e.surface=="ceiling")&&e.song==-1&&!e.pit,"spawn "+e.id+" within the entry rules");
            }
            // Astra's parser keeps e, id, kind, from, exit, yaw, level, clear, height and surface; NativeExpeditionM1Arena hands those
            // parsed entries to the horde. A manifest-authored Hulk drop or breach therefore loses `hulk` and its panel (block, center,
            // size, normal) at load: reported, not failed (the live town's 4 Hulk drops and 7 breaches have the same gap).
            int lost=raw.Count(e=>e.hulk||e.kind=="breach");int kept=f.spawns.Count(s=>s.entry.hulk||s.entry.kind=="breach"&&!string.IsNullOrEmpty(s.entry.block));
            Console.WriteLine("NOTE expedition ironworks draft: "+lost+" Hulk drops and breaches authored; "+kept+" keep their hulk flag or panel through NativeExpeditionLayout.Parse (the parser's spawn reader drops hulk, block, center, size and normal)");
            var rec=areaNode["ironworks"];
            Check(rec!=null&&rec["kinds"].AsArray().Count==4&&rec["rooms"].AsArray().Count==9&&rec["hazards"].AsArray().All(h=>((string)h["id"]).StartsWith(MapId+"-",StringComparison.Ordinal)&&(string)h["status"]=="proposal")&&rec["spawns"].AsArray().Count==f.spawns.Length,"the additive ironworks record: 9 rooms, proposed hazards (map-prefixed ids), 4 proposed kinds, a proposal per spawn");
            // ---------------- the look ----------------
            var lookSet=JsonSerializer.Deserialize<MapLookSet>(R(LookPath),o);NativeMapLooks.Validate(lookSet);count++;
            var look=lookSet.maps.Single();NativeMapLooks.Validate(new MapLookSet{schema=looksLive.schema,maps=looksLive.maps.Append(look).ToArray(),preview=looksLive.preview});count++;
            Check(look.id==MapId&&look.look=="ironworks"&&look.sky.variant=="none"&&look.lights.Length<=NativeMapLooks.MaxLights&&look.blocks.dressed&&look.key.shadows=="none","the look: a sealed interior (no sky, no shadowing key under the roof, as Ward Zero's), at most eight practical lights, dressed blocks");
            int listed=0;foreach(var b in map.blocks){var c=NativeBlockLooks.Resolve(look.blockLooks,null,b.id,b.material,0);if(c.Listed)listed++;}
            Check(listed==map.blocks.Length,"every block has a look ("+listed+"/"+map.blocks.Length+")");
            // ---------------- not referenced: the runtime and the player build never see the drafts ----------------
            var hits=Directory.EnumerateFiles(Path.Combine(root,"Assets"),"*",SearchOption.AllDirectories)
                .Where(p=>TextKinds.Contains(Path.GetExtension(p))&&!p.Replace('\\','/').Contains("/LocalAssets/"))
                .Where(p=>{var t=File.ReadAllText(p);return t.Contains("ironworks-draft",StringComparison.Ordinal)||t.Contains("ironworks-layout-draft",StringComparison.Ordinal)||t.Contains("ironworks-look-draft",StringComparison.Ordinal)||t.Contains("exp-layout-3-ironworks-draft",StringComparison.Ordinal);}).ToArray();
            Check(hits.Length==0,"nothing under Assets/ names the drafts ("+string.Join(", ",hits.Select(Path.GetFileName))+")");
            Check(!R("tools/generate_expedition.py").Contains("ironworks"),"the importer imports the live fixtures only");
            Console.WriteLine("PASS expedition ironworks draft ("+count+" checks: "+MapPath+", "+LayoutPath+" and "+LookPath+" are not live and named nowhere under Assets/; the live exp-layout-3 manifest byte-identical and still accepted; "
                +"the map validates and imports beside today's maps with importer-style navigation ("+map.nav.nodes.Length+" nodes, "+map.nav.links.Length+" links, "+navMs+" ms); Astra's parser and geometry gate accept the draft manifest: "
                +"the town's foundry door, Ward Zero and the foundry's "+f.spawns.Length+" spawns, entries, caches and boss points; the draft is the live manifest plus the foundry only; the look validates and covers all "+map.blocks.Length+" blocks)");
        }
    }
}
