using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// One town dressing part (presentation only, never sim data): a box in its parent block's rest frame (centre offset from the
    /// block's rest centre in the block's own axes; half extents; its own turn in that frame), drawn through NativeBlockLit like
    /// an arena block. `Look` and `Flags` as NativeBlockLooks.Resolve gives them (a shared bank slot carries
    /// NativeCyberCityLooks.Bank), `Color` sRGB, `Gain` the emission scale (HDR above 1), `Level` the window light the shader
    /// reads (0 keeps a window-look pane dark), `Drive` what animates it (<see cref="NativeExpeditionCityKit.Drive"/>, its phase
    /// in `Param`). `Role` says what the part is for (the checks hold each role to its own placement rule). `Shadow`: big parts
    /// that cast. `Fade`: hidden while it sits between the camera and the pilot. `Detail`: fine dressing the low graphics tier
    /// and compatibility rendering leave out. `Face`: 1 + the index of the building face it stands on in
    /// <see cref="NativeExpeditionCityKit.FaceCulls"/> (0 none), `Back` how far it may be seen past that face's edges from behind
    /// (its inset from the edges over how far it stands out of the face): the runtime leaves it undrawn while the building hides
    /// it from the camera (<see cref="NativeExpeditionCityKit.Hide"/>); never set on a part that casts.
    /// </summary>
    public struct NativeCityPart
    {
        public int Parent,Look,Flags,Color;public float X,Y,Z,Hx,Hy,Hz,Qx,Qy,Qz,Qw,Gain,Level,Param,Back;public byte Drive,Role;public bool Shadow,Fade,Detail;public short Face;
    }
    /// <summary>A building face the town's parts stand on, as the runtime's back-face rule reads it (world, metres): the face's
    /// plane `Plane` across axis `Axis` (0 x, 2 z), looking out along `Sign`; the building `Depth` deep behind it; its span
    /// `U0..U1` along the other horizontal axis and its height `Y0..Y1`, both shrunk by the blocks' chamfer
    /// (<see cref="NativeBlockLooks.Chamfer"/>), where the drawn block is solid; `Grounded` when the building stands on the town's
    /// floor (nothing is seen under it).</summary>
    public struct NativeCityFaceCull {public byte Axis;public sbyte Sign;public bool Grounded;public float Plane,Depth,U0,U1,Y0,Y1;}
    /// <summary>A light or effect source of the town: world place (for a fire the burning surface under the flames), linear colour
    /// times intensity, the reach of its light (m), the size of its flames or halo, its phase and its kind
    /// (<see cref="NativeExpeditionCityKit"/> Glow* constants).</summary>
    public struct NativeCityGlow {public float X,Y,Z,R,G,B,Range,Size,Param;public byte Kind;}

    /// <summary>
    /// The Expedition town's dressing kit (docs/EXPEDITION-LAYOUT.md "Town dressing"): a zombie city at dusk in the game's blocky
    /// language, laid on the 124 greybox blocks of `expedition-city` (docs/arenas/scripts/expedition_city.py) by block id stem,
    /// with the town's records (Resources/ExpeditionLayout.json, area exp-city) kept clear. Built once per map content; nothing
    /// here collides, moves a block or enters the network frame.
    /// <list type="bullet">
    /// <item>Facades on every exposed face of the building masses: punched windows in storeys (dark glass, a few lit warm or by a
    /// television, boarded, broken and sooted, two burning), sills, lintels, string courses, cornices and parapets; storefronts
    /// with shop signs (original names in block letters, neon or painted), roller shutters, awnings; residential doors; office
    /// lobby glass; depot roller doors; fire escapes on the side and alley faces.</item>
    /// <item>Landmarks: the city hall's pilasters, tall windows, CITY HALL lettering and the stopped clock; the Meridian
    /// Biocontainment frontage (white cladding, cold ribbon windows, its sign, the WARD ZERO canopy, the rotating beacon); the
    /// sealed foundry and the boarded transit station; the final checkpoint's blast walls, razor wire, floodlights and lettering.</item>
    /// <item>Street: wall lamps (working, flickering, dead), flashing traffic signals, road paint, litter, blood, scorch marks,
    /// graffiti (wayfinding SANCTUARY arrows among it), posters and quarantine notices; the vehicles dressed on their blocks
    /// (wheels, glass, lights, the crashed bus, ambulance, taxi, police cars with one live light bar, burnt and overturned wrecks).</item>
    /// <item>The sanctuary: sandbag courses, sheet metal and planks on the barricades, the brazier's drum and coals, four lamp
    /// posts on the barricade corners with a string of bulbs, spent casings behind the barricades.</item>
    /// <item>Spawns, from the layout: every door spawn gets a dark doorway with its frame and a torn leaf, every vent a street grate;
    /// a breach panel is dressed as a bricked-up shopfront (its parts hide when the breach hides the panel).</item>
    /// </list>
    /// Placement rules (Tests/ExpeditionDressingChecks.cs): below 3 m nothing stands out more than <see cref="WallBand"/> from a
    /// block except flat paint (at most <see cref="Flat"/> tall); nothing lies on a spawn's exit, its doorway or its drop column,
    /// on a vent, a slot, a return, the approach, an interaction point, a boss point, the door's or the gate's volumes; the
    /// sanctuary's floor holds nothing but flat paint. Every part's look and place derive from its parent's id and the layout.
    /// Pure: the same blocks and area give the same parts in the same order; <see cref="Drive"/> is a pure function of the part
    /// and the clock. Parts flush with a block or an earlier part stand clear of it by whole 2 mm steps (NativeSurfaceLayers).
    /// </summary>
    public sealed partial class NativeExpeditionCityKit
    {
        public const string MapId="expedition-city",Prefix="expedition-city-";
        /// <summary>Whether a map id is the town: every town hook is inert on any other map.</summary>
        public static bool Active(string mapId)=>mapId==MapId||mapId==NativeExpeditionGeneratedWorld.MapId;
        public const byte DriveSteady=0,DriveFlicker=1,DriveFire=2,DriveBlink=3,DriveStrobe=4,DriveTv=5,DrivePolice=6,DriveBulbs=7;
        public const byte RoleFacade=0,RoleGround=1,RoleClutter=2,RoleSpawnDoor=3,RoleVent=4,RoleRoof=5,RoleVehicle=6,RoleSanctuary=7,RoleBreach=8,RoleSign=9,RoleLamp=10,RoleGate=11,RoleHigh=12,RoleLandmark=13;
        public const byte GlowLamp=0,GlowLampFlicker=1,GlowFire=2,GlowBrazier=3,GlowNeon=4,GlowCold=5,GlowWarning=6,GlowBeacon=7,GlowPolice=8,GlowBulbs=9,GlowWindowFire=10,GlowFlood=11;
        /// <summary>How far below 3 m a part may stand out from a block (m), the most flat paint may rise (m), and the height under
        /// which the wall band applies (a Hulk is 3.06 m tall).</summary>
        public const float WallBand=.25f,Flat=.06f,Low=3.1f;
        /// <summary>How far from the camera the runtime draws a part (m; exp-layout-7, the city grown 24 m out on three arms), by its
        /// size L (its largest full extent), so the per-frame cost follows what the camera can see rather than the town's size:
        /// anything that glows (the tube look of the light bank, or any animated drive: lit windows, neon, lamps, fires, beacons)
        /// to `far`; other parts to <see cref="PartBase"/> + <see cref="PerMetre"/> L (a 0.5 m lamp head 81 m, a 1 m window 92 m,
        /// 6 m and longer to `far`); fine detail (`Detail`, which the low tier never draws, glowing or not) to <see cref="DetailBase"/> +
        /// <see cref="PerMetre"/> L, at most <see cref="DetailRange"/> (a 0.2 m sill 39 m, a 1 m poster 57 m). Every range stops at
        /// `far`, the look's linear fog end plus <see cref="FarMargin"/> (<see cref="FarRange"/>), where the fog covers a part and
        /// its block alike. Below about 4-6 pixels at 1080p when it goes. The city detail pass (2026-10-20) paid for its pieces
        /// here: 80/50/25 m and 110 m became 70/35/22 m and 80 m, with the back-face rule (<see cref="Hide"/>), so a frame at the
        /// city check's six points draws no more than exp-layout-6 did.</summary>
        public const float PartBase=70,DetailBase=35,PerMetre=22,DetailRange=80,FarMargin=25,FarDefault=200;
        public static float FarRange(MapLook look)=>look?.fog!=null&&look.fog.mode=="linear"&&look.fog.end>0?look.fog.end+FarMargin:FarDefault;
        /// <summary>The distance (m) to which part p is drawn, with <see cref="FarRange"/> `far`.</summary>
        public static float Range(in NativeCityPart p,float far)
        {
            float size=2*Math.Max(p.Hx,Math.Max(p.Hy,p.Hz));
            // A glow to the far range, except fine detail that glows (the detail pass's puddles and candles): its own range.
            if(p.Drive!=DriveSteady||p.Look==NativeCyberCityLooks.Tube&&(p.Flags&NativeCyberCityLooks.Bank)!=0)return p.Detail?Math.Min(Math.Min(DetailRange,far),DetailBase+PerMetre*size):far;
            return p.Detail?Math.Min(Math.Min(DetailRange,far),DetailBase+PerMetre*size):Math.Min(far,PartBase+PerMetre*size);
        }
        /// <summary>Whether part p is drawn `distanceSquared` m² from the camera (its rest spot), with <see cref="FarRange"/> `far`.</summary>
        public static bool Drawn(in NativeCityPart p,float distanceSquared,float far){float r=Range(p,far);return distanceSquared<=r*r;}
        /// <summary>The back-face rule (Claude, 2026-10-20, the city detail pass). How far outside a building's block (grown by this
        /// margin) the eye must stand for its parts to be left out, so no near clip plane ever opens the block (m).</summary>
        public const float BackMargin=.5f;
        /// <summary>The building faces parts may be hidden behind (index k is a part's `Face` k + 1).</summary>
        public NativeCityFaceCull[] FaceCulls=Array.Empty<NativeCityFaceCull>();
        /// <summary>
        /// Fills `ratio` (length FaceCulls.Length + 1; [0] stays +inf) for an eye at (x, y, z): for face k + 1, when the eye stands
        /// behind the face's plane by e and outside its building grown by <see cref="BackMargin"/>, the eye's reach past the face's
        /// edges (its distance beyond the span or over the top; under the floor never counts) over e, else +inf. Part p on that face
        /// is hidden when ratio[p.Face] &lt;= p.Back: then a line from the eye to any point of the part crosses the face's plane
        /// inside the solid face (a part of the face s m in from every edge and d m out of it is seen past an edge only when the
        /// eye's reach past that edge exceeds s e / d), so it passes through the building's block, which the eye sees from outside.
        /// Allocation free.
        /// </summary>
        public void Hide(float x,float y,float z,float[] ratio)
        {
            var f=FaceCulls;ratio[0]=float.PositiveInfinity;
            for(int k=0;k<f.Length;k++)
            {
                ref readonly var c=ref f[k];float n=c.Axis==0?x:z,u=c.Axis==0?z:x;float e=(c.Plane-n)*c.Sign;
                float reach=Math.Max(0,Math.Max(u-c.U1,c.U0-u)),over=Math.Max(0,y-c.Y1),under=c.Grounded?0:Math.Max(0,c.Y0-y);float past=Math.Max(reach,Math.Max(over,under));
                bool outside=past>=BackMargin||e>=c.Depth+BackMargin;
                ratio[k+1]=e>0&&outside?past/e:float.PositiveInfinity;
            }
        }
        /// <summary>The runtime's selection of part p from an eye `distanceSquared` m² away: its range, then the back-face rule
        /// (`ratio` as <see cref="Hide"/> filled it for that eye; null skips it).</summary>
        public static bool Drawn(in NativeCityPart p,float distanceSquared,float far,float[] ratio)=>(ratio==null||p.Face==0||ratio[p.Face]>p.Back)&&Drawn(p,distanceSquared,far);
        public NativeCityPart[] Parts=Array.Empty<NativeCityPart>();public NativeCityGlow[] Glows=Array.Empty<NativeCityGlow>();
        /// <summary>The keep-out boxes the kit honoured (world AABBs and their owner spawn, -1 none), for the checks.</summary>
        public NativeCityKeep[] Keeps=Array.Empty<NativeCityKeep>();
        /// <summary>Counts by role, for the budget lines.</summary>
        public int Rejected;

        private readonly List<NativeCityPart> parts=new List<NativeCityPart>();private readonly List<NativeCityGlow> glows=new List<NativeCityGlow>();private readonly List<NativeCityKeep> keeps=new List<NativeCityKeep>();
        private ContentBlock[] blocks;private ExpeditionAreaLayout area;private readonly Dictionary<string,int> byStem=new Dictionary<string,int>();
        private int groupStart=-1,glowStart=-1;private bool groupBad,sanctuaryOk;private short owner=-1;

        // ---------------- palette (sRGB) ----------------
        public const int GlassHex=0x1C2230,GlassSkyHex=0x6A5C88,GlassDuskHex=0x8A6070,GlassBlueHex=0x232C3C,FrameHex=0x26221F,SillHex=0x8A8276,LintelHex=0x9C9284,SootHex=0x15120F,BoardHex=0x8A6A44,BlackHex=0x070708,
            WarmHex=0xFFB060,TvHex=0x7FB8FF,FireHex=0xFF7A22,SodiumHex=0xFFA040,ColdHex=0xCFF2FF,RedHex=0xFF2A20,AmberHex=0xFFB000,BloodHex=0x4A0A08,BloodDryHex=0x2E0806,
            PaperHex=0xC8C0AC,BrassHex=0xC8A050,SandbagHex=0x9A8A64,RustHex=0x6A3418,PaintWhiteHex=0xD8D4C8,PaintYellowHex=0xD8B040,ShutterHex=0x6E7076,
            MeridianHex=0xE4EAEC,MeridianTealHex=0x2E8A8C,CheckpointHex=0x9C968A;

        // ---------------- hashing (stable across machines) ----------------
        private static uint Mix(uint x){unchecked{x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x;}}
        private static uint Fnv(string s){unchecked{uint h=2166136261;if(s!=null)foreach(char c in s)h=(h^c)*16777619;return h;}}
        /// <summary>A stable hash in [0, 1) of a block id and two integers.</summary>
        public static double Hash(string s,int a=0,int c=0){unchecked{return Mix(Fnv(s)^Mix((uint)a*0x9E3779B9U+(uint)c*0x85EBCA6BU))/4294967296.0;}}
        private static bool Starts(string s,string p)=>s.StartsWith(p,StringComparison.Ordinal);
        /// <summary>The id with the town prefix removed, or null for other maps' ids.</summary>
        public static string Stem(string id)=>id!=null&&id.StartsWith(Prefix,StringComparison.Ordinal)?id.Substring(Prefix.Length):null;

        // ---------------- block glyphs (3 x 5 cells; rows top to bottom, bit 2 the left column) ----------------
        private const string GlyphChars="ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-.!'&>/<=";
        private static readonly int[][] GlyphRows={
            new[]{2,5,7,5,5},new[]{6,5,6,5,6},new[]{3,4,4,4,3},new[]{6,5,5,5,6},new[]{7,4,6,4,7},new[]{7,4,6,4,4},new[]{3,4,5,5,3},new[]{5,5,7,5,5},new[]{7,2,2,2,7},new[]{1,1,1,5,2},
            new[]{5,5,6,5,5},new[]{4,4,4,4,7},new[]{5,7,7,5,5},new[]{6,5,5,5,5},new[]{7,5,5,5,7},new[]{6,5,6,4,4},new[]{2,5,5,6,3},new[]{6,5,6,5,5},new[]{3,4,2,1,6},new[]{7,2,2,2,2},
            new[]{5,5,5,5,7},new[]{5,5,5,5,2},new[]{5,5,7,7,5},new[]{5,5,2,5,5},new[]{5,5,2,2,2},new[]{7,1,2,4,7},
            new[]{7,5,5,5,7},new[]{2,6,2,2,7},new[]{6,1,2,4,7},new[]{6,1,2,1,6},new[]{5,5,7,1,1},new[]{7,4,6,1,6},new[]{3,4,6,5,2},new[]{7,1,2,2,2},new[]{2,5,2,5,2},new[]{2,5,3,1,6},
            new[]{0,0,7,0,0},new[]{0,0,0,0,2},new[]{2,2,2,0,2},new[]{2,2,0,0,0},new[]{2,5,2,5,3},new[]{4,2,1,2,4},new[]{1,1,2,4,4},new[]{1,2,4,2,1},new[]{0,7,0,7,0}};
        /// <summary>A glyph's cells as merged rectangles (column, row from the top, width, height in cells): horizontal runs per
        /// row, stacked where consecutive rows repeat a run. Empty for a space or an unknown character.</summary>
        public static List<int[]> GlyphBoxes(char c)
        {
            var o=new List<int[]>();int g=GlyphChars.IndexOf(char.ToUpperInvariant(c));if(g<0)return o;var rows=GlyphRows[g];var open=new List<int[]>();
            for(int r=0;r<=5;r++)
            {
                var runs=new List<int[]>();if(r<5){int bits=rows[r];for(int col=0;col<3;){if((bits>>(2-col)&1)==0){col++;continue;}int s=col;while(col<3&&(bits>>(2-col)&1)!=0)col++;runs.Add(new[]{s,col-s});}}
                var next=new List<int[]>();
                foreach(var b in open){var same=runs.Find(x=>x[0]==b[0]&&x[1]==b[2]);if(same!=null){b[3]++;runs.Remove(same);next.Add(b);}else o.Add(b);}
                foreach(var x in runs)next.Add(new[]{x[0],r,x[1],1});open=next;
            }
            return o;
        }

        // ---------------- build ----------------
        /// <summary>Builds the kit for the town's blocks and its layout area (either may be null: no spawn dressing and no
        /// keep-outs without the area). `layers`: parts flush with a block or an earlier part stand clear of it
        /// (NativeSurfaceLayers.Over; off only for the surface audit's before count).</summary>
        public static NativeExpeditionCityKit Build(ContentBlock[] blocks,ExpeditionAreaLayout area,bool layers=true)=>Build(blocks,area,null,null,layers);
        private NativeExpeditionCityKit Make(bool layers)
        {
            var k=this;
            for(int i=0;i<k.blocks.Length;i++){var s=Stem(k.blocks[i]?.id);if(s!=null&&!k.byStem.ContainsKey(s))k.byStem[s]=i;}
            if(k.byStem.Count==0)return k;
            k.Faces();k.KeepOuts();k.HubKeepOuts();k.FaceKeeps();
            for(int i=0;i<k.blocks.Length;i++){var s=Stem(k.blocks[i]?.id);if(s==null)continue;k.Dress(i,s);}
            k.QuarterSquares();k.HubHeaders();k.Spawns();k.Street();k.WildsGround();k.EntranceLights();k.RouteWires();k.Camp();k.Detail();
            k.Parts=k.parts.ToArray();k.Glows=k.glows.ToArray();k.Keeps=k.keeps.ToArray();k.Lettering=k.lettering.ToArray();k.FaceCulls=k.faceCulls.ToArray();k.parts.Clear();k.glows.Clear();if(layers)Layers(k.Parts,k.blocks);k.BackRatios();return k;
        }
        private static void Layers(NativeCityPart[] parts,ContentBlock[] blocks)
        {
            var boxes=new NativeSurfaceLayers.Box[parts.Length];
            for(int i=0;i<parts.Length;i++)
            {
                var p=parts[i];var b=NativeSurfaceLayers.Parented(blocks[p.Parent],p.X,p.Y,p.Z,p.Hx,p.Hy,p.Hz,p.Gain==1&&p.Drive==DriveSteady?NativeSurfaceLayers.Key(p.Look,p.Color,p.Flags):-1);
                // The part's own turn after its parent's (world = parent · part).
                Mul(b.Qx,b.Qy,b.Qz,b.Qw,p.Qx,p.Qy,p.Qz,p.Qw,out b.Qx,out b.Qy,out b.Qz,out b.Qw);boxes[i]=b;
            }
            var lifts=NativeSurfaceLayers.Over(blocks,boxes,out _);
            for(int i=0;i<parts.Length;i++)
            {
                NativeSurfaceLayers.Apply(lifts,i,out float sx,out float sy,out float sz,out float gx,out float gy,out float gz);if(gx==0&&gy==0&&gz==0)continue;
                // The shift is along the part's own axes: turned by its own rotation into the parent's frame.
                ref var p=ref parts[i];Rotate(p.Qx,p.Qy,p.Qz,p.Qw,sx,sy,sz,out double ox,out double oy,out double oz);p.X+=(float)ox;p.Y+=(float)oy;p.Z+=(float)oz;p.Hx+=gx;p.Hy+=gy;p.Hz+=gz;
            }
        }

        // ---------------- quaternions ----------------
        private static void Mul(double ax,double ay,double az,double aw,double bx,double by,double bz,double bw,out double x,out double y,out double z,out double w)
        {x=aw*bx+ax*bw+ay*bz-az*by;y=aw*by-ax*bz+ay*bw+az*bx;z=aw*bz+ax*by-ay*bx+az*bw;w=aw*bw-ax*bx-ay*by-az*bz;}
        /// <summary>v turned by the unit quaternion (x, y, z, w).</summary>
        public static void Rotate(double qx,double qy,double qz,double qw,double vx,double vy,double vz,out double ox,out double oy,out double oz)
        {double tx=2*(qy*vz-qz*vy),ty=2*(qz*vx-qx*vz),tz=2*(qx*vy-qy*vx);ox=vx+qw*tx+(qy*tz-qz*ty);oy=vy+qw*ty+(qz*tx-qx*tz);oz=vz+qw*tz+(qx*ty-qy*tx);}
        private static void Axis(double ax,double ay,double az,double degrees,out double x,out double y,out double z,out double w)
        {double h=degrees*Math.PI/360,s=Math.Sin(h);x=ax*s;y=ay*s;z=az*s;w=Math.Cos(h);}
        private struct Rot {public double X,Y,Z,W;public static readonly Rot None=new Rot{W=1};}
        private static Rot AxisRot(double ax,double ay,double az,double deg){Axis(ax,ay,az,deg,out double x,out double y,out double z,out double w);return new Rot{X=x,Y=y,Z=z,W=w};}
        private static Rot Then(Rot a,Rot b){Mul(a.X,a.Y,a.Z,a.W,b.X,b.Y,b.Z,b.W,out double x,out double y,out double z,out double w);return new Rot{X=x,Y=y,Z=z,W=w};}
        private static Rot BlockRot(ContentBlock b){var r=b.rotation;double x=r?.x??0,y=r?.y??0,z=r?.z??0,w=r?.w??1,l=Math.Sqrt(x*x+y*y+z*z+w*w);if(!(l>1e-9))return Rot.None;return new Rot{X=x/l,Y=y/l,Z=z/l,W=w/l};}

        // ---------------- the pen ----------------
        private struct Pen {public int Look,Flags,Color;public float Gain,Level,Param;public byte Drive,Role;public bool Shadow,Fade,Detail;}
        private static Pen P(int look,int color,byte role,int flags=0){return new Pen{Look=look,Color=color,Role=role,Flags=flags,Gain=1,Level=1};}
        private static Pen Glow(int color,float gain,byte role,byte drive=DriveSteady,float param=0){return new Pen{Look=NativeCyberCityLooks.Tube,Flags=NativeCyberCityLooks.Bank,Color=color,Role=role,Gain=gain,Level=1,Drive=drive,Param=param};}
        private static Pen With(Pen p,bool shadow=false,bool fade=false,bool detail=false){p.Shadow|=shadow;p.Fade|=fade;p.Detail|=detail;return p;}
        /// <summary>Adds a part given in the world (centre, full size along its own axes, world turn) to `parent`; inside a group
        /// a part that meets a keep-out spoils the whole group.</summary>
        private void Box(int parent,double x,double y,double z,double sx,double sy,double sz,Pen pen,Rot q)
        {
            if(parent<0||!(sx>0&&sy>0&&sz>0))return;var b=blocks[parent];
            if(Blocked(x,y,z,sx*.5,sy*.5,sz*.5,q)){if(groupStart>=0)groupBad=true;else Rejected++;return;}
            var pq=BlockRot(b);Rot inv=new Rot{X=-pq.X,Y=-pq.Y,Z=-pq.Z,W=pq.W};
            Rotate(inv.X,inv.Y,inv.Z,inv.W,x-b.pos.x,y-b.pos.y,z-b.pos.z,out double ox,out double oy,out double oz);var rel=Then(inv,q);
            float back=0;short face=pen.Shadow?(short)0:BackFace(x,y,z,sx*.5,sy*.5,sz*.5,q,out back);
            parts.Add(new NativeCityPart{Parent=parent,Look=pen.Look,Flags=pen.Flags,Color=pen.Color,X=(float)ox,Y=(float)oy,Z=(float)oz,Hx=(float)(sx*.5),Hy=(float)(sy*.5),Hz=(float)(sz*.5),
                Qx=(float)rel.X,Qy=(float)rel.Y,Qz=(float)rel.Z,Qw=(float)rel.W,Gain=pen.Gain,Level=pen.Level,Param=pen.Param,Drive=pen.Drive,Role=pen.Role,Shadow=pen.Shadow,Fade=pen.Fade,Detail=pen.Detail,
                Face=face,Back=back});
        }
        private void Box(int parent,double x,double y,double z,double sx,double sy,double sz,Pen pen)=>Box(parent,x,y,z,sx,sy,sz,pen,Rot.None);
        // ---------------- the back-face rule ----------------
        private Face onFace;private readonly List<NativeCityFaceCull> faceCulls=new List<NativeCityFaceCull>();private readonly Dictionary<int,int> faceCullOf=new Dictionary<int,int>();
        /// <summary>Each face part's `Back` from where it ends up (after the layers' lifts move it out of its face by a few millimetres):
        /// its inset from the solid face's edges over how far it now stands out, rounded down; a part that no longer fits loses its record.</summary>
        private void BackRatios()
        {
            for(int i=0;i<Parts.Length;i++)
            {
                ref var p=ref Parts[i];if(p.Face==0)continue;var c=FaceCulls[p.Face-1];var b=blocks[p.Parent];
                Extent(b.pos.x+p.X,b.pos.y+p.Y,b.pos.z+p.Z,p.Hx,p.Hy,p.Hz,p.Qx,p.Qy,p.Qz,p.Qw,out double ax0,out double ax1,out double ay0,out double ay1,out double az0,out double az1);
                double pu0=c.Axis==0?az0:ax0,pu1=c.Axis==0?az1:ax1,pn0=c.Axis==0?ax0:az0,pn1=c.Axis==0?ax1:az1;
                double outward=c.Sign>0?pn1-c.Plane:c.Plane-pn0,inset=Math.Min(Math.Min(pu0-c.U0,c.U1-pu1),c.Y1-ay1);if(!c.Grounded)inset=Math.Min(inset,ay0-c.Y0);
                if(!(inset>0)||!(outward>1e-4)){p.Face=0;p.Back=0;continue;}
                p.Back=(float)(Math.Floor(inset/outward*1000)/1000);
            }
        }
        /// <summary>1 + the index of the face record that hides a part placed on <see cref="onFace"/> (world centre, half extents, turn)
        /// from an eye behind its building, or 0, and in `back` the part's inset from the solid face's edges (the floor's edge left
        /// out on a grounded building) over how far it stands out of the face: only on an unturned building's own face, for a part
        /// in front of the face and inside its span and height.</summary>
        private short BackFace(double x,double y,double z,double hx,double hy,double hz,Rot q,out float back)
        {
            back=0;var f=onFace;if(f==null||!f.Real)return 0;var b=blocks[f.Block];var s=Stem(b?.id);if(s==null||!IsBuilding(s))return 0;var br=BlockRot(b);if(Math.Abs(br.W)<.99999)return 0;
            const double c=NativeBlockLooks.Chamfer;
            double x0=b.pos.x-b.size.x/2,x1=b.pos.x+b.size.x/2,z0=b.pos.z-b.size.z/2,z1=b.pos.z+b.size.z/2,y0=b.pos.y-b.size.y/2,y1=b.pos.y+b.size.y/2;
            double u0=(f.Axis==0?z0:x0)+c,u1=(f.Axis==0?z1:x1)-c,v0=y0+c,v1=y1-c,plane=f.Axis==0?(f.Sign>0?x1:x0):(f.Sign>0?z1:z0);bool grounded=Math.Abs(y0)<.01;
            Extent(x,y,z,hx,hy,hz,q,out double ax0,out double ax1,out double ay0,out double ay1,out double az0,out double az1);
            double pu0=f.Axis==0?az0:ax0,pu1=f.Axis==0?az1:ax1,pn0=f.Axis==0?ax0:az0,pn1=f.Axis==0?ax1:az1;
            double outward=f.Sign>0?pn1-plane:plane-pn0,inset=Math.Min(Math.Min(pu0-u0,u1-pu1),v1-ay1);if(!grounded)inset=Math.Min(inset,ay0-v0);
            if(!(inset>0)||!(outward>1e-4))return 0;
            int key=f.Block*4+f.Index;
            if(!faceCullOf.TryGetValue(key,out int k))
            {
                k=faceCulls.Count;faceCullOf[key]=k;
                faceCulls.Add(new NativeCityFaceCull{Axis=(byte)f.Axis,Sign=(sbyte)f.Sign,Grounded=grounded,Plane=(float)plane,Depth=(float)(f.Axis==0?b.size.x:b.size.z),U0=(float)u0,U1=(float)u1,Y0=(float)v0,Y1=(float)v1});
            }
            // Rounded down to whole thousandths: the stored ratio never claims more than the geometry gives.
            back=(float)(Math.Floor(inset/outward*1000)/1000);return (short)(k+1);
        }
        private void Begin(){groupStart=parts.Count;glowStart=glows.Count;groupBad=false;}
        private bool End(){bool ok=!groupBad;if(!ok){Rejected++;parts.RemoveRange(groupStart,parts.Count-groupStart);glows.RemoveRange(glowStart,glows.Count-glowStart);}groupStart=glowStart=-1;groupBad=false;return ok;}
        private void Light(double x,double y,double z,int hex,double intensity,double range,double size,byte kind,double param=0)
        {
            glows.Add(new NativeCityGlow{X=(float)x,Y=(float)y,Z=(float)z,R=(float)(NativeSceneryBuilder.Channel(hex,16)*intensity),G=(float)(NativeSceneryBuilder.Channel(hex,8)*intensity),B=(float)(NativeSceneryBuilder.Channel(hex,0)*intensity),Range=(float)range,Size=(float)size,Param=(float)param,Kind=kind});
        }

        // ---------------- keep-outs ----------------
        // What a part may never meet (world AABBs; a spawn's own dressing passes its own keep-outs): spawn exits and their paths,
        // door spawns' doorways, vents, drop columns, slots, returns, the approach, interaction and boss points, the doors' and the
        // gate's volumes, and the sanctuary's floor.
        private void KeepOuts()
        {
            if(area==null)return;
            var spawns=area.spawns??Array.Empty<ExpeditionSpawnLayout>();
            for(short s=0;s<spawns.Length;s++)
            {
                var e=spawns[s]?.entry;if(e?.from==null||e.exit==null)continue;float clear=Math.Max(1.2f,e.clear);
                if(e.kind=="drop"){Keep(e.exit.x-clear,e.exit.x+clear,-1,e.height+2,e.exit.z-clear,e.exit.z+clear,s,1);continue;}
                Keep(e.exit.x-clear,e.exit.x+clear,-1,e.kind=="breach"?4.4f:3.2f,e.exit.z-clear,e.exit.z+clear,s,0);
                if(e.kind=="vent")Keep(e.from.x-.9f,e.from.x+.9f,-1,3.2f,e.from.z-.9f,e.from.z+.9f,s,0);
                // The path from inside the wall to the exit, 2.2 m wide (a door's leaf and frame are the spawn's own).
                float x0=Math.Min(e.from.x,e.exit.x),x1=Math.Max(e.from.x,e.exit.x),z0=Math.Min(e.from.z,e.exit.z),z1=Math.Max(e.from.z,e.exit.z);bool alongX=x1-x0>z1-z0;
                float w=e.kind=="breach"?2.0f:1.1f;Keep(alongX?x0:x0-w,alongX?x1:x1+w,-1,e.kind=="breach"?4.4f:3.0f,alongX?z0-w:z0,alongX?z1+w:z1,s,0);
            }
            void Point(ExpeditionPoint p,float r,float h){if(p!=null)Keep(p.x-r,p.x+r,p.y-1,p.y+h,p.z-r,p.z+r,-1,2);}
            foreach(var p in area.slots??Array.Empty<ExpeditionPoint>())Point(p,.9f,2.4f);
            foreach(var p in area.bossPoints??Array.Empty<ExpeditionPoint>())Point(p,1.6f,4f);
            foreach(var i in area.interactions??Array.Empty<ExpeditionInteractionLayout>())Keep(i.x-1.1f,i.x+1.1f,i.y-1,i.y+2.4f,i.z-1.1f,i.z+1.1f,-1,2);
            foreach(var a in area.anchors??Array.Empty<ExpeditionWorldAnchor>())Keep(a.x-1.1f,a.x+1.1f,a.y-1,a.y+2.4f,a.z-1.1f,a.z+1.1f,-1,2);
            foreach(var d in area.doors??Array.Empty<ExpeditionDoorLayout>())
            {
                Volume(d.interact,.4f);Volume(d.closure,.6f);Point(d.approach,.9f,2.4f);
                // The doorway's airlock unit (claude/expedition-states: pockets 2 m either side of the leaves, header to 4 m, a sill
                // 0.6 m out toward the party).
                if(d.closure!=null){var unit=Unit(d.closure);Keep(unit[0],unit[1],-1,unit[3],unit[4],unit[5],-1,2);}
                foreach(var p in d.returns??Array.Empty<ExpeditionPoint>())Point(p,.9f,2.4f);foreach(var p in d.entries??Array.Empty<ExpeditionPoint>())Point(p,.9f,2.4f);
                // The join circle (the city detail pass): nothing, flat paint included, in its pad grown .3 m, up to its counted height.
                if(d.join!=null){float r=d.join.r+.3f;Keep(d.join.x-r,d.join.x+r,d.join.y-1,d.join.y+d.join.h,d.join.z-r,d.join.z+r,-1,2);}
            }
            foreach(var g in area.gates??Array.Empty<ExpeditionGateLayout>())Volume(g.closure,.8f,.6f);
            // The sanctuary's floor: nothing but flat paint below head height, away from its barricades and brazier (checked in Blocked).
            foreach(var v in area.sanctuary??Array.Empty<ExpeditionVolume>()){var b=Aabb(v,0);keeps.Add(new NativeCityKeep{X0=b[0]+.9f,X1=b[1]-.9f,Y0=b[2]+Flat+.001f,Y1=b[2]+2.6f,Z0=b[4]+.9f,Z1=b[5]-.9f,Spawn=-1,Kind=3});}
        }
        /// <summary>The space a door's airlock unit takes round its closure volume (world AABB, as Aabb returns it): the leaves'
        /// pockets and lamps <see cref="UnitWing"/> m either side, the header to <see cref="UnitTop"/> m, <see cref="UnitDepth"/> m
        /// either side of the doorway plane.</summary>
        public static float[] Unit(ExpeditionVolume v)=>Aabb(new ExpeditionVolume{x=v.x,y=UnitTop/2,z=v.z,sx=v.sx+2*UnitWing,sy=UnitTop,sz=2*UnitDepth,yaw=v.yaw},0);
        public const float UnitWing=2.0f,UnitTop=4.2f,UnitDepth=.85f;
        private void Volume(ExpeditionVolume v,float pad,float top=0){if(v==null)return;var b=Aabb(v,pad);Keep(b[0],b[1],b[2]-.2f,b[3]+top,b[4],b[5],-1,2);}
        public static float[] Aabb(ExpeditionVolume v,float pad)
        {
            double a=v.yaw*Math.PI/180,c=Math.Abs(Math.Cos(a)),s=Math.Abs(Math.Sin(a));float hx=(float)(c*v.sx/2+s*v.sz/2)+pad,hz=(float)(s*v.sx/2+c*v.sz/2)+pad;
            return new[]{v.x-hx,v.x+hx,v.y-v.sy/2,v.y+v.sy/2+pad,v.z-hz,v.z+hz};
        }
        private void Keep(float x0,float x1,float y0,float y1,float z0,float z1,short spawn,byte kind)=>keeps.Add(new NativeCityKeep{X0=x0,X1=x1,Y0=y0,Y1=y1,Z0=z0,Z1=z1,Spawn=spawn,Kind=kind});
        private bool Blocked(double x,double y,double z,double hx,double hy,double hz,Rot q)
        {
            Extent(x,y,z,hx,hy,hz,q,out double x0,out double x1,out double y0,out double y1,out double z0,out double z1);
            foreach(var k in keeps)
            {
                if(k.Spawn>=0&&k.Spawn==owner||k.Kind==3&&sanctuaryOk)continue;
                if(x1<=k.X0||x0>=k.X1||y1<=k.Y0||y0>=k.Y1||z1<=k.Z0||z0>=k.Z1)continue;
                return true;
            }
            return false;
        }
        /// <summary>A turned box's world AABB.</summary>
        public static void Extent(double x,double y,double z,double hx,double hy,double hz,double qx,double qy,double qz,double qw,out double x0,out double x1,out double y0,out double y1,out double z0,out double z1)
        {
            x0=y0=z0=double.MaxValue;x1=y1=z1=double.MinValue;
            for(int k=0;k<8;k++){Rotate(qx,qy,qz,qw,(k&1)==0?-hx:hx,(k&2)==0?-hy:hy,(k&4)==0?-hz:hz,out double ox,out double oy,out double oz);x0=Math.Min(x0,x+ox);x1=Math.Max(x1,x+ox);y0=Math.Min(y0,y+oy);y1=Math.Max(y1,y+oy);z0=Math.Min(z0,z+oz);z1=Math.Max(z1,z+oz);}
        }
        private static void Extent(double x,double y,double z,double hx,double hy,double hz,Rot q,out double x0,out double x1,out double y0,out double y1,out double z0,out double z1)=>Extent(x,y,z,hx,hy,hz,q.X,q.Y,q.Z,q.W,out x0,out x1,out y0,out y1,out z0,out z1);

        // ---------------- faces ----------------
        /// <summary>A building's side face: its block, outward normal (axis 0 x or 2 z, sign), the plane, the span along the viewer's
        /// right (`t` = (-n.z, 0, n.x)), the block's height and what the street in front is (0 enclosed, 1 alley, 2 street).</summary>
        private sealed class Face {public int Block,Axis,Sign,Index;public bool Real;public double Plane,U0,U1,Top,Base;public int Front;public readonly List<double[]> Occ=new List<double[]>();public readonly List<double[]> Keep=new List<double[]>();public readonly List<double[]> Used=new List<double[]>();}
        private readonly List<Face> faces=new List<Face>();private readonly Dictionary<int,List<Face>> facesOf=new Dictionary<int,List<Face>>();
        private static double Tx(Face f)=>f.Axis==2?-f.Sign:0;private static double Tz(Face f)=>f.Axis==0?f.Sign:0;
        private static double Nx(Face f)=>f.Axis==0?f.Sign:0;private static double Nz(Face f)=>f.Axis==2?f.Sign:0;
        /// <summary>World point of face coordinates: u along the face (viewer's right), y up, d out of the plane.</summary>
        private static void At(Face f,double u,double y,double d,out double x,out double z){if(f.Axis==0){x=f.Plane+f.Sign*d;z=Tz(f)*u;}else{z=f.Plane+f.Sign*d;x=Tx(f)*u;}}
        private static double U(Face f,double x,double z)=>Tx(f)*x+Tz(f)*z;
        private bool IsBuilding(string s)=>Starts(s,"bldg-");
        private void Faces()
        {
            foreach(var kv in byStem)
            {
                if(!IsBuilding(kv.Key))continue;var list=new List<Face>();facesOf[kv.Value]=list;
                for(int k=0;k<4;k++){var f=MakeFace(kv.Value,k<2?0:2,k%2==0?1:-1);if(f==null)continue;faces.Add(f);list.Add(f);}
            }
        }
        /// <summary>Face (axis, sign) of an unturned block, its occluders and street read; null when it is never seen.</summary>
        private Face MakeFace(int block,int axis,int sign)
        {
            var b=blocks[block];double x0=b.pos.x-b.size.x/2,x1=b.pos.x+b.size.x/2,z0=b.pos.z-b.size.z/2,z1=b.pos.z+b.size.z/2,y0=b.pos.y-b.size.y/2,y1=b.pos.y+b.size.y/2;
            var f=new Face{Block=block,Axis=axis,Sign=sign,Index=(axis==0?0:2)+(sign>0?0:1),Real=true,Base=y0,Top=y1};f.Plane=f.Axis==0?(f.Sign>0?x1:x0):(f.Sign>0?z1:z0);
            // A face beyond the bounds that looks out of the town is never seen.
            if(f.Axis==0?(f.Sign>0?f.Plane>=bx1-.1:f.Plane<=bx0+.1):(f.Sign>0?f.Plane>=bz1-.1:f.Plane<=bz0+.1))return null;
            double ua=f.Axis==0?U(f,0,z0):U(f,x0,0),ub=f.Axis==0?U(f,0,z1):U(f,x1,0);f.U0=Math.Min(ua,ub);f.U1=Math.Max(ua,ub);
            // The town ends at its bounds: the faces' useful span stops there.
            if(f.Axis==0){double a=U(f,0,bz0),c=U(f,0,bz1);f.U0=Math.Max(f.U0,Math.Min(a,c));f.U1=Math.Min(f.U1,Math.Max(a,c));}else{double a=U(f,bx0,0),c=U(f,bx1,0);f.U0=Math.Max(f.U0,Math.Min(a,c));f.U1=Math.Min(f.U1,Math.Max(a,c));}
            if(f.U1-f.U0<.3)return null;Occluders(f);f.Front=Front(f);return f;
        }
        // Other blocks against the face (within 0.35 m of its plane: neighbours, breach panels, shutters) hide it; low blocks within
        // 1.6 m (dumpsters, crates, barriers, wrecks) hide the wall behind them up to their height.
        private void Occluders(Face f)
        {
            for(int i=0;i<blocks.Length;i++)
            {
                var b=blocks[i];var s=Stem(b?.id);if(i==f.Block||s==null||Starts(s,"slab-")||Starts(s,"sanctuary-paving")||Starts(s,"square-paving"))continue;
                Extent(b.pos.x,b.pos.y,b.pos.z,b.size.x/2,b.size.y/2,b.size.z/2,BlockRot(b),out double x0,out double x1,out double y0,out double y1,out double z0,out double z1);
                double n0=f.Axis==0?x0:z0,n1=f.Axis==0?x1:z1;double reach=y1-y0<2.6&&!IsBuilding(s)?1.6:.35;
                bool touches=f.Sign>0?n1>f.Plane+.01&&n0<f.Plane+reach:n0<f.Plane-.01&&n1>f.Plane-reach;if(!touches)continue;
                double ua,ub;if(f.Axis==0){ua=U(f,0,z0);ub=U(f,0,z1);}else{ua=U(f,x0,0);ub=U(f,x1,0);}
                double u0=Math.Max(f.U0,Math.Min(ua,ub)),u1=Math.Min(f.U1,Math.Max(ua,ub));if(u1<=u0)continue;f.Occ.Add(new[]{u0,u1,y0,y1});
            }
        }
        // The street in front: the clear run out of the face at 1 m and at 6 m height, sampled along it (2 street, 1 alley, 0 shut).
        private int Front(Face f)
        {
            int n=0;double sum=0;
            for(double u=f.U0+1;u<f.U1-.5;u+=2){double best=12;At(f,u,1,0,out double px,out double pz);
                for(int i=0;i<blocks.Length;i++)
                {
                    var b=blocks[i];var s=Stem(b?.id);if(i==f.Block||s==null||!IsBuilding(s)&&!Starts(s,"gate-"))continue;
                    double x0=b.pos.x-b.size.x/2,x1=b.pos.x+b.size.x/2,z0=b.pos.z-b.size.z/2,z1=b.pos.z+b.size.z/2;
                    if(f.Axis==0){if(pz<z0||pz>z1)continue;double d=f.Sign>0?x0-f.Plane:f.Plane-x1;if(d>-.01&&d<best)best=d;}else{if(px<x0||px>x1)continue;double d=f.Sign>0?z0-f.Plane:f.Plane-z1;if(d>-.01&&d<best)best=d;}
                }
                sum+=best;n++;}
            double mean=n>0?sum/n:12;return mean>=7?2:mean>=2.2?1:0;
        }
        private static bool Overlaps(double[] r,double u0,double u1,double y0,double y1,double pad)=>u0<r[1]+pad&&u1>r[0]-pad&&y0<r[3]+pad&&y1>r[2]-pad;
        /// <summary>Whether the face rectangle is free of occluders, of the face's keep-out rectangles (doorways) and of dressing already on it.</summary>
        private bool Open(Face f,double u0,double u1,double y0,double y1,double pad=.12,bool used=true)
        {
            if(u0<f.U0+.05||u1>f.U1-.05||y1>f.Top-f.Base+.001)return false;
            foreach(var r in f.Occ)if(Overlaps(r,u0,u1,y0,y1,pad))return false;foreach(var r in f.Keep)if(Overlaps(r,u0,u1,y0,y1,pad))return false;
            if(used)foreach(var r in f.Used)if(Overlaps(r,u0,u1,y0,y1,0))return false;return true;
        }
        /// <summary>Marks a face rectangle taken by dressing (windows, doors, shopfronts, lamps): later wall pieces keep off it.</summary>
        private static void Use(Face f,double u0,double u1,double y0,double y1)=>f.Used.Add(new[]{u0,u1,y0,y1});
        /// <summary>A box on face f: centre (u, y, d out of the plane), size w along u, h up, depth along the normal; `roll` turns it
        /// in the wall's plane (+ clockwise as the viewer sees the face), `pitch` about the face's tangent (+ tips the outer edge down).</summary>
        private void FBox(Face f,double u,double y,double d,double w,double h,double depth,Pen pen,double roll=0,double pitch=0)
        {
            At(f,u,y,d,out double x,out double z);double sx=f.Axis==0?depth:w,sz=f.Axis==0?w:depth;var q=Rot.None;
            if(roll!=0)q=AxisRot(Nx(f),0,Nz(f),roll);if(pitch!=0)q=Then(AxisRot(Tx(f),0,Tz(f),-pitch),q);
            onFace=f;Box(f.Block,x,f.Base+y,z,sx,h,sz,pen,q);onFace=null;
        }
        /// <summary>Block letters on face f: text centred at u, glyph cells `cell` m, the baseline at y, `d` out of the plane, cell depth `depth`.</summary>
        private void Text(Face f,string text,double u,double y,double d,double cell,double depth,Pen pen,double roll=0,int dead=-1,Pen deadPen=default)
        {
            double width=text.Length*4*cell-cell,u0=u-width/2;int before=parts.Count;
            for(int i=0;i<text.Length;i++)
            {
                var p=i==dead?deadPen:pen;double lu=u0+i*4*cell;
                foreach(var g in GlyphBoxes(text[i])){double cu=lu+(g[0]+g[2]*.5)*cell,cy=y+(5-g[1]-g[3]*.5)*cell;
                    if(roll!=0){double a=roll*Math.PI/180,du=cu-u,dy=cy-(y+2.5*cell);cu=u+du*Math.Cos(a)+dy*Math.Sin(a);cy=y+2.5*cell-du*Math.Sin(a)+dy*Math.Cos(a);}
                    FBox(f,cu,cy,d,g[2]*cell,g[3]*cell,depth,p,roll);}
            }
            if(parts.Count>before)lettering.Add(text);
        }
        public static double TextWidth(string text,double cell)=>text.Length*4*cell-cell;

        // ---------------- dispatch ----------------
        private void Dress(int i,string s)
        {
            var b=blocks[i];
            if(Starts(s,"bldg-"))Building(i,s.Substring(5));
            else if(Starts(s,"roof-"))RoofProp(i,s);
            else if(Starts(s,"gate-"))Gate(i,s);
            else if(Starts(s,"wz-portico"))Portico(i,s);
            else if(s=="foundry-shutter")FoundryShutter(i);
            else if(Starts(s,"transit-"))TransitPiece(i,s);
            else if(Starts(s,"sanctuary-barricade-"))Barricade(i,s);
            else if(Starts(s,"sanctuary-fence-"))Fence(i,s);
            else if(Starts(s,"wild-"))WildPiece(i,s);
            else if(s=="sanctuary-brazier")Brazier(i);
            else if(Starts(s,"square-"))Square(i,s);
            else if(Starts(s,"car-")||Starts(s,"taxi-")||Starts(s,"ambulance-")||Starts(s,"bus-")||Starts(s,"truck-"))Vehicle(i,s);
            else if(Starts(s,"barrier-"))Barrier(i);
            else if(Starts(s,"dumpster-"))Dumpster(i);
            else if(Starts(s,"crate-"))Crate(i);
            else if(Starts(s,"breach-"))Breach(i,s);
        }
    }
    /// <summary>A world AABB the town kit keeps clear; `Spawn` is the spawn (index in the area's spawns) whose own dressing may
    /// enter it (-1 none); `Kind` 0 a spawn's exit and path, 1 a drop column, 2 a record (slot, return, approach, interaction,
    /// anchor, boss point, door or gate volume), 3 the sanctuary floor (flat paint allowed).</summary>
    public struct NativeCityKeep {public float X0,X1,Y0,Y1,Z0,Z1;public short Spawn;public byte Kind;}
}
