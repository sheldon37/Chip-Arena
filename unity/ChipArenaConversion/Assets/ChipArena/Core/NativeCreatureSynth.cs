using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The built-in synthesized fallbacks of the footsteps, the landing and the zombies' voices and footfalls (as NativeKitSynth for
    /// the hazard kit): a build without the owner's LocalAssets bank, or a bank without these keys, still hears every one. Original
    /// deterministic PCM (no recording, nothing licensed), 22.05 kHz mono, <see cref="Variants"/> takes per key from the key's seed;
    /// each take is normalised as the bank measures its clips (<see cref="MomentaryMax"/>: ITU-R BS.1770 K-weighting, the loudest
    /// 400 ms, mono as dual mono) to the level a bank clip of its family plays at: steps <see cref="StepLevel"/> (the bank's -24 LUFS
    /// steps with their 1.8 gain), the landing <see cref="LandLevel"/>, voices <see cref="VoiceLevel"/> LUFS; then soft-limited under
    /// full scale.
    /// <list type="bullet">
    /// <item>Voices: a glottal saw on a falling pitch with jitter, rasp (noise in step with the pitch) and growl (a subharmonic),
    /// through three formants; gurgles are bubbles in a throat; the spit a hork and a splatter.</item>
    /// <item>Steps: a heel and a toe hit (noise burst and thump) with the surface's colour: metal modes, a hollow wood knock, gravel
    /// and snow grains; the zombies' scuffing drag, bare slap, heavy stomp with debris and the crawl's slap and cloth drag.</item>
    /// </list>
    /// </summary>
    public static class NativeCreatureSynth
    {
        public const int Rate=22050,Variants=3;public const float StepLevel=-19,LandLevel=-20,VoiceLevel=-18;
        public static readonly string[] Keys={"move.step.concrete","move.step.gravel","move.step.metal","move.step.snow","move.step.stone","move.step.wood","move.land",
            "pve.zombie.moan","pve.zombie.groan","pve.zombie.shriek","pve.zombie.attack","pve.zombie.hurt","pve.zombie.death","pve.zombie.runner.snarl",
            "pve.zombie.spitter.gurgle","pve.zombie.spitter.spit","pve.zombie.brute.grunt","pve.zombie.step.shuffle","pve.zombie.step.run","pve.zombie.step.heavy","pve.zombie.step.crawl"};
        public static bool Has(string key)=>key!=null&&Array.IndexOf(Keys,key)>=0;
        /// <summary>Whether `key` is a zombie call (NativeZombieVoice.Key), normalised to the voices' level.</summary>
        public static bool Voice(string key){if(key==null)return false;for(var c=NativeZombieCall.Moan;c<=NativeZombieCall.Death;c++)if(NativeZombieVoice.Key(c)==key)return true;return false;}

        private static uint state;
        private static double Noise(){unchecked{state^=state<<13;state^=state>>17;state^=state<<5;return state/4294967296.0*2-1;}}
        private static double U(){unchecked{state^=state<<13;state^=state>>17;state^=state<<5;return state/4294967296.0;}}

        /// <summary>Take `variant` (0..Variants-1) of `key`; null for keys this synth does not own.</summary>
        public static float[] Samples(string key,int variant)
        {
            if(!Has(key))return null;variant=Math.Max(0,Math.Min(Variants-1,variant));
            state=0x5eed7u+(uint)variant*0x9e3779b9u;foreach(char c in key)state=unchecked(state*31+c);if(state==0)state=1;
            double v=(variant-1)*.5+(U()-.5)*.3;  // the take's spread, about -0.65..0.65
            double[] x;
            switch(key)
            {
                case "move.step.concrete":x=Step(.24,v,0);break;case "move.step.stone":x=Step(.28,v,1);break;case "move.step.metal":x=Step(.34,v,2);break;
                case "move.step.wood":x=Step(.3,v,3);break;case "move.step.gravel":x=Step(.32,v,4);break;case "move.step.snow":x=Step(.36,v,5);break;
                case "move.land":x=Land(v);break;
                case "pve.zombie.step.shuffle":x=Shuffle(v);break;case "pve.zombie.step.run":x=Slap(v);break;case "pve.zombie.step.heavy":x=Stomp(v);break;case "pve.zombie.step.crawl":x=Crawl(v);break;
                case "pve.zombie.moan":x=Throat(1.9+.25*v,new[]{104,112,90,76},.3,.28,.22,.08,500,880,2350,600,1050,2450,5.5,.03,v);break;
                case "pve.zombie.groan":x=Throat(.8+.1*v,new[]{96,100,84,70},.42,.35,.05,.18,640,1080,2500,620,1000,2400,0,.04,v,28);break;
                case "pve.zombie.brute.grunt":x=Throat(.75+.08*v,new[]{66,70,60,54},.3,.55,.02,.18,420,780,2200,400,760,2150,0,.05,v,18);break;
                case "pve.zombie.hurt":x=Throat(.34+.04*v,new[]{165,150,120,104},.45,.25,.008,.12,620,1150,2550,560,1000,2450,0,.06,v);break;
                case "pve.zombie.attack":x=Attack(v);break;case "pve.zombie.shriek":x=Shriek(v);break;case "pve.zombie.death":x=Death(v);break;
                case "pve.zombie.runner.snarl":x=Snarl(v);break;case "pve.zombie.spitter.gurgle":x=Gurgle(v);break;case "pve.zombie.spitter.spit":x=Spit(v);break;
                default:return null;
            }
            return Finish(x,Voice(key)?VoiceLevel:key=="move.land"?LandLevel:StepLevel);
        }

        // ---------- voices ----------
        // A throat: `pitch` four points over the call (Hz), `breath` the noise share, `growl` the subharmonic, attack/release (s),
        // two formant sets (F1-F3, Hz) it moves between, a tremolo (Hz), the pitch jitter, and a rattle (amplitude Hz, 0 none).
        private static double[] Throat(double seconds,int[] pitch,double breath,double growl,double attack,double release,double f1a,double f2a,double f3a,double f1b,double f2b,double f3b,double tremolo,double jitter,double v,double rattle=0)
        {
            int n=(int)(seconds*Rate);var o=new double[n];double phase=0,sub=0,drift=0,scale=1+.06*v;
            var r1=new Res();var r2=new Res();var r3=new Res();double hp=0,lastIn=0;
            for(int i=0;i<n;i++)
            {
                double t=i/(double)Rate,u=t/seconds;
                drift+=(Noise()*jitter-drift)*.002;double f0=Curve(pitch,u)*scale*(1+drift*4);
                phase+=f0/Rate;phase-=Math.Floor(phase);sub+=f0*.5/Rate;sub-=Math.Floor(sub);
                double saw=2*phase-1,sn=Math.Sin(Math.PI*phase),s2=sn*sn,pulse=s2*s2*s2;
                double src=(saw*.55+pulse*.9)*(1-breath)+Noise()*breath*(.35+.65*pulse);
                src*=1+growl*Math.Sin(2*Math.PI*sub);if(rattle>0)src*=.55+.45*Math.Sin(2*Math.PI*rattle*t+Noise()*.3);
                double y=r1.Run(src,Lerp(f1a,f1b,u),5)*1.0+r2.Run(src,Lerp(f2a,f2b,u),7)*.55+r3.Run(src,Lerp(f3a,f3b,u),9)*.25;
                // a gentle high-pass (no sub rumble)
                hp=.995*(hp+y-lastIn);lastIn=y;
                double env=Env(t,attack,seconds,release)*(tremolo>0?.85+.15*Math.Sin(2*Math.PI*tremolo*t):1);
                o[i]=Math.Tanh(hp*1.6)*env;
            }
            return o;
        }
        private static double[] Attack(double v)
        {
            var growl=Throat(.5+.05*v,new[]{140,128,110,96},.5,.45,.01,.16,760,1250,2600,700,1150,2500,0,.07,v,0);
            for(int i=0;i<growl.Length;i++){double t=i/(double)Rate;growl[i]+=.5*Noise()*Math.Exp(-t*22)*Math.Min(1,t/.004);}
            return growl;
        }
        private static double[] Shriek(double v)
        {
            double seconds=1.05+.1*v;var o=Throat(seconds,new[]{300,520,470,380},.55,.12,.03,.3,820,1800,2850,760,1650,2700,0,.05,v);
            for(int i=0;i<o.Length;i++)o[i]=Math.Tanh(o[i]*3)*.8;
            return o;
        }
        private static double[] Death(double v)
        {
            double seconds=1.35+.12*v;var o=Throat(seconds,new[]{112,96,70,52},.4,.35,.02,.45,620,1000,2400,520,880,2300,0,.06,v,20);
            Bubbles(o,0,.6*seconds,.035,240,620,.55);
            for(int i=(int)(.65*seconds*Rate);i<o.Length;i++){double t=i/(double)Rate-.65*seconds;o[i]+=.25*Noise()*Math.Exp(-t*5)*Math.Min(1,t/.05);}
            return o;
        }
        private static double[] Snarl(double v)
        {
            double seconds=.8+.08*v;var o=Throat(seconds,new[]{128,140,124,110},.62,.6,.02,.18,700,1300,2600,680,1200,2500,0,.08,v,0);
            var hp=new Res();
            for(int i=0;i<o.Length;i++){double t=i/(double)Rate,pant=Math.Max(0,Math.Sin(2*Math.PI*(6.5+.6*v)*t));o[i]=o[i]*(.45+.55*pant)+.35*hp.Run(Noise(),2600,1.2)*pant*pant;}
            return o;
        }
        private static double[] Gurgle(double v)
        {
            double seconds=.9+.08*v;var o=Throat(seconds,new[]{72,76,70,66},.5,.4,.08,.2,380,820,2200,420,900,2300,0,.05,v,0);
            for(int i=0;i<o.Length;i++)o[i]*=.35;
            Bubbles(o,0,seconds-.08,.022,260,760,1.0);
            return o;
        }
        private static double[] Spit(double v)
        {
            double seconds=.48+.04*v;int n=(int)(seconds*Rate);var o=new double[n];var bp=new Res();var low=new Res();double hork=.13+.02*v;
            for(int i=0;i<n;i++)
            {
                double t=i/(double)Rate;
                if(t<hork)o[i]+=bp.Run(Noise(),1100+2400*t/hork,3)*Env(t,.02,hork,.03)*.9;
                double s=t-hork;if(s>=0){o[i]+=Noise()*Math.Exp(-s*30)*.8+low.Run(Noise(),520,2)*Math.Exp(-s*9)*.6;if(U()<.004*Math.Exp(-s*6))o[i]+=(U()-.5)*1.4;}
            }
            Bubbles(o,hork,seconds-.05,.012,500,1300,.4);
            return o;
        }
        // Wet bubbles: short rising sine chirps (each 15-50 ms, exponential decay) between `from` and `to` s, `gap` s apart on average.
        private static void Bubbles(double[] o,double from,double to,double gap,double lo,double hi,double level)
        {
            double t=from+U()*gap;
            while(t<to)
            {
                double f=lo+(hi-lo)*U(),len=.015+.035*U(),amp=level*(.4+.6*U());int start=(int)(t*Rate),count=(int)(len*Rate);double ph=0;
                for(int k=0;k<count&&start+k<o.Length;k++){double s=k/(double)Rate;ph+=f*(1+1.6*s/len)/Rate;o[start+k]+=amp*Math.Sin(2*Math.PI*ph)*Math.Exp(-s/len*4)*Math.Min(1,s/.002);}
                t+=gap*(.4+1.2*U());
            }
        }

        // ---------- steps ----------
        // A boot: a heel hit and a toe hit (`surface` 0 concrete, 1 stone, 2 metal, 3 wood, 4 gravel, 5 snow).
        private static double[] Step(double seconds,double v,int surface)
        {
            int n=(int)(seconds*Rate);var o=new double[n];double toe=.026+.006*v,tone=1+.05*v;
            double tau=surface==5?.05:surface==3?.016:.011,cut=surface==5?1600:surface==3?2200:surface==2?4200:3000,thump=(surface==3?115:surface==2?100:88)*tone;
            // (a boot's click carries on small speakers: the thump at half the hit's weight)
            Hit(o,0,1,tau,cut,thump,.028,surface==5?.02:.002,.45);Hit(o,toe,.5,tau*.8,cut*1.2,thump*1.15,.02,surface==5?.015:.0015,.45);
            if(surface==2)Modes(o,0,new[]{540,890,1340,2110,3380},new[]{.16,.14,.12,.11,.09},.18,tone);
            if(surface==3)Modes(o,0,new[]{186,338,612},new[]{.05,.045,.04},.3,tone);
            if(surface==1)Grains(o,.002,.06,9,2500,6000,.28);
            if(surface==4)Grains(o,.0,.16+.03*v,60,1800,6500,.45);
            if(surface==5)Grains(o,.005,.2,26,900,3000,.3);
            return o;
        }
        private static double[] Land(double v)
        {
            double seconds=.38;int n=(int)(seconds*Rate);var o=new double[n];
            Hit(o,0,1.3,.016,2400,72*(1+.04*v),.05,.002);Hit(o,.022+.004*v,.9,.014,2600,80,.04,.002);
            var bp=new Res();for(int i=0;i<n;i++){double t=i/(double)Rate;o[i]+=.22*bp.Run(Noise(),2200,1.1)*Env(t,.01,seconds,.25);}
            return o;
        }
        private static double[] Shuffle(double v)
        {
            double seconds=.42+.03*v;int n=(int)(seconds*Rate);var o=new double[n];var bp=new Res();var bp2=new Res();
            Hit(o,0,.6,.014,2000,85,.03,.004);
            for(int i=0;i<n;i++){double t=i/(double)Rate,drag=Env(t,.04,seconds,.18)*(.7+.3*Math.Sin(2*Math.PI*(9+v)*t));o[i]+=(.5*bp.Run(Noise(),1200+300*v,1.4)+.25*bp2.Run(Noise(),3200,2))*drag;}
            Grains(o,.03,seconds-.08,14,2000,5000,.18);
            return o;
        }
        private static double[] Slap(double v)
        {
            double seconds=.22;int n=(int)(seconds*Rate);var o=new double[n];var hp=new Res();
            Hit(o,0,1,.008,4500,105*(1+.05*v),.02,.001);
            for(int i=0;i<n;i++){double t=i/(double)Rate;o[i]+=.5*hp.Run(Noise(),3000,1.5)*Math.Exp(-t*90);}
            return o;
        }
        private static double[] Stomp(double v)
        {
            double seconds=.62;int n=(int)(seconds*Rate);var o=new double[n];
            Hit(o,0,1.4,.03,900,52*(1+.06*v),.09,.004);
            for(int i=0;i<n;i++){double t=i/(double)Rate;o[i]+=.6*Math.Sin(2*Math.PI*(40+20*Math.Exp(-t*12))*t)*Math.Exp(-t*7)*Math.Min(1,t/.004);}
            Grains(o,.07,.4,22,1200,4500,.22);
            return o;
        }
        private static double[] Crawl(double v)
        {
            double seconds=.55+.04*v;int n=(int)(seconds*Rate);var o=new double[n];var lp=new Res();
            Hit(o,0,.9,.006,3800,140,.015,.001);
            for(int i=0;i<n;i++){double t=i/(double)Rate;o[i]+=.55*lp.Run(Noise(),900+200*v,.9)*Env(t-.04,.08,seconds-.04,.2);}
            return o;
        }
        // One hit: a noise burst (decay `tau`, low-passed at `cut`) and a falling thump (`f` Hz, decay `thumpTau`), from `at` s.
        private static void Hit(double[] o,double at,double gain,double tau,double cut,double f,double thumpTau,double attack,double thumpGain=.9)
        {
            var lp=new Res();int start=(int)(at*Rate);double ph=0;
            for(int i=start;i<o.Length;i++)
            {
                double t=(i-start)/(double)Rate,a=Math.Min(1,t/Math.Max(.0005,attack));if(t>.4)break;
                ph+=f*(1+.6*Math.Exp(-t*40))/Rate;
                o[i]+=gain*a*(.8*lp.Run(Noise(),cut,.7)*Math.Exp(-t/tau)+thumpGain*Math.Sin(2*Math.PI*ph)*Math.Exp(-t/thumpTau));
            }
        }
        private static void Modes(double[] o,double at,int[] hz,double[] amp,double tau,double tone)
        {
            int start=(int)(at*Rate);
            for(int m=0;m<hz.Length;m++){double f=hz[m]*tone*(1+.01*Noise()),d=tau*(1-.1*m);for(int i=start;i<o.Length;i++){double t=(i-start)/(double)Rate;o[i]+=amp[m]*Math.Sin(2*Math.PI*f*t)*Math.Exp(-t/d)*Math.Min(1,t/.0008);}}
        }
        // `count` grains (1-3 ms noise bursts, band-passed between lo and hi) between `from` and `to` s, denser early.
        private static void Grains(double[] o,double from,double to,int count,double lo,double hi,double level)
        {
            for(int g=0;g<count;g++)
            {
                double t=from+(to-from)*Math.Pow(U(),1.6),len=.001+.002*U(),f=lo+(hi-lo)*U(),amp=level*(.3+.7*U());var bp=new Res();int start=(int)(t*Rate),c=(int)(len*Rate)+1;
                for(int k=0;k<c*3&&start+k<o.Length;k++){double s=k/(double)Rate;o[start+k]+=amp*bp.Run(Noise(),f,2.5)*Math.Exp(-s/len);}
            }
        }

        // ---------- shared ----------
        private static double Curve(int[] p,double u){u=Math.Max(0,Math.Min(1,u))*(p.Length-1);int i=Math.Min(p.Length-2,(int)u);double f=u-i,s=f*f*(3-2*f);return p[i]+(p[i+1]-p[i])*s;}
        private static double Lerp(double a,double b,double u)=>a+(b-a)*Math.Max(0,Math.Min(1,u));
        private static double Env(double t,double attack,double length,double release)=>t<0?0:Math.Max(0,Math.Min(1,Math.Min(t/Math.Max(1e-4,attack),(length-t)/Math.Max(1e-4,release))));
        /// <summary>A state-variable band-pass (resonator) at `f` Hz with quality `q`, one sample at a time.</summary>
        private sealed class Res
        {
            private double low,band;
            // (Chamberlin's filter is stable for k < sqrt(damp^2+4) - damp; the coefficient keeps 10% under that bound)
            public double Run(double x,double f,double q){double damp=1/Math.Max(.5,q),k=Math.Min(2*Math.Sin(Math.PI*Math.Min(f,Rate*.24)/Rate),.9*(Math.Sqrt(damp*damp+4)-damp));low+=k*band;double high=x-low-damp*band;band+=k*high;return band;}
        }
        /// <summary>The loudest momentary loudness of `x` (LUFS at <see cref="Rate"/>), as the bank's tools measure a clip: ITU-R
        /// BS.1770 K-weighting (its pre-filter shelf and RLB high-pass, for this rate), 400 ms windows, a mono clip read as dual mono.</summary>
        public static double MomentaryMax(float[] x){var d=new double[x.Length];for(int i=0;i<x.Length;i++)d[i]=x[i];return MomentaryMax(d);}
        private static double MomentaryMax(double[] x)
        {
            double fs=Rate,k=Math.Tan(Math.PI*1681.974450955533/fs),vh=Math.Pow(10,3.999843853973347/20),vb=Math.Pow(vh,.4996667741545416),q=.7071752369554196,a0=1+k/q+k*k;
            double b0=(vh+vb*k/q+k*k)/a0,b1=2*(k*k-vh)/a0,b2=(vh-vb*k/q+k*k)/a0,a1=2*(k*k-1)/a0,a2=(1-k/q+k*k)/a0;
            double k2=Math.Tan(Math.PI*38.13547087602444/fs),q2=.5003270373238773,c0=1+k2/q2+k2*k2,c1=2*(k2*k2-1)/c0,c2=(1-k2/q2+k2*k2)/c0;
            int w=(int)(.4*Rate);double x1=0,x2=0,y1=0,y2=0,u1=0,u2=0,v1=0,v2=0,sum=0,best=0;var ring=new double[w];
            for(int i=0;i<x.Length+w;i++)
            {
                double x0=i<x.Length?x[i]:0,y=b0*x0+b1*x1+b2*x2-a1*y1-a2*y2;x2=x1;x1=x0;y2=y1;y1=y;
                double z=y-2*u1+u2-c1*v1-c2*v2;u2=u1;u1=y;v2=v1;v1=z;
                sum+=z*z-ring[i%w];ring[i%w]=z*z;if(sum/w>best)best=sum/w;
            }
            return -.691+10*Math.Log10(2*best+1e-20);
        }
        // To the family level (the bank's measure), a soft limit under full scale, faded ends.
        private static float[] Finish(double[] x,float levelLufs)
        {
            int n=x.Length;double gain=Math.Pow(10,(levelLufs-MomentaryMax(x))/20);var o=new float[n];int fade=Math.Min(n/4,(int)(.012*Rate));
            for(int i=0;i<n;i++){double y=x[i]*gain;y=Math.Abs(y)>.7?Math.Sign(y)*(.7+.28*Math.Tanh((Math.Abs(y)-.7)/.28)):y;double e=i>=n-fade?(n-1-i)/(double)fade:1;if(i<16)e*=i/16.0;o[i]=(float)(y*e);}
            return o;
        }
    }
}
