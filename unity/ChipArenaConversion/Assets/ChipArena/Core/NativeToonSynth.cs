using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // The Top Dog's toon sounds (docs/TOP-DOG.md "Sound"): original, synthesized from nothing (no clip, no LocalAssets), so the
    // class is never silent. Each is a short cartoon cue: the mallet's bonk, the spring's boing, the slide whistle, the anvil's
    // fall and clang, splats, the gulp, the burp, the pop. NativeAudio asks the effects bank for "topdog.<id>" first (no such clip
    // is registered yet), and plays these otherwise. Deterministic: the same id always makes the
    // same samples. Mono, Rate Hz, peaks normalised to Peak, the ends faded so nothing clicks. Presentation only.
    public static class NativeToonSynth
    {
        public const int Rate=48000;public const float Peak=.82f;
        public const string Prefix="topdog.";
        /// <summary>Every sound, by id.</summary>
        public static readonly string[] Ids={"bonk","slam","swing","boing","zip","windup","slide-up","slide-down","whistle-fall","clang","slip","splat","squirt","sproing","punch","pie","gulp","burp","inflate","pop","bell","honk","tweet","sizzle","fanfare","drumroll","crash","poof","squeak","wobble","hiccup","whirl","stretch","chomp","piano","ring-cut","ring-slice","ring-throw","ring-ping","ring-hum","ring-burst","ring-catch","ring-flick"};
        public static bool Known(string id)=>Array.IndexOf(Ids,id)>=0;
        /// <summary>The sound's samples (a new array each call; the runtime keeps one clip per id).</summary>
        public static float[] Samples(string id){lock(Gate)return Make(id);}
        private static readonly object Gate=new object();
        private static float[] Make(string id)
        {
            var b=new Buf(Length(id));
            switch(id)
            {
                case "bonk":Bonk(b,1,0);break;
                case "slam":Bonk(b,.72f,0);Thump(b,0,64,7,1);Rumble(b,0,320,5,.5f);Crash(b,.01f,.25f,4.5f);break;
                case "swing":Whoosh(b,0,.3f,420,2300,620,.9f);break;
                case "boing":Boing(b,0,165,.9f,1);break;
                case "zip":Chirp(b,0,.16f,520,2700,.5f,Wave.Sine,1);Whoosh(b,.02f,.34f,900,6200,2400,.7f);break;
                case "windup":Windup(b);break;
                case "slide-up":Slide(b,0,.56f,420,1500,1);break;
                case "slide-down":Slide(b,0,.56f,1500,380,1);break;
                case "whistle-fall":Slide(b,0,.95f,2100,520,1,swell:true);break;
                case "clang":Clang(b,0,440,1);Thump(b,0,58,8,1);Noise(b,0,.012f,0,9000,1,.7f);break;
                case "slip":Chirp(b,0,.17f,600,2500,.55f,Wave.Sine,1);Thump(b,.24f,88,13,.9f);Splat(b,.24f,.4f,1500);break;
                case "splat":Splat(b,0,1,1800);break;
                case "squirt":Squirt(b);break;
                case "sproing":Chirp(b,0,.1f,200,1800,.6f,Wave.Sine,1);Boing(b,.07f,700,.5f,.6f);break;
                case "punch":Thump(b,0,92,17,1);Noise(b,0,.06f,40,2200,40,.8f);Tone(b,0,.25f,410,30,.45f,Wave.Tri);break;
                case "pie":Splat(b,0,1,1100);Splat(b,.06f,.5f,700);Noise(b,.03f,.3f,300,1600,9,.25f);break;
                case "gulp":Gulp(b,0,210,95,.14f,1);Gulp(b,.2f,165,80,.12f,.6f);break;
                case "burp":Burp(b);break;
                case "inflate":Inflate(b);break;
                case "pop":Noise(b,0,.03f,400,16000,70,1);Thump(b,0,70,12,.9f);Tone(b,0,.04f,2100,120,.5f,Wave.Sine);Rumble(b,0,260,7,.45f);break;
                case "bell":for(int i=0;i<3;i++)Bell(b,i*.16f,1318.5f,1-.15f*i);break;
                case "honk":Honk(b,0);Honk(b,.3f);break;
                case "tweet":for(int i=0;i<4;i++)Chirp(b,i*.13f,.06f,2800+120*(i%2),3700,.35f,Wave.Sine,1);break;
                case "sizzle":Sizzle(b);break;
                case "fanfare":Fanfare(b);break;
                case "drumroll":Drumroll(b);break;
                case "crash":Crash(b,0,1,2.4f);break;
                case "poof":Noise(b,0,.5f,0,900,9,.9f);Chirp(b,0,.2f,140,60,.6f,Wave.Sine,1);break;
                case "squeak":Chirp(b,0,.12f,1700,2350,.45f,Wave.Sine,1,vibrato:.04f,vibratoHz:38);break;
                case "wobble":Wobble(b);break;
                case "hiccup":Noise(b,0,.008f,1000,8000,1,.5f);Chirp(b,.004f,.05f,940,600,.6f,Wave.Tri,1);break;
                case "whirl":Whirl(b);break;
                case "stretch":Chirp(b,0,.5f,180,520,.35f,Wave.Saw,1,vibrato:.05f,vibratoHz:11,cutoff:1400);break;
                case "piano":Piano(b);break;
                // The Glaive's (docs/GLAIVE.md "Sound"): a thin blade's swish, a slice, a spun throw, a ricochet's ping, the halo's hum, the burst,
                // the catch and the flick.
                case "ring-cut":Whoosh(b,0,.24f,900,3800,1500,.85f);Tone(b,.02f,.2f,2350,14,.08f,Wave.Sine);break;
                case "ring-slice":Noise(b,0,.05f,2500,11000,50,.8f);Clang(b,0,1650,.35f);Thump(b,0,110,22,.45f);break;
                case "ring-throw":Whoosh(b,0,.36f,700,4200,1800,.8f);Chirp(b,0,.3f,600,1400,.18f,Wave.Tri,1,vibrato:.08f,vibratoHz:28,cutoff:3000);break;
                case "ring-ping":Clang(b,0,2100,.7f);Tone(b,0,.4f,3150,9,.25f,Wave.Sine);Noise(b,0,.02f,3000,12000,80,.5f);break;
                case "ring-hum":Chirp(b,0,.8f,420,470,.4f,Wave.Saw,1,vibrato:.1f,vibratoHz:24,cutoff:1600);Whoosh(b,0,.8f,500,1500,700,.3f);break;
                case "ring-burst":Thump(b,0,70,6,1);Crash(b,0,.8f,3.5f);Noise(b,0,.1f,800,9000,18,.8f);Clang(b,0,1900,.4f);Rumble(b,0,260,4,.5f);break;
                case "ring-catch":Clang(b,0,900,.45f);Thump(b,0,180,30,.5f);Tone(b,0,.2f,1800,20,.12f,Wave.Sine);break;
                case "ring-flick":Whoosh(b,0,.3f,1100,5200,2000,.75f);Tone(b,.04f,.3f,2600,8,.1f,Wave.Sine);break;
                case "chomp":Noise(b,0,.02f,600,5000,1,.5f);Thump(b,.005f,150,30,.6f);Gulp(b,.06f,260,150,.07f,.4f);break;
                default:return Array.Empty<float>();
            }
            return b.Finish();
        }
        /// <summary>The sound's length (s).</summary>
        public static float Length(string id)
        {
            switch(id)
            {
                case "bonk":return .5f;case "slam":return 1.2f;case "swing":return .34f;case "boing":return .95f;case "zip":return .45f;case "windup":return .42f;
                case "slide-up":case "slide-down":return .62f;case "whistle-fall":return 1;case "clang":return 1.9f;case "slip":return .75f;case "splat":return .6f;
                case "squirt":return 1.6f;case "sproing":return .65f;case "punch":return .42f;case "pie":return .7f;case "gulp":return .46f;case "burp":return .72f;
                case "inflate":return 1.35f;case "pop":return .7f;case "bell":return 1.6f;case "honk":return .66f;case "tweet":return .62f;case "sizzle":return 2.2f;
                case "fanfare":return 1.15f;case "drumroll":return 1.3f;case "crash":return 2.2f;case "poof":return .6f;case "squeak":return .14f;case "wobble":return .85f;
                case "hiccup":return .26f;case "whirl":return .7f;case "stretch":return .55f;case "chomp":return .2f;case "piano":return 2.4f;case "ring-cut":return .28f;case "ring-slice":return .4f;case "ring-throw":return .42f;case "ring-ping":return .55f;case "ring-hum":return .85f;case "ring-burst":return 1.6f;case "ring-catch":return .32f;case "ring-flick":return .36f;default:return .1f;
            }
        }

        // ---- the toolkit ----
        private enum Wave{Sine,Tri,Saw,Square}
        private sealed class Buf
        {
            public readonly float[] D;public Buf(float seconds){D=new float[Math.Max(16,(int)(seconds*Rate))];}
            public int N=>D.Length;
            public void Add(int i,double v){if(i>=0&&i<D.Length)D[i]+=(float)v;}
            public float[] Finish()
            {
                float peak=0;foreach(var v in D)peak=Math.Max(peak,Math.Abs(v));
                float g=peak>1e-6f?Peak/peak:0;int fade=(int)(Rate*.003f);
                for(int i=0;i<D.Length;i++){float e=1;if(i<fade)e=i/(float)fade;int left=D.Length-1-i;if(left<fade)e=Math.Min(e,left/(float)fade);D[i]=D[i]*g*e;}
                return D;
            }
        }
        private static uint seed=0x9E3779B9u;
        private static void Seed(uint s){seed=s*2654435761u+0x1234567u;if(seed==0)seed=1;}
        private static float White(){seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;return (seed&0xFFFFFF)/8388608f-1;}
        private static int I(float t)=>(int)(t*Rate);
        // Band-limited waves from a phase (turns): harmonics below 14 kHz only, so a high bend never aliases.
        private static double Osc(Wave w,double phase,double freq)
        {
            double p=phase-Math.Floor(phase),x=2*Math.PI*p;
            switch(w)
            {
                case Wave.Sine:return Math.Sin(x);
                case Wave.Tri:{double s=0;for(int k=1;k<=9;k+=2){if(k*freq>14000)break;s+=(((k-1)/2)%2==0?1:-1)*Math.Sin(k*x)/(k*k);}return s*.81;}
                case Wave.Saw:{double s=0;for(int k=1;k<=40;k++){if(k*freq>14000)break;s+=Math.Sin(k*x)/k;}return s*.6;}
                default:{double s=0;for(int k=1;k<=39;k+=2){if(k*freq>14000)break;s+=Math.Sin(k*x)/k;}return s*.9;}
            }
        }
        private static double Smooth(double u){u=u<0?0:u>1?1:u;return u*u*(3-2*u);}
        // A Chamberlin state-variable filter, run twice a sample (stable up to high cutoffs): band (mode 1), low (0) or high (2).
        private struct Svf
        {
            private double low,band;
            public double Step(double x,double cutoff,double q,int mode)
            {
                double f=Math.Min(.85,2*Math.Sin(Math.PI*Math.Max(10,Math.Min(cutoff,Rate*.3))/(2*Rate))),damp=Math.Min(1.9,1/Math.Max(.5,q)),o=0;
                for(int i=0;i<2;i++){low+=f*band;double high=x-low-damp*band;band+=f*high;o=mode==0?low:mode==1?band:high;}
                return o;
            }
        }
        // A tone of fixed pitch with an attack and an exponential decay (per second).
        private static void Tone(Buf b,float at,float len,float freq,float decay,float amp,Wave w)
        {
            int s=I(at),n=I(len);double ph=0;
            for(int i=0;i<n;i++){double t=i/(double)Rate,a=Math.Min(1,t/.0015)*Math.Exp(-decay*t);ph+=freq/Rate;b.Add(s+i,amp*a*Osc(w,ph,freq));}
        }
        // A glide from f0 to f1 (exponential in pitch) over `len`, with an optional vibrato and a low-pass on rich waves.
        private static void Chirp(Buf b,float at,float len,float f0,float f1,float amp,Wave w,float shape,float vibrato=0,float vibratoHz=0,float cutoff=0)
        {
            int s=I(at),n=I(len);double ph=0;var svf=new Svf();
            for(int i=0;i<n;i++)
            {
                double u=i/(double)n,t=i/(double)Rate,f=f0*Math.Pow(f1/f0,Math.Pow(u,shape));f*=1+vibrato*Math.Sin(2*Math.PI*vibratoHz*t);ph+=f/Rate;
                double env=Math.Min(1,t/.004)*Math.Min(1,(n-i)/(Rate*.02));double v=Osc(w,ph,f);if(cutoff>0)v=svf.Step(v,cutoff,.8,0);
                b.Add(s+i,amp*env*v);
            }
        }
        // A low sine thump that drops a little in pitch as it dies.
        private static void Thump(Buf b,float at,float freq,float decay,float amp)
        {
            int s=I(at),n=Math.Min(b.N-s,I(4/decay+.05f));double ph=0;
            for(int i=0;i<n;i++){double t=i/(double)Rate,f=freq*(1+.6*Math.Exp(-40*t));ph+=f/Rate;b.Add(s+i,amp*Math.Min(1,t/.001)*Math.Exp(-decay*t)*Math.Sin(2*Math.PI*ph));}
        }
        // Filtered noise: a burst of `len` (0: until it dies) through a band from lo to hi, decaying per second.
        private static void Noise(Buf b,float at,float len,float lo,float hi,float decay,float amp)
        {
            Seed((uint)(at*1000+lo+hi));int s=I(at),n=len>0?I(len):Math.Min(b.N-s,I(5/Math.Max(1,decay)));var a=new Svf();var c=new Svf();
            for(int i=0;i<n;i++){double t=i/(double)Rate,x=White();if(hi>0)x=a.Step(x,hi,.7,0);if(lo>0)x=c.Step(x,lo,.7,2);double env=Math.Min(1,t/.0008)*Math.Exp(-decay*t)*(len>0?Math.Min(1,(n-i)/(Rate*.004)):1);b.Add(s+i,amp*env*x);}
        }
        // A deep, low-passed rumble.
        private static void Rumble(Buf b,float at,float cutoff,float decay,float amp)
        {
            Seed(77);int s=I(at),n=b.N-s;var f=new Svf();var g=new Svf();
            for(int i=0;i<n;i++){double t=i/(double)Rate;double x=g.Step(f.Step(White(),cutoff,.6,0),cutoff,.6,0)*3;b.Add(s+i,amp*Math.Min(1,t/.01)*Math.Exp(-decay*t)*x);}
        }
        // Noise swept through a band-pass: from f0 up to fMid at the middle and on to f1, a bell in level.
        private static void Whoosh(Buf b,float at,float len,float f0,float fMid,float f1,float amp)
        {
            Seed(31);int s=I(at),n=I(len);var f=new Svf();
            for(int i=0;i<n;i++){double u=i/(double)n,c=u<.5?f0+(fMid-f0)*Smooth(u*2):fMid+(f1-fMid)*Smooth(u*2-1);double env=Math.Sin(Math.PI*Math.Pow(u,.8));b.Add(s+i,amp*env*f.Step(White(),c,2.2,1)*2.2);}
        }
        // The cartoon wood-block bonk: a hollow knock with a quick downward "boink" and a body thump.
        private static void Bonk(Buf b,float scale,float at)
        {
            Tone(b,at,.4f,620*scale,18,.75f,Wave.Sine);Tone(b,at,.3f,1490*scale,32,.38f,Wave.Sine);Tone(b,at,.2f,2660*scale,48,.18f,Wave.Sine);
            Chirp(b,at,.12f,900*scale,290*scale,.55f,Wave.Tri,.6f);Thump(b,at,112*scale,20,.62f);Noise(b,at,.004f,2000,7000,1,.5f);
        }
        // A jaw-harp spring: a low tone whose pitch wobbles and dies as it rises a little.
        private static void Boing(Buf b,float at,float f0,float len,float amp)
        {
            int s=I(at),n=I(len);double ph=0,ph2=0;
            for(int i=0;i<n;i++)
            {
                double t=i/(double)Rate,f=f0*(1+.55*Math.Sin(2*Math.PI*11*t)*Math.Exp(-3*t))*(1+.9*t);ph+=f/Rate;ph2+=2400*(1+.01*Math.Sin(2*Math.PI*30*t))/Rate;
                double a=Math.Min(1,t/.002)*Math.Exp(-2.6*t);b.Add(s+i,amp*a*(Math.Sin(2*Math.PI*ph)+.3*Math.Sin(4*Math.PI*ph)+.12*Math.Sin(6*Math.PI*ph))*.75+amp*.06*Math.Exp(-9*t)*Math.Sin(2*Math.PI*ph2));
            }
        }
        // A slide whistle: a breathy sine that glides between two pitches (smoothly), with a gentle vibrato.
        private static void Slide(Buf b,float at,float len,float f0,float f1,float amp,bool swell=false)
        {
            Seed(5);int s=I(at),n=I(len);double ph=0;var f=new Svf();
            for(int i=0;i<n;i++)
            {
                double u=i/(double)n,t=i/(double)Rate,fr=f0*Math.Pow(f1/f0,Smooth(u))*(1+.014*Math.Sin(2*Math.PI*6*t));ph+=fr/Rate;
                double env=Math.Min(1,t/.02)*Math.Min(1,(n-i)/(Rate*.06))*(swell?.45+.55*u:1);
                b.Add(s+i,amp*env*(.8*Math.Sin(2*Math.PI*ph)+.08*Math.Sin(4*Math.PI*ph)+.12*f.Step(White(),fr,6,1)));
            }
        }
        // An anvil: inharmonic partials ringing out over a heavy hit.
        private static void Clang(Buf b,float at,float f0,float amp)
        {
            float[] r={1,2.32f,4.25f,6.63f,9.38f},g={1,.6f,.45f,.3f,.2f},d={2.6f,4,5.5f,7.5f,10};
            for(int k=0;k<r.Length;k++)Tone(b,at,1.9f-at,f0*r[k],d[k],amp*g[k]*.5f,Wave.Sine);
        }
        // A wet splat: low-passed noise whose cutoff falls, a squelch, and droplets after.
        private static void Splat(Buf b,float at,float amp,float cutoff)
        {
            Seed((uint)(cutoff+at*100));int s=I(at),n=Math.Min(b.N-s,I(.32f));var f=new Svf();
            for(int i=0;i<n;i++){double u=i/(double)n,t=i/(double)Rate;b.Add(s+i,amp*1.4*Math.Min(1,t/.001)*Math.Exp(-12*t)*f.Step(White(),cutoff*(1-.75*u)+80,1.1,0));}
            Chirp(b,at,.12f,260,70,.5f*amp,Wave.Sine,.7f);
            for(int k=0;k<5;k++){float off=.05f+.055f*k+.02f*(k%2);Tone(b,at+off,.05f,900+170*((k*7)%5),60,.12f*amp,Wave.Sine);}
        }
        private static void Squirt(Buf b)
        {
            Seed(11);var f=new Svf();var g=new Svf();double flutter=0;
            for(int i=0;i<b.N;i++)
            {
                double t=i/(double)Rate;if(i%(Rate/18)==0)flutter=.45+.55*(White()*.5+.5);
                double env=Math.Min(1,t/.05)*Math.Min(1,(b.N-i)/(Rate*.15))*flutter;
                b.Add(i,env*(f.Step(White(),1200,2.5,1)*1.6+.4*g.Step(White(),260,1.5,0)*Math.Sin(2*Math.PI*7*t)));
            }
        }
        private static void Gulp(Buf b,float at,float f0,float f1,float len,float amp)
        {
            Noise(b,at,.006f,800,6000,1,.25f*amp);int s=I(at),n=I(len);double ph=0;var f=new Svf();Seed(23);
            for(int i=0;i<n;i++){double u=i/(double)n,fr=f0*Math.Pow(f1/f0,u);ph+=fr/Rate;double env=Math.Sin(Math.PI*u);b.Add(s+i,amp*env*(Math.Sin(2*Math.PI*ph)+.25*f.Step(White(),520,3,1)));}
        }
        // A long cartoon burp: a jittery glottal buzz through two formants.
        private static void Burp(Buf b)
        {
            Seed(42);var f1=new Svf();var f2=new Svf();double ph=0,jitter=0;
            for(int i=0;i<b.N;i++)
            {
                double u=i/(double)b.N,t=i/(double)Rate;if(i%240==0)jitter=jitter*.7+White()*.03;double fr=96*(1-.18*u)*(1+jitter);ph+=fr/Rate;
                double p=ph-Math.Floor(ph),pulse=p<.12?Math.Sin(Math.PI*p/.12):0;double env=Math.Min(1,t/.03)*Math.Min(1,(b.N-i)/(Rate*.12))*(.8+.2*Math.Sin(2*Math.PI*5*t));
                b.Add(i,env*(f1.Step(pulse,520,4,1)*1.2+f2.Step(pulse,1150,5,1)*.7));
            }
        }
        // A balloon stretching: a rubbery squeal that climbs, trembling.
        private static void Inflate(Buf b)
        {
            Seed(9);double ph=0,jitter=0;var f=new Svf();
            for(int i=0;i<b.N;i++)
            {
                double u=i/(double)b.N,t=i/(double)Rate;if(i%300==0)jitter=jitter*.6+White()*.02;double fr=220*Math.Pow(820/220.0,u)*(1+jitter);ph+=fr/Rate;
                double env=Math.Min(1,t/.08)*Math.Min(1,(b.N-i)/(Rate*.05))*(.7+.3*Math.Sin(2*Math.PI*9*t));
                b.Add(i,env*(.6*Osc(Wave.Tri,ph,fr)+.25*f.Step(Osc(Wave.Saw,ph*.5,fr*.5),1500,.8,0)));
            }
        }
        // A struck dinner bell: a bright triangle with its clapper's click.
        // A grand piano landing: a low cluster struck all at once (the keys under it), the case's thump, the strings ringing out.
        private static void Piano(Buf b)
        {
            float[] notes={55,82.4f,98,116.5f,130.8f,155.6f,185,233.1f};
            for(int i=0;i<notes.Length;i++){float f=notes[i],g=.55f-.04f*i;Tone(b,.004f*i,2.3f,f,1.6f+.25f*i,g,Wave.Tri);Tone(b,.004f*i,1.6f,f*2.003f,2.6f,g*.4f,Wave.Sine);Tone(b,.004f*i,1,f*3.01f,4,g*.18f,Wave.Sine);}
            Thump(b,0,58,6,1);Noise(b,0,.18f,120,2600,14,.75f);Noise(b,.01f,.05f,2000,9000,40,.3f);
        }
        private static void Bell(Buf b,float at,float f0,float amp)
        {
            float[] r={1,2,2.76f,5.4f},g={1,.4f,.3f,.12f},d={2.2f,3,4,7};
            for(int k=0;k<r.Length;k++)Tone(b,at,1.6f-at,f0*r[k],d[k],amp*g[k]*.45f,Wave.Sine);Noise(b,at,.003f,3000,12000,1,.3f*amp);
        }
        private static void Honk(Buf b,float at)
        {
            int s=I(at),n=I(.26f);double ph=0;var f=new Svf();var g=new Svf();
            for(int i=0;i<n;i++){double u=i/(double)n,t=i/(double)Rate,fr=360+30*Smooth(u*3);ph+=fr/Rate;double v=Osc(Wave.Saw,ph,fr);double env=Math.Min(1,t/.012)*Math.Min(1,(n-i)/(Rate*.03));b.Add(s+i,env*(f.Step(v,900,1.6,1)+.6*g.Step(v,2400,2,1)));}
        }
        private static void Sizzle(Buf b)
        {
            Seed(3);var f=new Svf();
            for(int i=0;i<b.N;i++){double t=i/(double)Rate,env=Math.Min(1,t/.2)*Math.Min(1,(b.N-i)/(Rate*.3));double x=f.Step(White(),3200,.7,2)*.35;if(White()>.9985f)x+=White()*.9;b.Add(i,env*x);}
        }
        // "Ta-da!": a short brass pick-up and a held major chord, its filter opening like a bell of a horn.
        private static void Fanfare(Buf b)
        {
            void Brass(float at,float len,float fr,float amp)
            {
                int s=I(at),n=I(len);double ph=0;var f=new Svf();
                for(int i=0;i<n;i++){double u=i/(double)n,t=i/(double)Rate,fv=fr*(1+(t>.15?.008*Math.Sin(2*Math.PI*5.5*t):0));ph+=fv/Rate;double cut=600+2800*Smooth(t/.08)*(1-.4*u);double env=Math.Min(1,t/.015)*Math.Min(1,(n-i)/(Rate*.08));b.Add(s+i,amp*env*f.Step(Osc(Wave.Saw,ph,fv),cut,.9,0));}
            }
            Brass(0,.13f,392,.7f);Brass(.15f,.95f,523.25f,.6f);Brass(.15f,.95f,659.25f,.45f);Brass(.15f,.95f,783.99f,.4f);Crash(b,.15f,.25f,3.2f);
        }
        private static void Drumroll(Buf b)
        {
            for(float t=0;t<1.26f;t+=1/22f){float u=t/1.26f;Noise(b,t,.04f,1800,6500,60,.35f+.6f*u);Tone(b,t,.04f,190,60,.2f+.3f*u,Wave.Tri);}
        }
        private static void Crash(Buf b,float at,float amp,float decay)
        {
            Seed(17);int s=I(at),n=b.N-s;var f=new Svf();var g=new Svf();
            for(int i=0;i<n;i++){double t=i/(double)Rate;double x=f.Step(White(),5200,.7,2)+.5*g.Step(White(),9000,3,1);b.Add(s+i,amp*.6*Math.Min(1,t/.002)*Math.Exp(-decay*t)*x);}
        }
        private static void Wobble(Buf b)
        {
            double ph=0;for(int i=0;i<b.N;i++){double t=i/(double)Rate,fr=118*(1+.12*Math.Sin(2*Math.PI*7*t));ph+=fr/Rate;double env=Math.Min(1,t/.03)*Math.Min(1,(b.N-i)/(Rate*.1))*(.55+.45*Math.Sin(2*Math.PI*7*t));b.Add(i,env*(Math.Sin(2*Math.PI*ph)+.3*Math.Sin(4*Math.PI*ph)));}
        }
        // Running in place before a dash: a motor of rising taps and a climbing whine.
        private static void Windup(Buf b)
        {
            float t=0;int k=0;while(t<.4f){float u=t/.4f;Noise(b,t,.012f,300,3000,90,.4f+.3f*u);t+=1/(12+30*u);k++;}
            Chirp(b,0,.4f,160,900,.3f,Wave.Tri,1.4f);
        }
        // A lasso of links whirled overhead: three whooshes, each a little higher.
        private static void Whirl(Buf b){for(int i=0;i<3;i++)Whoosh(b,i*.22f,.24f,300+80*i,1300+250*i,500,.7f);}
    }
}
