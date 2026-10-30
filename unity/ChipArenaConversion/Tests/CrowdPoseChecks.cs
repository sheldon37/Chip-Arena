using System;
namespace ChipArena.UnityConversion
{
    internal static class CrowdPoseChecks
    {
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Crowd pose: "+what);}
        public static void Run()
        {
            foreach(int hz in new[]{30,60,144})
            {
                var clock=new NativeCrowdPoseClock();int far=0;
                for(int i=0;i<hz*10;i++)if(clock.Due(i/(double)hz,900,false,false,10001))far++;
                Check(far>=99&&far<=101,"offscreen rate remains 10 Hz at "+hz+" Hz");
                clock.Reset();
                for(int i=0;i<hz;i++)Check(clock.Due(i/(double)hz,900,true,false,10001),"visible every frame");
                clock.Reset();
                for(int i=0;i<hz;i++)Check(clock.Due(i/(double)hz,144,false,false,10001),"close every frame");
                clock.Reset();
                for(int i=0;i<hz;i++)Check(clock.Due(i/(double)hz,900,false,true,10001),"urgent every frame");
            }
            var c=new NativeCrowdPoseClock();Check(c.Due(10,900,false,false,0),"first pose");
            Check(!c.Due(10.001,900,false,false,0),"redundant hidden pose skipped");
            Check(c.Due(10.002,900,true,false,0),"camera turn immediately refreshes");
            Check(c.Due(1,900,false,false,0),"rewind refreshes");
            c.Reset();Check(c.Due(1.001,900,false,false,0),"pooled model refreshes");
            var clocks=new NativeCrowdPoseClock[40];for(int i=0;i<clocks.Length;i++){clocks[i]=new NativeCrowdPoseClock();clocks[i].Due(0,900,false,false,10000+i);}
            int peak=0,total=0;for(int f=1;f<=60;f++){int count=0;for(int i=0;i<clocks.Length;i++)if(clocks[i].Due(f/60d,900,false,false,10000+i))count++;peak=Math.Max(peak,count);total+=count;}
            Check(total==400,"40 hidden skeletons: 400 updates/sec instead of 2400");Check(peak<16,"refreshes staggered across frames");
            Console.WriteLine("PASS Crowd pose: full-rate visibility/near/urgent, rewind/pool reset; 40 hidden skeletons 2400 -> 400 updates/sec, peak "+peak);
        }
    }
}
