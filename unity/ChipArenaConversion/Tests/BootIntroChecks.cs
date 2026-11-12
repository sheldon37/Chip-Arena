using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

// The logo intro at launch (Core NativeBootIntroRules; owner 2026-11-11: "Lets add this as an intro when starting the game (Use
// this time to load assets in). (This is also the new logo for the game)"; docs/BOOT-INTRO.md). When it plays, its timeline
// (skip, hold, crossfade), the clip in the project and the build, and where the game's loading meets it.
internal static class BootIntroChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Boot intro: "+what);count++;}
    private static bool Near(float a,float b,float e=1e-4f)=>Math.Abs(a-b)<=e;
    // Steps a flow for `seconds` at 60 fps with the clip's clock following (or held at `stopAt` from then on).
    private static void Run(NativeBootIntroFlow f,ref float clip,float seconds,bool loaded,bool ended=false,bool failed=false,float stopAt=float.MaxValue,bool started=true)
    {
        for(float t=0;t<seconds-1e-5f;t+=1f/60){if(started)clip=Math.Min(clip+1f/60,stopAt);f.Step(1f/60,started?clip:-1,ended,failed,loaded);}
    }
    public static void Run(string root)
    {
        // ---- when it plays ----
        var none=new string[0];var R=NativeBootIntroRules.Skip.Plays;
        Check(NativeBootIntroRules.Decide(false,false,none,false,false,false,false)==R,"the player plays it every launch");
        Check(NativeBootIntroRules.Decide(true,false,none,false,false,false,false)==NativeBootIntroRules.Skip.DedicatedServer,"never on a dedicated server");
        Check(NativeBootIntroRules.Decide(false,true,none,false,false,false,false)==NativeBootIntroRules.Skip.BatchMode&&NativeBootIntroRules.Decide(false,true,none,true,true,true,false)==NativeBootIntroRules.Skip.BatchMode,"never in batch mode, even when the Editor asks");
        foreach(var flag in new[]{"-nointro","-NoIntro","--nointro"})Check(NativeBootIntroRules.Decide(false,false,new[]{"Chip Arena.exe",flag},false,false,false,false)==NativeBootIntroRules.Skip.Argument,"never with "+flag);
        Check(NativeBootIntroRules.Decide(false,false,new[]{"x","-nointroX","nointro"},false,false,false,false)==R,"only the whole flag counts");
        Check(NativeBootIntroRules.Decide(false,false,none,true,false,false,false)==NativeBootIntroRules.Skip.EditorOff,"the Editor's Play mode does not play it unless asked");
        Check(NativeBootIntroRules.Decide(false,false,none,true,true,false,false)==R&&NativeBootIntroRules.Decide(false,false,none,true,false,true,false)==R,"asked for (always, or once) it plays in Play mode");
        Check(NativeBootIntroRules.Decide(false,false,none,true,true,false,true)==NativeBootIntroRules.Skip.EditorAutomation,"the capture tool's runs never see it, even with always on");
        Check(NativeBootIntroRules.Decide(false,false,none,true,false,true,true)==R,"a tool run that asks for it (once) sees it");
        Check(NativeBootIntroRules.Decide(false,false,new[]{"-nointro"},true,true,true,false)==NativeBootIntroRules.Skip.Argument,"-nointro wins over the Editor's ask");

        // ---- the letterbox ----
        void Fit(float w,float h,float ex,float ey,float ew,float eh,string what){NativeBootIntroRules.Fit(w,h,out float x,out float y,out float fw,out float fh);Check(Near(x,ex,.01f)&&Near(y,ey,.01f)&&Near(fw,ew,.01f)&&Near(fh,eh,.01f),what+" ("+x+","+y+" "+fw+"x"+fh+")");}
        Fit(1920,1080,0,0,1920,1080,"16:9 fills the screen");Fit(3440,1440,440,0,2560,1440,"21:9 pillarboxes");Fit(1024,768,0,96,1024,576,"4:3 letterboxes");Fit(1600,900,0,0,1600,900,"the default window fills");Fit(0,0,0,0,0,0,"no screen, no picture");

        // ---- the timeline ----
        float clip=0;var f=new NativeBootIntroFlow();
        Run(f,ref clip,.2f,true,started:false);Check(f.State==NativeBootIntroFlow.Phase.Playing&&f.Blocking&&f.Video==0&&!f.LogoShown&&f.Cover==1,"black until the clip's first frame, game held");
        clip=0;Run(f,ref clip,.2f,true);Check(f.Video==1&&f.Cover==1&&f.Indicator==0&&f.Sound==1&&!f.Settled,"the clip plays opaque, at full sound");
        Check(!f.Skip()&&!f.Skipped,"no skip in the first half second");
        Run(f,ref clip,NativeBootIntroRules.SettledFrom-clip-.05f,true);Check(!f.Settled&&f.State==NativeBootIntroFlow.Phase.Playing&&f.Blocking,"ready early, the clip plays its animation out");
        Run(f,ref clip,.1f,true);
        Check(f.Settled&&f.State==NativeBootIntroFlow.Phase.Fading&&!f.Blocking&&f.HoldAt<0&&f.Video==1&&Near(f.Up-f.FadeAt,0,.06f),"its animation finished (7.6 s), ready: the crossfade starts that frame, input to the menu (owner: \"transition a lot quicker after animation finishes\")");
        float before=f.Cover;Run(f,ref clip,.2f,true);Check(f.Cover<before&&f.Cover>0&&f.Sound<1,"the crossfade fades the picture and the still logo's sound");
        Run(f,ref clip,.21f,true);Check(f.State==NativeBootIntroFlow.Phase.Done&&f.Cover==0,"done after "+NativeBootIntroRules.Crossfade+" s, long before the clip's end ("+NativeBootIntroRules.ClipSeconds+" s)");
        // Not ready at 7.6 s: the clip plays on, and the crossfade comes the moment the game is.
        f=new NativeBootIntroFlow();clip=0;Run(f,ref clip,NativeBootIntroRules.SettledFrom+.5f,false);Check(f.Settled&&f.State==NativeBootIntroFlow.Phase.Playing,"settled but loading: the clip plays on");
        f.Step(1f/60,clip+=1f/60,false,false,true);Check(f.State==NativeBootIntroFlow.Phase.Fading,"then the crossfade the moment the game is ready");

        // Skipped while the game still loads: the logo holds with the indicator, then the crossfade.
        f=new NativeBootIntroFlow();clip=0;Run(f,ref clip,1,false);
        Check(f.Skip()&&f.Skipped&&!f.Skip(),"a press after half a second skips, once");
        f.Step(1f/60,clip,false,false,false);Check(f.State==NativeBootIntroFlow.Phase.Holding&&f.Blocking&&f.LogoShown&&f.Logo==1&&f.Video>0&&f.Settled,"skipped, not loaded: the final logo holds (the clip blending out over it)");
        Run(f,ref clip,NativeBootIntroRules.SkipBlend,false);Check(f.Video==0&&f.Sound==0,"the clip and its sound gone within "+NativeBootIntroRules.SkipBlend+" s");
        Check(f.Indicator==0,"no indicator at once");
        Run(f,ref clip,NativeBootIntroRules.IndicatorDelay+NativeBootIntroRules.IndicatorFade,false);Check(Near(f.Indicator,1,.02f)&&f.State==NativeBootIntroFlow.Phase.Holding,"the loading indicator after a moment's hold");
        Check(!f.Skip(),"a press while holding does nothing");
        Run(f,ref clip,5,false);Check(f.State==NativeBootIntroFlow.Phase.Holding,"it holds as long as the game loads");
        f.Step(1f/60,clip,false,false,true);Check(f.State==NativeBootIntroFlow.Phase.Fading&&!f.Blocking,"loaded: the crossfade");
        Run(f,ref clip,1,true);Check(f.State==NativeBootIntroFlow.Phase.Done,"then done");

        // A clip that ends before the game is ready holds its last frame (not the logo image) with the indicator.
        f=new NativeBootIntroFlow();clip=0;Run(f,ref clip,NativeBootIntroRules.ClipSeconds,false);f.Step(1f/60,clip,true,false,false);
        Check(f.State==NativeBootIntroFlow.Phase.Holding&&f.Video==1&&!f.LogoShown&&f.Sound==1,"ended, loading: the clip's own last frame holds");
        Run(f,ref clip,1,false,ended:true);Check(f.Indicator>.9f,"with the indicator");

        // A clip that never shows a frame (no decoder, a missing file) or fails: the logo instead.
        f=new NativeBootIntroFlow();clip=0;Run(f,ref clip,NativeBootIntroRules.PrepareTimeout+.1f,false,started:false);
        Check(f.VideoFailed&&f.LogoShown&&f.Logo>.99f&&f.Video==0&&f.State==NativeBootIntroFlow.Phase.Holding,"no frame in "+NativeBootIntroRules.PrepareTimeout+" s: the logo holds while it loads");
        f=new NativeBootIntroFlow();clip=0;Run(f,ref clip,.5f,true);f.Step(1f/60,clip,false,true,true);Check(f.VideoFailed&&f.State==NativeBootIntroFlow.Phase.Fading,"a player error ends it (ready: the crossfade)");
        // A clock that stops short of the end.
        f=new NativeBootIntroFlow();clip=0;Run(f,ref clip,3,true,stopAt:2);Check(f.State==NativeBootIntroFlow.Phase.Playing,"a short stall is waited out");
        Run(f,ref clip,NativeBootIntroRules.StallTimeout,true,stopAt:2);Check(f.VideoEnded&&f.State!=NativeBootIntroFlow.Phase.Playing,"a clock stopped "+NativeBootIntroRules.StallTimeout+" s counts as the end");
        // A first frame after a long hitch neither skips phases nor blends at once.
        f=new NativeBootIntroFlow();f.Step(float.NaN,-1,false,false,false);f.Step(-1,-1,false,false,false);Check(f.Up==0,"a bad dt counts as none");
        Check(Near(NativeBootIntroRules.SkipAfter,.5f)&&Near(NativeBootIntroRules.Crossfade,.4f)&&Near(NativeBootIntroRules.SettledFrom,7.6f)&&Near(NativeBootIntroRules.Gain,.72f),"pinned timings and level");
        // The menu under the curtain counts as settled after 30 frames in a row under 20 ms, or 1.5 s after it was built.
        {
            Check(NativeBootIntroRules.SmoothFrames==30&&Near(NativeBootIntroRules.SmoothFrame,.02f)&&Near(NativeBootIntroRules.SettleCap,1.5f),"pinned settle");
            int smooth=0;bool settled=false;float since=0;for(int i=0;i<29;i++){since+=1/144f;settled|=NativeBootIntroRules.Settled(ref smooth,1/144f,since);}
            Check(!settled,"29 smooth frames are not enough");Check(NativeBootIntroRules.Settled(ref smooth,1/144f,since+1/144f),"the 30th settles it");
            smooth=0;since=0;for(int i=0;i<29;i++){since+=1/144f;NativeBootIntroRules.Settled(ref smooth,1/144f,since);}
            Check(!NativeBootIntroRules.Settled(ref smooth,.05f,since+.05f)&&smooth==0,"a 50 ms frame starts the count again");
            smooth=0;Check(!NativeBootIntroRules.Settled(ref smooth,.03f,1.49f)&&NativeBootIntroRules.Settled(ref smooth,.03f,1.5f),"a menu that never runs smoothly lets the clip start 1.5 s after it was built");
        }

        // ---- the clip, the logo and the project ----
        string U(string rel)=>Path.Combine(root,rel);string Text(string rel)=>File.ReadAllText(U(rel));
        string clipPath=U("Assets/StreamingAssets/"+NativeBootIntroRules.Clip);var bytes=File.ReadAllBytes(clipPath);
        string sha;using(var s=SHA256.Create())sha=string.Concat(s.ComputeHash(bytes).Select(b=>b.ToString("x2")));
        Check(bytes.LongLength==NativeBootIntroRules.ClipBytes&&sha==NativeBootIntroRules.ClipSha256,"the clip is the committed re-encode ("+bytes.Length+" bytes, "+sha+")");
        int moov=IndexOf(bytes,"moov"),mdat=IndexOf(bytes,"mdat");
        Check(System.Text.Encoding.ASCII.GetString(bytes,4,4)=="ftyp"&&moov>0&&mdat>0&&moov<mdat,"an MP4 with its index first (fast start)");
        Check(IndexOf(bytes,"avc1")>0&&IndexOf(bytes,"mp4a")>0,"H.264 picture and AAC sound");
        Check(bytes.Length<8*1024*1024,"small for the repository (under 8 MB)");
        string Guid(string meta){var m=Regex.Match(Text(meta),"^guid: ([0-9a-f]{32})$",RegexOptions.Multiline);return m.Success?m.Groups[1].Value:null;}
        var metas=new[]{"Assets/StreamingAssets.meta","Assets/StreamingAssets/"+NativeBootIntroRules.Clip+".meta","Assets/ChipArena/Resources/"+NativeBootIntroRules.Logo+".jpg.meta"};
        Check(metas.All(m=>File.Exists(U(m))&&Guid(m)!=null)&&metas.Select(Guid).Distinct().Count()==3,"the clip, its folder and the logo carry their own metas");
        Check(Text(metas[0]).Contains("folderAsset: yes")&&Text(metas[1]).Contains("DefaultImporter:"),"StreamingAssets is copied into the build as it is");
        var logoMeta=Text(metas[2]);Check(logoMeta.Contains("enableMipMap: 0")&&logoMeta.Contains("sRGBTexture: 1")&&logoMeta.Contains("textureCompression: 0")&&logoMeta.Contains("wrapU: 1"),"the logo: no mips, sRGB, uncompressed, clamped");
        var logo=File.ReadAllBytes(U("Assets/ChipArena/Resources/"+NativeBootIntroRules.Logo+".jpg"));Check(logo.Length>1000&&logo.Length<400*1024&&logo[0]==0xFF&&logo[1]==0xD8,"the logo is a JPEG (the clip's last frame)");
        foreach(var m in Directory.GetFiles(U("Assets"),"*.meta",SearchOption.AllDirectories).Where(p=>!p.Contains("LocalAssets")))
        {var g=Regex.Match(File.ReadAllText(m),"^guid: ([0-9a-f]{32})$",RegexOptions.Multiline).Groups[1].Value;if(metas.Any(x=>Path.GetFullPath(U(x))!=Path.GetFullPath(m)&&Guid(x)==g))Check(false,"GUID shared with "+m);}
        Check(Text("Packages/manifest.json").Contains("\"com.unity.modules.video\": \"1.0.0\""),"the Video module is in the project (VideoPlayer)");
        Check(Text("tools/asset-lock.json").Contains("\"path\": \"Assets/StreamingAssets/BootIntro.mp4\",\n      \"bytes\": "+NativeBootIntroRules.ClipBytes),"the clip's receipt is in the asset lock (the source package's size rule)");
        Check(Text("Assets/ChipArena/Editor/NativeBootIntroBuild.cs").Contains("Verify(Path.Combine(data,\"StreamingAssets\",NativeBootIntroRules.Clip),\"the player\")")&&Text("Assets/ChipArena/Editor/NativeBootIntroBuild.cs").Contains("public void OnPreprocessBuild(BuildReport report){Verify(ProjectClip,\"the project\");}"),
            "every build checks the clip before and the player's copy after");
        var credits=File.ReadAllText(Path.Combine(root,"../../docs/ASSET-CREDITS.md"));Check(credits.Contains("## Logo intro")&&credits.Contains(NativeBootIntroRules.ClipSha256),"credited as the owner's logo intro");

        // ---- the wiring (source text; Unity cannot run here) ----
        string game=Text("Assets/ChipArena/Runtime/ConversionGame.cs"),load=Text("Assets/ChipArena/Runtime/NativeBootLoad.cs"),intro=Text("Assets/ChipArena/Runtime/NativeBootIntro.cs");
        string bank=Text("Assets/ChipArena/Runtime/NativeBankLoad.cs"),source=Text("Assets/ChipArena/Runtime/NativeSourceMotion.cs"),scene=Text("Assets/ChipArena/Runtime/NativeSceneMotion.cs");
        int account=game.IndexOf("InitializeAccount();Application.runInBackground=true;",StringComparison.Ordinal),start=game.IndexOf("if(!BootIntroStart()){",StringComparison.Ordinal),
            sync=game.IndexOf("long motionAt=System.Diagnostics.Stopwatch.GetTimestamp();NativeSourceMotion.Prewarm();NativeSceneMotion.Prewarm();",StringComparison.Ordinal),
            rest=game.IndexOf("nativeAudio=gameObject.AddComponent<NativeAudio>();presentation=gameObject.AddComponent<ArenaPresentation>();presentation.Initialize(false);",StringComparison.Ordinal),
            updater=game.IndexOf("ApplySettings();InitializeUpdater();",StringComparison.Ordinal),dedicated=game.IndexOf("if(InitializeDedicated())return;",StringComparison.Ordinal),
            prefetch=game.IndexOf("BootPrefetch();\n                var asset=Resources.Load<TextAsset>(\"GameContent\");",StringComparison.Ordinal);
        Check(prefetch>0&&dedicated>prefetch&&account>dedicated&&start>account&&sync>start&&rest>sync&&updater>rest,"Awake's order: the intro's workers (decoding only), content, dedicated, prefs, account, then the intro or, without it, the banks at once as before, then audio, presentation, cameras, settings, updater");
        Check(load.Contains("Decide(Array.IndexOf(args,\"--ca-server\")>=0,Application.isBatchMode,args,Application.isEditor,always,once,automated)")&&load.Contains("if(!bootWanted||dedicated)return false;"),"the workers start only when the intro will play (the dedicated server's flag decides before Awake knows)");
        Check(Text("Assets/ChipArena/Runtime/NativeSourceModels.cs").Contains("var data=Prefetched()??JsonUtility.FromJson<SourceModelSet>(Decode(\"SourceModels\"));")&&load.Contains("NativeSourceMotion.PrewarmAsync();NativeSceneMotion.PrewarmAsync();NativeSourceModels.PrefetchAsync();")&&load.Contains("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]\n        private static void Early()=>Decide();"),
            "the source models are still built in presentation.Initialize, from their JSON decoded on a worker (else decoded there as before)");
        Check(game.Contains("NativeSceneMotion.Count,HitchMs(motionAt));}\n"),"without the intro the banks load and log in Awake exactly as before");
        Check(game.Contains("AccountXpTick();\n            // The logo intro holds the game (no input) while it loads under it (NativeBootLoad.cs).\n            if(BootIntroHolds())return;\n            if(padInputCaptured)"),"Update: the network, deployment and account ticks run, then the intro holds all input");
        Check(game.Contains("private void LateFrame()\n        {\n            // Under the logo intro the menu builds only once the loading allows it (NativeBootLoad.cs).\n            if(BootIntroFrame())return;"),"LateFrame: nothing draws or reads a bank until the loading allows it");
        Check(game.Contains("private void OnGUI()\n        {\n            // Under the logo intro the menu's GUI draws only once the loading allows it (NativeBootLoad.cs).\n            if(BootIntroGui())return;"),"OnGUI: the menu's GUI waits first");
        Check(Regex.IsMatch(load,@"enum BootStage\{Banks,Assets,Scene,Gui,Ready\}")&&load.Contains("if(bootStage<BootStage.Scene)return true;")&&load.Contains("if(bootStage<BootStage.Gui)return true;"),
            "the order: banks, assets, the menu's scene, its GUI, ready: all under the curtain, before the clip");
        Check(load.Contains("bool sourceIn=NativeSourceMotion.Ready,sceneIn=NativeSceneMotion.Ready;if(!sourceIn||!sceneIn)return;")&&load.Contains("NativeSourceMotion.Prewarm();NativeSceneMotion.Prewarm();")&&load.Contains("MOTION_BANKS_READY source={0} scene={1}"),"the banks are joined (and logged) before the menu may read them");
        Check(load.Contains("Func<bool> built=()=>bootStage==BootStage.Ready&&bootSettled||error.Length>0||bootFailed;")&&intro.Contains("if(canPlay!=null&&!canPlay())return;")&&intro.Contains("player.Play();started=true;startedAt=Ms;Application.backgroundLoadingPriority=loadingPriority;"),
            "the clip starts only once the menu is built under the curtain (nothing heavy while it plays or after it), loading at full speed until then");
        Check(load.Contains("if(bootGuiFrames>=2){BootStep(\"gui\");bootStage=BootStage.Ready;bootBuiltAt=BootMs;}return;")&&load.Contains("if(!bootSettled&&NativeBootIntroRules.Settled(ref bootSmooth,Time.unscaledDeltaTime,(float)(BootMs-bootBuiltAt)/1000)){bootSettled=true;BootStep(\"settled\");}return;"),
            "and once the built menu runs smoothly under the curtain (its first frames after the build land there, not on the clip's opening)");
        Check(load.Contains("{if(bootGuiFrames==0)NativeUi.WarmFaces();bootGuiFrames++;bootWarmFrame=Time.frameCount;}")&&Text("Assets/ChipArena/Runtime/NativeUi.cs").Contains("foreach(bool m in new[]{false,true})for(int f=0;f<4;f++)"),
            "every face the UI draws is laid out once under the curtain (the fight HUD's faces read their tables there, not at the countdown)");
        Check(load.Contains("()=>bootStage==BootStage.Ready&&BootMs>=bootDevHold*1000||error.Length>0")&&load.Contains("if(bootIntro==null||error.Length>0)return false;"),"a start-up error ends the hold and draws through the crossfade, never under a held intro");
        Check(load.Contains("if(noticeText.Length>0&&noticeLast>=bootNoticeFrom){noticeShown=noticeLast=Time.unscaledTime;}"),"a notice raised under the intro shows after it, for its full time");
        // Without Unity's splash the window stayed unpainted (white) through Awake: the intro's black curtain goes up first and
        // the game comes a frame later, once that black frame is on screen; a curtain the game never arms comes down at once.
        Check(game.Contains("private static void Boot(){if(FindFirstObjectByType<ConversionGame>()==null&&!BootCurtain())new GameObject(\"Chip Arena Native Conversion\").AddComponent<ConversionGame>();}"),"the game is made at once unless the curtain goes up first");
        Check(load.Contains("NativeBootPrefetch.Decide();if(!NativeBootPrefetch.Wanted||NativeBootIntro.Current!=null)return false;")&&load.Contains("NativeBootIntro.Curtain();new GameObject(\"Boot defer\").AddComponent<NativeBootDefer>();return true;"),"only when the intro will play");
        Check(load.Contains("if(++updates<2)return;")&&load.Contains("var curtain=NativeBootIntro.Current;if(curtain!=null&&!curtain.Armed)curtain.Dismiss();"),"the game a frame later; an unarmed curtain lifted (Awake failed or chose no intro: its error shows)");
        Check(intro.Contains("if(finished||!armed)return;")&&load.Contains("NativeBootIntro.Current!=null&&!NativeBootIntro.Current.Armed?NativeBootIntro.Current.Arm(level,ready,built):NativeBootIntro.Begin(level,ready,built)"),"the curtain neither plays nor runs its clock until Awake arms it");
        Check(Text("ProjectSettings/ProjectSettings.asset").Contains("\n  m_ShowUnitySplashScreen: 0\n"),"no Unity splash screen (owner: \"Yes, remove it\")");
        Check(load.Contains("nativeAudio.MenuHeld=NativeBootIntro.Blocking;")&&Text("Assets/ChipArena/Runtime/NativeAudio.cs").Contains("Track(\"music/menu-afterglow\",Menu*MenuTrackGain,MenuHeld);return;}"),"the menu song waits for the crossfade");
        Check(load.Contains("prefs.sound?Mathf.Clamp01(prefs.menuMusic)*NativeBootIntroRules.Gain:0"),"its sound follows Mute audio and the Menu Music slider");
        Check(load.Contains("UnityEditor.SessionState.GetString(NativeBootIntro.EditorAutomationKey,\"\")")&&intro.Contains("EditorAutomationKey=\"bozo-play-job\""),"the Editor knows the capture tool's runs");
        foreach(var key in new[]{"KeyCode.Escape","KeyCode.Return","KeyCode.KeypadEnter","KeyCode.Space","GetMouseButtonDown(0)"})Check(intro.Contains(key),"skips on "+key);
        Check(load.Contains("if(PadBack||PadDown(PadAction.Jump)||PadDown(PadAction.Pause))bootIntro.Press();"),"and on a pad");
        Check(intro.Contains("return UnityEngine.Rendering.SplashScreen.isFinished;")&&intro.Contains("player.audioOutputMode=VideoAudioOutputMode.AudioSource;")&&intro.Contains("Path.Combine(Application.streamingAssetsPath,NativeBootIntroRules.Clip)"),
            "the clip plays by URL from StreamingAssets after Unity's splash, its sound through an AudioSource");
        Check(intro.Contains("GUI.depth=-1000;")&&intro.Contains("if(flow.Blocking&&(e.type==EventType.MouseDown"),"drawn over every GUI; no click or key reaches the menu under it");
        // The worker touches no Unity object: the resource and its bytes are read on the main thread.
        Check(bank.Contains("byte[] raw=asset.bytes;")&&bank.Contains("task=Task.Run(()=>build(NativeSourceModels.Unwrap(resource,Text(raw)),Begin));")&&!Regex.IsMatch(Text("Assets/ChipArena/Runtime/NativeSourceModels.cs").Split(new[]{"public static string Unwrap("},StringSplitOptions.None)[1].Split('\n')[1],@"Resources\.|UnityEngine\.Object"),
            "the banks' workers run plain C# only (the resource is read on the main thread)");
        foreach(var (name,text,check) in new[]{("NativeSourceMotion",source,"throw new InvalidOperationException(\"Invalid motion clip \"+c.key);clips.Add(c.key,c);}"),("NativeSceneMotion",scene,"throw new InvalidOperationException(\"Invalid cosmetic scene: \"+c.key);clips.Add(c.key,c);}")})
        {
            Check(text.Contains("if(loading!=null){var load=loading;loading=null;try{clips=load.Join();}catch(Exception){clips=load.Partial;throw;}return;}")&&text.Contains(check),name+": every read waits for a pending load; the same checks run on the worker");
            Check(text.Contains("public static void Prewarm()=>Ensure();"),name+": Prewarm still loads at once");
        }
        Check(source.Contains("NativeIntroMotion.Prepare(c.key.Substring(6),c.values,c.frames,c.duration);\n            return clips;}"),"the weapon intros' plans are built with the clips, before the bank is published");
        Sculpts(root);
        FirstPlay(root);
        Console.WriteLine("PASS boot intro: "+count+" checks");
    }
    // ---- the sculpts made on a worker (Core NativeBootSculpts) ----
    private static void Add(IncrementalHash h,System.Collections.Generic.List<System.Numerics.Vector3> v){foreach(var p in v){h.AppendData(BitConverter.GetBytes(p.X));h.AppendData(BitConverter.GetBytes(p.Y));h.AppendData(BitConverter.GetBytes(p.Z));}}
    private static void Add(IncrementalHash h,System.Collections.Generic.List<System.Numerics.Vector4> v){foreach(var p in v){h.AppendData(BitConverter.GetBytes(p.X));h.AppendData(BitConverter.GetBytes(p.Y));h.AppendData(BitConverter.GetBytes(p.Z));h.AppendData(BitConverter.GetBytes(p.W));}}
    private static void Add(IncrementalHash h,System.Collections.Generic.List<int> v){foreach(var p in v)h.AppendData(BitConverter.GetBytes(p));}
    private static void Add(IncrementalHash h,WeaponMesh m)
    {
        if(m==null){h.AppendData(new byte[]{0});return;}
        foreach(var s in m.Sections){h.AppendData(BitConverter.GetBytes(s.Bone));h.AppendData(BitConverter.GetBytes((int)s.Stretch));Add(h,s.Positions);Add(h,s.Normals);Add(h,s.Colors);Add(h,s.Emission);Add(h,s.Indices);foreach(var d in s.Data){h.AppendData(BitConverter.GetBytes(d.X));h.AppendData(BitConverter.GetBytes(d.Y));}}
    }
    private static string Hash(object o)
    {
        using var h=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        switch(o)
        {
            case null:h.AppendData(new byte[]{0});break;
            case HordeModel m:Add(h,m.Mesh);Add(h,m.Jaw);break;
            case WeaponMesh m:Add(h,m);break;
            case SheldonSection[] ss:foreach(var s in ss){h.AppendData(System.Text.Encoding.UTF8.GetBytes(s.Part??""));h.AppendData(BitConverter.GetBytes(s.Bone));Add(h,s.Positions);Add(h,s.Normals);Add(h,s.Colors);Add(h,s.Emission);Add(h,s.Indices);}break;
            default:throw new Exception("Boot intro: an unknown sculpt type "+o.GetType().Name);
        }
        return Convert.ToHexString(h.GetHashAndReset());
    }
    private static void Sculpts(string root)
    {
        var jobs=NativeBootSculpts.Jobs().ToList();var keys=jobs.Select(j=>j.key).ToList();
        Check(jobs.Count>100&&keys.Distinct().Count()==keys.Count,"the boot sculpts: "+jobs.Count+" jobs, every key once");
        var want=jobs.ToDictionary(j=>j.key,j=>Hash(j.make()));
        // The worker beside a main thread running the same sculpts (the runtime's other builders share the kit): identical results.
        NativeBootSculpts.Reset();NativeBootSculpts.Start();var beside=jobs.ToDictionary(j=>j.key,j=>Hash(j.make()));
        int same=0;foreach(var (key,make) in jobs){var got=NativeBootSculpts.Take<object>(key,()=>throw new Exception("Boot intro: no sculpt for "+key));if(Hash(got)==want[key]&&beside[key]==want[key])same++;}
        Check(same==jobs.Count,"the worker's sculpts are the single-threaded ones ("+same+"/"+jobs.Count+")");
        Check(NativeBootSculpts.Taken==jobs.Count&&NativeBootSculpts.Left==0&&NativeBootSculpts.Made==jobs.Count,"each made once and handed over once");
        bool rebuilt=false;var again=NativeBootSculpts.Take(keys[0],()=>{rebuilt=true;return (object)"x";});
        Check(rebuilt&&(string)again=="x"&&NativeBootSculpts.Fallbacks==1,"a key asked twice (a second bank) is sculpted by its caller");
        NativeBootSculpts.Reset();bool direct=false;NativeBootSculpts.Take(keys[0],()=>{direct=true;return (object)"y";});Check(direct&&!NativeBootSculpts.Started,"without the worker every caller sculpts as before");
        NativeBootSculpts.Reset();
        // Every builder asks for its sculpt by the list's key, with the very call it used to make.
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
        foreach(var (file,text) in new[]{
            ("NativeHordeKindModels.cs","var built=NativeBootSculpts.Take(NativeBootSculpts.Horde(kind,look,tier),()=>NativeHordeModels.Build(kind,look,tier));"),
            ("NativeIronworksKindsPresentation.cs","NativeBootSculpts.Take(NativeBootSculpts.Prop(p,t),()=>NativeHordeModels.IronworksProp(p,t))"),
            ("NativeIronworksKindsPresentation.cs","NativeBootSculpts.Take(NativeBootSculpts.Crust(l,t),()=>NativeHordeModels.IronworksCrust(l,t))"),
            ("NativeIronworksKindsPresentation.cs","NativeBootSculpts.Take(NativeBootSculpts.Cream(l,t),()=>NativeHordeModels.Build(NativeHordeKinds.Brute,l,t,NativeIronworksKinds.BruteSeam)).Mesh"),
            ("NativeStationKindsPresentation.cs","var built=NativeBootSculpts.Take(NativeBootSculpts.StationProp(prop,tier),()=>NativeHordeModels.BuildStationProp(prop,tier));"),
            ("NativeRobotHelmPresentation.cs","HelmParts(NativeBootSculpts.Take(NativeBootSculpts.Helm,NativeRobotHelm.Helm),material,\"Robot helmet\")"),
            ("NativeRobotHelmPresentation.cs","HelmParts(NativeBootSculpts.Take(NativeBootSculpts.HalfMask,NativeRobotHelm.Half),material,\"Robot half mask\")"),
            ("NativeRobotHelmPresentation.cs","HelmParts(NativeBootSculpts.Take(NativeBootSculpts.BrokenHelm(b),()=>NativeRobotHelm.Broken(b)),material,"),
            ("NativeWeaponSkinModels.cs","var built=NativeBootSculpts.Take(NativeBootSculpts.Weapon(spec,tier),()=>NativeWeaponModels.Build(spec,tier));"),
            ("NativeWeaponSkinModels.cs","var loose=NativeBootSculpts.Take(NativeBootSculpts.Loose(spec),()=>NativeWeaponModels.Loose(spec));")})
            Check(R(file).Contains(text),file+" takes its sculpt from the worker");
        var skins=R("NativeWeaponSkinModels.cs");Check(skins.Contains("System.Linq.Enumerable.Append(System.Linq.Enumerable.Append(System.Linq.Enumerable.Append(NativeWeaponSkins.Weapons,NativeBombs.Spec),NativeTopDog.Spec),NativeGlaive.Spec),new[]{NativeKatana.Spec,NativeFangs.Spec})")&&NativeBootSculpts.WeaponSpecs().SequenceEqual(NativeWeaponSkins.Weapons.Concat(new[]{NativeBombs.Spec,NativeTopDog.Spec,NativeGlaive.Spec,NativeKatana.Spec,NativeFangs.Spec})),
            "the worker sculpts the weapons WeaponSkins builds");
        var load=R("NativeBootLoad.cs");Check(load.Contains("NativeBootSculpts.Start();")&&load.Contains("private static void Reset(){Decided=Wanted=false;NativeBootSculpts.Reset();}"),"the worker starts with the banks', before the splash, and only then");
    }
    // ---- the first play (owner 2026-11-12: "Greatsword / DEV Greatsword VERY bad lag on intro. Seems to happen only first time it
    // plays?"): the pack entrances planned on a worker at start-up, and the arena's own frame behind the stage's loading screen ----
    private static NativePackIntro Copy(NativePackIntro p)=>new NativePackIntro(p.Style,p.Track,p.TwoHand?new[]{p.Support.X,p.Support.Y,p.Support.Z,p.SupportAlways?1f:0f}:null,p.Hit,p.PlantsLeft,p.PlantsRight,p.Breathe);
    private static bool Same(CapturedRig.Pose a,CapturedRig.Pose b){for(int i=0;i<a.Position.Length;i++)if(a.Position[i]!=b.Position[i]||a.Rotation[i]!=b.Rotation[i])return false;return true;}
    private static System.Collections.Generic.Dictionary<string,NativeEntranceClip> Clips(string root)
    {
        using var envelope=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/SourceMotion.json")));
        using var bytes=new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("data").GetString()));using var zip=new GZipStream(bytes,CompressionMode.Decompress);using var reader=new StreamReader(zip);
        using var doc=JsonDocument.Parse(reader.ReadToEnd());var map=new System.Collections.Generic.Dictionary<string,NativeEntranceClip>();
        foreach(var c in doc.RootElement.GetProperty("clips").EnumerateArray())
        {
            string key=c.GetProperty("key").GetString();if(!key.StartsWith("intro-weapon-"))continue;
            map[key]=new NativeEntranceClip(c.GetProperty("values").EnumerateArray().Select(v=>(float)v.GetDouble()).ToArray(),c.GetProperty("roots").EnumerateArray().Select(v=>(float)v.GetDouble()).ToArray(),c.GetProperty("frames").GetInt32(),(float)c.GetProperty("duration").GetDouble());
        }
        return map;
    }
    private static void FirstPlay(string root)
    {
        // The blade clearance is stateless per call: a fresh one gives the shared one's blade, whatever either solved before.
        var gs=NativePackIntros.All.First(p=>p.Style=="weapon-greatsword");var set=MeleeSet.For("greatsword");var probe=new CapturedRig.Pose();var rig=new MeleeRig();
        int cleared=0,samples=0;
        for(float t=0;t<=4.8f;t+=.1f)
        {
            gs.Captured(t,probe,out _,out _);rig.ReadPose(probe);var g0=new System.Numerics.Vector3(.1f,-.2f,.35f);var b0=System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(.2f,.9f,-.3f));var e0=System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(b0,System.Numerics.Vector3.UnitY));
            var (ga,ba,ea)=(g0,b0,e0);MeleeClearance.Shared.Clear(rig,ref ga,ref ba,ref ea,-set.Fit.Pommel,set.BladeEnd,true,set.Fit.HalfWidth,false);
            var (gb,bb,eb)=(g0,b0,e0);new MeleeClearance().Clear(rig,ref gb,ref bb,ref eb,-set.Fit.Pommel,set.BladeEnd,true,set.Fit.HalfWidth,false);
            samples++;if(ga==gb&&ba==bb&&ea==eb)cleared++;
        }
        Check(cleared==samples,"a pack's own blade clearance is the shared one's ("+cleared+"/"+samples+")");
        // The body push on its caller's scratch is the shared scratch's.
        int pushed=0;var scratch=new NativeIntroMotion.PushScratch();
        for(float t=0;t<=4.8f;t+=.1f){gs.Captured(t,probe,out _,out _);probe.World();foreach(bool left in new[]{false,true})if(NativeIntroMotion.BodyPush(gs.Style,probe,left)==NativeIntroMotion.BodyPush(gs.Style,probe,left,scratch))pushed++;}
        Check(pushed==samples*2,"the body push on a scratch of its own is the shared one's");
        // Every pack planned on a worker, while this thread plans copies of them and runs the shared clearance and body push (as the
        // gameplay and the bank's worker do), poses exactly as one planned on this thread alone.
        var packs=NativePackIntros.All;var alone=packs.Select(Copy).ToArray();var worked=packs.Select(Copy).ToArray();var beside=new[]{Copy(packs.First(p=>p.Style=="weapon-pistol"))};
        var pose=new CapturedRig.Pose();foreach(var p in alone)p.Pose(0,pose,out _,out _);
        var worker=System.Threading.Tasks.Task.Run(()=>{var q=new CapturedRig.Pose();foreach(var p in worked)p.Pose(0,q,out _,out _);});
        while(!worker.IsCompleted){foreach(var p in beside)p.Pose(1.7f,pose,out _,out _);pose.World();NativeIntroMotion.BodyPush(gs.Style,pose,false);rig.ReadPose(pose);var (g,b,e)=(System.Numerics.Vector3.Zero,System.Numerics.Vector3.UnitY,System.Numerics.Vector3.UnitX);MeleeClearance.Shared.Clear(rig,ref g,ref b,ref e,-.27f,1.6f);}
        worker.Wait();
        int same=0,poses=0;var a=new CapturedRig.Pose();var w=new CapturedRig.Pose();
        for(int i=0;i<packs.Length;i++)for(int k=0;k<=144;k++){float t=k/30f;alone[i].Pose(t,a,out var ra,out var qa);worked[i].Pose(t,w,out var rw,out var qw);poses++;if(ra==rw&&qa==qw&&Same(a,w))same++;}
        Check(packs.Length==10&&same==poses,"the ten pack entrances planned on a worker pose as planned alone ("+same+"/"+poses+")");
        // The director's warm: each pack signature bound to its clip on this thread, built on its worker under its PoseLock;
        // afterwards its camera and pose cost nothing more (the first frame builds nothing).
        var clips=Clips(root);var task=NativeEntranceDirector.Warm(style=>clips.TryGetValue("intro-"+style,out var c)?c:null,out int built);double ms=task.Result;
        Check(built==packs.Length&&ms>0,"the warm builds the "+packs.Length+" pack signatures on a worker ("+built+", "+ms.ToString("F0")+" ms)");
        foreach(var p in packs)
        {
            var script=NativeEntranceDirector.Script(p.Style);var clock=System.Diagnostics.Stopwatch.StartNew();script.Prepare();script.Camera(1.2f,out var view);script.Pose(1.8f,pose,out _,out _);
            Check(clock.Elapsed.TotalMilliseconds<20&&float.IsFinite(view.Eye.X),p.Style+" is built before its first frame ("+clock.Elapsed.TotalMilliseconds.ToString("F1")+" ms)");
        }
        // Left as the other groups find them: unbound (EntranceDirectorChecks binds its own clips).
        foreach(var p in packs)NativeEntranceDirector.Script(p.Style).Bind(null);
        Check(packs.All(p=>!NativeEntranceDirector.Script(p.Style).Ready),"the scripts are unbound again");
        // The wiring (source text).
        string R(string f)=>File.ReadAllText(Path.Combine(root,f));
        string game=R("Assets/ChipArena/Runtime/ConversionGame.cs"),load=R("Assets/ChipArena/Runtime/NativeBootLoad.cs"),intros=R("Assets/ChipArena/Runtime/NativeIntros.cs"),stage=R("Assets/ChipArena/Runtime/NativeStageLoadingUI.cs"),pack=R("Assets/ChipArena/Core/NativeEntrancePack.cs");
        Check(load.Contains("BootStep(\"banks\");EntranceWarmBegin();bootStage=BootStage.Assets;return;")&&game.Contains("NativeSceneMotion.Count,HitchMs(motionAt));}\n                // Without the intro the pack entrances' plans build on a worker from here (under it, from its Banks step).\n                if(bootIntro==null)EntranceWarmBegin();\n                nativeAudio=gameObject.AddComponent<NativeAudio>();"),
            "the warm starts as soon as the source motion bank is in: under the intro at its Banks step, else after Awake's load");
        Check(load.Contains("if(entranceWarm!=null||dedicated||Application.isBatchMode)return;")&&load.Contains("entranceWarm=NativeEntranceDirector.Warm(NativeSourceMotion.EntranceClip,out entranceWarmCount);"),"never on a dedicated server or in batch mode; the clips are the ones the entrance binds");
        Check(intros.Contains("if(script!=null){lock(script.PoseLock)DirectedIntro(script,arena,pilot,actor,style,age,camera);return;}"),"a directed entrance is staged under its script's PoseLock (never beside the worker)");
        Check(pack.Contains("melee.ReadPose(p);clearance.Clear(melee,ref grip,ref blade,ref edge,")&&!pack.Contains("MeleeClearance.Shared")&&pack.Contains("NativeIntroMotion.BodyPush(Style,planPose,false,push)")&&pack.Contains("NativeIntroMotion.BodyPush(Style,planPose,true,push)"),
            "a pack's plan touches no shared scratch");
        Check(game.Contains("if(DraftFrame())return;if(!menu&&IntroActive&&!StageWarmsArena){arena.Root.SetActive(false);")&&game.Contains("NativeLighting.EndIntro(content.Map(arena.MapId),cameraView);arena.Root.SetActive(true);StageArenaWarming();"),
            "the arena draws its own frame once behind the loading screen while the entrance holds the match");
        Check(stage.Contains("private bool StageWarmsArena=>stageLoad.Covered&&!stageLoad.ActionPending&&stagePlan.Step==StagePreloadStep.Frames&&stageFrames==0&&arena!=null&&match!=null&&match.Phase==NativePhase.Intro;")&&StagePreloadPlan.WarmFrames==2,
            "only the first of the two warm frames, fully covered, in a match's entrance: the second draws the entrance");
        Check(stage.Contains("yield return new WaitForEndOfFrame();\n            if(ReferenceEquals(arena,warmed)&&warmed.Root!=null&&IntroActive)warmed.Root.SetActive(false);"),"the arena's root goes back off at the end of that frame, before the next physics step");
    }
    private static int IndexOf(byte[] data,string tag){var t=System.Text.Encoding.ASCII.GetBytes(tag);for(int i=0;i+t.Length<=data.Length&&i<data.Length;i++){int k=0;while(k<t.Length&&data[i+k]==t[k])k++;if(k==t.Length)return i;}return -1;}
}
