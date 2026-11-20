using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The bleed (docs/EXPEDITION-OVERHAUL.md R3/R12 and W7, docs/MERIDIAN-BLEED.md): how strongly the instance behind each
    /// door colours the city at a place, and the band that share falls in.
    ///
    /// Each door's share comes from a distance measured from that door along its own route: from a place p, the cheapest way
    /// to the door is to walk to some point q of the route and then along the route to the door, where the walk to q costs
    /// its metres up to <see cref="Free"/> and <see cref="Across"/> times as much beyond. So the street a route runs down, and
    /// the facades either side of it, read the route's own metres (the plan's 30/60/110/160 m bands are walked metres), while a
    /// parallel street behind the blocks reads one band further out. The minimum over every segment has a closed form (the
    /// segment's best point, <see cref="Distance"/>), so the field is exact, the same everywhere for the same records, and
    /// continuous: a step of 0.25 m changes a share by at most 0.25 x <see cref="Across"/> x the curve's steepest slope
    /// (0.005), never the 0.08 the plan allows, and nothing is sampled on cells, so there is no seam at any border.
    ///
    /// The share against those metres (<see cref="ShareAt"/>) is piecewise linear through the plan's band edges, and
    /// <see cref="Band"/> maps a share to its band. Presentation only, today: it reads records every peer already has (the
    /// routes and the hall deal), writes nothing and never moves a rule. From A5a (plan) Astra owns it, thresholds included,
    /// and spawns may read it; presentation may then smooth its own blend on top but never changes At or Band.
    /// </summary>
    public sealed class NativeCityBleed
    {
        /// <summary>The thresholds' version (meridian-b-candidate-3's routes; provisional until Astra's A5a).</summary>
        public const string Version="meridian-bleed-1";
        public enum Bands{None,Rumour,Drift,Warp,Threshold}
        /// <summary>The share each band starts at (the plan's R3/R12 table).</summary>
        public const float RumourAt=.10f,DriftAt=.25f,WarpAt=.45f,ThresholdAt=.70f;
        /// <summary>Metres off a route walked at their own cost, and what each metre beyond costs in route metres.</summary>
        public const float Free=10f,Across=2f;
        // The share against route metres from the door: the threshold is the last 30 m, the warp 30-60 m, the drift 60-110 m
        // and the rumour 110-160 m (the plan's distances on the layered city), fading out by 200 m.
        private static readonly float[] KnotMetres={0,30,60,110,160,200};
        private static readonly float[] KnotShare={1,ThresholdAt,WarpAt,DriftAt,RumourAt,0};
        // Off the route by h (beyond Free), the best point of a segment lies h x Lead further along it (Across > 1).
        private static readonly float Lead=1f/(float)Math.Sqrt(Across*Across-1);

        /// <summary>One door: the instance behind it, its route from the gate to the door (the door is the last point) and
        /// the way the door faces out along the route's last leg.</summary>
        public sealed class Door
        {
            public string Instance;public V3[] Route;public V3 At,Out;
            internal float[] Remaining;
        }
        /// <summary>The dominant door at a place (-1: none), its share and the band.</summary>
        public struct Sample{public int Door;public float Share;public Bands Band;}

        public readonly Door[] Doors;

        public NativeCityBleed(IReadOnlyList<Door> doors)
        {
            if(doors==null)throw new ArgumentNullException(nameof(doors));
            Doors=new Door[doors.Count];
            for(int d=0;d<doors.Count;d++)
            {
                var door=doors[d];if(door?.Route==null||door.Route.Length<2)throw new ArgumentException("A door needs a route of two points or more.");
                var rest=new float[door.Route.Length];
                for(int i=rest.Length-2;i>=0;i--)rest[i]=rest[i+1]+(door.Route[i+1]-door.Route[i]).Length;
                door.Remaining=rest;Doors[d]=door;
            }
        }

        /// <summary>Route metres from a place to door `door` (see the summary).</summary>
        public float Distance(int door,V3 p)
        {
            var d=Doors[door];var r=d.Route;var rest=d.Remaining;float best=Cost((p-r[r.Length-1]).Length);
            for(int i=0;i<r.Length-1;i++)
            {
                var a=r[i];var ab=r[i+1]-a;float len=ab.Length;if(len<1e-4f)continue;var u=ab/len;
                var ap=p-a;float s0=V3.Dot(ap,u);var foot=ap-u*s0;float h=foot.Length;
                // f(s) = rest(b) + (len - s) + cost(|p - q(s)|) is convex in s; its minimum (see the summary) clamped to the segment
                float s=s0+Math.Max(h*Lead,(float)Math.Sqrt(Math.Max(0,Free*Free-h*h)));
                if(s<0)s=0;else if(s>len)s=len;
                float off=(float)Math.Sqrt(h*h+(s0-s)*(s0-s));
                float c=rest[i+1]+(len-s)+Cost(off);
                if(c<best)best=c;
            }
            return best;
        }
        private static float Cost(float off)=>off<=Free?off:Free+Across*(off-Free);

        /// <summary>A door's share at a place (0-1).</summary>
        public float Share(int door,V3 p)=>ShareAt(Distance(door,p));

        /// <summary>The share at route metres from a door.</summary>
        public static float ShareAt(float metres)
        {
            if(!(metres>0))return KnotShare[0];
            for(int i=1;i<KnotMetres.Length;i++)
                if(metres<KnotMetres[i])
                {
                    float t=(metres-KnotMetres[i-1])/(KnotMetres[i]-KnotMetres[i-1]);
                    return KnotShare[i-1]+(KnotShare[i]-KnotShare[i-1])*t;
                }
            return 0;
        }

        /// <summary>The band a share falls in.</summary>
        public static Bands Band(float share)=>share>=ThresholdAt?Bands.Threshold:share>=WarpAt?Bands.Warp:share>=DriftAt?Bands.Drift:share>=RumourAt?Bands.Rumour:Bands.None;

        /// <summary>The dominant door at a place (the largest share; the first on a tie), its share and band.</summary>
        public Sample At(V3 p)
        {
            var s=new Sample{Door=-1};
            for(int d=0;d<Doors.Length;d++){float k=Share(d,p);if(k>s.Share){s.Share=k;s.Door=d;}}
            s.Band=Band(s.Share);if(s.Band==Bands.None)s.Door=-1;return s;
        }

        /// <summary>Every door's share at a place, into `shares` (its length at least the door count).</summary>
        public void Shares(V3 p,float[] shares){for(int d=0;d<Doors.Length;d++)shares[d]=Share(d,p);}

        /// <summary>The theme id of an instance (the module variant suffix `@theme`, docs/MERIDIAN-BLEED.md): level0, poolrooms,
        /// wardzero, station, ironworks and final, by the walk's names or the run's area ids; null for anything else.</summary>
        public static string Theme(string instance)
        {
            switch(instance)
            {
                case "Level 0":case "exp-backrooms":return "level0";
                case "The Poolrooms":case "exp-poolrooms":return "poolrooms";
                case "Ward Zero":case "exp-ward-zero":return "wardzero";
                case "Central Station":case "exp-transit":return "station";
                case "City Ironworks":case "exp-foundry":return "ironworks";
                case "Final court":return "final";
                default:return null;
            }
        }

        /// <summary>The theme the city takes at a place, for the module planner (D1's NativeMeridianDress.ThemeAt): the dominant
        /// door's theme from the drift band on, with a seeded dither of up to 0.03 across the band's edge (6 m cells), else null
        /// (plain Meridian). Call it once per facade, at the facade's centre, so a whole facade swaps at once.</summary>
        public string ThemeAt(V3 p)
        {
            var s=At(p);if(s.Door<0)return null;
            uint h=Cell(p,6);
            float edge=DriftAt+((h&1023)/1023f-.5f)*.06f;
            return s.Share>=edge?Theme(Doors[s.Door].Instance):null;
        }

        /// <summary>Whether the bleed's own lamps stand at a place instead of the city's plain ones (the drift band on: troffers,
        /// red beacons, signal heads, work lamps and pool lamps replace the sodium lamps, R3/R12).</summary>
        public bool ReplacesLamps(V3 p)=>At(p).Band>=Bands.Drift;

        /// <summary>A small integer hash (the planner's and the lamps' seeded choices).</summary>
        public static uint Hash(uint x){unchecked{x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;x^=x>>16;return x;}}
        /// <summary>The hash of the `size` m cell a place lies in.</summary>
        public static uint Cell(V3 p,float size)
        {
            unchecked{return Hash((uint)(int)Math.Floor(p.x/size)*73856093u^(uint)(int)Math.Floor(p.z/size)*19349663u^(uint)(int)Math.Floor(p.y/size)*83492791u);}
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || ARENA_TESTS
        /// <summary>The Developer walk's doors: slot i (0 the final court, then east, south and west) at the end of route i,
        /// facing out along its last leg (as the walk's load fixture places the door records).</summary>
        public static NativeCityBleed ForWalk(NativeLayeredCityWalk walk,IReadOnlyList<string> slots)
        {
            if(walk==null)throw new ArgumentNullException(nameof(walk));if(slots==null||slots.Count!=walk.Routes.Count)throw new ArgumentException("One instance per route.");
            var doors=new List<Door>();
            for(int i=0;i<walk.Routes.Count;i++)
            {
                var pts=walk.Routes[i].Points;var at=pts[pts.Length-1];var leg=at-pts[pts.Length-2];leg=new V3(leg.x,0,leg.z);
                doors.Add(new Door{Instance=slots[i],Route=pts,At=at,Out=leg.Length>1e-4f?leg/(-leg.Length):new V3(0,0,-1)});
            }
            return new NativeCityBleed(doors);
        }
#endif
    }
}
