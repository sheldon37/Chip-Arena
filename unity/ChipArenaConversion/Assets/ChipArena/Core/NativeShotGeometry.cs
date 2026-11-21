using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>A triangle list in source (three.js) numbers: xyz positions and source index order.</summary>
    public sealed class NativeShotMesh
    {
        public readonly float[] Positions;public readonly float[] Normals;public readonly int[] Indices;
        public int VertexCount=>Positions.Length/3;
        public NativeShotMesh(float[] positions,float[] normals,int[] indices){Positions=positions;Normals=normals;Indices=indices;}
        public NativeShotMesh Translated(double x,double y,double z)
        {var p=(float[])Positions.Clone();for(int i=0;i<p.Length;i+=3){p[i]=(float)(p[i]+x);p[i+1]=(float)(p[i+1]+y);p[i+2]=(float)(p[i+2]+z);}return new NativeShotMesh(p,Normals,Indices);}
        /// <summary>BufferGeometry.rotateX: positions and normals through Matrix4.makeRotationX.</summary>
        public NativeShotMesh RotatedX(double angle)
        {
            double c=Math.Cos(angle),s=Math.Sin(angle);
            float[] Rotate(float[] v){if(v==null)return null;var o=(float[])v.Clone();for(int i=0;i<o.Length;i+=3){double y=v[i+1],z=v[i+2];o[i+1]=(float)(c*y-s*z);o[i+2]=(float)(s*y+c*z);}return o;}
            return new NativeShotMesh(Rotate(Positions),Rotate(Normals),Indices);
        }
        /// <summary>BufferGeometryUtils.mergeGeometries for indexed geometries: positions concatenated, indices offset.</summary>
        public static NativeShotMesh Merge(params NativeShotMesh[] parts)
        {
            var p=new List<float>();var n=new List<float>();var idx=new List<int>();bool normals=true;
            foreach(var part in parts){int offset=p.Count/3;p.AddRange(part.Positions);if(part.Normals==null)normals=false;else n.AddRange(part.Normals);foreach(int i in part.Indices)idx.Add(i+offset);}
            return new NativeShotMesh(p.ToArray(),normals?n.ToArray():null,idx.ToArray());
        }
    }

    /// <summary>
    /// three.js r180 geometry builders used by the source projectile visuals (client/renderer.ts ABILITY_SHOT and the
    /// primary bullet meshes, client/chip-effects.ts module parts, burst-pool.ts and event-vfx.ts boxes). Vertex order and
    /// index order follow three.js exactly, so a renderer that keeps the source's numbers (Unity world = source numbers,
    /// mirrored) and Cull Back sees the same front faces as NativeSourceModels does.
    /// </summary>
    public static class NativeShotGeometry
    {
        /// <summary>BoxGeometry(w,h,d,1,1,1): 24 vertices with face normals, 36 indices.</summary>
        public static NativeShotMesh Box(double width,double height,double depth)
        {
            var v=new List<float>();var n=new List<float>();var idx=new List<int>();int count=0;
            void Plane(int u,int vv,int w,double udir,double vdir,double pw,double ph,double pd)
            {
                double hw=pw/2,hh=ph/2,hd=pd/2;var vec=new double[3];
                for(int iy=0;iy<2;iy++){double y=iy*ph-hh;for(int ix=0;ix<2;ix++){double x=ix*pw-hw;vec[u]=x*udir;vec[vv]=y*vdir;vec[w]=hd;v.Add((float)vec[0]);v.Add((float)vec[1]);v.Add((float)vec[2]);vec[u]=0;vec[vv]=0;vec[w]=pd>0?1:-1;n.Add((float)vec[0]);n.Add((float)vec[1]);n.Add((float)vec[2]);}}
                int a=count,b=count+2,c=count+3,d=count+1;idx.Add(a);idx.Add(b);idx.Add(d);idx.Add(b);idx.Add(c);idx.Add(d);count+=4;
            }
            const int X=0,Y=1,Z=2;
            Plane(Z,Y,X,-1,-1,depth,height,width);Plane(Z,Y,X,1,-1,depth,height,-width);Plane(X,Z,Y,1,1,width,depth,height);
            Plane(X,Z,Y,1,-1,width,depth,-height);Plane(X,Y,Z,1,-1,width,height,depth);Plane(X,Y,Z,-1,-1,width,height,-depth);
            return new NativeShotMesh(v.ToArray(),n.ToArray(),idx.ToArray());
        }
        /// <summary>CylinderGeometry(top,bottom,height,radial,1,false); ConeGeometry(r,h,s) is Cylinder(0,r,h,s).</summary>
        public static NativeShotMesh Cylinder(double top,double bottom,double height,int radial)
        {
            const int heightSegments=1;var v=new List<float>();var idx=new List<int>();int index=0;var rows=new List<int[]>();double half=height/2;
            for(int y=0;y<=heightSegments;y++)
            {
                var row=new int[radial+1];double t=(double)y/heightSegments,radius=t*(bottom-top)+top;
                for(int x=0;x<=radial;x++){double theta=(double)x/radial*Math.PI*2;v.Add((float)(radius*Math.Sin(theta)));v.Add((float)(-t*height+half));v.Add((float)(radius*Math.Cos(theta)));row[x]=index++;}
                rows.Add(row);
            }
            for(int x=0;x<radial;x++)for(int y=0;y<heightSegments;y++)
            {
                int a=rows[y][x],b=rows[y+1][x],c=rows[y+1][x+1],d=rows[y][x+1];
                if(top>0||y!=0){idx.Add(a);idx.Add(b);idx.Add(d);}
                if(bottom>0||y!=heightSegments-1){idx.Add(b);idx.Add(c);idx.Add(d);}
            }
            void Cap(bool upper)
            {
                int start=index;double radius=upper?top:bottom,sign=upper?1:-1;
                for(int x=1;x<=radial;x++){v.Add(0);v.Add((float)(half*sign));v.Add(0);index++;}
                int end=index;
                for(int x=0;x<=radial;x++){double theta=(double)x/radial*Math.PI*2;v.Add((float)(radius*Math.Sin(theta)));v.Add((float)(half*sign));v.Add((float)(radius*Math.Cos(theta)));index++;}
                for(int x=0;x<radial;x++){int c=start+x,i=end+x;if(upper){idx.Add(i);idx.Add(i+1);idx.Add(c);}else{idx.Add(i+1);idx.Add(i);idx.Add(c);}}
            }
            if(top>0)Cap(true);if(bottom>0)Cap(false);
            return new NativeShotMesh(v.ToArray(),null,idx.ToArray());
        }
        public static NativeShotMesh Cone(double radius,double height,int radial)=>Cylinder(0,radius,height,radial);
        /// <summary>SphereGeometry(r,widthSegments,heightSegments) over the full sphere (pole rows emit one triangle per cell).</summary>
        public static NativeShotMesh Sphere(double radius,int widthSegments,int heightSegments)
        {
            widthSegments=Math.Max(3,widthSegments);heightSegments=Math.Max(2,heightSegments);var v=new List<float>();var idx=new List<int>();var grid=new List<int[]>();int index=0;
            for(int iy=0;iy<=heightSegments;iy++)
            {
                var row=new int[widthSegments+1];double t=(double)iy/heightSegments;
                for(int ix=0;ix<=widthSegments;ix++){double u=(double)ix/widthSegments;v.Add((float)(-radius*Math.Cos(u*Math.PI*2)*Math.Sin(t*Math.PI)));v.Add((float)(radius*Math.Cos(t*Math.PI)));v.Add((float)(radius*Math.Sin(u*Math.PI*2)*Math.Sin(t*Math.PI)));row[ix]=index++;}
                grid.Add(row);
            }
            for(int iy=0;iy<heightSegments;iy++)for(int ix=0;ix<widthSegments;ix++)
            {
                int a=grid[iy][ix+1],b=grid[iy][ix],c=grid[iy+1][ix],d=grid[iy+1][ix+1];
                if(iy!=0){idx.Add(a);idx.Add(b);idx.Add(d);}
                if(iy!=heightSegments-1){idx.Add(b);idx.Add(c);idx.Add(d);}
            }
            return new NativeShotMesh(v.ToArray(),null,idx.ToArray());
        }
        private static readonly double T=(1+Math.Sqrt(5))/2;
        private static readonly double[] IcoVertices={-1,T,0, 1,T,0, -1,-T,0, 1,-T,0, 0,-1,T, 0,1,T, 0,-1,-T, 0,1,-T, T,0,-1, T,0,1, -T,0,-1, -T,0,1};
        private static readonly int[] IcoFaces={0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1};
        /// <summary>IcosahedronGeometry(r,detail) through PolyhedronGeometry: non-indexed, each face subdivided then projected to r.</summary>
        public static NativeShotMesh Icosahedron(double radius,int detail)
        {
            var buffer=new List<double>();
            double[] Lerp(double[] a,double[] b,double t)=>new[]{a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t,a[2]+(b[2]-a[2])*t};
            void Push(double[] p){buffer.Add(p[0]);buffer.Add(p[1]);buffer.Add(p[2]);}
            for(int f=0;f<IcoFaces.Length;f+=3)
            {
                double[] At(int i)=>new[]{IcoVertices[i*3],IcoVertices[i*3+1],IcoVertices[i*3+2]};
                double[] a=At(IcoFaces[f]),b=At(IcoFaces[f+1]),c=At(IcoFaces[f+2]);int cols=detail+1;var grid=new double[cols+1][][];
                for(int i=0;i<=cols;i++)
                {
                    var aj=Lerp(a,c,(double)i/cols);var bj=Lerp(b,c,(double)i/cols);int rowCount=cols-i;grid[i]=new double[rowCount+1][];
                    for(int j=0;j<=rowCount;j++)grid[i][j]=j==0&&i==cols?aj:Lerp(aj,bj,(double)j/rowCount);
                }
                for(int i=0;i<cols;i++)for(int j=0;j<2*(cols-i)-1;j++)
                {
                    int k=j/2;
                    if(j%2==0){Push(grid[i][k+1]);Push(grid[i+1][k]);Push(grid[i][k]);}
                    else{Push(grid[i][k+1]);Push(grid[i+1][k+1]);Push(grid[i+1][k]);}
                }
            }
            var p=new float[buffer.Count];var idx=new int[buffer.Count/3];
            for(int i=0;i<buffer.Count;i+=3){double x=buffer[i],y=buffer[i+1],z=buffer[i+2],l=Math.Sqrt(x*x+y*y+z*z);if(l==0)l=1;p[i]=(float)(x/l*radius);p[i+1]=(float)(y/l*radius);p[i+2]=(float)(z/l*radius);idx[i/3]=i/3;}
            return new NativeShotMesh(p,null,idx);
        }
        /// <summary>TorusGeometry(R,tube,radialSegments,tubularSegments): the ring lies in local XY around +Z.</summary>
        public static NativeShotMesh Torus(double radius,double tube,int radialSegments,int tubularSegments)
        {
            var v=new List<float>();var idx=new List<int>();
            for(int j=0;j<=radialSegments;j++)for(int i=0;i<=tubularSegments;i++)
            {double u=(double)i/tubularSegments*Math.PI*2,w=(double)j/radialSegments*Math.PI*2;v.Add((float)((radius+tube*Math.Cos(w))*Math.Cos(u)));v.Add((float)((radius+tube*Math.Cos(w))*Math.Sin(u)));v.Add((float)(tube*Math.Sin(w)));}
            for(int j=1;j<=radialSegments;j++)for(int i=1;i<=tubularSegments;i++)
            {int a=(tubularSegments+1)*j+i-1,b=(tubularSegments+1)*(j-1)+i-1,c=(tubularSegments+1)*(j-1)+i,d=(tubularSegments+1)*j+i;idx.Add(a);idx.Add(b);idx.Add(d);idx.Add(b);idx.Add(c);idx.Add(d);}
            return new NativeShotMesh(v.ToArray(),null,idx.ToArray());
        }
        /// <summary>renderer.ts vox(): BoxGeometry(w,h,d).translate(x,y,z) for each [x,y,z,w,h,d], merged into one mesh.</summary>
        public static NativeShotMesh Vox(params double[][] boxes)
        {var parts=new NativeShotMesh[boxes.Length];for(int i=0;i<boxes.Length;i++){var b=boxes[i];parts[i]=Box(b[3],b[4],b[5]).Translated(b[0],b[1],b[2]);}return NativeShotMesh.Merge(parts);}
    }
}
