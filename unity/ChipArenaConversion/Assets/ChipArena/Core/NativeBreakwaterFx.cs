using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Breakwater's camera-facing effects for one frame (docs/arenas/BREAKWATER.md) into one <see cref="NativeFxQuads"/> (Runtime
    /// draws it as one effects NativeFxDraw batch), the frame's <see cref="NativeBeamList"/> while the look's volumetric beams are
    /// on (null: billboard shafts) and the eight practical light slots (<see cref="LightPos"/>, <see cref="LightRgb"/>,
    /// <see cref="LightBeam"/>: Runtime hands them to NativeLighting.PickedLights):
    /// <list type="bullet">
    /// <item>The lighthouse: the lamp's glare (it flashes as a beam crosses the eye) and its two turning beams; slot 0 is the lamp
    /// itself as a two-beam practical light, so the beams sweep the stone, the sea and the pilots.</item>
    /// <item>The sea: the swells' foam crests, the spray thrown up the wave wall where each breaks (lit as the beam passes), foam
    /// at the wall's foot, glints toward the afterglow, spindrift blown across the mole.</item>
    /// <item>The lights: the lanterns' halos on their swinging brackets (the seven nearest the eye are the other practical
    /// slots), the Stone's lamp, the halls' door lamps, the buoy's red flash, the boat's cabin, the town across the harbour, ships
    /// on the horizon.</item>
    /// <item>The wash (never reduced by the quality or the low tier): through the telegraph the zone's bright edges on every
    /// floor, chevrons running south, light pillars at its corners and the comber's lit crest; through the wash its foaming
    /// front, the water's streaks, the falls off the wall and the deck, the mist; after it the wet sheen and the drips.</item>
    /// </list>
    /// Pure: the same clocks, camera, quality and map give the same quads. Allocation free after <see cref="Prepare"/>.
    /// </summary>
    public sealed class NativeBreakwaterFx
    {
        public const int Capacity=4200;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        public NativeBeamList Beams;
        public int Telegraph,Atmosphere,Lights;
        public readonly float[] LightPos=new float[NativeMapLooks.MaxLights*4],LightRgb=new float[NativeMapLooks.MaxLights*3],LightBeam=new float[NativeMapLooks.MaxLights];
        private bool active;private string prepared;
        private double[] lamp=Array.Empty<double>();private int lampCount;
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz,quality;private bool low;
        private readonly double[] rank=new double[32],cand=new double[32*5];
        /// <summary>The fixed practical lights beside the lanterns (x, y, z, range, gain each): the lamp on the back wall of each
        /// hall's two rooms and the Mariners' Stone's lamp.</summary>
        public double[] Fixed=Array.Empty<double>();public int FixedCount;
        private static double H(int a,int b=0,int c=0)=>NativeBreakwaterLooks.Hash(a,b,c);
        private static double Frac(double v)=>v-Math.Floor(v);
        private static double L(int hex,int shift)=>NativeSceneryBuilder.Channel(hex,shift);
        public const int Warm=0xFFC878,Cold=0xA8ECFF,FoamHex=0xD6E8F0,Mist=0xA9BCCC;

        /// <summary>The map's fixed lamps, once per map (allocates; nothing for another map).</summary>
        public void Prepare(ContentMap map)
        {
            if(map?.id==prepared)return;prepared=map?.id;active=NativeBreakwaterLooks.Active(map?.id);var lamps=new List<double>();if(!active){lamp=Array.Empty<double>();lampCount=0;Fixed=Array.Empty<double>();FixedCount=0;return;}
            void Lamp(double x,double y,double z,int hex,double size,double blink=0){lamps.Add(x);lamps.Add(y);lamps.Add(z);lamps.Add(hex);lamps.Add(size);lamps.Add(blink);}
            foreach(var b in map.blocks??Array.Empty<ContentBlock>())
            {
                if(b?.id==null)continue;
                if(b.id=="breakwater-stone")Lamp(b.pos.x,b.pos.y+b.size.y/2+3.17,b.pos.z,Warm,1.5);
                else if(b.id.StartsWith("breakwater-end-",StringComparison.Ordinal)&&b.id.IndexOf("-lintel-",StringComparison.Ordinal)>0)Lamp(b.pos.x+(b.pos.x<0?1:-1)*(b.size.x/2+.16),b.pos.y-b.size.y/2+.7,b.pos.z,Warm,1.1);
                else if(b.id=="breakwater-turret"){foreach(double x in new[]{-.7,.7})Lamp(x,5.35,b.pos.z-b.size.z/2-.05,0xFFD79A,.7);}
            }
            // the mole running west to the shore: a lamp every 15 m; the far pier head's green light; the town; ships on the horizon
            for(double x=-34;x>=-170;x-=17)Lamp(x,4.3,-3.6,Warm,2.0);
            Lamp(-64,7.5,-118,0x5AFFA0,4.5,3.0);
            for(int i=0;i<64;i++){double a=H(81,i),x=-190-H(81,i,1)*190,z=-230+a*420,y=1.5+Math.Pow(H(81,i,2),1.6)*46;int hex=H(81,i,3)<.72?0xFFC27A:H(81,i,3)<.9?0xFFE6C0:0xA8D0FF;Lamp(x,y,z,hex,2.4+H(81,i,4)*3.2,H(81,i,5)<.2?-1:0);}
            Lamp(-120,7,520,0xFFFFFF,7);Lamp(-150,5,522,0xFF4030,6);Lamp(260,8,470,0xFFFFFF,7);Lamp(245,5,471,0x40FF70,6);Lamp(520,6,-380,0xFFFFFF,7);
            lamp=lamps.ToArray();lampCount=lamp.Length/6;
            var fixedLights=new List<double>();
            foreach(var b in map.blocks??Array.Empty<ContentBlock>())
            {
                if(b?.id==null)continue;
                if(b.id=="breakwater-stone"){fixedLights.Add(b.pos.x);fixedLights.Add(b.pos.y+b.size.y/2+3.17);fixedLights.Add(b.pos.z);fixedLights.Add(9);fixedLights.Add(1.5);}
                else if(b.id.StartsWith("breakwater-end-",StringComparison.Ordinal)&&b.id.EndsWith("-back",StringComparison.Ordinal))
                    foreach(double z in new[]{-2.35,2.35}){fixedLights.Add(b.pos.x+(b.pos.x<0?1:-1)*(b.size.x/2+.45));fixedLights.Add(2.85);fixedLights.Add(z);fixedLights.Add(7.5);fixedLights.Add(1.45);}
            }
            Fixed=fixedLights.ToArray();FixedCount=Fixed.Length/5;
        }

        /// <summary>Fills <see cref="Quads"/> and the light slots: `rig` after its Build for the same moment, `hazards` and the
        /// replicated state at field clock `now`, the render clock for the atmosphere, the camera and its right and up vectors,
        /// effects `quality` (0..1) and the low tier.</summary>
        public void Build(NativeBreakwaterRig rig,MapHazard[] hazards,int round,NativePhase phase,double fightAt,double now,double render,double camX,double camY,double camZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,double q,bool lowTier)
        {
            Quads.Clear();Telegraph=Atmosphere=Lights=0;if(!active)return;ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;quality=Math.Max(0,Math.Min(1,q));low=lowTier;
            double clock=NativeFactoryHazards.Clock(phase,fightAt,now);
            FarLamps(render);Glints(render);Sea(render);
            for(int i=0;i<(hazards?.Length??0);i++){var h=hazards[i];if(h!=null&&h.kind==NativeFactoryHazards.Vent)WashFx(h,round,clock,now,i);}
            Lighthouse(render);Lanterns(rig,render);Spindrift(render);PickLights(rig,render);
        }
        private int Count(int full)=>low?Math.Max(1,full/3):Math.Max(1,(int)Math.Round(full*(.35+.65*quality)));

        // ---------------- quad helpers ----------------
        private bool Sprite(double x,double y,double z,double size,double r,double g,double b,double a,bool additive,int mode=1)
        {
            double h=size/2,ax=(rx+ux)*h,ay=(ry+uy)*h,az=(rz+uz)*h,bx=(rx-ux)*h,by=(ry-uy)*h,bz=(rz-uz)*h;
            return Quads.Add(x-ax,y-ay,z-az,x+bx,y+by,z+bz,x+ax,y+ay,z+az,x-bx,y-by,z-bz,r,g,b,a,mode,additive,1);
        }
        private bool Hex(double x,double y,double z,double size,int hex,double a,bool additive,double gain=1)=>Sprite(x,y,z,size,L(hex,16)*gain,L(hex,8)*gain,L(hex,0)*gain,a,additive);
        private bool HexStreak(double ax,double ay,double az,double bx,double by,double bz,double w0,double w1,int hex,double a,double gain=1,bool additive=true)
        {
            double dx=bx-ax,dy=by-ay,dz=bz-az,vx=(ax+bx)/2-ex,vy=(ay+by)/2-ey,vz=(az+bz)/2-ez;
            double cx=dy*vz-dz*vy,cy=dz*vx-dx*vz,cz=dx*vy-dy*vx,l=Math.Sqrt(cx*cx+cy*cy+cz*cz);if(l<1e-6)return false;cx/=l;cy/=l;cz/=l;
            return Quads.Add(ax-cx*w0/2,ay-cy*w0/2,az-cz*w0/2,bx-cx*w1/2,by-cy*w1/2,bz-cz*w1/2,bx+cx*w1/2,by+cy*w1/2,bz+cz*w1/2,ax+cx*w0/2,ay+cy*w0/2,az+cz*w0/2,L(hex,16)*gain,L(hex,8)*gain,L(hex,0)*gain,a,1,additive,1);
        }
        /// <summary>A flat quad on a floor: x0..x1, z0..z1 at height y.</summary>
        private bool Floor(double x0,double x1,double z0,double z1,double y,int hex,double a,double gain=1,int mode=0,bool additive=true)
        =>Quads.Add(x0,y,z0,x1,y,z0,x1,y,z1,x0,y,z1,L(hex,16)*gain,L(hex,8)*gain,L(hex,0)*gain,a,mode,additive,1);
        /// <summary>A vertical sheet across x at z, y0..y1.</summary>
        private bool Sheet(double x0,double x1,double z,double y0,double y1,int hex,double a,double gain=1,bool additive=true,int mode=1)
        =>Quads.Add(x0,y0,z,x1,y0,z,x1,y1,z,x0,y1,z,L(hex,16)*gain,L(hex,8)*gain,L(hex,0)*gain,a,mode,additive,1);
        private void Beam(double ax,double ay,double az,double bx,double by,double bz,double r0,double r1,int hex,double gain,double alpha,double seed)
        {
            double r=L(hex,16)*gain,g=L(hex,8)*gain,b=L(hex,0)*gain;
            if(Beams!=null){Beams.Add(ax,ay,az,bx,by,bz,r0,r1,r*alpha*NativeBeamList.FromBillboard,g*alpha*NativeBeamList.FromBillboard,b*alpha*NativeBeamList.FromBillboard,.5,seed);return;}
            double dx=bx-ax,dy=by-ay,dz=bz-az,vx=(ax+bx)/2-ex,vy=(ay+by)/2-ey,vz=(az+bz)/2-ez,cx=dy*vz-dz*vy,cy=dz*vx-dx*vz,cz=dx*vy-dy*vx,cl=Math.Sqrt(cx*cx+cy*cy+cz*cz);if(cl<1e-6)return;cx/=cl;cy/=cl;cz/=cl;
            for(int k=0;k<4;k++)for(int j=0;j<5;j++)
            {
                double t0=k/4.0,t1=(k+1)/4.0,w0=r0+(r1-r0)*t0,w1=r0+(r1-r0)*t1,o=(j-2)*.4,wt=Math.Exp(-o*o*2.2)*(1-.22*k);
                double px0=ax+dx*t0+cx*o*w0,py0=ay+dy*t0+cy*o*w0,pz0=az+dz*t0+cz*o*w0,px1=ax+dx*t1+cx*o*w1,py1=ay+dy*t1+cy*o*w1,pz1=az+dz*t1+cz*o*w1;
                Quads.Add(px0-cx*w0*.2,py0-cy*w0*.2,pz0-cz*w0*.2,px1-cx*w1*.2,py1-cy*w1*.2,pz1-cz*w1*.2,px1+cx*w1*.2,py1+cy*w1*.2,pz1+cz*w1*.2,px0+cx*w0*.2,py0+cy*w0*.2,pz0+cz*w0*.2,r,g,b,alpha*wt,0,true,1);
            }
        }

        // ---------------- the lighthouse ----------------
        private void Lighthouse(double t)
        {
            double x=NativeBreakwaterLooks.LightX,y=NativeBreakwaterLooks.LanternY,z=NativeBreakwaterLooks.LightZ,a=NativeBreakwaterLooks.BeamHeading(t),reach=NativeBreakwaterLooks.BeamReach,dip=Math.Sin(NativeBreakwaterLooks.BeamDip),lev=Math.Cos(NativeBreakwaterLooks.BeamDip);
            // the lamp: a steady heart, and the flash as a beam crosses the eye
            double on=NativeBreakwaterLooks.BeamOn(t,ex,ez),dist=Math.Sqrt((x-ex)*(x-ex)+(y-ey)*(y-ey)+(z-ez)*(z-ez)),cap=Math.Max(2,dist*.5);
            Hex(x,y,z,Math.Min(cap,2.6),0xFFF4D8,.95,true,2.2);Hex(x,y,z,Math.Min(cap,7),Warm,.3,true,1.3);Lights+=2;
            if(on>.02){Hex(x,y,z,Math.Min(cap,6+26*on),0xFFF0C8,.75*on,true,2.4);HexStreak(x-Math.Min(cap,30*on),y,z,x+Math.Min(cap,30*on),y,z,.5,.5,0xFFF4D8,.5*on,2.0);Lights+=2;}
            for(int s=-1;s<=1;s+=2)
            {
                double dx=Math.Sin(a)*s*lev,dz=Math.Cos(a)*s*lev;
                Beam(x+dx*1.2,y,z+dz*1.2,x+dx*reach,y-dip*reach,z+dz*reach,.7,8.5,0xFFE9B8,1.15,.085,s+2);Lights++;
            }
        }

        // ---------------- the sea ----------------
        private void Sea(double t)
        {
            double sea=NativeBreakwaterLooks.SeaY,wall=NativeBreakwaterLooks.WallZ;
            for(int i=0;i<NativeBreakwaterLooks.Swells;i++)
            {
                // the crest's foam line, brighter as the swell steepens, lit as the lamp's beam crosses it
                var s=NativeBreakwaterLooks.SwellAt(i,t);
                if(s.Height>.25&&s.U>.3){double lit=1+1.6*NativeBreakwaterLooks.BeamOn(t,s.X,s.Z),k=(s.U-.3)/.7;HexStreak(s.X-s.Half*.92,sea+s.Height+.03,s.Z-.2,s.X+s.Half*.92,sea+s.Height+.03,s.Z-.2,.5+.5*k,.5+.5*k,FoamHex,.55*k,.9*lit,false);Atmosphere++;}
                // the break: spray thrown up the wall's face and blown south over the walk
                double since=NativeBreakwaterLooks.SinceBreak(i,t,out double bx,out double half);if(since>=NativeBreakwaterLooks.SprayLife)continue;
                double u=since/NativeBreakwaterLooks.SprayLife,beam=1+1.8*NativeBreakwaterLooks.BeamOn(t,bx,wall);int cyc=(int)Math.Floor(t/NativeBreakwaterLooks.SwellPeriod+(double)i/NativeBreakwaterLooks.Swells);
                Sheet(bx-half,bx+half,wall+.06,sea,sea+2.5+5.5*Math.Sqrt(u),FoamHex,.32*(1-u)*(1-u),1.0*beam,false);Atmosphere++;
                int n=Count(34);
                for(int k=0;k<n;k++)
                {
                    double px=bx+(H(cyc,i*31+k,1)-.5)*2*half,v=6+H(cyc,i*31+k,2)*6.5,drift=.8+H(cyc,i*31+k,3)*2.2,y=sea+1+v*since-5.2*since*since,z=wall+.5-drift*since;
                    if(y<sea)continue;Hex(px,y,z,.7+2.4*u+H(cyc,i*31+k,4)*.8,k%4==0?0xF2FAFF:FoamHex,.42*(1-u),false,beam*(k%4==0?1.2:.85));Atmosphere++;
                }
            }
            // foam washing at the wall's foot and round the quay's face
            int m=Count(36);
            for(int i=0;i<m;i++)
            {
                double x=-23+H(91,i)*46,z=wall+.4+H(91,i,1)*1.6,k=.5+.5*Math.Sin(t*.75+z*.21+x*.03+i);Hex(x,sea+.15+.25*k,z,1.3+H(91,i,2)*1.5,FoamHex,.16+.2*k,false,.8);Atmosphere++;
            }
        }
        private void Glints(double t)
        {
            // the afterglow on the water toward the west, the lamp's road on the sea east of the mole
            int n=Count(54);double sea=NativeBreakwaterLooks.SeaY;
            for(int i=0;i<n;i++)
            {
                double x=-26-Math.Pow(H(95,i),1.4)*230,z=(H(95,i,1)-.5)*(30+(-x)*.55),k=Math.Sin(t*(1.6+H(95,i,2)*2.2)+i*2.3);if(k<.2)continue;
                double w=.8+(-x)*.012;Floor(x-w,x+w,z-.07,z+.07,sea+.12+NativeBreakwaterLooks.SeaAt(x,z,t),0xFFA878,.5*(k-.2),1.5,1);Atmosphere++;
            }
            int m=Count(22);
            for(int i=0;i<m;i++)
            {
                double x=NativeBreakwaterLooks.LightX+(H(96,i)-.5)*16,z=(i%2==0?1:-1)*(10+H(96,i,1)*34),k=Math.Sin(t*(1.9+H(96,i,2)*2)+i*1.7);if(k<.3)continue;
                Floor(x-.7,x+.7,z-.06,z+.06,sea+.12+NativeBreakwaterLooks.SeaAt(x,z,t),0xFFE6B0,.42*(k-.3),1.5,1);Atmosphere++;
            }
        }
        private void Spindrift(double t)
        {
            // spray blown off the sea across the mole: short pale streaks driven south round the eye
            int n=Count(46);
            for(int i=0;i<n;i++)
            {
                double f=Frac(t*(.34+H(97,i)*.2)+H(97,i,1)),x=ex+(H(97,i,2)-.5)*30,y=Math.Max(NativeBreakwaterLooks.SeaY+.6,ey-2+H(97,i,3)*7)-f*1.4,z=ez+14-f*28;
                HexStreak(x,y,z,x+.05,y-.07,z-1.1-H(97,i,4)*.9,.05,.02,0xDCEAF2,.2*Math.Sin(f*Math.PI),1.0,false);Atmosphere++;
            }
        }
        private void FarLamps(double t)
        {
            for(int i=0;i<lampCount;i++)
            {
                double x=lamp[i*6],y=lamp[i*6+1],z=lamp[i*6+2],size=lamp[i*6+4],blink=lamp[i*6+5];int hex=(int)lamp[i*6+3];
                double k=blink>0?(Frac(t/blink)<.3?1:0):blink<0?.6+.4*Math.Sin(t*(2+H(83,i)*5)+i):.9+.1*Math.Sin(t*1.7+i);if(k<=0)continue;
                Hex(x,y,z,size*.42,hex,.9*k,true,1.9);Hex(x,y,z,size,hex,.2*k,true,1.2);Lights+=2;
            }
            // the buoy's flash and the boat's cabin
            double b=NativeBreakwaterLooks.BuoyLamp(t);
            if(b>0){NativeBreakwaterLooks.Ride(NativeBreakwaterLooks.BuoyX,NativeBreakwaterLooks.BuoyZ,t,2.2,out double lift,out _,out _);Hex(NativeBreakwaterLooks.BuoyX,NativeBreakwaterLooks.SeaY+lift+2.75,NativeBreakwaterLooks.BuoyZ,2.6*b,0xFF4A38,.8*b,true,2.2);Lights++;}
            {NativeBreakwaterLooks.Ride(NativeBreakwaterLooks.BoatX,NativeBreakwaterLooks.BoatZ,t,1.3,out double lift,out _,out _);Hex(NativeBreakwaterLooks.BoatX-.4,NativeBreakwaterLooks.SeaY+lift+1.9,NativeBreakwaterLooks.BoatZ-.9,1.3,Warm,.5,true,1.5);Lights++;}
        }
        private void Lanterns(NativeBreakwaterRig rig,double t)
        {
            if(rig==null)return;
            for(int i=0;i<rig.Posts;i++){double x=rig.Lantern[i*3],y=rig.Lantern[i*3+1],z=rig.Lantern[i*3+2],k=.92+.08*Math.Sin(t*11+i*3);Hex(x,y,z,.62,0xFFE2B0,.8*k,true,2.0);Hex(x,y,z,2.3,Warm,.16*k,true,1.2);Lights+=2;}
        }

        // ---------------- the practical lights ----------------
        /// <summary>The lamp's light as a practical slot: its range (m) and gain; a lantern's range and gain.</summary>
        public const double LampRange=120,LampGain=1.35,LanternRange=9.5,LanternGain=1.45;
        private void PickLights(NativeBreakwaterRig rig,double t)
        {
            // slot 0: the lighthouse's lamp, two opposite beams turning with the optic (NativeWardLight.cginc WardBeam: 8 + yaw)
            LightPos[0]=(float)NativeBreakwaterLooks.LightX;LightPos[1]=(float)NativeBreakwaterLooks.LanternY;LightPos[2]=(float)NativeBreakwaterLooks.LightZ;LightPos[3]=(float)LampRange;
            LightRgb[0]=(float)(L(0xFFE9B8,16)*LampGain);LightRgb[1]=(float)(L(0xFFE9B8,8)*LampGain);LightRgb[2]=(float)(L(0xFFE9B8,0)*LampGain);LightBeam[0]=(float)(8+NativeBreakwaterLooks.BeamHeading(t));
            // the others: the lights nearest the eye (the lanterns where they hang this frame, the halls' room lamps, the Stone's
            // lamp), each easing out as the next one out comes to take its slot
            int posts=Math.Min(rig?.Posts??0,24),n=0,slots=NativeMapLooks.MaxLights-1;
            for(int i=0;i<posts;i++,n++){cand[n*5]=rig.Lantern[i*3];cand[n*5+1]=rig.Lantern[i*3+1];cand[n*5+2]=rig.Lantern[i*3+2];cand[n*5+3]=LanternRange;cand[n*5+4]=LanternGain*(.92+.08*Math.Sin(t*11+i*3));}
            for(int i=0;i<FixedCount&&n<32;i++,n++){cand[n*5]=Fixed[i*5];cand[n*5+1]=Fixed[i*5+1];cand[n*5+2]=Fixed[i*5+2];cand[n*5+3]=Fixed[i*5+3];cand[n*5+4]=Fixed[i*5+4];}
            for(int i=0;i<n;i++){double dx=cand[i*5]-ex,dy=cand[i*5+1]-ey,dz=cand[i*5+2]-ez;rank[i]=Math.Sqrt(dx*dx+dy*dy+dz*dz);}
            double edge=double.PositiveInfinity;
            if(n>slots){
                // the distance of the nearest light left out: the (slots + 1)th smallest
                for(int i=0;i<n;i++){int nearer=0;for(int k=0;k<n;k++)if(rank[k]<rank[i]||rank[k]==rank[i]&&k<i)nearer++;if(nearer==slots)edge=rank[i];}}
            int s=1;
            for(int i=0;i<n&&s<NativeMapLooks.MaxLights;i++)
            {
                int nearer=0;for(int k=0;k<n;k++)if(rank[k]<rank[i]||rank[k]==rank[i]&&k<i)nearer++;if(nearer>=slots)continue;
                double fade=double.IsInfinity(edge)?1:Math.Max(0,Math.Min(1,(edge-rank[i])/3)),g=cand[i*5+4]*fade;
                LightPos[s*4]=(float)cand[i*5];LightPos[s*4+1]=(float)cand[i*5+1];LightPos[s*4+2]=(float)cand[i*5+2];LightPos[s*4+3]=(float)cand[i*5+3];
                LightRgb[s*3]=(float)(L(Warm,16)*g);LightRgb[s*3+1]=(float)(L(Warm,8)*g);LightRgb[s*3+2]=(float)(L(Warm,0)*g);LightBeam[s]=0;s++;
            }
            for(;s<NativeMapLooks.MaxLights;s++){LightPos[s*4]=0;LightPos[s*4+1]=-10000;LightPos[s*4+2]=0;LightPos[s*4+3]=1;LightRgb[s*3]=LightRgb[s*3+1]=LightRgb[s*3+2]=0;LightBeam[s]=0;}
        }

        // ---------------- the wash ----------------
        private void WashFx(MapHazard h,int round,double clock,double now,int index)
        {
            NativeBreakwaterLooks.WashAt(h,round,clock,out double warn,out double wash,out double level,out double after);
            double x0=h.a.x-h.size.x/2,x1=h.a.x+h.size.x/2,wall=NativeBreakwaterLooks.WallZ,quay=NativeBreakwaterLooks.QuayZ,sea=NativeBreakwaterLooks.SeaY;
            if(warn>0||wash>=0)
            {
                // the zone on every floor: its edges burning cold, a faint fill, chevrons running south (the way it will carry you)
                double beat=.6+.4*Math.Sin(now*(5+warn*16)),w=wash>=0?1:warn,gain=1.4+1.4*w,a=wash>=0?.9:.45+.4*warn*beat;int hex=wash>=0?0xE8FBFF:Cold;
                for(int lv=0;lv<3;lv++)
                {
                    double y=(lv==0?NativeBreakwaterLooks.WalkY:lv==1?0:NativeBreakwaterLooks.QuayY)+.03,zn=lv==0?wall:lv==1?5:-5,zs=lv==0?5:lv==1?-5:quay;
                    Floor(x0-.09,x0+.09,zs,zn,y,hex,a,gain);Floor(x1-.09,x1+.09,zs,zn,y,hex,a,gain);Floor(x0,x1,zs,zn,y-.005,Cold,.05+.09*w,1.0,1);Telegraph+=3;
                    for(int k=0;k<3;k++)
                    {
                        double z=zn-Frac(now*(.5+.5*w)+k/3.0+lv*.2)*(zn-zs),cx=(x0+x1)/2,arm=1.5;
                        // a chevron: two strokes meeting at the point, the point to the south
                        Quads.Add(cx-arm,y,z+.9,cx-arm,y,z+.55,cx,y,z-.35,cx,y,z,L(hex,16)*gain,L(hex,8)*gain,L(hex,0)*gain,.5*w,0,true,1);
                        Quads.Add(cx+arm,y,z+.9,cx+arm,y,z+.55,cx,y,z-.35,cx,y,z,L(hex,16)*gain,L(hex,8)*gain,L(hex,0)*gain,.5*w,0,true,1);Telegraph+=2;
                    }
                }
                // light pillars at the zone's corners on the deck and the walk, so it reads over cover and from the far end
                for(int e=0;e<2;e++){double px=e==0?x0:x1;HexStreak(px,0,-5,px,1.3+2.2*w,-5,.24,.04,Cold,.5*w,1.8);HexStreak(px,0,3.3,px,1.3+2.2*w,3.3,.24,.04,Cold,.5*w,1.8);HexStreak(px,NativeBreakwaterLooks.WalkY,wall-.3,px,NativeBreakwaterLooks.WalkY+1.3+2.2*w,wall-.3,.24,.04,Cold,.5*w,1.8);Telegraph+=3;}
                // the comber: its crest lit, spray torn off its top
                NativeBreakwaterLooks.Comber(warn,wash,out double cz,out double ch);
                HexStreak(x0-1,sea+ch+.05,cz-.5,x1+1,sea+ch+.05,cz-.5,.9,.9,0xF2FAFF,.5+.4*w,1.4+w,false);HexStreak(x0-1,sea+ch+.1,cz-.5,x1+1,sea+ch+.1,cz-.5,.3,.3,Cold,.7*w,2.2);Telegraph+=2;
                for(int i=0;i<16;i++){double f=Frac(now*1.3+H(index,i,60)),px=x0-1+H(index,i,61)*(x1-x0+2);Hex(px,sea+ch+f*1.8,cz-.5-f*2.2,.5+f*1.2,FoamHex,.5*(1-f)*w,false,1.1);Telegraph++;}
            }
            if(wash>=0)
            {
                // the wash: the foaming front running south, the water's streaks behind it, the falls off the wall and the deck, mist
                double front=NativeBreakwaterLooks.WashFront(wash),fy=front>5?NativeBreakwaterLooks.WalkY:front>-5?0:NativeBreakwaterLooks.QuayY;
                if(front>quay){Sheet(x0,x1,front,fy,fy+1.0+.5*level,0xF2FAFF,.55*level,1.5,false);Sheet(x0,x1,front+.25,fy,fy+.6,Cold,.4*level,2.0);Telegraph+=2;}
                for(int i=0;i<90;i++)
                {
                    double speed=9+H(index,i,70)*6,f=Frac(now*speed/(wall-quay)+H(index,i,71)),z=wall-f*(wall-quay);if(z<front)continue;
                    double y=(z>5?NativeBreakwaterLooks.WalkY:z>-5?0:NativeBreakwaterLooks.QuayY)+.08+H(index,i,72)*.25,x=x0+H(index,i,73)*(x1-x0),len=.9+H(index,i,74)*1.5;
                    HexStreak(x,y,z,x,y,z+len,.1,.03,i%3==0?0xFFFFFF:Cold,.7*level,1.7);Telegraph++;
                }
                for(int e=0;e<2;e++)
                {
                    double fz=e==0?5.0:-5.0;
                    if(front>=fz)continue;double top=fz>0?NativeBreakwaterLooks.WalkY:0;
                    for(int i=0;i<10;i++){double f=Frac(now*1.6+H(index,i,80+(int)fz)),x=x0+(i+.5)*(x1-x0)/10;Hex(x,top-f*2.2,fz-.3-f*.5,.8+f*.9,FoamHex,.5*level*(1-f*.6),false,1.2);Telegraph++;}
                }
                int m=Count(18);for(int i=0;i<m;i++){double f=Frac(now*.7+H(index,i,85)),x=x0+H(index,i,86)*(x1-x0),z=wall-H(index,i,87)*(wall-Math.Max(front,quay));Hex(x,(z>5?NativeBreakwaterLooks.WalkY:z>-5?0:NativeBreakwaterLooks.QuayY)+.5+f*2.4,z-f*1.5,1.6+f*2.2,Mist,.2*level*(1-f),false);Atmosphere++;}
            }
            if(after<2.6)
            {
                // the wet stone's sheen and the last of the water running off the edges
                double k=1-after/2.6;
                for(int lv=0;lv<3;lv++){double y=(lv==0?NativeBreakwaterLooks.WalkY:lv==1?0:NativeBreakwaterLooks.QuayY)+.025,zn=lv==0?wall:lv==1?5:-5,zs=lv==0?5:lv==1?-5:quay;Floor(x0,x1,zs,zn,y,Cold,.1*k,1.0,1);Atmosphere++;}
                int n=Count(14);for(int i=0;i<n;i++){double f=Frac(now*1.1+H(index,i,90)),x=x0+H(index,i,91)*(x1-x0);double fz=i%2==0?-5:quay,top=i%2==0?0:NativeBreakwaterLooks.QuayY;Hex(x,top-f*1.8,fz-.15,.14,0xDFF4FF,.6*k*(1-f),true,1.6);Atmosphere++;}
            }
        }
    }
}
