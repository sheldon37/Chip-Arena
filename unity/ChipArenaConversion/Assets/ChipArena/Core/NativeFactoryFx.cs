using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Factory's camera-dependent effect quads for one frame (FACTORY_PROPOSAL.md 3.10 "Atmosphere", 3.8 telegraphs), into a
    /// <see cref="NativeFxQuads"/> that Runtime/NativeFactoryPresentation.cs draws as one solid and one effects NativeSirenFx
    /// batch (two draws). Everything is a pure function of the hazard clock (hazards, from <see cref="NativeFactoryHazards.State"/>)
    /// and the presentation clock (life and air), so the killcam and every client see the same sparks, steam and dust.
    /// <list type="bullet">
    /// <item>Telegraphs, never reduced (2.1 rule 4): halos on every lit hazard lamp, the slot's glow strip while a saw warns,
    /// the crane zone's red floor wash and the load's contact shadow straight below it (the sun's own shadow would land metres
    /// away), the trolley beacon's sweep.</item>
    /// <item>Hazard action (halved on Reduced, fewer on the low tier): blade sparks raking back from the slot, blur discs on
    /// spinning blades, the impact's dust ring, sparks and shock skirt, the press's slam puffs, the vents' steam (the burst
    /// column is the damage volume, so it keeps its size).</item>
    /// <item>Atmosphere (arena and killcam only; off or halved on the low tier): sun shafts down every skylight slot, dust
    /// motes brightening inside them, the ladle's churning melt and embers, steam leaks, the welding bay's arc.</item>
    /// <item>Backdrop life (also behind the menu): three stack plumes and the cooling tower's, aviation lights, the furnace flare.</item>
    /// </list>
    /// Allocation free (fixed capacity, no closures over the frame's arrays).
    /// </summary>
    public sealed class NativeFactoryFx
    {
        public const int Capacity=3600;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        public int Sparks,Puffs,Halos,Shafts,Motes;
        /// <summary>The first sun-shaft quad of the last build (the shafts are Shafts quads from here).</summary>
        public int ShaftStart;
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz;private NativeFxLight light;
        /// <summary>The skylight slots (z from, z to; north hall, the south is their mirror) and the halls' x extent: FACTORY_PROPOSAL.md 3.3.</summary>
        public static readonly double[] SlotZ={12.6,14.1,17.1,18.6,21.6,23.1,26.1,27.6};
        public const double HallX=36,RoofY=11.5;
        /// <summary>The shafts' bounds: the outer walls' inner faces (x ±38, z ±30) and the halls' faces of the bay walls (z ±10.4).</summary>
        public const double ShellX=37.8,ShellZ=29.8,BayWallZ=10.5;
        /// <summary>Counts at full quality: motes, embers, sparks per running saw, impact puffs and sparks, steam puffs per vent.</summary>
        public const int MoteCount=200,EmberCount=22,SawSparks=64,ImpactPuffs=26,ImpactSparks=40,VentPuffs=22,LeakPuffs=8;
        private static readonly double[] LeakX={-36.6,36.6,-20.0},LeakY={7.25,7.25,7.25},LeakZ={29.9,-29.9,29.9};

        public static double Hash(int a,int b=0,int c=0){unchecked{uint h=(uint)a*0x9E3779B1u^(uint)b*0x85EBCA77u^(uint)c*0xC2B2AE3Du;h^=h>>15;h*=0x2C1B3C6Du;h^=h>>12;h*=0x297A2D39u;h^=h>>15;return h/4294967296.0;}}
        private static double Frac(double x)=>x-Math.Floor(x);
        private static double Wrap(double v,double l)=>((v%l)+l*1.5)%l-l/2;
        private static double Lin(int hex,int shift)=>NativeSceneryBuilder.Channel(hex,shift);
        private void Sprite(double x,double y,double z,double size,double r,double g,double b,double a,bool additive)
        {double h=size/2,ax=rx*h,ay=ry*h,az=rz*h,bx=ux*h,by=uy*h,bz=uz*h;Quads.Add(x-ax-bx,y-ay-by,z-az-bz,x+ax-bx,y+ay-by,z+az-bz,x+ax+bx,y+ay+by,z+az+bz,x-ax+bx,y-ay+by,z-az+bz,r,g,b,a,1,additive,1);}
        /// <summary>A flat soft quad on a horizontal plane (floor washes, contact shadows, shock skirts).</summary>
        private void Floor(double x,double y,double z,double sx,double sz,double r,double g,double b,double a,bool additive)
        {double hx=sx/2,hz=sz/2;Quads.Add(x-hx,y,z-hz,x+hx,y,z-hz,x+hx,y,z+hz,x-hx,y,z+hz,r,g,b,a,1,additive,1);}
        /// <summary>A streak from (x, y, z) along (dx, dy, dz), `width` wide, turned to the eye (sparks, shafts).</summary>
        private void Streak(double x,double y,double z,double dx,double dy,double dz,double width,double r,double g,double b,double a,bool additive,int mode)
        {
            double vx=ex-x,vy=ey-y,vz=ez-z,cx=dy*vz-dz*vy,cy=dz*vx-dx*vz,cz=dx*vy-dy*vx,l=Math.Sqrt(cx*cx+cy*cy+cz*cz);if(l<1e-9)return;double k=width/2/l;cx*=k;cy*=k;cz*=k;
            Quads.Add(x-cx,y-cy,z-cz,x+cx,y+cy,z+cz,x+dx+cx,y+dy+cy,z+dz+cz,x+dx-cx,y+dy-cy,z+dz-cz,r,g,b,a,mode,additive,1);
        }
        private void Lit(int hex,double ny,out double r,out double g,out double b){NativeBrickfallAtmosphere.Shade(Lin(hex,16),Lin(hex,8),Lin(hex,0),0,ny,0,light.Lx,light.Ly,light.Lz,light.Sr,light.Sg,light.Sb,light.Ambient,.5,out r,out g,out b);}
        private static void LampLinear(int hex,out double r,out double g,out double b){r=Lin(hex,16);g=Lin(hex,8);b=Lin(hex,0);}

        /// <summary>
        /// Fills <see cref="Quads"/>. `clock` the hazard clock (NativeFactoryHazards.Clock), `time` the presentation clock (life,
        /// air, backdrop); the camera's position, right and up; the scene light; effects quality 0..1 and the low graphics tier;
        /// `atmosphere` false behind the menu (backdrop life and hazards only).
        /// </summary>
        public void Build(FactoryHazard[] hazards,int round,double clock,double time,double camX,double camY,double camZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,NativeFxLight sun,double quality,bool low,bool atmosphere)
        {
            Quads.Clear();ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;light=sun;Sparks=Puffs=Halos=Shafts=Motes=0;
            quality=Math.Max(0,Math.Min(1,quality));double action=low?Math.Min(quality,.5):quality;
            Backdrop(time,quality);
            if(hazards!=null)
            {
                // the dark contact shadows and floor washes first (premultiplied), then steam, then the additive work
                foreach(var h in hazards)if(h?.kind==NativeFactoryHazards.Crane)CraneGround(h,NativeFactoryHazards.State(h,round,clock),clock);
                foreach(var h in hazards)if(h?.kind==NativeFactoryHazards.Vent)VentSteam(h,round,clock,action);
                foreach(var h in hazards)
                {
                    if(h==null)continue;var st=NativeFactoryHazards.State(h,round,clock);
                    switch(h.kind)
                    {
                        case NativeFactoryHazards.SawSlot:SlotFx(h,st,round,clock,action);break;case NativeFactoryHazards.SawRail:RailFx(h,st,clock);break;
                        case NativeFactoryHazards.Press:PressFx(h,st,clock,action);break;case NativeFactoryHazards.Crane:CraneFx(h,st,round,clock,action);break;
                    }
                }
            }
            if(!atmosphere)return;
            ShaftStart=Quads.Count;if(!low)ShaftsPass(time,quality);
            MotesPass(time,quality,low);LadlePass(time,quality,low);LeaksPass(time,action);WeldPass(time,action);
        }

        // ---------------- hazards ----------------
        // Halos on a lit lamp (any colour brighter than the off state).
        private void Halo(double x,double y,double z,int hex,double size)
        {
            if(hex==NativeFactoryRig.LampOff)return;LampLinear(hex,out double r,out double g,out double b);double d=Math.Sqrt((x-ex)*(x-ex)+(y-ey)*(y-ey)+(z-ez)*(z-ez));
            double k=hex==NativeFactoryRig.LampSafe?.35:1;Sprite(x,y,z,Math.Max(size,d*.012),r*1.6*k,g*1.6*k,b*1.6*k,.8,true);Halos++;
        }
        private void SlotFx(FactoryHazard h,FactoryHazardState st,int round,double clock,double q)
        {
            bool alongX=Math.Abs(h.b.x-h.a.x)>Math.Abs(h.b.z-h.a.z);double s0=alongX?h.a.x:h.a.z,s1=alongX?h.b.x:h.b.z,c=alongX?h.a.z:h.a.x,len=Math.Abs(s1-s0);int n=(int)Math.Floor(len/2)+1;
            for(int i=0;i<n;i++){double f=n>1?(double)i/(n-1):0,s=s0+(s1-s0)*f;int col=NativeFactoryRig.SlotLamp(st,f,len,clock);
                for(int k=0;k<2;k++){double side=k==0?-.28:.28;if(alongX)Halo(s,.06,c+side,col,.32);else Halo(c+side,.06,s,col,.32);}}
            // the warning: an amber glow strip down the slot and the blade grinding up sparks at its start end
            if(st.Phase==NativeFactoryHazards.Warning){double a=.18+.12*Math.Sin(clock*NativeFactoryRig.WarnHz*2*Math.PI);
                for(int i=0;i<n-1;i++){double s=s0+(s1-s0)*(i+.5)/(n-1);if(alongX)Floor(s,.02,c,len/(n-1)*1.05,.9,.9,.42,.05,a,true);else Floor(c,.02,s,.9,len/(n-1)*1.05,.9,.42,.05,a,true);}}
            if(st.Lift>0&&st.Rate>15)
            {
                // the spinning blade's blur disc (half of it below the floor is hidden by the slab) and the cut's orange glow along the slot
                double bx=st.Center.x,by=st.Center.y,bz=st.Center.z,k=Math.Min(1,st.Lift*1.5)*Math.Min(1,st.Rate/NativeFactoryHazards.SpinRate);
                Sprite(bx,Math.Max(by,.05),bz,h.radius*2.1,.55,.58,.62,.5*k,true);Sprite(bx,Math.Max(by,.05),bz,h.radius*1.2,.35,.37,.4,.35*k,true);
                if(alongX)Floor(bx,.02,bz,h.radius*2.6,.5,2.2,.7,.12,.7*k,true);else Floor(bx,.02,bz,.5,h.radius*2.6,2.2,.7,.12,.7*k,true);
            }
            // sparks: born at the blade's floor cut over the last .5 s, raking back against its travel and up, falling under gravity
            int count=(int)(SawSparks*q);if(st.Phase==NativeFactoryHazards.Warning)count=(int)(count*st.Warn*.4);
            if(st.Phase==NativeFactoryHazards.Parked||count<=0)return;
            double dir=(st.Reverse?-1:1)*Math.Sign(s1-s0),life=.5;
            for(int i=0;i<count;i++)
            {
                double born=clock-life*Frac(Hash(i,h.id.Length,7)+clock*2.3),age=clock-born;var b=NativeFactoryHazards.State(h,round,born);if(b.Lift*h.radius<.05)continue;
                double cut=Math.Sqrt(Math.Max(0,h.radius*h.radius-(b.Center.y)*(b.Center.y)));double px=(alongX?b.Center.x-dir*cut:b.Center.x),pz=(alongX?b.Center.z:b.Center.z-dir*cut);
                double sp=7+6*Hash(i,3),ang=.35+.5*Hash(i,5),side=(Hash(i,9)-.5)*1.6,vx=alongX?-dir*sp*Math.Cos(ang):side,vz=alongX?side:-dir*sp*Math.Cos(ang),vy=sp*Math.Sin(ang);
                double x=px+vx*age,y=.02+vy*age-4.9*age*age,z=pz+vz*age;if(y<0)continue;double tdx=vx*.035,tdy=(vy-9.8*age)*.035,tdz=vz*.035,heat=1-age/life;
                Streak(x,y,z,tdx*1.6,tdy*1.6,tdz*1.6,.035,3.6*heat+.7,1.6*heat+.25,.3*heat,.95*heat,true,0);Sparks++;
            }
        }
        private void RailFx(FactoryHazard h,FactoryHazardState st,double clock)
        {
            double dir=Math.Sign(h.b.z-h.a.z),face=h.a.z+dir*.6;Halo(h.a.x,h.a.y+h.radius+.36,face+dir*.1,NativeFactoryRig.HazardLamp(st,clock),.75);
            // the sweep's band across the deck (0.2 m under the blade; from the wall face to as far short of b as the housing is deep): amber pulsing through the warning, red while live; never reduced
            double deck=h.a.y-h.radius-.2+.02,far=h.b.z-dir*.6,zm=(face+far)/2,len=Math.Abs(far-face);
            if(st.Phase==NativeFactoryHazards.Warning){double a=.3+.18*Math.Sin(clock*NativeFactoryRig.WarnHz*2*Math.PI);Floor(h.a.x,deck,zm,1.0,len,.9,.42,.05,a,true);
                for(int i=0;i<6;i++){double t=Frac(Hash(i,13)+clock*2.5);Streak(h.a.x+(Hash(i,3)-.5)*.5,h.a.y-h.radius*.4-t*.5,face+dir*t*.5,0,-.06,dir*.05,.02,2.4,1.0,.2,.7*(1-t),true,0);Sparks++;}}
            else if(st.Lethal)Floor(h.a.x,deck,zm,1.0,len,1.1,.12,.06,.55,true);
            if(st.Rate>15){double k=Math.Min(1,st.Rate/NativeFactoryHazards.SpinRate);Sprite(st.Center.x-.12,st.Center.y,st.Center.z,h.radius*2.1,.55,.58,.62,.5*k,true);Sprite(st.Center.x-.12,st.Center.y,st.Center.z,h.radius*1.2,.35,.37,.4,.35*k,true);}
            if(st.Phase==NativeFactoryHazards.Running||st.Phase==NativeFactoryHazards.Returning)for(int i=0;i<10;i++){double t=Frac(Hash(i,11)+clock*3),y=st.Center.y+h.radius*.6,z=face;Streak(h.a.x+(Hash(i,2)-.5)*.3,y-t*.6,z+dir*t*.8,0,-.08,dir*.05,.02,2.6,1.1,.2,.8*(1-t),true,0);Sparks++;}
        }
        private void PressFx(FactoryHazard h,FactoryHazardState st,double clock,double q)
        {
            int lamp=NativeFactoryRig.HazardLamp(st,clock);double hz=h.size.z/2,head=h.a.y+h.size.y/2;for(int k=0;k<2;k++){double s=k==0?-1:1;Halo(h.a.x,head+.27,h.a.z+s*(hz+.09),lamp,.45);}
            Halo(h.a.x,4.84,h.a.z-1.7*Math.Sign(h.a.z),lamp,.6);
            // the slam: dust and a few sparks out of both tunnel mouths for 1 s
            if(st.Phase==NativeFactoryHazards.Holding||st.Phase==NativeFactoryHazards.Lifting){double age=st.Age-h.warn-h.rise;if(age<0||age>1.1)return;int n=(int)(10*q);
                for(int k=0;k<2;k++){double s=k==0?-1:1;for(int i=0;i<n;i++){double t=age/1.1,dx=(Hash(i,k,4)-.5)*h.size.x,x=h.a.x+dx,z=h.a.z+s*(hz+.2+1.4*t*Hash(i,k,5)),y=.2+.9*t*Hash(i,k,6);
                    Lit(0xB8AE9C,.7,out double r,out double g,out double b);Sprite(x,y,z,.5+1.3*t,r,g,b,.42*(1-t),false);Puffs++;}}}
        }
        private void VentSteam(FactoryHazard h,int round,double clock,double q)
        {
            var st=NativeFactoryHazards.State(h,round,clock);if(st.Phase==NativeFactoryHazards.Parked)return;bool burst=st.Phase==NativeFactoryHazards.Bursting;
            int n=burst?VentPuffs:(int)(10*Math.Max(.5,q));double top=h.size.y;
            for(int i=0;i<n;i++)
            {
                double life=burst?.9:1.6,t=Frac(Hash(i,h.id.Length,2)+clock/life),x=h.a.x+(Hash(i,3)-.5)*h.size.x*(.4+.6*t),z=h.a.z+(Hash(i,4)-.5)*h.size.z*(.4+.6*t),y=h.a.y+.1+(burst?top*1.1:1.2)*t;
                double a=burst?.55*st.U*(1-t*.6):.4*st.Warn*(1-t);if(a<.01)continue;Lit(0xE8ECEE,.8,out double r,out double g,out double b);Sprite(x,y,z,(burst?.9:.4)+(burst?1.6:.8)*t,r*1.05,g*1.05,b*1.05,a,false);Puffs++;
            }
        }
        // The crane's ground telegraph: the zone's red wash and the load's contact shadow right below it.
        private void CraneGround(FactoryHazard h,FactoryHazardState st,double clock)
        {
            if(st.Zone<0||h.zones==null)return;var z=h.zones[st.Zone];double sx=h.size.x+1,sz=h.size.z+1;
            if(st.Phase==NativeFactoryHazards.Warning||st.Phase==NativeFactoryHazards.Falling)
            {
                double pulse=st.Phase==NativeFactoryHazards.Falling?1:.55+.45*(NativeFactoryRig.Strobe(clock,NativeFactoryRig.ZoneHz)?1:0);double a=(.25+.45*st.Warn)*pulse;
                Floor(z.center.x,.015,z.center.z,sx*1.25,sz*1.15,.95,.05,.03,a,true);Floor(z.center.x,.016,z.center.z,sx*.8,sz*.8,.9,.08,.03,a*.6,true);
            }
            if(st.Phase==NativeFactoryHazards.Travelling||st.Phase==NativeFactoryHazards.Warning||st.Phase==NativeFactoryHazards.Falling)
            {
                // sharper and darker as it nears: a soft wide penumbra under a tighter core
                double near=st.Phase==NativeFactoryHazards.Falling?1-st.LoadY/Math.Max(.1,h.carry):st.Phase==NativeFactoryHazards.Warning?.25*st.Warn:0,a=st.Phase==NativeFactoryHazards.Travelling?.18:.32+.45*near+.18*st.Warn;
                double spread=1.6-.55*near;Floor(st.X,.02,st.Z,h.size.x*spread+.6,h.size.z*spread+.6,0,0,0,a*.6,false);Floor(st.X,.021,st.Z,h.size.x*(1.05+.2*(1-near)),h.size.z*(1.02+.15*(1-near)),0,0,0,a,false);
            }
        }
        private void CraneFx(FactoryHazard h,FactoryHazardState st,int round,double clock,double q)
        {
            // the trolley beacon: a sweeping amber or red halo (rotating at .8 turns a second while it moves or warns)
            int col=NativeFactoryRig.HazardLamp(st,clock);double bx=st.X,by=h.a.y+.14+1.76+1.21,bz=st.Z;
            if(col!=NativeFactoryRig.LampSafe&&col!=NativeFactoryRig.LampOff){double az=Math.Atan2(ex-bx,ez-bz),beam=clock*.8*2*Math.PI,k=Math.Pow(Math.Max(0,Math.Cos(az-beam)),6);LampLinear(col,out double r,out double g,out double b);
                double d=Math.Sqrt((bx-ex)*(bx-ex)+(by-ey)*(by-ey)+(bz-ez)*(bz-ez));Sprite(bx,by,bz,Math.Max(.6,d*.02)*(1+2.5*k),r*(1+3*k),g*(1+3*k),b*(1+3*k),.85,true);Halos++;}
            else Halo(bx,by,bz,col,.4);
            for(int k=0;k<(h.zones?.Length??0);k++){var zn=h.zones[k];double hx=h.size.x/2+.5,hz=h.size.z/2+.5;int c=NativeFactoryRig.ZoneLamp(st,k,clock);
                for(int j=0;j<4;j++){double sx=(j&1)==0?-1:1,sz=(j&2)==0?-1:1;Halo(zn.center.x+sx*(hx+.08),.08,zn.center.z+sz*(hz+.08),c,.5);}}
            // the impact: a dust ring rolling out to 5 m, a shock skirt, sparks, then settling haze (4 s)
            if(double.IsNaN(st.ImpactAt))return;double age=clock-st.ImpactAt;if(age<0||age>4)return;
            var z=h.zones[Math.Max(0,st.Zone)];double cx=z.center.x,cz=z.center.z;int puffs=(int)(ImpactPuffs*q);
            if(age<.6){double t=age/.6;for(int i=0;i<16;i++){double a=i*Math.PI/8,r0=h.size.z*.55+3.5*t;Floor(cx+Math.Sin(a)*r0,.03,cz+Math.Cos(a)*r0*.8,1.2+1.5*t,1.2+1.5*t,.55,.5,.44,.35*(1-t),false);}}
            for(int i=0;i<puffs;i++)
            {
                double a=Hash(i,21)*Math.PI*2,sp=.6+.4*Hash(i,22),t=Math.Min(1,age/2.6),grow=1-Math.Pow(1-t,2.2),d=(h.size.z*.5+4.6*sp*grow);
                double x=cx+Math.Sin(a)*d,zz=cz+Math.Cos(a)*d*.8,y=.3+(.6+1.6*Hash(i,23))*grow,size=1.0+2.4*grow,alpha=.5*(1-Math.Pow(age/4,1.4));
                Lit(0x9C9282,.6,out double r,out double g,out double b);Sprite(x,y,zz,size,r,g,b,alpha,false);Puffs++;
            }
            if(age<.7)for(int i=0;i<(int)(ImpactSparks*q);i++)
            {
                double a=Hash(i,31)*Math.PI*2,sp=6+7*Hash(i,32),vy=3+5*Hash(i,33),t=age;double x=cx+Math.Sin(a)*(h.size.x*.5+sp*t),zz=cz+Math.Cos(a)*(h.size.z*.5+sp*t),y=.1+vy*t-4.9*t*t;if(y<0)continue;double heat=1-t/.7;
                Streak(x,y,zz,Math.Sin(a)*sp*.03,(vy-9.8*t)*.03,Math.Cos(a)*sp*.03,.024,3.4*heat+.4,1.5*heat+.2,.3*heat,.9*heat,true,0);Sparks++;
            }
        }

        // ---------------- atmosphere ----------------
        private void ShaftsPass(double time,double quality)
        {
            // Each slot throws a slanted sheet of sun to the floor: segments every 6 m along the hall, faint and warm, fading at both ends.
            double lx=light.Lx,ly=light.Ly,lz=light.Lz;if(ly<.15)return;double drop=RoofY/ly,dx=-lx*drop,dy=-RoofY,dz=-lz*drop,k=.035+.03*quality;
            double sr=light.Sr*k,sg=light.Sg*k,sb=light.Sb*k;
            for(int side=0;side<2;side++)for(int i=0;i+1<SlotZ.Length;i+=2)
            {
                double zc=(side==0?1:-1)*(SlotZ[i]+SlotZ[i+1])/2;
                for(double x=-HallX+3;x<HallX-2;x+=6)
                {
                    // the sheet stops where it meets an outer wall or the bay wall (never drawn outside the hall it lights)
                    double t=1,ex2=x+dx,ez2=zc+dz,zin=side==0?BayWallZ:-ShellZ,zout=side==0?ShellZ:-BayWallZ;if(Math.Abs(ex2)>ShellX)t=Math.Min(t,(Math.Sign(dx)*ShellX-x)/dx);
                    if(ez2>zout)t=Math.Min(t,(zout-zc)/dz);else if(ez2<zin)t=Math.Min(t,(zin-zc)/dz);if(t<.15)continue;
                    double flick=.85+.15*Math.Sin(time*.21+x*.7+zc);Streak(x,RoofY,zc,dx*t,dy*t,dz*t,1.6,sr*flick,sg*flick,sb*flick,1,true,1);Shafts++;
                }
            }
        }
        private void MotesPass(double time,double quality,bool low)
        {
            int n=(int)(MoteCount*quality*(low?.5:1));double r=light.Sr*.5+light.Ambient[3],g=light.Sg*.5+light.Ambient[4],b=light.Sb*.5+light.Ambient[5];
            for(int i=0;i<n;i++)
            {
                // world-anchored motes wrapped into an 18 m box round the eye, drifting slowly
                const double cell=9,L=cell*2;double ox=(Hash(i,1)-.5)*L,oy=Hash(i,2)*6-1.5,oz=(Hash(i,3)-.5)*L,drift=time*(.08+.06*Hash(i,4));
                double x=ex+Wrap(ox+Math.Sin(drift+i)*.6-ex,L),z=ez+Wrap(oz+drift*.7-ez,L),y=Math.Max(.2,ey+oy+Math.Sin(drift*1.3+i*.7)*.3);
                // brighter inside a sun shaft: trace up the sun to the roof and test the slot
                double lit=.35;if(light.Ly>.15){double t=(RoofY-y)/light.Ly,sz=Math.Abs(z+light.Lz*t),sx=Math.Abs(x+light.Lx*t);if(sx<HallX)for(int s=0;s+1<SlotZ.Length;s+=2)if(sz>SlotZ[s]&&sz<SlotZ[s+1]){lit=1.6;break;}}
                double d=Math.Sqrt((x-ex)*(x-ex)+(y-ey)*(y-ey)+(z-ez)*(z-ez));if(d<.8)continue;double fade=Math.Min(1,(cell-d)/2);if(fade<=0)continue;
                Sprite(x,y,z,.035+.02*Hash(i,5),r*lit,g*lit,b*lit,.7*fade,true);Motes++;
            }
        }
        private void LadlePass(double time,double quality,bool low)
        {
            // the melt: three churning glows on the ladle's top (4.0 m), embers lifting off it, heat shimmer steam
            const double top=4.02;for(int i=0;i<3;i++){double a=time*(.3+.1*i)+i*2.1;Floor(Math.Sin(a)*.5,top+i*.002,Math.Cos(a*1.3)*.5,2.8-.5*i,2.8-.5*i,2.6,.8,.12,.9,true);}
            Floor(0,top+.01,0,3.1,3.1,1.4,.35,.05,.85,true);
            int n=(int)(EmberCount*quality*(low?.5:1));for(int i=0;i<n;i++){double life=1.6+Hash(i,41),t=Frac(Hash(i,42)+time/life),x=(Hash(i,43)-.5)*2.6+Math.Sin(time+i)*.2*t,z=(Hash(i,44)-.5)*2.6,y=top+.1+3.2*t;
                Sprite(x,y,z,.05+.03*(1-t),3.0,1.0,.2,.9*(1-t),true);Sparks++;}
            int m=(int)(10*quality);for(int i=0;i<m;i++){double t=Frac(Hash(i,45)+time/5),x=(Hash(i,46)-.5)*2,z=(Hash(i,47)-.5)*2,y=top+.3+4*t;Lit(0xC8C4BC,.8,out double r,out double g,out double b);Sprite(x,y,z,1+2*t,r,g,b,.12*(1-t)*Math.Min(1,t*5),false);Puffs++;}
        }
        private void LeaksPass(double time,double q)
        {
            int n=(int)(LeakPuffs*q);for(int s=0;s<LeakX.Length;s++){double on=Frac(time/7.3+s*.37);if(on>.55)continue;double inward=LeakZ[s]>0?-1:1;
                for(int i=0;i<n;i++){double t=Frac(Hash(i,s,51)+time/1.4);Lit(0xE4E8EA,.8,out double r,out double g,out double b);Sprite(LeakX[s]+(Hash(i,s,52)-.5)*.3*t,LeakY[s]+.6*t,LeakZ[s]+inward*1.6*t,.25+.9*t,r,g,b,.32*(1-t),false);Puffs++;}}
        }
        private void WeldPass(double time,double q)
        {
            // the welding bay in the north-west room: arcs in bursts (2.2 s on, 1.5 s off), a blue-white flash and a spray of sparks
            double u=time%3.7;if(u>2.2)return;double x=-35.5,y=1.15,z=24.6,flash=.6+.4*Math.Sin(time*53)*Math.Sin(time*31);
            Sprite(x,y,z,.7+.3*flash,2.2*flash,2.6*flash,3.2*flash,.9,true);Halos++;
            int n=(int)(16*q);for(int i=0;i<n;i++){double t=Frac(Hash(i,61)+time*1.7),a=Hash(i,62)*Math.PI*2,sp=1.5+2*Hash(i,63);double px=x+Math.Sin(a)*sp*t*.4,pz=z+Math.Cos(a)*sp*t*.4,py=y+1.2*t-3*t*t;if(py<0)continue;
                Streak(px,py,pz,Math.Sin(a)*.03,.02-.05*t,Math.Cos(a)*.03,.015,3,1.6,.6,.9*(1-t),true,0);Sparks++;}
        }

        // ---------------- backdrop life ----------------
        private void Backdrop(double time,double quality)
        {
            int puffs=quality<.5?16:28;
            for(int c=0;c<NativeFactoryScenery.Stacks;c++)
            {
                NativeFactoryScenery.Stack(c,out double sx,out double sz,out double sh);
                for(int p=0;p<puffs;p++){NativeFactoryScenery.Plume(c,p,time,sx,sh,sz,out double x,out double y,out double z,out double s,out double a);Lit(0x8A8682,.6,out double r,out double g,out double b);Sprite(x,y,z,s,r*.9,g*.9,b*.9,a,false);Puffs++;}
                // aviation lights: red at the top and mid height, 40 a minute
                if(Frac((time+c*.41)/1.5)<.4)for(int l=0;l<2;l++){double y=l==0?sh+.3:sh*.55,d=Math.Sqrt((sx-ex)*(sx-ex)+(y-ey)*(y-ey)+(sz-ez)*(sz-ez));Sprite(sx,y,sz,Math.Max(1.6,d*.03),2.2,.1,.05,.9,true);}
            }
            // the cooling tower's plume: slow, white, wide
            for(int p=0;p<puffs;p++){NativeFactoryScenery.Plume(NativeFactoryScenery.Stacks,p,time,NativeFactoryScenery.CoolingX,NativeFactoryScenery.CoolingH,NativeFactoryScenery.CoolingZ,out double x,out double y,out double z,out double s,out double a);Lit(0xE6E8E8,.7,out double r,out double g,out double b);Sprite(x,y,z,s*1.8,r,g,b,a*.8,false);Puffs++;}
            // the furnace top flare: a flickering orange glow over the stack's crown
            double f=.7+.3*Math.Sin(time*2.1)*Math.Sin(time*1.37+1);Sprite(NativeFactoryScenery.FurnaceX,NativeFactoryScenery.FurnaceH+3,NativeFactoryScenery.FurnaceZ,9+3*f,2.4*f,.9*f,.25*f,.55,true);
        }
    }
}
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Factory's ambience (FACTORY_PROPOSAL.md 3.11; MapLooks "ambience": "factory") on the existing sound keys: a schedule
    /// of one-shots at fixed emitters, pure in the presentation clock, so a caller that keeps its last clock hears each once.
    /// Kinds: lathe chatter ("metal"), the bay's steam hammers ("stone"), the ladle's crackle ("fire"), the welding bay's arc
    /// ("lightning", on its bursts, NativeFactoryFx.WeldPass), the steam leaks ("water", on their puffs), a distant works horn
    /// ("countdown", low). Each emitter repeats at its period with a hashed jitter; gains are low (it sits under the fight).
    /// </summary>
    public static class NativeFactoryAmbience
    {
        public const int Clank=100,Hammer=101,Crackle=102,Arc=103,Hiss=104,Horn=105;
        private static readonly int[] Kind={Clank,Clank,Clank,Clank,Hammer,Hammer,Crackle,Arc,Hiss,Hiss,Hiss,Horn};
        private static readonly double[] X={-22.5,-4.5,22.5,4.5,-8.5,8.5,0,-35.5,-36.6,36.6,-20,60},Y={1.2,1.2,1.2,1.2,2,2,4.2,1.2,7.25,7.25,7.25,20},Z={13.3,13.3,-13.3,-13.3,5,-5,0,24.6,29.9,-29.9,29.9,-90};
        private static readonly double[] Period={3.3,4.1,3.7,2.9,4.6,5.2,1.9,3.7,7.3,7.3,7.3,47},Jitter={.8,.9,.7,.6,.3,.3,.6,0,0,0,0,0};
        public static int Emitters=>Kind.Length;
        /// <summary>The one-shots due in (from, to] of the presentation clock, into `into` (at most its length).</summary>
        public static int Between(double from,double to,FactoryCue[] into)
        {
            int count=0;if(into==null||!(to>from)||double.IsInfinity(to)||to-from>5)return 0;
            for(int e=0;e<Kind.Length;e++)
            {
                double p=Period[e],phase=e==8?0:e==9?.37*7.3:e==10?.74*7.3:NativeFactoryFx.Hash(e,71)*p;
                for(double n=Math.Floor((from-phase)/p);n<=Math.Floor((to-phase)/p)+1;n++)
                {double at=phase+n*p+Jitter[e]*(NativeFactoryFx.Hash(e,(int)n,72)-.5);if(at>from&&at<=to&&count<into.Length)into[count++]=new FactoryCue{Kind=Kind[e],Hazard=e,Cycle=(int)n,Time=at,At=new V3((float)X[e],(float)Y[e],(float)Z[e])};}
            }
            return count;
        }
    }
}
