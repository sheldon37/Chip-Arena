using System;

namespace ChipArena.UnityConversion
{
    /// <summary>One falling-glass event the presentation draws (docs/EXPEDITION-CENTRAL-STATION.md section 6): where the shard
    /// lands (world x, z on the concourse floor) and when it lands (`At`, arena seconds). The shadow grows over the
    /// <see cref="NativeExpeditionStation.GlassTelegraph"/> seconds before `At`; the shards burst at `At`.</summary>
    public struct NativeStationGlass {public float X,Z;public double At;}

    /// <summary>
    /// The station's presentation inputs (docs/EXPEDITION-CENTRAL-STATION.md section 8, "Networking: what the presentation
    /// needs"). Astra's replicated hazard and objective fields fill it on every peer; until they exist only the DEV preview
    /// (<see cref="NativeStationPreview"/>) does, and an untouched view reads as an idle station (no express scheduled, every
    /// third-rail section live, the flood dead, the levers home). Times are arena seconds (the clock `arena.Time` runs on).
    /// Nothing here is authority: the presentation reads it and never writes it back.
    /// <list type="bullet">
    /// <item>`TrainAt`, `TrainLine`: the express's next (or current) pass start and its line (1 = T1, the only express line).
    /// The warning runs the <see cref="NativeExpeditionStation.WarnSeconds"/> before it, the pass the
    /// <see cref="NativeExpeditionStation.PassSeconds"/> after it. NaN: none scheduled.</item>
    /// <item>`RailLive` (bit per section, <see cref="NativeExpeditionStation"/> Rail* order) and `RailChangedAt`: the third
    /// rail's sections as the authority powers them.</item>
    /// <item>`FloodLive`: the flood conducts (the rail arcs into it, or a Linesman is grounded in it).</item>
    /// <item>`Levers`, `LeversAt`, `LeversUntil`: the signal box lever objective: thrown at `LeversAt`, power back at `LeversUntil`.</item>
    /// <item>`LastCallAt`: a Conductor's last call (the board's row 0 reads LAST CALL for <see cref="NativeExpeditionStation.LastCallSeconds"/>).</item>
    /// <item>`Boss`: the boss has arrived (the board reads ALL SERVICES CANCELLED).</item>
    /// <item>`Glass` (`GlassCount` used): falling glass in the boss encounter.</item>
    /// <item>`Cabinets` (P2, P3) and `Steam`: the spark cabinets' and the steam main's phases as the Ward `grid` and `gas` kinds
    /// give them (Warn: the fizz or the hiss; Active: the arc or the scald).</item>
    /// </list>
    /// </summary>
    public sealed class NativeStationView
    {
        public double TrainAt=double.NaN;public int TrainLine=1;
        public int RailLive=NativeExpeditionStation.AllRails;public double RailChangedAt=double.NegativeInfinity;
        public bool FloodLive;
        public bool Levers;public double LeversAt=double.NaN,LeversUntil=double.NaN;
        public double LastCallAt=double.NaN;
        public bool Boss;public double BossAt=double.NaN;
        public readonly NativeStationGlass[] Glass=new NativeStationGlass[NativeExpeditionStation.MaxGlass];public int GlassCount;
        public readonly NativeHazardState[] Cabinets=new NativeHazardState[2];public NativeHazardState Steam;
        /// <summary>True while the DEV preview fills the view (the HUD says so; nothing else changes).</summary>
        public bool Preview;
        /// <summary>Back to an idle station.</summary>
        public void Reset()
        {
            TrainAt=double.NaN;TrainLine=1;RailLive=NativeExpeditionStation.AllRails;RailChangedAt=double.NegativeInfinity;FloodLive=false;Levers=false;LeversAt=LeversUntil=double.NaN;
            LastCallAt=double.NaN;Boss=false;BossAt=double.NaN;GlassCount=0;Cabinets[0]=Cabinets[1]=default;Steam=default;Preview=false;
        }
    }

