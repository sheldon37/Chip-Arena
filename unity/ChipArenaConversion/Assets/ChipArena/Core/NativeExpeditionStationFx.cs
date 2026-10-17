using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// What the station's atmosphere needs, built once (<see cref="Build"/>): the kit's shafts, waters, light sources and hazard
    /// places, the lights each water reflects (the brightest within reach), the drips (where the roof is open) and the fog
    /// colour. No allocation after Build.
    /// </summary>
    public sealed class NativeStationFxScene
    {
        public NativeStationShaft[] Shafts=Array.Empty<NativeStationShaft>();public NativeStationWater[] Waters=Array.Empty<NativeStationWater>();
        public NativeStationGlow[] Glows=Array.Empty<NativeStationGlow>();
        /// <summary>Per water: up to <see cref="Reflect"/> glow indices it mirrors (-1 none).</summary>
        public int[] Mirrors=Array.Empty<int>();public const int Reflect=5;
        /// <summary>Drip points (x, y top, z, y floor) under the vault's gaps and the skylights.</summary>
        public float[] Drips=Array.Empty<float>();
        public readonly float[] RailX0=new float[NativeExpeditionStation.RailSections],RailX1=new float[NativeExpeditionStation.RailSections],RailZ=new float[NativeExpeditionStation.RailSections];
        public readonly float[] Cabinet=new float[6],Steam=new float[3];
        public float FogR=.04f,FogG=.08f,FogB=.09f;
        public static NativeStationFxScene Build(NativeExpeditionStationKit kit,MapLook look)
        {
            var s=new NativeStationFxScene();if(kit==null)return s;s.Shafts=kit.Shafts;s.Waters=kit.Waters;s.Glows=kit.Glows;
            Array.Copy(kit.RailX0,s.RailX0,s.RailX0.Length);Array.Copy(kit.RailX1,s.RailX1,s.RailX1.Length);Array.Copy(kit.RailZ,s.RailZ,s.RailZ.Length);Array.Copy(kit.Cabinet,s.Cabinet,6);Array.Copy(kit.SteamMain,s.Steam,3);
            s.Mirrors=new int[s.Waters.Length*Reflect];
            for(int w=0;w<s.Waters.Length;w++)
            {
                var wa=s.Waters[w];var best=new List<KeyValuePair<float,int>>();
                for(int g=0;g<s.Glows.Length;g++)
                {
                    var gl=s.Glows[g];if(gl.Y<wa.Y+.3f||gl.Size<=0)continue;double cx=Math.Max(wa.X0,Math.Min(wa.X1,gl.X)),cz=Math.Max(wa.Z0,Math.Min(wa.Z1,gl.Z)),d=Math.Sqrt((cx-gl.X)*(cx-gl.X)+(cz-gl.Z)*(cz-gl.Z));
                    if(d>16)continue;float lum=gl.R*.3f+gl.G*.55f+gl.B*.15f;best.Add(new KeyValuePair<float,int>(-(float)(lum/(1+d*.2)),g));
                }
                best.Sort((a,b)=>a.Key!=b.Key?a.Key.CompareTo(b.Key):a.Value.CompareTo(b.Value));
                for(int k=0;k<Reflect;k++)s.Mirrors[w*Reflect+k]=k<best.Count?best[k].Value:-1;
            }
            var drips=new List<float>();foreach(var sh in s.Shafts){if(sh.Kind!=NativeExpeditionStationKit.ShaftMoon)continue;drips.Add(sh.X0);drips.Add(sh.Y0-.3f);drips.Add(sh.Z0);drips.Add(sh.Y1-.05f);}
            s.Drips=drips.ToArray();
            if(look?.fog!=null){s.FogR=(float)NativeSceneryBuilder.Channel(look.fog.color,16);s.FogG=(float)NativeSceneryBuilder.Channel(look.fog.color,8);s.FogB=(float)NativeSceneryBuilder.Channel(look.fog.color,0);}
            return s;
        }
    }

    /// <summary>
    /// Builds the station's camera-dependent quads for one frame into a <see cref="NativeFxQuads"/> (Runtime draws them as one
    /// solid and one effects batch through NativeSirenFx: two draws). Atmosphere, then the hazards' telegraphs over it (rule
    /// zero, readability first: telegraphs are never thinned by the quality or the low tier, and the haze stays faint and
    /// above the floor):
    ///  - moonlight: soft shafts through the vault's gaps and the skylights (chains of overlapping radial sprites, soft across
    ///    and along their length: ranked gap 2), dust motes drifting in them, the sodium pendants' cones and their warm pools;
    ///  - halos round the lights (the board, CENTRAL, globes, the signals in their aspect, the beacons as they sweep);
    ///  - water: each light's reflection streaked across the flood and the puddles toward the eye (ranked gap 3's sheen),
    ///    ripples (drips landing, the sumps breathing), low mist on the flood, drips falling from the open roof;
    ///  - the cafe's smouldering fire (flames, embers, smoke), the steam main's wisps;
    ///  - the express (ranked gap 4): through the warning the headlight grows in the west mouth with a flare and a beam along the
    ///    pit, and glints race along the rails toward the platforms (the rails singing); through the pass sparks spray off
    ///    the wheels and a dust wall boils up behind the tail and settles;
    ///  - the third rail: crackles at its insulators while live; the flood glowing blue and arcing while it conducts;
    ///  - the spark cabinets: a fizz of sparks at the door gaps, then a 2 m arc and a burst; the steam main's hiss, then its
    ///    scalding plume;
    ///  - falling glass: a shadow growing on the floor through the telegraph, then the shards bursting out and glinting.
    /// Counts scale with the effects quality and the low tier (shafts halved and shortened, no motes, mist or reflections)
    /// except the telegraphs. Pure: the same clock, camera, state and scene give the same quads; premultiplied pieces are sorted
    /// far to near. Allocation free.
    /// </summary>
    public sealed class NativeExpeditionStationFx
    {
        public const int Capacity=2600,SortMax=420;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        /// <summary>This frame's counts by family (the budget lines and the checks).</summary>
        public int Shafts,Motes,Halos,Pools,Reflections,Ripples,Mist,Drips,Fire,Smoke,Telegraph,Sparks,Dust,Glass,Arcs;
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz,fx,fy,fz;private float quality;private bool low;
        // Premultiplied pieces wait here, sorted far to near before they go out.
        private readonly double[] pc=new double[SortMax*12],pk=new double[SortMax];private readonly float[] pcol=new float[SortMax*4];private readonly int[] pidx=new int[SortMax];private readonly byte[] pmode=new byte[SortMax];private int pn;

        private static readonly double[] Pair75={-.75,.75},Pair72={-.72,.72};
        private static double Frac(double x)=>x-Math.Floor(x);
        private static double H(int a,int b){unchecked{uint x=(uint)a*0x9E3779B9U^(uint)b*0x85EBCA6BU;x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x/4294967296.0;}}
        private static float Lin(int hex,int shift)=>(float)NativeSceneryBuilder.Channel(hex,shift);

        public void Build(NativeStationFxScene scene,NativeStationState s,double time,double eyeX,double eyeY,double eyeZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,float effects,bool lowTier)
        {
            var q=Quads;q.Clear();pn=0;Shafts=Motes=Halos=Pools=Reflections=Ripples=Mist=Drips=Fire=Smoke=Telegraph=Sparks=Dust=Glass=Arcs=0;if(scene==null)return;
            ex=eyeX;ey=eyeY;ez=eyeZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;fx=ry*uz-rz*uy;fy=rz*ux-rx*uz;fz=rx*uy-ry*ux;quality=effects<0?0:effects>1?1:effects;low=lowTier;
            // Telegraphs first (they are never thinned), then the atmosphere into what is left.
            Express(scene,s,time);Rails(scene,s,time);Flood(scene,s,time);CabinetsFx(scene,s,time);SteamFx(scene,s,time);GlassFx(s,time);
            MoonShafts(scene,time);LampCones(scene,time);HalosFx(scene,s,time);WaterFx(scene,s,time);DripsFx(scene,time);FireFx(scene,time);
            Flush();
        }
        private int Budget(int full)=>Math.Max(0,(int)Math.Round(full*(low?.5f:1f)*(.35f+.65f*quality)));
        private bool Ahead(double x,double y,double z,double margin=1){double dx=x-ex,dy=y-ey,dz=z-ez;return dx*fx+dy*fy+dz*fz>-margin;}
        private double Dist(double x,double y,double z){double dx=x-ex,dy=y-ey,dz=z-ez;return Math.Sqrt(dx*dx+dy*dy+dz*dz);}

        // ---------------- primitives ----------------
        private void Add(double ax,double ay,double az,double bx,double by,double bz,double cx,double cy,double cz,double dx,double dy,double dz,float r,float g,float b,float a,int mode,bool additive)
        {
            if(additive){Quads.Add(ax,ay,az,bx,by,bz,cx,cy,cz,dx,dy,dz,r,g,b,a,mode,true,1);return;}
            if(pn>=SortMax||!(a>0))return;int o=pn*12;pc[o]=ax;pc[o+1]=ay;pc[o+2]=az;pc[o+3]=bx;pc[o+4]=by;pc[o+5]=bz;pc[o+6]=cx;pc[o+7]=cy;pc[o+8]=cz;pc[o+9]=dx;pc[o+10]=dy;pc[o+11]=dz;
            pcol[pn*4]=r;pcol[pn*4+1]=g;pcol[pn*4+2]=b;pcol[pn*4+3]=a;pmode[pn]=(byte)mode;double mx=(ax+cx)/2-ex,my=(ay+cy)/2-ey,mz=(az+cz)/2-ez;pk[pn]=mx*mx+my*my+mz*mz;pidx[pn]=pn;pn++;
        }
        private void Flush()
        {
            for(int i=1;i<pn;i++){int v=pidx[i];double key=pk[v];int j=i-1;while(j>=0&&pk[pidx[j]]<key){pidx[j+1]=pidx[j];j--;}pidx[j+1]=v;}
            for(int i=0;i<pn;i++){int v=pidx[i],o=v*12;Quads.Add(pc[o],pc[o+1],pc[o+2],pc[o+3],pc[o+4],pc[o+5],pc[o+6],pc[o+7],pc[o+8],pc[o+9],pc[o+10],pc[o+11],pcol[v*4],pcol[v*4+1],pcol[v*4+2],pcol[v*4+3],pmode[v],false,1);}
            pn=0;
        }
        /// <summary>A camera-facing radial sprite (radius `size`).</summary>
        private void Sprite(double x,double y,double z,double size,float r,float g,float b,float a,bool additive=true)
        {
            double sx=rx*size,sy=ry*size,sz=rz*size,tx=ux*size,ty=uy*size,tz=uz*size;
            Add(x-sx-tx,y-sy-ty,z-sz-tz,x+sx-tx,y+sy-ty,z+sz-tz,x+sx+tx,y+sy+ty,z+sz+tz,x-sx+tx,y-sy+ty,z-sz+tz,r,g,b,a,1,additive);
        }
        /// <summary>A radial sprite lying flat at height y (radii along x and z, turned `yaw` radians).</summary>
        private void Flat(double x,double y,double z,double hx,double hz,double yaw,float r,float g,float b,float a,bool additive=true,int mode=1)
        {
            double c=Math.Cos(yaw),s=Math.Sin(yaw),ax=c*hx,az=-s*hx,bx=s*hz,bz=c*hz;
            Add(x-ax-bx,y,z-az-bz,x+ax-bx,y,z+az-bz,x+ax+bx,y,z+az+bz,x-ax+bx,y,z-az+bz,r,g,b,a,mode,additive);
        }
        /// <summary>A soft sprite stretched along a segment (a-b), facing the camera across it: half width `w`.</summary>
        private void Streak(double ax,double ay,double az,double bx,double by,double bz,double w,float r,float g,float b,float a,bool additive=true,int mode=1)
        {
            double dx=bx-ax,dy=by-ay,dz=bz-az,mx=(ax+bx)/2-ex,my=(ay+by)/2-ey,mz=(az+bz)/2-ez;double sx=dy*mz-dz*my,sy=dz*mx-dx*mz,sz=dx*my-dy*mx,l=Math.Sqrt(sx*sx+sy*sy+sz*sz);
            if(l<1e-9){sx=rx;sy=ry;sz=rz;l=1;}sx*=w/l;sy*=w/l;sz*=w/l;
            Add(ax-sx,ay-sy,az-sz,ax+sx,ay+sy,az+sz,bx+sx,by+sy,bz+sz,bx-sx,by-sy,bz-sz,r,g,b,a,mode,additive);
        }
        /// <summary>A soft beam from a to b, widening from w0 to w1: overlapping sprites along it (soft across and along).</summary>
        private int Beam(double ax,double ay,double az,double bx,double by,double bz,double w0,double w1,float r,float g,float b,float a,int max)
        {
            double dx=bx-ax,dy=by-ay,dz=bz-az,len=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(len<.1)return 0;int n=Math.Min(max,Math.Max(3,(int)Math.Ceiling(len/((w0+w1)*.55))));int made=0;
            for(int k=0;k<n;k++)
            {
                double t=(k+.5)/n,hl=len/n*1.35,cx=ax+dx*t,cy=ay+dy*t,cz=az+dz*t,w=w0+(w1-w0)*t,fade=Math.Pow(Math.Sin(Math.PI*Math.Min(1,Math.Max(0,t*1.04))),.6);
                double ux2=dx/len*hl,uy2=dy/len*hl,uz2=dz/len*hl;Streak(cx-ux2,cy-uy2,cz-uz2,cx+ux2,cy+uy2,cz+uz2,w,r,g,b,(float)(a*fade),true);made++;
            }
            return made;
        }

        // ---------------- atmosphere ----------------
        private void MoonShafts(NativeStationFxScene sc,double t)
        {
            float mr=Lin(NativeExpeditionStationKit.ShaftHex,16),mg=Lin(NativeExpeditionStationKit.ShaftHex,8),mb=Lin(NativeExpeditionStationKit.ShaftHex,0);
            int motes=low?0:Budget(8);
            for(int i=0;i<sc.Shafts.Length;i++)
            {
                var s=sc.Shafts[i];if(s.Kind!=NativeExpeditionStationKit.ShaftMoon)continue;if(low&&(i&1)==1)continue;
                double mx=(s.X0+s.X1)/2,my=(s.Y0+s.Y1)/2,mz=(s.Z0+s.Z1)/2;if(Dist(mx,my,mz)>95||!Ahead(mx,my,mz,12))continue;
                float drift=(float)(.82+.18*Math.Sin(t*.21+i*1.7));float a=s.Alpha*drift*.1f;
                double y1=low?s.Y0+(s.Y1-s.Y0)*.6:s.Y1,x1=low?s.X0+(s.X1-s.X0)*.6:s.X1,z1=low?s.Z0+(s.Z1-s.Z0)*.6:s.Z1;
                Shafts+=Beam(s.X0,s.Y0,s.Z0,x1,y1,z1,s.W0*.4,s.W1*.4,mr,mg,mb,a*1.9f,14);
                Shafts+=Beam(s.X0,s.Y0,s.Z0,x1,y1,z1,s.W0,s.W1,mr,mg,mb,a*.8f,12);
                // Where it lands: a soft pool of moonlight.
                if(!low){Flat(s.X1,s.Y1+.02,s.Z1,s.W1*1.1,s.W1*.8,0,mr,mg,mb,a*2.4f);Pools++;}
                for(int k=0;k<motes;k++)
                {
                    double h=H(i*31+k,7),ph=Frac(h+t*.03*(1+H(i,k)));double px=s.X0+(s.X1-s.X0)*ph+Math.Sin(t*.4+k)*.3,py=s.Y0+(s.Y1-s.Y0)*ph,pz=s.Z0+(s.Z1-s.Z0)*ph+Math.Cos(t*.33+k)*.3;
                    if(Dist(px,py,pz)>30)continue;Sprite(px,py,pz,.035,mr*2,mg*2,mb*2,.7f*(float)Math.Sin(Math.PI*ph));Motes++;
                }
            }
        }
        private void LampCones(NativeStationFxScene sc,double t)
        {
            float lr=Lin(NativeExpeditionStationKit.SodiumHex,16),lg=Lin(NativeExpeditionStationKit.SodiumHex,8),lb=Lin(NativeExpeditionStationKit.SodiumHex,0);
            for(int i=0;i<sc.Shafts.Length;i++)
            {
                var s=sc.Shafts[i];if(s.Kind!=NativeExpeditionStationKit.ShaftLamp)continue;double d=Dist(s.X0,2.5,s.Z0);if(d>60||!Ahead(s.X0,2.5,s.Z0,6))continue;
                if(!low)Shafts+=Beam(s.X0,s.Y0,s.Z0,s.X1,s.Y1+.6,s.Z1,s.W0,s.W1,lr,lg,lb,s.Alpha*.13f,4);
                Flat(s.X1,s.Y1+.01,s.Z1,2.6,2.6,0,lr,lg,lb,.16f);Pools++;
            }
        }
        // The light's colour this frame (the kit's glow with the state: signals by aspect, beacons sweeping, the board stuttering).
        private bool GlowNow(in NativeStationGlow g,NativeStationState s,double t,out float r,out float gg,out float b)
        {
            r=g.R;gg=g.G;b=g.B;float k=1;
            switch(g.Kind)
            {
                case NativeExpeditionStationKit.GlowLampFlicker:case NativeExpeditionStationKit.GlowWindow:case NativeExpeditionStationKit.GlowGenerator:k=NativeExpeditionStationKit.FlickerLevel(g.Param,t);break;
                case NativeExpeditionStationKit.GlowTubeFlicker:k=NativeExpeditionStationKit.Stutter(t+g.Param*5.3,9+g.Param*7,.35,.85);break;
                case NativeExpeditionStationKit.GlowBoard:k=NativeExpeditionStationKit.Stutter(t,7,.18,.5);break;
                case NativeExpeditionStationKit.GlowFire:k=NativeExpeditionStationKit.FireLevel(g.Param,t);break;
                case NativeExpeditionStationKit.GlowStrip:k=NativeExpeditionStationKit.Stutter(t+g.Param*3.1,5,.15,.6);break;
                case NativeExpeditionStationKit.GlowSignal:
                {
                    byte a=s?.Signal??NativeStationState.SignalRed;if(a==NativeStationState.SignalDark)return false;
                    if(g.Tag/2==0&&a==NativeStationState.SignalGreen){float m=Math.Max(r,Math.Max(gg,b));r=m*.15f;gg=m;b=m*.3f;}k=2;break;
                }
                case NativeExpeditionStationKit.GlowBeacon:{if((s?.Danger??0)<=0)return false;k=NativeExpeditionStationKit.BeaconLevel(g.Param,t)*1.2f;break;}
                case NativeExpeditionStationKit.GlowCabinet:{var ph=s!=null&&g.Tag>=0&&g.Tag<2?s.CabinetPhase[g.Tag]:NativeHazardPhase.Idle;if(ph==NativeHazardPhase.Idle)return false;k=ph==NativeHazardPhase.Active?3:(Frac(t*13+g.Param)<.5?1:.3f);break;}
                case NativeExpeditionStationKit.GlowWater:{if(g.Tag>=0){k=.6f;break;}float f=s?.FloodGlow??0;if(f<=0)return false;r=.05f*f;gg=.35f*f;b=.95f*f;break;}
            }
            r*=k;gg*=k;b*=k;return r+gg+b>1e-4;
        }
        private void HalosFx(NativeStationFxScene sc,NativeStationState s,double t)
        {
            foreach(var g in sc.Glows)
            {
                if(g.Size<=0)continue;double d=Dist(g.X,g.Y,g.Z);if(d>75||!Ahead(g.X,g.Y,g.Z,1))continue;if(!GlowNow(g,s,t,out float r,out float gg,out float b))continue;
                bool tele=g.Kind==NativeExpeditionStationKit.GlowBeacon||g.Kind==NativeExpeditionStationKit.GlowSignal||g.Kind==NativeExpeditionStationKit.GlowCabinet;
                Sprite(g.X,g.Y,g.Z,g.Size*(1+d*.012),r*.55f,gg*.55f,b*.55f,tele?.8f:.45f);if(tele)Telegraph++;else Halos++;
            }
        }
        private void WaterFx(NativeStationFxScene sc,NativeStationState s,double t)
        {
            if(low)return;
            for(int w=0;w<sc.Waters.Length;w++)
            {
                var wa=sc.Waters[w];double cx=(wa.X0+wa.X1)/2,cz=(wa.Z0+wa.Z1)/2;if(Dist(cx,wa.Y,cz)>70)continue;if(ey<wa.Y+.2)continue;
                // Reflections: each mirrored light streaks across the surface toward the eye from where its image meets it.
                for(int k=0;k<NativeStationFxScene.Reflect;k++)
                {
                    int gi=sc.Mirrors[w*NativeStationFxScene.Reflect+k];if(gi<0)break;var g=sc.Glows[gi];if(!GlowNow(g,s,t,out float r,out float gg,out float b))continue;
                    double iy=2*wa.Y-g.Y,f=(ey-wa.Y)/(ey-iy),hx=ex+(g.X-ex)*f,hz=ez+(g.Z-ez)*f;
                    if(hx<wa.X0-.3||hx>wa.X1+.3||hz<wa.Z0-.3||hz>wa.Z1+.3)continue;
                    double vx=ex-hx,vz=ez-hz,vl=Math.Sqrt(vx*vx+vz*vz);if(vl<2.5)continue;vx/=vl;vz/=vl;double len=Math.Min(Math.Min(4.5,vl*.4),.6+(g.Y-wa.Y)*.45);
                    double ax=hx-vx*len*.25,az=hz-vz*len*.25,bx2=hx+vx*len*.75,bz2=hz+vz*len*.75;
                    double wob=wa.Kind==NativeExpeditionStationKit.WaterFlood?.03*Math.Sin(t*2.3+gi):0;float near=(float)Math.Min(1,(vl-2.5)/4);
                    // Broken into dashes that shimmer on the flood (still on a puddle).
                    double hw=Math.Min(.3,.06+g.Size*.07)+wob;int dashes=wa.Kind==NativeExpeditionStationKit.WaterFlood?4:2;
                    for(int d=0;d<dashes;d++){double u0=(double)d/dashes,u1=u0+.8/dashes,sh=wa.Kind==NativeExpeditionStationKit.WaterFlood?.08*Math.Sin(t*3.1+d*1.7+gi):0;
                        StreakFlat(ax+(bx2-ax)*u0-vz*sh,wa.Y+.012,az+(bz2-az)*u0+vx*sh,ax+(bx2-ax)*u1-vz*sh,wa.Y+.012,az+(bz2-az)*u1+vx*sh,hw*(1-.3*u0),r*.5f,gg*.5f,b*.5f,.85f*near);Reflections++;}
                }
                // A puddle darkens the floor softly (wet), its edge fading.
                if(wa.Kind==NativeExpeditionStationKit.WaterPuddle){Flat(cx,wa.Y+.004,cz,(wa.X1-wa.X0)*.5,(wa.Z1-wa.Z0)*.5,cx*.7+cz,.01f,.012f,.014f,.55f,false);Pools++;continue;}
                if(wa.Kind!=NativeExpeditionStationKit.WaterFlood)continue;
                // Ripples: rings opening across the flood, more near the sumps; low mist drifting over it.
                int rings=Budget(5);
                for(int k=0;k<rings;k++)
                {
                    double cyc=1.8+H(w,k)*1.6,ph=Frac(t/cyc+H(k,w)),seed=Math.Floor(t/cyc+H(k,w));double px=wa.X0+(wa.X1-wa.X0)*H((int)seed,k*7+w),pz=wa.Z0+(wa.Z1-wa.Z0)*H((int)seed,k*11+w);
                    Ring(px,wa.Y+.015,pz,.15+ph*1.4,.035,.6f,.75f,.8f,(float)(.28*(1-ph)));Ripples++;
                }
                // The sumps breathe a faint aqua under the surface (where the Undertow waits).
                foreach(var g in sc.Glows){if(g.Kind!=NativeExpeditionStationKit.GlowWater||g.Tag<0)continue;if(g.X<wa.X0-1||g.X>wa.X1+1||g.Z<wa.Z0-1||g.Z>wa.Z1+1)continue;float br=(float)(.6+.4*Math.Sin(t*1.3+g.Tag));Flat(g.X,wa.Y+.01,g.Z,2.2,2.2,0,.05f*br,.42f*br,.32f*br,.55f);Ripples++;}
                int mist=Budget(6);
                for(int k=0;k<mist;k++){double px=wa.X0+(wa.X1-wa.X0)*Frac(H(w,k+40)+t*.006*(1+k%3)),pz=wa.Z0+(wa.Z1-wa.Z0)*H(w,k+60);if(Dist(px,wa.Y,pz)>40)continue;Flat(px,wa.Y+.18+.1*Math.Sin(t*.3+k),pz,3.4,2.2,t*.02+k,sc.FogR*1.6f+.03f,sc.FogG*1.6f+.04f,sc.FogB*1.6f+.04f,.16f,false);Mist++;}
            }
        }
        /// <summary>A soft streak lying flat on a surface (from a to b, half width w).</summary>
        private void StreakFlat(double ax,double ay,double az,double bx,double by,double bz,double w,float r,float g,float b,float a)
        {
            double dx=bx-ax,dz=bz-az,l=Math.Sqrt(dx*dx+dz*dz);if(l<1e-6)return;double sx=-dz/l*w,sz=dx/l*w;
            Add(ax-sx,ay,az-sz,ax+sx,ay,az+sz,bx+sx,by,bz+sz,bx-sx,by,bz-sz,r,g,b,a,1,true);
        }
        /// <summary>A thin ring lying flat (radius, line width) as ten soft dashes.</summary>
        private void Ring(double x,double y,double z,double radius,double width,float r,float g,float b,float a)
        {
            const int n=10;for(int k=0;k<n;k++){double a0=k*Math.PI*2/n,a1=(k+1)*Math.PI*2/n;StreakFlat(x+Math.Cos(a0)*radius,y,z+Math.Sin(a0)*radius,x+Math.Cos(a1)*radius,y,z+Math.Sin(a1)*radius,width,r,g,b,a);}
        }
        private void DripsFx(NativeStationFxScene sc,double t)
        {
            int n=sc.Drips.Length/4,max=Budget(16);
            for(int i=0;i<n&&Drips<max;i++)
            {
                double x=sc.Drips[i*4],top=sc.Drips[i*4+1],z=sc.Drips[i*4+2],floor=sc.Drips[i*4+3];if(Dist(x,(top+floor)/2,z)>45)continue;
                for(int k=0;k<2;k++)
                {
                    double cyc=2.6+H(i,k)*2.2,ph=Frac(t/cyc+H(k,i)),fall=Math.Sqrt(2*(top-floor)/9.81),tf=ph*cyc;double ox=(H(i,k+3)-.5)*1.2,oz=(H(i,k+5)-.5)*1.2;
                    if(tf<fall){double y=top-4.905*tf*tf;Streak(x+ox,y+.25,z+oz,x+ox,y-.15,z+oz,.012,.5f,.6f,.7f,.5f);Drips++;}
                    else if(tf<fall+.5){double u=(tf-fall)/.5;Ring(x+ox,floor+.02,z+oz,.05+u*.45,.02,.5f,.6f,.7f,(float)(.4*(1-u)));Ripples++;}
                }
            }
        }
        private void FireFx(NativeStationFxScene sc,double t)
        {
            foreach(var g in sc.Glows)
            {
                if(g.Kind!=NativeExpeditionStationKit.GlowFire||Dist(g.X,g.Y,g.Z)>50)continue;double bx=g.X,by=g.Floor+.15,bz=g.Z+.35;
                for(int k=0;k<6;k++){double ph=Frac(t*1.4+H(k,1)),x=bx+(H(k,2)-.5)*1.4,y=by+ph*.7;Sprite(x,y,bz,.22*(1-ph*.5),2.2f,.7f,.15f,(float)(.55*(1-ph)));Fire++;}
                int embers=Budget(10);for(int k=0;k<embers;k++){double ph=Frac(t*.35+H(k,9)),x=bx+(H(k,10)-.5)*2+Math.Sin(t+k)*.2*ph,y=by+ph*3.2;Sprite(x,y,bz+(H(k,11)-.5),.025,3f,1.2f,.3f,(float)(1-ph));Fire++;}
                int smoke=Budget(8);for(int k=0;k<smoke;k++){double ph=Frac(t*.09+H(k,20)),x=bx+(H(k,21)-.5)+ph*1.6,y=by+.6+ph*5.5;Sprite(x,y,bz-.4+ph*.8,.5+ph*1.8,.05f,.045f,.04f,(float)(.35*Math.Sin(Math.PI*ph)),false);Smoke++;}
            }
        }

        // ---------------- telegraphs (never thinned) ----------------
        private void Express(NativeStationFxScene sc,NativeStationState s,double t)
        {
            if(s==null)return;int line=s.Line-1;double z=NativeExpeditionStation.LineZ[line],bed=NativeExpeditionStation.TrackBed;
            if(s.Express==NativeStationState.ExpressWarn)
            {
                // The headlight grows in the west mouth: a halo, a horizontal flare and a beam reaching down the pit.
                double h=s.Headlight,mx=-NativeExpeditionStation.ShellX+.2;
                foreach(double dz in Pair75){Sprite(mx,bed+1.15,z+dz,.3+h*1.6,1.6f*(float)h+.2f,1.65f*(float)h+.2f,1.8f*(float)h+.2f,.9f);Telegraph++;}
                Streak(mx+.1,bed+1.15,z-6*h,mx+.1,bed+1.15,z+6*h,.08+h*.25,(float)h*1.4f,(float)h*1.45f,(float)h*1.6f,.7f);Telegraph++;
                Telegraph+=Beam(mx+.5,bed+1.15,z,mx+8+h*30,bed+.9,z,.4,2.4,(float)h*.55f,(float)h*.58f,(float)h*.65f,.45f*(float)h,10);
                // The rails sing: glints race along both running rails from the mouth toward the station.
                for(int k=0;k<10;k++){double px=-44+Frac(t*.9+k*.1)*88*(.4+.6*s.Warn);foreach(double dz in Pair72){Streak(px-1.6,bed+.18,z+dz,px+1.6,bed+.18,z+dz,.06,1.2f,1.3f,1.5f,.6f*(float)(.4+.6*s.Warn));Telegraph++;}}
            }
            if(s.Express==NativeStationState.ExpressPass||s.Express==NativeStationState.ExpressAfter)
            {
                double nose=s.Nose,tail=nose-NativeExpeditionStation.TrainLength;double a0=Math.Max(-NativeExpeditionStation.ShellX,tail),a1=Math.Min(NativeExpeditionStation.ShellX,nose);
                if(s.Express==NativeStationState.ExpressPass&&a1>a0)
                {
                    // The headlight at the nose; sparks spraying back off the wheels along the train.
                    if(nose<NativeExpeditionStation.ShellX){foreach(double dz in Pair75){Sprite(nose+.3,bed+1.15,z+dz,1.4,2f,2f,2.2f,.9f);Telegraph++;}}
                    for(int k=0;k<36;k++){double u=H((int)(t*30),k),px=a0+(a1-a0)*u,dz=(H(k,(int)(t*30)+5)-.5)*1.5>0?.72:-.72,len=.4+H(k,3)*.8;Streak(px,bed+.2,z+dz,px-len,bed+.2+H(k,4)*.5,z+dz+(H(k,6)-.5)*.6,.035,3f,1.5f,.45f,.9f);Sparks++;}
                }
                // The dust wall: boiling behind the tail along the pit, settling after it.
                int puffs=24;
                for(int k=0;k<puffs;k++)
                {
                    double u=H(k,41),px=-NativeExpeditionStation.ShellX+u*2*NativeExpeditionStation.ShellX;if(px>tail+6)continue;double age=(tail+6-px)/NativeExpeditionStation.Speed+(s.Express==NativeStationState.ExpressAfter?s.ExpressAge:0);
                    if(age>NativeExpeditionStation.WakeSeconds+1)continue;double fade=Math.Max(0,1-age/(NativeExpeditionStation.WakeSeconds+1));
                    Sprite(px+age*3,bed+.8+age*.9,z+(H(k,42)-.5)*3.5,1.2+age*1.6,.09f,.08f,.065f,(float)(.42*fade),false);Dust++;
                }
            }
        }
        private void Rails(NativeStationFxScene sc,NativeStationState s,double t)
        {
            int live=s?.Rails??NativeExpeditionStation.AllRails;
            for(int r=0;r<NativeExpeditionStation.RailSections;r++)
            {
                if((live>>r&1)==0||float.IsNaN(sc.RailX0[r]))continue;double x0=sc.RailX0[r],x1=sc.RailX1[r],z=sc.RailZ[r],y=NativeExpeditionStation.TrackBed+.27;
                // A crackle now and then at an insulator: a few sparks and a blue flash.
                double cyc=1.7+r*.31,ph=Frac(t/cyc+r*.17);if(ph>.12)continue;double seed=Math.Floor(t/cyc+r*.17),px=x0+(x1-x0)*H((int)seed,r);
                if(Dist(px,y,z)>45)continue;Sprite(px,y,z,.35,.5f,.9f,1.6f,(float)(.8*(1-ph/.12)));Telegraph++;
                for(int k=0;k<5;k++){double a=H(k,(int)seed)*Math.PI*2,l=.15+H(k+3,(int)seed)*.35;Streak(px,y,z,px+Math.Cos(a)*l,y+.1+H(k,7)*.3,z+Math.Sin(a)*l,.012,1.2f,1.8f,3f,.9f);Sparks++;}
            }
        }
        private void Flood(NativeStationFxScene sc,NativeStationState s,double t)
        {
            if(s==null||s.FloodGlow<=0)return;
            foreach(var wa in sc.Waters)
            {
                if(wa.Kind!=NativeExpeditionStationKit.WaterFlood)continue;double cx=(wa.X0+wa.X1)/2,cz=(wa.Z0+wa.Z1)/2;
                // The water lit electric blue (soft sheets over it), and arcs crawling across the surface.
                int nx=Math.Max(1,(int)((wa.X1-wa.X0)/6)),nz=Math.Max(1,(int)((wa.Z1-wa.Z0)/6));
                for(int i=0;i<nx;i++)for(int j=0;j<nz;j++){double px=wa.X0+(i+.5)*(wa.X1-wa.X0)/nx,pz=wa.Z0+(j+.5)*(wa.Z1-wa.Z0)/nz;double surge=.45+.55*Math.Max(0,Math.Sin(t*4.1+i*1.3+j*2.1));Flat(px,wa.Y+.02,pz,(wa.X1-wa.X0)/nx*.95,(wa.Z1-wa.Z0)/nz*.95,0,.05f,.32f,.95f,(float)(.5*surge)*s.FloodGlow);Telegraph++;}
                for(int k=0;k<6;k++)
                {
                    double seed=Math.Floor(t*7+k*.3);double ax=wa.X0+(wa.X1-wa.X0)*H((int)seed,k),az=wa.Z0+(wa.Z1-wa.Z0)*H((int)seed,k+9);double px=ax,pz=az;
                    Sprite(ax,wa.Y+.08,az,.5,.6f,1.2f,2.6f,.9f);Arcs++;
                    for(int j=0;j<6;j++){double nx2=px+(H(j,(int)seed+k)-.3)*1.4,nz2=pz+(H(j+5,(int)seed+k)-.5)*1.2;Streak(px,wa.Y+.05,pz,nx2,wa.Y+.05,nz2,.055,1.4f,2.2f,3.8f,.95f);Arcs++;px=nx2;pz=nz2;}
                }
            }
        }
        private void CabinetsFx(NativeStationFxScene sc,NativeStationState s,double t)
        {
            if(s==null)return;
            for(int c=0;c<2;c++)
            {
                var ph=s.CabinetPhase[c];if(ph==NativeHazardPhase.Idle)continue;double x=sc.Cabinet[c*3],y=sc.Cabinet[c*3+1],z=sc.Cabinet[c*3+2]-.05;if(x==0&&z==0)continue;
                if(ph==NativeHazardPhase.Warn)
                {
                    // The fizz: sparks spitting at the door gaps.
                    for(int k=0;k<8;k++){double seed=Math.Floor(t*24)+k;double px=x+(H(k,(int)seed)-.5)*.1,py=.2+H(k+2,(int)seed)*1.3;Streak(px,py,z,px+(H(k+4,(int)seed)-.5)*.5,py-H(k+6,(int)seed)*.4,z-H(k+8,(int)seed)*.4,.012,2.5f,2.2f,1.2f,.9f);Sparks++;}
                    Telegraph++;Sprite(x,1.0,z-.1,.5,.6f,.9f,1.4f,.5f*(float)(.5+.5*Math.Sin(t*40)));
                }
                else
                {
                    // The arc: a jagged bolt 2 m out from the cabinet and back to the platform, a burst, a flash.
                    double seed=Math.Floor(t*20);double px=x,py=1.1,pz=z;double ang=H(3,(int)seed)*Math.PI-Math.PI/2;double tx=x+Math.Sin(ang)*2.0,tz=z-Math.Cos(ang)*2.0;
                    for(int j=0;j<7;j++){double u=(j+1)/7.0,nx=x+(tx-x)*u+(H(j,(int)seed)-.5)*.35,ny=1.1+(.05-1.1)*u*u+(H(j+3,(int)seed)-.5)*.3,nz=z+(tz-z)*u+(H(j+6,(int)seed)-.5)*.35;Streak(px,py,pz,nx,ny,nz,.05,1.6f,2.4f,4f,1f);Streak(px,py,pz,nx,ny,nz,.18,.3f,.5f,1.2f,.6f);Arcs+=2;px=nx;py=ny;pz=nz;}
                    Sprite(x,1.0,z-.3,1.6,.8f,1.2f,2f,.8f);Sprite(tx,.1,tz,.8,.8f,1.2f,2f,.7f);Telegraph+=2;
                    for(int k=0;k<10;k++){double a=H(k,(int)seed+40)*Math.PI*2,l=.3+H(k,9)*.6;Streak(tx,.1,tz,tx+Math.Cos(a)*l,.1+H(k,11)*.6,tz+Math.Sin(a)*l,.015,3f,2.4f,1.2f,1f);Sparks++;}
                }
            }
        }
        private void SteamFx(NativeStationFxScene sc,NativeStationState s,double t)
        {
            double x=sc.Steam[0],y=sc.Steam[1],z=sc.Steam[2];if(x==0&&y==0)return;if(Dist(x,y,z)>45)return;var ph=s?.SteamPhase??NativeHazardPhase.Idle;
            int n=ph==NativeHazardPhase.Active?22:ph==NativeHazardPhase.Warn?8:Budget(3);
            for(int k=0;k<n;k++)
            {
                double p=Frac(t*(ph==NativeHazardPhase.Active?.9:.5)+H(k,50));double out2=ph==NativeHazardPhase.Active?.5+p*3.2:.2+p*1.2,spread=ph==NativeHazardPhase.Active?p*2.0:p*.5;
                double px=x+(H(k,51)-.5)*spread*1.6,py=y-.1-p*(ph==NativeHazardPhase.Active?2.8:.4)+(H(k,52)-.5)*spread,pz=z+out2;
                float a=(float)((ph==NativeHazardPhase.Active?.5:ph==NativeHazardPhase.Warn?.4:.15)*Math.Sin(Math.PI*p));
                Sprite(px,py,pz,.25+p*(ph==NativeHazardPhase.Active?1.6:.6),.22f,.24f,.25f,a,false);if(ph!=NativeHazardPhase.Idle)Telegraph++;else Smoke++;
            }
        }
        private void GlassFx(NativeStationState s,double t)
        {
            // The view's glass events are in the state's source; the state only counts them, so the view is read here.
            var v=GlassView;if(v==null)return;
            for(int i=0;i<v.GlassCount&&i<v.Glass.Length;i++)
            {
                var g=v.Glass[i];double age=t-g.At;if(age<-NativeExpeditionStation.GlassTelegraph||age>NativeExpeditionStation.GlassSettle)continue;
                if(age<0)
                {
                    // The telegraph: a shadow growing on the floor where it will land, a glint high above as the pane lets go.
                    double u=1+age/NativeExpeditionStation.GlassTelegraph;Flat(g.X,.03,g.Z,.6+u*1.8,.6+u*1.8,0,0,0,0,(float)(.25+.5*u),false);Telegraph++;
                    Flat(g.X,.035,g.Z,.6+u*1.8,.6+u*1.8,0,.6f,.15f,.1f,(float)(.25*u));
                    // Its edge marked by a ring of cold glints, so the landing zone reads at a glance.
                    for(int k=0;k<12;k++){double a=k*Math.PI/6+t*.8,rr=.6+u*1.8;Sprite(g.X+Math.Cos(a)*rr,.08,g.Z+Math.Sin(a)*rr,.16,1.1f,1.2f,1.4f,(float)(.55*u));Telegraph++;}
                    double fy=NativeExpeditionStationKit.VaultY(g.Z)-.5-u*u*14;Sprite(g.X,Math.Max(.5,fy),g.Z,.25+u*.3,1.4f,1.6f,1.8f,.8f);Telegraph++;
                }
                else
                {
                    // The landing: shards bursting outward and sliding to rest, glints fading.
                    double u=Math.Min(1,age/.6);
                    for(int k=0;k<14;k++){double a=H(k,i)*Math.PI*2,d=(.4+H(k,i+3)*2.6)*(1-(1-u)*(1-u)),px=g.X+Math.Cos(a)*d,pz=g.Z+Math.Sin(a)*d,yy=.04+Math.Max(0,(.6-age)*1.2*Math.Sin(Math.PI*u));
                        Flat(px,yy,pz,.08+H(k,5)*.14,.05+H(k,6)*.1,a+t*(1-u)*6,.5f,.6f,.7f,.85f,false,0);Glass++;
                        if(Frac(t*3+H(k,8))<.25){Sprite(px,yy+.05,pz,.08,1.6f,1.7f,1.9f,(float)(1-age/NativeExpeditionStation.GlassSettle));Glass++;}}
                    if(age<.3){Sprite(g.X,.4,g.Z,1.2*(1-age/.3)+.2,.8f,.9f,1f,(float)(.6*(1-age/.3)));Telegraph++;}
                }
            }
        }
        /// <summary>The view the glass events are read from this frame (the presentation sets it before Build).</summary>
        public NativeStationView GlassView;
    }
}
