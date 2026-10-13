using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>A point of the map's PvE entries with its height (the floor it stands on; the draft's PvePoint plus y).</summary>
    [Serializable] public sealed class NativeWardPoint {public float x,y,z;}
    /// <summary>
    /// The presentation's read of a Ward Zero PvE entry (docs/arenas/WARDZERO-SIMULATION-CONTRACT.md section 6): today's
    /// PveEntry fields plus the ones the draft adds (the entry's surface, floor level, drop height and clear radius, the song
    /// vent side and pit flag, and for a breach its panel block, box and outward normal). JsonUtility fills it from
    /// PveContent.json's `entries` (absent fields read as their defaults), so it works before and after Astra adds the fields
    /// to PveEntry; nothing here is simulation data.
    /// </summary>
    [Serializable] public sealed class NativeWardEntry
    {
        public int e,level,song=-1;public string id,kind,surface,block;public NativeWardPoint from,exit;public float yaw,height,clear;public bool pit,hulk;public V3 center,size,normal;
        public V3 From=>from==null?default:new V3(from.x,from.y,from.z);
        public V3 Exit=>exit==null?default:new V3(exit.x,exit.y,exit.z);
        /// <summary>The surface a vent opens in ("floor" when the data names none, as today's vents).</summary>
        public string Surface=>string.IsNullOrEmpty(surface)?"floor":surface;
    }
    [Serializable] public sealed class NativeWardEntrySet {public NativeWardEntry[] entries;}

    /// <summary>
    /// One dressing part (presentation only, never sim data): a box in its parent block's rest frame (centre offset from the
    /// block's rest centre in the block's own axes; half extents), drawn through NativeBlockLit like an arena block. `Look`
    /// and `Flags` as NativeBlockLooks.Resolve gives them (a shared bank slot carries NativeCyberCityLooks.Bank), `Color`
    /// sRGB, `Gain` the emission scale (HDR above 1). `Drive` says what animates it (<see cref="NativeWardZeroKit.Drive"/>),
    /// `Hazard` the hazard it reads (index into the hazards Build took), `Entry` the PvE entry (index into its entries).
    /// A moving part (an entry's door, gate, lid or hatch leaf; a shutter slat; buried ink) is offset by (Mx, My, Mz) times its
    /// <see cref="NativeWardZeroKit.Move"/> fraction; a shutter slat hides once its bottom rises past `Clip` (local y).
    /// `Fade`: hidden while it sits between the camera and the pilot (decoration above 2.5 m has no collider). `Detail`: fine
    /// dressing (lettering, rivets, posts, paint lines, ink) the low graphics tier leaves out.
    /// </summary>
    public struct NativeWardZeroPart
    {
        public int Parent,Look,Flags,Color;public float X,Y,Z,Hx,Hy,Hz,Gain,Param,Mx,My,Mz,Clip;public byte Drive;public short Hazard,Entry;public bool Fade,Detail;
    }
    /// <summary>A light source the effects and the practical-light picker read: world place, linear colour, size (m), its kind
    /// (<see cref="NativeWardZeroKit"/> Glow* constants), the floor height under it, a seed, and the hazard or entry it follows.</summary>
    public struct NativeWardGlow {public float X,Y,Z,R,G,B,Size,Floor,Param;public byte Kind;public short Hazard,Entry;}

    /// <summary>
    /// Ward Zero's dressing kit (docs/arenas/WARDZERO_PROPOSAL.md section 10.4): deterministic parts attached to the map's
    /// blocks by id stem (the draft's families, docs/arenas/scripts/wardzero_m1.py), plus the entrances' rigs from the PvE
    /// entries and the hazards' rigs from the hazard definitions. Built once; nothing here collides or enters the network frame.
    /// Below 3 m nothing stands out more than 0.25 m from its block except floor paint and thin trims.
    /// <list type="bullet">
    /// <item>Light: fluorescent troffers under every deck and over the upper floor (they flicker and dim with the mood),
    /// high-bays over the atrium and the double-height halls, rotating emergency beacons, the specimen tanks' fluid.</item>
    /// <item>Walls: the clinical wainscot, cove and chair rail, pipes and cable trays high up, door frames with room signs
    /// read from the blocks behind each door, window frames, warning plates, the Siren's ink veins.</item>
    /// <item>Routes between the floors (never reduced): LED nosings that chase up every stair, ramp side strips, floor arrows
    /// at every foot (UP, cyan-white) and every top (DOWN, warm), gallery edge nosings and rail base strips.</item>
    /// <item>Rooms: cell doors and gates, observation doors, the nurse booth, morgue drawers and slabs, intake lockers and
    /// showers, lab benches, gurneys and carts, control consoles and racks, the decon freezers.</item>
    /// <item>Entrances (from the entries): floor vents with sliding grate lids, wall grilles, ceiling grilles, cell gates that
    /// rise, observation and ward doors that slide, the Spine hatch over the Cradle (5.2 m, the Hulk fits) and the
    /// observation hatches, all opened by the entry's open amount; the breach panels' intact look (hazard frame, braces,
    /// rivets, a red lamp). The burst and its holographic repair are the zombie presentation's.</item>
    /// <item>Hazards: the live-grating bridges (grate glow, energised bars, lamps), the burner grates and the furnaces, the
    /// decon nozzles and beacons, the blast shutters' slats, housings and lamps.</item>
    /// <item>The Cradle: the broken vessel's ring, the drop rings (brute 2.4 m, Hulk 4.5 m), and ink that spreads outward
    /// over both floors with the corruption.</item>
    /// </list>
    /// Pure: the same blocks, hazards and entries give the same parts; <see cref="Drive"/> and <see cref="Move"/> are pure
    /// functions of the part, the hazard states, the entries' open amounts, the mood and the clock.
    /// </summary>
    public sealed class NativeWardZeroKit
    {
        public const byte DriveSteady=0,DriveFluor=1,DriveStrobe=2,DriveRoute=3,DriveInk=4,DriveTank=5,DriveBurner=6,DriveGridLamp=7,DriveGridBar=8,DriveGridDeck=9,DriveGas=10,
            DriveShutterLamp=11,DriveLeaf=12,DriveEntryLamp=13,DriveBreach=14,DriveSlat=15,DriveScreen=16,DriveSign=17,DriveFurnace=18,DriveRouteDown=19;
        public const byte GlowFluor=0,GlowHighBay=1,GlowTank=2,GlowFurnace=3,GlowStrobe=4,GlowSign=5,GlowInk=6,GlowScreen=7,GlowSpine=8;
        public NativeWardZeroPart[] Parts;public NativeWardGlow[] Glows;
        /// <summary>Hazard indices by role (into the array Build took; -1 when absent): the two grids by the sign of their x.</summary>
        public readonly int[] Grids={-1,-1};public int[] Burners=Array.Empty<int>(),Gases=Array.Empty<int>(),Shutters=Array.Empty<int>();
        /// <summary>Entry indices of the drops and breaches (into the entries Build took).</summary>
        public int[] Drops=Array.Empty<int>(),Breaches=Array.Empty<int>();
        /// <summary>The routes between the floors the kit found (stairs from their treads, ramps from their pitched decks): foot
        /// and top in world space, the run's unit direction and the width.</summary>
        public NativeWardRoute[] Routes=Array.Empty<NativeWardRoute>();
        private readonly List<NativeWardZeroPart> parts=new List<NativeWardZeroPart>();private readonly List<NativeWardGlow> glows=new List<NativeWardGlow>();private readonly List<NativeWardRoute> routes=new List<NativeWardRoute>();
        private ContentBlock[] blocks;private MapHazard[] hazards;private NativeWardEntry[] entries;private int parent;private ContentBlock b;private float hx,hy,hz;private short entryTag=-1;private bool detail;

        private const int Bk=NativeCyberCityLooks.Bank;
        private const int LBurn=NativeWardZeroLooks.Burn,LTile=NativeWardZeroLooks.Tile,LPanel=NativeWardZeroLooks.Panel,LGlass=NativeWardZeroLooks.Glass,LLight=NativeWardZeroLooks.Light,LInk=NativeWardZeroLooks.Ink;
        private const int LGrate=NativeCyberCityLooks.Grate,LTube=NativeCyberCityLooks.Tube,LHolo=NativeCyberCityLooks.Holo,LMetal=NativeCyberCityLooks.Metal;
        private const int LSteel=NativeBlockLooks.SteelGreen,LStripe=NativeBlockLooks.Demolition,LPlain=NativeBlockLooks.Plain,LConcrete=NativeBlockLooks.ConcreteBoard,LWindow=NativeBlockLooks.WindowLit;
        private const int Steel=NativeWardZeroLooks.SteelHex,SteelDark=NativeWardZeroLooks.SteelDarkHex,PanelGrey=NativeWardZeroLooks.PanelGreyHex,Teal=NativeWardZeroLooks.TealHex,Fluid=NativeWardZeroLooks.FluidHex,
            Fluor=NativeWardZeroLooks.FluorHex,Red=NativeWardZeroLooks.EmergencyHex,Amber=NativeWardZeroLooks.AmberHex,Ink=NativeWardZeroLooks.InkHex,InkDeep=NativeWardZeroLooks.InkDeepHex,
            Burn=NativeWardZeroLooks.BurnHex,Gas=NativeWardZeroLooks.GasHex,ExitC=NativeWardZeroLooks.ExitHex,Up=NativeWardZeroLooks.UpHex,Down=NativeWardZeroLooks.DownHex,Safe=NativeWardZeroLooks.SafeHex,Void=0x050507;
        /// <summary>The upper floor's top and the roof's underside the draft uses (m): the kit reads heights from the blocks; these
        /// only classify what it finds (a block's base near 0 or near Upper).</summary>
        public const float Upper=6f,DeckUnder=5.6f,Roof=13f;

        // ---------------- hashing (stable across machines) ----------------
        private static uint Mix(uint x){unchecked{x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x;}}
        private static uint Fnv(string s){unchecked{uint h=2166136261;if(s!=null)foreach(char c in s)h=(h^c)*16777619;return h;}}
        public static double Hash(string s,int a=0,int c=0){unchecked{return Mix(Fnv(s)^Mix((uint)a*0x9E3779B9U+(uint)c*0x85EBCA6BU))/4294967296.0;}}
        private double H(int a,int c=0)=>Hash(b.id,a,c);
        private static bool Starts(string s,string p)=>s.StartsWith(p,StringComparison.Ordinal);
        private static bool Has(string s,string p)=>s.IndexOf(p,StringComparison.Ordinal)>=0;
        /// <summary>The id with the map prefix removed, or null for other maps' ids.</summary>
        public static string Stem(string id)=>id!=null&&id.StartsWith("siren-",StringComparison.Ordinal)?id.Substring(6):null;

        /// <summary>Builds the kit for a map's blocks, its hazards and its PvE entries (any may be null).</summary>
        public static NativeWardZeroKit Build(ContentBlock[] blocks,MapHazard[] hazards,NativeWardEntry[] entries)
        {
            var k=new NativeWardZeroKit{blocks=blocks??Array.Empty<ContentBlock>(),hazards=hazards??Array.Empty<MapHazard>(),entries=entries??Array.Empty<NativeWardEntry>()};
            var burn=new List<int>();var gas=new List<int>();var shut=new List<int>();
            for(int i=0;i<k.hazards.Length;i++){var h=k.hazards[i];if(h==null)continue;if(NativeWardZeroHazards.IsGrid(h)){int side=h.center.x<0?0:1;if(k.Grids[side]<0)k.Grids[side]=i;}else if(h.kind==NativeWardZeroHazards.Burner)burn.Add(i);else if(h.kind==NativeWardZeroHazards.Gas)gas.Add(i);else if(h.kind==NativeWardZeroHazards.Shutter)shut.Add(i);}
            k.Burners=burn.ToArray();k.Gases=gas.ToArray();k.Shutters=shut.ToArray();
            var drops=new List<int>();var br=new List<int>();for(int i=0;i<k.entries.Length;i++){var e=k.entries[i];if(e==null)continue;if(e.kind=="drop")drops.Add(i);if(e.kind=="breach")br.Add(i);}k.Drops=drops.ToArray();k.Breaches=br.ToArray();
            k.FindRoutes();
            for(int i=0;i<k.blocks.Length;i++){var bl=k.blocks[i];if(bl?.id==null||Stem(bl.id)==null)continue;k.Use(i);k.Dress();}
            for(int i=0;i<k.entries.Length;i++)if(k.entries[i]!=null)k.DressEntry(i);
            foreach(int h in k.Shutters)k.ShutterRig(h);foreach(int h in k.Gases)k.GasRig(h);
            k.RouteMarks();
            k.Parts=k.parts.ToArray();k.Glows=k.glows.ToArray();k.Routes=k.routes.ToArray();k.parts.Clear();k.glows.Clear();k.routes.Clear();return k;
        }
        private void Use(int i){parent=i;b=blocks[i];hx=b.size.x/2;hy=b.size.y/2;hz=b.size.z/2;entryTag=-1;}
        private float Bottom=>b.pos.y-hy;private float Top=>b.pos.y+hy;
        private static bool Upright(ContentBlock bl)=>bl.rotation==null||Math.Abs(bl.rotation.x)<1e-5&&Math.Abs(bl.rotation.y)<1e-5&&Math.Abs(bl.rotation.z)<1e-5;

        // ---------------- part helpers (parent-local: offsets from the block's rest centre in its own axes) ----------------
        private void P(float x,float y,float z,float sx,float sy,float sz,int look,int flags,int color,float gain=1,byte drive=0,int hazard=-1,float param=0,float mx=0,float my=0,float mz=0,float clip=0,bool fade=true)
        {
            if(!(sx>0&&sy>0&&sz>0))return;
            parts.Add(new NativeWardZeroPart{Parent=parent,X=x,Y=y,Z=z,Hx=sx/2,Hy=sy/2,Hz=sz/2,Look=look,Flags=flags,Color=color,Gain=gain,Drive=drive,Hazard=(short)hazard,Entry=entryTag,Param=param,Mx=mx,My=my,Mz=mz,Clip=clip,Fade=fade&&b.pos.y+y>2.5f,Detail=detail||drive==DriveInk});
        }
        private void Lamp(float x,float y,float z,float sx,float sy,float sz,int color,float gain=2f,byte drive=0,int hazard=-1,float param=0){P(x,y,z,sx,sy,sz,LTube,Bk,color,gain,drive,hazard,param);}
        private void Glow(float x,float y,float z,int color,float size,byte kind,float floor,int hazard=-1,float param=0)
        {glows.Add(new NativeWardGlow{X=b.pos.x+x,Y=b.pos.y+y,Z=b.pos.z+z,R=(float)NativeSceneryBuilder.Channel(color,16),G=(float)NativeSceneryBuilder.Channel(color,8),B=(float)NativeSceneryBuilder.Channel(color,0),Size=size,Floor=floor,Param=param,Kind=kind,Hazard=(short)hazard,Entry=entryTag});}
        // A face of the block: axis 0 (x) or 2 (z), outward sign. u along the face's horizontal tangent (z on an x face, x on a z
        // face; offsets from the block's centre), v the bottom edge above the block's base, w out from the face.
        private void FaceP(int axis,int sign,float u,float v,float w,float width,float height,float depth,int look,int flags,int color,float gain=1,byte drive=0,int hazard=-1,float param=0,float mu=0,float mv=0,float clip=0)
        {
            float y=-hy+v+height/2;
            if(axis==0)P(sign*(hx+w+depth/2),y,u,depth,height,width,look,flags,color,gain,drive,hazard,param,0,mv,mu,clip);
            else P(u,y,sign*(hz+w+depth/2),width,height,depth,look,flags,color,gain,drive,hazard,param,mu,mv,0,clip);
        }
        private float HalfU(int axis)=>axis==0?hz:hx;
        private bool Wallish=>Math.Min(b.size.x,b.size.z)<=.65f&&b.size.y>=.9f&&Upright(b);
        private float BaseLevel(float y)=>y<Upper-1?0:Upper;
        // The world height of the floor a block stands on (0 or the upper floor), for the floor glows under its parts.
        private float FloorUnder=>Bottom<Upper-1?0:Upper;

        // ---------------- the world, for placement ----------------
        // Whether an upright collision block covers (x, z) and overlaps heights y0..y1.
        private bool Solid(float x,float z,float y0,float y1,string skipStem=null)
        {
            foreach(var o in blocks)
            {
                if(o?.id==null||!Upright(o)||o.role=="rubble")continue;float ox=o.size.x/2,oz=o.size.z/2,oy=o.size.y/2;
                if(Math.Abs(x-o.pos.x)>ox||Math.Abs(z-o.pos.z)>oz||o.pos.y+oy<=y0||o.pos.y-oy>=y1)continue;
                var st=Stem(o.id);if(st==null||st=="slab"||st=="vault")continue;if(skipStem!=null&&Starts(st,skipStem))continue;if(Starts(st,"stair-")&&!Has(st,"landing"))continue;return true;
            }
            return false;
        }
        // The top of the floor under (x, z) at about height y (a deck or the slab), or NaN.
        private float FloorAt(float x,float z,float near)
        {
            float best=float.NaN;
            foreach(var o in blocks)
            {
                if(o?.id==null||!Upright(o))continue;float t=o.pos.y+o.size.y/2;if(Math.Abs(t-near)>.45f)continue;
                if(Math.Abs(x-o.pos.x)<=o.size.x/2&&Math.Abs(z-o.pos.z)<=o.size.z/2){var st=Stem(o.id);if(st!=null&&(st=="slab"||Starts(st,"deck-")||Starts(st,"gallery-")||Has(st,"-deck")||Has(st,"-platform")||Has(st,"-specimen")||Has(st,"-link")||Has(st,"-landing")))best=t;}
            }
            return best;
        }
        // The room a point is in, named from the blocks nearest it (signs above doors name the room beyond).
        private string RoomAt(float x,float y,float z)
        {
            string best=null;float near=9;
            foreach(var o in blocks)
            {
                var st=Stem(o?.id);if(st==null||!Upright(o))continue;if(o.pos.y-o.size.y/2>y+2||o.pos.y+o.size.y/2<y-.5f)continue;if(Math.Min(o.size.x,o.size.z)<=.65f&&!Has(st,"-row"))continue;
                string name=Starts(st,"morgue-")?"MORGUE":Starts(st,"cells-")?"CELLS":Starts(st,"obs-")?"OBS":Starts(st,"intake-")?"INTAKE":Starts(st,"lab-")||Starts(st,"ramp-")?"LAB":
                    Starts(st,"lane-")?"FURNACE":Starts(st,"decon-")?"DECON":Starts(st,"control-")?"CONTROL":Starts(st,"cradle-")||Starts(st,"atrium-cart")||Starts(st,"atrium-screen")?"WARD 0":Starts(st,"hall-")||Starts(st,"concourse-")?"HALL":null;
                if(name==null)continue;float dx=Math.Max(0,Math.Abs(x-o.pos.x)-o.size.x/2),dz=Math.Max(0,Math.Abs(z-o.pos.z)-o.size.z/2),d=(float)Math.Sqrt(dx*dx+dz*dz);
                if(d<near){near=d;best=name;}
            }
            return best;
        }

        // ---------------- the 5 x 7 font (Cyber City's), flat on a face ----------------
        private void Word(int axis,int sign,string text,float u,float v,float cell,int color,float gain=1.8f,int look=LTube,int flags=Bk,byte drive=DriveSign,float depth=.03f)
        {
            // Lettering is detail (the low tier leaves it out), except a hazard's warning word (DECON), which stays.
            bool was=detail;detail=drive==DriveSign;
            float width=(text.Length*6-1)*cell,start=u-width/2;bool flip=axis==0?sign<0:sign>0;
            for(int i=0;i<text.Length;i++)foreach(var r in NativeCyberCityKit.Glyph(text[i]))
            {
                float a=start+(i*6+r[0]+r[2]*.5f)*cell,uu=flip?2*u-a:a,vv=v+(7-r[1]-r[3]*.5f)*cell;
                FaceP(axis,sign,uu,vv-r[3]*cell*.5f+cell*.1f,0,r[2]*cell-cell*.2f,r[3]*cell-cell*.2f,depth,look,flags,color,gain,drive);
            }
            detail=was;
        }
        // A sign plate with a word: a dark backing and the lit letters (a room sign, a warning).
        private void Sign(int axis,int sign,float u,float v,string text,int color,float cell=.045f,float w=0)
        {
            float width=(text.Length*6-1)*cell;FaceP(axis,sign,u,v-cell*2,0,Math.Max(w,width+cell*5),cell*11,.025f,LMetal,Bk,SteelDark);
            Word(axis,sign,text,u,v+cell*.5f,cell,color,1.7f,LTube,Bk,DriveSign,.05f);
        }
        // A floor arrow (paint) at (x, z) pointing along (dx, dz) (axis-aligned only), length L: a shaft and three head bars.
        private void Arrow(float x,float y,float z,float dx,float dz,float L,int color,byte drive,float param,float gain=1.5f)
        {
            bool alongX=Math.Abs(dx)>Math.Abs(dz);float s=alongX?Math.Sign(dx):Math.Sign(dz);
            void Bar(float along,float across,float la,float lc){if(alongX)P(x+along*s-b.pos.x,y,z+across-b.pos.z,la,.02f,lc,LTube,Bk,color,gain,drive,-1,param,fade:false);else P(x+across-b.pos.x,y,z+along*s-b.pos.z,lc,.02f,la,LTube,Bk,color,gain,drive,-1,param,fade:false);}
            Bar(-L*.15f,0,L*.7f,.16f);for(int i=0;i<3;i++){float w=.75f-i*.25f;Bar(L*.25f+i*.13f,0,.14f,w);}
        }

        // ---------------- routes between the floors ----------------
        private void FindRoutes()
        {
            // Stairs: the treads of each flight (ids "<flight>-NN"): foot at the lowest tread's down-slope edge, top past the highest.
            var flights=new Dictionary<string,List<int>>();
            for(int i=0;i<blocks.Length;i++){var st=Stem(blocks[i]?.id);if(st==null||!Starts(st,"stair-")||Has(st,"-landing")||Has(st,"-rail"))continue;int d=st.LastIndexOf('-');if(d<0)continue;string f=st.Substring(0,d);if(!int.TryParse(st.Substring(d+1),out _))continue;if(!flights.TryGetValue(f,out var l))flights[f]=l=new List<int>();l.Add(i);}
            var names=new List<string>(flights.Keys);names.Sort(StringComparer.Ordinal);
            foreach(var f in names)
            {
                var l=flights[f];if(l.Count<2)continue;l.Sort((p,q)=>(blocks[p].pos.y+blocks[p].size.y/2).CompareTo(blocks[q].pos.y+blocks[q].size.y/2));
                var lo=blocks[l[0]];var hi=blocks[l[l.Count-1]];float dx=hi.pos.x-lo.pos.x,dz=hi.pos.z-lo.pos.z;bool alongX=Math.Abs(dx)>Math.Abs(dz);float ux=alongX?Math.Sign(dx):0,uz=alongX?0:Math.Sign(dz);
                float run=alongX?lo.size.x:lo.size.z,width=alongX?lo.size.z:lo.size.x;
                routes.Add(new NativeWardRoute{Id="siren-"+f,Stair=true,FootX=lo.pos.x-ux*run/2,FootZ=lo.pos.z-uz*run/2,FootY=lo.pos.y-lo.size.y/2,TopX=hi.pos.x+ux*run/2,TopZ=hi.pos.z+uz*run/2,TopY=hi.pos.y+hi.size.y/2,Ux=ux,Uz=uz,Width=width,Steps=l.Count});
            }
            // Ramps: a pitched deck (rotated about x or z): its top face's ends.
            for(int i=0;i<blocks.Length;i++)
            {
                var o=blocks[i];var st=Stem(o?.id);if(st==null||!Starts(st,"ramp-")||!Has(st,"-deck")||Upright(o))continue;var r=o.rotation;
                bool aboutX=Math.Abs(r.x)>Math.Abs(r.z);float ang=2*(float)Math.Atan2(aboutX?r.x:r.z,r.w);float L=aboutX?o.size.z:o.size.x,t=o.size.y/2;
                // Local top-face ends (0, t, +-L/2) on an x rotation, (+-L/2, t, 0) on a z rotation, turned into the world.
                float c=(float)Math.Cos(ang),s=(float)Math.Sin(ang);
                float ay,az,by,bz;if(aboutX){ay=t*c-(-L/2)*s;az=t*s+(-L/2)*c;by=t*c-(L/2)*s;bz=t*s+(L/2)*c;}else{ay=(-L/2)*s+t*c;az=(-L/2)*c-t*s;by=(L/2)*s+t*c;bz=(L/2)*c-t*s;}
                float x0,z0,x1,z1;if(aboutX){x0=x1=o.pos.x;z0=o.pos.z+az;z1=o.pos.z+bz;}else{z0=z1=o.pos.z;x0=o.pos.x+az;x1=o.pos.x+bz;}
                float y0=o.pos.y+ay,y1=o.pos.y+by;if(y1<y0){float tx=x0;x0=x1;x1=tx;float tz=z0;z0=z1;z1=tz;float ty=y0;y0=y1;y1=ty;}
                float ux=Math.Sign(x1-x0),uz=Math.Sign(z1-z0);if(aboutX)ux=0;else uz=0;
                routes.Add(new NativeWardRoute{Id=o.id,Stair=false,FootX=x0,FootZ=z0,FootY=y0,TopX=x1,TopZ=z1,TopY=y1,Ux=ux,Uz=uz,Width=aboutX?o.size.x:o.size.z,Steps=0});
            }
        }
        // Floor arrows at every foot (UP) and top (DOWN), on the slab or the deck under them; a sign on the nearest wall.
        private void RouteMarks()
        {
            int slab=-1;for(int i=0;i<blocks.Length;i++)if(Stem(blocks[i]?.id)=="slab"){slab=i;break;}
            foreach(var r in routes)
            {
                if(slab>=0){Use(slab);for(int k=0;k<3;k++)Arrow(r.FootX-r.Ux*(1.2f+k*1.1f),Top+.01f,r.FootZ-r.Uz*(1.2f+k*1.1f),r.Ux,r.Uz,1.0f,Up,DriveRoute,1-k*.25f,1.6f);}
                // The top: the deck block under the arrival.
                float tx=r.TopX+r.Ux*1.4f,tz=r.TopZ+r.Uz*1.4f;int deck=-1;
                for(int i=0;i<blocks.Length;i++){var o=blocks[i];var st=Stem(o?.id);if(st==null||!Upright(o))continue;if(Math.Abs(o.pos.y+o.size.y/2-r.TopY)<.2f&&Math.Abs(tx-o.pos.x)<=o.size.x/2&&Math.Abs(tz-o.pos.z)<=o.size.z/2){deck=i;break;}}
                if(deck<0)continue;Use(deck);
                for(int k=0;k<3;k++){float d=1.0f+k*1.1f;float ax=r.TopX+r.Ux*d,az=r.TopZ+r.Uz*d;if(Math.Abs(ax-b.pos.x)>hx-.4f||Math.Abs(az-b.pos.z)>hz-.4f)continue;Arrow(ax,Top+.01f,az,-r.Ux,-r.Uz,1.0f,Down,DriveRouteDown,k*.25f,1.5f);}
            }
        }

        // ---------------- rules by id stem ----------------
        private void Dress()
        {
            string s=Stem(b.id);if(s==null)return;
            if(s=="slab"){Slab();return;}
            if(s=="vault"){Vault();return;}
            if(Starts(s,"breach-")){Breach();return;}
            if(Has(s,"-rail")){Rail(s);return;}
            if(Starts(s,"stair-")){if(Has(s,"-landing"))Landing();else Tread(s);return;}
            if(Starts(s,"ramp-")){if(Has(s,"-deck"))RampDeck();else if(Has(s,"-skirt"))Skirt();else WalkEdge();return;}
            if(Starts(s,"bridge-")){if(Has(s,"-deck"))GridBridge();else WalkEdge();return;}
            if(Starts(s,"gantry-")){Gantry();return;}
            if(Starts(s,"deck-")||Starts(s,"gallery-")){Deck(s);return;}
            if(Starts(s,"cradle-")){Shard();return;}
            if(Has(s,"-row")){CellRow(Starts(s,"obs-"));return;}
            if(Starts(s,"lab-")&&Has(s,"-tank-")){Tank();return;}
            if(Has(s,"-bench")){Bench(Starts(s,"lab-"));return;}
            if(Has(s,"-furnace")){Furnace();return;}
            if(Has(s,"-slab-")){Autopsy();return;}
            if(Has(s,"-chamber-")||Has(s,"-freezer-")){Chamber(Has(s,"-freezer-"));return;}
            if(Has(s,"-lockers")){Lockers();return;}
            if(Has(s,"-shower-")){Shower();return;}
            if(Has(s,"-console")||Has(s,"-desk")){Console(Has(s,"-desk"));return;}
            if(Has(s,"-rack")){Rack();return;}
            if(Has(s,"-station")){Booth();return;}
            if(Has(s,"-gurney-")||Has(s,"-cart")){Gurney(Has(s,"-cart"));return;}
            if(Has(s,"-screen-")){TriageScreen();return;}
            if(Starts(s,"shell-")){Shell();return;}
            if(Wallish)Wall(s);
        }

        // ---------------- floors and ceilings ----------------
        private void Slab()
        {
            float y=hy+.006f;
            // The Cradle: the broken vessel's base ring (5.6 m), the Hulk's landing ring (4.5 m, red) and the brute's (2.4 m, amber).
            for(int i=0;i<96;i++){double a=i*Math.PI*2/96;float r=5.6f;P(r*(float)Math.Sin(a),y+.004f,r*(float)Math.Cos(a),.42f,.02f,.42f,LMetal,Bk,0x3A4448,fade:false);}
            // Dotted rings (axis-aligned dots read as a circle where squares would not).
            for(int i=0;i<64;i++){double a=i*Math.PI*2/64;float r=4.5f;P(r*(float)Math.Sin(a),y,r*(float)Math.Cos(a),.18f,.012f,.18f,LTube,Bk,Red,.9f,DriveSign,fade:false);}
            for(int i=0;i<36;i++){double a=i*Math.PI*2/36;float r=2.4f;P(r*(float)Math.Sin(a),y,r*(float)Math.Cos(a),.15f,.012f,.15f,LTube,Bk,Amber,.9f,DriveSign,fade:false);}
            // The ink: tendrils creeping out from the Cradle (small plates along random walks), each plate shown once the
            // corruption passes its share of the way out (buried under the floor until then).
            for(int t=0;t<26;t++)
            {
                double a=(t+Hash("ink-tendril",t)*.6)*Math.PI*2/26;float x=4.6f*(float)Math.Sin(a),z=4.6f*(float)Math.Cos(a),dist=4.6f;
                for(int k=0;k<16;k++)
                {
                    a+=(Hash("ink-tendril",t,k)-.5)*.9;float step=.45f+.5f*(float)Hash("ink-tendril",t,k+40);x+=step*(float)Math.Sin(a);z+=step*(float)Math.Cos(a);dist+=step;
                    if(Math.Abs(x)>37||Math.Abs(z)>29)break;if(Solid(x,z,.05f,1.6f))break;float w=Math.Max(.08f,.34f-k*.016f)*(.7f+.6f*(float)Hash("ink-tendril",t,k+80));
                    // A vein stroke laid along the walk's main axis (the boxes are axis aligned), tapering outward.
                    float len=step*1.15f;bool alongX=Math.Abs(Math.Sin(a))>Math.Abs(Math.Cos(a));float cx=x-step*.5f*(float)Math.Sin(a),cz=z-step*.5f*(float)Math.Cos(a);
                    P(cx,y+.002f,cz,alongX?len:w,.012f,alongX?w:len,LInk,Bk,InkDeep,1,DriveInk,-1,Math.Min(.97f,dist/30f),0,-.05f,0,fade:false);
                }
            }
            // Pools round the Cradle: three overlapping plates each (a blot, not a tile).
            for(int i=0;i<10;i++){double a=i*Math.PI*2/10+.3;float r=3.2f+(float)Hash("ink-pool",i)*1.2f,px=r*(float)Math.Sin(a),pz=r*(float)Math.Cos(a),sx=.9f+.6f*(float)Hash("ink-pool",i,1),sz=.7f+.5f*(float)Hash("ink-pool",i,2);
                P(px,y+.002f,pz,sx,.012f,sz,LInk,Bk,InkDeep,1,DriveInk,-1,.02f,0,-.05f,0,fade:false);
                P(px+sx*.3f,y+.003f,pz-sz*.25f,sx*.55f,.012f,sz*1.35f,LInk,Bk,InkDeep,1,DriveInk,-1,.03f,0,-.05f,0,fade:false);
                P(px-sx*.25f,y+.003f,pz+sz*.3f,sx*1.3f,.012f,sz*.5f,LInk,Bk,InkDeep,1,DriveInk,-1,.04f,0,-.05f,0,fade:false);}
            // The halls' centre lines, the portals' hazard thresholds, the burner grates.
            HallLines(y);
            foreach(int k in Burners)BurnerGrates(k,y);
            foreach(int k in Shutters){var h=hazards[k];bool alongX=h.size.x>h.size.z;float w=alongX?h.size.x:h.size.z;float c=alongX?h.center.z:h.center.x;
                for(int side=-1;side<=1;side+=2){float o=c+side*.75f;if(alongX)P(h.center.x-b.pos.x,y,o-b.pos.z,w,.012f,1.0f,LStripe,0,0,fade:false);else P(o-b.pos.x,y,h.center.z-b.pos.z,1.0f,.012f,w,LStripe,0,0,fade:false);}}
        }
        private void HallLines(float y)
        {
            // Dashed lane lines on the ground floor's long halls (open floor only).
            for(int side=-1;side<=1;side+=2)for(float x=-36;x<=36;x+=2.4f)
            {float z=side*16.7f,px=x*side;if(Solid(px,z,.05f,2f)||Math.Abs(px)<4)continue;detail=true;P(px,y,z,1.2f,.01f,.12f,LTube,Bk,0xC8CC9A,.35f,fade:false);detail=false;}
        }
        private void BurnerGrates(int k,float y)
        {
            var h=hazards[k];if(h.zones==null)return;
            foreach(var z in h.zones)
            {
                float x0=z.center.x-z.size.x/2,x1=z.center.x+z.size.x/2;int n=Math.Max(1,(int)Math.Round(z.size.x/1.8f));float w=z.size.x/n;
                for(int i=0;i<n;i++){float cx=x0+w*(i+.5f);P(cx-b.pos.x,y+.004f,z.center.z-b.pos.z,w-.12f,.02f,z.size.z-.12f,LBurn,Bk,0x4A3A30,1,DriveBurner,k,(float)i/n,fade:false);}
                for(int s=-1;s<=1;s+=2)P(z.center.x-b.pos.x,y,z.center.z+s*(z.size.z/2+.12f)-b.pos.z,z.size.x,.012f,.22f,LStripe,0,0,fade:false);
                glows.Add(new NativeWardGlow{X=z.center.x,Y=.2f,Z=z.center.z,R=1,G=.35f,B=.06f,Size=Math.Max(z.size.x,z.size.z),Floor=0,Kind=GlowFurnace,Hazard=(short)k,Entry=-1});
            }
        }
        private void Vault()
        {
            float under=-hy-.03f;
            // Lights under the roof: troffers over the upper floor's walkable decks, high-bays over open double-height space.
            for(float x=-36f;x<=36.01f;x+=4.8f)for(float z=-28f;z<=28.01f;z+=4.8f)
            {
                if(Solid(x,z,12.2f,12.99f)||Solid(x,z,Upper+.3f,Roof-.3f))continue;
                bool deck=!float.IsNaN(FloorAt(x,z,Upper));double h=Hash("vault-light",(int)(x*10),(int)(z*10));
                if(deck){Troffer(x,under,z,1.25f,.6f,h,Upper);}
                else if(((int)Math.Round((x+36)/4.8f)+(int)Math.Round((z+28)/4.8f))%2==0)
                {
                    P(x,under+.05f,z,1.6f,.12f,1.6f,LMetal,Bk,SteelDark);P(x,under-.04f,z,1.3f,.05f,1.3f,LLight,Bk,Fluor,2.6f,DriveFluor,-1,(float)h);
                    Glow(x,under-.06f,z,Fluor,2.6f,GlowHighBay,0,-1,(float)h);
                }
            }
            // Cable trays and ducts along the upper floor's long runs.
            for(int side=-1;side<=1;side+=2){P(0,under-.3f,side*16.7f,74,.12f,.6f,LMetal,Bk,SteelDark);P(0,under-.65f,side*21.5f,74,.8f,1.2f,LMetal,Bk,0x47505A);}
            for(int side=-1;side<=1;side+=2)P(side*36.2f,under-.3f,0,.6f,.12f,56,LMetal,Bk,SteelDark);
            // Rotating emergency beacons over the atrium and the halls.
            float[,] beacons={{-11,-9},{11,9},{-11,9},{11,-9},{-27,0},{27,0},{0,24},{0,-24}};
            for(int i=0;i<beacons.GetLength(0);i++){float x=beacons[i,0],z=beacons[i,1];P(x,under-.12f,z,.35f,.24f,.35f,LMetal,Bk,SteelDark);Lamp(x,under-.32f,z,.24f,.16f,.24f,Red,2.4f,DriveStrobe,-1,i*.137f);Glow(x,under-.35f,z,Red,1.6f,GlowStrobe,FloorAt(x,z,Upper)>=0?Upper:0,-1,i*.137f);}
            // The Spine: a column of light from the Cradle's hatch (the glow the effects shaft hangs from).
            Glow(0,under-.05f,0,0xBFD8FF,5.2f,GlowSpine,0);
        }
        // A fluorescent troffer flush under a ceiling: housing, diffuser (flickers and dims with the mood), its glow.
        private void Troffer(float x,float y,float z,float sx,float sz,double seed,float floor)
        {
            bool alongX=H((int)(x*7),(int)(z*7))<.5;if(!alongX){float t=sx;sx=sz;sz=t;}
            P(x,y+.02f,z,sx+.1f,.05f,sz+.1f,LMetal,Bk,0x8A9298);P(x,y-.02f,z,sx,.035f,sz,LLight,Bk,seed<.06?0xB8E8C8:Fluor,1.9f,DriveFluor,-1,(float)seed);
            Glow(x,y-.04f,z,Fluor,Math.Max(sx,sz)*1.4f,GlowFluor,floor,-1,(float)seed);
        }
        private void Deck(string s)
        {
            // Under the deck: the ground floor's ceiling lights (where the ground below is open floor), every 4 m on a grid.
            float under=-hy-.025f;
            for(float x=b.pos.x-hx+2f;x<=b.pos.x+hx-1.5f;x+=4f)for(float z=b.pos.z-hz+2f;z<=b.pos.z+hz-1.5f;z+=4f)
            {
                if(Solid(x,z,.6f,DeckUnder-.1f))continue;double h=Hash("deck-light",(int)Math.Round(x*10),(int)Math.Round(z*10));Troffer(x-b.pos.x,under,z-b.pos.z,1.25f,.6f,h,0);
            }
            // The gallery's void edge: a yellow nosing on top and a cold LED strip under the lip (the edge reads from both floors).
            if(Starts(s,"gallery-"))
            {
                bool north=Starts(s,"gallery-n")||Starts(s,"gallery-s");int sign=north?(b.pos.z>0?-1:1):(b.pos.x>0?-1:1);int axis=north?2:0;
                FaceP(axis,sign,0,b.size.y-.02f,0,(north?hx:hz)*2-.02f,.04f,.12f,LStripe,0,0);FaceP(axis,sign,0,.02f,0,(north?hx:hz)*2,.05f,.04f,LTube,Bk,0xCFF3FF,1.4f,DriveRoute,-1,.5f);
                // Ink climbing over the gallery decks near the void.
                for(int i=0;i<26;i++){double u=Hash(b.id+"-ink",i);float px=(float)(Hash(b.id+"-ink",i,1)*2-1)*(hx-.6f),pz=(float)(Hash(b.id+"-ink",i,2)*2-1)*(hz-.6f);
                    P(px,hy+.008f,pz,.4f+1.2f*(float)Hash(b.id+"-ink",i,3),.012f,.4f+1.0f*(float)u,LInk,Bk,InkDeep,1,DriveInk,-1,.35f+.6f*(float)u,0,-.05f,0,fade:false);}
            }
            // Upper floor lane lines on the concourses (the deck over the north and south halls).
            if(Starts(s,"deck-"))for(float x=-36;x<=36;x+=2.4f){float z=(b.pos.z>0?1:-1)*16.7f;if(Math.Abs(z-b.pos.z)>hz||Solid(x,z,Upper+.05f,Upper+2))continue;detail=true;P(x-b.pos.x,hy+.006f,z-b.pos.z,1.2f,.01f,.12f,LTube,Bk,0xC8CC9A,.35f,fade:false);detail=false;}
        }

        // ---------------- walls ----------------
        private void Shell()
        {
            // The inner face (toward the middle): wainscot, cove and chair rail on both floors, the deck line, pipes high up.
            int axis,sign;if(hx<hz){axis=0;sign=b.pos.x>0?-1:1;}else{axis=2;sign=b.pos.z>0?-1:1;}float half=HalfU(axis)-2;
            float baseY=-Bottom;   // the block's base is below the slab; v is measured from the base
            for(int f=0;f<2;f++)
            {
                float floor=f==0?0:Upper,v0=baseY+floor;
                FaceP(axis,sign,0,v0,0,half*2,1.15f,.012f,LPanel,Bk,PanelGrey);FaceP(axis,sign,0,v0,0,half*2,.15f,.03f,LMetal,Bk,SteelDark);FaceP(axis,sign,0,v0+1.15f,0,half*2,.06f,.03f,LMetal,Bk,Steel);
                FaceP(axis,sign,0,v0+(f==0?4.5f:5.8f),0,half*2,.14f,.16f,LMetal,Bk,0x3E6B5A);FaceP(axis,sign,0,v0+(f==0?4.8f:6.2f),0,half*2,.1f,.12f,LMetal,Bk,0x3F5D86);
            }
            FaceP(axis,sign,0,baseY+DeckUnder,0,half*2,.4f,.02f,LMetal,Bk,SteelDark);
            // Drawer walls where the shell bounds a morgue, a scorched band behind the incinerator lanes, warning plates.
            for(float u=-half;u<=half;u+=1.0f)
            {
                float x=axis==0?b.pos.x+sign*hx:b.pos.x+u,z=axis==0?b.pos.z+u:b.pos.z+sign*hz;float ix=x-(axis==0?sign*.8f:0),iz=z-(axis==2?sign*.8f:0);
                var room=RoomAt(ix,0,iz);
                if(room=="MORGUE"&&!Solid(ix,iz,.2f,2.2f))for(int r=0;r<3;r++)FaceP(axis,sign,u,baseY+.3f+r*.75f,0,.9f,.68f,.05f,LMetal,Bk,0xA8B2B6);
                if(room=="INCINERATOR")FaceP(axis,sign,u,baseY,0,1.0f,3.4f,.01f,LPlain,0,0x1D1714);
            }
            for(int i=0;i<(int)(half*2/9);i++){float u=-half+4.5f+i*9;var room=RoomAt(axis==0?b.pos.x+sign*(hx+1):b.pos.x+u,0,axis==0?b.pos.z+u:b.pos.z+sign*(hz+1));if(room==null||room=="MORGUE")continue;Sign(axis,sign,u,baseY+2.4f,room,NativeWardZeroLooks.SirenHex,.05f);}
        }
        private void Wall(string s)
        {
            int axis=hx<hz?0:2;float half=HalfU(axis);float floor=BaseLevel(Bottom),v0=floor-Bottom;bool lintel=Bottom>floor+1.6f,sill=!lintel&&Top-floor<1.25f&&Top-floor>.6f,window=lintel&&Solid(b.pos.x,b.pos.z,floor+.3f,floor+.8f);
            for(int sign=-1;sign<=1;sign+=2)
            {
                if(!lintel&&!sill)
                {
                    // Wainscot, cove and chair rail on full-height pieces; pipes and a tray on tall ones.
                    float h=Math.Min(1.15f,b.size.y);FaceP(axis,sign,0,0,0,half*2-.02f,h,.012f,LPanel,Bk,PanelGrey);FaceP(axis,sign,0,0,0,half*2,.15f,.03f,LMetal,Bk,SteelDark);
                    if(b.size.y>1.4f)FaceP(axis,sign,0,1.15f,0,half*2,.06f,.03f,LMetal,Bk,Steel);
                    if(b.size.y>4.6f&&half>1.5f&&H(1,sign)<.6)FaceP(axis,sign,0,Math.Min(b.size.y-.6f,4.6f),0,half*2,.12f,.14f,LMetal,Bk,H(2,sign)<.5?0x3E6B5A:0x3F5D86);
                    // Warning plates and ink veins now and then.
                    if(half>1.6f&&H(3,sign)<.22)FaceP(axis,sign,(float)(H(4,sign)*2-1)*(half-.8f),1.55f,0,.55f,.55f,.02f,LStripe,0,0);
                    if(H(5,sign)<.35){double u=H(6,sign);FaceP(axis,sign,(float)(H(7,sign)*2-1)*(half-.6f),0,0,.35f+.6f*(float)H(8,sign),.6f+2.2f*(float)u,.012f,LInk,Bk,InkDeep,1,DriveInk,-1,.2f+.75f*(float)H(9,sign));
                        if(parts.Count>0){var p=parts[parts.Count-1];if(axis==0)p.Mx=-sign*.06f;else p.Mz=-sign*.06f;parts[parts.Count-1]=p;}}
                }
                if(lintel&&window)FaceP(axis,sign,0,0,0,half*2,.08f,.08f,LMetal,Bk,Steel);
                if(lintel&&!window)
                {
                    // A door: the header under the lintel, jambs down both sides, the room's sign above, a status light.
                    float open=Bottom-floor;FaceP(axis,sign,0,0,0,half*2+.2f,.16f,.06f,LMetal,Bk,Steel);
                    for(int e=-1;e<=1;e+=2)FaceP(axis,sign,e*(half+.05f),-open,0,.1f,open,.05f,LMetal,Bk,Steel);
                    float ux=axis==0?b.pos.x-sign*1.2f:b.pos.x,uz=axis==0?b.pos.z:b.pos.z-sign*1.2f,fx=axis==0?b.pos.x+sign*1.2f:b.pos.x,fz=axis==0?b.pos.z:b.pos.z+sign*1.2f;var room=RoomAt(ux,floor,uz);var here=RoomAt(fx,floor,fz);
                    if(room!=null&&room!=here&&(here==null||here=="HALL"||room=="WARD 0"||room=="DECON")&&b.size.y>.5f&&half>.95f)Sign(axis,sign,0,Math.Min(b.size.y-.5f,.35f),room,ExitC,Math.Min(.05f,(half*2-.3f)/(room.Length*6+4)));
                    FaceP(axis,sign,half-.25f,.22f,0,.12f,.12f,.04f,LTube,Bk,Safe,1.8f,DriveSign);
                }
                if(sill)FaceP(axis,sign,0,b.size.y-.06f,0,half*2,.06f,.08f,LMetal,Bk,Steel);
            }
        }
        private void Rail(string s)
        {
            // A see-through rail: the top bar gets a handrail cap, posts down to the kick plate every 1.25 m and a thin mid rail;
            // the kick plate a cold LED strip on both sides (edges read from both floors). Live bridges' rails carry the
            // energised bars and the lamps.
            bool alongX=hx>hz;float len=alongX?hx*2:hz*2;bool live=Starts(s,"bridge-")&&!Has(s,"specimen")&&!Has(s,"link");int grid=live?Grids[b.pos.x<0?0:1]:-1;
            if(Has(s,"-kick"))
            {
                for(int side=-1;side<=1;side+=2)P(alongX?0:side*(hx+.012f),0,alongX?side*(hz+.012f):0,alongX?len:.02f,.04f,alongX?.02f:len,LTube,Bk,live?0x7FE8FF:0xBFEFFF,live?.8f:1.1f,live?DriveGridBar:DriveRoute,grid,.3f);
                return;
            }
            P(0,hy+.03f,0,alongX?len:.09f,.05f,alongX?.09f:len,LMetal,Bk,Steel);
            int n=Math.Max(1,(int)(len/1.25f));detail=true;for(int i=0;i<=n;i++){float t=-len/2+len*i/n;P(alongX?t:0,-hy-.4f,alongX?0:t,.05f,.8f,.05f,LMetal,Bk,SteelDark);}
            P(0,-hy-.42f,0,alongX?len:.03f,.03f,alongX?.03f:len,LMetal,Bk,SteelDark);detail=false;
            if(live){for(int side=-1;side<=1;side+=2)P(alongX?0:side*(hx+.012f),0,alongX?side*(hz+.012f):0,alongX?len:.02f,.03f,alongX?.02f:len,LTube,Bk,0x7FE8FF,.8f,DriveGridBar,grid,.6f);
                for(int e=-1;e<=1;e+=2)Lamp(alongX?e*(len/2-.1f):0,hy+.12f,alongX?0:e*(len/2-.1f),.14f,.14f,.14f,Safe,1.6f,DriveGridLamp,grid);}
        }
        private void Tread(string s)
        {
            // An LED nosing on the tread's down-slope edge, chasing up the flight (Param: the tread's share of the climb); the
            // stepped balustrade on both open sides.
            int d=s.LastIndexOf('-');int.TryParse(s.Substring(d+1),out int k);NativeWardRoute r=default;bool found=false;
            foreach(var x in routes)if(x.Stair&&x.Id=="siren-"+s.Substring(0,d)){r=x;found=true;break;}if(!found)return;
            float param=(float)k/Math.Max(1,r.Steps-1);bool alongX=r.Ux!=0;float sx=r.Ux,sz=r.Uz;
            if(alongX){P(-sx*(hx-.03f),hy-.02f,0,.05f,.03f,b.size.z-.1f,LTube,Bk,Up,1.5f,DriveRoute,-1,param,fade:false);FaceP(0,-(int)sx,0,b.size.y-.08f,0,b.size.z-.1f,.03f,.02f,LTube,Bk,Up,1.1f,DriveRoute,-1,param);}
            else{P(0,hy-.02f,-sz*(hz-.03f),b.size.x-.1f,.03f,.05f,LTube,Bk,Up,1.5f,DriveRoute,-1,param,fade:false);FaceP(2,-(int)sz,0,b.size.y-.08f,0,b.size.x-.1f,.03f,.02f,LTube,Bk,Up,1.1f,DriveRoute,-1,param);}
            if(k%2==0)for(int side=-1;side<=1;side+=2)
            {
                float ax=alongX?0:side*(hx-.05f),az=alongX?side*(hz-.05f):0;if(Solid(b.pos.x+ax+(alongX?0:side*.35f),b.pos.z+az+(alongX?side*.35f:0),Top+.2f,Top+1.2f))continue;detail=true;
                P(ax,hy+.5f,az,.05f,1.0f,.05f,LMetal,Bk,Steel);P(ax,hy+1.0f,az,alongX?STEPRUN*2:.06f,.05f,alongX?.06f:STEPRUN*2,LMetal,Bk,Steel);detail=false;
            }
        }
        private const float STEPRUN=.5f;
        private void Landing(){for(int i=0;i<3;i++){float t=-.6f+i*.6f;P(t,hy+.006f,0,.18f,.01f,b.size.z*.6f,LTube,Bk,Down,1.2f,DriveRouteDown,-1,i*.25f,fade:false);}}
        private void RampDeck()
        {
            // In the pitched deck's own frame: anti-slip bands across, LED strips along both sides chasing up, curbs.
            bool aboutX=Math.Abs(b.rotation.x)>Math.Abs(b.rotation.z);float L=aboutX?hz*2:hx*2,W=aboutX?hx*2:hz*2;
            float ang=2*(float)Math.Atan2(aboutX?b.rotation.x:b.rotation.z,b.rotation.w);bool upPositive=aboutX?ang<0:ang>0;
            int n=(int)(L/.8f);
            for(int i=0;i<n;i++){float t=-L/2+.4f+i*.8f;if(aboutX)P(0,hy+.006f,t,W-.3f,.012f,.12f,LStripe,0,0,fade:false);else P(t,hy+.006f,0,.12f,.012f,W-.3f,LStripe,0,0,fade:false);}
            for(int side=-1;side<=1;side+=2)for(int i=0;i<10;i++)
            {
                float t=-L/2+L*(i+.5f)/10,param=upPositive?(i/9f):(1-i/9f);
                if(aboutX){P(side*(hx-.08f),hy+.05f,t,.12f,.1f,L/10-.05f,LMetal,Bk,Steel,fade:false);P(side*(hx-.08f),hy+.11f,t,.05f,.02f,L/10-.1f,LTube,Bk,Up,1.5f,DriveRoute,-1,param,fade:false);}
                else{P(t,hy+.05f,side*(hz-.08f),L/10-.05f,.1f,.12f,LMetal,Bk,Steel,fade:false);P(t,hy+.11f,side*(hz-.08f),L/10-.1f,.02f,.05f,LTube,Bk,Up,1.5f,DriveRoute,-1,param,fade:false);}
            }
        }
        private void Skirt(){int axis=hx<hz?0:2;for(int sign=-1;sign<=1;sign+=2){FaceP(axis,sign,0,0,0,HalfU(axis)*2,.15f,.03f,LMetal,Bk,SteelDark);if(b.size.y>1.4f)FaceP(axis,sign,0,1.2f,0,HalfU(axis)*2,.04f,.02f,LStripe,0,0);}}
        private void WalkEdge()
        {
            // Platforms, the specimen bridge and the link: a yellow nosing round the top edges, cold lights under them.
            for(int sign=-1;sign<=1;sign+=2){FaceP(2,sign,0,b.size.y-.02f,0,hx*2,.04f,.1f,LStripe,0,0);FaceP(0,sign,0,b.size.y-.02f,0,hz*2,.04f,.1f,LStripe,0,0);}
            for(float t=-hx+1.5f;t<=hx-1;t+=3f){P(t,-hy-.03f,0,1.0f,.04f,.4f,LLight,Bk,Fluor,1.6f,DriveFluor,-1,(float)H((int)(t*10)));Glow(t,-hy-.05f,0,Fluor,1.6f,GlowFluor,0,-1,(float)H((int)(t*10)));}
        }
        private void GridBridge()
        {
            // The live grating (cyber-grate over the deck, its void glowing with the charge), stringers, insulators, DANGER plates.
            int g=Grids[b.pos.x<0?0:1];P(0,hy+.01f,0,hx*2-.1f,.02f,hz*2-.1f,LGrate,Bk,0x5A6070,1,DriveGridDeck,g,fade:false);
            for(int side=-1;side<=1;side+=2){P(side*(hx-.06f),-hy-.15f,0,.12f,.3f,hz*2,LMetal,Bk,SteelDark);
                for(int i=0;i<6;i++){float z=-hz+1.6f+i*(hz*2-3.2f)/5;P(side*(hx+.03f),-hy-.1f,z,.08f,.2f,.08f,LTube,Bk,0x7FE8FF,1.2f,DriveGridBar,g,i*.17f);}}
            for(int e=-1;e<=1;e+=2){P(0,hy+.007f,e*(hz-.35f),hx*2-.2f,.012f,.5f,LStripe,0,0,fade:false);}
            glows.Add(new NativeWardGlow{X=b.pos.x,Y=Top+.1f,Z=b.pos.z,R=.5f,G=.85f,B=1f,Size=hz*2,Floor=Upper,Kind=GlowScreen,Hazard=(short)g,Entry=-1});
        }
        private void Gantry()
        {
            P(0,hy+.01f,0,hx*2-.06f,.02f,hz*2-.06f,LGrate,Bk,0x4E5560,1,DriveSteady,fade:false);
            for(float z=-hz+2;z<=hz-1.5f;z+=4){P(0,-hy-.05f,z,1.0f,.05f,.4f,LLight,Bk,0xFFE6C8,1.4f,DriveFluor,-1,(float)H((int)(z*10)));Glow(0,-hy-.07f,z,0xFFE6C8,1.5f,GlowFluor,0,-1,(float)H((int)(z*10)));}
            P(-hx+.06f,-hy-.2f,0,.12f,.4f,hz*2,LMetal,Bk,SteelDark);
        }

        // ---------------- rooms and props ----------------
        private void CellRow(bool upper)
        {
            // The row's face toward the aisle: a cell door every 4 m (entries' doors move; the rest stay shut), numbers, lamps;
            // the row's name in the band above. Breach bays (a breach panel in front) are left bare.
            int sign=b.pos.z>0?-1:1;float half=hx;
            for(int i=0;i<8;i++)
            {
                float u=-half+2+i*4,wx=b.pos.x+u;bool breach=false;foreach(int e in Breaches){var be=entries[e];if(Math.Abs(be.center.x-wx)<1.7f&&Math.Abs(be.center.y-b.pos.y)<hy+1)breach=true;}if(breach)continue;
                int entry=-1;for(int e=0;e<entries.Length;e++){var en=entries[e];if(en==null||(en.kind!="cell"&&en.kind!="door"))continue;if(Math.Abs(en.From.x-wx)<.6f&&Math.Abs(en.From.z-b.pos.z)<hz+.2f&&Math.Abs(en.From.y-Bottom)<1){entry=e;break;}}
                if(entry>=0)continue;   // the entrance rig dresses entry doors (DressEntry)
                CellDoor(2,sign,u,upper,i);
            }
            Word(2,sign,upper?"OBSERVATION":"ISOLATION",0,3.6f,.07f,NativeWardZeroLooks.SirenHex,1.3f);
            FaceP(2,sign,0,3.35f,0,half*2,.06f,.03f,LTube,Bk,Teal,1.2f,DriveSign);
        }
        // A shut cell door (dressing): frame, door slab with a lit slit, a number, a red lamp.
        private void CellDoor(int axis,int sign,float u,bool upper,int n)
        {
            FaceP(axis,sign,u,0,0,1.8f,2.9f,.05f,LMetal,Bk,Steel);FaceP(axis,sign,u,.05f,.05f,1.5f,2.7f,.04f,LMetal,Bk,upper?0x8C9AA0:0x6F7C72);
            FaceP(axis,sign,u,1.6f,.09f,.5f,.12f,.02f,LTube,Bk,H(n,40)<.3?Red:0x9FD8C8,H(n,41)<.25?.3f:1.0f,DriveScreen,-1,(float)H(n,42));
            FaceP(axis,sign,u,2.35f,.05f,.4f,.18f,.01f,LPlain,0,0xD8D8C4);
        }
        private void Tank()
        {
            // A specimen tank: plinth and cap (steel), four frame posts, pipes up to the ceiling, a status screen; its fluid glows.
            double h=H(1);P(0,-hy+.15f,0,hx*2+.2f,.3f,hz*2+.2f,LMetal,Bk,SteelDark);P(0,hy-.15f,0,hx*2+.16f,.3f,hz*2+.16f,LMetal,Bk,Steel);
            for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)P(sx*(hx+.02f),0,sz*(hz+.02f),.1f,b.size.y-.4f,.1f,LMetal,Bk,Steel);
            P(-.4f,hy+1.25f,0,.16f,2.2f,.16f,LMetal,Bk,0x3E6B5A);P(.4f,hy+1.25f,.3f,.12f,2.2f,.12f,LMetal,Bk,0x3F5D86);
            FaceP(2,b.pos.z>0?-1:1,0,.5f,.06f,.6f,.35f,.02f,LHolo,Bk,Fluid,1.0f,DriveScreen,-1,(float)h);
            P(0,0,0,hx*2+.02f,b.size.y-.62f,hz*2+.02f,LGlass,Bk,h<.2?0x2A3D38:Fluid,h<.2?.2f:1.1f,DriveTank,-1,(float)h);
            Glow(0,.2f,0,Fluid,2.4f,GlowTank,FloorUnder,-1,(float)h);
        }
        private void Bench(bool lab)
        {
            P(0,hy+.02f,0,hx*2+.06f,.04f,hz*2+.06f,LMetal,Bk,0xB8C0C2);
            if(lab){int n=(int)(Math.Max(hx,hz)*2/1.1f);for(int i=0;i<n;i++){float t=-(Math.Max(hx,hz))+.55f+i*1.1f;double h=H(i);float x=hx>hz?t:0,z=hx>hz?0:t;
                if(h<.4)P(x,hy+.2f,z,.3f,.35f,.3f,LMetal,Bk,0xD8DCDC);else if(h<.7){P(x,hy+.25f,z,.34f,.4f,.08f,LHolo,Bk,Teal,.9f,DriveScreen,-1,(float)h);}else P(x,hy+.12f,z,.5f,.2f,.35f,LMetal,Bk,0x5A6A70);}}
            else P(0,-hy+.15f,0,hx*2-.1f,.05f,hz*2-.1f,LMetal,Bk,SteelDark);
        }
        private void Furnace()
        {
            // The incinerator mouth: a hazard frame, a dark throat, a slot of fire (follows its lane's burners), grill bars.
            int sign=b.pos.x>0?-1:1;FaceP(0,sign,0,0,0,hz*2+.3f,b.size.y+.2f,.04f,LStripe,0,0);FaceP(0,sign,0,.3f,.04f,hz*2-.4f,b.size.y-.7f,.02f,LPlain,0,0x0B0807);
            int lane=-1;foreach(int k in Burners)if(Math.Sign(hazards[k].center.x)==Math.Sign(b.pos.x)){lane=k;break;}
            FaceP(0,sign,0,.6f,.06f,hz*2-.8f,.35f,.02f,LTube,Bk,Burn,2.4f,DriveFurnace,lane);
            for(int i=0;i<7;i++)FaceP(0,sign,-hz+.5f+i*(hz*2-1)/6,.3f,.09f,.08f,b.size.y-.7f,.04f,LMetal,Bk,SteelDark);
            Glow(sign*(hx+.3f),-hy+.8f,0,Burn,3.4f,GlowFurnace,0,lane);
        }
        private void Autopsy(){P(0,hy+.03f,0,hx*2,.05f,hz*2,LMetal,Bk,0xB8C0C2);P(0,hy+.06f,0,hx*2-.3f,.01f,.08f,LPlain,0,0x101214);P(0,-hy+.4f,0,.3f,.8f,.3f,LMetal,Bk,Steel);}
        private void Chamber(bool freezer)
        {
            int axis=hx<hz?0:2,sign=axis==0?(b.pos.x>0?-1:1):(b.pos.z>0?-1:1);float half=HalfU(axis);int cols=Math.Max(1,(int)(half*2/.95f));
            for(int r=0;r<3;r++)for(int c=0;c<cols;c++){float u=-half+half*2*(c+.5f)/cols;FaceP(axis,sign,u,.2f+r*.78f,0,half*2/cols-.08f,.7f,.03f,LMetal,Bk,freezer?0xC8D4DA:0xA8B2B6);FaceP(axis,sign,u,.48f+r*.78f,.03f,.22f,.05f,.03f,LMetal,Bk,SteelDark);}
            if(freezer)FaceP(axis,sign,0,b.size.y-.2f,0,half*2,.06f,.03f,LTube,Bk,0xBFEFFF,1.2f,DriveSign);
        }
        private void Lockers(){int sign=b.pos.z>0?-1:1;int n=(int)(hx*2/.6f);for(int i=0;i<n;i++){float u=-hx+.3f+i*.6f;FaceP(2,sign,u,.1f,0,.55f,b.size.y-.2f,.03f,LMetal,Bk,0x3F7F78);FaceP(2,sign,u,b.size.y-.55f,.03f,.3f,.12f,.01f,LPlain,0,0x1A1D1E);}}
        private void Shower(){P(0,hy+.02f,0,hx*2,.04f,hz*2,LMetal,Bk,Steel);int sign=b.pos.z>0?-1:1;FaceP(2,sign,0,0,0,hx*2-.1f,b.size.y-.1f,.01f,LTile,Bk,0xD8E4E2);FaceP(2,sign,0,1.9f,.02f,.18f,.08f,.25f,LMetal,Bk,Steel);}
        private void Console(bool desk)
        {
            P(0,hy+.02f,0,hx*2+.04f,.04f,hz*2+.04f,LMetal,Bk,SteelDark);int n=Math.Max(1,(int)(hx*2/.9f));int sign=b.pos.z>0?1:-1;
            for(int i=0;i<n;i++){float u=-hx+hx*2*(i+.5f)/n;P(u,hy+.32f,-sign*(hz-.15f),hx*2/n-.1f,.5f,.06f,LHolo,Bk,i%3==0?Teal:0x6FB8FF,1.1f,DriveScreen,-1,(float)H(i));Glow(u,hy+.35f,-sign*(hz-.3f),Teal,.9f,GlowScreen,FloorUnder,-1,(float)H(i));}
        }
        private void Rack(){int sign=b.pos.x>0?-1:1;for(int r=0;r<8;r++)for(int c=0;c<3;c++)FaceP(0,sign,-hz+.6f+c*(hz*2-1.2f)/2,.2f+r*.26f,0,.05f,.04f,.02f,LTube,Bk,H(r,c)<.2?Red:0x5CFF9A,1.6f,DriveScreen,-1,(float)H(r,c+3));}
        private void Booth()
        {
            // The nurse booth: a desk band, glazing above it (dim lit), a roof plate, a monitor, the ward's name.
            for(int axis=0;axis<=2;axis+=2)for(int sign=-1;sign<=1;sign+=2){FaceP(axis,sign,0,0,0,HalfU(axis)*2,1.1f,.02f,LMetal,Bk,0x6F8A84);FaceP(axis,sign,0,1.15f,0,HalfU(axis)*2-.2f,1.05f,.02f,LWindow,0,0x7FA8A0);}
            P(0,hy+.05f,0,hx*2+.1f,.1f,hz*2+.1f,LMetal,Bk,SteelDark);Glow(0,.6f,0,0x9FE0D0,2.2f,GlowScreen,FloorUnder,-1,.5f);
            Word(2,b.pos.z>0?-1:1,"WARD 0",0,2.25f,.035f,NativeWardZeroLooks.SirenHex,1.3f);
        }
        private void Gurney(bool cart)
        {
            if(cart){P(0,hy-.1f,0,hx*2,.08f,hz*2,LMetal,Bk,0xB8C0C2);P(0,-hy+.4f,0,hx*2-.1f,.06f,hz*2-.1f,LMetal,Bk,Steel);for(int i=0;i<3;i++)P(-hx*.5f+i*hx*.5f,hy+.06f,0,.2f,.12f,.2f,LMetal,Bk,i==1?Red:0xD8DCDC);return;}
            P(0,hy-.15f,0,hx*2-.1f,.12f,hz*2-.1f,LPlain,0,0xD7DDE0);P(0,hy-.28f,0,hx*2,.06f,hz*2,LMetal,Bk,Steel);
            for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)P(sx*(hx-.12f),-hy+.08f,sz*(hz-.12f),.12f,.16f,.12f,LPlain,0,0x1A1D1E);
            if(H(1)<.5)P(hx>hz?hx*.6f:0,hy-.05f,hx>hz?0:hz*.6f,.5f,.1f,.4f,LInk,Bk,InkDeep,1,DriveInk,-1,.3f);
        }
        private void TriageScreen(){int axis=hx<hz?0:2;for(int sign=-1;sign<=1;sign+=2)FaceP(axis,sign,0,.2f,0,HalfU(axis)*2-.1f,b.size.y-.3f,.01f,LPlain,0,0x8FB8B0);P(0,hy+.02f,0,hx*2,.04f,hz*2,LMetal,Bk,Steel);}
        private void Shard()
        {
            // A shard of the Siren's vessel (its own frame: yaw-turned): jagged teeth on top, ink running down from them.
            int n=3;for(int i=0;i<n;i++){float t=-hx+hx*2*(i+.5f)/n;P(t,hy+.15f+(float)H(i)*.3f,0,.25f+(float)H(i,1)*.3f,.3f+(float)H(i,2)*.6f,hz*2*.8f,LGlass,Bk,0x2A8C78,.5f,DriveTank,-1,.5f);}
            for(int side=-1;side<=1;side+=2){P((float)(H(5,side)-.5f)*hx,0,side*(hz+.005f),.3f,b.size.y*.8f,.01f,LInk,Bk,InkDeep,1,DriveInk,-1,.05f);}
            P(0,-hy+.04f,0,hx*2+.5f,.06f,hz*2+.5f,LMetal,Bk,SteelDark,fade:false);
            Glow(0,-hy+.3f,0,Ink,1.6f,GlowInk,0,-1,.05f);
        }

        // ---------------- the breach panels (the Hulk's way in; their intact look) ----------------
        private void Breach()
        {
            int e=-1;for(int i=0;i<entries.Length;i++)if(entries[i]?.kind=="breach"&&entries[i].block==b.id){e=i;break;}
            entryTag=(short)e;int axis,sign;
            if(e>=0){var n=entries[e].normal;if(Math.Abs(n.x)>Math.Abs(n.z)){axis=0;sign=n.x>0?1:-1;}else{axis=2;sign=n.z>0?1:-1;}}else{if(hx<hz){axis=0;sign=b.pos.x>0?-1:1;}else{axis=2;sign=b.pos.z>0?-1:1;}}
            float W=HalfU(axis)*2,Hh=b.size.y;
            // Hazard-striped frame, a reinforced inner plate, braces, rivet rows, dents, a CONTAINMENT stencil, a red lamp.
            FaceP(axis,sign,0,0,0,W,.2f,.03f,LStripe,0,0);FaceP(axis,sign,0,Hh-.2f,0,W,.2f,.03f,LStripe,0,0);FaceP(axis,sign,-W/2+.1f,.2f,0,.2f,Hh-.4f,.03f,LStripe,0,0);FaceP(axis,sign,W/2-.1f,.2f,0,.2f,Hh-.4f,.03f,LStripe,0,0);
            FaceP(axis,sign,0,.22f,0,W-.44f,Hh-.44f,.02f,LMetal,Bk,0x4A5560);
            for(int i=0;i<3;i++)FaceP(axis,sign,0,.6f+i*(Hh-1.2f)/2,.02f,W-.5f,.14f,.05f,LMetal,Bk,SteelDark);FaceP(axis,sign,0,.3f,.02f,.18f,Hh-.6f,.05f,LMetal,Bk,SteelDark);
            detail=true;for(int r=0;r<2;r++)for(int c=0;c<5;c++)FaceP(axis,sign,-W/2+.35f+c*(W-.7f)/4,r==0?.32f:Hh-.38f,.03f,.06f,.06f,.02f,LMetal,Bk,0xA0A8AC);detail=false;
            for(int i=0;i<3;i++)FaceP(axis,sign,(float)(H(i,9)*2-1)*(W/2-.6f),.9f+(float)H(i,10)*(Hh-1.8f),.02f,.35f+(float)H(i,11)*.3f,.3f+(float)H(i,12)*.3f,.015f,LMetal,Bk,0x343C44);
            FaceP(axis,sign,0,Hh*.5f-.25f,.03f,1.4f,.3f,.01f,LPlain,0,0xD8D8C4);FaceP(axis,sign,0,Hh*.5f-.18f,.04f,1.2f,.16f,.01f,LPlain,0,0x22262A);
            FaceP(axis,sign,0,Hh-.5f,.03f,.25f,.18f,.08f,LTube,Bk,Red,2.2f,DriveBreach,-1,(float)H(13));
            entryTag=-1;
            // Behind the panel, on the wall it stands against (inside the panel's box, so hidden while it stands): a black
            // recess plate, so the broken panel opens onto darkness and the Hulk comes out of it, not out of a painted wall.
            int me=parent;float t=axis==0?b.size.x:b.size.z,cx=b.pos.x,cz=b.pos.z,bottom=Bottom;
            int w=BlockAt(cx-(axis==0?sign*(t/2+.15f):0),b.pos.y,cz-(axis==2?sign*(t/2+.15f):0),o=>o.id!=b.id&&Stem(o.id)?.StartsWith("breach-",StringComparison.Ordinal)!=true);
            if(w>=0){Use(w);FaceP(axis,sign,axis==0?cz-b.pos.z:cx-b.pos.x,bottom-Bottom+.08f,.07f,W-.16f,Hh-.16f,.01f,LPlain,0,Void);Use(me);}
        }

        // ---------------- entrances (from the PvE entries) ----------------
        private int BlockAt(float x,float y,float z,Func<ContentBlock,bool> ok)
        {
            for(int i=0;i<blocks.Length;i++){var o=blocks[i];if(o?.id==null||!Upright(o)||Stem(o.id)==null||!ok(o))continue;if(Math.Abs(x-o.pos.x)<=o.size.x/2+.02f&&Math.Abs(z-o.pos.z)<=o.size.z/2+.02f&&y>=o.pos.y-o.size.y/2-.02f&&y<=o.pos.y+o.size.y/2+.02f)return i;}
            return -1;
        }
        private void DressEntry(int index)
        {
            var en=entries[index];var f=en.From;var x=en.Exit;entryTag=(short)index;
            float dx=x.x-f.x,dz=x.z-f.z,dl=(float)Math.Sqrt(dx*dx+dz*dz);if(dl>1e-4f){dx/=dl;dz/=dl;}
            switch(en.kind)
            {
                case "vent":
                    if(en.Surface=="floor"){int p=BlockAt(f.x,f.y-.05f,f.z,o=>Math.Abs(o.pos.y+o.size.y/2-f.y)<.08f);if(p<0)break;Use(p);entryTag=(short)index;FloorVent(f,en);}
                    else if(en.Surface=="wall"){int p=BlockAt(f.x-dx*.3f,f.y+.3f,f.z-dz*.3f,o=>true);if(p<0)break;Use(p);entryTag=(short)index;WallVent(f,dx,dz);}
                    else{int p=BlockAt(f.x,f.y+.05f,f.z,o=>Math.Abs(o.pos.y-o.size.y/2-f.y)<.08f);if(p<0)break;Use(p);entryTag=(short)index;CeilingVent(f,index);}
                    break;
                case "cell":case "door":
                {int p=BlockAt(f.x,f.y+1f,f.z,o=>true);if(p<0)break;Use(p);entryTag=(short)index;EntryDoor(f,dx,dz,en.kind=="cell",index);break;}
                case "drop":
                {int p=BlockAt(f.x,f.y+en.height+.05f,f.z,o=>Math.Abs(o.pos.y-o.size.y/2-(f.y+en.height))<.08f);if(p<0)break;Use(p);entryTag=(short)index;Hatch(f.x,f.z,en.hulk?5.2f:3.0f,index);break;}
            }
            entryTag=-1;
        }
        // A floor vent: a frame, the dark shaft, the grate lid sliding aside as it opens, hazard marks on song vents.
        private void FloorVent(V3 f,NativeWardEntry en)
        {
            float x=f.x-b.pos.x,z=f.z-b.pos.z,y=hy+.005f,s=1.9f;
            P(x,y-.002f,z,s,.012f,s,LPlain,0,Void,fade:false);
            for(int k=-1;k<=1;k+=2){P(x+k*(s/2+.06f),y+.01f,z,.12f,.03f,s+.24f,LMetal,Bk,Steel,fade:false);P(x,y+.01f,z+k*(s/2+.06f),s,.03f,.12f,LMetal,Bk,Steel,fade:false);}
            P(x,y+.02f,z,s-.06f,.03f,s-.06f,LGrate,Bk,0x6A7078,1,DriveLeaf,-1,0,s+.1f,.03f,0,fade:false);
            if(en.song>=0)for(int k=0;k<8;k++){double a=k*Math.PI/4;P(x+1.45f*(float)Math.Sin(a),y,z+1.45f*(float)Math.Cos(a),.3f,.012f,.3f,LStripe,0,0,fade:false);}
            Lamp(x+s/2+.06f,y+.04f,z+s/2+.06f,.1f,.04f,.1f,Amber,1.4f,DriveEntryLamp);
        }
        private void WallVent(V3 f,float dx,float dz)
        {
            int axis=Math.Abs(dx)>Math.Abs(dz)?0:2,sign=axis==0?(dx>0?1:-1):(dz>0?1:-1);float u=axis==0?f.z-b.pos.z:f.x-b.pos.x,v=f.y-Bottom-.15f;
            FaceP(axis,sign,u,v,0,1.3f,.9f,.01f,LPlain,0,Void);FaceP(axis,sign,u,v-.08f,0,1.5f,.08f,.06f,LMetal,Bk,Steel);FaceP(axis,sign,u,v+.9f,0,1.5f,.08f,.06f,LMetal,Bk,Steel);
            for(int i=0;i<6;i++)FaceP(axis,sign,u,v+.08f+i*.13f,.02f,1.25f,.06f,.03f,LMetal,Bk,0x7A848A,1,DriveLeaf,-1,0,0,.95f);
            FaceP(axis,sign,u+.85f,v+.95f,0,.1f,.1f,.04f,LTube,Bk,Amber,1.4f,DriveEntryLamp);
        }
        private void CeilingVent(V3 f,int index)
        {
            float x=f.x-b.pos.x,z=f.z-b.pos.z,y=-hy-.006f;
            P(x,y,z,1.3f,.012f,1.3f,LPlain,0,Void);for(int k=-1;k<=1;k+=2){P(x+k*.72f,y-.02f,z,.12f,.04f,1.56f,LMetal,Bk,Steel);P(x,y-.02f,z+k*.72f,1.44f,.04f,.12f,LMetal,Bk,Steel);}
            P(x,y-.03f,z,1.25f,.03f,1.25f,LGrate,Bk,0x6A7078,1,DriveLeaf,-1,0,1.4f,0,0);
            Lamp(x+.8f,y-.06f,z+.8f,.1f,.06f,.1f,Amber,1.4f,DriveEntryLamp);
        }
        // A cell gate (bars that rise) or a door (two leaves that slide apart) on the face the entry walks out of.
        private void EntryDoor(V3 f,float dx,float dz,bool cell,int index)
        {
            int axis=Math.Abs(dx)>Math.Abs(dz)?0:2,sign=axis==0?(dx>0?1:-1):(dz>0?1:-1);float u=axis==0?f.z-b.pos.z:f.x-b.pos.x,floor=f.y-Bottom;
            float w=cell?1.5f:1.8f,h=cell?2.7f:3.0f;
            FaceP(axis,sign,u,floor,0,w,h,.01f,LPlain,0,Void);FaceP(axis,sign,u,floor+h,0,w+.4f,.18f,.08f,LMetal,Bk,Steel);for(int e=-1;e<=1;e+=2)FaceP(axis,sign,u+e*(w/2+.1f),floor,0,.2f,h,.08f,LMetal,Bk,Steel);
            if(cell)
            {
                for(int i=0;i<7;i++)FaceP(axis,sign,u-w/2+.12f+i*(w-.24f)/6,floor+.02f,.02f,.06f,h-.04f,.06f,LMetal,Bk,0x9AA4A8,1,DriveLeaf,-1,0,0,h-.1f);
                for(int r=0;r<2;r++)FaceP(axis,sign,u,floor+.6f+r*1.4f,.03f,w-.1f,.08f,.05f,LMetal,Bk,0x9AA4A8,1,DriveLeaf,-1,0,0,h-.1f);
            }
            else for(int e=-1;e<=1;e+=2)
            {
                FaceP(axis,sign,u+e*w/4,floor+.02f,.02f,w/2-.02f,h-.04f,.06f,LMetal,Bk,0x8C9AA0,1,DriveLeaf,-1,0,e*(w/2-.05f),0);
                FaceP(axis,sign,u+e*w/4,floor+1.5f,.08f,.18f,.35f,.01f,LTube,Bk,0x9FD8C8,.6f,DriveLeaf,-1,0,e*(w/2-.05f),0);
            }
            FaceP(axis,sign,u+w/2+.32f,floor+h-.3f,0,.12f,.12f,.04f,LTube,Bk,Red,1.6f,DriveEntryLamp);
            FaceP(axis,sign,u,floor+h+.3f,0,.5f,.18f,.02f,LTube,Bk,ExitC,.9f,DriveSign);
        }
        // A drop hatch under a ceiling: the hazard ring, the dark opening, four leaves that slide out as it opens, beacons.
        private void Hatch(float wx,float wz,float size,int index)
        {
            float x=wx-b.pos.x,z=wz-b.pos.z,y=-hy-.01f,h=size/2;
            P(x,y,z,size,.012f,size,LPlain,0,Void);
            for(int k=-1;k<=1;k+=2){P(x+k*(h+.18f),y-.03f,z,.36f,.05f,size+.72f,LStripe,0,0);P(x,y-.03f,z+k*(h+.18f),size,.05f,.36f,LStripe,0,0);}
            for(int k=-1;k<=1;k+=2){P(x+k*h/2,y-.06f,z,h-.04f,.06f,size-.04f,LMetal,Bk,0x4A5560,1,DriveLeaf,-1,0,k*(h+.2f),.0f,0);}
            for(int k=-1;k<=1;k+=2)for(int j=-1;j<=1;j+=2){Lamp(x+k*(h+.5f),y-.12f,z+j*(h+.5f),.2f,.14f,.2f,Red,2.2f,DriveEntryLamp,-1,(k+2)*.3f+j*.1f);}
        }
        // The blast shutters: twelve slats in each portal (they drop from the lintel as it closes and hide back inside),
        // a housing with lamps on both faces, guide rails.
        private void ShutterRig(int k)
        {
            var h=hazards[k];float top=h.center.y+h.size.y/2;int p=BlockAt(h.center.x,top+.1f,h.center.z,o=>Math.Abs(o.pos.y-o.size.y/2-top)<.1f);if(p<0)return;Use(p);
            bool alongX=h.size.x>h.size.z;float w=alongX?h.size.x:h.size.z,tall=h.size.y;int n=12;float slat=tall/n;float cx=h.center.x-b.pos.x,cz=h.center.z-b.pos.z,lintelBottom=-hy;
            for(int i=0;i<n;i++)
            {
                float y=lintelBottom-slat*(i+.5f);
                if(alongX)P(cx,y,cz,w-.1f,slat-.03f,.12f,LMetal,Bk,i%4==0?0x6A6F45:0x707A7E,1,DriveSlat,k,0,0,tall,0,lintelBottom);
                else P(cx,y,cz,.12f,slat-.03f,w-.1f,LMetal,Bk,i%4==0?0x6A6F45:0x707A7E,1,DriveSlat,k,0,0,tall,0,lintelBottom);
            }
            for(int side=-1;side<=1;side+=2)
            {
                float o=alongX?cz+side*(hz+.14f):cx+side*(hx+.14f);
                if(alongX){P(cx,lintelBottom+.18f,o,w+.3f,.36f,.28f,LMetal,Bk,SteelDark);P(cx,lintelBottom+.02f,o+side*.13f,w,.04f,.02f,LStripe,0,0);for(int e=-1;e<=1;e+=2)Lamp(cx+e*(w/2+.05f),lintelBottom+.2f,o+side*.16f,.16f,.16f,.06f,Amber,2f,DriveShutterLamp,k);}
                else{P(o,lintelBottom+.18f,cz,.28f,.36f,w+.3f,LMetal,Bk,SteelDark);P(o+side*.13f,lintelBottom+.02f,cz,.02f,.04f,w,LStripe,0,0);for(int e=-1;e<=1;e+=2)Lamp(o+side*.16f,lintelBottom+.2f,cz+e*(w/2+.05f),.06f,.16f,.16f,Amber,2f,DriveShutterLamp,k);}
            }
            for(int e=-1;e<=1;e+=2){if(alongX)P(cx+e*(w/2+.03f),lintelBottom-tall/2,cz,.06f,tall,.3f,LMetal,Bk,Steel);else P(cx,lintelBottom-tall/2,cz+e*(w/2+.03f),.3f,tall,.06f,LMetal,Bk,Steel);}
            glows.Add(new NativeWardGlow{X=h.center.x,Y=top-.2f,Z=h.center.z,R=1,G=.6f,B=.1f,Size=w,Floor=0,Kind=GlowStrobe,Hazard=(short)k,Entry=-1,Param=1});
        }
        // A decon lock: nozzle heads on its ceiling, a beacon and a DECON sign over each of its doors (the walls round it).
        private void GasRig(int k)
        {
            var h=hazards[k];float top=h.center.y+h.size.y/2;int p=BlockAt(h.center.x,top+.1f,h.center.z,o=>Math.Abs(o.pos.y-o.size.y/2-top)<.15f);if(p<0)return;Use(p);
            float cx=h.center.x-b.pos.x,cz=h.center.z-b.pos.z,under=-hy-.03f;
            for(int i=0;i<3;i++)for(int j=0;j<2;j++){float x=cx+(i-1)*h.size.x/3,z=cz+(j-.5f)*h.size.z/2;P(x,under-.1f,z,.3f,.2f,.3f,LMetal,Bk,Steel);Lamp(x,under-.23f,z,.16f,.06f,.16f,Gas,1.4f,DriveGas,k);}
            glows.Add(new NativeWardGlow{X=h.center.x,Y=h.center.y,Z=h.center.z,R=.6f,G=1,B=.7f,Size=Math.Max(h.size.x,h.size.z),Floor=h.center.y-h.size.y/2,Kind=GlowStrobe,Hazard=(short)k,Entry=-1,Param=2});
            // Door lintels touching the lock's volume: a beacon and the sign on both faces.
            for(int i=0;i<blocks.Length;i++)
            {
                var o=blocks[i];if(o?.id==null||!Upright(o)||Stem(o.id)==null)continue;float floor=h.center.y-h.size.y/2;if(o.pos.y-o.size.y/2<floor+1.6f||o.pos.y-o.size.y/2>floor+4)continue;
                if(Math.Min(o.size.x,o.size.z)>.65f)continue;if(Math.Abs(o.pos.x-h.center.x)>h.size.x/2+.6f||Math.Abs(o.pos.z-h.center.z)>h.size.z/2+.6f)continue;
                Use(i);int axis=hx<hz?0:2;for(int sign=-1;sign<=1;sign+=2){FaceP(axis,sign,0,.4f,0,.25f,.18f,.08f,LTube,Bk,Amber,2f,DriveGas,k);Word(axis,sign,"DECON",0,.65f,.04f,Gas,1.3f,LTube,Bk,DriveGas);}
            }
        }

        // ---------------- drives (pure) ----------------
        private static double Frac(double v)=>v-Math.Floor(v);
        private static double Hash01(int a,int c)=>Mix(Mix((uint)a*0x9E3779B9U)^(uint)c*0x85EBCA6BU)/4294967296.0;
        /// <summary>A fluorescent fixture's light level: the mood's level, pumped by the beat, and the stutter of a failing tube
        /// (a fixture fails while its seed is under the mood's flicker share; the rarest ones are dead or buzzing always).</summary>
        public static float FluorLevel(float seed,in NativeWardMood m,double time)
        {
            float level=m.Fluor*(1+.18f*m.Beat);
            if(seed<.035f)return .05f+.04f*(float)Math.Sin(time*40+seed*100);
            if(seed<m.Flicker)
            {
                double rate=6+seed*30,k=Math.Floor(time*rate);double h=Hash01((int)k,(int)(seed*10000));
                if(h<.35)level*=.08f;else if(h<.5)level*=.45f;
                if(Frac(time*.23+seed*7)<.18)level*=.1f;
            }
            return Math.Max(0,level);
        }
        /// <summary>A part's linear colour (gain folded in) and light level this frame.</summary>
        public static void Drive(in NativeWardZeroPart p,NativeHazardState[] states,float[] open,in NativeWardMood m,double time,out float r,out float g,out float bl,out float level)
        {
            int hex=p.Color;float gain=p.Gain;level=1;
            NativeHazardState s=p.Hazard>=0&&states!=null&&p.Hazard<states.Length?states[p.Hazard]:default;var ph=s.Phase;float o=p.Entry>=0&&open!=null&&p.Entry<open.Length?open[p.Entry]:0;
            switch(p.Drive)
            {
                case DriveFluor:level=FluorLevel(p.Param,m,time);break;
                case DriveStrobe:{double turn=Frac(time*1.1+p.Param);float flash=(float)Math.Pow(Math.Max(0,Math.Cos(turn*Math.PI*2)),6);level=.15f+m.Strobe*(.25f+2.2f*flash);break;}
                case DriveRoute:{double u=Frac(time*.7-p.Param*.85);level=.55f+.85f*(float)Math.Pow(1-u,5);break;}
                case DriveRouteDown:{double u=Frac(time*.7+p.Param*.85);level=.5f+.8f*(float)Math.Pow(1-u,5);break;}
                case DriveInk:{float reveal=Smooth01((m.Ink-p.Param)/.06f);level=reveal*(.5f+.7f*m.Beat+.35f*(float)Math.Sin(time*1.7+p.Param*40));break;}
                case DriveTank:{level=p.Param<.2f?.4f:(.75f+.12f*(float)Math.Sin(time*.9+p.Param*30))*(m.Fluor*.4f+.6f)+.35f*m.Beat*m.Groove;if(m.Emergency>.5f&&Hash01((int)Math.Floor(time*8),(int)(p.Param*1000))<.2*m.Emergency)level*=.3f;break;}
                case DriveBurner:
                    if(ph==NativeHazardPhase.Warn){level=1.05f+.9f*s.Level*(.75f+.25f*(float)Math.Sin(time*22+p.Param*6));}
                    else if(ph==NativeHazardPhase.Active){hex=NativeWardZeroLooks.BurnCoreHex;level=1.9f+.5f*(float)Hash01((int)Math.Floor(time*24),(int)(p.Param*97));}
                    else if(ph==NativeHazardPhase.Clear){level=1+.6f*s.Level;}
                    else level=1.02f+.03f*(float)Math.Sin(time*3+p.Param*9);
                    break;
                case DriveFurnace:level=ph==NativeHazardPhase.Active?2.2f+.4f*(float)Math.Sin(time*17):ph==NativeHazardPhase.Warn?1.2f+.6f*s.Level:ph==NativeHazardPhase.Clear?.8f+.8f*s.Level:.7f+.12f*(float)Math.Sin(time*2.3);break;
                case DriveGridLamp:
                    if(ph==NativeHazardPhase.Warn){hex=NativeWardZeroLooks.AmberHex;level=Frac(time*3)<.5?1.4f:.2f;}else if(ph==NativeHazardPhase.Active){hex=NativeWardZeroLooks.EmergencyHex;level=1.7f;}else level=.6f;
                    break;
                case DriveGridBar:
                    if(ph==NativeHazardPhase.Warn){double crawl=Frac(time*2.2+p.Param*.25);level=.2f+1.1f*s.Level*(float)(.55+.45*Math.Sin(crawl*6.2831853));}
                    else if(ph==NativeHazardPhase.Active){hex=0xE6F7FF;level=Hash01((int)Math.Floor(time*30),(int)(p.Param*100))<.2?.6f:2.4f;}
                    else if(ph==NativeHazardPhase.Clear)level=.15f+.9f*s.Level;else level=.35f;
                    break;
                case DriveGridDeck:level=NativeCyberCityKit.DeckGlow(s,time);break;
                case DriveGas:
                    if(ph==NativeHazardPhase.Warn){hex=NativeWardZeroLooks.AmberHex;level=Frac(time*3.2)<.5?1.6f:.25f;}else if(ph==NativeHazardPhase.Active){level=1.8f+.3f*(float)Math.Sin(time*9);}else if(ph==NativeHazardPhase.Clear)level=.6f+.8f*s.Level;else{hex=NativeWardZeroLooks.SafeHex;level=.55f;}
                    break;
                case DriveShutterLamp:
                    if(ph==NativeHazardPhase.Warn)level=Frac(time*3)<.5?1.7f:.2f;else if(ph==NativeHazardPhase.Danger||ph==NativeHazardPhase.Active){hex=NativeWardZeroLooks.EmergencyHex;level=ph==NativeHazardPhase.Danger&&Frac(time*5)<.5?.3f:1.8f;}
                    else if(ph==NativeHazardPhase.Clear)level=1.2f;else{hex=NativeWardZeroLooks.SafeHex;level=.6f;}
                    break;
                case DriveEntryLamp:level=o>.01f?1.2f+1.1f*(Frac(time*4+p.Param)<.5?1:0)*o:.18f;break;
                case DriveBreach:level=.35f+1.0f*(float)Math.Pow(Math.Max(0,Math.Sin(time*2.2+p.Param*6)),8)+.2f*m.Strobe;break;
                case DriveScreen:{double k=Math.Floor(time*12+p.Param*50);level=.85f+.25f*(float)Hash01((int)k,(int)(p.Param*1000));if(Hash01((int)Math.Floor(time*2+p.Param*9),7)<.04+.25*m.Flicker)level*=.2f;level*=.6f+.4f*m.Fluor;break;}
                case DriveSign:level=.9f+.06f*(float)Math.Sin(time*1.3+p.X*.7)+.1f*m.Down;break;
                case DriveLeaf:case DriveSlat:case DriveSteady:default:level=1;break;
            }
            r=(float)NativeSceneryBuilder.Channel(hex,16)*gain;g=(float)NativeSceneryBuilder.Channel(hex,8)*gain;bl=(float)NativeSceneryBuilder.Channel(hex,0)*gain;
        }
        /// <summary>The share of a moving part's (Mx, My, Mz) it is offset by this frame: an entry's open amount for its doors,
        /// gates, lids and hatch leaves; 1 - closure for a shutter slat (up inside the lintel when open); 1 - reveal for buried
        /// ink. `hidden`: a shutter slat whose bottom is back inside the lintel.</summary>
        public static float Move(in NativeWardZeroPart p,NativeHazardState[] states,float[] open,in NativeWardMood m,out bool hidden)
        {
            hidden=false;
            switch(p.Drive)
            {
                case DriveLeaf:return p.Entry>=0&&open!=null&&p.Entry<open.Length?Clamp01(open[p.Entry]):0;
                case DriveSlat:
                {
                    var s=p.Hazard>=0&&states!=null&&p.Hazard<states.Length?states[p.Hazard]:default;float f=1-NativeWardZeroHazards.Closure(s);
                    hidden=p.Y-p.Hy+p.My*f>=p.Clip-.02f;return f;
                }
                case DriveInk:{float reveal=Smooth01((m.Ink-p.Param)/.06f);hidden=reveal<=0;return 1-reveal;}
                default:return 0;
            }
        }
        private static float Clamp01(float v)=>v<0?0:v>1?1:v;
        private static float Smooth01(float t){t=Clamp01(t);return t*t*(3-2*t);}
    }
    /// <summary>A route between the floors (a stair from its treads, a ramp from its pitched deck): foot and top in world space,
    /// the horizontal run direction (axis-aligned), the width, the tread count (0 for a ramp).</summary>
    public struct NativeWardRoute {public string Id;public bool Stair;public float FootX,FootY,FootZ,TopX,TopY,TopZ,Ux,Uz,Width;public int Steps;}
}
