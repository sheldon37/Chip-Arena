using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Training Room's hazard bay samples drawn for the eye (map `training` only; the Factory's own rigs,
    /// Core NativeFactoryRig, draw only the Factory and its crane is the hall's bridge crane). One sample of each kind the map
    /// authors, posed only from the simulation's pure clocks, so host, guests and the killcam see the same state:
    /// <see cref="NativeFactoryHazards.State"/> for the saw slot, press, vent and crane, <see cref="NativeCyberCityHazards.At"/>
    /// for the power grid. The Factory kit's part kinds and lamp rules (NativeFactoryKit, NativeFactoryRig's SlotLamp,
    /// HazardLamp, ZoneLamp) keep the samples reading like the real hazards:
    /// <list type="bullet">
    /// <item>Static fittings (<see cref="Fittings"/>, built once): the saw slot's void, edge plates, danger lines and end
    /// stripes; the vent's grate and striped collar; the press's floor stripes; the crane zone's wash and hatched frame; the
    /// grid's grating, striped frame and conductor bars.</item>
    /// <item>Moving rigs and lamps (<see cref="Build"/>, every frame into a fixed buffer): the slot's blade and lamps, the press
    /// ram with its guides and head lamps, the vent's corner lamps, the grid's lamps and live bars, the crane's monorail trolley
    /// riding the runway block with its hoist, cables, hook and the strapped beam bundle, its zone's corner lamps; and the
    /// strafing target's carriage under the dummy.</item>
    /// <item>Effects (<see cref="Effects"/>, every frame into a fixed quad buffer): lamp halos, the vent's steam, the saw's
    /// sparks, the grid's arcs, the crane zone's red wash and the load's contact shadow.</item>
    /// </list>
    /// Fixed capacities: allocation free per frame. Never sim data and never a collider.
    /// </summary>
    public sealed class NativeTrainingBay
    {
        public const int Capacity=900,FxCapacity=1200;
        public readonly NativeFactoryKit.Writer Parts;public readonly NativeFxQuads Quads=new NativeFxQuads(FxCapacity);
        private readonly NativeFactoryKit.Looks looks;
        private static readonly double[] Unit={-1,1},P28={-.28,.28},P42={-.42,.42},P12={-.12,.12},P08={-.08,.08};
        public NativeTrainingBay(NativeFactoryKit.Looks looks){this.looks=looks;Parts=new NativeFactoryKit.Writer(Capacity,false,looks);}
        public static bool Drawn(string kind)=>kind==NativeFactoryHazards.SawSlot||kind==NativeFactoryHazards.Press||kind==NativeFactoryHazards.Vent||kind==NativeFactoryHazards.Crane||kind=="grid";

        // ---------------- static fittings ----------------
        public static NativeFactoryKit.Writer Fittings(MapHazard[] hazards,NativeFactoryKit.Looks looks)
        {
            var w=new NativeFactoryKit.Writer(256,true,looks);
            foreach(var h in hazards??Array.Empty<MapHazard>())
            {
                if(h==null)continue;
                switch(h.kind)
                {
                    case NativeFactoryHazards.SawSlot:
                    {
                        bool alongX=Math.Abs(h.b.x-h.a.x)>Math.Abs(h.b.z-h.a.z);double s0=Math.Min(alongX?h.a.x:h.a.z,alongX?h.b.x:h.b.z)-h.radius-.2,s1=Math.Max(alongX?h.a.x:h.a.z,alongX?h.b.x:h.b.z)+h.radius+.2,c=alongX?h.a.z:h.a.x;
                        void Strip(double a0,double a1,double c0,double c1,double y0,double y1,int kind){if(alongX)w.Box(a0,a1,y0,y1,c-c1,c-c0,kind,false);else w.Box(c-c1,c-c0,y0,y1,a0,a1,kind,false);}
                        Strip(s0,s1,-.12,.12,0,.004,NativeFactoryKit.Void);Strip(s0,s1,.12,.34,0,.012,NativeFactoryKit.Plate);Strip(s0,s1,-.34,-.12,0,.012,NativeFactoryKit.Plate);
                        Strip(s0,s1,.64,.72,0,.007,NativeFactoryKit.Danger);Strip(s0,s1,-.72,-.64,0,.007,NativeFactoryKit.Danger);
                        foreach(double e in new[]{s0-1.2,s1})Strip(e,e+1.2,-.72,.72,0,.014,NativeFactoryKit.Stripe);
                        break;
                    }
                    case NativeFactoryHazards.Vent:
                    {
                        double hx=h.size.x/2-.1,hz=h.size.z/2-.1;w.Box(h.a.x-hx,h.a.x+hx,0,.02,h.a.z-hz,h.a.z+hz,NativeFactoryKit.Grate,false);
                        foreach(double s in Unit){w.Box(h.a.x-hx-.18,h.a.x+hx+.18,0,.012,h.a.z+s*hz,h.a.z+s*(hz+.18),NativeFactoryKit.Stripe,false);w.Box(h.a.x+s*hx,h.a.x+s*(hx+.18),0,.012,h.a.z-hz,h.a.z+hz,NativeFactoryKit.Stripe,false);}
                        break;
                    }
                    case NativeFactoryHazards.Press:
                    {
                        double hx=h.size.x/2+.25,hz=h.size.z/2+.6;foreach(double s in Unit)w.Box(h.a.x-hx,h.a.x+hx,0,.008,h.a.z+s*hz-.15,h.a.z+s*hz+.15,NativeFactoryKit.Stripe,false);
                        break;
                    }
                    case NativeFactoryHazards.Crane:
                    {
                        for(int k=0;k<(h.zones?.Length??0);k++)
                        {
                            var z=h.zones[k];double cx=z.center.x,cz=z.center.z,hx=h.size.x/2+.5,hz=h.size.z/2+.5;
                            w.Box(cx-hx,cx+hx,0,.004,cz-hz,cz+hz,NativeFactoryKit.PaintZone,false);
                            foreach(double s in Unit){w.Box(cx-hx,cx+hx,0,.009,cz+s*hz-(s>0?.35:0),cz+s*hz+(s<0?.35:0),NativeFactoryKit.Stripe,false);w.Box(cx+s*hx-(s>0?.35:0),cx+s*hx+(s<0?.35:0),0,.009,cz-hz,cz+hz,NativeFactoryKit.Stripe,false);}
                        }
                        break;
                    }
                    case "grid":
                    {
                        double cx=h.center.x,cz=h.center.z,hx=h.size.x/2,hz=h.size.z/2;
                        w.Box(cx-hx+.12,cx+hx-.12,0,.016,cz-hz+.12,cz+hz-.12,NativeFactoryKit.Grate,false);
                        foreach(double s in Unit){w.Box(cx-hx,cx+hx,0,.012,cz+s*hz-(s>0?.12:0),cz+s*hz+(s<0?.12:0),NativeFactoryKit.Stripe,false);w.Box(cx+s*hx-(s>0?.12:0),cx+s*hx+(s<0?.12:0),0,.012,cz-hz,cz+hz,NativeFactoryKit.Stripe,false);}
                        break;
                    }
                }
            }
            return w;
        }

        // ---------------- the moving rigs ----------------
        /// <summary>Poses every sample at the drawn arena's clock: `round` the stage round (NativeStructureFx.RoundIndex),
        /// `clock` the factory hazard clock (NativeFactoryHazards.Clock), `phase`/`fightAt`/`time` for the grid; `carriage` the
        /// strafing target's floor position (NaN: none).</summary>
        public void Build(MapHazard[] hazards,int round,double clock,NativePhase phase,double fightAt,double time,double carriageX=double.NaN,double carriageZ=double.NaN)
        {
            Parts.Clear();
            if(!double.IsNaN(carriageX))Carriage(carriageX,carriageZ,time);
            if(hazards==null)return;
            foreach(var h in hazards)
            {
                if(h==null)continue;
                if(h.kind=="grid"){GridRig(h,NativeCyberCityHazards.At(h,phase,round,fightAt,time),time);continue;}
                var st=NativeFactoryHazards.State(h,round,clock);
                switch(h.kind)
                {
                    case NativeFactoryHazards.SawSlot:SlotRig(h,st,clock);break;case NativeFactoryHazards.Press:PressRig(h,st,clock);break;
                    case NativeFactoryHazards.Vent:VentRig(h,st,clock);break;case NativeFactoryHazards.Crane:CraneRig(h,st,clock);break;
                }
            }
        }
        // The strafing target's carriage: a low plate under the dummy riding its rail, lit at both ends.
        private void Carriage(double x,double z,double time)
        {
            var w=Parts;w.Box(x-.42,x+.42,0,.024,z-.36,z+.36,NativeFactoryKit.CraneDark,false);
            foreach(double s in Unit)w.Box(x-.3,x+.3,.024,.03,z+s*.36-.03,z+s*.36+.03,NativeFactoryKit.Lamp,false,NativeTrainingLook.Magenta);
        }
        private void Blade(FactoryHazard h,double cx,double cy,double cz,double spin)
        {
            var w=Parts;double half=h.radius/Math.Sqrt(2),t=.025;bool alongX=Math.Abs(h.axis.x)>.5;
            FactoryQ basis=alongX?FactoryQ.Axis(0,1,0,Math.PI/2):FactoryQ.Identity;
            for(int k=0;k<4;k++)w.Add(cx,cy,cz,half,half,t,basis*FactoryQ.Axis(0,0,1,spin+k*Math.PI/8),NativeFactoryKit.Blade,true,-1,1,h.a.x,h.a.y,h.a.z);
            for(int k=0;k<2;k++)w.Add(cx,cy,cz,.17,.17,t+.02,basis*FactoryQ.Axis(0,0,1,spin+k*Math.PI/4),NativeFactoryKit.Hub,true,-1,1,h.a.x,h.a.y,h.a.z);
        }
        private void SlotRig(FactoryHazard h,in FactoryHazardState st,double clock)
        {
            var w=Parts;if(st.Lift>0)Blade(h,st.Center.x,st.Center.y,st.Center.z,st.Spin);
            bool alongX=Math.Abs(h.b.x-h.a.x)>Math.Abs(h.b.z-h.a.z);double s0=alongX?h.a.x:h.a.z,s1=alongX?h.b.x:h.b.z,c=alongX?h.a.z:h.a.x,len=Math.Abs(s1-s0);int n=(int)Math.Floor(len/2)+1;
            for(int i=0;i<n;i++)
            {
                double f=n>1?(double)i/(n-1):0,s=s0+(s1-s0)*f;int col=NativeFactoryRig.SlotLamp(st,f,len,clock);
                foreach(double side in P28){if(alongX)w.Box(s-.07,s+.07,0,.03,c+side-.04,c+side+.04,NativeFactoryKit.Lamp,false,col);else w.Box(c+side-.04,c+side+.04,0,.03,s-.07,s+.07,NativeFactoryKit.Lamp,false,col);}
            }
        }
        private void PressRig(FactoryHazard h,in FactoryHazardState st,double clock)
        {
            var w=Parts;double x=st.Center.x,y=st.Center.y,z=st.Center.z,hx=h.size.x/2,hy=h.size.y/2,hz=h.size.z/2,head=h.a.y+hy;
            w.Add(x,y,z,hx,hy,hz,FactoryQ.Identity,NativeFactoryKit.Ram,true,-1,1,h.a.x,h.a.y,h.a.z);
            foreach(double s in Unit)
            {
                w.Box(x-hx,x+hx,y-hy,y-hy+.28,z+s*hz-(s<0?.03:0),z+s*hz+(s>0?.03:0),NativeFactoryKit.Stripe,false);
                foreach(double ux in Unit)w.Box(x+ux*hx*.7-.05,x+ux*hx*.7+.05,y+hy,Math.Max(y+hy+.01,head+.25),z+s*hz*.5-.05,z+s*hz*.5+.05,NativeFactoryKit.Chrome);
            }
            int lamp=NativeFactoryRig.HazardLamp(st,clock);foreach(double s in Unit)w.Box(x-.18,x+.18,head+.2,head+.34,z+s*(hz+.06)-.025,z+s*(hz+.06)+.025,NativeFactoryKit.Lamp,false,lamp);
        }
        private void VentRig(FactoryHazard h,in FactoryHazardState st,double clock)
        {
            var w=Parts;int lamp=NativeFactoryRig.HazardLamp(st,clock);double hx=h.size.x/2+.1,hz=h.size.z/2+.1;
            foreach(double sx in Unit)foreach(double sz in Unit){double px=h.a.x+sx*hx,pz=h.a.z+sz*hz;w.Box(px-.07,px+.07,0,.04,pz-.07,pz+.07,NativeFactoryKit.Lamp,false,lamp);}
        }
        /// <summary>The grid's lamp colour: dark idle, amber blinking faster as it charges, cyan live, fading as it discharges.</summary>
        public static int GridLamp(in NativeHazardState s,double time)
        {
            switch(s.Phase)
            {
                case NativeHazardPhase.Warn:return NativeFactoryRig.Strobe(time,2+6*s.Level)?NativeFactoryRig.LampAmber:NativeFactoryRig.LampOff;
                case NativeHazardPhase.Active:return NativeTrainingLook.Cyan;
                case NativeHazardPhase.Clear:return s.Level>.5f?NativeTrainingLook.CyanDeep:NativeFactoryRig.LampOff;
                default:return 0x1E3A44;
            }
        }
        private void GridRig(MapHazard h,in NativeHazardState s,double time)
        {
            var w=Parts;double cx=h.center.x,cz=h.center.z,hx=h.size.x/2,hz=h.size.z/2;int lamp=GridLamp(s,time);
            // border lamps every metre, and the conductor bars across the plate (lit while live)
            for(double z=cz-hz+.5;z<=cz+hz-.5+1e-6;z+=1)foreach(double sx in Unit)w.Box(cx+sx*(hx+.1)-.06,cx+sx*(hx+.1)+.06,0,.035,z-.08,z+.08,NativeFactoryKit.Lamp,false,lamp);
            bool live=s.Phase==NativeHazardPhase.Active||s.Phase==NativeHazardPhase.Clear&&s.Level>.3f;
            int bar=live?NativeTrainingLook.Cyan:s.Phase==NativeHazardPhase.Warn?(s.Level>.6f?0x5A4A20:0x2A2A24):0x1C2328;
            for(double z=cz-hz+.75;z<cz+hz-.5;z+=.75)w.Box(cx-hx+.2,cx+hx-.2,.016,.024,z-.03,z+.03,NativeFactoryKit.Lamp,false,bar);
        }
        private void CraneRig(FactoryHazard h,in FactoryHazardState st,double clock)
        {
            // a monorail hoist under the map's runway block (training-hazard-crane-runway): its carriage over the beam's
            // underside, the hoist and motor, the beacon, cables to the hook, the hook and the strapped beam bundle
            var w=Parts;double X=st.X,Z=st.Z,beam=h.a.y-1,hookY=st.HookY,top=h.size.y;
            double cy0=Math.Min(beam-.25,hookY+.62),cy1=beam;
            w.Box(X-.7,X+.7,cy1-.06,cy1,Z-.62,Z+.62,NativeFactoryKit.CraneDark);
            foreach(double s in Unit){w.Box(X-.7,X+.7,cy1-.06,cy1+1.0,Z+s*.62-.05,Z+s*.62+.05,NativeFactoryKit.CraneDark);w.Box(X-.72,X+.72,cy1+.42,cy1+.5,Z+s*.62-.07,Z+s*.62+.07,NativeFactoryKit.Stripe,false);
                foreach(double dx in P42)w.Add(X+dx,cy1+1.05,Z+s*.42,.12,.12,.05,FactoryQ.Axis(0,0,1,Math.PI/4),NativeFactoryKit.Wheel,false);}
            w.Box(X-.55,X+.55,cy0,cy1-.06,Z-.4,Z+.4,NativeFactoryKit.CraneBody);w.Box(X+.55,X+.9,cy0+.05,cy1-.12,Z-.25,Z+.25,NativeFactoryKit.Motor);
            w.Box(X-.1,X+.1,cy0-.14,cy0,Z+.42,Z+.62,NativeFactoryKit.Lamp,false,NativeFactoryRig.HazardLamp(st,clock));
            double hook=hookY+.45;foreach(double dx in P12)foreach(double dz in P08)if(cy0>hook+.01)w.Box(X+dx-.016,X+dx+.016,hook,cy0,Z+dz-.016,Z+dz+.016,NativeFactoryKit.Cable,false);
            bool high=hookY>4;w.Box(X-.24,X+.24,hookY+.2,hook,Z-.16,Z+.16,NativeFactoryKit.Hook,!high);w.Box(X-.25,X+.25,hookY+.2,hookY+.3,Z-.17,Z+.17,NativeFactoryKit.Stripe,false);
            w.Box(X-.05,X+.05,hookY,hookY+.2,Z-.06,Z+.06,NativeFactoryKit.CraneDark,false);
            double lx=X,lz=Z,ly=st.LoadY;bool cast=ly<=2.5;FactoryQ sway=FactoryQ.Axis(1,0,0,st.Sway);double pivotY=hookY;
            void Piece(double px,double py,double pz,double hx,double hy,double hz,int kind,bool shadow)
            {double dx=px-lx,dy=py-pivotY,dz=pz-lz;sway.Rotate(dx,dy,dz,out double ox,out double oy,out double oz);w.Add(lx+ox,pivotY+oy,lz+oz,hx,hy,hz,sway,kind,shadow,-1,1,px-lx+h.a.x,py-ly,pz-lz);}
            double bw=h.size.x/3;
            for(int k=0;k<3;k++){double bx=lx-h.size.x/2+bw*(k+.5);Piece(bx,ly+.04,lz,bw*.48,.04,h.size.z/2,NativeFactoryKit.Load,cast);Piece(bx,ly+top-.04,lz,bw*.48,.04,h.size.z/2,NativeFactoryKit.Load,cast);Piece(bx,ly+top/2,lz,.035,top/2-.08,h.size.z/2,NativeFactoryKit.Load,cast);}
            foreach(double uz in Unit)Piece(lx,ly+top/2,lz+uz*h.size.z*.3,h.size.x/2+.02,top/2+.02,.05,NativeFactoryKit.Strap,false);
            if(st.Attached)foreach(double ux in Unit)foreach(double uz in Unit)
            {double dx=ux*h.size.x*.4,dz=uz*h.size.z*.4,ax=lx+dx,az=lz+dz,ay=ly+top;double ddx=ax-lx,ddy=ay-pivotY,ddz=az-lz;sway.Rotate(ddx,ddy,ddz,out double ox,out double oy,out double oz);w.Bar(X,hookY,Z,lx+ox,pivotY+oy,lz+oz,.04,.04,NativeFactoryKit.Strap,false);}
            for(int k=0;k<(h.zones?.Length??0);k++)
            {
                var zn=h.zones[k];double hx=h.size.x/2+.5,hz=h.size.z/2+.5;int col=NativeFactoryRig.ZoneLamp(st,k,clock);
                foreach(double sx in Unit)foreach(double sz in Unit){double px=zn.center.x+sx*(hx+.08),pz=zn.center.z+sz*(hz+.08);w.Box(px-.1,px+.1,0,.05,pz-.1,pz+.1,NativeFactoryKit.Lamp,false,col);}
            }
        }

        // ---------------- effects ----------------
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz;
        private static double Hash(int a,int b,int c=0){uint h=(uint)(a*73856093)^(uint)(b*19349663)^(uint)(c*83492791);h^=h>>13;h*=0x5bd1e995;h^=h>>15;return (h&0xffffff)/16777216.0;}
        private static double Frac(double v)=>v-Math.Floor(v);
        private static void Linear(int hex,out double r,out double g,out double b){r=NativeSceneryBuilder.Channel(hex,16);g=NativeSceneryBuilder.Channel(hex,8);b=NativeSceneryBuilder.Channel(hex,0);}
        private void Sprite(double x,double y,double z,double size,double r,double g,double b,double a,bool additive)
        {double s=size/2,ax=rx*s,ay=ry*s,az=rz*s,bx=ux*s,by=uy*s,bz=uz*s;Quads.Add(x-ax-bx,y-ay-by,z-az-bz,x+ax-bx,y+ay-by,z+az-bz,x+ax+bx,y+ay+by,z+az+bz,x-ax+bx,y-ay+by,z-az+bz,r,g,b,a,1,additive,1);}
        private void Floor(double x,double y,double z,double sx,double sz,double r,double g,double b,double a,bool additive)
        {double hx=sx/2,hz=sz/2;Quads.Add(x-hx,y,z-hz,x+hx,y,z-hz,x+hx,y,z+hz,x-hx,y,z+hz,r,g,b,a,1,additive,1);}
        private void Streak(double x,double y,double z,double dx,double dy,double dz,double width,double r,double g,double b,double a)
        {
            // a quad from (x, y, z) along (dx, dy, dz), turned to the eye
            double mx=x+dx/2-ex,my=y+dy/2-ey,mz=z+dz/2-ez,sx=dy*mz-dz*my,sy=dz*mx-dx*mz,sz=dx*my-dy*mx,l=Math.Sqrt(sx*sx+sy*sy+sz*sz);if(l<1e-9)return;sx*=width/2/l;sy*=width/2/l;sz*=width/2/l;
            Quads.Add(x-sx,y-sy,z-sz,x+sx,y+sy,z+sz,x+dx+sx,y+dy+sy,z+dz+sz,x+dx-sx,y+dy-sy,z+dz-sz,r,g,b,a,1,true,1);
        }
        private void Halo(double x,double y,double z,int hex,double size)
        {
            if(hex==NativeFactoryRig.LampOff||hex==0x1E3A44||hex==0x1C2328)return;Linear(hex,out double r,out double g,out double b);double d=Math.Sqrt((x-ex)*(x-ex)+(y-ey)*(y-ey)+(z-ez)*(z-ez));
            double k=hex==NativeFactoryRig.LampSafe?.35:1;Sprite(x,y,z,Math.Max(size,d*.012),r*1.6*k,g*1.6*k,b*1.6*k,.8,true);
        }
        /// <summary>The frame's camera-facing effect quads (eye position and its right and up axes), on the same clocks as <see cref="Build"/>;
        /// `quality` scales the steam, sparks and arcs (the telegraphs' halos and washes are never reduced).</summary>
        public void Effects(MapHazard[] hazards,int round,double clock,NativePhase phase,double fightAt,double time,double camX,double camY,double camZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,double quality)
        {
            Quads.Clear();ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;quality=Math.Max(0,Math.Min(1,quality));if(hazards==null)return;
            for(int i=0;i<hazards.Length;i++)
            {
                var h=hazards[i];if(h==null)continue;
                if(h.kind=="grid"){GridFx(h,i,NativeCyberCityHazards.At(h,phase,round,fightAt,time),time,quality);continue;}
                var st=NativeFactoryHazards.State(h,round,clock);
                switch(h.kind)
                {
                    case NativeFactoryHazards.Vent:
                    {
                        int lamp=NativeFactoryRig.HazardLamp(st,clock);double hx=h.size.x/2+.1,hz=h.size.z/2+.1;foreach(double sx in Unit)foreach(double sz in Unit)Halo(h.a.x+sx*hx,.06,h.a.z+sz*hz,lamp,.35);
                        if(st.Phase==NativeFactoryHazards.Parked)break;bool burst=st.Phase==NativeFactoryHazards.Bursting;int n=(int)Math.Round((burst?22:10)*Math.Max(.4,quality));
                        for(int k=0;k<n;k++)
                        {
                            double life=burst?.9:1.6,t=Frac(Hash(k,i,2)+clock/life),x=h.a.x+(Hash(k,3)-.5)*h.size.x*(.4+.6*t),z=h.a.z+(Hash(k,4)-.5)*h.size.z*(.4+.6*t),y=h.a.y+.1+(burst?h.size.y*1.1:1.2)*t;
                            double a=burst?.55*st.U*(1-t*.6):.4*st.Warn*(1-t);if(a<.01)continue;Linear(0xE8ECEE,out double r,out double g,out double b);Sprite(x,y,z,(burst?.9:.4)+(burst?1.6:.8)*t,r*.8,g*.8,b*.8,a,false);
                        }
                        break;
                    }
                    case NativeFactoryHazards.SawSlot:
                    {
                        bool alongX=Math.Abs(h.b.x-h.a.x)>Math.Abs(h.b.z-h.a.z);double s0=alongX?h.a.x:h.a.z,s1=alongX?h.b.x:h.b.z,c=alongX?h.a.z:h.a.x,len=Math.Abs(s1-s0);int lamps=(int)Math.Floor(len/2)+1;
                        for(int k=0;k<lamps;k++){double f=lamps>1?(double)k/(lamps-1):0,s=s0+(s1-s0)*f;int col=NativeFactoryRig.SlotLamp(st,f,len,clock);foreach(double side in P28)Halo(alongX?s:c+side,.05,alongX?c+side:s,col,.22);}
                        if(st.Phase==NativeFactoryHazards.Running&&st.Lift>.5)
                        {
                            int n=(int)Math.Round(24*quality);double dir=st.Reverse?1:-1;
                            for(int k=0;k<n;k++){double t=Frac(Hash(k,i,7)+clock*3.1),sx=(alongX?dir:0)*(1.2+Hash(k,9))*t,sz=(alongX?0:dir)*(1.2+Hash(k,9))*t,sy=.5*t*(1-t)*4*Hash(k,11);
                                Streak(st.Center.x+sx,.02+sy,st.Center.z+sz+(Hash(k,5)-.5)*.1,(alongX?dir:0)*.18,.05,(alongX?0:dir)*.18,.025,2.4,1.2,.4,1-t);}
                        }
                        break;
                    }
                    case NativeFactoryHazards.Press:
                    {
                        int lamp=NativeFactoryRig.HazardLamp(st,clock);double head=h.a.y+h.size.y/2;foreach(double s in Unit)Halo(st.Center.x,head+.27,st.Center.z+s*(h.size.z/2+.06),lamp,.3);
                        break;
                    }
                    case NativeFactoryHazards.Crane:
                    {
                        Halo(st.X,h.a.y-1.32,st.Z+.52,NativeFactoryRig.HazardLamp(st,clock),.3);
                        for(int k=0;k<(h.zones?.Length??0);k++)
                        {
                            var zn=h.zones[k];double hx=h.size.x/2+.5,hz=h.size.z/2+.5;int col=NativeFactoryRig.ZoneLamp(st,k,clock);foreach(double sx in Unit)foreach(double sz in Unit)Halo(zn.center.x+sx*(hx+.08),.08,zn.center.z+sz*(hz+.08),col,.3);
                            if(st.Zone==k&&(st.Phase==NativeFactoryHazards.Warning||st.Phase==NativeFactoryHazards.Falling))
                            {
                                double pulse=st.Phase==NativeFactoryHazards.Falling?1:.55+.45*(NativeFactoryRig.Strobe(clock,NativeFactoryRig.ZoneHz)?1:0);Linear(NativeFactoryRig.LampRed,out double r,out double g,out double b);
                                Floor(zn.center.x,.02,zn.center.z,2*hx,2*hz,r*.6,g*.6,b*.6,(.2+.4*st.Warn)*pulse,true);
                            }
                        }
                        if(st.Zone>=0&&st.LoadY<8){double k2=Math.Max(0,1-st.LoadY/8);Floor(st.X,.021,st.Z,h.size.x*(1.4-.4*k2),h.size.z*(1.2-.2*k2),0,0,0,.25+.45*k2,false);}
                        break;
                    }
                }
            }
        }
        private void GridFx(MapHazard h,int index,in NativeHazardState s,double time,double quality)
        {
            double cx=h.center.x,cz=h.center.z,hx=h.size.x/2,hz=h.size.z/2;int lamp=GridLamp(s,time);
            for(double z=cz-hz+.5;z<=cz+hz-.5+1e-6;z+=1)foreach(double sx in Unit)Halo(cx+sx*(hx+.1),.05,z,lamp,.22);
            if(s.Phase!=NativeHazardPhase.Active&&!(s.Phase==NativeHazardPhase.Clear&&s.Level>.3f))return;
            Linear(NativeTrainingLook.Cyan,out double r,out double g,out double b);Floor(cx,.03,cz,2*hx-.3,2*hz-.3,r*.5,g*.5,b*.5,.5*(s.Phase==NativeHazardPhase.Active?1:s.Level),true);
            // arcs: short jagged bolts between random points of the plate, re-struck 12 times a second
            int n=(int)Math.Round(8*Math.Max(.4,quality));long slot=(long)Math.Floor(time*12);
            for(int k=0;k<n;k++)
            {
                double ax=cx+(Hash(k,(int)slot,index)-.5)*(2*hx-.4),az=cz+(Hash(k,(int)slot,index+7)-.5)*(2*hz-.4),bx=ax+(Hash(k,(int)slot,3)-.5)*1.2,bz=az+(Hash(k,(int)slot,4)-.5)*1.2,ay=.04,peak=.25+.35*Hash(k,(int)slot,5);
                double mx=(ax+bx)/2+(Hash(k,(int)slot,6)-.5)*.3,mz=(az+bz)/2+(Hash(k,(int)slot,8)-.5)*.3;
                Streak(ax,ay,az,mx-ax,peak,mz-az,.035,r*3,g*3,b*3.4,.9);Streak(mx,ay+peak,mz,bx-mx,-peak,bz-mz,.035,r*3,g*3,b*3.4,.9);
            }
        }
    }
}
