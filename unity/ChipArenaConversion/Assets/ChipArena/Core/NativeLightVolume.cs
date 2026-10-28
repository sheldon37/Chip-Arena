using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>A light the volume bakes the bounce of: its position, range and linear colour times intensity (as the shaders'
    /// practical slots light, with the slots' fall-off (1 - (d/range)^2)^2).</summary>
    public struct NativeVolumeLight
    {
        public float X,Y,Z,Range,R,G,B;
        public NativeVolumeLight(float x,float y,float z,float range,float r,float g,float b){X=x;Y=y;Z=z;Range=range;R=r;G=g;B=b;}
    }

    /// <summary>What the volume bakes from: the map's blocks at rest with their linear albedo (three floats per block), the play
    /// box, the key light (toward it, its linear colour times intensity), the trilight ambient (linear sky, equator, ground),
    /// the practical lights, the cell asked for and the rays per cell.</summary>
    public sealed class NativeLightVolumeInput
    {
        public ContentBlock[] Blocks=Array.Empty<ContentBlock>();public float[] Albedo=Array.Empty<float>();
        public float MinX=-40,MaxX=40,MinZ=-32,MaxZ=32,Ceiling=24,Cell=1;public int Rays=NativeAdvancedLooks.RaysHigh;
        public float KeyX,KeyY=1,KeyZ,KeyR,KeyG,KeyB;public readonly float[] Ambient=new float[9];
        public NativeVolumeLight[] Lights=Array.Empty<NativeVolumeLight>();
    }

    /// <summary>
    /// The runtime light volume (docs/LIGHTING-ADVANCED.md "Light volume"): the maps are built at run time from JSON, so no
    /// Editor bake (lightmaps, light probes) can reach them. This bakes, once per map at rest on a worker thread, a coarse 3D
    /// grid of what a lightmapper would add to the shaders' own light: ambient occlusion (how open each point is, and how much
    /// sky it sees) and one bounce of the key light, the practical lights and the ambient off the blocks round it, tinted by
    /// each block's own colour. It generalises Ward Zero's ceiling light field (Core NativeWardZeroField): the same once-per-map
    /// bake into an RGBAHalf 3D texture the shaders sample at a surface moved out along its normal.
    /// <para>Steps: (1) the blocks into a voxel grid at half the cell (each block thickened to one voxel, so a thin wall or deck
    /// still stops a ray; each voxel keeps the first block that fills it); (2) per open cell, a shadow ray to the key, one to
    /// each practical light in range, and <see cref="SkyRays"/> rays up for a first sky share; (3) per open cell,
    /// <see cref="NativeLightVolumeInput.Rays"/> rays over the sphere (a golden-angle spiral turned about y by the cell's own
    /// hash): the openness within <see cref="Near"/> m, the sky seen, and at each hit within <see cref="Reach"/> m the light on
    /// that face (the key times its facing and the visibility of the cell in front, the lights the same way, the ambient by
    /// facing times that cell's sky share) times the block's albedo; (4) a 1-2-1 blur over the open cells; (5) the open values
    /// carried into the solid cells (as the Ward field does), so a face never reads the inside of its own block.</para>
    /// <para>Texel (Unity's Texture3D order: x fastest, then y, then z): rgb the bounce (linear light, the shaders add albedo x it),
    /// a the occlusion: openness x (<see cref="SkyFloor"/> + (1 - SkyFloor) x sky share), 1 in the open, about .5 under a roof.
    /// The shaders darken the ambient toward it by the look's `occlusion`, never below its `floor`.</para>
    /// Memory: at most <see cref="MaxCells"/> cells (the cell widens to fit: 1 m on the arenas, about 1.8 m on the 240 m town).
    /// Pure and deterministic: every cell is its own sum, so the worker threads never change the result.
    /// </summary>
    public sealed class NativeLightVolume
    {
        /// <summary>The cell budget (64^3: 2 MB of RGBAHalf), the least cell, the most cells up the y axis.</summary>
        public const int MaxCells=262144,MaxY=48;public const float MinCell=.75f;
        /// <summary>The practical lights the bake takes (a visibility bit each).</summary>
        public const int MaxLights=64;
        /// <summary>The first pass's rays up for the sky share, the occlusion's reach (m) and the bounce's reach (m).</summary>
        public const int SkyRays=10;public const float Near=3.5f,Reach=14f,KeyReach=90f;
        /// <summary>The occlusion a cell keeps that sees no sky (inside, under a roof), and the ambient's share of the bounce.</summary>
        public const float SkyFloor=.55f,AmbientBounce=.5f;
        /// <summary>A ray that reaches its end without a hit sees this much sky (the open air beyond the reach).</summary>
        public const float FarSky=.6f;
        public int Nx,Ny,Nz;public float MinX,MinY,MinZ,Cell;public float[] Data=Array.Empty<float>();public double BakeMs;public int Open,Lights,Voxels;

        /// <summary>The golden-angle sphere of `n` directions (y from top to bottom), the base the cells turn.</summary>
        public static float[] Sphere(int n)
        {
            var d=new float[n*3];const double golden=2.39996322972865332;
            for(int k=0;k<n;k++){double y=1-(2*k+1.0)/n,r=Math.Sqrt(Math.Max(0,1-y*y)),p=k*golden;d[k*3]=(float)(Math.Cos(p)*r);d[k*3+1]=(float)y;d[k*3+2]=(float)(Math.Sin(p)*r);}
            return d;
        }
        /// <summary>A cell's own turn about y (radians), from its index.</summary>
        public static float Turn(int cell){unchecked{uint h=(uint)cell*0x9E3779B1u;h^=h>>15;h*=0x2C1B3C6Du;h^=h>>12;h*=0x297A2D39u;h^=h>>15;return (float)(h/4294967296.0*Math.PI*2);}}
        /// <summary>The trilight ambient a face with normal y `ny` takes (sky above, equator at the side, ground below).</summary>
        public static void AmbientAt(float[] a,float ny,out float r,out float g,out float b)
        {
            if(ny>=0){r=a[3]+(a[0]-a[3])*ny;g=a[4]+(a[1]-a[4])*ny;b=a[5]+(a[2]-a[5])*ny;}
            else{float t=-ny;r=a[3]+(a[6]-a[3])*t;g=a[4]+(a[7]-a[4])*t;b=a[5]+(a[8]-a[5])*t;}
        }
        /// <summary>The practical slots' fall-off: (1 - (d/range)^2)^2.</summary>
        public static float Falloff(float d,float range){if(!(range>0))return 0;float q=d/range;if(q>=1)return 0;q=1-q*q;return q*q;}

        public static NativeLightVolume Build(NativeLightVolumeInput input)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();var blocks=input.Blocks??Array.Empty<ContentBlock>();
            // The box: the play bounds widened by 2 m, from just under the lowest walkable top to the ceiling (at most 3 m over the
            // tallest block), the cell widened until the grid fits the budget.
            float lo=float.MaxValue,hi=float.MinValue;
            foreach(var b in blocks){if(b==null)continue;float top=b.pos.y+b.size.y*.5f;if(b.size.x*b.size.z>=1)lo=Math.Min(lo,top);hi=Math.Max(hi,top);}
            if(lo==float.MaxValue){lo=0;hi=8;}lo=Math.Max(-8,lo-1.5f);hi=Math.Min(Math.Max(input.Ceiling,lo+4),hi+3);
            float x0=(float)Math.Floor(input.MinX-2),z0=(float)Math.Floor(input.MinZ-2),sx=input.MaxX+2-x0,sz=input.MaxZ+2-z0,sy=Math.Max(2,hi-lo);
            float cell=Math.Max(MinCell,input.Cell);int nx,ny,nz;
            for(;;){nx=Math.Max(2,(int)Math.Ceiling(sx/cell));ny=Math.Max(2,(int)Math.Ceiling(sy/cell));nz=Math.Max(2,(int)Math.Ceiling(sz/cell));if((long)nx*ny*nz<=MaxCells&&ny<=MaxY)break;cell*=1.08f;}
            var v=new NativeLightVolume{Nx=nx,Ny=ny,Nz=nz,MinX=x0,MinY=lo,MinZ=z0,Cell=cell};int cells=nx*ny*nz;
            var grid=new Grid(blocks,x0,lo,z0,nx*2,ny*2,nz*2,cell*.5f);v.Voxels=grid.Filled;
            var albedo=input.Albedo??Array.Empty<float>();int nb=blocks.Length;var alb=new float[nb*3];for(int i=0;i<nb*3;i++)alb[i]=i<albedo.Length?Math.Max(0,Math.Min(1,albedo[i])):.4f;
            var lights=input.Lights??Array.Empty<NativeVolumeLight>();int nl=Math.Min(MaxLights,lights.Length);v.Lights=nl;
            float kl=(float)Math.Sqrt(input.KeyX*input.KeyX+input.KeyY*input.KeyY+input.KeyZ*input.KeyZ);float kx=kl>0?input.KeyX/kl:0,ky=kl>0?input.KeyY/kl:1,kz=kl>0?input.KeyZ/kl:0;
            bool key=ky>.02f&&input.KeyR+input.KeyG+input.KeyB>0;var amb=input.Ambient;
            // Each cell's ray origin: its centre, or the first open voxel of its eight when the centre's is filled (none: solid).
            var ox=new float[cells];var oy=new float[cells];var oz=new float[cells];var open=new bool[cells];
            var kv=new float[cells];var bits=new ulong[cells];var sky1=new float[cells];
            var up=Sphere(SkyRays*2);
            System.Threading.Tasks.Parallel.For(0,nz,iz=>
            {
                for(int iy=0;iy<ny;iy++)for(int ix=0;ix<nx;ix++)
                {
                    int c=(iz*ny+iy)*nx+ix;float px=x0+(ix+.5f)*cell,py=lo+(iy+.5f)*cell,pz=z0+(iz+.5f)*cell;
                    if(grid.Solid(px,py,pz))
                    {
                        bool found=false;
                        for(int k=0;k<8&&!found;k++){float qx=px+((k&1)!=0?.25f:-.25f)*cell,qy=py+((k&2)!=0?.25f:-.25f)*cell,qz=pz+((k&4)!=0?.25f:-.25f)*cell;if(!grid.Solid(qx,qy,qz)){px=qx;py=qy;pz=qz;found=true;}}
                        if(!found)continue;
                    }
                    open[c]=true;ox[c]=px;oy[c]=py;oz[c]=pz;
                    if(key&&grid.Cast(px,py,pz,kx,ky,kz,KeyReach,out _,out _,out _)!=Hit)kv[c]=1;
                    ulong m=0;
                    for(int j=0;j<nl;j++)
                    {
                        var l=lights[j];float dx=l.X-px,dy=l.Y-py,dz=l.Z-pz,d=(float)Math.Sqrt(dx*dx+dy*dy+dz*dz);if(d>=l.Range)continue;
                        if(d<.4f||grid.Cast(px,py,pz,dx/d,dy/d,dz/d,d-.35f,out _,out _,out _)!=Hit)m|=1UL<<j;
                    }
                    bits[c]=m;
                    // The sky share: the upper half of a spiral, turned, cosine weighted.
                    float turn=Turn(c),cs=(float)Math.Cos(turn),sn=(float)Math.Sin(turn),seen=0,all=0;
                    for(int k=0;k<SkyRays;k++)
                    {
                        float bx=up[k*3],by=up[k*3+1],bz=up[k*3+2],dx=bx*cs-bz*sn,dz=bx*sn+bz*cs;int r=grid.Cast(px,py,pz,dx,by,dz,Reach,out _,out _,out _);
                        all+=by;if(r==Out)seen+=by;else if(r==Miss)seen+=by*FarSky;
                    }
                    sky1[c]=all>0?seen/all:0;
                }
            });
            // The gather.
            int rays=Math.Max(8,input.Rays);var dirs=Sphere(rays);float wAll=0,skyAll=0;for(int k=0;k<rays;k++){wAll+=Math.Max(0,dirs[k*3+1]+.35f);skyAll+=Math.Max(0,dirs[k*3+1]);}
            var raw=new float[cells*4];float half=cell*.5f;
            System.Threading.Tasks.Parallel.For(0,nz,iz=>
            {
                for(int iy=0;iy<ny;iy++)for(int ix=0;ix<nx;ix++)
                {
                    int c=(iz*ny+iy)*nx+ix;if(!open[c])continue;float px=ox[c],py=oy[c],pz=oz[c];
                    float turn=Turn(c),cs=(float)Math.Cos(turn),sn=(float)Math.Sin(turn);float occ=0,seen=0,br=0,bg=0,bb=0;
                    for(int k=0;k<rays;k++)
                    {
                        float bx=dirs[k*3],dy=dirs[k*3+1],bz=dirs[k*3+2],dx=bx*cs-bz*sn,dz=bx*sn+bz*cs;float w=Math.Max(0,dy+.35f);
                        int r=grid.Cast(px,py,pz,dx,dy,dz,Reach,out float t,out int axis,out int block);
                        if(r!=Hit){occ+=w;if(dy>0)seen+=r==Out?dy:dy*FarSky;continue;}
                        float o=Math.Min(1,t/Near);o=1-(1-o)*(1-o);occ+=w*o;
                        // The face hit: its normal against the ray, the point, the open cell in front of it.
                        float nX=axis==0?(dx>0?-1:1):0,nY=axis==1?(dy>0?-1:1):0,nZ=axis==2?(dz>0?-1:1):0;
                        float hx=px+dx*t,hy=py+dy*t,hz=pz+dz*t;int f=v.CellAt(hx+nX*half*.5f,hy+nY*half*.5f,hz+nZ*half*.5f);if(f<0||!open[f])f=c;
                        float er=0,eg=0,eb=0;
                        if(key){float nl_=nX*kx+nY*ky+nZ*kz;if(nl_>0){float k_=nl_*kv[f];er+=input.KeyR*k_;eg+=input.KeyG*k_;eb+=input.KeyB*k_;}}
                        ulong m=bits[f];
                        for(int j=0;m!=0&&j<nl;j++)
                        {
                            if((m&(1UL<<j))==0)continue;var l=lights[j];float lx=l.X-hx,ly=l.Y-hy,lz=l.Z-hz,ld=(float)Math.Sqrt(lx*lx+ly*ly+lz*lz);if(ld<1e-4f)continue;
                            float fa=Falloff(ld,l.Range);if(fa<=0)continue;float nd=(nX*lx+nY*ly+nZ*lz)/ld;if(nd<=0)continue;fa*=nd;er+=l.R*fa;eg+=l.G*fa;eb+=l.B*fa;
                        }
                        AmbientAt(amb,nY,out float ar,out float ag,out float ab);float s=sky1[f]*AmbientBounce;er+=ar*s;eg+=ag*s;eb+=ab*s;
                        br+=alb[block*3]*er;bg+=alb[block*3+1]*eg;bb+=alb[block*3+2]*eb;
                    }
                    float openness=wAll>0?occ/wAll:1,skyShare=skyAll>0?seen/skyAll:0;
                    raw[c*4]=br/rays;raw[c*4+1]=bg/rays;raw[c*4+2]=bb/rays;raw[c*4+3]=openness*(SkyFloor+(1-SkyFloor)*skyShare);
                }
            });
            int openCount=0;for(int c=0;c<cells;c++)if(open[c])openCount++;v.Open=openCount;
            // The 1-2-1 blur along x, y and z over the open cells (a solid neighbour adds nothing), then the solid cells.
            var tmp=new float[cells*4];
            for(int axis=0;axis<3;axis++)
            {
                var from=axis==1?tmp:raw;var to=axis==1?raw:tmp;int stride=axis==0?1:axis==1?nx:nx*ny,len=axis==0?nx:axis==1?ny:nz;
                System.Threading.Tasks.Parallel.For(0,nz,iz=>
                {
                    for(int iy=0;iy<ny;iy++)for(int ix=0;ix<nx;ix++)
                    {
                        int c=(iz*ny+iy)*nx+ix;if(!open[c]){for(int q=0;q<4;q++)to[c*4+q]=from[c*4+q];continue;}int pos=axis==0?ix:axis==1?iy:iz;
                        float w=2,r=from[c*4]*2,g=from[c*4+1]*2,b=from[c*4+2]*2,a=from[c*4+3]*2;
                        if(pos>0&&open[c-stride]){int u=c-stride;w++;r+=from[u*4];g+=from[u*4+1];b+=from[u*4+2];a+=from[u*4+3];}
                        if(pos<len-1&&open[c+stride]){int u=c+stride;w++;r+=from[u*4];g+=from[u*4+1];b+=from[u*4+2];a+=from[u*4+3];}
                        to[c*4]=r/w;to[c*4+1]=g/w;to[c*4+2]=b/w;to[c*4+3]=a/w;
                    }
                });
            }
            // The x and z passes wrote into tmp: the blurred volume is tmp.
            var data=tmp;var filled=(bool[])open.Clone();var now=new bool[cells];
            for(int pass=0;pass<6;pass++)
            {
                bool any=false;
                for(int iz=0;iz<nz;iz++)for(int iy=0;iy<ny;iy++)for(int ix=0;ix<nx;ix++)
                {
                    int c=(iz*ny+iy)*nx+ix;if(filled[c])continue;int k=0;float r=0,g=0,b=0,a=0;
                    for(int o=0;o<6;o++)
                    {
                        int x=ix+(o==0?1:o==1?-1:0),y=iy+(o==2?1:o==3?-1:0),z=iz+(o==4?1:o==5?-1:0);if(x<0||y<0||z<0||x>=nx||y>=ny||z>=nz)continue;
                        int u=(z*ny+y)*nx+x;if(!filled[u])continue;r+=data[u*4];g+=data[u*4+1];b+=data[u*4+2];a+=data[u*4+3];k++;
                    }
                    if(k>0){data[c*4]=r/k;data[c*4+1]=g/k;data[c*4+2]=b/k;data[c*4+3]=a/k;now[c]=true;any=true;}
                }
                for(int c=0;c<cells;c++)if(now[c]){filled[c]=true;now[c]=false;}
                if(!any)break;
            }
            for(int c=0;c<cells;c++)if(!filled[c]){data[c*4]=data[c*4+1]=data[c*4+2]=0;data[c*4+3]=1;}
            v.Data=data;v.BakeMs=watch.Elapsed.TotalMilliseconds;return v;
        }

        /// <summary>The volume's floats as IEEE half floats (RGBAHalf texels; round to nearest even, past 65504 to infinity), so the
        /// worker thread does the conversion and the main thread only uploads.</summary>
        public static ushort[] Half(float[] data)
        {
            var h=new ushort[data.Length];
            for(int i=0;i<data.Length;i++)
            {
                uint x=(uint)BitConverter.SingleToInt32Bits(data[i]);uint sign=(x>>16)&0x8000u;int exp=(int)((x>>23)&0xff)-127+15;uint man=x&0x7fffffu;
                if(((x>>23)&0xff)==0xff){h[i]=(ushort)(sign|0x7c00u|(man!=0?0x200u:0));continue;}
                if(exp>=31){h[i]=(ushort)(sign|0x7c00u);continue;}
                if(exp<=0){if(exp<-10){h[i]=(ushort)sign;continue;}man|=0x800000u;int shift=14-exp;uint half=man>>shift,rest=man&((1u<<shift)-1),mid=1u<<(shift-1);if(rest>mid||rest==mid&&(half&1)!=0)half++;h[i]=(ushort)(sign|half);continue;}
                uint v=sign|((uint)exp<<10)|(man>>13);uint r=man&0x1fffu;if(r>0x1000u||r==0x1000u&&(v&1)!=0)v++;h[i]=(ushort)v;
            }
            return h;
        }
        /// <summary>The cell holding a point, or -1 outside the box.</summary>
        public int CellAt(float x,float y,float z)
        {
            int ix=(int)Math.Floor((x-MinX)/Cell),iy=(int)Math.Floor((y-MinY)/Cell),iz=(int)Math.Floor((z-MinZ)/Cell);
            return ix<0||iy<0||iz<0||ix>=Nx||iy>=Ny||iz>=Nz?-1:(iz*Ny+iy)*Nx+ix;
        }
        /// <summary>The offset along the normal the shaders read the volume at (half a cell: a face reads the air in front of it).</summary>
        public float Offset=>Cell*.5f;
        /// <summary>The volume at a surface point and its normal, as the shaders read it (trilinear between texel centres, at the
        /// point moved out by <see cref="Offset"/>; outside the box the open value 0, 0, 0, 1). Allocation free.</summary>
        public void Sample(float x,float y,float z,float nx,float ny,float nz,out float r,out float g,out float b,out float occ)
        {
            r=g=b=0;occ=1;if(Data.Length==0)return;float o=Offset;x+=nx*o;y+=ny*o;z+=nz*o;
            float u=(x-MinX)/Cell-.5f,w=(y-MinY)/Cell-.5f,s=(z-MinZ)/Cell-.5f;if(u<-.5f||w<-.5f||s<-.5f||u>Nx-.5f||w>Ny-.5f||s>Nz-.5f)return;
            int x0=(int)Math.Floor(u),y0=(int)Math.Floor(w),z0=(int)Math.Floor(s);float ax=u-x0,ay=w-y0,az=s-z0;occ=0;
            for(int k=0;k<8;k++)
            {
                int xi=Math.Max(0,Math.Min(Nx-1,x0+(k&1))),yi=Math.Max(0,Math.Min(Ny-1,y0+((k>>1)&1))),zi=Math.Max(0,Math.Min(Nz-1,z0+((k>>2)&1)));
                float wt=((k&1)!=0?ax:1-ax)*(((k>>1)&1)!=0?ay:1-ay)*(((k>>2)&1)!=0?az:1-az);int t=((zi*Ny+yi)*Nx+xi)*4;
                r+=Data[t]*wt;g+=Data[t+1]*wt;b+=Data[t+2]*wt;occ+=Data[t+3]*wt;
            }
        }

        /// <summary>A ray's end: a block (<see cref="Hit"/>), its reach in the open (<see cref="Miss"/>) or the box's side or top (<see cref="Out"/>).</summary>
        public const int Hit=1,Miss=0,Out=-1;
        /// <summary>The blocks as voxels (each the index + 1 of the first block filling it; 0 open), and a 3D DDA through them.</summary>
        internal sealed class Grid
        {
            private readonly ushort[] v;private readonly int nx,ny,nz;private readonly float x0,y0,z0,size,inv;public readonly int Filled;
            public Grid(ContentBlock[] blocks,float minX,float minY,float minZ,int sizeX,int sizeY,int sizeZ,float voxel)
            {
                nx=sizeX;ny=sizeY;nz=sizeZ;x0=minX;y0=minY;z0=minZ;size=voxel;inv=1/voxel;v=new ushort[nx*ny*nz];float grow=voxel*.5f+.001f;int filled=0;
                for(int bi=0;bi<blocks.Length&&bi<65534;bi++)
                {
                    var b=blocks[bi];if(b==null||Math.Min(b.size.x,Math.Min(b.size.y,b.size.z))<.04f)continue;
                    float qx=0,qy=0,qz=0,qw=1;if(b.rotation!=null){qx=b.rotation.x;qy=b.rotation.y;qz=b.rotation.z;qw=b.rotation.w;float ql=(float)Math.Sqrt(qx*qx+qy*qy+qz*qz+qw*qw);if(ql>1e-6f){qx/=ql;qy/=ql;qz/=ql;qw/=ql;}else{qx=qy=qz=0;qw=1;}}
                    // The rotation's rows (world from local) and the thickened half extents.
                    float m00=1-2*(qy*qy+qz*qz),m01=2*(qx*qy-qz*qw),m02=2*(qx*qz+qy*qw),m10=2*(qx*qy+qz*qw),m11=1-2*(qx*qx+qz*qz),m12=2*(qy*qz-qx*qw),m20=2*(qx*qz-qy*qw),m21=2*(qy*qz+qx*qw),m22=1-2*(qx*qx+qy*qy);
                    float hx=Math.Max(b.size.x*.5f,grow),hy=Math.Max(b.size.y*.5f,grow),hz=Math.Max(b.size.z*.5f,grow);
                    float ex=Math.Abs(m00)*hx+Math.Abs(m01)*hy+Math.Abs(m02)*hz,ey=Math.Abs(m10)*hx+Math.Abs(m11)*hy+Math.Abs(m12)*hz,ez=Math.Abs(m20)*hx+Math.Abs(m21)*hy+Math.Abs(m22)*hz;
                    int ax=Math.Max(0,(int)Math.Floor((b.pos.x-ex-x0)*inv)),bx=Math.Min(nx-1,(int)Math.Floor((b.pos.x+ex-x0)*inv));
                    int ay=Math.Max(0,(int)Math.Floor((b.pos.y-ey-y0)*inv)),by=Math.Min(ny-1,(int)Math.Floor((b.pos.y+ey-y0)*inv));
                    int az=Math.Max(0,(int)Math.Floor((b.pos.z-ez-z0)*inv)),bz=Math.Min(nz-1,(int)Math.Floor((b.pos.z+ez-z0)*inv));
                    ushort id=(ushort)(bi+1);
                    for(int z=az;z<=bz;z++)for(int y=ay;y<=by;y++)for(int x=ax;x<=bx;x++)
                    {
                        int k=(z*ny+y)*nx+x;if(v[k]!=0)continue;float px=x0+(x+.5f)*size-b.pos.x,py=y0+(y+.5f)*size-b.pos.y,pz=z0+(z+.5f)*size-b.pos.z;
                        float lx=m00*px+m10*py+m20*pz,ly=m01*px+m11*py+m21*pz,lz=m02*px+m12*py+m22*pz;
                        if(Math.Abs(lx)<=hx&&Math.Abs(ly)<=hy&&Math.Abs(lz)<=hz){v[k]=id;filled++;}
                    }
                }
                Filled=filled;
            }
            public bool Solid(float x,float y,float z)
            {
                int ix=(int)Math.Floor((x-x0)*inv),iy=(int)Math.Floor((y-y0)*inv),iz=(int)Math.Floor((z-z0)*inv);
                return ix>=0&&iy>=0&&iz>=0&&ix<nx&&iy<ny&&iz<nz&&v[(iz*ny+iy)*nx+ix]!=0;
            }
            /// <summary>From (px, py, pz) along the unit (dx, dy, dz): <see cref="Hit"/> with the distance to the face entered, its
            /// axis and the block; <see cref="Miss"/> past `reach` or out through the bottom (under every floor: no sky);
            /// <see cref="Out"/> out through a side or the top (the open air the look's ambient is). The origin's voxel is never a hit.</summary>
            public int Cast(float px,float py,float pz,float dx,float dy,float dz,float reach,out float t,out int axis,out int block)
            {
                t=0;axis=1;block=0;float gx=(px-x0)*inv,gy=(py-y0)*inv,gz=(pz-z0)*inv;int ix=(int)Math.Floor(gx),iy=(int)Math.Floor(gy),iz=(int)Math.Floor(gz);
                if(ix<0||iy<0||iz<0||ix>=nx||iy>=ny||iz>=nz)return Out;
                int sx=dx>0?1:-1,sy=dy>0?1:-1,sz=dz>0?1:-1;const float big=1e30f;
                float tdx=dx!=0?size/Math.Abs(dx):big,tdy=dy!=0?size/Math.Abs(dy):big,tdz=dz!=0?size/Math.Abs(dz):big;
                float tx=dx>0?(ix+1-gx)*size/dx:dx<0?(gx-ix)*size/-dx:big,ty=dy>0?(iy+1-gy)*size/dy:dy<0?(gy-iy)*size/-dy:big,tz=dz>0?(iz+1-gz)*size/dz:dz<0?(gz-iz)*size/-dz:big;
                for(;;)
                {
                    if(tx<ty&&tx<tz){ix+=sx;t=tx;tx+=tdx;axis=0;}else if(ty<tz){iy+=sy;t=ty;ty+=tdy;axis=1;}else{iz+=sz;t=tz;tz+=tdz;axis=2;}
                    if(t>reach)return Miss;
                    if(ix<0||iy<0||iz<0||ix>=nx||iy>=ny||iz>=nz)return iy<0?Miss:Out;
                    int b=v[(iz*ny+iy)*nx+ix];if(b!=0){block=b-1;return Hit;}
                }
            }
        }

        /// <summary>
        /// The bake's input from a map and its (styled) look, as NativeLighting applies it: the blocks with the colours the
        /// dressing gives them (NativeBlockLooks.Resolve over `materialColor`, the content materials' colours; the foundation
        /// takes the map's floor), linearised and times <see cref="AlbedoShare"/> (the shaders' paint darkens a look's base); the key
        /// and the ambient from the look (sRGB to linear; the key's linear colour times its intensity, as Unity's linear
        /// intensity applies it); the look's practical lights and the extra lights given (a presentation's candidates).
        /// </summary>
        public static NativeLightVolumeInput From(ContentMap map,MapLook look,Func<string,int> materialColor,IList<NativeVolumeLight> extra,float cell,int rays)
        {
            var i=new NativeLightVolumeInput{Cell=cell,Rays=rays};var blocks=map?.blocks??Array.Empty<ContentBlock>();i.Blocks=blocks;var bd=map?.bounds;
            if(bd!=null){i.MinX=bd.minX;i.MaxX=bd.maxX;i.MinZ=bd.minZ;i.MaxZ=bd.maxZ;i.Ceiling=bd.ceiling>0?bd.ceiling:24;}
            i.Albedo=new float[blocks.Length*3];
            for(int k=0;k<blocks.Length;k++)
            {
                var b=blocks[k];if(b==null)continue;int content=b.material=="foundation"?map.floor:materialColor?.Invoke(b.material??"")??0x8A9496;
                var c=NativeBlockLooks.Resolve(look?.blockLooks,null,b.id,b.material,content);int hex=c.Color;
                i.Albedo[k*3]=(float)NativeSceneryBuilder.Channel(hex,16)*AlbedoShare;i.Albedo[k*3+1]=(float)NativeSceneryBuilder.Channel(hex,8)*AlbedoShare;i.Albedo[k*3+2]=(float)NativeSceneryBuilder.Channel(hex,0)*AlbedoShare;
            }
            if(look!=null)
            {
                NativeMapLooks.Toward(look.key,out double kx,out double ky,out double kz);i.KeyX=(float)kx;i.KeyY=(float)ky;i.KeyZ=(float)kz;var key=look.key;
                i.KeyR=(float)NativeSceneryBuilder.Channel(key.color,16)*key.intensity;i.KeyG=(float)NativeSceneryBuilder.Channel(key.color,8)*key.intensity;i.KeyB=(float)NativeSceneryBuilder.Channel(key.color,0)*key.intensity;
                var a=look.ambient;if(a!=null){Tri(i.Ambient,0,a.sky,a.skyScale);Tri(i.Ambient,3,a.equator,a.equatorScale);Tri(i.Ambient,6,a.ground,a.groundScale);}
            }
            var list=new List<NativeVolumeLight>();
            foreach(var l in look?.lights??Array.Empty<MapLookPractical>()){if(l==null)continue;float r=(float)NativeSceneryBuilder.Channel(l.color,16)*l.intensity,g=(float)NativeSceneryBuilder.Channel(l.color,8)*l.intensity,b=(float)NativeSceneryBuilder.Channel(l.color,0)*l.intensity;list.Add(new NativeVolumeLight(l.pos.x,l.pos.y,l.pos.z,l.range,r,g,b));}
            if(extra!=null)foreach(var l in extra)if(list.Count<MaxLights&&l.Range>0)list.Add(l);
            i.Lights=list.ToArray();return i;
        }
        /// <summary>The share of a look's base colour the bounce takes (the shaders' paint, grime and dust darken it).</summary>
        public const float AlbedoShare=.8f;
        // A trilight colour as Unity applies it: the sRGB colour times its scale, then linearised.
        private static void Tri(float[] into,int at,int hex,float scale)
        {
            for(int k=0;k<3;k++){double c=((hex>>(16-8*k))&255)/255.0*scale;into[at+k]=(float)NativeEntranceLight.ToLinear(c);}
        }
    }
}
