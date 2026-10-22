using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// A map look's anime style (MapLooks.json `toon`; docs/MAP-STYLE-ANIME.md). Presentation only: nothing here is read by the
    /// simulation, the authority, navigation, the network frame or a save. Missing, or `on` false, is today's look. Colours
    /// are sRGB ints like the rest of MapLooks; tints (`shade`, `lit`, `gradeShadow`, `gradeLight`) are linear multipliers.
    /// <list type="bullet">
    /// <item>Ramp (Resources/NativeToon.cginc): `shadeEdge` and `lightEdge` on N.L times the key's shadow, the half-tone
    /// band's level `halfTone`, the band edges' half width `softness`; `shade` tints the ambient on the shade side and `lift`
    /// keeps that share of the key there; `lit` tints the key; `flatten` blends Unity's trilight toward one sky-to-ground
    /// fill; `points` steps the point lights (practical slots, flashes, muzzle and actor lights); `round` is the soft-edge
    /// radius per metre of a block's thinnest half extent; `rimColor` x `rimIntensity` the edge back light, `edgeLight` the key
    /// on a rounded edge.</item>
    /// <item>Paint: `detail` keeps that share of a look's fine variation, `gradient` darkens walls low and lightens them high,
    /// `wash` a broad soft mottling, `baseShadow` a soft shadow up the first 1.6 m of every wall.</item>
    /// <item>Shapes (the wrap mesh, NativeToonMesh.Wrap; each 0 or 1): `plinths` (walls' plinths, columns' bases), `copings`
    /// (walls' copings, columns' capitals), `chips` (a broken top corner on some old masonry), `posts` (crate and container
    /// posts and rails).</item>
    /// <item>Highlights and occlusion: `specular` the stylised highlight band on rounded edges past `specularEdge` (N.H),
    /// `occlusion` the depth of the third, darkest band at wall feet (`occlusionHeight` m) and under ledges.</item>
    /// <item>Outline (Resources/NativeToonEdge.shader, one full-screen pass on the normal tiers): `line` its strength, `lineColor`
    /// the tint it multiplies the scene by, faded out from `lineFadeStart` to `lineFadeEnd` m, `crease` the crease threshold.</item>
    /// <item>Grade (Resources/NativeBloom.shader's composite, the normal tiers): `saturation`, `contrast`, the split tints.</item>
    /// <item>Sky (Resources/NativeNightSky.shader): `skyToon` paints the clouds in flat tones (`cloudLit`, `cloudShade`, a
    /// cover of `clouds`, their size `cloudScale`) over a horizon band `horizonBand` `bandWidth` high.</item>
    /// <item>Overrides for the style (each only when written: a light with `shadows`, an ambient with a scale, a sky with a
    /// variant, a colour or a start other than 0): `key`, `rim`, `ambient`, `sky`, `fogColor`, `fogStart` (a linear fog's start
    /// only, nearer for more atmospheric perspective), `background`, `hazeColor`. The fog's mode and end, the practical lights
    /// and every block look stay the look's own, so draw ranges and budgets never move.</item>
    /// </list>
    /// </summary>
    [Serializable] public sealed class MapLookToon
    {
        public bool on;
        public float softness,shadeEdge,lightEdge,halfTone,round,flatten,lift,points,rimIntensity,edgeLight;public V3 shade,lit;public int rimColor;
        public float detail,gradient,wash,baseShadow;
        public float plinths,copings,chips,posts,specular,specularEdge,occlusion,occlusionHeight;
        public float line,lineFadeStart,lineFadeEnd,crease;public int lineColor;
        public float saturation,contrast;public V3 gradeShadow,gradeLight;
        public float skyToon,clouds,cloudScale,bandWidth;public int cloudLit,cloudShade,horizonBand;
        public MapLookLight key,rim;public MapLookAmbient ambient;public MapLookSky sky;public int fogColor,background,hazeColor;public float fogStart;
    }
    /// <summary>The shader values of a look's style (linear), as NativeLighting writes them: the NativeToon.cginc globals, the
    /// outline's, the grade's and the sky's. `On` false: every global is written as 0 (the legacy look).</summary>
    public sealed class MapStyleState
    {
        public bool On,Outline,Grade,Sky;
        public float[] Toon=new float[4],Band=new float[4],Shade=new float[4],Lit=new float[4],Rim=new float[4],Paint=new float[4],Wrap=new float[4],Fx=new float[4];
        public float[] Edge=new float[4],EdgeColor=new float[4],GradeParams=new float[4],GradeShadow=new float[4],GradeLight=new float[4];
        public float[] SkyToon=new float[4],CloudLit=new float[4],CloudShade=new float[4],SkyBand=new float[4];
        public void Clear()
        {
            On=Outline=Grade=Sky=false;Array.Clear(Toon,0,4);Array.Clear(Band,0,4);Array.Clear(Shade,0,4);Array.Clear(Lit,0,4);Array.Clear(Rim,0,4);Array.Clear(Paint,0,4);Array.Clear(Wrap,0,4);Array.Clear(Fx,0,4);Array.Clear(Edge,0,4);Array.Clear(EdgeColor,0,4);
            Array.Clear(GradeParams,0,4);Array.Clear(GradeShadow,0,4);Array.Clear(GradeLight,0,4);Array.Clear(SkyToon,0,4);Array.Clear(CloudLit,0,4);Array.Clear(CloudShade,0,4);Array.Clear(SkyBand,0,4);
        }
    }

    /// <summary>
    /// The anime map style's rules (docs/MAP-STYLE-ANIME.md): the switch, the styled look, the shader values, and the CPU copy of
    /// every formula Resources/NativeToon.cginc, NativeBlockLit's toon shapes and NativeToonEdge run (the checks hold the two
    /// together). Pure; the per-frame helpers allocate nothing.
    /// </summary>
    public static class NativeMapStyle
    {
        /// <summary>The command line that restores the legacy look on every map (`--ca-map-style legacy`).</summary>
        public const string Argument="--ca-map-style",Legacy="legacy";
        /// <summary>Whether the style is on for a look: its `toon.on`, unless the legacy switch is set.</summary>
        public static bool On(MapLook look,bool legacy)=>!legacy&&look?.toon!=null&&look.toon.on;
        /// <summary>Whether `args` hold the legacy switch (`--ca-map-style legacy` or `--ca-map-style=legacy`).</summary>
        public static bool LegacyArgument(string[] args)
        {
            if(args==null)return false;
            for(int i=0;i<args.Length;i++){var a=args[i];if(a==Argument+"="+Legacy||a==Argument&&i+1<args.Length&&args[i+1]==Legacy)return true;}
            return false;
        }

        // ---------------- the styled look ----------------
        /// <summary>
        /// The look as the style draws it: `look` itself when the style is off; else a copy with the style's overrides (key, rim,
        /// ambient, sky, fog colour, background, haze colour) in place. The fog's mode and range, the lights, the blocks' dressing
        /// and every block and kit look are the look's own, so the kits' draw ranges (NativeExpeditionCityKit.FarRange) and every
        /// budget measured on the look stay the same. Allocates the copy (once per Apply).
        /// </summary>
        public static MapLook Styled(MapLook look,bool legacy)
        {
            if(!On(look,legacy))return look;var t=look.toon;
            var s=new MapLook{id=look.id,look=look.look,farClip=look.farClip,background=t.background!=0?t.background:look.background,key=Has(t.key)?t.key:look.key,rim=Has(t.rim)?t.rim:look.rim,
                ambient=Has(t.ambient)?t.ambient:look.ambient,shadows=look.shadows,sky=Has(t.sky)?t.sky:look.sky,lights=look.lights,blockLooks=look.blockLooks,kitLooks=look.kitLooks,
                dressing=look.dressing,backdrop=look.backdrop,atmosphere=look.atmosphere,ambience=look.ambience,toon=t};
            var f=look.fog;s.fog=f==null?null:new MapLookFog{mode=f.mode,color=t.fogColor!=0?t.fogColor:f.color,skyLerp=t.fogColor!=0?0:f.skyLerp,start=t.fogStart>0&&t.fogStart<f.end?t.fogStart:f.start,end=f.end,density=f.density};
            var b=look.blocks;s.blocks=b==null?null:new MapLookBlocks{dressed=b.dressed,dust=b.dust,haze=b.haze,hazeTop=b.hazeTop,hazeColor=t.hazeColor!=0?t.hazeColor:b.hazeColor};
            return s;
        }
        /// <summary>A written override (JsonUtility fills a missing object with defaults, System.Text.Json leaves it null).</summary>
        public static bool Has(MapLookLight l)=>l!=null&&!string.IsNullOrEmpty(l.shadows);
        public static bool Has(MapLookAmbient a)=>a!=null&&(a.skyScale>0||a.equatorScale>0||a.groundScale>0);
        public static bool Has(MapLookSky s)=>s!=null&&!string.IsNullOrEmpty(s.variant);

        // ---------------- the shader values ----------------
        /// <summary>
        /// The globals for `look` (styled or not) into `into`: off (every value 0) when the style is off; else NativeToon.cginc's
        /// six vectors, the outline's (`outline` false on the low tier: its strength 0, and the runtime skips the pass), the
        /// grade's and the sky's. `linear` converts sRGB colours to linear (a Linear-colour project); allocation free.
        /// </summary>
        public static void State(MapLook look,bool legacy,bool outline,MapStyleState into)
        {
            into.Clear();if(!On(look,legacy))return;var t=look.toon;into.On=true;
            Set(into.Toon,1,t.softness,t.rimIntensity,t.flatten);Set(into.Band,t.shadeEdge,t.lightEdge,t.halfTone,t.round);
            Set(into.Shade,t.shade.x,t.shade.y,t.shade.z,t.lift);Set(into.Lit,t.lit.x,t.lit.y,t.lit.z,t.points);
            Lin(into.Rim,t.rimColor);into.Rim[3]=t.edgeLight;Set(into.Paint,t.detail,t.gradient,t.wash,t.baseShadow);
            Set(into.Wrap,t.plinths,t.copings,t.chips,t.posts);Set(into.Fx,t.specular,t.specularEdge,t.occlusion,t.occlusionHeight);
            into.Outline=outline&&t.line>0;Set(into.Edge,into.Outline?t.line:0,t.lineFadeStart,t.lineFadeEnd,t.crease);Lin(into.EdgeColor,t.lineColor);into.EdgeColor[3]=1;
            into.Grade=t.saturation>0&&t.contrast>0;Set(into.GradeParams,into.Grade?t.saturation:1,into.Grade?t.contrast:1,0,into.Grade?1:0);
            Set(into.GradeShadow,Or1(t.gradeShadow.x),Or1(t.gradeShadow.y),Or1(t.gradeShadow.z),0);Set(into.GradeLight,Or1(t.gradeLight.x),Or1(t.gradeLight.y),Or1(t.gradeLight.z),0);
            into.Sky=t.skyToon>0;Set(into.SkyToon,t.skyToon,t.clouds,t.cloudScale,t.bandWidth);Lin(into.CloudLit,t.cloudLit);Lin(into.CloudShade,t.cloudShade);Lin(into.SkyBand,t.horizonBand);
        }
        private static float Or1(float v)=>v>0?v:1;
        private static void Set(float[] a,float x,float y,float z,float w){a[0]=x;a[1]=y;a[2]=z;a[3]=w;}
        private static void Lin(float[] a,int hex){a[0]=(float)NativeEntranceLight.ToLinear(((hex>>16)&255)/255.0);a[1]=(float)NativeEntranceLight.ToLinear(((hex>>8)&255)/255.0);a[2]=(float)NativeEntranceLight.ToLinear((hex&255)/255.0);a[3]=0;}

        // ---------------- validation ----------------
        /// <summary>Throws on a style the runtime cannot apply as written (NativeMapLooks.Validate calls it for every look).</summary>
        public static void Validate(string id,MapLookToon t)
        {
            if(t==null||!t.on)return;
            void Fail(string what)=>throw new InvalidOperationException("MapLooks.json "+id+" toon: "+what);
            bool Finite(params float[] v){foreach(var x in v)if(float.IsNaN(x)||float.IsInfinity(x))return false;return true;}
            bool Unit(float v)=>v>=0&&v<=1;bool Rgb(int c)=>c>=0&&c<=0xffffff;
            if(!Finite(t.softness,t.shadeEdge,t.lightEdge,t.halfTone,t.round,t.flatten,t.lift,t.points,t.rimIntensity,t.edgeLight,t.shade.x,t.shade.y,t.shade.z,t.lit.x,t.lit.y,t.lit.z))Fail("ramp values");
            if(!(t.softness>0&&t.softness<=.25f)||!(t.shadeEdge>=0&&t.lightEdge>t.shadeEdge&&t.lightEdge<=1)||!Unit(t.halfTone)||!Unit(t.flatten)||!Unit(t.lift)||!Unit(t.points)||t.round<0||t.round>1||t.rimIntensity<0||!Unit(t.edgeLight))Fail("ramp ranges");
            foreach(var v in new[]{t.shade,t.lit})if(!(v.x>0&&v.y>0&&v.z>0&&v.x<=2&&v.y<=2&&v.z<=2))Fail("tints (0..2, linear multipliers)");
            if(!Finite(t.detail,t.gradient,t.wash,t.baseShadow)||!Unit(t.detail)||!(t.gradient>=0&&t.gradient<=.5f)||!(t.wash>=0&&t.wash<=.5f)||!(t.baseShadow>=0&&t.baseShadow<=.6f))Fail("paint");
            if(!Finite(t.plinths,t.copings,t.chips,t.posts)||!Unit(t.plinths)||!Unit(t.copings)||!Unit(t.chips)||!Unit(t.posts))Fail("shapes (0..1)");
            if(!Finite(t.specular,t.specularEdge,t.occlusion,t.occlusionHeight)||!Unit(t.specular)||t.specular>0&&!(t.specularEdge>=.8f&&t.specularEdge<1)||!Unit(t.occlusion)||!(t.occlusionHeight>=0&&t.occlusionHeight<=1))Fail("highlights and occlusion");
            if(!Finite(t.line,t.lineFadeStart,t.lineFadeEnd,t.crease)||!Unit(t.line)||t.line>0&&!(t.lineFadeStart>=0&&t.lineFadeEnd>t.lineFadeStart&&t.crease>0))Fail("outline");
            if(!Finite(t.saturation,t.contrast,t.gradeShadow.x,t.gradeShadow.y,t.gradeShadow.z,t.gradeLight.x,t.gradeLight.y,t.gradeLight.z)||t.saturation<0||t.saturation>2||t.contrast<0||t.contrast>2)Fail("grade");
            if(!Finite(t.skyToon,t.clouds,t.cloudScale,t.bandWidth)||!Unit(t.skyToon)||!Unit(t.clouds)||t.skyToon>0&&!(t.cloudScale>0&&t.bandWidth>0&&t.bandWidth<1))Fail("sky");
            if(!Finite(t.fogStart)||t.fogStart<0)Fail("fog start");
            if(!Rgb(t.rimColor)||!Rgb(t.lineColor)||!Rgb(t.cloudLit)||!Rgb(t.cloudShade)||!Rgb(t.horizonBand)||!Rgb(t.fogColor)||!Rgb(t.background)||!Rgb(t.hazeColor))Fail("colours");
        }

        // ---------------- the CPU copy of NativeToon.cginc ----------------
        /// <summary>NativeToon.cginc's constants: the point lights' steps and stair edge, the soft-edge radius cap, the edge light's gain.</summary>
        public const float PointSteps=3,Stair=.18f,RadiusMax=.3f,EdgeGain=3.5f;
        private static float Smooth(float a,float b,float x){float t=Math.Max(0,Math.Min(1,(x-a)/(b-a)));return t*t*(3-2*t);}
        private static float Sat(float x)=>Math.Max(0,Math.Min(1,x));
        public static float Step(float x,float edge,float soft)=>Smooth(edge-soft,edge+soft,x);
        /// <summary>The key's ramp (ToonRamp) for a band set: 0 shade, `half` between the edges, 1 lit.</summary>
        public static float Ramp(float x,float shadeEdge,float lightEdge,float half,float soft){soft=Math.Max(soft,.001f);return half*Step(x,shadeEdge,soft)+(1-half)*Step(x,lightEdge,soft);}
        /// <summary>ToonStairs: soft stairs at whole steps.</summary>
        public static float Stairs(float x){float f=(float)Math.Floor(x);return f+Smooth(.5f-Stair,.5f+Stair,x-f);}
        /// <summary>ToonPoints' gain on a point-light sum of luminance `l` with blend `points` (1 when the sum is 0).</summary>
        public static float PointGain(float l,float points){if(l<.000001f)return 1;float q=Stairs((float)Math.Sqrt(l)*PointSteps)/PointSteps;return 1+(q*q/l-1)*points;}
        /// <summary>ToonDiffuse for one channel: the shade (ambient plus the lift in the shade tint) to the lit (ambient plus the
        /// key in the lit tint) through the ramp of N.L x shadow.</summary>
        public static float Diffuse(float ambient,float key,float ndl,float atten,float shadeTint,float lift,float litTint,float shadeEdge,float lightEdge,float half,float soft)
        {float r=Ramp(Sat(ndl),shadeEdge,lightEdge,half,soft)*Shadow(atten);float shade=(ambient+key*lift)*shadeTint;return shade+(ambient+key*litTint-shade)*r;}
        /// <summary>ToonShadow: the key's shadow eased between ShadowLo and ShadowHi (kept apart from N.L).</summary>
        public const float ShadowLo=.15f,ShadowHi=.85f;
        public static float Shadow(float atten)=>Smooth(ShadowLo,ShadowHi,atten);
        /// <summary>ToonRadius: the soft-edge radius for half extents h and drawn chamfer c with share `round`.</summary>
        public static float Radius(float hx,float hy,float hz,float c,float round){float lo=Math.Min(hx,Math.Min(hy,hz));return Math.Min(.9f*lo,Math.Min(RadiusMax,Math.Max(c,round*lo)));}
        /// <summary>ToonRoundNormal (object space) into n: the direction out of the box shrunk by r, or the facet past 60 degrees.</summary>
        public static void RoundNormal(float x,float y,float z,float hx,float hy,float hz,float r,float fx,float fy,float fz,out float nx,out float ny,out float nz)
        {
            float cx=Math.Max(hx-r,0),cy=Math.Max(hy-r,0),cz=Math.Max(hz-r,0);float qx=x-Math.Max(-cx,Math.Min(cx,x)),qy=y-Math.Max(-cy,Math.Min(cy,y)),qz=z-Math.Max(-cz,Math.Min(cz,z));
            float l=(float)Math.Sqrt(qx*qx+qy*qy+qz*qz);if(l>.00001f){nx=qx/l;ny=qy/l;nz=qz/l;}else{nx=fx;ny=fy;nz=fz;}
            if(nx*fx+ny*fy+nz*fz<=.5f){nx=fx;ny=fy;nz=fz;}
        }

        // ---------------- NativeBlockLit's toon shapes ----------------
        /// <summary>The taper flag (NativeBlockLit FLAG_TAPER; next to NativeBlockLooks' flags) and how much a tapered part narrows at its top.</summary>
        public const int Taper=256;public const float TaperShare=.3f;
        /// <summary>ToonChamfer's rules: a rod's thinner half extents (m, and their ratio), its length over the thicker; the thickness
        /// up to which a block's chamfer grows, the growth per metre and its cap.</summary>
        public const float Rod=.15f,RodSquare=.6f,RodLong=3,MidHalf=1,ChamferGrow=.16f,ChamferMax=.08f;
        /// <summary>The chamfer the style draws for half extents h and the instance's chamfer c (NativeBlockLit ToonChamfer).</summary>
        public static float Chamfer(float hx,float hy,float hz,float c)
        {
            float lo=Math.Min(hx,Math.Min(hy,hz)),hi=Math.Max(hx,Math.Max(hy,hz)),mid=hx+hy+hz-lo-hi;
            if(IsRod(hx,hy,hz))return NativeBlockLooks.ClampShare*lo;
            if(lo<=MidHalf)return Math.Max(c,Math.Min(ChamferMax,ChamferGrow*lo));
            return c;
        }
        /// <summary>Whether the style draws half extents h as a rod (a round section): poles, pipes, posts, cables, rails.</summary>
        public static bool IsRod(float hx,float hy,float hz){float lo=Math.Min(hx,Math.Min(hy,hz)),hi=Math.Max(hx,Math.Max(hy,hz)),mid=hx+hy+hz-lo-hi;return mid<=Rod&&lo>=RodSquare*mid&&hi>=RodLong*mid;}
        /// <summary>A lattice vertex as the style places it (ToonLattice without the mesh turns): the chamfer, then the taper.</summary>
        public static void Lattice(float ax,float ay,float az,float bx,float by,float bz,float hx,float hy,float hz,float c,int flags,out float x,out float y,out float z)
        {
            float t=Chamfer(hx,hy,hz,c);x=NativeBlockLooks.Corner(ax,bx,hx,t);y=NativeBlockLooks.Corner(ay,by,hy,t);z=NativeBlockLooks.Corner(az,bz,hz,t);
            if((flags&Taper)!=0){float k=1-TaperShare*Sat(y/Math.Max(hy,.0001f)*.5f+.5f);x*=k;z*=k;}
        }

        // ---------------- NativeBlockLit's wrap (NativeToonMesh.Wrap) ----------------
        /// <summary>The wrap's classes (NativeBlockLit WRAP_*), its outset cap (m: no band or post stands farther from a face), the
        /// walls' bevel cap, the broken corner's drop and the post line's slide share of a half extent.</summary>
        public const int WrapNone=0,WrapWall=1,WrapColumn=2,WrapCrate=3,WrapContainer=4;
        public const float WrapOut=.11f,WrapBevel=.07f,WrapChip=.22f,WrapChipShare=.3f,WrapPilaster=.35f,WrapPilasterOut=.05f;
        /// <summary>A wrap's shape for one block (ToonWrapShape's outputs, m).</summary>
        public struct WrapForm {public float Section,Plinth,PlinthOut,Coping,CopingOut,Top,Bottom,Slide,PostOut,Chip;}
        /// <summary>The wrap class for half extents h, a look id and the instance flags (NativeBlockLit ToonWrapClass).</summary>
        public static int WrapClass(float hx,float hy,float hz,int look,int flags)
        {
            if((flags&NativeCyberCityLooks.Bank)!=0)return WrapNone;
            float lo=Math.Min(hx,hz),hi=Math.Max(hx,hz);bool masonry=look>=1&&look<=4||look==NativeBlockLooks.Cobble;
            if(masonry&&hy>=.6f&&lo>=.08f&&lo>=.5f*hi&&hi<=.8f&&hy>=1.8f*hi)return WrapColumn;
            if(masonry&&hy>=.3f&&lo>=.1f&&hi>=.3f)return WrapWall;
            if(look==NativeBlockLooks.Timber&&hy>=.2f&&hy<=1.2f&&lo>=.2f&&hi<=1.5f)return WrapCrate;
            if(look==NativeBlockLooks.Corrugated&&hy>=.4f&&lo>=.3f)return WrapContainer;
            return WrapNone;
        }
        private static float Frac(float v)=>v-(float)Math.Floor(v);
        /// <summary>The wrap's shape for a class (ToonWrapShape): `c` the style's chamfer, `wrap` _ArenaToonWrap, `seed` the block's.</summary>
        public static WrapForm WrapShape(int cls,float hx,float hy,float hz,float c,float seed,float plinths,float copings,float chips,float posts)
        {
            float H=2*hy,lo=Math.Min(hx,hz);var f=new WrapForm{Section=c,Top=Math.Min(c,ClampShare*Math.Min(lo,hy))};
            if(cls==WrapWall)
            {
                f.Section=Math.Max(c,Math.Min(WrapBevel,.4f*lo));f.Top=Math.Min(WrapBevel,ClampShare*Math.Min(lo,hy));
                f.Plinth=Math.Min(.32f,.09f*H)*plinths;f.PlinthOut=Math.Min(.035f,.25f*lo)*plinths;f.Coping=Math.Min(.26f,.07f*H)*copings;f.CopingOut=Math.Min(.05f,.3f*lo)*copings;
                f.Chip=chips*(Frac(seed*7.31f)>=.7f?1:0)*(1+(float)Math.Floor(Frac(seed*13.7f)*3.999f));f.Slide=f.Chip>.5f?WrapChip:0;
                if(Math.Max(hx,hz)>=2&&hy>=2&&plinths>.5f){f.Slide=WrapPilaster;f.PostOut=WrapPilasterOut;}
            }
            else if(cls==WrapColumn)
            {
                f.Section=Math.Max(c,.25f*lo);f.Top=Math.Min(.04f,ClampShare*Math.Min(lo,hy));
                f.Plinth=Math.Min(.3f,.12f*H)*plinths;f.PlinthOut=Math.Min(.07f,.25f*lo)*plinths;f.Coping=Math.Min(.28f,.12f*H)*copings;f.CopingOut=Math.Min(.08f,.3f*lo)*copings;
            }
            else if(cls==WrapCrate){f.Plinth=.1f*posts;f.PlinthOut=.015f*posts;f.Coping=.1f*posts;f.CopingOut=.015f*posts;f.Slide=Math.Min(.11f,.3f*lo)*posts;f.PostOut=.032f*posts;}
            else if(cls==WrapContainer){f.Plinth=.12f*posts;f.PlinthOut=.018f*posts;f.Coping=.12f*posts;f.CopingOut=.018f*posts;f.Slide=Math.Min(.12f,.3f*lo)*posts;f.PostOut=.035f*posts;}
            f.Bottom=f.Plinth>0?0:f.Top;f.Plinth=Math.Min(f.Plinth,.25f*H);f.Coping=Math.Min(Math.Max(f.Coping,f.Top+f.CopingOut),.3f*H);
            return f;
        }
        private const float ClampShare=NativeBlockLooks.ClampShare;
        /// <summary>A wrap vertex for its section point (ax, row, az; bx, zone, bz, slide), half extents h and shape `f` (ToonWrap
        /// before the taper); `flags` only for the taper.</summary>
        public static void WrapPlace(float ax,float row,float az,float bx,float zone,float bz,float slide,float hx,float hy,float hz,WrapForm f,int flags,out float x,out float y,out float z)
        {
            float rs=f.Section;x=NativeBlockLooks.Corner(ax,bx,hx,rs);z=NativeBlockLooks.Corner(az,bz,hz,rs);
            if(Math.Abs(slide)>.5f){bool alongX=Math.Abs(slide)<1.5f;float sl=Math.Sign(slide)*Math.Min(f.Slide,WrapChipShare*(alongX?hx:hz));if(alongX)x+=sl;else z+=sl;}
            float snx=Math.Sign(ax)*(1-Math.Abs(bx)),snz=Math.Sign(az)*(1-Math.Abs(bz));
            float pb=f.Plinth,op=f.PlinthOut,ct=f.Coping,oc=f.CopingOut,rt=f.Top,rb=f.Bottom;float o;
            if(row<.5f){y=-hy;o=pb>0?op:-rb;}
            else if(row<1.5f){y=pb>0?-hy+pb-Math.Min(op,.5f*pb):-hy+.2929f*rb;o=pb>0?op:-.2929f*rb;}
            else if(row<2.5f){y=-hy+Math.Max(pb,rb);o=0;}
            else if(row<3.5f){y=hy-ct;o=0;}
            else if(row<4.5f){y=hy-ct+Math.Min(oc,.5f*(ct-rt));o=oc;}
            else if(row<5.5f){y=hy-rt;o=oc;}
            else if(row<6.5f){y=hy-.2929f*rt;o=oc-.2929f*rt;}
            else{y=hy;o=oc-rt;}
            if(zone>1.5f)o+=f.PostOut;
            if(f.Chip>.5f&&row>3.5f&&zone>1.5f&&Math.Abs((ax>0?1:0)+(az>0?2:0)-(f.Chip-1))<.5f){y=Math.Max(y-Math.Min(WrapChip,ct+.1f)*(row-3)*.25f,hy-ct+.002f*(row-3));o=Math.Min(o,0)-.04f;}
            x+=snx*o;z+=snz*o;
            if((flags&Taper)!=0){float k=1-TaperShare*Sat(y/Math.Max(hy,.0001f)*.5f+.5f);x*=k;z*=k;}
        }
        /// <summary>A wrap vertex as NativeBlockLit's vertex stage places it: the class and shape for the block, then the place.
        /// `c` the instance's chamfer (the style's is taken here), `wrap` the map's features (plinths, copings, chips, posts).</summary>
        public static void WrapPoint(float ax,float row,float az,float bx,float zone,float bz,float slide,float hx,float hy,float hz,float c,int look,int flags,float seed,out float x,out float y,out float z,
            float plinths=1,float copings=1,float chips=1,float posts=1)
        {
            var f=WrapShape(WrapClass(hx,hy,hz,look,flags),hx,hy,hz,Chamfer(hx,hy,hz,c),seed,plinths,copings,chips,posts);
            WrapPlace(ax,row,az,bx,zone,bz,slide,hx,hy,hz,f,flags,out x,out y,out z);
        }

        // ---------------- NativeToonEdge's line ----------------
        /// <summary>NativeToonEdge's constants: a silhouette needs a neighbour farther by at least GapMin + GapShare x depth (m);
        /// the crease term's weight against the silhouette.</summary>
        public const float GapMin=.25f,GapShare=.04f,CreaseWeight=.85f,WideGap=2,CreaseFoot0=.015f,CreaseFoot1=.035f;
        /// <summary>A camera's pixel footprint per metre of eye depth (m): 2 tan(fov / 2) over the pixel height.</summary>
        public static float Foot(float fovDegrees,float pixelHeight)=>(float)(2*Math.Tan(fovDegrees*Math.PI/360)/Math.Max(1,pixelHeight));
        /// <summary>
        /// EdgeLine for a pixel of eye depth c (m) with neighbours l, r, d, u (left, right, down, up): a silhouette where a
        /// neighbour lies farther than the other side's slope carries on by the gap (l + r - 2c: a plane never; the line sits on
        /// the nearer surface: an occluder's edge, never the background's),
        /// a crease where 1/depth bends (it is affine across a plane) by more than `crease` of the pixel's own 1/depth while no
        /// neighbour is nearer by the gap (the far side of a silhouette is never lined), faded out
        /// from `fadeStart` to `fadeEnd` m, times `strength`.
        /// </summary>
        public static float Edge(float c,float l,float r,float d,float u,float strength,float fadeStart,float fadeEnd,float crease)=>Edge(c,l,r,d,u,l,r,d,u,strength,fadeStart,fadeEnd,crease,0);
        /// <summary>EdgeLine with its second ring of taps two widths away (L, R, D, U): a silhouette there needs WideGap x the gap,
        /// so a deep step lines two widths wide and a shallow one (interior detail) one.</summary>
        /// `foot` the camera's footprint per metre of depth (Foot): a crease fades out from CreaseFoot0 to CreaseFoot1 m per pixel.
        public static float Edge(float c,float l,float r,float d,float u,float L,float R,float D,float U,float strength,float fadeStart,float fadeEnd,float crease,float foot)
        {
            float gap=GapMin+GapShare*c;float far=Math.Max(l+r,d+u)-2*c,wide=Math.Max(L+R,D+U)-2*c;float sil=far>=gap||wide>=WideGap*gap?1:0;
            float ic=1/c,cx=Math.Abs(1/l+1/r-2*ic),cy=Math.Abs(1/d+1/u-2*ic);float near=c-Math.Min(Math.Min(l,r),Math.Min(d,u));float bend=near<=gap?Smooth(crease,crease*2,Math.Max(cx,cy)/ic)*(1-Smooth(CreaseFoot0,CreaseFoot1,c*foot)):0;
            float fade=1-Smooth(fadeStart,fadeEnd,c);return Sat(Math.Max(sil,bend*CreaseWeight))*fade*strength;
        }
    }
}
