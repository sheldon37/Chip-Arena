using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// One baked scenery mesh in the Unity frame: per-vertex position, flat face normal, linear albedo (rgba) and linear
    /// emission (rgb, w 0), the layout ChipArena/SourceLit reads (COLOR albedo, TEXCOORD1 emission; w 0 keeps every tint at 1).
    /// Triangles wind clockwise seen from outside (Unity front faces). Bounds are the vertex bounds.
    /// </summary>
    public sealed class NativeSceneryMesh
    {
        public float[] Positions,Normals,Colors,Emission;public int[] Indices;public int Boxes;
        public float MinX,MinY,MinZ,MaxX,MaxY,MaxZ;
        public int Vertices=>Positions.Length/3;
    }

    /// <summary>
    /// Bakes flat-coloured boxes (the game's box-built style) into <see cref="NativeSceneryMesh"/>es, one per group, so a
    /// whole backdrop sector is one draw. A box is centre, full size and a Unity Euler rotation (Quaternion.Euler(pitch, yaw,
    /// roll): roll about z, then pitch about x, then yaw about y). Colours are sRGB hex, converted to linear once here; `shade`
    /// scales the albedo (a building's tone), `grime` darkens the box's lower corners (a soot ramp down tall walls), `glow` and
    /// `level` give linear emission. A box resting on the ground (unrotated, bottom at y ≤ 0) drops its unseen bottom face;
    /// `skip` drops further faces (1 +x, 2 -x, 4 +y, 8 -y, 16 +z, 32 -z, local). With an exclusion set, a box whose ground
    /// footprint overlaps |x| &lt; ExcludeX, |z| &lt; ExcludeZ throws (<see cref="EntersRect"/>): backdrop geometry never enters the arena.
    /// Deterministic: the same calls give the same arrays on every machine. Allocates only while building.
    /// </summary>
    public sealed class NativeSceneryBuilder
    {
        private sealed class Group {public readonly List<float> P=new List<float>(),N=new List<float>(),C=new List<float>(),E=new List<float>();public readonly List<int> T=new List<int>();public int Boxes;}
        private readonly Group[] groups;
        public int Current;public double ExcludeX=double.NaN,ExcludeZ=double.NaN;
        public NativeSceneryBuilder(int count){groups=new Group[count];for(int i=0;i<count;i++)groups[i]=new Group();}
        public int Count(int group)=>groups[group].Boxes;
        /// <summary>sRGB channel to linear (the exact piecewise curve, as Unity's Color.linear).</summary>
        public static double Linear(double c)=>c<=.04045?c/12.92:Math.Pow((c+.055)/1.055,2.4);
        public static double Channel(int hex,int shift)=>Linear(((hex>>shift)&255)/255.0);
        // Face k: axis k/2, sign + for even k; tangent axes (k/2+1)%3 and (k/2+2)%3.
        private static readonly int[] FaceBit={1,2,4,8,16,32};
        public void Box(double x,double y,double z,double sx,double sy,double sz,int hex,double pitch=0,double yaw=0,double roll=0,int glow=0,double level=0,double shade=1,double grime=0,int skip=0)
        {
            if(!(sx>0&&sy>0&&sz>0))return;var g=groups[Current];
            // R = Ry(yaw)·Rx(pitch)·Rz(roll), Unity's left-handed convention (Euler(0, 90, 0) turns +z to +x).
            double a=pitch*Math.PI/180,b=yaw*Math.PI/180,c=roll*Math.PI/180,ca=Math.Cos(a),sa=Math.Sin(a),cb=Math.Cos(b),sb=Math.Sin(b),cc=Math.Cos(c),sc=Math.Sin(c);
            double m00=cb*cc+sb*sa*sc,m01=-cb*sc+sb*sa*cc,m02=sb*ca,m10=ca*sc,m11=ca*cc,m12=-sa,m20=-sb*cc+cb*sa*sc,m21=sb*sc+cb*sa*cc,m22=cb*ca;
            bool rotated=pitch!=0||yaw!=0||roll!=0;if(!rotated&&y-sy/2<=1e-4)skip|=8;
            double hx=sx/2,hy=sy/2,hz=sz/2;
            if(!double.IsNaN(ExcludeX)&&EntersRect(x,z,hx,hy,hz,m00,m01,m02,m20,m21,m22,ExcludeX,ExcludeZ))throw new InvalidOperationException("Scenery box enters the arena at "+x.ToString("0.##")+", "+y.ToString("0.##")+", "+z.ToString("0.##"));
            float r=(float)(Channel(hex,16)*shade),gr=(float)(Channel(hex,8)*shade),bl=(float)(Channel(hex,0)*shade);
            float er=glow==0?0:(float)(Channel(glow,16)*level),eg=glow==0?0:(float)(Channel(glow,8)*level),eb=glow==0?0:(float)(Channel(glow,0)*level),dark=(float)(1-grime);
            for(int f=0;f<6;f++)
            {
                if((skip&FaceBit[f])!=0)continue;int axis=f/2;double sign=f%2==0?1:-1;
                double[] n={0,0,0},u={0,0,0},v={0,0,0};n[axis]=sign;u[(axis+1)%3]=1;v[(axis+2)%3]=1;
                // u × v is the +axis direction: flip the quad on negative faces so it winds clockwise seen from outside.
                bool flip=sign<0;double[] h={hx,hy,hz};
                double nx=m00*n[0]+m01*n[1]+m02*n[2],ny=m10*n[0]+m11*n[1]+m12*n[2],nz=m20*n[0]+m21*n[1]+m22*n[2];
                int start=g.P.Count/3;
                for(int k=0;k<4;k++)
                {
                    double su=k==1||k==2?1:-1,sv=k>=2?1:-1;
                    double lx=(n[0]+u[0]*su+v[0]*sv)*h[0],ly=(n[1]+u[1]*su+v[1]*sv)*h[1],lz=(n[2]+u[2]*su+v[2]*sv)*h[2];
                    g.P.Add((float)(x+m00*lx+m01*ly+m02*lz));g.P.Add((float)(y+m10*lx+m11*ly+m12*lz));g.P.Add((float)(z+m20*lx+m21*ly+m22*lz));
                    g.N.Add((float)nx);g.N.Add((float)ny);g.N.Add((float)nz);
                    float k2=ly<0?dark:1;g.C.Add(r*k2);g.C.Add(gr*k2);g.C.Add(bl*k2);g.C.Add(1);g.E.Add(er);g.E.Add(eg);g.E.Add(eb);g.E.Add(0);
                }
                // Corners a(-u,-v) b(+u,-v) c(+u,+v) d(-u,+v): (b-a)×(d-a) ∝ u×v = +axis; Unity front faces need cross·normal > 0
                // for (a, b, c) in the standard cross product, so positive faces take (a, b, c), (a, c, d) and negative ones flip.
                if(!flip){g.T.Add(start);g.T.Add(start+1);g.T.Add(start+2);g.T.Add(start);g.T.Add(start+2);g.T.Add(start+3);}
                else{g.T.Add(start);g.T.Add(start+2);g.T.Add(start+1);g.T.Add(start);g.T.Add(start+3);g.T.Add(start+2);}
            }
            g.Boxes++;
        }
        /// <summary>
        /// Whether a box's ground footprint (its 8 corners projected on XZ, a convex polygon) overlaps the open rectangle
        /// |x| &lt; rx, |z| &lt; rz: a separating-axis test over the rectangle's axes and the normals of the projected box edges.
        /// </summary>
        public static bool EntersRect(double x,double z,double hx,double hy,double hz,double m00,double m01,double m02,double m20,double m21,double m22,double rx,double rz)
        {
            double[] ax={1,0,m20,m21,m22},az={0,1,-m00,-m01,-m02};
            for(int a=0;a<5;a++)
            {
                double nx=ax[a],nz=az[a],l=Math.Sqrt(nx*nx+nz*nz);if(l<1e-9)continue;nx/=l;nz/=l;
                double lo=double.MaxValue,hi=double.MinValue;
                for(int k=0;k<8;k++){double lx=(k&1)==0?-hx:hx,ly=(k&2)==0?-hy:hy,lz=(k&4)==0?-hz:hz,p=(x+m00*lx+m01*ly+m02*lz)*nx+(z+m20*lx+m21*ly+m22*lz)*nz;lo=Math.Min(lo,p);hi=Math.Max(hi,p);}
                double r=rx*Math.Abs(nx)+rz*Math.Abs(nz);if(hi<=-r+1e-6||lo>=r-1e-6)return false;
            }
            return true;
        }
        public NativeSceneryMesh Bake(int group)
        {
            var g=groups[group];var m=new NativeSceneryMesh{Positions=g.P.ToArray(),Normals=g.N.ToArray(),Colors=g.C.ToArray(),Emission=g.E.ToArray(),Indices=g.T.ToArray(),Boxes=g.Boxes};
            m.MinX=m.MinY=m.MinZ=float.MaxValue;m.MaxX=m.MaxY=m.MaxZ=float.MinValue;
            for(int i=0;i<m.Positions.Length;i+=3){m.MinX=Math.Min(m.MinX,m.Positions[i]);m.MaxX=Math.Max(m.MaxX,m.Positions[i]);m.MinY=Math.Min(m.MinY,m.Positions[i+1]);m.MaxY=Math.Max(m.MaxY,m.Positions[i+1]);m.MinZ=Math.Min(m.MinZ,m.Positions[i+2]);m.MaxZ=Math.Max(m.MaxZ,m.Positions[i+2]);}
            if(m.Positions.Length==0)m.MinX=m.MinY=m.MinZ=m.MaxX=m.MaxY=m.MaxZ=0;
            return m;
        }
    }
}
