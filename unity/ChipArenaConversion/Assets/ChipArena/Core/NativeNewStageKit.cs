using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The five M1 stages' static kit (docs/arenas/NEW-STAGES.md "Stage looks (presentation)"): the detail NativeBlockLit instances
    /// draw on and over the map's dressed blocks, by block id stem, so each stage reads without the kit bank. Two kinds of part:
    /// <list type="bullet">
    /// <item>Attached: on its block (<see cref="Parent"/>): container corner posts, rails, doors and locking bars, tier seams and
    /// reefer units; lit windows, doors and signs on booths, gatehouses, modules and huts; copings, scuppers, reflectors, hoops,
    /// rail tubes and toe boards; the mast's lattice painted on its faces; floor paint (yard lines, the drop slots' hatching, the
    /// burner lanes, the muster point). Every attached part stays inside its block grown by <see cref="Proud"/> (STAGE-KITS
    /// "the collider rule": the stage rule allows 0.25 m below 3 m; these stay within 2 cm of the face nearly everywhere), so
    /// what looks like cover is cover and nothing invisible stops a shot.</item>
    /// <item>Overhead: structure above the play that no block stands for (<see cref="Overhead"/>, parent -1): Pier 9's ship-to-shore
    /// crane over its portal legs, Deepwell's derrick lattice and pedestal-crane booms, Polaris's radome dome over its unstandable
    /// roof, Skyframe's tower-crane head over its mast. Each stands at least <see cref="Clearance"/> m above every standable top
    /// under it (Tests NewStageLookChecks), like the Factory's crane bridge: nothing anyone could take for cover.</item>
    /// </list>
    /// Deterministic (the same map and look give the same parts); allocates only while building (Runtime packs it once per map).
    /// </summary>
    public sealed class NativeNewStageKit
    {
        public const float Proud=.25f;public const double Clearance=2.0,Decal=.012;
        public readonly NativeFactoryKit.Writer Parts;public int[] Parent=Array.Empty<int>();public bool[] Overhead=Array.Empty<bool>();
        private readonly List<int> parents=new List<int>();private readonly List<bool> overhead=new List<bool>();
        private int current=-1;private bool over;
        public int Count=>Parts.Count;
        private NativeNewStageKit(NativeFactoryKit.Looks looks){Parts=new NativeFactoryKit.Writer(1024,true,looks);}
        private void Track(int before){for(int i=before;i<Parts.Count;i++){parents.Add(current);overhead.Add(over);}}
        private void Add(double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=true,int color=-1,double window=1){int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow,color,window);Track(n);}
        private void Box(double x0,double x1,double y0,double y1,double z0,double z1,int kind,bool shadow=true,int color=-1,double window=1){int n=Parts.Count;Parts.Box(x0,x1,y0,y1,z0,z1,kind,shadow,color,window);Track(n);}
        private void Bar(double x0,double y0,double z0,double x1,double y1,double z1,double t,int kind,bool shadow=true,int color=-1){int n=Parts.Count;Parts.Bar(x0,y0,z0,x1,y1,z1,t,t,kind,shadow,color);Track(n);}
        private static double H(int a,int b=0,int c=0)=>NativeNewStageLooks.Hash(a,b,c);
        private static bool Starts(string id,string p)=>id!=null&&id.StartsWith(p,StringComparison.Ordinal);
        private static bool Has(string id,string p)=>id!=null&&id.IndexOf(p,StringComparison.Ordinal)>=0;
        private static int Shade(int hex,double k){int C(int s)=>(int)Math.Max(0,Math.Min(255,Math.Round(((hex>>s)&255)*k)));return C(16)<<16|C(8)<<8|C(0);}

        // A block's box in the world (the kit leaves turned blocks alone: none of the five stages has one).
        private struct Bx {public double X0,X1,Y0,Y1,Z0,Z1;public string Id;public int Color,Index;public double Sx=>X1-X0;public double Sy=>Y1-Y0;public double Sz=>Z1-Z0;public double Cx=>(X0+X1)/2;public double Cz=>(Z0+Z1)/2;}

        /// <summary>The map's kit for its look (null for a map that is not one of the five). `materials`: the content's materials
        /// (block colours resolve as NativeArenaDressing resolves them).</summary>
        public static NativeNewStageKit Build(ContentMap map,MapLook look,ContentMaterial[] materials)
        {
            int stage=NativeNewStageLooks.Index(map?.id);if(stage<0)return null;var k=new NativeNewStageKit(NativeNewStageLooks.Resolve(look?.kitLooks));
            var mats=new Dictionary<string,int>(StringComparer.Ordinal);foreach(var m in materials??Array.Empty<ContentMaterial>())if(m?.id!=null&&!mats.ContainsKey(m.id))mats[m.id]=m.color;
            var all=new List<Bx>();
            for(int i=0;i<(map.blocks?.Length??0);i++)
            {
                var b=map.blocks[i];if(b?.id==null)continue;var r=b.rotation;if(r!=null&&(Math.Abs(r.x)>1e-4||Math.Abs(r.y)>1e-4||Math.Abs(r.z)>1e-4))continue;
                int mc=b.material=="foundation"?map.floor:mats.TryGetValue(b.material??"",out int c)?c:0xFFFFFF;var ch=NativeBlockLooks.Resolve(look?.blockLooks,null,b.id,b.material,mc);
                all.Add(new Bx{X0=b.pos.x-b.size.x/2,X1=b.pos.x+b.size.x/2,Y0=b.pos.y-b.size.y/2,Y1=b.pos.y+b.size.y/2,Z0=b.pos.z-b.size.z/2,Z1=b.pos.z+b.size.z/2,Id=b.id,Color=ch.Color,Index=i});
            }
            switch(stage){case 0:k.Breakwater(all);break;case 1:k.Harbor(all,map);break;case 2:k.Skyframe(all,map);break;case 3:k.Rig(all,map);break;default:k.Polar(all);break;}
            k.Parent=k.parents.ToArray();k.Overhead=k.overhead.ToArray();return k;
        }
        private void On(Bx b){current=b.Index;over=false;}
        private void Above(){current=-1;over=true;}

        // ---------------- face helpers (u along the face, v up; a hair proud) ----------------
        /// <summary>A plate on one face of the block: side 0 +x, 1 -x, 2 +z, 3 -z, 4 the top; (u0..u1) along it (x for ±z faces
        /// and the top, z for ±x faces), (v0..v1) up it (world y; z on the top), `out` m proud, `t` thick.</summary>
        private void Face(Bx b,int side,double u0,double u1,double v0,double v1,int kind,int color=-1,double t=Decal,double o=.004,bool shadow=false,double window=1)
        {
            switch(side)
            {
                case 0:Box(b.X1+o-t,b.X1+o,v0,v1,u0,u1,kind,shadow,color,window);break;case 1:Box(b.X0-o,b.X0-o+t,v0,v1,u0,u1,kind,shadow,color,window);break;
                case 2:Box(u0,u1,v0,v1,b.Z1+o-t,b.Z1+o,kind,shadow,color,window);break;case 3:Box(u0,u1,v0,v1,b.Z0-o,b.Z0-o+t,kind,shadow,color,window);break;
                default:Box(u0,u1,b.Y1+o-t,b.Y1+o,v0,v1,kind,shadow,color,window);break;
            }
        }
        private static void Span(Bx b,int side,out double u0,out double u1){if(side<=1){u0=b.Z0;u1=b.Z1;}else{u0=b.X0;u1=b.X1;}}
        /// <summary>A row of windows across a face at height y (centre) `h` tall, `w` wide, every `pitch` m; lit by the hash share.</summary>
        private void Windows(Bx b,int side,double y,double h,double w,double pitch,double lit,int seed,double margin=.6)
        {
            Span(b,side,out double a0,out double a1);double len=a1-a0-2*margin;if(len<w)return;int n=Math.Max(1,(int)Math.Floor((len+pitch-w)/pitch));double start=a0+margin+(len-(n-1)*pitch-w)/2;
            for(int i=0;i<n;i++){double u=start+i*pitch;bool on=H(seed,i,side)<lit;Face(b,side,u-.06,u+w+.06,y-h/2-.06,y+h/2+.06,NativeNewStageLooks.Frame);Face(b,side,u,u+w,y-h/2,y+h/2,on?NativeNewStageLooks.Glass:NativeNewStageLooks.GlassDark,-1,Decal,.008);}
        }
        /// <summary>A shipping container's detail on a block: corner posts, top and bottom rails per tier, the door end (two doors,
        /// four locking bars, the seam) at the -long end, `tiers` tiers.</summary>
        private void Container(Bx b,int tiers,bool doors=true)
        {
            bool alongX=b.Sx>=b.Sz;int dark=Shade(b.Color,.55),rail=Shade(b.Color,.78);double th=b.Sy/Math.Max(1,tiers);
            for(int t=0;t<tiers;t++)
            {
                double y0=b.Y0+t*th,y1=y0+th;
                // corner posts (castings at the tier's corners)
                foreach(double ux in new[]{b.X0,b.X1})foreach(double uz in new[]{b.Z0,b.Z1}){double sx=ux<b.Cx?1:-1,sz=uz<b.Cz?1:-1;Box(Math.Min(ux-sx*.004,ux+sx*.16),Math.Max(ux-sx*.004,ux+sx*.16),y0,y1,Math.Min(uz-sz*.004,uz+sz*.16),Math.Max(uz-sz*.004,uz+sz*.16),NativeNewStageLooks.Dark,false,dark);}
                foreach(int side in alongX?new[]{2,3}:new[]{0,1}){Span(b,side,out double a0,out double a1);Face(b,side,a0+.16,a1-.16,y1-.14,y1-.02,NativeNewStageLooks.Steel,rail);Face(b,side,a0+.16,a1-.16,y0+.02,y0+.16,NativeNewStageLooks.Steel,rail);}
                if(!doors)continue;
                // the door end: -x (or -z) face
                int end=alongX?1:3;Span(b,end,out double e0,out double e1);double mid=(e0+e1)/2;
                Face(b,end,mid-.025,mid+.025,y0+.18,y1-.18,NativeNewStageLooks.Dark,dark);
                foreach(double f in new[]{.22,.38,.62,.78}){double u=e0+(e1-e0)*f;Face(b,end,u-.025,u+.025,y0+.2,y1-.2,NativeNewStageLooks.Steel,Shade(b.Color,.7),.03,.02);}
                foreach(double f in new[]{.3,.7}){double u=e0+(e1-e0)*f;Face(b,end,u-.12,u+.12,y0+th*.48,y0+th*.56,NativeNewStageLooks.Dark,dark,.03,.02);}
            }
        }
        /// <summary>A guard rail's paint on a thin rail block: the top tube, the mid rail and the toe board (safety yellow), the
        /// panel between dark (a solid edge guard: it stops shots, so it reads solid).</summary>
        private void RailBlock(Bx b)
        {
            bool alongX=b.Sx>=b.Sz;double len=alongX?b.Sx:b.Sz;
            foreach(int side in alongX?new[]{2,3}:new[]{0,1}){Span(b,side,out double a0,out double a1);Face(b,side,a0,a1,b.Y1-.09,b.Y1,NativeNewStageLooks.Rail);Face(b,side,a0,a1,b.Y0+(b.Sy)*.5-.035,b.Y0+b.Sy*.5+.035,NativeNewStageLooks.Rail);Face(b,side,a0,a1,b.Y0,b.Y0+.15,NativeNewStageLooks.Rail);
                for(double u=a0+.05;u<a1;u+=Math.Max(1.2,len/Math.Max(1,Math.Round(len/1.8))))Face(b,side,u,u+.06,b.Y0,b.Y1,NativeNewStageLooks.Rail);}
            Face(b,4,b.X0,b.X1,b.Z0,b.Z1,NativeNewStageLooks.Rail);
        }
        /// <summary>Floor paint: a rectangle's outline (`w` wide) on the top of `floor`, and its hatching (stripes `pitch` apart).</summary>
        private void Paint(Bx floor,double x0,double x1,double z0,double z1,double w,int kind,double pitch=0,int hatch=-1)
        {
            Face(floor,4,x0,x1,z0,z0+w,kind);Face(floor,4,x0,x1,z1-w,z1,kind);Face(floor,4,x0,x0+w,z0+w,z1-w,kind);Face(floor,4,x1-w,x1,z0+w,z1-w,kind);
            if(pitch<=0)return;for(double x=x0+pitch;x<x1-pitch*.5;x+=pitch)Face(floor,4,x-w*.5,x+w*.5,z0+w,z1-w,hatch<0?kind:hatch,-1,Decal,.006);
        }
        private static Bx? Find(List<Bx> all,string id){foreach(var b in all)if(b.Id==id)return b;return null;}
        /// <summary>The floor block under a point: the highest block top at or below `y` covering (x, z).</summary>
        private static Bx? Floor(List<Bx> all,double x,double z,double y)
        {Bx? best=null;foreach(var b in all)if(x>=b.X0&&x<=b.X1&&z>=b.Z0&&z<=b.Z1&&b.Y1<=y+.01&&(best==null||b.Y1>best.Value.Y1))best=b;return best;}

        // ---------------- Breakwater ----------------
        private void Breakwater(List<Bx> all)
        {
            var floor=Find(all,"breakwater-floor");
            foreach(var b in all)
            {
                string id=b.Id;On(b);
                if(Has(id,"-container-"))Container(b,1);
                else if(Starts(id,"breakwater-parapet-"))
                {
                    bool alongX=b.Sx>=b.Sz;// coping (overhangs 3 cm), scuppers at the foot every 4 m on the inner face
                    Box(b.X0-.03,b.X1+.03,b.Y1-.12,b.Y1+.02,b.Z0-.03,b.Z1+.03,NativeNewStageLooks.Concrete,true,-1);
                    int inner=alongX?(b.Cz>0?3:2):(b.Cx>0?1:0);Span(b,inner,out double a0,out double a1);
                    for(double u=a0+2;u<a1-1;u+=4)Face(b,inner,u-.25,u+.25,b.Y0,b.Y0+.18,NativeNewStageLooks.Dark);
                    for(double u=a0+4;u<a1-2;u+=8)Face(b,inner,u-.1,u+.1,b.Y0+.5,b.Y0+.7,NativeNewStageLooks.Steel,-1,.03,.02);
                }
                else if(Starts(id,"breakwater-booth-")&&Has(id,"-back"))
                {
                    int side=b.Cz>0?3:2;Windows(b,side,1.75,1.0,1.1,1.6,.85,b.Index,.7);Face(b,side,b.X0+.3,b.X1-.3,2.62,2.72,NativeNewStageLooks.Sign,-1,.02,.01);
                    Face(b,side==3?2:3,b.X0+.4,b.X1-.4,1.2,2.2,NativeNewStageLooks.Panel,-1,.012);
                }
                else if(Starts(id,"breakwater-booth-")&&Has(id,"-roof")){Face(b,4,b.X0,b.X1,b.Z0,b.Z1,NativeNewStageLooks.Dark,-1,.02,.005);Box(b.X0-.02,b.X1+.02,b.Y0-.02,b.Y0+.06,b.Cz>0?b.Z0-.02:b.Z1-.1,b.Cz>0?b.Z0+.1:b.Z1+.02,NativeNewStageLooks.Light,false);}
                else if(Starts(id,"breakwater-barrier-"))
                {
                    bool alongX=b.Sx>=b.Sz;double len=alongX?b.Sx:b.Sz;for(double u=.2;u<len-.2;u+=.8){int kind=((int)(u/.8)&1)==0?NativeNewStageLooks.Red:NativeNewStageLooks.White;if(alongX)Face(b,4,b.X0+u,b.X0+u+.4,b.Z0+.05,b.Z1-.05,kind);else Face(b,4,b.X0+.05,b.X1-.05,b.Z0+u,b.Z0+u+.4,kind);}
                }
                else if(Starts(id,"breakwater-winch-"))
                {
                    int side=b.Cz>0?3:2;Face(b,side,b.X0+.4,b.X1-.4,b.Y0,b.Y0+2.0,NativeNewStageLooks.Door);Face(b,side,b.X0+.2,b.X1-.2,b.Y1-.5,b.Y1-.3,NativeNewStageLooks.Grate);
                    foreach(int s in new[]{0,1,2,3}){Span(b,s,out double a0,out double a1);Face(b,s,a0,a1,b.Y0,b.Y0+.3,NativeNewStageLooks.Stripe);}
                    Face(b,4,b.X0+.2,b.X1-.2,b.Z0+.2,b.Z1-.2,NativeNewStageLooks.Dark);
                }
                else if(Starts(id,"breakwater-drums-"))foreach(double y in new[]{.28,.72})foreach(int s in new[]{0,1,2,3}){Span(b,s,out double a0,out double a1);Face(b,s,a0,a1,y-.03,y+.03,NativeNewStageLooks.Dark,-1,.015,.01);}
            }
            if(floor is Bx f)
            {
                // the gutter inside the parapet, iron hatch covers off the lanes, mooring rings by the wall
                On(f);Paint(f,-19.92,19.92,-12.92,12.92,.32,NativeNewStageLooks.Dark);
                foreach(var (x,z) in new[]{(-10.5,0.0),(10.5,0.0),(-6.0,-11.4),(6.0,11.4),(-17.0,-9.6),(17.0,9.6)}){Face(f,4,x-.45,x+.45,z-.45,z+.45,NativeNewStageLooks.Grate);Face(f,4,x-.5,x+.5,z-.5,z+.5,NativeNewStageLooks.Dark,-1,Decal,.002);}
                for(double x=-16;x<=16;x+=8)foreach(int side in new[]{-1,1})Face(f,4,x-.15,x+.15,side*12.4-.15,side*12.4+.15,NativeNewStageLooks.Steel,-1,.03,.02);
            }
        }

        // ---------------- Pier 9 ----------------
        private void Harbor(List<Bx> all,ContentMap map)
        {
            var floor=Find(all,"harbor-floor");
            foreach(var b in all)
            {
                string id=b.Id;On(b);
                if(Starts(id,"harbor-stack-"))Container(b,(int)Math.Round(b.Sy/2.6));
                else if(Starts(id,"harbor-reefer-"))
                {
                    Container(b,3,false);int end=b.Cx<0?0:1;Span(b,end,out double a0,out double a1);
                    for(int t=0;t<3;t++){double y=b.Y0+t*2.6;Face(b,end,a0+.25,a1-.25,y+.25,y+2.35,NativeNewStageLooks.Panel);Face(b,end,a0+.5,a0+1.2,y+1.2,y+2.0,NativeNewStageLooks.Grate,-1,.02,.012);Face(b,end,a1-.6,a1-.45,y+2.0,y+2.15,NativeNewStageLooks.Lamp,0x40FF70,.02,.014);}
                }
                else if(Starts(id,"harbor-hatch-")){foreach(int s in new[]{2,3}){Span(b,s,out double a0,out double a1);Face(b,s,a0,a1,b.Y0+1.15,b.Y0+1.25,NativeNewStageLooks.Dark);Face(b,s,a0+.3,a1-.3,b.Y1-.12,b.Y1,NativeNewStageLooks.Stripe);}Face(b,4,b.X0+.4,b.X1-.4,b.Z0+.3,b.Z1-.3,NativeNewStageLooks.Steel);}
                else if(Starts(id,"harbor-truck-")&&Has(id,"-trailer"))
                {
                    foreach(int s in new[]{0,1}){Span(b,s,out double a0,out double a1);Face(b,s,a0,a1,b.Y0,b.Y0+1.3,NativeNewStageLooks.Dark);foreach(double u in new[]{a0+.5,a0+1.7,a1-3.4})Face(b,s,u,u+1.0,b.Y0+.05,b.Y0+1.05,NativeNewStageLooks.Wheel,-1,.03,.02);}
                    var top=new Bx{X0=b.X0,X1=b.X1,Y0=b.Y0+1.3,Y1=b.Y1,Z0=b.Z0,Z1=b.Z1,Color=b.Color,Index=b.Index,Id=id};Container(top,1);
                }
                else if(Starts(id,"harbor-truck-")&&Has(id,"-cab"))
                {
                    Face(b,2,b.X0+.2,b.X1-.2,b.Y0+1.9,b.Y1-.3,NativeNewStageLooks.GlassDark);Face(b,2,b.X0+.3,b.X1-.3,b.Y0+.5,b.Y0+1.5,NativeNewStageLooks.Grate);
                    foreach(double u in new[]{b.X0+.25,b.X1-.55})Face(b,2,u,u+.3,b.Y0+.7,b.Y0+.95,NativeNewStageLooks.Light,-1,.02,.012);
                    foreach(int s in new[]{0,1}){Span(b,s,out double a0,out double a1);Face(b,s,a0+.3,a0+1.3,b.Y0+.05,b.Y0+1.05,NativeNewStageLooks.Wheel,-1,.03,.02);Face(b,s,a0+.4,a1-.4,b.Y0+1.9,b.Y1-.4,NativeNewStageLooks.GlassDark);}
                }
                else if(Starts(id,"harbor-gatehouse-")||Starts(id,"harbor-office-"))
                {
                    for(int s=0;s<4;s++)Windows(b,s,b.Y0+1.6,1.0,1.2,1.9,.8,b.Index+s,.6);Box(b.X0-.06,b.X1+.06,b.Y1-.16,b.Y1+.03,b.Z0-.06,b.Z1+.06,NativeNewStageLooks.Concrete,true);
                    int door=Starts(id,"harbor-office-")?3:b.Cx<0?0:1;Span(b,door,out double a0,out double a1);double m=(a0+a1)/2;Face(b,door,m-.5,m+.5,b.Y0,b.Y0+2.1,NativeNewStageLooks.Door,-1,.016,.012);
                }
                else if(Starts(id,"harbor-rack-"))
                {
                    foreach(int s in b.Sx<b.Sz?new[]{0,1}:new[]{2,3})
                    {
                        Span(b,s,out double a0,out double a1);for(double u=a0;u<=a1+1e-6;u+=(a1-a0)/2)Face(b,s,Math.Max(a0,u-.06),Math.Min(a1,u+.06),b.Y0,b.Y1,NativeNewStageLooks.Steel,0x2F5F9E,.02,.014);
                        foreach(double y in new[]{1.55,3.15})Face(b,s,a0,a1,b.Y0+y,b.Y0+y+.14,NativeNewStageLooks.Steel,0xD0662A,.02,.014);
                        for(int lv=0;lv<3;lv++)for(int c=0;c<2;c++){double u0=a0+.15+c*(a1-a0)/2,u1=u0+(a1-a0)/2-.3,y0=b.Y0+.15+lv*1.6;Face(b,s,u0,u1,y0,y0+1.2,NativeNewStageLooks.Panel,Shade(0xB89A6A,.8+.3*H(b.Index,lv,c)),.012,.008);}
                    }
                }
                else if(Starts(id,"harbor-gantry-runway-")){Box(b.X0,b.X1,b.Y1,b.Y1+.12,b.Cz-.06,b.Cz+.06,NativeNewStageLooks.Steel,true,0x8A9096);foreach(int s in new[]{2,3}){Span(b,s,out double a0,out double a1);Face(b,s,a0,a1,b.Y0,b.Y0+.12,NativeNewStageLooks.Dark);}}
                else if(Starts(id,"harbor-gantry-column-")||Starts(id,"harbor-leg-sts-")||Starts(id,"harbor-shed-column-"))for(int s=0;s<4;s++){Span(b,s,out double a0,out double a1);Face(b,s,a0,a1,b.Y0,b.Y0+1.0,NativeNewStageLooks.Stripe);}
                else if(Starts(id,"harbor-bollard-"))Box(b.X0-.04,b.X1+.04,b.Y1-.12,b.Y1+.02,b.Z0-.04,b.Z1+.04,NativeNewStageLooks.Crane,true);
                else if(Starts(id,"harbor-rail-")||Starts(id,"harbor-sill-"))RailBlock(b);
                else if(id=="harbor-shed-roof")
                {
                    for(double x=-34;x<=34;x+=12)for(double z=-26;z<=-14;z+=6)Box(x-1.4,x+1.4,b.Y0-.06,b.Y0,z-.18,z+.18,NativeNewStageLooks.Light,false);
                    Box(b.X0,b.X1,b.Y0-.24,b.Y0,b.Z1-.24,b.Z1,NativeNewStageLooks.Steel,true);
                }
                else if(id=="harbor-wall-s"){for(double x=-34;x<=34;x+=17)Face(b,2,x-2.4,x+2.4,b.Y0,b.Y0+5.2,NativeNewStageLooks.Door);Face(b,2,b.X0,b.X1,b.Y1-.3,b.Y1,NativeNewStageLooks.Concrete,-1,.06,.05);}
                else if(Starts(id,"harbor-wall-"))
                {
                    int s=b.Cx<0?0:1;Span(b,s,out double a0,out double a1);for(double u=a0+3;u<a1;u+=6)Face(b,s,u-.15,u+.15,b.Y0,b.Y1,NativeNewStageLooks.Steel,-1,.03,.02);
                    Face(b,s,a0,a1,b.Y1-.25,b.Y1,NativeNewStageLooks.Steel,-1,.03,.02);Face(b,s,-3,3,4.6,5.8,NativeNewStageLooks.Sign,-1,.02,.012);
                }
                else if(Starts(id,"harbor-forklift-")){int s=b.Cz>-20?2:3;Face(b,s,b.X0+.1,b.X1-.1,b.Y0+1.0,b.Y1-.1,NativeNewStageLooks.GlassDark);foreach(int t in new[]{0,1}){Span(b,t,out double a0,out double a1);Face(b,t,a0+.2,a0+.9,b.Y0,b.Y0+.7,NativeNewStageLooks.Wheel,-1,.03,.02);Face(b,t,a1-.9,a1-.2,b.Y0,b.Y0+.7,NativeNewStageLooks.Wheel,-1,.03,.02);}}
                else if(id=="harbor-gangway-c"){Face(b,4,b.X0,b.X1,b.Z0,b.Z1,NativeNewStageLooks.Grate);Face(b,3,b.X0,b.X1,b.Y0,b.Y1,NativeNewStageLooks.Stripe,-1,Decal,.004);}
            }
            if(floor is Bx f)
            {
                On(f);
                // the drop slots in the cross aisle (the crane's zones: their kill box plus 0.2 m, hatched), the yard lanes, the road.
                foreach(var h in map.hazards??Array.Empty<MapHazard>())if(h?.kind=="crane")foreach(var z in h.zones??Array.Empty<MapVolume>()){double hx=z.size.x/2+.2,hz=z.size.z/2+.2;Paint(f,z.center.x-hx,z.center.x+hx,z.center.z-hz,z.center.z+hz,.2,NativeNewStageLooks.PaintZone,.9);}
                foreach(double z in new[]{1.45,6.55})Face(f,4,-38,38,z-.06,z+.06,NativeNewStageLooks.Paint);
                foreach(double x in new[]{-7.7,7.7})Face(f,4,x-.06,x+.06,-5.6,13.6,NativeNewStageLooks.Paint);
                for(double x=-36;x<=36;x+=4)Face(f,4,x-1.2,x+1.2,-7.86,-7.74,NativeNewStageLooks.PaintZone);
                Face(f,4,-40,40,28.9,29.1,NativeNewStageLooks.PaintZone);Face(f,4,-40,40,13.5,13.6,NativeNewStageLooks.Paint);
            }
            // The ship-to-shore crane over its portal legs (overhead: its portal beams from 15 m, far above the 9 m deck).
            Above();Sts(all);
        }
        private void Sts(List<Bx> all)
        {
            const double y0=15,x0=-6.2,x1=6.2,z0=19.8,z1=28.2;int K=NativeNewStageLooks.CraneAlt,D=NativeNewStageLooks.CraneDark;
            foreach(double z in new[]{z0+.6,z1-.6})Box(x0,x1,y0,y0+1.6,z-.6,z+.6,K);foreach(double x in new[]{x0+.6,x1-.6})Box(x-.6,x+.6,y0,y0+1.6,z0,z1,K);
            foreach(double x in new[]{x0+.6,x1-.6}){Bar(x,y0+1.6,z0+.6,x,33.5,z0+3.5,.9,K);Bar(x,y0+1.6,z1-.6,x,33.5,z0+3.5,.9,K);}
            Box(x0+.2,x1-.2,31,32.6,z0-26,z1+48,K);Box(-1.2,1.2,32.6,33.2,z0-26,z1+48,D,false);
            foreach(double x in new[]{x0+.6,x1-.6}){Bar(x,33.5,z0+3.5,x,32.6,z1+40,.35,D,false);Bar(x,33.5,z0+3.5,x,32.6,z0-22,.35,D,false);}
            Box(-3.6,3.6,32.6,36.8,z0-24,z0-12,D);for(double z=z0-23;z<z0-13;z+=1.4)Box(-3.66,-3.6,34,35,z,z+.8,NativeNewStageLooks.Glass,false);
            Box(-1.4,1.4,29.4,31,z1+3,z1+6,NativeNewStageLooks.Panel);Box(-1.36,1.36,29.6,30.8,z1+5.96,z1+6.02,NativeNewStageLooks.Glass,false);
            Box(-.9,.9,30,31,z1+12,z1+14,D);foreach(double dx in new[]{-.4,.4})Bar(dx,30,z1+13,dx,9.6,z1+13,.05,NativeNewStageLooks.Cable,false);
            Box(-.25,.25,33.5,34,z0+3.25,z0+3.75,NativeNewStageLooks.Lamp,false,0xFF3020);Box(-.25,.25,32.6,33.1,z1+47.5,z1+48,NativeNewStageLooks.Lamp,false,0xFF3020);
            foreach(double x in new[]{x0,x1})for(double z=z0+.8;z<z1-.8;z+=2.4)Bar(x,y0+1.6,z,x,y0+1.6,z+2.4,.1,NativeNewStageLooks.Rail,false);
        }

        // ---------------- Skyframe ----------------
        private void Skyframe(List<Bx> all,ContentMap map)
        {
            var slab=Find(all,"skyframe-slab");
            foreach(var b in all)
            {
                string id=b.Id;On(b);
                if(Starts(id,"skyframe-cabin-"))
                {
                    Container(b,1,false);int s=b.Sx>=b.Sz?(b.Cz>0?3:2):(b.Cx>0?1:0);Windows(b,s,b.Y0+1.55,.8,1.0,2.0,0,b.Index,.8);Span(b,s,out double a0,out double a1);Face(b,s,a1-1.4,a1-.5,b.Y0,b.Y0+2.1,NativeNewStageLooks.Door);
                    Face(b,4,b.X0+.1,b.X1-.1,b.Z0+.1,b.Z1-.1,NativeNewStageLooks.Dark,-1,Decal,.003);
                }
                else if(Starts(id,"skyframe-container-"))Container(b,1);
                else if(Starts(id,"skyframe-rail-"))RailBlock(b);
                else if(Starts(id,"skyframe-column-"))
                {
                    foreach(int s in new[]{0,1}){Span(b,s,out double a0,out double a1);Face(b,s,a0+.06,a1-.06,b.Y0,b.Y1,NativeNewStageLooks.Dark);}
                    Box(b.X0-.06,b.X1+.06,b.Y0,b.Y0+.05,b.Z0-.06,b.Z1+.06,NativeNewStageLooks.Steel,false);Box(b.X0-.04,b.X1+.04,b.Y1-.3,b.Y1,b.Z0-.04,b.Z1+.04,NativeNewStageLooks.Steel,true);
                }
                else if(id=="skyframe-crane-mast"||Starts(id,"skyframe-drill-")||id=="skyframe-crane-frame")Mast(b,id=="skyframe-crane-frame"?1.1:2.0);
                else if(Starts(id,"skyframe-bundle-"))
                {
                    bool alongX=b.Sx>=b.Sz;foreach(int end in alongX?new[]{0,1}:new[]{2,3}){Span(b,end,out double a0,out double a1);double w=(a1-a0)/3;for(int k=0;k<3;k++){double u=a0+w*k;Face(b,end,u+.03,u+w-.03,b.Y0+.04,b.Y0+.1,NativeNewStageLooks.Dark);Face(b,end,u+.03,u+w-.03,b.Y1-.1,b.Y1-.04,NativeNewStageLooks.Dark);Face(b,end,u+w/2-.03,u+w/2+.03,b.Y0+.1,b.Y1-.1,NativeNewStageLooks.Dark);}}
                    double len=alongX?b.Sx:b.Sz;foreach(double f in new[]{.25,.75}){double u=(alongX?b.X0:b.Z0)+len*f;if(alongX){Face(b,2,u-.06,u+.06,b.Y0,b.Y1,NativeNewStageLooks.Red);Face(b,3,u-.06,u+.06,b.Y0,b.Y1,NativeNewStageLooks.Red);Face(b,4,u-.06,u+.06,b.Z0,b.Z1,NativeNewStageLooks.Red);}else{Face(b,0,u-.06,u+.06,b.Y0,b.Y1,NativeNewStageLooks.Red);Face(b,1,u-.06,u+.06,b.Y0,b.Y1,NativeNewStageLooks.Red);Face(b,4,b.X0,b.X1,u-.06,u+.06,NativeNewStageLooks.Red);}}
                }
                else if(Starts(id,"skyframe-blocks-")||Starts(id,"skyframe-pallet-"))
                {
                    for(int s=0;s<4;s++){Span(b,s,out double a0,out double a1);Face(b,s,a0,a1,b.Y0,b.Y0+.14,NativeNewStageLooks.Timber);for(double y=b.Y0+.14+.4;y<b.Y1-.05;y+=.4)Face(b,s,a0,a1,y-.015,y+.015,NativeNewStageLooks.Dark,-1,Decal,.006);}
                    if(Starts(id,"skyframe-pallet-")){bool ax=b.Sx>=b.Sz;foreach(double f in new[]{.3,.7}){double u=(ax?b.X0:b.Z0)+(ax?b.Sx:b.Sz)*f;if(ax){Face(b,2,u-.03,u+.03,b.Y0,b.Y1,NativeNewStageLooks.Steel);Face(b,3,u-.03,u+.03,b.Y0,b.Y1,NativeNewStageLooks.Steel);}else{Face(b,0,u-.03,u+.03,b.Y0,b.Y1,NativeNewStageLooks.Steel);Face(b,1,u-.03,u+.03,b.Y0,b.Y1,NativeNewStageLooks.Steel);}}}
                }
                else if(Starts(id,"skyframe-perch-"))
                {
                    for(int s=0;s<4;s++){Span(b,s,out double a0,out double a1);for(double u=a0;u<=a1+1e-6;u+=(a1-a0)/2)Face(b,s,Math.Max(a0,u-.05),Math.Min(a1,u+.05),b.Y0,b.Y1,NativeNewStageLooks.Steel,-1,.03,.02);foreach(double y in new[]{b.Y0+.05,b.Y0+1.8,b.Y1-.05})Face(b,s,a0,a1,y-.04,y+.04,NativeNewStageLooks.Steel,-1,.03,.02);Bar(s<=1?(s==0?b.X1+.02:b.X0-.02):a0,b.Y0,s<=1?a0:(s==2?b.Z1+.02:b.Z0-.02),s<=1?(s==0?b.X1+.02:b.X0-.02):a1,b.Y1,s<=1?a1:(s==2?b.Z1+.02:b.Z0-.02),.05,NativeNewStageLooks.Steel,false);}
                }
                else if(Starts(id,"skyframe-wall-"))
                {
                    int s=b.Cz>0?3:2;Face(b,s,-9,9,7.4,10.6,NativeNewStageLooks.Sign,-1,.03,.02);Face(b,s,-9,9,7.4,7.7,NativeNewStageLooks.Crane,-1,.035,.025);Face(b,s,-9,-5.6,7.7,10.6,NativeNewStageLooks.Crane,-1,.035,.025);
                    for(double x=-24;x<=24;x+=8){Face(b,s,x-.08,x+.08,0,12,NativeNewStageLooks.Concrete,-1,.04,.03);}
                    for(double x=-20;x<=20;x+=16)Box(x-.5,x+.5,b.Y0+5.6,b.Y0+5.9,s==3?b.Z0-.22:b.Z1,s==3?b.Z0:b.Z1+.22,NativeNewStageLooks.Light,false);
                    Face(b,s,b.X0,b.X1,b.Y1-.25,b.Y1,NativeNewStageLooks.Concrete,-1,.05,.04);
                }
                else if(Starts(id,"skyframe-core-"))
                {
                    if(Has(id,"-wall-")&&!Has(id,"-head")&&!Has(id,"-sill")&&b.Sy>9)for(int s=0;s<4;s++){Span(b,s,out double a0,out double a1);if(a1-a0<1)continue;Face(b,s,a0,a1,3.5,3.7,NativeNewStageLooks.Concrete,-1,.02,.012);}
                }
                else if(Starts(id,"skyframe-formwork-"))for(int s=0;s<4;s++){Span(b,s,out double a0,out double a1);for(double u=a0+.6;u<a1-.2;u+=.6)Face(b,s,u-.02,u+.02,b.Y0,b.Y1,NativeNewStageLooks.Dark,-1,Decal,.006);}
            }
            // The drop zones' paint on the slab and on the decks under them, the slab's edge paint where it meets the void.
            foreach(var h in map.hazards??Array.Empty<MapHazard>())if(h?.kind=="crane")foreach(var z in h.zones??Array.Empty<MapVolume>())
            {var fl=Floor(all,z.center.x,z.center.z,z.center.y+.05);if(fl is Bx f){On(f);double hx=z.size.x/2+.2,hz=z.size.z/2+.2;Paint(f,z.center.x-hx,z.center.x+hx,z.center.z-hz,z.center.z+hz,.2,NativeNewStageLooks.PaintZone,.9,NativeNewStageLooks.Stripe);}}
            if(slab is Bx sl){On(sl);foreach(int side in new[]{-1,1})Face(sl,4,side>0?27.2:-27.8,side>0?27.8:-27.2,-24,24,NativeNewStageLooks.PaintZone);for(double x=-27;x<=27;x+=6)Face(sl,4,x-.04,x+.04,-24,24,NativeNewStageLooks.Dark,-1,Decal,.002);}
            // The tower crane's head over the mast's top (the slewing jib is the rig's): the apex and its pendant anchors.
            if(Find(all,"skyframe-crane-mast") is Bx mast){Above();double t=mast.Y1;Bar(-.9,t,-.9,0,t+5,0,.18,NativeNewStageLooks.Crane);Bar(.9,t,-.9,0,t+5,0,.18,NativeNewStageLooks.Crane);Bar(-.9,t,.9,0,t+5,0,.18,NativeNewStageLooks.Crane);Bar(.9,t,.9,0,t+5,0,.18,NativeNewStageLooks.Crane);Box(-.3,.3,t+5,t+5.4,-.3,.3,NativeNewStageLooks.CraneDark);Box(-.18,.18,t+5.4,t+5.75,-.18,.18,NativeNewStageLooks.Lamp,false,0xFF3020);}
        }
        /// <summary>A lattice mast painted on a solid block (it stops shots, so it reads boxed: a dark core, yellow chords at the
        /// corners and braces on every face every `bay` m).</summary>
        private void Mast(Bx b,double bay)
        {
            foreach(double x in new[]{b.X0,b.X1})foreach(double z in new[]{b.Z0,b.Z1}){double sx=x<b.Cx?1:-1,sz=z<b.Cz?1:-1;Box(Math.Min(x-sx*.01,x+sx*.18),Math.Max(x-sx*.01,x+sx*.18),b.Y0,b.Y1,Math.Min(z-sz*.01,z+sz*.18),Math.Max(z-sz*.01,z+sz*.18),NativeNewStageLooks.Crane,true);}
            for(int s=0;s<4;s++)
            {
                Span(b,s,out double a0,out double a1);double w=a1-a0;
                for(double y=b.Y0;y<b.Y1-.1;y+=bay)
                {
                    double y1=Math.Min(b.Y1,y+bay);Face(b,s,a0,a1,y,y+.08,NativeNewStageLooks.Crane,-1,.02,.012);
                    bool flip=((int)((y-b.Y0)/bay)&1)==1;double ua=flip?a1-.1:a0+.1,ub=flip?a0+.1:a1-.1,ox=s==0?b.X1+.01:s==1?b.X0-.01:0,oz=s==2?b.Z1+.01:s==3?b.Z0-.01:0;
                    if(s<=1)Bar(ox,y+.05,ua,ox,y1-.05,ub,.07,NativeNewStageLooks.Crane,false);else Bar(ua,y+.05,oz,ub,y1-.05,oz,.07,NativeNewStageLooks.Crane,false);
                }
            }
        }

        // ---------------- Deepwell ----------------
        private void Rig(List<Bx> all,ContentMap map)
        {
            foreach(var b in all)
            {
                string id=b.Id;On(b);
                if(Starts(id,"rig-rail-"))RailBlock(b);
                else if(Starts(id,"rig-accom-")&&(Has(id,"-wall-")||Has(id,"-upper")||Has(id,"-utility")))
                {
                    for(int s=0;s<4;s++){Span(b,s,out double a0,out double a1);if(a1-a0<2.5)continue;double rows=Math.Floor(b.Sy/1.75);for(int r=0;r<rows;r++)Windows(b,s,b.Y0+1.0+r*1.75,.7,.9,1.6,.75,b.Index*7+r+s*31,.5);}
                    if(Has(id,"-upper")){int s=b.Cx<0?0:1;Span(b,s,out double a0,out double a1);Face(b,s,a0+2,a1-2,b.Y1-.75,b.Y1-.2,NativeNewStageLooks.Sign,-1,.02,.012);}
                }
                else if(Starts(id,"rig-kodrum-"))
                {
                    foreach(double f in new[]{.18,.82}){double x=b.X0+b.Sx*f;Box(x-.25,x+.25,b.Y0,b.Y0+.6,b.Z0-.02,b.Z1+.02,NativeNewStageLooks.Dark,true);}
                    foreach(double f in new[]{0.0,1.0}){double x=b.X0+b.Sx*f;Box(x-.04,x+.04,b.Y0+.6,b.Y1-.05,b.Z0+.15,b.Z1-.15,NativeNewStageLooks.Steel,false);}
                    int walk=b.Cz>0?2:3;Span(b,walk,out double a0,out double a1);Face(b,walk,a0+.3,a1-.3,b.Y1-.5,b.Y1-.38,NativeNewStageLooks.Stripe,-1,.02,.012);
                    Face(b,walk,(a0+a1)/2-.6,(a0+a1)/2+.6,b.Y0+1.2,b.Y0+1.8,NativeNewStageLooks.Sign,-1,.02,.012);
                    Box(b.Cx-.3,b.Cx+.3,b.Y1,b.Y1+.22,b.Cz-.3,b.Cz+.3,NativeNewStageLooks.PipeHot,true);
                }
                else if(Starts(id,"rig-module-")||Starts(id,"rig-switchroom-")||Starts(id,"rig-separator-")||Starts(id,"rig-compressor-")||Starts(id,"rig-skid-")||Starts(id,"rig-heater-")||Starts(id,"rig-pump-"))
                {
                    for(int s=0;s<4;s++)
                    {
                        Span(b,s,out double a0,out double a1);if(a1-a0<1.6)continue;double m=(a0+a1)/2;
                        if(s==((int)(H(b.Index,1)*4)&3)&&b.Sy>2.5)Face(b,s,m-.55,m+.55,b.Y0,b.Y0+2.1,NativeNewStageLooks.Door);
                        else{Face(b,s,a0+.3,a1-.3,b.Y0+.5,b.Y0+.62,NativeNewStageLooks.Pipe,-1,.06,.05);Face(b,s,a0+.3,a1-.3,b.Y0+1.3,b.Y0+1.42,NativeNewStageLooks.Pipe,Shade(0x6A7076,.8),.06,.05);if(b.Sy>2.4)Face(b,s,m-.4,m+.4,b.Y1-.9,b.Y1-.3,NativeNewStageLooks.Grate,-1,.02,.012);}
                    }
                    Face(b,4,b.X0+.15,b.X1-.15,b.Z0+.15,b.Z1-.15,NativeNewStageLooks.Grate,-1,Decal,.004);
                }
                else if(Starts(id,"rig-rack-"))
                {
                    bool alongX=b.Sx>=b.Sz;double len=alongX?b.Sx:b.Sz,wid=alongX?b.Sz:b.Sx;int n=Math.Max(2,(int)(wid/.32));
                    for(int k=0;k<n;k++){double c=(alongX?b.Z0:b.X0)+wid*(k+.5)/n,r=wid/n*.42;int col=k%3==0?0x8A2A24:k%3==1?0x6A7076:0xC8A028;if(alongX)Box(b.X0,b.X1,b.Y1-.04-2*r,b.Y1-.04,c-r,c+r,NativeNewStageLooks.Pipe,true,col);else Box(c-r,c+r,b.Y1-.04-2*r,b.Y1-.04,b.Z0,b.Z1,NativeNewStageLooks.Pipe,true,col);}
                }
                else if(Starts(id,"rig-telltale-"))Box(b.X0-.04,b.X1+.04,b.Y1-.3,b.Y1+.02,b.Z0-.04,b.Z1+.04,NativeNewStageLooks.Lamp,false,0x40FF70);
                else if(Starts(id,"rig-derrick-leg-"))for(int s=0;s<4;s++){Span(b,s,out double a0,out double a1);for(double y=b.Y0+1.2;y<b.Y1;y+=2.4)Face(b,s,a0,a1,y-.05,y+.05,NativeNewStageLooks.Dark,-1,.02,.012);}
                else if(id=="rig-wellhead"||Starts(id,"rig-bop"))
                {
                    for(int s=0;s<4;s++){Span(b,s,out double a0,out double a1);double m=(a0+a1)/2;Face(b,s,m-.6,m+.6,b.Y0+.4,b.Y1-.3,NativeNewStageLooks.Red,-1,.03,.02);Face(b,s,a0,a1,b.Y0,b.Y0+.25,NativeNewStageLooks.Dark);}
                    Face(b,4,b.X0+.4,b.X1-.4,b.Z0+.4,b.Z1-.4,NativeNewStageLooks.Grate);
                }
                else if(Starts(id,"rig-leg-"))for(int s=0;s<4;s++){Span(b,s,out double a0,out double a1);Face(b,s,a0,a1,b.Y0,b.Y0+.6,NativeNewStageLooks.Stripe);Face(b,s,a0,a1,b.Y1-.4,b.Y1,NativeNewStageLooks.Dark);}
                else if(Starts(id,"rig-crane-")){for(int s=0;s<4;s++){Span(b,s,out double a0,out double a1);Face(b,s,a0,a1,b.Y0,b.Y0+.4,NativeNewStageLooks.Stripe);Face(b,s,a0,a1,b.Y1-.3,b.Y1,NativeNewStageLooks.Dark);}}
                else if(Starts(id,"rig-tank-")||Starts(id,"rig-sump-")||Starts(id,"rig-filter-"))
                {
                    for(int s=0;s<4;s++){Span(b,s,out double a0,out double a1);foreach(double y in new[]{.3,b.Sy-.3})Face(b,s,a0,a1,b.Y0+y-.05,b.Y0+y+.05,NativeNewStageLooks.Dark);}
                    int side=b.Cz>0?3:2;Span(b,side,out double c0,out double c1);double cm=(c0+c1)/2;for(double y=b.Y0+.3;y<b.Y1-.2;y+=.3)Face(b,side,cm-.25,cm+.25,y-.02,y+.02,NativeNewStageLooks.Rail,-1,.05,.04);
                }
                else if(Starts(id,"rig-locker-"))for(int s=0;s<4;s++){Span(b,s,out double a0,out double a1);for(double u=a0+.4;u<a1-.2;u+=.6)Face(b,s,u-.02,u+.02,b.Y0+.1,b.Y1-.1,NativeNewStageLooks.Dark,-1,Decal,.008);}
            }
            // Deck paint: the burner lanes (hatched red over their zones), the vents' grates and rings, walkway edges, muster points.
            foreach(var h in map.hazards??Array.Empty<MapHazard>())
            {
                if(h?.kind=="burner")foreach(var z in h.zones??Array.Empty<MapVolume>()){double y=z.center.y-z.size.y/2;var fl=Floor(all,z.center.x,z.center.z,y+.05);if(fl is Bx f){On(f);Paint(f,z.center.x-z.size.x/2-.2,z.center.x+z.size.x/2+.2,z.center.z-z.size.z/2-.1,z.center.z+z.size.z/2+.1,.18,NativeNewStageLooks.Red,.7,NativeNewStageLooks.Stripe);}}
                if(h?.kind=="vent"){var fl=Floor(all,h.a.x,h.a.z,h.a.y+.05);if(fl is Bx f){On(f);double hx=h.size.x/2+.25,hz=h.size.z/2+.25;Paint(f,h.a.x-hx,h.a.x+hx,h.a.z-hz,h.a.z+hz,.15,NativeNewStageLooks.PaintZone,.5,NativeNewStageLooks.Stripe);}}
            }
            foreach(var b in all)if(Starts(b.Id,"rig-deck-main-")||Starts(b.Id,"rig-deck-drill")){On(b);Face(b,4,b.X0,b.X1,b.Z0,b.Z0+.12,NativeNewStageLooks.Rail);Face(b,4,b.X0,b.X1,b.Z1-.12,b.Z1,NativeNewStageLooks.Rail);Face(b,4,b.X0,b.X0+.12,b.Z0,b.Z1,NativeNewStageLooks.Rail);Face(b,4,b.X1-.12,b.X1,b.Z0,b.Z1,NativeNewStageLooks.Rail);}
            if(Find(all,"rig-deck-main-0") is Bx m0){On(m0);Paint(m0,-22.6,-18.6,-2.0,2.0,.18,NativeNewStageLooks.Light);}
            if(Find(all,"rig-deck-main-6") is Bx m6){On(m6);Paint(m6,18.6,22.6,-2.0,2.0,.18,NativeNewStageLooks.Light);}
            // The derrick's lattice from 3.2 m above the drill floor to its crown, the pedestal cranes' booms in their rests (overhead).
            Above();
            var legs=new List<Bx>();foreach(var b in all)if(Starts(b.Id,"rig-derrick-leg-"))legs.Add(b);
            if(legs.Count==4)
            {
                double lo=legs[0].Y0+3.2,hi=legs[0].Y1;double x0=-4.1,x1=4.1,z0=-4.1,z1=4.1;
                for(double y=lo;y<hi-.5;y+=2.2){double y1=Math.Min(hi,y+2.2);foreach(var (ax,az,bx,bz) in new[]{(x0,z0,x1,z0),(x0,z1,x1,z1),(x0,z0,x0,z1),(x1,z0,x1,z1)}){Bar(ax,y,az,bx,y1,bz,.12,NativeNewStageLooks.Crane);Bar(bx,y,bz,ax,y1,az,.12,NativeNewStageLooks.Crane);Bar(ax,y1,az,bx,y1,bz,.14,NativeNewStageLooks.Crane);}}
                Box(-1.6,1.6,hi+1,hi+1.6,-.4,.4,NativeNewStageLooks.CraneDark);Box(-.25,.25,hi+1.6,hi+1.95,-.25,.25,NativeNewStageLooks.Lamp,false,0xFF3020);
            }
            foreach(var b in all)if(Starts(b.Id,"rig-crane-"))
            {
                double sx=Math.Sign(b.Cx),sz=Math.Sign(b.Cz),y=b.Y1;Box(b.X0+.2,b.X1-.2,y,y+.9,b.Z0+.2,b.Z1-.2,NativeNewStageLooks.Crane);Box(b.Cx-sx*1.1-.6,b.Cx-sx*1.1+.6,y+.9,y+2.5,b.Cz-.7,b.Cz+.7,NativeNewStageLooks.Crane);
                Box(b.Cx-sx*1.1-.62,b.Cx-sx*1.1+.62,y+1.5,y+2.3,b.Cz+sz*.72-.02,b.Cz+sz*.72+.02,NativeNewStageLooks.Glass,false);
                double tx=b.Cx-sx*15,tz=b.Cz-sz*1.5,ty=y+5.5;foreach(double o in new[]{-.5,.5}){Bar(b.Cx,y+1.2,b.Cz+o,tx,ty,tz+o,.18,NativeNewStageLooks.Crane);Bar(b.Cx,y+2.2,b.Cz+o*.6,tx,ty+.6,tz+o*.6,.14,NativeNewStageLooks.Crane);}
                for(double f=.1;f<1;f+=.12){double ax=b.Cx+(tx-b.Cx)*f,ay=y+1.2+(ty-y-1.2)*f,az=b.Cz+(tz-b.Cz)*f;Bar(ax,ay,az-.5,ax,ay+.7,az+.5,.07,NativeNewStageLooks.Crane,false);}
                Bar(tx,ty,tz,tx,ty-3,tz,.04,NativeNewStageLooks.Cable,false);Box(tx-.2,tx+.2,ty-3.5,ty-3,tz-.2,tz+.2,NativeNewStageLooks.Crane,false);
            }
        }

        // ---------------- Polaris Station ----------------
        private void Polar(List<Bx> all)
        {
            foreach(var b in all)
            {
                string id=b.Id;On(b);
                if(Starts(id,"polar-module-"))
                {
                    Container(b,1,false);bool alongX=b.Sx>=b.Sz;
                    foreach(int s in alongX?new[]{2,3}:new[]{0,1})Windows(b,s,b.Y0+1.6,.7,.9,2.2,.8,b.Index*3+s,.9);
                    int end=alongX?(b.Cx<0?0:1):(b.Cz<0?2:3);Span(b,end,out double a0,out double a1);double m=(a0+a1)/2;Face(b,end,m-.5,m+.5,b.Y0,b.Y0+2.1,NativeNewStageLooks.Door,-1,.016,.012);
                    Face(b,end,m-.18,m+.18,b.Y0+2.25,b.Y0+2.4,NativeNewStageLooks.Light,-1,.15,.15);
                    Face(b,4,b.X0+.05,b.X1-.05,b.Z0+.05,b.Z1-.05,NativeNewStageLooks.Snow,-1,.08,.07);
                    for(int s=0;s<4;s++){Span(b,s,out double c0,out double c1);Face(b,s,c0,c1,b.Y0,b.Y0+.35,NativeNewStageLooks.Frost);}
                }
                else if(Starts(id,"polar-tank-"))
                {
                    for(int s=0;s<4;s++){Span(b,s,out double a0,out double a1);foreach(double y in new[]{.6,1.5,2.4})Face(b,s,a0,a1,b.Y0+y-.04,b.Y0+y+.04,NativeNewStageLooks.Dark);Face(b,s,a0,a1,b.Y0,b.Y0+.3,NativeNewStageLooks.Frost);}
                    Face(b,4,b.X0+.1,b.X1-.1,b.Z0+.1,b.Z1-.1,NativeNewStageLooks.Snow,-1,.1,.09);
                }
                else if(Starts(id,"polar-hut-")||Starts(id,"polar-shed-")||Starts(id,"polar-drill-")&&Has(id,"-house"))
                {
                    for(int s=0;s<4;s++)Windows(b,s,b.Y0+1.5,.6,.7,1.6,.7,b.Index*5+s,.6);Face(b,4,b.X0+.05,b.X1-.05,b.Z0+.05,b.Z1-.05,NativeNewStageLooks.Snow,-1,.1,.09);
                    int d=b.Cz>0?3:2;Span(b,d,out double a0,out double a1);Face(b,d,(a0+a1)/2-.45,(a0+a1)/2+.45,b.Y0,b.Y0+2.0,NativeNewStageLooks.Door,-1,.016,.012);
                }
                else if(Starts(id,"polar-garage-")&&Has(id,"-roof")){Face(b,4,b.X0,b.X1,b.Z0,b.Z1,NativeNewStageLooks.Snow,-1,.12,.11);for(double z=b.Z0+2;z<b.Z1-1;z+=3.5)Box(b.Cx-1.2,b.Cx+1.2,b.Y0-.05,b.Y0,z-.15,z+.15,NativeNewStageLooks.Light,false);}
                else if(Starts(id,"polar-garage-")&&(Has(id,"-wall-")||Has(id,"-header"))){for(int s=0;s<4;s++){Span(b,s,out double a0,out double a1);if(a1-a0<2)continue;for(double u=a0+1;u<a1;u+=2)Face(b,s,u-.04,u+.04,b.Y0,b.Y1,NativeNewStageLooks.Dark,-1,Decal,.006);Face(b,s,a0,a1,b.Y0,b.Y0+.4,NativeNewStageLooks.Frost);}}
                else if(Starts(id,"polar-garage-")&&(Has(id,"-loader")||Has(id,"-tractor"))||Starts(id,"polar-snowcat-")&&!Has(id,"-blade")&&!Has(id,"-sled")||Starts(id,"polar-bowser-"))
                {
                    for(int s=0;s<4;s++){Span(b,s,out double a0,out double a1);Face(b,s,a0,a1,b.Y0,b.Y0+.75,NativeNewStageLooks.Wheel);if(a1-a0>1.8)Face(b,s,a0+.3,a1-.3,b.Y0+1.3,b.Y1-.25,NativeNewStageLooks.GlassDark);}
                    Face(b,4,b.X0+.2,b.X1-.2,b.Cz-.12,b.Cz+.12,NativeNewStageLooks.Lamp,0xFFB020,.06,.05);
                }
                else if(Starts(id,"polar-hall-wall-")||Starts(id,"polar-hall-porch-"))for(int s=0;s<4;s++){Span(b,s,out double a0,out double a1);Face(b,s,a0,a1,b.Y0,b.Y0+.4,NativeNewStageLooks.Frost);Face(b,s,a0,a1,b.Y1-.15,b.Y1,NativeNewStageLooks.Steel,-1,.02,.012);}
                else if(id=="polar-radome")for(int s=0;s<4;s++){Span(b,s,out double a0,out double a1);for(double u=a0+1.5;u<a1-.5;u+=1.5)Face(b,s,u-.03,u+.03,b.Y0,b.Y1,NativeNewStageLooks.Dark,-1,Decal,.006);Face(b,s,a0,a1,b.Y1-.3,b.Y1,NativeNewStageLooks.Steel,-1,.02,.012);Face(b,s,(a0+a1)/2-2,(a0+a1)/2+2,b.Y0+1.2,b.Y0+2.2,NativeNewStageLooks.Sign,-1,.02,.012);}
                else if(id=="polar-hall-roof")Face(b,4,b.X0,b.X1,b.Z0,b.Z1,NativeNewStageLooks.Snow,-1,.1,.09);
                else if(Starts(id,"polar-drill-")&&Has(id,"-mast"))Mast(b,1.6);
                else if(Starts(id,"polar-rail-"))RailBlock(b);
                else if(Starts(id,"polar-crate-"))Face(b,4,b.X0+.05,b.X1-.05,b.Z0+.05,b.Z1-.05,NativeNewStageLooks.Snow,-1,.06,.05);
            }
            // The radome's dome over the radome block's unstandable roof (overhead): latitude rings of panels with dark seams.
            if(Find(all,"polar-radome") is Bx r)
            {
                Above();double cx=NativeNewStageLooks.DomeX,cz=NativeNewStageLooks.DomeZ,cy=r.Y1,R=NativeNewStageLooks.DomeRadius;
                Box(cx-R,cx+R,cy,cy+.35,cz-R*.42,cz+R*.42,NativeNewStageLooks.White);Box(cx-R*.42,cx+R*.42,cy,cy+.35,cz-R,cz+R,NativeNewStageLooks.White);
                for(int ring=0;ring<6;ring++)
                {
                    double a0=ring*Math.PI/12,a1=(ring+1)*Math.PI/12,rr=R*Math.Cos((a0+a1)/2),yy=cy+.35+R*Math.Sin((a0+a1)/2);int seg=Math.Max(4,(int)Math.Round(16*Math.Cos((a0+a1)/2)));
                    double tall=R*(Math.Sin(a1)-Math.Sin(a0))+.06,wide=2*rr*Math.Tan(Math.PI/seg)*1.02,lean=(a0+a1)/2;
                    for(int k=0;k<seg;k++){double yaw=(k+(ring&1)*.5)*2*Math.PI/seg;var q=FactoryQ.YawPitch(yaw,-lean);Add(cx+Math.Sin(yaw)*rr,yy,cz+Math.Cos(yaw)*rr,wide/2,tall/2/Math.Max(.2,Math.Cos(lean)),.06,q,NativeNewStageLooks.White,true,Shade(0xE6E6E0,.94+.08*H(ring,k,77)));}
                }
                Box(cx-.25,cx+.25,cy+R+.3,cy+R+.9,cz-.25,cz+.25,NativeNewStageLooks.Steel);Box(cx-.16,cx+.16,cy+R+.9,cy+R+1.2,cz-.16,cz+.16,NativeNewStageLooks.Lamp,false,0xFF3020);
            }
        }
    }
}
