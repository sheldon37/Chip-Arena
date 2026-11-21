using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Belvedere Camp's placeholder sounds (docs/audio/SOUND-REQUESTS.md "The camp's sound bed"): original, synthesized, deterministic,
    /// mono at <see cref="Rate"/>. They play until the owner's listening pass and the ElevenLabs batch (plan N5; generation is paid, so
    /// nothing is requested from a service without the owner's word). Loops end where they begin: their noise is cross-faded over the
    /// seam, and every tone is a whole number of cycles of the loop.
    /// <list type="bullet">
    /// <item><c>camp.fire</c>: a drum fire's low roar with crackles and pops (3 s loop);</item>
    /// <item><c>camp.generator</c>: a diesel generator's hum behind the fence, with its slow load wobble (2 s loop);</item>
    /// <item><c>camp.radio</c>: the camp PA on 64.7 FM, a lo-fi tune of its own under band-limited static (8 s loop);</item>
    /// <item><c>camp.murmur</c>: people talking low round the camp, no words (8 s loop);</item>
    /// <item><c>camp.lamp.click</c>: the war board's desk lamp switch (one shot).</item>
    /// </list>
    /// </summary>
    public static class NativeCampSynth
    {
        public const int Rate=22050;
        public static readonly string[] Keys={"camp.fire","camp.generator","camp.radio","camp.murmur","camp.lamp.click"};
        public static bool Loop(string key)=>key!="camp.lamp.click";
        private static uint seed;
        private static double Noise(){unchecked{seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;return seed/4294967296.0*2-1;}}
        private static double Unit(){unchecked{seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;return seed/4294967296.0;}}
        /// <summary>The samples of `key` (null for a key the camp does not own).</summary>
        public static float[] Samples(string key)
        {
            seed=0xC4A9u;if(key!=null)foreach(char c in key)seed=unchecked(seed*31+c);if(seed==0)seed=1;
            switch(key)
            {
                case "camp.fire":return Fire(3.0);
                case "camp.generator":return Generator(2.0);
                case "camp.radio":return Radio(8.0);
                case "camp.murmur":return Murmur(8.0);
                case "camp.lamp.click":return Click();
            }
            return null;
        }
        // A one-pole low pass and a two-pole band pass (the RBJ cookbook's constant-peak form).
        private sealed class Band
        {
            private double b0,b2,a1,a2,x1,x2,y1,y2;
            public Band(double f,double q){Set(f,q);}
            public void Set(double f,double q){double w=2*Math.PI*f/Rate,al=Math.Sin(w)/(2*q),a0=1+al;b0=al/a0;b2=-al/a0;a1=-2*Math.Cos(w)/a0;a2=(1-al)/a0;}
            public double Step(double x){double y=b0*x+b2*x2-a1*y1-a2*y2;x2=x1;x1=x;y2=y1;y1=y;return y;}
        }
        // Renders `seconds` plus a fade, then lays the fade over the start so the loop's seam is seamless.
        private static float[] Seam(double seconds,Func<int,double> f,double fade=.08)
        {
            int n=(int)Math.Round(seconds*Rate),m=(int)Math.Round(fade*Rate);var raw=new double[n+m];for(int i=0;i<raw.Length;i++)raw[i]=f(i);
            var o=new float[n];for(int i=0;i<n;i++){double v=raw[i];if(i<m){double k=i/(double)m;v=raw[i]*k+raw[n+i]*(1-k);}o[i]=(float)Math.Max(-1,Math.Min(1,v));}
            return o;
        }
        private static float[] Fire(double seconds)
        {
            double lp=0,lp2=0;var band=new Band(900,.7);double crack=0,crackDecay=0,crackTone=0,crackPhase=0;
            return Seam(seconds,i=>
            {
                double t=i/(double)Rate,w=Noise();lp+=(w-lp)*.035;lp2+=(lp-lp2)*.05;
                // the roar: low, breathing slowly
                double roar=lp2*3.2*(.8+.2*Math.Sin(2*Math.PI*.7*t));
                // crackles: short bright bursts, now and then a louder pop
                if(Unit()<7.0/Rate){crack=.35+.5*Unit();crackDecay=Math.Exp(-1/(Rate*(.004+.01*Unit())));crackTone=1800+2600*Unit();if(Unit()<.12){crack=.9;crackDecay=Math.Exp(-1/(Rate*.02));crackTone=500+400*Unit();}}
                crackPhase+=2*Math.PI*crackTone/Rate;double c=crack*(band.Step(Noise())*1.6+.35*Math.Sin(crackPhase));crack*=crackDecay;
                return .55*(roar+c);
            });
        }
        private static float[] Generator(double seconds)
        {
            double lp=0;
            return Seam(seconds,i=>
            {
                double t=i/(double)Rate,load=1+.06*Math.Sin(2*Math.PI*.5*t);lp+=(Noise()-lp)*.02;
                // a 30 Hz firing note and its harmonics (whole cycles of the 2 s loop), a little mechanical rattle
                double hum=.42*Math.Sin(2*Math.PI*30*t)+.3*Math.Sin(2*Math.PI*60*t+.6)+.16*Math.Sin(2*Math.PI*90*t+1.1)+.1*Math.Sin(2*Math.PI*120*t+2.2)+.05*Math.Sin(2*Math.PI*240*t);
                double rattle=.08*lp*(1+Math.Sin(2*Math.PI*15*t));
                return .42*load*(hum+rattle);
            },.05);
        }
        private static float[] Radio(double seconds)
        {
            // an original eight-bar tune in a pentatonic scale, on a soft square lead and a triangle bass, through a small speaker
            int[] lead={0,2,4,7,4,2,0,-1, 4,7,9,7,4,2,4,-1, 0,2,4,7,9,7,4,2, 4,2,0,-3,0,-1,-1,-1};
            int[] bass={-12,-12,-5,-5,-8,-8,-10,-10};
            var speaker=new Band(1400,.8);var hiss=new Band(3200,1.1);double ph=0,pb=0;
            return Seam(seconds,i=>
            {
                double t=i/(double)Rate,beat=t*4;int step=(int)beat%lead.Length;double u=beat-Math.Floor(beat);
                int note=lead[step];double env=note<0?0:Math.Min(1,u*40)*Math.Exp(-u*2.2);double f=note<0?0:261.6*Math.Pow(2,note/12.0);
                ph+=2*Math.PI*f/Rate;double sq=Math.Sign(Math.Sin(ph))*.6+Math.Sin(ph)*.4;
                int bar=(int)(t*.5)%bass.Length;double fb=130.8*Math.Pow(2,bass[bar]/12.0);pb+=2*Math.PI*fb/Rate;double tri=2/Math.PI*Math.Asin(Math.Sin(pb));
                double music=speaker.Step(.5*sq*env+.35*tri);
                // the static, swelling and fading as the signal drifts
                double drift=.5+.5*Math.Sin(2*Math.PI*.125*t+1.3);double st=hiss.Step(Noise())*(.12+.22*drift);
                if(Unit()<3.0/Rate)st+=.6*Noise();
                return .5*(music*(1-.4*drift)+st);
            });
        }
        private static float[] Murmur(double seconds)
        {
            // six voices: noise through two moving formants, in phrases of syllables with pauses between
            const int V=6;var f1=new Band[V];var f2=new Band[V];var rate=new double[V];var phase=new double[V];var pitch=new double[V];var gain=new double[V];
            for(int v=0;v<V;v++){f1[v]=new Band(500,4);f2[v]=new Band(1500,5);rate[v]=3.2+1.6*Unit();phase[v]=Unit();pitch[v]=95+90*Unit();gain[v]=.6+.4*Unit();}
            var vp=new double[V];double lp=0;
            return Seam(seconds,i=>
            {
                double t=i/(double)Rate,sum=0;
                for(int v=0;v<V;v++)
                {
                    // phrases: a voice talks for a while (the loop's whole cycles: 0.25 Hz), syllables at about 3-5 Hz
                    double phrase=.5+.5*Math.Sin(2*Math.PI*.25*t+phase[v]*6.28);if(phrase<.35){continue;}
                    double syl=Math.Max(0,Math.Sin(2*Math.PI*rate[v]*t+phase[v]*9));syl*=syl;
                    if(i%64==0){f1[v].Set(420+260*Math.Sin(t*2.3+v),4);f2[v].Set(1250+500*Math.Sin(t*1.7+v*2),5);}
                    vp[v]+=2*Math.PI*pitch[v]*(1+.04*Math.Sin(t*3+v))/Rate;double src=.5*Math.Sin(vp[v])+.5*Noise();
                    sum+=gain[v]*syl*(phrase-.35)*(f1[v].Step(src)*1.4+f2[v].Step(src));
                }
                lp+=(sum-lp)*.35;return .9*lp;
            });
        }
        private static float[] Click()
        {
            int n=(int)(.12*Rate);var o=new float[n];var ring=new Band(2100,6);
            for(int i=0;i<n;i++){double t=i/(double)Rate,tick=i<44?Noise()*(1-i/44.0):0;o[i]=(float)Math.Max(-1,Math.Min(1,.9*tick+ring.Step(tick)*2.2*Math.Exp(-t*60)));}
            return o;
        }
    }
}
