#if ARENA_TESTS
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The anime style on the menus and 3D backdrops, stage 2 (docs/MAP-STYLE-ANIME.md section 6; Core/NativeMenuStyle.cs): the
    // home screen's rooftop (its own toon values through NativeLighting.Rooftop, Resources/NativeRooftopToon.cginc and the four
    // rooftop shaders, the outline on the menu camera), the chip draft's corridor (NativeDraftBackdrop.shader's _BackToon path),
    // the menu portraits (the toon podium, the resolve pass's outline and gradient in the backdrop's tint) and the 2D covers'
    // night palette. Checked: the switch (on by default, every piece off under the legacy command line), the shaders' constants
    // and formulas against the Core, every legacy path kept behind its one branch, the bands' and the outline's maths, the
    // runtime hooks, nothing allocated per frame, the metas. Rendering itself (D3D11, the look) is not run here: the offline
    // renders are (scratch), the Windows build is the owner's.
    internal static class MenuStyleChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Menu style: "+what);count++;}
        private static string Code(string text)=>string.Join("\n",text.Split('\n').Select(l=>{int c=l.IndexOf("//",StringComparison.Ordinal);return c<0?l:l.Substring(0,c);}));
        private static string Body(string code,string head)
        {
            int at=code.IndexOf(head,StringComparison.Ordinal);if(at<0)return "";int open=code.IndexOf('{',at);if(open<0)return "";
            int depth=0;for(int i=open;i<code.Length;i++){if(code[i]=='{')depth++;else if(code[i]=='}'&&--depth==0)return code.Substring(open,i-open+1);}
            return "";
        }
        private static float F(string s)=>float.Parse(s,CultureInfo.InvariantCulture);
        private static float Define(string shader,string name){var m=Regex.Match(shader,@"#define "+name+@" ([-0-9.]+)\r?\n");Check(m.Success,"define "+name);return F(m.Groups[1].Value);}
        private static Vector3 Define3(string shader,string name){var m=Regex.Match(shader,@"#define "+name+@" float3\(([-0-9.]+),([-0-9.]+),([-0-9.]+)\)");Check(m.Success,"define "+name);return new Vector3(F(m.Groups[1].Value),F(m.Groups[2].Value),F(m.Groups[3].Value));}
        private static bool Near(float a,float b,float e=1e-5f)=>Math.Abs(a-b)<=e;
        private static bool Near(Vector3 a,Vector3 b)=>Near(a.X,b.X)&&Near(a.Y,b.Y)&&Near(a.Z,b.Z);
        public static void Run(string root)
        {
            string R(string p)=>File.ReadAllText(Path.Combine(root,p));
            Switch();Console.WriteLine("PASS menu style: the switch (the rooftop's own look validates and turns the toon, outline, grade and sky on; the draft, the portraits and the 2D covers follow it; the legacy command line turns every one off) ("+count+" checks)");
            Rooftop(R);Console.WriteLine("PASS menu style: the rooftop's shaders draw the toon sky, haze, facades, props and puddles behind the one switch with the Core's constants, keep every legacy path, mark the outline and cast into the depth texture ("+count+" checks)");
            Draft(R);Console.WriteLine("PASS menu style: the draft corridor's flat panels (lit low, navy by eye height), cel bands easing into smooth gradients and ink sit behind _BackToon, with no back wall, with the Core's constants; the text-safe bands and the cap apply after them ("+count+" checks)");
            Portraits(R);Console.WriteLine("PASS menu style: the portraits' toon podium and the resolve's outline and gradient (the backdrop's tint) behind the switch; the line rule and the gradient's bands ("+count+" checks)");
            Maths();Console.WriteLine("PASS menu style: the bands, the haze's planes and the portrait line and gradient (monotonic, flat plateaus, no line across flat coverage) ("+count+" checks)");
            Runtime(R);Console.WriteLine("PASS menu style: NativeLighting.Rooftop writes the rooftop's values, the presentations set theirs once, the outline stays the match camera's, the 2D covers take the night palette, metas ("+count+" checks)");
            Allocation();Console.WriteLine("PASS menu style: the rooftop's state, the bands and the portrait helpers allocate nothing per frame ("+count+" checks)");
        }
        // ---- the switch ----
        private static void Switch()
        {
            var look=NativeMenuStyle.Rooftop;var t=look.toon;NativeMapStyle.Validate(look.id,t);
            Check(NativeMenuStyle.RooftopOn(false)&&!NativeMenuStyle.RooftopOn(true)&&NativeMapStyle.On(look,false)&&!NativeMapStyle.On(look,true),"the rooftop's style: on, off under the legacy command line");
            Check(!NativeMapStyle.Has(t.key)&&!NativeMapStyle.Has(t.rim)&&!NativeMapStyle.Has(t.ambient)&&!NativeMapStyle.Has(t.sky)&&t.fogColor==0&&t.fogStart==0&&t.background==0,"the rooftop's look overrides no light, ambient, sky or fog (NativeLighting.Rooftop keeps its night)");
            var s=new MapStyleState();NativeMapStyle.State(look,false,true,s);
            Check(s.On&&s.Toon[0]==1&&s.Outline&&s.Edge[0]==1&&s.Edge[1]==60&&s.Edge[2]==320&&s.Grade&&s.Sky&&s.Wrap.All(v=>v==0),"the rooftop's state: the ramp, the outline from 60 to 320 m, the grade and the sky; no wrap (no blocks)");
            Check(t.shade.z>t.shade.x&&t.shade.z>t.shade.y&&t.lit.x>=t.lit.z&&t.lineColor==0x2a1e40,"the cool shade and the maps' line colour");
            NativeMapStyle.State(look,true,true,s);Check(!s.On&&s.Toon.All(v=>v==0)&&s.Edge.All(v=>v==0)&&s.GradeParams[3]==0&&s.SkyToon.All(v=>v==0),"legacy: every rooftop global 0");
            NativeMapStyle.State(look,false,false,s);Check(s.On&&!s.Outline&&s.Edge[0]==0,"the low tier: no outline");
            Check(NativeMenuStyle.DraftToon(false).X==1&&NativeMenuStyle.DraftToon(true).X==0,"the draft: on, off under the legacy command line");
            Check(NativeMenuStyle.PortraitToon(false)&&!NativeMenuStyle.PortraitToon(true),"the portraits: on, off under the legacy command line");
            NativeMapStyle.State(look,false,true,s);NativeMenuStyle.RooftopSky(s,out var sky,out var lit,out var shade);
            Check(sky.X==1&&Near(sky.Y,t.clouds)&&Near(sky.Z,t.cloudScale)&&Near(sky.W,t.bandWidth)&&lit.Z>lit.X&&shade.Z>shade.X&&lit.X+lit.Y+lit.Z>shade.X+shade.Y+shade.Z,"the rooftop sky's values: cover, size, base; a lit underside over a darker cool top");
        }
        // ---- the rooftop's shaders ----
        private static void Rooftop(Func<string,string> R)
        {
            string res="Assets/ChipArena/Resources/";string inc=R(res+"NativeRooftopToon.cginc"),common=R(res+"NativeRooftopCommon.cginc"),world=R(res+"NativeRooftop.shader"),sky=R(res+"NativeRooftopSky.shader"),screen=R(res+"NativeRooftopScreen.shader"),sprite=R(res+"NativeRooftopSprite.shader");
            foreach(var (name,v) in new[]{("ZENITH",NativeMenuStyle.Zenith),("UPPER",NativeMenuStyle.Upper),("HORIZON",NativeMenuStyle.Horizon),("GLOW_MAGENTA",NativeMenuStyle.GlowMagenta),("GLOW_CYAN",NativeMenuStyle.GlowCyan),("STREET",NativeMenuStyle.StreetHaze),
                ("FACADE_SHADE",NativeMenuStyle.FacadeShade),("FACADE_LIT",NativeMenuStyle.FacadeLit),("ROOF",NativeMenuStyle.Roof),("KEY",NativeMenuStyle.FacadeKey),("AMB_UP",NativeMenuStyle.AmbientUp),("AMB_SIDE",NativeMenuStyle.AmbientSide)})
                Check(Near(Define3(inc,"ROOF_TOON_"+name),v),"ROOF_TOON_"+name+" is NativeMenuStyle's");
            Check(Define(inc,"ROOF_TOON_WINDOW")==NativeMenuStyle.WindowGain&&Define(inc,"ROOF_TOON_RUN")==NativeMenuStyle.WindowRun&&Define(inc,"ROOF_TOON_LIFT")==NativeMenuStyle.PropLift&&Define(inc,"ROOF_TOON_FLATTEN")==NativeMenuStyle.PropFlatten&&Define(inc,"ROOF_TOON_HAZE_GLOW")==NativeMenuStyle.HazeGlow,"the rooftop's scalar constants are NativeMenuStyle's");
            // the palette: a dark blue city (the towers' tones blue-dominant and dark, the shade family of #2A3460), never near-black on the props
            foreach(var v in new[]{NativeMenuStyle.FacadeShade,NativeMenuStyle.FacadeLit,NativeMenuStyle.Roof,NativeMenuStyle.Horizon,NativeMenuStyle.Upper})Check(v.Z>v.X*2&&v.Z>v.Y*1.5f&&v.Z<.3f,"a cool dark blue tone ("+v+")");
            Check(NativeMenuStyle.FacadeLit.Z>NativeMenuStyle.FacadeShade.Z&&NativeMenuStyle.Roof.Z>=NativeMenuStyle.FacadeLit.Z,"two tones on the towers, a third on the roofs");
            Check(inc.Contains("float RoofToonPlanes(float x){float k=x*4.0;float f=floor(k);return (f+smoothstep(0.3,0.7,k-f))/4.0;}")&&inc.Contains("aerial=lerp(aerial,RoofToonPlanes(aerial),_RoofToonFog.x);"),"the haze's planes are NativeMenuStyle.Planes");
            Check(Regex.Matches(inc,"ROOFTOP TOON SHARED (BEGIN|END)").Count==2,"the shared block markers (the offline renders translate it)");
            // every legacy function and frag branches once on the switch; the legacy code follows unchanged
            Check(common.Contains("#include \"NativeToon.cginc\"\n#include \"NativeRooftopToon.cginc\"")&&common.IndexOf("#include \"NativeRooftopToon.cginc\"",StringComparison.Ordinal)>common.IndexOf("float RoofHash1(",StringComparison.Ordinal),"Common includes the switch and the toon block after its hashes");
            Check(Body(common,"float3 RoofSkyBase(float3 d)").Contains("if(_ArenaToon.x>0.5)return RoofToonSkyBase(d);\n    float h=d.y;float3 zen=float3(.002,.003,.012),mid=float3(.010,.010,.034),hor=float3(.085,.018,.080);"),"the legacy sky after one branch");
            Check(Body(common,"float3 RoofHaze(float3 d,float y)").Contains("if(_ArenaToon.x>0.5)return RoofToonHaze(d,y);\n    float3 c=RoofSkyBase(normalize(float3(d.x,max(d.y,0.0)*.3+.02,d.z)));"),"the legacy haze after one branch");
            Check(Body(common,"float3 RoofFog(float3 c,float3 w)").Contains("if(_ArenaToon.x>0.5)return RoofToonFog(c,w);\n    float3 v=w-_WorldSpaceCameraPos;float z=length(v);float3 d=v/max(z,1e-4);float aerial=1.0-exp(-z*0.0013);"),"the legacy fog after one branch");
            Check(world.Contains("if(_ArenaToon.x>0.5&&(mode<1.5||mode>3.5))c=RoofToonSurface(i.uv0,i.uv1,i.world,n,fw);\n                else if(mode<.5)")&&world.Contains("c=float3(.006,.008,.020)+float3(.004,.003,.008)*RoofHash1(float3(seed,seed,seed));")&&world.Contains("c=alb*(1.0+amb*2.0)+alb*ToonPoints(sign+cy)*3.0+refl*fres*wet*.16;"),"the world: the toon surfaces behind the switch, the legacy facades and props kept");
            // the window grid's derivative is taken once before the mode branches, none inside them, and the ring loops unroll
            string wfrag=Body(world,"float4 frag(v2f i):SV_Target");int wd=wfrag.IndexOf("float2 fw=fwidth(i.uv0.xy/float2(1.5,2.4));",StringComparison.Ordinal);
            Check(wd>0&&wd<wfrag.IndexOf("if(_ArenaToon.x>0.5",StringComparison.Ordinal)&&Regex.Matches(wfrag,@"\b(ddx|ddy|fwidth)\b").Count==1&&!Regex.IsMatch(Body(world,"float3 RoofToonSurface("),@"\b(ddx|ddy|fwidth)\b"),"the world takes its one derivative before any branch");
            Check(Regex.Matches(world,@"for\(int k").Count==2&&Regex.Matches(world,@"\[unroll\] for\(int k").Count==2,"the puddle rings' loops unroll in both looks");
            Check(world.Contains("Stencil { Ref 16 WriteMask 16 Comp Always Pass Replace }")&&Regex.Matches(world,"Stencil").Count==1&&world.Contains("Tags { \"LightMode\"=\"ShadowCaster\" }")&&world.Contains("#pragma multi_compile_shadowcaster")&&world.Contains("SHADOW_CASTER_FRAGMENT(i)"),"the world marks the environment for the outline and casts into the depth texture (its shadow caster pass, no stencil)");
            Check(sky.Contains("if(_ArenaToon.x>0.5)c=RoofToonSky(d,c);\n                else\n                {")&&sky.Contains("float dens=smoothstep(.45,.75,cl)*smoothstep(0.0,.08,d.y)*smoothstep(.5,.15,d.y);c=lerp(c,float3(.10,.03,.10)+float3(.02,.06,.08)*cl,dens*.7);")&&sky.Contains("c+=float3(.25,.55,.7)*.10*exp(-bd*bd*900.0)*smoothstep(0.0,.2,d.y);"),"the sky: the cumulus behind the switch, the legacy deck kept, the searchlights in both");
            Check(sky.Contains("float4 _RoofSkyToon,_RoofSkyLit,_RoofSkyShade;")&&Regex.Matches(sky,@"RoofToonCumulus\(").Count==3,"three cumulus layers from the sky material's values");
            Check(screen.Contains("if(_ArenaToon.x>0.5){scan=lerp(scan,1.0,.65);")&&screen.Contains("float scan=.82+.18*sin(i.uv.y*900.0);"),"the screens: flatter scanlines and a crisp edge behind the switch");
            Check(sprite.Contains("if(_ArenaToon.x>0.5)a=smoothstep(.3,.36,a)*.72;}")&&sprite.Contains("p+=v.uv1.xyz*ph*v.uv1.w;"),"the sprites: crisp mist and steam behind the switch, every motion kept");
            Check(Regex.Matches(inc+world+sky,@"\b(line|point|triangle)\s*=").Count==0,"no HLSL reserved word as a variable");
        }
        // ---- the draft corridor ----
        private static void Draft(Func<string,string> R)
        {
            string shader=R("Assets/ChipArena/Resources/NativeDraftBackdrop.shader");
            Check(Define(shader,"BACK_TOON_REACH")==NativeMenuStyle.DraftReach&&Define(shader,"BACK_TOON_BANDS")==NativeMenuStyle.DraftBandCount&&Define(shader,"BACK_TOON_RISER")==NativeMenuStyle.DraftRiser&&Define(shader,"BACK_TOON_PANEL_A")==NativeMenuStyle.DraftPanelA
                &&Define(shader,"BACK_TOON_PANEL_B")==NativeMenuStyle.DraftPanelB&&Define(shader,"BACK_TOON_FLOOR")==NativeMenuStyle.DraftFloor&&Define(shader,"BACK_TOON_LIGHT")==NativeMenuStyle.DraftLight&&Near(Define3(shader,"BACK_TOON_INK"),NativeMenuStyle.DraftInk),"the corridor's toon constants are NativeMenuStyle's");
            Check(Define(shader,"BACK_TOON_PANEL_HIGH")==NativeMenuStyle.DraftPanelHigh&&Define(shader,"BACK_TOON_LINE")==NativeMenuStyle.DraftLine&&Define(shader,"BACK_TOON_SOFT_NEAR")==NativeMenuStyle.DraftSoftNear&&Define(shader,"BACK_TOON_SOFT_FAR")==NativeMenuStyle.DraftSoftFar
                &&Define(shader,"BACK_TOON_FADE_LOW")==NativeMenuStyle.DraftFadeLow&&Define(shader,"BACK_TOON_FADE_HIGH")==NativeMenuStyle.DraftFadeHigh,"the soft bands', the fade's and the ink's constants are NativeMenuStyle's");
            Check(shader.Contains("float BackBands(float x,float n){float k=saturate(x)*n;float f=floor(k);return min(1.0,(f+smoothstep(.5-BACK_TOON_RISER,.5+BACK_TOON_RISER,k-f))/n);}"),"BackBands is NativeMenuStyle.Bands");
            var names=Regex.Match(shader,@"float4 (_Back[^;]*);").Groups[1].Value.Split(',').Select(x=>x.Trim()).ToArray();Check(names.Length==16&&!names.Contains("_BackToon")&&shader.Contains("            float4 _BackToon;\n"),"the 16 frame uniforms unchanged, the switch declared apart");
            int a=shader.IndexOf("// ---- DRAFT BACKDROP SHARED BEGIN",StringComparison.Ordinal),z=shader.IndexOf("// ---- DRAFT BACKDROP SHARED END",StringComparison.Ordinal);string shared=shader.Substring(a,z-a);
            Check(Regex.Matches(shared,@"if\(toon>0\.5\)").Count==2&&shared.Contains("float toon=_BackToon.x;"),"every toon term behind the one switch");
            // The owner's review (2026-10-22): no back wall (it read as a lit rectangle behind the pilot's head); the far end is the
            // legacy haze, the bands ease into smooth gradients down the corridor and the flat tones fade to navy by eye height.
            Check(!Regex.IsMatch(shared,@"\b(onBack|BACK_TOON_WALL\w*|BACK_TOON_SKY|BACK_TOON_GLOW)\b")&&!shared.Contains("(-BACK_TOON_REACH-eye.z)"),"no back wall: nothing closes the corridor behind the pilot");
            Check(shared.Contains("float soft=smoothstep(BACK_TOON_SOFT_NEAR,BACK_TOON_SOFT_FAR,fog);float reach=saturate(depth/BACK_TOON_REACH);")&&shared.Contains("float lightB=lerp(BackBands(reach,BACK_TOON_BANDS),reach,soft);")
                &&shared.Contains("fog=lerp(BackBands(fog,BACK_TOON_BANDS),fog,soft);")&&shared.IndexOf("float soft=",StringComparison.Ordinal)<shared.IndexOf("fog=lerp(BackBands(fog",StringComparison.Ordinal),"the light and the haze ease from bands into smooth gradients by the fog (NativeMenuStyle.SoftBands), the ease read before the fog is banded");
            Check(shared.Contains("nearC=lerp(toned,nearC,smoothstep(BACK_TOON_FADE_LOW,BACK_TOON_FADE_HIGH,pw.y)*(1.0-onFloor));")&&shared.Contains("float3 haze=_BackDeep.rgb*(1.0+1.6*axis)+A*(.004+.03*axis)*pulseW;")&&!shared.Contains("haze=lerp(")&&!shared.Contains("haze+="),
                "the flat tones fade to the legacy navy by eye height (NativeMenuStyle.DraftFade); the far haze is the legacy one, untouched");
            Check(!Regex.IsMatch(shared,@"\b(fmod|atan2|mul|static|ddx|ddy|rsqrt)\b"),"the shared block stays GLSL-portable");
            int toonAt=shared.IndexOf("if(toon>0.5)\n",StringComparison.Ordinal),fogAt=shared.IndexOf("float3 c=lerp(nearC,haze,fog);detail*=1.0-fog*.85;",StringComparison.Ordinal),capAt=shared.IndexOf("float peak=max(c.r,max(c.g,c.b));return c*min(1.0,cap/max(peak,1e-6));",StringComparison.Ordinal),safeAt=shared.IndexOf("float safe=lerp(_BackSafe.z,1.0,hs)*lerp(_BackSafe.w,1.0,tb)",StringComparison.Ordinal);
            Check(toonAt>0&&fogAt>toonAt&&safeAt>fogAt&&capAt>safeAt,"the toon terms come before the haze mix; the text-safe bands and the cap apply after them, as before");
            Check(shared.Contains("float halo=exp(-dot(gd,gd));if(toon>0.5)halo=BackBands(halo,3.0);detail+=A*_BackStyle.w*halo*(.85+.15*pulseW);")&&shared.Contains("detail=max(detail-S*(seam*.022*pulseW+rib*.022)*(1.0-onFloor)*boot,0.0);"),"the glow in bands; the seams and ribs turn to ink, never below zero");
        }
        // ---- the portraits ----
        private static void Portraits(Func<string,string> R)
        {
            string res="Assets/ChipArena/Resources/";string lit=R(res+"NativePortrait.shader"),resolve=R(res+"NativePortraitResolve.shader");
            Check(Near(Define3(lit,"PODIUM_TOP_LIT"),NativeMenuStyle.PodiumTopLit)&&Near(Define3(lit,"PODIUM_TOP_SHADE"),NativeMenuStyle.PodiumTopShade)&&Near(Define3(lit,"PODIUM_SIDE"),NativeMenuStyle.PodiumSide)&&Near(Define3(lit,"PODIUM_FOOT"),NativeMenuStyle.PodiumFoot)
                &&Define(lit,"PODIUM_EDGE")==NativeMenuStyle.PodiumEdge&&Define(lit,"PODIUM_RIM")==NativeMenuStyle.PodiumRim&&Define(lit,"PODIUM_RIM_WHITE")==NativeMenuStyle.PodiumRimWhite&&Define(lit,"PODIUM_RIM_LEVEL")==NativeMenuStyle.PodiumRimLevel,"the podium's tones are NativeMenuStyle's");
            Check(NativeMenuStyle.PodiumTopLit.Z>NativeMenuStyle.PodiumTopShade.Z&&NativeMenuStyle.PodiumTopShade.Z>NativeMenuStyle.PodiumSide.Z&&NativeMenuStyle.PodiumSide.Z>NativeMenuStyle.PodiumFoot.Z,"the podium: lit top, shade, side, foot, darker in turn");
            Check(lit.Contains("if(_Mode>2.5)color=ToonPodium(i.obj,normalize(i.normal));\n    else if(basic)color=_Mode>.5?albedo:emission;")&&lit.Contains("if(_Mode<1.5)color=Aces(color);else color=saturate(color);"),"mode 3 is the podium's alone; every other mode keeps its code (the podium's tones are display colours, not tone mapped)");
            Check(Near(Define3(resolve,"PORTRAIT_SHADE"),NativeMenuStyle.PortraitShade)&&Define(resolve,"PORTRAIT_TINT_LOW")==NativeMenuStyle.PortraitTintLow&&Define(resolve,"PORTRAIT_TINT_HIGH")==NativeMenuStyle.PortraitTintHigh,"the gradient's shade and tint shares are NativeMenuStyle's");
            Check(resolve.Contains("float ink=max(smoothstep(0.2,0.45,c.a-n1),0.7*smoothstep(0.45,0.75,c.a-n2))*smoothstep(0.25,0.55,c.a);")&&resolve.Contains("float PortraitBands(float x){float k=saturate(x)*3.0;float f=floor(k);return min(1.0,(f+smoothstep(0.35,0.65,k-f))/3.0);}"),"the resolve's line and bands are NativeMenuStyle.PortraitEdge and PortraitGradientAt");
            Check(resolve.Contains("if(_PortraitToon.x>0.5)c=PortraitToon(c,i.uv);")&&resolve.Contains("if(c.a>.0001)return float4(saturate(c.rgb/c.a),c.a);")&&resolve.Contains("return around.a>.0001?float4(saturate(around.rgb/around.a),0):float4(0,0,0,0);"),"the resolve: the toon step behind the switch; the straight alpha and the edge dilation kept");
            Check(resolve.Contains("return c+float4(gc*g,g)*(1.0-c.a);"),"the gradient goes under the pilot (premultiplied over), so the pilot's own coverage draws the line");
        }
        // ---- the maths ----
        private static void Maths()
        {
            foreach(float n in new[]{3f,4f})
            {
                float prev=-1;for(int i=0;i<=400;i++){float x=i/400f,b=NativeMenuStyle.Bands(x,n);Check(b>=prev-1e-6f&&b>=0&&b<=1,"Bands is monotonic in 0..1");prev=b;}
                Check(NativeMenuStyle.Bands(0,n)==0&&Math.Abs(NativeMenuStyle.Bands(1,n)-1)<1e-6f,"Bands keeps 0 and 1");
                for(int k=0;k<n;k++){float mid=(k+.05f)/n,mid2=(k+.25f)/n;Check(Math.Abs(NativeMenuStyle.Bands(mid,n)-NativeMenuStyle.Bands(mid2,n))<1e-6f,"a flat plateau in band "+k);}
            }
            // the draft corridor down its length (fog as the shader's, 1-exp(-0.032 t)): never dipping more than a quarter of an
            // 8-bit step (0.001) below what nearer points reached (the ease from bands to smooth dips by at most 0.0006), bands near
            // the pilot, exactly smooth past the ease; the fade lit low and navy by eye height
            {float pl=-1,pf=-1;for(int i=0;i<=4000;i++){float t=i/10f,fog=1-(float)Math.Exp(-.032*t),l=NativeMenuStyle.SoftBands(t/NativeMenuStyle.DraftReach,NativeMenuStyle.DraftBandCount,fog),f=NativeMenuStyle.SoftBands(fog,NativeMenuStyle.DraftBandCount,fog);
                Check(l>=pl-1e-3f&&f>=pf-1e-3f&&l>=0&&l<=1&&f>=0&&f<=1,"the corridor's light and haze grow down its length");pl=Math.Max(pl,l);pf=Math.Max(pf,f);
                if(fog>=NativeMenuStyle.DraftSoftFar)Check(Math.Abs(f-fog)<1e-6f&&Math.Abs(l-Math.Min(1,t/NativeMenuStyle.DraftReach))<1e-6f,"past the ease, smooth: no band edge behind the pilot's head");}}
            Check(Math.Abs(NativeMenuStyle.SoftBands(.05f/4,4,0)-NativeMenuStyle.SoftBands(.25f/4,4,0))<1e-6f,"near the pilot, still flat bands");
            Check(NativeMenuStyle.DraftFade(0)==0&&NativeMenuStyle.DraftFade(NativeMenuStyle.DraftFadeLow)==0&&NativeMenuStyle.DraftFade(1.555f)==1&&NativeMenuStyle.DraftFadeHigh<1.555f,"the flat tones lit at the floor, the legacy navy by the draft camera's eye height (1.555 m: the text-safe band's rows)");
            {float prev=-1;for(int i=0;i<=400;i++){float b=NativeMenuStyle.Planes(i/400f);Check(b>=prev-1e-6f,"the haze's planes are monotonic");prev=b;}}
            {float prev=-1;for(int z=0;z<=3000;z+=25){float h=NativeMenuStyle.Haze(z,200,NativeMenuStyle.RoofFog);Check(h>=prev-1e-5f&&h<=.95f,"the haze grows with distance and is capped");prev=h;}}
            Check(NativeMenuStyle.Haze(10,200,NativeMenuStyle.RoofFog)<.05f&&NativeMenuStyle.Haze(1500,200,NativeMenuStyle.RoofFog)>.5f,"near props clear, the far city hazed");
            Check(NativeMenuStyle.PortraitEdge(1,1,1)==0&&NativeMenuStyle.PortraitEdge(0,0,0)==0&&NativeMenuStyle.PortraitEdge(.5f,.5f,.5f)==0,"no line across flat coverage or on an empty texel");
            Check(NativeMenuStyle.PortraitEdge(1,0,0)>.99f&&NativeMenuStyle.PortraitEdge(1,.7f,0)>.6f&&NativeMenuStyle.PortraitEdge(.1f,0,0)==0,"the silhouette's covered side is lined (one or two widths in), a faint fringe never");
            float row=.2f,top=.6f;Check(Math.Abs(NativeMenuStyle.PortraitGradientAt(.1f,row,top)-NativeMenuStyle.PortraitGradient)<1e-6f&&NativeMenuStyle.PortraitGradientAt(top+.01f,row,top)==0,"the gradient: full at and under the podium, gone over its top row");
            {float prev=2;for(int i=0;i<=200;i++){float g=NativeMenuStyle.PortraitGradientAt(i/200f,row,top);Check(g<=prev+1e-6f&&g<=NativeMenuStyle.PortraitGradient+1e-6f,"the gradient fades upward");prev=g;}}
            Check(NativeMenuStyle.PortraitGradient<.5f,"the gradient stays translucent (the card's backdrop shows through)");
        }
        // ---- the runtime ----
        private static void Runtime(Func<string,string> R)
        {
            string rt="Assets/ChipArena/Runtime/";string lighting=Code(R(rt+"NativeLighting.cs")),roof=Code(R(rt+"NativeRooftopPresentation.cs")),draft=Code(R(rt+"NativeDraftBackdropPresentation.cs")),portraits=Code(R(rt+"NativePortraits.cs")),ui=Code(R(rt+"NativePortraitUI.cs"));
            string theme=Code(R(rt+"NativeCyberTheme.cs")),hud=Code(R(rt+"NativeScreenTransitionHud.cs")),game=Code(R(rt+"ConversionGame.cs"));
            var rooftop=Body(lighting,"public static void Rooftop(Camera camera,Material skybox)");
            Check(rooftop.Contains("NativeMuzzleLights.Off();if(NativeMenuStyle.RooftopOn(LegacyStyle))StyleGlobals(NativeMenuStyle.Rooftop);else StyleOff();")&&!rooftop.Contains("Sky(")&&rooftop.Contains("moon.color=new Color(.85f,.25f,.75f);moon.intensity=.55f;"),"the rooftop writes its own toon values through StyleGlobals (or turns the style off); its night lights are unchanged");
            Check(game.Contains("outline=NativeToonOutline.Attach(cameraView);")&&game.Contains("presentation.RenderRooftop(cameraView,"),"the outline on the match camera is the menu camera's: the rooftop draws for cameraView");
            Check(roof.Contains("NativeMapStyle.State(NativeMenuStyle.Rooftop,false,true,style);NativeMenuStyle.RooftopSky(style,out var skyToon,out var skyLit,out var skyShade);")&&roof.Contains("skyMaterial.SetVector(RoofSkyToonId,V4(skyToon));skyMaterial.SetVector(RoofSkyLitId,Lin(V4(skyLit)));skyMaterial.SetVector(RoofSkyShadeId,Lin(V4(skyShade)));"),"the rooftop sky's values set once, with the scene");
            var draw=Body(roof,"public void Draw(Camera camera,float time,bool reduced)");Check(draw.Contains("Shader.SetGlobalVector(RoofToonFogId,roofToonFog);")&&!Regex.IsMatch(draw,@"\bnew MapStyleState\b"),"the haze's global each frame, nothing made per frame");
            Check(draft.Contains("draftBackMaterial.SetVector(BackToonId,BackV(NativeMenuStyle.DraftToon(NativeLighting.LegacyStyle)));")&&draft.IndexOf("BackToonId,BackV(",StringComparison.Ordinal)<draft.IndexOf("bool entering=",StringComparison.Ordinal),"the draft's switch set once with its material, outside the frame");
            Check(portraits.Contains("private static readonly bool Toon=NativeMenuStyle.PortraitToon(NativeLighting.LegacyStyle);")&&portraits.Contains("if(Toon)Podium(")&&portraits.Contains("block.SetFloat(ModeId,3);"),"the portraits' podium in mode 3 while the style is on");
            Check(portraits.Contains("resolve.SetVector(PortraitToonId,new Vector4(1,Mathf.Max(1,Mathf.Round(Screen.height/1080f)),NativeMenuStyle.PortraitGradient,row));")&&portraits.Contains("else resolve.SetVector(PortraitToonId,Vector4.zero);")&&portraits.IndexOf("resolve.SetVector(PortraitToonId",StringComparison.Ordinal)<portraits.IndexOf("Graphics.Blit(target,s.Texture,resolve);",StringComparison.Ordinal),"the resolve's values (line width per 1080 lines, the gradient from the podium's row) set before each blit; off: 0");
            Check(portraits.Contains("slot.Back=look.HasBack?look.Back:look.Accent;")&&portraits.Contains("public bool Same(in PortraitLook o)=>Spec==o.Spec&&Pose==o.Pose&&Hat==o.Hat&&Body==o.Body&&Accent==o.Accent&&Skin==o.Skin&&HatColor==o.HatColor&&BodySkin==o.BodySkin;"),"the backdrop's tint is a render input outside Same() (a new colour re-renders, never re-derives)");
            Check(ui.Contains("var lit=look;lit.Back=backColor<0?HexOf(accent):backColor;lit.HasBack=true;")&&ui.Contains("Color tint=backColor<0?accent:U.Hex(backColor);"),"the card passes its backdrop's tint (the picker's colour, Auto the accent) and still draws the picked backdrop");
            Check(theme.Contains("public static Color Night(float a=1)=>NativeUi.Hex(NativeLighting.LegacyStyle?VoidHex:NativeMenuStyle.NightHex,a);")&&theme.Contains("public static Color Dusk(float a=1)=>NativeUi.Hex(NativeLighting.LegacyStyle?NavyHex:NativeMenuStyle.DuskHex,a);"),"the 2D covers' night palette, Void and Navy under the legacy command line");
            Check(hud.Contains("H.Cut(r,C.Night(alpha),cut,set);")&&hud.Contains("U.Fill(new Rect(0,0,W,Hh),C.Night(alpha));")&&!hud.Contains("C.Void(alpha)"),"the shutters' body and the reduced fade take the night");
            int b=NativeMenuStyle.NightHex&255,g=(NativeMenuStyle.NightHex>>8)&255,r=(NativeMenuStyle.NightHex>>16)&255,db=NativeMenuStyle.DuskHex&255,dr=(NativeMenuStyle.DuskHex>>16)&255;Check(b>2*r&&b>g&&db>2*dr&&db>b&&b<80,"night and dusk: dark, blue-dominant, dusk the lighter");
            foreach(var f in new[]{"Assets/ChipArena/Core/NativeMenuStyle.cs.meta","Assets/ChipArena/Resources/NativeRooftopToon.cginc.meta"}){var m=Regex.Match(R(f),"guid: ([0-9a-f]{32})");Check(m.Success,f+" has a guid");}
        }
        // ---- allocation ----
        private static void Allocation()
        {
            var s=new MapStyleState();float sink=0;
            void Frame(){NativeMapStyle.State(NativeMenuStyle.Rooftop,false,true,s);NativeMenuStyle.RooftopSky(s,out var a,out var b,out var c);sink+=a.X+b.Y+c.Z+NativeMenuStyle.DraftToon(false).X;
                for(int i=0;i<64;i++){float x=i/63f;sink+=NativeMenuStyle.Bands(x,4)+NativeMenuStyle.SoftBands(x,4,x)+NativeMenuStyle.DraftFade(x*2)+NativeMenuStyle.Planes(x)+NativeMenuStyle.Haze(x*900,50,NativeMenuStyle.RoofFog)+NativeMenuStyle.PortraitEdge(x,x*.5f,0)+NativeMenuStyle.PortraitGradientAt(x,.2f,.6f);}}
            Frame();long before=GC.GetAllocatedBytesForCurrentThread();Frame();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(bytes==0&&!float.IsNaN(sink),"the per-frame helpers allocate nothing ("+bytes+" bytes)");
        }
    }
}
#endif
