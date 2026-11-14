using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Hollow Sanctum's camera-facing effects and lights for one frame (docs/arenas/SANCTUM.md "The look"), into one
    /// <see cref="NativeFxQuads"/> (Runtime draws it as one NativeFxDraw batch), the frame's <see cref="NativeBeamList"/> while the
    /// look's volumetric beams are on (null: their billboard shafts), and the eight practical slots (<see cref="LightPos"/>):
    /// <list type="bullet">
    /// <item>The moon through the glass: a shaft from every window the moon faces down its ray to the floor, gallery or pew it
    /// meets (found once per map against the blocks), coloured by the glass, and on that surface the window's light as a mosaic
    /// of coloured pools; dust motes turning in the shafts.</item>
    /// <item>Candlelight: every candle's flame (the kit's and the chandeliers'), flickering on the render clock, leaning with a
    /// swinging chandelier, guttering after a fall, dipping with each toll of the bell; their halos.</item>
    /// <item>The telegraphs, never reduced by the quality or the tier: the served chandelier's ring of warning light on its zone
    /// (gold, quickening to red), its cone, the plaster sifting from its anchor, its impact's sparks and dust; the censer's
    /// glowing path on the floor, its coals, its smoke trail and sparks as it swings; the holy fire's kindling curtain and its
    /// falling fire; the bell's strokes (rings of light down the oculus, dust shaken from the vault) and the gates' red lanterns.</item>
    /// <item>Atmosphere (reduced by the quality and the low tier): incense haze under the vault and over the floor, motes.</item>
    /// </list>
    /// The hazards' moments come only from the replicated state (NativeSanctumRig's poses). Pure: the same clock, camera, quality,
    /// map and rig give the same quads and lights. Allocation free after <see cref="Prepare"/> (per map).
    /// </summary>
    public sealed class NativeSanctumFx
    {
        public const int Capacity=7000;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        public NativeBeamList Beams;
        public int Telegraph,Atmosphere,Flames,Shafts;
        /// <summary>The pools of window light on the floors (once per map).</summary>
        public int Pools=>poolCount;
        /// <summary>This frame's eight practical lights: x, y, z, range and linear r, g, b (NativeLighting.PickedLights).</summary>
        public readonly float[] LightPos=new float[NativeMapLooks.MaxLights*4],LightRgb=new float[NativeMapLooks.MaxLights*3];
        private string prepared;private double[] candles=Array.Empty<double>();private double[] statics=Array.Empty<double>();
        // shafts: per moonlit window section: from x, y, z, to x, y, z, radius, r, g, b; pools: per cell 4 corners and r, g, b, a
        private double[] shafts=Array.Empty<double>(),pools=Array.Empty<double>();private int shaftCount,poolCount;
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz,quality;private bool low;
        private static double H(int a,int b=0,int c=0)=>NativeSanctumLooks.Hash(a,b,c);
        private static double Frac(double v)=>v-Math.Floor(v);
        private static double L(int hex,int shift)=>NativeSceneryBuilder.Channel(hex,shift);

        /// <summary>The map's candles, light sources, shafts and pools, once per map (allocates; nothing for another map).</summary>
        public void Prepare(ContentMap map,NativeSanctumKit kit)
        {
            if(map?.id==prepared&&candles.Length>0)return;prepared=map?.id;candles=kit?.Candles.ToArray()??Array.Empty<double>();statics=kit?.Lights.ToArray()??Array.Empty<double>();
            var s=new List<double>();var p=new List<double>();shaftCount=poolCount=0;if(map==null||!NativeSanctumLooks.Active(map.id)){shafts=pools=Array.Empty<double>();return;}
            NativeSanctumLooks.MoonRay(out double dx,out double dy,out double dz);var boxes=map.blocks??Array.Empty<ContentBlock>();
            foreach(var w in NativeSanctumLooks.Windows)
            {
                double share=NativeSanctumLooks.MoonShare(w);if(share<.05)continue;
                bool zn=w.Nz!=0;int sections=w.Rose?1:3;int col=GlassColor(w.Design);
                for(int k=0;k<sections;k++)
                {
                    double vy=w.Rose?w.Y:w.Y-w.HalfY+w.HalfY*2*(k+.5)/sections,half=w.Rose?w.Half-.3:w.Half-.2,hv=w.Rose?w.Half-.3:w.HalfY/sections;
                    double ox=w.X+w.Nx*.45,oz=w.Z+w.Nz*.45;double t=Hit(boxes,ox,vy,oz,dx,dy,dz,90);if(t<=1)continue;
                    int beam=NativeSanctumLooks.Mix(0xC4D2FF,col,.3);s.Add(ox);s.Add(vy);s.Add(oz);s.Add(ox+dx*t);s.Add(vy+dy*t);s.Add(oz+dz*t);s.Add(Math.Min(half,hv)*.95);s.Add(L(beam,16)*share);s.Add(L(beam,8)*share);s.Add(L(beam,0)*share);shaftCount++;
                    // the pools: the section's glass in a grid of cells, each corner carried down the ray to the floor it meets; a
                    // cell that straddles an edge (a pier, a balustrade, a step) is cut in four and only the quarters that lie
                    // whole on one floor are kept
                    int nu=w.Rose?4:2,nv=w.Rose?4:3;
                    for(int i=0;i<nu;i++)for(int j=0;j<nv;j++)
                    {
                        double u0=-half+2*half*i/nu,u1=-half+2*half*(i+1)/nu,v0=vy-hv+2*hv*j/nv,v1=vy-hv+2*hv*(j+1)/nv;
                        Pool(p,boxes,w,half,u0,u1,v0,v1,dx,dy,dz,PoolColor(w.Design,i,j),.55*share,0);
                    }
                }
            }
            shafts=s.ToArray();pools=p.ToArray();
        }
        /// <summary>How far over its floor a pool of light, a warning ring or a path strip is drawn (m): over the runner and the
        /// brass inlays (the stage art's lie up to 25 mm over the flagstones).</summary>
        public const double GroundLift=.04;
        // One cell of a window's light on the floor: its four corners down the moon's ray; kept when all four land on floors at
        // one height (the top of a static block at least .6 m wide: not a pew, a coping or a pier's side), else quartered once.
        private void Pool(List<double> p,ContentBlock[] boxes,in NativeSanctumLooks.Window w,double half,double u0,double u1,double v0,double v1,double dx,double dy,double dz,int color,double a,int depth)
        {
            bool zn=w.Nz!=0;double uc=(u0+u1)/2,vc=(v0+v1)/2;if(w.Rose&&uc*uc+(vc-w.Y)*(vc-w.Y)>half*half)return;
            double y=0;bool whole=true;
            for(int k=0;k<4&&whole;k++)
            {
                double uu=k==0||k==3?u0:u1,vv=k<2?v0:v1,px=w.X+(zn?uu:0)+w.Nx*.45,pz=w.Z+(zn?0:uu)+w.Nz*.45;
                if(!Top(boxes,px,vv,pz,dx,dy,dz,90,out double sy)||k>0&&Math.Abs(sy-y)>1e-3){whole=false;break;}
                y=sy;double tt=(sy+GroundLift-vv)/dy;corner[k*2]=px+dx*tt;corner[k*2+1]=pz+dz*tt;
            }
            if(whole){p.Add(y+GroundLift);for(int k=0;k<8;k++)p.Add(corner[k]);p.Add(L(color,16));p.Add(L(color,8));p.Add(L(color,0));p.Add(a);poolCount++;return;}
            if(depth>=1)return;double um=(u0+u1)/2,vm=(v0+v1)/2;
            Pool(p,boxes,w,half,u0,um,v0,vm,dx,dy,dz,color,a,depth+1);Pool(p,boxes,w,half,um,u1,v0,vm,dx,dy,dz,color,a,depth+1);Pool(p,boxes,w,half,u0,um,vm,v1,dx,dy,dz,color,a,depth+1);Pool(p,boxes,w,half,um,u1,vm,v1,dx,dy,dz,color,a,depth+1);
        }
        private readonly double[] corner=new double[8];
        /// <summary>Whether the ray from (x, y, z) along unit d first meets the top of a static block at least .6 m wide (a floor, a
        /// gallery, a step, a tomb's lid: not a breakable pew, which it passes through), and that top's height.</summary>
        public static bool Top(ContentBlock[] blocks,double x,double y,double z,double dx,double dy,double dz,double max,out double sy)
        {
            double best=max;int face=-1;ContentBlock hit=null;
            foreach(var b in blocks)
            {
                if(b==null||b.role!="static")continue;double t0=0,t1=best;bool miss=false;int enter=-1;
                for(int k=0;k<3&&!miss;k++)
                {
                    double o=k==0?x:k==1?y:z,d=k==0?dx:k==1?dy:dz,c=k==0?b.pos.x:k==1?b.pos.y:b.pos.z,h=(k==0?b.size.x:k==1?b.size.y:b.size.z)/2;
                    if(Math.Abs(d)<1e-9){if(o<c-h||o>c+h)miss=true;continue;}
                    double ta=(c-h-o)/d,tb=(c+h-o)/d;if(ta>tb){var q=ta;ta=tb;tb=q;}if(ta>t0){t0=ta;enter=k;}t1=Math.Min(t1,tb);if(t0>t1)miss=true;
                }
                if(!miss&&t0>1e-3&&t0<best){best=t0;face=enter;hit=b;}
            }
            sy=hit!=null?hit.pos.y+hit.size.y/2:0;return hit!=null&&face==1&&dy<0&&Math.Min(hit.size.x,hit.size.z)>=.6;
        }
        private static int GlassColor(int design){switch(design%4){case 0:return 0x6A88FF;case 1:return 0xFF6A7A;case 2:return 0x8AF0A8;default:return 0xC890FF;}}
        private static int PoolColor(int design,int i,int j){int[] c={0x3A5CFF,0xFF2A40,0xFFB030,0x30D070,0xA050FF,0xD8E4FF};return c[(design+i*2+j*3)%c.Length];}
        /// <summary>The ray's distance to the first block it meets (from (x, y, z) along unit d), else `max`.</summary>
        public static double Hit(ContentBlock[] blocks,double x,double y,double z,double dx,double dy,double dz,double max)
        {
            double best=max;
            foreach(var b in blocks)
            {
                if(b==null)continue;double t0=0,t1=best;bool miss=false;
                for(int k=0;k<3&&!miss;k++)
                {
                    double o=k==0?x:k==1?y:z,d=k==0?dx:k==1?dy:dz,c=k==0?b.pos.x:k==1?b.pos.y:b.pos.z,h=(k==0?b.size.x:k==1?b.size.y:b.size.z)/2;
                    if(Math.Abs(d)<1e-9){if(o<c-h||o>c+h)miss=true;continue;}
                    double ta=(c-h-o)/d,tb=(c+h-o)/d;if(ta>tb){var q=ta;ta=tb;tb=q;}t0=Math.Max(t0,ta);t1=Math.Min(t1,tb);if(t0>t1)miss=true;
                }
                if(!miss&&t0>1e-3&&t0<best)best=t0;
            }
            return best;
        }

        /// <summary>Fills the quads and the lights for the sanctum (prepared) from its rig's poses this frame, the hazards'
        /// replicated state, the render clock, the camera, the effects quality (0..1) and the low tier.</summary>
        public void Build(MapHazard[] hazards,NativeSanctumRig rig,int round,NativePhase phase,double fightAt,double now,double render,double camX,double camY,double camZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,double q,bool lowTier)
        {
            Quads.Clear();Telegraph=Atmosphere=Flames=Shafts=0;ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;quality=Math.Max(0,Math.Min(1,q));low=lowTier;
            double clock=NativeFactoryHazards.Clock(phase,fightAt,now);NativeHazardState gate=NativeCyberCityHazards.Idle();bool hasGate=false;
            for(int i=0;i<(hazards?.Length??0);i++)
            {
                var h=hazards[i];if(h==null)continue;
                if(h.kind==NativeFactoryHazards.Crane)ChandelierFx(h,NativeFactoryHazards.State(h,round,clock),clock,render,i,rig);
                else if(h.kind==NativeFactoryHazards.SawRail)CenserFx(h,NativeFactoryHazards.State(h,round,clock),round,clock,render,i,rig);
                else if(h.kind==NativeWardZeroHazards.Burner)FireFx(h,NativeWardZeroHazards.At(h,phase,round,fightAt,now),now,i);
                else if(h.kind==NativeWardZeroHazards.Shutter&&!hasGate){hasGate=true;gate=NativeWardZeroHazards.At(h,phase,round,fightAt,now);}
            }
            TollFx(gate,now,rig,hazards);
            double dip=TollDip(gate)*(.4+.6*quality);
            Moonlight(render);
            CandleFlames(render,rig,dip);
            Incense(render);
            Pick(rig,render,dip,hazards,round,phase,fightAt,now);
        }
        private bool lowAtmosphere()=>low&&quality<.5;
        private int Count(int full)=>low?Math.Max(1,full/3):Math.Max(1,(int)Math.Round(full*(.35+.65*quality)));

        // ---------------- quad helpers (as the M1 stages draw them) ----------------
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
        // A quad lying on a floor (a pool of light, a ring, a strip, a glow): each corner drawn nearer the eye along its own ray by
        // more than the stage art's deepest stack of layers (NativeStageArt.Nearer: the same pixel, a nearer depth), so it never
        // trades pixels with the flagstones, the runner or the brass inlays under it at any distance or angle.
        private bool Ground(double ax,double ay,double az,double bx,double by,double bz,double cx,double cy,double cz,double dx,double dy,double dz,double r,double g,double b,double a,int mode,bool additive)
        {
            Near(ref ax,ref ay,ref az);Near(ref bx,ref by,ref bz);Near(ref cx,ref cy,ref cz);Near(ref dx,ref dy,ref dz);
            return Quads.Add(ax,ay,az,bx,by,bz,cx,cy,cz,dx,dy,dz,r,g,b,a,mode,additive,1);
        }
        private void Near(ref double x,ref double y,ref double z)
        {
            double dx=x-ex,dy=y-ey,dz=z-ez,k=1-NativeStageArt.Nearer((float)Math.Sqrt(dx*dx+dy*dy+dz*dz),NativeStageArt.MostLayers);x=ex+dx*k;y=ey+dy*k;z=ez+dz*k;
        }
        private bool Flat(double x,double y,double z,double hx,double hz,double yaw,double r,double g,double b,double a,bool additive,int mode=1)
        {
            double c=Math.Cos(yaw),s=Math.Sin(yaw),ax=c*hx,az=-s*hx,bx=s*hz,bz=c*hz;
            return Ground(x-ax-bx,y,z-az-bz,x+ax-bx,y,z+az-bz,x+ax+bx,y,z+az+bz,x-ax+bx,y,z-az+bz,r,g,b,a,mode,additive);
        }
        private void Ring(double x,double y,double z,double r,double w,int n,double cr,double cg,double cb,double a)
        {
            for(int i=0;i<n;i++){double a0=2*Math.PI*i/n,a1=2*Math.PI*(i+1)/n,c0=Math.Cos(a0),s0=Math.Sin(a0),c1=Math.Cos(a1),s1=Math.Sin(a1);
                Ground(x+c0*(r-w),y,z+s0*(r-w),x+c1*(r-w),y,z+s1*(r-w),x+c1*(r+w),y,z+s1*(r+w),x+c0*(r+w),y,z+s0*(r+w),cr,cg,cb,a,0,true);}
        }
        private void Beam(double ax,double ay,double az,double bx,double by,double bz,double r0,double r1,double r,double g,double b,double alpha,double seed)
        {
            if(Beams!=null){Beams.Add(ax,ay,az,bx,by,bz,r0,r1,r*alpha*NativeBeamList.FromBillboard,g*alpha*NativeBeamList.FromBillboard,b*alpha*NativeBeamList.FromBillboard,.7,seed);Shafts++;return;}
            double dx=bx-ax,dy=by-ay,dz=bz-az,vx=(ax+bx)/2-ex,vy=(ay+by)/2-ey,vz=(az+bz)/2-ez,cx=dy*vz-dz*vy,cy=dz*vx-dx*vz,cz=dx*vy-dy*vx,cl=Math.Sqrt(cx*cx+cy*cy+cz*cz);if(cl<1e-6)return;cx/=cl;cy/=cl;cz/=cl;
            for(int k=0;k<3;k++)for(int j=0;j<5;j++)
            {
                double t0=k/3.0,t1=(k+1)/3.0,w0=r0+(r1-r0)*t0,w1=r0+(r1-r0)*t1,o=(j-2)*.4,wt=Math.Exp(-o*o*2.2)*(1-.3*k);
                double px0=ax+dx*t0+cx*o*w0,py0=ay+dy*t0+cy*o*w0,pz0=az+dz*t0+cz*o*w0,px1=ax+dx*t1+cx*o*w1,py1=ay+dy*t1+cy*o*w1,pz1=az+dz*t1+cz*o*w1;
                Quads.Add(px0-cx*w0*.2,py0-cy*w0*.2,pz0-cz*w0*.2,px1-cx*w1*.2,py1-cy*w1*.2,pz1-cz*w1*.2,px1+cx*w1*.2,py1+cy*w1*.2,pz1+cz*w1*.2,px0+cx*w0*.2,py0+cy*w0*.2,pz0+cz*w0*.2,r,g,b,alpha*wt,0,true,1);
            }
            Shafts++;
        }

        // ---------------- the moon through the glass ----------------
        private void Moonlight(double t)
        {
            for(int i=0;i<shaftCount;i++)
            {
                int o=i*10;double r=shafts[o+6],shimmer=.9+.1*Math.Sin(t*.37+i*1.3);
                Beam(shafts[o],shafts[o+1],shafts[o+2],shafts[o+3],shafts[o+4],shafts[o+5],r,r*1.15,shafts[o+7],shafts[o+8],shafts[o+9],.042*shimmer,i);
                // motes turning in the shaft
                int n=Count(10);double ax=shafts[o],ay=shafts[o+1],az=shafts[o+2],dx=shafts[o+3]-ax,dy=shafts[o+4]-ay,dz=shafts[o+5]-az;
                for(int k=0;k<n;k++)
                {
                    double f=Frac(H(i,k)+t*.012*(.5+H(i,k,2))),px=ax+dx*f+(H(i,k,3)-.5)*r*1.4+Math.Sin(t*.3+k)*.15,py=ay+dy*f+(H(i,k,4)-.5)*r*1.4,pz=az+dz*f+(H(i,k,5)-.5)*r*1.4;
                    double tw=.5+.5*Math.Sin(t*(1.1+H(i,k,6))+k);Sprite(px,py,pz,.05+.04*H(i,k,7),.95,.97,1,.55*tw,true);Atmosphere++;
                }
            }
            for(int i=0;i<poolCount;i++)
            {
                int o=i*13;double y=pools[o],tw=.92+.08*Math.Sin(t*.5+i*.7);
                Ground(pools[o+1],y,pools[o+2],pools[o+3],y,pools[o+4],pools[o+5],y,pools[o+6],pools[o+7],y,pools[o+8],pools[o+9],pools[o+10],pools[o+11],pools[o+12]*tw,1,true);Shafts++;
            }
        }

        // ---------------- candlelight ----------------
        /// <summary>How far the bell's toll ducks every flame (0 none .. .7): a gust after each stroke.</summary>
        public static double TollDip(in NativeHazardState gate)
        {
            if(gate.Phase!=NativeHazardPhase.Warn)return 0;double u=Frac(gate.Since/1.0);return u<.35?.7*Math.Sin(u/.35*Math.PI):0;
        }
        private void Flame(double x,double y,double z,double size,double seed,double t,double lean,double lit)
        {
            if(lit<=.02)return;double f=.82+.18*Math.Sin(t*(9+seed*5)+seed*31)+.08*Math.Sin(t*23+seed*17);double s=size*f*(.5+.5*lit);
            double d=Math.Sqrt((x-ex)*(x-ex)+(y-ey)*(y-ey)+(z-ez)*(z-ez));double grow=1+Math.Min(2.5,d/30);
            Streak(x,y,z,x+lean,y+s*1.35,z,s*.55*grow,s*.12*grow,1.0*2.6,.62*2.6,.24*2.6,.9*lit,true);
            Sprite(x,y+s*.35,z,s*.5*grow,1.4,1.25,.9,.95*lit,true);
            if(!low||d<14)Sprite(x,y+s*.4,z,s*(3.2+.4*f)*Math.Min(2,grow),1.0,.62,.26,.16*lit*f,true);Flames++;
        }
        private void CandleFlames(double t,NativeSanctumRig rig,double dip)
        {
            double lit=1-dip;
            for(int i=0;i+4<candles.Length;i+=5){double sd=candles[i+4];Flame(candles[i],candles[i+1],candles[i+2],candles[i+3],sd,t,.015*Math.Sin(t*1.3+sd*9),lit);}
            if(rig==null)return;
            for(int k=0;k<NativeSanctumRig.Chandeliers;k++)
            {
                double sway=rig.Ring[k*5+3];int phase=(int)rig.Ring[k*5+4];double lean=phase==1?-sway*1.8:phase==2?0:0,rise=phase==2?1:0;
                for(int i=0;i<NativeSanctumRig.ChandelierCandles;i++)
                {
                    int o=(k*NativeSanctumRig.ChandelierCandles+i)*4;double w=rig.Wicks[o+3];if(w<=.02)continue;double sd=H(k,i,41);
                    if(rise>0)
                    {
                        // falling: each flame torn up into a streak as long as the fall is fast, a spark let go behind it
                        double v=rig.FallSpeed[k],len=.3+.075*v,fl=.8+.2*Math.Sin(t*31+sd*40);
                        Streak(rig.Wicks[o],rig.Wicks[o+1],rig.Wicks[o+2],rig.Wicks[o]+(sd-.5)*.08,rig.Wicks[o+1]+len*fl,rig.Wicks[o+2]+(H(k,i,43)-.5)*.08,.1,.01,2.6,1.5,.5,.8,true);
                        Sprite(rig.Wicks[o],rig.Wicks[o+1]+.03,rig.Wicks[o+2],.12,1.4,1.2,.8,.9,true);
                        double f=Frac(t*3.1+sd),lag=f*(.4+.11*v);Sprite(rig.Wicks[o]+(H(k,i,44)-.5)*.3*f,rig.Wicks[o+1]+lag,rig.Wicks[o+2]+(H(k,i,45)-.5)*.3*f,.05,2.4,1.3,.4,.8*(1-f),true);Flames++;continue;
                    }
                    if(phase==4&&w<1)Sprite(rig.Wicks[o],rig.Wicks[o+1]+.05,rig.Wicks[o+2],.5*(1-w)+.1,1.6,1.2,.6,.7*(1-w),true);
                    Flame(rig.Wicks[o],rig.Wicks[o+1],rig.Wicks[o+2],.14+.04*sd,sd,t,lean,w*lit);
                }
            }
        }

        // ---------------- atmosphere ----------------
        private void Incense(double t)
        {
            int n=Count(16);
            for(int i=0;i<n;i++)
            {
                double x=-26+H(501,i)*52+Math.Sin(t*.04+i)*3,z=(H(501,i,1)-.5)*20,y=i%2==0?16+H(501,i,2)*4:.4+H(501,i,3)*1.6;
                Hex(x,y,z,(i%2==0?9:5)+H(501,i,4)*5,i%2==0?0x2A3050:0x30303C,i%2==0?.08:.06,false);Atmosphere++;
            }
            int m=Count(40);const double box=12;
            for(int i=0;i<m;i++)
            {
                double fx=Frac(H(503,i)+t*.011),fy=Frac(H(503,i,1)+t*.017),fz=Frac(H(503,i,2)-t*.008);double x=ex+(fx-.5)*2*box,y=ey-2+fy*7,z=ez+(fz-.5)*2*box;
                double d=Math.Sqrt((x-ex)*(x-ex)+(y-ey)*(y-ey)+(z-ez)*(z-ez));if(d<1.5)continue;Hex(x,y,z,.04+.03*H(503,i,3),0xFFE6C0,.35*Math.Min(1,(box-d)/4),true);Atmosphere++;
            }
            // the belfry over the oculus: the moon through its south louvres onto the bell, a cool glow seen up through the oculus
            if(!lowAtmosphere()){Hex(0,25.4,0,7,0x3A4A7A,.16,true);Hex(0,24.6,0,3.2,0x9AB0FF,.12,true);Atmosphere+=2;}
            NativeSanctumLooks.MoonRay(out double mx,out double my,out double mz);for(int i=-1;i<=1;i+=2){double sx=i*1.8,sy=26.5,sz=-3.7;Beam(sx,sy,sz,sx+mx*6,sy+my*6,sz+mz*6,.9,1.1,.75,.82,1,.06,700+i);}
            // the roses' halos in the haze
            foreach(var w in NativeSanctumLooks.Windows)if(w.Rose){double share=NativeSanctumLooks.MoonShare(w);Hex(w.X+w.Nx*1.5,w.Y,w.Z+w.Nz*1.5,w.Half*3.2,GlassColor(w.Design),.05+.08*share,true);Atmosphere++;}
        }

        // ---------------- telegraphs ----------------
        // A spark's height on its arc `t` seconds after it was thrown up at `up` m/s: it skips once (40% of its speed), then dies (-1).
        private static double Skip(double up,double t)
        {
            double first=2*up/9.8;if(t<first)return up*t-4.9*t*t;t-=first;up*=.4;return t<2*up/9.8?up*t-4.9*t*t:-1;
        }
        private void ChandelierFx(MapHazard h,in FactoryHazardState st,double clock,double render,int index,NativeSanctumRig rig)
        {
            if(st.Zone<0)return;var zn=h.zones[st.Zone];double fy=zn.center.y,cx=zn.center.x,cz=zn.center.z,r=Math.Max(zn.size.x,zn.size.z)/2,g0=fy+GroundLift;int k=st.Zone;
            NativeFactoryHazards.CraneTimes(h,out _,out double W,out double F,out _,out _,out _);
            double snap=rig!=null&&k<NativeSanctumRig.Chandeliers?rig.Snap[k]:-1,hoist=rig!=null&&k<NativeSanctumRig.Chandeliers?rig.Hoist[k]:-1;
            if(st.Phase==NativeFactoryHazards.Warning||st.Phase==NativeFactoryHazards.Falling)
            {
                double w=st.Phase==NativeFactoryHazards.Falling?1:st.Warn,hz=1.2+4.5*w*w,pulse=st.Phase==NativeFactoryHazards.Falling?1:.55+.45*Math.Max(0,Math.Sin(clock*hz*2*Math.PI));
                double cr=1.0,cg=.75-.55*w,cb=.25-.2*w;
                Ring(cx,g0,cz,r,.09+.05*w,32,cr*2,cg*2,cb*2,.75*pulse);Ring(cx,g0,cz,r*.62,.05,24,cr*1.4,cg*1.4,cb*1.4,.45*pulse);Telegraph+=56;
                Flat(cx,g0-.004,cz,r*.95,r*.95,Math.PI/4,cr*.6,cg*.6,cb*.6,.22*pulse*(.4+.6*w),true,1);Telegraph++;
                double bx=rig!=null?rig.Body[k*3]:cx,by=rig!=null?rig.Body[k*3+1]:h.carry,bz=rig!=null?rig.Body[k*3+2]:cz;
                Beam(bx,by,bz,cx,fy+.05,cz,r*.75,r*1.05,1.0,.62-.4*w,.28-.2*w,.05+.07*w*pulse,index+40);Telegraph++;
            }
            if(st.Phase==NativeFactoryHazards.Warning)
            {
                // plaster sifting from the anchor as the links work; a burst of it, a puff of dust and sparks at the crown ring
                // when the crown slips
                double w=st.Warn,t=w*W;
                int n=6+(int)(10*w);for(int i=0;i<n;i++){double f=Frac(clock*.9+H(index,i,7)),y=NativeSanctumRig.Anchor-f*7;Sprite(cx+(H(index,i,8)-.5)*.8+Math.Sin(f*9)*.1,y,cz+(H(index,i,9)-.5)*.8,.05+.04*H(i,2),.9,.86,.8,.6*(1-f)*(.4+.6*w),false);Telegraph++;}
                Hex(cx,NativeSanctumRig.Anchor-.2,cz,.7+.6*w,0xFFE0B0,.25*w,true);Telegraph++;
                for(int sl=0;sl<2;sl++)
                {
                    double ts=t-(sl==0?.45:.8)*W;if(ts<0||ts>=.9)continue;double f=ts/.9;
                    for(int i=0;i<12;i++){double y=NativeSanctumRig.Anchor-.2-(1.5+3*H(index,i,sl+70))*ts-2.4*ts*ts;Sprite(cx+(H(index,i,sl+72)-.5)*1.1,y,cz+(H(index,i,sl+74)-.5)*1.1,.06+.06*H(index,i,sl+76),.9,.86,.8,.75*(1-f),false);Telegraph++;}
                    Hex(cx,NativeSanctumRig.Anchor-.3-ts,cz,.7+1.8*f,0xB8B0A4,.3*(1-f),false);Telegraph++;
                    if(ts<.3&&rig!=null)
                    {
                        int c=(k*NativeSanctumRig.ChainPoints+rig.ChainCount[k]-1)*3;double qx=rig.ChainPts[c],qy=rig.ChainPts[c+1],qz=rig.ChainPts[c+2],s=ts/.3;
                        for(int i=0;i<8;i++){double a=2*Math.PI*(i+H(index,i,sl+78))/8,sp=.5+1.4*H(index,i,sl+80),px=qx+Math.Cos(a)*sp*ts,py=qy+(1.2*H(index,i,sl+82)-.2)*ts-4.9*ts*ts,pz=qz+Math.Sin(a)*sp*ts;Streak(px,py,pz,px-Math.Cos(a)*.1,py+.05,pz-Math.Sin(a)*.1,.035,.008,3,1.9,.7,.9*(1-s),true);Telegraph++;}
                    }
                }
            }
            if(snap>=0)
            {
                // the snap: a flash and sparks where the link parts, plaster and dust shaken down from the anchor for two seconds
                double by=h.carry-.14+NativeSanctumRig.ChainFoot+NativeSanctumRig.StubLength;
                if(snap<.14){double f=1-snap/.14;Hex(cx,by,cz,1.6*f+.3,0xFFE8B0,.8*f,true,2.2);Telegraph++;}
                if(snap<.5){double s=snap/.5;for(int i=0;i<14;i++){double a=2*Math.PI*(i+H(index,i,90))/14,sp=1.5+3*H(index,i,91),up=.4+2.4*H(index,i,92),px=cx+Math.Cos(a)*sp*snap,py=by+up*snap-4.9*snap*snap,pz=cz+Math.Sin(a)*sp*snap;Streak(px,py,pz,px-Math.Cos(a)*sp*.04,py-(up-9.8*snap)*.04,pz-Math.Sin(a)*sp*.04,.045,.008,3,1.9,.7,.95*(1-s),true);Telegraph++;}}
                if(snap<2.2)
                {
                    int n=Count(18);for(int i=0;i<n;i++){double age=snap-.7*H(index,i,93);if(age<0)continue;double y=NativeSanctumRig.Anchor-.2-.8*age-1.7*age*age;if(y<fy+.1)continue;Sprite(cx+(H(index,i,94)-.5)*(.7+.5*age),y,cz+(H(index,i,95)-.5)*(.7+.5*age),.06+.07*H(index,i,96),.9,.86,.8,.6,false);Atmosphere++;}
                    Hex(cx,NativeSanctumRig.Anchor-.5-snap*.6,cz,1+2*snap,0xB8B0A4,.28*Math.Max(0,1-snap/1.6),false);Atmosphere++;
                }
            }
            if(st.Phase==NativeFactoryHazards.Falling&&snap>=0)
            {
                // the fall: embers and smoke left behind along its path
                for(int j=0;j<16;j++)
                {
                    double a=(j+Frac(render*7+H(index,j,61)))*.045,ts=snap-a;if(ts<0)break;double qq=ts/F,y=fy+(h.carry-fy)*(1-qq*qq)+.3,ang=2*Math.PI*H(index,j,62);
                    Sprite(cx+Math.Cos(ang)*1.3,y+.6*a,cz+Math.Sin(ang)*1.3,.07,2.6,1.3,.35,.9*Math.Max(0,1-a/.75),true);Telegraph++;
                }
                for(int j=0;j<6;j++)
                {
                    double a=(j+Frac(render*3+H(index,j,63)))*.11,ts=snap-a;if(ts<0)break;double qq=ts/F,y=fy+(h.carry-fy)*(1-qq*qq)+1.0;
                    Hex(cx+(H(index,j,64)-.5)*1.6,y+.5*a,cz+(H(index,j,65)-.5)*1.6,.8+1.6*a,0x3A3438,.16*Math.Max(0,1-a/.7),false);Atmosphere++;
                }
            }
            double ti=double.IsNaN(st.ImpactAt)?-1:clock-st.ImpactAt;
            if(ti>=0&&ti<2.6)
            {
                // the crash: a flash and its light on the floor; the shock running out as a ring; sparks thrown out, falling and
                // skipping once; dust rolling out low and a plume standing up from the wreck; embers lofted and drifting
                if(ti<.16){double f=1-ti/.16;Hex(cx,fy+.5,cz,r*3.2*f+1,0xFFD8A0,.6*f,true,2);Flat(cx,g0+.006,cz,r*2.4,r*2.4,0,2.4,1.5,.6,.5*f,true,1);Telegraph+=2;}
                if(ti<.55){double f=ti/.55;Ring(cx,g0+.004,cz,r*.8+f*7,.1+.3*f,40,1.5,1.2,.9,.5*(1-f)*(1-f));Telegraph+=40;}
                if(ti<.95)for(int i=0;i<34;i++)
                {
                    double a=2*Math.PI*(i+H(index,i,4))/34,sp=2.5+5*H(index,i,5),up=2+4*H(index,i,6),y1=Skip(up,ti),y0=Skip(up,Math.Max(0,ti-.035));if(y1<0||y0<0)continue;
                    double d1=r*.6+sp*ti,d0=r*.6+sp*Math.Max(0,ti-.035);Streak(cx+Math.Cos(a)*d1,fy+.15+y1,cz+Math.Sin(a)*d1,cx+Math.Cos(a)*d0,fy+.15+y0,cz+Math.Sin(a)*d0,.05,.01,3,1.8,.6,.95*(1-ti/.95),true);Telegraph++;
                }
                if(ti<1.8){double t=ti/1.8;int n=Count(22);for(int i=0;i<n;i++){double a=i*2*Math.PI/n+H(index,i),rad=r*.8+t*(3.0+H(index,i,2)*1.6);Hex(cx+Math.Cos(a)*rad,fy+.3+t*1.0,cz+Math.Sin(a)*rad,1.1+t*2.6,0x6A6058,.4*(1-t)*(1-t),false);Atmosphere++;}}
                {
                    int n=Count(12);for(int i=0;i<n;i++){double age=ti-.2*H(index,i,14);if(age<0)continue;double f=age/2.4;if(f>=1)continue;double up=3.8*(1-(1-f)*(1-f));Hex(cx+(H(index,i,15)-.5)*(1.2+1.6*f),fy+.4+up*(.5+.5*H(index,i,16)),cz+(H(index,i,17)-.5)*(1.2+1.6*f),1.2+2.6*f,0x6A6058,.34*(1-f)*Math.Min(1,age/.12),false);Atmosphere++;}
                    int m=Count(20);for(int i=0;i<m;i++){double a=2*Math.PI*H(index,i,18),rr=(.4+1.2*H(index,i,19))*(1+.5*ti),y=fy+.3+(1+2.5*H(index,i,20))*ti*.8-.15*ti*ti,fl=.6+.4*Math.Sin(render*(9+7*H(index,i,24))+i);Sprite(cx+Math.Cos(a)*rr+Math.Sin(ti*2+i)*.15,y,cz+Math.Sin(a)*rr,.05+.03*H(index,i,25),2.4,1.2,.35,.8*(1-ti/2.6)*fl,true);Atmosphere++;}
                }
            }
            if(rig!=null&&snap>=0&&st.Phase!=NativeFactoryHazards.Hoisting)
            {
                // the debris as it lands: a link rings on the stone (a glint), a lump of plaster bursts into dust
                for(int i=NativeSanctumRig.Shards;i<NativeSanctumRig.DebrisCount;i++)
                {
                    double age=snap-NativeSanctumRig.DebrisLands(k,i,fy,h.carry);if(age<0||age>=.5)continue;int o=(k*NativeSanctumRig.DebrisCount+i)*8;double px=rig.Scrap[o],pz=rig.Scrap[o+2];
                    if(i<NativeSanctumRig.Shards+NativeSanctumRig.Links){if(age<.2)Sprite(px,fy+.08,pz,.22*(1-age/.2)+.05,2.2,1.9,1.3,.9*(1-age/.2),true);}
                    else Hex(px,fy+.2+age*.5,pz,.45+1.3*age/.5,0xB8B0A4,.32*(1-age/.5),false);
                    Atmosphere++;
                }
            }
            // smoke from the guttering wreck while it lies, its dying glow on the floor
            if(st.Phase==NativeFactoryHazards.Landed||st.Phase==NativeFactoryHazards.Regrip)
            {
                int n=Count(8);double age=clock-st.ImpactAt;for(int i=0;i<n;i++){double f=Frac(clock*.25+H(index,i,11));Hex(cx+(H(index,i,12)-.5)*r*1.6+f*.6,fy+.3+f*3,cz+(H(index,i,13)-.5)*r*1.6,.6+f*1.8,0x2E2A30,.22*(1-f)*Math.Max(0,1-age/6),false);Atmosphere++;}
                Flat(cx,g0-.004,cz,r*.9,r*.9,0,1,.45,.12,.22*Math.Max(0,1-age/3.5)*(.8+.2*Math.Sin(render*9)),true,1);Atmosphere++;
            }
            // the hoist: dust shaken off as the winch takes it up
            if(hoist>=0&&hoist<.9){double f=hoist/.9;int n=Count(10);for(int i=0;i<n;i++){double a=2*Math.PI*(i+H(index,i,26))/n;Hex(cx+Math.Cos(a)*(r*.7+f),fy+.25+f*.5,cz+Math.Sin(a)*(r*.7+f),.7+f,0x6A6058,.22*(1-f),false);Atmosphere++;}}
        }
        // Where the censer was (its centre and its speed along the path) at an earlier hazard clock: the simulation's point then.
        private static void CenserAt(MapHazard h,int round,double clock,out double x,out double y,out double z,out double v)
        {
            var s=NativeFactoryHazards.State(h,round,clock);NativeSanctumRig.CenserPoint(h,s,out x,out y,out z,out _);var n=NativeFactoryHazards.State(h,round,clock+.02);v=(n.Center.z-s.Center.z)/.02;
        }
        private void CenserFx(MapHazard h,in FactoryHazardState st,int round,double clock,double render,int index,NativeSanctumRig rig)
        {
            if(rig==null)return;double x=rig.CenserX,y=rig.CenserY,z=rig.CenserZ,glow=rig.CenserGlow,v=rig.CenserSpeed,g0=GroundLift;
            // the coals through the lid, the glow round the bowl, their light on the floor under it
            Hex(x,y-.1,z,.9+.5*glow,0xFF7A30,.6+.3*glow,true,1.4+glow);Hex(x,y,z,3.2,0xFF6A20,.1+.12*glow,true);Telegraph+=2;
            Flat(x,g0+.008,z,1.5,1.5,0,1.6,.6,.15,.3*Math.Min(1,glow)*Math.Max(0,1-(y-1.3)*.6),true,1);Telegraph++;
            // the path: a smouldering line on the floor from the south arch to the north, kindling through the warning, burning as it swings
            bool warning=st.Phase==NativeFactoryHazards.Warning,moving=st.Phase==NativeFactoryHazards.Running||st.Phase==NativeFactoryHazards.Holding||st.Phase==NativeFactoryHazards.Returning;
            double k=warning?.3+.7*st.Warn:moving?1:.12;double z0=Math.Min(h.a.z,h.b.z)-h.size.z/2,z1=Math.Max(h.a.z,h.b.z)+h.size.z/2;
            int seg=12;for(int i=0;i<seg;i++){double za=z0+(z1-z0)*i/seg,zb=z0+(z1-z0)*(i+1)/seg,pulse=warning?.7+.3*Math.Sin(clock*14-i*.8):1;
                Ground(x-h.size.x/2,g0,za,x+h.size.x/2,g0,za,x+h.size.x/2,g0,zb,x-h.size.x/2,g0,zb,1.0,.35,.08,.3*k*pulse,1,true);Telegraph++;}
            Flat(x,g0-.003,(z0+z1)/2,.06,(z1-z0)/2,0,2,.8,.2,.6*k,true,0);Flat(x-h.size.x/2,g0-.003,(z0+z1)/2,.03,(z1-z0)/2,0,1.6,.6,.15,.5*k,true,0);Flat(x+h.size.x/2,g0-.003,(z0+z1)/2,.03,(z1-z0)/2,0,1.6,.6,.15,.5*k,true,0);Telegraph+=3;
            // the smoke: every puff is let go where the censer was when it left the lid and rises from there, so it stands as a
            // column while the censer hangs, billows through the warning and lies along the arc behind it while it swings
            const double life=1.7;int n=Count(14)+(warning?(int)(10*st.Warn):0)+(moving?Count(8):0);
            for(int i=0;i<n;i++)
            {
                double a=Frac(render/life+H(index,i,21))*life,f=a/life;CenserAt(h,round,clock-a,out double px,out double py,out double pz,out _);
                Hex(px+(H(index,i,22)-.5)*(.3+.9*f),py+.9+f*2.2,pz+(H(index,i,23)-.5)*(.3+.9*f),.45+f*1.7,0x8A8494,.28*(1-f)*Math.Min(1,f/.08),false);Atmosphere++;
            }
            if(moving)
            {
                // the swing: the coals' glow drawn out along the arc behind it, sparks shed where it passed, falling
                double sx=x,sy=y,sz=z,fast=Math.Min(1,Math.Abs(v)/7);
                for(int j=1;j<=8;j++){CenserAt(h,round,clock-j*.03,out double px,out double py,out double pz,out _);Streak(sx,sy,sz,px,py,pz,.6-.05*j,.55-.05*j,1.2,.5,.14,.2*(1-j/9.0)*fast,true);Telegraph++;sx=px;sy=py;sz=pz;}
                for(int i=0;i<18;i++)
                {
                    double a=Frac(clock*1.9+H(index,i,31))*.7;CenserAt(h,round,clock-a,out double px,out double py,out double pz,out double pv);if(Math.Abs(pv)<1.5)continue;
                    double qx=px+(H(index,i,32)-.5)*.5,qy=py-.3-3.4*a*a+.5*a*(H(index,i,33)-.3),qz=pz+pv*.12*a+(H(index,i,34)-.5)*.4;if(qy<.05)continue;
                    Streak(qx,qy,qz,qx,qy+.05+.6*a*.2,qz-pv*.02,.045,.01,3,1.4,.4,.85*(1-a/.7),true);Telegraph++;
                }
                // the release: a burst of sparks spilt behind it as the tether lets go
                double t=st.Age-h.warn;
                if(st.Phase==NativeFactoryHazards.Running&&t<.4)for(int i=0;i<14;i++)
                {
                    double a=2*Math.PI*H(index,i,35),sp=.6+1.8*H(index,i,36),up=1+2.5*H(index,i,37),px=h.a.x+Math.Cos(a)*sp*t,py=h.a.y+.9+up*t-4.9*t*t,pz=h.a.z-.3-Math.Abs(Math.Sin(a))*sp*t;
                    Streak(px,py,pz,px-Math.Cos(a)*sp*.04,py-(up-9.8*t)*.04,pz+Math.Abs(Math.Sin(a))*sp*.04,.045,.008,3,1.7,.5,.95*(1-t/.4),true);Telegraph++;
                }
            }
            if(warning)
            {
                // each heave of the tether shakes a cough of sparks out through the lid
                for(int hv=0;hv<NativeSanctumRig.Heaves;hv++)
                {
                    double t=st.Age-NativeSanctumRig.HeaveAt(hv,h.warn);if(t<0||t>=.35)continue;
                    for(int i=0;i<7;i++){double a=2*Math.PI*H(index,i,hv+38),sp=.4+.9*H(index,i,hv+41),up=1.2+1.6*H(index,i,hv+44),px=x+Math.Cos(a)*sp*t,py=y+.7+up*t-4.9*t*t,pz=z+Math.Sin(a)*sp*t;Streak(px,py,pz,px,py-(up-9.8*t)*.04,pz,.04,.008,3,1.6,.5,.9*(1-t/.35),true);Telegraph++;}
                    Hex(x,y+.5,z,1.2+2*t,0xFF9A40,.35*(1-t/.35),true,1.5);Telegraph++;
                }
            }
        }
        private void FireFx(MapHazard h,in NativeHazardState st,double time,int index)
        {
            double soffit=NativeSanctumLooks.Gallery-.4;
            foreach(var z in h.zones??Array.Empty<MapVolume>())
            {
                double x=z.center.x,z0=z.center.z-z.size.z/2,z1=z.center.z+z.size.z/2,depth=z.size.x,tall=z.size.y;
                for(int b=0;b<3;b++)
                {
                    double bz=z0+.75+(z1-z0-1.5)*b/2,by=soffit-.75;
                    if(st.Phase==NativeHazardPhase.Idle){double f=.85+.15*Math.Sin(time*9+b+x);Streak(x,by-.1,bz,x,by+.25*f,bz,.22,.04,2.2,1.9,1.3,.7,true);Hex(x,by-.05,bz,.7,0xFFD890,.25,true,1.2);Telegraph+=2;continue;}
                    if(st.Phase==NativeHazardPhase.Warn)
                    {
                        double l=st.Level;Hex(x,by,bz,.6+.9*l,0xFFE2A0,.6+.3*l,true,1.6+l);Telegraph++;
                        // embers dripping, a shimmer of light down the curtain's plane
                        for(int i=0;i<4;i++){double f=Frac(time*1.4+H(index,b,i)),py=by-f*by;Sprite(x+(H(index,i,b+7)-.5)*.3,py,bz+(H(index,b,i+3)-.5)*.4,.06,2,1.6,.8,.9*(1-f*.5)*l,true);Telegraph++;}
                    }
                    else if(st.Phase==NativeHazardPhase.Active)
                    {
                        Hex(x,by,bz,1.6,0xFFF0C8,.9,true,2.4);Telegraph++;
                        int n=low?4:7;for(int i=0;i<n;i++)
                        {
                            double f=Frac(time*2.2+H(index,b,i+20)),py=by-f*(by-.1),wob=Math.Sin(time*13+i*2.1+b)*.12;double wd=(.35+f*.5)*depth*.42;
                            Streak(x+wob,py+.35,bz+(H(index,b,i+30)-.5)*.6,x+wob*.5,py-.45,bz+(H(index,b,i+31)-.5)*.6,wd*.6,wd,2.2,1.9,1.3,.55*(1-.4*f),true);Telegraph++;
                        }
                        Hex(x,.25,bz,1.4,0xFFD890,.6,true,1.8);Telegraph++;
                    }
                    else{for(int i=0;i<3;i++){double f=Frac(time*.5+H(index,b,i+40));Hex(x,by+.2+f*1.5,bz,.6+f*1.2,0x3A3640,.3*st.Level*(1-f),false);Atmosphere++;}}
                }
                // the curtain's plane: a faint kindling sheet through the warning, a bright one while it pours, the glyph line flaring
                if(st.Phase==NativeHazardPhase.Warn){double l=st.Level,fl=.7+.3*Math.Sin(time*20);Quads.Add(x,.02,z0+.2,x,.02,z1-.2,x,soffit-.6,z1-.2,x,soffit-.6,z0+.2,1,.8,.4,.07*l*fl,1,true,1);Flat(x,GroundLift+.01,(z0+z1)/2,.25,(z1-z0)/2-.1,0,1.8,1.4,.6,.5*l,true,0);Telegraph+=2;}
                if(st.Phase==NativeHazardPhase.Active){Quads.Add(x,.02,z0+.1,x,.02,z1-.1,x,soffit-.5,z1-.1,x,soffit-.5,z0+.1,1.4,1.1,.7,.28,1,true,1);Flat(x,GroundLift+.01,(z0+z1)/2,depth*.45,(z1-z0)/2,0,2.2,1.7,1,.45,true,1);Telegraph+=2;
                    for(int i=0;i<Count(10);i++){double f=Frac(time*.8+H(index,i,50));Sprite(x+(H(index,i,51)-.5)*depth,tall*.9+f*1.5,z0+H(index,i,52)*(z1-z0),.06,2,1.6,.9,.9*(1-f),true);Telegraph++;}}
            }
        }
        private void TollFx(in NativeHazardState gate,double time,NativeSanctumRig rig,MapHazard[] hazards)
        {
            // the strokes: a ring of pale light down the oculus at each stroke, dust shaken from the whole vault after it
            if(gate.Phase==NativeHazardPhase.Warn||gate.Phase==NativeHazardPhase.Danger&&gate.Since<1.2)
            {
                double since=gate.Phase==NativeHazardPhase.Warn?gate.Since:gate.Since+3;double u=Frac(since/1.0);int stroke=(int)Math.Floor(since);
                for(int k=0;k<3;k++){double f=Frac(u+k*.33);double rr=1.5+f*9,y=NativeSanctumLooks.BellY-2-f*14;Ring(0,y,0,rr,.08+.1*f,40,1.6,1.4,1.0,.35*(1-f)*(1-f));Telegraph+=40;}
                Hex(NativeSanctumLooks.BellX,NativeSanctumLooks.BellY-1.4,NativeSanctumLooks.BellZ,3.5,0xFFE0A0,.35*(1-u),true,1.6);Telegraph++;
                // each stroke lands on the floor under the oculus a moment later and runs out across the crossing as a ring of dust
                double g=u-.25;if(g>0){double rr=1+g*16,fade=(1-g/.75);Ring(0,.05,0,rr,.12+.2*g,48,1.4,1.2,.9,.4*fade*fade);Telegraph+=48;int m=Count(16);for(int i=0;i<m;i++){double ang=2*Math.PI*i/m+H(611,i);Hex(Math.Cos(ang)*rr,.3+g*.6,Math.Sin(ang)*rr,.8+g*1.6,0x8A8078,.25*fade,false);Atmosphere++;}}
                int n=Count(40);for(int i=0;i<n;i++){double f=Frac(u*1.3+H(601,i,stroke%5)),px=-26+H(601,i)*52,pz=(H(601,i,1)-.5)*22;Sprite(px,NativeSanctumLooks.Vault-.5-f*9,pz,.05+.05*H(601,i,2),.85,.82,.78,.55*(1-f),false);Atmosphere++;}
            }
            // the gates' lanterns: their halos in the crypt and the stairwells
            if(rig==null)return;int lamp=rig.Lamp;double a=gate.Phase==NativeHazardPhase.Idle?.15:.75;
            foreach(var h in hazards??Array.Empty<MapHazard>())
            {
                if(h?.kind!=NativeWardZeroHazards.Shutter)continue;double top=h.center.y+h.size.y/2;
                for(int sd=0;sd<4;sd++){double side=sd<2?-1:1,z=sd%2==0?h.center.z-h.size.z/2-.12:h.center.z+h.size.z/2+.12;Hex(h.center.x+side*.2,top-.45,z,.9,lamp,a,true,1.6);Telegraph++;}
                if(gate.Phase==NativeHazardPhase.Warn||gate.Phase==NativeHazardPhase.Danger){double f=NativeFactoryRig.Strobe(time,2.5)?1:.3;Flat(h.center.x,h.center.y-h.size.y/2+GroundLift,h.center.z,.6,h.size.z/2+.3,0,2,.15,.08,.35*f,true,1);Telegraph++;}
            }
        }

        // ---------------- the practical lights ----------------
        private const int Cand=64;private readonly double[] cx=new double[Cand],cy=new double[Cand],cz=new double[Cand],cr=new double[Cand],cg=new double[Cand],cb=new double[Cand],crange=new double[Cand],score=new double[Cand];private int cn;
        private void Candidate(double x,double y,double z,double range,double r,double g,double b,double weight)
        {
            if(cn>=Cand||!(r+g+b>1e-4))return;cx[cn]=x;cy[cn]=y;cz[cn]=z;crange[cn]=range;cr[cn]=r;cg[cn]=g;cb[cn]=b;
            double dx=x-ex,dy=y-ey,dz=z-ez,d2=dx*dx+dy*dy+dz*dz,lum=r*.3+g*.55+b*.15;score[cn]=lum*weight*(1/(1+d2/(range*range*.5)));cn++;
        }
        /// <summary>Chooses the eight slots: the chandeliers where they hang (or fall, or lie guttering), the censer's coals, the
        /// holy fire, the gates' lanterns, and the kit's candle sources, scored by their light at the eye; a slot near the cut-off
        /// fades before it is given up, so no light pops.</summary>
        private void Pick(NativeSanctumRig rig,double t,double dip,MapHazard[] hazards,int round,NativePhase phase,double fightAt,double now)
        {
            cn=0;double warm=1-dip*.6;
            if(rig!=null)
            {
                for(int k=0;k<NativeSanctumRig.Chandeliers;k++){int o=k*5;int ph=(int)rig.Ring[o+4];double lit=ph==3?.45:1,f=.9+.1*Math.Sin(t*7.3+k*2)+.05*Math.Sin(t*17+k);
                    Candidate(rig.Ring[o],rig.Ring[o+1]+.4,rig.Ring[o+2],13,1.0*1.8*lit*f*warm,.62*1.8*lit*f*warm,.3*1.8*lit*f*warm,ph==1||ph==2?3:1.4);}
                double g=rig.CenserGlow,fc=.85+.15*Math.Sin(t*11);Candidate(rig.CenserX,rig.CenserY,rig.CenserZ,7,1.0*(.6+1.2*g)*fc,.38*(.6+1.2*g)*fc,.1*(.6+1.2*g)*fc,g>.5?3:1.2);
                for(int i=0;i<rig.BasketCount;i+=3){int o=(i+1)*4;double heat=rig.Baskets[o+3];if(heat<.2)continue;Candidate(rig.Baskets[o],1.8,rig.Baskets[o+2],9,1.0*2.6*heat,.86*2.6*heat,.6*2.6*heat,4);}
                if(rig.Gate>0||((rig.Lamp>>16)&255)>200)foreach(var h in hazards??Array.Empty<MapHazard>())if(h?.kind==NativeWardZeroHazards.Shutter){double lv=((rig.Lamp>>16)&255)/255.0;Candidate(h.center.x,h.center.y+.8,h.center.z,4.5,.95*lv,.1*lv,.05*lv,3);}
            }
            for(int i=0;i+6<statics.Length;i+=7){double f=.88+.12*Math.Sin(t*(6+i%5)+i);Candidate(statics[i],statics[i+1],statics[i+2],statics[i+3],statics[i+4]*f*warm,statics[i+5]*f*warm,statics[i+6]*f*warm,1);}
            // the best eight, and the ninth's score as the fade's floor
            int slots=NativeMapLooks.MaxLights;double ninth=0;
            for(int s=0;s<slots;s++)
            {
                int best=-1;for(int i=0;i<cn;i++)if(score[i]>=0&&(best<0||score[i]>score[best]))best=i;
                if(best<0){LightPos[s*4]=0;LightPos[s*4+1]=-10000;LightPos[s*4+2]=0;LightPos[s*4+3]=1;LightRgb[s*3]=LightRgb[s*3+1]=LightRgb[s*3+2]=0;continue;}
                cand[s]=best;keep[s]=score[best];score[best]=-1;
            }
            for(int i=0;i<cn;i++)if(score[i]>ninth)ninth=score[i];
            for(int s=0;s<slots;s++)
            {
                int i=cand[s];if(i<0||i>=cn||keep[s]<=0)continue;double fade=ninth<=0?1:Math.Max(0,Math.Min(1,(keep[s]-ninth)/(ninth*.6)));
                LightPos[s*4]=(float)cx[i];LightPos[s*4+1]=(float)cy[i];LightPos[s*4+2]=(float)cz[i];LightPos[s*4+3]=(float)crange[i];LightRgb[s*3]=(float)(cr[i]*fade);LightRgb[s*3+1]=(float)(cg[i]*fade);LightRgb[s*3+2]=(float)(cb[i]*fade);
            }
            for(int s=0;s<slots;s++){cand[s]=-1;keep[s]=0;}
        }
        private readonly int[] cand={-1,-1,-1,-1,-1,-1,-1,-1};private readonly double[] keep=new double[8];
        /// <summary>The light volume's bake candidates (more than the eight slots): the kit's sources and the chandeliers at rest.</summary>
        public void Candidates(List<NativeVolumeLight> into,MapHazard[] hazards)
        {
            for(int i=0;i+6<statics.Length;i+=7)into.Add(new NativeVolumeLight((float)statics[i],(float)statics[i+1],(float)statics[i+2],(float)statics[i+3],(float)statics[i+4],(float)statics[i+5],(float)statics[i+6]));
            foreach(var h in hazards??Array.Empty<MapHazard>())if(h?.kind==NativeFactoryHazards.Crane)foreach(var z in h.zones)into.Add(new NativeVolumeLight(z.center.x,h.carry+.5f,z.center.z,13,1.6f,.9f,.4f));
        }
    }
}
