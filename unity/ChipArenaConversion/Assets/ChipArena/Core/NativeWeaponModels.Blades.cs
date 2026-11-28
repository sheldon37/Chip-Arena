using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // The blade classes' weapons (owner 2026-11-10: "Use really great designs for the weapons"): the Katana and its saya
    // (Core/NativeWeaponModels.Katana.cs) and the Fangs' twin daggers (Core/NativeWeaponModels.Fangs.cs), registered here.
    public static partial class NativeWeaponModels
    {
        static partial void Blades(Dictionary<string,Design> d)
        {
            d[NativeKatana.Spec]=KatanaDesign(NativeKatana.Spec);d[NativeFangs.Spec]=FangsDesign(NativeFangs.Spec);
        }
    }
}
