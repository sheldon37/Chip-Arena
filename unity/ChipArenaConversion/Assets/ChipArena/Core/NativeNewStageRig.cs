using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The five M1 stages' moving rigs for one frame (docs/arenas/NEW-STAGES.md "Stage looks (presentation)"), posed only from the
    /// replicated hazard state: the round index, the match phase, FightAt and the drawn arena's field clock, through the very
    /// functions the simulation decides hits with (NativeFactoryHazards.State for the cranes and vents, NativeWardZeroHazards.At for
    /// the burners), so host, guests and the killcam draw the same rig at the same moment and nothing new is sent.
    /// <list type="bullet">
    /// <item>Pier 9's rail-mounted gantry: the bridge between the two runways at the hook's x, its end trucks and operator's cab,
    /// the trolley over the hook with its beacon, the four ropes, the headblock and spreader (hanging 1.1 m under the headblock,
    /// on the container's roof while it holds it), the 20 ft container itself, and the five slots' corner lamps.</item>
    /// <item>Skyframe's tower crane: the slewing jib, counter-jib, counterweights and cab round the mast, the pendants to the
    /// static head, the trolley at the hook's reach, ropes, hook block, slings and the beam bundle, the four zones' lamps on their
    /// own floors. The simulation moves the hook straight from zone to zone (through the mast for opposite zones); while it
    /// travels, carried high and harmless with no collider, the rig slews and trollies instead (the same start, the same end,
    /// <see cref="Reach"/>); from the warning to the next travel it is exactly the simulation's hook.</item>
    /// <item>Deepwell's flare burners (the grates in the walkway glowing through the 1.5 s warning, the nozzle row white-hot
    /// while they burn, cooling red after) with each drum's beacon, and the blowdown vents' grates and corner lamps.</item>
    /// <item>The ambient rigs: Breakwater's lighthouse optic turning in its lantern, Polaris's radar on the met tower.</item>
    /// </list>
    /// Each part carries its <see cref="Role"/> (the tests' rules): overhead structure, hanging gear, floor lamps and grates, or
    /// ambient pieces outside the bounds. Lamps telegraph at full strength whatever the settings (ARENA_PROPOSAL.md 2.1 rule 4).
    /// Fills a fixed buffer (<see cref="Capacity"/>): allocation free per frame.
    /// </summary>
    public sealed class NativeNewStageRig
    {
        public const int Capacity=1400;
        public const byte Overhead=0,Hanging=1,Floor=2,Ambient=3,Attached=4;
        public readonly NativeFactoryKit.Writer Parts;public readonly byte[] Role=new byte[Capacity];
        /// <summary>Where the rig draws each crane's hook this frame (index by hazard; NaN for other kinds): the drawn x and z,
        /// the load's underside, whether the drawn hook is the simulation's own.</summary>
        public readonly double[] HookX=new double[16],HookZ=new double[16],LoadY=new double[16];public readonly bool[] Exact=new bool[16];
        private byte role;
        public NativeNewStageRig(NativeFactoryKit.Looks looks){Parts=new NativeFactoryKit.Writer(Capacity,false,looks);}
        private void Add(double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=true,int color=-1,double window=1){int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow,color,window);if(Parts.Count>n)Role[n]=role;}
        private void Box(double x0,double x1,double y0,double y1,double z0,double z1,int kind,bool shadow=true,int color=-1){int n=Parts.Count;Parts.Box(x0,x1,y0,y1,z0,z1,kind,shadow,color);if(Parts.Count>n)Role[n]=role;}
        private void Bar(double x0,double y0,double z0,double x1,double y1,double z1,double t,int kind,bool shadow=true,int color=-1){int n=Parts.Count;Parts.Bar(x0,y0,z0,x1,y1,z1,t,t,kind,shadow,color);if(Parts.Count>n)Role[n]=role;}
        private static readonly double[] Unit={-1,1},P150={-1.5,1.5},P090={-.9,.9},P050={-.5,.5},P070={-.7,.7},P018={-.18,.18},Doors={-.55,-.2,.2,.55},Chords={-JibHalf,JibHalf};

        /// <summary>Poses every rig of the stage `mapId` from its hazards in round `round` at match phase/FightAt and the field clock
        /// `now`; `render` is the render clock for the ambient rigs. Nothing for any other map.</summary>
        public void Build(string mapId,MapHazard[] hazards,ContentBlock[] blocks,int round,NativePhase phase,double fightAt,double now,double render)
        {
            Parts.Clear();for(int i=0;i<HookX.Length;i++){HookX[i]=HookZ[i]=LoadY[i]=double.NaN;Exact[i]=false;}
            int stage=NativeNewStageLooks.Index(mapId);if(stage<0)return;double clock=NativeFactoryHazards.Clock(phase,fightAt,now);
            for(int i=0;i<(hazards?.Length??0);i++)
            {
                var h=hazards[i];if(h==null)continue;
                if(h.kind==NativeFactoryHazards.Crane){var st=NativeFactoryHazards.State(h,round,clock);if(stage==2)TowerCrane(h,st,round,clock,i);else Gantry(h,st,clock,blocks,i);}
                else if(h.kind==NativeFactoryHazards.Vent)Vent(h,NativeFactoryHazards.State(h,round,clock),clock);
                else if(h.kind==NativeWardZeroHazards.Burner)Burner(h,NativeWardZeroHazards.At(h,phase,round,fightAt,now),now,blocks);
            }
            role=Ambient;
            if(stage==0)Optic(render);else if(stage==4)Radar(render);
        }

        // ---------------- Pier 9: the rail-mounted gantry ----------------
        /// <summary>The runway lines the gantry's end trucks ride (z of the two runway blocks nearest its rail line), else ±10 m.</summary>
        public static void Runways(FactoryHazard h,ContentBlock[] blocks,out double south,out double north)
        {
            south=h.a.z-10;north=h.a.z+10;double bs=double.NaN,bn=double.NaN;
            foreach(var b in blocks??Array.Empty<ContentBlock>())
            {
                if(b?.id==null||b.id.IndexOf("gantry-runway",StringComparison.Ordinal)<0||Math.Abs(b.pos.y+b.size.y/2-h.a.y)>.05)continue;
                if(b.pos.z<h.a.z&&(double.IsNaN(bs)||b.pos.z>bs))bs=b.pos.z;if(b.pos.z>h.a.z&&(double.IsNaN(bn)||b.pos.z<bn))bn=b.pos.z;
            }
            if(!double.IsNaN(bs))south=bs;if(!double.IsNaN(bn))north=bn;
        }
        private void Gantry(MapHazard h,in FactoryHazardState st,double clock,ContentBlock[] blocks,int index)
        {
            Runways(h,blocks,out double zs,out double zn);double X=st.X,Z=st.Z,rail=h.a.y+.12,top=rail+2.4;int K=NativeNewStageLooks.Crane,D=NativeNewStageLooks.CraneDark;
            role=Overhead;
            // end trucks on the runways' rails, two box girders between them, the walkway and the operator's cab at the north end
            for(int e=0;e<2;e++){double ez=e==0?zs:zn;Box(X-2.2,X+2.2,rail,rail+.75,ez-.42,ez+.42,D);foreach(double dx in P150)Add(X+dx,rail+.3,ez,.3,.3,.36,FactoryQ.Axis(0,0,1,Math.PI/4),NativeNewStageLooks.Wheel,false);Box(X-2.25,X+2.25,rail+.75,rail+.85,ez-.45,ez+.45,NativeNewStageLooks.Stripe,false);}
            foreach(double s in Unit){Box(X+s*1.15-.32,X+s*1.15+.32,rail+.75,top,zs-.5,zn+.5,K);Box(X+s*1.15-.33,X+s*1.15+.33,top-.12,top,zs-.5,zn+.5,NativeNewStageLooks.Stripe,false);}
            Box(X+1.5,X+2.3,rail+.75,rail+.82,zs,zn,NativeNewStageLooks.Grate,false);for(double z=zs;z<=zn;z+=2)Box(X+2.26,X+2.3,rail+.82,rail+1.85,z-.03,z+.03,NativeNewStageLooks.Rail,false);Box(X+2.26,X+2.3,rail+1.8,rail+1.86,zs,zn,NativeNewStageLooks.Rail,false);
            double cz=zn-2.6;Box(X-1,X+1,rail-2.2,rail+.75,cz-1,cz+1,K);Box(X-1.02,X+1.02,rail-1.5,rail+.4,cz-1.02,cz-.98,NativeNewStageLooks.Glass,false);Box(X-1.02,X-.98,rail-1.5,rail+.4,cz-.9,cz+.9,NativeNewStageLooks.Glass,false);
            // the trolley over the hook: frame, hoist drum, motor, beacon telegraphing the cycle
            Box(X-1.6,X+1.6,top,top+.5,Z-1.4,Z+1.4,D);Box(X-1.1,X+1.1,top+.5,top+1.3,Z-.5,Z+.5,K);Box(X+1.1,X+1.6,top+.5,top+1.1,Z-.4,Z+.4,NativeNewStageLooks.Panel);
            Box(X-.14,X+.14,top+1.3,top+1.6,Z-.14,Z+.14,NativeNewStageLooks.Lamp,false,NativeFactoryRig.HazardLamp(st,clock));
            // ropes, headblock, the spreader 1.1 m under it (on the container's roof while it holds it), the load
            role=Hanging;double hy=st.HookY;foreach(double dx in P090)foreach(double dz in P050)Box(X+dx-.025,X+dx+.025,hy+.45,top,Z+dz-.025,Z+dz+.025,NativeNewStageLooks.Cable,false);
            Box(X-1.0,X+1.0,hy,hy+.45,Z-.7,Z+.7,K,hy<4);Box(X-1.02,X+1.02,hy+.3,hy+.45,Z-.72,Z+.72,NativeNewStageLooks.Stripe,false);
            double sb=hy-NativeFactoryHazards.HookGap;foreach(double dx in P070)Box(X+dx-.06,X+dx+.06,sb+.25,hy,Z-.06,Z+.06,D,false);
            Box(X-h.size.x/2,X+h.size.x/2,sb,sb+.25,Z-h.size.z/2+.05,Z+h.size.z/2-.05,D,hy<5);foreach(double ux in Unit)Box(X+ux*h.size.x/2-.15,X+ux*h.size.x/2+.15,sb+.25,sb+.45,Z-h.size.z/2+.1,Z+h.size.z/2-.1,K,false);
            HookX[index]=X;HookZ[index]=Z;LoadY[index]=st.LoadY;Exact[index]=true;
            LoadContainer(h,X,st.LoadY,Z,st.Sway,hy);
            role=Floor;ZoneLamps(h,st,clock);
        }
        /// <summary>The 20 ft container under the spreader (swaying about the headblock), its corner posts, ribs and door end.</summary>
        private void LoadContainer(MapHazard h,double x,double y,double z,double sway,double pivot)
        {
            var q=FactoryQ.Axis(1,0,0,sway);double hx=h.size.x/2,hy=h.size.y/2,hz=h.size.z/2;bool cast=y<2.5;
            void P(double px,double py,double pz,double ex,double ey,double ez,int kind,bool shadow,int color=-1){double dx=px-x,dy=py-pivot,dz=pz-z;q.Rotate(dx,dy,dz,out double ox,out double oy,out double oz);Add(x+ox,pivot+oy,z+oz,ex,ey,ez,q,kind,shadow,color);}
            P(x,y+hy,z,hx,hy,hz,NativeNewStageLooks.Load,cast);
            foreach(double ux in Unit)foreach(double uz in Unit)P(x+ux*(hx-.08),y+hy,z+uz*(hz-.08),.09,hy+.004,.09,NativeNewStageLooks.Dark,false);
            for(double u=-hx+.6;u<hx-.4;u+=.6)foreach(double uz in Unit)P(x+u,y+hy,z+uz*(hz+.006),.04,hy-.12,.008,NativeNewStageLooks.Dark,false);
            P(x-hx-.006,y+hy,z,.008,hy-.12,.02,NativeNewStageLooks.Dark,false);foreach(double f in Doors)P(x-hx-.012,y+hy,z+f*hz,.012,hy-.18,.025,NativeNewStageLooks.Steel,false);
        }
        /// <summary>A crane's zone corner lamps (NativeFactoryRig.ZoneLamp: dark unless served; amber travelling there, red strobing
        /// through the telegraph, the chase as it falls, amber while landed) on each zone's own floor.</summary>
        private void ZoneLamps(MapHazard h,in FactoryHazardState st,double clock)
        {
            for(int k=0;k<(h.zones?.Length??0);k++)
            {
                var zn=h.zones[k];double hx=zn.size.x/2+.5,hz=zn.size.z/2+.5,y=zn.center.y;int col=NativeFactoryRig.ZoneLamp(st,k,clock);
                foreach(double sx in Unit)foreach(double sz in Unit){double px=zn.center.x+sx*hx,pz=zn.center.z+sz*hz;Box(px-.12,px+.12,y,y+.05,pz-.12,pz+.12,NativeNewStageLooks.Lamp,false,col);}
            }
        }

        // ---------------- Skyframe: the tower crane ----------------
        /// <summary>The jib's reach where the trolley parks before the first lift, the least it ever draws (clear of the mast), its
        /// length, the counter-jib's, and the jib's chords (the bottom one at the hazard's runway height).</summary>
        public const double ParkReach=8,MinReach=3.2,JibLength=30,CounterJib=11,JibDepth=2.0,JibHalf=.8;
        /// <summary>
        /// The drawn hook for a tower crane at hazard clock `clock`: its heading (rad, from +z toward +x), its reach, and whether it
        /// is the simulation's own point. Travelling, it slews and trollies from the zone it left (the park at <see cref="ParkReach"/>
        /// on the first lift's heading) to the one it serves, on the simulation's own easing, so both ends are the simulation's
        /// points; at every other phase it is the simulation's hook (X, Z).
        /// </summary>
        public static bool Reach(FactoryHazard h,in FactoryHazardState st,int round,double clock,out double heading,out double reach)
        {
            double zx=st.X,zz=st.Z;
            if(st.Cycle<0){int z0=NativeFactoryHazards.ZoneOf(h,round,0);heading=z0>=0?Math.Atan2(h.zones[z0].center.x,h.zones[z0].center.z):0;reach=ParkReach;return false;}
            if(st.Phase==NativeFactoryHazards.Travelling&&NativeFactoryHazards.Cycle(h,clock,out _,out double u)&&h.travel>0)
            {
                var to=h.zones[st.Zone].center;double a1=Math.Atan2(to.x,to.z),r1=Math.Sqrt(to.x*to.x+to.z*to.z),a0,r0;
                if(st.From>=0){var from=h.zones[st.From].center;a0=Math.Atan2(from.x,from.z);r0=Math.Sqrt(from.x*from.x+from.z*from.z);}else{a0=a1;r0=ParkReach;}
                double d=a1-a0;while(d>Math.PI)d-=2*Math.PI;while(d<-Math.PI)d+=2*Math.PI;double q=NativeFactoryHazards.Smooth(u/h.travel);
                heading=a0+d*q;reach=Math.Max(MinReach,r0+(r1-r0)*q);return q>=1-1e-9;
            }
            heading=Math.Atan2(zx,zz);reach=Math.Sqrt(zx*zx+zz*zz);if(reach<MinReach){reach=MinReach;return false;}return true;
        }
        private void TowerCrane(MapHazard h,in FactoryHazardState st,int round,double clock,int index)
        {
            bool exact=Reach(h,st,round,clock,out double a,out double r);double s=Math.Sin(a),c=Math.Cos(a),px=-c,pz=s;// (px, pz): across the jib
            double jy=h.a.y,ty=jy+JibDepth;int K=NativeNewStageLooks.Crane,D=NativeNewStageLooks.CraneDark;
            double X(double u,double w)=>s*u+px*w;double Z(double u,double w)=>c*u+pz*w;
            role=Overhead;
            // the slewing unit and the cab beside it
            var yaw=FactoryQ.Axis(0,1,0,a);Add(0,jy-.7,0,1.9,.7,1.9,yaw,D);Add(0,jy-.05,0,2.0,.08,2.0,yaw,NativeNewStageLooks.Stripe,false);
            Add(X(1.4,1.9),jy-.6,Z(1.4,1.9),.8,.9,.9,yaw,K);Add(X(2.21,1.9),jy-.5,Z(2.21,1.9),.02,.55,.75,yaw,NativeNewStageLooks.Glass,false);
            // the jib: two bottom chords and the top one, laced every 2 m; the counter-jib with its counterweights
            foreach(double w in Chords)Bar(X(1.2,w),jy,Z(1.2,w),X(JibLength,w),jy,Z(JibLength,w),.16,K);Bar(X(1.2,0),ty,Z(1.2,0),X(JibLength-1,0),ty-.3,Z(JibLength-1,0),.16,K);
            for(double u=1.2;u<JibLength-1.5;u+=2)
            {
                double u1=u+2,yt0=ty-.3*(u-1.2)/(JibLength-2.2),yt1=ty-.3*(u1-1.2)/(JibLength-2.2);
                foreach(double w in Chords)Bar(X(u,w),jy,Z(u,w),X(u1,0),yt1,Z(u1,0),.08,K,false);
                Bar(X(u,-JibHalf),jy,Z(u,-JibHalf),X(u1,JibHalf),jy,Z(u1,JibHalf),.07,K,false);Bar(X(u1,-JibHalf),jy,Z(u1,-JibHalf),X(u1,JibHalf),jy,Z(u1,JibHalf),.07,K,false);
            }
            Add(X(JibLength+.1,0),jy+.4,Z(JibLength+.1,0),.15,.5,JibHalf+.1,yaw,K);Add(X(JibLength+.1,0),jy+1.0,Z(JibLength+.1,0),.12,.12,.12,yaw,NativeNewStageLooks.Lamp,false,0xFF3020);
            foreach(double w in Unit)Bar(X(-1.2,w),jy+.2,Z(-1.2,w),X(-CounterJib,w),jy+.2,Z(-CounterJib,w),.3,K);
            Add(X(-CounterJib*.55,0),jy+.25,Z(-CounterJib*.55,0),CounterJib*.42,.05,1.0,yaw,NativeNewStageLooks.Grate,false);
            for(int k=0;k<4;k++){double u=-CounterJib+.5+k*.9;Add(X(u,0),jy-.9,Z(u,0),.42,1.1,1.2,yaw,NativeNewStageLooks.Weight);}
            Add(X(-5.2,0),jy+.9,Z(-5.2,0),1.0,.7,.8,yaw,NativeNewStageLooks.Panel);
            double apex=24+5;Bar(0,apex,0,X(JibLength*.66,0),ty-.2,Z(JibLength*.66,0),.06,NativeNewStageLooks.Cable,false);Bar(0,apex,0,X(-CounterJib+.4,0),jy+.3,Z(-CounterJib+.4,0),.06,NativeNewStageLooks.Cable,false);
            // the trolley at the hook's reach, the ropes, the hook block, the slings and the beam bundle
            double tx=X(r,0),tz=Z(r,0);Add(tx,jy-.25,tz,.7,.25,JibHalf+.15,yaw,D);Add(tx,jy+.1,tz,.5,.12,.5,yaw,K,false);
            role=Hanging;double hy=st.HookY;foreach(double w in P018)Box(X(r,w)-.02,X(r,w)+.02,hy+1.0,jy-.5,Z(r,w)-.02,Z(r,w)+.02,NativeNewStageLooks.Cable,false);
            Add(tx,hy+.62,tz,.32,.38,.2,yaw,K,hy<4);Add(tx,hy+.78,tz,.33,.08,.21,yaw,NativeNewStageLooks.Stripe,false);Box(tx-.05,tx+.05,hy,hy+.24,tz-.06,tz+.06,D,false);
            HookX[index]=tx;HookZ[index]=tz;LoadY[index]=st.LoadY;Exact[index]=exact;
            Bundle(h,st,tx,tz,hy);
            role=Floor;ZoneLamps(h,st,clock);
        }
        /// <summary>The beam bundle (three I-beams strapped), hanging and swaying about the hook, falling or landed (Factory's load).</summary>
        private void Bundle(MapHazard h,in FactoryHazardState st,double lx,double lz,double pivot)
        {
            double ly=st.LoadY,top=h.size.y,bw=h.size.z/3;bool cast=ly<2.5+(h.zones!=null&&st.Zone>=0?h.zones[st.Zone].center.y:0);var sway=FactoryQ.Axis(1,0,0,st.Sway);
            void Piece(double px,double py,double pz,double hx,double hy,double hz,int kind,bool shadow){double dx=px-lx,dy=py-pivot,dz=pz-lz;sway.Rotate(dx,dy,dz,out double ox,out double oy,out double oz);Add(lx+ox,pivot+oy,lz+oz,hx,hy,hz,sway,kind,shadow);}
            for(int k=0;k<3;k++){double bz=lz-h.size.z/2+bw*(k+.5);Piece(lx,ly+.04,bz,h.size.x/2,.04,bw*.46,NativeNewStageLooks.Load,cast);Piece(lx,ly+top-.04,bz,h.size.x/2,.04,bw*.46,NativeNewStageLooks.Load,cast);Piece(lx,ly+top/2,bz,h.size.x/2,top/2-.08,.035,NativeNewStageLooks.Load,cast);}
            foreach(double ux in Unit)Piece(lx+ux*h.size.x*.3,ly+top/2,lz,.05,top/2+.02,h.size.z/2+.02,NativeNewStageLooks.Stripe,false);
            if(st.Attached)foreach(double ux in Unit){double ax=lx+ux*h.size.x*.4,ay=ly+top;double dx=ax-lx,dy=ay-pivot;sway.Rotate(dx,dy,0,out double ox,out double oy,out double oz);Bar(lx,pivot,lz,lx+ox,pivot+oy,lz+oz,.04,NativeNewStageLooks.Cable,false);}
            else Box(lx-.02,lx+.02,pivot-1.0,pivot,lz-.02,lz+.02,NativeNewStageLooks.Cable,false);
        }

        // ---------------- Deepwell: the flare burners and the blowdown vents ----------------
        /// <summary>A burner nozzle's colour by the burst's phase (`time`: seconds into the phase, so every client and the killcam
        /// flicker alike): banked embers at rest, an amber flicker rising through the warning,
        /// white-hot while it burns, cooling red after.</summary>
        public static int NozzleColor(in NativeHazardState s,double time,int slot)
        {
            switch(s.Phase)
            {
                case NativeHazardPhase.Warn:{double k=.35+.65*s.Level,f=.8+.2*Math.Sin(time*23+slot*1.7);return Mix(0x2A1408,0xFFB030,k*f);}
                case NativeHazardPhase.Active:return Mix(0xFFC870,0xFFF0D0,.5+.5*Math.Sin(time*31+slot));
                case NativeHazardPhase.Clear:return Mix(0x3A1A10,0xC83A1E,s.Level);
                default:return 0x2A1408;
            }
        }
        /// <summary>The drum beacon over each burner lane: green at rest, amber strobing through the warning, red while it burns
        /// and through the clear (never reduced).</summary>
        public static int BeaconColor(in NativeHazardState s,double time)=>s.Phase==NativeHazardPhase.Warn?(NativeFactoryRig.Strobe(time,NativeFactoryRig.WarnHz)?NativeFactoryRig.LampAmber:NativeFactoryRig.LampOff):s.Phase==NativeHazardPhase.Active||s.Phase==NativeHazardPhase.Clear?NativeFactoryRig.LampRed:NativeFactoryRig.LampSafe;
        private static int Mix(int a,int b,double t)=>NativeNewStageScenery.Mix(a,b,t);
        private void Burner(MapHazard h,in NativeHazardState s,double time,ContentBlock[] blocks)
        {
            role=Floor;
            foreach(var z in h.zones??Array.Empty<MapVolume>())
            {
                double y=z.center.y-z.size.y/2,x0=z.center.x-z.size.x/2,x1=z.center.x+z.size.x/2,z0=z.center.z-z.size.z/2,z1=z.center.z+z.size.z/2;
                Box(x0+.1,x1-.1,y,y+.025,z0+.15,z1-.15,NativeNewStageLooks.Burner,false);
                for(int i=0;i<4;i++)for(int j=0;j<6;j++){double px=x0+.3+(x1-x0-.6)*(i+.5)/4,pz=z0+.4+(z1-z0-.8)*(j+.5)/6;Box(px-.08,px+.08,y+.02,y+.045,pz-.08,pz+.08,NativeNewStageLooks.Lamp,false,NozzleColor(s,s.Since,i*6+j));}
            }
            // the beacons on the knock-out drums' walkway faces beside the lane
            role=Attached;int beacon=BeaconColor(s,s.Since);
            foreach(var b in blocks??Array.Empty<ContentBlock>())
            {
                if(b?.id==null||b.id.IndexOf("-kodrum-",StringComparison.Ordinal)<0||Math.Sign(b.pos.z)!=Math.Sign(h.center.z))continue;
                double face=b.pos.z+Math.Sign(h.center.z)*b.size.z/2,top=b.pos.y+b.size.y/2;
                Box(b.pos.x-.2,b.pos.x+.2,top-.32,top-.08,Math.Min(face,face+Math.Sign(h.center.z)*.12),Math.Max(face,face+Math.Sign(h.center.z)*.12),NativeNewStageLooks.Lamp,false,beacon);
            }
        }
        private void Vent(MapHazard h,in FactoryHazardState st,double clock)
        {
            role=Floor;double x=h.a.x,y=h.a.y,z=h.a.z,hx=h.size.x/2,hz=h.size.z/2;
            Box(x-hx,x+hx,y,y+.03,z-hz,z+hz,NativeNewStageLooks.Grate,false);Box(x-hx-.12,x+hx+.12,y,y+.02,z-hz-.12,z+hz+.12,NativeNewStageLooks.Dark,false);
            int col=NativeFactoryRig.HazardLamp(st,clock);foreach(double sx in Unit)foreach(double sz in Unit)Box(x+sx*(hx+.22)-.08,x+sx*(hx+.22)+.08,y,y+.05,z+sz*(hz+.22)-.08,z+sz*(hz+.22)+.08,NativeNewStageLooks.Lamp,false,col);
        }

        // ---------------- the ambient rigs ----------------
        private void Optic(double t)
        {
            double a=NativeNewStageLooks.BeamHeading(t),x=NativeNewStageLooks.LighthouseX,y=NativeNewStageLooks.LanternY,z=NativeNewStageLooks.LighthouseZ;var q=FactoryQ.Axis(0,1,0,a);
            Add(x,y-.9,z,.3,.25,.3,q,NativeNewStageLooks.Dark);
            foreach(double side in Unit){double s=Math.Sin(a)*side,c=Math.Cos(a)*side;Add(x+s*.42,y,z+c*.42,.5,.6,.05,q,NativeNewStageLooks.Light,false);}
            Add(x,y,z,.14,.62,.14,q,NativeNewStageLooks.Light,false,0xFFF6E0);
        }
        private void Radar(double t)
        {
            double a=t*1.1,x=48,y=24.9,z=52;var q=FactoryQ.Axis(0,1,0,a);Add(x,y,z,.18,.18,.18,q,NativeNewStageLooks.Dark);Add(x,y+.3,z,1.6,.12,.08,q,NativeNewStageLooks.White);
        }
    }
}
