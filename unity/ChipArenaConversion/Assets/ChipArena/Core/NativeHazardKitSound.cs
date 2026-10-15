using System;

namespace ChipArena.UnityConversion
{
    /// <summary>A kit loop the frame wants: its bank key, place, gain (falloff, duck and trims applied; 0..1), the piece and a
    /// rate (beacon motors and saw whines follow their speed).</summary>
    public struct NativeKitLoop {public string Key;public V3 At;public float Gain,Rate;public int Piece;}
    /// <summary>A kit one-shot: key, place, gain, rate (1 natural), the clock time it sounds at (it may be scheduled a little
    /// ahead), the variant to play (-1 the bank's bag) and, for a beat-locked klaxon, the room it has (s; 0 free).</summary>
    public struct NativeKitCue {public string Key;public V3 At;public float Gain,Rate,Room;public double Time;public int Piece,Variant;}

    /// <summary>
    /// The kit's sound (docs/HAZARD-KIT.md "Audio"), pure: which loops sound and which one-shots fall in a frame, with the
    /// kit's spatial falloff (full inside `inner`, a smooth roll to silence at `outer`, unlike the bank's endless 1/(1+.13d)),
    /// a duck under combat (alarms give way to shots, hits and blasts near the listener) and a loudness cap across alarms
    /// (two alarms together are not twice as loud). Keys have a built-in synthesized fallback (<see cref="NativeKitSynth"/>),
    /// so a build without LocalAssets still hears every piece. Allocation free.
    /// </summary>
    public sealed class NativeKitSoundPlan
    {
        public const int MaxLoops=8,MaxCues=48,MaxTrips=8;
        public const float AlarmInner=5,AlarmOuter=42,KlaxonInner=7,KlaxonOuter=60,MachineInner=2.5f,MachineOuter=22;
        public readonly NativeKitLoop[] Loops=new NativeKitLoop[MaxLoops];public int LoopCount;
        public readonly NativeKitCue[] Cues=new NativeKitCue[MaxCues];public int CueCount;
        /// <summary>The clock times alarms with `react` tripped within earshot this frame (the music's dip and swell).</summary>
        public readonly double[] Trips=new double[MaxTrips];public int TripCount;
        private readonly double[] times=new double[24];

        /// <summary>A kit key's voice priority (the bank contract's).</summary>
        public static NativeSoundPriority Priority(string key)=>NativeSoundKeys.Find(key,out var d)?d.Priority:NativeSoundPriority.Low;
        public static float Falloff(float d,float inner,float outer){if(d<=inner)return 1;if(d>=outer)return 0;double u=(d-inner)/(outer-inner);return (float)(.5+.5*Math.Cos(Math.PI*u));}
        /// <summary>The alarms' duck under combat: `combat` 0 quiet .. 1 a firefight at the listener; alarms keep at least 40%.</summary>
        public static float Duck(float combat)=>1-.6f*Math.Max(0,Math.Min(1,combat));
        /// <summary>The cap across alarm loops: n alarms of gains g sum to at most 1 (each scaled by 1/sqrt of their power sum over 1).</summary>
        public static float Cap(float sumSquares)=>sumSquares<=1?1:(float)(1/Math.Sqrt(sumSquares));
        public static string AlarmLoop(KitRecord r)=>r.sounds?.loop??NativeHazardKit.Look(r.look).Loop;
        private static float Dist(V3 a,V3 b){float dx=a.x-b.x,dy=a.y-b.y,dz=a.z-b.z;return (float)Math.Sqrt(dx*dx+dy*dy+dz*dz);}
        private float Inner(KitRecord r,float d)=>r.sounds!=null&&r.sounds.inner>0?r.sounds.inner:d;
        private float Outer(KitRecord r,float d)=>r.sounds!=null&&r.sounds.outer>0?r.sounds.outer:d;
        private float Trim(KitRecord r)=>(r.gain>0?r.gain:1)*(r.sounds!=null&&r.sounds.gain>0?r.sounds.gain:1);

