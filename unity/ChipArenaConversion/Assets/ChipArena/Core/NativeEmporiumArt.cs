using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Top Dog Emporium's art poses (docs/arenas/STAGE-ART.md, docs/arenas/EMPORIUM.md "Stage art"): where each modelled rig part of
    /// the stage bank ChipArenaStageEmporium (tools/stages/emporium_art.py; its frames are listed there) stands this frame. Read only
    /// from the replicated state through the very functions Core NativeEmporiumRig poses its boxes with (the simulation's own hazard
    /// states and the rig's public helpers), so the models move exactly as the box rig did: the Wiener Run's frankfurter rolling
    /// with its googly eyes and the ovens' flaps, the bun presses' buns (squash and stretch), rams and bells, the griddles' beds by
    /// their heat and their popcorn sausages, the geysers' caps, pumps and sauce columns, the turning sign and its chasing bulbs,
    /// the bun conveyors, the roller grills' sausages, the Big Dog's wave and blink, the jukeboxes' bubble tubes. The telegraph
    /// lamps and the presses' drop shadows stay the box rig's (NativeEmporiumRig.Kept). Pure and allocation free per frame.
    /// </summary>
    public static class NativeEmporiumArt
    {
        /// <summary>The rig props by slot (the bank's "rig/&lt;name&gt;/&lt;tier&gt;").</summary>
        public static readonly string[] Slots={"wiener","wiener-face","wiener-pupil","flap","bun","ram","bell","popcorn","geyser-cap-k","geyser-cap-m","pump-rod","pump-head-k","pump-head-m",
            "sauce-k","sauce-m","sauce-head-k","sauce-head-m","sign","sign-bulbs-0","sign-bulbs-1","sign-bulbs-2","bun-hanger","grill-sausage-c","grill-sausage-p","bigdog-arm","bigdog-pupils","bigdog-lids",
            "juke-tube","bubble",
            "griddle-emporium-griddle-w-a-0","griddle-emporium-griddle-w-a-1","griddle-emporium-griddle-w-b-0","griddle-emporium-griddle-w-b-1",
            "griddle-emporium-griddle-e-a-0","griddle-emporium-griddle-e-a-1","griddle-emporium-griddle-e-b-0","griddle-emporium-griddle-e-b-1"};
        public const int Wiener=0,WienerFace=1,WienerPupil=2,Flap=3,Bun=4,Ram=5,Bell=6,Popcorn=7,GeyserCap=8,PumpRod=10,PumpHead=11,Sauce=13,SauceHead=15,Sign=17,SignBulbs=18,BunHanger=21,
            GrillSausageCounter=22,GrillSausagePrep=23,BigDogArm=24,BigDogPupils=25,BigDogLids=26,JukeTube=27,Bubble=28,Griddle=29,Griddles=4;
        /// <summary>The bun's height at rest (the box rig's stacked layers), the flaps' hinge, the bell over the press head, the
        /// pump's collar, the hangers' swing length (m).</summary>
        public const double BunHeight=.94,FlapTop=2.2,BellUp=1.3,BellOut=1.2,PumpCollar=.25,HangerDrop=.7;
        /// <summary>The slots that cast shadows (not the small glowing or tiny parts).</summary>
        public static bool Casts(int slot)=>slot!=WienerPupil&&!(slot>=SignBulbs&&slot<SignBulbs+3)&&slot!=BigDogPupils&&slot!=BigDogLids&&slot!=JukeTube&&slot!=Bubble&&slot<Griddle;

        private static readonly FactoryQ Half=FactoryQ.Axis(0,1,0,Math.PI),ToX=FactoryQ.Axis(0,1,0,Math.PI/2);
        private static double Frac(double v)=>v-Math.Floor(v);

        /// <summary>Poses the Emporium's modelled rig for round `round` at match phase/FightAt, the field clock `now` and the render
        /// clock `render` (the ambient rigs), exactly as NativeEmporiumRig.Build poses its boxes. Nothing for any other map.</summary>
        public static void Pose(string mapId,MapHazard[] hazards,ContentBlock[] blocks,int round,NativePhase phase,double fightAt,double now,double render,NativeStageArtPose into)
        {
            into.Clear();if(!NativeEmporiumLooks.Active(mapId))return;double clock=NativeFactoryHazards.Clock(phase,fightAt,now),time=now-fightAt;int griddle=0;
            for(int i=0;i<(hazards?.Length??0);i++)
            {
                var h=hazards[i];if(h==null)continue;
                if(h.kind=="maglev")Lane(h,NativeCyberCityHazards.At(h,phase,round,fightAt,now),time,into);
                else if(h.kind==NativeFactoryHazards.Press)Press(h,NativeFactoryHazards.State(h,round,clock),clock,into);
                else if(h.kind==NativeWardZeroHazards.Burner){if(griddle<Griddles)Burner(h,NativeWardZeroHazards.At(h,phase,round,fightAt,now),time,i,griddle,into);griddle++;}
                else if(h.kind==NativeFactoryHazards.Vent)Geyser(h,NativeFactoryHazards.State(h,round,clock),clock,blocks,into);
            }
            Ambient(blocks,render,into);
        }

        // The Wiener Run: the flaps at both portals, the frankfurter rolling, its face (it does not roll) and its wobbling pupils.
        private static void Lane(MapHazard h,in NativeHazardState s,double time,NativeStageArtPose into)
        {
            double cz=h.center.z,x0=h.center.x-h.size.x/2,x1=h.center.x+h.size.x/2,r=NativeEmporiumRig.Radius(h);
            for(int side=-1;side<=1;side+=2){double px=side<0?x0:x1;int f=side<0?1:-1;into.Add(Flap,px+f*.03,FlapTop,cz,FactoryQ.Axis(0,0,1,f*NativeEmporiumRig.Flap(h,s,px,f,time)));}
            double c=NativeEmporiumRig.Centre(h,s);if(double.IsNaN(c)||Math.Abs(c-h.center.x)>h.size.x/2+2.2)return;
            int d=s.Direction==0?1:s.Direction;double y=r+NativeEmporiumRig.Bob(c);var facing=d>0?FactoryQ.Identity:Half;
            into.Add(Wiener,c,y,cz,FactoryQ.Axis(0,0,1,-c/r));into.Add(WienerFace,c,y,cz,facing);
            for(int e=-1;e<=1;e+=2){double wob=Math.Sin(time*13+e)*.06;into.Add(WienerPupil,c+d*r*.78+d*.1,y+r*.55-.12+wob*.3,cz+e*.42+wob,facing);}
        }

        // A bun press: the bun at the simulation's stroke with the rig's squash, the chrome ram up to the head, the bell.
        private static void Press(MapHazard h,in FactoryHazardState st,double clock,NativeStageArtPose into)
        {
            NativeEmporiumRig.BunPlace(h,st,clock,out double cx,out double cz,out double sq,out double wide);double bottom=st.LoadY,head=NativeEmporiumRig.Head(h),top=bottom+BunHeight*sq;
            double spread=Math.Min(wide,1.55/Math.Max(.1,h.size.x/2));Scaled(into,Bun,cx,bottom,cz,FactoryQ.Identity,spread,sq,spread);
            if(head>top)into.Add(Ram,cx,top,cz,FactoryQ.Identity,head-top);
            into.Add(Bell,cx,head+BellUp,cz+BellOut,FactoryQ.Axis(1,0,0,NativeEmporiumRig.Bell(st,clock)));
        }

        // A griddle: each zone's bed glowing by its heat, its popcorn sausages.
        private static void Burner(MapHazard h,in NativeHazardState s,double time,int index,int griddle,NativeStageArtPose into)
        {
            double k=Math.Max(0,(NativeEmporiumRig.Heat(s,time)-1)/.75);double r=.02+2.4*k,g=.008+1.5*k*k,b=.004+.9*k*k*k;int zones=Math.Min(2,h.zones?.Length??0);
            for(int z=0;z<zones;z++)
            {
                into.Add(Griddle+griddle*2+z,0,0,0,FactoryQ.Identity,1,r,g,b);
                for(int j=0;j<NativeEmporiumRig.PopcornPerZone;j++){NativeEmporiumRig.Popcorn(h.zones[z],s,time,index,z,j,out double px,out double py,out double pz,out FactoryQ q);into.Add(Popcorn,px,py,pz,q);}
            }
        }

        // A geyser: the cap bulging, the pump's rod and head at their stroke, the sauce column and its head while it bursts.
        private static void Geyser(MapHazard h,in FactoryHazardState st,double clock,ContentBlock[] blocks,NativeStageArtPose into)
        {
            double x=h.a.x,y=h.a.y,z=h.a.z;int sauce=NativeEmporiumLooks.KetchupSide(x)?0:1;
            into.Add(GeyserCap+sauce,x,y+.02,z,FactoryQ.Identity,.03+NativeEmporiumRig.Bulge(st));
            var pump=NativeEmporiumRig.PumpOf(blocks,x,z);
            if(pump!=null)
            {
                double top=pump.pos.y+pump.size.y/2,rise=.55*(1-NativeEmporiumRig.Stroke(h,st));
                into.Add(PumpRod,pump.pos.x,top,pump.pos.z,FactoryQ.Identity,PumpCollar+rise);into.Add(PumpHead+sauce,pump.pos.x,top+PumpCollar+rise,pump.pos.z,x>=pump.pos.x?FactoryQ.Identity:Half);
            }
            double column=NativeEmporiumRig.Column(st);if(column<.1)return;
            double fat=1+.1*Math.Sin(clock*15);Scaled(into,Sauce+sauce,x,y,z,FactoryQ.Axis(0,1,0,clock*2),fat,column,fat);
            double head=1+.12*Math.Sin(clock*11);Scaled(into,SauceHead+sauce,x,y+column,z,FactoryQ.Axis(0,1,0,clock),head,1,head);
        }

        // The ambient rigs on the render clock: the sign, the conveyors, the grills, the Big Dog, the jukeboxes.
        private static void Ambient(ContentBlock[] blocks,double t,NativeStageArtPose into)
        {
            var yaw=FactoryQ.Axis(0,1,0,NativeEmporiumLooks.SignHeading(t));double X=NativeEmporiumLooks.SignX,Y=NativeEmporiumLooks.SignY,Z=NativeEmporiumLooks.SignZ;
            into.Add(Sign,X,Y,Z,yaw);
            for(int g=0;g<3;g++){bool on=((g+(int)Math.Floor(t*6))%3)==0;if(on)into.Add(SignBulbs+g,X,Y,Z,yaw);else into.Add(SignBulbs+g,X,Y,Z,yaw,1,.42,.16,.03);}
            int n=NativeEmporiumRig.Hangers;
            for(int side=-1;side<=1;side+=2)for(int k=0;k<n;k++){NativeEmporiumRig.Hanger(side,k,t,out double x,out double z,out double sw);into.Add(BunHanger,x,NativeEmporiumRig.ConveyorY,z,FactoryQ.Axis(0,0,1,sw/HangerDrop));}
            int seed=0;
            for(int i=0;i<(blocks?.Length??0);i++)
            {
                var b=blocks[i];if(!NativeEmporiumRig.Grill(b,out double y,out bool alongX,out double len,out double cross,out int count))continue;seed++;
                for(int k=0;k<count;k++)
                {
                    double u=-len/2+(k+.5)*len/count,roll=t*2.2+k*.7,jig=.006*Math.Sin(t*9+k*1.9+seed);
                    double px=alongX?b.pos.x+u:b.pos.x+jig,pz=alongX?b.pos.z+jig:b.pos.z+u;
                    into.Add(cross>.6?GrillSausageCounter:GrillSausagePrep,px,y,pz,(alongX?FactoryQ.Identity:ToX)*FactoryQ.Axis(0,0,1,roll));
                }
            }
            // the Big Dog: the waving forearm from its elbow, the pupils or (blinking) the lids
            double b0=NativeEmporiumRig.MascotBase,wave=NativeEmporiumRig.Wave(t),ex=.6+.55,ey=b0+1.9+.35,dx=Math.Sin(wave)*.6,dy=Math.Cos(wave)*.6,dz=-.05,l=Math.Sqrt(dx*dx+dy*dy+dz*dz);
            into.Add(BigDogArm,ex,ey,-.1,NativeStageArtPose.FromTo(0,1,0,dx/l,dy/l,dz/l));
            into.Add(NativeEmporiumRig.Blink(t)?BigDogLids:BigDogPupils,0,b0,0,FactoryQ.Identity);
            for(int i=0;i<(blocks?.Length??0);i++)
            {
                var b=blocks[i];if(b?.id==null||b.id.IndexOf("-jukebox-",StringComparison.Ordinal)<0)continue;int f=b.pos.z>0?-1:1;double face=b.pos.z+f*b.size.z/2;
                for(int k=0;k<2;k++)
                {
                    double px=b.pos.x+(k==0?-1:1)*(b.size.x/2-.12);Lin(NativeEmporiumLooks.Mix(NativeEmporiumLooks.NeonPink,NativeEmporiumLooks.NeonCyan,.5+.5*Math.Sin(t*1.3+k*2)),out double r,out double g,out double bl);
                    into.Add(JukeTube,px,.4,face+f*.03,FactoryQ.Identity,1.9,r,g,bl);
                    for(int j=0;j<4;j++)into.Add(Bubble,px,.535+Frac(t*.4+j*.25+k*.13)*1.7,face+f*.0375,FactoryQ.Identity);
                }
            }
        }

        // A draw scaled along all three of its prop's axes.
        private static void Scaled(NativeStageArtPose p,int slot,double x,double y,double z,in FactoryQ q,double sx,double sy,double sz)
        {
            int i=p.Count;p.Add(slot,x,y,z,q,sy);if(p.Count==i)return;int o=i*12;
            p.Place[o]*=(float)sx;p.Place[o+4]*=(float)sx;p.Place[o+8]*=(float)sx;p.Place[o+2]*=(float)sz;p.Place[o+6]*=(float)sz;p.Place[o+10]*=(float)sz;
        }
        // An sRGB hex to linear 0..1 (a tube's tint multiplies a white glow).
        private static void Lin(int hex,out double r,out double g,out double b){r=L((hex>>16)&255);g=L((hex>>8)&255);b=L(hex&255);}
        private static double L(int c){double v=c/255.0;return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);}
    }
}
