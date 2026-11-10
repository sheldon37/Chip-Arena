using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Top Dog Emporium (map `emporium`, docs/arenas/EMPORIUM.md): a glass-roofed hot dog palace at golden hour. Shared here by its
    /// presentation (Core NativeEmporiumKit, NativeEmporiumRig, NativeEmporiumFx, NativeEmporiumScenery, NativeEmporiumSound; Runtime
    /// NativeEmporiumPresentation and NativeEmporiumAudio): which map this is, the kit kinds its parts draw with, resolved per look
    /// from MapLooks.json `kitLooks` ("emporium-kit-*" ids; an unlisted kind takes <see cref="Defaults"/>), the palette, and the few
    /// places the rig, the effects and the scenery agree on (the big turning sign, the roof, the sun's shafts). Presentation only:
    /// none of this is sim data, collides, damages or enters the network frame. Pure.
    /// </summary>
    public static class NativeEmporiumLooks
    {
        public const string MapId="emporium",Look="emporium-golden-hour";
        /// <summary>Whether a map id is the Emporium: every Emporium hook is inert on any other map.</summary>
        public static bool Active(string mapId)=>mapId==MapId;

        /// <summary>Kit kinds (index = kind), resolved once per look; at most NativeFactoryKit.Kinds.Length (the Looks arrays).</summary>
        public static readonly string[] Kinds={"emporium-kit-dark","emporium-kit-chrome","emporium-kit-red","emporium-kit-yellow","emporium-kit-cream","emporium-kit-teal",
            "emporium-kit-pink","emporium-kit-wood","emporium-kit-sausage","emporium-kit-sausage-dark","emporium-kit-bun","emporium-kit-bun-top","emporium-kit-sesame","emporium-kit-lamp",
            "emporium-kit-tube","emporium-kit-glass","emporium-kit-glass-dark","emporium-kit-tile","emporium-kit-checker","emporium-kit-quarry","emporium-kit-roller","emporium-kit-grate",
            "emporium-kit-griddle","emporium-kit-rubber","emporium-kit-pickle","emporium-kit-mustard","emporium-kit-ketchup","emporium-kit-screen","emporium-kit-white","emporium-kit-iron",
            "emporium-kit-stripe","emporium-kit-mint","emporium-kit-vinyl","emporium-kit-soda","emporium-kit-relish","emporium-kit-onion"};
        public const int Dark=0,Chrome=1,Red=2,Yellow=3,Cream=4,Teal=5,Pink=6,Wood=7,Sausage=8,SausageDark=9,Bun=10,BunTop=11,Sesame=12,Lamp=13,Tube=14,Glass=15,GlassDark=16,
            Tile=17,Checker=18,Quarry=19,Roller=20,Grate=21,Griddle=22,Rubber=23,Pickle=24,Mustard=25,Ketchup=26,Screen=27,White=28,Iron=29,Stripe=30,Mint=31,Vinyl=32,Soda=33,Relish=34,Onion=35;
        /// <summary>Each kind's look key and sRGB colour when the look does not list it (0: the look's palette colour).</summary>
        public static readonly (string look,int color)[] Defaults={("plain",0x1E1A22),("plain",0xDDE3EA),("plain",0xD8322A),("plain",0xF6B821),("plain",0xF7EEDC),("plain",0x1FA89C),
            ("plain",0xFF6FAE),("timber",0),("plain",0xC4502C),("plain",0x6E2414),("plain",0xF0B266),("plain",0xD98636),("plain",0xFFF4DA),("neon",0xFFB040),
            ("cyber-tube",0xFF4FA0),("window-lit",0xFFC88A),("window-lit",0x40546A),("ward-tile",0xF4F1EA),("plain",0x231F28),("ward-tile",0xC8432E),("plain",0xB4BCC6),("cyber-grate",0x5A6070),
            ("ward-burner",0x4A3A30),("plain",0x18181A),("plain",0x4E9A34),("plain",0xFFC81E),("plain",0xE3241C),("cyber-holo",0x22E6FF),("plain",0xFFFFFF),("plain",0x2C4A48),
            ("demolition",0),("plain",0x9FE6D6),("plain",0xC8202E),("plain",0x7FDFF2),("plain",0x6CC63A),("plain",0xF4E7C2)};
        /// <summary>The kinds' looks for one look's `kitLooks` (an entry by its "emporium-kit-*" id wins; else the default, resolved
        /// the same way, so a shared bank key such as `cyber-tube` keeps its bank flag).</summary>
        public static NativeFactoryKit.Looks Resolve(MapLookBlock[] kitLooks)
        {
            var l=new NativeFactoryKit.Looks();var one=new MapLookBlock[1];
            for(int k=0;k<Kinds.Length;k++)
            {
                var c=NativeBlockLooks.Resolve(kitLooks,null,Kinds[k],"",0xFFFFFF);
                if(!c.Listed){var d=Defaults[k];one[0]=new MapLookBlock{block=Kinds[k],look=d.look,color=d.color};c=NativeBlockLooks.Resolve(one,null,Kinds[k],"",0xFFFFFF);}
                l.Look[k]=c.Look;l.Color[k]=c.Color;l.Flags[k]=c.Flags&~(NativeBlockLooks.Band|NativeBlockLooks.Tag);
            }
            return l;
        }
        /// <summary>A kind drawn in the Cyber City bank's emissive looks (tube, holo), whose part `window` above 1 is an HDR gain on
        /// its colour (Runtime applies it; bloom takes emission above 1), not a light level.</summary>
        public static bool Gained(int kind)=>kind==Tube||kind==Screen;

        // ---------------- palette (sRGB) ----------------
        /// <summary>Neon and signal colours: the diner's pink, cyan, mint and lemon; the hazard colours (amber warn, red danger) are
        /// kept for the telegraphs.</summary>
        public const int NeonPink=0xFF3FA4,NeonCyan=0x2CE8FF,NeonLemon=0xFFE84A,NeonMint=0x5CFFB8,NeonOrange=0xFF8A2A,NeonViolet=0xA45CFF,Warm=0xFFC27A,
            Amber=0xFFB000,Danger=0xFF2A2A,Safe=0x44FF88,Off=0x2A2226,KetchupHex=0xE3241C,MustardHex=0xFFC81E;

        // ---------------- the places the rig, effects and scenery share ----------------
        /// <summary>The big turning hot dog sign over the Big Dog plinth: its centre (it hangs from the roof truss), its turn (rad/s)
        /// and the length of its frankfurter.</summary>
        public const double SignX=0,SignY=12.6,SignZ=0,SignTurn=.32,SignLength=8.4;
        public static double SignHeading(double t)=>(t*SignTurn)%(2*Math.PI);
        /// <summary>The glazed roof: the eaves (top of the hall's walls), the ridge, the vault's half span (z) and the hall's half
        /// length (x), metres. Nothing of the roof comes below <see cref="RoofLow"/> (the tests' overhead rule).</summary>
        public const double Eaves=12.5,Ridge=19.0,RoofHalfZ=23.4,RoofHalfX=29.4,RoofLow=12.4;
        /// <summary>The sun's travel (from it toward the hall): golden hour from the south-west, as MapLooks' key (bearing 235,
        /// elevation 32). A unit vector, the shafts through the roof follow it.</summary>
        public static void SunRay(out double x,out double y,out double z)
        {double el=32*Math.PI/180,br=235*Math.PI/180;x=-Math.Sin(br)*Math.Cos(el);y=-Math.Sin(el);z=-Math.Cos(br)*Math.Cos(el);}
        /// <summary>A stable 0..1 hash of three integers (the Emporium's seeds).</summary>
        public static double Hash(int a,int b=0,int c=0)=>NativeBrickfallScenery.Hash(a+4447,b+101,c+37);
        public static int Mix(int a,int b,double t){t=t<0?0:t>1?1:t;int C(int s)=>(int)Math.Round(((a>>s)&255)*(1-t)+((b>>s)&255)*t);return C(16)<<16|C(8)<<8|C(0);}
        public static int Scale(int hex,double k){int C(int s)=>(int)Math.Max(0,Math.Min(255,Math.Round(((hex>>s)&255)*k)));return C(16)<<16|C(8)<<8|C(0);}
        /// <summary>Whether a hazard id belongs to the ketchup (west) or mustard (east) side: the geysers and towers take their sauce.</summary>
        public static bool KetchupSide(double x)=>x<0;
    }
}
