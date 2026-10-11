#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // Arena block looks (Core/NativeBlockLooks.cs, Resources/NativeBlockLit.shader, MapLooks.json blockLooks/blocks/preview):
    // the shader's look ids, flags, colours and formulas against the Core rules and ARENA_PROPOSAL.md 2.3/3.10, the stage
    // meshes (closed, outward at every block size and turn, never past the collider, the chamfer, shades, chips, waist and
    // stub), stages, look selection (Brickfall's entries, the draft script's ids, the dev preview, material defaults), the
    // opt-in (no old map changes by default), the draw budget, the dust coat, the flash fall-off and validation.
    internal static class BlockLookChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Block looks: "+what);count++;}
        private static double Lin(int c)=>c/255.0<=.04045?c/255.0/12.92:Math.Pow((c/255.0+.055)/1.055,2.4);
        public static IEnumerable<string> Run(string root,ConversionContent content)
        {
            string shader=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativeBlockLit.shader"));
            // Look ids and flags: the shader's defines are the Core's keys and constants.
            string[] names={"PLAIN","BRICK_RED","BRICK_BUFF","TERRACOTTA","CONCRETE","STEEL","TIMBER","TAR","ASPHALT","COBBLE","WINDOW","NEON","CORRUGATED","DEMOLITION"};
            Check(names.Length==NativeBlockLooks.Keys.Length&&NativeBlockLooks.Palette.Length==NativeBlockLooks.Keys.Length,"one shader id and palette colour per key");
            for(int i=0;i<names.Length;i++){var m=Regex.Match(shader,@"#define LOOK_"+names[i]+@" ([0-9.]+)\r?\n");Check(m.Success&&float.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture)==i,"LOOK_"+names[i]+" is "+i+" ("+NativeBlockLooks.Keys[i]+")");Check(NativeBlockLooks.Id(NativeBlockLooks.Keys[i])==i,"Id("+NativeBlockLooks.Keys[i]+")");}
            foreach(var (n,v) in new[]{("BAND",NativeBlockLooks.Band),("TAG",NativeBlockLooks.Tag),("FRAGMENT",NativeBlockLooks.Fragment),("STROBE",NativeBlockLooks.Strobe),("DARK",NativeBlockLooks.Dark),("SPIN_Y",NativeBlockLooks.SpinY),("SPIN_Z",NativeBlockLooks.SpinZ)})
                Check(shader.Contains("#define FLAG_"+n+" "+v+"u"),"FLAG_"+n+" = "+v);
            int allFlags=NativeBlockLooks.SpinZ*2-1;Check(allFlags*16+NativeBlockLooks.Keys.Length<1<<24,"look + 16 x flags stays exact in a float");
            // 3.10 palette per look key.
            var palette=new Dictionary<string,int>{["brick-red"]=0x9C4A30,["brick-buff"]=0xB8774E,["terracotta"]=0xE3C9A0,["concrete-board"]=0x8F8A80,["steel-green"]=0x3E6B5A,["timber"]=0xB4834F,["tar"]=0x2B2A2E,["asphalt"]=0x3A3836,["cobble"]=0x6B625A,["window-lit"]=0x6E8F96,["neon"]=0x4FA8FF,["demolition"]=0xF4B321};
            foreach(var p in palette)Check(NativeBlockLooks.Palette[NativeBlockLooks.Id(p.Key)]==p.Value,p.Key+" palette");Check(NativeBlockLooks.Palette[NativeBlockLooks.Plain]==-1,"plain keeps the block's colour");
            // Shader colours: the 3.10 / 2.3 hex values in linear light.
            var colours=new Dictionary<string,int>{["MORTAR"]=0xCDBB9E,["FRAME"]=0x1F2A30,["LIT"]=0xFFB45E,["RUST"]=0x7A3E22,["YELLOW"]=0xF4B321,["BLACK"]=0x1C1A18,["DANGER"]=0xD6332A,["INK"]=0xF2EDE4,["DUST"]=0xB8A68F,["SOOT"]=0x4A403A,["COAT"]=0xB3A48E,["FRESH_CONCRETE"]=0xC9C2B4,["FRESH_BRICK"]=0xD2BFA3};
            foreach(var c in colours)
            {
                var m=Regex.Match(shader,@"#define BK_"+c.Key+@" float3\(([-0-9.]+),([-0-9.]+),([-0-9.]+)\)");Check(m.Success,"BK_"+c.Key+" defined");
                for(int k=0;k<3;k++)Check(Math.Abs(double.Parse(m.Groups[k+1].Value,CultureInfo.InvariantCulture)-Lin((c.Value>>(16-8*k))&255))<6e-5,"BK_"+c.Key+" channel "+k+" is #"+c.Value.ToString("X6")+" in linear light");
            }
            for(int k=0;k<3;k++){double ratio=Lin((0x7E3A26>>(16-8*k))&255)/Lin((0x9C4A30>>(16-8*k))&255);Check(Math.Abs(ratio-.63)<.03,"dark course #7E3A26 = brick red x .63 in linear light, channel "+k+": "+ratio);}
            Check(shader.Contains("#define BK_DARK_COURSE 0.63"),"dark course factor");
            yield return "block looks: 14 look ids, 7 flags, the 3.10 palette and 13 shader colours (2.3 chevron yellow/black, danger red) match the Core and the proposal";
            // Formulas the Core mirrors, SM 3.5 rules, the light block's own arrays.
            foreach(var expr in new[]{"float3 p=a*h+sign(b)*min(abs(b)*c,0.42*abs(a)*h);","#define BK_GRIME 0.22","1.0-smoothstep(1.2,1.8,yb)","smoothstep(1.0-fw.y,1.0+fw.y,yb)*(1.0-smoothstep(1.6-fw.y,1.6+fw.y,yb))",
                "float a=saturate(1.0-q*q);","_ArenaLightColor[k].rgb*(a*a*saturate(dot(n,d/len)))","saturate(5.0*(1.0-q))/(1.0+25.0*q)","smoothstep(4.0,40.0,dist)","alb*=shade.rgb*1.25;",
                "float4 _ArenaLightPos[8];float4 _ArenaLightColor[8];float4 _ArenaFlashPos[3];float4 _ArenaFlashColor[3];","\"LightMode\"=\"ForwardBase\"","\"LightMode\"=\"ShadowCaster\"","#pragma target 3.5","#pragma multi_compile_instancing","ShadeSH9(float4(nW,1.0))","UNITY_LIGHT_ATTENUATION(atten,i,i.world.xyz)","UNITY_APPLY_FOG(i.fogCoord,c)"})
                Check(shader.Contains(expr),"shader has "+expr);
            Check(NativeBlastSim.LightCapacity==3&&NativeMapLooks.MaxLights==8,"3 flash and 8 practical slots, as the shader's arrays");
            string code=Regex.Replace(shader,@"//[^\n]*","");
            Check(!code.Contains("_StageLight")&&!code.Contains("ForwardAdd")&&!Regex.IsMatch(code,@"\btex2D|sampler2D")&&!code.Contains("#pragma surface"),"own light arrays (not the entrance stage's), no add pass, no textures");
            string Body(string head){int at=shader.IndexOf(head,StringComparison.Ordinal);Check(at>=0,"shader function "+head);int open=shader.IndexOf('{',at),depth=0;for(int i=open;i<shader.Length;i++){if(shader[i]=='{')depth++;else if(shader[i]=='}'&&--depth==0)return shader.Substring(at,i-at+1);}throw new Exception("unclosed "+head);}
            foreach(var head in new[]{"float3 BlockLattice(","void BlockVertex(","v2f vert(","v2fs vertShadow("})Check(!Regex.IsMatch(Body(head),@"\b(ddx|ddy|fwidth|tex2D)\b"),"no derivatives in the vertex stage: "+head);
            string frag=Body("fixed4 frag(");int deriv=Math.Max(frag.LastIndexOf("fwidth",StringComparison.Ordinal),frag.LastIndexOf("ddy",StringComparison.Ordinal));Check(deriv>0&&deriv<frag.IndexOf("BlockSurface(",StringComparison.Ordinal),"every derivative is taken before the branching look code");
            Check(!Regex.IsMatch(Body("BlockSurf BlockSurface("),@"\b(ddx|ddy|fwidth)\b")&&!Regex.IsMatch(Body("float3 BlockLights("),@"\b(ddx|ddy|fwidth)\b"),"no derivatives inside the look and light code");
            // The tag's glyphs spell DANGER in 3x5 cells.
            var gm=Regex.Match(shader,@"float g=L<0\.5\?(\d+)\.0:L<1\.5\?(\d+)\.0:L<2\.5\?(\d+)\.0:L<3\.5\?(\d+)\.0:L<4\.5\?(\d+)\.0:(\d+)\.0;");Check(gm.Success,"glyph table");
            string[][] font={new[]{"110","101","101","101","110"},new[]{"010","101","111","101","101"},new[]{"110","101","101","101","101"},new[]{"011","100","101","101","011"},new[]{"111","100","110","100","111"},new[]{"110","101","110","101","101"}};
            for(int l=0;l<6;l++){int g=int.Parse(gm.Groups[l+1].Value);for(int r=0;r<5;r++)for(int c=0;c<3;c++)Check(((g>>(14-(r*3+c)))&1)==(font[l][r][c]=='1'?1:0),"glyph "+"DANGER"[l]+" row "+r);}
            yield return "block looks: the shader's lattice, grime ramp (22% to 1.2 m, gone by 1.8 m), 1.0-1.6 m band, fall-offs and haze are the Core's; forward base + shadow caster, SM 3.5, no vertex derivatives, own 8+3 light arrays; the tag spells DANGER";
            // Stage meshes at every size and turn: closed, outward, inside the collider; the chamfer, shades, chips, waist and stub.
            var sizes=new[]{(1f,1f,1f),(2.5f,3.05f,.3f),(1.2f,3.6f,1.2f),(64f,1.5f,56f),(.3f,14.4f,.3f),(.04f,.04f,.04f),(8f,.24f,1.2f),(.46f*2.5f,.44f*3.05f,.88f*.3f)};
            int[] verts={216,216,264,216,216},tris={188,188,252,188,188};
            Check(NativeBlockLooks.Meshes==5&&NativeBlockLooks.MeshFor(2,.9f)==2&&NativeBlockLooks.MeshFor(2,.89f)==4&&NativeBlockLooks.MeshFor(1,.1f)==1&&NativeBlockLooks.MeshFor(3,5)==3,"S2 is waisted on blocks 1.8 m tall or more, else mesh 4");
            for(int stage=0;stage<NativeBlockLooks.Meshes;stage++)
            {
                var m=NativeBlockLooks.Build(stage);Check(m.Vertices==verts[stage]&&m.Triangles.Length==tris[stage]*3,"mesh "+stage+" size "+m.Vertices+"/"+m.Triangles.Length/3);
                Check(m.Lattice.Length==m.Vertices*4&&m.Inset.Length==m.Vertices*4&&m.Shade.Length==m.Vertices*4&&m.Normal.Length==m.Vertices*3,"mesh "+stage+" attribute lengths");
                foreach(var (sx,sy,sz) in sizes)foreach(int spin in new[]{0,NativeBlockLooks.SpinY,NativeBlockLooks.SpinZ,NativeBlockLooks.SpinY|NativeBlockLooks.SpinZ})
                {
                    if(NativeBlockLooks.MeshFor(stage==4?2:stage,sy/2)!=stage)continue;
                    float hx=sx/2,hy=sy/2,hz=sz/2;var p=Positions(m,hx,hy,hz,spin,out var n);
                    for(int v=0;v<m.Vertices;v++)Check(Math.Abs(p[v*3])<=hx+1e-5&&Math.Abs(p[v*3+1])<=hy+1e-5&&Math.Abs(p[v*3+2])<=hz+1e-5,"stage "+stage+" size "+sx+"x"+sy+"x"+sz+" vertex inside the collider");
                    double area=0,flipped=0;
                    for(int t=0;t<m.Triangles.Length;t+=3)
                    {
                        int a=m.Triangles[t],b=m.Triangles[t+1],c=m.Triangles[t+2];double ux=p[b*3]-p[a*3],uy=p[b*3+1]-p[a*3+1],uz=p[b*3+2]-p[a*3+2],wx=p[c*3]-p[a*3],wy=p[c*3+1]-p[a*3+1],wz=p[c*3+2]-p[a*3+2];
                        double cx=uy*wz-uz*wy,cy=uz*wx-ux*wz,cz=ux*wy-uy*wx,len=Math.Sqrt(cx*cx+cy*cy+cz*cz);area+=len;if(cx*n[a*3]+cy*n[a*3+1]+cz*n[a*3+2]<-1e-7*Math.Max(len,1e-12)-1e-12)flipped+=len;
                    }
                    Check(flipped<=area*1e-9,"stage "+stage+" size "+sx+"x"+sy+"x"+sz+" spin "+spin+": every triangle faces out ("+flipped+" of "+area+")");
                    if(spin==0)Check(Closed(m,p),"stage "+stage+" size "+sx+"x"+sy+"x"+sz+": closed (each edge shared by two triangles in opposite order)");
                }
                // The shades baked into vertex colour: bevels +18%, corners -22%, faces 1, stored x .8.
                var shades=new HashSet<float>();for(int v=0;v<m.Vertices;v++)shades.Add((float)Math.Round(m.Shade[v*4]/NativeBlockLooks.ShadeStore,4));
                foreach(float s in new[]{1f,1.18f,.78f,(float)Math.Round(1.18*.78,4)})Check(shades.Contains(s),"stage "+stage+" bakes shade "+s);Check(shades.All(s=>s<=1.18f+1e-4&&s>=.78f-1e-4)&&m.Shade.Where((x,i)=>i%4!=3).All(x=>x<=1),"stage "+stage+" shades within -22%..+18% and stored at or below 1");
                int freshCorners=0;for(int t=0;t<m.Triangles.Length;t+=3){int a=m.Triangles[t];bool corner=Enumerable.Range(0,3).All(k=>m.Shade[m.Triangles[t+k]*4+3]==1);if(corner&&Math.Abs(m.Shade[a*4]/NativeBlockLooks.ShadeStore-.78f)<1e-4)freshCorners++;}
                Check(stage==0?m.Shade.Where((x,i)=>i%4==3).All(x=>x==0):m.Shade.Where((x,i)=>i%4==3).Any(x=>x>0),"stage "+stage+" fresh breaks: none at S0, some at S1-S3");
                if(stage==1)Check(freshCorners==3,"S1 chips 3 corners: "+freshCorners);if(stage==2||stage==4)Check(freshCorners==6,"S2 chips 6 corners: "+freshCorners);
            }
            {
                // The waisted S2 on every pier, wall and column height it is used for (1.8 m and up), at every turn.
                var m=NativeBlockLooks.Build(2);int tested=0;
                foreach(float sy in new[]{1.8f,2.2f,3.05f,3.6f,7.2f,14.4f})foreach(float sx in new[]{.04f,.3f,1.2f,2.5f,8f,64f})foreach(int spin in new[]{0,NativeBlockLooks.SpinY,NativeBlockLooks.SpinZ})
                {
                    var p=Positions(m,sx/2,sy/2,sx*.7f/2,spin,out var n);double flipped=0;tested++;
                    for(int t=0;t<m.Triangles.Length;t+=3){int a=m.Triangles[t],b=m.Triangles[t+1],c=m.Triangles[t+2];double ux=p[b*3]-p[a*3],uy=p[b*3+1]-p[a*3+1],uz=p[b*3+2]-p[a*3+2],wx=p[c*3]-p[a*3],wy=p[c*3+1]-p[a*3+1],wz=p[c*3+2]-p[a*3+2];double cx=uy*wz-uz*wy,cy=uz*wx-ux*wz,cz=ux*wy-uy*wx,len=Math.Sqrt(cx*cx+cy*cy+cz*cz);if(cx*n[a*3]+cy*n[a*3+1]+cz*n[a*3+2]<-1e-7*Math.Max(len,1e-12)-1e-12)flipped+=len;}
                    Check(flipped==0,"waisted S2 at "+sx+"x"+sy+" faces out");if(spin==0)Check(Closed(m,p),"waisted S2 at "+sx+"x"+sy+" closed");
                }
                Check(tested==108,"108 waisted sizes");
            }
            {
                var s0=NativeBlockLooks.Build(0);var p=Positions(s0,1,1,1,0,out _);double maxX=0;for(int v=0;v<s0.Vertices;v++)maxX=Math.Max(maxX,p[v*3]);Check(Math.Abs(maxX-1)<1e-6,"S0's faces reach the collider's planes");
                // On a 2 m cube a face corner sits a chamfer in from both edges: the 5 cm bevel.
                bool found=false;for(int v=0;v<s0.Vertices;v++)if(Math.Abs(p[v*3]-1)<1e-6&&Math.Abs(p[v*3+1]-(1-NativeBlockLooks.Chamfer))<1e-6&&Math.Abs(p[v*3+2]-(1-NativeBlockLooks.Chamfer))<1e-6)found=true;Check(found,"5 cm chamfer");
                Check(NativeBlockLooks.Chamfer>=.04f&&NativeBlockLooks.Chamfer<=.06f,"chamfer within 2.2's 4-6 cm");
                var thin=Positions(s0,.015f,1,1,0,out _);double minFace=1;for(int v=0;v<s0.Vertices;v++)if(Math.Abs(thin[v*3]-.015)<1e-7)minFace=Math.Min(minFace,thin[v*3+1]);Check(minFace>=-1-1e-6,"a 3 cm plate clamps its chamfer instead of folding");
                var s2=NativeBlockLooks.Build(2);var q=Positions(s2,1,2,1,0,out _);bool waist=false;for(int v=0;v<s2.Vertices;v++)if(Math.Abs(Math.Abs(q[v*3+1])-2/3.0)<1e-5&&Math.Abs(Math.Abs(q[v*3])-(1-NativeBlockLooks.Waist*NativeBlockLooks.Chamfer))<1e-5)waist=true;Check(waist&&Math.Abs(NativeBlockLooks.Waist*NativeBlockLooks.Chamfer-.08f)<1e-6,"S2 waists its faces 8 cm in over the middle third");
                var s3=NativeBlockLooks.Build(3);var r=Positions(s3,1,1,1,0,out _);var cap=Enumerable.Range(0,s3.Vertices).Where(v=>s3.Lattice[v*4+1]==1&&s3.Inset[v*4+1]>-.5f-NativeBlockLooks.Inner/2).Select(v=>r[v*3+1]).ToArray();
                Check(cap.Length>0&&cap.Max()<=1+1e-6&&cap.Max()>=.99&&cap.Min()<1-NativeBlockLooks.Chamfer-.08&&cap.Distinct().Count()>4,"S3's top is ragged below the cut: "+cap.Min()+".."+cap.Max());
                Check(NativeBlockLooks.StubTop.Length==16&&NativeBlockLooks.StubTop.All(x=>x<=0&&x>-.12f*1.01f),"stub jitter 0 to 11 cm down");
            }
            yield return "block looks: 5 meshes (S0-S3, S2 unwaisted) are closed and face out at 8 sizes x 4 turns (the waisted S2 at 108 tall sizes), never pass the collider, keep the 5 cm chamfer (clamped on thin plates), bake +18% bevels and -22% corners, chip 3 then 6 corners, waist S2 and rag S3's top";
            // Stages, seeds and turns.
            Check(NativeBlockLooks.Stage(1)==0&&NativeBlockLooks.Stage(.67f)==0&&NativeBlockLooks.Stage(.66f)==1&&NativeBlockLooks.Stage(.34f)==1&&NativeBlockLooks.Stage(.33f)==2&&NativeBlockLooks.Stage(.01f)==2&&NativeBlockLooks.Stage(0)==3,"stages at 3.9's 66/33/0%");
            Check(NativeBlockLooks.Fraction(40,120,false)==1/3f&&NativeBlockLooks.Fraction(-5,120,false)==0&&NativeBlockLooks.Fraction(10,0,true)==1&&NativeBlockLooks.Fraction(10,0,false)==1&&NativeBlockLooks.Fraction(200,120,false)==1,"health fraction, clamped; indestructible and zero durability stay whole");
            Check(NativeBlockLooks.Seed("hotel-pier-nw")==NativeBlockLooks.Seed("hotel-pier-nw")&&NativeBlockLooks.Seed("hotel-pier-nw")!=NativeBlockLooks.Seed("hotel-pier-ne"),"seeds are per id and repeatable");
            var seeds=content.maps.SelectMany(m=>m.blocks).Select(b=>NativeBlockLooks.Seed(b.id)).ToArray();Check(seeds.All(s=>s>=0&&s<1)&&seeds.Average()>.4&&seeds.Average()<.6,"seeds spread over [0,1)");
            var spins=content.maps.SelectMany(m=>m.blocks).Select(b=>NativeBlockLooks.Spin(b.id,1)).GroupBy(x=>x).Count();Check(spins==4&&content.maps.SelectMany(m=>m.blocks).All(b=>(NativeBlockLooks.Spin(b.id,3)&NativeBlockLooks.SpinZ)==0),"all four turns occur; a stub never turns over");
            yield return "block looks: damage stages S0-S3 at 66/33/0% of the material's durability; per-id seeds and turns (the same on every client)";
            // Look selection.
            var set=MapLookChecks.Load(root);var bf=NativeMapLooks.Find(set,"brickfall");var bl=bf.blockLooks;
            NativeBlockLooks.Choice R(string id,string material="brick",int color=0x123456)=>NativeBlockLooks.Resolve(bl,null,id,material,color);
            void Is(string id,string key,bool band,string material="brick"){var c=R(id,material);Check(c.Look==NativeBlockLooks.Id(key)&&((c.Flags&NativeBlockLooks.Band)!=0)==band&&((c.Flags&NativeBlockLooks.Tag)!=0)==band&&c.Listed,id+" -> "+key+(band?" banded":"")+" (got "+NativeBlockLooks.Keys[c.Look]+")");}
            foreach(string pier in new[]{"hotel-pier-nw","hotel-pier-ne","hotel-pier-sw","hotel-pier-se"})Is(pier,"brick-buff",true);
            foreach(string pier in new[]{"ashby-pier-F1","ashby-pier-R2","bray-pier-F3"})Is(pier,"brick-red",true);
            Is("stack-n-base","brick-red",true);Is("stack-n-drum-2","brick-red",false);Is("bridge-ashby-bearing","steel-green",true);Is("bridge-ashby-deck","steel-green",false);
            Is("hotel-n0","brick-buff",false);Is("hotel-seam-w","brick-buff",false);Is("hotel-core-w","concrete-board",false);Is("hotel-roof","tar",false);Is("hotel-roof-stair","brick-buff",false);Is("hotel-n-sill-3","terracotta",false);Is("hotel-ne-cornice-1","terracotta",false);
            Is("frontage-n","brick-red",false);Is("frontage-n-coping","terracotta",false);Is("ashby-lane0","brick-red",false);Is("ashby-core","concrete-board",false);Is("kessels","terracotta",false);Is("market-wall-w0","brick-red",false);Is("market-wall-w-arch-2","terracotta",false);
            Is("tram-shelter-n","steel-green",false);Is("monument-ne","terracotta",false);Is("barricade-w","timber",false);Is("pallet-sw0","timber",false);Is("stall-w3","timber",false);Is("sandbag-0","plain",false);Is("dock-sw","concrete-board",false);Is("truck-sw","corrugated",false);
            Is("hotel-n-window-4","window-lit",false);Is("mercer-sign","neon",false);Is("kessels-neon-1","neon",false);Is("brickfall-slab","asphalt",false);Is("lane-n","cobble",false);Is("barrier-3","demolition",false);
            Check(R("sandbag-0").Color==0x9C8B6E&&R("dock-sw").Color==0x5F5A53&&R("kessels-neon-1").Color==0xFF5A4E&&R("mercer-sign").Color==0x4FA8FF&&R("hotel-pier-nw").Color==0xB8774E,"entry colours, else the look's palette colour");
            var none=R("chunk-12","brick",0x9C4A30);Check(!none.Listed&&none.Look==NativeBlockLooks.BrickRed&&none.Color==0x9C4A30,"an unlisted block takes its material's look and keeps its own colour");
            Check(NativeBlockLooks.MaterialDefault("stone")==NativeBlockLooks.ConcreteBoard&&NativeBlockLooks.MaterialDefault("wood")==NativeBlockLooks.Timber&&NativeBlockLooks.MaterialDefault("metal")==NativeBlockLooks.SteelGreen&&NativeBlockLooks.MaterialDefault("foundation")==NativeBlockLooks.Asphalt&&NativeBlockLooks.MaterialDefault("glass")==NativeBlockLooks.WindowLit&&NativeBlockLooks.MaterialDefault("unobtainium")==NativeBlockLooks.Plain,"material defaults");
            var rules=new[]{new MapLookBlock{block="material:stone",look="tar"},new MapLookBlock{block="tower-wall-*",look="cobble"},new MapLookBlock{block="tower-*",look="timber"},new MapLookBlock{block="tower-wall-3",look="neon"},new MapLookBlock{block="*-wall-*",look="asphalt"}};
            Check(NativeBlockLooks.Resolve(rules,null,"tower-wall-3","stone",0).Look==NativeBlockLooks.Neon&&NativeBlockLooks.Resolve(rules,null,"tower-wall-9","stone",0).Look==NativeBlockLooks.Cobble&&NativeBlockLooks.Resolve(rules,null,"tower-roof","stone",0).Look==NativeBlockLooks.Timber&&NativeBlockLooks.Resolve(rules,null,"keep","stone",0).Look==NativeBlockLooks.Tar&&!NativeBlockLooks.Resolve(rules,null,"keep","wood",0).Listed,"exact id, then the first matching glob, then material:, then the material default");
            Check(NativeBlockLooks.Resolve(rules,null,"x-wall-y","stone",0).Look==NativeBlockLooks.Asphalt&&NativeBlockLooks.Resolve(new[]{rules[4],new MapLookBlock{block="x-*",look="neon"}},null,"x-wall-y","stone",0).Look==NativeBlockLooks.Asphalt&&NativeBlockLooks.Resolve(new[]{new MapLookBlock{block="x-*",look="neon"},rules[4]},null,"x-wall-y","stone",0).Look==NativeBlockLooks.Neon,"globs win in list order");
            Check(NativeBlockLooks.Resolve(rules,new[]{new MapLookBlock{block="keep",look="cobble"}},"keep","wood",0).Look==NativeBlockLooks.Cobble&&NativeBlockLooks.Resolve(rules,new[]{new MapLookBlock{block="tower-*",look="neon"}},"tower-roof","stone",0).Look==NativeBlockLooks.Timber,"the preview list only fills in where the map's own list has nothing");
            Check(NativeBlockLooks.Glob("a*b*c","axxbyyc")&&NativeBlockLooks.Glob("*","")&&NativeBlockLooks.Glob("hotel-*","hotel-")&&!NativeBlockLooks.Glob("hotel-*","hote")&&!NativeBlockLooks.Glob("a*c","ab")&&NativeBlockLooks.Glob("*-pier-*","hotel-pier-nw")&&!NativeBlockLooks.Glob("stack-*-base*","stack-n"),"glob matching");
            Check(NativeBlockLooks.Score("material:brick","x","brick")==0&&NativeBlockLooks.Score("material:brick","x","bricks")==-1&&NativeBlockLooks.Score("material:brick","x","bric")==-1&&NativeBlockLooks.Score("x","x","")==int.MaxValue&&NativeBlockLooks.Score("x*","xy","")==1&&NativeBlockLooks.Score("x*","y","")==-1,"match scores");
            var used=new HashSet<string>(bl.Select(b=>b.look));foreach(var key in new[]{"brick-red","brick-buff","terracotta","concrete-board","steel-green","timber","tar","asphalt","cobble","window-lit","neon"})Check(used.Contains(key),"Brickfall uses the 3.10 look "+key);
            Check(bl.All(b=>!string.IsNullOrEmpty(b.note))&&bl.Where(b=>b.band).All(b=>b.look=="brick-red"||b.look=="brick-buff"||b.look=="steel-green"),"every Brickfall entry is documented; banded members are brick or steel");
            Check(bf.blocks!=null&&bf.blocks.dressed&&bf.blocks.haze==.15f&&bf.blocks.hazeTop==2.5f&&bf.blocks.hazeColor==0xCF9A74&&bf.blocks.dust>=0&&bf.blocks.dust<.1f,"Brickfall opts in, with 3.10's 15% ground haze to 2.5 m in the fog colour");
            yield return "block looks: Brickfall's blockLooks give the 2.3/3.10 looks by id stem (banded hotel, tenement, chimney and bridge members), exact > first glob > material: > material default, and use all 11 3.10 looks";
            // The draft layout's element names (docs/arenas/scripts/brickfall_m1.py) resolve to listed looks.
            string draft=Path.Combine(root,"../../docs/arenas/scripts/brickfall_m1.py");bool hasDraft=File.Exists(draft);
            if(hasDraft)
            {
                string py=File.ReadAllText(draft);var ids=new List<string>();
                foreach(Match m in Regex.Matches(py,@"\badd\('([a-z][a-z0-9-]*)'"))ids.Add(m.Groups[1].Value);foreach(Match m in Regex.Matches(py,@"wall_[xz]\('([a-z][a-z0-9-]*)'"))ids.Add(m.Groups[1].Value+"0");
                foreach(Match m in Regex.Matches(py,@"f'([a-z-]+)\{i\}'"))ids.Add(m.Groups[1].Value+"0");foreach(Match m in Regex.Matches(py,@"add\('ashby-pier-' \+ nm"))ids.Add("ashby-pier-F1");
                if(py.Contains("mname='bray-pier-' + nm"))ids.Add("bray-pier-F1");if(py.Contains("f'hotel-pier-{"))ids.Add("hotel-pier-nw");
                Check(ids.Count>=30,"draft script elements found: "+ids.Count);
                foreach(string id in ids.Distinct()){var c=R(id);Check(c.Listed,"draft element "+id+" has a Brickfall look");if(id.Contains("-pier-"))Check((c.Flags&NativeBlockLooks.Band)!=0,"draft pier "+id+" is banded");}
            }
            yield return "block looks: "+(hasDraft?"every element named by docs/arenas/scripts/brickfall_m1.py resolves to a listed look and every pier is banded":"draft script not present (source package); names checked from 5.2 only");
            // Opt-in: no old map is dressed without the dev flag; the preview dresses them with today's colours under the new surfaces.
            int previewed=0,listedPreview=0,draws=0;
            foreach(var map in content.maps)
            {
                var entry=NativeMapLooks.Find(set,map.id);Check(!NativeBlockLooks.Dressed(entry,false)&&NativeBlockLooks.Dressed(entry,true),map.id+" keeps today's renderers unless the dev preview is on");
                int[] n=new int[NativeBlockLooks.Meshes];
                foreach(var b in map.blocks)
                {
                    var mat=content.Material(b.material);int colour=b.material=="foundation"?map.floor:mat.color;var c=NativeBlockLooks.Resolve(entry.blockLooks,set.preview,b.id,b.material,colour);
                    Check(c.Look>=0&&c.Look<NativeBlockLooks.Keys.Length,"preview look for "+b.id);if(c.Listed)listedPreview++;else Check(c.Color==colour,"an unlisted old block keeps its colour: "+b.id);previewed++;
                    n[NativeBlockLooks.MeshFor(NativeBlockLooks.Stage(NativeBlockLooks.Fraction(mat.durability,mat.durability,mat.indestructible)),b.size.y/2)]++;
                }
                draws=Math.Max(draws,NativeBlockLooks.Draws(n));Check(n[0]==map.blocks.Length,map.id+" starts whole (S0)");
            }
            Check(NativeBlockLooks.Dressed(bf,false)&&set.preview!=null&&set.preview.Length>0&&listedPreview>previewed/3,"Brickfall is dressed by its look; the preview lists cover the old towers ("+listedPreview+" of "+previewed+")");
            Check(draws==1&&NativeBlockLooks.Draws(360,0,0,0,0)==1&&NativeBlockLooks.Draws(501,0,0,0,0)==2&&NativeBlockLooks.Draws(200,80,40,40,40)==5&&NativeBlockLooks.Draws(0,0,0,0,0)==0,"draws: one per 500 blocks of a mesh (every old map: 1; the 360-prop cap spread over all five meshes: 5)");
            Check(NativeBlockLooks.Draws(360,360,360,360,360)<=NativeBlockLooks.DrawBudget&&NativeBlockLooks.DrawBudget==60,"the 360-prop cap fits 2.4's 60-draw arena budget in any stage mix");
            yield return "block looks: the 15 old maps stay on today's renderers unless --ca-dress-blocks is given ("+previewed+" blocks previewed, "+listedPreview+" by the preview list); each old map is 1 instanced draw, the prop cap at most 5 of 60";
            // Dust coat, flash fall-off.
            var coat=new NativeDustCoat();coat.Add(1,1);Check(Math.Abs(coat.Sample(1,1)-NativeDustCoat.PerBreak)<.03f,"a break dusts about PerBreak at its point: "+coat.Sample(1,1));Check(coat.Sample(1+NativeDustCoat.Radius+NativeDustCoat.Cell,1)==0,"nothing beyond the radius");
            for(int i=0;i<40;i++)coat.Add(1,1);Check(coat.Sample(1,1)<=NativeDustCoat.Cap+1e-6f&&coat.Sample(1,1)>NativeDustCoat.Cap-.05f,"the coat caps");Check(coat.Sample(200,0)==0&&coat.Sample(float.NaN,0)==0,"0 off the grid");coat.Clear();Check(coat.Sample(1,1)==0,"a new round clears it");
            Check(NativeBlockLooks.FlashFalloff(0,8)==1&&NativeBlockLooks.FlashFalloff(8,8)==0&&NativeBlockLooks.FlashFalloff(9,8)==0&&NativeBlockLooks.FlashFalloff(1,0)==0&&NativeBlockLooks.FlashFalloff(7.99,8)<.002,"flash fall-off 1 at the flash, 0 at its range, continuous at the cut");
            double prev=2;for(int i=0;i<=100;i++){double f=NativeBlockLooks.FlashFalloff(i*.08,8);Check(f<=prev,"flash fall-off decreases");prev=f;}Check(Math.Abs(NativeBlockLooks.FlashFalloff(4,8)-1/(1+25*.25))<1e-12,"Unity's 1/(1+25q^2) at half range");
            yield return "block looks: the dust coat (a break dusts 6 m around it, capped, cleared each round) and the blast-flash fall-off";
            // Validation.
            void Reject(Action<MapLookSet> mutate,string what){var bad=MapLookChecks.Load(root);mutate(bad);bool threw=false;try{NativeMapLooks.Validate(bad);}catch(InvalidOperationException){threw=true;}Check(threw,"rejects "+what);}
            Reject(s=>NativeMapLooks.Find(s,"brickfall").blockLooks[0].look="marble","an unknown look key");Reject(s=>NativeMapLooks.Find(s,"brickfall").blockLooks[0].color=0x1000000,"a block colour beyond 0xFFFFFF");
            Reject(s=>NativeMapLooks.Find(s,"brickfall").blockLooks[0].block="material:","an empty material entry");Reject(s=>NativeMapLooks.Find(s,"brickfall").blocks.haze=1.5f,"haze above 1");
            Reject(s=>NativeMapLooks.Find(s,"brickfall").blocks.hazeTop=0,"haze with no top");Reject(s=>NativeMapLooks.Find(s,"brickfall").blocks.dust=float.NaN,"a NaN dust coat");Reject(s=>s.preview[0].look="","a preview entry without a look");
            NativeMapLooks.Validate(set);
            yield return "block looks: validation rejects unknown looks, bad colours, empty patterns, bad haze and dust, bad preview entries ("+count+" checks in all)";
        }
        // The shader's vertex stage on the CPU: lattice positions for half extents h and a turn; normals turned the same way.
        private static double[] Positions(NativeBlockLooks.StageMesh m,float hx,float hy,float hz,int spin,out double[] normals)
        {
            var p=new double[m.Vertices*3];normals=new double[m.Vertices*3];float[] h={hx,hy,hz};
            for(int v=0;v<m.Vertices;v++)
            {
                for(int k=0;k<3;k++){p[v*3+k]=NativeBlockLooks.Corner(m.Lattice[v*4+k],m.Inset[v*4+k],h[k],NativeBlockLooks.Chamfer);normals[v*3+k]=m.Normal[v*3+k];}
                if((spin&NativeBlockLooks.SpinY)!=0){p[v*3]=-p[v*3];p[v*3+2]=-p[v*3+2];normals[v*3]=-normals[v*3];normals[v*3+2]=-normals[v*3+2];}
                if((spin&NativeBlockLooks.SpinZ)!=0){p[v*3]=-p[v*3];p[v*3+1]=-p[v*3+1];normals[v*3]=-normals[v*3];normals[v*3+1]=-normals[v*3+1];}
            }
            return p;
        }
        // Every directed edge between distinct positions has its reverse exactly once (coincident vertices merged by position;
        // edges of zero length, from clamped bevels, are skipped).
        private static bool Closed(NativeBlockLooks.StageMesh m,double[] p)
        {
            string Key(int v)=>Math.Round(p[v*3],5).ToString(CultureInfo.InvariantCulture)+","+Math.Round(p[v*3+1],5).ToString(CultureInfo.InvariantCulture)+","+Math.Round(p[v*3+2],5).ToString(CultureInfo.InvariantCulture);
            var edges=new Dictionary<(string,string),int>();
            for(int t=0;t<m.Triangles.Length;t+=3)for(int k=0;k<3;k++){string a=Key(m.Triangles[t+k]),b=Key(m.Triangles[t+(k+1)%3]);if(a==b)continue;edges[(a,b)]=edges.TryGetValue((a,b),out int n)?n+1:1;}
            foreach(var e in edges){int forward=e.Value;edges.TryGetValue((e.Key.Item2,e.Key.Item1),out int back);if(forward!=back)return false;}
            return true;
        }
    }
}
#endif
