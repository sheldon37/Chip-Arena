#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The anime map style (docs/MAP-STYLE-ANIME.md; Core/NativeMapStyle.cs, Core/NativeToonMesh.cs, Resources/NativeToon.cginc,
    // NativeToonSurface.cginc, NativeToonEdge.shader, the toon paths of NativeBlockLit, NativeLit, NativeSourceLit, NativeNightSky
    // and NativeBloom, Runtime/NativeToonOutline.cs and NativeLighting's switch). Checked: the switch (only the styled looks
    // turn it on: stage 1's pilots, stage 2's Factory and Cyber City; the legacy command line and every other map leave every global at 0, the styled look keeps every field the
    // kits' budgets read), the shaders' constants and formulas against the Core copy, the legacy paths kept exactly (each toon
    // path behind the one uniform branch, Lambert and the bloom composite untouched while it is off), the ramp maths, the
    // rounded mesh (closed, outward, never past the block, exact legacy faces, deterministic, under the legacy mesh's budget),
    // the shape rules, the outline's line rule on synthetic depth, nothing allocated per frame, the runtime hooks and the data;
    // stage 2: the damaged and toothed wraps, toothed ends, the bevelled debris, the kit shapes and the town's wrap/rounded split.
    // Rendering itself (D3D11, the look) is not run here: the offline renders are, the Windows build is the owner's.
    internal static class MapStyleChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Map style: "+what);count++;}
        private static string Code(string text)=>string.Join("\n",text.Split('\n').Select(l=>{int c=l.IndexOf("//",StringComparison.Ordinal);return c<0?l:l.Substring(0,c);}));
        private static string Body(string code,string head)
        {
            int at=code.IndexOf(head,StringComparison.Ordinal);if(at<0)return "";int open=code.IndexOf('{',at);if(open<0)return "";
            int depth=0;for(int i=open;i<code.Length;i++){if(code[i]=='{')depth++;else if(code[i]=='}'&&--depth==0)return code.Substring(open,i-open+1);}
            return "";
        }
        private static float Define(string shader,string name){var m=Regex.Match(shader,@"#define "+name+@" ([-0-9.]+)u?\r?\n");Check(m.Success,"define "+name);return float.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture);}
        private static bool Near(double a,double b,double e=1e-5)=>Math.Abs(a-b)<=e;
        public static void Run(string root)
        {
            string R(string p)=>File.ReadAllText(Path.Combine(root,p));
            var o=new JsonSerializerOptions{IncludeFields=true};
            var set=JsonSerializer.Deserialize<MapLookSet>(R("Assets/Conversion/Resources/MapLooks.json"),o);NativeMapLooks.Validate(set);
            Switch(set);Console.WriteLine("PASS map style: the switch (only expedition-city, brickfall, factory, cybercity, Ward Zero (quarantine), Central Station and the Ironworks turn it on; legacy and every other map write 0) and the styled look keeps the fog end, lights, block and kit looks and dressing the budgets read ("+count+" checks)");
            Shaders(root,R);Console.WriteLine("PASS map style: the shaders' toon constants and formulas are the Core's, every legacy path is kept behind the one uniform branch, the outline marks and tests stencil bit 16 ("+count+" checks)");
            Ramp();Console.WriteLine("PASS map style: the ramp (three bands, monotonic, a cast shadow takes the turned-away shade), the soft stairs, the stepped point lights and the rounded normal ("+count+" checks)");
            Mesh();var lg=NativeBlockLooks.Build(0);Console.WriteLine("PASS map style: the rounded box is closed and outward at every size, inside the block, on the legacy faces, deterministic and under the legacy S0 mesh ("+NativeToonMesh.VertexCount+" vertices and "+NativeToonMesh.TriangleCount+" triangles against "+lg.Vertices+" and "+lg.Triangles.Length/3+"); stages 1-4 keep the legacy meshes ("+count+" checks)");
            Wrap();Console.WriteLine("PASS map style: the wrap ("+NativeToonMesh.WrapVertexCount+" vertices, "+NativeToonMesh.WrapTriangleCount+" triangles) is closed and outward for every class and size, never above or below the block nor more than "+NativeMapStyle.WrapOut+" m proud of it, deterministic; walls, columns, crates and containers take their classes, the legacy meshes never take its path ("+count+" checks)");
            Stage2(root,R);Console.WriteLine("PASS map style: stage 2 - the damaged wraps (S1, S2 with and without the waist, the stub) and the toothed end are closed and outward and keep their coping and plinth through S2, toothed ends only at free ends of big brick walls, the debris's bevelled cube is closed and outward, the kit shapes (rake, awning, cornice, frame) and the town's wrap/rounded split match the shader ("+count+" checks)");
            Shapes();Console.WriteLine("PASS map style: rods round at the lattice clamp, mid blocks grow their chamfer to 8 cm, big blocks keep theirs, a taper narrows the top 30% ("+count+" checks)");
            Edges();Console.WriteLine("PASS map style: the outline lines silhouettes on the nearer pixel only, creases where 1/depth bends, never a plane, and fades out with distance ("+count+" checks)");
            Allocation(set);Console.WriteLine("PASS map style: the ramp, stairs, point gain, diffuse, radius, round normal, chamfer, lattice, edge, the damaged and toothed wrap, the bevelled debris facet, the kit shapes and the style state allocate nothing per frame ("+count+" checks)");
            Runtime(root,R);Console.WriteLine("PASS map style: NativeLighting writes the globals and turns them off on stages (the rooftop writes its own look), the outline pass is a command buffer at BeforeForwardAlpha off on the low tier, the kits draw the rounded box only while the style is on, the debris takes the ramp ("+count+" checks)");
            Data(set);Console.WriteLine("PASS map style: the styled looks' values validate (the pilots, the Factory, Cyber City, Ward Zero, Central Station and the Ironworks), broken ones are refused, the old maps' looks are untouched ("+count+" checks)");
        }
        // ---- the switch and the styled look ----
        private static void Switch(MapLookSet set)
        {
            var on=set.maps.Where(m=>NativeMapStyle.On(m,false)).Select(m=>m.id).OrderBy(x=>x).ToArray();
            Check(on.SequenceEqual(new[]{"breakwater","brickfall","cybercity","expedition-city","expedition-foundry","expedition-transit","factory","harbor","polar","rig","siren-quarantine","skyframe"}),"only the styled maps (stage 1's pilots; stage 2's Factory, Cyber City, Ward Zero's quarantine look, Central Station and the Ironworks; the five M1 stages) turn the style on: "+string.Join(",",on));
            var state=new MapStyleState();
            foreach(var m in set.maps)
            {
                Check(!NativeMapStyle.On(m,true),m.id+": the legacy switch turns it off");Check(ReferenceEquals(NativeMapStyle.Styled(m,true),m),m.id+": legacy draws the look itself");
                NativeMapStyle.State(m,true,true,state);Check(!state.On&&state.Toon.All(v=>v==0)&&state.Edge.All(v=>v==0)&&state.SkyToon.All(v=>v==0)&&state.GradeParams[3]==0,m.id+": legacy writes every toon, edge, sky and grade value as 0");
                if(!NativeMapStyle.On(m,false)){Check(ReferenceEquals(NativeMapStyle.Styled(m,false),m),m.id+": a look without the style is itself");NativeMapStyle.State(m,false,true,state);Check(!state.On&&state.Toon[0]==0,m.id+": off");continue;}
                var s=NativeMapStyle.Styled(m,false);
                Check(!ReferenceEquals(s,m)&&ReferenceEquals(s.lights,m.lights)&&ReferenceEquals(s.blockLooks,m.blockLooks)&&ReferenceEquals(s.kitLooks,m.kitLooks)&&ReferenceEquals(s.shadows,m.shadows)&&s.farClip==m.farClip&&s.look==m.look&&s.id==m.id,m.id+": the styled copy shares the lights, block and kit looks and shadows");
                Check(s.fog.mode==m.fog.mode&&s.fog.end==m.fog.end&&s.fog.start<=m.fog.end&&NativeExpeditionCityKit.FarRange(s)==NativeExpeditionCityKit.FarRange(m),m.id+": the fog's mode and end (the kits' far range) are the look's");
                Check(s.blocks.dressed==m.blocks.dressed&&s.blocks.dust==m.blocks.dust&&s.blocks.haze==m.blocks.haze&&s.blocks.hazeTop==m.blocks.hazeTop,m.id+": the dressing, dust and haze amounts are the look's");
                Check(m.toon.fogColor==0||s.fog.color==m.toon.fogColor&&s.fog.skyLerp==0,m.id+": the style's fog colour");
                Check(NativeMapStyle.Has(m.toon.key)&&ReferenceEquals(s.key,m.toon.key)&&NativeMapStyle.Has(m.toon.sky)&&ReferenceEquals(s.sky,m.toon.sky),m.id+": the style's key and sky");
                var a=new MapLookState();var b=new MapLookState();NativeMapLooks.Applied(m,0,false,a);NativeMapLooks.Applied(s,0,false,b);
                Check(a.Cascades==b.Cascades&&a.ShadowDistance==b.ShadowDistance&&a.FarClip==b.FarClip&&a.Fog.End==b.Fog.End&&a.Fog.Mode==b.Fog.Mode&&a.RimPixel==b.RimPixel,m.id+": the styled look applies the same shadows, far clip, fog mode and end");
                NativeMapStyle.State(s,false,true,state);
                Check(state.On&&state.Toon[0]==1&&state.Outline&&state.Edge[0]==m.toon.line&&state.Sky==(m.toon.skyToon>0)&&state.Grade,m.id+": the state turns the toon, outline, sky and grade on");
                NativeMapStyle.State(s,false,false,state);Check(state.On&&!state.Outline&&state.Edge[0]==0,m.id+": the low tier's state has no outline");
            }
            Check(NativeMapStyle.LegacyArgument(new[]{"x","--ca-map-style","legacy"})&&NativeMapStyle.LegacyArgument(new[]{"--ca-map-style=legacy"})&&!NativeMapStyle.LegacyArgument(new[]{"--ca-map-style","anime"})&&!NativeMapStyle.LegacyArgument(new[]{"--ca-map-style"})&&!NativeMapStyle.LegacyArgument(null),"the legacy command line");
        }
        // ---- the shaders against the Core ----
        private static void Shaders(string root,Func<string,string> R)
        {
            string res="Assets/ChipArena/Resources/";string toon=R(res+"NativeToon.cginc"),block=R(res+"NativeBlockLit.shader"),edge=R(res+"NativeToonEdge.shader"),surf=R(res+"NativeToonSurface.cginc");
            string lit=R(res+"NativeLit.shader"),src=R(res+"NativeSourceLit.shader"),zombie=R(res+"NativeZombieLit.shader"),sky=R(res+"NativeNightSky.shader"),bloom=R(res+"NativeBloom.shader");
            Check(Define(toon,"TOON_SHADOW_LO")==NativeMapStyle.ShadowLo&&Define(toon,"TOON_SHADOW_HI")==NativeMapStyle.ShadowHi,"the shadow's ease is NativeMapStyle's");
            Check(Define(toon,"TOON_POINT_STEPS")==NativeMapStyle.PointSteps&&Define(toon,"TOON_STAIR")==NativeMapStyle.Stair&&Define(toon,"TOON_RADIUS_MAX")==NativeMapStyle.RadiusMax&&Define(toon,"TOON_EDGE_GAIN")==NativeMapStyle.EdgeGain,"NativeToon.cginc's constants are NativeMapStyle's");
            string shared=toon.Substring(toon.IndexOf("TOON SHARED BEGIN",StringComparison.Ordinal),toon.IndexOf("TOON SHARED END",StringComparison.Ordinal)-toon.IndexOf("TOON SHARED BEGIN",StringComparison.Ordinal));
            foreach(var f in new[]{"return _ArenaToonBand.z*ToonStep(x,_ArenaToonBand.x,s)+(1.0-_ArenaToonBand.z)*ToonStep(x,_ArenaToonBand.y,s);","float ToonStairs(float x){float f=floor(x);return f+smoothstep(0.5-TOON_STAIR,0.5+TOON_STAIR,x-f);}",
                "if(_ArenaToon.x<0.5)return c;","float q=ToonStairs(sqrt(l)*TOON_POINT_STEPS)/TOON_POINT_STEPS;","float3 p=c*lerp(1.0,q*q/l,_ArenaToonLit.w);float lp=dot(p,float3(0.2126,0.7152,0.0722));","float cool=_ArenaToonCool.x*(1.0-smoothstep(0.0,max(_ArenaToonCool.y,0.0001),lp));",
                "return lerp(p,_ArenaToonShade.rgb*(lp/max(dot(_ArenaToonShade.rgb,float3(0.2126,0.7152,0.0722)),0.0001)),cool);","float r=ToonRamp(saturate(ndl))*ToonShadow(atten);","float ToonShadow(float atten){return smoothstep(TOON_SHADOW_LO,TOON_SHADOW_HI,atten);}","float3 shade=(amb+key*_ArenaToonShade.w)*_ArenaToonShade.rgb;",
                "return lerp(shade,amb+key*_ArenaToonLit.rgb,r);","float3 core=max(h-float3(r,r,r),float3(0.0,0.0,0.0));float3 q=obj-clamp(obj,-core,core);","return dot(n,facet)>0.5?n:facet;","return min(0.9*lo,clamp(_ArenaToonBand.w*lo,c,TOON_RADIUS_MAX));",
                "float spec=smoothstep(_ArenaToonFx.y,_ArenaToonFx.y+0.012,dot(n,normalize(keyDir+v)))*ToonShadow(atten);","return edge*(key*(lit*_ArenaToonRim.w+spec*_ArenaToonFx.x)+_ArenaToonRim.rgb*(back*_ArenaToon.z));",
                "float3 ToonOcclude(float3 light,float occ){return light*lerp(float3(1.0,1.0,1.0),_ArenaToonShade.rgb*(1.0-_ArenaToonFx.z),saturate(occ));}"})
                Check(shared.Contains(f),"NativeToon.cginc shared: "+f);
            Check(!Regex.IsMatch(Code(shared),@"(?<![\w.])\d+(?![\w.u])")&&!Regex.IsMatch(Code(shared),@"\btex2D|sampler|static\b"),"the toon block is GLSL-compatible (float literals, no textures)");
            // NativeBlockLit: the shapes, the switch, the legacy path kept.
            Check(Define(block,"TOON_ROD")==NativeMapStyle.Rod&&Define(block,"TOON_ROD_SQUARE")==NativeMapStyle.RodSquare&&Define(block,"TOON_ROD_LONG")==NativeMapStyle.RodLong&&Define(block,"TOON_MID_HALF")==NativeMapStyle.MidHalf
                &&Define(block,"TOON_CHAMFER_GROW")==NativeMapStyle.ChamferGrow&&Define(block,"TOON_CHAMFER_MAX")==NativeMapStyle.ChamferMax&&Define(block,"TOON_TAPER")==NativeMapStyle.TaperShare&&Define(block,"FLAG_TAPER")==NativeMapStyle.Taper,"NativeBlockLit's shape constants are NativeMapStyle's");
            Check((NativeMapStyle.Taper&(NativeBlockLooks.Band|NativeBlockLooks.Tag|NativeBlockLooks.Fragment|NativeBlockLooks.LampAmber|NativeBlockLooks.LampRed|NativeBlockLooks.SpinY|NativeBlockLooks.SpinZ|NativeCyberCityLooks.Bank))==0&&(NativeMapStyle.Taper*2-1)*16+NativeBlockLooks.Keys.Length<1<<24,"the taper flag is a bit of its own and look + 16 x flags stays exact in a float");
            var chamfer=Body(block,"float ToonChamfer(float3 h,float c)");
            Check(chamfer.Contains("if(mid<=TOON_ROD&&lo>=TOON_ROD_SQUARE*mid&&hi>=TOON_ROD_LONG*mid)return 0.42*lo;")&&chamfer.Contains("if(lo<=TOON_MID_HALF)return clamp(TOON_CHAMFER_GROW*lo,c,TOON_CHAMFER_MAX);")&&chamfer.Contains("return c;"),"ToonChamfer is NativeMapStyle.Chamfer");
            var lattice=Body(block,"float3 ToonLattice(");
            Check(lattice.Contains("if(_ArenaToon.x<0.5)return BlockLattice(a,b,h,c,flags,n);")&&lattice.Contains("p.xz*=1.0-TOON_TAPER*saturate(p.y/max(h.y,0.0001)*0.5+0.5);"),"ToonLattice: the legacy lattice exactly while off, the taper on the style");
            // The wrap: its constants, class rules and shape lines are NativeMapStyle's; the vertex stage takes it only for uv0.w 1.
            Check(Define(block,"WRAP_OUT")==NativeMapStyle.WrapOut&&Define(block,"WRAP_BEVEL")==NativeMapStyle.WrapBevel&&Define(block,"WRAP_CHIP")==NativeMapStyle.WrapChip&&Define(block,"WRAP_CHIP_SHARE")==NativeMapStyle.WrapChipShare&&Define(block,"WRAP_PILASTER")==NativeMapStyle.WrapPilaster&&Define(block,"WRAP_PILASTER_OUT")==NativeMapStyle.WrapPilasterOut
                &&Define(block,"WRAP_WALL")==NativeMapStyle.WrapWall&&Define(block,"WRAP_COLUMN")==NativeMapStyle.WrapColumn&&Define(block,"WRAP_CRATE")==NativeMapStyle.WrapCrate&&Define(block,"WRAP_CONTAINER")==NativeMapStyle.WrapContainer,"NativeBlockLit's wrap constants are NativeMapStyle's");
            var wclass=Body(block,"float ToonWrapClass(");var wshape=Body(block,"void ToonWrapShape(");var wplace=Body(block,"float3 ToonWrap(float4 a");
            foreach(var f in new[]{"if(masonry&&h.y>=0.6&&lo>=0.08&&lo>=0.5*hi&&hi<=0.8&&h.y>=1.8*hi)return WRAP_COLUMN;","if(masonry&&h.y>=0.3&&lo>=0.1&&hi>=0.3)return WRAP_WALL;","if(BkIs(look,LOOK_TIMBER)&&h.y>=0.2&&h.y<=1.2&&lo>=0.2&&hi<=1.5)return WRAP_CRATE;","if(BkIs(look,LOOK_CORRUGATED)&&h.y>=0.4&&lo>=0.3)return WRAP_CONTAINER;"})Check(wclass.Contains(f),"ToonWrapClass: "+f);
            foreach(var f in new[]{"pb=min(0.32,0.09*H)*w.x;op=min(0.035,0.25*lo)*w.x;ct=min(0.26,0.07*H)*w.y;oc=min(0.05,0.3*lo)*w.y;","chip=w.z*step(0.7,frac(seed*7.31))*(1.0+floor(frac(seed*13.7)*3.999));d=chip>0.5?WRAP_CHIP:0.0;","if(hi>=2.0&&h.y>=2.0&&w.x>0.5){d=WRAP_PILASTER;po=WRAP_PILASTER_OUT;}",
                "rs=max(c,0.25*lo);rt=min(0.04,0.42*min(lo,h.y));","pb=min(0.3,0.12*H)*w.x;op=min(0.07,0.25*lo)*w.x;ct=min(0.28,0.12*H)*w.y;oc=min(0.08,0.3*lo)*w.y;","d=min(0.11,0.3*lo)*w.w;po=0.032*w.w;","d=min(0.12,0.3*lo)*w.w;po=0.035*w.w;","rb=pb>0.0?0.0:rt;","pb=min(pb,0.25*H);ct=min(max(ct,rt+oc),0.3*H);"})Check(wshape.Contains(f),"ToonWrapShape: "+f);
            foreach(var f in new[]{"else if(row<4.5){y=h.y-ct+min(oc,0.5*(ct-rt));o=oc;}","{y=max(y-min(WRAP_CHIP,ct+0.1)*(row-3.0)*0.25,h.y-ct+0.002*(row-3.0));o=min(o,0.0)-0.04;}","p.xz+=sn*o;p.y=y;"})Check(wplace.Contains(f),"ToonWrap: "+f);
            Check(Body(block,"void BlockVertex(").Contains("if(v.texcoord.w>0.5){float4 look=UNITY_ACCESS_INSTANCED_PROP(BlockProps,_BlockLook);v.vertex=float4(ToonWrap(v.texcoord,v.texcoord1,size.xyz,ToonChamfer(size.xyz,size.w),look.x,look.w,look.z,v.color,n),1.0);}")&&!Regex.IsMatch(wclass+wshape+wplace,@"\b(ddx|ddy|fwidth)\b"),"the vertex stage and the shadow caster place the wrap (uv0.w 1) without derivatives");
            Check(Body(block,"void BlockVertex(").Contains("ToonLattice(v.texcoord.xyz,v.texcoord1.xyz,size.xyz,size.w,flags,n)")&&!Regex.IsMatch(Body(block,"float3 ToonLattice(")+Body(block,"float ToonChamfer("),@"\b(ddx|ddy|fwidth)\b"),"the vertex stage and the shadow caster place the style's lattice, without derivatives");
            string frag=Body(block,"fixed4 frag(");
            Check(frag.Contains("if(_ArenaToon.x>0.5)")&&frag.Contains("float3 light=ShadeSH9(float4(nW,1.0))+_LightColor0.rgb*(saturate(dot(nW,_WorldSpaceLightPos0.xyz))*atten)+BlockLights(i.world.xyz,nW);")&&frag.Contains("col=s.albedo*light+s.emit;")
                &&frag.Contains("fixed4 c=fixed4(BlockHaze(col,i.world.xyz,distance(i.world.xyz,_WorldSpaceCameraPos)),1.0);"),"the fragment's legacy light is today's, behind the one branch");
            var surface=Body(block,"BlockSurf BlockSurface(");
            Check(Body(block,"float3 ToonBlockLit(").Contains("float bounce=1.0+_ArenaToonPaint.y*(saturate(world.y/12.0)-0.35)*1.5;")&&Body(block,"float3 ToonBlockLit(").Contains("float3 light=ToonOcclude(ToonDiffuse(ToonAmbient(shN,shUp,shDown,nT.y)*bounce,key,dot(nT,keyDir),atten),occ)+ToonPoints(BlockLights(world,nT));")&&frag.Contains("float rad=ToonRadius(i.size.xyz,i.ax.w);float keep=smoothstep(1.0,3.0,rad/max(fpx,0.00001));")&&frag.Contains("float3 nR=normalize(lerp(nO,ToonRoundNormal(i.obj.xyz,i.size.xyz,rad,nO),keep)+s.bump);"),"the occlusion band and the surface's normal turns are the style's only");
            Check(surface.Contains("if(toon)s.occ=max(")&&surface.Contains("BlockSurf s;s.albedo=lerp(alb,BK_COAT,coat);s.emit=emit*(1.0-0.5*coat);s.occ=0.0;")&&surface.Contains("float3 bump=float3(0.0,0.0,0.0);")&&surface.Contains("s.bump=bump;")&&surface.Contains("if(toon)bump=(top?")&&surface.Contains("else if(toon)occLook=")&&Regex.Matches(surface,@"\bbump=").Count==4,"the occlusion and normal turns are written only behind the switch (the legacy look never reads them)");
            Check(surface.Contains("bool toon=_ArenaToon.x>0.5;if(toon)detail*=_ArenaToonPaint.x;")&&surface.Contains("if(toon)shade.rgb=float3(0.8,0.8,0.8);")&&surface.Contains("alb*=shade.rgb*1.25;")&&surface.Contains("if(!top&&toon)alb*=1.0-_ArenaToonPaint.w*(1.0-smoothstep(0.0,1.6,yb));"),"the paint's toon steps are each behind the switch");
            int band=surface.IndexOf("// The 2.3 load-bearing band",StringComparison.Ordinal),paint=surface.IndexOf("if(toon)\n",StringComparison.Ordinal);
            Check(band>0&&paint>0&&paint<band,"the paint comes before the telegraphs (the band, the DANGER tag and the lamp keep their colours)");
            string forward=block.Substring(block.IndexOf("\"LightMode\"=\"ForwardBase\"",StringComparison.Ordinal),block.IndexOf("\"LightMode\"=\"ShadowCaster\"",StringComparison.Ordinal)-block.IndexOf("\"LightMode\"=\"ForwardBase\"",StringComparison.Ordinal));
            Check(forward.Contains("Stencil { Ref 16 WriteMask 16 Comp Always Pass Replace }")&&Regex.Matches(block,"Stencil").Count==1,"the blocks' forward pass marks the environment (bit 16); the shadow caster does not");
            Check(block.Contains("#include \"NativeToon.cginc\"")&&block.IndexOf("#include \"NativeToon.cginc\"",StringComparison.Ordinal)>block.IndexOf("// SHARED BEGIN",StringComparison.Ordinal)&&block.IndexOf("#include \"NativeToon.cginc\"",StringComparison.Ordinal)<block.IndexOf("// SHARED END",StringComparison.Ordinal),"the toon block sits in NativeBlockLit's shared section (the offline harness runs it)");
            // The surface shaders: Lambert exactly while off; the stencil marks.
            Check(surf.Contains("if(_ArenaToon.x<0.5)return LightingLambert(s,gi);")&&surf.Contains("inline void LightingArenaToon_GI(SurfaceOutput s,UnityGIInput data,inout UnityGI gi){LightingLambert_GI(s,data,gi);}"),"ArenaToon is Unity's Lambert while the style is off");
            Check(lit.Contains("#pragma surface surf ArenaToon fullforwardshadows addshadow")&&lit.Contains("#include \"NativeToonSurface.cginc\"")&&lit.Contains("ToonPoints(MuzzleLights(")&&lit.Contains("Stencil { Ref 16 WriteMask 16 Comp Always Pass Replace }"),"NativeLit: the toon model, its muzzle lights in stairs, the environment mark");
            Check(src.Contains("#pragma surface surfArmor ArenaToon fullforwardshadows addshadow vertex:vert finalcolor:StageTone")&&src.Contains("Stencil { Ref [_ToonEdgeRef] WriteMask 16 Comp Always Pass Replace }")&&src.Contains("_ToonEdgeRef(\"")&&src.Contains("Float)=0 }")
                &&src.Contains("o.Emission+=o.Albedo*ToonPoints(ActorLights(i.worldPos,normalize(i.worldNormal)));")&&src.Contains("o.Emission+=o.Albedo*ToonPoints(MuzzleLights(i.worldPos,normalize(i.worldNormal),.35));"),"SourceLit: the toon model, characters unmarked by default, actor and muzzle lights in stairs");
            Check(zombie.Contains("Stencil { Ref 0 WriteMask 16 Comp Always Pass Replace }"),"ZombieLit clears the environment mark");
            // The outline.
            Check(Define(edge,"EDGE_GAP_MIN")==NativeMapStyle.GapMin&&Define(edge,"EDGE_GAP_SHARE")==NativeMapStyle.GapShare&&Define(edge,"EDGE_CREASE")==NativeMapStyle.CreaseWeight&&Define(edge,"EDGE_WIDE_GAP")==NativeMapStyle.WideGap&&Define(edge,"EDGE_CREASE_FOOT0")==NativeMapStyle.CreaseFoot0&&Define(edge,"EDGE_CREASE_FOOT1")==NativeMapStyle.CreaseFoot1,"NativeToonEdge's constants are NativeMapStyle's");
            var line=Body(edge,"float EdgeLine(float2 uv,float2 px,float foot)");
            Check(line.Contains("float L=EdgeDepth(uv-float2(2.0*px.x,0.0));float R=EdgeDepth(uv+float2(2.0*px.x,0.0));float D=EdgeDepth(uv-float2(0.0,2.0*px.y));float U=EdgeDepth(uv+float2(0.0,2.0*px.y));")
                &&line.Contains("float gap=EDGE_GAP_MIN+EDGE_GAP_SHARE*c;float sil=max(step(gap,max(l+r,d+u)-2.0*c),step(EDGE_WIDE_GAP*gap,max(L+R,D+U)-2.0*c));")&&line.Contains("float ic=1.0/c;float bend=max(abs(1.0/l+1.0/r-2.0*ic),abs(1.0/d+1.0/u-2.0*ic))/ic;")&&line.Contains("float crease=smoothstep(_ArenaEdge.w,_ArenaEdge.w*2.0,bend)*step(c-min(min(l,r),min(d,u)),gap)*(1.0-smoothstep(EDGE_CREASE_FOOT0,EDGE_CREASE_FOOT1,c*foot));")
                &&line.Contains("return saturate(max(sil,crease*EDGE_CREASE))*(1.0-smoothstep(_ArenaEdge.y,_ArenaEdge.z,c))*_ArenaEdge.x;"),"EdgeLine is NativeMapStyle.Edge");
            Check(edge.Contains("Stencil { Ref 16 ReadMask 16 Comp Equal }")&&edge.Contains("Blend DstColor Zero")&&edge.Contains("Cull Off ZWrite Off ZTest Always")&&edge.Contains("Shader \"Hidden/ChipArena/NativeToonEdge\"")&&Regex.Matches(edge,"CGPROGRAM").Count==1,"the outline: one hidden pass over the environment mark, multiplying the scene");
            Check(!Regex.IsMatch(Code(edge),@"\btex2D\("),"the outline reads only the depth texture (no other sampler)");
            // The sky and the grade: today's while off.
            Check(sky.Contains("float4 frag(v2f i):SV_Target {float3 d=normalize(i.dir);if(_SkyToon.x>0.5)return float4(AnimeSky(d),1);")&&sky.Contains("_SkyToon(\"Anime sky: on, cloud cover, cloud scale, horizon band height\",Vector)=(0,0,0,0)"),"the anime sky: its own branch at the top of frag, off by default");
            string composite=Body(Code(bloom),"float4 FragComposite(v2f_img i)");
            Check(composite.Contains("if(_ArenaGrade.w>0.5)return float4(BloomGrade(BloomComposite(scene.rgb,BloomUv(i.uv),_BloomTex_TexelSize.xy,_BloomParams.x),_ArenaGrade,_ArenaGradeShadow.rgb,_ArenaGradeLight.rgb),scene.a);")&&composite.Contains("return float4(BloomComposite(scene.rgb,BloomUv(i.uv),_BloomTex_TexelSize.xy,_BloomParams.x),scene.a);"),"the grade: its own branch, the composite as it was while off");
            foreach(var meta in new[]{"Resources/NativeToon.cginc.meta","Resources/NativeToonSurface.cginc.meta","Resources/NativeToonEdge.shader.meta","Core/NativeMapStyle.cs.meta","Core/NativeToonMesh.cs.meta","Runtime/NativeToonOutline.cs.meta"})
            {
                var text=R("Assets/ChipArena/"+meta);var g=Regex.Match(text,@"guid: ([0-9a-f]{32})");Check(g.Success,meta+" has a guid");
                int owners=Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories).Count(m=>File.ReadAllText(m).Contains("guid: "+g.Groups[1].Value));Check(owners==1,meta+"'s guid is its own");
            }
            Check(R("Assets/ChipArena/Editor/NativeBuildChecks.cs").Contains("Resources.Load<Shader>(\"NativeToonEdge\")"),"the outline shader is build-checked");
        }
        // ---- the ramp ----
        private static void Ramp()
        {
            const float e0=.06f,e1=.42f,half=.5f,soft=.035f;
            Check(NativeMapStyle.Ramp(0,e0,e1,half,soft)==0&&NativeMapStyle.Ramp(e0-soft,e0,e1,half,soft)==0&&Near(NativeMapStyle.Ramp((e0+e1)/2,e0,e1,half,soft),half)&&Near(NativeMapStyle.Ramp(1,e0,e1,half,soft),1),"three bands: shade 0, half-tone, lit 1");
            float last=-1;for(int i=0;i<=1000;i++){float r=NativeMapStyle.Ramp(i/1000f,e0,e1,half,soft);Check(r>=last-1e-6f&&r>=0&&r<=1,"the ramp rises with N.L");last=r;}
            Check(NativeMapStyle.Stairs(.2f)==0&&NativeMapStyle.Stairs(1.2f)==1&&NativeMapStyle.Stairs(2.9f)==3&&Near(NativeMapStyle.Stairs(1.5f),1.5),"soft stairs at whole steps");
            Check(NativeMapStyle.PointGain(.3f,0)==1&&NativeMapStyle.PointGain(0,1)==1,"point lights unstepped with points 0 or nothing to step");
            for(int i=1;i<200;i++){float l=i/40f,g=NativeMapStyle.PointGain(l,1),q=(float)Math.Sqrt(l*g)*NativeMapStyle.PointSteps,k=(float)Math.Sqrt(l)*NativeMapStyle.PointSteps,fr=k-(float)Math.Floor(k);Check(g>0,"stepped light stays on: "+l);if(fr<.3f||fr>.7f)Check(Math.Abs(q-Math.Round(q))<1e-3f,"off a riser, a stepped point light sits on a whole stair: "+l);}
            // A cast shadow is the same flat shade as a face turned from the key.
            float amb=.12f,key=.9f;
            float shadowed=NativeMapStyle.Diffuse(amb,key,.9f,0,.8f,.06f,1.1f,e0,e1,half,soft),turned=NativeMapStyle.Diffuse(amb,key,-.4f,1,.8f,.06f,1.1f,e0,e1,half,soft),lit=NativeMapStyle.Diffuse(amb,key,.9f,1,.8f,.06f,1.1f,e0,e1,half,soft);
            Check(Near(shadowed,turned)&&Near(turned,(amb+key*.06f)*.8f)&&Near(lit,amb+key*1.1f),"shade = (ambient + lift x key) x shade tint, lit = ambient + key x lit tint; a cast shadow is the turned-away shade");
            // The rounded normal: the face normal on the flat, turned on the soft edge, the facet past 60 degrees.
            NativeMapStyle.RoundNormal(0,1,0,1,1,1,.2f,0,1,0,out float nx,out float ny,out float nz);Check(nx==0&&ny==1&&nz==0,"flat face: its own normal");
            NativeMapStyle.RoundNormal(.95f,1,0,1,1,1,.2f,0,1,0,out nx,out ny,out nz);Check(nx>.2f&&ny>.5f&&Near(nx*nx+ny*ny+nz*nz,1,1e-4),"within the radius of an edge: turned toward the side face");
            NativeMapStyle.RoundNormal(.99f,.4f,0,1,1,1,.2f,1,0,0,out nx,out ny,out nz);Check(nx==1&&ny==0&&nz==0,"a side face's flat: its own normal");
            NativeMapStyle.RoundNormal(.95f,-.6f,0,1,1,1,.2f,0,1,0,out nx,out ny,out nz);Check(nx==0&&ny==1&&nz==0,"a facet far from the rounded normal (a chip, a stub's break) keeps its own");
            Check(NativeMapStyle.Radius(5,10,5,.05f,.1f)==.3f&&Near(NativeMapStyle.Radius(.5f,.5f,.5f,.08f,.1f),.08f)&&Near(NativeMapStyle.Radius(.15f,3,10,.05f,.1f),.05f)&&NativeMapStyle.Radius(.02f,1,1,.05f,.1f)<=.9f*.02f+1e-6f,"the soft-edge radius: a share of the thinnest half, at least the chamfer, at most .3 m and .9 of it");
        }
        // ---- the rounded mesh ----
        private static void Mesh()
        {
            var m=NativeToonMesh.Build();var legacy=NativeBlockLooks.Build(0);
            Check(m.Vertices==NativeToonMesh.VertexCount&&m.Triangles.Length==NativeToonMesh.TriangleCount*3&&m.Lattice.Length==m.Vertices*4&&m.Inset.Length==m.Vertices*4&&m.Shade.Length==m.Vertices*4&&m.Normal.Length==m.Vertices*3,"exact counts");
            Check(m.Vertices<legacy.Vertices&&m.Triangles.Length<legacy.Triangles.Length,"under the legacy S0 mesh's budget: "+m.Vertices+" vertices, "+m.Triangles.Length/3+" triangles against "+legacy.Vertices+" and "+legacy.Triangles.Length/3);
            var again=NativeToonMesh.Build();Check(m.Lattice.SequenceEqual(again.Lattice)&&m.Inset.SequenceEqual(again.Inset)&&m.Triangles.SequenceEqual(again.Triangles)&&m.Normal.SequenceEqual(again.Normal)&&m.Preview.SequenceEqual(again.Preview),"deterministic");
            Check(m.Shade.Where((v,i)=>i%4<3).All(v=>v==NativeBlockLooks.ShadeStore)&&m.Shade.Where((v,i)=>i%4==3).All(v=>v==0),"neutral shade, no fresh break");
            for(int s=1;s<NativeBlockLooks.Meshes;s++){var a=NativeToonMesh.Stage(s);var b=NativeBlockLooks.Build(s);Check(a.Lattice.SequenceEqual(b.Lattice)&&a.Triangles.SequenceEqual(b.Triangles),"stage mesh "+s+" is the legacy one (breaks stay hard)");}
            Check(NativeToonMesh.Stage(0).Triangles.SequenceEqual(m.Triangles),"stage 0 is the rounded box");
            foreach(var (hx,hy,hz,c) in new[]{(.5f,.5f,.5f,.05f),(2f,.05f,3f,.08f),(.04f,.04f,2f,.0168f),(6f,12f,4f,.05f),(.3f,1.8f,.3f,.08f)})
            {
                int n=m.Vertices;var p=new double[n*3];for(int v=0;v<n;v++){p[v*3]=NativeBlockLooks.Corner(m.Lattice[v*4],m.Inset[v*4],hx,c);p[v*3+1]=NativeBlockLooks.Corner(m.Lattice[v*4+1],m.Inset[v*4+1],hy,c);p[v*3+2]=NativeBlockLooks.Corner(m.Lattice[v*4+2],m.Inset[v*4+2],hz,c);}
                Check(Enumerable.Range(0,n).All(v=>Math.Abs(p[v*3])<=hx+1e-6&&Math.Abs(p[v*3+1])<=hy+1e-6&&Math.Abs(p[v*3+2])<=hz+1e-6),"never past the block at "+hx+","+hy+","+hz);
                string Key(int v)=>string.Join(",",Enumerable.Range(0,3).Select(k=>Math.Round(p[v*3+k],6).ToString(CultureInfo.InvariantCulture)));
                var edges=new Dictionary<(string,string),int>();double vol=0;
                for(int t=0;t<m.Triangles.Length;t+=3)
                {
                    int a=m.Triangles[t],b=m.Triangles[t+1],cc=m.Triangles[t+2];
                    vol+=p[a*3]*(p[b*3+1]*p[cc*3+2]-p[b*3+2]*p[cc*3+1])-p[a*3+1]*(p[b*3]*p[cc*3+2]-p[b*3+2]*p[cc*3])+p[a*3+2]*(p[b*3]*p[cc*3+1]-p[b*3+1]*p[cc*3]);
                    for(int k=0;k<3;k++){string ka=Key(m.Triangles[t+k]),kb=Key(m.Triangles[t+(k+1)%3]);if(ka==kb)continue;edges[(ka,kb)]=edges.TryGetValue((ka,kb),out int e)?e+1:1;}
                }
                int open=edges.Count(e=>!edges.TryGetValue((e.Key.Item2,e.Key.Item1),out int back)||back!=e.Value);
                Check(open==0,"closed at "+hx+","+hy+","+hz+" ("+open+" unmatched edges)");Check(vol/6>0&&vol/6<=8*hx*hy*hz+1e-6,"wound outward (positive volume within the box) at "+hx+","+hy+","+hz);
            }
            // The faces are the legacy faces: the plane at the half extent, the flat region inset by the chamfer.
            for(int v=0;v<m.Vertices;v++)for(int k=0;k<3;k++)if(Math.Abs(m.Lattice[v*4+k])==1&&m.Inset[v*4+k]==0&&m.Normal[v*3+k]!=0&&Math.Abs(m.Normal[v*3+k])==1)
            {for(int j=0;j<3;j++)if(j!=k)Check(Math.Abs(m.Inset[v*4+j])==1&&Math.Sign(m.Inset[v*4+j])==-Math.Sign(m.Lattice[v*4+j]),"a face corner sits a chamfer in from the edges, as the legacy face's");}
        }
        // ---- the wrap ----
        private static void Wrap()
        {
            var m=NativeToonMesh.Wrap();int n=m.Vertices;
            Check(n==NativeToonMesh.WrapVertexCount&&m.Triangles.Length==NativeToonMesh.WrapTriangleCount*3&&m.Lattice.Length==n*4&&m.Inset.Length==n*4&&m.Normal.Length==n*3&&m.Shade.Length==n*4,"exact counts");
            Check(Enumerable.Range(0,n).All(v=>m.Lattice[v*4+3]==1)&&Enumerable.Range(0,NativeBlockLooks.Meshes).All(s=>{var l=NativeBlockLooks.Build(s);return Enumerable.Range(0,l.Vertices).All(v=>l.Lattice[v*4+3]==0);})&&Enumerable.Range(0,NativeToonMesh.VertexCount).All(v=>NativeToonMesh.Build().Lattice[v*4+3]==0),"only the wrap takes the shader's wrap path (TEXCOORD0.w 1; 0 on the legacy and rounded meshes)");
            var again=NativeToonMesh.Wrap();Check(m.Lattice.SequenceEqual(again.Lattice)&&m.Inset.SequenceEqual(again.Inset)&&m.Triangles.SequenceEqual(again.Triangles)&&m.Normal.SequenceEqual(again.Normal),"deterministic");
            // The classes (Brickfall's own blocks among them).
            Check(NativeMapStyle.WrapClass(.6f,1.8f,.6f,NativeBlockLooks.BrickRed,0)==NativeMapStyle.WrapColumn,"a 1.2 x 3.6 m brick pier is a column");
            Check(NativeMapStyle.WrapClass(.7f,1.68f,.15f,NativeBlockLooks.BrickBuff,0)==NativeMapStyle.WrapWall&&NativeMapStyle.WrapClass(2.5f,7,1,NativeBlockLooks.ConcreteBoard,0)==NativeMapStyle.WrapWall,"facade panels and frontage walls are walls");
            Check(NativeMapStyle.WrapClass(1.2f,.55f,.6f,NativeBlockLooks.Timber,0)==NativeMapStyle.WrapCrate&&NativeMapStyle.WrapClass(3.05f,1.3f,1.2f,NativeBlockLooks.Corrugated,0)==NativeMapStyle.WrapContainer,"cover crates are crates, containers and trucks containers");
            Check(NativeMapStyle.WrapClass(2.8f,.12f,3,NativeBlockLooks.ConcreteBoard,0)==NativeMapStyle.WrapNone&&NativeMapStyle.WrapClass(.6f,1.8f,.6f,NativeBlockLooks.BrickRed,NativeCyberCityLooks.Bank)==NativeMapStyle.WrapNone&&NativeMapStyle.WrapClass(.9f,.55f,.5f,NativeBlockLooks.Plain,0)==NativeMapStyle.WrapNone&&NativeMapStyle.WrapClass(.05f,1.6f,.05f,NativeBlockLooks.SteelGreen,0)==NativeMapStyle.WrapNone,"slabs, the bank looks, sandbags and rails take no class (a rounded box)");
            // Every class at sizes from a rail to a frontage: closed, outward, inside the block's height, at most WrapOut proud.
            var cases=new List<(float,float,float,int,float)>{(.5f,.5f,.5f,NativeBlockLooks.Plain,.3f),(.05f,1.5f,.05f,NativeBlockLooks.SteelGreen,.1f),(2.8f,.12f,3,NativeBlockLooks.ConcreteBoard,.2f),(.7f,1.68f,.15f,NativeBlockLooks.BrickBuff,.75f),(.2f,1.68f,.15f,NativeBlockLooks.BrickRed,.8f),
                (2.5f,7,1,NativeBlockLooks.BrickRed,.9f),(7.5f,7,1,NativeBlockLooks.BrickRed,.71f),(1,28,1,NativeBlockLooks.BrickRed,.72f),(.6f,1.8f,.6f,NativeBlockLooks.BrickRed,.1f),(.15f,4.1f,.5f,NativeBlockLooks.ConcreteBoard,.3f),(1.2f,.55f,.6f,NativeBlockLooks.Timber,.4f),(.75f,.7f,.75f,NativeBlockLooks.Timber,.5f),(3.05f,1.3f,1.2f,NativeBlockLooks.Corrugated,.6f),(1,.4f,1,NativeBlockLooks.BrickRed,.95f)};
            foreach(var (hx,hy,hz,look,seed) in cases)foreach(var flags in new[]{0,NativeMapStyle.Taper})
            {
                var p=new double[n*3];
                for(int v=0;v<n;v++){NativeMapStyle.WrapPoint(m.Lattice[v*4],m.Lattice[v*4+1],m.Lattice[v*4+2],m.Inset[v*4],m.Inset[v*4+1],m.Inset[v*4+2],m.Inset[v*4+3],hx,hy,hz,NativeBlockLooks.Chamfer,look,flags,seed,out float x,out float y,out float z);p[v*3]=x;p[v*3+1]=y;p[v*3+2]=z;}
                string at=hx+","+hy+","+hz+" look "+look+(flags!=0?" tapered":"");
                Check(Enumerable.Range(0,n).All(v=>Math.Abs(p[v*3+1])<=hy+1e-5&&Math.Abs(p[v*3])<=hx+NativeMapStyle.WrapOut+1e-5&&Math.Abs(p[v*3+2])<=hz+NativeMapStyle.WrapOut+1e-5),"never above or below the block, at most WrapOut proud: "+at);
                double vol=0;var edges=new Dictionary<(int,int),int>();
                // Positions that coincide are one point (a feature at 0 folds rows together); edges are counted between points.
                var key=new Dictionary<string,int>();int Id(int v){string k=string.Join(",",Enumerable.Range(0,3).Select(c=>Math.Round(p[v*3+c],5).ToString(CultureInfo.InvariantCulture)));if(!key.TryGetValue(k,out int id)){id=key.Count;key[k]=id;}return id;}
                for(int t=0;t<m.Triangles.Length;t+=3)
                {
                    int a=m.Triangles[t],b=m.Triangles[t+1],c=m.Triangles[t+2];
                    vol+=p[a*3]*(p[b*3+1]*p[c*3+2]-p[b*3+2]*p[c*3+1])-p[a*3+1]*(p[b*3]*p[c*3+2]-p[b*3+2]*p[c*3])+p[a*3+2]*(p[b*3]*p[c*3+1]-p[b*3+1]*p[c*3]);
                    int ia=Id(a),ib=Id(b),ic=Id(c);if(ia==ib||ib==ic||ia==ic)continue;
                    foreach(var (u,w) in new[]{(ia,ib),(ib,ic),(ic,ia)})edges[(u,w)]=edges.TryGetValue((u,w),out int e)?e+1:1;
                }
                int open=edges.Count(e=>!edges.TryGetValue((e.Key.Item2,e.Key.Item1),out int back)||back!=e.Value);
                Check(open==0,"closed: "+at+" ("+open+" unmatched edges)");
                double box=8.0*(hx+NativeMapStyle.WrapOut)*hy*(hz+NativeMapStyle.WrapOut);Check(vol/6>.5*8*hx*hy*hz*(flags!=0?.5:1)&&vol/6<=box+1e-6,"wound outward, the volume about the block's: "+at+" ("+vol/6+" against "+8*hx*hy*hz+")");
            }
            // The features: a wall's coping and plinth stand proud, its top is rounded; a crate's posts stand out of its planks.
            NativeMapStyle.WrapPoint(1,4,-1,0,0,-1,0,.7f,1.68f,.15f,NativeBlockLooks.Chamfer,NativeBlockLooks.BrickBuff,0,.1f,out float cx,out float cy,out float cz);Check(cx>.7f+.01f&&cy<1.68f-.1f,"a wall's coping stands proud below its top");
            NativeMapStyle.WrapPoint(1,0,-1,0,0,-1,0,.7f,1.68f,.15f,NativeBlockLooks.Chamfer,NativeBlockLooks.BrickBuff,0,.1f,out cx,out cy,out cz);Check(cx>.7f+.01f&&Near(cy,-1.68f),"its plinth stands proud at its foot");
            NativeMapStyle.WrapPoint(1,0,-1,0,0,-1,0,.7f,1.68f,.15f,NativeBlockLooks.Chamfer,NativeBlockLooks.BrickBuff,0,.1f,out cx,out cy,out cz,plinths:0);Check(Near(cx,.7f)||cx<.7f,"plinths off (the city): the foot is flush");
            NativeMapStyle.WrapPoint(1,3,-1,0,2,-1,-2,1.2f,.55f,.6f,NativeBlockLooks.Chamfer,NativeBlockLooks.Timber,0,.1f,out float px,out float py,out float pz);NativeMapStyle.WrapPoint(1,3,-1,0,0,-1,-2,1.2f,.55f,.6f,NativeBlockLooks.Chamfer,NativeBlockLooks.Timber,0,.1f,out float qx,out float qy,out float qz);
            Check(px-qx>.02f&&Near(pz,qz)&&Near(py,qy),"a crate's corner post stands out of its face");
            int chipped=0;for(int i=0;i<200;i++)if(NativeMapStyle.WrapShape(NativeMapStyle.WrapWall,.7f,1.68f,.15f,.05f,i/200f,1,1,1,1).Chip>.5f)chipped++;Check(chipped>30&&chipped<90,"a broken corner on some old masonry ("+chipped+" of 200)");
            Check(NativeMapStyle.WrapShape(NativeMapStyle.WrapWall,7.5f,7,1,.05f,.1f,1,1,1,1).PostOut==NativeMapStyle.WrapPilasterOut&&NativeMapStyle.WrapShape(NativeMapStyle.WrapWall,7.5f,7,1,.05f,.1f,0,1,1,1).PostOut==0&&NativeMapStyle.WrapShape(NativeMapStyle.WrapWall,.7f,1.68f,.15f,.05f,.1f,1,1,1,1).PostOut==0,"big walls stand pilasters (with the plinths), panels do not");
        }
        // ---- stage 2: damaged wraps, toothed ends, bevelled debris, the kit shapes, the town's split ----
        private static (double[] p,int open,double vol) Placed(NativeBlockLooks.StageMesh m,float hx,float hy,float hz,int look,int flags,float seed)
        {
            int n=m.Vertices;var p=new double[n*3];
            for(int v=0;v<n;v++){NativeMapStyle.WrapPoint(m.Lattice[v*4],m.Lattice[v*4+1],m.Lattice[v*4+2],m.Inset[v*4],m.Inset[v*4+1],m.Inset[v*4+2],m.Inset[v*4+3],hx,hy,hz,NativeBlockLooks.Chamfer,look,flags,seed,out float x,out float y,out float z,stageW:m.Lattice[v*4+3],dropShare:m.Shade[v*4],inShare:m.Shade[v*4+1]);p[v*3]=x;p[v*3+1]=y;p[v*3+2]=z;}
            var key=new Dictionary<string,int>();int Id(int v){string k=string.Join(",",Enumerable.Range(0,3).Select(c=>Math.Round(p[v*3+c],5).ToString(CultureInfo.InvariantCulture)));if(!key.TryGetValue(k,out int id)){id=key.Count;key[k]=id;}return id;}
            double vol=0;var edges=new Dictionary<(int,int),int>();
            for(int t=0;t<m.Triangles.Length;t+=3)
            {
                int a=m.Triangles[t],b=m.Triangles[t+1],c=m.Triangles[t+2];
                vol+=p[a*3]*(p[b*3+1]*p[c*3+2]-p[b*3+2]*p[c*3+1])-p[a*3+1]*(p[b*3]*p[c*3+2]-p[b*3+2]*p[c*3])+p[a*3+2]*(p[b*3]*p[c*3+1]-p[b*3+1]*p[c*3]);
                int ia=Id(a),ib=Id(b),ic=Id(c);if(ia==ib||ib==ic||ia==ic)continue;
                foreach(var (u,w) in new[]{(ia,ib),(ib,ic),(ic,ia)})edges[(u,w)]=edges.TryGetValue((u,w),out int e)?e+1:1;
            }
            return (p,edges.Count(e=>!edges.TryGetValue((e.Key.Item2,e.Key.Item1),out int back)||back!=e.Value),vol/6);
        }
        private static void Stage2(string root,Func<string,string> R)
        {
            // The shader's stage 2 constants and lines are the Core's.
            string block=R("Assets/ChipArena/Resources/NativeBlockLit.shader");
            Check(Define(block,"WRAP_DMG_DROP")==NativeMapStyle.WrapDamageDrop&&Define(block,"WRAP_DMG_IN")==NativeMapStyle.WrapDamageIn&&Define(block,"WRAP_STUB_RAG")==NativeMapStyle.WrapStubRag&&Define(block,"WRAP_WAIST")==NativeMapStyle.WrapWaist
                &&Define(block,"WRAP_TEETH")==NativeMapStyle.WrapTeeth&&Define(block,"WRAP_TOOTH_COURSE")==NativeMapStyle.WrapToothCourse&&Define(block,"WRAP_TOOTH_DEPTH")==NativeMapStyle.WrapToothDepth&&Define(block,"WRAP_TOOTH_SEAM")==NativeMapStyle.WrapToothSeam
                &&Define(block,"FLAG_TOOTH_POS")==NativeMapStyle.ToothPos&&Define(block,"FLAG_TOOTH_NEG")==NativeMapStyle.ToothNeg&&Define(block,"FLAG_FRAME")==NativeMapStyle.Frame&&Define(block,"FLAG_CORNICE")==NativeMapStyle.Cornice&&Define(block,"FLAG_RAKE")==NativeMapStyle.Rake&&Define(block,"FLAG_AWNING")==NativeMapStyle.Awning
                &&Define(block,"TOON_RAKE_SLOPE")==NativeMapStyle.RakeSlope&&Define(block,"TOON_AWNING_ARCH")==NativeMapStyle.AwningArch&&Define(block,"TOON_FRAME")==NativeMapStyle.FrameWidth,"NativeBlockLit's stage 2 constants and flags are NativeMapStyle's");
            int kitBits=NativeMapStyle.ToothPos|NativeMapStyle.ToothNeg|NativeMapStyle.Frame|NativeMapStyle.Cornice|NativeMapStyle.Rake|NativeMapStyle.Awning;
            Check((kitBits&(NativeMapStyle.Taper|NativeBlockLooks.Band|NativeBlockLooks.Tag|NativeBlockLooks.Fragment|NativeBlockLooks.LampAmber|NativeBlockLooks.LampRed|NativeBlockLooks.SpinY|NativeBlockLooks.SpinZ|NativeCyberCityLooks.Bank))==0&&(NativeMapStyle.Awning*2-1)*16+NativeBlockLooks.Keys.Length<1<<24,"the stage 2 flags are bits of their own and look + 16 x flags stays exact in a float");
            var kit=Body(block,"float3 ToonKitShape(");var wplace=Body(block,"float3 ToonWrap(float4 a");var wshape=Body(block,"void ToonWrapShape(");
            Check(kit.Contains("if((fl&FLAG_RAKE)!=0u){float r=saturate((p.y+h.y)/max(2.0*h.y,0.0001))*2.0*h.y/TOON_RAKE_SLOPE;t*=max(hl-r,0.0)/max(hl,0.0001);}")&&kit.Contains("if((fl&FLAG_AWNING)!=0u){float u=t/max(hl,0.0001);p.y+=min(TOON_AWNING_ARCH,0.25*hl)*(1.0-u*u);}")
                &&Body(block,"float3 ToonLattice(").Contains("return ToonKitShape(p,h,uint(flags+0.5));")&&wplace.Contains("p=ToonKitShape(p,h,fl);"),"ToonKitShape is NativeMapStyle.KitShape, on the lattice and the wrap");
            Check(wshape.Contains("uint kf=uint(flags+0.5);if((kf&FLAG_CORNICE)!=0u){rb=min(0.6*h.y,0.6*lo);rt=min(rt,0.02);}if((kf&FLAG_AWNING)!=0u)d=hi;"),"ToonWrapShape: the cornice's moulding and the awning's post lines");
            foreach(var f in new[]{"if(stage>0.5){d=max(d,min(WRAP_CHIP,WRAP_CHIP_SHARE*lo));chip=0.0;}","if(stage>2.5){oc=0.0;ct=rt;}","if(stage>0.5){y-=dmg.r*(stage>2.5?min(WRAP_STUB_RAG,0.6*h.y):min(WRAP_DMG_DROP,0.6*h.y));o-=dmg.g*min(WRAP_DMG_IN,0.35*lo);}",
                "float t=row-10.0;float j=floor(t*0.5)+1.0;float bh=min(WRAP_TOOTH_COURSE,(2.0*h.y-pb-ct)/(WRAP_TEETH+1.0));","float side=t-2.0*(j-1.0);y=h.y-ct-j*bh+(side<0.5?-WRAP_TOOTH_SEAM:WRAP_TOOTH_SEAM);o=0.0;float band=side<0.5?j:j-1.0;",
                "if(band<WRAP_TEETH-0.5&&frac(band*0.5)>0.25&&(end>0.0&&(fl&FLAG_TOOTH_POS)!=0u||end<0.0&&(fl&FLAG_TOOTH_NEG)!=0u))","{float td=min(WRAP_TOOTH_DEPTH,0.25*(alongX?h.x:h.z));if(alongX)p.x-=end*td;else p.z-=end*td;}",
                "else if(row>8.5){y=h.y/3.0;o=-min(WRAP_WAIST,0.3*lo);}","else if(row>7.5){y=-h.y/3.0;o=-min(WRAP_WAIST,0.3*lo);}"})Check(wplace.Contains(f),"ToonWrap: "+f);
            // Every wrap variant at sizes from a rail to a frontage: closed, outward, never above or below the block nor past WrapOut.
            var cases=new[]{(.5f,.5f,.5f,NativeBlockLooks.Plain,.3f),(.7f,1.68f,.15f,NativeBlockLooks.BrickBuff,.75f),(2.5f,7f,1f,NativeBlockLooks.BrickRed,.9f),(7.5f,7f,1f,NativeBlockLooks.BrickRed,.71f),(.6f,1.8f,.6f,NativeBlockLooks.BrickRed,.1f),(1.2f,.55f,.6f,NativeBlockLooks.Timber,.4f),(3.05f,1.3f,1.2f,NativeBlockLooks.Corrugated,.2f),(.05f,1.5f,.05f,NativeBlockLooks.SteelGreen,.1f),(3f,2f,.3f,NativeBlockLooks.BrickRed,.5f),(.3f,2f,3f,NativeBlockLooks.BrickBuff,.5f)};
            for(int mesh=0;mesh<NativeToonMesh.WrapMeshes;mesh++)
            {
                var m=NativeToonMesh.WrapMesh(mesh);int n=m.Vertices;
                Check(n==NativeToonMesh.WrapVertices(mesh)&&m.Triangles.Length==NativeToonMesh.WrapTriangles(mesh)*3&&Enumerable.Range(0,n).All(v=>m.Lattice[v*4+3]==1+NativeToonMesh.WrapStage(mesh)),"wrap mesh "+mesh+": exact counts, uv0.w 1 + its stage");
                Check(mesh==0?m.Lattice.SequenceEqual(NativeToonMesh.Wrap().Lattice):!m.Triangles.SequenceEqual(NativeToonMesh.Wrap().Triangles)||mesh==1||mesh==3||mesh==4,"wrap mesh "+mesh+" against the intact wrap");
                foreach(var (hx,hy,hz,look,seed) in cases)foreach(var flags in mesh==NativeToonMesh.ToothedMesh?new[]{0,NativeMapStyle.ToothPos,NativeMapStyle.ToothNeg,NativeMapStyle.ToothPos|NativeMapStyle.ToothNeg}:new[]{0,NativeMapStyle.Taper})
                {
                    var (p,open,vol)=Placed(m,hx,hy,hz,look,flags,seed);string at="mesh "+mesh+" at "+hx+","+hy+","+hz+" look "+look+" flags "+flags;
                    Check(Enumerable.Range(0,n).All(v=>Math.Abs(p[v*3+1])<=hy+1e-5&&Math.Abs(p[v*3])<=hx+NativeMapStyle.WrapOut+1e-5&&Math.Abs(p[v*3+2])<=hz+NativeMapStyle.WrapOut+1e-5),"never above or below the block, at most WrapOut proud: "+at);
                    Check(open==0,"closed: "+at+" ("+open+" unmatched edges)");
                    Check(vol>.4*8*hx*hy*hz*(flags==NativeMapStyle.Taper?.5:1)&&vol<=8.0*(hx+NativeMapStyle.WrapOut)*hy*(hz+NativeMapStyle.WrapOut)+1e-6,"wound outward, about the block's volume: "+at+" ("+vol+")");
                }
            }
            // A damaged wall keeps its coping and plinth through S2 (only the broken corners lose them); the stub loses its coping.
            double Proud(int mesh,int row){var m=NativeToonMesh.WrapMesh(mesh);var (p,_,_)=Placed(m,2.5f,1.68f,.3f,NativeBlockLooks.BrickRed,0,.5f);double o=0;for(int v=0;v<m.Vertices;v++)if(m.Lattice[v*4+1]==row&&m.Inset[v*4+1]<1.5f)o=Math.Max(o,Math.Abs(p[v*3+2])-.3);return o;}
            Check(Proud(1,4)>.01&&Proud(2,4)>.01&&Proud(4,4)>.01&&Proud(1,0)>.01&&Proud(2,0)>.01,"S1 and S2 keep the coping and the plinth along their faces (coping "+Proud(1,4).ToString("0.000")+", "+Proud(2,4).ToString("0.000")+" m proud)");
            Check(Proud(3,4)<=1e-4&&Proud(3,0)>.01,"the stub keeps its plinth and loses its coping");
            double Top(int mesh){var m=NativeToonMesh.WrapMesh(mesh);var (p,_,_)=Placed(m,2.5f,1.68f,.3f,NativeBlockLooks.BrickRed,0,.5f);return Enumerable.Range(0,m.Vertices).Where(v=>m.Inset[v*4+1]>1.5f&&m.Lattice[v*4+1]==7).Min(v=>p[v*3+1]);}
            Check(Top(1)<1.68-.1&&Top(2)<Top(1)+1e-6&&Top(0)>1.68-1e-4,"the damaged stages break their corners (S1 "+Top(1).ToString("0.00")+", S2 "+Top(2).ToString("0.00")+" m at the top)");
            // Toothed ends: the free end's odd courses step in along the longer axis; no flag, no step.
            {
                var m=NativeToonMesh.WrapMesh(NativeToonMesh.ToothedMesh);var (p0,_,_)=Placed(m,3,2,.3f,NativeBlockLooks.BrickRed,0,.5f);var (pp,_,_)=Placed(m,3,2,.3f,NativeBlockLooks.BrickRed,NativeMapStyle.ToothPos,.5f);
                double minEnd=double.MaxValue,minNeg=double.MaxValue;for(int v=0;v<m.Vertices;v++)if(m.Lattice[v*4+1]>=10){if(m.Lattice[v*4]>0)minEnd=Math.Min(minEnd,pp[v*3]);else minNeg=Math.Min(minNeg,-pp[v*3]);}
                double flat=Enumerable.Range(0,m.Vertices).Where(v=>m.Lattice[v*4+1]>=10&&m.Lattice[v*4]>0).Min(v=>p0[v*3]);
                Check(minEnd<flat-NativeMapStyle.WrapToothDepth+.005&&minEnd>flat-NativeMapStyle.WrapToothDepth-.005&&Near(minNeg,flat,1e-4),"a toothed end steps its courses in at the flagged end only ("+minEnd.ToString("0.00")+" against "+flat.ToString("0.00")+")");
            }
            // ToothEnds: a free end of a big brick wall is toothed; an end that meets another wall, or a junction beside it, is not.
            ContentBlock W(string id,float x,float z,float sx,float sz)=>new ContentBlock{id=id,material="brick",pos=new V3(x,2,z),size=new V3(sx,4,sz)};
            var walls=new[]{W("a",0,0,6,.6f),W("b",3.3f,0,.6f,4),W("c",-12,0,6,.6f)};
            var ends=NativeMapStyle.ToothEnds(walls,i=>i!=1);
            Check(ends[0]==NativeMapStyle.ToothNeg&&ends[1]==0&&ends[2]==(NativeMapStyle.ToothPos|NativeMapStyle.ToothNeg),"ToothEnds: the end meeting a wall is not toothed, free ends are, non-candidates never ("+string.Join(",",ends)+")");
            Check(NativeMapStyle.ToothCandidate(3,2,.3f,NativeBlockLooks.BrickRed)&&!NativeMapStyle.ToothCandidate(3,2,.15f,NativeBlockLooks.BrickRed)&&!NativeMapStyle.ToothCandidate(3,1,.3f,NativeBlockLooks.BrickRed)&&!NativeMapStyle.ToothCandidate(3,2,.3f,NativeBlockLooks.ConcreteBoard)&&!NativeMapStyle.ToothCandidate(.4f,2,.3f,NativeBlockLooks.BrickRed),"toothed ends only on big brick walls (40 cm thick, 1 m long, twice as long as thick, 2.4 m tall)");
            // The debris's bevelled cube: closed, outward, inside its box, every facet wound to its normal.
            var cr=new float[12];
            foreach(var (hx,hy,hz) in new[]{(.2f,.2f,.2f),(.6f,.1f,.3f),(.05f,.4f,.05f)})
            {
                float b=NativeMapStyle.DebrisBevel(hx,hy,hz);var edges=new Dictionary<(string,string),int>();double vol=0;
                string Key(int k)=>string.Join(",",Enumerable.Range(0,3).Select(c=>Math.Round(cr[k*3+c],5).ToString(CultureInfo.InvariantCulture)));
                for(int f=0;f<NativeMapStyle.BevelFacets;f++)
                {
                    int k=NativeMapStyle.BevelFacet(f,hx,hy,hz,b,cr,out float nx,out float ny,out float nz);Check(k==3||k==4,"a facet is a triangle or a quad");
                    for(int i=0;i<k;i++)Check(Math.Abs(cr[i*3])<=hx+1e-5&&Math.Abs(cr[i*3+1])<=hy+1e-5&&Math.Abs(cr[i*3+2])<=hz+1e-5,"inside the cube's box");
                    for(int i=1;i+1<k;i++)
                    {
                        double ux=cr[i*3]-cr[0],uy=cr[i*3+1]-cr[1],uz=cr[i*3+2]-cr[2],vx=cr[(i+1)*3]-cr[0],vy=cr[(i+1)*3+1]-cr[1],vz=cr[(i+1)*3+2]-cr[2];
                        double cx=uy*vz-uz*vy,cy=uz*vx-ux*vz,cz=ux*vy-uy*vx;Check(cx*nx+cy*ny+cz*nz>0,"facet "+f+" winds counter-clockwise from outside (to its normal)");
                        vol+=cr[0]*(cr[i*3+1]*cr[(i+1)*3+2]-cr[i*3+2]*cr[(i+1)*3+1])-cr[1]*(cr[i*3]*cr[(i+1)*3+2]-cr[i*3+2]*cr[(i+1)*3])+cr[2]*(cr[i*3]*cr[(i+1)*3+1]-cr[i*3+1]*cr[(i+1)*3]);
                    }
                    for(int i=0;i<k;i++){string ka=Key(i),kb=Key((i+1)%k);edges[(ka,kb)]=edges.TryGetValue((ka,kb),out int e)?e+1:1;}
                }
                int open=edges.Count(e=>!edges.TryGetValue((e.Key.Item2,e.Key.Item1),out int back)||back!=e.Value);
                Check(open==0&&vol/6>.8*8*hx*hy*hz&&vol/6<=8*hx*hy*hz+1e-6,"the bevelled debris cube is closed and outward at "+hx+","+hy+","+hz+" ("+open+" open edges, volume "+(vol/6).ToString("0.0000")+")");
            }
            // The kit shapes (the shader's ToonKitShape): a raked slab's top ends draw in by its height over the slope, its foot keeps
            // its length; an awning arches up at its middle only; a cornice is moulded underneath; the frame is paint.
            {
                float x=2,y=.28f,z=0;NativeMapStyle.KitShape(ref x,ref y,ref z,2,.28f,.17f,NativeMapStyle.Rake);Check(Near(x,2-.56f/NativeMapStyle.RakeSlope,1e-4)&&Near(y,.28f),"a raked slab's top end draws in by its height over the slope");
                x=2;y=-.28f;NativeMapStyle.KitShape(ref x,ref y,ref z,2,.28f,.17f,NativeMapStyle.Rake);Check(Near(x,2),"its foot keeps its length");
                x=-1;y=.28f;NativeMapStyle.KitShape(ref x,ref y,ref z,2,.28f,.17f,NativeMapStyle.Rake);float x2=-1.5f,y2=.28f,z2=0;NativeMapStyle.KitShape(ref x2,ref y2,ref z2,2,.28f,.17f,NativeMapStyle.Rake);Check(x>-1&&x2<x,"a raked top keeps its points in order (scaled, not folded)");
                x=0;y=.03f;z=0;NativeMapStyle.KitShape(ref x,ref y,ref z,.8f,.03f,.5f,NativeMapStyle.Awning);float xe=.8f,ye=.03f,ze=0;NativeMapStyle.KitShape(ref xe,ref ye,ref ze,.8f,.03f,.5f,NativeMapStyle.Awning);
                Check(Near(y,.03f+NativeMapStyle.AwningArch)&&Near(ye,.03f),"an awning arches "+NativeMapStyle.AwningArch+" m at its middle, none at its ends");
                var plain=NativeMapStyle.WrapShape(NativeMapStyle.WrapNone,.3f,.2f,1,.05f,.5f,1,1,1,1);var cornice=NativeMapStyle.WrapShape(NativeMapStyle.WrapNone,.3f,.2f,1,.05f,.5f,1,1,1,1,NativeMapStyle.Cornice);
                Check(cornice.Bottom>=.1f&&cornice.Bottom>plain.Bottom&&cornice.Top<=.02f,"a cornice: a deep moulding under it, a crisp top");
                Check(NativeMapStyle.NeedsWrap(2.5f,7,1,NativeBlockLooks.BrickRed,0)&&NativeMapStyle.NeedsWrap(.3f,.2f,1,NativeBlockLooks.Plain,NativeMapStyle.Cornice)&&NativeMapStyle.NeedsWrap(.8f,.03f,.5f,NativeBlockLooks.Plain,NativeMapStyle.Awning)&&NativeMapStyle.NeedsWrap(2,.28f,.17f,NativeBlockLooks.ConcreteBoard,NativeMapStyle.Rake)
                    &&!NativeMapStyle.NeedsWrap(.3f,.2f,1,NativeBlockLooks.Plain,0)&&!NativeMapStyle.NeedsWrap(.4f,.6f,.02f,NativeBlockLooks.WindowLit,NativeMapStyle.Frame),"the parts that need the wrap: a wrap class or a cornice, awning or rake (a painted frame does not)");
            }
            // A part that needs no wrap draws the same chamfered box on the rounded mesh (the town's split changes no silhouette).
            {
                var wrap=NativeToonMesh.Wrap();var box=NativeToonMesh.Build();
                foreach(var (hx,hy,hz) in new[]{(.3f,.2f,1f),(.05f,.05f,1.2f),(.6f,.02f,.4f)})
                {
                    var (p,_,_)=Placed(wrap,hx,hy,hz,NativeBlockLooks.Plain,0,.5f);double mx=0,my=0,mz=0;for(int v=0;v<wrap.Vertices;v++){mx=Math.Max(mx,p[v*3]);my=Math.Max(my,p[v*3+1]);mz=Math.Max(mz,p[v*3+2]);}
                    double bx=0,by=0,bz=0;float c=NativeMapStyle.Chamfer(hx,hy,hz,NativeBlockLooks.Chamfer);for(int v=0;v<box.Vertices;v++){NativeMapStyle.Lattice(box.Lattice[v*4],box.Lattice[v*4+1],box.Lattice[v*4+2],box.Inset[v*4],box.Inset[v*4+1],box.Inset[v*4+2],hx,hy,hz,NativeBlockLooks.Chamfer,0,out float x,out float y,out float z);bx=Math.Max(bx,x);by=Math.Max(by,y);bz=Math.Max(bz,z);}
                    Check(Near(mx,bx,1e-4)&&Near(my,by,1e-4)&&Near(mz,bz,1e-4),"a part without a wrap class has the same extents on the wrap and the rounded box at "+hx+","+hy+","+hz);
                }
            }
        }
        // ---- the shapes ----
        private static void Shapes()
        {
            const float c=NativeBlockLooks.Chamfer;
            Check(NativeMapStyle.IsRod(.05f,3,.05f)&&NativeMapStyle.IsRod(.01f,.01f,5)&&NativeMapStyle.IsRod(2,.08f,.06f)&&!NativeMapStyle.IsRod(.5f,.5f,.5f)&&!NativeMapStyle.IsRod(.2f,3,.2f)&&!NativeMapStyle.IsRod(.02f,3,.12f)&&!NativeMapStyle.IsRod(.05f,.12f,.05f),"rods: thin, square-ish, long (poles, cables, rails), never a crate, a thick post, a flat strip or a stub");
            Check(Near(NativeMapStyle.Chamfer(.05f,3,.05f,c),NativeBlockLooks.ClampShare*.05f),"a rod rounds at the lattice clamp");
            Check(Near(NativeMapStyle.Chamfer(.5f,.5f,.5f,c),.08f)&&Near(NativeMapStyle.Chamfer(.25f,.5f,.5f,c),c)&&Near(NativeMapStyle.Chamfer(.4f,.9f,2,c),.064f),"mid blocks grow the chamfer with their thinnest half (to 8 cm)");
            Check(NativeMapStyle.Chamfer(4,10,6,c)==c&&NativeMapStyle.Chamfer(1.5f,3,2,c)==c,"big blocks (buildings, walls over a metre thick) keep the chamfer");
            for(int i=0;i<500;i++){float hx=.01f+i*.013f%3,hy=.02f+i*.031f%6,hz=.015f+i*.007f%2;float t=NativeMapStyle.Chamfer(hx,hy,hz,c);Check(t>=c-1e-7f||NativeMapStyle.IsRod(hx,hy,hz),"the style's chamfer is never under the instance's (but a rod's)");Check(t<=Math.Max(c,NativeMapStyle.ChamferMax)+1e-6f,"at most the cap");}
            NativeMapStyle.Lattice(1,1,1,0,0,0,1,1,1,c,NativeMapStyle.Taper,out float x,out float y,out float z);Check(Near(x,.7f)&&Near(y,1)&&Near(z,.7f),"a tapered part's top narrows by 30%");
            NativeMapStyle.Lattice(1,-1,1,0,0,0,1,1,1,c,NativeMapStyle.Taper,out x,out y,out z);Check(Near(x,1)&&Near(y,-1)&&Near(z,1),"its base keeps the full size");
            NativeMapStyle.Lattice(1,1,1,-1,0,0,.5f,.5f,.5f,c,0,out x,out y,out z);Check(Near(x,.5f-.08f)&&Near(y,.5f),"untapered: the lattice with the style's chamfer");
        }
        // ---- the outline ----
        private static void Edges()
        {
            const float s=1,fs=40,fe=110,cr=.02f;
            // A plane (1/depth affine across the screen): never a line.
            // Planes from facing to grazing (1/depth affine: depth 1/(b + a u) at pixel u, its depth changing by up to 10% a pixel,
            // a floor seen from 0.2 m at 1080p), near and far.
            for(int i=0;i<60;i++){float b=.01f+.03f*(i%7),a=b*(.001f+i*.0017f);float D(float u)=>1/(b+a*u);Check(NativeMapStyle.Edge(D(0),D(-1),D(1),D(-1),D(1),s,fs,fe,cr)<1e-3f,"a plane draws no line ("+D(-1)+" to "+D(1)+" m)");}
            // A silhouette: the nearer pixel next to a farther background gets the line, the background pixel does not.
            Check(NativeMapStyle.Edge(10,10,30,10,10,s,fs,fe,cr)>.99f,"the nearer side of a depth gap is lined");
            Check(NativeMapStyle.Edge(30,10,30,30,30,s,fs,fe,cr)<1e-3f,"the farther side is not (a character in front is never lined by this pass)");
            Check(NativeMapStyle.Edge(10,10,10.1f,10,10,s,fs,fe,cr)<1e-3f,"a 10 cm step at 10 m is neither a silhouette nor a crease");
            // A crease: a wall meeting the floor (1/depth bends).
            float ic=1/10f;Check(NativeMapStyle.Edge(10,1/(ic-.004f),1/(ic-.004f*.2f),10,10,s,fs,fe,cr)>.5f,"a crease is lined");
            // A crease fades with the pixel's footprint (the same crease at 10 m: lined at 1080p, gone at 360p; a silhouette stays).
            float cl=1/(ic-.004f),cr2=1/(ic-.004f*.2f);
            Check(Near(NativeMapStyle.Foot(60,1080),2*Math.Tan(Math.PI/6)/1080,1e-7)&&NativeMapStyle.Edge(10,cl,cr2,10,10,cl,cr2,10,10,s,fs,fe,cr,NativeMapStyle.Foot(60,1080))>.5f&&NativeMapStyle.Edge(10,cl,cr2,10,10,cl,cr2,10,10,s,fs,fe,cr,NativeMapStyle.Foot(60,360))<.1f
                &&NativeMapStyle.Edge(10,10,30,10,10,10,30,10,10,s,fs,fe,cr,NativeMapStyle.Foot(60,360))>.99f,"interior creases fade out with the pixel's footprint (no moire at range or low resolution); silhouettes stay");
            // Two widths: a deep step two pixels away lines this pixel too (a silhouette two widths wide); a shallow one does not.
            Check(NativeMapStyle.Edge(10,10,10,10,10,10,30,10,10,s,fs,fe,cr,0)>.99f&&NativeMapStyle.Edge(10,10,10,10,10,10,11,10,10,s,fs,fe,cr,0)<1e-3f&&NativeMapStyle.Edge(10,10,11,10,10,10,11,10,10,s,fs,fe,cr,0)>.99f,"silhouettes draw two widths where the step is deep, one where it is shallow");
            for(int i=0;i<60;i++){float b=.01f+.03f*(i%7),a=b*(.001f+i*.0017f);float D(float u)=>1/(b+a*u);Check(NativeMapStyle.Edge(D(0),D(-1),D(1),D(-1),D(1),D(-2),D(2),D(-2),D(2),s,fs,fe,cr,0)<1e-3f,"a plane draws no line at either width ("+D(-2)+" to "+D(2)+" m)");}
            Check(NativeMapStyle.Edge(fe+1,fe+1,fe+40,fe+1,fe+1,s,fs,fe,cr)==0&&NativeMapStyle.Edge(10,10,30,10,10,0,fs,fe,cr)==0,"faded out past the fade end, nothing at strength 0");
        }
        // ---- nothing allocated per frame ----
        private static readonly float[] Corners=new float[12];
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static float Frame(MapLook look,MapStyleState state)
        {
            float sum=0;
            for(int i=0;i<2000;i++)
            {
                float x=i/2000f;sum+=NativeMapStyle.Ramp(x,.06f,.42f,.5f,.035f)+NativeMapStyle.Stairs(x*3)+NativeMapStyle.PointGain(x,.7f)+NativeMapStyle.Diffuse(.1f,.9f,x,1,.8f,.06f,1.1f,.06f,.42f,.5f,.035f)+NativeMapStyle.Radius(x,1,2,.05f,.1f);
                NativeMapStyle.RoundNormal(x,1,0,1,1,1,.2f,0,1,0,out float nx,out float ny,out float nz);sum+=nx+ny+nz+NativeMapStyle.Chamfer(x,2,.1f,.05f);
                NativeMapStyle.Lattice(1,1,-1,-1,0,1,x+.1f,1,1,.05f,NativeMapStyle.Taper,out float px,out float py,out float pz);sum+=px+py+pz+NativeMapStyle.Edge(10+x,10,30,10,10,10,30,10,10,1,40,110,.02f,.001f)+NativeMapStyle.Foot(60,1080+i)+NativeMapStyle.Shadow(x);
                NativeMapStyle.WrapPoint(1,i%8,-1,-.3f,2,0,0,x+.5f,1.7f,.15f,.05f,NativeBlockLooks.BrickRed,0,x,out px,out py,out pz);sum+=px+py+pz;
                NativeMapStyle.WrapPoint(1,10+i%24,-1,-.3f,2,0,0,3,2,.3f,.05f,NativeBlockLooks.BrickRed,NativeMapStyle.ToothPos,x,out px,out py,out pz,stageW:1+i%4,dropShare:x,inShare:x);sum+=px+py+pz;
                sum+=NativeMapStyle.BevelFacet(i%NativeMapStyle.BevelFacets,.2f+x,.3f,.1f,.03f,Corners,out float bx,out float by,out float bz)+bx+by+bz;
                float kx=x,ky=.28f,kz=0;NativeMapStyle.KitShape(ref kx,ref ky,ref kz,2,.28f,.17f,NativeMapStyle.Rake|NativeMapStyle.Awning);sum+=kx+ky+(NativeMapStyle.NeedsWrap(x,.2f,1,0,NativeMapStyle.Cornice)?1:0);
            }
            NativeMapStyle.State(look,false,true,state);return sum+state.Toon[0];
        }
        private static void Allocation(MapLookSet set)
        {
            var look=NativeMapLooks.Find(set,"expedition-city");var state=new MapStyleState();Frame(look,state);
            long before=GC.GetAllocatedBytesForCurrentThread();Frame(look,state);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(bytes==0,"the per-frame helpers and the style state allocate nothing ("+bytes+" bytes)");
        }
        // ---- the runtime ----
        private static void Runtime(string root,Func<string,string> R)
        {
            string rt="Assets/ChipArena/Runtime/";string lighting=Code(R(rt+"NativeLighting.cs")),outline=Code(R(rt+"NativeToonOutline.cs")),game=Code(R(rt+"ConversionGame.cs")),settings=Code(R(rt+"NativeSettings.cs"));
            Check(lighting.Contains("Sky(s);RenderSettings.skybox=s.Skybox?sky:null;Shadows();StyleGlobals(look);")&&lighting.Contains("NativeMapStyle.State(look,LegacyStyle,true,Style);")&&lighting.Contains("public static readonly bool LegacyStyle=NativeMapStyle.LegacyArgument(Environment.GetCommandLineArgs());"),"Apply writes the style's globals from NativeMapStyle (the legacy command line honoured)");
            // Stage 2 (Tests/MenuStyleChecks): the rooftop writes its own toon values (NativeMenuStyle.Rooftop), off under the legacy switch.
            Check(Regex.Matches(lighting,@"WardFieldOff\(\);EnterStage\(\);NativeMuzzleLights\.Off\(\);StyleOff\(\);").Count==3&&Body(lighting,"public static void Rooftop(Camera camera,Material skybox)").Contains("NativeMuzzleLights.Off();if(NativeMenuStyle.RooftopOn(LegacyStyle))StyleGlobals(NativeMenuStyle.Rooftop);else StyleOff();"),"every entrance, intro and draft frame turns the style off; the rooftop writes its own (Apply turns the map's back on)");
            Check(Body(lighting,"public static void StyleOff()").Contains("Shader.SetGlobalVector(ToonId,Vector4.zero);")&&Body(lighting,"public static void StyleOff()").Contains("Shader.SetGlobalVector(EdgeId,Vector4.zero);"),"off: _ArenaToon and _ArenaEdge to 0");
            Check(Body(lighting,"public static MapLookSet LookSet()").Contains("for(int i=0;i<set.maps.Length;i++)set.maps[i]=NativeMapStyle.Styled(set.maps[i],LegacyStyle);"),"the look set holds each look styled, made once at load (no per-frame copy; every reader sees the same look)");
            Check(outline.Contains("view.AddCommandBuffer(CameraEvent.BeforeForwardAlpha,buffer);")&&outline.Contains("NativeLighting.StyleActive&&NativeLighting.Style.Outline&&!LowGraphics")&&outline.Contains("buffer.DrawMesh(triangle,Matrix4x4.identity,material,0,0);"),"the outline: one draw from a command buffer before the transparent queue, only while the style draws it and not on the low tier");
            Check(!Regex.IsMatch(Body(outline,"private void LateUpdate()"),@"\bnew\b"),"the outline's frame allocates nothing");
            Check(outline.Contains("material.SetFloat(FootId,NativeMapStyle.Foot(fov,Mathf.Max(1,view.pixelHeight)));")&&Code(R("Assets/ChipArena/Resources/NativeToonEdge.shader")).Contains("EdgeLine(i.pos.xy*texel,texel*_EdgeWidth,_EdgeFoot)"),"the outline's creases fade by the camera's own pixel footprint (resolution and zoom)");
            Check(game.Contains("outline=NativeToonOutline.Attach(cameraView);")&&game.IndexOf("bloom=NativeBloom.Attach(cameraView);",StringComparison.Ordinal)<game.IndexOf("outline=NativeToonOutline.Attach(cameraView);",StringComparison.Ordinal)&&settings.Contains("if(outline!=null)outline.Quality(low);"),"on the match camera after the bloom, off on the low tier");
            Check(lighting.Contains("public static bool StyleWrap=>StyleActive&&!lowShadows;")&&lighting.Contains("Shader.SetGlobalVector(ToonWrapId,V(Style.Wrap));Shader.SetGlobalVector(ToonFxId,V(Style.Fx));"),"the wrap on the normal tiers (the low tier keeps the rounded box); its and the highlights' globals written");
            foreach(var (file,pick) in new[]{("NativeArenaDressing.cs","Draw(NativeLighting.StyleActive?ToonStage(s):meshes[s],batches[s][i],n);"),("NativeArenaDressing.cs","private Mesh ToonStage(int s)=>NativeLighting.StyleWrap?(wrapMeshes[s]??=MeshOf(NativeToonMesh.WrapMesh(s),s)):s==0?(toonMesh??=MeshOf(NativeToonMesh.Build(),0)):meshes[Math.Min(s,meshes.Length-1)];"),
                ("NativeExpeditionCityPresentation.cs","private Mesh TownMesh=>NativeLighting.StyleWrap?(townWrap??=MetroMeshOf(NativeToonMesh.Wrap())):NativeLighting.StyleActive?(townToon??=MetroMeshOf(NativeToonMesh.Build())):townMesh;"),
                ("NativeExpeditionHubPresentation.cs","private Mesh Shape=>NativeLighting.StyleWrap?(wrapMesh??=MeshOf(NativeToonMesh.Wrap())):NativeLighting.StyleActive?(toonMesh??=MeshOf(NativeToonMesh.Build())):mesh;"),
                ("NativeExpeditionStatesPresentation.cs","private Mesh Shape=>NativeLighting.StyleWrap?(wrapMesh??=MeshOf(NativeToonMesh.Wrap())):NativeLighting.StyleActive?(toonMesh??=MeshOf(NativeToonMesh.Build())):mesh;")})
                Check(Code(R(rt+file)).Contains(pick),file+" draws the wrap (normal tiers) or the rounded box (low tier) only while the style is on (made once)");
            foreach(var file in new[]{"NativeArenaDressing.cs","NativeExpeditionCityPresentation.cs","NativeExpeditionHubPresentation.cs","NativeExpeditionStatesPresentation.cs","NativeCyberCityPresentation.cs"})
            {var code=Code(R(rt+file));Check(!code.Contains("s.Lattice[i*4+2],0)")&&!code.Contains("s.Inset[i*4+2],0)"),file+" passes the mesh's TEXCOORD w through (the wrap's flag; 0 on every legacy mesh)");}
            Check(Code(R(rt+"NativeBrickfallPresentation.cs")).Contains("brickfallLit.SetFloat(\"_ToonEdgeRef\",16);"),"Brickfall's skyline is marked for the outline");
            // Stage 2: the damaged stages and toothed ends on the wrap, the town's split, the Factory's and Cyber City's rounded box.
            var dressing=Code(R(rt+"NativeArenaDressing.cs"));
            Check(dressing.Contains("int mesh=NativeBlockLooks.MeshFor(stage,size.y);if(mesh==0&&e.Tooth!=0&&NativeLighting.StyleWrap){mesh=NativeToonMesh.ToothedMesh;flags=(flags&~NativeBlockLooks.SpinY)|e.Tooth;}")
                &&dressing.Contains("tooth=NativeMapStyle.On(look,NativeLighting.LegacyStyle)?NativeMapStyle.ToothEnds(source,ToothCandidate):Array.Empty<int>();")&&dressing.Contains("counts=new int[NativeToonMesh.WrapMeshes]"),"the dressing draws the damaged wraps and a free brick end's toothed wrap (ends found once per styled map; one batch list more)");
            var town=Code(R(rt+"NativeExpeditionCityPresentation.cs"));
            Check(town.Contains("private Mesh TownSmall=>NativeLighting.StyleWrap?(townToon??=MetroMeshOf(NativeToonMesh.Build())):TownMesh;")&&town.Contains("townOrder=new int[townKit.Parts.Length];int w=0;for(int i=0;i<townOrder.Length;i++)if(TownNeedsWrap(i))townOrder[w++]=i;townWrapParts=w;for(int i=0;i<townOrder.Length;i++)if(!TownNeedsWrap(i))townOrder[w++]=i;")
                &&town.Contains("int i=townOrder[k];var part=parts[i];")&&town.Contains("if(k<townWrapParts){if(part.Shadow)townCastWrap=used;else townTrimWrap=used;}")&&town.Contains("TownDraw(townCast[i],ShadowCastingMode.On,i<townCastWrap?TownMesh:TownSmall);")&&town.Contains("TownDraw(townTrim[i],ShadowCastingMode.Off,i<townTrimWrap?TownMesh:TownSmall);"),"the town draws its parts that need the wrap first: the batches holding them take the wrap, the rest the rounded box (the same batches)");
            foreach(var (file,pick) in new[]{("NativeWardZeroPresentation.cs","private Mesh WardShape=>NativeLighting.StyleActive?(wzToon??=MetroMeshOf(NativeToonMesh.Build())):wzMesh;"),("NativeExpeditionStationPresentation.cs","private Mesh StationShape=>NativeLighting.StyleActive?(stationToon??=MetroMeshOf(NativeToonMesh.Build())):stationMesh;"),("NativeIronworksPresentation.cs","private Mesh IronShape=>NativeLighting.StyleActive?(iwToon??=MetroMeshOf(NativeToonMesh.Build())):iwMesh;"),("NativeFactoryPresentation.cs","private Mesh Block=>NativeLighting.StyleActive?(blockToon??=MeshOf(NativeToonMesh.Build())):block;"),("NativeHazardKitPresentation.cs","private Mesh KitShape=>NativeLighting.StyleActive?(kitToon??=MetroMeshOf(NativeToonMesh.Build())):kitMesh;"),("NativeCyberCityPresentation.cs","private Mesh MetroShape=>NativeLighting.StyleActive?(metroToon??=MetroMeshOf(NativeToonMesh.Build())):metroMesh;")})
                Check(Code(R(rt+file)).Contains(pick)&&!Regex.IsMatch(Code(R(rt+file)),@"Graphics\.DrawMesh(Instanced)?\((block|kitMesh|metroMesh|wzMesh|stationMesh|iwMesh),"),file+" draws the rounded box only while the style is on (made once), the legacy mesh otherwise");
            Check(Code(R(rt+"NativeFactoryPresentation.cs")).Contains("sceneryLit.SetFloat(\"_ToonEdgeRef\",16);"),"the Factory's scenery is marked for the outline");
            Check(lighting.Contains("Shader.SetGlobalVector(ToonFloorId,V(Style.Floor));")&&Code(R("Assets/ChipArena/Resources/NativeBlockLit.shader")).Contains("alb=max(alb,_ArenaToonFloor.x*_ArenaToonShade.rgb/max(max(_ArenaToonShade.r,_ArenaToonShade.g),max(_ArenaToonShade.b,0.0001)));"),"the style's albedo floor (shadeFloor, 0 but on Cyber City) is written with the toon globals and read only in the toon paint");
            Check(Code(R("Assets/ChipArena/Resources/NativeBlockLit.shader")).Contains("float show=max(max(smoothstep(0.4,0.48,BkFbm(uv*0.42+seed*9.0)),1.0-smoothstep(0.3,0.75,min(edge,yb))),_ArenaToonFloor.y)*(1.0-smoothstep(0.02,0.045,fwm));"),"a look's brickCourses draws the toon brick's courses over that share of the face (0: stage 1's patches), faded out at the same footprint");
            Check(lighting.Contains("Shader.SetGlobalVector(ToonCoolId,V(Style.Cool));")&&lighting.Contains("ToonCoolId=Shader.PropertyToID(\"_ArenaToonCool\")"),"the point lights' cooling (pointCool, 0 but on the Ironworks) is written with the toon globals");
            Check(NativeMapStyle.PointCool(0,1,.35f)==1&&NativeMapStyle.PointCool(.35f,1,.35f)==0&&NativeMapStyle.PointCool(.1f,0,.35f)==0&&NativeMapStyle.PointCool(.1f,1,.35f)>NativeMapStyle.PointCool(.2f,1,.35f),"a dim point light turns fully to the shade's hue, a light at the level keeps its own colour, 0 is off (ToonPoints' CPU copy)");
            Check(lighting.Contains("sky.SetVector(SkyToonFogId,VC(Style.SkyFog));")&&Code(R("Assets/ChipArena/Resources/NativeNightSky.shader")).Contains("return lerp(col*(1-c.a)+c.rgb,_SkyToonFog.rgb,_SkyToonFog.w*(1-smoothstep(0,2*_SkyToon.w,up)));"),"the anime sky's horizon blends into the fog colour by the look's skyFog (0 on the stage 1 looks)");
            Check(Code(R(rt+"NativeBrickfallPresentation.cs")).Contains("NativeBrickfallScenery.Build(NativeMapStyle.On(NativeMapLooks.Find(NativeLighting.LookSet(),\"brickfall\"),NativeLighting.LegacyStyle))"),"Brickfall's skyline takes its rounder tanks and stacks only with the style");
            var fx=Code(R(rt+"NativeStructureFxPresentation.cs"));Check(fx.Contains("bool toon=NativeLighting.StyleActive;")&&fx.Contains("NativeMapStyle.Diffuse(ambient,key,ndl,1,s.Shade[k],s.Shade[3],s.Lit[k],s.Band[0],s.Band[1],s.Band[2],s.Toon[1])"),"the debris takes the style's ramp (the destruction holds the look)");
            Check(fx.Contains("private readonly float[] bevelCorners=new float[12];")&&fx.Contains("int k=NativeMapStyle.BevelFacet(f,hx,hy,hz,bev,bevelCorners,out float nx,out float ny,out float nz);"),"the style's debris is the bevelled cube, made without allocating");
        }
        // ---- the data ----
        private static void Data(MapLookSet set)
        {
            foreach(var id in new[]{"expedition-city","brickfall","factory","cybercity","siren-quarantine","expedition-transit","expedition-foundry"})
            {
                var t=NativeMapLooks.Find(set,id).toon;NativeMapStyle.Validate(id,t);
                Check(t.line>0&&t.lineFadeEnd<=NativeMapLooks.Find(set,id).fog.end&&t.skyToon==(NativeMapLooks.Find(set,id).sky.variant=="none"?0:1)&&t.saturation>=1&&t.saturation<=1.2f&&t.detail<1,id+": outline faded within the fog, the anime sky where the map has a sky (none sealed), a light grade, simplified detail");
                Check(t.shade.z>t.shade.x&&(t.sky.variant=="night"||t.lit.x>=t.lit.z),id+": cool shade, warm light (a night map keeps its moonlight)");
                Check(t.copings==1&&t.chips==(id=="expedition-city"||id=="brickfall"?1:0)&&t.posts==1&&t.specular>0&&t.occlusion>0&&t.occlusionHeight>0&&t.halfTone<.2f,id+": copings, posts, the highlight and the occlusion band on (broken corners on the old masonry maps only); two main bands");
                Check(id=="expedition-city"||id=="cybercity"?t.plinths==0:t.plinths==1,id+": plinths and pilasters on, off in the cities (their kit doors and shop fronts meet the street)");
                var raw=NativeMapLooks.Find(set,id);Check(t.key.bearing==raw.key.bearing&&t.key.elevation==raw.key.elevation&&t.sky.variant==raw.sky.variant,id+": the map keeps its own key light's direction and its sky (time of day)");
                if(id=="factory")Check(t.detail>=.5f&&t.shade.y>t.shade.x&&t.shade.z>t.shade.x&&(t.key.color>>16&255)>(t.key.color&255)+60,"factory: the grating and corrugation keep their pattern at 0.5 detail, a rusty orange key against teal shade");
                if(id=="siren-quarantine")Check(t.skyToon==0&&t.sky.variant=="none"&&t.lineFadeStart==25&&t.lineFadeEnd==70&&Math.Abs(t.detail-.4f)<1e-5&&t.lit.x>t.lit.z&&t.shade.z>t.shade.y,"Ward Zero: sealed (no sky), lines 25 to 70 m indoors, tiles at 0.4 detail, warm troffer light against cool shade");
                if(id=="expedition-foundry")Check(t.skyToon==0&&t.sky.variant=="none"&&Math.Abs(t.detail-.4f)<1e-5&&t.lit.x>t.lit.z&&t.points<1,"Ironworks: sealed, black iron at 0.4 detail, warm light, the heat glow's stairs blended");
                if(id=="expedition-foundry")Check(t.brickCourses==1&&t.pointCool>.5f&&t.pointCoolLevel>0&&(t.fogColor&255)>(t.fogColor>>16&255)&&t.contrast<=1,"Ironworks: the furnace walls' brick in courses, the furnace light cooling to steel blue away from the heat, a cool fog, no darkening contrast");
                Check(id=="expedition-foundry"?t.brickCourses>0&&t.pointCool>0:t.brickCourses==0&&t.pointCool==0,id+": brick courses over the whole face and the cooling point lights only on the Ironworks");
                if(id=="expedition-transit")Check(t.skyToon==1&&t.sky.variant=="night"&&t.flatten>=.35f&&t.points>=.7f,"Central Station: night under the broken roof, terrazzo and tile flattened, sodium pools in stairs");
                if(id=="factory")Check(t.skyFog>.5f&&t.shade.y>=t.shade.z,"factory: the horizon blends into the fog (no hard band over the skyline), teal shade (green as strong as blue)");
                Check(id=="cybercity"?t.shadeFloor>0&&t.shadeFloor<=.08f:t.shadeFloor==0,id+": an albedo floor only where near-black paint lost its form (Cyber City)");
                if(id=="cybercity")Check(t.lineFadeStart==30&&t.lineFadeEnd==90&&raw.fog.end>=t.lineFadeEnd&&t.fogColor==0x2A3460&&t.sky.variant=="night","cybercity: lines fade 30 to 90 m in the rain, the dark blue #2A3460 shade fog, night");
                MapLookToon Copy()=>(MapLookToon)typeof(MapLookToon).GetMethod("MemberwiseClone",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(t,null);
                foreach(var (what,breakIt) in new (string,Action<MapLookToon>)[]{("softness 0",x=>x.softness=0),("edges crossed",x=>x.lightEdge=x.shadeEdge),("tint 0",x=>x.shade=new V3(0,1,1)),("a line without a fade",x=>x.lineFadeEnd=x.lineFadeStart),("a band as high as the sky",x=>{x.skyToon=1;x.bandWidth=1;}),("a sky fog over 1",x=>x.skyFog=1.5f),("a floor over 0.2",x=>x.shadeFloor=.3f),("brick courses over 1",x=>x.brickCourses=1.5f),("a cooling light without a level",x=>{x.pointCool=1;x.pointCoolLevel=0;}),("a colour out of range",x=>x.cloudLit=0x1000000),("a negative fog start",x=>x.fogStart=-1),("NaN",x=>x.wash=float.NaN),("a shape switch past 1",x=>x.chips=2),("a highlight past every angle",x=>{x.specular=.5f;x.specularEdge=.5f;}),("an occlusion band 2 m high",x=>x.occlusionHeight=2),("occlusion NaN",x=>x.occlusion=float.NaN)})
                {var b=Copy();breakIt(b);bool threw=false;try{NativeMapStyle.Validate(id,b);}catch(InvalidOperationException){threw=true;}Check(threw,id+": refused: "+what);}
            }
            var off=new MapLookToon{on=false,softness=float.NaN};NativeMapStyle.Validate("x",off);Check(true,"a look with the style off is never read");
            Check(set.maps.Count(m=>m.toon!=null&&m.toon.on)==12,"twelve looks carry the style (stage 1's pilots; stage 2's Factory, Cyber City, quarantine, Central Station and the Ironworks; the five M1 stages)");
        }
    }
}
#endif
