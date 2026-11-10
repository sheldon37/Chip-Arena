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
    /// height. The one the crane serves creaks and swings ever wider through the warning (its crown slipping twice on the chain),
    /// drops on the simulation's own fall curve, lies broken in its ring as the landed load (the collider's height), and the
    /// winch takes it up again on the hoist. The others hang, barely stirring.</item>
    /// <item>The Thurible: the great silver censer on its rope from the oculus's yoke, drawn back over the south arch by its
    /// pull rope while parked, released at the end of the warning, its pendulum at the simulation's point along the path.</item>
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
        private static readonly int[] Sides={-1,1};private static readonly double[] BannerX={12.0,18.0,24.0};
        public NativeSanctumRig(NativeFactoryKit.Looks looks){Parts=new NativeFactoryKit.Writer(Capacity,false,looks);}
        private void Add(double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=true,int color=-1,double window=1)=>Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow,color,window);
        private void Box(double x0,double x1,double y0,double y1,double z0,double z1,int kind,bool shadow=true,int color=-1,double window=1)=>Parts.Box(Math.Min(x0,x1),Math.Max(x0,x1),Math.Min(y0,y1),Math.Max(y0,y1),Math.Min(z0,z1),Math.Max(z0,z1),kind,shadow,color,window);
        private void Bar(double x0,double y0,double z0,double x1,double y1,double z1,double t,int kind,bool shadow=true,int color=-1)=>Parts.Bar(x0,y0,z0,x1,y1,z1,t,t,kind,shadow,color);
        /// <summary>The chain's anchor in the vault over each chandelier.</summary>
        public const double Anchor=20.7;

        public void Build(MapHazard[] hazards,int round,NativePhase phase,double fightAt,double now,double render)
        {
            Parts.Clear();BasketCount=0;CenserGlow=Bell=Gate=0;double clock=NativeFactoryHazards.Clock(phase,fightAt,now);bool gate=false;
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
        /// <summary>The served chandelier's swing through the warning (rad): growing from a shiver to 0.16 rad, quickening; zero
        /// before and after. A pure function of the warning's progress and the clock.</summary>
        public static double Sway(double warn,double clock)=>warn<=0||warn>=1?0:(.012+.15*warn*warn)*Math.Sin(clock*(3.2+3.4*warn))*Math.Min(1,(1-warn)*6+.35);
        private void ChandelierSet(MapHazard h,in FactoryHazardState st,double clock,double render)
        {
            for(int k=0;k<Math.Min(Chandeliers,h.zones?.Length??0);k++)
            {
                var z=h.zones[k];double x=z.center.x,zz=z.center.z,floor=z.center.y,rest=h.carry;int phase=0;double y=rest,sway=.004*Math.Sin(render*.7+k*1.9),slip=0,since=0;
                if(st.Zone==k)
                {
                    switch(st.Phase)
                    {
                        case NativeFactoryHazards.Warning:phase=1;sway=Sway(st.Warn,clock);slip=st.Warn>.45?(st.Warn>.8?.14:.07):0;break;
                        case NativeFactoryHazards.Falling:phase=2;y=st.LoadY;sway=0;break;
                        case NativeFactoryHazards.Landed:case NativeFactoryHazards.Regrip:phase=3;y=floor;sway=0;since=clock-st.ImpactAt;break;
                        case NativeFactoryHazards.Hoisting:phase=4;y=st.LoadY;sway=.01*Math.Sin(clock*2.1);since=(st.LoadY-floor)/Math.Max(.1,rest-floor);break;
                    }
                }
                Chandelier(k,x,y-slip,zz,sway,phase,floor,since,render);
            }
        }
        private void Chandelier(int k,double x,double y,double z,double sway,int phase,double floor,double age,double render)
        {
            int I=NativeSanctumLooks.Iron,G=NativeSanctumLooks.Gold,W=NativeSanctumLooks.Wax,C=NativeSanctumLooks.Chain;double R=1.4;bool down=phase==3,falling=phase==2;
            // the pendulum: every part turns about the chain's anchor by the sway (about z: it swings across the nave's axis)
            // fallen, the ring lies askew on its crushed crown (about the ring's own centre, inside the landed load's box)
            var q=FactoryQ.Axis(0,0,1,sway);double px=x,py=Anchor;
            if(down){double ta=(NativeSanctumLooks.Hash(k,9,2)-.5)*2.4;q=FactoryQ.Axis(Math.Cos(ta),0,Math.Sin(ta),.11);py=y+.3;}
            void P(double lx,double ly,double lz,out double wx,out double wy,out double wz){q.Rotate(lx-px,ly-py,lz-z,out double ox,out double oy,out double oz);wx=px+ox;wy=py+oy;wz=z+oz;}
            void Part(double lx,double ly,double lz,double hx,double hy,double hz,FactoryQ r,int kind,bool shadow=true,int color=-1,double window=1){P(lx,ly,lz,out double wx,out double wy,out double wz);Add(wx,wy,wz,hx,hy,hz,q*r,kind,shadow,color,window);}
            void Seg(double ax,double ay,double az,double bx,double by,double bz,double t,int kind,bool shadow=true){P(ax,ay,az,out double x0,out double y0,out double z0);P(bx,by,bz,out double x1,out double y1,out double z1);Bar(x0,y0,z0,x1,y1,z1,t,kind,shadow);}
            bool cast=y<6;
            // the hoop (two rings), the drip pans and candles round it, four arms to the hub, the hub and its finial, the crown ring
            double crown=down?.6:NativeSanctumLooks.CrownHeight,hub=y+crown;
            for(int i=0;i<24;i++){double a0=2*Math.PI*i/24,a1=2*Math.PI*(i+1)/24;Seg(x+Math.Cos(a0)*R,y+.12,z+Math.Sin(a0)*R,x+Math.Cos(a1)*R,y+.12,z+Math.Sin(a1)*R,.11,I,cast);Seg(x+Math.Cos(a0)*(R-.16),y+.04,z+Math.Sin(a0)*(R-.16),x+Math.Cos(a1)*(R-.16),y+.04,z+Math.Sin(a1)*(R-.16),.05,G,false);}
            int n=NativeSanctumRig.ChandelierCandles;
            for(int i=0;i<n;i++)
            {
                double a=2*Math.PI*(i+.5)/n,cx=x+Math.Cos(a)*R,cz=z+Math.Sin(a)*R;Part(cx,y+.2,cz,.09,.02,.09,FactoryQ.Axis(0,1,0,a),I,false);
                // after the fall a few candles stand, most lie scattered round the ring
                double h=NativeSanctumLooks.Hash(k,i,17);bool standing=!down||h<.35;double tall=.16+.1*NativeSanctumLooks.Hash(k,i,3);
                int o=(k*n+i)*4;
                if(standing){Part(cx,y+.22+tall/2,cz,.035,tall/2,.035,FactoryQ.Identity,W,false);P(cx,y+.24+tall,cz,out Wicks[o],out Wicks[o+1],out Wicks[o+2]);Wicks[o+3]=down?Math.Max(0,1-age/3.0):falling?.55:phase==4?Math.Min(1,age*1.4):1;}
                else{double sa=a+(h-.5)*2.2,sr=R+.4+1.3*h;double lx=x+Math.Cos(sa)*sr,lz=z+Math.Sin(sa)*sr;Add(lx,floor+.04,lz,.035,.035,tall/2,FactoryQ.Axis(0,1,0,sa*1.7),W,false);Wicks[o]=lx;Wicks[o+1]=floor+.06;Wicks[o+2]=lz;Wicks[o+3]=h<.55?Math.Max(0,.8-age/4.0):0;}
            }
            for(int i=0;i<4;i++)
            {
                double a=Math.PI/2*i+Math.PI/4,ex=x+Math.Cos(a)*(R-.05),ez=z+Math.Sin(a)*(R-.05),mx=x+Math.Cos(a)*R*.55,mz=z+Math.Sin(a)*R*.55;
                if(!down){Seg(ex,y+.12,ez,mx,y+crown*.55,mz,.07,I);Seg(mx,y+crown*.55,mz,x,hub,z,.06,I);}
                else{Seg(ex,y+.12,ez,mx,y+.3,mz,.07,I);Seg(mx,y+.3,mz,x+Math.Cos(a+.4)*.3,y+.55,z+Math.Sin(a+.4)*.3,.06,I);}
            }
            Part(x,hub,z,.18,.22,.18,FactoryQ.Axis(0,1,0,Math.PI/4),I);Part(x,hub+.3,z,.07,.12,.07,FactoryQ.Identity,G);
            for(int i=0;i<12;i++){double a0=2*Math.PI*i/12,a1=2*Math.PI*(i+1)/12;Seg(x+Math.Cos(a0)*.55,hub-.05,z+Math.Sin(a0)*.55,x+Math.Cos(a1)*.55,hub-.05,z+Math.Sin(a1)*.55,.05,G,false);}
            // the chain: from the hub to the anchor while it hangs; snapped while it falls and lies (the upper length whips back up)
            if(phase==0||phase==1||phase==4)Seg(x,hub+.4,z,x,Anchor,z,.06,C,false);
            else{double tail=Math.Min(Anchor-.5,hub+.4+Math.Max(0,6-age*5));Bar(x,Anchor,z,x+Math.Sin(age*9)*.12,tail,z,.06,C,false);}
            Box(x-.2,x+.2,Anchor,Anchor+.3,z-.2,z+.2,I);
            int o5=k*5;Ring[o5]=x;Ring[o5+1]=y+.12;Ring[o5+2]=z;Ring[o5+3]=sway;Ring[o5+4]=phase;
        }

        // ---------------- the censer ----------------
        /// <summary>The censer's centre on its pendulum: the simulation's z along the path, the arc's height for its rope.</summary>
        public static void CenserPoint(MapHazard h,in FactoryHazardState st,out double x,out double y,out double z,out double angle)
        {
            double L=NativeSanctumLooks.CenserPivotY-h.a.y;z=st.Center.z;x=st.Center.x;double s=Math.Max(-.95,Math.Min(.95,z/L));angle=Math.Asin(s);y=NativeSanctumLooks.CenserPivotY-L*Math.Cos(angle);
        }
        private void Censer(MapHazard h,in FactoryHazardState st,int round,double clock,double render)
        {
            CenserPoint(h,st,out double x,out double y,out double z,out double ang);int S=NativeSanctumLooks.Silver,G=NativeSanctumLooks.Gold,E=NativeSanctumLooks.Ember,R=NativeSanctumLooks.Rope,I=NativeSanctumLooks.Iron;
            bool parked=st.Phase==NativeFactoryHazards.Parked,warn=st.Phase==NativeFactoryHazards.Warning;
            // drawn back: it trembles at its pull rope through the warning
            double tremble=warn?.01*Math.Sin(clock*31)*(.4+st.Warn):0;ang+=tremble;
            double glow=parked?.35:warn?.35+.65*st.Warn:1;CenserGlow=glow;CenserX=x;CenserY=y;CenserZ=z;
            double py=NativeSanctumLooks.CenserPivotY,L=py-h.a.y;
            var q=FactoryQ.Axis(1,0,0,-ang);
            void P(double ly,out double wx,out double wy,out double wz){q.Rotate(0,ly,0,out double ox,out double oy,out double oz);wx=x+ox;wy=py-L*Math.Cos(ang)+oy;wz=Math.Sin(ang)*L+oz;}
            void Part(double ly,double hx,double hy,double hz,FactoryQ r,int kind,bool shadow=true,double window=1){P(ly,out double wx,out double wy,out double wz);Add(wx,wy,wz,hx,hy,hz,q*r,kind,shadow,-1,window);}
            // the bowl in eight-sided tiers (foot, belly, the gold band), the coals glowing through the gaps of the pierced lid's
            // tiers, the finial, the six chains to the crown ring, the rope to the yoke
            void Tier(double ly,double rad,double hy,int kind,double gap){for(int i=0;i<8;i++){double a=Math.PI/4*i+Math.PI/8;var r=FactoryQ.Axis(0,1,0,a);r.Rotate(0,0,rad,out double ox,out _,out double oz);P(ly,out double wx,out double wy,out double wz);Add(wx+ox,wy,wz+oz,rad*.4142*(1-gap)+.01,hy,.05,q*r,kind,true);}}
            Part(-.86,.22,.05,.22,FactoryQ.Axis(0,1,0,Math.PI/8),G);Tier(-.72,.32,.1,S,0);Tier(-.42,.5,.2,S,0);Tier(-.12,.56,.1,S,0);Tier(.0,.58,.04,G,0);
            Part(.02,.5,.06,.5,FactoryQ.Axis(0,1,0,Math.PI/8),E,false,1);
            Tier(.2,.5,.1,S,.3);Tier(.4,.38,.09,S,.3);Tier(.57,.25,.08,S,.2);Tier(.7,.13,.06,G,0);
            Part(.85,.06,.12,.06,FactoryQ.Axis(0,1,0,Math.PI/8),G);Part(1.02,.05,.05,.05,FactoryQ.Axis(0,1,0,Math.PI/4),G);
            P(2.4,out double rx,out double ry,out double rz);
            for(int i=0;i<6;i++){double a=Math.PI/3*i;var r=FactoryQ.Axis(0,1,0,a);r.Rotate(.6,0,0,out double ox,out _,out double oz);P(.16,out double bx,out double by,out double bz);Bar(bx+ox,by,bz+oz,rx,ry,rz,.035,NativeSanctumLooks.Chain,false);}
            Part(2.4,.2,.05,.2,FactoryQ.Identity,G);
            Bar(rx,ry,rz,0,py-.1,0,.07,R,false);
            // the pull rope to the south arch while it is held back
            if(parked||warn){Bar(x,y+.6,z,0,5.05,-7.02,.04,R,false);Box(-.12,.12,4.95,5.15,-7.1,-6.95,I);}
            var next=NativeFactoryHazards.State(h,round,clock+.02);CenserSpeed=(next.Center.z-st.Center.z)/.02;
        }

        // ---------------- holy fire ----------------
        /// <summary>A basket's heat (0 banked .. 1 white-hot) by the burner's phase.</summary>
        public static double Heat(in NativeHazardState s)=>s.Phase==NativeHazardPhase.Warn?.25+.55*s.Level:s.Phase==NativeHazardPhase.Active?1:s.Phase==NativeHazardPhase.Clear?.2+.6*s.Level:.12;
        private void FireBaskets(MapHazard h,in NativeHazardState s,double time)
        {
            int I=NativeSanctumLooks.Iron,C=NativeSanctumLooks.Coal,Ch=NativeSanctumLooks.Chain;double heat=Heat(s);double soffit=NativeSanctumLooks.Gallery-.4;
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
            Lamp=lamp;
        }

        // ---------------- ambient ----------------
        /// <summary>The nave's banners: crimson on the west half, blue on the east, one on each arcade pier's nave face, stirring.</summary>
        private void Banners(double t)
        {
            foreach(int s in Sides)foreach(double px in BannerX)foreach(int sz in Sides)
            {
                double x=s*px,z0=sz*5.3,face=-sz;int cloth=s<0?NativeSanctumLooks.ClothRed:NativeSanctumLooks.ClothBlue,G=NativeSanctumLooks.ClothGold,I=NativeSanctumLooks.Iron;
                double ang=.035*Math.Sin(t*.6+px*.7+sz)+.015*Math.Sin(t*1.7+px);var q=FactoryQ.Axis(0,0,1,ang);double top=15.4,len=5.6,zc=z0+face*.85;
                Box(x-.05,x+.05,top+.05,top+.15,z0,z0+face*1.7,I);
                void Part(double ly,double hy,double dz,double hz,int kind){q.Rotate(0,ly,0,out double ox,out double oy,out double oz);Add(x+ox,top+oy,zc+dz+oz,.012,hy,hz,q,kind,true);}
                Part(-len/2,len/2,0,.7,cloth);Part(-len/2,len/2-.1,0,.6,cloth);Part(-.12,.06,0,.74,G);Part(-len+.08,.08,0,.72,G);
                for(int i=0;i<3;i++){double a=-len*(.3+.2*i);Part(a,.16,0,.16,G);}
                q.Rotate(0,-len-.2,0,out double tx,out double ty,out double tz);Add(x+tx,top+ty,zc+tz,.02,.12,.12,q*FactoryQ.Axis(1,0,0,Math.PI/4),G,false);
            }
        }
    }
}
