using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE 6:47's arrival beats on the shared boss clock (seconds from the arrival, BossSongTime 0; doc section 6): the chime
    /// hit (the board flips to ARRIVING), the light cut (the headlight blooms in the west mouth), four intro accents (brake
    /// sparks, the stop, the legs unfolding, the step up), the drop (the burst through the barrier gate), the banner (the fight's
    /// drop) and the intro hold. The boss song binds them from its accents (<see cref="NativeStationBossBeats.UseSong"/>, Station
    /// Chime of Doom through docs/ZONE-BOSS-MUSIC.md's intro edit); the doc's table is the fallback when the song is missing.
    /// `Song` marks beats from the song: its stage (NativeExpeditionStationMusic) then owns the house lights' cut and flare and
    /// the board's LAST CALL, and the boss's stage hook adds only its own parts. Presentation only: no hit, hold or rule reads these.
    /// </summary>
    public struct StationBossBeats
    {
        public float ChimeHit,LightCut,Accent0,Accent1,Accent2,Accent3,Drop,Banner,Hold;public bool Song;
        public float Accent(int i)=>i<=0?Accent0:i==1?Accent1:i==2?Accent2:Accent3;
        /// <summary>The doc's fallback seconds (no song installed): the banner on the drop, the hold the intro hold
        /// (NativePve's 9.0678 s, which the boss song's drop lands on).</summary>
        public static readonly StationBossBeats Fallback=new StationBossBeats{ChimeHit=0,LightCut=1.6f,Accent0=3.0f,Accent1=3.95f,Accent2=4.9f,Accent3=5.85f,Drop=6.8f,Banner=6.8f,Hold=9.0678f};
        /// <summary>Ordered and inside the hold (a set that breaks the order is refused).</summary>
        public bool Valid=>ChimeHit>=0&&LightCut>ChimeHit&&Accent0>LightCut&&Accent1>Accent0&&Accent2>Accent1&&Accent3>Accent2&&Drop>Accent3&&Banner>=Drop&&Hold>=Drop&&Banner<=Hold+.05f&&Hold<30;
        /// <summary>
        /// The beats from a boss song on the boss clock (<see cref="NativeZoneBossSong.FirstBoss"/>: the intro edit's opening and
        /// its resumed run to the drop; accents the edit cuts are skipped):
        /// <list type="bullet">
        /// <item>the chime hit is the first chime and the light cut the first cut after it (the headlight blooms as the song's
        /// stage cuts the lights);</item>
        /// <item>the stop at the crossing is the first slam or drop at least 1.2 s after the cut (where the song's stage flares the
        /// lights back), and the brake sparks fall on the first beat of the song's grid .5 s after the cut;</item>
        /// <item>the unfold and the step up are the next hits .5 s and .4 s on;</item>
        /// <item>the burst is the last slam at least .25 s before the banner, and the banner the drop on the hold's end (the hold if
        /// the map has none).</item>
        /// </list>
        /// Returns false (and the fallback) when the song cannot place them in order. Allocates nothing; run once per song.
        /// </summary>
        public static bool FromSong(NativeZoneBossSong song,out StationBossBeats b)
        {
            b=Fallback;if(song?.Accents==null||!(song.Hold>0)||song.Hold>=30)return false;float hold=(float)song.Hold,end=hold+.05f;
            const byte Any=255,Hit=254;
            float chime=First(song,NativeZoneBossSong.AccentChime,0,end);if(float.IsNaN(chime))return false;
            float cut=First(song,NativeZoneBossSong.AccentCut,chime+1e-3f,end);if(float.IsNaN(cut))return false;
            float banner=Last(song,NativeZoneBossSong.AccentDrop,cut,end);if(float.IsNaN(banner))banner=hold;banner=Math.Min(banner,hold);
            float stop=First(song,Hit,cut+1.2f,banner-1.5f),sparks=Beat(song,cut+.5f,stop-.3f);if(float.IsNaN(sparks))sparks=(cut+stop)*.5f;
            float unfold=First(song,Any,stop+.5f,banner-1.2f),step=First(song,Any,unfold+.4f,banner-.8f),drop=Last(song,NativeZoneBossSong.AccentSlam,step+.5f,banner-.25f);
            var r=new StationBossBeats{ChimeHit=chime,LightCut=cut,Accent0=sparks,Accent1=stop,Accent2=unfold,Accent3=step,Drop=drop,Banner=banner,Hold=hold,Song=true};
            if(!r.Valid)return false;b=r;return true;
        }
        // The first beat of the song's grid on the boss clock in [from, to]; NaN if none.
        private static float Beat(NativeZoneBossSong s,float from,float to){var g=s.Track?.beats;if(g==null)return float.NaN;foreach(double c in g){double t=s.FirstBoss(c);if(t>=from&&t<=to)return (float)t;}return float.NaN;}
        // The earliest (latest) accent of `kind` (255: any; 254: a slam or a drop) on the boss clock in [from, to]; NaN if none.
        private static bool Kind(byte want,byte kind)=>want==255||want==kind||want==254&&(kind==NativeZoneBossSong.AccentSlam||kind==NativeZoneBossSong.AccentDrop);
        private static float First(NativeZoneBossSong s,byte kind,float from,float to){float best=float.NaN;foreach(var a in s.Accents){if(!Kind(kind,a.Kind))continue;double t=s.FirstBoss(a.T);if(t>=from&&t<=to&&!(t>=best))best=(float)t;}return best;}
        private static float Last(NativeZoneBossSong s,byte kind,float from,float to){float best=float.NaN;foreach(var a in s.Accents){if(!Kind(kind,a.Kind))continue;double t=s.FirstBoss(a.T);if(t>=from&&t<=to&&!(t<=best))best=(float)t;}return best;}
    }

    /// <summary>The one place the arrival's beats come from: the boss song's once it is bound, else the doc's fallback.</summary>
    public static class NativeStationBossBeats
    {
        private static StationBossBeats bound;private static bool has;private static NativeZoneBossSong song;
        /// <summary>The beats the arrival plays on now.</summary>
        public static StationBossBeats Current=>has?bound:StationBossBeats.Fallback;
        /// <summary>Binds a set of beats; an out-of-order set is refused and the previous one stays. Returns whether it was taken.</summary>
        public static bool Use(in StationBossBeats beats){if(!beats.Valid)return false;bound=beats;has=true;song=null;return true;}
        /// <summary>Binds the boss song's accents (<see cref="StationBossBeats.FromSong"/>); the same song again is a no-op, so the
        /// presenter may call this every frame. A song that cannot place the beats leaves the fallback. Returns whether bound.</summary>
        public static bool UseSong(NativeZoneBossSong s)
        {
            if(s==null){if(song!=null)Clear();return false;}if(ReferenceEquals(s,song))return has;
            song=s;if(StationBossBeats.FromSong(s,out var b)){bound=b;has=true;return true;}has=false;return false;
        }
        public static void Clear(){has=false;song=null;}
    }

    /// <summary>
    /// What the station should show for THE 6:47 at one moment (the hook the station's own presentation reads later: the board,
    /// every station clock, the house lights, T1's west signal, the barrier and the board's fall). It is presentation only and
    /// a pure function of the boss view, the clock and the beats (<see cref="NativeStationBossStage.Evaluate"/>).
    /// <list type="bullet">
    /// <item><c>Board</c>: <see cref="NativeStationBossStage.BoardOwn"/> (the station's own rows), <c>BoardArriving</c> (row 0
    /// reads <see cref="NativeStationBossStage.ArrivingText"/>, blinking while <c>BoardBlink</c>), <c>BoardDeparted</c> (row 0
    /// reads <see cref="NativeStationBossStage.DepartedText"/>); <c>BoardFlicker</c> 0..1 the flip's stutter.</item>
    /// <item><c>BoardFall</c> 0 hanging .. 1 lying across the booth (TERMINUS), <c>BarrierFall</c> 0 standing .. 1 the centre
    /// segments thrown (PLATFORM ALTERATION). Visual only: their colliders are the authority's.</item>
    /// <item><c>ClockHour</c>:<c>ClockMinute</c> what every clock reads (6:47; 6:48 once the boss has departed), <c>ClockTick</c>
    /// a 0..1 pulse on each strike (Terminus's notes, the phase change, the last tick).</item>
    /// <item><c>Lights</c> the house and practical lights' level (1 as authored, about .06 through the arrival's cut),
    /// <c>LightSurge</c> the slam back on the drop (0..1, decaying): only on the fallback beats. With the boss song bound the
    /// song's stage owns the lights and these stay 1 and 0. <c>WestSignalGreen</c> T1's west signal for the arrival.</item>
    /// <item><c>Shake</c> 0..1 the station's shudder on impacts, <c>Dust</c> 0..1 dust falling from the vault.</item>
    /// </list>
    /// </summary>
    public struct StationBossStage
    {
        public bool On;public byte Board;public bool BoardBlink;public float BoardFlicker,BoardFall,BarrierFall;
        public int ClockHour,ClockMinute;public float ClockTick;
        public float Lights,LightSurge;public bool WestSignalGreen;
        public float Shake,Dust;
        /// <summary>The station as it is with no boss.</summary>
        public static readonly StationBossStage Idle=new StationBossStage{On=false,Board=NativeStationBossStage.BoardOwn,ClockHour=6,ClockMinute=47,Lights=1};
        /// <summary>Row 0's text for the board's state (null: the station's own row).</summary>
        public string Row0=>Board==NativeStationBossStage.BoardArriving?NativeStationBossStage.ArrivingText:Board==NativeStationBossStage.BoardDeparted?NativeStationBossStage.DepartedText:null;
    }

    /// <summary>
    /// The hook (owner 2026-10-17 approval: "clocks to 6:48 and DEPARTED"): THE 6:47's presentation writes <see cref="Shown"/>
    /// every frame it draws (live, killcam or DEV preview), and the station's board, clocks and lights read it. Until the station
    /// presentation reads it nothing in the station changes (the boss's own model, effects and sounds are drawn by
    /// Runtime/NativeStationBossPresentation.cs). Allocation free.
    /// </summary>
    public static class NativeStationBossStage
    {
        public const byte BoardOwn=0,BoardArriving=1,BoardDeparted=2;
        public const string ArrivingText="6.47 EVAC SPECIAL 1 ARRIVING",DepartedText="6.47 EVAC SPECIAL DEPARTED";
        /// <summary>Seconds after the death: the clocks tick to 6:48 and the board flips to DEPARTED (doc section 6, inside the
        /// existing 4 s victory hold).</summary>
        public const float DeathClocks=3.5f,DeathBoard=3.62f;
        /// <summary>The arrival's light cut level, how fast the lights cut and come back, the surge's decay.</summary>
        public const float CutLevel=.06f,CutFade=.18f,SurgeDecay=1.1f;
        /// <summary>The board's fall (seconds to land, with its bounce) and the barrier's.</summary>
        public const float BoardFallSeconds=.95f,BarrierFallSeconds=.35f;
        /// <summary>What the presentation drew this frame and on which clock (On false when no boss is shown).</summary>
        public static StationBossStage Shown=StationBossStage.Idle;public static double ShownClock;
        /// <summary>The stage at `time` for this view and these beats.</summary>
        public static StationBossStage Evaluate(in StationBossView v,float time,in StationBossBeats b)
        {
            var s=StationBossStage.Idle;if(!v.live)return s;s.On=true;
            float a=StationBossView.At(v.spawned)?time-v.spawned:float.PositiveInfinity;
            // ---- the arrival ----
            if(a>=b.ChimeHit&&a<b.Hold){s.Board=BoardArriving;s.BoardBlink=a<b.Drop;s.BoardFlicker=Pulse(a-b.ChimeHit,.35f);}
            // The house lights' cut and flare are the song's when it plays (NativeExpeditionStationMusic); the doc's otherwise.
            if(a>=b.LightCut&&a<b.Hold+2&&!b.Song)
            {
                float cut=Ramp((a-b.LightCut)/CutFade),back=Ramp((a-b.Drop)/(CutFade*.5f));
                s.Lights=1-(1-CutLevel)*cut*(1-back);
                if(a>=b.Drop)s.LightSurge=(float)Math.Exp(-(a-b.Drop)*SurgeDecay);
            }
            if(a>=b.LightCut&&a<b.Drop+1.5f)s.WestSignalGreen=true;
            if(a>=b.Drop)s.Shake=Math.Max(s.Shake,Impact(a-b.Drop,.9f));
            for(int i=0;i<4;i++){float k=a-b.Accent(i);if(k>=0&&k<.6f)s.Shake=Math.Max(s.Shake,.35f*Impact(k,.5f));}
            // ---- the arena changes ----
            if(v.BarrierDown(time)){s.BarrierFall=Ramp((time-v.barrierDownAt)/BarrierFallSeconds);s.Shake=Math.Max(s.Shake,Impact(time-v.barrierDownAt,.8f));s.Dust=Math.Max(s.Dust,Fade(time-v.barrierDownAt,2.5f));}
            if(v.BoardDown(time)){s.BoardFall=BoardDrop(time-v.boardDownAt);s.BoardFlicker=Math.Max(s.BoardFlicker,Fade(time-v.boardDownAt,3f));float land=time-v.boardDownAt-BoardFallSeconds*.62f;if(land>=0){s.Shake=Math.Max(s.Shake,Impact(land,1f));s.Dust=Math.Max(s.Dust,Fade(land,3f));}}
            // ---- the clocks strike: the TERMINUS change (every clock in the station), Terminus's four notes ----
            if(StationBossView.At(v.phaseAt)&&v.phase>=3){float k=time-v.phaseAt;for(int n=0;n<4;n++)s.ClockTick=Math.Max(s.ClockTick,Pulse(k-.5f-n*.6f,.25f));s.Dust=Math.Max(s.Dust,Fade(k-.5f,3f)*.6f);}
            if(v.action=="terminus"&&StationBossView.At(v.actionAt))
            {
                var m=NativeStationBoss.Move("terminus");float k=time-v.actionAt,hit=StationBossView.At(v.hitAt)?v.hitAt-v.actionAt:m.Anticipation,scale=m.Anticipation>0?hit/m.Anticipation:1;
                for(int n=0;n<NativeStationBoss.ChimeNotes;n++){float at=m.Note(n)*scale;s.ClockTick=Math.Max(s.ClockTick,Pulse(k-at,.22f));s.Dust=Math.Max(s.Dust,.4f*Fade(k-at,.8f));}
                if(k>=hit){s.Shake=Math.Max(s.Shake,Impact(k-hit,1f));s.Dust=Math.Max(s.Dust,Fade(k-hit,2.2f));}
            }
            if(StationBossView.At(v.staggerAt)&&time>=v.staggerAt)s.Shake=Math.Max(s.Shake,(v.staggerCause=="express"?1:v.staggerCause=="crash"?.8f:.3f)*Impact(time-v.staggerAt,.8f));
            // ---- the death: the clocks start again at 6:48, the board reads DEPARTED ----
            if(v.Dead(time))
            {
                float d=time-v.diedAt;s.Board=BoardOwn;s.BoardBlink=false;
                if(d>=DeathClocks){s.ClockMinute=48;s.ClockTick=Math.Max(s.ClockTick,Pulse(d-DeathClocks,.3f));}
                if(d>=DeathBoard-.25f){s.BoardFlicker=Math.Max(s.BoardFlicker,1-Ramp((d-DeathBoard)/.45f));if(d>=DeathBoard)s.Board=BoardDeparted;}
                if(d<1.4f)s.Shake=Math.Max(s.Shake,.5f*Impact(d-.9f,.7f));
            }
            return s;
        }
        private static float Ramp(float x)=>x<=0?0:x>=1?1:x*x*(3-2*x);
        /// <summary>A strike's pulse: up over the first 15% of `len`, back down by its end (0 outside).</summary>
        private static float Pulse(float t,float len)=>t<0||t>=len?0:t<len*.15f?t/(len*.15f):1-(t-len*.15f)/(len*.85f);
        private static float Fade(float t,float len)=>t<0||t>=len?0:1-t/len;
        /// <summary>An impact's shudder: sharp at the hit, decaying (0 before and after `len`).</summary>
        private static float Impact(float t,float len)=>t<0||t>=len?0:(float)Math.Exp(-t*5/len)*(1-t/len);
        /// <summary>The board's fall: swinging down off its chains, landing at .62 of the time, a small bounce, lying still.</summary>
        public static float BoardDrop(float t)
        {
            if(t<=0)return 0;float u=t/BoardFallSeconds;if(u>=1)return 1;
            if(u<.62f){float k=u/.62f;return k*k;}
            float b=(u-.62f)/.38f;return 1-.06f*(float)Math.Sin(Math.PI*b)*(1-b);
        }
    }
}
