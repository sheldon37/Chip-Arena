using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>One dressing box drawn through NativeBlockLit (FACTORY_PROPOSAL.md 5.3): centre, half extents, rotation, the rest
    /// centre its pattern is glued to, look id, sRGB colour, look flags, seed, window light and whether it casts a shadow.</summary>
    public struct FactoryPart {public float X,Y,Z,Hx,Hy,Hz,Qx,Qy,Qz,Qw,Rx,Ry,Rz,Seed,Window;public int Look,Color,Flags;public bool Shadow;}

    /// <summary>A quaternion in Unity's (x, y, z, w) layout and maths (Quaternion.AngleAxis, *).</summary>
    public struct FactoryQ
    {
        public double X,Y,Z,W;
        public static readonly FactoryQ Identity=new FactoryQ{W=1};
        public static FactoryQ Axis(double ax,double ay,double az,double radians){double s=Math.Sin(radians/2);return new FactoryQ{X=ax*s,Y=ay*s,Z=az*s,W=Math.Cos(radians/2)};}
        public static FactoryQ operator*(FactoryQ a,FactoryQ b)=>new FactoryQ{W=a.W*b.W-a.X*b.X-a.Y*b.Y-a.Z*b.Z,X=a.W*b.X+a.X*b.W+a.Y*b.Z-a.Z*b.Y,Y=a.W*b.Y-a.X*b.Z+a.Y*b.W+a.Z*b.X,Z=a.W*b.Z+a.X*b.Y-a.Y*b.X+a.Z*b.W};
        public void Rotate(double vx,double vy,double vz,out double ox,out double oy,out double oz)
        {double tx=2*(Y*vz-Z*vy),ty=2*(Z*vx-X*vz),tz=2*(X*vy-Y*vx);ox=vx+W*tx+(Y*tz-Z*ty);oy=vy+W*ty+(Z*tx-X*tz);oz=vz+W*tz+(X*ty-Y*tx);}
        /// <summary>Unity's Quaternion.Euler(pitch, yaw, 0) in radians: yaw about y after pitch about x.</summary>
        public static FactoryQ YawPitch(double yaw,double pitch)=>Axis(0,1,0,yaw)*Axis(1,0,0,pitch);
    }

    /// <summary>
    /// The Factory's dressing kit (FACTORY_PROPOSAL.md 5.3): presentation boxes attached by block id prefix and by hazard, drawn
    /// as NativeBlockLit instances by Runtime/NativeFactoryPresentation.cs, never sim data and never colliders. Each part takes
    /// its look from MapLooks.json `kitLooks` by its kind id ("factory-kit-rail", ...; <see cref="Kinds"/>). Rules (2.1 rule 1):
    /// below 3 m nothing stands more than 0.25 m proud of the block it dresses, and anything thin enough to walk through stays
    /// against a surface. Static parts (<see cref="Build"/>) attach only to `static` blocks and the hazards' fixed fittings,
    /// so they never need to follow a moving or breaking block; the moving rigs are <see cref="NativeFactoryRig"/>.
    /// Deterministic: the same blocks, hazards and looks give the same parts. Build allocates; the rig does not.
    /// </summary>
    public static class NativeFactoryKit
    {
        /// <summary>Kind ids, resolved once per look set through kitLooks (unlisted: plain white).</summary>
        public static readonly string[] Kinds={"factory-kit-rail","factory-kit-toe","factory-kit-bracket","factory-kit-stripe","factory-kit-truss","factory-kit-glass","factory-kit-frame",
            "factory-kit-pipe-steam","factory-kit-pipe-water","factory-kit-pipe-gas","factory-kit-flange","factory-kit-valve","factory-kit-lamp","factory-kit-void","factory-kit-plate",
            "factory-kit-paint-walk","factory-kit-paint-zone","factory-kit-paint-white","factory-kit-blade","factory-kit-hub","factory-kit-crane","factory-kit-crane-dark","factory-kit-cab-glass",
            "factory-kit-cable","factory-kit-hook","factory-kit-load","factory-kit-strap","factory-kit-ram","factory-kit-grate","factory-kit-bar","factory-kit-coil","factory-kit-wheel",
            "factory-kit-panel","factory-kit-sign","factory-kit-ladle","factory-kit-ladle-rim","factory-kit-track","factory-kit-concrete","factory-kit-door","factory-kit-window",
            "factory-kit-machine","factory-kit-chrome","factory-kit-black","factory-kit-motor","factory-kit-timber","factory-kit-danger","factory-kit-joint","factory-kit-box","factory-kit-bin",
            "factory-kit-pipe-air","factory-kit-cladding","factory-kit-roof","factory-kit-red","factory-kit-green"};
        public const int Rail=0,Toe=1,Bracket=2,Stripe=3,Truss=4,Glass=5,Frame=6,PipeSteam=7,PipeWater=8,PipeGas=9,Flange=10,Valve=11,Lamp=12,Void=13,Plate=14,PaintWalk=15,PaintZone=16,PaintWhite=17,
            Blade=18,Hub=19,CraneBody=20,CraneDark=21,CabGlass=22,Cable=23,Hook=24,Load=25,Strap=26,Ram=27,Grate=28,Bar=29,Coil=30,Wheel=31,Panel=32,Sign=33,Ladle=34,LadleRim=35,Track=36,
            Concrete=37,Door=38,Window=39,Machine=40,Chrome=41,Black=42,Motor=43,Timber=44,Danger=45,Joint=46,Carton=47,Bin=48,PipeAir=49,Cladding=50,RoofTop=51,Red=52,Green=53;
        /// <summary>Kinds' resolved looks (index = kind): NativeBlockLooks look id, sRGB colour, flags.</summary>
        public sealed class Looks {public readonly int[] Look=new int[Kinds.Length],Color=new int[Kinds.Length],Flags=new int[Kinds.Length];}
        public static Looks Resolve(MapLookBlock[] kitLooks)
        {
            var l=new Looks();for(int k=0;k<Kinds.Length;k++){var c=NativeBlockLooks.Resolve(kitLooks,null,Kinds[k],"",0xffffff);l.Look[k]=c.Look;l.Color[k]=c.Color;l.Flags[k]=c.Flags&~(NativeBlockLooks.Band|NativeBlockLooks.Tag);}
            return l;
        }
        /// <summary>The chamfer a part is drawn with (Runtime/NativeFactoryPresentation): 3 cm, at most 30% of its thinnest half.</summary>
        public static float Bevel(float hx,float hy,float hz)=>Math.Min(.03f,.3f*Math.Min(hx,Math.Min(hy,hz)));
        /// <summary>The static kit's part count at most (the draw budget: 2.4 dressing ≤60 draws of 500).</summary>
        public const int MaxParts=12000;

        /// <summary>A writer of parts: grows while building the static kit, refuses past its capacity in a rig frame.</summary>
        public sealed class Writer
        {
            public FactoryPart[] Items;public int Count;private readonly bool grow;private readonly Looks looks;
            public Writer(int capacity,bool grow,Looks looks){Items=new FactoryPart[capacity];this.grow=grow;this.looks=looks;}
            public void Clear()=>Count=0;
            /// <summary>Adds a part: centre, half extents, rotation, kind, shadow; colour -1 the kind's; rest the pattern's origin (NaN: the centre).</summary>
            public void Add(double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=true,int color=-1,double window=1,double rx=double.NaN,double ry=0,double rz=0)
            {
                if(!(hx>0&&hy>0&&hz>0))return;if(Count>=Items.Length){if(!grow)return;Array.Resize(ref Items,Items.Length*2);}
                bool rest=!double.IsNaN(rx);uint h=unchecked((uint)(Math.Floor(x*7.3)*73856093)^(uint)(Math.Floor(y*5.1)*19349663)^(uint)(Math.Floor(z*6.7)*83492791)^(uint)kind*2654435761u);
                Items[Count++]=new FactoryPart{X=(float)x,Y=(float)y,Z=(float)z,Hx=(float)hx,Hy=(float)hy,Hz=(float)hz,Qx=(float)q.X,Qy=(float)q.Y,Qz=(float)q.Z,Qw=(float)q.W,Rx=(float)(rest?rx:x),Ry=(float)(rest?ry:y),Rz=(float)(rest?rz:z),
                    Seed=(h&0xFFFFFF)/16777216f,Window=(float)window,Look=looks.Look[kind],Color=color>=0?color:looks.Color[kind],Flags=looks.Flags[kind],Shadow=shadow};
            }
            public void Box(double x0,double x1,double y0,double y1,double z0,double z1,int kind,bool shadow=true,int color=-1,double window=1)=>Add((x0+x1)/2,(y0+y1)/2,(z0+z1)/2,(x1-x0)/2,(y1-y0)/2,(z1-z0)/2,FactoryQ.Identity,kind,shadow,color,window);
            /// <summary>A bar of section tx (horizontal) × ty from one point to another (local z along it).</summary>
            public void Bar(double x0,double y0,double z0,double x1,double y1,double z1,double tx,double ty,int kind,bool shadow=true,int color=-1)
            {
                double dx=x1-x0,dy=y1-y0,dz=z1-z0,l=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(l<1e-4)return;
                Add((x0+x1)/2,(y0+y1)/2,(z0+z1)/2,tx/2,ty/2,l/2,FactoryQ.YawPitch(Math.Atan2(dx,dz),Math.Asin(Math.Max(-1,Math.Min(1,-dy/l)))),kind,shadow,color);
            }
        }

        private struct Bx {public double X0,X1,Y0,Y1,Z0,Z1;public string Id;public bool Static;}
        private static bool Overlaps(Bx b,double x0,double x1,double y0,double y1,double z0,double z1)=>b.X0<x1&&b.X1>x0&&b.Y0<y1&&b.Y1>y0&&b.Z0<z1&&b.Z1>z0;
        private static bool Starts(string id,string prefix)=>id!=null&&id.StartsWith(prefix,StringComparison.Ordinal);

        /// <summary>Builds the static kit for these blocks and hazards (FACTORY_PROPOSAL.md 5.3 lists what each prefix gets).
        /// `layers`: parts that share a plane with a block or an earlier part stand clear of it (NativeSurfaceLayers.Over; off
        /// only for the surface audit's before count).</summary>
        public static Writer Build(ContentBlock[] blocks,FactoryHazard[] hazards,Looks looks,bool layers=true)
        {
            var w=new Writer(2048,true,looks);var all=new List<Bx>();
            foreach(var b in blocks??Array.Empty<ContentBlock>())
            {
                if(b==null||b.id==null)continue;var r=b.rotation;if(r!=null&&(Math.Abs(r.x)>1e-4||Math.Abs(r.y)>1e-4||Math.Abs(r.z)>1e-4))continue;
                all.Add(new Bx{X0=b.pos.x-b.size.x/2,X1=b.pos.x+b.size.x/2,Y0=b.pos.y-b.size.y/2,Y1=b.pos.y+b.size.y/2,Z0=b.pos.z-b.size.z/2,Z1=b.pos.z+b.size.z/2,Id=b.id,Static=b.role=="static"});
            }
            var rails=new List<Bx>();foreach(var h in hazards??Array.Empty<FactoryHazard>())if(h?.kind==NativeFactoryHazards.SawRail){double r=h.radius+.3;rails.Add(new Bx{X0=Math.Min(h.a.x,h.b.x)-r,X1=Math.Max(h.a.x,h.b.x)+r,Y0=Math.Min(h.a.y,h.b.y)-r,Y1=Math.Max(h.a.y,h.b.y)+r,Z0=Math.Min(h.a.z,h.b.z)-r,Z1=Math.Max(h.a.z,h.b.z)+r});}
            var flights=new Dictionary<string,List<Bx>>();
            foreach(var b in all)
            {
                if(!b.Static)continue;string id=b.Id;
                if(Starts(id,"factory-gallery-")||Starts(id,"factory-catwalk-")||id.EndsWith("-landing",StringComparison.Ordinal)||Starts(id,"factory-pulpit-")&&id.EndsWith("-deck",StringComparison.Ordinal))Deck(w,b,all,rails);
                else if(Starts(id,"factory-stair-")){int dash=id.LastIndexOf('-');string key=id.Substring(0,dash);if(!flights.TryGetValue(key,out var list))flights[key]=list=new List<Bx>();list.Add(b);}
                else if(Starts(id,"factory-column-"))Column(w,b);
                else if(Starts(id,"factory-runway-"))Runway(w,b);
                else if(Starts(id,"factory-wall-"))Wall(w,b,id.EndsWith("-clad",StringComparison.Ordinal));
                else if(Starts(id,"factory-baywall-"))BayWall(w,b,id.Contains("-lintel-"));
                else if(Starts(id,"factory-press-")&&id.EndsWith("-head",StringComparison.Ordinal))PressHead(w,b);
                else if(Starts(id,"factory-lathe-"))Lathe(w,b,id.EndsWith("-head",StringComparison.Ordinal));
                else if(Starts(id,"factory-mill-"))Mill(w,b);
                else if(Starts(id,"factory-cabinet-"))Cabinet(w,b);
                else if(Starts(id,"factory-rack-")||Starts(id,"factory-bay-rack-"))Rack(w,b);
                else if(Starts(id,"factory-coils-"))Coils(w,b);
                else if(Starts(id,"factory-crib-"))Crib(w,b);
                else if(Starts(id,"factory-shelf-"))Shelf(w,b);
                else if(Starts(id,"factory-screen-"))Screen(w,b);
                else if(Starts(id,"factory-bins-"))Bins(w,b);
                else if(Starts(id,"factory-dock-")&&id.EndsWith("-office",StringComparison.Ordinal))Office(w,b);
                else if(Starts(id,"factory-dock-")&&id.EndsWith("-container",StringComparison.Ordinal))Container(w,b);
                else if(Starts(id,"factory-dock-")&&id.EndsWith("-forklift",StringComparison.Ordinal))Forklift(w,b);
                else if(Starts(id,"factory-pulpit-")&&id.EndsWith("-roof",StringComparison.Ordinal))PulpitRoof(w,b);
                else if(id=="factory-ladle")LadleShell(w,b);
                else if(id=="factory-ladle-car")LadleCar(w,b);
                else if(Starts(id,"factory-bay-billets-"))Ends(w,b,.13,Bar,Joint);
                else if(Starts(id,"factory-bay-beams-"))Beams(w,b);
                else if(Starts(id,"factory-bay-ingots-")||Starts(id,"factory-bay-plates-"))Courses(w,b,Starts(id,"factory-bay-ingots-")?.3:.08);
                else if(Starts(id,"factory-bay-moulds-"))Courses(w,b,.35);
                else if(Starts(id,"factory-bay-hammer-"))Hammer(w,b);
                else if(id=="factory-floor")Floor(w,b);
            }
            foreach(var f in flights)Flight(w,f.Value,all);
            Roofs(w,all);
            foreach(var h in hazards??Array.Empty<FactoryHazard>())Fittings(w,h,all);
            Unique(w);if(layers)Layers(w,blocks);
            return w;
        }
        // A part two dressers both place (a post at a corner two rails share, a floor line laid twice) is drawn once: the later of
        // two parts with the same box (to 0.1 mm), look, colour, flags, rest, window and shadow is dropped.
        private static void Unique(Writer w)
        {
            var seen=new HashSet<string>();int keep=0;
            for(int i=0;i<w.Count;i++)
            {
                var p=w.Items[i];string R(float v)=>Math.Round(v*1e4).ToString(System.Globalization.CultureInfo.InvariantCulture);
                string key=string.Join(",",R(p.X),R(p.Y),R(p.Z),R(p.Hx),R(p.Hy),R(p.Hz),R(p.Qx),R(p.Qy),R(p.Qz),R(p.Qw),R(p.Rx),R(p.Ry),R(p.Rz),R(p.Window),p.Look,p.Color,p.Flags,p.Shadow?1:0);
                if(seen.Add(key))w.Items[keep++]=p;
            }
            w.Count=keep;
        }
        // Parts flush with a block or with an earlier part are drawn over it by the least whole steps (NativeSurfaceLayers):
        // the centre moves half the lift along the part's own axes and the half extent grows by half, so only the lifted face
        // moves; the rest centre moves with it, so the pattern stays where it was.
        private static void Layers(Writer w,ContentBlock[] blocks)
        {
            var boxes=new NativeSurfaceLayers.Box[w.Count];
            for(int i=0;i<w.Count;i++){var p=w.Items[i];boxes[i]=new NativeSurfaceLayers.Box{X=p.X,Y=p.Y,Z=p.Z,Hx=p.Hx,Hy=p.Hy,Hz=p.Hz,Qx=p.Qx,Qy=p.Qy,Qz=p.Qz,Qw=p.Qw,Bevel=Bevel(p.Hx,p.Hy,p.Hz),Material=NativeSurfaceLayers.Key(p.Look,p.Color,p.Flags)};}
            var lifts=NativeSurfaceLayers.Over(blocks,boxes,out _);
            for(int i=0;i<w.Count;i++)
            {
                NativeSurfaceLayers.Apply(lifts,i,out float sx,out float sy,out float sz,out float gx,out float gy,out float gz);if(gx==0&&gy==0&&gz==0)continue;
                ref var p=ref w.Items[i];var q=new FactoryQ{X=p.Qx,Y=p.Qy,Z=p.Qz,W=p.Qw};q.Rotate(sx,sy,sz,out double wx,out double wy,out double wz);
                p.X+=(float)wx;p.Y+=(float)wy;p.Z+=(float)wz;p.Hx+=gx;p.Hy+=gy;p.Hz+=gz;p.Rx+=sx;p.Ry+=sy;p.Rz+=sz;
            }
        }

        // ---------------- walkways ----------------
        // Rails along each open edge of a deck: posts at most 1.5 m apart, top rail at 1.07 m, knee rail at .55 m, a toe board,
        // and a dark stringer on the deck's side. An edge sample is open when nothing walkable continues within .3 m of the deck's
        // height just outside it and no wall stands there; a rail saw's swept volume always leaves a gap.
        private static void Deck(Writer w,Bx d,List<Bx> all,List<Bx> rails)
        {
            double top=d.Y1;
            void Edge(bool alongX,double fixedAt,double outward,double from,double to)
            {
                const double step=.25;int n=Math.Max(1,(int)Math.Round((to-from)/step));double run=double.NaN;
                for(int i=0;i<=n;i++)
                {
                    double s=from+(to-from)*Math.Min(1,(i+.5)/n);bool open=i<n;
                    if(open)
                    {
                        double ox0,ox1,oz0,oz1,c=fixedAt+outward*.18;if(alongX){ox0=s-.12;ox1=s+.12;oz0=c-.15;oz1=c+.15;}else{oz0=s-.12;oz1=s+.12;ox0=c-.15;ox1=c+.15;}
                        foreach(var b in all){if(b.Id==d.Id||b.Id=="factory-floor")continue;
                            if(Overlaps(b,ox0,ox1,top-.35,top+.3,oz0,oz1)&&b.Y1<=top+.3){open=false;break;}           // walkable continuation
                            if(Overlaps(b,ox0,ox1,top+.3,top+1.1,oz0,oz1)){open=false;break;}}                         // a wall or column
                        if(open)foreach(var r in rails)if(Overlaps(r,ox0,ox1,top,top+1.2,oz0,oz1)){open=false;break;}
                    }
                    double at=from+(to-from)*i/n;
                    if(open&&double.IsNaN(run))run=at;
                    if((!open||i==n)&&!double.IsNaN(run)){double end=open?to:at;if(end-run>.3)RailRun(w,alongX,fixedAt,outward,run,end,top);run=double.NaN;}
                }
                // the deck's side stringer
                if(alongX)w.Box(from,to,top-.26,top-.02,fixedAt+(outward>0?0:-.04),fixedAt+(outward>0?.04:0),Bracket);else w.Box(fixedAt+(outward>0?0:-.04),fixedAt+(outward>0?.04:0),top-.26,top-.02,from,to,Bracket);
            }
            Edge(true,d.Z0,-1,d.X0,d.X1);Edge(true,d.Z1,1,d.X0,d.X1);Edge(false,d.X0,-1,d.Z0,d.Z1);Edge(false,d.X1,1,d.Z0,d.Z1);
            // under a cantilevered gallery: brackets every 3 m from the wall face out to the deck edge
            if(Starts(d.Id,"factory-gallery-"))
            {
                bool south=d.Z0>0;double edge=south?d.Z0:d.Z1,face=south?d.Z0+1.0:d.Z1-1.0;
                for(double x=d.X0+1.5;x<d.X1-1;x+=3){w.Box(x-.03,x+.03,top-.62,top-.2,Math.Min(face,edge),Math.Max(face,edge),Bracket);w.Box(x-.07,x+.07,top-.24,top-.2,Math.Min(face,edge),Math.Max(face,edge),Bracket,false);}
            }
            if(Starts(d.Id,"factory-catwalk-"))
            {
                bool north=d.Z1>0;double wall=north?d.Z1:d.Z0,edge=north?d.Z0:d.Z1;
                for(double x=d.X0+2;x<d.X1-1;x+=4)w.Bar(x,top-1.45,wall,x,top-.22,edge+(north?.12:-.12),.08,.12,Bracket);
            }
        }
        private static void RailRun(Writer w,bool alongX,double at,double outward,double from,double to,double top)
        {
            double inset=at-outward*.05;int posts=Math.Max(1,(int)Math.Ceiling((to-from)/1.5));
            for(int p=0;p<=posts;p++){double s=from+(to-from)*p/posts;if(alongX)w.Box(s-.025,s+.025,top,top+1.07,inset-.025,inset+.025,Rail,false);else w.Box(inset-.025,inset+.025,top,top+1.07,s-.025,s+.025,Rail,false);}
            foreach(var (y,t) in new[]{(1.07,.05),(.55,.035)}){if(alongX)w.Box(from,to,top+y-t/2,top+y+t/2,inset-.03,inset+.03,Rail,y>1);else w.Box(inset-.03,inset+.03,top+y-t/2,top+y+t/2,from,to,Rail,y>1);}
            if(alongX)w.Box(from,to,top,top+.1,inset-.012,inset+.012,Toe,false);else w.Box(inset-.012,inset+.012,top,top+.1,from,to,Toe,false);
        }
        // A flight of steps: sloped stringers on both sides and a sloped handrail with posts on each open side.
        private static void Flight(Writer w,List<Bx> steps,List<Bx> all)
        {
            if(steps.Count<2)return;steps.Sort((a,b)=>a.Y1.CompareTo(b.Y1));var lo=steps[0];var hi=steps[steps.Count-1];
            bool alongX=Math.Abs((hi.X0+hi.X1)-(lo.X0+lo.X1))>Math.Abs((hi.Z0+hi.Z1)-(lo.Z0+lo.Z1));
            double a0=alongX?(lo.X0+lo.X1)/2:(lo.Z0+lo.Z1)/2,a1=alongX?(hi.X0+hi.X1)/2:(hi.Z0+hi.Z1)/2,y0=lo.Y1,y1=hi.Y1;
            double s0=alongX?lo.Z0:lo.X0,s1=alongX?lo.Z1:lo.X1;
            foreach(var (side,out_) in new[]{(s0,-1.0),(s1,1.0)})
            {
                double c=side+out_*.03;
                if(alongX)w.Bar(a0,y0-.16,c,a1,y1-.16,c,.05,.32,Bracket);else w.Bar(c,y0-.16,a0,c,y1-.16,a1,.05,.32,Bracket);
                // a wall beside the flight takes no rail
                double probe=side+out_*.2;bool wall=false;foreach(var b in all){if(b.Id.StartsWith("factory-stair-",StringComparison.Ordinal))continue;
                    if(alongX?Overlaps(b,Math.Min(a0,a1),Math.Max(a0,a1),y1+.2,y1+1.0,probe-.1,probe+.1):Overlaps(b,probe-.1,probe+.1,y1+.2,y1+1.0,Math.Min(a0,a1),Math.Max(a0,a1))){wall=true;break;}}
                if(wall)continue;double r=side-out_*.05;
                if(alongX){w.Bar(a0,y0+.95,r,a1,y1+.95,r,.05,.05,Rail);for(int k=0;k<=2;k++){double t=k/2.0,x=a0+(a1-a0)*t,y=y0+(y1-y0)*t;w.Box(x-.025,x+.025,y,y+.95,r-.025,r+.025,Rail,false);}}
                else{w.Bar(r,y0+.95,a0,r,y1+.95,a1,.05,.05,Rail);for(int k=0;k<=2;k++){double t=k/2.0,z=a0+(a1-a0)*t,y=y0+(y1-y0)*t;w.Box(r-.025,r+.025,y,y+.95,z-.025,z+.025,Rail,false);}}
            }
            // nosing strips (yellow) on each tread's front edge
            for(int i=0;i<steps.Count;i++){var s=steps[i];double e=alongX?(a1>a0?s.X0:s.X1):(a1>a0?s.Z0:s.Z1);
                if(alongX)w.Box(e-.03,e+.03,s.Y1,s.Y1+.008,s.Z0+.04,s.Z1-.04,Toe,false);else w.Box(s.X0+.04,s.X1-.04,s.Y1,s.Y1+.008,e-.03,e+.03,Toe,false);}
        }

        // ---------------- structure ----------------
        private static void Column(Writer w,Bx c)
        {
            double cx=(c.X0+c.X1)/2,cz=(c.Z0+c.Z1)/2,h=c.Y1;
            w.Box(c.X0-.02,c.X0+.03,0,h,c.Z0-.02,c.Z1+.02,Bracket);w.Box(c.X1-.03,c.X1+.02,0,h,c.Z0-.02,c.Z1+.02,Bracket);        // flanges
            w.Box(c.X0+.08,c.X1-.08,0,h,c.Z0-.012,c.Z0,Black,false);w.Box(c.X0+.08,c.X1-.08,0,h,c.Z1,c.Z1+.012,Black,false);       // shadowed web
            w.Box(cx-.46,cx+.46,0,.05,cz-.46,cz+.46,Plate);for(int k=0;k<4;k++)w.Box(cx+(k%2==0?-.36:.3),cx+(k%2==0?-.3:.36),.05,.1,cz+(k<2?-.36:.3),cz+(k<2?-.3:.36),Black,false);
            w.Box(c.X0-.05,c.X1+.05,0,1.2,c.Z0-.05,c.Z1+.05,Stripe);                                                             // hazard collar
            w.Box(c.X0-.06,c.X1+.06,h-.06,h,c.Z0-.06,c.Z1+.06,Plate);
            // haunch plates under the runway
            w.Bar(cx,h-1.4,cz,cx,h-.06,cz+(cz>0?-.5:.5),.04,.4,Bracket);
        }
        private static void Runway(Writer w,Bx r)
        {
            double cz=(r.Z0+r.Z1)/2;w.Box(r.X0,r.X1,r.Y1,r.Y1+.14,cz-.06,cz+.06,Track);
            for(double x=r.X0+.75;x<r.X1;x+=1.5){w.Box(x-.02,x+.02,r.Y0+.04,r.Y1-.04,r.Z0-.03,r.Z0,Bracket,false);w.Box(x-.02,x+.02,r.Y0+.04,r.Y1-.04,r.Z1,r.Z1+.03,Bracket,false);}
            w.Box(r.X0-.04,r.X1+.04,r.Y0-.04,r.Y0,r.Z0-.04,r.Z1+.04,Bracket);w.Box(r.X0-.04,r.X1+.04,r.Y1-.04,r.Y1,r.Z0-.04,r.Z1+.04,Bracket);
            foreach(double x in new[]{r.X0+.25,r.X1-.25})w.Box(x-.2,x+.2,r.Y1,r.Y1+.55,cz-.3,cz+.3,Stripe);
        }
        // The shell: on its inner face pilasters every 9 m; on the cladding a clerestory band under the roofs and service pipes at
        // 6-7 m along the long walls (above the catwalk heads), roller doors with striped jambs on the dock ends; coping on top.
        private static void Wall(Writer w,Bx b,bool clad)
        {
            bool alongX=b.X1-b.X0>b.Z1-b.Z0;double inner=alongX?(b.Z0>0?b.Z0:b.Z1):(b.X0>0?b.X0:b.X1),s=alongX?(b.Z0>0?-1:1):(b.X0>0?-1:1),len0=alongX?b.X0:b.Z0,len1=alongX?b.X1:b.Z1;
            void Face(double u0,double u1,double y0,double y1,double d0,double d1,int kind,bool shadow=true,int color=-1,double window=1)
            {double a=inner+s*d0,c=inner+s*d1;if(alongX)w.Box(u0,u1,y0,y1,Math.Min(a,c),Math.Max(a,c),kind,shadow,color,window);else w.Box(Math.Min(a,c),Math.Max(a,c),y0,y1,u0,u1,kind,shadow,color,window);}
            if(!clad)
            {
                for(double u=Math.Ceiling(len0/9)*9;u<=len1-1;u+=9){if(Math.Abs(u)>37.5)continue;Face(u-.35,u+.35,0,b.Y1+(alongX?7.1:7.1),-.01,.22,Concrete);}
                Face(len0,len1,b.Y1-.12,b.Y1,-.01,.06,Concrete);                                                                    // plinth cap
                if(!alongX)foreach(double z0 in new[]{-7.0,2.5}){double z1=z0+4.5;Face(z0,z1,0,4.3,-.01,.05,Door,true,-1);for(double y=.3;y<4.3;y+=.3)Face(z0,z1,y-.02,y,.05,.065,Black,false);
                    Face(z0-.2,z0,0,4.4,-.01,.14,Stripe);Face(z1,z1+.2,0,4.4,-.01,.14,Stripe);Face(z0-.1,z1+.1,4.3,4.75,-.01,.35,Bracket);}
                return;
            }
            Face(len0,len1,b.Y1-.18,b.Y1+.06,-.04,b.Z1-b.Z0>3||b.X1-b.X0>3?0:.05,Bracket);                                            // coping
            for(double u=Math.Ceiling((len0+1)/9)*9-4.5;u<len1-2;u+=9)
            {
                double u0=u-3.6,u1=u+3.6;if(!alongX&&Math.Abs(u)<10.4)continue;if(Math.Abs(u0)>37.6||Math.Abs(u1)>37.6)continue;
                Face(u0,u1,8.3,10.1,-.01,.04,Window,false,-1,.12);Face(u0-.08,u1+.08,8.22,8.3,-.01,.12,Frame,false);Face(u0-.08,u1+.08,10.1,10.18,-.01,.08,Frame,false);
                for(double m=u0+1.2;m<u1-.1;m+=1.2)Face(m-.03,m+.03,8.3,10.1,.04,.07,Frame,false);
            }
            if(alongX)
            {
                foreach(var (y,r,kind) in new[]{(7.25,.15,PipeSteam),(7.68,.1,PipeWater),(7.98,.07,PipeGas)})
                {
                    Face(-37.4,37.4,y-r,y+r,.06,.06+2*r,kind,r>.1);for(double u=-36;u<=36;u+=6)Face(u-.06,u+.06,y-r-.03,y+r+.03,.04,.1+2*r,Flange,false);
                    for(double u=-34.5;u<=36;u+=3)Face(u-.04,u+.04,y-r-.12,y-r,0,.12+2*r,Bracket,false);
                }
                foreach(double u in new[]{-34.0,33.0}){Face(u-.15,u+.15,1.0,7.1,.06,.36,PipeSteam);Face(u-.22,u+.22,1.25,1.55,.0,.46,Valve,false);Face(u-.05,u+.05,1.0,1.8,.4,.47,Valve,false);}
            }
        }
        private static void BayWall(Writer w,Bx b,bool lintel)
        {
            if(lintel){foreach(double z in new[]{b.Z0,b.Z1}){double o=z==b.Z0?-1:1;w.Box(b.X0,b.X1,b.Y0+.06,b.Y0+.32,Math.Min(z,z+o*.025),Math.Max(z,z+o*.025),Stripe,false);}return;}
            // jamb guards at each end that faces an opening (not the shell), on both faces; a kick strip along the foot
            foreach(double x in new[]{b.X0,b.X1})
            {
                if(Math.Abs(x)>37.9)continue;double s=x==b.X0?1:-1;
                foreach(double z in new[]{b.Z0,b.Z1}){double o=z==b.Z0?-1:1;w.Box(Math.Min(x,x+s*.2),Math.Max(x,x+s*.2),0,2.8,Math.Min(z,z+o*.03),Math.Max(z,z+o*.03),Stripe,false);}
                w.Box(Math.Min(x,x-s*.03),Math.Max(x,x-s*.03),0,2.8,b.Z0,b.Z1,Stripe,false);
            }
            foreach(double z in new[]{b.Z0,b.Z1}){double o=z==b.Z0?-1:1;w.Box(b.X0,b.X1,0,.22,Math.Min(z,z+o*.012),Math.Max(z,z+o*.012),Black,false);}
            // signs: a plate every 12 m on the bay face
            double bay=b.Z0>0?b.Z0:b.Z1,so=b.Z0>0?-1:1;for(double x=Math.Ceiling(b.X0/12)*12+6;x<b.X1-1;x+=12)if(Math.Abs(x)<26)w.Box(x-.6,x+.6,1.9,2.5,Math.Min(bay,bay+so*.02),Math.Max(bay,bay+so*.02),Sign,false);
        }
        private static void PressHead(Writer w,Bx b)
        {
            double cx=(b.X0+b.X1)/2;
            foreach(double z in new[]{b.Z0,b.Z1})
            {
                double o=z==b.Z0?-1:1,f=z+o*.18;
                // hydraulic cylinders on the wall beside the mouth (never in it), capped where the gallery bracket meets them
                foreach(double dx in new[]{-(b.X1-b.X0)/2-.35,(b.X1-b.X0)/2+.35}){w.Box(cx+dx-.15,cx+dx+.15,1.6,3.3,Math.Min(z,f),Math.Max(z,f),Ram);w.Box(cx+dx-.18,cx+dx+.18,3.3,3.4,Math.Min(z,f+o*.02),Math.Max(z,f+o*.02),CraneDark);w.Box(cx+dx-.18,cx+dx+.18,1.5,1.6,Math.Min(z,f+o*.02),Math.Max(z,f+o*.02),CraneDark);}
                w.Box(b.X0,b.X1,b.Y0,b.Y1,Math.Min(z,z+o*.03),Math.Max(z,z+o*.03),Stripe,false);
                // light-curtain posts against the jambs, red emitters facing across the mouth
                foreach(double x in new[]{b.X0+.04,b.X1-.04}){w.Box(x-.04,x+.04,0,1.9,Math.Min(z,z-o*.08),Math.Max(z,z-o*.08),Toe,false);for(double y=.3;y<1.85;y+=.3)w.Box(x-.045,x+.045,y-.015,y+.015,Math.Min(z-o*.03,z-o*.05),Math.Max(z-o*.03,z-o*.05),Red,false);}
            }
            foreach(double x in new[]{b.X0,b.X1})w.Box(x-.03,x+.03,0,3.0,b.Z0,b.Z1,Stripe,false);
        }

        // ---------------- machines and stock ----------------
        private static void Lathe(Writer w,Bx b,bool head)
        {
            double cz=(b.Z0+b.Z1)/2,fz=cz<0?b.Z1:b.Z0,fo=cz<0?1:-1;                                                                   // the operator's side faces the bay wall
            if(head){w.Box(b.X1,b.X1+.12,b.Y0+.08,b.Y1-.12,cz-.32,cz+.32,Chrome);w.Box(b.X0+.2,b.X1-.2,b.Y1-.02,b.Y1+.02,b.Z0+.1,b.Z1-.1,Black,false);
                w.Box(b.X0+.3,b.X0+.9,b.Y0+.15,b.Y1-.15,Math.Min(fz,fz+fo*.05),Math.Max(fz,fz+fo*.05),Panel);for(int k=0;k<3;k++){double x=b.X0+.42+k*.2;w.Box(x-.04,x+.04,b.Y1-.32,b.Y1-.24,Math.Min(fz,fz+fo*.07),Math.Max(fz,fz+fo*.07),Lamp,false,k==0?0x37D95A:k==1?0xFFB02E:0xE0402E);}return;}
            w.Box(b.X0+.1,b.X1-.1,b.Y1,b.Y1+.05,cz-.42,cz-.28,Chrome);w.Box(b.X0+.1,b.X1-.1,b.Y1,b.Y1+.05,cz+.28,cz+.42,Chrome);       // ways
            double cx=(b.X0+b.X1)/2+.4;w.Box(cx-.35,cx+.35,b.Y1,b.Y1+.22,b.Z0+.05,b.Z1-.05,Machine);w.Box(cx-.12,cx+.12,b.Y1+.22,b.Y1+.24,cz-.12,cz+.12,Black);
            w.Box(b.X1-.75,b.X1-.15,b.Y1,b.Y1+.24,cz-.3,cz+.3,Machine);                                                                // tailstock
            w.Box(b.X0+.2,b.X1-.2,b.Y1-.32,b.Y1-.28,Math.Min(fz,fz+fo*.06),Math.Max(fz,fz+fo*.06),Chrome,false);                       // leadscrew
            // The kick plate, and the hazard collar along both sides of the bed's top edge (two strips, not a slab over the top, so
            // the bed's top stays machine green).
            w.Box(b.X0,b.X1,0,.16,Math.Min(fz,fz+fo*.04),Math.Max(fz,fz+fo*.04),Black,false);w.Box(b.X0,b.X1,b.Y1-.06,b.Y1,b.Z0-.03,b.Z0,Stripe,false);w.Box(b.X0,b.X1,b.Y1-.06,b.Y1,b.Z1,b.Z1+.03,Stripe,false);
        }
        private static void Mill(Writer w,Bx b)
        {
            double cx=(b.X0+b.X1)/2,cz=(b.Z0+b.Z1)/2,fz=cz>0?b.Z0:b.Z1,fo=cz>0?-1:1;
            w.Box(b.X0+.2,b.X1-.2,.95,1.08,Math.Min(fz,fz+fo*.22),Math.Max(fz,fz+fo*.22),Chrome);                                       // table
            w.Box(cx-.5,cx+.5,1.5,2.2,Math.Min(fz,fz+fo*.12),Math.Max(fz,fz+fo*.12),Panel);w.Box(cx-.3,cx+.3,1.75,2.05,Math.Min(fz,fz+fo*.14),Math.Max(fz,fz+fo*.14),Lamp,false,0x2E7A4A);
            foreach(double dx in new[]{-.8,.8})w.Add(cx+dx,.7,fz+fo*.1,.12,.12,.02,FactoryQ.Axis(0,0,1,.4),Black,false);
            w.Box(cx-.25,cx+.25,b.Y1,b.Y1+.18,cz-.25,cz+.25,Machine);w.Box(cx-.08,cx+.08,b.Y1+.18,b.Y1+.24,cz-.08,cz+.08,Lamp,false,0xFFB02E);
            w.Box(b.X0-.02,b.X1+.02,0,.12,b.Z0-.02,b.Z1+.02,Black,false);
        }
        private static void Cabinet(Writer w,Bx b)
        {
            double fz=b.Z0>0?b.Z0:b.Z1,fo=b.Z0>0?-1:1;
            for(double x=b.X0+.75;x<b.X1-.1;x+=.75)w.Box(x-.012,x+.012,.08,b.Y1-.08,Math.Min(fz,fz+fo*.012),Math.Max(fz,fz+fo*.012),Black,false);
            for(double x=b.X0+.6;x<b.X1;x+=.75){w.Box(x-.02,x+.02,1.0,1.3,Math.Min(fz,fz+fo*.04),Math.Max(fz,fz+fo*.04),Chrome,false);w.Box(x-.18,x-.1,1.75,1.83,Math.Min(fz,fz+fo*.03),Math.Max(fz,fz+fo*.03),Lamp,false,x<b.X0+1.5?0xE0402E:0x37D95A);}
            w.Box(b.X0+.2,b.X1-.2,b.Y1,b.Y1+.2,b.Z0+.15,b.Z1-.15,Bracket);for(double x=b.X0+.5;x<b.X1;x+=1.0)w.Box(x-.05,x+.05,b.Y1+.2,9.4,b.Z0+.4,b.Z0+.5,Bracket,false);
            w.Box(b.X0+.3,b.X0+.7,1.9,2.1,Math.Min(fz,fz+fo*.015),Math.Max(fz,fz+fo*.015),Danger,false);
        }
        private static void Rack(Writer w,Bx b)
        {
            // uprights at the ends and every 2 m, arms carrying the bar loads; the bar ends show on the rack's end faces
            for(double x=b.X0+.15;x<=b.X1-.1;x+=Math.Max(1.4,(b.X1-b.X0-.3)/Math.Max(1,Math.Round((b.X1-b.X0)/2)))){w.Box(x-.12,x+.12,0,b.Y1+.1,b.Z0-.06,b.Z1+.06,Bracket);
                foreach(double y in new[]{.75,1.5,2.25})w.Box(x-.1,x+.1,y-.06,y,b.Z0-.12,b.Z1+.12,Bracket);}
            foreach(double y in new[]{.75,1.5,2.25})foreach(double x in new[]{b.X0,b.X1})for(int k=0;k<6;k++){double z=b.Z0+.12+k*(b.Z1-b.Z0-.24)/5,s=x==b.X0?-1:1;w.Box(Math.Min(x,x+s*.05),Math.Max(x,x+s*.05),y+.02,y+.14,z-.06,z+.06,Bar,false);}
            w.Box(b.X0,b.X1,0,.1,b.Z0-.08,b.Z1+.08,Black,false);
        }
        private static void Coils(Writer w,Bx b)
        {
            double cy=(b.Y0+b.Y1)/2,r=(b.Y1-b.Y0)/2;foreach(double x in new[]{b.X0,b.X1}){double s=x==b.X0?-1:1;
                foreach(double zc in new[]{b.Z0+(b.Z1-b.Z0)*.27,b.Z0+(b.Z1-b.Z0)*.73})for(int k=0;k<4;k++)w.Add(x+s*.02,cy,zc,.02,r*.86,r*.36,FactoryQ.Axis(1,0,0,k*Math.PI/4),Coil,false);
                w.Box(Math.Min(x,x+s*.03),Math.Max(x,x+s*.03),cy-.12,cy+.12,b.Z0+.4,b.Z1-.4,Black,false);}
            w.Box(b.X0+.2,b.X1-.2,b.Y1-.01,b.Y1+.02,b.Z0+.3,b.Z1-.3,Strap,false);w.Box(b.X0-.05,b.X1+.05,0,.18,b.Z0-.05,b.Z1+.05,Timber);
        }
        private static void Crib(Writer w,Bx b)
        {
            for(double x=b.X0;x<=b.X1+1e-6;x+=Math.Max(1,(b.X1-b.X0)/Math.Round(Math.Max(1,(b.X1-b.X0)/1.6))))w.Box(x-.05,x+.05,0,b.Y1+.05,b.Z0-.05,b.Z1+.05,Bracket);
            for(double z=b.Z0;z<=b.Z1+1e-6;z+=Math.Max(1,(b.Z1-b.Z0)/Math.Round(Math.Max(1,(b.Z1-b.Z0)/1.6))))w.Box(b.X0-.05,b.X1+.05,0,b.Y1+.05,z-.05,z+.05,Bracket);
            w.Box(b.X0-.06,b.X1+.06,b.Y1-.06,b.Y1+.06,b.Z0-.06,b.Z1+.06,Bracket);w.Box(b.X0-.04,b.X1+.04,0,.15,b.Z0-.04,b.Z1+.04,Bracket);
            double fx=b.X0<0?b.X1:b.X0,fo=b.X0<0?1:-1;w.Box(Math.Min(fx,fx+fo*.02),Math.Max(fx,fx+fo*.02),1.6,2.0,(b.Z0+b.Z1)/2-.4,(b.Z0+b.Z1)/2+.4,Sign,false);
        }
        private static void Shelf(Writer w,Bx b)
        {
            foreach(double z in new[]{b.Z0,b.Z1}){double o=z==b.Z0?-1:1;
                for(double y=.05;y<b.Y1;y+=.85)w.Box(b.X0,b.X1,y,y+.1,Math.Min(z,z+o*.06),Math.Max(z,z+o*.06),Bracket);
                for(double x=b.X0+.05;x<b.X1;x+=.55){uint h=unchecked((uint)(x*100)*2654435761u^(uint)(z*77));for(int k=0;k<3;k++){double y=.15+k*.85,sz=.25+((h>>(k*4))&7)*.03;if(((h>>(k*3+12))&3)==0)continue;
                    w.Box(x+.03,Math.Min(b.X1,x+.5),y,y+sz,Math.Min(z,z+o*.08),Math.Max(z,z+o*.08),((h>>k)&1)==0?Carton:Bin,false);}}}
            foreach(double x in new[]{b.X0,b.X1})w.Box(x-.05,x+.05,0,b.Y1,b.Z0-.07,b.Z1+.07,Bracket);
        }
        private static void Screen(Writer w,Bx b)
        {
            w.Box(b.X0-.05,b.X1+.05,b.Y1,b.Y1+.08,b.Z0-.05,b.Z1+.05,Concrete);foreach(double x in new[]{b.X0,b.X1}){double o=x==b.X0?-1:1;w.Box(Math.Min(x,x+o*.015),Math.Max(x,x+o*.015),.9,1.05,b.Z0,b.Z1,PaintWalk,false);
                double zc=(b.Z0+b.Z1)/2;w.Box(Math.Min(x,x+o*.02),Math.Max(x,x+o*.02),1.6,2.3,zc-.5,zc+.5,Danger,false);w.Box(Math.Min(x,x+o*.12),Math.Max(x,x+o*.12),.9,1.4,zc+1.2,zc+1.5,Red);}
        }
        private static void Bins(Writer w,Bx b){for(double y=.3;y<b.Y1;y+=.3)w.Box(b.X0-.03,b.X1+.03,y-.04,y,b.Z0-.03,b.Z1+.03,Bin,false);}
        private static void Office(Writer w,Bx b)
        {
            // windows on the faces that look into the dock, a door, flashing and a roof unit
            double fx=b.X0<0?b.X1:b.X0,fo=b.X0<0?1:-1,fz=b.Z0<0?b.Z1:b.Z0,zo=b.Z0<0?1:-1;
            w.Box(Math.Min(fx,fx+fo*.03),Math.Max(fx,fx+fo*.03),1.0,2.2,b.Z0+.6,b.Z1-1.6,Window,false,-1,1);w.Box(Math.Min(fx,fx+fo*.05),Math.Max(fx,fx+fo*.05),.95,1.0,b.Z0+.5,b.Z1-1.5,Frame,false);
            w.Box(Math.Min(fx,fx+fo*.03),Math.Max(fx,fx+fo*.03),0,2.1,b.Z1-1.3,b.Z1-.4,Door,false,0x3C4E5C);
            w.Box(b.X0+.5,b.X1-.5,1.0,2.2,Math.Min(fz,fz+zo*.03),Math.Max(fz,fz+zo*.03),Window,false,-1,1);
            w.Box(b.X0-.05,b.X1+.05,b.Y1-.12,b.Y1+.05,b.Z0-.05,b.Z1+.05,Bracket);w.Box(b.X0+.6,b.X0+1.6,b.Y1+.05,b.Y1+.75,b.Z0+.6,b.Z0+1.6,Panel);
            w.Box(Math.Min(fx,fx+fo*.02),Math.Max(fx,fx+fo*.02),2.45,2.8,b.Z0+1.0,b.Z1-1.0,Sign,false);
        }
        private static void Container(Writer w,Bx b)
        {
            foreach(double x in new[]{b.X0,b.X1})foreach(double y in new[]{b.Y0,b.Y1-.16})foreach(double z in new[]{b.Z0,b.Z1-.16})w.Box(x-(x==b.X0?.02:.16),x+(x==b.X0?.16:.02),y,y+.16,z-.02,z+.18,Black,false);
            foreach(double z in new[]{b.Z0,b.Z1}){double o=z==b.Z0?-1:1;for(int k=0;k<4;k++){double x=b.X0+.35+k*(b.X1-b.X0-.7)/3;w.Box(x-.025,x+.025,.15,b.Y1-.15,Math.Min(z,z+o*.06),Math.Max(z,z+o*.06),Chrome,false);}}
            w.Box(b.X0-.01,b.X1+.01,b.Y1-.5,b.Y1-.35,b.Z0-.01,b.Z1+.01,Sign,false);
        }
        private static void Forklift(Writer w,Bx b)
        {
            double cx=(b.X0+b.X1)/2,fz=b.Z1;                                                                                            // forks toward +z
            foreach(double dx in new[]{-.45,.45}){w.Box(cx+dx-.05,cx+dx+.05,0,b.Y1+.4,fz,fz+.12,Black);w.Box(cx+dx-.08,cx+dx+.08,.02,.08,fz+.12,fz+.25,Black,false);}
            foreach(double x in new[]{b.X0,b.X1})foreach(double z in new[]{b.Z0+.4,b.Z1-.45}){double s=x==b.X0?-1:1;w.Add(x+s*.06,.32,z,.06,.3,.3,FactoryQ.Axis(1,0,0,Math.PI/4),Wheel,false);}
            w.Box(b.X0+.05,b.X1-.05,b.Y1,b.Y1+.06,b.Z0+.3,b.Z1-.2,Black);w.Box(b.X0-.02,b.X1+.02,0,.9,b.Z0-.02,b.Z0+.4,CraneDark);
            w.Box(cx-.06,cx+.06,b.Y1+.06,b.Y1+.18,(b.Z0+b.Z1)/2-.06,(b.Z0+b.Z1)/2+.06,Lamp,false,0xFFB02E);
        }
        private static void PulpitRoof(Writer w,Bx r)
        {
            // corner posts, glazing round the cab between the parapet top (7.0) and the roof, the beacon mast on the roof
            foreach(double x in new[]{r.X0+.25,r.X1-.25})foreach(double z in new[]{r.Z0+.25,r.Z1-.25})w.Box(x-.07,x+.07,6.0,r.Y0,z-.07,z+.07,Bracket);
            // an open cab (glass would be opaque here and blind the perch): mullion posts and a head rail on the bay face
            double bayX=r.X0<0?r.X1-.25:r.X0+.25;for(double z=r.Z0+1.3;z<r.Z1-1;z+=1.05)w.Box(bayX-.035,bayX+.035,7.0,r.Y0,z-.035,z+.035,Bracket);w.Box(bayX-.05,bayX+.05,r.Y0-.25,r.Y0,r.Z0+.25,r.Z1-.25,Bracket);
            w.Box(r.X0-.05,r.X1+.05,r.Y1,r.Y1+.1,r.Z0-.05,r.Z1+.05,Bracket);double cx=(r.X0+r.X1)/2,cz=(r.Z0+r.Z1)/2;w.Box(cx-.05,cx+.05,r.Y1+.1,r.Y1+.7,cz-.05,cz+.05,Bracket);
        }
        private static void LadleShell(Writer w,Bx b)
        {
            double cx=(b.X0+b.X1)/2,cz=(b.Z0+b.Z1)/2;foreach(double y in new[]{b.Y0+.5,b.Y0+1.6,b.Y1-.35})w.Box(b.X0-.05,b.X1+.05,y-.08,y+.08,b.Z0-.05,b.Z1+.05,LadleRim);
            w.Box(b.X0-.06,b.X1+.06,b.Y1-.1,b.Y1,b.Z0-.06,b.Z1+.06,LadleRim);foreach(double z in new[]{b.Z0,b.Z1}){double o=z==b.Z0?-1:1;w.Box(cx-.3,cx+.3,b.Y1-1.0,b.Y1-.4,Math.Min(z,z+o*.24),Math.Max(z,z+o*.24),Ladle);}
            w.Box(b.X1,b.X1+.24,b.Y1-.3,b.Y1-.1,cz-.4,cz+.4,LadleRim);                                                                // pouring lip
        }
        private static void LadleCar(Writer w,Bx b)
        {
            foreach(double x in new[]{b.X0+.5,b.X1-.5})foreach(double z in new[]{b.Z0,b.Z1}){double o=z==b.Z0?-1:1;w.Add(x,.32,z+o*.06,.3,.3,.06,FactoryQ.Axis(0,0,1,Math.PI/4),Wheel,false);}
            w.Box(b.X0-.04,b.X1+.04,b.Y1-.08,b.Y1,b.Z0-.04,b.Z1+.04,Stripe,false);
        }
        // Bar ends: a grid of squares on the end faces.
        private static void Ends(Writer w,Bx b,double cell,int kind,int alt)
        {
            foreach(double x in new[]{b.X0,b.X1}){double s=x==b.X0?-1:1;for(double y=b.Y0+cell/2;y<b.Y1;y+=cell)for(double z=b.Z0+cell/2;z<b.Z1;z+=cell){w.Box(Math.Min(x,x+s*.015),Math.Max(x,x+s*.015),y-cell*.42,y+cell*.42,z-cell*.42,z+cell*.42,((int)(y*31+z*17))%3==0?alt:kind,false);}}
            foreach(double x in new[]{b.X0+.4,b.X1-.4})w.Box(x-.08,x+.08,0,.1,b.Z0-.06,b.Z1+.06,Timber);
        }
        private static void Beams(Writer w,Bx b)
        {
            int n=Math.Max(1,(int)Math.Round((b.Z1-b.Z0)/.4));double c=(b.Z1-b.Z0)/n;
            foreach(double x in new[]{b.X0,b.X1}){double s=x==b.X0?-1:1;for(int k=0;k<n;k++){double z=b.Z0+c*(k+.5);for(double y=b.Y0;y<b.Y1-.05;y+=.3){
                w.Box(Math.Min(x,x+s*.012),Math.Max(x,x+s*.012),y+.02,y+.06,z-c*.42,z+c*.42,Load,false);w.Box(Math.Min(x,x+s*.012),Math.Max(x,x+s*.012),y+.24,y+.28,z-c*.42,z+c*.42,Load,false);w.Box(Math.Min(x,x+s*.012),Math.Max(x,x+s*.012),y+.06,y+.24,z-.02,z+.02,Load,false);}}}
            foreach(double x in new[]{b.X0+.5,b.X1-.5})w.Box(x-.04,x+.04,b.Y0,b.Y1+.01,b.Z0-.02,b.Z1+.02,Strap,false);
        }
        private static void Courses(Writer w,Bx b,double course)
        {
            for(double y=b.Y0+course;y<b.Y1-.01;y+=course){w.Box(b.X0-.012,b.X1+.012,y-.012,y+.012,b.Z0-.012,b.Z1+.012,Joint,false);}
            foreach(double x in new[]{b.X0+.3,b.X1-.3})w.Box(x-.07,x+.07,0,.08,b.Z0-.05,b.Z1+.05,Timber);
        }
        private static void Hammer(Writer w,Bx b)
        {
            double cx=(b.X0+b.X1)/2,cz=(b.Z0+b.Z1)/2;
            foreach(double x in new[]{b.X0,b.X1}){double s=x==b.X0?-1:1;w.Box(Math.Min(x,x+s*.22),Math.Max(x,x+s*.22),0,b.Y1,cz-.5,cz+.5,Machine);w.Box(Math.Min(x,x+s*.24),Math.Max(x,x+s*.24),1.0,1.3,cz-.55,cz+.55,Black,false);}
            w.Box(cx-.7,cx+.7,b.Y1,b.Y1+1.6,cz-.7,cz+.7,Machine);w.Box(cx-.8,cx+.8,b.Y1+1.6,b.Y1+1.75,cz-.8,cz+.8,Black);w.Bar(cx+.7,b.Y1+1.2,cz,cx+3.0,b.Y1+1.2,cz,.14,.14,PipeSteam);
            w.Box(b.X0-.2,b.X1+.2,0,.25,b.Z0-.2,b.Z1+.2,CraneDark);foreach(double z in new[]{b.Z0,b.Z1}){double o=z==b.Z0?-1:1;w.Box(cx-.6,cx+.6,2.0,2.5,Math.Min(z,z+o*.02),Math.Max(z,z+o*.02),Sign,false);}
        }
        // Floor paint: aisle lines, expansion joints, drains (the crane zones and hazard borders come from the hazards).
        private static void Floor(Writer w,Bx f)
        {
            const double t=.006;
            foreach(double s in new[]{1.0,-1.0})
            {
                foreach(double z in new[]{11.75,16.9,22.1,26.9})w.Box(-26.6,26.6,0,t,s*z-.06,s*z+.06,PaintWalk,false);
                w.Box(-26.6,26.6,0,t,s*7.75-.06,s*7.75+.06,PaintWalk,false);
                foreach(double x in new[]{-26.6,26.6}){w.Box(x-.06,x+.06,0,t,s>0?11.75:-26.9,s>0?26.9:-11.75,PaintWalk,false);}
                foreach(double z in new[]{-7.75,7.75})w.Box(s*27.0-.06,s*27.0+.06,0,t,-7.75,7.75,PaintWalk,false);
            }
            for(double x=-36;x<=36;x+=6)w.Box(x-.015,x+.015,0,t*.5,-29.9,29.9,Joint,false);for(double z=-30;z<=30;z+=6)w.Box(-37.9,37.9,0,t*.5,z-.015,z+.015,Joint,false);
            foreach(var (x,z) in new[]{(-33.0,-8.3),(33.0,8.3),(-36.5,26.0),(36.5,-26.0),(0.0,8.4),(0.0,-8.4)})w.Box(x-.3,x+.3,0,.01,z-.3,z+.3,Grate,false);
        }
        // The hall roofs: trusses every 6 m and purlins under the plates; glazed lanterns over each skylight slot (no shadow, so
        // the sun reaches the floor through them); vents on the roof top for the menu's view.
        private static void Roofs(Writer w,List<Bx> all)
        {
            foreach(int side in new[]{1,-1})
            {
                double x0=1e9,x1=-1e9,z0=1e9,z1=-1e9,y=0;var plates=new List<Bx>();
                foreach(var b in all)if(Starts(b.Id,"factory-roof-")&&(b.Z0+b.Z1)*side>0){plates.Add(b);x0=Math.Min(x0,b.X0);x1=Math.Max(x1,b.X1);z0=Math.Min(z0,b.Z0);z1=Math.Max(z1,b.Z1);y=b.Y0;}
                if(plates.Count==0)continue;plates.Sort((a,b)=>a.Z0.CompareTo(b.Z0));double top=plates[0].Y1;
                double inner=side>0?z0+.25:z1-.25,outer=side>0?z1-.25:z0+.25,bottom=y-1.7;
                for(double x=x0+2;x<=x1-1.9;x+=6)
                {
                    w.Bar(x,bottom,inner,x,bottom,outer,.16,.18,Truss);w.Bar(x,y-.1,inner,x,y-.1,outer,.16,.18,Truss);int n=8;
                    for(int k=0;k<=n;k++){double z=inner+(outer-inner)*k/n;w.Bar(x,bottom,z,x,y-.1,z,.1,.1,Truss);if(k<n){double zn=inner+(outer-inner)*(k+1)/n;w.Bar(x,k%2==0?bottom:y-.1,z,x,k%2==0?y-.1:bottom,zn,.08,.08,Truss,false);}}
                }
                foreach(var p in plates)for(double z=p.Z0+.4;z<p.Z1-.2;z+=1.4)w.Bar(x0,y-.16,z,x1,y-.16,z,.1,.16,Truss,false);
                for(int i=0;i+1<plates.Count;i++)
                {
                    double a=plates[i].Z1,c=plates[i+1].Z0;if(c-a<.5)continue;
                    w.Box(x0,x1,top,top+.95,a,a+.04,Glass,false,-1,0);w.Box(x0,x1,top,top+.95,c-.04,c,Glass,false,-1,0);w.Box(x0,x1,top+.95,top+1.0,a,c,Glass,false,-1,0);
                    for(double x=x0;x<=x1;x+=3){w.Box(x-.04,x+.04,top,top+1.04,a-.04,a+.04,Frame,false);w.Box(x-.04,x+.04,top,top+1.04,c-.04,c+.04,Frame,false);w.Box(x-.04,x+.04,top+.96,top+1.04,a,c,Frame,false);}
                    w.Box(x0,x1,top+.96,top+1.06,a-.05,a+.05,Frame,false);w.Box(x0,x1,top+.96,top+1.06,c-.05,c+.05,Frame,false);
                }
                for(double x=x0+6;x<x1-3;x+=12){double zc=(z0+z1)/2+side*3;w.Box(x-.6,x+.6,top,top+.9,zc-.6,zc+.6,RoofTop,false);w.Box(x-.75,x+.75,top+.9,top+1.0,zc-.75,zc+.75,Bracket,false);}
            }
        }

        // ---------------- hazard fittings (static) ----------------
        private static void Fittings(Writer w,FactoryHazard h,List<Bx> all)
        {
            if(h==null)return;
            switch(h.kind)
            {
                case NativeFactoryHazards.SawSlot:
                {
                    bool alongX=Math.Abs(h.b.x-h.a.x)>Math.Abs(h.b.z-h.a.z);double s0=Math.Min(alongX?h.a.x:h.a.z,alongX?h.b.x:h.b.z)-h.radius-.2,s1=Math.Max(alongX?h.a.x:h.a.z,alongX?h.b.x:h.b.z)+h.radius+.2,c=alongX?h.a.z:h.a.x;
                    void Strip(double a0,double a1,double c0,double c1,double y0,double y1,int kind,int color=-1){if(alongX)w.Box(a0,a1,y0,y1,c-c1,c-c0,kind,false,color);else w.Box(c-c1,c-c0,y0,y1,a0,a1,kind,false,color);}
                    Strip(s0,s1,-.12,.12,-.03,.004,Void);Strip(s0,s1,.12,.34,0,.012,Plate);Strip(s0,s1,-.34,-.12,0,.012,Plate);
                    Strip(s0,s1,.64,.72,0,.007,Danger);Strip(s0,s1,-.72,-.64,0,.007,Danger);
                    foreach(double e in new[]{s0-1.2,s1}){Strip(e,e+1.2,-.72,.72,0,.014,Stripe);}
                    break;
                }
                case NativeFactoryHazards.SawRail:
                {
                    double x=h.a.x,zw=h.a.z,ze=h.b.z,dir=Math.Sign(ze-zw),y=h.a.y+h.radius+.45,face=dir<0?30.0:-30.0;
                    foreach(var b in all)if(Starts(b.Id,"factory-wall-")&&b.X0<x&&b.X1>x&&b.Z0<zw&&b.Z1>zw){face=dir<0?b.Z0:b.Z1;break;}
                    w.Box(x-.11,x+.11,y-.15,y+.15,Math.Min(face,ze-dir*.6),Math.Max(face,ze-dir*.6),CraneDark);w.Box(x-.14,x+.14,y+.12,y+.17,Math.Min(face,ze-dir*.6),Math.Max(face,ze-dir*.6),Bracket);
                    w.Bar(x,y+.9,face,x,y+.15,ze-dir*.9,.1,.12,Bracket);
                    double f=face+dir*.01;w.Box(x-.17,x+.17,h.a.y-h.radius-.1,h.a.y+h.radius+.1,Math.Min(face,f),Math.Max(face,f),Void,false);
                    foreach(double dx in new[]{-.25,.25})w.Box(x+dx-.07,x+dx+.07,h.a.y-h.radius-.2,h.a.y+h.radius+.2,Math.Min(face,face+dir*.03),Math.Max(face,face+dir*.03),Stripe,false);
                    w.Box(x-.32,x+.32,h.a.y+h.radius+.12,h.a.y+h.radius+.24,Math.Min(face,face+dir*.03),Math.Max(face,face+dir*.03),Stripe,false);
                    break;
                }
                case NativeFactoryHazards.Vent:
                {
                    double hx=h.size.x/2-.1,hz=h.size.z/2-.1;w.Box(h.a.x-hx,h.a.x+hx,0,.02,h.a.z-hz,h.a.z+hz,Grate,false);
                    foreach(double s in new[]{-1.0,1.0}){w.Box(h.a.x-hx-.18,h.a.x+hx+.18,0,.012,h.a.z+s*hz,h.a.z+s*(hz+.18),Stripe,false);w.Box(h.a.x+s*hx,h.a.x+s*(hx+.18),0,.012,h.a.z-hz,h.a.z+hz,Stripe,false);}
                    break;
                }
                case NativeFactoryHazards.Press:
                {
                    double hx=h.size.x/2+.25,hz=h.size.z/2+.6;foreach(double s in new[]{-1.0,1.0})w.Box(h.a.x-hx,h.a.x+hx,0,.008,h.a.z+s*hz-.15,h.a.z+s*hz+.15,Stripe,false);
                    w.Box(h.a.x-.04,h.a.x+.04,3.6,4.75,h.a.z-1.7*Math.Sign(h.a.z)-.04,h.a.z-1.7*Math.Sign(h.a.z)+.04,Rail);
                    break;
                }
                case NativeFactoryHazards.Crane:
                {
                    // painted drop zones: a hatched frame, a dark wash and the zone's number in 25 cm cells
                    for(int k=0;k<(h.zones?.Length??0);k++)
                    {
                        var z=h.zones[k];double cx=z.center.x,cz=z.center.z,hx=h.size.x/2+.5,hz=h.size.z/2+.5;
                        w.Box(cx-hx,cx+hx,0,.004,cz-hz,cz+hz,PaintZone,false,0x2A2724);
                        foreach(double s in new[]{-1.0,1.0}){w.Box(cx-hx,cx+hx,0,.009,cz+s*hz-(s>0?.35:0),cz+s*hz+(s<0?.35:0),Stripe,false);w.Box(cx+s*hx-(s>0?.35:0),cx+s*hx+(s<0?.35:0),0,.009,cz-hz,cz+hz,Stripe,false);}
                        Digit(w,k+1,cx,cz,.24);
                    }
                    break;
                }
            }
        }
        private static readonly int[] Glyphs={0x7B6F,0x2C97,0x73E7,0x73CF,0x5BC9,0x79CF,0x79EF,0x7249,0x7BEF,0x7BCF};
        /// <summary>A 3×5 digit painted on the floor at (cx, cz), cells `cell` m, read from the bay's long axis.</summary>
        private static void Digit(Writer w,int d,double cx,double cz,double cell)
        {
            int g=Glyphs[d%10];for(int row=0;row<5;row++)for(int col=0;col<3;col++)if(((g>>(14-(row*3+col)))&1)!=0)
            {double x=cx+(col-1)*cell,z=cz+(2-row)*cell;w.Box(x-cell*.46,x+cell*.46,0,.008,z-cell*.46,z+cell*.46,PaintWhite,false);}
        }
    }
}