    /// <summary>
    /// What the station's presentation draws at one moment, derived from the view and the clock (<see cref="Evaluate"/>): the
    /// express's phase and where its nose is, the headlight's growth in the west mouth, the signals, the rail and flood power,
    /// the levers' pose, the departure board's rows and blink, the falling glass. Pure and allocation free.
    /// </summary>
    public sealed class NativeStationState
    {
        public const byte ExpressIdle=0,ExpressWarn=1,ExpressPass=2,ExpressAfter=3,ExpressHalted=4;
        public const byte SignalRed=0,SignalGreen=1,SignalDark=2;
        /// <summary>The departure board's variants: row 0's status, row 4's status, the title.</summary>
        public const byte Row0Delayed=0,Row0LastCall=1,Row0Cancelled=2,Row4NoStop=0,Row4Approaching=1,Row4Suspended=2,Row4Cancelled=3,TitleDepartures=0,TitleCancelled=1;
        public byte Express;public double ExpressAge;
        /// <summary>The warning's progress (0 at its start, 1 at the pass).</summary>
        public float Warn;
        /// <summary>The nose's world x (east of the west mouth while it passes; far west of it before).</summary>
        public float Nose;
        /// <summary>The headlight's strength (0 dark .. 1 at the mouth) and the pass's own (1 while the train is in the station).</summary>
        public float Headlight,Passing;
        /// <summary>After the tail leaves: 1 falling to 0 over the dust's settle.</summary>
        public float Wake;
        public byte Signal;
        /// <summary>The danger lamps and beacons' strength (0 off; 1 flashing through the warning and the pass).</summary>
        public float Danger;
        public int Rails;public float RailCut;
        public bool Flood;public float FloodGlow;
        public bool Levers;public float LeverPose;
        public byte Row0,Row4,Title;public bool Blink;
        public int Glass;
        /// <summary>The spark cabinets' (P2, P3) and the steam main's phases and their levels (0..1 through the phase).</summary>
        public readonly NativeHazardPhase[] CabinetPhase=new NativeHazardPhase[2];public readonly float[] CabinetLevel=new float[2];public NativeHazardPhase SteamPhase;public float SteamLevel;
        /// <summary>The line the express runs on (1..3) and its pit's centre z (filled by the kit's geometry: <see cref="NativeExpeditionStation.LineZ"/>).</summary>
        public int Line=1;

