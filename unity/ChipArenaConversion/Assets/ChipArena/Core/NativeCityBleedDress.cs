using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The approach dressing's plan (docs/MERIDIAN-BLEED.md): what the bleed puts on each door's street, from the band's outer
    /// edge to the door front's line (<see cref="FrontLine"/> m out, where D2's door front begins), built from the map's blocks
    /// and each door's route records only, so the live run can call it as the Developer walk does.
    ///
    /// Every piece is a kind of the bank's (<see cref="Kinds"/>, `rig/bleed/&lt;key&gt;/&lt;tier&gt;` in ChipArenaCityBleed) placed by
    /// one of four mounts, and each mount keeps the city's placement rules:
    ///  - Floor: paint on a floor's top, <see cref="Flat"/> m or less, every corner on that top (a 2 m world grid, so no two
    ///    tiles overlap);
    ///  - Wall: on a collider's face found from the street (the face continuous and in sight across the whole bay), flat paint of
    ///    <see cref="Flat"/> m or less below <see cref="Clear"/> m over the floor, anything above it;
    ///  - Span: cable-borne pieces across the street between two facing walls, wholly above <see cref="Clear"/> m;
    ///  - Overhead: the hung cells of Level 0's ceiling, wholly above <see cref="Clear"/> m.
    /// Nothing comes within <see cref="JoinKeep"/> m of a door's join circle or inside a door front. Positions follow the blocks
    /// and the routes (fixed per layout); the run seed picks variants, patches and which lamps are dead or flicker (1 in 5 and
    /// 1 in 10, the owner's lamp rule). Presentation only: no collider, rule or record.
    /// </summary>
    public static class NativeCityBleedDress
    {
        public const string Version="meridian-bleed-dress-1";
        /// <summary>The bank file (Resources) that draws the plan's kinds.</summary>
        public const string Bank="ChipArenaCityBleed";
        public enum Mount:byte{Floor,Wall,Span,Overhead}
        /// <summary>The rules: flat paint (m), the clear height over a floor (m), the reach of art onto a collider (m), the join
        /// circle's keep-out (m), the door front's depth (m), the join circle's distance out from its door (the walk's load fixture).</summary>
        public const float Flat=.06f,Clear=3.1f,Reach=.25f,JoinKeep=3.3f,FrontLine=12f,JoinOut=8f;
        /// <summary>The module sizes: a wall bay, a floor tile, the ground storey's skin and an upper storey's.</summary>
        public const float Bay=2.5f,Tile=2f,Storey=3.1f,Upper=3f;
        /// <summary>Route metres between lamps (the owner's "about one lamp every 12 m"), spans, rumour signs and details.</summary>
        public const float LampEvery=12,SpanEvery=24,SignEvery=36,DetailEvery=10;
        /// <summary>The outer edge of the dressing, in route metres (the rumour band ends at 160 m).</summary>
        public const float Outer=165;

        /// <summary>A piece's kind: its bank key, mount, envelope at scale 1 in its own frame (Floor: centred on the floor's top,
        /// z along the street; Wall: the bay's foot on the face, +z out of the wall, -x the viewer's right; Span: from the cable's
        /// start, x across the street; Overhead: the cell's centre on its underside), whether it casts, and a lamp's light and head.</summary>
        public sealed class Kind
        {
            public readonly string Key;public readonly Mount Mount;public readonly V3 Min,Max,Head;public readonly bool Casts;public readonly uint Light;
            public bool Lamp=>Light!=0;
            public Kind(string key,Mount mount,V3 min,V3 max,bool casts=false,uint light=0,V3 head=default)
            {Key=key;Mount=mount;Min=min;Max=max;Casts=casts;Light=light;Head=head;}
        }
        private static V3 V(float x,float y,float z)=>new V3(x,y,z);
        public const uint Sodium=0xFFB45A,Office=0xF4F2C8,Siren=0xFF2424,SignalRed=0xFF3020,SignalGreen=0x40FF70,Work=0xFF8A2A,HighBay=0xFFB060,Pool=0x8FF4FF;
        public static readonly Kind[] Kinds=
        {
            // floors
            new Kind("level0.carpet",Mount.Floor,V(-1,0,-1),V(1,.04f,1)),
            new Kind("poolrooms.tile",Mount.Floor,V(-1,0,-1),V(1,.03f,1)),
            new Kind("poolrooms.flood",Mount.Floor,V(-1,0,-1),V(1,.045f,1)),
            new Kind("poolrooms.drain",Mount.Floor,V(-.6f,0,-.3f),V(.6f,.03f,.3f)),
            new Kind("poolrooms.ring",Mount.Floor,V(-.45f,0,-.45f),V(.45f,.055f,.45f)),
            new Kind("wardzero.litter",Mount.Floor,V(-1,0,-1),V(1,.02f,1)),
            new Kind("wardzero.decon",Mount.Floor,V(-1,0,-1),V(1,.03f,1)),
            new Kind("station.rails",Mount.Floor,V(-.9f,0,-1),V(.9f,.03f,1)),
            new Kind("station.platform",Mount.Floor,V(-1,0,-1),V(1,.03f,1)),
            new Kind("ironworks.ash",Mount.Floor,V(-1,0,-1),V(1,.03f,1)),
            new Kind("ironworks.slag",Mount.Floor,V(-.35f,0,-1),V(.35f,.04f,1)),
            new Kind("ironworks.plates",Mount.Floor,V(-1,0,-1),V(1,.04f,1)),
            // ground storey skins (threshold) and warp skins: flat
            new Kind("level0.wallpaper",Mount.Wall,V(-1.25f,0,0),V(1.25f,Storey,.05f)),
            new Kind("level0.threshold",Mount.Wall,V(-1.25f,0,0),V(1.25f,Storey,.05f)),
            new Kind("poolrooms.threshold",Mount.Wall,V(-1.25f,0,0),V(1.25f,Storey,.05f)),
            new Kind("wardzero.growth",Mount.Wall,V(-1.25f,0,0),V(1.25f,Storey,.05f)),
            new Kind("wardzero.cladding",Mount.Wall,V(-1.25f,0,0),V(1.25f,Storey,.05f)),
            new Kind("station.brick",Mount.Wall,V(-1.25f,0,0),V(1.25f,Storey,.05f)),
            new Kind("station.threshold",Mount.Wall,V(-1.25f,0,0),V(1.25f,Storey,.05f)),
            new Kind("ironworks.plate",Mount.Wall,V(-1.25f,0,0),V(1.25f,Storey,.05f)),
            new Kind("ironworks.furnace",Mount.Wall,V(-1.25f,0,0),V(1.25f,Storey,.05f)),
            // upper storey skins (relief allowed: they start at the clear height)
            new Kind("level0.threshold.upper",Mount.Wall,V(-1.25f,0,0),V(1.25f,Upper,.12f)),
            new Kind("poolrooms.threshold.upper",Mount.Wall,V(-1.25f,0,0),V(1.25f,Upper,.12f)),
            new Kind("wardzero.cladding.upper",Mount.Wall,V(-1.25f,0,0),V(1.25f,Upper,.15f)),
            new Kind("wardzero.sheet",Mount.Wall,V(-1.25f,0,0),V(1.25f,Upper,.35f),true),
            new Kind("station.threshold.upper",Mount.Wall,V(-1.25f,0,0),V(1.25f,Upper,.15f)),
            new Kind("ironworks.furnace.upper",Mount.Wall,V(-1.25f,0,0),V(1.25f,Upper,.2f)),
            // flat details on the ground storey
            new Kind("level0.exit",Mount.Wall,V(-.6f,0,0),V(.6f,2.75f,.05f)),
            new Kind("level0.missing",Mount.Wall,V(-.8f,.9f,0),V(.8f,2.3f,.05f)),
            new Kind("poolrooms.plate",Mount.Wall,V(-.3f,1.5f,0),V(.3f,1.9f,.03f)),
            new Kind("poolrooms.glassblock",Mount.Wall,V(-.8f,.8f,0),V(.8f,2.4f,.04f)),
            new Kind("poolrooms.doorway",Mount.Wall,V(-.75f,0,0),V(.75f,2.5f,.05f)),
            new Kind("wardzero.poster",Mount.Wall,V(-.4f,1.1f,0),V(.4f,2.2f,.02f)),
            new Kind("wardzero.evac.l",Mount.Wall,V(-.65f,2.1f,0),V(.65f,2.6f,.03f)),
            new Kind("wardzero.evac.r",Mount.Wall,V(-.65f,2.1f,0),V(.65f,2.6f,.03f)),
            new Kind("station.roundel",Mount.Wall,V(-.5f,1.7f,0),V(.5f,2.7f,.04f)),
            new Kind("ironworks.rollerdoor",Mount.Wall,V(-1.2f,0,0),V(1.2f,2.9f,.05f)),
            new Kind("ironworks.sign",Mount.Wall,V(-.35f,1.5f,0),V(.35f,2f,.02f)),
            // lamps and fittings above the clear height (origin: the wall plate at its height)
            new Kind("common.lamp",Mount.Wall,V(-.2f,-.45f,0),V(.2f,.35f,1.3f),true,Sodium,V(0,-.3f,1.15f)),
            new Kind("level0.lamp",Mount.Wall,V(-.7f,-.35f,0),V(.7f,.3f,1),true,Office,V(0,-.3f,.8f)),
            new Kind("poolrooms.poollamp",Mount.Wall,V(-.25f,.45f,0),V(.25f,.95f,.04f),false,Pool,V(0,.7f,.05f)),
            new Kind("wardzero.lamp",Mount.Wall,V(-.25f,-.2f,0),V(.25f,.55f,1.1f),true,Siren,V(0,.2f,.95f)),
            new Kind("station.lamp",Mount.Wall,V(-.25f,-.6f,0),V(.25f,.5f,.7f),true,SignalRed,V(0,0,.55f)),
            new Kind("ironworks.worklamp",Mount.Wall,V(-.3f,-.5f,0),V(.3f,.3f,.9f),true,Work,V(0,-.3f,.75f)),
            new Kind("ironworks.highbay",Mount.Wall,V(-.45f,-.6f,0),V(.45f,.35f,1.3f),true,HighBay,V(0,-.5f,1.1f)),
            new Kind("wardzero.horn",Mount.Wall,V(-.35f,-.35f,0),V(.35f,.4f,.8f),true),
            new Kind("station.clock",Mount.Wall,V(-.45f,-.5f,0),V(.45f,.5f,.9f),true),
            new Kind("station.board",Mount.Wall,V(-1.2f,-.4f,0),V(1.2f,.4f,.7f),true),
            new Kind("common.anchor",Mount.Wall,V(-.15f,-.15f,0),V(.15f,.15f,.12f)),
            // spans: x across the street from the cable's start (a 1 m cable, or 2 m segments), scaled to fit
            new Kind("common.wire",Mount.Span,V(0,-.02f,-.02f),V(1,.02f,.02f)),
            new Kind("poolrooms.lanerope",Mount.Span,V(0,-.12f,-.12f),V(2,.12f,.12f)),
            new Kind("poolrooms.pennants",Mount.Span,V(0,-.45f,-.05f),V(2,.05f,.05f)),
            new Kind("wardzero.tent",Mount.Span,V(0,-1.3f,-1.5f),V(2,.5f,1.5f),true),
            new Kind("ironworks.pipes",Mount.Span,V(0,-.9f,-1.1f),V(2,.6f,1.1f),true),
            new Kind("ironworks.girder",Mount.Span,V(0,-1,-.5f),V(2,.3f,.5f),true),
            // hung from a span (origin: the hanging point)
            new Kind("level0.troffer",Mount.Overhead,V(-.65f,-.35f,-.2f),V(.65f,0,.2f),true,Office,V(0,-.33f,0)),
            new Kind("station.dropper",Mount.Overhead,V(-.15f,-.5f,-.6f),V(.15f,0,.6f)),
            new Kind("ironworks.hook",Mount.Overhead,V(-.6f,-2.2f,-.6f),V(.6f,0,.6f),true),
            // Level 0's office ceiling (2 m cells, from the underside up)
            new Kind("level0.ceiling.tile",Mount.Overhead,V(-1,-.35f,-1),V(1,1.8f,1),true),
            new Kind("level0.ceiling.gap",Mount.Overhead,V(-1,-.35f,-1),V(1,1.8f,1),true),
            new Kind("level0.ceiling.light",Mount.Overhead,V(-1,-.35f,-1),V(1,1.8f,1),true,Office,V(0,0,0)),
        };
        private static readonly Dictionary<string,int> kindIndex=Index();
        private static Dictionary<string,int> Index(){var d=new Dictionary<string,int>(StringComparer.Ordinal);for(int i=0;i<Kinds.Length;i++)d.Add(Kinds[i].Key,i);return d;}
        public static int KindOf(string key)=>key!=null&&kindIndex.TryGetValue(key,out int i)?i:-1;

        /// <summary>A lamp's state (the owner's rule: about 1 in 5 dead, 1 in 10 flickering).</summary>
        public enum LampState:byte{None,Steady,Flicker,Dead,Turning}
        /// <summary>A placed piece: its kind, place (feet or mount point), turn about +y (radians: its +z faces
        /// (sin, 0, cos)), scale, door, band, lamp state and colour, and its own seed (flicker phase, the glow's variation).</summary>
        public struct Piece
        {
            public int Kind;public V3 At;public float Yaw;public V3 Scale;public byte Door;public NativeCityBleed.Bands Band;public LampState Lamp;public uint Light;public uint Seed;
        }
        /// <summary>A street's route records, parallel to the bleed's doors: each segment's width and kind ("flat", "stair", "ramp").</summary>
        public sealed class Street{public float[] Widths;public string[] Kinds;}
        public sealed class Plan
        {
            public readonly List<Piece> Pieces=new List<Piece>();
            /// <summary>Each door's join circle centre (the keep-out's), and its door front (D2's: the hall's frontage to
            /// FrontLine m out).</summary>
            public readonly List<V3> Joins=new List<V3>();
            public readonly List<Front> Fronts=new List<Front>();
            public int WallSlots,FloorCells;
        }
        /// <summary>A door front: the door, its outward way, and the frontage's half width either side of the door.</summary>
        public struct Front{public V3 Door,Out;public float Left,Right;}

        /// <summary>The world matrix's parts of a piece: its kind's frame turned by Yaw, scaled, at At.</summary>
        public static V3 Local(in Piece p,V3 local)
        {
            float c=(float)Math.Cos(p.Yaw),s=(float)Math.Sin(p.Yaw);float x=local.x*p.Scale.x,y=local.y*p.Scale.y,z=local.z*p.Scale.z;
            return new V3(p.At.x+x*c+z*s,p.At.y+y,p.At.z-x*s+z*c);
        }
        /// <summary>The yaw whose +z faces a horizontal direction.</summary>
        public static float YawOf(V3 dir)=>(float)Math.Atan2(dir.x,dir.z);

        // ---- The plan ---------------------------------------------------------------------------------------------------------
        private sealed class World
        {
            public ContentBlock[] Blocks;public bool[] Wall;public HashSet<string> Treads;
        }
        // A wall bay beside the street: its foot on the face at the street's height (on a slope: its middle's), the face's way
        // out and along, the wall's top over the street (on a slope: over the bay's high end), and how far the slope rises over
        // half a bay (0 on the flat: only flat bays take skins and floor-anchored details).
        private struct Slot{public V3 Foot,Normal,Along;public float Top,Metres,Share,Rise;public int Block,Side;public NativeCityBleed.Bands Band;public bool Sloped=>Rise>.005f;}

        /// <summary>The approach dressing for every instance door (the final court, slot 0, is M7's and left plain).</summary>
        public static Plan Build(ContentMap map,NativeCityBleed bleed,IReadOnlyList<Street> streets,uint seed)
        {
            if(map?.blocks==null||bleed==null||streets==null||streets.Count!=bleed.Doors.Length)throw new ArgumentException("A map, its bleed and one street per door.");
            var plan=new Plan();var w=WorldOf(map);
            for(int d=0;d<bleed.Doors.Length;d++)
            {
                var door=bleed.Doors[d];plan.Joins.Add(door.At+door.Out*JoinOut);plan.Fronts.Add(FrontOf(w,door));
            }
            for(int d=0;d<bleed.Doors.Length;d++)
            {
                string theme=NativeCityBleed.Theme(bleed.Doors[d].Instance);if(theme==null||theme=="final")continue;
                Dress(plan,w,bleed,d,streets[d],theme,seed);
            }
            fitWorld=null;fitNear=null;
            return plan;
        }

        private static World WorldOf(ContentMap map)
        {
            var w=new World{Blocks=map.blocks,Wall=new bool[map.blocks.Length],Treads=new HashSet<string>(StringComparer.Ordinal)};
            if(map.ramps!=null)foreach(var r in map.ramps)if(r?.blocks!=null)foreach(var b in r.blocks)w.Treads.Add(b);
            for(int i=0;i<map.blocks.Length;i++)
            {
                var b=map.blocks[i];string id=b.id??"";
                w.Wall[i]=!w.Treads.Contains(id)&&b.size.y>=Storey+.1f&&!Starts(id,"hall-","city-hall","court-","cordon","camp-fence","fire-escape","landmark-","viaduct-parapet","street-rail");
            }
            return w;
        }
        private static bool Starts(string id,params string[] stems){foreach(var s in stems)if(id.StartsWith(s,StringComparison.Ordinal))return true;return false;}

        // The door's hall (the hall-* blocks nearest the door) gives its frontage across the door's outward way.
        private static Front FrontOf(World w,NativeCityBleed.Door door)
        {
            var f=new Front{Door=door.At,Out=door.Out,Left=6,Right=6};var right=new V3(door.Out.z,0,-door.Out.x);
            string stem=null;float near=40;
            foreach(var b in w.Blocks){if(b.id==null||!b.id.StartsWith("hall-",StringComparison.Ordinal))continue;float dd=FlatDistance(b,door.At);if(dd<near){near=dd;int dash=b.id.IndexOf('-',5);stem=dash<0?b.id:b.id.Substring(0,dash);}}
            if(stem==null)return f;
            foreach(var b in w.Blocks)
            {
                if(b.id==null||!(b.id==stem||b.id.StartsWith(stem+"-",StringComparison.Ordinal)))continue;
                foreach(float sx in new[]{-.5f,.5f})foreach(float sz in new[]{-.5f,.5f})
                {
                    var c=new V3(b.pos.x+sx*b.size.x-door.At.x,0,b.pos.z+sz*b.size.z-door.At.z);float a=V3.Dot(c,right);
                    if(a>f.Right)f.Right=a;if(-a>f.Left)f.Left=-a;
                }
            }
            f.Left+=2;f.Right+=2;return f;
        }
        private static float FlatDistance(ContentBlock b,V3 p)
        {
            float dx=Math.Max(0,Math.Abs(p.x-b.pos.x)-b.size.x/2),dz=Math.Max(0,Math.Abs(p.z-b.pos.z)-b.size.z/2);return (float)Math.Sqrt(dx*dx+dz*dz);
        }

        /// <summary>Whether a place (with a horizontal radius) keeps clear of every join circle and door front.</summary>
        public static bool Clear_(Plan plan,V3 p,float radius)
        {
            foreach(var j in plan.Joins){float dx=p.x-j.x,dz=p.z-j.z;if(dx*dx+dz*dz<(JoinKeep+radius)*(JoinKeep+radius))return false;}
            foreach(var f in plan.Fronts)
            {
                var c=new V3(p.x-f.Door.x,0,p.z-f.Door.z);float depth=V3.Dot(c,f.Out);var right=new V3(f.Out.z,0,-f.Out.x);float a=V3.Dot(c,right);
                if(depth<FrontLine+radius&&a<f.Right+radius&&-a<f.Left+radius&&depth>-40)return false;
                if(c.x*c.x+c.z*c.z<(FrontLine+radius)*(FrontLine+radius))return false;
            }
            return true;
        }

        // ---- One door's street -------------------------------------------------------------------------------------------------
        private static void Dress(Plan plan,World w,NativeCityBleed bleed,int d,Street street,string theme,uint seed)
        {
            var door=bleed.Doors[d];var pts=door.Route;var rest=door.Remaining;
            // the blocks near the dressed stretch only
            var near=new List<int>();
            for(int i=0;i<w.Blocks.Length;i++)
            {
                var b=w.Blocks[i];float best=1e9f;
                for(int k=0;k<pts.Length-1;k++)if(rest[k+1]<Outer+30)best=Math.Min(best,SegmentBox(pts[k],pts[k+1],b));
                if(best<34)near.Add(i);
            }
            var slots=new List<Slot>();var cells=new HashSet<long>();var floor=new List<(V3 at,float metres,NativeCityBleed.Bands band,float share,V3 along,float width,int seg)>();
            var crossings=new List<(V3 at,float width,float metres,NativeCityBleed.Bands band)>();var segs=new List<(V3 a,V3 along,float len,float width,float metres)>();
            for(int k=pts.Length-2;k>=0;k--)
            {
                if(rest[k+1]>Outer)break;
                string kind=street.Kinds!=null&&k<street.Kinds.Length?street.Kinds[k]:"flat";
                var a=pts[k];var b=pts[k+1];var ab=new V3(b.x-a.x,0,b.z-a.z);float len=ab.Length;if(len<.5f)continue;
                bool flat=kind=="flat"&&Math.Abs(a.y-b.y)<.01f;float grade=(b.y-a.y)/len,run=(b-a).Length;
                var along=ab/len;var right=new V3(along.z,0,-along.x);float width=street.Widths!=null&&k<street.Widths.Length?street.Widths[k]:10;
                if(flat&&k>0&&k<pts.Length-1)crossings.Add((a,width,rest[k+1]+len,NativeCityBleed.Band(bleed.Share(d,a))));
                if(flat)segs.Add((a,along,len,width,rest[k+1]));
                // the wall bays either side, every bay along the segment (from the door's end back)
                for(float s=len-Bay/2;s>0;s-=Bay)
                {
                    float metres=rest[k+1]+(len-s)*run/len;if(metres<FrontLine||metres>Outer)continue;
                    var c=a+along*s+new V3(0,grade*s,0);
                    foreach(int side in new[]{1,-1})
                    {
                        if(!FindWall(w,near,c,right*side,Math.Min(22,width/2+14),flat?0:grade,out var slot))continue;
                        slot.Metres=metres;slot.Side=side;slot.Share=bleed.Share(d,slot.Foot+slot.Normal*.5f+new V3(0,1.5f,0));slot.Band=NativeCityBleed.Band(slot.Share);
                        if(bleed.At(slot.Foot+slot.Normal*.5f).Door!=d||slot.Band==NativeCityBleed.Bands.None)continue;
                        if(!Clear_(plan,slot.Foot,Bay/2))continue;
                        bool overlap=false;foreach(var o in slots)if(o.Block==slot.Block&&V3.Dot(o.Normal,slot.Normal)>.99f&&Math.Abs(V3.Dot(o.Foot-slot.Foot,slot.Along))<Bay-.01f&&Math.Abs(V3.Dot(o.Foot-slot.Foot,slot.Normal))<.02f){overlap=true;break;}
                        if(!overlap)slots.Add(slot);
                    }
                }
                // the floor's 2 m world cells on the street's top (flat stretches only)
                if(!flat)continue;
                float half=width/2+3f;
                var lo=new V3(Math.Min(a.x,b.x),a.y,Math.Min(a.z,b.z));var hi=new V3(Math.Max(a.x,b.x),a.y,Math.Max(a.z,b.z));
                lo=lo-new V3(Math.Abs(right.x)*half,0,Math.Abs(right.z)*half);hi=hi+new V3(Math.Abs(right.x)*half,0,Math.Abs(right.z)*half);
                for(int gx=(int)Math.Floor(lo.x/Tile);gx*Tile+Tile<=hi.x+1e-3f;gx++)
                    for(int gz=(int)Math.Floor(lo.z/Tile);gz*Tile+Tile<=hi.z+1e-3f;gz++)
                    {
                        if(gx*Tile<lo.x-1e-3f||gz*Tile<lo.z-1e-3f)continue;
                        var at=new V3(gx*Tile+Tile/2,a.y,gz*Tile+Tile/2);
                        long key=((long)gx<<32)^(uint)gz^((long)Math.Round(a.y*4)<<52);if(!cells.Add(key))continue;
                        float s=V3.Dot(at-a,along);float metres=rest[k+1]+(len-Math.Max(0,Math.Min(len,s)));if(metres<FrontLine||metres>Outer)continue;
                        if(!Supported(w,near,at,Tile/2,Tile/2))continue;
                        float share=bleed.Share(d,at+new V3(0,1,0));var band=NativeCityBleed.Band(share);
                        if(band==NativeCityBleed.Bands.None||bleed.At(at).Door!=d||!Clear_(plan,at,Tile*.71f))continue;
                        floor.Add((at,metres,band,share,along,width,k));
                    }
            }
            plan.WallSlots+=slots.Count;plan.FloorCells+=floor.Count;
            fitWorld=w;fitNear=near;
            uint rs=NativeCityBleed.Hash(seed*2654435761u^(uint)(d+1)*40503u);
            Floors(plan,w,near,floor,theme,d,rs);
            Lines(plan,w,near,bleed,segs,theme,d,rs);
            Walls(plan,slots,theme,d,rs);
            Lamps(plan,slots,theme,d,rs);
            Spans(plan,w,near,slots,theme,d,rs);
            if(theme=="level0")Ceilings(plan,w,near,floor,crossings,d,rs);
        }

        private static float Rand(uint rs,uint salt,int i)=>(NativeCityBleed.Hash(rs^salt*0x9E3779B9u^(uint)i*0x85EBCA6Bu)&0xFFFFFF)/16777216f;

        // The street being dressed (Add's last guard: no piece above the floor enters a box).
        [ThreadStatic] private static World fitWorld;[ThreadStatic] private static List<int> fitNear;
        private static bool Add(Plan plan,string key,V3 at,float yaw,V3 scale,int d,NativeCityBleed.Bands band,uint rs,int i,LampState lamp=LampState.None,uint light=0)
        {
            int k=KindOf(key);if(k<0)throw new InvalidOperationException("No bleed kind "+key);
            var piece=new Piece{Kind=k,At=at,Yaw=yaw,Scale=scale,Door=(byte)d,Band=band,Lamp=lamp,Light=light==0?Kinds[k].Light:light,Seed=NativeCityBleed.Hash(rs^(uint)i*2246822519u^(uint)k*3266489917u)};
            if(Kinds[k].Mount!=Mount.Floor&&fitWorld!=null)
            {
                Bounds(piece,out var lo,out var hi);var ins=new V3(.012f,.012f,.012f);lo=lo+ins;hi=hi-ins;
                if(Kinds[k].Mount==Mount.Span){var a=Local(piece,new V3(Kinds[k].Min.x,0,0));var e=Local(piece,new V3(Kinds[k].Max.x,0,0));if(Math.Abs(a.x-e.x)>Math.Abs(a.z-e.z)){lo.x+=Reach;hi.x-=Reach;}else{lo.z+=Reach;hi.z-=Reach;}}
                if(!BoxClear(fitWorld,fitNear,lo,hi))return false;
            }
            plan.Pieces.Add(piece);return true;
        }
        /// <summary>A piece's world box (its kind's envelope, turned and scaled).</summary>
        public static void Bounds(in Piece p,out V3 lo,out V3 hi)
        {
            var k=Kinds[p.Kind];lo=new V3(1e9f,1e9f,1e9f);hi=new V3(-1e9f,-1e9f,-1e9f);
            foreach(float x in new[]{k.Min.x,k.Max.x})foreach(float y in new[]{k.Min.y,k.Max.y})foreach(float z in new[]{k.Min.z,k.Max.z})
            {var c=Local(p,new V3(x,y,z));lo=new V3(Math.Min(lo.x,c.x),Math.Min(lo.y,c.y),Math.Min(lo.z,c.z));hi=new V3(Math.Max(hi.x,c.x),Math.Max(hi.y,c.y),Math.Max(hi.z,c.z));}
        }
        private static readonly V3 One=new V3(1,1,1);

        // Paint on the street: patches that creep in through the warp and cover the threshold.
        private static void Floors(Plan plan,World w,List<int> near,List<(V3 at,float metres,NativeCityBleed.Bands band,float share,V3 along,float width,int seg)> cells,string theme,int d,uint rs)
        {
            for(int i=0;i<cells.Count;i++)
            {
                var c=cells[i];float r=Rand(rs,11,i);float t=(c.share-NativeCityBleed.WarpAt)/(NativeCityBleed.ThresholdAt-NativeCityBleed.WarpAt);
                float yaw=(int)(Rand(rs,12,i)*4)*(float)(Math.PI/2);bool warp=c.band==NativeCityBleed.Bands.Warp,thresh=c.band==NativeCityBleed.Bands.Threshold;
                // the warp's patches creep with the share; a seeded field (4 m cells) keeps them in clumps, not salt and pepper
                float clump=(NativeCityBleed.Cell(c.at,4)&1023)/1023f;bool patch=thresh||warp&&clump*.6f+r*.4f<.25f+.6f*Math.Max(0,t);
                bool drift=c.band==NativeCityBleed.Bands.Drift;
                switch(theme)
                {
                    // the carpet starts creeping in through the drift's inner half
                    case "level0":if(patch||drift&&clump*.7f+r*.3f<1.4f*(c.share-.35f))Add(plan,"level0.carpet",c.at,yaw,One,d,c.band,rs,i);break;
                    case "poolrooms":
                        if(thresh)Add(plan,r<.5f?"poolrooms.flood":"poolrooms.tile",c.at,yaw,One,d,c.band,rs,i);
                        else if(patch)Add(plan,"poolrooms.tile",c.at,yaw,One,d,c.band,rs,i);
                        else if(drift&&r<.05f)Add(plan,"poolrooms.drain",c.at,YawOf(c.along),One,d,c.band,rs,i);
                        if(warp&&!patch&&r>.94f)Add(plan,"poolrooms.ring",c.at+new V3(0,.001f,0),yaw,One,d,c.band,rs,i+100000);
                        break;
                    case "wardzero":
                        if(patch)Add(plan,"wardzero.decon",c.at,yaw,One,d,c.band,rs,i);
                        else if(drift&&clump<.18f+.5f*(c.share-NativeCityBleed.DriftAt))Add(plan,"wardzero.litter",c.at,yaw,One,d,c.band,rs,i);
                        break;
                    case "station":
                        if(thresh)Add(plan,"station.platform",c.at,yaw,One,d,c.band,rs,i);
                        break;
                    case "ironworks":
                        if(thresh)Add(plan,"ironworks.plates",c.at,yaw,One,d,c.band,rs,i);
                        else if(drift&&clump<.2f+.8f*(c.share-NativeCityBleed.DriftAt))Add(plan,"ironworks.ash",c.at,yaw,One,d,c.band,rs,i);
                        break;
                }
            }
        }

        // Linear paint along each flat stretch: the tram rails converging down the Station's street (its drift and warp, on the
        // centre line) and the slag crust in the Ironworks' gutters (its warp, a strip inside each kerb), every 2 m.
        private static void Lines(Plan plan,World w,List<int> near,NativeCityBleed bleed,List<(V3 a,V3 along,float len,float width,float metres)> segs,string theme,int d,uint rs)
        {
            if(theme!="station"&&theme!="ironworks")return;int i=0;
            foreach(var g in segs)
            {
                var right=new V3(g.along.z,0,-g.along.x);
                for(float s=g.len-1;s>1;s-=2,i++)
                {
                    float metres=g.metres+(g.len-s);if(metres<FrontLine+2||metres>Outer)continue;
                    var c=g.a+g.along*s;
                    if(theme=="station")
                    {
                        float share=bleed.Share(d,c+new V3(0,1,0));var band=NativeCityBleed.Band(share);
                        if(band<NativeCityBleed.Bands.Drift||band==NativeCityBleed.Bands.Threshold||bleed.At(c).Door!=d||!Clear_(plan,c,1.4f))continue;
                        // one pair down the middle through the drift; the warp's fan of tracks either side of it
                        foreach(float off in band==NativeCityBleed.Bands.Warp?new[]{0f,-3.2f,3.2f}:new[]{0f})
                        {
                            if(Math.Abs(off)+.9f>g.width/2+2.5f)continue;var at=c+right*off;
                            if(!Clear_(plan,at,1.4f)||!Supported(w,near,at,g.along,.9f,1))continue;
                            Add(plan,"station.rails",at,YawOf(g.along),One,d,band,rs,200000+i*3+(off<0?1:off>0?2:0));
                        }
                    }
                    else foreach(int side in new[]{1,-1})
                    {
                        var at=c+right*(side*(g.width/2-.45f));float share=bleed.Share(d,at+new V3(0,1,0));var band=NativeCityBleed.Band(share);
                        if(band!=NativeCityBleed.Bands.Warp||bleed.At(at).Door!=d||!Clear_(plan,at,1.1f))continue;
                        if(!Supported(w,near,at,g.along,.35f,1))continue;
                        Add(plan,"ironworks.slag",at,YawOf(g.along),One,d,band,rs,300000+i*2+(side>0?1:0));
                    }
                }
            }
        }

        // Skins and details on the facades.
        private static void Walls(Plan plan,List<Slot> slots,string theme,int d,uint rs)
        {
            float lastSign=-1e9f,lastDetail=-1e9f;
            for(int i=0;i<slots.Count;i++)
            {
                var s=slots[i];float yaw=YawOf(s.Normal);float r=Rand(rs,21,i);
                if(s.Sloped)
                {
                    // a bay beside a ramp or stair: signs and posters only (their feet a metre or more over the floor)
                    if(Math.Abs(s.Metres-lastDetail)<(s.Band==NativeCityBleed.Bands.Rumour?SignEvery:DetailEvery)||s.Band==NativeCityBleed.Bands.Drift&&r>.6f)continue;
                    string key=SlopeDetail(theme,s);if(key==null)continue;lastDetail=s.Metres;
                    Add(plan,key,s.Foot,yaw,One,d,s.Band,rs,i);continue;
                }
                switch(s.Band)
                {
                    case NativeCityBleed.Bands.Rumour:
                        if(Math.Abs(s.Metres-lastSign)<SignEvery)break;lastSign=s.Metres;
                        Add(plan,RumourSign(theme,s),s.Foot,yaw,One,d,s.Band,rs,i);break;
                    case NativeCityBleed.Bands.Drift:
                        if(Math.Abs(s.Metres-lastDetail)<DetailEvery||r>.6f)break;lastDetail=s.Metres;
                        Add(plan,DriftDetail(theme,r),s.Foot,yaw,One,d,s.Band,rs,i);break;
                    case NativeCityBleed.Bands.Warp:
                    {
                        string skin=WarpSkin(theme);
                        // the warp's skin takes whole facades (one seeded dither per block face), more of them nearer the door
                        float t=(s.Share-NativeCityBleed.WarpAt)/(NativeCityBleed.ThresholdAt-NativeCityBleed.WarpAt);
                        float face=(NativeCityBleed.Hash(rs^(uint)s.Block*2654435761u^(s.Normal.x>.5f?1u:s.Normal.x<-.5f?2u:s.Normal.z>.5f?3u:4u))&1023)/1023f;
                        bool skinned=skin!=null&&face<.3f+.7f*t;
                        if(skinned)Add(plan,skin,s.Foot,yaw,One,d,s.Band,rs,i);
                        else if(Math.Abs(s.Metres-lastDetail)>=DetailEvery*.6f&&r<.7f){lastDetail=s.Metres;Add(plan,WarpDetail(theme,r),s.Foot,yaw,One,d,s.Band,rs,i);}
                        if(theme=="wardzero"&&s.Top>=Clear+Upper+.05f&&face<.5f+.5f*t)Uppers(plan,s,"wardzero.sheet",d,rs,i,2);
                        break;
                    }
                    case NativeCityBleed.Bands.Threshold:
                    {
                        string skin=ThresholdSkin(theme);if(skin==null)break;
                        Add(plan,skin,s.Foot,yaw,One,d,s.Band,rs,i);
                        bool sheet=theme=="wardzero"&&s.Top>=Clear+2*Upper+Upper*.5f;
                        Uppers(plan,s,skin+".upper",d,rs,i,sheet?2:3);
                        if(sheet)Uppers(plan,s,"wardzero.sheet",d,rs,i+50000,1,2);
                        break;
                    }
                }
            }
        }
        // Upper storeys from the clear height to the wall's top (at most `rows`, from row `from`); the last row is scaled to fit,
        // or left bare under half a storey.
        private static void Uppers(Plan plan,Slot s,string key,int d,uint rs,int i,int rows,int from=0)
        {
            if(KindOf(key)<0)return;float yaw=YawOf(s.Normal);
            for(int row=from;row<from+rows;row++)
            {
                float base_=Storey+row*Upper;float room=s.Top-base_;if(room<Upper*.5f)break;
                float k=Math.Min(1,room/Upper);Add(plan,key,s.Foot+new V3(0,base_,0),yaw,new V3(1,k,1),d,s.Band,rs,i*8+row+1000000);
                if(k<1)break;
            }
        }
        private static string RumourSign(string theme,Slot s)
        {
            switch(theme)
            {
                case "level0":return "level0.exit";
                case "poolrooms":return "poolrooms.plate";
                // EVACUATION ROUTE points away from the door, toward the camp (the viewer's right is the sign's -x)
                case "wardzero":return s.Side>0?"wardzero.evac.r":"wardzero.evac.l";
                case "station":return "station.roundel";
                default:return "ironworks.sign";
            }
        }
        private static string SlopeDetail(string theme,Slot s)
        {
            switch(theme)
            {
                case "level0":return s.Band>=NativeCityBleed.Bands.Warp?"level0.missing":null;
                case "poolrooms":return s.Band==NativeCityBleed.Bands.Rumour?"poolrooms.plate":"poolrooms.glassblock";
                case "wardzero":return s.Band==NativeCityBleed.Bands.Rumour?(s.Side>0?"wardzero.evac.r":"wardzero.evac.l"):"wardzero.poster";
                case "station":return "station.roundel";
                default:return "ironworks.sign";
            }
        }
        private static string DriftDetail(string theme,float r)
        {
            switch(theme)
            {
                case "level0":return "level0.exit";
                case "poolrooms":return r<.3f?"poolrooms.plate":"poolrooms.glassblock";
                case "wardzero":return "wardzero.poster";
                case "station":return "station.roundel";
                default:return r<.4f?"ironworks.sign":"ironworks.rollerdoor";
            }
        }
        private static string WarpSkin(string theme)=>theme=="level0"?"level0.wallpaper":theme=="wardzero"?"wardzero.growth":theme=="station"?"station.brick":theme=="ironworks"?"ironworks.plate":null;
        private static string WarpDetail(string theme,float r)
        {
            switch(theme)
            {
                case "level0":return r<.5f?"level0.missing":"level0.exit";
                case "poolrooms":return r<.4f?"poolrooms.doorway":r<.7f?"poolrooms.glassblock":"poolrooms.plate";
                case "wardzero":return "wardzero.poster";
                case "station":return "station.roundel";
                default:return "ironworks.rollerdoor";
            }
        }
        private static string ThresholdSkin(string theme)=>theme=="level0"?"level0.threshold":theme=="poolrooms"?"poolrooms.threshold":theme=="wardzero"?"wardzero.cladding":theme=="station"?"station.threshold":theme=="ironworks"?"ironworks.furnace":null;

        // About one lamp every 12 m, alternating sides; 1 in 5 dead and 1 in 10 flickering (Level 0's by circuits of four),
        // none dead at the Poolrooms' calm threshold, every Ward Zero lamp red and turning from the warp.
        private static void Lamps(Plan plan,List<Slot> slots,string theme,int d,uint rs)
        {
            float next=-1;int count=0,side=1;var order=new List<int>();for(int i=0;i<slots.Count;i++)order.Add(i);
            order.Sort((a,b)=>slots[a].Metres!=slots[b].Metres?slots[a].Metres.CompareTo(slots[b].Metres):slots[a].Side.CompareTo(slots[b].Side));
            foreach(int i in order)
            {
                var s=slots[i];if(s.Band<NativeCityBleed.Bands.Drift||s.Metres<next)continue;
                bool flush=theme=="poolrooms"&&s.Band>=NativeCityBleed.Bands.Warp;
                if(s.Side!=side&&HasOther(slots,i,side))continue;
                if(!flush&&s.Top<4.6f+.6f)continue;
                next=s.Metres+LampEvery;side=-s.Side;
                string key;uint light=0;var state=LampState.Steady;float r=Rand(rs,31,count),t=(s.Share-NativeCityBleed.DriftAt)/(NativeCityBleed.WarpAt-NativeCityBleed.DriftAt);
                switch(theme)
                {
                    case "level0":key="level0.lamp";r=Rand(rs,32,count/4);break;
                    case "poolrooms":key=flush?"poolrooms.poollamp":"common.lamp";if(!flush)light=0x9FE8F0;break;
                    case "wardzero":
                        if(s.Band>=NativeCityBleed.Bands.Warp||Rand(rs,33,count)<t){key="wardzero.lamp";state=LampState.Turning;}else key="common.lamp";break;
                    case "station":key="station.lamp";light=Rand(rs,34,count)<.6f?SignalRed:SignalGreen;break;
                    default:key=s.Band>=NativeCityBleed.Bands.Warp?"ironworks.highbay":"ironworks.worklamp";break;
                }
                if(state==LampState.Steady&&!(theme=="poolrooms"&&s.Band==NativeCityBleed.Bands.Threshold))
                {
                    if(r<.2f)state=LampState.Dead;else if(Rand(rs,35,count)<.1f)state=LampState.Flicker;
                }
                if(flush&&s.Sloped)continue;
                float h=flush?0:4.6f+s.Rise;
                Add(plan,key,s.Foot+new V3(0,h,0),YawOf(s.Normal),One,d,s.Band,rs,count,state,light);count++;
            }
        }
        private static bool HasOther(List<Slot> slots,int i,int side)
        {
            var s=slots[i];foreach(var o in slots)if(o.Side==side&&Math.Abs(o.Metres-s.Metres)<Bay*1.5f&&o.Band>=NativeCityBleed.Bands.Drift)return true;return false;
        }

        // Cables across the street between two facing walls: Level 0's troffer wires and the Station's catenary from the drift,
        // the Poolrooms' lane ropes and pennants, Ward Zero's decon tents and the Ironworks' pipe bridges from the warp, the
        // crane girder at the Ironworks' threshold.
        private static void Spans(Plan plan,World w,List<int> near,List<Slot> slots,string theme,int d,uint rs)
        {
            float next=-1;int count=0;V3? lastWire=null;float lastWireMetres=0;
            var order=new List<int>();for(int i=0;i<slots.Count;i++)if(slots[i].Side>0)order.Add(i);
            order.Sort((a,b)=>slots[a].Metres.CompareTo(slots[b].Metres));
            foreach(int i in order)
            {
                var s=slots[i];if(s.Band<NativeCityBleed.Bands.Drift||s.Metres<next)continue;
                string piece;float height;
                bool warp=s.Band>=NativeCityBleed.Bands.Warp,thresh=s.Band==NativeCityBleed.Bands.Threshold;
                switch(theme)
                {
                    case "level0":if(thresh)continue;piece=null;height=5.4f;break;
                    case "station":piece=null;height=5.8f;break;
                    case "poolrooms":if(!warp)continue;piece=count%2==0?"poolrooms.lanerope":"poolrooms.pennants";height=5.2f;break;
                    case "wardzero":if(!warp)continue;piece="wardzero.tent";height=4.9f;break;
                    default:if(!warp)continue;piece=thresh?"ironworks.girder":"ironworks.pipes";height=thresh?9.5f:6.2f;break;
                }
                // the facing wall across the street, at the same station
                Slot other=default;bool found=false;
                foreach(var o in slots)if(o.Side<0&&Math.Abs(o.Metres-s.Metres)<.01f&&V3.Dot(o.Normal,s.Normal)<-.99f){other=o;found=true;break;}
                if(!found)continue;
                float need=height+(piece=="ironworks.pipes"?1.0f:piece=="ironworks.girder"?.6f:.5f);
                if(s.Top<need||other.Top<need)continue;
                height+=s.Rise;
                var a=s.Foot+new V3(0,height,0);var b=other.Foot+new V3(0,height,0);var ab=new V3(b.x-a.x,0,b.z-a.z);float gap=ab.Length;
                if(gap<4||gap>24)continue;var across=ab/gap;float yaw=YawOf(new V3(across.z,0,-across.x));
                // the street's floor under the span must lie Clear m below its lowest point everywhere (checked again by BleedChecks)
                var kind=piece==null?Kinds[KindOf("common.wire")]:Kinds[KindOf(piece)];
                if(!SpanClear(w,near,a,b,height+kind.Min.y))continue;
                next=s.Metres+SpanEvery;
                if(piece==null)
                {
                    Add(plan,"common.wire",a,YawOf(across)-(float)(Math.PI/2),new V3(gap,1,1),d,s.Band,rs,count*16);
                    if(theme=="level0")
                    {
                        int n=gap>12?2:1;for(int k=0;k<n;k++){var at=a+across*(gap*(k+1)/(n+1));Add(plan,"level0.troffer",at,yaw,One,d,s.Band,rs,count*16+1+k,TroffState(rs,count*16+1+k));}
                    }
                    else
                    {
                        var mid=a+across*(gap/2);Add(plan,"station.dropper",mid,yaw,One,d,s.Band,rs,count*16+1);
                        // the contact wire along the street to the last span's dropper, under the cables
                        if(lastWire.HasValue&&Math.Abs(s.Metres-lastWireMetres)<SpanEvery*1.6f)
                        {
                            var from=lastWire.Value-new V3(0,.5f,0);var to=mid-new V3(0,.5f,0);var run=new V3(to.x-from.x,0,to.z-from.z);float rl=run.Length;
                            if(rl>1&&Math.Abs(from.y-to.y)<.01f&&SpanClear(w,near,from,to,from.y-.02f))Add(plan,"common.wire",from,YawOf(run/rl)-(float)(Math.PI/2),new V3(rl,1,1),d,s.Band,rs,count*16+2);
                        }
                        lastWire=mid;lastWireMetres=s.Metres;
                    }
                }
                else
                {
                    int n=Math.Max(1,(int)Math.Round(gap/2));float scale=gap/(2*n);
                    for(int k=0;k<n;k++)Add(plan,piece,a+across*(2*scale*k),YawOf(across)-(float)(Math.PI/2),new V3(scale,1,1),d,s.Band,rs,count*16+k%16+1000*k);
                    if(piece=="ironworks.girder")Add(plan,"ironworks.hook",a+across*(gap*(.35f+.3f*Rand(rs,41,count)))+new V3(0,kind.Min.y,0),yaw,One,d,s.Band,rs,count*16+15);
                }
                Add(plan,"common.anchor",s.Foot+new V3(0,height,0),YawOf(s.Normal),One,d,s.Band,rs,count*16+13);
                Add(plan,"common.anchor",other.Foot+new V3(0,height,0),YawOf(other.Normal),One,d,s.Band,rs,count*16+14);
                count++;
            }
        }
        private static LampState TroffState(uint rs,int i){float r=Rand(rs,36,i);return r<.2f?LampState.Dead:r<.3f?LampState.Flicker:LampState.Steady;}

        // Level 0's signature: an office ceiling grid hung 6 m over its crossings in the warp, and over the whole street at the
        // threshold, half its tiles gone (2 m cells on the floor's own grid).
        private static void Ceilings(Plan plan,World w,List<int> near,List<(V3 at,float metres,NativeCityBleed.Bands band,float share,V3 along,float width,int seg)> cells,List<(V3 at,float width,float metres,NativeCityBleed.Bands band)> crossings,int d,uint rs)
        {
            const float H=6f;
            for(int i=0;i<cells.Count;i++)
            {
                var c=cells[i];bool under=c.band==NativeCityBleed.Bands.Threshold;
                if(!under&&c.band==NativeCityBleed.Bands.Warp)foreach(var x in crossings)if(x.band>=NativeCityBleed.Bands.Warp&&Math.Abs(x.at.x-c.at.x)<x.width/2+4&&Math.Abs(x.at.z-c.at.z)<x.width/2+4&&Math.Abs(x.at.y-c.at.y)<.1f){under=true;break;}
                if(!under)continue;
                var at=c.at+new V3(0,H,0);if(!BoxClear(w,near,at+new V3(-1,0,-1),at+new V3(1,1.8f,1)))continue;
                float r=Rand(rs,51,i);string key=r<.45f?"level0.ceiling.tile":r<.8f?"level0.ceiling.gap":"level0.ceiling.light";
                Add(plan,key,at,0,One,d,c.band,rs,i,key=="level0.ceiling.light"?TroffState(rs,i+7000):LampState.None);
            }
        }

        // ---- Geometry: rays, support and clearance against the map's boxes (all axis-aligned in candidate 3) --------------------
        /// <summary>The first box face a horizontal ray meets: false when none within `reach`.</summary>
        public static bool Ray(ContentBlock[] blocks,List<int> near,V3 o,V3 dir,float reach,out int block,out float t,out V3 normal)
        {
            block=-1;t=reach;normal=default;
            foreach(int i in near)
            {
                var b=blocks[i];V3 lo=b.pos-b.size*.5f,hi=b.pos+b.size*.5f;
                if(o.y<lo.y||o.y>hi.y)continue;
                float t0=0,t1=t;V3 n=default;bool ok=true;
                for(int ax=0;ax<3&&ok;ax+=2)
                {
                    float oc=ax==0?o.x:o.z,dc=ax==0?dir.x:dir.z,l=ax==0?lo.x:lo.z,h=ax==0?hi.x:hi.z;
                    if(Math.Abs(dc)<1e-6f){if(oc<l||oc>h)ok=false;continue;}
                    float a=(l-oc)/dc,c=(h-oc)/dc;var na=ax==0?new V3(-1,0,0):new V3(0,0,-1);
                    if(a>c){(a,c)=(c,a);na=new V3(-na.x,0,-na.z);}
                    if(a>t0){t0=a;n=na;}if(c<t1)t1=c;if(t0>t1)ok=false;
                }
                if(!ok||t0<=1e-4f||t0>=t)continue;
                t=t0;block=i;normal=n;
            }
            return block>=0;
        }
        // grade: the street's rise per metre along it (0 on the flat); the face runs along the street, so a bay's floor rises by
        // grade x its offset along the face (either way).
        private static bool FindWall(World w,List<int> near,V3 c,V3 dir,float reach,float grade,out Slot slot)
        {
            slot=default;var o=c+new V3(0,1.6f,0);float rise=Math.Abs(grade)*Bay/2;
            if(!Ray(w.Blocks,near,o,dir,reach,out int bi,out float t,out var n)||!w.Wall[bi])return false;
            var b=w.Blocks[bi];float top=b.pos.y+b.size.y/2,bottom=b.pos.y-b.size.y/2;
            if(bottom>c.y-rise+.02f||top<c.y+rise+Storey+.05f)return false;
            var hit=o+dir*t;var along=new V3(n.z,0,-n.x);var foot=new V3(hit.x,c.y,hit.z);
            // the whole bay: the same face in sight from the street at its ends and middle, low, mid and high (over the bay's
            // high end on a slope), and on the flat the street's floor at the wall's foot
            foreach(float u in new[]{-Bay/2+.05f,0,Bay/2-.05f})
                foreach(float h in new[]{.15f+2*rise,1.5f+rise,Storey-.05f})
                {
                    var p=foot+along*u+n*1.2f+new V3(0,h,0);
                    if(!Ray(w.Blocks,near,p,new V3(-n.x,0,-n.z),2f,out int bj,out float tj,out var nj)||bj!=bi||Math.Abs(tj-1.2f)>.01f)return false;
                }
            if(rise<=.005f)foreach(float u in new[]{-Bay/2+.05f,Bay/2-.05f})if(!Supported(w,near,foot+along*u+n*.3f,.05f,.05f))return false;
            // nothing stands against the bay's ground storey (a rail, a post, a tread; on a slope above its treads)
            var c0=foot+along*(-Bay/2+.02f)+n*.01f+new V3(0,.01f+2*rise+(rise>.005f?.3f:0),0);var c1=foot+along*(Bay/2-.02f)+n*.4f+new V3(0,Storey+rise,0);
            if(!BoxClear(w,near,new V3(Math.Min(c0.x,c1.x),c0.y,Math.Min(c0.z,c1.z)),new V3(Math.Max(c0.x,c1.x),c1.y,Math.Max(c0.z,c1.z))))return false;
            slot=new Slot{Foot=foot,Normal=n,Along=along,Top=top-c.y-rise,Block=bi,Rise=rise};
            return true;
        }
        // A rectangle along a street (half extents across it and along it): the streets run along x or z.
        private static bool Supported(World w,List<int> near,V3 at,V3 along,float across,float alongHalf)=>Math.Abs(along.x)>Math.Abs(along.z)?Supported(w,near,at,alongHalf,across):Supported(w,near,at,across,alongHalf);
        /// <summary>Whether a floor rectangle (centre, half extents in x and z) lies wholly on box tops at its height.</summary>
        private static bool Supported(World w,List<int> near,V3 at,float hx,float hz)
        {
            foreach(float sx in new[]{-1f,0,1f})foreach(float sz in new[]{-1f,0,1f})
            {
                float x=at.x+sx*hx*.98f,z=at.z+sz*hz*.98f;bool on=false;
                foreach(int i in near)
                {
                    var b=w.Blocks[i];if(Math.Abs(b.pos.y+b.size.y/2-at.y)>.01f)continue;
                    if(Math.Abs(x-b.pos.x)<=b.size.x/2+1e-3f&&Math.Abs(z-b.pos.z)<=b.size.z/2+1e-3f){on=true;break;}
                }
                if(!on)return false;
                // nothing stands on the floor there (a kerb, a post, a tread)
                if(!BoxClear(w,near,new V3(x-.01f,at.y+.02f,z-.01f),new V3(x+.01f,at.y+.3f,z+.01f)))return false;
            }
            return true;
        }
        private static bool BoxClear(World w,List<int> near,V3 lo,V3 hi)
        {
            foreach(int i in near)
            {
                var b=w.Blocks[i];
                if(hi.x>b.pos.x-b.size.x/2+1e-3f&&lo.x<b.pos.x+b.size.x/2-1e-3f&&hi.y>b.pos.y-b.size.y/2+1e-3f&&lo.y<b.pos.y+b.size.y/2-1e-3f&&hi.z>b.pos.z-b.size.z/2+1e-3f&&lo.z<b.pos.z+b.size.z/2-1e-3f)return false;
            }
            return true;
        }
        // A cable's path at height y from a to b (horizontal): every floor under it Clear m below `low`, and nothing in its way.
        private static bool SpanClear(World w,List<int> near,V3 a,V3 b,float low)
        {
            var ab=b-a;float len=new V3(ab.x,0,ab.z).Length;int n=Math.Max(2,(int)Math.Ceiling(len/.5f));
            for(int k=1;k<n;k++)
            {
                var p=a+ab*((float)k/n);float floor=FloorBelow(w,near,p);
                if(low-floor<Clear+.4f)return false;
                if(k>1&&k<n-1&&!BoxClear(w,near,new V3(p.x-.05f,low,p.z-.05f),new V3(p.x+.05f,a.y+.6f,p.z+.05f)))return false;
            }
            return true;
        }
        /// <summary>The highest box top at or below a place (or -1e9).</summary>
        public static float FloorBelow(ContentBlock[] blocks,IEnumerable<int> near,V3 p)
        {
            float best=-1e9f;
            foreach(int i in near){var b=blocks[i];float top=b.pos.y+b.size.y/2;if(top<=p.y+1e-3f&&top>best&&Math.Abs(p.x-b.pos.x)<=b.size.x/2&&Math.Abs(p.z-b.pos.z)<=b.size.z/2)best=top;}
            return best;
        }
        private static float FloorBelow(World w,List<int> near,V3 p)=>FloorBelow(w.Blocks,near,p);
        private static float SegmentBox(V3 a,V3 b,ContentBlock box)
        {
            // horizontal distance from a segment to a box's footprint (sampled every 2 m: a coarse corridor filter)
            var ab=b-a;float len=new V3(ab.x,0,ab.z).Length;int n=Math.Max(1,(int)Math.Ceiling(len/2));float best=1e9f;
            for(int k=0;k<=n;k++)best=Math.Min(best,FlatDistance(box,a+ab*((float)k/n)));
            return best;
        }

        /// <summary>The five instances a door can lead to, by the walk's names (the Developer walk's door-deal picker).</summary>
        public static readonly string[] Instances={"Level 0","The Poolrooms","Ward Zero","Central Station","City Ironworks"};
        /// <summary>The first run seed from `from` on whose hall deal (NativeCityLayout.DealHalls, unchanged) puts these three
        /// instances behind the east, south and west doors: the picker's chosen deal, dealt the run's own way.</summary>
        public static uint DealSeedFor(uint from,IReadOnlyList<string> eastSouthWest)
        {
            for(uint s=from,n=0;n<4096;s++,n++)
            {
                var slots=NativeCityLayout.DealHalls(s,eastSouthWest);
                if(slots[1]==eastSouthWest[0]&&slots[2]==eastSouthWest[1]&&slots[3]==eastSouthWest[2])return s;
            }
            throw new InvalidOperationException("No run seed deals "+string.Join(", ",eastSouthWest));
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || ARENA_TESTS
        /// <summary>The Developer walk's plan: its map, the bleed of its deal (NativeCityBleed.ForWalk) and its routes' records.</summary>
        public static Plan ForWalk(NativeLayeredCityWalk walk,NativeCityBleed bleed,uint seed)
        {
            var streets=new List<Street>();foreach(var r in walk.Routes)streets.Add(new Street{Widths=r.Widths,Kinds=r.Kinds});
            return Build(walk.Map,bleed,streets,seed);
        }
#endif
    }
}
