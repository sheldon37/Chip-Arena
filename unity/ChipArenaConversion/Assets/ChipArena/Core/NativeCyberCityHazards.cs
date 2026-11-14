using System;

namespace ChipArena.UnityConversion
{
    /// <summary>A hazard's phase. Maglev: Idle, Warn (amber), Danger (red), Active (the pass), Clear (2 s of fading after it).
    /// Grid: Idle, Warn (charging), Active (live), Clear (discharge).</summary>
    public enum NativeHazardPhase { Idle, Warn, Danger, Active, Clear }
    /// <summary>
    /// A hazard's state at one moment (<see cref="NativeCyberCityHazards.Evaluate"/>): the phase, its cycle (a maglev's pass, a
    /// grid's cycle; -1 before the first), how long the phase has run and has left, a 0..1 presentation level (the warning's
    /// build, a charge's ramp, a fade), and for a maglev its direction (+1 west to east), the seconds until the head reaches its
    /// near portal (`Lead`, negative during the pass; `Until` in Idle is the time to the next Warn, before the first one too) and the head's and tail's x along the track at that moment (they keep
    /// moving through the tunnels beyond the portals, so the approach can be drawn).
    /// </summary>
    public struct NativeHazardState
    {
        /// <summary>Safe to commit to a crossing that takes `margin` seconds: Idle with at least that long before the next
        /// warning (the bot and PvE nav gate, contract §6).</summary>
        public bool Clear(double margin)=>Phase==NativeHazardPhase.Idle&&Until>=margin;
        public NativeHazardPhase Phase;public int Cycle,Direction;public double Since,Until,Lead,Head,Tail;public float Level;
        public bool Telegraph=>Phase==NativeHazardPhase.Warn||Phase==NativeHazardPhase.Danger;
    }

