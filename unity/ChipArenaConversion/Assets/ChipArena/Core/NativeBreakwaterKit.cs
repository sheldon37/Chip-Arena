using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Breakwater's static box kit (docs/arenas/BREAKWATER.md "The look"): what NativeBlockLit instances draw on the map's blocks
    /// by id stem when the stage bank is absent (CI, a build without the Drive zip); with the bank the modelled art draws in its
    /// place (docs/arenas/STAGE-ART.md). Two kinds of part:
    /// <list type="bullet">
    /// <item>Attached (<see cref="Parent"/> the block, <see cref="Overhead"/> false): copings, courses and joints, buttresses,
    /// stair nosings, door frames and storm lamps, the bollards' heads, the Stone's plinth and plaque. Every attached part stays
    /// inside its block grown by <see cref="Proud"/> (the stage rule: what looks like cover is cover).</item>
    /// <item>Overhead (<see cref="Overhead"/> true): what stands on a block nobody can stand on (the turret's belfry and roof, the
    /// Stone's obelisk, the end buildings' slate roofs and the gate's flagstaff: their blocks carry a pilot column) or hangs at
    /// least <see cref="Clearance"/> m over the floor beside its block (the lamp posts' brackets).</item>
    /// </list>
    /// Deterministic; allocates only while building (Runtime packs it once per map).
    /// </summary>
    public sealed class NativeBreakwaterKit
    {
        public const float Proud=.25f;public const double Clearance=2.0,Decal=.012;
        public readonly NativeFactoryKit.Writer Parts;public int[] Parent=Array.Empty<int>();public bool[] Overhead=Array.Empty<bool>();
        private readonly List<int> parents=new List<int>();private readonly List<bool> overheads=new List<bool>();private int current=-1;private bool over;
        public int Count=>Parts.Count;
        private NativeBreakwaterKit(NativeFactoryKit.Looks looks){Parts=new NativeFactoryKit.Writer(1024,true,looks);}
        private void Track(int before){for(int i=before;i<Parts.Count;i++){parents.Add(current);overheads.Add(over);}}
        private void Add(double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=true){int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow);Track(n);}
        private void Box(double x0,double x1,double y0,double y1,double z0,double z1,int kind,bool shadow=true){int n=Parts.Count;Parts.Box(x0,x1,y0,y1,z0,z1,kind,shadow);Track(n);}
        private void Bar(double x0,double y0,double z0,double x1,double y1,double z1,double t,int kind,bool shadow=true){int n=Parts.Count;Parts.Bar(x0,y0,z0,x1,y1,z1,t,t,kind,shadow);Track(n);}
        private static double H(int a,int b=0,int c=0)=>NativeBreakwaterLooks.Hash(a,b,c);
        private static bool Starts(string id,string p)=>id!=null&&id.StartsWith(p,StringComparison.Ordinal);
        private static bool Has(string id,string p)=>id!=null&&id.IndexOf(p,StringComparison.Ordinal)>=0;
        private struct Bx {public double X0,X1,Y0,Y1,Z0,Z1;public string Id;public int Index;public double Sx=>X1-X0;public double Sy=>Y1-Y0;public double Sz=>Z1-Z0;public double Cx=>(X0+X1)/2;public double Cz=>(Z0+Z1)/2;public double Cy=>(Y0+Y1)/2;}
        private const int Granite=NativeBreakwaterLooks.Granite,Dark=NativeBreakwaterLooks.GraniteDark,Coping=NativeBreakwaterLooks.Coping,Wet=NativeBreakwaterLooks.Wet,Iron=NativeBreakwaterLooks.Iron,Rust=NativeBreakwaterLooks.Rust,
            Timber=NativeBreakwaterLooks.Timber,Rope=NativeBreakwaterLooks.Rope,Lamp=NativeBreakwaterLooks.Lamp,Glass=NativeBreakwaterLooks.Glass,Brass=NativeBreakwaterLooks.Brass,Lime=NativeBreakwaterLooks.Lime,
            Band=NativeBreakwaterLooks.Band,Copper=NativeBreakwaterLooks.Copper,Slate=NativeBreakwaterLooks.Slate;

        /// <summary>The map's kit for its look (null for another map).</summary>
        public static NativeBreakwaterKit Build(ContentMap map,MapLook look)
        {
            if(!NativeBreakwaterLooks.Active(map?.id))return null;var k=new NativeBreakwaterKit(NativeBreakwaterLooks.Resolve(look?.kitLooks));
            for(int i=0;i<(map.blocks?.Length??0);i++)
            {
                var b=map.blocks[i];if(b?.id==null||b.role!="static")continue;
                var x=new Bx{X0=b.pos.x-b.size.x/2,X1=b.pos.x+b.size.x/2,Y0=b.pos.y-b.size.y/2,Y1=b.pos.y+b.size.y/2,Z0=b.pos.z-b.size.z/2,Z1=b.pos.z+b.size.z/2,Id=b.id,Index=i};
                k.current=i;k.over=false;k.Dress(x);
            }
            k.Parent=k.parents.ToArray();k.Overhead=k.overheads.ToArray();return k;
        }

        /// <summary>A plate on face `side` (0 +x, 1 -x, 2 +z, 3 -z, 4 top): u0..u1 along it (z on ±x faces, x on ±z faces and the
        /// top), v0..v1 up it (y; z on the top), `t` thick from `o` proud.</summary>
        private void Face(Bx b,int side,double u0,double u1,double v0,double v1,int kind,double t=Decal,double o=.004,bool shadow=false)
        {
            switch(side)
            {
                case 0:Box(b.X1+o-t,b.X1+o,v0,v1,u0,u1,kind,shadow);break;case 1:Box(b.X0-o,b.X0-o+t,v0,v1,u0,u1,kind,shadow);break;
                case 2:Box(u0,u1,v0,v1,b.Z1+o-t,b.Z1+o,kind,shadow);break;case 3:Box(u0,u1,v0,v1,b.Z0-o,b.Z0-o+t,kind,shadow);break;
                default:Box(u0,u1,b.Y1+o-t,b.Y1+o,v0,v1,kind,shadow);break;
            }
        }
        // Ashlar courses on a vertical face: a dark bed joint every `course` m between y0 and y1.
        private void Courses(Bx b,int side,double y0,double y1,double course=.6)
        {
            double u0=side<=1?b.Z0:b.X0,u1=side<=1?b.Z1:b.X1;for(double y=y0+course;y<y1-.1;y+=course)Face(b,side,u0+.02,u1-.02,y-.015,y+.015,Dark,.008,.003);
        }

        private void Dress(Bx b)
        {
            string id=b.Id;
            if(id=="breakwater-deck"){Deck(b);return;}
            if(Starts(id,"breakwater-wall-")){Wall(b);return;}
            if(Starts(id,"breakwater-quay-")||id=="breakwater-ledge"){Quay(b);return;}
            if(id=="breakwater-bastion"){Face(b,4,b.X0,b.X1,b.Z0,b.Z0+.4,Coping,.05,.05,true);Courses(b,3,NativeBreakwaterLooks.QuayY,b.Y1);return;}
            if(id=="breakwater-pulpit"){Face(b,4,b.X0,b.X1,b.Z0,b.Z0+.3,Coping,.05,.05,true);Courses(b,3,b.Y0,b.Y1);Face(b,3,-.6,.6,.9,1.6,Brass,.03,.03);return;}
            if(id=="breakwater-turret"){Turret(b);return;}
            if(id=="breakwater-stone"){Stone(b);return;}
            if(Starts(id,"breakwater-end-")){End(b);return;}
            if(Starts(id,"breakwater-stair-")){Face(b,4,b.X0,b.X1,b.Z0+.03,b.Z1-.03,Coping,.03,.02);return;}
            if(Starts(id,"breakwater-landing-")){Face(b,4,b.X0,b.X1,b.Z0,b.Z0+.3,Coping,.05,.05,true);return;}
            if(Starts(id,"breakwater-parapet-")){Face(b,4,b.X0-.05,b.X1+.05,b.Z0-.05,b.Z1+.05,Coping,.1,.1,true);Courses(b,3,b.Y0,b.Y1,.37);return;}
            if(Starts(id,"breakwater-blocks-")){Blocks(b);return;}
            if(Starts(id,"breakwater-boat-")){Boat(b);return;}
            if(Starts(id,"breakwater-bollard-")){Box(b.X0-.1,b.X1+.1,b.Y1-.22,b.Y1+.06,b.Z0-.1,b.Z1+.1,Iron);Face(b,2,b.X0+.1,b.X1-.1,b.Y0+.3,b.Y0+.36,Rope,.05,.05);Face(b,3,b.X0+.1,b.X1-.1,b.Y0+.3,b.Y0+.36,Rope,.05,.05);return;}
            if(Starts(id,"breakwater-lamp-")){Post(b);return;}
        }
        private void Deck(Bx b)
        {
            // granite setts: joints across every 2 m and along every 2.5 m, a coping course along the south edge, mooring rings
            for(double x=b.X0+2;x<b.X1-.5;x+=2)Face(b,4,x-.02,x+.02,b.Z0+.4,b.Z1-.05,Dark,.008,.003);
            foreach(double z in new[]{-2.5,0.0,2.5})Face(b,4,b.X0+.05,b.X1-.05,z-.02,z+.02,Dark,.008,.003);
            Face(b,4,b.X0+.03,b.X1-.03,b.Z0+.02,b.Z0+.4,Coping,.04,.03,true);Courses(b,3,NativeBreakwaterLooks.QuayY,b.Y1);
            for(double x=-12;x<=12;x+=8)Face(b,3,x-.12,x+.12,-.75,-.5,Rust,.05,.05);
        }
        private void Wall(Bx b)
        {
            // the wave wall: a coping along both edges of its walk, courses and buttresses on its deck face, weed at the waterline
            Face(b,4,b.X0+.03,b.X1-.03,b.Z0+.02,b.Z0+.35,Coping,.04,.03,true);Face(b,4,b.X0+.03,b.X1-.03,b.Z1-.78,b.Z1-.43,Coping,.04,.03,true);
            Courses(b,3,0,b.Y1);Courses(b,2,NativeBreakwaterLooks.SeaY,b.Y1,.8);
            for(double x=b.X0+2.2;x<b.X1-1;x+=4.4)Face(b,3,x-.3,x+.3,0,b.Y1-.3,Granite,.14,.14,true);
            Face(b,2,b.X0,b.X1,NativeBreakwaterLooks.SeaY-.4,NativeBreakwaterLooks.SeaY+.9,Wet,.02,.02);
        }
        private void Quay(Bx b)
        {
            // the quay: its coping on the harbour edge, timber fenders down its face, an iron ladder
            Face(b,4,b.X0+.03,b.X1-.03,b.Z0+.02,b.Z0+.4,Coping,.04,.03,true);Courses(b,3,NativeBreakwaterLooks.SeaY,b.Y1);
            for(double x=b.X0+1.5;x<b.X1-.5;x+=3)Face(b,3,x-.12,x+.12,NativeBreakwaterLooks.SeaY-.6,b.Y1-.1,Timber,.2,.2,true);
            Face(b,3,b.X0,b.X1,NativeBreakwaterLooks.SeaY-.4,NativeBreakwaterLooks.SeaY+.7,Wet,.02,.02);
        }
        private void Turret(Bx b)
        {
            // string courses, lit slits toward the deck, the corbel table; over it the open belfry's four piers and the slate cone
            foreach(double y in new[]{NativeBreakwaterLooks.WalkY+.1,4.4,b.Y1-.3})foreach(int s in new[]{0,1,2,3}){double u0=s<=1?b.Z0+.02:b.X0,u1=s<=1?b.Z1-.02:b.X1;Face(b,s,u0,u1,y,y+.2,Coping,.1,.1,true);}
            Courses(b,3,0,b.Y1);Courses(b,0,NativeBreakwaterLooks.WalkY,b.Y1);Courses(b,1,NativeBreakwaterLooks.WalkY,b.Y1);
            foreach(double x in new[]{-.7,.7})Face(b,3,x-.12,x+.12,4.9,5.8,Glass,.03,.01);
            over=true;double cx=b.Cx,cz=b.Cz,top=b.Y1;
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})Box(cx+sx*1.25-.2,cx+sx*1.25+.2,top,top+1.8,cz+sz*1.3-.2,cz+sz*1.3+.2,Granite);
            Box(cx-1.7,cx+1.7,top+1.8,top+2.05,cz-1.8,cz+1.8,Coping);
            for(int k=0;k<5;k++){double r=1.6-k*.34,y=top+2.05+k*.55;Box(cx-r,cx+r,y,y+.55,cz-r-.05,cz+r+.05,Slate);}
            Box(cx-.04,cx+.04,top+4.8,top+5.9,cz-.04,cz+.04,Iron);
        }
        private void Stone(Bx b)
        {
            // the Mariners' Stone: a stepped plinth, a bronze plaque on each long face; over it the obelisk and its lantern
            foreach(int s in new[]{0,1,2,3}){double u0=s<=1?b.Z0:b.X0,u1=s<=1?b.Z1:b.X1;Face(b,s,u0,u1,b.Y0,b.Y0+.35,Coping,.2,.2,true);Face(b,s,u0,u1,b.Y1-.3,b.Y1,Coping,.14,.14,true);}
            Face(b,0,-.7,.7,1.1,2.2,Brass,.03,.03);Face(b,1,-.7,.7,1.1,2.2,Brass,.03,.03);Courses(b,2,b.Y0+.35,b.Y1-.3,.7);Courses(b,3,b.Y0+.35,b.Y1-.3,.7);
            over=true;for(int k=0;k<5;k++){double r=.62-k*.09,y=b.Y1+k*.6;Box(-r,r,y,y+.6,-r,r,Granite);}
            Box(-.16,.16,b.Y1+3,b.Y1+3.34,-.16,.16,Lamp,false);Box(-.24,.24,b.Y1+3.34,b.Y1+3.42,-.24,.24,Iron);
        }
        private void End(Bx b)
        {
            bool east=b.Cx>0;string id=b.Id;int wall=east?Lime:Granite;
            if(Has(id,"-roof"))
            {
                // the slate roof: two pitches along z over the hall (the lighthouse's hall: a flat lead roof under the tower's foot)
                Face(b,4,b.X0,b.X1,b.Z0,b.Z1,Slate,.03,.02);over=true;
                if(east){Box(b.X0-.15,b.X1,b.Y1,b.Y1+.5,b.Z0-.15,b.Z1+.15,Coping);for(double z=b.Z0+.4;z<b.Z1;z+=1.3)Box(b.X0-.15,b.X0+.35,b.Y1+.5,b.Y1+1.0,z,z+.7,Coping);}
                else
                {
                    for(int k=0;k<6;k++){double zin=k*.8,y=b.Y1+k*.42;Box(b.X0-.2,b.X1+.25,y,y+.42,b.Z0-.25+zin,b.Z1+.25-zin,Slate);}
                    Box(b.Cx-.06,b.Cx+.06,b.Y1+2.5,b.Y1+6.2,-.06,.06,Iron);
                }
                return;
            }
            if(Has(id,"-wing-")){Courses(b,east?1:0,b.Y0,b.Y1);Face(b,4,b.X0,b.X1,b.Z0,b.Z1,Coping,.1,.1,true);return;}
            if(Has(id,"-front-")||Has(id,"-lintel-"))
            {
                int s=east?1:0;Courses(b,s,b.Y0,b.Y1);Face(b,s,b.Z0,b.Z1,4.5,4.8,east?Band:Coping,.1,.1,true);
                if(Has(id,"-lintel-")){Face(b,s,b.Z0,b.Z1,b.Y0,b.Y0+.22,Coping,.12,.12,true);Face(b,s,b.Cz-.14,b.Cz+.14,b.Y0+.5,b.Y0+.9,Lamp,.14,.14);}
                else if(Has(id,"-front-1"))Face(b,s,b.Z0+.2,b.Z1-.2,1.2,2.4,east?Band:Brass,.03,.03);
                return;
            }
            if(Has(id,"-north")||Has(id,"-south")){Courses(b,Has(id,"-north")?3:2,b.Y0,b.Y1);return;}
            if(Has(id,"-back")){int s=east?1:0;Courses(b,s,b.Y0,b.Y1);for(double z=-1.2;z<=1.21;z+=.4)Face(b,s,z-.05,z+.05,.1,3.2,Iron,.06,.06);Face(b,s,-1.5,1.5,3.2,3.5,wall==Lime?Band:Coping,.1,.1,true);return;}
            if(Has(id,"-mid")){Courses(b,2,b.Y0,b.Y1);Courses(b,3,b.Y0,b.Y1);return;}
        }
        private void Blocks(Bx b)
        {
            // quarried blocks stacked dry: joints between the stones, an iron lewis ring on top
            double course=b.Sy>2?1.3:b.Sy;foreach(int s in new[]{0,1,2,3})
            {
                double u0=s<=1?b.Z0:b.X0,u1=s<=1?b.Z1:b.X1;for(double y=b.Y0+course;y<b.Y1-.2;y+=course)Face(b,s,u0,u1,y-.03,y+.03,Dark,.012,.004);
                Face(b,s,(u0+u1)/2-.03,(u0+u1)/2+.03,b.Y0,Math.Min(b.Y1,b.Y0+course)-.03,Dark,.012,.004);
            }
            Box(b.Cx-.1,b.Cx+.1,b.Y1,b.Y1+.14,b.Cz-.03,b.Cz+.03,Rust);
        }
        private void Boat(Bx b)
        {
            // an upturned boat on its trestles: the keel, strakes along the hull, the trestles' legs
            Face(b,4,b.X0+.1,b.X1-.1,b.Cz-.05,b.Cz+.05,Iron,.06,.06,true);
            foreach(int s in new[]{2,3})foreach(double y in new[]{.45,.75})Face(b,s,b.X0+.05,b.X1-.05,b.Y0+y-.02,b.Y0+y+.02,Rope,.012,.004);
            foreach(double x in new[]{b.X0+.4,b.X1-.4})foreach(int s in new[]{2,3})Face(b,s,x-.06,x+.06,b.Y0,b.Y0+.4,Timber,.08,.08,true);
        }
        private void Post(Bx b)
        {
            // a cast-iron lamp post: its base, a collar, and (overhead) the bracket the lantern swings from
            Box(b.X0-.08,b.X1+.08,b.Y0,b.Y0+.3,b.Z0-.08,b.Z1+.08,Iron);Box(b.X0-.05,b.X1+.05,b.Y0+1.6,b.Y0+1.7,b.Z0-.05,b.Z1+.05,Brass);
            over=true;NativeBreakwaterRig.Bracket(b.Cx,b.Y1,b.Cz,out double px,out double py,out double pz);
            Bar(b.Cx,b.Y1-.12,b.Cz,px,py+.06,pz,.07,Iron);Bar(b.Cx,b.Y1-.7,b.Cz,(b.Cx+px)/2,py+.03,(b.Cz+pz)/2,.04,Iron);Box(b.X0+.08,b.X1-.08,b.Y1,b.Y1+.18,b.Z0+.08,b.Z1-.08,Brass);
        }
    }

    /// <summary>
    /// Breakwater's rig for one frame (docs/arenas/BREAKWATER.md): the parts that move all match long, from the clock alone (the
    /// lighthouse's optic turning in its lantern, the lanterns swinging on their brackets, the turret's storm bell, the swells
    /// running in on the wave wall, the bell buoy and the boats riding the sea, the gulls, the flags) and the wash from the
    /// replicated hazard state (the comber gathering through the telegraph, the water running across the mole). Allocation free
    /// per frame (a fixed-capacity writer).
    /// </summary>
    public sealed class NativeBreakwaterRig
    {
        public const int Capacity=640;
        public readonly NativeFactoryKit.Writer Parts;
        /// <summary>The stage art's view of the frame's parts (docs/arenas/STAGE-ART.md; Core NativeBreakwaterArt reads them): for
        /// part i its rig slot (-1: the modelled art has no piece of its own for it) and a level (a lamp's light).</summary>
        public readonly int[] Slot=new int[Capacity];public readonly double[] Level=new double[Capacity];
        private void Put(int slot,double level,double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=false)
        {int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow);if(Parts.Count>n){Slot[n]=slot;Level[n]=level;}}
        private readonly double[] posts;private readonly int postCount;
        /// <summary>The lamp posts (their axes and tops) and, after <see cref="Build"/>, each lantern's place this frame.</summary>
        public int Posts=>postCount;public double PostX(int i)=>posts[i*3];public double PostTop(int i)=>posts[i*3+1];public double PostZ(int i)=>posts[i*3+2];
        public readonly double[] Lantern;
        /// <summary>After <see cref="Build"/>: the lamp's heading, the bell's swing (rad) and the strongest wash telegraph and wash.</summary>
        public double Heading,BellSwing,Warn,Washing;
        public NativeBreakwaterRig(NativeFactoryKit.Looks looks,ContentMap map)
        {
            Parts=new NativeFactoryKit.Writer(Capacity,false,looks);var p=new List<double>();
            foreach(var k in map?.blocks??Array.Empty<ContentBlock>())if(k?.id!=null&&k.id.StartsWith("breakwater-lamp-",StringComparison.Ordinal)){p.Add(k.pos.x);p.Add(k.pos.y+k.size.y/2);p.Add(k.pos.z);}
            posts=p.ToArray();postCount=posts.Length/3;Lantern=new double[postCount*3];
        }
        private const int Iron=NativeBreakwaterLooks.Iron,Lamp=NativeBreakwaterLooks.Lamp,Glass=NativeBreakwaterLooks.Glass,Brass=NativeBreakwaterLooks.Brass,Sea=NativeBreakwaterLooks.Sea,Foam=NativeBreakwaterLooks.Foam,
            Gull=NativeBreakwaterLooks.Gull,Hull=NativeBreakwaterLooks.Hull,Red=NativeBreakwaterLooks.Red,Flag=NativeBreakwaterLooks.Flag,Timber=NativeBreakwaterLooks.Timber,Beam=NativeBreakwaterLooks.Beam;

        /// <summary>The bracket's tip a post's lantern hangs from: out over the floor beside it (toward the mole's middle).</summary>
        public static void Bracket(double x,double top,double z,out double px,out double py,out double pz){px=x;py=top-.1;pz=z+(z>1?-.6:.6);}
        /// <summary>How far under its pivot a lantern's middle hangs (m).</summary>
        public const double LanternDrop=.42;
        /// <summary>The storm bell in the turret's belfry and the flagstaffs' heads (the gate's and the turret's).</summary>
        public const double BellX=0,BellY=7.9,BellZ=6.7,BellDrop=.45,GateFlagX=-19.25,GateFlagY=11.3,GateFlagZ=0,TurretFlagY=12.3;
        /// <summary>The bell's swing (rad): it tolls through a wash's telegraph and rings down after it; a breath of sway otherwise.</summary>
        public static double Bell(double t,double warn,double after){double k=warn>0?1:after<3?1-after/3:0;return .05*Math.Sin(t*1.1)+k*.62*Math.Sin(t*4.4);}

        /// <summary>Fills <see cref="Parts"/> for the hazards and the replicated state (round, phase, FightAt) at field clock `now`
        /// and render clock `render`.</summary>
        public void Build(MapHazard[] hazards,int round,NativePhase phase,double fightAt,double now,double render)
        {
            Parts.Clear();double clock=NativeFactoryHazards.Clock(phase,fightAt,now),t=render;Warn=Washing=0;double after=double.PositiveInfinity;
            Optic(t);
            for(int i=0;i<(hazards?.Length??0);i++)
            {
                var h=hazards[i];if(h==null||h.kind!=NativeFactoryHazards.Vent)continue;
                NativeBreakwaterLooks.WashAt(h,round,clock,out double warn,out double wash,out double level,out double aft);Warn=Math.Max(Warn,warn);Washing=Math.Max(Washing,level);after=Math.Min(after,aft);
                WashRig(h,warn,wash,level,t);
            }
            Lanterns(hazards,round,clock,t);
            BellSwing=Bell(t,Warn,Washing>0?0:after);
            {var q=FactoryQ.Axis(1,0,0,BellSwing);q.Rotate(0,-BellDrop,0,out double ox,out double oy,out double oz);Put(NativeBreakwaterArt.Bell,0,BellX+ox,BellY+oy,BellZ+oz,.34,.36,.34,q,Brass,true);}
            Swells(t);Floats(t);Gulls(t);Flags(t);
        }
        private void Optic(double t)
        {
            // the lens turning in the lantern: the lamp at its heart, two bull's-eye panels facing the beams, the brass frame
            Heading=NativeBreakwaterLooks.BeamHeading(t);double x=NativeBreakwaterLooks.LightX,y=NativeBreakwaterLooks.LanternY,z=NativeBreakwaterLooks.LightZ;var q=FactoryQ.Axis(0,1,0,Heading);
            Put(NativeBreakwaterArt.Optic,1,x,y,z,.42,.62,.42,q,Lamp);
            for(int s=-1;s<=1;s+=2){q.Rotate(0,0,s*.78,out double ox,out double oy,out double oz);Put(-1,0,x+ox,y+oy,z+oz,.6,.7,.05,q,Glass);}
            Put(-1,0,x,y-.76,z,.72,.07,.72,q,Brass);Put(-1,0,x,y+.76,z,.72,.07,.72,q,Brass);
        }
        private void Lanterns(MapHazard[] hazards,int round,double clock,double t)
        {
            for(int i=0;i<postCount;i++)
            {
                double x=PostX(i),z=PostZ(i);Bracket(x,PostTop(i),z,out double px,out double py,out double pz);double gust=0;
                for(int k=0;k<(hazards?.Length??0);k++){var h=hazards[k];if(h==null||h.kind!=NativeFactoryHazards.Vent||Math.Abs(x-h.a.x)>h.size.x/2+3)continue;NativeBreakwaterLooks.WashAt(h,round,clock,out double w,out _,out double lv,out _);gust=Math.Max(gust,Math.Max(w*.6,lv));}
                double a=NativeBreakwaterLooks.LanternSwing(i,t,gust),b=.5*NativeBreakwaterLooks.LanternSwing(i+7,t*.8,gust*.5);var q=FactoryQ.Axis(1,0,0,a)*FactoryQ.Axis(0,0,1,b);
                q.Rotate(0,-LanternDrop,0,out double ox,out double oy,out double oz);double lx=px+ox,ly=py+oy,lz=pz+oz;Lantern[i*3]=lx;Lantern[i*3+1]=ly;Lantern[i*3+2]=lz;
                Put(NativeBreakwaterArt.Lantern,.92+.08*Math.Sin(t*11+i*3),lx,ly,lz,.11,.15,.11,q,Lamp);
                q.Rotate(0,.19,0,out ox,out oy,out oz);Put(-1,0,lx+ox,ly+oy,lz+oz,.15,.03,.15,q,Iron);q.Rotate(0,LanternDrop*.62,0,out ox,out oy,out oz);Put(-1,0,lx+ox,ly+oy,lz+oz,.012,LanternDrop*.36,.012,q,Iron);
            }
        }
        private void WashRig(MapHazard h,double warn,double wash,double level,double t)
        {
            if(!(warn>0)&&wash<0)return;double x=h.a.x,half=h.size.x/2,sea=NativeBreakwaterLooks.SeaY;
            // the comber: the sea heaping up and running in on the breach through the telegraph, standing over it through the wash
            NativeBreakwaterLooks.Comber(warn,wash,out double cz,out double ch);
            Put(NativeBreakwaterArt.Comber,0,x,sea+ch/2,cz,half+1.2,ch/2,1.5+.5*warn,FactoryQ.Identity,Sea,true);Put(-1,0,x,sea+ch-.08,cz-.5,half+1.0,.12,.5,FactoryQ.Identity,Foam);
            if(wash<0)return;
            // the water on the mole: a sheet on each floor from its north edge to the front, and the falls down the two faces
            double front=NativeBreakwaterLooks.WashFront(wash),th=.07+.13*level;
            Sheet(x,half,NativeBreakwaterLooks.WalkY,NativeBreakwaterLooks.WallZ,5,front,th*1.4);Sheet(x,half,0,5,-5,front,th);Sheet(x,half,NativeBreakwaterLooks.QuayY,-5,NativeBreakwaterLooks.QuayZ,front,th);
            if(front<5)Put(NativeBreakwaterArt.WashFall,0,x,NativeBreakwaterLooks.WalkY/2,5-th,half,NativeBreakwaterLooks.WalkY/2,th,FactoryQ.Identity,Foam);
            if(front<-5)Put(NativeBreakwaterArt.WashFall,0,x,NativeBreakwaterLooks.QuayY/2,-5-th,half,-NativeBreakwaterLooks.QuayY/2,th,FactoryQ.Identity,Foam);
        }
        private void Sheet(double x,double half,double y,double north,double south,double front,double th)
        {double s=Math.Max(south,front);if(s>=north-.05)return;Put(NativeBreakwaterArt.Wash,0,x,y+th/2+.01,(north+s)/2,half,th/2,(north-s)/2,FactoryQ.Identity,Foam);}
        private void Swells(double t)
        {
            for(int i=0;i<NativeBreakwaterLooks.Swells;i++)
            {
                var s=NativeBreakwaterLooks.SwellAt(i,t);if(s.Height<.05)continue;double depth=1.1+1.3*s.U;
                Put(NativeBreakwaterArt.Swell,s.U,s.X,NativeBreakwaterLooks.SeaY+s.Height/2,s.Z,s.Half,s.Height/2,depth,FactoryQ.Identity,Sea);
                if(s.U>.45)Put(-1,0,s.X,NativeBreakwaterLooks.SeaY+s.Height-.04,s.Z-depth*.3,s.Half*.9,.06,.3,FactoryQ.Identity,Foam);
            }
        }
        private void Floats(double t)
        {
            // the bell buoy: its float, the lattice and the bell, the lamp that flashes red
            double sea=NativeBreakwaterLooks.SeaY;
            {double x=NativeBreakwaterLooks.BuoyX,z=NativeBreakwaterLooks.BuoyZ;NativeBreakwaterLooks.Ride(x,z,t,2.2,out double lift,out double ax,out double az);var q=FactoryQ.Axis(1,0,0,ax)*FactoryQ.Axis(0,0,1,az);
                Put(NativeBreakwaterArt.Buoy,0,x,sea+lift,z,.9,.45,.9,q,Red,true);q.Rotate(0,1.5,0,out double ox,out double oy,out double oz);Put(-1,0,x+ox,sea+lift+oy,z+oz,.35,1.1,.35,q,Iron);
                q.Rotate(0,2.75,0,out ox,out oy,out oz);Put(NativeBreakwaterArt.BuoyLamp,NativeBreakwaterLooks.BuoyLamp(t),x+ox,sea+lift+oy,z+oz,.1,.12,.1,q,Red);}
            // the fishing boat at her mooring and a dinghy by the quay
            {double x=NativeBreakwaterLooks.BoatX,z=NativeBreakwaterLooks.BoatZ;NativeBreakwaterLooks.Ride(x,z,t,1.3,out double lift,out double ax,out double az);var q=FactoryQ.Axis(0,1,0,1.25)*FactoryQ.Axis(1,0,0,ax*.6)*FactoryQ.Axis(0,0,1,az);
                Put(NativeBreakwaterArt.Boat,0,x,sea+lift,z,1.5,.6,4.2,q,Hull,true);q.Rotate(0,1.3,-1.2,out double ox,out double oy,out double oz);Put(-1,0,x+ox,sea+lift+oy,z+oz,.9,.75,1.0,q,Timber);}
            {double x=NativeBreakwaterLooks.DinghyX,z=NativeBreakwaterLooks.DinghyZ;NativeBreakwaterLooks.Ride(x,z,t,1.8,out double lift,out double ax,out double az);var q=FactoryQ.Axis(0,1,0,-.5)*FactoryQ.Axis(1,0,0,ax)*FactoryQ.Axis(0,0,1,az);
                Put(NativeBreakwaterArt.Dinghy,0,x,sea+lift,z,.62,.26,1.5,q,Timber);}
        }
        private void Gulls(double t)
        {
            for(int i=0;i<NativeBreakwaterLooks.Gulls;i++)
            {
                NativeBreakwaterLooks.GullAt(i,t,out double x,out double y,out double z,out double heading,out double bank,out double flap);var q=FactoryQ.Axis(0,1,0,heading)*FactoryQ.Axis(0,0,1,-bank);
                Put(NativeBreakwaterArt.GullBody,0,x,y,z,.07,.06,.24,q,Gull);
                for(int s=-1;s<=1;s+=2)
                {
                    // each wing hinges at the shoulder: its root in the body's frame, lifted by the flap
                    var w=q*FactoryQ.Axis(0,0,1,s*flap);q.Rotate(s*.06,.03,.03,out double rx,out double ry,out double rz);w.Rotate(s*.3,0,0,out double ox,out double oy,out double oz);
                    Put(s<0?NativeBreakwaterArt.GullWingL:NativeBreakwaterArt.GullWingR,0,x+rx+ox,y+ry+oy,z+rz+oz,.3,.012,.1,w,Gull);
                }
            }
        }
        /// <summary>A flag's cloth: `n` lengths streaming south from the staff's head, each `len` long (half lengths in the parts).</summary>
        public const int FlagLengths=3;public const double FlagLength=.62,FlagHeight=.46;
        private void Flags(double t)
        {
            Cloth(GateFlagX,GateFlagY,GateFlagZ,t,0,1);Cloth(BellX,TurretFlagY,BellZ,t,1,.62);
        }
        private void Cloth(double x,double y,double z,double t,int seed,double scale)
        {
            // the wind off the sea streams it south: each length turns a little further about the staff and droops
            double px=x,py=y,pz=z;
            for(int k=0;k<FlagLengths;k++)
            {
                double yaw=.35*Math.Sin(t*5.2-k*1.3+seed*2)+.18*Math.Sin(t*8.1-k*2.1+seed),droop=.1+.07*k+.06*Math.Sin(t*3.1-k+seed),l=FlagLength*scale;
                var q=FactoryQ.Axis(0,1,0,Math.PI+yaw)*FactoryQ.Axis(1,0,0,droop);q.Rotate(0,0,l,out double dx,out double dy,out double dz);
                Put(NativeBreakwaterArt.Flag,scale,px+dx/2,py+dy/2,pz+dz/2,.012,FlagHeight*scale/2,l/2,q,Flag);px+=dx;py+=dy;pz+=dz;
            }
        }
    }
}
