using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Factory's moving hazard rigs for one frame (FACTORY_PROPOSAL.md 3.8, 5.3), posed only from
    /// <see cref="NativeFactoryHazards.State"/> at the hazard clock, so host, guests and the killcam draw the same saw, ram and
    /// crane at the same moment, and the lamps that telegraph them (never reduced): slot lamps every 2 m along each slot
    /// (green when safe, amber strobing through the warning, red with a bright chase just ahead of the blade while it runs),
    /// each rail saw's wall lamp, each press's head lamps and gallery beacon, the crane's trolley beacon and the four corner
    /// lamps of the zone it serves (amber while it travels there, red strobing through the 3 s telegraph, solid red as the load
    /// falls). The blades are four turned square plates (sixteen teeth) on a dark hub, spun by the state's angle; the crane is
    /// the bridge, end trucks, trolley, hoist, hanging cab, cables, hook block, slings and the beam bundle (which casts no sun
    /// shadow while it hangs high: the sun's would land metres from the zone; the drop's own contact shadow is an effect).
    /// Fills a fixed buffer (<see cref="Capacity"/>): allocation free per frame.
    /// </summary>
    public sealed class NativeFactoryRig
    {
        public const int Capacity=1400;
        public readonly NativeFactoryKit.Writer Parts;
        private readonly NativeFactoryKit.Looks looks;
        /// <summary>Lamp colours (sRGB): off, safe green, amber, red, and the bright chase.</summary>
        public const int LampOff=0x1A120A,LampSafe=0x1F7A3A,LampAmber=0xFFB020,LampRed=0xC81E14,LampChase=0xFF4A30;
        /// <summary>Strobe rates (Hz): the warning's amber, the zone's red, the chase.</summary>
        public const double WarnHz=6,ZoneHz=8,ChaseHz=10;
        private static readonly double[] Unit={-1,1},P12={-.12,.12},P18={-.18,.18},P28={-.28,.28},P60={-.6,.6},P120={-1.2,1.2};
        public NativeFactoryRig(NativeFactoryKit.Looks looks){this.looks=looks;Parts=new NativeFactoryKit.Writer(Capacity,false,looks);}
        public static bool Strobe(double clock,double hz)=>!double.IsInfinity(clock)&&(clock*hz-Math.Floor(clock*hz))<.5;
        /// <summary>The colour of a slot lamp at fraction `s` along the slot for this state and clock.</summary>
        public static int SlotLamp(in FactoryHazardState st,double s,double length,double clock)
        {
            switch(st.Phase)
            {
                case NativeFactoryHazards.Warning:return Strobe(clock,WarnHz)?LampAmber:LampOff;
                case NativeFactoryHazards.Rising:case NativeFactoryHazards.Running:case NativeFactoryHazards.Sinking:
                {double ahead=(st.Reverse?st.U-s:s-st.U)*length;return ahead>-1&&ahead<3.5?(Strobe(clock,ChaseHz)?LampChase:LampRed):LampRed;}
                default:return LampSafe;
            }
        }
        /// <summary>A hazard's own lamp (rail saw, press, crane beacon): safe green, amber strobing while warning, red while live.</summary>
        public static int HazardLamp(in FactoryHazardState st,double clock)
        {
            switch(st.Phase)
            {
                case NativeFactoryHazards.Parked:return LampSafe;
                case NativeFactoryHazards.Warning:return Strobe(clock,WarnHz)?LampAmber:LampOff;
                case NativeFactoryHazards.Travelling:return Strobe(clock,2)?LampAmber:LampOff;
                case NativeFactoryHazards.Landed:case NativeFactoryHazards.Regrip:case NativeFactoryHazards.Hoisting:return LampAmber;
                default:return LampRed;
            }
        }
        /// <summary>A drop zone's corner lamps: dark unless the crane serves it; amber travelling there, red strobing over it, red falling.</summary>
        public static int ZoneLamp(in FactoryHazardState st,int zone,double clock)
        {
            if(st.Zone!=zone)return LampOff;
            switch(st.Phase)
            {
                case NativeFactoryHazards.Travelling:return Strobe(clock,2)?LampAmber:LampOff;
                case NativeFactoryHazards.Warning:return Strobe(clock,ZoneHz)?LampRed:LampOff;
                case NativeFactoryHazards.Falling:return LampChase;
                case NativeFactoryHazards.Landed:case NativeFactoryHazards.Regrip:return LampAmber;
                default:return LampOff;
            }
        }

        /// <summary>Poses every hazard at hazard clock `clock` in round `round` into <see cref="Parts"/>. `blood`: each hazard's
        /// last kill (the hazard clock a saw cut a pilot this round, NaN none; the presentation's Core NativeSawBisect): that
        /// blade carries blood from then on (null: none).</summary>
        public void Build(FactoryHazard[] hazards,int round,double clock,double[] blood=null)
        {
            Parts.Clear();clockNow=clock;if(hazards==null)return;
            for(int i=0;i<hazards.Length;i++)
            {
                var h=hazards[i];if(h==null)continue;var st=NativeFactoryHazards.State(h,round,clock);kill=blood!=null&&i<blood.Length?blood[i]:double.NaN;
                switch(h.kind)
                {
                    case NativeFactoryHazards.SawSlot:Slot(h,st,clock);break;case NativeFactoryHazards.SawRail:Rail(h,st,clock);break;
                    case NativeFactoryHazards.Press:PressRig(h,st,clock);break;case NativeFactoryHazards.Crane:CraneRig(h,st,clock);break;
                }
            }
        }
        // A blade: four square plates turned by 22.5° (sixteen teeth at the radius) and a two-plate hub, about the axle.
        private void Blade(FactoryHazard h,double cx,double cy,double cz,double spin,bool shadow)
        {
            var w=Parts;double half=h.radius/Math.Sqrt(2),t=.025;bool alongX=Math.Abs(h.axis.x)>.5;
            FactoryQ basis=alongX?FactoryQ.Axis(0,1,0,Math.PI/2):FactoryQ.Identity;
            for(int k=0;k<4;k++)w.Add(cx,cy,cz,half,half,t,basis*FactoryQ.Axis(0,0,1,spin+k*Math.PI/8),NativeFactoryKit.Blade,shadow,-1,1,h.a.x,h.a.y,h.a.z);
            for(int k=0;k<2;k++)w.Add(cx,cy,cz,.17,.17,t+.02,basis*FactoryQ.Axis(0,0,1,spin+k*Math.PI/4),NativeFactoryKit.Hub,shadow,-1,1,h.a.x,h.a.y,h.a.z);
            Smears(h,cx,cy,cz,spin,t,alongX,basis);
        }
        private double kill=double.NaN,clockNow;
        /// <summary>Wet blood fresh off a cut darkening as it dries (sRGB), and how long it takes (s).</summary>
        public const int BloodWet=0x7a0912,BloodDry=0x34060a;public const double BloodDries=24;
        /// <summary>
        /// A blade that has cut a pilot this round keeps the blood (owner 2026-10-12, the saw split): seven smears over both
        /// faces at hashed radii, turning with the blade, wet at the kill and drying dark. Nothing before the kill.
        /// </summary>
        private void Smears(FactoryHazard h,double cx,double cy,double cz,double spin,double t,bool alongX,FactoryQ basis)
        {
            if(double.IsNaN(kill)||!(clockNow>=kill))return;double dry=Math.Min(1,(clockNow-kill)/BloodDries);int col=Mix(BloodWet,BloodDry,dry);
            for(int k=0;k<7;k++)
            {
                double f=((k*0x9e3779b1u)>>8&0xffff)/65536.0,r=h.radius*(.32+.56*f),a=spin+k*2.39996,len=.05+.09*(((k*0x85ebca77u)>>8&0xffff)/65536.0);
                double u=r*Math.Cos(a),v=r*Math.Sin(a),x=alongX?0:u,z=alongX?-u:0;
                Parts.Add(cx+x,cy+v,cz+z,len,.014+.01*f,t+.004,basis*FactoryQ.Axis(0,0,1,a),NativeFactoryKit.Red,false,col);
            }
        }
        private static int Mix(int a,int b,double t){int C(int s)=>(int)Math.Round(((a>>s)&255)+(((b>>s)&255)-((a>>s)&255))*t);return C(16)<<16|C(8)<<8|C(0);}
        private void Slot(FactoryHazard h,in FactoryHazardState st,double clock)
        {
            var w=Parts;if(st.Lift>0)Blade(h,st.Center.x,st.Center.y,st.Center.z,st.Spin,true);
            bool alongX=Math.Abs(h.b.x-h.a.x)>Math.Abs(h.b.z-h.a.z);double s0=alongX?h.a.x:h.a.z,s1=alongX?h.b.x:h.b.z,c=alongX?h.a.z:h.a.x,len=Math.Abs(s1-s0);int n=(int)Math.Floor(len/2)+1;
            for(int i=0;i<n;i++)
            {
                double f=n>1?(double)i/(n-1):0,s=s0+(s1-s0)*f;int col=SlotLamp(st,f,len,clock);
                foreach(double side in P28){if(alongX)w.Box(s-.07,s+.07,0,.03,c+side-.04,c+side+.04,NativeFactoryKit.Lamp,false,col);else w.Box(c+side-.04,c+side+.04,0,.03,s-.07,s+.07,NativeFactoryKit.Lamp,false,col);}
            }
        }
        private void Rail(FactoryHazard h,in FactoryHazardState st,double clock)
        {
            var w=Parts;double x=st.Center.x,y=st.Center.y,z=st.Center.z,track=h.a.y+h.radius+.45;
            w.Box(x-.16,x+.16,track-.4,track-.15,z-.28,z+.28,NativeFactoryKit.CraneDark);w.Box(x-.05,x+.05,y+.1,track-.4,z-.05,z+.05,NativeFactoryKit.CraneDark);
            w.Box(x-.07,x+.07,y+h.radius*.15,y+h.radius+.12,z-h.radius-.05,z+h.radius+.05,NativeFactoryKit.Stripe);
            w.Box(x+.08,x+.32,y-.12,y+.12,z-.16,z+.16,NativeFactoryKit.Motor);
            Blade(h,x-.12,y,z,st.Spin,true);
            double dir=Math.Sign(h.b.z-h.a.z),face=h.a.z+dir*.6;w.Box(x-.15,x+.15,h.a.y+h.radius+.26,h.a.y+h.radius+.46,Math.Min(face,face+dir*.08),Math.Max(face,face+dir*.08),NativeFactoryKit.Lamp,false,HazardLamp(st,clock));
        }
        private void PressRig(FactoryHazard h,in FactoryHazardState st,double clock)
        {
            var w=Parts;double x=st.Center.x,y=st.Center.y,z=st.Center.z,hx=h.size.x/2,hy=h.size.y/2,hz=h.size.z/2,head=h.a.y+hy;
            w.Add(x,y,z,hx,hy,hz,FactoryQ.Identity,NativeFactoryKit.Ram,true,-1,1,h.a.x,h.a.y,h.a.z);
            foreach(double s in Unit){w.Box(x-hx,x+hx,y-hy,y-hy+.28,z+s*hz-(s<0?.03:0),z+s*hz+(s>0?.03:0),NativeFactoryKit.Stripe,false);
                foreach(double ux in Unit)w.Box(x+ux*hx*.7-.05,x+ux*hx*.7+.05,y+hy,Math.Max(y+hy+.01,head+.25),z+s*hz*.5-.05,z+s*hz*.5+.05,NativeFactoryKit.Chrome);}
            int lamp=HazardLamp(st,clock);foreach(double s in Unit)w.Box(x-.18,x+.18,head+.2,head+.34,z+s*(hz+.06)-.025,z+s*(hz+.06)+.025,NativeFactoryKit.Lamp,false,lamp);
            double post=z-1.7*Math.Sign(z);w.Box(x-.09,x+.09,4.75,4.93,post-.09,post+.09,NativeFactoryKit.Lamp,false,lamp);
        }
        private void CraneRig(FactoryHazard h,in FactoryHazardState st,double clock)
        {
            var w=Parts;double X=st.X,Z=st.Z,rail=h.a.y+.14,span=10.9,hookY=st.HookY;
            // bridge: end trucks on the runway rails, two box girders, a walkway, the hanging cab at the -z end
            foreach(double s in Unit)
            {
                double ez=s*10.7;w.Box(X-1.7,X+1.7,rail,rail+.56,ez-.3,ez+.3,NativeFactoryKit.CraneDark);w.Box(X-1.75,X-1.45,rail+.05,rail+.5,ez-.32,ez+.32,NativeFactoryKit.Stripe);w.Box(X+1.45,X+1.75,rail+.05,rail+.5,ez-.32,ez+.32,NativeFactoryKit.Stripe);
                foreach(double dx in P120)w.Add(X+dx,rail+.18,ez,.22,.22,.33,FactoryQ.Axis(0,0,1,Math.PI/4),NativeFactoryKit.Wheel,false);
                w.Box(X+s*.95-.23,X+s*.95+.23,rail+.56,rail+1.76,-span,span,NativeFactoryKit.CraneBody);
                w.Box(X+s*.95-.24,X+s*.95+.24,rail+.56,rail+1.76,s*span-(s>0?.5:0),s*span+(s<0?.5:0),NativeFactoryKit.Stripe);
            }
            w.Box(X+1.18,X+1.9,rail+.56,rail+.62,-span+.6,span-.6,NativeFactoryKit.Grate,false);for(double z=-span+.6;z<=span-.6;z+=1.8)w.Box(X+1.86,X+1.9,rail+.62,rail+1.62,z-.02,z+.02,NativeFactoryKit.Rail,false);
            w.Box(X+1.86,X+1.9,rail+1.58,rail+1.62,-span+.6,span-.6,NativeFactoryKit.Rail,false);
            double cabZ=-9.4,cy0=rail-1.9,cy1=rail-.35;w.Box(X-.8,X+.8,cy0,cy1,cabZ-.8,cabZ+.8,NativeFactoryKit.CraneBody);w.Box(X-.82,X+.82,cy0+.55,cy1-.15,cabZ+.78,cabZ+.82,NativeFactoryKit.CabGlass,false,-1,.3);
            w.Box(X-.82,X-.78,cy0+.55,cy1-.15,cabZ-.6,cabZ+.6,NativeFactoryKit.CabGlass,false,-1,.3);w.Box(X+.78,X+.82,cy0+.55,cy1-.15,cabZ-.6,cabZ+.6,NativeFactoryKit.CabGlass,false,-1,.3);
            foreach(double dx in P60)w.Box(X+dx-.05,X+dx+.05,cy1,rail+.56,cabZ-.05,cabZ+.05,NativeFactoryKit.CraneDark);
            // trolley with hoist, motor and beacon
            double ty=rail+1.76;w.Box(X-1.3,X+1.3,ty,ty+.42,Z-1.1,Z+1.1,NativeFactoryKit.CraneDark);w.Box(X-.8,X+.8,ty+.42,ty+1.1,Z-.35,Z+.35,NativeFactoryKit.CraneBody);
            w.Box(X+.85,X+1.3,ty+.42,ty+.95,Z-.3,Z+.3,NativeFactoryKit.Motor);w.Box(X-1.3,X-.85,ty+.42,ty+.85,Z-.4,Z+.4,NativeFactoryKit.Panel);
            w.Box(X-.1,X+.1,ty+1.1,ty+1.32,Z-.1,Z+.1,NativeFactoryKit.Lamp,false,HazardLamp(st,clock));
            // cables to the hook block, the block and hook (shadowless while high: the drop's contact shadow is the telegraph)
            bool high=hookY>4;foreach(double dx in P18)foreach(double dz in P12)w.Box(X+dx-.018,X+dx+.018,hookY+1.15,ty,Z+dz-.018,Z+dz+.018,NativeFactoryKit.Cable,false);
            w.Box(X-.28,X+.28,hookY+.35,hookY+1.15,Z-.18,Z+.18,NativeFactoryKit.Hook,!high);w.Box(X-.29,X+.29,hookY+.35,hookY+.55,Z-.19,Z+.19,NativeFactoryKit.Stripe,false);w.Box(X-.05,X+.05,hookY,hookY+.35,Z-.06,Z+.06,NativeFactoryKit.CraneDark,false);
            // the load: three I-beams strapped, hanging (with sway about the hook) or falling or landed at its zone
            double lx=X,lz=Z,ly=st.LoadY,top=h.size.y;bool loadHigh=ly>2.5;
            FactoryQ sway=FactoryQ.Axis(1,0,0,st.Sway);double pivotY=hookY;
            void Piece(double px,double py,double pz,double hx,double hy,double hz,int kind,bool shadow)
            {
                double dx=px-lx,dy=py-pivotY,dz=pz-lz;sway.Rotate(dx,dy,dz,out double ox,out double oy,out double oz);
                w.Add(lx+ox,pivotY+oy,lz+oz,hx,hy,hz,sway,kind,shadow,-1,1,px-lx+h.a.x,py-ly,pz-lz);
            }
            double bw=h.size.x/3;bool cast=!loadHigh;
            for(int k=0;k<3;k++){double bx=lx-h.size.x/2+bw*(k+.5);Piece(bx,ly+.04,lz,bw*.48,.04,h.size.z/2,NativeFactoryKit.Load,cast);Piece(bx,ly+top-.04,lz,bw*.48,.04,h.size.z/2,NativeFactoryKit.Load,cast);Piece(bx,ly+top/2,lz,.035,top/2-.08,h.size.z/2,NativeFactoryKit.Load,cast);}
            foreach(double uz in Unit)Piece(lx,ly+top/2,lz+uz*h.size.z*.3,h.size.x/2+.02,top/2+.02,.05,NativeFactoryKit.Strap,false);
            if(st.Attached)foreach(double ux in Unit)foreach(double uz in Unit)
            {double dx=ux*h.size.x*.4,dz=uz*h.size.z*.4,ax=lx+dx,az=lz+dz,ay=ly+top;double ddx=ax-lx,ddy=ay-pivotY,ddz=az-lz;sway.Rotate(ddx,ddy,ddz,out double ox,out double oy,out double oz);w.Bar(X,hookY,Z,lx+ox,pivotY+oy,lz+oz,.04,.04,NativeFactoryKit.Strap,false);}
            else foreach(double dx in P12)w.Box(X+dx-.02,X+dx+.02,hookY-1.0,hookY,Z-.02,Z+.02,NativeFactoryKit.Strap,false);
            // zone corner lamps
            for(int k=0;k<(h.zones?.Length??0);k++)
            {
                var zn=h.zones[k];double hx=h.size.x/2+.5,hz=h.size.z/2+.5;int col=ZoneLamp(st,k,clock);
                foreach(double sx in Unit)foreach(double sz in Unit){double px=zn.center.x+sx*(hx+.08),pz=zn.center.z+sz*(hz+.08);w.Box(px-.1,px+.1,0,.05,pz-.1,pz+.1,NativeFactoryKit.Lamp,false,col);}
            }
        }
    }
}