        /// <summary>Fills this state from `v` at arena time `t`.</summary>
        public void Evaluate(NativeStationView v,double t)
        {
            v=v??Idle;Line=v.TrainLine<1||v.TrainLine>3?1:v.TrainLine;
            Levers=v.Levers&&!(t>=v.LeversUntil);double sinceLevers=t-v.LeversAt;LeverPose=Levers?(float)Clamp01(double.IsNaN(sinceLevers)?1:sinceLevers/NativeExpeditionStation.LeverThrowSeconds):0;
            // The express: halted while the levers hold (the schedule resumes after), else by the pass start.
            Express=ExpressIdle;ExpressAge=0;Warn=0;Headlight=0;Passing=0;Wake=0;Nose=NativeExpeditionStation.NoseStart;Danger=0;
            double at=v.TrainAt;
            if(Levers)Express=ExpressHalted;
            else if(!double.IsNaN(at))
            {
                double w=NativeExpeditionStation.WarnSeconds,p=NativeExpeditionStation.PassSeconds,s=t-at;
                if(s>=-w&&s<0){Express=ExpressWarn;ExpressAge=s+w;Warn=(float)((s+w)/w);float k=Warn;Headlight=.3f+.7f*k*k;Danger=1;Nose=(float)(NativeExpeditionStation.NoseStart+s*NativeExpeditionStation.Speed);}
                else if(s>=0&&s<=p){Express=ExpressPass;ExpressAge=s;Passing=1;Headlight=1;Danger=1;Nose=(float)(NativeExpeditionStation.NoseStart+s*NativeExpeditionStation.Speed);}
                else if(s>p&&s<p+NativeExpeditionStation.WakeSeconds){Express=ExpressAfter;ExpressAge=s-p;Wake=(float)(1-(s-p)/NativeExpeditionStation.WakeSeconds);Nose=(float)(NativeExpeditionStation.NoseStart+s*NativeExpeditionStation.Speed);}
            }
            Signal=Levers?SignalDark:Express==ExpressWarn||Express==ExpressPass?SignalGreen:SignalRed;
            // Power: the levers cut every section; the glints die over a second after any cut.
            Rails=Levers?0:v.RailLive&NativeExpeditionStation.AllRails;double since=Levers?sinceLevers:t-v.RailChangedAt;RailCut=(float)Clamp01(double.IsNaN(since)?1:since);
            Flood=v.FloodLive&&!Levers;FloodGlow=Flood?(float)(.75+.25*Math.Sin(t*7.3)*Math.Sin(t*2.9+1)):0;
            // The board (doc section 3, "The station's text"): the boss cancels everything; else the levers suspend the express,
            // its warning and pass blink APPROACHING; a last call blinks on row 0.
            Blink=Frac(t*NativeExpeditionStation.BlinkHz)<.62;
            bool boss=v.Boss;bool lastCall=!double.IsNaN(v.LastCallAt)&&t>=v.LastCallAt&&t<v.LastCallAt+NativeExpeditionStation.LastCallSeconds;
            Title=boss?TitleCancelled:TitleDepartures;
            Row0=boss?Row0Cancelled:lastCall?Row0LastCall:Row0Delayed;
            Row4=boss?Row4Cancelled:Levers?Row4Suspended:Express==ExpressWarn||Express==ExpressPass?Row4Approaching:Row4NoStop;
            for(int i=0;i<2;i++){CabinetPhase[i]=v.Cabinets[i].Phase;CabinetLevel[i]=v.Cabinets[i].Level;}SteamPhase=v.Steam.Phase;SteamLevel=v.Steam.Level;
            int g=0;for(int i=0;i<v.GlassCount&&i<v.Glass.Length;i++){double s=t-v.Glass[i].At;if(s>-NativeExpeditionStation.GlassTelegraph&&s<NativeExpeditionStation.GlassSettle)g++;}Glass=g;
        }
        /// <summary>Whether a board variant is lit this frame: blinking rows go dark on the blink's off beat.</summary>
        public bool BoardRowLit(int row){if(row==0&&Row0==Row0LastCall)return Blink;if(row==4&&Row4==Row4Approaching)return Blink;return true;}
        private static readonly NativeStationView Idle=new NativeStationView();
        private static double Clamp01(double x)=>x<0?0:x>1?1:x;
        internal static double Frac(double x)=>x-Math.Floor(x);
    }

    /// <summary>
    /// Central Station (its map id and look name are <see cref="MapId"/> and <see cref="Look"/>; docs/EXPEDITION-CENTRAL-STATION.md,
    /// docs/EXPEDITION-CENTRAL-STATION-VISUALS.md): the gate every
    /// station visual checks, the hazard timings the presentation draws to, the departure board's and the signs' final text,
    /// and the audio cue timeline. Nothing here runs on any other map. Pure; no allocation after the static tables.
    /// </summary>
    public static class NativeExpeditionStation
    {
        public const string MapId="expedition-transit",Look="central-station",Prefix="expedition-transit-";
        /// <summary>Whether a map id is the station: every station visual is inert on any other map (today no live map is).</summary>
        public static bool Active(string mapId)=>mapId==MapId;

        // ---------------- hazards (doc section 6; the authority's values are Astra's, these are the presentation's) ----------------
        /// <summary>The express: the warning before the pass, the pass end to end, the dust settling after it.</summary>
        public const double WarnSeconds=4,PassSeconds=2.2,WakeSeconds=2.4;
        /// <summary>The express's cars (m), the gap between them and the train's length; the shell's inner faces (the tunnel's
        /// dark beyond the mouths) the nose runs between during the pass.</summary>
        public const float CarLength=21.5f,CarGap=.8f,TrainLength=3*CarLength+2*CarGap,ShellX=48f,Mouth=44f;
        /// <summary>The nose at the pass start (the west shell face) and its speed: nose to tail through the station in PassSeconds.</summary>
        public const float NoseStart=-ShellX;
        public static readonly double Speed=(2*ShellX+TrainLength)/PassSeconds;
        /// <summary>A tunnel mouth's face x on a side (-1 west, +1 east).</summary>
        public static float MouthX(int side)=>side<0?-Mouth:Mouth;
        /// <summary>The lines' pit centres (z), T1..T3, and the track bed's top.</summary>
        public static readonly float[] LineZ={-12.5f,-29.5f,-44.5f};
        public const float TrackBed=-1.2f,WaterY=-.85f;
        /// <summary>The levers' throw (s), the board's blink rate, how long a last call shows, the glass telegraph and settle.</summary>
        public const double LeverThrowSeconds=.6,BlinkHz=1.6,LastCallSeconds=6,GlassTelegraph=1.2,GlassSettle=2.2;
        public const int MaxGlass=8;
        /// <summary>The third-rail sections (the draft's `rail-t1-w`, `rail-t1-e`, `rail-t2`, `rail-t3-w`, `rail-t3-e`), as bits.</summary>
        public const int RailT1W=0,RailT1E=1,RailT2=2,RailT3W=3,RailT3E=4,RailSections=5,AllRails=(1<<RailSections)-1;
        public static readonly string[] RailIds={"rail-t1-w","rail-t1-e","rail-t2","rail-t3-w","rail-t3-e"};

