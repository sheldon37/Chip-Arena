using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // Simulation-owned adapter for both authored clocks. No Unity objects or mutable clock state.
    public static class NativeStageHazards
    {
        public static bool City(MapHazard h)=>h.kind=="maglev"||h.kind=="grid";
        public static bool DeathKind(string kind)=>string.IsNullOrEmpty(kind)||kind=="collapse"||kind=="maglev"||kind=="grid"||kind=="hazard";
        public static void Validate(ContentMap map)
        {
            var hs=map.hazards??Array.Empty<MapHazard>();
            void Need(bool value,string reason){if(!value)throw new InvalidOperationException("Stage hazards "+map.id+": "+reason);}
            Need(hs.All(h=>h!=null&&!string.IsNullOrEmpty(h.id)&&h.id.StartsWith(map.id+"-",StringComparison.Ordinal))&&hs.Select(h=>h.id).Distinct().Count()==hs.Length,"IDs");
            foreach(var h in hs)
            {
                Need(NativeMapRules.Box(h.center,h.size)&&NativeMapRules.Finite(h.a)&&NativeMapRules.Finite(h.b)&&NativeMapRules.Finite(h.axis),"geometry "+h.id);
                Need(new[]{h.first,h.period,h.offset,h.warn,h.danger,h.speed,h.length,h.charge,h.live,h.tick,h.damage,h.radius,h.start,h.rise,h.travel,h.dwell,h.hoist,h.carry,h.push}.All(MathUtil.Finite),"finite clock "+h.id);
                Need(h.period>0&&h.damage>=0,"period/damage "+h.id);
                if(City(h))
                {
                    Need(h.first>=0&&h.first+h.offset>=0,"first "+h.id);
                    if(h.kind=="maglev")Need(h.lethal&&h.speed>0&&h.length>0&&h.period>h.warn&&h.warn>h.danger&&h.danger>0&&(h.size.x+h.length)/h.speed+2+h.warn<h.period,"maglev timing "+h.id);
                    else Need(!h.lethal&&h.charge>0&&h.live>0&&h.charge+h.live+1<=h.period&&h.tick>0&&h.damage>0,"grid timing "+h.id);
                }
                else
                {
                    foreach(var z in h.zones??Array.Empty<MapVolume>())Need(z!=null&&NativeMapRules.Box(z.center,z.size),"zone "+h.id);
                    NativeFactoryHazards.Validate(new FactoryHazard[]{h});
                    Need(h.kind=="vent"?h.tick>0&&!h.lethal:h.lethal,"damage mode "+h.id);
                    Need(h.rise>=0&&h.travel>=0&&h.dwell>=0&&h.hoist>=0,"negative duration "+h.id);
                }
            }
            foreach(var l in map.nav?.links??Array.Empty<MapNavLink>())Need((l.hazards??Array.Empty<string>()).All(id=>hs.Any(h=>h.id==id)),"nav hazard reference "+l.id);
        }
        // Exact distance between an upright capsule's axial segment and an AABB.
        public static bool Touch(V3 p,V3 center,V3 size,float radius=.42f,float halfHeight=.85f)
        {
            if(size.x<=0||size.y<=0||size.z<=0)return false;
            float x=Math.Max(0,Math.Abs(p.x-center.x)-size.x/2),z=Math.Max(0,Math.Abs(p.z-center.z)-size.z/2),y=Math.Max(0,Math.Abs(p.y-center.y)-Math.Max(0,halfHeight-radius)-size.y/2);
            return x*x+y*y+z*z<=radius*radius;
        }
        public static bool TrainSweep(MapHazard h,int round,double fightAt,double from,double to,out V3 center,out V3 size)
        {
            center=size=default;if(to<from||to<fightAt+h.first)return false;
            int cycle=(int)Math.Floor((to-fightAt-h.first)/h.period);double start=fightAt+h.first+cycle*h.period,end=start+(h.size.x+h.length)/h.speed;
            double a=Math.Max(from,start),b=Math.Min(to,end);if(b<a)return false;
            int dir=(cycle+round)%2==0?1:-1;double near=h.center.x-dir*h.size.x/2;
            double h0=near+dir*h.speed*(a-start),h1=near+dir*h.speed*(b-start);
            double lo=Math.Max(h.center.x-h.size.x/2,Math.Min(Math.Min(h0,h1),Math.Min(h0-dir*h.length,h1-dir*h.length)));
            double hi=Math.Min(h.center.x+h.size.x/2,Math.Max(Math.Max(h0,h1),Math.Max(h0-dir*h.length,h1-dir*h.length)));
            if(hi<=lo)return false;center=new V3((float)((lo+hi)/2),h.center.y,h.center.z);size=new V3((float)(hi-lo),h.size.y,h.size.z);return true;
        }
        // Number of scheduled ticks in (from,to], including the tick at active start, excluding active end.
        public static int Ticks(double from,double to,double start,double duration,double tick)
        {
            if(tick<=0||to<start||from>=start+duration||to<=from)return 0;
            int first=Math.Max(0,(int)Math.Floor((from-start)/tick+1e-7)+1);
            int last=Math.Min((int)Math.Ceiling(duration/tick-1e-7)-1,(int)Math.Floor((to-start)/tick+1e-7));return Math.Max(0,last-first+1);
        }
        public static int DamageTicks(MapHazard h,int round,double fightAt,double from,double to)
        {
            double origin,duration;
            if(h.kind=="grid"){origin=fightAt+h.first+(round%2==0?h.offset:0)+h.period-h.live-1;duration=h.live;}
            else if(h.kind=="vent"){origin=fightAt+NativeFactoryHazards.ArmDelay+h.start+h.offset+h.period-h.dwell;duration=h.dwell;}
            else return 0;
            int cycle=(int)Math.Floor((to-origin)/h.period);if(cycle<0)return 0;
            return Ticks(from,to,origin+cycle*h.period,duration,h.tick);
        }
        public static bool GridTouch(MapHazard h,V3 p,float halfHeight=.85f)
        {float feet=p.y-halfHeight;return feet>=h.center.y-h.size.y/2-.1f&&feet<=h.center.y+h.size.y/2&&Math.Abs(p.x-h.center.x)<h.size.x/2+.42f&&Math.Abs(p.z-h.center.z)<h.size.z/2+.42f;}
        public static bool FactoryBox(MapHazard h,in FactoryHazardState s,out V3 center,out V3 size)
        {
            center=s.BoxCenter;size=s.BoxSize;if(!s.Lethal)return false;
            if(h.kind=="saw-slot"){float top=center.y+size.y/2;size.y=Math.Max(0,top);center.y=top/2;}return size.y>0;
        }
        public static bool Landed(in FactoryHazardState s)=>s.Phase==NativeFactoryHazards.Landed||s.Phase==NativeFactoryHazards.Regrip;
        public static bool LinkOpen(MapHazard h,NativePhase phase,int round,double fightAt,double now,V3 a,V3 b)
        {
            if(phase!=NativePhase.Fight&&phase!=NativePhase.Result)return true;
            if(City(h))return NativeCyberCityHazards.At(h,phase,round,fightAt,now).Clear(h.kind=="maglev"?6:1.5);
            double clock=NativeFactoryHazards.Clock(phase,fightAt,now);var s=NativeFactoryHazards.State(h,round,clock);
            if(h.kind=="saw-slot")return s.Phase==NativeFactoryHazards.Parked||!Cross(a,b,new V3(s.Center.x, .8f,s.Center.z),new V3(8,1.6f,1.4f));
            if(h.kind=="crane")
            {
                if(s.Zone<0)return true;var zone=h.zones[s.Zone];
                if(s.Phase==NativeFactoryHazards.Travelling||s.Phase==NativeFactoryHazards.Warning||s.Phase==NativeFactoryHazards.Falling||Landed(s))return !Cross(a,b,zone.center,zone.size);
                return true;
            }
            if(h.kind=="vent")return h.damage<=0||s.Phase==NativeFactoryHazards.Parked;
            if(s.Phase!=NativeFactoryHazards.Parked)return false;
            // Check the whole one-second crossing window, including next cycle and pre-first idle.
            for(int i=1;i<=10;i++)if(NativeFactoryHazards.State(h,round,clock+i*.1).Phase!=NativeFactoryHazards.Parked)return false;
            return true;
        }
        public static bool Cross(V3 a,V3 b,V3 c,V3 size,float margin=.45f)=>NativeStructureRules.Crosses(a,b,new MapVolume{center=c,size=size},margin);
    }
}
