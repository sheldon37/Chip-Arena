using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One vertex of a surface patch (Runtime's mesh layout: position, normal, colour, uv0).</summary>
    public struct HumPatchVertex{public Vector3 P,N;public float R,G,B,A;public float U,V;}

    /// <summary>A growing mesh of fixed arrays (nothing allocated per frame).</summary>
    public sealed class HumPatchMesh
    {
        public readonly HumPatchVertex[] Vertices;public readonly int[] Indices;public int VertexCount,IndexCount;
        public HumPatchMesh(int vertices,int indices){Vertices=new HumPatchVertex[vertices];Indices=new int[indices];}
        public void Clear(){VertexCount=IndexCount=0;}
        public bool Room(int v,int i)=>VertexCount+v<=Vertices.Length&&IndexCount+i<=Indices.Length;
        public void Tri(int a,int b,int c){Indices[IndexCount++]=a;Indices[IndexCount++]=b;Indices[IndexCount++]=c;}
    }

    /// <summary>
    /// The level's surfaces reacting to THE HUM (docs/EXPEDITION-BOSS-BACKROOMS.md sections 6 and 7): real geometry laid over a wall, the floor or the ceiling in its own texture, stretched and bulged and torn; and behind each tear a hole that shows nothing at all, streaked
    /// paper running into it. A patch is a polar grid about its centre: the paper's own uv runs from the hole's jagged edge outward, so as the hole opens the pattern streaks and stretches toward it; a bulge and up to four finger peaks push it out; its rim stays on the surface. `Slip` is a
    /// rectangle whose texture creeps and swims (NOCLIP's missing floor). Everything a pure function of its arguments: the Fx calls it each frame from the record and the clock. Nothing is allocated.
    /// The wall's meshes draw with the level's own wallpaper (Runtime NativeHumBossRoom.cs); <see cref="Void"/> with the toon's vertex colours.
    /// </summary>
    public sealed class NativeHumSurface
    {
        public const byte WallS=0,FloorS=1,CeilingS=2;
        public readonly HumPatchMesh[] Paper={new HumPatchMesh(11000,48000),new HumPatchMesh(4200,18000),new HumPatchMesh(4200,18000)};
        public readonly HumPatchMesh Void=new HumPatchMesh(4000,14000);
        /// <summary>How many patches this frame (budget lines, checks).</summary>
        public int Patches,Refused;
        public void Begin(){foreach(var m in Paper)m.Clear();Void.Clear();Patches=Refused=0;}
        private const int Rings=9,Sectors=30;
        private static float H(int seed,int i){unchecked{uint x=(uint)seed*0x9E3779B9U^(uint)i*0x85EBCA6BU;x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return (x>>8)/16777216f;}}
        /// <summary>The surface's own uv at a point, as Core BackroomsMesh lays each: a wall by its run and height, the floor and ceiling by x and z.</summary>
        private static void SurfaceUv(byte surf,Vector3 q,Vector3 n,out float u,out float v)
        {
            if(surf==WallS){u=(Math.Abs(n.X)>.5f?q.Z:q.X)/BackroomsMesh.WallpaperRepeat;v=q.Y/BackroomsMesh.WallpaperRepeat;}
            else if(surf==FloorS){u=q.X/BackroomsMesh.CarpetRepeat;v=q.Z/BackroomsMesh.CarpetRepeat;}
            else{u=q.X/BackroomsLayout.Cell;v=q.Z/BackroomsLayout.Cell;}
        }
        /// <summary>
        /// A tear (or, with open 0, a bulge) on a surface: `centre` on it, `n` its outward normal, `a` and `b` unit axes in its plane (for a wall: along it and up), `radius` the patch's size, `open` the hole's radius (0 none), `bulge` how far the paper
        /// stands out at the centre (m; negative: pulled in), `lift` how far the torn edge curls off the surface, `fingers` up to four peaks (a, b offsets in xy and height in z) pushing out through it, min/max the surface's limits along a and b (the patch stays on it). `eye` makes the hole's depth
        /// shift with the viewer. `dark`: how far toward black the paper is near the hole (a stain, a burn).
        /// </summary>
        public void Tear(byte surf,Vector3 centre,Vector3 n,Vector3 a,Vector3 b,float radius,float open,float bulge,float lift,int seed,Vector3 eye,float minA,float maxA,float minB,float maxB,Vector3 f0=default,Vector3 f1=default,Vector3 f2=default,Vector3 f3=default,float tint=1)
        {
            var m=Paper[surf];int gridV=(Rings+1)*(Sectors+1),gridI=Rings*Sectors*6;
            if(!m.Room(gridV,gridI)||!Void.Room(Sectors*3+3,Sectors*15)){Refused++;return;}
            Patches++;int v0=m.VertexCount;float off=surf==WallS?.012f:.008f;
            for(int s=0;s<=Sectors;s++)
            {
                int ks=s%Sectors;float ang=ks*BossMath.Tau/Sectors,ca=(float)Math.Cos(ang),sa=(float)Math.Sin(ang);
                float edge=open*(.56f+.62f*H(seed,ks)*H(seed+1,ks*3+1)+.22f*H(seed+2,ks/3));float flap=lift*(.45f+.55f*H(seed+3,ks));
                for(int r=0;r<=Rings;r++)
                {
                    float rn=(float)r/Rings;float rad=edge+(radius-edge)*rn*rn;
                    float pa=ca*rad,pb=sa*rad;
                    // Stays on the surface it's on.
                    float clA=BossMath.Clamp(pa,minA,maxA),clB=BossMath.Clamp(pb,minB,maxB);
                    float k=rad/Math.Max(.01f,radius);
                    float h=bulge*(float)Math.Exp(-k*k*5.2f);
                    h+=Peak(pa,pb,f0)+Peak(pa,pb,f1)+Peak(pa,pb,f2)+Peak(pa,pb,f3);
                    // The torn edge curls away; further from the edge it lies on the wall.
                    float curl=open>.001f?flap*(1-rn)*(1-rn)*(1-rn):0;
                    float rim=1-rn*rn*rn*rn;h=(h+curl)*Math.Max(0,rim);
                    var q=centre+a*clA+b*clB+n*(off+h);
                    // The uv: the paper as it was, drawn out toward the hole's edge.
                    float ur=rad-edge*(1-rn);var qu=centre+a*BossMath.Clamp(ca*ur,minA,maxA)+b*BossMath.Clamp(sa*ur,minB,maxB);
                    SurfaceUv(surf,qu,n,out float u,out float v);
                    float c=1-.55f*(1-rn)*(1-rn);m.Vertices[m.VertexCount++]=new HumPatchVertex{P=q,N=n,R=c*tint,G=c*tint,B=c*tint,A=1,U=u,V=v};
                }
            }
            // Normals from the grid, faces toward the surface's outside.
            for(int s=0;s<=Sectors;s++)for(int r=0;r<=Rings;r++)
            {
                int i=v0+s*(Rings+1)+r;var p=m.Vertices[i].P;
                var dr=m.Vertices[v0+s*(Rings+1)+Math.Min(Rings,r+1)].P-m.Vertices[v0+s*(Rings+1)+Math.Max(0,r-1)].P;
                var ds=m.Vertices[v0+Math.Min(Sectors,s+1)*(Rings+1)+r].P-m.Vertices[v0+Math.Max(0,s-1)*(Rings+1)+r].P;
                var nn=Vector3.Cross(dr,ds);float l=nn.Length();if(l>1e-8f){nn/=l;if(Vector3.Dot(nn,n)<0)nn=-nn;m.Vertices[i].N=Vector3.Normalize(nn+n*.35f);}
            }
            // (a x b) along n means s, r runs one way; wind so the outside is the front (Unity: clockwise from the front).
            bool flip=Vector3.Dot(Vector3.Cross(a,b),n)<0;
            for(int s=0;s<Sectors;s++)for(int r=0;r<Rings;r++)
            {
                int p00=v0+s*(Rings+1)+r,p01=p00+1,p10=v0+(s+1)*(Rings+1)+r,p11=p10+1;
                if(flip){m.Tri(p00,p11,p01);m.Tri(p00,p10,p11);}else{m.Tri(p00,p01,p11);m.Tri(p00,p11,p10);}
            }
            if(open<=.001f)return;
            // The hole: nothing at all; a rim of dark streaks, running in toward a centre that shifts with the viewer.
            var toEye=eye-centre;float dep=.42f*radius/Math.Max(.6f,toEye.Length());var shift=-(a*Vector3.Dot(toEye,a)+b*Vector3.Dot(toEye,b))*dep;float cap=open*.5f;float sl=shift.Length();if(sl>cap)shift*=cap/sl;
            int vc=Void.VertexCount;var off2=n*(off*.5f);
            for(int s=0;s<Sectors;s++)
            {
                int ks=s;float ang=ks*BossMath.Tau/Sectors,ca=(float)Math.Cos(ang),sa=(float)Math.Sin(ang);float edge=open*(.56f+.62f*H(seed,ks)*H(seed+1,ks*3+1)+.22f*H(seed+2,ks/3));
                var rim=centre+a*BossMath.Clamp(ca*edge,minA,maxA)+b*BossMath.Clamp(sa*edge,minB,maxB)+off2;
                float st=H(seed+7,ks);float tone=st<.45f?.0f:(st-.45f)*.42f;
                Void.Vertices[Void.VertexCount++]=new HumPatchVertex{P=rim,N=n,R=.44f*tone+.012f,G=.42f*tone+.011f,B=.2f*tone+.008f,A=0};
                var mid=Vector3.Lerp(rim,centre+shift+off2,.42f);
                Void.Vertices[Void.VertexCount++]=new HumPatchVertex{P=mid,N=n,R=.09f*tone+.004f,G=.085f*tone+.004f,B=.04f*tone+.003f,A=0};
            }
            int vcen=Void.VertexCount;Void.Vertices[Void.VertexCount++]=new HumPatchVertex{P=centre+shift+off2,N=n,R=.002f,G=.002f,B=.002f,A=0};
            for(int s=0;s<Sectors;s++)
            {
                int a0=vc+s*2,a1=a0+1,b0=vc+((s+1)%Sectors)*2,b1=b0+1;
                if(flip){Void.Tri(a0,a1,b1);Void.Tri(a0,b1,b0);Void.Tri(a1,vcen,b1);}else{Void.Tri(a0,b1,a1);Void.Tri(a0,b0,b1);Void.Tri(a1,b1,vcen);}
            }
        }
        private static float Peak(float pa,float pb,Vector3 f){if(f.Z==0)return 0;float dx=pa-f.X,dy=pb-f.Y,d=(dx*dx+dy*dy)/(.085f*.085f);return f.Z*(float)Math.Exp(-d);}
        /// <summary>A rectangle of a surface whose texture creeps and swims: centre, normal, axes, half sizes, the slip's amount and clock.</summary>
        public void Slip(byte surf,Vector3 centre,Vector3 n,Vector3 a,Vector3 b,float ha,float hb,float amount,float t,int seed,float tint=1)
        {
            const int NX=10,NY=8;var m=Paper[surf];if(amount<=.001f)return;if(!m.Room((NX+1)*(NY+1),NX*NY*6)){Refused++;return;}
            Patches++;int v0=m.VertexCount;float off=surf==WallS?.009f:.006f;
            float creepA=(H(seed,1)-.5f)*.5f*t*amount,creepB=(H(seed,2)-.5f)*.4f*t*amount;
            for(int y=0;y<=NY;y++)for(int x=0;x<=NX;x++)
            {
                float fa=(float)x/NX*2-1,fb=(float)y/NY*2-1;var q=centre+a*(fa*ha)+b*(fb*hb)+n*off;
                float wave=amount*(.16f*(float)Math.Sin(t*1.3f+fb*6+seed)+.1f*(float)Math.Sin(t*2.1f+fa*7+seed*2));float wave2=amount*.12f*(float)Math.Sin(t*.9f+fa*5-fb*3+seed);
                var qu=q+a*(creepA+wave)+b*(creepB+wave2);SurfaceUv(surf,qu,n,out float u,out float v);
                m.Vertices[m.VertexCount++]=new HumPatchVertex{P=q,N=n,R=tint,G=tint,B=tint,A=1,U=u,V=v};
            }
            bool flip=Vector3.Dot(Vector3.Cross(a,b),n)<0;
            for(int y=0;y<NY;y++)for(int x=0;x<NX;x++)
            {
                int p00=v0+y*(NX+1)+x,p10=p00+1,p01=p00+NX+1,p11=p01+1;
                if(flip){m.Tri(p00,p11,p10);m.Tri(p00,p01,p11);}else{m.Tri(p00,p10,p11);m.Tri(p00,p11,p01);}
            }
        }
    }
}
