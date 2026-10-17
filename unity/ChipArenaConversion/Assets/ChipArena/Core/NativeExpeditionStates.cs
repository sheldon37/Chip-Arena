using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // Expedition state presentation (Claude, 2026-10-14; docs/EXPEDITION-PRESENTATION.md "Doors, gate, sanctuary, transfer and
    // boss arrival"). Engine-free: what the doors, the final gate, the sanctuary, the area transfer screen and the boss intro show,
    // read from the public ExpeditionView only (Astra's director state: closures[], areaBarrier, bossArrivals[], members[].safe)
    // and the static layout manifest (Resources/ExpeditionLayout.json) every client holds. Host and guests read the same fields,
    // so they draw the same thing. Nothing here decides a rule, writes the view or touches collision: a closure blocks exactly
    // when its state is not "open" (ExpeditionClosureView.Blocking, the collider's predicate) and the drawing follows that.
    // A missing record (no closure for a door, no layout fingerprint, no position for a boss) draws nothing.

    public enum ExpeditionClosurePhase { Open, Sealing, Sealed, Opening }

    public static class NativeExpeditionStates
    {
        /// <summary>The director's transition: sealing and opening take one second of the authority clock (ExpeditionDirector).</summary>
        public const double TransitionSeconds=1;
        /// <summary>How far the presentation clock may run ahead of the last view's elapsed (views arrive at 20 Hz).</summary>
        public const double MaxLead=.25;
        /// <summary>A boss arrival first seen later than this after its spawn shows no intro (only the HUD boss plate).</summary>
        public const double LateJoin=3;
        /// <summary>The boss intro's length from the authority spawn time, and the final gate's beacon cue from its opening.</summary>
        public const double IntroSeconds=3.4,CueSeconds=7,GateNoticeSeconds=4;
        /// <summary>A held gate cue starts this long after the client is back in the gate's area (the transfer screen's fade-out).</summary>
        public const double ArriveDelay=.5;
        public const string Open="open",Sealing="sealing",Sealed="sealed",Opening="opening";

        /// <summary>The closure's phase. An unknown or missing state reads sealed: the collider blocks on anything but "open".</summary>
        public static ExpeditionClosurePhase Phase(ExpeditionClosureView c)
        {
            switch(c?.state){case Open:return ExpeditionClosurePhase.Open;case Sealing:return ExpeditionClosurePhase.Sealing;case Opening:return ExpeditionClosurePhase.Opening;default:return ExpeditionClosurePhase.Sealed;}
        }
        /// <summary>The collider's predicate, mirrored: blocking from the start of sealing to the end of opening (and on any
        /// state that is not "open").</summary>
        public static bool Blocking(ExpeditionClosureView c)=>c!=null&&c.state!=Open;
        /// <summary>How far through its one-second transition a sealing or opening closure is at presentation time t (0..1).</summary>
        public static float Progress(ExpeditionClosureView c,double t)
        {
            if(c==null)return 1;double p=(t-c.began)/TransitionSeconds;return p>=1||double.IsNaN(p)?1:p<=0?0:(float)p;
        }
        /// <summary>A heavy door's travel: slow off the mark, slow into the stop (smoothstep).</summary>
        public static float Ease(float x){x=x<0?0:x>1?1:x;return x*x*(3-2*x);}
        /// <summary>How shut a closure is at t: 0 open, 1 sealed; sealing and opening travel over exactly TransitionSeconds from
        /// `began`, the authority time the state started.</summary>
        public static float Shut(ExpeditionClosureView c,double t)
        {
            switch(Phase(c))
            {
                case ExpeditionClosurePhase.Open:return 0;
                case ExpeditionClosurePhase.Sealing:return Ease(Progress(c,t));
                case ExpeditionClosurePhase.Opening:return 1-Ease(Progress(c,t));
                default:return 1;
            }
        }
        /// <summary>The door's lock bolts: they engage in the last 15% of sealing and draw back in the first 15% of opening.</summary>
        public static float Bolts(ExpeditionClosureView c,double t)
        {
            float p=Progress(c,t);
            switch(Phase(c))
            {
                case ExpeditionClosurePhase.Sealed:return 1;
                case ExpeditionClosurePhase.Sealing:return p<.85f?0:(p-.85f)/.15f;
                case ExpeditionClosurePhase.Opening:return p>=.15f?0:1-p/.15f;
                default:return 0;
            }
        }
        /// <summary>The final gate: its lock clamps draw back over the first quarter of opening, then it rises over the rest (and
        /// the reverse when it seals). Returns the rise (0 down, 1 fully up) and the unlock (0 locked, 1 clear).</summary>
        public static float GateRise(ExpeditionClosureView c,double t,out float unlock)
        {
            float p=Progress(c,t);
            switch(Phase(c))
            {
                case ExpeditionClosurePhase.Open:unlock=1;return 1;
                case ExpeditionClosurePhase.Opening:unlock=Math.Min(1,p/.25f);return Ease((p-.2f)/.8f);
                case ExpeditionClosurePhase.Sealing:unlock=p<.8f?1:1-(p-.8f)/.2f;return 1-Ease(p/.8f);
                default:unlock=0;return 0;
            }
        }
        /// <summary>A layout area id as the screens name it: "exp-ward-zero" is WARD ZERO (NativeExpeditionPresentation.Name drops
        /// the manifest's "exp-" prefix).</summary>
        public static string AreaName(string id)=>NativeExpeditionPresentation.Name(id);
        /// <summary>A boss archetype as the intro names it. Test encounters keep TEST in the name until a boss design exists.</summary>
        public static string BossName(string archetype)
        {
            if(string.IsNullOrEmpty(archetype))return "UNKNOWN BOSS";string z=NativeZoneBossNames.Name(archetype);if(z!=null)return z;string n=NativeExpeditionPresentation.Name(archetype);
            return archetype.IndexOf("test",StringComparison.OrdinalIgnoreCase)>=0&&n.IndexOf("TEST",StringComparison.Ordinal)<0?"TEST · "+n:n;
        }
        /// <summary>Whether the sanctuary is lit: the phases in which the authority grants `safe` (town and victory). In any other
        /// phase the boundary stays a dim outline, so it never looks like shelter the authority is not giving.</summary>
        public static bool SanctuaryLit(ExpeditionView v)=>v!=null&&(v.phase==ExpeditionPhase.Town||v.phase==ExpeditionPhase.Victory);
        /// <summary>Whether the view names this client's layout: areaBarrier.layout is the authority's validated fingerprint
        /// (manifest bytes plus content revision), published as soon as the director binds the layout (the initial inactive
        /// barrier included), so the state pieces bind before the first area load. Anything else binds nothing.</summary>
        public static bool Bound(ExpeditionView v,string fingerprint)=>v?.areaBarrier?.layout!=null&&fingerprint!=null&&v.areaBarrier.layout==fingerprint;
        /// <summary>Whether the arena holds the map of the bound area (its rig's map). The view names the next area as soon as the
        /// authority moves the run, before the scene has loaded it: until the arena holds that map nothing anchored to the area
        /// (doors, gate, sanctuary, world pieces and their holograms) is drawn, so nothing stands in the previous map. Without a
        /// rig (layout not bound) there is no map to compare: true, and the world pieces keep their anchor fallback.</summary>
        public static bool MapReady(ExpeditionStateRig rig,string arenaMap)=>rig==null||rig.Map!=null&&arenaMap==rig.Map;
        /// <summary>A stable string hash (FNV-1a over the characters; .NET's string hash differs per process), for patterns that
        /// must match on every client.</summary>
        public static int StableHash(string s,int h=unchecked((int)2166136261))
        {unchecked{if(s!=null)for(int i=0;i<s.Length;i++){h^=s[i];h*=16777619;}return h;}}
        /// <summary>A deterministic hash in [0,1) (no randomness: every client draws the same pattern).</summary>
        public static float Hash(int a,int b)
        {unchecked{uint h=(uint)a*0x8DA6B343u^(uint)b*0xD8163841u^0x9E3779B9u;h^=h>>15;h*=0x2C1B3C6Du;h^=h>>12;h*=0x297A2D39u;h^=h>>15;return (h&0xFFFFFF)/16777216f;}}
    }

    /// <summary>
    /// The presentation clock: the authority's run time (`elapsed`, the clock every closure `began` and boss `spawned` is on) as
    /// of the last view, run forward by local time between views (at most MaxLead) so a one-second transition moves smoothly at
    /// 20 Hz projections. It holds still while the run is frozen (loading or a host transfer) and never steps back by a sliver;
    /// a new run, or a rewind of more than a second, starts it over.
    /// </summary>
    public sealed class ExpeditionPresentationClock
    {
        private string run;private double elapsed=double.NaN,anchor;
        public double Now{get;private set;}
        public void Reset(){run=null;elapsed=double.NaN;Now=0;}
        public double Observe(ExpeditionView v,double local)
        {
            if(v==null){Reset();return 0;}
            bool fresh=v.run!=run||double.IsNaN(elapsed);
            if(fresh||v.elapsed!=elapsed){run=v.run;elapsed=v.elapsed;anchor=local;}
            double lead=v.loading||v.migrating?0:Math.Min(NativeExpeditionStates.MaxLead,Math.Max(0,local-anchor));
            double now=elapsed+lead;
            Now=!fresh&&now<Now&&Now-now<1?Now:now;return Now;
        }
    }

    /// <summary>
    /// What presentation remembers between views (allocation-free once each closure and arrival has been seen):
    ///  - the final gate's beacon cue: this client saw the gate go from sealed/sealing to opening/open (or first saw it opening),
    ///    timed from the authority's `began` when the client is in the gate's area, so everyone's beacon plays in step. An opening
    ///    seen from elsewhere (the party is inside the last building when it is cleared) is held until the client is back in the
    ///    gate's area with no barrier up, then cued ArriveDelay after that view's authority time. A gate first seen open (a resume,
    ///    a late join) cues nothing; sealing again (a new loop) drops a held cue;
    ///  - the boss intro: once per arrival, timed from its authority `spawned`; an arrival first seen more than LateJoin seconds
    ///    after its spawn is marked seen without an intro;
    ///  - the sanctuary crossings: a pilot's inside/outside flip against the sanctuary volumes starts a shimmer at the boundary.
    ///    A placement is not a crossing: while the view is loading (an area barrier is up) the sides are forgotten, and a pilot
    ///    that moved more than TeleportMetres between two samples (a placement or respawn seen late on a guest) only records
    ///    its new side.
    /// A new run forgets everything; a new loop forgets the arrivals (the director clears them per loop).
    /// </summary>
    public sealed class ExpeditionStateCues
    {
        private struct Closure{public string Id;public ExpeditionClosurePhase Last;public double CueAt;public bool Pending;}
        private struct Side{public int Volume;public float X,Z;}
        /// <summary>An arrival as first seen: its intro flag, and its immutable spawn point when the record carries one (a record
        /// from the current contract has maxHp above 0; a legacy one has neither position nor health).</summary>
        public struct Arrival{public string Area,Id,Archetype;public double Spawned;public bool Intro,Placed;public float X,Y,Z;public int Seed;}
        public struct Ripple{public int Volume;public float X,Y,Z,Along;public double At;}
        public const int RippleSlots=16;
        /// <summary>Further than this between two samples (horizontal metres) is a placement or respawn, never a walk across.</summary>
        public const float TeleportMetres=3;
        private readonly List<Closure> closures=new List<Closure>(16);
        private readonly List<Arrival> arrivals=new List<Arrival>(16);
        public readonly Ripple[] Ripples=new Ripple[RippleSlots];private int nextRipple;
        private readonly Dictionary<int,Side> inside=new Dictionary<int,Side>(32);
        private string run;private int loop=int.MinValue;private bool placing;private ExpeditionStateRig crossingRig;
        public int ArrivalCount=>arrivals.Count;
        public Arrival ArrivalAt(int i)=>arrivals[i];
        public void Reset(){closures.Clear();arrivals.Clear();inside.Clear();for(int i=0;i<Ripples.Length;i++)Ripples[i].At=double.NegativeInfinity;nextRipple=0;run=null;loop=int.MinValue;placing=false;crossingRig=null;}
        public ExpeditionStateCues(){Reset();}

        public void Observe(ExpeditionView v,double t)
        {
            if(v==null){if(run!=null)Reset();return;}
            if(v.run!=run){Reset();run=v.run;}
            if(v.loop!=loop){arrivals.Clear();loop=v.loop;}
            // An area barrier (the run loading) ends in a placement: forget the pilots' sides until it is over.
            placing=v.loading||v.areaBarrier!=null&&v.areaBarrier.active;if(placing)inside.Clear();
            var list=v.closures;
            if(list!=null)for(int i=0;i<list.Length;i++)
            {
                // Here: this client stands in the closure's area with no barrier up. The final gate opens when the last building
                // is cleared, so the party is usually inside it (another area) when it does: that opening is held (Pending) and
                // cued when the client is next here, ArriveDelay after the arrival's authority time (the transfer screen fading).
                var c=list[i];if(c?.id==null)continue;var phase=NativeExpeditionStates.Phase(c);int k=Find(c.id);bool here=c.area!=null&&c.area==v.area&&!placing;
                if(k<0){bool opening=phase==ExpeditionClosurePhase.Opening;closures.Add(new Closure{Id=c.id,Last=phase,CueAt=opening&&here?c.began:double.NaN,Pending=opening&&!here});continue;}
                var m=closures[k];
                if((m.Last==ExpeditionClosurePhase.Sealed||m.Last==ExpeditionClosurePhase.Sealing)&&(phase==ExpeditionClosurePhase.Opening||phase==ExpeditionClosurePhase.Open))
                {
                    if(here){m.CueAt=phase==ExpeditionClosurePhase.Opening?c.began:c.began-NativeExpeditionStates.TransitionSeconds;m.Pending=false;}
                    else{m.CueAt=double.NaN;m.Pending=true;}
                }
                else if(phase==ExpeditionClosurePhase.Sealed||phase==ExpeditionClosurePhase.Sealing)m.Pending=false;
                if(m.Pending&&here){m.CueAt=v.elapsed+NativeExpeditionStates.ArriveDelay;m.Pending=false;}
                m.Last=phase;closures[k]=m;
            }
            var boss=v.bossArrivals;
            if(boss!=null)for(int i=0;i<boss.Length;i++)
            {
                var a=boss[i];if(a?.id==null||a.area==null||FindArrival(a.area,a.id)>=0)continue;
                // First seen: an intro only while it is still on (a late joiner past LateJoin sees the HUD plate alone).
                bool placed=a.maxHp>0&&Finite(a.x)&&Finite(a.y)&&Finite(a.z);
                arrivals.Add(new Arrival{Area=a.area,Id=a.id,Archetype=a.archetype,Spawned=a.spawned,Intro=t-a.spawned<=NativeExpeditionStates.LateJoin,
                    Placed=placed,X=a.x,Y=a.y,Z=a.z,Seed=NativeExpeditionStates.StableHash(a.id,NativeExpeditionStates.StableHash(a.area))});
            }
        }
        private static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        private int Find(string id){for(int i=0;i<closures.Count;i++)if(closures[i].Id==id)return i;return -1;}
        private int FindArrival(string area,string id){for(int i=0;i<arrivals.Count;i++)if(arrivals[i].Id==id&&arrivals[i].Area==area)return i;return -1;}
        /// <summary>The age of closure `id`'s opening cue at t, when one is due (0..CueSeconds); false otherwise.</summary>
        public bool Cue(string id,double t,out double age)
        {
            age=0;int k=id==null?-1:Find(id);if(k<0||double.IsNaN(closures[k].CueAt))return false;
            age=t-closures[k].CueAt;return age>=0&&age<=NativeExpeditionStates.CueSeconds;
        }
        /// <summary>The boss intro playing at t in `area` (the latest arrival still within IntroSeconds of its spawn), or -1.</summary>
        public int Intro(string area,double t,out double age)
        {
            age=0;int best=-1;
            for(int i=0;i<arrivals.Count;i++)
            {
                var a=arrivals[i];if(!a.Intro||a.Area!=area)continue;double g=t-a.Spawned-NativeZoneBossNames.BannerDelay(a.Archetype);
                if(g>=0&&g<NativeExpeditionStates.IntroSeconds&&(best<0||a.Spawned>=arrivals[best].Spawned)){best=i;age=g;}
            }
            return best;
        }
        /// <summary>
        /// A pilot's position this frame (world metres) against the sanctuary volumes of `rig`: a flip of inside/outside starts a
        /// shimmer at the nearest boundary point. Purely visual: it says where the boundary is, never that the pilot is safe
        /// (the SAFE tags read members[].safe only). The first sight of a pilot only records its side, and so does a sample during
        /// an area barrier (forgotten) or one more than TeleportMetres from the last (a placement or respawn).
        /// </summary>
        public void Pilot(int id,float x,float y,float z,ExpeditionStateRig rig,double t)
        {
            if(rig==null||rig.Sanctuary.Length==0||placing)return;if(!ReferenceEquals(rig,crossingRig)){inside.Clear();crossingRig=rig;}
            int now=-1;for(int i=0;i<rig.Sanctuary.Length;i++)if(rig.Sanctuary[i].Contains(new V3(x,y,z))){now=i;break;}
            if(!inside.TryGetValue(id,out var last)){inside[id]=new Side{Volume=now,X=x,Z=z};return;}
            float jx=x-last.X,jz=z-last.Z;bool jump=!(jx*jx+jz*jz<=TeleportMetres*TeleportMetres);inside[id]=new Side{Volume=now,X=x,Z=z};
            int was=last.Volume;if(was==now||jump)return;int vi=now>=0?now:was;
            ExpeditionStateRig.Boundary(rig.Sanctuary[vi],x,z,out float bx,out float bz,out float along);
            Ripples[nextRipple]=new Ripple{Volume=vi,X=bx,Y=rig.Sanctuary[vi].y-rig.Sanctuary[vi].sy/2,Z=bz,Along=along,At=t};nextRipple=(nextRipple+1)%Ripples.Length;
        }
        /// <summary>Forget the pilots' sides (a new map or a respawn elsewhere must not read as a crossing).</summary>
        public void ForgetPilots(){inside.Clear();}
    }

    /// <summary>A door or gate closure's frame: the floor centre of its volume, the width axis and the side the party stands on.</summary>
    public struct ExpeditionFrame
    {
        /// <summary>Floor centre (the volume's base), the width axis (volume x) and toward-the-party axis (± volume z), unit.</summary>
        public float Cx,Cy,Cz,Ux,Uz,Fx,Fz;
        /// <summary>Full width, height and depth of the closure volume; its yaw (degrees, the volume's).</summary>
        public float W,H,D,Yaw;
        public void Point(float u,float v,float d,out float x,out float y,out float z){x=Cx+Ux*u+Fx*d;y=Cy+v;z=Cz+Uz*u+Fz*d;}
        /// <summary>The frame of a closure volume, facing the side that holds `toward` (fallback `fx`,`fz` when on the plane).</summary>
        public static ExpeditionFrame Of(ExpeditionVolume v,float towardX,float towardZ,float fx,float fz)
        {
            double a=v.yaw*Math.PI/180;float ux=(float)Math.Cos(a),uz=-(float)Math.Sin(a),zx=(float)Math.Sin(a),zz=(float)Math.Cos(a);
            float side=(towardX-v.x)*zx+(towardZ-v.z)*zz;float s=Math.Abs(side)>1e-4f?Math.Sign(side):fx*zx+fz*zz>=0?1:-1;
            return new ExpeditionFrame{Cx=v.x,Cy=v.y-v.sy/2,Cz=v.z,Ux=ux,Uz=uz,Fx=zx*s,Fz=zz*s,W=v.sx,H=v.sy,D=v.sz,Yaw=v.yaw};
        }
    }
    public enum ExpeditionDoorLook { Biocontainment, Heavy }
    public sealed class ExpeditionDoorRig
    {
        public string Anchor,Building;public bool Exit;public ExpeditionDoorLook Look;public ExpeditionFrame Frame;
    }
    public sealed class ExpeditionGateRig {public string Id;public ExpeditionFrame Frame;}

    /// <summary>
    /// One area's state pieces from the layout manifest: each door's closure frame (the doorway plane, facing the party: the
    /// approach's side), the final gate's frame (facing the sanctuary) and the sanctuary volumes. Built once per layout and area;
    /// the closure volumes are the colliders' own, so leaves and seals cover exactly what blocks.
    /// </summary>
    public sealed class ExpeditionStateRig
    {
        public string Area,Map;public bool World;
        public ExpeditionDoorRig[] Doors=Array.Empty<ExpeditionDoorRig>();public ExpeditionGateRig[] Gates=Array.Empty<ExpeditionGateRig>();
        public ExpeditionVolume[] Sanctuary=Array.Empty<ExpeditionVolume>();
        public static ExpeditionStateRig For(ExpeditionLayout layout,string area)
        {
            var a=layout?.Area(area);if(a==null)return null;
            var r=new ExpeditionStateRig{Area=a.id,Map=a.map,World=a.id==layout.world,Sanctuary=a.sanctuary??Array.Empty<ExpeditionVolume>()};
            var doors=new List<ExpeditionDoorRig>();
            foreach(var d in a.doors??Array.Empty<ExpeditionDoorLayout>())
            {
                ExpeditionWorldAnchor anchor=null;foreach(var x in a.anchors)if(x.id==d.anchor)anchor=x;if(anchor==null||d.closure==null)continue;
                double yaw=anchor.yaw*Math.PI/180;float ox=(float)Math.Sin(yaw),oz=(float)Math.Cos(yaw);bool exit=!r.World;
                // The party's side: the approach; on the plane, out of the doorway for a town door and in for an instance exit.
                float tx=d.approach!=null?d.approach.x:anchor.x,tz=d.approach!=null?d.approach.z:anchor.z;
                doors.Add(new ExpeditionDoorRig{Anchor=d.anchor,Building=anchor.building,Exit=exit,Look=anchor.building=="exp-ward-zero"?ExpeditionDoorLook.Biocontainment:ExpeditionDoorLook.Heavy,
                    Frame=ExpeditionFrame.Of(d.closure,tx,tz,exit?-ox:ox,exit?-oz:oz)});
            }
            r.Doors=doors.ToArray();
            var gates=new List<ExpeditionGateRig>();
            foreach(var g in a.gates??Array.Empty<ExpeditionGateLayout>())
            {
                if(g?.closure==null)continue;float tx=0,tz=0;
                if(a.slots!=null&&a.slots.Length>0){tx=a.slots[0].x;tz=a.slots[0].z;}else if(r.Sanctuary.Length>0){tx=r.Sanctuary[0].x;tz=r.Sanctuary[0].z;}
                float fx=0,fz=-1;if(a.bossTrigger!=null){fx=g.closure.x-a.bossTrigger.x;fz=g.closure.z-a.bossTrigger.z;}
                gates.Add(new ExpeditionGateRig{Id=g.id,Frame=ExpeditionFrame.Of(g.closure,tx,tz,fx,fz)});
            }
            r.Gates=gates.ToArray();return r;
        }
        public int Door(string anchor){if(anchor==null)return -1;for(int i=0;i<Doors.Length;i++)if(Doors[i].Anchor==anchor)return i;return -1;}
        public int Gate(string id){if(id==null)return -1;for(int i=0;i<Gates.Length;i++)if(Gates[i].Id==id)return i;return -1;}
        /// <summary>The perimeter length of a sanctuary volume's footprint.</summary>
        public static float Perimeter(ExpeditionVolume v)=>2*(v.sx+v.sz);
        /// <summary>The perimeter point at distance `along` (from the volume's -x,-z corner, counter-clockwise seen from above) and
        /// its inward normal.</summary>
        public static void Along(ExpeditionVolume v,float along,out float x,out float z,out float nx,out float nz)
        {
            float p=Perimeter(v);along=((along%p)+p)%p;float hx=v.sx/2,hz=v.sz/2,lx,lz,ix,iz;
            if(along<v.sx){lx=-hx+along;lz=-hz;ix=0;iz=1;}
            else if(along<v.sx+v.sz){lx=hx;lz=-hz+(along-v.sx);ix=-1;iz=0;}
            else if(along<2*v.sx+v.sz){lx=hx-(along-v.sx-v.sz);lz=hz;ix=0;iz=-1;}
            else{lx=-hx;lz=hz-(along-2*v.sx-v.sz);ix=1;iz=0;}
            double a=v.yaw*Math.PI/180;float c=(float)Math.Cos(a),s=(float)Math.Sin(a);
            // Volume local x is (cos, -sin) in world x/z, local z is (sin, cos) (ExpeditionVolume.Contains).
            x=v.x+lx*c+lz*s;z=v.z-lx*s+lz*c;nx=ix*c+iz*s;nz=-ix*s+iz*c;
        }
        /// <summary>The nearest perimeter point of a footprint to (x, z) and its distance along the perimeter.</summary>
        public static void Boundary(ExpeditionVolume v,float x,float z,out float bx,out float bz,out float along)
        {
            double a=v.yaw*Math.PI/180;float c=(float)Math.Cos(a),s=(float)Math.Sin(a),dx=x-v.x,dz=z-v.z;
            float lx=c*dx-s*dz,lz=s*dx+c*dz,hx=v.sx/2,hz=v.sz/2;lx=Math.Max(-hx,Math.Min(hx,lx));lz=Math.Max(-hz,Math.Min(hz,lz));
            // Snap to the nearest edge.
            float toL=lx+hx,toR=hx-lx,toB=lz+hz,toT=hz-lz,m=Math.Min(Math.Min(toL,toR),Math.Min(toB,toT));
            if(m==toB){lz=-hz;along=lx+hx;}else if(m==toR){lx=hx;along=v.sx+lz+hz;}else if(m==toT){lz=hz;along=v.sx+v.sz+(hx-lx);}else{lx=-hx;along=2*v.sx+v.sz+(hz-lz);}
            bx=v.x+lx*c+lz*s;bz=v.z-lx*s+lz*c;
        }
    }

    /// <summary>The area transfer screen (areaBarrier while active): the entry by target, the party's ready / required, the pips.</summary>
    public sealed class ExpeditionTransferModel
    {
        public const string Back="RETURNING TO THE CITY",First="ENTERING THE CITY",Loop="STARTING LOOP ",Eyebrow="AREA TRANSFER · LOADING",Waiting="WAITING FOR THE PARTY",Paused="THE RUN CLOCK IS PAUSED WHILE THE PARTY LOADS";
        public bool Active;public string Title,Ready,Detail;public int ReadyCount,Required;public float Fill;
        private readonly Dictionary<string,string> titles=new Dictionary<string,string>(StringComparer.Ordinal);
        /// <summary>Reads the barrier: an instance target is ENTERING its name, the world RETURNING TO THE CITY once a building was
        /// entered this loop, STARTING LOOP n for a later loop's fresh barrier (Continue: the city reloads into itself and the
        /// director resets every building's `entered`), else ENTERING THE CITY (the run's first load). Counts are the authority's,
        /// clamped for display only.</summary>
        public void From(ExpeditionView v,ExpeditionLabels labels)
        {
            var b=v?.areaBarrier;Active=b!=null&&b.active;if(!Active)return;
            bool world=b.target!=null&&b.target==v.world;bool entered=false;foreach(var i in v.instances)if(i!=null&&i.entered)entered=true;
            if(world)Title=entered?Back:v.loop>0?labels.Int(Loop,v.loop+1):First;
            else{string key=b.target??"";if(!titles.TryGetValue(key,out var s)){if(titles.Count>64)titles.Clear();s=titles[key]="ENTERING "+(b.target==null?"THE NEXT AREA":NativeExpeditionStates.AreaName(b.target));}Title=s;}
            Required=Math.Max(0,b.required);ReadyCount=Math.Max(0,Math.Min(b.ready,Required));Fill=Required>0?ReadyCount/(float)Required:0;
            Ready=Required>0?labels.Pair("PARTY READY ",ReadyCount,Required):Waiting;Detail=v.loading?Paused:null;
        }
    }
    /// <summary>The transfer screen's fade: in over FadeIn while the barrier is up, held until the map the area needs is loaded,
    /// then out over FadeOut (never held more than HoldMax after the barrier drops).</summary>
    public sealed class ExpeditionTransferFade
    {
        public const float FadeIn=.25f,FadeOut=.45f,HoldMax=3;
        public float Alpha{get;private set;}private float held;
        public float Step(bool active,bool mapReady,float dt)
        {
            if(!(dt>=0))dt=0;
            if(active){held=0;Alpha=Math.Min(1,Alpha+dt/FadeIn);return Alpha;}
            if(Alpha<=0)return Alpha=0;held+=dt;
            if(mapReady||held>=HoldMax)Alpha=Math.Max(0,Alpha-dt/FadeOut);return Alpha;
        }
        public void Reset(){Alpha=0;held=0;}
    }

    /// <summary>The local pilot's SAFE chip and the party's safe members, from members[].safe and mine.safe only (the authority's
    /// sanctuary membership: checkpoint, extraction and remote reprint; not damage protection, and never from a position).</summary>
    public sealed class ExpeditionSafeModel
    {
        public const string Chip="SAFE · SANCTUARY";
        public bool Shown;public int Safe,Party;public string Line,Names;
        public void From(ExpeditionView v,ExpeditionLabels labels)
        {
            Shown=false;Safe=Party=0;Line=Names=null;if(v==null)return;
            var me=v.mine;bool member=NativeExpeditionPresentation.Has(me?.account)&&me.account==v.viewer;
            int shownNames=0,more=0;string names=null;
            foreach(var m in v.members)
            {
                if(m==null||!m.connected||m.extracted)continue;Party++;if(!m.safe)continue;Safe++;
                if(m.account==v.viewer)continue;if(shownNames<3){string n=labels.Who(string.IsNullOrEmpty(m.name)?m.account:m.name);names=names==null?n:labels.Join(labels.Join(names,", "),n);shownNames++;}else more++;
            }
            Shown=member&&me.safe&&me.connected&&!me.extracted;
            if(Safe>0){Line=labels.Pair("PARTY ",Safe,Party," SAFE");Names=names==null?null:more>0?labels.Join(names,labels.Int(" +",more)):names;}
        }
    }

    /// <summary>The SAFE tags over the pilots: the seat (members[].seat, the authority's account-to-pilot mapping) of every
    /// connected, living, not extracted member the authority has in the sanctuary (members[].safe). Seat 0 is unbound: no tag.
    /// Public state, the same for every viewer.</summary>
    public sealed class ExpeditionSafeTags
    {
        public const int Max=16;public const string Text="SAFE";
        /// <summary>Tags show within Range metres of the camera, fading out from FadeFrom.</summary>
        public const float Range=55,FadeFrom=35;
        public readonly int[] Seats=new int[Max];public int Count;
        /// <summary>A tag's opacity at `metres` from the camera: none past Range (or not finite), fading from FadeFrom, and none over
        /// my own pilot in first person. Occlusion (walls between the camera and the tag) is the caller's ray, as the health bars'.</summary>
        public static float Alpha(float metres,bool own,bool firstPerson)
        {
            if(own&&firstPerson||!(metres>=0&&metres<=Range))return 0;
            return Math.Min(1,Math.Max(0,1-(metres-FadeFrom)/(Range-FadeFrom)));
        }
        public void From(ExpeditionView v)
        {
            Count=0;if(v?.members==null)return;
            foreach(var m in v.members)if(m!=null&&m.seat>0&&m.safe&&m.connected&&m.alive&&!m.extracted&&Count<Max&&!Has(m.seat))Seats[Count++]=m.seat;
        }
        public bool Has(int seat){for(int i=0;i<Count;i++)if(Seats[i]==seat)return true;return false;}
    }

    /// <summary>The boss intro banner and the HUD boss plates for the current area (archetype names keep TEST), with the boss's
    /// health from bossArrivals[].hp / maxHp (a legacy record with maxHp 0 has none: no bar).</summary>
    public sealed class ExpeditionBossModel
    {
        public const int Plates=3;public const string Eyebrow="BOSS ARRIVAL";
        public int Intro=-1;public double Age;public string Name,Area;
        public readonly string[] PlateNames=new string[Plates];public readonly bool[] PlateDown=new bool[Plates];public readonly double[] PlateAge=new double[Plates];public int PlateCount;
        /// <summary>Each plate's health fraction (0..1), or -1 without health; its percentage label (cached), or null.</summary>
        public readonly float[] PlateHealth=new float[Plates];public readonly string[] PlatePercent=new string[Plates];
        private readonly Dictionary<string,string> names=new Dictionary<string,string>(StringComparer.Ordinal),areas=new Dictionary<string,string>(StringComparer.Ordinal);
        private string NameOf(string archetype){string k=archetype??"";if(!names.TryGetValue(k,out var s)){if(names.Count>128)names.Clear();s=names[k]=NativeExpeditionStates.BossName(archetype);}return s;}
        private string AreaOf(string area){string k=area??"";if(!areas.TryGetValue(k,out var s)){if(areas.Count>32)areas.Clear();s=areas[k]=Eyebrow+" · "+NativeExpeditionStates.AreaName(area);}return s;}
        public void From(ExpeditionView v,ExpeditionStateCues cues,double t,ExpeditionLabels labels=null)
        {
            Intro=-1;Name=Area=null;PlateCount=0;if(v==null||cues==null)return;
            int i=cues.Intro(v.area,t,out double age);if(i>=0){var a=cues.ArrivalAt(i);Intro=i;Age=age;Name=NameOf(a.Archetype);Area=AreaOf(a.Area);}
            for(int k=0;k<cues.ArrivalCount&&PlateCount<Plates;k++)
            {
                var a=cues.ArrivalAt(k);if(a.Area!=v.area)continue;var live=Live(v,a.Area,a.Id);
                float health=live!=null&&live.maxHp>0&&live.hp>=0?(float)Math.Min(1,live.hp/live.maxHp):-1;bool down=Down(v,a.Area,a.Id)||health==0;
                PlateNames[PlateCount]=NameOf(a.Archetype);PlateDown[PlateCount]=down;PlateAge[PlateCount]=a.Intro?t-a.Spawned-NativeZoneBossNames.BannerDelay(a.Archetype):double.PositiveInfinity;
                PlateHealth[PlateCount]=health;PlatePercent[PlateCount]=health<0||labels==null?null:labels.Int("",(int)Math.Ceiling(health*100-1e-6),"%");PlateCount++;
            }
        }
        /// <summary>The view's current record of an arrival (its live health), or null.</summary>
        public static ExpeditionBossArrivalView Live(ExpeditionView v,string area,string id)
        {var list=v?.bossArrivals;if(list!=null)for(int i=0;i<list.Length;i++){var a=list[i];if(a!=null&&a.id==id&&a.area==area)return a;}return null;}
        /// <summary>Whether an arrived boss is down in the view: its building lists it defeated (or is cleared); the final boss once
        /// the run has left the final-boss phase.</summary>
        public static bool Down(ExpeditionView v,string area,string id)
        {
            foreach(var b in v.instances)if(b!=null&&b.id==area){if(b.cleared)return true;foreach(var d in b.defeated)if(d==id)return true;return false;}
            return v.phase!=ExpeditionPhase.FinalBoss;
        }
    }
}
