using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // The town kit's hub (Claude, 2026-10-15; docs/EXPEDITION-HUB.md, exp-layout-3): the sanctuary's fence (a wooden palisade
    // on the north and west mouths, scrap-metal panels on the east and south), its gate posts, lamps and header signs; the
    // wilds' pieces (containers, boxcars, rubble, ruined walls, booths, sandbag nests, dead trees, a power pylon, floodlight
    // towers, the coal tank, bus shelters) and ground; the distance markers toward the instances. Static dressing like the rest
    // of the kit (NativeExpeditionCityKit.cs): the gates' leaves are NativeExpeditionHubGates', drawn each frame from their state.
    public sealed partial class NativeExpeditionCityKit
    {
        /// <summary>The town's bounds in exp-layout-3 (the map's own are passed in when known): the faces beyond them that look
        /// out of the town are never seen, and the faces' spans stop at them.</summary>
        public const float TownMinX=-96,TownMaxX=96,TownMinZ=-96,TownMaxZ=60;
        private double bx0=TownMinX,bx1=TownMaxX,bz0=TownMinZ,bz1=TownMaxZ;
        private ExpeditionHubLayout hub;
        public const int PalisadeHex=0x6E5236,PalisadeDarkHex=0x4E3A26,ScrapHex=0x5E625E,WarnHex=0xE8C830,WardSignHex=0x8AE89A;

        /// <summary>Builds the kit with the town's hub record (fence gates, wilds, routes) and the map's bounds.</summary>
        public static NativeExpeditionCityKit Build(ContentBlock[] blocks,ExpeditionAreaLayout area,ExpeditionHubLayout hub,MapBounds bounds,bool layers=true)
        {
            var k=new NativeExpeditionCityKit{blocks=blocks??Array.Empty<ContentBlock>(),area=area,hub=hub};
            if(bounds!=null){k.bx0=bounds.minX;k.bx1=bounds.maxX;k.bz0=bounds.minZ;k.bz1=bounds.maxZ;}
            return k.Make(layers);
        }
        /// <summary>Whether a point lies in a wilds district (darker: dead lamps, no lit windows).</summary>
        private bool Wild(double x,double z)=>hub!=null&&hub.InWilds(x,z);
        private bool WildFace(Face f,double u){At(f,u,0,1.5,out double x,out double z);return Wild(x,z);}

        // ---------------- keep-outs ----------------
        // Each hub gate: its closure (to 3.6 m), the threshold either side (nothing stands in front of a gate), and the leaves'
        // travel: a wooden gate's swing over the sanctuary side (to 160 degrees and a little more), a metal gate's pockets behind
        // the fence sections.
        private void HubKeepOuts()
        {
            if(hub==null)return;
            foreach(var g in hub.Gates)
            {
                double half=g.Width/2;
                GateBox(g,-half,half,-.45,.45,3.6);GateBox(g,-half,half,.45,2.6,3.1);GateBox(g,-half,half,-2.6,-.45,3.1);
                // A wooden leaf folds back to 160 degrees, 0.2 m inside the fence plane at its hinge (inside the post) and deeper
                // toward its tip: the palisade's logs and inner battens (to 0.27 m) stay, anything deeper is refused.
                if(g.Wood)GateBox(g,-half-2.12,half+2.12,-2.3,-.28,3.4);else GateBox(g,-half-2.3,half+2.3,-.46,-.2,3.4);
            }
        }
        private void GateBox(ExpeditionHubGate g,double u0,double u1,double d0,double d1,double top)
        {
            double x0=double.MaxValue,x1=double.MinValue,z0=double.MaxValue,z1=double.MinValue;
            foreach(var (u,d) in new[]{(u0,d0),(u1,d0),(u0,d1),(u1,d1)}){NativeExpeditionHubGates.World(g,u,0,d,out double x,out _,out double z);x0=Math.Min(x0,x);x1=Math.Max(x1,x);z0=Math.Min(z0,z);z1=Math.Max(z1,z);}
            keeps.Add(new NativeCityKeep{X0=(float)x0,X1=(float)x1,Y0=-.2f,Y1=(float)top,Z0=(float)z0,Z1=(float)z1,Spawn=-1,Kind=2});
        }

        // ---------------- the fence ----------------
        private static readonly int[] ScrapHexes={0x6A3A22,0x8A9094,0x3A5068,0x4A5A3E,0x7A3028,0x9A8A5A,0x5E625E};
        /// <summary>A fence section: palisade logs or scrap sheets over both faces, the gate post at its gate end (lamp on top),
        /// sandbags along its inner foot clear of the leaves, razor wire or pointed tops above it.</summary>
        private void Fence(int i,string s)
        {
            var b=blocks[i];sanctuaryOk=true;bool wood=s.Contains("-wood-");bool alongX=b.size.x>b.size.z;int ax=alongX?2:0;
            int outer=(alongX?b.pos.z:b.pos.x)>0?1:-1;double len=alongX?b.size.x:b.size.z,top=b.pos.y+b.size.y/2,thick=alongX?b.size.z:b.size.x;
            // The section's gate end: the end nearer the mouth's centre line.
            double along=alongX?b.pos.x:b.pos.z,gateEnd=Math.Abs(along-len/2)<Math.Abs(along+len/2)?along-len/2:along+len/2,dir=Math.Sign(gateEnd-along);
            double X(double a)=>alongX?a:b.pos.x;double Z(double a)=>alongX?b.pos.z:a;
            if(wood)
            {
                // Logs side by side through the section's thickness, their tops ragged, each with a stepped point.
                int n=(int)Math.Floor(len/.3);double pitch=len/n;
                for(int k=0;k<n;k++)
                {
                    double a=along-len/2+pitch*(k+.5),h=top+.05+.3*Hash(b.id,k,1);int hex=Pick(new[]{PalisadeHex,PalisadeDarkHex,0x7A5A3A,0x5A4430},Hash(b.id,k,2));
                    Box(i,X(a),h/2,Z(a),alongX?pitch-.03:thick+.04,h,alongX?thick+.04:pitch-.03,P(NativeBlockLooks.Timber,hex,RoleSanctuary),AxisRot(0,1,0,(Hash(b.id,k,3)-.5)*6));
                    Box(i,X(a),h+.06,Z(a),alongX?pitch*.66:thick*.7,.12,alongX?thick*.7:pitch*.66,P(NativeBlockLooks.Timber,hex,RoleSanctuary));Box(i,X(a),h+.17,Z(a),alongX?pitch*.3:thick*.34,.1,alongX?thick*.34:pitch*.3,With(P(NativeBlockLooks.Timber,hex,RoleSanctuary),detail:true));
                }
                // Two battens on the inner face; scrap planks, claw marks and a smear on the outer face; SAFE painted on one.
                var fi=MakeFace(i,ax,-outer);if(fi!=null)foreach(double y in new[]{.7,2.5})FBox(fi,(fi.U0+fi.U1)/2,y,.04,fi.U1-fi.U0-.1,.18,.06,P(NativeBlockLooks.Timber,PalisadeDarkHex,RoleSanctuary));
                var fo=MakeFace(i,ax,outer);
                if(fo!=null)
                {
                    double mid=(fo.U0+fo.U1)/2;FBox(fo,mid+(Hash(b.id,0,5)-.5),1.4,.04,1.3,.22,.04,P(NativeBlockLooks.Timber,0x8A6A44,RoleSanctuary),(Hash(b.id,0,6)-.5)*14);
                    for(int k=0;k<3;k++)FBox(fo,fo.U0+.5+(fo.U1-fo.U0-1)*Hash(b.id,k,7),.6+.5*Hash(b.id,k,8),.035,.05,.5,.012,With(P(NativeBlockLooks.Plain,0x2A1A12,RoleSanctuary),detail:true),12+8*k);
                    FBox(fo,fo.U0+.6+(fo.U1-fo.U0-1.2)*Hash(b.id,1,9),.7,.035,.5,.6,.012,With(P(NativeBlockLooks.Plain,BloodDryHex,RoleSanctuary),detail:true),25);
                    if(s.EndsWith("-0",StringComparison.Ordinal))Text(fo,"SAFE",mid,1.9,.045,.09,.01,P(NativeBlockLooks.Plain,0xE8E4D8,RoleSign),(Hash(b.id,0,10)-.5)*5);
                }
            }
            else
            {
                // Scrap sheets lapped on the outer face (mismatched, a few degrees off true), I-beam posts, a top rail, razor
                // wire above, KEEP OUT stencilled; on the inner face only the posts beyond the leaves' pocket.
                var fo=MakeFace(i,ax,outer);
                if(fo!=null)
                {
                    double u=fo.U0+.02;int k=0;while(u<fo.U1-.15){double w=Math.Min(fo.U1-.02-u,.9+.5*Hash(b.id,k,11));double hgt=2.85+.4*Hash(b.id,k,12);
                        FBox(fo,u+w/2,hgt/2+.02,.03,w+.06,hgt,.04,P(k%3==1?NativeBlockLooks.Plain:NativeBlockLooks.Corrugated,Pick(ScrapHexes,Hash(b.id,k,13)),RoleSanctuary),(Hash(b.id,k,14)-.5)*3);u+=w-.04;k++;}
                    for(double pu=fo.U0+.15;pu<fo.U1;pu+=1.45)FBox(fo,pu,1.65,.1,.14,3.3,.12,With(P(NativeBlockLooks.SteelGreen,0x3A3C3A,RoleSanctuary),shadow:true));
                    FBox(fo,(fo.U0+fo.U1)/2,3.18,.08,fo.U1-fo.U0,.1,.1,P(NativeBlockLooks.SteelGreen,0x3A3C3A,RoleSanctuary));
                    Text(fo,"KEEP OUT",(fo.U0+fo.U1)/2,1.2,.065,.08,.01,P(NativeBlockLooks.Plain,0xE8E4D8,RoleSign),(Hash(b.id,0,15)-.5)*4);
                    FBox(fo,fo.U0+.6+(fo.U1-fo.U0-1.2)*Hash(b.id,2,16),.6,.06,.6,.5,.012,With(P(NativeBlockLooks.Plain,BloodDryHex,RoleSanctuary),detail:true),-20);
                }
                // Razor wire: a zigzag of short bars along the top.
                for(double a=along-len/2+.15;a<along+len/2-.1;a+=.3){bool up=((int)Math.Floor((a-along)/.3)&1)==0;
                    Box(i,X(a),top+.32,Z(a),alongX?.34:.03,.03,alongX?.03:.34,With(P(NativeBlockLooks.SteelGreen,0x8A8A84,RoleSanctuary),detail:true),AxisRot(alongX?0:1,0,alongX?1:0,up?40:-40));}
            }
            // Sandbags along the inner foot, clear of the leaves (the keep-outs refuse any bag in their travel).
            {
                // A thin course hugging the fence (nothing below 3.1 m stands more than 0.25 m off a block).
                double inner=(alongX?b.pos.z:b.pos.x)-outer*(thick/2+.125);
                for(int c=0;c<2;c++)for(double a=along-len/2+.4+(c%2)*.4;a<along+len/2-.3;a+=.82)
                {
                    double s2=Hash(b.id,c*100+(int)(a*10),20);double x=alongX?a:inner,z=alongX?inner:a;
                    Box(i,x,.16+c*.3,z,alongX?.78:.22,.3,alongX?.22:.78,With(P(NativeBlockLooks.Plain,BagHexes[(int)(s2*BagHexes.Length)%BagHexes.Length],RoleSanctuary),detail:c==1),AxisRot(0,1,0,(s2-.5)*3));
                }
            }
            // The gate post at the gate end, inside the section's footprint: a heavy post to 4.4 m, a lamp arm into the plaza.
            {
                double pa=gateEnd-dir*.25,pc=alongX?b.pos.z:b.pos.x;double px=alongX?pa:pc,pz=alongX?pc:pa;double inner=-outer;
                // The post stands flush with the inner face (the leaves' travel starts there) and 5 cm proud of the outer one.
                double dc=(.05-0)/2*outer;double cx=alongX?px:px+dc,cz=alongX?pz+dc:pz;
                Box(i,cx,2.2,cz,alongX?.5:thick+.05,4.4,alongX?thick+.05:.5,With(P(wood?NativeBlockLooks.Timber:NativeBlockLooks.SteelGreen,wood?0x4E3A26:0x34383A,RoleSanctuary),shadow:true));
                Box(i,cx,4.45,cz,alongX?.6:thick+.15,.1,alongX?thick+.15:.6,P(wood?NativeBlockLooks.Timber:NativeBlockLooks.SteelGreen,wood?0x3A2A1E:0x2A2C2C,RoleSanctuary));
                // Hazard paint on a metal post's outer edge; iron hinge pins on a wooden one.
                if(!wood)for(int k=0;k<4;k++)Box(i,alongX?cx:cx+outer*(thick/2+.03),.45+k*.5,alongX?cz+outer*(thick/2+.03):cz,alongX?.5:.012,.24,alongX?.012:.5,With(P(NativeBlockLooks.Plain,k%2==0?WarnHex:0x1A1A1A,RoleSanctuary),detail:true));
                Begin();double hx=alongX?px:px+inner*1.0,hz=alongX?pz+inner*1.0:pz,ty=4.5;var steel=P(NativeBlockLooks.SteelGreen,0x2A2E2A,RoleSanctuary);
                Box(i,(cx+hx)/2,ty+.35,(cz+hz)/2,alongX?.08:1.0,.08,alongX?1.0:.08,steel);Box(i,hx,ty+.25,hz,.42,.18,.42,P(NativeBlockLooks.SteelGreen,0x1E2220,RoleSanctuary));
                bool dead=Hash(b.id,0,30)<.12;Box(i,hx,ty+.13,hz,.3,.06,.3,dead?P(NativeBlockLooks.Plain,0x3A3630,RoleSanctuary):Glow(0xFFC27A,1.8f,RoleSanctuary,Hash(b.id,0,31)<.2?DriveFlicker:DriveSteady,(float)Hash(b.id,0,32)*9));
                if(!dead)Light(hx,ty-.1,hz,0xFFC27A,1.1,10,1.0,GlowLamp);lampHeads.Add(new[]{hx,ty+.2,hz});End();
            }
            sanctuaryOk=false;
        }
        /// <summary>Each hub gate's header: a beam across the opening above a Hulk (4.0-4.35 m) between its posts, SANCTUARY on
        /// the street side, where the gate leads on the plaza side (the route's name and walked distance).</summary>
        private void HubHeaders()
        {
            if(hub==null)return;
            foreach(var g in hub.Gates)
            {
                int parent=-1;foreach(var id in g.Fence)if(byStem.TryGetValue(Stem(id)??"",out int p)){parent=p;break;}if(parent<0)continue;
                double half=g.Width/2;bool wood=g.Wood;sanctuaryOk=true;
                NativeExpeditionHubGates.World(g,0,4.18,0,out double cx,out double cy,out double cz);double a=g.Yaw*Math.PI/180;bool ew=Math.Abs(Math.Sin(a))>.5;
                Box(parent,cx,cy,cz,ew?.3:g.Width+1.0,.35,ew?g.Width+1.0:.3,With(P(wood?NativeBlockLooks.Timber:NativeBlockLooks.SteelGreen,wood?0x4E3A26:0x3A3C3E,RoleSign),shadow:true));
                // Pseudo faces in the gate's plane (u along the fence, centred on the gate): out toward the street, in toward the plaza.
                double plane=ew?g.X:g.Z;int outSign=(ew?Math.Sin(a):Math.Cos(a))>0?1:-1;
                var fo=new Face{Block=parent,Axis=ew?0:2,Sign=outSign,Plane=plane,U0=-50,U1=50,Base=0,Top=10};var fi=new Face{Block=parent,Axis=ew?0:2,Sign=-outSign,Plane=plane,U0=-50,U1=50,Base=0,Top=10};
                FBox(fo,0,4.18,.16,3.6,.3,.03,P(NativeBlockLooks.Plain,wood?0x2A3A24:0x1E2A2A,RoleSign));Text(fo,"SANCTUARY",0,4.18-2.5*.05,.18,.05,.012,Glow(WardSignHex,1.1f,RoleSign,DriveSteady));
                string to=g.Side=="north"?"FINAL GATE":"";double dist=0;
                foreach(var r in hub.Routes)if(r.Gate==g.Id){to=r.Reserved?(r.To=="exp-foundry"?"IRONWORKS":"STATION")+" SEALED":r.To=="exp-ward-zero"?"WARD ZERO":r.To.ToUpperInvariant();dist=r.Reserved?0:r.Path;}
                string label=dist>0?to+" "+Metres(dist)+" M":to;
                if(label.Length>0){double cell=Math.Min(.05,3.4/(4*label.Length-1));FBox(fi,0,4.18,.16,TextWidth(label,cell)+.4,.3,.03,P(NativeBlockLooks.Plain,0x1A1A1A,RoleSign));Text(fi,label,0,4.18-2.5*cell,.18,cell,.012,P(NativeBlockLooks.Plain,dist>0?0xE8F0F4:0xB8B0A0,RoleSign));}
                sanctuaryOk=false;
            }
        }

        // ---------------- the wilds' pieces ----------------
        private void WildPiece(int i,string s)
        {
            var b=blocks[i];double hx=b.size.x/2,hy=b.size.y/2,hz=b.size.z/2,top=b.pos.y+hy;bool alongX=b.size.x>=b.size.z;
            if(Starts(s,"wild-container-"))
            {
                // A shipping container: ribs on its long sides, doors and lock bars at one end, a stencilled code, rust runs.
                foreach(int sg in new[]{-1,1})
                {
                    var f=MakeFace(i,alongX?2:0,sg);if(f!=null){for(double u=f.U0+.25;u<f.U1-.2;u+=.55)FBox(f,u,(f.Top-f.Base)/2,.02,.08,f.Top-f.Base-.1,.04,With(P(NativeBlockLooks.Corrugated,Pick(new[]{0x6A4434,0x3A5068,0x4A5A3E},Hash(b.id,0,1)),RoleClutter),detail:true));
                        FBox(f,(f.U0+f.U1)/2+(Hash(b.id,sg,2)-.5)*2,1.2,.05,.8,1.6,.012,With(P(NativeBlockLooks.Plain,RustHex,RoleClutter),detail:true));
                        if(sg>0)Text(f,"MRD 44"+((int)(Hash(b.id,0,3)*90)+10).ToString(System.Globalization.CultureInfo.InvariantCulture),(f.U0+f.U1)/2,(f.Top-f.Base)*.7,.06,.07,.01,P(NativeBlockLooks.Plain,0xD8D0C0,RoleSign));}
                    var e=MakeFace(i,alongX?0:2,sg);if(e!=null&&sg>0){double mid=(e.U0+e.U1)/2;FBox(e,mid,(e.Top-e.Base)/2,.03,e.U1-e.U0-.1,e.Top-e.Base-.1,.05,P(NativeBlockLooks.SteelGreen,0x5A3A2E,RoleClutter));
                        for(int k=-2;k<=2;k++)if(k!=0)FBox(e,mid+k*.5,(e.Top-e.Base)/2,.08,.05,e.Top-e.Base-.3,.05,P(NativeBlockLooks.SteelGreen,0x8A8A84,RoleClutter));}
                }
            }
            else if(Starts(s,"wild-boxcar-"))
            {
                // A boxcar: a dark underframe band, its sliding door, ladder rungs at the ends, reporting marks and a tag.
                foreach(int sg in new[]{-1,1})
                {
                    var f=MakeFace(i,alongX?2:0,sg);if(f==null)continue;double mid=(f.U0+f.U1)/2;
                    FBox(f,mid,.45,.04,f.U1-f.U0,.9,.06,P(NativeBlockLooks.SteelGreen,0x1E1C1A,RoleClutter));
                    for(int k=-1;k<=1;k+=2){FBox(f,mid+k*4.2,.35,.08,1.6,.55,.1,P(NativeBlockLooks.SteelGreen,0x141210,RoleClutter));}
                    FBox(f,mid+(Hash(b.id,sg,4)-.5)*1.5,2.5,.06,2.6,3.0,.06,P(NativeBlockLooks.Corrugated,0x4A2A20,RoleClutter));FBox(f,mid,4.05,.08,f.U1-f.U0-.2,.08,.08,P(NativeBlockLooks.SteelGreen,0x2A2622,RoleClutter));
                    if(sg>0)Text(f,"IW "+((int)(Hash(b.id,0,7)*40)+2).ToString(System.Globalization.CultureInfo.InvariantCulture),mid-3.2,3.3,.07,.06,.01,P(NativeBlockLooks.Plain,0xD8D0C0,RoleSign));
                    if(Hash(b.id,sg,5)<.6)Graffiti(f,mid+2.6,1.5,.07,2.2,Hash(b.id,sg,6));
                    for(int r=0;r<6;r++)FBox(f,f.U0+.25,1.2+r*.42,.1,.4,.04,.04,With(P(NativeBlockLooks.SteelGreen,0x2A2622,RoleClutter),detail:true));
                }
                Box(i,b.pos.x,top+.04,b.pos.z,alongX?b.size.x-.4:.6,.08,alongX?.6:b.size.z-.4,With(P(NativeBlockLooks.Timber,0x3A2A1E,RoleHigh),detail:true));
            }
            else if(Starts(s,"wild-rubble-"))
            {
                // Concrete slabs tipped at angles, broken bricks and rebar sticking out of the heap.
                // Every chunk stays inside the heap's footprint grown by 0.2 m (nothing below 3.1 m stands off a block).
                for(int k=0;k<9;k++){double sz=.45+Hash(b.id,k,3)*.5,u=(Hash(b.id,k,1)-.5)*2*Math.Max(0,hx-sz*.55),w=(Hash(b.id,k,2)-.5)*2*Math.Max(0,hz-sz*.55);
                    Box(i,b.pos.x+u,top+.05+.15*Hash(b.id,k,4),b.pos.z+w,sz,.22,sz*.7,P(k%3==0?NativeBlockLooks.BrickRed:NativeBlockLooks.ConcreteBoard,k%3==0?0x6A3A2E:Pick(new[]{0x6A6460,0x5A5450,0x7A746C},Hash(b.id,k,5)),RoleClutter),Then(AxisRot(0,1,0,Hash(b.id,k,6)*180),AxisRot(1,0,0,(Hash(b.id,k,7)-.5)*40)));}
                for(int k=0;k<4;k++){double u=(Hash(b.id,k,8)-.5)*2*Math.Max(0,hx-.5),w=(Hash(b.id,k,9)-.5)*2*Math.Max(0,hz-.5);Box(i,b.pos.x+u,top+.4,b.pos.z+w,.03,.9,.03,With(P(NativeBlockLooks.SteelGreen,RustHex,RoleClutter),detail:true),Then(AxisRot(0,1,0,k*70),AxisRot(1,0,0,25+k*8)));}
                // A mound, not a plinth: big broken slabs stacked toward the middle, and slabs leaning on every side (the leaning
                // ones within 0.25 m of the heap's faces).
                for(int k=0;k<3;k++){double sz=1.1+.6*Hash(b.id,k,12),u=(Hash(b.id,k,10)-.5)*Math.Max(0,hx-sz*.6),w=(Hash(b.id,k,11)-.5)*Math.Max(0,hz-sz*.6);
                    Box(i,b.pos.x+u,top+.16+k*.15,b.pos.z+w,sz,.32,sz*.72,P(NativeBlockLooks.ConcreteBoard,Pick(new[]{0x6A6460,0x5A5450,0x7A746C},Hash(b.id,k,13)),RoleClutter),Then(AxisRot(0,1,0,Hash(b.id,k,14)*180),AxisRot(1,0,0,(Hash(b.id,k,15)-.5)*22)));}
                foreach(int ax in new[]{0,2})foreach(int sg in new[]{-1,1})
                {
                    var f=MakeFace(i,ax,sg);if(f==null)continue;
                    for(int k=0;k<3;k++){double wdt=.8+.5*Hash(b.id,ax*10+sg*3+k,16),ht=.5+.3*Hash(b.id,ax*10+sg*3+k,17),u=f.U0+.12+wdt/2+(f.U1-f.U0-.24-wdt)*k/2.0;
                        FBox(f,u,ht/2,.06,wdt,ht,.1,P(k==1?NativeBlockLooks.BrickRed:NativeBlockLooks.ConcreteBoard,k==1?0x5E3A30:Pick(new[]{0x6A6460,0x5A5450,0x7A746C},Hash(b.id,ax*10+sg*3+k,18)),RoleClutter),(Hash(b.id,ax*10+sg*3+k,19)-.5)*14,8+8*Hash(b.id,ax*10+sg*3+k,20));}
                }
            }
            else if(Starts(s,"wild-wall-"))
            {
                // A ruined wall stub: a broken stepped top, a blind window, soot and a tag.
                double len=alongX?b.size.x:b.size.z;int n=(int)(len/.6);
                for(int k=0;k<n;k++){double a=-len/2+len*(k+.5)/n,h=Hash(b.id,k,1)<.45?0:.2+.9*Hash(b.id,k,2);if(h<=0)continue;Box(i,b.pos.x+(alongX?a:0),top+h/2,b.pos.z+(alongX?0:a),alongX?len/n-.02:b.size.x,h,alongX?b.size.z:len/n-.02,P(NativeBlockLooks.BrickRed,Pick(new[]{0x5E3A30,0x6A4034,0x4E3028},Hash(b.id,k,3)),RoleClutter));}
                foreach(int sg in new[]{-1,1}){var f=MakeFace(i,alongX?2:0,sg);if(f==null)continue;double mid=(f.U0+f.U1)/2;FBox(f,mid+(Hash(b.id,sg,4)-.5)*(len-2),1.4,.02,.9,1.0,.04,P(NativeBlockLooks.Plain,BlackHex,RoleClutter));
                    FBox(f,mid,(f.Top-f.Base)-.4,.012,len*.6,.8,.02,With(P(NativeBlockLooks.Plain,SootHex,RoleClutter),detail:true));if(sg>0)Graffiti(f,mid+len*.25,.6,.02,1.6,Hash(b.id,sg,5));}
            }
            else if(Starts(s,"wild-booth-"))
            {
                // A checkpoint or ticket booth: dark windows round it, a roof slab, a sign; the boom stands raised.
                foreach(var (ax,sg) in new[]{(0,1),(0,-1),(2,1),(2,-1)}){var f=MakeFace(i,ax,sg);if(f==null)continue;FBox(f,(f.U0+f.U1)/2,1.7,.02,f.U1-f.U0-.5,.9,.04,P(NativeBlockLooks.Plain,Hash(b.id,ax+sg,1)<.5?BlackHex:0x2A3A44,RoleClutter));}
                Box(i,b.pos.x,top+.1,b.pos.z,b.size.x+.36,.2,b.size.z+.36,P(NativeBlockLooks.ConcreteBoard,0x6A665E,RoleHigh));
                var fs=MakeFace(i,2,1);bool station=s.Contains("-s-");if(fs!=null){FBox(fs,(fs.U0+fs.U1)/2,3.36,.1,2.2,.4,.04,P(NativeBlockLooks.Plain,station?0x1E2E2A:0x8A1A16,RoleSign));Text(fs,station?"TICKETS":"CHECKPOINT",(fs.U0+fs.U1)/2,3.27,.13,.035,.01,P(NativeBlockLooks.Plain,0xE8E0D0,RoleSign));}
                if(!station){Box(i,b.pos.x+hx+.08,top+1.4,b.pos.z+hz-.2,.1,2.8,.1,P(NativeBlockLooks.Plain,0xD8302A,RoleClutter));for(int k=0;k<4;k++)Box(i,b.pos.x+hx+.08,top+.4+k*.7,b.pos.z+hz-.2,.12,.3,.12,With(P(NativeBlockLooks.Plain,0xE8E4D8,RoleClutter),detail:true));}
            }
            else if(Starts(s,"wild-sandbag-"))
            {
                foreach(int sg in new[]{-1,1}){var f=MakeFace(i,alongX?2:0,sg);if(f==null)continue;for(int c=0;c<3;c++){double off=c%2==0?0:.4;for(double u=f.U0+.45+off;u<f.U1-.3;u+=.82){double s2=Hash(b.id,c*100+(int)(u*10)+sg,1);
                    FBox(f,u,.18+c*.34,.06,.78,.32,.12,With(P(NativeBlockLooks.Plain,BagHexes[(int)(s2*BagHexes.Length)%BagHexes.Length],RoleClutter),detail:c==1),(s2-.5)*4);}}}
                Box(i,b.pos.x,top+.05,b.pos.z,b.size.x-.2,.1,b.size.z-.1,With(P(NativeBlockLooks.Plain,0x3A4A34,RoleClutter),detail:true),AxisRot(0,1,0,3));
            }
            else if(Starts(s,"wild-tree-"))
            {
                // A dead tree: bark on the trunk, a fork above head height and bare limbs reaching out over the street.
                foreach(var (ax,sg) in new[]{(0,1),(0,-1),(2,1),(2,-1)}){var f=MakeFace(i,ax,sg);if(f!=null)FBox(f,(f.U0+f.U1)/2,1.6,.02,.5,3.0,.04,With(P(NativeBlockLooks.Timber,0x221A14,RoleClutter),detail:true));}
                double baseAng=Hash(b.id,0,1)*360;Box(i,b.pos.x,top+.9,b.pos.z,.4,1.8,.4,With(P(NativeBlockLooks.Timber,0x2A221C,RoleHigh),shadow:true));
                for(int k=0;k<5;k++)
                {
                    double ang=baseAng+k*72+Hash(b.id,k,2)*30,tilt=30+30*Hash(b.id,k,3),len=1.6+1.8*Hash(b.id,k,4),y0=top+1.4+.4*k/5;double r=ang*Math.PI/180,t=tilt*Math.PI/180;
                    double cx=b.pos.x+Math.Sin(r)*Math.Sin(t)*len/2,cz=b.pos.z+Math.Cos(r)*Math.Sin(t)*len/2,cy=y0+Math.Cos(t)*len/2;
                    Box(i,cx,cy,cz,.12,len,.12,With(P(NativeBlockLooks.Timber,0x2A221C,RoleHigh),fade:true),Then(AxisRot(0,1,0,ang),AxisRot(1,0,0,tilt)));
                    double tx=b.pos.x+Math.Sin(r)*Math.Sin(t)*len,tz=b.pos.z+Math.Cos(r)*Math.Sin(t)*len,ty=y0+Math.Cos(t)*len;double l2=.9+.7*Hash(b.id,k,5),a2=ang+40*(Hash(b.id,k,6)-.5);
                    double r2=a2*Math.PI/180;Box(i,tx+Math.Sin(r2)*.35*l2,ty+.3*l2,tz+Math.Cos(r2)*.35*l2,.07,l2,.07,With(P(NativeBlockLooks.Timber,0x2A221C,RoleHigh),detail:true,fade:true),Then(AxisRot(0,1,0,a2),AxisRot(1,0,0,45)));
                }
            }
            else if(Starts(s,"wild-pylon-"))
            {
                // A lattice power pylon on its footing: four legs tapering to 19 m, X bracing every 3 m, crossarms with
                // insulators, a red aviation light. Every part above the footing stays inside its footprint below 3.1 m.
                Begin();double h0=top,H=19,w0=Math.Min(hx,hz)-.1,w1=.45;var steel=P(NativeBlockLooks.SteelGreen,0x4A4E4C,RoleHigh);
                double W(double y)=>w0+(w1-w0)*(y-h0)/H;
                foreach(var (sx,sz) in new[]{(-1,-1),(1,-1),(1,1),(-1,1)})
                {
                    double lean=Math.Atan2(w0-w1,H)*180/Math.PI;Box(i,b.pos.x+sx*(w0+w1)/2,h0+H/2,b.pos.z+sz*(w0+w1)/2,.14,Math.Sqrt(H*H+(w0-w1)*(w0-w1)*2),.14,With(steel,shadow:true),Then(AxisRot(0,1,0,Math.Atan2(sx,sz)*180/Math.PI),AxisRot(1,0,0,-lean)));
                }
                for(double y=h0+3;y<h0+H-2;y+=3)
                {
                    double wa=W(y),wb=W(y+3);
                    for(int f=0;f<4;f++){double ux=f<2?0:1,uz=f<2?1:0,sgn=f%2==0?1:-1;double cxp=b.pos.x+(f>=2?sgn*(wa+wb)/2:0),czp=b.pos.z+(f<2?sgn*(wa+wb)/2:0);double span=Math.Sqrt(9+(wa+wb)*(wa+wb));
                        foreach(int d in new[]{-1,1})Box(i,cxp,y+1.5,czp,f<2?.05:.05,span,f<2?.05:.05,With(steel,detail:true),Then(AxisRot(0,1,0,f<2?0:90),AxisRot(0,0,1,d*Math.Atan2((wa+wb),3)*180/Math.PI)));}
                }
                foreach(double y in new[]{h0+H-3.5,h0+H-1.2}){Box(i,b.pos.x,y,b.pos.z,5.6,.18,.18,steel);foreach(int k in new[]{-1,1})Box(i,b.pos.x+k*2.6,y-.4,b.pos.z,.1,.7,.1,P(NativeBlockLooks.Plain,0x8AA0A0,RoleHigh));}
                Box(i,b.pos.x,h0+H+.3,b.pos.z,.3,.3,.3,Glow(RedHex,2.2f,RoleHigh,DriveBlink,(float)Hash(b.id,0,9)));End();
            }
            else if(Starts(s,"wild-flood-"))
            {
                // A floodlight tower: a pole to 7 m, a head of two lamps aimed down the district (one dead on some).
                Begin();var steel=P(NativeBlockLooks.SteelGreen,0x2A2E2C,RoleHigh);Box(i,b.pos.x,top+2.8,b.pos.z,.18,5.6,.18,With(steel,shadow:true));
                double tx=-Math.Sign(b.pos.x),tz=-Math.Sign(b.pos.z);if(Math.Abs(b.pos.x)<Math.Abs(b.pos.z))tx=0;else tz=0;
                Box(i,b.pos.x,top+5.7,b.pos.z,tx!=0?.2:1.4,.2,tx!=0?1.4:.2,steel);bool cold=b.pos.x>40;bool dead2=Hash(b.id,0,1)<.5;
                foreach(int k in new[]{-1,1}){double ox=tx!=0?0:k*.5,oz=tx!=0?k*.5:0;bool on=k<0||!dead2;Box(i,b.pos.x+ox+tx*.2,top+5.95,b.pos.z+oz+tz*.2,.5,.4,.5,P(NativeBlockLooks.SteelGreen,0x1E2220,RoleHigh),AxisRot(tz!=0?1:0,0,tx!=0?1:0,tx!=0?-tx*25:tz*25));
                    Box(i,b.pos.x+ox+tx*.42,top+5.9,b.pos.z+oz+tz*.42,tx!=0?.04:.38,.3,tx!=0?.38:.04,on?Glow(cold?0xE8F4FF:SodiumHex,2.2f,RoleHigh,cold?DriveSteady:DriveFlicker,(float)Hash(b.id,k,2)*7):P(NativeBlockLooks.Plain,0x2A2622,RoleHigh));}
                Light(b.pos.x+tx*2.4,top+4.6,b.pos.z+tz*2.4,cold?0xE8F4FF:SodiumHex,1.0,16,2.0,GlowFlood);End();
            }
            else if(Starts(s,"wild-tank-"))
            {
                foreach(var (ax,sg) in new[]{(0,1),(0,-1),(2,1),(2,-1)}){var f=MakeFace(i,ax,sg);if(f==null)continue;for(int k=1;k<4;k++)FBox(f,(f.U0+f.U1)/2,k*(f.Top-f.Base)/4,.04,f.U1-f.U0+.06,.14,.08,P(NativeBlockLooks.SteelGreen,0x3A3E3A,RoleClutter));
                    FBox(f,(f.U0+f.U1)/2,(f.Top-f.Base)*.55,.02,1.4,1.8,.012,With(P(NativeBlockLooks.Plain,RustHex,RoleClutter),detail:true));if(ax==0&&sg>0){Text(f,"COAL",(f.U0+f.U1)/2,(f.Top-f.Base)*.7,.06,.12,.01,P(NativeBlockLooks.Plain,0xD8D0C0,RoleSign));for(int r=0;r<10;r++)FBox(f,f.U1-.3,.4+r*.5,.1,.4,.04,.04,With(P(NativeBlockLooks.SteelGreen,0x2A2622,RoleClutter),detail:true));}}
            }
            else if(Starts(s,"wild-shelter-"))
            {
                // A bus shelter: the block is its back panel (an advert lit cold on some); a roof 2.6 m up overhanging the
                // street side, a bench and two posts against it, the stop's sign on the roof's edge.
                double len=alongX?b.size.x:b.size.z;var steel=P(NativeBlockLooks.SteelGreen,0x2A3A34,RoleClutter);int front=1;
                var f=MakeFace(i,alongX?2:0,front);
                if(f!=null)
                {
                    double mid=(f.U0+f.U1)/2;FBox(f,mid,1.5,.02,len-.4,1.6,.04,Hash(b.id,0,1)<.5?Glow(0xCFE8F0,.5f,RoleClutter,DriveFlicker,(float)Hash(b.id,0,2)*9):P(NativeBlockLooks.Plain,0x2A3A44,RoleClutter));
                    FBox(f,mid,.45,.12,len-.3,.06,.24,steel);FBox(f,mid,.22,.12,len-.5,.44,.04,steel);
                    foreach(int k in new[]{-1,1})FBox(f,mid+k*(len/2-.08),1.3,.2,.08,2.6,.08,With(steel,shadow:true));
                    FBox(f,mid,2.65,.1,len+.1,.1,.28,P(NativeBlockLooks.SteelGreen,0x2A3A34,RoleHigh));
                    FBox(f,mid+len/2-.6,3.3,.12,.6,.3,.04,P(NativeBlockLooks.Plain,0xD8C890,RoleSign));Text(f,"BUS",mid+len/2-.6,3.2,.15,.035,.01,P(NativeBlockLooks.Plain,0x1A1A1A,RoleSign));
                }
            }
        }

        // ---------------- the wilds' ground and the markers ----------------
        private static readonly double[] MarkerShifts={0,-3,3,-6,6};
        /// <summary>Flat dressing in the wilds: dark dirt and ash over the asphalt, debris, the Ironworks' rails, the bus bays,
        /// QUARANTINE ZONE on the Meridian Approach; then the distance markers along every route to an instance door.</summary>
        private void WildsGround()
        {
            if(hub==null)return;
            foreach(var w in hub.Wilds)
            {
                var v=w.Volume;double x0=v.x-v.sx/2,z0=v.z-v.sz/2;int placed=0;
                for(int k=0;k<220&&placed<55;k++)
                {
                    double x=x0+v.sx*Hash(w.Id,k,1),z=z0+v.sz*Hash(w.Id,k,2);int g=GroundAt(x,z);if(g<0||InBlock(x,z,.15))continue;double s=Hash(w.Id,k,3),size=1.2+3*Hash(w.Id,k,4);
                    int hex=s<.55?Pick(new[]{0x2A2420,0x241E1C,0x302A24},s*5):s<.8?0x1C1A1A:0x3A3228;
                    Box(g,x,.004+.001*(k%4),z,size,.008,size*(.5+.5*Hash(w.Id,k,5)),With(P(NativeBlockLooks.Plain,hex,RoleGround),detail:true),AxisRot(0,1,0,s*360));placed++;
                }
                if(w.Side=="west")for(int track=0;track<2;track++)
                {
                    double tz=track==0?6.5:-6.5;for(double x=v.x-v.sx/2+.5;x<v.x+v.sx/2-.5;x+=.7)
                    {
                        int g=GroundAt(x,tz);if(g<0||InBlock(x,tz,.3))continue;Box(g,x,.012,tz,.22,.024,2.4,With(P(NativeBlockLooks.Timber,0x2A221C,RoleGround),detail:true));
                        // The rails run on in lengths of four sleepers.
                        if(((int)Math.Round((x-v.x)/.7)&3)==0)foreach(double r in new[]{-.72,.72})Box(g,x+1.05,.03,tz+r,2.8,.05,.07,P(NativeBlockLooks.SteelGreen,0x4A4440,RoleGround));
                    }
                }
                if(w.Side=="east")GroundText("QUARANTINE ZONE",v.x-13,0,1,0,.26,0xC8B040);
                if(w.Side=="south")for(int k=0;k<3;k++){double x=-24+k*8;int g=GroundAt(x,-58);if(g>=0&&!InBlock(x,-58,.2))GroundText("BUS",x,-58,0,1,.22,0xD8B040);}
            }
            // Distance markers along each route to an instance door: painted chevrons and the walked metres left, every 15 m.
            foreach(var r in hub.Routes)
            {
                if(r.Reserved)continue;var g=hub.Gate(r.Gate);if(g==null)continue;double dx=r.Approach.x-g.X,dz=r.Approach.z-g.Z,len=Math.Sqrt(dx*dx+dz*dz);if(len<1)continue;dx/=len;dz/=len;
                string name=r.To=="exp-ward-zero"?"WARD ZERO":r.To.ToUpperInvariant();
                for(double at0=15;at0<len-6;at0+=15)
                {
                    // A marker that would land on cover (the rubble heap on the axis) slides along the route to clear ground.
                    double at=at0;foreach(double sh in MarkerShifts){double sa=at0+sh,cx=g.X+dx*sa,cz=g.Z+dz*sa;if(sa<len-6&&!InBlock(cx,cz,.6)&&!InBlock(cx-dx*1.6,cz-dz*1.6,.6)){at=sa;break;}}
                    double x=g.X+dx*at,z=g.Z+dz*at,left=r.Path*(1-at/len);
                    for(double side=-1.6;side<=1.6;side+=1.6){double px=x+(-dz)*side,pz=z+dx*side;if(InBlock(px,pz,.3))continue;int gg=GroundAt(px,pz);if(gg<0)continue;
                        Chevron(gg,px,pz,dx,dz,Wild(px,pz)?0x7AD8E8:0xD8D0C0);}
                    double tx=x-dx*1.6,tz=z-dz*1.6;if(((int)Math.Round(at0/15)&1)==0&&!InBlock(tx,tz,.6))GroundText(name+" "+Metres(left)+"M",tx,tz,dx,dz,.12,Wild(tx,tz)?0x7AD8E8:0xD8D0C0);
                }
            }
            // Wall signs where each route leaves the city (two per route, on the faces beside its last stretch), and SANCTUARY
            // arrows back the other way in the wilds.
            var signs=new Dictionary<string,int>();
            foreach(var f in faces)
            {
                if(f.Front==0)continue;At(f,(f.U0+f.U1)/2,0,0,out double cx,out double cz);string id=blocks[f.Block].id;
                foreach(var r in hub.Routes)
                {
                    var g=hub.Gate(r.Gate);if(g==null)continue;signs.TryGetValue(r.To,out int made);if(made>=2)continue;double dx=r.Approach.x-g.X,dz=r.Approach.z-g.Z,len=Math.Sqrt(dx*dx+dz*dz);dx/=len;dz/=len;
                    // A face along the route (its normal across it), beside the route's last stretch through the city's edge or the wilds.
                    double along=(cx-g.X)*dx+(cz-g.Z)*dz,across=Math.Abs(-(cx-g.X)*dz+(cz-g.Z)*dx);if(Math.Abs(Nx(f)*dx+Nz(f)*dz)>.2||along<24||along>len-10||across>21)continue;
                    double u=U(f,g.X+dx*Math.Min(len-12,Math.Max(26,along)),g.Z+dz*Math.Min(len-12,Math.Max(26,along)));if(u<f.U0+2.5||u>f.U1-2.5)continue;
                    double ahead=Tx(f)*dx+Tz(f)*dz;string to=r.Reserved?(r.To=="exp-foundry"?"IRONWORKS":"STATION")+" SEALED":(r.To=="exp-ward-zero"?"WARD ZERO":r.To.ToUpperInvariant())+" "+Metres(r.Path*(1-along/len))+" M";
                    string word=ahead>0?to+" >":"< "+to;double cell=.075;if(!Open(f,u-TextWidth(word,cell)/2-.3,u+TextWidth(word,cell)/2+.3,2.0,2.9,.05))continue;
                    Begin();FBox(f,u,2.45,.04,TextWidth(word,cell)+.4,.6,.04,P(NativeBlockLooks.Plain,r.Reserved?0x2A2622:0x14303A,RoleSign));Text(f,word,u,2.45-2.5*cell,.07,cell,.01,P(NativeBlockLooks.Plain,r.Reserved?0xB8B0A0:0xD8F4FF,RoleSign));if(End())signs[r.To]=made+1;
                    double back=-ahead;string home=back>0?"SANCTUARY >":"< SANCTUARY";if(Wild(cx,cz)&&Open(f,u-TextWidth(home,.08)/2-.1+(back>0?3:-3),u+TextWidth(home,.08)/2+.1+(back>0?3:-3),1.5,2.1,.05)){Begin();Text(f,home,u+(back>0?3:-3),1.55,.075,.08,.01,P(NativeBlockLooks.Plain,0x5AD87A,RoleSign),(Hash(id,f.Index,5)-.5)*4);End();}
                    break;
                }
            }
        }
        /// <summary>Metres to the nearest 5 (invariant digits).</summary>
        private static string Metres(double m)=>((int)(Math.Round(m/5)*5)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        // A painted chevron on the ground pointing along (dx, dz).
        private void Chevron(int g,double x,double z,double dx,double dz,int hex)
        {
            double yaw=Math.Atan2(dx,dz)*180/Math.PI;
            foreach(int k in new[]{-1,1}){double ox=(-dz)*k*.32-dx*.25,oz=dx*k*.32-dz*.25;Box(g,x+ox,.005,z+oz,.18,.01,.9,With(P(NativeBlockLooks.Plain,hex,RoleGround),detail:false),AxisRot(0,1,0,yaw+k*35));}
        }
        /// <summary>Block letters painted flat on the street at (x, z), read by someone walking along (dx, dz).</summary>
        private void GroundText(string text,double x,double z,double dx,double dz,double cell,int hex)
        {
            double l=Math.Sqrt(dx*dx+dz*dz);if(l<1e-6)return;dx/=l;dz/=l;double rx=dz,rz=-dx;double width=TextWidth(text,cell),u0=-width/2;double yaw=Math.Atan2(dx,dz)*180/Math.PI;
            for(int i=0;i<text.Length;i++)foreach(var gl in GlyphBoxes(text[i]))
            {
                double cu=u0+i*4*cell+(gl[0]+gl[2]*.5)*cell,cv=(5-gl[1]-gl[3]*.5)*cell-2.5*cell;double px=x+rx*cu+dx*cv,pz=z+rz*cu+dz*cv;int g=GroundAt(px,pz);if(g<0)continue;
                Box(g,px,.006,pz,gl[2]*cell,.008,gl[3]*cell,With(P(NativeBlockLooks.Plain,hex,RoleGround),detail:true),AxisRot(0,1,0,yaw));
            }
        }
    }
}
