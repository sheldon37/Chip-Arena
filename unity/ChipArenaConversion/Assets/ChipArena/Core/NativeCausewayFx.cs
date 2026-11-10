using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Starfall Causeway's camera-facing effects for one frame (docs/arenas/CAUSEWAY.md) into one <see cref="NativeFxQuads"/> (Runtime
    /// draws it as one effects NativeFxDraw batch) and, while the look's volumetric beams are on, the frame's
    /// <see cref="NativeBeamList"/> (null: billboard shafts):
    /// <list type="bullet">
    /// <item>The void: the deep star field all round and below (the sky dome draws only the stars above the horizon), the bright
    /// stars with their spikes, the nebulae in layered veils with dust lanes across them, the river of stars drifting far below,
    /// shooting stars, the distant sun's glare and the planet's atmosphere.</item>
    /// <item>The stone: the braziers' cold fire, the crystals' halos and the spires' beacons, the gate lanterns, the keystone's
    /// orbiting glyph ring and the motes rising through the Breach, star-dust drifting round the eye.</item>
    /// <item>The hazards (never reduced by the quality or the low tier): a falling star's impact ring, inner glyph ring and fill,
    /// the star kindling overhead with its aiming shaft, its fall (head, tail, sparks), the impact flash, shockwave and shards' dust,
    /// the scorched crater's glow and smoke; the comet's approach along the bridge's axis, the lane's glow through the warning and
    /// the danger, the pass (head, coma, ion and dust tails, glitter) and its fading wake; the solar wind's shimmering front, its
    /// streaming gust and the front sweeping across the section.</item>
    /// </list>
    /// Pure: the same clocks, camera, quality and map give the same quads. Allocation free after <see cref="Prepare"/>.
    /// </summary>
    public sealed class NativeCausewayFx
    {
        public const int Capacity=7000;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        public NativeBeamList Beams;
        public int Telegraph,Atmosphere,Lights;
        private bool active;private string prepared;
        private double[] lamp=Array.Empty<double>();private int lampCount;private double[] glow=Array.Empty<double>();private int glowCount;
        private readonly float[] star=new float[StarCount*7];
        private const int StarCount=1100,RiverCount=760;
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz,quality;private bool low;
        private static double H(int a,int b=0,int c=0)=>NativeCausewayLooks.Hash(a,b,c);
        private static double Frac(double v)=>v-Math.Floor(v);
        private static double L(int hex,int shift)=>NativeSceneryBuilder.Channel(hex,shift);

        /// <summary>The map's fixed lamps and the star field, once per map (allocates; nothing for another map).</summary>
        public void Prepare(ContentMap map)
        {
            if(map?.id==prepared)return;prepared=map?.id;active=NativeCausewayLooks.Active(map?.id);var lamps=new List<double>();var glows=new List<double>();if(!active){lamp=glow=Array.Empty<double>();lampCount=glowCount=0;return;}
            void Lamp(double x,double y,double z,int hex,double size,double blink=0){lamps.Add(x);lamps.Add(y);lamps.Add(z);lamps.Add(hex);lamps.Add(size);lamps.Add(blink);}
            void Glow(double x,double y,double z,int kind){glows.Add(x);glows.Add(y);glows.Add(z);glows.Add(kind);}
            foreach(var b in map.blocks??Array.Empty<ContentBlock>())
            {
                if(b?.id==null)continue;double top=b.pos.y+b.size.y/2;
                if(b.id.StartsWith("causeway-pylon-",StringComparison.Ordinal)){Lamp(b.pos.x,top+.75,b.pos.z,0x7FE0FF,1.3);}
                else if(b.id.StartsWith("causeway-brazier-",StringComparison.Ordinal)){Glow(b.pos.x,top,b.pos.z,0);Lamp(b.pos.x,top+.5,b.pos.z,0x7FE0FF,2.6);}
                else if(b.id.StartsWith("causeway-spire-",StringComparison.Ordinal)){Glow(b.pos.x,top,b.pos.z,1);Lamp(b.pos.x,top+1.6,b.pos.z,0xB48CFF,3.2);}
                else if(b.id.StartsWith("causeway-gate-",StringComparison.Ordinal)&&!b.id.EndsWith("-head",StringComparison.Ordinal))
                {double ud=b.pos.z>0?b.pos.z+b.size.z/2-.25:b.pos.z-b.size.z/2+.25;foreach(int sd in new[]{-1,1})Lamp(b.pos.x+sd*(b.size.x/2+.15),2.06,ud,0xFFC27A,.9);}
                else if(b.id.StartsWith("causeway-altar-",StringComparison.Ordinal)){foreach(int sd in new[]{-1,1})Lamp(b.pos.x+sd*(b.size.x/2+.08),2.0,b.pos.z,0x7FE0FF,1.6);}
                else if(b.id.StartsWith("causeway-hall-",StringComparison.Ordinal)&&b.id.EndsWith("-arch",StringComparison.Ordinal)){foreach(int sd in new[]{-1,1})Lamp(b.pos.x+sd*(b.size.x/2+.08),b.pos.y,b.pos.z,0xFFB45A,1.2);}
                else if(b.id=="causeway-keystone"){Glow(0,-1.4,0,2);}
            }
            // the far lights: the lost spans' crystals, the shrine and the colossus's sword
            Lamp(40,-7.4,150,0x7FE0FF,6);Lamp(-78,-11,-142,0x7FE0FF,5,0);Lamp(-150,29.4,90,0x7FE0FF,5);Lamp(170,-15,110,0x7FE0FF,5);
            lamp=lamps.ToArray();lampCount=lamp.Length/6;glow=glows.ToArray();glowCount=glow.Length/4;
            // the star field: a shell 1500 m out, denser toward and below the horizon (the sky dome draws the upper stars)
            for(int i=0;i<StarCount;i++)
            {
                double u=H(601,i),v=H(601,i,1),y=i<StarCount*3/4?-1+1.25*u:-.2+1.2*u,a=v*2*Math.PI,r=Math.Sqrt(Math.Max(0,1-y*y));double d=1500;
                star[i*7]=(float)(Math.Cos(a)*r*d);star[i*7+1]=(float)(y*d);star[i*7+2]=(float)(Math.Sin(a)*r*d);
                double t=H(601,i,2);int hex=t<.62?0xDDE8FF:t<.8?0xFFE6B8:t<.9?0xB8CCFF:t<.96?0xFFB890:0xC8B0FF;
                double m=Math.Pow(H(601,i,3),3);star[i*7+3]=(float)L(hex,16);star[i*7+4]=(float)L(hex,8);star[i*7+5]=(float)L(hex,0);star[i*7+6]=(float)(.4+m*2.6);
            }
        }

        /// <summary>Fills <see cref="Quads"/>: `hazards` and the replicated state at field clock `now`, the render clock for the
        /// atmosphere, the camera and its right and up vectors, effects `quality` (0..1) and the low tier.</summary>
        public void Build(MapHazard[] hazards,int round,NativePhase phase,double fightAt,double now,double render,double camX,double camY,double camZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,double q,bool lowTier)
        {
            Quads.Clear();Telegraph=Atmosphere=Lights=0;if(!active)return;ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;quality=Math.Max(0,Math.Min(1,q));low=lowTier;
            double clock=NativeFactoryHazards.Clock(phase,fightAt,now);
            // the void behind everything first (its dark dust lanes over its own glow), then the hazards, then the near glows
            Nebulae(render);Field(render);River(render);Dust(render);Shooting(render);Sun();PlanetHalo();
            for(int i=0;i<(hazards?.Length??0);i++)
            {
                var h=hazards[i];if(h==null)continue;
                if(h.kind==NativeFactoryHazards.Press)StarFx(h,round,clock,now,i);
                else if(NativeCyberCityHazards.IsMaglev(h))CometFx(h,NativeCausewayLooks.CometAt(h,phase,round,fightAt,now),now,i);
                else if(h.kind==NativeFactoryHazards.Vent)WindFx(h,round,clock,now,i);
            }
            Braziers(render);Keystone(render);Motes(render);LampHalos(render);
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
        /// <summary>A flat ring of `n` segments at height y, radius `r`, each `w` wide, turned by `phase`.</summary>
        private void Ring(double x,double y,double z,double r,double w,int n,double phase,int hex,double a,double gain,double gap=0)
        {
            double seg=Math.PI*r/n*(1-gap);for(int i=0;i<n;i++){double t=phase+i*2*Math.PI/n;FlatHex(x+Math.Sin(t)*r,y,z+Math.Cos(t)*r,seg,w/2,t,hex,a,true,gain,0);Telegraph++;}
        }
        /// <summary>A vertical sheet from (x0, z0) to (x1, z1), y0..y1.</summary>
        private bool Sheet(double x0,double z0,double x1,double z1,double y0,double y1,double r,double g,double b,double a,bool additive,int mode=0)
        =>Quads.Add(x0,y0,z0,x1,y0,z1,x1,y1,z1,x0,y1,z0,r,g,b,a,mode,additive,1);
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

        // ---------------- the void ----------------
        private static readonly double[] NebX={-900,700,-300,1100,0},NebY={-650,240,520,-160,-1250},NebZ={900,1000,-1350,300,-200},NebR={520,420,380,340,760};
        private static readonly int[] NebA={0xFF4FB0,0x3F7CFF,0x9A5CFF,0x2FE0B8,0x4A5CFF},NebB={0x7A4CFF,0x2FD8FF,0xFF8A6A,0x3F9CFF,0x9A4CFF};
        private void Nebulae(double t)
        {
            // each a veil of soft additive clouds in two hues round a brighter core, dark dust lanes laid over it
            for(int k=0;k<NebX.Length;k++)
            {
                int n=Count(k==4?30:22);double cx=NebX[k],cy=NebY[k],cz=NebZ[k],R=NebR[k];
                for(int i=0;i<n;i++)
                {
                    double a=H(701+k,i)*2*Math.PI,b=(H(701+k,i,1)-.5)*Math.PI*.7,r=R*Math.Pow(H(701+k,i,2),.7);double px=cx+Math.Cos(a)*Math.Cos(b)*r,py=cy+Math.Sin(b)*r*.55,pz=cz+Math.Sin(a)*Math.Cos(b)*r;
                    double size=R*(.3+H(701+k,i,3)*.6)*(1-.4*r/R),breathe=.85+.15*Math.Sin(t*.07+i);int hex=NativeNewStageScenery.Mix(NebA[k],NebB[k],H(701+k,i,4));
                    Hex(px,py,pz,size,hex,(.022+.05*(1-r/R))*breathe,true,1.3);Atmosphere++;
                }
                Hex(cx,cy,cz,R*.35,NativeNewStageScenery.Mix(NebA[k],0xFFFFFF,.4),.09,true,1.5);
                int m=Count(7);for(int i=0;i<m;i++){double a=H(721+k,i)*2*Math.PI,r=R*.6*H(721+k,i,1);Hex(cx+Math.Cos(a)*r,cy+(H(721+k,i,2)-.5)*R*.4,cz+Math.Sin(a)*r,R*(.18+H(721+k,i,3)*.25),0x04030A,.5,false);Atmosphere++;}
            }
        }
        private void Field(double t)
        {
            int n=Count(StarCount);
            for(int i=0;i<n;i++)
            {
                int o=i*7;double x=star[o],y=star[o+1],z=star[o+2],m=star[o+6],tw=.75+.25*Math.Sin(t*(1.2+H(611,i)*2.4)+i*1.7);
                Sprite(x,y,z,m*4.6,star[o+3]*1.6*m,star[o+4]*1.6*m,star[o+5]*1.6*m,Math.Min(1,.5+.5*m)*tw,true);Atmosphere++;
                if(m>2.2&&!low){double s=m*30;Streak(x-rx*s,y-ry*s,z-rz*s,x+rx*s,y+ry*s,z+rz*s,1.2,1.2,star[o+3]*.7,star[o+4]*.7,star[o+5]*.7,.5*tw,true);Streak(x-ux*s,y-uy*s,z-uz*s,x+ux*s,y+uy*s,z+uz*s,1.2,1.2,star[o+3]*.7,star[o+4]*.7,star[o+5]*.7,.5*tw,true);Atmosphere+=2;}
            }
        }
        private void River(double t)
        {
            // the river of stars far below: a band of stars drifting along its S, glowing veils down its length
            int veils=Count(36);for(int i=0;i<veils;i++){double s=-1+2*(i+.5)/veils;NativeCausewayLooks.River(s,out double x,out double z);Hex(x,NativeCausewayLooks.RiverY-20,z,260+H(751,i)*160,i%3==0?0x6A5CFF:0x4A9CFF,.07,true,1.3);Atmosphere++;}
            int n=Count(RiverCount);double len=2800;
            for(int i=0;i<n;i++)
            {
                double s=Frac(H(761,i)+t*NativeCausewayLooks.RiverDrift/len*(.7+.6*H(761,i,1)))*2-1;NativeCausewayLooks.River(s,out double x,out double z);
                double g1=H(761,i,2)+H(761,i,3)-1,g2=H(761,i,4)+H(761,i,5)-1;double px=x+g2*40,pz=z+g1*NativeCausewayLooks.RiverWidth*1.4,py=NativeCausewayLooks.RiverY+(H(761,i,6)-.5)*60;
                double m=.6+Math.Pow(H(761,i,7),4)*3;int hex=H(761,i,8)<.7?0xD8E6FF:0xFFE0B0;Hex(px,py,pz,m*3.4,hex,.8,true,1.4*m);Atmosphere++;
            }
        }
        /// <summary>The mid layer between the far star shell and the eye's motes: stardust 70-320 m out round the bridge, drifting
        /// slowly, so the void shifts in depth as the eye moves.</summary>
        private void Dust(double t)
        {
            int n=Count(220);
            for(int i=0;i<n;i++)
            {
                double a=H(841,i)*2*Math.PI,r=70+Math.Pow(H(841,i,1),.7)*250,x=Math.Cos(a)*r*1.3+Math.Sin(t*.004+i)*6,z=Math.Sin(a)*r,y=-160+H(841,i,2)*260+Math.Sin(t*.006+i*1.3)*4;
                double tw=.6+.4*Math.Sin(t*(.8+H(841,i,3)*1.6)+i);int hex=H(841,i,4)<.7?0xCFE0FF:0xFFE2B8;Hex(x,y,z,.3+.5*H(841,i,5)+r*.0035,hex,.75*tw,true,1.5);Atmosphere++;
            }
        }
        private void Shooting(double t)
        {
            // a shooting star every 1.6 s somewhere in the sky or falling through the void below, 0.9 s each
            for(int k=0;k<3;k++)
            {
                const double every=1.6,life=.9;double tk=t+k*every/3;double slot=Math.Floor(tk/every);double age=tk-slot*every;if(age>life)continue;int c=(int)slot*3+k;
                double a=H(781,c)*2*Math.PI,el=-.5+H(781,c,1)*1.1,d=700+H(781,c,2)*400;double sx=Math.Cos(a)*Math.Cos(el)*d,sy=Math.Sin(el)*d,sz=Math.Sin(a)*Math.Cos(el)*d;
                double da=a+Math.PI/2*(H(781,c,3)<.5?1:-1),vx=Math.Cos(da)*.8,vz=Math.Sin(da)*.8,vy=-.6;double speed=260,u=age/life;
                double hx=sx+vx*speed*age,hy=sy+vy*speed*age,hz=sz+vz*speed*age,tail=60+H(781,c,4)*60;double fade=Math.Sin(u*Math.PI);
                HexStreak(hx,hy,hz,hx-vx*tail,hy-vy*tail,hz-vz*tail,2.2,.1,0xE8F0FF,.8*fade,1.6);Hex(hx,hy,hz,7,0xFFFFFF,.9*fade,true,1.8);Atmosphere+=2;
            }
        }
        private void Sun()
        {
            double d=1800,x=NativeCausewayLooks.SunX*d,y=NativeCausewayLooks.SunY*d,z=NativeCausewayLooks.SunZ*d;
            Hex(x,y,z,90,0xFFFFFF,1,true,3.0);Hex(x,y,z,260,0xBFD4FF,.35,true,1.4);Hex(x,y,z,620,0x8AA8FF,.1,true,1.2);Lights+=3;
            if(!low){double s=420;HexStreak(x-rx*s,y-ry*s,z-rz*s,x+rx*s,y+ry*s,z+rz*s,7,7,0xD8E4FF,.45,1.6);HexStreak(x-ux*s,y-uy*s,z-uz*s,x+ux*s,y+uy*s,z+uz*s,7,7,0xD8E4FF,.45,1.6);Lights+=2;}
        }
        private void PlanetHalo()
        {
            // the atmosphere: soft glows at the planet's centre (the sphere hides their middle, leaving the limb lit)
            double x=NativeCausewayLooks.PlanetX,y=NativeCausewayLooks.PlanetY,z=NativeCausewayLooks.PlanetZ,R=NativeCausewayLooks.PlanetR;
            Hex(x,y,z,R*2.25,0xFFE6B8,.32,true,1.3);Hex(x,y,z,R*2.7,0x9AB8FF,.14,true,1.2);Lights+=2;
        }

        // ---------------- the stone ----------------
        private void Braziers(double t)
        {
            for(int i=0;i<glowCount;i++)
            {
                int o=i*4;double x=glow[o],y=glow[o+1],z=glow[o+2];int kind=(int)glow[o+3];
                if(kind==0)
                {
                    // cold fire: wisps curling up off the crystals, cyan to violet, and warm sparks
                    int n=Count(16);for(int k=0;k<n;k++){double f=Frac(t*.9+H(801,i,k)),sw=Math.Sin(t*2.6+k*1.3)*.18*f;double px=x+Math.Cos(k*2.4)*.18+sw,pz=z+Math.Sin(k*2.4)*.18,py=y+.25+f*1.9;
                        int hex=NativeNewStageScenery.Mix(0x7FE8FF,0xB48CFF,f);Hex(px,py,pz,(.55-.3*f),hex,.75*(1-f),true,1.7);Atmosphere++;}
                    for(int k=0;k<Count(5);k++){double f=Frac(t*.55+H(802,i,k));Hex(x+(H(803,i,k)-.5)*.6,y+.4+f*2.6,z+(H(804,i,k)-.5)*.6,.06,0xFFD08A,.9*(1-f),true,2.2);Atmosphere++;}
                    if(!low)Beam(x,y+.3,z,x,y+4.4,z,.3,1.1,0x7FE0FF,1.0,.05,i);
                }
                else if(kind==1)Beam(x,y+2.4,z,x,y+40,z,.25,2.4,0xB48CFF,1.1,.05,i+10);
            }
        }
        private void Keystone(double t)
        {
            // the glyph ring turning round the floating keystone, and motes rising up through the Breach
            int n=12;for(int k=0;k<n;k++){double a=t*.35+k*2*Math.PI/n,r=2.05;double x=Math.Sin(a)*r,z=Math.Cos(a)*r*1.15,y=-1.3+Math.Sin(t*.8+k)*.08;FlatHex(x,y,z,.22,.1,a,0xFFB45A,.8,true,1.6,0);Hex(x,y,z,.5,0xFFB45A,.3,true,1.2);Atmosphere+=2;}
            int m=Count(26);for(int k=0;k<m;k++){double f=Frac(t*.12+H(811,k));double x=(H(812,k)-.5)*6,z=(H(813,k)-.5)*9,y=-14+f*16;Hex(x,y,z,.12,k%2==0?0x7FE0FF:0xFFD08A,.8*Math.Sin(f*Math.PI),true,1.6);Atmosphere++;}
        }
        private void Motes(double t)
        {
            int n=Count(70);const double box=13;
            for(int i=0;i<n;i++)
            {
                double fx=Frac(H(821,i)+t*.011),fy=Frac(H(821,i,1)+t*.017),fz=Frac(H(821,i,2)-t*.008);double x=ex+(fx-.5)*2*box,y=ey-3+fy*8,z=ez+(fz-.5)*2*box;
                double d=Math.Sqrt((x-ex)*(x-ex)+(y-ey)*(y-ey)+(z-ez)*(z-ez));if(d<1.5)continue;Hex(x,y,z,.05+.03*H(821,i,3),i%3==0?0xB8E4FF:0xFFE0B0,.7*Math.Min(1,(box-d)/4),true,1.4);Atmosphere++;
            }
        }
        private void LampHalos(double t)
        {
            for(int i=0;i<lampCount;i++)
            {
                int o=i*6;double x=lamp[o],y=lamp[o+1],z=lamp[o+2],size=lamp[o+4],blink=lamp[o+5];int hex=(int)lamp[o+3];
                if(blink>0&&Frac(t/blink+H(i,3))>.18)continue;double d=Math.Sqrt((x-ex)*(x-ex)+(y-ey)*(y-ey)+(z-ez)*(z-ez));double grow=1+Math.Min(3,d/60);double pulse=.85+.15*Math.Sin(t*1.7+i);
                Hex(x,y,z,size*grow,hex,.5*pulse,true,1.2);if(size>1.5&&!low)Hex(x,y,z,size*2.4*grow,hex,.1,true);Lights++;
            }
        }

        // ---------------- the falling stars ----------------
        private void StarFx(MapHazard h,int round,double clock,double now,int index)
        {
            var s=NativeCausewayLooks.StarAt(h,round,clock);double x=s.X,y=s.Y+.03,z=s.Z,R=Math.Sqrt(2)*h.size.x/2+.42;
            if(s.Warn>0)
            {
                // the ground: the impact ring (beats faster toward the strike), the inner glyph ring turning, the fill growing
                double w=s.Warn,beat=.6+.4*Math.Sin(now*(6+w*20));double gain=1.4+1.6*w;
                Ring(x,y,z,R,.32,32,now*.2,0xFF8A3A,.75+.25*beat,gain*1.3);Ring(x,y,z,R-.42,.08,32,-now*.4,0xFFD08A,.6,gain,.3);
                Ring(x,y,z,R*.62,.12,12,now*1.6,0xFFB45A,.8,gain,.55);
                // the countdown: a ring closing from the rim to the heart as the star nears
                Ring(x,y+.01,z,Math.Max(.15,R*(1-w)),.16,24,0,0xFFF0D0,.9,gain*1.2);
                FlatHex(x,y,z,R*w,R*w,0,0xFF6A2A,.3+.15*beat,true,1.4,1);Telegraph++;
                FlatHex(x,y-.005,z,R+1.2,R+1.2,0,0xFF5A20,.14*w+.05,true,1.0,1);Telegraph++;
                // light pillars round the rim, so the ring reads over cover and from the walkways
                for(int k=0;k<12;k++){double a=k*Math.PI/6+now*.3,px=x+Math.Sin(a)*R,pz=z+Math.Cos(a)*R,ht=1.4+2.2*w+.3*beat;HexStreak(px,y,pz,px,y+ht,pz,.22,.04,0xFF9A4A,.45*(.4+.6*w),1.8);Telegraph++;}
                // overhead: the star kindling on its path and the shaft it falls down, a column of light over the ring
                double grow=2+6*w*w;Hex(s.Px,s.Py,s.Pz,grow,0xFFF0D0,.9,true,2.2);Hex(s.Px,s.Py,s.Pz,grow*3.2,0xFF9A4A,.3,true,1.4);Telegraph+=2;
                Beam(s.Px,s.Py,s.Pz,x,y,z,.25,.9,0xFF8A3A,1.2,.05+.08*w,index);Beam(x,y,z,x,y+16,z,R*.4,R*.9,0xFF7A30,1.0,.04+.08*w,index+20);Telegraph+=2;
            }
            if(s.Fall>=0)
            {
                // the fall: the white-hot head, the long tail back up its path, sparks shed along it
                double tail=40;HexStreak(s.Px,s.Py,s.Pz,s.Px-s.Dx*tail,s.Py-s.Dy*tail,s.Pz-s.Dz*tail,2.0,.2,0xFF7A30,.85,2.0);HexStreak(s.Px,s.Py,s.Pz,s.Px-s.Dx*tail*.55,s.Py-s.Dy*tail*.55,s.Pz-s.Dz*tail*.55,.9,.1,0xFFF0D0,.9,2.6);
                Hex(s.Px,s.Py,s.Pz,3.2,0xFFFFFF,.95,true,3.0);Hex(s.Px,s.Py,s.Pz,9,0xFF9A4A,.45,true,1.8);Telegraph+=4;
                for(int k=0;k<14;k++){double f=H(index,s.Cycle,k)*.9,d=f*tail;Hex(s.Px-s.Dx*d+(H(index,k,1)-.5)*f*4,s.Py-s.Dy*d+(H(index,k,2)-.5)*f*4,s.Pz-s.Dz*d+(H(index,k,3)-.5)*f*4,.3,0xFFC06A,.9*(1-f),true,2.2);Telegraph++;}
                Ring(x,y,z,R,.2,32,now*.2,0xFFF0D0,.9,3.0);
            }
            if(s.Since<.3){double k=1-s.Since/.3;Hex(x,y+1.2,z,16*k+4,0xFFFFFF,.9*k,true,3.0);Hex(x,y+.6,z,30*k,0xFFB060,.35*k,true,1.8);Telegraph+=2;}
            if(s.Since<1.1)
            {
                // the shockwave across the stone, the dust thrown up and out, the fireball's last
                double u=s.Since/1.1,r=1.2+u*13;Ring(x,y+.05,z,r,.5*(1-u)+.1,40,0,0xFFD08A,.8*(1-u),2.2);
                FlatHex(x,y+.02,z,r*.7,r*.7,0,0xFF8A3A,.25*(1-u)*(1-u),true,1.6,1);Telegraph++;
                int n=Count(20);for(int i=0;i<n;i++){double a=i*2*Math.PI/n+H(index,i,7),d=1+u*(4+H(index,i,8)*3);Hex(x+Math.Cos(a)*d,y+.4+u*2.2*H(index,i,9),z+Math.Sin(a)*d,1.4+u*3.6,0x5A4A58,.4*(1-u),false);Atmosphere++;}
                Hex(x,y+1.4+u*2,z,4+u*4,0xFF6A2A,.5*(1-u)*(1-u),true,1.6);Telegraph++;
            }
            if(s.Since<7)
            {
                // the scorched crater: a dark burn, its rim glowing out over three seconds, embers and a thin smoke
                double u=s.Since/7;FlatHex(x,y-.012,z,R*.95,R*.95,H(index,s.Cycle)*6,0x0A0608,.7*(1-u),false,1,1);Atmosphere++;
                double hot=Math.Max(0,1-s.Since/3.2);if(hot>0){Ring(x,y+.01,z,R*.55,.24,20,H(index,s.Cycle,1)*6,0xFF6A2A,.7*hot,1.8*hot+.2);FlatHex(x,y+.01,z,R*.5,R*.5,0,0xFF4A1A,.3*hot,true,1.4,1);Telegraph++;}
                int n=Count(9);for(int i=0;i<n;i++){double f=Frac(now*.35+H(index,i,30));Hex(x+(H(index,i,31)-.5)*2.4+f*.8,y+.3+f*4.5,z+(H(index,i,32)-.5)*2.4,.7+f*2.2,0x2A2430,.3*(1-f)*(1-u),false);Atmosphere++;
                    if(i<5&&hot>0){double g=Frac(now*.8+H(index,i,33));Hex(x+(H(index,i,34)-.5)*2,y+.2+g*2,z+(H(index,i,35)-.5)*2,.06,0xFFA050,hot*(1-g),true,2.4);}}
            }
        }

        // ---------------- the comet ----------------
        private void CometFx(MapHazard h,in NativeHazardState st,double now,int index)
        {
            double x0=h.center.x-h.size.x/2,x1=h.center.x+h.size.x/2,cy=h.center.y,cz=h.center.z,hw=h.size.z/2;int dir=st.Direction==0?1:st.Direction;
            // the lane's glow along its channel through the warning and the danger, burning behind the pass, fading after
            double lane=0;switch(st.Phase){case NativeHazardPhase.Warn:lane=.25+.45*st.Level;break;case NativeHazardPhase.Danger:lane=.8+.2*Math.Sin(now*24);break;case NativeHazardPhase.Active:lane=1;break;case NativeHazardPhase.Clear:lane=.6*st.Level;break;}
            if(lane>0)
            {
                for(int half=-1;half<=1;half+=2)
                {
                    double a0=half<0?x0:3.0,a1=half<0?-3.0:x1;int hex=st.Phase==NativeHazardPhase.Warn?0xFFB45A:0x9FE8FF;
                    FlatHex((a0+a1)/2,.015,cz,(a1-a0)/2,hw,0,hex,.14*lane,true,1.4,1);Telegraph++;
                    for(int e=-1;e<=1;e+=2){FlatHex((a0+a1)/2,.02,cz+e*hw,(a1-a0)/2,.06,0,hex,.7*lane,true,2.2,0);Telegraph++;}
                }
                if(st.Phase==NativeHazardPhase.Danger||st.Phase==NativeHazardPhase.Warn&&st.Level>.5)
                {
                    int n=st.Phase==NativeHazardPhase.Danger?34:14;for(int i=0;i<n;i++){double f=Frac(now*1.4+H(index,i,40)),x=x0+H(index,i,41)*(x1-x0);if(Math.Abs(x)<3.1)continue;
                        Hex(x,.1+f*2.4,cz+(H(index,i,42)-.5)*hw*2,.12,st.Phase==NativeHazardPhase.Danger?0xCFF4FF:0xFFD08A,.9*(1-f),true,2.2);Telegraph++;}
                }
            }
            // the comet itself: far out along the bridge's axis and high, sweeping down toward the near portal through the warning,
            // in the lane through the pass, gone after it
            if(st.Phase==NativeHazardPhase.Warn||st.Phase==NativeHazardPhase.Danger||st.Phase==NativeHazardPhase.Active)
            {
                NativeCausewayLooks.CometHead(h,st,out double hx,out double hy,out double hz,out double near);
                // its glow never fills the eye: the sprites keep within a share of their distance from it
                double cd=Math.Sqrt((hx-ex)*(hx-ex)+(hy-ey)*(hy-ey)+(hz-ez)*(hz-ez)),cap=Math.Max(1.2,cd*.55);double core=Math.Min(1.6+3.4*near,cap*.3),coma=Math.Min(7+13*near,cap);
                // travel direction (unit), toward the lane from up the slope
                double vx=dir,vy=st.Phase==NativeHazardPhase.Active?0:-.16,vz=st.Phase==NativeHazardPhase.Active?0:-.06,vl=Math.Sqrt(vx*vx+vy*vy+vz*vz);vx/=vl;vy/=vl;vz/=vl;
                Hex(hx,hy,hz,core,0xFFFFFF,.95,true,2.4);Hex(hx,hy,hz,coma,0x9FE8FF,.32,true,1.5);Hex(hx,hy,hz,coma*2.2,0x4A8CFF,.07,true,1.2);Telegraph+=3;
                double ion=34+60*near,dust=24+30*near;
                HexStreak(hx,hy,hz,hx-vx*ion,hy-vy*ion+ion*.05,hz-vz*ion,core*.9,coma*.25,0xBFF0FF,.55,1.8);
                HexStreak(hx,hy,hz,hx-vx*dust,hy-vy*dust+dust*.18,hz-vz*dust+dust*.12,core*1.4,coma*.6,0xFFD8A0,.32,1.5);
                HexStreak(hx,hy,hz,hx-vx*ion*.5,hy-vy*ion*.5,hz-vz*ion*.5,core*.5,.2,0xFFFFFF,.7,2.2);Telegraph+=3;
                int n=st.Phase==NativeHazardPhase.Active?60:24;
                for(int i=0;i<n;i++){double f=H(index,st.Cycle,i),d=f*ion*.9;double jit=(H(index,i,50)-.5)*(1+f*6);Hex(hx-vx*d+jit*.3,hy-vy*d+(H(index,i,51)-.5)*(1+f*4),hz-vz*d+(H(index,i,52)-.5)*(1+f*5),.18+f*.3,i%3==0?0xFFE0B0:0xCFF4FF,.75*(1-f),true,2.2);Telegraph++;}
                if(st.Phase==NativeHazardPhase.Active){double dx=hx-ex;if(Math.Abs(dx)<10){double k=1-Math.Abs(dx)/10;Hex(hx,hy,hz,Math.Min(10*k,cap),0xE8F8FF,.22*k,true,1.4);Telegraph++;}}
            }
            if(st.Phase==NativeHazardPhase.Clear)
            {
                // the wake: glitter settling along the lane and ice drifting off it
                int n=40;for(int i=0;i<n;i++){double x=x0+H(index,st.Cycle,i+100)*(x1-x0),f=1-st.Level;if(Math.Abs(x)<3.1)continue;Hex(x,.2+H(index,i,101)*2.2-f*.6,cz+(H(index,i,102)-.5)*hw*2.4,.14,i%2==0?0xCFF4FF:0xFFE0B0,.8*st.Level*(.6+.4*Math.Sin(now*12+i)),true,2.2);Telegraph++;}
            }
        }

        // ---------------- the solar wind ----------------
        private void WindFx(MapHazard h,int round,double clock,double now,int index)
        {
            NativeCausewayLooks.WindAt(h,round,clock,out double warn,out double gust,out double after);
            double x0=h.a.x-h.size.x/2,x1=h.a.x+h.size.x/2,y0=h.a.y,y1=h.a.y+h.size.y,ax=h.axis.z>=0?1:-1,zu=-ax*h.size.z/2,zd=ax*h.size.z/2;
            if(warn>0)
            {
                // the shimmering front gathering on the upwind edge: a wavering curtain of light, motes beginning to stream
                int n=24;for(int i=0;i<n;i++){double x=x0+(i+.5)*(x1-x0)/n,wv=Math.Sin(now*7+i*1.3)*.35,z=zu+ax*(.6+warn*1.6+wv);
                    HexStreak(x,y0+.2,z,x+Math.Sin(now*3+i)*.3,y1,z+ax*.4,.35,.6,0xFFE6B0,.12+.28*warn,1.4);Telegraph++;}
                Sheet(x0,zu+ax*(.8+warn*1.6),x1,zu+ax*(.8+warn*1.6),y0,y1,L(0xFFD890,16)*1.2,L(0xFFD890,8)*1.2,L(0xFFD890,0)*1.2,.05+.1*warn,true,1);Telegraph++;
                int m=30;for(int i=0;i<m;i++){double f=Frac(now*.6*(.5+warn)+H(index,i,60));double x=x0+H(index,i,61)*(x1-x0),y=y0+.3+H(index,i,62)*(y1-y0-.6),z=zu+ax*f*h.size.z*warn;Hex(x,y,z,.1,0xFFE0B0,.8*warn*(1-f),true,2.0);Telegraph++;}
                // the arrows of the downwind edge: chevrons painted in light on the deck and walkways pointing where it will blow
                for(int k=0;k<4;k++){double x=x0+(k+.5)*(x1-x0)/4;for(int lv=0;lv<2;lv++){double fy=lv==0?.03:-3.17;double pz=ax*(2+Frac(now*.8+k*.25)*5);FlatHex(x,fy,pz,.5,.12,ax>0?.7:-.7+Math.PI,0xFFC06A,.5*warn,true,1.8,0);FlatHex(x,fy,pz,.5,.12,ax>0?-.7:.7+Math.PI,0xFFC06A,.5*warn,true,1.8,0);Telegraph+=2;}}
            }
            if(gust>0)
            {
                // the gust: streaks driven across the section, the bright front sweeping through it, dust lifted off the stone
                double age=(clock-(Math.Floor((clock-h.start-h.offset)/h.period)*h.period+h.start+h.offset))-(h.period-h.dwell);double front=Math.Max(0,Math.Min(1,age/(h.dwell*.6)));
                double zf=zu+ax*front*h.size.z;Sheet(x0,zf,x1,zf,y0,y1,L(0xFFE6B8,16)*1.5,L(0xFFE6B8,8)*1.5,L(0xFFE6B8,0)*1.5,.22*gust,true,1);Telegraph++;
                int n=150;for(int i=0;i<n;i++)
                {
                    double speed=18+H(index,i,70)*10,f=Frac(now*speed/h.size.z+H(index,i,71));double x=x0+H(index,i,72)*(x1-x0),y=y0+.2+H(index,i,73)*(y1-y0-.4),z=zu+ax*f*h.size.z;
                    if(ax>0?z>zf+1:z<zf-1)continue;double len=1.4+H(index,i,74)*2.4;HexStreak(x,y,z,x+Math.Sin(now*5+i)*.1,y+Math.Sin(now*3+i)*.08,z-ax*len,.11,.03,i%4==0?0xFFFFFF:0xFFD890,.8*gust,2.0);Telegraph++;
                }
                int m=Count(16);for(int i=0;i<m;i++){double f=Frac(now*.9+H(index,i,80));double x=x0+H(index,i,81)*(x1-x0),fy=H(index,i,82)<.5?0:-3.2,z=zu+ax*f*h.size.z;Hex(x,fy+.3+f*.8,z,.9+f*1.6,0xC8B8A8,.18*gust*(1-f),false);Atmosphere++;}
            }
            if(after<1.6){double k=1-after/1.6;int n=Count(20);for(int i=0;i<n;i++){double x=x0+H(index,i,90)*(x1-x0),y=y0+.4+H(index,i,91)*(y1-y0-.8),z=zd-ax*H(index,i,92)*4+ax*after*2;Hex(x,y-after*.5,z,.09,0xFFE0B0,.7*k,true,1.8);Atmosphere++;}}
        }
    }
}
