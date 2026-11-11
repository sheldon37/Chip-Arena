using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Stopwatch=System.Diagnostics.Stopwatch;
namespace ChipArena.UnityConversion
{
    // The loading the logo intro covers (NativeBootIntro.cs, docs/BOOT-INTRO.md). Before Unity's splash screen, when the intro
    // will play, the two motion banks and the source models' JSON start decoding on workers (NativeBankLoad.cs, NativeBootPrefetch
    // below), through the splash and Awake; the source models are still built in presentation.Initialize (which waits for its
    // JSON) and the banks are joined here. Then, in order, one step a frame:
    //   1. Banks: joined once both workers are done.
    //   2. Assets: what the menu's first frames load synchronously comes in by Resources.LoadAsync, at Low priority meanwhile.
    //   3. Scene: once the clip has settled on its logo (or is over), the menu's scene builds and draws for two frames under it.
    //   4. Gui: then the menu's GUI draws for two frames under it (its skins, text and portraits made).
    //   5. Ready: the intro may crossfade into the menu.
    // Until a step allows it, LateFrame and OnGUI return at once (nothing reads a bank before it is in) and Update takes no
    // input; the rest of Awake ran as it always did. Without the intro (a dedicated server, batch mode, -nointro, the Editor
    // unless asked) the banks load in Awake as before and none of this runs. Presentation only.
    public sealed partial class ConversionGame
    {
        private enum BootStage{Banks,Assets,Scene,Gui,Ready}
        private NativeBootIntro bootIntro;private BootStage bootStage;private bool bootHanded,bootFailed;
        private readonly List<ResourceRequest> bootAssets=new List<ResourceRequest>();
        private int bootSceneFrames,bootGuiFrames,bootWarmFrame=-1;private long bootAt;private float bootNoticeFrom;
        private readonly List<string> bootSteps=new List<string>();
        // DEV: `--ca-boot-hold S` keeps the loading open until S seconds after the intro began, to see the held logo and its
        // indicator (the local capture tool sets it too). Never set in normal play.
        private float bootDevHold=BootHoldArgument();
        private static float BootHoldArgument(){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"--ca-boot-hold");return i>=0&&i+1<a.Length&&float.TryParse(a[i+1],NumberStyles.Float,CultureInfo.InvariantCulture,out float s)?Mathf.Clamp(s,0,120):0;}
        // What the first menu frames would otherwise load on the main thread: the model's bake (its meshes, materials and
        // textures; absent in cloud builds), its outfit table and the menu's icon sets.
        private static readonly (string path,Type type)[] BootAssets={(ArenaPresentation.VisualBodyResource,typeof(GameObject)),(ArenaPresentation.VisualLookResource,typeof(TextAsset)),("Icons",typeof(TextAsset)),("ClassIcons",typeof(TextAsset))};
        private double BootMs=>(Stopwatch.GetTimestamp()-bootAt)*1000d/Stopwatch.Frequency;
        private bool bootWanted;
        /// <summary>
        /// ConversionGame.Boot: with the intro to play, its black curtain now and the game one frame later (NativeBootDefer), so
        /// a black frame is on screen before Awake's work (without Unity's splash the window showed white until the first frame).
        /// Awake itself is unchanged. False: the game is made at once, as always.
        /// </summary>
        private static bool BootCurtain()
        {
            NativeBootPrefetch.Decide();if(!NativeBootPrefetch.Wanted||NativeBootIntro.Current!=null)return false;
            NativeBootIntro.Curtain();new GameObject("Boot defer").AddComponent<NativeBootDefer>();return true;
        }
        /// <summary>The top of Awake: whether the intro will play (decided before the splash; here if that hook did not run).</summary>
        private void BootPrefetch()
        {
            NativeBootPrefetch.Decide();bootWanted=NativeBootPrefetch.Wanted;if(!bootWanted)return;bootAt=NativeBootPrefetch.StartedAt;
            bootSteps.Add("workers="+NativeBootPrefetch.StartMs.ToString("F0",CultureInfo.InvariantCulture)+"ms");BootStep("awake");
        }
        /// <summary>Awake, where the banks used to load: true when the intro plays; false keeps the synchronous load.</summary>
        private bool BootIntroStart()
        {
            if(!bootWanted||dedicated)return false;
            bootNoticeFrom=Time.unscaledTime;
            float level=prefs.sound?Mathf.Clamp01(prefs.menuMusic)*NativeBootIntroRules.Gain:0;Func<bool> ready=()=>bootStage==BootStage.Ready&&BootMs>=bootDevHold*1000||error.Length>0;
            bootIntro=NativeBootIntro.Current!=null&&!NativeBootIntro.Current.Armed?NativeBootIntro.Current.Arm(level,ready):NativeBootIntro.Begin(level,ready);
            bool voxel=Array.IndexOf(Environment.GetCommandLineArgs(),ArenaPresentation.VoxelBodiesArgument)>=0;
            foreach(var (path,type) in BootAssets)if(!(voxel&&type==typeof(GameObject)))bootAssets.Add(Resources.LoadAsync(path,type));
            Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"BOOT_INTRO playing at {0:F2} s since start ({1:F0} ms after its workers started, before the splash)",Time.realtimeSinceStartupAsDouble,BootMs);
            return true;
        }
        private void BootStep(string what){bootSteps.Add(what+"="+BootMs.ToString("F0",CultureInfo.InvariantCulture));}
        /// <summary>Update, before any input is read: steps the loading; true while the intro holds the game (no input then).</summary>
        private bool BootIntroHolds()
        {
            if(bootIntro==null)return false;
            BootLoadStep();
            nativeAudio.MenuHeld=NativeBootIntro.Blocking;
            if(!NativeBootIntro.Blocking){BootHandoff();return false;}
            if(PadBack||PadDown(PadAction.Jump)||PadDown(PadAction.Pause))bootIntro.Press();
            pending=new InputFrame();Cursor.lockState=CursorLockMode.None;Cursor.visible=false;
            return true;
        }
        private void BootLoadStep()
        {
            // The frame a warm step ran in costs its whole interval (its build, its draw); recorded on the next frame.
            if(bootWarmFrame>=0&&Time.frameCount>bootWarmFrame){bootSteps.Add("frame"+(bootSceneFrames+bootGuiFrames)+"="+(Time.unscaledDeltaTime*1000).ToString("F0",CultureInfo.InvariantCulture)+"ms");bootWarmFrame=-1;}
            if(bootFailed||error.Length>0)return;
            try
            {
                switch(bootStage)
                {
                    case BootStage.Banks:
                        // Both stepped every frame (each starts its worker as soon as its resource is in), then joined together.
                        bool sourceIn=NativeSourceMotion.Ready,sceneIn=NativeSceneMotion.Ready;if(!sourceIn||!sceneIn)return;
                        long joinAt=Stopwatch.GetTimestamp();NativeSourceMotion.Prewarm();NativeSceneMotion.Prewarm();
                        UnityEngine.Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"MOTION_BANKS_READY source={0} scene={1} ms={2:F1} (on workers from before the splash; joined in {3:F1} ms)",NativeSourceMotion.ClipCount,NativeSceneMotion.Count,BootMs,HitchMs(joinAt));
                        BootStep("banks");bootStage=BootStage.Assets;return;
                    case BootStage.Assets:
                        foreach(var r in bootAssets)if(!r.isDone)return;
                        BootStep("assets");bootStage=BootStage.Scene;return;
                    case BootStage.Scene:
                        if(bootSceneFrames>=2){BootStep("scene");bootStage=BootStage.Gui;}return;
                    case BootStage.Gui:
                        if(bootGuiFrames>=2){BootStep("gui");bootStage=BootStage.Ready;}return;
                }
            }
            catch(Exception e)
            {
                // A bank that fails to decode fails as it did in Awake: the start-up error shows (after the intro), on black.
                bootFailed=true;error=e.ToString();Debug.LogException(e);if(cameraView!=null)cameraView.enabled=false;if(viewModelCamera!=null)viewModelCamera.enabled=false;
            }
        }
        /// <summary>LateFrame, first: true (skip the frame) until the menu's scene may build under the intro.</summary>
        private bool BootIntroFrame()
        {
            if(bootIntro==null)return false;if(bootFailed)return true;
            if(bootStage<BootStage.Scene||bootStage==BootStage.Scene&&!bootIntro.Flow.Settled)return true;
            if(bootStage==BootStage.Scene&&bootSceneFrames<2){bootSceneFrames++;bootWarmFrame=Time.frameCount;}
            return false;
        }
        /// <summary>OnGUI, first: true (draw nothing) until the menu's GUI may draw under the intro. A start-up error always draws.</summary>
        private bool BootIntroGui()
        {
            if(bootIntro==null||error.Length>0)return false;
            if(bootStage<BootStage.Gui)return true;
            if(bootStage==BootStage.Gui&&bootGuiFrames<2&&Event.current.type==EventType.Repaint){bootGuiFrames++;bootWarmFrame=Time.frameCount;}
            return false;
        }
        // The crossfade began: the menu takes input, its song starts from the top, and a notice raised under the intro (the
        // updater's, a save warning) shows now for its full time.
        private void BootHandoff()
        {
            if(bootHanded)return;bootHanded=true;nativeAudio.MenuHeld=false;
            if(noticeText.Length>0&&noticeLast>=bootNoticeFrom){noticeShown=noticeLast=Time.unscaledTime;}
            Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"BOOT_HANDOFF interactive at {0:F2} s since start ({1:F0} ms after the workers started); steps since then {2}; sculpts made {3} in {4:F0} ms, taken {5}, sculpted here {6}, unused {7}",Time.realtimeSinceStartupAsDouble,BootMs,string.Join(" ",bootSteps),NativeBootSculpts.Made,NativeBootSculpts.WorkerMs,NativeBootSculpts.Taken,NativeBootSculpts.Fallbacks,NativeBootSculpts.Left);
        }
    }
    /// <summary>The game one frame after the intro's curtain (ConversionGame.BootCurtain): once a black frame has been shown.</summary>
    internal sealed class NativeBootDefer:MonoBehaviour
    {
        private int updates;
        private void Awake()=>DontDestroyOnLoad(gameObject);
        private void Update()
        {
            if(++updates<2)return;
            if(FindFirstObjectByType<ConversionGame>()==null)new GameObject("Chip Arena Native Conversion").AddComponent<ConversionGame>();
            var curtain=NativeBootIntro.Current;if(curtain!=null&&!curtain.Armed)curtain.Dismiss();
            Destroy(gameObject);
        }
    }
    /// <summary>
    /// The logo intro's workers start before Unity's splash screen (RuntimeInitializeLoadType.BeforeSplashScreen): nothing of the
    /// game runs while the splash plays (2.2 s in a Windows build, 2026-11-11), so the banks decode through it. Decided once per
    /// run, from the command line and the Editor's asks only (the dedicated server's own flag, --ca-server, says no): a decision
    /// to play changes nothing but when the banks decode; ConversionGame.Awake starts the intro itself.
    /// </summary>
    internal static class NativeBootPrefetch
    {
        public static bool Decided{get;private set;}public static bool Wanted{get;private set;}
        public static long StartedAt{get;private set;}public static double StartMs{get;private set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){Decided=Wanted=false;NativeBootSculpts.Reset();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Early()=>Decide();
        public static void Decide()
        {
            if(Decided)return;Decided=true;
            bool always=false,once=false,automated=false;var args=Environment.GetCommandLineArgs();
#if UNITY_EDITOR
            always=UnityEditor.EditorPrefs.GetBool(NativeBootIntro.EditorAlwaysKey,false);
            once=UnityEditor.SessionState.GetBool(NativeBootIntro.EditorOnceKey,false);if(once)UnityEditor.SessionState.EraseBool(NativeBootIntro.EditorOnceKey);
            automated=UnityEditor.SessionState.GetString(NativeBootIntro.EditorAutomationKey,"").Length>0;
#endif
            var why=NativeBootIntroRules.Decide(Array.IndexOf(args,"--ca-server")>=0,Application.isBatchMode,args,Application.isEditor,always,once,automated);
            if(why!=NativeBootIntroRules.Skip.Plays){Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"BOOT_INTRO off ({0})",why);return;}
            Wanted=true;StartedAt=Stopwatch.GetTimestamp();
            NativeSourceMotion.PrewarmAsync();NativeSceneMotion.PrewarmAsync();NativeSourceModels.PrefetchAsync();
            // The sculpts behind the source models' slowest builds (Core NativeBootSculpts), on their own worker.
            NativeBootSculpts.Start();
            StartMs=(Stopwatch.GetTimestamp()-StartedAt)*1000d/Stopwatch.Frequency;
        }
    }
}
