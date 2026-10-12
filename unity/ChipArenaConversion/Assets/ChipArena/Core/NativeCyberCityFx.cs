using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>A screen the atmosphere animates: centre, half-width and half-height axes, outward normal (world), seed, and for a
    /// billboard its segment's replicated state and t0 (ARENA_PROPOSAL.md §5.3; -1 for a backdrop screen, which always plays).</summary>
    public struct NativeCyberScreen {public float X,Y,Z,Ux,Uy,Uz,Vx,Vy,Vz,Nx,Ny,Nz,T0;public int Seed,State;public bool Live;}

    /// <summary>
    /// What the Cyber City atmosphere needs from the map, built once (<see cref="Build"/>): a 1 m roof map over the slab (the
    /// topmost surface of every cell, from blocks at least .6 m across) for the rain and its splashes; the kit's glow sources
    /// (wet reflections, rain haze); steam vents; the alcoves' beacons; the couplings' decks and the maglev's track from the
    /// hazards; the backdrop's screens and beacons. No allocation after Build.
    /// </summary>
    public sealed class NativeCyberFxScene
    {
        public const int MapX=80,MapZ=64;public const float OriginX=-40,OriginZ=-32;
        public readonly float[] Roof=new float[MapX*MapZ];
        public NativeCyberGlow[] Glows=Array.Empty<NativeCyberGlow>();public float[] VentX=Array.Empty<float>(),VentY=Array.Empty<float>(),VentZ=Array.Empty<float>();
        public float[] BeaconX=Array.Empty<float>(),BeaconZ=Array.Empty<float>();public MapHazard[] Hazards=Array.Empty<MapHazard>();public int Maglev=-1;public int[] Grids={-1,-1};
        public NativeCyberScreen[] Screens=Array.Empty<NativeCyberScreen>();public float[] AviationX=Array.Empty<float>(),AviationY=Array.Empty<float>(),AviationZ=Array.Empty<float>();
        /// <summary>The topmost surface over (x, z): the roof map on the slab (the Cut's floor where nothing covers it), the street beyond.</summary>
        public float Top(double x,double z)
        {
            int i=(int)Math.Floor(x-OriginX),j=(int)Math.Floor(z-OriginZ);
            if(i<0||j<0||i>=MapX||j>=MapZ)return 0;return Roof[j*MapX+i];
        }
        public static NativeCyberFxScene Build(ContentBlock[] blocks,MapHazard[] hazards,NativeCyberCityKit kit,NativeCyberBox[] backdrop)
        {
            var s=new NativeCyberFxScene{Hazards=hazards??Array.Empty<MapHazard>()};
            for(int k=0;k<s.Roof.Length;k++)s.Roof[k]=float.NegativeInfinity;
            foreach(var b in blocks??Array.Empty<ContentBlock>())
            {
                if(b==null||b.role=="rubble"||b.size.x<.6f||b.size.z<.6f)continue;float top=b.pos.y+b.size.y/2,x0=b.pos.x-b.size.x/2,x1=b.pos.x+b.size.x/2,z0=b.pos.z-b.size.z/2,z1=b.pos.z+b.size.z/2;
                for(int j=Math.Max(0,(int)Math.Floor(z0-OriginZ));j<Math.Min(MapZ,(int)Math.Ceiling(z1-OriginZ));j++)for(int i=Math.Max(0,(int)Math.Floor(x0-OriginX));i<Math.Min(MapX,(int)Math.Ceiling(x1-OriginX));i++)
                {float cx=OriginX+i+.5f,cz=OriginZ+j+.5f;if(cx>=x0&&cx<=x1&&cz>=z0&&cz<=z1&&top>s.Roof[j*MapX+i])s.Roof[j*MapX+i]=top;}
            }
            for(int k=0;k<s.Roof.Length;k++)if(float.IsNegativeInfinity(s.Roof[k]))s.Roof[k]=0;
            for(int i=0;i<s.Hazards.Length;i++){if(NativeCyberCityHazards.IsMaglev(s.Hazards[i])&&s.Maglev<0)s.Maglev=i;if(NativeCyberCityHazards.IsGrid(s.Hazards[i]))s.Grids[s.Hazards[i].center.x<0?0:1]=i;}
            var glows=new List<NativeCyberGlow>();var vx=new List<float>();var vy=new List<float>();var vz=new List<float>();var bx=new List<float>();var bz=new List<float>();
            if(kit!=null)
            {
                foreach(var g in kit.Glows)if(g.Size>=.8f)glows.Add(g);
                foreach(var p in kit.Parts)
                {
                    if(p.Parent<0||blocks==null||p.Parent>=blocks.Length)continue;var b=blocks[p.Parent];float wx=b.pos.x+p.X,wy=b.pos.y+p.Y,wz=b.pos.z+p.Z;
                    if(p.Drive==NativeCyberCityKit.DriveSafe){bx.Add(wx);bz.Add(wz);}
                    // Street vents and manholes (grate plates on the ground) and rooftop plant grilles breathe steam.
                    if(p.Look==NativeCyberCityLooks.Grate&&(p.Flags&NativeCyberCityLooks.Bank)!=0&&p.Hx<.6f&&p.Hz<.6f&&p.Hy<.02f){vx.Add(wx);vy.Add(wy);vz.Add(wz);}
                    else if(p.Look==NativeCyberCityLooks.Grate&&wy>8&&p.Hy<.02f){vx.Add(wx);vy.Add(wy);vz.Add(wz);}
                }
            }
            s.Glows=glows.ToArray();s.VentX=vx.ToArray();s.VentY=vy.ToArray();s.VentZ=vz.ToArray();s.BeaconX=bx.ToArray();s.BeaconZ=bz.ToArray();
            var screens=new List<NativeCyberScreen>();var ax=new List<float>();var ay=new List<float>();var az=new List<float>();
            foreach(var b in backdrop??Array.Empty<NativeCyberBox>())
            {
                if(b.Kind==3){ax.Add(b.X);ay.Add(b.Y);az.Add(b.Z);continue;}if(b.Kind!=2)continue;
                bool xFace=b.Hx<b.Hz;float nx=xFace?-Math.Sign(b.X):0,nz=xFace?0:-Math.Sign(b.Z);
                screens.Add(new NativeCyberScreen{X=b.X+nx*(b.Hx+.05f),Y=b.Y,Z=b.Z+nz*(b.Hz+.05f),Ux=xFace?0:b.Hx,Uz=xFace?b.Hz:0,Vy=b.Hy,Nx=nx,Nz=nz,Seed=(int)(Math.Abs(b.X*7+b.Z*13))%9973,State=-1,Live=true});
            }
            s.Screens=screens.ToArray();s.AviationX=ax.ToArray();s.AviationY=ay.ToArray();s.AviationZ=az.ToArray();
            return s;
        }
    }

    /// <summary>
    /// Builds Cyber City's camera-dependent quads for one frame into a <see cref="NativeFxQuads"/> (Runtime draws them as one solid
    /// and one effects NativeSirenFx batch: two draws), docs/arenas/CYBERCITY_PROPOSAL.md §8 and §9:
    ///  - backdrop life (also behind the menu): the flying traffic (dark bodies, head, tail and underglow lights), the expressway
    ///    light streams, the searchlights in the cloud deck, the towers' aviation beacons, the holo screens' moving content;
    ///  - atmosphere (arena and killcam only, scaled by quality and the low tier): rain streaks round the eye (none under cover:
    ///    the scene's roof map), splashes on the topmost surfaces, steam from street vents and rooftop plant, rain haze round the
    ///    big neon, and the wet ground's reflection streaks of the neon toward the eye;
    ///  - the hazards' telegraphs and events (never reduced, ARENA_PROPOSAL.md §2.1 rule 4): the maglev's alcove beacons and their
    ///    light pools, the tunnel glare and headlight cone as it approaches, the head and tail lights, rail sparks and spray as it
    ///    passes; the couplings' arcs, deck glow, lamp halos and sparks; the billboards' glitch (stressed), noise (falling) and
    ///    dying flicker with sparks (rubble).
    /// Pure: the same clock, camera, light, quality, states and scene give the same quads. Allocation free.
    /// </summary>
    public sealed class NativeCyberCityFx
    {
        public const int Capacity=3600;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        public const int RainFull=720,SplashFull=96,SteamPer=6,HazeMax=40,ReflectMax=64;
        public const double RainRadius=16,RainHeight=24,RainSpeed=11,WindX=1.6,WindZ=.5;
        public int Rain,Splashes,Steam,Haze,Reflections,Pools,Cars,Lights,Beams,Telegraph;
        public const int PoolMax=56;
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz;private NativeFxLight light;
        private readonly double[] key=new double[96],px=new double[96],py=new double[96],pz=new double[96],ps=new double[96],pa=new double[96];private readonly int[] order=new int[96];

        private static uint Mix(uint x){unchecked{x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x;}}
        private static double H(int a,int b=0,int c=0){unchecked{return Mix(Mix(Mix((uint)a+0x9E3779B9U)^(uint)b*0x85EBCA6BU)+(uint)c*0xC2B2AE35U)/4294967296.0;}}
        private static double Frac(double v)=>v-Math.Floor(v);
        private static double Lin(int hex,int shift)=>NativeSceneryBuilder.Channel(hex,shift);
        /// <summary>A count for an effects quality and the low tier (NativeBrickfallAtmosphere.Count's rule: full x quality, at
        /// least .3, halved on the low tier).</summary>
        public static int Count(int full,double quality,bool low)=>NativeBrickfallAtmosphere.Count(full,quality,low);

        /// <summary>
        /// Fills <see cref="Quads"/>. `time`: the presentation clock (match time; the render clock behind the menu). The camera's
        /// position and its right and up vectors orient the sprites. `states` are the hazards' states at `time` (indexed like the
        /// scene's hazards; null: all idle); `screens` the billboards' screens this frame (their segment state filled in), drawn
        /// after the scene's backdrop screens. `atmosphere` false draws only the backdrop life (the menu).
        /// </summary>
        public void Build(NativeCyberFxScene scene,double time,double camX,double camY,double camZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,NativeFxLight sun,double quality,bool low,bool atmosphere,NativeHazardState[] states,NativeCyberScreen[] screens,int screenCount)
        {
            Quads.Clear();ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;light=sun;Rain=Splashes=Steam=Haze=Reflections=Pools=Cars=Lights=Beams=Telegraph=0;
            Searchlights(time,low);Traffic(time,quality,low);Streams(time,quality,low);Aviation(scene,time);
            foreach(var sc in scene.Screens)ScreenFx(sc,time);
            if(!atmosphere)return;
            for(int i=0;i<screenCount&&screens!=null&&i<screens.Length;i++)if(screens[i].Live)ScreenFx(screens[i],time);
            MaglevFx(scene,states,time);GridFx(scene,states,time);
            PoolFx(scene,quality,low);Reflect(scene,quality,low);HazeFx(scene,quality,low);SteamFx(scene,time,quality,low);RainFx(scene,states,time,quality,low);SplashFx(scene,time,quality,low);
        }

        // ---------------- primitives ----------------
        private void Sprite(double x,double y,double z,double size,double r,double g,double b,double a,bool additive)
        {
            double h=size/2,ax=rx*h,ay=ry*h,az=rz*h,bx=ux*h,by=uy*h,bz=uz*h;
            Quads.Add(x-ax-bx,y-ay-by,z-az-bz,x+ax-bx,y+ay-by,z+az-bz,x+ax+bx,y+ay+by,z+az+bz,x-ax+bx,y-ay+by,z-az+bz,r,g,b,a,1,additive,1);
        }
        // A sprite that never shrinks below `minPixels`-ish on screen: size grows with distance at `perMetre`.
        private void Point(double x,double y,double z,double size,double perMetre,double r,double g,double b,double a,bool additive=true)
        {double d=Math.Sqrt((x-ex)*(x-ex)+(y-ey)*(y-ey)+(z-ez)*(z-ez));Sprite(x,y,z,Math.Max(size,d*perMetre),r,g,b,a,additive);}
        // A flat quad lying in the plane y (soft radial when mode 1), centred at (x, z), half axes along (ax, az) and its perpendicular.
        private void Flat(double x,double y,double z,double ax,double az,double halfLength,double halfWidth,double r,double g,double b,double a,int mode,bool additive)
        {
            double l=Math.Sqrt(ax*ax+az*az);if(l<1e-6){ax=1;az=0;l=1;}ax/=l;az/=l;double px_=-az,pz_=ax;
            double lx=ax*halfLength,lz=az*halfLength,wx=px_*halfWidth,wz=pz_*halfWidth;
            Quads.Add(x-lx-wx,y,z-lz-wz,x+lx-wx,y,z+lz-wz,x+lx+wx,y,z+lz+wz,x-lx+wx,y,z-lz+wz,r,g,b,a,mode,additive,1);
        }
        // A camera-facing ribbon from A to B, `width` wide.
        // A soft light shaft (radial falloff across and along): it fades as the eye lines up with it, so looking down the beam
        // never shows the ribbon as a flat wedge.
        private void Beam(double ax,double ay,double az,double bx,double by,double bz,double width,double r,double g,double b,double a)
        {
            double dx=bx-ax,dy=by-ay,dz=bz-az,dl=Math.Sqrt(dx*dx+dy*dy+dz*dz),mx=(ax+bx)/2-ex,my=(ay+by)/2-ey,mz=(az+bz)/2-ez,ml=Math.Sqrt(mx*mx+my*my+mz*mz);if(dl<1e-6||ml<1e-6)return;
            double c=Math.Abs(dx*mx+dy*my+dz*mz)/(dl*ml),side=1-c*c;Line(ax,ay,az,bx,by,bz,width,r,g,b,a*side*side,true,1);
        }
        private void Line(double ax,double ay,double az,double bx,double by,double bz,double width,double r,double g,double b,double a,bool additive=true,int mode=0)
        {
            double dx=bx-ax,dy=by-ay,dz=bz-az,mx=(ax+bx)/2-ex,my=(ay+by)/2-ey,mz=(az+bz)/2-ez;
            double sx=dy*mz-dz*my,sy=dz*mx-dx*mz,sz=dx*my-dy*mx,sl=Math.Sqrt(sx*sx+sy*sy+sz*sz);if(sl<1e-9)return;sx*=width/2/sl;sy*=width/2/sl;sz*=width/2/sl;
            Quads.Add(ax-sx,ay-sy,az-sz,bx-sx,by-sy,bz-sz,bx+sx,by+sy,bz+sz,ax+sx,ay+sy,az+sz,r,g,b,a,mode,additive,1);
        }
        private void Lit(int hex,double nx,double ny,double nz,out double r,out double g,out double b)
        {NativeBrickfallAtmosphere.Shade(Lin(hex,16),Lin(hex,8),Lin(hex,0),nx,ny,nz,light.Lx,light.Ly,light.Lz,light.Sr,light.Sg,light.Sb,light.Ambient,0,out r,out g,out b);}
        private readonly double[] corner=new double[12],hb=new double[3],lb=new double[3];
        // A box turned `yaw` degrees about y (Unity Euler(0, yaw, 0): +z turns toward +x), each face lit by its own normal
        // (solid layer), faces turned away from the eye skipped (the batch is two-sided).
        private void Body(double x,double y,double z,double sx,double sy,double sz,double yaw,int hex)
        {
            double t=yaw*Math.PI/180,cb=Math.Cos(t),sb=Math.Sin(t);var h=hb;h[0]=sx/2;h[1]=sy/2;h[2]=sz/2;
            for(int f=0;f<6;f++)
            {
                int axis=f/2;double sg=f%2==0?1:-1,nx=axis==0?sg:0,ny=axis==1?sg:0,nz=axis==2?sg:0,wx=cb*nx+sb*nz,wz=-sb*nx+cb*nz;
                double ext=h[axis],fx=x+wx*ext,fy=y+ny*ext,fz=z+wz*ext;if((ex-fx)*wx+(ey-fy)*ny+(ez-fz)*wz<=0)continue;
                Lit(hex,wx,ny,wz,out double r,out double g,out double b);int ua=(axis+1)%3,va=(axis+2)%3;var q=corner;
                for(int k=0;k<4;k++)
                {
                    double su=k==1||k==2?1:-1,sv=k>=2?1:-1;var l=lb;l[0]=nx*h[0];l[1]=ny*h[1];l[2]=nz*h[2];l[ua]=su*h[ua];l[va]=sv*h[va];
                    q[k*3]=x+cb*l[0]+sb*l[2];q[k*3+1]=y+l[1];q[k*3+2]=z-sb*l[0]+cb*l[2];
                }
                Quads.Add(q[0],q[1],q[2],q[3],q[4],q[5],q[6],q[7],q[8],q[9],q[10],q[11],r,g,b,1,0,false,0);
            }
        }
        private void SortFar(int n)
        {
            for(int i=0;i<n;i++){double dx=px[i]-ex,dy=py[i]-ey,dz=pz[i]-ez;key[i]=dx*dx+dy*dy+dz*dz;order[i]=i;}
            for(int i=1;i<n;i++){int v=order[i];double k=key[v];int j=i-1;while(j>=0&&key[order[j]]<k){order[j+1]=order[j];j--;}order[j+1]=v;}
        }

        // ---------------- backdrop life ----------------
        private void Searchlights(double time,bool low)
        {
            if(low)return;
            for(int i=0;i<NativeCyberCityScenery.Searchlights;i++)
            {
                NativeCyberCityScenery.Searchlight(i,time,out double x,out double y,out double z,out double dx,out double dy,out double dz);double len=420;
                // The beam's width faces the camera round its axis, widening toward the clouds.
                double mx=x+dx*len/2-ex,my=y+dy*len/2-ey,mz=z+dz*len/2-ez,sx=dy*mz-dz*my,sy=dz*mx-dx*mz,sz=dx*my-dy*mx,sl=Math.Sqrt(sx*sx+sy*sy+sz*sz);if(sl<1e-6)continue;sx/=sl;sy/=sl;sz/=sl;
                double w0=2.5,w1=26,bx=x+dx*len,by=y+dy*len,bz=z+dz*len,ml=Math.Sqrt(mx*mx+my*my+mz*mz),c=ml>1e-6?Math.Abs(dx*mx+dy*my+dz*mz)/ml:1,fa=.07*(1-c*c);
                Quads.Add(x-sx*w0,y-sy*w0,z-sz*w0,bx-sx*w1,by-sy*w1,bz-sz*w1,bx+sx*w1,by+sy*w1,bz+sz*w1,x+sx*w0,y+sy*w0,z+sz*w0,.55,.62,.9,Math.Max(.004,fa),0,true,1);Beams++;
                Point(x,y,z,4,.012,.8,.85,1,.6);
            }
        }
        private void Traffic(double time,double quality,bool low)
        {
            for(int l=0;l<NativeCyberCityScenery.Lanes;l++)
            {
                int cars=low||quality<.5?NativeCyberCityScenery.LaneCars[l]/2:NativeCyberCityScenery.LaneCars[l];
                for(int i=0;i<cars;i++)
                {
                    int k=i*NativeCyberCityScenery.LaneCars[l]/cars;NativeCyberCityScenery.Car(l,k,time,out double x,out double y,out double z,out double yaw,out bool magenta);Cars++;
                    double c=Math.Sin(yaw*Math.PI/180),s=Math.Cos(yaw*Math.PI/180);
                    Body(x,y,z,2.0,.8,4.4,yaw,0x1A1D28);
                    Point(x+c*2.3,y,z+s*2.3,.5,.004,2.2,2.3,2.4,.95);Point(x-c*2.3,y+.05,z-s*2.3,.4,.0035,2.4,.15,.12,.9);
                    Point(x,y-.55,z,1.4,.006,magenta?1.2:.15,magenta?.12:.9,magenta?1:1.3,.35);Lights+=3;
                }
            }
        }
        private void Streams(double time,double quality,bool low)
        {
            int n=Count(NativeCyberCityScenery.StreamLights,quality,low);
            for(int r=0;r<2;r++)for(int lane=0;lane<2;lane++)for(int i=0;i<n;i++)
            {
                NativeCyberCityScenery.Stream(r,lane,i*NativeCyberCityScenery.StreamLights/n,time,out double x,out double y,out double z,out bool head);
                if(head)Point(x,y,z,.6,.0045,2.0,2.1,2.3,.85);else Point(x,y,z,.5,.004,2.2,.12,.1,.85);Lights++;
            }
        }
        private void Aviation(NativeCyberFxScene scene,double time)
        {
            for(int i=0;i<scene.AviationX.Length;i++)
            {
                double on=Frac(time*.8+H(i,1));if(on>.18)continue;
                Point(scene.AviationX[i],scene.AviationY[i],scene.AviationZ[i],1.6,.012,2.2,.08,.05,.95);Lights++;
            }
        }
        /// <summary>
        /// A screen's moving content over its face: two drifting colour blocks, a scanning bright band and, for a billboard, its
        /// segment's story: glitch slices and colour split while stressed (the telegraph), noise while falling, a 3 s dying flicker
        /// after it lands, then dark.
        /// </summary>
        private void ScreenFx(in NativeCyberScreen s,double time)
        {
            double level=1,glitch=0;
            if(s.State==1){glitch=1;level=Frac(time*8)<.5?1:.35;}
            else if(s.State==2){glitch=1.5;level=.6+.4*H((int)(time*20),s.Seed);}
            else if(s.State==3){double since=time-s.T0;if(since>3)return;level=(1-since/3)*(H((int)(time*14),s.Seed)<.5?1:.15);glitch=.8;}
            double nx=s.Nx*.04,nz=s.Nz*.04,ny=s.Ny*.04;
            for(int k=0;k<3;k++)
            {
                double u=Frac(time*(.05+.03*k)+H(s.Seed,k))*2-1,v=(H(s.Seed,k,2)*2-1)*.5,w=.18+.2*H(s.Seed,k,3),h=.25+.3*H(s.Seed,k,4);
                if(glitch>0){u+=(H((int)(time*24),s.Seed,k)-.5)*.3*glitch;v+=(H((int)(time*24),s.Seed,k+9)-.5)*.2*glitch;}
                u=Math.Max(-1+w,Math.Min(1-w,u));
                int hex=k==0?0xFF3DCF:k==1?0x22E6FF:0x8B5CFF;double a=.32*level;
                Panel(s,u,v,w,h,nx*(1+k),ny,nz*(1+k),Lin(hex,16)*1.6,Lin(hex,8)*1.6,Lin(hex,0)*1.6,a);
            }
            double band=Frac(time*.35+H(s.Seed,7))*2-1;Panel(s,0,band,1,.05,nx*4,ny,nz*4,1.2,1.3,1.5,.25*level);
            if(glitch>0)for(int k=0;k<4;k++){double v=H((int)(time*30),s.Seed,k+20)*2-1;Panel(s,(H((int)(time*30),s.Seed,k+30)-.5)*.4,v,1,.04,nx*5,ny,nz*5,1.4,.2,1.2,.45*level);}
            Telegraph+=s.State>=1?1:0;
        }
        private void Panel(in NativeCyberScreen s,double u,double v,double hw,double hh,double ox,double oy,double oz,double r,double g,double b,double a)
        {
            double cx=s.X+s.Ux*u+s.Vx*v+ox,cy=s.Y+s.Uy*u+s.Vy*v+oy,cz=s.Z+s.Uz*u+s.Vz*v+oz,ax=s.Ux*hw,ay=s.Uy*hw,az=s.Uz*hw,bx=s.Vx*hh,by=s.Vy*hh,bz=s.Vz*hh;
            Quads.Add(cx-ax-bx,cy-ay-by,cz-az-bz,cx+ax-bx,cy+ay-by,cz+az-bz,cx+ax+bx,cy+ay+by,cz+az+bz,cx-ax+bx,cy-ay+by,cz-az+bz,r,g,b,a,0,true,1);
        }

        // ---------------- hazards (never reduced) ----------------
        private void MaglevFx(NativeCyberFxScene scene,NativeHazardState[] states,double time)
        {
            if(scene.Maglev<0||states==null||scene.Maglev>=states.Length)return;var h=scene.Hazards[scene.Maglev];var s=states[scene.Maglev];var ph=s.Phase;
            double floor=h.center.y-h.size.y/2,half=h.size.x/2;
            // The Cut's walls take the edge strips' light: a cool wash at rest, amber in the warning, red strobing in the danger and
            // the pass (overlapping soft panels 16 m wide every 8 m on both walls, the wall's full height).
            {
                double wr=.12,wg=.55,wb=.85,wa=.5;
                if(ph==NativeHazardPhase.Warn){wr=.9;wg=.42;wb=.04;wa=.55+.25*Math.Sin(time*6.3);}
                else if(ph==NativeHazardPhase.Danger||ph==NativeHazardPhase.Active){bool on=Frac(time*4)<.5;wr=on?1.1:.3;wg=on?.06:.02;wb=on?.05:.02;wa=.75;}
                // Two bands: the upper half under the edge strip at full wash, the lower half dimmer (the light falls off down the wall).
                double wall=h.size.z/2+.3-.03,yc=floor/2;
                for(int side=-1;side<=1;side+=2)for(double x=h.center.x-half;x<=h.center.x+half+.01;x+=8)
                {
                    double z=side*wall;Quads.Add(x-8,yc,z,x+8,yc,z,x+8,0,z,x-8,0,z,wr*.1,wg*.1,wb*.1,wa,1,true,1);Quads.Add(x-8,floor,z,x+8,floor,z,x+8,yc,z,x-8,yc,z,wr*.04,wg*.04,wb*.04,wa,1,true,1);Telegraph+=2;
                }
            }
            if(s.Telegraph||ph==NativeHazardPhase.Active)
            {
                // Beacons over every alcove and their light pools on the Cut's floor: amber in the warning, red (4 Hz) in the danger.
                bool red=ph!=NativeHazardPhase.Warn;double spin=time*(red?9:5);
                for(int i=0;i<scene.BeaconX.Length;i++)
                {
                    double bx=scene.BeaconX[i],bz=scene.BeaconZ[i],pulse=red?(Frac(time*4)<.5?1:.25):.6+.4*Math.Sin(spin+i);
                    double cr=red?2.6:2.4,cg=red?.12:1.05,cb=red?.08:.05;
                    Point(bx,-1.7,bz-Math.Sign(bz)*.12,.9,.006,cr,cg,cb,.85*pulse);
                    Flat(bx,floor+.02,bz-Math.Sign(bz)*1.4,Math.Cos(spin+i),Math.Sin(spin+i),2.2,1.1,cr*.35,cg*.35,cb*.35,.6*pulse,1,true);Telegraph+=2;
                }
            }
            if(double.IsNaN(s.Head)||ph==NativeHazardPhase.Idle)return;
            int d=s.Direction;double near=h.center.x-d*half;
            if(s.Telegraph)
            {
                // The tunnel mouth glows as the head comes, and its headlight cone reaches out of the portal in the last 1.5 s.
                double k=Math.Max(0,Math.Min(1,1-s.Lead/h.warn));Point(near,floor+1.8,0,3+5*k,.0,1.4*k,1.5*k,1.7*k,.5+.4*k);Telegraph++;
                if(s.Lead<1.5){double reach=(1.5-s.Lead)/1.5*30;Beam(near-d*2,floor+1.3,0,near+d*reach,floor+1.0,0,3.2,.5*k,.55*k,.65*k,.35);Flat(near+d*reach*.6,floor+.35,0,1,0,reach*.5,2.2,.6,.65,.8,.25,1,true);Telegraph+=2;}
            }
            // The train's own lights: twin head lamps and the cone ahead, red tail lamps, sparks off the rails and spray along its flanks.
            double head=s.Head,tail=s.Tail,rail=NativeCyberCityKit.RailTop;
            if(Math.Abs(head)<half+90)
            {
                for(int k=-1;k<=1;k+=2)Point(head-d*.3,rail+.75,k*1.4,1.1,.004,3,3.1,3.3,.95);
                Beam(head,rail+1.0,0,head+d*26,rail+.6,0,4.5,.35,.38,.45,.3);Flat(head+d*12,floor+.03,0,1,0,12,2.4,.5,.55,.7,.28,1,true);Telegraph+=4;
            }
            if(Math.Abs(tail)<half+90)for(int k=-1;k<=1;k+=2){Point(tail+d*.3,rail+.75,k*1.4,.9,.004,2.8,.15,.1,.9);Telegraph++;}
            if(ph==NativeHazardPhase.Active)
            {
                double lo=Math.Max(-half,Math.Min(head,tail)),hi=Math.Min(half,Math.Max(head,tail));
                for(int i=0;i<18;i++)
                {
                    double t=H((int)(time*30),i,1),x=lo+(hi-lo)*t,z=(H((int)(time*30),i,2)<.5?-1:1)*.62,len=.6+H((int)(time*30),i,3)*1.4;
                    Line(x,rail+.12,z,x-d*len,rail+.12+H((int)(time*30),i,4)*.4,z*(1.3+H(i,5)),.04,2.6,1.6,.6,.9);Telegraph++;
                }
                for(int i=0;i<10;i++){double x=lo+(hi-lo)*Frac(H(i,7)+time*.9),z=(i%2==0?-1:1)*2.75;Point(x,floor+1.2+H(i,8)*1.2,z,2.4,0,.22,.25,.32,.22,false);Telegraph++;}
            }
        }
        private void GridFx(NativeCyberFxScene scene,NativeHazardState[] states,double time)
        {
            for(int side=0;side<2;side++)
            {
                int gi=scene.Grids[side];if(gi<0||states==null||gi>=states.Length)continue;var h=scene.Hazards[gi];var s=states[gi];var ph=s.Phase;if(ph==NativeHazardPhase.Idle)continue;
                double x0=h.center.x-h.size.x/2+.5,x1=h.center.x+h.size.x/2-.5,z0=h.center.z-h.size.z/2-.4,z1=h.center.z+h.size.z/2+.4,y=h.center.y-h.size.y/2+.06;
                double level=ph==NativeHazardPhase.Warn?s.Level:ph==NativeHazardPhase.Active?1:s.Level*.6;
                Flat(h.center.x,y+.02,h.center.z,0,1,(z1-z0)/2+.4,(x1-x0)/2+.6,.25*level,.75*level,1.1*level,.55,1,true);Telegraph++;
                int arcs=ph==NativeHazardPhase.Warn?1+(int)(s.Level*3):ph==NativeHazardPhase.Active?9:2;int bucket=(int)Math.Floor(time*24);
                for(int a=0;a<arcs;a++)
                {
                    // A jagged arc of six links between two points on the deck's rails (near the pylons while charging).
                    double ax=x0+(x1-x0)*Math.Floor(H(bucket,a,side)*4)/3,bx=x0+(x1-x0)*Math.Floor(H(bucket,a,side+7)*4)/3;
                    double az=ph==NativeHazardPhase.Warn?(H(bucket,a,3)<.5?z0:z1):z0+(z1-z0)*H(bucket,a,4),bz=az+(H(bucket,a,5)-.5)*(ph==NativeHazardPhase.Warn?1.2:3.4);
                    double lx=ax,ly=y,lz=az,bright=ph==NativeHazardPhase.Active?1:.6;
                    for(int k=1;k<=6;k++)
                    {
                        double t=k/6.0,nx=ax+(bx-ax)*t+(k<6?(H(bucket,a*10+k,6)-.5)*.5:0),ny=y+Math.Sin(t*Math.PI)*(.25+.5*H(bucket,a,8))+(k<6?(H(bucket,a*10+k,9)-.5)*.2:0),nz=az+(bz-az)*t+(k<6?(H(bucket,a*10+k,10)-.5)*.5:0);
                        Line(lx,ly,lz,nx,ny,nz,.05,1.6*bright,2.2*bright,3*bright,.95);lx=nx;ly=ny;lz=nz;Telegraph++;
                    }
                }
                if(ph==NativeHazardPhase.Active)for(int i=0;i<14;i++)
                {
                    double t=Frac(time*1.7+H(i,side)),x=x0+(x1-x0)*H(i,side,1),z=z0+(z1-z0)*H(i,side,2),hgt=Math.Sin(t*Math.PI)*.8;
                    Point(x+(t-.5)*.6,y+hgt,z,.12,0,3,2.6,1.6,1-t);Telegraph++;
                }
                // Lamp halos on the four pylons (amber charging, red live).
                double lr=ph==NativeHazardPhase.Warn?2.4:2.6,lg=ph==NativeHazardPhase.Warn?1.05:.12,lb=ph==NativeHazardPhase.Warn?.05:.08,lp=ph==NativeHazardPhase.Warn?(Frac(time*3)<.5?1:.25):ph==NativeHazardPhase.Active?1:.4;
                for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2){Point(h.center.x+sx*(h.size.x/2+.4),2.75,sz*3.8,1.4,.005,lr,lg,lb,.8*lp);Telegraph++;}
            }
        }

        // ---------------- atmosphere ----------------
        private void PoolFx(NativeCyberFxScene scene,double quality,bool low)
        {
            // Each glow source's light on the surface below it (the night's light pools): a soft additive disc, wider and dimmer
            // the higher the source; a source over its own wall's cell looks a metre to each side for the floor.
            int budget=Count(PoolMax,quality,low);
            for(int i=0;i<scene.Glows.Length&&Pools<budget;i++)
            {
                var g=scene.Glows[i];double dx=g.X-ex,dz=g.Z-ez;if(dx*dx+dz*dz>48*48)continue;
                double top=scene.Top(g.X,g.Z),x=g.X,z=g.Z;
                if(top>g.Y-.25){double best=double.MaxValue;for(int k=0;k<4;k++){double ox=k==0?1.2:k==1?-1.2:0,oz=k==2?1.2:k==3?-1.2:0,t=scene.Top(g.X+ox,g.Z+oz);if(t<g.Y-.25&&g.Y-t<best){best=g.Y-t;top=t;x=g.X+ox*.6;z=g.Z+oz*.6;}}if(best==double.MaxValue)continue;}
                double height=g.Y-top;if(height>14)continue;bool cut=top<-1;double radius=Math.Max(1,Math.Min(cut?3:7,g.Size*1.1+height*.55)),k2=(cut?.08:.22)/(1+height*.22);
                Flat(x,top+.035,z,1,0,radius,radius,g.R*k2,g.G*k2,g.B*k2,1,1,true);Pools++;
            }
        }
        private void Reflect(NativeCyberFxScene scene,double quality,bool low)
        {
            // Each glow source mirrored in the wet street below it: the point where the eye's ray to the mirror image meets the
            // street, a soft streak stretched toward the eye; only on open sky, never in the Cut, fading with distance.
            int budget=Count(ReflectMax,quality,low);if(ey<.3)return;
            for(int i=0;i<scene.Glows.Length&&Reflections<budget;i++)
            {
                var g=scene.Glows[i];if(g.Y<.4||g.Y>16)continue;double dx=g.X-ex,dz=g.Z-ez,dd=dx*dx+dz*dz;if(dd>45*45)continue;
                double my=-g.Y,t=ey/(ey-my);if(!(t>0&&t<1))continue;double x=ex+(g.X-ex)*t,z=ez+(g.Z-ez)*t;
                if(Math.Abs(z)<3.2&&Math.Abs(x)<40||scene.Top(x,z)>.15)continue;
                double vx=ex-x,vz=ez-z,vl=Math.Sqrt(vx*vx+vz*vz);if(vl<.5)continue;double grazing=1-Math.Min(1,ey/Math.Max(.5,Math.Sqrt(vl*vl+ey*ey)));
                double fade=Math.Max(0,1-Math.Sqrt(dd)/45),k=.32*(.3+.7*grazing*grazing)*fade,len=Math.Min(6,.8+g.Y*.45),wid=Math.Max(.25,g.Size*.22);
                Flat(x+vx/vl*len*.35,.03,z+vz/vl*len*.35,vx,vz,len,wid,g.R*k,g.G*k,g.B*k,.9,1,true);Reflections++;
            }
        }
        private void HazeFx(NativeCyberFxScene scene,double quality,bool low)
        {
            int budget=Count(HazeMax,quality,low);
            for(int i=0;i<scene.Glows.Length&&Haze<budget;i++)
            {
                var g=scene.Glows[i];if(g.Size<1.6f)continue;double dx=g.X-ex,dy=g.Y-ey,dz=g.Z-ez,d=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(d>70||d<2)continue;
                double a=.075*Math.Min(1,(d-2)/6);Sprite(g.X,g.Y,g.Z,g.Size*2.4,g.R*a,g.G*a,g.B*a,1,true);Haze++;
            }
        }
        private void SteamFx(NativeCyberFxScene scene,double time,double quality,bool low)
        {
            int per=Math.Max(2,Count(SteamPer,quality,low)),n=0;
            for(int v=0;v<scene.VentX.Length;v++)
            {
                double vx=scene.VentX[v],vy=scene.VentY[v],vz=scene.VentZ[v],dx=vx-ex,dz=vz-ez;if(dx*dx+dz*dz>55*55)continue;
                for(int p=0;p<per&&n<px.Length;p++)
                {
                    double age=Frac(time/3.2+(double)p/per+H(v,1)),rise=age*3.2;
                    px[n]=vx+(H(v,p,2)-.5)*.6+WindX*.35*age*3;py[n]=vy+.2+rise;pz[n]=vz+(H(v,p,3)-.5)*.6+WindZ*.35*age*3;ps[n]=.5+age*2.2;pa[n]=.22*Math.Sin(age*Math.PI)*(1-age*.4);
                    double d=Math.Sqrt((px[n]-ex)*(px[n]-ex)+(py[n]-ey)*(py[n]-ey)+(pz[n]-ez)*(pz[n]-ez));if(d<Math.Max(1.5,ps[n]*.6))continue;n++;
                }
            }
            SortFar(n);
            for(int k=0;k<n;k++){int i=order[k];Lit(0x9AA3C8,0,.7,0,out double r,out double g,out double b);r+=.03;g+=.025;b+=.06;Sprite(px[i],py[i],pz[i],ps[i],r,g,b,pa[i],false);Steam++;}
        }
        private void RainFx(NativeCyberFxScene scene,NativeHazardState[] states,double time,double quality,bool low)
        {
            int n=Count(RainFull,quality,low);double span=2*RainRadius;
            // The maglev's pass drags the rain along the Cut near the train.
            double gustHead=double.NaN,gustTail=double.NaN;int gustDir=0;
            if(scene.Maglev>=0&&states!=null&&scene.Maglev<states.Length&&states[scene.Maglev].Phase==NativeHazardPhase.Active){gustHead=states[scene.Maglev].Head;gustTail=states[scene.Maglev].Tail;gustDir=states[scene.Maglev].Direction;}
            for(int i=0;i<n;i++)
            {
                double ox=H(i,1)*span,oz=H(i,2)*span,phase=H(i,3);
                double x=Math.Floor((ex-ox)/span+.5)*span+ox,z=Math.Floor((ez-oz)/span+.5)*span+oz,fall=Frac(time*RainSpeed/RainHeight+phase),y=ey+RainHeight/2-fall*RainHeight;
                double drift=fall*RainHeight/RainSpeed;x+=WindX*drift;z+=WindZ*drift;
                double vx=WindX,vy=-RainSpeed,vz=WindZ;
                if(gustDir!=0&&Math.Abs(z)<4&&y<1){double lo=Math.Min(gustHead,gustTail)-6,hi=Math.Max(gustHead,gustTail)+6;if(x>lo&&x<hi){vx+=gustDir*30;x+=gustDir*1.5;}}
                if(y<scene.Top(x,z)+.05||y<-4)continue;
                double dx=x-ex,dy=y-ey,dz=z-ez,d=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(d<.6||d>RainRadius)continue;
                double a=.2*Math.Min(1,(d-.6)/1.5)*Math.Min(1,(RainRadius-d)/4),len=.55,vl=Math.Sqrt(vx*vx+vy*vy+vz*vz);
                Line(x,y,z,x+vx/vl*len,y+vy/vl*len,z+vz/vl*len,Math.Max(.012,d*.0012),.42*a,.5*a,.68*a,1);Rain++;
            }
        }
        private void SplashFx(NativeCyberFxScene scene,double time,double quality,bool low)
        {
            int n=Count(SplashFull,quality,low);
            for(int i=0;i<n;i++)
            {
                double cyc=time/.42+H(i,4),k=Math.Floor(cyc),age=cyc-k;
                double x=ex+(H(i,(int)k,5)-.5)*20,z=ez+(H(i,(int)k,6)-.5)*20,top=scene.Top(x,z);if(Math.Abs(x)<40&&Math.Abs(z)<3.2&&top<-3.9)top=-4;
                double d=Math.Sqrt((x-ex)*(x-ex)+(top-ey)*(top-ey)+(z-ez)*(z-ez));if(d<.8||d>12)continue;
                double s=.06+age*.32,a=.35*(1-age);Flat(x,top+.025,z,1,0,s,s,.5*a,.58*a,.75*a,1,1,true);Splashes++;
            }
        }
    }
}
