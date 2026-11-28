using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Factory saws' bisect (owner 2026-10-12: the buzzsaw death splits the body in the middle): which saw killed a pilot,
    /// and the cut it makes, from replicated data only (the map's hazards, the round, FightAt and the death record's time and
    /// place), so the host, guests, late joiners and the killcam split the same body the same way. The plane is the killing
    /// blade's (its normal the axle, NativeFactoryHazards "saw-slot" and "saw-rail") snapped to the body's midline through the
    /// pelvis; the blade's travel and spin drive the parting, the sparks and the blood flung off its rim. Presentation only:
    /// no hit, damage, timing, network or hazard rule reads this.
    /// </summary>
    public static class NativeSawBisect
    {
        public static bool Saw(FactoryHazard h)=>h!=null&&(h.kind==NativeFactoryHazards.SawSlot||h.kind==NativeFactoryHazards.SawRail);
        /// <summary>A hazard's live kill volume at a state (NativeStageHazards.FactoryBox for any FactoryHazard: a slot blade's
        /// box reaches only from the floor to its top).</summary>
        public static bool Box(FactoryHazard h,in FactoryHazardState s,out V3 center,out V3 size)
        {
            center=s.BoxCenter;size=s.BoxSize;if(!s.Lethal)return false;
            if(h.kind==NativeFactoryHazards.SawSlot){float top=center.y+size.y/2;size.y=Math.Max(0,top);center.y=top/2;}return size.y>0;
        }
        /// <summary>
        /// The index of the saw that killed a pilot whose death record says a stage hazard killed it at `deathAt` (match time) with
        /// its body centre at `body` (NativeDeath.position), or -1 (another hazard did, or none is found). The authority's order:
        /// the first lethal hazard touching the pilot on the death's tick (or the tick before) kills; failing that (the record's
        /// float time), the nearest lethal saw within <see cref="Reach"/>.
        /// </summary>
        public static int Find(FactoryHazard[] hazards,int round,double fightAt,double deathAt,V3 body)
        {
            if(hazards==null||double.IsNaN(deathAt))return -1;double clock=NativeFactoryHazards.Clock(NativePhase.Fight,fightAt,deathAt);
            for(int pass=0;pass<2;pass++)
            {
                double t=clock-pass/60.0;
                for(int i=0;i<hazards.Length;i++)
                {
                    var h=hazards[i];if(h==null||h.kind==NativeFactoryHazards.Vent&&!h.lethal)continue;var s=NativeFactoryHazards.State(h,round,t);
                    if(!Box(h,s,out var c,out var size))continue;if(NativeStageHazards.Touch(body,c,size))return Saw(h)?i:-1;
                }
            }
            int best=-1;double near=Reach;
            for(int i=0;i<hazards.Length;i++)
            {
                var h=hazards[i];if(!Saw(h))continue;
                for(int pass=0;pass<3;pass++)
                {
                    var s=NativeFactoryHazards.State(h,round,clock+(pass-1)/60.0);if(!Box(h,s,out var c,out var size))continue;
                    double dx=Math.Max(0,Math.Abs(body.x-c.x)-size.x/2),dy=Math.Max(0,Math.Abs(body.y-c.y)-size.y/2-.43),dz=Math.Max(0,Math.Abs(body.z-c.z)-size.z/2),d=Math.Sqrt(dx*dx+dy*dy+dz*dz);
                    if(d<near){near=d;best=i;}
                }
            }
            return best;
        }
        /// <summary>How far past touching (m) a saw may stand from the body when the record's time missed the exact tick.</summary>
        public const double Reach=.75;
        /// <summary>The blade's axle as the rig turns it (Core NativeFactoryRig: an axle mostly along x spins about +x, else +z).</summary>
        public static Vector3 RigAxle(FactoryHazard h)=>Math.Abs(h.axis.x)>.5f?Vector3.UnitX:Vector3.UnitZ;
        /// <summary>The cut plane's normal: the hazard's authored axle (unit; its rig axis when the data has none).</summary>
        public static Vector3 Axle(FactoryHazard h){var a=new Vector3(h.axis.x,h.axis.y,h.axis.z);return a.LengthSquared()>1e-8f?Vector3.Normalize(a):RigAxle(h);}
        /// <summary>
        /// The cut a saw makes through a body whose pelvis is at `pelvis` (Unity world), the saw at hazard clock `clock` in round
        /// `round`: the blade's plane (normal its axle) moved to the pelvis, the blade's velocity and spin (from the pure state
        /// either side of that clock), a slot blade holding the body a little longer than a rail blade, sparks, and heavy blood.
        /// </summary>
        public static BisectRequest Request(FactoryHazard h,int round,double clock,Vector3 pelvis,uint seed)
        {
            var s=NativeFactoryHazards.State(h,round,clock);var a=NativeFactoryHazards.State(h,round,clock-.01);var b=NativeFactoryHazards.State(h,round,clock+.01);
            var travel=(N(b.Center)-N(a.Center))/.02f;if(!(travel.LengthSquared()<400))travel=Vector3.Zero;
            bool slot=h.kind==NativeFactoryHazards.SawSlot;
            var r=NativeBisect.Request(h.kind,pelvis,Axle(h),travel,seed,part:slot?1.55f:1.35f,lift:slot?1.1f:.5f,push:.16f,topple:slot?2.6f:2.1f,hitch:slot?.17f:.13f,blood:1.6f,sparks:true);
            r.Hub=N(s.Center);if(!slot)r.Hub-=new Vector3(.12f,0,0);r.Axle=RigAxle(h);r.Rate=(float)((b.Spin-a.Spin)/.02);if(float.IsNaN(r.Rate)||float.IsInfinity(r.Rate))r.Rate=0;r.Radius=h.radius;
            return r;
        }
        private static Vector3 N(V3 v)=>new Vector3(v.x,v.y,v.z);
    }
}
