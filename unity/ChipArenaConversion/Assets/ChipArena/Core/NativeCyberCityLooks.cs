using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Cyber City's surface bank for Resources/NativeBlockLit.shader (docs/arenas/CYBERCITY_PROPOSAL.md §8 "Visual identity").
    /// A second set of procedural looks beside NativeBlockLooks.Keys, chosen by the per-instance flag <see cref="Bank"/>
    /// (the shader's FLAG_CYBER) so the base keys, their shader ids and every old map stay exactly as they are: with the flag
    /// the instance's look id names a slot of this bank. Slots sit on base ids whose shared layers suit them (cracks and fresh
    /// concrete on the masonry ids below 4.5, plain fresh breaks elsewhere, never the timber id 6).
    /// <list type="bullet">
    /// <item>`cyber-tile` (0): 60 cm pavers in thin joints with wet patches; sidewalks, plazas, decks.</item>
    /// <item>`cyber-panel` (4): precast facade panels 1.2 x 1.8 m in recessed seams with rain streaks; frontage, podium shells.</item>
    /// <item>`cyber-metal` (5): dark anodised cladding 0.6 x 1.2 m, brushed, bolted, no rust; kiosks, vans, rails, units.</item>
    /// <item>`cyber-grate` (7): steel floor grating over a dark void; the couplings. A light level above 1 glows the void (the
    /// hazard rig's charge).</item>
    /// <item>`cyber-road` (8): wet asphalt with puddles that keep a cool sheen; the street slabs.</item>
    /// <item>`cyber-glass` (10): curtain wall, 1.5 m bays in 3.6 m storeys, a third of the panes lit in cool white, cyan, magenta
    /// or warm; fades to its bay average with distance; the spires and the backdrop towers.</item>
    /// <item>`cyber-holo` (11): an LED/holo screen in the block's colour with scanlines, a hue band and a dark bezel.</item>
    /// <item>`cyber-tube` (12): a neon tube or LED strip, emissive in the block's colour (HDR on kit parts).</item>
    /// </list>
    /// MapLooks.json names these keys like any other; NativeBlockLooks.Id returns <see cref="Offset"/> + slot for them and
    /// NativeBlockLooks.Resolve turns that into the slot plus the <see cref="Bank"/> flag. Pure; nothing allocates.
    /// </summary>
    public static class NativeCyberCityLooks
    {
        public const string MapId="cybercity";
        /// <summary>Whether a map id is Cyber City: every Cyber City hook is inert on any other map.</summary>
        public static bool Active(string mapId)=>mapId==MapId;
        /// <summary>The per-instance flag bit (NativeBlockLit FLAG_CYBER), above NativeBlockLooks' seven.</summary>
        public const int Bank=128;
        /// <summary>The ids NativeBlockLooks.Id hands out for this bank's keys: Offset + slot (beyond any base key's index).</summary>
        public const int Offset=16,Slots=16;
        public const int Tile=0,Panel=4,Metal=5,Grate=7,Road=8,Glass=10,Holo=11,Tube=12;
        /// <summary>Keys by slot (null: unused slot).</summary>
        public static readonly string[] Keys={"cyber-tile",null,null,null,"cyber-panel","cyber-metal",null,"cyber-grate","cyber-road",null,"cyber-glass","cyber-holo","cyber-tube",null,null,null};
        /// <summary>Each slot's base colour (sRGB) when an entry names none (§8 palette); -1 for unused slots.</summary>
        public static readonly int[] Palette={0x3B3D47,-1,-1,-1,0x4A4E5C,0x2C3140,-1,0x5A6070,0x22232A,-1,0x141A2C,0x22E6FF,0xFF3DCF,-1,-1,-1};
        /// <summary>A key's bank id (Offset + slot), or -1.</summary>
        public static int Id(string key){if(key==null)return -1;for(int i=0;i<Keys.Length;i++)if(Keys[i]==key)return Offset+i;return -1;}
        /// <summary>Whether an id from NativeBlockLooks.Id is one of this bank's.</summary>
        public static bool Is(int id)=>id>=Offset&&id<Offset+Slots&&Keys[id-Offset]!=null;
        public static int Slot(int id)=>id-Offset;
        /// <summary>The key of a resolved choice (its slot plus the bank flag, or a base look) for logs and checks.</summary>
        public static string KeyOf(int look,int flags)=>(flags&Bank)!=0?look>=0&&look<Slots?Keys[look]:null:look>=0&&look<NativeBlockLooks.Keys.Length?NativeBlockLooks.Keys[look]:null;

        // ---------------- §8 palette (sRGB) ----------------
        /// <summary>Neon and signal colours: the HUD's cyan and magenta (NativeCyberTheme), violet, ice white; the hazard colours
        /// (amber warn, red danger) are reserved for telegraphs and the 2.3 bands.</summary>
        public const int CyanHex=0x22E6FF,MagentaHex=0xFF3DCF,VioletHex=0x8B5CFF,IceHex=0xC8F4FF,WarmHex=0xFF8A4D,AmberHex=0xFFB000,DangerHex=0xFF2A3D,LimeHex=0x7DFF8A;
        public const int ConcreteHex=0x4A4E5C,ConcreteDarkHex=0x34363F,MetalHex=0x2C3140,MetalLightHex=0x6B7385,AsphaltHex=0x22232A,PavingHex=0x3B3D47,GlassDarkHex=0x141A2C,PaintHex=0xC9CED8,HazardHex=0xF4B321;
    }
}