        /// <summary>
        /// The frame's loops and cues for a listener at `ear`, over the clock window (from, to] (a caller keeping its last
        /// `to` hears each cue once; a clock that ran back restarts the window). `combat` ducks the alarms.
        /// </summary>
        public void Build(NativeKitScene scene,V3 ear,double from,double to,float combat)
        {
            LoopCount=CueCount=TripCount=0;if(scene==null||!(to>from))return;if(double.IsNegativeInfinity(from)||to-from>.5)from=to-.5;
            float duck=Duck(combat),power=0;
            for(int i=0;i<scene.Records.Length;i++)
            {
                var r=scene.Records[i];if(r.silent)continue;var p=scene.Phases[i];var at=scene.At[i];float d=Dist(at,ear),trim=Trim(r);
                bool tripped=p.State==NativeKitState.Arming&&Crossed(to-p.Since,from,to);
                switch(r.kind)
                {
                    case NativeHazardKit.Beacon:case NativeHazardKit.Strobe:
                    {
                        float g=Falloff(d,Inner(r,AlarmInner),Outer(r,AlarmOuter));
                        if(tripped){Cue("alarm.trip",at,.7f*g*trim,1,to-p.Since,i);if(r.react&&g>.05f)Trip(to-p.Since);}
                        bool sounding=p.State==NativeKitState.Active||p.State==NativeKitState.Arming||p.State==NativeKitState.Cooldown;
                        if(sounding&&(r.kind==NativeHazardKit.Beacon||r.sounds?.loop!=null)){float lv=(p.State==NativeKitState.Cooldown?p.Level:p.State==NativeKitState.Arming?.5f+.5f*p.Level:1)*g*duck*trim;if(lv>.01f){power+=lv*lv;Loop(AlarmLoop(r),at,lv,1,i);}}
                        if(r.kind==NativeHazardKit.Beacon&&sounding){float m=Falloff(d,Inner(r,MachineInner),Outer(r,10))*NativeHazardKitClock.BeaconLight(p,scene.Mode);if(m>.02f)Loop("alarm.beacon",at,.5f*m*trim,.9f+.2f*m,i);}
                        break;
                    }
                    case NativeHazardKit.Klaxon:
                    {
                        float g=Falloff(d,Inner(r,KlaxonInner),Outer(r,KlaxonOuter))*trim;if(tripped&&r.react&&g>.05f)Trip(to-p.Since);if(g<.01f)break;
                        bool locked=NativeHazardKit.BeatLocked(r)&&scene.Beat.On;int n=NativeHazardKitClock.Blasts(r,p,to,from,to+.06,scene.Beat,locked,2,times);
                        double room=0;if(locked&&scene.Beat.At(out _,out _,out double bp))room=bp*2;
                        for(int k=0;k<n;k++)Cue(r.sounds?.pulse??"alarm.klaxon",at,.95f*g*duck,1,times[k],i,(float)room);
                        break;
                    }
                    case NativeHazardKit.Turret:TurretSound(r,scene.Turrets[i],at,d,trim,from,to,i);break;
                    case NativeHazardKit.Saw:SawSound(r,p,scene.Saws[i],at,d,trim,from,to,i);if(scene.HitIn(i,from,to,out double hit))SawHit(scene,i,ear,hit);break;
                }
            }
            // The cap across alarm loops (the beacon motors are machines: uncapped).
            float cap=Cap(power);if(cap<1)for(int l=0;l<LoopCount;l++)if(Loops[l].Key!="alarm.beacon"&&Loops[l].Key.StartsWith("alarm.",StringComparison.Ordinal))Loops[l].Gain*=cap;
        }
        private static bool Crossed(double t,double from,double to)=>t>from&&t<=to;
        private void Loop(string key,V3 at,float gain,float rate,int piece)
        {
            if(gain<=.005f)return;int slot=LoopCount<Loops.Length?LoopCount++:-1;
            if(slot<0){int worst=0;for(int i=1;i<Loops.Length;i++)if(Loops[i].Gain<Loops[worst].Gain)worst=i;if(Loops[worst].Gain>=gain)return;slot=worst;}
            Loops[slot]=new NativeKitLoop{Key=key,At=at,Gain=Math.Min(1,gain),Rate=rate,Piece=piece};
        }
        private void Cue(string key,V3 at,float gain,float rate,double time,int piece,float room=0,int variant=-1)
        {if(gain<=.01f||CueCount>=Cues.Length)return;Cues[CueCount++]=new NativeKitCue{Key=key,At=at,Gain=Math.Min(1.2f,gain),Rate=rate,Time=time,Piece=piece,Variant=variant,Room=room};}
        private void Trip(double at){if(TripCount<Trips.Length)Trips[TripCount++]=at;}

