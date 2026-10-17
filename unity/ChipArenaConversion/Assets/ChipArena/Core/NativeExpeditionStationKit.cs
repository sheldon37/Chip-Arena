using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// One station dressing part (presentation only, never sim data): a box in its parent block's rest frame (centre offset in
    /// the block's own axes, half extents, its own turn in that frame), drawn through NativeBlockLit like an arena block. `Look`
    /// and `Flags` as NativeBlockLooks.Resolve gives them (a shared bank slot carries NativeCyberCityLooks.Bank), `Color` sRGB,
    /// `Gain` the emission scale (HDR above 1), `Level` the window light the shader reads, `Drive` what animates it
    /// (<see cref="NativeExpeditionStationKit.Drive"/>, its seed in `Param`, its subject in `Tag`: a board row and variant, a
    /// rail section, a cabinet, a signal's aspect). `Role` says what it is for (the checks hold each role to its rule), `Room`
    /// where it is (the budget lines), `Tier` which graphics tier draws it (<see cref="NativeExpeditionStationKit.TierAll"/>,
    /// TierFull: detail the low tier and compatibility rendering leave out, TierLow: its cheap stand-in). `Shadow`: big parts
    /// that cast. `Fade`: hidden while it sits between the camera and the pilot.
    /// </summary>
    public struct NativeStationPart
    {
        public int Parent,Look,Flags,Color;public float X,Y,Z,Hx,Hy,Hz,Qx,Qy,Qz,Qw,Gain,Level,Param;public byte Drive,Role,Room,Tier;public short Tag;public bool Shadow,Fade;
    }
    /// <summary>A light or effect source of the station: world place, linear colour times intensity, its light's reach (m), the
    /// size of its halo, its seed, the floor under it, its kind (<see cref="NativeExpeditionStationKit"/> Glow* constants), its
    /// room and its subject (a rail section, a signal's line and side, a cabinet).</summary>
    public struct NativeStationGlow {public float X,Y,Z,R,G,B,Range,Size,Param,Floor;public byte Kind,Room;public short Tag;}
    /// <summary>A light shaft (moonlight through a broken pane or a skylight, a lamp's cone): from (X0, Y0, Z0) to (X1, Y1, Z1),
    /// its width at each end, its strength and kind.</summary>
    public struct NativeStationShaft {public float X0,Y0,Z0,X1,Y1,Z1,W0,W1,Alpha;public byte Kind,Room;}
    /// <summary>A still water surface (the flood, the pump room's floor, a puddle): an axis box at height Y, its kind.</summary>
    public struct NativeStationWater {public float X0,X1,Z0,Z1,Y;public byte Kind;}
    /// <summary>A keep-out box the kit honoured (world AABB) and the spawn whose own dressing may enter it (-1 none).</summary>
    public struct NativeStationKeep {public float X0,X1,Y0,Y1,Z0,Z1;public short Spawn;public byte Kind;}

    /// <summary>
    /// Central Station's dressing kit (docs/EXPEDITION-CENTRAL-STATION.md sections 3-6, the ranked gaps of section 4): the
    /// station's set pieces laid on the draft map's blocks by id stem (docs/arenas/scripts/expedition_central_station.py), with
    /// the area's records kept clear. Built once per map content; nothing here collides, moves a block or enters the network
    /// frame, and every other map's blocks give no parts (<see cref="NativeExpeditionStation.Active"/> gates the runtime too).
    /// <list type="bullet">
    /// <item>The concourse: the broken glass vault (arched ribs, ties, purlins, glazing, missing panes with hanging shards and
    /// the moon falling through), the glazed screen and clerestory, the CENTRAL sign and the 6:47 clock, the four-faced
    /// departure board with live rows (<see cref="NativeStationState"/>: APPROACHING, SUSPENDED, LAST CALL, ALL SERVICES
    /// CANCELLED), its paged ticker and side faces, the booth, the burnt cafe and the newsagent, the balconies' globes and
    /// balusters, the field hospital, the platform gates' signs, terrazzo, puddles under the broken panes, glass grit.</item>
    /// <item>The ticket hall: troffers (some dead, some stuttering), cream tile and the green dado, the ticket windows, the
    /// machines, the turnstiles' red lamps, sandbags, the evacuation checkpoint, the roundel, WAY OUT, the route map.</item>
    /// <item>The shed: trusses and skylights, sodium pendants, the platforms' coping, yellow lines and the pits' red danger
    /// strips, signs in the line colours, the tracks (rails, sleepers, the live rail on its insulators with the yellow guard
    /// board), the barrow crossings, the tunnel mouths and their signals, beacons and STAND BACK / NO ENTRY signs, the stalled
    /// train (windows, the red band, doors, NOT IN SERVICE, car 2's lit interior), the derailed wagon, the flood, the
    /// footbridges' lattice and lamps, the signal box (red windows, CENTRAL SIGNAL BOX, the lever frame, the track diagram),
    /// the corridor's pipes, trays, red strips, generator and the steam main.</item>
    /// <item>Entrances, from the area's spawns: a staff doorway in its frame for each door spawn on a wall, a carriage door for
    /// each train door, a drain grate for each vent, the bricked arches' frames.</item>
    /// <item>The express (<see cref="Express"/>): three stainless cars in the train's own frame, posed by the state's nose.</item>
    /// </list>
    /// Placement rules (Tests/ExpeditionStationVisualChecks.cs): below 3.1 m (on each level) nothing stands out more than
    /// 0.25 m from a block except flat paint; on a walkable top a free-standing part below 2.2 m is at most 0.65 m tall; nothing
    /// lies on a spawn's exit or path (track parts aside: the zombies walk the rails), a vent, a drop column, an entry, an
    /// interaction or boss point or the exit door's volumes. Pure: the same blocks and area give the same parts in the same
    /// order; <see cref="Drive"/> is a pure function of the part, the state and the clock. Allocation free after Build.
    /// </summary>
    public sealed partial class NativeExpeditionStationKit
    {
        public const byte DriveSteady=0,DriveFlicker=1,DriveFluor=2,DriveFire=3,DriveBoard=4,DriveTicker=5,DriveSignal=6,DriveDanger=7,DriveRail=8,DriveFlood=9,DriveCabinet=10,
            DriveWindow=11,DriveLever=12,DriveBox=13,DriveStrip=14,DriveExpress=15,DriveBeacon=16,DriveMouth=17,DriveDiagram=18,DriveDead=19;
        public const byte RoleWall=0,RoleFlat=1,RoleClutter=2,RoleHigh=3,RoleTrack=4,RoleSpawn=5,RoleBoard=6,RoleSign=7,RoleLamp=8,RoleTrain=9,RoleWater=10,RoleFurniture=11;
        public const byte RoomHall=0,RoomConcourse=1,RoomShed=2,RoomCorridor=3,RoomBox=4,RoomUpper=5,Rooms=6;
        public static readonly string[] RoomNames={"ticket hall","concourse","shed","corridor","signal box","upper floor"};
        public const byte TierAll=0,TierFull=1,TierLow=2;
        public const byte GlowLamp=0,GlowLampFlicker=1,GlowTube=2,GlowTubeFlicker=3,GlowBoard=4,GlowSign=5,GlowFire=6,GlowGlobe=7,GlowWindow=8,GlowCold=9,GlowBox=10,GlowStrip=11,
            GlowSignal=12,GlowBeacon=13,GlowWater=14,GlowCabinet=15,GlowWarm=16,GlowGenerator=17,GlowScreen=18;
        public const byte ShaftMoon=0,ShaftLamp=1;
        public const byte WaterFlood=0,WaterPump=1,WaterPuddle=2;
        /// <summary>How far below 3.1 m a part may stand out from a block (m), the most flat paint may rise (m), the height under
        /// which the wall band applies, and the tallest free-standing clutter on a walkable top below <see cref="Clutter"/>.</summary>
        public const float WallBand=.25f,Flat=.06f,Low=3.1f,ClutterMax=.65f,Clutter=2.2f;
        /// <summary>The kit's budget per room (doc section 11; the concourse carries the departure board's live text, about 2,250
        /// glyph parts the design's estimate left out, so it takes part of the shed's share), the instanced draws (the kit, the
        /// express and the atmosphere's two) and the light slots (the low tier keeps four).</summary>
        public static readonly int[] RoomBudget={900,3800,3400,700,350,600};
        public const int DrawBudget=22,Slots=NativeMapLooks.MaxLights,LowSlots=4;

        public NativeStationPart[] Parts=Array.Empty<NativeStationPart>(),Express=Array.Empty<NativeStationPart>();
        public NativeStationGlow[] Glows=Array.Empty<NativeStationGlow>();public NativeStationShaft[] Shafts=Array.Empty<NativeStationShaft>();
        public NativeStationWater[] Waters=Array.Empty<NativeStationWater>();public NativeStationKeep[] Keeps=Array.Empty<NativeStationKeep>();
        /// <summary>Parts per room (full tier), casters, parts the low tier draws; and the parts the keep-outs refused.</summary>
        public readonly int[] RoomParts=new int[Rooms];public int Casters,LowParts,Rejected;
        /// <summary>Where the third rail's sections run (x0, x1, z, top y per section; NaN x when a section has no rail here).</summary>
        public readonly float[] RailX0=new float[NativeExpeditionStation.RailSections],RailX1=new float[NativeExpeditionStation.RailSections],RailZ=new float[NativeExpeditionStation.RailSections];
        /// <summary>The spark cabinets (P2, P3) and the steam main's place (x, y, z).</summary>
        public readonly float[] Cabinet=new float[6],SteamMain=new float[3];

        private readonly List<NativeStationPart> parts=new List<NativeStationPart>(),express=new List<NativeStationPart>();private readonly List<NativeStationGlow> glows=new List<NativeStationGlow>();
        private readonly List<NativeStationShaft> shafts=new List<NativeStationShaft>();private readonly List<NativeStationWater> waters=new List<NativeStationWater>();private readonly List<NativeStationKeep> keeps=new List<NativeStationKeep>();
        private ContentBlock[] blocks;private ExpeditionAreaLayout area;private readonly Dictionary<string,int> byStem=new Dictionary<string,int>(StringComparer.Ordinal);
        private int groupStart=-1,glowStart=-1;private bool groupBad;private short owner=-1;

        // ---------------- palette (sRGB; doc section 3) ----------------
        public const int TealHex=0x7AE8D0,CreamHex=0xC8BCA0,GreenHex=0x3E5A4E,IronHex=0x24342E,SodiumHex=0xFF9E3A,AmberHex=0xFFB45A,MoonHex=0x8FAEDC,ShaftHex=0x9ABCE8,
            RedHex=0xFF2A22,FluorHex=0xD8FFF0,BrassHex=0xC8A040,GlassHex=0x16222C,GlassDeepHex=0x0E161E,BlackHex=0x040506,PaperHex=0xC8C0AC,WaterHex=0x0A2224,
            SteelHex=0x8A9AA0,RustHex=0x6A3A22,YellowHex=0xD8B000,CopingHex=0xB8B4A8,SignalGreenHex=0x3AFF6A,BeaconHex=0xFFA020,HeadlightHex=0xF2F6FF;
        public static readonly int[] LineHex={0xD8302A,0x2A8AD8,0xE8B030,0x3AB04A};

        // ---------------- hashing (stable across machines) ----------------
        private static uint Mix(uint x){unchecked{x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x;}}
        private static uint Fnv(string s){unchecked{uint h=2166136261;if(s!=null)foreach(char c in s)h=(h^c)*16777619;return h;}}
        /// <summary>A stable hash in [0, 1) of a name and two integers.</summary>
        public static double Hash(string s,int a=0,int c=0){unchecked{return Mix(Fnv(s)^Mix((uint)a*0x9E3779B9U+(uint)c*0x85EBCA6BU))/4294967296.0;}}
        /// <summary>The id with the station prefix removed, or null for other maps' ids.</summary>
        public static string Stem(string id)=>id!=null&&id.StartsWith(NativeExpeditionStation.Prefix,StringComparison.Ordinal)?id.Substring(NativeExpeditionStation.Prefix.Length):null;
        private int B(string stem)=>byStem.TryGetValue(stem,out int i)?i:-1;
        private bool Has(string stem)=>byStem.ContainsKey(stem);

        // ---------------- build ----------------
        /// <summary>Builds the kit for the station's blocks and its area (either may be null: no entrance dressing and no
        /// keep-outs without the area). Another map's blocks give an empty kit.</summary>
        public static NativeExpeditionStationKit Build(ContentBlock[] blocks,ExpeditionAreaLayout area=null)
        {
            var k=new NativeExpeditionStationKit{blocks=blocks??Array.Empty<ContentBlock>(),area=area};
            for(int i=0;i<k.blocks.Length;i++){var s=Stem(k.blocks[i]?.id);if(s!=null&&!k.byStem.ContainsKey(s))k.byStem[s]=i;}
            for(int s=0;s<NativeExpeditionStation.RailSections;s++){k.RailX0[s]=k.RailX1[s]=float.NaN;}
            if(k.byStem.Count==0||!k.Has("track-bed"))return k;
            k.KeepOuts();
            k.Concourse();k.Hall();k.Upper();k.Shed();k.Tracks();k.Train();k.Bridges();k.Corridor();k.SignalBox();k.Spawns();
            k.BuildExpress();
            k.Parts=k.parts.ToArray();k.Express=k.express.ToArray();k.Glows=k.glows.ToArray();k.Shafts=k.shafts.ToArray();k.Waters=k.waters.ToArray();k.Keeps=k.keeps.ToArray();
            k.parts.Clear();k.express.Clear();k.glows.Clear();k.shafts.Clear();k.waters.Clear();
            foreach(var p in k.Parts){if(p.Tier!=TierLow)k.RoomParts[p.Room]++;if(p.Tier!=TierFull)k.LowParts++;if(p.Shadow&&p.Tier!=TierLow)k.Casters++;}
            return k;
        }
        /// <summary>The room a world point is in (the budget lines; doc section 11's rooms).</summary>
        public static byte RoomOf(double x,double y,double z)
        {
            if(x>=29.5&&x<=42.5&&z>=-60.5&&z<=-49.5&&y>=5.4)return RoomBox;
            if(z<-53.4&&y<5.4)return RoomCorridor;
            if(z>=20&&y<5.6)return RoomHall;
            if(z>=20)return y>10.6?RoomConcourse:RoomUpper;
            if(y>=5.4&&y<9.6&&(z>=-4.6&&Math.Abs(x)>=25.5||z<-4.6&&Math.Abs(x)>=31.5&&Math.Abs(x)<=36.5))return RoomUpper;
            if(z>=-4.6)return RoomConcourse;
            return RoomShed;
        }

        // ---------------- quaternions ----------------
        private struct Rot {public double X,Y,Z,W;public static readonly Rot None=new Rot{W=1};}
        private static void Mul(double ax,double ay,double az,double aw,double bx,double by,double bz,double bw,out double x,out double y,out double z,out double w)
        {x=aw*bx+ax*bw+ay*bz-az*by;y=aw*by-ax*bz+ay*bw+az*bx;z=aw*bz+ax*by-ay*bx+az*bw;w=aw*bw-ax*bx-ay*by-az*bz;}
        private static Rot Then(Rot a,Rot b){Mul(a.X,a.Y,a.Z,a.W,b.X,b.Y,b.Z,b.W,out double x,out double y,out double z,out double w);return new Rot{X=x,Y=y,Z=z,W=w};}
        private static Rot Axis(double ax,double ay,double az,double deg){double h=deg*Math.PI/360,s=Math.Sin(h);return new Rot{X=ax*s,Y=ay*s,Z=az*s,W=Math.Cos(h)};}
        /// <summary>Unity's Quaternion.Euler(pitch, yaw, roll): roll about z, then pitch about x, then yaw about y.</summary>
        private static Rot Euler(double yaw,double pitch,double roll){var q=Rot.None;if(yaw!=0)q=Axis(0,1,0,yaw);if(pitch!=0)q=Then(q,Axis(1,0,0,pitch));if(roll!=0)q=Then(q,Axis(0,0,1,roll));return q;}
        /// <summary>v turned by the unit quaternion (x, y, z, w).</summary>
        public static void Rotate(double qx,double qy,double qz,double qw,double vx,double vy,double vz,out double ox,out double oy,out double oz)
        {double tx=2*(qy*vz-qz*vy),ty=2*(qz*vx-qx*vz),tz=2*(qx*vy-qy*vx);ox=vx+qw*tx+(qy*tz-qz*ty);oy=vy+qw*ty+(qz*tx-qx*tz);oz=vz+qw*tz+(qx*ty-qy*tx);}
        private static Rot BlockRot(ContentBlock b){var r=b.rotation;double x=r?.x??0,y=r?.y??0,z=r?.z??0,w=r?.w??1,l=Math.Sqrt(x*x+y*y+z*z+w*w);if(!(l>1e-9))return Rot.None;return new Rot{X=x/l,Y=y/l,Z=z/l,W=w/l};}
        /// <summary>A turned box's world AABB.</summary>
        public static void Extent(double x,double y,double z,double hx,double hy,double hz,double qx,double qy,double qz,double qw,out double x0,out double x1,out double y0,out double y1,out double z0,out double z1)
        {
            x0=y0=z0=double.MaxValue;x1=y1=z1=double.MinValue;
            for(int k=0;k<8;k++){Rotate(qx,qy,qz,qw,(k&1)==0?-hx:hx,(k&2)==0?-hy:hy,(k&4)==0?-hz:hz,out double ox,out double oy,out double oz);x0=Math.Min(x0,x+ox);x1=Math.Max(x1,x+ox);y0=Math.Min(y0,y+oy);y1=Math.Max(y1,y+oy);z0=Math.Min(z0,z+oz);z1=Math.Max(z1,z+oz);}
        }
        /// <summary>A part's world centre and turn on its parent block at rest.</summary>
        public static void World(ContentBlock b,in NativeStationPart p,out double x,out double y,out double z,out double qx,out double qy,out double qz,out double qw)
        {
            var q=BlockRot(b);Rotate(q.X,q.Y,q.Z,q.W,p.X,p.Y,p.Z,out double ox,out double oy,out double oz);x=b.pos.x+ox;y=b.pos.y+oy;z=b.pos.z+oz;
            Mul(q.X,q.Y,q.Z,q.W,p.Qx,p.Qy,p.Qz,p.Qw,out qx,out qy,out qz,out qw);
        }

        // ---------------- the pen ----------------
        private struct Pen {public int Look,Flags,Color;public float Gain,Level,Param;public byte Drive,Role,Tier;public short Tag;public bool Shadow,Fade;}
        private static Pen P(int look,int color,byte role=RoleWall,int flags=0)=>new Pen{Look=look,Color=color,Role=role,Flags=flags,Gain=1,Level=1};
        /// <summary>A base look by key ("steel-green") or a bank key ("cyber-tube", "ward-tile"): the id and the bank flag.</summary>
        private static Pen L(string key,int color,byte role=RoleWall)
        {int id=NativeBlockLooks.Id(key);if(id<0)throw new ArgumentException("unknown look "+key);return id>=NativeCyberCityLooks.Offset?P(id-NativeCyberCityLooks.Offset,color,role,NativeCyberCityLooks.Bank):P(id,color,role);}
        private static Pen Glow(int color,float gain,byte role=RoleWall,byte drive=DriveSteady,float param=0,short tag=-1)
        {var p=L("cyber-tube",color,role);p.Gain=gain;p.Drive=drive;p.Param=param;p.Tag=tag;return p;}
        private static Pen With(Pen p,bool shadow=false,bool fade=false,bool detail=false,byte tier=255,byte drive=255,short tag=short.MinValue,float level=-1)
        {p.Shadow|=shadow;p.Fade|=fade;if(detail)p.Tier=TierFull;if(tier!=255)p.Tier=tier;if(drive!=255)p.Drive=drive;if(tag!=short.MinValue)p.Tag=tag;if(level>=0)p.Level=level;return p;}
        /// <summary>Adds a part given in the world (centre, full size along its own axes, world turn in degrees) to `parent`;
        /// inside a group a part that meets a keep-out spoils the whole group.</summary>
        private void Box(int parent,double x,double y,double z,double sx,double sy,double sz,Pen pen,double yaw=0,double pitch=0,double roll=0)
        {
            if(parent<0||!(sx>0&&sy>0&&sz>0))return;var b=blocks[parent];var q=Euler(yaw,pitch,roll);
            if(Blocked(x,y,z,sx*.5,sy*.5,sz*.5,q,pen.Role)){if(groupStart>=0)groupBad=true;else Rejected++;return;}
            var pq=BlockRot(b);var inv=new Rot{X=-pq.X,Y=-pq.Y,Z=-pq.Z,W=pq.W};
            Rotate(inv.X,inv.Y,inv.Z,inv.W,x-b.pos.x,y-b.pos.y,z-b.pos.z,out double ox,out double oy,out double oz);var rel=Then(inv,q);
            parts.Add(new NativeStationPart{Parent=parent,Look=pen.Look,Flags=pen.Flags,Color=pen.Color,X=(float)ox,Y=(float)oy,Z=(float)oz,Hx=(float)(sx*.5),Hy=(float)(sy*.5),Hz=(float)(sz*.5),
                Qx=(float)rel.X,Qy=(float)rel.Y,Qz=(float)rel.Z,Qw=(float)rel.W,Gain=pen.Gain,Level=pen.Level,Param=pen.Param,Drive=pen.Drive,Role=pen.Role,Tier=pen.Tier,Tag=pen.Tag,
                Shadow=pen.Shadow,Fade=pen.Fade,Room=RoomOf(x,y,z)});
        }
        /// <summary>An axis box from its corners.</summary>
        private void Span(int parent,double x0,double x1,double y0,double y1,double z0,double z1,Pen pen)=>Box(parent,(x0+x1)/2,(y0+y1)/2,(z0+z1)/2,Math.Abs(x1-x0),Math.Abs(y1-y0),Math.Abs(z1-z0),pen);
        private void Begin(){groupStart=parts.Count;glowStart=glows.Count;groupBad=false;}
        private bool End(){bool ok=!groupBad;if(!ok){Rejected++;parts.RemoveRange(groupStart,parts.Count-groupStart);glows.RemoveRange(glowStart,glows.Count-glowStart);}groupStart=glowStart=-1;groupBad=false;return ok;}
        private void Light(double x,double y,double z,int hex,double intensity,double range,double size,byte kind,double param=0,double floor=0,short tag=-1)
        {
            glows.Add(new NativeStationGlow{X=(float)x,Y=(float)y,Z=(float)z,R=(float)(NativeSceneryBuilder.Channel(hex,16)*intensity),G=(float)(NativeSceneryBuilder.Channel(hex,8)*intensity),B=(float)(NativeSceneryBuilder.Channel(hex,0)*intensity),
                Range=(float)range,Size=(float)size,Param=(float)param,Floor=(float)floor,Kind=kind,Room=RoomOf(x,y,z),Tag=tag});
        }
        private void Shaft(double x0,double y0,double z0,double x1,double y1,double z1,double w0,double w1,double alpha,byte kind)
        =>shafts.Add(new NativeStationShaft{X0=(float)x0,Y0=(float)y0,Z0=(float)z0,X1=(float)x1,Y1=(float)y1,Z1=(float)z1,W0=(float)w0,W1=(float)w1,Alpha=(float)alpha,Kind=kind,Room=RoomOf(x0,Math.Min(y0,9),z0)});
        private void Water(double x0,double x1,double z0,double z1,double y,byte kind)=>waters.Add(new NativeStationWater{X0=(float)x0,X1=(float)x1,Z0=(float)z0,Z1=(float)z1,Y=(float)y,Kind=kind});

        // ---------------- text ----------------
        /// <summary>Block letters (NativeExpeditionCityKit.GlyphBoxes' 3 x 5 font) on a face whose outward normal is turned `yaw`
        /// degrees from +z: centred on (x, y, z) (`align` -1 starts there, +1 ends there), glyph cells `cell` m, `depth` deep.</summary>
        private void Text(int parent,string text,double x,double y,double z,double yaw,double cell,double depth,Pen pen,int align=0)
        {
            double a=yaw*Math.PI/180,nx=Math.Sin(a),nz=Math.Cos(a),rx=-nz,rz=nx,width=TextWidth(text,cell),u0=align<0?0:align>0?-width:-width/2;
            for(int i=0;i<text.Length;i++)
                foreach(var g in Glyph(text[i]))
                {
                    double u=u0+(i*4+g[0]+g[2]*.5)*cell,v=(5-g[1]-g[3]*.5)*cell-2.5*cell;
                    Box(parent,x+rx*u,y+v,z+rz*u,g[2]*cell,g[3]*cell,depth,pen,yaw);
                }
        }
        public static double TextWidth(string text,double cell)=>text.Length*4*cell-cell;
        // The city font has no colon: two dots in the middle column.
        private static readonly System.Collections.Generic.List<int[]> Colon=new System.Collections.Generic.List<int[]>{new[]{1,1,1,1},new[]{1,3,1,1}};
        // The font's P has a rounded bowl that reads as F at board distance (DEFARTURES); the station squares it.
        private static readonly System.Collections.Generic.List<int[]> SquareP=new System.Collections.Generic.List<int[]>{new[]{0,0,3,1},new[]{0,1,1,4},new[]{2,1,1,1},new[]{1,2,2,1}};
        /// <summary>The station's glyph boxes: the town's 3 x 5 font with a colon and a squared P.</summary>
        public static System.Collections.Generic.List<int[]> Glyph(char c)=>c==':'?Colon:c=='P'||c=='p'?SquareP:NativeExpeditionCityKit.GlyphBoxes(c);

        // ---------------- keep-outs ----------------
        // What a part may never meet (world AABBs; a spawn's own dressing passes its own): spawn exits and their paths, vents,
        // drop columns, entries, the approach, interaction and boss points, the exit door's volumes.
        private void KeepOuts()
        {
            if(area==null)return;
            var spawns=area.spawns??Array.Empty<ExpeditionSpawnLayout>();
            for(short s=0;s<spawns.Length;s++)
            {
                var e=spawns[s]?.entry;if(e?.from==null||e.exit==null)continue;float clear=Math.Max(1.2f,e.clear),fy=e.exit.y;
                if(e.kind=="drop"){Keep(e.exit.x-clear,e.exit.x+clear,fy-.05f,fy+e.height+.5f,e.exit.z-clear,e.exit.z+clear,s,1);continue;}
                Keep(e.exit.x-clear,e.exit.x+clear,fy-.05f,fy+(e.kind=="breach"?4.4f:3.2f),e.exit.z-clear,e.exit.z+clear,s,0);
                if(e.kind=="vent")Keep(e.from.x-.9f,e.from.x+.9f,fy-.05f,fy+3.2f,e.from.z-.9f,e.from.z+.9f,s,0);
                float x0=Math.Min(e.from.x,e.exit.x),x1=Math.Max(e.from.x,e.exit.x),z0=Math.Min(e.from.z,e.exit.z),z1=Math.Max(e.from.z,e.exit.z);bool alongX=x1-x0>z1-z0;
                float w=e.kind=="breach"?2.0f:1.1f;Keep(alongX?x0:x0-w,alongX?x1:x1+w,fy-.05f,fy+(e.kind=="breach"?4.4f:3.0f),alongX?z0-w:z0,alongX?z1+w:z1,s,0);
            }
            void Point(ExpeditionPoint p,float r,float h){if(p!=null)Keep(p.x-r,p.x+r,p.y-.05f,p.y+h,p.z-r,p.z+r,-1,2);}
            foreach(var p in area.slots??Array.Empty<ExpeditionPoint>())Point(p,.9f,2.4f);
            foreach(var p in area.bossPoints??Array.Empty<ExpeditionPoint>())Point(p,1.6f,4f);
            foreach(var i in area.interactions??Array.Empty<ExpeditionInteractionLayout>())Keep(i.x-1.1f,i.x+1.1f,i.y-.05f,i.y+2.4f,i.z-1.1f,i.z+1.1f,-1,2);
            foreach(var a in area.anchors??Array.Empty<ExpeditionWorldAnchor>())if(a.kind!="door")Keep(a.x-1.1f,a.x+1.1f,a.y-.05f,a.y+2.4f,a.z-1.1f,a.z+1.1f,-1,2);
            // The live activation's machines (chip, med, recruit): the cabinet itself and where a pilot stands and the drop lands.
            // Floor paint may run under them (kind 3), as under a spawn's path.
            foreach(var m in area.machines??Array.Empty<PveMachine>())
            {
                if(m==null)continue;float bx=m.x-(float)Math.Sin(m.yaw)*.25f,bz=m.z-(float)Math.Cos(m.yaw)*.25f;Keep(bx-1.0f,bx+1.0f,m.y-.05f,m.y+2.6f,bz-1.0f,bz+1.0f,-1,3);
                foreach(var q in new[]{m.front,m.dispense,m.chipDrop})if(q!=null)Keep(q.x-.8f,q.x+.8f,q.y-.05f,q.y+2.4f,q.z-.8f,q.z+.8f,-1,3);
            }
            foreach(var d in area.doors??Array.Empty<ExpeditionDoorLayout>())
            {
                Volume(d.interact,.4f);Volume(d.closure,.6f);Point(d.approach,.9f,2.4f);
                foreach(var p in d.entries??Array.Empty<ExpeditionPoint>())Point(p,.9f,2.4f);foreach(var p in d.returns??Array.Empty<ExpeditionPoint>())Point(p,.9f,2.4f);
            }
        }
        private void Volume(ExpeditionVolume v,float pad){if(v==null)return;var b=NativeExpeditionCityKit.Aabb(v,pad);Keep(b[0],b[1],b[2]-.2f,b[3],b[4],b[5],-1,2);}
        private void Keep(float x0,float x1,float y0,float y1,float z0,float z1,short spawn,byte kind)=>keeps.Add(new NativeStationKeep{X0=x0,X1=x1,Y0=y0,Y1=y1,Z0=z0,Z1=z1,Spawn=spawn,Kind=kind});
        private bool Blocked(double x,double y,double z,double hx,double hy,double hz,Rot q,byte role)
        {
            if(keeps.Count==0)return false;Extent(x,y,z,hx,hy,hz,q.X,q.Y,q.Z,q.W,out double x0,out double x1,out double y0,out double y1,out double z0,out double z1);
            // Track parts no taller than a sleeper (the zombies walk the rails out of the tunnels), the flood's surface and flat
            // paint lie under their feet, so they may run across a spawn's exit and path; nothing else may.
            bool track=role==RoleTrack&&y1<NativeExpeditionStation.TrackBed+.36||role==RoleWater&&y1<NativeExpeditionStation.WaterY+.05||role==RoleFlat&&y1-y0<=Flat+1e-3;
            foreach(var k in keeps)
            {
                if(k.Spawn>=0&&k.Spawn==owner||track&&(k.Kind==0||k.Kind==3))continue;
                if(x1<=k.X0||x0>=k.X1||y1<=k.Y0||y0>=k.Y1||z1<=k.Z0||z0>=k.Z1)continue;
                return true;
            }
            return false;
        }

        // ---------------- drives (pure) ----------------
        private static double Frac(double x)=>x-Math.Floor(x);
        /// <summary>The look's stutter (two dips of `depth` in the first `length` s of every `period`), as NativeMapLooks.Flicker.</summary>
        public static float Stutter(double t,double period,double length,double depth){double u=t-Math.Floor(t/period)*period;return u<length&&(((int)(u/length*4))&1)==0?(float)(1-depth):1f;}
        /// <summary>A sodium lamp's or a stalled window's failing flicker by its seed (1 steady most of the time).</summary>
        public static float FlickerLevel(float seed,double t){double c=Frac((t+seed*3.7)/(4.5+seed%1.5));return c<.06?(c<.03?.15f:.8f):c>.5&&c<.53?.25f:1f;}
        public static float FireLevel(float seed,double t)=>(float)(.78+.12*Math.Sin(t*9.1+seed*7)+.07*Math.Sin(t*14.3+seed*3)+.05*Math.Sin(t*23.7+seed));
        /// <summary>A beacon's sweep (one rotation every .75 s): the lens bright as it faces the eye.</summary>
        public static float BeaconLevel(float seed,double t){double c=Math.Cos(Frac(t*1.33+seed)*Math.PI*2);return (float)(.6+1.4*Math.Pow(Math.Max(0,c),4));}
        /// <summary>
        /// A part's linear colour (gain folded in) and light level this frame, and whether it shows at all: board glyphs show
        /// only their row's current variant (the state's Row0, Row4, Title; the ticker's page), blink and stutter with the board;
        /// the signals show their aspect (red idle, green through the warning and pass, dark under the levers); the danger lamps
        /// and beacons flash through the warning and pass; the insulators glint blue while their section is live; the flood
        /// glows blue while it conducts; the cabinets' doors glow through the fizz and blaze in the arc; the levers show their
        /// home or thrown pose; the express's parts show only while it is in the station (its window band strobing); the west
        /// mouth's glow grows with the headlight through the warning. Pure; allocation free.
        /// </summary>
        public static bool Drive(in NativeStationPart p,NativeStationState s,double t,out float r,out float g,out float b,out float level)
        {
            float k=1;level=p.Level;bool show=true;
            switch(p.Drive)
            {
                case DriveFlicker:k=FlickerLevel(p.Param,t);break;
                case DriveFluor:k=Stutter(t+p.Param*5.3,9+p.Param*7,.35,.85);level=p.Level*k;break;
                case DriveFire:k=FireLevel(p.Param,t);break;
                case DriveWindow:k=FlickerLevel(p.Param,t*.7);level=p.Level*k;break;
                case DriveStrip:k=Stutter(t+p.Param*3.1,5,.15,.6);break;
                case DriveBox:k=(float)(.85+.15*Math.Sin(t*1.7+p.Param));break;
                case DriveDead:k=1;break;
                case DriveBoard:
                {
                    k=Stutter(t,7,.18,.5);int tag=p.Tag;if(tag<0)break;int row=tag/16,variant=tag%16;
                    if(row==0){show=s==null?variant==NativeStationState.Row0Delayed:variant==s.Row0;if(show&&s!=null&&!s.BoardRowLit(0))k*=.08f;}
                    else if(row==4){show=s==null?variant==NativeStationState.Row4NoStop:variant==s.Row4;if(show&&s!=null&&!s.BoardRowLit(4))k*=.08f;}
                    else if(row==5)show=s==null?variant==NativeStationState.TitleDepartures:variant==s.Title;
                    break;
                }
                case DriveTicker:k=Stutter(t,7,.18,.5);show=p.Tag==NativeExpeditionStation.TickerAt(t);break;
                case DriveSignal:
                {
                    byte a=s?.Signal??NativeStationState.SignalRed;show=true;
                    // Tag: the lamp's aspect (0 red, 1 green); a lit lamp blazes, an unlit one is dark glass.
                    bool lit=a!=NativeStationState.SignalDark&&p.Tag==a;k=lit?1:.04f;break;
                }
                case DriveDanger:{float d=s?.Danger??0;k=d<=0?.03f:(Frac(t*2.2+p.Param)<.5?1.2f:.12f);break;}
                case DriveBeacon:{float d=s?.Danger??0;k=d<=0?.03f:BeaconLevel(p.Param,t);break;}
                case DriveRail:
                {
                    int live=s?.Rails??NativeExpeditionStation.AllRails;bool on=p.Tag>=0&&(live>>p.Tag&1)!=0;float cut=s?.RailCut??1;
                    // A live insulator glints now and then (each its own beat); a dead one fades out over the cut's second.
                    double c=Frac(t*.37+p.Param*7.1);float glint=c<.08?(float)(1-c/.08):0;k=on?(.35f+1.4f*glint):Math.Max(0,1-cut)*.5f;break;
                }
                case DriveFlood:
                {
                    float glow=s?.FloodGlow??0;
                    // The water's own dark colour, lifting toward electric blue while it conducts.
                    r=(float)NativeSceneryBuilder.Channel(p.Color,16)*p.Gain;g=(float)NativeSceneryBuilder.Channel(p.Color,8)*p.Gain;b=(float)NativeSceneryBuilder.Channel(p.Color,0)*p.Gain;
                    if(glow>0){r+=.02f*glow;g+=.15f*glow;b+=.5f*glow;}
                    return true;
                }
                case DriveCabinet:
                {
                    NativeHazardPhase ph=NativeHazardPhase.Idle;if(s!=null&&p.Tag>=0&&p.Tag<s.CabinetPhase.Length)ph=s.CabinetPhase[p.Tag];
                    k=ph==NativeHazardPhase.Warn?(Frac(t*13+p.Param)<.5?.9f:.3f):ph==NativeHazardPhase.Active?2.6f:.06f;break;
                }
                case DriveLever:{bool thrown=s!=null&&s.Levers;show=p.Tag==1?thrown:!thrown;break;}
                case DriveDiagram:
                {
                    // The track diagram: the express line lit white through the warning and pass, the live sections red, all dark
                    // under the levers but the box's own panel.
                    if(p.Tag==100){k=s!=null&&(s.Express==NativeStationState.ExpressWarn||s.Express==NativeStationState.ExpressPass)?(Frac(t*2)<.5?1.4f:.5f):.15f;break;}
                    int live=s?.Rails??NativeExpeditionStation.AllRails;k=p.Tag>=0&&(live>>p.Tag&1)!=0?1:.08f;break;
                }
                case DriveExpress:
                {
                    byte e=s?.Express??NativeStationState.ExpressIdle;show=e==NativeStationState.ExpressPass||e==NativeStationState.ExpressAfter&&s.Nose-NativeExpeditionStation.TrainLength<NativeExpeditionStation.ShellX+1;
                    if(p.Tag==1)k=Frac(t*11+p.Param)<.55?1:.25f;
                    break;
                }
                case DriveMouth:{float h=s?.Headlight??0;show=h>.01f&&s.Express==NativeStationState.ExpressWarn;k=h*1.4f;break;}
            }
            float gain=p.Gain*k;r=(float)NativeSceneryBuilder.Channel(p.Color,16)*gain;g=(float)NativeSceneryBuilder.Channel(p.Color,8)*gain;b=(float)NativeSceneryBuilder.Channel(p.Color,0)*gain;
            return show;
        }
        /// <summary>The express part's world centre for a nose at x `nose` on the pit at z `line` (the train frame: +x forward).</summary>
        public static void ExpressPlace(in NativeStationPart p,float nose,float line,out float x,out float y,out float z){x=nose+p.X;y=NativeExpeditionStation.TrackBed+p.Y;z=line+p.Z;}
    }
}
