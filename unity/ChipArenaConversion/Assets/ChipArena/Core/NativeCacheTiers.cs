using System;

namespace ChipArena.UnityConversion
{
    /// <summary>A Civil Defence cache's grade (R9; the owner's Q26 tiers): one locker, four liveries, four openings.</summary>
    public enum NativeCacheTier { Standard, Rare, Epic, Legendary }

    /// <summary>
    /// The caches' tiers as the presentation reads them (Claude, 2026-11-19; docs/EXPEDITION-OVERHAUL.md R9 and W6, the board 3
    /// opening design). Engine-free. Nothing here rolls, prices or decides a tier: the tier is Astra's A2 contract
    /// (<c>ExpeditionChestOffer.tier</c>: standard, rare, epic or legendary; missing reads as standard), read through the one
    /// adapter <see cref="Of(ExpeditionChestOffer)"/>. Until that field lands every live cache reads STANDARD.
    /// <para>The chip machine plays the same opening by its chip's rarity (owner, 2026-11-19: "That will be fine for now"):
    /// <see cref="OfRarity"/> maps the content's chip rarities onto the four tiers (docs/EXPEDITION-PRESENTATION.md "Cache
    /// openings").</para>
    /// </summary>
    public static class NativeCacheTiers
    {
        public const int Count=4;
        public static readonly NativeCacheTier[] All={NativeCacheTier.Standard,NativeCacheTier.Rare,NativeCacheTier.Epic,NativeCacheTier.Legendary};
        /// <summary>The tier colours (NativeMachineModels.RarityColors: common, rare, epic, legendary) and their whiter cores.</summary>
        private static readonly int[] hex={0xf1f5f9,0x70b5ff,0xce8aff,0xffae55},core={0xffffff,0xd8ecff,0xf0dcff,0xffe6b0};
        /// <summary>The lamp faces after the shut (art bible 3.1, beacon red), and the short wallet's red.</summary>
        public const int Red=0xff2433;
        private static readonly string[] ids={"standard","rare","epic","legendary"};
        private static readonly string[] names={"STANDARD","RARE","EPIC","LEGENDARY"};
        private static readonly string[] titles={"STANDARD CACHE","RARE CACHE","EPIC CACHE","LEGENDARY CACHE"};

        // ---- the one presentation adapter (A2) ----------------------------------------------------------------------------
        /// <summary>A cache offer's tier: the offer's tier field, missing (or unknown) as standard; null reads standard.</summary>
        public static NativeCacheTier Of(ExpeditionChestOffer o)=>Parse(TierField(o));
        /// <summary>The offer's tier field: Astra's A2 <c>ExpeditionChestOffer.tier</c> (PR #425, 2026-11-19).</summary>
        private static string TierField(ExpeditionChestOffer o)=>o?.tier;
        /// <summary>The contract's tier id (standard, rare, epic, legendary; any case); anything else, or none, is standard.</summary>
        public static NativeCacheTier Parse(string id)
        {
            if(string.IsNullOrEmpty(id))return NativeCacheTier.Standard;
            for(int i=1;i<ids.Length;i++)if(string.Equals(ids[i],id,StringComparison.OrdinalIgnoreCase))return (NativeCacheTier)i;
            return NativeCacheTier.Standard;
        }
        public static string Id(NativeCacheTier t)=>ids[Index(t)];

        // ---- the chip machine: a chip's rarity on the cache ladder ----------------------------------------------------------
        /// <summary>
        /// A chip rarity (GameContent rarities, the machine's rolled pickup tier) as a cache tier: common and uncommon are
        /// STANDARD, rare RARE, epic EPIC, legendary, mythic and the rainbow pickup LEGENDARY (the top of the ladder). The class
        /// rarities (Mishima, Ansatsuken, L44T, Siren, Bombadier and the DEV classes) are not on the machine's odds; they and any
        /// unknown or missing rarity read STANDARD, as a missing cache tier does.
        /// </summary>
        public static NativeCacheTier OfRarity(string rarity)
        {
            switch(rarity)
            {
                case "rare":return NativeCacheTier.Rare;
                case "epic":return NativeCacheTier.Epic;
                case "legendary":case "mythic":case "rainbow":return NativeCacheTier.Legendary;
                default:return NativeCacheTier.Standard;
            }
        }
        /// <summary>The chip cartridge model a tier's stand-in cartridges use (NativeMachineModels.PickupTiers).</summary>
        public static string Cartridge(NativeCacheTier t)=>t==NativeCacheTier.Rare?"rare":t==NativeCacheTier.Epic?"epic":t==NativeCacheTier.Legendary?"legendary":"common";

