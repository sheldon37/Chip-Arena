using System;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The logo intro at launch (Runtime/NativeBootIntro.cs, docs/BOOT-INTRO.md; owner 2026-11-11: "Lets add this as an intro
    /// when starting the game (Use this time to load assets in). (This is also the new logo for the game)"). Engine-free, so
    /// CoreTests run the rules the player runs: when it plays, when a press skips it, and what the screen shows at each moment.
    /// Presentation only: no rule, save or network state reads it.
    /// </summary>
    public static class NativeBootIntroRules
    {
        /// <summary>The clip, under StreamingAssets (the player plays it by URL; the build copies the folder as it is).</summary>
        public const string Clip="BootIntro.mp4";
        /// <summary>Its final frame, a Resources texture: what holds after a skip and in place of a video that cannot play.</summary>
        public const string Logo="BootIntroLogo";
        /// <summary>The clip's size and SHA-256 (CoreTests and the build check hold the file to them).</summary>
        public const long ClipBytes=5266834;public const string ClipSha256="0f0e60a0b5b2a0a9649c0873596ba098e3adb1270a9e3153352c27ad1320b63b";
        public const float ClipSeconds=10.08f;public const int ClipWidth=1280,ClipHeight=720;
        /// <summary>Command lines that never show it (a dedicated server never reaches it: it returns before the intro starts).</summary>
        public const string NoIntro="-nointro";
        /// <summary>How long the intro is up before a click, Esc, Enter or Space skips it.</summary>
        public const float SkipAfter=.5f;
        /// <summary>After a skip the video gives way to the held logo over this long; its sound fades with it.</summary>
        public const float SkipBlend=.25f;
        /// <summary>A hold for loading shows the loading indicator once it has lasted this long, fading in over IndicatorFade.</summary>
        public const float IndicatorDelay=.3f,IndicatorFade=.35f;
        /// <summary>The crossfade into the main menu.</summary>
        public const float Crossfade=.6f;
        /// <summary>A video that never shows a frame this long after the intro is up (no decoder, the file missing) gives way to the
        /// logo; a playing video whose clock stops this long counts as over.</summary>
        public const float PrepareTimeout=4f,StallTimeout=2f;
        /// <summary>From this far into the clip the logo has settled (the last 2 s are the logo centred on black, sparks drifting
        /// off): the menu's scene may build and draw once under it (NativeBootLoad.cs), where a stall hides on a still frame.</summary>
        public const float SettledFrom=8.1f;
        /// <summary>The clip's sound against the Menu Music level: the clip measures -21.7 LUFS and the menu song -14.1 LUFS at its
        /// x.3 gain (NativeAudio.MenuTrackGain), so x.72 plays the logo as loud as the song that follows it.</summary>
        public const float Gain=.72f;

        public enum Skip{Plays,DedicatedServer,BatchMode,Argument,EditorOff,EditorAutomation}
        /// <summary>
        /// Whether it plays. Never in batch mode or with -nointro (any case, also --nointro). In the Editor only when asked for
        /// (Chip Arena > Boot intro: always in Play mode, or once), and never under the local tools' automated runs, even when
        /// always is on (a run that asks for it sets `once`). The player plays it every launch.
        /// </summary>
        public static Skip Decide(bool dedicated,bool batchMode,string[] args,bool editor,bool editorAlways,bool editorOnce,bool automated)
        {
            if(dedicated)return Skip.DedicatedServer;if(batchMode)return Skip.BatchMode;if(Asked(args))return Skip.Argument;
            if(!editor)return Skip.Plays;if(editorOnce)return Skip.Plays;if(automated)return Skip.EditorAutomation;return editorAlways?Skip.Plays:Skip.EditorOff;
        }
        public static bool Asked(string[] args)
        {
            if(args==null)return false;
            foreach(var a in args)if(string.Equals(a,NoIntro,StringComparison.OrdinalIgnoreCase)||string.Equals(a,"-"+NoIntro,StringComparison.OrdinalIgnoreCase))return true;
            return false;
        }
        /// <summary>The 16:9 picture letterboxed (or pillarboxed) in a w x h screen: x, y, width, height in pixels, centred.</summary>
        public static void Fit(float w,float h,out float x,out float y,out float fw,out float fh)
        {
            const float aspect=ClipWidth/(float)ClipHeight;if(w<=0||h<=0){x=y=fw=fh=0;return;}
            if(w/h>aspect){fh=h;fw=h*aspect;}else{fw=w;fh=w/aspect;}x=(w-fw)*.5f;y=(h-fh)*.5f;
        }
        public static float Smooth(float t){t=t<0?0:t>1?1:t;return t*t*(3-2*t);}
    }

    /// <summary>
    /// The intro's timeline, one Step a frame (Runtime/NativeBootIntro.cs feeds it the clip's state and whether the game below is
    /// ready). Playing until the clip ends or a press skips it; then Holding on the final logo while the game still loads (the
    /// indicator after a moment); then the Crossfade into the menu; then Done. A clip that never plays holds the logo instead.
    /// </summary>
    public sealed class NativeBootIntroFlow
    {
        public enum Phase{Playing,Holding,Fading,Done}
        public Phase State{get;private set;}
        /// <summary>The intro has been up this long (seconds of its own clock, from the first frame it drew).</summary>
        public float Up{get;private set;}
        public bool Skipped{get;private set;}public bool VideoFailed{get;private set;}public bool VideoEnded{get;private set;}
        public float SkippedAt{get;private set;}=-1;public float HoldAt{get;private set;}=-1;public float FadeAt{get;private set;}=-1;
        /// <summary>Under the intro nothing takes input and the menu neither builds nor draws (until Warm says so).</summary>
        public bool Blocking=>State==Phase.Playing||State==Phase.Holding;
        /// <summary>The clip is past its last moving part (settled, over, skipped or failed): a stall there hides on a still frame.</summary>
        public bool Settled{get;private set;}
        /// <summary>The whole overlay's opacity: 1, then down to 0 over the crossfade.</summary>
        public float Cover{get;private set;}=1;
        /// <summary>The video over the held logo: 1 while it plays and after it ends; down to 0 over SkipBlend after a skip; 0
        /// when it never played. LogoShown: the logo image draws under it (after a skip, a failure, and never before).</summary>
        public float Video{get;private set;}=1;public bool LogoShown{get;private set;}public float Logo{get;private set;}
        /// <summary>The loading indicator's opacity.</summary>
        public float Indicator{get;private set;}
        /// <summary>The clip's sound against its level: 1, down to 0 with a skip's blend and the crossfade.</summary>
        public float Sound{get;private set;}=1;
        private float lastVideoTime=-1,videoStill;private bool videoStarted;
        /// <summary>A press (click, Esc, Enter, Space): skips once the intro has been up SkipAfter, while the clip plays.</summary>
        public bool Skip()
        {
            if(State!=Phase.Playing||Skipped||Up<NativeBootIntroRules.SkipAfter)return false;Skipped=true;SkippedAt=Up;return true;
        }
        /// <summary>
        /// Advances by dt (unscaled seconds). videoTime: the clip's clock, negative while no frame has shown. ended / failed: the
        /// player reported its end or an error. loaded: everything the menu needs is ready (NativeBootLoad.cs).
        /// </summary>
        public void Step(float dt,float videoTime,bool ended,bool failed,bool loaded)
        {
            if(State==Phase.Done)return;if(dt<0||float.IsNaN(dt))dt=0;Up+=dt;
            if(videoTime>=0&&!videoStarted)videoStarted=true;
            if(failed&&!VideoEnded)VideoFailed=true;
            if(!videoStarted&&!VideoFailed&&Up>=NativeBootIntroRules.PrepareTimeout)VideoFailed=true;
            // A clock that stops short of the end (a decoder that stalls) counts as the end.
            if(videoStarted&&!ended&&!VideoFailed){if(videoTime>lastVideoTime+1e-4f){lastVideoTime=videoTime;videoStill=0;}else if((videoStill+=dt)>=NativeBootIntroRules.StallTimeout)ended=true;}
            if(ended&&videoStarted)VideoEnded=true;
            bool over=Skipped||VideoEnded||VideoFailed;
            Settled=over||videoStarted&&videoTime>=NativeBootIntroRules.SettledFrom;
            if(State==Phase.Playing&&over){State=loaded?Phase.Fading:Phase.Holding;if(State==Phase.Holding)HoldAt=Up;else FadeAt=Up;}
            else if(State==Phase.Holding&&loaded){State=Phase.Fading;FadeAt=Up;}
            if(State==Phase.Fading&&Up-FadeAt>=NativeBootIntroRules.Crossfade)State=Phase.Done;
            // The layers.
            Cover=State==Phase.Fading?1-NativeBootIntroRules.Smooth((Up-FadeAt)/NativeBootIntroRules.Crossfade):State==Phase.Done?0:1;
            float skipBlend=Skipped?NativeBootIntroRules.Smooth((Up-SkippedAt)/NativeBootIntroRules.SkipBlend):0;
            if(VideoFailed&&!Skipped&&!VideoEnded){Video=0;LogoShown=true;Logo=NativeBootIntroRules.Smooth(Up/.4f);}
            else{Video=videoStarted?1-skipBlend:0;LogoShown=Skipped;Logo=Skipped?1:0;}
            Indicator=State==Phase.Holding?NativeBootIntroRules.Smooth((Up-HoldAt-NativeBootIntroRules.IndicatorDelay)/NativeBootIntroRules.IndicatorFade):State==Phase.Fading&&HoldAt>=0?Indicator*Math.Max(0,1-dt/.2f):0;
            Sound=(1-skipBlend)*Cover;
        }
    }
}
