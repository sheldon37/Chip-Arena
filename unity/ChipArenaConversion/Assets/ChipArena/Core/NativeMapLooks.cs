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
    /// <item>`blockLooks` (read by NativeArenaDressing, Core/NativeBlockLooks.cs): `block` is an exact block id, a glob ("*"
    /// matches any run: "hotel-pier-*"; the first matching glob in list order wins, so specific ones go first) or
    /// "material:brick" (used when no id or glob matches); `look` a NativeBlockLooks key ("brick-red"); `color` the base
    /// colour (0: the look's 3.10 palette colour); `band` paints the 2.3 load-bearing chevron band (1.0-1.6 m above the
    /// block's base) and its DANGER tag; `note` is for people (the 3.3 legend element it covers). Brickfall's entries expect
    /// the grey-box to keep the id stems of docs/arenas/scripts/brickfall_m1.py and ARENA_PROPOSAL.md 5.2: frontage-, stack-
    /// (stack-*-base* banded), hotel- (hotel-pier-nw/-ne/-sw/-se banded, hotel-core-, hotel-floor-, hotel-roof), ashby- and
    /// bray- (ashby-pier-F1.. banded, -core, -floor-, -roof), bridge- (bridge-*-bearing* banded), kessels, dunmores, market-,
    /// storeroom-, kiosk-, news-kiosk-, phonebox-, tram-shelter-, monument-, barricade-, pallet-, sandbag-, stall-, dock-,
    /// truck-, and parts named -sill-, -lintel-, -cornice-, -string-, -quoin-, -coping, -window-, -boards-, -shutter, -ramp,
    /// -canopy, -neon-, -rubble-; mercer-sign, -slab, street-, lane-, yard-, square-, barrier-. Anything else falls back to
    /// its material's look.</item>
    /// <item>Stubs the later passes fill; nothing reads them yet: `dressing` (articulation kit id), `backdrop`, `atmosphere`
    /// and `ambience` ids. Empty means today's.</item>
    /// </list>
    /// A map with no entry gets <see cref="NativeMapLooks.Fallback"/>, which is today's look worked out from the map itself.
    /// </summary>
    [Serializable] public sealed class MapLook
    {
        public string id,look;public float farClip;public int background;public MapLookLight key,rim;public MapLookAmbient ambient;public MapLookFog fog;
        public MapLookShadows shadows;public MapLookSky sky;public MapLookPractical[] lights;public MapLookBlock[] blockLooks;public string dressing,backdrop,atmosphere,ambience;public MapLookBlocks blocks;
    }
    [Serializable] public sealed class MapLookLight {public int color;public float intensity,elevation,bearing,shadowStrength;public V3 from;public string shadows;}
    [Serializable] public sealed class MapLookAmbient {public int sky,equator,ground;public float skyScale,equatorScale,groundScale;}
    [Serializable] public sealed class MapLookFog {public string mode;public int color;public float skyLerp,start,end,density;}
    [Serializable] public sealed class MapLookShadows {public int cascades,fallbackCascades;public float distance,fallbackDistance;}
    [Serializable] public sealed class MapLookSky {public string variant;public int horizon,zenith,glow,disc,cloud;public float discSize,halo,stars,crater,cloudSpeed,elevation,bearing;public V3 dir;}
    [Serializable] public sealed class MapLookPractical {public string id,flicker;public V3 pos;public int color;public float intensity,range,rate,period,length,depth,phase;}
    [Serializable] public sealed class MapLookBlock {public string block,look,note;public int color;public bool band;}
    [Serializable] public sealed class MapLookBlocks {public bool dressed;public float dust,haze,hazeTop;public int hazeColor;}

    /// <summary>
    /// Loader rules for Resources/MapLooks.json: lookup, today's fallback, validation, light and sky directions, the sky
    /// shader's derived values and the practical lights' flicker and fall-off. Pure; Flicker and Attenuation allocate nothing.
    /// </summary>
    public static class NativeMapLooks
    {
        public const int Schema=1,MaxLights=8;
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
                lights=Array.Empty<MapLookPractical>(),blockLooks=Array.Empty<MapLookBlock>(),dressing="",backdrop="",atmosphere="",ambience=""};
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
                BlockLooks(id,m.blockLooks);
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
