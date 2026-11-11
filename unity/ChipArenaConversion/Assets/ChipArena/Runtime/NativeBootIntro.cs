using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Video;
using Stopwatch=System.Diagnostics.Stopwatch;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The logo intro at launch (owner 2026-11-11: "Lets add this as an intro when starting the game (Use this time to load
    /// assets in). (This is also the new logo for the game)"; docs/BOOT-INTRO.md). The clip plays full screen, letterboxed 16:9
    /// on black, its sound at the Menu Music level (silent under Mute audio), while the game loads under it (NativeBootLoad.cs).
    /// A click, Esc, Enter or Space (a pad's A, B or Start) skips it after half a second. If the game is not ready when the clip
    /// ends or is skipped, the final logo holds with a small loading indicator; then it crossfades into the main menu. Drawn over
    /// every other GUI (GUI.depth); nothing under it takes input. The timeline is Core NativeBootIntroFlow. Presentation only.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class NativeBootIntro : MonoBehaviour
    {
        /// <summary>Editor only (Chip Arena > Boot intro): play it on every Play mode entry, or on the next one only.</summary>
        public const string EditorAlwaysKey="ChipArena.BootIntro.PlayMode",EditorOnceKey="ChipArena.BootIntro.Once";
        /// <summary>The local capture tool's play job (its SessionState key, docs/BOOT-INTRO.md): an automated Editor run.</summary>
        public const string EditorAutomationKey="bozo-play-job";
        public static NativeBootIntro Current{get;private set;}
        /// <summary>The intro hides the game: input stays with it, the menu waits (until the crossfade starts).</summary>
        public static bool Blocking=>Current!=null&&Current.flow.Blocking;
        /// <summary>The clip is on screen and moving (the measurement window of the intro's frame report).</summary>
        public static bool VideoPlaying=>Current!=null&&Current.Moving;
        public NativeBootIntroFlow Flow=>flow;
        private readonly NativeBootIntroFlow flow=new NativeBootIntroFlow();
        private VideoPlayer player;private AudioSource voice;private RenderTexture frame;private Texture2D logo,dot;private ResourceRequest logoLoad;
        private Func<bool> ready;private float level;private bool prepared,started,ended,failed,splashDone,shown,stopped,finished;private string failure;
        private ThreadPriority loadingPriority;private GUIStyle label;
        private readonly List<float> playFrames=new List<float>();private readonly Stopwatch clock=Stopwatch.StartNew();
        private double shownAt=-1,startedAt=-1,overAt=-1,readyAt=-1,fadeAt=-1;
        private bool Moving=>started&&player!=null&&player.frame>=0&&!flow.VideoEnded&&!flow.Skipped&&!flow.VideoFailed;
        /// <summary>
        /// Starts the intro (ConversionGame.Awake, once the preferences are in): the clip prepares at once and plays once Unity's
        /// own splash screen is over. `level` 0-1 is the sound's volume; `ready` says when the game below may be shown.
        /// </summary>
        public static NativeBootIntro Begin(float level,Func<bool> ready)=>Curtain().Arm(level,ready);
        /// <summary>
        /// The intro before the game exists (ConversionGame.Boot, one frame ahead of the game's Awake): black over everything, so
        /// the window shows black, not an unpainted white, through Awake's work; the clip prepares meanwhile. Nothing plays and
        /// its clock does not run until Arm.
        /// </summary>
        public static NativeBootIntro Curtain()
        {
            var go=new GameObject("Boot intro");DontDestroyOnLoad(go);var intro=go.AddComponent<NativeBootIntro>();intro.Setup();return intro;
        }
        /// <summary>The game's Awake hands over its sound level and its readiness: the intro starts.</summary>
        public NativeBootIntro Arm(float level,Func<bool> ready)
        {
            this.ready=ready;this.level=Mathf.Clamp01(level);if(voice!=null)voice.volume=this.level;armed=true;return this;
        }
        public bool Armed=>armed;private bool armed;
        /// <summary>A curtain the game never armed (its Awake failed before the intro, or chose none): gone at once.</summary>
        public void Dismiss(){if(finished)return;finished=true;Debug.Log("BOOT_INTRO curtain lifted without the intro");Release();Current=null;Destroy(gameObject);}
        private void Setup()
        {
            Current=this;
            // The intro's loads (and the game's) integrate in small slices while the clip plays; restored when it ends.
            loadingPriority=Application.backgroundLoadingPriority;Application.backgroundLoadingPriority=ThreadPriority.Low;
            logoLoad=Resources.LoadAsync<Texture2D>(NativeBootIntroRules.Logo);
            dot=Dot();
            string path=Path.Combine(Application.streamingAssetsPath,NativeBootIntroRules.Clip);
            if(!File.Exists(path)){Fail("the clip is missing ("+path+")");return;}
            frame=new RenderTexture(NativeBootIntroRules.ClipWidth,NativeBootIntroRules.ClipHeight,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default){name="Boot intro frame",hideFlags=HideFlags.HideAndDontSave};frame.Create();
            var was=RenderTexture.active;RenderTexture.active=frame;GL.Clear(false,true,Color.black);RenderTexture.active=was;
            voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.loop=false;voice.spatialBlend=0;voice.priority=0;voice.bypassEffects=voice.bypassListenerEffects=voice.bypassReverbZones=true;voice.volume=this.level;
            player=gameObject.AddComponent<VideoPlayer>();player.playOnAwake=false;player.source=VideoSource.Url;player.url=path;player.isLooping=false;player.skipOnDrop=true;player.waitForFirstFrame=true;
            player.renderMode=VideoRenderMode.RenderTexture;player.targetTexture=frame;player.aspectRatio=VideoAspectRatio.Stretch;
            player.audioOutputMode=VideoAudioOutputMode.AudioSource;player.controlledAudioTrackCount=1;player.EnableAudioTrack(0,true);player.SetTargetAudioSource(0,voice);
            player.errorReceived+=(p,message)=>Fail(message);player.prepareCompleted+=p=>prepared=true;player.loopPointReached+=p=>ended=true;
            player.Prepare();
        }
        private void Fail(string why){if(failed)return;failed=true;failure=why;Debug.LogWarning("BOOT_INTRO the clip cannot play ("+why+"); the logo holds instead.");}
        /// <summary>A press from elsewhere (the game's pad buttons): skips as a click would.</summary>
        public void Press(){flow.Skip();}
        private static bool Pressed()=>Input.GetMouseButtonDown(0)||Input.GetMouseButtonDown(1)||Input.GetKeyDown(KeyCode.Escape)||Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter)||Input.GetKeyDown(KeyCode.Space);
        private static bool SplashDone()
        {
#if UNITY_EDITOR
            return true;
#else
            return UnityEngine.Rendering.SplashScreen.isFinished;
#endif
        }
        private double Ms=>clock.Elapsed.TotalMilliseconds;
        private void Update()
        {
            if(finished||!armed)return;
            // Unity's splash screen (Player Settings) draws over everything: the clip waits for it, the loading does not.
            if(!splashDone){splashDone=SplashDone();if(!splashDone)return;}
            float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);if(!shown){shown=true;dt=0;shownAt=Ms;}
            if(!started&&prepared&&!failed){player.Play();started=true;startedAt=Ms;}
            if(logo==null&&logoLoad!=null&&logoLoad.isDone){logo=logoLoad.asset as Texture2D;logoLoad=null;}
            if(Moving)playFrames.Add(Time.unscaledDeltaTime*1000);
            if(Pressed())flow.Skip();
            bool loaded=ready==null||ready();if(loaded&&readyAt<0)readyAt=Ms;
            float videoTime=started&&player.frame>=0?(float)player.time:-1;
            flow.Step(dt,videoTime,ended,failed,loaded);
            if(overAt<0&&(flow.Skipped||flow.VideoEnded||flow.VideoFailed))overAt=Ms;if(fadeAt<0&&flow.State==NativeBootIntroFlow.Phase.Fading)fadeAt=Ms;
            if(voice!=null)voice.volume=level*flow.Sound;
            // A skipped clip stops once it has given way to the logo (its decoder freed for the loading).
            if(player!=null&&started&&!stopped&&flow.Skipped&&flow.Video<=0){player.Stop();stopped=true;}
            if(flow.State==NativeBootIntroFlow.Phase.Done)Finish();
        }
        private void OnGUI()
        {
            if(finished)return;GUI.depth=-1000;var e=Event.current;
            // Nothing under the intro takes a click or a key (the menu's first draw runs under it, NativeBootLoad.cs).
            if(flow.Blocking&&(e.type==EventType.MouseDown||e.type==EventType.MouseUp||e.type==EventType.MouseDrag||e.type==EventType.KeyDown||e.type==EventType.KeyUp||e.type==EventType.ScrollWheel)){e.Use();return;}
            if(e.type!=EventType.Repaint)return;
            float W=Screen.width,H=Screen.height;NativeBootIntroRules.Fit(W,H,out float x,out float y,out float w,out float h);var fit=new Rect(x,y,w,h);
            var old=GUI.color;float cover=flow.Cover;
            GUI.color=new Color(0,0,0,cover);GUI.DrawTexture(new Rect(0,0,W,H),Texture2D.whiteTexture);
            if(flow.LogoShown&&logo!=null&&flow.Logo>0){float a=cover*flow.Logo;GUI.color=new Color(1,1,1,a);GUI.DrawTexture(fit,logo,ScaleMode.StretchToFill,a<.999f);}
            // Opaque while it plays (whatever alpha the decoder writes), blended only through the skip's and the crossfade's fades.
            if(frame!=null&&started&&flow.Video>0){float a=cover*flow.Video;GUI.color=new Color(1,1,1,a);GUI.DrawTexture(fit,frame,ScaleMode.StretchToFill,a<.999f);}
            if(flow.Indicator>0)Indicator(fit,cover*flow.Indicator);
            GUI.color=old;
        }
        // The loading indicator: a ring of ten dots turning in the logo's orange under the chip, and LOADING in its cream.
        private void Indicator(Rect fit,float alpha)
        {
            float cx=fit.center.x,cy=fit.y+fit.height*.905f,r=fit.height*.017f,d=Mathf.Max(3,fit.height*.0068f),turn=(float)(clock.Elapsed.TotalSeconds*5.5);
            for(int i=0;i<10;i++)
            {
                float a=turn-i*.5f,trail=1-i/10f;GUI.color=new Color(1,.56f,.16f,alpha*trail*trail);
                GUI.DrawTexture(new Rect(cx+r*Mathf.Cos(a)-d*.5f,cy+r*Mathf.Sin(a)-d*.5f,d,d),dot);
            }
            if(label==null)label=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,clipping=TextClipping.Overflow,wordWrap=false};
            label.fontSize=Mathf.Max(10,Mathf.RoundToInt(fit.height*.017f));label.normal.textColor=new Color(.97f,.9f,.76f,alpha*.75f);GUI.color=Color.white;
            GUI.Label(new Rect(cx-fit.height*.2f,cy+r+fit.height*.014f,fit.height*.4f,fit.height*.03f),"L O A D I N G",label);
        }
        private static Texture2D Dot()
        {
            const int n=32;var t=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Boot intro dot",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};var px=new Color32[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++){float dx=(x+.5f)/n*2-1,dy=(y+.5f)/n*2-1,q=Mathf.Clamp01((1-Mathf.Sqrt(dx*dx+dy*dy))*n*.5f);px[y*n+x]=new Color32(255,255,255,(byte)(q*255));}
            t.SetPixels32(px);t.Apply(false,true);return t;
        }
        // One line at the end (BOOT_INTRO): when it showed, played, ended, the game was ready, the crossfade began, and the
        // main-thread frame intervals while the clip moved.
        private void Report()
        {
            var a=playFrames.OrderBy(v=>v).ToArray();float P(float q)=>a.Length==0?0:a[Math.Min(a.Length-1,(int)Math.Ceiling(q*a.Length)-1)];
            Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"{0}",string.Format(CultureInfo.InvariantCulture,
                "BOOT_INTRO shown_ms={0:F0} play_ms={1:F0} over_ms={2:F0} ready_ms={3:F0} fade_ms={4:F0} done_ms={5:F0} skipped={6} failed={7} held_ms={8:F0} frames n={9} mean={10:F1} p95={11:F1} p99={12:F1} max={13:F1} over33={14} over50={15}{16}",
                shownAt,startedAt,overAt,readyAt,fadeAt,Ms,flow.Skipped,flow.VideoFailed,flow.HoldAt>=0?(fadeAt-overAt):0,a.Length,a.Length==0?0:a.Average(),P(.95f),P(.99f),a.Length==0?0:a[a.Length-1],a.Count(v=>v>33.4f),a.Count(v=>v>50),failure!=null?" failure="+failure:""));
        }
        private void Finish()
        {
            if(finished)return;finished=true;Report();Release();Current=null;Destroy(gameObject);
        }
        private void Release()
        {
            Application.backgroundLoadingPriority=loadingPriority;
            if(player!=null&&!stopped){player.Stop();stopped=true;}
            if(frame!=null){frame.Release();Destroy(frame);frame=null;}if(dot!=null){Destroy(dot);dot=null;}
            if(logo!=null){Resources.UnloadAsset(logo);logo=null;}
        }
        private void OnDestroy(){if(!finished){finished=true;Release();}if(Current==this)Current=null;}
    }
}
