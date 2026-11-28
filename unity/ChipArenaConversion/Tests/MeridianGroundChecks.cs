using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;

// Meridian's open grounds read at night (owner, 2026-11-21, Crown Hill's sanctuary plaza: "Theres very big empty areas, Like I feel
// the grounds should be textured? Unsure if they're intended to look like this still."; Core NativeMeridianGround and the grounds'
// finishes in NativeMeridianSurfaces; Resources/NativeStageLit.shader's Ground; docs/expedition/MERIDIAN-DRESS.md "The grounds").
// Every street plan's live deal dressed as the live run dresses it:
//  - the precinct table is the plans' own (docs/expedition/meridian-plans/*.json hub.precinct);
//  - every open floor of a ground block, and every sloped street, carries one of the grounds' finishes (a pattern and broad variation
//    StageLit keeps at every distance), never a plain one; each zone its own: the square on the precinct, setts round its edge, asphalt
//    on the routes, setts in the yards; every open ground over BigArea m2 among them;
//  - every retaining wall is the engineering brick with its weathering read from the floor in front of it (uv1.y), never from its
//    buried bottom;
//  - at night (the look's moon, shadowed by the blocks, its rim fill, and every light of the city as its drawers add them to the lamp grid), no
//    reachable floor of the square is darker than SquareFloor (linear luminance of the light it sends back: its albedo times its light),
//    and its mean at least SquareMean; shared's flat asphalt under the same light would fail both;
//  - the square's drains and manholes are flat paint, and the plaza's floods reach out over it;
//  - StageLit's grounds as pinned below (the kinds' numbers, every pattern filtered by a pixel's footprint, puddles on level floors only).
internal static class MeridianGroundChecks
{
    private static readonly System.Globalization.CultureInfo Inv=System.Globalization.CultureInfo.InvariantCulture;
    /// <summary>An open ground (m2) that must carry a ground finish; the square's floor at night: the least and the mean linear luminance
    /// it sends back (its albedo times its light; sRGB about 6 and 39 of 255 before fog and the anime steps).</summary>
    public const float BigArea=100f,SquareFloor=.002f,SquareMean=.02f;
    private static int count;
    private static void Check(bool ok,string what){count++;if(!ok)throw new Exception("Meridian grounds: "+what);}
    private static float Lin(int rgb,int shift){double v=((rgb>>shift)&255)/255.0;return (float)(v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4));}
    private static V3 Albedo(int rgb)=>new V3(Lin(rgb,16),Lin(rgb,8),Lin(rgb,0));
    private static float Luma(V3 c)=>c.x*.2126f+c.y*.7152f+c.z*.0722f;
    private static float Area(NativeMeridianDress.Quad q){var c=NativeMeridianDress.Cross(q.U,q.Vb);return c.Length;}

    public static void Run(string root,bool probe=false)
    {
        count=0;var report=new List<string>();
        // ---- the precinct table is the plans' own ----
        var plans=Path.GetFullPath(Path.Combine(root,"../../docs/expedition/meridian-plans"));
        foreach(var plan in NativeCityLayout.Plans)
        {
            string file=Path.Combine(plans,plan.Name.Replace("The ","").Replace(' ','-').ToLowerInvariant()+".json");
            Check(File.Exists(file),plan.Name+": its plan "+file);
            using var doc=JsonDocument.Parse(File.ReadAllText(file));var want=new List<(float,float,float,float)>();
            foreach(var r in doc.RootElement.GetProperty("hub").GetProperty("precinct").EnumerateArray())
                want.Add((r.GetProperty("x0").GetSingle(),r.GetProperty("z0").GetSingle(),r.GetProperty("x1").GetSingle(),r.GetProperty("z1").GetSingle()));
            var have=NativeMeridianGround.Precincts.Where(p=>p.layout==plan.Seed).Select(p=>(p.x0,p.z0,p.x1,p.z1)).ToList();
            Check(have.SequenceEqual(want),plan.Name+": the precinct table is the plan's hub.precinct");
            Check(Math.Abs(doc.RootElement.GetProperty("hub").GetProperty("y").GetSingle()-NativeMeridianGround.HubY)<1e-4f,plan.Name+": the hub's floor is at y 0");
        }
        // ---- the grounds' finishes: each a pattern with broad variation (StageLit's Ground), the square on the camp's band grid ----
        var F=NativeMeridianSurfaces.Fins;
        Check(F[NativeMeridianSurfaces.Asphalt].Detail==NativeMeridianSurfaces.DRoad&&F[NativeMeridianSurfaces.Setts].Detail==NativeMeridianSurfaces.DSetts&&F[NativeMeridianSurfaces.Paving].Detail==NativeMeridianSurfaces.DFootway
            &&F[NativeMeridianSurfaces.Square].Detail==NativeMeridianSurfaces.DSquare&&F[NativeMeridianSurfaces.BrickBlue].Detail==NativeMeridianSurfaces.DRetain&&F[NativeMeridianSurfaces.BrickBlue].FloorParam,
            "the asphalt, setts, pavements, square and retaining brick wear the grounds' finishes; the brick reads the floor in front");
        Check(Math.Abs(F[NativeMeridianSurfaces.Square].Param-NativeMeridianDress.SquareBand)<1e-6f&&NativeMeridianDress.SquareBand==8f,"the square's bands are the camp's own 8 m grid (a band out of each gate)");
        for(int z=0;z<NativeMeridianGround.Zones;z++)Check(NativeMeridianSurfaces.Ground(F[NativeMeridianGround.FinOf(z)].Detail),"zone "+NativeMeridianGround.NameOf(z)+" wears a ground finish");
        Check(Luma(Albedo(F[NativeMeridianSurfaces.Square].Rgb))>4*Luma(Albedo(F[NativeMeridianSurfaces.Asphalt].Rgb)),"the square's York flags are more than four times the asphalt's albedo");
        // ---- the look's moon (expedition-night): its light on a floor, its direction for the shadows ----
        var looks=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),new JsonSerializerOptions{IncludeFields=true});
        var night=NativeMapLooks.ByLook(looks,"expedition-night");Check(night?.key!=null,"the night look");
        // (each as Unity applies it in linear space with the project's gamma-space light intensities, as NativeLighting.ArenaRim reads
        // it: NativeEntranceLight.Applied; the rim, the look's teal fill from the south-east, casts no shadow)
        V3 Dir(MapLookLight l){double e=l.elevation*Math.PI/180,b=l.bearing*Math.PI/180;return new V3((float)(Math.Sin(b)*Math.Cos(e)),(float)Math.Sin(e),(float)(Math.Cos(b)*Math.Cos(e)));}
        V3 Light(MapLookLight l){V3 c=new V3((l.color>>16&255)/255f,(l.color>>8&255)/255f,(l.color&255)/255f);return new V3((float)NativeEntranceLight.Applied(c.x,l.intensity,true,false),(float)NativeEntranceLight.Applied(c.y,l.intensity,true,false),(float)NativeEntranceLight.Applied(c.z,l.intensity,true,false));}
        var toMoon=Dir(night.key);var moon=Light(night.key);var toRim=Dir(night.rim);var rim=Light(night.rim)*Math.Max(0,toRim.y);
        foreach(var live in HubZFightChecks.Worlds(root))
        {
            var dress=HubZFightChecks.Dress(live,null,false);var map=live.World.City.Map;string name=NativeCityLayout.Plans[live.Plan].Name;uint layout=NativeCityLayout.Plans[live.Plan].Seed;
            Check(HubZFightChecks.Dress(live,null,false).Digest()==dress.Digest(),name+": the grounds are deterministic");
            var S=new NativeMeridianDress.Solids(map.blocks);var zones=new NativeMeridianGround(layout,dress==null?null:RoutesOf(live));
            // every open floor of a ground block and every sloped street wears a ground finish; the zones by area
            var area=new float[NativeMeridianGround.Zones];int floors=0,walls=0,raised=0,onFloor=0;float big=0;var groundTop=new Dictionary<int,float>();
            foreach(var q in dress.Quads)
            {
                bool ground=q.Block>=0&&map.blocks[q.Block].id!=null&&map.blocks[q.Block].id.StartsWith("terrace-",StringComparison.Ordinal);
                bool slope=q.Block<0&&q.Fin==NativeMeridianSurfaces.Setts&&q.N.y>.9f&&q.N.y<.9999f;
                if(ground&&q.N.y>.9f||slope)
                {
                    Check(NativeMeridianSurfaces.Ground(F[q.Fin].Detail),name+": the open floor of "+(ground?map.blocks[q.Block].id:"a sloped street")+" wears a ground finish ("+q.Fin+")");floors++;
                    if(ground){var c=q.O+q.U*.5f+q.Vb*.5f;int zone=zones.At(c);Check(q.Fin==NativeMeridianGround.FinOf(zone),name+": "+map.blocks[q.Block].id+"'s floor at "+F3(c)+" is its zone's ("+NativeMeridianGround.NameOf(zone)+")");
                        area[zone]+=Area(q);groundTop[q.Block]=(groundTop.TryGetValue(q.Block,out var a0)?a0:0)+Area(q);}
                }
                else if(ground&&Math.Abs(q.N.y)<.1f)
                {
                    Check(q.Fin==NativeMeridianSurfaces.BrickBlue||q.Fin==NativeMeridianSurfaces.Coping,name+": the retaining wall of "+map.blocks[q.Block].id+" is engineering brick under its coping");
                    if(q.Fin==NativeMeridianSurfaces.BrickBlue)
                    {
                        var foot=q.O+q.U*.5f;float bottom=map.blocks[q.Block].pos.y-map.blocks[q.Block].size.y*.5f;float front=S.Floor(foot.x+q.N.x*.3f,foot.z+q.N.z*.3f,foot.y+.05f);
                        float want=front==float.MinValue||front<bottom?bottom:front;
                        // (a long face's pieces share the floor found at its middle: it lies between the block's bottom and the piece's foot)
                        Check(NativeMeridianSurfaces.ParamOf(F[q.Fin],q.Floor)==q.Floor&&q.Floor>=bottom-1e-3f&&q.Floor<=foot.y+.05f,name+FormattableString.Invariant($": the retaining wall of {map.blocks[q.Block].id} at {F3(foot)} weathers from a floor between its bottom and its foot ({q.Floor:F2}; bottom {bottom:F2})"));
                        walls++;if(q.Floor>bottom+.01f)raised++;if(Math.Abs(want-foot.y)<.02f&&Math.Abs(q.Floor-want)<1e-3f)onFloor++;
                    }
                }
            }
            foreach(var kv in groundTop)if(kv.Value>=BigArea)big+=kv.Value;
            Check(raised*2>walls,name+": most retaining wall faces weather from a floor over their buried bottom ("+raised+" of "+walls+")");
            Check(area[NativeMeridianGround.Square]>2000f,name+FormattableString.Invariant($": the square covers its precinct ({area[NativeMeridianGround.Square]:F0} m2)"));
            report.Add(name+FormattableString.Invariant($": {floors} open floors in the grounds' finishes ({big:F0} m2 of grounds over {BigArea:F0} m2), square {area[0]:F0} m2, border {area[1]:F0}, roads {area[2]:F0}, yards {area[3]:F0}; {walls} retaining wall faces, {raised} weathering from a floor over their bottom ({onFloor} exactly the floor at their foot)"));
            // ---- the square at night: its moonlight (shadowed by the blocks) and every light of the city on a 2 m grid ----
            var lights=MeridianLampChecks.CityTable(live,out int nd,out int ns,out int nb,out int nc);var table=NativeCityLamps.Build(lights);
            var square=Albedo(F[NativeMeridianSurfaces.Square].Rgb)*.94f*.9f;var asphalt=Albedo(F[NativeMeridianSurfaces.Asphalt].Rgb)*.94f;var up=new V3(0,1,0);
            float least=9,sum=0,leastOld=9,sumOld=0;int cells=0,shadowed=0;V3 at=default;
            foreach(var p in NativeMeridianGround.Precincts.Where(p=>p.layout==layout))
                for(float x=p.x0+1;x<p.x1;x+=2)for(float z=p.z0+1;z<p.z1;z+=2)
                {
                    var c=new V3(x,.01f,z);if(zones.At(c)!=NativeMeridianGround.Square)continue;
                    if(Math.Abs(S.Floor(x,z,.05f))>.02f||S.Ceiling(x,z,.05f)<2.2f)continue;   // a floor at the hub's level a pilot stands on
                    bool lit=!Shadowed(S,c,toMoon);if(!lit)shadowed++;
                    var light=table.Light(c,up)+rim+(lit?moon*toMoon.y:new V3(0,0,0));
                    float l=Luma(new V3(light.x*square.x,light.y*square.y,light.z*square.z)),o=Luma(new V3(light.x*asphalt.x,light.y*asphalt.y,light.z*asphalt.z));
                    if(l<least){least=l;at=c;}leastOld=Math.Min(leastOld,o);sum+=l;sumOld+=o;cells++;
                }
            Check(cells>400,name+": the square has its floor ("+cells+" cells)");
            float mean=sum/cells,meanOld=sumOld/cells;
            report.Add(name+FormattableString.Invariant($", the square at night ({cells} cells of 4 m2, {shadowed} in the moon's shadow; {lights.Count} city lights: {nd} lamps, {ns} street life, {nb} bleed, {nc} camp): least {least:F4} at {F3(at)}, mean {mean:F4}; shared's asphalt under the same light: least {leastOld:F4}, mean {meanOld:F4}"));
            if(!probe)
            {
                Check(least>=SquareFloor,name+FormattableString.Invariant($": no floor of the square is darker than {SquareFloor} at night ({least:F4} at {F3(at)})"));
                Check(mean>=SquareMean,name+FormattableString.Invariant($": the square's floor at night averages at least {SquareMean} ({mean:F4})"));
                Check(leastOld<SquareFloor&&meanOld<SquareMean,name+": shared's asphalt under the same light fails both");
            }
            // ---- the square's drainage is flat paint; the plaza's floods reach out over it ----
            var bleedPlan=NativeCityBleedDress.ForWalk(live.World.City,live.World.Bleed,live.World.Blueprint.worldSeed);
            var life=NativeMeridianStreetLife.Build(NativeMeridianStreetLife.InputOf(live.World.City,layout,live.World.Blueprint.worldSeed,live.World.HallSlots,HubZFightChecks.Dress(live,null,true),live.World.Bleed,bleedPlan));
            int drains=0,manholes=0,floods=0;
            foreach(var piece in life.Pieces)
            {
                var k=NativeMeridianStreetLife.Kinds[piece.Kind];
                if((k.Key=="ground.drain"||k.Key=="ground.manhole")&&zones.At(piece.At+new V3(0,.01f,0))==NativeMeridianGround.Square){if(k.Key=="ground.drain")drains++;else manholes++;Check(k.Max.y<=NativeMeridianDress.Paint,name+": "+k.Key+" is flat paint");}
                if(k.Key=="upper.flood")floods++;
            }
            Check(drains>=6,name+": the square's gullies ("+drains+")");
            report.Add(name+": the square's "+drains+" gullies and "+manholes+" manholes; "+floods+" plaza floods");
        }
        var flood=NativeMeridianStreetLife.Kinds.First(k=>k.Key=="upper.flood");
        Check(NativeMeridianStreetLife.CityLight(flood,new V3(0,7,0),new V3(0,0,7),1,1,1).Range>=20f,"a plaza flood's light reaches 20 m or more out over the square");
        Pins(root);
        foreach(var line in report)Console.WriteLine("  "+line);
        if(!probe)Console.WriteLine("PASS meridian grounds: "+count+" checks over the five plans' live deals: every open floor in a ground finish (its zone's), the retaining walls weathering from the floor in front, the square readable at night (least "+SquareFloor+", mean "+SquareMean+"), its drains flat paint");
    }
    private static string F3(V3 v)=>string.Format(Inv,"({0:F1},{1:F1},{2:F1})",v.x,v.y,v.z);
    private static NativeMeridianDress.Route[] RoutesOf(HubZFightChecks.Live live)=>live.World.City.Routes.Select(r=>new NativeMeridianDress.Route{Name=r.Name,Points=r.Points,Widths=r.Widths,Kinds=r.Kinds}).ToArray();
    /// <summary>Whether a block stands between a floor point and the moon (sampled every 0.4 m up to 60 m along the ray).</summary>
    private static bool Shadowed(NativeMeridianDress.Solids S,V3 p,V3 to)
    {
        for(float t=.4f;t<=60f;t+=.4f){var q=p+to*t;if(q.y>40f)return false;if(S.Inside(q.x,q.y,q.z,-1,.01f))return true;}
        return false;
    }
    private static void Pins(string root)
    {
        string R(string f)=>File.ReadAllText(Path.Combine(root,f));
        var stage=R("Assets/ChipArena/Resources/NativeStageLit.shader");
        Check(stage.Contains("#define DETAIL_ROAD "+NativeMeridianSurfaces.DRoad)&&stage.Contains("#define DETAIL_SQUARE "+NativeMeridianSurfaces.DSquare)&&stage.Contains("#define DETAIL_SETTS "+NativeMeridianSurfaces.DSetts)
            &&stage.Contains("#define DETAIL_FOOTWAY "+NativeMeridianSurfaces.DFootway)&&stage.Contains("#define DETAIL_RETAIN "+NativeMeridianSurfaces.DRetain),"StageLit's grounds are the Core's kinds");
        Check(stage.Contains("if(kind>=DETAIL_ROAD&&kind<=DETAIL_RETAIN)Ground(s,kind,i.mat.w,world,fw3,ng,fineFade,toon?0.45:1.0,gw,gp);")&&stage.Contains("float3 fw3=fwidth(world);"),
            "StageLit draws the grounds' broad part at every distance and their fine part within the fine fade, filtered by a pixel's footprint");
        Check(stage.Contains("return lerp(j,w/period,saturate(f/w-0.5));")&&stage.Contains("float GKeep(float size,float f){return saturate(1.6-2.0*f/size);}"),"a joint eases into its mean and a detail fades before it would alias");
        Check(stage.Contains("if(wet<=0.0||ny<0.99)return 0.0;"),"puddles stand on level floors only");
        Check(stage.Contains("float2 bd=abs(frac(p/B+0.5)-0.5)*B;"),"the square's bands lie on the finish's parameter (the camp's grid)");
        var plan=R("Assets/ChipArena/Core/NativeMeridianDressPlan.cs");
        Check(plan.Contains("case RGround:")&&plan.Contains("GroundTop(b,bi);")&&plan.Contains("if(f!=2&&f!=3)floor=FloorInFront(b,ro+ru*.5f,NormalOf(f),bottom);"),"the planner draws a ground's top by zone and its sides' weathering from the floor in front");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Core/NativeMeridianGround.cs.meta")),"NativeMeridianGround.cs has its meta");
    }
}
