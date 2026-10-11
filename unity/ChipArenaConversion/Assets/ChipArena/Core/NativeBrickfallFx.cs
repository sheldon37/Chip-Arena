using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// A frame's camera-dependent world quads in fixed arrays, as Runtime/NativeFxDraw.cs takes them: four corners, a linear
    /// colour and opacity, mode 0 flat or 1 soft radial sprite (alpha (1-|uv|)^1.6 over uv -1..1 corner to corner), flag
    /// additive, and the layer (0 solid: opaque, depth-written; 1 effects: premultiplied, depth-tested). Filled in submission
    /// order (the effects batch blends in that order). Capacity is fixed at construction; <see cref="Add"/> refuses past it.
    /// </summary>
    public sealed class NativeFxQuads
    {
        public readonly int Capacity;public int Count;
        public readonly float[] Corners,Colors;public readonly byte[] Modes,Additive,Layers;
        public NativeFxQuads(int capacity){Capacity=capacity;Corners=new float[capacity*12];Colors=new float[capacity*4];Modes=new byte[capacity];Additive=new byte[capacity];Layers=new byte[capacity];}
        public void Clear()=>Count=0;
        public bool Add(double ax,double ay,double az,double bx,double by,double bz,double cx,double cy,double cz,double dx,double dy,double dz,double r,double g,double b,double a,int mode,bool additive,int layer)
        {
            if(Count>=Capacity||!(a>0))return false;int o=Count*12;
            Corners[o]=(float)ax;Corners[o+1]=(float)ay;Corners[o+2]=(float)az;Corners[o+3]=(float)bx;Corners[o+4]=(float)by;Corners[o+5]=(float)bz;
            Corners[o+6]=(float)cx;Corners[o+7]=(float)cy;Corners[o+8]=(float)cz;Corners[o+9]=(float)dx;Corners[o+10]=(float)dy;Corners[o+11]=(float)dz;
            int c=Count*4;Colors[c]=(float)r;Colors[c+1]=(float)g;Colors[c+2]=(float)b;Colors[c+3]=(float)Math.Min(1,a);Modes[Count]=(byte)mode;Additive[Count]=(byte)(additive?1:0);Layers[Count]=(byte)layer;Count++;return true;
        }
    }

    /// <summary>The light the CPU-shaded pieces are lit by, read from the scene each frame (linear): toward-the-sun direction and colour, trilight ambient.</summary>
    public sealed class NativeFxLight
    {
        public double Lx,Ly=1,Lz,Sr,Sg,Sb;public readonly double[] Ambient=new double[9];
    }

    /// <summary>
    /// Builds Brickfall's camera-facing and CPU-lit pieces for one frame into a <see cref="NativeFxQuads"/> (Runtime draws them
    /// as one solid and one effects NativeSirenFx batch, so all of this costs two draws):
    ///  - backdrop life (also behind the menu): the bird flocks, the crane aviation lights (red cubes and additive halos that
    ///    keep a minimum on-screen size), the two distant smoke columns;
    ///  - atmosphere (in the arena and the killcam only): sun shafts, dust motes, paper scraps, pigeons, tarps and the
    ///    drifting collapse dust (NativeBrickfallAtmosphere).
    /// Pure: the same clock, camera, light, quality and memory give the same quads. Premultiplied pieces are sorted far to
    /// near within each kind; the far smoke goes first. Allocation free (scratch arrays are preallocated).
    /// </summary>
    public sealed class NativeBrickfallFx
    {
        public const int Capacity=2600;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        private readonly double[] spookScratch=new double[NativeBrickfallMemory.PerPigeon];
        private readonly double[] sortKey=new double[64],corner=new double[12],cloudTime=new double[NativeBrickfallMemory.Collapses];private readonly int[] sortIndex=new int[64],cloudIndex=new int[NativeBrickfallMemory.Collapses];
        private readonly double[] puffX=new double[64],puffY=new double[64],puffZ=new double[64],puffS=new double[64],puffA=new double[64];
        public int Shafts,Motes,Scraps,Pigeons,Birds,DustPuffs;
        // Camera.
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz;private NativeFxLight light;

        /// <summary>
        /// Fills <see cref="Quads"/>. `time` is the match clock in a match (the render clock behind the menu); the camera's
        /// position and its right and up vectors orient the sprites; `atmosphere` false draws only the backdrop life (menu).
        /// </summary>
        public void Build(double time,double camX,double camY,double camZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,NativeFxLight sun,double quality,bool low,bool atmosphere,NativeBrickfallMemory memory,double roundStart)
        {
            Quads.Clear();ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;light=sun;Shafts=Motes=Scraps=Pigeons=Birds=DustPuffs=0;
            Smoke(time,quality);
            if(atmosphere&&memory!=null)CollapseDust(time,quality,low,memory,roundStart);
            Aviation(time);Flocks(time,quality,low);
            if(!atmosphere)return;
            ShaftsPass(time,quality,low);MotesPass(time,quality,low);ScrapsPass(time,quality,low);
            if(memory!=null)PigeonsPass(time,quality,low,memory);
            TarpsPass(time);
        }

        // ---------------- helpers ----------------
        private static double Lin(int hex,int shift)=>NativeSceneryBuilder.Channel(hex,shift);
        private void Lit(int hex,double nx,double ny,double nz,double wrap,out double r,out double g,out double b)
        {NativeBrickfallAtmosphere.Shade(Lin(hex,16),Lin(hex,8),Lin(hex,0),nx,ny,nz,light.Lx,light.Ly,light.Lz,light.Sr,light.Sg,light.Sb,light.Ambient,wrap,out r,out g,out b);}
        private void Sprite(double x,double y,double z,double size,double r,double g,double b,double a,bool additive)
        {
            double h=size/2,ax=rx*h,ay=ry*h,az=rz*h,bx=ux*h,by=uy*h,bz=uz*h;
            Quads.Add(x-ax-bx,y-ay-by,z-az-bz,x+ax-bx,y+ay-by,z+az-bz,x+ax+bx,y+ay+by,z+az+bz,x-ax+bx,y-ay+by,z-az+bz,r,g,b,a,1,additive,1);
        }
        // A box of full size (sx, sy, sz) rotated by Unity Euler (pitch, yaw, roll), each face lit by its own normal (solid layer).
        private void Box(double x,double y,double z,double sx,double sy,double sz,double pitch,double yaw,double roll,int hex,double wrap=0,double shade=1)
        {
            double a=pitch*Math.PI/180,bb=yaw*Math.PI/180,c=roll*Math.PI/180,ca=Math.Cos(a),sa=Math.Sin(a),cb=Math.Cos(bb),sb=Math.Sin(bb),cc=Math.Cos(c),sc=Math.Sin(c);
            double m00=cb*cc+sb*sa*sc,m01=-cb*sc+sb*sa*cc,m02=sb*ca,m10=ca*sc,m11=ca*cc,m12=-sa,m20=-sb*cc+cb*sa*sc,m21=sb*sc+cb*sa*cc,m22=cb*ca;
            double hx=sx/2,hy=sy/2,hz=sz/2;
            for(int f=0;f<6;f++)
            {
                int axis=f/2;double s=f%2==0?1:-1,nx=axis==0?s:0,ny=axis==1?s:0,nz=axis==2?s:0;
                double wx=m00*nx+m01*ny+m02*nz,wy=m10*nx+m11*ny+m12*nz,wz=m20*nx+m21*ny+m22*nz;
                // Skip faces turned away from the eye (the batch is drawn two-sided).
                double fx=x+wx*(axis==0?hx:axis==1?hy:hz),fy=y+wy*(axis==0?hx:axis==1?hy:hz),fz=z+wz*(axis==0?hx:axis==1?hy:hz);if((ex-fx)*wx+(ey-fy)*wy+(ez-fz)*wz<=0)continue;
                Lit(hex,wx,wy,wz,wrap,out double r,out double g,out double b);
                int ua=(axis+1)%3,va=(axis+2)%3;var q=corner;
                for(int k=0;k<4;k++)
                {
                    double su=k==1||k==2?1:-1,sv=k>=2?1:-1,lx=nx*hx,ly=ny*hy,lz=nz*hz;
                    if(ua==0)lx=su*hx;else if(ua==1)ly=su*hy;else lz=su*hz;if(va==0)lx=sv*hx;else if(va==1)ly=sv*hy;else lz=sv*hz;
                    q[k*3]=x+m00*lx+m01*ly+m02*lz;q[k*3+1]=y+m10*lx+m11*ly+m12*lz;q[k*3+2]=z+m20*lx+m21*ly+m22*lz;
                }
                Quads.Add(q[0],q[1],q[2],q[3],q[4],q[5],q[6],q[7],q[8],q[9],q[10],q[11],r*shade,g*shade,b*shade,1,0,false,0);
            }
        }
        private void SortFar(int n,double[] x,double[] y,double[] z)
        {
            for(int i=0;i<n;i++){double dx=x[i]-ex,dy=y[i]-ey,dz=z[i]-ez;sortKey[i]=dx*dx+dy*dy+dz*dz;sortIndex[i]=i;}
            for(int i=1;i<n;i++){int v=sortIndex[i];double k=sortKey[v];int j=i-1;while(j>=0&&sortKey[sortIndex[j]]<k){sortIndex[j+1]=sortIndex[j];j--;}sortIndex[j+1]=v;}
        }

        // ---------------- backdrop life ----------------
        private void Smoke(double time,double quality)
        {
            int puffs=quality<.5?NativeBrickfallScenery.SmokePuffs/2:NativeBrickfallScenery.SmokePuffs;
            // Columns far to near, puffs far to near.
            int first=0;{NativeBrickfallScenery.SmokePuff(0,0,time,out double ax,out _,out double az,out _,out _);NativeBrickfallScenery.SmokePuff(1,0,time,out double bx,out _,out double bz,out _,out _);if((bx-ex)*(bx-ex)+(bz-ez)*(bz-ez)>(ax-ex)*(ax-ex)+(az-ez)*(az-ez))first=1;}
            for(int c=0;c<NativeBrickfallScenery.SmokeColumns;c++)
            {
                int col=c==0?first:1-first,n=0;
                for(int p=0;p<puffs;p++){NativeBrickfallScenery.SmokePuff(col,p*(NativeBrickfallScenery.SmokePuffs/puffs),time,out puffX[n],out puffY[n],out puffZ[n],out puffS[n],out puffA[n]);n++;}
                SortFar(n,puffX,puffY,puffZ);
                for(int k=0;k<n;k++)
                {
                    // Soot-dark low down among the roofs, warm grey higher up where the low sun catches the plume (and where only a
                    // lit plume reads against the dusk sky through the fog).
                    int i=sortIndex[k];Lit(NativeBrickfallScenery.Soot,0,.6,0,0,out double r,out double g,out double b);Lit(0x9A9088,light.Lx,light.Ly,light.Lz,0,out double sr,out double sg,out double sb);
                    double up=Math.Max(0,Math.Min(1,(puffY[i]-28)/50));up=up*up*(3-2*up);r+=(sr*.8-r)*up;g+=(sg*.8-g)*up;b+=(sb*.8-b)*up;
                    Sprite(puffX[i],puffY[i],puffZ[i],puffS[i],r,g,b,puffA[i]*(quality<.5?1:.95),false);
                }
            }
        }
        private void Aviation(double time)
        {
            for(int c=0;c<2;c++)for(int l=0;l<NativeBrickfallScenery.AviationLights;l++)
            {
                if(!NativeBrickfallScenery.AviationOn(c,l,time))continue;NativeBrickfallScenery.AviationLocal(l,out double lx,out double ly,out double lz);NativeBrickfallScenery.JibToWorld(c,time,lx,ly,lz,out double x,out double y,out double z);
                double s=.32;Quads.Add(x-s,y,z-s,x+s,y,z-s,x+s,y,z+s,x-s,y,z+s,2.4,.12,.05,1,0,false,0);
                Quads.Add(x-s,y-s,z,x+s,y-s,z,x+s,y+s,z,x-s,y+s,z,2.4,.12,.05,1,0,false,0);Quads.Add(x,y-s,z-s,x,y-s,z+s,x,y+s,z+s,x,y+s,z-s,2.4,.12,.05,1,0,false,0);
                double d=Math.Sqrt((x-ex)*(x-ex)+(y-ey)*(y-ey)+(z-ez)*(z-ez));Sprite(x,y,z,Math.Max(2.2,d*.035),1.6,.1,.04,.9,true);
            }
        }
        private void Flocks(double time,double quality,bool low)
        {
            int birds=quality<.5||low?NativeBrickfallScenery.FlockBirds/2:NativeBrickfallScenery.FlockBirds;
            for(int f=0;f<NativeBrickfallScenery.Flocks;f++)for(int i=0;i<birds;i++)
            {
                NativeBrickfallScenery.Bird(f,i*(NativeBrickfallScenery.FlockBirds/birds),time,out double x,out double y,out double z,out double yaw,out double flap);Birds++;
                Box(x,y,z,.16,.12,.42,0,yaw,0,0x2E2824);double wing=flap*35;
                double c=Math.Cos(yaw*Math.PI/180),s=Math.Sin(yaw*Math.PI/180);
                // Wings hinge at the body sides: each a thin box rolled by the beat, centred half a wing out.
                for(int side=-1;side<=1;side+=2){double out_=.32,ang=wing*Math.PI/180,ox=c*side*out_*Math.Cos(ang),oz=-s*side*out_*Math.Cos(ang),oy=out_*Math.Sin(ang);Box(x+ox,y+oy,z+oz,.62,.02,.2,0,yaw,side*wing,0x3A3230);}
            }
        }

        // ---------------- atmosphere ----------------
        private void CollapseDust(double time,double quality,bool low,NativeBrickfallMemory memory,double roundStart)
        {
            int per=NativeBrickfallAtmosphere.Count(NativeBrickfallAtmosphere.DustPuffs,quality,low),clouds=0,n=0;
            // This round's landings still drifting, newest first, up to the cloud and puff budgets.
            for(int k=0;k<memory.CollapseCount;k++)
            {
                memory.CollapseAt(k,out double t,out _,out _,out _,out _,out _);if(t<roundStart||t>time||time-t>NativeBrickfallAtmosphere.DustLife)continue;
                int j=clouds++;while(j>0&&cloudTime[j-1]<t){cloudTime[j]=cloudTime[j-1];cloudIndex[j]=cloudIndex[j-1];j--;}cloudTime[j]=t;cloudIndex[j]=k;
            }
            for(int c=0;c<Math.Min(clouds,NativeBrickfallAtmosphere.DustClouds);c++)
            {
                memory.CollapseAt(cloudIndex[c],out double ct,out double cx,out double cy,out double cz,out double cr,out double ce);
                for(int p=0;p<per&&n<NativeBrickfallAtmosphere.DustBudget;p++){NativeBrickfallAtmosphere.DustPuff(cloudIndex[c],p,cx,cy,cz,cr,ce,time-ct,out puffX[n],out puffY[n],out puffZ[n],out puffS[n],out puffA[n]);n++;}
            }
            SortFar(n,puffX,puffY,puffZ);
            for(int k=0;k<n;k++){int i=sortIndex[k];Lit(NativeBrickfallScenery.Dust,0,.7,0,0,out double r,out double g,out double b);Sprite(puffX[i],puffY[i],puffZ[i],puffS[i],r,g,b,puffA[i],false);DustPuffs++;}
        }
        private void ShaftsPass(double time,double quality,bool low)
        {
            int n=NativeBrickfallAtmosphere.Count(NativeBrickfallAtmosphere.Shafts,quality,low,true);
            // The shaft lies along the light's travel (toward -L); its width faces the camera around that axis.
            double dx=-light.Lx,dy=-light.Ly,dz=-light.Lz,dl=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(dl<1e-6)return;dx/=dl;dy/=dl;dz/=dl;
            for(int i=0;i<n;i++)
            {
                int s=n==NativeBrickfallAtmosphere.Shafts?i:i*2;NativeBrickfallAtmosphere.Shaft(s,time,out double x,out double y,out double z,out double length,out double width,out double alpha);
                double vx=ex-x,vy=ey-y,vz=ez-z,vd=Math.Sqrt(vx*vx+vy*vy+vz*vz);if(vd<1e-4)continue;vx/=vd;vy/=vd;vz/=vd;
                double along=vx*dx+vy*dy+vz*dz,distance=Math.Sqrt(Math.Max(0,1-along*along))*vd;alpha*=NativeBrickfallAtmosphere.ShaftView(distance,along);if(alpha<=.002)continue;
                double wx=dy*vz-dz*vy,wy=dz*vx-dx*vz,wz=dx*vy-dy*vx,wl=Math.Sqrt(wx*wx+wy*wy+wz*wz);if(wl<1e-6)continue;wx*=width/2/wl;wy*=width/2/wl;wz*=width/2/wl;
                double hx=dx*length/2,hy=dy*length/2,hz=dz*length/2;
                Quads.Add(x-hx-wx,y-hy-wy,z-hz-wz,x-hx+wx,y-hy+wy,z-hz+wz,x+hx+wx,y+hy+wy,z+hz+wz,x+hx-wx,y+hy-wy,z+hz-wz,light.Sr,light.Sg*.92,light.Sb*.8,alpha,1,true,1);Shafts++;
            }
        }
        private void MotesPass(double time,double quality,bool low)
        {
            int n=NativeBrickfallAtmosphere.Count(NativeBrickfallAtmosphere.Motes,quality,low);
            double r=Lin(NativeBrickfallScenery.Dust,16)*(light.Sr*.8+light.Ambient[3]),g=Lin(NativeBrickfallScenery.Dust,8)*(light.Sg*.8+light.Ambient[4]),b=Lin(NativeBrickfallScenery.Dust,0)*(light.Sb*.8+light.Ambient[5]);
            for(int i=0;i<n;i++){NativeBrickfallAtmosphere.Mote(i,ex,ey,ez,time,out double x,out double y,out double z,out double size,out double alpha);if(alpha<=.01)continue;Sprite(x,y,z,size*2.4,r,g,b,alpha*.55,true);Motes++;}
        }
        private void ScrapsPass(double time,double quality,bool low)
        {
            int n=NativeBrickfallAtmosphere.Count(NativeBrickfallAtmosphere.Scraps,quality,low);
            for(int i=0;i<n;i++)
            {
                NativeBrickfallAtmosphere.Scrap(i,time,out double x,out double y,out double z,out double pitch,out double yaw,out double roll,out double alpha);if(alpha<=.05)continue;
                double k=.6+.4*alpha;Box(x,y,z,.3*k,.004,.42*k,pitch,yaw,roll,i%4==0?0xC8B89A:i%4==1?NativeBrickfallScenery.Mortar:i%4==2?0xD8D0C0:NativeBrickfallScenery.Dust,.55);Scraps++;
            }
        }
        private void PigeonsPass(double time,double quality,bool low,NativeBrickfallMemory memory)
        {
            int n=NativeBrickfallAtmosphere.Count(NativeBrickfallMemory.Perches,Math.Max(.5,quality),low);
            for(int k=0;k<n;k++)
            {
                int i=k*NativeBrickfallMemory.Perches/n,count=memory.Spooks(i,spookScratch);double away=NativeBrickfallAtmosphere.Away(time,spookScratch,count);
                NativeBrickfallAtmosphere.Pigeon(i,time,away,out double x,out double y,out double z,out double yaw,out double flap,out double wings,out double head);Pigeons++;
                double c=Math.Cos(yaw*Math.PI/180),s=Math.Sin(yaw*Math.PI/180);
                const int body=0x7E828E,wing=0x6A6E78,neck=0x5E6A64;
                Box(x,y+.11,z,.13,.12,.27,0,yaw,0,body);
                double hy=y+.2-head*.09,hf=.13+head*.05;Box(x+s*hf,hy,z+c*hf,.08,.08,.09,head*40,yaw,0,neck);Box(x+s*(hf+.06),hy-.01,z+c*(hf+.06),.03,.025,.05,0,yaw,0,0xC8A27A);
                Box(x-s*.17,y+.11,z-c*.17,.1,.025,.13,-8,yaw,0,wing);
                if(wings<.5){for(int side=-1;side<=1;side+=2)Box(x+c*side*.065,y+.13,z-s*side*.065,.025,.07,.24,0,yaw,0,wing);}
                else for(int side=-1;side<=1;side+=2){double ang=flap*55*Math.PI/180,out_=.17;Box(x+c*side*out_*Math.Cos(ang),y+.14+out_*Math.Sin(ang),z-s*side*out_*Math.Cos(ang),.34,.015,.14,0,yaw,side*flap*55,wing);}
            }
        }
        private void TarpsPass(double time)
        {
            const int C=NativeBrickfallAtmosphere.TarpCols,R=NativeBrickfallAtmosphere.TarpRows;
            for(int t=0;t<NativeBrickfallAtmosphere.Tarps;t++)
            {
                int hex=NativeBrickfallAtmosphere.TarpColor(t);
                for(int row=0;row<R-1;row++)for(int col=0;col<C-1;col++)
                {
                    NativeBrickfallAtmosphere.TarpVertex(t,col,row,time,out double ax,out double ay,out double az);NativeBrickfallAtmosphere.TarpVertex(t,col+1,row,time,out double bx,out double by,out double bz);
                    NativeBrickfallAtmosphere.TarpVertex(t,col+1,row+1,time,out double cx,out double cy,out double cz);NativeBrickfallAtmosphere.TarpVertex(t,col,row+1,time,out double dx,out double dy,out double dz);
                    double e1x=cx-ax,e1y=cy-ay,e1z=cz-az,e2x=dx-bx,e2y=dy-by,e2z=dz-bz,nx=e1y*e2z-e1z*e2y,ny=e1z*e2x-e1x*e2z,nz=e1x*e2y-e1y*e2x,nl=Math.Sqrt(nx*nx+ny*ny+nz*nz);if(nl<1e-9)continue;nx/=nl;ny/=nl;nz/=nl;
                    // Light the side facing the arena; the wall behind keeps the sun off its back (no wrap).
                    NativeBrickfallAtmosphere.TarpNormal(t,out double wx,out double wz);if(nx*wx+nz*wz<0){nx=-nx;ny=-ny;nz=-nz;}
                    Lit(hex,nx,ny,nz,0,out double r,out double g,out double b);double fold=1-.07*row;
                    Quads.Add(ax,ay,az,bx,by,bz,cx,cy,cz,dx,dy,dz,r*fold,g*fold,b*fold,1,0,false,0);
                }
            }
        }
    }
}
