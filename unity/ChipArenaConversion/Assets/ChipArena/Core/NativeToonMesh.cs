using System;
using System.Collections.Generic;

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

        /// <summary>The wrap's section points (seven per corner: two post points on each face, three on the corner circle), its
        /// rows and its vertex and triangle counts.</summary>
        public const int WrapPoints=28,WrapRows=8,WrapVertexCount=WrapPoints*WrapRows,WrapTriangleCount=WrapPoints*(WrapRows-1)*2+2*(4*5+6);
        /// <summary>The rows' vertical hint (the orientation hint's y before normalising: the foot leans down, the top up).</summary>
        private static readonly float[] RowHint={-.6f,0,0,0,0,.3f,.7f,2};
        /// <summary>
        /// The anime style's wrap for the intact stage (docs/MAP-STYLE-ANIME.md "Shapes"; NativeBlockLit ToonWrap places it, and
        /// <see cref="NativeMapStyle.WrapPoint"/> is the CPU copy): a lathe of a rounded section through <see cref="WrapRows"/>
        /// rows. The section runs counter-clockwise from +x toward +z; each corner has its circle in three points and, on either
        /// face, a post line in two points at the same place (the inner one flat with the face, the outer one with the corner, so
        /// a post or a broken corner steps out of the face). The rows are the foot, the plinth's top, the body's foot and top,
        /// the coping's foot, its top and the top's rounding (45 degrees and the top). The caps are a fan per corner and one fan
        /// over the inner post points, so a broken corner slopes only within its corner. Vertex data: TEXCOORD0 (A.x, row, A.z,
        /// 1 + the stage drawn: the shader's wrap path), TEXCOORD1 (B.x, zone: 2 the corner's points, B.z, slide: 1 along x, 2
        /// along z, its sign toward the face's middle), COLOR (the damaged meshes' break: r drop, g inset, a fresh); normals are
        /// orientation hints (the style shades by its own normal). Deterministic; a load-time call. Only drawn while the style is
        /// on (the legacy look never sees it).
        /// </summary>
        public static NativeBlockLooks.StageMesh Wrap()=>WrapMesh(0);
        /// <summary>The wraps by NativeBlockLooks mesh index (0 intact, 1 S1, 2 S2 with its waist, 3 the S3 stub, 4 S2 without
        /// the waist) and <see cref="ToothedMesh"/> (the intact wrap with a toothed end's band rows).</summary>
        public const int ToothedMesh=5,WrapMeshes=6;
        /// <summary>The rows of each wrap mesh, bottom to top (ids as ToonWrap reads them: 8 and 9 the waist, 10 on the teeth).</summary>
        public static int[] WrapRowsOf(int mesh)
        {
            if(mesh==2)return new[]{0,1,2,8,9,3,4,5,6,7};
            if(mesh==ToothedMesh){var r=new List<int>{0,1,2};for(int j=(int)NativeMapStyle.WrapTeeth;j>=1;j--){r.Add(10+2*(j-1));r.Add(10+2*(j-1)+1);}r.AddRange(new[]{3,4,5,6,7});return r.ToArray();}
            return new[]{0,1,2,3,4,5,6,7};
        }
        /// <summary>The stage a wrap mesh draws (its TEXCOORD0.w less 1).</summary>
        public static int WrapStage(int mesh)=>mesh==ToothedMesh?0:mesh==4?2:mesh;
        public static int WrapVertices(int mesh)=>WrapPoints*WrapRowsOf(mesh).Length;
        public static int WrapTriangles(int mesh)=>WrapPoints*(WrapRowsOf(mesh).Length-1)*2+2*(4*5+6);
        /// <summary>
        /// The break a damaged wrap bakes per corner and row group (corner q of (+x,+z), (-x,+z), (-x,-z), (+x,-z); its corner
        /// points only): S1 breaks two top corners and one plinth corner, S2 all four top corners deeper and two plinth corners
        /// (the waist a fresh band), the stub a ragged top over the cut. Drop and inset are shares of ToonWrap's caps.
        /// </summary>
        private static void Damage(int mesh,int corner,bool zone2,int row,int point,out float drop,out float inset,out float fresh)
        {
            drop=inset=fresh=0;int stage=WrapStage(mesh);if(stage==0)return;
            bool top=row>=3&&row<=7,foot=row<=2;
            if(stage==3){if(row>=3&&row<=7){drop=StubRag[point];fresh=row==3?0:1;}return;}
            if(row==8||row==9){fresh=.35f;return;}
            if(!zone2)return;
            float[] tops=stage==1?new[]{1f,0,.7f,0}:new[]{1f,.8f,1f,.6f},topsIn=stage==1?new[]{1f,0,.8f,0}:new[]{1f,.9f,1f,.8f},feet=stage==1?new[]{0,0,0,1f}:new[]{0,1f,0,.8f};
            // Fresh only on the break's own rows (the coping and top, the plinth), so it never fades up a whole body face.
            if(top&&tops[corner]>0){drop=tops[corner];inset=topsIn[corner];fresh=row>=4?1:0;}
            else if(foot&&feet[corner]>0){inset=feet[corner];fresh=row<=1?1:0;}
        }
        /// <summary>The stub's ragged top per section point (shares of the stub's ragged cap; the corners deepest).</summary>
        private static readonly float[] StubRag={.3f,.6f,.9f,1f,.8f,.5f,.2f,.4f,.7f,.5f,.8f,.9f,.6f,.3f,.2f,.5f,.9f,1f,.7f,.4f,.1f,.3f,.5f,.8f,1f,.6f,.4f,.2f};
        public static NativeBlockLooks.StageMesh WrapMesh(int mesh)
        {
            if(mesh<0||mesh>=WrapMeshes)throw new ArgumentOutOfRangeException(nameof(mesh));
            int[] rows=WrapRowsOf(mesh);int nr=rows.Length,n=WrapPoints*nr,stageW=1+WrapStage(mesh);
            var lat=new float[n*4];var ins=new float[n*4];var nrm=new float[n*3];var pos=new float[n*3];var shade=new float[n*4];var tris=new int[WrapTriangles(mesh)*3];
            // The section: A.x, B.x, A.z, B.z, zone, slide and the hint's x and z, per point in order round the block.
            var sec=new float[WrapPoints,8];int k=0;
            void Point(float ax,float bx,float az,float bz,float zone,float slide,float hx,float hz){sec[k,0]=ax;sec[k,1]=bx;sec[k,2]=az;sec[k,3]=bz;sec[k,4]=zone;sec[k,5]=slide;sec[k,6]=hx;sec[k,7]=hz;k++;}
            foreach(var (sx,sz) in new[]{(1,1),(-1,1),(-1,-1),(1,-1)})
            {
                // Seven points from the x face to the z face (the arc's theta from 0 on the x face to 90 degrees on the z face).
                var c=new float[7,8];int m=0;
                void C(float ax,float bx,float az,float bz,float zone,float slide,float hx,float hz){c[m,0]=ax;c[m,1]=bx;c[m,2]=az;c[m,3]=bz;c[m,4]=zone;c[m,5]=slide;c[m,6]=hx;c[m,7]=hz;m++;}
                C(sx,0,sz,-sz,0,-2*sz,sx,-.6f*sz);C(sx,0,sz,-sz,2,-2*sz,sx,-.6f*sz);
                for(int i=0;i<=2;i++){double th=Math.PI/4*i;float si=i==2?1:(float)Math.Sin(th),co=i==2?0:(float)Math.Cos(th);C(sx,-sx*(1-co),sz,-sz*(1-si),2,0,sx*co,sz*si);}
                C(sx,-sx,sz,0,2,-sx,-.6f*sx,sz);C(sx,-sx,sz,0,0,-sx,-.6f*sx,sz);
                // Counter-clockwise passes a corner from its x face first where sx and sz agree, else from its z face.
                for(int i=0;i<7;i++){int j=sx*sz>0?i:6-i;Point(c[j,0],c[j,1],c[j,2],c[j,3],c[j,4],c[j,5],c[j,6],c[j,7]);}
            }
            for(int r=0;r<nr;r++)for(int p=0;p<WrapPoints;p++)
            {
                int i=r*WrapPoints+p,row=rows[r];lat[i*4]=sec[p,0];lat[i*4+1]=row;lat[i*4+2]=sec[p,2];lat[i*4+3]=stageW;ins[i*4]=sec[p,1];ins[i*4+1]=sec[p,4];ins[i*4+2]=sec[p,3];ins[i*4+3]=sec[p,5];
                // The hint's y: the foot leans down, the top up; a tooth band edge's step faces up where the band above is
                // recessed (even edges), down where the band below is.
                float hy=row>=10?((row-10)/2%2==1?.6f:-.6f):row>=8?0:RowHint[row];
                float hx=sec[p,6],hz=sec[p,7],l=(float)Math.Sqrt(hx*hx+hy*hy+hz*hz);nrm[i*3]=hx/l;nrm[i*3+1]=hy/l;nrm[i*3+2]=hz/l;
                Damage(mesh,p/7,sec[p,4]>1.5f,row,p,out float drop,out float inset,out float fresh);
                NativeMapStyle.WrapPoint(lat[i*4],lat[i*4+1],lat[i*4+2],ins[i*4],ins[i*4+1],ins[i*4+2],ins[i*4+3],.5f,.5f,.5f,NativeBlockLooks.Chamfer,0,0,0,out pos[i*3],out pos[i*3+1],out pos[i*3+2],stageW:stageW,dropShare:drop,inShare:inset);
                shade[i*4]=drop;shade[i*4+1]=inset;shade[i*4+2]=NativeBlockLooks.ShadeStore;shade[i*4+3]=fresh;
            }
            // Winding: outward (cross(b - a, c - a) along the vertices' mean hint), judged on a nominal intact shape with every
            // feature and no end recessed (no point coincides there): a tooth edge's two rows then make a seam-high strip facing
            // its face, and the same winding turns that strip into the right step where an end is recessed (up on the end face
            // and its corners, down under a recessed pilaster's overhang).
            var test=new float[n*3];var shape=new NativeMapStyle.WrapForm{Section=.4f,Plinth=.5f,PlinthOut=.1f,Coping=.6f,CopingOut=.12f,Top=.2f,Bottom=0,Slide=.3f,PostOut=.06f,Chip=0};
            for(int i=0;i<n;i++)NativeMapStyle.WrapPlace(lat[i*4],lat[i*4+1],lat[i*4+2],ins[i*4],ins[i*4+1],ins[i*4+2],ins[i*4+3],1,mesh==ToothedMesh?4:2,mesh==ToothedMesh?6:2,2,shape,0,0,0,out test[i*3],out test[i*3+1],out test[i*3+2]);
            int nt=0;
            void Tri(int a,int b,int c)
            {
                float ux=test[b*3]-test[a*3],uy=test[b*3+1]-test[a*3+1],uz=test[b*3+2]-test[a*3+2],vx=test[c*3]-test[a*3],vy=test[c*3+1]-test[a*3+1],vz=test[c*3+2]-test[a*3+2];
                float cx=uy*vz-uz*vy,cy=uz*vx-ux*vz,cz=ux*vy-uy*vx;float ox=nrm[a*3]+nrm[b*3]+nrm[c*3],oy=nrm[a*3+1]+nrm[b*3+1]+nrm[c*3+1],oz=nrm[a*3+2]+nrm[b*3+2]+nrm[c*3+2];
                if(cx*ox+cy*oy+cz*oz>=0){tris[nt++]=a;tris[nt++]=b;tris[nt++]=c;}else{tris[nt++]=a;tris[nt++]=c;tris[nt++]=b;}
            }
            int V(int r,int p)=>r*WrapPoints+(p%WrapPoints);
            for(int r=0;r<nr-1;r++)for(int p=0;p<WrapPoints;p++){Tri(V(r,p),V(r,p+1),V(r+1,p+1));Tri(V(r,p),V(r+1,p+1),V(r+1,p));}
            // The caps: per corner a fan from its first (inner) point, then the inner points' polygon.
            foreach(int r in new[]{0,nr-1})
            {
                for(int q=0;q<4;q++)for(int j=1;j<6;j++)Tri(V(r,q*7),V(r,q*7+j),V(r,q*7+j+1));
                int[] inner={0,6,7,13,14,20,21,27};for(int j=1;j<7;j++)Tri(V(r,inner[0]),V(r,inner[j]),V(r,inner[j+1]));
            }
            if(nt!=tris.Length)throw new InvalidOperationException("NativeToonMesh: wrapped "+nt/3+" triangles");
            return new NativeBlockLooks.StageMesh{Preview=pos,Normal=nrm,Lattice=lat,Inset=ins,Shade=shade,Triangles=tris};
        }
    }
}
