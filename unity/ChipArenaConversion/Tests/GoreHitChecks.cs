#if ARENA_TESTS
using System;
using System.Collections.Generic;
namespace ChipArena.UnityConversion
{
    // C17 review fix: which Hit events splash ink on a Ward Zero add and which one aims a severed chunk (NativeGoreHits).
    // The source never bleeds for field, zone or burn/poison ticks (no direction, horde.ts:354), and a gib flies along the
    // severing hit. These pin that rule with directions known, and the interim per-add spacing while they are not.
    internal static class GoreHitChecks
    {
        public static void Run()
        {
            int count=0;void Check(bool ok,string what){if(!ok)throw new Exception("Gore hits: "+what);count++;}
            V3 none=new V3(),up=new V3(0,1,0),shot=new V3(.6f,0,.8f),swing=new V3(0,.45f,-.9f);
            Check(!NativeGoreHits.Directional(none)&&!NativeGoreHits.Directional(up)&&!NativeGoreHits.Directional(new V3(1e-7f,0,0)),"no horizontal part, no direction");
            Check(NativeGoreHits.Directional(shot)&&NativeGoreHits.Directional(swing)&&NativeGoreHits.Directional(new V3(0,-1,1e-5f)),"a horizontal part is a direction");
            double never=double.NegativeInfinity;
            Check(!NativeGoreHits.Splash(true,none,10,never)&&!NativeGoreHits.Splash(true,up,10,never),"known directions: a tick never splashes");
            Check(NativeGoreHits.Splash(true,shot,10,never)&&NativeGoreHits.Splash(true,shot,10.0167,10)&&!NativeGoreHits.Splash(true,shot,10,10)&&!NativeGoreHits.Splash(true,shot,9.9,10),"known directions: once per new hit time");
            Check(NativeGoreHits.Splash(false,none,10,never)&&!NativeGoreHits.Splash(false,shot,10.1,10)&&!NativeGoreHits.Splash(false,none,10.249,10)&&NativeGoreHits.Splash(false,none,10.25,10),"unknown directions: at most once per .25 s");

            // One add in a damaging field (a tick every fixed step) that also burns (a tick every .25 s) for 3 s, shot every .1 s.
            int Run3(bool directions,bool shots,out int peak)
            {
                var drops=new NativeGoreRules.DropList();double last=never;int splashes=0;peak=0;
                for(int step=0;step<180;step++)
                {
                    double t=100+step/60.0;var hits=new List<V3>{none};if(step%15==0)hits.Add(none);if(shots&&step%6==3)hits.Insert(0,shot);
                    foreach(var n in hits)if(NativeGoreHits.Splash(directions,n,t,last)){last=t;splashes++;for(int i=0;i<NativeGoreRules.Spawned(NativeGoreRules.HitInk(1),1);i++)drops.Add(NativeGoreRules.MakeDrop(t,(uint)step,i,0,1,0,2.2));}
                    drops.Update(t);peak=Math.Max(peak,drops.Count);
                }
                return splashes;
            }
            Check(Run3(true,false,out int quiet)==0&&quiet==0,"known directions: field and burn ticks bleed nothing");
            Check(Run3(true,true,out int shotPeak)==30&&shotPeak<=80,"known directions: each shot splashes once ("+shotPeak+" live drops)");
            int interim=Run3(false,false,out int interimPeak);
            Check(interim==12&&interimPeak<=40&&interimPeak<NativeGoreRules.DropMax/4,"unknown directions: a field add splashes 4 times a second ("+interim+" splashes, "+interimPeak+" live drops)");

            ArenaEvent Hit(int target,double at,V3 normal,EventKind kind=EventKind.Hit,int id=0)=>new ArenaEvent{Id=id,Kind=kind,Target=target,Time=(float)at,Normal=normal};
            // A bullet severs, then the field ticks later in the same step and in the next one.
            var events=new List<ArenaEvent>{Hit(7,10,shot),Hit(9,10,none),Hit(7,10,none),Hit(7,10+1/60.0,none)};
            Check(NativeGoreHits.SeverHit(events,7,10.03,true,out bool aimed)==0&&aimed,"known directions: the bullet, not the later tick, aims the chunk");
            Check(NativeGoreHits.SeverHit(events,7,10.03,false,out aimed)==3&&!aimed,"unknown directions: the newest hit, aimed by the caller");
            Check(NativeGoreHits.SeverHit(events,9,10.03,true,out aimed)==1&&!aimed,"known directions: a tick alone throws with no direction");
            // A burn tick severs .2 s after the last shot: the shot is too old to be the severing blow.
            var burn=new List<ArenaEvent>{Hit(7,9.8f,shot,EventKind.Headshot)};for(int i=0;i<=12;i++)burn.Add(Hit(7,9.8+i/60.0+.0001,none));
            Check(NativeGoreHits.SeverHit(burn,7,10.01,true,out aimed)==burn.Count-1&&!aimed,"known directions: a tick well after the last shot keeps no aim");
            Check(NativeGoreHits.SeverHit(burn.GetRange(0,1),7,9.81,true,out aimed)==0&&aimed,"headshots count as hits");
            var others=new List<ArenaEvent>{Hit(7,10,shot,EventKind.Death),Hit(7,10,shot,EventKind.Block),Hit(8,10,shot),Hit(7,9.6,shot),Hit(7,10.6,shot)};
            Check(NativeGoreHits.SeverHit(others,7,10,true,out aimed)==-1&&!aimed&&NativeGoreHits.SeverHit(others,7,10,false,out aimed)==-1,"other kinds, other adds, stale and far-future hits are ignored");
            Check(NativeGoreHits.SeverHit(new List<ArenaEvent>{Hit(7,10.45,shot)},7,10,true,out aimed)==0&&aimed,"a hit up to .5 s ahead of the presented clock counts");
            Console.WriteLine("PASS Ward Zero hit ink and sever aim (horde.ts:354 directed hits only; interim .25 s spacing): "+count+" assertions");
        }
    }
}
#endif
