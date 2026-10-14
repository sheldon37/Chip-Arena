using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // The town kit's street level: the sanctuary, the vehicles, cover pieces, spawn doorways and grates, lamps, signals, road
    // paint, litter, blood, graffiti and notices; and the drive (NativeExpeditionCityKit.cs).
    public sealed partial class NativeExpeditionCityKit
    {
        // ---------------- the sanctuary ----------------
        private static readonly int[] BagHexes={0x9A8A64,0x8A7C58,0xA89870,0x7E7252};
        private void Barricade(int i,string s)
        {
            var b=blocks[i];sanctuaryOk=true;bool alongX=b.size.x>b.size.z;int ax=alongX?2:0;
            // Inner face toward the plaza's centre, outer face toward the street.
            int inner=(alongX?b.pos.z:b.pos.x)>0?-1:1;
            foreach(int sg in new[]{inner,-inner})
            {
                var f=MakeFace(i,ax,sg);if(f==null)continue;double len=f.U1-f.U0,h=f.Top-f.Base;
                if(sg==inner)
                {
                    // Sandbags in four staggered courses.
                    for(int c=0;c<3;c++){double off=c%2==0?0:.4;for(double u=f.U0+.45+off;u<f.U1-.3;u+=.82){double s2=Hash(b.id,c*100+(int)(u*10),1);
                        FBox(f,u,.18+c*.34,.06,.78,.32,.12,With(P(NativeBlockLooks.Plain,BagHexes[(int)(s2*BagHexes.Length)%BagHexes.Length],RoleSanctuary),detail:c==1),(s2-.5)*4);}}
                    // Spent casings on the paving behind the bags.
                    int paving=GroundAt(b.pos.x,b.pos.z);if(paving<0)paving=byStem.TryGetValue("sanctuary-paving",out int pv)?pv:-1;
                    for(int k=0;k<6&&paving>=0;k++){double u=f.U0+.4+(len-.8)*Hash(b.id,k,2),d=.15+.9*Hash(b.id,k,3);At(f,u,0,d,out double x,out double z);
                        Box(paving,x,.008,z,.014,.014,.04,With(P(NativeBlockLooks.Plain,BrassHex,RoleGround),detail:true),AxisRot(0,1,0,Hash(b.id,k,4)*180));}
                }
                else
                {
                    // Sheet metal and planks nailed over the outer face; SAFE ZONE painted on the four street ends.
                    for(double u=f.U0+.7;u<f.U1-.5;u+=1.3){double s2=Hash(b.id,(int)(u*10),5);FBox(f,u,.55,.05,1.2,.95,.06,P(NativeBlockLooks.Corrugated,s2<.5?0x6A6E68:0x7A5A3A,RoleSanctuary),(s2-.5)*6);}
                    for(int k=0;k<2;k++)FBox(f,(f.U0+f.U1)/2,.3+k*.5,.11,len-.3,.16,.04,P(NativeBlockLooks.Timber,0x8A6A44,RoleSanctuary),(Hash(b.id,k,6)-.5)*3);
                    // The words face the arm the barricade's gap opens on: its end nearer the street centre line.
                    double gapEnd=Math.Abs(f.U0)<Math.Abs(f.U1)?f.U0:f.U1,mid=(f.U0+f.U1)/2;if(s.EndsWith("-0",StringComparison.Ordinal))Text(f,"SAFE ZONE",gapEnd+(gapEnd<mid?1:-1)*2.0,.22,.13,.09,.01,P(NativeBlockLooks.Plain,0xE8E4D8,RoleSign));
                }
            }
            // A top course of bags.
            double along=alongX?b.size.x:b.size.z;for(double u=-along/2+.45;u<along/2-.3;u+=.86){double s2=Hash(b.id,(int)(u*10),7);
                Box(i,b.pos.x+(alongX?u:0),b.pos.y+b.size.y/2+.07,b.pos.z+(alongX?0:u),alongX?.82:.42,.14,alongX?.42:.82,With(P(NativeBlockLooks.Plain,BagHexes[(int)(s2*BagHexes.Length)%BagHexes.Length],RoleSanctuary),detail:true),AxisRot(0,1,0,(s2-.5)*6));}
            // A lamp post on the barricade's corner end, its arm reaching into the plaza (the four corners).
            double ex=alongX?Math.Sign(b.pos.x)*(Math.Abs(b.pos.x)+b.size.x/2-.4):b.pos.x,ez=alongX?b.pos.z:Math.Sign(b.pos.z)*(Math.Abs(b.pos.z)+b.size.z/2-.4);
            if(alongX&&Math.Abs(ex)>9)SanctuaryLamp(i,ex,ez);
            sanctuaryOk=false;
        }
        private readonly List<double[]> lampHeads=new List<double[]>();
        private void SanctuaryLamp(int i,double x,double z)
        {
            var b=blocks[i];double top=b.pos.y+b.size.y/2;double dx=-Math.Sign(x),dz=-Math.Sign(z);
            Begin();var steel=P(NativeBlockLooks.SteelGreen,0x2A2E2A,RoleSanctuary);
            Box(i,x,top+1.75,z,.14,3.5,.14,With(steel,shadow:true));
            double hx=x+dx*1.0,hz=z+dz*1.0;Box(i,(x+hx)/2,top+3.45,(z+hz)/2,.08,.08,1.45,steel,AxisRot(0,1,0,Math.Atan2(dx,dz)*180/Math.PI));
            Box(i,hx,top+3.32,hz,.44,.2,.44,P(NativeBlockLooks.SteelGreen,0x1E2220,RoleSanctuary));Box(i,hx,top+3.18,hz,.3,.08,.3,Glow(0xFFC27A,1.8f,RoleSanctuary));
            Light(hx,top+3.0,hz,0xFFC27A,1.2,10,1.0,GlowLamp);lampHeads.Add(new[]{x,top+3.4,z});
            End();
        }
        // The string of bulbs between the four lamp posts, sagging between them.
        private void Bulbs()
        {
            if(lampHeads.Count<4||!byStem.TryGetValue("sanctuary-paving",out int paving))return;sanctuaryOk=true;
            lampHeads.Sort((a,c)=>Math.Atan2(a[2],a[0]).CompareTo(Math.Atan2(c[2],c[0])));
            for(int k=0;k<lampHeads.Count;k++)
            {
                var a=lampHeads[k];var c=lampHeads[(k+1)%lampHeads.Count];double len=Math.Sqrt((c[0]-a[0])*(c[0]-a[0])+(c[2]-a[2])*(c[2]-a[2]));int n=(int)(len/1.9);
                double yaw=Math.Atan2(c[0]-a[0],c[2]-a[2])*180/Math.PI;
                for(int j=0;j<n;j++)
                {
                    double t0=(double)j/n,t1=(j+1.0)/n,tm=(t0+t1)/2;double Y(double t)=>a[1]+(c[1]-a[1])*t-1.1*4*t*(1-t);
                    double x=a[0]+(c[0]-a[0])*tm,z=a[2]+(c[2]-a[2])*tm,dy=Y(t1)-Y(t0),run=len/n;
                    Box(paving,x,Y(tm),z,.02,.02,Math.Sqrt(run*run+dy*dy),With(P(NativeBlockLooks.Plain,0x1A1A1A,RoleSanctuary),detail:true),Then(AxisRot(0,1,0,yaw),AxisRot(1,0,0,-Math.Atan2(dy,run)*180/Math.PI)));
                    bool dead=Hash("bulbs",k,j)<.15;Box(paving,x,Y(tm)-.09,z,.07,.1,.07,dead?With(P(NativeBlockLooks.Plain,0x3A3630,RoleSanctuary),detail:true):Glow(Pick(new[]{0xFFD49A,0xFFB870,0xFFE8C0},Hash("bulbs",k,j+50)),1.5f,RoleSanctuary,DriveBulbs,(float)Hash("bulbs",k,j+90)));
                }
            }
            sanctuaryOk=false;
        }
        private void Brazier(int i)
        {
            var b=blocks[i];sanctuaryOk=true;double top=b.pos.y+b.size.y/2,hx=b.size.x/2,hz=b.size.z/2;var drum=P(NativeBlockLooks.SteelGreen,0x2A2622,RoleSanctuary);
            Box(i,b.pos.x,top+.06,b.pos.z-hz+.04,b.size.x,.12,.08,drum);Box(i,b.pos.x,top+.06,b.pos.z+hz-.04,b.size.x,.12,.08,drum);Box(i,b.pos.x-hx+.04,top+.06,b.pos.z,.08,.12,b.size.z-.16,drum);Box(i,b.pos.x+hx-.04,top+.06,b.pos.z,.08,.12,b.size.z-.16,drum);
            Box(i,b.pos.x,top+.03,b.pos.z,b.size.x-.2,.06,b.size.z-.2,Glow(FireHex,1.4f,RoleSanctuary,DriveFire,.2f));
            Box(i,b.pos.x,top+.12,b.pos.z,1.3,.14,.18,P(NativeBlockLooks.Timber,0x3A2418,RoleSanctuary),AxisRot(0,1,0,30));Box(i,b.pos.x,top+.2,b.pos.z,1.2,.14,.16,P(NativeBlockLooks.Timber,0x2A1A12,RoleSanctuary),AxisRot(0,1,0,-50));
            foreach(var (ax,sg) in new[]{(0,1),(0,-1),(2,1),(2,-1)}){var f=MakeFace(i,ax,sg);if(f!=null){FBox(f,(f.U0+f.U1)/2,b.size.y*.75,.012,f.U1-f.U0-.1,b.size.y*.5,.02,With(P(NativeBlockLooks.Plain,SootHex,RoleSanctuary),detail:true));for(int k=0;k<3;k++)FBox(f,f.U0+.35+k*.45,.25,.02,.12,.12,.03,Glow(FireHex,.8f,RoleSanctuary,DriveFire,k*.3f));}}
            Light(b.pos.x,top,b.pos.z,0xFF8A3C,1.8,13,1.2,GlowBrazier,0);
            sanctuaryOk=false;
        }

        // ---------------- the final plaza ----------------
        private void Square(int i,string s)
        {
            var b=blocks[i];double top=b.pos.y+b.size.y/2;
            if(s=="square-dais")
            {
                // The city hall's steps: nosings, a torn runner up the middle, blood down the steps.
                var f=MakeFace(i,2,-1);if(f!=null){FBox(f,0,.3,.015,f.U1-f.U0-.1,.05,.03,P(NativeBlockLooks.Plain,0x5A544C,RoleLandmark));FBox(f,0,.6,.015,f.U1-f.U0-.1,.05,.03,P(NativeBlockLooks.Plain,0x5A544C,RoleLandmark));FBox(f,0,.45,.012,1.6,.9,.02,P(NativeBlockLooks.Plain,0x6A1A1A,RoleLandmark));}
                Box(i,b.pos.x,top+.006,b.pos.z,1.6,.012,b.size.z-.1,P(NativeBlockLooks.Plain,0x6A1A1A,RoleLandmark));Box(i,b.pos.x+2.4,top+.006,b.pos.z-.4,.7,.012,1.6,With(P(NativeBlockLooks.Plain,BloodHex,RoleGround),detail:true),AxisRot(0,1,0,20));
                return;
            }
            // A planter: dark soil and a dead shrub kept low (a pilot may stand on the planter), a cut stump.
            Box(i,b.pos.x,top+.01,b.pos.z,b.size.x-.2,.02,b.size.z-.2,P(NativeBlockLooks.Plain,0x2A2018,RoleLandmark));
            var bark=With(P(NativeBlockLooks.Timber,0x3A2C22,RoleLandmark),detail:true);Box(i,b.pos.x,top+.12,b.pos.z,.3,.24,.3,bark);
            for(int k=0;k<4;k++){double a=k*90+Hash(b.id,k,1)*40;Box(i,b.pos.x+Math.Sin(a*Math.PI/180)*.4,top+.1,b.pos.z+Math.Cos(a*Math.PI/180)*.4,.05,.24,.05,bark,Then(AxisRot(0,1,0,a),AxisRot(1,0,0,50)));}
        }

        // ---------------- vehicles ----------------
        private static readonly HashSet<string> Burnt=new HashSet<string>{"car-av-1","car-ss-0","car-rs-0","bus-es","car-re-0"},Overturned=new HashSet<string>{"car-rw-0","car-rs-1"};
        /// <summary>A box in a block's own frame (centre and size along its axes, optional own turn).</summary>
        private void LBox(int i,double lx,double ly,double lz,double sx,double sy,double sz,Pen pen,Rot extra)
        {
            var b=blocks[i];var q=BlockRot(b);Rotate(q.X,q.Y,q.Z,q.W,lx,ly,lz,out double ox,out double oy,out double oz);Box(i,b.pos.x+ox,b.pos.y+oy,b.pos.z+oz,sx,sy,sz,pen,Then(q,extra));
        }
        private void LBox(int i,double lx,double ly,double lz,double sx,double sy,double sz,Pen pen)=>LBox(i,lx,ly,lz,sx,sy,sz,pen,Rot.None);
        private void Vehicle(int i,string s)
        {
            var b=blocks[i];double hx=b.size.x/2,hy=b.size.y/2,hz=b.size.z/2;bool burnt=Burnt.Contains(s),over=Overturned.Contains(s),bus=Starts(s,"bus-"),truck=Starts(s,"truck-"),amb=Starts(s,"ambulance-"),taxi=Starts(s,"taxi-"),police=Starts(s,"car-fc-");
            var glass=P(NativeBlockLooks.Plain,burnt?BlackHex:GlassHex,RoleVehicle);var tyre=P(NativeBlockLooks.Plain,0x141414,RoleVehicle);var trim=P(NativeBlockLooks.Plain,0x1E1E20,RoleVehicle);
            double wheelR=bus||truck?.52:amb?.42:.32;int axles=bus||truck?3:2;double floorY=over?hy:-hy,up=over?-1:1;
            // Wheels (rims only when burnt), standing 12 cm out of the body; an overturned car shows them on top.
            for(int a=0;a<axles;a++){double z=axles==2?(a==0?hz-.85:-hz+.85):(a==0?hz-1.1:a==1?-hz+2.2:-hz+1.1);
                for(int sd=-1;sd<=1;sd+=2){double wx=sd*(hx-(over?.35:-.05));double wy=over?hy+.05:-hy+wheelR;
                    LBox(i,wx,wy,z,over?.3:.2,wheelR*2,wheelR*2,burnt?P(NativeBlockLooks.SteelGreen,RustHex,RoleVehicle):tyre);if(!burnt&&!over)LBox(i,sd*(hx+.155),wy,z,.012,wheelR*.9,wheelR*.9,With(P(NativeBlockLooks.Plain,0x8A8A88,RoleVehicle),detail:true));}}
            if(over)
            {
                LBox(i,0,hy+.012,0,b.size.x-.3,.024,b.size.z-.4,P(NativeBlockLooks.Corrugated,0x3A2A22,RoleVehicle));LBox(i,.4,hy+.08,-.4,.12,.12,b.size.z*.7,With(P(NativeBlockLooks.SteelGreen,RustHex,RoleVehicle),detail:true));
                for(int sd=-1;sd<=1;sd+=2)LBox(i,sd*(hx+.012),-hy+.3,-.1,.024,.36,b.size.z*.5,glass);
                return;
            }
            double cabY=bus||truck?.3:hy-.32,cabH=bus||truck?.8:.46;
            if(bus)
            {
                // The crashed bus: a band of windows down both sides (many broken), the doors, route 12, NO SERVICE.
                for(int sd=-1;sd<=1;sd+=2)for(double z=-hz+.8;z<hz-.6;z+=1.15){double h2=Hash(b.id,(int)(z*10),sd);LBox(i,sd*(hx+.012),.55,z,.024,1.0,1.0,h2<.45||z>hz-2.5?P(NativeBlockLooks.Plain,BlackHex,RoleVehicle):glass);}
                LBox(i,hx+.015,-.2,hz-1.6,.03,2.4,1.0,P(NativeBlockLooks.Plain,0x1A1A1C,RoleVehicle));
                LBox(i,0,.5,hz+.012,2.3,1.3,.024,P(NativeBlockLooks.Plain,BlackHex,RoleVehicle));LBox(i,0,1.3,hz+.02,1.9,.3,.03,Glow(AmberHex,.25f,RoleVehicle,DriveFlicker,.5f));
                LocalText(i,"NO SERVICE",0,1.24,hz+.04,.025,0xFFB000,1);LocalText(i,"12",-hx+.6,-.4,hz+.03,.07,0xE8E0D0,1);
                LBox(i,0,-.3,0,b.size.x+.03,.25,b.size.z-1,With(P(NativeBlockLooks.Plain,0x8A2A1E,RoleVehicle),detail:true));
            }
            else if(truck)
            {
                LBox(i,0,.0,hz-2.2,b.size.x+.03,b.size.y-.4,.06,P(NativeBlockLooks.Plain,0x2A2A2C,RoleVehicle));
                for(int sd=-1;sd<=1;sd+=2)LBox(i,sd*(hx+.012),.55,hz-1.0,.024,.7,1.2,glass);LBox(i,0,.55,hz+.012,2.1,.7,.024,glass);
                LBox(i,0,.0,-hz-.02,b.size.x-.3,b.size.y-.6,.04,P(NativeBlockLooks.Corrugated,0x8A8478,RoleVehicle));
                for(int sd=-1;sd<=1;sd+=2)LocalSideText(i,"MOVING",sd,.3,-.6,.07,0x2A3A6A);
            }
            else
            {
                for(int sd=-1;sd<=1;sd+=2){LBox(i,sd*(hx+.012),cabY,-.15,.024,cabH,b.size.z*.52,glass);LBox(i,sd*(hx+.02),cabY,-.15,.02,cabH,.08,With(trim,detail:true));}
                LBox(i,0,cabY,hz+.012,b.size.x-.3,cabH,.024,glass);LBox(i,0,cabY,-hz-.012,b.size.x-.3,cabH,.024,glass);
                LBox(i,0,-hy+.28,hz+.03,b.size.x+.02,.2,.08,With(trim,detail:true));LBox(i,0,-hy+.28,-hz-.03,b.size.x+.02,.2,.08,With(trim,detail:true));
            }
            if(!bus&&!burnt){for(int sd=-1;sd<=1;sd+=2){LBox(i,sd*(hx-.35),-hy+.6,hz+.04,.34,.14,.04,With(P(NativeBlockLooks.Plain,0xD8D8C8,RoleVehicle),detail:true));LBox(i,sd*(hx-.3),-hy+.62,-hz-.04,.3,.14,.04,With(P(NativeBlockLooks.Plain,0x8A1A14,RoleVehicle),detail:true));}}
            if(police)
            {
                // Police: black doors with POLICE, a light bar; the bar on car-fc-1 still flashes red and blue.
                for(int sd=-1;sd<=1;sd+=2){LBox(i,sd*(hx+.01),-.2,0,.02,.6,1.9,P(NativeBlockLooks.Plain,0x141416,RoleVehicle));LocalSideText(i,"POLICE",sd,-.32,0,.045,0xE8E8E8);}
                bool live=s=="car-fc-1";LBox(i,0,hy+.07,0,1.2,.12,.3,trim);
                LBox(i,-.32,hy+.14,0,.5,.06,.24,live?Glow(0xFF2020,2.6f,RoleVehicle,DrivePolice,0):P(NativeBlockLooks.Plain,0x5A1A1A,RoleVehicle));LBox(i,.32,hy+.14,0,.5,.06,.24,live?Glow(0x2050FF,2.6f,RoleVehicle,DrivePolice,.5f):P(NativeBlockLooks.Plain,0x1A2A5A,RoleVehicle));
                if(live){var q=BlockRot(b);Rotate(q.X,q.Y,q.Z,q.W,0,hy+.5,0,out double ox,out double oy,out double oz);Light(b.pos.x+ox,b.pos.y+oy,b.pos.z+oz,0xFF2020,1.0,10,.9,GlowPolice,0);}
            }
            if(taxi){for(int sd=-1;sd<=1;sd+=2)for(int k=0;k<8;k++)LBox(i,sd*(hx+.008),-.05,-1.4+k*.4,.016,.1,.2,With(P(NativeBlockLooks.Plain,k%2==0?0x141414:0xE8D8A0,RoleVehicle),detail:true));
                LBox(i,0,hy+.12,-.2,.7,.22,.25,P(NativeBlockLooks.Plain,0xE8C840,RoleVehicle));LocalSideText(i,"TAXI",1,hy+.08,-.2,.03,0x1A1A1A,.13);}
            if(amb)
            {
                for(int sd=-1;sd<=1;sd+=2){LBox(i,sd*(hx+.01),-.35,0,.02,.3,b.size.z-.3,P(NativeBlockLooks.Plain,0xC8401A,RoleVehicle));LocalSideText(i,"AMBULANCE",sd,.15,-.5,.06,0xC8401A);}
                LBox(i,0,hy+.08,hz-.6,1.6,.14,.3,trim);LBox(i,-.45,hy+.16,hz-.6,.5,.06,.24,Glow(RedHex,2.2f,RoleVehicle,DriveStrobe,.25f));LBox(i,.45,hy+.16,hz-.6,.5,.06,.24,P(NativeBlockLooks.Plain,0xD8D8D0,RoleVehicle));
                var q=BlockRot(b);Rotate(q.X,q.Y,q.Z,q.W,0,hy+.6,hz-.6,out double ox,out double oy,out double oz);Light(b.pos.x+ox,b.pos.y+oy,b.pos.z+oz,RedHex,.7,9,.9,GlowBeacon,.25);
                LBox(i,0,-.05,-hz-.012,.04,b.size.y-.4,.024,trim);
            }
            if(burnt)
            {
                // Char and rust over the body, and a scorch on the road round it; the bus's engine still burns.
                for(int sd=-1;sd<=1;sd+=2)LBox(i,sd*(hx+.006),-.1,(Hash(b.id,sd,1)-.5)*b.size.z*.3,.012,b.size.y*.55,b.size.z*.55,With(P(NativeBlockLooks.Plain,SootHex,RoleVehicle),detail:true));
                LBox(i,0,hy+.006,0,b.size.x*.8,.012,b.size.z*.6,P(NativeBlockLooks.Plain,0x1A1612,RoleVehicle));LBox(i,.3,-.2,hz+.008,.6,.4,.016,With(P(NativeBlockLooks.Plain,RustHex,RoleVehicle),detail:true));
                Scorch(i,hx,hz);
                if(bus){var q=BlockRot(b);Rotate(q.X,q.Y,q.Z,q.W,.6,hy,hz-.9,out double ox,out double oy,out double oz);Light(b.pos.x+ox,b.pos.y+oy,b.pos.z+oz,0xFF6A1E,1.6,9,1.5,GlowFire,.4);
                    LBox(i,.6,hy+.03,hz-.9,1.2,.06,1.4,Glow(FireHex,1.3f,RoleVehicle,DriveFire,.4f));}
            }
        }
        private void Scorch(int i,double hx,double hz)
        {
            var b=blocks[i];int g=GroundAt(b.pos.x,b.pos.z);if(g<0)return;var q=BlockRot(b);
            Box(g,b.pos.x,.004,b.pos.z,hx*2+1.6,.008,hz*2+1.4,P(NativeBlockLooks.Plain,0x161412,RoleGround),q);
        }
        /// <summary>Letters on a vehicle's end face (local +z when `dir` is 1), centred at local (x, y).</summary>
        private void LocalText(int i,string text,double x,double y,double z,double cell,int hex,int dir)
        {
            double w=TextWidth(text,cell),x0=x+w/2*dir;
            for(int k=0;k<text.Length;k++)foreach(var g in GlyphBoxes(text[k]))LBox(i,x0-dir*(k*4*cell+(g[0]+g[2]*.5)*cell),y+(5-g[1]-g[3]*.5)*cell,z,g[2]*cell,g[3]*cell,.012,P(NativeBlockLooks.Plain,hex,RoleVehicle));
        }
        /// <summary>Letters on a vehicle's side (local x = side·hx), reading front to back on the right side and back to front on
        /// the left, as a viewer on that side sees them; `hx` the block's half width unless given.</summary>
        private void LocalSideText(int i,string text,int side,double y,double z,double cell,int hex,double hxOverride=-1)
        {
            var b=blocks[i];double hx=hxOverride>0?hxOverride:b.size.x/2,w=TextWidth(text,cell),z0=z-side*w/2;double x=side*(hx+.022);
            for(int k=0;k<text.Length;k++)foreach(var g in GlyphBoxes(text[k]))LBox(i,x,y+(5-g[1]-g[3]*.5)*cell,z0+side*(k*4*cell+(g[0]+g[2]*.5)*cell),.012,g[3]*cell,g[2]*cell,P(NativeBlockLooks.Plain,hex,RoleVehicle));
        }
        private void Barrier(int i)
        {
            var b=blocks[i];bool alongX=b.size.x>b.size.z;
            foreach(int sg in new[]{-1,1}){var f=MakeFace(i,alongX?2:0,sg);if(f==null)continue;for(double u=f.U0+.3;u<f.U1-.2;u+=.8)FBox(f,u,.8,.012,.35,.12,.02,With(P(NativeBlockLooks.Plain,(int)((u-f.U0)/.8)%2==0?0xD83A2A:0xE8E4D8,RoleGround),detail:true));
                if(sg>0)Graffiti(f,(f.U0+f.U1)/2,.25,.012,f.U1-f.U0-.6,Hash(b.id,0,1));}
            // POLICE LINE tape along its top.
            Box(i,b.pos.x,b.pos.y+b.size.y/2+.04,b.pos.z,alongX?b.size.x-.1:.02,.06,alongX?.02:b.size.z-.1,With(P(NativeBlockLooks.Plain,0xE8C830,RoleGround),detail:true));
        }
        private void Dumpster(int i)
        {
            var b=blocks[i];double top=b.pos.y+b.size.y/2;
            Box(i,b.pos.x,top+.03,b.pos.z,b.size.x-.04,.06,b.size.z-.04,P(NativeBlockLooks.SteelGreen,0x2E3A30,RoleClutter),AxisRot(b.size.x>b.size.z?0:1,0,b.size.x>b.size.z?1:0,6));
            for(int k=0;k<3;k++){double s=Hash(b.id,k,1);Box(i,b.pos.x+(s-.5)*b.size.x*.5,top+.24,b.pos.z+(Hash(b.id,k,2)-.5)*b.size.z*.5,.5,.4,.45,P(NativeBlockLooks.Plain,s<.5?0x1A1C1A:0x2A3424,RoleClutter),AxisRot(0,1,0,s*80));}
            foreach(int sg in new[]{-1,1}){var f=MakeFace(i,b.size.x>b.size.z?2:0,sg);if(f!=null)FBox(f,(f.U0+f.U1)/2,b.size.y*.55,.012,f.U1-f.U0-.2,.1,.02,With(P(NativeBlockLooks.Plain,RustHex,RoleClutter),detail:true));}
        }
        private void Crate(int i)
        {
            var b=blocks[i];foreach(var (ax,sg) in new[]{(0,1),(0,-1),(2,1),(2,-1)}){var f=MakeFace(i,ax,sg);if(f==null)continue;FBox(f,(f.U0+f.U1)/2,b.size.y*.3,.012,f.U1-f.U0+.03,.08,.02,P(NativeBlockLooks.SteelGreen,0x3A3A36,RoleClutter));FBox(f,(f.U0+f.U1)/2,b.size.y*.75,.012,f.U1-f.U0+.03,.08,.02,P(NativeBlockLooks.SteelGreen,0x3A3A36,RoleClutter));}
        }
        // A Hulk breach panel: a bricked-up shopfront. Its frame and old fascia are the building's; the X, the notice and the
        // loose bricks are the panel's (they hide with it when the breach opens).
        private void Breach(int i,string s)
        {
            var b=blocks[i];bool x=b.size.x<b.size.z;int ax=x?0:2;
            // The panel stands proud of its building: its outer face is the one away from the building's mass.
            int building=-1;foreach(var kv in facesOf)foreach(var f0 in kv.Value){if(f0.Axis!=ax)continue;double p=x?b.pos.x:b.pos.z;if(Math.Abs(f0.Plane-p)<.35){double u=U(f0,b.pos.x,b.pos.z);if(u>f0.U0&&u<f0.U1){building=kv.Key;break;}}}
            int sg=0;if(building>=0){var bb=blocks[building];sg=(x?b.pos.x-bb.pos.x:b.pos.z-bb.pos.z)>0?1:-1;}else return;
            var f=MakeFace(i,ax,sg);if(f==null)return;double mid=(f.U0+f.U1)/2,w=f.U1-f.U0,h=f.Top-f.Base;
            FBox(f,mid,h/2,.03,w*.9,.22,.03,P(NativeBlockLooks.Plain,0xA82A20,RoleBreach),38);FBox(f,mid,h/2,.06,w*.9,.22,.03,P(NativeBlockLooks.Plain,0xA82A20,RoleBreach),-38);
            Notice(f,mid+w*.22,2.9,.09,false);
            for(int k=0;k<5;k++)FBox(f,mid+(Hash(b.id,k,1)-.5)*w*.8,.4+Hash(b.id,k,2)*3.2,.04,.36,.12,.08,With(P(NativeBlockLooks.BrickBuff,0x8A7A60,RoleBreach),detail:true));
            // The building's frame round the panel: jambs and a lintel, the old shop's fascia above.
            var bf=FaceAt(sg>0?(x?b.pos.x-b.size.x/2:b.pos.x):(x?b.pos.x+b.size.x/2:b.pos.x),sg>0?(x?b.pos.z:b.pos.z-b.size.z/2):(x?b.pos.z:b.pos.z+b.size.z/2),x?sg:0,x?0:sg);
            if(bf!=null){double u=U(bf,b.pos.x,b.pos.z);FBox(bf,u-w/2-.12,2.1,.24,.24,4.2,.48,P(NativeBlockLooks.ConcreteBoard,0x6A6258,RoleBreach));FBox(bf,u+w/2+.12,2.1,.24,.24,4.2,.48,P(NativeBlockLooks.ConcreteBoard,0x6A6258,RoleBreach));
                FBox(bf,u,4.35,.24,w+.6,.3,.48,P(NativeBlockLooks.ConcreteBoard,0x6A6258,RoleBreach));FBox(bf,u,4.9,.08,w+.4,.6,.12,P(NativeBlockLooks.Plain,Pick(FasciaHexes,Hash(b.id,0,9)),RoleSign));
                Text(bf,"CLOSED",u,4.9-2.5*.07,.15,.07,.02,P(NativeBlockLooks.Plain,0xD8C890,RoleSign));}
        }

        // ---------------- spawns ----------------
        private void Spawns()
        {
            var spawns=area?.spawns??Array.Empty<ExpeditionSpawnLayout>();
            foreach(var (f,u,s) in doorways)
            {
                owner=s;string id=blocks[f.Block].id;double h=Hash(id,s,1);
                // A dark doorway in its frame, the torn leaf leaning on the wall beside it, a smear on the frame, a drag mark out.
                FBox(f,u,1.24,.015,1.3,2.48,.03,P(NativeBlockLooks.Plain,0x050506,RoleSpawnDoor));
                FBox(f,u-.72,1.32,.07,.14,2.64,.14,P(NativeBlockLooks.ConcreteBoard,0x5A544C,RoleSpawnDoor));FBox(f,u+.72,1.32,.07,.14,2.64,.14,P(NativeBlockLooks.ConcreteBoard,0x5A544C,RoleSpawnDoor));FBox(f,u,2.6,.07,1.58,.18,.14,P(NativeBlockLooks.ConcreteBoard,0x5A544C,RoleSpawnDoor));
                double side=h<.5?-1:1;if(Open(f,u+side*1.25-.5,u+side*1.25+.5,0,2.3,.02))FBox(f,u+side*1.25,1.1,.06,.9,2.2,.06,P(NativeBlockLooks.Timber,0x3A2A1E,RoleSpawnDoor),side*4);
                FBox(f,u-side*.6,1.3,.15,.12,.7,.012,With(P(NativeBlockLooks.Plain,BloodHex,RoleSpawnDoor),detail:true),side*6);
                int g=SlabAt(f,u);if(g>=0)for(int k=0;k<2;k++){At(f,u+(Hash(id,s,k+2)-.5)*.5,0,.4+k*.7,out double x,out double z);Box(g,x,.004+k*.0005,z,.3+.15*Hash(id,s,k+9),.008,.45,With(P(NativeBlockLooks.Plain,k<2?BloodHex:BloodDryHex,RoleSpawnDoor),detail:true),AxisRot(0,1,0,f.Axis==0?90:0));}
            }
            for(short s=0;s<spawns.Length;s++)
            {
                var e=spawns[s]?.entry;if(e?.kind!="vent"||e.from==null)continue;int g=GroundAt(e.from.x,e.from.z);if(g<0)continue;owner=s;
                // A street grate over the vent: the grating, a dark frame.
                Box(g,e.from.x,.012,e.from.z,1.0,.024,1.0,P(NativeCyberCityLooks.Grate,0x4A4E50,RoleVent,NativeCyberCityLooks.Bank));
                Box(g,e.from.x,.008,e.from.z-.56,1.2,.016,.12,P(NativeBlockLooks.SteelGreen,0x2A2C2C,RoleVent));Box(g,e.from.x,.008,e.from.z+.56,1.2,.016,.12,P(NativeBlockLooks.SteelGreen,0x2A2C2C,RoleVent));
                Box(g,e.from.x-.56,.008,e.from.z,.12,.016,1.0,P(NativeBlockLooks.SteelGreen,0x2A2C2C,RoleVent));Box(g,e.from.x+.56,.008,e.from.z,.12,.016,1.0,P(NativeBlockLooks.SteelGreen,0x2A2C2C,RoleVent));
            }
            owner=-1;Bulbs();
        }

        // ---------------- the street ----------------
        private static readonly double[] LampOffsets={0,.7,-.7,1.3,-1.3,1.9,-1.9};
        private void Street()
        {
            // Wall lamps along the streets (every ~14 m) and alleys (one): working sodium, flickering or dead.
            foreach(var f in faces)
            {
                if(f.Front==0||f.Top-f.Base<6.5)continue;string id=blocks[f.Block].id;if(f.Front==1&&Hash(id,f.Index,200)<.5)continue;double len=f.U1-f.U0,step=f.Front==2?18:40;int n=Math.Max(1,(int)(len/step));
                for(int k=0;k<n;k++)
                {
                    // Between the windows: the first clear spot near the lamp's place along the face.
                    double u0=f.U0+len*(k+.5)/n+(Hash(id,f.Index*7+k,201)-.5)*2,u=double.NaN;foreach(double off in LampOffsets)if(Open(f,u0+off-.4,u0+off+.4,4.4,5.6,.05)){u=u0+off;break;}
                    if(double.IsNaN(u))continue;double s=Hash(id,f.Index*7+k,202);Use(f,u-.45,u+.45,4.4,5.6);
                    Begin();var steel=With(P(NativeBlockLooks.SteelGreen,0x2A2E2C,RoleLamp),fade:true);
                    FBox(f,u,5.12,.58,.08,.12,1.16,steel);FBox(f,u,5.06,1.15,.32,.14,.5,steel);
                    int state=s<.6?0:s<.8?1:2;FBox(f,u,4.97,1.15,.22,.03,.36,state==2?P(NativeBlockLooks.Plain,0x3A3630,RoleLamp):Glow(SodiumHex,1.9f,RoleLamp,state==1?DriveFlicker:DriveSteady,(float)s*11));
                    if(state<2){At(f,u,0,1.15,out double lx,out double lz);Light(lx,f.Base+4.6,lz,SodiumHex,1.0,11,1.0,state==0?GlowLamp:GlowLampFlicker,s*11);}
                    End();
                }
            }
            // Traffic signals on the corners where the arms meet the ring road: flashing amber, the rest dark.
            foreach(var f in faces)
            {
                if(f.Front!=2)continue;var b=blocks[f.Block];string name=Stem(b.id).Substring(5);if(!(name.EndsWith("-shops")||name=="se-row"||name=="sw-row"||name=="ne-tower"||name=="nw-tower"))continue;
                foreach(double u in new[]{f.U0+.6,f.U1-.6})
                {
                    At(f,u,0,0,out double x,out double z);if(Math.Abs(x)<26.5&&Math.Abs(z)<26.5)continue;if(!Open(f,u-.3,u+.3,3.3,4.6,.02))continue;
                    Begin();var housing=With(P(NativeBlockLooks.SteelGreen,0x1E2420,RoleLamp),fade:true);FBox(f,u,3.95,.35,.36,1.0,.3,housing);FBox(f,u,4.15,.2,.08,.08,.3,housing);
                    FBox(f,u,4.27,.51,.18,.18,.03,P(NativeBlockLooks.Plain,0x3A1412,RoleLamp));FBox(f,u,3.95,.51,.18,.18,.03,Glow(AmberHex,2.0f,RoleLamp,DriveBlink,(float)Hash(b.id,(int)u,1)));FBox(f,u,3.63,.51,.18,.18,.03,P(NativeBlockLooks.Plain,0x12301A,RoleLamp));
                    End();
                }
            }
            RoadPaint();Litter();Walls();
        }
        private void RoadPaint()
        {
            var paint=P(NativeBlockLooks.Plain,0xA89A58,RoleGround);var white=P(NativeBlockLooks.Plain,0xB8B4A8,RoleGround);
            void Dashes(double x0,double z0,double x1,double z1,Pen pen){double len=Math.Sqrt((x1-x0)*(x1-x0)+(z1-z0)*(z1-z0));int n=(int)(len/4);for(int k=0;k<n;k++){double t=(k+.5)/n,x=x0+(x1-x0)*t,z=z0+(z1-z0)*t;int g=GroundAt(x,z);if(g<0)continue;
                bool alongX=Math.Abs(x1-x0)>Math.Abs(z1-z0);if(Hash("paint",(int)(x*10),(int)(z*10))<.12)continue;Box(g,x,.004,z,alongX?2:.14,.008,alongX?.14:2,With(pen,detail:true));}}
            Dashes(0,13.5,0,35.5,paint);Dashes(0,-13.5,0,-33.5,paint);Dashes(12.5,0,33.5,0,paint);Dashes(-12.5,0,-33.5,0,paint);
            Dashes(31,-33,31,29,paint);Dashes(-31,-33,-31,29,paint);Dashes(-33,-31,33,-31,paint);Dashes(-35,33,35,33,paint);
            // Crosswalks across the four arms just outside the sanctuary.
            for(int arm=0;arm<4;arm++){for(int k=0;k<6;k++){double a=-4.4+k*1.7;double x=arm<2?a:(arm==2?14.2:-14.2),z=arm<2?(arm==0?14.2:-14.2):a;int g=GroundAt(x,z);if(g<0)continue;
                Box(g,x,.004,z,arm<2?.6:2.2,.008,arm<2?2.2:.6,white);}}
        }
        private bool InBlock(double x,double z,double pad)
        {
            foreach(var b in blocks){var s=Stem(b?.id);if(s==null||Starts(s,"slab-")||s.EndsWith("-paving",StringComparison.Ordinal))continue;var q=BlockRot(b);
                Rotate(q.X,-q.Y,q.Z,q.W,x-b.pos.x,0,z-b.pos.z,out double lx,out double ly,out double lz);if(Math.Abs(lx)<b.size.x/2+pad&&Math.Abs(lz)<b.size.z/2+pad)return true;}
            return false;
        }
        // Litter on the streets (paper, rags), dried blood trails, scattered casings at the forecourt's police line: all flat.
        private void Litter()
        {
            int placed=0;
            for(int k=0;k<900&&placed<120;k++)
            {
                double x=-58+116*Hash("litter",k,1),z=-58+116*Hash("litter",k,2);int g=GroundAt(x,z);if(g<0||InBlock(x,z,.1))continue;double s=Hash("litter",k,3);
                if(Math.Abs(x)<12&&Math.Abs(z)<12)continue;
                int hex=s<.5?Pick(new[]{PaperHex,0xA8A090,0xD8D0C0,0x8A8070},s*7):s<.75?0x3A3430:0x6A5A48;
                Box(g,x,.003+.002*(k%3),z,.18+.3*Hash("litter",k,4),.006,.25+.3*Hash("litter",k,5),With(P(NativeBlockLooks.Plain,hex,RoleGround),detail:true),AxisRot(0,1,0,s*360));placed++;
            }
            for(int t=0;t<14;t++)
            {
                double x=-50+100*Hash("blood",t,1),z=-50+100*Hash("blood",t,2),a=Hash("blood",t,3)*Math.PI*2;if(Math.Abs(x)<12&&Math.Abs(z)<12)continue;
                for(int k=0;k<9;k++){x+=Math.Sin(a)*.55;z+=Math.Cos(a)*.55;a+=(Hash("blood",t,k+4)-.5)*.5;int g=GroundAt(x,z);if(g<0||InBlock(x,z,.1))break;
                    Box(g,x,.004,z,.18+.22*Hash("blood",t,k+20),.008,.5,With(P(NativeBlockLooks.Plain,k<3?BloodHex:BloodDryHex,RoleGround),detail:k>2),AxisRot(0,1,0,a*180/Math.PI));}
            }
            for(int k=0;k<40;k++){double x=-24+50*Hash("casing",k,1),z=30.5+5*Hash("casing",k,2);int g=GroundAt(x,z);if(g<0||InBlock(x,z,.05))continue;Box(g,x,.008,z,.014,.014,.04,With(P(NativeBlockLooks.Plain,BrassHex,RoleGround),detail:true),AxisRot(0,1,0,k*47));}
        }
        // Notices, posters, wayfinding arrows and wall blood at street level.
        private void Walls()
        {
            foreach(var f in faces)
            {
                if(f.Front==0)continue;string id=blocks[f.Block].id;string name=Stem(id).Substring(5);int kind=KindOf(name);if(kind>=Meridian)continue;
                double len=f.U1-f.U0;
                for(int k=0;k<(f.Front==2?2:1);k++){double u=f.U0+1+(len-2)*Hash(id,f.Index*5+k,301);double s=Hash(id,f.Index*5+k,302);double y=kind==Shops?3.55:2.6;
                    if(kind==Shops)continue;if(!Open(f,u-.7,u+.7,y-.6,y+.6,.05))continue;Begin();if(s<.5)Notice(f,u,y,.07,false);else Poster(f,u,y,.07,s);End();}
                // SANCTUARY arrows pointing toward the plaza on faces along the arms and the ring.
                double au=f.U0+1.5+(len-3)*Hash(id,f.Index,304);At(f,au,0,0,out double cx,out double cz);double dist=Math.Sqrt(cx*cx+cz*cz);
                if(f.Front==2&&dist>16&&dist<36&&Hash(id,f.Index,303)<.35)
                {
                    double u=au;double toward=-(Tx(f)*cx+Tz(f)*cz);string word=toward>0?"SANCTUARY >":"< SANCTUARY";double cell=.085;
                    if(Open(f,u-TextWidth(word,cell)/2-.1,u+TextWidth(word,cell)/2+.1,1.6,2.2,.05)){Begin();Text(f,word,u,1.65,.075,cell,.01,P(NativeBlockLooks.Plain,0x5AD87A,RoleSign),(Hash(id,f.Index,305)-.5)*4);End();}
                }
                if(Hash(id,f.Index,306)<.3){double u=f.U0+1+(len-2)*Hash(id,f.Index,307);if(Open(f,u-.5,u+.5,.4,1.8,.05)){Begin();FBox(f,u,1.2,.07,.6,.8,.012,With(P(NativeBlockLooks.Plain,BloodHex,RoleFacade),detail:true),20);FBox(f,u+.15,.65,.07,.07,.6,.012,With(P(NativeBlockLooks.Plain,BloodDryHex,RoleFacade),detail:true));End();}}
            }
        }

        // ---------------- the drive ----------------
        private static double Frac(double v)=>v-Math.Floor(v);
        /// <summary>
        /// A part's linear colour (gain folded in) and window light this frame: steady parts as authored; neon and lamps that
        /// flicker (two quick dips every few seconds), fire (three beating sines), the traffic signals' amber blink, the rotating
        /// beacons, a television's blue flicker, the police bar's red-blue alternation, the bulbs' gentle sway. Pure; allocates nothing.
        /// </summary>
        public static void Drive(in NativeCityPart p,double time,out float r,out float g,out float b,out float level)
        {
            float k=Level(p.Drive,p.Param,time);float gain=p.Gain*k;
            r=(float)NativeSceneryBuilder.Channel(p.Color,16)*gain;g=(float)NativeSceneryBuilder.Channel(p.Color,8)*gain;b=(float)NativeSceneryBuilder.Channel(p.Color,0)*gain;level=p.Level;
        }
        /// <summary>The brightness factor a drive gives at `time` (1 steady).</summary>
        public static float Level(byte drive,float param,double time)
        {
            switch(drive)
            {
                case DriveFlicker:{double t=Frac((time+param*3.7)/(4.5+param%1.5));return t<.06?(t<.03?.15f:.8f):t>.5&&t<.53?.25f:1f;}
                case DriveFire:return (float)(.78+.12*Math.Sin(time*9.1+param*7)+.07*Math.Sin(time*14.3+param*3)+.05*Math.Sin(time*23.7+param));
                case DriveBlink:return Frac(time*.9+param)<.5?1f:.06f;
                case DriveStrobe:{double c=Math.Cos(Frac(time*.8+param)*Math.PI*2);return (float)(.12+1.3*Math.Pow(Math.Max(0,c),6));}
                case DriveTv:{double t=Math.Floor(time*6+param*13);return (float)(.55+.45*Hash("tv",(int)t,(int)(param*100)));}
                case DrivePolice:return Frac(time*1.6+param)<.5?1f:.04f;
                case DriveBulbs:return (float)(.85+.15*Math.Sin(time*1.3+param*20));
                default:return 1f;
            }
        }
    }
}
