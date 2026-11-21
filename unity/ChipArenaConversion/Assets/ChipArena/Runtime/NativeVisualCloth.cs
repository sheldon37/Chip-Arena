using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // Hair and cloth on the visual bodies (docs/BOZO-INTEGRATION.md step 5; owner 2026-10-22: cloth "on every applicable piece",
    // 2026-10-26: "go ahead"). The bodies hand their dressed instance to a simulation that a local install registers here: a
    // git-ignored bridge to the licensed cloth package, which ships with the local packs, never in this repository. Presentation
    // only: each client simulates its own, nothing is networked; the first-person body has none; the low graphics tier and
    // bodies past Far from the arena's camera hold the pose as drawn. Without a registered simulation the bodies draw as posed.
    public static class NativeVisualCloth
    {
        /// <summary>Starts simulating the loose pieces of a dressed body (`root`, its skeleton `skeleton`): a handle, or null when
        /// it has none.</summary>
        public static Func<GameObject,Transform,object> Attach;
        /// <summary>Simulates (true) or holds the pose as drawn (false).</summary>
        public static Action<object,bool> Enable;
        /// <summary>Back to the pose as drawn: a teleport, a respawn, a gap in the body's frames.</summary>
        public static Action<object> Reset;
        /// <summary>The distance from the arena's camera past which a body's cloth holds still (m).</summary>
        public const float Far=30;
        /// <summary>All three hooks are needed: attachment alone does not prove enable/reset lifecycle support.</summary>
        public static bool Registered=>Attach!=null&&Enable!=null&&Reset!=null;
        // Runs after the bridge's startup registration. This is a registration receipt, not proof of visible simulation.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ReportRegistration()
        {
            Debug.Log(Registered
                ? "Cloth runtime: bridge registered (attach/enable/reset). Verify hair, robe skirt and hood cowl motion in play."
                : "Cloth runtime: bridge unavailable or incomplete; posed-body fallback. Repack licensed assets with --with-cloth.");
        }

        /// <summary>Paint maps for the game's own cloth pieces, by their renderers' names (red fixed, green moving; readable): the
        /// Sheldon hood's. The simulation takes these before the bake's own (the pack's, by the same names).</summary>
        public static readonly System.Collections.Generic.Dictionary<string,Texture2D> PaintMaps=new System.Collections.Generic.Dictionary<string,Texture2D>(StringComparer.Ordinal);
    }
}