    /// <summary>
    /// Cyber City's timed hazards as pure functions of (round, FightAt, match time) and the hazard's definition
    /// (CYBERCITY-SIMULATION-CONTRACT.md §4): the reference timing the contract pins, which the presentation evaluates every
    /// frame on the arena's presentation clock (host, guests and the killcam replay agree, and nothing is sent). The simulation's
    /// kill and damage volumes are Astra's; <see cref="TrainBox"/> is the train's body at a moment, which the contract's kill
    /// envelope equals. <see cref="Draft"/> is the draft blockout's hazards (docs/arenas/scripts/cybercity_m1.py) for offline
    /// previews and checks. Allocation free (struct results).
    /// </summary>
    public static class NativeCyberCityHazards
    {
        /// <summary>How long a maglev's lights fade after the tail leaves; a grid's discharge.</summary>
        public const double MaglevClear=2,GridDischarge=1;
        public static bool IsMaglev(MapHazard h)=>h!=null&&h.kind=="maglev";
        public static bool IsGrid(MapHazard h)=>h!=null&&h.kind=="grid";
        /// <summary>The state of any known kind; an unknown kind or a missing definition is always Idle.</summary>
        public static NativeHazardState Evaluate(MapHazard h,int round,double fightAt,double time)
        =>IsMaglev(h)?Maglev(h,round,fightAt,time):IsGrid(h)?Grid(h,round,fightAt,time):Idle();
        /// <summary>The state during a match phase: the clock runs in Fight and on through Result (a pass in progress finishes
        /// behind the round's end); every other phase (intro, countdown, draft, lobby) is Idle, since FightAt still holds the
        /// last round's start there. The simulation applies damage in Fight only (contract §4).</summary>
        public static NativeHazardState At(MapHazard h,NativePhase phase,int round,double fightAt,double time)
        =>phase==NativePhase.Fight||phase==NativePhase.Result?Evaluate(h,round,fightAt,time):Idle();
        public static NativeHazardState Idle()=>new NativeHazardState{Phase=NativeHazardPhase.Idle,Cycle=-1,Head=double.NaN,Tail=double.NaN,Lead=double.PositiveInfinity,Until=double.PositiveInfinity};
        /// <summary>
        /// The Helix Line. Pass k's head reaches its near portal at FightAt + first + k * period, travelling west to east when
        /// k + round is even (east to west when odd), at `speed`; its tail clears the far portal (span + length) / speed later.
        /// Warn from `warn` seconds before that arrival, Danger from `danger` seconds before, Active through the pass, Clear for
        /// 2 s after; Idle before FightAt + first - warn and between passes.
        /// </summary>
        public static NativeHazardState Maglev(MapHazard h,int round,double fightAt,double time)
        {
            var s=Idle();if(h==null||!(h.period>0)||!(h.speed>0))return s;
            double u=time-fightAt-h.first+h.warn;if(double.IsNaN(u)||u<0){if(!double.IsNaN(u)){s.Lead=-u+h.warn;s.Until=-u;}return s;}
            int k=(int)Math.Floor(u/h.period);double since=u-k*h.period-h.warn,span=h.size.x,duration=(span+h.length)/h.speed;
            int d=((k+round)%2+2)%2==0?1:-1;double near=h.center.x-d*span/2;
            s.Cycle=k;s.Direction=d;s.Lead=-since;s.Head=near+d*h.speed*since;s.Tail=s.Head-d*h.length;
            if(since< -h.danger){s.Phase=NativeHazardPhase.Warn;s.Since=since+h.warn;s.Until=-h.danger-since;s.Level=(float)Clamp01((since+h.warn)/Math.Max(.001,h.warn-h.danger));}
            else if(since<0){s.Phase=NativeHazardPhase.Danger;s.Since=since+h.danger;s.Until=-since;s.Level=1;}
            else if(since<duration){s.Phase=NativeHazardPhase.Active;s.Since=since;s.Until=duration-since;s.Level=1;}
            else if(since<duration+MaglevClear){s.Phase=NativeHazardPhase.Clear;s.Since=since-duration;s.Until=duration+MaglevClear-since;s.Level=(float)(1-(since-duration)/MaglevClear);}
            else{s.Phase=NativeHazardPhase.Idle;s.Since=since-duration-MaglevClear;s.Until=h.period-h.warn-since;s.Level=0;}
            return s;
        }
        /// <summary>
        /// A power coupling. Cycle k starts at FightAt + first + k * period (+ offset on even rounds): Idle for period - charge -
        /// live - 1 s, then Warn while it charges (level ramps 0..1), Active while live, Clear for the 1 s discharge (level falls).
        /// </summary>
        public static NativeHazardState Grid(MapHazard h,int round,double fightAt,double time)
        {
            var s=Idle();if(h==null||!(h.period>0))return s;
            double u=time-fightAt-h.first-(round%2==0?h.offset:0),idle=Math.Max(0,h.period-h.charge-h.live-GridDischarge);if(double.IsNaN(u))return s;if(u<0){s.Until=idle-u;return s;}
            int k=(int)Math.Floor(u/h.period);double c=u-k*h.period;s.Cycle=k;
            if(c<idle){s.Phase=NativeHazardPhase.Idle;s.Since=c;s.Until=idle-c;s.Level=0;}
            else if(c<idle+h.charge){s.Phase=NativeHazardPhase.Warn;s.Since=c-idle;s.Until=idle+h.charge-c;s.Level=(float)Clamp01((c-idle)/Math.Max(.001,h.charge));}
            else if(c<idle+h.charge+h.live){s.Phase=NativeHazardPhase.Active;s.Since=c-idle-h.charge;s.Until=idle+h.charge+h.live-c;s.Level=1;}
            else{s.Phase=NativeHazardPhase.Clear;s.Since=c-idle-h.charge-h.live;s.Until=h.period-c;s.Level=(float)Clamp01(1-s.Since/GridDischarge);}
            return s;
        }
        /// <summary>The train's body at this moment, clipped to the hazard's envelope (the part inside the arena), as centre and
        /// full size; false when no part of it is between the portals.</summary>
        public static bool TrainBox(MapHazard h,in NativeHazardState s,out V3 center,out V3 size)
        {
            center=default;size=default;if(h==null||double.IsNaN(s.Head))return false;
            double lo=Math.Max(h.center.x-h.size.x/2,Math.Min(s.Head,s.Tail)),hi=Math.Min(h.center.x+h.size.x/2,Math.Max(s.Head,s.Tail));if(!(hi>lo))return false;
            center=new V3((float)((lo+hi)/2),h.center.y,h.center.z);size=new V3((float)(hi-lo),h.size.y,h.size.z);return true;
        }
        private static double Clamp01(double v)=>v<0?0:v>1?1:v;

        /// <summary>The draft blockout's hazards (docs/arenas/scripts/cybercity_m1.py, `hazards`), for offline previews and checks
        /// until Astra's map content carries its own.</summary>
        public static MapHazard[] Draft()=>new[]
        {
            new MapHazard{id="cybercity-maglev",kind="maglev",center=new V3(0,-2.45f,0),size=new V3(80,3.1f,5.4f),axis=new V3(1,0,0),first=18,period=32,warn=6,danger=3,speed=44,length=42,lethal=true},
            new MapHazard{id="cybercity-grid-w",kind="grid",center=new V3(-24,.5f,0),size=new V3(4,1,6),axis=new V3(0,0,1),first=4,period=14,offset=7,charge=2.5f,live=2,tick=.25f,damage=13},
            new MapHazard{id="cybercity-grid-e",kind="grid",center=new V3(24,.5f,0),size=new V3(4,1,6),axis=new V3(0,0,1),first=11,period=14,offset=-7,charge=2.5f,live=2,tick=.25f,damage=13},
        };
    }
}
