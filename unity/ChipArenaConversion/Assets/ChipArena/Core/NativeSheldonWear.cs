using System;

namespace ChipArena.UnityConversion
{
    // Sheldon's clothes on the model (owner 2026-10-27: "Can you remake Sheldon clothes into functional clothing items (Hood/face,
    // robe, pants, shoes. Shoeless option too - these are all dev items)", "I want it to utilize magica cloth for any kind of
    // fabric movement. I want a very clean design that fits the model perfectly."). The DEV Sheldon Clothes (the voxel body
    // skin's long dark coat with glowing seams, Core NativeBodyModels) and the Sheldon Head, as pieces the model wears:
    //  - the robe (NativeLook upper "sheldon-robe"): the pack's long kimono, made for the model: a wrap front closing over the
    //    chest, a sash, wide cuffs and a skirt to the ankles that moves as cloth (the pack's own Magica paint map);
    //  - the trousers ("sheldon-pants"): the pack's slim dress trousers; the boots ("sheldon-boots"): its work boots;
    //  - bare feet ("barefoot"): no shoes, the model's own feet set down on the floor (the bake's lift for them);
    //  - the hood and face: the Sheldon Head hat, a cloth hood fitted round the model's head (Core NativeSheldonHood).
    // Each piece takes the Sheldon palette: the hood's dark navy cloth, a lighter navy on its trims, and each trim's edge piped
    // in the glow (the bake's colour maps, Resources/ChipArenaVisualWear: the trim's texels along the cloth moved to the accent
    // channel), lit in the Sheldon glow (NativeBodySkins.SheldonGlow: a picked hat colour, cyan under the hood, else the pilot's
    // accent). All DEV only (NativeLook.Gate). Presentation only: no rule, hit volume, timing or network authority reads them.
    public static class NativeSheldonWear
    {
        public const string Robe="sheldon-robe",Pants="sheldon-pants",Boots="sheldon-boots",Barefoot="barefoot";
        /// <summary>A Sheldon piece (its renderers take the Sheldon palette and colour maps).</summary>
        public static bool IsSheldon(string id)=>id!=null&&id.StartsWith("sheldon-",StringComparison.Ordinal);
        /// <summary>A DEV-only wardrobe piece: Sheldon's clothes and bare feet.</summary>
        public static bool DevOnly(string id)=>IsSheldon(id)||id==Barefoot;
        /// <summary>The palette (sRGB) on the pack's colour weights: the cloth (each layer's first colour) and the trims (its second);
        /// the third, the accent, is the glow.</summary>
        public const int Cloth=0x1f2538,Trim=0x343d5c;
        /// <summary>Where the bake keeps a Sheldon piece's colour map, by its renderer's name (Resources).</summary>
        public const string MapFolder="ChipArenaVisualWear/";
    }
}
