using System;
namespace ChipArena.UnityConversion
{
    // Original slender-static.ts envelope, independent of the audio/render adapters.
    public static class NativeSlenderRules
    {
        public static float Static(float sanity,bool seen,float hopAge)=>MathUtil.Clamp(sanity*(seen?.95f:.7f)+(seen?.08f:0)+(hopAge<.35f?.45f*(1-Math.Max(0,hopAge)/.35f):0),0,1);
        public static float Tension(float sanity,int hops)=>MathUtil.Clamp(Math.Max(sanity,hops/8f),0,1);
        public static float BeatGap(float tension)=>1.05f-.6f*tension;
        public static float Heartbeat(float time)
        {if(time<0||time>=.2f)return 0;float gain=time<.015f?time/.015f:(float)Math.Pow(.0001,Math.Min(1,(time-.015f)/.145f));return (float)Math.Sin(2*Math.PI*52*time)*gain;}
        public static float[] Drone(int rate=48000)
        {
            // Five seconds is an integral period of all three original oscillators.
            int n=rate*5;var data=new float[n];var filter=new Filter(rate,240,false);
            for(int i=0;i<n*2;i++){double t=(i%n)/(double)rate,a=55*t,b=55.6*t,c=82.4*t;
                double sample=filter.Tick(2*(a-Math.Floor(a+.5))+Math.Sin(2*Math.PI*b)+2/Math.PI*Math.Asin(Math.Sin(2*Math.PI*c)));
                if(i>=n)data[i-n]=(float)sample;}
            return data;
        }
        public static float[] Noise(int rate=48000)
        {var noise=new float[rate];uint seed=9137;for(int i=0;i<rate;i++){seed=unchecked(seed*1664525+1013904223);noise[i]=(float)(seed/4294967296.0*2-1);}var data=new float[rate];var filter=new Filter(rate,2600,true);for(int i=0;i<rate*2;i++){float sample=(float)filter.Tick(noise[i%rate]);if(i>=rate)data[i-rate]=sample;}return data;}
        private sealed class Filter
        {
            private readonly double b0,b1,b2,a1,a2;private double x1,x2,y1,y2;
            public Filter(int rate,double hz,bool band){double w=2*Math.PI*hz/rate,c=Math.Cos(w),alpha=Math.Sin(w)/(2*(band?.6:Math.Sqrt(.5))),a=1+alpha;b0=band?alpha/a:(1-c)*.5/a;b1=band?0:(1-c)/a;b2=band?-alpha/a:b0;a1=-2*c/a;a2=(1-alpha)/a;}
            public double Tick(double x){double y=b0*x+b1*x1+b2*x2-a1*y1-a2*y2;x2=x1;x1=x;y2=y1;y1=y;return y;}
        }
    }
}
