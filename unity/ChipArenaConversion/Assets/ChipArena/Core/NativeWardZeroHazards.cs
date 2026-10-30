using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Ward Zero's timed hazards as pure functions of (round index, FightAt, match time) and the hazard's definition
    /// (docs/arenas/WARDZERO-SIMULATION-CONTRACT.md section 5): the reference timing the contract pins, which the presentation
    /// evaluates every frame on the arena's presentation clock, so host, guests and the killcam replay agree and nothing is
    /// sent. The simulation's damage, kill and collider rules are Astra's.
    /// <list type="bullet">
    /// <item>`grid` (the live grating on the atrium bridges): Cyber City's kind as it is (NativeCyberCityHazards.Grid: Idle,
    /// Warn = charging, Active = live, Clear = discharge). Astra's damage path for it already exists.</item>
    /// <item>`burner` (the incinerator rows) and `gas` (the decon locks): cycle k's warning starts at FightAt + first + k *
    /// period (+ offset on even rounds); Warn for `warn` s, Active for `live` s (the damage window), Clear for `dwell` s
    /// (smoke / the gas fading, harmless), then Idle.</item>
    /// <item>`shutter` (the atrium's blast shutters): the same cycle start; Warn for `warn` s (klaxon and beacons, the door
    /// still up), Danger while it closes (`travel` s), Active while shut (`dwell` s), Clear while it opens (`rise` s).</item>
    /// </list>
    /// Phases run in Fight and Result only (<see cref="At"/>): FightAt holds the last round's start in every other phase.
    /// Level: a warning's build 0..1, 1 while active, a clear's fade 1..0; a shutter's closure is <see cref="Closure"/>.
    /// <see cref="Draft"/> is the draft blockout's hazards (docs/arenas/scripts/wardzero_m1.py) for previews and checks.
    /// Allocation free (struct results).
    /// </summary>
    public static class NativeWardZeroHazards
    {
        public const string Grid="grid",Burner="burner",Gas="gas",Shutter="shutter";
        public static bool IsGrid(MapHazard h)=>h!=null&&h.kind==Grid;
        public static bool IsBurst(MapHazard h)=>h!=null&&(h.kind==Burner||h.kind==Gas);
        public static bool IsShutter(MapHazard h)=>h!=null&&h.kind==Shutter;
        /// <summary>The state of any Ward Zero kind; an unknown kind or a missing definition is always Idle.</summary>
        public static NativeHazardState Evaluate(MapHazard h,int round,double fightAt,double time)
        =>IsGrid(h)?NativeCyberCityHazards.Grid(h,round,fightAt,time):IsBurst(h)?Burst(h,round,fightAt,time):IsShutter(h)?ShutterState(h,round,fightAt,time):NativeCyberCityHazards.Idle();
        /// <summary>The state during a match phase: the clock runs in Fight and on through Result; Idle everywhere else.</summary>
        public static NativeHazardState At(MapHazard h,NativePhase phase,int round,double fightAt,double time)
        =>phase==NativePhase.Fight||phase==NativePhase.Result?Evaluate(h,round,fightAt,time):NativeCyberCityHazards.Idle();
        /// <summary>Seconds into the hazard's current cycle (its warning starts at 0) and the cycle index; false before the first.</summary>
        public static bool Cycle(MapHazard h,int round,double fightAt,double time,out int k,out double u)
        {
            k=-1;u=time-fightAt-h.first-(round%2==0?h.offset:0);if(double.IsNaN(u)||!(h.period>0))return false;if(u<0)return false;
            double c=Math.Floor(u/h.period);k=c>int.MaxValue?int.MaxValue:(int)c;u-=c*h.period;return true;
        }
        /// <summary>A burner row set or a decon lock: Warn, Active (damage), Clear (harmless fade), Idle.</summary>
        public static NativeHazardState Burst(MapHazard h,int round,double fightAt,double time)
        {
            var s=NativeCyberCityHazards.Idle();if(h==null||!(h.period>0))return s;
            if(!Cycle(h,round,fightAt,time,out int k,out double u)){double lead=-(time-fightAt-h.first-(round%2==0?h.offset:0));if(!double.IsNaN(lead))s.Until=lead;return s;}
            s.Cycle=k;double w=h.warn,a=h.live,d=h.dwell;
            if(u<w){s.Phase=NativeHazardPhase.Warn;s.Since=u;s.Until=w-u;s.Level=(float)Clamp01(u/Math.Max(.001,w));}
            else if(u<w+a){s.Phase=NativeHazardPhase.Active;s.Since=u-w;s.Until=w+a-u;s.Level=1;}
            else if(u<w+a+d){s.Phase=NativeHazardPhase.Clear;s.Since=u-w-a;s.Until=w+a+d-u;s.Level=(float)Clamp01(1-(u-w-a)/Math.Max(.001,d));}
            else{s.Phase=NativeHazardPhase.Idle;s.Since=u-w-a-d;s.Until=h.period-u;s.Level=0;}
            return s;
        }
        /// <summary>A blast shutter: Warn (door up, alarm), Danger (closing over `travel`), Active (shut for `dwell`), Clear
        /// (opening over `rise`), Idle.</summary>
        public static NativeHazardState ShutterState(MapHazard h,int round,double fightAt,double time)
        {
            var s=NativeCyberCityHazards.Idle();if(h==null||!(h.period>0))return s;
            if(!Cycle(h,round,fightAt,time,out int k,out double u)){double lead=-(time-fightAt-h.first-(round%2==0?h.offset:0));if(!double.IsNaN(lead))s.Until=lead;return s;}
            s.Cycle=k;double w=h.warn,c=h.travel,d=h.dwell,o=h.rise;
            if(u<w){s.Phase=NativeHazardPhase.Warn;s.Since=u;s.Until=w-u;s.Level=(float)Clamp01(u/Math.Max(.001,w));}
            else if(u<w+c){s.Phase=NativeHazardPhase.Danger;s.Since=u-w;s.Until=w+c-u;s.Level=(float)Smooth((u-w)/Math.Max(.001,c));}
            else if(u<w+c+d){s.Phase=NativeHazardPhase.Active;s.Since=u-w-c;s.Until=w+c+d-u;s.Level=1;}
            else if(u<w+c+d+o){s.Phase=NativeHazardPhase.Clear;s.Since=u-w-c-d;s.Until=w+c+d+o-u;s.Level=(float)(1-Smooth((u-w-c-d)/Math.Max(.001,o)));}
            else{s.Phase=NativeHazardPhase.Idle;s.Since=u-w-c-d-o;s.Until=h.period-u;s.Level=0;}
            return s;
        }
        /// <summary>A shutter's door closure: 0 fully up (open) .. 1 down (shut).</summary>
        public static float Closure(in NativeHazardState s)=>s.Phase==NativeHazardPhase.Danger||s.Phase==NativeHazardPhase.Active||s.Phase==NativeHazardPhase.Clear?s.Level:0;
        /// <summary>The damage tick a burst hazard is on (0, 1, ... while Active; -1 otherwise): ticks at the start of the live
        /// window and every `tick` s after it, the per-pilot key the contract asks for.</summary>
        public static int Tick(MapHazard h,in NativeHazardState s)=>s.Phase==NativeHazardPhase.Active&&h!=null&&h.tick>0?(int)Math.Floor(s.Since/h.tick+1e-9):-1;
        /// <summary>The next telegraph's lead (s until the next Warn; 0 while warning or active), for cue timing and nav gates.</summary>
        public static double Lead(in NativeHazardState s)=>s.Phase==NativeHazardPhase.Idle?s.Until:0;
        public static double Smooth(double t){t=t<0?0:t>1?1:t;return t*t*(3-2*t);}
        private static double Clamp01(double v)=>v<0?0:v>1?1:v;

        /// <summary>The draft blockout's hazards (docs/arenas/scripts/wardzero_m1.py `hazards`, sorted by id), for offline
        /// previews and checks until Astra's map content carries its own.</summary>
        public static MapHazard[] Draft()
        {
            MapVolume Row(float x0,float x1,float z)=>new MapVolume{center=new V3((x0+x1)/2,1.75f,z),size=new V3(x1-x0,3.5f,2)};
            MapHazard Burn(string id,float sx,float[] zs,float first,float offset)
            {float x0=sx>0?30.6f:-38,x1=sx>0?38:-30.6f;var zones=new MapVolume[zs.Length];for(int i=0;i<zs.Length;i++)zones[i]=Row(x0,x1,zs[i]);
             return new MapHazard{id=id,kind=Burner,center=new V3((x0+x1)/2,1.75f,0),size=new V3(x1-x0,3.5f,28),axis=new V3(0,0,1),zones=zones,first=first,period=10,offset=offset,warn=1.5f,live=2.5f,dwell=1,tick=.25f,damage=10};}
            MapHazard Shut(string id,V3 c,V3 s,float first,float offset)=>new MapHazard{id=id,kind=Shutter,center=c,size=s,axis=new V3(0,0,1),zones=Array.Empty<MapVolume>(),first=first,period=36,offset=offset,warn=3,travel=1.2f,dwell=9,rise=1.5f,push=6};
            MapHazard GasLock(string id,V3 c,float first,float offset)=>new MapHazard{id=id,kind=Gas,center=c,size=new V3(7.2f,7,5.6f),axis=new V3(0,0,1),zones=Array.Empty<MapVolume>(),first=first,period=24,offset=offset,warn=2.5f,live=4,dwell=3,tick=.5f,damage=6};
            return new[]
            {
                Burn("siren-burner-e-a",1,new[]{-9f,3f},6,5),Burn("siren-burner-e-b",1,new[]{-3f,9f},11,-5),
                Burn("siren-burner-w-a",-1,new[]{9f,-3f},6,5),Burn("siren-burner-w-b",-1,new[]{3f,-9f},11,-5),
                GasLock("siren-gas-nw",new V3(-28,9.5f,22.2f),15,12),GasLock("siren-gas-se",new V3(28,9.5f,-22.2f),27,-12),
                new MapHazard{id="siren-grid-e",kind=Grid,center=new V3(6,6.5f,0),size=new V3(3,1,20),axis=new V3(0,0,1),zones=Array.Empty<MapVolume>(),first=8,period=16,offset=8,charge=2.5f,live=2,tick=.25f,damage=13},
                new MapHazard{id="siren-grid-w",kind=Grid,center=new V3(-6,6.5f,0),size=new V3(3,1,20),axis=new V3(0,0,1),zones=Array.Empty<MapVolume>(),first=16,period=16,offset=-8,charge=2.5f,live=2,tick=.25f,damage=13},
                Shut("siren-shutter-e",new V3(16.2f,2.25f,0),new V3(.4f,4.5f,6),38,-18),Shut("siren-shutter-n",new V3(0,2.25f,14.2f),new V3(6,4.5f,.4f),20,18),
                Shut("siren-shutter-s",new V3(0,2.25f,-14.2f),new V3(6,4.5f,.4f),20,18),Shut("siren-shutter-w",new V3(-16.2f,2.25f,0),new V3(.4f,4.5f,6),38,-18),
            };
        }
    }
}
