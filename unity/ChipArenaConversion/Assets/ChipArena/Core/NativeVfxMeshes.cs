using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // Low-poly meshes for the effects overhaul (owner 2026-10-11: "The effects seem a bit outdated and there still seems to be
    // cube placeholders for certain impact effects"), built once in pure C# and deterministic (fixed seeds, no UnityEngine):
    // irregular rubble chunks and a crystal shard for the blast pools' Debris, Rock and Shard particles, the modelled chip
    // bomb for NativeFieldRules.Bomb, and the shock shell (sphere and dome) for NativeBlastSim.Shocks. The style is the game's
    // stylized low-poly look: flat-shaded facets, except the shell, whose smooth normals drive a fresnel rim. Every triangle is
    // wound clockwise seen from outside (Unity front faces), as NativeBlastSim.CubeTriangles: cross(b - a, c - a) points out.

    /// <summary>
    /// Mesh data: Positions and Normals (xyz per vertex), Triangles (three vertex indices each, Unity winding), and per
    /// vertex an Emissive weight (0..1, how much of the mesh glows; 0 except on the bomb's band and fuse tip) and a Part id
    /// (<see cref="NativeBombPart"/> on the bomb, 0 elsewhere). Flat-shaded meshes repeat a vertex per face.
    /// </summary>
    public sealed class NativeVfxMesh
    {
        public readonly string Name;
        public readonly double[] Positions,Normals,Emissive;
        public readonly int[] Triangles,Parts;
        public int VertexCount=>Emissive.Length;
        public int TriangleCount=>Triangles.Length/3;
        public NativeVfxMesh(string name,double[] positions,double[] normals,int[] triangles,double[] emissive,int[] parts)
        {Name=name;Positions=positions;Normals=normals;Triangles=triangles;Emissive=emissive;Parts=parts;}
    }

    /// <summary>The bomb mesh's parts (<see cref="NativeVfxMesh.Parts"/>): dark casing, glowing equator band, top cap, fuse
    /// stem and the glowing fuse tip.</summary>
    public enum NativeBombPart{Casing=0,Band=1,Cap=2,Fuse=3,Tip=4}

    public static class NativeVfxMeshes
    {
        public const int ChunkVariants=4;
        /// <summary>The bomb's casing diameter (m) at scale 1: the source's .38 m body cube edge (NativeBombPose scales it).</summary>
        public const double BombDiameter=.38;
        /// <summary>Height of the fuse tip's centre above the casing centre at scale 1 (the source's fuse cube sat .26 m above).</summary>
        public const double BombTipY=.262;
        /// <summary>Emissive weights: the band glows a little less than the fuse tip.</summary>
        public const double BandGlow=.8,TipGlow=1;

        /// <summary>Four irregular convex rubble chunks (8, 10, 12 and 14 hull points, flat-shaded), each fitting the voxel's unit
        /// box (-.5...5 on every axis, at least one axis spanning it), so a renderer scales it by the particle's edge
        /// lengths as it did the cube. Pick one per particle by seed (for example seed % ChunkVariants).</summary>
        public static readonly NativeVfxMesh[] Chunks=BuildChunks();
        /// <summary>One crystal shard: a slightly irregular hexagonal bipyramid along +Y, flat-shaded, in the voxel's unit box
        /// with its tips at y = ±.5. The pool's stretch (3.5 on every Shard spawn) makes it long.</summary>
        public static readonly NativeVfxMesh Shard=BuildShard();
        /// <summary>The chip bomb in metres at scale 1, centred on its casing: a low-poly casing (diameter BombDiameter), a
        /// raised equator band (Emissive BandGlow), a top cap, a short fuse stem and the fuse tip at BombTipY (Emissive TipGlow).
        /// Every part is a closed, outward-wound solid; parts overlap rather than join.</summary>
        public static readonly NativeVfxMesh Bomb=BuildBomb();
        /// <summary>The shock shell: an icosphere (2 subdivisions: 162 vertices, 320 triangles) of unit radius with smooth
        /// normals (the unit positions), pole up. Scale it by the front's radius around its origin.</summary>
        public static readonly NativeVfxMesh Shell=Icosphere(2,"shock shell");
        /// <summary>The shell's upper hemisphere (y >= 0, open at the base: its rim lies exactly on y = 0) for a front that is a
        /// dome rising from the floor (NativeBlastShock.Dome).</summary>
        public static readonly NativeVfxMesh Dome=Hemisphere(Shell,"shock dome");

        // ---- building ----
        private sealed class Builder
        {
            public readonly List<double> P=new List<double>(),N=new List<double>(),E=new List<double>();public readonly List<int> T=new List<int>(),Parts=new List<int>();
            // A flat face oriented so its normal agrees with out: a vertex per corner, all carrying the face normal.
            public void Face(double[] a,double[] b,double[] c,double ox,double oy,double oz,int part,double emissive)
            {
                double ux=b[0]-a[0],uy=b[1]-a[1],uz=b[2]-a[2],vx=c[0]-a[0],vy=c[1]-a[1],vz=c[2]-a[2];
                double nx=uy*vz-uz*vy,ny=uz*vx-ux*vz,nz=ux*vy-uy*vx;
                if(nx*ox+ny*oy+nz*oz<0){var t=b;b=c;c=t;nx=-nx;ny=-ny;nz=-nz;}
                double l=Math.Sqrt(nx*nx+ny*ny+nz*nz);if(l>0){nx/=l;ny/=l;nz/=l;}
                foreach(var q in new[]{a,b,c}){T.Add(E.Count);P.Add(q[0]);P.Add(q[1]);P.Add(q[2]);N.Add(nx);N.Add(ny);N.Add(nz);E.Add(emissive);Parts.Add(part);}
            }
            // A face of a solid around a known interior point: its normal points away from the centre.
            public void Out(double[] a,double[] b,double[] c,double cx,double cy,double cz,int part,double emissive)
            {Face(a,b,c,(a[0]+b[0]+c[0])/3-cx,(a[1]+b[1]+c[1])/3-cy,(a[2]+b[2]+c[2])/3-cz,part,emissive);}
            public NativeVfxMesh Build(string name)=>new NativeVfxMesh(name,P.ToArray(),N.ToArray(),T.ToArray(),E.ToArray(),Parts.ToArray());
        }
        private static double[] V(double x,double y,double z)=>new[]{x,y,z};

        // Convex hull of points in general position (brute force; n <= 14): every triple with all other points on one side.
        private static List<int[]> Hull(List<double[]> pts)
        {
            var faces=new List<int[]>();int n=pts.Count;
            for(int i=0;i<n;i++)for(int j=i+1;j<n;j++)for(int k=j+1;k<n;k++)
            {
                double[] a=pts[i],b=pts[j],c=pts[k];double ux=b[0]-a[0],uy=b[1]-a[1],uz=b[2]-a[2],vx=c[0]-a[0],vy=c[1]-a[1],vz=c[2]-a[2];
                double nx=uy*vz-uz*vy,ny=uz*vx-ux*vz,nz=ux*vy-uy*vx;int above=0,below=0;
                for(int m=0;m<n;m++){if(m==i||m==j||m==k)continue;double d=(pts[m][0]-a[0])*nx+(pts[m][1]-a[1])*ny+(pts[m][2]-a[2])*nz;if(d>1e-12)above++;else if(d<-1e-12)below++;}
                if(above==0||below==0)faces.Add(new[]{i,j,k});
            }
            return faces;
        }
        private static NativeVfxMesh[] BuildChunks()
        {
            var chunks=new NativeVfxMesh[ChunkVariants];
            for(int v=0;v<ChunkVariants;v++)
            {
                var random=new NativeBlastRandom(0xc4a0u+(uint)v*7919u);int n=8+2*v;var pts=new List<double[]>();
                // Squashed, jittered directions (golden spiral), so each chunk is a lumpy slab rather than a ball.
                double ax=.8+random.Next()*.4,ay=.55+random.Next()*.3,az=.75+random.Next()*.35;
                for(int i=0;i<n;i++)
                {
                    double y=1-2*(i+.5)/n,r=Math.Sqrt(Math.Max(0,1-y*y)),phi=i*2.39996+random.Next()*.9,len=.78+random.Next()*.22;
                    pts.Add(V(Math.Cos(phi)*r*len*ax,y*len*ay,Math.Sin(phi)*r*len*az));
                }
                chunks[v]=Solid(pts,"rubble chunk "+v);
            }
            return chunks;
        }
        // Fits the points in the unit box (centred, the largest extent spanning -.5..5), then flat-shades their hull.
        private static NativeVfxMesh Solid(List<double[]> pts,string name)
        {
            double[] lo={1e9,1e9,1e9},hi={-1e9,-1e9,-1e9};foreach(var q in pts)for(int a=0;a<3;a++){lo[a]=Math.Min(lo[a],q[a]);hi[a]=Math.Max(hi[a],q[a]);}
            double span=Math.Max(hi[0]-lo[0],Math.Max(hi[1]-lo[1],hi[2]-lo[2]));
            foreach(var q in pts)for(int a=0;a<3;a++)q[a]=(q[a]-(lo[a]+hi[a])/2)/span;
            double cx=0,cy=0,cz=0;foreach(var q in pts){cx+=q[0];cy+=q[1];cz+=q[2];}cx/=pts.Count;cy/=pts.Count;cz/=pts.Count;
            var b=new Builder();foreach(var f in Hull(pts))b.Out(pts[f[0]],pts[f[1]],pts[f[2]],cx,cy,cz,0,0);return b.Build(name);
        }
        private static NativeVfxMesh BuildShard()
        {
            var random=new NativeBlastRandom(0x5a4du);var pts=new List<double[]>();double ring=-.12;
            for(int i=0;i<6;i++){double a=(i+(random.Next()-.5)*.35)*Math.PI/3,r=.3+random.Next()*.08;pts.Add(V(Math.Cos(a)*r,ring+(random.Next()-.5)*.06,Math.Sin(a)*r));}
            pts.Add(V((random.Next()-.5)*.08,.5,(random.Next()-.5)*.08));pts.Add(V((random.Next()-.5)*.06,-.5,(random.Next()-.5)*.06));
            var b=new Builder();double cy=ring;
            for(int i=0;i<6;i++){var p=pts[i];var q=pts[(i+1)%6];b.Out(p,q,pts[6],0,cy,0,0,0);b.Out(q,p,pts[7],0,cy,0,0,0);}
            return b.Build("crystal shard");
        }
        // A closed prism or cylinder with flat faces: segments sides of radius r from y0 to y1, with caps.
        private static void Cylinder(Builder b,int segments,double r,double y0,double y1,double turn,int part,double emissive)
        {
            double cy=(y0+y1)/2;
            for(int i=0;i<segments;i++)
            {
                double a0=(i+turn)*2*Math.PI/segments,a1=(i+1+turn)*2*Math.PI/segments;
                double[] p0=V(Math.Cos(a0)*r,y0,Math.Sin(a0)*r),p1=V(Math.Cos(a1)*r,y0,Math.Sin(a1)*r),q0=V(p0[0],y1,p0[2]),q1=V(p1[0],y1,p1[2]);
                b.Out(p0,p1,q1,0,cy,0,part,emissive);b.Out(p0,q1,q0,0,cy,0,part,emissive);
                b.Out(V(0,y1,0),q0,q1,0,cy,0,part,emissive);b.Out(V(0,y0,0),p1,p0,0,cy,0,part,emissive);
            }
        }
        // The equator band: an annular prism (outer and inner walls, top and bottom rings), each face wound by its own outward normal.
        private static void Band(Builder b,int segments,double inner,double outer,double half,int part,double emissive)
        {
            for(int i=0;i<segments;i++)
            {
                double a0=i*2*Math.PI/segments,a1=(i+1)*2*Math.PI/segments,am=(i+.5)*2*Math.PI/segments,c0=Math.Cos(a0),s0=Math.Sin(a0),c1=Math.Cos(a1),s1=Math.Sin(a1),cm=Math.Cos(am),sm=Math.Sin(am);
                double[] ob0=V(c0*outer,-half,s0*outer),ob1=V(c1*outer,-half,s1*outer),ot0=V(c0*outer,half,s0*outer),ot1=V(c1*outer,half,s1*outer);
                double[] ib0=V(c0*inner,-half,s0*inner),ib1=V(c1*inner,-half,s1*inner),it0=V(c0*inner,half,s0*inner),it1=V(c1*inner,half,s1*inner);
                b.Face(ob0,ob1,ot1,cm,0,sm,part,emissive);b.Face(ob0,ot1,ot0,cm,0,sm,part,emissive);
                b.Face(ib0,it1,ib1,-cm,0,-sm,part,emissive);b.Face(ib0,it0,it1,-cm,0,-sm,part,emissive);
                b.Face(it0,ot0,ot1,0,1,0,part,emissive);b.Face(it0,ot1,it1,0,1,0,part,emissive);
                b.Face(ib0,ob1,ob0,0,-1,0,part,emissive);b.Face(ib0,ib1,ob1,0,-1,0,part,emissive);
            }
        }
        private static NativeVfxMesh BuildBomb()
        {
            var b=new Builder();double R=BombDiameter/2;
            // Casing: a faceted (1-subdivision icosphere) ball.
            var ball=Icosphere(1,"casing");var p=ball.Positions;var t=ball.Triangles;
            for(int i=0;i<t.Length;i+=3){double[] P(int k)=>V(p[t[k]*3]*R,p[t[k]*3+1]*R,p[t[k]*3+2]*R);b.Out(P(i),P(i+1),P(i+2),0,0,0,(int)NativeBombPart.Casing,0);}
            Band(b,16,R*.86,R*1.08,.03,(int)NativeBombPart.Band,BandGlow);
            Cylinder(b,8,.065,R*.86,R+.025,.5,(int)NativeBombPart.Cap,0);
            Cylinder(b,6,.014,R+.02,BombTipY-.012,0,(int)NativeBombPart.Fuse,0);
            // Fuse tip: an octahedron.
            double tr=.03;var tip=new[]{V(tr,BombTipY,0),V(-tr,BombTipY,0),V(0,BombTipY+tr,0),V(0,BombTipY-tr,0),V(0,BombTipY,tr),V(0,BombTipY,-tr)};
            foreach(int sx in new[]{0,1})foreach(int sy in new[]{2,3})foreach(int sz in new[]{4,5})b.Out(tip[sx],tip[sy],tip[sz],0,BombTipY,0,(int)NativeBombPart.Tip,TipGlow);
            return b.Build("chip bomb");
        }
        /// <summary>A unit icosphere, pole up, with shared vertices and smooth normals (the unit positions). Each subdivision
        /// splits every triangle in four through edge midpoints pushed back onto the sphere. Its equator (y = 0) is a ring of
        /// edges from the first subdivision on, so <see cref="Hemisphere"/> cuts it cleanly.</summary>
        public static NativeVfxMesh Icosphere(int subdivisions,string name="icosphere")
        {
            var pos=new List<double[]>();double h=1/Math.Sqrt(5),r=2/Math.Sqrt(5);
            pos.Add(V(0,1,0));for(int k=0;k<5;k++)pos.Add(V(Math.Cos(k*2*Math.PI/5)*r,h,Math.Sin(k*2*Math.PI/5)*r));
            for(int k=0;k<5;k++)pos.Add(V(Math.Cos((k+.5)*2*Math.PI/5)*r,-h,Math.Sin((k+.5)*2*Math.PI/5)*r));pos.Add(V(0,-1,0));
            var tris=new List<int[]>();
            for(int k=0;k<5;k++){int u0=1+k,u1=1+(k+1)%5,l0=6+k,l1=6+(k+1)%5;tris.Add(new[]{0,u0,u1});tris.Add(new[]{u0,l0,u1});tris.Add(new[]{u1,l0,l1});tris.Add(new[]{11,l1,l0});}
            for(int s=0;s<subdivisions;s++)
            {
                var mid=new Dictionary<long,int>();var next=new List<int[]>();
                int Mid(int a,int c)
                {
                    long key=a<c?(long)a<<32|(uint)c:(long)c<<32|(uint)a;if(mid.TryGetValue(key,out int m))return m;
                    double x=(pos[a][0]+pos[c][0])/2,y=(pos[a][1]+pos[c][1])/2,z=(pos[a][2]+pos[c][2])/2,l=Math.Sqrt(x*x+y*y+z*z);
                    pos.Add(V(x/l,y/l,z/l));mid[key]=pos.Count-1;return pos.Count-1;
                }
                foreach(var f in tris){int ab=Mid(f[0],f[1]),bc=Mid(f[1],f[2]),ca=Mid(f[2],f[0]);next.Add(new[]{f[0],ab,ca});next.Add(new[]{f[1],bc,ab});next.Add(new[]{f[2],ca,bc});next.Add(new[]{ab,bc,ca});}
                tris=next;
            }
            var positions=new double[pos.Count*3];for(int i=0;i<pos.Count;i++)for(int a=0;a<3;a++)positions[i*3+a]=pos[i][a];
            var triangles=new int[tris.Count*3];
            for(int i=0;i<tris.Count;i++)
            {
                // Unity winding: cross(b - a, c - a) along the outward direction (the face centroid on a unit sphere).
                int a=tris[i][0],b=tris[i][1],c=tris[i][2];double[] A=pos[a],B=pos[b],C=pos[c];
                double ux=B[0]-A[0],uy=B[1]-A[1],uz=B[2]-A[2],vx=C[0]-A[0],vy=C[1]-A[1],vz=C[2]-A[2];
                double nx=uy*vz-uz*vy,ny=uz*vx-ux*vz,nz=ux*vy-uy*vx;if(nx*(A[0]+B[0]+C[0])+ny*(A[1]+B[1]+C[1])+nz*(A[2]+B[2]+C[2])<0){int t=b;b=c;c=t;}
                triangles[i*3]=a;triangles[i*3+1]=b;triangles[i*3+2]=c;
            }
            return new NativeVfxMesh(name,positions,(double[])positions.Clone(),triangles,new double[pos.Count],new int[pos.Count]);
        }
        /// <summary>The triangles of a pole-up unit sphere mesh that lie on or above y = 0, re-indexed (vertices below dropped).</summary>
        public static NativeVfxMesh Hemisphere(NativeVfxMesh sphere,string name="hemisphere")
        {
            var p=sphere.Positions;var t=sphere.Triangles;var map=new Dictionary<int,int>();var tris=new List<int>();var keep=new List<int>();
            for(int i=0;i<t.Length;i+=3)
            {
                if(p[t[i]*3+1]<-1e-12||p[t[i+1]*3+1]<-1e-12||p[t[i+2]*3+1]<-1e-12)continue;
                for(int k=0;k<3;k++){int v=t[i+k];if(!map.TryGetValue(v,out int m)){m=keep.Count;keep.Add(v);map[v]=m;}tris.Add(m);}
            }
            var positions=new double[keep.Count*3];var normals=new double[keep.Count*3];
            for(int i=0;i<keep.Count;i++)for(int a=0;a<3;a++){positions[i*3+a]=p[keep[i]*3+a];normals[i*3+a]=sphere.Normals[keep[i]*3+a];}
            return new NativeVfxMesh(name,positions,normals,tris.ToArray(),new double[keep.Count],new int[keep.Count]);
        }
    }
}
