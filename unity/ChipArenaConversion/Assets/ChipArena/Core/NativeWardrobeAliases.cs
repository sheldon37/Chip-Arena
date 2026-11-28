namespace ChipArena.UnityConversion
{
    // Pilot clothes drawn by pieces the bake already carries for the zombies (owner 2026-11-27: "Lets add more clothes options
    // now"; docs/PILOT-BODY-HAIR.md section 4): each pilot id (Core/NativeLook.cs Ids) is drawn by the bake's look-data piece of
    // its zombie id (the local tool's Wardrobe table, Core/NativeZombieLooks.cs: the pack's piece closest to each zombie design),
    // with that piece's renderers, covers, tags, shoes' lift and gear anchors (Runtime/NativeHairBankPresentation.cs
    // VisualLookAddPieces). Left out: the tube top (the bake carries no mesh for it) and the bib overalls (they cover the legs, so
    // they would clash with every pair of trousers). Presentation only.
    public static class NativeWardrobeAliases
    {
        /// <summary>Slot, pilot id, the bake's piece that draws it.</summary>
        public static readonly string[][] Pairs={
            new[]{NativeLook.Upper,"school-jacket","z-work-jacket"},new[]{NativeLook.Upper,"cardigan","z-cardigan"},
            new[]{NativeLook.Upper,"henley","z-track-jacket"},new[]{NativeLook.Upper,"dress-shirt","z-work-shirt"},
            new[]{NativeLook.Upper,"short-shirt","z-short-shirt"},new[]{NativeLook.Upper,"sleeveless-shirt","z-zip-vest"},
            new[]{NativeLook.Upper,"sweater-vest","z-sweater-vest"},new[]{NativeLook.Upper,"suit-jacket","z-suit-jacket"},
            new[]{NativeLook.Upper,"kimono-top","z-gown"},
            new[]{NativeLook.Lower,"capris","z-capri-leggings"},new[]{NativeLook.Lower,"dress-pants","z-dress-pants"},
            new[]{NativeLook.Lower,"long-skirt","z-skirt"},new[]{NativeLook.Lower,"shorts","z-shorts"},
            new[]{NativeLook.Lower,"dolphin-shorts","z-dolphin-shorts"},
            new[]{NativeLook.Feet,"flats","z-flats"},new[]{NativeLook.Feet,"socked-flats","z-socks"},
            new[]{NativeLook.Feet,"heels","z-heels"},new[]{NativeLook.Feet,"loafers","z-loafers"},
            new[]{NativeLook.Feet,"flip-flops","z-flip-flops"}};
        /// <summary>The bake's piece that draws pilot piece `id` of `slot`, or null when it is the bake's own.</summary>
        public static string Of(string slot,string id)
        {
            foreach(var p in Pairs)if(p[0]==slot&&p[1]==id)return p[2];
            return null;
        }
    }
}
