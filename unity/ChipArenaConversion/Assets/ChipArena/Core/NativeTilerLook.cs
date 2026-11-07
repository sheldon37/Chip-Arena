using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Tiler (the Poolrooms' ambusher swarm; docs/EXPEDITION-ENEMY-REWORK.md section 4, docs/ZOMBIE-MODELS.md 4k): not a person
    /// but a crab of the pool's own tiles, its body made here rather than taken from a pack, so it ships with the game. White glazed
    /// plates, a few chipped to the biscuit and one or two dark depth-marker tiles, laid over a dome of wet teal grout; six thin
    /// jointed legs of grout with a glazed sliver along their tops; two mandibles of broken tile; four black glass eyes.
    /// Its moves on the authority's frame (Runtime NativeTilerPresentation.cs draws them): flat in its wall while dormant, a patch of
    /// tiles a little proud of the wall; the .6 s peel out of it onto the deck (the wake); a tripod scuttle at its 5.5 m/s; the
    /// leaping bite (a crouch, a leap at the pilot, the mandibles snapping shut on the authority's .4 s contact, a hop back); hits
    /// jolt it; its death bursts its plates over the floor and crumbles the grout. Presentation only: the authority's capsule (.65 m
    /// by .3 m), reach, timings and damage are its own.
    /// Frames: the body's (metres; x right, y up, z ahead; the floor under its middle at the origin when it stands) inside the add's
    /// (the authority's floor point and facing; the same axes).
    /// </summary>
    public static class NativeTilerLook
    {
        public static readonly int Kind=NativeHordeKinds.Resolve("tiler");

        // ---- the body ----
        /// <summary>The shell: an elliptic dome (half width A, half length B) from its rim at Rim up to its crown, the belly under it.</summary>
        public const float A=.25f,B=.3f,Rim=.17f,Crown=.3f,Belly=.115f;
        /// <summary>The plates: the grid's pitch over the shell's footprint, the grout line between them, a plate's thickness and the
        /// fall of its rounded edge.</summary>
        public const float Pitch=.085f,Gap=.006f,Thick=.012f,Bevel=.004f;
        /// <summary>The legs' segments (m) and how far the feet stand out from the hips (long, their knees over its back).</summary>
        public const float Coxa=.05f,Femur=.26f,Tibia=.34f,Spread=.34f;
        /// <summary>The peel out of the wall (the authority's wake, s); the leap's farthest reach drawn (m; the authority's bite reaches
        /// 2.5 m); how flat the dormant patch lies (its dome's depth kept) and how far it stands proud of the wall (m).</summary>
        public const float Peel=.6f,LeapMost=2.1f,Flat=.12f,Proud=.004f;
        /// <summary>The death: the plates fly and settle, then sink away; the grout crumbles. Gone after Gone s.</summary>
        public const float Settle=2.6f,GoneAt=3.3f;
        public const int Variants=3,Legs=6,Parts=Legs*3+2;
        /// <summary>The plates' cells on the grid (each a Pitch square, x then z from its low corner) over the shell's footprint.</summary>
        public static readonly Vector2[] Cells=MakeCells();
        public static int Plates=>Cells.Length;

        // Colours (sRGB): the glaze, its bevel's highlight, the chipped biscuit, a depth-marker tile, the grout, the wet underside and
        // joints, the glass eyes (no outline: alpha 0).
        public static readonly Vector4 Glaze=Rgb(0xE9EFED),GlazeEdge=Rgb(0xF7FAF9),Biscuit=Rgb(0xC4AF91),Marker=Rgb(0x1E3A4F),Grout=Rgb(0x5F918C),
            Wet=Rgb(0x2E4F4C),Joint=Rgb(0x4A7A75),Eye=Rgb(0x0A0D10,0),Stain=Rgb(0x6A2C24);

        /// <summary>A mesh made here: positions, normals, colours (sRGB; alpha 1 outlined, 0 not) and triangles (Unity's winding).</summary>
        public sealed class Shape{public Vector3[] Pos,Nor;public Vector4[] Col;public int[] Tri;}
        /// <summary>A carapace: the grout dome with its eyes (Dome), each plate in its own frame (Plate) at its place on the body
        /// (PlateAt), and all of it in one (Living).</summary>
        public sealed class Carapace{public Shape Dome,Living;public Shape[] Plate;public Matrix4x4[] PlateAt;}

        private static Carapace[] carapaces;private static Shape[] limbs;private static Shape chip,scar;
        /// <summary>The variant's carapace (made once).</summary>
        public static Carapace Shell(int variant){carapaces??=new Carapace[Variants];int v=((variant%Variants)+Variants)%Variants;return carapaces[v]??=MakeCarapace(v);}
        /// <summary>The limbs' shapes, in Parts' order (each leg's coxa, femur and tibia, then the left and right mandibles).</summary>
        public static Shape[] Limbs()=>limbs??=MakeLimbs();
        /// <summary>A chip off a plate (a hit's shard).</summary>
        public static Shape Chip()=>chip??=MakeChip();
        /// <summary>The hole its peel leaves in the wall (its rim plane's frame: y out of the wall).</summary>
        public static Shape Scar()=>scar??=MakeScar();
        public static int Variant(uint seed)=>(int)(seed%Variants);

        private static Vector4 Rgb(uint hex,float a=1)=>new Vector4(((hex>>16)&255)/255f,((hex>>8)&255)/255f,(hex&255)/255f,a);
        private static float Sm(float x)=>x<=0?0:x>=1?1:x*x*(3-2*x);
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Frac(float x)=>x-(float)Math.Floor(x);
        private static float Hash(uint seed,int i,int k){uint h=seed^unchecked((uint)(i*0x9E3779B1)+(uint)k*0x85EBCA77u);h^=h>>15;h*=0x2C1B3C6Du;h^=h>>12;h*=0x297A2D39u;h^=h>>15;return (h&0xFFFFFF)/16777216f;}
        private static Vector3 Unit(Vector3 v,Vector3 fallback){float l=v.Length();return l>1e-6f?v/l:fallback;}
        /// <summary>A frame from its axes and origin (row vectors: Vector3.Transform(p, m) = p.x right + p.y up + p.z ahead + at).</summary>
        public static Matrix4x4 Frame(Vector3 right,Vector3 up,Vector3 ahead,Vector3 at)=>new Matrix4x4(right.X,right.Y,right.Z,0,up.X,up.Y,up.Z,0,ahead.X,ahead.Y,ahead.Z,0,at.X,at.Y,at.Z,1);
        /// <summary>A frame at `at` whose z runs along `ahead`, its y toward `upHint`.</summary>
        public static Matrix4x4 Along(Vector3 at,Vector3 ahead,Vector3 upHint)
        {
            var f=Unit(ahead,Vector3.UnitZ);var r=Vector3.Cross(upHint,f);if(r.LengthSquared()<1e-8f)r=Vector3.Cross(Vector3.UnitX,f);r=Vector3.Normalize(r);
            return Frame(r,Vector3.Cross(f,r),f,at);
        }

        /// <summary>The dome's height over (x, z) and its normal there.</summary>
        public static float DomeY(float x,float z,out Vector3 n)
        {
            float u=(x/A)*(x/A)+(z/B)*(z/B),q=Math.Max(.01f,1-u),r=(float)Math.Sqrt(q),h=Crown-Rim;
            n=Vector3.Normalize(new Vector3(h*x/(A*A*r),1,h*z/(B*B*r)));return Rim+h*r;
        }
        // A point of the footprint drawn in to the shell's edge (u .99) when it lies past it.
        private static Vector2 Inside(Vector2 p){float u=(p.X/A)*(p.X/A)+(p.Y/B)*(p.Y/B);return u>.99f?p*(float)Math.Sqrt(.99f/u):p;}
        private static Vector2[] MakeCells()
        {
            // Every cell with a fair share of it on the shell (the edge ones cut to its rim, as tiles are cut round a curve).
            var g=new List<Vector2>();
            for(int i=-4;i<4;i++)for(int j=-4;j<4;j++)
            {
                var lo=new Vector2(i*Pitch,j*Pitch);int inside=0;
                for(int a=0;a<=4;a++)for(int b=0;b<=4;b++){var q=lo+new Vector2(a,b)*(Pitch/4);if((q.X/A)*(q.X/A)+(q.Y/B)*(q.Y/B)<.99f)inside++;}
                if(inside>=9)g.Add(lo);
            }
            return g.ToArray();
        }

        // ---- building ----
        private sealed class Builder
        {
            public readonly List<Vector3> P=new List<Vector3>(),N=new List<Vector3>();public readonly List<Vector4> C=new List<Vector4>();public readonly List<int> T=new List<int>();
            /// <summary>A flat convex polygon facing `want` (its winding turned to face it).</summary>
            public void Face(Vector3[] poly,Vector3 want,Vector4 col)
            {
                var n=Vector3.Zero;for(int i=1;i+1<poly.Length;i++)n+=Vector3.Cross(poly[i]-poly[0],poly[i+1]-poly[0]);if(n.LengthSquared()<1e-16f)return;
                bool flip=Vector3.Dot(n,want)<0;var nn=Vector3.Normalize(flip?-n:n);int b=P.Count;
                foreach(var p in poly){P.Add(p);N.Add(nn);C.Add(col);}
                for(int i=1;i+1<poly.Length;i++){T.Add(b);if(flip){T.Add(b+i+1);T.Add(b+i);}else{T.Add(b+i);T.Add(b+i+1);}}
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 want,Vector4 col)=>Face(new[]{a,b,c,d},want,col);
            /// <summary>A smooth band between rings of points (each ring the same length, closed round), its normals averaged,
            /// facing away from `center`.</summary>
            public void Rings(Vector3[][] rings,Vector4[] cols,Vector3 center)
            {
                int k=rings[0].Length,b=P.Count;var acc=new Vector3[rings.Length*k];
                for(int r=0;r<rings.Length;r++)for(int i=0;i<k;i++){P.Add(rings[r][i]);C.Add(cols[r]);}
                for(int r=0;r+1<rings.Length;r++)for(int i=0;i<k;i++)
                {
                    int a0=r*k+i,a1=r*k+(i+1)%k,b0=(r+1)*k+i,b1=(r+1)*k+(i+1)%k;
                    foreach(var (x,y,z) in new[]{(a0,a1,b1),(a0,b1,b0)})
                    {
                        Vector3 px=P[b+x],py=P[b+y],pz=P[b+z];var n=Vector3.Cross(py-px,pz-px);
                        bool flip=Vector3.Dot(n,(px+py+pz)/3-center)<0;if(flip)n=-n;acc[x]+=n;acc[y]+=n;acc[z]+=n;
                        T.Add(b+x);if(flip){T.Add(b+z);T.Add(b+y);}else{T.Add(b+y);T.Add(b+z);}
                    }
                }
                for(int i=0;i<acc.Length;i++)N.Add(Unit(acc[i],Unit(P[b+i]-center,Vector3.UnitY)));
            }
            public void Add(Shape s,Matrix4x4 m)
            {
                int b=P.Count;for(int i=0;i<s.Pos.Length;i++){P.Add(Vector3.Transform(s.Pos[i],m));N.Add(Unit(Vector3.TransformNormal(s.Nor[i],m),Vector3.UnitY));C.Add(s.Col[i]);}
                foreach(int t in s.Tri)T.Add(b+t);
            }
            public Shape Done()=>new Shape{Pos=P.ToArray(),Nor=N.ToArray(),Col=C.ToArray(),Tri=T.ToArray()};
        }

        /// <summary>A beveled slab over a convex outline (x, z) from y 0 to `thick`: its top and bevel `top`, the bevel a little
        /// brighter, its sides `side` (the edges listed in `cut` the biscuit), its underside `under`.</summary>
        private static void Slab(Builder b,IList<Vector2> outline,float thick,float bevel,Vector4 top,Vector4 edge,Vector4 side,Vector4 under,ICollection<int> cut,Matrix4x4 m)
        {
            int n=outline.Count;var c=Vector2.Zero;foreach(var o in outline)c+=o;c/=n;float reach=0;foreach(var o in outline)reach=Math.Max(reach,(o-c).Length());
            float s=Math.Max(.2f,(reach-bevel)/reach);Vector3 P(Vector2 v,float y)=>Vector3.Transform(new Vector3(v.X,y,v.Y),m);
            Vector3 up=Vector3.TransformNormal(Vector3.UnitY,m);var topPoly=new Vector3[n];var bottom=new Vector3[n];
            for(int i=0;i<n;i++){topPoly[i]=P(c+(outline[i]-c)*s,thick);bottom[i]=P(outline[i],0);}
            b.Face(topPoly,up,top);b.Face(bottom,-up,under);
            for(int i=0;i<n;i++)
            {
                int j=(i+1)%n;var d=outline[j]-outline[i];var o=new Vector2(d.Y,-d.X);if(Vector2.Dot(o,(outline[i]+outline[j])*.5f-c)<0)o=-o;
                var outward=Vector3.TransformNormal(new Vector3(o.X,0,o.Y),m);
                var ti=c+(outline[i]-c)*s;var tj=c+(outline[j]-c)*s;
                b.Quad(P(outline[i],thick-bevel),P(outline[j],thick-bevel),P(tj,thick),P(ti,thick),outward+up,edge);
                b.Quad(P(outline[i],0),P(outline[j],0),P(outline[j],thick-bevel),P(outline[i],thick-bevel),outward,cut!=null&&cut.Contains(i)?Biscuit:side);
            }
        }

        private static Carapace MakeCarapace(int v)
        {
            uint seed=0xC0FFEEu+(uint)v*7919u;var car=new Carapace{Plate=new Shape[Plates],PlateAt=new Matrix4x4[Plates]};
            // The grout under the plates (a little below them: the lines between show it), its skirt down to the belly, the flat wet
            // underside.
            var dome=new Builder();const int K=32;var rings=new List<Vector3[]>();var cols=new List<Vector4>();
            float[] rho={0,.2f,.4f,.58f,.74f,.86f,.94f,.99f};
            foreach(float r in rho){var ring=new Vector3[K];for(int i=0;i<K;i++){float a=i*(float)Math.PI*2/K,x=A*r*(float)Math.Cos(a),z=B*r*(float)Math.Sin(a);ring[i]=new Vector3(x,DomeY(x,z,out var n)-.007f,z)-new Vector3(n.X,0,n.Z)*.004f;}rings.Add(ring);cols.Add(Grout);}
            foreach(var (r,y,col) in new[]{(.97f,Rim-.03f,Grout),(.9f,Belly+.012f,Wet),(.84f,Belly,Wet),(.5f,Belly-.004f,Wet),(0f,Belly-.006f,Wet)})
            {var ring=new Vector3[K];for(int i=0;i<K;i++){float a=i*(float)Math.PI*2/K;ring[i]=new Vector3(A*r*(float)Math.Cos(a),y,B*r*(float)Math.Sin(a));}rings.Add(ring);cols.Add(col);}
            dome.Rings(rings.ToArray(),cols.ToArray(),new Vector3(0,(Rim+Belly)*.5f,0));
            // Four black glass eyes on the grout under the front lip of the shell, two big and two small, square like tiles.
            foreach(var (x,y,h) in new[]{(-.03f,Rim-.022f,.011f),(.03f,Rim-.022f,.011f),(-.072f,Rim-.016f,.007f),(.072f,Rim-.016f,.007f)})
            {
                float ra=A*.975f,rb=B*.975f,z=rb*(float)Math.Sqrt(Math.Max(0,1-(x/ra)*(x/ra)));var en=Vector3.Normalize(new Vector3(x/(ra*ra),0,z/(rb*rb)));
                var er=Vector3.Normalize(Vector3.Cross(en,Vector3.UnitY));var basis=Frame(er,en,Vector3.Cross(er,en),new Vector3(x,y,z)+en*.002f);
                Slab(dome,new[]{new Vector2(h,h),new Vector2(-h,h),new Vector2(-h,-h),new Vector2(h,-h)},.006f,.002f,Eye,Eye,Eye,Eye,null,basis);
            }
            car.Dome=dome.Done();
            // The plates: one per cell, wrapping the dome to its rim with a grout line between; a few chipped at a corner to the
            // biscuit, one dark depth marker, a few a little loose.
            var living=new Builder();living.Add(car.Dome,Matrix4x4.Identity);int marker=(int)(Hash(seed,99,0)*Plates);
            for(int i=0;i<Plates;i++)
            {
                var lo=Cells[i]+new Vector2(Gap*.5f,Gap*.5f);float span=Pitch-Gap;var mid=Inside(lo+new Vector2(span*.5f,span*.5f));
                float cy=DomeY(mid.X,mid.Y,out var cn);var f0=Unit(Vector3.UnitZ-cn*cn.Z,Vector3.UnitZ);var r0=Vector3.Normalize(Vector3.Cross(cn,f0));var f1=Vector3.Cross(r0,cn);
                float lift=Hash(seed,i,2)<.2f?.002f+.004f*Hash(seed,i,3):0,tilt=lift>0?(Hash(seed,i,4)-.5f)*.12f:0;
                var u1=Vector3.Normalize(cn*(float)Math.Cos(tilt)+r0*(float)Math.Sin(tilt));var r1=Vector3.Normalize(Vector3.Cross(u1,f1));var f2=Vector3.Cross(r1,u1);
                var center=new Vector3(mid.X,cy,mid.Y);car.PlateAt[i]=Frame(r1,u1,f2,center+cn*lift);
                int chip=Hash(seed,i,5)<.3f?(int)(Hash(seed,i,6)*4)%4:-1;bool dark=i==marker&&v!=2;
                float shade=.95f+.07f*Hash(seed,i,7);var glaze=dark?Marker:new Vector4(Glaze.X*shade,Glaze.Y*shade,Glaze.Z*shade*(Hash(seed,i,8)<.2f?.94f:1),1);
                var edge=dark?new Vector4(Marker.X*1.15f,Marker.Y*1.15f,Marker.Z*1.15f,1):GlazeEdge;
                car.Plate[i]=CurvedPlate(lo,span,center,r1,u1,f2,glaze,edge,chip);
                living.Add(car.Plate[i],car.PlateAt[i]);
            }
            car.Living=living.Done();return car;
        }
        /// <summary>A plate laid on the dome over the cell from `lo` (`span` square, drawn in to the rim), its top `top` with a rounded
        /// edge `edge`, its sides grout (the chipped corner's, `chip` 0 to 3 or -1, the biscuit), in its frame at `center` (r, u, f).</summary>
        private static Shape CurvedPlate(Vector2 lo,float span,Vector3 center,Vector3 r,Vector3 u,Vector3 f,Vector4 top,Vector4 edge,int chip)
        {
            const int N=3;var b=new Builder();var topP=new Vector3[N+1,N+1];var topN=new Vector3[N+1,N+1];var bottom=new Vector3[N+1,N+1];
            Vector3 Local(Vector3 w){var d=w-center;return new Vector3(Vector3.Dot(d,r),Vector3.Dot(d,u),Vector3.Dot(d,f));}
            Vector3 LocalN(Vector3 n)=>Vector3.Normalize(new Vector3(Vector3.Dot(n,r),Vector3.Dot(n,u),Vector3.Dot(n,f)));
            var mid=Inside(lo+new Vector2(span*.5f,span*.5f));int[] corner={0,N,N*(N+1)+N,N*(N+1)};
            for(int a=0;a<=N;a++)for(int c=0;c<=N;c++)
            {
                var q=Inside(lo+new Vector2(span*a/N,span*c/N));bool rim=a==0||c==0||a==N||c==N;
                // (The chipped corner drawn in toward the middle.)
                int idx=a*(N+1)+c;if(chip>=0&&idx==corner[chip])q+=(mid-q)*.25f;
                if(rim)q+=Unit2(mid-q)*(Bevel*.4f);
                float y=DomeY(q.X,q.Y,out var n);var s0=new Vector3(q.X,y,q.Y);
                topP[a,c]=Local(s0+n*(rim?Thick-Bevel:Thick));topN[a,c]=LocalN(n);bottom[a,c]=Local(s0-n*.002f);
            }
            // The top: a smooth curved face, its rim fallen away a little (a glazed tile's rounded edge); the underside the same
            // grid, wet.
            for(int side=0;side<2;side++)
            {
                int bt=b.P.Count;
                for(int a=0;a<=N;a++)for(int c=0;c<=N;c++){bool rim=a==0||c==0||a==N||c==N;b.P.Add(side==0?topP[a,c]:bottom[a,c]);b.N.Add(side==0?topN[a,c]:-topN[a,c]);b.C.Add(side==1?Wet:rim?edge:top);}
                for(int a=0;a<N;a++)for(int c=0;c<N;c++)
                {
                    int i00=bt+a*(N+1)+c,i10=bt+(a+1)*(N+1)+c,i01=bt+a*(N+1)+c+1,i11=bt+(a+1)*(N+1)+c+1;
                    foreach(var (x,y,z) in new[]{(i00,i10,i11),(i00,i11,i01)})
                    {
                        var nn=Vector3.Cross(b.P[y]-b.P[x],b.P[z]-b.P[x]);if(nn.LengthSquared()<1e-12f)continue;bool flip=Vector3.Dot(nn,b.N[x])<0;
                        b.T.Add(x);if(flip){b.T.Add(z);b.T.Add(y);}else{b.T.Add(y);b.T.Add(z);}
                    }
                }
            }
            // The sides round the edge (unglazed: grout-dark, the chipped corner's the biscuit).
            var ring=new List<(int a,int c)>();for(int k=0;k<N;k++)ring.Add((k,0));for(int k=0;k<N;k++)ring.Add((N,k));for(int k=N;k>0;k--)ring.Add((k,N));for(int k=N;k>0;k--)ring.Add((0,k));
            var cen=Vector3.Zero;foreach(var (a,c) in ring)cen+=topP[a,c];cen/=ring.Count;
            for(int k=0;k<ring.Count;k++)
            {
                var (a0,c0)=ring[k];var (a1,c1)=ring[(k+1)%ring.Count];var mid3=(topP[a0,c0]+topP[a1,c1])*.5f;var outward=mid3-cen;outward.Y=0;
                bool chipped=chip>=0&&(a0*(N+1)+c0==corner[chip]||a1*(N+1)+c1==corner[chip]);
                b.Quad(topP[a0,c0],topP[a1,c1],bottom[a1,c1],bottom[a0,c0],outward,chipped?Biscuit:Grout);
            }
            return b.Done();
        }
        private static Vector2 Unit2(Vector2 v){float l=v.Length();return l>1e-6f?v/l:Vector2.Zero;}

        /// <summary>A tapered leg segment along z from 0 to `len`: its top `top`, its sides `side`, its underside wet; a knob at its root
        /// (its joint); a shard of tile for a claw at its end when `claw` (the tibia's).</summary>
        private static Shape Segment(float len,float w0,float h0,float w1,float h1,float knob,Vector4 top,Vector4 side,bool claw)
        {
            var b=new Builder();
            Vector3[] Ring(float z,float w,float h)=>new[]{new Vector3(-w/2,-h/2,z),new Vector3(w/2,-h/2,z),new Vector3(w/2,h/2,z),new Vector3(-w/2,h/2,z)};
            var r0=Ring(0,w0,h0);var r1=Ring(len,w1,h1);
            b.Quad(r0[3],r0[2],r1[2],r1[3],Vector3.UnitY,top);b.Quad(r0[0],r0[1],r1[1],r1[0],-Vector3.UnitY,Wet);
            b.Quad(r0[1],r0[2],r1[2],r1[1],Vector3.UnitX,side);b.Quad(r0[0],r0[3],r1[3],r1[0],-Vector3.UnitX,side);
            b.Face(r0,-Vector3.UnitZ,side);b.Face(r1,Vector3.UnitZ,side);
            if(knob>0)
            {
                Vector3[] o={new Vector3(knob,0,0),new Vector3(0,knob,0),new Vector3(-knob,0,0),new Vector3(0,-knob,0)};
                for(int i=0;i<4;i++){var a=o[i];var c=o[(i+1)%4];var front=new Vector3(0,0,knob);b.Face(new[]{a,c,front},a+c+front,Joint);b.Face(new[]{a,c,-front},a+c-front,Joint);}
            }
            // (The claw: a thin glazed shard, broad at the leg's end and coming to a point, its broken edges the biscuit.)
            if(claw)Slab(b,new[]{new Vector2(-.011f,0),new Vector2(.011f,0),new Vector2(.003f,.07f)},.005f,.0015f,Glaze,GlazeEdge,Biscuit,Biscuit,null,Frame(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ,new Vector3(0,-.0025f,len-.035f)));
            return b.Done();
        }
        /// <summary>A mandible: a shard of broken tile curving in to its stained tip (hinge frame: z ahead, x out to its side for
        /// `side` +1).</summary>
        private static Shape Mandible(int side)
        {
            var b=new Builder();var pts=new[]{new Vector2(-.006f,0),new Vector2(.016f,0),new Vector2(.026f,.045f),new Vector2(.016f,.08f),new Vector2(-.012f,.11f),new Vector2(-.01f,.06f)};
            var outline=new Vector2[pts.Length];for(int i=0;i<pts.Length;i++)outline[i]=new Vector2(pts[i].X*side,pts[i].Y);
            // (Split in two so each half stays convex: the root and the curved tip.)
            Slab(b,new[]{outline[0],outline[1],outline[2],outline[5]},.009f,.002f,Glaze,GlazeEdge,Biscuit,Biscuit,null,Frame(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ,new Vector3(0,-.0045f,0)));
            Slab(b,new[]{outline[5],outline[2],outline[3],outline[4]},.009f,.002f,Stain*.6f+Glaze*.4f,GlazeEdge,Biscuit,Biscuit,null,Frame(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ,new Vector3(0,-.0045f,0)));
            return b.Done();
        }
        private static Shape[] MakeLimbs()
        {
            var s=new Shape[Parts];var coxa=Segment(Coxa,.034f,.03f,.028f,.026f,.024f,Grout,Grout,false);var femur=Segment(Femur,.022f,.022f,.016f,.016f,.02f,Glaze,Grout,false);
            var tibia=Segment(Tibia-.035f,.016f,.016f,.006f,.006f,.016f,Joint,Wet,true);
            for(int i=0;i<Legs;i++){s[i*3]=coxa;s[i*3+1]=femur;s[i*3+2]=tibia;}
            s[Legs*3]=Mandible(-1);s[Legs*3+1]=Mandible(1);return s;
        }
        private static Shape MakeChip()
        {
            var b=new Builder();Slab(b,new[]{new Vector2(.016f,0),new Vector2(-.01f,.012f),new Vector2(-.008f,-.014f)},.008f,.002f,Glaze,GlazeEdge,Biscuit,Biscuit,null,Matrix4x4.Identity);return b.Done();
        }
        /// <summary>The hole its peel leaves: the bare wet grout where its plates were (a ragged ellipse, a hair proud of the wall)
        /// with a few broken plate edges still in it.</summary>
        private static Shape MakeScar()
        {
            var b=new Builder();const int K=22;var ring=new Vector3[K];uint seed=0x5CA2u;
            for(int i=0;i<K;i++){float a=i*(float)Math.PI*2/K,r=.92f+.16f*Hash(seed,i,0);ring[i]=new Vector3(A*r*(float)Math.Cos(a),.002f,B*r*(float)Math.Sin(a));}
            var center=new Vector3(0,.002f,0);for(int i=0;i<K;i++)b.Face(new[]{center,ring[i],ring[(i+1)%K]},Vector3.UnitY,Rgb(0x23403D,0));
            for(int i=0;i<5;i++)
            {
                float a=Hash(seed,i,1)*(float)Math.PI*2;var at=new Vector3(A*.95f*(float)Math.Cos(a),.0f,B*.95f*(float)Math.Sin(a));
                Slab(b,new[]{new Vector2(.02f,0),new Vector2(-.016f,.02f),new Vector2(-.012f,-.018f)},.007f,.002f,Glaze,GlazeEdge,Biscuit,Biscuit,null,Frame(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ,at));
            }
            return b.Done();
        }

        // ---- the legs' places ----
        /// <summary>Leg i's hip on the body, its rest direction out over the floor (horizontal), and its foot at rest (add frame,
        /// standing). Legs 0 to 2 left (front to back), 3 to 5 right.</summary>
        public static void Leg(int i,out Vector3 hip,out Vector3 dir,out Vector3 rest)
        {
            int row=i%3;float sx=i<3?-1:1,z=row==0?.11f:row==1?0:-.11f,a=(row==0?45:row==1?5:-40)*(float)Math.PI/180;
            hip=new Vector3(sx*.17f,.16f,z);dir=new Vector3(sx*(float)Math.Cos(a),0,(float)Math.Sin(a));rest=new Vector3(hip.X+dir.X*Spread,0,hip.Z+dir.Z*Spread);
        }
        /// <summary>The tripods: legs 0, 4 and 2 (left front, right middle, left back) step together, the other three half a cycle on.</summary>
        public static float Offset(int i)=>i==0||i==4||i==2?0:.5f;
        public static float Stride(float speed)=>Math.Min(.56f,.2f+.065f*speed);

        /// <summary>The scuttle's state, stepped on the presented root's motion (the add frame's velocity and turn).</summary>
        public struct Gait{public float Phase,Speed,Turn;public Vector2 Dir;public bool Known;}
        public static void Step(ref Gait g,Vector2 velocity,float yawRate,float dt)
        {
            if(!(dt>0))return;float v=velocity.Length(),k=1-(float)Math.Exp(-dt*10);
            if(!g.Known){g.Known=true;g.Dir=Vector2.UnitY;}
            g.Speed+=(v-g.Speed)*k;g.Turn+=((float.IsFinite(yawRate)?yawRate:0)-g.Turn)*k;
            if(v>.05f){var d=g.Dir+(velocity/v-g.Dir)*(1-(float)Math.Exp(-dt*12));g.Dir=d.LengthSquared()>1e-6f?Vector2.Normalize(d):velocity/v;}
            float travel=g.Speed+Math.Abs(g.Turn)*.35f;g.Phase=Frac(g.Phase+travel*dt/Stride(g.Speed));
        }

        /// <summary>What the authority has it doing this frame (the add frame's directions and points).</summary>
        public struct Moment
        {
            /// <summary>Dormant in its wall (or flat in the deck when Wall is zero); the wake's age (NaN none).</summary>
            public bool Dormant;public float WakeAge;
            /// <summary>A bite's age, contact and recovery (NaN none); the pilot it leaps at (add frame), if any.</summary>
            public float BiteAge,Strike,Recover;public bool HasTarget;public Vector3 Target;
            /// <summary>The wall it sleeps in: the direction to its face (horizontal; zero for the deck) and how far.</summary>
            public Vector3 Wall;public float WallDistance;
            /// <summary>The last hit's age (NaN none) and its push (horizontal, add frame).</summary>
            public float HitAge;public Vector2 HitPush;
        }
        /// <summary>A posed Tiler: its body in the add frame (Body: may flatten and scale), its limbs in the body's (Parts' order),
        /// how present it is (1 alive; the death's shrink).</summary>
        public sealed class Pose{public Matrix4x4 Body;public readonly Matrix4x4[] Part=new Matrix4x4[Parts];public float Scale=1;}

        /// <summary>The dormant patch: where its rim plane lies (add frame), turned so its back faces out of the wall, flattened.</summary>
        public static void Patch(uint seed,in Moment m,out Vector3 at,out Quaternion turn)
        {
            if(m.Wall.LengthSquared()<1e-6f){at=new Vector3((Hash(seed,1,9)-.5f)*.3f,Proud,(Hash(seed,2,9)-.5f)*.3f);turn=Quaternion.CreateFromAxisAngle(Vector3.UnitY,Hash(seed,3,9)*6.283f);return;}
            var wall=Vector3.Normalize(new Vector3(m.Wall.X,0,m.Wall.Z));var outward=-wall;var along=Vector3.Cross(Vector3.UnitY,wall);
            float lateral=((int)(seed%3)-1)*.42f+(Hash(seed,4,9)-.5f)*.1f,height=.5f+.3f*Hash(seed,5,9);
            at=wall*m.WallDistance+along*lateral+Vector3.UnitY*height+outward*Proud;
            // (Its back out of the wall, its head up the wall a little askew.)
            var ahead=Vector3.Normalize(Vector3.UnitY*(float)Math.Cos(.3f*(Hash(seed,6,9)-.5f))+along*(float)Math.Sin(.3f*(Hash(seed,6,9)-.5f)));
            turn=Quaternion.CreateFromRotationMatrix(Frame(Vector3.Cross(outward,ahead),outward,ahead,Vector3.Zero));
        }
        private static Matrix4x4 BodyMatrix(Vector3 at,Quaternion turn,float flat,float sink)=>Matrix4x4.CreateTranslation(0,-sink,0)*Matrix4x4.CreateScale(1,flat,1)*Matrix4x4.CreateFromQuaternion(turn)*Matrix4x4.CreateTranslation(at);

        /// <summary>A living Tiler at `time` (s) on its gait and the authority's moment.</summary>
        public static void Living(Pose p,in Gait g,uint seed,float time,in Moment m)
        {
            p.Scale=1;float moving=Sm((g.Speed+Math.Abs(g.Turn)*.35f)/.5f),ph=g.Phase*(float)Math.PI*2;
            // The standing body: a bob on each tripod's step, a roll from side to side, nosing down as it runs, banking into turns; at
            // rest it breathes.
            float bob=.011f*moving*Math.Abs((float)Math.Sin(ph))+.004f*(1-moving)*(float)Math.Sin(time*2.3f+seed%7);
            float roll=.06f*moving*(float)Math.Sin(ph)-.08f*Math.Max(-1,Math.Min(1,g.Turn/3)),pitch=.06f*Sm(g.Speed/4);
            var stand=new Vector3(0,bob,0);var turn=Quaternion.CreateFromYawPitchRoll(0,pitch,roll);float flat=1,sink=0;
            // Hit: shoved along the push, kicked over away from it.
            if(m.HitAge>=0&&m.HitAge<.4f){float k=(float)Math.Exp(-m.HitAge*12)*Sm(m.HitAge/.03f);stand+=new Vector3(m.HitPush.X,0,m.HitPush.Y)*.06f*k;turn=Quaternion.CreateFromYawPitchRoll(0,-.14f*k*m.HitPush.Y,.14f*k*m.HitPush.X)*turn;}
            // The bite: crouched and reared through the first half of the wind-up, leaping at the pilot through the second, the
            // mandibles snapping shut on contact, then a hop back to where it stands.
            float air=0,open=.1f+.06f*(float)Math.Sin(time*7.1f+seed%5)*(1-moving);Vector3 leap=Vector3.Zero;
            if(m.BiteAge>=0&&m.BiteAge<=m.Recover+.3f&&m.Strike>0)
            {
                float a=m.BiteAge,s=m.Strike,c0=s*.5f;var to=m.HasTarget?new Vector3(m.Target.X,0,m.Target.Z):Vector3.UnitZ*1.5f;float dist=to.Length();var dir=dist>1e-3f?to/dist:Vector3.UnitZ;
                float reach=Math.Max(.2f,Math.Min(LeapMost,dist-.45f));
                float crouch=a<c0?Sm(a/c0):1-Sm((a-c0)/.06f),p1=a<c0?0:Sm((a-c0)/(s-c0)),p2=a<s?0:Sm((a-s)/.3f);
                air=a>c0-.02f&&a<s+.32f?Sm((a-c0+.02f)/.05f)*(1-Sm((a-s-.24f)/.08f)):0;
                leap=dir*reach*(p1-p2)+Vector3.UnitY*(.22f*(float)Math.Sin(Math.PI*p1)*(1-p2)+.1f*(float)Math.Sin(Math.PI*p2));
                stand+=leap-Vector3.UnitY*.06f*crouch;
                turn=Quaternion.CreateFromYawPitchRoll(0,-.32f*crouch+.28f*p1*(1-p2),0)*turn;
                open=a<s-.04f?Math.Max(open,Sm(a/(s*.7f))):a<s+.03f?.05f:a<s+.4f?.3f*(1-Sm((a-s-.1f)/.3f)):open;
            }
            var body=BodyMatrix(stand,turn,flat,sink);
            // Dormant, and peeling out of the wall: from the patch to the stand over the wake's .6 s.
            float unfold=1;Vector3 patchAt=default;Quaternion patchTurn=default;bool patch=m.Dormant||m.WakeAge>=0&&m.WakeAge<Peel;
            if(patch)
            {
                Patch(seed,m,out patchAt,out patchTurn);
                float t=m.Dormant?0:m.WakeAge/Peel;
                if(t<.3f){float push=Sm(t/.3f),shake=t>0?.03f*(float)Math.Sin(t*95)*push:0;var normal=Vector3.Transform(Vector3.UnitY,patchTurn);
                    body=BodyMatrix(patchAt+normal*.03f*push,Quaternion.CreateFromAxisAngle(normal,shake)*patchTurn,Flat,Rim);unfold=0;}
                else if(t<.85f)
                {
                    float u=Sm((t-.3f)/.55f);var at=Vector3.Lerp(patchAt+Vector3.Transform(Vector3.UnitY,patchTurn)*.03f,stand,u)+Vector3.UnitY*.12f*(float)Math.Sin(Math.PI*u);
                    body=BodyMatrix(at,Quaternion.Slerp(patchTurn,turn,u),Flat+(1-Flat)*u,Rim*(1-u));unfold=Sm((t-.25f)/.45f);
                }
                else{float u=(t-.85f)/.15f;body=BodyMatrix(stand-Vector3.UnitY*.025f*(float)Math.Sin(Math.PI*Clamp01(u)),turn,1,0);}
            }
            p.Body=body;Matrix4x4.Invert(body,out var inv);
            // The legs: each foot planted on the floor through its stance and swung forward in an arc (the add frame), carried with
            // the body in a leap (stretched ahead and back), folded under it in the wall; a twitch now and then at rest.
            float stride=Stride(g.Speed),travel=g.Speed+Math.Abs(g.Turn)*.35f,lift=Math.Min(.1f,.04f+.012f*g.Speed);
            int twitchLeg=(int)(Hash(seed,(int)(time/1.3f),11)*Legs)%Legs;float twitchAge=time%1.3f;
            for(int i=0;i<Legs;i++)
            {
                Leg(i,out var hip,out var dir,out var rest);
                var excursion=Vector2.Zero;
                if(travel>1e-3f){var v=g.Dir*g.Speed+g.Turn*new Vector2(rest.Z,-rest.X);excursion=v/travel*stride*.5f*moving;}
                float q=Frac(g.Phase+Offset(i)),off,up=0;
                if(q<.5f)off=.5f-q/.5f;else{float w=(q-.5f)/.5f;off=w-.5f;up=lift*(float)Math.Sin(Math.PI*w)*moving;}
                if(moving<.5f&&i==twitchLeg&&twitchAge<.28f)up+=.035f*(float)Math.Sin(Math.PI*twitchAge/.28f)*(1-moving*2);
                var planted=new Vector3(rest.X+excursion.X*off,up,rest.Z+excursion.Y*off);
                var foot=Vector3.Transform(planted,inv);
                if(air>0)
                {
                    int row=i%3;var flying=hip+dir*.24f+(row==0?new Vector3(0,.02f,.16f):row==1?new Vector3(0,-.06f,.02f):new Vector3(0,-.04f,-.14f));
                    foot=Vector3.Lerp(foot,flying,air);
                }
                var folded=hip+dir*.22f-Vector3.UnitY*.06f;
                if(unfold<1)foot=Vector3.Lerp(folded,foot,unfold);
                var pole=Unit(Vector3.Lerp(-Vector3.UnitY+dir*.2f,Vector3.UnitY*.8f+dir*.2f,unfold),dir);
                LegChain(p,i,hip,dir,foot,pole);
            }
            // The mandibles: hinged under the front of the shell, opened outward and down.
            for(int k=0;k<2;k++)
            {
                float side=k==0?-1:1,ang=(.08f+.55f*open)*side;var hinge=new Vector3(side*.04f,Rim-.028f,B-.012f);
                var q=Quaternion.CreateFromYawPitchRoll(ang,.15f+.25f*open,0);p.Part[Legs*3+k]=Matrix4x4.CreateFromQuaternion(q)*Matrix4x4.CreateTranslation(hinge);
            }
        }
        /// <summary>Leg i's coxa out from its hip, then the femur and tibia to `foot` (body frame), the knee toward `pole`.</summary>
        private static void LegChain(Pose p,int i,Vector3 hip,Vector3 dir,Vector3 foot,Vector3 pole)
        {
            var c=hip+dir*Coxa+Vector3.UnitY*.01f;var to=foot-c;float d=to.Length();var u=d>1e-5f?to/d:dir;
            d=Math.Max(Math.Abs(Femur-Tibia)+.01f,Math.Min(Femur+Tibia-.002f,d));
            float a=(Femur*Femur-Tibia*Tibia+d*d)/(2*d),h=(float)Math.Sqrt(Math.Max(0,Femur*Femur-a*a));
            var bend=Unit(pole-u*Vector3.Dot(pole,u),Unit(Vector3.Cross(u,Vector3.Cross(Vector3.UnitY,u)),Vector3.UnitY));
            var knee=c+u*a+bend*h;var end=c+u*d;var top=Vector3.UnitY+dir*.6f;
            p.Part[i*3]=Along(hip,c-hip,top);p.Part[i*3+1]=Along(c,knee-c,top);p.Part[i*3+2]=Along(knee,end-knee,top);
        }

        /// <summary>A dying Tiler `age` s after its death: its grout slumping and crumbling with its legs splayed (Body, Part,
        /// Scale), and each plate flying from its place on the shell, bouncing once and settling on the floor (`plates`, add frame),
        /// sinking away after Settle s; false once it is gone.</summary>
        public static bool Dying(Pose p,uint seed,float age,Matrix4x4[] plates)
        {
            if(!(age>=0)||age>=GoneAt)return false;
            float slump=Sm(age/.3f),crumble=Sm((age-.45f)/1f);p.Scale=1-crumble;
            // (The legs are posed on the slumped body before it crumbles to nothing: its shrink has no inverse at the end.)
            var slumped=Matrix4x4.CreateScale(1,1-.55f*slump,1)*Matrix4x4.CreateTranslation(0,-(Belly-.01f)*slump,0);
            p.Body=Matrix4x4.CreateScale(Math.Max(.001f,1-crumble))*slumped;Matrix4x4.Invert(slumped,out var inv);
            for(int i=0;i<Legs;i++){Leg(i,out var hip,out var dir,out var rest);var foot=Vector3.Transform(rest+new Vector3(dir.X,0,dir.Z)*.1f*slump,inv);LegChain(p,i,hip,dir,foot,Vector3.UnitY*.5f+dir);}
            for(int k=0;k<2;k++){float side=k==0?-1:1;p.Part[Legs*3+k]=Matrix4x4.CreateFromYawPitchRoll(.5f*side,.4f,0)*Matrix4x4.CreateTranslation(new Vector3(side*.04f,Rim-.028f,B-.012f));}
            var shell=Shell(Variant(seed));const float g=9.8f;float floor=Thick*.5f,shrink=1-Sm((age-Settle)/(GoneAt-Settle-.1f));
            for(int i=0;i<Plates&&i<plates.Length;i++)
            {
                var at=shell.PlateAt[i];var start=new Vector3(at.M41,at.M42,at.M43);var n=new Vector3(at.M21,at.M22,at.M23);
                var v0=n*(1.1f+1.3f*Hash(seed,i,20))+new Vector3((Hash(seed,i,21)-.5f)*1.1f,1.3f+1.4f*Hash(seed,i,22),(Hash(seed,i,23)-.5f)*1.1f);
                float t1=(v0.Y+(float)Math.Sqrt(v0.Y*v0.Y+2*g*Math.Max(0,start.Y-floor)))/g,vb=.28f*Math.Abs(v0.Y-g*t1),t2=t1+2*vb/g;
                Vector3 pos;var h=new Vector2(v0.X,v0.Z);
                if(age<t1)pos=start+v0*age-Vector3.UnitY*(.5f*g*age*age);
                else
                {
                    var hit=new Vector2(start.X,start.Z)+h*t1;float tb=Math.Min(age,t2)-t1;var slide=hit+h*.4f*tb+h*.06f*Clamp01((age-t2)/.15f);
                    pos=new Vector3(slide.X,age<t2?floor+vb*tb-.5f*g*tb*tb:floor,slide.Y);
                }
                var axis=Unit(new Vector3(Hash(seed,i,24)-.5f,Hash(seed,i,25)-.5f,Hash(seed,i,26)-.5f),Vector3.UnitX);float rate=7+8*Hash(seed,i,27);
                var start3=new Matrix4x4(at.M11,at.M12,at.M13,0,at.M21,at.M22,at.M23,0,at.M31,at.M32,at.M33,0,0,0,0,1);var spin=Quaternion.CreateFromRotationMatrix(start3);
                var flying=Quaternion.CreateFromAxisAngle(axis,rate*Math.Min(age,t1))*spin;
                var rest=Quaternion.CreateFromAxisAngle(Vector3.UnitY,6.283f*Hash(seed,i,28))*(Hash(seed,i,29)<.3f?Quaternion.CreateFromAxisAngle(Vector3.UnitZ,(float)Math.PI):Quaternion.Identity);
                var turn=age<t1?flying:Quaternion.Slerp(flying,rest,Sm((age-t1)/.22f));
                plates[i]=Matrix4x4.CreateScale(shrink)*Matrix4x4.CreateTranslation(0,-Thick*.5f,0)*Matrix4x4.CreateFromQuaternion(turn)*Matrix4x4.CreateTranslation(pos-Vector3.UnitY*Thick*(1-shrink));
            }
            return true;
        }
        /// <summary>The limbs' shapes posed into one mesh's positions and normals (body frame), in Limbs' order (no allocation).</summary>
        public static void WriteLimbs(Pose p,Vector3[] pos,Vector3[] nor)
        {
            var s=Limbs();int o=0;
            for(int k=0;k<Parts;k++){var m=p.Part[k];var sh=s[k];for(int i=0;i<sh.Pos.Length;i++,o++){pos[o]=Vector3.Transform(sh.Pos[i],m);nor[o]=Vector3.Normalize(Vector3.TransformNormal(sh.Nor[i],m));}}
        }
        public static int LimbVertices{get{int n=0;foreach(var s in Limbs())n+=s.Pos.Length;return n;}}
    }
}
