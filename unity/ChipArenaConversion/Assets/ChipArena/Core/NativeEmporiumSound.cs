using System;

namespace ChipArena.UnityConversion
{
    /// <summary>A discrete Emporium hazard moment for the audio (<see cref="NativeEmporiumSound.Cues"/>): kind, hazard index, cycle,
    /// match time and place.</summary>
    public struct EmporiumCue {public int Kind,Hazard,Cycle;public double Time;public V3 At;}

    /// <summary>
    /// Top Dog Emporium's hazard sound (docs/arenas/EMPORIUM.md "Sound"): which cue plays when, as a pure function of the hazards, the
    /// round and the match clock (the same clocks the simulation and the rig run on: host, guests and the killcam hear the same
    /// moments), and the cues' synthesized samples (comic on purpose; owner clips may replace them later under the same keys):
    /// <list type="bullet">
    /// <item>The Wiener Run: an "ah-ooga" horn at the portal as the warning starts, a slide whistle rising through the danger, the
    /// wiener's rolling rumble as it bursts out, a boing as it dives into the far oven.</item>
    /// <item>The Bun Presses: a double bell ding as the warning starts, a wet SPLORT on the slam, a suction pop as it lifts.</item>
    /// <item>The Griddles: igniter clicks with the warning, a sizzle while they are live.</item>
    /// <item>The Geysers: three squelching pumps through the warning, a SPLOOSH and a spring boing on the burst.</item>
    /// </list>
    /// Pure and stateless: a caller keeping its last clock hears each cue once; a clock that runs back (a new round, a killcam rewind)
    /// restarts the window. Allocation free (the samples allocate once per key, on first use).
    /// </summary>
    public static class NativeEmporiumSound
    {
        public const int CueHorn=0,CueWhistle=1,CueRumble=2,CueBoing=3,CueDing=4,CueSplort=5,CuePop=6,CueClick=7,CueSizzle=8,CuePump=9,CueSploosh=10;
        public static readonly string[] Keys={"emporium.horn","emporium.whistle","emporium.rumble","emporium.boing","emporium.ding","emporium.splort","emporium.pop","emporium.click","emporium.sizzle","emporium.pump","emporium.sploosh"};
        /// <summary>Each cue's gain (before distance) and its reach (m: full within a fifth of it, silent past it).</summary>
        public static readonly float[] Gain={.85f,.6f,.8f,.7f,.75f,1f,.55f,.4f,.55f,.6f,.9f},Reach={70,60,70,55,50,55,40,35,40,40,55};

