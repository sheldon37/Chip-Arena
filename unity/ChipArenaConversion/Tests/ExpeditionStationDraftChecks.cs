using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace ChipArena.UnityConversion
{
    // The Central Station design draft (docs/EXPEDITION-CENTRAL-STATION.md), written by
    // docs/arenas/scripts/expedition_central_station.py --fixture to Tests/expedition-central-station-draft.json. NOT LIVE: the
    // game loads Resources/ExpeditionLayout.json (exp-layout-3), UnityMaps.json and MapLooks.json, none of which holds the
    // station. Since PR #254 the runtime knows the map id (NativeMapRules.ExpeditionInstance) and the importer has its
    // navigation profile (et, 1 m), but the importer only builds maps the historical baseline manifest names. Run from ExpeditionLayoutChecks.
    // Checks:
    //  - the historical baseline manifest is still exp-layout-3's bytes (the join draft's pin) and no live resource has the station's map,
    //    area or look;
    //  - the draft map passes the UnityMaps validator and imports beside today's maps (a simulated NativeMapRules.Merge; nothing
    //    is written), outside the random rotation and the arena lists; its blockHash is reproduced from the float fields;
    //  - Astra's parser and geometry gate (NativeExpeditionLayout.Parse, ValidateGeometry) accept the historical baseline manifest plus the
    //    station: the instance in `instances`, its area, and the hub door record at the reserved doorway. The station has no
    //    imported navigation yet, so its route evidence is a capsule flood over its authored collision (this file's grid, the
    //    same probe as the layout checks); the town keeps its imported navigation;
    //  - the spawns use existing kinds within the entry rules (the station enemies are named only in `station`, never in
    //    `allow`, and are not horde kinds yet); the parser (#254) keeps every authored spawn field, breach panels included;
    //  - NativeExpeditionLayout.BindContent (#254) gives a detached content copy the station's own entry table, boss point,
    //    pads and hide spots, and no Ward machines;
    //  - the draft look validates beside today's looks and lists every station block.
    // Static data checks only: no scene, PhysX, director or network claim.
    internal static class ExpeditionStationDraftChecks
    {
        internal const string DraftPath="Tests/expedition-central-station-draft.json";
        internal static readonly string[] StationEnemies={"linesman","conductor","undertow","pileup"};
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Expedition station draft: "+what);count++;}
        /// <summary>A capsule flood over a map's authored collision on a 0.5 m grid: the standable surfaces of every column
        /// (block tops and ramp wedges) where the layout checks' probe fits, joined to the four neighbours within the step.</summary>
        private sealed class Grid
        {
            private readonly Dictionary<(int,int),List<(float y,int c)>> cells=new Dictionary<(int,int),List<(float,int)>>();
            private readonly MapBounds b;public int Nodes,Components;public readonly Dictionary<int,int> Size=new Dictionary<int,int>();
            public Grid(ContentMap m,float r,float h)
            {
                b=m.bounds;var off=new HashSet<string>(m.navHints?.collisionOff??Array.Empty<string>());var blocks=m.blocks.Where(x=>!off.Contains(x.id)).ToArray();
                int nx=(int)Math.Round((b.maxX-b.minX)/.5),nz=(int)Math.Round((b.maxZ-b.minZ)/.5);
                for(int i=0;i<nx;i++)for(int j=0;j<nz;j++)
                {
                    float x=b.minX+(i+.5f)*.5f,z=b.minZ+(j+.5f)*.5f;var tops=new SortedSet<float>();
                    foreach(var k in blocks)
                    {
                        double yaw=2*Math.Atan2(k.rotation.y,k.rotation.w),c=Math.Cos(yaw),s=Math.Sin(yaw),dx=x-k.pos.x,dz=z-k.pos.z;
                        if(Math.Abs(dx*c-dz*s)<=k.size.x/2&&Math.Abs(dx*s+dz*c)<=k.size.z/2&&k.size.y>.02f)tops.Add((float)Math.Round(k.pos.y+k.size.y/2,3));
                    }
                    foreach(var w in m.ramps??Array.Empty<MapRamp>())
                    {
                        double ax=w.high.x-w.low.x,az=w.high.z-w.low.z,L=Math.Sqrt(ax*ax+az*az),t=((x-w.low.x)*ax+(z-w.low.z)*az)/L,u=(-(x-w.low.x)*az+(z-w.low.z)*ax)/L;
                        if(t>=0&&t<=L&&Math.Abs(u)<=w.width/2)tops.Add((float)Math.Round(w.low.y+(w.high.y-w.low.y)*t/L,3));
                    }
                    foreach(float y in tops)if(y>=b.killY&&y<=b.ceiling-1&&ExpeditionLayoutChecks.Supported(m,new V3(x,y,z),r,h)){if(!cells.TryGetValue((i,j),out var l))cells[(i,j)]=l=new List<(float,int)>();l.Add((y,-1));Nodes++;}
                }
                foreach(var key in cells.Keys.ToArray())for(int n=0;n<cells[key].Count;n++)
                {
                    if(cells[key][n].c>=0)continue;int id=Components++,size=0;var stack=new Stack<((int,int),int)>();Set(key,n,id);stack.Push((key,n));
                    while(stack.Count>0)
                    {
                        var (k,a)=stack.Pop();size++;float y=cells[k].ElementAt(a).y;
                        foreach(var (di,dj) in new[]{(1,0),(-1,0),(0,1),(0,-1)})
                        {
                            var nk=(k.Item1+di,k.Item2+dj);if(!cells.TryGetValue(nk,out var l))continue;
                            for(int q=0;q<l.Count;q++)if(l[q].c<0&&Math.Abs(l[q].y-y)<=.351f){Set(nk,q,id);stack.Push((nk,q));}
                        }
                    }
                    Size[id]=size;
                }
            }
            private void Set((int,int) k,int n,int id){var l=cells[k];l[n]=(l[n].y,id);}
            /// <summary>Nodes at a floor height, and how many of them lie in component `c`.</summary>
            public (int total,int inside) Floor(float y,int c){int t=0,n=0;foreach(var l in cells.Values)foreach(var (h,id) in l)if(Math.Abs(h-y)<.05f){t++;if(id==c)n++;}return (t,n);}
            /// <summary>The component of the node nearest a floor point (within .6 m across and .3 m up or down), or -1.</summary>
            public int At(V3 p)
            {
                int ci=(int)((p.x-b.minX)/.5f),cj=(int)((p.z-b.minZ)/.5f);double best=1e9;int c=-1;
                for(int di=-2;di<=2;di++)for(int dj=-2;dj<=2;dj++)
                {
                    if(!cells.TryGetValue((ci+di,cj+dj),out var l))continue;double x=b.minX+(ci+di+.5)*.5,z=b.minZ+(cj+dj+.5)*.5,d=Math.Sqrt((x-p.x)*(x-p.x)+(z-p.z)*(z-p.z));
                    foreach(var (y,id) in l)if(Math.Abs(y-p.y)<=.3&&d<=.6&&d<best){best=d;c=id;}
                }
                return c;
            }
        }
        internal static void Run(string root,Func<string,V3,V3,float,float,bool> townReach)
        {
            count=0;var o=new JsonSerializerOptions{IncludeFields=true};string R(string p)=>File.ReadAllText(Path.Combine(root,p));
            string draftText=R(DraftPath),liveText=R(ExpeditionJoinDraftChecks.LivePath),unityText=R("Assets/Conversion/Resources/UnityMaps.json"),looksText=R("Assets/Conversion/Resources/MapLooks.json");
            var draft=JsonNode.Parse(draftText).AsObject();
            // ---------------- not live ----------------
            var live=NativeExpeditionLayout.Parse(liveText);
            Check(live.revision=="exp-layout-3"&&live.Area("exp-transit")==null&&!live.instances.Contains("exp-transit")&&live.Area("exp-city").doors.All(d=>d.anchor!="exp-city:door:exp-transit"),"the historical baseline manifest is exp-layout-3 without the station (its bytes are pinned by the join draft check)");
            var unity=JsonSerializer.Deserialize<UnityMapContent>(unityText,o);var set=JsonSerializer.Deserialize<MapLookSet>(looksText,o);
            Check(unity.maps.Any(m=>m.id=="expedition-transit")&&set.maps.Any(m=>m.look=="central-station"),"activation snapshot exists beside the separately tested design draft");
            set.maps=set.maps.Where(m=>m.id!="expedition-transit").ToArray();
            Check(draft["draft"]?.GetValue<bool>()==true&&(string)draft["revision"]=="exp-transit-draft-1","the file is marked a draft");
            // ---------------- the map: validated, imported beside today's maps in memory ----------------
            var station=JsonSerializer.Deserialize<UnityMapContent>(draftText,o).maps.Single();
            Check(station.id=="expedition-transit"&&station.look=="central-station"&&!station.rotation&&!NativeMapRules.ArenaSelectable(station),"map id, look, not in the rotation, outside the arena lists");
            NativeMapRules.Validate(station);count++;
            Check(station.blocks.All(b=>b.id.StartsWith("expedition-transit-",StringComparison.Ordinal)&&b.role=="static")&&station.nav.nodes.Length==0&&station.ramps.All(r=>r.blocks.All(id=>station.navHints.collisionOff.Contains(id))),"own block ids, static, no navigation yet, every stair's steps visual only");
            var maps=JsonSerializer.Deserialize<UnityMapContent>(unityText,o);var content=JsonSerializer.Deserialize<ConversionContent>(R("Assets/Conversion/Resources/GameContent.json"),o);int rotation=content.maps.Count(m=>m.rotation);
            NativeMapRules.Merge(content,new UnityMapContent{schema=1,materials=maps.materials,maps=maps.maps.Where(m=>m.id!="expedition-transit").Concat(new[]{station}).ToArray()},unityText);
            Check(content.Map("expedition-transit")!=null&&content.maps.Count(m=>m.rotation)==rotation,"the draft imports beside today's maps; the rotation unchanged");
            var areaNode=draft["layout"]["area"].AsObject();
            Check((string)areaNode["blockHash"]==ExpeditionLayoutChecks.BlockHash(station),"the area's blockHash reproduced from the map's float fields");
            // ---------------- Astra's parser and geometry gate on the historical baseline manifest plus the station ----------------
            var composed=JsonNode.Parse(liveText).AsObject();composed["revision"]=(string)draft["revision"];composed["instances"].AsArray().Add("exp-transit");
            var town=composed["areas"][0].AsObject();Check((string)town["id"]=="exp-city","the town is the manifest's first area");
            town["anchors"].AsArray().Add(draft["layout"]["town"]["anchor"].DeepClone());town["doors"].AsArray().Add(draft["layout"]["town"]["door"].DeepClone());
            composed["areas"].AsArray().Add(areaNode.DeepClone());
            var layout=NativeExpeditionLayout.Parse(composed.ToJsonString());var area=layout.Area("exp-transit");
            Check(area!=null&&area.role=="instance"&&area.map=="expedition-transit"&&area.spawnSource==null&&area.doors.Single().entries.Length==8&&area.bossPoints.Length==3,"Parse reads the station: an instance on its own map, its own spawns, one exit with 8 entries, 3 boss points");
            var hub=layout.Area("exp-city").doors.Single(d=>d.anchor=="exp-city:door:exp-transit");var res=ExpeditionHubLayout.Parse(liveText,"exp-city").Routes.Single(r=>r.To=="exp-transit");
            Check(hub.returns.Length==8&&hub.join==null&&Math.Abs(res.X)<1e-4&&Math.Abs(res.Z+84)<1e-4&&layout.Area("exp-city").anchors.Single(a=>a.id==hub.anchor).building=="exp-transit","the hub door at the reserved doorway (0, -84) with 8 returns, building exp-transit");
            var grids=new Dictionary<float,Grid>();Grid G(float r,float h){if(!grids.TryGetValue(r,out var g))grids[r]=g=new Grid(content.Map("expedition-transit"),r,h);return g;}
            bool Probe(string id,V3 p,float r,float h)=>ExpeditionLayoutChecks.Supported(content.Map(id),p,r,h);
            bool Reach(string id,V3 a,V3 b,float r,float h){if(id!="expedition-transit")return townReach(id,a,b,r,h);var g=G(r,h);int ca=g.At(a),cb=g.At(b);return ca>=0&&ca==cb;}
            var pve=JsonSerializer.Deserialize<NativePveContent>(R("Assets/Conversion/Resources/PveContent.json"),o);
            NativeExpeditionLayout.ValidateGeometry(layout,content,pve,Probe,Reach);count++;
            var pilot=G(.45f,1.8f);var hulk=G(NativeHordeKinds.HulkRadius,NativeHordeKinds.HulkHeight);
            int origin=pilot.At(area.doors[0].entries[0].Position);
            var floors=new[]{0f,6f,-1.2f}.Select(y=>pilot.Floor(y,origin)).ToArray();
            Check(origin>=0&&floors.All(f=>f.total>0&&f.inside==f.total),"every walking-floor cell (ground y 0, upper y 6, track bed y -1.2) joined to the entries: "+string.Join(", ",floors.Select(f=>f.inside+"/"+f.total)));
            // ---------------- spawns: existing kinds within the entry rules; the station enemies only proposed ----------------
            var spawns=area.spawns;var raw=areaNode["spawns"].AsArray();
            Check(spawns.Length==raw.Count&&spawns.Select(s=>s.entry.e).SequenceEqual(Enumerable.Range(0,spawns.Length)),"spawns indexed 0.."+(spawns.Length-1));
            // The parser (#254) keeps every authored field: each parsed entry equals a direct deserialize of its record, so the
            // breach panels (block, centre, size, normal), the Hulk drop flags and the drop heights reach the director.
            var entries=JsonSerializer.Deserialize<PveEntry[]>(raw.ToJsonString(),o);var fieldsOnly=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
            for(int i=0;i<spawns.Length;i++)
            {
                var e=spawns[i].entry;
                Check(JsonSerializer.Serialize(entries[i],fieldsOnly)==JsonSerializer.Serialize(e,fieldsOnly)&&raw[i]["allow"].AsArray().Select(k=>(string)k).SequenceEqual(spawns[i].allow),"every spawn field and allow list survives the parser: "+e.id);
                Check(spawns[i].allow.All(k=>NativeHordeKinds.Known(k)&&NativeWardEntries.Allows(e,k))&&(!e.hulk||e.kind=="drop")
                    &&(e.kind!="breach"||station.blocks.Any(b=>b.id==e.block&&b.role=="static"&&Math.Abs(b.pos.x-e.center.x)<1e-3&&Math.Abs(b.pos.z-e.center.z)<1e-3)&&Math.Abs(e.normal.Length-1)<.01),"allow list within the entry rules: "+e.id);
            }
            var proposed=raw.Where(x=>x["station"]!=null).SelectMany(x=>x["station"].AsArray().Select(y=>(string)y)).Distinct().ToArray();
            Check(proposed.Length>0&&proposed.All(k=>StationEnemies.Contains(k)&&!NativeHordeKinds.Known(k))&&StationEnemies.All(k=>!NativeHordeKinds.Known(k)),"the station enemies ("+string.Join(", ",proposed)+") are proposals: named only beside the allow lists, not horde kinds");
            Check(spawns.Count(s=>s.allow.Contains("hulk"))==5&&spawns.Count(s=>s.entry.level==1)==4,"5 Hulk entrances, 4 on the upper floor");
            // ---------------- authored content binding (#254), on a detached copy as the arena does ----------------
            var bound=JsonSerializer.Deserialize<NativePveContent>(R("Assets/Conversion/Resources/PveContent.json"),o);int wardEntries=pve.entries.Length,wardMachines=pve.machines.Length;
            NativeExpeditionLayout.BindContent(area,bound);var boss0=area.bossPoints[0];
            Check(bound.entries.Length==spawns.Length&&bound.entries.Select(x=>x.id).SequenceEqual(spawns.Select(x=>x.entry.id))&&bound.entries.Count(x=>x.kind=="breach")==3&&bound.entries.Count(x=>x.hulk)==2,
                "BindContent: the station's own entry table (index order, 3 breaches, 2 Hulk drops)");
            Check(Math.Abs(bound.boss.spawn.x-boss0.x)<1e-4&&Math.Abs(bound.boss.spawn.z-boss0.z)<1e-4&&bound.hideSpots.Length==3&&bound.pads.Length==8&&bound.machines.Length==0&&bound.machineBlocks.Length==0,
                "BindContent: boss at boss point 0, 3 hide spots, 8 pads from the exit's entries, no Ward machines (the station's need authoring)");
            Check(pve.entries.Length==wardEntries&&pve.machines.Length==wardMachines&&NativeMapRules.ExpeditionInstance(station.id)&&!NativeMapRules.WardInstance(station.id),
                "binding leaves Ward content unchanged; the map id is a known Expedition instance, not a Ward one");
            // ---------------- the look ----------------
            var look=JsonSerializer.Deserialize<MapLook>(draft["look"].ToJsonString(),o);var all=new MapLookSet{schema=set.schema,maps=set.maps.Concat(new[]{look}).ToArray(),preview=set.preview};
            NativeMapLooks.Validate(all);count++;
            Check(NativeMapLooks.ByLook(all,station.look)==look&&look.lights.Length==NativeMapLooks.MaxLights&&look.sky.variant=="night"&&look.key.shadows!="none","the draft look validates beside today's: night, shadowed moonlight, 8 practical lights");
            int listed=station.blocks.Count(b=>NativeBlockLooks.Resolve(look.blockLooks,null,b.id,b.material,0).Listed);
            Check(listed==station.blocks.Length,"every station block has a look ("+listed+"/"+station.blocks.Length+")");
            // ---------------- not referenced by the runtime or the player build ----------------
            string[] kinds={".cs",".json",".shader",".cginc",".hlsl",".asset",".unity",".prefab",".mat",".asmdef",".txt",".md"};
            // The one runtime mention is #254's id list (NativeMapRules.ExpeditionInstance); no file loads the draft or its look.
            var hits=Directory.EnumerateFiles(Path.Combine(root,"Assets"),"*",SearchOption.AllDirectories).Where(f=>kinds.Contains(Path.GetExtension(f))&&!f.Replace('\\','/').Contains("/LocalAssets/"))
                .Where(f=>File.ReadAllText(f).Contains("expedition-central-station-draft",StringComparison.Ordinal)).ToArray();
            Check(hits.Length==0,"runtime never loads the design draft directly");
            // The importer has the station's profile but builds only the maps the selected manifest names; the live one names none.
            string importer=R("tools/generate_expedition.py");
            Check(importer.Contains("'expedition-transit': ('central-station', 'et', 1)")&&importer.Contains("for area in manifest['areas']:")&&!R("Tests/expedition-layout-draft.json").Contains("expedition-transit"),
                "the importer's navigation profile (et, 1 m) is ready, and the manifest it imports does not name the station");
            Console.WriteLine("PASS expedition station draft ("+count+" checks: Central Station at "+DraftPath+", design fixture; the map validates and imports beside today's maps ("+station.blocks.Length+" blocks, "+station.ramps.Length+" stairs); "
                +"Astra's parser and geometry gate accept the historical baseline manifest plus exp-transit and its hub door; station routes by capsule flood (floors "+string.Join(", ",floors.Select(f=>f.inside+"/"+f.total))+" pilot cells joined, "+hulk.Nodes+" Hulk cells); "
                +spawns.Length+" spawns within the entry rules, every field kept by the parser, bound as the station's own content; the 4 station enemies proposals only; the look validates and lists every block)");
        }
    }
}