        // ---------------- text (doc section 3 "The station's text", the draft's `text` record) ----------------
        public const string BoardTitle="DEPARTURES",BoardCancelled="ALL SERVICES CANCELLED";
        public static readonly string[] RowTime={"6.47","7.02","7.15","7.30","--.--"},RowTo={"EVAC SPECIAL","HARBOUR","NORTH CAMP","ALL STATIONS","EXPRESS"},RowPlatform={"2","3","4","1","1"},RowStatus={"DELAYED","CANCELLED","CANCELLED","CANCELLED","DOES NOT STOP"};
        /// <summary>The rows that change with the game: row 0's variants (by NativeStationState.Row0*) and row 4's (Row4*).</summary>
        public static readonly string[] Row0Status={"DELAYED","LAST CALL","CANCELLED"},Row4Status={"DOES NOT STOP","APPROACHING","SUSPENDED","CANCELLED"};
        public const string Ticker="SERVICE SUSPENDED - FOLLOW STAFF INSTRUCTIONS - DO NOT CROSS THE LINE";
        /// <summary>The ticker shown a page at a time (the message split at its dashes), each for <see cref="TickerPage"/> s.</summary>
        public static readonly string[] TickerPages={"SERVICE SUSPENDED","FOLLOW STAFF INSTRUCTIONS","DO NOT CROSS THE LINE"};
        public const double TickerPage=2.6;
        public static readonly string[] Side={"6:47","PLATFORM 2","EVAC SPECIAL","DOORS OPEN"};
        /// <summary>The board's row as one string (time, destination, platform and status; the doc's 32-character limit).</summary>
        public static string Row(int i,string status=null)=>RowTime[i]+" "+RowTo[i]+" "+RowPlatform[i]+" "+(status??RowStatus[i]);
        /// <summary>The ticker page shown at `t`.</summary>
        public static int TickerAt(double t){int n=TickerPages.Length;int k=(int)Math.Floor(t/TickerPage)%n;return k<0?k+n:k;}

