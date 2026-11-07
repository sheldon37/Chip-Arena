using System;

namespace ChipArena.UnityConversion
{
    /// <summary>What the ward's lighting reads each frame (all from replicated state: the match phase and clock, the PvE stage,
    /// the boss phase and possession, the boss song's clock), so host, guests and the killcam agree.</summary>
    public struct NativeWardMoodInput
    {
        public NativePhase Phase;public bool Pve,Hidden,Holding;public string Stage,Cinematic;public int Wave,BossPhase;
        /// <summary>Seconds since FightAt (negative before it).</summary>
        public double FightAge;
        /// <summary>The boss song: playing, its section id ("build", "groove", "lift", "breather", "climax", "outro"), the beat in
        /// the bar (1..4) and the beat's phase 0..1.</summary>
        public bool Song;public string Section;public int BeatInBar;public double BeatPhase;
        /// <summary>The loudest hazard alarm this frame (0..1): a shutter's klaxon, a decon cycle, a burner's warning nearby.</summary>
        public float Alarm;
    }
    /// <summary>The ward's lighting state. The four states the proposal names (calm, horde, boss, emergency) are weights that
    /// sum to 1; the channels below are what the kit, the effects and the practical lights read.</summary>
    public struct NativeWardMood
    {
        public float Calm,Horde,Boss,Emergency;
        /// <summary>Fluorescent level (1 = full clinical white), the share of fixtures that misbehave, the red emergency strobe,
        /// the red cast over everything, the Siren's ink corruption (0 clean .. 1 the whole ward), the haze density, the beat
        /// pulse (1 on the beat, decaying), the downbeat pulse, and how strongly the lights follow the song.</summary>
        public float Fluor,Flicker,Strobe,Red,Ink,Haze,Beat,Down,Groove;
    }
    /// <summary>
    /// Ward Zero's lighting mood (docs/arenas/WARDZERO_PROPOSAL.md section 10.3), upgrading today's song-synced hall
    /// (NativeWardPresentation's moods by song section) with the game state:
    /// <list type="bullet">
    /// <item>Calm: PvP and the Sandbox at rest, the PvE opening and intermissions, after a victory. Clinical white, a few
    /// fixtures flicker, light haze; in PvP the ink creeps in over the round.</item>
    /// <item>Horde: a PvE wave. Cooler and dimmer, more fixtures fail, slow amber sweeps, the haze thickens with the wave.</item>
    /// <item>Boss: the Siren's fight. Low white, violet ink light rising with her phase, the lights pump on the beat.</item>
    /// <item>Emergency: her phases 3 and 4, the "Find me..." hunt (possession) and a defeat: red strobes, heavy flicker.</item>
    /// </list>
    /// A hazard alarm (shutters, decon) pushes toward Emergency locally in any state. The song's section shapes the pulse
    /// (build, groove, lift, breather, climax, outro, as today's hall). <see cref="Target"/> is pure; <see cref="Ease"/> eases
    /// a state toward it (about a bar), as today's hall eases its section mood. Allocation free.
    /// </summary>
    public static class NativeWardZeroMood
    {
        public static NativeWardMood Target(in NativeWardMoodInput i)
        {
            float calm=0,horde=0,boss=0,emergency=0;bool fight=i.Phase==NativePhase.Fight||i.Phase==NativePhase.Result;
            if(i.Pve)
            {
                string st=i.Stage??"";
                if(i.Holding&&i.Cinematic=="defeat")emergency=1;
                else if(i.Holding&&i.Cinematic=="victory"||st=="ended")calm=1;
                else if(st=="boss"||i.Holding&&(i.Cinematic=="intro"||i.Cinematic=="reveal")){if(i.Hidden||i.BossPhase>=3)emergency=1;else boss=1;}
                else if(st=="wave")horde=1;else calm=1;
            }
            else calm=1;
            // A hazard alarm takes over a share of the room's look (never all of it: the room stays readable).
            float alarm=Clamp01(i.Alarm)*.55f;calm*=1-alarm;horde*=1-alarm;boss*=1-alarm;emergency+=alarm*(1-emergency);
            float sum=calm+horde+boss+emergency;if(sum<=0){calm=1;sum=1;}calm/=sum;horde/=sum;boss/=sum;emergency/=sum;
            var m=new NativeWardMood{Calm=calm,Horde=horde,Boss=boss,Emergency=emergency};
            m.Fluor=calm*1f+horde*.78f+boss*.5f+emergency*.26f;
            m.Flicker=calm*.06f+horde*.22f+boss*.3f+emergency*.65f;
            m.Strobe=horde*.18f+boss*.25f+emergency*1f;
            m.Red=horde*.08f+boss*.15f+emergency*.85f;
            m.Haze=calm*.7f+horde*(.85f+.05f*Math.Min(3,i.Wave))+boss*1f+emergency*1.15f;
            m.Ink=Corruption(i);
            // The song: the beat pulse (and the downbeat) while it plays, shaped by its section; breathers calm the pulse.
            if(i.Song)
            {
                float ph=(float)Clamp01(i.BeatPhase),pulse=(1-ph)*(1-ph)*(1-ph);
                float g=SectionGroove(i.Section,out float strobe,out float flick);
                m.Groove=g;m.Beat=pulse*g;m.Down=i.BeatInBar==1?pulse*g:0;
                m.Strobe=Math.Max(m.Strobe,strobe*(boss+emergency));m.Flicker=Math.Min(1,m.Flicker+flick*(boss+emergency));
            }
            if(!fight&&!i.Pve){m.Ink*=.5f;}
            return m;
        }
        /// <summary>The section's pulse depth, strobe and extra flicker (today's hall: build, groove, lift, breather, climax, outro).</summary>
        public static float SectionGroove(string section,out float strobe,out float flicker)
        {
            switch(section)
            {
                case "groove":strobe=.2f;flicker=.05f;return .65f;
                case "lift":strobe=.55f;flicker=.15f;return .9f;
                case "breather":strobe=0;flicker=0;return .15f;
                case "climax":strobe=1;flicker=.3f;return 1;
                case "outro":strobe=.1f;flicker=.05f;return .4f;
                default:strobe=.05f;flicker=0;return .35f;   // build and anything unknown
            }
        }
        /// <summary>How far the Siren's ink has spread (0..1): in PvE by the wave and then the boss phase (a quarter a phase,
        /// more while she hides in an add), in PvP over the fight's first 150 s up to half.</summary>
        public static float Corruption(in NativeWardMoodInput i)
        {
            if(i.Pve)
            {
                string st=i.Stage??"";if(i.Holding&&i.Cinematic=="victory"||st=="ended")return .15f;
                if(st=="boss"||i.Holding&&(i.Cinematic=="intro"||i.Cinematic=="reveal"))return Clamp01(.25f*Math.Max(1,i.BossPhase)+(i.Hidden?.15f:0));
                return Clamp01(.06f+.06f*Math.Max(0,i.Wave));
            }
            if(i.Phase!=NativePhase.Fight&&i.Phase!=NativePhase.Result)return .12f;
            return Clamp01(.12f+.38f*(float)Math.Max(0,Math.Min(1,i.FightAge/150)));
        }
        /// <summary>Eases `m` toward `target` over about a bar (rate 2.2 /s, today's hall), the beat channels following at once.</summary>
        public static void Ease(ref NativeWardMood m,in NativeWardMood target,float dt)
        {
            float k=1-(float)Math.Exp(-Math.Max(0,dt)*2.2f),ki=1-(float)Math.Exp(-Math.Max(0,dt)*.6f);
            m.Calm+=(target.Calm-m.Calm)*k;m.Horde+=(target.Horde-m.Horde)*k;m.Boss+=(target.Boss-m.Boss)*k;m.Emergency+=(target.Emergency-m.Emergency)*k;
            m.Fluor+=(target.Fluor-m.Fluor)*k;m.Flicker+=(target.Flicker-m.Flicker)*k;m.Strobe+=(target.Strobe-m.Strobe)*k;m.Red+=(target.Red-m.Red)*k;m.Haze+=(target.Haze-m.Haze)*k;
            m.Ink+=(target.Ink-m.Ink)*ki;m.Groove+=(target.Groove-m.Groove)*k;m.Beat=target.Beat;m.Down=target.Down;
        }
        /// <summary>A named preset (calm, horde, boss, emergency) for previews, the Dev Sandbox and the renders.</summary>
        public static NativeWardMood Preset(string name,double beatPhase=1,int beatInBar=2)
        {
            var i=new NativeWardMoodInput{Phase=NativePhase.Fight,Pve=name!="pvp",Stage=name=="horde"?"wave":name=="boss"||name=="emergency"?"boss":"opening",Wave=2,BossPhase=name=="emergency"?3:name=="boss"?2:0,
                Song=name=="boss"||name=="emergency",Section=name=="emergency"?"climax":"groove",BeatPhase=beatPhase,BeatInBar=beatInBar,FightAge=60};
            return Target(i);
        }
        private static float Clamp01(float v)=>v<0?0:v>1?1:v;
        private static double Clamp01(double v)=>v<0?0:v>1?1:v;
    }
}
