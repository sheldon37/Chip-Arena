using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The caches' licensed effects layer (Claude C2, 2026-11-25; Astra's revamp brief §4, owner: "lets get work started"):
    /// the Action RPG FX pack's chest glows and chest openings, its four levels on the four tiers (Lv1 STANDARD, Lv2 RARE,
    /// Lv3 EPIC, Lv4 LEGENDARY), on the CD-647 steel locker, beside the procedural opening (<see cref="NativeCacheOpenFx"/>),
    /// which stays as it is. While a cache is for sale, a small glow and its sparkles sit at the latch line on the locker's
    /// front (a localized cue, nothing on the floor); at the confirmed open moment, as the lid lifts (<see cref="BurstAt"/>),
    /// one burst rises out of the tray: the level's rays, a splash of sparkles and a few that linger.
    /// <para>The effects are the pack's particle systems re-tinted per tier on Unity's built-in particle shaders (no pack shader or
    /// script), in the git-ignored effects folder (Assets/LocalAssets/ChipArenaFx, its Resources/<see cref="Resource"/> prefab:
    /// its two effects <see cref="Glow"/> and <see cref="Open"/>), shipped with the separate effects bank (DEV_LOG
    /// 2026-11-23). Without them nothing extra draws: CI, cloud builds, other checkouts and the public source keep today's look.</para>
    /// <para>Who sees what: every client reads the same replicated open moment (<see cref="NativeCacheClock"/>: the generation
    /// changed with its cooldown ahead), so the host and each guest play one burst per opening, of the tier the offer holds
    /// (the livery shown); a cache first seen already opened (a late joiner, an area just entered) plays none
    /// (<see cref="NativeCacheBursts"/>). Off with Reduced effects (the owner's rule: Reduced shows only the lid), on the low
    /// graphics tier, for the hologram cache and the chip machines. Engine-free; presentation only: it reads replicated data and
    /// decides no rule, timing, hitbox or network message.</para>
    /// </summary>
    public static class NativeCachePackFx
    {
        /// <summary>The effects prefab's Resources name (Assets/LocalAssets/ChipArenaFx/Resources/ChipArenaCacheFx.prefab).</summary>
        public const string Resource="ChipArenaCacheFx";
        /// <summary>The layer's switch (a DEV and capture comparison; the game leaves it on).</summary>
        public static bool On=true;
        /// <summary>The prefab's two effects (each inactive, playing nothing on awake): the for-sale glow, from the pack's ChestLv1Glow (its
        /// aura turned to face the camera, a soft halo, its sparkles in a thin box along the latch line), and the opening, from its
        /// ChestLv4Open (a flat aura at the mouth, the rays, the splash and the linger; its light dropped). One of each, re-tinted and
        /// re-counted per tier at each start (<see cref="Levels"/>), so the four levels cost two effects' bytes.</summary>
        public const string Glow="glow",Open="open";
        /// <summary>The systems by name: the glow's aura is its root's, then its halo and sparkles; the opening's mouth aura is its
        /// root's, then its rays, splash and linger.</summary>
        public const string Halo="halo",Sparkles="sparkles",Rays="rays",Splash="splash",Linger="linger";
        /// <summary>One tier's look: the pack's own level (its four ChestLv<i>N</i>Glow and ChestLv<i>N</i>Open prefabs' numbers),
        /// in the tier's colour, the sparkle atlas's tile in its colour (3 x 3: 7 pale, 4 sky blue, 2 violet, 5 orange).</summary>
        public readonly struct Level
        {
            /// <summary>Sparkles at the latch a second, and their lifetime (s).</summary>
            public readonly float SparkRate,SparkLifeMin,SparkLifeMax;
            /// <summary>The opening's rays: lifetime (s), largest start size (pack metres) and opacity.</summary>
            public readonly float RaysLifeMin,RaysLifeMax,RaysSize,RaysAlpha;
            /// <summary>The opening's splash and linger sparkle counts and the splash's top speed (m/s).</summary>
            public readonly int Splash,Linger;public readonly float SplashSpeed;
            /// <summary>The sparkle atlas tile.</summary>
            public readonly int Tile;
            public Level(float sparkRate,float sparkLifeMin,float sparkLifeMax,float raysLifeMin,float raysLifeMax,float raysSize,float raysAlpha,int splash,int linger,float splashSpeed,int tile)
            {SparkRate=sparkRate;SparkLifeMin=sparkLifeMin;SparkLifeMax=sparkLifeMax;RaysLifeMin=raysLifeMin;RaysLifeMax=raysLifeMax;RaysSize=raysSize;RaysAlpha=raysAlpha;Splash=splash;Linger=linger;SplashSpeed=splashSpeed;Tile=tile;}
        }
        /// <summary>STANDARD, RARE, EPIC, LEGENDARY: the pack's Lv1 to Lv4.</summary>
        public static readonly Level[] Levels=
        {
            new Level(12,.4f,.8f, .15f,.25f,1.175f,.5f,  12,6,3, 7),
            new Level(17,.4f,.8f, .25f,.35f,1.175f,.745f,35,11,4,4),
            new Level(25,.6f,1f,  .35f,.45f,1.3f,.9f,    45,18,5,2),
            new Level(40,.6f,1f,  .35f,.45f,1.5f,.9f,    45,18,5,5),
        };
        public static Level LevelOf(NativeCacheTier t)=>Levels[NativeCacheTiers.Index(t)];
        /// <summary>The atlas tiles (3 x 3).</summary>
        public const int TilesX=3,TilesY=3;
        /// <summary>The pack's level a tier plays (1 to 4).</summary>
        public static int LevelNumber(NativeCacheTier t)=>NativeCacheTiers.Index(t)+1;

        // ---- where they stand, in the locker's frame (NativeCacheOpenFx's marks: the floor under its middle, its front toward -z) ----
        /// <summary>The glow: centred between the two latches at their height, just proud of the front (so the body never hides it).</summary>
        public const float GlowX=0,GlowY=NativeCacheOpenFx.LatchY,GlowZ=NativeCacheOpenFx.LatchZ-.05f;
        /// <summary>The burst: from the tray's mouth, the middle of the opening.</summary>
        public const float OpenX=0,OpenY=NativeCacheOpenFx.MouthY,OpenZ=0;
        /// <summary>The pack's chest is about 1.1 m wide; the locker's mouth about 0.75 m: the effects play at this scale.</summary>
        public const float Scale=.7f;

        // ---- when ----
        /// <summary>The burst: the lid's lift (R9's beat), so the rays come out of the opening gap.</summary>
        public const float BurstAt=NativeCacheOpenFx.LidAt;
        /// <summary>An opening first seen more than this after its burst moment plays none (a late joiner, a long hitch).</summary>
        public const float Late=.35f;
        /// <summary>How long a burst plays (its longest system: the linger's 1.4 s lifetime after a 1 s emission), then it is put away.</summary>
        public const float OpenLife=2.6f;
        /// <summary>A glow whose cache stops being for sale stops emitting; its sparkles live out, then it is put away.</summary>
        public const float GlowFade=1.2f;
        /// <summary>At most this many glows (the nearest caches to the camera, within <see cref="GlowReach"/> m) and bursts at once.</summary>
        public const int MaxGlows=6,MaxBursts=4;
        public const float GlowReach=45;

        /// <summary>Whether a cache shows its for-sale glow: a steel locker for sale, with full effects.</summary>
        public static bool Glowing(in NativeCacheShow s,bool reduced)=>On&&!reduced&&s.Id!=null&&s.Locker&&!s.Machine&&s.State==NativeCacheState.ForSale;
        /// <summary>Whether an opening is inside its burst window this frame (from <see cref="BurstAt"/>, for <see cref="Late"/> s).</summary>
        public static bool InWindow(in NativeCacheShow s,bool reduced)
            =>On&&!reduced&&s.Id!=null&&s.Locker&&!s.Machine&&s.State==NativeCacheState.Opening&&s.Since>=BurstAt&&s.Since<BurstAt+Late;
    }

    /// <summary>
    /// The bursts one client has played: one per opening (a cache's id and the opening's seed, the cache and the generation that
    /// opened, alike on every peer), however many frames, cameras or duplicated snapshots show it. Engine-free; once warm a frame
    /// allocates nothing.
    /// </summary>
    public sealed class NativeCacheBursts
    {
        private readonly Dictionary<string,uint> fired=new Dictionary<string,uint>(StringComparer.Ordinal);
        /// <summary>True once for each opening whose burst window this frame shows (the caller plays the burst then).</summary>
        public bool Fire(in NativeCacheShow s,bool reduced)
        {
            if(!NativeCachePackFx.InWindow(s,reduced))return false;
            if(fired.TryGetValue(s.Id,out uint seed)&&seed==s.Seed)return false;
            if(fired.Count>64)fired.Clear();
            fired[s.Id]=s.Seed;return true;
        }
        public void Clear()=>fired.Clear();
    }
}
