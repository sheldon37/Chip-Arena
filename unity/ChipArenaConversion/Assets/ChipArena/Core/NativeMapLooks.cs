using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Resources/MapLooks.json: one look per map id (presentation only; never part of the network frame). `preview`: block
    /// looks for the dev preview only (NativeArenaDressing.Preview, the `--ca-dress-blocks` command line), which dresses
    /// every map; a map's own blockLooks win over it. No map's look changes without that flag.
    /// </summary>
    [Serializable] public sealed class MapLookSet {public int schema;public MapLook[] maps;public MapLookBlock[] preview;}
    /// <summary>
    /// One map's look. `id` is the map id it applies to; `look` names the preset (unique; "golden-demolition" for Brickfall).
    /// Colours are sRGB ints (0xRRGGBB) and vectors {x,y,z} in the Unity world frame, as in GameContent.json; JsonUtility reads
    /// a missing field as 0, "" or an empty object, so every entry writes every field.
    /// <list type="bullet">
    /// <item>`farClip`, `background`: the camera's far plane and background colour (seen only when `sky.variant` is "none").</item>
    /// <item>`key`, `rim`: the two directional lights NativeLighting.Create made (the key casts the shadows; it is
    /// RenderSettings.sun). A light sits at `from` and looks at the origin when `from` is non-zero; otherwise it shines from
    /// `elevation` degrees above the horizon at compass `bearing` (degrees from +z toward +x, so 322 is north-west).
    /// A rim with neither keeps the rig's own direction (Create's, which the draft stage turns; today's behaviour).
    /// `shadows` is "none", "soft" or "hard"; `shadowStrength` 0..1.</item>
    /// <item>`ambient`: the trilight; each colour is scaled by its factor in sRGB before Unity linearises it.</item>
    /// <item>`fog`: `mode` "linear", "exp", "exp2" or "none"; the colour is `color`, or with `skyLerp` > 0 the map's own sky
    /// colour lerped that far toward `color` (today's outdoor fog); `start`/`end` for linear, `density` for exp/exp2.</item>
    /// <item>`shadows`: cascades and distance, and the fallback pair used on the low graphics tier (performance, or auto on
    /// low-end hardware, where NativeSettings also turns shadows off today).</item>
    /// <item>`sky`: `variant` "night" or "dusk" (both draw NativeNightSky) or "none" (sealed interiors: solid background, no
    /// skybox); `horizon`, `zenith`; the sun or moon disc's direction (`dir` when non-zero, else `elevation`/`bearing`),
    /// `discSize`, colour `disc` and `crater` shade; the halo around it, colour `glow` and e-fold size `halo`; `stars`
    /// (0..1); the lit cloud tint `cloud` and drift `cloudSpeed`. A `glow`, `disc` or `cloud` of 0 keeps the shader's
    /// default, which is today's night constant.</item>
    /// <item>`lights`: up to 8 practical point lights (`pos`, sRGB `color` times `intensity` in linear light, `range`) for
    /// NativeBlockLit's own light block, which reads the `_ArenaLightPos/_ArenaLightColor` globals NativeLighting writes
    /// (unused slots are black), so only dressed blocks take them. `flicker` "" (steady), "fire" (three
    /// sines at `rate`, 1.5x and 1.23x it, dipping by up to `depth`) or "stutter" (two dips of `depth` inside the first
    /// `length` seconds of every `period`); `phase` offsets the clock in seconds.</item>
    /// <item>`blocks`: `dressed` opts the map's blocks into NativeArenaDressing (instanced bevelled blocks through
    /// NativeBlockLit.shader; the authority cube renderers are hidden); missing or false keeps today's renderers. `dust` is
    /// a map-wide dust coat (0..1) under the per-block coat, `haze` the ground haze's opacity at the street (0..1), falling
    /// to 0 at `hazeTop` m, in `hazeColor`.</item>
    /// <item>`blockLooks` (read by NativeArenaDressing, Core/NativeBlockLooks.cs): looks for sim blocks only (2.1 rule 5: decor
    /// never goes into sim data). `block` is an exact block id, a glob ("*" matches any run: "hotel-pier-*"; the first
    /// matching glob in list order wins, so specific ones go first) or "material:brick" (used when no id or glob matches);
    /// `look` a NativeBlockLooks key ("brick-red"); `color` the base colour (0: the look's 3.10 palette colour); `band` paints
    /// the 2.3 load-bearing chevron band and its DANGER tag, fitted to the block's height (NativeBlockLooks.BandFit: 1.0-1.6 m
    /// above its base on blocks 1.9 m tall or more); `note` is for people (the 3.3 legend element it covers). Brickfall's
    /// entries expect the grey-box to keep the sim id stems of docs/arenas/scripts/brickfall_m1.py and ARENA_PROPOSAL.md 5.2:
    /// members (hotel-pier-nw/-ne/-sw/-se, ashby-/bray-pier-F1..R2, stack-*-base*, bridge-*-bearing*; banded), masonry
    /// panels and shells (hotel-, ashby-, bray-, stack-, bridge-), floor and roof plates (hotel-floor-, -roof), the roof stair
    /// house, ramps (-ramp), the street slab (-slab), cover (barricade-, pallet-, sandbag-, stall-, phonebox-, monument-),
    /// static blocks (frontage-, the cores -core, kessels, dunmores, market-, kiosk-, news-kiosk-, storeroom-, tram-shelter-,
    /// dock-, truck-, the tenement floors ashby-/bray-floor-) and rubble sets (-rubble-). Anything else falls back to its
    /// material's look. Entries whose note ends "[static]" cover blocks that 3.9 and 5.3 make `static`; those leave
    /// arena.Props at 6.1 step 2, before the grey-box, so the entries apply only once NativeArenaDressing draws Astra's static
    /// block list (not built yet; until then those blocks keep their plain renderers).</item>
    /// <item>`kitLooks`: looks for NativeDressingKits' parts (2.2 "Set dressing"; not built yet, so nothing reads this list),
    /// keyed the same way by kit part id. These are presentation only and never grey-box blocks: windows are open holes in
    /// the sim walls (3.10) and their glazing, boards, sills, lintels, string courses, cornices, quoins, coping, arches,
    /// shutters, canopies, signage and neon, and the lane, yard and square paving painted on the slab are kit parts attached
    /// to a block id. A kit entry has no `band` and no "material:" pattern.</item>
    /// <item>Stubs the later passes fill; nothing reads them yet: `dressing` (articulation kit id), `backdrop`, `atmosphere`
    /// and `ambience` ids. Empty means today's.</item>
    /// </list>
    /// A map with no entry gets <see cref="NativeMapLooks.Fallback"/>, which is today's look worked out from the map itself.
    /// </summary>
    [Serializable] public sealed class MapLook
    {
        public string id,look;public float farClip;public int background;public MapLookLight key,rim;public MapLookAmbient ambient;public MapLookFog fog;
        public MapLookShadows shadows;public MapLookSky sky;public MapLookPractical[] lights;public MapLookBlock[] blockLooks,kitLooks;public string dressing,backdrop,atmosphere,ambience;public MapLookBlocks blocks;
        /// <summary>The anime map style (Core NativeMapStyle; docs/MAP-STYLE-ANIME.md): missing or `on` false is the legacy look.</summary>
        public MapLookToon toon;
    }
    [Serializable] public sealed class MapLookLight {public int color;public float intensity,elevation,bearing,shadowStrength;public V3 from;public string shadows;}
    [Serializable] public sealed class MapLookAmbient {public int sky,equator,ground;public float skyScale,equatorScale,groundScale;}
    [Serializable] public sealed class MapLookFog {public string mode;public int color;public float skyLerp,start,end,density;}
    [Serializable] public sealed class MapLookShadows {public int cascades,fallbackCascades;public float distance,fallbackDistance;}
    [Serializable] public sealed class MapLookSky {public string variant;public int horizon,zenith,glow,disc,cloud;public float discSize,halo,stars,crater,cloudSpeed,elevation,bearing;public V3 dir;}
    [Serializable] public sealed class MapLookPractical {public string id,flicker;public V3 pos;public int color;public float intensity,range,rate,period,length,depth,phase;}
    [Serializable] public sealed class MapLookBlock {public string block,look,note;public int color;public bool band;}
    [Serializable] public sealed class MapLookBlocks {public bool dressed;public float dust,haze,hazeTop;public int hazeColor;}

    /// <summary>A UnityEngine.Color's four floats as Color holds them (display values; Unity linearises them in Linear space),
    /// with the Color maths NativeLighting used before this moved into Core (ColorOf, Lerp with t clamped, scaling with the
    /// alpha), so the values Unity receives are the same bit for bit.</summary>
    public struct MapLookColor
    {
        public float r,g,b,a;
        public static MapLookColor Of(int hex)=>new MapLookColor{r=((hex>>16)&255)/255f,g=((hex>>8)&255)/255f,b=(hex&255)/255f,a=1};
        public static MapLookColor Of(float r,float g,float b)=>new MapLookColor{r=r,g=g,b=b,a=1};
        public static MapLookColor Lerp(MapLookColor x,MapLookColor y,float t){t=t<0?0:t>1?1:t;return new MapLookColor{r=x.r+(y.r-x.r)*t,g=x.g+(y.g-x.g)*t,b=x.b+(y.b-x.b)*t,a=x.a+(y.a-x.a)*t};}
        public MapLookColor Scaled(float k)=>new MapLookColor{r=r*k,g=g*k,b=b*k,a=a*k};
    }
    /// <summary>Unity's fog modes by their UnityEngine.FogMode values.</summary>
    public enum MapFogMode{Linear=1,Exponential=2,ExponentialSquared=3}
    /// <summary>RenderSettings' fog as NativeLighting writes it: on, mode, colour, the linear start and end, and the density,
    /// written only when `WritesDensity` (Apply writes it for exp and exp2 only; a stage's restore writes the one it found).</summary>
    public struct MapFogState{public bool On,WritesDensity;public MapFogMode Mode;public MapLookColor Color;public float Start,End,Density;}
    /// <summary>A light's aim as NativeLighting applies it: none (`Has` false: a rim that keeps the rig's direction), a point
    /// it looks at the origin from (`From`), or the unit direction toward the light (it sits 24 m out along it, looking back).</summary>
    public struct MapLookAim{public bool Has,From;public float X,Y,Z;}
    /// <summary>A sky tint: `Hex` 0 keeps the shader default (R, G, B, already linear); otherwise NativeLighting linearises Hex
    /// in the active colour space.</summary>
    public struct MapLookTint{public int Hex;public float R,G,B;}
    /// <summary>Everything NativeLighting.Apply writes for a look (<see cref="NativeMapLooks.Applied"/>): the camera, the fog,
    /// NativeNightSky's properties, the shadow settings on both graphics tiers, the key and rim lights, the trilight ambient.</summary>
    public sealed class MapLookState
    {
        public float FarClip;public MapLookColor Background;public bool Skybox;public MapFogState Fog;
        public MapLookColor Horizon,Zenith;public float MoonSize,SunX,SunY,SunZ,DiscBase,Crater,HaloFalloff,Stars,CloudSpeed;public MapLookTint Disc,Halo,Cloud;
        public int Cascades,FallbackCascades;public float ShadowDistance,FallbackDistance;
        public MapLookColor KeyColor,RimColor;public MapLookAim KeyAim,RimAim;public string KeyShadows,RimShadows;public float KeyStrength,RimStrength,KeyIntensity,RimIntensity;
        /// <summary>A dressed map keeps the rim per-pixel (NativeBlockLit adds its own rim term; an SH rim would count twice).</summary>
        public bool RimPixel;
        public MapLookColor AmbientSky,AmbientEquator,AmbientGround;
        public int ShadowCascades(bool low)=>low?FallbackCascades:Cascades;
        public float Shadows(bool low)=>low?FallbackDistance:ShadowDistance;
    }

    /// <summary>
    /// Loader rules for Resources/MapLooks.json: lookup, today's fallback, validation, light and sky directions, the sky
    /// shader's derived values, the practical lights' flicker and fall-off, the look-to-applied-values mapping NativeLighting.Apply
    /// and Tests/MapLookChecks both call (<see cref="Applied"/>), and the fog and shadows the entrance and draft stages own.
    /// Pure; Applied, Flicker and Attenuation allocate nothing.
    /// </summary>
    public static class NativeMapLooks
    {
        public const int Schema=1,MaxLights=8;
        /// <summary>NativeLighting.Create's rig: the key at (-8, 20, -12) and the rim at (14, 7, 12), both looking at the origin
        /// (a rim with no direction of its own keeps the rig's, or the draft stage's turn of it).</summary>
        public static readonly float[] RigKey={-8,20,-12},RigRim={14,7,12};
        /// <summary>
        /// What NativeLighting.Apply writes for `look` on a map whose own sky colour is `mapSky` (the base of a skyLerp fog), with
        /// the dev dressing preview (`dressPreview`: NativeArenaDressing.Preview) on or off, into `into`: the camera's far plane,
        /// background and clear (a "none" sky is a solid background), the fog (a "none" mode is fog off in Linear mode; the
        /// density only for exp and exp2), the sky's properties (tints of 0 keep the shader defaults), both tiers' shadows, the
        /// key (always aimed) and rim (aimed only with a direction of its own; per-pixel on a dressed map) with their colours,
        /// shadow modes, strengths and intensities, and the trilight ambient (each colour times its scale before Unity
        /// linearises it). The one mapping: Tests/MapLookChecks compares its output for the 15 old maps to the ce3c506 oracle and
        /// fixture bit for bit.
        /// </summary>
        public static void Applied(MapLook look,int mapSky,bool dressPreview,MapLookState into)
        {
            var s=into;var f=look.fog;var k=look.sky;var sh=look.shadows;var a=look.ambient;
            s.FarClip=look.farClip;s.Background=MapLookColor.Of(look.background);s.Skybox=k.variant!="none";
            var mode=f.mode=="exp"?MapFogMode.Exponential:f.mode=="exp2"?MapFogMode.ExponentialSquared:MapFogMode.Linear;
            s.Fog=new MapFogState{On=f.mode!="none",Mode=mode,WritesDensity=mode!=MapFogMode.Linear,Density=f.density,Color=f.skyLerp>0?MapLookColor.Lerp(MapLookColor.Of(mapSky),MapLookColor.Of(f.color),f.skyLerp):MapLookColor.Of(f.color),Start=f.start,End=f.end};
            s.Horizon=MapLookColor.Of(k.horizon);s.Zenith=MapLookColor.Of(k.zenith);s.MoonSize=k.discSize;SunDirection(k,out s.SunX,out s.SunY,out s.SunZ);
            s.Disc=Tint(k.disc,DiscDefault);s.DiscBase=DiscBase(k);s.Crater=k.crater;s.Halo=Tint(k.glow,HaloDefault);s.HaloFalloff=HaloFalloff(k);s.Stars=k.stars;s.Cloud=Tint(k.cloud,CloudDefault);s.CloudSpeed=k.cloudSpeed;
            s.Cascades=sh.cascades;s.ShadowDistance=sh.distance;s.FallbackCascades=sh.fallbackCascades;s.FallbackDistance=sh.fallbackDistance;
            s.KeyColor=MapLookColor.Of(look.key.color);s.KeyAim=Aim(look.key);s.KeyShadows=look.key.shadows;s.KeyStrength=look.key.shadowStrength;s.KeyIntensity=look.key.intensity;
            s.RimColor=MapLookColor.Of(look.rim.color);s.RimAim=HasDirection(look.rim)?Aim(look.rim):default;s.RimShadows=look.rim.shadows;s.RimStrength=look.rim.shadowStrength;s.RimIntensity=look.rim.intensity;
            s.RimPixel=NativeBlockLooks.Dressed(look,dressPreview);
            s.AmbientSky=MapLookColor.Of(a.sky).Scaled(a.skyScale);s.AmbientEquator=MapLookColor.Of(a.equator).Scaled(a.equatorScale);s.AmbientGround=MapLookColor.Of(a.ground).Scaled(a.groundScale);
        }
        private static MapLookTint Tint(int hex,float[] fallback)=>hex==0?new MapLookTint{R=fallback[0],G=fallback[1],B=fallback[2]}:new MapLookTint{Hex=hex};
        // A light at `from` looking at the origin (today's rig), or shining from its elevation and bearing (float casts as Apply made them).
        private static MapLookAim Aim(MapLookLight l)
        {
            if(HasFrom(l))return new MapLookAim{Has=true,From=true,X=l.from.x,Y=l.from.y,Z=l.from.z};
            Toward(l.elevation,l.bearing,out double x,out double y,out double z);return new MapLookAim{Has=true,X=(float)x,Y=(float)y,Z=(float)z};
        }
        /// <summary>
        /// The fog the entrance and draft stages own while they hold the screen (NativeLighting.Intro and Draft write it every
        /// frame; EndIntro gives back the fog it found, then Apply the map's): on, Linear (three's Fog, and the only fog variant
        /// the builds keep, Editor/PrototypeBuild), the stage's colour and range, the density left alone. Every old map's look is
        /// linear fog that is on, so its entrances and draft see what they saw before; a map's own fog (Brickfall's) never
        /// reaches them.
        /// </summary>
        public static MapFogState StageFog(MapLookColor color,float start,float end)=>new MapFogState{On=true,Mode=MapFogMode.Linear,Color=color,Start=start,End=end};
        /// <summary>The chip draft's backdrop (its camera background) and its fog, 30 to 100 m into that backdrop, as draft-stage.ts
        /// fogs to its own background (its pilot and tray sit within 4 m of the camera, so nothing it draws reaches the fog).</summary>
        public static readonly MapLookColor DraftBackground=MapLookColor.Of(.025f,.032f,.055f);
        public const float DraftFogStart=30,DraftFogEnd=100;
        public static MapFogState DraftFog=>StageFog(DraftBackground,DraftFogStart,DraftFogEnd);
        /// <summary>The stages' shadows: the old rig's 2 cascades over 65 m and, in the draft, a soft key at full strength. Every
        /// old map's look has exactly these on both tiers, so pinning them changes nothing there, and a map's own (Brickfall's 4
        /// cascades over 75 m and a .85 key) stays out of the entrances and the draft.</summary>
        public const int StageCascades=2;public const float StageShadowDistance=65,StageShadowStrength=1;public const string StageKeyShadows="soft";
        /// <summary>NativeNightSky.shader's property defaults: the constants the night sky hard-coded before it had properties.</summary>
        public static readonly float[] SunDirDefault={-.4f,.46f,-.78f},DiscDefault={.79f,.86f,1f},HaloDefault={.16f,.21f,.3f},CloudDefault={.24f,.29f,.40f};
        public const float HaloFalloffDefault=12,DiscBaseDefault=.76f,CraterDefault=.24f,StarsDefault=1,CloudSpeedDefault=1,DiscSizeDefault=.028f;
        /// <summary>The night halo's e-fold size, 1/12; HaloFalloff turns it back into exactly 12.</summary>
        public const float NightHalo=1f/12;
        public static MapLook Find(MapLookSet set,string mapId){if(set?.maps==null)return null;foreach(var m in set.maps)if(m!=null&&m.id==mapId)return m;return null;}
        public static MapLook ByLook(MapLookSet set,string look){if(set?.maps==null)return null;foreach(var m in set.maps)if(m!=null&&m.look==look)return m;return null;}
        /// <summary>The map's entry, or today's look worked out from its id, theme and colours.</summary>
        public static MapLook Resolve(MapLookSet set,string id,string theme,int sky,int accent)=>Find(set,id)??Fallback(id,theme,sky,accent);
        /// <summary>
        /// Today's look for any map (NativeLighting before this file): the moonlit night rig, Ward Zero sealed. The volcanic
        /// sky follows the forge theme, as night-world.ts does in the Electron client (it was keyed on the id "forge", so
        /// Cinder Crossing, also forge-themed, had the plain night sky there); the moon colour stays keyed on the id, as
        /// map-scenery.ts keys it.
        /// </summary>
        public static MapLook Fallback(string id,string theme,int sky,int accent)
        {
            bool indoor=id=="siren",cold=theme=="moonlit"||theme=="snow",volcanic=theme=="forge";
            return new MapLook{id=id,look="night-"+id,farClip=350,background=indoor?0x050407:sky,
                key=new MapLookLight{color=id=="outpost"?0xffc5a5:id=="forge"?0xffac9c:0xb2ceff,intensity=.9f,from=indoor?new V3(-3,30,-5):new V3(-8,20,-12),shadows="soft",shadowStrength=1},
                rim=new MapLookLight{color=accent,intensity=.24f,shadows="none",shadowStrength=1},
                ambient=new MapLookAmbient{sky=0x92b4f0,skyScale=.48f,equator=0x9aaccd,equatorScale=.27f,ground=0x171320,groundScale=.48f},
                fog=indoor?new MapLookFog{mode="linear",color=0x0d0a12,start=30,end=118}:new MapLookFog{mode="linear",color=0x405677,skyLerp=.42f,start=65,end=210},
                shadows=new MapLookShadows{cascades=2,distance=65,fallbackCascades=2,fallbackDistance=65},
                sky=new MapLookSky{variant=indoor?"none":"night",horizon=volcanic?0x633345:cold?0x405677:0x333f64,zenith=volcanic?0x100b1c:0x080e22,discSize=id=="moonlit"?.065f:DiscSizeDefault,
                    halo=NightHalo,stars=StarsDefault,crater=CraterDefault,cloudSpeed=CloudSpeedDefault,dir=new V3(SunDirDefault[0],SunDirDefault[1],SunDirDefault[2])},
                lights=Array.Empty<MapLookPractical>(),blockLooks=Array.Empty<MapLookBlock>(),kitLooks=Array.Empty<MapLookBlock>(),dressing="",backdrop="",atmosphere="",ambience=""};
        }
        public static bool HasFrom(MapLookLight l)=>l!=null&&(l.from.x!=0||l.from.y!=0||l.from.z!=0);
        /// <summary>Whether the light has its own direction (a position or an elevation/bearing); a rim without one keeps the rig's.</summary>
        public static bool HasDirection(MapLookLight l)=>HasFrom(l)||l!=null&&(l.elevation!=0||l.bearing!=0);
        /// <summary>The unit direction toward a light at `elevation` degrees above the horizon and compass `bearing` (from +z toward +x).</summary>
        public static void Toward(double elevation,double bearing,out double x,out double y,out double z){double e=elevation*Math.PI/180,b=bearing*Math.PI/180;x=Math.Cos(e)*Math.Sin(b);y=Math.Sin(e);z=Math.Cos(e)*Math.Cos(b);}
        /// <summary>The direction toward the light (unit), from `from` when set, else from elevation and bearing.</summary>
        public static void Toward(MapLookLight l,out double x,out double y,out double z)
        {if(HasFrom(l)){double n=Math.Sqrt((double)l.from.x*l.from.x+(double)l.from.y*l.from.y+(double)l.from.z*l.from.z);x=l.from.x/n;y=l.from.y/n;z=l.from.z/n;}else Toward(l.elevation,l.bearing,out x,out y,out z);}
        /// <summary>The sky shader's `_SunDir` (the shader normalises it): `dir` as written, else the unit vector from the angles.</summary>
        public static void SunDirection(MapLookSky s,out float x,out float y,out float z)
        {if(s.dir.x!=0||s.dir.y!=0||s.dir.z!=0){x=s.dir.x;y=s.dir.y;z=s.dir.z;return;}Toward(s.elevation,s.bearing,out double a,out double b,out double c);x=(float)a;y=(float)b;z=(float)c;}
        /// <summary>The shader's `_HaloFalloff`: 1 / halo size (exactly 12 for the night's 1/12).</summary>
        public static float HaloFalloff(MapLookSky s)=>(float)(1.0/s.halo);
        /// <summary>The disc's base shade under the crater term: 1 - crater (exactly .76 for the night's .24).</summary>
        public static float DiscBase(MapLookSky s)=>(float)(1.0-s.crater);
        public static bool Flickers(MapLookPractical l)=>l!=null&&(l.flicker=="fire"||l.flicker=="stutter");
        /// <summary>The practical light's intensity multiplier at `time` seconds: 1 when steady, 1 - depth .. 1 when it flickers.</summary>
        public static double Flicker(MapLookPractical l,double time)
        {
            if(l==null)return 1;double t=time+l.phase;
            if(l.flicker=="fire"){double w=2*Math.PI*l.rate,s=.5*Math.Sin(w*t)+.3*Math.Sin(w*1.5*t+1.7)+.2*Math.Sin(w*1.23*t+4.1);return 1-l.depth*(.5+.5*s);}
            if(l.flicker=="stutter"){double u=t%l.period;if(u<0)u+=l.period;return u<l.length&&(((int)(u/l.length*4))&1)==0?1-l.depth:1;}
            return 1;
        }
        /// <summary>The light block's fall-off: (1 - (d/range)²)², 1 at the light, 0 at and beyond `range`.</summary>
        public static double Attenuation(double distance,double range){if(!(range>0))return 0;double q=distance/range;if(q>=1)return 0;q=1-q*q;return q*q;}
        private static readonly HashSet<string> LightShadows=new HashSet<string>{"none","soft","hard"},FogModes=new HashSet<string>{"none","linear","exp","exp2"},Variants=new HashSet<string>{"night","dusk","none"},FlickerKinds=new HashSet<string>{"","fire","stutter"};
        private static void Fail(string id,string what)=>throw new InvalidOperationException("MapLooks.json "+id+": "+what);
        private static bool Finite(params float[] v){foreach(var x in v)if(float.IsNaN(x)||float.IsInfinity(x))return false;return true;}
        private static bool Rgb(int c)=>c>=0&&c<=0xffffff;
        private static void Light(string id,string name,MapLookLight l,bool needsDirection)
        {
            if(l==null)Fail(id,name+" is missing");if(!Rgb(l.color)||!Finite(l.intensity,l.elevation,l.bearing,l.shadowStrength,l.from.x,l.from.y,l.from.z)||l.intensity<0)Fail(id,name+" colour or intensity");
            if(!LightShadows.Contains(l.shadows??"")||l.shadowStrength<0||l.shadowStrength>1)Fail(id,name+" shadows");if(l.elevation<=-90||l.elevation>90)Fail(id,name+" elevation");
            if(needsDirection&&!HasDirection(l))Fail(id,name+" has no direction");
        }
        /// <summary>Throws on anything the runtime cannot apply as written.</summary>
        public static void Validate(MapLookSet set)
        {
            if(set==null||set.schema!=Schema||set.maps==null)throw new InvalidOperationException("MapLooks.json: missing maps or schema "+Schema);
            var ids=new HashSet<string>();var names=new HashSet<string>();
            foreach(var m in set.maps)
            {
                if(m==null||string.IsNullOrEmpty(m.id))throw new InvalidOperationException("MapLooks.json: an entry has no map id");string id=m.id;
                if(!ids.Add(id))Fail(id,"duplicate map id");if(string.IsNullOrEmpty(m.look)||!names.Add(m.look))Fail(id,"missing or duplicate look name");
                if(!Finite(m.farClip)||m.farClip<=0||!Rgb(m.background))Fail(id,"camera");
                Light(id,"key",m.key,true);Light(id,"rim",m.rim,false);
                var a=m.ambient;if(a==null||!Rgb(a.sky)||!Rgb(a.equator)||!Rgb(a.ground)||!Finite(a.skyScale,a.equatorScale,a.groundScale)||a.skyScale<0||a.equatorScale<0||a.groundScale<0)Fail(id,"ambient");
                var f=m.fog;if(f==null||!FogModes.Contains(f.mode??"")||!Rgb(f.color)||!Finite(f.skyLerp,f.start,f.end,f.density)||f.skyLerp<0||f.skyLerp>1)Fail(id,"fog");
                if(f.mode=="linear"&&!(f.start>=0&&f.end>f.start))Fail(id,"linear fog range");if((f.mode=="exp"||f.mode=="exp2")&&!(f.density>0))Fail(id,"fog density");
                var sh=m.shadows;if(sh==null||!Cascades(sh.cascades)||!Cascades(sh.fallbackCascades)||!Finite(sh.distance,sh.fallbackDistance)||sh.distance<=0||sh.fallbackDistance<=0)Fail(id,"shadows");
                var s=m.sky;if(s==null||!Variants.Contains(s.variant??"")||!Rgb(s.horizon)||!Rgb(s.zenith)||!Rgb(s.glow)||!Rgb(s.disc)||!Rgb(s.cloud))Fail(id,"sky colours or variant");
                if(!Finite(s.discSize,s.halo,s.stars,s.crater,s.cloudSpeed,s.elevation,s.bearing,s.dir.x,s.dir.y,s.dir.z)||s.discSize<0||s.discSize>=1||!(s.halo>0)||s.stars<0||s.stars>1||s.crater<0||s.crater>1||s.cloudSpeed<0)Fail(id,"sky parameters");
                if(s.variant!="none"&&s.dir.x==0&&s.dir.y==0&&s.dir.z==0&&s.elevation==0&&s.bearing==0)Fail(id,"sky has no sun or moon direction");
                var lights=m.lights??Array.Empty<MapLookPractical>();if(lights.Length>MaxLights)Fail(id,"more than "+MaxLights+" practical lights");
                foreach(var l in lights)
                {
                    if(l==null||!Rgb(l.color)||!Finite(l.pos.x,l.pos.y,l.pos.z,l.intensity,l.range,l.rate,l.period,l.length,l.depth,l.phase)||l.intensity<0||!(l.range>0))Fail(id,"practical light "+l?.id);
                    if(!FlickerKinds.Contains(l.flicker??"")||l.depth<0||l.depth>1)Fail(id,"practical light flicker "+l.id);
                    if(l.flicker=="fire"&&!(l.rate>0))Fail(id,"fire flicker needs a rate: "+l.id);if(l.flicker=="stutter"&&!(l.length>0&&l.period>l.length))Fail(id,"stutter needs period > length > 0: "+l.id);
                }
                BlockLooks(id,m.blockLooks);BlockLooks(id+" kit",m.kitLooks);foreach(var b in m.kitLooks??Array.Empty<MapLookBlock>())if(b.band||b.block.StartsWith("material:",StringComparison.Ordinal))Fail(id,"kit look "+b.block+" with a band or a material");
                NativeMapStyle.Validate(id,m.toon);
                var k=m.blocks;if(k!=null&&(!Finite(k.dust,k.haze,k.hazeTop)||k.dust<0||k.dust>1||k.haze<0||k.haze>1||k.haze>0&&!(k.hazeTop>0)||!Rgb(k.hazeColor)))Fail(id,"blocks");
            }
            BlockLooks("preview",set.preview);
        }
        // An entry needs a block pattern (an id, a glob, or "material:" and a name) and a known look key.
        private static void BlockLooks(string id,MapLookBlock[] list)
        {
            foreach(var b in list??Array.Empty<MapLookBlock>())
                if(b==null||string.IsNullOrEmpty(b.block)||b.block=="material:"||NativeBlockLooks.Id(b.look)<0||!Rgb(b.color))Fail(id,"block look "+b?.block);
        }
        private static bool Cascades(int n)=>n==1||n==2||n==4;
    }
}
