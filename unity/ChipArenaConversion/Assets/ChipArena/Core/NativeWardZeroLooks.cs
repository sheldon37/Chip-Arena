using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Ward Zero's revamp (docs/arenas/WARDZERO_PROPOSAL.md): the gate every new Ward Zero hook checks, its palette, and its
    /// surfaces for Resources/NativeBlockLit.shader.
    /// <para>The gate. The game already has a Ward Zero (map id "siren", built from GameContent.json and drawn from the
    /// exported source models by NativeWardPresentation). The revamp keeps that id and replaces the map when Astra imports the
    /// draft (docs/arenas/scripts/wardzero_m1.py); until then nothing here runs. <see cref="Active"/> is true only for a map
    /// with the id "siren" whose `look` is <see cref="Look"/> ("quarantine"): the draft carries that look and today's Ward
    /// Zero carries none, so the current map, its presentation and its lighting stay exactly as they are.</para>
    /// <para>The surfaces sit in the Cyber City bank's unused slots (the shader's FLAG_CYBER, NativeCyberCityLooks.Bank): with
    /// the flag the look id names a slot, so the base keys, their shader ids, every old map and Cyber City's eight slots stay
    /// as they are. Slots 1-3 are masonry ids (cracks and fresh breaks as concrete), 13-15 plain fresh breaks.</para>
    /// <list type="bullet">
    /// <item>`ward-tile` (1): 30 cm clinical tile in thin joints, scuffed and stained; the floors, decks and tiled walls.</item>
    /// <item>`ward-panel` (2): white wall panels 1.2 x 2.4 m with seams, rivets and streaks; walls, rows, the atrium.</item>
    /// <item>`ward-burner` (3): an incinerator grate's bars and rods over its trough; a light level above 1 heats it orange,
    /// then yellow-white in the burn (the kit's burner drive).</item>
    /// <item>`ward-glass` (13): a specimen tank's lit fluid behind glass with a meniscus band and bubble trails; the light
    /// level (the instance's window channel) lights the fluid, 0 drains it.</item>
    /// <item>`ward-light` (14): a fluorescent troffer's diffuser with two tubes and a louvre grid, emissive by the light level
    /// (the kit's flicker and the mood drive it).</item>
    /// <item>`ward-ink` (15): the Siren's ink: glossy black-violet with glowing veins, the veins by the light level.</item>
    /// </list>
    /// The kit also uses Cyber City's `cyber-grate`, `cyber-tube`, `cyber-holo` and `cyber-metal` and the base looks. Pure; no
    /// allocation.
    /// </summary>
    public static class NativeWardZeroLooks
    {
        public const string MapId="siren",Look="quarantine";
        /// <summary>Whether a map is the revamped Ward Zero (the id the game uses and the draft's look); every new Ward Zero hook
        /// is inert on any other map, today's Ward Zero included.</summary>
        public static bool Active(ContentMap map)=>map!=null&&map.id==MapId&&map.look==Look;
        public const int Tile=1,Burn=3,Panel=2,Glass=13,Light=14,Ink=15;
        /// <summary>Keys by slot of the shared bank (null: a slot this bank does not use; Cyber City's are its own).</summary>
        public static readonly string[] Keys={null,"ward-tile","ward-panel","ward-burner",null,null,null,null,null,null,null,null,null,"ward-glass","ward-light","ward-ink"};
        /// <summary>Each slot's base colour (sRGB) when an entry names none; -1 for slots not in this bank.</summary>
        public static readonly int[] Palette={-1,0xB9C3C6,0xC9CFCC,0x4A3A30,-1,-1,-1,-1,-1,-1,-1,-1,-1,0x3FD6B0,0xDDEBFF,0x1A0E24};
        /// <summary>A key's bank id (NativeCyberCityLooks.Offset + slot), or -1.</summary>
        public static int Id(string key){if(key==null)return -1;for(int i=0;i<Keys.Length;i++)if(Keys[i]==key)return NativeCyberCityLooks.Offset+i;return -1;}
        /// <summary>Whether an id from NativeBlockLooks.Id is one of this bank's slots.</summary>
        public static bool Is(int id)=>id>=NativeCyberCityLooks.Offset&&id<NativeCyberCityLooks.Offset+Keys.Length&&Keys[id-NativeCyberCityLooks.Offset]!=null;
        /// <summary>The key of a resolved choice (a slot of either bank with the bank flag, or a base look), for logs and checks.</summary>
        public static string KeyOf(int look,int flags)
        {
            if((flags&NativeCyberCityLooks.Bank)==0)return look>=0&&look<NativeBlockLooks.Keys.Length?NativeBlockLooks.Keys[look]:null;
            if(look<0||look>=Keys.Length)return null;return Keys[look]??NativeCyberCityLooks.Keys[look];
        }

        // ---------------- palette (sRGB), docs/arenas/WARDZERO_PROPOSAL.md section 10 ----------------
        /// <summary>Clinical whites and tiles, painted steel, the containment teal, the specimen fluid, the fluorescent and the
        /// emergency colours, the Siren's ink violet and her pale white (the map's accent), the burner and gas colours. Hazard
        /// amber and red are kept for telegraphs, alarms and the 2.3 bands.</summary>
        public const int TileHex=0xB9C3C6,PanelHex=0xC9CFCC,PanelGreyHex=0x8F9A9C,SteelHex=0x5D6B6E,SteelDarkHex=0x2F383B,ConcreteHex=0x6E7472,
            TealHex=0x2FB7A3,FluidHex=0x3FD6B0,FluorHex=0xDDEBFF,FluorWarmHex=0xFFF1D6,EmergencyHex=0xFF2A34,AmberHex=0xFFB000,
            InkHex=0x8B3DFF,InkDeepHex=0x1A0E24,SirenHex=0xE9E4F2,BurnHex=0xFF7A1F,BurnCoreHex=0xFFD9A0,GasHex=0xB8FFD0,ExitHex=0xE8FFF6,SafeHex=0x5CFF9A;
        /// <summary>Route signs: the way up is cyan-white, the way down warm, so a stair reads from both floors.</summary>
        public const int UpHex=0x8FE9FF,DownHex=0xFFC27A;
    }
}
