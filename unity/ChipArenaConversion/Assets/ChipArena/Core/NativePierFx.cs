using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Pier 9's camera-facing effects for one frame (docs/arenas/HARBOR.md) into one <see cref="NativeFxQuads"/> (Runtime draws it
    /// as one effects NativeFxDraw batch) and, while the look's volumetric beams are on, the frame's <see cref="NativeBeamList"/>
    /// (null: billboard shafts):
    /// <list type="bullet">
    /// <item>The night: fireworks over the sea from the barge (the rising shell, the burst's stars on their arcs with their
    /// trails, willows, rings, the flash and its reflection on the water), fog lying on the water round the pier and in the
    /// channel, the pier's lights broken on the swell, the lighthouse's turning beam.</item>
    /// <item>The lights: the halos of the lamp globes, the turrets, the strings of bulbs, the rides' crowns; the Ferris wheel as
    /// a wheel of light (its rim's lamps and its spokes' colours running round it).</item>
    /// <item>The hazards (never reduced by the quality or the low tier): the coaster lane's glow through the warning (amber,
    /// running toward the train) and the danger (red), the wig-wags' glare, the train's headlamp and its sparks in the pass; a
    /// drop tower's square filling in from its edges, the column of light under the gondola, the beacon's glare, the slam's
    /// flash, the dust ring thrown across the boards and the splinters.</item>
    /// </list>
    /// Pure: the same clocks, camera, quality and map give the same quads. Allocation free after <see cref="Prepare"/>.
    /// </summary>
    public sealed class NativePierFx
    {
        public const int Capacity=6000;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        public NativeBeamList Beams;
        public int Telegraph,Atmosphere,Lights,Fireworks;
        private bool active;private string prepared;
        private double[] lamp=Array.Empty<double>();private int lampCount;private double[] bulb=Array.Empty<double>();private int bulbCount;private double[] signal=Array.Empty<double>();private int signalCount;
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz,quality;private bool low;
        private static double H(int a,int b=0,int c=0)=>NativePierLooks.Hash(a,b,c);
        private static double Frac(double v)=>v-Math.Floor(v);
        private static double L(int hex,int shift)=>NativeSceneryBuilder.Channel(hex,shift);
        private const int Warm=0xFFC870,Pink=0xFF5FA8,Cyan=0x5FE8FF,RedHex=0xFF3A28,Amber=0xFFA030,White=0xFFF4E0;

        /// <summary>The map's fixed lamps and bulbs, once per map (allocates; nothing for another map).</summary>
        public void Prepare(ContentMap map)
        {
            if(map?.id==prepared)return;prepared=map?.id;active=NativePierLooks.Active(map?.id);var lamps=new List<double>();var bulbs=new List<double>();var sig=new List<double>();
            if(!active){lamp=bulb=signal=Array.Empty<double>();lampCount=bulbCount=signalCount=0;return;}
            void Lamp(double x,double y,double z,int hex,double size){lamps.Add(x);lamps.Add(y);lamps.Add(z);lamps.Add(hex);lamps.Add(size);}
            foreach(var b in map.blocks??Array.Empty<ContentBlock>())
            {
                if(b?.id==null)continue;double top=b.pos.y+b.size.y/2;
                if(b.id.StartsWith("harbor-planter-",StringComparison.Ordinal)){foreach(int s in new[]{-1,1})Lamp(b.pos.x+s*.3,top+2.45,b.pos.z,Warm,1.5);}
                else if(b.id.StartsWith("harbor-turret-",StringComparison.Ordinal))Lamp(b.pos.x,top+3.62,b.pos.z,Warm,2.2);
                else if(b.id.StartsWith("harbor-sign-",StringComparison.Ordinal))Lamp(b.pos.x,b.pos.y,b.pos.z+(b.pos.z<0?.6:-.6),Warm,6.5);
                else if(b.id.StartsWith("harbor-kiosk-",StringComparison.Ordinal)){Lamp(b.pos.x,b.pos.y,b.pos.z+.7,Warm,3.4);Lamp(b.pos.x,b.pos.y,b.pos.z-.7,Warm,3.4);}
                else if(b.id.StartsWith("harbor-booth-",StringComparison.Ordinal))Lamp(b.pos.x,top+.3,b.pos.z,Warm,2.6);
                else if(b.id.EndsWith("-drum",StringComparison.Ordinal)){Lamp(b.pos.x,NativePierLooks.RideEave+2.6,b.pos.z,b.pos.z<0?Pink:Cyan,3.2);Lamp(b.pos.x,1.5,b.pos.z,b.pos.z<0?Pink:Cyan,5.5);}
                else if(b.id.StartsWith("harbor-signal-",StringComparison.Ordinal)){sig.Add(b.pos.x);sig.Add(b.pos.z);}
            }
            for(int turn=1;turn>=-1;turn-=2)for(int k=0;k<NativePierKit.Strings.Length/7;k++)
            {int n=12;for(int i=1;i<n;i++){NativePierKit.StringAt(k,turn,i/(double)n,out double x,out double y,out double z);bulbs.Add(x);bulbs.Add(y-.07);bulbs.Add(z);bulbs.Add(new[]{Warm,Pink,Warm,Cyan}[i%4]);}}
            lamp=lamps.ToArray();lampCount=lamp.Length/5;bulb=bulbs.ToArray();bulbCount=bulb.Length/4;signal=sig.ToArray();signalCount=signal.Length/2;
        }

        /// <summary>Fills <see cref="Quads"/>: `hazards` and the replicated state at field clock `now`, the render clock for the
        /// atmosphere, the camera and its right and up vectors, effects `quality` (0..1) and the low tier.</summary>
        public void Build(MapHazard[] hazards,int round,NativePhase phase,double fightAt,double now,double render,double camX,double camY,double camZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,double q,bool lowTier)
        {
            Quads.Clear();Telegraph=Atmosphere=Lights=Fireworks=0;if(!active)return;ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;quality=Math.Max(0,Math.Min(1,q));low=lowTier;
            double clock=NativeFactoryHazards.Clock(phase,fightAt,now);
            Shells(render);Lighthouse(render);Fog(render);Glints(render);
            for(int i=0;i<(hazards?.Length??0);i++)
            {
                var h=hazards[i];if(h==null)continue;
                if(h.kind==NativeFactoryHazards.Press)DropFx(h,round,clock,now,i);
                else if(NativeCyberCityHazards.IsMaglev(h))CoasterFx(h,NativeCyberCityHazards.At(h,phase,round,fightAt,now),NativePierLooks.TrainAt(h,phase,round,fightAt,now),now,i);
            }
            WheelLight(render);Halos(render);
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
        private bool HexStreak(double ax,double ay,double az,double bx,double by,double bz,double w0,double w1,int hex,double a,double gain=1)=>Streak(ax,ay,az,bx,by,bz,w0,w1,L(hex,16)*gain,L(hex,8)*gain,L(hex,0)*gain,a,true);
        private bool Flat(double x,double y,double z,double hx,double hz,double yaw,double r,double g,double b,double a,bool additive,int mode=1)
        {
            double c=Math.Cos(yaw),s=Math.Sin(yaw),ax=c*hx,az=-s*hx,bx=s*hz,bz=c*hz;
            return Quads.Add(x-ax-bx,y,z-az-bz,x+ax-bx,y,z+az-bz,x+ax+bx,y,z+az+bz,x-ax+bx,y,z-az+bz,r,g,b,a,mode,additive,1);
        }
        private bool FlatHex(double x,double y,double z,double hx,double hz,double yaw,int hex,double a,bool additive,double gain=1,int mode=1)=>Flat(x,y,z,hx,hz,yaw,L(hex,16)*gain,L(hex,8)*gain,L(hex,0)*gain,a,additive,mode);
        private bool Sheet(double x0,double z0,double x1,double z1,double y0,double y1,int hex,double a,double gain=1)
        =>Quads.Add(x0,y0,z0,x1,y0,z1,x1,y1,z1,x0,y1,z0,L(hex,16)*gain,L(hex,8)*gain,L(hex,0)*gain,a,0,true,1);
        private void Beam(double ax,double ay,double az,double bx,double by,double bz,double r0,double r1,int hex,double gain,double alpha,double seed)
        {
            double r=L(hex,16)*gain,g=L(hex,8)*gain,b=L(hex,0)*gain;
            if(Beams!=null){Beams.Add(ax,ay,az,bx,by,bz,r0,r1,r*alpha*NativeBeamList.FromBillboard,g*alpha*NativeBeamList.FromBillboard,b*alpha*NativeBeamList.FromBillboard,.5,seed);return;}
            double dx=bx-ax,dy=by-ay,dz=bz-az,vx=(ax+bx)/2-ex,vy=(ay+by)/2-ey,vz=(az+bz)/2-ez,cx=dy*vz-dz*vy,cy=dz*vx-dx*vz,cz=dx*vy-dy*vx,cl=Math.Sqrt(cx*cx+cy*cy+cz*cz);if(cl<1e-6)return;cx/=cl;cy/=cl;cz/=cl;
            for(int k=0;k<3;k++)for(int j=0;j<5;j++)
            {
                double t0=k/3.0,t1=(k+1)/3.0,w0=r0+(r1-r0)*t0,w1=r0+(r1-r0)*t1,o=(j-2)*.4,wt=Math.Exp(-o*o*2.2)*(1-.3*k);
                double px0=ax+dx*t0+cx*o*w0,py0=ay+dy*t0+cy*o*w0,pz0=az+dz*t0+cz*o*w0,px1=ax+dx*t1+cx*o*w1,py1=ay+dy*t1+cy*o*w1,pz1=az+dz*t1+cz*o*w1;
                Quads.Add(px0-cx*w0*.2,py0-cy*w0*.2,pz0-cz*w0*.2,px1-cx*w1*.2,py1-cy*w1*.2,pz1-cz*w1*.2,px1+cx*w1*.2,py1+cy*w1*.2,pz1+cz*w1*.2,px0+cx*w0*.2,py0+cy*w0*.2,pz0+cz*w0*.2,r,g,b,alpha*wt,0,true,1);
            }
        }

        // ---------------- the night ----------------
        /// <summary>The fireworks' rhythm: a shell every <see cref="ShellEvery"/> s, each rising <see cref="ShellRise"/> s from the
        /// barge and burning <see cref="ShellBurn"/> s.</summary>
        public const double ShellEvery=2.3,ShellRise=2.1,ShellBurn=2.9;
        private static readonly int[] ShellHex={0xFFD070,0xFF5FA8,0x5FE8FF,0xFFF0D8,0x8CFF7A,0xB48CFF,0xFF7A3A};
        /// <summary>Shell k: where it bursts (over the sea, east and north-east of the pier), its colour and its kind (0 a peony,
        /// 1 a willow, 2 a ring).</summary>
        public static void Shell(int k,out double x,out double y,out double z,out int hex,out int kind)
        {
            x=NativePierScenery.BargeX+(H(71,k)-.5)*110;z=NativePierScenery.BargeZ+(H(71,k,1)-.5)*170;y=30+H(71,k,2)*30;hex=ShellHex[(int)(H(71,k,3)*ShellHex.Length)%ShellHex.Length];double u=H(71,k,4);kind=u<.55?0:u<.8?1:2;
        }
        private void Shells(double t)
        {
            int last=(int)Math.Floor(t/ShellEvery);
            for(int k=last;k>=Math.Max(0,last-2);k--)
            {
                double age=t-k*ShellEvery-H(71,k,5)*.9;if(age<0)continue;Shell(k,out double x,out double y,out double z,out int hex,out int kind);double bx=NativePierScenery.BargeX+(H(71,k,6)-.5)*18,by=NativePierScenery.BargeY+1,bz=NativePierScenery.BargeZ;
                if(age<ShellRise)
                {   // the rising shell: a gold thread and its head
                    double u=age/ShellRise,e=1-(1-u)*(1-u),v=Math.Max(0,e-.07);HexStreak(bx+(x-bx)*v,by+(y-by)*v,bz+(z-bz)*v,bx+(x-bx)*e,by+(y-by)*e,bz+(z-bz)*e,.2,.55,0xFFC060,.8*(1-u*.5),1.6);Hex(bx+(x-bx)*e,by+(y-by)*e,bz+(z-bz)*e,1.6,0xFFE0A0,.9,true,1.8);Fireworks+=2;
                    if(age<.25)Hex(bx,by+1,bz,9*(1-age*4),0xFFB060,.7,true,1.6);continue;
                }
                double tau=age-ShellRise;if(tau>ShellBurn)continue;double life=1-tau/ShellBurn,fade=Math.Min(1,life*2.2);
                // the flash, and its light lying on the water toward the eye
                if(tau<.35){double f=1-tau/.35;Hex(x,y,z,70*f+20,hex,.55*f,true,2.2);Hex(x,y,z,26*f,0xFFFFFF,.8*f,true,2.4);Fireworks+=2;}
                {double dx=ex-x,dz=ez-z,dl=Math.Sqrt(dx*dx+dz*dz);if(dl>1){double yaw=Math.Atan2(dx,dz);FlatHex(x+dx/dl*40,NativePierLooks.Sea+.12,z+dz/dl*40,5,34,yaw,hex,.16*fade,true,1.4,1);Fireworks++;}}
                int n=Count(kind==2?44:kind==1?54:76);double drag=kind==1?.9:1.5,speed=kind==1?26:kind==2?30:34,grav=kind==1?7.5:4.2;
                for(int i=0;i<n;i++)
                {
                    // a star's direction: over the sphere (a ring's lie in one tilted plane)
                    double a=H(73+k,i)*2*Math.PI,c=kind==2?(H(73+k,i,1)-.5)*.08:H(73+k,i,1)*2-1,sr=Math.Sqrt(Math.Max(0,1-c*c)),dx=Math.Cos(a)*sr,dy=c,dz=Math.Sin(a)*sr;
                    if(kind==2){double tl=.5+H(71,k,7);double ny=dy*Math.Cos(tl)-dz*Math.Sin(tl);dz=dy*Math.Sin(tl)+dz*Math.Cos(tl);dy=ny;}
                    double sp=speed*(kind==0?.75+.25*H(73+k,i,2):1);
                    double P(double tt)=>(1-Math.Exp(-drag*tt))/drag;
                    double t1=tau,t0=Math.Max(0,tau-(kind==1?.55:.16)),p1=P(t1)*sp,p0=P(t0)*sp;
                    double x1=x+dx*p1,y1=y+dy*p1-grav*t1*t1*.5,z1=z+dz*p1,x0=x+dx*p0,y0=y+dy*p0-grav*t0*t0*.5,z0=z+dz*p0;
                    double tw=kind==1?1:.7+.3*Math.Sin(t*31+i*2.3);int col=kind==1?0xFFC870:(i%7==0?0xFFFFFF:hex);
                    HexStreak(x0,y0,z0,x1,y1,z1,.12,kind==1?.5:.75,col,(kind==1?.55:.8)*fade*tw,1.9);Hex(x1,y1,z1,kind==1?.9:1.5,col,.9*fade*tw,true,2.2);Fireworks+=2;
                }
            }
        }
        private void Lighthouse(double t)
        {
            double a=t*.52,lx=NativePierScenery.LightX,ly=NativePierScenery.LightY,lz=NativePierScenery.LightZ;
            for(int k=0;k<2;k++){double b=a+k*Math.PI,dx=Math.Sin(b),dz=Math.Cos(b);Beam(lx+dx*2,ly,lz+dz*2,lx+dx*420,ly-6,lz+dz*420,2.5,34,0xFFF0D0,1.5,.09,k+3);}
            // the lantern flares as a beam crosses the eye
            double tx=ex-lx,tz=ez-lz,tl=Math.Sqrt(tx*tx+tz*tz);if(tl>1){double to=Math.Atan2(tx,tz),d=Math.Abs(Math.IEEERemainder(a-to,Math.PI)),f=Math.Exp(-d*d*60);Hex(lx,ly,lz,10+50*f,0xFFF0D0,.5+.5*f,true,1.6+f);Atmosphere++;}
        }
        private void Fog(double t)
        {
            // banks lying on the water round the pier, drifting east; low wisps along the channel under the boards
            int n=Count(46);
            for(int i=0;i<n;i++)
            {
                double x=Frac(H(81,i)+t*.0045*(.6+H(81,i,1)))*220-110,z=H(81,i,2)*220-110;if(Math.Abs(x)<38&&Math.Abs(z)<52)continue;
                double size=16+H(81,i,3)*22,edge=Math.Min(1,(110-Math.Abs(x))/20);Hex(x,NativePierLooks.Sea+.6+size*.16,z,size,0x8AA0C8,(.035+.03*H(81,i,4))*edge,false,.8);Atmosphere++;
            }
            int m=Count(14);
            for(int i=0;i<m;i++){double x=Frac(H(83,i)+t*.012)*52-26,z=(H(83,i,1)-.5)*8;if(Math.Abs(z)<2.2&&Math.Abs(x)<21)z+=z<0?-2.4:2.4;Hex(x,NativePierLooks.Sea+.7,z,2.6+H(83,i,2)*1.2,0x9AB0D8,.07,false,.8);Atmosphere++;}
        }
        private void Glints(double t)
        {
            // the pier's lights broken on the swell: short streaks lying on the water round the pier, flickering
            int n=Count(90);
            for(int i=0;i<n;i++)
            {
                double side=H(85,i),u=H(85,i,1),out_=4+H(85,i,2)*26,x,z;
                if(side<.36){x=-20-out_;z=u*68-34;}else if(side<.72){x=20+out_;z=u*68-34;}else if(side<.86){x=u*40-20;z=-34-out_*.4;}else{x=u*40-20;z=34+out_*.4;}
                double fl=.5+.5*Math.Sin(t*(1.6+H(85,i,3)*2.2)+i*1.9);if(fl<.15)continue;int hex=H(85,i,4)<.6?Warm:H(85,i,4)<.8?Pink:Cyan;
                double yaw=Math.Atan2(x,z)+Math.PI/2;FlatHex(x,NativePierLooks.Sea+.1,z,.35+H(85,i,5)*.5,1.6+H(85,i,6)*3,yaw+Math.PI/2,hex,.22*fl,true,1.5,1);Atmosphere++;
            }
        }

        // ---------------- the lights ----------------
        private void WheelLight(double t)
        {
            double cx=NativePierLooks.WheelX,cy=NativePierLooks.WheelY,cz=NativePierLooks.WheelZ,R=NativePierLooks.WheelR,a0=NativePierLooks.WheelAngle(t);int n=NativePierLooks.Gondolas*2;
            // the rim's lamps (a wave of light chasing round) and the spokes' colours turning the other way
            for(int i=0;i<n;i++)
            {
                double a=a0+i*2*Math.PI/n,y=cy+R*Math.Sin(a),z=cz+R*Math.Cos(a),wave=.45+.55*Math.Max(0,Math.Sin(i*2*Math.PI/n*3-t*2.2));int hex=i%2==0?Pink:Cyan;
                Hex(cx,y,z,1.5+1.3*wave,hex,.5+.4*wave,true,1.5);Lights++;
                if(i%2==0){int sp=ShellHex[(i/2+(int)Math.Floor(t*.7))%ShellHex.Length];HexStreak(cx,cy,cz,cx,y,z,.3,.5,sp,.34,1.3);Lights++;}
            }
            Hex(cx,cy,cz,9,Warm,.6,true,1.6);Lights++;
        }
        private void Halos(double t)
        {
            for(int i=0;i<lampCount;i++){int o=i*5;Hex(lamp[o],lamp[o+1],lamp[o+2],lamp[o+4],(int)lamp[o+3],.42,true,1.3);Lights++;}
            int n=low?bulbCount/2:bulbCount;
            for(int i=0;i<n;i++){int o=i*4;double tw=.6+.4*Math.Sin(t*2.1+i*1.31);Hex(bulb[o],bulb[o+1],bulb[o+2],.55,(int)bulb[o+3],.6*tw,true,1.5);Lights++;}
        }

        // ---------------- the hazards (never reduced) ----------------
        private void CoasterFx(MapHazard h,in NativeHazardState st,in NativePierLooks.Train train,double t,int index)
        {
            double x0=h.center.x-h.size.x/2,x1=h.center.x+h.size.x/2,z=h.center.z,hw=h.size.z/2,half=h.size.x/2;int dir=st.Direction==0?1:st.Direction;
            bool warn=st.Phase==NativeHazardPhase.Warn,danger=st.Phase==NativeHazardPhase.Danger,pass=st.Phase==NativeHazardPhase.Active;
            if(warn||danger||pass||st.Phase==NativeHazardPhase.Clear)
            {
                // the lane's glow: amber chevrons running toward the side the train comes from, then solid red
                double level=warn?st.Level:danger||pass?1:st.Level*.6;int hex=warn?Amber:RedHex;
                for(double x=x0+1.25;x<x1;x+=2.5)
                {
                    double along=dir>0?(x-x0)/(x1-x0):(x1-x)/(x1-x0),run=warn?.35+.65*Math.Max(0,Math.Sin(t*7+along*16)):.75+.25*Math.Sin(t*20+x);
                    FlatHex(x,.05,z,1.2,hw,0,hex,.2*level*run,true,1.5,0);Telegraph++;
                    if(danger||pass){FlatHex(x,.06,z-hw,1.25,.09,0,RedHex,.85,true,2.2,0);FlatHex(x,.06,z+hw,1.25,.09,0,RedHex,.85,true,2.2,0);Telegraph+=2;}
                }
                // the wig-wags' glare at every signal
                double beat=Math.Floor(t*(warn?2.5:5));
                for(int i=0;i<signalCount;i++)for(int s=-1;s<=1;s+=2){if(((int)beat+(s>0?1:0))%2!=0)continue;Hex(signal[i*2]+s*.3,2.35,signal[i*2+1],1.7,RedHex,.8,true,2.2);Telegraph++;}
            }
            // the train's headlamps (both ends carry one; the leading one throws a beam) and, in the pass, the sparks under it
            for(int e=0;e<2;e++)
            {
                double s=e==0?train.Min:train.Min+NativePierLooks.Cars*NativePierLooks.CarLength,sg=e==0?-1:1;NativePierLooks.Track(s,half,out double x,out double y,out double pitch);
                double wx=h.center.x+x,dxn=sg*Math.Cos(pitch),dyn=sg*Math.Sin(pitch);bool leading=train.Speed*sg>0;
                Hex(wx,y+.75,z,leading?2.6:1.2,White,leading?.9:.5,true,leading?2.2:1.4);Lights++;
                if(leading&&Math.Abs(train.Speed)>1)Beam(wx+dxn*.4,y+.75+dyn*.4,z,wx+dxn*17,y+.75+dyn*17,z,.4,3.2,White,1.5,.22,index);
            }
            if(pass)
            {
                for(int i=0;i<26;i++)
                {
                    double s=train.Min+H(91,i,st.Cycle)*NativePierLooks.Cars*NativePierLooks.CarLength,ph=Frac(t*3.1+H(91,i,1));if(Math.Abs(s)>half)continue;
                    double zz=z+(i%2==0?-.72:.72),back=-dir*(1.2+2.4*ph);HexStreak(s,.14,zz,s+back,.14+.5*ph,zz+(H(91,i,2)-.5)*.8*ph,.1,.03,0xFFD890,.9*(1-ph),2.2);Telegraph++;
                }
            }
        }
        private void DropFx(MapHazard h,int round,double clock,double t,int index)
        {
            var d=NativePierLooks.DropAt(h,round,clock);double x=h.b.x,z=h.b.z,r=h.size.x/2,floor=h.b.y-h.size.y/2;
            bool warn=d.Phase==NativeFactoryHazards.Warning,fall=d.Phase==NativeFactoryHazards.Slamming,held=d.Phase==NativeFactoryHazards.Holding||d.Phase==NativeFactoryHazards.Lifting&&d.Live;
            if(warn||fall||held)
            {
                double w=fall||held?1:d.Warn,beat=fall||held?1:.6+.4*Math.Sin(t*(7+16*w));int hex=w>.66||fall||held?RedHex:Amber;
                // the square's border, and its fill closing in from the edges as the telegraph runs out
                for(int s=-1;s<=1;s+=2){FlatHex(x,floor+.05,z+s*r,r,.11,0,hex,.9*beat,true,2.2,0);FlatHex(x+s*r,floor+.05,z,.11,r,0,hex,.9*beat,true,2.2,0);Telegraph+=2;}
                double band=r*w;for(int s=-1;s<=1;s+=2){FlatHex(x,floor+.04,z+s*(r-band/2),r,band/2,0,hex,.26*beat,true,1.4,0);FlatHex(x+s*(r-band/2),floor+.04,z,band/2,r-band,0,hex,.26*beat,true,1.4,0);Telegraph+=2;}
                // the column of light from the square up to the gondola's underside
                double top=Math.Max(floor+.4,d.Y);for(int s=-1;s<=1;s+=2){Sheet(x-r,z+s*r,x+r,z+s*r,floor,top,hex,.05+.09*w,1.4);Sheet(x+s*r,z-r,x+s*r,z+r,floor,top,hex,.05+.09*w,1.4);Telegraph+=2;}
                Hex(x,d.Y+.1,z,r*2.6,hex,.3+.4*w,true,1.5);Hex(x,h.a.y+3.3,z,3+3*beat,RedHex,.8,true,2.2);Telegraph+=2;
            }
            if(d.Since<1.1)
            {
                // the slam: a flash, a ring of dust thrown across the boards, splinters
                double u=d.Since/1.1,f=Math.Max(0,1-d.Since/.22);
                if(f>0){Hex(x,floor+.6,z,12*f+4,0xFFE0B0,.8*f,true,2.2);Telegraph++;}
                double rr=r+1+u*5.5;int seg=28;for(int i=0;i<seg;i++){double a=i*2*Math.PI/seg+index;Hex(x+Math.Cos(a)*rr,floor+.35+u*.5,z+Math.Sin(a)*rr,1.6+u*2.2,0xB8A890,.3*(1-u),false,.9);Telegraph++;}
                for(int i=0;i<18;i++){double a=H(95+index,i,d.Cycle)*2*Math.PI,v=4+H(95+index,i,1)*6,up=3+H(95+index,i,2)*4,ts=d.Since,px=x+Math.Cos(a)*(r+v*ts),pz=z+Math.Sin(a)*(r+v*ts),py=floor+.2+up*ts-9*ts*ts;if(py<floor)continue;
                    HexStreak(px,py,pz,px-Math.Cos(a)*.35,py-.15,pz-Math.Sin(a)*.35,.07,.02,0xD8B080,.9*(1-u),1.3);Telegraph++;}
            }
        }
    }
}
