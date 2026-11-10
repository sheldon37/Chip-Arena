using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Top Dog Emporium's moving rigs for one frame (docs/arenas/EMPORIUM.md "Look"), posed only from the replicated state (the round
    /// index, the match phase, FightAt and the drawn arena's field clock) through the very functions the simulation decides hits with
    /// (NativeCyberCityHazards.At for the wiener lanes, NativeFactoryHazards.State for the bun presses and the geysers,
    /// NativeWardZeroHazards.At for the griddles), so host, guests and the killcam draw the same rig at the same moment and nothing new
    /// is sent:
    /// <list type="bullet">
    /// <item>The Wiener Run: each lane's giant frankfurter exactly where the simulation's sweep is (its centre one radius behind the
    /// head), rolling without slipping (grill stripes turn with it), bobbing over the rollers, its googly eyes looking where it is
    /// going; the ovens' pet-door flaps it bursts through (they rattle through the danger and swing shut after it); the lane-edge
    /// lamps: dark at rest, an amber chase running the way it will come through the warning, a fast red chase through the danger,
    /// red ahead of it while it runs, fading after.</item>
    /// <item>The Bun Presses: a giant sesame top bun on a chrome ram, at the simulation's stroke (its underside is the ram's), winding
    /// up with a tremble and a stretch through the warning, stretched as it slams, squashed flat on impact and springing back while
    /// held; the bell over it wobbling; the post lamps (Factory's press logic); a cartoon drop shadow on the zone that darkens and
    /// spreads as the bun comes down.</item>
    /// <item>The Griddles: each zone's plate heating through the warning (NativeBlockLit's burner look, by light level), glowing
    /// white-hot while it is live and cooling after; corner lamps; little sausages that hop like popcorn on a live plate.</item>
    /// <item>The Geysers: the pump-dispenser beside each grate pumping three strokes through the warning, held down through the burst;
    /// the cap's rim lamps; the sauce column (mustard east, ketchup west) shooting up to 5 m and wobbling while it bursts.</item>
    /// <item>Ambient, on the render clock: the big turning hot dog sign under the roof with its chasing marquee bulbs and neon mustard,
    /// the overhead bun conveyors, the counters' roller grills turning their sausages, the Big Dog mascot waving and blinking on its
    /// plinth, the jukebox's bubble tubes.</item>
    /// </list>
    /// Each part carries its <see cref="Role"/> (the tests' rules): overhead structure, hanging gear, floor lamps and plates, ambient
    /// pieces, parts attached to a block. Telegraph lamps are full strength whatever the settings. Fills a fixed buffer
    /// (<see cref="Capacity"/>): allocation free per frame.
    /// </summary>
    public sealed class NativeEmporiumRig
    {
        public const int Capacity=3400;
        public const byte Overhead=0,Hanging=1,Floor=2,Ambient=3,Attached=4,Hazard=5;
        public readonly NativeFactoryKit.Writer Parts;public readonly byte[] Role=new byte[Capacity];
        /// <summary>Per hazard (index into the map's hazards; NaN for other kinds): the drawn wiener's centre x, the drawn bun's underside,
        /// the drawn sauce column's top (0 while quiet).</summary>
        public readonly double[] WienerX=new double[32],BunBottom=new double[32],ColumnTop=new double[32];
        private byte role;
        public NativeEmporiumRig(NativeFactoryKit.Looks looks){Parts=new NativeFactoryKit.Writer(Capacity,false,looks);}
        private void Add(double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=true,int color=-1,double window=1){int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow,color,window);if(Parts.Count>n)Role[n]=role;}
        private void Box(double x0,double x1,double y0,double y1,double z0,double z1,int kind,bool shadow=true,int color=-1,double window=1){int n=Parts.Count;Parts.Box(x0,x1,y0,y1,z0,z1,kind,shadow,color,window);if(Parts.Count>n)Role[n]=role;}
        private void Bar(double x0,double y0,double z0,double x1,double y1,double z1,double t,int kind,bool shadow=true,int color=-1){int n=Parts.Count;Parts.Bar(x0,y0,z0,x1,y1,z1,t,t,kind,shadow,color);if(Parts.Count>n)Role[n]=role;}
        private static readonly double[] Unit={-1,1};
        private static double H(int a,int b=0,int c=0)=>NativeEmporiumLooks.Hash(a,b,c);
        private static double Frac(double v)=>v-Math.Floor(v);
        private static double Clamp01(double v)=>v<0?0:v>1?1:v;
        private static double Smooth(double a,double b,double v){double t=Clamp01((v-a)/(b-a));return t*t*(3-2*t);}
        // Local axes: rotations that turn local z onto world x, y or z (the cylinders' axis).
        private static readonly FactoryQ ToX=FactoryQ.Axis(0,1,0,Math.PI/2),ToY=FactoryQ.Axis(1,0,0,-Math.PI/2),ToZ=FactoryQ.Identity;

        /// <summary>A prism of `sides` (8 or 16) round local z through `q` (its axis), centre (x, y, z), half length `l`, apothem `r`, its
        /// faces turned by `roll` about the axis; `sy` squashes it across (local y, 1: round). 8 sides: 4 slabs; 16: 8.</summary>
        private void Cyl(double x,double y,double z,FactoryQ q,double l,double r,double roll,int kind,int sides=16,bool shadow=true,int color=-1,double window=1,double sy=1)
        {
            int n=sides/2;double t=r*Math.Tan(Math.PI/sides)*1.04;
            for(int k=0;k<n;k++){var qk=q*FactoryQ.Axis(0,0,1,roll+k*Math.PI/n);Add(x,y,z,r,t*(sy<1?sy:1),l,qk,kind,shadow,color,window);}
        }

        /// <summary>Poses every rig of the Emporium from its hazards in round `round` at match phase/FightAt and the field clock `now`;
        /// `render` is the render clock for the ambient rigs. Nothing for any other map.</summary>
        public void Build(string mapId,MapHazard[] hazards,ContentBlock[] blocks,int round,NativePhase phase,double fightAt,double now,double render)
        {
            Parts.Clear();for(int i=0;i<WienerX.Length;i++){WienerX[i]=BunBottom[i]=double.NaN;ColumnTop[i]=0;}
            if(!NativeEmporiumLooks.Active(mapId))return;double clock=NativeFactoryHazards.Clock(phase,fightAt,now);
            for(int i=0;i<(hazards?.Length??0);i++)
            {
                var h=hazards[i];if(h==null)continue;
                if(h.kind=="maglev")Lane(h,NativeCyberCityHazards.At(h,phase,round,fightAt,now),now-fightAt,i);
                else if(h.kind==NativeFactoryHazards.Press)Press(h,NativeFactoryHazards.State(h,round,clock),round,clock,i);
                else if(h.kind==NativeWardZeroHazards.Burner)Griddle(h,NativeWardZeroHazards.At(h,phase,round,fightAt,now),now-fightAt,i);
                else if(h.kind==NativeFactoryHazards.Vent)Geyser(h,NativeFactoryHazards.State(h,round,clock),round,clock,blocks,i);
            }
            role=Ambient;Sign(render);Conveyors(render);Grills(blocks,render);Mascot(render);Jukebox(blocks,render);
        }

        // ================================================================ the Wiener Run (maglev)
        /// <summary>The wiener's radius (half the sweep's length along the lane), its length across the lane, and its centre this
        /// moment (NaN when no pass is under way).</summary>
        public static double Radius(MapHazard h)=>h.length/2;
        public static double Long(MapHazard h)=>Math.Max(1,h.size.z-1.0)/2;
        public static double Centre(MapHazard h,in NativeHazardState s)=>double.IsNaN(s.Head)?double.NaN:s.Head-s.Direction*Radius(h);
        /// <summary>The drawn bob over the rollers (m) at centre x: ba-dum, ba-dum.</summary>
        public static double Bob(double x)=>.09*Math.Abs(Math.Sin(x*1.7));
        /// <summary>A lane-edge lamp's colour (the telegraph; never reduced): lamp at x on a lane whose sweep is in state `s`.</summary>
        public static int LaneLamp(MapHazard h,in NativeHazardState s,double x,double time)
        {
            int d=s.Direction==0?1:s.Direction;
            switch(s.Phase)
            {
                case NativeHazardPhase.Warn:return Frac(x*d/6.0-time*2.2)<.34?NativeEmporiumLooks.Amber:NativeEmporiumLooks.Scale(NativeEmporiumLooks.Amber,.22+.25*s.Level);
                case NativeHazardPhase.Danger:return Frac(x*d/3.0-time*6.0)<.5||NativeFactoryRig.Strobe(time,4)?NativeEmporiumLooks.Danger:NativeEmporiumLooks.Scale(NativeEmporiumLooks.Danger,.3);
                case NativeHazardPhase.Active:{double c=Centre(h,s);return (x-c)*d>-Radius(h)?NativeEmporiumLooks.Danger:NativeEmporiumLooks.Scale(NativeEmporiumLooks.Danger,.18);}
                case NativeHazardPhase.Clear:return NativeEmporiumLooks.Mix(NativeEmporiumLooks.Off,NativeEmporiumLooks.Danger,s.Level*.7);
                default:return NativeEmporiumLooks.Off;
            }
        }
        /// <summary>A pet-door flap's swing (rad, + toward the lane) at the portal x `px` (facing `f` into the lane) for the sweep in
        /// state `s` at `time`: pushed open as the wiener passes, swinging shut after it, rattling through the danger.</summary>
        public static double Flap(MapHazard h,in NativeHazardState s,double px,int f,double time)
        {
            double a=0;if(s.Phase==NativeHazardPhase.Danger&&(px-h.center.x)*s.Direction<0)a+=.07*Math.Sin(time*41)*s.Level;
            double c=Centre(h,s);if(double.IsNaN(c)||s.Phase==NativeHazardPhase.Idle&&s.Since>3)return a;
            double r=Radius(h),past=(c-px)*s.Direction,sign=s.Direction*f>0?1:-1;
            if(past<-(r+.3))return a;
            if(past<r-.4)return a+sign*1.35*Smooth(-(r+.3),r-.4,past);
            double tau=(past-(r-.4))/Math.Max(1,h.speed);return a+sign*1.35*Math.Exp(-2.4*tau)*Math.Cos(9*tau);
        }
        private void Lane(MapHazard h,in NativeHazardState s,double time,int index)
        {
            double cz=h.center.z,hw=h.size.z/2,x0=h.center.x-h.size.x/2,x1=h.center.x+h.size.x/2,r=Radius(h),L=Long(h);
            role=Hazard;
            // the lane-edge lamps, both edges, every 1.5 m
            for(double x=x0+.75;x<x1;x+=1.5){int col=LaneLamp(h,s,x,time);foreach(double side in Unit){double z=cz+side*(hw+.16);Box(x-.16,x+.16,0,.05,z-.07,z+.07,NativeEmporiumLooks.Tube,false,col,2.2);}}
            // the ovens' flaps at both portals (hinged at the top, 2.2 m)
            foreach(double side in Unit)
            {
                double px=side<0?x0:x1;int f=side<0?1:-1;double ang=Flap(h,s,px,f,time);var q=FactoryQ.Axis(0,0,1,f*ang);const double top=2.2;
                void P(double ox,double oy,double oz,double ex,double ey,double ez,int kind,int color=-1){q.Rotate(ox,oy,oz,out double rx,out double ry,out double rz);Add(px+f*.03+rx,top+ry,cz+rz,ex,ey,ez,q,kind,false,color);}
                P(0,-1.05,0,.03,1.05,L+.15,NativeEmporiumLooks.Red);P(.035,-1.05,0,.005,.9,L-.1,NativeEmporiumLooks.Cream);
                P(.04,-.7,0,.006,.18,L*.55,NativeEmporiumLooks.Yellow);P(.04,-1.75,0,.006,.08,L,NativeEmporiumLooks.Dark);
            }
            // the wiener
            double c=Centre(h,s);WienerX[index]=double.NaN;if(double.IsNaN(c)||Math.Abs(c-h.center.x)>h.size.x/2+2.2)return;
            WienerX[index]=c;int d=s.Direction==0?1:s.Direction;double y=r+Bob(c),roll=-c/r;
            role=Hanging;
            Cyl(c,y,cz,ToZ,L-r*.35,r,roll,NativeEmporiumLooks.Sausage,16,true);
            foreach(double e in Unit){Cyl(c,y,cz+e*(L-r*.2),ToZ,r*.32,r*.86,roll,NativeEmporiumLooks.Sausage,8,true);Cyl(c,y,cz+e*(L+r*.2),ToZ,r*.16,r*.55,roll,NativeEmporiumLooks.SausageDark,8,false);}
            for(int k=0;k<3;k++){double a=roll+k*2*Math.PI/3;var q=FactoryQ.Axis(0,0,1,a);Add(c+Math.Cos(a+Math.PI/2)*(r+.004),y+Math.Sin(a+Math.PI/2)*(r+.004),cz,.09,.006,L-r*.4,q,NativeEmporiumLooks.SausageDark,false);}
            // googly eyes on the leading side, looking where it goes (they do not roll); the pupils hang low and wobble
            foreach(double e in Unit)
            {
                double ex=c+d*r*.78,ey=y+r*.55,ez=cz+e*.42;Cyl(ex+d*.04,ey,ez,ToX,.05,.28,0,NativeEmporiumLooks.White,8,false);
                double wob=Math.Sin(time*13+e)*.06;Box(ex+d*.1-.03,ex+d*.1+.03,ey-.2+wob*.3,ey-.04+wob*.3,ez-.08+wob,ez+.08+wob,NativeEmporiumLooks.Dark,false);
            }
        }

        // ================================================================ the Bun Presses (press)
        /// <summary>The drawn bun's squash (y scale; x and z take its inverse root): wind-up stretch and tremble, stretched slamming,
        /// squashed on impact and springing back while held.</summary>
        public static double Squash(FactoryHazard h,in FactoryHazardState st,out double tremble)
        {
            tremble=0;switch(st.Phase)
            {
                case NativeFactoryHazards.Warning:tremble=.025*st.Warn;return 1+.12*st.Warn;
                case NativeFactoryHazards.Slamming:return 1.16;
                case NativeFactoryHazards.Holding:{double tau=Math.Max(0,st.Age-(h.period-h.warn-h.rise-h.dwell-h.travel)-h.warn-h.rise);return 1-.3*Math.Exp(-5.5*tau)*Math.Cos(13*tau);}
                case NativeFactoryHazards.Lifting:return 1+.04*Math.Sin(st.Age*12);
                default:return 1;
            }
        }
        private static readonly double[] LayerH={.24,.3,.26,.18},LayerW={1.0,.94,.8,.55};
        private void Press(MapHazard h,in FactoryHazardState st,int round,double clock,int index)
        {
            double cx=st.Center.x,cz=st.Center.z,bottom=st.LoadY,hx=h.size.x/2,hz=h.size.z/2,sq=Squash(h,st,out double tremble),wide=1/Math.Sqrt(sq);
            if(tremble>0){cx+=tremble*Math.Sin(clock*71);cz+=tremble*Math.Cos(clock*59);}
            BunBottom[index]=bottom;double head=h.a.y+h.size.y/2+.15;
            role=Hanging;
            // the bun: a stepped dome of four rounded layers, sesame seeds on top; squash scales the stack
            double y=bottom;var lh=LayerH;var lw=LayerW;
            for(int k=0;k<4;k++){double t=lh[k]*sq;Add(cx,y+t/2,cz,Math.Min(1.55,hx*lw[k]*wide),t/2,Math.Min(1.55,hz*lw[k]*wide),FactoryQ.Identity,k==0?NativeEmporiumLooks.Bun:NativeEmporiumLooks.BunTop,true);y+=t;}
            for(int s=0;s<18;s++){double a=H(index,s)*2*Math.PI,rr=Math.Sqrt(H(index,s,1))*.85,px=cx+Math.Cos(a)*rr*hx*.8*wide,pz=cz+Math.Sin(a)*rr*hz*.8*wide;double py=bottom+(lh[0]+lh[1]+lh[2]*(rr<.6?1:.5)+(rr<.4?lh[3]:0))*sq;
                Add(px,py+.015,pz,.07,.025,.035,FactoryQ.Axis(0,1,0,a*3),NativeEmporiumLooks.Sesame,false);}
            // the ram: chrome rod from the head to the bun, its sleeve
            Box(cx-.22,cx+.22,y,head,cz-.22,cz+.22,NativeEmporiumLooks.Chrome,true);Box(cx-.34,cx+.34,head-1.2,head,cz-.34,cz+.34,NativeEmporiumLooks.Red,true);
            // the bell on the head: wobbles through the warning
            role=Overhead;double bell=st.Phase==NativeFactoryHazards.Warning?.35*Math.Sin(clock*22):0;var bq=FactoryQ.Axis(1,0,0,bell);
            Add(cx,head+1.3,cz+1.2,.32,.28,.32,bq,NativeEmporiumLooks.Yellow,true);Add(cx,head+1.0,cz+1.2,.36,.04,.36,bq,NativeEmporiumLooks.Yellow,false);
            // the post lamps (the press logic) at 2.4 m on the four posts' inner faces
            role=Hazard;int lamp=NativeFactoryRig.HazardLamp(st,clock);
            foreach(double sx in Unit)foreach(double sz in Unit){double px=h.a.x+sx*1.6,pz=h.a.z+sz*1.6;Box(px-.07,px+.07,2.2,2.7,pz-.07,pz+.07,NativeEmporiumLooks.Tube,false,lamp,2.0);}
            // the cartoon drop shadow on the zone: darkens and spreads as the bun comes down
            double k2=Clamp01(1-(bottom-.05)/(h.a.y-h.size.y/2));int shade=NativeEmporiumLooks.Mix(0x6E2A1C,0x120808,k2);double sh=.55+.45*k2;
            role=Floor;Box(h.a.x-hx*sh,h.a.x+hx*sh,.006,.014,h.a.z-hz*sh,h.a.z+hz*sh,NativeEmporiumLooks.Dark,false,shade);
        }

        // ================================================================ the Griddles (burner)
        /// <summary>A griddle plate's light level (NativeBlockLit's burner look heats above 1): cold at rest, heating through the
        /// warning, white-hot live, cooling after.</summary>
        public static double Heat(in NativeHazardState s,double time)=>s.Phase==NativeHazardPhase.Warn?1+.45*s.Level*(.85+.15*Math.Sin(time*21)):s.Phase==NativeHazardPhase.Active?1.75+.1*Math.Sin(time*17):s.Phase==NativeHazardPhase.Clear?1+.7*s.Level:1;
        public static int GriddleLamp(in NativeHazardState s,double time)=>s.Phase==NativeHazardPhase.Warn?(NativeFactoryRig.Strobe(time,NativeFactoryRig.WarnHz)?NativeEmporiumLooks.Amber:NativeEmporiumLooks.Off):s.Phase==NativeHazardPhase.Active||s.Phase==NativeHazardPhase.Clear?NativeEmporiumLooks.Danger:NativeEmporiumLooks.Scale(NativeEmporiumLooks.Safe,.6);
        private void Griddle(MapHazard h,in NativeHazardState s,double time,int index)
        {
            double heat=Heat(s,time);int lamp=GriddleLamp(s,time);
            for(int k=0;k<(h.zones?.Length??0);k++)
            {
                var z=h.zones[k];double x0=z.center.x-z.size.x/2,x1=z.center.x+z.size.x/2,z0=z.center.z-z.size.z/2,z1=z.center.z+z.size.z/2,y=z.center.y-z.size.y/2;
                role=Floor;Box(x0+.08,x1-.08,y,y+.02,z0+.08,z1-.08,NativeEmporiumLooks.Griddle,false,-1,heat);
                role=Hazard;foreach(double sx in Unit)foreach(double sz in Unit){double px=sx<0?x0+.18:x1-.18,pz=sz<0?z0+.18:z1-.18;Box(px-.1,px+.1,y+.02,y+.05,pz-.1,pz+.1,NativeEmporiumLooks.Tube,false,lamp,2.0);}
                // popcorn sausages: lying still when cold, jittering as it heats, hopping while it is live
                role=Hazard;
                for(int j=0;j<3;j++)
                {
                    double px=z.center.x+(H(index*7+k,j)-.5)*z.size.x*.6,pz=z.center.z+(H(index*7+k,j,1)-.5)*z.size.z*.6,yaw=H(index*7+k,j,2)*Math.PI*2,hop=0;
                    if(s.Phase==NativeHazardPhase.Active)hop=Math.Max(0,Math.Sin(time*8.5+j*2.1+k))*.42;else if(s.Phase==NativeHazardPhase.Warn)hop=.03*Math.Abs(Math.Sin(time*30+j))*s.Level;
                    var q=FactoryQ.Axis(0,1,0,yaw+(s.Phase==NativeHazardPhase.Active?time*3:0))*FactoryQ.Axis(1,0,0,hop*3);
                    Add(px,y+.08+hop,pz,.075,.075,.24,q,NativeEmporiumLooks.Sausage,false);
                }
            }
        }

        // ================================================================ the Geysers (vent with push)
        /// <summary>The pump's plunger stroke 0 (up) .. 1 (down): three strokes through the warning, held down through the burst.</summary>
        public static double Stroke(FactoryHazard h,in FactoryHazardState st)=>st.Phase==NativeFactoryHazards.Warning?Math.Pow(Math.Abs(Math.Sin(st.Warn*3*Math.PI)),.6):st.Phase==NativeFactoryHazards.Bursting?1:0;
        /// <summary>The sauce column's top over the grate (m): up to 5.2 m with the burst's level.</summary>
        public static double Column(in FactoryHazardState st)=>st.Phase==NativeFactoryHazards.Bursting?5.2*st.U:0;
        private void Geyser(MapHazard h,in FactoryHazardState st,int round,double clock,ContentBlock[] blocks,int index)
        {
            double x=h.a.x,y=h.a.y,z=h.a.z,hx=h.size.x/2,hz=h.size.z/2;bool ketchup=NativeEmporiumLooks.KetchupSide(x);int sauce=ketchup?NativeEmporiumLooks.Ketchup:NativeEmporiumLooks.Mustard;
            role=Hazard;int lamp=NativeFactoryRig.HazardLamp(st,clock);
            foreach(double sx in Unit)foreach(double sz in Unit)Box(x+sx*(hx+.12)-.09,x+sx*(hx+.12)+.09,y,y+.05,z+sz*(hz+.12)-.09,z+sz*(hz+.12)+.09,NativeEmporiumLooks.Tube,false,lamp,2.0);
            // the cap bulging through the warning (squash and stretch)
            double bulge=st.Phase==NativeFactoryHazards.Warning?.06*Math.Abs(Math.Sin(st.Warn*3*Math.PI)):st.Phase==NativeFactoryHazards.Bursting?.1:0;
            role=Floor;Box(x-hx*.55,x+hx*.55,y+.02,y+.05+bulge,z-hz*.55,z+hz*.55,sauce,false);
            // the pump-dispenser beside it: plunger head and nozzle on its block's top
            ContentBlock pump=null;double best=6;
            foreach(var b in blocks??Array.Empty<ContentBlock>()){if(b?.id==null||b.id.IndexOf("-pump-",StringComparison.Ordinal)<0)continue;double dd=Math.Abs(b.pos.x-x)+Math.Abs(b.pos.z-z);if(dd<best){best=dd;pump=b;}}
            if(pump!=null)
            {
                role=Attached;double top=pump.pos.y+pump.size.y/2,px=pump.pos.x,pz=pump.pos.z,stroke=Stroke(h,st),rise=.55*(1-stroke);int dir=Math.Sign(x-px);
                Box(px-.12,px+.12,top,top+.25+rise,pz-.12,pz+.12,NativeEmporiumLooks.White,true);
                double hy=top+.25+rise;Box(px-.42,px+.42,hy,hy+.22,pz-.42,pz+.42,sauce,true);Box(px-.46,px+.46,hy+.22,hy+.3,pz-.46,pz+.46,NativeEmporiumLooks.White,false);
                Bar(px,hy+.12,pz,px+dir*.75,hy-.05,pz,.16,NativeEmporiumLooks.White,true);
            }
            // the sauce column
            double top2=Column(st);ColumnTop[index]=top2;if(top2<.1)return;
            role=Hanging;int seg=6;
            for(int k=0;k<seg;k++){double y0=y+top2*k/seg,y1=y+top2*(k+1)/seg,rr=.55+.13*Math.Sin(clock*15+k*1.3)+.08*k/seg;Cyl(x+.04*Math.Sin(clock*9+k),(y0+y1)/2,z,ToY,(y1-y0)/2+.05,rr,clock*2+k,sauce,8,false);}
            Cyl(x,y+top2+.1,z,ToY,.22,.85+.1*Math.Sin(clock*11),clock,sauce,8,false);
        }

        // ================================================================ ambient
        private void Sign(double t)
        {
            double a=NativeEmporiumLooks.SignHeading(t),X=NativeEmporiumLooks.SignX,Y=NativeEmporiumLooks.SignY,Z=NativeEmporiumLooks.SignZ,L=NativeEmporiumLooks.SignLength/2;
            var yaw=FactoryQ.Axis(0,1,0,a);
            void P(double ox,double oy,double oz,double ex,double ey,double ez,FactoryQ q,int kind,bool shadow=true,int color=-1,double win=1){yaw.Rotate(ox,oy,oz,out double rx,out double ry,out double rz);Add(X+rx,Y+ry,Z+rz,ex,ey,ez,yaw*q,kind,shadow,color,win);}
            void C(double ox,double oy,double oz,FactoryQ q,double l,double r,int kind,int sides,bool shadow=true,int color=-1){yaw.Rotate(ox,oy,oz,out double rx,out double ry,out double rz);Cyl(X+rx,Y+ry,Z+rz,yaw*q,l,r,0,kind,sides,shadow,color);}
            // the frankfurter in its bun, local x along it
            C(0,0,0,ToX,L-.5,.9,NativeEmporiumLooks.Sausage,16);foreach(double e in Unit){C(e*(L-.32),0,0,ToX,.3,.76,NativeEmporiumLooks.Sausage,8);C(e*(L+.02),0,0,ToX,.12,.46,NativeEmporiumLooks.SausageDark,8,false);}
            foreach(double e in Unit){C(0,-.55,e*.84,ToX,L-1.2,.74,NativeEmporiumLooks.Bun,8);C(0,-.9,e*.72,ToX,L-1.6,.52,NativeEmporiumLooks.BunTop,8);}
            // neon mustard zigzag along the top (HDR)
            for(int k=0;k<14;k++){double u0=-L+1.1+k*(2*L-2.2)/14,u1=u0+(2*L-2.2)/14,w0=(k&1)==0?-.4:.4;yaw.Rotate(u0,.92,w0,out double ax,out double ay,out double az);yaw.Rotate(u1,.92,-w0,out double bx,out double by,out double bz);
                int n=Parts.Count;Parts.Bar(X+ax,Y+ay,Z+az,X+bx,Y+by,Z+bz,.14,.14,NativeEmporiumLooks.Tube,false,NativeEmporiumLooks.NeonLemon);if(Parts.Count>n){Parts.Items[n].Window=2.6f;Role[n]=role;}}
            // the marquee: an elliptical ring of bulbs chasing round, the pink neon between them
            const int bulbs=36;double rx0=L+1.4,ry0=2.9;
            for(int k=0;k<bulbs;k++)
            {
                double u=k*2*Math.PI/bulbs,u1=(k+1)*2*Math.PI/bulbs;bool on=((k+(int)Math.Floor(t*6))%3)==0;
                P(Math.Cos(u)*rx0,Math.Sin(u)*ry0,0,.17,.17,.17,FactoryQ.Identity,NativeEmporiumLooks.Tube,false,on?0xFFF2C0:0xFFA040,on?3.0:1.2);
                yaw.Rotate(Math.Cos(u)*(rx0+.22),Math.Sin(u)*(ry0+.22),0,out double ax,out double ay,out double az);yaw.Rotate(Math.Cos(u1)*(rx0+.22),Math.Sin(u1)*(ry0+.22),0,out double bx,out double by,out double bz);
                int n=Parts.Count;Parts.Bar(X+ax,Y+ay,Z+az,X+bx,Y+by,Z+bz,.11,.11,NativeEmporiumLooks.Tube,false,NativeEmporiumLooks.NeonPink);if(Parts.Count>n){Parts.Items[n].Window=2.8f;Role[n]=role;}
            }
            P(0,ry0+.7,0,2.6,.55,.1,FactoryQ.Identity,NativeEmporiumLooks.Red,true);P(0,ry0+.7,.11,2.4,.4,.012,FactoryQ.Identity,NativeEmporiumLooks.Tube,false,0xFFF2C0,2.6);P(0,ry0+.7,-.11,2.4,.4,.012,FactoryQ.Identity,NativeEmporiumLooks.Tube,false,0xFFF2C0,2.6);
            // its hangers to the ridge
            role=Overhead;foreach(double e in Unit){yaw.Rotate(e*(L-.6),ry0+.4,0,out double ax,out double ay,out double az);Bar(X+ax,Y+ay,Z+az,X+ax*.5,NativeEmporiumLooks.Ridge-.6,Z+az*.5,.05,NativeEmporiumLooks.Dark,false);}
            Box(X-.2,X+.2,Y+ry0+.9,NativeEmporiumLooks.Ridge-.4,Z-.2,Z+.2,NativeEmporiumLooks.Chrome,false);
            role=Ambient;
        }
        /// <summary>The two overhead bun conveyors (west and east loops over the kitchen line): a point at arc length `s` along one.</summary>
        public const double ConveyorY=9.4,ConveyorX0=9.2,ConveyorX1=17.2,ConveyorZ=5.0,ConveyorSpeed=.85;
        public static double ConveyorLength=>2*(ConveyorX1-ConveyorX0)+4*ConveyorZ;
        public static void ConveyorAt(int side,double s,out double x,out double z)
        {
            double w=ConveyorX1-ConveyorX0,len=ConveyorLength;s=Frac(s/len)*len;double e0=w,e1=w+2*ConveyorZ,e2=2*w+2*ConveyorZ;
            if(s<e0){x=ConveyorX0+s;z=ConveyorZ;}else if(s<e1){x=ConveyorX1;z=ConveyorZ-(s-e0);}else if(s<e2){x=ConveyorX1-(s-e1);z=-ConveyorZ;}else{x=ConveyorX0;z=-ConveyorZ+(s-e2);}
            x*=side;z*=side;
        }
        private void Conveyors(double t)
        {
            double len=ConveyorLength;int n=(int)Math.Floor(len/2.3);
            for(int side=-1;side<=1;side+=2)for(int k=0;k<n;k++)
            {
                ConveyorAt(side,k*len/n+t*ConveyorSpeed,out double x,out double z);double sw=.06*Math.Sin(t*1.7+k);
                role=Overhead;Box(x-.03,x+.03,ConveyorY-.72,ConveyorY-.05,z-.03,z+.03,NativeEmporiumLooks.Chrome,false);Box(x-.5,x+.5,ConveyorY-.8,ConveyorY-.72,z-.32,z+.32,NativeEmporiumLooks.Chrome,true);
                role=Ambient;for(int e=-1;e<=1;e++){double bx=x+e*.3+sw,bz=z;Cyl(bx,ConveyorY-.62,bz,ToZ,.26,.13,0,NativeEmporiumLooks.Bun,8,true);Box(bx-.12,bx+.12,ConveyorY-.52,ConveyorY-.48,bz-.2,bz+.2,NativeEmporiumLooks.BunTop,false);}
            }
        }
        /// <summary>The roller grills on the counters and prep tables: each block whose id names a counter or a prep table carries one
        /// machine along its top (the kit draws the housing); its sausages turn (grill stripes) and jiggle.</summary>
        private void Grills(ContentBlock[] blocks,double t)
        {
            int seed=0;
            foreach(var b in blocks??Array.Empty<ContentBlock>())
            {
                if(b?.id==null)continue;bool counter=b.id.IndexOf("-counter-",StringComparison.Ordinal)>=0,prep=b.id.IndexOf("-prep-",StringComparison.Ordinal)>=0;if(!counter&&!prep)continue;seed++;
                double top=b.pos.y+b.size.y/2,y=top+.16;bool alongX=b.size.x>=b.size.z;double len=Math.Min(3.0,(alongX?b.size.x:b.size.z)-.6),cross=Math.Min(.7,(alongX?b.size.z:b.size.x)-.25);
                int n=(int)Math.Floor(len/.26);
                for(int k=0;k<n;k++)
                {
                    double u=-len/2+(k+.5)*len/n,roll=t*2.2+k*.7,jig=.006*Math.Sin(t*9+k*1.9+seed);
                    double px=alongX?b.pos.x+u:b.pos.x+jig,pz=alongX?b.pos.z+jig:b.pos.z+u;var q=alongX?FactoryQ.Identity:ToX;
                    Add(px,y,pz,.055,.055,cross/2-.04,q,NativeEmporiumLooks.Sausage,false);
                    var qs=q*FactoryQ.Axis(0,0,1,roll);qs.Rotate(0,.056,0,out double ox,out double oy,out double oz);Add(px+ox,y+oy,pz+oz,.02,.006,cross/2-.08,qs,NativeEmporiumLooks.SausageDark,false);
                }
            }
        }
        /// <summary>The Big Dog on the plinth: a standing hot dog in its bun, a chef's hat, googly eyes that blink, one arm waving.</summary>
        private void Mascot(double t)
        {
            const double x=0,z=0,base0=6.0;
            role=Ambient;
            Cyl(x,base0+1.6,z,ToY,1.5,.6,0,NativeEmporiumLooks.Sausage,16);Cyl(x,base0+3.2,z,ToY,.2,.46,0,NativeEmporiumLooks.Sausage,8);
            foreach(double e in Unit)Cyl(x+e*.6,base0+1.3,z,ToY,1.25,.44,0,NativeEmporiumLooks.Bun,8);
            for(int k=0;k<5;k++){double y0=base0+.5+k*.5,w=(k&1)==0?-.3:.3;int n=Parts.Count;Parts.Bar(x+w,y0,z-.6,x-w,y0+.5,z-.6,.08,.08,NativeEmporiumLooks.Tube,false,NativeEmporiumLooks.NeonLemon);if(Parts.Count>n){Parts.Items[n].Window=2.2f;Role[n]=role;}}
            Cyl(x,base0+3.5,z,ToY,.1,.6,0,NativeEmporiumLooks.White,8);Cyl(x,base0+3.85,z,ToY,.3,.5,0,NativeEmporiumLooks.White,8);Cyl(x,base0+4.2,z,ToY,.16,.64,0,NativeEmporiumLooks.White,8);
            bool blink=Frac(t/3.7)<.05;
            foreach(double e in Unit){Cyl(x+e*.24,base0+2.6,z-.58,ToZ,.04,.18,0,NativeEmporiumLooks.White,8,false,-1,1);if(!blink)Box(x+e*.24-.06,x+e*.24+.06,base0+2.44,base0+2.58,z-.65,z-.62,NativeEmporiumLooks.Dark,false);else Box(x+e*.24-.17,x+e*.24+.17,base0+2.57,base0+2.63,z-.65,z-.62,NativeEmporiumLooks.Dark,false);}
            Box(x-.25,x+.25,base0+2.1,base0+2.18,z-.63,z-.59,NativeEmporiumLooks.Dark,false);Box(x-.32,x-.24,base0+2.18,base0+2.28,z-.63,z-.59,NativeEmporiumLooks.Dark,false);Box(x+.24,x+.32,base0+2.18,base0+2.28,z-.63,z-.59,NativeEmporiumLooks.Dark,false);
            // arms: the left on its hip, the right waving
            Bar(x-.6,base0+1.9,z,x-1.15,base0+1.2,z-.2,.14,NativeEmporiumLooks.White);
            double wave=.5+.45*Math.Sin(t*5.2);double ax=x+.6,ay=base0+1.9,ex=ax+.55,ey=ay+.35,hx=ex+Math.Sin(wave)*.6,hy=ey+Math.Cos(wave)*.6;
            Bar(ax,ay,z,ex,ey,z-.1,.14,NativeEmporiumLooks.White);Bar(ex,ey,z-.1,hx,hy,z-.15,.13,NativeEmporiumLooks.White);Cyl(hx,hy,z-.15,ToZ,.1,.17,0,NativeEmporiumLooks.White,8);
        }
        private void Jukebox(ContentBlock[] blocks,double t)
        {
            role=Attached;
            foreach(var b in blocks??Array.Empty<ContentBlock>())
            {
                if(b?.id==null||b.id.IndexOf("-jukebox-",StringComparison.Ordinal)<0)continue;
                int f=b.pos.z>0?-1:1;double face=b.pos.z+f*b.size.z/2;
                for(int k=0;k<2;k++){double px=b.pos.x+(k==0?-1:1)*(b.size.x/2-.12);int col=NativeEmporiumLooks.Mix(NativeEmporiumLooks.NeonPink,NativeEmporiumLooks.NeonCyan,.5+.5*Math.Sin(t*1.3+k*2));
                    Box(px-.06,px+.06,.4,2.3,Math.Min(face,face+f*.06),Math.Max(face,face+f*.06),NativeEmporiumLooks.Tube,false,col,2.2);
                    for(int j=0;j<4;j++){double by=.5+Frac(t*.4+j*.25+k*.13)*1.7;Box(px-.035,px+.035,by,by+.07,Math.Min(face,face+f*.075),Math.Max(face,face+f*.075),NativeEmporiumLooks.White,false);}}
            }
        }
    }
}
