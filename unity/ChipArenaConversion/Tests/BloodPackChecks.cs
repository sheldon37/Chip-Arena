#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The blood pack (owner 2026-11-10; docs/BLOOD-PACK.md): the splashes a wound throws, the decals its blood leaves and the
    // flesh lumps among a gib's fragments are pure functions of the replicated wound, its seed, the settings and the clock;
    // each part falls back to the blood as it was when the local bake lacks it; gore off draws none.
    internal static class BloodPackChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Blood pack: "+what);count++;}
        private static string Code(string text)=>string.Join("\n",text.Replace("\r\n","\n").Split('\n').Where(l=>!l.TrimStart().StartsWith("//")));
        private static string Between(string s,string a,string b){int i=s.IndexOf(a,StringComparison.Ordinal);if(i<0)return "";int j=s.IndexOf(b,i+a.Length,StringComparison.Ordinal);return j<0?s.Substring(i):s.Substring(i,j-i);}
        public static void Run(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f)).Replace("\r\n","\n");
            Splashes();Decals();Guts();Source(R);
            Console.WriteLine("PASS blood pack: "+count+" checks");
        }
        private static readonly string[] Kinds={"cut","sever","puncture","cavity","crush","squirt","bruise"};
        private static void Splashes()
        {
            // How many: by the wound, a kill one more, thinned by reduced effects and the low tier, none with no quality or for a burn.
            foreach(string kind in Kinds)foreach(float s in new[]{0f,.5f,1f})
            {
                int full=NativeBloodSplash.Count(kind,s,1,false,false),kill=NativeBloodSplash.Count(kind,s,1,false,true),reduced=NativeBloodSplash.Count(kind,s,.3f,false,false),low=NativeBloodSplash.Count(kind,s,1,true,false);
                Check(full>=0&&full<=3&&kill<=4&&(full==0||kill==full+1),kind+" "+s+": at most three splashes, a kill one more ("+full+", "+kill+")");
                Check(reduced<=full&&(full==0||reduced>=1)&&reduced<=(full+1)/2&&low<=1&&NativeBloodSplash.Count(kind,s,1,true,true)<=2,kind+" "+s+": reduced effects halve them, the low tier draws one (two on a kill)");
                Check(NativeBloodSplash.Count(kind,s,0,false,true)==0,kind+": nothing at no quality");
            }
            Check(NativeBloodSplash.Count("scorch",1,1,false,true)==0&&NativeBloodSplash.Count("drip",1,1,false,false)==0&&NativeBloodSplash.Count(null,1,1,false,false)==0,"a burn, a drip and no kind throw none");
            Check(NativeBloodSplash.Count("cut",.3f,1,false,false)<NativeBloodSplash.Count("cut",.9f,1,false,false)&&NativeBloodSplash.Count("bruise",.2f,1,false,false)==0&&NativeBloodSplash.Count("bruise",.8f,1,false,false)==1,"a deep cut throws more; only a hard bruise bleeds");
            // Each splash: a sheet of the bake, a unit direction, a bounded size and life; the same wound gives the same splash.
            Vector3 normal=Vector3.Normalize(new Vector3(.3f,.2f,1)),along=Vector3.Normalize(new Vector3(1,.1f,-.2f));int[] used=new int[NativeBloodSplash.Sheets];
            foreach(string kind in Kinds)for(uint seed=1;seed<60;seed++)for(int i=0;i<4;i++)
            {
                NativeBloodSplash.Puff(kind,seed,i,normal,along,.6f,i==3,out int sheet,out var dir,out float size,out float life,out float delay,out bool flip);
                NativeBloodSplash.Puff(kind,seed,i,normal,along,.6f,i==3,out int sheet2,out var dir2,out float size2,out float life2,out float delay2,out bool flip2);
                Check(sheet==sheet2&&dir==dir2&&size==size2&&life==life2&&delay==delay2&&flip==flip2,kind+": the same wound throws the same splash");
                Check(sheet>=0&&sheet<NativeBloodSplash.Sheets&&Math.Abs(dir.Length()-1)<1e-3f&&size>.15f&&size<=NativeBloodSplash.SizeMax&&life>.3f&&life<.8f&&delay>=0&&delay<.2f&&(i>0||delay==0),kind+" "+i+": a sheet, a unit direction, a size and a life in range");
                used[sheet]++;
            }
            Check(used.All(n=>n>0),"every sheet of the bake is used");
            NativeBloodSplash.Puff("cut",7,0,normal,along,.6f,false,out int jet,out var jetDir,out _,out _,out _,out _);
            Check((jet==NativeBloodSplash.JetA||jet==NativeBloodSplash.JetB)&&Vector3.Dot(jetDir,along)>.6f,"a cut's first splash is a jet along the blade's travel");
            NativeBloodSplash.Puff("puncture",7,0,normal,along,.6f,false,out _,out var exitDir,out float exitSize,out _,out _,out _);NativeBloodSplash.Puff("puncture",7,1,normal,along,.6f,false,out _,out var entryDir,out float entrySize,out _,out _,out _);
            Check(Vector3.Dot(exitDir,along)>.8f&&Vector3.Dot(entryDir,along)<-.4f&&entrySize<exitSize,"a shot: a puff out of the exit, a smaller one back out of the entry");
            NativeBloodSplash.Puff("cut",7,0,normal,along,.6f,false,out _,out _,out float plain,out _,out _,out _);NativeBloodSplash.Puff("cut",7,0,normal,along,.6f,true,out _,out _,out float fatal,out _,out _,out _);
            NativeBloodSplash.Puff("cut",7,3,normal,along,.6f,true,out int burst,out _,out _,out _,out _,out _);
            Check(fatal>plain&&burst==NativeBloodSplash.Burst,"a killing blow's splashes are larger and add the burst");
            NativeBloodSplash.Puff("cut",7,0,new Vector3(float.NaN,0,0),Vector3.Zero,float.NaN,false,out _,out var safe,out float safeSize,out _,out _,out _);
            Check(Math.Abs(safe.Length()-1)<1e-3f&&safeSize>0&&!float.IsNaN(safeSize),"a broken wound still gives a finite splash");
            // The flipbook: every frame once in order, blended, inside its cell; the sheets' grids hold their frames.
            for(int sheet=0;sheet<NativeBloodSplash.Sheets;sheet++)
            {
                int columns=NativeBloodSplash.Columns[sheet],frames=NativeBloodSplash.Frames[sheet];Check(frames<=columns*columns&&frames>8,"sheet "+sheet+": its frames fit its grid");
                Check(NativeBloodSplash.PivotU[sheet]>0&&NativeBloodSplash.PivotU[sheet]<.3f&&Math.Abs(NativeBloodSplash.PivotV[sheet]-.5f)<.1f,"sheet "+sheet+": the spray starts at the cell's left middle");
                int last=-1;
                for(int k=0;k<=200;k++)
                {
                    NativeBloodSplash.Frame(sheet,k/200f,out int a,out int b,out float blend);
                    Check(a>=last&&a<=last+1&&a<frames&&(b==a+1||b==frames-1)&&blend>=0&&blend<1.0001f,"sheet "+sheet+": frames in order");last=a;
                    NativeBloodSplash.Cell(columns,a,out float u0,out float v0,out float side);Check(u0>=-1e-5f&&v0>=-1e-5f&&u0+side<=1.0001f&&v0+side<=1.0001f,"sheet "+sheet+": the cell is on the sheet");
                }
                Check(last==frames-1,"sheet "+sheet+": the last frame at the end");
            }
            NativeBloodSplash.Cell(4,0,out float cu,out float cv,out float cs);NativeBloodSplash.Cell(4,5,out float du,out float dv,out _);
            Check(cu==0&&Math.Abs(cv-.75f)<1e-6f&&cs==.25f&&du==.25f&&Math.Abs(dv-.5f)<1e-6f,"frames run left to right from the sheet's top row");
            NativeBloodSplash.Frame(0,float.NaN,out int nanA,out _,out _);Check(nanA==NativeBloodSplash.Frames[0]-1&&NativeBloodSplash.Opacity(0)==1&&NativeBloodSplash.Opacity(.5f)==1&&NativeBloodSplash.Opacity(.9f)<1&&NativeBloodSplash.Opacity(1)==0&&NativeBloodSplash.Opacity(float.NaN)==0,"whole, then gone over the last fifth");
            // The quad: on the wound, facing the camera, its travel across the screen; a spray at the camera still has a frame.
            var eye=new Vector3(0,1.6f,-6);var origin=new Vector3(1,1.2f,2);
            foreach(var dir in new[]{Vector3.UnitX,Vector3.UnitY,Vector3.Normalize(eye-origin),-Vector3.Normalize(eye-origin),Vector3.Normalize(new Vector3(-1,.4f,.3f))})foreach(bool flip in new[]{false,true})
            {
                NativeBloodSplash.Quad(origin,dir,.6f,.5f,0,flip,eye,out var pivot,out var ux,out var uy,out var n);
                Check(pivot==origin&&Math.Abs(ux.Length()-1)<1e-3f&&Math.Abs(uy.Length()-1)<1e-3f&&Math.Abs(Vector3.Dot(ux,uy))<1e-3f&&Math.Abs(Vector3.Dot(ux,n))<1e-3f,"a unit frame on the wound");
                Check(Vector3.Dot(n,Vector3.Normalize(eye-origin))>.999f&&Vector3.Dot(Vector3.Cross(ux,uy),n)*(flip?-1:1)>.99f,"facing the camera, mirrored by its flip");
                var flat=dir-n*Vector3.Dot(dir,n);if(flat.Length()>.3f)Check(Vector3.Dot(ux,Vector3.Normalize(flat))>.999f,"its +u is the spray as the camera sees it");
                NativeBloodSplash.Quad(origin,dir,.6f,.5f,.5f,flip,eye,out var later,out _,out _,out _);
                Check((later-origin).Length()<.6f*NativeBloodSplash.Drift+NativeBloodSplash.Sag*.125f+1e-3f&&later.Y<origin.Y+.6f*NativeBloodSplash.Drift+1e-4f,"it drifts along its spray and sags a little");
            }
            // The ring: bounded, budgeted, a kill never cut; it plays out and a rewind drops it.
            var ring=new NativeBloodSplashes();int thrown=0;for(int i=0;i<200;i++)thrown+=ring.Add("cut",.8f,(uint)i,origin,normal,along,1+i*.001f,1,false,false);
            Check(thrown<=NativeBloodSplashes.BurstMax+NativeBloodSplashes.Rate*.2f+1&&thrown>=3&&ring.Alive(1.25f)==thrown,"a flood of hits in a fifth of a second is thinned to the budget ("+thrown+")");
            int fatalCount=ring.Add("cut",.8f,999,origin,normal,along,1.2f,1,false,true);Check(fatalCount==4,"a killing blow's splashes are never cut");
            int refilled=ring.Add("cut",.8f,1000,origin,normal,along,3,1,false,false);Check(refilled==3&&ring.Update(3.01f)>=1&&ring.Alive(10)==0&&ring.Update(10)==0,"the budget refills; the splashes play out");
            for(int i=0;i<100;i++)ring.Add("sever",1,(uint)i,origin,normal,along,20+i,1,false,true);Check(ring.Items.Length==NativeBloodSplashes.Max&&ring.Items.Count(p=>p.Live)<=NativeBloodSplashes.Max,"a fixed ring");
            ring.Add("cut",.8f,5,origin,normal,along,500,1,false,true);Check(ring.Update(400)==0,"a rewind drops the splashes born after it");
            var reducedRing=new NativeBloodSplashes();int few=0;for(int i=0;i<200;i++)few+=reducedRing.Add("cut",.8f,(uint)i,origin,normal,along,1,.3f,false,false);Check(few<thrown,"reduced effects thin the budget too ("+few+")");
            ring.Clear();Check(ring.Items.All(p=>!p.Live),"cleared with the round");
            // Every spray throws its splashes where the runtime hands the ring over, and none without it.
            var sim=new NativeBloodSim();sim.Spray("cut",.8f,3,11,origin,normal,along,1,1,false,true);Check(sim.Flying>0,"the droplets as before without the ring");
            var with=new NativeBloodSim{Splashes=new NativeBloodSplashes()};with.Spray("cut",.8f,3,11,origin,normal,along,1,1,false,true);
            Check(with.Flying==sim.Flying&&with.Splashes.Alive(1.2f)==3,"a spray throws its splashes beside the same droplets");
            with.Spray("scorch",.8f,3,12,origin,normal,along,1,1,false,true);with.Spray("drip",1,3,13,origin,normal,along,1,1,false,true);Check(with.Splashes.Alive(1.2f)==3,"a burn and a drip throw none");
        }
        private static void Decals()
        {
            int[] sheets=new int[NativeBloodDecalLook.Sheets];int mirrored=0,n=0;
            for(uint seed=1;seed<=400;seed++)foreach(float g in new[]{.8f,1f,1.3f,1.6f,2.4f})
            {
                float w=.03f+.0001f*seed,l=w*g;
                NativeBloodDecalLook.Pick(seed,l,w,false,out int sheet,out float spin,out bool mirror,out float size);NativeBloodDecalLook.Pick(seed,l,w,false,out int sheet2,out float spin2,out bool mirror2,out float size2);
                Check(sheet==sheet2&&spin==spin2&&mirror==mirror2&&size==size2,"the same splat picks the same decal");
                Check(sheet>=0&&sheet<NativeBloodDecalLook.Sheets&&size>Math.Max(l,w)*2&&size<Math.Max(l,w)*6,"seed "+seed+": a sheet and a cell about the splat's size ("+size+")");
                if(g>NativeBloodSplatLook.StreakAbove)Check(sheet==NativeBloodDecalLook.Tail||sheet==NativeBloodDecalLook.Smear,"a grazing splat runs along its travel");
                if(sheet==NativeBloodDecalLook.Round)Check(spin>=0&&spin<MathF.PI*2+1e-4f,"a round one spun at random");else Check(Math.Abs(spin)<=.25f+1e-4f,"the others laid along the travel");
                sheets[sheet]++;if(mirror)mirrored++;n++;
            }
            Check(sheets.All(k=>k>n/20)&&mirrored>n/3&&mirrored<n*2/3,"all four sheets in use, about half mirrored");
            NativeBloodDecalLook.Pick(9,.35f,.3f,true,out int pool,out _,out _,out float poolSize);Check((pool==NativeBloodDecalLook.Round||pool==NativeBloodDecalLook.Splash)&&poolSize>.6f&&poolSize<1.1f,"a pool is a round or a splash, about its extent across");
            NativeBloodDecalLook.Pick(9,float.NaN,0,false,out _,out _,out _,out float broken);Check(broken>0&&!float.IsNaN(broken),"a broken splat still has a size");
            for(int k=0;k<NativeBloodDecalLook.Sheets;k++)Check(NativeBloodDecalLook.PivotU[k]>0&&NativeBloodDecalLook.PivotU[k]<1&&NativeBloodDecalLook.PivotV[k]>0&&NativeBloodDecalLook.PivotV[k]<1&&NativeBloodDecalLook.Span[k]>.5f&&NativeBloodDecalLook.Span[k]<=1,"sheet "+k+": its landing point and span are on the cell");
            Check(NativeBloodDecalLook.Frames==NativeBloodDecalLook.Columns*NativeBloodDecalLook.Columns&&NativeBloodDecalLook.Frame(0,.3f)==0&&NativeBloodDecalLook.Frame(-1,.3f)==0&&NativeBloodDecalLook.Frame(.3f,.3f)==NativeBloodDecalLook.Frames-1&&NativeBloodDecalLook.Frame(99,.3f)==NativeBloodDecalLook.Frames-1,"a splat spreads from its first frame to its last and stays");
            Check(NativeBloodDecalLook.Frame(.1f,.3f)>NativeBloodDecalLook.Frame(.05f,.3f)*1.5f&&NativeBloodDecalLook.Frame(.15f,.3f)>(NativeBloodDecalLook.Frames-1)*.7f,"fast first, easing to rest");
            Check(NativeBloodDecalLook.Spread(true)>NativeBloodDecalLook.Spread(false)*4&&NativeBloodDecalLook.PoolDelay<.5f,"a pool spreads slowly, and lands before the pool's rewind window");
            Check(NativeBloodDecalLook.PoolSize("sever",1,3)>NativeBloodDecalLook.PoolSize("puncture",1,3)&&NativeBloodDecalLook.PoolSize("cut",.5f,3)>.25f&&NativeBloodDecalLook.PoolSize("cut",1,3)<.5f&&NativeBloodDecalLook.PoolSize("bruise",1,3)==0&&NativeBloodDecalLook.PoolSize("scorch",1,3)==0&&NativeBloodDecalLook.PoolSize(null,1,3)==0,"a kill's pool by its wound; a bruise and a burn leave none");
            // The pool flag rides the splat pool and nothing else changes.
            var p=new NativeBloodSplats();int slot=p.Add(1,Vector3.Zero,Vector3.UnitY,Vector3.UnitX,.3f,.28f,7,9,1+NativeBloodDecalLook.PoolDelay,false,true);int drop=p.Add(2,Vector3.Zero,Vector3.UnitY,Vector3.UnitX,.05f,.04f,8,9,1);
            Check(slot>=0&&p.Items[slot].Pool&&!p.Items[drop].Pool&&p.Opacity(slot,1)==0&&p.Opacity(slot,2)==1,"a pool is a splat of the pool: unseen until it lands");
            p.Update(1);Check(p.Items[slot].Live,"it waits inside the rewind window");
        }
        private static void Guts()
        {
            for(int seed=0;seed<50;seed++)foreach(int chunks in new[]{6,24,64,84})
            {
                int full=0,reduced=0,low=0;bool[] meshes=new bool[NativeBloodGut.Meshes];
                for(int i=0;i<chunks;i++)
                {
                    if(NativeBloodGut.Is(i,chunks,seed,1,false,out int mesh)){full++;Check(mesh>=0&&mesh<NativeBloodGut.Meshes,"a mesh of the bake");meshes[mesh]=true;NativeBloodGut.Is(i,chunks,seed,1,false,out int again);Check(again==mesh,"the same lump for the same fragment");}
                    if(NativeBloodGut.Is(i,chunks,seed,.3f,false,out _))reduced++;if(NativeBloodGut.Is(i,chunks,seed,1,true,out _))low++;
                    float ratio=NativeBloodGut.Ratio(i,seed);Check(ratio>2&&ratio<3.2f,"a lump a few times its cube");
                }
                Check(full<=NativeBloodGut.Most&&full<=chunks/NativeBloodGut.Every+1&&(chunks<NativeBloodGut.Every*2||full>=1)&&reduced<=NativeBloodGut.Most/2&&reduced<=full&&low==0,"seed "+seed+", "+chunks+" fragments: a few lumps ("+full+"), fewer reduced ("+reduced+"), none on the low tier");
                if(chunks>=64)Check(full==NativeBloodGut.Most&&meshes.Count(m=>m)>=3,"a large gib has its full share, of several shapes");
            }
            Check(!NativeBloodGut.Is(-1,10,0,1,false,out _)&&!NativeBloodGut.Is(0,0,0,1,false,out _),"nothing outside the fragments");
        }
        private static void Source(Func<string,string> R)
        {
            string pack=Code(R("Runtime/NativeBloodPackPresentation.cs")),blood=Code(R("Runtime/NativeBloodPresentation.cs")),core=Code(R("Core/NativeBlood.cs")),kill=Code(R("Runtime/NativeKillEffectPresentation.cs"));
            // The bake's entries on the game's own shaders, or the blood as it was.
            Check(pack.Contains("var holder=prefab!=null?prefab.transform.Find(\"VisualBlood\"):null;if(holder==null)return;")&&pack.Contains("if(m==null||m.shader==null||m.shader.name!=shader||!m.shader.isSupported)return null;"),"each group whole on its own shader, or unused");
            foreach(var (group,shader) in new[]{("Splash","NativeBloodSplash"),("Decal","NativeBloodDecal"),("Gut","NativeBloodGut")})
            {
                Check(pack.Contains("holder.Find(\""+group+"\")")&&pack.Contains("\"ChipArena/"+shader+"\""),group+" reads its entries");
                string text=R("Resources/"+shader+".shader");Check(text.Contains("Shader \"ChipArena/"+shader+"\"")&&text.Contains("#include \"NativeBloodLight.cginc\"")&&text.Contains("\"LightMode\"=\"ForwardBase\"")&&text.Contains("Fallback Off"),shader+": the game's own shader, in the arena's light");
                Check(Regex.IsMatch(R("Resources/"+shader+".shader.meta"),@"^guid: [0-9a-f]{32}$",RegexOptions.Multiline),shader+" has its .meta");
            }
            Check(Regex.IsMatch(R("Resources/NativeBloodLight.cginc.meta"),@"^guid: [0-9a-f]{32}$",RegexOptions.Multiline)&&R("Resources/NativeBloodLight.cginc").Contains("float4 _ArenaLightPos[8];float4 _ArenaLightColor[8];"),"the light block reads the arena's slots");
            string splash=R("Resources/NativeBloodSplash.shader"),decal=R("Resources/NativeBloodDecal.shader"),gut=R("Resources/NativeBloodGut.shader");
            foreach(string text in new[]{splash,decal})Check(text.Contains("Blend One OneMinusSrcAlpha")&&text.Contains("ZWrite Off")&&text.Contains("multi_compile_fog")&&text.Contains("UnpackNormal("),"premultiplied, no depth write, fogged, normal mapped");
            Check(decal.Contains("\"Queue\"=\"Transparent-60\"")&&decal.Contains("GammaToLinearSpace(i.color.rgb)")&&pack.Contains("\"ChipArena/NativeBloodDecal\",(int)RenderQueue.Transparent-60,"),"the decals where today's splats draw, in today's colours");
            Check(gut.Contains("\"LightMode\"=\"ShadowCaster\"")&&gut.Contains("UNITY_LIGHT_ATTENUATION("),"the lumps cast and take the key's shadow");
            var block=Code(Between(decal,"BLOOD DECAL SHARED BEGIN","BLOOD DECAL SHARED END"));
            Check(block.Length>100&&!block.Contains("tex2D")&&!block.Contains("mul(")&&Regex.Matches(block,@"(?<![\w.])\d+(?![\w.])").Count==0,"the decal block: float literals only, no texture reads");
            Check(block.Contains("(1.0-(1.0-u)*(1.0-u))*35.0")&&NativeBloodDecalLook.Frames==36&&block.Contains("/6.0")&&NativeBloodDecalLook.Columns==6,"the shader's frame is the rule's");
            // The settings: gore off draws no blood; the low tier keeps the fans, no pools, no lumps; the mist only without splashes.
            Check(blood.Contains("if(w==null||!Gore)return;")&&pack.Contains("blood.Splashes=Gore&&splashMaterials!=null?splashes:null;")&&pack.Contains("if(decalMaterials==null||!Gore)return false;")&&pack.Contains("if(!Gore||alpha<.5f)return false;")&&pack.Contains("if(!Gore||LowGraphics||decalMaterials==null||bloodArena==null)return;"),"gore off: no blood, no splashes, no decals, no pools, no lumps");
            Check(blood.Contains("bool decals=!low&&DecalBegin();")&&blood.Contains("if(decals)DecalSplat(s,a,now);else if(sheets)SheetSplat(s,a,now);else Splat(s,a,now,low);"),"the pack's decals off the low tier, then the bank's splats, then the fans");
            Check(blood.Contains("if(stain&&BloodMistShown&&(")&&pack.Contains("private bool BloodMistShown=>blood.Splashes==null;"),"the wounds' mist only where no splash draws");
            Check(blood.Contains("if(kill)BloodPool(key,k,w.severity,seed,origin,along,time);")&&pack.Contains("blood.Splats.Add(key^(1L<<58),")&&pack.Contains("time+NativeBloodDecalLook.PoolDelay,false,true);"),"a kill lays its pool once, keyed by its wound");
            Check(core.Contains("if(count>0)Splashes?.Add(kind,severity,seed,origin,normal,along,time,quality,low,kill,ink);"),"every spray throws its splashes after the droplets' own budget");
            Check(kill.Contains("if(GutChunk(corpse,i,debris.Chunks,chunk,alpha))continue;")&&kill.Contains("sourceModels.KillChunk(chunk,new Color("),"a lump in a fragment's place, the others as before");
            Check(blood.Contains("DrawSplats(camera,now);DrawSplashes(camera,now);DrawGuts(camera);")&&blood.Contains("blood.Clear(splats);ClearBloodPack();"),"drawn with the blood, cleared with it");
            // Pooled: no allocation per splash, splat or lump, and no clock-free randomness.
            foreach(var (from,to) in new[]{("private void DrawSplashes(","private bool DecalBegin()"),("private void DecalSplat(","private void DecalEnd()"),("private bool GutChunk(","private void DrawGuts(")})
            {
                var body=Between(pack,from,to);Check(body.Length>200&&!Regex.IsMatch(body,@"new\s+(?!Vector4\b|Vector3\b|Color\b|NVector\b|Bounds\b|GutDraw\b)\w+"),"nothing allocated in "+from);
            }
            Check(!pack.Contains("Random")&&!pack.Contains("Time.time")&&!pack.Contains("Instantiate("),"no clock-free randomness, no instances");
            foreach(string f in new[]{"Runtime/NativeBloodPackPresentation.cs","Core/NativeBloodPack.cs","Resources/NativeBloodSplash.shader","Resources/NativeBloodDecal.shader","Resources/NativeBloodGut.shader","Resources/NativeBloodLight.cginc"})
                foreach(string name in new[]{"RVFX","BloodFX","BloodEffectsPack"})Check(!R(f).Contains(name),"no pack names in "+f+": "+name);
        }
    }
}
#endif
