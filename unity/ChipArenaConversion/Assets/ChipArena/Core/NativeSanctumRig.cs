using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Hollow Sanctum's moving rigs for one frame (docs/arenas/SANCTUM.md "Hazards"), posed only from the replicated hazard
    /// state: the round index, the match phase, FightAt and the drawn arena's field clock, through the very functions the
    /// simulation decides hits with (NativeFactoryHazards.State for the chandeliers' crane and the censer's rail,
    /// NativeWardZeroHazards.At for the fire baskets' burners and the crypt gates' shutters), so host, guests and the killcam draw
    /// the same rig at the same moment and nothing new is sent.
    /// <list type="bullet">
    /// <item>The Fall of the Lights: four iron chandeliers on their chains from the vault, each over its zone at the crane's carry
    /// height. The one the crane serves creaks and swings ever wider through the warning (its crown slipping twice on the chain
    /// with a jolt, the second killing the swing into a shudder), the chain snaps and its upper length lashes, it drops on the
    /// simulation's own fall curve tipping as it goes (its low rim at the load's height), slams, hops and rocks to rest as the
    /// landed load, its candles and debris thrown round it, and the winch pays the chain out and rights it on the hoist. The
    /// others hang, barely stirring, and shiver when one crashes.</item>
    /// <item>The Thurible: the great silver censer on its rope from the oculus's yoke, its centre at the simulation's point along
    /// the path on the pendulum's arc. About that point: held cocked back by its tether to the south bridge, heaved further
    /// three times through the warning, let go with a whip, its bowl trailing and leading the rope, turning on its swivel, its
    /// lid lifting on its chains, the rope bowed behind it, the tether paid out slack and hauled in to catch it again.</item>
    /// <item>All of that secondary motion is a pure function of the hazards' own states and clocks (docs/arenas/SANCTUM.md "The
    /// hazards' polish"): nothing integrates from frame to frame, so every client and the killcam draw the same take.</item>
    /// <item>Holy fire: three iron fire baskets under each transept bridge over each curtain and the floor's channel, their coals
    /// banked at rest, kindling through the warning, white-hot while they pour, cooling after.</item>
    /// <item>The Toll: the great bell in the belfry swinging through the gates' warning (three strokes), easing after; the crypt
    /// gates' portcullises dropping in their frames by the shutters' closure, with their red lanterns.</item>
    /// <item>Ambient: the nave's banners stirring on their poles (the render clock).</item>
    /// </list>
    /// Fills a fixed buffer (<see cref="Capacity"/>): allocation free per frame.
    /// </summary>
    public sealed class NativeSanctumRig
    {
        public const int Capacity=2600,Chandeliers=4,ChandelierCandles=16;
        public readonly NativeFactoryKit.Writer Parts;
        /// <summary>This frame's chandelier candles: wick x, y, z and whether it burns (0..1: guttering after the fall), per candle.</summary>
        public readonly double[] Wicks=new double[Chandeliers*ChandelierCandles*4];
        /// <summary>Each chandelier's ring centre (x, y, z), its sway (rad) and its phase (0 rest, 1 warning, 2 falling, 3 down, 4 rising).</summary>
        public readonly double[] Ring=new double[Chandeliers*5];
        /// <summary>The censer's centre, its swing speed (m/s along z) and its glow (0..1), the bell's swing (rad) and the gates' closure.</summary>
        public double CenserX,CenserY,CenserZ,CenserSpeed,CenserGlow,Bell,Gate;public int Lamp;
        /// <summary>The fire baskets' coals: x, y, z, heat (0 banked .. 1 white), per basket (three a curtain).</summary>
        public readonly double[] Baskets=new double[24*4];public int BasketCount;
        /// <summary>For the stage art's poses (Core NativeSanctumArt): each chandelier's time since its fall (or its hoist's share),
        /// the censer's drawn angle (rad, with its tremble), its rope's length and whether its pull rope holds it; each shutter's
        /// closure and lantern colour and each burner's heat, in the hazards' order.</summary>
        public readonly double[] Age=new double[Chandeliers];public double CenserAngle,CenserRope;public bool CenserHeld;
        public const int MaxGates=4,MaxFires=4;public readonly double[] GateClosure=new double[MaxGates],FireHeat=new double[MaxFires];public readonly int[] GateLamp=new int[MaxGates];public int GateCount,FireCount;
        private static readonly int[] Sides={-1,1};
        public NativeSanctumRig(NativeFactoryKit.Looks looks){Parts=new NativeFactoryKit.Writer(Capacity,false,looks);}
        private void Add(double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=true,int color=-1,double window=1)=>Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow,color,window);
        private void Box(double x0,double x1,double y0,double y1,double z0,double z1,int kind,bool shadow=true,int color=-1,double window=1)=>Parts.Box(Math.Min(x0,x1),Math.Max(x0,x1),Math.Min(y0,y1),Math.Max(y0,y1),Math.Min(z0,z1),Math.Max(z0,z1),kind,shadow,color,window);
        private void Bar(double x0,double y0,double z0,double x1,double y1,double z1,double t,int kind,bool shadow=true,int color=-1)=>Parts.Bar(x0,y0,z0,x1,y1,z1,t,t,kind,shadow,color);
        /// <summary>The chain's anchor in the vault over each chandelier.</summary>
        public const double Anchor=20.7;

        public void Build(MapHazard[] hazards,int round,NativePhase phase,double fightAt,double now,double render)
        {
            Parts.Clear();BasketCount=0;CenserGlow=Bell=Gate=0;GateCount=FireCount=0;double clock=NativeFactoryHazards.Clock(phase,fightAt,now);bool gate=false;
            for(int i=0;i<(hazards?.Length??0);i++)
            {
                var h=hazards[i];if(h==null)continue;
                if(h.kind==NativeFactoryHazards.Crane)ChandelierSet(h,NativeFactoryHazards.State(h,round,clock),clock,render);
                else if(h.kind==NativeFactoryHazards.SawRail)Censer(h,NativeFactoryHazards.State(h,round,clock),round,clock,render);
                else if(h.kind==NativeWardZeroHazards.Burner)FireBaskets(h,NativeWardZeroHazards.At(h,phase,round,fightAt,now),now);
                else if(h.kind==NativeWardZeroHazards.Shutter){var s=NativeWardZeroHazards.At(h,phase,round,fightAt,now);PortCullis(h,s,now);if(!gate){gate=true;BellPose(s);}}
            }
            if(!gate)BellPose(NativeCyberCityHazards.Idle());
            Banners(render);
        }

        // ---------------- the chandeliers ----------------
        /// <summary>The crown ring the chain takes (the chandelier's frame, over the hoop's foot), the stub of chain a fallen
        /// chandelier keeps, how a wreck lies (turned <see cref="RestTilt"/> about a level axis of its own, <see cref="TiltAxis"/>),
        /// how far a falling one has tipped when its low rim meets the floor, and how far its crown has given at the warning's end.</summary>
        public const double ChainFoot=NativeSanctumLooks.CrownHeight+.7,StubLength=1.2,RestTilt=.11,FallTilt=.30,WarnTilt=.12;
        public const int ChainPoints=12,StubPoints=4;
        /// <summary>Each chandelier's drawn frame this frame: the hoop's foot (x, y, z) and its turn. Its chain as a line of points
        /// from the anchor down (to the crown ring while it holds; to its free end once it has snapped) and the stub of chain the
        /// fallen chandelier keeps, from its crown ring.</summary>
        public readonly double[] Body=new double[Chandeliers*3];public readonly FactoryQ[] BodyQ=new FactoryQ[Chandeliers];
        public readonly double[] ChainPts=new double[Chandeliers*ChainPoints*3],StubPts=new double[Chandeliers*StubPoints*3];public readonly int[] ChainCount=new int[Chandeliers],StubCount=new int[Chandeliers];
        /// <summary>Seconds since each chandelier's chain snapped and since its hoist began (-1: neither).</summary>
        public readonly double[] Snap=new double[Chandeliers],Hoist=new double[Chandeliers];
        /// <summary>How fast each chandelier is falling (m/s; 0 unless it falls).</summary>
        public readonly double[] FallSpeed=new double[Chandeliers];
        /// <summary>The candles a fall throws off, per candle: centre x, y, z, turn (x, y, z, w) and scale (0: it stands on the hoop).</summary>
        public readonly double[] Loose=new double[Chandeliers*ChandelierCandles*8];
        /// <summary>The fall's debris, per piece (<see cref="Shards"/> of iron, then <see cref="Links"/> of the chain, then
        /// <see cref="Chunks"/> of plaster): centre x, y, z, turn and scale (0: not there).</summary>
        public const int Shards=6,Links=8,Chunks=8,DebrisCount=Shards+Links+Chunks;
        public readonly double[] Scrap=new double[Chandeliers*DebrisCount*8];
        private static double Sm(double t)=>NativeFactoryHazards.Smooth(t);
        /// <summary>A slip's settling: 0 before it, a quick drop to 1 with one rebound.</summary>
        public static double Jolt(double t)=>t<=0?0:1+.35*Math.Exp(-7*t)*Math.Sin(18*t)-Math.Exp(-30*t);
        /// <summary>A struck thing's ringing: 0 before, a decaying sine after.</summary>
        public static double Ringing(double t,double decay,double rate)=>t<=0?0:Math.Exp(-decay*t)*Math.Sin(rate*t);
        /// <summary>The served chandelier's swing through the warning (rad): growing from a shiver to 0.16 rad, quickening; zero
        /// before and after. A pure function of the warning's progress and the clock. (The first box rig's; the drawn swing is
        /// <see cref="Warned"/>, which runs on the warning's own time.)</summary>
        public static double Sway(double warn,double clock)=>warn<=0||warn>=1?0:(.012+.15*warn*warn)*Math.Sin(clock*(3.2+3.4*warn))*Math.Min(1,(1-warn)*6+.35);
        /// <summary>The served chandelier through its warning of W seconds, on the warning's own time (so every cycle swings alike):
        /// its pendulum's swing along the nave and across it (rad about the anchor: growing to 0.16 rad by the second slip, which
        /// kills the swing into a shudder, so it hangs nearly plumb when the chain goes), how far its crown has slipped down the
        /// chain (two jolts of 7 cm at 45% and 80%, each with a rebound), the hoop's rocking behind the swing, how far its crown
        /// has given to one side after the second slip, and its twist on the chain.</summary>
        public static void Warned(int k,double warn,double W,out double sx,out double sz,out double slip,out double rock,out double sag,out double yaw)
        {
            double w=Math.Max(0,Math.Min(1,warn)),t=w*W,t1=.45*W,t2=.8*W,grow=.015+.145*Math.Pow(Math.Min(1,w/.78),1.6),late=t>t2?Math.Exp(-(t-t2)*3.2):1,ph=3.0*t+.3*t*t;
            sx=grow*late*Math.Sin(ph)+.03*Ringing(t-t1,3,11)+.035*Ringing(t-t2,3.5,13);
            sz=.35*grow*late*w*Math.Cos(ph)+.012*Ringing(t-t1,3,9)-.018*Ringing(t-t2,3.5,10);
            slip=.07*Jolt(t-t1)+.07*Jolt(t-t2);
            rock=.5*grow*late*Math.Sin(ph-1.0);
            sag=WarnTilt*Sm((t-t2)/(W-t2))+(t>t2?.014*Math.Sin(38*t):0)+.02*Ringing(t-t1,5,20);
            yaw=.05*Math.Sin(1.1*t)+.1*Ringing(t-t1,4,7)-.14*Ringing(t-t2,4,8);
        }
        /// <summary>The level axis a chandelier tips about as it falls and lies, and the turn it comes to rest at.</summary>
        public static void TiltAxis(int k,out double ax,out double az){double ta=(NativeSanctumLooks.Hash(k,9,2)-.5)*2.4;ax=Math.Cos(ta);az=Math.Sin(ta);}
        public static double RestYaw(int k)=>(NativeSanctumLooks.Hash(k,5,7)-.5)*.7;
        /// <summary>How high a tipped hoop's centre rides when its low rim is on the floor.</summary>
        public static double Lift(double tilt)=>Hoop*Math.Sin(tilt)-.05;
        /// <summary>The wreck's hop after the crash (m): one bounce and a smaller one, then it lies.</summary>
        public static double Hop(double since){double t=since-.05;if(t<0)return 0;double a=t/.34;if(a<1)return .2*4*a*(1-a);double b=(t-.34)/.2;return b<1?.06*4*b*(1-b):0;}
        /// <summary>The wreck's rocking tilt after the crash (rad): from the fall's tip it slams past its rest and settles on it.</summary>
        public static double Rocking(double since)=>RestTilt+(FallTilt-RestTilt)*Math.Exp(-9*since)*Math.Cos(17*since);
        private void ChandelierSet(MapHazard h,in FactoryHazardState st,double clock,double render)
        {
            NativeFactoryHazards.CraneTimes(h,out double T,out double W,out double F,out double D,out double G,out double H);double u=st.Age,busy=T+W+F+D+G+H;
            double sinceImpact=double.IsNaN(st.ImpactAt)?-1:clock-st.ImpactAt;
            for(int k=0;k<Math.Min(Chandeliers,h.zones?.Length??0);k++)
            {
                var z=h.zones[k];double x=z.center.x,zz=z.center.z,floor=z.center.y,rest=h.carry;bool served=st.Zone==k;int phase=0;double simY=rest,since=0;
                TiltAxis(k,out double ax,out double az);double restYaw=RestYaw(k);
                // at rest: barely stirring (faded out before its own warning and in again after its hoist, so nothing jumps), and a
                // shiver when another one crashes
                double amb=1;
                if(served)amb=st.Phase==NativeFactoryHazards.Travelling?1-Sm(u/Math.Max(.01,T)):st.Phase==NativeFactoryHazards.Parked?Sm((u-busy)/1.5):0;
                else if(st.From==k&&st.Cycle>0)amb=Sm((u+Math.Max(0,h.period-busy))/1.5);
                double shiver=!served&&sinceImpact>=0&&sinceImpact<3?.012*Math.Exp(-2*sinceImpact)*Math.Sin((8+k)*sinceImpact)*(1-.5*(k&1)):0;
                double sx=.004*Math.Sin(render*.7+k*1.9)*amb+shiver,sz=.0025*Math.Sin(render*.53+k*2.3)*amb+shiver*.5,slip=0,yaw=.03*Math.Sin(render*.31+k)*amb;
                double ox=x,oy=rest,oz=zz;var q=FactoryQ.Identity;bool pendulum=true;double snap=-1,hoist=-1,ripple=0,ripplePhase=0;
                if(served)switch(st.Phase)
                {
                    case NativeFactoryHazards.Warning:
                    {
                        phase=1;Warned(k,st.Warn,W,out sx,out sz,out slip,out double rock,out double sag,out yaw);simY=rest-slip;double t=st.Warn*W;
                        q=FactoryQ.Axis(0,0,1,rock)*FactoryQ.Axis(ax,0,az,sag);ripple=.1*((t>.45*W?Math.Exp(-4*(t-.45*W)):0)+(t>.8*W?1.3*Math.Exp(-4*(t-.8*W)):0));ripplePhase=20*t;break;
                    }
                    case NativeFactoryHazards.Falling:
                    {
                        // from where the warning left it: the swing's last offset eases out, the crown's tip grows to the fall's,
                        // the low rim rides the simulation's load height (so the rim meets the floor at the impact)
                        phase=2;pendulum=false;simY=st.LoadY;double f=Math.Max(0,Math.Min(1,(u-T-W)/Math.Max(.01,F)));snap=f*F;
                        Warned(k,1,W,out double sx0,out double sz0,out double slip0,out double rock0,out double sag0,out double yaw0);
                        Pendulum(x,zz,rest,slip0,sx0,sz0,out double wx,out double wy,out double wz);double ease=1-Sm(f/.6),tilt=sag0+(FallTilt-sag0)*Math.Pow(f,1.2);
                        ox=x+(wx-x)*ease;oz=zz+(wz-zz)*ease;oy=st.LoadY-(rest-wy)*(1-f)+Lift(tilt)*Sm(f/.3);
                        q=FactoryQ.Axis(1,0,0,sz0*ease)*FactoryQ.Axis(0,0,1,(sx0+rock0)*ease)*FactoryQ.Axis(ax,0,az,tilt)*FactoryQ.Axis(0,1,0,yaw0+(restYaw*.8-yaw0)*Sm(f));break;
                    }
                    case NativeFactoryHazards.Landed:case NativeFactoryHazards.Regrip:
                    {
                        phase=3;pendulum=false;simY=floor;since=Math.Max(0,sinceImpact);snap=F+since;double tilt=Rocking(since);
                        ox=x;oz=zz;oy=floor+Lift(tilt)+Hop(since);q=FactoryQ.Axis(ax,0,az,tilt)*FactoryQ.Axis(0,1,0,restYaw*(.8+.2*(1-Math.Exp(-6*since))));break;
                    }
                    case NativeFactoryHazards.Hoisting:
                    {
                        // the winch rights it as it takes it up; it comes to rest plumb at the carry height
                        phase=4;pendulum=false;simY=st.LoadY;hoist=Math.Max(0,u-(busy-H));since=(st.LoadY-floor)/Math.Max(.1,rest-floor);
                        double tilt=RestTilt*(1-Sm(hoist/.9))+.035*Ringing(hoist,1.4,5);ox=x;oz=zz;oy=st.LoadY+Lift(RestTilt)*(1-Sm(hoist/.6));
                        q=FactoryQ.Axis(ax,0,az,tilt)*FactoryQ.Axis(0,1,0,restYaw*(1-Sm(hoist/H)));ripple=.08*Math.Exp(-4*hoist);ripplePhase=16*hoist;break;
                    }
                }
                if(pendulum){Pendulum(x,zz,rest,slip,sx,sz,out ox,out oy,out oz);q=FactoryQ.Axis(1,0,0,sz)*FactoryQ.Axis(0,0,1,sx)*q*FactoryQ.Axis(0,1,0,yaw);}
                int o3=k*3;Body[o3]=ox;Body[o3+1]=oy;Body[o3+2]=oz;BodyQ[k]=q;Age[k]=since;Snap[k]=snap;Hoist[k]=hoist;FallSpeed[k]=phase==2?2*(rest-floor)*snap/(F*F):0;
                // the chain: to the crown ring while it holds; snapped, the upper length whips up and hangs, the stub trails the
                // fall and drapes over the wreck; through the regrip the winch pays it out to the wreck again
                q.Rotate(0,ChainFoot,0,out double rx,out double ry,out double rz);rx+=ox;ry+=oy;rz+=oz;
                if(phase==2||phase==3)
                {
                    Warned(k,1,W,out double sx0,out double sz0,out double slip0,out _,out _,out _);double upper=Anchor-(rest-slip0+ChainFoot)-StubLength;
                    Whip(k,x,zz,upper,snap);
                    if(st.Phase==NativeFactoryHazards.Regrip)
                    {
                        // down to the ring it will take the chandelier up by (the hoist's first frame)
                        double pay=Sm((u-(T+W+F+D))/(G*.75));var q0=FactoryQ.Axis(ax,0,az,RestTilt)*FactoryQ.Axis(0,1,0,restYaw);q0.Rotate(0,ChainFoot,0,out double hx,out double hy,out double hz);
                        hx+=x;hy+=floor+Lift(RestTilt);hz+=zz;int c=k*ChainPoints*3;
                        for(int i=0;i<ChainPoints;i++){double s=(double)i/(ChainPoints-1);ChainPts[c+i*3]+=(x+(hx-x)*s-ChainPts[c+i*3])*pay;ChainPts[c+i*3+1]+=(Anchor+(hy-Anchor)*s-ChainPts[c+i*3+1])*pay;ChainPts[c+i*3+2]+=(zz+(hz-zz)*s-ChainPts[c+i*3+2])*pay;}
                    }
                    StubLine(k,rx,ry,rz,phase==2?snap:-1,since,ax,az);
                }
                else{ChainLine(k,x,Anchor,zz,rx,ry,rz,ripple,ripplePhase);StubCount[k]=0;}
                // the candles the crash throws off and its debris
                int n=ChandelierCandles;
                for(int i=0;i<n;i++){int o=(k*n+i)*8;Loose[o+7]=0;if(phase==3||phase==4){Candle(k,i,out _,out _,out bool stands,out _,out _,out _);if(!stands){Loose[o+7]=LooseCandle(k,i,x,floor,zz,phase==3?since:D+G,hoist,out Loose[o],out Loose[o+1],out Loose[o+2],out var lq);Loose[o+3]=lq.X;Loose[o+4]=lq.Y;Loose[o+5]=lq.Z;Loose[o+6]=lq.W;}}}
                for(int i=0;i<DebrisCount;i++){int o=(k*DebrisCount+i)*8;Scrap[o+7]=0;if(phase<2)continue;double sn=phase==4?F+D+G+hoist:snap;Scrap[o+7]=Debris(k,i,x,floor,zz,rest,sn,F,hoist,out Scrap[o],out Scrap[o+1],out Scrap[o+2],out var dq);Scrap[o+3]=dq.X;Scrap[o+4]=dq.Y;Scrap[o+5]=dq.Z;Scrap[o+6]=dq.W;}
                Chandelier(k,x,zz,phase,floor,since,hoist);
                int o5=k*5;Ring[o5]=x;Ring[o5+1]=simY+.12;Ring[o5+2]=zz;Ring[o5+3]=sx;Ring[o5+4]=phase;
            }
        }
        // The hoop's foot on its chain: the pendulum from the anchor, swung sx along the nave and sz across it, slipped down the chain.
        private static void Pendulum(double x,double z,double rest,double slip,double sx,double sz,out double ox,out double oy,out double oz)
        {
            var qc=FactoryQ.Axis(1,0,0,sz)*FactoryQ.Axis(0,0,1,sx);qc.Rotate(0,-(Anchor-rest+slip),0,out ox,out oy,out oz);ox+=x;oy+=Anchor;oz+=z;
        }
        // A held chain: a line from the anchor to the crown ring, a ripple running up it after a slip.
        private void ChainLine(int k,double ax,double ay,double az,double bx,double by,double bz,double ripple,double phase)
        {
            int c=k*ChainPoints*3;ChainCount[k]=ChainPoints;
            for(int i=0;i<ChainPoints;i++)
            {
                double s=(double)i/(ChainPoints-1),e=Math.Sin(Math.PI*s)*ripple;
                ChainPts[c+i*3]=ax+(bx-ax)*s+e*Math.Sin(3*Math.PI*s+phase);ChainPts[c+i*3+1]=ay+(by-ay)*s;ChainPts[c+i*3+2]=az+(bz-az)*s+.6*e*Math.Cos(2*Math.PI*s+phase);
            }
        }
        /// <summary>How hard the snapped chain's upper length lashes (rad at its free end) `t` seconds after the snap: nothing at the
        /// snap, a whip within a fifth of a second, dying to a slow stir.</summary>
        public static double Lash(double t)=>t<=0?0:1.5*(Math.Exp(-2.2*t)-Math.Exp(-12*t))+.04*(1-Math.Exp(-3*t))*Math.Exp(-.2*t);
        // The snapped chain's upper length from the anchor: every link turned from the plumb by the lash, more toward the free end,
        // the wave running down it (the length is kept, so a hard lash curls the end up).
        private void Whip(int k,double x,double z,double length,double t)
        {
            int c=k*ChainPoints*3;ChainCount[k]=ChainPoints;double px=x,py=Anchor,pz=z,ds=length/(ChainPoints-1),lash=Lash(t);
            ChainPts[c]=px;ChainPts[c+1]=py;ChainPts[c+2]=pz;
            for(int i=1;i<ChainPoints;i++)
            {
                double s=(i-.5)/(ChainPoints-1),amp=lash*Math.Pow(s,1.5),tx=amp*Math.Sin(9*t-2.2*Math.PI*s+1+k),tz=.7*amp*Math.Sin(7.5*t-1.7*Math.PI*s+2.4+k);
                px+=Math.Sin(tx)*Math.Cos(tz)*ds;py-=Math.Cos(tx)*Math.Cos(tz)*ds;pz+=Math.Sin(tz)*ds;ChainPts[c+i*3]=px;ChainPts[c+i*3+1]=py;ChainPts[c+i*3+2]=pz;
            }
        }
        // The stub of chain on a fallen chandelier's crown ring: flailing above it as it falls, draped across the wreck after.
        private void StubLine(int k,double rx,double ry,double rz,double falling,double since,double ax,double az)
        {
            int c=k*StubPoints*3;StubCount[k]=StubPoints;double px=rx,py=ry,pz=rz,ds=StubLength/(StubPoints-1),lie=falling>=0?0:Sm(since/.3);
            StubPts[c]=px;StubPts[c+1]=py;StubPts[c+2]=pz;
            for(int i=1;i<StubPoints;i++)
            {
                double fl=falling>=0?.25+.5*falling:.08*Math.Exp(-3*since),wx=fl*Math.Sin(13*Math.Max(0,falling)+i*.9+k),wz=fl*Math.Cos(11*Math.Max(0,falling)+i*1.3);
                // lying: out along the level axis and down over the hoop
                double dx=wx*(1-lie)+az*lie,dy=1-lie*1.45,dz=wz*(1-lie)-ax*lie,l=Math.Sqrt(dx*dx+dy*dy+dz*dz);px+=dx/l*ds;py+=dy/l*ds;pz+=dz/l*ds;
                StubPts[c+i*3]=px;StubPts[c+i*3+1]=py;StubPts[c+i*3+2]=pz;
            }
        }
        /// <summary>Chandelier k's candle i (shared with the stage art's models, docs/arenas/STAGE-ART.md): its angle round the hoop,
        /// its height, whether it still stands once the chandelier lies fallen, and where it lies if not (the angle and radius about
        /// the zone's centre; it lies along the turn sa x 1.7) and whether it gutters there.</summary>
        public static void Candle(int k,int i,out double a,out double tall,out bool stands,out double sa,out double sr,out bool glows)
        {
            a=2*Math.PI*(i+.5)/ChandelierCandles;double h=NativeSanctumLooks.Hash(k,i,17);tall=.16+.1*NativeSanctumLooks.Hash(k,i,3);stands=h<.35;
            sa=a+(h-.5)*2.2;sr=Hoop+.4+1.3*h;glows=h<.55;
        }
        /// <summary>A candle the crash throws off chandelier k (one that does not stand on the wreck), `since` seconds after the
        /// impact: flung from its place on the tipped hoop in an arc, tumbling, to where it lies (kept inside the central aisle:
        /// its radius across the nave is halved), a short roll; its centre and turn (its length along its own y). Returns its
        /// scale: 1, shrinking away over the hoist's first .8 s (hoist &gt;= 0), when the winch takes the chandelier up relit.</summary>
        public static double LooseCandle(int k,int i,double x,double floor,double z,double since,double hoist,out double px,out double py,out double pz,out FactoryQ q)
        {
            Candle(k,i,out double a,out double tall,out _,out double sa,out double sr,out _);TiltAxis(k,out double ax,out double az);
            var q0=FactoryQ.Axis(ax,0,az,FallTilt)*FactoryQ.Axis(0,1,0,RestYaw(k)*.8);q0.Rotate(Math.Cos(a)*Hoop,.22+tall/2,Math.Sin(a)*Hoop,out double sx,out double sy,out double sz);sx+=x;sy+=floor+Lift(FallTilt);sz+=z;
            double ex=x+Math.Cos(sa)*sr,ez=z+Math.Sin(sa)*sr*.5,ey=floor+.035,mx=sx+(ex-sx)*.85,mz=sz+(ez-sz)*.85;
            double fly=.38+.3*NativeSanctumLooks.Hash(k,i,23),u=since/fly,turn=sa*1.7;int tumbles=NativeSanctumLooks.Hash(k,i,31)<.5?3:7;
            if(u<1){px=sx+(mx-sx)*u;pz=sz+(mz-sz)*u;py=sy+(ey-sy)*u+(.35+.5*NativeSanctumLooks.Hash(k,i,29))*4*u*(1-u);}
            else{double v=Math.Min(1,(since-fly)/.5),e=1-(1-v)*(1-v),b=(since-fly)/.16;px=mx+(ex-mx)*e;pz=mz+(ez-mz)*e;py=ey+(b<1?.05*4*b*(1-b):0);}
            q=FactoryQ.Axis(0,1,0,turn)*FactoryQ.Axis(1,0,0,Math.PI/2*(1+tumbles*Math.Max(0,1-u)));
            return hoist>=0?1-Sm(hoist/.8):1;
        }
        /// <summary>A piece of the fall's debris, `snap` seconds after the chain snapped (the fall lasts F): iron shards thrown out
        /// by the crash (pieces 0..), links of the broken chain raining down from the break after it, plaster shaken from the
        /// anchor in the vault falling last; each drops or arcs to its place by the zone (inside the central aisle), bounces once
        /// and lies, tumbling until then. Its centre and turn; returns its scale (0 before it exists; shrinking away over the
        /// hoist's first .8 s).</summary>
        public static double Debris(int k,int i,double x,double floor,double z,double carry,double snap,double F,double hoist,out double px,out double py,out double pz,out FactoryQ q)
        {
            double h1=NativeSanctumLooks.Hash(k,i,61),h2=NativeSanctumLooks.Hash(k,i,67),h3=NativeSanctumLooks.Hash(k,i,71);px=x;py=floor;pz=z;q=FactoryQ.Identity;
            double t,sx,sy,sz,ex,ez,ey,fly,apex,hop;bool drop;
            if(i<Shards){t=snap-F;double a=2*Math.PI*(i+h1)/Shards,r=1.3+1.5*h2;sx=x;sy=floor+.5;sz=z;ex=x+Math.Cos(a)*r;ez=z+Math.Sin(a)*r*.5;ey=floor+.03;fly=.42+.25*h3;apex=.5+.8*h2;hop=.1;drop=false;}
            else if(i<Shards+Links){t=snap-.15*h3;sx=x+(h1-.5)*.2;sy=carry+ChainFoot+StubLength-.14;sz=z;ex=x+(h1-.5)*5.5;ez=z+(h2-.5)*2.6;ey=floor+.02;fly=Math.Sqrt(2*(sy-ey)/9.8);apex=0;hop=.12;drop=true;}
            else{t=snap-.25*h3;sx=x+(h1-.5)*.5;sy=Anchor-.15;sz=z+(h2-.5)*.5;ex=x+(h2-.5)*3.6;ez=z+(h1-.5)*2.4;ey=floor+.04;fly=Math.Sqrt(2*(sy-ey)/9.8);apex=0;hop=.06;drop=true;}
            if(t<0)return 0;double u=t/fly,scale=hoist>=0?1-Sm(hoist/.8):1;const double bounce=.2;
            if(u<1){px=sx+(ex-sx)*u*.9;pz=sz+(ez-sz)*u*.9;py=drop?sy+(ey-sy)*u*u:sy+(ey-sy)*u+apex*4*u*(1-u);}
            else{double b=Math.Min(1,(t-fly)/bounce);px=sx+(ex-sx)*(.9+.1*b);pz=sz+(ez-sz)*(.9+.1*b);py=ey+hop*4*b*(1-b);if(i>=Shards+Links)scale*=.55+.45*(1-b);}
            q=FactoryQ.Axis(0,1,0,2*Math.PI*h1)*FactoryQ.Axis(1,0,0,Math.PI/2+(4+8*h2)*Math.Max(0,1-t/(fly+bounce)));
            return scale;
        }
        /// <summary>When a link of the chain or a lump of plaster (debris piece i, from <see cref="Shards"/> on) lands: seconds after the snap.</summary>
        public static double DebrisLands(int k,int i,double floor,double carry)
        {
            double h3=NativeSanctumLooks.Hash(k,i,71);bool link=i<Shards+Links;double sy=link?carry+ChainFoot+StubLength-.14:Anchor-.15,ey=floor+(link?.02:.04);return (link?.15:.25)*h3+Math.Sqrt(2*(sy-ey)/9.8);
        }
        /// <summary>The hoop's radius, and how a fallen chandelier lies: turned .11 rad about a level axis at its own angle.</summary>
        public const double Hoop=1.4;
        public static FactoryQ DownTilt(int k){TiltAxis(k,out double ax,out double az);return FactoryQ.Axis(ax,0,az,RestTilt);}
        // The box rig's chandelier (drawn only without the stage bank), in the frame's pose: every part is placed in the hoop's own
        // frame (origin at its foot, +y up the hub) and carried by Body and BodyQ, as the stage art's models are.
        private void Chandelier(int k,double x,double z,int phase,double floor,double age,double hoist)
        {
            int I=NativeSanctumLooks.Iron,G=NativeSanctumLooks.Gold,W=NativeSanctumLooks.Wax,C=NativeSanctumLooks.Chain;double R=Hoop;bool down=phase==3,falling=phase==2;
            var q=BodyQ[k];double bx=Body[k*3],by=Body[k*3+1],bz=Body[k*3+2];
            void P(double lx,double ly,double lz,out double wx,out double wy,out double wz){q.Rotate(lx,ly,lz,out double ox,out double oy,out double oz);wx=bx+ox;wy=by+oy;wz=bz+oz;}
            void Part(double lx,double ly,double lz,double hx,double hy,double hz,FactoryQ r,int kind,bool shadow=true,int color=-1,double window=1){P(lx,ly,lz,out double wx,out double wy,out double wz);Add(wx,wy,wz,hx,hy,hz,q*r,kind,shadow,color,window);}
            void Seg(double ax,double ay,double az,double cx,double cy,double cz,double t,int kind,bool shadow=true){P(ax,ay,az,out double x0,out double y0,out double z0);P(cx,cy,cz,out double x1,out double y1,out double z1);Bar(x0,y0,z0,x1,y1,z1,t,kind,shadow);}
            bool cast=by<6;
            // the hoop (two rings), the drip pans and candles round it, four arms to the hub, the hub and its finial, the crown ring
            double crown=down?.6:NativeSanctumLooks.CrownHeight;
            for(int i=0;i<24;i++){double a0=2*Math.PI*i/24,a1=2*Math.PI*(i+1)/24;Seg(Math.Cos(a0)*R,.12,Math.Sin(a0)*R,Math.Cos(a1)*R,.12,Math.Sin(a1)*R,.11,I,cast);Seg(Math.Cos(a0)*(R-.16),.04,Math.Sin(a0)*(R-.16),Math.Cos(a1)*(R-.16),.04,Math.Sin(a1)*(R-.16),.05,G,false);}
            int n=ChandelierCandles;
            for(int i=0;i<n;i++)
            {
                Candle(k,i,out double a,out double tall,out bool stands,out _,out _,out bool glows);double cx=Math.Cos(a)*R,cz=Math.Sin(a)*R;Part(cx,.2,cz,.09,.02,.09,FactoryQ.Axis(0,1,0,a),I,false);
                // after the fall a few candles stand, the rest are thrown off round the ring; the winch takes it up relit, one by one
                int o=(k*n+i)*4,l=(k*n+i)*8;
                if(!down||stands)
                {
                    Part(cx,.22+tall/2,cz,.035,tall/2,.035,FactoryQ.Identity,W,false);P(cx,.24+tall,cz,out Wicks[o],out Wicks[o+1],out Wicks[o+2]);
                    Wicks[o+3]=down?Math.Max(0,1-age/3.0):falling?.55:phase==4?Math.Max(0,Math.Min(1,(hoist-.35-i*.09)*4)):1;
                }
                else
                {
                    var lq=new FactoryQ{X=Loose[l+3],Y=Loose[l+4],Z=Loose[l+5],W=Loose[l+6]};Add(Loose[l],Loose[l+1],Loose[l+2],.035,tall/2,.035,lq,W,false);
                    lq.Rotate(0,tall/2+.02,0,out double tx,out double ty,out double tz);Wicks[o]=Loose[l]+tx;Wicks[o+1]=Math.Max(floor+.06,Loose[l+1]+ty);Wicks[o+2]=Loose[l+2]+tz;Wicks[o+3]=glows?Math.Max(0,.8-age/4.0):Math.Max(0,.6-age*1.5);
                }
                if(phase==4&&!stands&&Loose[l+7]>.02){var lq=new FactoryQ{X=Loose[l+3],Y=Loose[l+4],Z=Loose[l+5],W=Loose[l+6]};double s=Loose[l+7];Add(Loose[l],Loose[l+1],Loose[l+2],.035*s,tall/2*s,.035*s,lq,W,false);}
            }
            for(int i=0;i<4;i++)
            {
                double a=Math.PI/2*i+Math.PI/4,ex=Math.Cos(a)*(R-.05),ez=Math.Sin(a)*(R-.05),mx=Math.Cos(a)*R*.55,mz=Math.Sin(a)*R*.55;
                if(!down){Seg(ex,.12,ez,mx,crown*.55,mz,.07,I);Seg(mx,crown*.55,mz,0,crown,0,.06,I);}
                else{Seg(ex,.12,ez,mx,.3,mz,.07,I);Seg(mx,.3,mz,Math.Cos(a+.4)*.3,.55,Math.Sin(a+.4)*.3,.06,I);}
            }
            Part(0,crown,0,.18,.22,.18,FactoryQ.Axis(0,1,0,Math.PI/4),I);Part(0,crown+.3,0,.07,.12,.07,FactoryQ.Identity,G);
            for(int i=0;i<12;i++){double a0=2*Math.PI*i/12,a1=2*Math.PI*(i+1)/12;Seg(Math.Cos(a0)*.55,crown-.05,Math.Sin(a0)*.55,Math.Cos(a1)*.55,crown-.05,Math.Sin(a1)*.55,.05,G,false);}
            // the chain and the stub, link by link; the debris
            int c=k*ChainPoints*3;for(int i=0;i+1<ChainCount[k];i++)Bar(ChainPts[c+i*3],ChainPts[c+i*3+1],ChainPts[c+i*3+2],ChainPts[c+i*3+3],ChainPts[c+i*3+4],ChainPts[c+i*3+5],.06,C,false);
            int sc=k*StubPoints*3;for(int i=0;i+1<StubCount[k];i++)Bar(StubPts[sc+i*3],StubPts[sc+i*3+1],StubPts[sc+i*3+2],StubPts[sc+i*3+3],StubPts[sc+i*3+4],StubPts[sc+i*3+5],.06,C,false);
            for(int i=0;i<DebrisCount;i++)
            {
                int o=(k*DebrisCount+i)*8;double s=Scrap[o+7];if(s<=.02)continue;var dq=new FactoryQ{X=Scrap[o+3],Y=Scrap[o+4],Z=Scrap[o+5],W=Scrap[o+6]};
                if(i<Shards)Add(Scrap[o],Scrap[o+1],Scrap[o+2],.03*s,.18*s,.03*s,dq,I,false);else if(i<Shards+Links)Add(Scrap[o],Scrap[o+1],Scrap[o+2],.03*s,.045*s,.012*s,dq,C,false);else Add(Scrap[o],Scrap[o+1],Scrap[o+2],.07*s,.05*s,.06*s,dq,NativeSanctumLooks.Trim,false);
            }
            Box(x-.2,x+.2,Anchor,Anchor+.3,z-.2,z+.2,I);
        }

        // ---------------- the censer ----------------
        /// <summary>The censer's centre on its pendulum: the simulation's z along the path, the arc's height for its rope.</summary>
        public static void CenserPoint(MapHazard h,in FactoryHazardState st,out double x,out double y,out double z,out double angle)
        {
            double L=NativeSanctumLooks.CenserPivotY-h.a.y;z=st.Center.z;x=st.Center.x;double s=Math.Max(-.95,Math.Min(.95,z/L));angle=Math.Asin(s);y=NativeSanctumLooks.CenserPivotY-L*Math.Cos(angle);
        }
        /// <summary>The censer's own frame: its crown ring (where its six chains meet and the rope takes it) and its foot ring (where
        /// the tether takes it), along its axis from its centre. How far the tether holds it cocked back past its rope (rad), how
        /// far each heave of the warning cocks it further, how far the bowl trails its rope in a swing; the tether's cleat on the
        /// south bridge.</summary>
        public const double CenserRing=2.4,CenserFoot=-.92,Cock=.32,HeaveStep=.075,SwingLag=.2,CleatY=5.05,CleatZ=-7.02;
        public const int RopePoints=13,TetherPoints=11,Heaves=3;
        /// <summary>The censer's turn this frame (its axis up its chains), its lid's lift along that axis and the lid's own turn,
        /// the rope from its crown ring to the yoke and the tether from its foot to the cleat as lines of points, and how far its
        /// six chains bow (world x and z, at their middles).</summary>
        public FactoryQ CenserQ=FactoryQ.Identity,LidQ=FactoryQ.Identity;public double LidHop,ChainBowX,ChainBowZ;
        public readonly double[] RopePts=new double[RopePoints*3],TetherPts=new double[TetherPoints*3];
        /// <summary>When heave i of the warning comes (seconds into a warning of w seconds).</summary>
        public static double HeaveAt(int i,double w)=>w*(.12+.3*i);
        // A heave's take-up: 0 before it, a snatch to 1 with a ring.
        private static double Snatch(double t)=>t<=0?0:1-Math.Exp(-14*t)+.5*Math.Exp(-8*t)*Math.Sin(22*t);
        /// <summary>The censer's secondary motion at one moment, from the rail's state alone (its phase and the time into its cycle):
        /// <list type="bullet">
        /// <item>lean: how far its axis leans north past its rope (rad). The tether holds it cocked back (its foot drawn toward the
        /// cleat); each of the warning's three heaves cocks it further with a snatch; let go, it whips through and past its rope
        /// once, then the bowl trails the rope while it gathers speed and swings ahead of it while it slows; caught again at the
        /// south arch it cocks back with a rebound.</item>
        /// <item>yaw: its turn on its swivel (it winds through the swing and unwinds on the way back; a slow stir at rest).</item>
        /// <item>side: a small lean across the path.</item>
        /// <item>lid: the lid's lift on its chains (m): a rattle at each heave, a jump at the release, at the turn and at the catch.</item>
        /// <item>slack: the tether's slack (the share of its span it has paid out; 0 taut).</item>
        /// <item>twang: the taut tether's and the rope's tremble (0..1). whip: seconds since the release (-1 outside the swing).</item>
        /// </list></summary>
        public static void CenserMotion(MapHazard h,in FactoryHazardState st,double clock,out double lean,out double yaw,out double side,out double lid,out double slack,out double twang,out double whip)
        {
            double w=h.warn,T=Math.Max(.01,h.travel),d=Math.Max(.01,h.dwell),u=st.Age;yaw=.4*Math.Sin(.45*clock);side=.02*Math.Sin(1.3*clock);lid=0;slack=0;twang=0;whip=-1;
            switch(st.Phase)
            {
                case NativeFactoryHazards.Warning:
                {
                    double steps=0,rattle=0;for(int i=0;i<Heaves;i++){double t=u-HeaveAt(i,w);steps+=Snatch(t);if(t>0){rattle+=Math.Exp(-9*t)*Math.Abs(Math.Sin(26*t));twang+=Math.Exp(-5*t);}}
                    lean=Cock+HeaveStep*steps;yaw-=.1*steps;lid=.035*rattle;twang=Math.Min(1,twang);break;
                }
                case NativeFactoryHazards.Running:
                {
                    double t=u-w,s=t/T,start=Cock+HeaveStep*Heaves;whip=t;
                    lean=start*Math.Exp(-4.5*t)*Math.Cos(10.5*t)+SwingLag*Math.Sin(2*Math.PI*s)*(1-.25*s);yaw+=-.1*Heaves+(2.6+.1*Heaves)*Sm(s);side+=.05*Ringing(t,3,8);
                    lid=.12*Math.Exp(-6*t)*Math.Abs(Math.Sin(20*t))+.03*Math.Sin(2*Math.PI*s)*Math.Sin(2*Math.PI*s);slack=.03*(1-Math.Exp(-3*t));break;
                }
                case NativeFactoryHazards.Holding:
                {
                    double t=u-w-T;whip=T+t;lean=-.06*Math.Sin(Math.PI*t/d);yaw+=2.6;lid=.07*Math.Sin(Math.PI*t/d);slack=.03;break;
                }
                case NativeFactoryHazards.Returning:
                {
                    // the tether is hauled in as it comes back, taut at the catch
                    double t=u-w-T-d,s=t/T;whip=T+d+t;lean=-SwingLag*Math.Sin(2*Math.PI*s)*(1-.25*s);yaw+=2.6*(1-Sm(s));lid=.03*Math.Sin(2*Math.PI*s)*Math.Sin(2*Math.PI*s);slack=.03*(1-Sm(s*1.15));break;
                }
                default:
                {
                    double t=st.Cycle>=0&&u>=w+2*T+d?u-(w+2*T+d):99;lean=Cock*(1-Math.Exp(-5*t)*Math.Cos(10*t))+.012*Math.Sin(.9*clock);lid=.08*Math.Exp(-6*t)*Math.Abs(Math.Sin(18*t));twang=Math.Exp(-4*t);break;
                }
            }
        }
        private void Censer(MapHazard h,in FactoryHazardState st,int round,double clock,double render)
        {
            CenserPoint(h,st,out double x,out double y,out double z,out double ang);int S=NativeSanctumLooks.Silver,G=NativeSanctumLooks.Gold,E=NativeSanctumLooks.Ember,R=NativeSanctumLooks.Rope,I=NativeSanctumLooks.Iron,C=NativeSanctumLooks.Chain;
            bool parked=st.Phase==NativeFactoryHazards.Parked,warn=st.Phase==NativeFactoryHazards.Warning;
            CenserMotion(h,st,clock,out double lean,out double yaw,out double side,out double lid,out double slack,out double twang,out double whip);
            double glow=Math.Min(1.3,(parked?.35:warn?.35+.65*st.Warn:1)+lid*3);CenserGlow=glow;CenserX=x;CenserY=y;CenserZ=z;
            double py=NativeSanctumLooks.CenserPivotY,L=py-h.a.y;CenserAngle=ang;CenserRope=L;CenserHeld=slack<=0;
            var next=NativeFactoryHazards.State(h,round,clock+.02);CenserSpeed=(next.Center.z-st.Center.z)/.02;
            // its axis: up its rope (leaning north by -ang from the plumb), leaning further by the motion's lean; its swivel's turn
            var body=FactoryQ.Axis(0,0,1,side)*FactoryQ.Axis(1,0,0,lean-ang);var q=body*FactoryQ.Axis(0,1,0,yaw);CenserQ=q;LidHop=lid;
            LidQ=q*FactoryQ.Axis(Math.Cos(7*yaw),0,Math.Sin(7*yaw),2.2*lid);ChainBowX=-.25*side;ChainBowZ=-.012*CenserSpeed;
            void P(double lx,double ly,double lz,out double wx,out double wy,out double wz){q.Rotate(lx,ly,lz,out double ox,out double oy,out double oz);wx=x+ox;wy=y+oy;wz=z+oz;}
            void Part(double ly,double hx,double hy,double hz,FactoryQ r,int kind,bool shadow=true,double window=1){P(0,ly,0,out double wx,out double wy,out double wz);Add(wx,wy,wz,hx,hy,hz,q*r,kind,shadow,-1,window);}
            // the bowl in eight-sided tiers (foot, belly, the gold band), the coals glowing through the gaps of the pierced lid's
            // tiers (the lid lifted on its chains), the finial, the six chains to the crown ring
            void Tier(double ly,double rad,double hy,int kind,double gap){for(int i=0;i<8;i++){double a=Math.PI/4*i+Math.PI/8;var r=FactoryQ.Axis(0,1,0,a);r.Rotate(0,0,rad,out double ox,out _,out double oz);P(ox,ly,oz,out double wx,out double wy,out double wz);Add(wx,wy,wz,rad*.4142*(1-gap)+.01,hy,.05,q*r,kind,true);}}
            Part(-.86,.22,.05,.22,FactoryQ.Axis(0,1,0,Math.PI/8),G);Tier(-.72,.32,.1,S,0);Tier(-.42,.5,.2,S,0);Tier(-.12,.56,.1,S,0);Tier(.0,.58,.04,G,0);
            Part(.02,.5,.06,.5,FactoryQ.Axis(0,1,0,Math.PI/8),E,false,1);
            Tier(.2+lid,.5,.1,S,.3);Tier(.4+lid,.38,.09,S,.3);Tier(.57+lid,.25,.08,S,.2);Tier(.7+lid,.13,.06,G,0);
            Part(.85+lid,.06,.12,.06,FactoryQ.Axis(0,1,0,Math.PI/8),G);Part(1.02+lid,.05,.05,.05,FactoryQ.Axis(0,1,0,Math.PI/4),G);
            P(0,CenserRing,0,out double rx,out double ry,out double rz);
            for(int i=0;i<6;i++){double a=Math.PI/3*i;P(Math.Cos(a)*.6,.16,Math.Sin(a)*.6,out double bx,out double by,out double bz);double mx=(bx+rx)/2+ChainBowX,my=(by+ry)/2,mz=(bz+rz)/2+ChainBowZ;Bar(bx,by,bz,mx,my,mz,.035,C,false);Bar(mx,my,mz,rx,ry,rz,.035,C,false);}
            Part(CenserRing,.2,.05,.2,FactoryQ.Identity,G);
            // the rope to the yoke: bowed behind the swing, a ripple down it after the release, trembling while the tether strains
            for(int i=0;i<RopePoints;i++)
            {
                double s=(double)i/(RopePoints-1),e=Math.Sin(Math.PI*Math.Pow(s,.8)),bow=-.045*CenserSpeed*e+(whip>=0?.1*(Math.Exp(-2.5*whip)-Math.Exp(-14*whip))*Math.Sin(3*Math.PI*s-14*whip)*e:0)+.008*twang*Math.Sin(41*clock)*Math.Sin(2*Math.PI*s);
                // the bow lies in the swing's plane, square to the rope (which stands near the plumb): along z
                RopePts[i*3]=rx+(0-rx)*s;RopePts[i*3+1]=ry+(py-.1-ry)*s;RopePts[i*3+2]=rz+(0-rz)*s+bow;
            }
            for(int i=0;i+1<RopePoints;i++)Bar(RopePts[i*3],RopePts[i*3+1],RopePts[i*3+2],RopePts[i*3+3],RopePts[i*3+4],RopePts[i*3+5],.07,R,false);
            // the tether from its foot to the cleat on the south bridge: taut while it holds the censer back (twanging at each
            // heave and at the catch), paid out slack behind it through the swing (a wave down it at the release), hauled in on the way back
            P(0,CenserFoot,0,out double fx,out double fy,out double fz);double span=Math.Sqrt(fx*fx+(CleatY-fy)*(CleatY-fy)+(CleatZ-fz)*(CleatZ-fz)),sag=span*Math.Sqrt(3*Math.Max(0,slack)/8);
            for(int i=0;i<TetherPoints;i++)
            {
                double s=(double)i/(TetherPoints-1),e=4*s*(1-s),wave=whip>=0?.35*(Math.Exp(-3*whip)-Math.Exp(-14*whip))*Math.Sin(2*Math.PI*(2*s+2.5*whip))*e:0,hum=.03*twang*Math.Sin(40*clock)*Math.Sin(Math.PI*s);
                TetherPts[i*3]=fx+(0-fx)*s+.4*wave*Math.Cos(9*Math.Max(0,whip));TetherPts[i*3+1]=fy+(CleatY-fy)*s-sag*e+wave+hum;TetherPts[i*3+2]=fz+(CleatZ-fz)*s;
            }
            for(int i=0;i+1<TetherPoints;i++)Bar(TetherPts[i*3],TetherPts[i*3+1],TetherPts[i*3+2],TetherPts[i*3+3],TetherPts[i*3+4],TetherPts[i*3+5],.04,R,false);
            Box(-.12,.12,4.95,5.15,-7.1,-6.95,I);
        }

        // ---------------- holy fire ----------------
        /// <summary>A basket's heat (0 banked .. 1 white-hot) by the burner's phase.</summary>
        public static double Heat(in NativeHazardState s)=>s.Phase==NativeHazardPhase.Warn?.25+.55*s.Level:s.Phase==NativeHazardPhase.Active?1:s.Phase==NativeHazardPhase.Clear?.2+.6*s.Level:.12;
        private void FireBaskets(MapHazard h,in NativeHazardState s,double time)
        {
            int I=NativeSanctumLooks.Iron,C=NativeSanctumLooks.Coal,Ch=NativeSanctumLooks.Chain;double heat=Heat(s);double soffit=NativeSanctumLooks.Gallery-.4;if(FireCount<MaxFires)FireHeat[FireCount++]=heat;
            foreach(var z in h.zones??Array.Empty<MapVolume>())
            {
                double x=z.center.x,z0=z.center.z-z.size.z/2,z1=z.center.z+z.size.z/2;
                // the floor's channel under the curtain: a grate kindling with the baskets
                Box(x-.16,x+.16,0,.022,z0+.15,z1-.15,C,false,-1,1+heat*1.3);
                for(int i=0;i<3;i++)
                {
                    double bz=z0+.75+(z1-z0-1.5)*i/2,by=soffit-.95;double fl=.85+.15*Math.Sin(time*(7+i)+x);
                    for(int k=0;k<8;k++){double a=Math.PI/4*k+Math.PI/8;var r=FactoryQ.Axis(0,1,0,a);r.Rotate(0,0,.3,out double ox,out _,out double oz);Add(x+ox,by+.12,bz+oz,.13,.14,.03,r*FactoryQ.Axis(1,0,0,-.35),I,true);r.Rotate(0,0,.38,out ox,out _,out oz);Add(x+ox,by+.27,bz+oz,.17,.025,.04,r,I,false);}
                    Box(x-.14,x+.14,by-.06,by,bz-.14,bz+.14,I);Box(x-.3,x+.3,by+.1,by+.24,bz-.3,bz+.3,C,false,-1,1+heat*fl*1.5);
                    for(int k=0;k<3;k++){double a=2*Math.PI*k/3;Bar(x+Math.Cos(a)*.36,by+.28,bz+Math.Sin(a)*.36,x,soffit-.02,bz,.03,Ch,false);}
                    if(BasketCount<24){int o=BasketCount*4;Baskets[o]=x;Baskets[o+1]=by+.2;Baskets[o+2]=bz;Baskets[o+3]=heat*fl;BasketCount++;}
                }
            }
        }

        // ---------------- the bell and the gates ----------------
        /// <summary>The bell's swing (rad) by the gates' state: three strokes through the warning, easing through the drop.</summary>
        public static double BellSwing(in NativeHazardState s)
        {
            if(s.Phase==NativeHazardPhase.Warn)return .55*Math.Sin(Math.PI*s.Since/.5)*Math.Min(1,s.Since*1.6+.3);
            if(s.Phase==NativeHazardPhase.Danger)return .35*Math.Sin(Math.PI*(s.Since+3)/.5)*Math.Max(0,1-s.Since*.8);
            return 0;
        }
        private void BellPose(in NativeHazardState s)
        {
            double sw=BellSwing(s);Bell=sw;var q=FactoryQ.Axis(1,0,0,sw);double px=NativeSanctumLooks.BellX,py=NativeSanctumLooks.BellY,pz=NativeSanctumLooks.BellZ;int B=NativeSanctumLooks.Bronze,O=NativeSanctumLooks.OakDark,I=NativeSanctumLooks.Iron;
            void Part(double ly,double hx,double hy,double hz,FactoryQ r,int kind){q.Rotate(0,ly,0,out double ox,out double oy,out double oz);Add(px+ox,py+oy,pz+oz,hx,hy,hz,q*r,kind);}
            Part(.15,1.0,.16,.2,FactoryQ.Identity,O);
            for(int k=0;k<7;k++){double t=k/6.0,r=.55+(NativeSanctumLooks.BellMouth-.55)*t*t,ly=-.1-NativeSanctumLooks.BellHeight*t;for(int i=0;i<8;i++){double a=Math.PI/4*i;var rot=FactoryQ.Axis(0,1,0,a);rot.Rotate(0,0,r,out double ox,out _,out double oz);q.Rotate(ox,ly,oz,out double wx,out double wy,out double wz);Add(px+wx,py+wy,pz+wz,r*.42,.2,.06,q*rot,B);}}
            Part(-NativeSanctumLooks.BellHeight-.1,NativeSanctumLooks.BellMouth+.05,.05,NativeSanctumLooks.BellMouth+.05,FactoryQ.Axis(0,1,0,Math.PI/8),B);
            // the clapper lags the swing
            var cq=FactoryQ.Axis(1,0,0,sw*.55);cq.Rotate(0,-1.9,0,out double cx,out double cy,out double cz);Add(px+cx,py+cy,pz+cz,.12,.14,.12,cq,I);
            foreach(int s2 in Sides)Box(px+s2*1.0-.1,px+s2*1.0+.1,py-.1,py+.4,pz-.12,pz+.12,I);
        }
        private void PortCullis(MapHazard h,in NativeHazardState s,double time)
        {
            double c=NativeWardZeroHazards.Closure(s);Gate=Math.Max(Gate,c);int I=NativeSanctumLooks.Iron,G=NativeSanctumLooks.Glow;
            double x=h.center.x,z0=h.center.z-h.size.z/2,z1=h.center.z+h.size.z/2,bottom=h.center.y-h.size.y/2,top=h.center.y+h.size.y/2,tall=top-bottom;
            double y0=bottom+(1-c)*tall,y1=y0+tall;// the grille's foot: up in its frame over the stair's end when open
            for(int i=0;i<=8;i++){double z=z0+.1+(z1-z0-.2)*i/8;Box(x-.04,x+.04,y0,y1,z-.035,z+.035,I);Box(x-.03,x+.03,y0-.12,y0,z-.02,z+.02,I);}
            for(int k=0;k<4;k++){double y=y0+.3+(tall-.5)*k/3;Box(x-.05,x+.05,y-.04,y+.04,z0+.05,z1-.05,I);}
            // the frame's posts up into the stairwell, the winch beam, the lanterns: dark at rest, red strobing through the toll, red shut
            for(int e=0;e<2;e++){double z=e==0?z0-.12:z1+.12;Box(x-.09,x+.09,bottom,top+tall+.2,z-.06,z+.06,I);}Box(x-.12,x+.12,top+tall+.2,top+tall+.45,z0-.2,z1+.2,I);
            int lamp=s.Phase==NativeHazardPhase.Warn?(NativeFactoryRig.Strobe(time,2.5)?0xFF2A18:0x2A0806):s.Phase==NativeHazardPhase.Danger||s.Phase==NativeHazardPhase.Active?0xFF2A18:s.Phase==NativeHazardPhase.Clear?0xC04018:0x3A1208;
            foreach(int side in Sides)for(int e=0;e<2;e++){double z=e==0?z0-.12:z1+.12;Box(x+side*.1,x+side*.3,top-.6,top-.3,z-.1,z+.1,G,false,lamp);}
            Lamp=lamp;if(GateCount<MaxGates){GateClosure[GateCount]=c;GateLamp[GateCount]=lamp;GateCount++;}
        }

        // ---------------- ambient ----------------
        /// <summary>A banner's stir (rad about z) by the render clock, the arcade pier it hangs from (x magnitude) and the side.</summary>
        public static double BannerSway(double t,double px,int sz)=>.035*Math.Sin(t*.6+px*.7+sz)+.015*Math.Sin(t*1.7+px);
        /// <summary>Where the banners hang: the arcade piers (x magnitude), their pivots' height and their reach from the pier.</summary>
        public static readonly double[] BannerPiers={12.0,18.0,24.0};public const double BannerTop=15.4,BannerZ=5.3,BannerReach=.85;
        /// <summary>The nave's banners: crimson on the west half, blue on the east, one on each arcade pier's nave face, stirring.</summary>
        private void Banners(double t)
        {
            foreach(int s in Sides)foreach(double px in BannerPiers)foreach(int sz in Sides)
            {
                double x=s*px,z0=sz*BannerZ,face=-sz;int cloth=s<0?NativeSanctumLooks.ClothRed:NativeSanctumLooks.ClothBlue,G=NativeSanctumLooks.ClothGold,I=NativeSanctumLooks.Iron;
                double ang=BannerSway(t,px,sz);var q=FactoryQ.Axis(0,0,1,ang);double top=BannerTop,len=5.6,zc=z0+face*BannerReach;
                Box(x-.05,x+.05,top+.05,top+.15,z0,z0+face*1.7,I);
                void Part(double ly,double hy,double dz,double hz,int kind){q.Rotate(0,ly,0,out double ox,out double oy,out double oz);Add(x+ox,top+oy,zc+dz+oz,.012,hy,hz,q,kind,true);}
                Part(-len/2,len/2,0,.7,cloth);Part(-len/2,len/2-.1,0,.6,cloth);Part(-.12,.06,0,.74,G);Part(-len+.08,.08,0,.72,G);
                for(int i=0;i<3;i++){double a=-len*(.3+.2*i);Part(a,.16,0,.16,G);}
                q.Rotate(0,-len-.2,0,out double tx,out double ty,out double tz);Add(x+tx,top+ty,zc+tz,.02,.12,.12,q*FactoryQ.Axis(1,0,0,Math.PI/4),G,false);
            }
        }
    }
}
