using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The arrival's beats on the shared boss clock (s since the arrival; docs/EXPEDITION-BOSS-CITY-IRONWORKS.md section 7.1, read
    /// from Hydraulic Hell's map in docs/ZONE-BOSS-MUSIC.md): the first hydraulic hit (the intro's first slam: the crust splits,
    /// the tongs grip the rim), the downbeat after it (the heave out of the pit), the second slam (the rear legs climb out), the
    /// furnace flare (the last downbeat before the drop, the stoke's entry the stage's furnaces flare on: the lamps click on, the
    /// visor lights, the mouth flares), the drop (the first blow, the fight) and the hold's end. Presentation only.
    /// </summary>
    public struct IronworksBossBeats
    {
        public float Hit,Heave,Climb,Flare,Drop,Hold;
        /// <summary>Hydraulic Hell's beats as its committed map gives them (the same numbers <see cref="From"/> reads from it): used
        /// only when the map cannot be read.</summary>
        public static readonly IronworksBossBeats Fallback=new IronworksBossBeats{Hit=3.4227f,Heave=3.8574f,Climb=5.2710f,Flare=7.3342f,Drop=9.0678f,Hold=9.0678f};
        public bool Valid=>Hit>.5f&&Heave>Hit&&Climb>Heave&&Flare>Climb&&Drop>Flare&&Hold>=Drop&&Hold<30;
        /// <summary>The beats from a zone boss song's map: its first two intro slams heard, the downbeats heard between them and
        /// before the drop, the drop at the hold's end. Invalid (Hit 0) when the map has no such accents.</summary>
        public static IronworksBossBeats From(NativeZoneBossSong song)
        {
            var b=new IronworksBossBeats();if(song?.Track?.beats==null)return b;
            double hold=song.Hold,s1=double.NaN,s2=double.NaN;
            foreach(var a in song.Accents)
            {
                if(a.Kind!=NativeZoneBossSong.AccentSlam)continue;double at=song.FirstBoss(a.T);if(double.IsNaN(at)||at<=.5||at>=hold)continue;
                if(double.IsNaN(s1)||at<s1){s2=s1;s1=at;}else if(double.IsNaN(s2)||at<s2)s2=at;
            }
            if(double.IsNaN(s1)||double.IsNaN(s2))return b;
            double heave=double.NaN,flare=double.NaN;
            for(int bar=1;bar<=song.DropBar+1;bar++)
            {
                double at=song.FirstBoss(song.Downbeat(bar));if(double.IsNaN(at)||at>=hold-.05)continue;
                if(at>s1+.05&&at<s2&&double.IsNaN(heave))heave=at;if(at>s2&&(double.IsNaN(flare)||at>flare))flare=at;
            }
            if(double.IsNaN(heave))heave=(s1+s2)/2;if(double.IsNaN(flare))flare=(s2+hold)/2;
            b.Hit=(float)s1;b.Heave=(float)heave;b.Climb=(float)s2;b.Flare=(float)flare;b.Drop=(float)hold;b.Hold=(float)hold;return b;
        }
    }

    /// <summary>The beats the presentation plays the arrival on: Hydraulic Hell's (bound once from its map), else the fallback.</summary>
    public static class NativeIronworksBossBeats
    {
        private static IronworksBossBeats bound;private static bool has;
        public static IronworksBossBeats Current=>has?bound:IronworksBossBeats.Fallback;
        public static bool Bound=>has;
        /// <summary>Binds the beats read from `song` (the city-ironworks-boss map); false (and the fallback kept) when they are invalid.</summary>
        public static bool UseSong(NativeZoneBossSong song){var b=IronworksBossBeats.From(song);if(!b.Valid)return false;bound=b;has=true;return true;}
        public static void Clear(){has=false;}
    }

    /// <summary>
    /// What THE CONVERTER does to the Ironworks' stage, on top of the music's choreography (docs/ZONE-BOSS-MUSIC.md; the Ironworks
    /// presentation applies it to the song's beat before it drives the furnaces, lights and steam: Runtime NativeIronworksBossPresentation
    /// IronworksBossMusic). Presentation only: no hazard, lamp of a hazard, telegraph or timing reads it.
    /// <list type="bullet">
    /// <item><c>Heat</c>: the furnaces' and molten glow's multiplier (the death: the shift ends and the foundry stops, the furnaces
    /// sinking to a third over two seconds as the whistle blows).</item>
    /// <item><c>Lamps</c>: the work lights' multiplier (they sag with the foundry; never below .75, so the mercury slots keep the
    /// hall readable).</item>
    /// <item><c>Whistle</c>: the shift whistle's steam (0..1), <c>Wake</c>: each hall vessel's wake flare (0..1: the call, the shed's
    /// jolt), <c>Exhale</c>: the vessels' exhale in the death.</item>
    /// <item><c>MusicDuck</c>: the music's level under the blow's roar and the call's bellow (-4 dB), and its fade before the
    /// whistle (the presentation ducks the playing music this frame; NativeAudio's routing is unchanged).</item>
    /// </list>
    /// </summary>
    public struct IronworksBossStage
    {
        public bool On;public float Heat,Lamps,Whistle,WakeA,WakeB,Exhale,MusicDuck,Shake;
        public static readonly IronworksBossStage Idle=new IronworksBossStage{On=false,Heat=1,Lamps=1,MusicDuck=1};
    }

    public static class NativeIronworksBossStage
    {
        /// <summary>The death's stage times (s from the death's start, inside the 4 s victory hold): the whistle blows, the foundry stops.</summary>
        public const float WhistleAt=2.55f,StopAt=2.3f,StopSeconds=1.9f,ExhaleAt=2.9f;
        /// <summary>The last stage the presentation evaluated (the Ironworks presentation reads it; Idle when no boss shows).</summary>
        public static IronworksBossStage Shown=IronworksBossStage.Idle;
        /// <summary>The stage for the record at `t`, the death's age `death` (NaN alive) and the rig as drawn.</summary>
        public static IronworksBossStage Evaluate(in IronworksBossView v,float t,float death,IronworksBossRig rig)
        {
            var s=IronworksBossStage.Idle;if(!v.live)return s;s.On=true;
            if(!float.IsNaN(death))
            {
                float stop=BossMath.Smoother((death-StopAt)/StopSeconds);s.Heat=1-.66f*stop;s.Lamps=1-.25f*stop;
                float w=death-WhistleAt;s.Whistle=w<0?0:w<.2f?w/.2f:w<2.6f?1:Math.Max(0,1-(w-2.6f)/1.2f);
                float e=death-ExhaleAt;s.Exhale=e<0?0:e<.4f?e/.4f:Math.Max(0,1-(e-.4f)/2.2f);
                s.MusicDuck=1-.92f*BossMath.Smoother((death-(WhistleAt-.5f))/.5f);
            }
            // The call wakes the vessels it names (bit 0 A north, bit 1 B south); the shed jolts both awake.
            if(v.action=="call"&&IronworksBossView.At(v.actionAt))
            {
                var a=NativeIronworksBoss.Find("call");float k=t-(v.actionAt+a.Windup);float wake=k<0?0:k<.25f?k/.25f:Math.Max(0,1-(k-.25f)/2.6f);
                if((v.variant&1)!=0)s.WakeA=Math.Max(s.WakeA,wake);if((v.variant&2)!=0)s.WakeB=Math.Max(s.WakeB,wake);
                if(k>-.05f&&k<1.0f)s.MusicDuck=Math.Min(s.MusicDuck,.63f);
            }
            if(rig!=null&&rig.PhaseAct.Active&&rig.PhaseKind==2){float k=rig.PhaseAct.Age-1.55f;float wake=k<0?0:k<.2f?k/.2f:Math.Max(0,1-(k-.2f)/2.4f);s.WakeA=Math.Max(s.WakeA,wake);s.WakeB=Math.Max(s.WakeB,wake);}
            if(v.action=="blow"&&IronworksBossView.At(v.actionAt)){var a=NativeIronworksBoss.Find("blow");float k=t-(v.actionAt+a.Windup);if(k>-.1f&&k<2.1f)s.MusicDuck=Math.Min(s.MusicDuck,.63f);}
            if(rig!=null&&rig.Arriving){float k=rig.ArrivalAge-NativeIronworksBossBeats.Current.Drop;if(k>=0&&k<1.4f)s.MusicDuck=Math.Min(s.MusicDuck,.8f);}
            return s;
        }
        /// <summary>The song's beat with the boss's stage on top (the furnaces' heat, the lights' level): the Ironworks presentation's input.</summary>
        public static NativeZoneBossBeat Apply(in NativeZoneBossBeat music,in IronworksBossStage s)
        {
            if(!s.On||!music.On)return music;var m=music;m.Mood.Heat*=s.Heat;m.Mood.Light*=s.Lamps;return m;
        }
    }
}
