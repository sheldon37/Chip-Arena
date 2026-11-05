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
        public readonly HumPatchMesh[] Paper={new HumPatchMesh(14000,62000),new HumPatchMesh(5200,22000),new HumPatchMesh(5200,22000)};
        public readonly HumPatchMesh Void=new HumPatchMesh(9000,26000);
        /// <summary>How many patches this frame (budget lines, checks).</summary>
        public int Patches,Refused;
        public void Begin(){foreach(var m in Paper)m.Clear();Void.Clear();Patches=Refused=0;}
        private const int Rings=9,Sectors=48;
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
        public void Tear(byte surf,Vector3 centre,Vector3 n,Vector3 a,Vector3 b,float radius,float open,float bulge,float lift,int seed,Vector3 eye,float minA,float maxA,float minB,float maxB,Vector3 f0=default,Vector3 f1=default,Vector3 f2=default,Vector3 f3=default,float tint=1,float tall=1,float strips=0,float t=0)
        {
            var m=Paper[surf];int gridV=(Rings+1)*(Sectors+1),gridI=Rings*Sectors*6;
            if(!m.Room(gridV,gridI)||!Void.Room(Sectors*5+3+StripCount*12,Sectors*21+StripCount*24)){Refused++;return;}
            Patches++;int v0=m.VertexCount;float off=surf==WallS?.012f:.008f;
            for(int s=0;s<=Sectors;s++)
            {
                int ks=s%Sectors;float ang=ks*BossMath.Tau/Sectors,ca=(float)Math.Cos(ang),sa=(float)Math.Sin(ang);
                float edge=Edge(open,seed,ks);float flap=lift*(.35f+.65f*H(seed+3,ks));
                for(int r=0;r<=Rings;r++)
                {
                    float rn=(float)r/Rings;float rad=edge+(radius-edge)*rn*rn;
                    // Taller than it's wide at its edge; round at the patch's rim.
                    float stretch=1+(tall-1)*(1-rn);float pa=ca*rad,pb=sa*rad*stretch;
                    float clA=BossMath.Clamp(pa,minA,maxA),clB=BossMath.Clamp(pb,minB,maxB);
                    float k=rad/Math.Max(.01f,radius);
                    float h=bulge*(float)Math.Exp(-k*k*5.2f);
                    h+=Peak(pa,pb,f0)+Peak(pa,pb,f1)+Peak(pa,pb,f2)+Peak(pa,pb,f3);
                    // Wrinkles running in toward the hole where it's strained, and the torn edge lifting a little off the wall.
                    if(open>.001f){h+=.004f*(1-rn)*(float)Math.Sin(ks*1.3f+seed)*Math.Min(1,open*2);h+=flap*(1-rn)*(1-rn)*(1-rn);}
                    float rim=1-rn*rn*rn*rn;h*=Math.Max(0,rim);
                    var q=centre+a*clA+b*clB+n*(off+h);
                    // The uv: the paper as it was, drawn out toward the hole's edge.
                    float ur=rad-edge*(1-rn);var qu=centre+a*BossMath.Clamp(ca*ur,minA,maxA)+b*BossMath.Clamp(sa*ur*stretch,minB,maxB);
                    SurfaceUv(surf,qu,n,out float u,out float v);
                    m.Vertices[m.VertexCount++]=new HumPatchVertex{P=q,N=n,R=tint,G=tint,B=tint,A=1,U=u,V=v};
                }
            }
            // Normals from the grid, faces toward the surface's outside.
            for(int s=0;s<=Sectors;s++)for(int r=0;r<=Rings;r++)
            {
                int i=v0+s*(Rings+1)+r;
                var dr=m.Vertices[v0+s*(Rings+1)+Math.Min(Rings,r+1)].P-m.Vertices[v0+s*(Rings+1)+Math.Max(0,r-1)].P;
                var ds=m.Vertices[v0+Math.Min(Sectors,s+1)*(Rings+1)+r].P-m.Vertices[v0+Math.Max(0,s-1)*(Rings+1)+r].P;
                var nn=Vector3.Cross(dr,ds);float l=nn.Length();if(l>1e-8f){nn/=l;if(Vector3.Dot(nn,n)<0)nn=-nn;m.Vertices[i].N=Vector3.Normalize(nn+n*1.6f);}
            }
            // (a x b) along n means s, r runs one way; wind so the outside is the front (Unity: clockwise from the front).
            bool flip=Vector3.Dot(Vector3.Cross(a,b),n)<0;
            for(int s=0;s<Sectors;s++)for(int r=0;r<Rings;r++)
            {
                int p00=v0+s*(Rings+1)+r,p01=p00+1,p10=v0+(s+1)*(Rings+1)+r,p11=p10+1;
                if(flip){m.Tri(p00,p11,p01);m.Tri(p00,p10,p11);}else{m.Tri(p00,p01,p11);m.Tri(p00,p11,p10);}
            }
            if(open<=.001f)return;
            // The hole: nothing at all; dark streaks running in toward a centre that shifts with the viewer; and round the edge a thin pale torn rim, the paper's core.
            var toEye=eye-centre;float dep=.42f*radius/Math.Max(.6f,toEye.Length());var shift=-(a*Vector3.Dot(toEye,a)+b*Vector3.Dot(toEye,b))*dep;float cap=open*.5f;float sl=shift.Length();if(sl>cap)shift*=cap/sl;
            int vc=Void.VertexCount;var off2=n*(off*.5f);
            for(int s=0;s<Sectors;s++)
            {
                float ang=s*BossMath.Tau/Sectors,ca=(float)Math.Cos(ang),sa=(float)Math.Sin(ang);float edge=Edge(open,seed,s);
                var rim=centre+a*BossMath.Clamp(ca*edge,minA,maxA)+b*BossMath.Clamp(sa*edge*tall,minB,maxB)+off2;
                float st=H(seed+7,s);float tone=st<.5f?.0f:(st-.5f)*.3f;
                Void.Vertices[Void.VertexCount++]=new HumPatchVertex{P=rim,N=n,R=.44f*tone+.006f,G=.42f*tone+.006f,B=.2f*tone+.004f,A=0};
                var mid=Vector3.Lerp(rim,centre+shift+off2,.42f);
                Void.Vertices[Void.VertexCount++]=new HumPatchVertex{P=mid,N=n,R=.05f*tone+.002f,G=.048f*tone+.002f,B=.024f*tone+.002f,A=0};
                // The torn rim: on the paper's edge (where the paper's inner ring stands), a thin pale lip outside the hole.
                int inner=v0+s*(Rings+1);var pv=m.Vertices[inner].P;float w=.006f+.03f*H(seed+11,s)*H(seed+12,s*2);var outward=Vector3.Normalize(a*ca+b*sa*tall)*w;
                Void.Vertices[Void.VertexCount++]=new HumPatchVertex{P=pv+n*.002f,N=n,R=.5f,G=.46f,B=.32f,A=0};
                Void.Vertices[Void.VertexCount++]=new HumPatchVertex{P=pv+outward+n*.002f,N=n,R=.36f,G=.33f,B=.22f,A=0};
            }
            int vcen=Void.VertexCount;Void.Vertices[Void.VertexCount++]=new HumPatchVertex{P=centre+shift+off2,N=n,R=.001f,G=.001f,B=.001f,A=0};
            for(int s=0;s<Sectors;s++)
            {
                int a0=vc+s*4,a1=a0+1,l0=a0+2,l1=a0+3,b0=vc+((s+1)%Sectors)*4,b1=b0+1,m0=b0+2,m1=b0+3;
                if(flip){Void.Tri(a0,a1,b1);Void.Tri(a0,b1,b0);Void.Tri(a1,vcen,b1);Void.Tri(l0,l1,m1);Void.Tri(l0,m1,m0);}else{Void.Tri(a0,b1,a1);Void.Tri(a0,b0,b1);Void.Tri(a1,b1,vcen);Void.Tri(l0,m1,l1);Void.Tri(l0,m0,m1);}
            }
            // Strips of paper hanging off its upper edge, turning a little, curling away; the printed side on the wall's mesh, the reverse pale on the hole's.
            if(strips>.01f&&surf==WallS)for(int k=0;k<StripCount;k++)
            {
                if(!m.Room(StripRows*2+2,StripRows*6))break;
                float ang=(.1f+.8f*H(seed+20,k))*BossMath.Pi;float ca=(float)Math.Cos(ang),sa=(float)Math.Sin(ang);int sec=(int)(ang/BossMath.Tau*Sectors);float edge=Edge(open,seed,sec);
                var root=centre+a*BossMath.Clamp(ca*edge,minA,maxA)+b*BossMath.Clamp(sa*edge*tall,minB,maxB)+n*(off+.002f);
                float len=(.16f+.42f*H(seed+21,k))*Math.Min(1,open*2.2f)*strips,wid=.035f+.065f*H(seed+22,k);if(len<.02f)continue;
                float sway=(float)Math.Sin(t*(1.1f+H(seed+23,k))+k*2)*.06f;
                int pv0=m.VertexCount,bv0=Void.VertexCount;
                for(int r=0;r<=StripRows;r++)
                {
                    float u=(float)r/StripRows;var c=root+(-b*(u*len))+a*(sway*u*u)+n*(.015f+.26f*u*u*(.5f+H(seed+24,k)));float wr=wid*(1-.75f*u*u);
                    var left=c-a*wr;var right=c+a*wr;var nr=Vector3.Normalize(n+b*(.5f*u));
                    SurfaceUv(surf,root+(-b*(u*len))-a*wr,n,out float ul,out float vl);SurfaceUv(surf,root+(-b*(u*len))+a*wr,n,out float ur2,out float vr);
                    m.Vertices[m.VertexCount++]=new HumPatchVertex{P=left,N=nr,R=1,G=1,B=1,A=1,U=ul,V=vl};m.Vertices[m.VertexCount++]=new HumPatchVertex{P=right,N=nr,R=1,G=1,B=1,A=1,U=ur2,V=vr};
                    float pale=.42f+.1f*H(seed+25,r);Void.Vertices[Void.VertexCount++]=new HumPatchVertex{P=left-n*.002f,N=-nr,R=pale,G=pale*.93f,B=pale*.68f,A=0};Void.Vertices[Void.VertexCount++]=new HumPatchVertex{P=right-n*.002f,N=-nr,R=pale,G=pale*.93f,B=pale*.68f,A=0};
                }
                for(int r=0;r<StripRows;r++)
                {
                    int l0=pv0+r*2,r0=l0+1,l1=l0+2,r1=l0+3;if(!flip){m.Tri(l0,l1,r1);m.Tri(l0,r1,r0);}else{m.Tri(l0,r1,l1);m.Tri(l0,r0,r1);}
                    int bl0=bv0+r*2,br0=bl0+1,bl1=bl0+2,br1=bl0+3;if(flip){Void.Tri(bl0,bl1,br1);Void.Tri(bl0,br1,br0);}else{Void.Tri(bl0,br1,bl1);Void.Tri(bl0,br0,br1);}
                }
            }
        }
        private const int StripCount=9,StripRows=5;
        // The hole's jagged edge at sector s: long tears out between short ones.
        private static float Edge(float open,int seed,int s){return open*(.5f+.5f*H(seed,s/2)+.42f*H(seed+1,s*3+1)*H(seed+2,s)+.18f*H(seed+9,s/5));}
        private static float Peak(float pa,float pb,Vector3 f){if(f.Z==0)return 0;float dx=pa-f.X,dy=pb-f.Y,d=(dx*dx+dy*dy)/(.085f*.085f);return f.Z*(float)Math.Exp(-d);}
        /// <summary>A door cut in the wallpaper (the death's: the way out): a dark opening `w` by `h` on the surface at `foot` (its bottom middle), its papered leaf swung open by `open` (0..1) on a hinge up its left side, the leaf's reverse pale.</summary>
        public void Door(Vector3 foot,Vector3 n,Vector3 a,float w,float h,float open)
        {
            var m=Paper[WallS];if(open<=.001f||!m.Room(4,6)||!Void.Room(12,18)){Refused++;return;}
            Patches++;var up=Vector3.UnitY;var off=n*.01f;bool flip=Vector3.Dot(Vector3.Cross(a,up),n)<0;
            var p0=foot-a*(w*.5f)+off;var p1=foot+a*(w*.5f)+off;var p2=p1+up*h;var p3=p0+up*h;
            int v=Void.VertexCount;
            void VQ(Vector3 q,float r,float g,float b){Void.Vertices[Void.VertexCount++]=new HumPatchVertex{P=q,N=n,R=r,G=g,B=b,A=0};}
            VQ(p0,.006f,.006f,.005f);VQ(p1,.006f,.006f,.005f);VQ(p2,.002f,.002f,.002f);VQ(p3,.002f,.002f,.002f);
            if(flip){Void.Tri(v,v+2,v+1);Void.Tri(v,v+3,v+2);}else{Void.Tri(v,v+1,v+2);Void.Tri(v,v+2,v+3);}
            // The leaf, on its hinge at p0-p3.
            float ang=open*1.25f;var sw=a*(float)Math.Cos(ang)+n*(float)Math.Sin(ang);var q0=p0+n*.003f;var q3=p3+n*.003f;var q1=q0+sw*w;var q2=q3+sw*w;var ln=Vector3.Normalize(n*(float)Math.Cos(ang)-a*(float)Math.Sin(ang));
            int k=m.VertexCount;
            void PQ(Vector3 q,Vector3 at){SurfaceUv(WallS,at,n,out float u,out float vv);m.Vertices[m.VertexCount++]=new HumPatchVertex{P=q,N=ln,R=1,G=1,B=1,A=1,U=u,V=vv};}
            PQ(q0,p0);PQ(q1,p1);PQ(q2,p2);PQ(q3,p3);
            if(flip){m.Tri(k,k+2,k+1);m.Tri(k,k+3,k+2);}else{m.Tri(k,k+1,k+2);m.Tri(k,k+2,k+3);}
            int b0=Void.VertexCount;void BQ(Vector3 q){Void.Vertices[Void.VertexCount++]=new HumPatchVertex{P=q-ln*.004f,N=-ln,R=.46f,G=.43f,B=.32f,A=0};}
            BQ(q0);BQ(q1);BQ(q2);BQ(q3);
            if(flip){Void.Tri(b0,b0+1,b0+2);Void.Tri(b0,b0+2,b0+3);}else{Void.Tri(b0,b0+2,b0+1);Void.Tri(b0,b0+3,b0+2);}
        }
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