        /// <summary>Every cue with match time in (from, to] for these hazards in round `round` (fight started at `fightAt`), into
        /// `into` (at most its length; the count is returned). Only the fight's clock runs hazards; outside it nothing plays.</summary>
        public static int Cues(MapHazard[] hazards,int round,double fightAt,double from,double to,EmporiumCue[] into)
        {
            int count=0;if(hazards==null||into==null||!(to>from)||double.IsNaN(from)||double.IsInfinity(to))return 0;if(double.IsNegativeInfinity(from))from=to-1;
            for(int i=0;i<hazards.Length;i++)
            {
                var h=hazards[i];if(h==null||!(h.period>0))continue;
                double origin;switch(h.kind)
                {
                    case "maglev":origin=fightAt+h.first-h.warn;break;
                    case NativeWardZeroHazards.Burner:origin=fightAt+h.first+(round%2==0?h.offset:0);break;
                    case NativeFactoryHazards.Press:case NativeFactoryHazards.Vent:origin=fightAt+NativeFactoryHazards.ArmDelay+h.start+h.offset;break;
                    default:continue;
                }
                double first=Math.Floor((Math.Max(from,origin-1)-origin)/h.period)-1,last=Math.Floor((to-origin)/h.period);
                for(double k=Math.Max(0,first);k<=last;k++)
                {
                    double c0=origin+k*h.period;int n=(int)k;
                    void Add(int kind,double at,V3 where){if(at>from&&at<=to&&count<into.Length)into[count++]=new EmporiumCue{Kind=kind,Hazard=i,Cycle=n,Time=at,At=where};}
                    switch(h.kind)
                    {
                        case "maglev":
                        {
                            int d=((n+round)%2+2)%2==0?1:-1;double near=h.center.x-d*h.size.x/2,far=h.center.x+d*h.size.x/2,run=(h.size.x+h.length)/h.speed;
                            var pn=new V3((float)near,1.2f,h.center.z);var pf=new V3((float)far,1.2f,h.center.z);
                            Add(CueHorn,c0,pn);Add(CueWhistle,c0+h.warn-h.danger,pn);Add(CueRumble,c0+h.warn,pn);Add(CueBoing,c0+h.warn+run-h.length/h.speed,pf);break;
                        }
                        case NativeWardZeroHazards.Burner:{var p=new V3(h.center.x,.5f,h.center.z);Add(CueClick,c0,p);Add(CueSizzle,c0+h.warn,p);break;}
                        case NativeFactoryHazards.Press:
                        {
                            double open=Math.Max(0,h.period-h.warn-h.rise-h.dwell-h.travel);var p=new V3(h.a.x,2.5f,h.a.z);
                            Add(CueDing,c0+open,p);Add(CueSplort,c0+open+h.warn+h.rise,new V3(h.a.x,.5f,h.a.z));Add(CuePop,c0+open+h.warn+h.rise+h.dwell,p);break;
                        }
                        case NativeFactoryHazards.Vent:
                        {
                            double quiet=Math.Max(0,h.period-h.warn-h.dwell);var p=new V3(h.a.x,.6f,h.a.z);
                            for(int s=0;s<3;s++)Add(CuePump,c0+quiet+h.warn*(s+.35)/3,p);Add(CueSploosh,c0+quiet+h.warn,p);break;
                        }
                    }
                }
            }
            return count;
        }
        /// <summary>A cue's gain at distance `d` (m): full near, easing to silence at its reach.</summary>
        public static float Falloff(int kind,float d){float r=Reach[kind];if(d<=r*.2f)return Gain[kind];if(d>=r)return 0;float u=(d-r*.2f)/(r*.8f);return Gain[kind]*(1-u)*(1-u);}

