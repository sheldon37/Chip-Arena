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
            int roles=Roles(R),renderer=Renderer(root,R),hooks=Hooks(R),shaders=Shaders(R),shared=Shared(R),bloom=Bloom(R);
            Console.WriteLine("PASS effects base layer renderer: "+roles+" role mappings, "+renderer+" renderer checks (sorting, off-thread atlas, submission, release, no per-frame allocation), "+hooks+" hook checks (blasts, sparks, field cores, no disc), "+shaders+" shader checks, "+shared+" Core-shared numbers, "+bloom+" bloom checks (text scans; no Unity render)");
        }
        // Every role has its primitive; the shell voxels are not drawn one by one; smoke grows on its raw size.
        private static int Roles(Func<string,string> R)
        {
            int n=0;string draw=Code(R("Runtime/NativeVfxDraw.cs"));
            foreach(var name in Enum.GetNames(typeof(NativeFxRole))){Check(draw.Contains("case NativeFxRole."+name+":"),"NativeVfxDraw maps role "+name);n++;}
            var expect=new (string role,string draw)[]
            {
                ("Shell","case NativeFxRole.Shell:break;"),("Mote","Glow(at,color*MoteGain,edge*MoteSize,Mote,"),("Core","Glow(at,color*CoreGain,edge*CoreSize,CoreGlow,"),
                ("Bubble","Glow(at,color*RingGain,edge*RingSize,Ring,"),("Snow","Glow(at,c,edge*SnowSize,Snow,"),("Spark","Streak(at,velocity,color*SparkGain,"),
                ("Ember","Streak(at,velocity,color*EmberGain,"),("Streak","Streak(at,velocity,color*StreakGain,"),("Bolt/Line","Link(i,(int)seed);"),
                ("Smoke","Puff(at,color*SmokeAlbedo,SmokeOpacity,raw*SmokeSize,SmokeMode,"),("Cloud","Puff(at,color*SmokeAlbedo,CloudOpacity,raw*CloudSize,SmokeMode,"),
                ("Dark","Puff(at,color,DarkOpacity,raw*DarkSize,DarkMode,"),("Fireball","Puff(at,color,1,raw*FireballSize,FireballMode,"),("Flame","Puff(at,color*FlameTint,1,edge*FlameSize,FireballMode,"),
                ("Debris/Rock","Chunk(o,w,(int)seed,u,color,role==NativeFxRole.Rock);"),("Shard","Crystal(o,w,color);")
            };
            foreach(var (role,code) in expect){Check(draw.Contains(code),role+" draws as its primitive");n++;}
            Check(draw.Contains("public void Blasts(NativeBlastSim sim){if(sim==null)return;Pool(sim.Solid);Pool(sim.Glow);Shocks(sim);}")&&draw.Contains("var shape=s.Dome?dome:shell;"),"each front is one shell, a dome or a sphere");n++;
            Check(draw.Contains("chunks[(seed%NativeVfxMeshes.ChunkVariants+NativeVfxMeshes.ChunkVariants)%NativeVfxMeshes.ChunkVariants]")&&draw.Contains("new Shape(NativeVfxMeshes.Shard)"),"rubble picks a chunk by seed; ice is the crystal shard");n++;
            Check(draw.Contains("Array.Sort(linkSeed,linkIndex,0,links);")&&draw.Contains("if(gap<1||gap>(bolt?2:1))continue;"),"bolt and line points join their seed neighbours");n++;
            Check(draw.Contains("(float)NativeVfxMeshes.BombTipY*scale.y")&&draw.Contains("float pulse=(float)pose.Emissive")&&draw.Contains("if(pose.FuseLit)Glow("),"the modelled bomb glows with the fuse pulse and a tip glow while the fuse shows");n++;
            return n;
        }
        private static int Renderer(string root,Func<string,string> R)
        {
            int n=0;string text=R("Runtime/NativeVfxDraw.cs"),draw=Code(text);
            Check(draw.Contains("keys[i]=-Vector3.Dot(puffs[i].Centre-eye,forward);order[i]=i;")&&draw.Contains("Array.Sort(keys,order,0,puffCount);")&&draw.Contains("ref var p=ref puffs[order[k]];"),"the billboards are sorted far to near every frame");n++;
            Check(draw.Contains("if(Interlocked.CompareExchange(ref built,1,0)!=0)return;")&&draw.Contains("ThreadPool.QueueUserWorkItem(BuildTextures)")&&draw.Contains("var atlas=NativeVfxTextures.SmokeAtlas();"),"the smoke atlas is built once per process, off the main thread");n++;
            var builders=Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs").Where(f=>Code(File.ReadAllText(f)).Contains("NativeVfxTextures.SmokeAtlas(")).Select(Path.GetFileName).ToList();
            Check(builders.Count==1&&builders[0]=="NativeVfxDraw.cs","only the shared renderer builds the atlas ("+string.Join(", ",builders)+")");n++;
            Check(Regex.Matches(draw,@"NativeVfxTextures\.SmokeAtlas\(").Count==1&&draw.Contains("if(atlas!=null||Volatile.Read(ref built)!=2)return;")&&draw.Contains("billboardMaterial.SetFloat(ReadyId,1);"),"the main thread only uploads the finished bytes, then switches the puffs from their stand-in");n++;
            Check(draw.Contains("atlas.SetPixelData(a,0);atlas.Apply(true,true);")&&draw.Contains("TextureFormat.RGBA32,true,true)")&&draw.Contains("noise.SetPixelData(n,0);noise.Apply(false,true);"),"the textures upload as linear RGBA32 (mipmapped atlas), CPU copies released");n++;
            Check(draw.Contains("Graphics.DrawMesh(Mesh,Matrix4x4.identity,material,0,camera,0,null,ShadowCastingMode.Off,false);")&&draw.Contains("lit.Draw(litMaterial,camera);billboards.Draw(billboardMaterial,camera);shocks.Draw(shockMaterial,camera);"),"three draws a frame to the given camera, layer 0, no shadows");n++;
            Check(draw.Contains("renderQueue=(int)RenderQueue.Transparent+3")&&draw.Contains("renderQueue=(int)RenderQueue.Geometry")&&draw.Contains("renderQueue=(int)RenderQueue.Transparent+4"),"billboards after the domes, lit meshes opaque, shells after the puffs");n++;
            foreach(var release in new[]{"billboards.Mesh","lit.Mesh","shocks.Mesh","billboardMaterial","litMaterial","shockMaterial","atlas","noise"}){Check(draw.Contains("CapturedAnimation.Release("+release+")"),"Dispose releases "+release);n++;}
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
            Check(arena.Contains("vfx=new NativeVfxDraw();blasts=new NativeBlastPresentation(transform,vfx);shots=new NativeShotPresentation(vfx);")&&arena.Contains("fieldTelegraphs=new NativeFieldPresentation(vfx);"),"one shared renderer for the blasts, sparks and field cores");n++;
            int begin=arena.IndexOf("vfx?.Begin();fieldTelegraphs?.Collect(arena,fields);",StringComparison.Ordinal),blasts=arena.IndexOf("blasts?.Render(arena,camera,now,BlastQuality,paused,fieldTelegraphs);",StringComparison.Ordinal),submit=arena.IndexOf("shots?.Render(arena,camera,fields,BlastQuality,paused,shotTracker);vfx?.Submit(camera);",StringComparison.Ordinal);
            Check(begin>=0&&blasts>begin&&submit>blasts,"RenderGlow begins the renderer, queues the blasts, field cores and sparks, then submits once");n++;
            Check(arena.Contains("vfx?.Clear();")&&arena.Contains("vfx?.Dispose();"),"reset clears and destroy disposes the renderer");n++;
            // Nothing in the renderer path reads back into the match.
            foreach(var (name,code) in new[]{("NativeVfxDraw",Code(R("Runtime/NativeVfxDraw.cs"))),("NativeBloomImage",Code(R("Runtime/NativeBloomImage.cs")))}){Check(!code.Contains("arena.")&&!code.Contains("Match")&&!code.Contains("Events"),name+" reads no match state");n++;}
            return n;
        }
        private static int Shaders(Func<string,string> R)
        {
            int n=0;string vfx=R("Resources/NativeVfx.shader"),lit=R("Resources/NativeVfxLit.shader"),draw=Code(R("Runtime/NativeVfxDraw.cs")),gate=R("Editor/NativeBuildChecks.cs");
            Check(vfx.Contains("Blend One OneMinusSrcAlpha")&&vfx.Contains("ZWrite Off")&&vfx.Contains("\"LightMode\"=\"ForwardBase\"")&&vfx.Contains("#pragma multi_compile_fog")&&vfx.Contains("#pragma target 3.5"),"billboards: premultiplied, no depth write, forward base, fog, SM3.5");n++;
            Check(vfx.Contains("UNITY_APPLY_FOG_COLOR(i.fogCoord,c,float4(unity_FogColor.rgb*c.a,0));"),"premultiplied fog: smoke fogs to the fog colour, additive glows to black");n++;
            // The C# modes and the shader's thresholds agree.
            Check(draw.Contains("public const float Mote=0,CoreGlow=1,Ring=2,Snow=3,StreakMode=4,SmokeMode=5,FireballMode=6,DarkMode=7;")&&vfx.Contains("if(mode<3.5)c=i.color*VfxGlow(i.uv.xy,mode);")&&vfx.Contains("else if(mode<4.5)c=i.color*VfxStreak(")&&vfx.Contains("if(mode<5.5)c=VfxSmoke(")&&vfx.Contains("else if(mode<6.5)c=VfxFireball(")&&vfx.Contains("else c=VfxDark("),"billboard modes match the renderer's constants");n++;
            Check(vfx.Contains("ShadeSH9(float4(0,1,0,1))")&&vfx.Contains("_LightColor0.rgb")&&vfx.Contains("_ArenaFlashColor[k].rgb*VfxFlashFalloff(v.vertex,_ArenaFlashPos[k])"),"smoke is lit by the ambient, the main light and the three blast flashes");n++;
            Check(vfx.Contains("(world.y-v.life.w)/max(0.05,size*0.3)")&&vfx.Contains("c*=saturate(i.data.w);")&&vfx.Contains("float nearFade=saturate((depth-0.2)/max(0.35,size*0.6));"),"puffs fade below their floor and near the camera");n++;
            Check(vfx.Contains("tex2D(_VfxAtlas,i.uv.zw)")&&vfx.Contains("lerp(VfxPuffStandIn(i.uv.xy),tex2D(_VfxAtlas,i.uv.zw),_VfxAtlasReady)"),"the atlas is sampled outside the mode branches, the stand-in until it is uploaded");n++;
            Check(lit.Contains("Blend [_Src] [_Dst]")&&lit.Contains("Cull [_Cull]")&&lit.Contains("\"LightMode\"=\"ForwardBase\"")&&lit.Contains("#pragma target 3.5")&&lit.Contains("#pragma multi_compile_fog"),"lit meshes and shells: one shader, render state per material");n++;
            Check(lit.Contains("float base = lerp(1.0, smoothstep(0.0, 0.35, height), dome);")&&draw.Contains("B=new Vector4(seed,isDome,n.y,0)"),"a dome shell's base fades out: no ring on the floor");n++;
            Check(lit.Contains("float rim = pow(1.0 - facing, 3.0);")&&lit.Contains("float body = 0.03 + 0.97 * rim;")&&lit.Contains("float nearFade=saturate((length(_WorldSpaceCameraPos-i.world)-0.4)/2.2);")&&draw.Contains("shockMaterial.SetFloat(\"_Dst\",(float)BlendMode.One)"),"the shell is an additive fresnel rim with a nearly clear centre");n++;
            Check(lit.Contains("VfxLitFlash(v.vertex,n,_ArenaFlashPos[k],_ArenaFlashColor[k])")&&lit.Contains("ShadeSH9(float4(n,1.0))"),"lit meshes take the ambient, the main light and the blast flashes");n++;
            Check(gate.Contains("foreach(var name in new[]{\"NativeVfx\",\"NativeVfxLit\",\"NativeBloom\"}){var shader=Resources.Load<Shader>(name);if(shader==null||ShaderUtil.ShaderHasError(shader))")&&gate.Contains("CheckVfx();"),"the new shaders are build-checked, with the renderer's Editor check");n++;
            foreach(var name in new[]{"Resources/NativeVfx.shader","Resources/NativeVfxLit.shader","Resources/NativeBloom.shader","Runtime/NativeVfxDraw.cs","Runtime/NativeBloomImage.cs","Editor/NativeVfxBuildChecks.cs"})
            {Check(Regex.IsMatch(R(name+".meta"),@"^guid: [0-9a-f]{32}$",RegexOptions.Multiline),name+" has its .meta");n++;}
            return n;
        }
        // The numbers the shaders share with Core, and the SHARED blocks stay in the GLSL-compatible subset.
        private static int Shared(Func<string,string> R)
        {
            int n=0;string vfx=R("Resources/NativeVfx.shader"),lit=R("Resources/NativeVfxLit.shader"),bloom=R("Resources/NativeBloom.shader");
            // Atlas tile by seed (shader: tile = seed mod 16, column tile mod 4, row tile / 4 from the bottom, 0.25 per tile) is Core's TileRect.
            Check(NativeVfxTextures.SmokeTiles==4&&NativeVfxTextures.SmokeVariants==16&&vfx.Contains("float tile=v.life.y-16.0*floor(v.life.y/16.0),col=tile-4.0*floor(tile/4.0),row=floor(tile/4.0);")&&vfx.Contains("*0.46+0.5)*0.25)"),"the shader's atlas tiles are Core's 4 x 4");n++;
            for(int seed=0;seed<200;seed+=7)
            {
                double tile=seed-16.0*Math.Floor(seed/16.0),col=tile-4.0*Math.Floor(tile/4.0),row=Math.Floor(tile/4.0);
                NativeVfxTextures.TileRect((int)tile,out double u0,out double v0,out double u1,out double v1);
                Check(Math.Abs(col*.25-u0)<1e-12&&Math.Abs(row*.25-v0)<1e-12&&Math.Abs(u1-u0-.25)<1e-12,"seed "+seed+" picks tile "+tile+" at Core's TileRect");n++;
            }
            // The quad's corners reach exactly the puff's edge (density 0 there), so no tile's neighbour shows.
            Check(Math.Abs(.46-NativeVfxTextures.SmokeEdge/2)<1e-12,"the quad spans the puff out to SmokeEdge");n++;
            // The shader's erosion is Core's Dissolve.
            Check(vfx.Contains("float soft = 0.15;")&&Math.Abs(NativeVfxTextures.DissolveSoft-.15)<1e-12&&vfx.Contains("float e = saturate((threshold + soft - u * (1.0 + soft)) / soft);")&&vfx.Contains("return u < 1.0 ? density * e * e * (3.0 - 2.0 * e) : 0.0;"),"the shader's erosion is NativeVfxTextures.Dissolve");n++;
            double worst=0;for(double d=0;d<=1;d+=.125)for(double t=0;t<=1;t+=.0625)for(double u=0;u<=1.0001;u+=.03125)
            {double e=Math.Max(0,Math.Min(1,(t+.15-u*1.15)/.15)),shader=u<1?d*e*e*(3-2*e):0;worst=Math.Max(worst,Math.Abs(shader-NativeVfxTextures.Dissolve(d,t,u)));}
            Check(worst<1e-12,"the shader's erosion formula equals Core's at every sample ("+worst+")");n++;
            foreach(var (name,text,begin,end) in new[]{("NativeVfx",vfx,"VFX SHARED BEGIN","VFX SHARED END"),("NativeVfxLit",lit,"VFX LIT SHARED BEGIN","VFX LIT SHARED END"),("NativeBloom",bloom,"BLOOM SHARED BEGIN","BLOOM SHARED END")})
            {
                var block=Code(Between(text,begin,end));
                Check(!block.Contains("mul(")&&!block.Contains("x3")&&!block.Contains("x4")&&!block.Contains("tex2D"),name+" SHARED: no matrices or texture reads");
                var ints=Regex.Matches(block,@"(?<![\w.])\d+(?![\w.])").Cast<Match>().Select(m=>m.Value).ToList();
                Check(ints.Count==0,name+" SHARED: float literals only ("+string.Join(", ",ints)+")");n+=2;
            }
            return n;
        }
        private static int Bloom(Func<string,string> R)
        {
            int n=0;string image=Code(R("Runtime/NativeBloomImage.cs")),shader=R("Resources/NativeBloom.shader"),game=Code(R("Runtime/ConversionGame.cs")),settings=Code(R("Runtime/NativeSettings.cs")),damage=Code(R("Runtime/NativeDamageScreenPresentation.cs"));
            Check(image.Contains("private void OnRenderImage(RenderTexture source,RenderTexture destination)")&&image.Contains("Graphics.Blit(source,destination);return;")&&image.Contains("Graphics.Blit(source,destination,material,3);"),"bloom is an OnRenderImage effect through Graphics.Blit, passing the frame through when off");n++;
            Check(!image.Contains("CommandBuffer")&&!image.Contains("CameraTarget")&&!image.Contains("BuiltinRenderTextureType"),"no camera-target round trip (the damage screen's flip lesson)");n++;
            Check(image.Contains("RenderTexture.ReleaseTemporary(levels[i])")&&image.Contains("CapturedAnimation.Release(material)"),"temporaries and the material are released");n++;
            Check(shader.Contains("if(_MainTex_TexelSize.y<0.0)uv.y=1.0-uv.y;")&&Regex.Matches(shader,@"\n\s*Pass\s*\n").Count==4&&shader.Contains("Blend One One"),"four passes; the additive upsample; the second texture follows a flipped source");n++;
            int camera=game.IndexOf("cameraView=cam.AddComponent<Camera>();",StringComparison.Ordinal),added=game.IndexOf("bloom=cam.AddComponent<NativeBloomImage>();",StringComparison.Ordinal);
            Check(camera>=0&&added>camera&&!damage.Contains("NativeBloom")&&settings.Contains("if(bloom!=null)bloom.Low=low;"),"the match camera gets the bloom first (before the damage screen's effect), low graphics reaches it");n++;
            return n;
        }
    }
}
#endif
