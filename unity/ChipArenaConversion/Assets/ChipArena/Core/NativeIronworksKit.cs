using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// One Ironworks dressing part (presentation only, never sim data): a box drawn through NativeBlockLit like an arena block.
    /// Centre (world m, or in its moving frame's rest space when `Frame` is not 0), half extents and its own turn; `Look` and
    /// `Flags` as NativeBlockLooks.Resolve gives them (a bank slot carries NativeCyberCityLooks.Bank), `Color` sRGB, `Gain` the
    /// emission scale, `Level` the light level the shader reads, `Drive` what animates its colour (<see cref="NativeIronworksKit.Drive"/>,
    /// phase in `Param`, its hazard in `Hazard`), `Frame` what moves it (<see cref="NativeIronworksKit.FramePose"/>), `Parent`
    /// the block it belongs to (it hides with a breached block), `Role`, `Room` and `Owner` (the spawn whose own dressing it is, -1
    /// none) for the checks and the budget lines. `Fade`:
    /// hidden while between the camera and the pilot. `Detail`: fine dressing the low tier leaves out.
    /// </summary>
    public struct NativeIronPart
    {
        public int Parent,Look,Flags,Color;public float X,Y,Z,Hx,Hy,Hz,Qx,Qy,Qz,Qw,Gain,Level,Param;public byte Drive,Role,Room,Frame;public short Hazard,Owner;public bool Fade,Detail;
    }
    /// <summary>
    /// One molten surface drawn through Resources/NativeMolten.shader: a box (centre, half extents, turn, as a part) whose top and
    /// sides show flowing, crusting metal. `Kind` is the shader's style (<see cref="NativeIronworksKit"/> Molten* constants), the
    /// flow runs along its own `Axis` (0 x, 1 y, 2 z) in direction `Sign` at `Speed` m/s, hot `HeatA` where it starts and
    /// `HeatB` where it ends (0 crust .. 1 white heat), `Crust` its share of floating crust; `Drive` and `Hazard` say what changes
    /// them each frame (<see cref="NativeIronworksKit.MoltenState"/>); `Owner` the spawn whose own dressing it is (-1 none).
    /// </summary>
    public struct NativeMoltenPiece
    {
        public int Parent;public float X,Y,Z,Hx,Hy,Hz,Qx,Qy,Qz,Qw,Speed,HeatA,HeatB,Crust,Gain,Seed,Param;public byte Kind,Axis,Drive,Room,Frame;public sbyte Sign;public short Hazard,Owner;public bool Detail;
    }
    /// <summary>A light or effect source of the works: place (world m, or in its moving frame's rest space when `Frame` is not 0),
    /// linear colour times intensity, reach (m), size, phase, its kind (<see cref="NativeIronworksKit"/> Glow* constants), room,
    /// light field channel (-1 none) and hazard (-1 none).</summary>
    public struct NativeIronGlow {public float X,Y,Z,R,G,B,Range,Size,Param;public byte Kind,Room,Frame;public sbyte Channel;public short Hazard;}
    /// <summary>A world AABB the kit keeps clear; `Spawn` the spawn whose own dressing may enter it (-1 none); `Kind` 0 a spawn's
    /// exit and path, 1 a drop column, 2 a record (slot, cache, boss point, door or gate volume, machine spot).</summary>
    public struct NativeIronKeep {public float X0,X1,Y0,Y1,Z0,Z1;public short Spawn;public byte Kind;}

    /// <summary>
    /// The City Ironworks' dressing kit (Claude, 2026-10-16; docs/EXPEDITION-CITY-IRONWORKS.md sections 3, 4 and 9): a sealed
    /// steelworks still burning, laid on the live map `expedition-foundry` (the activation of the design blockout,
    /// docs/arenas/scripts/expedition_ironworks.py: the same blocks under the live prefix) by block id stem, with the instance's
    /// records (the exp-foundry area of Resources/ExpeditionLayout.json: rooms, spawns, doors, machines) kept clear. Built once per
    /// map content; nothing here collides, moves a block or enters the network frame. Inert on every other map (<see cref="Active"/>).
    /// <list type="bullet">
    /// <item>Structure: Pratt roof trusses every 6.5 m with the roof monitors' cold glass, the crane runways' hangers and rails, the
    /// ladle crane's bridge, trolley, hook and cables, the converter hall's runways, the upper ring's toe boards and posts, wall
    /// pilasters, steam mains and cable trays, sodium high-bays and mercury lamps, the Rigger's eight hang points as chain hoists on
    /// their girders.</item>
    /// <item>Rooms: the casting floor (torpedo cars and track, the ladle on the crane and its pour, the molds, the pig bed, spare
    /// ladles on their stands), the furnace aisle (buckstays, charging doors in water-cooled frames, hoods and flues, tapholes and
    /// the three runners), the rolling mill (rollers, the stands' screwdowns and rolls, the reheat door, the shear, the cooling
    /// bed, the pulpit), the coke oven battery (doors with glowing seams, one jammed open, the gas main, the pusher car, the quench
    /// tower), the converter hall (two banded vessels on trunnions, the pouring pit, the shutter's frame), the slag yard and drop
    /// forge, the time office (the shift board stopped at 6:47), the stockhouse and the pattern shop.</item>
    /// <item>Molten surfaces (<see cref="Molten"/>) for NativeMolten.shader, light sources (<see cref="Glows"/>) for the picker
    /// and the light field, and moving frames (the ladle, the drop hammer's ram, the hot slab, the shear blade, the pusher ram,
    /// the slag pot) driven by the hazard views (NativeIronworksHazards).</item>
    /// </list>
    /// Placement rules (Tests/IronworksVisualChecks.cs): within 3.1 m over a floor one can stand on (the ground, the ring) nothing
    /// stands out more than <see cref="WallBand"/> from a block, except flat paint (<see cref="Flat"/> tall at most), thin hanging
    /// chains and a hazard's own machinery inside its hazard volume; nothing lies on a spawn's exit, path or drop column, a slot,
    /// a cache, a boss point, a machine spot, the doors' or the shutter's volumes. Pure: the same blocks and area give the same
    /// parts in the same order; <see cref="Drive"/>, <see cref="MoltenState"/> and <see cref="FramePose"/> are pure functions.
    /// </summary>
    public sealed partial class NativeIronworksKit
    {
        /// <summary>The live map (Astra's activation keeps `expedition-foundry`, the exp-foundry area's map in the live manifest) and
        /// the design draft's later name (`expedition-ironworks`): both are the Ironworks, with their blocks under their own prefix,
        /// so a later rename keeps working. <see cref="MapId"/> is the live one.</summary>
        public const string LiveMap="expedition-foundry",DesignMap="expedition-ironworks",MapId=LiveMap,Prefix=LiveMap+"-",DesignPrefix=DesignMap+"-",AreaId="exp-foundry";
        /// <summary>Whether a map id is the Ironworks (either name): every Ironworks hook is inert on any other map.</summary>
        public static bool Active(string mapId)=>mapId==LiveMap||mapId==DesignMap;
        public const byte DriveSteady=0,DriveFlicker=1,DriveFire=2,DriveBeacon=3,DriveHazardGlow=4,DriveSodium=5,DriveMercury=6,DriveLed=7,DriveSeam=8,DriveTap=9,DriveWarnLamp=10;
        public const byte RoleStructure=0,RoleWall=1,RoleFloor=2,RoleProp=3,RoleHazard=4,RoleSpawn=5,RoleLamp=6,RoleSign=7,RoleChain=8,RoleMolten=9,RoleHigh=10,RoleBreach=11;
        public const byte FrameStatic=0,FrameLadle=1,FrameRam=2,FrameSlab=3,FrameShear=4,FramePusher=5,FrameSlagPot=6,FrameHook=7,Frames=8;
        /// <summary>NativeMolten.shader styles: a flowing surface (runner, mold, pool), a falling stream, fire (a furnace mouth),
        /// a solid hot piece (slab, billet, pig), slag (glassy black with cracks) and glowing coke.</summary>
        public const byte MoltenFlow=0,MoltenStream=1,MoltenFire=2,MoltenSlab=3,MoltenSlag=4,MoltenCoke=5;
        public const byte MDriveSteady=0,MDriveRunner=1,MDriveStream=2,MDriveLadle=3,MDriveMold=4,MDriveSlagTongue=5,MDriveSlab=6,MDriveFurnace=7,MDriveBillet=8,MDriveSlagPot=9,MDriveConverter=10;
        public const byte GlowFurnace=0,GlowTaphole=1,GlowRunner=2,GlowPour=3,GlowMold=4,GlowTorpedo=5,GlowPig=6,GlowReheat=7,GlowSlab=8,GlowConverter=9,GlowPit=10,GlowSlag=11,GlowHearth=12,GlowBillet=13,GlowCokeDoor=14,GlowCokeSeam=15,
            GlowSodium=16,GlowMercury=17,GlowOffice=18,GlowLamp=19,GlowLed=20,GlowBeacon=21,GlowWindow=22,GlowFill=23;
        /// <summary>How far within 3.1 m of a floor a part may stand out from a block (m), the most flat paint may rise (m), the
        /// band's height over a floor, the widest a hanging chain may be (half extent, m).</summary>
        public const float WallBand=.25f,Flat=.06f,Low=3.1f,ChainHalf=.06f;
        /// <summary>The floors one can stand on: the ground and the upper ring (the deck tops).</summary>
        public const float Ground=0,RingFloor=6;

        public NativeIronPart[] Parts=Array.Empty<NativeIronPart>();public NativeMoltenPiece[] Molten=Array.Empty<NativeMoltenPiece>();public NativeIronGlow[] Glows=Array.Empty<NativeIronGlow>();
        public NativeIronKeep[] Keeps=Array.Empty<NativeIronKeep>();
        /// <summary>Parts and groups rejected by a keep-out (the checks expect the kit to fit, so these stay few), and where (each
        /// rejected part's world AABB, the keep-out it met in `Spawn` and `Kind`).</summary>
        public int Rejected;public NativeIronKeep[] RejectedAt=Array.Empty<NativeIronKeep>();private readonly List<NativeIronKeep> rejectedAt=new List<NativeIronKeep>();
        /// <summary>Parts left out because, within 3.1 m of a floor, they would stand out from every block (the placement rule:
        /// a wall's pilaster where the wall has an opening, say), and where.</summary>
        public int Unbacked;public NativeIronKeep[] UnbackedAt=Array.Empty<NativeIronKeep>();private readonly List<NativeIronKeep> unbackedAt=new List<NativeIronKeep>();

        private readonly List<NativeIronPart> parts=new List<NativeIronPart>();private readonly List<NativeMoltenPiece> molten=new List<NativeMoltenPiece>();private readonly List<NativeIronGlow> glows=new List<NativeIronGlow>();private readonly List<NativeIronKeep> keeps=new List<NativeIronKeep>();
        private ContentBlock[] blocks=Array.Empty<ContentBlock>();private ExpeditionAreaLayout area;private readonly Dictionary<string,int> byStem=new Dictionary<string,int>();
        private int groupStart=-1,moltenStart=-1,glowStart=-1;private bool groupBad;private short owner=-1;private int parent;

        // ---------------- palette (sRGB) ----------------
        /// <summary>Black iron and soot, machine green, worn safety yellow, refractory, rust, the hot colours (furnace orange, sodium,
        /// mercury's cold white), the hazard kit's amber (hazards only) and the telegraphs' cream.</summary>
        public const int IronHex=0x24211E,IronDarkHex=0x161412,SootHex=0x110F0D,SteelHex=0x48474A,SteelLightHex=0x6E6C68,GreenHex=0x2F4038,GreenDarkHex=0x22302A,YellowHex=0xC9A23A,YellowDarkHex=0x8A6E26,
            RefractoryHex=0x8A6A4C,RefractoryDarkHex=0x4E3C2C,RustHex=0x6A3A1E,RustDarkHex=0x3E2414,ConcreteHex=0x4A4642,ConcreteDarkHex=0x2E2B28,TimberHex=0x8A6A44,TimberLightHex=0xB08A58,SandHex=0x9C8660,OreHex=0x5A3424,CokeHex=0x1E1C1C,
            FurnaceHex=0xFF6A1C,SeamHex=0xFF5A12,SodiumHex=0xFFB347,MercuryHex=0xBFD8FF,OfficeHex=0xFFC266,LedHex=0xFF2A1A,AmberHex=0xFFB000,CreamHex=0xFFD27A,PaperHex=0xC8C0AC,GlassColdHex=0x2A3A58,WindowWarmHex=0xFFC266,
            RedPaintHex=0x8A2A22,WhitePaintHex=0xD8D4C8,CableHex=0x151515,ChainHex=0x3A3632,HookHex=0x2C2926;

        // ---------------- hashing (stable across machines) ----------------
        private static uint Mix(uint x){unchecked{x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x;}}
        /// <summary>A stable hash in [0, 1) of two integers and a salt.</summary>
        public static double Hash(int a,int b=0,int c=0){unchecked{return Mix(Mix(Mix((uint)a+0x9E3779B9U)^(uint)b*0x85EBCA6BU)+(uint)c*0xC2B2AE35U)/4294967296.0;}}
        private static bool Starts(string s,string p)=>s.StartsWith(p,StringComparison.Ordinal);
        /// <summary>The id with the map prefix removed, or null for other maps' ids.</summary>
        public static string Stem(string id)=>id==null?null:id.StartsWith(Prefix,StringComparison.Ordinal)?id.Substring(Prefix.Length):id.StartsWith(DesignPrefix,StringComparison.Ordinal)?id.Substring(DesignPrefix.Length):null;

        // ---------------- rooms (the draft record's room volumes, x and z extents) ----------------
        public const byte RoomTimeOffice=0,RoomStockhouse=1,RoomPatternShop=2,RoomCasting=3,RoomFurnace=4,RoomMill=5,RoomCoke=6,RoomConverter=7,RoomSlag=8,Rooms=9;
        public static readonly string[] RoomNames={"time-office","stockhouse","pattern-shop","casting-floor","furnace-aisle","rolling-mill","coke-ovens","converter-hall","slag-yard"};
        /// <summary>x0, x1, z0, z1 per room (the record's volumes).</summary>
        public static readonly float[] RoomBox={36.3f,50,-9.7f,9.7f, 36.3f,50,10.3f,34, 36.3f,50,-34,-10.3f, -23.7f,35.7f,-13.7f,22, -23.7f,35.7f,22,30, -23.7f,35.7f,-34,-14.3f, -50,-24.3f,12.3f,34, -50,-24.3f,-11.7f,11.7f, -50,-24.3f,-34,-12.3f};
        /// <summary>The room a world point lies in (nearest room box when it lies in none, e.g. inside a wall).</summary>
        public static byte RoomOf(double x,double z)
        {
            byte best=RoomCasting;double bd=double.MaxValue;
            for(int r=0;r<Rooms;r++){double x0=RoomBox[r*4],x1=RoomBox[r*4+1],z0=RoomBox[r*4+2],z1=RoomBox[r*4+3];double dx=x<x0?x0-x:x>x1?x-x1:0,dz=z<z0?z0-z:z>z1?z-z1:0,d=dx*dx+dz*dz;if(d<bd){bd=d;best=(byte)r;}if(d==0)return (byte)r;}
            return best;
        }

        // ---------------- build ----------------
        /// <summary>Builds the kit for the map's blocks and the instance's layout area (either may be null: no keep-outs without the
        /// area). Nothing is built for another map's blocks (no block carries the Ironworks prefix).</summary>
        public static NativeIronworksKit Build(ContentBlock[] blocks,ExpeditionAreaLayout area)
        {
            var k=new NativeIronworksKit{blocks=blocks??Array.Empty<ContentBlock>(),area=area};
            for(int i=0;i<k.blocks.Length;i++){var s=Stem(k.blocks[i]?.id);if(s!=null&&!k.byStem.ContainsKey(s))k.byStem[s]=i;}
            if(k.byStem.Count==0||!k.byStem.ContainsKey("slab"))return k;
            k.KeepOuts();k.parent=k.byStem["slab"];
            k.Structure();k.Furnaces();k.Casting();k.Mill();k.Coke();k.Converter();k.Slag();k.East();k.Hangs();
            k.Parts=k.parts.ToArray();k.Molten=k.molten.ToArray();k.Glows=k.glows.ToArray();k.Keeps=k.keeps.ToArray();k.RejectedAt=k.rejectedAt.ToArray();k.UnbackedAt=k.unbackedAt.ToArray();k.parts.Clear();k.molten.Clear();k.glows.Clear();k.rejectedAt.Clear();k.unbackedAt.Clear();return k;
        }
        /// <summary>The block index of a stem, or -1.</summary>
        public int Block(string stem)=>stem!=null&&byStem.TryGetValue(stem,out int i)?i:-1;
        private ContentBlock B(string stem){int i=Block(stem);return i>=0?blocks[i]:null;}
        private static double X0(ContentBlock b)=>b.pos.x-b.size.x/2;private static double X1(ContentBlock b)=>b.pos.x+b.size.x/2;
        private static double Y0(ContentBlock b)=>b.pos.y-b.size.y/2;private static double Y1(ContentBlock b)=>b.pos.y+b.size.y/2;
        private static double Z0(ContentBlock b)=>b.pos.z-b.size.z/2;private static double Z1(ContentBlock b)=>b.pos.z+b.size.z/2;
        /// <summary>Sets the block the next parts belong to (by stem; the floor slab when the stem is missing).</summary>
        private void Under(string stem){int i=Block(stem);parent=i>=0?i:Block("slab");}

        // ---------------- quaternions ----------------
        private struct Rot {public double X,Y,Z,W;public static readonly Rot None=new Rot{W=1};}
        private static void Mul(double ax,double ay,double az,double aw,double bx,double by,double bz,double bw,out double x,out double y,out double z,out double w)
        {x=aw*bx+ax*bw+ay*bz-az*by;y=aw*by-ax*bz+ay*bw+az*bx;z=aw*bz+ax*by-ay*bx+az*bw;w=aw*bw-ax*bx-ay*by-az*bz;}
        /// <summary>v turned by the unit quaternion (x, y, z, w).</summary>
        public static void Rotate(double qx,double qy,double qz,double qw,double vx,double vy,double vz,out double ox,out double oy,out double oz)
        {double tx=2*(qy*vz-qz*vy),ty=2*(qz*vx-qx*vz),tz=2*(qx*vy-qy*vx);ox=vx+qw*tx+(qy*tz-qz*ty);oy=vy+qw*ty+(qz*tx-qx*tz);oz=vz+qw*tz+(qx*ty-qy*tx);}
        private static Rot AxisRot(double ax,double ay,double az,double deg){double h=deg*Math.PI/360,s=Math.Sin(h);return new Rot{X=ax*s,Y=ay*s,Z=az*s,W=Math.Cos(h)};}
        private static Rot Then(Rot a,Rot b){Mul(a.X,a.Y,a.Z,a.W,b.X,b.Y,b.Z,b.W,out double x,out double y,out double z,out double w);return new Rot{X=x,Y=y,Z=z,W=w};}
        private static Rot Yaw(double deg)=>AxisRot(0,1,0,deg);
        /// <summary>The turn taking +z to the direction (dx, dy, dz) with its x axis kept horizontal (a member, a chain, a cable).</summary>
        private static Rot Along(double dx,double dy,double dz)
        {
            double l=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(l<1e-9)return Rot.None;dx/=l;dy/=l;dz/=l;
            double yaw=Math.Atan2(dx,dz)*180/Math.PI,pitch=-Math.Asin(Math.Max(-1,Math.Min(1,dy)))*180/Math.PI;
            if(Math.Abs(dy)>.9999)return AxisRot(1,0,0,dy>0?-90:90);
            return Then(Yaw(yaw),AxisRot(1,0,0,pitch));
        }
        /// <summary>A turned box's world AABB.</summary>
        public static void Extent(double x,double y,double z,double hx,double hy,double hz,double qx,double qy,double qz,double qw,out double x0,out double x1,out double y0,out double y1,out double z0,out double z1)
        {
            x0=y0=z0=double.MaxValue;x1=y1=z1=double.MinValue;
            for(int k=0;k<8;k++){Rotate(qx,qy,qz,qw,(k&1)==0?-hx:hx,(k&2)==0?-hy:hy,(k&4)==0?-hz:hz,out double ox,out double oy,out double oz);x0=Math.Min(x0,x+ox);x1=Math.Max(x1,x+ox);y0=Math.Min(y0,y+oy);y1=Math.Max(y1,y+oy);z0=Math.Min(z0,z+oz);z1=Math.Max(z1,z+oz);}
        }

        // ---------------- frames (moving dressing) ----------------
        /// <summary>Each moving frame's rest origin (world m): local coordinates are world minus this at rest.</summary>
        public static readonly float[] FrameOrigin={0,0,0, 5.5f,8.6f,-7.35f, -29.5f,8.2f,-28.4f, 25.6f,1.14f,-25f, -15.5f,4.0f,-25f, -37.96f,2.2f,27.4f, -47.2f,2.0f,-24.6f, 5.5f,10.55f,-7.35f};
        /// <summary>The ladle's tilt while pouring (deg, about the z axis, lip toward +x), its hoist when parked (m), the drop hammer's
        /// lift (m over the anvil), the hot slab's run (m west along the table), the shear blade's stroke (m), the pusher ram's
        /// reach (m north), the slag pot's tip (deg).</summary>
        public const float LadleTilt=58,LadleHoist=1.8f,RamLift=5.0f,SlabRun=44.6f,ShearStroke=1.3f,PusherReach=.2f,PotTip=62f;
        /// <summary>A frame's world pose this frame: world = (Px, Py, Pz) + Q * local. Frame 0 is the identity.</summary>
        /// `music`: the zone boss song (NativeIronworksMusic): the parked ladle and its hook rock with the bar; nothing else moves to it.
        public static void FramePose(int frame,IronworksHazardView[] views,double time,out float px,out float py,out float pz,out float qx,out float qy,out float qz,out float qw,in NativeZoneBossBeat music=default)
        {
            px=py=pz=0;qx=qy=qz=0;qw=1;if(frame<=0||frame>=Frames)return;px=FrameOrigin[frame*3];py=FrameOrigin[frame*3+1];pz=FrameOrigin[frame*3+2];
            IronworksHazardView V(int h)=>views!=null&&h<views.Length?views[h]:default;
            switch(frame)
            {
                case FrameLadle:
                {
                    var v=V(NativeIronworksHazards.Ladle);double tilt=LadleTiltOf(v);double sway=.6*Math.Sin(time*.9)+(v.Phase==NativeHazardPhase.Warn?1.2*Math.Sin(time*7)*v.Level:0)+MusicSway(music,v.Parked);
                    if(v.Parked)py+=LadleHoist;var q=Then(AxisRot(0,0,1,-tilt),AxisRot(1,0,0,sway*.5));qx=(float)q.X;qy=(float)q.Y;qz=(float)q.Z;qw=(float)q.W;break;
                }
                case FrameRam:py+=(float)(RamLiftOf(V(NativeIronworksHazards.Hammer))-RamLift);break;
                case FrameSlab:px-=(float)SlabRunOf(V(NativeIronworksHazards.Slab));break;
                case FrameShear:py-=(float)(ShearOf(V(NativeIronworksHazards.Shear))*ShearStroke);break;
                case FramePusher:pz+=(float)(PusherOf(V(NativeIronworksHazards.Pusher))*PusherReach);break;
                case FrameSlagPot:{double tip=PotTipOf(V(NativeIronworksHazards.Slag));var q=AxisRot(0,0,1,-tip);qx=(float)q.X;qy=(float)q.Y;qz=(float)q.Z;qw=(float)q.W;break;}
                case FrameHook:{var v=V(NativeIronworksHazards.Ladle);if(v.Parked)py+=LadleHoist;double rock=MusicSway(music,v.Parked);if(rock!=0){var q=AxisRot(1,0,0,rock*.25);qx=(float)q.X;qy=(float)q.Y;qz=(float)q.Z;qw=(float)q.W;}break;}
            }
        }
        /// <summary>The ladle's tilt (deg): tipping in the warning, full while pouring, back over the cool-down; 0 at rest or parked.</summary>
        public static double LadleTiltOf(in IronworksHazardView v)
        {
            if(v.Parked)return 0;
            switch(v.Phase){case NativeHazardPhase.Warn:return LadleTilt*.45*Smooth(v.Level);case NativeHazardPhase.Active:return LadleTilt*(.45+.55*Smooth(Math.Min(1,v.Since/.5)));case NativeHazardPhase.Clear:return LadleTilt*Smooth(v.Level);default:return 0;}
        }
        /// <summary>The drop hammer's ram over the anvil (m): it lifts through the warning, falls in the live window, rests on the billet
        /// a moment and climbs back slowly.</summary>
        public static double RamLiftOf(in IronworksHazardView v)
        {
            switch(v.Phase)
            {
                case NativeHazardPhase.Warn:return RamLift*(.55+.45*Smooth(v.Level));
                case NativeHazardPhase.Active:{double u=v.Since/.12;return u>=1?0:RamLift*(1-u*u);}
                case NativeHazardPhase.Clear:return 0;
                case NativeHazardPhase.Idle:return v.Rate==NativeIronworksHazards.RateOff?RamLift:RamLift*.55*Smooth(Math.Min(1,v.Since/2.5));
                default:return RamLift;
            }
        }
        /// <summary>The hot slab's run west along the table (m; at 0 it has just left the reheat door): it slides out of the door
        /// through the warning, runs the table onto the cooling bed while live, and is hidden otherwise.</summary>
        public static double SlabRunOf(in IronworksHazardView v)
        {
            if(v.Phase==NativeHazardPhase.Warn)return -3.0*(1-Smooth(v.Level));
            if(v.Phase==NativeHazardPhase.Active)return SlabRun*Math.Min(1,v.Since/NativeIronworksHazards.Defs[NativeIronworksHazards.Slab].Live);
            if(v.Phase==NativeHazardPhase.Clear)return SlabRun+2*(1-v.Level);
            return -6;
        }
        public static bool SlabShown(in IronworksHazardView v)=>v.Phase==NativeHazardPhase.Warn||v.Phase==NativeHazardPhase.Active||v.Phase==NativeHazardPhase.Clear;
        /// <summary>The shear blade's drop (0 up .. 1 through the slab line).</summary>
        public static double ShearOf(in IronworksHazardView v)=>v.Phase==NativeHazardPhase.Active?Math.Sin(Math.Min(1,v.Since/.6)*Math.PI):v.Phase==NativeHazardPhase.Warn?.08*v.Level:0;
        /// <summary>The pusher ram's reach (0 home .. 1 fully out).</summary>
        public static double PusherOf(in IronworksHazardView v)=>v.Phase==NativeHazardPhase.Active?Smooth(Math.Min(1,v.Since/2.2)):v.Phase==NativeHazardPhase.Clear?Smooth(v.Level):0;
        /// <summary>The slag pot's tip on the bank (deg): it tips through the warning, pours over the live window's first seconds.</summary>
        public static double PotTipOf(in IronworksHazardView v)=>v.Phase==NativeHazardPhase.Warn?PotTip*.55*Smooth(v.Level):v.Phase==NativeHazardPhase.Active?PotTip*(.55+.45*Smooth(Math.Min(1,v.Since/1.5)))*(v.Since>6?Smooth(1-Math.Min(1,(v.Since-6)/3)):1):0;
        public static double Smooth(double u){u=u<0?0:u>1?1:u;return u*u*(3-2*u);}
        private static double Frac(double v)=>v-Math.Floor(v);

        // ---------------- drives ----------------
        /// <summary>A part's linear colour (times its gain) and light level this frame (pure).</summary>
        /// `music`: the zone boss song (NativeIronworksMusic.MusicGain): the lamps and furnaces follow it, hazard drives never.
        public static void Drive(in NativeIronPart p,IronworksHazardView[] views,double time,out float r,out float g,out float b,out float level,in NativeZoneBossBeat music=default)
        {
            r=(float)NativeSceneryBuilder.Channel(p.Color,16);g=(float)NativeSceneryBuilder.Channel(p.Color,8);b=(float)NativeSceneryBuilder.Channel(p.Color,0);float k=p.Gain;level=p.Level;
            var v=p.Hazard>=0&&views!=null&&p.Hazard<views.Length?views[p.Hazard]:default;
            switch(p.Drive)
            {
                case DriveFlicker:k*=Level(DriveFlicker,p.Param,time);break;
                case DriveFire:k*=Level(DriveFire,p.Param,time);break;
                case DriveSodium:k*=Level(DriveSodium,p.Param,time);break;
                case DriveMercury:k*=.92f+.08f*(float)Math.Sin(time*1.7+p.Param*9);break;
                case DriveSeam:k*=.75f+.25f*Level(DriveFire,p.Param,time);break;
                case DriveLed:k*=Frac(time*.5+p.Param)<.94?1:.15f;break;
                case DriveBeacon:
                {
                    // The hazard kit's amber beacon: dark at rest, a 1.1 turn/s sweep while the hazard warns or runs.
                    bool on=v.Phase==NativeHazardPhase.Warn||v.Phase==NativeHazardPhase.Active;k*=on?.4f+.6f*(float)Math.Pow(Math.Max(0,Math.Cos((time*1.1+p.Param)*Math.PI*2)),4):.04f;break;
                }
                case DriveWarnLamp:{bool on=v.Phase==NativeHazardPhase.Warn||v.Phase==NativeHazardPhase.Active;k*=on?(Frac(time*2.6+p.Param)<.5?1:.2f):.05f;break;}
                case DriveHazardGlow:k*=HazardGlow(v,p.Param);break;
                case DriveTap:k*=TapGlow(v,time,p.Param);break;
            }
            if(music.On)k*=MusicGain(p,music);
            r*=k;g*=k;b*=k;
        }
        /// <summary>A hazard-lit part's level: dim at rest, building through the warning, full while live, fading in the cool-down.</summary>
        public static float HazardGlow(in IronworksHazardView v,float rest)=>v.Phase==NativeHazardPhase.Warn?rest+(1-rest)*v.Level:v.Phase==NativeHazardPhase.Active?1:v.Phase==NativeHazardPhase.Clear?rest+(1-rest)*v.Level:rest;
        /// <summary>A taphole's glow: a dull plug at rest (plugged in Wave 1: dimmer still), the roar of the warning, the open hole live.</summary>
        public static float TapGlow(in IronworksHazardView v,double time,float param)
        {
            float rest=v.Rate==NativeIronworksHazards.RateOff?.18f:.32f;
            if(v.Phase==NativeHazardPhase.Warn)return rest+(1-rest)*v.Level*(.8f+.2f*Level(DriveFire,param,time*3));
            if(v.Phase==NativeHazardPhase.Active)return .9f+.1f*Level(DriveFire,param,time*2);
            if(v.Phase==NativeHazardPhase.Clear)return rest+(.9f-rest)*v.Level;return rest;
        }
        /// <summary>The flicker drives (0..about 1.3): sodium's stutter, a fluorescent's flicker, fire's beat.</summary>
        public static float Level(byte drive,double param,double time)
        {
            switch(drive)
            {
                case DriveFlicker:{double k=Math.Floor(time*9+param*37);return Hash((int)k,(int)(param*997),5)<.12?.25f:1;}
                case DriveSodium:{double k=Math.Floor(time*4.3+param*17);double h=Hash((int)k,(int)(param*991),7);return h<.07?.12f:h<.12?.55f:1;}
                case DriveFire:{double t=time+param*13;return (float)(.82+.1*Math.Sin(t*7.3)+.06*Math.Sin(t*13.1+1.3)+.05*Math.Sin(t*3.1+.4));}
                default:return 1;
            }
        }
        /// <summary>
        /// A molten piece's shader state this frame (pure): its heat where the flow starts and ends, the crust share, how far along
        /// the flow the hot metal reaches (`front`, 0..1), the flow's speed scale and whether it shows. Runners tap in the warning
        /// (the trough brightens from the furnace end), run live, crust over in the cool-down and rest plugged (a dull red glow at
        /// the taphole); the pour's stream falls only while the ladle pours; the slag tongue spreads in its first seconds live and
        /// cools over the cool-down to a dark crust that stays.
        /// </summary>
        /// `music`: the zone boss song: the furnace and converter pools take its heat (NativeIronworksMusic.MusicHeat); hazard metal never.
        public static void MoltenState(in NativeMoltenPiece m,IronworksHazardView[] views,double time,out float heatA,out float heatB,out float crust,out float front,out float flow,out bool shown,in NativeZoneBossBeat music=default)
        {
            heatA=m.HeatA;heatB=m.HeatB;crust=m.Crust;front=1;flow=1;shown=true;var v=m.Hazard>=0&&views!=null&&m.Hazard<views.Length?views[m.Hazard]:default;
            switch(m.Drive)
            {
                case MDriveRunner:
                {
                    float rest=v.Rate==NativeIronworksHazards.RateOff?.16f:.24f;
                    if(v.Phase==NativeHazardPhase.Warn){front=(float)Smooth(v.Level)*.35f;heatA=rest+(m.HeatA-rest)*v.Level;heatB=rest;crust=.55f;flow=.4f+.6f*v.Level;}
                    else if(v.Phase==NativeHazardPhase.Active){front=(float)Math.Min(1,.35+.65*Smooth(v.Since/1.2));crust=m.Crust;}
                    else if(v.Phase==NativeHazardPhase.Clear){float c=v.Level;heatA=rest+(m.HeatA-rest)*c;heatB=rest*.6f+(m.HeatB-rest*.6f)*c*c;crust=m.Crust+(.92f-m.Crust)*(1-c);flow=.15f+.85f*c;}
                    // With no hazard clock (normal play today): the whole trough under its crust, a dull red in the cracks (residual
                    // warmth, never a live run's white-orange). Between taps in a running cycle: plugged, cold beyond the first metres.
                    else if(v.Rate==NativeIronworksHazards.RateOff){heatA=.28f;heatB=.17f;crust=.9f;flow=.04f;front=1;}
                    else{heatA=rest;heatB=.05f;crust=.92f;flow=.12f;front=.22f;}
                    break;
                }
                case MDriveStream:
                {
                    var l=views!=null&&NativeIronworksHazards.Ladle<views.Length?views[NativeIronworksHazards.Ladle]:default;
                    if(l.Parked||l.Phase!=NativeHazardPhase.Active&&!(l.Phase==NativeHazardPhase.Clear&&l.Level>.6f)){shown=false;break;}
                    // The stream reaches down from the lip over its first .35 s and thins from the top as the ladle rights.
                    double reach=l.Phase==NativeHazardPhase.Active?Math.Min(1,l.Since/.35):1;front=(float)reach;if(m.Param>reach+.02){shown=false;break;}
                    if(l.Phase==NativeHazardPhase.Clear){heatA*=.85f;heatB*=.85f;}break;
                }
                case MDriveLadle:{var l=views!=null&&NativeIronworksHazards.Ladle<views.Length?views[NativeIronworksHazards.Ladle]:default;if(l.Parked){heatA=heatB=.22f;crust=.85f;flow=.1f;}else if(l.Phase==NativeHazardPhase.Active){flow=2.2f;crust=.45f;}break;}
                case MDriveMold:
                {
                    // A mold under the pour fills while it pours and keeps its heat between pours (cooling over the period).
                    var l=views!=null&&NativeIronworksHazards.Ladle<views.Length?views[NativeIronworksHazards.Ladle]:default;double since=l.Phase==NativeHazardPhase.Active?0:l.Phase==NativeHazardPhase.Clear?l.Since:l.Phase==NativeHazardPhase.Idle?l.Since+2:l.Since+20;
                    float cool=(float)Math.Exp(-since/(m.Param>0?m.Param:18));if(l.Rate==NativeIronworksHazards.RateOff&&!l.Parked)cool*=.5f;heatA=m.HeatB+(m.HeatA-m.HeatB)*cool;heatB=heatA*.9f;crust=.25f+.65f*(1-cool);flow=.3f+.7f*cool;break;
                }
                case MDriveSlagTongue:
                {
                    // Spread east over the first 3 s live (front), burning; then cooling over the cool-down to the dark crust that stays.
                    if(v.Phase==NativeHazardPhase.Active){front=(float)Smooth(Math.Min(1,v.Since/3));crust=.35f;}
                    else if(v.Phase==NativeHazardPhase.Clear){float c=v.Level;heatA=.08f+(m.HeatA-.08f)*c;heatB=.04f+(m.HeatB-.04f)*c;crust=.35f+.55f*(1-c);flow=.1f+.3f*c;}
                    else{heatA=.08f;heatB=.04f;crust=.95f;flow=.02f;}
                    break;
                }
                case MDriveSlab:shown=SlabShown(v);if(v.Phase==NativeHazardPhase.Active){float u=(float)Math.Min(1,v.Since/NativeIronworksHazards.Defs[NativeIronworksHazards.Slab].Live);heatA=heatB=m.HeatA-(m.HeatA-m.HeatB)*u;}else if(v.Phase==NativeHazardPhase.Clear)heatA=heatB=m.HeatB*v.Level;flow=0;break;
                case MDriveBillet:{float f=HazardGlow(v,.75f);heatA*=f;heatB*=f;if(v.Phase==NativeHazardPhase.Active||v.Phase==NativeHazardPhase.Clear&&v.Since<.2){heatA=heatB=Math.Min(1,m.HeatA*1.08f);}flow=0;break;}
                case MDriveSlagPot:{if(v.Phase==NativeHazardPhase.Warn||v.Phase==NativeHazardPhase.Active){heatA=Math.Min(1,m.HeatA+.25f*v.Level);crust=.4f;flow=1.5f;}break;}
                case MDriveFurnace:{float f=Level(DriveFire,m.Param,time);heatA*=.92f+.08f*f;heatB*=.9f+.1f*f;if(music.On){float h=MusicHeat(music);heatA=Math.Min(1,heatA*h);heatB=Math.Min(1,heatB*h);}break;}
                case MDriveConverter:{float f=Level(DriveFire,m.Param,time*.7);heatA*=.9f+.1f*f;if(music.On)heatA=Math.Min(1,heatA*MusicHeat(music));break;}
            }
        }

        // ---------------- the pen ----------------
        private struct Pen {public int Look,Flags,Color;public float Gain,Level,Param;public byte Drive,Role,Frame;public short Hazard;public bool Fade,Detail;}
        private static Pen P(int look,int color,byte role,int flags=0)=>new Pen{Look=look,Color=color,Role=role,Flags=flags,Gain=1,Level=1,Hazard=-1};
        /// <summary>Iron reads clean (the cyber bank's anodised plate: seams and bolts, no blotches under the furnace light, so the
        /// cranes' safety yellow stays paint); only rust-coloured iron keeps the painted steel look's rust.</summary>
        private static Pen Iron(int color=IronHex,byte role=RoleStructure)=>color==RustHex||color==RustDarkHex?P(NativeBlockLooks.SteelGreen,color,role):P(NativeCyberCityLooks.Metal,color,role,NativeCyberCityLooks.Bank);
        private static Pen Metal(int color,byte role=RoleProp)=>P(NativeCyberCityLooks.Metal,color,role,NativeCyberCityLooks.Bank);
        private static Pen Brick(int color,byte role=RoleWall)=>P(NativeBlockLooks.BrickBuff,color,role);
        private static Pen Paint(int color)=>P(NativeBlockLooks.Plain,color,RoleFloor);
        private static Pen Glow(int color,float gain,byte role,byte drive=DriveSteady,float param=0,short hazard=-1)=>new Pen{Look=NativeCyberCityLooks.Tube,Flags=NativeCyberCityLooks.Bank,Color=color,Role=role,Gain=gain,Level=1,Drive=drive,Param=param,Hazard=hazard};
        private static Pen With(Pen p,bool detail=false,bool fade=false,byte frame=FrameStatic,short hazard=-2,byte role=255){p.Detail|=detail;p.Fade|=fade;if(frame!=FrameStatic)p.Frame=frame;if(hazard!=-2)p.Hazard=hazard;if(role!=255)p.Role=role;return p;}
        private static Pen Detail(Pen p){p.Detail=true;return p;}

        /// <summary>Adds a part given in the world at its frame's rest pose (centre, full size along its own axes, world turn);
        /// inside a group a part that meets a keep-out spoils the whole group.</summary>
        private void Box(double x,double y,double z,double sx,double sy,double sz,Pen pen,Rot q)
        {
            if(!(sx>0&&sy>0&&sz>0))return;
            if(Blocked(x,y,z,sx*.5,sy*.5,sz*.5,q)){if(groupStart>=0)groupBad=true;else Rejected++;return;}
            if(pen.Role!=RoleHazard&&!(pen.Role==RoleChain&&Math.Min(sx,sz)*.5<=ChainHalf)&&!Placed(x,y,z,sx*.5,sy*.5,sz*.5,q))return;
            double ox=FrameOrigin[pen.Frame*3],oy=FrameOrigin[pen.Frame*3+1],oz=FrameOrigin[pen.Frame*3+2];
            parts.Add(new NativeIronPart{Parent=parent,Look=pen.Look,Flags=pen.Flags,Color=pen.Color,X=(float)(x-ox),Y=(float)(y-oy),Z=(float)(z-oz),Hx=(float)(sx*.5),Hy=(float)(sy*.5),Hz=(float)(sz*.5),
                Qx=(float)q.X,Qy=(float)q.Y,Qz=(float)q.Z,Qw=(float)q.W,Gain=pen.Gain,Level=pen.Level,Param=pen.Param,Drive=pen.Drive,Role=pen.Role,Room=RoomOf(x,z),Frame=pen.Frame,Hazard=pen.Hazard,Owner=owner,Fade=pen.Fade,Detail=pen.Detail});
        }
        private void Box(double x,double y,double z,double sx,double sy,double sz,Pen pen)=>Box(x,y,z,sx,sy,sz,pen,Rot.None);
        /// <summary>Whether optional dressing fits here (no keep-out met; nothing is counted as rejected).</summary>
        private bool Free(double x0,double x1,double y0,double y1,double z0,double z1){int n=rejectedAt.Count;bool b=Blocked((x0+x1)/2,(y0+y1)/2,(z0+z1)/2,Math.Abs(x1-x0)/2,Math.Abs(y1-y0)/2,Math.Abs(z1-z0)/2,Rot.None);if(rejectedAt.Count>n)rejectedAt.RemoveAt(rejectedAt.Count-1);return !b;}
        /// <summary>An axis-aligned part from its world bounds.</summary>
        private void Span(double x0,double x1,double y0,double y1,double z0,double z1,Pen pen)=>Box((x0+x1)/2,(y0+y1)/2,(z0+z1)/2,Math.Abs(x1-x0),Math.Abs(y1-y0),Math.Abs(z1-z0),pen);
        /// <summary>A member from a to b, `w` wide (its x) and `h` deep (its y), its x axis kept horizontal.</summary>
        private void Member(double ax,double ay,double az,double bx,double by,double bz,double w,double h,Pen pen)
        {
            double dx=bx-ax,dy=by-ay,dz=bz-az,l=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(l<1e-6)return;Box((ax+bx)/2,(ay+by)/2,(az+bz)/2,w,h,l,pen,Along(dx,dy,dz));
        }
        /// <summary>An I-beam from its bounds along its longest axis (flanges top and bottom for a horizontal beam, on its two faces
        /// across the narrow axis for an upright one).</summary>
        private void IBeam(double x0,double x1,double y0,double y1,double z0,double z1,Pen pen,double flange=.035)
        {
            double sx=x1-x0,sy=y1-y0,sz=z1-z0;
            if(sy>=sx&&sy>=sz){bool across=sx<=sz;double c=across?(x0+x1)/2:(z0+z1)/2;if(across){Span(x0,x1,y0,y1,z0,z0+flange,pen);Span(x0,x1,y0,y1,z1-flange,z1,pen);Span(c-.012,c+.012,y0,y1,z0+flange,z1-flange,pen);}else{Span(x0,x0+flange,y0,y1,z0,z1,pen);Span(x1-flange,x1,y0,y1,z0,z1,pen);Span(x0+flange,x1-flange,y0,y1,c-.012,c+.012,pen);}return;}
            Span(x0,x1,y1-flange,y1,z0,z1,pen);Span(x0,x1,y0,y0+flange,z0,z1,pen);
            if(sx>=sz){double c=(z0+z1)/2;Span(x0,x1,y0+flange,y1-flange,c-.012,c+.012,pen);}else{double c=(x0+x1)/2;Span(c-.012,c+.012,y0+flange,y1-flange,z0,z1,pen);}
        }
        /// <summary>An upright ring of `n` plates round (cx, cz) of radius r from y0 to y1 (a vessel, a ladle, a coil).</summary>
        private void Ring(double cx,double y0,double y1,double cz,double r,double t,int n,Pen pen,double phase=0)
        {
            double w=2*r*Math.Tan(Math.PI/n)+.01;for(int i=0;i<n;i++){double a=phase+i*2*Math.PI/n;Box(cx+Math.Sin(a)*(r-t/2),(y0+y1)/2,cz+Math.Cos(a)*(r-t/2),w,y1-y0,t,pen,Yaw(a*180/Math.PI));}
        }
        /// <summary>A ring of `n` plates lying round an axis along x (a coil on its side, a drum) at (y, z), from x0 to x1.</summary>
        private void RingX(double x0,double x1,double cy,double cz,double r,double t,int n,Pen pen)
        {
            double w=2*r*Math.Tan(Math.PI/n)+.004;for(int i=0;i<n;i++){double a=i*2*Math.PI/n;Box((x0+x1)/2,cy+Math.Cos(a)*(r-t/2),cz+Math.Sin(a)*(r-t/2),x1-x0,w,t,pen,AxisRot(1,0,0,a*180/Math.PI-90));}
        }
        /// <summary>A small cylinder along x as an octagonal prism (two square bars, one turned 45 degrees): a roller, a pipe, a bar.</summary>
        private void RodX(double x0,double x1,double cy,double cz,double r,Pen pen){double s=r*1.72;Box((x0+x1)/2,cy,cz,x1-x0,s,s,pen);Box((x0+x1)/2,cy,cz,x1-x0,s,s,pen,AxisRot(1,0,0,45));}
        private void RodZ(double z0,double z1,double cx,double cy,double r,Pen pen){double s=r*1.72;Box(cx,cy,(z0+z1)/2,s,s,z1-z0,pen);Box(cx,cy,(z0+z1)/2,s,s,z1-z0,pen,AxisRot(0,0,1,45));}
        private void RodY(double y0,double y1,double cx,double cz,double r,Pen pen){double s=r*1.72;Box(cx,(y0+y1)/2,cz,s,y1-y0,s,pen);Box(cx,(y0+y1)/2,cz,s,y1-y0,s,pen,Yaw(45));}
        /// <summary>A chain hanging from (x, top, z) to `bottom`: links of `link` m alternating their turn, the hook at the end.</summary>
        private void Chain(double x,double top,double bottom,double z,double link,Pen pen,bool hook=true,double phase=0)
        {
            int n=Math.Max(1,(int)Math.Floor((top-bottom)/(link*.8)));double step=(top-bottom)/n;var c=With(pen,role:RoleChain);
            for(int i=0;i<n;i++){double y=top-(i+.5)*step;var q=Yaw(phase+(i%2==0?0:90));Box(x,y,z,link*.42,step*1.08,.022,c,q);}
            if(hook){var h=With(Iron(HookHex),role:RoleChain);Box(x,bottom-.09,z,.1,.16,.05,h);Box(x+.035,bottom-.21,z,.05,.12,.05,h);Box(x-.005,bottom-.285,z,.09,.04,.05,h);}
        }
        /// <summary>A block letter sign on a face: `face` 0 looks toward +x from the plane x = `plane`, 1 toward -x, 2 toward +z,
        /// 3 toward -z; `u` the text's centre along the face (world z for faces 0 and 1, world x for 2 and 3), the baseline at y,
        /// glyph cells `cell` m, standing `d` out of the plane, `depth` deep (NativeExpeditionCityKit.GlyphBoxes' 3 x 5 glyphs).
        /// The letters run along the viewer's right: +z on face 0, -z on 1, -x on 2, +x on 3.</summary>
        private void Text(string text,double u,double y,double plane,int face,double cell,double d,double depth,Pen pen)
        {
            double width=text.Length*4*cell-cell,s=face==0||face==3?1:-1;
            for(int i=0;i<text.Length;i++)
            {
                double lu=-width/2+i*4*cell;
                foreach(var g in NativeExpeditionCityKit.GlyphBoxes(text[i]))
                {
                    double t=u+s*(lu+(g[0]+g[2]*.5)*cell),cy=y+(5-g[1]-g[3]*.5)*cell,w=g[2]*cell,h=g[3]*cell;
                    switch(face)
                    {
                        case 0:Box(plane+d+depth/2,cy,t,depth,h,w,pen);break;
                        case 1:Box(plane-d-depth/2,cy,t,depth,h,w,pen);break;
                        case 2:Box(t,cy,plane+d+depth/2,w,h,depth,pen);break;
                        default:Box(t,cy,plane-d-depth/2,w,h,depth,pen);break;
                    }
                }
            }
        }
        public static double TextWidth(string text,double cell)=>text.Length*4*cell-cell;
        private void Begin(){groupStart=parts.Count;moltenStart=molten.Count;glowStart=glows.Count;groupBad=false;}
        private bool End(){bool ok=!groupBad;if(!ok){Rejected++;parts.RemoveRange(groupStart,parts.Count-groupStart);molten.RemoveRange(moltenStart,molten.Count-moltenStart);glows.RemoveRange(glowStart,glows.Count-glowStart);}groupStart=moltenStart=glowStart=-1;groupBad=false;return ok;}

        /// <summary>Adds a molten surface (world centre at its frame's rest pose, full size, flow axis and sign).</summary>
        private void Melt(double x,double y,double z,double sx,double sy,double sz,byte kind,byte axis,int sign,float speed,float heatA,float heatB,float crust,byte drive=MDriveSteady,short hazard=-1,byte frame=FrameStatic,float param=0,float gain=1,bool detail=false)
        {
            if(!(sx>0&&sy>0&&sz>0))return;if(Blocked(x,y,z,sx*.5,sy*.5,sz*.5,Rot.None)){if(groupStart>=0)groupBad=true;else Rejected++;return;}
            if(hazard<0&&!Placed(x,y,z,sx*.5,sy*.5,sz*.5,Rot.None))return;
            double ox=FrameOrigin[frame*3],oy=FrameOrigin[frame*3+1],oz=FrameOrigin[frame*3+2];
            molten.Add(new NativeMoltenPiece{Parent=parent,X=(float)(x-ox),Y=(float)(y-oy),Z=(float)(z-oz),Hx=(float)(sx*.5),Hy=(float)(sy*.5),Hz=(float)(sz*.5),Qw=1,Speed=speed,HeatA=heatA,HeatB=heatB,Crust=crust,Gain=gain,
                Seed=(float)Hash(molten.Count,77,(int)(x*13+z*7)),Param=param,Kind=kind,Axis=axis,Sign=(sbyte)(sign<0?-1:1),Drive=drive,Room=RoomOf(x,z),Frame=frame,Hazard=hazard,Owner=owner,Detail=detail});
        }
        private void MeltSpan(double x0,double x1,double y0,double y1,double z0,double z1,byte kind,byte axis,int sign,float speed,float heatA,float heatB,float crust,byte drive=MDriveSteady,short hazard=-1,byte frame=FrameStatic,float param=0,float gain=1,bool detail=false)
        =>Melt((x0+x1)/2,(y0+y1)/2,(z0+z1)/2,Math.Abs(x1-x0),Math.Abs(y1-y0),Math.Abs(z1-z0),kind,axis,sign,speed,heatA,heatB,crust,drive,hazard,frame,param,gain,detail);
        /// <summary>A light source: place, sRGB colour times intensity, reach, size, kind, light field channel and hazard.</summary>
        /// <summary>A light riding a moving frame (local coordinates in the frame's rest space).</summary>
        private void LightFrame(double lx,double ly,double lz,int hex,double intensity,double range,byte kind,short hazard,byte frame)
        {
            double x=FrameOrigin[frame*3]+lx,z=FrameOrigin[frame*3+2]+lz;
            glows.Add(new NativeIronGlow{X=(float)lx,Y=(float)ly,Z=(float)lz,R=(float)(NativeSceneryBuilder.Channel(hex,16)*intensity),G=(float)(NativeSceneryBuilder.Channel(hex,8)*intensity),B=(float)(NativeSceneryBuilder.Channel(hex,0)*intensity),Range=(float)range,Size=1,Kind=kind,Room=RoomOf(x,z),Frame=frame,Channel=-1,Hazard=hazard});
        }
        /// <summary>A glow's world place this frame (its frame's pose applied).</summary>
        public static void GlowAt(in NativeIronGlow g,IronworksHazardView[] views,double time,out float x,out float y,out float z)
        {
            if(g.Frame==FrameStatic){x=g.X;y=g.Y;z=g.Z;return;}
            FramePose(g.Frame,views,time,out float px,out float py,out float pz,out float qx,out float qy,out float qz,out float qw);Rotate(qx,qy,qz,qw,g.X,g.Y,g.Z,out double ox,out double oy,out double oz);x=(float)(px+ox);y=(float)(py+oy);z=(float)(pz+oz);
        }
        private void Light(double x,double y,double z,int hex,double intensity,double range,byte kind,sbyte channel=-1,short hazard=-1,double size=1,double param=0)
        {
            glows.Add(new NativeIronGlow{X=(float)x,Y=(float)y,Z=(float)z,R=(float)(NativeSceneryBuilder.Channel(hex,16)*intensity),G=(float)(NativeSceneryBuilder.Channel(hex,8)*intensity),B=(float)(NativeSceneryBuilder.Channel(hex,0)*intensity),Range=(float)range,Size=(float)size,Param=(float)param,Kind=kind,Room=RoomOf(x,z),Channel=channel,Hazard=hazard});
        }

        // ---------------- keep-outs ----------------
        // What a part may never meet (world AABBs; a spawn's own dressing passes its own keep-outs): spawn exits and their paths,
        // door spawns' doorways, floor vents, drop columns, slots, caches, boss points, the doors' and the shutter's volumes, the
        // approved machine spots.
        private void KeepOuts()
        {
            if(area==null)return;var spawns=area.spawns??Array.Empty<ExpeditionSpawnLayout>();
            for(short s=0;s<spawns.Length;s++)
            {
                var e=spawns[s]?.entry;if(e?.from==null||e.exit==null)continue;float clear=Math.Max(1.0f,e.clear),fy=e.exit.y;
                // From the spawn's floor up (what lies under its floor, a deck's joists, blocks nothing).
                float fl=fy-.05f;
                if(e.kind=="drop"||e.kind=="vent"&&e.surface=="ceiling"){Keep(e.exit.x-clear,e.exit.x+clear,fl,fy+Math.Max(3.2f,e.height+.2f),e.exit.z-clear,e.exit.z+clear,s,1);continue;}
                Keep(e.exit.x-clear,e.exit.x+clear,fl,fy+(e.kind=="breach"?4.4f:3.2f),e.exit.z-clear,e.exit.z+clear,s,0);
                if(e.kind=="vent")Keep(e.from.x-.9f,e.from.x+.9f,fl,fy+3.2f,e.from.z-.9f,e.from.z+.9f,s,0);
                float x0=Math.Min(e.from.x,e.exit.x),x1=Math.Max(e.from.x,e.exit.x),z0=Math.Min(e.from.z,e.exit.z),z1=Math.Max(e.from.z,e.exit.z);bool alongX=x1-x0>z1-z0;
                float w=e.kind=="breach"?2.0f:1.1f;Keep(alongX?x0:x0-w,alongX?x1:x1+w,fl,fy+(e.kind=="breach"?4.4f:3.0f),alongX?z0-w:z0,alongX?z1+w:z1,s,0);
            }
            void Point(ExpeditionPoint p,float r,float h){if(p!=null)Keep(p.x-r,p.x+r,p.y-.05f,p.y+h,p.z-r,p.z+r,-1,2);}
            foreach(var p in area.slots??Array.Empty<ExpeditionPoint>())Point(p,.9f,2.4f);
            foreach(var p in area.bossPoints??Array.Empty<ExpeditionPoint>())Point(p,1.6f,4f);
            foreach(var i in area.interactions??Array.Empty<ExpeditionInteractionLayout>())Keep(i.x-1.1f,i.x+1.1f,i.y-.05f,i.y+2.4f,i.z-1.1f,i.z+1.1f,-1,2);
            foreach(var d in area.doors??Array.Empty<ExpeditionDoorLayout>())
            {
                Volume(d.interact,.3f);Volume(d.closure,.5f);Point(d.approach,.9f,2.4f);foreach(var p in d.entries??Array.Empty<ExpeditionPoint>())Point(p,.9f,2.4f);foreach(var p in d.returns??Array.Empty<ExpeditionPoint>())Point(p,.9f,2.4f);
                if(d.closure!=null){var u=NativeExpeditionCityKit.Unit(d.closure);Keep(u[0],u[1],-1,u[3],u[4],u[5],-1,2);}
            }
            foreach(var g in area.gates??Array.Empty<ExpeditionGateLayout>())Volume(g.closure,.05f,0);
            // The approved machine spots (owner, 2026-10-16; docs/EXPEDITION-CITY-IRONWORKS.md section 2.5): the machine and its front.
            for(int m=0;m<MachineSpots.Length/3;m++){float x=MachineSpots[m*3],y=MachineSpots[m*3+1],z=MachineSpots[m*3+2];Keep(x-1.1f,x+1.1f,y-.05f,y+2.6f,z-1.1f,z+1.1f,-1,2);}
            // The live machines (the manifest's records): the machine, the spot its user stands on, where it dispenses and drops chips.
            foreach(var m in area.machines??Array.Empty<PveMachine>())
            {
                if(m==null)continue;float x0=m.x,x1=m.x,z0=m.z,z1=m.z;
                foreach(var p in new[]{m.front,m.dispense,m.chipDrop})if(p!=null){x0=Math.Min(x0,p.x);x1=Math.Max(x1,p.x);z0=Math.Min(z0,p.z);z1=Math.Max(z1,p.z);}
                Keep(x0-1.1f,x1+1.1f,m.y-.05f,m.y+2.6f,z0-1.1f,z1+1.1f,-1,2);
            }
        }
        /// <summary>The approved machine spots (x, y, z): chip and med in the pattern shop, the second chip machine on the coke
        /// walkway, the recruit post in the time office (the draft record's `machines`).</summary>
        public static readonly float[] MachineSpots={46.6f,0,-16f, 46.6f,0,-24f, -30f,6,26.6f, 39f,0,7f};
        private void Volume(ExpeditionVolume v,float pad,float top=0){if(v==null)return;var b=NativeExpeditionCityKit.Aabb(v,pad);Keep(b[0],b[1],b[2]-.2f,b[3]+top,b[4],b[5],-1,2);}
        /// <summary>The placement rule (as Tests/IronworksVisualChecks.cs recomputes it): within <see cref="Low"/> m over the ground,
        /// or over the ring where a deck lies under it, a part stands within <see cref="WallBand"/> of some block, or is flat paint.
        /// A part that fails is left out (counted in <see cref="Unbacked"/>).</summary>
        private bool Placed(double x,double y,double z,double hx,double hy,double hz,Rot q)
        {
            Extent(x,y,z,hx,hy,hz,q.X,q.Y,q.Z,q.W,out double x0,out double x1,out double y0,out double y1,out double z0,out double z1);
            if(IsFlat(y0,y1)||!LowAt(x0,x1,y0,y1,z0,z1,blocks))return true;if(Backed(x0,x1,y0,y1,z0,z1,blocks))return true;
            Unbacked++;unbackedAt.Add(new NativeIronKeep{X0=(float)x0,X1=(float)x1,Y0=(float)y0,Y1=(float)y1,Z0=(float)z0,Z1=(float)z1,Spawn=-1});return false;
        }
        /// <summary>Whether a world AABB lies within 3.1 m over the ground, or over the ring with a deck under it.</summary>
        /// <summary>Whether a block other than the slab or roof stands on the ground within `pad` of (x, z) (flat floor dressing skips it).</summary>
        private bool OverBlock(double x,double z,double pad)
        {
            foreach(var b in blocks){if(b?.id==null||b.pos.y-b.size.y/2>.2||Stem(b.id)=="slab"||Stem(b.id)=="roof")continue;if(Math.Abs(x-b.pos.x)<b.size.x/2+pad&&Math.Abs(z-b.pos.z)<b.size.z/2+pad)return true;}
            return false;
        }
        public static bool LowAt(double x0,double x1,double y0,double y1,double z0,double z1,ContentBlock[] blocks)
        {
            if(y0<Low-.001)return !OnTop(x0,x1,y0,z0,z1,blocks);if(!(y0<RingFloor+Low-.001&&y1>RingFloor+.001))return false;
            foreach(var d in blocks){if(d?.id==null||!d.id.Contains("-deck-")||d.id.Contains("-rail-")||d.id.Contains("-col-"))continue;if(x1>X0(d)&&x0<X1(d)&&z1>Z0(d)&&z0<Z1(d))return true;}
            return false;
        }
        /// <summary>Whether a part rests on top of a block too tall to stand on (its top 1.5 m or more over the ground: the slag bank, a
        /// stand), its footprint inside the block's: no one walks there, so nothing it holds stands in anyone's way.</summary>
        public static bool OnTop(double x0,double x1,double y0,double z0,double z1,ContentBlock[] blocks)
        {
            foreach(var b in blocks){if(b==null||b.id==null||b.id.Contains("-deck-"))continue;double top=Y1(b);if(top<1.5||y0<top-.05||y0>top+.6)continue;if(x0>=X0(b)-.05&&x1<=X1(b)+.05&&z0>=Z0(b)-.05&&z1<=Z1(b)+.05)return true;}
            return false;
        }
        /// <summary>Whether a world AABB lies inside some block's AABB grown by the wall band.</summary>
        public static bool Backed(double x0,double x1,double y0,double y1,double z0,double z1,ContentBlock[] blocks)
        {
            const double e=1e-4;double w=WallBand;
            foreach(var b in blocks){if(b==null)continue;if(x0>=X0(b)-w-e&&x1<=X1(b)+w+e&&y0>=Y0(b)-w-e&&y1<=Y1(b)+w+e&&z0>=Z0(b)-w-e&&z1<=Z1(b)+w+e)return true;}
            return false;
        }
        /// <summary>Whether a part from y0 to y1 is flat paint on a floor one can stand on (the ground or the ring's decks).</summary>
        public static bool IsFlat(double y0,double y1)=>y1-y0<=Flat+1e-4&&(Math.Abs(y0-Ground)<.03||Math.Abs(y0-RingFloor)<.03);
        private void Keep(float x0,float x1,float y0,float y1,float z0,float z1,short spawn,byte kind)=>keeps.Add(new NativeIronKeep{X0=x0,X1=x1,Y0=y0,Y1=y1,Z0=z0,Z1=z1,Spawn=spawn,Kind=kind});
        private bool Blocked(double x,double y,double z,double hx,double hy,double hz,Rot q)
        {
            Extent(x,y,z,hx,hy,hz,q.X,q.Y,q.Z,q.W,out double x0,out double x1,out double y0,out double y1,out double z0,out double z1);
            // Flat paint on a floor (decals, plates, rails, grates' frames) blocks nothing and may lie anywhere.
            if(IsFlat(y0,y1))return false;
            foreach(var k in keeps){if(k.Spawn>=0&&k.Spawn==owner)continue;if(x1<=k.X0||x0>=k.X1||y1<=k.Y0||y0>=k.Y1||z1<=k.Z0||z0>=k.Z1)continue;rejectedAt.Add(new NativeIronKeep{X0=(float)x0,X1=(float)x1,Y0=(float)y0,Y1=(float)y1,Z0=(float)z0,Z1=(float)z1,Spawn=k.Spawn,Kind=k.Kind});return true;}
            return false;
        }
        /// <summary>The spawn whose own dressing follows (its keep-outs do not apply to it), by its exit; -1 none.</summary>
        private short SpawnAt(double x,double y,double z,double within=.6)
        {
            var spawns=area?.spawns;if(spawns==null)return -1;
            for(short s=0;s<spawns.Length;s++){var e=spawns[s]?.entry;if(e?.exit==null)continue;if(Math.Abs(e.exit.x-x)<=within&&Math.Abs(e.exit.z-z)<=within&&Math.Abs(e.exit.y-y)<=1.5)return s;}
            return -1;
        }
        private void Own(short spawn)=>owner=spawn;

        // ---------------- budget ----------------
        /// <summary>Parts, molten pieces and light sources per room (Rooms long each), for the budget lines and checks.</summary>
        public void Counts(int[] parts,int[] molten,int[] lights,bool low=false)
        {
            Array.Clear(parts,0,parts.Length);Array.Clear(molten,0,molten.Length);Array.Clear(lights,0,lights.Length);
            foreach(var p in Parts){if(low&&p.Detail)continue;parts[p.Room]++;}foreach(var m in Molten){if(low&&m.Detail)continue;molten[m.Room]++;}foreach(var g in Glows)lights[g.Room]++;
        }
        /// <summary>Instanced draws of the kit at 500 instances a draw (NativeBlockLooks.Chunk), the molten pieces' draws.</summary>
        public int Draws(bool low){int n=0;foreach(var p in Parts)if(!(low&&p.Detail))n++;return (n+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk;}
        public int MoltenDraws(bool low){int n=0;foreach(var m in Molten)if(!(low&&m.Detail))n++;return (n+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk;}
    }
}
