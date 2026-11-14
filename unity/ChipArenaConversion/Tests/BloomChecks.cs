#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The match camera's bloom (Core/NativeBloomRules.cs, Runtime/NativeBloom.cs, Resources/NativeBloom.shader). Unity-only (the
    // Electron client had no post effects): no parity fixture, so these pin the Unity rules. Checked: the soft knee is exactly
    // 0 at or below linear 1 and continuous above it, the level plan, the quality gate (off on the low tier, x.6 with Reduced
    // effects), the component on the match camera only, HDR on, its place before the damage screen's image effect, the
    // orientation-safe OnRenderImage -> Graphics.Blit path, every temporary released in the frame, the four passes and the
    // GLSL-compatible shared code, the build gate and the metas. Rendering itself (D3D11, the halo's look) is not run here.
    internal static class BloomChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Bloom: "+what);count++;}
        // C# or shader source without // comments (the headers describe paths the code must not take).
        private static string Code(string text)=>string.Join("\n",text.Split('\n').Select(l=>{int c=l.IndexOf("//",StringComparison.Ordinal);return c<0?l:l.Substring(0,c);}));
        private static string Body(string code,string head)
        {
            int at=code.IndexOf(head,StringComparison.Ordinal);if(at<0)return "";int open=code.IndexOf('{',at);if(open<0)return "";
            int depth=0;for(int i=open;i<code.Length;i++){if(code[i]=='{')depth++;else if(code[i]=='}'&&--depth==0)return code.Substring(open,i-open+1);}
            return "";
        }
        public static void Run(string root)
        {
            Rules();
            string R(string p)=>File.ReadAllText(Path.Combine(root,p));
            string bloom=Code(R("Assets/ChipArena/Runtime/NativeBloom.cs")),game=Code(R("Assets/ChipArena/Runtime/ConversionGame.cs")),settings=Code(R("Assets/ChipArena/Runtime/NativeSettings.cs"));
            string shaderText=R("Assets/ChipArena/Resources/NativeBloom.shader");
            Component(bloom);Camera(root,game,settings);Order(root,game);Shader(shaderText,bloom);
            Check(R("Assets/ChipArena/Editor/NativeBuildChecks.cs").Contains("Resources.Load<Shader>(\"NativeBloom\")"),"the shader is build-checked");
            foreach(var file in new[]{"Runtime/NativeBloom.cs","Core/NativeBloomRules.cs","Resources/NativeBloom.shader"})
            {
                string meta=Path.Combine(root,"Assets/ChipArena",file+".meta");Check(File.Exists(meta),"meta: "+file);
                var guid=Regex.Match(File.ReadAllText(meta),@"^guid: ([0-9a-f]{32})$",RegexOptions.Multiline);Check(guid.Success,"meta guid: "+file);
                int owners=Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories).Count(m=>File.ReadAllText(m).Contains("guid: "+guid.Groups[1].Value));
                Check(owners==1,"unique guid: "+file);
            }
            Console.WriteLine("PASS Bloom ("+count+" checks: knee 0 at or below 1, continuous and monotone; 6 levels half to 1/64; off on low graphics, x.6 reduced; match camera only, HDR on, attached with the camera ahead of the damage screen; OnRenderImage -> Graphics.Blit, ARGBHalf temporaries released every frame; four passes, flip guard, GLSL-safe shared code; hue-keeping display shoulder; build gate, metas)");
        }
        private static void Rules()
        {
            Check(NativeBloomRules.Threshold==1f&&NativeBloomRules.Knee==.5f,"threshold 1, knee .5");
            foreach(float b in new[]{-1f,0f,.5f,.9f,.99f,1f})Check(NativeBloomRules.Weight(b)==0,"nothing at or below linear 1 blooms: "+b);
            Check(NativeBloomRules.Weight(1.01f)>0&&NativeBloomRules.Weight(1.01f)<.001f,"the knee starts gently above 1");
            Check(Math.Abs(NativeBloomRules.Weight(1.5f)-.125f)<1e-6&&Math.Abs(NativeBloomRules.Weight(2f)-.5f)<1e-6&&Math.Abs(NativeBloomRules.Weight(3f)-1.5f)<1e-6&&Math.Abs(NativeBloomRules.Weight(10f)-8.5f)<1e-5,"quadratic over [1, 2], then the linear excess b - 1.5");
            float prev=0,worstStep=0;for(int i=0;i<=1200;i++){float b=i*.01f,w=NativeBloomRules.Weight(b);Check(w>=prev-1e-6f&&w<=Math.Max(0,b),"monotone and never above the input at "+b);worstStep=Math.Max(worstStep,w-prev);prev=w;}
            Check(worstStep<=.0101f,"continuous: no step above the 0.01 sweep ("+worstStep+")");
            Check(NativeBloomRules.Levels==6&&NativeBloomRules.LevelCount(1920,1080)==6&&NativeBloomRules.LevelSize(1920,0)==960&&NativeBloomRules.LevelSize(1080,0)==540&&NativeBloomRules.LevelSize(1920,5)==30&&NativeBloomRules.LevelSize(1080,5)==16,"1080p: 960x540 down to 30x16");
            Check(NativeBloomRules.LevelCount(3440,1440)==6&&NativeBloomRules.LevelCount(640,360)==6&&NativeBloomRules.LevelCount(16,16)==3&&NativeBloomRules.LevelCount(4,4)==1&&NativeBloomRules.LevelCount(3,3)==0&&NativeBloomRules.LevelCount(0,0)==0&&NativeBloomRules.LevelCount(1920,2)==0,"small windows get fewer levels, never one under 2 px");
            Check(NativeBloomRules.LevelSize(1920,-1)==0&&NativeBloomRules.LevelSize(1920,31)==0&&NativeBloomRules.LevelSize(-5,0)==0,"level sizes stay defined");
            Check(NativeBloomRules.Strength(false,false)==NativeBloomRules.Intensity&&NativeBloomRules.Intensity>0&&NativeBloomRules.Intensity<=1,"full intensity on the high tier");
            Check(NativeBloomRules.ReducedScale==.6f&&Math.Abs(NativeBloomRules.Strength(false,true)-NativeBloomRules.Intensity*.6f)<1e-7,"Reduced effects x.6");
            Check(NativeBloomRules.Strength(true,false)==0&&NativeBloomRules.Strength(true,true)==0,"off on the low graphics tier");
            Check(NativeBloomRules.FireflyClamp>=4&&NativeBloomRules.FireflyClamp<=64,"a firefly clamp well above the knee");
            // The display shoulder: identity up to .9, never above 1, monotone in brightness, hue kept below 2, white when very hot.
            System.Numerics.Vector3 V(float r,float g,float b)=>new System.Numerics.Vector3(r,g,b);
            foreach(var c in new[]{V(0,0,0),V(.9f,.5f,.1f),V(.2f,.85f,.9f),V(.5f,.5f,.5f)})Check(NativeBloomRules.Shoulder(c)==c,"the shoulder leaves colours at or below .9 alone: "+c);
            float last=0;for(int i=0;i<=4000;i++){float m=.9f+i*.005f;var o=NativeBloomRules.Shoulder(V(m,m*.45f,m*.05f));float p=Math.Max(o.X,Math.Max(o.Y,o.Z));Check(p<=1f&&p>=last-1e-6f,"the shoulder's peak stays at most 1 and rises with brightness at "+m);last=p;}
            var fire=NativeBloomRules.Shoulder(V(1.29f,.58f,.065f));Check(Math.Abs(fire.Y/fire.X-.58f/1.29f)<1e-5f&&Math.Abs(fire.Z/fire.X-.065f/1.29f)<1e-5f&&fire.X>.99f,"a fireball a little over 1 keeps its hue ("+fire+")");
            var spark=NativeBloomRules.Shoulder(V(9f,4f,.45f));Check(spark.X>.99f&&Math.Abs(spark.X-spark.Z)<1e-5f,"a white-hot spark core reads white ("+spark+")");
            var core=NativeBloomRules.Shoulder(V(3f,1.35f,.15f));Check(core.Y/core.X>.45f&&core.Y/core.X<.65f,"a hot spark body warms toward white only a little ("+core+")");
        }
        private static void Component(string c)
        {
            Check(c.Contains("public sealed class NativeBloom:MonoBehaviour"),"the component");
            string image=Body(c,"private void OnRenderImage(RenderTexture source,RenderTexture destination)");Check(image.Length>0,"OnRenderImage(source, destination)");
            foreach(var token in new[]{"CommandBuffer","CameraTarget","CameraEvent","GetTemporaryRT","new RenderTexture(","ReadPixels"})Check(!c.Contains(token),"no command buffer or camera-target round trip: "+token);
            Check(image.Contains("Graphics.Blit(source,destination);")&&image.Contains("Graphics.Blit(source,destination,material,CompositePass);"),"passes the frame through, or composites it, with Graphics.Blit");
            Check(Regex.Matches(image,@"\bnew (?!Vector4\()").Count==0,"OnRenderImage allocates nothing but temporaries");
            Check(Regex.Matches(c,@"RenderTexture\.GetTemporary\(").Count==1&&c.Contains("RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear)"),"one ARGBHalf linear temporary helper");
            Check(image.Contains("down[i]=Temporary(")&&image.Contains("up[i]=Temporary(")&&Regex.IsMatch(image,@"finally\{[^}]*Release\(\);\}"),"every temporary released in the same frame (finally)");
            string release=Body(c,"private void Release()");
            Check(release.Contains("RenderTexture.ReleaseTemporary(down[i]);down[i]=null;")&&release.Contains("RenderTexture.ReleaseTemporary(up[i]);up[i]=null;")&&release.Contains("i<down.Length"),"Release returns and clears every slot");
            Check(c.Contains("private void OnDisable()=>Release();")&&Regex.IsMatch(Body(c,"private void OnDestroy()"),@"Release\(\);.*Destroy\(material\)"),"released when disabled or destroyed, material dropped");
            Check(c.Contains("public const int PrefilterPass=0,DownPass=1,UpPass=2,CompositePass=3;"),"pass indices");
            Check(c.Contains("public void Quality(bool lowGraphics,bool reducedEffects){LowGraphics=lowGraphics;ReducedEffects=reducedEffects;enabled=!lowGraphics&&!missing;}"),"disabled on the low tier (Unity skips it)");
            Check(c.Contains("public float Strength=>NativeBloomRules.Strength(LowGraphics,ReducedEffects);")&&image.Contains("new Vector4(strength,0,0,0)"),"the composite intensity comes from the rules");
            Check(image.Contains("NativeBloomRules.LevelCount(source.width,source.height)")&&image.Contains("NativeBloomRules.LevelSize(source.width,i)")&&image.Contains("new Vector4(NativeBloomRules.Threshold,NativeBloomRules.Knee,NativeBloomRules.FireflyClamp,0)"),"the level plan and curve come from the rules");
            Check(c.Contains("Resources.Load<Shader>(\"NativeBloom\")")&&c.Contains("!shader.isSupported")&&c.Contains("SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)"),"off without the shader or half-float targets");
            string attach=Body(c,"public static NativeBloom Attach(Camera camera)");
            Check(attach.Contains("camera.GetComponent<NativeBloom>()")&&attach.Contains("camera.gameObject.AddComponent<NativeBloom>()"),"Attach reuses or appends the component");
        }
        private static void Camera(string root,string game,string settings)
        {
            string awake=Body(game,"private void Awake()");
            int created=awake.IndexOf("cameraView=cam.AddComponent<Camera>();",StringComparison.Ordinal),attached=awake.IndexOf("cameraView.allowHDR=true;bloom=NativeBloom.Attach(cameraView);",StringComparison.Ordinal);
            int model=awake.IndexOf("viewModelCamera=model.AddComponent<Camera>();",StringComparison.Ordinal),apply=awake.IndexOf("ApplySettings();",StringComparison.Ordinal);
            Check(created>=0&&attached>created&&model>attached&&apply>attached,"HDR on and the bloom attached right after the match camera is made, before the view-model camera and the settings");
            Check(Regex.Matches(awake,@"NativeBloom\.Attach\(").Count==1&&!awake.Contains("NativeBloom.Attach(viewModelCamera"),"one bloom, on the match camera only");
            Check(game.Contains("private NativeBloom bloom;"),"the game keeps it");
            string apply2=Body(settings,"private void ApplySettings()");int low=apply2.IndexOf("bool low=prefs.graphics==\"performance\"",StringComparison.Ordinal),quality=apply2.IndexOf("if(bloom!=null)bloom.Quality(low,prefs.reducedEffects);",StringComparison.Ordinal);
            Check(low>=0&&quality>low,"ApplySettings gates it by the graphics tier and Reduced effects");
            foreach(var file in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs"))
            {
                string name=Path.GetFileName(file),text=Code(File.ReadAllText(file));
                if(name!="ConversionGame.cs")Check(!text.Contains("NativeBloom.Attach("),"only the match camera gets the bloom: "+name);
                if(name!="NativeBloom.cs")Check(!text.Contains("AddComponent<NativeBloom>"),"only Attach adds it: "+name);
                Check(!text.Contains("cameraView.allowHDR=false")&&!text.Contains("Destroy(bloom"),"the match camera stays HDR and keeps its bloom: "+name);
            }
        }
        // Unity runs a camera's image effects in component order: the bloom must come before the damage screen's.
        private static void Order(string root,string game)
        {
            string awake=Body(game,"private void Awake()");
            Check(!awake.Contains("NativeDamageScreen"),"Awake never adds the damage screen's image effect");
            // The Sheldon Intro's glitch pass follows it the same way (Runtime/NativeSheldonIntroPresentation.cs, also added on demand).
            Check(game.Contains("try{LateFrame();DamageScreen();GlitchScreen();}finally{FinishHitchFrame(HitchMs(at));}")&&!awake.Contains("NativeGlitchScreen"),"the damage screen and glitch passes run from LateUpdate, after Awake");
            foreach(var file in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs"))
            {
                string name=Path.GetFileName(file),text=Code(File.ReadAllText(file));
                if(name=="NativeDamageScreenPresentation.cs")continue;
                Check(!text.Contains("AddComponent<NativeDamageScreenImage>")&&!text.Contains("NativeDamageScreenPass.Frame("),"only the damage pass adds its image effect, on demand: "+name);
            }
            string pass=Code(File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeDamageScreenPresentation.cs")));
            Check(Body(pass,"private static NativeDamageScreenImage Image(Camera camera)").Contains("camera.gameObject.AddComponent<NativeDamageScreenImage>()")&&Body(pass,"private void DamageScreen()").Contains("NativeDamageScreenPass.Frame("),"the damage image is appended lazily from the frame pass, so it sits behind the bloom");
        }
        private static void Shader(string text,string component)
        {
            string code=Code(text);
            Check(code.Contains("Shader \"Hidden/ChipArena/NativeBloom\""),"hidden shader name");
            var frags=Regex.Matches(code,@"#pragma fragment (\w+)").Select(m=>m.Groups[1].Value).ToArray();
            Check(frags.SequenceEqual(new[]{"FragPrefilter","FragDown","FragUp","FragComposite"}),"four passes in the component's order: "+string.Join(",",frags));
            Check(Regex.Matches(code,@"#pragma vertex vert_img").Count==4&&Regex.Matches(code,@"#pragma target 3\.5").Count==4,"vert_img, SM 3.5");
            foreach(var id in new[]{"_BloomCurve","_BloomParams","_BloomTex"})Check(code.Contains(id)&&component.Contains("\""+id+"\""),"property "+id);
            var blocks=Regex.Matches(text,@"// SHARED BEGIN\n(.*?)// SHARED END",RegexOptions.Singleline);Check(blocks.Count==1,"one SHARED block");
            string shared=Code(blocks[0].Groups[1].Value);
            foreach(var f in new[]{"float3 BloomSafe(","float3 BloomKnee(","float3 BloomDown(","float3 BloomTent(","float3 BloomUp(","float3 BloomShoulder(","float3 BloomComposite("})Check(shared.Contains(f),"shared "+f);
            foreach(var token in new[]{"tex2D","sampler","_MainTex","_BloomTex","int ","uint","half","static","bool","mul(","float2x2","float3x3","float4x4","#"})Check(!shared.Contains(token),"shared code stays GLSL-compatible: no "+token.Trim());
            Check(!Regex.IsMatch(shared,@"(?<![\w.])\d+(?![\w.])"),"shared code uses float literals only");
            Check(!Regex.IsMatch(shared,@"\d[eE][+-]?\d"),"shared code has no exponent literals");
            Check(shared.Contains("c=min(max(c,float3(0.0,0.0,0.0)),float3(65504.0,65504.0,65504.0));")&&shared.Contains("return c*(min(m,limit)/max(m,0.0001));"),"firefly clamp: NaN, Inf and negatives out, brightest channel limited, hue kept");
            Check(shared.Contains("float q=clamp(b-t,0.0,2.0*kk);")&&shared.Contains("q=q*q/(4.0*kk);")&&shared.Contains("return c*(max(q,b-t-kk)/max(b,0.0001));"),"the knee matches NativeBloomRules.Weight");
            Check(shared.Contains("if(prefilter>0.5)c=BloomKnee(BloomSafe(c,curve.z),curve.x,curve.y);"),"each prefilter tap is made safe and thresholded");
            Check(shared.Contains("return SourceTap(uv)+BloomTent(bloomUv,d);")&&shared.Contains("return BloomShoulder(scene+BloomTent(bloomUv,d)*intensity);"),"accumulating up steps and an additive composite through the display shoulder");
            Check(shared.Contains("float d=0.9+0.1*(1.0-exp((0.9-m)*10.0));")&&shared.Contains("float w=clamp((m-2.0)*0.16,0.0,1.0);")&&shared.Contains("return m>0.9?t:c;")&&NativeBloomRules.ShoulderStart==.9f&&NativeBloomRules.ShoulderSoft==.1f&&NativeBloomRules.WhiteFrom==2f&&NativeBloomRules.WhiteRate==.16f,"the shader's shoulder is NativeBloomRules.Shoulder");
            string uv=Body(code,"float2 BloomUv(float2 uv)");
            Check(Regex.IsMatch(uv,@"#if UNITY_UV_STARTS_AT_TOP\s*if\(_MainTex_TexelSize\.y<0\.0\)uv\.y=1\.0-uv\.y;\s*#endif"),"the second texture flips when the source is stored top-down");
            Check(code.Contains("BloomUp(i.uv,BloomUv(i.uv),")&&code.Contains("BloomComposite(scene.rgb,BloomUv(i.uv),")&&!Regex.IsMatch(Body(code,"float4 FragPrefilter(v2f_img i)")+Body(code,"float4 FragDown(v2f_img i)"),"BloomUv"),"only the two-texture passes flip their second texture");
            Check(code.Contains("return float4(BloomComposite(scene.rgb,BloomUv(i.uv),_BloomTex_TexelSize.xy,_BloomParams.x),scene.a);"),"the composite keeps the frame's alpha");
            Check(code.Contains("Cull Off ZWrite Off ZTest Always")&&code.Contains("Fallback Off"),"full-screen state");
        }
    }
}
#endif
