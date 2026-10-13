using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// A wall breach entrance (owner 2026-10-13: "They can also break through the walls to enter (Stage will eventually
    /// holographically recover broken pieces)"). Presentation side of a contract the rules do not implement yet
    /// (docs/SIREN-ZOMBIES.md hands it to Astra), generic and data driven: an add whose replicated entry is a map entry of kind
    /// <see cref="Kind"/> breaks the wall segment the map names <c>breach:&lt;entry id&gt;</c> (<see cref="ObjectName"/>; its
    /// renderers are the breakable panel, its collider the authority's to remove and restore) on this timeline, from the add's
    /// replicated SpawnAt and ActiveAt and the breach's restore time:
    /// <list type="number">
    /// <item>The knocks (SpawnAt to <see cref="BreakAt"/>): the add pounds the wall from behind it (<see cref="Blows"/>); the
    /// panel shudders, cracks spread from each blow and dust sifts out.</item>
    /// <item>The break at BreakAt = ActiveAt - <see cref="Through"/>: the panel bursts inward into chunks and dust, leaving a
    /// ragged hole the add's size (<see cref="Hole"/>), and the add charges through it to the entry's exit.</item>
    /// <item>The holographic restore from RestoreAt (replicated; until then <see cref="DefaultRestoreAt"/>): a hardlight scan
    /// sweeps the hole bottom to top, the broken pieces dissolve into hologram wireframes that fly back to their places and
    /// solidify; at <see cref="SolidAt"/> the wall is whole again (the rules return its collision then).</item>
    /// </list>
    /// No coordinates here: the wall, the hole and the pieces come from the map's wall segment and the entry's from/exit.
    /// Engine free; a hole is built once per breach (its arrays), everything per frame is a pure function of time.
    /// </summary>
    public static class NativeBreach
    {
        public const string Kind="breach";
        /// <summary>The name the map gives a breakable wall segment for the breach entry `id`.</summary>
        public const string ObjectPrefix="breach:";
        public static string ObjectName(string entryId)=>ObjectPrefix+entryId;
        public static bool Is(PveEntry e)=>e!=null&&string.Equals(e.kind,Kind,StringComparison.Ordinal);

        // ---- timeline (s) ----
        /// <summary>SpawnAt to the break (the proposed knocking), the break to ActiveAt (the charge through), the break to the
        /// restore (a default until the rules replicate RestoreAt) and the rebuild's length (RestoreAt to whole).</summary>
        public const float Knock=2.1f,Through=1.3f,RestoreDelay=12f,Rebuild=2.6f;
        /// <summary>The blows on the wall before the break (s after SpawnAt); the last is the break itself.</summary>
        public static readonly float[] Blows={.55f,1.2f,Knock};
        public static float BreakAt(float activeAt)=>activeAt-Through;
        public static float DefaultRestoreAt(float breakAt)=>breakAt+RestoreDelay;
        public static float SolidAt(float restoreAt)=>restoreAt+Rebuild;
        public enum Phase{Intact,Knock,Broken,Rebuild,Solid}
        /// <summary>The breach at time `t`: its phase and the seconds into it.</summary>
        public static Phase At(float t,float spawnAt,float breakAt,float restoreAt,out float into)
        {
            if(t<spawnAt){into=0;return Phase.Intact;}
            if(t<breakAt){into=t-spawnAt;return Phase.Knock;}
            if(t<restoreAt){into=t-breakAt;return Phase.Broken;}
            if(t<SolidAt(restoreAt)){into=t-restoreAt;return Phase.Rebuild;}
            into=t-SolidAt(restoreAt);return Phase.Solid;
        }
        /// <summary>The panel's shudder at `since` s after SpawnAt (m along the normal, the blow's ring-out): each blow knocks it.</summary>
        public static float Shudder(float since)
        {
            float s=0;for(int i=0;i<Blows.Length-1;i++){float a=since-Blows[i];if(a<0||a>.45f)continue;s+=.045f*(float)Math.Exp(-a*9)*(float)Math.Sin(a*62);}
            return s;
        }
        /// <summary>The age of the knock the add is swinging at `since` s after SpawnAt (a smash whose impact lands on each blow,
        /// `strike` its wind-up), NaN between them.</summary>
        public static float KnockAge(float since,float strike)
        {
            for(int i=Blows.Length-1;i>=0;i--){float age=since-(Blows[i]-strike);if(age>=0)return age<strike+.45f?age:float.NaN;}
            return float.NaN;
        }
        /// <summary>How far the cracks have spread over the panel (0..1) by `since` s after SpawnAt (a step per blow).</summary>
        public static float Cracked(float since){float c=0;for(int i=0;i<Blows.Length-1;i++)if(since>=Blows[i])c+=(1f/(Blows.Length-1))*Sm((since-Blows[i])/.18f);return Math.Min(1,c);}

        // ---- the wall segment ----
        /// <summary>A wall segment in the world: its centre, the unit along it (Right), up, and the unit out of it toward the side
        /// the add comes out on (Normal, into the arena); its width, height and thickness (m).</summary>
        public readonly struct Wall
        {
            public readonly Vector3 Center,Right,Up,Normal;public readonly float Width,Height,Thickness;
            public Wall(Vector3 center,Vector3 right,Vector3 normal,float width,float height,float thickness){Center=center;Right=right;Up=Vector3.UnitY;Normal=normal;Width=width;Height=height;Thickness=thickness;}
            public Vector3 ToWorld(Vector3 local)=>Center+Right*local.X+Up*local.Y+Normal*local.Z;
            public float Bottom=>Center.Y-Height*.5f;
            /// <summary>The wall's frame (local x along it, y up, z the normal) as a matrix (System.Numerics, row vectors).</summary>
            public Matrix4x4 Frame=>new Matrix4x4(Right.X,Right.Y,Right.Z,0,0,1,0,0,Normal.X,Normal.Y,Normal.Z,0,Center.X,Center.Y,Center.Z,1);
        }
        /// <summary>
        /// The wall from the map's segment: its bounds' centre, its rotation and its extents along its own axes (Unity world axes).
        /// The thinner horizontal axis is the thickness; the normal is the one that points from the entry's `from` (behind the
        /// wall) toward its `exit` (inside).
        /// </summary>
        public static Wall WallOf(Vector3 center,Quaternion rotation,Vector3 extents,Vector3 from,Vector3 exit)
        {
            var ax=Vector3.Transform(Vector3.UnitX,rotation);var az=Vector3.Transform(Vector3.UnitZ,rotation);ax.Y=0;az.Y=0;
            ax=ax.LengthSquared()>1e-8f?Vector3.Normalize(ax):Vector3.UnitX;az=az.LengthSquared()>1e-8f?Vector3.Normalize(az):Vector3.UnitZ;
            bool thinX=Math.Abs(extents.X)<Math.Abs(extents.Z);var normal=thinX?ax:az;var along=thinX?az:ax;float width=Math.Abs(thinX?extents.Z:extents.X),thick=Math.Abs(thinX?extents.X:extents.Z);
            var through=exit-from;through.Y=0;if(Vector3.Dot(through,normal)<0)normal=-normal;
            var right=Vector3.Cross(Vector3.UnitY,normal);right=right.LengthSquared()>1e-8f?Vector3.Normalize(right):along;
            return new Wall(center,right,normal,width,Math.Abs(extents.Y),Math.Max(.05f,thick));
        }
        /// <summary>The wall the entry alone implies (no named segment in the map): across the line from `from` to `exit`, half
        /// way, `width` by `height` (the presentation then draws the burst and the pieces but hides no panel).</summary>
        public static Wall WallOf(Vector3 from,Vector3 exit,float floor,float width,float height,float thickness)
        {
            var n=exit-from;n.Y=0;n=n.LengthSquared()>1e-6f?Vector3.Normalize(n):Vector3.UnitZ;var mid=(from+exit)*.5f;
            return new Wall(new Vector3(mid.X,floor+height*.5f,mid.Z),Vector3.Cross(Vector3.UnitY,n),n,width,height,thickness);
        }

        // ---- the hole and its pieces ----
        public const int Outline=20,MaxChunks=40;
        /// <summary>A breach's hole in its wall (wall-local metres: x along, y up from the centre, z the normal), its broken panel
        /// mesh and its pieces' slots and flights. Built once when the breach starts; queries never allocate.</summary>
        public sealed class Hole
        {
            public Wall Wall;public uint Seed;
            /// <summary>The hole's centre (local), half width and half height, and whether it opens onto the floor.</summary>
            public Vector2 Center;public float HalfWidth,HalfHeight;
            public readonly Vector2[] Rim=new Vector2[Outline];
            public int Chunks;public readonly Vector3[] Slot=new Vector3[MaxChunks],Size=new Vector3[MaxChunks],Velocity=new Vector3[MaxChunks],Spin=new Vector3[MaxChunks];
            public readonly float[] Order=new float[MaxChunks];
            /// <summary>The broken panel (local): positions, normals, uvs (metres), triangles.</summary>
            public Vector3[] Positions,Normals;public Vector2[] Uvs;public int[] Triangles;
            /// <summary>The hole's radius from its centre along the unit direction (local xy).</summary>
            public float RadiusAlong(float angle){return Radius(this,angle);}
        }
        private static float Sm(float x)=>x<=0?0:x>=1?1:x*x*(3-2*x);
        private static float H(uint seed,int i,int k)=>(float)NativeGoreRules.Hash01(seed,i,k);
        /// <summary>The ragged hole radius at `angle` (rad, local xy from the centre): a jagged ellipse, cut off by the floor (it opens
        /// onto it) and kept off the panel's other edges.</summary>
        private static float Radius(Hole h,float angle)
        {
            float c=(float)Math.Cos(angle),s=(float)Math.Sin(angle);
            // The jagged outline: the rim points' radii interpolated by angle.
            float u=angle/(float)(Math.PI*2)*Outline;u-=(float)Math.Floor(u/Outline)*Outline;int i=(int)u;float f=u-i;
            var a=h.Rim[i%Outline];var b=h.Rim[(i+1)%Outline];float r=a.Length()+(b.Length()-a.Length())*f;
            return Math.Min(r,Edge(h,c,s,true));
        }
        // The distance from the hole's centre to the panel's edge along (c, s): the floor in full, the sides and top kept 12 cm in.
        private static float Edge(Hole h,float c,float s,bool keep)
        {
            float best=float.MaxValue,hw=h.Wall.Width*.5f-(keep?.12f:0),top=h.Wall.Height*.5f-(keep?.12f:0),bottom=-h.Wall.Height*.5f;
            if(c>1e-5f)best=Math.Min(best,(hw-h.Center.X)/c);if(c<-1e-5f)best=Math.Min(best,(-hw-h.Center.X)/c);
            if(s>1e-5f)best=Math.Min(best,(top-h.Center.Y)/s);if(s<-1e-5f)best=Math.Min(best,(bottom-h.Center.Y)/s);
            return Math.Max(0,best);
        }
        /// <summary>
        /// The hole an add of capsule `radius` x `height` (world) breaks through `wall` where it crosses it (`cross`, world), seeded:
        /// its rag, its pieces (a jittered grid of chunks over the hole, a little over a cell so they close it) and the broken
        /// panel's mesh.
        /// </summary>
        public static Hole Build(in Wall wall,Vector3 cross,float radius,float height,uint seed)
        {
            var h=new Hole{Wall=wall,Seed=seed};
            float hw=Math.Min(wall.Width*.5f-.15f,radius*1.25f+.15f),hh=Math.Min(wall.Height-.2f,height*1.08f)*.5f;hw=Math.Max(.3f,hw);hh=Math.Max(.4f,hh);
            float x=Vector3.Dot(cross-wall.Center,wall.Right);x=Math.Max(-wall.Width*.5f+hw+.12f,Math.Min(wall.Width*.5f-hw-.12f,x));
            h.Center=new Vector2(x,-wall.Height*.5f+hh*.92f);h.HalfWidth=hw;h.HalfHeight=hh;
            for(int i=0;i<Outline;i++)
            {
                float a=(float)(Math.PI*2*i/Outline),jag=.8f+.3f*H(seed,i,1)-(i%2==1?.1f*H(seed,i,2):0);
                // (Below its middle the rag runs on into the floor: a breach is a doorway it walks through.)
                if(Math.Sin(a)<-.3f)jag=Math.Max(jag,1.35f);
                h.Rim[i]=new Vector2((float)Math.Cos(a)*hw*jag,(float)Math.Sin(a)*hh*jag);
            }
            // The pieces: cells over the hole's box, kept where their centre is inside the rag.
            float cell=Math.Max(.42f,Math.Min(.7f,(float)Math.Sqrt(hw*2*hh*2/28f)));int nx=(int)Math.Ceiling(hw*2/cell),ny=(int)Math.Ceiling(hh*2.2f/cell);
            for(int iy=0;iy<ny&&h.Chunks<MaxChunks;iy++)for(int ix=0;ix<nx&&h.Chunks<MaxChunks;ix++)
            {
                float cx=h.Center.X-hw+(ix+.5f)*hw*2/nx+(H(seed,iy*31+ix,3)-.5f)*cell*.25f,cy=h.Center.Y-hh*1.1f+(iy+.5f)*hh*2.2f/ny+(H(seed,iy*31+ix,4)-.5f)*cell*.25f;
                float dx=cx-h.Center.X,dy=cy-h.Center.Y,ang=(float)Math.Atan2(dy,dx);if((float)Math.Sqrt(dx*dx+dy*dy)>Radius(h,ang)+cell*.15f)continue;
                int k=h.Chunks++;float sx=hw*2/nx*(1.12f+.12f*H(seed,k,5)),sy=hh*2.2f/ny*(1.12f+.12f*H(seed,k,6));
                // (A bottom piece sits on the floor: never below it.)
                if(cy<-wall.Height*.5f+sy*.5f)cy=-wall.Height*.5f+sy*.5f;
                h.Slot[k]=new Vector3(cx,cy,0);h.Size[k]=new Vector3(sx,sy,wall.Thickness*.94f);
                // Thrown into the arena (+z), out from the hole's middle and up, spinning.
                float out_=3.4f+3.6f*H(seed,k,7);h.Velocity[k]=new Vector3(dx*1.4f+(H(seed,k,8)-.5f)*1.6f,1.2f+2.4f*H(seed,k,9)+dy*.6f,out_);
                h.Spin[k]=new Vector3(H(seed,k,10)-.5f,H(seed,k,11)-.5f,H(seed,k,12)-.5f)*14;
                h.Order[k]=(cy+wall.Height*.5f)/wall.Height;
            }
            BuildPanel(h);return h;
        }
        public const float Gravity=9.8f;
        /// <summary>
        /// Piece `k` of the hole `since` s after the break (local): thrown along its flight, bouncing once off the floor and coming to
        /// rest; `spin` its tumble (rad about its axes).
        /// </summary>
        public static void Piece(Hole h,int k,float since,out Vector3 at,out Vector3 spin)
        {
            var v=h.Velocity[k];var p0=h.Slot[k];float floor=-h.Wall.Height*.5f+h.Size[k].Y*.42f,t=Math.Max(0,since);
            float land=Land(p0.Y,v.Y,floor);float t1=Math.Min(t,land);float y=p0.Y+v.Y*t1-.5f*Gravity*t1*t1;var flat=new Vector2(v.X,v.Z)*t1;float roll=t1;
            if(t>land)
            {
                // One soft bounce, then it slides to rest.
                float vy=-(v.Y-Gravity*land)*.3f,b=Math.Min(t-land,2*vy/Gravity);y=floor+vy*b-.5f*Gravity*b*b;
                float slide=Math.Min(t-land,.6f);flat+=new Vector2(v.X,v.Z)*(.35f*slide-.29f*slide*slide);roll=land+slide*.4f;
            }
            at=new Vector3(p0.X+flat.X,Math.Max(floor,y),p0.Z+flat.Y);spin=h.Spin[k]*roll;
        }
        private static float Land(float y0,float vy,float floor){float c=y0-floor;return (vy+(float)Math.Sqrt(vy*vy+2*Gravity*Math.Max(0,c)))/Gravity;}
        /// <summary>
        /// The restore of piece `k` at `into` s after RestoreAt: 0 still rubble, rising to 1 when it is back in its slot (dissolved into
        /// a hologram wireframe on the way), and how solid it is again (0 wire .. 1 solid). The scan reaches a piece's height first.
        /// </summary>
        public static void Restore(Hole h,int k,float into,out float back,out float solid)
        {
            float start=.35f+h.Order[k]*1.0f+.12f*H(h.Seed,k,13),fly=.75f;
            back=Sm((into-start)/fly);solid=Sm((into-start-fly-.15f)/.45f);
        }
        /// <summary>The hardlight scan's height over the hole (local y) and its strength at `into` s after RestoreAt.</summary>
        public static bool Scan(Hole h,float into,out float y,out float strength)
        {
            float u=into/1.45f;y=h.Center.Y-h.HalfHeight*1.1f+u*h.HalfHeight*2.3f;strength=u<0||u>1.15f?0:Sm(u/.08f)*(1-Sm((u-1)/.15f));return strength>0;
        }
        /// <summary>Where piece `k` is drawn at time `t` (local) and how it is drawn: 0 a solid piece, else the hologram's share.</summary>
        public static void PieceAt(Hole h,int k,Phase phase,float into,float sinceBreak,out Vector3 at,out Vector3 spin,out float holo,out float solid)
        {
            Piece(h,k,sinceBreak,out var rest,out var turned);holo=0;solid=1;
            if(phase!=Phase.Rebuild){at=rest;spin=turned;return;}
            Restore(h,k,into,out float back,out solid);var slot=h.Slot[k];
            // Flying home on a short arc, unturning as it goes.
            float arc=4*back*(1-back)*.6f;at=Vector3.Lerp(rest,slot,back)+new Vector3(0,arc,0);spin=turned*(1-back);holo=back>0?1-solid:0;
        }

        // ---- the broken panel ----
        // The wall less the hole: the front and back faces fanned between the rag and the panel's edge by angle, the rag's inner
        // edge through the thickness (a little rougher on the back), the panel's top and sides.
        private static void BuildPanel(Hole h)
        {
            var w=h.Wall;float hw=w.Width*.5f,hh=w.Height*.5f,t=w.Thickness*.5f;
            // The angles: every rag point and the panel's corners (seen from the hole's centre), sorted.
            var ang=new float[Outline+4];int n=0;for(int i=0;i<Outline;i++)ang[n++]=(float)(Math.PI*2*i/Outline);
            Vector2[] corners={new Vector2(hw,hh),new Vector2(-hw,hh),new Vector2(-hw,-hh),new Vector2(hw,-hh)};
            foreach(var c in corners){float a=(float)Math.Atan2(c.Y-h.Center.Y,c.X-h.Center.X);if(a<0)a+=(float)(Math.PI*2);ang[n++]=a;}
            Array.Sort(ang,0,n);
            var pos=new System.Collections.Generic.List<Vector3>();var nor=new System.Collections.Generic.List<Vector3>();var uv=new System.Collections.Generic.List<Vector2>();var tri=new System.Collections.Generic.List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal,Func<Vector3,Vector2> map)
            {
                // Two triangles, each wound to face along `normal` (a rag's quad need not be flat or convex); slivers dropped.
                int o=pos.Count;foreach(var p in new[]{a,b,c,d}){pos.Add(p);nor.Add(normal);uv.Add(map(p));}
                void Tri(int i,int j,int k){var e=Vector3.Cross(pos[j]-pos[i],pos[k]-pos[i]);if(e.LengthSquared()<1e-10f)return;if(Vector3.Dot(e,normal)<0){tri.Add(i);tri.Add(k);tri.Add(j);}else{tri.Add(i);tri.Add(j);tri.Add(k);}}
                Tri(o,o+1,o+2);Tri(o,o+2,o+3);
            }
            Vector2 Rag(float a,float rough)=>h.Center+new Vector2((float)Math.Cos(a),(float)Math.Sin(a))*Radius(h,a)*rough;
            Vector2 Rim(float a)=>h.Center+new Vector2((float)Math.Cos(a),(float)Math.Sin(a))*Edge(h,(float)Math.Cos(a),(float)Math.Sin(a),false);
            for(int i=0;i<n;i++)
            {
                float a0=ang[i],a1=i+1<n?ang[i+1]:ang[0]+(float)(Math.PI*2);if(a1-a0<1e-5f)continue;
                var h0=Rag(a0,1);var h1=Rag(a1,1);var o0=Rim(a0);var o1=Rim(a1);
                // Front (+z) and back (-z) faces.
                Quad(new Vector3(h0,t),new Vector3(o0,t),new Vector3(o1,t),new Vector3(h1,t),Vector3.UnitZ,p=>new Vector2(p.X,p.Y));
                Quad(new Vector3(h0,-t),new Vector3(o0,-t),new Vector3(o1,-t),new Vector3(h1,-t),-Vector3.UnitZ,p=>new Vector2(-p.X,p.Y));
                // The rag's broken edge through the wall (none where it opens onto the floor).
                float bottom=-hh+.01f;if(h0.Y>bottom||h1.Y>bottom)
                {
                    var b0=Rag(a0,1.04f+.06f*H(h.Seed,i,20));var b1=Rag(a1,1.04f+.06f*H(h.Seed,i+1,20));if(b0.Y<-hh)b0.Y=-hh;if(b1.Y<-hh)b1.Y=-hh;
                    var mid=(h0+h1)*.5f-h.Center;var inward=new Vector3(-mid.X,-mid.Y,0);inward=inward.LengthSquared()>1e-8f?Vector3.Normalize(inward):Vector3.UnitY;
                    Quad(new Vector3(h0,t),new Vector3(h1,t),new Vector3(b1,-t),new Vector3(b0,-t),inward,p=>new Vector2(p.X+p.Z,p.Y));
                }
            }
            // The panel's top and sides.
            Quad(new Vector3(-hw,hh,t),new Vector3(hw,hh,t),new Vector3(hw,hh,-t),new Vector3(-hw,hh,-t),Vector3.UnitY,p=>new Vector2(p.X,p.Z));
            Quad(new Vector3(hw,-hh,t),new Vector3(hw,hh,t),new Vector3(hw,hh,-t),new Vector3(hw,-hh,-t),Vector3.UnitX,p=>new Vector2(p.Z,p.Y));
            Quad(new Vector3(-hw,-hh,t),new Vector3(-hw,hh,t),new Vector3(-hw,hh,-t),new Vector3(-hw,-hh,-t),-Vector3.UnitX,p=>new Vector2(p.Z,p.Y));
            h.Positions=pos.ToArray();h.Normals=nor.ToArray();h.Uvs=uv.ToArray();h.Triangles=tri.ToArray();
        }
        /// <summary>Whether a local point of the panel plane (x, y) is in the hole.</summary>
        public static bool InHole(Hole h,float x,float y)
        {
            float dx=x-h.Center.X,dy=y-h.Center.Y;return (float)Math.Sqrt(dx*dx+dy*dy)<Radius(h,(float)Math.Atan2(dy,dx));
        }
        /// <summary>A crack line `i` over the panel while it is knocked (local xy from a blow point toward the edges), its length share.</summary>
        public const int CrackLines=14;
        public static bool Crack(Hole h,int i,float cracked,out Vector2 a,out Vector2 b,out float width)
        {
            a=b=Vector2.Zero;width=0;float share=(i+1f)/CrackLines;if(cracked<share*.6f)return false;
            float ang=(float)(Math.PI*2*(i+H(h.Seed,i,30)*.6f)/CrackLines),len=(.4f+.9f*H(h.Seed,i,31))*Math.Max(h.HalfWidth,h.HalfHeight)*Sm((cracked-share*.6f)/.4f);
            var dir=new Vector2((float)Math.Cos(ang),(float)Math.Sin(ang));a=h.Center+dir*.15f;b=a+dir*len+new Vector2(-dir.Y,dir.X)*(H(h.Seed,i,32)-.5f)*len*.3f;width=.018f+.02f*H(h.Seed,i,33);return true;
        }
    }
}
