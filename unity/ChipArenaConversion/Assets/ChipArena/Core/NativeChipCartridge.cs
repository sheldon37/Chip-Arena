using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The chip's design as a "neural core cartridge", shared by the cards and every 2D chip (Runtime NativeChipArt,
    // NativeChipCard; baked by NativeChipBake) and the dispensed / held 3D chip (NativeMachineModels.Pickup): a chamfered
    // cartridge (big shoulder cuts on top, small cuts below) in a machined, faceted metal bezel with a rarity inlay line; a
    // dark anodised body with etched traces radiating from the core; a hexagonal glass window over a glowing crystal (the
    // chip's own icon projected inside it); and a gold edge connector along the bottom in place of legs. The tier escalates
    // in shape and ornament as well as colour (so it reads without colour): Common plain brushed steel; Uncommon lit seams;
    // Rare a second inlay and holographic glass; Epic side fins and an orbiting ring; Legendary a gold-plated bezel, a
    // filigree crown and corner gems; Mythic a fractured crimson crystal, drifting foil and shards. The exclusive kits keep
    // their colours with a crest of their own (Mishima's twin bolts, Ansatsuken's headband knot, L44T's terminal brackets,
    // Siren's black flames). Rainbow (the 8-choice pick, dispensed chips only) is a prism. Engine-free and deterministic: the
    // checks cover the tier table, the colours against GameContent's rarities, the geometry and every animation curve.
    public static class NativeChipCartridge
    {
        /// <summary>The chip tiers in rank order (NativeChipRanks.RarityRank), then the dispensed-only rainbow tier.</summary>
        public static readonly string[] Tiers={"common","uncommon","rare","epic","legendary","mythic","mishima","ansatsuken","l44t","siren"};
        public const string Rainbow="rainbow";
        public enum Metal{Steel,Titanium,Gold,BlackChrome,Gunmetal,Lacquer,Phosphor,Void,Prism}
        public enum Crest{None,Crown,Shards,Bolts,Knot,Terminal,Flames,Prism}
        // A tier's look. Hex colours (sRGB): Rarity the scheme's colour (GameContent rarities, exactly); Inlay the lit line and
        // seams; Core / CoreHi / CoreLo the crystal's light, bright and deep facets; Accent the crest, fins' edges and sparks;
        // Body the anodised face; MetalHi / MetalMid / MetalLo the bezel's lit, flat and shadow tones. Rank 1-6 (0 for the
        // kits): the pips and the ornament count. Breath s per core pulse; Depth its swing.
        public readonly struct Look
        {
            public readonly string Tier;public readonly int Rarity,Inlay,Core,CoreHi,CoreLo,Accent,Body,MetalHi,MetalMid,MetalLo,Rank;public readonly Metal Metal;public readonly Crest Crest;
            public readonly bool Seams,SecondInlay,Holo,Fins,Ring,Gems,Fractured,Foil,Embers,Brushed;public readonly float Breath,Depth;
            public Look(string tier,int rarity,int inlay,int core,int coreHi,int coreLo,int accent,int body,int hi,int mid,int lo,int rank,Metal metal,Crest crest,bool seams,bool second,bool holo,bool fins,bool ring,bool gems,bool fractured,bool foil,bool embers,bool brushed,float breath,float depth)
            {Tier=tier;Rarity=rarity;Inlay=inlay;Core=core;CoreHi=coreHi;CoreLo=coreLo;Accent=accent;Body=body;MetalHi=hi;MetalMid=mid;MetalLo=lo;Rank=rank;Metal=metal;Crest=crest;Seams=seams;SecondInlay=second;Holo=holo;Fins=fins;Ring=ring;Gems=gems;Fractured=fractured;Foil=foil;Embers=embers;Brushed=brushed;Breath=breath;Depth=depth;}
            /// <summary>How many tier ornaments it wears (seams, second inlay, holo glass, fins, ring, crest, gems, fracture, foil, embers).</summary>
            public int Ornaments=>(Seams?1:0)+(SecondInlay?1:0)+(Holo?1:0)+(Fins?1:0)+(Ring?1:0)+(Crest!=Crest.None?1:0)+(Gems?1:0)+(Fractured?1:0)+(Foil?1:0)+(Embers?1:0);
        }
        /// <summary>The scheme's rarity colour (sRGB hex): GameContent's rarities, as NativeChipCard.Rarity and NativeMachineModels.RarityColors.</summary>
        public static int Rarity(string tier)=>tier=="mishima"?0xb3b5c2:tier=="ansatsuken"?0xff354e:tier=="l44t"?0x4dff83:tier=="siren"?0xb7a8ff:tier=="uncommon"?0x79e59a:tier=="rare"?0x70b5ff:tier=="epic"?0xce8aff:tier=="legendary"?0xffae55:tier=="mythic"?0xff6475:0xf1f5f9;
        public static bool Known(string tier)=>Array.IndexOf(Tiers,tier)>=0||tier==Rainbow;
        public static Look Of(string tier)
        {
            switch(tier)
            {
                //                         rarity    inlay    core     coreHi   coreLo   accent   body     metalHi  mid      lo       rank metal              crest        seams second holo  fins  ring  gems  frac  foil  embers brushed breath depth
                case "uncommon":return new Look(tier,0x79e59a,0x8dffb0,0x5fdc86,0xd2ffe0,0x0f4a26,0xa6f5c0,0x0c1712,0xe2ece6,0x8b968f,0x2f3833,2,Metal.Steel,Crest.None,true,false,false,false,false,false,false,false,false,true,2.9f,.42f);
                case "rare":return new Look(tier,0x70b5ff,0x8cc6ff,0x5aa6ff,0xd6ebff,0x0f2e5c,0xa8d4ff,0x0b1220,0xdde8f6,0x8392a8,0x2a3446,3,Metal.Steel,Crest.None,true,true,true,false,false,false,false,false,false,true,2.6f,.46f);
                case "epic":return new Look(tier,0xce8aff,0xdca6ff,0xb46cf5,0xf1dcff,0x3a1260,0xe0b4ff,0x120d1c,0xd9cdf0,0x6d5d92,0x251c38,4,Metal.Titanium,Crest.None,true,true,true,true,true,false,false,false,false,false,2.3f,.5f);
                case "legendary":return new Look(tier,0xffae55,0xffcf7a,0xffa63d,0xfff0c8,0x6a3608,0xffd88a,0x161009,0xfff1c4,0xd9a441,0x6e4a12,5,Metal.Gold,Crest.Crown,true,true,true,true,true,true,false,false,true,false,2.1f,.52f);
                case "mythic":return new Look(tier,0xff6475,0xff8a96,0xff3a52,0xffd6dc,0x5a0716,0xffb3bd,0x150b10,0xffa4b0,0x2e2430,0x0a070c,6,Metal.BlackChrome,Crest.Shards,true,true,true,true,true,true,true,true,false,false,1.8f,.58f);
                case "mishima":return new Look(tier,0xb3b5c2,0xd9dbe3,0xff3c5d,0xffd0d8,0x5c0718,0xff2448,0x101014,0xe4e6ee,0x6a6c78,0x1e1f26,0,Metal.Gunmetal,Crest.Bolts,false,false,false,false,false,false,false,false,false,true,2.2f,.5f);
                case "ansatsuken":return new Look(tier,0xff354e,0xff8a97,0xff4d63,0xffd6da,0x5e0a18,0xffb2bc,0x170a0e,0xffc4cb,0x8a1d2e,0x2c0910,0,Metal.Lacquer,Crest.Knot,false,false,false,false,false,false,false,false,false,false,2.0f,.55f);
                case "l44t":return new Look(tier,0x4dff83,0x8affb0,0x45f57d,0xd4ffe2,0x063d1a,0x9effbd,0x050c08,0x9fe6b8,0x223a2c,0x0a140e,0,Metal.Phosphor,Crest.Terminal,false,false,false,false,false,false,false,false,false,false,1.6f,.6f);
                case "siren":return new Look(tier,0xb7a8ff,0xc9bfff,0x9b87ff,0xf3efff,0x140c2e,0xd9d0ff,0x07060b,0xc9bfff,0x2a2342,0x07060b,0,Metal.Void,Crest.Flames,false,false,false,false,false,false,false,false,false,false,3.0f,.6f);
                case Rainbow:return new Look(tier,0xffffff,0xffffff,0xffffff,0xffffff,0x303040,0xffffff,0x0c0c12,0xf4f6ff,0x9aa0b4,0x2c3040,6,Metal.Prism,Crest.Prism,true,true,true,true,true,false,false,true,false,false,1.9f,.55f);
                default:return new Look("common",0xf1f5f9,0xe6edf6,0xc9d3e2,0xffffff,0x2a3242,0xc9d4e6,0x0d1118,0xe8edf4,0x8a93a3,0x2c323d,1,Metal.Steel,Crest.None,false,false,false,false,false,false,false,false,false,true,3.2f,.38f);
            }
        }

        // ---- the silhouette (the card's and the 3D chip's outline) ----
        // The card outline in its rect: shoulder cuts `top` on the two upper corners, `bottom` on the two lower ones (virtual px).
        // Tray cards use TopCut/BottomCut, the spotlight card the large pair; the badge and the 3D chip take the shape in
        // proportion (UnitOutline).
        public const float TopCut=13,BottomCut=6,TopCutLarge=18,BottomCutLarge=8,Bezel=6,BezelLarge=8;
        /// <summary>The edge connector under the card: its height below the card (px), the pads' pitch and width, the inset from the bottom cuts.</summary>
        public const float ConnectorHeight=9,PadPitch=8,PadWidth=4.8f,ConnectorInset=16;
        /// <summary>The octagon's corners clockwise from the top edge's left end (x right, y down), for a w x h rect at the origin.</summary>
        public static Vector2[] Outline(float w,float h,float top,float bottom)
        {
            return new[]{new Vector2(top,0),new Vector2(w-top,0),new Vector2(w,top),new Vector2(w,h-bottom),new Vector2(w-bottom,h),new Vector2(bottom,h),new Vector2(0,h-bottom),new Vector2(0,top)};
        }
        /// <summary>The outline's length (the holo scan's trace and comet run along it).</summary>
        public static float Perimeter(float w,float h,float top,float bottom)=>(w-2*top)+(w-2*bottom)+2*(h-top-bottom)+2*Root2*(top+bottom);
        public const float Root2=1.41421356f;
        /// <summary>The point `d` along the outline (wrapping) from the top edge's left end, clockwise, inset `inset` px.</summary>
        public static Vector2 Along(float w,float h,float top,float bottom,float d,float inset=0)
        {
            var o=Outline(w,h,top,bottom);float L=Perimeter(w,h,top,bottom);d-=(float)Math.Floor(d/L)*L;
            for(int i=0;i<8;i++)
            {
                var a=o[i];var b=o[(i+1)%8];float len=Vector2.Distance(a,b);
                if(d<=len||i==7){var dir=len>0?(b-a)/len:Vector2.UnitX;var p=a+dir*Math.Min(d,len);return p+new Vector2(-dir.Y,dir.X)*inset;}
                d-=len;
            }
            return o[0];
        }
        /// <summary>The 3D chip's outline in model units: the card's proportions stood upright (narrower than tall), centred.</summary>
        public static Vector2[] UnitOutline(float w,float h,float top,float bottom)
        {
            var o=Outline(w,h,top,bottom);var r=new Vector2[8];for(int i=0;i<8;i++)r[i]=new Vector2(o[i].X-w/2,h/2-o[i].Y);return r;
        }
        /// <summary>A hexagon's corners, pointy-top (`pointy`) or flat-top, radius `r` about `c` (x right, y down).</summary>
        public static Vector2[] Hex(Vector2 c,float r,bool pointy)
        {
            var p=new Vector2[6];for(int i=0;i<6;i++){double a=(pointy?-90:0)+60*i;a*=Math.PI/180;p[i]=c+new Vector2((float)Math.Cos(a)*r,(float)Math.Sin(a)*r);}return p;
        }

        // ---- the etched traces: polylines radiating from the core (in core radii, x right, y down), PCB style (45-degree bends) --
        // The baked trace layer and the light packets that run along it share them, so the sweep rides the drawn traces.
        public static readonly Vector2[][] Traces=
        {
            new[]{new Vector2(1.02f,.35f),new Vector2(1.6f,.35f),new Vector2(2.05f,.8f),new Vector2(8.6f,.8f)},
            new[]{new Vector2(.92f,.62f),new Vector2(1.55f,1.25f),new Vector2(5.4f,1.25f),new Vector2(6.1f,1.95f),new Vector2(9.2f,1.95f)},
            new[]{new Vector2(.62f,.92f),new Vector2(.95f,1.25f),new Vector2(.95f,2.3f),new Vector2(1.6f,2.95f),new Vector2(7.4f,2.95f),new Vector2(8.1f,3.65f),new Vector2(9.4f,3.65f)},
            new[]{new Vector2(.15f,1.08f),new Vector2(.15f,3.4f),new Vector2(.9f,4.15f),new Vector2(5.2f,4.15f)},
            new[]{new Vector2(-.42f,1.02f),new Vector2(-.42f,4.6f)},
            new[]{new Vector2(1.06f,-.1f),new Vector2(1.5f,-.1f)},
        };
        /// <summary>A point `u` (0..1 of its length) along trace `i`, in core radii.</summary>
        public static Vector2 OnTrace(int i,float u)
        {
            var t=Traces[i];float total=0;for(int k=1;k<t.Length;k++)total+=Vector2.Distance(t[k-1],t[k]);float d=Clamp01(u)*total;
            for(int k=1;k<t.Length;k++){float len=Vector2.Distance(t[k-1],t[k]);if(d<=len)return Vector2.Lerp(t[k-1],t[k],len>0?d/len:0);d-=len;}
            return t[t.Length-1];
        }

        // ---- animation (seconds on the screen clock; calm = Reduced effects: everything rests where it settles) ----
        /// <summary>The core's breath 0..1 (a smooth cosine; calm holds the middle).</summary>
        public static float Breath(in Look l,float t,bool calm)=>calm?.5f:.5f-.5f*(float)Math.Cos(2*Math.PI*t/l.Breath);
        /// <summary>The core light's strength: its rest, swinging by the tier's depth with the breath, raised by `charge`.</summary>
        public static float CoreLight(in Look l,float t,bool calm,float charge)=>(1-l.Depth*.5f)+l.Depth*Breath(l,t,calm)+.6f*Clamp01(charge);
        /// <summary>The light sweep along the traces: every SweepEvery s (offset per chip by `phase`), a packet runs out from the
        /// core over SweepTime s. False between sweeps (and calm); else the packet's eased progress 0..1.</summary>
        public const float SweepEvery=3.8f,SweepTime=1.1f;
        public static bool Sweep(float t,float phase,bool calm,out float u)
        {
            u=0;if(calm)return false;float k=t+phase*SweepEvery;k-=(float)Math.Floor(k/SweepEvery)*SweepEvery;if(k>=SweepTime)return false;u=NativeUiMotion.Bezier(.35f,0,.3f,1,k/SweepTime);return true;
        }
        /// <summary>A stable 0..1 phase per chip id (FNV-1a), so a row of cards does not sweep in step. Allocates nothing.</summary>
        public static float Phase(string id)
        {
            if(string.IsNullOrEmpty(id))return 0;uint h=2166136261;foreach(char c in id){h^=c;h*=16777619;}return (h%1000)/1000f;
        }
        /// <summary>The glass sheen's pass across the dome, `age` s after the highlight engaged (or the periodic gleam): false
        /// outside it; else the streak's position -1..1 across the dome.</summary>
        public const float SheenTime=.6f,GleamEvery=4.6f;
        public static bool Sheen(float age,out float x)
        {
            x=0;if(age<0||age>=SheenTime)return false;x=-1.25f+2.5f*NativeUiMotion.Bezier(.3f,.6f,.2f,1,age/SheenTime);return true;
        }
        /// <summary>The ring's orbit angle (radians) about the core (Epic and up; calm stands still at a three-quarter view).</summary>
        public static float Orbit(float t,bool calm)=>calm?.9f:(float)(2*Math.PI*(t/5.5f-Math.Floor(t/5.5f)));
        // The reveal's assembly at `u` (0 just appeared .. 1 settled; NativeChipCard.Entrance's own timing): the bezel's pieces
        // snap in from Spread px out by 50%, the core ignites (dark, a flash, settled) by 70%, the connector's pads light left to
        // right over the last 30%.
        public const float Spread=16;
        public static float FrameOffset(float u)=>Spread*(1-NativeUiMotion.Bezier(.2f,.9f,.3f,1.15f,Clamp01(u/.5f)));
        public static float FrameAlpha(float u)=>Clamp01(u/.18f);
        static readonly float[] IgniteAt={0,.3f,.42f,.5f,.58f,.7f},IgniteValue={0,0,1.8f,.5f,1.25f,1};
        public static float Ignite(float u)=>u>=1?1:NativeUiMotion.Keys(Clamp01(u),IgniteAt,IgniteValue);
        /// <summary>How lit pad `i` of `n` is as the assembly finishes (1 settled; the last 30% runs left to right).</summary>
        public static float PadLit(float u,int i,int n)=>u>=1?1:Clamp01((u-.7f)/.3f*(n+3)-i);
        // Particles (cards): Legendary's embers rising off the crown and top edge, Mythic's shards drifting round the bezel. `k`
        // the particle's 0..1 life on its own period.
        public static int Embers(in Look l)=>l.Embers?7:0;
        public static int Shards(in Look l)=>l.Fractured?6:0;
        public static float Life(float t,int i,float period)=>(float)((t/period+i*.618034)-Math.Floor(t/period+i*.618034));
        /// <summary>A particle's opacity over its life: in over the first 20%, out over the rest.</summary>
        public static float LifeAlpha(float k)=>k<.2f?k/.2f:1-(k-.2f)/.8f;
        static float Clamp01(float v)=>v<0?0:v>1?1:v;
    }
}
