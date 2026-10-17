using System;

namespace ChipArena.UnityConversion
{
    /// <summary>One City Ironworks hazard as the presentation draws it (docs/EXPEDITION-CITY-IRONWORKS.md section 5): its id, kind,
    /// volume (centre and full size, world m), cycle (warn, live and period in s, offset into the instance's hazard clock), the
    /// presentation's cool-down after the live window (`Cool`, harmless: a runner crusting over, the slag cooling), the half-rate
    /// cycle Wave 1 runs (`HalfPeriod`, `HalfWarn`), the first wave it runs in (`From`; 1 for Wave 1) and its boss-encounter rule
    /// (`Boss`: <see cref="NativeIronworksHazards.BossOn"/>, Off or Driven).</summary>
    public struct IronworksHazardDef
    {
        public string Id;public byte Kind;public float X,Y,Z,Sx,Sy,Sz,Warn,Live,Period,Offset,Cool,HalfPeriod,HalfWarn;public byte Wave1,From,Boss;
    }
    /// <summary>What drives the hazards' presentation this frame: the instance's hazard clock (s since its schedule began; Astra's
    /// authority will publish it with the phase), whether it runs, the wave (1..4) and whether the boss encounter is on. The DEV
    /// preview fills one from the presentation clock; with nothing bound every hazard rests (Idle).</summary>
    public struct IronworksHazardInput {public bool Running,Boss;public double Clock;public int Wave;}
    /// <summary>A hazard's state for the presentation: phase (Idle, Warn, Active = the live window, Clear = the harmless cool-down),
    /// its cycle (-1 before the first), how long the phase has run and has left, a 0..1 level (the warning's build, 1 while live,
    /// the cool-down's fade), the rate it runs at (<see cref="NativeIronworksHazards.RateOff"/>, Half or Full) and `Parked` (the
    /// ladle crane during the boss encounter: hoisted, upright, no pour).</summary>
    public struct IronworksHazardView
    {
        public NativeHazardPhase Phase;public int Cycle;public double Since,Until;public float Level;public byte Rate;public bool Parked;
        public bool Live=>Phase==NativeHazardPhase.Active;public bool Warning=>Phase==NativeHazardPhase.Warn;
    }

    /// <summary>
    /// The City Ironworks' hazard views (Claude, 2026-10-16; presentation only, docs/EXPEDITION-CITY-IRONWORKS.md section 5 and
    /// owner decision 10). Astra owns the hazards' authority, damage and replication; this derives what the presentation needs
    /// from the instance's hazard clock and phase (<see cref="IronworksHazardInput"/>), as the Factory's hazards derive theirs from
    /// the match clock, so host, guests and the killcam agree and nothing is sent.
    /// <list type="bullet">
    /// <item>Cycle k's warning starts at Offset + k x period: Warn for `warn` s, Active (the live window) for `live` s, Clear
    /// (the presentation's cool-down, harmless) for `cool` s, then Idle until the next warning.</item>
    /// <item>Wave 1 runs light (owner, 2026-10-16): runner r2 is the one live runner (r1 and r3 plugged, a dull red glow), the
    /// pour at half rate (every 52 s, warn 3.5 s), the quench steam at half rate (every 44 s), the drop hammer as always; the hot
    /// slab and the flying shear join in Wave 2 with r1 and r3, the slag spill and the pusher in Wave 3.</item>
    /// <item>The boss encounter: everything runs except the pour, which stops (the ladle crane parks: <see cref="IronworksHazardView.Parked"/>).
    /// The converter tilt is the boss's (a stub here: always Idle until the boss design drives it).</item>
    /// </list>
    /// Pure and allocation free (struct results; <see cref="Evaluate"/> fills a caller's array).
    /// </summary>
    public static class NativeIronworksHazards
    {
        public const byte KindRunner=0,KindCrane=1,KindPress=2,KindSlag=3,KindSlab=4,KindShear=5,KindVent=6,KindPusher=7,KindConverter=8;
        public const byte RateOff=0,RateHalf=1,RateFull=2;
        /// <summary>Wave 1 schedule values and the boss rule.</summary>
        public const byte Wave1Off=0,Wave1Half=1,Wave1On=2,BossOff=0,BossOn=1,BossDriven=2;
        public const int R1=0,R2=1,R3=2,Ladle=3,Hammer=4,Slag=5,Slab=6,Shear=7,Steam=8,Pusher=9,Converter=10,Count=11;
        /// <summary>The section 5 list in the draft's order (docs/arenas/scripts/expedition_ironworks.py `hazards`); the managed
        /// check holds each id, volume and cycle to the draft record.</summary>
        public static readonly IronworksHazardDef[] Defs=
        {
            Def("runner-r1",KindRunner,-15,.3f,20,1.2f,.6f,16,2,6,34,0,5,0,0,Wave1Off,2,BossOn),
            Def("runner-r2",KindRunner,0,.3f,20,1.2f,.6f,16,2,6,34,11,5,0,0,Wave1On,1,BossOn),
            Def("runner-r3",KindRunner,15,.3f,20,1.2f,.6f,16,2,6,34,22,5,0,0,Wave1Off,2,BossOn),
            Def("ladle-crane",KindCrane,5,2,-6,6,4,5,2.5f,3,26,0,1.6f,52,3.5f,Wave1Half,1,BossOff),
            Def("drop-hammer",KindPress,-29.5f,4.5f,-28.4f,2.4f,9,2.4f,1.2f,.35f,6,0,.4f,0,0,Wave1On,1,BossOn),
            Def("slag-spill",KindSlag,-36,.3f,-24.5f,6,.6f,4,3,10,40,0,10,0,0,Wave1Off,3,BossOn),
            Def("hot-slab",KindSlab,4.5f,1.4f,-25,49,1,2.4f,1.5f,9,18,0,.8f,0,0,Wave1Off,2,BossOn),
            Def("flying-shear",KindShear,-15.5f,2.3f,-25,.4f,2,2.6f,1,.6f,9,0,.6f,0,0,Wave1Off,2,BossOn),
            Def("quench-steam",KindVent,-44,3,18,4,6,4,1.5f,3,22,0,2.5f,44,1.5f,Wave1Half,1,BossOn),
            Def("pusher",KindPusher,-37,1.8f,26,3,3.6f,3.1f,3,6,30,0,2,0,0,Wave1Off,3,BossOn),
            Def("converter-tilt",KindConverter,-39,.3f,7,4,.6f,4,2.5f,4,0,0,0,0,0,Wave1Off,99,BossDriven),
        };
        public const string IdPrefix=NativeIronworksKit.Prefix;
        private static IronworksHazardDef Def(string id,byte kind,float x,float y,float z,float sx,float sy,float sz,float warn,float live,float period,float offset,float cool,float halfPeriod,float halfWarn,byte wave1,byte from,byte boss)
        =>new IronworksHazardDef{Id=IdPrefix+id,Kind=kind,X=x,Y=y,Z=z,Sx=sx,Sy=sy,Sz=sz,Warn=warn,Live=live,Period=period,Offset=offset,Cool=cool,HalfPeriod=halfPeriod,HalfWarn=halfWarn,Wave1=wave1,From=from,Boss=boss};

