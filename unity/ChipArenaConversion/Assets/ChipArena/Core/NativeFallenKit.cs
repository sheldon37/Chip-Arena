using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// NOCTIEL's C#-built rigid pieces (the claymore, Core NativeFallenSword; the adds, Core NativeFallenAdds) in the body shader's terms
    /// (Resources/NativeFallenBody.shader): positions, smooth normals from the faces round each vertex, a colour per vertex (the gear bank's hex,
    /// tools/gear/fallen_boss.py finishes(); the Runtime makes it linear as it does the bank's), whether it takes the black line, and two lists of
    /// triangles: lit (submesh 0) and light (submesh 1, the shader's glow). Every face is wound from a hint of which way is out, so a helper is right
    /// whichever way its rings run. Unity's axes (x right, y up, z forward; a face is in front where cross(b-a,c-a) points). Pure.
    /// </summary>
    public sealed class NativeFallenKit
    {
        /// <summary>The bank's finishes (hex as drawn).</summary>
        public const uint Plate=0xECE9E2,PlateTrim=0xB9BABC,PlateDark=0x4A4A4E,Steel=0xC8CCD0,SteelDark=0x34363A,Chain=0x55585C,Black=0x050506,Vein=0x030304,
            Feather=0xE8E5DE,FeatherDark=0x9C9A96,Bone=0xCFCBC0,Light=0xFFFFFF,Cloth=0x17171B;
        public readonly List<Vector3> P=new List<Vector3>(),N=new List<Vector3>();public readonly List<uint> Color=new List<uint>();public readonly List<bool> Line=new List<bool>();
        public readonly List<int> Lit=new List<int>(),Glow=new List<int>();
        public int Count=>P.Count;
        public void Clear(){P.Clear();N.Clear();Color.Clear();Line.Clear();Lit.Clear();Glow.Clear();}
        public int V(Vector3 p,uint color,bool line=true){P.Add(p);N.Add(Vector3.Zero);Color.Add(color);Line.Add(line&&color!=Light);return P.Count-1;}
        /// <summary>A triangle wound to face along `outward`, its (area-weighted) normal added to its corners'.</summary>
        public void Tri(int a,int b,int c,Vector3 outward,bool glow)
        {
            var n=Vector3.Cross(P[b]-P[a],P[c]-P[a]);if(Vector3.Dot(n,outward)<0){(b,c)=(c,b);n=-n;}
            var l=glow?Glow:Lit;l.Add(a);l.Add(b);l.Add(c);N[a]+=n;N[b]+=n;N[c]+=n;
        }
        public void Quad(int a,int b,int c,int d,Vector3 outward,bool glow){Tri(a,b,c,outward,glow);Tri(a,c,d,outward,glow);}
        /// <summary>Normalises the normals (call once, after the last face).</summary>
        public void Finish(){for(int i=0;i<N.Count;i++){var n=N[i];float l=n.Length();N[i]=l>1e-12f?n/l:Vector3.UnitY;}}
        /// <summary>Another kit's pieces appended, each point through `m`.</summary>
        public void Add(NativeFallenKit k,Matrix4x4 m)
        {
            // Normals by the inverse transpose; a mirroring matrix turns the faces round, so their corners swap to keep the front out.
            Matrix4x4.Invert(m,out var inv);var nt=Matrix4x4.Transpose(inv);bool flip=m.GetDeterminant()<0;
            int o=P.Count;for(int i=0;i<k.P.Count;i++){P.Add(Vector3.Transform(k.P[i],m));var n=Vector3.TransformNormal(k.N[i],nt);N.Add(n.LengthSquared()>1e-20f?Vector3.Normalize(n):Vector3.UnitY);Color.Add(k.Color[i]);Line.Add(k.Line[i]);}
            void Into(List<int> from,List<int> to){for(int t=0;t+2<from.Count;t+=3){to.Add(o+from[t]);if(flip){to.Add(o+from[t+2]);to.Add(o+from[t+1]);}else{to.Add(o+from[t+1]);to.Add(o+from[t+2]);}}}
            Into(k.Lit,Lit);Into(k.Glow,Glow);
        }

        // ---- shapes ----
        /// <summary>A superellipse round `c` in the plane of u and v (the bank's ring()).</summary>
        public static Vector3[] Ring(Vector3 c,Vector3 u,Vector3 v,float rx,float ry,int n,float power=2)
        {
            var r=new Vector3[n];bool round=power==2;for(int i=0;i<n;i++){double a=2*Math.PI*i/n;float ca=(float)Math.Cos(a),sa=(float)Math.Sin(a);float x=round?ca*rx:Math.Sign(ca)*(float)Math.Pow(Math.Abs(ca),2/power)*rx,y=round?sa*ry:Math.Sign(sa)*(float)Math.Pow(Math.Abs(sa),2/power)*ry;r[i]=c+u*x+v*y;}
            return r;
        }
        /// <summary>Rings joined in order (each the same count round, `centres` their middles: out is away from them), the ends capped if asked.</summary>
        public void Loft(IList<Vector3[]> rings,IList<Vector3> centres,uint color,bool glow=false,bool line=true,bool capFirst=false,bool capLast=false)
        {
            int n=rings[0].Length,start=P.Count;foreach(var r in rings)foreach(var p in r)V(p,color,line);
            for(int k=0;k+1<rings.Count;k++)for(int i=0;i<n;i++)
            {
                int j=(i+1)%n,a=start+k*n+i,b=start+k*n+j,c=start+(k+1)*n+j,d=start+(k+1)*n+i;var mid=(P[a]+P[b]+P[c]+P[d])*.25f;
                var axis=centres[k+1]-centres[k];var cm=(centres[k]+centres[k+1])*.5f;var o=mid-cm;float al=axis.LengthSquared();if(al>1e-12f)o-=axis*(Vector3.Dot(o,axis)/al);
                if(o.LengthSquared()<1e-14f)o=mid-cm;Quad(a,b,c,d,o,glow);
            }
            if(capFirst)Cap(rings[0],centres[0],centres.Count>1?centres[0]-centres[1]:Vector3.UnitY,color,glow,line);
            if(capLast)Cap(rings[rings.Count-1],centres[rings.Count-1],centres.Count>1?centres[rings.Count-1]-centres[rings.Count-2]:Vector3.UnitY,color,glow,line);
        }
        /// <summary>A fan closing a ring (its own corners, so the edge stays hard).</summary>
        public void Cap(Vector3[] ring,Vector3 centre,Vector3 outward,uint color,bool glow=false,bool line=true)
        {
            int c=V(centre,color,line),s=P.Count;foreach(var p in ring)V(p,color,line);for(int i=0;i<ring.Length;i++)Tri(c,s+i,s+(i+1)%ring.Length,outward,glow);
        }
        /// <summary>A round tube along the points (parallel-transported frames), a radius at each.</summary>
        public void Tube(IList<Vector3> pts,IList<float> radii,int segs,uint color,bool glow=false,bool line=true,bool caps=true,bool closed=false)
        {
            int n=pts.Count;if(n<2)return;var rings=new List<Vector3[]>(n+1);var cs=new List<Vector3>(n+1);
            var t0=Vector3.Normalize(pts[1]-pts[0]);var nrm=Math.Abs(t0.Y)<.9f?Vector3.Normalize(Vector3.Cross(t0,Vector3.UnitY)):Vector3.Normalize(Vector3.Cross(t0,Vector3.UnitX));
            for(int i=0;i<n;i++)
            {
                Vector3 t=closed?pts[(i+1)%n]-pts[(i+n-1)%n]:i==0?pts[1]-pts[0]:i==n-1?pts[n-1]-pts[n-2]:pts[i+1]-pts[i-1];t=Vector3.Normalize(t);
                nrm-=t*Vector3.Dot(nrm,t);if(nrm.LengthSquared()<1e-10f)nrm=Math.Abs(t.Y)<.9f?Vector3.Cross(t,Vector3.UnitY):Vector3.Cross(t,Vector3.UnitX);nrm=Vector3.Normalize(nrm);var b=Vector3.Cross(t,nrm);
                float r=radii[Math.Min(i,radii.Count-1)];var ring=new Vector3[segs];for(int k=0;k<segs;k++){double a=2*Math.PI*k/segs;ring[k]=pts[i]+r*((float)Math.Cos(a)*nrm+(float)Math.Sin(a)*b);}
                rings.Add(ring);cs.Add(pts[i]);
            }
            if(closed){rings.Add(rings[0]);cs.Add(cs[0]);}
            Loft(rings,cs,color,glow,line,caps&&!closed,caps&&!closed);
        }
        public void Tube(IList<Vector3> pts,float radius,int segs,uint color,bool glow=false,bool line=true,bool caps=true){var r=new float[pts.Count];for(int i=0;i<r.Length;i++)r[i]=radius;Tube(pts,r,segs,color,glow,line,caps);}
        /// <summary>A ring about `axis` through `centre`.</summary>
        public void Torus(Vector3 centre,Vector3 axis,float major,float minor,int segs,int rsegs,uint color,bool glow=false,bool line=true)
        {
            axis=Vector3.Normalize(axis);var u=Math.Abs(axis.Y)<.9f?Vector3.Normalize(Vector3.Cross(axis,Vector3.UnitY)):Vector3.Normalize(Vector3.Cross(axis,Vector3.UnitX));var v=Vector3.Cross(axis,u);
            var pts=new Vector3[segs];for(int i=0;i<segs;i++){double a=2*Math.PI*i/segs;pts[i]=centre+major*((float)Math.Cos(a)*u+(float)Math.Sin(a)*v);}
            var r=new float[segs];for(int i=0;i<segs;i++)r[i]=minor;Tube(pts,r,rsegs,color,glow,line,false,true);
        }
        /// <summary>An ellipsoid: its centre and three half axes.</summary>
        public void Ellipsoid(Vector3 c,Vector3 ax,Vector3 ay,Vector3 az,int u,int v,uint color,bool glow=false,bool line=true)
        {
            var rings=new List<Vector3[]>();var cs=new List<Vector3>();
            for(int j=0;j<=v;j++){double th=Math.PI*j/v;float z=-(float)Math.Cos(th),s=(float)Math.Sin(th);var ring=new Vector3[u];for(int i=0;i<u;i++){double ph=2*Math.PI*i/u;ring[i]=c+az*z+ax*(s*(float)Math.Cos(ph))+ay*(s*(float)Math.Sin(ph));}rings.Add(ring);cs.Add(c+az*z*.999f);}
            Loft(rings,cs,color,glow,line);
        }
        /// <summary>A flat blade or leaf (the bank's blade_shape): half widths along it, diamond in section, `thick` its depth for its width.</summary>
        public void Blade(Vector3 b,Vector3 along,Vector3 across,Vector3 thick,float length,IList<float> widths,uint color,bool glow=false,bool line=true,float depth=.22f)
        {
            int n=widths.Count;var rings=new List<Vector3[]>(n);var cs=new List<Vector3>(n);
            for(int i=0;i<n;i++){var c=b+along*(length*i/(n-1));float w=widths[i];rings.Add(new[]{c+across*w,c+thick*(w*depth+.0005f),c-across*w,c-thick*(w*depth+.0005f)});cs.Add(c);}
            Loft(rings,cs,color,glow,line,true,true);
        }
        /// <summary>A blade along a curve (points, each with its across way and half width), diamond in section.</summary>
        public void Quill(IList<Vector3> pts,IList<Vector3> across,IList<float> widths,uint color,bool glow=false,bool line=true,float depth=.22f)
        {
            int n=pts.Count;var rings=new List<Vector3[]>(n);var cs=new List<Vector3>(n);
            for(int i=0;i<n;i++)
            {
                var t=Vector3.Normalize(i==0?pts[1]-pts[0]:i==n-1?pts[n-1]-pts[n-2]:pts[i+1]-pts[i-1]);var a=Vector3.Normalize(across[i]-t*Vector3.Dot(across[i],t));var th=Vector3.Cross(t,a);float w=widths[i];
                rings.Add(new[]{pts[i]+a*w,pts[i]+th*(w*depth+.0005f),pts[i]-a*w,pts[i]-th*(w*depth+.0005f)});cs.Add(pts[i]);
            }
            Loft(rings,cs,color,glow,line,true,true);
        }
        /// <summary>A Catmull-Rom curve through the points, `per` samples a span.</summary>
        public static List<Vector3> Curve(IList<Vector3> pts,int per)
        {
            var o=new List<Vector3>();int n=pts.Count;for(int i=0;i+1<n;i++){var p0=pts[Math.Max(0,i-1)];var p1=pts[i];var p2=pts[i+1];var p3=pts[Math.Min(n-1,i+2)];for(int k=0;k<per;k++){float t=k/(float)per,t2=t*t,t3=t2*t;o.Add(.5f*(2*p1+(p2-p0)*t+(2*p0-5*p1+4*p2-p3)*t2+(3*p1-p0-3*p2+p3)*t3));}}
            o.Add(pts[n-1]);return o;
        }
        /// <summary>A small deterministic random (the bank's rnd()).</summary>
        public sealed class Rand{private uint s;public Rand(int seed){s=(uint)seed*2654435761u+12345u;}public float Next(){s^=s<<13;s^=s>>17;s^=s<<5;return (s&0xFFFFFF)/16777216f;}public float Range(float a,float b)=>a+(b-a)*Next();}
    }
}
