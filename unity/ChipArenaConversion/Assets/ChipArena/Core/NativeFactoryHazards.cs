using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// One Factory stage hazard as UnityMaps.json `hazards[]` authors it (docs/arenas/FACTORY-SIMULATION-CONTRACT.md 2; the draft
    /// is docs/arenas/scripts/factory_m1.py). JsonUtility shape: vectors {x,y,z}, no nested arrays, every field written. `kind`:
    /// <list type="bullet">
    /// <item>"saw-slot": a floor-slot buzzsaw. `a`→`b` the blade centre's path at floor level, `axis` its axle (unit, across the
    /// path), `radius`, `size` the blade box (diameter, diameter, thickness). Each cycle: `warn` (spinning up under the floor),
    /// `rise` (emerging), `travel` (running a→b, or b→a on alternate cycles with `alternate`), `rise` again (sinking), parked.</item>
    /// <item>"saw-rail": a hanging rail saw that sweeps across a lane. `a` the blade centre parked in its wall housing, `b` fully
    /// out; `warn`, `travel` out, `dwell` held out, `travel` back, parked.</item>
    /// <item>"crane": the overhead crane. `a`/`b` the bridge's travel ends on the runway top (y), `size` the load, `carry` the
    /// load's underside when hoisted, `zones` the drop zones in visiting order (centre on the floor; `size.y` the lethal
    /// height). Each cycle: `travel` to the zone, `warn` over it, `rise` the fall, `dwell` landed (cover), the hook's
    /// <see cref="NativeFactoryHazards.RegripSeconds"/> s, `hoist`, idle.</item>
    /// <item>"press": a die press in a wall tunnel. `a` the ram centre open, `b` slammed, `size` the ram; open, `warn`, `rise`
    /// the slam, `dwell` held, `travel` the lift.</item>
    /// <item>"vent": a steam vent. `a` the grate centre, `size` the steam column; quiet, `warn` (hiss), `dwell` the burst;
    /// `damage` per `tick` inside the column while it bursts (0: presentation only).</item>
    /// </list>
    /// Shared: `start` (s after the arm time, FightAt + 4 s) and `offset` (s) place the first cycle, `period` repeats it;
    /// `lethal` (instant kill inside the live volume) or `damage`; `push` (m/s, non-lethal hits).
    /// </summary>
    [Serializable] public class FactoryHazard
    {
        public string id,kind;public V3 a,b,axis,size;
        public float radius,start,period,offset,warn,rise,travel,dwell,hoist,carry,damage,tick,push;public bool lethal,alternate;public MapVolume[] zones;
    }
    /// <summary>The presentation's read of UnityMaps.json until ContentMap carries `hazards` (JsonUtility skips the other fields).</summary>
    [Serializable] public sealed class FactoryHazardContent {public FactoryHazardMap[] maps;}
    [Serializable] public sealed class FactoryHazardMap {public string id;public FactoryHazard[] hazards;}

    /// <summary>A hazard's pose and phase at one clock (<see cref="NativeFactoryHazards.State"/>); one struct for every kind.</summary>
    public struct FactoryHazardState
    {
        /// <summary>Phase (<see cref="NativeFactoryHazards"/> constants), cycle index (-1 before the first), and for the crane the
        /// zone index it serves and the one it came from (-1: the park).</summary>
        public int Phase,Cycle,Zone,From;
        /// <summary>Lethal: the kill volume is live (or, for a vent with damage, the column damages). Reverse: running b→a.</summary>
        public bool Lethal,Reverse,Landed,Attached;
        /// <summary>
        /// U: 0..1 along a→b (saws), the press ram's stroke (Drop, -.06 wind-up .. 1 slammed), the vent's burst level (0..1).
        /// Lift: a slot blade's exposure (0 under the floor .. 1 half the disc up). Spin: the blade's angle (rad, a pure function
        /// of the clock); Rate its rad/s. Warn: the telegraph's progress 0..1 (1 through the motion it warns of).
        /// Crane: X, Z the hook over the floor; HookY the hook block's underside; LoadY the load's underside; Sway the load's
        /// swing (rad, about x); ImpactAt the clock of this cycle's impact (NaN before a fall).
        /// </summary>
        public double U,Lift,Spin,Rate,Warn,X,Z,HookY,LoadY,Sway,ImpactAt,Age;
        /// <summary>The live kill or damage volume (centre and full size; Unity frame) this state describes, also drawn as the telegraph.</summary>
        public V3 Center,BoxCenter,BoxSize;
    }
    /// <summary>A discrete hazard moment for the audio and the effects (<see cref="NativeFactoryHazards.Cues"/>).</summary>
    public struct FactoryCue {public int Kind,Hazard,Cycle;public double Time;public V3 At;}

    /// <summary>
    /// The Factory's hazard clock and every hazard's state as pure functions of (definition, round index, hazard clock): host,
    /// guests and the killcam pose the same saws, presses and crane from the replicated phase, FightAt and match time, so
    /// nothing new is sent (FACTORY-SIMULATION-CONTRACT.md 3). The hazard clock is seconds since the arm time (FightAt +
    /// <see cref="ArmDelay"/>) during the fight and its result; before it every hazard rests parked and harmless.
    /// The presentation draws from these; the simulation decides hits from the same states (Lethal, BoxCenter, BoxSize).
    /// Allocation free.
    /// </summary>
    public static class NativeFactoryHazards
    {
        public const string MapId="factory";
        public static bool Active(string mapId)=>mapId==MapId;
        public const string SawSlot="saw-slot",SawRail="saw-rail",Crane="crane",Press="press",Vent="vent";
        /// <summary>Phases. Saws: Parked, Warning, Rising, Running, Sinking (slot) / Holding, Returning (rail). Press: Parked (open),
        /// Warning, Slamming, Holding, Lifting. Crane: Parked, Travelling, Warning, Falling, Landed, Regrip, Hoisting. Vent:
        /// Parked, Warning, Bursting.</summary>
        public const int Parked=0,Warning=1,Rising=2,Running=3,Sinking=4,Holding=5,Returning=6,Slamming=7,Lifting=8,Travelling=9,Falling=10,Landed=11,Regrip=12,Hoisting=13,Bursting=14;
        /// <summary>Hazards arm with the structural members (NativeStructureRules.ArmDelay): nothing moves for the fight's first 4 s.</summary>
        public const double ArmDelay=NativeStructureRules.ArmDelay;
        /// <summary>A slot blade is lethal once this much of it (m) stands above the floor; a press ram once its underside is below
        /// LethalRam (m, a pilot is 1.7 m); the crane's landed load stays lethal for ImpactGrace s after it lands.</summary>
        public const double Exposure=.15,LethalRam=1.8,ImpactGrace=.06;
        /// <summary>Blade spin (rad/s at speed), its spin-down after a run (s), the crane hook's regrip (s), the hook block's gap
        /// over a carried load (m) and the load's sway amplitude (rad).</summary>
        public const double SpinRate=46,SpinDown=1.4,RegripSeconds=1.5,HookGap=1.1,SwayMax=.035;

        /// <summary>The hazard clock: seconds since FightAt + ArmDelay in the fight and its result; -infinity otherwise (parked).</summary>
        public static double Clock(NativePhase phase,double fightAt,double now)=>phase==NativePhase.Fight||phase==NativePhase.Result?now-fightAt-ArmDelay:double.NegativeInfinity;
        /// <summary>Cycle index and time into it for a hazard at `clock`; false before its first cycle.</summary>
        public static bool Cycle(FactoryHazard h,double clock,out int n,out double u)
        {
            double c=clock-h.start-h.offset;if(!(c>=0)||!(h.period>0)||double.IsInfinity(c)){n=-1;u=0;return false;}
            double k=Math.Floor(c/h.period);n=k>int.MaxValue?int.MaxValue:(int)k;u=c-k*h.period;return true;
        }
        /// <summary>Whether cycle `n` runs b→a: alternate cycles reverse, and odd rounds start reversed (each side gets the first run in turn).</summary>
        public static bool Reversed(FactoryHazard h,int round,int n)=>h.alternate&&(((n<0?0:n)+(round&1))&1)==1;
        /// <summary>The zone the crane serves in cycle `n` (authored order; odd rounds start one zone later, on the other side).</summary>
        public static int ZoneOf(FactoryHazard h,int round,int n){int z=h.zones?.Length??0;if(z==0)return -1;long k=((long)(n<0?0:n)+(round&1))%z;return (int)k;}
        public static double Smooth(double t){t=t<0?0:t>1?1:t;return t*t*(3-2*t);}
        private static V3 Lerp(V3 a,V3 b,double t)=>new V3((float)(a.x+(b.x-a.x)*t),(float)(a.y+(b.y-a.y)*t),(float)(a.z+(b.z-a.z)*t));

        /// <summary>The hazard's state at hazard clock `clock` in round `round` (NativeStructureFx.RoundIndex).</summary>
        public static FactoryHazardState State(FactoryHazard h,int round,double clock)
        {
            switch(h?.kind)
            {
                case SawSlot:return Slot(h,round,clock);case SawRail:return Rail(h,round,clock);case Crane:return CraneState(h,round,clock);
                case Press:return PressState(h,round,clock);case Vent:return VentState(h,round,clock);default:return new FactoryHazardState{Cycle=-1,Zone=-1,From=-1,ImpactAt=double.NaN};
            }
        }
        // A spinning blade's angle: spin-up over the warning (quadratic), full speed through the motion, spin-down after it.
        private static double SpinAngle(double u,double up,double run,out double rate)
        {
            if(u<=0){rate=0;return 0;}
            if(u<up){rate=SpinRate*u/up;return SpinRate*u*u/(2*up);}
            double a=SpinRate*up/2;if(u<up+run){rate=SpinRate;return a+SpinRate*(u-up);}
            a+=SpinRate*run;double d=Math.Min(u-up-run,SpinDown);rate=SpinRate*(1-d/SpinDown);return a+SpinRate*(d-d*d/(2*SpinDown));
        }
        private static FactoryHazardState Slot(FactoryHazard h,int round,double clock)
        {
            var s=new FactoryHazardState{Zone=-1,From=-1,ImpactAt=double.NaN};bool on=Cycle(h,clock,out int n,out double u);s.Cycle=n;
            double w=h.warn,r=h.rise,T=h.travel;bool rev=Reversed(h,round,on?n:0);s.Reverse=rev;double from=rev?1:0,to=rev?0:1;
            if(!on){s.Phase=Parked;s.U=from;}
            else if(u<w){s.Phase=Warning;s.U=from;s.Warn=u/w;}
            else if(u<w+r){s.Phase=Rising;s.U=from;s.Lift=Smooth((u-w)/r);s.Warn=1;}
            else if(u<w+r+T){s.Phase=Running;s.U=from+(to-from)*((u-w-r)/T);s.Lift=1;s.Warn=1;}
            else if(u<w+2*r+T){s.Phase=Sinking;s.U=to;s.Lift=1-Smooth((u-w-r-T)/r);s.Warn=1;}
            else{s.Phase=Parked;s.U=to;}
            s.Age=on?u:0;s.Spin=(rev?-1:1)*SpinAngle(on?u:0,w,2*r+T,out s.Rate);
            s.Center=Lerp(h.a,h.b,s.U);s.Center.y=(float)(h.a.y-h.radius*(1-s.Lift));
            s.Lethal=h.lethal&&s.Lift*h.radius>=Exposure;s.BoxCenter=s.Center;s.BoxSize=h.size;return s;
        }
        private static FactoryHazardState Rail(FactoryHazard h,int round,double clock)
        {
            var s=new FactoryHazardState{Zone=-1,From=-1,ImpactAt=double.NaN,Lift=1};bool on=Cycle(h,clock,out int n,out double u);s.Cycle=n;double w=h.warn,T=h.travel,d=h.dwell;
            if(!on){s.Phase=Parked;}
            else if(u<w){s.Phase=Warning;s.Warn=u/w;}
            else if(u<w+T){s.Phase=Running;s.U=Smooth((u-w)/T);s.Warn=1;}
            else if(u<w+T+d){s.Phase=Holding;s.U=1;s.Warn=1;}
            else if(u<w+2*T+d){s.Phase=Returning;s.U=1-Smooth((u-w-T-d)/T);s.Warn=1;}
            else s.Phase=Parked;
            s.Age=on?u:0;s.Spin=SpinAngle(on?u:0,w,2*T+d,out s.Rate);s.Center=Lerp(h.a,h.b,s.U);
            s.Lethal=h.lethal&&(s.Phase==Running||s.Phase==Holding||s.Phase==Returning);s.BoxCenter=s.Center;s.BoxSize=h.size;return s;
        }
        private static FactoryHazardState PressState(FactoryHazard h,int round,double clock)
        {
            var s=new FactoryHazardState{Zone=-1,From=-1,ImpactAt=double.NaN};bool on=Cycle(h,clock,out int n,out double u);s.Cycle=n;
            double w=h.warn,sl=h.rise,d=h.dwell,l=h.travel,open=Math.Max(0,h.period-w-sl-d-l);
            if(!on||u<open){s.Phase=Parked;}
            else if(u<open+w){s.Phase=Warning;s.Warn=(u-open)/w;s.U=-.06*Smooth((u-open)/w);}
            else if(u<open+w+sl){s.Phase=Slamming;double q=(u-open-w)/sl;s.U=-.06+1.06*q*q;s.Warn=1;}
            else if(u<open+w+sl+d){s.Phase=Holding;s.U=1;s.Warn=1;}
            else if(u<open+w+sl+d+l){s.Phase=Lifting;s.U=1-Smooth((u-open-w-sl-d)/l);s.Warn=1;}
            else s.Phase=Parked;
            s.Age=on?u-open:0;s.Center=Lerp(h.a,h.b,s.U);double bottom=s.Center.y-h.size.y/2;s.LoadY=bottom;
            s.Lethal=h.lethal&&bottom<LethalRam&&(s.Phase==Slamming||s.Phase==Holding||s.Phase==Lifting);
            // The live volume: the ram's footprint from the die bed (its slammed underside) up to the ram's top.
            double bed=h.b.y-h.size.y/2,top=s.Center.y+h.size.y/2;s.BoxSize=new V3(h.size.x,(float)(top-bed),h.size.z);s.BoxCenter=new V3(s.Center.x,(float)((bed+top)/2),s.Center.z);return s;
        }
        private static FactoryHazardState VentState(FactoryHazard h,int round,double clock)
        {
            var s=new FactoryHazardState{Zone=-1,From=-1,ImpactAt=double.NaN};bool on=Cycle(h,clock,out int n,out double u);s.Cycle=n;double w=h.warn,b=h.dwell,quiet=Math.Max(0,h.period-w-b);
            if(!on||u<quiet)s.Phase=Parked;
            else if(u<quiet+w){s.Phase=Warning;s.Warn=(u-quiet)/w;}
            else if(u<quiet+w+b){s.Phase=Bursting;double t=u-quiet-w;s.U=Math.Min(1,Math.Min(t/.15,(b-t)/.4));s.Warn=1;}
            else s.Phase=Parked;
            s.Age=on?u-quiet:0;s.Center=h.a;s.Lethal=s.Phase==Bursting&&h.damage>0;s.BoxSize=h.size;s.BoxCenter=new V3(h.a.x,h.a.y+h.size.y/2,h.a.z);return s;
        }
        /// <summary>The crane's park: midway along its runway, over the bay's centre line, load hoisted.</summary>
        public static void Park(FactoryHazard h,out double x,out double z){x=(h.a.x+h.b.x)*.5;z=(h.a.z+h.b.z)*.5;}
        private static void ZoneXZ(FactoryHazard h,int k,out double x,out double z){if(k<0||h.zones==null||k>=h.zones.Length){Park(h,out x,out z);return;}x=h.zones[k].center.x;z=h.zones[k].center.z;}
        /// <summary>The crane's cycle times: travel, warn, fall, landed, regrip, hoist (their sum never exceeds the period).</summary>
        public static void CraneTimes(FactoryHazard h,out double T,out double W,out double F,out double D,out double G,out double H){T=h.travel;W=h.warn;F=h.rise;D=h.dwell;G=RegripSeconds;H=h.hoist;}
        private static FactoryHazardState CraneState(FactoryHazard h,int round,double clock)
        {
            var s=new FactoryHazardState{Zone=-1,From=-1,ImpactAt=double.NaN,Attached=true};bool on=Cycle(h,clock,out int n,out double u);s.Cycle=n;
            CraneTimes(h,out double T,out double W,out double F,out double D,out double G,out double H);double carry=h.carry;
            int zone=on?ZoneOf(h,round,n):-1,from=on&&n>0?ZoneOf(h,round,n-1):-1;s.Zone=zone;s.From=from;ZoneXZ(h,zone,out double zx,out double zz);ZoneXZ(h,from,out double fx,out double fz);
            double floor=zone>=0?h.zones[zone].center.y:0;
            s.LoadY=carry;s.X=zx;s.Z=zz;
            if(!on){s.Phase=Parked;Park(h,out s.X,out s.Z);}
            else if(u<T){s.Phase=Travelling;double q=Smooth(u/T);s.X=fx+(zx-fx)*q;s.Z=fz+(zz-fz)*q;s.Sway=SwayMax*Math.Sin(Math.PI*u/T)*(fx==zx&&fz==zz?0:1);}
            else if(u<T+W){s.Phase=Warning;s.Warn=(u-T)/W;s.Sway=SwayMax*.6*Math.Sin(u*4.1)*Math.Exp(-(u-T)*1.2);}
            else if(u<T+W+F){s.Phase=Falling;s.Warn=1;s.Attached=false;double q=(u-T-W)/F;s.LoadY=floor+(carry-floor)*(1-q*q);}
            else if(u<T+W+F+D){s.Phase=Landed;s.Landed=true;s.Attached=false;s.LoadY=floor;}
            else if(u<T+W+F+D+G){s.Phase=Regrip;s.Landed=true;s.Attached=false;s.LoadY=floor;}
            else if(u<T+W+F+D+G+H){s.Phase=Hoisting;s.LoadY=floor+(carry-floor)*Smooth((u-T-W-F-D-G)/H);}
            else s.Phase=Parked;
            if(on){s.ImpactAt=clock-u+T+W+F;s.Age=u;}
            // The hook rides the load's top plus the gap; released, it stays up, then lowers to the landed load and regrips it.
            double top=h.size.y,up=carry+top+HookGap;
            if(s.Phase==Falling||s.Phase==Landed)s.HookY=up;
            else if(s.Phase==Regrip)s.HookY=up+(floor+top+HookGap-up)*Smooth((u-T-W-F-D)/G/.75);
            else s.HookY=s.LoadY+top+HookGap;
            s.Lethal=h.lethal&&zone>=0&&(s.Phase==Falling&&s.LoadY<=floor+h.zones[zone].size.y||s.Phase==Landed&&u-(T+W+F)<ImpactGrace);
            var zv=zone>=0?h.zones[zone]:null;s.Center=new V3((float)s.X,(float)s.LoadY,(float)s.Z);
            s.BoxSize=zv!=null?zv.size:new V3();s.BoxCenter=zv!=null?new V3(zv.center.x,zv.center.y+zv.size.y/2,zv.center.z):s.Center;return s;
        }

        /// <summary>Cue kinds (audio and one-shot effects): a saw spinning up, emerging, sinking, a rail saw's warning, swing out and back,
        /// a press's hiss, slam and release, the crane setting off, its horn (twice), the release, the impact, the hoist, a vent's
        /// hiss and burst, and a running slot saw's grind (every <see cref="GrindEvery"/> s along its run).</summary>
        public const int CueSpinUp=0,CueEmerge=1,CueSink=2,CueRailWarn=3,CueRailOut=4,CueRailBack=5,CuePressHiss=6,CuePressSlam=7,CuePressLift=8,CueCraneMove=9,CueHorn=10,CueRelease=11,CueImpact=12,CueHoist=13,CueVentHiss=14,CueVentBurst=15,CueGrind=16;
        public const double GrindEvery=.55;
        /// <summary>
        /// Every cue with hazard-clock time in (from, to] for these hazards in round `round`, into `into` (at most its length; the
        /// count is returned). Pure and stateless: a caller that keeps its last clock hears each cue once; a clock that runs back
        /// (a new round, a killcam rewind) simply restarts the window. Positions are the hazard's at the cue.
        /// </summary>
        public static int Cues(FactoryHazard[] hazards,int round,double from,double to,FactoryCue[] into)
        {
            int count=0;if(hazards==null||into==null||!(to>from)||double.IsInfinity(to)||double.IsNaN(from))return 0;if(double.IsNegativeInfinity(from))from=-1e9;
            for(int i=0;i<hazards.Length;i++)
            {
                var h=hazards[i];if(h==null||!(h.period>0))continue;double origin=h.start+h.offset;
                double first=Math.Floor((Math.Max(from,origin-1)-origin)/h.period)-1,last=Math.Floor((to-origin)/h.period);
                for(double k=Math.Max(0,first);k<=last;k++)
                {
                    double c0=origin+k*h.period;int n=(int)k;
                    void Add(int kind,double at){if(at>from&&at<=to&&count<into.Length){var st=State(h,round,at+1e-6);into[count++]=new FactoryCue{Kind=kind,Hazard=i,Cycle=n,Time=at,At=kind==CueImpact||kind==CueRelease?new V3((float)st.X,st.Zone>=0?h.zones[st.Zone].center.y:0,(float)st.Z):st.Center};}}
                    switch(h.kind)
                    {
                        case SawSlot:Add(CueSpinUp,c0);Add(CueEmerge,c0+h.warn);for(double g=c0+h.warn+h.rise;g<c0+h.warn+h.rise+h.travel;g+=GrindEvery)Add(CueGrind,g);Add(CueSink,c0+h.warn+h.rise+h.travel);break;
                        case SawRail:Add(CueRailWarn,c0);Add(CueRailOut,c0+h.warn);Add(CueRailBack,c0+h.warn+h.travel+h.dwell);break;
                        case Press:{double open=Math.Max(0,h.period-h.warn-h.rise-h.dwell-h.travel);Add(CuePressHiss,c0+open);Add(CuePressSlam,c0+open+h.warn+h.rise);Add(CuePressLift,c0+open+h.warn+h.rise+h.dwell);break;}
                        case Crane:{CraneTimes(h,out double T,out double W,out double F,out double D,out double G,out double H);Add(CueCraneMove,c0);Add(CueHorn,c0+T);Add(CueHorn,c0+T+W*.5);Add(CueRelease,c0+T+W);Add(CueImpact,c0+T+W+F);Add(CueHoist,c0+T+W+F+D+G);break;}
                        case Vent:{double quiet=Math.Max(0,h.period-h.warn-h.dwell);Add(CueVentHiss,c0+quiet);Add(CueVentBurst,c0+quiet+h.warn);break;}
                    }
                }
            }
            return count;
        }
        /// <summary>Throws on a hazard the presentation (and the simulation contract) cannot run as written.</summary>
        public static void Validate(FactoryHazard[] hazards)
        {
            if(hazards==null)return;var ids=new System.Collections.Generic.HashSet<string>();
            foreach(var h in hazards)
            {
                void Need(bool ok,string what){if(!ok)throw new InvalidOperationException("Factory hazard "+h?.id+": "+what);}
                Need(h!=null&&!string.IsNullOrEmpty(h.id)&&ids.Add(h.id),"missing or duplicate id");Need(h.kind==SawSlot||h.kind==SawRail||h.kind==Crane||h.kind==Press||h.kind==Vent,"unknown kind "+h.kind);
                Need(h.period>0&&h.start>=0&&h.offset>=0&&h.warn>0,"period, start, offset or warn");
                double busy=h.kind==SawSlot?h.warn+2*h.rise+h.travel:h.kind==SawRail?h.warn+2*h.travel+h.dwell:h.kind==Press?h.warn+h.rise+h.dwell+h.travel:h.kind==Vent?h.warn+h.dwell:h.travel+h.warn+h.rise+h.dwell+RegripSeconds+h.hoist;
                Need(busy<=h.period+1e-6,"its motion ("+busy+" s) exceeds the period");
                if(h.kind==SawSlot||h.kind==SawRail){Need(h.radius>0&&h.travel>0&&Math.Abs(h.axis.Length-1)<1e-3,"blade radius, travel or axle");}
                if(h.kind==SawSlot)Need(h.rise>0,"rise");if(h.kind==Press)Need(h.rise>0&&h.travel>0&&h.size.y>0,"press slam, lift or ram");
                if(h.kind==Crane)Need(h.zones!=null&&h.zones.Length>0&&Array.TrueForAll(h.zones,z=>z!=null&&h.carry>z.center.y+z.size.y)&&h.rise>0&&h.hoist>0&&h.size.x>0,"crane zones, carry, fall or hoist");
            }
        }
    }
}
