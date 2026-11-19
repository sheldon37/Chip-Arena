using System;
using System.Globalization;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The frame-time rows of the expedition overhaul's budget table (docs/EXPEDITION-DEPLOYMENT.md, W0): a window of frame times
    /// in milliseconds as its mean, p50, p95, p99 and worst, and the frames over 20 and 50 ms, the way BoZo's xperf windows report
    /// them (the q-th of the sorted times is the one at floor(n x q)), so the Editor's 1-pilot rows and the 4-pilot rows from
    /// Astra's DEV load harness (Runtime NativeExpeditionLoadFrameTimes) read alike. Presentation measurement only; engine-free.
    /// </summary>
    public static class NativeFrameTimes
    {
        public struct Summary {public int Frames;public double Mean,P50,P95,P99,Worst;public int Over20,Over50;}

        /// <summary>The first `count` of `ms` summarised (allocates its one sorted copy).</summary>
        public static Summary Of(float[] ms,int count)
        {
            var s=new Summary();count=Math.Max(0,Math.Min(count,ms?.Length??0));s.Frames=count;if(count==0)return s;
            var sorted=new float[count];Array.Copy(ms,sorted,count);Array.Sort(sorted);double sum=0;
            for(int i=0;i<count;i++){sum+=sorted[i];if(sorted[i]>20)s.Over20++;if(sorted[i]>50)s.Over50++;}
            double At(double q)=>sorted[Math.Min(count-1,(int)Math.Floor(count*q))];
            s.Mean=sum/count;s.P50=At(.5);s.P95=At(.95);s.P99=At(.99);s.Worst=sorted[count-1];return s;
        }

        /// <summary>One log line: "LABEL: N frames; ms mean m p50 a p95 b p99 c worst w; over 20 ms x, over 50 ms y".</summary>
        public static string Line(string label,Summary s)
        {
            string F(double v)=>v.ToString("0.00",CultureInfo.InvariantCulture);
            return label+": "+s.Frames+" frames; ms mean "+F(s.Mean)+" p50 "+F(s.P50)+" p95 "+F(s.P95)+" p99 "+F(s.P99)+" worst "+F(s.Worst)+"; over 20 ms "+s.Over20+", over 50 ms "+s.Over50;
        }
    }
}
