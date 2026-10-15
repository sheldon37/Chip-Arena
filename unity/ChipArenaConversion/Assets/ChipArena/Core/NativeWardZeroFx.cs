using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// What Ward Zero's effects need from the map, built once (<see cref="Build"/>): the kit's light sources (halos, pools,
    /// shafts), the floor vents and their song sides, the hazards by role, the drop hatches, the routes between the floors, and
    /// seeded sources for steam, sparks and drips. No allocation after Build.
    /// </summary>
    public sealed class NativeWardFxScene
    {
        public NativeWardGlow[] Glows=Array.Empty<NativeWardGlow>();public MapHazard[] Hazards=Array.Empty<MapHazard>();
        public int[] Burners=Array.Empty<int>(),Gases=Array.Empty<int>(),Shutters=Array.Empty<int>();public int[] Grids={-1,-1};
        /// <summary>Floor vents: entry index, place, the boss song's vent side (-1: none) and whether it is a pit.</summary>
        public int[] VentEntry=Array.Empty<int>(),VentSong=Array.Empty<int>();public float[] VentX=Array.Empty<float>(),VentY=Array.Empty<float>(),VentZ=Array.Empty<float>();
        /// <summary>Doors, cells and wall vents (light spill and dust when they open): entry index, the face point, outward normal.</summary>
        public int[] DoorEntry=Array.Empty<int>();public float[] DoorX=Array.Empty<float>(),DoorY=Array.Empty<float>(),DoorZ=Array.Empty<float>(),DoorNx=Array.Empty<float>(),DoorNz=Array.Empty<float>();
        /// <summary>Drop hatches: entry index, landing point, height, size.</summary>
        public int[] DropEntry=Array.Empty<int>();public float[] DropX=Array.Empty<float>(),DropY=Array.Empty<float>(),DropZ=Array.Empty<float>(),DropH=Array.Empty<float>(),DropSize=Array.Empty<float>();
        public NativeWardRoute[] Routes=Array.Empty<NativeWardRoute>();
        /// <summary>Seeded sources: steam leaks (x, y, z), spark fixtures (troffer glows that fail), drips (ceiling, floor).</summary>
        public float[] SteamX=Array.Empty<float>(),SteamY=Array.Empty<float>(),SteamZ=Array.Empty<float>();
        public int[] SparkGlow=Array.Empty<int>(),DripGlow=Array.Empty<int>();
        public static NativeWardFxScene Build(ContentBlock[] blocks,MapHazard[] hazards,NativeWardZeroKit kit,NativeWardEntry[] entries)
        {
            var s=new NativeWardFxScene{Hazards=hazards??Array.Empty<MapHazard>()};
            if(kit!=null){s.Glows=kit.Glows;s.Burners=kit.Burners;s.Gases=kit.Gases;s.Shutters=kit.Shutters;s.Grids=(int[])kit.Grids.Clone();s.Routes=kit.Routes;}
            var ve=new List<int>();var vs=new List<int>();var vx=new List<float>();var vy=new List<float>();var vz=new List<float>();
            var de=new List<int>();var dx=new List<float>();var dy=new List<float>();var dz=new List<float>();var dnx=new List<float>();var dnz=new List<float>();
            var pe=new List<int>();var px=new List<float>();var py=new List<float>();var pz=new List<float>();var ph=new List<float>();var ps=new List<float>();
            for(int i=0;i<(entries?.Length??0);i++)
            {
                var e=entries[i];if(e==null)continue;var f=e.From;var x=e.Exit;
                if(e.kind=="vent"&&e.Surface=="floor"){ve.Add(i);vs.Add(e.song);vx.Add(f.x);vy.Add(f.y);vz.Add(f.z);}
                else if(e.kind=="drop"){pe.Add(i);px.Add(x.x);py.Add(x.y);pz.Add(x.z);ph.Add(e.height>0?e.height:NativeHordeDrop.DefaultHeight);ps.Add(e.hulk?5.2f:3f);}
                else if(e.kind=="door"||e.kind=="cell"||e.kind=="vent"&&e.Surface=="wall")
                {float nx=x.x-f.x,nz=x.z-f.z,l=(float)Math.Sqrt(nx*nx+nz*nz);if(l<1e-4f)continue;nx/=l;nz/=l;de.Add(i);dx.Add(f.x+nx*(e.kind=="vent"?.05f:.65f));dy.Add(f.y);dz.Add(f.z+nz*(e.kind=="vent"?.05f:.65f));dnx.Add(nx);dnz.Add(nz);}
            }
            s.VentEntry=ve.ToArray();s.VentSong=vs.ToArray();s.VentX=vx.ToArray();s.VentY=vy.ToArray();s.VentZ=vz.ToArray();
            s.DoorEntry=de.ToArray();s.DoorX=dx.ToArray();s.DoorY=dy.ToArray();s.DoorZ=dz.ToArray();s.DoorNx=dnx.ToArray();s.DoorNz=dnz.ToArray();
            s.DropEntry=pe.ToArray();s.DropX=px.ToArray();s.DropY=py.ToArray();s.DropZ=pz.ToArray();s.DropH=ph.ToArray();s.DropSize=ps.ToArray();
            // Sparks from the fixtures that fail worst, drips from a handful of others, steam leaks along the pipes over the halls.
            var sg=new List<int>();var dg=new List<int>();
            for(int i=0;i<s.Glows.Length;i++){var g=s.Glows[i];if(g.Kind!=NativeWardZeroKit.GlowFluor)continue;if(g.Param<.035f)sg.Add(i);else if(g.Param>=.08f&&g.Param<.13f)dg.Add(i);}
            s.SparkGlow=sg.ToArray();s.DripGlow=dg.ToArray();
            var sx=new List<float>();var sy=new List<float>();var sz=new List<float>();
            for(int i=0;i<16;i++){double h=NativeWardZeroKit.Hash("ward-steam",i);float x=(float)(-34+68*h),side=i%2==0?1:-1,z=side*(i%4<2?18.6f:14.9f);float y=i%3==0?11.3f:4.6f;sx.Add(x);sy.Add(y);sz.Add(z);}
            s.SteamX=sx.ToArray();s.SteamY=sy.ToArray();s.SteamZ=sz.ToArray();
            return s;
        }
    }

    /// <summary>
    /// Builds Ward Zero's camera-dependent quads for one frame into a <see cref="NativeFxQuads"/> (Runtime draws them as one
    /// solid and one effects batch: two draws), docs/arenas/WARDZERO_PROPOSAL.md section 10:
    ///  - light (scaled by quality and the low tier): halos round the fixtures that flicker with them, soft light pools on the
    ///    floors under them, light shafts in the haze under the high-bays and down the Spine over the Cradle, dust motes in the
    ///    shafts, low haze that thickens with the mood and takes its red and violet;
    ///  - life (scaled): steam leaks, sparks from failing fixtures, drips from the ceiling with their splash rings, the ink's
    ///    breath over the Cradle (wisps, rings on the beat) as the corruption spreads;
    ///  - telegraphs (never reduced, ARENA_PROPOSAL.md section 2.1 rule 4): the burners' pilot flames, heat and fire columns and
    ///    embers; the bridges' arcs and sparks; the decon locks' jets and gas; the shutters' rotating beacons, their closing edge's
    ///    sparks and the red line under a shut one; the song vents' rims, wisps and eruptions; the emergency beacons' beams;
    ///  - entrances (never reduced): a floor vent's rim and dust as it opens, light and dust from opening doors and grilles,
    ///    the drop hatches' warning shaft and falling grit;
    ///  - routes between the floors (never reduced): a soft light column and pool at every foot (cyan-white, up) and every top
    ///    (warm, down).
    /// Pure: the same clock, camera, light, quality, states, entries' open amounts, song vents and mood give the same quads.
    /// Allocation free.
    /// </summary>
    public sealed class NativeWardZeroFx
    {
        public const int Capacity=4200;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        public const int HazeFull=46,MoteFull=90,HaloRange=46,PoolRange=32;
        /// <summary>The floor pools' glow (alpha: <see cref="PoolShare"/> times the fixture's level, at most <see cref="PoolAlpha"/>; was
        /// .04 and .14 before the owner's second darkening, 2026-10-15) and the low haze's shade (times its grey-teal, red and violet;
        /// it never lifts the dark).</summary>
        public const double PoolShare=.025,PoolAlpha=.085,HazeShade=.6;
        public int Halos,Pools,Shafts,Haze,Motes,Steam,Sparks,Drips,Ink,Telegraph,Entrances,Guide;
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz;private NativeFxLight light;

        private static uint Mix(uint x){unchecked{x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x;}}
        private static double H(int a,int b=0,int c=0){unchecked{return Mix(Mix(Mix((uint)a+0x9E3779B9U)^(uint)b*0x85EBCA6BU)+(uint)c*0xC2B2AE35U)/4294967296.0;}}
        private static double Frac(double v)=>v-Math.Floor(v);
        private static double Lin(int hex,int shift)=>NativeSceneryBuilder.Channel(hex,shift);
        public static int Count(int full,double quality,bool low)=>NativeBrickfallAtmosphere.Count(full,quality,low);

        /// <summary>
        /// Fills <see cref="Quads"/>. `open` holds each entry's open amount this frame (NativeVentRules.Open; null: all shut);
        /// `songActive` and `songWarn` the boss song's two vent sides (NativeBossTrack.Vent; null: quiet). `states` indexes the
        /// scene's hazards (null: all idle).
        /// </summary>
        public void Build(NativeWardFxScene scene,double time,double camX,double camY,double camZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,NativeFxLight sun,double quality,bool low,in NativeWardMood mood,NativeHazardState[] states,float[] open,bool[] songActive,float[] songWarn)
        {
            Quads.Clear();ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;light=sun;Halos=Pools=Shafts=Haze=Motes=Steam=Sparks=Drips=Ink=Telegraph=Entrances=Guide=0;
            // Telegraphs, entrances and the way between the floors first: they are never reduced and must fit.
            BurnerFx(scene,states,time);GridFx(scene,states,time);GasFx(scene,states,time);ShutterFx(scene,states,time);SongVentFx(scene,open,songActive,songWarn,time);BeaconFx(scene,mood,time);
            EntranceFx(scene,open,time);GuideFx(scene,mood,time);
            HaloFx(scene,mood,time,quality,low);PoolFx(scene,mood,time,quality,low);ShaftFx(scene,mood,time,quality,low);InkFx(mood,time,quality,low);
            SteamFx(scene,time,quality,low);SparkFx(scene,mood,time,quality,low);DripFx(scene,time,quality,low);HazeFx(mood,time,quality,low);MoteFx(scene,mood,time,quality,low);
        }

        // ---------------- primitives ----------------
        private void Sprite(double x,double y,double z,double size,double r,double g,double b,double a,bool additive,int layer=1)
        {
            double h=size/2,ax=rx*h,ay=ry*h,az=rz*h,bx=ux*h,by=uy*h,bz=uz*h;
            Quads.Add(x-ax-bx,y-ay-by,z-az-bz,x+ax-bx,y+ay-by,z+az-bz,x+ax+bx,y+ay+by,z+az+bz,x-ax+bx,y-ay+by,z-az+bz,r,g,b,a,1,additive,layer);
        }
        private void Flat(double x,double y,double z,double ax,double az,double halfLength,double halfWidth,double r,double g,double b,double a,int mode,bool additive)
        {
            double l=Math.Sqrt(ax*ax+az*az);if(l<1e-6){ax=1;az=0;l=1;}ax/=l;az/=l;double px_=-az,pz_=ax;
            double lx=ax*halfLength,lz=az*halfLength,wx=px_*halfWidth,wz=pz_*halfWidth;
            Quads.Add(x-lx-wx,y,z-lz-wz,x+lx-wx,y,z+lz-wz,x+lx+wx,y,z+lz+wz,x-lx+wx,y,z-lz+wz,r,g,b,a,mode,additive,1);
        }
        // A soft light shaft (radial falloff across and along): it fades as the eye lines up with it.
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
        // A cone of light from (x, y, z) straight down `length`, `r0` wide at the top, `r1` at the bottom (two crossed ribbons
        // facing the eye as a trapezoid with soft falloff).
        private void Cone(double x,double y,double z,double length,double r0,double r1,double r,double g,double b,double a)
        {
            double mx=x-ex,mz=z-ez,ml=Math.Sqrt(mx*mx+mz*mz);if(ml<1e-6){mx=1;mz=0;ml=1;}double sx=-mz/ml,sz=mx/ml;double yb=y-length;
            Quads.Add(x-sx*r0,y,z-sz*r0,x-sx*r1,yb,z-sz*r1,x+sx*r1,yb,z+sz*r1,x+sx*r0,y,z+sz*r0,r,g,b,a,1,true,1);
        }
        private static double D2(double x,double y,double z,double a,double b,double c){double dx=x-a,dy=y-b,dz=z-c;return dx*dx+dy*dy+dz*dz;}
        private bool Near(double x,double y,double z,double range)=>D2(x,y,z,ex,ey,ez)<range*range;
        private bool Ahead(double x,double y,double z,double slack=4){double dx=x-ex,dy=y-ey,dz=z-ez,fx=uy*rz-uz*ry,fy=uz*rx-ux*rz,fz=ux*ry-uy*rx;return dx*fx+dy*fy+dz*fz>-slack;}
        private static NativeHazardState S(NativeHazardState[] states,int i)=>states!=null&&i>=0&&i<states.Length?states[i]:default;

        // ---------------- light ----------------
        private void HaloFx(NativeWardFxScene s,in NativeWardMood m,double time,double q,bool low)
        {
            int cap=Count(420,q,low),n=0;
            for(int i=0;i<s.Glows.Length&&n<cap;i++)
            {
                var g=s.Glows[i];if(!Near(g.X,g.Y,g.Z,HaloRange)||!Ahead(g.X,g.Y,g.Z))continue;float level;
                switch(g.Kind)
                {
                    case NativeWardZeroKit.GlowFluor:case NativeWardZeroKit.GlowHighBay:level=NativeWardZeroKit.FluorLevel(g.Param,m,time);break;
                    case NativeWardZeroKit.GlowTank:level=.55f+.25f*m.Beat;break;
                    case NativeWardZeroKit.GlowScreen:level=.35f*(.6f+.4f*m.Fluor);break;
                    default:continue;
                }
                if(level<.05f)continue;double a=Math.Min(.55,.16*level);Sprite(g.X,g.Y-.05,g.Z,g.Size*(g.Kind==NativeWardZeroKit.GlowHighBay?1.8:1.3),g.R,g.G,g.B,a,true);n++;Halos++;
            }
        }
        private void PoolFx(NativeWardFxScene s,in NativeWardMood m,double time,double q,bool low)
        {
            int cap=Count(260,q,low),n=0;
            for(int i=0;i<s.Glows.Length&&n<cap;i++)
            {
                var g=s.Glows[i];if(!Near(g.X,g.Floor,g.Z,PoolRange))continue;float level;double rad;
                switch(g.Kind)
                {
                    case NativeWardZeroKit.GlowFluor:level=NativeWardZeroKit.FluorLevel(g.Param,m,time);rad=2.3;break;
                    case NativeWardZeroKit.GlowHighBay:level=NativeWardZeroKit.FluorLevel(g.Param,m,time);rad=3.6;break;
                    case NativeWardZeroKit.GlowTank:level=.9f+.4f*m.Beat;rad=2.2;break;
                    case NativeWardZeroKit.GlowScreen:level=.3f;rad=1.2;break;
                    default:continue;
                }
                if(level<.05f)continue;double r=g.R,gg=g.G,b=g.B;float red=m.Red*.4f;r=r*(1-red)+red;gg*=1-red*.8f;b*=1-red*.8f;
                // A faint glow over the floor's pool (the light itself is the ceiling light field's, NativeWardZeroField).
                Flat(g.X,g.Floor+.03,g.Z,1,0,rad,rad,r,gg,b,Math.Min(PoolAlpha,PoolShare*level),1,true);n++;Pools++;
            }
        }
        private void ShaftFx(NativeWardFxScene s,in NativeWardMood m,double time,double q,bool low)
        {
            if(low&&q<.5)return;int cap=Count(40,q,low),n=0;double haze=Math.Max(.4,m.Haze);
            for(int i=0;i<s.Glows.Length&&n<cap;i++)
            {
                var g=s.Glows[i];
                if(g.Kind==NativeWardZeroKit.GlowSpine)
                {
                    // The Spine: a cold column down the hatch onto the Cradle, violet as the ink spreads, pumping with the song.
                    double ink=m.Ink,k=(.08+.06*m.Beat*m.Groove)*haze;double r=.75*(1-ink*.5)+.5*ink,gg=.85*(1-ink*.7),b=1;
                    Cone(g.X,g.Y,g.Z,g.Y-.2,2.4,3.4,r,gg,b,k);Cone(g.X,g.Y,g.Z,g.Y-.2,1.2,1.8,r,gg,b,k*1.4);Flat(g.X,.04,g.Z,1,0,3.6,3.6,r,gg,b,.16*haze,1,true);n+=3;Shafts+=3;continue;
                }
                if(g.Kind!=NativeWardZeroKit.GlowHighBay||!Near(g.X,g.Y,g.Z,60))continue;float level=NativeWardZeroKit.FluorLevel(g.Param,m,time);if(level<.1f)continue;
                Cone(g.X,g.Y,g.Z,Math.Min(g.Y-.2,9),.7,2.6,g.R,g.G,g.B,.05*level*haze);n++;Shafts++;
            }
        }
        private void HazeFx(in NativeWardMood m,double time,double q,bool low)
        {
            // Low haze round the eye on its own floor, drifting, in the mood's colour (a dark grey-teal that never lifts the dark,
            // red, violet).
            int n=Count(HazeFull,q,low);double floor=ey>6.6?6:0;double rr=HazeShade*(.17*(1-m.Red)+.5*m.Red+.12*m.Ink),gg=HazeShade*(.21*(1-m.Red)+.07*m.Red),bb=HazeShade*(.23*(1-m.Red)+.09*m.Red+.25*m.Ink);
            for(int i=0;i<n;i++)
            {
                double a=H(i,1)*Math.PI*2,rad=4+H(i,2)*24,drift=time*(.15+.1*H(i,3));double x=ex+Math.Sin(a+drift*.05)*rad+Math.Sin(drift+i)*1.5,z=ez+Math.Cos(a+drift*.05)*rad+Math.Cos(drift*.8+i)*1.5;
                double y=floor+.4+H(i,4)*2.2;double size=3+H(i,5)*5;double alpha=.035*m.Haze*(1-Math.Max(0,(rad-18)/12));if(alpha<=.002)continue;
                Sprite(x,y,z,size,rr,gg,bb,alpha,false,1);Haze++;
            }
        }
        private void MoteFx(NativeWardFxScene s,in NativeWardMood m,double time,double q,bool low)
        {
            // Dust in the shafts: the Spine and the high-bays nearest the eye.
            int n=Count(MoteFull,q,low);if(n<=0)return;int k=0;
            for(int i=0;i<s.Glows.Length&&k<n;i++)
            {
                var g=s.Glows[i];if(g.Kind!=NativeWardZeroKit.GlowSpine&&(g.Kind!=NativeWardZeroKit.GlowHighBay||!Near(g.X,g.Y,g.Z,24)))continue;int per=g.Kind==NativeWardZeroKit.GlowSpine?40:8;
                for(int j=0;j<per&&k<n;j++,k++)
                {
                    double t=Frac(time*.03+H(i,j)),a=H(i,j,1)*Math.PI*2,rad=Math.Sqrt(H(i,j,2))*(g.Kind==NativeWardZeroKit.GlowSpine?2.6:1.6);
                    double x=g.X+Math.Sin(a+time*.1)*rad,z=g.Z+Math.Cos(a+time*.1)*rad,y=g.Y-.3-(g.Y-.5)*t;Sprite(x,y,z,.05,1,.96,.9,.5*Math.Sin(t*Math.PI),true);Motes++;
                }
            }
        }

        // ---------------- life ----------------
        private void SteamFx(NativeWardFxScene s,double time,double q,bool low)
        {
            int per=Math.Max(2,Count(7,q,low));
            for(int i=0;i<s.SteamX.Length;i++)
            {
                double x=s.SteamX[i],y=s.SteamY[i],z=s.SteamZ[i];if(!Near(x,y,z,34))continue;
                for(int j=0;j<per;j++){double t=Frac(time*.35+H(i,j));double px=x+(H(i,j,1)-.5)*.3+t*.8,py=y-.1+t*1.6,pz=z+(z>0?-1:1)*t*.6;Sprite(px,py,pz,.3+t*1.4,.78,.82,.84,.12*(1-t)*Math.Min(1,t*6),false);Steam++;}
            }
            // The furnaces' smoke and the burner lanes' heat haze at rest.
            foreach(int k in s.Burners){var h=s.Hazards[k];if(h==null)continue;double fx=h.center.x+Math.Sign(h.center.x)*3.2;if(!Near(fx,1.5,0,30))continue;
                for(int j=0;j<per;j++){double t=Frac(time*.25+H(k,j,7));Sprite(fx-Math.Sign(h.center.x)*t*1.5,3.4+t*2.5,(H(k,j,8)-.5)*3,.6+t*2,.18,.16,.15,.18*(1-t),false);Steam++;}}
        }
        private void SparkFx(NativeWardFxScene s,in NativeWardMood m,double time,double q,bool low)
        {
            int per=Math.Max(3,Count(8,q,low));double every=3.2-1.8*m.Flicker;
            for(int i=0;i<s.SparkGlow.Length;i++)
            {
                var g=s.Glows[s.SparkGlow[i]];if(!Near(g.X,g.Y,g.Z,30))continue;double phase=time/every+H(i,9);int burst=(int)Math.Floor(phase);double t=(phase-burst)*every;if(t>.8||H(burst,i)>.55)continue;
                for(int j=0;j<per;j++)
                {
                    double vx=(H(burst,i,j)-.5)*3,vz=(H(burst,i,j+40)-.5)*3,vy=-H(burst,i,j+80)*1.5;double x=g.X+vx*t,z=g.Z+vz*t,y=g.Y+vy*t-4.9*t*t;if(y<g.Floor)continue;
                    Line(x,y,z,x-vx*.04,y-(vy-9.8*t)*.04,z-vz*.04,.025,1,.82,.45,.9*(1-t/.8),true,0);Sparks++;
                }
                Sprite(g.X,g.Y-.05,g.Z,.6,1,.9,.6,.5*(1-t/.8),true);Sparks++;
            }
        }
        private void DripFx(NativeWardFxScene s,double time,double q,bool low)
        {
            for(int i=0;i<s.DripGlow.Length;i++)
            {
                var g=s.Glows[s.DripGlow[i]];double x=g.X+1.1,z=g.Z+.6;if(!Near(x,g.Y,z,22))continue;double fall=g.Y-g.Floor,period=1.4+H(i,3)*1.8,dur=Math.Sqrt(2*fall/9.8);double t=Frac(time/period+H(i,4))*period;
                if(t<dur){double y=g.Y-.5*9.8*t*t;Line(x,y,z,x,y+.12,z,.018,.7,.78,.85,.45,false,0);Drips++;}
                else if(t<dur+.45){double u=(t-dur)/.45;Flat(x,g.Floor+.025,z,1,0,.06+u*.35,.06+u*.35,.75,.8,.85,.25*(1-u),1,true);Drips++;}
            }
        }
        private void InkFx(in NativeWardMood m,double time,double q,bool low)
        {
            if(m.Ink<.05f)return;double k=m.Ink;
            // Rings out from the Cradle on the beat (each a soft violet band), wisps rising from the vessel, a dark bloom on the floor.
            for(int i=0;i<3;i++){double t=Frac(time*.35+i/3.0),rad=1.5+t*(4+20*k);Flat(0,.035,0,1,0,rad,rad,.55,.16,1,.06*k*(1-t)*(.5+m.Beat),1,true);Ink++;}
            Flat(0,.03,0,1,0,6+14*k,6+14*k,.03,.01,.05,.5*k,1,false);Ink++;
            int n=Count(26,q,low);for(int i=0;i<n;i++){double t=Frac(time*.18+H(i,21)),a=H(i,22)*Math.PI*2,rad=3.2+H(i,23)*2.6;double x=Math.Sin(a)*rad,z=Math.Cos(a)*rad;Sprite(x,.3+t*5,z,.8+t*2.4,.18,.05,.3,.22*k*(1-t),false);Ink++;}
        }

        // ---------------- telegraphs ----------------
        private void BurnerFx(NativeWardFxScene s,NativeHazardState[] states,double time)
        {
            foreach(int k in s.Burners)
            {
                var h=s.Hazards[k];if(h?.zones==null)continue;var st=S(states,k);
                foreach(var z in h.zones)
                {
                    double x0=z.center.x-z.size.x/2,cz=z.center.z,w=z.size.x,depth=z.size.z;int cols=6;
                    if(st.Phase==NativeHazardPhase.Idle){for(int i=0;i<cols;i++){double x=x0+w*(i+.5)/cols;Sprite(x,.08,cz,.18,.3,.5,1,.35,true);Telegraph++;}continue;}
                    if(st.Phase==NativeHazardPhase.Warn)
                    {
                        // Pilot flames rising along the row, the grate's glow, heat shimmer.
                        for(int i=0;i<cols*2;i++){double x=x0+w*(i+.5)/(cols*2),fl=.25+.5*st.Level*(.7+.3*Math.Sin(time*25+i));Sprite(x,.08+fl*.4,cz+(H(k,i)-.5)*depth*.6,.25+fl*.4,1,.55,.2,.6,true);Telegraph++;}
                        Flat(z.center.x,.04,cz,1,0,w/2,depth/2,1,.38,.08,.28*st.Level,1,true);Telegraph++;
                        for(int i=0;i<4;i++){double t=Frac(time*1.3+i*.25);Sprite(x0+w*(i+.5)/4,.4+t*2.4,cz,1.4,1,.5,.2,.05*st.Level*(1-t),true);Telegraph++;}
                        continue;
                    }
                    if(st.Phase==NativeHazardPhase.Active)
                    {
                        // The fire: stacked flame sprites to 3.5 m, licking and flickering; embers above; the row lit white-orange.
                        double tall=z.size.y;Flat(z.center.x,.05,cz,1,0,w/2+.4,depth/2+.4,1,.5,.15,.45,1,true);Telegraph++;
                        for(int i=0;i<cols;i++)for(int j=0;j<5;j++)
                        {
                            double x=x0+w*(i+.5)/cols,t=Frac(time*1.8+H(k,i,j)),y=.2+t*tall,size=(1.4-t*.6)*(depth*.55),flick=.8+.2*Math.Sin(time*31+i*3+j);
                            double r=1,g=.66-.42*t,b=.22-.2*t;Sprite(x+(H(k,i,j+9)-.5)*.6,y,cz+(H(k,i,j+19)-.5)*depth*.5,size*(.85-.25*t),r,g,Math.Max(0,b),.55*(1-t)*flick,true);Telegraph++;
                        }
                        for(int i=0;i<10;i++){double t=Frac(time*.9+H(k,i,50));Sprite(x0+H(k,i,51)*w,tall*.8+t*2.4,cz+(H(k,i,52)-.5)*depth,.06,1,.7,.3,.9*(1-t),true);Telegraph++;}
                        continue;
                    }
                    // Clear: smoke rolling off the grate.
                    for(int i=0;i<cols;i++){double t=Frac(time*.6+H(k,i,70));Sprite(x0+w*(i+.5)/cols,.4+t*2.8,cz,1+t*1.6,.14,.13,.12,.3*st.Level*(1-t),false);Telegraph++;}
                }
            }
        }
        private void GridFx(NativeWardFxScene s,NativeHazardState[] states,double time)
        {
            for(int side=0;side<2;side++)
            {
                int k=s.Grids[side];if(k<0||k>=s.Hazards.Length)continue;var h=s.Hazards[k];var st=S(states,k);double cx=h.center.x,cy=h.center.y-h.size.y/2+.05,cz=h.center.z,hx=h.size.x/2,hz=h.size.z/2;
                if(st.Phase==NativeHazardPhase.Warn)
                {
                    for(int e=-1;e<=1;e+=2)for(int i=0;i<3;i++){double t=Frac(time*4+i*.33),x=cx+(H(k,i,e+5)-.5)*hx*2;Sprite(x,cy+.3+t*.3,cz+e*(hz-.3),.3,.55,.85,1,.6*st.Level*(1-t),true);Telegraph++;}
                    Flat(cx,cy+.02,cz,0,1,hz,hx,.3,.75,1,.12*st.Level,1,true);Telegraph++;
                }
                else if(st.Phase==NativeHazardPhase.Active)
                {
                    // Arcs: zigzag bolts along and across the deck, re-rolled 20 times a second; sparks; the deck's glow.
                    int frame=(int)Math.Floor(time*20);Flat(cx,cy+.02,cz,0,1,hz,hx,.6,.9,1,.3,1,true);Telegraph++;
                    for(int a=0;a<9;a++)
                    {
                        double z0=cz-hz+H(frame,a,1)*hz*2,x=cx-hx,y=cy+.2;
                        for(int seg=0;seg<5;seg++){double nx=x+hx*2/5,nz=z0+(H(frame,a,seg+3)-.5)*.8,ny=cy+.15+H(frame,a,seg+9)*.5;Line(x,y,z0,nx,ny,nz,.05,.75,.92,1,.9,true,0);x=nx;z0=nz;y=ny;Telegraph++;}
                    }
                    for(int i=0;i<12;i++){double t=Frac(time*2.5+H(k,i,30));Sprite(cx+(H(k,i,31)-.5)*hx*2,cy+.1+t*.8,cz+(H(k,i,32)-.5)*hz*2,.08,.8,.95,1,.9*(1-t),true);Telegraph++;}
                }
                else if(st.Phase==NativeHazardPhase.Clear){for(int i=0;i<6;i++){double t=Frac(time*1.5+H(k,i,40));Sprite(cx+(H(k,i,41)-.5)*hx*2,cy+.1+t*.4,cz+(H(k,i,42)-.5)*hz*2,.07,.6,.85,1,.5*st.Level*(1-t),true);Telegraph++;}}
            }
        }
        private void GasFx(NativeWardFxScene s,NativeHazardState[] states,double time)
        {
            foreach(int k in s.Gases)
            {
                var h=s.Hazards[k];var st=S(states,k);if(st.Phase==NativeHazardPhase.Idle)continue;double cx=h.center.x,cz=h.center.z,y0=h.center.y-h.size.y/2,top=h.center.y+h.size.y/2,hx=h.size.x/2,hz=h.size.z/2;
                if(st.Phase==NativeHazardPhase.Warn)
                {
                    // Jets from the nozzles: thin streams to the floor, building, a cold mist at their feet.
                    for(int i=0;i<3;i++)for(int j=0;j<2;j++){double x=cx+(i-1)*h.size.x/3,z=cz+(j-.5)*hz;double len=(top-y0-.4)*Math.Min(1,st.Level*1.6);Line(x,top-.3,z,x,top-.3-len,z,.08+.1*st.Level,.75,1,.85,.35*st.Level,true,0);Sprite(x,top-.3-len,z,.6,.8,1,.9,.2*st.Level,false);Telegraph+=2;}
                    continue;
                }
                // The gas: soft clouds filling the lock from the floor up while active, thinning while it clears.
                double fill=st.Phase==NativeHazardPhase.Active?Math.Min(1,.35+st.Since/1.5):st.Level;int n=st.Phase==NativeHazardPhase.Active?42:30;
                for(int i=0;i<n;i++)
                {
                    double t=Frac(time*.12+H(k,i,1)),x=cx+(H(k,i,2)*2-1)*(hx-.6)+Math.Sin(time*.7+i)*.4,z=cz+(H(k,i,3)*2-1)*(hz-.6)+Math.Cos(time*.6+i)*.4,y=y0+.5+H(k,i,4)*(top-y0-1)*fill;
                    Sprite(x,y,z,2.2+H(k,i,5)*2.4,.72,.95,.8,.2*fill*(.6+.4*Math.Sin(t*Math.PI)),false);Telegraph++;
                }
            }
        }
        private void ShutterFx(NativeWardFxScene s,NativeHazardState[] states,double time)
        {
            foreach(int k in s.Shutters)
            {
                var h=s.Hazards[k];var st=S(states,k);if(st.Phase==NativeHazardPhase.Idle)continue;bool alongX=h.size.x>h.size.z;double cx=h.center.x,cz=h.center.z,top=h.center.y+h.size.y/2,w=alongX?h.size.x:h.size.z;
                // Rotating beacons at both ends of the housing on both faces: amber in the warning, red while it closes and is shut.
                bool red=st.Phase!=NativeHazardPhase.Warn;double r=1,g=red?.1:.55,b=red?.08:.05;
                for(int e=-1;e<=1;e+=2)for(int side=-1;side<=1;side+=2)
                {
                    double bx=alongX?cx+e*(w/2+.05):cx+side*.5,bz=alongX?cz+side*.5:cz+e*(w/2+.05),by=top+.2;double turn=time*2.2+e*.5+side*.25;
                    double dx=Math.Sin(turn*Math.PI*2),dz=Math.Cos(turn*Math.PI*2);Beam(bx,by,bz,bx+dx*5,by-1.2,bz+dz*5,1.2,r,g,b,.22);Sprite(bx,by,bz,.7,r,g,b,.6,true);Telegraph+=2;
                }
                if(st.Phase==NativeHazardPhase.Danger)
                {
                    // Sparks off the descending edge's guides.
                    double edge=top-h.size.y*NativeWardZeroHazards.Closure(st);
                    for(int e=-1;e<=1;e+=2)for(int i=0;i<6;i++){double t=Frac(time*6+H(k,i,e+3)),x=alongX?cx+e*w/2:cx,z=alongX?cz:cz+e*w/2;Line(x,edge-t*.6,z,x+(H(k,i,9)-.5)*.6,edge-t*.6-.2,z+(H(k,i,11)-.5)*.6,.03,1,.8,.4,.8*(1-t),true,0);Telegraph++;}
                }
                if(st.Phase==NativeHazardPhase.Active)
                {
                    // A red line under the shut door on both sides of the threshold.
                    if(alongX){Flat(cx,.03,cz+.45,1,0,w/2,.08,1,.12,.1,.5,0,true);Flat(cx,.03,cz-.45,1,0,w/2,.08,1,.12,.1,.5,0,true);}else{Flat(cx+.45,.03,cz,0,1,w/2,.08,1,.12,.1,.5,0,true);Flat(cx-.45,.03,cz,0,1,w/2,.08,1,.12,.1,.5,0,true);}Telegraph+=2;
                }
            }
        }
        private void SongVentFx(NativeWardFxScene s,float[] open,bool[] active,float[] warn,double time)
        {
            for(int i=0;i<s.VentEntry.Length;i++)
            {
                int side=s.VentSong[i];double x=s.VentX[i],y=s.VentY[i],z=s.VentZ[i];float o=open!=null&&s.VentEntry[i]<open.Length?open[s.VentEntry[i]]:0;
                bool erupt=side>=0&&active!=null&&side<active.Length&&active[side];float w=side>=0&&warn!=null&&side<warn.Length?warn[side]:0;
                if(w>0||o>.02f)
                {
                    // The rim fills amber, black wisps rise as it warns or opens.
                    double k=Math.Max(w,o);for(int e=0;e<8;e++){double a=e*Math.PI/4+time*.4;Sprite(x+Math.Sin(a)*1.05,y+.06,z+Math.Cos(a)*1.05,.5,1,.55,.12,.4*k,true);Telegraph++;}
                    for(int j=0;j<6;j++){double t=Frac(time*.8+H(i,j,5));Sprite(x+(H(i,j,6)-.5)*1.4,y+.2+t*1.8,z+(H(i,j,7)-.5)*1.4,.6+t,.05,.03,.08,.35*k*(1-t),false);Telegraph++;}
                }
                if(erupt)
                {
                    // The song's eruption: a column of steam and ink out of the vent, its base glowing.
                    Flat(x,y+.04,z,1,0,1.2,1.2,.8,.3,1,.4,1,true);Telegraph++;
                    for(int j=0;j<12;j++){double t=Frac(time*2.4+H(i,j,8));Sprite(x+(H(i,j,9)-.5)*.6,y+.3+t*5.5,z+(H(i,j,10)-.5)*.6,.6+t*1.6,j%3==0?.35:.8,j%3==0?.1:.82,j%3==0?.6:.86,.4*(1-t),false);Telegraph++;}
                }
            }
        }
        private void BeaconFx(NativeWardFxScene s,in NativeWardMood m,double time)
        {
            if(m.Strobe<.05f)return;
            foreach(var g in s.Glows)
            {
                if(g.Kind!=NativeWardZeroKit.GlowStrobe||g.Hazard>=0)continue;double turn=time*1.1+g.Param;double dx=Math.Sin(turn*Math.PI*2),dz=Math.Cos(turn*Math.PI*2);
                Beam(g.X,g.Y,g.Z,g.X+dx*9,g.Y-2.5,g.Z+dz*9,2.2,1,.12,.1,.16*m.Strobe);Sprite(g.X,g.Y,g.Z,1.2,1,.15,.12,.5*m.Strobe,true);Telegraph+=2;
            }
        }

        // ---------------- entrances and routes ----------------
        private void EntranceFx(NativeWardFxScene s,float[] open,double time)
        {
            for(int i=0;i<s.DoorEntry.Length;i++)
            {
                float o=open!=null&&s.DoorEntry[i]<open.Length?open[s.DoorEntry[i]]:0;if(o<.02f)continue;double x=s.DoorX[i],y=s.DoorY[i],z=s.DoorZ[i],nx=s.DoorNx[i],nz=s.DoorNz[i];
                // A red glow out of the dark doorway and dust kicked out along the floor.
                Flat(x+nx*.9,y+.03,z+nz*.9,nx,nz,1.2,.9,1,.15,.12,.18*o,1,true);Entrances++;
                for(int j=0;j<5;j++){double t=Frac(time*.9+H(i,j,3));Sprite(x+nx*(.3+t*1.6)+(H(i,j,4)-.5)*nz*1.2,y+.15+t*.4,z+nz*(.3+t*1.6)-(H(i,j,4)-.5)*nx*1.2,.4+t*.8,.55,.5,.46,.16*o*(1-t),false);Entrances++;}
            }
            for(int i=0;i<s.DropEntry.Length;i++)
            {
                float o=open!=null&&s.DropEntry[i]<open.Length?open[s.DropEntry[i]]:0;if(o<.02f)continue;double x=s.DropX[i],y=s.DropY[i],z=s.DropZ[i],hgt=s.DropH[i],size=s.DropSize[i];
                // The hatch opens: a red warning shaft down to the landing and grit falling through it.
                Cone(x,y+hgt-.05,z,hgt-.1,size*.45,size*.6,1,.15,.12,.08*o);Entrances++;
                for(int j=0;j<10;j++){double t=Frac(time*.7+H(i,j,12));Line(x+(H(i,j,13)-.5)*size*.8,y+hgt-t*hgt,z+(H(i,j,14)-.5)*size*.8,x+(H(i,j,13)-.5)*size*.8,y+hgt-t*hgt+.15,z+(H(i,j,14)-.5)*size*.8,.03,.6,.58,.55,.6*o,false,0);Entrances++;}
            }
        }
        private void GuideFx(NativeWardFxScene s,in NativeWardMood m,double time)
        {
            foreach(var r in s.Routes)
            {
                // The foot: an up-light column and pool (cyan-white); the top: a warm one at the arrival.
                double pulse=.75+.25*Math.Sin(time*2.4+r.FootX);
                Flat(r.FootX-r.Ux*1.4,r.FootY+.035,r.FootZ-r.Uz*1.4,1,0,1.6,1.6,.56,.91,1,.22*pulse,1,true);Beam(r.FootX-r.Ux*1.4,r.FootY+.1,r.FootZ-r.Uz*1.4,r.FootX-r.Ux*1.4,r.FootY+2.6,r.FootZ-r.Uz*1.4,.9,.56,.91,1,.16*pulse);
                Flat(r.TopX+r.Ux*1.4,r.TopY+.035,r.TopZ+r.Uz*1.4,1,0,1.4,1.4,1,.76,.48,.2*pulse,1,true);Beam(r.TopX+r.Ux*1.4,r.TopY+.1,r.TopZ+r.Uz*1.4,r.TopX+r.Ux*1.4,r.TopY+2.4,r.TopZ+r.Uz*1.4,.8,1,.76,.48,.14*pulse);
                Guide+=4;
            }
        }
    }
}
