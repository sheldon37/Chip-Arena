using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>What a bow arrow did to the pilot it hit: stopped in it, or went on through it.</summary>
    public enum ArrowFate : byte { Lodge, Pass }

    /// <summary>A bow arrow in flight as the presentation sees it now (the pass-through fallback's input for hits from older
    /// frames that carry no projectile id): its id, its shooter, the pierces it may still spend, when the data is from and
    /// where it is, how fast it goes and how hard it falls (Unity world).</summary>
    public struct ArrowShot { public int Id,Owner,Pierces;public float At,Gravity;public Vector3 Position,Velocity; }

    /// <summary>
    /// Arrow gibbing (owner 2026-10-14: "Add gibbing from arrows (Can pierce through enemies if player has pierce modifiers,
    /// can go in body/head and stop halfway) Blood coming out of arrow gibbing points"). Engine free, deterministic,
    /// presentation only; no hit, damage, timing, hitbox or network rule reads it.
    /// <list type="bullet">
    /// <item>Lodge or pass (<see cref="Fate"/>): a hit's own replicated flag decides (ArenaEvent.Pierced, with its projectile
    /// id). A hit from an older frame (no id) passes only when an arrow of the same shooter is seen past the target on the
    /// hit's line and its pierce chips allow it; in doubt it lodges.</item>
    /// <item>Halfway (<see cref="Lodge"/>): an arrow that stops buries to about the middle of the struck part along its
    /// flight (the joint's body boxes, CapturedRig.Boxes), a seeded share either side; a head shot stops about halfway into
    /// the head.</item>
    /// <item>Through (<see cref="Through"/>): no arrow stays; the entry and the exit (the far side of the part along the
    /// flight) both bleed, the exit with a spray along the flight.</item>
    /// <item>Blood (<see cref="Drips"/>): a lodged arrow's entry drips a short, decaying trickle riding its joint; at most
    /// <see cref="MaxBleeds"/> bleed at once (<see cref="Bleeds"/>). The wound's own spray is the existing wound path's.</item>
    /// </list>
    /// </summary>
    public static class NativeArrowGib
    {
        /// <summary>A lodged arrow's depth: this share of the part's thickness along the flight, a seeded share either side,
        /// within these bounds (m).</summary>
        public const float DepthShare=.5f,DepthJitter=.1f,MinDepth=.045f,MaxDepth=.24f;
        /// <summary>The older-frame fallback: how far (m) a live arrow's line may pass from the hit and still be its arrow,
        /// how far behind it the arrow may already stand, and how far past it must be to have gone through.</summary>
        public const float MatchRadius=.3f,MatchBehind=.3f,PassBeyond=.04f;
        /// <summary>A lodged arrow's trickle: how long it drips (s), its first rate (drops/s) and how fast it eases (s).</summary>
        public const float DripLife=6.5f,DripRate=4.5f,DripFade=1.5f;
        /// <summary>How many arrow points bleed at once (the low graphics tier and reduced effects: fewer).</summary>
        public const int MaxBleeds=12,LowBleeds=5;
        private static Vector3 Unit(Vector3 v)=>CapturedRig.Unit(v,Vector3.UnitZ);
        private static float H(uint seed,int i,int k)=>(float)NativeWoundCarve.Hash(seed,i,k);

        // ================================================================================================ where it sits
        /// <summary>
        /// The flight's span through the struck joint's body boxes (CapturedRig.Boxes on `bone`; `local` and `dir` in that joint's
        /// Unity frame, where a box sits at its source place mirrored in x): where the line first enters the boxes and where it
        /// leaves the span of boxes joined to that entry, in metres along `dir` from `local`. False when it misses them.
        /// </summary>
        public static bool Extent(int bone,Vector3 local,Vector3 dir,out float enter,out float exit)
        {
            enter=exit=0;dir=Unit(dir);Span<float> t0=stackalloc float[16],t1=stackalloc float[16];int n=0;
            foreach(var box in CapturedRig.Boxes)
            {
                if(box.Bone!=bone||n==16)continue;var c=CapturedRig.Mirror(box.Position);var half=box.Size*.5f;float a=float.NegativeInfinity,b=float.PositiveInfinity;bool miss=false;
                for(int axis=0;axis<3&&!miss;axis++)
                {
                    float o=axis==0?local.X-c.X:axis==1?local.Y-c.Y:local.Z-c.Z,d=axis==0?dir.X:axis==1?dir.Y:dir.Z,e=axis==0?half.X:axis==1?half.Y:half.Z;
                    if(Math.Abs(d)<1e-7f){if(Math.Abs(o)>e)miss=true;continue;}
                    float near=(-e-o)/d,far=(e-o)/d;if(near>far){float s=near;near=far;far=s;}a=Math.Max(a,near);b=Math.Min(b,far);if(a>b)miss=true;
                }
                if(miss||b<0)continue;
                // Insertion by entry.
                int at=n++;while(at>0&&t0[at-1]>a){t0[at]=t0[at-1];t1[at]=t1[at-1];at--;}t0[at]=a;t1[at]=b;
            }
            if(n==0)return false;enter=t0[0];exit=t1[0];
            for(int i=1;i<n;i++)if(t0[i]<=exit+.03f)exit=Math.Max(exit,t1[i]);
            return exit>enter;
        }
        /// <summary>
        /// Where an arrow that stopped sits (joint frame, as <see cref="Extent"/>): `entry` on the part's surface along the flight
        /// and `depth` (m) the head is buried past it, about the middle of the part (<see cref="DepthShare"/>, a seeded
        /// <see cref="DepthJitter"/> either side). False (the boxes missed): the hit point and the old fixed bury.
        /// </summary>
        public static bool Lodge(int bone,Vector3 local,Vector3 dir,uint seed,out Vector3 entry,out float depth)
        {
            dir=Unit(dir);local=Onto(bone,local,dir);if(!Extent(bone,local,dir,out float enter,out float exit)){entry=local;depth=NativeBowStuck.Bury;return false;}
            depth=Bury(exit-enter,seed);entry=local+dir*enter;return true;
        }
        /// <summary>How far (m) the head of an arrow that stopped is buried in a part `thick` m through along its flight: about
        /// its middle (<see cref="DepthShare"/>, a seeded <see cref="DepthJitter"/> either side), within
        /// <see cref="MinDepth"/> and <see cref="MaxDepth"/> and short of the far side.</summary>
        public static float Bury(float thick,uint seed)
        {
            float share=DepthShare+DepthJitter*(2*H(seed,0,1)-1);return Math.Max(MinDepth,Math.Min(Math.Min(MaxDepth,thick-.02f),thick*share));
        }
        /// <summary>Where an arrow that went through entered and left the struck part (joint frame); false when the boxes miss
        /// (both at the hit point).</summary>
        public static bool Through(int bone,Vector3 local,Vector3 dir,out Vector3 entry,out Vector3 exit)
        {
            dir=Unit(dir);local=Onto(bone,local,dir);if(!Extent(bone,local,dir,out float enter,out float leave)){entry=exit=local;return false;}
            entry=local+dir*enter;exit=local+dir*leave;return true;
        }
        /// <summary>How far beside the struck joint's boxes (m) a flight may pass and still be moved onto them (the game's collider
        /// is wider than the body; owner 2026-11-18: "they sometimes 'attach' off of a players body"), and how far inside the
        /// nearest box's surface the moved flight then passes.</summary>
        public const float BoxMiss=.48f,BoxInside=.012f;
        /// <summary>
        /// The flight through `local` along `dir` (joint frame), moved sideways onto joint `bone`'s boxes when it misses them: through
        /// the point of the boxes nearest the line, <see cref="BoxInside"/> past their surface. A flight that meets them, or lies
        /// farther than <see cref="BoxMiss"/> from all of them, is kept.
        /// </summary>
        public static Vector3 Onto(int bone,Vector3 local,Vector3 dir)
        {
            dir=Unit(dir);if(Extent(bone,local,dir,out _,out _))return local;float best=BoxMiss*BoxMiss;Vector3 moved=local;
            foreach(var box in CapturedRig.Boxes)
            {
                if(box.Bone!=bone)continue;var c=CapturedRig.Mirror(box.Position);var half=box.Size*.5f;
                // The line's point nearest the box's middle, then the box's point nearest that, then the line's nearest that (twice).
                var p=local+dir*Vector3.Dot(c-local,dir);Vector3 q=p;
                for(int k=0;k<3;k++){q=Vector3.Clamp(p,c-half,c+half);p=local+dir*Vector3.Dot(q-local,dir);}
                var off=q-p;float d=off.LengthSquared();if(d>=best)continue;best=d;
                // Into the box past its surface (towards its middle when the line already touches it).
                var inward=d>1e-10f?off/(float)Math.Sqrt(d):Unit(c-p);moved=local+off+inward*Math.Min(BoxInside,.5f*Math.Min(half.X,Math.Min(half.Y,half.Z)));
            }
            return moved;
        }

        // ================================================================================================ where it sits on the model
        /// <summary>Where a pilot's arrow met its drawn model (<see cref="Seat"/>): whether found, whether the flight was moved onto
        /// the body (it passed beside the skin: the game's collider is wider), the entry on the skin and the struck part's far side
        /// along the flight (world), the part's thickness there and the body bone (the caller's index) the entry's skin rides.</summary>
        public struct ModelSeat{public bool Found,Moved;public Vector3 Entry,Exit;public int Bone;public float Thick;}
        /// <summary>How far before and past the replicated hit point the flight is cast against the skin (m); how far beside it the
        /// body may lie and the flight still be moved onto it (the collider's radius and some); how far inside the skin the moved
        /// flight passes; how far past the entry the part's far side is looked for, and the thickness taken without one.</summary>
        public const float SeatBack=.9f,SeatAhead=.9f,SeatMiss=.48f,SeatInside=.012f,SeatReach=.6f,SeatThick=.12f;
        /// <summary>
        /// Where an arrow that struck a pilot at `hit` along `dir` (world) sits on its drawn model, the skin as posed: `at` its
        /// vertices (world; the first `vertices`), `tris` its triangles (the first `triangles`), `bone` each vertex's body bone
        /// (the caller's index; -1 none) and `region` each bone's body (NativeBisect.Anchor of the game joint at or above it).
        /// The flight is cast from <see cref="SeatBack"/> before the hit: the first face it meets is the entry, riding the bone of
        /// that face's vertex nearest it. A flight that misses the skin (the hit lies on the game's capsule, outside the model) is
        /// moved sideways through the body's vertex nearest the line (within <see cref="SeatMiss"/>), <see cref="SeatInside"/>
        /// past it, and cast again. A head shot (`head`) looks on the head first. The far side: the farthest face of the entry's
        /// body along the flight within <see cref="SeatReach"/>. Engine free, allocation free; run off the main thread.
        /// </summary>
        public static ModelSeat Seat(Vector3[] at,int vertices,int[] tris,int triangles,int[] bone,int[] region,Vector3 hit,Vector3 dir,bool head)
        {
            var s=new ModelSeat{Bone=-1};if(at==null||tris==null||bone==null||region==null)return s;dir=Unit(dir);
            vertices=Math.Min(vertices,Math.Min(at.Length,bone.Length));triangles=Math.Min(triangles,tris.Length/3);
            if(head&&SeatOn(at,vertices,tris,triangles,bone,region,hit,dir,CapturedRig.Head,ref s))return s;
            SeatOn(at,vertices,tris,triangles,bone,region,hit,dir,-1,ref s);return s;
        }
        private static bool SeatOn(Vector3[] at,int vertices,int[] tris,int triangles,int[] bone,int[] region,Vector3 hit,Vector3 dir,int only,ref ModelSeat s)
        {
            var from=hit-dir*SeatBack;int tri=Cast(at,vertices,tris,triangles,bone,region,from,dir,SeatBack+SeatAhead,false,only,out float t);bool moved=false;
            if(tri<0)
            {
                int q=-1;float best=SeatMiss*SeatMiss;var off=Vector3.Zero;
                for(int v=0;v<vertices;v++)
                {
                    int b=bone[v];if(b<0||b>=region.Length||only>=0&&region[b]!=only)continue;var rel=at[v]-hit;float along=Vector3.Dot(rel,dir);
                    if(along< -SeatBack||along>SeatAhead)continue;var perp=rel-dir*along;float d=perp.LengthSquared();if(d<best){best=d;q=v;off=perp;}
                }
                if(q<0)return false;float l=off.Length();moved=true;from=hit+(l>1e-5f?off*(1+SeatInside/l):Vector3.Zero)-dir*SeatBack;
                tri=Cast(at,vertices,tris,triangles,bone,region,from,dir,SeatBack+SeatAhead,false,only,out t);
                if(tri<0){s=new ModelSeat{Found=true,Moved=true,Entry=at[q],Exit=at[q]+dir*SeatThick,Bone=bone[q],Thick=SeatThick};return true;}
            }
            var entry=from+dir*t;int on=-1;float near=float.MaxValue;
            for(int k=0;k<3;k++){int v=tris[tri*3+k];int b=v<vertices?bone[v]:-1;if(b<0||b>=region.Length)continue;float d=Vector3.DistanceSquared(at[v],entry);if(d<near){near=d;on=b;}}
            if(on<0)return false;
            float thick=Cast(at,vertices,tris,triangles,bone,region,entry+dir*.001f,dir,SeatReach,true,region[on],out float far)>=0?far+.001f:SeatThick;
            s=new ModelSeat{Found=true,Moved=moved,Entry=entry,Exit=entry+dir*thick,Bone=on,Thick=thick};return true;
        }
        // The nearest (or farthest) triangle a ray from `from` along `dir` meets within `reach` (its index, -1 none; `t` how far),
        // both sides, over the triangles with a vertex on a bone (in body `only` when it is not -1).
        private static int Cast(Vector3[] at,int vertices,int[] tris,int triangles,int[] bone,int[] region,Vector3 from,Vector3 dir,float reach,bool farthest,int only,out float t)
        {
            int hit=-1;t=0;
            for(int i=0;i<triangles;i++)
            {
                int ia=tris[i*3],ib=tris[i*3+1],ic=tris[i*3+2];if(ia>=vertices||ib>=vertices||ic>=vertices)continue;
                int ba=bone[ia],bb=bone[ib],bc=bone[ic];if(ba<0&&bb<0&&bc<0)continue;
                if(only>=0&&!(ba>=0&&ba<region.Length&&region[ba]==only||bb>=0&&bb<region.Length&&region[bb]==only||bc>=0&&bc<region.Length&&region[bc]==only))continue;
                Vector3 a=at[ia],ab=at[ib]-a,ac=at[ic]-a;var h=Vector3.Cross(dir,ac);float det=Vector3.Dot(ab,h);if(Math.Abs(det)<1e-12f)continue;float inv=1/det;
                var o=from-a;float u=Vector3.Dot(o,h)*inv;if(u<0||u>1)continue;var r=Vector3.Cross(o,ab);float v=Vector3.Dot(dir,r)*inv;if(v<0||u+v>1)continue;
                float d=Vector3.Dot(ac,r)*inv;if(d<=0||d>reach)continue;if(hit<0||(farthest?d>t:d<t)){hit=i;t=d;}
            }
            return hit;
        }

        // ================================================================================================ when and where on the drawn body
        /// <summary>How far ahead of the drawn pose (s) a guest's hit may be and still be placed now; and the most (s) a hit's
        /// place is carried along its target's velocity to the drawn pose's moment.</summary>
        public const float DueSlack=.004f,CarryMost=.25f;
        /// <summary>Whether a hit at `hitAt` is placed on a body drawn at `poseAt` now: a guest draws others about a tenth of a second
        /// behind its newest frame, whose hits land ahead of the drawn pose (the arrow is still in flight there), so the arrow waits
        /// for the pose to reach its hit; `waits` false (the host, a replay, a guest's own predicted pilot, a dead target) places
        /// it at once.</summary>
        public static bool Due(float hitAt,float poseAt,bool waits)=>!waits||poseAt>=hitAt-DueSlack;
        /// <summary>The replicated hit point `hit` moved onto the body as drawn: by the drawn body's offset from its replicated
        /// place (`drawnOffset`: smoothing, a technique's or spawn's shift) and by its velocity over the time from the hit to the
        /// drawn pose's moment (at most <see cref="CarryMost"/> either way).</summary>
        public static Vector3 OnDrawn(Vector3 hit,Vector3 drawnOffset,Vector3 velocity,float hitAt,float poseAt)
        {
            float dt=poseAt-hitAt;if(!(dt>-CarryMost))dt=-CarryMost;if(!(dt<CarryMost))dt=CarryMost;
            var v=float.IsNaN(velocity.X+velocity.Y+velocity.Z)?Vector3.Zero:velocity;var o=float.IsNaN(drawnOffset.X+drawnOffset.Y+drawnOffset.Z)?Vector3.Zero:drawnOffset;return hit+o+v*dt;
        }

        // ================================================================================================ through or not
        /// <summary>
        /// A bow hit's fate. With its projectile id (every hit from a current host) its own flag decides: `pierced` true is a
        /// pass-through, false a stop. Without one (an older frame) the fallback: the hit's arrow is the shooter's live arrow
        /// whose line passes nearest the hit point (<see cref="Match"/>); it went through only when that arrow is already past
        /// the hit and had a pierce left (<see cref="ArrowShot.Pierces"/>, its chips' pierces less the ones it spent); in doubt it
        /// lodges. `shot`: the arrow's id when known (0 none).
        /// </summary>
        public static ArrowFate Fate(int projectile,bool pierced,int source,Vector3 hit,float hitAt,ArrowShot[] shots,int count,out int shot)
        {
            shot=projectile;if(projectile>0)return pierced?ArrowFate.Pass:ArrowFate.Lodge;
            int i=Match(source,hit,hitAt,shots,count,out float along);if(i<0)return ArrowFate.Lodge;shot=shots[i].Id;
            return shots[i].Pierces>0&&along>=PassBeyond?ArrowFate.Pass:ArrowFate.Lodge;
        }
        /// <summary>The index of the shooter's live arrow whose path passes nearest the hit (within <see cref="MatchRadius"/>,
        /// plus its fall since), or -1; `along`: how far past the hit along its flight it now is.</summary>
        public static int Match(int source,Vector3 hit,float hitAt,ArrowShot[] shots,int count,out float along)
        {
            along=0;int best=-1;float score=float.MaxValue;if(shots==null)return -1;count=Math.Min(count,shots.Length);
            for(int i=0;i<count;i++)
            {
                var s=shots[i];if(s.Owner!=source)continue;float speed=s.Velocity.Length();if(!(speed>1))continue;var d=s.Velocity/speed;
                var rel=s.Position-hit;float past=Vector3.Dot(rel,d),dt=Math.Max(0,s.At-hitAt),expected=speed*dt;
                if(past< -MatchBehind||past>expected+speed/20+.6f)continue;
                float off=(rel-d*past).Length(),tolerance=MatchRadius+.5f*Math.Abs(s.Gravity)*dt*dt+.02f*Math.Abs(past);if(off>tolerance)continue;
                float sc=off+.05f*Math.Abs(past-expected);if(sc<score){score=sc;best=i;along=past;}
            }
            return best;
        }

        // ================================================================================================ blood
        /// <summary>The drops a lodged arrow's entry has dripped `age` s after the hit (`strength` 1 a normal arrow, more for a
        /// killing full-draw one): a decaying trickle over <see cref="DripLife"/>, thinned like the other blood.</summary>
        public static int Drips(float age,float strength,float quality,bool low)
        {
            if(!(age>0)||!(strength>0)||!(quality>0))return 0;age=Math.Min(age,DripLife);
            float n=DripRate*strength*DripFade*(1-(float)Math.Exp(-age/DripFade));n*=quality<1?.35f+.55f*Math.Min(1,quality):1;if(low)n*=.55f;return (int)Math.Floor(n);
        }
        /// <summary>An arrow point's blood strength: a lodged arrow 1, a killing full-draw arrow a little more; the exit of a
        /// pass-through more again. Gore off: none.</summary>
        public static float Strength(bool gore,float draw,bool lethal,bool exit)
        {
            if(!gore)return 0;float s=exit?1.3f:1;if(lethal&&draw>=.999f)s+=.35f;else if(lethal)s+=.15f;return s;
        }
        /// <summary>A pass-through's exit spray (NativeBloodSim "cavity" along the flight): its severity and the extra drops.</summary>
        public static void Exit(float draw,bool lethal,out float severity,out int extra)
        {
            draw=Math.Max(0,Math.Min(1,draw));severity=Math.Min(1,.7f+.25f*draw+(lethal?.1f:0));extra=(int)Math.Round(3+5*draw)+(lethal&&draw>=.999f?6:lethal?3:0);
        }
        /// <summary>How many arrow points may bleed at once (reduced effects and the low tier: <see cref="LowBleeds"/>).</summary>
        public static int BleedCap(float quality,bool low)=>low||quality<.5f?LowBleeds:MaxBleeds;
        private static bool Bleeding(in NativeBowStuck.Record r,float time)=>r.Kind==NativeBowStuck.Kind.Pilot&&r.Blood>0&&time-r.Born>=0&&time-r.Born<DripLife;
        /// <summary>Whether stuck record `i` bleeds now: a pilot's arrow point with blood, still dripping, among the `cap` newest
        /// that are (allocation free).</summary>
        public static bool Bleeds(NativeBowStuck stuck,int i,float time,int cap)
        {
            if(stuck==null||i<0||i>=stuck.Count)return false;var r=stuck[i];if(!Bleeding(r,time))return false;int newer=0;
            for(int j=0;j<stuck.Count;j++){if(j==i)continue;var o=stuck[j];if(Bleeding(o,time)&&(o.Born>r.Born||o.Born==r.Born&&j>i)&&++newer>=cap)return false;}
            return true;
        }
        /// <summary>The half of a split body (Core NativeBisect) an arrow rides: its joint's half, or for a joint the cut crossed,
        /// the side of the cut its point (`point`, where it sat on the body as it died, Unity world) was on.</summary>
        public static int Half(BisectRequest r,int bone,Vector3 point)=>r==null?0:r.HalfOf(bone,point);
    }
}
