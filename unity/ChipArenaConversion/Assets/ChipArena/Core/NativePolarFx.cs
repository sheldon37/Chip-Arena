using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Polaris Station's camera-facing effects for one frame (docs/arenas/POLAR.md) into one <see cref="NativeFxQuads"/> (Runtime
    /// draws it as one effects NativeFxDraw batch) and, while the look's volumetric beams are on, the frame's
    /// <see cref="NativeBeamList"/> (null: billboard shafts):
    /// <list type="bullet">
    /// <item>The sky: the aurora's four curtains (green at the foot, violet above, a bright hem; folds and pulses travelling along
    /// them all match long, NativePolarLooks.AuroraRay), its light lying on the sea and the thin ice.</item>
    /// <item>The air: snow falling round the eye, spindrift streaming over the ice, frost smoke over the open water, the cabins'
    /// stove smoke, the icebreaker's funnel steam, the searchlight's shaft and its pool of light on the ice.</item>
    /// <item>The lights: warm halos at the doors and windows, the masts', pedestal's and dish's red beacons, the balloon's sonde.</item>
    /// <item>The hazards (never reduced by the quality or the low tier): a vent's wisps, its amber ring closing and hissing jets
    /// through the warning, the white roaring column and its hot ring through the burst; a sheet of thin ice cracking (the seam
    /// splitting, cracks running to its edges, a red beating border), going (spray along its edges, a splash) and gone (rings on
    /// the open water).</item>
    /// </list>
    /// Pure: the same clocks, camera, quality and map give the same quads. Allocation free after <see cref="Prepare"/>.
    /// </summary>
    public sealed class NativePolarFx
    {
        public const int Capacity=5200;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        public NativeBeamList Beams;
        public int Telegraph,Atmosphere,Lights,Aurora;
        private bool active;private string prepared;
        private double[] lamp=Array.Empty<double>();private int lampCount;private double[] smoke=Array.Empty<double>();private int smokeCount;private double[] ice=Array.Empty<double>();private int iceCount;
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz,quality;private bool low;
        private static double H(int a,int b=0,int c=0)=>NativePolarLooks.Hash(a,b,c);
        private static double Frac(double v)=>v-Math.Floor(v);
        private static double L(int hex,int shift)=>NativeSceneryBuilder.Channel(hex,shift);
        public const int AuroraGreen=0x38FFA0,AuroraTeal=0x30E8D8,AuroraViolet=0x9A50FF,AuroraRose=0xFF4FC0,Warm=0xFFC88A,Hot=0xFF8A30,Ice=0x9FE8FF;

        /// <summary>The map's fixed lamps and smoke stacks, once per map (allocates; nothing for another map).</summary>
        public void Prepare(ContentMap map)
        {
            if(map?.id==prepared)return;prepared=map?.id;active=NativePolarLooks.Active(map?.id);var lamps=new List<double>();var stacks=new List<double>();var crystals=new List<double>();if(!active){lamp=smoke=ice=Array.Empty<double>();lampCount=smokeCount=iceCount=0;return;}
            void Lamp(double x,double y,double z,int hex,double size,double blink=0){lamps.Add(x);lamps.Add(y);lamps.Add(z);lamps.Add(hex);lamps.Add(size);lamps.Add(blink);}
            foreach(var b in map.blocks??Array.Empty<ContentBlock>())
            {
                if(b?.id==null)continue;double top=b.pos.y+b.size.y/2,x0=b.pos.x-b.size.x/2,x1=b.pos.x+b.size.x/2,z0=b.pos.z-b.size.z/2,z1=b.pos.z+b.size.z/2;bool alongZ=b.size.x<b.size.z;
                if(b.id.EndsWith("-roof",StringComparison.Ordinal)&&!b.id.StartsWith("polar-ship",StringComparison.Ordinal))
                {double px=b.pos.x+(b.pos.x<0?1:-1)*(b.size.x/2-1.2),pz=b.pos.z+b.size.z/2-1.4;stacks.Add(px);stacks.Add(top+.9);stacks.Add(pz);stacks.Add(.5);}
                else if(b.id.StartsWith("polar-spire-",StringComparison.Ordinal)||b.id.StartsWith("polar-floe-",StringComparison.Ordinal)){crystals.Add(b.pos.x);crystals.Add(b.pos.y);crystals.Add(b.pos.z);crystals.Add(b.size.x/2);crystals.Add(b.size.y/2);crystals.Add(b.size.z/2);}
                else if(b.id.IndexOf("-lintel",StringComparison.Ordinal)>=0)
                {double ox=alongZ?(b.pos.x-(b.pos.x<0?-26.8:26.8)>0?x1+.12:x0-.12):b.pos.x,oz=alongZ?b.pos.z:(b.pos.z-(b.pos.z>10?17.6:b.pos.z<-10?-18:0)>0?z1+.12:z0-.12);Lamp(ox,b.pos.y-.05,oz,Warm,1.5);}
                else if(b.id.StartsWith("polar-mast-",StringComparison.Ordinal)||b.id=="polar-ship-mast"||b.id=="polar-obs-beacon")Lamp(b.pos.x,top+.2,b.pos.z,0xFF3020,2.0,1.6+H(1,(int)(b.pos.x*3))*1.2);
                else if(b.id=="polar-obs-pedestal"){foreach(int s in new[]{-1,1}){Lamp(b.pos.x+s*(b.size.x/2+.1),top-1,b.pos.z,0xFF3020,1.3,2.2);Lamp(b.pos.x,top-1,b.pos.z+s*(b.size.z/2+.1),0xFF3020,1.3,2.2);}}
                else if(b.id=="polar-obs-terrace")for(int k=-3;k<=3;k++)Lamp(k*2.4,top-.3,z1+.1,Warm,.9);
                else if(b.id=="polar-ship-bridge")for(int k=0;k<6;k++)Lamp(x0+b.size.x/7*(k+1),b.pos.y-b.size.y/2+6.6,z0-.12,Warm,1.7);
                else if(b.id=="polar-ship-house"){Lamp(-1.6,top-1,z0-.12,Warm,1.1);Lamp(1.6,top-1,z0-.12,Warm,1.1);}
                else if(b.id.StartsWith("polar-snowcat-",StringComparison.Ordinal)&&!b.id.EndsWith("-blade",StringComparison.Ordinal)){Lamp(x0+.45,1.08,z1+.1,0xFFF0D0,1.2);Lamp(x1-.45,1.08,z1+.1,0xFFF0D0,1.2);}
            }
            // the icebreaker past the bound: her masthead and side lights, the funnel's stack
            Lamp(0,34.4,31.44,0xFF3020,3.2,2.6);Lamp(-3.75,29.75,31.44,0xFF3020,1.6);Lamp(3.75,29.75,31.44,0x30FF60,1.6);
            stacks.Add(0);stacks.Add(26);stacks.Add(40.04);stacks.Add(2.6);
            lamp=lamps.ToArray();lampCount=lamp.Length/6;smoke=stacks.ToArray();smokeCount=smoke.Length/4;ice=crystals.ToArray();iceCount=ice.Length/6;
        }

        /// <summary>Fills <see cref="Quads"/>: the map's hazards and each segment's replicated state at field clock `now`, the camera
        /// and its right and up vectors, effects `quality` (0..1) and the low tier.</summary>
        public void Build(ContentMap map,int[] segState,float[] segT0,int round,NativePhase phase,double fightAt,double now,double camX,double camY,double camZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,double q,bool lowTier)
        {
            Quads.Clear();Telegraph=Atmosphere=Lights=Aurora=0;if(!active||map==null)return;ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;quality=Math.Max(0,Math.Min(1,q));low=lowTier;
            double clock=NativeFactoryHazards.Clock(phase,fightAt,now),t=now;
            Curtains(t);SeaLight(t);Searchlight(t);
            for(int s=0;s<(map.segments?.Length??0);s++)
            {
                var d=map.segments[s];int st=segState!=null&&s<segState.Length?segState[s]:0;double t0=segT0!=null&&s<segT0.Length?segT0[s]:0;
                NativePolarLooks.SheetAt(d,st,t0,now,out double crack,out double sink,out bool open,out double since);Sheet(d,s,crack,sink,open,since,t);
            }
            for(int i=0;i<(map.hazards?.Length??0);i++){var h=map.hazards[i];if(h?.kind==NativeFactoryHazards.Vent)VentFx(h,round,clock,t,i);}
            Smoke(t);Spindrift(t);Snow(t);Glints(t);LampHalos(t);Balloon(t);
        }
        private int Count(int full)=>low?Math.Max(1,full/3):Math.Max(1,(int)Math.Round(full*(.35+.65*quality)));

        // ---------------- quad helpers ----------------
        private bool Sprite(double x,double y,double z,double size,double r,double g,double b,double a,bool additive,int mode=1)
        {
            double h=size/2,ax=(rx+ux)*h,ay=(ry+uy)*h,az=(rz+uz)*h,bx=(rx-ux)*h,by=(ry-uy)*h,bz=(rz-uz)*h;
            return Quads.Add(x-ax,y-ay,z-az,x+bx,y+by,z+bz,x+ax,y+ay,z+az,x-bx,y-by,z-bz,r,g,b,a,mode,additive,1);
        }
        private bool Hex(double x,double y,double z,double size,int hex,double a,bool additive,double gain=1)=>Sprite(x,y,z,size,L(hex,16)*gain,L(hex,8)*gain,L(hex,0)*gain,a,additive);
        private bool Streak(double ax,double ay,double az,double bx,double by,double bz,double w0,double w1,double r,double g,double b,double a,bool additive,int mode=1)
        {
            double dx=bx-ax,dy=by-ay,dz=bz-az,vx=(ax+bx)/2-ex,vy=(ay+by)/2-ey,vz=(az+bz)/2-ez;
            double cx=dy*vz-dz*vy,cy=dz*vx-dx*vz,cz=dx*vy-dy*vx,l=Math.Sqrt(cx*cx+cy*cy+cz*cz);if(l<1e-6)return false;cx/=l;cy/=l;cz/=l;
            return Quads.Add(ax-cx*w0/2,ay-cy*w0/2,az-cz*w0/2,bx-cx*w1/2,by-cy*w1/2,bz-cz*w1/2,bx+cx*w1/2,by+cy*w1/2,bz+cz*w1/2,ax+cx*w0/2,ay+cy*w0/2,az+cz*w0/2,r,g,b,a,mode,additive,1);
        }
        private bool HexStreak(double ax,double ay,double az,double bx,double by,double bz,double w0,double w1,int hex,double a,double gain=1,bool additive=true)=>Streak(ax,ay,az,bx,by,bz,w0,w1,L(hex,16)*gain,L(hex,8)*gain,L(hex,0)*gain,a,additive);
        private bool Flat(double x,double y,double z,double hx,double hz,double yaw,double r,double g,double b,double a,bool additive,int mode=1)
        {
            double c=Math.Cos(yaw),s=Math.Sin(yaw),ax=c*hx,az=-s*hx,bx=s*hz,bz=c*hz;
            return Quads.Add(x-ax-bx,y,z-az-bz,x+ax-bx,y,z+az-bz,x+ax+bx,y,z+az+bz,x-ax+bx,y,z-az+bz,r,g,b,a,mode,additive,1);
        }
        private bool FlatHex(double x,double y,double z,double hx,double hz,double yaw,int hex,double a,bool additive,double gain=1,int mode=1)=>Flat(x,y,z,hx,hz,yaw,L(hex,16)*gain,L(hex,8)*gain,L(hex,0)*gain,a,additive,mode);
        /// <summary>A flat ring of `n` segments at height y, radius `r`, each `w` wide, turned by `phase`.</summary>
        private void Ring(double x,double y,double z,double r,double w,int n,double phase,int hex,double a,double gain,double gap=0)
        {
            double seg=Math.PI*r/n*(1-gap);for(int i=0;i<n;i++){double t=phase+i*2*Math.PI/n;FlatHex(x+Math.Sin(t)*r,y,z+Math.Cos(t)*r,seg,w/2,t,hex,a,true,gain,0);Telegraph++;}
        }

        // ---------------- the aurora ----------------
        private void Curtains(double t)
        {
            for(int k=0;k<NativePolarLooks.Curtains;k++)
            {
                int rays=Count(64-k*8);double px=0,py=0,pz=0,pb=0;
                for(int i=0;i<rays;i++)
                {
                    double f=(i+.5)/rays;NativePolarLooks.AuroraRay(k,f,t,out double x,out double y,out double z,out double h,out double b,out double lean);
                    double r=Math.Sqrt(x*x+z*z),w=r*(2.5-k*.28)/rays*2.3,tx=-z/r*lean,tz=x/r*lean;
                    if(b>.04)
                    {
                        // the ray: green from its foot, teal through its middle, violet to its top, a rose fringe on the brightest
                        Streak(x,y,z,x+tx*.45,y+h*.5,z+tz*.45,w,w*.95,L(AuroraGreen,16)*b,L(AuroraGreen,8)*b,L(AuroraGreen,0)*b,.5,true);
                        Streak(x+tx*.3,y+h*.32,z+tz*.3,x+tx*.7,y+h*.72,z+tz*.7,w*.95,w*.85,L(AuroraTeal,16)*b*.7,L(AuroraTeal,8)*b*.7,L(AuroraTeal,0)*b*.7,.34,true);
                        Streak(x+tx*.55,y+h*.55,z+tz*.55,x+tx,y+h,z+tz,w*.9,w*.6,L(AuroraViolet,16)*b,L(AuroraViolet,8)*b,L(AuroraViolet,0)*b,.4,true);Aurora+=3;
                        if(b>.6&&!low){Streak(x+tx*.85,y+h*.86,z+tz*.85,x+tx*1.15,y+h*1.16,z+tz*1.15,w*.6,w*.3,L(AuroraRose,16)*(b-.6),L(AuroraRose,8)*(b-.6),L(AuroraRose,0)*(b-.6),.5,true);Aurora++;}
                    }
                    // the hem: a bright line along the curtain's foot, ray to ray
                    if(i>0&&(b>.04||pb>.04)){double hb=(b+pb)/2;Streak(px,py,pz,x,y,z,5+3*hb,5+3*hb,.75*hb,1.0*hb,.85*hb,.6,true);Aurora++;}
                    px=x;py=y;pz=z;pb=b;
                }
            }
        }
        // The aurora's light lying on the sea south of the shelf (long shimmering streaks toward the horizon) and a sheen on the thin ice.
        private void SeaLight(double t)
        {
            int n=Count(26);double sea=NativePolarLooks.Sea+.03;
            for(int i=0;i<n;i++)
            {
                double x=(H(71,i)-.5)*180,z=NativePolarLooks.ShelfZ-8-H(71,i,1)*150,len=10+H(71,i,2)*26,sh=.5+.5*Math.Sin(t*(.5+H(71,i,3))+i*1.9);
                int hex=H(71,i,4)<.7?AuroraGreen:AuroraViolet;Flat(x+Math.Sin(t*.2+i)*1.5,sea,z,1.2+H(71,i,5)*2.2,len,(H(71,i,6)-.5)*.3,L(hex,16)*.5,L(hex,8)*.5,L(hex,0)*.5,.10+.14*sh,true);Atmosphere++;
            }
        }
        private void Searchlight(double t)
        {
            NativePolarLooks.Searchlight(t,out double x,out double y,out double z);double lx=NativePolarLooks.LampX,ly=NativePolarLooks.LampY,lz=NativePolarLooks.LampZ;
            double dx=x-lx,dy=y-ly,dz=z-lz,l=Math.Sqrt(dx*dx+dy*dy+dz*dz);dx/=l;dy/=l;dz/=l;const int hex=0xE4F2FF;
            if(Beams!=null)Beams.Add(lx+dx*.5,ly+dy*.5,lz+dz*.5,x,y,z,.3,2.6,L(hex,16)*.55,L(hex,8)*.55,L(hex,0)*.55,.6,3.1);
            else for(int k=0;k<4;k++){double a=k/4.0,b=(k+1)/4.0;HexStreak(lx+dx*l*a,ly+dy*l*a,lz+dz*l*a,lx+dx*l*b,ly+dy*l*b,lz+dz*l*b,.6+5*a,.6+5*b,hex,.10*(1-.6*a));}
            Hex(lx+dx*.5,ly+dy*.5,lz+dz*.5,2.4,hex,.9,true,2.2);FlatHex(x,y+.02,z,3.4,3.0,Math.Atan2(dx,dz),hex,.16,true,1.2);Lights+=2;
        }

        // ---------------- the thin ice ----------------
        private void Sheet(StructureDefinition d,int s,double crack,double sink,bool open,double since,double t)
        {
            double cx=d.crush.center.x,cz=d.crush.center.z,hx=d.crush.size.x/2,hz=d.crush.size.z/2,sea=NativePolarLooks.Sea,y=.03-sink*3.2;
            if(!open)
            {
                // whole or cracking: the seam's cold line (always, faint: it reads as the fracture), the aurora's sheen
                FlatHex(cx,.03,cz,.07+.1*crack,hz,0,Ice,.22+.6*crack,true,1+1.6*crack,0);
                Flat(cx+Math.Sin(t*.13+s)*.6,.028,cz,hx*.8,hz*.8,0,L(AuroraGreen,16)*.3,L(AuroraGreen,8)*.3,L(AuroraGreen,0)*.3,.05+.03*Math.Sin(t*.4+s),true);Atmosphere+=2;
            }
            if(crack>0&&sink<1)
            {
                // cracks running from the seam to the edges, each reaching further as the sheet goes; a red border beating
                for(int i=0;i<14;i++)
                {
                    double z0=cz+(H(s,i,11)*2-1)*hz*.9,side=i%2==0?-1:1,reach=Math.Min(1,crack*1.25)*(.55+.45*H(s,i,12)),z1=z0+(H(s,i,13)-.5)*1.8,xm=side*hx*reach*.5,zm=z0+(H(s,i,14)-.5)*.9;
                    double x1=side*hx*reach;Seg(cx,y,z0,cx+xm,y,zm,.07,Ice,.9,2.2);Seg(cx+xm,y,zm,cx+x1,y,z1,.05,Ice,.8,2.0);Telegraph+=2;
                }
                double beat=.55+.45*Math.Sin(t*(9+8*crack));int n=6;
                for(int i=0;i<n;i++)
                {
                    double u=(i+.5)/n;FlatHex(cx-hx+.1,.035,cz-hz+2*hz*u,.09,hz/n*.7,0,0xFF4A30,.75*beat,true,1.8,0);FlatHex(cx+hx-.1,.035,cz-hz+2*hz*u,.09,hz/n*.7,0,0xFF4A30,.75*beat,true,1.8,0);
                    FlatHex(cx-hx+2*hx*u,.035,cz-hz+.1,hx/n*.7,.09,0,0xFF4A30,.75*beat,true,1.8,0);FlatHex(cx-hx+2*hx*u,.035,cz+hz-.1,hx/n*.7,.09,0,0xFF4A30,.75*beat,true,1.8,0);Telegraph+=4;
                }
            }
            if(!open)return;
            // going: spray thrown up along the edges and a splash where it meets the water; gone: rings spreading, frost smoke
            if(since<2.4)
            {
                double u=since/2.4,up=Math.Sin(Math.Min(1,since/.9)*Math.PI);
                for(int i=0;i<18;i++)
                {
                    double a=H(s,i,21)*2*Math.PI,ox=Math.Cos(a)*hx*(.4+.6*H(s,i,22)),oz=Math.Sin(a)*hz*(.4+.6*H(s,i,22)),rise=(1.2+H(s,i,23)*2.6)*up-since*since*.5;
                    Sprite(cx+ox*(1+.3*u),Math.Max(sea,sea+.4+rise),cz+oz*(1+.3*u),.5+H(s,i,24)*.9+u,.86,.94,1,.34*(1-u),false);Telegraph++;
                }
                Hex(cx,sea+.5,cz,5+6*u,0xDFF4FF,.3*(1-u),true,1.3);
            }
            for(int i=0;i<3;i++){double u=Frac(since*.22+i/3.0);Ring(cx,sea+.03,cz,Math.Min(hx,hz)*(.25+.7*u),.07,12,i,0xBFE8FF,.3*(1-u),1.1,.25);}
            for(int i=0;i<Count(6);i++)
            {double u=Frac(t*.09+H(s,i,31));Sprite(cx+(H(s,i,32)*2-1)*hx*.8+u*1.2,sea+.3+u*1.6,cz+(H(s,i,33)*2-1)*hz*.8,1.2+u*1.6,.8,.88,.95,.11*Math.Sin(u*Math.PI),false);Atmosphere++;}
        }
        private void Seg(double x0,double y0,double z0,double x1,double y1,double z1,double w,int hex,double a,double gain)
        {double dx=x1-x0,dz=z1-z0,l=Math.Sqrt(dx*dx+dz*dz);if(l<1e-4)return;FlatHex((x0+x1)/2,y0,(z0+z1)/2,w,l/2,Math.Atan2(dx,dz),hex,a,true,gain,0);}

        // ---------------- the steam vents ----------------
        private void VentFx(MapHazard h,int round,double clock,double t,int index)
        {
            NativePolarLooks.VentAt(h,round,clock,out double warn,out double burst);double x=h.a.x,y=h.a.y,z=h.a.z,r=h.size.x/2,top=h.size.y;
            // idle: a wisp off the grate now and then
            if(warn<=0&&burst<=0){for(int i=0;i<2;i++){double u=Frac(t*.33+H(index,i,41));Sprite(x+(H(index,i,42)-.5)*.7,y+.2+u*1.4,z+(H(index,i,43)-.5)*.7,.5+u*.7,.86,.9,.95,.12*Math.Sin(u*Math.PI),false);Atmosphere++;}return;}
            if(warn>0)
            {
                // the warning: an amber ring closing on the grate, beating faster; thin jets hissing up, taller as it nears
                double beat=.6+.4*Math.Sin(t*(8+10*warn));Ring(x,y+.04,z,r*(1.75-.75*warn),.12,16,t*1.4,Hot,.9*beat,1.6+warn,.3);Ring(x,y+.04,z,r+.12,.08,16,0,Hot,.5+.4*warn,1.4,.1);
                FlatHex(x,y+.035,z,r,r,0,Hot,.18+.3*warn*beat,true,1.2);
                for(int i=0;i<8;i++){double u=Frac(t*(1.6+warn)+i/8.0),hgt=(.5+1.6*warn)*u;Sprite(x+(H(index,i,44)-.5)*r*1.4,y+.1+hgt,z+(H(index,i,45)-.5)*r*1.4,.3+.5*u,.95,.95,.98,.3*(1-u)*(.4+.6*warn),false);Telegraph++;}
                Hex(x,y+.5,z,2.2+1.4*warn,Hot,.25*warn*beat,true,1.4);return;
            }
            // the burst: the column roaring to its full height, white at its heart, its hot ring on the ice
            Ring(x,y+.04,z,r+.12,.14,16,0,0xFFE8C0,.95,2.4,.1);FlatHex(x,y+.035,z,r,r,0,0xFFD8A0,.5*burst,true,1.8);
            for(int i=0;i<34;i++)
            {
                double u=Frac(t*2.2+H(index,i,46)),w=r*(.55+.6*u);double px=x+(H(index,i,47)-.5)*w*2,pz=z+(H(index,i,48)-.5)*w*2,py=y+.15+u*(top+.6);
                Sprite(px,py,pz,.9+1.3*u,.96,.97,1,(.42-.3*u)*burst,false);Telegraph++;
            }
            for(int i=0;i<6;i++){double u=Frac(t*3.1+i/6.0);HexStreak(x+(H(index,i,49)-.5)*r,y+.1+u*top*.7,z+(H(index,i,50)-.5)*r,x+(H(index,i,49)-.5)*r*1.2,y+.1+u*top*.7+1.1,z+(H(index,i,50)-.5)*r*1.2,.3,.1,0xFFFFFF,.5*burst*(1-u),1.5);Telegraph++;}
            Hex(x,y+1.3,z,4.4,0xFFE0B8,.3*burst,true,1.5);Hex(x,y+top,z,3.4,0xFFFFFF,.14*burst,true,1.2);Lights+=2;
        }

        // ---------------- the air ----------------
        private void Smoke(double t)
        {
            for(int k=0;k<smokeCount;k++)
            {
                double x=smoke[k*4],y=smoke[k*4+1],z=smoke[k*4+2],w=smoke[k*4+3];int n=Count(w>1?16:7);
                for(int i=0;i<n;i++)
                {
                    double u=Frac(t*(w>1?.045:.09)+(double)i/n+H(k,i,51)*.05),rise=u*(w>1?26:7),drift=u*u*(w>1?22:5);
                    Sprite(x+drift+Math.Sin(t*.3+i)*w*.3,y+rise,z+drift*.28,w*(1+3.2*u),.8,.86,.94,(w>1?.2:.16)*Math.Sin(Math.Min(1,u*1.15)*Math.PI),false);Atmosphere++;
                }
            }
        }
        private void Spindrift(double t)
        {
            int n=Count(80);const double box=20;double wx=.95,wz=.31;
            for(int i=0;i<n;i++)
            {
                double fx=Frac(H(81,i)+t*.1*(.7+.6*H(81,i,3))),fz=Frac(H(81,i,1)+t*.03);double x=ex+(fx-.5)*2*box,z=ez+(fz-.5)*2*box;if(z<NativePolarLooks.ShelfZ)continue;
                double y=.05+H(81,i,2)*.5,len=1.2+H(81,i,4)*2.6;Streak(x,y,z,x+wx*len,y+.04,z+wz*len,.09,.02,.85,.92,1,.2*Math.Sin(fx*Math.PI),false);Atmosphere++;
            }
        }
        // Snow falling round the eye (the look's own weather draws the storm above the low tier; this keeps flakes on every tier).
        private void Snow(double t)
        {
            int n=Count(110);const double box=14,tall=12;
            for(int i=0;i<n;i++)
            {
                double fy=Frac(H(91,i)-t*(.09+.05*H(91,i,3))),fx=Frac(H(91,i,1)+t*.035+Math.Sin(t*.4+i)*.01),fz=Frac(H(91,i,2)+t*.012);
                double x=ex+(fx-.5)*2*box,y=ey-3+fy*tall,z=ez+(fz-.5)*2*box;if(y<.02)continue;Sprite(x,y,z,.05+.05*H(91,i,4),.95,.97,1,.75*Math.Sin(fy*Math.PI),false);Atmosphere++;
            }
        }
        // The ice's glints: a spark of the aurora's light catching a crystal's edge or a floe's face, each for a moment.
        private void Glints(double t)
        {
            int per=low?1:3;
            for(int i=0;i<iceCount;i++)
            {
                int o=i*6;
                for(int k=0;k<per;k++)
                {
                    double period=2.6+3*H(i,k,61),u=Frac(t/period+H(i,k,62)),w=u<.24?Math.Sin(u/.24*Math.PI):0;if(w<=.02)continue;int cyc=(int)Math.Floor(t/period+H(i,k,62))%997;
                    double sx=H(i,k*7+cyc,63)<.5?-1:1,face=H(i,k*7+cyc,64);double x=ice[o]+(face<.5?sx*(ice[o+3]+.03):(H(i,cyc,65)*2-1)*ice[o+3]),z=ice[o+2]+(face<.5?(H(i,cyc,66)*2-1)*ice[o+5]:sx*(ice[o+5]+.03)),y=ice[o+1]+(H(i,k+cyc,67)*1.6-.6)*ice[o+4];
                    int hex=H(i,k,68)<.6?Ice:AuroraGreen;Hex(x,y,z,.16+.3*w,hex,.9*w,true,1.8);if(!low)HexStreak(x-rx*.5*w,y-ry*.5*w,z-rz*.5*w,x+rx*.5*w,y+ry*.5*w,z+rz*.5*w,.03,.03,hex,.7*w,1.6);Atmosphere++;
                }
            }
        }
        private void LampHalos(double t)
        {
            for(int i=0;i<lampCount;i++)
            {
                int o=i*6;double blink=lamp[o+5],k=blink>0?(Frac(t/blink+i*.37)<.22?1:.12):.85+.15*Math.Sin(t*2.1+i);
                Hex(lamp[o],lamp[o+1],lamp[o+2],lamp[o+4]*(.8+.2*k),(int)lamp[o+3],.55*k,true,1.5);Lights++;
            }
            // the dish's feed light, where the reflector carries it
            NativePolarRig.DishPoint(t,0,0,NativePolarLooks.DishFeed-NativePolarLooks.DishBack+.6,out double fx,out double fy,out double fz);Hex(fx,fy,fz,1.6,0xFF3020,Frac(t/1.8)<.3?.9:.2,true,1.8);Lights++;
        }
        private void Balloon(double t)
        {
            if(!NativePolarLooks.BalloonAt(t,out double x,out double y,out double z,out double scale,out double sway))return;double r=NativePolarLooks.BalloonRadius*scale;
            double sx=x+Math.Sin(sway)*NativePolarLooks.SondeDrop,sy=y-r-Math.Cos(sway)*NativePolarLooks.SondeDrop;Hex(sx,sy-.44,z,1.1,0xFF3020,Frac(t/1.1)<.35?.9:.25,true,1.8);Hex(x,y,z,r*3.2,AuroraGreen,.05,true);Lights+=2;
        }
    }
}
