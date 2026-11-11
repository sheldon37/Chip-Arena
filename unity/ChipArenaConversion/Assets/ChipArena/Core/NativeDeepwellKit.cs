using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Deepwell's box kit (docs/arenas/DEEPWELL.md "The look"): what NativeBlockLit instances draw when the stage bank
    /// ChipArenaStageDeepwell is not on the machine (CI, a build without the Drive zip); with the bank the modelled art draws in
    /// its place (Runtime NativeDeepwellPresentation). Three kinds of part:
    /// <list type="bullet">
    /// <item>Attached (<see cref="Parent"/> the block): the decks' plate seams, the wellhead's brass ring and warning stripes, the
    /// pools' rim stripes; the walls' ribs, brass bands, skirtings and warm lamps; the glass's frames, mullions and transoms (the
    /// glass blocks' own boxes are left undrawn, <see cref="NativeDeepwellLooks.KitBlock"/>: the effects glaze the panes); the
    /// pylons' bands and light strips; the consoles' screens; lockers, screens and chambers; rail caps; stair nosings. Every
    /// attached part stays inside its block grown by <see cref="Proud"/> (the stage rule: what looks like cover is cover).</item>
    /// <item>Overhead (Parent -1, at least <see cref="Clearance"/> m over every floor under it): the Well's dome ribs and rings,
    /// the drill's gantry and housing, the tunnels', spine's, wings' and bays' roof ribs, the pendant lamps.</item>
    /// <item>Outside (Parent -1, wholly outside the hull): the seabed, its rocks and coral, the station's legs, the black
    /// smokers, the far habitats.</item>
    /// </list>
    /// No two parts lay faces of one facing in one plane (each layer stands its own hair proud). Deterministic; allocates only
    /// while building (Runtime packs it once per map).
    /// </summary>
    public sealed class NativeDeepwellKit
    {
        public const float Proud=.25f;public const double Clearance=2.0;
        public readonly NativeFactoryKit.Writer Parts;public int[] Parent=Array.Empty<int>();
        /// <summary>For part i: 0 attached, 1 overhead, 2 outside.</summary>
        public int[] Role=Array.Empty<int>();
        private readonly List<int> parents=new List<int>(),roles=new List<int>();private int current=-1,role;
        public int Count=>Parts.Count;
        private NativeDeepwellKit(NativeFactoryKit.Looks looks){Parts=new NativeFactoryKit.Writer(2048,true,looks);}
        private void Track(int before){for(int i=before;i<Parts.Count;i++){parents.Add(role==0?current:-1);roles.Add(role);}}
        private void Add(double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=true,int color=-1){int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow,color);Track(n);}
        private void Box(double x0,double x1,double y0,double y1,double z0,double z1,int kind,bool shadow=true,int color=-1){int n=Parts.Count;Parts.Box(x0,x1,y0,y1,z0,z1,kind,shadow,color);Track(n);}
        private void Bar(double x0,double y0,double z0,double x1,double y1,double z1,double t,int kind,bool shadow=true){int n=Parts.Count;Parts.Bar(x0,y0,z0,x1,y1,z1,t,t,kind,shadow);Track(n);}
        private static double H(int a,int b=0,int c=0)=>NativeDeepwellLooks.Hash(a,b,c);
        private static bool Starts(string id,string p)=>id!=null&&id.StartsWith(p,StringComparison.Ordinal);
        private static bool Has(string id,string p)=>id!=null&&id.IndexOf(p,StringComparison.Ordinal)>=0;
        private static bool Ends(string id,string p)=>id!=null&&id.EndsWith(p,StringComparison.Ordinal);
        private struct Bx {public double X0,X1,Y0,Y1,Z0,Z1;public string Id;public int Index;public double Sx=>X1-X0;public double Sy=>Y1-Y0;public double Sz=>Z1-Z0;public double Cx=>(X0+X1)/2;public double Cz=>(Z0+Z1)/2;public double Cy=>(Y0+Y1)/2;}
        private const int Steel=NativeDeepwellLooks.Steel,Dark=NativeDeepwellLooks.SteelDark,Brass=NativeDeepwellLooks.Brass,Frame=NativeDeepwellLooks.Frame,Lamp=NativeDeepwellLooks.Lamp,Cyan=NativeDeepwellLooks.Cyan,
            Core=NativeDeepwellLooks.Core,Stripe=NativeDeepwellLooks.Stripe,Rubber=NativeDeepwellLooks.Rubber,Rock=NativeDeepwellLooks.Rock,Sand=NativeDeepwellLooks.Sand,Hull=NativeDeepwellLooks.Hull,Coral=NativeDeepwellLooks.Coral,
            Screen=NativeDeepwellLooks.Screen;

        /// <summary>The map's kit for its look (null for another map).</summary>
        public static NativeDeepwellKit Build(ContentMap map,MapLook look)
        {
            if(!NativeDeepwellLooks.Active(map?.id))return null;var k=new NativeDeepwellKit(NativeDeepwellLooks.Resolve(look?.kitLooks));
            for(int i=0;i<(map.blocks?.Length??0);i++)
            {
                var b=map.blocks[i];if(b?.id==null)continue;var r=b.rotation;if(r!=null&&(Math.Abs(r.x)>1e-4||Math.Abs(r.y)>1e-4||Math.Abs(r.z)>1e-4))continue;
                var x=new Bx{X0=b.pos.x-b.size.x/2,X1=b.pos.x+b.size.x/2,Y0=b.pos.y-b.size.y/2,Y1=b.pos.y+b.size.y/2,Z0=b.pos.z-b.size.z/2,Z1=b.pos.z+b.size.z/2,Id=b.id,Index=i};
                k.current=i;k.role=0;k.Dress(x);
            }
            k.current=-1;k.role=1;k.Overhead(map);k.role=2;k.Outside();
            k.Parent=k.parents.ToArray();k.Role=k.roles.ToArray();return k;
        }

        /// <summary>A plate on face `side` (0 +x, 1 -x, 2 +z, 3 -z, 4 top, 5 bottom): u0..u1 along it (z on ±x faces, x on ±z faces and
        /// the top), v0..v1 up it (y; z on the top), `t` thick, its outer face `o` proud of the block's.</summary>
        private void Face(Bx b,int side,double u0,double u1,double v0,double v1,int kind,double t,double o,bool shadow=false)
        {
            if(!(u1>u0&&v1>v0))return;
            switch(side)
            {
                case 0:Box(b.X1+o-t,b.X1+o,v0,v1,u0,u1,kind,shadow);break;case 1:Box(b.X0-o,b.X0-o+t,v0,v1,u0,u1,kind,shadow);break;
                case 2:Box(u0,u1,v0,v1,b.Z1+o-t,b.Z1+o,kind,shadow);break;case 3:Box(u0,u1,v0,v1,b.Z0-o,b.Z0-o+t,kind,shadow);break;
                case 4:Box(u0,u1,b.Y1+o-t,b.Y1+o,v0,v1,kind,shadow);break;default:Box(u0,u1,b.Y0-o,b.Y0-o+t,v0,v1,kind,shadow);break;
            }
        }
        private static bool LongX(Bx b)=>b.Sx>=b.Sz;

        private void Dress(Bx b)
        {
            string id=b.Id;
            if(Starts(id,"rig-glass-")){Glass(b);return;}
            if(Starts(id,"rig-stair-")){Step(b);return;}
            if(Starts(id,"rig-rail-")){Cap(b);return;}
            if(Starts(id,"rig-pylon-")){Pylon(b);return;}
            if(Starts(id,"rig-console-")){Face(b,4,b.X0+.1,b.X1-.1,b.Z0+.1,b.Z1-.1,Screen,.02,.012);Band(b,b.Y0+.12,b.Y0+.2,Brass,.02,.012);return;}
            if(Starts(id,"rig-sub-")){Face(b,4,b.X0+.5,b.X1-.5,b.Cz-.45,b.Cz+.45,Dark,.03,.02);Band(b,b.Y1-.5,b.Y1-.38,Stripe,.02,.012);return;}
            if(Starts(id,"rig-screen-")||Starts(id,"rig-locker-")||Starts(id,"rig-chamber-")){Fitting(b);return;}
            if(Starts(id,"rig-bulkhead-")||Starts(id,"rig-wall-")){Wall(b);return;}
            if(Starts(id,"rig-case-")){Band(b,b.Y0+.5,b.Y0+.62,Stripe,.02,.012);return;}
            if(id=="rig-well-floor"){Deck(b);Wellhead(b);return;}
            if(Has(id,"-floor")||Has(id,"-upper")||Starts(id,"rig-gallery-")||Starts(id,"rig-catwalk-")){Deck(b);if(Starts(id,"rig-bay-"))PoolRim(b);return;}
        }
        // a band round the block's four sides
        private void Band(Bx b,double y0,double y1,int kind,double t,double o)
        {Face(b,0,b.Z0,b.Z1,y0,y1,kind,t,o);Face(b,1,b.Z0,b.Z1,y0,y1,kind,t,o);Face(b,2,b.X0,b.X1,y0,y1,kind,t,o);Face(b,3,b.X0,b.X1,y0,y1,kind,t,o);}
        private void Cap(Bx b){if(LongX(b))Box(b.X0,b.X1,b.Y1+.004,b.Y1+.05,b.Z0-.02,b.Z1+.02,Brass);else Box(b.X0-.02,b.X1+.02,b.Y1+.008,b.Y1+.058,b.Z0,b.Z1,Brass);}

        // The decks: plate seams every 2 m, each way in its own layer
        private void Deck(Bx b)
        {
            for(double x=Math.Ceiling((b.X0+.3)/2)*2;x<b.X1-.3;x+=2)Face(b,4,x-.02,x+.02,b.Z0+.04,b.Z1-.04,Dark,.008,.003);
            for(double z=Math.Ceiling((b.Z0+.3)/2)*2;z<b.Z1-.3;z+=2)Face(b,4,b.X0+.04,b.X1-.04,z-.02,z+.02,Dark,.008,.006);
        }
        // The wellhead: warning stripes round the ram's footprint, a brass ring, the core's glow through the grate
        private void Wellhead(Bx b)
        {
            const double r=2.2,w=.3;
            Face(b,4,-r-w,r+w,r,r+w,Stripe,.012,.012);Face(b,4,-r-w,r+w,-r-w,-r,Stripe,.012,.012);Face(b,4,r,r+w,-r,r,Stripe,.012,.012);Face(b,4,-r-w,-r,-r,r,Stripe,.012,.012);
            Face(b,4,-1.5,1.5,-1.5,1.5,Rubber,.012,.014);
            for(int k=0;k<16;k++){double a=(k+.5)*Math.PI/8,l=2*3.6*Math.Tan(Math.PI/16);Add(Math.Sin(a)*3.6,b.Y1+.03+(k%2)*.006,Math.Cos(a)*3.6,l/2,.02,.06,FactoryQ.Axis(0,1,0,a),Brass,false);}
        }
        // The pool's rim: a warning stripe along every edge of the water that lies on this floor plate
        private void PoolRim(Bx b)
        {
            double sz=b.Cz>0?1:-1,x0=NativeDeepwellLooks.PoolX0,x1=NativeDeepwellLooks.PoolX1,z0=sz>0?NativeDeepwellLooks.PoolZ0:-NativeDeepwellLooks.PoolZ1,z1=sz>0?NativeDeepwellLooks.PoolZ1:-NativeDeepwellLooks.PoolZ0;const double w=.35;
            void Strip(double a0,double a1,double c0,double c1){double u0=Math.Max(a0,b.X0),u1=Math.Min(a1,b.X1),v0=Math.Max(c0,b.Z0),v1=Math.Min(c1,b.Z1);Face(b,4,u0,u1,v0,v1,Stripe,.012,.012);}
            Strip(x0-w,x1+w,z0-w,z0);Strip(x0-w,x1+w,z1,z1+w);Strip(x0-w,x0,z0,z1);Strip(x1,x1+w,z0,z1);
        }
        // Steel walls and bulkheads: ribs every 2 m on both long faces, a brass band, a skirting, warm lamps high up
        private void Wall(Bx b)
        {
            bool lx=LongX(b);double a0=lx?b.X0:b.Z0,a1=lx?b.X1:b.Z1;int f0=lx?2:0,f1=lx?3:1;
            if(Ends(b.Id,"-head")||Ends(b.Id,"-sill")){Face(b,5,lx?b.X0+.1:b.X0+.06,lx?b.X1-.1:b.X1-.06,lx?b.Z0+.06:b.Z0+.1,lx?b.Z1-.06:b.Z1-.1,Cyan,.02,.012);return;}
            foreach(int f in new[]{f0,f1})
            {
                for(double u=a0+.5;u<a1-.2;u+=2)Face(b,f,u-.08,u+.08,b.Y0+.3,b.Y1-.05,Frame,.06,.06,true);
                Face(b,f,a0+.04,a1-.04,b.Y0,b.Y0+.26,Dark,.03,.03);
                if(b.Sy>2.2)Face(b,f,a0+.04,a1-.04,b.Y0+1.04,b.Y0+1.14,Brass,.02,.02);
                if(b.Sy>3)for(double u=a0+1.63;u<a1-.6;u+=4)Face(b,f,u-.3,u+.3,b.Y0+2.42,b.Y0+2.54,Lamp,.05,.09);
            }
        }
        // The glass: a frame of top and bottom rails, mullions every 2.4 m or so, a transom at each deck line; the pane is the effects'
        private void Glass(Bx b)
        {
            bool lx=LongX(b);double a0=lx?b.X0:b.Z0,a1=lx?b.X1:b.Z1,len=a1-a0;int n=Math.Max(1,(int)Math.Round(len/2.4));
            void Post(double u0,double u1,double y0,double y1,double grow,int kind){if(lx)Box(u0,u1,y0,y1,b.Z0-grow,b.Z1+grow,kind);else Box(b.X0-grow,b.X1+grow,y0,y1,u0,u1,kind);}
            for(int k=0;k<=n;k++){double u=a0+len*k/n;Post(k==0?a0-.012:u-.07,k==n?a1+.012:u+.07,b.Y0-.006,b.Y1+.006,0,Frame);}
            Post(a0,a1,b.Y0,b.Y0+.22,.02,Steel);Post(a0,a1,b.Y1-.22,b.Y1,.02,Steel);
            foreach(double y in new[]{0.0,3.2})if(y>b.Y0+.5&&y<b.Y1-.5)Post(a0,a1,y-.2,y+.02,.03,Brass);
        }
        private void Pylon(Bx b)
        {
            foreach(double y in new[]{.9,3.5,6.2})Band(b,y,y+.2,Brass,.04,.02);
            foreach(var (y0,y1) in new[]{(1.3,2.9),(4.2,5.9)})
            {Face(b,0,b.Cz-.06,b.Cz+.06,y0,y1,Cyan,.02,.012);Face(b,1,b.Cz-.06,b.Cz+.06,y0,y1,Cyan,.02,.012);Face(b,2,b.Cx-.06,b.Cx+.06,y0,y1,Cyan,.02,.012);Face(b,3,b.Cx-.06,b.Cx+.06,y0,y1,Cyan,.02,.012);}
        }
        private void Fitting(Bx b)
        {
            bool lx=LongX(b);double a0=lx?b.X0:b.Z0,a1=lx?b.X1:b.Z1;int f0=lx?2:0,f1=lx?3:1;
            Band(b,b.Y1-.24,b.Y1-.1,Brass,.02,.012);
            foreach(int f in new[]{f0,f1})
            {
                if(Starts(b.Id,"rig-locker-"))for(double u=a0+.6;u<a1-.1;u+=.6)Face(b,f,u-.012,u+.012,b.Y0+.1,b.Y1-.3,Dark,.01,.006);
                else if(Starts(b.Id,"rig-chamber-")){double c=(a0+a1)/2;Face(b,f,c-.35,c+.35,b.Y0+1.2,b.Y0+1.9,Screen,.02,.012);}
                else Face(b,f,a0+.3,a1-.3,b.Y0+1.5,b.Y0+1.58,Cyan,.02,.012);
            }
        }
        private void Step(Bx b)
        {
            bool lx=LongX(b);
            if(lx)Face(b,4,b.X0+.02,b.X1-.02,b.Z0+.01,b.Z0+.06,Brass,.01,.006);else Face(b,4,b.X0+.01,b.X0+.06,b.Z0+.02,b.Z1-.02,Brass,.01,.006);
        }

        // ---------------- overhead ----------------
        private void Arch(double x0,double z0,double x1,double z1,double y,double rise,double t,int kind,int n=6)
        {
            double px=x0,py=y,pz=z0;
            for(int k=1;k<=n;k++){double f=(double)k/n,qx=x0+(x1-x0)*f,qz=z0+(z1-z0)*f,qy=y+rise*Math.Sin(Math.PI*f);Bar(px,py,pz,qx,qy,qz,t,kind,false);px=qx;py=qy;pz=qz;}
        }
        private void Overhead(ContentMap map)
        {
            // the Well's dome: twelve ribs from the wall's top to the crown ring, two rings; the drill's gantry on the pylons and its housing
            double r=NativeDeepwellLooks.WellRadius,y0=NativeDeepwellLooks.DomeSpring,y1=NativeDeepwellLooks.DomeApex;
            for(int k=0;k<12;k++)
            {
                double a=k*Math.PI/6+Math.PI/12,px=0,py=0,pz=0;
                for(int s=0;s<=6;s++){double f=s/6.0*Math.PI/2*.86,rr=r*1.12*Math.Cos(f),yy=y0+(y1-y0)*Math.Sin(f)/Math.Sin(Math.PI/2*.86),qx=Math.Sin(a)*rr,qz=Math.Cos(a)*rr;if(s>0)Bar(px,py,pz,qx,yy,qz,.16,Frame,false);px=qx;py=yy;pz=qz;}
            }
            foreach(double f in new[]{.34,.66})
            {
                double ang=f*Math.PI/2*.86,rr=r*1.12*Math.Cos(ang),yy=y0+(y1-y0)*Math.Sin(ang)/Math.Sin(Math.PI/2*.86);
                for(int k=0;k<12;k++){double a=k*Math.PI/6+Math.PI/12,c=a+Math.PI/6;Bar(Math.Sin(a)*rr,yy,Math.Cos(a)*rr,Math.Sin(c)*rr,yy,Math.Cos(c)*rr,.12,Frame,false);}
            }
            Box(-4.9,4.9,7.24,7.7,3.6,4.8,Dark);Box(-4.9,4.9,7.24,7.7,-4.8,-3.6,Dark);Box(-4.8,-3.6,7.24,7.7,-3.6,3.6,Dark);Box(3.6,4.8,7.24,7.7,-3.6,3.6,Dark);
            Box(-1.5,1.5,11.4,y1+.3,-1.5,1.5,Steel);Box(-1.7,1.7,11.2,11.4,-1.7,1.7,Brass);
            foreach(var b in map.blocks??Array.Empty<ContentBlock>())
            {
                if(b?.id==null)continue;double x0=b.pos.x-b.size.x/2,x1=b.pos.x+b.size.x/2,z0=b.pos.z-b.size.z/2,z1=b.pos.z+b.size.z/2;
                if(Starts(b.id,"rig-tunnel-")&&(Has(b.id,"-floor-")||Ends(b.id,"-base")))
                {
                    // a ring tunnel's barrel ribs across its 4 m, and a pendant lamp every other bay
                    bool alongZ=Ends(b.id,"-floor-0");
                    if(alongZ)for(double z=z0+1.2;z<z1-.4;z+=2.4){Arch(x0+.2,z,x1-.2,z,3.24,1.15,.1,Frame);}
                    else for(double x=x0+1.2;x<x1-.4;x+=2.4){Arch(x,z0+.2,x,z1-.2,3.24,1.15,.1,Frame);}
                    Box(b.pos.x-.3,b.pos.x+.3,2.9,3.0,b.pos.z-.3,b.pos.z+.3,Lamp,false);
                }
                else if(Starts(b.id,"rig-spine-")&&Ends(b.id,"-floor")){for(double x=x0+1.2;x<x1-.4;x+=2.4)Arch(x,z0+.2,x,z1-.2,6.44,1.1,.12,Frame);Box(b.pos.x-.4,b.pos.x+.4,6.0,6.1,-.4,.4,Lamp,false);Box(b.pos.x-.4,b.pos.x+.4,2.62,2.7,-.4,.4,Lamp,false);}
                else if(Starts(b.id,"rig-wing-")&&Ends(b.id,"-floor"))
                {
                    for(double z=z0+1.4;z<z1-.4;z+=3)Arch(x0+.2,z,x1-.2,z,6.44,1.9,.14,Frame);
                    double sg=b.pos.x>0?1:-1;foreach(double z in new[]{-2.5,2.5})Box(b.pos.x+sg*1.6-.4,b.pos.x+sg*1.6+.4,5.9,6.0,z-.4,z+.4,Lamp,false);foreach(double z in new[]{-4.0,0,4.0})Box(b.pos.x+sg*3.6-.4,b.pos.x+sg*3.6+.4,2.64,2.72,z-.4,z+.4,Lamp,false);
                }
                else if(b.id=="rig-glass-bay-n"||b.id=="rig-glass-bay-s")
                {
                    double zs=b.pos.z>0?1:-1;for(double z=11.8;z<21;z+=3.2)Arch(-8.2,zs*z,8.2,zs*z,6.44,2.2,.14,Frame,8);
                    Box(-6.6,-5.4,5.6,5.7,zs*15.6,zs*16.8,Lamp,false);Box(5.4,6.6,5.6,5.7,zs*15.6,zs*16.8,Lamp,false);
                }
            }
        }

        // ---------------- outside ----------------
        private void Outside()
        {
            double sea=NativeDeepwellLooks.Seabed;
            Box(-150,150,sea-1,sea,-150,150,Sand);
            // rocks and coral heads on the seabed, clear of the hull
            for(int i=0;i<64;i++)
            {
                double a=H(i,31)*2*Math.PI,d=24+H(i,32)*70,x=Math.Sin(a)*d,z=Math.Cos(a)*d;if(NativeDeepwellLooks.Inside(x,z,5))continue;
                double s=1.2+H(i,33)*3.6,h=s*(.5+H(i,34)*.9);Add(x,sea+h*.42,z,s,h/2,s*(.6+.5*H(i,35)),FactoryQ.Axis(0,1,0,H(i,36)*3)*FactoryQ.Axis(1,0,0,(H(i,37)-.5)*.5),Rock);
                if(i%3==0)Add(x+s*.3,sea+h*.9,z-s*.2,s*.32,s*.3,s*.32,FactoryQ.Axis(0,1,0,H(i,38)*3),Coral);
            }
            // the station's legs (outside the plan of every floor's edge: under the hull's outline, a metre out)
            foreach(var (x,z) in new[]{(29.2,10.2),(29.2,-10.2),(29.2,0.0),(11.5,11.5),(11.5,-11.5),(9.2,22.2),(9.2,-22.2),(25.7,18.8),(25.7,-18.8),(14.0,3.8),(14.0,-3.8)})
                foreach(double sx in new[]{-1.0,1.0}){Box(sx*x-.45,sx*x+.45,sea,-1.1,z-.45,z+.45,Dark);Box(sx*x-.8,sx*x+.8,sea,sea+.5,z-.8,z+.8,Steel);}
            // black smokers: chimneys with a glowing throat
            for(int i=0;i<5;i++){Smoker(i,out double x,out double z,out double h);Box(x-.9,x+.9,sea,sea+h*.6,z-.9,z+.9,Rock);Box(x-.55,x+.55,sea+h*.6,sea+h,z-.55,z+.55,Rock);Box(x-.3,x+.3,sea+h,sea+h+.08,z-.3,z+.3,Core,false);}
            // the far habitats: three lit pods on the plain
            foreach(var (x,z,s) in new[]{(-78.0,52.0,1.0),(84.0,-40.0,1.2),(10.0,96.0,.9)})
            {
                Box(x-7*s,x+7*s,sea+2,sea+7*s,z-7*s,z+7*s,Steel);Box(x-5*s,x+5*s,sea+7*s,sea+10*s,z-5*s,z+5*s,Dark);
                Box(x-7.1*s,x+7.1*s,sea+4*s,sea+4.6*s,z-7.1*s,z+7.1*s,Lamp,false);Box(x-.4,x+.4,sea+10*s,sea+16*s,z-.4,z+.4,Dark);Box(x-.6,x+.6,sea+16*s,sea+16*s+.8,z-.6,z+.6,NativeDeepwellLooks.Red,false);
            }
        }
        /// <summary>Black smoker `i` on the seabed: its place and height (the effects' bubble columns rise from its throat).</summary>
        public const int Smokers=5;
        public static void Smoker(int i,out double x,out double z,out double height)
        {
            switch(i){case 0:x=-38;z=-17;break;case 1:x=37;z=19;break;case 2:x=-15;z=30;break;case 3:x=17;z=-31;break;default:x=-44;z=24;break;}
            height=i==0?3.6:i==1?4.4:i==2?3.2:i==3?4.0:4.6;
        }
    }

    /// <summary>
    /// Deepwell's moving pieces for one frame (docs/arenas/DEEPWELL.md), posed from the replicated hazard state and the render clock
    /// into NativeBlockLit parts: the drill's ram riding its stroke over the wellhead, the klaxon lamps on the pylons, the core's
    /// glow, the vents' grates rattling; and the sea outside, all match long: the whales, the jellyfish pulsing, the schools of fish
    /// wheeling, the kelp swaying, the survey drone. Each part carries the stage art's slot (Core NativeDeepwellArt poses its model
    /// from the part itself). Deterministic (the same state gives the same parts); allocation free after construction.
    /// </summary>
    public sealed class NativeDeepwellRig
    {
        public const int Capacity=900;
        public readonly NativeFactoryKit.Writer Parts;
        /// <summary>The stage art's view of the frame's parts: for part i its rig slot (-1: none of its own), a level (a size or a
        /// light) and its light's colour (linear rgb times its gain). Nothing draws by them without the stage bank.</summary>
        public readonly int[] Slot=new int[Capacity];public readonly double[] Level=new double[Capacity];public readonly float[] Tint=new float[Capacity*3];
        /// <summary>The frame's moments (the effects and the checks read them): the piston, the core's light, each vent's telegraph and blast.</summary>
        public NativeDeepwellLooks.Piston Piston;public double CoreLevel;public int Vents;public readonly double[] VentWarn=new double[8],VentBlast=new double[8];
        private const int Steel=NativeDeepwellLooks.Steel,Dark=NativeDeepwellLooks.SteelDark,Brass=NativeDeepwellLooks.Brass,Core=NativeDeepwellLooks.Core,Rubber=NativeDeepwellLooks.Rubber,Kelp=NativeDeepwellLooks.Kelp,
            Jelly=NativeDeepwellLooks.Jelly,Fish=NativeDeepwellLooks.Fish,Whale=NativeDeepwellLooks.Whale,Hull=NativeDeepwellLooks.Hull,Red=NativeDeepwellLooks.Red,Belly=NativeDeepwellLooks.Belly,Frame=NativeDeepwellLooks.Frame,Lamp=NativeDeepwellLooks.Lamp;
        private readonly double[] pylons;private readonly int pylonCount;
        public NativeDeepwellRig(NativeFactoryKit.Looks looks,ContentMap map)
        {
            Parts=new NativeFactoryKit.Writer(Capacity,false,looks);var p=new List<double>();
            foreach(var k in map?.blocks??Array.Empty<ContentBlock>())if(k?.id!=null&&k.id.StartsWith("rig-pylon-",StringComparison.Ordinal)){p.Add(k.pos.x);p.Add(k.pos.z);p.Add(k.size.x/2);}
            pylons=p.ToArray();pylonCount=pylons.Length/3;
        }
        private void Put(int slot,double level,double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=true,int color=-1,double r=1,double g=1,double b=1)
        {int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow,color);if(Parts.Count>n){Slot[n]=slot;Level[n]=level;Tint[n*3]=(float)r;Tint[n*3+1]=(float)g;Tint[n*3+2]=(float)b;}}
        private static int Hex(double r,double g,double b){int C(double v)=>(int)Math.Round(255*Math.Max(0,Math.Min(1,v)));return C(r)<<16|C(g)<<8|C(b);}
        /// <summary>A jellyfish's light by tint (0 violet, 1 cyan, 2 rose): linear rgb.</summary>
        public static void JellyLight(int tint,out double r,out double g,out double b){if(tint==0){r=.62;g=.42;b=1;}else if(tint==1){r=.25;g=.9;b=1;}else{r=1;g=.38;b=.62;}}
        /// <summary>The klaxon lamps' light for a telegraph level: they beat faster as the stroke nears, and stay lit while the ram is down.</summary>
        public static double Klaxon(in NativeDeepwellLooks.Piston p,double render)
        {if(p.Held||p.Moving)return 1;if(!(p.Warn>0))return 0;double beat=.5+.5*Math.Sin(render*(6+14*p.Warn));return .25+.75*beat;}

        /// <summary>Fills <see cref="Parts"/> for the hazards and the replicated state (round, phase, FightAt) at field clock `now`
        /// and render clock `render`.</summary>
        public void Build(MapHazard[] hazards,int round,NativePhase phase,double fightAt,double now,double render)
        {
            Parts.Clear();Vents=0;Piston=default;Piston.Since=double.PositiveInfinity;double clock=NativeFactoryHazards.Clock(phase,fightAt,now);var I=FactoryQ.Identity;
            for(int i=0;i<(hazards?.Length??0);i++)
            {
                var h=hazards[i];if(h==null)continue;
                if(h.kind==NativeFactoryHazards.Press)
                {
                    // the ram: its body, a brass collar, the dark shoe, the rods up into the housing
                    var p=NativeDeepwellLooks.PistonAt(h,round,clock);Piston=p;double shake=p.Warn>0&&!p.Moving&&!p.Held?Math.Sin(render*47)*.02*p.Warn:0,y=p.Y+shake,hx=h.size.x/2,hy=h.size.y/2,hz=h.size.z/2;
                    Put(NativeDeepwellArt.Ram,0,h.b.x,y,h.b.z,hx-.12,hy-.3,hz-.12,I,Steel);
                    Put(-1,0,h.b.x,y-hy+.15,h.b.z,hx,.15,hz,I,Dark);Put(-1,0,h.b.x,y+hy-.5,h.b.z,hx-.04,.12,hz-.04,I,Brass);Put(-1,0,h.b.x,y,h.b.z,hx-.06,.1,hz-.06,I,Brass);
                    double top=NativeDeepwellLooks.DomeApex-1.0;if(top>y+hy)for(int s=-1;s<=1;s+=2)Put(NativeDeepwellArt.RamRod,0,h.b.x+s*.8,(y+hy+top)/2,h.b.z,.22,(top-y-hy)/2,.22,I,Frame);
                    // the klaxons on the pylons' inner corners, and the core under the wellhead's grate
                    double kl=Klaxon(p,render);
                    for(int k=0;k<pylonCount;k++){double px=pylons[k*3],pz=pylons[k*3+1],hw=pylons[k*3+2],sx=px>0?-1:1,sz=pz>0?-1:1;Put(NativeDeepwellArt.Klaxon,kl,px+sx*(hw+.1),2.5,pz+sz*(hw+.1),.1,.16,.1,I,Red,false,Hex(.25+.75*kl,.05+.2*kl,.04+.12*kl),.3+2.6*kl,.05+.5*kl,.04+.3*kl);}
                    CoreLevel=NativeDeepwellLooks.CoreLevel(p,render);double c=CoreLevel;
                    Put(NativeDeepwellArt.Core,c,h.b.x,.02,h.b.z,1.3,.012,1.3,I,Core,false,Hex(.35+.65*c,.12+.5*c,.04+.3*c),.6+3.4*c,.22+1.5*c,.05+.6*c);
                }
                else if(h.kind==NativeFactoryHazards.Vent&&Vents<VentWarn.Length)
                {
                    // the vent's grate: it rattles through the telegraph and stands open through the blast
                    NativeDeepwellLooks.VentAt(h,round,clock,out double warn,out double blast,out _);VentWarn[Vents]=warn;VentBlast[Vents]=blast;Vents++;
                    double tilt=blast>0?.5*blast:warn>0?Math.Sin(render*38+i)*.05*warn:0,sgn=h.axis.x>=0?1:-1;
                    Put(NativeDeepwellArt.Grate,Math.Max(warn,blast),h.a.x,h.a.y+.03+Math.Abs(Math.Sin(tilt))*.5,h.a.z,h.size.x/2-.5,.03,h.size.z/2-.3,FactoryQ.Axis(0,0,1,sgn*tilt),Rubber,false);
                }
            }
            Sea(render);
        }

        // The sea outside: a pure function of the render clock
        private void Sea(double t)
        {
            // the whales: body, tail (with its flukes), two flippers
            for(int k=0;k<NativeDeepwellLooks.Whales;k++)
            {
                NativeDeepwellLooks.WhaleAt(k,t,out double x,out double y,out double z,out double heading,out double pitch,out double tail,out double fin);double L=NativeDeepwellLooks.WhaleLength(k),s=L/17;
                var q=FactoryQ.YawPitch(heading,-pitch);
                Put(NativeDeepwellArt.WhaleBody,s,x,y,z,.105*L,.095*L,.32*L,q,Whale);
                q.Rotate(0,-.05*L,.08*L,out double bx,out double by,out double bz);Put(-1,s,x+bx,y+by,z+bz,.09*L,.06*L,.26*L,q,Belly,false);
                var qt=q*FactoryQ.Axis(1,0,0,tail);q.Rotate(0,0,-.32*L,out double px,out double py,out double pz);qt.Rotate(0,0,-.17*L,out double ox,out double oy,out double oz);
                Put(NativeDeepwellArt.WhaleTail,s,x+px+ox,y+py+oy,z+pz+oz,.05*L,.045*L,.17*L,qt,Whale);
                qt.Rotate(0,0,-.36*L,out double fx,out double fy,out double fz);Put(-1,s,x+px+fx,y+py+fy,z+pz+fz,.15*L,.012*L,.05*L,qt,Whale);
                for(int sd=-1;sd<=1;sd+=2)
                {
                    var qf=q*FactoryQ.Axis(0,0,1,-sd*(.55+fin));q.Rotate(sd*.1*L,-.05*L,.13*L,out double ax,out double ay,out double az);qf.Rotate(sd*.1*L,0,0,out double cx,out double cy,out double cz);
                    Put(sd<0?NativeDeepwellArt.WhaleFinL:NativeDeepwellArt.WhaleFinR,s,x+ax+cx,y+ay+cy,z+az+cz,.1*L,.012*L,.04*L,qf,Whale,false);
                }
            }
            // the jellyfish: the bell squeezing, the arms trailing
            for(int i=0;i<NativeDeepwellLooks.Jellies;i++)
            {
                NativeDeepwellLooks.JellyAt(i,t,out double x,out double y,out double z,out double pulse,out double ax,out double ay,out double az,out double size,out int tint);
                JellyLight(tint,out double r,out double g,out double b);double gain=.75+1.5*pulse;var q=NativeStageArtPose.FromTo(0,1,0,ax,ay,az);int col=Hex(.35+.5*r,.35+.5*g,.35+.5*b);
                double w=size*(1-.2*pulse),hgt=size*.55*(1+.25*pulse);
                Put(NativeDeepwellArt.JellyBell,pulse,x,y,z,w,hgt,w,q,Jelly,false,col,r*gain,g*gain,b*gain);
                double al=size*1.35,aw=size*.6*(1-.3*pulse);
                Put(NativeDeepwellArt.JellyArms,aw,x-ax*(hgt+al),y-ay*(hgt+al),z-az*(hgt+al),size*.1,al,size*.1,q,Jelly,false,col,r*gain*.55,g*gain*.55,b*gain*.55);
                for(int k=0;k<3;k++){double a=k*2.1+i,ox=Math.Sin(a)*aw*.8,oz=Math.Cos(a)*aw*.8;Put(-1,0,x+ox-ax*(hgt+al*.8),y-ay*(hgt+al*.8),z+oz-az*(hgt+al*.8),size*.035,al*.8,size*.035,q,Jelly,false,col);}
            }
            // the schools
            for(int s=0;s<NativeDeepwellLooks.Schools;s++)for(int i=0;i<NativeDeepwellLooks.SchoolFish;i++)
            {
                NativeDeepwellLooks.FishAt(s,i,t,out double x,out double y,out double z,out double heading,out double pitch,out double wag);double len=.2+.12*NativeDeepwellLooks.Hash(s*97+i,15);
                Put(NativeDeepwellArt.Fish,len,x,y,z,len*.12,len*.3,len,FactoryQ.YawPitch(heading+wag*.3,-pitch),Fish,false);
            }
            // the kelp: each stalk a chain of joints leaning on the swell
            for(int i=0;i<NativeDeepwellLooks.KelpStalks;i++)
            {
                NativeDeepwellLooks.KelpRoot(i,out double x,out double z,out double height,out double turn);double y=NativeDeepwellLooks.Seabed-1.0,l=(height+1.0)/NativeDeepwellLooks.KelpJoints;
                for(int k=0;k<NativeDeepwellLooks.KelpJoints;k++)
                {
                    NativeDeepwellLooks.KelpSway(i,k,t,out double lx,out double lz);var q=FactoryQ.Axis(0,1,0,turn)*FactoryQ.Axis(1,0,0,lx)*FactoryQ.Axis(0,0,1,lz);q.Rotate(0,l/2,0,out double cx,out double cy,out double cz);
                    Put(NativeDeepwellArt.Kelp,k,x+cx,y+cy,z+cz,.16,l/2,.03,q,Kelp,false);x+=2*cx;y+=2*cy;z+=2*cz;
                }
            }
            // the survey drone
            NativeDeepwellLooks.Drone(t,out double dx,out double dy,out double dz,out double dh);var dq=FactoryQ.Axis(0,1,0,dh);
            Put(NativeDeepwellArt.Drone,0,dx,dy,dz,.45,.3,.7,dq,Hull);dq.Rotate(0,-.05,.72,out double lx2,out double ly2,out double lz2);Put(-1,0,dx+lx2,dy+ly2,dz+lz2,.14,.14,.04,dq,Lamp,false);
        }
    }
}
