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
    // stub), stages and the stage scale, the band and tag fitted to every banded member and spared by the dust coat, the band
    // lamp and the telegraph's window light (per instance from the CPU clock), look selection (Brickfall's sim entries, kit
    // parts kept out of them, the [static] marks, the draft script's elements and piers as Tests/look-references.json froze
    // them, the dev preview, material defaults), the opt-in (no old map changes by default), the draw budget, the round's one
    // dust coat (breaks, stacked breaks of one tick and landings on the match clock, killcam rewinds, order independence,
    // folding, no allocations), the flash fall-off and validation.
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
            foreach(var (n,v) in new[]{("BAND",NativeBlockLooks.Band),("TAG",NativeBlockLooks.Tag),("FRAGMENT",NativeBlockLooks.Fragment),("LAMP_AMBER",NativeBlockLooks.LampAmber),("LAMP_RED",NativeBlockLooks.LampRed),("SPIN_Y",NativeBlockLooks.SpinY),("SPIN_Z",NativeBlockLooks.SpinZ)})
                Check(shader.Contains("#define FLAG_"+n+" "+v+"u"),"FLAG_"+n+" = "+v);
            int allFlags=NativeBlockLooks.SpinZ*2-1;Check(allFlags*16+NativeBlockLooks.Keys.Length<1<<24,"look + 16 x flags stays exact in a float");
            // 3.10 palette per look key.
            var palette=new Dictionary<string,int>{["brick-red"]=0x9C4A30,["brick-buff"]=0xB8774E,["terracotta"]=0xE3C9A0,["concrete-board"]=0x8F8A80,["steel-green"]=0x3E6B5A,["timber"]=0xB4834F,["tar"]=0x2B2A2E,["asphalt"]=0x3A3836,["cobble"]=0x6B625A,["window-lit"]=0x6E8F96,["neon"]=0x4FA8FF,["demolition"]=0xF4B321};
            foreach(var p in palette)Check(NativeBlockLooks.Palette[NativeBlockLooks.Id(p.Key)]==p.Value,p.Key+" palette");Check(NativeBlockLooks.Palette[NativeBlockLooks.Plain]==-1,"plain keeps the block's colour");
            // Shader colours: the 3.10 / 2.3 hex values in linear light.
            var colours=new Dictionary<string,int>{["MORTAR"]=0xCDBB9E,["FRAME"]=0x1F2A30,["LIT"]=0xFFB45E,["RUST"]=0x7A3E22,["YELLOW"]=0xF4B321,["BLACK"]=0x1C1A18,["DANGER"]=0xD6332A,["INK"]=0xF2EDE4,["DUST"]=0xB8A68F,["SOOT"]=0x4A403A,["COAT"]=0xB3A48E,["FRESH_CONCRETE"]=0xC9C2B4,["FRESH_BRICK"]=0xD2BFA3,["LAMP_AMBER"]=NativeBlockLooks.LampAmberColor};
            foreach(var c in colours)
            {
                var m=Regex.Match(shader,@"#define BK_"+c.Key+@" float3\(([-0-9.]+),([-0-9.]+),([-0-9.]+)\)");Check(m.Success,"BK_"+c.Key+" defined");
                for(int k=0;k<3;k++)Check(Math.Abs(double.Parse(m.Groups[k+1].Value,CultureInfo.InvariantCulture)-Lin((c.Value>>(16-8*k))&255))<6e-5,"BK_"+c.Key+" channel "+k+" is #"+c.Value.ToString("X6")+" in linear light");
            }
            for(int k=0;k<3;k++){double ratio=Lin((0x7E3A26>>(16-8*k))&255)/Lin((0x9C4A30>>(16-8*k))&255);Check(Math.Abs(ratio-.63)<.03,"dark course #7E3A26 = brick red x .63 in linear light, channel "+k+": "+ratio);}
            Check(shader.Contains("#define BK_DARK_COURSE 0.63"),"dark course factor");
            Check(NativeBlockLooks.LampAmberColor==0xFFB000&&NativeBlockLooks.LampRedColor==0xD6332A&&shader.Contains("?BK_DANGER:"),"the band lamp is amber #FFB000 or the 2.3 danger red");
            yield return "block looks: 14 look ids, 7 flags, the 3.10 palette and 14 shader colours (2.3 chevron yellow/black, danger red, the lamp's amber) match the Core and the proposal";
            // Formulas the Core mirrors, SM 3.5 rules, the light block's own arrays.
            foreach(var expr in new[]{"float3 p=a*h+sign(b)*min(abs(b)*c,0.42*abs(a)*h);","#define BK_GRIME 0.22","1.0-smoothstep(1.2,1.8,yb)","inBand=smoothstep(bt.x-fw.y,bt.x+fw.y,yb)*(1.0-smoothstep(bt.y-fw.y,bt.y+fw.y,yb));",
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
            yield return "block looks: the shader's lattice, grime ramp (22% to 1.2 m, gone by 1.8 m), fitted band, fall-offs and haze are the Core's; forward base + shadow caster, SM 3.5, no vertex derivatives, own 8+3 light arrays; the tag spells DANGER";
            // The band and tag: BkBand is NativeBlockLooks.BandFit, each is drawn only with its own flag (the demolition look
            // implies no tag), never on a stub, and the dust coat spares them.
            string G(float v)=>((double)v).ToString("0.0###",CultureInfo.InvariantCulture);
            foreach(var expr in new[]{"float c=min("+G(NativeBlockLooks.Chamfer)+","+G(NativeBlockLooks.ClampShare*.5f)+"*height);",
                "float2 band=height>="+G(NativeBlockLooks.BandFullMin)+"?float2("+G(NativeBlockLooks.BandLow)+","+G(NativeBlockLooks.BandHigh)+"):height*0.5+float2(-0.5,0.5)*min("+G(NativeBlockLooks.BandMax)+","+G(NativeBlockLooks.BandShare)+"*(height-2.0*c));",
                "float above=band.y+"+G(NativeBlockLooks.TagGap+NativeBlockLooks.TagHalf)+";float below=band.x-"+G(NativeBlockLooks.TagGap+NativeBlockLooks.TagHalf)+";",
                "above+"+G(NativeBlockLooks.TagHalf)+"<=height-c-"+G(NativeBlockLooks.TagClear)+"?above:(below-"+G(NativeBlockLooks.TagHalf)+">=c+"+G(NativeBlockLooks.TagClear)+"?below:-1.0)",
                "float3 bt=BkBand(2.0*hs.y);","if((fl&(FLAG_BAND|FLAG_TAG))!=0u&&!top&&health>0.0)","if((fl&FLAG_TAG)!=0u&&bt.z>0.0&&halfAcross>"+G(NativeBlockLooks.TagMinHalfWidth)+")",
                "float2 tp=float2(across,yb-bt.z);","coat*=1.0-"+G(1-NativeBlockLooks.BandCoatKeep)+"*max(max(inBand,plate),lamp);","(nO.y>0.7?1.0:0.55)*(0.75+0.5*BkNoise(uv*1.3))"})
                Check(shader.Contains(expr),"shader has "+expr);
            Check(!Regex.IsMatch(code,@"\bdemo\b")&&Regex.Matches(code,@"BkGlyphs\(tp").Count==1,"the tag is drawn in one place, only with FLAG_TAG");
            Check(Math.Abs(NativeBlockLooks.TagHalf*2-.24f)<1e-6&&Math.Abs(NativeBlockLooks.ClampShare*.5f-.21f)<1e-6,"the plate is .24 m tall; the shader's .21 is the chamfer clamp per metre of height");
            {
                float lo,hi,tag;NativeBlockLooks.BandFit(3.6f,out lo,out hi,out tag);Check(lo==1&&hi==1.6f&&Math.Abs(tag-1.8f)<1e-5,"a 3.6 m pier: 2.3's band at 1.0-1.6 m, the tag at 1.8 m");
                NativeBlockLooks.BandFit(1.95f,out lo,out hi,out tag);Check(lo==1&&hi==1.6f&&Math.Abs(tag-.8f)<1e-5,"a 1.95 m block: the band at 1.0-1.6 m, the tag below it (no room above)");
                NativeBlockLooks.BandFit(1.8f,out lo,out hi,out tag);Check(Math.Abs(lo-.6f)<1e-5&&Math.Abs(hi-1.2f)<1e-5&&Math.Abs(tag-1.4f)<1e-5,"a 1.8 m chimney base: a .6 m band centred at .9 m, the tag above it");
                NativeBlockLooks.BandFit(1f,out lo,out hi,out tag);Check(Math.Abs(lo-.2f)<1e-5&&Math.Abs(hi-.8f)<1e-5&&tag<0,"a 1.0 m bearing: a .6 m band centred, no room for the tag");
                int fitted=0,tagged=0;
                for(int i=1;i<=1440;i++)
                {
                    float h=i*.01f,c=Math.Min(NativeBlockLooks.Chamfer,NativeBlockLooks.ClampShare*h*.5f);NativeBlockLooks.BandFit(h,out lo,out hi,out tag);fitted++;
                    Check(lo>=c-1e-5&&hi<=h-c+1e-5&&hi>lo,"band inside the flat face at "+h+" m: "+lo+".."+hi);
                    if(h>=.5f)Check(hi-lo>=.3f-1e-5,"a band at least .3 m tall on a block of "+h+" m");
                    if(h>=NativeBlockLooks.BandFullMin)Check(lo==NativeBlockLooks.BandLow&&hi==NativeBlockLooks.BandHigh,"2.3's 1.0-1.6 m band at "+h+" m");
                    if(tag<0){Check(h<1.4f,"a tag on every block 1.4 m tall or more ("+h+" m)");continue;}tagged++;
                    float t0=tag-NativeBlockLooks.TagHalf,t1=tag+NativeBlockLooks.TagHalf;
                    Check(t0>=c+NativeBlockLooks.TagClear-1e-5&&t1<=h-c-NativeBlockLooks.TagClear+1e-5,"tag plate clear of the bevels at "+h+" m");
                    Check(t0>=hi+NativeBlockLooks.TagGap-1e-5||t1<=lo-NativeBlockLooks.TagGap+1e-5,"tag plate apart from the band at "+h+" m");
                    if(h>=1.99f)Check(Math.Abs(tag-1.8f)<1e-5,"the tag at 1.8 m once it fits above the 1.0-1.6 m band ("+h+" m)");
                }
                Check(fitted==1440&&tagged>1000,"band fit swept over .01-14.4 m ("+tagged+" with a tag)");
            }
            {
                // Every banded Brickfall entry at its member's smallest size: the draft layout's piers (docs/arenas/scripts/
                // brickfall_m1.py: hotel 1.2 x 3.6 x 1.2, tenement 1.0 x 3.6 x 1.0; every draft pier is checked against these
                // from Tests/look-references.json) and the render stand-ins for the members 3.9 names without a size (bridge
                // bearing .8 x 1.0 x .8, chimney base ring 2.6 x 1.8 x 2.6).
                var bfLooks=NativeMapLooks.Find(MapLookChecks.Load(root),"brickfall").blockLooks;
                var members=new[]{("hotel-pier-nw",1.2f,3.6f,true),("ashby-pier-F1",1f,3.6f,true),("bray-pier-R2",1f,3.6f,true),("bridge-ashby-bearing",.8f,1f,false),("stack-n-base",2.6f,1.8f,true)};
                foreach(var (id,w,h,tagged) in members)
                {
                    var c=NativeBlockLooks.Resolve(bfLooks,null,id,"brick",0);Check((c.Flags&NativeBlockLooks.Band)!=0&&(c.Flags&NativeBlockLooks.Tag)!=0,id+" is banded");
                    NativeBlockLooks.BandFit(h,out float lo,out float hi,out float tag);Check(hi-lo>=.5f&&lo>=.05f&&hi<=h-.05f,id+" at "+h+" m shows a band of "+(hi-lo)+" m");
                    Check((tag>0&&w*.5f>NativeBlockLooks.TagMinHalfWidth)==tagged,id+(tagged?" shows":" has no room for")+" the DANGER tag");
                }
                foreach(var e in bfLooks.Where(e=>e.band))Check(members.Any(m=>NativeBlockLooks.Score(e.block,m.Item1,"brick")>0),"banded entry "+e.block+" has a member size checked");
                int piers=0;
                foreach(var e in MapLookChecks.References(root).GetProperty("draft").GetProperty("elements").EnumerateArray())
                {
                    string id=e[0].GetString();if(!id.Contains("-pier-"))continue;piers++;var m=members.Where(k=>k.Item1.StartsWith(id.Substring(0,id.IndexOf("-pier-")+6),StringComparison.Ordinal)).ToArray();
                    double w=e[2].GetDouble()-e[1].GetDouble(),d=e[4].GetDouble()-e[3].GetDouble(),top=e[5].GetDouble();
                    Check(m.Length==1&&Math.Abs(top-3.6)<1e-6&&Math.Min(w,d)>=m[0].Item2-1e-6,"draft pier "+id+" ("+w+" x "+top+" x "+d+") is 3.6 m tall and at least its member's checked width");
                }
                Check(piers==14,"the draft's 14 piers (hotel 4, Ashby 5, Bray 5) checked: "+piers);
            }
            {
                // The coat at its heaviest on a side face (a saturated map and block coat x .55 x the noise's 1.25) against the band.
                double Y(double[] c)=>.2126*c[0]+.7152*c[1]+.0722*c[2];
                double[] V(string name){var m=Regex.Match(shader,@"#define BK_"+name+@" float3\(([-0-9.]+),([-0-9.]+),([-0-9.]+)\)");return Enumerable.Range(1,3).Select(k=>double.Parse(m.Groups[k].Value,CultureInfo.InvariantCulture)).ToArray();}
                double yellow=Y(V("YELLOW")),black=Y(V("BLACK")),dust=Y(V("COAT")),side=Math.Min(1,.55*1.25);
                double Ratio(double t)=>(yellow*(1-t)+dust*t+.05)/(black*(1-t)+dust*t+.05);
                Check(Ratio(side*NativeBlockLooks.BandCoatKeep)>=6&&Ratio(side*NativeBlockLooks.BandCoatKeep)>=.65*Ratio(0)&&Ratio(side)<2,"the band keeps "+Ratio(side*NativeBlockLooks.BandCoatKeep).ToString("0.0")+":1 yellow/black contrast under the heaviest coat (clean "+Ratio(0).ToString("0.0")+":1; the full coat would leave "+Ratio(side).ToString("0.0")+":1)");
            }
            yield return "block looks: the 2.3 band and DANGER tag fit every block height (1.0-1.6 m with the tag at 1.8 m on tall members, a centred band and the tag only where its plate fits on short ones; every banded Brickfall member shows its band), only with their own flags, never on a stub, and keep at least 6:1 contrast under the heaviest dust coat";
            // The band lamp (2.3) and the 3.9 telegraph's window light: per instance from NativeArenaDressing on the CPU clock.
            foreach(var expr in new[]{"float ld=length(float2(across,yb-0.5*(bt.x+bt.y)));","lamp=1.0-smoothstep("+G(NativeBlockLooks.LampHousing)+"-fwm,"+G(NativeBlockLooks.LampHousing)+"+fwm,ld);","float lens=1.0-smoothstep("+G(NativeBlockLooks.LampLens)+"-fwm,"+G(NativeBlockLooks.LampLens)+"+fwm,ld);",
                "float3 lc=(fl&FLAG_LAMP_RED)!=0u?BK_DANGER:((fl&FLAG_LAMP_AMBER)!=0u?BK_LAMP_AMBER:float3(0.0,0.0,0.0));","emit=lerp(emit*(1.0-lamp),lc*"+G(NativeBlockLooks.LampGlow)+",lens);",
                "float lit=step(BkHash2(pane+seed*37.0),0.35)*win;","o.world=float4(mul(unity_ObjectToWorld,v.vertex).xyz,col.w);","i.shade,i.obj.w,i.world.w);"})Check(shader.Contains(expr),"shader has "+expr);
            Check(!Regex.IsMatch(Regex.Replace(Body("BlockSurf BlockSurface("),@"//[^\n]*",""),@"_Time|\btime\b")&&!code.Contains("FLAG_STROBE")&&!code.Contains("FLAG_DARK"),"the window strobe and the lamp blink come from the CPU's presentation clock (host, guests and the killcam agree), not the shader's _Time");
            Check(NativeBlockLooks.LampHousing>NativeBlockLooks.LampLens&&2*NativeBlockLooks.LampHousing<=.3f-1e-6,"the lamp's housing fits the .3 m band of the shortest member");
            Check(NativeBlockLooks.Lamp(0,false,true,true)==NativeBlockLooks.LampAmber&&NativeBlockLooks.Lamp(1,false,true,false)==NativeBlockLooks.LampAmber&&NativeBlockLooks.Lamp(0,false,false,true)==0&&NativeBlockLooks.Lamp(1,false,false,true)==0,"amber while locked, dark once armed");
            Check(NativeBlockLooks.Lamp(2,false,false,true)==NativeBlockLooks.LampRed&&NativeBlockLooks.Lamp(2,false,true,false)==0&&NativeBlockLooks.Lamp(0,true,true,true)==NativeBlockLooks.LampRed&&NativeBlockLooks.Lamp(1,true,false,false)==0,"red on the strobe at S2 or under a stress telegraph (locked or not), dark between blinks");
            Check(NativeBlockLooks.Lamp(3,true,true,true)==0,"a stub (S3) has no lamp");
            Check(NativeBlockLooks.LockSeconds==4&&NativeBlockLooks.Locked(NativePhase.Countdown,0,5)&&NativeBlockLooks.Locked(NativePhase.Fight,10,13.99)&&!NativeBlockLooks.Locked(NativePhase.Fight,10,14)&&!NativeBlockLooks.Locked(NativePhase.Result,10,11)&&!NativeBlockLooks.Locked(NativePhase.Lobby,0,0),"members are locked before the fight and for its first 4 s (3.9 rule 1)");
            {int blinks=0;bool last=false;for(int i=0;i<1000;i++){bool on=NativeBlockLooks.Lamp(2,false,false,NativeStructureFx.LampStrobe(i*.001f)>0)!=0;if(on&&!last)blinks++;last=on;}Check(blinks==4,"the red lamp blinks 4 times a second: "+blinks);}
            yield return "block looks: the band lamp (amber while locked, red on the 4 Hz strobe at S2 or under a stress telegraph, else dark glass; spared by the coat) and the 8 Hz window light come per instance from the CPU clock, not the shader's time";
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
            // The stage scale (5.3: health / content hp, per-block stages), routed through one Core helper.
            Check(NativeBlockLooks.StageMax(140,120)==140&&NativeBlockLooks.StageMax(0,120)==120&&NativeBlockLooks.StageMax(-1,120)==120,"a block's hp wins over its material's durability");
            {
                NativeBlockLooks.Thresholds(new[]{.7f,.4f},out float a1,out float a2);NativeBlockLooks.Thresholds(null,out float d1,out float d2);NativeBlockLooks.Thresholds(new[]{.3f,.6f},out float b1,out float b2);NativeBlockLooks.Thresholds(new[]{1f,.5f},out float e1,out float e2);
                Check(a1==.7f&&a2==.4f&&d1==NativeBlockLooks.Stage1&&d2==NativeBlockLooks.Stage2&&b1==d1&&b2==d2&&e1==d1&&e2==d2,"a block's stages when two descending shares inside 0..1, else 66/33%");
                var brick=new ContentMaterial{id="brick",durability=120};float scale=NativeBlockLooks.StageMax(new ContentBlock{id="hotel-pier-nw",material="brick"},brick,out float s1,out float s2);
                Check(scale==120&&s1==NativeBlockLooks.Stage1&&s2==NativeBlockLooks.Stage2&&NativeBlockLooks.StageMax(new ContentBlock(),null,out _,out _)==0,"today a block's scale is its material's durability at 66/33%");
                scale=NativeBlockLooks.StageMax(new ContentBlock{hp=140,stages=new[]{.7f,.25f}},brick,out s1,out s2);Check(scale==140&&s1==.7f&&s2==.25f,"authored hp and stage thresholds override the material defaults");
                for(int i=0;i<=200;i++)
                {
                    float f=i/200f;Check(Math.Abs(NativeBlockLooks.Normalised(f,d1,d2)-f)<1e-6f,"default thresholds leave health as it is ("+f+")");
                    Check(NativeBlockLooks.Stage(NativeBlockLooks.Normalised(f,a1,a2))==NativeBlockLooks.Stage(f,a1,a2),"the shader's stage at "+f+" with 70/40% thresholds is the mesh's");
                }
                Check(NativeBlockLooks.Stage(.7f,.7f,.4f)==1&&NativeBlockLooks.Stage(.71f,.7f,.4f)==0&&NativeBlockLooks.Stage(.4f,.7f,.4f)==2&&NativeBlockLooks.Stage(0,.7f,.4f)==3,"stages at a block's own thresholds");
                // With 5.2's sample pier (hp 140, stages .66/.33) on brick (120): S1 at 66% of 140, not of 120.
                float pier=NativeBlockLooks.StageMax(140,120);Check(NativeBlockLooks.Stage(NativeBlockLooks.Fraction(93,pier,false))==0&&NativeBlockLooks.Stage(NativeBlockLooks.Fraction(92,pier,false))==1&&NativeBlockLooks.Stage(NativeBlockLooks.Fraction(92,120,false))==0,"a 140 HP pier enters S1 at 92 HP (66%), where durability would wait for 79");
            }
            Check(NativeBlockLooks.Seed("hotel-pier-nw")==NativeBlockLooks.Seed("hotel-pier-nw")&&NativeBlockLooks.Seed("hotel-pier-nw")!=NativeBlockLooks.Seed("hotel-pier-ne"),"seeds are per id and repeatable");
            var seeds=content.maps.SelectMany(m=>m.blocks).Select(b=>NativeBlockLooks.Seed(b.id)).ToArray();Check(seeds.All(s=>s>=0&&s<1)&&seeds.Average()>.4&&seeds.Average()<.6,"seeds spread over [0,1)");
            var spins=content.maps.SelectMany(m=>m.blocks).Select(b=>NativeBlockLooks.Spin(b.id,1)).GroupBy(x=>x).Count();Check(spins==4&&content.maps.SelectMany(m=>m.blocks).All(b=>(NativeBlockLooks.Spin(b.id,3)&NativeBlockLooks.SpinZ)==0),"all four turns occur; a stub never turns over");
            yield return "block looks: damage stages S0-S3 at the block's thresholds (66/33/0% by default) of its stage scale (a block hp over its material's durability once ContentBlock has one; the shader's health remapped to match); per-id seeds and turns (the same on every client)";
            // Look selection.
            var set=MapLookChecks.Load(root);var bf=NativeMapLooks.Find(set,"brickfall");var bl=bf.blockLooks;var kl=bf.kitLooks;
            NativeBlockLooks.Choice R(string id,string material="brick",int color=0x123456)=>NativeBlockLooks.Resolve(bl,null,id,material,color);
            NativeBlockLooks.Choice K(string id)=>NativeBlockLooks.Resolve(kl,null,id,"",0x123456);
            void Kit(string id,string key){var c=K(id);Check(c.Listed&&c.Look==NativeBlockLooks.Id(key)&&c.Flags==0,"kit part "+id+" -> "+key);}
            void Is(string id,string key,bool band,string material="brick"){var c=R(id,material);Check(c.Look==NativeBlockLooks.Id(key)&&((c.Flags&NativeBlockLooks.Band)!=0)==band&&((c.Flags&NativeBlockLooks.Tag)!=0)==band&&c.Listed,id+" -> "+key+(band?" banded":"")+" (got "+NativeBlockLooks.Keys[c.Look]+")");}
            foreach(string pier in new[]{"hotel-pier-nw","hotel-pier-ne","hotel-pier-sw","hotel-pier-se"})Is(pier,"brick-buff",true);
            foreach(string pier in new[]{"ashby-pier-F1","ashby-pier-R2","bray-pier-F3"})Is(pier,"brick-red",true);
            Is("stack-n-base","brick-red",true);Is("stack-n-drum-2","brick-red",false);Is("bridge-ashby-bearing","steel-green",true);Is("bridge-ashby-deck","steel-green",false);
            Is("hotel-n0","brick-buff",false);Is("hotel-seam-w","brick-buff",false);Is("hotel-core-w","concrete-board",false);Is("hotel-roof","tar",false);Is("hotel-roof-stair","brick-buff",false);Is("hotel-floor-1","concrete-board",false);
            Is("frontage-n","brick-red",false);Is("ashby-lane0","brick-red",false);Is("ashby-core","concrete-board",false);Is("ashby-floor-1","concrete-board",false);Is("kessels","terracotta",false);Is("market-wall-w0","brick-red",false);
            Is("tram-shelter-n","steel-green",false);Is("monument-ne","terracotta",false);Is("barricade-w","timber",false);Is("pallet-sw0","timber",false);Is("stall-w3","timber",false);Is("sandbag-0","plain",false);Is("dock-sw","concrete-board",false);Is("truck-sw","corrugated",false);
            Is("brickfall-slab","asphalt",false);Is("hotel-nw-rubble-1","brick-buff",false);Is("kiosk-w","steel-green",false);Is("news-kiosk-n","steel-green",false);
            Check(R("sandbag-0").Color==0x9C8B6E&&R("dock-sw").Color==0x5F5A53&&R("hotel-pier-nw").Color==0xB8774E,"entry colours, else the look's palette colour");
            // Decor is kit parts (2.1 rule 5, 2.2 "Set dressing", 3.10 "windows are open holes"), never sim block looks.
            Kit("hotel-n-sill-3","terracotta");Kit("hotel-ne-cornice-1","terracotta");Kit("frontage-n-coping","terracotta");Kit("market-wall-w-arch-2","terracotta");Kit("hotel-n-string-1","terracotta");Kit("hotel-ne-quoin-2","terracotta");
            Kit("hotel-n-window-4","window-lit");Kit("mercer-sign","neon");Kit("kessels-neon-1","neon");Kit("kessels-shutter-1","corrugated");Kit("ashby-lane-boards-2","timber");Kit("market-canopy-3","steel-green");Kit("lane-n","cobble");Kit("yard-sw","cobble");Kit("street-n","asphalt");Kit("square-ne","cobble");
            Check(K("kessels-neon-1").Color==0xFF5A4E&&K("mercer-sign").Color==0x4FA8FF,"kit colours: coral shop neon, MERCER blue");
            var authored=System.Text.Json.JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true}).maps.Single(m=>m.id=="brickfall");
            foreach(var e in bl)Check(!KitStems.Any(k=>e.block.Contains(k))||authored.blocks.Any(b=>NativeBlockLooks.Glob(e.block,b.id)),"sim entry "+e.block+" names an authored block, not only a kit part");
            Check(kl.Length>0&&kl.All(e=>!e.band&&e.note.StartsWith("kit: ",StringComparison.Ordinal)&&KitStems.Any(k=>e.block.Contains(k))),"every kit entry is a kit part (no band, noted kit:)");
            Check(!bl.Any(e=>e.look=="demolition")&&!bl.Concat(kl).Any(e=>e.block.StartsWith("barrier",StringComparison.Ordinal)),"no Brickfall block takes the all-over demolition stripes (2.3 has no such element)");
            // 3.9 / 5.3 static blocks: their entries wait for the static block path, and say so.
            foreach(var e in bl){bool stat=StaticStems.Contains(e.block);Check(e.note.EndsWith(" [static]",StringComparison.Ordinal)==stat,e.block+(stat?" is marked [static]":" is not static"));}
            Check(StaticStems.All(x=>bl.Any(e=>e.block==x)),"every static stem has an entry");
            var none=R("chunk-12","brick",0x9C4A30);Check(!none.Listed&&none.Look==NativeBlockLooks.BrickRed&&none.Color==0x9C4A30,"an unlisted block takes its material's look and keeps its own colour");
            Check(NativeBlockLooks.MaterialDefault("stone")==NativeBlockLooks.ConcreteBoard&&NativeBlockLooks.MaterialDefault("wood")==NativeBlockLooks.Timber&&NativeBlockLooks.MaterialDefault("metal")==NativeBlockLooks.SteelGreen&&NativeBlockLooks.MaterialDefault("foundation")==NativeBlockLooks.Asphalt&&NativeBlockLooks.MaterialDefault("glass")==NativeBlockLooks.WindowLit&&NativeBlockLooks.MaterialDefault("unobtainium")==NativeBlockLooks.Plain,"material defaults");
            var rules=new[]{new MapLookBlock{block="material:stone",look="tar"},new MapLookBlock{block="tower-wall-*",look="cobble"},new MapLookBlock{block="tower-*",look="timber"},new MapLookBlock{block="tower-wall-3",look="neon"},new MapLookBlock{block="*-wall-*",look="asphalt"}};
            Check(NativeBlockLooks.Resolve(rules,null,"tower-wall-3","stone",0).Look==NativeBlockLooks.Neon&&NativeBlockLooks.Resolve(rules,null,"tower-wall-9","stone",0).Look==NativeBlockLooks.Cobble&&NativeBlockLooks.Resolve(rules,null,"tower-roof","stone",0).Look==NativeBlockLooks.Timber&&NativeBlockLooks.Resolve(rules,null,"keep","stone",0).Look==NativeBlockLooks.Tar&&!NativeBlockLooks.Resolve(rules,null,"keep","wood",0).Listed,"exact id, then the first matching glob, then material:, then the material default");
            Check(NativeBlockLooks.Resolve(rules,null,"x-wall-y","stone",0).Look==NativeBlockLooks.Asphalt&&NativeBlockLooks.Resolve(new[]{rules[4],new MapLookBlock{block="x-*",look="neon"}},null,"x-wall-y","stone",0).Look==NativeBlockLooks.Asphalt&&NativeBlockLooks.Resolve(new[]{new MapLookBlock{block="x-*",look="neon"},rules[4]},null,"x-wall-y","stone",0).Look==NativeBlockLooks.Neon,"globs win in list order");
            Check(NativeBlockLooks.Resolve(rules,new[]{new MapLookBlock{block="keep",look="cobble"}},"keep","wood",0).Look==NativeBlockLooks.Cobble&&NativeBlockLooks.Resolve(rules,new[]{new MapLookBlock{block="tower-*",look="neon"}},"tower-roof","stone",0).Look==NativeBlockLooks.Timber,"the preview list only fills in where the map's own list has nothing");
            Check(NativeBlockLooks.Glob("a*b*c","axxbyyc")&&NativeBlockLooks.Glob("*","")&&NativeBlockLooks.Glob("hotel-*","hotel-")&&!NativeBlockLooks.Glob("hotel-*","hote")&&!NativeBlockLooks.Glob("a*c","ab")&&NativeBlockLooks.Glob("*-pier-*","hotel-pier-nw")&&!NativeBlockLooks.Glob("stack-*-base*","stack-n"),"glob matching");
            Check(NativeBlockLooks.Score("material:brick","x","brick")==0&&NativeBlockLooks.Score("material:brick","x","bricks")==-1&&NativeBlockLooks.Score("material:brick","x","bric")==-1&&NativeBlockLooks.Score("x","x","")==int.MaxValue&&NativeBlockLooks.Score("x*","xy","")==1&&NativeBlockLooks.Score("x*","y","")==-1,"match scores");
            var used=new HashSet<string>(bl.Concat(kl).Select(b=>b.look));foreach(var key in new[]{"brick-red","brick-buff","terracotta","concrete-board","steel-green","timber","tar","asphalt","cobble","window-lit","neon"})Check(used.Contains(key),"Brickfall uses the 3.10 look "+key);
            Check(bl.All(b=>!string.IsNullOrEmpty(b.note))&&bl.Where(b=>b.band).All(b=>b.look=="brick-red"||b.look=="brick-buff"||b.look=="steel-green"),"every Brickfall entry is documented; banded members are brick or steel");
            Check(bf.blocks!=null&&bf.blocks.dressed&&bf.blocks.haze==.15f&&bf.blocks.hazeTop==2.5f&&bf.blocks.hazeColor==0xCF9A74&&bf.blocks.dust>=0&&bf.blocks.dust<.1f,"Brickfall opts in, with 3.10's 15% ground haze to 2.5 m in the fog colour");
            yield return "block looks: Brickfall's blockLooks give sim blocks the 2.3/3.10 looks by id stem (banded hotel, tenement, chimney and bridge members; [static] marks on 3.9's static blocks), kit parts (windows, trims, signage, paving) live in kitLooks, exact > first glob > material: > material default, and all 11 3.10 looks are used";
            // The draft layout's element names (docs/arenas/scripts/brickfall_m1.py, every element it lists, mirrors included, as
            // tools/export_look_references.py froze them in Tests/look-references.json) resolve to listed looks.
            {
                var draft=MapLookChecks.References(root).GetProperty("draft");var ids=draft.GetProperty("elements").EnumerateArray().Select(e=>e[0].GetString()).ToArray();
                Check(draft.GetProperty("path").GetString()=="docs/arenas/scripts/brickfall_m1.py"&&ids.Length>100&&ids.Distinct().Count()==ids.Length,"draft elements frozen: "+ids.Length);
                foreach(string id in ids){var c=R(id);Check(c.Listed,"draft element "+id+" has a Brickfall look");if(id.Contains("-pier-"))Check((c.Flags&NativeBlockLooks.Band)!=0,"draft pier "+id+" is banded");}
                yield return "block looks: every element the draft script lists ("+ids.Length+", frozen in Tests/look-references.json) resolves to a listed look and every pier is banded";
            }
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
            // The round's one dust coat: breaks and landings on the match clock.
            var coat=new NativeDustCoat();
            Check(coat.Break(10,1,2,1)&&!coat.Break(10,1,2,1)&&!coat.Break(10,1.01f,2.01f,1)&&coat.Breaks==1&&!coat.Break(double.NaN,0,0,0)&&!coat.Break(10,0,float.NaN,0),"a break fed twice counts once");coat.At(9);Check(coat.Sample(1,1)==0&&coat.Local(1,1)==0,"nothing before the break's time");
            coat.At(10);Check(Math.Abs(coat.Sample(1,1)-NativeDustCoat.PerBreak)<.03f&&coat.Local(1,1)==coat.Sample(1,1),"a break dusts about PerBreak at its point: "+coat.Sample(1,1));Check(coat.Sample(1+NativeDustCoat.Radius+NativeDustCoat.Cell,1)==0,"nothing beyond the radius");
            for(int i=0;i<40;i++)coat.Break(11+i,1,2,1);coat.At(60);Check(coat.Sample(1,1)<=NativeDustCoat.Cap+1e-6f&&coat.Sample(1,1)>NativeDustCoat.Cap-.05f,"the coat caps");Check(coat.Sample(200,0)==0&&coat.Sample(float.NaN,0)==0,"0 off the grid");
            coat.At(10.5);Check(Math.Abs(coat.Sample(1,1)-NativeDustCoat.PerBreak)<.03f,"a killcam frame at 10.5 s sees the first break only (the grid rebuilds)");coat.At(60);Check(coat.Sample(1,1)>NativeDustCoat.Cap-.05f&&coat.Wide==0,"the live clock sees them all again; breaks leave the map-wide level alone");
            coat.Clear();Check(coat.Sample(1,1)==0&&coat.Breaks==0&&coat.Lands==0&&coat.Wide==0,"a new round clears it");
            // Stacked members one blast breaks in the same tick (Break events at each prop's own centre: same x, z and time).
            Check(coat.Break(30,5,1.8f,5)&&coat.Break(30,5,5.4f,5)&&!coat.Break(30,5,5.4f,5)&&coat.Breaks==2,"stacked breaks of one tick each count; each fed again does not");
            coat.At(30);Check(Math.Abs(coat.Sample(5,5)-2*NativeDustCoat.PerBreak)<1e-5f,"two stacked breaks stamp twice: "+coat.Sample(5,5));coat.Clear();
            Check(coat.Land(50,0,0,8,1)&&!coat.Land(50,0,0,8,1)&&coat.Lands==1,"a landing fed twice counts once");coat.At(49.9);Check(coat.Wide==0&&coat.Local(0,0)==0,"a killcam frame before the landing has none of it");
            coat.At(52);float w52=coat.Wide,l52=coat.Local(0,0);coat.At(58);float w58=coat.Wide,l58=coat.Local(0,0);coat.At(80);float w80=coat.Wide,l80=coat.Local(0,0);
            Check(w52>0&&w58>w52&&w80>=w58&&Math.Abs(w80-(1-Math.Exp(-NativeDustCoat.LandWide)))<1e-6,"the map-wide level settles in over 8 s to 1 - exp(-.22) for one full-energy landing ("+w80+")");
            Check(l52>0&&l58>l52&&Math.Abs(l80-NativeDustCoat.LandLocal)<1e-5&&coat.Local(12.7f,0)>0&&coat.Local(12.9f,0)==0,"the local coat settles to .3 at the landing, out to 1.6 x its 8 m radius");
            coat.Land(60,30,0,1,0);coat.At(80);Check(coat.Local(35.9f,0)>0&&coat.Local(36.1f,0)==0,"a small landing still dusts 6 m");
            for(int k=0;k<40;k++)coat.Land(70+k,k-20,5,6,.5f);coat.At(400);Check(coat.Lands==NativeDustCoat.LandSlots&&coat.Wide<1&&coat.Wide>.9f,"landings past the slots fold into the base and the coat saturates below 1 ("+coat.Wide+")");
            {
                // Order independence (a guest or the killcam feeding the same entries in another order sees the same coat), folding
                // past the slots, and no allocations once built.
                var a=new NativeDustCoat();var b=new NativeDustCoat();for(int i=0;i<300;i++)a.Break(i*.1,(i*7)%60-30,i%3,(i*11)%50-25);for(int i=299;i>=0;i--)b.Break(i*.1,(i*7)%60-30,i%3,(i*11)%50-25);
                a.Land(12,4,4,6,.7f);b.Land(12,4,4,6,.7f);a.At(20);b.At(20);bool same=a.Wide==b.Wide;for(float x=-40;x<=40;x+=1.3f)for(float z=-40;z<=40;z+=1.7f)same&=Math.Abs(a.Local(x,z)-b.Local(x,z))<1e-5f;Check(same,"the coat does not depend on the order entries arrive in (to float rounding)");
                a.At(5);b.At(29.9);b.At(5);same=true;for(float x=-40;x<=40;x+=1.3f)for(float z=-40;z<=40;z+=1.7f)same&=Math.Abs(a.Local(x,z)-b.Local(x,z))<1e-5f;Check(same,"stepping back to a clock gives the coat built straight to it");
                var big=new NativeDustCoat();for(int i=0;i<NativeDustCoat.BreakSlots+88;i++)big.Break(i*.05,(i%40)-20,0,(i/40)-8);big.At(1000);Check(big.Breaks==NativeDustCoat.BreakSlots&&big.Sample(0,0)>0,"breaks past the slots fold into the base");
                big.At(0);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<300;i++){big.At(i*.1);big.Local(i%30-15,0);}big.At(-1);big.At(30);long after=GC.GetAllocatedBytesForCurrentThread();Check(after==before,"the coat allocates nothing per frame ("+(after-before)+" bytes)");
            }
            Check(NativeDustCoat.Global=="_ArenaDustCoat"&&shader.Contains("float _ArenaDustCoat;")&&shader.Contains("saturate(_ArenaDust+"+G(NativeDustCoat.WideShare)+"*_ArenaDustCoat+dust)"),"the shader adds the map base, .6 x the map-wide level and the block's own coat");
            Check(NativeBlockLooks.FlashFalloff(0,8)==1&&NativeBlockLooks.FlashFalloff(8,8)==0&&NativeBlockLooks.FlashFalloff(9,8)==0&&NativeBlockLooks.FlashFalloff(1,0)==0&&NativeBlockLooks.FlashFalloff(7.99,8)<.002,"flash fall-off 1 at the flash, 0 at its range, continuous at the cut");
            double prev=2;for(int i=0;i<=100;i++){double f=NativeBlockLooks.FlashFalloff(i*.08,8);Check(f<=prev,"flash fall-off decreases");prev=f;}Check(Math.Abs(NativeBlockLooks.FlashFalloff(4,8)-1/(1+25*.25))<1e-12,"Unity's 1/(1+25q^2) at half range");
            yield return "block looks: the round's one dust coat (a break dusts 6 m around it, a landing 6-14 m around it and the whole map as it settles over 8 s; capped, killcam-true on the match clock, order-free, cleared each round, no allocations) and the blast-flash fall-off";
            // Validation.
            void Reject(Action<MapLookSet> mutate,string what){var bad=MapLookChecks.Load(root);mutate(bad);bool threw=false;try{NativeMapLooks.Validate(bad);}catch(InvalidOperationException){threw=true;}Check(threw,"rejects "+what);}
            Reject(s=>NativeMapLooks.Find(s,"brickfall").blockLooks[0].look="marble","an unknown look key");Reject(s=>NativeMapLooks.Find(s,"brickfall").blockLooks[0].color=0x1000000,"a block colour beyond 0xFFFFFF");
            Reject(s=>NativeMapLooks.Find(s,"brickfall").blockLooks[0].block="material:","an empty material entry");Reject(s=>NativeMapLooks.Find(s,"brickfall").blocks.haze=1.5f,"haze above 1");
            Reject(s=>NativeMapLooks.Find(s,"brickfall").blocks.hazeTop=0,"haze with no top");Reject(s=>NativeMapLooks.Find(s,"brickfall").blocks.dust=float.NaN,"a NaN dust coat");Reject(s=>s.preview[0].look="","a preview entry without a look");
            Reject(s=>NativeMapLooks.Find(s,"brickfall").kitLooks[0].band=true,"a banded kit part");Reject(s=>NativeMapLooks.Find(s,"brickfall").kitLooks[0].block="material:brick","a kit entry by material");Reject(s=>NativeMapLooks.Find(s,"brickfall").kitLooks[0].look="glass","a kit entry with an unknown look");
            NativeMapLooks.Validate(set);
            yield return "block looks: validation rejects unknown looks, bad colours, empty patterns, bad haze and dust, bad preview entries and banded or material kit entries ("+count+" checks in all)";
        }
        // Kit part names (2.2 "Set dressing", 3.10): presentation only, never sim block ids.
        private static readonly string[] KitStems={"-window-","-sill-","-lintel-","-cornice-","-string-","-quoin-","-coping","-boards-","-shutter","-canopy","-neon-","-arch","mercer-sign","street-","lane-","yard-","square-"};
        // Brickfall's blockLooks entries for 3.9's static blocks (cores, frontage, shops, kiosks, market walls, docks, truck,
        // tenement floors) and 5.3's (shelters, storerooms).
        private static readonly HashSet<string> StaticStems=new HashSet<string>{"frontage-*","hotel-core-*","ashby-core*","bray-core*","ashby-floor-*","bray-floor-*","kessels*","dunmores*","market-*","storeroom-*","news-kiosk-*","kiosk-*","tram-shelter-*","dock-*","truck-*"};
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
