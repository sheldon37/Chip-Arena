using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// One vertex of the shared effects renderer's lit batch (Runtime NativeVfxDraw.LitPieces copies these in as they are, in
    /// this order: position, normal, albedo and specular, emission and gloss, fresnel rim): the ROUNDS adaptations' satellites,
    /// combat drones, crystals, thorns and pylons are built in Core so the checks can measure them. Linear colours, HDR allowed.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VfxLitVertex
    {
        public Vector3 P,N;public Vector4 Albedo,Emit,Rim;
    }

    /// <summary>A small static mesh (positions, normals, triangles), built once.</summary>
    public sealed class RoundsMesh
    {
        public readonly Vector3[] P,N;public readonly int[] T;public readonly string Name;
        public RoundsMesh(string name,List<Vector3> p,List<Vector3> n,List<int> t){Name=name;P=p.ToArray();N=n.ToArray();T=t.ToArray();}
        public int Vertices=>P.Length;
    }

    /// <summary>
    /// The lit vertices of one frame (a fixed buffer; a piece that would pass it is refused whole and counted). Each piece is a
    /// <see cref="RoundsMesh"/> placed by a matrix with one material. Allocation free.
    /// </summary>
    public sealed class RoundsLit
    {
        public readonly VfxLitVertex[] V;public readonly int[] I;public int Vertices,Indices,Pieces,Refused;
        public RoundsLit(int vertices=24000,int indices=60000){V=new VfxLitVertex[vertices];I=new int[indices];}
        public void Clear(){Vertices=Indices=Pieces=Refused=0;}
        /// <summary>Mesh `m` placed by `world` (scale allowed; normals use its inverse transpose by renormalizing the rotated
        /// scaled normal), albedo and specular, emission (HDR) and gloss, rim (HDR).</summary>
        public void Add(RoundsMesh m,in Matrix4x4 world,Vector3 albedo,float spec,Vector3 emit,float gloss,Vector3 rim)
        {
            if(m==null)return;int n=m.P.Length,t=m.T.Length;if(Vertices+n>V.Length||Indices+t>I.Length){Refused++;return;}
            Matrix4x4.Invert(world,out var inv);var nt=Matrix4x4.Transpose(inv);
            var a=new Vector4(albedo,spec);var e=new Vector4(emit,gloss);var r=new Vector4(rim,0);int first=Vertices;
            for(int k=0;k<n;k++){var nn=Vector3.TransformNormal(m.N[k],nt);float l=nn.Length();V[first+k]=new VfxLitVertex{P=Vector3.Transform(m.P[k],world),N=l>1e-6f?nn/l:Vector3.UnitY,Albedo=a,Emit=e,Rim=r};}
            for(int k=0;k<t;k++)I[Indices+k]=first+m.T[k];
            Vertices+=n;Indices+=t;Pieces++;
        }
    }

    /// <summary>
    /// The ROUNDS adaptations' models as small lit meshes (sizes in metres, +z forward, +y up): the four satellite looks, the
    /// Drone Nest combat drone's parts, an ice crystal, a thorn, a lattice pillar, a relay pylon, a data packet and a ring.
    /// Flat-shaded hard surfaces (the game's cyber hardware) except the lenses and cores. Built once per process.
    /// </summary>
    public static class NativeRoundsMeshes
    {
        private sealed class B
        {
            public readonly List<Vector3> P=new List<Vector3>(),N=new List<Vector3>();public readonly List<int> T=new List<int>();
            // A flat triangle (its own vertices and face normal, wound so the normal points away from `inside`).
            public void Tri(Vector3 a,Vector3 b,Vector3 c,Vector3 inside)
            {
                var n=Vector3.Cross(b-a,c-a);float l=n.Length();if(l<1e-9f)return;n/=l;if(Vector3.Dot(n,(a+b+c)/3-inside)<0){var s=b;b=c;c=s;n=-n;}
                int k=P.Count;P.Add(a);P.Add(b);P.Add(c);N.Add(n);N.Add(n);N.Add(n);T.Add(k);T.Add(k+1);T.Add(k+2);
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 inside){Tri(a,b,c,inside);Tri(a,c,d,inside);}
            public RoundsMesh Build(string name)=>new RoundsMesh(name,P,N,T);
        }
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        /// <summary>A box of half sizes hx, hy, hz.</summary>
        public static RoundsMesh Box(float hx,float hy,float hz,string name="box"){var b=new B();AddBox(b,Vector3.Zero,hx,hy,hz);return b.Build(name);}
        private static void AddBox(B b,Vector3 c,float hx,float hy,float hz)
        {
            Vector3 P(int i)=>c+new Vector3((i&1)!=0?hx:-hx,(i&2)!=0?hy:-hy,(i&4)!=0?hz:-hz);
            int[][] f={new[]{0,1,3,2},new[]{4,6,7,5},new[]{0,4,5,1},new[]{2,3,7,6},new[]{0,2,6,4},new[]{1,5,7,3}};
            foreach(var q in f)b.Quad(P(q[0]),P(q[1]),P(q[2]),P(q[3]),c);
        }
        /// <summary>A chamfered box (an octagonal prism along z with bevelled long edges): the drone's hull.</summary>
        private static void AddHull(B b,Vector3 c,float hx,float hy,float hz,float bevel)
        {
            var ring=new Vector2[]{new Vector2(-hx+bevel,-hy),new Vector2(hx-bevel,-hy),new Vector2(hx,-hy+bevel),new Vector2(hx,hy-bevel),new Vector2(hx-bevel,hy),new Vector2(-hx+bevel,hy),new Vector2(-hx,hy-bevel),new Vector2(-hx,-hy+bevel)};
            float zf=hz,zb=-hz;
            for(int i=0;i<8;i++){var p=ring[i];var q=ring[(i+1)%8];b.Quad(c+V(p.X,p.Y,zb),c+V(q.X,q.Y,zb),c+V(q.X,q.Y,zf),c+V(p.X,p.Y,zf),c);}
            // The nose and tail caps, a little inset (a chamfered face).
            float s=.82f;
            for(int i=0;i<8;i++)
            {
                var p=ring[i];var q=ring[(i+1)%8];
                b.Quad(c+V(p.X,p.Y,zf),c+V(q.X,q.Y,zf),c+V(q.X*s,q.Y*s,zf+bevel),c+V(p.X*s,p.Y*s,zf+bevel),c);
                b.Quad(c+V(p.X,p.Y,zb),c+V(q.X,q.Y,zb),c+V(q.X*s,q.Y*s,zb-bevel),c+V(p.X*s,p.Y*s,zb-bevel),c);
                b.Tri(c+V(p.X*s,p.Y*s,zf+bevel),c+V(q.X*s,q.Y*s,zf+bevel),c+V(0,0,zf+bevel),c);b.Tri(c+V(p.X*s,p.Y*s,zb-bevel),c+V(q.X*s,q.Y*s,zb-bevel),c+V(0,0,zb-bevel),c);
            }
        }
        /// <summary>A cylinder of radius r and length h along y (centred), `sides` facets, capped.</summary>
        public static RoundsMesh Cylinder(float r,float h,int sides,string name="cylinder"){var b=new B();AddCylinder(b,Vector3.Zero,r,r,h,sides);return b.Build(name);}
        private static void AddCylinder(B b,Vector3 c,float r0,float r1,float h,int sides)
        {
            for(int i=0;i<sides;i++)
            {
                float a0=i*6.2831853f/sides,a1=(i+1)*6.2831853f/sides;var d0=V((float)Math.Cos(a0),0,(float)Math.Sin(a0));var d1=V((float)Math.Cos(a1),0,(float)Math.Sin(a1));
                var lo=c-V(0,h/2,0);var hi=c+V(0,h/2,0);
                if(r0>0||r1>0)b.Quad(lo+d0*r0,lo+d1*r0,hi+d1*r1,hi+d0*r1,c);
                if(r0>0)b.Tri(lo,lo+d0*r0,lo+d1*r0,c+V(0,h,0));if(r1>0)b.Tri(hi,hi+d0*r1,hi+d1*r1,c-V(0,h,0));
            }
        }
        /// <summary>A smooth uv sphere of radius r.</summary>
        public static RoundsMesh Sphere(float r,int lat,int lon,string name="sphere")
        {
            var p=new List<Vector3>();var n=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<=lat;i++)for(int j=0;j<=lon;j++){float th=(float)Math.PI*i/lat,ph=6.2831853f*j/lon;var d=V((float)(Math.Sin(th)*Math.Cos(ph)),(float)Math.Cos(th),(float)(Math.Sin(th)*Math.Sin(ph)));p.Add(d*r);n.Add(d);}
            for(int i=0;i<lat;i++)for(int j=0;j<lon;j++){int a=i*(lon+1)+j,b=a+lon+1;t.Add(a);t.Add(a+1);t.Add(b);t.Add(b);t.Add(a+1);t.Add(b+1);}
            return new RoundsMesh(name,p,n,t);
        }
        /// <summary>A torus of ring radius R and tube radius r about y, smooth.</summary>
        public static RoundsMesh Torus(float R,float r,int seg,int sides,string name="torus")
        {
            var p=new List<Vector3>();var n=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<=seg;i++)for(int j=0;j<=sides;j++)
            {
                float a=6.2831853f*i/seg,b=6.2831853f*j/sides;var c=V((float)Math.Cos(a),0,(float)Math.Sin(a));var d=c*(float)Math.Cos(b)+V(0,(float)Math.Sin(b),0);
                p.Add(c*R+d*r);n.Add(d);
            }
            for(int i=0;i<seg;i++)for(int j=0;j<sides;j++){int a=i*(sides+1)+j,b=a+sides+1;t.Add(a);t.Add(b);t.Add(a+1);t.Add(b);t.Add(b+1);t.Add(a+1);}
            return new RoundsMesh(name,p,n,t);
        }
        // An elongated octahedron: half length l along z, half width w along x, half thickness h along y (a blade, a prism).
        private static void AddDiamond(B b,Vector3 c,float l,float w,float h,float waist=0)
        {
            var tip=c+V(0,0,l);var tail=c-V(0,0,l*.62f);var x0=c+V(w,0,waist);var x1=c-V(w,0,-waist);var y0=c+V(0,h,waist);var y1=c-V(0,h,-waist);
            b.Tri(tip,x0,y0,c);b.Tri(tip,y0,x1,c);b.Tri(tip,x1,y1,c);b.Tri(tip,y1,x0,c);b.Tri(tail,x0,y0,c);b.Tri(tail,y0,x1,c);b.Tri(tail,x1,y1,c);b.Tri(tail,y1,x0,c);
        }
        private static RoundsMesh Diamond(float l,float w,float h,string name){var b=new B();AddDiamond(b,Vector3.Zero,l,w,h);return b.Build(name);}
        /// <summary>A crescent blade in the x-z plane: an arc of `sweep` rad at radius r, its width tapering to both points.</summary>
        private static RoundsMesh Crescent(float r,float width,float thick,float sweep,int seg)
        {
            var b=new B();Vector3 inside=Vector3.Zero;var outer=new Vector3[seg+1];var inner=new Vector3[seg+1];
            for(int i=0;i<=seg;i++){float u=(float)i/seg,a=-sweep/2+sweep*u,w=width*(float)Math.Sin(Math.PI*u)+.004f;var d=V((float)Math.Sin(a),0,(float)Math.Cos(a));outer[i]=d*(r+w*.5f);inner[i]=d*(r-w*.5f);}
            var up=V(0,thick,0);
            for(int i=0;i<seg;i++)
            {
                // The blade's section is a triangle: the outer back (top and bottom edges) and the sharp inner edge.
                var c=(outer[i]+outer[i+1])*.25f+(inner[i]+inner[i+1])*.25f;b.Quad(outer[i]+up*.5f,outer[i+1]+up*.5f,inner[i+1],inner[i],c-up);b.Quad(outer[i]-up*.5f,inner[i],inner[i+1],outer[i+1]-up*.5f,c+up);
                b.Tri(outer[i]+up*.5f,outer[i+1]+up*.5f,outer[i+1]-up*.5f,c);b.Tri(outer[i]+up*.5f,outer[i+1]-up*.5f,outer[i]-up*.5f,c);
            }
            return b.Build("crescent");
        }
        /// <summary>A hexagonal ice crystal along y, `h` tall with a pointed tip.</summary>
        private static RoundsMesh Crystal(float r,float h)
        {
            var b=new B();var c=V(0,h*.4f,0);var tip=V(0,h,0);var top=new Vector3[6];var bot=new Vector3[6];
            for(int i=0;i<6;i++){float a=i*1.0471976f;var d=V((float)Math.Cos(a),0,(float)Math.Sin(a));bot[i]=d*r*.8f;top[i]=d*r+V(0,h*.72f,0);}
            for(int i=0;i<6;i++){int j=(i+1)%6;b.Quad(bot[i],bot[j],top[j],top[i],c);b.Tri(top[i],top[j],tip,c);b.Tri(bot[i],bot[j],Vector3.Zero,c+V(0,1,0));}
            return b.Build("ice crystal");
        }
        /// <summary>A thorn: a tapering octagonal tube curving forward (+z) as it rises.</summary>
        private static RoundsMesh Thorn(float r,float h,float bend)
        {
            var b=new B();const int rings=6,sides=6;var ring=new Vector3[rings+1,sides];var axis=new Vector3[rings+1];
            for(int i=0;i<=rings;i++){float u=(float)i/rings;axis[i]=V(0,h*u,bend*u*u);float rr=r*(1-u*.92f);for(int k=0;k<sides;k++){float a=k*6.2831853f/sides;ring[i,k]=axis[i]+V((float)Math.Cos(a)*rr,0,(float)Math.Sin(a)*rr);}}
            for(int i=0;i<rings;i++)for(int k=0;k<sides;k++){int j=(k+1)%sides;b.Quad(ring[i,k],ring[i,j],ring[i+1,j],ring[i+1,k],(axis[i]+axis[i+1])*.5f);}
            var tip=axis[rings]+V(0,r*.6f,bend*.06f);for(int k=0;k<sides;k++)b.Tri(ring[rings,k],ring[rings,(k+1)%sides],tip,axis[rings]-V(0,.01f,0));
            return b.Build("thorn");
        }
        // ---- the models ----
        public static readonly RoundsMesh SatBlade=Diamond(.17f,.045f,.016f,"escort blade"),SatFin=Box(.05f,.004f,.02f,"escort fin");
        public static readonly RoundsMesh SatScythe=Crescent(.13f,.05f,.016f,2.5f,14);
        public static readonly RoundsMesh SatPrism=Diamond(.075f,.075f,.075f,"manifest prism"),SatRing=Torus(.15f,.006f,40,5,"manifest ring");
        public static readonly RoundsMesh SatPod=Sphere(.075f,8,12,"sentry pod"),SatCollar=Torus(.085f,.012f,28,6,"sentry collar"),SatBarrel=Cylinder(.014f,.15f,8,"sentry barrel");
        public static readonly RoundsMesh Core=Sphere(.03f,6,10,"core");
        public static readonly RoundsMesh DroneHull=HullMesh(),DroneCanopy=Sphere(.085f,6,12,"drone canopy"),DroneLens=Sphere(.04f,6,10,"drone lens");
        public static readonly RoundsMesh DroneArm=Box(.11f,.012f,.018f,"drone arm"),DroneHub=Cylinder(.028f,.035f,10,"drone hub"),DroneBlade=Box(.1f,.003f,.016f,"drone blade");
        public static readonly RoundsMesh DroneBarrel=Cylinder(.018f,.14f,8,"drone barrel"),DroneStrip=Box(.004f,.008f,.13f,"drone strip"),DroneSkid=Box(.006f,.008f,.12f,"drone skid");
        public static readonly RoundsMesh IceCrystal=Crystal(.06f,.42f),ThornSpike=Thorn(.05f,.5f,.16f),Pillar=Box(.035f,.5f,.035f,"lattice pillar");
        public static readonly RoundsMesh PylonMast=Cylinder(.025f,.6f,8,"pylon mast"),PylonFoot=Cylinder(.09f,.05f,8,"pylon foot"),PylonHead=Sphere(.06f,6,10,"pylon head");
        public static readonly RoundsMesh Packet=Box(.035f,.035f,.035f,"data packet"),Hook=Diamond(.06f,.03f,.01f,"hook"),Link=Torus(.03f,.007f,12,4,"chain link");
        private static RoundsMesh HullMesh(){var b=new B();AddHull(b,Vector3.Zero,.13f,.05f,.15f,.035f);return b.Build("drone hull");}
        /// <summary>Every model mesh (checks: sizes, normals, counts).</summary>
        public static RoundsMesh[] All=>new[]{SatBlade,SatFin,SatScythe,SatPrism,SatRing,SatPod,SatCollar,SatBarrel,Core,DroneHull,DroneCanopy,DroneLens,DroneArm,DroneHub,DroneBlade,DroneBarrel,DroneStrip,DroneSkid,IceCrystal,ThornSpike,Pillar,PylonMast,PylonFoot,PylonHead,Packet,Hook,Link};
    }
}