        // ---------------- the samples ----------------
        public const int Rate=22050;
        private static uint seed;private static readonly double[] Dings={0,.32},Clicks={0,.22,.44};
        private static double Noise(){unchecked{seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;return seed/4294967296.0*2-1;}}
        /// <summary>The synthesized samples of a cue (mono, <see cref="Rate"/> Hz, -1..1); null for an unknown kind.</summary>
        public static float[] Samples(int kind)
        {
            seed=0x5eedu+(uint)kind*7919u;double lp=0,lp2=0,ph=0,ph2=0;
            switch(kind)
            {
                case CueHorn:return Make(.95,t=>{double f=t<.28?150+120*t/.28:270-130*Math.Min(1,(t-.32)/.55);ph+=f/Rate;double wah=t<.3?.6:.6+.4*Math.Sin(Math.PI*Math.Min(1,(t-.3)/.6));
                    double v=.55*Saw(ph)*wah+.35*Square(ph*2,.4)*(1-wah)*.6+.1*Math.Sin(2*Math.PI*ph*3);return v*Env(t,.02,.95,.12)*(t>.28&&t<.33?.25:1);});
                case CueWhistle:return Make(1.3,t=>{double f=520*Math.Pow(3.4,Math.Min(1,t/1.15))*(1+.025*Math.Sin(2*Math.PI*6.5*t));ph+=f/Rate;return .42*(Math.Sin(2*Math.PI*ph)+.12*Noise())*Env(t,.04,1.3,.1);});
                case CueRumble:return Make(1.6,t=>{ph+=(52+10*Math.Sin(t*9))/Rate;lp+=(Noise()-lp)*.04;double bump=.6+.4*Math.Abs(Math.Sin(Math.PI*t*5.2));return (.6*Math.Sin(2*Math.PI*ph)+.35*Saw(ph*2)*.4+.8*lp)*bump*Env(t,.03,1.6,.5);});
                case CueBoing:return Make(.75,t=>{double f=330*(1+.35*Math.Sin(2*Math.PI*18*t)*Math.Exp(-t*4))*(1-.25*t);ph+=f/Rate;return .5*Math.Sin(2*Math.PI*ph)*Math.Exp(-t*3.2)*Env(t,.005,.75,.05);});
                case CueDing:return Make(1.3,t=>{double v=0;foreach(double t0 in Dings){double u=t-t0;if(u<0)continue;v+=Math.Exp(-u*3.5)*(Math.Sin(2*Math.PI*1046*u)+.5*Math.Sin(2*Math.PI*2.76*1046*u)*Math.Exp(-u*6)+.25*Math.Sin(2*Math.PI*5.4*1046*u)*Math.Exp(-u*12));}return .3*v;});
                case CueSplort:return Make(.6,t=>{lp+=(Noise()-lp)*(.08+.3*Math.Exp(-t*10));lp2+=(lp-lp2)*.25;ph+=(90*Math.Exp(-t*6)+40)/Rate;double squelch=Math.Sin(2*Math.PI*(380+260*Math.Sin(t*38))*t)*Math.Exp(-t*7)*.25;return (.9*Math.Sin(2*Math.PI*ph)*Math.Exp(-t*8)+1.4*lp2*Math.Exp(-t*5)+squelch)*Env(t,.002,.6,.15);});
                case CuePop:return Make(.28,t=>{ph+=(900*Math.Exp(-t*18)+120)/Rate;return .5*Math.Sin(2*Math.PI*ph)*Math.Exp(-t*14)+(t<.004?.6*Noise():0);});
                case CueClick:return Make(.7,t=>{double v=0;foreach(double t0 in Clicks){double u=t-t0;if(u>=0&&u<.03)v+=Noise()*Math.Exp(-u*220)+Math.Sin(2*Math.PI*2400*u)*Math.Exp(-u*160)*.5;}return .5*v;});
                case CueSizzle:return Make(1.9,t=>{lp+=(Noise()-lp)*.6;double hp=Noise()-lp;double crack=Noise()>.985?Noise()*.9:0;return (.18*hp+crack*.5)*Env(t,.05,1.9,.6)*(.8+.2*Math.Sin(t*23));});
                case CuePump:return Make(.3,t=>{lp+=(Noise()-lp)*.12;double res=Math.Sin(2*Math.PI*(220+600*t)*t);return (.7*lp*res+.3*Math.Sin(2*Math.PI*(140-200*t)*t)*Math.Exp(-t*10))*Env(t,.01,.3,.1);});
                case CueSploosh:return Make(1.1,t=>{lp+=(Noise()-lp)*(.05+.35*Math.Exp(-t*4));lp2+=(lp-lp2)*.3;double gl=0;if(t>.25){double u=t-.25;gl=Math.Sin(2*Math.PI*(260+180*Math.Sin(u*31))*u)*Math.Exp(-u*3)*.25*(Math.Sin(u*47)>0?1:.3);}
                    ph2+=(380*(1+.3*Math.Sin(2*Math.PI*16*t)*Math.Exp(-t*3)))/Rate;double boing=t>.08?.3*Math.Sin(2*Math.PI*ph2)*Math.Exp(-(t-.08)*3.5):0;return (1.5*lp2*Math.Exp(-t*2.5)+gl+boing)*Env(t,.004,1.1,.25);});
                default:return null;
            }
        }
        private static float[] Make(double seconds,Func<double,double> f)
        {
            int n=(int)Math.Round(seconds*Rate);var data=new float[n];for(int i=0;i<n;i++){double v=f(i/(double)Rate);data[i]=(float)Math.Max(-1,Math.Min(1,v));}
            return data;
        }
        private static double Saw(double ph)=>2*(ph-Math.Floor(ph+.5));
        private static double Square(double ph,double duty)=>ph-Math.Floor(ph)<duty?1:-1;
        private static double Env(double t,double attack,double length,double release)=>Math.Max(0,Math.Min(1,Math.Min(t/attack,(length-t)/release)));
    }
}
