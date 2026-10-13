using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// One dressing part (presentation only, never sim data: ARENA_PROPOSAL.md §2.1 rule 5): a box in its parent block's rest
    /// frame (centre offset from the block's rest centre, in the block's own axes; half extents), drawn through NativeBlockLit
    /// like an arena block. `Look`/`Flags` as NativeBlockLooks.Resolve gives them (a Cyber City bank slot carries
    /// NativeCyberCityLooks.Bank), `Color` sRGB, `Gain` the emission scale on the linear colour (HDR neon above 1). `Drive`
    /// says what animates it (<see cref="NativeCyberCityKit.Drive"/>), `Hazard` and `Param` what it reads. `Shadow`: casts
    /// shadows (big parts only; small trims never do); `Fade`: hidden while it sits between the camera and the pilot (§5.5:
    /// decoration above 2.5 m has no collider, so the camera passes through it).
    /// </summary>
    public struct NativeCyberCityPart
    {
        public int Parent,Look,Flags,Color;public float X,Y,Z,Hx,Hy,Hz,Gain,Param;public byte Drive;public short Hazard;public bool Shadow,Fade;
    }
    /// <summary>A glow source the atmosphere reflects in the wet ground and hazes in the rain: world place, linear colour, size.</summary>
    public struct NativeCyberGlow {public float X,Y,Z,R,G,B,Size;public int Part;}

    /// <summary>
    /// Cyber City's dressing kit (docs/arenas/CYBERCITY_PROPOSAL.md §8, ARENA_PROPOSAL.md §2.2 "Set dressing"): deterministic
    /// parts attached to the map's blocks by id prefix (the draft blockout's stems, docs/arenas/scripts/cybercity_m1.py), so they
    /// ride a falling billboard and vanish with a block that breaks. Built once from the map's blocks and hazards; nothing here
    /// collides or enters the network frame. Below 3 m nothing stands out more than 0.25 m from its block (§2.1 rule 1); the
    /// rest (signs, fins, lattices, strings) sits above 3 m or inside the block's footprint.
    /// <list type="bullet">
    /// <item>Frontage: shopfronts with lit windows, shutters and signs, floor-line LED strips, window bands, neon fins, wall
    /// units and holo ad panels on the arena face; a lit cornice.</item>
    /// <item>The Cut: guideway rails and the maglev coils (<see cref="DriveCoil"/>), edge LED strips that turn amber then red
    /// in the warning (<see cref="DriveEdge"/>), cable trays, kerb stripes, framed alcoves with lime SAFE lights
    /// (<see cref="DriveSafe"/>), the portal signals (<see cref="DriveSignal"/>), drains.</item>
    /// <item>Couplings: grating, energised rails (<see cref="DriveGridBar"/>), pylon insulators and lamps
    /// (<see cref="DriveGridLamp"/>), hazard stripes.</item>
    /// <item>The Cut's exits (when Build gets the map's <see cref="NativeCyberCityExits"/>): the lintel over each exit door carries
    /// a white EXIT sign with a stair pictogram rising the way the climb goes instead of the lime SAFE light (<see cref="DriveExit"/>),
    /// the walls either side frame the door with white strips, ">>" chevrons through it and a small EXIT, the landing has floor
    /// chevrons toward the stairs, and the climb's steps carry white nosings and riser lines that chase up toward the street
    /// (<see cref="DriveExitChase"/>). SAFE means "hide here"; white EXIT means "climb out".</item>
    /// <item>Station, podiums and spires: deck and stair LED nosings, framed openings, floor-line strips (Nova magenta, Vega
    /// cyan), spire corner fins and crowns, the NOVA / VEGA / HELIX signs, rooftop plant details.</item>
    /// <item>Markets, avenues and yards: lantern strings and lanterns, the open canopy lattice, stall lamps, signs, vending
    /// glow, vehicle lights and underglow, lamp heads, planter shrubs, pillar screens, sidewalks, markings and the painted
    /// fell zones.</item>
    /// </list>
    /// The maglev train is a rig of its own (<see cref="Train"/>, posed from the hazard's head position). Pure: the same blocks
    /// give the same parts; <see cref="Drive"/> is a pure function of the part, the hazard states and the clock.
    /// </summary>
    public sealed class NativeCyberCityKit
    {
        public const byte DriveSteady=0,DriveFlicker=1,DriveSignal=2,DriveCoil=3,DriveEdge=4,DriveGridBar=5,DriveGridLamp=6,DriveSafe=7,DriveGridDeck=8,DriveExit=9,DriveExitChase=10;
        public NativeCyberCityPart[] Parts;public NativeCyberGlow[] Glows;public NativeCyberCityPart[] Train;
        /// <summary>Hazard indices the drives read (into the array Build took; -1 when absent).</summary>
        public int Maglev=-1;public readonly int[] Grids={-1,-1};
        /// <summary>The Cut's exits Build dressed (null when it got none: the exit doors then look like the alcoves).</summary>
        public NativeCyberCityExits Exits;
        private readonly List<NativeCyberCityPart> parts=new List<NativeCyberCityPart>();private readonly List<NativeCyberGlow> glows=new List<NativeCyberGlow>();
        private ContentBlock[] blocks;private int parent;private ContentBlock b;private float hx,hy,hz;

        private const int LPanel=NativeCyberCityLooks.Panel,LMetal=NativeCyberCityLooks.Metal,LTube=NativeCyberCityLooks.Tube,LHolo=NativeCyberCityLooks.Holo,LGlass=NativeCyberCityLooks.Glass,LTile=NativeCyberCityLooks.Tile,LGrate=NativeCyberCityLooks.Grate;
        private const int Cy=NativeCyberCityLooks.Bank;
        private const int CyanC=NativeCyberCityLooks.CyanHex,MagentaC=NativeCyberCityLooks.MagentaHex,VioletC=NativeCyberCityLooks.VioletHex,IceC=NativeCyberCityLooks.IceHex,WarmC=NativeCyberCityLooks.WarmHex,LimeC=NativeCyberCityLooks.LimeHex,AmberC=NativeCyberCityLooks.AmberHex,RedC=NativeCyberCityLooks.DangerHex,ExitC=NativeCyberCityLooks.ExitHex;
        private static readonly int[] Neons={CyanC,MagentaC,VioletC,IceC,0xFF5A8A,0x5AFFD2};

        // ---------------- hashing (stable across machines) ----------------
        private static uint Mix(uint x){unchecked{x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x;}}
        private static uint Fnv(string s){unchecked{uint h=2166136261;if(s!=null)foreach(char c in s)h=(h^c)*16777619;return h;}}
        /// <summary>Deterministic [0, 1) from a string and two salts.</summary>
        public static double Hash(string s,int a=0,int c=0){unchecked{return Mix(Fnv(s)^Mix((uint)a*0x9E3779B9U+(uint)c*0x85EBCA6BU))/4294967296.0;}}
        private double H(int a,int c=0)=>Hash(b.id,a,c);

        /// <summary>Builds the kit for a map's blocks (Cyber City ids only; anything else gets nothing) and its hazards.</summary>
        public static NativeCyberCityKit Build(ContentBlock[] blocks,MapHazard[] hazards)=>Build(blocks,hazards,null);
        /// <summary>The kit with the Cut's exits dressed (<see cref="NativeCyberCityExits.Build(ContentMap)"/> on the same blocks).</summary>
        public static NativeCyberCityKit Build(ContentBlock[] blocks,MapHazard[] hazards,NativeCyberCityExits exits)
        {
            var k=new NativeCyberCityKit{blocks=blocks??Array.Empty<ContentBlock>()};
            if(exits!=null&&exits.Exits.Length>0&&exits.BlockOpening.Length==k.blocks.Length)k.Exits=exits;
            if(hazards!=null)for(int i=0;i<hazards.Length;i++){if(NativeCyberCityHazards.IsMaglev(hazards[i])&&k.Maglev<0)k.Maglev=i;if(NativeCyberCityHazards.IsGrid(hazards[i])){int side=hazards[i].center.x<0?0:1;if(k.Grids[side]<0)k.Grids[side]=i;}}
            for(int i=0;i<k.blocks.Length;i++){var bl=k.blocks[i];if(bl?.id==null)continue;k.parent=i;k.b=bl;k.hx=bl.size.x/2;k.hy=bl.size.y/2;k.hz=bl.size.z/2;k.Dress();}
            k.Parts=k.parts.ToArray();k.Glows=k.glows.ToArray();k.Train=BuildTrain();k.parts.Clear();k.glows.Clear();return k;
        }
        /// <summary>The id with the map prefix removed (rubble keeps its "rubble-" marker in front), or null for other maps' ids.</summary>
        public static string Stem(string id)
        {
            if(id==null)return null;const string p="cybercity-",r="rubble-cybercity-";
            return id.StartsWith(r,StringComparison.Ordinal)?"rubble-"+id.Substring(r.Length):id.StartsWith(p,StringComparison.Ordinal)?id.Substring(p.Length):null;
        }
        private static bool Has(string s,string part)=>s.IndexOf(part,StringComparison.Ordinal)>=0;
        private static bool Starts(string s,string p)=>s.StartsWith(p,StringComparison.Ordinal);
        // Side of the map (+1 north, -1 south) and the podium/market colour family.
        private int North=>b.pos.z>=0?1:-1;
        private int Accent=>Starts(Stem(b.id)??"","vega")||b.pos.z<0&&!Starts(Stem(b.id)??"","nova")?CyanC:MagentaC;

        // ---------------- part helpers (parent-local: offsets from the block's rest centre in its own axes) ----------------
        private void P(float x,float y,float z,float sx,float sy,float sz,int look,int flags,int color,float gain=1,byte drive=0,int hazard=-1,float param=0,bool shadow=false,bool fade=true)
        {
            if(!(sx>0&&sy>0&&sz>0))return;
            parts.Add(new NativeCyberCityPart{Parent=parent,X=x,Y=y,Z=z,Hx=sx/2,Hy=sy/2,Hz=sz/2,Look=look,Flags=flags,Color=color,Gain=gain,Drive=drive,Hazard=(short)hazard,Param=param,Shadow=shadow,Fade=fade&&b.pos.y+y>2.5f});
        }
        private void Tube(float x,float y,float z,float sx,float sy,float sz,int color,float gain=2.2f,byte drive=0,int hazard=-1,float param=0,bool glow=false)
        {P(x,y,z,sx,sy,sz,LTube,Cy,color,gain,drive,hazard,param);if(glow)Glow(x,y,z,color,Math.Max(sx,Math.Max(sy,sz))*gain*.45f);}
        private void Glow(float x,float y,float z,int color,float size){glows.Add(new NativeCyberGlow{X=b.pos.x+x,Y=b.pos.y+y,Z=b.pos.z+z,R=(float)NativeSceneryBuilder.Channel(color,16),G=(float)NativeSceneryBuilder.Channel(color,8),B=(float)NativeSceneryBuilder.Channel(color,0),Size=size,Part=parts.Count-1});}
        // A face of the block: axis 0 (x) or 2 (z), outward sign. FaceP places a part at u along the face's horizontal tangent
        // (z on an x face, x on a z face; offsets from the block's centre), its bottom edge v metres above the block's base, w out from the face
        // (its centre at w + depth/2: flush parts have w = 0), with width, height and depth.
        private void FaceP(int axis,int sign,float u,float v,float w,float width,float height,float depth,int look,int flags,int color,float gain=1,byte drive=0,int hazard=-1,float param=0,bool shadow=false)
        {
            float y=-hy+v+height/2;
            if(axis==0)P(sign*(hx+w+depth/2),y,u,depth,height,width,look,flags,color,gain,drive,hazard,param,shadow);
            else P(u,y,sign*(hz+w+depth/2),width,height,depth,look,flags,color,gain,drive,hazard,param,shadow);
        }
        private void FaceTube(int axis,int sign,float u,float v,float width,float height,int color,float gain=2.2f,byte drive=0,int hazard=-1,float param=0,bool glow=false)
        {
            FaceP(axis,sign,u,v,0,width,height,.05f,LTube,Cy,color,gain,drive,hazard,param);
            if(glow){float y=-hy+v+height/2;Glow(axis==0?sign*(hx+.05f):u,y,axis==0?u:sign*(hz+.05f),color,Math.Max(width,height)*gain*.4f);}
        }
        private float HalfU(int axis)=>axis==0?hz:hx;
        // The face of the block that looks toward the arena's middle (frontage, portal) or away from a podium's middle.
        private void TopP(float x,float z,float sx,float sz,float thick,int look,int flags,int color,float gain=1){P(x,hy+thick/2,z,sx,thick,sz,look,flags,color,gain,fade:false);}

        // ---------------- the 5 x 7 neon font ----------------
        private static readonly Dictionary<char,string> Font=MakeFont();
        private static Dictionary<char,string> MakeFont()
        {
            var f=new Dictionary<char,string>();
            string[] g={"A01110100011000111111100011000110001","B11110100011000111110100011000111110","C01110100011000010000100001000101110","D11110100011000110001100011000111110","E11111100001000011110100001000011111",
                "F11111100001000011110100001000010000","G01110100011000010111100011000101111","H10001100011000111111100011000110001","I01110001000010000100001000010001110","K10001100101010011000101001001010001",
                "L10000100001000010000100001000011111","M10001110111010110101100011000110001","N10001100011100110101100111000110001","O01110100011000110001100011000101110","P11110100011000111110100001000010000",
                "R11110100011000111110101001001010001","S01111100001000001110000010000111110","T11111001000010000100001000010000100","U10001100011000110001100011000101110","V10001100011000110001100010101000100",
                "W10001100011000110101101011010101010","X10001100010101000100010101000110001","Y10001100010101000100001000010000100","Z11111000010001000100010001000011111","001110100011001110101110011000101110",
                "100100011000010000100001000010001110","201110100010000100010001000100011111","311110000010000101110000010000111110","400010001100101010010111110001000010","511111100001111000001000011000101110",
                "600110010001000011110100011000101110","711111000010001000100010000100001000","801110100011000101110100011000101110","901110100011000101111000010001001100","-00000000000000011111000000000000000",
                "!00100001000010000100001000000000100"," 00000000000000000000000000000000000"};
            foreach(var s in g)f[s[0]]=s.Substring(1);return f;
        }
        /// <summary>The glyph rectangles of a 5 x 7 neon letter (column, row from the top, width, height in cells): rows' runs,
        /// merged down where consecutive rows repeat a run.</summary>
        public static List<int[]> Glyph(char c)
        {
            var o=new List<int[]>();if(!Font.TryGetValue(char.ToUpperInvariant(c),out var bits))return o;var open=new List<int[]>();
            for(int r=0;r<7;r++)
            {
                var runs=new List<int[]>();for(int x=0;x<5;){if(bits[r*5+x]=='1'){int s=x;while(x<5&&bits[r*5+x]=='1')x++;runs.Add(new[]{s,x-s});}else x++;}
                var next=new List<int[]>();
                foreach(var run in runs){var m=open.Find(q=>q[0]==run[0]&&q[2]==run[1]&&q[1]+q[3]==r);if(m!=null){m[3]++;next.Add(m);}else{var n=new[]{run[0],r,run[1],1};o.Add(n);next.Add(n);}}
                open=next;
            }
            return o;
        }
        /// <summary>A neon word on a face, centred at u, its baseline v up, letter cells `cell` m (a letter is 5 x 7 cells, 1 cell apart).</summary>
        private void Word(int axis,int sign,string text,float u,float v,float cell,int color,float gain=2.6f,float depth=.06f,byte drive=0,float param=0,int hazard=-1)
        {
            float width=(text.Length*6-1)*cell,start=u-width/2;bool flip=axis==0?sign<0:sign>0;
            for(int i=0;i<text.Length;i++)foreach(var r in Glyph(text[i]))
            {
                float a=start+(i*6+r[0]+r[2]*.5f)*cell,uu=flip?2*u-a:a,vv=v+(7-r[1]-r[3]*.5f)*cell;
                FaceP(axis,sign,uu,vv-r[3]*cell*.5f+cell*.12f,0,r[2]*cell-cell*.24f,r[3]*cell-cell*.24f,depth,LTube,Cy,color,gain,drive,hazard,param);
            }
            float y=-hy+v+3.5f*cell;Glow(axis==0?sign*(hx+.1f):u,y,axis==0?u:sign*(hz+.1f),color,width*.55f+cell*4);
        }

        // ---------------- rules by id stem ----------------
        private void Dress()
        {
            string s=Stem(b.id);if(s==null)return;
            // An exit's lintel takes the EXIT sign in place of its own dressing (the alcoves' SAFE light).
            int role=Exits!=null?Exits.BlockOpening[parent]:-1;if(role>=0&&role%4==1){ExitSign(role/4);return;}
            if(Starts(s,"frontage-"))Frontage(s);
            else if(Starts(s,"portal-"))Portal();
            else if(Starts(s,"trench-wall-"))TrenchWall();
            else if(s=="guideway")Guideway();
            else if(s=="trench-floor")TrenchFloor();
            else if(Starts(s,"ground-"))Ground(s);
            else if(s=="plaza-deck")PlazaDeck();
            else if(Starts(s,"grid-")&&Has(s,"-deck"))GridDeck();
            else if(Starts(s,"grid-")&&Has(s,"-pylon-"))GridPylon();
            else if(Starts(s,"grid-")&&Has(s,"-beam-"))GridBeam();
            else if(s=="station-deck")StationDeck();
            else if(Starts(s,"station-col-"))Column();
            else if(s=="station-lift")Lift();
            else if(s=="station-lift-head")LiftHead();
            else if(Has(s,"-rail-")||Has(s,"-rail"))RailStrip();
            else if(Has(s,"stair-"))Nosing();
            else if(Has(s,"-spire"))Spire(s);
            else if(Has(s,"-roof-unit-"))RoofUnit();
            else if(Starts(s,"nova-s")||Starts(s,"vega-s"))PodiumWall(s);
            else if(Has(s,"-floor-"))Ceiling();
            else if(Has(s,"-parapet-"))ParapetStrip();
            else if(Has(s,"-post-"))Post(s);
            else if(Has(s,"-bar")&&!Has(s,"barrier"))Bar();
            else if(Has(s,"-booth"))Booth();
            else if(Has(s,"-box-"))Container(s);
            else if(Has(s,"-stall-"))Stall();
            else if(Has(s,"-vend-"))Vending();
            else if(Has(s,"-van-")||Has(s,"-cab-")||Has(s,"-bike"))Vehicle(s);
            else if(Has(s,"-kiosk"))Kiosk();
            else if(Has(s,"-pillar"))Pillar();
            else if(Has(s,"-shelter-"))Shelter();
            else if(Has(s,"-planter-"))Planter();
            else if(Starts(s,"lamp-"))Lamp();
            else if(Starts(s,"billboard-")&&Has(s,"-strut-"))Strut();
            else if(Starts(s,"billboard-")&&Has(s,"-frame-"))Bezel();
            else if(Has(s,"-canopy"))Canopy();
            else if(Has(s,"-pylon-"))Charger();
            else if(Has(s,"-wall"))YardWall();
            if(role>=0)ExitJamb(role/4,role%4==2);
            int exit=ExitOf();if(exit>=0&&!Has(s,"stair-"))ExitLanding(exit);
        }
        private int ExitOf()=>Exits!=null?Exits.BlockExit[parent]:-1;
        // Which face (axis, sign) of the block looks toward the arena's middle: the long axis' normal toward the origin.
        private void Inward(out int axis,out int sign){if(hx<hz){axis=0;sign=b.pos.x>0?-1:1;}else{axis=2;sign=b.pos.z>0?-1:1;}}
        private void Frontage(string s)
        {
            Inward(out int ax,out int sg);float half=HalfU(ax);int bays=Math.Max(1,(int)(half*2/7.2f));float bay=half*2/bays;
            for(int i=0;i<bays;i++)
            {
                float u=-half+bay*(i+.5f);double h=H(i);
                // Ground floor: a lit shop window or a shutter, a fascia sign, and a step of LED under the fascia.
                if(h<.55){FaceP(ax,sg,u,.15f,0,bay*.62f,2.5f,.06f,LGlass,Cy,0x1B2440);FaceTube(ax,sg,u,.15f,bay*.62f,.05f,Pick(Neons,i,7));}
                else FaceP(ax,sg,u,.05f,0,bay*.6f,2.7f,.08f,LMetal,Cy,0x3A3F4E);
                if(H(i,2)<.7)FaceP(ax,sg,u,2.95f,0,bay*.7f,.55f,.08f,LHolo,Cy,Pick(Neons,i,3),1.1f);
                // Upper floors: window bands, floor-line strips, a neon fin every other bay, wall units.
                for(int f=1;f<4;f++)
                {
                    float v=f*3.6f+.3f;if(H(i,10+f)<.75)FaceP(ax,sg,u,v+.6f,0,bay*.8f,1.8f,.05f,LGlass,Cy,0x141A2C);
                    if(H(i,20+f)<.35)FaceP(ax,sg,u+bay*.3f,v+.1f,0,.9f,.6f,.5f,LMetal,Cy,0x4A5163,shadow:true);
                }
                FaceTube(ax,sg,u,3.6f,bay,.06f,Pick(Neons,i,5),1.6f);
                if(i%2==1)FaceP(ax,sg,u-bay/2,4.2f,0,.16f,10.5f,.24f,LTube,Cy,Pick(Neons,i,11),2.4f);
                if(H(i,30)<.22&&bay>6){FaceP(ax,sg,u,8.4f,0,bay*.7f,3.6f,.12f,LHolo,Cy,Pick(Neons,i,13),1.2f);Glow(ax==0?sg*(hx+.2f):u,-hy+10.2f,ax==0?u:sg*(hz+.2f),Pick(Neons,i,13),5);}
            }
            FaceTube(ax,sg,0,15.8f,half*2,.08f,VioletC,1.8f);
        }
        private static int Pick(int[] list,int i,int salt)=>list[(int)(Mix((uint)(i*7919+salt*104729))%(uint)list.Length)];
        private void Portal()
        {
            // The tunnel mouth: hazard chevrons framing the opening, the HELIX LINE sign above it.
            int sg=b.pos.x>0?-1:1;
            FaceP(0,sg,0,0,0,6.2f,.5f,.06f,NativeBlockLooks.Demolition,0,0);
            Word(0,sg,"HELIX",0,2.1f,.16f,CyanC);Word(0,sg,"LINE",0,.9f,.12f,IceC,2f);
        }
        private void TrenchWall()
        {
            // The band piece's face toward the Cut, from the floor to the street: an edge LED strip under the lip that warns with
            // the maglev, a cable tray, and at an alcove (a short lintel) a lime SAFE light and frame.
            int sg=b.pos.z>0?-1:1;float len=hx*2;bool lintel=b.size.y<3;
            if(lintel){FaceTube(2,sg,0,.06f,len-.1f,.06f,LimeC,2.4f,DriveSafe,Maglev,b.pos.x,true);FaceP(2,sg,0,b.size.y-.18f,0,len,.12f,.04f,NativeBlockLooks.Demolition,0,0);return;}
            FaceTube(2,sg,0,b.size.y-.55f,len,.06f,CyanC,1.6f,DriveEdge,Maglev,b.pos.x);
            FaceP(2,sg,0,b.size.y-1.3f,0,len,.1f,.12f,LMetal,Cy,0x3A3F4E);
            TopP(0,sg*(hz-.14f),len,.25f,.03f,NativeBlockLooks.Demolition,0,0);
            for(float u=-hx+1.5f;u<hx-1;u+=6)FaceP(2,sg,u,b.size.y-3.3f,0,.5f,.5f,.06f,LMetal,Cy,0x2A2E3A);
        }
        private void Guideway()
        {
            // Two rails, and the coils along each side that light ahead of the train.
            for(int s=-1;s<=1;s+=2)TopP(0,s*.62f,hx*2,.16f,.12f,LMetal,Cy,0x6B7385);
            for(float x=-hx+1;x<hx;x+=2)for(int s=-1;s<=1;s+=2)P(x,-hy+.12f,s*(hz+.03f),1.6f,.08f,.06f,LTube,Cy,CyanC,2.4f,DriveCoil,Maglev,b.pos.x+x,fade:false);
        }
        private void TrenchFloor()
        {
            for(int s=-1;s<=1;s+=2){P(0,hy+.01f,s*2.8f,hx*2,.02f,.35f,LGrate,Cy,0x3A3F4E,fade:false);P(0,hy+.005f,s*1.6f,hx*2,.01f,.08f,NativeBlockLooks.Plain,0,0xC9CED8,fade:false);}
        }
        private void Ground(string s)
        {
            if(Has(s,"strip"))return;int n=North;
            // The promenade's paving along the Cut, the avenue's sidewalks, lane markings, crosswalks, manholes and vents.
            float bz=b.pos.z;
            void Plate(float x0,float x1,float z0,float z1,int look,int flags,int color,float t=.012f){P((x0+x1)/2-b.pos.x,hy+t/2,(z0+z1)/2-bz,x1-x0,t,z1-z0,look,flags,color,fade:false);}
            Plate(-38,38,n>0?6f:-9f,n>0?9f:-6f,LTile,Cy,0x3B3D47);
            foreach(var (x0,x1) in new[]{(-38f,-37.2f),(-26f,11f),(22f,38f)})Plate(n>0?x0:-x1,n>0?x1:-x0,n>0?3.25f:-6f,n>0?6f:-3.25f,LTile,Cy,0x3B3D47);
            Plate(-38,38,n>0?22f:-23f,n>0?23f:-22f,LTile,Cy,0x3B3D47);Plate(-38,38,n>0?29.2f:-30f,n>0?30f:-29.2f,LTile,Cy,0x3B3D47);
            for(float x=-36;x<26;x+=4.5f)Plate(n>0?x:-x-2.2f,n>0?x+2.2f:-x,n>0?25.9f:-26.1f,n>0?26.1f:-25.9f,NativeBlockLooks.Plain,0,0xD8DCE4,.014f);
            foreach(float cx in new[]{-12f,8f})for(int k=0;k<6;k++){float x=n>0?cx-2.5f+k*1f:-cx+2.5f-k*1f;Plate(x-.25f,x+.25f,n>0?23f:-29.2f,n>0?29.2f:-23f,NativeBlockLooks.Plain,0,0xD8DCE4,.014f);}
            // Fell zone: the billboard's painted landing, a hatched border (2.3: kept free of props).
            float fx0=n>0?-10.5f:-4.5f,fx1=n>0?4.5f:10.5f,fz0=n>0?22.6f:-28.4f,fz1=n>0?28.4f:-22.6f;
            Plate(fx0,fx1,fz0,fz0+.3f,NativeBlockLooks.Demolition,0,0,.016f);Plate(fx0,fx1,fz1-.3f,fz1,NativeBlockLooks.Demolition,0,0,.016f);
            Plate(fx0,fx0+.3f,fz0,fz1,NativeBlockLooks.Demolition,0,0,.016f);Plate(fx1-.3f,fx1,fz0,fz1,NativeBlockLooks.Demolition,0,0,.016f);
            foreach(var m in n>0?new[]{(-20f,24.5f),(2f,27.4f),(16f,23.6f),(-34f,12.5f),(-4f,19f)}:new[]{(20f,-24.5f),(-2f,-27.4f),(-16f,-23.6f),(34f,-12.5f),(4f,-19f)})
                Plate(m.Item1-.45f,m.Item1+.45f,m.Item2-.45f,m.Item2+.45f,LGrate,Cy,0x4A5163,.02f);
        }
        private void PlazaDeck()
        {
            for(int s=-1;s<=1;s+=2){FaceTube(0,s,0,hy*2-.35f,hz*2,.06f,CyanC,1.8f,DriveEdge,Maglev,b.pos.x+s*hx);TopP(s*(hx-.14f),0,.25f,hz*2,.03f,NativeBlockLooks.Demolition,0,0);}
        }
        private void GridDeck()
        {
            int g=b.pos.x<0?0:1;int hazard=Grids[g];
            P(0,hy+.015f,0,hx*2,.03f,hz*2+1.2f,LGrate,Cy,0x5A6070,1,DriveGridDeck,hazard,0,fade:false);
            for(int i=0;i<4;i++){float x=-hx+.5f+i*(hx*2-1)/3;P(x,hy+.04f,0,.08f,.04f,hz*2+1.2f,LTube,Cy,CyanC,2.6f,DriveGridBar,hazard,i,fade:false);}
            for(int s=-1;s<=1;s+=2)P(s*(hx-.12f),hy+.035f,0,.24f,.01f,hz*2+1.2f,NativeBlockLooks.Demolition,0,0,fade:false);
        }
        private void GridPylon()
        {
            int g=b.pos.x<0?0:1;
            for(int i=0;i<3;i++)P(0,-hy+1.7f+i*.28f,0,hx*2+.18f,.1f,hz*2+.18f,LMetal,Cy,0x8A93A8);
            P(0,-hy+1.0f,0,hx*2+.02f,.4f,hz*2+.02f,NativeBlockLooks.Demolition,0,0,fade:false);
            P(0,hy-.25f,0,hx*2+.06f,.35f,hz*2+.06f,LTube,Cy,AmberC,2.8f,DriveGridLamp,Grids[g],0);Glow(0,hy-.25f,0,AmberC,1.6f);
        }
        private void GridBeam()
        {
            int g=b.pos.x<0?0:1;P(0,-hy-.03f,0,hx*2-.2f,.05f,.12f,LTube,Cy,CyanC,2.2f,DriveGridBar,Grids[g],9);
            int sg=b.pos.z>0?-1:1;Word(2,sg,"HV",0,.02f,.05f,AmberC,2.2f);
        }
        private void StationDeck()
        {
            // Fascia LED along the deck edges and a grid of LED lines under it (lighting the plaza).
            foreach(var (ax,sg) in new[]{(0,1),(0,-1),(2,1),(2,-1)})FaceTube(ax,sg,0,.25f,(ax==0?hz:hx)*2,.08f,IceC,2f);
            for(float x=-hx+1.5f;x<hx;x+=3)P(x,-hy-.03f,0,.12f,.05f,hz*2-1,LTube,Cy,IceC,2.4f);
            Glow(0,-hy-.2f,0,IceC,9);
        }
        private void Column()
        {
            for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)P(sx*(hx+.02f),0,sz*(hz+.02f),.07f,hy*2-.4f,.07f,LTube,Cy,IceC,1.8f);
        }
        private void Lift()
        {
            // Lift doors on the two faces along the bridge, framed in light, and a floor indicator.
            for(int s=-1;s<=1;s+=2){FaceP(2,s,0,0,0,1.4f,2.4f,.05f,LMetal,Cy,0x6B7385);FaceTube(2,s,0,2.45f,1.6f,.05f,CyanC,2f);for(int e=-1;e<=1;e+=2)FaceTube(2,s,e*.78f,0,.05f,2.45f,CyanC,2f);FaceP(2,s,0,2.7f,0,.6f,.25f,.04f,LHolo,Cy,AmberC);}
        }
        private void LiftHead()
        {
            foreach(var (ax,sg) in new[]{(0,1),(0,-1),(2,1),(2,-1)})Word(ax,sg,"HELIX",0,1.3f,.075f,ax==0?CyanC:MagentaC,2.6f);
            TopP(0,0,hx*2,hz*2,.06f,LTube,Cy,IceC,1.6f);
        }
        private void RailStrip(){TopP(0,0,hx*2,hz*2,.04f,LTube,Cy,Accent==MagentaC&&!Has(Stem(b.id),"station")?MagentaC:CyanC,1.5f);}
        private void Nosing()
        {
            // An LED nosing on each step's front edge (the stair rises along its longer axis; the front faces down-slope). On an
            // exit's climb they are the exit's white, chasing up toward the street (Param: the step's height up the climb), and the
            // riser facing down the climb gets a line under its nosing.
            bool runX=hx<hz;int e=ExitOf();bool exit=e>=0;int col=exit?ExitC:IceC;float gain=exit?2.4f:1.6f,param=exit?Exits.BlockClimb[parent]:0;byte drive=exit?DriveExitChase:DriveSteady;int hz0=exit?Maglev:-1;
            if(runX)for(int s=-1;s<=1;s+=2)P(s*(hx-.03f),hy-.02f,0,.05f,.03f,hz*2-.1f,LTube,Cy,col,gain,drive,hz0,param,fade:false);
            else for(int s=-1;s<=1;s+=2)P(0,hy-.02f,s*(hz-.03f),hx*2-.1f,.03f,.05f,LTube,Cy,col,gain,drive,hz0,param,fade:false);
            if(!exit)return;var x=Exits.Exits[e];
            if(runX)FaceTube(0,x.RunX>=0?-1:1,0,b.size.y-.1f,hz*2-.12f,.035f,ExitC,1.9f,DriveExitChase,Maglev,param);
            else FaceTube(2,x.RunZ>=0?-1:1,0,b.size.y-.1f,hx*2-.12f,.035f,ExitC,1.9f,DriveExitChase,Maglev,param);
        }
        // ---------------- the Cut's exits ----------------
        private void ExitSign(int e)
        {
            // The lintel over an exit door, on its face toward the Cut: a dark plate, EXIT in white under the top edge and a stair
            // pictogram rising the way the climb behind the wall goes (the world direction, so it reads true from the Cut), a white
            // line along the top, the lower edge strip where the climb's chase starts, and a light line under the lintel lighting
            // the doorway. No lime SAFE light: an alcove is somewhere to hide, this is the way out.
            var x=Exits.Exits[e];int sg=-x.Side;float len=hx*2,tall=b.size.y;
            FaceP(2,sg,0,.12f,0,len-.06f,tall-.2f,.03f,LMetal,Cy,0x0B0F1C);
            float cell=Math.Min(.075f,len*.86f/23f),word=tall-.16f-7*cell;
            Word(2,sg,"EXIT",0,word,cell,ExitC,3.2f,.06f,DriveExit,0,Maglev);
            int dir=x.RunX>=0?1:-1;float run=Math.Min(.88f,len*.44f),tread=run/4,riser=Math.Max(.05f,Math.Min(.11f,(word-.42f)/4));
            for(int k=0;k<4;k++)FaceP(2,sg,dir*(-run/2+(k+.5f)*tread),.3f,0,tread-.03f,riser*(k+1),.05f,LTube,Cy,ExitC,2.6f,DriveExit,Maglev,k);
            FaceTube(2,sg,0,tall-.075f,len-.08f,.04f,ExitC,2.0f,DriveExit,Maglev);
            FaceTube(2,sg,0,.05f,len-.08f,.06f,ExitC,2.8f,DriveExitChase,Maglev,0,true);
            P(0,-hy-.015f,0,len-.3f,.03f,hz*2-.4f,LTube,Cy,ExitC,2.2f,DriveExit,Maglev,0,fade:false);Glow(0,-hy-.3f,0,ExitC,2.4f);
        }
        private void ExitJamb(int e,bool low)
        {
            // A wall beside an exit door, on its face into the opening (seen head-on from down the Cut): a white strip up its front
            // edge, ">>" chevrons pointing through the door at chest height and a small EXIT over them.
            var x=Exits.Exits[e];int sg=low?1:-1;float open=Math.Min(b.size.y,x.OpenTop-(b.pos.y-hy));if(open<1.2f)return;
            FaceTube(0,sg,-x.Side*(hz-.04f),.08f,.06f,open-.16f,ExitC,2.8f,DriveExit,Maglev,0,true);
            float cell=Math.Min(.07f,(hz*2-.3f)/9.5f),wc=Math.Min(.042f,(hz*2-.2f)/23f);
            for(int c=0;c<2;c++)Chevron(0,sg,x.Side*(c-.5f)*4.5f*cell,1.15f,cell,x.Side,.05f);
            Word(0,sg,"EXIT",0,1.15f+7*cell+.14f,wc,ExitC,3.0f,.05f,DriveExit,0,Maglev);
        }
        private void ExitLanding(int e)
        {
            // A landing under the climb at floor level: lit floor chevrons toward the stairs (along the climb's run).
            var x=Exits.Exits[e];if(b.pos.y+hy>Exits.FloorY+.6f)return;bool alongX=Math.Abs(x.RunX)>=Math.Abs(x.RunZ);int dir=alongX?(x.RunX>=0?1:-1):(x.RunZ>=0?1:-1);
            float cell=Math.Min(.09f,Math.Min(alongX?hx*2:hz*2,alongX?hz*2:hx*2)*.8f/9.5f);
            for(int c=0;c<2;c++)
                for(int r=0;r<7;r++)
                {
                    int col=r<=3?r:6-r;float along=dir*((c-.5f)*4.5f+col+1-2.5f)*cell,across=(3-r)*cell;
                    float px=alongX?along:across,pz=alongX?across:along;
                    P(px,hy+.007f,pz,alongX?cell*1.8f:cell*.8f,.012f,alongX?cell*.8f:cell*1.8f,LTube,Cy,ExitC,1.8f,DriveExitChase,Maglev,0,fade:false);
                }
        }
        // A 5 x 7 ">" of two-cell strokes on a face, its tip toward `dir` along the face's u axis (world direction, unflipped),
        // centred at u, its bottom v up.
        private void Chevron(int axis,int sign,float u,float v,float cell,int dir,float depth)
        {
            for(int r=0;r<7;r++){int col=r<=3?r:6-r;FaceP(axis,sign,u+dir*(col+1-2.5f)*cell,v+(6-r)*cell+cell*.1f,0,cell*1.8f,cell*.8f,depth,LTube,Cy,ExitC,2.6f,DriveExit,Maglev,r);}
        }
        private void Spire(string s)
        {
            int c=Has(s,"nova")?MagentaC:CyanC;string name=Has(s,"nova")?"NOVA":"VEGA";
            for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)P(sx*(hx+.08f),hy-6.5f,sz*(hz+.08f),.18f,13f,.18f,LTube,Cy,c,2.4f);
            P(0,hy+.06f,0,hx*2+.3f,.12f,hz*2+.3f,LTube,Cy,c,2.2f);P(0,hy+3.2f,0,.3f,6.2f,.3f,LMetal,Cy,0x4A5163,shadow:true);
            int sg=b.pos.z>0?-1:1;Word(2,sg,name,0,16.5f,.2f,c,2.8f,.1f);Word(0,b.pos.x>0?-1:1,name,0,16.5f,.2f,c,2.8f,.1f);
        }
        private void RoofUnit()
        {
            TopP(0,0,hx*2-.2f,hz*2-.2f,.03f,LGrate,Cy,0x5A6070);
            for(int s=-1;s<=1;s+=2)FaceP(2,s,0,.4f,0,hx*2-.4f,hy*2-.8f,.04f,LMetal,Cy,0x5A6070);
            FaceTube(0,1,0,hy*2-.3f,.12f,.08f,LimeC,2f);
        }
        private void PodiumWall(string s)
        {
            // Exterior face: a floor-line strip at this storey's base, a thin frame at the piece's edges (framing the openings).
            bool alongX=hx>hz;int ax=alongX?2:0;float cx=Has(s,"nova")?15f:-15f,cz=Has(s,"nova")?15.5f:-15.5f;
            int sg=alongX?(b.pos.z>cz?1:-1):(b.pos.x>cx?1:-1);float len=alongX?hx*2:hz*2;bool baseStrip=Math.Abs((b.pos.y-hy)%3.6f)<.05f;
            int c=Has(s,"nova")?MagentaC:CyanC;
            if(baseStrip&&b.pos.y-hy>1)FaceTube(ax,sg,0,.02f,len,.06f,c,2f);
            if(b.size.y<2.2f)FaceP(ax,sg,0,hy*2-.06f,0,len,.06f,.05f,LMetal,Cy,0x8A93A8);
            else for(int e=-1;e<=1;e+=2)FaceP(ax,sg,e*(len/2-.03f),0,0,.06f,hy*2,.05f,LMetal,Cy,0x8A93A8);
            // A holo ribbon over the top storey's windows on the faces toward the Cut.
            if(b.size.y<1.2f&&b.pos.y>9.8f&&(Has(s,"nova-s2-south")||Has(s,"vega-s2-north")))FaceP(ax,sg,0,.2f,0,len-.3f,.55f,.05f,LHolo,Cy,c,1.2f);
        }
        private void Ceiling()
        {
            // Light panels under the floor slab (the storey below's ceiling).
            for(float x=-hx+1.5f;x<hx-1;x+=3.5f)for(float z=-hz+1.2f;z<hz-1;z+=3.5f)P(x,-hy-.025f,z,1.4f,.04f,.25f,LTube,Cy,IceC,1.4f,fade:false);
        }
        private void ParapetStrip(){bool runX=hx>hz;TopP(0,0,runX?hx*2:.07f,runX?.07f:hz*2,.03f,LTube,Cy,Accent,1.1f);}
        private void Post(string s)
        {
            if(!Has(s,"lantern")&&!Has(s,"signal")){P(0,hy-.05f,0,.25f,.1f,.25f,LTube,Cy,IceC,1.6f);return;}
            // Market posts carry the lantern strings toward the next post along the market (its east neighbour) and the open
            // canopy lattice at 5.4 m (girders only: under 20% of the sky, so it hides no one).
            int n=North;float y=hy;bool east=n>0?b.pos.x<-14:b.pos.x>14,far=Math.Abs(b.pos.z)>15;
            if(east)for(int k=0;k<10;k++){float t=(k+.5f)/10f,sag=.5f*4*t*(1-t);P(n*11.3f*t,y-.9f-sag,0,1.14f,.03f,.03f,LMetal,Cy,0x222630);if(k%2==0){int c=Pick(new[]{0xFF3B2F,0xFF6A4D,MagentaC,0xFFB45E},k,(int)(b.pos.x*10));P(n*11.3f*t,y-1.25f-sag,0,.32f,.42f,.32f,NativeBlockLooks.Neon,0,c,2.2f);Glow(n*11.3f*t,y-1.25f-sag,0,c,1.4f);}}
            if(east)P(n*5.65f,y+.1f,0,11.3f,.16f,.16f,LMetal,Cy,0x2A2E3A);if(far)P(0,y+.1f,n*-5.3f,.16f,.16f,10.6f,LMetal,Cy,0x2A2E3A);
        }
        private void Bar()
        {
            int sg=b.pos.z>0?-1:1;FaceP(2,sg,0,.9f,0,hx*2-1,1.6f,.05f,NativeBlockLooks.WindowLit,0,0x6E8F96);
            FaceP(2,sg,0,2.45f,0,hx*2-.8f,.35f,.08f,NativeBlockLooks.Neon,0,0xFF3B2F,1.4f);
            Word(2,sg,"NOODLE",0,3.25f,.1f,b.pos.z>0?MagentaC:CyanC,2.8f);
        }
        private void Booth()
        {
            int sg=b.pos.x<0?1:-1;Word(0,sg,"GAME",0,3.25f,.11f,0xFFE14D,2.6f,drive:DriveFlicker,param:3);
            FaceP(0,sg,0,.3f,0,hz*2-.8f,2.2f,.05f,LHolo,Cy,VioletC,1.1f);
        }
        private void Container(string s)
        {
            int sg=b.pos.z>0?-1:1;FaceP(2,sg,0,.1f,0,.06f,hy*2-.2f,.06f,LMetal,Cy,0x1F2330);
            if(b.pos.y-hy>2)Word(2,sg,"CAPSULE",0,hy*2-1.0f,.09f,LimeC,2.4f,drive:DriveFlicker,param:7);
        }
        private void Stall()
        {
            // A board counter top, two small lanterns over its corners (inside its footprint) and a menu screen.
            TopP(0,0,hx*2+.1f,hz*2+.1f,.05f,NativeBlockLooks.Timber,0,0x8A5A3A);
            for(int s=-1;s<=1;s+=2){P(s*(hx-.2f),hy+.75f,0,.04f,1.4f,.04f,LMetal,Cy,0x222630);P(s*(hx-.2f),hy+1.35f,0,.26f,.32f,.26f,NativeBlockLooks.Neon,0,s<0?0xFF3B2F:0xFFB45E,2.2f);}
            int sg=b.pos.z>0?-1:1;FaceP(2,sg,0,.2f,0,hx*2-.6f,.5f,.04f,LHolo,Cy,Pick(Neons,(int)(b.pos.x*3),2),1.1f);
        }
        private void Vending()
        {
            int ax=hx<hz?0:2,sg=ax==0?(b.pos.x<0?1:-1):(b.pos.z<0?1:-1);
            FaceP(ax,sg,0,.25f,0,(ax==0?hz:hx)*2-.2f,1.4f,.04f,LHolo,Cy,H(1)<.5?CyanC:0xFF3B2F,1.2f);FaceTube(ax,sg,0,1.75f,(ax==0?hz:hx)*2-.2f,.06f,IceC,2f);
        }
        private void Vehicle(string s)
        {
            bool alongX=hx>=hz;int ax=alongX?0:2;float len=alongX?hx*2:hz*2,wid=alongX?hz*2:hx*2;
            // Window band, head and tail lights on the ends, an underglow strip, a roof light bar on cabs.
            for(int sg=-1;sg<=1;sg+=2){FaceP(alongX?2:0,sg,0,hy*2-.85f,0,len-1,.55f,.04f,LGlass,Cy,0x101626);}
            FaceTube(ax,1,0,hy*.6f,wid-.4f,.12f,IceC,3f,glow:true);FaceTube(ax,-1,0,hy*.6f,wid-.4f,.12f,RedC,2.6f,glow:true);
            // Underglow: a strip round the base (the body hovers over its glow) and its light on the street.
            int under=Has(s,"cab")?0xFFE14D:CyanC;for(int sg=-1;sg<=1;sg+=2){P(alongX?0:sg*(hx+.02f),-hy+.06f,alongX?sg*(hz+.02f):0,alongX?len-.4f:.04f,.06f,alongX?.04f:len-.4f,LTube,Cy,under,2.6f,fade:false);}
            Glow(0,-hy+.05f,0,under,Math.Max(len,wid)*.8f);
            if(Has(s,"cab")){TopP(0,0,alongX?.8f:.3f,alongX?.3f:.8f,.18f,LTube,Cy,0xFFE14D,2.2f);}
        }
        private void Kiosk()
        {
            int sg=b.pos.z>0?-1:1;FaceP(2,sg,0,.9f,0,hx*2-.6f,1.1f,.04f,LHolo,Cy,Pick(Neons,(int)b.pos.x,4),1.1f);FaceP(2,sg,0,.05f,0,hx*2-.4f,.8f,.08f,LMetal,Cy,0x5A6070);
            TopP(0,0,hx*2,hz*2,.05f,LTube,Cy,IceC,1.4f);
            Word(2,sg,"24H",0,hy*2+.05f,.07f,LimeC,2.4f,drive:DriveFlicker,param:5);
        }
        private void Pillar()
        {
            for(int s=-1;s<=1;s+=2)FaceP(0,s,0,.4f,0,hz*2-.08f,hy*2-.8f,.03f,LHolo,Cy,Pick(Neons,(int)(b.pos.x+b.pos.z),s+3),1.1f);
            TopP(0,0,hx*2,hz*2,.06f,LTube,Cy,IceC,1.8f);
        }
        private void Shelter()
        {
            int sg=b.pos.z>0?1:-1;
            TopP(0,0,hx*2+.4f,hz*2+.4f,.12f,LMetal,Cy,0x2A2E3A);FaceP(2,-sg,0,.4f,0,hx*2-.8f,1.8f,.04f,LHolo,Cy,Pick(Neons,(int)b.pos.x,9),1.1f);
            P(0,hy-.02f,0,hx*2,.03f,.05f,LTube,Cy,IceC,1.6f);
        }
        private void Planter()
        {
            for(int i=0;i<3;i++){double h=H(i);P((float)((h-.5)*hx*1.2f),hy+.1f,(float)((H(i,3)-.5)*hz*.8f),(float)(.6+h*.5),.2f,(float)(.5+H(i,4)*.4),NativeBlockLooks.Plain,0,0x1E3A2C,fade:false);}
            for(int s=-1;s<=1;s+=2)FaceTube(2,s,0,hy*2-.08f,hx*2-.2f,.04f,s>0?CyanC:MagentaC,1.6f);
        }
        private void Lamp()
        {
            // The arm and the light bar at the top, leaning out toward the street's middle (above 3 m).
            int sz=b.pos.z>0?(b.pos.z>26?-1:1):(b.pos.z<-26?1:-1);
            P(0,hy-.15f,sz*.6f,.12f,.12f,1.2f,LMetal,Cy,0x2A2E3A);P(0,hy-.24f,sz*1.05f,.22f,.06f,.7f,LTube,Cy,IceC,3f);Glow(0,hy-.3f,sz*1.05f,IceC,1.8f);
        }
        private void Strut(){P(0,hy+.05f,0,hx*2+.4f,.1f,hz*2+.4f,LMetal,Cy,0x4A5163);}
        private void Bezel(){int sg=b.pos.z>0?-1:1;FaceTube(2,sg,0,0,b.size.x>b.size.z?hx*2:.06f,b.size.y,VioletC,2f);}
        private void Canopy()
        {
            for(float x=-hx+1;x<hx;x+=1.5f)P(x,-hy-.025f,0,.08f,.04f,hz*2-.6f,LTube,Cy,IceC,1.6f,fade:false);
            // The rank's sign stands on the canopy's front edge (above 3 m: nothing hangs into the yard).
            Word(2,b.pos.z>0?-1:1,b.pos.z>0?"TAXI":"DOCK",0,hy*2+.04f,.1f,0xFFE14D,2.4f);
        }
        private void Charger(){FaceTube(0,1,0,1.2f,hz*2-.2f,.6f,LimeC,1.6f);FaceTube(0,-1,0,1.2f,hz*2-.2f,.6f,LimeC,1.6f);TopP(0,0,hx*2,hz*2,.08f,LTube,Cy,CyanC,1.8f);}
        private void YardWall()
        {
            bool alongX=hx>hz;FaceTube(alongX?2:0,1,0,hy*2-.15f,(alongX?hx:hz)*2,.05f,Accent,1.6f);FaceTube(alongX?2:0,-1,0,hy*2-.15f,(alongX?hx:hz)*2,.05f,Accent,1.6f);
        }

        // ---------------- the maglev train (train frame: +x the travel direction, origin at the nose on the guideway top) ----------------
        /// <summary>The train's height above the trench floor at the guideway top (its rest Y in the train frame is 0 there).</summary>
        public const float RailTop=-3.7f;
        private static NativeCyberCityPart TP(float x,float y,float z,float sx,float sy,float sz,int look,int flags,int color,float gain=1,bool shadow=false)
        =>new NativeCyberCityPart{Parent=-2,X=x,Y=y,Z=z,Hx=sx/2,Hy=sy/2,Hz=sz/2,Look=look,Flags=flags,Color=color,Gain=gain,Shadow=shadow};
        private static NativeCyberCityPart[] BuildTrain()
        {
            // Three cars (14, 13, 14 m; .5 m gaps: 42 m), 4.9 m wide, a graphite body .3..2.6 m above the guide beam with a lit window
            // band, a cyan light line along each skirt and a violet one along the roof edge; a six-step tapered nose and tail with a
            // dark windscreen, an ice-white headlight bar (red at the tail) and the levitation glow under the skirt.
            var t=new List<NativeCyberCityPart>();float[] cars={14,13,14};float x=0;const int Body=0x2A2F3C,Roof=0x3A3F4E;
            for(int c=0;c<3;c++)
            {
                float len=cars[c],x0=x-len,x1=x;bool nose=c==0,tail=c==2;float body0=x0+(tail?3.0f:0),body1=x1-(nose?3.0f:0),mid=(body0+body1)/2,bl=body1-body0;
                t.Add(TP(mid,1.45f,0,bl,2.3f,4.9f,LMetal,Cy,Body,1,true));t.Add(TP(mid,2.66f,0,bl-.6f,.12f,4.3f,LMetal,Cy,Roof));
                for(int s=-1;s<=1;s+=2)
                {
                    t.Add(TP(mid,1.85f,s*2.46f,bl-1.0f,.62f,.04f,LGlass,Cy,0x101626));t.Add(TP(mid,.55f,s*2.47f,bl,.06f,.05f,LTube,Cy,CyanC,3.2f));t.Add(TP(mid,2.58f,s*2.2f,bl-.6f,.04f,.04f,LTube,Cy,VioletC,2.6f));
                    for(float d=body0+1.2f;d<body1-1;d+=4.2f)t.Add(TP(d,1.4f,s*2.47f,.9f,1.9f,.03f,LMetal,Cy,0x1C2029));
                }
                t.Add(TP(mid,.2f,0,bl-.2f,.2f,4.2f,LMetal,Cy,0x1A1D26));t.Add(TP(mid,.08f,0,bl-.6f,.04f,3.8f,LTube,Cy,VioletC,2.8f));
                if(nose||tail)
                {
                    int sg=nose?1:-1;float start=nose?body1:body0;
                    for(int k=0;k<6;k++){float l=.5f,a=start+sg*(k*l+l/2),h=2.3f-k*.3f,w=4.9f-k*.42f;t.Add(TP(a,.3f+h/2,0,l,h,w,LMetal,Cy,Body,1,true));}
                    float end=start+sg*3.0f;
                    t.Add(TP(start+sg*1.6f,1.95f,0,1.2f,.5f,3.4f,LGlass,Cy,0x0C1020));
                    t.Add(TP(end-sg*.4f,.95f,0,.06f,.14f,2.4f,LTube,Cy,nose?IceC:RedC,5f));
                    for(int s=-1;s<=1;s+=2)t.Add(TP(end-sg*.9f,.62f,s*1.15f,.06f,.12f,.7f,LTube,Cy,nose?IceC:RedC,5f));
                }
                x=x0-.5f;
            }
            return t.ToArray();
        }
        /// <summary>World centre of train part `p` for a head at x `head` travelling `direction` (+1 east) on the track at z `track`.</summary>
        public static void TrainPlace(in NativeCyberCityPart p,double head,int direction,float track,out float x,out float y,out float z)
        {x=(float)(head+direction*p.X);y=RailTop+p.Y;z=track+direction*p.Z;}

        // ---------------- drives (pure) ----------------
        /// <summary>
        /// A part's linear colour (rgb, the emission gain folded in) and light level this frame (the shader's window value: 1 lit,
        /// 0 dark; above 1 glows a grate) from its drive, the hazard states (indexed like the hazards Build took; null: idle) and
        /// the presentation clock. Steady parts keep their colour; flickering signs stutter on a seeded schedule; the maglev's
        /// signals show clear (cyan) / warning (amber) / danger (red, 4 Hz); the coils race toward the approaching end through the
        /// warning and blaze under the train; the edge strips turn amber then strobe red; the SAFE lights brighten; the EXIT signs
        /// and frames breathe and brighten from the warning, the climb lights chase up the stairs (faster from the warning); a coupling's
        /// rails idle faintly, crawl brighter while charging and blaze white-blue (30 Hz broken) while live, its lamps amber in the
        /// charge and red live.
        /// </summary>
        public static void Drive(in NativeCyberCityPart p,NativeHazardState[] states,double time,out float r,out float g,out float bl,out float level)
        {
            int hex=p.Color;float gain=p.Gain;level=1;
            NativeHazardState s=p.Hazard>=0&&states!=null&&p.Hazard<states.Length?states[p.Hazard]:default;var ph=s.Phase;
            switch(p.Drive)
            {
                case DriveFlicker:{double u=time+p.Param*1.7;double k=Math.Floor(u*9);double w=u%(9.0+p.Param);level=w<.5&&Hash01((int)k,(int)p.Param)<.55?.08f:1;break;}
                case DriveSignal:{int want=ph==NativeHazardPhase.Danger||ph==NativeHazardPhase.Active?2:ph==NativeHazardPhase.Warn?1:0;level=(int)p.Param==want?(want==2&&Strobe(time,4)<.5?.25f:1):.04f;break;}
                case DriveCoil:
                {
                    // Idle: a faint cyan breath. Warning: pulses run from the approaching end toward the train's far end, faster in
                    // the danger. Pass: blazing within 12 m of the train. Clear: fades.
                    double x=p.Param;level=.18f+.06f*(float)Math.Sin(time*1.3+x*.15);
                    if(s.Telegraph&&s.Direction!=0){double from=-s.Direction*40,d=(x-from)*s.Direction,speed=ph==NativeHazardPhase.Danger?36:18,wave=Frac((time*speed-d)/16);level=.25f+(ph==NativeHazardPhase.Danger?1.6f:.9f)*(float)Math.Pow(1-wave,6)*s.Level;}
                    else if(ph==NativeHazardPhase.Active&&!double.IsNaN(s.Head)){double lo=Math.Min(s.Head,s.Tail)-12,hi=Math.Max(s.Head,s.Tail)+12;level=x>=lo&&x<=hi?2.2f:.3f;}
                    else if(ph==NativeHazardPhase.Clear)level=.18f+1.2f*s.Level;
                    break;
                }
                case DriveEdge:
                    if(ph==NativeHazardPhase.Warn){hex=AmberC;level=.7f+.3f*(float)Math.Sin(time*6.3);}
                    else if(ph==NativeHazardPhase.Danger||ph==NativeHazardPhase.Active){hex=RedC;level=Strobe(time,4)<.5?1.4f:.15f;}
                    else level=.55f;
                    break;
                case DriveSafe:level=s.Telegraph||ph==NativeHazardPhase.Active?1.3f+.5f*(float)Math.Sin(time*8):.75f;break;
                case DriveGridBar:
                    if(ph==NativeHazardPhase.Warn){double crawl=Frac(time*2.2+p.Param*.25);level=.15f+1.1f*s.Level*(float)(.55+.45*Math.Sin(crawl*6.2831853));hex=0x7FE8FF;}
                    else if(ph==NativeHazardPhase.Active){hex=0xE6F7FF;level=Hash01((int)Math.Floor(time*30),(int)p.Param)<.2?.6f:2.4f;}
                    else if(ph==NativeHazardPhase.Clear){hex=0x7FE8FF;level=.12f+.9f*s.Level;}
                    else level=.1f;
                    break;
                case DriveGridDeck:level=DeckGlow(s,time);break;
                case DriveExit:level=s.Telegraph||ph==NativeHazardPhase.Active?1.3f+.25f*(float)Math.Sin(time*8):.92f+.08f*(float)Math.Sin(time*1.7+p.Param);break;
                case DriveExitChase:
                {
                    // A pulse up the climb every 1.8 s (0.9 s from the warning on, brighter), Param the part's height up it.
                    bool alarm=s.Telegraph||ph==NativeHazardPhase.Active;double u=Frac(time*(alarm?1.1:.55)-p.Param);level=(.4f+1.15f*(float)Math.Pow(1-u,4))*(alarm?1.3f:1);break;
                }
                case DriveGridLamp:
                    if(ph==NativeHazardPhase.Warn){hex=AmberC;level=Strobe(time,3)<.5?1.2f:.2f;}
                    else if(ph==NativeHazardPhase.Active){hex=RedC;level=1.5f;}
                    else{hex=CyanC;level=.5f;}
                    break;
            }
            r=(float)NativeSceneryBuilder.Channel(hex,16)*gain;g=(float)NativeSceneryBuilder.Channel(hex,8)*gain;bl=(float)NativeSceneryBuilder.Channel(hex,0)*gain;
        }
        private static double Frac(double v)=>v-Math.Floor(v);
        private static float Strobe(double time,double hz)=>(float)Frac(time*hz);
        private static double Hash01(int a,int c)=>Mix(Mix((uint)a*0x9E3779B9U)^(uint)c*0x85EBCA6BU)/4294967296.0;
        /// <summary>The grate glow a coupling deck's own block takes (the shader's light level above 1): idle none, charge rising, live
        /// full with the bar flicker, discharge falling.</summary>
        public static float DeckGlow(in NativeHazardState s,double time)=>s.Phase==NativeHazardPhase.Warn?1+.6f*s.Level:s.Phase==NativeHazardPhase.Active?(Hash01((int)Math.Floor(time*30),7)<.2?1.4f:2.2f):s.Phase==NativeHazardPhase.Clear?1+.6f*s.Level:1;
    }
}