        // The turret: the scan hum while it hunts, a servo on each sweep's turn and the snap to a target, the accelerating
        // lock beeps, the spin-up as the lock completes, every round, the overheat purge, sparks while damaged, the break.
        private void TurretSound(KitRecord r,in NativeTurretView v,V3 at,float d,float trim,double from,double to,int i)
        {
            float g=Falloff(d,Inner(r,MachineInner),Outer(r,34))*trim;if(g<.01f)return;bool entered=Crossed(v.StateAt,from,to);
            switch(v.State)
            {
                case NativeTurretState.Scan:Loop("turret.scan",at,.35f*g,1,i);
                {double turn=Math.PI/1.1;double k0=Math.Floor((from-v.StateAt)/turn),k1=Math.Floor((to-v.StateAt)/turn);if(k1>k0&&k1>=0)Cue("turret.servo",at,.45f*g,1,v.StateAt+k1*turn,i);}break;
                case NativeTurretState.Acquire:Loop("turret.scan",at,.3f*g,1.15f,i);if(entered){Cue("turret.servo",at,.7f*g,1.1f,v.StateAt,i);Cue("turret.lock",at,.55f*g,.9f,v.StateAt+.05,i);}break;
                case NativeTurretState.Lock:
                {
                    int n=NativeTurretPreview.LockBeeps(v.StateAt,from,to+.05,times);for(int k=0;k<n;k++)Cue("turret.lock",at,.8f*g,1+.3f*(float)((times[k]-v.StateAt)/NativeTurretPreview.LockTime),times[k],i);
                    if(entered)Cue("turret.spinup",at,.8f*g,1,v.StateAt,i);break;
                }
                case NativeTurretState.Fire:
                {
                    double rate=r.rate>0?r.rate:NativeTurretPreview.Rate;for(int k=0;k<8;k++){double t=v.LastShot-k/rate;if(t<=from||t<v.StateAt-1e-6)break;if(t<=to)Cue("turret.fire",at,.9f*g,1,t,i);}break;
                }
                case NativeTurretState.Overheat:if(entered)Cue("turret.overheat",at,.85f*g,1,v.StateAt,i);break;
                case NativeTurretState.Destroyed:if(entered)Cue("turret.break",at,1f*g,1,v.StateAt,i);break;
                case NativeTurretState.Disabled:if(entered)Cue("turret.break",at,.5f*g,1.25f,v.StateAt,i);break;
            }
            if(v.Health<.5f&&v.State!=NativeTurretState.Offline){double k0=Math.Floor(from*3.1+i),k1=Math.Floor(to*3.1+i);if(k1>k0&&Hash(i,(int)k1)<(v.State==NativeTurretState.Destroyed?.25:.55))Cue("turret.spark",at,.6f*g,1,(k1-i)/3.1,i);}
        }
        // The saw: its whine while the blade turns (rate and level with its speed), the spin-up at the arming, the spin-down
        // at the cooldown, a scrape every .45 s while it grinds.
        private void SawSound(KitRecord r,in NativeKitPhase p,in NativeKitSawPose s,V3 at,float d,float trim,double from,double to,int i)
        {
            float g=Falloff(d,Inner(r,MachineInner+1),Outer(r,30))*trim;if(g<.01f)return;V3 hub=s.Hub;
            if(s.Rate>3)Loop("saw.whine",hub,.75f*g*(float)Math.Min(1,s.Rate/NativeKitSaw.SpinRate),.7f+.3f*(float)(s.Rate/NativeKitSaw.SpinRate),i);
            if(p.State==NativeKitState.Arming&&Crossed(to-p.Since,from,to))Cue("saw.spinup",hub,.85f*g,1,to-p.Since,i);
            if(p.State==NativeKitState.Cooldown&&Crossed(to-p.Since,from,to))Cue("saw.spindown",hub,.8f*g,1,to-p.Since,i);
            if(s.Grinding){double k0=Math.Floor(from/.45),k1=Math.Floor(to/.45);if(k1>k0)Cue("saw.scrape",s.Contact,.6f*g,1,k1*.45,i);}
        }
        /// <summary>A saw's hit (the authority's hit event at `at`): the wet chop at the blade.</summary>
        public void SawHit(NativeKitScene scene,int i,V3 ear,double at)
        {
            if(scene==null||i<0||i>=scene.Records.Length)return;var r=scene.Records[i];float g=Falloff(Dist(scene.Saws[i].Hub,ear),MachineInner+1,30)*Trim(r);Cue("saw.hit",scene.Saws[i].Hub,1f*g,1,at,i);
        }
        private static double Hash(int a,int b){unchecked{uint x=(uint)a*0x9E3779B9U^(uint)b*0x85EBCA6BU;x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x/4294967296.0;}}
    }

