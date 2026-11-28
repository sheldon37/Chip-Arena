using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Meridian's street life (docs/expedition/MERIDIAN-STREETS.md; the owner, 2026-11-20: "streets seem very empty ... no life
    /// essentially like props or anything, it still feels kind of 'grey box-ish'"): a zombie-outbreak city at night, abandoned but
    /// recently lived in, drawn over the street dress (D1), the landmarks (D2) and the bleed (D3) without a single collider.
    ///
    /// Every piece is a kind of the bank's (<see cref="Kinds"/>, `rig/streets/&lt;key&gt;/&lt;tier&gt;` in ChipArenaCityStreets, built by
    /// tools/stages/meridian_streets.py) placed by one of six mounts, and each mount keeps the honesty rule (no invisible walls,
    /// nothing solid-looking a pilot can walk through):
    ///  - Floor: flat paint, <see cref="Flat"/> m or less, lying wholly on a floor's top: road markings, manholes and drains,
    ///    cracks, oil, litter, blood, chalk, the evacuation's stencils, flares;
    ///  - Wall: flat art on a collider's face, from just past the facade modules' 6 cm hugging band to <see cref="WallOut"/> m out:
    ///    posters, graffiti, boards over windows, notices, handprints, hazard paint;
    ///  - Low: small clutter standing on the floor against a wall, within <see cref="Reach"/> m of its face and under 1.6 m (the
    ///    camp's barrel fires' rule): bin bags, a bicycle, cardboard, sandbags, a half-drum fire, a meter box, a payphone hood, a
    ///    downpipe (its height the wall's);
    ///  - Upper: on a wall wholly <see cref="Clear"/> m or more over the floor under it: air conditioners, window boxes, awnings,
    ///    blade signs, survivors' banners, flags, Civil Defence floods;
    ///  - Span: across a street between two facing walls, every point <see cref="Clear"/> m or more over the floor: wires, washing
    ///    lines, bunting, festoons;
    ///  - Hung: hung from a span: the traffic signals stuck on amber.
    /// Nothing comes inside the camp, onto a cache or its approach, onto the landmarks' blocks or door fronts, into the bleed's drift
    /// band and beyond (the bleed's own dressing takes over there; its rumour band gets each instance's litter here, leading on to
    /// it), on another piece of paint, or onto the dress's own paint, lamps and plates.
    ///
    /// Deterministic per layout and run (no clock, no Unity): where the city's fittings stand (markings, drains, awnings, signs,
    /// wires) is the layout's; the story (litter, blood, posters, graffiti, banners, flares, fires) is the run's. Presentation
    /// only: never a collider, never sim data, nothing on the wire.
    /// </summary>
    public static partial class NativeMeridianStreetLife
    {
        public const string Version="meridian-streets-1";
        /// <summary>The bank file (Resources) the pieces draw from.</summary>
        public const string Bank="ChipArenaCityStreets";
        public enum Mount:byte{Floor,Wall,Low,Upper,Span,Hung}
        /// <summary>The rules (m): flat paint, the facade modules' hugging band (a wall piece starts past it), a wall piece's most,
        /// a low piece's most (the bible's 0.25 m), the clear height over a floor, a low piece's tallest, the bay.</summary>
        public const float Flat=.06f,HugOut=.06f,WallIn=.062f,WallOut=.10f,Reach=.25f,Clear=3.1f,LowTop=1.6f,Bay=2.5f;
        /// <summary>The floor's sampling cell (m) and the drawer's culling cell (m).</summary>
        public const float Cell=2f,CullCell=32f;
        /// <summary>What a piece's glow does at runtime: a lit sign, a traffic signal, a fire's coals, a flare, a flood, a festoon.</summary>
        public enum Glow:byte{None,Sign,Signal,Fire,Flare,Flood,Festoon}
        /// <summary>A piece's state: steady, flickering (signs: at most 2.7 steps a second; steady and slow with reduced effects),
        /// dead, blinking (the signals' amber at 1 Hz; steady with reduced effects), burning (fires and flares: under 2.4 Hz, under
        /// 0.7 Hz with reduced effects), spent (a flare burnt out, a cold drum), steaming (a grate).</summary>
        public enum Life:byte{None,Steady,Flicker,Dead,Blink,Burning,Spent,Steam}

        /// <summary>A kind: its bank key, mount, envelope at scale 1 in its own frame (Floor: centred on the floor's top, y up, z along
        /// the street; Wall, Low, Upper: the mount point on the collider face, +z out of the wall, the viewer's right -x; Span: from
        /// the cable's start, x across the street; Hung: from the hanging point, y down), whether it casts, its glow, its light
        /// (colour, the head in its frame, the pool's radius), and whether it stretches up its wall (a downpipe).</summary>
        public sealed class Kind
        {
            public readonly string Key;public readonly Mount Mount;public readonly V3 Min,Max,Head;public readonly bool Casts,Stretch;public readonly Glow Glow;public readonly uint Light;public readonly float Pool;
            public bool Lit=>Glow!=Glow.None;
            public Kind(string key,Mount mount,V3 min,V3 max,bool casts=false,Glow glow=Glow.None,uint light=0,V3 head=default,float pool=0,bool stretch=false)
            {Key=key;Mount=mount;Min=min;Max=max;Casts=casts;Glow=glow;Light=light;Head=head;Pool=pool;Stretch=stretch;}
        }
        private static V3 V(float x,float y,float z)=>new V3(x,y,z);
        /// <summary>The lights (art bible 3.2: warm light means people): a flare's red, a fire's orange, a sign's tungsten and neon,
        /// the signal's amber, a Civil Defence flood's cold white, a festoon's bulbs.</summary>
        public const uint FlareRed=0xFF3A26,FireOrange=0xFF8A3A,Tungsten=0xFFC27A,NeonPink=0xFF4FA8,NeonTeal=0x4FE8E0,Amber=0xFFB020,Flood=0xEAF2FF,Bulb=0xFFD08A;
        public static readonly Kind[] Kinds=
        {
            // ---- floor: road markings and fittings (the layout's) ----
            new Kind("road.dash",Mount.Floor,V(-.09f,0,-1),V(.09f,.012f,1)),
            new Kind("road.yellow",Mount.Floor,V(-.17f,0,-1),V(.17f,.012f,1)),
            new Kind("road.zebra",Mount.Floor,V(-1,0,-1),V(1,.012f,1)),
            new Kind("road.box",Mount.Floor,V(-1,0,-1),V(1,.012f,1)),
            new Kind("road.slow",Mount.Floor,V(-.9f,0,-1),V(.9f,.012f,1)),
            new Kind("road.bus",Mount.Floor,V(-1,0,-1),V(1,.012f,1)),
            new Kind("ground.manhole",Mount.Floor,V(-.42f,0,-.42f),V(.42f,.02f,.42f)),
            new Kind("ground.drain",Mount.Floor,V(-.32f,0,-.22f),V(.32f,.016f,.22f)),
            new Kind("ground.grate",Mount.Floor,V(-.72f,0,-.47f),V(.72f,.02f,.47f)),
            // ---- floor: the long night's wear and the story (the run's) ----
            new Kind("ground.crack.0",Mount.Floor,V(-1,0,-1),V(1,.008f,1)),
            new Kind("ground.crack.1",Mount.Floor,V(-1,0,-1),V(1,.008f,1)),
            new Kind("ground.patch",Mount.Floor,V(-1,0,-1),V(1,.01f,1)),
            new Kind("ground.oil",Mount.Floor,V(-1,0,-1),V(1,.008f,1)),
            new Kind("ground.skid",Mount.Floor,V(-1,0,-1),V(1,.008f,1)),
            new Kind("ground.scorch",Mount.Floor,V(-1,0,-1),V(1,.045f,1)),
            new Kind("litter.paper",Mount.Floor,V(-1,0,-1),V(1,.03f,1)),
            new Kind("litter.news",Mount.Floor,V(-1,0,-1),V(1,.03f,1)),
            new Kind("litter.leaves",Mount.Floor,V(-1,0,-1),V(1,.03f,1)),
            new Kind("litter.glass",Mount.Floor,V(-1,0,-1),V(1,.02f,1)),
            new Kind("litter.cans",Mount.Floor,V(-1,0,-1),V(1,.05f,1)),
            new Kind("litter.clothes",Mount.Floor,V(-1,0,-1),V(1,.05f,1)),
            new Kind("litter.card",Mount.Floor,V(-1,0,-1),V(1,.04f,1)),
            new Kind("blood.pool",Mount.Floor,V(-1,0,-1),V(1,.008f,1)),
            new Kind("blood.trail",Mount.Floor,V(-1,0,-1),V(1,.008f,1)),
            new Kind("blood.prints",Mount.Floor,V(-1,0,-1),V(1,.008f,1)),
            new Kind("mark.chalk.0",Mount.Floor,V(-1,0,-1),V(1,.006f,1)),
            new Kind("mark.chalk.1",Mount.Floor,V(-1,0,-1),V(1,.006f,1)),
            new Kind("mark.cd.0",Mount.Floor,V(-1,0,-1),V(1,.008f,1)),
            new Kind("mark.cd.1",Mount.Floor,V(-1,0,-1),V(1,.008f,1)),
            new Kind("ground.tent",Mount.Floor,V(-1,0,-1),V(1,.012f,1)),
            new Kind("ground.ash",Mount.Floor,V(-1,0,-1),V(1,.04f,1)),
            new Kind("flare",Mount.Floor,V(-.3f,0,-.3f),V(.3f,.04f,.3f),false,Glow.Flare,FlareRed,V(0,.03f,.16f),3.6f),
            new Kind("flare.spent",Mount.Floor,V(-.3f,0,-.3f),V(.3f,.04f,.3f)),
            // the instances' rumour (110-160 m out along a door's route): each one's litter, before the bleed's own dressing begins
            new Kind("rumour.level0",Mount.Floor,V(-1,0,-1),V(1,.03f,1)),
            new Kind("rumour.poolrooms",Mount.Floor,V(-1,0,-1),V(1,.02f,1)),
            new Kind("rumour.wardzero",Mount.Floor,V(-1,0,-1),V(1,.04f,1)),
            new Kind("rumour.station",Mount.Floor,V(-1,0,-1),V(1,.03f,1)),
            new Kind("rumour.ironworks",Mount.Floor,V(-1,0,-1),V(1,.04f,1)),
            // ---- wall: flat art past the facades' hugging band ----
            new Kind("poster.stay",Mount.Wall,V(-.45f,1.2f,WallIn),V(.45f,2.35f,.08f)),
            new Kind("poster.missing",Mount.Wall,V(-.27f,1.3f,WallIn),V(.27f,2.05f,.08f)),
            new Kind("poster.cd",Mount.Wall,V(-.46f,1.25f,WallIn),V(.46f,2.3f,.08f)),
            new Kind("poster.bills",Mount.Wall,V(-1.1f,.9f,WallIn),V(1.1f,2.5f,.088f)),
            new Kind("graffiti.tag.0",Mount.Wall,V(-1.15f,.35f,WallIn),V(1.15f,2.25f,.07f)),
            new Kind("graffiti.tag.1",Mount.Wall,V(-1.15f,.35f,WallIn),V(1.15f,2.25f,.07f)),
            new Kind("graffiti.msg.0",Mount.Wall,V(-1.2f,.8f,WallIn),V(1.2f,2.6f,.07f)),
            new Kind("graffiti.msg.1",Mount.Wall,V(-1.2f,.8f,WallIn),V(1.2f,2.6f,.07f)),
            new Kind("graffiti.msg.2",Mount.Wall,V(-1.2f,.8f,WallIn),V(1.2f,2.6f,.07f)),
            new Kind("graffiti.msg.3",Mount.Wall,V(-1.2f,.8f,WallIn),V(1.2f,2.6f,.07f)),
            new Kind("graffiti.x",Mount.Wall,V(-.6f,1.05f,WallIn),V(.6f,2.35f,.07f)),
            new Kind("wall.boards",Mount.Wall,V(-1.05f,.25f,WallIn),V(1.05f,2.6f,WallOut)),
            new Kind("wall.notice",Mount.Wall,V(-.25f,1.3f,WallIn),V(.25f,1.88f,.07f)),
            new Kind("wall.hand",Mount.Wall,V(-.55f,.65f,WallIn),V(.55f,1.95f,.066f)),
            new Kind("wall.hazard",Mount.Wall,V(-1.25f,0,WallIn),V(1.25f,.6f,.068f)),
            new Kind("wall.stencil",Mount.Wall,V(-.95f,1.15f,WallIn),V(.95f,1.95f,.068f)),
            // ---- low: clutter against a wall, within 0.25 m of its face ----
            new Kind("low.binbags",Mount.Low,V(-.95f,0,WallIn),V(.95f,.9f,Reach),true),
            new Kind("low.bike",Mount.Low,V(-.95f,0,WallIn),V(.95f,1.08f,Reach),true),
            new Kind("low.card",Mount.Low,V(-.62f,0,WallIn),V(.62f,1.15f,Reach)),
            new Kind("low.sandbags",Mount.Low,V(-1.22f,0,WallIn),V(1.22f,.66f,Reach),true),
            new Kind("low.drum",Mount.Low,V(-.34f,0,WallIn),V(.34f,.98f,Reach),true,Glow.Fire,FireOrange,V(0,.92f,.16f),4.2f),
            new Kind("low.meter",Mount.Low,V(-.3f,0,WallIn),V(.3f,1.6f,Reach)),
            new Kind("low.phone",Mount.Low,V(-.4f,.6f,WallIn),V(.4f,1.6f,Reach)),
            new Kind("low.pipe",Mount.Low,V(-.1f,0,WallIn),V(.1f,1,.2f),false,Glow.None,0,default,0,true),
            // ---- upper: on a wall, wholly 3.1 m or more over the floor under it ----
            new Kind("upper.ac",Mount.Upper,V(-.44f,-.56f,WallIn),V(.44f,.04f,.64f),true),
            new Kind("upper.box",Mount.Upper,V(-.62f,-.12f,.13f),V(.62f,.52f,.46f),true),
            new Kind("upper.awning",Mount.Upper,V(-1.22f,-.3f,WallIn),V(1.22f,.04f,1.26f),true),
            new Kind("upper.awning.torn",Mount.Upper,V(-1.22f,-.3f,WallIn),V(1.22f,.04f,1.26f),true),
            new Kind("upper.sign.0",Mount.Upper,V(-.07f,-.82f,WallIn),V(.07f,.3f,1.16f),true,Glow.Sign,Tungsten,V(0,-.3f,.66f),2.6f),
            new Kind("upper.sign.1",Mount.Upper,V(-.07f,-.82f,WallIn),V(.07f,.3f,1.16f),true,Glow.Sign,NeonPink,V(0,-.3f,.66f),2.6f),
            new Kind("upper.sign.2",Mount.Upper,V(-.07f,-.82f,WallIn),V(.07f,.3f,1.16f),true,Glow.Sign,NeonTeal,V(0,-.3f,.66f),2.6f),
            new Kind("upper.sign.3",Mount.Upper,V(-.07f,-.82f,WallIn),V(.07f,.3f,1.16f),true,Glow.Sign,Tungsten,V(0,-.3f,.66f),2.6f),
            new Kind("upper.banner.0",Mount.Upper,V(-1.02f,-2.62f,.12f),V(1.02f,.06f,.22f)),
            new Kind("upper.banner.1",Mount.Upper,V(-1.02f,-2.62f,.12f),V(1.02f,.06f,.22f)),
            new Kind("upper.banner.2",Mount.Upper,V(-1.02f,-2.62f,.12f),V(1.02f,.06f,.22f)),
            // (the plaza's floods throw 13 m pools, their light reaching about 21 m out over the square: 9 m until 2026-11-21, when the
            // open grounds read as a dark void at night; owner: "Theres very big empty areas")
            new Kind("upper.flood",Mount.Upper,V(-.38f,-.64f,WallIn),V(.38f,.36f,1.36f),true,Glow.Flood,Flood,V(0,-.36f,1.06f),13f),
            new Kind("upper.flag",Mount.Upper,V(-.08f,-1.5f,WallIn),V(.08f,.2f,1.62f),true),
            // the traffic signal on a high arm over the road, its head both ways along the street, stuck on amber
            new Kind("upper.signal",Mount.Upper,V(-.26f,-1.36f,WallIn),V(.26f,.28f,2.62f),true,Glow.Signal,Amber,V(0,-.74f,2.36f),2.4f),
            // ---- spans: across a street between two facing walls ----
            new Kind("span.wires",Mount.Span,V(0,-.62f,-.14f),V(1,.03f,.14f)),
            new Kind("span.washing",Mount.Span,V(0,-1.12f,-.16f),V(2,.03f,.16f)),
            new Kind("span.bunting",Mount.Span,V(0,-.48f,-.05f),V(2,.03f,.05f)),
            new Kind("span.festoon",Mount.Span,V(0,-.36f,-.08f),V(2,.03f,.08f),false,Glow.Festoon,Bulb,V(1,-.2f,0),1.6f),
            // ---- hung from a span ----
            new Kind("hung.signal",Mount.Hung,V(-.26f,-1.22f,-.21f),V(.26f,0,.21f),true,Glow.Signal,Amber,V(0,-.62f,.19f),2.2f),
        };
        private static readonly Dictionary<string,int> kindIndex=IndexKinds();
        private static Dictionary<string,int> IndexKinds(){var d=new Dictionary<string,int>(StringComparer.Ordinal);for(int i=0;i<Kinds.Length;i++)d.Add(Kinds[i].Key,i);return d;}
        public static int KindOf(string key)=>key!=null&&kindIndex.TryGetValue(key,out int i)?i:-1;
        /// <summary>The planner's reasons for refusing a piece, in <see cref="Plan.Refused"/>'s order.</summary>
        public static readonly string[] Refusals={"camp","cache","landmark or door front","bleed","paint on paint","the dress's or bleed's paint","off the floor","off its face","in a block","the dress's or bleed's fitting","another piece","not on the floor in front","under 3.1 m"};

        /// <summary>A placed piece: its kind, place (Floor: on the floor's top; Wall, Low, Upper: on the collider face), turn about +y
        /// (radians: its +z faces (sin, 0, cos)), scale, state, tint (0: as banked), its own seed (variation, flicker phase), its
        /// district (the nearest lot's style: r rows, v villas, s shops, m market, t tenements, w warehouses, h sheds, c civic; o open
        /// ground, p the camp's precinct) and the block it stands against (-1: none).</summary>
        public struct Piece
        {
            public int Kind;public V3 At;public float Yaw;public V3 Scale;public Life Life;public uint Tint,Seed;public char District;public int Block;
        }
        /// <summary>The world matrix's parts of a piece: its kind's frame turned by Yaw, scaled, at At.</summary>
        public static V3 Local(in Piece p,V3 local)
        {
            float c=(float)Math.Cos(p.Yaw),s=(float)Math.Sin(p.Yaw);float x=local.x*p.Scale.x,y=local.y*p.Scale.y,z=local.z*p.Scale.z;
            return new V3(p.At.x+x*c+z*s,p.At.y+y,p.At.z-x*s+z*c);
        }
        /// <summary>A piece's world box (its kind's envelope, turned and scaled).</summary>
        public static void Bounds(in Piece p,out V3 lo,out V3 hi)
        {
            var k=Kinds[p.Kind];lo=new V3(1e9f,1e9f,1e9f);hi=new V3(-1e9f,-1e9f,-1e9f);
            for(int i=0;i<8;i++)
            {
                var c=Local(p,new V3((i&1)==0?k.Min.x:k.Max.x,(i&2)==0?k.Min.y:k.Max.y,(i&4)==0?k.Min.z:k.Max.z));
                lo=new V3(Math.Min(lo.x,c.x),Math.Min(lo.y,c.y),Math.Min(lo.z,c.z));hi=new V3(Math.Max(hi.x,c.x),Math.Max(hi.y,c.y),Math.Max(hi.z,c.z));
            }
        }
        /// <summary>The yaw whose +z faces a horizontal direction.</summary>
        public static float YawOf(V3 dir)=>(float)Math.Atan2(dir.x,dir.z);

        /// <summary>A plan: the pieces, and the drawer's culling cells (CullCell m, the pieces' indices in each).</summary>
        public sealed class Plan
        {
            public uint Layout,Run;
            public readonly List<Piece> Pieces=new List<Piece>();
            public float MinX,MinZ;public int Nx,Nz;
            /// <summary>Each culling cell's pieces: Order[CellStart[c] .. CellStart[c+1]).</summary>
            public int[] CellStart,Order;
            /// <summary>The planner's counts for the report: wall bays found, floor cells, spans tried, pieces kept out.</summary>
            public int Bays,FloorCells,Spans,KeptOut;
            /// <summary>Why the planner refused pieces (the report's diagnosis): by <see cref="Refusals"/>.</summary>
            public readonly int[] Refused=new int[Refusals.Length];
            public int CellOf(float x,float z){int cx=Math.Max(0,Math.Min(Nx-1,(int)Math.Floor((x-MinX)/CullCell)));int cz=Math.Max(0,Math.Min(Nz-1,(int)Math.Floor((z-MinZ)/CullCell)));return cx+cz*Nx;}
            /// <summary>A digest of the whole plan (kinds, places, states, tints): the determinism checks compare it.</summary>
            public ulong Digest()
            {
                ulong h=1469598103934665603UL;void F(float f){h^=(ulong)BitConverter.SingleToInt32Bits(f)&0xffffffffUL;h*=1099511628211UL;}void U(uint u){h^=u;h*=1099511628211UL;}
                foreach(var p in Pieces){U((uint)p.Kind);F(p.At.x);F(p.At.y);F(p.At.z);F(p.Yaw);F(p.Scale.x);F(p.Scale.y);F(p.Scale.z);U((uint)p.Life);U(p.Tint);U(p.Seed);}
                return h;
            }
            /// <summary>How many pieces of each mount.</summary>
            public int Count(Mount m){int n=0;foreach(var p in Pieces)if(Kinds[p.Kind].Mount==m)n++;return n;}
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The light: one function every peer, the drawer and the checks share (photosensitivity: reduced effects never flash
        // faster than 3 Hz; nothing here does even without them)
        // ------------------------------------------------------------------------------------------------------------------
        /// <summary>A lit piece's level at time t (s, 0 dark .. 1 full): steady 1; a flickering sign stutters at most 2.7 steps a
        /// second (the dress's lamp rule), now and then dropping out, and with reduced effects only breathes slowly (0.35 Hz); a signal
        /// blinks its amber once a second, eased (steady with reduced effects); a fire or a flare dances under 2.4 Hz (under 0.7 Hz with
        /// reduced effects); dead, spent and unlit pieces are dark.</summary>
        public static float Level(Life life,uint seed,float t,bool reduced)
        {
            float phase=(seed&1023)/1023f;
            switch(life)
            {
                case Life.Steady:return 1;
                case Life.Flicker:
                {
                    if(reduced)return .8f+.2f*(float)Math.Sin(2*Math.PI*(.35f*t+phase));
                    int k=(int)Math.Floor(t*2.7f+phase*17f);float h=(NativeMeridianDress.Mix((uint)k*2654435761u^seed)&0xffffff)/16777216f;
                    return h<.3f?.06f+.14f*h:h<.42f?.5f:1f;
                }
                case Life.Blink:
                {
                    if(reduced)return .72f;
                    float c=t+phase;c-=(float)Math.Floor(c);
                    // on for half the second, eased in and out over a tenth (one flash a second)
                    float on=c<.1f?c/.1f:c<.5f?1f:c<.6f?1f-(c-.5f)/.1f:0f;return .08f+.92f*on*on*(3-2*on);
                }
                case Life.Burning:
                {
                    float a=reduced?.31f:1.13f,b=reduced?.53f:1.87f,c=reduced?.67f:2.37f;double w=2*Math.PI;
                    return .78f+.12f*(float)Math.Sin(w*(a*t+phase))+.07f*(float)Math.Sin(w*(b*t+phase*3.1f))+.03f*(float)Math.Sin(w*(c*t+phase*5.7f));
                }
                case Life.Steam:return 1;
                default:return 0;
            }
        }
        /// <summary>The highest rate (Hz) <see cref="Level"/> can change at for a state (the checks hold it under 3 with reduced effects).</summary>
        public static float Rate(Life life,bool reduced)=>life==Life.Flicker?(reduced?.35f:2.7f):life==Life.Blink?(reduced?0:1):life==Life.Burning?(reduced?.67f:2.37f):0;
    }
}
