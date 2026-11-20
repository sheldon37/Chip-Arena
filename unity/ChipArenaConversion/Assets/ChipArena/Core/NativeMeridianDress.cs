using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Meridian's street dress (docs/expedition/MERIDIAN-DRESS.md; the MERIDIAN art bible; plan W1, W8b/c, W10): the planner that
    /// turns a city's blocks, ramps, routes and roof hatches into the city bank's modules (Resources/ChipArenaCityMeridian, built by
    /// tools/stages/meridian_bank.py, git-ignored, from the Drive zip) and into generated surfaces on the colliders' own faces, and
    /// says which map blocks the art covers. Runtime NativeMeridianDressDraw combines both into 32 m chunks at load and draws them;
    /// without the bank nothing here is used and the greybox draws as before.
    /// <para>The rules it keeps (the integrator's brief, the bible 2.3): art is never more than 0.25 m short of a collider (the
    /// generated surfaces lie on the faces; a facade module's recesses are at most 0.25 m deep); nothing stands proud of a collider
    /// below 3.1 m over the floor in front of it, except flat paint of 6 cm or less (a module placed lower than that is its hugging
    /// variant, whose every vertex lies between 0.25 m in and 0.06 m out of its face); every face of a covered block that can be
    /// seen is drawn, and no block is covered that is not. Free-standing furniture waits for Astra's prop blocks: lamps go on
    /// wall brackets and span wires, the 6:47 clock and the siren on walls, paint on the ground and the walls.</para>
    /// <para>Deterministic: the same records, layout and run give the same plan on every peer (no clock, no Unity). The theme hook
    /// <see cref="ThemeAt"/> (D3's bleed) may swap a module for its themed variant. Presentation only: never a collider, never sim
    /// data, nothing on the wire.</para>
    /// </summary>
    public static partial class NativeMeridianDress
    {
        public const string Bank="ChipArenaCityMeridian";
        public const float ChunkSize=32f,Hug=3.1f,Paint=.06f,Short=.25f,Bay=2.5f,GroundStorey=4f,Storey=3f;
        /// <summary>The theme at a point (D3's bleed, NativeCityBleed): a theme id (level0, poolrooms, wardzero, station, ironworks,
        /// final) or null for plain Meridian. Null by default. A themed module is the plain key plus "@" and the theme; the plain
        /// key draws where the bank has no such variant.</summary>
        public static Func<V3,string> ThemeAt;
        public static readonly string[] Themes={"level0","poolrooms","wardzero","station","ironworks","final"};
        /// <summary>The presentation look table (plan W1, R4): a map id (or an id prefix ending in '-') and the look it draws in,
        /// read by NativeLighting.Look before the map's own look. Its one row: the Developer walk's five Meridian layouts draw in
        /// `expedition-night` (MapLooks.json, written by docs/arenas/scripts/expedition_city.py --maplooks) while the city bank is
        /// present (the night needs its lamps; without the bank the greybox keeps the town's dusk). The live expedition's generated
        /// map keeps its look until Astra moves the run onto candidate-3 (then `expedition-generated-city` joins the table).</summary>
        public static readonly (string map,string look)[] LookTable={("expedition-city-walk-","expedition-night")};
        /// <summary>The table's look for a map id (null: none), given whether the city bank is present (asked only on a match).</summary>
        public static string LookOf(string mapId,Func<bool> bank)
        {
            if(mapId==null)return null;
            foreach(var (map,look) in LookTable)if(map.EndsWith("-",StringComparison.Ordinal)?mapId.StartsWith(map,StringComparison.Ordinal):mapId==map)return bank==null||bank()?look:null;
            return null;
        }

        /// <summary>A route as the walk (or later the live run) records it: its polyline from the gate (horizontal between points),
        /// each leg's width and kind (flat, stair, ramp, bridge, roof).</summary>
        public sealed class Route{public string Name;public V3[] Points;public float[] Widths;public string[] Kinds;}
        /// <summary>What the planner reads: the map's blocks and ramps, the layout (the plan's seed), the run's seed (dead lamps and
        /// flickers), the four routes (north to the final court, then east, south and west to the dealt halls), the roof hatches
        /// and the hall deal (slot 0 the final court).</summary>
        public sealed class Input{public ContentMap Map;public uint Layout,Run;public Route[] Routes;public V3[] Hatches;public string[] Slots;}

        /// <summary>A module drawn at a place: its bank key (themed or plain), its 3 x 4 rows (module space to the world; the
        /// facades' are a rotation with a stretch along the facade and up), the chunk it is combined into, the block and face it
        /// dresses (-1: none) and its face rectangle (along, up; for the checks), the floor in front of it, and whether it draws
        /// apart from the chunks each frame (a flickering lamp).</summary>
        public sealed class Placement{public string Key;public readonly float[] M=new float[12];public int Chunk,Block=-1,Face=-1;public float U0,U1,V0,V1,Floor;public bool Dynamic,Hugs,Paint;}
        /// <summary>A generated surface: a rectangle (origin, its two edges) on a collider's face, its finish (NativeMeridianSurfaces),
        /// its layer (the faces it lies on), the floor below it (the grime rises from it), its chunk and block.</summary>
        public struct Quad{public V3 O,U,V,N;public int Fin,Layer,Chunk,Block;public float Floor;}
        /// <summary>A street lamp: its mount on the wall (or the span's middle), the light's place (its bowl), the pool's centre on
        /// the floor, its kind (0 a swan-neck bracket, 1 a span pendant), its state (0 working, 1 flickering, 2 dead), whether it
        /// is on a route home (the protected circuit: warmer glass, never dead), its seed and its placement. Kind 2 is a flush lamp
        /// set in a low wall (its light low, at the floor in front).</summary>
        public sealed class Lamp{public V3 Mount,Bowl,Pool;public int Kind,State,Placement=-1,Route=-1;public bool Protected;public float Seed,Radius;}
        public const int Working=0,Flicker=1,Dead=2,Swan=0,Pendant=1,Flush=2;
        public sealed class Plan
        {
            public uint Layout,Run;public float MinX,MinZ;public int Nx,Nz;
            public readonly List<Placement> Placements=new List<Placement>();
            public readonly List<Quad> Quads=new List<Quad>();
            public readonly List<Lamp> Lamps=new List<Lamp>();
            /// <summary>The map blocks the art draws in place of (their greybox boxes are left out).</summary>
            public readonly HashSet<string> Covers=new HashSet<string>(StringComparer.Ordinal);
            /// <summary>Route lamp points that found no wall or span to hang from (dark gaps on the route; counted for the report).</summary>
            public int Unmounted;
            public int ChunkOf(float x,float z){int cx=Math.Max(0,Math.Min(Nx-1,(int)Math.Floor((x-MinX)/ChunkSize)));int cz=Math.Max(0,Math.Min(Nz-1,(int)Math.Floor((z-MinZ)/ChunkSize)));return cx+cz*Nx;}
            /// <summary>A digest of the whole plan (keys, places, surfaces, lamps): the determinism checks compare it.</summary>
            public ulong Digest()
            {
                ulong h=1469598103934665603UL;void S(string s){foreach(char c in s){h^=c;h*=1099511628211UL;}}void F(float f){h^=(ulong)BitConverter.SingleToInt32Bits(f)&0xffffffffUL;h*=1099511628211UL;}
                foreach(var p in Placements){S(p.Key);foreach(var m in p.M)F(m);F(p.Chunk);}
                foreach(var q in Quads){F(q.O.x);F(q.O.y);F(q.O.z);F(q.U.x);F(q.U.y);F(q.U.z);F(q.V.x);F(q.V.y);F(q.V.z);F(q.Fin);F(q.Layer);}
                foreach(var l in Lamps){F(l.Bowl.x);F(l.Bowl.y);F(l.Bowl.z);F(l.State);F(l.Kind);}
                return h;
            }
        }

        // ------------------------------------------------------------------------------------------------------------------
        // Roles: what each block of the five plans is, from its id (the plans' roles; MeridianDressChecks holds this to the specs)
        // ------------------------------------------------------------------------------------------------------------------
        public const string RLot="lot",RGround="ground",RTread="tread",RLanding="landing",RRail="rail",RParapet="parapet",RBridge="bridge",RLid="lid",
            RArcadeRoof="arcade-roof",RPost="post",RPier="pier",RViaduct="viaduct",RWagon="wagon",RWall="wall",RShed="shed",RCordon="cordon",RTower="tower",
            RTunnel="tunnel-roof",RHall="hall",RCourt="court",RLandmark="landmark",RCamp="camp",ROther="other";
        /// <summary>The role of a map block by its id (lots: those the sheet lists). Halls, the court, City Hall and the landmarks are
        /// D2's; the camp fence is the hub's; neither is dressed here.</summary>
        public static string RoleOf(string id,IReadOnlyDictionary<string,string> lots)
        {
            if(string.IsNullOrEmpty(id))return ROther;
            if(id.Contains("-tread-"))return RTread;
            if(lots!=null&&lots.ContainsKey(id))return RLot;
            if(id.StartsWith("terrace-",StringComparison.Ordinal))return RGround;
            if(id.StartsWith("camp-",StringComparison.Ordinal))return RCamp;
            if(id.StartsWith("fire-escape-",StringComparison.Ordinal)&&id.EndsWith("-landing",StringComparison.Ordinal))return RLanding;
            if(id.StartsWith("street-rail-",StringComparison.Ordinal))return RRail;
            if(id.Contains("-parapet"))return RParapet;
            if(id.StartsWith("cordon-",StringComparison.Ordinal))return id.Contains("-tower")?RTower:RCordon;
            if(id=="city-hall"||id=="city-hall-tower"||id.StartsWith("court-",StringComparison.Ordinal))return RCourt;
            if(id.StartsWith("hall-",StringComparison.Ordinal))return RHall;
            if(id.StartsWith("arcade-",StringComparison.Ordinal))return id.Contains("-post-")?RPost:id.EndsWith("-roof",StringComparison.Ordinal)?RArcadeRoof:ROther;
            if(id.StartsWith("viaduct-pier",StringComparison.Ordinal)||id.StartsWith("signal-gantry-legs",StringComparison.Ordinal))return RPier;
            if(id=="viaduct-deck")return RViaduct;
            if(id.StartsWith("rake-",StringComparison.Ordinal)||id.StartsWith("train-car-",StringComparison.Ordinal))return RWagon;
            if(id.StartsWith("goods-shed-wall",StringComparison.Ordinal))return RWall;
            if(id=="carriage-shed"||id.StartsWith("roundhouse-",StringComparison.Ordinal))return RShed;
            if(id=="cutting-arch-roof")return RTunnel;
            if(id.StartsWith("n-lid-",StringComparison.Ordinal)||id.StartsWith("s-lid-",StringComparison.Ordinal)||id.StartsWith("w-lid-",StringComparison.Ordinal)||id=="goods-shed-roof"||id=="rotunda-dome"||
               id.StartsWith("shambles-jetty-",StringComparison.Ordinal)||id=="undercroft-ramp-lid")return RLid;
            if(id.StartsWith("barge-",StringComparison.Ordinal)||id=="coaling-tower"||id=="mound-keep"||id=="park-bandstand"||id.StartsWith("quay-crane-",StringComparison.Ordinal)||id=="signal-box"||id=="water-tower")return RLandmark;
            if(id.Contains("bridge")||id.StartsWith("skybridge-",StringComparison.Ordinal)||id.StartsWith("w-deck-",StringComparison.Ordinal)||id=="low-causeway"||id=="signal-gantry"||id.StartsWith("cutting-footbridge",StringComparison.Ordinal))return RBridge;
            return ROther;
        }
        /// <summary>Whether the art draws a role (and covers its blocks).</summary>
        public static bool Dressed(string role)=>role!=RHall&&role!=RCourt&&role!=RLandmark&&role!=RCamp&&role!=ROther;
        /// <summary>The sheet's lots of a layout: id to style and roof ("tw": tenements, walkable).</summary>
        public static Dictionary<string,string> Lots(uint layout)
        {
            var d=new Dictionary<string,string>(StringComparer.Ordinal);var s=SheetLots(layout);if(s==null)return d;
            foreach(var e in s.Split(';')){int k=e.IndexOf('=');if(k>0)d[e.Substring(0,k)]=e.Substring(k+1);}
            return d;
        }

        // ------------------------------------------------------------------------------------------------------------------
        // Hashing (FNV-1a and a mix): every seeded choice is a function of the layout, the run and a place's own key
        // ------------------------------------------------------------------------------------------------------------------
        public static uint Hash(string s,uint seed=2166136261u){uint h=seed^2166136261u;if(s!=null)foreach(char c in s){h^=c;h*=16777619u;}return Mix(h);}
        public static uint Mix(uint h){h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;h^=h>>16;return h;}
        public static uint Mix(uint a,uint b)=>Mix(a*0x9e3779b9u^b+0x632be5abu);
        public static float Unit(uint h)=>(Mix(h)&0xffffff)/16777216f;

        /// <summary>The module for a key at a place: the theme's variant where the bank has it (<see cref="ThemeAt"/>), else the key.</summary>
        public static string Pick(string key,V3 at,Func<string,bool> has)
        {
            var theme=ThemeAt?.Invoke(at);
            if(theme!=null&&has!=null){var themed=key+"@"+theme;if(has(themed))return themed;}
            return key;
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The solids: every block of the map in 8 m buckets (inside tests, floors under a point)
        // ------------------------------------------------------------------------------------------------------------------
        public sealed class Solids
        {
            public readonly float[] B;public readonly int Count;private readonly float minX,minZ;private readonly int nx,nz;private readonly List<int>[] cells;private const float Cell=8f;
            public Solids(ContentBlock[] blocks)
            {
                Count=blocks.Length;B=new float[Count*6];float x0=float.MaxValue,z0=float.MaxValue,x1=float.MinValue,z1=float.MinValue;
                for(int i=0;i<Count;i++)
                {
                    var b=blocks[i];float hx=b.size.x*.5f,hy=b.size.y*.5f,hz=b.size.z*.5f;
                    B[i*6]=b.pos.x-hx;B[i*6+1]=b.pos.x+hx;B[i*6+2]=b.pos.y-hy;B[i*6+3]=b.pos.y+hy;B[i*6+4]=b.pos.z-hz;B[i*6+5]=b.pos.z+hz;
                    x0=Math.Min(x0,B[i*6]);x1=Math.Max(x1,B[i*6+1]);z0=Math.Min(z0,B[i*6+4]);z1=Math.Max(z1,B[i*6+5]);
                }
                if(Count==0){x0=z0=0;x1=z1=1;}
                minX=x0-1;minZ=z0-1;nx=Math.Max(1,(int)Math.Ceiling((x1-minX+1)/Cell));nz=Math.Max(1,(int)Math.Ceiling((z1-minZ+1)/Cell));cells=new List<int>[nx*nz];
                for(int i=0;i<Count;i++)
                {
                    int ax=CX(B[i*6]),bx=CX(B[i*6+1]),az=CZ(B[i*6+4]),bz=CZ(B[i*6+5]);
                    for(int z=az;z<=bz;z++)for(int x=ax;x<=bx;x++)(cells[x+z*nx]??=new List<int>()).Add(i);
                }
            }
            private int CX(float x)=>Math.Max(0,Math.Min(nx-1,(int)Math.Floor((x-minX)/Cell)));
            private int CZ(float z)=>Math.Max(0,Math.Min(nz-1,(int)Math.Floor((z-minZ)/Cell)));
            /// <summary>Whether a point lies inside any block (more than eps inside it), other than `skip`.</summary>
            public bool Inside(float x,float y,float z,int skip=-1,float eps=.01f)
            {
                var c=cells[CX(x)+CZ(z)*nx];if(c==null)return false;
                foreach(int i in c){if(i==skip)continue;int o=i*6;if(x>B[o]+eps&&x<B[o+1]-eps&&y>B[o+2]+eps&&y<B[o+3]-eps&&z>B[o+4]+eps&&z<B[o+5]-eps)return true;}
                return false;
            }
            /// <summary>The highest block top at (x, z) at or below `below` (float.MinValue: none).</summary>
            public float Floor(float x,float z,float below)
            {
                var c=cells[CX(x)+CZ(z)*nx];float best=float.MinValue;if(c==null)return best;
                foreach(int i in c){int o=i*6;if(x>=B[o]&&x<=B[o+1]&&z>=B[o+4]&&z<=B[o+5]&&B[o+3]<=below+.02f&&B[o+3]>best)best=B[o+3];}
                return best;
            }
            /// <summary>The lowest block bottom at (x, z) above `above` (float.MaxValue: none): the headroom over a floor.</summary>
            public float Ceiling(float x,float z,float above)
            {
                var c=cells[CX(x)+CZ(z)*nx];float best=float.MaxValue;if(c==null)return best;
                foreach(int i in c){int o=i*6;if(x>=B[o]&&x<=B[o+1]&&z>=B[o+4]&&z<=B[o+5]&&B[o+2]>=above-.02f&&B[o+2]<best)best=B[o+2];}
                return best;
            }
        }

        // The six faces of a box: 0 +x, 1 -x, 2 +y, 3 -y, 4 +z, 5 -z.
        public static V3 NormalOf(int f)=>f==0?new V3(1,0,0):f==1?new V3(-1,0,0):f==2?new V3(0,1,0):f==3?new V3(0,-1,0):f==4?new V3(0,0,1):new V3(0,0,-1);
        /// <summary>A face's frame: its origin (a corner) and its two edges (along and up for a side; x and z for a top or bottom),
        /// wound so along x up is the face's outward normal (Unity's front faces).</summary>
        public static void FaceFrame(float[] b,int o,int f,out V3 origin,out V3 u,out V3 v)
        {
            float x0=b[o],x1=b[o+1],y0=b[o+2],y1=b[o+3],z0=b[o+4],z1=b[o+5];
            switch(f)
            {
                case 0:origin=new V3(x1,y0,z1);u=new V3(0,0,z0-z1);v=new V3(0,y1-y0,0);break;
                case 1:origin=new V3(x0,y0,z0);u=new V3(0,0,z1-z0);v=new V3(0,y1-y0,0);break;
                case 4:origin=new V3(x0,y0,z1);u=new V3(x1-x0,0,0);v=new V3(0,y1-y0,0);break;
                case 5:origin=new V3(x1,y0,z0);u=new V3(x0-x1,0,0);v=new V3(0,y1-y0,0);break;
                case 2:origin=new V3(x0,y1,z0);u=new V3(0,0,z1-z0);v=new V3(x1-x0,0,0);break;
                default:origin=new V3(x0,y0,z0);u=new V3(x1-x0,0,0);v=new V3(0,0,z1-z0);break;
            }
        }
        // Unity's front faces turn clockwise as seen from outside: for a side face (origin, u, v), u x v (left handed) is the
        // normal when u runs to the viewer's right. u = up x n for a facade (MeridianDressChecks holds the six frames to this).
        public static V3 Cross(V3 a,V3 b)=>new V3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
    }
}
