using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// What the town's atmosphere needs, built once (<see cref="Build"/>): the kit's fires, lamps, floods, beacons and neon, the
    /// sanctuary's bulbs, the skyline's burning towers, a 2 m open-street map over the town (where ground fog may lie and how far
    /// it is to the nearest wall, and which cells are the hub's wilds, where it lies thicker and darker), and the fog tint. No
    /// allocation after Build.
    /// </summary>
    public sealed class NativeCityFxScene
    {
        /// <summary>The grid over the town's blocks (x -122..122, z -122..62 since the city grew, exp-layout-7), 2 m cells.</summary>
        public const int CellsX=122,CellsZ=92;public const float OriginX=-122,OriginZ=-122,Cell=2;
        /// <summary>Per 2 m cell: 0 inside a block, else the distance in cells to the nearest blocked cell (1..4).</summary>
        public readonly byte[] Open=new byte[CellsX*CellsZ];
        /// <summary>Per 2 m cell: 1 in a wilds district of the hub record (thicker, darker fog), else 0.</summary>
        public readonly byte[] Wild=new byte[CellsX*CellsZ];
        public NativeCityGlow[] Fires=Array.Empty<NativeCityGlow>(),Halos=Array.Empty<NativeCityGlow>();public NativeCityBackdropFire[] Far=Array.Empty<NativeCityBackdropFire>();
        public float[] BulbX=Array.Empty<float>(),BulbY=Array.Empty<float>(),BulbZ=Array.Empty<float>(),BulbP=Array.Empty<float>();
        /// <summary>The kit's steaming manholes (x, y, z, phase per source; the city detail pass).</summary>
        public float[] Steam=Array.Empty<float>();
        public float FogR=.1f,FogG=.08f,FogB=.1f;
        public byte OpenAt(double x,double z){int i=(int)Math.Floor((x-OriginX)/Cell),j=(int)Math.Floor((z-OriginZ)/Cell);return i<0||j<0||i>=CellsX||j>=CellsZ?(byte)0:Open[j*CellsX+i];}
        public bool WildAt(double x,double z){int i=(int)Math.Floor((x-OriginX)/Cell),j=(int)Math.Floor((z-OriginZ)/Cell);return i>=0&&j>=0&&i<CellsX&&j<CellsZ&&Wild[j*CellsX+i]!=0;}
        public static NativeCityFxScene Build(ContentBlock[] blocks,NativeExpeditionCityKit kit,NativeCityBackdropFire[] far,MapLook look)=>Build(blocks,kit,far,look,null);
        public static NativeCityFxScene Build(ContentBlock[] blocks,NativeExpeditionCityKit kit,NativeCityBackdropFire[] far,MapLook look,ExpeditionHubLayout hub)
        {
            var s=new NativeCityFxScene{Far=far??Array.Empty<NativeCityBackdropFire>()};
            for(int k=0;k<s.Open.Length;k++)s.Open[k]=4;
            foreach(var b in blocks??Array.Empty<ContentBlock>())
            {
                var stem=NativeExpeditionCityKit.Stem(b?.id);if(stem==null||stem.StartsWith("slab-",StringComparison.Ordinal)||stem.EndsWith("-paving",StringComparison.Ordinal)||b.pos.y-b.size.y/2>2)continue;
                double r=Math.Max(b.size.x,b.size.z)*.5*(b.rotation!=null&&Math.Abs(b.rotation.y)>.01f?1.0:0);
                double x0=b.pos.x-(r>0?r:b.size.x/2),x1=b.pos.x+(r>0?r:b.size.x/2),z0=b.pos.z-(r>0?r:b.size.z/2),z1=b.pos.z+(r>0?r:b.size.z/2);
                int ia=Math.Max(0,(int)Math.Floor((x0-OriginX)/Cell)),ib=Math.Min(CellsX-1,(int)Math.Ceiling((x1-OriginX)/Cell)),ja=Math.Max(0,(int)Math.Floor((z0-OriginZ)/Cell)),jb=Math.Min(CellsZ-1,(int)Math.Ceiling((z1-OriginZ)/Cell));
                for(int j=ja;j<=jb;j++)for(int i=ia;i<=ib;i++){double cx=OriginX+(i+.5)*Cell,cz=OriginZ+(j+.5)*Cell;if(cx>=x0&&cx<=x1&&cz>=z0&&cz<=z1)s.Open[j*CellsX+i]=0;}
            }
            // Distance to the nearest blocked cell, up to 4 (three passes of a 4-neighbour relaxation).
            for(int pass=0;pass<4;pass++)for(int j=0;j<CellsZ;j++)for(int i=0;i<CellsX;i++)
            {
                int k=j*CellsX+i;if(s.Open[k]==0)continue;int m=4;
                if(i>0)m=Math.Min(m,s.Open[k-1]+1);if(i<CellsX-1)m=Math.Min(m,s.Open[k+1]+1);if(j>0)m=Math.Min(m,s.Open[k-CellsX]+1);if(j<CellsZ-1)m=Math.Min(m,s.Open[k+CellsX]+1);s.Open[k]=(byte)Math.Max(1,m);
            }
            if(hub!=null)for(int j=0;j<CellsZ;j++)for(int i=0;i<CellsX;i++)if(hub.InWilds(OriginX+(i+.5)*Cell,OriginZ+(j+.5)*Cell))s.Wild[j*CellsX+i]=1;
            var fires=new List<NativeCityGlow>();var halos=new List<NativeCityGlow>();
            foreach(var g in kit?.Glows??Array.Empty<NativeCityGlow>()){if(g.Kind==NativeExpeditionCityKit.GlowFire||g.Kind==NativeExpeditionCityKit.GlowBrazier||g.Kind==NativeExpeditionCityKit.GlowWindowFire)fires.Add(g);else halos.Add(g);}
            // The look's own lights the kit does not already mark (the gate's warning lamp).
            foreach(var l in look?.lights??Array.Empty<MapLookPractical>())
            {
                if(l?.pos==null||l.flicker=="fire")continue;bool near=false;foreach(var g in halos){double dx=g.X-l.pos.x,dy=g.Y-l.pos.y,dz=g.Z-l.pos.z;if(dx*dx+dy*dy+dz*dz<9){near=true;break;}}if(near)continue;
                halos.Add(new NativeCityGlow{X=l.pos.x,Y=l.pos.y,Z=l.pos.z,R=(float)NativeSceneryBuilder.Channel(l.color,16)*l.intensity,G=(float)NativeSceneryBuilder.Channel(l.color,8)*l.intensity,B=(float)NativeSceneryBuilder.Channel(l.color,0)*l.intensity,Range=l.range,Size=1.2f,Kind=NativeExpeditionCityKit.GlowWarning});
            }
            s.Fires=fires.ToArray();s.Halos=halos.ToArray();
            var bx=new List<float>();var by=new List<float>();var bz=new List<float>();var bp=new List<float>();
            if(kit!=null&&blocks!=null)foreach(var p in kit.Parts){if(p.Drive!=NativeExpeditionCityKit.DriveBulbs||p.Parent<0||p.Parent>=blocks.Length)continue;var b=blocks[p.Parent];bx.Add(b.pos.x+p.X);by.Add(b.pos.y+p.Y);bz.Add(b.pos.z+p.Z);bp.Add(p.Param);}
            s.BulbX=bx.ToArray();s.BulbY=by.ToArray();s.BulbZ=bz.ToArray();s.BulbP=bp.ToArray();s.Steam=kit?.Steam??Array.Empty<float>();
            if(look?.fog!=null){s.FogR=(float)NativeSceneryBuilder.Channel(look.fog.color,16);s.FogG=(float)NativeSceneryBuilder.Channel(look.fog.color,8);s.FogB=(float)NativeSceneryBuilder.Channel(look.fog.color,0);}
            return s;
        }
    }

    /// <summary>
    /// Builds the town's camera-dependent quads for one frame into a <see cref="NativeFxQuads"/> (Runtime draws them as one solid
    /// and one effects batch: two draws):
    ///  - the skyline's burning towers: a flare at each and a leaning smoke column into the dusk; a helicopter circling far out
    ///    with blinking lights and a searchlight sweeping the outskirts (its body in the solid batch);
    ///  - fires (the brazier, the bus, the burning windows): flames, a warm halo, rising embers and smoke;
    ///  - light: halos round the working lamps, floods, beacons, the police bar and neon, warm pools under the lamps and fires,
    ///    the sanctuary's bulbs;
    ///  - ground fog: soft sheets lying in the streets round the eye (none inside blocks, smaller near walls), drifting, and lower,
    ///    denser pockets in the narrow alleys and lanes (the city detail pass); falling ash;
    ///  - the city detail pass: steam breathing out of some manholes, and a soft shaft of light in the haze under each working
    ///    lamp near the eye.
    /// Counts scale with the effects quality and the low tier (NativeBrickfallAtmosphere.Count's rule), except the fires' flames and
    /// the lamps' halos, which mark where light is. Pure: the same clock, camera, light, quality and scene give the same quads;
    /// premultiplied smoke and fog are sorted far to near within their kind. Allocation free.
    /// </summary>
    public sealed class NativeExpeditionCityFx
    {
        public const int Capacity=2800,SortMax=320;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        public int Flames,Embers,Smoke,Halos,Pools,Fog,Ash,Bulbs,Columns,Heli,Steam,Shafts,Pockets;
        public const double FogRadius=40,WindX=.55,WindZ=.3;
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz,hx,hz;private NativeFxLight light;
        private readonly double[] key=new double[SortMax],sx=new double[SortMax],sy=new double[SortMax],sz=new double[SortMax],ss=new double[SortMax],sr=new double[SortMax],sg=new double[SortMax],sb=new double[SortMax],sa=new double[SortMax];
        private readonly int[] order=new int[SortMax],smode=new int[SortMax];private int sorted;

        private static uint Mix(uint x){unchecked{x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x;}}
        private static double H(int a,int b=0,int c=0){unchecked{return Mix(Mix(Mix((uint)a+0x9E3779B9U)^(uint)b*0x85EBCA6BU)+(uint)c*0xC2B2AE35U)/4294967296.0;}}
        private static double Frac(double v)=>v-Math.Floor(v);
        public static int Count(int full,double quality,bool low)=>NativeBrickfallAtmosphere.Count(full,quality,low);

        /// <summary>Fills <see cref="Quads"/> for the presentation clock `time` and the camera (position, right, up).</summary>
        public void Build(NativeCityFxScene scene,double time,double camX,double camY,double camZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,NativeFxLight sun,double quality,bool low)
        {
            Quads.Clear();ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;light=sun;double hl=Math.Sqrt(rx*rx+rz*rz);hx=hl>1e-6?rx/hl:1;hz=hl>1e-6?rz/hl:0;
            Flames=Embers=Smoke=Halos=Pools=Fog=Ash=Bulbs=Columns=Heli=Steam=Shafts=Pockets=0;if(scene==null)return;
            // Premultiplied pieces first, each kind far to near: the far smoke columns, the ground fog, the near smoke; then the
            // additive pieces (order free).
            Begin();ColumnFx(scene,time,quality,low);Flush();
            Begin();FogFx(scene,time,quality,low);PocketFx(scene,time,quality,low);Flush();
            Begin();SmokeFx(scene,time,quality,low);SteamFx(scene,time,quality,low);AshFx(time,quality,low);Flush();
            FireFx(scene,time,quality,low);HaloFx(scene,time);BulbFx(scene,time);HeliFx(time,quality,low);
        }

        // ---------------- primitives ----------------
        private void Sprite(double x,double y,double z,double size,double r,double g,double b,double a,bool additive,int layer=1)
        {
            double h=size/2,ax=rx*h,ay=ry*h,az=rz*h,bx=ux*h,by=uy*h,bz=uz*h;
            Quads.Add(x-ax-bx,y-ay-by,z-az-bz,x+ax-bx,y+ay-by,z+az-bz,x+ax+bx,y+ay+by,z+az+bz,x-ax+bx,y-ay+by,z-az+bz,r,g,b,a,1,additive,layer);
        }
        // An upright billboard (turns about y to the camera): width w, height h, its foot at (x, y, z).
        private void Upright(double x,double y,double z,double w,double h,double r,double g,double b,double a,bool additive)
        {
            double ax=hx*w/2,az=hz*w/2;Quads.Add(x-ax,y,z-az,x+ax,y,z+az,x+ax,y+h,z+az,x-ax,y+h,z-az,r,g,b,a,1,additive,1);
        }
        private void Flat(double x,double y,double z,double radius,double r,double g,double b,double a,bool additive)
        {Quads.Add(x-radius,y,z-radius,x+radius,y,z-radius,x+radius,y,z+radius,x-radius,y,z+radius,r,g,b,a,1,additive,1);}
        private double Dist(double x,double y,double z)=>Math.Sqrt((x-ex)*(x-ex)+(y-ey)*(y-ey)+(z-ez)*(z-ez));
        // The sorted batch: premultiplied sprites (mode 0) or flat sheets (mode 1) queued, then written far to near.
        private void Begin()=>sorted=0;
        private void Queue(double x,double y,double z,double size,double r,double g,double b,double a,int mode)
        {
            if(sorted>=SortMax||!(a>.002))return;int i=sorted++;sx[i]=x;sy[i]=y;sz[i]=z;ss[i]=size;sr[i]=r;sg[i]=g;sb[i]=b;sa[i]=a;smode[i]=mode;key[i]=Dist(x,y,z);order[i]=i;
        }
        private void Flush()
        {
            for(int i=1;i<sorted;i++){int v=order[i];double k=key[v];int j=i-1;while(j>=0&&key[order[j]]<k){order[j+1]=order[j];j--;}order[j+1]=v;}
            for(int n=0;n<sorted;n++){int i=order[n];if(smode[i]==1)Flat(sx[i],sy[i],sz[i],ss[i],sr[i]*sa[i],sg[i]*sa[i],sb[i]*sa[i],sa[i],false);else Sprite(sx[i],sy[i],sz[i],ss[i],sr[i]*sa[i],sg[i]*sa[i],sb[i]*sa[i],sa[i],false);}
            sorted=0;
        }
        // Smoke's colour: the dusk ambient on dark soot, warmer low down over a fire.
        private void SmokeColor(double warm,out double r,out double g,out double b)
        {
            double a0=light!=null?(light.Ambient[0]+light.Ambient[3])*.5:.05,a1=light!=null?(light.Ambient[1]+light.Ambient[4])*.5:.04,a2=light!=null?(light.Ambient[2]+light.Ambient[5])*.5:.05;
            r=.03+a0*.55+warm*.35;g=.028+a1*.55+warm*.12;b=.03+a2*.55+warm*.03;
        }

        // ---------------- the skyline's fires and the helicopter ----------------
        private void ColumnFx(NativeCityFxScene s,double time,double quality,bool low)
        {
            int per=Count(14,quality,low);
            for(int f=0;f<s.Far.Length;f++)
            {
                var c=s.Far[f];
                for(int k=0;k<per;k++)
                {
                    double life=40+H(f,k,1)*20,ph=Frac(time/life+H(f,k,2)),rise=ph*90,size=c.Size*.6+ph*38,a=.42*(1-ph)*Math.Min(1,ph*6);
                    double x=c.X+WindX*rise*.6+Math.Sin(ph*6+f)*3,z=c.Z+WindZ*rise*.6+Math.Cos(ph*5+f)*3;SmokeColor(Math.Max(0,1-ph*4),out double r,out double g,out double b);
                    Queue(x,c.Y+rise,z,size,r,g,b,a,0);Columns++;
                }
            }
        }
        private void HeliFx(double time,double quality,bool low)
        {
            if(low&&quality<.5)return;
            double a=time*.06+.8,x=Math.Sin(a)*165,z=Math.Cos(a)*150,y=58+4*Math.Sin(time*.3);
            // The body against the sky (solid, dark), its blinking lights; the searchlight sweeping the outskirts.
            Sprite(x,y,z,3.2,.02,.02,.025,1,false,0);Sprite(x+hx*2.2,y+.2,z+hz*2.2,1.6,.02,.02,.025,1,false,0);
            if(Frac(time*1.1)<.12)Sprite(x,y-1,z,Math.Max(.8,Dist(x,y,z)*.006),1,.1,.08,1,true);if(Frac(time*1.1+.5)<.06)Sprite(x,y+1.2,z,Math.Max(.7,Dist(x,y,z)*.005),1,1,1,1,true);
            double sweep=time*.35,tx=x*.62+Math.Sin(sweep)*28,tz=z*.62+Math.Cos(sweep*1.3)*28,ty=0;
            // Out past the town's edge (122 m since exp-layout-7; 98 before), by 6 m.
            if(Math.Abs(tx)<128&&Math.Abs(tz)<128){double k=128/Math.Max(Math.Abs(tx),Math.Abs(tz));tx*=k;tz*=k;}
            Beam(x,y-1.5,z,tx,ty,tz,1.2,16,.85,.92,1,.06);Flat(tx,.12,tz,11,.1,.11,.12,1,true);Heli++;
        }
        // An additive beam from a to b, its width growing from w0 to w1, turned about its axis to face the camera.
        private void Beam(double ax,double ay,double az,double bx,double by,double bz,double w0,double w1,double r,double g,double b,double alpha)
        {
            double dx=bx-ax,dy=by-ay,dz=bz-az,l=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(l<1e-3)return;dx/=l;dy/=l;dz/=l;
            double vx=(ax+bx)/2-ex,vy=(ay+by)/2-ey,vz=(az+bz)/2-ez;double px=dy*vz-dz*vy,py=dz*vx-dx*vz,pz=dx*vy-dy*vx,pl=Math.Sqrt(px*px+py*py+pz*pz);if(pl<1e-6)return;px/=pl;py/=pl;pz/=pl;
            Quads.Add(ax-px*w0/2,ay-py*w0/2,az-pz*w0/2,ax+px*w0/2,ay+py*w0/2,az+pz*w0/2,bx+px*w1/2,by+py*w1/2,bz+pz*w1/2,bx-px*w1/2,by-py*w1/2,bz-pz*w1/2,r*alpha,g*alpha,b*alpha,alpha,0,true,1);
        }

        // ---------------- ground fog and ash ----------------
        private void FogFx(NativeCityFxScene s,double time,double quality,bool low)
        {
            int max=Count(130,quality,low);const double C=6;int i0=(int)Math.Floor((ex-FogRadius)/C),i1=(int)Math.Floor((ex+FogRadius)/C),j0=(int)Math.Floor((ez-FogRadius)/C),j1=(int)Math.Floor((ez+FogRadius)/C);
            for(int j=j0;j<=j1;j++)for(int i=i0;i<=i1;i++)
            {
                if(Fog>=max)return;double h=H(i+500,j+500,7);
                double x=(i+.15+.7*H(i,j,8))*C+Math.Sin(time*.05+h*20)*1.6,z=(j+.15+.7*H(i,j,9))*C+Math.Cos(time*.04+h*13)*1.6;byte open=s.OpenAt(x,z);if(open==0)continue;
                // The wilds: every cell carries a sheet (the city's leave a quarter bare), thicker, darker, a second layer higher up.
                bool wild=s.WildAt(x,z);if(!wild&&h<.25)continue;
                double d=Math.Sqrt((x-ex)*(x-ex)+(z-ez)*(z-ez));if(d>FogRadius)continue;double fade=Math.Min(1,Math.Max(0,(d-2.5)/6))*Math.Min(1,(FogRadius-d)/10);
                double radius=Math.Min((wild?4.4:3.2)+3*H(i,j,10),open*NativeCityFxScene.Cell*.9+.6),y=.3+.5*H(i,j,11);
                if(wild){Queue(x,y,z,radius*1.15,s.FogR*.85,s.FogG*.8,s.FogB*.95,.34*fade*(.6+.4*h),1);Fog++;if(Fog<max&&h>.5){Queue(x+1.3,y+1.1,z-.9,radius*.9,s.FogR*.75,s.FogG*.72,s.FogB*.88,.2*fade,1);Fog++;}}
                else{Queue(x,y,z,radius,s.FogR*1.3,s.FogG*1.25,s.FogB*1.3,.2*fade*(.6+.4*h),1);Fog++;}
            }
        }
        private void AshFx(double time,double quality,bool low)
        {
            int n=Count(70,quality,low);const double B=16;
            for(int k=0;k<n;k++)
            {
                double fall=Frac(time*(.05+.03*H(k,1))+H(k,2)),x=ex+(Frac(H(k,3)+time*.012*WindX-ex/B)-.5)*B,z=ez+(Frac(H(k,4)+time*.012*WindZ-ez/B)-.5)*B,y=ey+6-fall*8;
                x+=Math.Sin(time*1.3+k)*.15;Queue(x,y,z,.05+.04*H(k,5),.55,.52,.5,.5*Math.Min(1,fall*5)*Math.Min(1,(1-fall)*5),0);Ash++;
            }
        }

        // ---------------- fires, smoke and embers ----------------
        private void FireFx(NativeCityFxScene s,double time,double quality,bool low)
        {
            int embers=Count(14,quality,low);
            for(int f=0;f<s.Fires.Length;f++)
            {
                var c=s.Fires[f];double d=Dist(c.X,c.Y,c.Z);if(d>110)continue;double k=NativeExpeditionCityKit.Level(NativeExpeditionCityKit.DriveFire,c.Param,time),size=c.Size;
                // The flames: a core and tongues that lick up and sway; the halo.
                for(int t=0;t<5;t++)
                {
                    double ph=Frac(time*(1.6+.4*H(f,t,1))+H(f,t,2)),w=size*(.55-.07*t)*(1-.3*ph),hgt=size*(.8+.5*H(f,t,3))*(.7+.6*Math.Sin(ph*Math.PI));
                    double ox=(H(f,t,4)-.5)*size*.5+Math.Sin(time*3+t)*.05,oz=(H(f,t,5)-.5)*size*.5;bool core=t==0;
                    Upright(c.X+ox,c.Y-.05,c.Z+oz,w,hgt,core?1.0:1.0,core?.62:.38,core?.22:.08,(core?.75:.5)*k,true);Flames++;
                }
                Sprite(c.X,c.Y+size*.4,c.Z,size*3.2,.9*k,.32*k,.06*k,.16,true);Halos++;
                Flat(c.X,.03,c.Z,Math.Min(6,size*3.5),.55*k,.2*k,.05*k,.3,true);Pools++;
                if(d>70)continue;
                for(int e=0;e<embers;e++)
                {
                    double life=1.6+H(f,e,6)*1.6,ph=Frac(time/life+H(f,e,7)),y=c.Y+.3+ph*(2.6+size*1.5),x=c.X+(H(f,e,8)-.5)*size+Math.Sin(time*2.1+e)*.25+WindX*ph*1.2,z=c.Z+(H(f,e,9)-.5)*size+Math.Cos(time*1.7+e)*.25+WindZ*ph*1.2;
                    double a=(1-ph)*Math.Min(1,ph*8);Sprite(x,y,z,Math.Max(.05,d*.0016),1,.55,.15,a,true);Embers++;
                }
            }
        }
        private void SmokeFx(NativeCityFxScene s,double time,double quality,bool low)
        {
            int per=Count(8,quality,low);
            for(int f=0;f<s.Fires.Length;f++)
            {
                var c=s.Fires[f];double d=Dist(c.X,c.Y,c.Z);if(d>110)continue;bool brazier=c.Kind==NativeExpeditionCityKit.GlowBrazier;
                for(int k=0;k<per;k++)
                {
                    double life=7+H(f,k,11)*4,ph=Frac(time/life+H(f,k,12)),rise=ph*(brazier?7:12),size=c.Size*.8+ph*(brazier?3:6),a=(brazier?.18:.34)*(1-ph)*Math.Min(1,ph*5);
                    SmokeColor(Math.Max(0,1-ph*3)*.6,out double r,out double g,out double b);Queue(c.X+WindX*rise*.5+Math.Sin(ph*4+k)*.3,c.Y+1.2+rise,c.Z+WindZ*rise*.5,size,r,g,b,a,0);Smoke++;
                }
            }
        }

        // ---------------- light: halos, pools, bulbs ----------------
        private void HaloFx(NativeCityFxScene s,double time)
        {
            for(int i=0;i<s.Halos.Length;i++)
            {
                var g=s.Halos[i];double d=Dist(g.X,g.Y,g.Z);if(d>150)continue;double k;byte kind=g.Kind;
                switch(kind)
                {
                    case NativeExpeditionCityKit.GlowLampFlicker:k=NativeExpeditionCityKit.Level(NativeExpeditionCityKit.DriveFlicker,g.Param,time);break;
                    case NativeExpeditionCityKit.GlowBeacon:k=NativeExpeditionCityKit.Level(NativeExpeditionCityKit.DriveStrobe,g.Param,time);break;
                    case NativeExpeditionCityKit.GlowPolice:k=1;break;
                    default:k=1;break;
                }
                double r=g.R,gg=g.G,b=g.B,m=Math.Max(r,Math.Max(gg,b));if(m<=0)continue;r/=m;gg/=m;b/=m;
                if(kind==NativeExpeditionCityKit.GlowPolice){bool red=NativeExpeditionCityKit.Level(NativeExpeditionCityKit.DrivePolice,g.Param,time)>.5;r=red?1:.12;gg=red?.08:.3;b=red?.06:1;}
                double size=Math.Max(g.Size*(kind==NativeExpeditionCityKit.GlowNeon?1.3:kind==NativeExpeditionCityKit.GlowFlood?2.4:1.5),d*.012),a=(kind==NativeExpeditionCityKit.GlowNeon?.16:.26)*k;
                Sprite(g.X,g.Y,g.Z,size,r,gg,b,a,true);Halos++;
                // A warm pool on the street under the lamps (not the floods, beacons or signs).
                if((kind==NativeExpeditionCityKit.GlowLamp||kind==NativeExpeditionCityKit.GlowLampFlicker)&&d<70){Flat(g.X,.025,g.Z,g.Range*.42,r*.5*k,gg*.5*k,b*.5*k,.22,true);Pools++;
                    // Its shaft in the haze (the city detail pass): a soft beam from the head to the street, near the eye only.
                    if(d<40&&g.Y>2.5&&Shafts<ShaftMax){double fade=Math.Min(1,(40-d)/12);Beam(g.X,g.Y-.12,g.Z,g.X,.05,g.Z,.22,g.Range*.55,r,gg,b,.045*k*fade);Shafts++;}}
            }
        }
        public const int ShaftMax=24,SteamMax=8,PocketMax=40;
        // ---------------- the city detail pass: steam and fog pockets ----------------
        private void SteamFx(NativeCityFxScene s,double time,double quality,bool low)
        {
            int per=Count(6,quality,low),n=0;var st=s.Steam;
            for(int i=0;i+3<st.Length&&n<SteamMax;i+=4)
            {
                double x=st[i],y=st[i+1],z=st[i+2],p=st[i+3],d=Dist(x,y,z);if(d>50)continue;n++;
                for(int k=0;k<per;k++)
                {
                    double life=3.5+H(i,k,91)*2,ph=Frac(time/life+H(i,k,92)+p),rise=ph*2.6,size=.35+ph*1.5,a=.16*(1-ph)*Math.Min(1,ph*6);
                    SmokeColor(0,out double r,out double g,out double b);Queue(x+WindX*rise*.6+Math.Sin(ph*5+k)*.15,y+.2+rise,z+WindZ*rise*.6,size,r*2.2+.04,g*2.2+.04,b*2.2+.05,a,0);Steam++;
                }
            }
        }
        private void PocketFx(NativeCityFxScene s,double time,double quality,bool low)
        {
            // Narrow streets (2 m cells at most one cell from a wall on a 3 m lattice round the eye): a low, denser sheet.
            int max=Count(PocketMax,quality,low);const double C=3;const double R=28;int i0=(int)Math.Floor((ex-R)/C),i1=(int)Math.Floor((ex+R)/C),j0=(int)Math.Floor((ez-R)/C),j1=(int)Math.Floor((ez+R)/C);
            for(int j=j0;j<=j1;j++)for(int i=i0;i<=i1;i++)
            {
                if(Pockets>=max)return;double h=H(i+900,j+900,17);if(h<.35)continue;
                double x=(i+.2+.6*H(i,j,18))*C+Math.Sin(time*.06+h*17)*.8,z=(j+.2+.6*H(i,j,19))*C+Math.Cos(time*.05+h*11)*.8;byte open=s.OpenAt(x,z);if(open==0||open>1||s.WildAt(x,z))continue;
                double d=Math.Sqrt((x-ex)*(x-ex)+(z-ez)*(z-ez));if(d>R)continue;double fade=Math.Min(1,Math.Max(0,(d-2)/5))*Math.Min(1,(R-d)/8);
                Queue(x,.18+.2*H(i,j,20),z,1.6+.8*H(i,j,21),s.FogR*1.2,s.FogG*1.15,s.FogB*1.25,.24*fade*(.6+.4*h),1);Pockets++;
            }
        }
        private void BulbFx(NativeCityFxScene s,double time)
        {
            for(int i=0;i<s.BulbX.Length;i++){double d=Dist(s.BulbX[i],s.BulbY[i],s.BulbZ[i]);if(d>60)continue;double k=NativeExpeditionCityKit.Level(NativeExpeditionCityKit.DriveBulbs,s.BulbP[i],time);
                Sprite(s.BulbX[i],s.BulbY[i],s.BulbZ[i],Math.Max(.45,d*.008),1,.72,.38,.28*k,true);Bulbs++;}
        }
    }

    /// <summary>A town sound cue: its kind (<see cref="NativeExpeditionCityAmbience"/> constants), cycle, presentation time and place.</summary>
    public struct NativeCityCue {public int Kind,Cycle;public double Time;public V3 At;}
    /// <summary>
    /// The town's ambience (MapLooks.json `ambience` "expedition-city"), mapped onto the existing audio keys until the owner
    /// supplies the city's own beds (no new audio): distant rifle shots and machine-gun bursts somewhere out in the dead city,
    /// a far explosion now and then (all heard from 150-230 m out, beyond the town), and the brazier's and the bus fire's crackle
    /// heard from them. Each emitter repeats at its period with a hashed jitter and a hashed bearing; gains are low (it sits under
    /// the fight). Pure: the cues due in a clock span are a function of that span alone, so host, guests and the killcam hear
    /// the same schedule. Allocation free.
    /// </summary>
    public static class NativeExpeditionCityAmbience
    {
        public const string Id=NativeExpeditionCityKit.MapId;
        public const int Shot=0,Burst=1,Boom=2,Crackle=3;
        private static readonly int[] Kind={Shot,Shot,Burst,Boom,Crackle,Crackle};
        private static readonly double[] Period={9.5,13.7,23,41,1.35,1.7},Jitter={5,6,9,14,.5,.6};
        private static readonly double[] FixedX={0,0,0,0,0,22.6},FixedY={0,0,0,0,1.2,1.6},FixedZ={0,0,0,0,0,-3.9};
        public static int Emitters=>Kind.Length;
        private static uint Mix(uint x){unchecked{x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x;}}
        private static double H(int a,int b,int c){unchecked{return Mix(Mix(Mix((uint)a+0x9E3779B9U)^(uint)b*0x85EBCA6BU)+(uint)c*0xC2B2AE35U)/4294967296.0;}}
        /// <summary>The cues due in (from, to] of the presentation clock, into `into` (at most its length); a burst is four cues.</summary>
        public static int Between(double from,double to,NativeCityCue[] into)
        {
            int count=0;if(into==null||!(to>from)||double.IsInfinity(to)||to-from>5)return 0;
            for(int e=0;e<Kind.Length;e++)
            {
                double p=Period[e],phase=H(e,0,81)*p;
                for(double n=Math.Floor((from-phase)/p)-1;n<=Math.Floor((to-phase)/p)+1;n++)
                {
                    double at=phase+n*p+Jitter[e]*(H(e,(int)n,82)-.5);V3 where;
                    if(Kind[e]==Crackle)where=new V3((float)FixedX[e],(float)FixedY[e],(float)FixedZ[e]);
                    else{double a=H(e,(int)n,83)*Math.PI*2,r=150+80*H(e,(int)n,84);where=new V3((float)(Math.Sin(a)*r),(float)(4+20*H(e,(int)n,85)),(float)(Math.Cos(a)*r));}
                    int shots=Kind[e]==Burst?4:1;
                    for(int k=0;k<shots;k++){double t=at+k*.11;if(t>from&&t<=to&&count<into.Length)into[count++]=new NativeCityCue{Kind=Kind[e],Cycle=(int)n,Time=t,At=where};}
                }
            }
            return count;
        }
    }
}
