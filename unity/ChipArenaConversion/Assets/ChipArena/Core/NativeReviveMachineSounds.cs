using System;

namespace ChipArena.UnityConversion
{
    // The REVIVE-O-MATIC's sound (docs/REVIVE-MACHINE.md): original, synthesized here like the Deployment Station's (NativeDeploySounds), no asset. Cartoon on purpose: money clattering up a tube, a motor winding past
    // what it can stand, a gulp, then PTOOEY, a slide whistle for the flight, a boing off the stack, a splat on the mat, the door's clang and a little ding for the next patient.
    public static class NativeReviveMachineSounds
    {
        public const int SampleRate=48000;
        public const int Coins=0,Windup=1,Gulp=2,Ptooey=3,Whistle=4,Boing=5,Clang=6,Honk=7,Splat=8,Ding=9,Hiss=10,Count=11;
        public struct Cue{public float At,Gain;public int Sound;public Cue(float at,int sound,float gain){At=at;Sound=sound;Gain=gain;}}
        /// <summary>The performance's sounds on its timeline (seconds from the money), in time order.</summary>
        public static readonly Cue[] Timeline=
        {
            new Cue(.02f,Coins,.75f),new Cue(.08f,Honk,.55f),new Cue(NativeReviveMachine.SpinFrom,Windup,.72f),new Cue(1.1f,Hiss,.4f),
            new Cue(NativeReviveMachine.Gulp-.02f,Honk,.5f),new Cue(NativeReviveMachine.Gulp+.1f,Gulp,.95f),
            new Cue(NativeReviveMachine.Spit-.2f,Honk,.5f),new Cue(NativeReviveMachine.Spit,Ptooey,1),new Cue(NativeReviveMachine.Spit,Clang,.6f),new Cue(NativeReviveMachine.Spit+.02f,Boing,.7f),new Cue(NativeReviveMachine.Spit+.03f,Whistle,.62f),new Cue(NativeReviveMachine.Spit+.05f,Hiss,.7f),
            new Cue(NativeReviveMachine.Touch,Splat,.9f),new Cue(NativeReviveMachine.Touch2,Splat,.55f),new Cue(NativeReviveMachine.Shut,Clang,.8f),new Cue(NativeReviveMachine.Shut+.32f,Ding,.5f),
        };
        public static double Duration(int i)
        {
            switch(i){case Coins:return 1.25;case Windup:return NativeReviveMachine.Gulp-NativeReviveMachine.SpinFrom;case Gulp:return .46;case Ptooey:return .62;case Whistle:return NativeReviveMachine.Flight+.1;case Boing:return .95;case Clang:return .75;case Honk:return .2;case Splat:return .34;case Ding:return .95;default:return 1.5;}
        }
        /// <summary>A sound's samples, mono at SampleRate. Deterministic.</summary>
        public static float[] Samples(int index)
        {
            if(index<0||index>=Count)throw new ArgumentOutOfRangeException(nameof(index));
            double duration=Duration(index);var data=new float[(int)Math.Ceiling(duration*SampleRate)];uint seed=(uint)(4243+index*7919);double phase=0,phase2=0,low=0,band=0;
            for(int n=0;n<data.Length;n++)
            {
                double t=n/(double)SampleRate,u=t/duration;seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;double noise=seed/(double)uint.MaxValue*2-1,value=0;
                switch(index)
                {
                    case Coins:
                    {
                        // Each coin: a bright inharmonic ping, one after another up the tube, over a thin rattle.
                        for(int c=0;c<14;c++){double at=c*.052+.02*(c%3),age=t-at;if(age<0||age>.2)continue;double f=2400+230*((c*7)%5);value+=.11*Math.Exp(-age*34)*(Math.Sin(2*Math.PI*f*age)+.6*Math.Sin(2*Math.PI*f*1.47*age)+.35*Math.Sin(2*Math.PI*f*2.13*age));}
                        value+=.018*noise*Math.Sin(Math.PI*Math.Min(1,t/.9));break;
                    }
                    case Windup:
                    {
                        // A motor climbing past what it can stand: fundamental up, a ratchet that speeds up with it, a rising rattle; the gulp cuts it dead.
                        double f=45+420*u*u;phase+=2*Math.PI*f/SampleRate;phase2+=2*Math.PI*(f*1.5+7)/SampleRate;double teeth=Math.Pow(Math.Max(0,Math.Sin(2*Math.PI*(8*t+18*t*t))),10);
                        value=(.2+.22*u)*(Math.Tanh(2.2*Math.Sin(phase))+.35*Math.Sin(phase2))+.22*(.3+u)*noise*teeth+.05*u*u*noise;value*=Math.Min(1,t/.1)*(u>.985?(1-u)/.015:1);break;
                    }
                    case Gulp:
                    {
                        // Glug: a wobbling low glide, down and up once, wet.
                        double k=t/.16;double f=t<.16?190-110*k:t<.3?80+130*(t-.16)/.14:210-140*(t-.3)/.16;phase+=2*Math.PI*f/SampleRate;double gate=Math.Sin(Math.PI*Math.Min(1,t/.46));value=.62*gate*Math.Tanh(1.8*Math.Sin(phase))*(1+.25*Math.Sin(2*Math.PI*31*t));break;
                    }
                    case Ptooey:
                    {
                        // The P: a plosive thump and a crack of noise. The TOOEY: a formant slide, the vowels of it, over a huge breath of steam.
                        double thump=Math.Exp(-t*22)*Math.Sin(2*Math.PI*(62-34*Math.Min(1,t/.12))*t)*.95;double crack=Math.Exp(-t*90)*noise*.9;
                        double f=320+170*Math.Sin(Math.PI*Math.Min(1,t/.42));phase+=2*Math.PI*f/SampleRate;double voice=Math.Tanh(2*Math.Sin(phase));
                        low+=.09*(noise-low);band+=.26*(voice-band);double vowel=band*Math.Sin(Math.PI*Math.Min(1,t/.42))*.42;
                        value=thump+crack+vowel+.36*Math.Exp(-t*4.5)*(noise-low);break;
                    }
                    case Whistle:
                    {
                        // The slide whistle: up as they leave, a wobble over the top, down as they drop.
                        double k=u;double f=720+980*Math.Sin(Math.PI*Math.Min(1,k*1.05));f*=1+.012*Math.Sin(2*Math.PI*11*t);phase+=2*Math.PI*f/SampleRate;value=.3*(Math.Sin(phase)+.12*Math.Sin(phase*2))*Math.Sin(Math.PI*Math.Min(1,k*1.02))+.015*noise;break;
                    }
                    case Boing:
                    {
                        // A spring: a sagging pitch with a fast wobble that rings down.
                        double f=170+90*Math.Exp(-t*2.5)*Math.Cos(2*Math.PI*11*t)+40*Math.Exp(-t*3);phase+=2*Math.PI*f/SampleRate;phase2+=2*Math.PI*(f*2.76)/SampleRate;value=.55*Math.Exp(-t*3.6)*(Math.Sin(phase)+.32*Math.Sin(phase2));break;
                    }
                    case Clang:
                    {
                        double e=Math.Exp(-t*7);value=.42*e*(Math.Sin(2*Math.PI*186*t)+.7*Math.Sin(2*Math.PI*431*t)+.55*Math.Sin(2*Math.PI*793*t)+.3*Math.Sin(2*Math.PI*1287*t))+.45*Math.Exp(-t*55)*noise+.6*Math.Exp(-t*20)*Math.Sin(2*Math.PI*72*t);break;
                    }
                    case Honk:
                    {
                        // The bulb horn: a nasal squeeze rising a little.
                        double f=430+90*u;phase+=2*Math.PI*f/SampleRate;double saw=2*(phase/(2*Math.PI)-Math.Floor(phase/(2*Math.PI)+.5));band+=.34*(saw-band);value=.7*Math.Sin(Math.PI*u)*(saw-band*.6);break;
                    }
                    case Splat:
                    {
                        // A body on a gym mat: a low whump and a slap.
                        low+=.05*(noise-low);value=.95*Math.Exp(-t*16)*Math.Sin(2*Math.PI*(95-55*Math.Min(1,t/.1))*t)+1.4*Math.Exp(-t*24)*low+.26*Math.Exp(-t*70)*noise;break;
                    }
                    case Ding:
                    {
                        double e=Math.Exp(-t*4.5);value=.3*e*(Math.Sin(2*Math.PI*1568*t)+.55*Math.Sin(2*Math.PI*3136*t)+.28*Math.Sin(2*Math.PI*4186*t));break;
                    }
                    default:
                    {
                        // A jet of steam: bright noise, a quick attack and a slow tail.
                        low+=.5*(noise-low);value=.4*(noise-low)*Math.Min(1,t/.04)*Math.Exp(-t*1.9);break;
                    }
                }
                data[n]=(float)Math.Max(-1,Math.Min(1,value));
            }
            return data;
        }
    }
}