        /// <summary>The rate hazard `i` runs at for this input: off before the schedule runs, then by wave (Wave 1's light set, the
        /// rest from their first wave) and in the boss encounter by its boss rule.</summary>
        public static byte RateOf(int i,in IronworksHazardInput input)
        {
            if(!input.Running||i<0||i>=Count)return RateOff;var d=Defs[i];
            if(input.Boss)return d.Boss==BossOn?RateFull:RateOff;
            int wave=Math.Max(1,input.Wave);if(wave==1)return d.Wave1==Wave1On?RateFull:d.Wave1==Wave1Half?RateHalf:RateOff;
            return wave>=d.From?RateFull:RateOff;
        }
        /// <summary>Hazard `i`'s view for this input (pure).</summary>
        public static IronworksHazardView At(int i,in IronworksHazardInput input)
        {
            var v=new IronworksHazardView{Phase=NativeHazardPhase.Idle,Cycle=-1};if(i<0||i>=Count)return v;var d=Defs[i];
            v.Rate=RateOf(i,input);v.Parked=i==Ladle&&input.Running&&input.Boss;if(v.Rate==RateOff)return v;
            double period=v.Rate==RateHalf&&d.HalfPeriod>0?d.HalfPeriod:d.Period,warn=v.Rate==RateHalf&&d.HalfWarn>0?d.HalfWarn:d.Warn;if(!(period>0))return v;
            double u=input.Clock-d.Offset;if(double.IsNaN(u))return v;if(u<0){v.Until=-u;return v;}
            double c=Math.Floor(u/period);v.Cycle=c>int.MaxValue?int.MaxValue:(int)c;u-=c*period;double a=d.Live,cool=d.Cool;
            if(u<warn){v.Phase=NativeHazardPhase.Warn;v.Since=u;v.Until=warn-u;v.Level=(float)Clamp01(u/Math.Max(.001,warn));}
            else if(u<warn+a){v.Phase=NativeHazardPhase.Active;v.Since=u-warn;v.Until=warn+a-u;v.Level=1;}
            else if(u<warn+a+cool){v.Phase=NativeHazardPhase.Clear;v.Since=u-warn-a;v.Until=warn+a+cool-u;v.Level=(float)Clamp01(1-(u-warn-a)/Math.Max(.001,cool));}
            else{v.Phase=NativeHazardPhase.Idle;v.Since=u-warn-a-cool;v.Until=period-u;}
            return v;
        }
        /// <summary>Every hazard's view into `into` (Count long).</summary>
        public static void Evaluate(in IronworksHazardInput input,IronworksHazardView[] into){if(into==null)return;int n=Math.Min(Count,into.Length);for(int i=0;i<n;i++)into[i]=At(i,input);}
        /// <summary>The period hazard `i` runs at for this rate (0 when off).</summary>
        public static double PeriodOf(int i,byte rate){if(i<0||i>=Count||rate==RateOff)return 0;var d=Defs[i];return rate==RateHalf&&d.HalfPeriod>0?d.HalfPeriod:d.Period;}
        /// <summary>The hazard's index by id (with or without the map prefix), or -1.</summary>
        public static int Index(string id){if(id==null)return -1;for(int i=0;i<Count;i++)if(Defs[i].Id==id||Defs[i].Id==IdPrefix+id)return i;return -1;}
        /// <summary>The DEV preview's input: the presentation clock as the hazard clock, the chosen stage (0 off, 1..4 a wave, 5
        /// the boss encounter).</summary>
        public static IronworksHazardInput Preview(int stage,double clock)=>new IronworksHazardInput{Running=stage>0,Boss=stage>=5,Wave=Math.Min(4,Math.Max(1,stage)),Clock=clock};
        public static string StageName(int stage)=>stage<=0?"off":stage>=5?"boss encounter":"wave "+stage;
        private static double Clamp01(double v)=>v<0?0:v>1?1:v;
    }
}
