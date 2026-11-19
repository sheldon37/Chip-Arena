using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The generated Expedition map's frame budget (docs/EXPEDITION-OVERHAUL.md W0 and section 6; the budget table in
    // docs/EXPEDITION-DEPLOYMENT.md "Frame budget"): what the city's own presentation submits for one view, counted the way the
    // runtime selects and batches it, from six fixed eyes over twenty rolls, on the High and Low tiers. Pinned at the shared
    // head's numbers with 15% headroom, so a change that adds draws, triangles, instances, casters, effect quads or lights shows
    // here first, and its package raises the pin on purpose.
    //
    // A view's census (Runtime NativeExpeditionCityPresentation, NativeArenaDressing, NativeAdvancedLightingPresentation):
    //  - the kit: DrawTownKit's selection (the low tier leaves the detail parts out; a part past NativeExpeditionCityKit.Range of
    //    the eye, or on a face its building hides, NativeExpeditionCityKit.Hide, is left undrawn), in its order (the parts that
    //    take the style's wrap first) into casting and trim batches of NativeBlockLooks.Chunk; a batch up to the last holding a
    //    wrap part draws the wrap mesh, the rest the rounded box (one box mesh on the low tier, the wrap being off there);
    //  - the skyline round the footprint (NativeExpeditionCityScenery.Around): static batches, every frame, no shadows;
    //  - the dressed blocks (NativeArenaDressing): every block of the map at rest in batches of Chunk, the style's stage mesh;
    //  - the atmosphere (NativeExpeditionCityFx: one solid and one effects draw), the volumetric beams that take its shafts on
    //    the High tier (one instanced draw), and the advanced weather (NativeWeatherFx: its own two draws) on the High tier;
    //  - the practical slots the picker fills at the eye (NativeExpeditionCityLights.Settle: at most eight).
    // Casters: the casting kit batches and the blocks on the High tier (the low tier has no shadows). Left out: pilots, zombies,
    // pods, machines, the hub gates' leaves and dust, the sky and the HUD (they do not grow with the city), and the shadow
    // cascades' repeats of the casting batches.
    internal static class GeneratedFrameChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Generated frame: "+what);count++;}
        /// <summary>One view's census.</summary>
        internal struct View
        {
            public int Draws,Instances,MaxPerDraw,Casters,Quads,Rain,Beams,Lights,KitParts,LowParts,Blocks,Backdrop;public long Tris;
        }
        internal const int Seeds=20,Eyes=6;internal const double Time=31.3;
        internal static readonly string[] EyeNames={"hub","south apron","east court","west court","final court","hub high"};
        // ---- the shared head's numbers (claude/sleepy-ritchie-g8ruhv 15dab150, 2026-11-18, before any overhaul code): the most
        // over 6 eyes x 20 rolls. The pins are these with 15% headroom (Pin, rounded up), but the effect quads': W0's ground fog
        // now lies on the generated streets (+14%), so they are pinned on W0's own numbers (FogHighQuads, FogLowQuads).
        internal const int BaseHighDraws=31,BaseHighInstances=11835,BaseHighCasters=1004,BaseHighQuads=1105,BaseHighRain=222,BaseHighBeams=18,BaseHighKit=9896;internal const long BaseHighTris=2177224;
        internal const int BaseLowDraws=22,BaseLowInstances=8878,BaseLowQuads=765,BaseLowParts=6951;internal const long BaseLowTris=817302;
        internal const int FogHighQuads=1243,FogLowQuads=860;
        internal static long Pin(long baseline)=>(long)Math.Ceiling(baseline*1.15);
        // ---- the triangles (W0's review): already over the plan's targets (section 6: 2.0 M a view at High, 0.8 M at Low) before
        // any overhaul code, so there is no headroom (the target minus the baseline) to share out by milestone. Until the owner
        // decides (accept the baseline, or cut the skyline: docs/EXPEDITION-DEPLOYMENT.md), they are a no-growth gate at today's
        // worst view, not a budget: High at the shared head's 2,177,224 (W0's 2,173,532 is under it), Low at W0's 821,492 (the
        // shared head's 817,302 plus 0.5%: the seeded dressing puts 45 more low-tier parts in the worst of the 120 views).
        internal const long TargetHighTris=2000000,TargetLowTris=800000,GateHighTris=2177224,GateLowTris=821492;

        /// <summary>The six eyes for a roll, by role so they stay comparable across rolls (and later layouts): the sanctuary's
        /// centre, the apron outside its south gate, the east and west entrances' join circles, the final court, and the hub's
        /// overview from 46 m up and 62 m south. Head height 1.7 m.</summary>
        internal static (float x,float y,float z)[] EyesOf(ExpeditionGeneratedWorld world)
        {
            var area=world.Area;var s=world.Blueprint;
            (float,float,float) Join(string side)
            {
                var d=s.districts.FirstOrDefault(x=>x.side==side);var door=d==null?null:area.doors.FirstOrDefault(x=>x.anchor=="exp-city:door:"+d.instance);
                return door==null?(side=="east"?60f:-60f,1.7f,0f):(door.join.x,1.7f,door.join.z);
            }
            return new[]{(0f,1.7f,0f),(0f,1.7f,-30f),Join("east"),Join("west"),(0f,1.7f,70f),(0f,46f,-62f)};
        }

        /// <summary>A roll's city as the runtime builds it once per map (the kit, the atmosphere's scene, the light picker).</summary>
        internal sealed class City
        {
            public ExpeditionGeneratedWorld World;public NativeExpeditionCityKit Kit;public NativeCityFxScene Scene;public NativeExpeditionCityLights Lights;public MapLook Look;
            public float Far;public float[] Spots;public int[] Order;public int WrapParts;public NativeWeatherCover Cover;
            /// <summary>The skyline round this roll's footprint and its backdrop fires, from the map's own bounds as the runtime builds
            /// them (NativeExpeditionCityScenery.Around over map.bounds), so a larger map's skyline is counted as drawn.</summary>
            public NativeCityBackdropBox[] Backdrop;public NativeCityBackdropFire[] Fires;
        }
        /// <summary>A roll's city from the shared look as the runtime draws it (NativeLighting.Look: the generated map's own
        /// practicals, NativeExpeditionCityLights.ForMap; Runtime NativeExpeditionCityPresentation.ExpeditionCityBuild), its skyline
        /// from `backdrop` and `fires` (Setup.Skyline over its bounds).</summary>
        internal static City Build(ExpeditionGeneratedWorld world,MapLook look,NativeCityBackdropBox[] backdrop,NativeCityBackdropFire[] fires)
        {
            var map=world.Map;var blocks=map.blocks;look=NativeExpeditionCityLights.ForMap(look,map.id);var c=new City{World=world,Look=look,Backdrop=backdrop,Fires=fires};
            c.Kit=NativeExpeditionCityKit.Build(blocks,world.Area,world.Hub,map.bounds,true,world.Blueprint);
            c.Scene=NativeCityFxScene.Build(blocks,c.Kit,fires,look,world.Hub,map.bounds);c.Scene.KeepNearest=true;
            c.Lights=NativeExpeditionCityLights.Build(c.Kit,look);c.Far=NativeExpeditionCityKit.FarRange(look);c.Cover=NativeWeatherCover.Build(blocks,map.bounds);
            var parts=c.Kit.Parts;c.Spots=new float[parts.Length*3];
            for(int i=0;i<parts.Length;i++){var p=parts[i];var b=blocks[p.Parent];c.Spots[i*3]=b.pos.x+p.X;c.Spots[i*3+1]=b.pos.y+p.Y;c.Spots[i*3+2]=b.pos.z+p.Z;}
            c.Order=new int[parts.Length];int w=0;
            for(int i=0;i<parts.Length;i++)if(NativeMapStyle.NeedsWrap(parts[i].Hx,parts[i].Hy,parts[i].Hz,parts[i].Look,parts[i].Flags))c.Order[w++]=i;c.WrapParts=w;
            for(int i=0;i<parts.Length;i++)if(!NativeMapStyle.NeedsWrap(parts[i].Hx,parts[i].Hy,parts[i].Hz,parts[i].Look,parts[i].Flags))c.Order[w++]=i;
            return c;
        }
        private static int Tris(NativeBlockLooks.StageMesh m)=>m.Triangles.Length/3;
        private static int Chunks(int n)=>(n+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk;

        /// <summary>One view's census from eye e on the High tier (`low` false) or the Low tier.</summary>
        internal static View Census(City c,NativeCityBackdropBox[] backdrop,(float x,float y,float z) e,bool low,NativeExpeditionCityFx fx,NativeBeamList beams,NativeWeatherFx weather,NativeFxLight sun,AdvancedLightState adv)
        {
            var v=new View();var kit=c.Kit;var parts=kit.Parts;var look=c.Look;bool style=NativeMapStyle.On(look,false),wrap=style&&!low;
            int meshTris=Tris(NativeBlockLooks.Build(0)),toonTris=Tris(NativeToonMesh.Build()),wrapTris=Tris(NativeToonMesh.Wrap());
            int townMesh=wrap?wrapTris:style?toonTris:meshTris,townSmall=wrap?toonTris:townMesh;
            // ---- the kit (DrawTownKit) ----
            var ratio=new float[kit.FaceCulls.Length+1];kit.Hide(e.x,e.y,e.z,ratio);
            var cast=new List<int>();var trim=new List<int>();int castWrap=0,trimWrap=0;
            for(int k=0;k<c.Order.Length;k++)
            {
                int i=c.Order[k];var p=parts[i];if(p.Detail&&low)continue;
                float dx=c.Spots[i*3]-e.x,dy=c.Spots[i*3+1]-e.y,dz=c.Spots[i*3+2]-e.z;float r=NativeExpeditionCityKit.Range(p,c.Far);
                if(dx*dx+dy*dy+dz*dz>r*r||p.Face!=0&&ratio[p.Face]<=p.Back)continue;
                var list=p.Shadow?cast:trim;if(list.Count==0||list[list.Count-1]==NativeBlockLooks.Chunk)list.Add(0);list[list.Count-1]++;
                if(k<c.WrapParts){if(p.Shadow)castWrap=cast.Count;else trimWrap=trim.Count;}
            }
            for(int b=0;b<cast.Count;b++){v.Tris+=(long)cast[b]*(b<castWrap?townMesh:townSmall);v.MaxPerDraw=Math.Max(v.MaxPerDraw,cast[b]);}
            for(int b=0;b<trim.Count;b++){v.Tris+=(long)trim[b]*(b<trimWrap?townMesh:townSmall);v.MaxPerDraw=Math.Max(v.MaxPerDraw,trim[b]);}
            v.KitParts=cast.Sum()+trim.Sum();if(low)v.LowParts=v.KitParts;v.Draws+=cast.Count+trim.Count;v.Instances+=v.KitParts;if(!low)v.Casters+=cast.Sum();
            // ---- the skyline (DrawTownBackdrop: the town mesh, every frame) ----
            v.Backdrop=backdrop.Length;v.Draws+=Chunks(backdrop.Length);v.Instances+=backdrop.Length;v.Tris+=(long)backdrop.Length*townMesh;
            v.MaxPerDraw=Math.Max(v.MaxPerDraw,Math.Min(NativeBlockLooks.Chunk,backdrop.Length));
            // ---- the dressed blocks (NativeArenaDressing: every block at rest, stage 0, the style's stage mesh) ----
            int blocks=c.World.Map.blocks.Length;v.Blocks=blocks;v.Draws+=Chunks(blocks);v.Instances+=blocks;v.Tris+=(long)blocks*(wrap?wrapTris:style?toonTris:meshTris);if(!low)v.Casters+=blocks;
            v.MaxPerDraw=Math.Max(v.MaxPerDraw,Math.Min(NativeBlockLooks.Chunk,blocks));
            // ---- the atmosphere, its beams and the weather ----
            beams.Clear();fx.Beams=low?null:beams;fx.Build(c.Scene,Time,e.x,e.y,e.z,1,0,0,0,1,0,sun,1,low);fx.Beams=null;
            v.Quads=fx.Quads.Count;v.Tris+=2L*v.Quads;if(v.Quads>0)v.Draws+=2;
            v.Beams=low?0:beams.Count;if(v.Beams>0){v.Draws++;v.Instances+=v.Beams;}
            if(!low&&adv.Weather.Length>0){weather.Build(adv.Weather,adv.Amount,adv.Wind,Time,e.x,e.y,e.z,1,0,0,0,1,0,sun,c.Cover);v.Rain=weather.Quads.Count;v.Tris+=2L*v.Rain;if(v.Rain>0)v.Draws+=2;}
            // ---- the practical slots ----
            c.Lights.Settle(e.x,e.y,e.z,Time);for(int s=0;s<NativeExpeditionCityLights.Slots;s++)if(c.Lights.Slot[s]>=0)v.Lights++;
            return v;
        }

        /// <summary>The content, layout and look the rolls are built from (as GeneratedDistrictChecks reads them).</summary>
        internal sealed class Setup
        {
            public ConversionContent Content;public ExpeditionLayout Layout;public string LayoutJson;public MapLook Look;public AdvancedLightState Adv=new AdvancedLightState();
            private readonly Dictionary<(double,double,double,double),(NativeCityBackdropBox[] boxes,NativeCityBackdropFire[] fires)> skylines=new Dictionary<(double,double,double,double),(NativeCityBackdropBox[],NativeCityBackdropFire[])>();
            /// <summary>The skyline and backdrop fires round a map's own bounds (W0's review: not a fixed footprint, so the census
            /// follows the map when it grows; the owner chose size B), built once per distinct bounds as the runtime builds it.</summary>
            public (NativeCityBackdropBox[] boxes,NativeCityBackdropFire[] fires) Skyline(MapBounds b)
            {
                var key=(b.minX,b.maxX,b.minZ,b.maxZ);if(skylines.TryGetValue(key,out var s))return s;
                var boxes=NativeExpeditionCityScenery.Around(out var fires,b.minX,b.maxX,b.minZ,b.maxZ);s=(boxes,fires);skylines[key]=s;return s;
            }
            public Setup(string root)
            {
                var o=new JsonSerializerOptions{IncludeFields=true};string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources",f));
                string mapsJson=R("UnityMaps.json");LayoutJson=R("ExpeditionLayout.json");
                Content=new ConversionContent{maps=JsonSerializer.Deserialize<UnityMapContent>(mapsJson,o).maps,contentRevision=NativeMapRules.Revision(mapsJson)};
                Layout=NativeExpeditionLayout.Parse(LayoutJson);Layout.compatibility=NativeExpeditionLayout.Compatibility(LayoutJson,Content);
                Look=NativeMapStyle.Styled(NativeMapLooks.ByLook(MapLookChecks.Load(root),"expedition-city"),false);NativeAdvancedLooks.Resolve(Look,NativeGraphicsTier.High,false,Adv);
            }
            /// <summary>A rolled world's city, its skyline from its own map's bounds.</summary>
            public City CityOf(ExpeditionGeneratedWorld world){var s=Skyline(world.Map.bounds);return GeneratedFrameChecks.Build(world,Look,s.boxes,s.fires);}
            /// <summary>The world a seed rolls (null when the station refuses the roll).</summary>
            public ExpeditionGeneratedWorld World(uint seed)
            {
                var room=new ExpeditionDeploymentRoom("room","host",seed,"black-siren",Layout,Content);if(!room.Execute("host",0,ExpeditionDeploymentIntent.RollInstances))return null;
                return ExpeditionRunBundle.Build(room.Ticket,Content,LayoutJson).World;
            }
        }
        /// <summary>Every view's census: [roll][eye][tier 0 High, 1 Low].</summary>
        internal static View[][][] Measure(Setup set,out uint[] seeds,out double ms,out int[] build,out City first)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            var fx=new NativeExpeditionCityFx();var beams=new NativeBeamList();var weather=new NativeWeatherFx();var sun=Sun();
            var list=new List<uint>();var all=new List<View[][]>();build=new int[5];first=null;
            for(uint seed=1;list.Count<Seeds&&seed<200;seed++)
            {
                var world=set.World(seed);if(world==null)continue;var city=set.CityOf(world);first??=city;var eyes=EyesOf(world);var per=new View[eyes.Length][];
                var kp=city.Kit.Parts;build[0]=Math.Max(build[0],kp.Length);build[1]=Math.Max(build[1],kp.Count(x=>!x.Detail));build[2]=Math.Max(build[2],kp.Count(x=>x.Shadow));build[3]=Math.Max(build[3],city.Kit.Glows.Length);build[4]=Math.Max(build[4],city.Backdrop.Length);
                for(int i=0;i<eyes.Length;i++)per[i]=new[]{Census(city,city.Backdrop,eyes[i],false,fx,beams,weather,sun,set.Adv),Census(city,city.Backdrop,eyes[i],true,fx,beams,weather,sun,set.Adv)};
                list.Add(seed);all.Add(per);
            }
            seeds=list.ToArray();ms=watch.Elapsed.TotalMilliseconds;return all.ToArray();
        }
        private static NativeFxLight Sun(){var sun=new NativeFxLight();for(int k=0;k<9;k++)sun.Ambient[k]=.05;return sun;}

        internal static void Run(string root)
        {
            count=0;var set=new Setup(root);var views=Measure(set,out var seeds,out double ms,out var build,out var city);Check(seeds.Length==Seeds,"twenty rolls ("+seeds.Length+")");
            View Most(int tier){var m=new View();foreach(var roll in views)foreach(var eye in roll){var v=eye[tier];
                m.Draws=Math.Max(m.Draws,v.Draws);m.Tris=Math.Max(m.Tris,v.Tris);m.Instances=Math.Max(m.Instances,v.Instances);m.MaxPerDraw=Math.Max(m.MaxPerDraw,v.MaxPerDraw);m.Casters=Math.Max(m.Casters,v.Casters);
                m.Quads=Math.Max(m.Quads,v.Quads);m.Rain=Math.Max(m.Rain,v.Rain);m.Beams=Math.Max(m.Beams,v.Beams);m.Lights=Math.Max(m.Lights,v.Lights);m.KitParts=Math.Max(m.KitParts,v.KitParts);m.LowParts=Math.Max(m.LowParts,v.LowParts);}return m;}
            var hi=Most(0);var lo=Most(1);
            string Line(View m)=>m.Draws+" draws, "+m.Tris.ToString("N0")+" triangles, "+m.Instances+" instances (at most "+m.MaxPerDraw+" a draw), "+m.Casters+" casters, "+m.Quads+" effect quads, "+m.Rain+" rain quads, "+m.Beams+" beams, "+m.Lights+" slots lit, "+m.KitParts+" kit parts";
            Console.WriteLine("   the most over "+Eyes+" eyes x "+seeds.Length+" rolls ("+ms.ToString("0")+" ms): High "+Line(hi)+"; Low "+Line(lo)+", "+lo.LowParts+" low-tier parts");
            Console.WriteLine("   meshes: the style's wrap "+Tris(NativeToonMesh.Wrap())+" triangles, its rounded box "+Tris(NativeToonMesh.Build())+", the legacy box "+Tris(NativeBlockLooks.Build(0))+"; the most a roll builds: "+build[0]+" kit parts, "+build[1]+" low-tier, "+build[2]+" casting, "+build[3]+" light sources; "+build[4]+" skyline boxes; rolls "+string.Join(",",seeds));
            for(int i=0;i<Eyes;i++)
            {
                double Mean(int tier,Func<View,double> f)=>views.Average(r=>f(r[i][tier]));
                Console.WriteLine("   "+EyeNames[i]+": High mean "+Mean(0,v=>v.Draws).ToString("0")+" draws, "+Mean(0,v=>v.Tris).ToString("N0")+" triangles, "+Mean(0,v=>v.Casters).ToString("0")+" casters, "+Mean(0,v=>v.Quads).ToString("0")+" quads, "+Mean(0,v=>v.Lights).ToString("0.0")+" slots"
                    +"; Low mean "+Mean(1,v=>v.Draws).ToString("0")+" draws, "+Mean(1,v=>v.Tris).ToString("N0")+" triangles, "+Mean(1,v=>v.LowParts).ToString("0")+" low-tier parts, "+Mean(1,v=>v.Quads).ToString("0")+" quads");
            }
            // ---- the hard limits, then the pins ----
            Check(hi.MaxPerDraw<=NativeBlockLooks.Chunk&&lo.MaxPerDraw<=NativeBlockLooks.Chunk,"no draw holds more than "+NativeBlockLooks.Chunk+" instances");
            Check(hi.Lights<=NativeExpeditionCityLights.Slots&&lo.Lights<=NativeExpeditionCityLights.Slots,"at most "+NativeExpeditionCityLights.Slots+" practical slots lit");
            Check(hi.Quads<=NativeExpeditionCityFx.Capacity&&lo.Quads<=NativeExpeditionCityFx.Capacity,"the atmosphere fits its buffer");
            void Under(string what,long now,long pin,long baseline){Check(now<=pin,what+": "+now.ToString("N0")+" (pin "+pin.ToString("N0")+", the shared head's "+baseline.ToString("N0")+"); a package that needs more raises the pin on purpose (docs/EXPEDITION-DEPLOYMENT.md \"Frame budget\")");}
            // The triangles: a no-growth gate (no headroom under the section 6 targets), the gap to the target in the message.
            void Gate(string what,long now,long gate,long target){Check(now<=gate,what+": "+now.ToString("N0")+" (no-growth gate "+gate.ToString("N0")+": today's worst view is already "+(gate-target).ToString("N0")+" over the plan's "+target.ToString("N0")+", so no package may add any until the owner decides; docs/EXPEDITION-DEPLOYMENT.md \"Frame budget\")");}
            Console.WriteLine("   over target before any overhaul code: High triangles "+hi.Tris.ToString("N0")+" against "+TargetHighTris.ToString("N0")+" ("+((hi.Tris-TargetHighTris)*100.0/TargetHighTris).ToString("+0.0")+"%), Low "+lo.Tris.ToString("N0")+" against "+TargetLowTris.ToString("N0")+" ("+((lo.Tris-TargetLowTris)*100.0/TargetLowTris).ToString("+0.0")+"%); Low lights "+lo.Lights+" practical slots against 3");
            Gate("High triangles",hi.Tris,GateHighTris,TargetHighTris);Gate("Low triangles",lo.Tris,GateLowTris,TargetLowTris);
            Check(BaseHighTris>TargetHighTris&&BaseLowTris>TargetLowTris&&GateHighTris==Math.Max(BaseHighTris,2173532)&&GateLowTris<=BaseLowTris*1.01,"the triangle gates hold today's worst view (no headroom under the targets)");
            Under("High draws",hi.Draws,Pin(BaseHighDraws),BaseHighDraws);Under("High instances",hi.Instances,Pin(BaseHighInstances),BaseHighInstances);
            Under("High casters",hi.Casters,Pin(BaseHighCasters),BaseHighCasters);Under("High effect quads",hi.Quads,Pin(FogHighQuads),BaseHighQuads);Under("High rain quads",hi.Rain,Pin(BaseHighRain),BaseHighRain);
            Under("High beams",hi.Beams,Pin(BaseHighBeams),BaseHighBeams);Under("High kit parts",hi.KitParts,Pin(BaseHighKit),BaseHighKit);
            Under("Low draws",lo.Draws,Pin(BaseLowDraws),BaseLowDraws);Under("Low instances",lo.Instances,Pin(BaseLowInstances),BaseLowInstances);
            Under("Low effect quads",lo.Quads,Pin(FogLowQuads),BaseLowQuads);Under("Low low-tier parts",lo.LowParts,Pin(BaseLowParts),BaseLowParts);
            Check(lo.Casters==0&&lo.Rain==0&&lo.Beams==0,"the low tier casts no shadows and draws no advanced weather or beams");
            var again=Census(city,city.Backdrop,EyesOf(city.World)[0],false,new NativeExpeditionCityFx(),new NativeBeamList(),new NativeWeatherFx(),Sun(),set.Adv);var once=views[0][0][0];
            Check(again.Draws==once.Draws&&again.Tris==once.Tris&&again.Instances==once.Instances&&again.Quads==once.Quads&&again.Lights==once.Lights,"the census is the same every time");
            Skyline(set,city);Fog(city);FxFlags(root);Practicals(set,city);Variety(set);Volume(set,city);RuntimeSites(root);FrameTimes(root);
            Console.WriteLine("PASS generated frame: "+count+" checks ("+seeds.Length+" rolls x "+Eyes+" eyes, High and Low; the skyline from the map's bounds, the ground fog, the effects' flags, the generated map's own practicals, the kit's variety, the light volume's candidates, the runtime's call sites and the 4-pilot frame-time hook)");
        }

        // ---- W0's review: the 4-pilot frame-time row is taken alongside Astra's DEV load harness as pushed (A0: an isolated session
        // launched with -expedition-load N, -load-balanced, -load-low), summarised as BoZo's xperf windows summarise the 1-pilot rows ----
        private static void FrameTimes(string root)
        {
            var ms=new float[120];for(int i=0;i<100;i++)ms[i]=100-i;var s=NativeFrameBudget.Of(ms,100);
            Check(s.Frames==100&&Math.Abs(s.Mean-50.5)<1e-9&&s.P50==51&&s.P95==96&&s.P99==100&&s.Worst==100&&s.Over20==80&&s.Over50==50,"the frame-time summary reads as xperf's (the q-th of the sorted times at floor(n x q): p50 "+s.P50+", p95 "+s.P95+", p99 "+s.P99+")");
            Check(NativeFrameBudget.Of(ms,0).Frames==0&&NativeFrameBudget.Of(null,5).Frames==0&&NativeFrameBudget.Line("x",s)=="x: 100 frames; ms mean 50.50 p50 51.00 p95 96.00 p99 100.00 worst 100.00; over 20 ms 80, over 50 ms 50","an empty window is none; the line is the budget table's");
            var src=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeExpeditionLoadFrameTimes.cs")).Replace("\r\n","\n");
            Check(src.StartsWith("#if UNITY_EDITOR || DEVELOPMENT_BUILD\n",StringComparison.Ordinal)&&src.TrimEnd().EndsWith("#endif",StringComparison.Ordinal),"the frame-time hook is in Editor and Development builds only");
            Check(src.Contains("public const string Flag=\"-expedition-load\",Balanced=\"-load-balanced\",Low=\"-load-low\";")&&src.Contains("int at=Array.IndexOf(args,Flag);if(at<0)return;")&&src.Contains("NativeFrameBudget.Line(label,NativeFrameBudget.Of(ms,n))"),"it reads A0's harness flags and nothing else, and does nothing without them");
            Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Runtime/NativeExpeditionLoadFrameTimes.cs.meta"))&&File.Exists(Path.Combine(root,"Assets/ChipArena/Core/NativeFrameBudget.cs.meta")),"Unity metas for the hook");
        }

        // ---- W0's review: the census builds each roll's skyline from its own map's bounds, as the runtime does, so it follows
        // the map when it grows (the owner chose size B, 600 x 480 m) ----
        private static void Skyline(Setup set,City c)
        {
            var b=c.World.Map.bounds;var own=NativeExpeditionCityScenery.Around(out var fires,b.minX,b.maxX,b.minZ,b.maxZ);
            Check(c.Backdrop.Length==own.Length&&c.Fires.Length==fires.Length&&c.Backdrop.Length>0,"the census counts the skyline round the map's own bounds ("+c.Backdrop.Length+" boxes round "+b.minX+".."+b.maxX+" x "+b.minZ+".."+b.maxZ+")");
            // A larger map's skyline is its own (a size-B footprint, 600 x 480 m): the census would count that one, not today's.
            var big=set.Skyline(new MapBounds{minX=-300,maxX=300,minZ=-240,maxZ=240});
            Check(big.boxes.Length>0&&!ReferenceEquals(big.boxes,c.Backdrop)&&(big.boxes.Length!=c.Backdrop.Length||big.boxes.Min(x=>x.X)<c.Backdrop.Min(x=>x.X)),"a larger map's census takes its own skyline ("+big.boxes.Length+" boxes round 600 x 480 m)");
        }

        // ---- W0's review: the runtime's call sites for the fixes, pinned (the checks above build their scenes themselves, so
        // reverting one of these lines would otherwise still pass) ----
        private static void RuntimeSites(string root)
        {
            string Src(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f)).Replace("\r\n","\n");
            var city=Src("NativeExpeditionCityPresentation.cs");var audio=Src("NativeExpeditionCityAudio.cs");var lit=Src("NativeLighting.cs");var adv=Src("NativeAdvancedLighting.cs");
            Check(city.Contains("townScene=NativeCityFxScene.Build(blocks,townKit,townGenerated?townGenFires??Array.Empty<NativeCityBackdropFire>():townFires,look,townHub,map.bounds);townScene.KeepNearest=townGenerated;"),"the city's presentation sizes the fog's grid from the map's bounds and keeps the nearest sheets on the generated map");
            Check(city.Contains("NativeExpeditionCityScenery.Around(out townGenFires,map.bounds.minX,map.bounds.maxX,map.bounds.minZ,map.bounds.maxZ)"),"the runtime's skyline is round the map's own bounds (as the census counts it)");
            Check(audio.Contains("NativeExpeditionCityAmbience.Between(townTime,time,townCues,arena.MapId==NativeExpeditionCityKit.MapId)"),"the city's audio leaves the town's brazier and bus crackle off the generated map");
            Check(lit.Contains("public static MapLook Look(ContentMap map)=>NativeExpeditionCityLights.ForMap("),"the lighting draws the generated map's look with its own practicals");
            // The volume's candidates are taken once per map and run, even when a map hands none, so its key is not built every frame.
            Check(adv.Contains("if(map==null||map.id!=mapId||candidatesMap==mapId&&candidatesRun==run&&(candidates.Count>0||lights==null||lights.Count==0))return;"),"the light volume takes a map's candidates once per map and run, an empty hand included (no key built per frame)");
        }

        // ---- W0 (its review): the effects shader decodes each piece's mode and flags as sent. They are whole numbers on every vertex,
        // but the interpolator returns them within a rounding error; an authored premultiplied piece's 2 came back 1.9999998 on about
        // half the pixels of a flat fog sheet at street level, read as additive there, so the sheet vanished in a crawling pattern ----
        private static void FxFlags(string root)
        {
            var shader=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativeSirenFx.shader"));
            Check(shader.Contains("float mode=round(i.uv.z),flags=round(i.uv.w);bool additive=fmod(flags,2)>.5,authored=flags>1.5;"),"NativeSirenFx rounds the mode and flags before it decodes them");
            // The decode as the shader writes it (fmod keeps the dividend's sign, as HLSL's), raw and rounded, a float32 step off.
            static (bool add,bool authored) Decode(float f,bool round){if(round)f=MathF.Round(f);return (f%2>.5f,f>1.5f);}
            int wrong=0,raw=0;
            foreach(float sent in new[]{0f,1f,2f,3f})foreach(float off in new[]{-2.4e-7f,0f,2.4e-7f})
            {
                float got=sent+off*Math.Max(1,sent);var want=Decode(sent,false);if(Decode(got,true)!=want)wrong++;if(Decode(got,false)!=want)raw++;
            }
            Check(wrong==0&&raw>0,"every piece decodes as sent within a rounding error ("+raw+" of 12 raw decodes would flip, the authored premultiplied 2 among them)");
        }

        // ---- W0: the ground fog on the generated streets (Core NativeCityFxScene: the grid from the map's bounds, the floor
        // block no longer blanking it) ----
        private static void Fog(City c)
        {
            var map=c.World.Map;var b=map.bounds;var s=c.Scene;
            Check(s.OriginX<=b.minX&&s.OriginZ<=b.minZ&&s.OriginX+s.CellsX*NativeCityFxScene.Cell>=b.maxX&&s.OriginZ+s.CellsZ*NativeCityFxScene.Cell>=b.maxZ,"the fog's grid covers the generated map ("+s.OriginX+".."+(s.OriginX+s.CellsX*NativeCityFxScene.Cell)+" x "+s.OriginZ+".."+(s.OriginZ+s.CellsZ*NativeCityFxScene.Cell)+")");
            var floor=map.blocks.Single(x=>NativeExpeditionCityKit.Stem(x.id)=="gen-floor");Check(!NativeCityFxScene.BlocksFog(floor)&&map.blocks.Where(x=>x.id.Contains("-building-")).All(NativeCityFxScene.BlocksFog),"the floor keeps no fog out; the buildings do");
            // Every road cell takes fog but those under an instance's hall (the hall stands behind its entrance, over the road's end).
            var halls=map.blocks.Where(x=>NativeExpeditionCityKit.Stem(x.id).StartsWith("bldg-hall:",StringComparison.Ordinal)).ToArray();
            bool UnderHall(double x,double z)=>halls.Any(h=>Math.Abs(x-h.pos.x)<=h.size.x/2+NativeCityFxScene.Cell&&Math.Abs(z-h.pos.z)<=h.size.z/2+NativeCityFxScene.Cell);
            int open=0,roads=0;var shut=new List<string>();
            foreach(var cell in c.World.Blueprint.cells){if(!cell.road||UnderHall(cell.x*10,cell.z*10))continue;roads++;if(s.OpenAt(cell.x*10,cell.z*10)>0)open++;else shut.Add(cell.x+":"+cell.z);}
            Check(roads>80&&open==roads,"fog may lie on every generated street ("+open+" of "+roads+" road cells clear of the halls"+(shut.Count>0?"; shut: "+string.Join(", ",shut):"")+")");
            Check(s.OpenAt(0,70)>0&&s.OpenAt(0,-30)>0&&s.OpenAt(0,0)>0,"in the final court, on the apron and in the sanctuary");
            var fx=new NativeExpeditionCityFx();var sun=Sun();
            foreach(var e in new[]{(0f,1.7f,-30f),(0f,1.7f,70f)}){fx.Build(s,Time,e.Item1,e.Item2,e.Item3,1,0,0,0,1,0,sun,1,false);Check(fx.Fog>20,"ground fog lies round an eye at "+e.Item1+", "+e.Item3+" ("+fx.Fog+" sheets)");}
            // The town keeps today's grid (its bounds give x -122..122, z -122..62, the constants it had).
            var town=NativeCityFxScene.Build(Array.Empty<ContentBlock>(),null,null,null,null,new MapBounds{minX=NativeExpeditionCityKit.TownMinX,maxX=NativeExpeditionCityKit.TownMaxX,minZ=NativeExpeditionCityKit.TownMinZ,maxZ=NativeExpeditionCityKit.TownMaxZ});
            var none=NativeCityFxScene.Build(Array.Empty<ContentBlock>(),null,null,null,null,null);
            Check(town.OriginX==-122&&town.OriginZ==-122&&town.CellsX==122&&town.CellsZ==92&&none.OriginX==-122&&none.CellsX==122&&none.CellsZ==92,"the town's grid is today's (x -122..122, z -122..62)");
            FogBudget(c);
        }
        // ---- W0 (its review): a reduced budget (the low tier, the reduced effects) keeps the sheets nearest the eye, never a cut in
        // the lattice's scan order, which on the open hub dropped the far rows of the eye's disc (and swapped them as it moved) ----
        private static void FogBudget(City c)
        {
            var s=c.Scene;var fx=new NativeExpeditionCityFx();var sun=Sun();Check(s.KeepNearest,"the generated map's scene keeps the nearest sheets");
            int lowFog=NativeExpeditionCityFx.Count(NativeExpeditionCityFx.FogMax,1,true),lowPockets=NativeExpeditionCityFx.Count(NativeExpeditionCityFx.PocketMax,1,true);
            int redFog=NativeExpeditionCityFx.Count(NativeExpeditionCityFx.FogMax,.3,false),redPockets=NativeExpeditionCityFx.Count(NativeExpeditionCityFx.PocketMax,.3,false);
            double reach=Math.Max(NativeExpeditionCityFx.NearRadius(NativeExpeditionCityFx.FogRadius,lowFog,NativeExpeditionCityFx.FogMax,NativeExpeditionCityFx.FogSpare),NativeExpeditionCityFx.NearRadius(NativeExpeditionCityFx.PocketRadius,lowPockets,NativeExpeditionCityFx.PocketMax,NativeExpeditionCityFx.PocketSpare))+2;
            // The fog's sheets and pockets: the batch's flat premultiplied quads (the pools and the searchlight's spot are additive).
            List<(double x,double z)> Sheets(){var l=new List<(double,double)>();var q=fx.Quads;for(int i=0;i<q.Count;i++){int o=i*12;if(q.Modes[i]!=1||q.Additive[i]!=0||q.Layers[i]!=1||Math.Abs(q.Corners[o+1]-q.Corners[o+4])>1e-4f||Math.Abs(q.Corners[o+1]-q.Corners[o+7])>1e-4f)continue;
                l.Add(((q.Corners[o]+q.Corners[o+3]+q.Corners[o+6]+q.Corners[o+9])/4.0,(q.Corners[o+2]+q.Corners[o+5]+q.Corners[o+8]+q.Corners[o+11])/4.0));}return l;}
            int eyes=0,mostFog=0,mostPockets=0,mostRed=0;double far=0;
            for(int z=-90;z<=90;z+=5)for(int x=-120;x<=120;x+=5)
            {
                if(s.OpenAt(x,z)==0)continue;eyes++;
                fx.Build(s,Time,x,1.7,z,1,0,0,0,1,0,sun,1,true);mostFog=Math.Max(mostFog,fx.Fog);mostPockets=Math.Max(mostPockets,fx.Pockets);
                foreach(var p in Sheets())far=Math.Max(far,Math.Sqrt((p.x-x)*(p.x-x)+(p.z-z)*(p.z-z)));
                fx.Build(s,Time,x,1.7,z,1,0,0,0,1,0,sun,.3,false);mostRed=Math.Max(mostRed,Math.Max(fx.Fog*100/redFog,fx.Pockets*100/redPockets));
            }
            Check(eyes>250&&mostFog<lowFog&&mostPockets<lowPockets,"on the low tier no eye's fog reaches its cap ("+eyes+" eyes: at most "+mostFog+" sheets of "+lowFog+", "+mostPockets+" pockets of "+lowPockets+")");
            Check(far<=reach,"the low tier's sheets are the nearest ("+far.ToString("0.0")+" m out at most, its disc "+reach.ToString("0.0")+" m)");
            Check(mostRed<100,"nor with the reduced effects (at most "+mostRed+"% of the cap)");
            // The full budget draws the full disc as before.
            foreach(var e in new[]{(0.0,0.0),(0.0,-30.0),(0.0,70.0)})
            {
                fx.Build(s,Time,e.Item1,1.7,e.Item2,1,0,0,0,1,0,sun,1,false);int keep=fx.Fog+fx.Pockets;s.KeepNearest=false;fx.Build(s,Time,e.Item1,1.7,e.Item2,1,0,0,0,1,0,sun,1,false);int was=fx.Fog+fx.Pockets;s.KeepNearest=true;
                Check(keep==was&&keep>0,"the High tier's fog at "+e.Item1+", "+e.Item2+" is the full disc ("+keep+" sheets)");
            }
            // At the sanctuary's centre on the low tier the sheets lie all round the eye (the cut kept a third north of it, two thirds south).
            fx.Build(s,Time,0,1.7,0,1,0,0,0,1,0,sun,1,true);var all=Sheets();int north=all.Count(p=>p.z>0),south=all.Count-north;
            Check(all.Count>=20&&north>=all.Count*.4&&south>=all.Count*.4,"the low tier's fog at the sanctuary lies all round the eye ("+north+" north, "+south+" south)");
        }
        // ---- W0: the generated map's own practicals (no brazier among the pods) ----
        private static void Practicals(Setup set,City c)
        {
            var gen=NativeExpeditionCityLights.ForMap(set.Look,NativeExpeditionGeneratedWorld.MapId);
            Check(gen!=set.Look&&gen.lights.Length==NativeExpeditionCityLights.GeneratedPracticals.Length&&gen.fog==set.Look.fog&&gen.key==set.Look.key&&gen.advanced==set.Look.advanced&&gen.toon==set.Look.toon&&ReferenceEquals(gen,NativeExpeditionCityLights.ForMap(set.Look,NativeExpeditionGeneratedWorld.MapId)),"the generated map draws the town's look with practicals of its own (one copy)");
            Check(ReferenceEquals(NativeExpeditionCityLights.ForMap(set.Look,NativeExpeditionCityKit.MapId),set.Look)&&set.Look.lights.Length==8&&set.Look.lights.Any(l=>l.id=="sanctuary-brazier"),"the town keeps its eight, the brazier among them");
            Check(!gen.lights.Any(l=>Math.Abs(l.pos.x)<25&&Math.Abs(l.pos.z)<25),"no practical stands inside the generated sanctuary");
            Check(c.Lights.Candidates.All(l=>l.Kind!=255),"the generated map's picker takes no town practical");
            Check(!c.Lights.Candidates.Any(l=>l.X*l.X+l.Z*l.Z<20*20),"no light source among the pods (none within 20 m of the sanctuary's centre)");
            // The fence's own gate-post lamps stand at its line (24 m out); nothing else glows over the plaza (the town's two lamps hung halos at 9.5 m).
            Check(!c.Scene.Halos.Any(h=>Math.Abs(h.X)<23&&Math.Abs(h.Z)<23)&&!c.Scene.Fires.Any(f=>Math.Abs(f.X)<24.8&&Math.Abs(f.Z)<24.8),"no halo or fire hangs over the generated plaza");
            var cues=new NativeCityCue[64];int crackles=0,distant=0;
            for(double t=10;t<190;t+=4){int n=NativeExpeditionCityAmbience.Between(t,t+4,cues,false);for(int i=0;i<n;i++)if(cues[i].Kind==NativeExpeditionCityAmbience.Crackle)crackles++;else distant++;}
            Check(crackles==0&&distant>20,"the generated map hears its distant shots and booms but no brazier ("+distant+" cues in 180 s)");
        }
        // ---- W0: the kit's hash mixes in the run's world seed ----
        private static void Variety(Setup set)
        {
            // The town's hash is the one it had (seed 0; the values the shared head gave).
            Check(Math.Abs(NativeExpeditionCityKit.Hash("expedition-city-bldg-ne-shops",0,0,11)-0.7072922820225358)<1e-12&&Math.Abs(NativeExpeditionCityKit.Hash("expedition-generated-city-building-3:-2",0,7,3)-0.4437326565384865)<1e-12&&Math.Abs(NativeExpeditionCityKit.Hash("tv",0,41,250)-0.11251510167494416)<1e-12,"seed 0 is the town's hash, unchanged");
            Check(NativeExpeditionCityKit.SeedOf(null)==0&&NativeExpeditionCityKit.Build(Array.Empty<ContentBlock>(),null,null,null).Seed==0,"the town's kit mixes in no seed");
            // Two rolls whose cells stand at the same heights (a cell's height is its hash plus the world seed, mod 4), so the same cell
            // holds the same building in both.
            var a=set.World(1);ExpeditionGeneratedWorld b=null;for(uint seed=2;seed<80&&b==null;seed++){var w=set.World(seed);if(w!=null&&w.Blueprint.worldSeed!=a.Blueprint.worldSeed&&(w.Blueprint.worldSeed-a.Blueprint.worldSeed)%4==0)b=w;}
            Check(b!=null,"a second roll with the first's heights");Check(a.Blueprint.worldSeed!=b.Blueprint.worldSeed&&NativeExpeditionCityKit.SeedOf(a.Blueprint)!=NativeExpeditionCityKit.SeedOf(b.Blueprint)&&NativeExpeditionCityKit.SeedOf(a.Blueprint)!=0,"two rolls, two kit seeds");
            var ka=NativeExpeditionCityKit.Build(a.Map.blocks,a.Area,a.Hub,a.Map.bounds,true,a.Blueprint);var kb=NativeExpeditionCityKit.Build(b.Map.blocks,b.Area,b.Hub,b.Map.bounds,true,b.Blueprint);
            Check(ka.Seed==NativeExpeditionCityKit.SeedOf(a.Blueprint)&&kb.Seed==NativeExpeditionCityKit.SeedOf(b.Blueprint),"each roll's kit takes its run's seed");
            // The same cell's building, the same height in both rolls: its dressing differs (it repeated exactly before).
            string Sig(NativeExpeditionCityKit k,int i)=>string.Join(";",k.Parts.Where(p=>p.Parent==i).Select(p=>p.Look+","+p.Color+","+p.Flags+","+p.Drive+","+Math.Round(p.X,2)+","+Math.Round(p.Y,2)+","+Math.Round(p.Z,2)));
            int same=0,differ=0;
            for(int i=0;i<a.Map.blocks.Length;i++)
            {
                var x=a.Map.blocks[i];if(!x.id.Contains("-building-"))continue;int j=Array.FindIndex(b.Map.blocks,y=>y.id==x.id);if(j<0||b.Map.blocks[j].size.y!=x.size.y||b.Map.blocks[j].pos.x!=x.pos.x||b.Map.blocks[j].pos.z!=x.pos.z)continue;
                if(Sig(ka,i)==Sig(kb,j))same++;else differ++;
            }
            Console.WriteLine("   the kit's variety: of the buildings that recur at one height in rolls 1 and "+b.Blueprint.seed+", "+differ+" of "+(same+differ)+" dress anew (before W0 a sixth repeated exactly: 59 of 342 for rolls 4242 and 4243 on the shared head)");
            Check(differ>0&&differ>=(same+differ)*.98,"a building that recurs from roll to roll dresses anew ("+differ+" of "+(same+differ)+" shared cells differ)");
        }
        // ---- W0: the light volume's candidates by importance and spread (Core NativeLightVolume.Pick) ----
        private static void Volume(Setup set,City c)
        {
            // A synthetic street of lamps listed west to east: the pick reaches the east, takes each place once, and repeats itself.
            var pool=new List<NativeVolumeLight>();for(int i=0;i<200;i++)pool.Add(new NativeVolumeLight(-100+i,4.4f,(i%2)*8,11,.9f,.6f,.3f));pool.Add(pool[10]);
            var pick=new List<NativeVolumeLight>();NativeLightVolume.Pick(pool,40,pick);var again=new List<NativeVolumeLight>();NativeLightVolume.Pick(pool,40,again);
            Check(pick.Count==40&&pick.SequenceEqual(again)&&pick.Distinct().Count()==40&&pick.Min(l=>l.X)<-90&&pick.Max(l=>l.X)>90,"the pick spreads over the whole street (x "+pick.Min(l=>l.X)+".."+pick.Max(l=>l.X)+"), each place once, the same every time");
            pool.Insert(0,new NativeVolumeLight(50,6,0,30,3,2.4f,1.6f));NativeLightVolume.Pick(pool,40,pick);Check(pick[0].X==50&&pick[0].Range==30,"the strongest light goes first");
            NativeLightVolume.Pick(pool,0,pick);Check(pick.Count==0,"no room, no pick");
            // A generated roll's bake, its kit's lights alone (no key, no ambient, none of the town's practicals, which W0 takes off
            // the generated map): before W0 the bake took the kit's first sources in build order (the first 48 after the town's eight,
            // twice), the westernmost; now the kit's 64 by importance and spread. The east and the south now take the lamps' bounce.
            var map=c.World.Map;var townPicker=NativeExpeditionCityLights.Build(c.Kit,set.Look);
            var old=townPicker.Candidates.Take(NativeLightVolume.MaxLights-NativeMapLooks.MaxLights).Where(l=>l.Kind!=255).Select(l=>new NativeVolumeLight(l.X,l.Y,l.Z,l.Range,l.R,l.G,l.B)).ToList();
            var fresh=new List<NativeVolumeLight>();NativeLightVolume.Pick(c.Lights.Candidates.Where(l=>l.Kind!=255).Select(l=>new NativeVolumeLight(l.X,l.Y,l.Z,l.Range,l.R,l.G,l.B)).ToList(),NativeLightVolume.MaxLights,fresh,0);
            Check(fresh.Count==NativeLightVolume.MaxLights&&fresh.Max(l=>l.X)>60&&fresh.Min(l=>l.Z)<-40&&fresh.Min(l=>l.X)<-60,"the generated city's 64 reach its east, west and south (x "+fresh.Min(l=>l.X).ToString("0")+".."+fresh.Max(l=>l.X).ToString("0")+", z from "+fresh.Min(l=>l.Z).ToString("0")+")");
            var halls=map.blocks.Where(x=>NativeExpeditionCityKit.Stem(x.id).StartsWith("bldg-hall:",StringComparison.Ordinal)).ToArray();
            var streets=c.World.Blueprint.cells.Where(x=>x.road&&!halls.Any(h=>Math.Abs(x.x*10-h.pos.x)<=h.size.x/2&&Math.Abs(x.z*10-h.pos.z)<=h.size.z/2)).Select(x=>(x:x.x*10.0,z:x.z*10.0)).ToArray();
            double Reached(List<NativeVolumeLight> list,Func<double,double,bool> where){var cells=streets.Where(s=>where(s.x,s.z)).ToArray();return cells.Length==0?0:cells.Count(s=>list.Any(l=>(l.X-s.x)*(l.X-s.x)+(l.Z-s.z)*(l.Z-s.z)<l.Range*l.Range))/(double)cells.Length;}
            NativeLightVolume Bake(List<NativeVolumeLight> extra){var input=NativeLightVolume.From(map,c.Look,_=>0x8A9496,extra,1,12);input.KeyR=input.KeyG=input.KeyB=0;Array.Clear(input.Ambient,0,9);return NativeLightVolume.Build(input);}
            var before=Bake(old);var after=Bake(fresh);
            double Lit(NativeLightVolume v,Func<double,double,bool> where)
            {
                double sum=0;int n=0;foreach(var s in streets){if(!where(s.x,s.z))continue;v.Sample((float)s.x,.6f,(float)s.z,0,1,0,out float r,out float g,out float bl,out _);sum+=r*.3+g*.55+bl*.15;n++;}
                return n>0?sum/n:0;
            }
            Func<double,double,bool> east=(x,z)=>x>40,south=(x,z)=>z<-35,west=(x,z)=>x<-40;
            Console.WriteLine("   the light volume's kit lights (before W0 / now): "+old.Count+" / "+fresh.Count+"; streets within a baked lamp's reach: east "+Reached(old,east).ToString("P0")+" / "+Reached(fresh,east).ToString("P0")+", south "+Reached(old,south).ToString("P0")+" / "+Reached(fresh,south).ToString("P0")+", west "+Reached(old,west).ToString("P0")+" / "+Reached(fresh,west).ToString("P0")
                +"; their bounce at 0.6 m: east "+Lit(before,east).ToString("0.0000")+" / "+Lit(after,east).ToString("0.0000")+", south "+Lit(before,south).ToString("0.0000")+" / "+Lit(after,south).ToString("0.0000")+", west "+Lit(before,west).ToString("0.0000")+" / "+Lit(after,west).ToString("0.0000"));
            // Where the 64 go (W0's review, for W1 and W10: the pick weighs a light by its range squared, so the halls' floods and
            // the final court's ring take many, and the south arm's middle stays unbaked; noted, not changed in W0).
            Console.WriteLine("   the light volume's 64 by area: hub "+fresh.Count(l=>Math.Abs(l.X)<25&&Math.Abs(l.Z)<25)+", final court "+fresh.Count(l=>l.Z>50)+", east "+fresh.Count(l=>l.X>40&&l.Z<=50)+", west "+fresh.Count(l=>l.X<-40&&l.Z<=50)+", south "+fresh.Count(l=>l.Z<-35&&Math.Abs(l.X)<=40)+", elsewhere "+fresh.Count(l=>!(Math.Abs(l.X)<25&&Math.Abs(l.Z)<25)&&!(l.Z>50)&&!(l.X>40&&l.Z<=50)&&!(l.X<-40&&l.Z<=50)&&!(l.Z<-35&&Math.Abs(l.X)<=40)));
            Check(Reached(fresh,east)>Reached(old,east)+.2&&Reached(fresh,south)>Reached(old,south)+.2&&Lit(after,east)>Lit(before,east)*2&&Lit(after,south)>Lit(before,south)*2,"the east and south streets take the lamps' bounce now");
            Check(after.Lights==NativeLightVolume.MaxLights,"the bake takes the most lights");
        }
    }
}
