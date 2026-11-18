using System;

namespace ChipArena.UnityConversion
{
    // The drop pods' sound (docs/EXPEDITION-OVERHAUL.md W4, W12): original placeholders synthesized here like the Deployment
    // Station's (NativeDeploySounds) and the REVIVE-O-MATIC's, no asset, until the owner's ElevenLabs batch replaces them (N5): the
    // roar of a pod burning in, the cloud punch's boom (it reaches each listener late by its distance), the retros' burn or their
    // sputter, the impact, the explosive bolts, the door slamming down as a ramp, the steam, the LED sign's blip, the harness
    // unlatching and the hull ticking as it cools. Each pod's cues come from its plan (NativePodDrop.Plan), so every peer hears
    // the same crash at the same moment; Runtime/NativePodAudio.cs plays them as the presentation clock passes them.
    public static class NativePodSounds
    {
        public const int SampleRate=48000;
        public const int Roar=0,Boom=1,Retro=2,Sputter=3,Impact=4,Bolts=5,Door=6,Steam=7,Tick=8,Blip=9,Harness=10,Count=11;
        public static readonly string[] Keys={"pod.roar","pod.boom","pod.retro","pod.sputter","pod.impact","pod.bolts","pod.door","pod.steam","pod.tick","pod.blip","pod.harness"};
        /// <summary>One cue: when (seconds from GO), which sound, its gain at the source and the distance it carries to, its rate,
        /// and whether it sounds from where the pod is in its flight at that moment (else from its rest centre).</summary>
        public struct Cue
        {
            public float At,Gain,Range,Rate;public int Sound;public bool Sky;
            public Cue(float at,int sound,float gain,float range,float rate=1,bool sky=false){At=at;Sound=sound;Gain=gain;Range=range;Rate=rate;Sky=sky;}
        }
        /// <summary>The most cues one pod plans.</summary>
        public const int MaxCues=40;
        /// <summary>Pod `p`'s cues into `o` (at least MaxCues long), in time order; the count.</summary>
        public static int Plan(in PodPlan p,Cue[] o)
        {
            int n=0;
            o[n++]=new Cue(p.Appear+.05f,Roar,.8f,420,.96f+.08f*H(p.Seed,1),true);
            o[n++]=new Cue(p.Punch,Boom,1,650,.94f+.12f*H(p.Seed,2),true);
            o[n++]=new Cue(p.RetroAt,p.Fails?Sputter:Retro,.75f,220,.95f+.1f*H(p.Seed,3),true);
            o[n++]=new Cue(p.Impact,Impact,1,180,(p.Fails?.9f:1)+.06f*H(p.Seed,4));
            o[n++]=new Cue(p.Impact+.1f,Steam,.4f,32);
            o[n++]=new Cue(p.Impact+.25f,Blip,.3f,24);
            o[n++]=new Cue(p.DoorAt-NativePodDrop.BoltLead,Bolts,.75f,70);
            o[n++]=new Cue(p.DoorAt+p.DoorSeconds,Door,.8f,55,.95f+.1f*H(p.Seed,5));
            o[n++]=new Cue(p.StandFrom,Harness,.4f,18);
            // the hull ticking as it cools: quick at first, slower and softer until it is black
            float t=p.Impact+1.6f;
            for(int k=0;n<MaxCues&&t<p.Impact+NativePodDrop.CoolSeconds;k++)
            {
                float u=(t-p.Impact)/NativePodDrop.CoolSeconds;o[n++]=new Cue(t,Tick,.32f*(1-u)+.05f,16,.85f+.35f*H(p.Seed,40+k));
                t+=.45f+1.3f*u+.5f*H(p.Seed,80+k);
            }
            // in time order (an insertion sort: the list is short and nearly sorted, and nothing allocates)
            for(int i=1;i<n;i++){var c=o[i];int j=i-1;while(j>=0&&o[j].At>c.At){o[j+1]=o[j];j--;}o[j+1]=c;}
            return n;
        }
        public static double Duration(int i)
        {
            switch(i){case Roar:return 2.9;case Boom:return 2.2;case Retro:return .7;case Sputter:return .75;case Impact:return 2.6;case Bolts:return .5;case Door:return 1.0;case Steam:return 1.8;case Tick:return .14;case Blip:return .2;case Harness:return .4;}
            return .5;
        }
        /// <summary>A sound's samples, mono at SampleRate. Deterministic.</summary>
        public static float[] Samples(int index)
        {
            if(index<0||index>=Count)throw new ArgumentOutOfRangeException(nameof(index));
            double duration=Duration(index);var data=new float[(int)Math.Ceiling(duration*SampleRate)];uint seed=(uint)(9137+index*7919);
            double phase=0,phase2=0,low=0,low2=0,band=0;
            for(int n=0;n<data.Length;n++)
            {
                double t=n/(double)SampleRate,u=t/duration;seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;double noise=seed/(double)uint.MaxValue*2-1,v=0;
                switch(index)
                {
                    case Roar:
                    {
                        // a tearing roar: low noise opening up as it comes in (brighter, louder), crackling, then falling away
                        double swell=Math.Sin(Math.PI*Math.Min(1,u*1.15))*(u<.08?u/.08:1),bright=.03+.22*swell;low+=bright*(noise-low);low2+=.01*(low-low2);
                        double crackle=Math.Pow(Math.Max(0,noise),18)*(Math.Sin(2*Math.PI*7*t)>.2?1:0);
                        v=swell*(1.8*low+2.4*low2)+.35*crackle*swell;break;
                    }
                    case Boom:
                    {
                        // the cloud punch: a double crack (an N-wave's two edges) and a long rolling boom under it
                        double crack=(t<.012?1:0)-(t>.11&&t<.122?1:0);low+=.02*(noise-low);
                        phase+=2*Math.PI*(48-18*Math.Min(1,t/1.2))/SampleRate;double roll=Math.Exp(-t*2.2)*(Math.Sin(phase)*.7+low*3.5)*Math.Min(1,t/.02);
                        v=.9*crack+.8*roll+.25*noise*Math.Exp(-t*14);break;
                    }
                    case Retro:
                    {
                        // the retros: a hard hissing burn with a rising whine
                        low+=.35*(noise-low);band+=.08*(low-band);phase+=2*Math.PI*(380+520*u)/SampleRate;
                        v=(Math.Sin(Math.PI*Math.Min(1,u*1.05))*(.9*(low-band)+.5*band)+.12*Math.Sin(phase))*Math.Min(1,t/.03);break;
                    }
                    case Sputter:
                    {
                        // a failing retro: coughs and pops at random, sparks crackling between
                        double gate=Math.Sin(2*Math.PI*(9*t+3*t*t))>.35?1:.08;low+=.25*(noise-low);
                        v=gate*(.8*low+.25*noise)*(1-u)+.3*Math.Pow(Math.Max(0,noise),12);break;
                    }
                    case Impact:
                    {
                        // the slam: a sub thud sweeping down, a crunch of torn metal and ground, debris rattling out
                        phase+=2*Math.PI*(62-34*Math.Min(1,t/.35))/SampleRate;double thud=Math.Exp(-t*4.5)*Math.Sin(phase)*1.2;
                        low+=.18*(noise-low);double crunch=Math.Exp(-t*9)*(low*2.2+.5*noise);
                        double ring=Math.Exp(-t*6)*(.25*Math.Sin(2*Math.PI*233*t)+.18*Math.Sin(2*Math.PI*397*t)+.12*Math.Sin(2*Math.PI*611*t));
                        double rattle=Math.Exp(-t*1.8)*.18*Math.Pow(Math.Max(0,noise),6)*(t>.15?1:0);v=thud+crunch+ring+rattle;break;
                    }
                    case Bolts:
                    {
                        // four sharp cracks, 30 ms apart
                        for(int k=0;k<4;k++){double a=t-k*.032-.004*(k%2);if(a>=0&&a<.08)v+=Math.Exp(-a*90)*(noise*.9+.4*Math.Sin(2*Math.PI*1800*a));}
                        low+=.05*(noise-low);v+=.3*low*Math.Exp(-t*8);break;
                    }
                    case Door:
                    {
                        // the door falling open and slamming down: a heavy clang over a thud, a small bounce
                        double hit=t-.02,bounce=t-.2;phase+=2*Math.PI*70/SampleRate;
                        if(hit>=0)v+=Math.Exp(-hit*7)*(.5*Math.Sin(2*Math.PI*213*hit)+.32*Math.Sin(2*Math.PI*377*hit)+.2*Math.Sin(2*Math.PI*594*hit)+.12*Math.Sin(2*Math.PI*1031*hit))+Math.Exp(-hit*18)*(.8*Math.Sin(phase)+.4*noise);
                        if(bounce>=0)v+=.35*Math.Exp(-bounce*12)*(Math.Sin(2*Math.PI*240*bounce)+.3*noise);break;
                    }
                    case Steam:
                    {
                        // a vent of steam: a bright hiss rising fast and dying away
                        low+=.6*(noise-low);band+=.25*(noise-band);double env=Math.Min(1,t/.05)*Math.Exp(-t*1.6);v=env*(.8*(noise-band)+.2*low);break;
                    }
                    case Tick:
                    {
                        // hot metal ticking as it cools: a small, dry tink
                        v=Math.Exp(-t*60)*(.5*Math.Sin(2*Math.PI*2870*t)+.35*Math.Sin(2*Math.PI*4130*t)+.2*Math.Sin(2*Math.PI*1460*t))+.2*noise*Math.Exp(-t*300);break;
                    }
                    case Blip:
                    {
                        // the LED sign powering on: two quick tones
                        double f=t<.08?1240:1860;phase+=2*Math.PI*f/SampleRate;v=.4*Math.Sign(Math.Sin(phase))*(t<.075||t>.09?1:0)*Math.Exp(-t*9);break;
                    }
                    case Harness:
                    {
                        // the harness unlatching: a mechanical clack, a hiss of air as the bar lifts
                        double clack=Math.Exp(-t*70)*(noise*.7+.5*Math.Sin(2*Math.PI*820*t));low+=.4*(noise-low);phase2=Math.Min(1,Math.Max(0,(t-.04)/.05));
                        v=clack+.35*phase2*Math.Exp(-(t-.04)*7)*(noise-low);break;
                    }
                }
                double envelope=Math.Min(1,t/.002)*Math.Min(1,(data.Length-1-n)/(SampleRate*.02));
                data[n]=(float)(Math.Tanh(v)*envelope);
            }
            return data;
        }
        private static float H(int a,int b){unchecked{uint x=(uint)a*0x9E3779B1u+(uint)b*0x85EBCA77u+0x6C8E9CF5u;x^=x>>15;x*=0x2C1B3C6Du;x^=x>>12;x*=0x297A2D39u;x^=x>>15;return (x>>8)*(1f/16777216f);}}
    }
}
