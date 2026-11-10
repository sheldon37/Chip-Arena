using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Top Dog Emporium's camera-facing effects for one frame (docs/arenas/EMPORIUM.md "Look") into one <see cref="NativeFxQuads"/>
    /// (Runtime draws it as one NativeFxDraw batch) and, while the look's volumetric beams are on above the low tier, the frame's
    /// <see cref="NativeBeamList"/> (null: their billboard shafts):
    /// <list type="bullet">
    /// <item>Telegraphs, never reduced (ARENA_PROPOSAL.md 2.1 rule 4), from the replicated state only: the wiener ovens steaming and
    /// glowing at the portal the wiener will come from (amber through the warning, sparking and red through the danger), the wiener's
    /// steam trail, speed streaks and sizzle sparks while it runs; the bun presses' pulsing red zone and falling flour, the impact's
    /// flour ring, shock ring and flying condiment splats; the griddles' glow, shimmer, sizzle sparks, grease pops and smoke; the
    /// geysers bubbling through the pumps, the burst's spray and droplets, the splat left on the floor.</item>
    /// <item>Atmosphere (scaled by the effects quality, a third on the low tier): the golden-hour shafts through the glazed roof with
    /// dust motes drifting in them, the ovens' chimney plumes, the soda fountain's fizz, steam off the fryers, the neon and lamp
    /// halos, the big sign's glow.</item>
    /// </list>
    /// Pure: the same clock, camera, quality and map give the same quads. Allocation free after <see cref="Prepare"/> (per map).
    /// </summary>
    public sealed class NativeEmporiumFx
    {
        public const int Capacity=3600;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        public NativeBeamList Beams;
        public int Telegraph,Atmosphere,Lights;
        private string prepared;private bool active;
        private readonly List<double> lamps=new List<double>();private double[] lamp=Array.Empty<double>();private int lampCount;
        private readonly List<double> chimneys=new List<double>();private double[] chimney=Array.Empty<double>();
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz,quality;private bool low;
        private static double H(int a,int b=0,int c=0)=>NativeEmporiumLooks.Hash(a,b,c);
        private static double Frac(double v)=>v-Math.Floor(v);
        private static double L(int hex,int shift)=>NativeSceneryBuilder.Channel(hex,shift);

        /// <summary>The map's fixed lamps and chimneys, once per map (allocates; nothing for another map).</summary>
        public void Prepare(ContentMap map)
        {
            if(map?.id==prepared)return;prepared=map?.id;active=NativeEmporiumLooks.Active(map?.id);lamps.Clear();chimneys.Clear();if(!active){lamp=chimney=Array.Empty<double>();lampCount=0;return;}
            void Lamp(double x,double y,double z,int hex,double size,double blink=0){lamps.Add(x);lamps.Add(y);lamps.Add(z);lamps.Add(hex);lamps.Add(size);lamps.Add(blink);}
            foreach(double z in new[]{-17.6,17.6})for(double x=-25;x<=25.01;x+=5)Lamp(x,7.8,z,0xFFD8A0,1.3);
            foreach(double z in new[]{-21.2,21.2})for(double x=-10;x<=10.01;x+=5)Lamp(x,7.4,z,0xFFD8A0,1.2);
            foreach(int s in new[]{-1,1}){Lamp(0,5.4,s*22.85,NativeEmporiumLooks.NeonPink,9.0);Lamp(s*28.85,5.2,0,s<0?NativeEmporiumLooks.KetchupHex:NativeEmporiumLooks.NeonLemon,5.0);Lamp(s*5.6,5.3,s*19.6,NativeEmporiumLooks.NeonPink,2.6);}
            foreach(var b in map.blocks??Array.Empty<ContentBlock>())
            {
                if(b?.id==null)continue;
                if(b.id.StartsWith("emporium-oven-",StringComparison.Ordinal)){double fx=b.pos.x<0?b.pos.x+b.size.x/2+.1:b.pos.x-b.size.x/2-.1;Lamp(fx,2.4,b.pos.z,0xFF8A3A,2.2);chimneys.Add(b.pos.x);chimneys.Add(b.pos.y+b.size.y/2+1.5);chimneys.Add(b.pos.z+1.35);}
                if(b.id.IndexOf("-jukebox-",StringComparison.Ordinal)>=0)Lamp(b.pos.x,1.7,b.pos.z+(b.pos.z>0?-.7:.7),0xFFC070,1.6);
                if(b.id.IndexOf("-soda-",StringComparison.Ordinal)>=0)Lamp(b.pos.x,1.9,b.pos.z+(b.pos.z>0?-.85:.85),NativeEmporiumLooks.NeonCyan,2.0);
                if(b.id.IndexOf("-fryer-",StringComparison.Ordinal)>=0){double fx=b.pos.x<0?b.pos.x+b.size.x/2+.25:b.pos.x-b.size.x/2-.25;Lamp(fx,3.3,b.pos.z,NativeEmporiumLooks.NeonLemon,2.4);}
            }
            lamp=lamps.ToArray();lampCount=lamp.Length/6;chimney=chimneys.ToArray();
        }

        /// <summary>Fills <see cref="Quads"/> (prepared): the hazards and the replicated state (round, phase, FightAt) at the field clock
        /// `now`, the render clock `render` for the atmosphere, the camera and its right and up vectors, effects `quality` (0..1), the
        /// low tier. Telegraphs ignore the quality and the tier.</summary>
        public void Build(MapHazard[] hazards,int round,NativePhase phase,double fightAt,double now,double render,double camX,double camY,double camZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,double q,bool lowTier)
        {
            Quads.Clear();Telegraph=Atmosphere=Lights=0;if(!active)return;ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;quality=Math.Max(0,Math.Min(1,q));low=lowTier;
            double clock=NativeFactoryHazards.Clock(phase,fightAt,now);
            for(int i=0;i<(hazards?.Length??0);i++)
            {
                var h=hazards[i];if(h==null)continue;
                if(h.kind=="maglev")LaneFx(h,NativeCyberCityHazards.At(h,phase,round,fightAt,now),now-fightAt,i);
                else if(h.kind==NativeFactoryHazards.Press)PressFx(h,round,clock,i);
                else if(h.kind==NativeWardZeroHazards.Burner)GriddleFx(h,NativeWardZeroHazards.At(h,phase,round,fightAt,now),now-fightAt,i);
                else if(h.kind==NativeFactoryHazards.Vent)GeyserFx(h,round,clock,i);
            }
            Shafts(render);Motes(render);Plumes(render);SignGlow(render);LampHalos(render);
        }
        private int Count(int full)=>low?Math.Max(1,full/3):Math.Max(1,(int)Math.Round(full*(.35+.65*quality)));

        // ---------------- quad helpers ----------------
        private bool Sprite(double x,double y,double z,double size,double r,double g,double b,double a,bool additive,int mode=1)
        {
            double h=size/2,ax=(rx+ux)*h,ay=(ry+uy)*h,az=(rz+uz)*h,bx=(rx-ux)*h,by=(ry-uy)*h,bz=(rz-uz)*h;
            return Quads.Add(x-ax,y-ay,z-az,x+bx,y+by,z+bz,x+ax,y+ay,z+az,x-bx,y-by,z-bz,r,g,b,a,mode,additive,1);
        }
        private bool Hex(double x,double y,double z,double size,int hex,double a,bool additive,double gain=1)=>Sprite(x,y,z,size,L(hex,16)*gain,L(hex,8)*gain,L(hex,0)*gain,a,additive);
        private bool Streak(double ax,double ay,double az,double bx,double by,double bz,double w0,double w1,int hex,double a,bool additive,double gain=1)
        {
            double dx=bx-ax,dy=by-ay,dz=bz-az,vx=(ax+bx)/2-ex,vy=(ay+by)/2-ey,vz=(az+bz)/2-ez;
            double cx=dy*vz-dz*vy,cy=dz*vx-dx*vz,cz=dx*vy-dy*vx,l=Math.Sqrt(cx*cx+cy*cy+cz*cz);if(l<1e-6)return false;cx/=l;cy/=l;cz/=l;
            double r=L(hex,16)*gain,g=L(hex,8)*gain,b=L(hex,0)*gain;
            return Quads.Add(ax-cx*w0/2,ay-cy*w0/2,az-cz*w0/2,bx-cx*w1/2,by-cy*w1/2,bz-cz*w1/2,bx+cx*w1/2,by+cy*w1/2,bz+cz*w1/2,ax+cx*w0/2,ay+cy*w0/2,az+cz*w0/2,r,g,b,a,1,additive,1);
        }
        private bool Flat(double x,double y,double z,double hx,double hz,double yaw,int hex,double a,bool additive,int mode=1,double gain=1)
        {
            double c=Math.Cos(yaw),s=Math.Sin(yaw),ax=c*hx,az=-s*hx,bx=s*hz,bz=c*hz;double r=L(hex,16)*gain,g=L(hex,8)*gain,b=L(hex,0)*gain;
            return Quads.Add(x-ax-bx,y,z-az-bz,x+ax-bx,y,z+az-bz,x+ax+bx,y,z+az+bz,x-ax+bx,y,z-az+bz,r,g,b,a,mode,additive,1);
        }
        private void Beam(double ax,double ay,double az,double bx,double by,double bz,double r0,double r1,int hex,double gain,double alpha,double seed)
        {
            double r=L(hex,16)*gain,g=L(hex,8)*gain,b=L(hex,0)*gain;
            if(Beams!=null){Beams.Add(ax,ay,az,bx,by,bz,r0,r1,r*alpha*NativeBeamList.FromBillboard,g*alpha*NativeBeamList.FromBillboard,b*alpha*NativeBeamList.FromBillboard,.6,seed);return;}
            double dx=bx-ax,dy=by-ay,dz=bz-az,vx=(ax+bx)/2-ex,vy=(ay+by)/2-ey,vz=(az+bz)/2-ez,cx=dy*vz-dz*vy,cy=dz*vx-dx*vz,cz=dx*vy-dy*vx,cl=Math.Sqrt(cx*cx+cy*cy+cz*cz);if(cl<1e-6)return;cx/=cl;cy/=cl;cz/=cl;
            for(int k=0;k<3;k++)for(int j=0;j<5;j++)
            {
                double t0=k/3.0,t1=(k+1)/3.0,w0=r0+(r1-r0)*t0,w1=r0+(r1-r0)*t1,o=(j-2)*.4,wt=Math.Exp(-o*o*2.2)*(1-.25*k);
                double px0=ax+dx*t0+cx*o*w0,py0=ay+dy*t0+cy*o*w0,pz0=az+dz*t0+cz*o*w0,px1=ax+dx*t1+cx*o*w1,py1=ay+dy*t1+cy*o*w1,pz1=az+dz*t1+cz*o*w1;
                Quads.Add(px0-cx*w0*.2,py0-cy*w0*.2,pz0-cz*w0*.2,px1-cx*w1*.2,py1-cy*w1*.2,pz1-cz*w1*.2,px1+cx*w1*.2,py1+cy*w1*.2,pz1+cz*w1*.2,px0+cx*w0*.2,py0+cy*w0*.2,pz0+cz*w0*.2,r,g,b,alpha*wt,0,true,1);
            }
        }

        // ---------------- telegraphs ----------------
        private void LaneFx(MapHazard h,in NativeHazardState s,double t,int index)
        {
            double cz=h.center.z,hw=h.size.z/2,span=h.size.x,r=NativeEmporiumRig.Radius(h);int d=s.Direction==0?1:s.Direction;double near=h.center.x-d*span/2;
            if(s.Phase==NativeHazardPhase.Warn||s.Phase==NativeHazardPhase.Danger)
            {
                bool danger=s.Phase==NativeHazardPhase.Danger;int col=danger?NativeEmporiumLooks.Danger:NativeEmporiumLooks.Amber;double pulse=.6+.4*Math.Sin(t*(danger?18:7));
                Hex(near+d*.3,1.2,cz,4.2*(.8+.2*pulse),col,(danger?.55:.35)*pulse,true);Telegraph++;
                Flat(near+d*2.0,.03,cz,2.0,hw,0,col,(danger?.4:.22)*pulse,true,0);Telegraph++;
                int n=danger?10:5;for(int i=0;i<n;i++){double f=Frac(t*(danger?1.6:.9)+i/(double)n),py=2.0+f*2.6,px=near+d*(.4+f*1.2);Hex(px,py,cz+(H(index,i)-.5)*hw*1.6,.8+f*1.8,0xF2EEE8,.45*(1-f),false);Telegraph++;}
                if(danger)for(int i=0;i<8;i++){double f=Frac(t*3.1+H(index,i,4)),pz=cz+(H(index,i,5)-.5)*hw*1.8;Streak(near,.2+H(index,i,6)*1.8,pz,near+d*(.6+f*1.8),.3+H(index,i,6)*1.8+f*.6,pz,.05,.02,0xFFC060,.9*(1-f),true,1.6);Telegraph++;}
            }
            else if(s.Phase==NativeHazardPhase.Active||s.Phase==NativeHazardPhase.Clear)
            {
                double c=NativeEmporiumRig.Centre(h,s);if(double.IsNaN(c))return;double k=s.Phase==NativeHazardPhase.Active?1:s.Level;double x0=h.center.x-span/2,x1=h.center.x+span/2;
                if(s.Phase==NativeHazardPhase.Active&&c>x0-1&&c<x1+1)
                {
                    Flat(c,.04,cz,r*1.4,hw*.9,0,0xFF6A20,.32,true,1);Telegraph++;
                    for(int i=0;i<12;i++){double f=Frac(t*4+H(index,i)),pz=cz+(H(index,i,1)-.5)*hw*1.7;Sprite(c-d*f*.8,.05+f*.5,pz,.09,1,.7,.25,.9*(1-f),true);Telegraph++;}
                    for(int i=0;i<6;i++){double pz=cz+(i-2.5)*hw*.3,len=2.2+H(index,i,2)*1.6;Streak(c-d*r,r*(.4+.25*(i&1)),pz,c-d*(r+len),r*(.4+.25*(i&1)),pz,.08,.01,0xFFF2D8,.5,true);Telegraph++;}
                }
                int puffs=Count(14);for(int i=0;i<puffs;i++){double back=(i+.5)*1.6,px=c-d*(r+back);if(px<x0||px>x1)continue;double age=back/Math.Max(1,h.speed),f=Math.Min(1,age/1.8);Hex(px,.4+f*1.4,cz+(H(index,i,3)-.5)*hw,1.0+f*2.2,0xEEE4DA,.32*k*(1-f),false);Atmosphere++;}
            }
        }
        private void PressFx(MapHazard h,int round,double clock,int index)
        {
            var st=NativeFactoryHazards.State(h,round,clock);double x=h.a.x,z=h.a.z,hx=h.size.x/2,hz=h.size.z/2;
            if(st.Phase==NativeFactoryHazards.Warning||st.Phase==NativeFactoryHazards.Slamming)
            {
                bool on=st.Phase==NativeFactoryHazards.Slamming||NativeFactoryRig.Strobe(clock,8);Flat(x,.03,z,hx+.3,hz+.3,0,NativeEmporiumLooks.Danger,on?.42:.16,true,0);Telegraph++;
                for(int i=0;i<10;i++){double f=Frac(clock*1.3+H(index,i));double py=st.LoadY-f*st.LoadY;Hex(x+(H(index,i,1)-.5)*hx*1.6,py,z+(H(index,i,2)-.5)*hz*1.6,.12,0xFFF4E0,.7*(1-f)*st.Warn,false);Telegraph++;}
            }
            // the impact: a flour ring, a shock ring and the condiment splats flying out from under the bun (1.2 s)
            double open=Math.Max(0,h.period-h.warn-h.rise-h.dwell-h.travel);
            if(NativeFactoryHazards.Cycle(h,clock,out _,out double u))
            {
                double since=u-open-h.warn-h.rise;
                if(since>=0&&since<1.2)
                {
                    double f=since/1.2;int n=Count(20);
                    for(int i=0;i<n;i++){double a=i*2*Math.PI/n+H(index,i,3),rr=1.6+f*(2.4+H(index,i,4)*1.6);Hex(x+Math.Cos(a)*rr,.3+f*1.0,z+Math.Sin(a)*rr,1.0+f*2.2,0xF6EAD2,.5*(1-f)*(1-f),false);Atmosphere++;}
                    Flat(x,.05,z,1.8+f*5,1.8+f*5,0,0xFFF6E0,.5*(1-f),true,1);Telegraph++;
                    for(int i=0;i<14;i++){double a=H(index,i,8)*2*Math.PI,v=4+H(index,i,9)*4,tt=since,px=x+Math.Cos(a)*(1.5+v*tt),pz=z+Math.Sin(a)*(1.5+v*tt),py=.3+5*tt-11*tt*tt;if(py<.03)py=.03;
                        Hex(px,py,pz,.26+.12*H(index,i,10),(i&1)==0?NativeEmporiumLooks.KetchupHex:NativeEmporiumLooks.MustardHex,.95*(1-f*.6),false);Telegraph++;}
                }
                if(st.Phase==NativeFactoryHazards.Lifting){for(int i=0;i<6;i++){double ff=Frac(clock*1.8+H(index,i,12));Hex(x+(H(index,i,13)-.5)*hx*1.5,st.LoadY*(1-ff),z+(H(index,i,14)-.5)*hz*1.5,.1,0xE9A85A,.8*(1-ff),false);Atmosphere++;}}
            }
        }
        private void GriddleFx(MapHazard h,in NativeHazardState s,double t,int index)
        {
            foreach(var zn in h.zones??Array.Empty<MapVolume>())
            {
                double y=zn.center.y-zn.size.y/2,x=zn.center.x,z=zn.center.z,hx=zn.size.x/2,hz=zn.size.z/2;int zi=(int)(x*7+z*13);
                if(s.Phase==NativeHazardPhase.Warn)
                {
                    Flat(x,y+.03,z,hx,hz,0,0xFF5A10,.32*s.Level*(.8+.2*Math.Sin(t*14)),true,0);Telegraph++;
                    for(int i=0;i<5;i++){double f=Frac(t*1.2+i/5.0);Hex(x+(H(zi,i)-.5)*hx*1.6,y+.3+f*1.6,z+(H(zi,i,1)-.5)*hz*1.6,1.0+f,0xFFB070,.07*s.Level*(1-f),true);Telegraph++;}
                }
                else if(s.Phase==NativeHazardPhase.Active)
                {
                    Flat(x,y+.04,z,hx+.1,hz+.1,0,0xFF8A30,.45,true,0);Telegraph++;
                    for(int i=0;i<16;i++){double f=Frac(t*3.3+H(zi,i,2)),a=H(zi,i,3)*2*Math.PI,v=1.2+H(zi,i,4)*1.4,px=x+(H(zi,i,5)-.5)*hx*1.8+Math.Cos(a)*v*f*.4,pz=z+(H(zi,i,6)-.5)*hz*1.8+Math.Sin(a)*v*f*.4,py=y+.05+f*(1.4-f)*1.2;
                        Sprite(px,py,pz,.07,1,.75,.3,.95*(1-f),true);Telegraph++;}
                    for(int i=0;i<4;i++){double f=Frac(t*.8+H(zi,i,7));Hex(x+(H(zi,i,8)-.5)*hx,y+.5+f*3.2,z+(H(zi,i,9)-.5)*hz,1.2+f*2.4,0x6A5A50,.3*(1-f),false);Telegraph++;}
                    for(int i=0;i<3;i++){double f=Frac(t*2.1+H(zi,i,11));if(f>.3)continue;Hex(x+(H(zi,i,12)-.5)*hx*1.4,y+.12,z+(H(zi,i,13)-.5)*hz*1.4,.5*Math.Sin(f/.3*Math.PI),0xFFE080,.8,true);Telegraph++;}
                }
                else if(s.Phase==NativeHazardPhase.Clear){for(int i=0;i<3;i++){double f=Frac(t*.6+H(zi,i,14));Hex(x+(H(zi,i,15)-.5)*hx,y+.4+f*2.6,z+(H(zi,i,16)-.5)*hz,1+f*2,0x7A6A60,.26*s.Level*(1-f),false);Telegraph++;}}
                else if(H(zi,(int)Math.Floor(t*.5))<.35){double f=Frac(t*.5);Hex(x,y+.3+f*1.5,z,.7+f,0xB8ACA0,.08*(1-f),false);Atmosphere++;}
            }
        }
        private void GeyserFx(MapHazard h,int round,double clock,int index)
        {
            var st=NativeFactoryHazards.State(h,round,clock);double x=h.a.x,y=h.a.y,z=h.a.z;int sauce=NativeEmporiumLooks.KetchupSide(x)?NativeEmporiumLooks.KetchupHex:NativeEmporiumLooks.MustardHex;
            if(st.Phase==NativeFactoryHazards.Warning)
            {
                for(int i=0;i<6;i++){double f=Frac(clock*2.4+i/6.0),sw=Math.Sin(f*Math.PI);Hex(x+(H(index,i)-.5)*.9,y+.08+f*.35,z+(H(index,i,1)-.5)*.9,.18+.35*sw*st.Warn,sauce,.95*sw,false);Telegraph++;}
                Flat(x,y+.06,z,.9,.9,0,sauce,.3+.3*st.Warn,true,1,1.2);Telegraph++;
            }
            else if(st.Phase==NativeFactoryHazards.Bursting)
            {
                double top=NativeEmporiumRig.Column(st);
                for(int i=0;i<18;i++){double f=Frac(clock*2.2+H(index,i,2));Hex(x+(H(index,i,3)-.5)*.9,y+f*top,z+(H(index,i,4)-.5)*.9,.5+.4*H(index,i,5),sauce,.9*(1-f*.5),false);Telegraph++;}
                for(int i=0;i<16;i++){double f=Frac(clock*1.1+H(index,i,6)),a=H(index,i,7)*2*Math.PI,v=1.6+H(index,i,8)*2.4,tt=f*1.1,py=top+3*tt-11*tt*tt;if(py<.02)continue;
                    Hex(x+Math.Cos(a)*v*tt,py,z+Math.Sin(a)*v*tt,.2+.1*H(index,i,9),sauce,.95,false);Telegraph++;}
                Hex(x,y+top+.2,z,2.2,sauce,.6*st.U,false);Hex(x,y+top*.5,z,3.2,0xFFFFFF,.12*st.U,true);Telegraph++;
            }
            // the splat left on the floor after a burst: spreads in the burst's first second, fades over four
            if(NativeFactoryHazards.Cycle(h,clock,out _,out double u))
            {
                double quiet=Math.Max(0,h.period-h.warn-h.dwell),since=u-quiet-h.warn;
                if(since>=0&&since<5){double spread=Math.Min(1,since/.8),fade=since<1?1:1-(since-1)/4;
                    for(int i=0;i<7;i++){double a=i*2*Math.PI/7+H(index,i,20),rr=(.6+H(index,i,21)*.9)*spread;Flat(x+Math.Cos(a)*rr*1.3,.012+.001*i,z+Math.Sin(a)*rr*1.3,.35+.3*H(index,i,22),.3+.3*H(index,i,23),a,sauce,.85*fade,false,1);Atmosphere++;}
                    Flat(x,.011,z,1.3*spread,1.3*spread,0,sauce,.8*fade,false,1);Atmosphere++;}
            }
        }

        // ---------------- atmosphere ----------------
        /// <summary>The golden-hour shafts through the glazed roof: from the roof down the sun's ray, across the hall.</summary>
        private void Shafts(double t)
        {
            NativeEmporiumLooks.SunRay(out double dx,out double dy,out double dz);int n=low?4:Count(7);
            for(int i=0;i<n;i++)
            {
                double px=-20+i*40.0/Math.Max(1,n-1)+Math.Sin(t*.05+i)*.6,pz=-6+H(501,i)*12,py=17.0,len=(py-.2)/-dy;double bx=px+dx*len,bz=pz+dz*len;
                double w=1.4+H(501,i,1)*1.6,flick=.85+.15*Math.Sin(t*.4+i*1.7);
                Beam(px,py,pz,bx,.2,bz,w*.8,w*1.3,0xFFC27A,1.0,.032*flick,i);Lights++;
            }
        }
        private void Motes(double t)
        {
            int n=Count(70);const double box=12;
            for(int i=0;i<n;i++)
            {
                double fx=Frac(H(502,i)+t*.011),fy=Frac(H(502,i,1)+t*.017),fz=Frac(H(502,i,2)-t*.008);double x=ex+(fx-.5)*2*box,y=Math.Max(.3,ey-2.5+fy*7),z=ez+(fz-.5)*2*box;
                double d=Math.Sqrt((x-ex)*(x-ex)+(y-ey)*(y-ey)+(z-ez)*(z-ez));if(d<1.2)continue;double tw=.5+.5*Math.Sin(t*1.3+i);Hex(x,y,z,.05+.03*H(502,i,3),0xFFE6B0,.55*tw*Math.Min(1,(box-d)/4),true);Atmosphere++;
            }
        }
        private void Plumes(double t)
        {
            for(int c=0;c+2<chimney.Length;c+=3)
            {
                double x=chimney[c],y=chimney[c+1],z=chimney[c+2];int n=Count(6);
                for(int i=0;i<n;i++){double f=Frac(t*.32+i/(double)n);Hex(x+f*1.6+Math.Sin(t+i)*.3,y+f*4.5,z+f*.6,.8+f*2.6,0xF2ECE6,.3*(1-f),false);Atmosphere++;}
            }
            // the soda fountains' fizz and the fryers' steam
            for(int s=-1;s<=1;s+=2)
            {
                int n=Count(10);for(int i=0;i<n;i++){double f=Frac(t*.55+H(503,i,s+2));Hex(s*15.7+(H(503,i,s+5)-.5)*1.6,1.4+f*1.4,s*12.75+(H(503,i,9)-.5)*.3,.06,0xDFF8FF,.7*(1-f),true);Atmosphere++;}
                int m=Count(5);for(int i=0;i<m;i++){double f=Frac(t*.4+i/(double)m);Hex(s*-18.6+(H(504,i)-.5)*.6,1.3+f*2.4,(H(504,i,1)-.5)*4.6,.6+f*1.6,0xF4F0EA,.18*(1-f),false);Atmosphere++;}
            }
        }
        private void SignGlow(double t)
        {
            double x=NativeEmporiumLooks.SignX,y=NativeEmporiumLooks.SignY,z=NativeEmporiumLooks.SignZ;
            Hex(x,y,z,14,NativeEmporiumLooks.NeonPink,.12+.03*Math.Sin(t*2),true);Hex(x,y+.6,z,6,0xFFE6A0,.14,true);Lights+=2;
        }
        private void LampHalos(double t)
        {
            for(int i=0;i<lampCount;i++)
            {
                int o=i*6;double x=lamp[o],y=lamp[o+1],z=lamp[o+2],size=lamp[o+4],blink=lamp[o+5];int hex=(int)lamp[o+3];
                if(blink>0&&Frac(t/blink+H(i,3))>.2)continue;double d=Math.Sqrt((x-ex)*(x-ex)+(y-ey)*(y-ey)+(z-ez)*(z-ez));double grow=1+Math.Min(2,d/50);
                Hex(x,y,z,size*grow,hex,size>4?.16:.42,true);if(size>2&&!low)Hex(x,y,z,size*2.2*grow,hex,.08,true);Lights++;
            }
        }
    }
}
