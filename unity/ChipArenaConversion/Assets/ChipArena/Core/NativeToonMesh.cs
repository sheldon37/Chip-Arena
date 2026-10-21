using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The anime map style's block mesh (docs/MAP-STYLE-ANIME.md "Shapes"): a box with rounded edges and corners in place of the
    /// legacy S0 mesh's flat 45-degree chamfer, its corner darkening and its 30 cm inner lines, in the same lattice layout
    /// (<see cref="NativeBlockLooks.StageMesh"/>: per axis A and inset B, so NativeBlockLit's lattice expands it to any block's
    /// half extents with the instance's chamfer as the rounding radius, clamped on thin blocks exactly as the legacy mesh is).
    /// Every edge is a quarter circle in <see cref="Segments"/> facets and every corner an octant of the same circle, with the
    /// circle's normals (the shader's orientation hint; the style shades by its analytic rounded normal). Six flat faces of one
    /// quad each, so the whole box is <see cref="TriangleCount"/> triangles against the legacy S0's 188 and draws in the same
    /// instanced batches (one mesh in place of one mesh: no draw more). Only the intact stage changes: chipped, waisted and
    /// stub meshes (S1-S3, mesh 4) stay the legacy ones, so a break still shows hard fresh facets. Vertex shade is neutral
    /// (<see cref="NativeBlockLooks.ShadeStore"/>, no fresh break). Every vertex lies within the block's half extents (never
    /// past the collider) and the faces are exactly the legacy faces (the kits' 2 mm layering holds). Deterministic; Build
    /// is a load-time call (its output arrays at their exact size, a few small scratch arrays), never a per-frame one.
    /// </summary>
    public static class NativeToonMesh
    {
        /// <summary>Facets per quarter circle, and the mesh's vertex and triangle counts.</summary>
        public const int Segments=2;
        public const int VertexCount=6*4+12*(Segments+1)*2+8*(Segments+1)*(Segments+2)/2,TriangleCount=6*2+12*Segments*2+8*Segments*Segments;
        /// <summary>The style's stage meshes: the rounded box for the intact stage (mesh 0), the legacy mesh for every other.</summary>
        public static NativeBlockLooks.StageMesh Stage(int mesh)=>mesh==0?Build():NativeBlockLooks.Build(mesh);
        public static NativeBlockLooks.StageMesh Build()
        {
            var lat=new float[VertexCount*4];var ins=new float[VertexCount*4];var nrm=new float[VertexCount*3];var pos=new float[VertexCount*3];var shade=new float[VertexCount*4];var tris=new int[TriangleCount*3];
            int nv=0,nt=0;
            int Vertex(float ax,float bx,float ay,float by,float az,float bz,float nx,float ny,float nz)
            {
                int i=nv++;lat[i*4]=ax;lat[i*4+1]=ay;lat[i*4+2]=az;ins[i*4]=bx;ins[i*4+1]=by;ins[i*4+2]=bz;
                pos[i*3]=NativeBlockLooks.Corner(ax,bx,.5f,NativeBlockLooks.Chamfer);pos[i*3+1]=NativeBlockLooks.Corner(ay,by,.5f,NativeBlockLooks.Chamfer);pos[i*3+2]=NativeBlockLooks.Corner(az,bz,.5f,NativeBlockLooks.Chamfer);
                float l=(float)Math.Sqrt(nx*nx+ny*ny+nz*nz);nrm[i*3]=nx/l;nrm[i*3+1]=ny/l;nrm[i*3+2]=nz/l;
                shade[i*4]=shade[i*4+1]=shade[i*4+2]=NativeBlockLooks.ShadeStore;shade[i*4+3]=0;return i;
            }
            // A triangle wound outward (cross(b - a, c - a) along the vertices' mean normal), judged on a 4 m box with a 0.5 m
            // radius, where no inset is clamped (the true shape).
            void Tri(int a,int b,int c)
            {
                float P(int v,int k)=>NativeBlockLooks.Corner(lat[v*4+k],ins[v*4+k],2,.5f);
                float ux=P(b,0)-P(a,0),uy=P(b,1)-P(a,1),uz=P(b,2)-P(a,2),vx=P(c,0)-P(a,0),vy=P(c,1)-P(a,1),vz=P(c,2)-P(a,2);
                float cx=uy*vz-uz*vy,cy=uz*vx-ux*vz,cz=ux*vy-uy*vx;float ox=nrm[a*3]+nrm[b*3]+nrm[c*3],oy=nrm[a*3+1]+nrm[b*3+1]+nrm[c*3+1],oz=nrm[a*3+2]+nrm[b*3+2]+nrm[c*3+2];
                if(cx*ox+cy*oy+cz*oz>=0){tris[nt++]=a;tris[nt++]=b;tris[nt++]=c;}else{tris[nt++]=a;tris[nt++]=c;tris[nt++]=b;}
            }
            // Axis coordinates as (A, B) pairs, written per axis index.
            float[] A=new float[3],B=new float[3],N=new float[3];
            int V(){return Vertex(A[0],B[0],A[1],B[1],A[2],B[2],N[0],N[1],N[2]);}
            // Faces: the plane on its axis, the flat region's four corners (the chamfer in from every edge).
            for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
            {
                int u=(axis+1)%3,w=(axis+2)%3;var q=new int[4];int k=0;
                foreach(var (du,dw) in new[]{(-1,-1),(1,-1),(1,1),(-1,1)})
                {A[axis]=sign;B[axis]=0;N[axis]=sign;A[u]=du;B[u]=-du;N[u]=0;A[w]=dw;B[w]=-dw;N[w]=0;q[k++]=V();}
                Tri(q[0],q[1],q[2]);Tri(q[0],q[2],q[3]);
            }
            // Edges: between faces a and b (signs sa, sb) along the third axis e, a quarter circle from face b's flat edge
            // (theta 0) to face a's (theta 90 degrees), across the flat region along e.
            for(int e=0;e<3;e++){int a=(e+1)%3,b=(e+2)%3;for(int sa=-1;sa<=1;sa+=2)for(int sb=-1;sb<=1;sb+=2)
            {
                var ring=new int[Segments+1,2];
                for(int i=0;i<=Segments;i++)for(int j=0;j<2;j++)
                {
                    double th=Math.PI/2*i/Segments;float s=(float)Math.Sin(th),c=(float)Math.Cos(th);if(i==Segments){s=1;c=0;}
                    A[a]=sa;B[a]=-sa*(1-s);N[a]=sa*s;A[b]=sb;B[b]=-sb*(1-c);N[b]=sb*c;int de=j==0?-1:1;A[e]=de;B[e]=-de;N[e]=0;ring[i,j]=V();
                }
                for(int i=0;i<Segments;i++){Tri(ring[i,0],ring[i+1,0],ring[i+1,1]);Tri(ring[i,0],ring[i+1,1],ring[i,1]);}
            }}
            // Corners: an octant of the same circle, its points the normalised (i, j, k) with i + j + k = Segments.
            for(int sx=-1;sx<=1;sx+=2)for(int sy=-1;sy<=1;sy+=2)for(int sz=-1;sz<=1;sz+=2)
            {
                var grid=new int[Segments+1,Segments+1];int[] sg={sx,sy,sz};
                for(int i=0;i<=Segments;i++)for(int j=0;j+i<=Segments;j++)
                {
                    int k=Segments-i-j;double l=Math.Sqrt(i*i+j*j+k*k);float[] d={(float)(i/l),(float)(j/l),(float)(k/l)};
                    for(int x=0;x<3;x++){A[x]=sg[x];B[x]=-sg[x]*(1-d[x]);N[x]=sg[x]*d[x];if(d[x]==1)B[x]=0;else if(d[x]==0)B[x]=-sg[x];}
                    grid[i,j]=V();
                }
                for(int i=0;i<Segments;i++)for(int j=0;j+i<Segments;j++){Tri(grid[i,j],grid[i+1,j],grid[i,j+1]);if(i+j<Segments-1)Tri(grid[i+1,j],grid[i+1,j+1],grid[i,j+1]);}
            }
            if(nv!=VertexCount||nt!=TriangleCount*3)throw new InvalidOperationException("NativeToonMesh: built "+nv+" vertices and "+nt/3+" triangles");
            return new NativeBlockLooks.StageMesh{Preview=pos,Normal=nrm,Lattice=lat,Inset=ins,Shade=shade,Triangles=tris};
        }
    }
}
