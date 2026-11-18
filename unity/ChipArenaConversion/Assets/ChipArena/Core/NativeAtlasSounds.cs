using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// ATLAS's sounds (docs/EXPEDITION-OVERHAUL.md W2): Claude's presentation table, so Astra's NativeDeploySounds stays as it is
    /// (it plays machines 0 and 1; the board is machine 2). Original, synthesized, deterministic (no global random state):
    /// <list type="bullet">
    /// <item><b>The clatter:</b> one click per flap that lands as line 1 arrives, at the moment it lands (built from the board's
    /// own schedule, NativeAtlasBoard.Clicks, so the sound lands with the picture): a light impact, a short aluminium ring and a
    /// little thump, each a touch different, over a faint mechanical rattle.</item>
    /// <item><b>The bell:</b> a two-note chime as the line lands (E5 then C5, bell partials, a soft strike).</item>
    /// </list>
    /// Placeholders until the owner's ElevenLabs pass (docs/EXPEDITION-OVERHAUL.md N5): the keys stay the same.
    /// </summary>
    public static class NativeAtlasSounds
    {
        public const int SampleRate=48000,Count=2,Clatter=0,Bell=1;
        public static readonly string[] Keys={"deploy.atlas.clatter","deploy.atlas.bell"};
        public struct Sound{public int Index;public string Key;public float Gain;}
        /// <summary>The sound a moment plays (the clatter starts as the flaps do, the bell rings as they land).</summary>
        public static Sound Of(NativeAtlasBoard.Cue cue)
        {
            int i=cue.Kind==NativeAtlasBoard.CueKind.Clatter?Clatter:cue.Kind==NativeAtlasBoard.CueKind.Bell?Bell:-1;
            return i<0?new Sound{Index=-1}:new Sound{Index=i,Key=Keys[i],Gain=i==Clatter?.62f:.55f};
        }
        private static List<float> clicks;
        private static List<float> Clicks()=>clicks??=NativeAtlasBoard.Clicks(NativeAtlasBoard.Board,0);
        /// <summary>A clip's length (s): the clatter runs to its last click and rings out; the bell's two strikes ring 2.4 s.</summary>
        public static double Duration(int index)
        {
            if(index==Clatter){var c=Clicks();return (c.Count>0?c[c.Count-1]:0)+.14;}
            return 2.4;
        }
        // A hash in [0,1) (the board's mixer), for each click's small differences.
        private static double H(int a,int b)=>NativeAtlasBoard.H(a,b,77);
        /// <summary>The clip's samples, mono at SampleRate, click-free at both ends.</summary>
        public static float[] Samples(int index)
        {
            if(index<0||index>=Count)throw new ArgumentOutOfRangeException(nameof(index));
            double duration=Duration(index);var data=new double[(int)Math.Ceiling(duration*SampleRate)];uint seed=(uint)(40961+index*7919);
            double Noise(){seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;return seed/(double)uint.MaxValue*2-1;}
            if(index==Clatter)
            {
                var c=Clicks();int span=(int)(.045*SampleRate);
                for(int k=0;k<c.Count;k++)
                {
                    int n0=(int)Math.Round(c[k]*SampleRate);double amp=.55+.45*H(k,1),pitch=.88+.24*H(k,2),ring=3150*pitch,body=1700*pitch,thud=190+40*H(k,3);
                    for(int n=0;n<span&&n0+n<data.Length;n++)
                    {
                        double t=n/(double)SampleRate,noise=Noise();
                        data[n0+n]+=amp*(.9*noise*Math.Exp(-t/.0011)+.42*Math.Sin(2*Math.PI*ring*t)*Math.Exp(-t/.0055)+.3*Math.Sin(2*Math.PI*body*t)*Math.Exp(-t/.0095)+.34*Math.Sin(2*Math.PI*thud*t)*Math.Exp(-t/.012));
                    }
                }
                // the drums' faint rattle under the clicks, while they run
                double end=c.Count>0?c[c.Count-1]:0,lp=0;
                for(int n=0;n<data.Length;n++){double t=n/(double)SampleRate;double env=Math.Min(1,t/.08)*Math.Max(0,Math.Min(1,(end-t)/.12));lp+=.18*(Noise()-lp);data[n]+=.05*env*lp*(1+.4*Math.Sin(2*Math.PI*31*t));}
                for(int n=0;n<data.Length;n++)data[n]*=.38;
            }
            else
            {
                // two strikes of one bell: E5 then C5, each with its partials ringing out and a soft strike
                var strikes=new[]{(at:0.0,f:659.25),(at:.36,f:523.25)};var partials=new[]{(r:1.0,a:1.0,tau:1.15),(r:2.0,a:.36,tau:.55),(r:2.93,a:.2,tau:.32),(r:4.05,a:.11,tau:.19),(r:.5,a:.16,tau:1.5)};
                foreach(var s in strikes)
                {
                    int n0=(int)Math.Round(s.at*SampleRate);
                    for(int n=n0;n<data.Length;n++)
                    {
                        double t=(n-n0)/(double)SampleRate,v=0;foreach(var p in partials)v+=p.a*Math.Sin(2*Math.PI*s.f*p.r*t+p.r)*Math.Exp(-t/p.tau);
                        data[n]+=.3*Math.Min(1,t/.003)*v+.12*Noise()*Math.Exp(-t/.0018);
                    }
                }
            }
            var o=new float[data.Length];
            for(int n=0;n<o.Length;n++)
            {
                double t=n/(double)SampleRate,envelope=Math.Min(1,t/.002)*Math.Min(1,(o.Length-1-n)/(SampleRate*.015));
                o[n]=(float)(Math.Tanh(data[n])*envelope);
            }
            return o;
        }
    }
}
