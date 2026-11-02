using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The generated Expedition map's dressing (Core/NativeExpeditionCityKit.Generated.cs): on real rolls of the authority's
    // generated world, the district looks follow the blueprint's blend with no seam, every street-facing building and the
    // entrances, gates and final court are dressed, the town's placement rules hold against the generated records, the cost
    // stays in the town's budget, and the skyline keeps out of the footprint.
    internal static class GeneratedDistrictChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Generated districts: "+what);count++;}
        internal static void Run(string root)
        {
            count=0;var o=new JsonSerializerOptions{IncludeFields=true};string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources",f));
            string mapsJson=R("UnityMaps.json"),layoutJson=R("ExpeditionLayout.json");
            var content=new ConversionContent{maps=JsonSerializer.Deserialize<UnityMapContent>(mapsJson,o).maps,contentRevision=NativeMapRules.Revision(mapsJson)};
            var layout=NativeExpeditionLayout.Parse(layoutJson);layout.compatibility=NativeExpeditionLayout.Compatibility(layoutJson,content);
            Check(NativeExpeditionCityKit.LookOf("industrial")==NativeExpeditionCityKit.LookIndustrial&&NativeExpeditionCityKit.LookOf("rail")==NativeExpeditionCityKit.LookRail&&
                NativeExpeditionCityKit.LookOf("quarantine")==NativeExpeditionCityKit.LookQuarantine&&NativeExpeditionCityKit.LookOf("catacombs")==NativeExpeditionCityKit.LookTown,"the authority's themes name their looks; an unknown theme dresses as the town");
            Check(NativeExpeditionCityKit.Stem("expedition-city-bldg-ne-shops")=="bldg-ne-shops"&&NativeExpeditionCityKit.Stem(NativeExpeditionCityKit.GeneratedPrefix+"building-3:-2")=="bldg-g:3:-2"&&
                NativeExpeditionCityKit.Stem(NativeExpeditionCityKit.GeneratedPrefix+"fence-north1")=="sanctuary-fence-north1"&&NativeExpeditionCityKit.Stem(NativeExpeditionCityKit.GeneratedPrefix+"floor")=="gen-floor"&&
                NativeExpeditionCityKit.Stem("brickfall-wall-1")==null,"the generated ids read in the town's terms; other maps' ids stay unnamed");
            var near=new double[4];var nearN=new int[4];int rolls=0;var bins=new double[3,4];var binN=new int[3,4];
            foreach(uint seed in new uint[]{123,17,0x80000000u})
            {
                var room=new ExpeditionDeploymentRoom("room","host",seed,"black-siren",layout,content);Check(room.Execute("host",0,ExpeditionDeploymentIntent.RollInstances),"seed "+seed+" rolls");
                var world=ExpeditionRunBundle.Build(room.Ticket,content,layoutJson).World;var s=world.Blueprint;var map=world.Map;var blocks=map.blocks;var area=world.Area;rolls++;
                var sw=System.Diagnostics.Stopwatch.StartNew();var kit=NativeExpeditionCityKit.Build(blocks,area,world.Hub,map.bounds,true,s);long ms=sw.ElapsedMilliseconds;
                var again=NativeExpeditionCityKit.Build(blocks,area,world.Hub,map.bounds,true,s);
                Check(kit.Parts.Length>0&&kit.Parts.SequenceEqual(again.Parts)&&kit.Glows.SequenceEqual(again.Glows),"seed "+seed+": two builds give the same dressing ("+kit.Parts.Length+" parts, "+kit.Glows.Length+" lights, "+ms+" ms)");
                Check(kit.Parts.Length<=ExpeditionDressingChecks.MaxParts&&kit.Parts.Count(p=>p.Shadow)<=ExpeditionDressingChecks.MaxCasters,"seed "+seed+": within the town's budget ("+kit.Parts.Length+" parts, "+kit.Parts.Count(p=>p.Shadow)+" casters)");
                Check(kit.Parts.All(p=>(p.Flags&NativeCyberCityLooks.Bank)!=0?NativeCyberCityLooks.Keys[p.Look]!=null||NativeWardZeroLooks.Keys[p.Look]!=null:p.Look>=0&&p.Look<NativeBlockLooks.Keys.Length),"seed "+seed+": every part's look is one the shader draws");
                // ---- the district looks ----
                var parented=new HashSet<int>(kit.Parts.Select(p=>p.Parent));int dressed=0,streetFacing=0;
                for(int i=0;i<blocks.Length;i++)
                {
                    var stem=NativeExpeditionCityKit.Stem(blocks[i].id);if(stem==null||!stem.StartsWith("bldg-g:",StringComparison.Ordinal))continue;
                    var b=blocks[i];int cx=(int)Math.Round(b.pos.x/10),cz=(int)Math.Round(b.pos.z/10);
                    bool road=new[]{(1,0),(-1,0),(0,1),(0,-1)}.Any(d=>{var c=NativeDeployPresentation.CellAt(s,cx+d.Item1,cz+d.Item2);return c!=null&&c.road;});
                    if(road){streetFacing++;if(parented.Contains(i))dressed++;}
                    int pick=kit.Districts[i]-1;Check(pick>=0&&pick<4&&pick==NativeExpeditionCityKit.DistrictAt(s,b.id,b.pos.x,b.pos.z),"seed "+seed+": "+b.id+" records the district it dressed as");
                    var w=NativeDeployPresentation.Weights(s,b.pos.x,b.pos.z);
                    for(int k=0;k<4;k++){int bin=w[k]<.34?0:w[k]<.67?1:2;bins[bin,k]+=pick==k?1:0;binN[bin,k]++;}
                    for(int k=1;k<4;k++){var d=s.districts[k-1];if(Math.Abs(b.pos.x-d.x*10)+Math.Abs(b.pos.z-d.z*10)<=30){near[k]+=pick==k?1:0;nearN[k]++;}}
                    if(Math.Abs(b.pos.x)<=55&&Math.Abs(b.pos.z)<=55){near[0]+=pick==0?1:0;nearN[0]++;}
                }
                Check(streetFacing>0&&dressed==streetFacing,"seed "+seed+": every building on a street is dressed ("+dressed+" of "+streetFacing+")");
                // ---- signs ----
                foreach(var d in s.districts)Check(kit.Lettering.Contains(NativeExpeditionJoinZones.Of(d.instance).Name),"seed "+seed+": the "+d.instance+" entrance carries its name");
                Check(kit.Lettering.Contains("SANCTUARY")&&kit.Lettering.Contains("CHECKPOINT"),"seed "+seed+": the sanctuary's gates and the final court are lettered");
                Check(kit.Glows.Length>=s.districts.Length*4,"seed "+seed+": the streets and entrances are lit ("+kit.Glows.Length+" lights)");
                // ---- placement: the town's rules on the generated records ----
                var solid=blocks.Where(b=>NativeExpeditionCityKit.Stem(b.id)!="gen-floor").ToArray();int low=0,flat=0;
                for(int i=0;i<kit.Parts.Length;i++)
                {
                    var p=kit.Parts[i];var b=blocks[p.Parent];var w=ExpeditionDressingChecks.Pose(b,p);string at=b.id+"#"+i+" (role "+p.Role+") at "+((w.X0+w.X1)/2).ToString("0.0")+", "+((w.Y0+w.Y1)/2).ToString("0.00")+", "+((w.Z0+w.Z1)/2).ToString("0.0");
                    bool isFlat=w.Y1<=NativeExpeditionCityKit.Flat+1e-4;if(isFlat)flat++;
                    if(w.Y0<NativeExpeditionCityKit.Low&&!isFlat)
                    {
                        low++;bool hugs=true;for(int k=0;k<8&&hugs;k++){double best=double.MaxValue;foreach(var c in solid){if(c.pos.y+c.size.y/2<w.Y0-.3||c.pos.y-c.size.y/2>w.Y1)continue;best=Math.Min(best,ExpeditionDressingChecks.Outside(c,w.Cx[k],w.Cz[k]));}if(best>NativeExpeditionCityKit.WallBand+.012)hugs=false;}
                        Check(hugs,"seed "+seed+": below 3.1 m a part stands within 0.25 m of a block: "+at);
                    }
                    foreach(var e in area.spawns.Select(x=>x.entry))Check(!ExpeditionDressingChecks.Disc(w,e.exit.x,e.exit.z,Math.Max(1.2f,e.clear),-1,3.2),"seed "+seed+": nothing on a spawn's exit ("+e.id+"): "+at);
                    void Point(ExpeditionPoint q,double r,double h,string what){if(q!=null)Check(!ExpeditionDressingChecks.Disc(w,q.x,q.z,r,q.y-1,q.y+h),"seed "+seed+": nothing on "+what+": "+at);}
                    foreach(var q in area.slots)Point(q,.6,2.2,"a sanctuary slot");foreach(var q in area.bossPoints)Point(q,1.2,3.4,"a boss point");
                    foreach(var it in area.interactions)Check(!ExpeditionDressingChecks.Disc(w,it.x,it.z,.9,it.y-1,it.y+2.2),"seed "+seed+": nothing on a cache: "+at);
                    foreach(var a in area.anchors)Check(!ExpeditionDressingChecks.Disc(w,a.x,a.z,.9,a.y-1,a.y+2.2),"seed "+seed+": nothing on an anchor: "+at);
                    foreach(var d in area.doors)
                    {
                        Point(d.approach,.6,2.2,"a door's approach");foreach(var q in d.returns)Point(q,.6,2.2,"a door return");
                        Check(!ExpeditionDressingChecks.Disc(w,d.join.x,d.join.z,d.join.r,d.join.y-1,d.join.y+d.join.h),"seed "+seed+": nothing in a join circle: "+at);
                        foreach(var v in new[]{d.interact,d.closure}){var x=ExpeditionDressingChecks.Aabb(v);Check(!ExpeditionDressingChecks.BoxHit(w,x[0],x[1],x[2],x[3],x[4],x[5]),"seed "+seed+": nothing in a door's volumes: "+at);}
                        var c=d.closure;double ang=c.yaw*Math.PI/180,cs=Math.Abs(Math.Cos(ang)),sn=Math.Abs(Math.Sin(ang)),hw=c.sx/2+1.9,hd=.75;double ux=cs*hw+sn*hd,uz=sn*hw+cs*hd;
                        Check(!ExpeditionDressingChecks.BoxHit(w,c.x-ux,c.x+ux,0,4.1,c.z-uz,c.z+uz),"seed "+seed+": nothing in a door's airlock unit space: "+at);
                    }
                    foreach(var g in area.gates){var x=ExpeditionDressingChecks.Aabb(g.closure);Check(!ExpeditionDressingChecks.BoxHit(w,x[0]-.5,x[1]+.5,x[2],x[3]+.5,x[4]-.5,x[5]+.5),"seed "+seed+": nothing in the final gate's volume: "+at);}
                    foreach(var v in area.sanctuary)
                    {
                        var x=ExpeditionDressingChecks.Aabb(v);if(isFlat||w.Y0>=2.6||!ExpeditionDressingChecks.BoxHit(w,x[0],x[1],x[2],x[3],x[4],x[5]))continue;
                        Check(NativeExpeditionCityKit.Stem(b.id).StartsWith("sanctuary-fence-",StringComparison.Ordinal),"seed "+seed+": the sanctuary floor holds only flat paint and its fences' dressing: "+at);
                    }
                }
                Check(low>0&&flat>0,"seed "+seed+": the rules saw wall dressing and flat paint ("+low+" low, "+flat+" flat)");
            }
            // The blend with no seam: across the rolls, a district's buildings grow with its share; each entrance's neighbourhood is
            // mostly its own; the plaza's mostly the town's.
            for(int k=0;k<4;k++)
            {
                double Share(int bin)=>binN[bin,k]==0?double.NaN:bins[bin,k]/binN[bin,k];
                var low=Share(0);var mid=Share(1);var high=Share(2);
                Check(double.IsNaN(mid)||double.IsNaN(low)||mid>low,"district "+k+": more of its buildings where its share is a third to two thirds than below ("+low.ToString("0.00")+" / "+mid.ToString("0.00")+")");
                Check(double.IsNaN(high)||double.IsNaN(mid)||high>mid,"district "+k+": more where its share is over two thirds ("+mid.ToString("0.00")+" / "+high.ToString("0.00")+")");
            }
            for(int k=1;k<4;k++)Check(nearN[k]>0&&near[k]/nearN[k]>=.5,"district "+k+": at least half the buildings within 30 m of its entrance wear its look ("+near[k]+" of "+nearN[k]+")");
            Check(nearN[0]>0&&near[0]/nearN[0]>=.45,"the town's look leads in the plaza's ring, 55 m out ("+near[0]+" of "+nearN[0]+")");
            // ---- the skyline round the footprint ----
            var back=NativeExpeditionCityScenery.Around(out var fires,-125,125,-95,95);
            Check(back.Length>0&&back.All(b=>{double t=b.Yaw*Math.PI/180,c=Math.Abs(Math.Cos(t)),n=Math.Abs(Math.Sin(t)),ex=c*b.Hx+n*b.Hz,ez=n*b.Hx+c*b.Hz;
                bool standing=b.Y+b.Hy>.001f;double m=standing?NativeExpeditionCityScenery.Margin-.01:-.01;return !(b.X+ex>-125-m&&b.X-ex<125+m&&b.Z+ez>-95-m&&b.Z-ez<95+m);}),"the generated map's skyline stands "+NativeExpeditionCityScenery.Margin+" m or more beyond its footprint; its ground plates stop at the edge ("+back.Length+" boxes, "+fires.Length+" fires)");
            Console.WriteLine("PASS generated districts: "+count+" checks ("+rolls+" rolls)");
        }
    }
}
