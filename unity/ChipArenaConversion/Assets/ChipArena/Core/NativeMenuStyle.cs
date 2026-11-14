using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The anime style on the menus and 3D backdrops (docs/MAP-STYLE-ANIME.md section 6, stage 2): the home screen's rooftop,
    /// the chip draft's corridor, the menu portraits and the 2D screens' palette. Presentation only: nothing here is read by the
    /// simulation, the authority, navigation, the network frame or a save. The same switch as the maps: the
    /// `--ca-map-style legacy` command line (NativeMapStyle.LegacyArgument) draws every one of them in its legacy look. The
    /// voxel pilots are left to the BoZo swap (docs/BOZO-INTEGRATION.md); the cool, saturated shade palette is the maps'.
    /// Pure; the per-frame helpers allocate nothing. The shaders' constants are mirrored here and checked
    /// (Tests/MenuStyleChecks.cs).
    /// </summary>
    public static class NativeMenuStyle
    {
        // ---------------- the home screen's rooftop ----------------
        /// <summary>
        /// The rooftop's own toon values (NativeLighting.Rooftop writes them through NativeMapStyle.State, as Apply writes a
        /// map's): a night look, so the key the pilots' ramp reads is the rooftop's magenta neon key, the shade the cool tint;
        /// lines from 60 m out to 320 m (the city lies far below), the menu's grade. The look carries no light, ambient, sky or
        /// fog override: NativeLighting.Rooftop keeps writing the night's own. The sky values feed the rooftop's own sky
        /// material (RooftopSky): `clouds` the cumulus cover, `cloudScale` their size, `bandWidth` the layers' base elevation
        /// (radians), `cloudLit` the city-lit underside, `cloudShade` the tops.
        /// </summary>
        public static readonly MapLook Rooftop=new MapLook{id="rooftop",look="rooftop",toon=RooftopToon()};
        private static MapLookToon RooftopToon()=>new MapLookToon
        {
            on=true,softness=.025f,shadeEdge=.03f,lightEdge=.2f,halfTone=0,round=.1f,flatten=.35f,lift=.25f,points=.7f,rimIntensity=.6f,edgeLight=.5f,
            shade=new V3(.62f,.74f,1.38f),lit=new V3(1.06f,1f,.96f),rimColor=0x9db8ff,
            detail=.3f,gradient=.14f,wash=.06f,baseShadow=0,plinths=0,copings=0,chips=0,posts=0,specular=.55f,specularEdge=.965f,occlusion=.4f,occlusionHeight=.22f,
            line=1,lineFadeStart=60,lineFadeEnd=320,crease=.02f,lineColor=0x2a1e40,
            saturation=1.12f,contrast=1.05f,gradeShadow=new V3(.94f,.96f,1.08f),gradeLight=new V3(1.04f,1f,.96f),
            skyToon=1,clouds=.55f,cloudScale=1.1f,bandWidth=.15f,cloudLit=0x6a58a8,cloudShade=0x1a2452,horizonBand=0x3b4486,
        };
        /// <summary>The rooftop's haze globals (_RoofToonFog): x how far the aerial haze steps into its depth planes (0 smooth,
        /// 1 four soft planes), y its density per metre, z the street glow's height (m), w its strength.</summary>
        public static readonly Vector4 RoofFog=new Vector4(.6f,.0011f,48,.6f);
        /// <summary>NativeRooftopToon.cginc's constants (linear): the sky's zenith, upper sky and horizon, the city glow's magenta and
        /// cyan sides, the street haze; the towers' shade, lit and roof tones, the facades' key, the windows' gain; the props'
        /// albedo lift and their two ambient bands.</summary>
        public static readonly Vector3 Zenith=new Vector3(.0021f,.0037f,.018f),Upper=new Vector3(.006f,.011f,.056f),Horizon=new Vector3(.03f,.036f,.155f),
            GlowMagenta=new Vector3(.125f,.035f,.21f),GlowCyan=new Vector3(.022f,.095f,.24f),StreetHaze=new Vector3(.042f,.024f,.13f),
            FacadeShade=new Vector3(.009f,.014f,.06f),FacadeLit=new Vector3(.02f,.03f,.125f),Roof=new Vector3(.025f,.036f,.15f),FacadeKey=new Vector3(.37f,.55f,.75f),
            AmbientUp=new Vector3(.44f,.54f,1.15f),AmbientSide=new Vector3(.28f,.34f,.76f);
        /// <summary>The windows' gain and the run of windows a lit floor lights together; the props' albedo lift and how hard it
        /// is compressed (a/(1+a k)); the haze's share of the city glow.</summary>
        public const float WindowGain=.46f,WindowRun=6,PropLift=2.6f,PropFlatten=4,HazeGlow=.3f;
        /// <summary>Whether the rooftop draws in the anime style (the legacy command line turns it off).</summary>
        public static bool RooftopOn(bool legacy)=>NativeMapStyle.On(Rooftop,legacy);
        /// <summary>The rooftop sky material's values (NativeRooftopSky: _RoofSkyToon, _RoofSkyLit, _RoofSkyShade; linear) for a state
        /// NativeMapStyle.State filled from Rooftop: cover, size and the layers' base elevation; the lit and shade tones.</summary>
        public static void RooftopSky(MapStyleState s,out Vector4 toon,out Vector4 lit,out Vector4 shade)
        {
            toon=new Vector4(s.SkyToon[0],s.SkyToon[1],s.SkyToon[2],s.SkyToon[3]);lit=new Vector4(s.CloudLit[0],s.CloudLit[1],s.CloudLit[2],0);shade=new Vector4(s.CloudShade[0],s.CloudShade[1],s.CloudShade[2],0);
        }
        /// <summary>The haze's depth planes (RoofToonPlanes): four soft stairs of the aerial haze's share.</summary>
        public static float Planes(float x){float k=x*4;float f=(float)Math.Floor(k);return (f+Smooth(.3f,.7f,k-f))/4;}
        /// <summary>RoofToonFog's haze share at eye distance z (m) and height y over the street (m), before the cap.</summary>
        public static float Haze(float z,float y,Vector4 fog)
        {
            float aerial=1-(float)Math.Exp(-z*fog.Y);aerial+=(Planes(aerial)-aerial)*fog.X;float low=(float)Math.Exp(-Math.Max(0,y)/fog.Z)*(1-(float)Math.Exp(-z/90));
            return Math.Min(.95f,aerial+low*fog.W);
        }

        // ---------------- the chip draft's corridor (Resources/NativeDraftBackdrop.shader) ----------------
        /// <summary>The draft backdrop's switch (_BackToon: x 1 the anime style, 0 its legacy look), set once with its material.</summary>
        public static Vector4 DraftToon(bool legacy)=>new Vector4(legacy?0:1,0,0,0);
        /// <summary>The shader's BACK_TOON_* constants: the depth the panels' cel bands of light span (m); the height the panels'
        /// tones step over (m); the ink's half width (pixels); the soft bands' count and riser; the fog where the bands start and
        /// finish easing into smooth gradients (no contour closes behind the pilot's head; there is no back wall); the panels' two
        /// flat tones and the floor's (times the navy); how far the banded light lifts them; the wall heights (m) over which the
        /// flat tones ease into the legacy navy (lit low, navy by eye height, so the text-safe band's cap never cuts a shape out of
        /// them); the ink's tint (linear).</summary>
        public const float DraftReach=52,DraftPanelHigh=3.2f,DraftLine=1,DraftBandCount=4,DraftRiser=.15f,DraftSoftNear=.3f,DraftSoftFar=.6f,DraftPanelA=1.5f,DraftPanelB=2.1f,DraftFloor=1.3f,DraftLight=1.4f,DraftFadeLow=.7f,DraftFadeHigh=1.5f;
        public static readonly Vector3 DraftInk=new Vector3(.18f,.2f,.42f);
        /// <summary>BackBands: x (0..1) in n flat bands with soft risers; 0 maps to 0 and 1 to 1, never decreasing.</summary>
        public static float Bands(float x,float n){float k=Math.Max(0,Math.Min(1,x))*n;float f=(float)Math.Floor(k);return Math.Min(1,(f+Smooth(.5f-DraftRiser,.5f+DraftRiser,k-f))/n);}
        /// <summary>The corridor's bands down its length: Bands near the pilot, easing into x itself as the fog thickens
        /// (BACK_TOON_SOFT_*), as the shader's lerp(BackBands(x,n),x,soft).</summary>
        public static float SoftBands(float x,float n,float fog){float s=Smooth(DraftSoftNear,DraftSoftFar,fog);return Bands(x,n)*(1-s)+Math.Max(0,Math.Min(1,x))*s;}
        /// <summary>How far a wall point at this height has eased from the flat tones into the legacy navy (0 lit, 1 navy).</summary>
        public static float DraftFade(float height)=>Smooth(DraftFadeLow,DraftFadeHigh,height);

        // ---------------- the menu portraits (Resources/NativePortrait.shader, NativePortraitResolve.shader) ----------------
        /// <summary>Whether the portraits draw the anime style (the toon podium, the resolve's outline and gradient).</summary>
        public static bool PortraitToon(bool legacy)=>!legacy;
        /// <summary>The line's tint (linear; the maps' lineColor #2A1E40), the gradient's opacity at the podium and the height
        /// over the podium's top it fades out by (m); the share of the backdrop's tint over the cool shade at its top and added
        /// toward its foot.</summary>
        public static readonly Vector3 PortraitLine=new Vector3(.0232f,.013f,.0513f);
        public const float PortraitGradient=.38f,PortraitGradientRise=1.1f,PortraitTintLow=.12f,PortraitTintHigh=.3f;
        /// <summary>The toon podium's display tones (the shader's PODIUM_*): top lit and in shade, side, foot; the key's band edge
        /// on N.L, the rim band's inner edge (share of the top radius), its lift toward white and its level.</summary>
        public static readonly Vector3 PodiumTopLit=new Vector3(.062f,.074f,.15f),PodiumTopShade=new Vector3(.04f,.048f,.105f),PodiumSide=new Vector3(.026f,.03f,.066f),PodiumFoot=new Vector3(.012f,.014f,.032f);
        public const float PodiumEdge=.22f,PodiumRim=.955f,PodiumRimWhite=.15f,PodiumRimLevel=.85f;
        /// <summary>The cool shade under the gradient (the resolve's PORTRAIT_SHADE, linear #2A3460).</summary>
        public static readonly Vector3 PortraitShade=new Vector3(.023f,.034f,.117f);
        /// <summary>The resolve's line on a texel of coverage a with the lowest neighbour coverage n1 one width away and n2 two widths
        /// away (0..1): the silhouette on the covered side (none on an empty texel, none across a flat coverage).</summary>
        public static float PortraitEdge(float a,float n1,float n2)=>Math.Max(Smooth(.2f,.45f,a-n1),.7f*Smooth(.45f,.75f,a-n2))*Smooth(.25f,.55f,a);
        /// <summary>The resolve's gradient opacity at row y (uv, 0 bottom) for the podium's row and the top row.</summary>
        public static float PortraitGradientAt(float y,float podiumRow,float topRow){float x=1-(y-podiumRow)/Math.Max(topRow-podiumRow,.001f);float k=Math.Max(0,Math.Min(1,x))*3;float f=(float)Math.Floor(k);return Math.Min(1,(f+Smooth(.35f,.65f,k-f))/3)*PortraitGradient;}

        // ---------------- the 2D screens' palette (Runtime NativeCyberTheme.Night, Dusk) ----------------
        /// <summary>The night navy the full-screen covers take (the transition shutters' body, the loading screen's reduced cover,
        /// the Expeditions screen's veil and card bodies) and the dusk of their washes: the cool shade family (#2A3460 is the
        /// maps' and the rooftop's shade) instead of the near-black void (#05070F) and navy (#0A1024).</summary>
        public const int NightHex=0x0e1430,DuskHex=0x1c2654;

        // ---------------- shared maths ----------------
        private static float Smooth(float a,float b,float x){float t=Math.Max(0,Math.Min(1,(x-a)/(b-a)));return t*t*(3-2*t);}
    }
}
