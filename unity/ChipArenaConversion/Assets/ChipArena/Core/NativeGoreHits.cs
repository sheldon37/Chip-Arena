using System;
using System.Collections.Generic;
namespace ChipArena.UnityConversion
{
    // C17: which replicated Hit events splash ink on a Ward Zero add, and which one times and aims a severed chunk. In the
    // source only a hit whose direction has a horizontal part sets the add's hitAt (shared/pve/horde.ts:354), and only a
    // new hitAt splashes ink (client/pve/horde-view.ts:211). Chip fields, ability zones and burn/poison ticks hit with no
    // direction (shared/chaos.ts:122,138; hitMinion's dir=v()), so they never bleed, and a gib flies along the direction of
    // the hit that severed it (gore.ts:52-61; zero for those ticks, so it rises with only its hashed jitter).
    // A Unity Hit event carries that direction in ArenaEvent.Normal (Unity space) once the simulation fills it (DEV_LOG,
    // needs Astra). Until an arena shows one directed hit, no hit can be told from a tick: each add then splashes at most
    // once per HitInkGap so a damaging field cannot flood the ink pool, and a sever takes the newest hit as before.
    public static class NativeGoreHits
    {
        // HitInkGap: the per-add splash spacing while directions are unknown. Ahead/Behind: a hit up to .5 s ahead of the
        // presented clock waits in the pool; one .3 s old is stale (horde-view.ts:211). SeverSlack: how far behind the
        // newest hit on the add a directed hit may be and still be taken as the severing blow.
        public const double HitInkGap=.25,Ahead=.5,Behind=.3,SeverSlack=.1;

        public static bool Directional(V3 n)=>Math.Sqrt((double)n.x*n.x+(double)n.z*n.z)>1e-6;
        public static bool IsHit(in ArenaEvent e)=>e.Kind==EventKind.Hit||e.Kind==EventKind.Headshot;
        public static bool Recent(double at,double now)=>at-now<=Ahead&&now-at<Behind;

        // Does a hit at `at` with `normal` splash, given the add's last splash time (negative infinity for none)?
        // `directions`: this arena's hits are known to carry their direction.
        public static bool Splash(bool directions,V3 normal,double at,double last)=>directions?Directional(normal)&&at>last:at-last>=HitInkGap;

        // The hit that times and aims a fresh sever on `target`, or -1. Directions known: the newest recent directed hit
        // within SeverSlack of the newest recent hit (a field or burn tick later in the same step, or a frame or two
        // later, does not steal the aim); if none, the newest recent hit, a tick, with no aim. Directions unknown: the
        // newest recent hit, `aimed` false (the caller aims it from the attacker).
        public static int SeverHit(IReadOnlyList<ArenaEvent> events,int target,double now,bool directions,out bool aimed)
        {
            int newest=-1;double newestAt=0;aimed=false;
            for(int i=events.Count-1;i>=0;i--)
            {
                var e=events[i];if(e.Target!=target||!IsHit(e)||!Recent(e.Time,now))continue;
                if(newest<0){newest=i;newestAt=e.Time;if(!directions)return i;}
                if(newestAt-e.Time>SeverSlack)break;
                if(Directional(e.Normal)){aimed=true;return i;}
            }
            return newest;
        }
    }
}
