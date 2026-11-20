using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Belvedere Camp, the Expedition hub's life (W5 of docs/EXPEDITION-OVERHAUL.md; the owner's R6 and R10; docs/expedition/BELVEDERE-CAMP.md).
    /// Presentation data only: nothing here collides, spawns, pays or reaches the wire, and every figure and flame is derived from the
    /// camp's fixed frame and the run's seed, so each peer, a late joiner and the killcam draw the same camp.
    /// <list type="bullet">
    /// <item><b>Part 1, live:</b> the camp's light and life on the fence and overhead (plan Q19: until Astra's A1 builds the hub blocks,
    /// nothing stands on the plaza floor a pilot could walk through). Lamps on the fence and gates, masts clamped to the fence, bulb
    /// strings and sails from 3.5 m, half-drum fires against the fence's inner face within 0.25 m, the war board and the MISSING board
    /// flat on the fence, cables, the fuse board and the camp's sound. <see cref="Live"/>.</item>
    /// <item><b>Part 2, the DEV preview:</b> the whole W5 camp from the hub plan table (<see cref="Table"/>: A1's blocks, their art, the
    /// machines and the survivors at their posts), drawn only in the Developer walk, never in a live run. <see cref="Preview"/>.</item>
    /// </list>
    /// The fixed frame (Core/NativeLayeredCityWalk.cs:64-66, Core/NativeExpeditionGeneratedWorld.cs:86-98): the sanctuary is 49.6 m
    /// square at the origin, its fence sections 0.2 m thick with their inner faces at ±24.8 and 3.2 m tall, four 10 m metal gates on
    /// the axes whose leaves roll into pockets 5.1-10.08 m either side of a gate's centre under a top track at 3.3 m (Core
    /// NativeExpeditionHub.cs:174-180,299-303). The camp is the same on all five Meridian plans (their floor is flat at y 0).
    /// </summary>
    public static class NativeCamp
    {
        public const string Bank="ChipArenaCamp";
        // ---------------- the fixed frame ----------------
        public const float Inner=24.8f,Outer=25f,FenceTop=3.2f,GateHalf=5f,PocketTo=10.3f,PocketTop=3.4f,PocketInboard=.5f,TrackTop=3.36f;
        // ---------------- the art rules (the common brief; plan Q19; art bible 2.3 and 6.1) ----------------
        /// <summary>Art below <see cref="Overhead"/> lies within <see cref="WallReach"/> of a collider (the fence); the camp's strings and
        /// sails hang from <see cref="StringFloor"/>; a survivor's post leaves less than <see cref="Headroom"/> over the figure (the pilot
        /// capsule, Core/NativeLocomotionRules.cs:8).</summary>
        public const float WallReach=.25f,Overhead=3.1f,StringFloor=3.5f,Headroom=1.7f;
        // ---------------- the light (art bible 3.2: the camp is home) ----------------
        public const int BulbHex=0xFFC274,LanternHex=0xFFB054,FloodHex=0xFFD9A8,FireHex=0xFF7426,BoardLampHex=0xFFE0B0,SearchHex=0xFFE6C0;
        /// <summary>How near the war board's desk lamp clicks on (m, plan R6).</summary>
        public const float BoardLampReach=4f;

        // ---------------- the sides ----------------
        /// <summary>Side s (0 N, 1 E, 2 S, 3 W): the fence's outward normal.</summary>
        public static V3 Normal(int s)=>s==0?new V3(0,0,1):s==1?new V3(1,0,0):s==2?new V3(0,0,-1):new V3(-1,0,0);
        /// <summary>The viewer's right along side s, facing the fence from inside the camp.</summary>
        public static V3 Right(int s)=>s==0?new V3(1,0,0):s==1?new V3(0,0,-1):s==2?new V3(-1,0,0):new V3(0,0,1);
        /// <summary>The yaw (degrees, forward +z) that turns a fence prop's frame onto side s: its +z into the fence, +x the viewer's right.</summary>
        public static float Yaw(int s)=>s*90f;
        /// <summary>The point `a` m along side s's inner face (toward the viewer's right), `up` m high and `outward` m out of it.</summary>
        public static V3 OnFence(int s,float a,float up,float outward=0)=>Normal(s)*(Inner+outward)+Right(s)*a+new V3(0,up,0);
        /// <summary>Along-fence coordinate of world point (x, z) on side s.</summary>
        public static float Along(int s,float x,float z)=>V3.Dot(new V3(x,0,z),Right(s));

        /// <summary>Whether a world box (min, max) reaches into a gate's leaf travel, pockets or top track: 5-10.3 m either side of a
        /// gate along its fence, from the inner face to 0.5 m inboard, 0-3.4 m high (the clearance A1 holds every block to).</summary>
        public static bool InPocket(V3 lo,V3 hi)
        {
            if(lo.y>=PocketTop)return false;
            for(int s=0;s<4;s++)
            {
                var n=Normal(s);var r=Right(s);
                // depth range along n of the box, and along r
                float n0=Math.Min(V3.Dot(lo,n),V3.Dot(hi,n)),n1=Math.Max(V3.Dot(lo,n),V3.Dot(hi,n));
                float a0=Math.Min(V3.Dot(lo,r),V3.Dot(hi,r)),a1=Math.Max(V3.Dot(lo,r),V3.Dot(hi,r));
                if(n1<=Inner-PocketInboard||n0>=Inner)continue;
                foreach(int sign in new[]{-1,1})
                {
                    float p0=sign>0?GateHalf:-PocketTo,p1=sign>0?PocketTo:-GateHalf;
                    if(a1>p0&&a0<p1)return true;
                }
            }
            return false;
        }

        // =====================================================================================================================
        // The hub plan table (W5; the A1 hand-off of 2026-11-20, camp-astra-a1-paste.md): A1's blocks, axis-aligned, in metres.
        // Claude proposes it and checks it here (CampChecks); Astra owns it from A1 (NativeExpeditionHubPlan).
        // =====================================================================================================================
        public enum RowKind{Fountain,Canopy,CanopyPost,Bench,Kiosk,AwningPost,Infirmary,Lookout,LookoutCabin,Searchlight,Sentry,Radio,Supply,Generator,Sandbag}
        /// <summary>A row: its id, kind, centre (x, z), floor y0 and size (x × y × z).</summary>
        public readonly struct Row
        {
            public readonly string Id;public readonly RowKind Kind;public readonly float X,Z,Y0,Sx,Sy,Sz;
            public Row(string id,RowKind kind,float x,float z,float sx,float sy,float sz,float y0=0){Id=id;Kind=kind;X=x;Z=z;Y0=y0;Sx=sx;Sy=sy;Sz=sz;}
            public V3 Min=>new V3(X-Sx/2,Y0,Z-Sz/2);public V3 Max=>new V3(X+Sx/2,Y0+Sy,Z+Sz/2);public float Top=>Y0+Sy;
        }
        public static readonly Row[] Table=BuildTable();
        private static Row[] BuildTable()
        {
            var t=new List<Row>
            {
                new Row("camp-fountain",RowKind.Fountain,0,0,3.6f,.9f,3.6f),
                new Row("camp-canopy-n",RowKind.Canopy,0,3.4f,9.2f,.2f,2.4f,2.6f),new Row("camp-canopy-s",RowKind.Canopy,0,-3.4f,9.2f,.2f,2.4f,2.6f),
                new Row("camp-canopy-e",RowKind.Canopy,3.4f,0,2.4f,.2f,4.4f,2.6f),new Row("camp-canopy-w",RowKind.Canopy,-3.4f,0,2.4f,.2f,4.4f,2.6f),
                new Row("camp-canopy-post-0",RowKind.CanopyPost,4.4f,4.4f,.3f,2.6f,.3f),new Row("camp-canopy-post-1",RowKind.CanopyPost,-4.4f,4.4f,.3f,2.6f,.3f),
                new Row("camp-canopy-post-2",RowKind.CanopyPost,4.4f,-4.4f,.3f,2.6f,.3f),new Row("camp-canopy-post-3",RowKind.CanopyPost,-4.4f,-4.4f,.3f,2.6f,.3f),
                // the benches' long axis runs round the ring
                new Row("camp-bench-0",RowKind.Bench,0,3.4f,.9f,1.4f,.7f),new Row("camp-bench-1",RowKind.Bench,3.4f,0,.7f,1.4f,.9f),
                new Row("camp-bench-2",RowKind.Bench,0,-3.4f,.9f,1.4f,.7f),new Row("camp-bench-3",RowKind.Bench,-3.4f,0,.7f,1.4f,.9f),
                new Row("camp-kiosk",RowKind.Kiosk,16.4f,16.4f,2.6f,3.2f,2.6f),
                new Row("camp-awning-post-0",RowKind.AwningPost,14.6f,12.4f,.3f,3.4f,.3f),new Row("camp-awning-post-1",RowKind.AwningPost,12.4f,14.6f,.3f,3.4f,.3f),
                new Row("camp-infirmary",RowKind.Infirmary,-16.9f,-16.9f,3f,2.6f,3f),
                new Row("camp-lookout",RowKind.Lookout,13.5f,-13.5f,3f,9f,3f),
                new Row("camp-lookout-cabin",RowKind.LookoutCabin,13.5f,-13.5f,3.6f,2.6f,3.6f,9f),
                new Row("camp-searchlight",RowKind.Searchlight,13.5f,-13.5f,1.2f,1.2f,1.2f,11.6f),
                new Row("camp-sentry-n",RowKind.Sentry,-11.5f,23.8f,2f,5.2f,2f),new Row("camp-sentry-e",RowKind.Sentry,23.8f,11.5f,2f,5.2f,2f),
                new Row("camp-sentry-s",RowKind.Sentry,11.5f,-23.8f,2f,5.2f,2f),new Row("camp-sentry-w",RowKind.Sentry,-23.8f,-11.5f,2f,5.2f,2f),
                new Row("camp-radio",RowKind.Radio,13f,23.9f,4f,3.3f,1.8f),
                new Row("camp-supply",RowKind.Supply,-12.5f,-23.8f,3f,3.3f,2f),
                new Row("camp-generator",RowKind.Generator,24.3f,-12.5f,1f,1.4f,2f),
            };
            int k=0;
            foreach(int s in new[]{-1,1})foreach(int o in new[]{-1,1})
            {
                t.Add(new Row("camp-sandbag-"+(k++).ToString(System.Globalization.CultureInfo.InvariantCulture),RowKind.Sandbag,7.5f*o,20.5f*s,2.4f,1f,.6f));
                t.Add(new Row("camp-sandbag-"+(k++).ToString(System.Globalization.CultureInfo.InvariantCulture),RowKind.Sandbag,20.5f*s,7.5f*o,.6f,1f,2.4f));
            }
            return t.ToArray();
        }
        public static Row RowOf(string id){foreach(var r in Table)if(r.Id==id)return r;throw new ArgumentException("No camp row "+id);}
        /// <summary>The camp's two machines (A1's PveMachine records, plan R10): kind, place and yaw (radians, +z the front).</summary>
        public static readonly (string id,string kind,float x,float z,float yaw)[] Machines=
            {("exp-city:hub-machine:chip","chip",13.5f,13.5f,3.927f),("exp-city:hub-machine:med","med",-13.5f,-13.5f,.785f)};
        /// <summary>The flat boards on the north fence (art only, no block): x from, x to, y from, y to.</summary>
        public static readonly (float x0,float x1,float y0,float y1) WarBoard=(15.4f,19f,.85f,2.65f),MissingBoard=(-16.5f,-13f,.9f,2.55f);

        // =====================================================================================================================
        // The survivors' posts (plan R6; about 20: 13 in the camp, up to 5 rescued). Their figures are the preview's for now (the
        // bank's rigid figures); W5 brings the visual body near and keeps these beyond 30 m. Never replicated.
        // =====================================================================================================================
        public enum Cover{Glass,Mesh,Hatch,SeatedRoof,Machine}
        /// <summary>A post: who, the structure (row id) round them, where they stand (feet), facing (yaw degrees), the pose's bank key, how
        /// the post keeps pilots off them, and the instance whose clear brings them (null: in the camp from the start).</summary>
        public readonly struct Post
        {
            public readonly string Id,Role,Structure,Pose,Rescued;public readonly V3 At;public readonly float Yaw;public readonly Cover Cover;
            public Post(string id,string role,string structure,float x,float y,float z,float yaw,string pose,Cover cover,string rescued=null){Id=id;Role=role;Structure=structure;At=new V3(x,y,z);Yaw=yaw;Pose=pose;Cover=cover;Rescued=rescued;}
        }
        public static readonly Post[] Posts=
        {
            new Post("fire-0","survivor","camp-bench-0",0,0,3.4f,180,"seated-warm",Cover.SeatedRoof),
            new Post("fire-1","survivor","camp-bench-1",3.4f,0,0,270,"seated-mug",Cover.SeatedRoof),
            new Post("fire-2","survivor","camp-bench-2",0,0,-3.4f,0,"seated-lantern",Cover.SeatedRoof),
            new Post("quartermaster","Quartermaster","camp-kiosk",15.6f,0,16f,270,"hatch-lean",Cover.Hatch),
            new Post("doc","Doc","camp-infirmary",-16f,0,-16f,45,"hatch-clipboard",Cover.Hatch),
            new Post("radio","radio operator","camp-radio",12.1f,0,23.75f,180,"seated-radio",Cover.Glass),
            new Post("lookout-0","lookout","camp-lookout-cabin",12.5f,9f,-13.6f,270,"stand-binoculars",Cover.Glass),
            new Post("lookout-1","lookout","camp-lookout-cabin",14.2f,9f,-14.3f,180,"stand-lantern",Cover.Glass),
            new Post("sentry-n","sentry","camp-sentry-n",-11.5f,3f,24.1f,0,"sentry-rifle",Cover.Mesh),
            new Post("sentry-e","sentry","camp-sentry-e",24.1f,3f,11.5f,90,"sentry-rifle",Cover.Mesh),
            new Post("sentry-s","sentry","camp-sentry-s",11.5f,3f,-24.1f,180,"sentry-lamp",Cover.Mesh),
            new Post("sentry-w","sentry","camp-sentry-w",-24.1f,3f,-11.5f,270,"sentry-lamp",Cover.Mesh),
            new Post("sparks","Sparks, the mechanic","revive-o-matic",0,0,0,0,"stand-wrench",Cover.Machine),
            // the rescued, one per cleared instance, in the camp's own look (plan R6)
            new Post("nurse","nurse","camp-infirmary",-17.7f,0,-16.05f,0,"hatch-arms",Cover.Hatch,"exp-ward-zero"),
            new Post("clerk","ticket clerk","camp-radio",14.1f,0,23.75f,180,"seated-ledger",Cover.Glass,"exp-transit"),
            new Post("welder","welder","camp-kiosk",16.05f,0,15.6f,180,"hatch-welder",Cover.Hatch,"exp-foundry"),
            new Post("office","office worker","camp-supply",-12.5f,0,-23.45f,0,"stand-blanket",Cover.Glass,"exp-backrooms"),
            new Post("attendant","pool attendant","camp-bench-3",-3.4f,0,0,90,"seated-towel",Cover.SeatedRoof,"exp-poolrooms"),
        };
        /// <summary>The REVIVE-O-MATIC's spot in the generated hub (Core/NativeReviveMachine.cs:308): Sparks works inside its solids.</summary>
        public static V3 SparksAt()
        {
            NativeReviveMachine.Spot(NativeExpeditionGeneratedWorld.MapId,out var at,out float yaw);
            var p=System.Numerics.Vector3.Transform(new System.Numerics.Vector3(1.2f,0,1.45f),NativeReviveMachine.Frame(at,yaw));return new V3(p.X,p.Y,p.Z);
        }
        /// <summary>The headroom left over a figure at its post: from the top of the figure's pose to the underside of what roofs it
        /// (the canopy over a bench; the structure's own roof; a mesh cabin's). The pose heights are the bank's (CampChecks).</summary>
        public static float PoseHeight(string pose)=>pose.StartsWith("seated",StringComparison.Ordinal)?1.32f:1.74f;

        // =====================================================================================================================
        // Part 1: the live camp (fence, gates, overhead), the same on every plan; the run's seed varies only its small choices.
        // =====================================================================================================================
        public enum Kind{FenceLamp,SailMast,CornerMast,GateMast,Fire,WarBoard,WarLamp,Missing,Junction,Notice,Sign}
        /// <summary>A placed prop: its bank key, kind, place (3 × 4 rows: rotation then translation, Unity space), its envelope in its own
        /// frame (min, max), its base (the part below the gates' track, 3.4 m: what the wall and pocket rules hold; empty when min.y &gt;
        /// max.y) and, for a lamp or a fire, its light. CampChecks holds the bank's meshes to both boxes.</summary>
        public sealed class Piece
        {
            public string Key;public Kind Kind;public int Side=-1;public float[] Place=new float[12];public V3 Lo,Hi,BaseLo=new V3(0,1,0),BaseHi=new V3(0,0,0);
            public bool HasBase=>BaseLo.y<=BaseHi.y;
            public bool Lit;public V3 Light;public float Range;public int Hex;public float Gain=1;public int Seed;
            public V3 World(V3 p)=>new V3(Place[0]*p.x+Place[1]*p.y+Place[2]*p.z+Place[3],Place[4]*p.x+Place[5]*p.y+Place[6]*p.z+Place[7],Place[8]*p.x+Place[9]*p.y+Place[10]*p.z+Place[11]);
            /// <summary>The envelope's world box (axis-aligned round its turned corners).</summary>
            public void Bounds(out V3 lo,out V3 hi)=>Box(Lo,Hi,out lo,out hi);
            /// <summary>The base's world box.</summary>
            public void BaseBounds(out V3 lo,out V3 hi)=>Box(BaseLo,BaseHi,out lo,out hi);
            private void Box(V3 a,V3 b,out V3 lo,out V3 hi)
            {
                lo=new V3(float.MaxValue,float.MaxValue,float.MaxValue);hi=new V3(float.MinValue,float.MinValue,float.MinValue);
                for(int i=0;i<8;i++){var c=World(new V3((i&1)==0?a.x:b.x,(i&2)==0?a.y:b.y,(i&4)==0?a.z:b.z));lo=new V3(Math.Min(lo.x,c.x),Math.Min(lo.y,c.y),Math.Min(lo.z,c.z));hi=new V3(Math.Max(hi.x,c.x),Math.Max(hi.y,c.y),Math.Max(hi.z,c.z));}
            }
        }
        /// <summary>A bulb string: its wire from A to B with a catenary sag at mid span (m), a bulb every `Pitch` m.</summary>
        public sealed class Strand{public string Id;public V3 A,B;public float Sag,Pitch=.9f;public V3 At(float t){var p=A+(B-A)*t;p.y-=Sag*4*t*(1-t);return p;}public float Span=>(B-A).Length;}
        /// <summary>A shade sail: three tied corners, the cloth's belly (m) and its colour.</summary>
        public sealed class Sail{public string Id;public V3 A,B,C;public float Belly;public int Hex;}
        /// <summary>The live camp.</summary>
        public sealed class Layout
        {
            public readonly List<Piece> Pieces=new List<Piece>();public readonly List<Strand> Strands=new List<Strand>();public readonly List<Sail> Sails=new List<Sail>();
            /// <summary>The bulbs: their places (along every strand), whether each is lit (one in about 14 is dead, by the seed).</summary>
            public readonly List<V3> Bulbs=new List<V3>();public readonly List<bool> BulbLit=new List<bool>();
            /// <summary>The sound sources: key, place, gain, reach (m).</summary>
            public readonly List<(string key,V3 at,float gain,float reach)> Sounds=new List<(string,V3,float,float)>();
            public uint Seed;
        }
        // The masts, sails and lamps by side half (along-fence |a|): sail masts at 19.6, fence lamps and fires as below; each clear of
        // the gates' pockets (|a| 5-10.3) and of A1's rows on the fence (the sentries, the radio shack, the war and MISSING boards, the
        // supply cabin and the generator), so the live camp never moves when A1's blocks land.
        public const float SailMastA=19.6f,GateMastA=5.35f,SignHalf=2.05f;
        /// <summary>Fires: side, along (world x on N/S, world z on E/W).</summary>
        public static readonly (int side,float world)[] Fires={(0,-22f),(0,22f),(1,16.5f),(1,-16.8f),(2,22f),(2,-22f),(3,16.5f),(3,-16.8f)};
        /// <summary>Fence lamps: side, along (world).</summary>
        public static readonly (int side,float world)[] FenceLamps={(0,-14.75f),(1,22.3f),(1,-22.3f),(2,15.5f),(2,-16.6f),(3,22.3f),(3,-22.3f)};
        /// <summary>The fuse board on the east fence over A1's generator (z -12.5): the camp's cables start there, and its hum.</summary>
        public static readonly V3 JunctionAt=new V3(Inner,0,-12.5f);
        /// <summary>The camp PA on the north gate's east mast: the radio bed (64.7 FM) plays from it.</summary>
        public static readonly V3 RadioAt=new V3(GateMastA,5.55f,Inner+.1f);

        /// <summary>The along-fence `a` (toward the viewer's right on side s) of a world coordinate (x on N/S, z on E/W).</summary>
        public static float ToA(int s,float world)=>s==0?world:s==1?-world:s==2?-world:world;
        private static void Fence(Piece p,int s,float a,float outward=0)
        {
            p.Side=s;double r=Yaw(s)*Math.PI/180;float c=(float)Math.Cos(r),sn=(float)Math.Sin(r);var at=OnFence(s,a,0,outward);
            // rotation about y by yaw: x' = c x + s z, z' = -s x + c z (Unity's left-handed turn; forward +z goes to (sin, 0, cos))
            p.Place=new[]{c,0,sn,at.x,0,1,0,at.y,-sn,0,c,at.z};
        }
        private static Piece Add(Layout l,string key,Kind kind,int s,float a,V3 lo,V3 hi,float outward=0)
        {var p=new Piece{Key=key,Kind=kind,Lo=lo,Hi=hi};Fence(p,s,a,outward);l.Pieces.Add(p);return p;}
        private static Piece Base(Piece p,V3 lo,V3 hi){p.BaseLo=lo;p.BaseHi=hi;return p;}
        private static uint Mix(uint x){unchecked{x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x;}}
        /// <summary>A seeded unit value for (seed, a, b): deterministic on every peer.</summary>
        public static float Hash(uint seed,int a,int b=0){unchecked{return Mix(Mix(seed^0x51ED27u)+(uint)a*0x9E3779B9U+(uint)b*0x85EBCA6BU)/4294967296f;}}

        /// <summary>The live camp for a run (its world seed; the Developer walk's deal seed). Allocates; once per area load.</summary>
        public static Layout Live(uint seed)
        {
            var l=new Layout{Seed=seed};
            // ---- fence lamps: a caged bulkhead on a gooseneck over the fence top, its head at 3.7 m, 0.42 m inboard ----
            foreach(var (s,w) in FenceLamps)
            {
                var p=Base(Add(l,"camp/lamp/fence",Kind.FenceLamp,s,ToA(s,w),new V3(-.2f,2.85f,-.5f),new V3(.2f,4.05f,.22f)),new V3(-.12f,2.85f,-.06f),new V3(.12f,3.4f,.22f));
                p.Lit=true;p.Light=p.World(new V3(0,3.62f,-.42f));p.Range=9f;p.Hex=LanternHex;p.Gain=1.05f;p.Seed=(int)(Hash(seed,s*10+(int)w)*1000);
            }
            // ---- sail masts (clamped to the inner face, from the floor to 4.8 m), each with a small lamp at 4.4 m ----
            for(int s=0;s<4;s++)foreach(int sign in new[]{-1,1})
            {
                var p=Base(Add(l,"camp/mast/sail",Kind.SailMast,s,sign*SailMastA,new V3(-.12f,0,-.48f),new V3(.12f,4.85f,.05f)),new V3(-.1f,0,-.14f),new V3(.1f,3.4f,.02f));
                p.Lit=true;p.Light=p.World(new V3(0,4.32f,-.4f));p.Range=8f;p.Hex=LanternHex;p.Gain=.9f;p.Seed=s*2+(sign>0?1:0);
            }
            // ---- corner masts (in the fence's inner corner, from the floor to 7.2 m), a warm flood at 6.8 m toward the middle: corner c
            // (NE, SE, SW, NW) in the frame of side c, at its right end (the corner at local x 0, the other fence's face the plane x 0) ----
            for(int c=0;c<4;c++)
            {
                var p=Base(Add(l,"camp/mast/corner",Kind.CornerMast,c,Inner,new V3(-.75f,0,-.75f),new V3(.02f,7.3f,.02f)),new V3(-.23f,0,-.23f),new V3(0,3.4f,0));
                p.Lit=true;p.Light=p.World(new V3(-.45f,6.72f,-.45f));p.Range=16f;p.Hex=FloodHex;p.Gain=1.2f;
            }
            // ---- gate masts on the fence top at each opening's edges, a flood at 4.7 m down the lane; the north pair carries the PA ----
            for(int s=0;s<4;s++)foreach(int sign in new[]{-1,1})
            {
                var p=Base(Add(l,s==0&&sign>0?"camp/mast/gate-pa":"camp/mast/gate",Kind.GateMast,s,sign*GateMastA,new V3(-.32f,FenceTop,-.8f),new V3(.32f,6.3f,.22f)),new V3(-.16f,FenceTop,0),new V3(.16f,3.4f,.2f));
                p.Lit=true;p.Light=p.World(new V3(0,4.5f,-.55f));p.Range=13f;p.Hex=FloodHex;p.Gain=1.15f;
            }
            // ---- the fires: half drums against the inner face (0.245 m deep) ----
            for(int i=0;i<Fires.Length;i++)
            {
                var (s,w)=Fires[i];var p=Add(l,"camp/fire/drum",Kind.Fire,s,ToA(s,w),new V3(-.5f,0,-.245f),new V3(.5f,2.75f,.02f));Base(p,p.Lo,new V3(p.Hi.x,NativeCamp.PocketTop,p.Hi.z));
                p.Lit=true;p.Light=p.World(new V3(0,1.05f,-.14f));p.Range=7.5f;p.Hex=FireHex;p.Gain=1.3f;p.Seed=i;
            }
            // ---- the boards: flat on the north fence (6 cm at most), the war board's desk lamp on the fence top over it ----
            var war=Add(l,"camp/board/war",Kind.WarBoard,0,(WarBoard.x0+WarBoard.x1)/2,new V3(-1.82f,WarBoard.y0-.05f,-.058f),new V3(1.82f,WarBoard.y1+.06f,0));Base(war,war.Lo,war.Hi);
            var lamp=Base(Add(l,"camp/board/war-lamp",Kind.WarLamp,0,(WarBoard.x0+WarBoard.x1)/2,new V3(-.75f,2.9f,-.46f),new V3(.75f,3.75f,.22f)),new V3(-.62f,2.9f,-.06f),new V3(.62f,3.4f,.22f));
            lamp.Lit=true;lamp.Light=lamp.World(new V3(0,3.42f,-.34f));lamp.Range=5f;lamp.Hex=BoardLampHex;lamp.Gain=1f;
            var missing=Add(l,"camp/board/missing",Kind.Missing,0,(MissingBoard.x0+MissingBoard.x1)/2,new V3(-1.78f,MissingBoard.y0-.05f,-.058f),new V3(1.78f,MissingBoard.y1+.08f,0));Base(missing,missing.Lo,missing.Hi);
            // ---- the fuse board (over A1's generator), the camp's notices ----
            var box=Add(l,"camp/box/junction",Kind.Junction,1,ToA(1,JunctionAt.z),new V3(-.4f,1.6f,-.18f),new V3(.4f,3.27f,.22f));Base(box,box.Lo,box.Hi);
            foreach(var (s,w,key) in new[]{(1,20.4f,"camp/notice/curfew"),(3,-20.4f,"camp/notice/light"),(2,-18.4f,"camp/notice/water"),(3,18.4f,"camp/notice/radio")})
            {var n=Add(l,key,Kind.Notice,s,ToA(s,w),new V3(-.5f,1.1f,-.03f),new V3(.5f,2.3f,0));Base(n,n.Lo,n.Hi);}
            // ---- the gate signs, hung between each gate's masts at 4.35-5.05 m (BELVEDERE CAMP over the north gate) ----
            for(int s=0;s<4;s++)Add(l,s==0?"camp/sign/north":"camp/sign/gate-"+"NESW"[s],Kind.Sign,s,0,new V3(-2.2f,3.95f,-.12f),new V3(2.2f,5.0f,.12f),.1f);

            // ---- the strings (from 3.5 m; their lowest bulb hangs at least 4.3 m up) ----
            V3 Gate(int s,int sign,float up)=>OnFence(s,sign*GateMastA,up,.1f);
            V3 Corner(int c,float up){float x=(c<2?1:-1)*(Inner-.12f),z=(c==0||c==3?1:-1)*(Inner-.12f);return new V3(x,up,z);}
            // the long spans: north gate to south gate along x = ±5.35, east to west along z = ±5.35 (the "#" round the mess ring)
            foreach(int sign in new[]{-1,1})
            {
                l.Strands.Add(new Strand{Id="span-ns-"+(sign>0?"e":"w"),A=Gate(0,sign,6.05f),B=Gate(2,-sign,6.05f),Sag=1.15f});
                l.Strands.Add(new Strand{Id="span-ew-"+(sign>0?"s":"n"),A=Gate(1,sign,6.05f),B=Gate(3,-sign,6.05f),Sag=1.15f});
            }
            // the fence runs: each corner mast to the two gate masts beside it
            for(int c=0;c<4;c++)
            {
                int sx=c<2?1:-1,sz=c==0||c==3?1:-1;
                int sideZ=sz>0?0:2,sideX=sx>0?1:3;
                // the gate mast on side sideZ nearest this corner: along-fence a with the corner's x sign
                int signZ=Math.Sign(ToA(sideZ,sx*GateMastA)),signX=Math.Sign(ToA(sideX,sz*GateMastA));
                l.Strands.Add(new Strand{Id="run-"+c+"-z",A=Corner(c,7.0f),B=Gate(sideZ,signZ,5.95f),Sag=.55f});
                l.Strands.Add(new Strand{Id="run-"+c+"-x",A=Corner(c,7.0f),B=Gate(sideX,signX,5.95f),Sag=.55f});
            }
            // across each gate's opening, between its masts
            for(int s=0;s<4;s++)l.Strands.Add(new Strand{Id="gate-"+"nesw"[s],A=Gate(s,-1,5.05f),B=Gate(s,1,5.05f),Sag=.28f,Pitch=.7f});
            // ---- the sails across the corners: the corner mast at 6.4 m and the two sail masts at 4.6 m ----
            int[] sailHex={0x4A5A6E,0x6A6A3E,0x8A4A2A,0x3A7A7A};
            for(int c=0;c<4;c++)
            {
                int sx=c<2?1:-1,sz=c==0||c==3?1:-1;
                l.Sails.Add(new Sail{Id="sail-"+c,A=Corner(c,6.4f)-new V3(sx*.08f,0,sz*.08f),B=new V3(sx*SailMastA,4.62f,sz*(Inner-.08f)),C=new V3(sx*(Inner-.08f),4.62f,sz*SailMastA),Belly=.32f,Hex=sailHex[(c+(int)(Hash(seed,77)*4))%4]});
            }
            // ---- the bulbs: one in about fourteen dead (seeded), never two dark side by side ----
            bool lastDead=false;
            for(int i=0;i<l.Strands.Count;i++)
            {
                var st=l.Strands[i];int n=Math.Max(2,(int)Math.Round(st.Span/st.Pitch));
                for(int k=1;k<n;k++)
                {
                    float t=k/(float)n;
                    // a gate's string carries its sign at mid span: no bulbs over it
                    if(st.Id.StartsWith("gate-",StringComparison.Ordinal)&&Math.Abs(t-.5f)*st.Span<SignHalf+.2f)continue;
                    var b=st.At(t);b.y-=.11f;l.Bulbs.Add(b);bool dead=!lastDead&&Hash(seed,1000+i,k)<.07f;l.BulbLit.Add(!dead);lastDead=dead;
                }
            }
            // ---- the sound bed (docs/audio/SOUND-REQUESTS.md "The camp's sound bed"): each fire, the fuse board's hum, the PA's radio,
            // the murmur of people round the camp ----
            foreach(var p in l.Pieces)if(p.Kind==Kind.Fire)l.Sounds.Add(("camp.fire",p.Light,.55f,14f));
            l.Sounds.Add(("camp.generator",new V3(JunctionAt.x-.1f,1.2f,JunctionAt.z),.42f,18f));
            l.Sounds.Add(("camp.radio",RadioAt,.38f,26f));
            l.Sounds.Add(("camp.murmur",new V3(0,1.6f,0),.3f,34f));
            return l;
        }

        // =====================================================================================================================
        // Part 2: the DEV preview of the whole camp (the Developer walk only; never a live run).
        // =====================================================================================================================
        /// <summary>A preview piece: a bank key in a row's place (axis-aligned: centre on its floor, yaw degrees).</summary>
        public readonly struct Shown{public readonly string Key,Post;public readonly V3 At;public readonly float Yaw;public Shown(string key,V3 at,float yaw,string post=null){Key=key;At=at;Yaw=yaw;Post=post;}}
        /// <summary>The structures' art (one key per row kind; rows that share a model share a key), the survivors at their posts (the
        /// rescued only up to `rescued` of them) and the landing field's pods for the seed.</summary>
        public static List<Shown> Preview(uint seed,int rescued)
        {
            var o=new List<Shown>();
            o.Add(new Shown("camp/row/ring",new V3(0,0,0),0));
            o.Add(new Shown("camp/row/fountain",new V3(0,0,0),0));
            foreach(var r in Table)
            {
                switch(r.Kind)
                {
                    case RowKind.Bench:o.Add(new Shown("camp/row/bench",new V3(r.X,0,r.Z),r.Sx>r.Sz?(r.Z>0?180:0):(r.X>0?270:90)));break;
                    case RowKind.Kiosk:o.Add(new Shown("camp/row/kiosk",new V3(r.X,0,r.Z),0));break;
                    case RowKind.Infirmary:o.Add(new Shown("camp/row/infirmary",new V3(r.X,0,r.Z),0));break;
                    case RowKind.Lookout:o.Add(new Shown("camp/row/lookout",new V3(r.X,0,r.Z),0));break;
                    case RowKind.Sentry:{int s=r.Z>20?0:r.X>20?1:r.Z<-20?2:3;o.Add(new Shown("camp/row/sentry",new V3(r.X,0,r.Z),Yaw(s)));break;}
                    case RowKind.Radio:o.Add(new Shown("camp/row/radio",new V3(r.X,0,r.Z),0));break;
                    case RowKind.Supply:o.Add(new Shown("camp/row/supply",new V3(r.X,0,r.Z),180));break;
                    case RowKind.Generator:o.Add(new Shown("camp/row/generator",new V3(r.X,0,r.Z),90));break;
                    case RowKind.Sandbag:o.Add(new Shown("camp/row/sandbag",new V3(r.X,0,r.Z),r.Sx>r.Sz?0:90));break;
                }
            }
            bool attendant=false;
            foreach(var (p,at,yaw) in PreviewPosts(rescued))
            {
                if(p.Id=="attendant")attendant=true;
                int variant=(int)(Hash(seed,NativeExpeditionStates.StableHash(p.Id),7)*3);
                o.Add(new Shown("camp/figure/"+p.Pose+"/"+variant.ToString(System.Globalization.CultureInfo.InvariantCulture),at,yaw,p.Id));
            }
            // the fourth bench waits for a rescued survivor: a folded blanket, a crate and a lantern keep it until then
            if(!attendant){var b=RowOf("camp-bench-3");o.Add(new Shown("camp/row/bench-waiting",new V3(b.X,0,b.Z),90));}
            return o;
        }
        /// <summary>The posts the preview shows (the rescued only up to `rescued` of them), each where its survivor is (Sparks at the
        /// REVIVE-O-MATIC's works) and facing (degrees).</summary>
        public static IEnumerable<(Post post,V3 at,float yaw)> PreviewPosts(int rescued)
        {
            int joined=0;
            foreach(var p in Posts)
            {
                if(p.Rescued!=null&&joined++>=rescued)continue;
                yield return (p,p.Id=="sparks"?SparksAt():p.At,p.Id=="sparks"?225:p.Yaw);
            }
        }
    }
}
