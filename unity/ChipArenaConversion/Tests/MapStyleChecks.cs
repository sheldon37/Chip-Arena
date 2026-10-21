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
    // and NativeBloom, Runtime/NativeToonOutline.cs and NativeLighting's switch). Checked: the switch (only the two pilot looks
    // turn it on, the legacy command line and every other map leave every global at 0, the styled look keeps every field the
    // kits' budgets read), the shaders' constants and formulas against the Core copy, the legacy paths kept exactly (each toon
    // path behind the one uniform branch, Lambert and the bloom composite untouched while it is off), the ramp maths, the
    // rounded mesh (closed, outward, never past the block, exact legacy faces, deterministic, under the legacy mesh's budget),
    // the shape rules, the outline's line rule on synthetic depth, nothing allocated per frame, the runtime hooks and the data.
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
            Switch(set);Console.WriteLine("PASS map style: the switch (only expedition-city and brickfall turn it on; legacy and every other map write 0) and the styled look keeps the fog end, lights, block and kit looks and dressing the budgets read ("+count+" checks)");
            Shaders(root,R);Console.WriteLine("PASS map style: the shaders' toon constants and formulas are the Core's, every legacy path is kept behind the one uniform branch, the outline marks and tests stencil bit 16 ("+count+" checks)");
            Ramp();Console.WriteLine("PASS map style: the ramp (three bands, monotonic, a cast shadow takes the turned-away shade), the soft stairs, the stepped point lights and the rounded normal ("+count+" checks)");
            Mesh();Console.WriteLine("PASS map style: the rounded box is closed and outward at every size, inside the block, on the legacy faces, deterministic and under the legacy S0 mesh's vertices and triangles; stages 1-4 keep the legacy meshes ("+count+" checks)");
            Shapes();Console.WriteLine("PASS map style: rods round at the lattice clamp, mid blocks grow their chamfer to 8 cm, big blocks keep theirs, a taper narrows the top 30% ("+count+" checks)");
            Edges();Console.WriteLine("PASS map style: the outline lines silhouettes on the nearer pixel only, creases where 1/depth bends, never a plane, and fades out with distance ("+count+" checks)");
            Allocation(set);Console.WriteLine("PASS map style: the ramp, stairs, point gain, diffuse, radius, round normal, chamfer, lattice, edge and the style state allocate nothing per frame ("+count+" checks)");
            Runtime(root,R);Console.WriteLine("PASS map style: NativeLighting writes the globals and turns them off on stages and the rooftop, the outline pass is a command buffer at BeforeForwardAlpha off on the low tier, the kits draw the rounded box only while the style is on, the debris takes the ramp ("+count+" checks)");
            Data(set);Console.WriteLine("PASS map style: the pilot looks' values validate, broken ones are refused, the old maps' looks are untouched ("+count+" checks)");
        }
        // ---- the switch and the styled look ----
        private static void Switch(MapLookSet set)
        {
            var on=set.maps.Where(m=>NativeMapStyle.On(m,false)).Select(m=>m.id).OrderBy(x=>x).ToArray();
            Check(on.SequenceEqual(new[]{"brickfall","expedition-city"}),"only the stage 1 pilots turn the style on: "+string.Join(",",on));
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
            Check(Define(toon,"TOON_POINT_STEPS")==NativeMapStyle.PointSteps&&Define(toon,"TOON_STAIR")==NativeMapStyle.Stair&&Define(toon,"TOON_RADIUS_MAX")==NativeMapStyle.RadiusMax&&Define(toon,"TOON_EDGE_GAIN")==NativeMapStyle.EdgeGain,"NativeToon.cginc's constants are NativeMapStyle's");
            string shared=toon.Substring(toon.IndexOf("TOON SHARED BEGIN",StringComparison.Ordinal),toon.IndexOf("TOON SHARED END",StringComparison.Ordinal)-toon.IndexOf("TOON SHARED BEGIN",StringComparison.Ordinal));
            foreach(var f in new[]{"return _ArenaToonBand.z*ToonStep(x,_ArenaToonBand.x,s)+(1.0-_ArenaToonBand.z)*ToonStep(x,_ArenaToonBand.y,s);","float ToonStairs(float x){float f=floor(x);return f+smoothstep(0.5-TOON_STAIR,0.5+TOON_STAIR,x-f);}",
                "if(_ArenaToon.x<0.5)return c;","float q=ToonStairs(sqrt(l)*TOON_POINT_STEPS)/TOON_POINT_STEPS;","return c*lerp(1.0,q*q/l,_ArenaToonLit.w);","float r=ToonRamp(saturate(ndl)*atten);","float3 shade=(amb+key*_ArenaToonShade.w)*_ArenaToonShade.rgb;",
                "return lerp(shade,amb+key*_ArenaToonLit.rgb,r);","float3 core=max(h-float3(r,r,r),float3(0.0,0.0,0.0));float3 q=obj-clamp(obj,-core,core);","return dot(n,facet)>0.5?n:facet;","return min(0.9*lo,clamp(_ArenaToonBand.w*lo,c,TOON_RADIUS_MAX));"})
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
            Check(Body(block,"void BlockVertex(").Contains("ToonLattice(v.texcoord.xyz,v.texcoord1.xyz,size.xyz,size.w,flags,n)")&&!Regex.IsMatch(Body(block,"float3 ToonLattice(")+Body(block,"float ToonChamfer("),@"\b(ddx|ddy|fwidth)\b"),"the vertex stage and the shadow caster place the style's lattice, without derivatives");
            string frag=Body(block,"fixed4 frag(");
            Check(frag.Contains("if(_ArenaToon.x>0.5)")&&frag.Contains("float3 light=ShadeSH9(float4(nW,1.0))+_LightColor0.rgb*(saturate(dot(nW,_WorldSpaceLightPos0.xyz))*atten)+BlockLights(i.world.xyz,nW);")&&frag.Contains("col=s.albedo*light+s.emit;")
                &&frag.Contains("fixed4 c=fixed4(BlockHaze(col,i.world.xyz,distance(i.world.xyz,_WorldSpaceCameraPos)),1.0);"),"the fragment's legacy light is today's, behind the one branch");
            var surface=Body(block,"BlockSurf BlockSurface(");
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
            Check(Define(edge,"EDGE_GAP_MIN")==NativeMapStyle.GapMin&&Define(edge,"EDGE_GAP_SHARE")==NativeMapStyle.GapShare&&Define(edge,"EDGE_CREASE")==NativeMapStyle.CreaseWeight,"NativeToonEdge's constants are NativeMapStyle's");
            var line=Body(edge,"float EdgeLine(float2 uv,float2 px)");
            Check(line.Contains("float gap=EDGE_GAP_MIN+EDGE_GAP_SHARE*c;float sil=step(gap,max(l+r,d+u)-2.0*c);")&&line.Contains("float ic=1.0/c;float bend=max(abs(1.0/l+1.0/r-2.0*ic),abs(1.0/d+1.0/u-2.0*ic))/ic;")&&line.Contains("float crease=smoothstep(_ArenaEdge.w,_ArenaEdge.w*2.0,bend)*step(c-min(min(l,r),min(d,u)),gap);")
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
            Check(NativeMapStyle.Edge(fe+1,fe+1,fe+40,fe+1,fe+1,s,fs,fe,cr)==0&&NativeMapStyle.Edge(10,10,30,10,10,0,fs,fe,cr)==0,"faded out past the fade end, nothing at strength 0");
        }
        // ---- nothing allocated per frame ----
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static float Frame(MapLook look,MapStyleState state)
        {
            float sum=0;
            for(int i=0;i<2000;i++)
            {
                float x=i/2000f;sum+=NativeMapStyle.Ramp(x,.06f,.42f,.5f,.035f)+NativeMapStyle.Stairs(x*3)+NativeMapStyle.PointGain(x,.7f)+NativeMapStyle.Diffuse(.1f,.9f,x,1,.8f,.06f,1.1f,.06f,.42f,.5f,.035f)+NativeMapStyle.Radius(x,1,2,.05f,.1f);
                NativeMapStyle.RoundNormal(x,1,0,1,1,1,.2f,0,1,0,out float nx,out float ny,out float nz);sum+=nx+ny+nz+NativeMapStyle.Chamfer(x,2,.1f,.05f);
                NativeMapStyle.Lattice(1,1,-1,-1,0,1,x+.1f,1,1,.05f,NativeMapStyle.Taper,out float px,out float py,out float pz);sum+=px+py+pz+NativeMapStyle.Edge(10+x,10,30,10,10,1,40,110,.02f);
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
            Check(lighting.Contains("private static void EnterStage(){StyleOff();")&&Body(lighting,"public static void Rooftop(Camera camera,Material skybox)").Contains("StyleOff();"),"the entrance stages, the draft and the rooftop turn the style off (Apply turns the map's back on)");
            Check(Body(lighting,"public static void StyleOff()").Contains("Shader.SetGlobalVector(ToonId,Vector4.zero);")&&Body(lighting,"public static void StyleOff()").Contains("Shader.SetGlobalVector(EdgeId,Vector4.zero);"),"off: _ArenaToon and _ArenaEdge to 0");
            Check(lighting.Contains("Styled((string.IsNullOrEmpty(map.look)?null:NativeMapLooks.ByLook(LookSet(),map.look))??NativeMapLooks.Resolve(")&&Body(lighting,"private static MapLook Styled(MapLook raw)").Contains("styled.TryGetValue(raw,out var s)"),"Look gives the styled look, made once per look (no per-frame copy)");
            Check(outline.Contains("view.AddCommandBuffer(CameraEvent.BeforeForwardAlpha,buffer);")&&outline.Contains("NativeLighting.StyleActive&&NativeLighting.Style.Outline&&!LowGraphics")&&outline.Contains("buffer.DrawMesh(triangle,Matrix4x4.identity,material,0,0);"),"the outline: one draw from a command buffer before the transparent queue, only while the style draws it and not on the low tier");
            Check(!Regex.IsMatch(Body(outline,"private void LateUpdate()"),@"\bnew\b"),"the outline's frame allocates nothing");
            Check(game.Contains("outline=NativeToonOutline.Attach(cameraView);")&&game.IndexOf("bloom=NativeBloom.Attach(cameraView);",StringComparison.Ordinal)<game.IndexOf("outline=NativeToonOutline.Attach(cameraView);",StringComparison.Ordinal)&&settings.Contains("if(outline!=null)outline.Quality(low);"),"on the match camera after the bloom, off on the low tier");
            foreach(var (file,pick) in new[]{("NativeArenaDressing.cs","s==0&&NativeLighting.StyleActive?(toonMesh??=MeshOf(NativeToonMesh.Build(),0)):meshes[s]"),("NativeExpeditionCityPresentation.cs","private Mesh TownMesh=>NativeLighting.StyleActive?(townToon??=MetroMeshOf(NativeToonMesh.Build())):townMesh;"),
                ("NativeExpeditionHubPresentation.cs","private Mesh Shape=>NativeLighting.StyleActive?(toonMesh??=MeshOf(NativeToonMesh.Build())):mesh;"),("NativeExpeditionStatesPresentation.cs","private Mesh Shape=>NativeLighting.StyleActive?(toonMesh??=MeshOf(NativeToonMesh.Build())):mesh;")})
                Check(Code(R(rt+file)).Contains(pick),file+" draws the rounded box only while the style is on (made once)");
            Check(Code(R(rt+"NativeBrickfallPresentation.cs")).Contains("brickfallLit.SetFloat(\"_ToonEdgeRef\",16);"),"Brickfall's skyline is marked for the outline");
            var fx=Code(R(rt+"NativeStructureFxPresentation.cs"));Check(fx.Contains("bool toon=NativeLighting.StyleActive;")&&fx.Contains("NativeMapStyle.Diffuse(ambient,key,ndl,1,s.Shade[k],s.Shade[3],s.Lit[k],s.Band[0],s.Band[1],s.Band[2],s.Toon[1])"),"the debris takes the style's ramp (the destruction holds the look)");
        }
        // ---- the data ----
        private static void Data(MapLookSet set)
        {
            foreach(var id in new[]{"expedition-city","brickfall"})
            {
                var t=NativeMapLooks.Find(set,id).toon;NativeMapStyle.Validate(id,t);
                Check(t.line>0&&t.lineFadeEnd<=NativeMapLooks.Find(set,id).fog.end&&t.skyToon==1&&t.saturation>=1&&t.saturation<=1.2f&&t.detail<1,id+": outline faded within the fog, anime sky, a light grade, simplified detail");
                Check(t.shade.z>t.shade.x&&t.lit.x>=t.lit.z,id+": cool shade, warm light");
                MapLookToon Copy()=>(MapLookToon)typeof(MapLookToon).GetMethod("MemberwiseClone",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(t,null);
                foreach(var (what,breakIt) in new (string,Action<MapLookToon>)[]{("softness 0",x=>x.softness=0),("edges crossed",x=>x.lightEdge=x.shadeEdge),("tint 0",x=>x.shade=new V3(0,1,1)),("a line without a fade",x=>x.lineFadeEnd=x.lineFadeStart),("a band as high as the sky",x=>x.bandWidth=1),("a colour out of range",x=>x.cloudLit=0x1000000),("a negative fog start",x=>x.fogStart=-1),("NaN",x=>x.wash=float.NaN)})
                {var b=Copy();breakIt(b);bool threw=false;try{NativeMapStyle.Validate(id,b);}catch(InvalidOperationException){threw=true;}Check(threw,id+": refused: "+what);}
            }
            var off=new MapLookToon{on=false,softness=float.NaN};NativeMapStyle.Validate("x",off);Check(true,"a look with the style off is never read");
            Check(set.maps.Count(m=>m.toon!=null&&m.toon.on)==2,"two looks carry the style (stage 1)");
        }
    }
}
#endif