    /// <summary>
    /// Built-in synthesized fallbacks for every kit key (original, deterministic; mono at <see cref="Rate"/>): what a build
    /// without the licensed LocalAssets plays, so every piece still sounds. Loops end where they begin (whole cycles).
    /// </summary>
    public static class NativeKitSynth
    {
        public const int Rate=22050;
        public static readonly string[] Keys={"alarm.biohazard","alarm.lockdown","alarm.evacuation","alarm.klaxon","alarm.trip","alarm.beacon","turret.servo","turret.scan","turret.lock",
            "turret.spinup","turret.fire","turret.overheat","turret.break","turret.spark","saw.whine","saw.spinup","saw.spindown","saw.scrape","saw.hit"};
        public static bool Has(string key)=>Array.IndexOf(Keys,key)>=0;
        public static bool Loop(string key)=>key=="alarm.biohazard"||key=="alarm.lockdown"||key=="alarm.evacuation"||key=="alarm.beacon"||key=="turret.scan"||key=="saw.whine";
        private static uint seed;
        private static double Noise(){unchecked{seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;return seed/4294967296.0*2-1;}}
        /// <summary>The fallback samples of `key` (null for keys the kit does not own).</summary>
        public static float[] Samples(string key)
        {
            seed=0x5eedu;if(key!=null)foreach(char c in key)seed=unchecked(seed*31+c);if(seed==0)seed=1;
            switch(key)
            {
                // A two-tone pulse (biohazard), a slow dive klaxon (lockdown), a rising wail (evacuation): each a whole number of cycles.
                case "alarm.biohazard":return Make(2.4,(t,n)=>{double f=t%.6<.3?880:660;return .35*Square(f,t,.3)*Gate(t%.3,.3);});
                case "alarm.lockdown":return Make(2.4,(t,n)=>{double u=t%1.2,f=330+220*Math.Min(1,u/.55);return u<.8?.38*Saw(f,t)*Env(u,.02,.8,.08):0;});
                case "alarm.evacuation":return Make(4.0,(t,n)=>.32*(Math.Sin(Phase(t,520,380,4.0))*.8+.2*Math.Sin(2*Phase(t,520,380,4.0))));
                case "alarm.klaxon":return Make(.72,(t,n)=>{double f=220*(1+.03*Math.Sin(2*Math.PI*30*t));return .5*(Saw(f,t)*.7+Square(f*1.5,t,.5)*.3)*Env(t,.015,.72,.12);});
                case "alarm.trip":return Make(.8,(t,n)=>.3*Math.Sin(2*Math.PI*(400*t+700*t*t/.8))*Env(t,.01,.8,.15));
                case "alarm.beacon":return Make(1.0,(t,n)=>.12*(Math.Sin(2*Math.PI*120*t)*.6+Math.Sin(2*Math.PI*240*t)*.25+Math.Sin(2*Math.PI*6*t)*.15*Math.Sin(2*Math.PI*360*t)));
                case "turret.servo":return Make(.32,(t,n)=>{double f=380+260*Math.Sin(Math.PI*t/.32);return .22*(Square(f,t,.5)*.4+Noise()*.15)*Env(t,.02,.32,.06);});
                case "turret.scan":return Make(1.0,(t,n)=>.08*(Math.Sin(2*Math.PI*1000*t)*(.5+.5*Math.Sin(2*Math.PI*4*t))+Math.Sin(2*Math.PI*150*t)*.4));
                case "turret.lock":return Make(.09,(t,n)=>.4*Math.Sin(2*Math.PI*1760*t)*Env(t,.003,.09,.03));
                case "turret.spinup":return Make(.8,(t,n)=>.3*Saw(80+900*t*t/.64,t)*Env(t,.05,.8,.1));
                case "turret.fire":return Make(.14,(t,n)=>.7*(Noise()*Math.Exp(-t*38)+Math.Sin(2*Math.PI*95*t)*Math.Exp(-t*30)*.8));
                case "turret.overheat":return Make(1.6,(t,n)=>.25*Noise()*Env(t,.04,1.6,.6)*(.6+.4*Math.Sin(2*Math.PI*9*t)));
                case "turret.break":return Make(.9,(t,n)=>.4*(Square(600*Math.Exp(-t*2.5),t,.5)*Math.Exp(-t*2)+Noise()*Math.Exp(-t*6)*.5)*Env(t,.005,.9,.2));
                case "turret.spark":return Make(.28,(t,n)=>.35*Noise()*(Noise()>.6?1:.1)*Env(t,.002,.28,.1));
                case "saw.whine":return Make(1.0,(t,n)=>.2*(Saw(180,t)*.6+Math.Sin(2*Math.PI*1440*t)*.25+Noise()*.12));
                case "saw.spinup":return Make(1.2,(t,n)=>.3*Saw(20+160*Math.Min(1,t/1.0),t)*Env(t,.05,1.2,.15));
                case "saw.spindown":return Make(1.4,(t,n)=>.3*Saw(180*(1-.85*t/1.4),t)*Env(t,.01,1.4,.4));
                case "saw.scrape":return Make(.5,(t,n)=>.3*Noise()*Math.Sin(2*Math.PI*2400*t)*Env(t,.01,.5,.2));
                case "saw.hit":return Make(.45,(t,n)=>.6*(Noise()*Math.Exp(-t*14)+Math.Sin(2*Math.PI*70*t)*Math.Exp(-t*10)));
            }
            return null;
        }
        private static float[] Make(double seconds,Func<double,int,double> f)
        {
            int n=(int)Math.Round(seconds*Rate);var data=new float[n];for(int i=0;i<n;i++){double v=f(i/(double)Rate,i);data[i]=(float)Math.Max(-1,Math.Min(1,v));}
            return data;
        }
        private static double Saw(double f,double t){double x=f*t;return 2*(x-Math.Floor(x+.5));}
        private static double Square(double f,double t,double duty){double x=f*t;return x-Math.Floor(x)<duty?1:-1;}
        private static double Gate(double u,double len)=>Math.Min(1,Math.Min(u/.01,(len-u)/.02));
        private static double Env(double t,double attack,double length,double release)=>Math.Max(0,Math.Min(1,Math.Min(t/attack,(length-t)/release)));
        // The phase of a tone sweeping f0 +- depth (cosine over `period`), integrated so the loop closes.
        private static double Phase(double t,double f0,double depth,double period)=>2*Math.PI*(f0*t+depth*(t/2-period/(4*Math.PI)*Math.Sin(2*Math.PI*t/period)));
    }
}
