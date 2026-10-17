using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The procedural kit the Standard weapon skins are built with (Core/NativeWeaponModels*.cs). Engine-free and deterministic:
    // no randomness, no engine calls, so the managed checks build exactly what the player builds. Geometry is authored in the
    // source weapon frame the Prototype models and every socket use (NativeWeaponDressing: +x the rig's left, +y up the sights
    // or the blade's flat, +z along the barrel or blade, the hand at the origin); the Runtime mirrors x and turns each
    // triangle over once (Runtime/NativeWeaponSkinModels.cs), exactly as tools/export_models.mjs converts the source models.
    // Faces are flat shaded (or smooth around a ring) with chamfered edges, so the hard-surface forms catch the arena's key and
    // rim light the way the voxel pilot's bevels do.

    /// <summary>
    /// One material zone: linear albedo and emission, and the emission tag NativeSourceLit reads per vertex (0 untinted, 2 the
    /// pilot's accent tint: ExportEmber in the bake becomes the pilot's own glow colour, as on the pilot's armour strips).
    /// </summary>
    public readonly struct WeaponPaint
    {
        public readonly Vector3 Color,Emit;public readonly float Tag;
        /// <summary>The surface class the zombie shading reads per vertex (WeaponSection.Data x; NativeHordeModels.Surf*: 0 plain
        /// Lambert, as every other model draws; ChipArena/ZombieLit only).</summary>
        public readonly float Surface;
        public WeaponPaint(Vector3 color,Vector3 emit,float tag){Color=color;Emit=emit;Tag=tag;Surface=0;}
        public WeaponPaint(Vector3 color,Vector3 emit,float tag,float surface){Color=color;Emit=emit;Tag=tag;Surface=surface;}
        /// <summary>This paint on surface class `surface`.</summary>
        public WeaponPaint As(float surface)=>new WeaponPaint(Color,Emit,Tag,surface);
        public static Vector3 Linear(int hex)=>NativePortraitRules.Linear(hex);
        /// <summary>A lit surface; `sheen` adds that share of its colour as self light (weapon-visuals.ts material's sheen).</summary>
        public static WeaponPaint Solid(int hex,float sheen=0){var c=Linear(hex);return new WeaponPaint(c,c*sheen,0);}
        /// <summary>A fixed-colour light (energy cores, sight dots): albedo share `albedo`, emission `strength`.</summary>
        public static WeaponPaint Glow(int hex,float strength,float albedo=.3f){var c=Linear(hex);return new WeaponPaint(c*albedo,c*strength,0);}
        /// <summary>An accent strip: the export ember, tinted to the pilot's accent by the draw (tag 2).</summary>
        public static WeaponPaint Accent(float strength=1.6f,float albedo=.35f){var c=NativePortraitRules.ExportEmber;return new WeaponPaint(c*albedo,c*strength,2);}
        public WeaponPaint Scaled(float k)=>new WeaponPaint(Color*k,Emit*k,Tag,Surface);
        public bool Lit=>Emit.X+Emit.Y+Emit.Z<=1e-6f;
    }
    /// <summary>How a section follows the melee reach stretch (Runtime applies it per draw; see NativeWeaponModels.Stretch).</summary>
    public enum WeaponStretch : byte { None, Rigid, Blade }
    /// <summary>One draw batch of a weapon tier: the joint it rides, its stretch class and its triangles (source frame).</summary>
    public sealed class WeaponSection
    {
        public int Bone;public WeaponStretch Stretch;
        public readonly List<Vector3> Positions=new List<Vector3>(),Normals=new List<Vector3>();
        public readonly List<Vector4> Colors=new List<Vector4>(),Emission=new List<Vector4>();public readonly List<int> Indices=new List<int>();
        /// <summary>Per-vertex shading data the sculpt writes beside each vertex (x the paint's surface class; the zombie models add
        /// their occlusion, missing-piece variant and sway, NativeHordeModels). Sections built by hand may leave it empty: a mesh
        /// uploads it only when it matches the positions.</summary>
        public readonly List<Vector2> Data=new List<Vector2>();
        public int Triangles=>Indices.Count/3;public int Vertices=>Positions.Count;
    }
    /// <summary>One detail tier of one weapon (0 full, 1 reduced, 2 low).</summary>
    public sealed class WeaponMesh
    {
        public string Spec;public int Tier;public readonly List<WeaponSection> Sections=new List<WeaponSection>();
        public int Triangles{get{int n=0;foreach(var s in Sections)n+=s.Triangles;return n;}}
        public WeaponSection Section(int bone,WeaponStretch stretch)
        {
            foreach(var s in Sections)if(s.Bone==bone&&s.Stretch==stretch)return s;
            var made=new WeaponSection{Bone=bone,Stretch=stretch};Sections.Add(made);return made;
        }
        public bool Bounds(int bone,out Vector3 min,out Vector3 max)
        {
            min=new Vector3(float.MaxValue);max=new Vector3(float.MinValue);bool any=false;
            foreach(var s in Sections)if(bone<0||s.Bone==bone)foreach(var p in s.Positions){min=Vector3.Min(min,p);max=Vector3.Max(max,p);any=true;}
            return any;
        }
    }
    /// <summary>
    /// Writes primitives into the current section through a transform stack (row vectors, System.Numerics: a pushed matrix
    /// applies before the ones below it). Detail adapts to the tier: ring sides, bevels and fine details thin out.
    /// </summary>
    public sealed class WeaponSculpt
    {
        public readonly int Tier;private readonly WeaponMesh mesh;private WeaponSection target;
        private Matrix4x4 m=Matrix4x4.Identity;private bool flip;private readonly Stack<Matrix4x4> stack=new Stack<Matrix4x4>();
        public WeaponSculpt(WeaponMesh mesh,int tier){this.mesh=mesh;Tier=tier;}
        /// <summary>Full detail only: serrations, rivets, slots, lettering-size trims.</summary>
        public bool Fine=>Tier==0;
        /// <summary>Full and reduced: secondary forms (sights, straps, rings).</summary>
        public bool Mid=>Tier<=1;
        public int Sides(int full)=>Tier==0?full:Tier==1?Math.Max(4,(full*2+2)/3):Math.Max(4,(full+1)/2);
        public float Bevel(float b)=>Tier>=2?0:Tier==1?(b>=.006f?b*.8f:0):b;
        public void Into(int bone,WeaponStretch stretch=WeaponStretch.None)=>target=mesh.Section(bone,stretch);
        public void Push(Matrix4x4 local){stack.Push(m);m=local*m;flip=m.GetDeterminant()<0;normalReady=false;}
        public void Pop(){m=stack.Pop();flip=m.GetDeterminant()<0;normalReady=false;}
        // The normal matrix (inverse transpose of the transform), made when a smooth face first needs it after a push or pop.
        private Matrix4x4 normalM;private bool normalReady;
        /// <summary>A normal through the current transform (the inverse transpose, so scaled and mirrored frames keep it true).</summary>
        public Vector3 N(Vector3 n)
        {
            if(!normalReady){normalM=Matrix4x4.Invert(m,out var inv)?Matrix4x4.Transpose(inv):Matrix4x4.Identity;normalReady=true;}
            return Unit(Vector3.TransformNormal(n,normalM));
        }
        public Vector3 P(Vector3 v)=>Vector3.Transform(v,m);
        public static Matrix4x4 T(float x,float y,float z)=>Matrix4x4.CreateTranslation(x,y,z);
        public static Matrix4x4 Rx(float a)=>Matrix4x4.CreateRotationX(a);
        public static Matrix4x4 Ry(float a)=>Matrix4x4.CreateRotationY(a);
        public static Matrix4x4 Rz(float a)=>Matrix4x4.CreateRotationZ(a);
        public static Matrix4x4 S(float x,float y,float z)=>Matrix4x4.CreateScale(x,y,z);

        // ---- raw faces (world points of the current section) ----
        private int Vertex(Vector3 p,Vector3 n,in WeaponPaint c)
        {
            var s=target;s.Positions.Add(p);s.Normals.Add(n);s.Colors.Add(new Vector4(c.Color,1));s.Emission.Add(new Vector4(c.Emit,c.Tag));s.Data.Add(new Vector2(c.Surface,0));return s.Positions.Count-1;
        }
        private static Vector3 Unit(Vector3 v){float l=v.Length();return l>1e-12f?v/l:Vector3.UnitY;}
        /// <summary>A flat triangle (counter-clockwise front, the source convention); `world` points skip the transform.</summary>
        public void Tri(in WeaponPaint c,Vector3 a,Vector3 b,Vector3 d,bool world=false)
        {
            if(!world){a=P(a);b=P(b);d=P(d);}if(flip){var t=b;b=d;d=t;}
            var n=Vector3.Cross(b-a,d-a);if(n.LengthSquared()<1e-14f)return;n=Unit(n);
            int i=Vertex(a,n,c);Vertex(b,n,c);Vertex(d,n,c);target.Indices.Add(i);target.Indices.Add(i+1);target.Indices.Add(i+2);
        }
        /// <summary>A flat quad a-b-c-d (counter-clockwise front); a collapsed corner becomes one triangle.</summary>
        public void Quad(in WeaponPaint c,Vector3 a,Vector3 b,Vector3 d,Vector3 e,bool world=false)
        {
            if(!world){a=P(a);b=P(b);d=P(d);e=P(e);}
            Face(c,a,b,d,e,Vector3.Zero,Vector3.Zero,Vector3.Zero,Vector3.Zero,false);
        }
        // World-space quad with optional per-corner normals (smooth lofts).
        private void Face(in WeaponPaint c,Vector3 a,Vector3 b,Vector3 d,Vector3 e,Vector3 na,Vector3 nb,Vector3 nd,Vector3 ne,bool smooth)
        {
            if(flip){var t=b;b=e;e=t;var u=nb;nb=ne;ne=u;}
            bool ab=Vector3.DistanceSquared(a,b)<1e-12f,bd=Vector3.DistanceSquared(b,d)<1e-12f,de=Vector3.DistanceSquared(d,e)<1e-12f,ea=Vector3.DistanceSquared(e,a)<1e-12f;
            var n=Vector3.Cross(d-a,e-b);if(n.LengthSquared()<1e-16f){if(!ab&&!bd)n=Vector3.Cross(b-a,d-a);else n=Vector3.Cross(d-a,e-a);}
            if(n.LengthSquared()<1e-16f)return;n=Unit(n);
            if(!smooth){na=nb=nd=ne=n;}
            int collapsed=(ab?1:0)+(bd?1:0)+(de?1:0)+(ea?1:0);if(collapsed>=2)return;
            var s=target;
            if(collapsed==1)
            {
                // Drop the repeated corner: one triangle.
                Vector3 p0,p1,p2,n0,n1,n2;
                if(ab){p0=a;p1=d;p2=e;n0=na;n1=nd;n2=ne;}else if(bd){p0=a;p1=b;p2=e;n0=na;n1=nb;n2=ne;}else if(de){p0=a;p1=b;p2=d;n0=na;n1=nb;n2=nd;}else{p0=a;p1=b;p2=d;n0=na;n1=nb;n2=nd;}
                int i=Vertex(p0,n0,c);Vertex(p1,n1,c);Vertex(p2,n2,c);s.Indices.Add(i);s.Indices.Add(i+1);s.Indices.Add(i+2);return;
            }
            int k=Vertex(a,na,c);Vertex(b,nb,c);Vertex(d,nd,c);Vertex(e,ne,c);
            s.Indices.Add(k);s.Indices.Add(k+1);s.Indices.Add(k+2);s.Indices.Add(k);s.Indices.Add(k+2);s.Indices.Add(k+3);
        }
        /// <summary>A smooth quad a-b-c-d (counter-clockwise front) with a normal at each corner (organic surfaces), through the
        /// current transform; a collapsed corner becomes one triangle.</summary>
        public void SmoothQuad(in WeaponPaint c,Vector3 a,Vector3 b,Vector3 d,Vector3 e,Vector3 na,Vector3 nb,Vector3 nd,Vector3 ne)
        {
            a=P(a);b=P(b);d=P(d);e=P(e);na=N(na);nb=N(nb);nd=N(nd);ne=N(ne);Face(c,a,b,d,e,na,nb,nd,ne,true);
        }
        /// <summary>A smooth quad whose corners take their own albedo (`ca`..`ce`, linear) over the paint's emission, tag and surface:
        /// colour fields blended across a surface (the zombies' skin, sockets and tears).</summary>
        public void SmoothQuad(in WeaponPaint c,Vector3 a,Vector3 b,Vector3 d,Vector3 e,Vector3 na,Vector3 nb,Vector3 nd,Vector3 ne,Vector3 ca,Vector3 cb,Vector3 cd,Vector3 ce)
        {
            a=P(a);b=P(b);d=P(d);e=P(e);na=N(na);nb=N(nb);nd=N(nd);ne=N(ne);
            if(flip){var t=b;b=e;e=t;var u=nb;nb=ne;ne=u;var w=cb;cb=ce;ce=w;}
            if(Vector3.Cross(d-a,e-b).LengthSquared()<1e-16f)return;
            WeaponPaint At(in WeaponPaint p,Vector3 col)=>new WeaponPaint(col,p.Emit,p.Tag,p.Surface);
            bool ab=Vector3.DistanceSquared(a,b)<1e-12f,bd=Vector3.DistanceSquared(b,d)<1e-12f,de=Vector3.DistanceSquared(d,e)<1e-12f,ea=Vector3.DistanceSquared(e,a)<1e-12f;
            int collapsed=(ab?1:0)+(bd?1:0)+(de?1:0)+(ea?1:0);if(collapsed>=2)return;var s=target;
            if(collapsed==1)
            {
                // Drop the repeated corner: one triangle.
                if(ab){b=d;nb=nd;cb=cd;d=e;nd=ne;cd=ce;}else if(bd){d=e;nd=ne;cd=ce;}
                int i=Vertex(a,na,At(c,ca));Vertex(b,nb,At(c,cb));Vertex(d,nd,At(c,cd));s.Indices.Add(i);s.Indices.Add(i+1);s.Indices.Add(i+2);return;
            }
            int k=Vertex(a,na,At(c,ca));Vertex(b,nb,At(c,cb));Vertex(d,nd,At(c,cd));Vertex(e,ne,At(c,ce));
            s.Indices.Add(k);s.Indices.Add(k+1);s.Indices.Add(k+2);s.Indices.Add(k);s.Indices.Add(k+2);s.Indices.Add(k+3);
        }
        /// <summary>A quad turned to face away from `inside` (convex primitives).</summary>
        private void QuadOut(in WeaponPaint c,Vector3 a,Vector3 b,Vector3 d,Vector3 e,Vector3 inside)
        {
            var n=Vector3.Cross(d-a,e-b);var mid=(a+b+d+e)*.25f;if(Vector3.Dot(n,mid-inside)<0){var t=b;b=e;e=t;}
            bool f=flip;flip=false;Face(c,a,b,d,e,default,default,default,default,false);flip=f;
        }
        private void TriOut(in WeaponPaint c,Vector3 a,Vector3 b,Vector3 d,Vector3 inside)
        {
            var n=Vector3.Cross(b-a,d-a);var mid=(a+b+d)/3;if(Vector3.Dot(n,mid-inside)<0){var t=b;b=d;d=t;}
            bool f=flip;flip=false;Tri(c,a,b,d,true);flip=f;
        }

        // ---- boxes ----
        /// <summary>A box (centre, full size) with every edge chamfered by `bevel` (26 faces; 6 when the tier drops bevels).</summary>
        public void Box(in WeaponPaint c,Vector3 center,Vector3 size,float bevel=0)
        {
            var h=size*.5f;float b=Math.Min(Bevel(bevel),.45f*Math.Min(h.X,Math.Min(h.Y,h.Z)));var o=P(center);
            Vector3 V(float x,float y,float z)=>P(center+new Vector3(x,y,z));
            if(b<=1e-5f)
            {
                for(int axis=0;axis<3;axis++)for(int s=-1;s<=1;s+=2)
                {
                    Vector3 Q(float u,float v){var p=new float[3];p[axis]=s*Get(h,axis);p[(axis+1)%3]=u*Get(h,(axis+1)%3);p[(axis+2)%3]=v*Get(h,(axis+2)%3);return V(p[0],p[1],p[2]);}
                    QuadOut(c,Q(-1,-1),Q(1,-1),Q(1,1),Q(-1,1),o);
                }
                return;
            }
            float hx=h.X,hy=h.Y,hz=h.Z;
            Vector3 PX(float sx,float sy,float sz)=>V(sx*hx,sy*(hy-b),sz*(hz-b));
            Vector3 PY(float sx,float sy,float sz)=>V(sx*(hx-b),sy*hy,sz*(hz-b));
            Vector3 PZ(float sx,float sy,float sz)=>V(sx*(hx-b),sy*(hy-b),sz*hz);
            for(int s=-1;s<=1;s+=2)
            {
                QuadOut(c,PX(s,-1,-1),PX(s,1,-1),PX(s,1,1),PX(s,-1,1),o);
                QuadOut(c,PY(-1,s,-1),PY(1,s,-1),PY(1,s,1),PY(-1,s,1),o);
                QuadOut(c,PZ(-1,-1,s),PZ(1,-1,s),PZ(1,1,s),PZ(-1,1,s),o);
            }
            var edge=c.Scaled(1);
            for(int s1=-1;s1<=1;s1+=2)for(int s2=-1;s2<=1;s2+=2)
            {
                // x-y edges (along z), x-z edges (along y), y-z edges (along x)
                QuadOut(edge,PX(s1,s2,-1),PX(s1,s2,1),PY(s1,s2,1),PY(s1,s2,-1),o);
                QuadOut(edge,PX(s1,-1,s2),PX(s1,1,s2),PZ(s1,1,s2),PZ(s1,-1,s2),o);
                QuadOut(edge,PY(-1,s1,s2),PY(1,s1,s2),PZ(1,s1,s2),PZ(-1,s1,s2),o);
            }
            for(int sx=-1;sx<=1;sx+=2)for(int sy=-1;sy<=1;sy+=2)for(int sz=-1;sz<=1;sz+=2)TriOut(edge,PX(sx,sy,sz),PY(sx,sy,sz),PZ(sx,sy,sz),o);
        }
        private static float Get(Vector3 v,int i)=>i==0?v.X:i==1?v.Y:v.Z;
        /// <summary>A box rotated by Euler angles (three's XYZ order) about its own centre.</summary>
        public void Box(in WeaponPaint c,Vector3 center,Vector3 size,float bevel,float rx,float ry=0,float rz=0)
        {
            Push(Rx(rx)*Ry(ry)*Rz(rz)*T(center.X,center.Y,center.Z));Box(c,Vector3.Zero,size,bevel);Pop();
        }
        /// <summary>A thin decal quad lifted `lift` off a face (vents, slots, panel lines); faces +n = u x v.</summary>
        public void Decal(in WeaponPaint c,Vector3 center,Vector3 u,Vector3 v,float lift=.0012f)
        {
            var n=Unit(Vector3.Cross(u,v))*lift;var o=center+n;Quad(c,o-u-v,o+u-v,o+u+v,o-u+v);
        }

        // ---- rings and lofts ----
        /// <summary>A regular polygon ring of `sides` points (counter-clockwise seen from +z) at depth z.</summary>
        public static Vector3[] Circle(int sides,float rx,float ry,float z,float phase=0,float cx=0,float cy=0)
        {
            var o=new Vector3[sides];for(int i=0;i<sides;i++){double a=phase+i*Math.PI*2/sides;o[i]=new Vector3(cx+rx*(float)Math.Cos(a),cy+ry*(float)Math.Sin(a),z);}return o;
        }
        /// <summary>A chamfered rectangle (8 points, counter-clockwise from +z): width w, height h, corner cut c.</summary>
        public static Vector3[] Rect(float w,float h,float c,float z,float cx=0,float cy=0)
        {
            float x=w/2,y=h/2;c=Math.Max(1e-4f,Math.Min(c,Math.Min(x,y)*.9f));
            return new[]{new Vector3(cx+x,cy-y+c,z),new Vector3(cx+x,cy+y-c,z),new Vector3(cx+x-c,cy+y,z),new Vector3(cx-x+c,cy+y,z),new Vector3(cx-x,cy+y-c,z),new Vector3(cx-x,cy-y+c,z),new Vector3(cx-x+c,cy-y,z),new Vector3(cx+x-c,cy-y,z)};
        }
        /// <summary>A planar outline (x, y pairs, counter-clockwise) placed at depth z.</summary>
        public static Vector3[] Outline(float z,params float[] xy){var o=new Vector3[xy.Length/2];for(int i=0;i<o.Length;i++)o[i]=new Vector3(xy[i*2],xy[i*2+1],z);return o;}
        public static Vector3[] Point(int count,Vector3 p){var o=new Vector3[count];for(int i=0;i<count;i++)o[i]=p;return o;}
        public static Vector3[] Moved(Vector3[] ring,Vector3 by,float scale=1)
        {
            var c=Centroid(ring);var o=new Vector3[ring.Length];for(int i=0;i<o.Length;i++){var d=ring[i]-c;o[i]=c+new Vector3(d.X*scale,d.Y*scale,d.Z)+by;}return o;
        }
        public static Vector3[] Scaled(Vector3[] ring,float sx,float sy,float z)
        {
            var o=new Vector3[ring.Length];for(int i=0;i<o.Length;i++)o[i]=new Vector3(ring[i].X*sx,ring[i].Y*sy,z);return o;
        }
        public static Vector3 Centroid(Vector3[] ring){var c=Vector3.Zero;foreach(var p in ring)c+=p;return c/ring.Length;}
        /// <summary>
        /// Skins consecutive rings (equal point counts, each counter-clockwise about the loft's direction) and caps both ends with
        /// fans about the ring centroid. `paint(segment, side)` paints each band; smooth 0 flat, 1 smooth around the ring, 2
        /// smooth both ways; `closed` joins the last ring back to the first (rings of a torus).
        /// </summary>
        public void Loft(IReadOnlyList<Vector3[]> rings,Func<int,int,WeaponPaint> paint,int smooth=0,bool capStart=true,bool capEnd=true,WeaponPaint? cap=null,bool closed=false,bool inward=false,WeaponPaint? endCap=null)
        {
            int count=rings.Count,n=rings[0].Length;var w=new Vector3[count][];
            for(int i=0;i<count;i++){if(rings[i].Length!=n)throw new ArgumentException("Loft rings differ in size");w[i]=new Vector3[n];for(int k=0;k<n;k++)w[i][k]=P(rings[i][k]);}
            bool f=flip^inward;int segments=closed?count:count-1;
            var faces=new Vector3[segments,n];
            for(int i=0;i<segments;i++)for(int k=0;k<n;k++)
            {
                Vector3 a=w[i][k],b=w[i][(k+1)%n],d=w[(i+1)%count][(k+1)%n],e=w[(i+1)%count][k];var fn=Vector3.Cross(d-a,e-b);
                if(fn.LengthSquared()<1e-16f)fn=Vector3.Cross(b-a,d-a).LengthSquared()>1e-16f?Vector3.Cross(b-a,d-a):Vector3.Cross(d-a,e-a);
                faces[i,k]=Unit(fn)*(f?-1:1);
            }
            Vector3 Corner(int i,int k,int seg)
            {
                // Smooth around: the two faces of this band meeting at point k; smooth both ways adds the neighbouring bands.
                var s=faces[seg,(k-1+n)%n]+faces[seg,k%n];
                if(smooth>=2){int prev=seg-1,next=seg+1;bool atStart=i==seg;int other=atStart?prev:next;if(closed)other=(other+segments)%segments;if(other>=0&&other<segments)s+=faces[other,(k-1+n)%n]+faces[other,k%n];}
                return Unit(s);
            }
            bool saved=flip;flip=f;
            for(int i=0;i<segments;i++)for(int k=0;k<n;k++)
            {
                int i1=(i+1)%count,k1=(k+1)%n;var c=paint(i,k);
                if(smooth>0)Face(c,w[i][k],w[i][k1],w[i1][k1],w[i1][k],Corner(i,k,i),Corner(i,k1,i),Corner(i1,k1,i),Corner(i1,k,i),true);
                else Face(c,w[i][k],w[i][k1],w[i1][k1],w[i1][k],default,default,default,default,false);
            }
            if(!closed)
            {
                var cp=cap??paint(0,0);
                if(capStart)Fan(cp,w[0],true,f);
                var ce=endCap??cap??paint(count-2,0);
                if(capEnd)Fan(ce,w[count-1],false,f);
            }
            flip=saved;
        }
        // A cap: a fan about the ring centroid, facing back along the loft at the start and forward at the end.
        private void Fan(in WeaponPaint c,Vector3[] ring,bool start,bool f)
        {
            var o=Vector3.Zero;foreach(var p in ring)o+=p;o/=ring.Length;int n=ring.Length;
            if(Vector3.DistanceSquared(ring[0],o)<1e-12f&&Vector3.DistanceSquared(ring[n/2],o)<1e-12f)return;
            bool saved=flip;flip=false;
            for(int k=0;k<n;k++){var a=ring[k];var b=ring[(k+1)%n];if(start^f)Tri(c,o,b,a,true);else Tri(c,o,a,b,true);}
            flip=saved;
        }
        /// <summary>A body of revolution about +z: (radius, z) pairs in increasing z, `sides` around.</summary>
        public void Lathe(Func<int,WeaponPaint> paint,float[] rz,int sides,int smooth=1,float phase=0,bool caps=true,float sx=1,float sy=1)
        {
            var rings=new List<Vector3[]>();for(int i=0;i+1<rz.Length;i+=2)rings.Add(Circle(sides,rz[i]*sx,rz[i]*sy,rz[i+1],phase));
            Loft(rings,(i,k)=>paint(i),smooth,caps,caps);
        }
        public void Lathe(WeaponPaint paint,float[] rz,int sides,int smooth=1,float phase=0,bool caps=true)=>Lathe(_=>paint,rz,sides,smooth,phase,caps);
        /// <summary>A cylinder along +z from z0 to z1 with bevelled rims.</summary>
        public void Cylinder(in WeaponPaint c,float r,float z0,float z1,int sides,float bevel=0,float cx=0,float cy=0,int smooth=1)
        {
            float b=Math.Min(Bevel(bevel),Math.Min(r*.4f,(z1-z0)*.4f));var rings=new List<Vector3[]>();
            if(b>1e-5f){rings.Add(Circle(sides,r-b,r-b,z0,0,cx,cy));rings.Add(Circle(sides,r,r,z0+b,0,cx,cy));rings.Add(Circle(sides,r,r,z1-b,0,cx,cy));rings.Add(Circle(sides,r-b,r-b,z1,0,cx,cy));}
            else{rings.Add(Circle(sides,r,r,z0,0,cx,cy));rings.Add(Circle(sides,r,r,z1,0,cx,cy));}
            var paint=c;Loft(rings,(i,k)=>paint,smooth);
        }
        /// <summary>A sphere (or ellipsoid) of `rings` bands and `sides` around, smooth both ways unless faceted.</summary>
        public void Sphere(in WeaponPaint c,Vector3 center,Vector3 radius,int rings,int sides,bool faceted=false)
        {
            var list=new List<Vector3[]>();
            for(int i=0;i<=rings;i++){double a=Math.PI*i/rings;float z=-(float)Math.Cos(a),r=(float)Math.Sin(a);var ring=Circle(sides,r*radius.X,r*radius.Y,z*radius.Z,0,center.X,center.Y);for(int k=0;k<sides;k++)ring[k].Z+=center.Z;list.Add(ring);}
            var paint=c;Loft(list,(i,k)=>paint,faceted?0:2,false,false);
        }
        /// <summary>A crystal: a bipyramid of `sides` with its girdle at `girdle` (0..1 of the height) about +z.</summary>
        public void Gem(in WeaponPaint c,Vector3 center,float radius,float height,int sides,float girdle=.5f,float phase=0,float crown=0)
        {
            var list=new List<Vector3[]>{Point(sides,center+new Vector3(0,0,-height*girdle))};
            var mid=Circle(sides,radius,radius,center.Z,phase,center.X,center.Y);list.Add(mid);
            if(crown>0)list.Add(Circle(sides,radius*crown,radius*crown,center.Z+height*(1-girdle)*.6f,phase+(float)Math.PI/sides,center.X,center.Y));
            list.Add(Point(sides,center+new Vector3(0,0,height*(1-girdle))));
            var paint=c;Loft(list,(i,k)=>paint,0,false,false);
        }
        /// <summary>
        /// A tube along a polyline (parallel-transported frames from `up`): radius per point (or one), `sides` around, smooth
        /// around; open ends capped unless `caps` is false; `closed` loops back (a torus).
        /// </summary>
        public void Tube(Func<int,WeaponPaint> paint,IReadOnlyList<Vector3> path,Func<int,float> radius,int sides,Vector3 up,int smooth=1,bool caps=true,bool closed=false,float squash=1,float phase=0)
        {
            int n=path.Count;var rings=new List<Vector3[]>(n);Vector3 normal=Vector3.Zero;
            for(int i=0;i<n;i++)
            {
                Vector3 tangent=closed?path[(i+1)%n]-path[(i-1+n)%n]:i==0?path[1]-path[0]:i==n-1?path[n-1]-path[n-2]:path[i+1]-path[i-1];tangent=Unit(tangent);
                if(i==0){normal=up-tangent*Vector3.Dot(up,tangent);if(normal.LengthSquared()<1e-8f)normal=Vector3.Cross(tangent,Vector3.UnitX);normal=Unit(normal);}
                else{normal-=tangent*Vector3.Dot(normal,tangent);normal=Unit(normal);}
                var bi=Vector3.Cross(tangent,normal);float r=radius(i);var ring=new Vector3[sides];
                for(int k=0;k<sides;k++){double a=phase+k*Math.PI*2/sides;ring[k]=path[i]+(normal*(float)Math.Cos(a)*squash+bi*(float)Math.Sin(a))*r;}
                rings.Add(ring);
            }
            Loft(rings,(i,k)=>paint(i),smooth,caps,caps,null,closed);
        }
        public void Tube(WeaponPaint paint,IReadOnlyList<Vector3> path,float radius,int sides,Vector3 up,int smooth=1,bool caps=true,bool closed=false,float squash=1)
            =>Tube(_=>paint,path,_=>radius,sides,up,smooth,caps,closed,squash);
        /// <summary>A torus about +z through `center`: major radius R, tube r, swept over [from, to] radians.</summary>
        public void Torus(in WeaponPaint c,Vector3 center,float major,float minor,int segments,int sides,float from=0,float to=(float)(Math.PI*2),float squash=1)
        {
            bool full=Math.Abs(to-from-Math.PI*2)<1e-4;int count=full?segments:segments+1;var path=new List<Vector3>(count);
            for(int i=0;i<count;i++){double a=from+(to-from)*i/segments;path.Add(center+new Vector3(major*(float)Math.Cos(a),major*(float)Math.Sin(a),0));}
            Tube(c,path,minor,sides,Vector3.UnitZ,1,!full,full,squash);
        }
        /// <summary>A bevelled slab from a counter-clockwise outline in the xy plane between z0 and z1 (front at z1).</summary>
        public void Slab(Func<int,WeaponPaint> sidePaint,WeaponPaint front,WeaponPaint back,Vector3[] outline,float z0,float z1,float bevel)
        {
            float b=Math.Min(Bevel(bevel),(z1-z0)*.45f);var rings=new List<Vector3[]>();
            if(b>1e-5f){rings.Add(Inset(outline,b,z0));rings.Add(Inset(outline,0,z0+b));rings.Add(Inset(outline,0,z1-b));rings.Add(Inset(outline,b,z1));}
            else{rings.Add(Inset(outline,0,z0));rings.Add(Inset(outline,0,z1));}
            int last=rings.Count-1;Loft(rings,(i,k)=>sidePaint(k),0,false,false);
            Fan(back,ToWorld(rings[0]),true,flip);Fan(front,ToWorld(rings[last]),false,flip);
        }
        private Vector3[] ToWorld(Vector3[] ring){var o=new Vector3[ring.Length];for(int i=0;i<o.Length;i++)o[i]=P(ring[i]);return o;}
        /// <summary>An outline (counter-clockwise, xy) moved inward by d along its corner bisectors, at depth z.</summary>
        public static Vector3[] Inset(Vector3[] outline,float d,float z)
        {
            int n=outline.Length;var o=new Vector3[n];
            for(int i=0;i<n;i++)
            {
                var p=outline[i];var a=outline[(i-1+n)%n];var b=outline[(i+1)%n];
                var e0=Vector2.Normalize(new Vector2(p.X-a.X,p.Y-a.Y));var e1=Vector2.Normalize(new Vector2(b.X-p.X,b.Y-p.Y));
                var n0=new Vector2(-e0.Y,e0.X);var n1=new Vector2(-e1.Y,e1.X);var bis=n0+n1;float len=bis.Length();
                bis=len>1e-5f?bis/len:n0;float cos=Math.Max(.35f,Vector2.Dot(bis,n0));var q=new Vector2(p.X,p.Y)+bis*(d/cos);
                o[i]=new Vector3(q.X,q.Y,z);
            }
            return o;
        }
    }
}
