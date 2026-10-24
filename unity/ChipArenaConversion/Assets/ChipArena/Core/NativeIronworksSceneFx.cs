using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>A heat haze source for the distortion pass (NativeHeatHaze.shader): a camera-facing quad's centre, width and height
    /// (m) and its strength (0..1).</summary>
    public struct NativeHeatSource {public float X,Y,Z,W,H,Strength;}

    /// <summary>
    /// What the Ironworks' effects need, built once from the kit (<see cref="Build"/>): the glows by kind (furnace mouths, tapholes,
    /// lamps, the converters, the coke doors), the hood and standpipe smoke points, the roof monitors' shafts. No allocation after
    /// Build.
    /// </summary>
    public sealed class NativeIronFxScene
    {
        public NativeIronGlow[] Halos=Array.Empty<NativeIronGlow>();public float[] Hoods=Array.Empty<float>(),Pipes=Array.Empty<float>();
        public static NativeIronFxScene Build(NativeIronworksKit kit)
        {
            var s=new NativeIronFxScene();var halos=new List<NativeIronGlow>();
            foreach(var g in kit?.Glows??Array.Empty<NativeIronGlow>())if(g.Kind!=NativeIronworksKit.GlowRunner&&g.Kind!=NativeIronworksKit.GlowCokeSeam&&g.Kind!=NativeIronworksKit.GlowMold)halos.Add(g);
            s.Halos=halos.ToArray();
            var hoods=new List<float>();foreach(float x in NativeIronworksKit.FurnaceX){hoods.Add(x);hoods.Add(17.4f);hoods.Add(27.2f);}s.Hoods=hoods.ToArray();
            var pipes=new List<float>();for(double x=-48.6;x<-25;x+=3.68){pipes.Add((float)x);pipes.Add(13.3f);pipes.Add(27.45f);}s.Pipes=pipes.ToArray();
            return s;
        }
    }

    /// <summary>
    /// Builds the Ironworks' camera-facing effects for one frame (docs/EXPEDITION-CITY-IRONWORKS.md sections 3.3 and 5) into a
    /// <see cref="NativeFxQuads"/> (Runtime draws them as one solid and one effects NativeSirenFx batch) and the heat haze sources
    /// (<see cref="Heat"/>, for NativeHeatHaze.shader):
    ///  - light: halos round the furnace mouths, tapholes, converters, the open coke door, the lamps (sodium warm, mercury cold),
    ///    the pour's splash, the slab, the billet; the roof monitors' cold shafts and the furnace mouths' warm rays;
    ///  - sparks: off the pour's splash (70 arcs falling to the floor), spat from the open furnace mouths, the taphole as it taps,
    ///    the mill stands as the slab goes through, the shear's cut and the hammer's blow; embers drifting up;
    ///  - smoke and steam: the hoods into the flues, the converters' mouths, the coke doors and standpipes, the pour, a dark
    ///    layer under the roof lit from below over the furnaces and the pour, the quench tower's burst, the stands' sprays;
    ///  - telegraphs (readability rule 4): hard-edged cream-white decals on the floor over each hazard's zone while it warns and
    ///    runs (the pour's zone and its landing ring, a runner's trough, the hammer's ring, the slag's tongue, the slab's lane, the
    ///    shear's line, the quench grate, the pusher's path); never orange, and brighter than any molten floor under them.
    /// Counts scale with the effects quality and the low tier (NativeBrickfallAtmosphere.Count's rule) except the telegraphs and
    /// the halos, which mark danger and light. Pure: the same clock, camera, views and quality give the same quads. Allocation
    /// free.
    /// </summary>
    public sealed class NativeIronworksSceneFx
    {
        public const int Capacity=3200,SortMax=360,HeatMax=48;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        public readonly NativeHeatSource[] Heat=new NativeHeatSource[HeatMax];public int HeatCount;
        public int Sparks,Smoke,Halos,Telegraphs,Embers,Shafts,Steam;
        /// <summary>The zone boss song (set before <see cref="Build"/>; Off by default, docs/ZONE-BOSS-MUSIC.md): the furnace mouths spit
        /// more sparks on the pulse, the coke standpipes and the hoods puff steam on the downbeats and slams, and the drop rains a
        /// shower of sparks from the roof over the casting floor. The telegraphs, the pour, the hazards' sparks and steam never change.</summary>
        public NativeZoneBossBeat Music;
        /// <summary>The telegraph decals' colour (linear, times its gain): cream white, the readability rules' area decal.</summary>
        public const float DecalR=1f,DecalG=.88f,DecalB=.66f,DecalGain=2.6f,DecalWidth=.14f,DecalY=.025f;
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz;
        private readonly double[] key=new double[SortMax],sx=new double[SortMax],sy=new double[SortMax],sz=new double[SortMax],ss=new double[SortMax],sr=new double[SortMax],sg=new double[SortMax],sb=new double[SortMax],sa=new double[SortMax];
        private readonly int[] order=new int[SortMax];private int sorted;
        private static readonly float[] ConverterZ={7f,-7f};

        private static double H(int a,int b=0,int c=0)=>NativeIronworksKit.Hash(a,b,c);
        private static double Frac(double v)=>v-Math.Floor(v);
        public static int Count(int full,double quality,bool low)=>NativeBrickfallAtmosphere.Count(full,quality,low);

        /// <summary>Fills <see cref="Quads"/> and <see cref="Heat"/> for the presentation clock and the camera (position, right, up).</summary>
        public void Build(NativeIronFxScene scene,IronworksHazardView[] views,double time,double camX,double camY,double camZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,double quality,bool low)
        {
            Quads.Clear();HeatCount=0;ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;Sparks=Smoke=Halos=Telegraphs=Embers=Shafts=Steam=0;if(scene==null)return;
            IronworksHazardView V(int h)=>views!=null&&h<views.Length?views[h]:default;
            // Premultiplied pieces first, far to near (the roof's smoke layer, the plumes, the steam); then the additive pieces.
            Begin();if(!low)RoofSmoke(time,quality);Plumes(scene,views,time,quality,low);SteamFx(views,time,quality,low);MusicSteam(scene,quality,low);Flush();
            HaloFx(scene,views,time);if(!low)ShaftFx(time,quality);
            PourFx(V(NativeIronworksHazards.Ladle),time,quality,low);FurnaceFx(views,time,quality,low);MillFx(views,time,quality,low);ForgeFx(V(NativeIronworksHazards.Hammer),V(NativeIronworksHazards.Slag),time,quality,low);
            EmberFx(time,quality,low);MusicShower(quality,low);TelegraphFx(views,time);if(!low)HeatFx(views);
        }

        // ---------------- primitives ----------------
        private void Sprite(double x,double y,double z,double size,double r,double g,double b,double a,bool additive)
        {
            double h=size/2,ax=rx*h,ay=ry*h,az=rz*h,bx=ux*h,by=uy*h,bz=uz*h;
            Quads.Add(x-ax-bx,y-ay-by,z-az-bz,x+ax-bx,y+ay-by,z+az-bz,x+ax+bx,y+ay+by,z+az+bz,x-ax+bx,y-ay+by,z-az+bz,r,g,b,a,1,additive,1);
        }
        private void Sprite2(double x,double y,double z,double w,double h,double r,double g,double b,double a,bool additive)
        {
            double ax=rx*w/2,ay=ry*w/2,az=rz*w/2,bx=ux*h/2,by=uy*h/2,bz=uz*h/2;
            Quads.Add(x-ax-bx,y-ay-by,z-az-bz,x+ax-bx,y+ay-by,z+az-bz,x+ax+bx,y+ay+by,z+az+bz,x-ax+bx,y-ay+by,z-az+bz,r,g,b,a,1,additive,1);
        }
        // A streak from a to b `w` wide, facing the camera (sparks, rays).
        private void Streak(double ax,double ay,double az,double bx,double by,double bz,double w,double r,double g,double b,double a,int mode=0)
        {
            double dx=bx-ax,dy=by-ay,dz=bz-az,cx=(ax+bx)/2-ex,cy=(ay+by)/2-ey,cz=(az+bz)/2-ez;double px=dy*cz-dz*cy,py=dz*cx-dx*cz,pz=dx*cy-dy*cx,l=Math.Sqrt(px*px+py*py+pz*pz);if(l<1e-9)return;px*=w/2/l;py*=w/2/l;pz*=w/2/l;
            Quads.Add(ax-px,ay-py,az-pz,bx-px,by-py,bz-pz,bx+px,by+py,bz+pz,ax+px,ay+py,az+pz,r,g,b,a,mode,true,1);
        }
        // A flat floor quad (x0..x1, z0..z1) at height y.
        private void Flat(double x0,double x1,double z0,double z1,double y,double r,double g,double b,double a,int mode=0){Quads.Add(x0,y,z0,x1,y,z0,x1,y,z1,x0,y,z1,r,g,b,a,mode,true,1);}
        private double Dist(double x,double y,double z)=>Math.Sqrt((x-ex)*(x-ex)+(y-ey)*(y-ey)+(z-ez)*(z-ez));
        private void Begin()=>sorted=0;
        private void Queue(double x,double y,double z,double size,double r,double g,double b,double a)
        {
            if(sorted>=SortMax||!(a>.002))return;int i=sorted++;sx[i]=x;sy[i]=y;sz[i]=z;ss[i]=size;sr[i]=r;sg[i]=g;sb[i]=b;sa[i]=a;key[i]=Dist(x,y,z);order[i]=i;
        }
        private void Flush()
        {
            for(int i=1;i<sorted;i++){int v=order[i];double k=key[v];int j=i-1;while(j>=0&&key[order[j]]<k){order[j+1]=order[j];j--;}order[j+1]=v;}
            for(int n=0;n<sorted;n++){int i=order[n];Sprite(sx[i],sy[i],sz[i],ss[i],sr[i]*sa[i],sg[i]*sa[i],sb[i]*sa[i],sa[i],false);}
            sorted=0;
        }
        private void AddHeat(double x,double y,double z,double w,double h,double strength)
        {if(HeatCount>=HeatMax||strength<=.01)return;Heat[HeatCount++]=new NativeHeatSource{X=(float)x,Y=(float)y,Z=(float)z,W=(float)w,H=(float)h,Strength=(float)Math.Min(1,strength)};}

        // ---------------- smoke, plumes, steam (premultiplied, sorted) ----------------
        private void RoofSmoke(double time,double quality)
        {
            // A dark layer under the roof, drifting west, lit warm from below over the furnace line and the pour.
            int n=Count(34,quality,false);
            for(int i=0;i<n;i++)
            {
                double x=Frac(H(1,i)+time*.004)*96-48,z=-30+H(2,i)*62,y=13.2+H(3,i)*3.4,s=9+H(4,i)*7;
                double warm=Math.Max(0,1-Math.Abs(z-24)/9)*(Math.Abs(x)<22?1:.3)+Math.Max(0,1-Math.Sqrt((x-6)*(x-6)+(z+7)*(z+7))/10)*.8;
                Queue(x,y,z,s,.026+.09*warm,.022+.03*warm,.02+.008*warm,.32);Smoke++;
            }
        }
        private void Plumes(NativeIronFxScene s,IronworksHazardView[] views,double time,double quality,bool low)
        {
            // Hood smoke curling up into the flues over each furnace.
            int per=Count(6,quality,low);
            for(int f=0;f<s.Hoods.Length/3;f++)for(int i=0;i<per;i++)
            {
                double u=Frac(H(5,f,i)+time*.11),x=s.Hoods[f*3]+(H(6,f,i)-.5)*6*(1-u),y=10.6+u*5.5,z=26.9-(1-u)*.8;
                Queue(x,y,z,2.4+u*2.6,.06+.12*(1-u),.035+.04*(1-u),.025,.36*(1-u*.7));Smoke++;
            }
            // The converters' mouths and the coke standpipes.
            for(int c=0;c<2;c++){float cz=ConverterZ[c];for(int i=0;i<Count(7,quality,low);i++){double u=Frac(H(7,(int)cz,i)+time*.09),x=-43.6+(H(8,(int)cz,i)-.5)*1.4+u*1.2,y=9.6+u*6.5;Queue(x,y,cz+(H(9,(int)cz,i)-.5)*2*u,1.6+u*3.2,.08*(1-u)+.03,.04*(1-u)+.025,.022,.34*(1-u*.6));Smoke++;}}
            for(int p=0;p<s.Pipes.Length/3;p++)for(int i=0;i<Count(3,quality,low);i++){double u=Frac(H(10,p,i)+time*.12);Queue(s.Pipes[p*3]+(H(11,p,i)-.5)*.6,s.Pipes[p*3+1]+u*3,s.Pipes[p*3+2]-.3*u,.8+u*2,.03,.028,.026,.3*(1-u));Smoke++;}
            // Smoke leaking at the coke doors under the deck.
            for(int i=0;i<Count(10,quality,low);i++){double u=Frac(H(12,i)+time*.07),x=-48+H(13,i)*23;Queue(x,1+u*4.2,27.4-u*1.6,1.2+u*1.8,.05+.06*(1-u),.032,.024,.28*(1-u));Smoke++;}
            // The pour's smoke column while it pours (and a little after).
            var l=views!=null&&NativeIronworksHazards.Ladle<views.Length?views[NativeIronworksHazards.Ladle]:default;double pour=l.Phase==NativeHazardPhase.Active?1:l.Phase==NativeHazardPhase.Clear?l.Level:0;
            if(pour>0)for(int i=0;i<Count(9,quality,low);i++){double u=Frac(H(14,i)+time*.18);Queue(NativeIronworksKit.LandX+(H(15,i)-.5)*1.6*u,1.2+u*9,NativeIronworksKit.PourZ+(H(16,i)-.5)*1.6*u,1.5+u*3.5,.12*(1-u)+.03,.05*(1-u)+.025,.02,.32*pour*(1-u*.5));Smoke++;}
            // The slag tongue smokes while live and as it cools.
            var sv=views!=null&&NativeIronworksHazards.Slag<views.Length?views[NativeIronworksHazards.Slag]:default;double sl=sv.Phase==NativeHazardPhase.Active?1:sv.Phase==NativeHazardPhase.Clear?sv.Level:0;
            if(sl>0)for(int i=0;i<Count(8,quality,low);i++){double u=Frac(H(17,i)+time*.13);Queue(-38.5+H(18,i)*5,.6+u*5,-26+H(19,i)*3,1.4+u*2.4,.1*(1-u)+.03,.04,.02,.3*sl*(1-u*.6));Smoke++;}
        }
        private void SteamFx(IronworksHazardView[] views,double time,double quality,bool low)
        {
            // The quench tower: a hiss of mist at the grate while it warns, then the burst rising off the grate and out of the louvres.
            var v=views!=null&&NativeIronworksHazards.Steam<views.Length?views[NativeIronworksHazards.Steam]:default;
            double burst=v.Phase==NativeHazardPhase.Active?1:v.Phase==NativeHazardPhase.Clear?v.Level:0,hiss=v.Phase==NativeHazardPhase.Warn?v.Level:0;
            for(int i=0;i<Count(26,quality,low);i++)
            {
                double u=Frac(H(20,i)+time*(.22+.1*burst)),x=-44+(H(21,i)-.5)*3.4*(.4+u),z=18+(H(22,i)-.5)*3.4*(.4+u),y=.3+u*(burst>0?9:1.4);
                double a=burst*.42*(1-u*.8)+hiss*.25*(1-u);Queue(x,y,z,(burst>0?1.6:.8)+u*3.2,.62,.64,.66,a);Steam++;
            }
            // The stands' water sprays steam where the hot slab runs through.
            var sl=views!=null&&NativeIronworksHazards.Slab<views.Length?views[NativeIronworksHazards.Slab]:default;
            if(sl.Phase==NativeHazardPhase.Active)foreach(float sxp in NativeIronworksKit.StandX)
            {
                double head=NativeIronworksKit.FrameOrigin[NativeIronworksKit.FrameSlab*3]-NativeIronworksKit.SlabRunOf(sl);if(Math.Abs(head-sxp)>4.5)continue;double k=1-Math.Abs(head-sxp)/4.5;
                for(int i=0;i<Count(5,quality,low);i++){double u=Frac(H(23,(int)sxp,i)+time*.4);Queue(sxp+(H(24,(int)sxp,i)-.5)*1.4,1.6+u*2.6,-25+(H(25,(int)sxp,i)-.5)*1.8,.9+u*1.6,.6,.62,.64,.3*k*(1-u));Steam++;}
            }
        }

        // ---------------- light: halos, shafts ----------------
        private void HaloFx(NativeIronFxScene s,IronworksHazardView[] views,double time)
        {
            foreach(var g in s.Halos)
            {
                var c=new NativeIronLight{X=g.X,Y=g.Y,Z=g.Z,R=g.R,G=g.G,B=g.B,Range=g.Range,Param=g.Param,Kind=g.Kind,Frame=g.Frame,Hazard=g.Hazard,Cool=g.Kind==NativeIronworksKit.GlowMercury};
                float k=NativeIronworksLights.Level(c,views,time,Music);if(k<=.01f)continue;NativeIronworksKit.GlowAt(g,views,time,out float x,out float y,out float z);
                double size,a;float r=g.R,gg=g.G,b=g.B,m=Math.Max(r,Math.Max(gg,b));if(m>0){r/=m;gg/=m;b/=m;}
                switch(g.Kind)
                {
                    case NativeIronworksKit.GlowSodium:case NativeIronworksKit.GlowMercury:size=1.4;a=.38;y-=.1f;break;
                    case NativeIronworksKit.GlowOffice:case NativeIronworksKit.GlowLamp:size=1.2;a=.3;break;
                    case NativeIronworksKit.GlowBeacon:size=1.1;a=.55;break;
                    case NativeIronworksKit.GlowWindow:case NativeIronworksKit.GlowLed:size=1.4;a=.12;break;
                    case NativeIronworksKit.GlowPour:size=3.2;a=.42;y=1.1f;break;
                    default:size=Math.Max(1.2,g.Size*2.2);a=.16+.05*g.Size;break;
                }
                Sprite(x,y,z,size,r,gg,b,a*Math.Min(1.2,k),true);Halos++;
            }
        }
        private void ShaftFx(double time,double quality)
        {
            // Cold shafts down from the roof monitors through the haze (soft, faint), drifting with the dust.
            int per=Count(8,quality,false);
            foreach(float z in NativeIronworksKit.MonitorZ)for(int i=0;i<per;i++)
            {
                double x=-42+i*(84.0/Math.Max(1,per-1))+(H(26,(int)z,i)-.5)*3,top=17.7,bot=1.0;double dx=3.2,dz=1.6;
                double flick=.85+.15*Math.Sin(time*.3+i);Streak(x,top,z,x+dx,bot,z+dz,3.0,.012*flick,.017*flick,.026*flick,1,1);Shafts++;
            }
        }

        // ---------------- the pour ----------------
        private void PourFx(in IronworksHazardView l,double time,double quality,bool low)
        {
            double pour=l.Parked?0:l.Phase==NativeHazardPhase.Active?Math.Min(1,l.Since/.35):l.Phase==NativeHazardPhase.Clear?Math.Max(0,l.Level-.4)/.6:0;if(pour<=0)return;
            double lx=NativeIronworksKit.LandX,ly=NativeIronworksKit.LandY,lz=NativeIronworksKit.PourZ;
            Sprite(lx,ly+.2,lz,3.0,1,.62,.25,.5*pour,true);Sprite2(lx-.3,ly+3.6,lz,1.4,7.4,1,.55,.2,.14*pour,true);Halos+=2;
            // Sparks thrown off the splash: ballistic arcs, each on its own cycle, drawn as short streaks along their path.
            int n=Count(70,quality,low);
            for(int i=0;i<n;i++)
            {
                double life=.55+H(30,i)*.6,t=Frac(time/life+H(31,i))*life,a=H(32,i)*Math.PI*2,up=1.6+H(33,i)*3.2,out_=1.0+H(34,i)*2.6;
                double vx=Math.Cos(a)*out_,vz=Math.Sin(a)*out_,x0=lx+vx*t,y0=ly+up*t-4.9*t*t,z0=lz+vz*t;if(y0<.02)continue;double t1=t+.035,x1=lx+vx*t1,y1=ly+up*t1-4.9*t1*t1,z1=lz+vz*t1;
                double fade=1-t/life;Streak(x0,y0,z0,x1,y1,z1,.024+.014*H(35,i),3.0*fade,1.6*fade,.5*fade,1);Sparks++;
            }
        }
        // ---------------- the furnaces and the runners ----------------
        private void FurnaceFx(IronworksHazardView[] views,double time,double quality,bool low)
        {
            for(int k=0;k<3;k++)
            {
                double c=NativeIronworksKit.FurnaceX[k];
                // Sparks spat out of the open mouth (the west door; No 2's both), falling onto the deck.
                for(int d=0;d<(k==1?2:1);d++)
                {
                    double dx=d==0?c-2.5:c+2.5;int n=Count(MusicSparks(8),quality,low);
                    for(int i=0;i<n;i++)
                    {
                        double life=.9+H(40,k*3+d,i)*.8,t=Frac(time/life+H(41,k*3+d,i))*life;double vx=(H(42,k*3+d,i)-.5)*1.6,vz=-1.4-H(43,k*3+d,i)*1.8,vy=.6+H(44,k*3+d,i)*1.4;
                        double x0=dx+(H(45,k*3+d,i)-.5)*1.4+vx*t,y0=6.4+H(46,k*3+d,i)*1.6+vy*t-4.9*t*t,z0=27.8+vz*t;if(y0<6.02)continue;double t1=t+.03;
                        double x1=x0+vx*.03,y1=y0+(vy-9.8*t)*.03,z1=z0+vz*.03;double fade=1-t/life;Streak(x0,y0,z0,x1,y1,z1,.022,2.6*fade,1.3*fade,.4*fade,1);Sparks++;
                    }
                    // Warm rays out of the mouth over the deck.
                    if(!low)Streak(dx,7.4,27.6,dx,5.9,22.5,2.6,.16,.06,.015,1,1);
                }
                // The taphole bursts sparks as it taps and while it runs.
                var v=views!=null&&NativeIronworksHazards.R1+k<views.Length?views[NativeIronworksHazards.R1+k]:default;
                double tap=v.Phase==NativeHazardPhase.Warn?v.Level:v.Phase==NativeHazardPhase.Active?.6:0;
                if(tap>0)for(int i=0;i<Count(16,quality,low);i++)
                {
                    double life=.5+H(47,k,i)*.5,t=Frac(time/life+H(48,k,i))*life;double vx=(H(49,k,i)-.5)*2.4,vz=-1-H(50,k,i)*2.4,vy=1+H(51,k,i)*2.2;
                    double x0=c+vx*t,y0=.6+vy*t-4.9*t*t,z0=27.6+vz*t;if(y0<.02)continue;double fade=(1-t/life)*tap;Streak(x0,y0,z0,x0+vx*.03,y0+(vy-9.8*t)*.03,z0+vz*.03,.022,3*fade,1.5*fade,.45*fade,1);Sparks++;
                }
            }
        }
        // ---------------- the mill ----------------
        private void MillFx(IronworksHazardView[] views,double time,double quality,bool low)
        {
            var sl=views!=null&&NativeIronworksHazards.Slab<views.Length?views[NativeIronworksHazards.Slab]:default;
            if(sl.Phase==NativeHazardPhase.Active)
            {
                double head=NativeIronworksKit.FrameOrigin[NativeIronworksKit.FrameSlab*3]-NativeIronworksKit.SlabRunOf(sl);
                Sprite2(head,1.5,-25,7,1.6,1,.5,.18,.22,true);Halos++;
                foreach(float sxp in NativeIronworksKit.StandX)
                {
                    if(Math.Abs(head-sxp)>3.2)continue;double k=1-Math.Abs(head-sxp)/3.2;
                    for(int i=0;i<Count(12,quality,low);i++){double life=.4+H(52,(int)sxp,i)*.4,t=Frac(time/life+H(53,(int)sxp,i))*life;double vx=(H(54,(int)sxp,i)-.5)*3,vz=(H(55,(int)sxp,i)-.5)*3,vy=1+H(56,(int)sxp,i)*2;
                        double x0=sxp+vx*t,y0=1.3+vy*t-4.9*t*t,z0=-25+vz*t;if(y0<.02)continue;double f=(1-t/life)*k;Streak(x0,y0,z0,x0+vx*.03,y0+(vy-9.8*t)*.03,z0+vz*.03,.02,2.6*f,1.2*f,.35*f,1);Sparks++;}
                }
            }
            // The shear's cut: a burst where the blade meets the line.
            var sh=views!=null&&NativeIronworksHazards.Shear<views.Length?views[NativeIronworksHazards.Shear]:default;
            if(sh.Phase==NativeHazardPhase.Active&&sh.Since>.15)for(int i=0;i<Count(18,quality,low);i++)
            {
                double t=sh.Since-.15+H(57,i)*.1,vx=(H(58,i)-.5)*4,vz=(H(59,i)-.5)*4,vy=.5+H(60,i)*2.5;double x0=-15.5+vx*t,y0=1.3+vy*t-4.9*t*t,z0=-25+vz*t;if(y0<.02)continue;
                Streak(x0,y0,z0,x0+vx*.03,y0+(vy-9.8*t)*.03,z0+vz*.03,.02,3,1.6,.5,1);Sparks++;
            }
        }
        // ---------------- the drop forge and the slag ----------------
        private void ForgeFx(in IronworksHazardView h,in IronworksHazardView s,double time,double quality,bool low)
        {
            // The white glint on the billet as the ram lifts (the warning), the burst of scale and sparks at the blow.
            if(h.Phase==NativeHazardPhase.Warn){double g=.5+.5*Math.Sin(time*18);Sprite(-29.5,1.35,-28.4,.9,1,.96,.88,.5*h.Level*g,true);Halos++;}
            double since=h.Phase==NativeHazardPhase.Active?h.Since:h.Phase==NativeHazardPhase.Clear?h.Since+NativeIronworksHazards.Defs[NativeIronworksHazards.Hammer].Live:-1;
            if(since>=.1&&since<.75)for(int i=0;i<Count(24,quality,low);i++)
            {
                double t=since-.1,a=H(61,i)*Math.PI*2,o=1.5+H(62,i)*3,vy=.6+H(63,i)*2.6;double x0=-29.5+Math.Cos(a)*o*t,y0=1.25+vy*t-4.9*t*t,z0=-28.4+Math.Sin(a)*o*t;if(y0<.02)continue;double f=1-t/.65;
                Streak(x0,y0,z0,x0+Math.Cos(a)*o*.03,y0+(vy-9.8*t)*.03,z0+Math.Sin(a)*o*.03,.024,3*f,1.6*f,.5*f,1);Sparks++;
            }
            if(s.Phase==NativeHazardPhase.Active||s.Phase==NativeHazardPhase.Clear){double k=s.Phase==NativeHazardPhase.Active?Math.Min(1,s.Since/3):s.Level;Sprite2(-36.5,.6,-24.5,8,1.4,1,.45,.15,.2*k,true);Halos++;}
        }
        private void EmberFx(double time,double quality,bool low)
        {
            int n=Count(90,quality,low);
            for(int i=0;i<n;i++)
            {
                double life=6+H(70,i)*6,u=Frac(time/life+H(71,i));double x=-22+H(72,i)*56+Math.Sin(time*.6+i)*.6,z=-12+H(73,i)*40+Math.Cos(time*.5+i*1.3)*.6,y=.4+u*10;
                double f=Math.Sin(u*Math.PI)*(.6+.4*Math.Sin(time*5+i*2.1));Sprite(x,y,z,.07,2.4*f,1.1*f,.35*f,1,true);Embers++;
            }
        }

        // ---------------- the zone boss song (presentation only) ----------------
        // The furnace mouths' spark count: the section's sparks and the pulse (at most three times the authored count).
        private int MusicSparks(int full)=>!Music.On?full:(int)Math.Round(full*Math.Min(3,Music.Mood.Spark*(1+1.5*Music.Pulse)));
        // A steam puff out of each coke standpipe and over each furnace hood on the song's downbeats and slams, rising and spreading
        // over .9 s (Calm: none).
        private void MusicSteam(NativeIronFxScene s,double quality,bool low)
        {
            var m=Music;if(!m.On||m.Mode==NativeKitFlash.Calm||!(m.SinceDown<.9))return;double u=m.SinceDown/.9,a=.34*(1-u)*(1-u)*Math.Min(1.6,m.Mood.Steam)*(m.Mode==NativeKitFlash.Reduced?.6:1);if(a<.01)return;
            int per=Math.Max(1,Count(3,quality,low));
            for(int p=0;p<s.Pipes.Length/3;p++)for(int i=0;i<per;i++){double h=H(90,p,i);Queue(s.Pipes[p*3]+(h-.5)*.5,s.Pipes[p*3+1]+.2+u*(2.2+h),s.Pipes[p*3+2]-.25*u,.5+u*(1.6+h),.66,.67,.68,a);Steam++;}
            for(int f=0;f<s.Hoods.Length/3;f++)for(int i=0;i<per;i++){double h=H(91,f,i);Queue(s.Hoods[f*3]+(h-.5)*3,10.4+u*(2.6+h),26.6-u*.6,.9+u*(2.4+h),.6,.6,.62,a*.8);Steam++;}
        }
        // The drop's shower: sparks raining from the roof trusses over the casting floor for about two seconds after the song's
        // drop (none in Calm; thinner in Reduced and with the effects quality).
        private void MusicShower(double quality,bool low)
        {
            var m=Music;if(!m.On||m.Mode==NativeKitFlash.Calm||!(m.SinceDrop<2.4))return;double t=m.SinceDrop;
            int n=Count(m.Mode==NativeKitFlash.Reduced?70:150,quality,low);
            for(int i=0;i<n;i++)
            {
                double delay=H(92,i)*.55,age=t-delay,life=1.1+H(93,i)*.7;if(age<0||age>life)continue;
                double x0=-18+H(94,i)*44,z0=-11+H(95,i)*28,top=13.4+H(96,i)*1.4,vx=(H(97,i)-.5)*1.2,vz=(H(98,i)-.5)*1.2;
                double y=top-4.9*age*age-.6*age,x=x0+vx*age,z=z0+vz*age;if(y<.05)continue;double vy=-9.8*age-.6,dt=.06;
                double fade=(1-age/life)*(1-.6*age/life);Streak(x,y,z,x+vx*dt,y+vy*dt,z+vz*dt,.045,3.4*fade,1.6*fade,.5*fade,1);Sparks++;
            }
        }

        // ---------------- telegraphs ----------------
        private void Decal(double x0,double x1,double z0,double z1,double y,double k){Flat(x0,x1,z0,z1,y+DecalY,DecalR*DecalGain*k,DecalG*DecalGain*k,DecalB*DecalGain*k,1);Telegraphs++;}
        // A hard-edged outline of a floor rectangle, `w` wide, dashed when `dash` > 0 (the dashes march at 1 m/s).
        private void Outline(double x0,double x1,double z0,double z1,double y,double k,double w,double dash,double time)
        {
            if(dash<=0){Decal(x0,x1,z0,z0+w,y,k);Decal(x0,x1,z1-w,z1,y,k);Decal(x0,x0+w,z0+w,z1-w,y,k);Decal(x1-w,x1,z0+w,z1-w,y,k);return;}
            double off=Frac(time)*dash*2;
            for(double a=x0-off;a<x1;a+=dash*2){double s=Math.Max(x0,a),e=Math.Min(x1,a+dash);if(e>s){Decal(s,e,z0,z0+w,y,k);Decal(s,e,z1-w,z1,y,k);}}
            for(double a=z0-off;a<z1;a+=dash*2){double s=Math.Max(z0,a),e=Math.Min(z1,a+dash);if(e>s){Decal(x0,x0+w,s,e,y,k);Decal(x1-w,x1,s,e,y,k);}}
        }
        private void Ring(double cx,double cz,double r,double y,double k,int n,double time)
        {
            double spin=time*.5;for(int i=0;i<n;i++){double a0=spin+i*2*Math.PI/n,a1=a0+Math.PI/n*1.1;double ox=Math.Cos(a0)*r,oz=Math.Sin(a0)*r,px=Math.Cos(a1)*r,pz=Math.Sin(a1)*r,ix=Math.Cos(a0)*(r-DecalWidth),iz=Math.Sin(a0)*(r-DecalWidth),jx=Math.Cos(a1)*(r-DecalWidth),jz=Math.Sin(a1)*(r-DecalWidth);
                Quads.Add(cx+ox,y+DecalY,cz+oz,cx+px,y+DecalY,cz+pz,cx+jx,y+DecalY,cz+jz,cx+ix,y+DecalY,cz+iz,DecalR*DecalGain*k,DecalG*DecalGain*k,DecalB*DecalGain*k,1,0,true,1);Telegraphs++;}
        }
        /// <summary>A hazard's telegraph strength: a building, pulsing warning (2 Hz), steady while live, gone otherwise.</summary>
        public static double TelegraphLevel(in IronworksHazardView v,double time)=>v.Phase==NativeHazardPhase.Warn?(.55+.45*v.Level)*(.7+.3*Math.Max(0,Math.Sin(time*Math.PI*4))):v.Phase==NativeHazardPhase.Active?.75:0;
        private void TelegraphFx(IronworksHazardView[] views,double time)
        {
            IronworksHazardView V(int h)=>views!=null&&h<views.Length?views[h]:default;double k;var defs=NativeIronworksHazards.Defs;
            for(int r=0;r<3;r++){k=TelegraphLevel(V(NativeIronworksHazards.R1+r),time);if(k>0){double c=NativeIronworksKit.FurnaceX[r];Outline(c-1.05,c+1.05,NativeIronworksKit.RunnerEnd-.35,NativeIronworksKit.RunnerStart,0,k,.1,.6,time);}}
            k=TelegraphLevel(V(NativeIronworksHazards.Ladle),time);if(k>0){var d=defs[NativeIronworksHazards.Ladle];Outline(d.X-d.Sx/2,d.X+d.Sx/2,d.Z-d.Sz/2,d.Z+d.Sz/2,0,k,DecalWidth,.8,time);Ring(NativeIronworksKit.LandX,NativeIronworksKit.PourZ,1.4,.92,k,10,time);}
            k=TelegraphLevel(V(NativeIronworksHazards.Hammer),time);if(k>0)Ring(-29.5,-28.4,1.7,0,k,12,time);
            k=TelegraphLevel(V(NativeIronworksHazards.Slag),time);if(k>0){var d=defs[NativeIronworksHazards.Slag];Outline(d.X-d.Sx/2,d.X+d.Sx/2,d.Z-d.Sz/2,d.Z+d.Sz/2,0,k,DecalWidth,.7,time);}
            k=TelegraphLevel(V(NativeIronworksHazards.Slab),time);if(k>0){Outline(-20,29,-26.45,-23.55,0,k,.12,1.2,time);}
            k=TelegraphLevel(V(NativeIronworksHazards.Shear),time);if(k>0){Decal(-15.62,-15.38,-26.6,-23.4,0,k);}
            k=TelegraphLevel(V(NativeIronworksHazards.Steam),time);if(k>0)Ring(-44,18,2.0,0,k,12,time);
            k=TelegraphLevel(V(NativeIronworksHazards.Pusher),time);if(k>0){var d=defs[NativeIronworksHazards.Pusher];Outline(d.X-d.Sx/2-.2,d.X+d.Sx/2+.2,d.Z-d.Sz/2-.2,d.Z+d.Sz/2-.4,0,k,DecalWidth,.6,time);}
        }
        // ---------------- heat haze sources ----------------
        private void HeatFx(IronworksHazardView[] views)
        {
            IronworksHazardView V(int h)=>views!=null&&h<views.Length?views[h]:default;
            for(int k=0;k<3;k++){double c=NativeIronworksKit.FurnaceX[k];AddHeat(c-2.5,8.6,27.0,2.4,3.0,.8);if(k==1)AddHeat(c+2.5,8.6,27.0,2.2,2.6,.6);
                var v=V(NativeIronworksHazards.R1+k);double r=v.Phase==NativeHazardPhase.Active?1:v.Phase==NativeHazardPhase.Clear?v.Level:v.Phase==NativeHazardPhase.Warn?.4*v.Level:0;
                if(r>0)for(int i=0;i<3;i++)AddHeat(c,1.2,14+i*5,1.6,2.2,.7*r);}
            var l=V(NativeIronworksHazards.Ladle);if(l.Phase==NativeHazardPhase.Active)AddHeat(NativeIronworksKit.LandX-.2,3.6,NativeIronworksKit.PourZ,1.6,6.5,.9);
            var s=V(NativeIronworksHazards.Slab);AddHeat(28.4,2.0,-25,1.8,2.4,NativeIronworksKit.HazardGlow(s,.4f));
            for(int c=0;c<2;c++)AddHeat(-43.4,10.4,ConverterZ[c],2.6,2.4,.6);
            var g=V(NativeIronworksHazards.Slag);if(g.Phase==NativeHazardPhase.Active||g.Phase==NativeHazardPhase.Clear)AddHeat(-36,1.2,-24.5,5,2.4,g.Phase==NativeHazardPhase.Active?.8:.8*g.Level);
            AddHeat(-30.25,2.6,27.6,1.6,4.2,.6);AddHeat(-26,1.9,-32.2,2.2,1.6,.5);
        }
    }
}
