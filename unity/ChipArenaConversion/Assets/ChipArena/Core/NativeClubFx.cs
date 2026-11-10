using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Substation 128's effects for one frame (docs/arenas/CLUB.md "The look") into one <see cref="NativeFxQuads"/> (Runtime
    /// draws it as one effects NativeFxDraw batch) and, while the look's volumetric beams are on above the low tier, the frame's
    /// <see cref="NativeBeamList"/> (null: billboard shafts):
    /// <list type="bullet">
    /// <item>Telegraphs (never reduced by the effects quality, the tier or the light-flash setting): the charged floor's red
    /// section outlines and rising sparks while it charges, its arcs and crackle while live, its smoke as it discharges; the
    /// laser wall's preview line through its danger and its three red beams, glow sheet and sparks in the pass; the drop's
    /// air rings pumping from the cones through the build and the shockwave, dust and flash of the burst.</item>
    /// <item>The show: the 16 moving heads' beams through the haze with their gobo spots and lens flares, the decorative
    /// laser fans off the speaker stacks and their dots on the far wall, the mirror balls' pin spots and the specks they throw
    /// round the hall, the stage lamps' beams up the LED wall, the CO2 jets and the confetti at the drop, the uplights' washes
    /// on the brick, the neon's glow, the haze drifting, phone lights in the crowd.</item>
    /// </list>
    /// Every moment comes from <see cref="NativeClubShow"/> (the replicated hazard clocks and the bar grid) or the render clock;
    /// pure: the same inputs give the same quads. Allocation free.
    /// </summary>
    public sealed class NativeClubFx
    {
        public const int Capacity=3600;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        /// <summary>The volumetric beams (null: billboards).</summary>
        public NativeBeamList Beams;
        public int Telegraph,Show,Atmosphere,BeamCount;
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz,quality;private bool low;
        private static double H(int a,int b=0,int c=0)=>NativeClubLooks.Hash(a,b,c);
        private static readonly double[] Sides={-1.0,1.0},LaserYs={.15,.35,.55},Rails={-NativeClubLooks.LaserRailZ,NativeClubLooks.LaserRailZ},JetXs={-6.0,-2.5,2.5,6.0},WashXs={-25.5,25.5},Woofers={-.66,.66};
        private static double Frac(double v)=>v-Math.Floor(v);
        private static double L(int hex,int shift)=>NativeSceneryBuilder.Channel(hex,shift);
        private int Count(int full)=>low?Math.Max(1,full/3):Math.Max(1,(int)Math.Round(full*(.35+.65*quality)));

        /// <summary>Fills <see cref="Quads"/> from the show (built this frame) at the render clock, the camera and its right and up
        /// vectors, effects `quality` 0..1 and the low tier.</summary>
        public void Build(NativeClubShow s,double render,double camX,double camY,double camZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,double q,bool lowTier)
        {
            Quads.Clear();Telegraph=Show=Atmosphere=BeamCount=0;if(s==null)return;ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;quality=Math.Max(0,Math.Min(1,q));low=lowTier;
            // telegraphs claim the buffer first
            GridFx(s,render);LaserFx(s,render);DropFx(s,render);Cones(s);
            HeadBeams(s);Fans(s,render);Balls(s,render);StageLamps(s);Jets(s);Confetti(s);Washes(s,render);Haze(s,render);Phones(s,render);
        }

        // ---------------- quad helpers ----------------
        private bool Sprite(double x,double y,double z,double size,double r,double g,double b,double a,bool additive=true,int mode=1)
        {
            double h=size/2,ax=(rx+ux)*h,ay=(ry+uy)*h,az=(rz+uz)*h,bx=(rx-ux)*h,by=(ry-uy)*h,bz=(rz-uz)*h;
            return Quads.Add(x-ax,y-ay,z-az,x+bx,y+by,z+bz,x+ax,y+ay,z+az,x-bx,y-by,z-bz,r,g,b,a,mode,additive,1);
        }
        private bool Streak(double ax,double ay,double az,double bx,double by,double bz,double w0,double w1,double r,double g,double b,double a,bool additive=true,int mode=1)
        {
            double dx=bx-ax,dy=by-ay,dz=bz-az,vx=(ax+bx)/2-ex,vy=(ay+by)/2-ey,vz=(az+bz)/2-ez;
            double cx=dy*vz-dz*vy,cy=dz*vx-dx*vz,cz=dx*vy-dy*vx,l=Math.Sqrt(cx*cx+cy*cy+cz*cz);if(l<1e-6)return false;cx/=l;cy/=l;cz/=l;
            return Quads.Add(ax-cx*w0/2,ay-cy*w0/2,az-cz*w0/2,bx-cx*w1/2,by-cy*w1/2,bz-cz*w1/2,bx+cx*w1/2,by+cy*w1/2,bz+cz*w1/2,ax+cx*w0/2,ay+cy*w0/2,az+cz*w0/2,r,g,b,a,mode,additive,1);
        }
        private bool Flat(double x,double y,double z,double hx,double hz,double yaw,double r,double g,double b,double a,bool additive=true,int mode=1)
        {
            double c=Math.Cos(yaw),sn=Math.Sin(yaw),ax=c*hx,az=-sn*hx,bx=sn*hz,bz=c*hz;
            return Quads.Add(x-ax-bx,y,z-az-bz,x+ax-bx,y,z+az-bz,x+ax+bx,y,z+az+bz,x-ax+bx,y,z-az+bz,r,g,b,a,mode,additive,1);
        }
        /// <summary>An upright quad facing along ±x (`facingX`) or ±z, half width hw, from y0 to y1.</summary>
        private bool Upright(double x,double z,double y0,double y1,double hw,bool facingX,double r,double g,double b,double a,int mode=1)
        {
            if(facingX)return Quads.Add(x,y0,z-hw,x,y0,z+hw,x,y1,z+hw,x,y1,z-hw,r,g,b,a,mode,true,1);
            return Quads.Add(x-hw,y0,z,x+hw,y0,z,x+hw,y1,z,x-hw,y1,z,r,g,b,a,mode,true,1);
        }
        private void Beam(double ax,double ay,double az,double bx,double by,double bz,double r0,double r1,double r,double g,double b,double alpha,double seed,double dust=.6)
        {
            BeamCount++;
            if(Beams!=null){double k=alpha*NativeBeamList.FromBillboard;if(Beams.Add(ax,ay,az,bx,by,bz,r0,r1,r*k,g*k,b*k,dust,seed))return;}
            // billboards: three strips across the beam, soft at its edges, fading along it
            double dx=bx-ax,dy=by-ay,dz=bz-az,vx=(ax+bx)/2-ex,vy=(ay+by)/2-ey,vz=(az+bz)/2-ez,cx=dy*vz-dz*vy,cy=dz*vx-dx*vz,cz=dx*vy-dy*vx,cl=Math.Sqrt(cx*cx+cy*cy+cz*cz);if(cl<1e-6)return;cx/=cl;cy/=cl;cz/=cl;
            int strips=low?2:3;
            for(int k=0;k<strips;k++)for(int j=0;j<3;j++)
            {
                double t0=k/(double)strips,t1=(k+1)/(double)strips,w0=r0+(r1-r0)*t0,w1=r0+(r1-r0)*t1,o=(j-1)*.5,wt=Math.Exp(-o*o*2.5)*(1-.32*k);
                double px0=ax+dx*t0+cx*o*w0,py0=ay+dy*t0+cy*o*w0,pz0=az+dz*t0+cz*o*w0,px1=ax+dx*t1+cx*o*w1,py1=ay+dy*t1+cy*o*w1,pz1=az+dz*t1+cz*o*w1;
                Quads.Add(px0-cx*w0*.35,py0-cy*w0*.35,pz0-cz*w0*.35,px1-cx*w1*.35,py1-cy*w1*.35,pz1-cz*w1*.35,px1+cx*w1*.35,py1+cy*w1*.35,pz1+cz*w1*.35,px0+cx*w0*.35,py0+cy*w0*.35,pz0+cz*w0*.35,r,g,b,alpha*wt,0,true,1);
            }
        }

        // ---------------- telegraphs ----------------
        private void GridFx(NativeClubShow s,double render)
        {
            for(int k=0;k<NativeClubShow.Sections;k++)
            {
                var h=s.GridHazard[k];if(h==null)continue;var st=s.Grid[k];if(st.Phase==NativeHazardPhase.Idle)continue;
                double cx=h.center.x,cz=h.center.z,hx=h.size.x/2-.04,hz=h.size.z/2-.04,y=.03;double dr=L(NativeClubLooks.Danger,16),dg=L(NativeClubLooks.Danger,8),db=L(NativeClubLooks.Danger,0);
                double edge=st.Phase==NativeHazardPhase.Warn?.6+1.8*st.Level:st.Phase==NativeHazardPhase.Active?3.0:1.2*st.Level;
                // the section's outline, a bright red frame at the tiles' level
                Flat(cx,y,cz+hz,hx,.06,0,dr*edge,dg*edge,db*edge,.9,true,0);Flat(cx,y,cz-hz,hx,.06,0,dr*edge,dg*edge,db*edge,.9,true,0);
                Flat(cx+hx,y,cz,.06,hz,0,dr*edge,dg*edge,db*edge,.9,true,0);Flat(cx-hx,y,cz,.06,hz,0,dr*edge,dg*edge,db*edge,.9,true,0);Telegraph+=4;
                if(st.Phase==NativeHazardPhase.Warn)
                {
                    // sparks crawl up off the section's edge as it charges, more and higher toward the end
                    int n=6+(int)(10*st.Level);
                    for(int i=0;i<n;i++){double t=Frac(render*1.3+H(k,i)),u=H(k,i,2)*4-2,px=Math.Abs(u)<1?cx+u*hx:cx+Math.Sign(u)*hx,pz=Math.Abs(u)<1?cz+(H(k,i,3)<.5?-hz:hz):cz+(H(k,i,4)-.5)*2*hz;
                        Sprite(px,.05+t*(.4+1.2*st.Level),pz,.12+.1*st.Level,1,.5+.3*st.Level,.2,.8*(1-t),true);Telegraph++;}
                }
                else if(st.Phase==NativeHazardPhase.Active)
                {
                    // arcs across the live section, re-struck 12 times a second, and the crackle where they land
                    long strike=(long)Math.Floor(render*12);
                    for(int a=0;a<5;a++)
                    {
                        double x0=cx+(H(k,a,(int)strike)-.5)*2*hx,z0=cz+(H(k,a+7,(int)strike)-.5)*2*hz,x1=cx+(H(k,a+13,(int)strike)-.5)*2*hx,z1=cz+(H(k,a+17,(int)strike)-.5)*2*hz;
                        double px=x0,py=.05,pz=z0;
                        for(int seg=1;seg<=5;seg++)
                        {
                            double t=seg/5.0,nx=x0+(x1-x0)*t+(seg<5?(H(k,a*11+seg,(int)strike)-.5)*.9:0),nz=z0+(z1-z0)*t+(seg<5?(H(k,a*13+seg,(int)strike)-.5)*.9:0),ny=.05+Math.Sin(t*Math.PI)*.45;
                            Streak(px,py,pz,nx,ny,nz,.05,.05,3.2,2.6,3.4,.95,true,0);Streak(px,py,pz,nx,ny,nz,.3,.3,dr*1.6,dg*1.6,db*1.6,.35,true,1);px=nx;py=ny;pz=nz;Telegraph+=2;
                        }
                        Sprite(x1,.1,z1,.7,2.5,1.6,1.8,.6,true);Telegraph++;
                    }
                }
                else
                {
                    // the discharge: smoke off the cooling tiles
                    int n=Count(6);for(int i=0;i<n;i++){double t=1-st.Level;Sprite(cx+(H(k,i,5)-.5)*2*hx,.3+t*1.4,cz+(H(k,i,6)-.5)*2*hz,.8+t*1.2,.35,.3,.34,.25*st.Level,false);Atmosphere++;}
                }
            }
        }
        private void LaserFx(NativeClubShow s,double render)
        {
            var h=s.LaserHazard;if(h==null)return;var st=s.Laser;if(st.Phase==NativeHazardPhase.Idle)return;
            double lr=L(NativeClubLooks.LaserRed,16),lg=L(NativeClubLooks.LaserRed,8),lb=L(NativeClubLooks.LaserRed,0),z0=-NativeClubLooks.LaserRailZ+.13,z1=NativeClubLooks.LaserRailZ-.13;
            double near=h.center.x-st.Direction*h.size.x/2;
            if(st.Phase==NativeHazardPhase.Warn||st.Phase==NativeHazardPhase.Danger)
            {
                // where it will start: a thin steady red line over the start column, brighter through the danger; sparks at the slots
                double k=st.Phase==NativeHazardPhase.Danger?1:.35+.4*st.Level;double x=near+st.Direction*.5;
                Streak(x,.35,z0,x,.35,z1,.04,.04,lr*4*k,lg*4*k,lb*4*k,.8*k,true,0);Telegraph++;
                Upright(x,0,0,.7,7.5,true,lr*.8,lg*.8,lb*.8,.12*k,1);Telegraph++;
                if(st.Phase==NativeHazardPhase.Danger)foreach(double z in Rails){Sprite(x,.4,z,1.2,lr*3,lg*3,lb*3,.7,true);Telegraph++;}
                return;
            }
            if(st.Phase==NativeHazardPhase.Active&&!double.IsNaN(st.Head))
            {
                double x=Math.Max(h.center.x-h.size.x/2,Math.Min(h.center.x+h.size.x/2,st.Head));
                foreach(double y in LaserYs)
                {
                    if(Beams!=null){Beams.Add(x,y,z0,x,y,z1,.035,.035,lr*5,lg*5,lb*5,.15,y*7);BeamCount++;}
                    Streak(x,y,z0,x,y,z1,.06,.06,lr*2.4,lg*1.4,lb*1.4,1,true,0);Streak(x,y,z0,x,y,z1,.6,.6,lr*1.2,lg*1.2,lb*1.2,.45,true,1);Telegraph+=2;
                }
                // the light curtain over the beams (it reads from across the hall), its trail, the scorch line on the tiles, flares at
                // the posts and sparks thrown back where the beams leave their slots
                Upright(x,0,0,1.8,7.5,true,lr*1.2,lg*1.2,lb*1.2,.3,1);Upright(x-st.Direction*.5,0,0,1.1,7.5,true,lr*1.1,lg*1.1,lb*1.1,.18,1);Telegraph+=2;
                Flat(x-st.Direction*.3,.04,0,.45,7.5,0,lr*1.5,lg*.8,lb*.8,.7,true,1);Telegraph++;
                foreach(double z in Rails)
                {
                    Sprite(x,.4,z,1.7,lr*4,lg*2,lb*2,.85,true);Telegraph++;
                    for(int i=0;i<6;i++){double t=Frac(render*3+i/6.0);Sprite(x-st.Direction*t*1.2,.15+t*.7*(1-t)*2,z+(H(i,1)-.5)*.4,.16,3,1.5,.7,.9*(1-t),true);Telegraph++;}
                }
                return;
            }
            if(st.Phase==NativeHazardPhase.Clear){double x=h.center.x+st.Direction*h.size.x/2;Upright(x,0,0,.6,7.5,true,lr,lg,lb,.15*st.Level,1);Telegraph++;}
        }
        private void DropFx(NativeClubShow s,double render)
        {
            foreach(double side in Sides)
            {
                var h=side<0?s.DropHazardW:s.DropHazardE;if(h==null)continue;var st=side<0?s.DropW:s.DropE;
                double cx=h.a.x,cz=h.a.z,front=NativeClubLooks.SpeakerZ-.1,ar=L(NativeClubLooks.Amber,16),ag=L(NativeClubLooks.Amber,8),ab=L(NativeClubLooks.Amber,0);
                if(st.Phase==NativeFactoryHazards.Warning)
                {
                    // the build: the zone's frame glows amber and the cones throw air rings on every beat, faster toward the drop
                    double u=st.Warn;Flat(cx,.03,cz,h.size.x/2,h.size.z/2,0,ar*(.4+1.6*u),ag*(.4+1.6*u),ab*(.4+1.6*u),.22+.3*u,true,1);Telegraph++;
                    double beats=s.Beats,rate=u<.5?1:u<.85?2:4,t=Frac(beats*rate);
                    for(int k=0;k<4;k++){double y=(k+.5)*NativeClubLooks.SpeakerTop/4,rad=.4+t*(1.2+u*1.5);Ring(cx,y,front-t*1.5,rad,ar*1.5,ag*1.5,ab*1.5,.5*(1-t)*(.4+.6*u),12);}
                    int n=4+(int)(8*u);for(int i=0;i<n;i++){double tt=Frac(render*.9+H(i,(int)side)),px=cx+(H(i,2)-.5)*h.size.x,pz=cz+(H(i,3)-.5)*h.size.z;Sprite(px,.1+tt*1.2*u,pz,.5+tt,.5,.45,.5,.18*(1-tt),false);Telegraph++;}
                }
                else if(st.Phase==NativeFactoryHazards.Bursting)
                {
                    double t=1-st.U,b=st.U;Sprite(cx,2.3,front,4.5,3*b,2.8*b,3*b,.8,true);Telegraph++;
                    Flat(cx,.03,cz,h.size.x/2,h.size.z/2,0,3*b,2.6*b,2.8*b,.5,true,0);Telegraph++;
                }
                // the shockwave: rings out of the cones and a ring along the floor for 0.8 s after the burst, dust for 2 s
                double age=s.BlastAge;
                if(age<.8){double k=1-age/.8;for(int i=0;i<3;i++)Ring(cx,1.2+i*1.2,front-age*9,.8+age*7,2*k,1.9*k,2.2*k,.7*k,18);
                    for(int i=0;i<16;i++){double a=i*Math.PI/8,r0=1.2+age*10,px=cx+Math.Cos(a)*r0,pz=cz+Math.Sin(a)*r0*.7-age*3;Sprite(px,.25,pz,1+age*2,1.8*k,1.7*k,2*k,.35*k,true);}Telegraph+=19;}
                if(age<2){double k=1-age/2;int n=Count(14);for(int i=0;i<n;i++){double a=H(i,4)*Math.PI-Math.PI,r0=.8+age*(3+H(i,5)*3);Sprite(cx+Math.Cos(a)*r0,.3+age*.8,cz+Math.Sin(a)*r0*.8,1.2+age*2,.5,.45,.55,.22*k,false);Atmosphere++;}}
            }
        }
        // The woofers' glow: a soft ring on each cone, the party's cyan breathing with the kick; amber through the drop's build
        // (the telegraph: counted, never reduced) and white at the burst.
        private void Cones(NativeClubShow s)
        {
            double kick=Math.Exp(-Frac(s.Beats)*6),cab=NativeClubLooks.SpeakerTop/4,front=NativeClubLooks.SpeakerZ-.09;
            foreach(double side in Sides)
            {
                var st=side<0?s.DropW:s.DropE;bool warn=st.Phase==NativeFactoryHazards.Warning,burst=st.Phase==NativeFactoryHazards.Bursting;int c=warn?NativeClubLooks.Amber:burst?0xF4F0FF:NativeClubLooks.Cyan;
                double k=burst?2.2:warn?.7+1.1*st.Warn*(.5+.5*kick):.25+.45*kick,cx=side*(NativeClubLooks.SpeakerX0+NativeClubLooks.SpeakerX1)/2;
                for(int i=0;i<3;i++)foreach(double dx in Woofers)
                {
                    Sprite(cx+dx,(i+.5)*cab,front,1.15,L(c,16)*k,L(c,8)*k,L(c,0)*k,.6,true);
                    if(warn||burst)Telegraph++;else Show++;
                }
            }
        }
        // A ring of short streaks round (x, y, z) in the plane facing ±z.
        private void Ring(double x,double y,double z,double radius,double r,double g,double b,double a,int segs)
        {
            if(!(a>.004))return;
            for(int i=0;i<segs;i++){double a0=i*2*Math.PI/segs,a1=(i+1)*2*Math.PI/segs;Streak(x+Math.Cos(a0)*radius,y+Math.Sin(a0)*radius,z,x+Math.Cos(a1)*radius,y+Math.Sin(a1)*radius,z,.08,.08,r,g,b,a,true,1);}
        }

        // ---------------- the show ----------------
        private void HeadBeams(NativeClubShow s)
        {
            for(int k=0;k<NativeClubLooks.Heads;k++)
            {
                double p=s.HeadPower[k];if(p<.02)continue;double hx=s.HeadAt[k*3],hy=s.HeadAt[k*3+1],hz=s.HeadAt[k*3+2],tx=s.HeadAim[k*3],ty=s.HeadAim[k*3+1],tz=s.HeadAim[k*3+2];
                double dx=tx-hx,dy=ty-hy,dz=tz-hz,len=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(len<.5)continue;
                // the beam reaches the floor (or 14 m out when it points up into the roof)
                double reach=dy<-.05?Math.Min(26,-hy/(dy/len)):14,bx=hx+dx/len*reach,by=hy+dy/len*reach,bz=hz+dz/len*reach;
                double r=s.HeadColor[k*3],g=s.HeadColor[k*3+1],b=s.HeadColor[k*3+2],spread=s.HeadSpread[k]*reach/10*.55;
                double sx=hx+dx/len*.42,sy=hy+dy/len*.42,sz=hz+dz/len*.42;
                Beam(sx,sy,sz,bx,by,bz,.1,Math.Max(.2,spread),r,g,b,.22*p,k*1.7,.65);Show++;
                if(by<.2){Flat(bx,.035,bz,spread*1.1,spread*1.1,0,r*1.4*p,g*1.4*p,b*1.4*p,.55,true,1);Show++;}
                Sprite(sx,sy,sz,.55+.4*p,r*3*p+.3,g*3*p+.3,b*3*p+.3,.75,true);Show++;
            }
        }
        private void Fans(NativeClubShow s,double render)
        {
            // thin beams fanned off each stack's top, high over the hall (8 to 11.5 m on the far wall): over every head, never across a sight line
            if(s.FanOn<.02)return;int n=low?5:8;double on=s.FanOn;double r=s.FanColor[0],g=s.FanColor[1],b=s.FanColor[2];
            foreach(double side in Sides)
            {
                double ox=side*9.4,oy=NativeClubLooks.SpeakerTop+.18,oz=15.6;
                for(int i=0;i<n;i++)
                {
                    double u=(i/(n-1.0)-.5)*2,tx=side*-3+u*18*s.FanSpread,ty=9.6+u*s.FanSweep*1.6+Math.Sin(render*1.3+i*.6+side)*.35+s.FanSweep*2,tz=-19.4;
                    if(Beams!=null){Beams.Add(ox,oy,oz,tx,ty,tz,.008,.022,r*1.1*on,g*1.1*on,b*1.1*on,.2,i+side*31);BeamCount++;}
                    else{Streak(ox,oy,oz,tx,ty,tz,.018,.03,r*1.6*on,g*1.6*on,b*1.6*on,.45,true,0);}
                    Sprite(tx,ty,tz+.05,.25,r*2.4*on,g*2.4*on,b*2.4*on,.7,true);Show+=2;
                }
                Sprite(ox,oy,oz,.6,r*2.4*on,g*2.4*on,b*2.4*on,.7,true);Show++;
            }
        }
        private void Balls(NativeClubShow s,double render)
        {
            double turn=NativeClubRig.BallTurn(render);int n=low?18:36;double warm=1;
            foreach(double side in Sides)
            {
                double bx=side*NativeClubLooks.BallX,by=NativeClubLooks.BallY,bz=NativeClubLooks.BallZ;
                // the pin spot from the truss corner onto the ball
                double px=side*NativeClubLooks.TrussX,py=NativeClubLooks.TrussY-.4,pz=-NativeClubLooks.TrussZ;Beam(px,py,pz,bx,by,bz,.05,.5,1,.95,.85,.18,side*5,.4);Show++;
                double ix=bx-px,iy=by-py,iz=bz-pz,il=Math.Sqrt(ix*ix+iy*iy+iz*iz);ix/=il;iy/=il;iz/=il;
                for(int k=0;k<n;k++)
                {
                    // a facet's normal (a fixed scatter turned with the ball): the pin spot reflected off it lands on the hall
                    double lat=(H(k,1,(int)side)-.5)*Math.PI*.9,lon=H(k,2,(int)side)*2*Math.PI+turn*side;double nx=Math.Cos(lat)*Math.Sin(lon),ny=Math.Sin(lat),nz=Math.Cos(lat)*Math.Cos(lon);
                    double d=ix*nx+iy*ny+iz*nz;if(d>=0)continue;double ox=ix-2*d*nx,oy=iy-2*d*ny,oz=iz-2*d*nz;
                    // the first of the floor, the ceiling (12 m) and the four walls the reflection meets
                    double t=1e9;int plane=-1;
                    if(oy<-1e-4){double tt=-by/oy;if(tt<t){t=tt;plane=0;}}if(oy>1e-4){double tt=(12-by)/oy;if(tt<t){t=tt;plane=1;}}
                    if(ox>1e-4){double tt=(25.55-bx)/ox;if(tt<t){t=tt;plane=2;}}if(ox<-1e-4){double tt=(-25.55-bx)/ox;if(tt<t){t=tt;plane=3;}}
                    if(oz>1e-4){double tt=(19.55-bz)/oz;if(tt<t){t=tt;plane=4;}}if(oz<-1e-4){double tt=(-19.55-bz)/oz;if(tt<t){t=tt;plane=5;}}
                    if(plane<0||t>40)continue;double hx=bx+ox*t,hy=by+oy*t,hz=bz+oz*t,size=.12+t*.012,a=.5*warm*(1-t/45);
                    if(plane==0)Flat(hx,.04,hz,size,size,0,2.2,2.1,1.9,a,true,1);else if(plane==1)Flat(hx,11.95,hz,size,size,0,2.2,2.1,1.9,a,true,1);
                    else if(plane<=3)Upright(hx,hz,hy-size,hy+size,size,true,2.2,2.1,1.9,a,1);else Upright(hx,hz,hy-size,hy+size,size,false,2.2,2.1,1.9,a,1);Show++;
                }
                // the ball's own glints
                for(int k=0;k<4;k++){double t=Frac(render*.7+k*.25+side*.1),a=Math.Sin(t*Math.PI);Sprite(bx+(H(k,8)-.5)*.8,by+(H(k,9)-.5)*.8,bz+(H(k,10)-.5)*.8,.35*a,3,3,3,.9*a,true);Show++;}
            }
        }
        private void StageLamps(NativeClubShow s)
        {
            int[] pal=NativeClubLooks.Party;int bar=(int)(((long)Math.Floor(s.Beats/4))%7+7)%7;double kick=Math.Exp(-Frac(s.Beats)*5);
            int i=0;for(double x=-7;x<7.5;x+=2,i++)
            {
                int c=pal[(bar+i)%pal.Length];double k=.35+.4*kick;if(s.Warned)k*=.5;
                Beam(x,1.15,13.28,x*.6,9.5,19.4,.08,.9,L(c,16),L(c,8),L(c,0),.18*k,i*3.1,.5);Show++;
            }
        }
        private void Jets(NativeClubShow s)
        {
            double age=s.BlastAge;if(age>1.6)return;double k=1-age/1.6;
            foreach(double x in JetXs){int n=Count(8);for(int i=0;i<n;i++){double t=Frac(age*1.6+i/(double)n);Sprite(x+(H(i,(int)x)-.5)*.4*t,1.1+t*3.4*k,12.9-t*.4,.4+t*1.6,1.2,1.2,1.3,.35*k*(1-t),false);Show++;}}
        }
        private void Confetti(NativeClubShow s)
        {
            double age=s.BlastAge;if(age>7)return;int n=Count(220);int[] pal=NativeClubLooks.Party;
            for(int i=0;i<n;i++)
            {
                double h1=H(i,1),h2=H(i,2),h3=H(i,3);double vx=(h1-.5)*9,vz=(h2-.5)*8,t=age,x=(h3<.5?-4:4)+vx*Math.Min(t,1.0)+Math.Sin(t*3+i)*.45,z=(h3<.25||h3>.75?-3:3)+vz*Math.Min(t,1.0),y=9.4+1.6*Math.Min(t,.4)-Math.Max(0,t-.4)*(1.7+.5*H(i,4));
                if(y<.02)y=.02;double spin=t*(4+h2*6)+i,a=Math.Abs(Math.Cos(spin))*.06+.02;int c=pal[i%pal.Length];double fade=t>5.5?1-(t-5.5)/1.5:1;
                a*=1.8;Quads.Add(x-a,y,z-.08,x+a,y+.05,z-.08,x+a,y+.05,z+.08,x-a,y,z+.08,L(c,16)*1.3,L(c,8)*1.3,L(c,0)*1.3,.95*fade,0,true,1);Show++;
            }
        }
        private void Washes(NativeClubShow s,double render)
        {
            int[] pal=NativeClubLooks.Party;int bar=(int)(((long)Math.Floor(s.Beats/4))%7+7)%7;double k=.5+.3*Math.Sin(s.Beats*Math.PI/4);
            for(int i=0;i<10;i++){double z=-17+i*3.4;int c=pal[(bar+i/3)%pal.Length];foreach(double x in WashXs)Upright(x,z,0,4.6,1.4,true,L(c,16)*k*1.4,L(c,8)*k*1.4,L(c,0)*k*1.4,.3,1);Atmosphere+=2;}
            Sprite(0,6.0,-19.45,9,L(NativeClubLooks.Magenta,16)*.5,L(NativeClubLooks.Magenta,8)*.5,L(NativeClubLooks.Magenta,0)*.5,.35,true);Atmosphere++;
        }
        private void Haze(NativeClubShow s,double render)
        {
            int n=Count(24);double fr=0,fg=0,fb=0;for(int t=0;t<NativeClubLooks.Tiles;t+=7){fr+=s.Floor[t*3];fg+=s.Floor[t*3+1];fb+=s.Floor[t*3+2];}double m=7.0/NativeClubLooks.Tiles;fr*=m;fg*=m;fb*=m;
            for(int i=0;i<n;i++)
            {
                double h1=H(i,21),h2=H(i,22),x=-18+Frac(h1+render*.004*(1+h2))*36,z=-14+Frac(h2+render*.003)*28,y=1.2+H(i,23)*6;
                Sprite(x,y,z,7+H(i,24)*5,.08+fr*.3,.06+fg*.3,.1+fb*.3,.06,false);Atmosphere++;
            }
        }
        private void Phones(NativeClubShow s,double render)
        {
            int n=low?10:24;
            for(int i=0;i<n;i++){int k=(int)(H(i,31)*64);NativeClubRig.CrowdAt(k,out double x,out double y,out double z,out _);double t=Frac(render*.13+H(i,32)),a=t<.7?1:0;if(a<=0)continue;Sprite(x,y+1.9,z,.12,2,2,2.2,.85,true);Atmosphere++;}
        }
    }
}
