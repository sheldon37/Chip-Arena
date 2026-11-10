using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The music's pulse for Substation 128's lights (docs/arenas/CLUB.md "Music reactivity"): the kick, the bass, the mids,
    /// the hats and the overall level, each 0..1, from the playing music's spectrum (Runtime NativeClubAudio samples the music
    /// voice's AudioSource.GetSpectrumData each frame and calls <see cref="Feed"/>), or, with no song playing (none installed,
    /// muted, the music volume at 0, a silent stretch), from a steady 128 bpm clock on the bar grid (<see cref="Clock"/>), so the
    /// club always dances. Presentation only: no hazard, telegraph, damage or timing ever reads it (they keep the match clock),
    /// so two clients that hear the music differently still fight on the same floor.
    /// <para>The light-flash setting (Settings · Effects · Alarm lights, Reduced effects: NativeKitFlash) shapes the kick:
    /// Full a crisp pulse on each kick (128 bpm is 2.1 a second, under the 3 a second the hazard kit allows), Reduced a soft
    /// pulse at most twice a second, Calm no pulse (a slow 0.3 Hz swell).</para>
    /// Allocation free; deterministic for the same inputs.
    /// </summary>
    public sealed class NativeClubPulse
    {
        /// <summary>The spectrum Runtime reads (bins over 0..rate/2) and the shares the bands take.</summary>
        public const int Bins=512;
        public const double KickLo=40,KickHi=115,BassHi=250,MidHi=2500,HighHi=10000;
        /// <summary>How quiet the music must stay (its bands' summed magnitude) and for how long before the clock takes over (s).</summary>
        public const double Silent=1e-6,SilentFor=.6,Handover=.8;
        public float Kick,Bass,Mid,High,Level;
        /// <summary>Whether the music drives it now (1) or the clock (0), eased between them over <see cref="Handover"/> s.</summary>
        public float Live;
        /// <summary>Kicks heard since the last reset (the show may step on them while the music drives it).</summary>
        public int Kicks;
        private double kickAvg,bassPeak,midPeak,highPeak,levelPeak,lastKick,sinceKick=10,quiet=10,live;
        private float clockKick,clockBass,clockMid,clockHigh,clockLevel;
        public void Reset(){Kick=Bass=Mid=High=Level=Live=0;Kicks=0;kickAvg=bassPeak=midPeak=highPeak=levelPeak=lastKick=0;sinceKick=quiet=10;live=0;}

        /// <summary>The steady 128 bpm clock at `beats` on the bar grid (NativeClubLooks.Beats): a kick on every beat, the bass
        /// swelling over each bar, the hats on the off-beats, a steady level. Fills the outputs; Feed blends it with the music.</summary>
        public void Clock(double beats,NativeKitFlash flash)
        {
            double b=beats-Math.Floor(beats),bar=NativeClubLooks.Frac(beats/4),off=NativeClubLooks.Frac(beats+.5);
            clockKick=(float)Shape(b,flash,beats);clockBass=(float)(.55+.45*Math.Exp(-b*3.2)*(bar<.25?1:.75));
            clockMid=(float)(.45+.25*Math.Sin(beats*Math.PI/8));clockHigh=(float)(flash==NativeKitFlash.Calm?.4:.25+.6*Math.Exp(-off*9));clockLevel=.72f;
            Kick=clockKick;Bass=clockBass;Mid=clockMid;High=clockHigh;Level=clockLevel;Live=0;
        }
        // A kick's envelope `b` beats (0..1) after it: Full crisp, Reduced soft and every other beat (under 2 a second), Calm a swell.
        private static double Shape(double b,NativeKitFlash flash,double beats)
        {
            if(flash==NativeKitFlash.Calm)return .3+.15*Math.Sin(beats*NativeClubLooks.Beat*2*Math.PI*.3);
            if(flash==NativeKitFlash.Reduced){double t=NativeClubLooks.Frac(beats/2);return .25+.55*Math.Sin(Math.PI*Math.Min(1,t*1.6))*Math.Exp(-t*1.2);}
            return Math.Exp(-b*7.5);
        }

        /// <summary>
        /// One frame: `spectrum` the music voice's magnitudes (`n` bins over 0..`rate`/2; null or n 0 when no music plays),
        /// `dt` the frame's seconds, `beats` the bar grid's beats for the clock it blends with, `flash` the light-flash setting.
        /// </summary>
        public void Feed(float[] spectrum,int n,double rate,double dt,double beats,NativeKitFlash flash)
        {
            Clock(beats,flash);dt=Math.Max(0,Math.Min(.1,dt));
            double kick=0,bass=0,mid=0,high=0;
            if(spectrum!=null&&n>8&&rate>1000)
            {
                double w=rate/2/n;
                for(int i=1;i<n&&i<spectrum.Length;i++)
                {
                    double f=i*w,m=spectrum[i];if(!(m>0)||double.IsInfinity(m))continue;
                    if(f<KickHi&&f>=KickLo)kick+=m;if(f<BassHi)bass+=m;else if(f<MidHi)mid+=m;else if(f<HighHi)high+=m;else break;
                }
            }
            double all=bass+mid+high;quiet=all<Silent?quiet+dt:0;
            double target=quiet<SilentFor?1:0;live+=(target-live)*Math.Min(1,dt/Handover);if(live<1e-3&&target==0)live=0;
            // adaptive scales: each band against its own slowly falling peak, so a quiet master or a loud song both fill the range
            double fall=Math.Exp(-dt/5);bassPeak=Math.Max(bass,Math.Max(bassPeak*fall,1e-6));midPeak=Math.Max(mid,Math.Max(midPeak*fall,1e-6));highPeak=Math.Max(high,Math.Max(highPeak*fall,1e-6));levelPeak=Math.Max(all,Math.Max(levelPeak*fall,1e-6));
            // the kick: an onset over its own running average, rising, at least .22 s after the last (Reduced: .5 s)
            sinceKick+=dt;double refractory=flash==NativeKitFlash.Full?.22:.5;
            bool onset=all>=Silent&&kick>kickAvg*1.45&&kick>lastKick&&sinceKick>=refractory;kickAvg+=(kick-kickAvg)*Math.Min(1,dt/.35);lastKick=kick;
            if(onset){sinceKick=0;Kicks++;}
            // the kick's envelope as the clock shapes it: Full crisp, Reduced a soft rise and a slow fall, Calm the clock's swell
            double heard;
            if(flash==NativeKitFlash.Calm)heard=clockKick;
            else if(flash==NativeKitFlash.Reduced)heard=sinceKick<.18?.25+.55*Math.Sin(Math.PI*.5*sinceKick/.18):.25+.55*Math.Exp(-(sinceKick-.18)*2.2);
            else heard=Math.Exp(-sinceKick*7.5);
            double l=live;Live=(float)l;
            Kick=(float)Mix(clockKick,heard,l);Bass=(float)Mix(clockBass,Math.Sqrt(bass/bassPeak),l);Mid=(float)Mix(clockMid,Math.Sqrt(mid/midPeak),l);High=(float)Mix(clockHigh,Math.Sqrt(high/highPeak),l);Level=(float)Mix(clockLevel,Math.Sqrt(all/levelPeak),l);
        }
        private static double Mix(double a,double b,double t)=>a+(NativeClubLooks.Clamp01(b)-a)*t;
    }
}
