#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // Effects overhaul, base layer, Runtime half (owner 2026-10-11: "The effects seem a bit outdated and there still seems to
    // be cube placeholders for certain impact effects", "that pancake ring effect is still there"). The Runtime, shaders and
    // Editor are not compiled here, so these are text scans of the shared renderer (Runtime/NativeVfxDraw.cs), its hooks, its
    // shaders and the bloom, plus the few numbers the shaders share with Core (atlas layout, erosion). The visual result is
    // not proven here: no Unity, GPU or Windows run.
    internal static class VfxRuntimeChecks
    {
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Vfx runtime check failed: "+what);}
        // Code without // comments (comments name what was removed and why).
        private static string Code(string text)=>string.Join("\n",text.Split('\n').Select(l=>{int c=l.IndexOf("//",StringComparison.Ordinal);return c>=0?l.Substring(0,c):l;}));
        private static string Between(string text,string begin,string end){int a=text.IndexOf(begin,StringComparison.Ordinal),b=text.IndexOf(end,StringComparison.Ordinal);Check(a>=0&&b>a,"block "+begin);return text.Substring(a,b-a);}
        public static void Run(string root)
        {
            string R(string file)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",file));
            int roles=Roles(R),renderer=Renderer(root,R),hooks=Hooks(R),shaders=Shaders(R),shared=Shared(R),bloom=NoBloom(root);
            Console.WriteLine("PASS effects base layer renderer: "+roles+" role mappings, "+renderer+" renderer checks (sorting, off-thread atlas, submission, release, no per-frame allocation), "+hooks+" hook checks (blasts, sparks, field cores, no disc), "+shaders+" shader checks, "+shared+" Core-shared numbers, "+bloom+" bloom ownership checks (text scans; no Unity render)");
        }
        // Every role has its primitive; the shell voxels are not drawn one by one; smoke grows on its raw size.
        private static int Roles(Func<string,string> R)
        {
            int n=0;string draw=Code(R("Runtime/NativeVfxDraw.cs"));
            foreach(var name in Enum.GetNames(typeof(NativeFxRole))){Check(draw.Contains("case NativeFxRole."+name+":"),"NativeVfxDraw maps role "+name);n++;}
            var expect=new (string role,string draw)[]
            {
                ("Shell","case NativeFxRole.Shell:break;"),("Mote","Glow(at,color*MoteGain,edge*MoteSize,Mote,"),("Core","Glow(at,color*CoreGain,edge*CoreSize,CoreGlow,"),
                ("Bubble","Glow(at,color*RingGain,edge*RingSize,Ring,"),("Snow","Flash(at,c,edge*SnowSize*SnowStar,NativeVfxTextures.StarTile,"),("Spark","Streak(at,velocity,color*SparkGain,"),
                ("Ember","Streak(at,velocity,color*EmberGain,"),("Streak","Streak(at,velocity,color*StreakGain,"),("Bolt/Line","Link(i,(int)seed);"),
                ("Smoke","Toon(at,ToonLift(color),ToonSmokeOpacity,start*ToonSmokeSize*Grow(u,"),("Cloud","Toon(at,ToonLift(color),ToonCloudOpacity,start*ToonSmokeSize*Grow(u,.6f,.35f,.8f),ToonSmokeMode,u,seed,floor,CloudGlowOf(color));"),
                ("Mist","Puff(at,color,ToonMistOpacity,start*ToonSmokeSize*Grow(u,.7f,.4f,.9f),SmokeMode,u,seed,floor,glow:MistGlow);"),
                ("Dark","DarkCore(at,edge,color);"),("Fireball","Toon(at,Hex(o,w+NativeBlastPool.HexFromAt),1,start*ToonFireSize*Grow(u,.55f,.27f,.5f),ToonFireMode,u,seed,floor,0,o[w+NativeBlastPool.HexToAt]);"),
                ("Flame","Toon(at,Hex(o,w+NativeBlastPool.HexFromAt)*FlameTint,1,edge*FlameSize,ToonFireMode,u*FlameAge,"),
                ("Water","Oriented(at,Hex(o,w+NativeBlastPool.HexFromAt),1,start*WaterSize*Grow(u,.7f,.25f,.55f),velocity*WaterStretch,o[w+NativeBlastPool.HexToAt],u,seed,floor);"),
                ("Blob","Oriented(at,Hex(o,w+NativeBlastPool.HexFromAt),1,start*BlobSize,velocity*BlobStretch,o[w+NativeBlastPool.HexToAt],0,seed,floor);"),
                ("Flash","Flash(at,c,start*FlashSize*(.45f+.55f*Mathf.Sqrt(u)),NativeVfxTextures.FlashFirst,u,seed);"),("Lick","NativeVfxTextures.CrescentFirst+(int)seed%NativeVfxTextures.Crescents,u,seed);"),
                ("Spike","((int)seed&1)==0?NativeVfxTextures.SpikeTile:NativeVfxTextures.LensTile,u,seed);"),("Droplet","Sprite(at,seg,c,wd,NativeVfxTextures.DropTile,u,seed);"),
                ("Halo","case NativeFxRole.Halo:Add(at,color*HaloGain,edge*CoreSize,0,u,seed,CoreGlow,NoFloor,new Vector4(0,0,0,1),true);"),
                ("Debris/Rock/HotDebris","case NativeFxRole.Debris:case NativeFxRole.Rock:case NativeFxRole.HotDebris:Chunk(o,w,(int)seed,u,color,role);"),("Shard","Crystal(o,w,(int)seed,color);")
            };
            foreach(var (role,code) in expect){Check(draw.Contains(code),role+" draws as its primitive");n++;}
            Check(draw.Contains("public void Blasts(NativeBlastSim sim){if(sim==null)return;Pool(sim.Solid);Pool(sim.Glow);Shocks(sim);}")&&draw.Contains("var shape=s.Dome?dome:shell;"),"each front is one shell, a dome or a sphere");n++;
            Check(draw.Contains("chunks[(seed%NativeVfxMeshes.ChunkVariants+NativeVfxMeshes.ChunkVariants)%NativeVfxMeshes.ChunkVariants]")&&draw.Contains("new Shape(NativeVfxMeshes.Shard)"),"rubble picks a chunk by seed; ice is the crystal shard");n++;
            Check(draw.Contains("Array.Sort(linkSeed,linkIndex,0,links);")&&draw.Contains("if(!NativeBlastPool.Linked(o,a,b))continue;")&&!draw.Contains("gap>(bolt?2:1)"),"bolt and line points join their seed neighbours within one strand (Core's rule)");n++;
            // The look fixes of the review (2026-10-11).
            Check(draw.Contains("float lum=.2126f*c.r+.7152f*c.g+.0722f*c.b;return c*Mathf.Clamp(ToonLiftMin/Mathf.Max(lum,1e-3f),1,ToonLiftMax);")&&draw.Contains("ToonLiftMin=.035f,ToonLiftMax=2f"),"toon smoke albedo is lifted only out of near-black (slate and ink stay dark), hue kept");n++;
            // Toon pass: fire runs on its spawn colours (flame to ash), water on its lit and shadow colours; growth curves; the
            // fuse; the slam wave; halos behind; the public Smoke helper is toon by default and SoftSmoke keeps the soft puff.
            Check(draw.Contains("private static float Grow(float u,float start,float peak,float end)=>u<peak?start+(1-start)*u/peak:1+(end-1)*(u-peak)/(1-peak);")&&draw.Contains("private static Color Hex(float[] o,int at)=>Linear((int)o[at]);"),"toon puffs grow and shrink over life; spawn colours from the draw list");n++;
            Check(draw.Contains("public void Smoke(Vector3 at,Color albedo,float opacity,float size,float u,float seed,float floor)=>Toon(at,albedo,Mathf.Clamp01(opacity),size,ToonSmokeMode,u,seed,floor);")&&draw.Contains("public void SoftSmoke(Vector3 at,Color albedo,float opacity,float size,float u,float seed,float floor)=>Puff(at,albedo,opacity,size,SmokeMode,u,seed,floor);"),"the public Smoke helper (same signature) is toon; SoftSmoke keeps the soft lit puff");n++;
            Check(draw.Contains("if(pose.Look!=NativeFieldLook.Mine)Fuse(tipAt,pose.Age,pose.Left);")&&draw.Contains("for(long k=(long)Math.Floor((age-.3)*rate);k<=(long)Math.Floor(age*rate);k++)")&&draw.Contains("float period=left<FuseHurry?.1f:.2f"),"the burning fuse: spikes, sparks, puffs and a flicker as a pure function of the bomb's age, faster in its last second");n++;
            Check(draw.Contains("if(!s.Started(t))continue;if(s.Kind==NativeShockKind.Slam){Slam(s,t);continue;}")&&draw.Contains("B=new Vector4(s.Seed,2,q.y,r)"),"Quake's slam wave is an upright open cylinder (B.y 2); late-born fronts draw from their birth");n++;
            Check(draw.Contains("emissive=HotColor*(HotGain*hot*hot)+color*ChunkSelf;")&&draw.Contains("float hot=Mathf.Clamp01(1-u/HotLife);")&&draw.Contains("Edges(o,w)*(rock?RockScale:DebrisScale)"),"fire-family debris glows hot and cools; chunks cover what the cubes did");n++;
            Check(draw.Contains("Sphere(at,radius,new Color(.004f,.002f,.008f,0),hue*DarkRim,Color.clear);"),"Void's dark core is an opaque orb with an HDR rim");n++;
            Check(draw.Contains("q=Quaternion.Slerp(q,lie,rest);")&&draw.Contains("Quaternion.AngleAxis(-90,Vector3.forward)")&&draw.Contains("color*CrystalGlow,color*CrystalRim")&&draw.Contains("CrystalGlow=.5f,CrystalRim=2.6f"),"crystals settle into a seeded resting pose; brighter edges");n++;
            Check(draw.Contains("if(s.Style==NativeBlastStyle.Quake)continue;")&&draw.Contains("BlastShock=1.2f,ImpactShock=.5f,NovaShock=1.9f"),"Quake draws no front; quieter blast and impact fronts");n++;
            Check(draw.Contains("(float)NativeVfxMeshes.BombTipY*scale.y")&&draw.Contains("float pulse=(float)pose.Emissive")&&draw.Contains("if(pose.FuseLit)Glow("),"the modelled bomb glows with the fuse pulse and a tip glow while the fuse shows");n++;
            return n;
        }
        private static int Renderer(string root,Func<string,string> R)
        {
            int n=0;string text=R("Runtime/NativeVfxDraw.cs"),draw=Code(text);
            Check(draw.Contains("keys[i]=-Vector3.Dot(p.Centre-eye,forward)+(p.Mode==CoreGlow&&!p.Behind?CoreLead*p.Size:0);order[i]=i;")&&draw.Contains("Array.Sort(keys,order,0,puffCount);")&&draw.Contains("ref var p=ref puffs[order[k]];"),"the billboards are sorted far to near every frame, core glows a little nearer");n++;
            Check(draw.Contains("var bounds=puffCount>0?new Bounds((lo+hi)*.5f,hi-lo):Everywhere;")&&draw.Contains("billboards.Draw(billboardMaterial,camera,bounds);")&&draw.Contains("Mesh.SetSubMesh(0,new SubMeshDescriptor(0,IndexCount){bounds=bounds,"),"the billboard batch is bounded by what it holds, so Unity sorts it among the transparent draws");n++;
            Check(draw.Contains("camera.depthTextureMode|=DepthTextureMode.Depth;depthCamera=camera;")&&draw.Contains("if(depthCamera!=null&&(depthCamera!=camera||Low)){depthCamera.depthTextureMode&=~DepthTextureMode.Depth;")&&draw.Contains("billboardMaterial.SetFloat(SoftId,!Low&&")&&draw.Contains("shockMaterial.SetFloat(LowId,Low?1:0);"),"soft particles: a depth texture on the normal tiers only, taken back on the low tier");n++;
            Check(draw.Contains("size*(Low?LowPuff:1)")&&draw.Contains("LowPuff=.9f")&&draw.Contains("if(Low&&Thin(seed))return;")&&draw.Contains("return (h>>16)%3==1;"),"the low tier skips a third of the toon puffs (by seed) and draws the rest smaller");n++;
            Check(draw.Contains("if(Interlocked.CompareExchange(ref built,1,0)!=0)return;")&&draw.Contains("ThreadPool.QueueUserWorkItem(BuildTextures)")&&draw.Contains("var atlas=NativeVfxTextures.SmokeAtlas();")&&draw.Contains("var toon=NativeVfxTextures.ToonSheet();"),"the smoke atlas and toon sheet are built once per process, off the main thread");n++;
            Check(Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs").Count(f=>Code(File.ReadAllText(f)).Contains("NativeVfxTextures.ToonSheet("))==1,"only the shared renderer builds the toon sheet");n++;
            var builders=Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs").Where(f=>Code(File.ReadAllText(f)).Contains("NativeVfxTextures.SmokeAtlas(")).Select(Path.GetFileName).ToList();
            Check(builders.Count==1&&builders[0]=="NativeVfxDraw.cs","only the shared renderer builds the atlas ("+string.Join(", ",builders)+")");n++;
            Check(Regex.Matches(draw,@"NativeVfxTextures\.SmokeAtlas\(").Count==1&&draw.Contains("if(atlas!=null||Volatile.Read(ref built)!=2)return;")&&draw.Contains("billboardMaterial.SetFloat(ReadyId,1);"),"the main thread only uploads the finished bytes, then switches the puffs from their stand-in");n++;
            Check(draw.Contains("atlas.SetPixelData(a,0);atlas.Apply(true,true);")&&draw.Contains("TextureFormat.RGBA32,true,true)")&&draw.Contains("noise.SetPixelData(n,0);noise.Apply(false,true);")&&draw.Contains("toon.SetPixelData(t,0);toon.Apply(true,true);")&&draw.Contains("billboardMaterial.SetTexture(ToonId,toon);"),"the textures upload as linear RGBA32 (mipmapped atlas and toon sheet), CPU copies released");n++;
            Check(draw.Contains("Graphics.DrawMesh(Mesh,Matrix4x4.identity,material,0,camera,0,null,ShadowCastingMode.Off,false);")&&draw.Contains("lit.Draw(litMaterial,camera);billboards.Draw(billboardMaterial,camera,bounds);shocks.Draw(shockMaterial,camera);"),"three draws a frame to the given camera, layer 0, no shadows");n++;
            Check(draw.Contains("renderQueue=(int)RenderQueue.Transparent,")&&draw.Contains("renderQueue=(int)RenderQueue.Geometry")&&draw.Contains("renderQueue=(int)RenderQueue.Transparent+4"),"billboards in the Transparent queue with the domes, lit meshes opaque, shells after the puffs");n++;
            foreach(var release in new[]{"billboards.Mesh","lit.Mesh","shocks.Mesh","billboardMaterial","litMaterial","shockMaterial","atlas","noise","toon"}){Check(draw.Contains("CapturedAnimation.Release("+release+")"),"Dispose releases "+release);n++;}
            Check(!draw.Contains("new List<")&&!draw.Contains("ToArray(")&&!draw.Contains("AddComponent<")&&!draw.Contains("ParticleSystem")&&!draw.Contains("new Light"),"no per-frame collections, no components, lights or particle systems");n++;
            // Arrays are only replaced when they must grow (Array.Resize), never per frame.
            var body=Between(draw,"public void Begin()","private void Upload()");var structs=new HashSet<string>{"Vector3","Vector4","Color","Quaternion","Queued","MeshVertex","BillboardVertex","SubMeshDescriptor"};
            var made=Regex.Matches(body,@"new\s+(\w+)\s*([\[({])").Cast<Match>().Where(m=>m.Groups[2].Value=="["||!structs.Contains(m.Groups[1].Value)).Select(m=>m.Value).ToList();
            Check(made.Count==0,"the per-frame queueing allocates nothing, structs only ("+string.Join(", ",made)+")");n++;
            Check(draw.Contains("SetVertexBufferData(Vertices,0,0,VertexCount,0,Quiet)")&&draw.Contains("MeshUpdateFlags.DontValidateIndices|MeshUpdateFlags.DontRecalculateBounds")&&draw.Contains("Mesh.MarkDynamic();"),"dynamic meshes refilled in place");n++;
            Check(draw.Contains("public void Submit(Camera camera)")&&Regex.Matches(draw,@"Begin\(\);\s*\}").Count>=1,"Submit empties the queue for the next frame");n++;
            return n;
        }
        // The presentations hand their particles to the shared renderer; the field glow disc is gone; the cubes are gone.
        private static int Hooks(Func<string,string> R)
        {
            int n=0;string blast=Code(R("Runtime/NativeBlastPresentation.cs")),shot=Code(R("Runtime/NativeShotPresentation.cs")),field=Code(R("Runtime/NativeFieldPresentation.cs")),arena=Code(R("Runtime/ArenaPresentation.cs"));
            Check(blast.Contains("if(ownsVfx)vfx.Begin();vfx.Blasts(sim);if(ownsVfx)vfx.Submit(camera);")&&blast.Contains("NativeArenaDressing.Flashes(sim.Flashes);")&&blast.Contains("light.type=LightType.Point;"),"blasts draw through the renderer and keep the three flash lights and the block feed");n++;
            Check(!blast.Contains("CubeCorners")&&!blast.Contains("Fill(")&&!blast.Contains("Resources.Load<Shader>(\"NativeBlast\")"),"no blast voxel cubes");n++;
            Check(shot.Contains("vfx.Sparks(pool);")&&!shot.Contains("CubeCorners")&&shot.Contains("Draw(solid,camera);Draw(beams,camera);if(ownsVfx)vfx.Submit(camera);"),"burst sparks are streaks; the models and beams are kept");n++;
            Check(field.Contains("if(NativeFieldRules.Bomb(v,now,out var pose))vfx.Bomb(pose,hex);")&&field.Contains("vfx.Sphere(core,")&&field.Contains("vfx.Streak(at,velocity,")&&!field.Contains("CubeCorners")&&!field.Contains("Native field cores"),"field cores: the modelled bomb, the black hole's lit core and the orbiting streaks, no cubes");n++;
            Check(!field.Contains("NativeFieldRules.GlowDisc")&&!field.Contains("Native field glow")&&field.Contains("if(look==NativeFieldLook.Dome){Append(dome,NativeFieldRules.Dome,"),"no field ground glow disc (the pancake); the time and repulsion domes stay");n++;
            Check(field.Contains("vfx.Glow(new Vector3(preview[w],preview[w+1],preview[w+2]),color,size*preview[w+3],NativeVfxDraw.Mote);"),"the aim preview motes are soft glows");n++;
            Check(arena.Contains("blasts=new NativeBlastPresentation(transform,vfx);shots=new NativeShotPresentation(vfx);")&&arena.Contains("fieldTelegraphs=new NativeFieldPresentation(vfx);"),"one shared renderer for the blasts, sparks and field cores");n++;
            Check(arena.Contains("vfx=new NativeVfxDraw();combatGlow=new NativeCombatGlow(transform,vfx);")&&arena.Contains("meleeFlash=new NativeMeleeFlashPresentation(vfx);"),"the melee flash and the combat glow's contact flashes share the renderer");n++;
            int begin=arena.IndexOf("if(vfx!=null)vfx.Low=LowGraphics;vfx?.Begin();fieldTelegraphs?.Collect(arena,fields);",StringComparison.Ordinal),blasts=arena.IndexOf("blasts?.Render(arena,camera,now,BlastQuality,paused,fieldTelegraphs);",StringComparison.Ordinal);
            int melee=arena.IndexOf("meleeFlash?.Render(arena,camera,fields,WeaponEffectsQuality);",StringComparison.Ordinal),early=arena.IndexOf("if(combatGlow==null){vfx?.Submit(camera);return;}",StringComparison.Ordinal),glow=arena.IndexOf("combatGlow.Render(arena,camera,",StringComparison.Ordinal),submit=arena.IndexOf("vfx?.Submit(camera);}",glow<0?0:glow,StringComparison.Ordinal);
            Check(begin>=0&&blasts>begin&&melee>blasts&&early>melee&&glow>early&&submit>glow&&Regex.Matches(arena,@"vfx\?\.Submit\(camera\)").Count==2,"RenderGlow tiers and begins the renderer, queues the blasts, field cores, sparks, melee and contact flashes, then submits once on either path");n++;
            string combat=Code(R("Runtime/NativeCombatGlow.cs")),melee2=Code(R("Runtime/NativeMeleeFlash.cs"));
            Check(!Regex.IsMatch(combat,@"world\.Halo\(point,rotation\*")&&!combat.Contains("NativeGlowRules.ImpactSize(")&&combat.Contains("vfx.Glow(point+rotation*Vector3.forward*ContactLift,color,ContactSize,NativeVfxDraw.CoreGlow);")&&combat.Contains("float f=Mathf.Clamp01(1-(now-mark.Born)/ContactLife);")&&combat.Contains("SetLight(impactLights[i],"),"no flat glowing disc on a hit surface (the second pancake): a short contact flash; the spill lights stay");n++;
            Check(!melee2.Contains("Box(")&&!melee2.Contains("CubeCorners")&&!melee2.Contains("Resources.Load<Shader>(\"NativeBlast\")")&&melee2.Contains("vfx.Streak(origin+")&&melee2.Contains("vfx.Smoke(root+")&&melee2.Contains("if(ownsVfx)vfx.Submit(camera);"),"melee sparks are streaks and slam dust is smoke puffs, no cuboids or squares; the stones stay");n++;
            Check(arena.Contains("vfx?.Clear();")&&arena.Contains("vfx?.Dispose();"),"reset clears and destroy disposes the renderer");n++;
            // Nothing in the renderer path reads back into the match.
            {var code=Code(R("Runtime/NativeVfxDraw.cs"));Check(!code.Contains("arena.")&&!code.Contains("Match")&&!code.Contains("Events"),"NativeVfxDraw reads no match state");n++;}
            return n;
        }
        private static int Shaders(Func<string,string> R)
        {
            int n=0;string vfx=R("Resources/NativeVfx.shader"),lit=R("Resources/NativeVfxLit.shader"),draw=Code(R("Runtime/NativeVfxDraw.cs")),gate=R("Editor/NativeBuildChecks.cs");
            Check(vfx.Contains("Blend One OneMinusSrcAlpha")&&vfx.Contains("ZWrite Off")&&vfx.Contains("\"LightMode\"=\"ForwardBase\"")&&vfx.Contains("#pragma multi_compile_fog")&&vfx.Contains("#pragma target 3.5"),"billboards: premultiplied, no depth write, forward base, fog, SM3.5");n++;
            Check(vfx.Contains("UNITY_APPLY_FOG_COLOR(i.fogCoord,c,float4(unity_FogColor.rgb*c.a,0));"),"premultiplied fog: smoke fogs to the fog colour, additive glows to black");n++;
            // The C# modes and the shader's thresholds agree.
            Check(draw.Contains("public const float Mote=0,CoreGlow=1,Ring=2,Snow=3,StreakMode=4,SmokeMode=5,FireballMode=6;")&&vfx.Contains("if(mode<3.5)c=i.color*VfxGlow(i.uv.xy,mode);")&&vfx.Contains("else if(mode<4.5)c=i.color*VfxStreak(")&&vfx.Contains("if(mode<5.5)")&&vfx.Contains("c=VfxSmoke(")&&vfx.Contains("else if(mode<6.5)c=VfxFireball(")&&!vfx.Contains("VfxDark("),"billboard modes match the renderer's constants");n++;
            // Toon pass: modes 7-11 and their thresholds, the toon sheet sampled outside the branches with its one-pixel widths.
            Check(draw.Contains("public const float ToonSmokeMode=7,ToonFireMode=8,ToonWaterMode=9,FlashMode=10,SpriteMode=11;")&&vfx.Contains("if(mode<7.5)c=VfxToonSmoke(")&&vfx.Contains("else if(mode<8.5)c=VfxToonFire(")&&vfx.Contains("else c=VfxToonWater(")&&vfx.Contains("else c=VfxToonSprite(toon,aa,i.color);")&&vfx.Contains("bool oriented=mode>3.5&&mode<4.5||mode>8.5&&mode<9.5||mode>10.5;"),"toon modes match the renderer's constants; streaks, toon water and sprites lie along their segment");n++;
            Check(vfx.Contains("float4 toon=lerp(VfxToonStandIn(i.uv.xy),tex2D(_VfxToon,i.toon.xy),_VfxAtlasReady);float4 aa=fwidth(toon);")&&vfx.Contains("float3 mask=VfxToonMask(toon,aa,VfxToonCut(u))"),"the toon sheet is read and differentiated outside the mode branches, a round stand-in until it is uploaded");n++;
            Check(vfx.Contains("float4 toon:TEXCOORD8;")&&vfx.Contains("float4 alt:TEXCOORD9;")&&!vfx.Contains("TEXCOORD10"),"at most ten interpolators (SM3.5)");n++;
            Check(vfx.Contains("float fadeLength=toonPuff?clamp(size*0.12,0.05,0.25):clamp(size*0.3,0.08,0.8);"),"toon puffs meet the floor and the scene nearly crisply (no glowing ring at their foot)");n++;
            Check(lit.Contains("if(i.b.y>1.5)c=float4(VfxSlam(")&&lit.Contains("float foot = smoothstep(0.0, 0.3, height) * (1.0 - height) * 1.6;")&&lit.Contains("float edge = pow(1.0 - facing, 2.5);"),"the slam wave: bright at its silhouette, rising off the floor, fading to its top (no line on the floor, no filled disc from above)");n++;
            Check(!vfx.Contains("float3(1.0, 0.86, 0.58)")&&vfx.Contains("float glow = (0.6 + 0.5 * heat) * (0.65 + 0.35 * tex.g) * saturate(1.3 - u * 1.25);")&&vfx.Contains("float alpha = cover * (0.85 + 0.1 * soot);"),"fireballs keep their hue (no white lerp), nearly opaque");n++;
            Check(vfx.Contains("UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);")&&vfx.Contains("SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen))")&&vfx.Contains("(_VfxSoft>0.5?VfxSoftFade(scene,i.screen.z,")&&vfx.Contains("o.screen=ComputeScreenPos(o.pos);"),"soft particles fade against the scene's depth, selected off without a depth texture");n++;
            Check(vfx.Contains("c.rgb+=i.color.rgb*(i.flash.w*c.a*(1.0-u));")&&vfx.Contains("o.flash=float4(flash*_VfxFlashGain,v.axis.x);")&&draw.Contains("new Vector4(glow,0,0,aspect)"),"a saturated cloud glows a little in its own colour");n++;
            Check(vfx.Contains("ShadeSH9(float4(0,1,0,1))")&&vfx.Contains("_LightColor0.rgb")&&vfx.Contains("_ArenaFlashColor[k].rgb*VfxFlashFalloff(v.vertex,_ArenaFlashPos[k])"),"smoke is lit by the ambient, the main light and the three blast flashes");n++;
            Check(vfx.Contains("(world.y-v.life.w)/fadeLength")&&vfx.Contains("c*=saturate(i.data.w);")&&vfx.Contains("float nearFade=saturate((depth-0.2)/max(0.35,size*0.6));"),"puffs fade below their floor and near the camera");n++;
            Check(vfx.Contains("tex2D(_VfxAtlas,i.uv.zw)")&&vfx.Contains("lerp(VfxPuffStandIn(i.uv.xy),tex2D(_VfxAtlas,i.uv.zw),_VfxAtlasReady)"),"the atlas is sampled outside the mode branches, the stand-in until it is uploaded");n++;
            Check(lit.Contains("Blend [_Src] [_Dst]")&&lit.Contains("Cull [_Cull]")&&lit.Contains("\"LightMode\"=\"ForwardBase\"")&&lit.Contains("#pragma target 3.5")&&lit.Contains("#pragma multi_compile_fog"),"lit meshes and shells: one shader, render state per material");n++;
            Check(lit.Contains("float base = lerp(1.0, smoothstep(0.05, 0.5, height), dome);")&&draw.Contains("B=new Vector4(seed,isDome,n.y,r)"),"a dome shell's base fades out: no ring or bowl on the floor");n++;
            Check(lit.Contains("float body = pow(1.0 - facing, 4.0);")&&!lit.Contains("0.03 + 0.97")&&lit.Contains("float travel = 0.15 + 0.85 * exp(-4.0 * progress);")&&lit.Contains("float nearFade=saturate((length(_WorldSpaceCameraPos-i.world)-0.4)/2.2);")&&draw.Contains("shockMaterial.SetFloat(\"_Dst\",(float)BlendMode.One)"),"the shell is a thin additive fresnel rim with a clear centre, bright at the arrival");n++;
            Check(lit.Contains("float front=dot(n,view)<0.0&&length(_WorldSpaceCameraPos-(i.world-n*i.b.w))>i.b.w?0.0:1.0;")&&lit.Contains("if(_Shock>0.5&&_VfxLow<0.5)grain=tex2Dlod(_VfxNoise,"),"front faces only from outside the shell; no noise read on the low tier");n++;
            Check(lit.Contains("VfxLitFlash(v.vertex,n,_ArenaFlashPos[k],_ArenaFlashColor[k])")&&lit.Contains("ShadeSH9(float4(n,1.0))"),"lit meshes take the ambient, the main light and the blast flashes");n++;
            Check(gate.Contains("foreach(var name in new[]{\"NativeVfx\",\"NativeVfxLit\"}){var shader=Resources.Load<Shader>(name);if(shader==null||ShaderUtil.ShaderHasError(shader))")&&gate.Contains("CheckVfx();"),"the new shaders are build-checked, with the renderer's Editor check");n++;
            foreach(var name in new[]{"Resources/NativeVfx.shader","Resources/NativeVfxLit.shader","Runtime/NativeVfxDraw.cs","Editor/NativeVfxBuildChecks.cs"})
            {Check(Regex.IsMatch(R(name+".meta"),@"^guid: [0-9a-f]{32}$",RegexOptions.Multiline),name+" has its .meta");n++;}
            return n;
        }
        // The numbers the shaders share with Core, and the SHARED blocks stay in the GLSL-compatible subset.
        private static int Shared(Func<string,string> R)
        {
            int n=0;string vfx=R("Resources/NativeVfx.shader"),lit=R("Resources/NativeVfxLit.shader");
            // Atlas tile by seed (shader: tile = seed mod 16, column tile mod 4, row tile / 4 from the bottom, 0.25 per tile) is Core's TileRect.
            Check(NativeVfxTextures.SmokeTiles==4&&NativeVfxTextures.SmokeVariants==16&&vfx.Contains("float tile=v.life.y-16.0*floor(v.life.y/16.0),col=tile-4.0*floor(tile/4.0),row=floor(tile/4.0);")&&vfx.Contains("*0.46+0.5)*0.25)"),"the shader's atlas tiles are Core's 4 x 4");n++;
            for(int seed=0;seed<200;seed+=7)
            {
                double tile=seed-16.0*Math.Floor(seed/16.0),col=tile-4.0*Math.Floor(tile/4.0),row=Math.Floor(tile/4.0);
                NativeVfxTextures.TileRect((int)tile,out double u0,out double v0,out double u1,out double v1);
                Check(Math.Abs(col*.25-u0)<1e-12&&Math.Abs(row*.25-v0)<1e-12&&Math.Abs(u1-u0-.25)<1e-12,"seed "+seed+" picks tile "+tile+" at Core's TileRect");n++;
            }
            // Toon pass: the toon sheet's tiles (8 x 4, from TEXCOORD1.y for puffs, TEXCOORD1.w for flashes and sprites) are Core's ToonRect.
            Check(vfx.Contains("float sheetCol=sheet-8.0*floor(sheet/8.0),sheetRow=floor(sheet/8.0);")&&vfx.Contains("o.toon=float4((sheetCol+tc.x*0.46+0.5)*0.125,(sheetRow+tc.y*0.46+0.5)*0.25,0,0);")&&vfx.Contains("sheet=v.life.w<23.5?v.life.w+min(floor(v.life.x*8.0),7.0):v.life.w;"),"the shader's toon tiles");n++;
            for(int tile=0;tile<32;tile++)
            {
                double col=tile-8.0*Math.Floor(tile/8.0),row=Math.Floor(tile/8.0);NativeVfxTextures.ToonRect(tile,out double u0,out double v0,out double u1,out double v1);
                Check(Math.Abs(col*.125-u0)<1e-12&&Math.Abs(row*.25-v0)<1e-12&&Math.Abs(u1-u0-.125)<1e-12&&Math.Abs(v1-v0-.25)<1e-12,"toon tile "+tile+" at Core's ToonRect");n++;
            }
            Check(NativeVfxTextures.FlashFirst+7.0<23.5&&NativeVfxTextures.CrescentFirst>23.5&&NativeVfxTextures.FlashFrames==8,"tiles below 23.5 play the flipbook (8 frames from tile 16); later tiles stay put");n++;
            // The shader's toon erosion cut is Core's ToonCut, and its hex decode is Core's Linear to within the fit.
            Check(vfx.Contains("float x = saturate((u - 0.42) / 0.58);")&&vfx.Contains("return x * x * (3.0 - 2.0 * x) * 1.02;")&&NativeVfxTextures.ToonEatFrom==.42,"the shader's toon cut is NativeVfxTextures.ToonCut");
            {double cutErr=0;for(double u=0;u<=1.0001;u+=.01){double x=Math.Max(0,Math.Min(1,(u-.42)/.58));cutErr=Math.Max(cutErr,Math.Abs(x*x*(3-2*x)*1.02-NativeVfxTextures.ToonCut(u)));}Check(cutErr<1e-12,"the toon cut formula equals Core's ("+cutErr+")");}
            {
                double hexErr=0;foreach(int hex in new[]{0xffce00,0x3e4556,0x2a9ae6,0xe8fbff,0x123456,0xffffff,0})
                {
                    double h=hex,r=Math.Floor(h/65536),g=Math.Floor((h-r*65536)/256),b=h-r*65536-g*256;NativeBlastSim.Linear(hex,out double lr,out double lg,out double lb);
                    double F(double c){c/=255;return c*(c*(c*0.305306011+0.682171111)+0.012522878);}hexErr=Math.Max(hexErr,Math.Max(Math.Abs(F(r)-lr),Math.Max(Math.Abs(F(g)-lg),Math.Abs(F(b)-lb))));
                }
                Check(vfx.Contains("float r = floor(hex / 65536.0);")&&vfx.Contains("return c * (c * (c * 0.305306011 + 0.682171111) + 0.012522878);")&&hexErr<.005,"the shader's packed hex decodes to Core's linear colour within .005 ("+hexErr.ToString("0.0000")+")");n+=3;
            }
            // The quad's corners reach exactly the puff's edge (density 0 there), so no tile's neighbour shows.
            Check(Math.Abs(.46-NativeVfxTextures.SmokeEdge/2)<1e-12,"the quad spans the puff out to SmokeEdge");n++;
            // The shader's erosion is Core's Dissolve.
            Check(vfx.Contains("float soft = 0.15;")&&Math.Abs(NativeVfxTextures.DissolveSoft-.15)<1e-12&&vfx.Contains("float e = saturate((threshold + soft - u * (1.0 + soft)) / soft);")&&vfx.Contains("return u < 1.0 ? density * e * e * (3.0 - 2.0 * e) : 0.0;"),"the shader's erosion is NativeVfxTextures.Dissolve");n++;
            double worst=0;for(double d=0;d<=1;d+=.125)for(double t=0;t<=1;t+=.0625)for(double u=0;u<=1.0001;u+=.03125)
            {double e=Math.Max(0,Math.Min(1,(t+.15-u*1.15)/.15)),shader=u<1?d*e*e*(3-2*e):0;worst=Math.Max(worst,Math.Abs(shader-NativeVfxTextures.Dissolve(d,t,u)));}
            Check(worst<1e-12,"the shader's erosion formula equals Core's at every sample ("+worst+")");n++;
            foreach(var (name,text,begin,end) in new[]{("NativeVfx",vfx,"VFX SHARED BEGIN","VFX SHARED END"),("NativeVfxLit",lit,"VFX LIT SHARED BEGIN","VFX LIT SHARED END")})
            {
                var block=Code(Between(text,begin,end));
                Check(!block.Contains("mul(")&&!block.Contains("x3")&&!block.Contains("x4")&&!block.Contains("tex2D"),name+" SHARED: no matrices or texture reads");
                var ints=Regex.Matches(block,@"(?<![\w.])\d+(?![\w.])").Cast<Match>().Select(m=>m.Value).ToList();
                Check(ints.Count==0,name+" SHARED: float literals only ("+string.Join(", ",ints)+")");n+=2;
            }
            return n;
        }
        // The match camera has exactly one bloom: claude/fx-bloom's Runtime/NativeBloom.cs with Resources/NativeBloom.shader
        // (Core/NativeBloomRules.cs, Tests/BloomChecks.cs). The base layer's own first-draft bloom (NativeBloomImage) is gone, so
        // no second bloom component or camera hook exists.
        private static int NoBloom(string root)
        {
            int n=0;
            Check(!File.Exists(Path.Combine(root,"Assets/ChipArena/Runtime/NativeBloomImage.cs")),"no second bloom here: Assets/ChipArena/Runtime/NativeBloomImage.cs");n++;
            foreach(var file in new[]{"Assets/ChipArena/Runtime/NativeBloom.cs","Assets/ChipArena/Resources/NativeBloom.shader"}){Check(File.Exists(Path.Combine(root,file)),"the one bloom is present: "+file);n++;}
            foreach(var file in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs"))
            {Check(!Code(File.ReadAllText(file)).Contains("NativeBloomImage"),"no second bloom hook: "+Path.GetFileName(file));n++;}
            return n;
        }
    }
}
#endif
