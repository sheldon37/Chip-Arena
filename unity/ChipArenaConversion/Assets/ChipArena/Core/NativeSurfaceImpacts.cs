namespace ChipArena.UnityConversion
{
    // Surface impacts (owner 2026-10-24: "Implement whatever we have on asset packs that would work good on this project, make
    // them fit"; Runtime NativeSurfaceImpactPresentation.cs): a gun round that hits a wall, floor or prop raises a burst of
    // the surface's own debris (stone dust and chips, metal sparks, wood splinters) from the owner's effect pack and leaves a
    // bullet hole that fades. This decides what shows; the effects are the bake's. Presentation only: the rounds, their
    // hits, damage and the replicated Impact events are unchanged.
    public static class NativeSurfaceImpacts
    {
        public const int Stone=0, Metal=1, Wood=2, Kinds=3;
        /// <summary>The bake's entries under VisualImpacts, by kind.</summary>
        public static readonly string[] Names={"stone","metal","wood"};
        /// <summary>A surface's kind from its prop material (NativeEventSounds' impact sound: metal, wood, else stone).</summary>
        public static int Kind(string material)=>material=="metal"?Metal:material=="wood"?Wood:Stone;
        /// <summary>A gun's plain round (a round with a Detail belongs to a special or a kit, which draws its own impact).</summary>
        public static bool Shows(string spec,string detail)=>string.IsNullOrEmpty(detail)&&(spec=="pistol"||spec=="rifle"||spec=="machinegun");
        /// <summary>Events older than Stale seconds draw nothing (a late join, a long frame). An effect is recycled after
        /// EffectLife; at most PerKind of a kind play at once (PerKindReduced with reduced effects), the oldest replaced.
        /// Holes: at most Decals, each for DecalLife seconds, fading over its last DecalFade.</summary>
        public const float Stale=.3f, EffectLife=2.2f, DecalLife=9, DecalFade=2.5f, Lift=.02f, DecalLift=.004f;
        public const int PerKind=6, PerKindReduced=3, Decals=48;
        /// <summary>A hole's opacity at `age` seconds (1 until its fade, then down to 0 at DecalLife).</summary>
        public static float DecalAlpha(float age)=>age<0?0:age<DecalLife-DecalFade?1:age<DecalLife?(DecalLife-age)/DecalFade:0;
    }
}
