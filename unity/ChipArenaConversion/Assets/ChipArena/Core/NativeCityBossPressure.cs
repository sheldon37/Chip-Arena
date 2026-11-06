using System;
using System.Collections.Generic;
namespace ChipArena.UnityConversion
{
    // Independent environmental attacks: no root distance, boss route, line of sight or nearest-player priority.
    // The renderer must consume the locked world XYZ/radius and the complete warning before this ships.
    public sealed class NativeCityBossPressure
    {
        public const float Interval=6,Warning=1.5f,Radius=2.2f,Damage=18,Deadline=8;
        private readonly Dictionary<int,float> next=new Dictionary<int,float>();
        private int serial=1000000;
        public static bool Kind(string kind)=>kind=="glass-pressure"||kind=="slag-pressure";
        public void Reset(){next.Clear();serial=1000000;}
        public void Remove(int seat)=>next.Remove(seat);
        public bool Due(int seat,float now)=>next.TryGetValue(seat,out float due)&&now>=due;
        public bool Schedule(string boss,int seat,float now,V3 feet,out NativeZoneBossHazardFrame hazard)
        {
            hazard=null;if(boss!=NativeZoneBossRules.Station&&boss!=NativeZoneBossRules.Ironworks)return false;
            if(!next.TryGetValue(seat,out float due)){next[seat]=now+Interval;return false;}
            if(now<due)return false;
            next[seat]=now+Interval;
            hazard=new NativeZoneBossHazardFrame{id=serial++,kind=boss==NativeZoneBossRules.Station?"glass-pressure":"slag-pressure",targetSeat=seat,
                x=(float)feet.x,y=(float)feet.y,z=(float)feet.z,radius=Radius,hitAt=now+Warning,until=now+Warning+.5f};return true;
        }
        public static bool Contact(NativeZoneBossHazardFrame h,int seat,V3 feet,float radius)
        {
            if(h.targetSeat!=seat||feet.y<h.y-.35||feet.y>h.y+2.5)return false;
            double x=feet.x-h.x,z=feet.z-h.z;return x*x+z*z<=(h.radius+radius)*(h.radius+radius);
        }
    }
}
