using System;
namespace ChipArena.UnityConversion
{
    // Surface impacts (owner 2026-10-24: "Implement whatever we have on asset packs that would work good on this project, make
    // them fit"; Runtime NativeSurfaceImpactPresentation.cs): a gun round that hits a wall, floor or prop raises a burst of
    // the surface's own debris (stone dust and chips, metal sparks, wood splinters) from the owner's effect pack and leaves a
    // bullet hole that fades. This decides what shows; the effects are the bake's. Presentation only: the rounds, their
    // hits, damage and the replicated Impact events are unchanged.
    public static class NativeSurfaceImpacts
    {
        // Owner (2026-10-28): the surface impact upgrades approved, for every surface. Stone, metal and wood since 2026-10-24;
        // brick, glass, dirt (snow, gravel and loose ground too), rock, plaster and water since. The bake's entries for the
        // newer kinds come from the local session (docs/PACK-EFFECTS.md, "Local session: add these kinds to the impacts bake");
        // until then a kind the bake lacks plays the stone one, with its own bullet hole from the pack bank where there is one.
        public const int Stone=0, Metal=1, Wood=2, Brick=3, Glass=4, Dirt=5, Rock=6, Plaster=7, Water=8, Kinds=9;
        /// <summary>The bake's entries under VisualImpacts, by kind.</summary>
        public static readonly string[] Names={"stone","metal","wood","brick","glass","dirt","rock","plaster","water"};
        /// <summary>A surface's kind from its prop material (NativeEventSounds' impact sound: metal, wood, else stone).</summary>
        public static int Kind(string material)=>material=="metal"?Metal:material=="wood"?Wood:Stone;
        /// <summary>
        /// The impact a round makes on prop `block` of `material` on `map`, hit with a normal whose up share is `up`. The material by
        /// the structure rules (NativeStructureFx.Classify: brick, masonry and terracotta brick; glass and windows glass; metal,
        /// steel, iron and rebar metal; wood and timber wood); loose ground (snow, ice, dirt, gravel, sand, mud) dirt; rubble, cobble
        /// and rock rock; water water. A building's inside (the 'interior-' blocks) is plaster on its walls, stone underfoot. The
        /// ground (a foundation hit from above) is the map's floor as its footsteps hear it (Core NativeFootsteps.Surface: snow
        /// and gravel maps dirt, the stone maps rock, wood and metal maps theirs); the stone maps' stone blocks are rock; the rest
        /// stone.
        /// </summary>
        public static int Surface(string material,string map,string block,float up)
        {
            switch(material)
            {
                case "water":case "liquid":return Water;
                case "snow":case "ice":case "dirt":case "gravel":case "sand":case "mud":case "grass":return Dirt;
                case "rubble":case "cobble":case "rock":return Rock;
            }
            switch(NativeStructureFx.Classify(material))
            {
                case NativeStructureMaterial.Masonry:return Brick;case NativeStructureMaterial.Glass:return Glass;
                case NativeStructureMaterial.Metal:return Metal;case NativeStructureMaterial.Timber:return Wood;
            }
            if(block!=null&&block.StartsWith("interior-",StringComparison.Ordinal))return up>.7f?Stone:Plaster;
            string floor=NativeFootsteps.Surface(map);
            if(material=="foundation"&&up>.7f)return floor==NativeFootsteps.Snow||floor==NativeFootsteps.Gravel?Dirt:floor==NativeFootsteps.Stone?Rock:floor==NativeFootsteps.Wood?Wood:floor==NativeFootsteps.Metal?Metal:Stone;
            return floor==NativeFootsteps.Stone&&material!="foundation"?Rock:Stone;
        }
        /// <summary>The bank texture a kind's bullet hole takes when the bake has no entry for the kind (null: the stone hole);
        /// water leaves none.</summary>
        public static string HoleTexture(int kind)=>kind==Brick?"impact-hole-brick":kind==Glass?"impact-hole-glass":kind==Dirt?"impact-hole-dirt":null;
        /// <summary>Whether a kind leaves a bullet hole (water does not).</summary>
        public static bool Holes(int kind)=>kind!=Water;
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
