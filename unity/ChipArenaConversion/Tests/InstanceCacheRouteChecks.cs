using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ChipArena.UnityConversion
{
    internal static class InstanceCacheRouteChecks
    {
        public static void Run(string root)
        {
            string Read(string file)=>File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources",file));
            var options=new JsonSerializerOptions{IncludeFields=true};
            var maps=JsonSerializer.Deserialize<UnityMapContent>(Read("UnityMaps.json"),options);
            var horde=JsonSerializer.Deserialize<NativePveContent>(Read("PveContent.json"),options);
            var authored=NativeExpeditionLayout.Parse(Read("ExpeditionLayout.json"));
            int candidates=0,layouts=0;
            foreach(var area in authored.areas.Where(a=>a.role=="instance"))
            {
                var map=maps.maps.Single(m=>m.id==area.map);
                var chests=area.anchors.Where(a=>a.kind=="chest").ToArray();
                int variants=1;
                foreach(var chest in chests)
                {
                    int count=area.CacheCandidates[chest.id].Length;
                    candidates+=count;
                    variants=checked(variants*count);
                }
                for(int variant=0;variant<variants;variant++)
                {
                    int selection=variant;
                    var lockers=new List<ContentBlock>();
                    var approaches=new HashSet<V3>();
                    foreach(var chest in chests)
                    {
                        var choices=area.CacheCandidates[chest.id];
                        var spot=choices[selection%choices.Length];
                        selection/=choices.Length;
                        chest.x=spot.x;chest.y=spot.y;chest.z=spot.z;chest.yaw=spot.yaw;
                        int interaction=Array.FindIndex(area.interactions,i=>i.anchor==chest.id);
                        area.interactions[interaction]=NativeExpeditionGeneratedWorld.CacheInteraction(chest);
                        if(NativeExpeditionGeneratedWorld.CacheColumnClear(chest,map.blocks))
                            lockers.Add(NativeExpeditionGeneratedWorld.CacheBlock(chest));
                        approaches.Add(NativeExpeditionGeneratedWorld.CacheApproach(chest).Position);
                    }
                    var placed=map.WithCacheBlocks(lockers.ToArray());
                    var probe=new BackroomsRouteChecks.Probe(placed);
                    var search=new NativeNavSearch(map.nav);
                    var nodes=map.nav.nodes.ToDictionary(n=>n.id);
                    var links=new Dictionary<(string,float,float),bool>();
                    var clearance=new Dictionary<(string,float,float),bool>();
                    var offset=new V3(0,NativeLocomotionRules.HalfHeight,0);
                    var checkedApproaches=new HashSet<V3>();
                    bool Reach(string id,V3 from,V3 to,float r,float h)
                    {
                        if(from.Equals(area.doors[0].entries[0].Position)&&approaches.Contains(to)&&
                            r==NativeHordeKinds.HulkRadius&&h==NativeHordeKinds.HulkHeight)
                            checkedApproaches.Add(to);
                        bool Clear(MapNavNode n)
                        {
                            var key=(n.id,r,h);
                            if(!clearance.TryGetValue(key,out bool ok))
                                clearance[key]=ok=probe.Supported(n.pos-offset,r,h);
                            return ok;
                        }
                        bool Active(MapNavLink l)
                        {
                            var key=(l.id,r,h);
                            if(!links.TryGetValue(key,out bool ok))
                            {
                                var a=nodes[l.a];var b=nodes[l.b];
                                ok=Clear(a)&&Clear(b)&&(l.kind=="drop"?
                                    (l.after?.Length??0)==0:probe.WalkLink(a.pos-offset,b.pos-offset,r,h));
                                links[key]=ok;
                            }
                            return ok;
                        }
                        var start=search.NearestLocal(from+offset,Clear,4,int.MaxValue);
                        var end=search.NearestLocal(to+offset,Clear,4,int.MaxValue);
                        if(start==null||end==null)return false;
                        var path=new List<string>();
                        search.Find(start.id,end.id,Active,path);
                        return path.Count>0;
                    }
                    var layout=new ExpeditionLayout{revision=NativeExpeditionGeneratedWorld.Version,areas=new[]{area}};
                    try{NativeExpeditionLayout.ValidateGeometry(layout,new ConversionContent{maps=new[]{placed}},horde,
                        (_,p,r,h)=>probe.Supported(p,r,h),Reach);}
                    catch(FormatException ex){throw new Exception(area.id+" cache combination "+variant+": "+ex.Message,ex);}
                    if(!approaches.SetEquals(checkedApproaches))
                        throw new Exception(area.id+" did not validate every Hulk approach");
                    layouts++;
                }
            }
            Console.WriteLine("PASS all "+candidates+" authored instance cache approaches across "+layouts+" locker combinations.");
        }
    }
}
