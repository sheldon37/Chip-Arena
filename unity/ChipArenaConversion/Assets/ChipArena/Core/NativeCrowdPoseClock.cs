using System;
namespace ChipArena.UnityConversion
{
    // Only the remote, off-camera model skeleton is decimated. Roots, gait, combat and visible poses still run every frame.
    public sealed class NativeCrowdPoseClock
    {
        public const double Interval=.1,NearSquared=144;
        private double last=double.NaN;
        public bool Due(double now,double distanceSquared,bool visible,bool urgent,int id)
        {
            double phase=(uint)id%17/17d;
            bool due=double.IsNaN(last)||now<last||visible||urgent||distanceSquared<=NearSquared||
                Math.Floor(now/Interval+phase)!=Math.Floor(last/Interval+phase);
            if(due)last=now;
            return due;
        }
        public void Reset(){last=double.NaN;}
    }
}
