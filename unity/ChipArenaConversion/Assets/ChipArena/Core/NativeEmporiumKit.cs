using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Top Dog Emporium's static kit (docs/arenas/EMPORIUM.md "Look"): the detail NativeBlockLit instances draw on and over the map's
    /// dressed blocks, by block id stem, so the stage reads as a hot dog palace without any licensed bank. Two kinds of part:
    /// <list type="bullet">
    /// <item>Attached (<see cref="Parent"/>): the floor's paint (the diner's checkerboard, the kitchen line's quarry tile, the roller
    /// beds of the wiener lanes and their hazard stripes, the presses' zones, the griddles' frames, the geysers' splats and arrows),
    /// the walls' tile, wainscot, frieze, posters, windows and neon lettering, the ovens' portals, windows and rooftop signs, the
    /// kitchens' shelving and goods, the booths' tufted vinyl and set tables, the counters' stools and roller grills, the soda
    /// fountain, jukebox, pumps, freezers, barrels, crates, the bun bridges (a hot dog to walk on), the balconies' fascias and the
    /// condiment towers' labels and caps. Every attached part stays inside its block grown by <see cref="Proud"/> below 3 m (what
    /// looks like cover is cover), and decals sit within 2 cm of their face.</item>
    /// <item>Overhead (parent -1): the glazed roof's arched trusses and purlins (they cast the striped golden-hour shadows), the
    /// pendant lamps, the bun conveyors' rails and the hangers, each at least <see cref="Clearance"/> m over every standable top
    /// under it, and nothing of the roof below NativeEmporiumLooks.RoofLow.</item>
    /// </list>
    /// Deterministic (the same map and look give the same parts); allocates only while building (Runtime packs it once per map).
    /// </summary>
    public sealed class NativeEmporiumKit
    {
        public const float Proud=.25f;public const double Clearance=2.0,Decal=.012;
        public readonly NativeFactoryKit.Writer Parts;public int[] Parent=Array.Empty<int>();public bool[] Overhead=Array.Empty<bool>();
        private readonly List<int> parents=new List<int>();private readonly List<bool> overhead=new List<bool>();
        private int current=-1;private bool over;
        /// <summary>For the stage art's layout export (docs/arenas/STAGE-ART.md; nothing draws by them): each part's family (the builder
        /// that made it, with a ":tag" for its neon lettering, icons and posters), the block it dresses (null: overhead) and its kind.</summary>
        public readonly List<string> Family=new List<string>(),Owner=new List<string>();public readonly List<int> Kind=new List<int>();private string family="",owner,tag;
        public int Count=>Parts.Count;
        private NativeEmporiumKit(NativeFactoryKit.Looks looks){Parts=new NativeFactoryKit.Writer(2048,true,looks);}
        private void Track(int before,int kind){for(int i=before;i<Parts.Count;i++){parents.Add(current);overhead.Add(over);Family.Add(tag==null?family:family+":"+tag);Owner.Add(owner);Kind.Add(kind);}}
        private void Add(double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=true,int color=-1,double window=1){int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow,color,window);Track(n,kind);}
        private void Box(double x0,double x1,double y0,double y1,double z0,double z1,int kind,bool shadow=true,int color=-1,double window=1){int n=Parts.Count;Parts.Box(Math.Min(x0,x1),Math.Max(x0,x1),Math.Min(y0,y1),Math.Max(y0,y1),Math.Min(z0,z1),Math.Max(z0,z1),kind,shadow,color,window);Track(n,kind);}
        private void Bar(double x0,double y0,double z0,double x1,double y1,double z1,double t,int kind,bool shadow=true,int color=-1,double window=1){int n=Parts.Count;Parts.Bar(x0,y0,z0,x1,y1,z1,t,t,kind,shadow,color);if(Parts.Count>n)Parts.Items[n].Window=(float)window;Track(n,kind);}
        private static double H(int a,int b=0,int c=0)=>NativeEmporiumLooks.Hash(a,b,c);
        private static bool Starts(string id,string p)=>id!=null&&id.StartsWith(p,StringComparison.Ordinal);
        private static bool Has(string id,string p)=>id!=null&&id.IndexOf(p,StringComparison.Ordinal)>=0;
        private const int K_Dark=NativeEmporiumLooks.Dark,K_Chrome=NativeEmporiumLooks.Chrome,K_Red=NativeEmporiumLooks.Red,K_Yellow=NativeEmporiumLooks.Yellow,K_Cream=NativeEmporiumLooks.Cream,
            K_Teal=NativeEmporiumLooks.Teal,K_Pink=NativeEmporiumLooks.Pink,K_Wood=NativeEmporiumLooks.Wood,K_Sausage=NativeEmporiumLooks.Sausage,K_Bun=NativeEmporiumLooks.Bun,K_BunTop=NativeEmporiumLooks.BunTop,
            K_Sesame=NativeEmporiumLooks.Sesame,K_Lamp=NativeEmporiumLooks.Lamp,K_Tube=NativeEmporiumLooks.Tube,K_Glass=NativeEmporiumLooks.Glass,K_GlassDark=NativeEmporiumLooks.GlassDark,
            K_Checker=NativeEmporiumLooks.Checker,K_Quarry=NativeEmporiumLooks.Quarry,K_Roller=NativeEmporiumLooks.Roller,K_Rubber=NativeEmporiumLooks.Rubber,K_Pickle=NativeEmporiumLooks.Pickle,
            K_Mustard=NativeEmporiumLooks.Mustard,K_Ketchup=NativeEmporiumLooks.Ketchup,K_Screen=NativeEmporiumLooks.Screen,K_White=NativeEmporiumLooks.White,K_Iron=NativeEmporiumLooks.Iron,
            K_Stripe=NativeEmporiumLooks.Stripe,K_Mint=NativeEmporiumLooks.Mint,K_Vinyl=NativeEmporiumLooks.Vinyl,K_Soda=NativeEmporiumLooks.Soda,K_Relish=NativeEmporiumLooks.Relish,K_Onion=NativeEmporiumLooks.Onion;

        private struct Bx {public double X0,X1,Y0,Y1,Z0,Z1;public string Id;public int Index;public double Sx=>X1-X0;public double Sy=>Y1-Y0;public double Sz=>Z1-Z0;public double Cx=>(X0+X1)/2;public double Cz=>(Z0+Z1)/2;}
        private List<Bx> all;

        /// <summary>The map's kit for its look (null for any map but the Emporium).</summary>
        public static NativeEmporiumKit Build(ContentMap map,MapLook look)
        {
            if(!NativeEmporiumLooks.Active(map?.id))return null;var k=new NativeEmporiumKit(NativeEmporiumLooks.Resolve(look?.kitLooks));
            var all=new List<Bx>();
            for(int i=0;i<(map.blocks?.Length??0);i++)
            {
                var b=map.blocks[i];if(b?.id==null)continue;var r=b.rotation;if(r!=null&&(Math.Abs(r.x)>1e-4||Math.Abs(r.y)>1e-4||Math.Abs(r.z)>1e-4))continue;
                all.Add(new Bx{X0=b.pos.x-b.size.x/2,X1=b.pos.x+b.size.x/2,Y0=b.pos.y-b.size.y/2,Y1=b.pos.y+b.size.y/2,Z0=b.pos.z-b.size.z/2,Z1=b.pos.z+b.size.z/2,Id=b.id,Index=i});
            }
            k.all=all;
            foreach(var b in all)
            {
                k.On(b);string id=b.Id;
                if(id=="emporium-floor"){k.family="floor";k.FloorPaint(b,map);}
                else if(Starts(id,"emporium-wall-")){k.family="wall";k.Wall(b);}
                else if(Starts(id,"emporium-oven-")){k.family="oven";k.Oven(b);}
                else if(Has(id,"-kwall-")){k.family="kwall";k.KitchenWall(b);}
                else if(Has(id,"-fryer-")){k.family="fryer";k.Fryer(b);}
                else if(Has(id,"-shelf-")||Has(id,"-baffle-")){k.family="shelf";k.Shelf(b);}
                else if(Has(id,"-icebox-")){k.family="icebox";k.Icebox(b);}
                else if(id=="emporium-plinth"){k.family="plinth";k.Plinth(b);}
                else if(Has(id,"-press-")&&Has(id,"-post-")){k.family="press-post";k.PressPost(b);}
                else if(Has(id,"-press-")){k.family="press-head";k.PressHead(b);}
                else if(Has(id,"-tower-")){k.family="tower";k.Tower(b);}
                else if(Has(id,"-prep-")){k.family="prep";k.Prep(b);}
                else if(Has(id,"-crate-")){k.family="crate";k.Crate(b);}
                else if(Starts(id,"emporium-bridge-")&&Has(id,"-flight-")){k.family="bridge-step";k.Step(b,K_BunTop);}
                else if(Starts(id,"emporium-bridge-")){k.family="bridge";k.BridgeDeck(b);}
                else if(Starts(id,"emporium-balcony-")&&Has(id,"-post-")){k.family="post";k.Post(b);}
                else if(Starts(id,"emporium-rail-")){k.family="rail";k.Rail(b);}
                else if(Starts(id,"emporium-balcony-")){k.family="balcony";k.Balcony(b);}
                else if(Starts(id,"emporium-stair-")){k.family="stair";k.Step(b,K_Teal);}
                else if(Has(id,"-booth-")){k.family="booth";k.Booth(b);}
                else if(Has(id,"-counter-")){k.family="counter";k.Counter(b);}
                else if(Has(id,"-soda-")){k.family="soda";k.Soda(b);}
                else if(Has(id,"-jukebox-")){k.family="jukebox";k.JukeboxBody(b);}
                else if(Has(id,"-pump-")){k.family="pump";k.Pump(b);}
                else if(Has(id,"-kiosk-")){k.family="kiosk";k.Kiosk(b);}
                else if(Has(id,"-freezer-")||Has(id,"-fridge-")){k.family="fridge";k.Fridge(b);}
                else if(Has(id,"-barrel-")){k.family="barrel";k.Barrel(b);}
            }
            k.Above();k.family="roof";k.Roof();k.family="pendant";k.Pendants();k.family="conveyor";k.ConveyorRails();k.family="door-sign";k.DoorSigns();
            k.Parent=k.parents.ToArray();k.Overhead=k.overhead.ToArray();return k;
        }
        private void On(Bx b){current=b.Index;over=false;owner=b.Id;}
        private void Above(){current=-1;over=true;owner=null;}

        // ---------------- face helpers (u along the face, v up; a hair proud) ----------------
        /// <summary>A plate on one face: side 0 +x, 1 -x, 2 +z, 3 -z, 4 the top, 5 the underside; (u0..u1) along it (z for ±x faces, x for
        /// ±z faces and the top and underside), (v0..v1) up it (world y; z on the top and underside), `o` m proud, `t` thick.</summary>
        private void Face(Bx b,int side,double u0,double u1,double v0,double v1,int kind,int color=-1,double t=Decal,double o=.004,bool shadow=false,double window=1)
        {
            switch(side)
            {
                case 0:Box(b.X1+o-t,b.X1+o,v0,v1,u0,u1,kind,shadow,color,window);break;case 1:Box(b.X0-o,b.X0-o+t,v0,v1,u0,u1,kind,shadow,color,window);break;
                case 2:Box(u0,u1,v0,v1,b.Z1+o-t,b.Z1+o,kind,shadow,color,window);break;case 3:Box(u0,u1,v0,v1,b.Z0-o,b.Z0-o+t,kind,shadow,color,window);break;
                case 4:Box(u0,u1,b.Y1+o-t,b.Y1+o,v0,v1,kind,shadow,color,window);break;default:Box(u0,u1,b.Y0-o,b.Y0-o+t,v0,v1,kind,shadow,color,window);break;
            }
        }
        private static void Span(Bx b,int side,out double u0,out double u1){if(side<=1){u0=b.Z0;u1=b.Z1;}else{u0=b.X0;u1=b.X1;}}
        /// <summary>The point on a face at (u, v), `o` proud: world x, y, z, and the face's outward normal (nx, nz) and its u axis.</summary>
        private static void At(Bx b,int side,double u,double v,double o,out double x,out double y,out double z,out double nx,out double nz)
        {y=v;switch(side){case 0:x=b.X1+o;z=u;nx=1;nz=0;break;case 1:x=b.X0-o;z=u;nx=-1;nz=0;break;case 2:x=u;z=b.Z1+o;nx=0;nz=1;break;default:x=u;z=b.Z0-o;nx=0;nz=-1;break;}}
        /// <summary>The block's side whose outward normal points most toward (tx, tz).</summary>
        private static int Toward(Bx b,double tx,double tz){double dx=tx-b.Cx,dz=tz-b.Cz;return Math.Abs(dx)>Math.Abs(dz)?(dx>0?0:1):(dz>0?2:3);}

        // ---------------- neon lettering: a stroke font on a 4 x 6 grid ----------------
        private static readonly Dictionary<char,string> Glyphs=new Dictionary<char,string>
        {
            ['A']="0,0 0,4 2,6 4,4 4,0|0,3 4,3",['B']="0,0 0,6 3,6 4,5 4,4 3,3 0,3|3,3 4,2 4,1 3,0 0,0",['C']="4,6 0,6 0,0 4,0",['D']="0,0 0,6 2,6 4,4 4,2 2,0 0,0",
            ['E']="4,6 0,6 0,0 4,0|0,3 3,3",['F']="4,6 0,6 0,0|0,3 3,3",['G']="4,5 4,6 0,6 0,0 4,0 4,3 2,3",['H']="0,0 0,6|4,0 4,6|0,3 4,3",['I']="1,6 3,6|2,6 2,0|1,0 3,0",
            ['K']="0,0 0,6|4,6 0,3 4,0",['L']="0,6 0,0 4,0",['M']="0,0 0,6 2,3 4,6 4,0",['N']="0,0 0,6 4,0 4,6",['O']="0,0 0,6 4,6 4,0 0,0",['P']="0,0 0,6 4,6 4,3 0,3",
            ['R']="0,0 0,6 4,6 4,3 0,3 4,0",['S']="4,6 0,6 0,3 4,3 4,0 0,0",['T']="0,6 4,6|2,6 2,0",['U']="0,6 0,0 4,0 4,6",['W']="0,6 1,0 2,3 3,0 4,6",['X']="0,0 4,6|0,6 4,0",
            ['Y']="0,6 2,3 4,6|2,3 2,0",['Z']="0,6 4,6 0,0 4,0",['!']="2,6 2,2|2,1 2,0",['*']="0,3 4,3|2,1 2,5|0,1 4,5|0,5 4,1",['2']="0,6 4,6 4,3 0,3 0,0 4,0",['4']="0,6 0,3 4,3|4,6 4,0",['7']="0,6 4,6 2,0",
        };
        /// <summary>The width of `text` at letter height `h` (m).</summary>
        public static double TextWidth(string text,double h)=>text.Length*h*(4.0/6+.28)-h*.28;
        /// <summary>Neon text on a face: centred at u (along the face's u axis), baseline v, letter height h, tubes `w` thick (HDR gain).</summary>
        private void Neon(Bx b,int side,double uc,double v,double h,string text,int color,double gain=2.4,double w=0)
        {
            if(side<=3){Span(b,side,out double f0,out double f1);double room=f1-f0-.16;if(TextWidth(text,h)>room){if(room<.3)return;h*=room/TextWidth(text,h);}}
            string was=tag;tag="neon";try{NeonStrokes(b,side,uc,v,h,text,color,gain,w);}finally{tag=was;}
        }
        private void NeonStrokes(Bx b,int side,double uc,double v,double h,string text,int color,double gain,double w)
        {
            if(w<=0)w=Math.Max(.035,h*.07);w=Math.Min(w,h*.12);double width=TextWidth(text,h),s=h/6,adv=h*(4.0/6+.28);
            // u runs left to right as seen from the front: the face's u axis is +z/+x on sides 1 and 2... flip for 0 and 3
            int dir=side==0||side==3?1:-1;double u=uc-dir*width/2;
            foreach(char ch in text)
            {
                if(Glyphs.TryGetValue(char.ToUpperInvariant(ch),out string g))
                    foreach(var stroke in g.Split('|'))
                    {
                        var pts=stroke.Split(' ');double px=double.NaN,py=0;
                        foreach(var p in pts){var xy=p.Split(',');double gx=double.Parse(xy[0],System.Globalization.CultureInfo.InvariantCulture),gy=double.Parse(xy[1],System.Globalization.CultureInfo.InvariantCulture);
                            double qx=u+dir*gx*s,qy=v+gy*s;if(!double.IsNaN(px)){At(b,side,px,py,w/2+.01,out double x0,out double y0,out double z0,out _,out _);At(b,side,qx,qy,w/2+.01,out double x1,out double y1,out double z1,out _,out _);Bar(x0,y0,z0,x1,y1,z1,w,K_Tube,false,color,gain);}
                            px=qx;py=qy;}
                    }
                u+=dir*adv;
            }
        }
        /// <summary>A neon hot dog icon on a face, centred (uc, vc), `l` long: the bun's two arcs, the frankfurter, the mustard squiggle.</summary>
        private void HotDogIcon(Bx b,int side,double uc,double vc,double l,double gain=2.4)
        {
            double r=l*.22,w=Math.Max(.04,l*.035);string was=tag;tag="icon";
            void Seg(double u0,double v0,double u1,double v1,int col){At(b,side,u0,v0,w/2+.01,out double x0,out double y0,out double z0,out _,out _);At(b,side,u1,v1,w/2+.01,out double x1,out double y1,out double z1,out _,out _);Bar(x0,y0,z0,x1,y1,z1,w,K_Tube,false,col,gain);}
            for(int k=0;k<8;k++){double a0=Math.PI+k*Math.PI/8,a1=a0+Math.PI/8;Seg(uc+Math.Cos(a0)*l*.5,vc-r*.2+Math.Sin(a0)*r,uc+Math.Cos(a1)*l*.5,vc-r*.2+Math.Sin(a1)*r,NativeEmporiumLooks.NeonOrange);}
            Seg(uc-l*.56,vc+r*.15,uc+l*.56,vc+r*.15,NativeEmporiumLooks.NeonPink);Seg(uc-l*.56,vc+r*.55,uc+l*.56,vc+r*.55,NativeEmporiumLooks.NeonPink);Seg(uc-l*.56,vc+r*.15,uc-l*.6,vc+r*.35,NativeEmporiumLooks.NeonPink);Seg(uc-l*.6,vc+r*.35,uc-l*.56,vc+r*.55,NativeEmporiumLooks.NeonPink);
            Seg(uc+l*.56,vc+r*.15,uc+l*.6,vc+r*.35,NativeEmporiumLooks.NeonPink);Seg(uc+l*.6,vc+r*.35,uc+l*.56,vc+r*.55,NativeEmporiumLooks.NeonPink);
            for(int k=0;k<7;k++){double u0=uc-l*.42+k*l*.12,u1=u0+l*.12;Seg(u0,vc+r*((k&1)==0?.42:.28),u1,vc+r*((k&1)==0?.28:.42),NativeEmporiumLooks.NeonLemon);}
            tag=was;
        }

        // ---------------- the floor ----------------
        private bool Covered(double x,double z){foreach(var b in all)if(b.Y0<.05&&b.Y1>.5&&x>b.X0&&x<b.X1&&z>b.Z0&&z<b.Z1&&b.Id!="emporium-floor")return true;return false;}
        private void FloorPaint(Bx f,ContentMap map)
        {
            double minX=map.bounds?.minX??-28,maxX=map.bounds?.maxX??28,minZ=map.bounds?.minZ??-23,maxZ=map.bounds?.maxZ??23;
            double laneOuter=0,laneInner=double.MaxValue;foreach(var h in map.hazards??Array.Empty<MapHazard>())if(h?.kind=="maglev"){laneOuter=Math.Max(laneOuter,Math.Abs(h.center.z)+h.size.z/2);laneInner=Math.Min(laneInner,Math.Abs(h.center.z)-h.size.z/2);}
            if(laneInner==double.MaxValue)laneInner=8;if(laneOuter==0)laneOuter=12;
            // the kitchen line: red quarry tile between the lanes; the diner: a black and white checkerboard of 1.2 m squares
            Face(f,4,minX,maxX,-laneInner+.05,laneInner-.05,K_Quarry);
            const double sq=1.2;
            foreach(double side in new[]{-1.0,1.0})
            {
                double z0=laneOuter+.4,z1=side>0?maxZ:-minZ;
                for(int j=0;z0+j*sq<z1-.01;j++)for(int i=0;minX+i*sq<maxX-.01;i++)
                {
                    if(((i+j)&1)==0)continue;double xa=minX+i*sq,xb=Math.Min(maxX,xa+sq),za=z0+j*sq,zb=Math.Min(z1,za+sq);if(Covered((xa+xb)/2,side*(za+zb)/2))continue;
                    Face(f,4,xa,xb,side>0?za:-zb,side>0?zb:-za,K_Checker);
                }
                Face(f,4,minX,maxX,side>0?laneOuter+.05:-laneOuter-.4,side>0?laneOuter+.4:-laneOuter-.05,K_Teal);
            }
            foreach(var h in map.hazards??Array.Empty<MapHazard>())
            {
                if(h==null)continue;
                if(h.kind=="maglev")
                {
                    double cz=h.center.z,hw=h.size.z/2,x0=h.center.x-h.size.x/2,x1=h.center.x+h.size.x/2;
                    Face(f,4,x0,x1,cz-hw,cz+hw,K_Dark);
                    for(double x=x0+.25;x<x1;x+=.5)Add(x,0,cz,.06,.04,hw-.08,FactoryQ.Identity,K_Roller,false);
                    foreach(double s in new[]{-1.0,1.0}){Face(f,4,x0,x1,cz+s*(hw+.32)-.15,cz+s*(hw+.32)+.15,K_Stripe,-1,Decal,.006);Box(x0,x1,0,.05,cz+s*hw-.05,cz+s*hw+.05,K_Chrome,false);}
                }
                else if(h.kind==NativeFactoryHazards.Press)
                {
                    double x=h.a.x,z=h.a.z,hx=h.size.x/2+.12,hz=h.size.z/2+.12;
                    Face(f,4,x-hx,x+hx,z-hz,z-hz+.16,K_Stripe,-1,Decal,.006);Face(f,4,x-hx,x+hx,z+hz-.16,z+hz,K_Stripe,-1,Decal,.006);Face(f,4,x-hx,x-hx+.16,z-hz+.16,z+hz-.16,K_Stripe,-1,Decal,.006);Face(f,4,x+hx-.16,x+hx,z-hz+.16,z+hz-.16,K_Stripe,-1,Decal,.006);
                    Face(f,4,x-hx+.16,x+hx-.16,z-hz+.16,z+hz-.16,K_Cream,0xF2D8B0);
                }
                else if(h.kind==NativeWardZeroHazards.Burner)
                {
                    double x0=h.center.x-h.size.x/2,x1=h.center.x+h.size.x/2,z0=h.center.z-h.size.z/2,z1=h.center.z+h.size.z/2;
                    Box(x0-.12,x1+.12,0,.04,z0-.12,z0,K_Chrome,false);Box(x0-.12,x1+.12,0,.04,z1,z1+.12,K_Chrome,false);Box(x0-.12,x0,0,.04,z0,z1,K_Chrome,false);Box(x1,x1+.12,0,.04,z0,z1,K_Chrome,false);
                    Face(f,4,x0,x1,z0,z1,K_Dark);
                }
                else if(h.kind==NativeFactoryHazards.Vent)
                {
                    bool k=NativeEmporiumLooks.KetchupSide(h.a.x);int sauce=k?K_Ketchup:K_Mustard;double x=h.a.x,z=h.a.z;
                    for(int i=0;i<9;i++){double a=i*2*Math.PI/9+H(i,(int)(x*10)),r=1.05+H(i,(int)(z*10))*.45,s=.22+.16*H(i,3,(int)x);Face(f,4,x+Math.Cos(a)*r-s,x+Math.Cos(a)*r+s,z+Math.Sin(a)*r-s,z+Math.Sin(a)*r+s,sauce,-1,Decal,.006);}
                    Face(f,4,x-1.0,x+1.0,z-1.0,z+1.0,sauce,NativeEmporiumLooks.Scale(k?NativeEmporiumLooks.KetchupHex:NativeEmporiumLooks.MustardHex,.8));
                    double dz=Math.Sign(z)*1.0;for(int c=0;c<3;c++){double zc=z-dz*(1.6+c*.55);Bar(x-.45,.006,zc,x,.006,zc+dz*.4,.12,K_White,false);Bar(x,.006,zc+dz*.4,x+.45,.006,zc,.12,K_White,false);}
                }
            }
        }

        // ---------------- the hall ----------------
        private void Wall(Bx b)
        {
            int side=Math.Abs(b.Cz)>Math.Abs(b.Cx)?(b.Cz>0?3:2):(b.Cx>0?1:0);Span(b,side,out double u0,out double u1);bool ns=side>=2;
            Face(b,side,u0,u1,0,.16,K_Dark);Face(b,side,u0,u1,.16,1.25,K_Teal);Face(b,side,u0,u1,1.25,1.34,K_Chrome,-1,.04,.02);
            Face(b,side,u0,u1,6.8,6.88,K_Chrome,-1,.04,.02);Face(b,side,u0,u1,6.88,8.0,K_Pink);
            for(double u=u0+1.2;u<u1-1;u+=2.4)Face(b,side,u-.05,u+.05,6.88,8.0,K_Cream,-1,Decal,.008);
            if(ns)
            {
                // over the balcony: the name in pink neon with a cyan rule; under it, a poster over every booth
                double c=(u0+u1)/2;Neon(b,side,c,4.75,1.25,"TOP DOG EMPORIUM",NativeEmporiumLooks.NeonPink,2.6);
                Face(b,side,c-TextWidth("TOP DOG EMPORIUM",1.25)/2,c+TextWidth("TOP DOG EMPORIUM",1.25)/2,4.45,4.53,K_Tube,NativeEmporiumLooks.NeonCyan,.05,.03,false,2.4);
                foreach(double u in new[]{-12.4,-7.6,-2.8,2.0,6.8,11.2}){double uu=b.Cz>0?u:-u;Poster(b,side,uu,1.55,2.85,(int)(uu*10));}
                foreach(double u in new[]{-24.0,24.0}){Face(b,side,u-1.6,u+1.6,3.2,6.2,K_Dark,-1,.03,.012);Face(b,side,u-1.5,u+1.5,3.3,6.1,K_Glass,0xFFB070,.02,.02);for(int k=-1;k<=1;k++)Face(b,side,u+k*.75-.04,u+k*.75+.04,3.3,6.1,K_Dark,-1,.03,.03);Face(b,side,u-1.5,u+1.5,4.7,4.78,K_Dark,-1,.03,.03);}
            }
            else
            {
                bool k=b.Cx<0;double c=(u0+u1)/2;Neon(b,side,c,4.6,1.1,k?"KETCHUP":"MUSTARD",k?NativeEmporiumLooks.KetchupHex:NativeEmporiumLooks.NeonLemon,2.8);
                HotDogIcon(b,side,c,3.2,2.6);
                foreach(double zz in new[]{-17.0,17.0}){Face(b,side,zz-2.4,zz+2.4,2.4,6.2,K_Dark,-1,.03,.012);Face(b,side,zz-2.3,zz+2.3,2.5,6.1,K_Glass,0xFFB070,.02,.02);for(int j=-2;j<=2;j++)Face(b,side,zz+j*.92-.04,zz+j*.92+.04,2.5,6.1,K_Dark,-1,.03,.03);Face(b,side,zz-2.3,zz+2.3,4.3,4.38,K_Dark,-1,.03,.03);}
                Neon(b,side,k?-17.0:17.0,1.6,.45,"OPEN",NativeEmporiumLooks.NeonCyan,2.4);
            }
        }
        private void Poster(Bx b,int side,double uc,double v0,double v1,int seed)
        {
            string was=tag;tag="poster";double w=(v1-v0)*.72;int[] backs={NativeEmporiumLooks.NeonLemon,0xFF6FAE,0x5CD6C8,0xFFF2D6};int back=backs[(int)(H(seed,1)*backs.Length)%backs.Length];
            Face(b,side,uc-w/2-.06,uc+w/2+.06,v0-.06,v1+.06,K_Chrome,-1,.03,.012);Face(b,side,uc-w/2,uc+w/2,v0,v1,K_Cream,back,.02,.02);
            double vc=(v0+v1)/2;Face(b,side,uc-w*.38,uc+w*.38,vc-.16,vc-.02,K_Bun,-1,.02,.035);Face(b,side,uc-w*.42,uc+w*.42,vc-.06,vc+.06,K_Sausage,-1,.02,.045);Face(b,side,uc-w*.34,uc+w*.34,vc+.02,vc+.04,K_Mustard,-1,.02,.05);
            Face(b,side,uc-w*.4,uc+w*.4,v1-.28,v1-.14,K_Red,-1,.02,.035);tag=was;
        }
        private void Oven(Bx b)
        {
            int lane=b.Cx<0?0:1,back=1-lane;double cz=b.Cz;Span(b,lane,out double u0,out double u1);
            // the portal round the flap (the rig draws the flap), its arch, the XING sign over it
            Face(b,lane,cz-2.08,cz-1.86,0,2.42,K_Red,-1,.05,.025);Face(b,lane,cz+1.86,cz+2.08,0,2.42,K_Red,-1,.05,.025);Face(b,lane,cz-2.08,cz+2.08,2.22,2.42,K_Red,-1,.05,.025);
            Face(b,lane,cz-1.86,cz+1.86,0,2.22,K_Dark,-1,Decal,.006);
            Face(b,lane,cz-1.6,cz+1.6,2.62,3.62,K_Yellow,-1,.03,.012);Neon(b,lane,cz,2.8,.62,"XING",NativeEmporiumLooks.KetchupHex,2.4);
            Face(b,lane,u0+.1,u1-.1,3.8,3.92,K_Chrome,-1,.03,.02);
            // the rotisserie windows on the long faces, glowing; knobs under them
            foreach(int s in new[]{2,3})
            {
                Span(b,s,out double a0,out double a1);double c=(a0+a1)/2;Face(b,s,c-1.25,c+1.25,1.05,2.75,K_Chrome,-1,.04,.015);Face(b,s,c-1.1,c+1.1,1.2,2.6,K_Glass,0xFF7A2A,.02,.02);
                Face(b,s,c-1.1,c+1.1,1.86,1.94,K_Dark,-1,.025,.025);for(int k=0;k<4;k++){double u=c-.9+k*.6;Face(b,s,u-.07,u+.07,.62,.76,K_Dark,-1,.06,.03);}
                Face(b,s,a0+.1,a1-.1,3.1,3.5,K_Red,-1,.02,.012);
            }
            Face(b,back,b.Z0+.4,b.Z1-.4,2.6,3.2,K_Yellow,-1,.02,.012);Neon(b,back,cz,2.72,.4,"HOT",NativeEmporiumLooks.KetchupHex,2.2);
            // on the roof: a chimney and a neon hot dog board facing the lane
            Box(b.Cx-.35,b.Cx+.35,b.Y1,b.Y1+1.3,cz+1.0,cz+1.7,K_Chrome);Box(b.Cx-.45,b.Cx+.45,b.Y1+1.3,b.Y1+1.45,cz+.9,cz+1.8,K_Red);
            double bx=lane==0?b.X1-.4:b.X0+.4;var board=new Bx{X0=bx-.06,X1=bx+.06,Y0=b.Y1,Y1=b.Y1+1.6,Z0=cz-2.0,Z1=cz+.6,Id=b.Id,Index=b.Index};
            Box(board.X0,board.X1,board.Y0+.1,board.Y1,board.Z0,board.Z1,K_Dark);Box(bx-.03,bx+.03,b.Y1,b.Y1+.1,cz-1.6,cz-1.4,K_Chrome);Box(bx-.03,bx+.03,b.Y1,b.Y1+.1,cz+.1,cz+.3,K_Chrome);
            HotDogIcon(board,lane,cz-.7,b.Y1+.85,2.0,2.6);
        }
        private void KitchenWall(Bx b)
        {
            bool a=Has(b.Id,"-a-");int team=a?K_Red:K_Yellow;
            foreach(int s in new[]{0,1}){Face(b,s,b.Z0,b.Z1,0,.16,K_Dark);Face(b,s,b.Z0,b.Z1,.16,1.25,team);Face(b,s,b.Z0,b.Z1,1.25,1.32,K_Chrome,-1,.03,.02);Face(b,s,b.Z0,b.Z1,3.5,3.58,K_Chrome,-1,.03,.02);Face(b,s,b.Z0,b.Z1,3.58,4.0,team);for(double v=1.6;v<3.4;v+=.6)Face(b,s,b.Z0,b.Z1,v,v+.06,team,-1,Decal,.006);}
            int toward=a?0:1;double door=b.Cz>0?b.Z0:b.Z1;
            Face(b,b.Cz>0?3:2,b.X0,b.X1,0,b.Y1,K_Chrome,-1,.03,.012);
            if(Math.Abs(b.Cz)<5)Neon(b,toward,b.Cz>0?door+1.8:door-1.8,2.2,.42,a?"EAT":"EAT",a?NativeEmporiumLooks.NeonPink:NativeEmporiumLooks.NeonCyan,2.4);
        }
        private void DoorSigns()
        {
            // the kitchen doors' lintel signs (over the 2.4 m doors, 3 m up: nobody reaches them)
            foreach(var b in all)
            {
                if(!Has(b.Id,"-kwall-")||b.Cz<0)continue;bool a=Has(b.Id,"-a-");if(!a&&!Has(b.Id,"-b-"))continue;
                double x=a?b.X1+.05:b.X0-.05,z0=-1.3,z1=1.3,y0=3.0,y1=3.7;Box(x-.06,x+.06,y0,y1,z0,z1,a?K_Red:K_Yellow);
                var sign=new Bx{X0=x-.06,X1=x+.06,Y0=y0,Y1=y1,Z0=z0,Z1=z1,Id=b.Id,Index=-1};Neon(sign,a?0:1,0,y0+.14,.42,a?"KETCHUP":"MUSTARD",0xFFF2C0,2.4);
            }
        }
        private void Fryer(Bx b)
        {
            int front=b.Cx<0?0:1;
            Face(b,front,b.Z0+.1,b.Z1-.1,.15,1.05,K_Chrome,-1,.03,.015);for(int k=0;k<4;k++){double u=b.Z0+.6+k*(b.Sz-1.2)/3;Face(b,front,u-.12,u+.12,.7,.94,K_Dark,-1,.05,.03);Face(b,front,u-.04,u+.04,.78,.86,K_Red,-1,.06,.04);}
            Face(b,front,b.Z0+.1,b.Z1-.1,1.05,1.15,K_Dark,-1,.06,.03);
            for(int k=0;k<4;k++){double u=b.Z0+.6+k*(b.Sz-1.2)/3;Face(b,front,u-.3,u+.3,1.2,1.65,K_Dark,-1,.12,.13);Face(b,front,u-.32,u+.32,1.6,1.66,K_Chrome,-1,.14,.14);Face(b,front,u-.03,u+.03,1.66,2.0,K_Chrome,-1,.05,.12);}
            Face(b,front,b.Z0,b.Z1,2.6,4.0,K_Chrome,-1,.22,.2,true);Neon(b,front,b.Cz,3.05,.5,"FRIES",NativeEmporiumLooks.NeonLemon,2.6);
            foreach(int s in new[]{2,3})Face(b,s,b.X0+.05,b.X1-.05,1.2,2.5,K_Red,-1,Decal,.006);
        }
        private void Shelf(Bx b)
        {
            bool alongX=b.Sx>=b.Sz;int[] faces=alongX?new[]{2,3}:new[]{0,1};int seed=b.Index*13;
            foreach(int s in faces)
            {
                Span(b,s,out double u0,out double u1);Face(b,s,u0,u0+.08,0,b.Y1,K_Chrome,-1,.06,.03);Face(b,s,u1-.08,u1,0,b.Y1,K_Chrome,-1,.06,.03);
                for(double v=.3;v<b.Y1-.3;v+=.78)
                {
                    Face(b,s,u0+.08,u1-.08,v,v+.05,K_Chrome,-1,.16,.12);
                    double u=u0+.18;int n=0;
                    while(u<u1-.3)
                    {
                        double r=H(seed,n,(int)(v*10));double w=.16+.16*H(seed,n,7),hh=.18+.22*H(seed,n,9);int kind=r<.3?K_Bun:r<.5?K_Ketchup:r<.68?K_Mustard:r<.82?K_Relish:r<.92?K_Onion:K_Pickle;
                        Face(b,s,u,u+w,v+.05,v+.05+hh,kind,-1,.14,.12,false);if(kind==K_Ketchup||kind==K_Mustard)Face(b,s,u+w*.3,u+w*.7,v+.05+hh,v+.12+hh,K_White,-1,.08,.09);
                        u+=w+.05;n++;
                    }
                }
                Face(b,s,u0+.08,u1-.08,b.Y1-.3,b.Y1,K_Red,-1,.03,.02);
            }
        }
        private void Icebox(Bx b){Face(b,4,b.X0+.04,b.X1-.04,b.Z0+.04,b.Z1-.04,K_Chrome);foreach(int s in new[]{0,1,2,3}){Span(b,s,out double u0,out double u1);Face(b,s,u0,u1,b.Y1-.12,b.Y1-.06,K_Chrome,-1,.03,.02);}Neon(b,b.Cx<0?0:1,b.Cz,.42,.3,"ICE",NativeEmporiumLooks.NeonCyan,2.2);}
        private void Plinth(Bx b)
        {
            foreach(int s in new[]{0,1,2,3})
            {
                Span(b,s,out double u0,out double u1);double c=(u0+u1)/2;
                Face(b,s,u0,u1,0,.3,K_Yellow,-1,.04,.02);Face(b,s,u0,u1,5.6,5.75,K_Yellow,-1,.04,.02);Face(b,s,u0,u1,5.75,6.0,K_Red,-1,.03,.02);
                Face(b,s,c-1.0,c+1.0,1.6,2.35,K_Red,-1,.03,.012);Neon(b,s,c,1.72,.5,"TOP DOG",NativeEmporiumLooks.NeonLemon,2.6);
                Face(b,s,u0+.1,u1-.1,.3,.42,K_Chrome,-1,.03,.02);for(double v=3.0;v<5.4;v+=.6)Face(b,s,u0+.2,u1-.2,v,v+.04,K_Yellow,-1,Decal,.006);
            }
            Box(b.X0-.2,b.X1+.2,0,.15,b.Z0-.2,b.Z1+.2,K_Chrome);
        }
        private void PressPost(Bx b)
        {
            foreach(int s in new[]{0,1,2,3}){Span(b,s,out double u0,out double u1);Face(b,s,u0,u1,0,.9,K_Stripe,-1,Decal,.005);Face(b,s,u0,u1,3.2,3.3,K_Red,-1,.03,.02);}
            Box(b.X0-.06,b.X1+.06,b.Y1-.3,b.Y1,b.Z0-.06,b.Z1+.06,K_Red);
        }
        private void PressHead(Bx b)
        {
            foreach(int s in new[]{0,1,2,3}){Span(b,s,out double u0,out double u1);Face(b,s,u0,u1,b.Y0+.42,b.Y0+.56,K_Chrome,-1,.04,.02);Face(b,s,u0+.2,u1-.2,b.Y0+.06,b.Y0+.38,K_Dark,-1,Decal,.006);}
            foreach(int s in new[]{0,1}){double c=b.Cz;Neon(b,s,c,b.Y0+.1,.24,"BUN PRESS",NativeEmporiumLooks.NeonLemon,2.4);}
            for(int k=0;k<3;k++){double x=b.X0+.7+k*(b.Sx-1.4)/2;Box(x-.18,x+.18,b.Y1,b.Y1+.08,b.Z1-.6,b.Z1-.24,K_Chrome);Box(x-.03,x+.03,b.Y1+.08,b.Y1+.22,b.Z1-.45,b.Z1-.39,K_Red);}
        }
        private void Tower(Bx b)
        {
            bool k=NativeEmporiumLooks.KetchupSide(b.Cx);int sauce=k?K_Ketchup:K_Mustard;string word=k?"KETCHUP":"MUSTARD";
            foreach(int s in new[]{0,1,2,3})
            {
                Span(b,s,out double u0,out double u1);double c=(u0+u1)/2;bool lettered=s>=2;
                Face(b,s,u0+.12,u1-.12,2.0,3.5,K_White,-1,.02,.012);Face(b,s,u0+.12,u1-.12,1.92,2.0,K_Dark,-1,.02,.012);Face(b,s,u0+.12,u1-.12,3.5,3.58,K_Dark,-1,.02,.012);
                if(lettered)Neon(b,s,c,2.5,.32,word,k?NativeEmporiumLooks.KetchupHex:0xE89A00,1.8,.05);
                for(int d=0;d<3;d++){double u=u0+.35+H(b.Index,s,d)*(u1-u0-.7),len=.4+.9*H(b.Index,s,d+5);Face(b,s,u-.07,u+.07,b.Y1-len,b.Y1,sauce,-1,.03,.02);Face(b,s,u-.1,u+.1,b.Y1-len-.14,b.Y1-len,sauce,-1,.05,.03);}
                Face(b,s,u0,u1,.0,.35,K_Dark,-1,.02,.012);
            }
            double cx=b.Cx,cz=b.Cz;
            Box(b.X0+.1,b.X1-.1,b.Y1,b.Y1+.35,b.Z0+.1,b.Z1-.1,sauce);Box(cx-.7,cx+.7,b.Y1+.35,b.Y1+1.0,cz-.7,cz+.7,K_White);Add(cx,b.Y1+.68,cz,.7,.325,.7,FactoryQ.Axis(0,1,0,Math.PI/4),K_White);
            Box(cx-.32,cx+.32,b.Y1+1.0,b.Y1+1.5,cz-.32,cz+.32,K_White);Box(cx-.14,cx+.14,b.Y1+1.5,b.Y1+1.95,cz-.14,cz+.14,K_White);Box(cx-.1,cx+.1,b.Y1+1.95,b.Y1+2.05,cz-.1,cz+.1,sauce);
        }
        private void Prep(Bx b)
        {
            foreach(int s in new[]{0,1,2,3}){Span(b,s,out double u0,out double u1);Face(b,s,u0,u1,b.Y1-.08,b.Y1,K_Chrome,-1,.03,.02);Face(b,s,u0+.05,u1-.05,.1,b.Y1-.12,K_Dark,-1,Decal,.006);}
            Face(b,4,b.X0+.04,b.X1-.04,b.Z0+.04,b.Z1-.04,K_Chrome);
            Box(b.Cx-.36,b.Cx+.36,b.Y1,b.Y1+.1,b.Z0+.15,b.Z1-.15,K_Chrome);Box(b.Cx-.38,b.Cx-.34,b.Y1+.1,b.Y1+.24,b.Z0+.15,b.Z1-.15,K_Dark,false);Box(b.Cx+.34,b.Cx+.38,b.Y1+.1,b.Y1+.24,b.Z0+.15,b.Z1-.15,K_Dark,false);
        }
        private void Crate(Bx b)
        {
            foreach(int s in new[]{0,1,2,3}){Span(b,s,out double u0,out double u1);for(double v=b.Y0+.1;v<b.Y1-.05;v+=.36)Face(b,s,u0+.04,u1-.04,v,v+.06,K_Dark,-1,Decal,.006);Face(b,s,u0+.3,u1-.3,b.Y0+.45,b.Y0+.72,K_Red,-1,Decal,.008);}
            if(b.Y1<1.3)for(int k=0;k<3;k++){double x=b.X0+.3+k*.3;Box(x-.12,x+.12,b.Y1,b.Y1+.08,b.Cz-.25,b.Cz+.25,K_Bun,false);}
        }
        private void BridgeDeck(Bx b)
        {
            // a hot dog to walk on: the frankfurter deck, the bun halves for kerbs, mustard down its middle, sesame on the bun
            foreach(int s in new[]{0,1}){double x=s==0?b.X1:b.X0,o=s==0?1:-1;Box(x,x+o*.22,b.Y0-.2,b.Y1+.14,b.Z0-.15,b.Z1+.15,K_BunTop);Box(x,x+o*.18,b.Y0-.24,b.Y0-.2,b.Z0,b.Z1,K_Bun);
                for(int k=0;k<10;k++){double z=b.Z0+.3+k*(b.Sz-.6)/9;Box(x+o*.06,x+o*.16,b.Y1+.14,b.Y1+.16,z-.05,z+.05,K_Sesame,false);}}
            int n=12;for(int k=0;k<n;k++){double z0=b.Z0+.2+k*(b.Sz-.4)/n,z1=z0+(b.Sz-.4)/n,x0=b.Cx+((k&1)==0?-.35:.35),x1=b.Cx+((k&1)==0?.35:-.35);Bar(x0,b.Y1+.006,z0,x1,b.Y1+.006,z1,.12,K_Mustard,false);}
            foreach(int s in new[]{2,3}){Span(b,s,out double u0,out double u1);Face(b,s,u0,u1,b.Y0,b.Y1,K_SausageDark(),-1,.03,.012);}
        }
        private static int K_SausageDark()=>NativeEmporiumLooks.SausageDark;
        private void Step(Bx b,int riser)
        {
            bool alongX=b.Sx<b.Sz;// the step's run is its short axis
            Face(b,4,b.X0,b.X1,b.Z0,b.Z1,K_Cream);
            if(alongX){Face(b,4,b.X0,b.X0+.05,b.Z0,b.Z1,K_Chrome,-1,.02,.012);Face(b,4,b.X1-.05,b.X1,b.Z0,b.Z1,K_Chrome,-1,.02,.012);}else{Face(b,4,b.X0,b.X1,b.Z0,b.Z0+.05,K_Chrome,-1,.02,.012);Face(b,4,b.X0,b.X1,b.Z1-.05,b.Z1,K_Chrome,-1,.02,.012);}
            foreach(int s in alongX?new[]{0,1}:new[]{2,3}){Span(b,s,out double u0,out double u1);Face(b,s,u0,u1,b.Y1-.2,b.Y1,riser,-1,Decal,.004);}
            foreach(int s in alongX?new[]{2,3}:new[]{0,1}){Span(b,s,out double u0,out double u1);Face(b,s,u0,u1,Math.Max(0,b.Y1-.06),b.Y1,K_Chrome,-1,Decal,.006);}
        }
        private void Balcony(Bx b)
        {
            int front=b.Cz>0?3:2;Span(b,front,out double u0,out double u1);
            Face(b,front,u0,u1,b.Y0,b.Y1,K_Teal,-1,.03,.012);Face(b,front,u0,u1,b.Y0+.12,b.Y0+.18,K_Tube,NativeEmporiumLooks.NeonPink,.04,.03,false,2.6);Face(b,front,u0,u1,b.Y1-.05,b.Y1,K_Chrome,-1,.03,.02);
            for(double u=u0+1.2;u<u1-.6;u+=2.4)Face(b,5,u-.25,u+.25,b.Cz-.3,b.Cz+.3,K_Lamp,0xFFE6B0,Decal,.004);
            Face(b,4,u0,u1,b.Z0,b.Z0+.1,K_Chrome);
        }
        private void Post(Bx b){foreach(int s in new[]{0,1,2,3}){Span(b,s,out double u0,out double u1);Face(b,s,u0,u1,0,.3,K_Red,-1,.03,.012);Face(b,s,u0,u1,2.0,2.1,K_Red,-1,.03,.012);}Box(b.X0-.08,b.X1+.08,b.Y1-.2,b.Y1,b.Z0-.08,b.Z1+.08,K_Chrome);}
        private void Rail(Bx b)
        {
            bool alongX=b.Sx>=b.Sz;foreach(int s in alongX?new[]{2,3}:new[]{0,1}){Span(b,s,out double u0,out double u1);Face(b,s,u0,u1,b.Y1-.1,b.Y1,K_Chrome,-1,.03,.02);Face(b,s,u0,u1,b.Y0,b.Y0+.12,K_Chrome,-1,.03,.02);Face(b,s,u0+.02,u1-.02,b.Y0+.12,b.Y1-.1,K_Teal,-1,Decal,.006);
                for(double u=u0+.05;u<u1;u+=1.2)Face(b,s,u,u+.06,b.Y0,b.Y1,K_Chrome,-1,.03,.02);}
            Face(b,4,b.X0,b.X1,b.Z0,b.Z1,K_Chrome);
        }
        private void Booth(Bx b)
        {
            if(Has(b.Id,"-table"))
            {
                Face(b,4,b.X0,b.X1,b.Z0,b.Z1,K_Cream,0xFFF6E8);foreach(int s in new[]{0,1,2,3}){Span(b,s,out double u0,out double u1);Face(b,s,u0,u1,b.Y1-.07,b.Y1,K_Chrome,-1,.03,.02);Face(b,s,u0,u1,0,b.Y1-.07,K_Dark,-1,Decal,.004);}
                double cx=b.Cx,cz=b.Cz;Box(cx-.06,cx+.06,b.Y1,b.Y1+.2,cz-.2,cz-.08,K_Ketchup,false);Box(cx-.06,cx+.06,b.Y1,b.Y1+.2,cz+.08,cz+.2,K_Mustard,false);Box(cx-.025,cx+.025,b.Y1+.2,b.Y1+.24,cz-.16,cz-.12,K_White,false);Box(cx-.025,cx+.025,b.Y1+.2,b.Y1+.24,cz+.12,cz+.16,K_White,false);
                Box(cx-.12,cx+.12,b.Y1,b.Y1+.16,cz-.05,cz+.05,K_Chrome,false);
                return;
            }
            // a bench: tufted vinyl toward the table (the side away from the wall), piping, a chrome cap; the seat cushion
            int toward=b.Id.EndsWith("-back-n",StringComparison.Ordinal)?3:b.Id.EndsWith("-back-s",StringComparison.Ordinal)?2:b.Cz>0?3:2;
            Span(b,toward,out double a0,out double a1);
            Face(b,toward,a0+.05,a1-.05,b.Y0+.45,b.Y1-.06,K_Vinyl,-1,.04,.02);for(double u=a0+.25;u<a1-.15;u+=.32)Face(b,toward,u-.012,u+.012,b.Y0+.55,b.Y1-.12,K_Dark,-1,.02,.045);
            Face(b,toward,a0,a1,b.Y1-.06,b.Y1,K_White,-1,.03,.03);Face(b,4,b.X0,b.X1,b.Z0,b.Z1,K_Chrome);
            Face(b,toward,a0+.02,a1-.02,b.Y0+.3,b.Y0+.46,K_Vinyl,0xE0303E,.24,.22);Face(b,toward,a0,a1,b.Y0,b.Y0+.3,K_Chrome,-1,Decal,.004);
            foreach(int s in toward<=1?new[]{2,3}:new[]{0,1}){Span(b,s,out double u0,out double u1);Face(b,s,u0,u1,b.Y0,b.Y1,K_Vinyl,-1,Decal,.004);}
        }
        private void Counter(Bx b)
        {
            int front=b.Cz>0?3:2;Span(b,front,out double u0,out double u1);
            Face(b,front,u0,u1,0,.14,K_Dark);Face(b,front,u0,u1,.14,b.Y1-.12,K_Mint,-1,.02,.012);for(double v=.32;v<b.Y1-.2;v+=.22)Face(b,front,u0,u1,v,v+.035,K_Chrome,-1,.02,.02);
            Face(b,4,b.X0,b.X1,b.Z0,b.Z1,K_Pink,0xFF9EC4);Face(b,front,u0,u1,b.Y1-.12,b.Y1,K_Chrome,-1,.04,.03);
            for(double u=u0+.5;u<u1-.3;u+=.9){At(b,front,u,0,0,out double x,out double y,out double z,out double nx,out double nz);Box(x-.04+nx*.08,x+.04+nx*.08,0,.72,z-.04+nz*.08,z+.04+nz*.08,K_Chrome,false);Box(x-.18+nx*.06,x+.18+nx*.06,.72,.82,z-.18+nz*.06,z+.18+nz*.06,K_Red,true);}
            double gx=b.Cx;Box(gx-1.6,gx+1.6,b.Y1,b.Y1+.1,b.Z0+.15,b.Z1-.15,K_Chrome);Box(gx-1.6,gx+1.6,b.Y1+.1,b.Y1+.12,b.Z0+.15,b.Z0+.2,K_Dark,false);
            Box(gx-1.6,gx+1.6,b.Y1+.24,b.Y1+.25,b.Z0+.1,b.Z1-.1,K_Glass,false,0xBFE8FF);
            double rx=b.X1-.6;Box(rx-.25,rx+.25,b.Y1,b.Y1+.22,b.Cz-.2,b.Cz+.2,K_Dark);Box(rx-.2,rx+.2,b.Y1+.18,b.Y1+.24,b.Cz-.22,b.Cz-.1,K_Screen,false,-1,1.6);
            double px=b.X1-1.7;Box(px-.18,px+.18,b.Y1,b.Y1+.03,b.Cz-.18,b.Cz+.18,K_Chrome);Box(px-.14,px+.14,b.Y1+.03,b.Y1+.2,b.Cz-.14,b.Cz+.14,K_Glass,false,0xFFD8E8);
        }
        private void Soda(Bx b)
        {
            int front=b.Cz>0?3:2;Span(b,front,out double u0,out double u1);double c=(u0+u1)/2;
            Face(b,front,u0+.1,u1-.1,1.45,2.35,K_Screen,0x2CE8FF,.02,.012,false,1.8);Neon(b,front,c,1.62,.5,"SODA",0xFFF2C0,2.4);
            int[] taps={NativeEmporiumLooks.KetchupHex,NativeEmporiumLooks.MustardHex,0x5CFFB8,0xFF6FAE,0x8B5CFF};for(int k=0;k<5;k++){double u=u0+.3+k*(u1-u0-.6)/4;Face(b,front,u-.05,u+.05,1.05,1.32,K_Chrome,-1,.12,.12);Face(b,front,u-.06,u+.06,1.25,1.42,K_Red,taps[k],.16,.17);}
            Face(b,front,u0+.15,u1-.15,.78,.86,K_Dark,-1,.12,.12);Face(b,front,u0,u1,0,.2,K_Dark);Face(b,front,u0,u1,2.45,2.6,K_Red,-1,.03,.02);
        }
        private void JukeboxBody(Bx b)
        {
            int front=b.Cz>0?3:2;Span(b,front,out double u0,out double u1);double c=(u0+u1)/2;
            Face(b,front,u0+.1,u1-.1,2.15,2.55,K_Chrome,-1,.05,.03);Face(b,front,u0+.2,u1-.2,1.25,2.05,K_Glass,0xFFC070,.02,.02);Face(b,front,u0+.2,u1-.2,.3,1.05,K_Dark,-1,.02,.012);
            for(double v=.38;v<1.0;v+=.12)Face(b,front,u0+.24,u1-.24,v,v+.03,K_Chrome,-1,.02,.025);Face(b,front,c-.3,c+.3,1.1,1.2,K_Tube,NativeEmporiumLooks.NeonLemon,.03,.03,false,2.2);
        }
        private void Pump(Bx b)
        {
            bool k=NativeEmporiumLooks.KetchupSide(b.Cx);int sauce=k?K_Ketchup:K_Mustard;
            foreach(int s in new[]{0,1,2,3}){Span(b,s,out double u0,out double u1);double c=(u0+u1)/2;Face(b,s,u0,u1,0,b.Y1,sauce,-1,Decal,.004);Face(b,s,u0+.08,u1-.08,1.3,2.25,K_White,-1,.02,.012);
                Neon(b,s,c,1.5,.28,"PUMP",k?NativeEmporiumLooks.KetchupHex:0xE89A00,1.6,.045);Face(b,s,c-.18,c+.18,1.95,2.15,K_Dark,-1,.02,.02);}
        }
        private void Kiosk(Bx b)
        {
            foreach(int s in new[]{2,3})
            {
                Span(b,s,out double u0,out double u1);double c=(u0+u1)/2;Face(b,s,u0,u1,b.Y0,b.Y0+.3,K_Dark);
                Face(b,s,u0+.1,u1-.1,b.Y0+.4,b.Y1-.1,K_Dark,-1,.02,.008);Neon(b,s,c,b.Y1-.62,.42,"MENU",NativeEmporiumLooks.NeonPink,2.6);
                for(int r=0;r<3;r++){double v=b.Y0+.6+r*.42;Face(b,s,u0+.25,u1-.25,v,v+.3,K_Screen,r==1?0xFF8A2A:0x2CE8FF,.015,.012,false,1.6);for(int k=0;k<4;k++){double u=u0+.4+k*.72;Face(b,s,u,u+.4,v+.12,v+.18,K_White,-1,.01,.016);}}
            }
        }
        private void Fridge(Bx b)
        {
            foreach(int s in new[]{0,1,2,3})
            {
                Span(b,s,out double u0,out double u1);if(u1-u0<1.0)continue;double c=(u0+u1)/2;
                Face(b,s,c-.02,c+.02,.1,b.Y1-.1,K_Dark,-1,Decal,.006);Face(b,s,c-.18,c-.1,1.0,1.7,K_Chrome,-1,.06,.04);Face(b,s,c+.1,c+.18,1.0,1.7,K_Chrome,-1,.06,.04);
                Face(b,s,u0+.15,u1-.15,b.Y1-.55,b.Y1-.15,K_Teal,-1,.02,.012);if(s==Toward(b,0,b.Cz>0?b.Cz-10:b.Cz+10))Neon(b,s,c,b.Y1-.5,.26,"ICE COLD",NativeEmporiumLooks.NeonCyan,2.0,.04);
                for(int k=0;k<4;k++){double u=u0+.3+H(b.Index,s,k)*(u1-u0-.6);Face(b,s,u-.15,u+.15,.15,.4+H(b.Index,k)*.4,K_White,0xDCF4FF,Decal,.01);}
            }
        }
        private void Barrel(Bx b)
        {
            foreach(int s in new[]{0,1,2,3}){Span(b,s,out double u0,out double u1);Face(b,s,u0,u1,.18,.28,K_Dark,-1,.03,.02);Face(b,s,u0,u1,b.Y1-.28,b.Y1-.18,K_Dark,-1,.03,.02);Face(b,s,(u0+u1)/2-.35,(u0+u1)/2+.35,.45,.75,K_Cream,-1,Decal,.008);}
            Add(b.Cx,b.Y1+.09,b.Cz,.12,.09,.36,FactoryQ.Axis(0,1,0,.6),K_Pickle,false);
        }

        // ---------------- overhead ----------------
        /// <summary>The glazed roof's arch height at z (an elliptic vault from the eaves to the ridge).</summary>
        public static double Arch(double z){double u=Math.Min(1,Math.Abs(z)/NativeEmporiumLooks.RoofHalfZ);return NativeEmporiumLooks.Eaves+(NativeEmporiumLooks.Ridge-NativeEmporiumLooks.Eaves)*Math.Sqrt(Math.Max(0,1-u*u));}
        private void Roof()
        {
            double hz=NativeEmporiumLooks.RoofHalfZ,hx=NativeEmporiumLooks.RoofHalfX;const int seg=10;
            for(double x=-hx;x<=hx+1e-6;x+=4.0)
            {
                for(int k=0;k<seg;k++){double z0=-hz+2*hz*k/seg,z1=-hz+2*hz*(k+1)/seg;Bar(x,Arch(z0),z0,x,Arch(z1),z1,.26,K_Iron,true);Bar(x,Arch(z0)+.55,z0*.98,x,Arch(z1)+.55,z1*.98,.12,K_Iron,true);}
                for(int k=1;k<seg;k++){double z=-hz+2*hz*k/seg;Bar(x,Arch(z),z,x,Arch(z)+.55,z*.98,.08,K_Iron,true);}
            }
            foreach(double z in new[]{-21.0,-17.0,-12.6,-8.4,-4.2,0.0,4.2,8.4,12.6,17.0,21.0})Box(-hx,hx,Arch(z)-.06,Arch(z)+.06,z-.06,z+.06,K_Iron,true);
            Box(-hx,hx,NativeEmporiumLooks.Eaves-.25,NativeEmporiumLooks.Eaves+.05,hz-.4,hz,K_Iron,true);Box(-hx,hx,NativeEmporiumLooks.Eaves-.25,NativeEmporiumLooks.Eaves+.05,-hz,-hz+.4,K_Iron,true);
        }
        private void Pendants()
        {
            foreach(double z in new[]{-17.6,17.6,-21.2,21.2})for(double x=-25;x<=25.01;x+=5)
            {
                bool bal=Math.Abs(z)>20&&Math.Abs(x)<14;double y=bal?7.4:7.8;if(Math.Abs(z)>20&&!bal)continue;
                Box(x-.012,x+.012,y+.62,Arch(z),z-.012,z+.012,K_Dark,false);Box(x-.07,x+.07,y+.5,y+.62,z-.07,z+.07,K_Chrome,true);Add(x,y+.38,z,.14,.12,.14,FactoryQ.Axis(0,1,0,Math.PI/4),K_Red,true);Add(x,y+.2,z,.26,.08,.26,FactoryQ.Axis(0,1,0,Math.PI/4),K_Red,true);Add(x,y+.08,z,.34,.05,.34,FactoryQ.Axis(0,1,0,Math.PI/4),K_Red,true);Box(x-.35,x+.35,y+.02,y+.04,z-.35,z+.35,K_Chrome,false);Box(x-.1,x+.1,y-.12,y+.04,z-.1,z+.1,K_Tube,false,0xFFF0C8,2.6);
            }
        }
        private void ConveyorRails()
        {
            double y=NativeEmporiumRig.ConveyorY,x0=NativeEmporiumRig.ConveyorX0,x1=NativeEmporiumRig.ConveyorX1,z=NativeEmporiumRig.ConveyorZ;
            foreach(double s in new[]{-1.0,1.0})
            {
                double a=s*x0,b2=s*x1;Box(Math.Min(a,b2)-.08,Math.Max(a,b2)+.08,y-.06,y+.06,z-.08,z+.08,K_Chrome);Box(Math.Min(a,b2)-.08,Math.Max(a,b2)+.08,y-.06,y+.06,-z-.08,-z+.08,K_Chrome);
                Box(a-.08,a+.08,y-.06,y+.06,-z,z,K_Chrome);Box(b2-.08,b2+.08,y-.06,y+.06,-z,z,K_Chrome);
                foreach(double zz in new[]{-z,z})for(double x=Math.Min(a,b2)+1;x<=Math.Max(a,b2)+.01;x+=7)Box(x-.025,x+.025,y+.06,Arch(zz),zz-.025,zz+.025,K_Dark,false);
            }
        }
    }
}
