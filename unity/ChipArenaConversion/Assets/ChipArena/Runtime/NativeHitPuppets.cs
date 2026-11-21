using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // Physical hit reactions on the drawn models (owner 2026-11-21: "Utilize Ragdoll Asset for hit reactions"; Core
    // NativeHitPuppetRules, NativeHitPuppetPresentation.cs). The bodies hand a hit to an active ragdoll that a local install
    // registers here: a git-ignored bridge to the owner's licensed PuppetMaster, which ships with the local packs, never in this
    // repository (as the cloth bridge does, NativeVisualCloth). Presentation only: each client reacts to the hit events it
    // already receives, nothing is networked, the model's root stays where the game put it and the puppet only bends the pose
    // the game drew this frame (its own physics scene: no collider of it ever meets the game's). Without a registered bridge,
    // on the low graphics tier and past the reach, the bodies draw as posed (today's look).
    public static class NativeHitPuppets
    {
        /// <summary>Makes an (asleep) puppet for a drawn body from its skeleton (the bake's VisualSkeleton, posed this frame): a
        /// handle, or null when the skeleton lacks the bones it needs.</summary>
        public static Func<Transform,object> Attach;
        /// <summary>A blow on `handle`: the hit point (world), the push (world, N·s) and how far the muscles there unpin (0..1). It
        /// wakes the puppet; false when the handle is no longer usable (its puppet is gone: attach a new one).</summary>
        public static Func<object,Vector3,Vector3,float,bool> Hit;
        /// <summary>Right after its body is posed this frame, before anything reads its bones: simulates `handle` for `dt` s and
        /// bends the drawn bones (rotations only). True while it is still reacting.</summary>
        public static Func<object,float,bool> Step;
        /// <summary>Whether `handle` is reacting (awake): what the cap counts.</summary>
        public static Func<object,bool> Awake;
        /// <summary>Starts blending `handle` back to the drawn pose (a body that is dying or past the reach).</summary>
        public static Action<object> Fade;
        /// <summary>Back to the pose as drawn at once (the body not drawn this frame, gone to another add, teleported).</summary>
        public static Action<object> Sleep;
        /// <summary>Destroys `handle`'s puppet (its body is gone).</summary>
        public static Action<object> Release;
        /// <summary>Every hook is needed: a partial registration draws the bodies as posed.</summary>
        public static bool Registered=>Attach!=null&&Hit!=null&&Step!=null&&Awake!=null&&Fade!=null&&Sleep!=null&&Release!=null;
        // Runs after the bridge's startup registration. This is a registration receipt, not proof of visible reactions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ReportRegistration()
        {
            Debug.Log(Registered
                ? "Hit puppets: bridge registered (attach/hit/step/fade/sleep/release). Verify the hit reactions in play."
                : "Hit puppets: bridge unavailable or incomplete; posed-body fallback. Repack licensed assets with the add-on allowlist (docs/HIT-PUPPETS.md).");
        }
    }
}