        // ---- the look ----------------------------------------------------------------------------------------------------------
        public static int Index(NativeCacheTier t)=>t<NativeCacheTier.Standard||t>NativeCacheTier.Legendary?0:(int)t;
        /// <summary>The tier's colour (sRGB hex): the lamp, the column's outer light, the label.</summary>
        public static int Hex(NativeCacheTier t)=>hex[Index(t)];
        /// <summary>The tier's whiter core (sRGB hex): the column's core, the flash.</summary>
        public static int CoreHex(NativeCacheTier t)=>core[Index(t)];
        /// <summary>The lock lamps on the locker, and the for-sale count they blink: 1 to 4.</summary>
        public static int Lamps(NativeCacheTier t)=>Index(t)+1;
        public static string Name(NativeCacheTier t)=>names[Index(t)];
        /// <summary>The label's title: RARE CACHE.</summary>
        public static string Title(NativeCacheTier t)=>titles[Index(t)];
        /// <summary>The label tone of a tier (its colour through the HUD palette).</summary>
        public static ExpeditionTone Tone(NativeCacheTier t)=>ExpeditionTone.CacheStandard+Index(t);
        /// <summary>The tier a cache tone names (Standard for any other tone).</summary>
        public static NativeCacheTier OfTone(ExpeditionTone tone)=>tone>=ExpeditionTone.CacheStandard&&tone<=ExpeditionTone.CacheLegendary?(NativeCacheTier)(tone-ExpeditionTone.CacheStandard):NativeCacheTier.Standard;
        public static bool IsCacheTone(ExpeditionTone tone)=>tone>=ExpeditionTone.CacheStandard&&tone<=ExpeditionTone.CacheLegendary;
    }

    /// <summary>
    /// The caches' words (R9 "Text"): RARE CACHE (in the tier colour) · $175 · 3 CHIPS · KEEP 1 · F · OPEN CACHE; or $75 SHORT
    /// in red; or RESTOCKS IN 0:42. No raw id anywhere (the stock generation is never shown). Compose through
    /// <see cref="ExpeditionLabels"/>, so a steady view allocates nothing per frame.
    /// </summary>
    public static class NativeCacheText
    {
        public const string Offer="3 CHIPS · KEEP 1",Key="F",Open="OPEN CACHE",Prompt="F · OPEN CACHE",ShortSuffix=" SHORT",Restocks="RESTOCKS IN ",
            Sold="SOLD",Bought="BOUGHT · YOURS",Opened="OPENED · C TO CHOOSE",NotMember="NOT IN THIS RUN",Separator=" · ";
        /// <summary>The for-sale detail: $175 · 3 CHIPS · KEEP 1.</summary>
        public static string Detail(ExpeditionLabels l,int price)=>l.Join(l.Money(price),Join3);
        private const string Join3=" · "+Offer;
        /// <summary>$75 SHORT.</summary>
        public static string Short(ExpeditionLabels l,int shortBy)=>l.Join(l.Money(shortBy),ShortSuffix);
        /// <summary>RESTOCKS IN 0:42.</summary>
        public static string Restocking(ExpeditionLabels l,int seconds)=>l.Clock(Restocks,seconds);
        /// <summary>m:ss (0:42, 1:05).</summary>
        public static string Clock(int seconds){if(seconds<0)seconds=0;int m=seconds/60,s=seconds%60;return m.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+(s<10?"0":"")+s.ToString(System.Globalization.CultureInfo.InvariantCulture);}
    }
}
