using System;
namespace ChipArena.UnityConversion
{
    // Fixed storage; retain a rolling window and session worst even after the pause menu opens.
    public sealed class NativeHitchWindow
    {
        public const int Capacity=240;
        private readonly double[] frames=new double[Capacity];
        private int head,count,reports,slowFrames;private double sum,nextReport;
        public double WorstMs {get;private set;}
        public int Hitches {get;private set;}
        public double AverageMs=>count==0?0:sum/count;
        public bool Observe(double now,double milliseconds)
        {
            if(double.IsNaN(milliseconds)||double.IsInfinity(milliseconds)||milliseconds<0)return false;
            if(count==Capacity)sum-=frames[head];else count++;
            frames[head]=milliseconds;sum+=milliseconds;head=(head+1)%Capacity;
            WorstMs=Math.Max(WorstMs,milliseconds);
            slowFrames=milliseconds>=33.333?Math.Min(Capacity,slowFrames+1):0;
            if(milliseconds>=100)Hitches++;
            if(milliseconds<100&&slowFrames<20&&(count<60||AverageMs<33.333))return false;
            if(now<nextReport||reports>=120)return false;
            nextReport=now+2;reports++;return true;
        }
    }
}
