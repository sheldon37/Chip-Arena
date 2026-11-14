using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Hollow Sanctum's hazard sounds as discrete cues (docs/arenas/SANCTUM.md "Hazards"), pure functions of the replicated
    /// clocks like NativeFactoryHazards.Cues: every cue whose time lies in (from, to] on the arena clock, for a caller that keeps its
    /// last clock (each cue lies in exactly one window; a clock that runs back, a new round or a killcam rewind just restarts it).
    /// <list type="bullet">
    /// <item>The chandeliers (crane): the creak as the warning starts, the chain's two slips, the snap, the crash, the winch.</item>
    /// <item>The censer (saw-rail): the coals flaring as it is drawn back, the whoosh out and the whoosh back.</item>
    /// <item>Holy fire (burner): the kindling at the warning, the roar as it pours, the hiss as it dies.</item>
    /// <item>The toll (shutter): three strokes of the bell through the warning, the gates' fall, their slam, their rise.</item>
    /// </list>
    /// Allocation free.
    /// </summary>
    public static class NativeSanctumCues
    {
        public const int Creak=0,Slip=1,Snap=2,Crash=3,Winch=4,CenserFlare=5,CenserOut=6,CenserBack=7,FireKindle=8,FireRoar=9,FireHiss=10,Toll=11,GateFall=12,GateSlam=13,GateRise=14;
        public static int Between(MapHazard[] hazards,int round,NativePhase phase,double fightAt,double from,double to,FactoryCue[] into)
        {
            int count=0;if(hazards==null||into==null||!(to>from)||double.IsNaN(from)||double.IsInfinity(to))return 0;
            if(phase!=NativePhase.Fight&&phase!=NativePhase.Result)return 0;
            for(int i=0;i<hazards.Length;i++)
            {
                var h=hazards[i];if(h==null||!(h.period>0))continue;
                void Add(int kind,double at,V3 p,int cycle){if(at>from&&at<=to&&count<into.Length)into[count++]=new FactoryCue{Kind=kind,Hazard=i,Cycle=cycle,Time=at,At=p};}
                if(h.kind==NativeFactoryHazards.Crane||h.kind==NativeFactoryHazards.SawRail)
                {
                    // the factory clock: FightAt + the arm delay + start + offset + k * period
                    double origin=fightAt+NativeFactoryHazards.ArmDelay+h.start+h.offset;
                    for(double k=Math.Max(0,Math.Floor((from-origin)/h.period)-1);k<=Math.Floor((to-origin)/h.period);k++)
                    {
                        double c0=origin+k*h.period;int n=(int)k;
                        if(h.kind==NativeFactoryHazards.Crane)
                        {
                            int zone=NativeFactoryHazards.ZoneOf(h,round,n);if(zone<0)continue;var z=h.zones[zone];var top=new V3(z.center.x,(float)h.carry+1,z.center.z);var floor=new V3(z.center.x,z.center.y+.3f,z.center.z);
                            double w0=c0+h.travel;Add(Creak,w0,top,n);Add(Slip,w0+h.warn*.45,top,n);Add(Slip,w0+h.warn*.8,top,n);Add(Snap,w0+h.warn,top,n);Add(Crash,w0+h.warn+h.rise,floor,n);Add(Winch,w0+h.warn+h.rise+h.dwell+NativeFactoryHazards.RegripSeconds,top,n);
                        }
                        else
                        {
                            var a=new V3(h.a.x,h.a.y+.6f,h.a.z);var mid=new V3((h.a.x+h.b.x)/2,h.a.y,(h.a.z+h.b.z)/2);
                            Add(CenserFlare,c0,a,n);Add(CenserOut,c0+h.warn,mid,n);Add(CenserBack,c0+h.warn+h.travel+h.dwell,mid,n);
                        }
                    }
                }
                else if(h.kind==NativeWardZeroHazards.Burner||h.kind==NativeWardZeroHazards.Shutter)
                {
                    double origin=fightAt+h.first+(round%2==0?h.offset:0);
                    for(double k=Math.Max(0,Math.Floor((from-origin)/h.period)-1);k<=Math.Floor((to-origin)/h.period);k++)
                    {
                        double c0=origin+k*h.period;int n=(int)k;var c=h.center;
                        if(h.kind==NativeWardZeroHazards.Burner){Add(FireKindle,c0,c,n);Add(FireRoar,c0+h.warn,c,n);Add(FireHiss,c0+h.warn+h.live,c,n);}
                        else
                        {
                            var bell=new V3((float)NativeSanctumLooks.BellX,(float)NativeSanctumLooks.BellY-1.5f,(float)NativeSanctumLooks.BellZ);
                            // the bell rings once for both gates (the first shutter); each gate falls, slams and rises where it stands
                            bool first=true;for(int j=0;j<i;j++)if(hazards[j]?.kind==NativeWardZeroHazards.Shutter){first=false;break;}
                            if(first)for(int s=0;s<3;s++)Add(Toll,c0+s,bell,n);
                            Add(GateFall,c0+h.warn,c,n);Add(GateSlam,c0+h.warn+h.travel,c,n);Add(GateRise,c0+h.warn+h.travel+h.dwell,c,n);
                        }
                    }
                }
            }
            return count;
        }
    }
}