        // ---------------- audio cues (doc section 10; the keys the owner's bank will hold) ----------------
        public const byte CueWarn=0,CueChime=1,CueStandBack=2,CueApproach=3,CuePass=4,CueRecede=5,CueLever=6,CueCut=7,CuePower=8,CueRestored=9,CueDoors=10,CueLastCall=11,
            CueGlassCreak=12,CueGlassFall=13,CueSparkFizz=14,CueSparkArc=15,CueSteam=16,CueBoardFlip=17,CueSignal=18;
        /// <summary>Each cue's bank key (the doc's section 10 names; none is in the bank yet, so the runtime plays a stand-in).</summary>
        public static readonly string[] CueKeys={"station.train.warn","station.pa.chime","station.pa.standback","station.train.approach","station.train.pass","station.train.recede",
            "station.signal.lever","station.rail.cut","station.pa.power","station.pa.restored","station.pa.doors","station.pa.lastcall","station.glass.creak","station.glass.fall",
            "station.spark.fizz","station.spark.arc","station.steam.burst","station.board.flip","station.signal.change"};
        /// <summary>Whether a cue is the PA's (2D, the whole station hears it) rather than positional.</summary>
        public static bool CueFlat(byte cue)=>cue==CueChime||cue==CueStandBack||cue==CuePower||cue==CueRestored||cue==CueDoors||cue==CueLastCall;
        /// <summary>
        /// The cues that start in (from, to] (arena seconds) from the view: the express's warning bell, the PA's chime and
        /// stand-back line and the rails' approach at the west mouth (the warning start), the pass at the nose, the recede at the
        /// east mouth; the levers' throw, the rail's cut and the power line, and the restored line when they end; a Conductor's
        /// doors line on its last call; the boss's last call; each glass's creak (the telegraph) and fall (the landing); the
        /// cabinets' fizz and arc and the steam's burst as their phases start. Writes up to `kinds.Length` cues (kind, time,
        /// place); returns the count. Allocation free. A step longer than 1 s (a load, a pause) plays nothing.
        /// </summary>
        public static int Cues(NativeStationView v,double from,double to,byte[] kinds,double[] times,float[] places)
        {
            int n=0;if(v==null||!(to>from)||to-from>1)return 0;
            void Add(byte k,double t,float x,float y,float z){if(n>=kinds.Length||n*3+2>=places.Length||n>=times.Length)return;kinds[n]=k;times[n]=t;places[n*3]=x;places[n*3+1]=y;places[n*3+2]=z;n++;}
            bool In(double t)=>t>from&&t<=to;
            bool levers=v.Levers&&!(from>=v.LeversUntil);
            int line=v.TrainLine<1||v.TrainLine>3?1:v.TrainLine;float z=LineZ[line-1];
            if(!double.IsNaN(v.TrainAt)&&!levers)
            {
                double w=v.TrainAt-WarnSeconds;
                if(In(w)){Add(CueWarn,w,-Mouth+2,TrackBed+1.2f,z);Add(CueChime,w,0,0,0);Add(CueApproach,w,-Mouth,TrackBed+1,z);}
                if(In(w+1.1))Add(CueStandBack,w+1.1,0,0,0);
                if(In(v.TrainAt))Add(CuePass,v.TrainAt,-30,TrackBed+1.5f,z);
                double tailOut=v.TrainAt+PassSeconds;if(In(tailOut-.4))Add(CueRecede,tailOut-.4,Mouth,TrackBed+1.5f,z);
            }
            if(!double.IsNaN(v.LeversAt)&&In(v.LeversAt)){Add(CueLever,v.LeversAt,36,7,-55.6f);Add(CueCut,v.LeversAt+.3,36,7,-55.6f);Add(CueChime,v.LeversAt+.5,0,0,0);Add(CuePower,v.LeversAt+.5,0,0,0);}
            if(!double.IsNaN(v.LeversUntil)&&In(v.LeversUntil)){Add(CueChime,v.LeversUntil,0,0,0);Add(CueRestored,v.LeversUntil,0,0,0);}
            if(!double.IsNaN(v.LastCallAt)&&In(v.LastCallAt)){Add(CueChime,v.LastCallAt,0,0,0);Add(CueDoors,v.LastCallAt,0,0,0);Add(CueBoardFlip,v.LastCallAt,0,8.6f,11);}
            if(!double.IsNaN(v.BossAt)&&In(v.BossAt)){Add(CueChime,v.BossAt,0,0,0);Add(CueLastCall,v.BossAt,0,0,0);Add(CueBoardFlip,v.BossAt,0,8.6f,11);}
            for(int i=0;i<v.GlassCount&&i<v.Glass.Length;i++){var g=v.Glass[i];if(In(g.At-GlassTelegraph))Add(CueGlassCreak,g.At-GlassTelegraph,g.X,16,g.Z);if(In(g.At))Add(CueGlassFall,g.At,g.X,.2f,g.Z);}
            return n;
        }
        /// <summary>The cues of hazard phases that start between two states (the cabinets' fizz and arc at their places, the
        /// steam's burst): called with the previous frame's and this frame's phases.</summary>
        public static int PhaseCues(NativeHazardPhase before,NativeHazardPhase now,byte warn,byte active,byte[] kinds,int n)
        {
            if(n>=kinds.Length||before==now)return n;if(now==NativeHazardPhase.Warn)kinds[n++]=warn;else if(now==NativeHazardPhase.Active||now==NativeHazardPhase.Danger)kinds[n++]=active;return n;
        }
    }

