using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    public static partial class NativeMeridianStreetLife
    {
        /// <summary>What the planner reads: the map's blocks and ramps, the layout (the plan's seed), the run's seed, the routes (north
        /// to the final court, then east, south and west to the dealt halls), the hall deal, the caches' keep-outs, and, where they
        /// draw, the street dress's plan (its modules, lamps, plates and paint), the bleed and its plan, and the landmarks' layout:
        /// what they already put on the streets, this keeps off. Each may be null (the piece is planned as if it were not there).</summary>
        public sealed class Input
        {
            public ContentMap Map;public uint Layout,Run;public NativeMeridianDress.Route[] Routes;public string[] Slots;
            public List<NativeCityBleedDress.KeepOut> KeepOut;
            public NativeMeridianDress.Plan Dress;
            public NativeCityBleed Bleed;public NativeCityBleedDress.Plan BleedPlan;
            public NativeMeridianLandmarks.Layout Landmarks;
        }

        // A ground-storey bay of a wall on a street: its foot on the collider face at the floor, the face's way out and along (the
        // viewer's left), its width, floor and the wall's height over it, its block, face and bay, the district (the lot's style; c
        // for a retaining wall, a shed or the Cordon: plain flat walls), the dress's ground module there (null: none, or flat), the
        // street in front (open: the pavement clear 2.4 m out), the facing wall's distance (99: none in 40 m), and the nearest route.
        private sealed class Slot
        {
            public V3 Foot,N,Along;public float W,Floor,Bottom,Top,Gap,Station=-1;public int Block,Face,Bay,Bays,Route=-1;public char Style;public string Key;public bool Open,Flat,Piped,Busy;
            public int Storeys;public string[] Uppers;
        }
        // A floor cell (2 m, on a floor's top): its centre, the block under it, whether it is a roof, the pavement in front of a wall
        // (and that wall's way out), its district, the route it lies on (-1: none) and that route's metres from its gate.
        private sealed class FloorCell
        {
            public V3 At;public int Block;public bool Roof,Pavement;public V3 WallN;public char District='o';public int Route=-1;public float Station;
        }
        private sealed class World
        {
            public Input In;public ContentBlock[] Blocks;public NativeMeridianDress.Solids S;public float[] B;public Dictionary<string,string> Lots;
            public string[] Roles;public bool[] Left;public bool[] Tread;public uint LSeed,RSeed;
            public readonly List<MapRamp> Ramps=new List<MapRamp>();
            // keep-outs: round ones (landmarks' features, door fronts), the boxes the other drawers' floor paint and fittings take
            public readonly List<(V3 c,float r)> Rounds=new List<(V3,float)>();
            public readonly Boxes Paint=new Boxes(),Fixtures=new Boxes(),Mine=new Boxes(),MineFloor=new Boxes();
            // the dress's modules by bay: ground (block, face, bay start in dm) and uppers (with the storey floor in dm)
            public readonly Dictionary<long,string> Ground=new Dictionary<long,string>(),Upper=new Dictionary<long,string>();
            public readonly Dictionary<long,NativeCityBleed.Sample> Bleed=new Dictionary<long,NativeCityBleed.Sample>();
            public readonly List<(V3 a,V3 b,float width,string kind,int route,float station)> Legs=new List<(V3,V3,float,string,int,float)>();
            public readonly Dictionary<long,List<int>> SlotHash=new Dictionary<long,List<int>>();
            public List<Slot> Slots;
            public readonly List<int> NearList=new List<int>();
        }
        /// <summary>Axis-aligned boxes in a 4 m hash (the keep-outs and the plan's own pieces, so paint never lies on paint).</summary>
        private sealed class Boxes
        {
            private readonly List<(V3 lo,V3 hi)> list=new List<(V3,V3)>();private readonly Dictionary<long,List<int>> hash=new Dictionary<long,List<int>>();
            private static long Key(int x,int z)=>((long)x<<32)^(uint)z;
            public void Add(V3 lo,V3 hi)
            {
                int i=list.Count;list.Add((lo,hi));
                for(int z=(int)Math.Floor(lo.z/4);z<=(int)Math.Floor(hi.z/4);z++)for(int x=(int)Math.Floor(lo.x/4);x<=(int)Math.Floor(hi.x/4);x++)
                {if(!hash.TryGetValue(Key(x,z),out var l))hash[Key(x,z)]=l=new List<int>();l.Add(i);}
            }
            /// <summary>Whether a box overlaps one already here by more than `by` m on every axis (vertical too: `dy`).</summary>
            public bool Hits(V3 lo,V3 hi,float by=.01f,float dy=.01f)
            {
                for(int z=(int)Math.Floor(lo.z/4);z<=(int)Math.Floor(hi.z/4);z++)for(int x=(int)Math.Floor(lo.x/4);x<=(int)Math.Floor(hi.x/4);x++)
                {
                    if(!hash.TryGetValue(Key(x,z),out var l))continue;
                    foreach(int i in l){var (a,b)=list[i];if(Math.Min(hi.x,b.x)-Math.Max(lo.x,a.x)>by&&Math.Min(hi.z,b.z)-Math.Max(lo.z,a.z)>by&&Math.Min(hi.y,b.y)-Math.Max(lo.y,a.y)>-dy)return true;}
                }
                return false;
            }
            public int Count=>list.Count;
        }

        private static float Rand(uint seed,uint salt,int i)=>(NativeMeridianDress.Mix(seed^salt*0x9E3779B9u^(uint)i*0x85EBCA6Bu)&0xFFFFFF)/16777216f;
        private static uint Hash2(uint a,uint b)=>NativeMeridianDress.Mix(a,b);
        private static uint PlaceHash(V3 p)=>NativeMeridianDress.Mix((uint)(int)Math.Floor(p.x*4)*73856093u^(uint)(int)Math.Floor(p.z*4)*19349663u^(uint)(int)Math.Round(p.y*4)*83492791u);
        // A seeded field over `size` m cells: litter gathers in drifts, not salt and pepper.
        private static float Clump(uint seed,V3 p,float size)=>(NativeMeridianDress.Mix(seed^NativeCityBleed.Cell(p,size))&1023)/1023f;

        /// <summary>The input for a city from its records (the Developer walk's or the live run's: the same blocks, routes, caches and
        /// deal), with the other drawers' plans where they draw (null where they do not) and the landmarks placed as their drawer
        /// places them.</summary>
        public static Input InputOf(NativeLayeredCityWalk city,uint layout,uint run,string[] slots,NativeMeridianDress.Plan dress,NativeCityBleed bleed,NativeCityBleedDress.Plan bleedPlan)
        {
            if(city==null)throw new ArgumentNullException(nameof(city));
            var routes=new NativeMeridianDress.Route[city.Routes.Count];var doors=new V3[city.Routes.Count];
            for(int i=0;i<routes.Length;i++){var r=city.Routes[i];routes[i]=new NativeMeridianDress.Route{Name=r.Name,Points=r.Points,Widths=r.Widths,Kinds=r.Kinds};doors[i]=r.Destination;}
            NativeMeridianLandmarks.Layout landmarks=null;
            if(slots!=null&&slots.Length==doors.Length)landmarks=NativeMeridianLandmarks.Place(layout,city.Map,doors,slots,1f);
            return new Input{Map=city.Map,Layout=layout,Run=run,Routes=routes,Slots=slots,KeepOut=NativeCityBleedDress.CacheKeepOuts(city.Caches,city.Map),
                Dress=dress,Bleed=bleedPlan!=null?bleed:null,BleedPlan=bleedPlan,Landmarks=landmarks};
        }

        /// <summary>The street life for a city. Pure and deterministic for the same input.</summary>
        public static Plan Build(Input input)
        {
            if(input?.Map?.blocks==null)throw new ArgumentException("No map",nameof(input));
            var w=WorldOf(input);
            var mb=input.Map.bounds;
            var plan=new Plan{Layout=input.Layout,Run=input.Run,MinX=(float)Math.Floor(mb.minX)-CullCell,MinZ=(float)Math.Floor(mb.minZ)-CullCell};
            plan.Nx=(int)Math.Ceiling((mb.maxX+CullCell-plan.MinX)/CullCell);plan.Nz=(int)Math.Ceiling((mb.maxZ+CullCell-plan.MinZ)/CullCell);
            w.Slots=Bays(w);plan.Bays=w.Slots.Count;
            var cells=FloorCells(w);plan.FloorCells=cells.Count;
            // the city's fittings first (the layout's), then the run's story round them
            Markings(w,plan,cells);
            Precinct(w,plan,cells);
            Fittings(w,plan);
            Spans(w,plan);
            WallStory(w,plan);
            Scatter(w,plan,cells);
            Trail(w,plan);
            Index(plan);
            return plan;
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The world: the blocks' roles, the dress's modules and fittings, the other drawers' keep-outs, the route legs
        // ------------------------------------------------------------------------------------------------------------------
        private static World WorldOf(Input input)
        {
            var map=input.Map;var w=new World{In=input,Blocks=map.blocks,S=new NativeMeridianDress.Solids(map.blocks),Lots=NativeMeridianDress.Lots(input.Layout)};
            w.B=w.S.B;w.LSeed=NativeMeridianDress.Mix(input.Layout,0x5bd1e995u);w.RSeed=NativeMeridianDress.Mix(w.LSeed,input.Run^0x27d4eb2fu);
            int n=map.blocks.Length;w.Roles=new string[n];w.Left=new bool[n];w.Tread=new bool[n];
            var treads=new HashSet<string>(StringComparer.Ordinal);
            foreach(var r in map.ramps??Array.Empty<MapRamp>()){w.Ramps.Add(r);if(r.blocks!=null)foreach(var b in r.blocks)treads.Add(b);}
            var left=NativeMeridianLandmarks.FixedCovers(input.Layout,map);
            for(int i=0;i<n;i++){var id=map.blocks[i].id;w.Left[i]=id!=null&&left.Contains(id);w.Tread[i]=id!=null&&treads.Contains(id);w.Roles[i]=NativeMeridianDress.RoleOf(id,w.Lots);}
            // the dress: its facade modules by bay, its lamps, wires, plates, clock and siren (fittings), its paint (floor)
            var d=input.Dress;
            if(d!=null)
            {
                foreach(var p in d.Placements)
                {
                    var at=new V3(p.M[3],p.M[7],p.M[11]);
                    if(p.Block>=0&&p.Face>=0&&p.Face!=2&&p.Face!=3)
                    {
                        if(Math.Abs(p.V0-w.B[p.Block*6+2])<.01f)w.Ground[BayKey(p.Block,p.Face,p.U0,0)]=p.Key;
                        else w.Upper[BayKey(p.Block,p.Face,p.U0,p.V0)]=p.Key;
                        // an oriel stands 0.6 m out over two storeys: a fitting nothing else may enter
                        if(p.Key.StartsWith("u.oriel",StringComparison.Ordinal)){var n0=new V3(p.M[2],p.M[6],p.M[10]);var al=new V3(p.M[0],p.M[4],p.M[8]).Normalized;Fix(w,at+al*-1.3f,at+al*1.3f+n0*.7f+new V3(0,6.2f,0));}
                        continue;
                    }
                    if(p.Paint&&p.Block<0){float r=p.Key.StartsWith("paint.sanctuary",StringComparison.Ordinal)?2.4f:p.Key.StartsWith("puddle.",StringComparison.Ordinal)?1.4f:p.Key.StartsWith("paint.",StringComparison.Ordinal)?.7f:1f;
                        w.Paint.Add(new V3(at.x-r,at.y-.1f,at.z-r),new V3(at.x+r,at.y+.2f,at.z+r));continue;}
                    if(p.Key=="roof.hatch"){w.Paint.Add(new V3(at.x-1,at.y-.1f,at.z-1),new V3(at.x+1,at.y+.3f,at.z+1));continue;}
                    if(p.Key=="lamp.wire")
                    {
                        var dir=new V3(p.M[0],p.M[4],p.M[8]);var end=at+dir;Fix(w,new V3(Math.Min(at.x,end.x),at.y-1.4f,Math.Min(at.z,end.z)),new V3(Math.Max(at.x,end.x),at.y+.4f,Math.Max(at.z,end.z)),.35f);continue;
                    }
                    if(p.Block<0&&p.Face<0)
                    {
                        // a lamp, a plate, the clock, the siren: a box round it, out from its wall
                        var nn=new V3(p.M[2],p.M[6],p.M[10]);float out_=p.Key.StartsWith("cd.siren",StringComparison.Ordinal)?1.6f:p.Key.StartsWith("lamp.",StringComparison.Ordinal)?1.3f:.9f;
                        var tip=at+nn*out_;Fix(w,new V3(Math.Min(at.x,tip.x),at.y-.9f,Math.Min(at.z,tip.z)),new V3(Math.Max(at.x,tip.x),at.y+.8f,Math.Max(at.z,tip.z)),.6f);
                    }
                }
                foreach(var l in d.Lamps)
                {
                    var a=l.Mount;var b=l.Bowl;Fix(w,new V3(Math.Min(a.x,b.x),Math.Min(a.y,b.y)-.5f,Math.Min(a.z,b.z)),new V3(Math.Max(a.x,b.x),Math.Max(a.y,b.y)+.5f,Math.Max(a.z,b.z)),.5f);
                }
            }
            // the bleed's pieces: its paint on the floor; everything else a fitting
            if(input.BleedPlan!=null)
            {
                foreach(var p in input.BleedPlan.Pieces)
                {
                    NativeCityBleedDress.Bounds(p,out var lo,out var hi);var k=NativeCityBleedDress.Kinds[p.Kind];
                    if(k.Mount==NativeCityBleedDress.Mount.Floor)w.Paint.Add(lo,hi);else Fix(w,lo,hi,.15f);
                }
                foreach(var j in input.BleedPlan.Joins)w.Rounds.Add((j,NativeCityBleedDress.JoinKeep+1f));
            }
            // the door fronts (D2's door front and the bleed's join circle) and the final court's gateway: the route's last point
            foreach(var r in input.Routes??Array.Empty<NativeMeridianDress.Route>())
                if(r?.Points!=null&&r.Points.Length>0)w.Rounds.Add((r.Points[r.Points.Length-1],NativeMeridianLandmarks.ApronDepth+5f));
            // the landmarks: each piece's features (reads) and its place, but the far layer (beyond the map)
            if(input.Landmarks!=null)foreach(var p in input.Landmarks.Pieces)
            {
                if(p.Kind==NativeMeridianLandmarks.Kind.Far||p.Kind==NativeMeridianLandmarks.Kind.Mast)continue;
                float r=p.Kind==NativeMeridianLandmarks.Kind.Court?24f:p.Kind==NativeMeridianLandmarks.Kind.Exit?7f:9f;w.Rounds.Add((p.At,r));
                foreach(var rd in p.Reads)w.Rounds.Add((rd.At,Math.Max(4f,rd.Size*.8f)));
            }
            // the routes' legs, with their stations from the gate
            var routes=input.Routes??Array.Empty<NativeMeridianDress.Route>();
            for(int ri=0;ri<routes.Length;ri++)
            {
                var R=routes[ri];if(R?.Points==null)continue;float st=0;
                for(int i=0;i+1<R.Points.Length;i++)
                {
                    var a=R.Points[i];var b=R.Points[i+1];float len=new V3(b.x-a.x,0,b.z-a.z).Length;
                    string kind=R.Kinds!=null&&i<R.Kinds.Length?R.Kinds[i]:"flat";float width=R.Widths!=null&&i<R.Widths.Length?R.Widths[i]:10;
                    w.Legs.Add((a,b,width,kind,ri,st));st+=len;
                }
            }
            return w;
        }
        private static long BayKey(int block,int face,float u0,float v0)=>((long)block<<34)^((long)face<<30)^((long)(int)Math.Round(u0*10)<<15)^(uint)(int)Math.Round(v0*10)&0x7fff;
        private static void Fix(World w,V3 lo,V3 hi,float pad=0){w.Fixtures.Add(new V3(lo.x-pad,lo.y-pad,lo.z-pad),new V3(hi.x+pad,hi.y+pad,hi.z+pad));}

        /// <summary>The bleed at a place, cached on a 4 m grid (the share moves at most 0.08 across a cell).</summary>
        private static NativeCityBleed.Sample BleedAt(World w,V3 p)
        {
            if(w.In.Bleed==null)return new NativeCityBleed.Sample{Door=-1};
            long key=((long)(int)Math.Floor(p.x/4)<<40)^((long)(int)Math.Floor(p.z/4)<<16)^(uint)((int)Math.Round(p.y)&0xffff);
            if(!w.Bleed.TryGetValue(key,out var s)){s=w.In.Bleed.At(new V3((float)Math.Floor(p.x/4)*4+2,p.y,(float)Math.Floor(p.z/4)*4+2));w.Bleed[key]=s;}
            return s;
        }
        /// <summary>The instance theme whose rumour band a place lies in (null: none, or the final court's).</summary>
        private static string RumourAt(World w,V3 p)
        {
            var s=BleedAt(w,p);if(s.Door<0||s.Band!=NativeCityBleed.Bands.Rumour)return null;
            var t=NativeCityBleed.Theme(w.In.Bleed.Doors[s.Door].Instance);return t=="final"?null:t;
        }

        // ------------------------------------------------------------------------------------------------------------------
        // Geometry against the map's boxes
        // ------------------------------------------------------------------------------------------------------------------
        private static List<int> Near(World w,float x0,float x1,float z0,float z1){w.NearList.Clear();w.NearList.AddRange(w.S.Near(x0,x1,z0,z1));return w.NearList;}
        /// <summary>Whether no block enters a box (by more than 1 mm).</summary>
        private static bool BoxClear(World w,V3 lo,V3 hi,int skip=-1)
        {
            var B=w.B;
            foreach(int i in w.S.Near(lo.x,hi.x,lo.z,hi.z))
            {
                if(i==skip)continue;int o=i*6;
                if(hi.x>B[o]+1e-3f&&lo.x<B[o+1]-1e-3f&&hi.y>B[o+2]+1e-3f&&lo.y<B[o+3]-1e-3f&&hi.z>B[o+4]+1e-3f&&lo.z<B[o+5]-1e-3f)return false;
            }
            return true;
        }
        /// <summary>The first block face a horizontal ray meets within `reach`: the distance, the block and its face's normal.</summary>
        private static bool Ray(World w,V3 o,V3 dir,float reach,out int block,out float t,out V3 normal)
        {
            var e=o+dir*reach;var list=Near(w,Math.Min(o.x,e.x),Math.Max(o.x,e.x),Math.Min(o.z,e.z),Math.Max(o.z,e.z));
            return NativeCityBleedDress.Ray(w.Blocks,list,o,dir,reach,out block,out t,out normal);
        }
        /// <summary>Whether a floor point lies on a floor's top at y (within 1 cm), with nothing standing on it.</summary>
        private static bool OnTop(World w,float x,float z,float y)=>Math.Abs(w.S.Floor(x,z,y+.02f)-y)<.01f&&!w.S.Inside(x,y+.08f,z,-1,.005f);
        /// <summary>Whether a floor piece's footprint lies wholly on a floor at its height: its corners, edges' middles and centre on a
        /// top, nothing standing on them, none on a ramp's wedge, a landmark's block or a tread.</summary>
        private static bool Supported(World w,in Piece p)
        {
            var k=Kinds[p.Kind];float y=p.At.y;
            foreach(float u in new[]{k.Min.x*.98f,0,k.Max.x*.98f})foreach(float v in new[]{k.Min.z*.98f,0,k.Max.z*.98f})
            {
                var c=Local(p,new V3(u,0,v));if(!OnTop(w,c.x,c.z,y))return false;
                if(OnRamp(w,c.x,c.z,y,.3f))return false;
                // the block under it: never a landmark's, a tread, the camp's or the Cordon's
                int under=BlockUnder(w,c.x,c.z,y);if(under<0||w.Left[under]||w.Tread[under])return false;var role=w.Roles[under];
                if(role==NativeMeridianDress.RCamp||role==NativeMeridianDress.RCordon||role==NativeMeridianDress.RTower||role==NativeMeridianDress.RHall||role==NativeMeridianDress.RCourt||role==NativeMeridianDress.RLandmark)return false;
            }
            return true;
        }
        private static int BlockUnder(World w,float x,float z,float y)
        {
            var B=w.B;foreach(int i in w.S.Near(x,x,z,z)){int o=i*6;if(x>=B[o]-1e-3f&&x<=B[o+1]+1e-3f&&z>=B[o+4]-1e-3f&&z<=B[o+5]+1e-3f&&Math.Abs(B[o+3]-y)<.011f)return i;}
            return -1;
        }
        private static bool OnRamp(World w,float x,float z,float y,float pad)
        {
            foreach(var r in w.Ramps)
            {
                var a=r.low;var b=r.high;float wx=a.x==b.x?r.width*.5f:0,wz=a.z==b.z?r.width*.5f:0;
                if(x>=Math.Min(a.x,b.x)-wx-pad&&x<=Math.Max(a.x,b.x)+wx+pad&&z>=Math.Min(a.z,b.z)-wz-pad&&z<=Math.Max(a.z,b.z)+wz+pad&&y>r.bottom-.3f&&y<r.high.y+.3f)return true;
            }
            return false;
        }
        /// <summary>Whether a face's line (a to b, horizontal) comes within `pad` m of a ramp's footprint (a stair, a sloped street, a
        /// fire escape) between heights y0 and y1.</summary>
        private static bool NearFlight(World w,V3 a,V3 b,float y0,float y1,float pad)
        {
            float x0=Math.Min(a.x,b.x)-pad,x1=Math.Max(a.x,b.x)+pad,z0=Math.Min(a.z,b.z)-pad,z1=Math.Max(a.z,b.z)+pad;
            foreach(var r in w.Ramps)
            {
                var l=r.low;var h=r.high;float wx=l.x==h.x?r.width*.5f:0,wz=l.z==h.z?r.width*.5f:0;
                if(Math.Max(l.x,h.x)+wx<x0||Math.Min(l.x,h.x)-wx>x1||Math.Max(l.z,h.z)+wz<z0||Math.Min(l.z,h.z)-wz>z1)continue;
                if(r.bottom>y1||h.y+.3f<y0)continue;
                return true;
            }
            return false;
        }
        /// <summary>The floor under a point (the highest top at or below it; -1e9: none).</summary>
        private static float FloorUnder(World w,float x,float z,float y){float f=w.S.Floor(x,z,y);return f<-1e5f?-1e9f:f;}
        /// <summary>Whether everything a piece's box covers stands at least `clear` m over the floor under it (sampled on a grid).</summary>
        private static bool Overhead(World w,V3 lo,V3 hi,float clear)
        {
            int nx=Math.Max(2,(int)Math.Ceiling((hi.x-lo.x)/.75f)),nz=Math.Max(2,(int)Math.Ceiling((hi.z-lo.z)/.75f));
            for(int i=0;i<=nx;i++)for(int j=0;j<=nz;j++)
            {
                float x=lo.x+(hi.x-lo.x)*i/nx,z=lo.z+(hi.z-lo.z)*j/nz;float f=FloorUnder(w,x,z,lo.y-.01f);
                if(f>-1e8f&&lo.y-f<clear)return false;
            }
            return true;
        }
        /// <summary>Whether a wall piece's face rectangle (its envelope's x and y at the face) lies on its block's face, continuous and
        /// in sight: 0.05 m out is free and 0.05 m in is the block, at its corners and middle.</summary>
        private static bool OnFace(World w,in Piece p,int block)
        {
            var k=Kinds[p.Kind];float y0=k.Min.y*p.Scale.y,y1=k.Max.y*p.Scale.y;
            foreach(float u in new[]{k.Min.x*.96f,0,k.Max.x*.96f})foreach(float v in new[]{y0+.04f,(y0+y1)*.5f,y1-.04f})
            {
                var o=Local(p,new V3(u,0,.05f/p.Scale.z));o=new V3(o.x,p.At.y+v,o.z);var i=Local(p,new V3(u,0,-.05f/p.Scale.z));i=new V3(i.x,p.At.y+v,i.z);
                if(w.S.Inside(o.x,o.y,o.z,-1,.005f))return false;
                int o6=block*6;var B=w.B;if(!(i.x>B[o6]&&i.x<B[o6+1]&&i.y>B[o6+2]&&i.y<B[o6+3]&&i.z>B[o6+4]&&i.z<B[o6+5]))return false;
            }
            return true;
        }

        // ------------------------------------------------------------------------------------------------------------------
        // Adding a piece: every mount's rule, the keep-outs, paint never on paint
        // ------------------------------------------------------------------------------------------------------------------
        private static bool Add(World w,Plan plan,string key,V3 at,float yaw,V3 scale,Life life,uint seed,char district,int block=-1,uint tint=0,bool onSpan=false)
        {
            int k=KindOf(key);if(k<0)throw new InvalidOperationException("No street kind "+key);var K=Kinds[k];
            var p=new Piece{Kind=k,At=at,Yaw=yaw,Scale=scale,Life=life,Tint=tint,Seed=seed,District=district,Block=block};
            Bounds(p,out var lo,out var hi);
            int why=KeptOut(w,p,lo,hi);if(why>=0){plan.KeptOut++;plan.Refused[why]++;return false;}
            switch(K.Mount)
            {
                case Mount.Floor:
                    if(w.MineFloor.Hits(lo,hi,.01f,.05f)){plan.Refused[4]++;return false;}
                    if(w.Paint.Hits(lo,hi,.01f,.1f)){plan.Refused[5]++;return false;}
                    if(!Supported(w,p)){plan.Refused[6]++;return false;}
                    w.MineFloor.Add(lo,hi);break;
                case Mount.Wall:
                case Mount.Low:
                {
                    if(block<0||!OnFace(w,p,block)){plan.Refused[7]++;return false;}
                    // nothing else stands in its box (a rail, a post, a tread; the dress's and the bleed's fittings; the plan's own)
                    var ins=new V3(.012f,.012f,.012f);if(!BoxClear(w,lo+ins,hi-ins,block)){plan.Refused[8]++;return false;}
                    if(w.Fixtures.Hits(lo,hi,.02f,.02f)){plan.Refused[9]++;return false;}
                    if(w.Mine.Hits(lo,hi,.02f,.02f)){plan.Refused[10]++;return false;}
                    // a low piece stands on the floor in front of its wall
                    if(K.Mount==Mount.Low&&!K.Stretch){var f=Local(p,new V3(0,0,(K.Min.z+K.Max.z)*.5f));if(!OnTop(w,f.x,f.z,at.y)){plan.Refused[11]++;return false;}}
                    w.Mine.Add(lo,hi);break;
                }
                default:
                {
                    var ins=new V3(.015f,.015f,.015f);
                    if(K.Mount==Mount.Span){var a=Local(p,new V3(K.Min.x,0,0));var e=Local(p,new V3(K.Max.x,0,0));if(Math.Abs(a.x-e.x)>Math.Abs(a.z-e.z)){ins.x=.06f;}else{ins.z=.06f;}}
                    if(!BoxClear(w,lo+ins,hi-ins,block)){plan.Refused[8]++;return false;}
                    if(!Overhead(w,lo+ins,hi-ins,Clear)){plan.Refused[12]++;return false;}
                    if(w.Fixtures.Hits(lo,hi,.02f,.02f)){plan.Refused[9]++;return false;}
                    if(!onSpan&&w.Mine.Hits(lo,hi,.05f,.05f)){plan.Refused[10]++;return false;}
                    w.Mine.Add(lo,hi);break;
                }
            }
            plan.Pieces.Add(p);return true;
        }
        /// <summary>Which keep-out a piece's box enters (-1: none; else its index in <see cref="Refusals"/>).</summary>
        private static int KeptOut(World w,in Piece p,V3 lo,V3 hi)
        {
            var K=Kinds[p.Kind];
            // the camp: nothing inside its fence (its floor, fence and gates are Belvedere Camp's own)
            if(lo.x<25.8f&&hi.x>-25.8f&&lo.z<25.8f&&hi.z>-25.8f&&lo.y<9f)return 0;
            // the caches: flush wall art keeps only off the locker itself; everything else out of every keep-out
            bool flush=K.Mount==Mount.Wall;
            if(w.In.KeepOut!=null)foreach(var ko in w.In.KeepOut)if((!flush||ko.Core)&&ko.Hits(lo,hi))return 1;
            // the landmarks' features and the door fronts
            float cy=(lo.y+hi.y)*.5f;
            foreach(var (c,r) in w.Rounds){float dx=Math.Max(0,Math.Max(lo.x-c.x,c.x-hi.x)),dz=Math.Max(0,Math.Max(lo.z-c.z,c.z-hi.z));if(dx*dx+dz*dz<r*r&&Math.Abs(cy-c.y)<14)return 2;}
            // an instance's drift band and beyond: the bleed's own dressing stands there (the final court's route it leaves plain)
            var s=BleedAt(w,new V3((lo.x+hi.x)*.5f,lo.y,(lo.z+hi.z)*.5f));
            if(s.Door>=0&&s.Band>=NativeCityBleed.Bands.Drift&&NativeCityBleed.Theme(w.In.Bleed.Doors[s.Door].Instance)!="final")return 3;
            return -1;
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The walls on the streets: every bay of a lot (and of a retaining wall, a shed, the Cordon) the street can see
        // ------------------------------------------------------------------------------------------------------------------
        private static List<Slot> Bays(World w)
        {
            var slots=new List<Slot>();var B=w.B;var mb=w.In.Map.bounds;
            for(int bi=0;bi<w.Blocks.Length;bi++)
            {
                if(w.Left[bi]||w.Tread[bi])continue;var role=w.Roles[bi];char style;
                if(role==NativeMeridianDress.RLot)style=w.Lots[w.Blocks[bi].id][0];
                else if(role==NativeMeridianDress.RGround||role==NativeMeridianDress.RShed||role==NativeMeridianDress.RWall||role==NativeMeridianDress.RCordon)style='c';
                else continue;
                bool flat=role!=NativeMeridianDress.RLot;int o=bi*6;float bottom=B[o+2],top=B[o+3];
                for(int f=0;f<6;f++)
                {
                    if(f==2||f==3)continue;const float e=.05f;
                    if(f==0&&B[o+1]>=mb.maxX-e||f==1&&B[o]<=mb.minX+e||f==4&&B[o+5]>=mb.maxZ-e||f==5&&B[o+4]<=mb.minZ+e)continue;
                    NativeMeridianDress.FaceFrame(B,o,f,out var fo,out var fu,out _);float L=fu.Length;if(L<2f)continue;var uh=fu/L;var N=NativeMeridianDress.NormalOf(f);
                    int n=Math.Max(1,(int)Math.Round(L/Bay));float bw=L/n;
                    for(int i=0;i<n;i++)
                    {
                        float ua=i*bw;var col=fo+uh*(ua+bw*.5f);
                        // the street's floor at the bay's foot: a retaining wall's street is at its own foot, a lot's at its bottom;
                        // find the floor 0.6 m out from the face, under the wall's top
                        var front=col+N*.6f;float floor=FloorUnder(w,front.x,front.z,top-.2f);if(floor<-1e8f||floor<bottom-.05f)continue;
                        float h=top-floor;if(h<3.2f)continue;
                        // the bay's foot in sight across its width: 8 cm out at 0.3 m and 2.2 m, at its middle and near its ends
                        bool seen=true;
                        foreach(float u in new[]{-bw*.5f+.12f,0,bw*.5f-.12f})foreach(float y in new[]{.3f,2.2f})
                        {var q=col+uh*u+N*.08f;if(w.S.Inside(q.x,floor+y,q.z,bi,.005f)){seen=false;break;}}
                        if(!seen)continue;
                        // the floor in front is level across the bay (a stair or a slope beside it takes nothing here)
                        bool level=true;foreach(float u in new[]{-bw*.5f+.15f,bw*.5f-.15f}){var q=col+uh*u+N*.6f;if(Math.Abs(FloorUnder(w,q.x,q.z,floor+.3f)-floor)>.03f){level=false;break;}}
                        if(!level||OnRamp(w,front.x,front.z,floor,1f))continue;
                        // never on or beside a flight: the stairs', the sloped streets' and the fire escapes' wedges and the retaining
                        // walls their sides get (the stairs pass's, M1) take no dressing of ours
                        if(NearFlight(w,col+uh*(-bw*.5f),col+uh*(bw*.5f),floor,top,1.5f))continue;
                        // nothing stands against the bay's ground storey within 0.45 m (a rail, a post, a tread, a landing)
                        var c0=col+uh*(-bw*.5f+.05f)+N*.01f;var c1=col+uh*(bw*.5f-.05f)+N*.45f;
                        if(!BoxClear(w,new V3(Math.Min(c0.x,c1.x),floor+.03f,Math.Min(c0.z,c1.z)),new V3(Math.Max(c0.x,c1.x),floor+Clear,Math.Max(c0.z,c1.z)),bi))continue;
                        bool open=true;foreach(float dd in new[]{.5f,1.5f,2.4f}){var q=col+N*dd;if(w.S.Inside(q.x,floor+.1f,q.z,-1,.005f)||Math.Abs(FloorUnder(w,q.x,q.z,floor+.1f)-floor)>.05f){open=false;break;}}
                        float gap=Ray(w,col+N*.02f+new V3(0,floor-col.y+2.2f,0),N,40f,out _,out float t,out _)?t:99f;
                        var s=new Slot{Foot=new V3(col.x,floor,col.z),N=N,Along=uh,W=bw,Floor=floor,Bottom=bottom,Top=h,Gap=gap,Block=bi,Face=f,Bay=i,Bays=n,Style=style,Open=open,Flat=flat};
                        if(!flat)
                        {
                            w.Ground.TryGetValue(BayKey(bi,f,ua,0),out s.Key);
                            int st=0;while(NativeMeridianDress.StoreyFloor(bottom,st)<top-.01f)st++;s.Storeys=st;s.Uppers=new string[st];
                            for(int k=1;k<st;k++)w.Upper.TryGetValue(BayKey(bi,f,ua,NativeMeridianDress.StoreyFloor(bottom,k)),out s.Uppers[k]);
                        }
                        // the nearest route leg in front of it, and its metres from the gate
                        float best=1e9f;
                        foreach(var lg in w.Legs)
                        {
                            var q=s.Foot+N*3f;float d=SegDist(q,lg.a,lg.b,out float tt);if(d>lg.width*.5f+12f||d>=best)continue;
                            float y=lg.a.y+(lg.b.y-lg.a.y)*tt;if(Math.Abs(y-floor)>1f)continue;
                            best=d;s.Route=lg.route;s.Station=lg.station+tt*new V3(lg.b.x-lg.a.x,0,lg.b.z-lg.a.z).Length;
                        }
                        int idx=slots.Count;slots.Add(s);
                        long hk=((long)(int)Math.Floor(s.Foot.x/8)<<32)^(uint)(int)Math.Floor(s.Foot.z/8);if(!w.SlotHash.TryGetValue(hk,out var hl))w.SlotHash[hk]=hl=new List<int>();hl.Add(idx);
                    }
                }
            }
            return slots;
        }
        private static float SegDist(V3 p,V3 a,V3 b,out float t)
        {
            float dx=b.x-a.x,dz=b.z-a.z,l=dx*dx+dz*dz;t=l<1e-6f?0:Math.Max(0,Math.Min(1,((p.x-a.x)*dx+(p.z-a.z)*dz)/l));float ex=a.x+dx*t-p.x,ez=a.z+dz*t-p.z;return (float)Math.Sqrt(ex*ex+ez*ez);
        }
        /// <summary>The nearest bay within `r` m of a floor point (and at its level), or null.</summary>
        private static Slot NearestSlot(World w,V3 p,float r)
        {
            Slot best=null;float bd=r*r;
            for(int z=(int)Math.Floor((p.z-r)/8);z<=(int)Math.Floor((p.z+r)/8);z++)for(int x=(int)Math.Floor((p.x-r)/8);x<=(int)Math.Floor((p.x+r)/8);x++)
            {
                if(!w.SlotHash.TryGetValue(((long)x<<32)^(uint)z,out var l))continue;
                foreach(int i in l){var s=w.Slots[i];if(Math.Abs(s.Floor-p.y)>.6f)continue;float dx=s.Foot.x-p.x,dz=s.Foot.z-p.z,d=dx*dx+dz*dz;if(d<bd){bd=d;best=s;}}
            }
            return best;
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The floor cells: every 2 m cell of a street, a yard, a bridge or a roof
        // ------------------------------------------------------------------------------------------------------------------
        private static List<FloorCell> FloorCells(World w)
        {
            var cells=new List<FloorCell>();var mb=w.In.Map.bounds;var B=w.B;var tops=new List<float>();
            for(int gz=(int)Math.Ceiling(mb.minZ/Cell);gz*Cell+Cell<=mb.maxZ;gz++)
                for(int gx=(int)Math.Ceiling(mb.minX/Cell);gx*Cell+Cell<=mb.maxX;gx++)
                {
                    float cx=gx*Cell+Cell*.5f,cz=gz*Cell+Cell*.5f;
                    // the camp (Belvedere Camp's own floor)
                    if(Math.Abs(cx)<26f&&Math.Abs(cz)<26f)continue;
                    tops.Clear();
                    foreach(int i in w.S.Near(cx,cx,cz,cz)){int o=i*6;if(cx>=B[o]&&cx<=B[o+1]&&cz>=B[o+4]&&cz<=B[o+5]&&!tops.Contains(B[o+3]))tops.Add(B[o+3]);}
                    tops.Sort();
                    foreach(float top in tops)
                    {
                        int under=BlockUnder(w,cx,cz,top);if(under<0||w.Left[under]||w.Tread[under])continue;var role=w.Roles[under];
                        bool roof=role==NativeMeridianDress.RLot||role==NativeMeridianDress.RArcadeRoof||role==NativeMeridianDress.RLid;
                        if(!(roof||role==NativeMeridianDress.RGround||role==NativeMeridianDress.RBridge||role==NativeMeridianDress.RViaduct))continue;
                        // the whole cell on a top at this height, nothing standing on it, and 2.4 m of headroom at its middle
                        bool ok=true;
                        foreach(float u in new[]{-.98f,0,.98f}){foreach(float v in new[]{-.98f,0,.98f})if(!OnTop(w,cx+u,cz+v,top)){ok=false;break;}if(!ok)break;}
                        if(!ok||w.S.Ceiling(cx,cz,top+.05f)-top<2.4f||OnRamp(w,cx,cz,top,.3f))continue;
                        var c=new FloorCell{At=new V3(cx,top,cz),Block=under,Roof=roof};
                        if(Precinct(c.At))c.District='p';
                        cells.Add(c);
                    }
                }
            // the pavement in front of each bay (2.6 m), and each cell's district from the nearest bay within 18 m
            var byKey=new Dictionary<long,int>();for(int i=0;i<cells.Count;i++)byKey[CellKey(cells[i].At)]=i;
            foreach(var s in w.Slots)
            {
                if(!s.Open||s.Flat)continue;
                for(float d=.8f;d<2.6f;d+=Cell)for(float u=-s.W*.5f+.6f;u<s.W*.5f;u+=Cell)
                {var q=s.Foot+s.Along*u+s.N*d;if(byKey.TryGetValue(CellKey(q),out int ci)){var c=cells[ci];c.Pavement=true;c.WallN=s.N;if(c.District!='p')c.District=s.Style;}}
            }
            foreach(var c in cells)
            {
                if(c.District=='p'||c.Pavement)continue;var s=NearestSlot(w,c.At,18f);if(s!=null&&!s.Flat)c.District=s.Style;
            }
            // the routes' corridors
            foreach(var lg in w.Legs)
            {
                var ab=new V3(lg.b.x-lg.a.x,0,lg.b.z-lg.a.z);float len=ab.Length;if(len<.5f)continue;var al=ab/len;var ac=new V3(al.z,0,-al.x);
                for(float s=Cell*.5f;s<len;s+=Cell*.5f)for(float u=-lg.width*.5f;u<=lg.width*.5f;u+=Cell*.5f)
                {
                    var q=lg.a+al*s+ac*u;q.y=lg.a.y+(lg.b.y-lg.a.y)*(s/len);
                    if(byKey.TryGetValue(CellKey(q),out int ci)&&cells[ci].Route<0&&Math.Abs(cells[ci].At.y-q.y)<.6f){cells[ci].Route=lg.route;cells[ci].Station=lg.station+s;}
                }
            }
            return cells;
        }
        private static long CellKey(V3 p)=>((long)(int)Math.Floor(p.x/Cell)<<40)^((long)(int)Math.Floor(p.z/Cell)<<16)^(uint)((int)Math.Round(p.y*2)&0xffff);

        // ------------------------------------------------------------------------------------------------------------------
        // The layout's markings down the routes' streets (a route's flat leg is its street; the camp's plaza has none): the centre
        // line's dashes and manholes; along a side with a wall, its gutter's drains, and in the shopping streets double yellow lines
        // before the shops; a steam grate now and then; at the turns zebras, SLOW and box junctions
        // ------------------------------------------------------------------------------------------------------------------
        private static void Markings(World w,Plan plan,List<FloorCell> cells)
        {
            uint ls=w.LSeed;int n=0;
            for(int li=0;li<w.Legs.Count;li++)
            {
                var lg=w.Legs[li];if(lg.kind!="flat"||Math.Abs(lg.a.y-lg.b.y)>.01f)continue;
                var ab=new V3(lg.b.x-lg.a.x,0,lg.b.z-lg.a.z);float len=ab.Length;if(len<10f)continue;var al=ab/len;var ac=new V3(al.z,0,-al.x);float yaw=YawOf(al);
                var mid=lg.a+al*(len*.5f);if(Precinct(mid))continue;
                var s0=NearestSlot(w,mid,lg.width*.5f+12f);char style=s0!=null&&!s0.Flat?s0.Style:'o';
                bool shopping=style=='s'||style=='m'||style=='c',town=shopping||style=='t'||style=='r'||style=='v';
                // the centre line's dashes (2 m every 6 m), off the junctions and the camp's plaza
                for(float s=6f;s<len-5f;s+=6f){var at=lg.a+al*s;if(!Precinct(at))Add(w,plan,"road.dash",at,yaw,One,Life.None,PlaceHash(at),style);}
                // manholes off the centre line, about every 30 m
                for(float s=8f+10f*Rand(ls,7,li);s<len-6f;s+=26f+10f*Rand(ls,8,n++))
                {var at=lg.a+al*s+ac*((Rand(ls,9,n)<.5f?-1:1)*1.6f);if(!Precinct(at))Add(w,plan,"ground.manhole",at,(int)(Rand(ls,10,n)*4)*(float)(Math.PI/2),One,Life.None,PlaceHash(at),style);}
                // each side: the gutter 2.55 m out from a wall within reach (its kerb's foot), sampled every 2 m down the leg
                foreach(float side in new[]{1f,-1f})
                {
                    float nextDrain=4f+9f*Rand(ls,3,li*2+(side>0?1:0));bool grate=town&&Rand(ls,5,li*2+(side>0?1:0))<.4f;float grateAt=len*(.3f+.4f*Rand(ls,6,li));
                    for(float s=1f;s<len-1f;s+=2f)
                    {
                        var foot=lg.a+al*s;if(Precinct(foot))continue;
                        if(!Ray(w,foot+new V3(0,1.6f,0),ac*side,lg.width*.5f+8f,out int wb,out float d,out _)||d<4.2f)continue;
                        float off=d-2.55f;var gut=foot+ac*(side*off);
                        if(s>=nextDrain&&s<len-3f){Add(w,plan,"ground.drain",gut,yaw,One,Life.None,PlaceHash(gut),style);nextDrain=s+15f+6f*Rand(ls,4,n++);}
                        if(shopping&&s>=3f&&s<len-3f){var sl=NearestSlot(w,gut,3.6f);if(sl!=null&&sl.Open&&!sl.Flat)Add(w,plan,"road.yellow",gut+ac*(side*-.12f),yaw,One,Life.None,0,style);}
                        if(grate&&Math.Abs(s-grateAt)<1f){grate=false;var at=gut+ac*(side*-1.2f);Add(w,plan,"ground.grate",at,yaw,One,Life.Steam,PlaceHash(at),style);}
                        if(shopping&&lg.width>=12f&&len>=24f&&Math.Abs(s-len*.5f)<1f&&Rand(ls,11,li*2+(side>0?1:0))<.3f){var at=gut+ac*(side*-1.1f);Add(w,plan,"road.bus",at,yaw,One,Life.None,0,style);}
                    }
                }
                // where the route turns into another street, 5 m short of the turn: a zebra across this street (SLOW before it), or a
                // box junction at the turn itself
                if(li+1<w.Legs.Count&&w.Legs[li+1].route==lg.route&&len>=14f&&town&&!Precinct(lg.b))
                {
                    var nx=w.Legs[li+1];var nd=new V3(nx.b.x-nx.a.x,0,nx.b.z-nx.a.z).Normalized;if(Math.Abs(V3.Dot(nd,al))>.7f)continue;
                    float pick=Rand(ls,12,li),half=lg.width*.5f-1.5f;
                    if(pick<.55f)
                    {
                        float s=len-5f;for(float u=-half+1f;u<=half-1f+.01f;u+=2f)Add(w,plan,"road.zebra",lg.a+al*s+ac*u,yaw,One,Life.None,0,style);
                        Add(w,plan,"road.slow",lg.a+al*(s-6f),yaw+(float)Math.PI,One,Life.None,0,style);
                    }
                    else if(shopping&&pick<.85f)
                    {
                        var c=lg.b;for(int i=-1;i<=1;i++)for(int j=-1;j<=1;j++)Add(w,plan,"road.box",c+al*(i*2f)+ac*(j*2f),yaw,One,Life.None,0,style);
                    }
                }
            }
        }
        /// <summary>Whether a place lies on the camp's plaza (outside the fence, 62 m or less from its middle, at its level).</summary>
        private static bool Precinct(V3 p)=>Math.Max(Math.Abs(p.x),Math.Abs(p.z))<62f&&Math.Abs(p.y)<.5f;
        private static readonly V3 One=new V3(1,1,1);

        // ------------------------------------------------------------------------------------------------------------------
        // The camp's precinct (the plaza round the fence): where the evacuation queued for the camp: the Civil Defence's assembly
        // stencils and lane arrows to the gates, the pitches of the tents that stood there, their fires' ash
        // ------------------------------------------------------------------------------------------------------------------
        private static void Precinct(World w,Plan plan,List<FloorCell> cells)
        {
            uint ls=w.LSeed,rs=w.RSeed;
            V3[] gates={new V3(0,0,1),new V3(1,0,0),new V3(0,0,-1),new V3(-1,0,0)};
            for(int g=0;g<4;g++)
            {
                var d=gates[g];var right=new V3(d.z,0,-d.x);float yaw=YawOf(d*-1);
                // ASSEMBLY POINT beside the lane out of the gate, and the painted lanes queueing into it
                Add(w,plan,"mark.cd.0",d*(34f+4f*Rand(ls,20,g))+right*(Rand(ls,21,g)<.5f?-7f:7f),yaw,One,Life.None,PlaceHash(d*34),'p');
                if(Rand(ls,22,g)<.7f)for(float s=30f;s<=50f;s+=4f)foreach(float u in new[]{-2.6f,2.6f})Add(w,plan,"mark.cd.1",d*s+right*u,yaw,One,Life.None,0,'p');
            }
            // the tents: two pitches of rows in the precinct's corners, the run's ones still marked; their fires' ash between them
            for(int q=0;q<4;q++)
            {
                if(Rand(ls,23,q)>.55f)continue;float sx=(q&1)==0?1:-1,sz=(q&2)==0?1:-1;
                for(int i=0;i<4;i++)for(int j=0;j<3;j++)
                {
                    var at=new V3(sx*(31f+i*4.2f),0,sz*(31f+j*4.6f));if(Rand(rs,24,q*16+i*4+j)<.3f)continue;
                    Add(w,plan,"ground.tent",at,(Rand(ls,25,q)<.5f?0:1)*(float)(Math.PI/2),One,Life.None,PlaceHash(at),'p');
                }
                for(int k=0;k<2;k++){var at=new V3(sx*(33f+8f*Rand(rs,26,q*2+k)),0,sz*(29f+2f*Rand(rs,27,q*2+k)));Add(w,plan,"ground.ash",at,0,One,Life.None,PlaceHash(at),'p');}
            }
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The layout's fittings on the walls: downpipes at the corners, awnings over the shops, blade signs, air conditioners
        // under the tenements' windows, window boxes on the rows, the civic flags, the Civil Defence floods over the camp's plaza
        // ------------------------------------------------------------------------------------------------------------------
        private static void Fittings(World w,Plan plan)
        {
            uint ls=w.LSeed,rs=w.RSeed;int floods=0;
            for(int i=0;i<w.Slots.Count;i++)
            {
                var s=w.Slots[i];float yaw=YawOf(s.N);uint h=Hash2(ls,(uint)(s.Block*16+s.Face)*977u+(uint)s.Bay);
                // a downpipe at each corner of a tall face, 0.18 m in from it
                if(!s.Flat&&s.Top>=6f&&s.Bays>=3&&(s.Bay==0||s.Bay==s.Bays-1)&&Rand(h,30,0)<.38f)
                {
                    float u=(s.Bay==0?-1:1)*(s.W*.5f-.18f);
                    if(Add(w,plan,"low.pipe",s.Foot+s.Along*u,yaw,new V3(1,s.Top-.12f,1),Life.None,h,s.Style,s.Block))s.Piped=true;
                }
                if(s.Flat)continue;
                bool shop=s.Key!=null?s.Key.StartsWith("g.shop.",StringComparison.Ordinal):(s.Style=='s'||s.Style=='m')&&s.Bay%3!=2;
                // an awning over a lit shopfront (its valance's foot at 3.12 m)
                if(shop&&s.Top>=4.6f&&Rand(h,31,0)<(s.Style=='m'?.55f:.4f))
                {
                    if(Add(w,plan,Rand(h,32,0)<.15f?"upper.awning.torn":"upper.awning",s.Foot+new V3(0,3.42f,0),yaw,One,Life.None,h,s.Style,s.Block,Canvas(h)))s.Busy=true;
                }
                // a blade sign at a bay's edge on the first floor of a shop, a market or civic face (one a face)
                if((s.Style=='s'||s.Style=='m'||s.Style=='c'||s.Style=='t')&&s.Top>=5.8f&&s.Bay==(int)(Rand(Hash2(ls,(uint)(s.Block*16+s.Face)),33,0)*Math.Max(1,s.Bays-1))&&s.Bays>=2&&Rand(h,34,0)<(s.Style=='t'?.25f:.5f))
                {
                    float r=Rand(rs,35,i);var life=r<.55f?Life.Steady:r<.72f?Life.Flicker:Life.Dead;
                    Add(w,plan,"upper.sign."+(int)(Rand(h,36,0)*4),s.Foot+s.Along*(s.W*.5f)+new V3(0,4.35f,0),yaw,One,life,h,s.Style,s.Block);
                }
                // under the upper storeys' windows: an air conditioner, or a window box on the sill
                for(int k=1;k<s.Storeys;k++)
                {
                    float fy=NativeMeridianDress.StoreyFloor(s.Bottom,k)-s.Floor;if(fy<Clear||fy+3f>s.Top-.8f)continue;
                    string up=s.Uppers!=null?s.Uppers[k]:null;bool window=up==null||up.StartsWith("u.win",StringComparison.Ordinal)||up.StartsWith("u.ten",StringComparison.Ordinal)||up.StartsWith("u.civic",StringComparison.Ordinal);
                    if(!window)continue;float r=Rand(h,40,k);
                    float ac=s.Style=='t'?.16f:s.Style=='s'?.08f:s.Style=='c'?.04f:s.Style=='r'||s.Style=='v'?.03f:0;
                    float box=s.Style=='r'||s.Style=='v'?.16f:s.Style=='s'||s.Style=='m'?.06f:s.Style=='t'?.05f:0;
                    if(r<ac)Add(w,plan,"upper.ac",s.Foot+new V3(0,fy+.62f,0),yaw,One,Life.None,Hash2(h,(uint)k),s.Style,s.Block);
                    else if(r<ac+box)Add(w,plan,"upper.box",s.Foot+new V3(0,fy+.75f,0),yaw,One,Life.None,Hash2(h,(uint)k),s.Style,s.Block,Bloom(Hash2(h,(uint)k)));
                }
                // a civic face's flag over its middle bay
                if(s.Style=='c'&&s.Bay==s.Bays/2&&s.Top>=8f&&Rand(h,41,0)<.5f)Add(w,plan,"upper.flag",s.Foot+new V3(0,6.75f,0),yaw,One,Life.None,h,s.Style,s.Block,Banner(h));
                // a Civil Defence flood over the camp's plaza: faces within 80 m of the camp that look toward it
                var to=new V3(-s.Foot.x,0,-s.Foot.z);float dist=to.Length;
                if(floods<8&&dist<80f&&dist>26f&&V3.Dot(s.N,to/dist)>.6f&&s.Top>=9f&&s.Bay==s.Bays/2&&Rand(h,42,0)<.6f)
                {
                    float r=Rand(rs,43,i);if(Add(w,plan,"upper.flood",s.Foot+new V3(0,7.6f,0),yaw,One,r<.8f?Life.Steady:Life.Dead,h,s.Style,s.Block))floods++;
                }
            }
        }
        // the seeded colours (a tint multiplies the banked albedo; the pieces bank their tinted parts white or pale)
        private static readonly uint[] Canvases={0xC8463C,0x3C7A5A,0x2E5E8C,0xC89A3C,0x7A3C6A,0xD8D0C0};
        private static readonly uint[] Sprays={0xE8E4DA,0xD8342C,0x1C1C20,0xF09A28,0x3CC8C0,0xE85AA0,0xF0D040};
        private static readonly uint[] Chalks={0xF4F0E8,0xF4A8C8,0xF4E890,0xA8D8F4};
        private static readonly uint[] Banners={0xC0302A,0x2A4A8A,0xE8E0D0,0x2A6A3A};
        private static uint Canvas(uint h)=>Canvases[(int)(NativeMeridianDress.Mix(h,51)%(uint)Canvases.Length)];
        private static uint Spray(uint h)=>Sprays[(int)(NativeMeridianDress.Mix(h,52)%(uint)Sprays.Length)];
        private static uint Chalk(uint h)=>Chalks[(int)(NativeMeridianDress.Mix(h,53)%(uint)Chalks.Length)];
        private static uint Banner(uint h)=>Banners[(int)(NativeMeridianDress.Mix(h,54)%(uint)Banners.Length)];
        private static readonly uint[] Blooms={0,0xC8B080,0xB0C890,0xC890A8};
        private static uint Bloom(uint h)=>Blooms[(int)(NativeMeridianDress.Mix(h,55)%4u)];

        // ------------------------------------------------------------------------------------------------------------------
        // Spans across the streets between facing walls: wires, washing lines in the tenements' slots, bunting and festoons over
        // the shops and the market; traffic signals stuck on amber where a route turns
        // ------------------------------------------------------------------------------------------------------------------
        private static void Spans(World w,Plan plan)
        {
            uint ls=w.LSeed,rs=w.RSeed;var last=new Dictionary<long,float>();
            for(int i=0;i<w.Slots.Count;i++)
            {
                var s=w.Slots[i];if(s.Flat||s.Gap<4f||s.Gap>22f||s.Bay%2!=0)continue;
                uint h=Hash2(ls,(uint)(s.Block*16+s.Face)*613u+(uint)s.Bay);float r=Rand(h,60,0);
                string key;float height;
                switch(s.Style)
                {
                    case 't':key=s.Gap<=16f&&r<.55f?"span.washing":r<.85f?"span.wires":null;break;
                    case 'r':case 'v':key=r<.45f?"span.wires":r<.6f&&s.Gap<=14f?"span.washing":null;break;
                    case 's':key=r<.35f?"span.bunting":r<.65f?"span.wires":r<.75f&&s.Gap<=16f?"span.festoon":null;break;
                    case 'm':key=r<.4f?"span.bunting":r<.75f&&s.Gap<=18f?"span.festoon":null;break;
                    case 'c':key=r<.2f?"span.wires":null;break;
                    default:key=r<.35f?"span.wires":null;break;
                }
                if(key==null)continue;
                // the storey line just above a floor (the string course's top, under the sills): 7.3 m, or 4.3 m on a two-storey street
                height=s.Top>=8.4f?7.3f:4.3f;if(key=="span.washing"&&s.Style=='t'&&s.Top>=11.4f&&Rand(h,61,0)<.5f)height=10.3f;
                // spacing along the street (one span a 7 m in the tenements' slots, 12 m elsewhere)
                long street=((long)s.Block<<3)^s.Face;float along=V3.Dot(s.Foot,s.Along);float space=key=="span.washing"?8f:12f;
                if(last.TryGetValue(street,out float prev)&&Math.Abs(along-prev)<space)continue;
                if(TrySpan(w,plan,s,key,height,h,Life.None))last[street]=along;
            }
            // traffic signals at the routes' turns in town: a span across the street just short of the turn, the signal at its middle
            for(int li=0;li+1<w.Legs.Count;li++)
            {
                // a turn into another street, or the foot of a sloped street (traffic lights at a hill's foot); never a stair's
                var lg=w.Legs[li];var nx=w.Legs[li+1];if(nx.route!=lg.route||lg.kind!="flat"||nx.kind=="stair")continue;
                var al=new V3(lg.b.x-lg.a.x,0,lg.b.z-lg.a.z);float len=al.Length;if(len<12f)continue;al=al/len;
                var nd=new V3(nx.b.x-nx.a.x,0,nx.b.z-nx.a.z).Normalized;if(Math.Abs(V3.Dot(nd,al))>.82f&&nx.kind!="ramp"||Rand(ls,62,li)>.75f)continue;
                if(Precinct(lg.b))continue;float r=Rand(rs,63,li);var life=r<.85f?Life.Blink:Life.Dead;
                // a span across the street just short of the turn, the signal at its middle; else a high arm off a wall beside it
                bool done=false;
                foreach(float back in new[]{6f,9f,12f})
                {
                    var at=lg.b-al*back;var s=NearestSlot(w,at,lg.width*.5f+14f);if(s==null||s.Flat||Math.Abs(V3.Dot(s.N,al))>.3f||s.Style=='w'||s.Style=='h')continue;
                    if(TrySpan(w,plan,s,"span.wires",7.3f,Hash2(ls,(uint)li),life,true,al*-1)){done=true;break;}
                    if(s.Top>=6.6f&&Add(w,plan,"upper.signal",s.Foot+new V3(0,4.8f,0),YawOf(s.N),One,life,Hash2(ls,(uint)li),s.Style,s.Block)){done=true;break;}
                }
                if(!done)plan.KeptOut+=0;
            }
        }
        /// <summary>A span from a bay across to the facing wall at a height over the street (false where it does not fit).</summary>
        private static bool TrySpan(World w,Plan plan,Slot s,string key,float height,uint h,Life life,bool signal=false,V3 facing=default)
        {
            var o=s.Foot+new V3(0,height,0);
            if(!Ray(w,o+s.N*.02f,s.N,24f,out int far,out float gap,out var fn)||V3.Dot(fn,s.N)>-.99f||gap<4f||gap>22f||w.Left[far])return false;
            // both walls stand 0.6 m over the anchors
            var B=w.B;if(B[s.Block*6+3]<o.y+.6f||B[far*6+3]<o.y+.6f)return false;
            var K=Kinds[KindOf(key)];var across=s.N;float yaw=YawOf(across)-(float)(Math.PI/2);int count=plan.Pieces.Count;
            if(K.Max.x<=1.01f)
            {
                if(!Add(w,plan,key,o+across*.02f,yaw,new V3(gap-.04f,1,1),life==Life.Blink?Life.None:life,h,s.Style,s.Block))return false;
            }
            else
            {
                int n=Math.Max(1,(int)Math.Round(gap/2f));float sc=(gap-.04f)/(2f*n);bool any=false;
                for(int k=0;k<n;k++){uint hk=Hash2(h,(uint)k);var life2=key=="span.festoon"?(Rand(hk,64,0)<.3f?Life.Dead:Life.Steady):Life.None;
                    any|=Add(w,plan,key,o+across*(.02f+2f*sc*k),yaw,new V3(sc,1,1),life2,hk,s.Style,s.Block);}
                if(!any)return false;
            }
            if(signal&&!Add(w,plan,"hung.signal",o+across*(gap*.5f)+new V3(0,-.02f,0),YawOf(facing),One,life,h,s.Style,-1,0,true)){plan.Pieces.RemoveRange(count,plan.Pieces.Count-count);return false;}
            plan.Spans++;return true;
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The run's story on the walls: posters in layers, graffiti and search marks, boards over the windows, handprints, the
        // warehouses' hazard paint and stencils; against the walls bin bags, bicycles, cardboard, sandbags, meters and payphones;
        // the survivors' banners from the upper windows
        // ------------------------------------------------------------------------------------------------------------------
        private static void WallStory(World w,Plan plan)
        {
            uint rs=w.RSeed;float lastBanner=-1e9f;int banners=0;
            for(int i=0;i<w.Slots.Count;i++)
            {
                var s=w.Slots[i];float yaw=YawOf(s.N);uint h=Hash2(rs,(uint)(s.Block*16+s.Face)*1013u+(uint)s.Bay);float r=Rand(h,70,0);
                // nothing on a bay next to its downpipe, or under its awning's own fixing
                string kind=Category(s);bool near=s.Station>=0;var rumour=RumourAt(w,s.Foot+s.N*1f+new V3(0,1,0));
                string flat=null;uint tint=0;
                switch(kind)
                {
                    case "blind":
                        flat=r<.12f?"poster.bills":r<.19f?"poster.stay":r<.24f?"poster.missing":r<.29f?"poster.cd":r<.43f?"graffiti.tag."+(int)(Rand(h,71,0)*2):r<.51f?"graffiti.msg."+(int)(Rand(h,72,0)*4):r<.54f?"wall.hand":null;
                        if(flat==null&&(s.Style=='w'||s.Style=='h')&&r<.62f)flat="wall.stencil";break;
                    case "shutter":flat=r<.3f?"graffiti.tag."+(int)(Rand(h,71,0)*2):r<.42f?"graffiti.msg."+(int)(Rand(h,72,0)*4):r<.5f?"poster.stay":null;break;
                    case "opening":flat=r<(rumour=="wardzero"?.35f:.09f)?"wall.boards":null;break;
                    case "door":flat=r<.14f&&(s.Style=='r'||s.Style=='t'||s.Style=='v')?"graffiti.x":r<.2f?"wall.boards":null;break;
                    case "ware":flat=r<.22f?"wall.hazard":r<.3f?"wall.stencil":r<.38f?"graffiti.tag."+(int)(Rand(h,71,0)*2):null;break;
                    case "flat":flat=r<.1f?"poster.bills":r<.16f?"poster.stay":r<.3f?"graffiti.tag."+(int)(Rand(h,71,0)*2):r<.38f?"graffiti.msg."+(int)(Rand(h,72,0)*4):r<.41f?"wall.hand":null;break;
                }
                if(flat!=null&&!(s.Piped&&!flat.StartsWith("poster.missing",StringComparison.Ordinal)))
                {
                    if(flat.StartsWith("graffiti.",StringComparison.Ordinal))tint=flat=="graffiti.x"?(Rand(h,73,0)<.6f?0xD8342Cu:0xF09A28u):Spray(h);
                    float jitter=flat.StartsWith("poster.",StringComparison.Ordinal)||flat=="wall.notice"?(Rand(h,74,0)-.5f)*Math.Max(0,s.W-1.2f)*.6f:0;
                    Add(w,plan,flat,s.Foot+s.Along*jitter,yaw,One,Life.None,h,s.Style,s.Block,tint);
                }
                // low clutter on the pavement against the wall (the open street in front, never a corner's downpipe bay)
                if(s.Open&&!s.Piped&&!s.Flat)
                {
                    float q=Rand(h,75,0);string low=null;
                    switch(s.Style)
                    {
                        case 't':low=q<.1f?"low.binbags":q<.13f?"low.bike":q<.15f?"low.meter":null;break;
                        case 's':low=q<.07f?"low.binbags":q<.11f?"low.card":q<.125f?"low.phone":q<.14f?"low.meter":null;break;
                        case 'm':low=q<.08f?"low.binbags":q<.14f?"low.card":null;break;
                        case 'r':case 'v':low=q<.04f?"low.binbags":q<.065f?"low.bike":q<.08f?"low.meter":null;break;
                        case 'c':low=q<.1f?"low.sandbags":q<.12f?"low.meter":null;break;
                        default:low=q<.05f?"low.binbags":q<.07f?"low.card":q<.09f?"low.meter":null;break;
                    }
                    if(low==null&&V3.Dot(s.N,new V3(-s.Foot.x,0,-s.Foot.z).Normalized)>.6f&&s.Foot.Length<70f&&q<.12f)low="low.sandbags";
                    if(low!=null)Add(w,plan,low,s.Foot,yaw,One,Life.None,h,s.Style,s.Block);
                }
                // a survivors' banner from an upper window over a route home (one a 90 m of route, eight at most)
                if(!s.Flat&&near&&s.Station>30f&&s.Station<240f&&(s.Style=='t'||s.Style=='r'||s.Style=='s')&&s.Storeys>=3&&banners<10&&Math.Abs(s.Station-lastBanner)>60f&&Rand(h,76,0)<.35f)
                {
                    int k=s.Storeys>=4&&Rand(h,77,0)<.5f?3:2;float fy=NativeMeridianDress.StoreyFloor(s.Bottom,k)-s.Floor+.74f;
                    string up=s.Uppers!=null&&k<s.Uppers.Length?s.Uppers[k]:null;
                    if(fy+.3f<s.Top-.6f&&(up==null||!up.StartsWith("u.oriel",StringComparison.Ordinal))&&Add(w,plan,"upper.banner."+(int)(Rand(h,78,0)*3),s.Foot+new V3(0,fy,0),yaw,One,Life.None,h,s.Style,s.Block,Banners[(int)(Rand(h,79,0)*2)==0?2:0]))
                    {lastBanner=s.Station;banners++;}
                }
            }
        }
        /// <summary>What a bay's face offers flat art: a blind wall, a shutter, an opening (a window or shop to board over), a door,
        /// a warehouse's plinth, or a plain flat wall (a retaining wall, a shed, the Cordon, or a lot drawn without the dress).</summary>
        private static string Category(Slot s)
        {
            if(s.Flat||s.Key==null)return "flat";var k=s.Key;
            if(k.StartsWith("g.blind",StringComparison.Ordinal))return "blind";
            if(k=="g.shutter")return "shutter";
            if(k.StartsWith("g.door",StringComparison.Ordinal)||k=="g.entry")return "door";
            if(k=="g.ware"||k=="g.load")return "ware";
            return "opening";
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The run's scatter on the floor: litter drifting into clumps along the pavements, the long night's cracks, patches and
        // oil, blood, chalk, scorch marks, and in each instance's rumour band its own litter
        // ------------------------------------------------------------------------------------------------------------------
        private static void Scatter(World w,Plan plan,List<FloorCell> cells)
        {
            uint rs=w.RSeed,ls=w.LSeed;
            for(int i=0;i<cells.Count;i++)
            {
                var c=cells[i];float r=Rand(rs,90,i),clump=Clump(rs,c.At,6f);char d=c.District;
                float yaw=(int)(Rand(rs,91,i)*4)*(float)(Math.PI/2);uint h=Hash2(rs,(uint)i);string key=null;uint tint=0;
                if(c.Roof)
                {
                    if(r<.008f)key="ground.patch";else if(r<.013f)key="litter.cans";else if(r<.0145f)key="ground.ash";
                }
                else
                {
                    float litter=d=='s'?.13f:d=='m'?.15f:d=='t'?.12f:d=='p'?.16f:d=='r'||d=='v'?.07f:d=='c'?.07f:d=='w'||d=='h'?.05f:.035f;
                    litter*=(c.Pavement?1.5f:.55f)*(.25f+1.5f*clump);
                    float wear=Clump(ls,c.At,10f);
                    var rumour=RumourAt(w,c.At);
                    if(rumour!=null&&r<.05f+.35f*litter)key="rumour."+rumour;
                    else if(r<litter)key=Litter(d,Rand(rs,92,i));
                    else
                    {
                        float q=(r-litter)/(1-litter);
                        float crack=c.Pavement?.012f:.026f*(.4f+wear),patch=c.Pavement?.003f:.013f*(.4f+wear),oil=c.Pavement?0:(d=='w'||d=='h'?.03f:d=='o'?.012f:.009f),skid=c.Route>=0&&!c.Pavement?.012f:0;
                        float blood=c.Pavement?.006f:.007f,chalk=c.Pavement&&(d=='r'||d=='v'||d=='t')?.016f:0,scorch=c.Pavement?0:.0025f;
                        float a=0;
                        if(q<(a+=crack))key=Rand(rs,93,i)<.5f?"ground.crack.0":"ground.crack.1";
                        else if(q<(a+=patch))key="ground.patch";
                        else if(q<(a+=oil))key="ground.oil";
                        else if(q<(a+=skid))key="ground.skid";
                        else if(q<(a+=blood)){float b=Rand(rs,94,i);key=b<.4f?"blood.trail":b<.7f?"blood.pool":"blood.prints";}
                        else if(q<(a+=chalk)){key=Rand(rs,95,i)<.35f?"mark.chalk.0":"mark.chalk.1";tint=Chalk(h);}
                        else if(q<(a+=scorch))key="ground.scorch";
                    }
                    // on a pavement, litter and chalk turn to face the wall (a drift along the kerb)
                    if(c.Pavement&&key!=null&&(key.StartsWith("litter.",StringComparison.Ordinal)||key.StartsWith("mark.chalk",StringComparison.Ordinal)))yaw=YawOf(c.WallN);
                }
                if(key!=null)Add(w,plan,key,c.At,yaw,One,Life.None,h,d,-1,tint);
            }
        }
        private static string Litter(char d,float r)
        {
            switch(d)
            {
                case 's':case 'm':return r<.25f?"litter.paper":r<.5f?"litter.news":r<.68f?"litter.card":r<.78f?"litter.glass":r<.88f?"litter.cans":r<.95f?"litter.clothes":"litter.leaves";
                case 't':return r<.2f?"litter.paper":r<.4f?"litter.cans":r<.55f?"litter.clothes":r<.65f?"litter.card":r<.75f?"litter.glass":r<.9f?"litter.news":"litter.leaves";
                case 'r':case 'v':return r<.45f?"litter.leaves":r<.65f?"litter.paper":r<.75f?"litter.news":r<.85f?"litter.clothes":r<.95f?"litter.cans":"litter.glass";
                case 'c':return r<.45f?"litter.paper":r<.65f?"litter.news":r<.75f?"litter.clothes":r<.85f?"litter.card":"litter.leaves";
                case 'p':return r<.35f?"litter.paper":r<.6f?"litter.clothes":r<.8f?"litter.card":r<.9f?"litter.cans":"litter.news";
                default:return r<.3f?"litter.card":r<.5f?"litter.paper":r<.7f?"litter.cans":r<.8f?"litter.glass":r<.9f?"litter.clothes":"litter.news";
            }
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The survivors' trail home: flares at the routes' turns near the camp (most still burning), barrel fires against the walls
        // along the routes, flares by the gates
        // ------------------------------------------------------------------------------------------------------------------
        private static void Trail(World w,Plan plan)
        {
            uint rs=w.RSeed;
            for(int li=0;li+1<w.Legs.Count;li++)
            {
                var lg=w.Legs[li];var nx=w.Legs[li+1];if(nx.route!=lg.route)continue;float station=lg.station+new V3(lg.b.x-lg.a.x,0,lg.b.z-lg.a.z).Length;
                if(station>170f||Rand(rs,100,li)>.75f)continue;
                var a=new V3(lg.b.x-lg.a.x,0,lg.b.z-lg.a.z).Normalized;var b=new V3(nx.b.x-nx.a.x,0,nx.b.z-nx.a.z).Normalized;if(V3.Dot(a,b)>.85f)continue;
                // on the turn's outside, a metre and a half off the line
                var outside=(a-b).Normalized;var at=lg.b+outside*Math.Min(3.5f,lg.width*.3f);float r=Rand(rs,101,li);
                Add(w,plan,r<.65f?"flare":"flare.spent",at,Rand(rs,102,li)*6.2832f,One,r<.65f?Life.Burning:Life.Spent,Hash2(rs,(uint)li),'o');
            }
            V3[] gates={new V3(0,0,1),new V3(1,0,0),new V3(0,0,-1),new V3(-1,0,0)};
            for(int g=0;g<4;g++)
            {
                if(Rand(rs,103,g)>.6f)continue;var d=gates[g];var right=new V3(d.z,0,-d.x);var at=d*(29f+3f*Rand(rs,104,g))+right*((Rand(rs,105,g)<.5f?-1:1)*(6.5f+2f*Rand(rs,106,g)));
                bool lit=Rand(rs,107,g)<.7f;Add(w,plan,lit?"flare":"flare.spent",at,Rand(rs,108,g)*6.2832f,One,lit?Life.Burning:Life.Spent,Hash2(rs,(uint)(g+77)),'p');
            }
            // barrel fires against the walls along the routes, 40-230 m out, about one a 70 m of each route
            var lastFire=new Dictionary<int,float>();
            for(int i=0;i<w.Slots.Count;i++)
            {
                var s=w.Slots[i];if(s.Flat||!s.Open||s.Piped||s.Route<0||s.Station<40f||s.Station>230f)continue;
                if(lastFire.TryGetValue(s.Route,out float prev)&&Math.Abs(s.Station-prev)<60f)continue;
                uint h=Hash2(rs,(uint)(s.Block*16+s.Face)*409u+(uint)s.Bay);if(Rand(h,109,0)>.18f)continue;
                bool lit=Rand(h,110,0)<.55f;
                if(Add(w,plan,"low.drum",s.Foot+s.Along*((Rand(h,111,0)-.5f)*(s.W-.8f)),YawOf(s.N),One,lit?Life.Burning:Life.Spent,h,s.Style,s.Block))lastFire[s.Route]=s.Station;
            }
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The drawer's culling cells
        // ------------------------------------------------------------------------------------------------------------------
        private static void Index(Plan plan)
        {
            int n=plan.Nx*plan.Nz;plan.CellStart=new int[n+1];plan.Order=new int[plan.Pieces.Count];var cell=new int[plan.Pieces.Count];
            for(int i=0;i<plan.Pieces.Count;i++){cell[i]=plan.CellOf(plan.Pieces[i].At.x,plan.Pieces[i].At.z);plan.CellStart[cell[i]+1]++;}
            for(int c=0;c<n;c++)plan.CellStart[c+1]+=plan.CellStart[c];
            var fill=new int[n];for(int i=0;i<plan.Pieces.Count;i++){int c=cell[i];plan.Order[plan.CellStart[c]+fill[c]++]=i;}
        }
    }
}
