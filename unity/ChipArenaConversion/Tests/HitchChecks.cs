using System;
using ChipArena.UnityConversion;
internal static class HitchChecks
{
    public static void Run()
    {
        void Check(bool ok,string why){if(!ok)throw new Exception("Hitch diagnostics: "+why);}
        var w=new NativeHitchWindow();for(int i=0;i<240;i++)Check(!w.Observe(i/120d,8),"smooth frame reported");
        Check(w.Observe(3,2500)&&w.WorstMs==2500&&w.Hitches==1,"multi-second freeze retained");
        Check(!w.Observe(3.1,500)&&w.Hitches==2,"rate limit still counts stalls");
        for(int i=0;i<240;i++)w.Observe(10+i/120d,8);
        Check(w.WorstMs==2500&&Math.Abs(w.AverageMs-8)<.001,"pause cannot erase session worst; rolling average recovers");
        Check(!w.Observe(15,double.NaN),"invalid sample refused");
        w=new NativeHitchWindow();for(int i=0;i<240;i++)w.Observe(i*.008,8);
        bool sustained=false;for(int i=0;i<40;i++)sustained|=w.Observe(3+i*.05,50);
        Check(sustained&&w.Hitches==0,"sustained 20fps slowdown reported without claiming 100ms hitch");
        w=new NativeHitchWindow();for(int i=0;i<120;i++)Check(w.Observe(i*3,150),"allowed bounded report");
        Check(!w.Observe(1000,4000)&&w.WorstMs==4000&&w.Hitches==121,"log cap keeps worst and counters");
        Console.WriteLine("PASS hitch diagnostics: freeze, sustained slowdown, rolling recovery, throttle, cap, invalid sample and retained session worst");
    }
}
