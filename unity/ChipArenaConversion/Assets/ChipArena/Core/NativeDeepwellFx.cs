using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Deepwell's effects for one frame (docs/arenas/DEEPWELL.md) into one <see cref="NativeFxQuads"/> (Runtime draws it as one
    /// effects NativeFxDraw batch) and, while the look's volumetric beams are on, the frame's <see cref="NativeBeamList"/> (null:
    /// billboard shafts):
    /// <list type="bullet">
    /// <item>The sea: the shafts of surface light swaying down through the water, the marine snow, the black smokers' glow and
    /// their columns of bubbles, the jellyfish's halos, the survey drone's lamp and beam, the far habitats' lights.</item>
    /// <item>The station: the glass (a faint tint and sliding reflections on every pane: the panes themselves are not drawn, so
    /// the sea shows through), the moon pools' water (dark, with light coming up through it and glints drifting on it), the warm
    /// lamps' halos, caustic light wandering over the decks.</item>
    /// <item>The hazards (never reduced by the quality or the low tier): the piston's ring beating faster round the wellhead, its
    /// fill and its curtain of light under the ram, the core flaring, the klaxons; the strike's flash, shockwave and dust; a
    /// vent's bubbles and mist gathering over its grate with chevrons marching toward the pool, then the blast's streaks driven
    /// across the strip and the water boiling.</item>
    /// </list>
    /// Pure: the same clocks, camera, quality and map give the same quads. Allocation free after <see cref="Prepare"/>.
    /// </summary>
    public sealed class NativeDeepwellFx
    {
        public const int Capacity=5200;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        public NativeBeamList Beams;
        /// <summary>The last frame's quads by purpose (the checks read them: telegraphs are never reduced).</summary>
        public int Telegraph,Atmosphere,Lights,Glass;
        private bool active;private string prepared;
        private double[] pane=Array.Empty<double>();private int paneCount;private double[] lamp=Array.Empty<double>();private int lampCount;private double[] pylon=Array.Empty<double>();private int pylonCount;
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz,quality;private bool low;
        private static double H(int a,int b=0,int c=0)=>NativeDeepwellLooks.Hash(a,b,c);
        private static double Frac(double v)=>v-Math.Floor(v);
        private static double L(int hex,int shift)=>NativeSceneryBuilder.Channel(hex,shift);
        private const int Teal=0x3FE0D0,Deep=0x2A9CB8,Warm=0xFFC27A,Amber=0xFFA030,Danger=0xFF4A2A,Foam=0xCFF6FF,CoreHot=0xFF8A3A;

        /// <summary>The map's panes, lamps and pylons, once per map (allocates; nothing for another map).</summary>
        public void Prepare(ContentMap map)
        {
            if(map?.id==prepared)return;prepared=map?.id;active=NativeDeepwellLooks.Active(map?.id);var panes=new List<double>();var lamps=new List<double>();var pyl=new List<double>();
            if(!active){pane=lamp=pylon=Array.Empty<double>();paneCount=lampCount=pylonCount=0;return;}
            void Lamp(double x,double y,double z,double size){lamps.Add(x);lamps.Add(y);lamps.Add(z);lamps.Add(size);}
            foreach(var b in map.blocks??Array.Empty<ContentBlock>())
            {
                if(b?.id==null)continue;double x0=b.pos.x-b.size.x/2,x1=b.pos.x+b.size.x/2,z0=b.pos.z-b.size.z/2,z1=b.pos.z+b.size.z/2,y0=b.pos.y-b.size.y/2,y1=b.pos.y+b.size.y/2;
                if(b.id.StartsWith("rig-glass-",StringComparison.Ordinal))
                {
                    // the pane: its middle plane, end to end
                    bool lx=b.size.x>=b.size.z;panes.Add(lx?x0:b.pos.x);panes.Add(lx?b.pos.z:z0);panes.Add(lx?x1:b.pos.x);panes.Add(lx?b.pos.z:z1);panes.Add(y0+.22);panes.Add(y1-.22);
                }
                else if(b.id.StartsWith("rig-pylon-",StringComparison.Ordinal)){pyl.Add(b.pos.x);pyl.Add(b.pos.z);pyl.Add(b.size.x/2);}
                else if(b.id.StartsWith("rig-tunnel-",StringComparison.Ordinal)&&(b.id.IndexOf("-floor-",StringComparison.Ordinal)>=0||b.id.EndsWith("-base",StringComparison.Ordinal)))Lamp(b.pos.x,2.9,b.pos.z,1.5);
                else if(b.id.StartsWith("rig-spine-",StringComparison.Ordinal)&&b.id.EndsWith("-floor",StringComparison.Ordinal)){Lamp(b.pos.x,6.0,0,1.8);Lamp(b.pos.x,2.62,0,1.5);}
                else if(b.id.StartsWith("rig-wing-",StringComparison.Ordinal)&&b.id.EndsWith("-floor",StringComparison.Ordinal)){double sg=b.pos.x>0?1:-1;foreach(double z in new[]{-2.5,2.5})Lamp(b.pos.x+sg*1.6,5.9,z,2.0);foreach(double z in new[]{-4.0,0,4.0})Lamp(b.pos.x+sg*3.6,2.64,z,1.5);}
                else if(b.id=="rig-glass-bay-n"||b.id=="rig-glass-bay-s"){}
            }
            foreach(double zs in new[]{-1.0,1.0}){Lamp(-6,5.6,zs*16.2,2.2);Lamp(6,5.6,zs*16.2,2.2);}
            for(int k=0;k<NativeDeepwellLooks.GalleryLamps.Length;k+=2)Lamp(NativeDeepwellLooks.GalleryLamps[k],2.7,NativeDeepwellLooks.GalleryLamps[k+1],1.5);
            pane=panes.ToArray();paneCount=pane.Length/6;lamp=lamps.ToArray();lampCount=lamp.Length/4;pylon=pyl.ToArray();pylonCount=pylon.Length/3;
        }

        /// <summary>Fills <see cref="Quads"/>: `hazards` and the replicated state at field clock `now`, the render clock for the
        /// sea, the camera and its right and up vectors, effects `quality` (0..1) and the low tier.</summary>
        public void Build(MapHazard[] hazards,int round,NativePhase phase,double fightAt,double now,double render,double camX,double camY,double camZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,double q,bool lowTier)
        {
            Quads.Clear();Telegraph=Atmosphere=Lights=Glass=0;if(!active)return;ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;quality=Math.Max(0,Math.Min(1,q));low=lowTier;
            double clock=NativeFactoryHazards.Clock(phase,fightAt,now);
            Shafts(render);Snow(render);Smokers(render);Creatures(render);Far(render);
            Pools(hazards,round,clock,render);Caustics(render);
            for(int i=0;i<(hazards?.Length??0);i++)
            {
                var h=hazards[i];if(h==null)continue;
                if(h.kind==NativeFactoryHazards.Press)PistonFx(h,round,clock,render);
                else if(h.kind==NativeFactoryHazards.Vent)VentFx(h,round,clock,render,i);
            }
            Lamps(render);Panes();
        }
        private int Count(int full)=>low?Math.Max(1,full/3):Math.Max(1,(int)Math.Round(full*(.35+.65*quality)));

        // ---------------- quad helpers ----------------
        private bool Sprite(double x,double y,double z,double size,double r,double g,double b,double a,bool additive,int mode=1)
        {
            double h=size/2,ax=(rx+ux)*h,ay=(ry+uy)*h,az=(rz+uz)*h,bx=(rx-ux)*h,by=(ry-uy)*h,bz=(rz-uz)*h;
            return Quads.Add(x-ax,y-ay,z-az,x+bx,y+by,z+bz,x+ax,y+ay,z+az,x-bx,y-by,z-bz,r,g,b,a,mode,additive,1);
        }
        private bool Hex(double x,double y,double z,double size,int hex,double a,bool additive=true,double gain=1)=>Sprite(x,y,z,size,L(hex,16)*gain,L(hex,8)*gain,L(hex,0)*gain,a,additive);
        private bool Streak(double ax,double ay,double az,double bx,double by,double bz,double w0,double w1,int hex,double a,double gain=1,int mode=1)
        {
            double dx=bx-ax,dy=by-ay,dz=bz-az,vx=(ax+bx)/2-ex,vy=(ay+by)/2-ey,vz=(az+bz)/2-ez;
            double cx=dy*vz-dz*vy,cy=dz*vx-dx*vz,cz=dx*vy-dy*vx,l=Math.Sqrt(cx*cx+cy*cy+cz*cz);if(l<1e-6)return false;cx/=l;cy/=l;cz/=l;
            return Quads.Add(ax-cx*w0/2,ay-cy*w0/2,az-cz*w0/2,bx-cx*w1/2,by-cy*w1/2,bz-cz*w1/2,bx+cx*w1/2,by+cy*w1/2,bz+cz*w1/2,ax+cx*w0/2,ay+cy*w0/2,az+cz*w0/2,L(hex,16)*gain,L(hex,8)*gain,L(hex,0)*gain,a,mode,true,1);
        }
        /// <summary>A flat quad on a floor: centre, half extents along its own x and z, turned by `yaw`.</summary>
        private bool Flat(double x,double y,double z,double hx,double hz,double yaw,int hex,double a,bool additive,double gain=1,int mode=1)
        {
            double c=Math.Cos(yaw),s=Math.Sin(yaw),ax=c*hx,az=-s*hx,bx=s*hz,bz=c*hz;
            return Quads.Add(x-ax-bx,y,z-az-bz,x+ax-bx,y,z+az-bz,x+ax+bx,y,z+az+bz,x-ax+bx,y,z-az+bz,L(hex,16)*gain,L(hex,8)*gain,L(hex,0)*gain,a,mode,additive,1);
        }
        /// <summary>A vertical sheet from (x0, z0) to (x1, z1), y0..y1.</summary>
        private bool Sheet(double x0,double z0,double x1,double z1,double y0,double y1,int hex,double a,bool additive,double gain=1,int mode=0)
        =>Quads.Add(x0,y0,z0,x1,y0,z1,x1,y1,z1,x0,y1,z0,L(hex,16)*gain,L(hex,8)*gain,L(hex,0)*gain,a,mode,additive,1);
        private void Ring(double x,double y,double z,double r,double w,int n,double phase,int hex,double a,double gain,double gap=0)
        {double seg=Math.PI*r/n*(1-gap);for(int i=0;i<n;i++){double t=phase+i*2*Math.PI/n;Flat(x+Math.Sin(t)*r,y,z+Math.Cos(t)*r,seg,w/2,t,hex,a,true,gain,0);Telegraph++;}}
        private void Beam(double ax,double ay,double az,double bx,double by,double bz,double r0,double r1,int hex,double gain,double alpha,double seed)
        {
            double r=L(hex,16)*gain,g=L(hex,8)*gain,b=L(hex,0)*gain;
            if(Beams!=null){Beams.Add(ax,ay,az,bx,by,bz,r0,r1,r*alpha*NativeBeamList.FromBillboard,g*alpha*NativeBeamList.FromBillboard,b*alpha*NativeBeamList.FromBillboard,.5,seed);return;}
            double dx=bx-ax,dy=by-ay,dz=bz-az,vx=(ax+bx)/2-ex,vy=(ay+by)/2-ey,vz=(az+bz)/2-ez,cx=dy*vz-dz*vy,cy=dz*vx-dx*vz,cz=dx*vy-dy*vx,cl=Math.Sqrt(cx*cx+cy*cy+cz*cz);if(cl<1e-6)return;cx/=cl;cy/=cl;cz/=cl;
            for(int k=0;k<3;k++)
            {
                double t0=k/3.0,t1=(k+1)/3.0,w0=r0+(r1-r0)*t0,w1=r0+(r1-r0)*t1,px0=ax+dx*t0,py0=ay+dy*t0,pz0=az+dz*t0,px1=ax+dx*t1,py1=ay+dy*t1,pz1=az+dz*t1;
                Quads.Add(px0-cx*w0,py0-cy*w0,pz0-cz*w0,px1-cx*w1,py1-cy*w1,pz1-cz*w1,px1+cx*w1,py1+cy*w1,pz1+cz*w1,px0+cx*w0,py0+cy*w0,pz0+cz*w0,r,g,b,alpha*(1-.3*k)*.5,1,true,1);
            }
        }

        // ---------------- the sea ----------------
        // Shafts of surface light: far overhead the sea is day; what reaches the floor sways with the swell
        private void Shafts(double t)
        {
            int n=low?5:10;
            for(int i=0;i<n;i++)
            {
                double a=H(i,51)*2*Math.PI,d=30+H(i,52)*34,x=Math.Sin(a)*d,z=Math.Cos(a)*d,sway=Math.Sin(t*.13+i*1.7)*5,lean=Math.Cos(t*.11+i*2.3)*4,level=.55+.45*Math.Sin(t*.21+i*2.9);
                Beam(x+sway*.2-6,46,z+lean*.2+3,x+sway,NativeDeepwellLooks.Seabed,z+lean,1.4,4.6+2*H(i,53),Deep,.9,.07*level,i*.37);Atmosphere++;
            }
        }
        // Marine snow: pale flecks sinking slowly all round the hull
        private void Snow(double t)
        {
            int n=Count(420);
            for(int i=0;i<n;i++)
            {
                double x=-70+140*H(i,61),z=-60+120*H(i,62);if(NativeDeepwellLooks.Inside(x,z,.8))continue;
                double dx=x-ex,dz=z-ez;if(dx*dx+dz*dz>46*46)continue;
                double fall=.16+.2*H(i,63),y=16-Frac(H(i,64)+t*fall/25)*25;x+=Math.Sin(t*.3+i)*.5;z+=Math.Cos(t*.24+i*1.3)*.5;
                Hex(x,y,z,.05+.07*H(i,65),Foam,.34,true,.6);Atmosphere++;
            }
        }
        // The black smokers: a glow in the throat, bubbles and a dark shimmer climbing
        private void Smokers(double t)
        {
            int per=Count(16);
            for(int s=0;s<NativeDeepwellKit.Smokers;s++)
            {
                NativeDeepwellKit.Smoker(s,out double x,out double z,out double h);double y0=NativeDeepwellLooks.Seabed+h;
                Hex(x,y0+.3,z,2.6+.5*Math.Sin(t*3+s),CoreHot,.5);Lights++;
                for(int i=0;i<per;i++)
                {
                    double u=Frac(H(s*31+i,71)+t*(.07+.05*H(s*31+i,72))),y=y0+u*24,w=.3+u*2.2;
                    Hex(x+Math.Sin(t*1.1+i*2.1+u*9)*w,y,z+Math.Cos(t*.9+i*1.7+u*7)*w,.1+.14*H(s*31+i,73)+u*.1,Foam,.5*(1-u*.7));Atmosphere++;
                }
            }
        }
        // The creatures' light: each jellyfish's halo, the drone's lamp and its beam
        private void Creatures(double t)
        {
            for(int i=0;i<NativeDeepwellLooks.Jellies;i++)
            {
                NativeDeepwellLooks.JellyAt(i,t,out double x,out double y,out double z,out double pulse,out _,out _,out _,out double size,out int tint);
                NativeDeepwellRig.JellyLight(tint,out double r,out double g,out double b);double a=.16+.3*pulse;
                Sprite(x,y,z,size*5.2,r,g,b,a,true);if(!low)Sprite(x,y,z,size*2.2,.6+.4*r,.6+.4*g,.6+.4*b,a*.9,true);Lights++;
            }
            NativeDeepwellLooks.Drone(t,out double dx,out double dy,out double dz,out double head);double fx=Math.Sin(head),fz=Math.Cos(head);
            Hex(dx+fx*.8,dy-.05,dz+fz*.8,1.6,Foam,.8);Beam(dx+fx*.8,dy-.05,dz+fz*.8,dx+fx*19,dy-7,dz+fz*19,.25,3.2,Foam,1,.16,7.7);Lights++;
        }
        // The far habitats' window bands and beacons
        private void Far(double t)
        {
            double sea=NativeDeepwellLooks.Seabed;
            Hex(-78,sea+4.3,52,16,Warm,.34);Hex(84,sea+5.2,-40,19,Warm,.34);Hex(10,sea+3.9,96,14,Warm,.34);
            double blink=Math.Pow(.5+.5*Math.Sin(t*1.9),6);
            Hex(-78,sea+16.4,52,3+6*blink,Danger,.4+.6*blink);Hex(84,sea+19.6,-40,3+6*blink,Danger,.4+.6*blink);Hex(10,sea+14.8,96,3+6*blink,Danger,.4+.6*blink);Lights+=6;
        }

        // ---------------- the station ----------------
        // The moon pools: dark water, light coming up through it, glints drifting on it; it boils while a vent beside it works
        private void Pools(MapHazard[] hazards,int round,double clock,double t)
        {
            double y=NativeDeepwellLooks.Water;
            for(int sz=-1;sz<=1;sz+=2)
            {
                double x0=NativeDeepwellLooks.PoolX0-.1,x1=NativeDeepwellLooks.PoolX1+.1,z0=sz>0?NativeDeepwellLooks.PoolZ0-.1:-NativeDeepwellLooks.PoolZ1-.1,z1=sz>0?NativeDeepwellLooks.PoolZ1+.1:-NativeDeepwellLooks.PoolZ0+.1,cx=(x0+x1)/2,cz=(z0+z1)/2;
                Quads.Add(x0,y,z0,x1,y,z0,x1,y,z1,x0,y,z1,L(0x04202A,16),L(0x04202A,8),L(0x04202A,0),.86,0,false,1);
                Flat(cx,y+.01,cz,4.6,3.2,0,Teal,.2+.05*Math.Sin(t*.7+sz),true,1.2);Lights++;
                int n=low?8:18;
                for(int i=0;i<n;i++)
                {
                    double u=H(i,81+sz),v=H(i,83+sz),px=x0+.5+(x1-x0-1)*Frac(u+t*.011*(i%2==0?1:-1)),pz=z0+.4+(z1-z0-.8)*Frac(v+t*.008),tw=.5+.5*Math.Sin(t*(1.2+u)+i*2.3);
                    Flat(px,y+.02,pz,.5+.5*u,.07+.06*v,t*.1+i,Foam,.2*tw,true,1);Atmosphere++;
                }
            }
        }
        // Caustic light wandering over the decks under the glass
        private void Caustics(double t)
        {
            if(low)return;int n=Count(34);
            for(int i=0;i<n;i++)
            {
                int room=i%5;double x,z,y;
                if(room==0){x=-8+16*H(i,91);z=-8+16*H(i,92);y=.03;}else if(room==1){x=-27+8*H(i,91);z=-8+16*H(i,92);y=3.23;}else if(room==2){x=19+8*H(i,91);z=-8+16*H(i,92);y=3.23;}
                else if(room==3){x=-7+14*H(i,91);z=19+1.6*H(i,92);y=-1.57;}else{x=-7+14*H(i,91);z=-19-1.6*H(i,92);y=-1.57;}
                x+=Math.Sin(t*.17+i)*1.2;z+=Math.Cos(t*.13+i*1.9)*1.2;double w=.5+.5*Math.Sin(t*.9+i*2.7);
                Flat(x,y,z,1.1+.9*H(i,93),.28+.2*H(i,94),t*.07+i,Teal,.05+.07*w,true,1);Atmosphere++;
            }
        }
        // The warm lamps' halos
        private void Lamps(double t)
        {
            for(int i=0;i<lampCount;i++){double x=lamp[i*4],y=lamp[i*4+1],z=lamp[i*4+2],dx=x-ex,dy=y-ey,dz=z-ez;if(dx*dx+dy*dy+dz*dz>70*70)continue;Hex(x,y-.08,z,lamp[i*4+3],Warm,.32);Lights++;}
        }
        // The glass: a faint tint on every pane and, above the low tier, reflections that slide as the eye moves
        private void Panes()
        {
            for(int i=0;i<paneCount;i++)
            {
                double x0=pane[i*6],z0=pane[i*6+1],x1=pane[i*6+2],z1=pane[i*6+3],y0=pane[i*6+4],y1=pane[i*6+5],len=Math.Sqrt((x1-x0)*(x1-x0)+(z1-z0)*(z1-z0));if(len<.2)continue;
                Sheet(x0,z0,x1,z1,y0,y1,0x7FE8F0,.05,false);Glass++;if(low)continue;
                double dx=(x1-x0)/len,dz=(z1-z0)/len,along=(ex-x0)*dx+(ez-z0)*dz;int n=Math.Max(1,(int)(len/5));
                for(int k=0;k<n;k++)
                {
                    // a slanted streak in each bay of the pane: it moves a third as far as the eye does
                    double u=(k+.35)*len/n+Math.Sin(along*.33+k)*.4-Frac(along*.07)*.0,h=y1-y0,w=Math.Min(.7,len/n*.2);if(u<w||u>len-w-h*.22)continue;
                    double ax=x0+dx*u,az=z0+dz*u,bx=ax+dx*h*.22,bz=az+dz*h*.22;
                    Quads.Add(ax-dx*w/2,y0,az-dz*w/2,ax+dx*w/2,y0,az+dz*w/2,bx+dx*w/2,y1,bz+dz*w/2,bx-dx*w/2,y1,bz-dz*w/2,L(Foam,16),L(Foam,8),L(Foam,0),.035,0,true,1);Glass++;
                }
            }
        }

        // ---------------- the hazards ----------------
        private void PistonFx(MapHazard h,int round,double clock,double t)
        {
            var p=NativeDeepwellLooks.PistonAt(h,round,clock);double x=h.b.x,z=h.b.z,half=h.size.x/2,floor=h.b.y-h.size.y/2,core=NativeDeepwellLooks.CoreLevel(p,t);
            // the core under the grate: always alight, flaring with the stroke
            Hex(x,floor+.25,z,3.2+5*core,CoreHot,.3+.6*core,true,1.2);Hex(x,floor+.2,z,1.4+2*core,0xFFE2B0,.5*core);Lights+=2;
            if(p.Warn>0)
            {
                bool down=p.Held||p.Moving;double w=p.Warn,beat=.5+.5*Math.Sin(t*(6+14*w)),level=down?1:.35+.65*beat;int hex=down||w>.66?Danger:Amber;
                // the ring round the wellhead, the footprint's fill and its edge, the curtain under the ram
                Ring(x,floor+.05,z,half*1.42+.25,.3,28,t*(.6+1.6*w),hex,.75*level,1.6,.25);
                Flat(x,floor+.04,z,half,half,0,hex,(.1+.3*w)*level,true,1.2,0);Telegraph++;
                for(int s=-1;s<=1;s+=2){Flat(x,floor+.06,z+s*half,half,.09,0,hex,.8*level,true,1.8,0);Flat(x+s*half,floor+.06,z,.09,half,0,hex,.8*level,true,1.8,0);Telegraph+=2;}
                double bottom=Math.Max(floor+.1,p.Y-h.size.y/2);
                if(bottom>floor+.3){double a=(.05+.1*w)*level;Sheet(x-half,z-half,x+half,z-half,floor,bottom,hex,a,true);Sheet(x-half,z+half,x+half,z+half,floor,bottom,hex,a,true);Sheet(x-half,z-half,x-half,z+half,floor,bottom,hex,a,true);Sheet(x+half,z-half,x+half,z+half,floor,bottom,hex,a,true);Telegraph+=4;}
                // the klaxons' halos on the pylons
                double kl=NativeDeepwellRig.Klaxon(p,t);
                for(int k=0;k<pylonCount;k++){double px=pylon[k*3],pz=pylon[k*3+1],hw=pylon[k*3+2];Hex(px+(px>0?-1:1)*(hw+.1),2.5,pz+(pz>0?-1:1)*(hw+.1),.6+1.8*kl,Danger,.2+.7*kl);Telegraph++;}
            }
            if(p.Since<2.2)
            {
                // the strike: a flash, a shockwave across the deck, dust thrown out and rising
                double s=p.Since,flash=Math.Exp(-s*7);
                if(flash>.02){Hex(x,floor+1,z,9+10*s,0xFFE8C8,.9*flash,true,1.5);Telegraph++;}
                Ring(x,floor+.07,z,half+1+s*11,.5+s*.8,32,0,0xFFD8A8,.7*Math.Exp(-s*2.6),1.3,.1);
                for(int i=0;i<18;i++){double a=i*2*Math.PI/18+H(i,101),d=half+.4+s*(2.2+2*H(i,102)),up=.3+s*(.9+H(i,103))-s*s*.25;Hex(x+Math.Sin(a)*d,floor+up,z+Math.Cos(a)*d,.9+s*1.6,0xB8C4C8,.34*Math.Exp(-s*1.5),false);Telegraph++;}
            }
        }
        private void VentFx(MapHazard h,int round,double clock,double t,int index)
        {
            NativeDeepwellLooks.VentAt(h,round,clock,out double warn,out double blast,out double after);
            double x=h.a.x,y=h.a.y,z=h.a.z,hx=h.size.x/2,hz=h.size.z/2,dir=h.axis.x>=0?1:-1;
            // the grate's standing glow (it is always a little alight, so the place is known)
            Flat(x,y+.05,z,hx-.5,hz-.3,0,Teal,.07+.25*warn+.2*blast,true,1,0);Lights++;
            if(warn>0)
            {
                // bubbles and mist gathering over the grate, chevrons marching toward the pool
                for(int i=0;i<34;i++)
                {
                    double u=Frac(H(index*17+i,111)+t*(.5+.6*H(index*17+i,112))),px=x+(H(index*17+i,113)-.5)*2*(hx-.4),pz=z+(H(index*17+i,114)-.5)*2*(hz-.2);
                    Hex(px+Math.Sin(t*5+i)*.08,y+.1+u*(1+1.6*warn),pz,.1+.14*H(index*17+i,115),Foam,.75*warn*(1-u));Telegraph++;
                }
                double march=Frac(t*(1.2+1.4*warn));
                for(int k=0;k<3;k++)
                {
                    double u=(k+march)/3,cx=x+dir*(-hx+.2+u*(2*hx+.8)),a=(.35+.65*warn)*Math.Sin(Math.PI*u);
                    for(int s=-1;s<=1;s+=2){Flat(cx-dir*.28,y+.06,z+s*(hz*.45),.5,.07,-s*dir*.62,Amber,.9*a,true,1.6,0);Telegraph++;}
                }
                Sheet(x-hx,z-hz,x-hx,z+hz,y,y+.6+1.6*warn,Teal,.05+.07*warn,true);Sheet(x+hx,z-hz,x+hx,z+hz,y,y+.6+1.6*warn,Teal,.05+.07*warn,true);Telegraph+=2;
            }
            if(blast>0)
            {
                // the blast: streaks driven across the strip toward the water, a bright front, the mist blown flat
                for(int i=0;i<42;i++)
                {
                    double u=Frac(H(index*19+i,121)+t*(1.6+1.2*H(index*19+i,122))),px=x+dir*(-hx+u*(2*hx+2.4)),py=y+.2+2.4*H(index*19+i,123),pz=z+(H(index*19+i,124)-.5)*2*hz,len=.8+1.4*H(index*19+i,125);
                    Streak(px,py,pz,px+dir*len,py+.1,pz,.1,.03,Foam,.6*blast*(1-u*.5),1.2);Telegraph++;
                }
                for(int i=0;i<14;i++){double u=Frac(H(index*23+i,131)+t*.9),px=x+dir*(-hx+u*(2*hx+3)),pz=z+(H(index*23+i,132)-.5)*2*hz;Hex(px,y+.5+1.5*H(index*23+i,133),pz,1.2+1.4*u,Foam,.2*blast*(1-u),false);Telegraph++;}
                Flat(x,y+.07,z,hx,hz,0,Danger,.16*blast,true,1.2,0);Telegraph++;
                // the pool beside it boils
                for(int i=0;i<16;i++){double px=x+dir*(hx+.5+2.6*H(index*29+i,141)),pz=z+(H(index*29+i,142)-.5)*2*hz,u=Frac(H(index*29+i,143)+t*2.2);Hex(px,NativeDeepwellLooks.Water+.05+u*.5,pz,.2+.3*u,Foam,.6*blast*(1-u));Telegraph++;}
            }
            else if(after<1.6){for(int i=0;i<10;i++){double px=x+dir*(hx*.4+H(index*23+i,151)*3),pz=z+(H(index*23+i,152)-.5)*2*hz;Hex(px,y+.6+after*.8+H(index*23+i,153),pz,1.6+after,Foam,.14*(1-after/1.6),false);Telegraph++;}}
        }
    }
}