    /// <summary>
    /// Where the station's view lives between the authority and the picture. Astra's replicated hazard and objective fields
    /// (doc section 8) fill <see cref="View"/> every frame on every peer (the host from its authority, a guest from the frame);
    /// nothing fills it today, so it reads as an idle station. The presentation publishes the view it drew (<see cref="Shown"/>:
    /// the live one, or the DEV preview's) and the clock it drew it at, so the station's sounds follow the picture.
    /// </summary>
    public static class NativeStationLive
    {
        public static readonly NativeStationView View=new NativeStationView();
        public static NativeStationView Shown;public static double ShownClock;
    }

    /// <summary>
    /// DEV PREVIEW ONLY (DEV panel · Camera · "Station hazards preview"): a synthetic station view so the express, the third
    /// rail, the flood, the levers, the board, the cabinets, the steam and the falling glass can be looked at without the
    /// authority. One <see cref="Period"/> s cycle on the arena clock: the express warns and passes every 24 s (at 8, 32, 56,
    /// 80 s into the cycle), the levers are thrown at 60 s for <see cref="LeverHold"/> s (the rails, the flood and the express
    /// go quiet), the flood conducts from 36 to 50 s, a Conductor's last call at 14 s, glass falls from 88 s and the boss
    /// state shows from 88 to 96 s; the cabinets fizz and arc on the Ward grid's 9-14 s rhythm and the steam main bursts every
    /// 15 s. It never reaches the simulation: no collider, no damage, no input, no network.
    /// </summary>
    public static class NativeStationPreview
    {
        public static bool On;
        public const double Period=96,LeverAt=60,LeverHold=20;
        public static void Fill(NativeStationView v,double t)
        {
            if(v==null)return;v.Preview=true;double c=t-Math.Floor(t/Period)*Period,start=t-c;
            // The express: the next pass start in this cycle (warn 4 s before), the levers' window skipped.
            double[] passes=Passes;double next=double.NaN;
            foreach(double p in passes){double at=start+p;if(t<=at+NativeExpeditionStation.PassSeconds+NativeExpeditionStation.WakeSeconds){next=at;break;}}
            if(double.IsNaN(next))next=start+Period+passes[0];v.TrainAt=next;v.TrainLine=1;
            v.Levers=c>=LeverAt&&c<LeverAt+LeverHold;v.LeversAt=start+LeverAt;v.LeversUntil=start+LeverAt+LeverHold;
            v.RailLive=NativeExpeditionStation.AllRails;v.RailChangedAt=c>=LeverAt+LeverHold?start+LeverAt+LeverHold:start;
            v.FloodLive=c>=36&&c<50;v.LastCallAt=start+14;v.Boss=c>=88;v.BossAt=start+88;
            v.GlassCount=0;if(c>=86){for(int i=0;i<4&&v.GlassCount<v.Glass.Length;i++){var g=Shards[i];v.Glass[v.GlassCount++]=new NativeStationGlass{X=g[0],Z=g[1],At=start+88+i*1.7};}}
            for(int i=0;i<2;i++)v.Cabinets[i]=Cycle(t+i*4.3,11.5,1,1);
            v.Steam=Cycle(t+2,15,2,3);
        }
        private static readonly double[] Passes={8,32,80};
        private static readonly float[][] Shards={new[]{-6f,4f},new[]{9f,7.5f},new[]{-14f,13f},new[]{4f,-1f}};
        // A warn-then-active cycle (period, warn and active seconds): the Ward grid's and gas's shape.
        private static NativeHazardState Cycle(double t,double period,double warn,double active)
        {
            double c=t-Math.Floor(t/period)*period;var s=new NativeHazardState{Cycle=(int)Math.Floor(t/period)};
            if(c<warn){s.Phase=NativeHazardPhase.Warn;s.Since=c;s.Until=warn-c;s.Level=(float)(c/warn);}
            else if(c<warn+active){s.Phase=NativeHazardPhase.Active;s.Since=c-warn;s.Until=warn+active-c;s.Level=1;}
            else{s.Phase=NativeHazardPhase.Idle;s.Since=c-warn-active;s.Until=period-c;}
            return s;
        }
    }
}
