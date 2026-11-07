using System;
using System.Linq;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Boss relics: each boss's own two chips (the owner, 2026-11-07: "Give the bosses exclusive chip drops ... themed around the boss, a special design and pick effect ... ONLY possible from the boss drop chip
    /// selection and a rare chance"). Twelve chips, two per boss, one copy each, in the boss's own tier (its card, its 3D chip, its pick: Runtime NativeBossChipArt). docs/BOSS-CHIPS.md.
    /// Live: the content carries the rows (Runtime NativeStructures), every ordinary offer leaves them out (NativeContentRules.Offers), and a boss's reward screen swaps one offer for one of
    /// that boss's relics at <see cref="DropChance"/> (<see cref="RewardBoss"/>, <see cref="Drop"/>). Their rules: Runtime NativeBossRelicRules. Pure.
    /// </summary>
    public static class NativeBossChips
    {
        public const string Prefix="boss-",TierPrefix="boss-";
        /// <summary>The chance a boss reward screen swaps one of its offers for one of that boss's relics (one at most, never one already held).</summary>
        public const float DropChance=.12f;
        public const string LastVerse="boss-siren-last-verse",Lullaby="boss-siren-lullaby",Express="boss-647-express",MindGap="boss-647-mind-the-gap",SlagHeart="boss-converter-slag-heart",Pour="boss-converter-pour",
            MaxHum="boss-hum-max-hum",Noclip="boss-hum-noclip",Undertow="boss-deepend-undertow",Warm="boss-deepend-warm",Feather="boss-noctiel-feather-fall",Lux="boss-noctiel-lux-cecidit";
        /// <summary>One boss's relics: its tier, the archetype it drops from, its name on the card, the line the pick confirms with, and its two chips.</summary>
        public readonly struct Set
        {
            public readonly string Tier,Boss,Name,Confirm;public readonly int Rarity,Label;
            public Set(string tier,string boss,string name,string confirm,int rarity,int label){Tier=tier;Boss=boss;Name=name;Confirm=confirm;Rarity=rarity;Label=label;}
        }
        public static readonly Set[] Sets={
            new Set("boss-siren","siren","THE SIREN","SHE IS STILL SINGING",0xa98bff,0xd6c9ff),
            new Set("boss-647",NativeZoneBossRules.Station,"THE 6:47","NOW DEPARTING",0xffb02e,0xffd27a),
            new Set("boss-converter",NativeZoneBossRules.Ironworks,"THE CONVERTER","POURED AND SET",0xff5a1c,0xffb27a),
            new Set("boss-hum",NativeHumBoss.Archetype,"THE HUM","IT HAS HEARD YOU",0xf2e49a,0xfff4c4),
            new Set("boss-deepend",NativeDeepEndBoss.Archetype,"THE DEEP END","THE WATER IS ALWAYS WARM",0x5fe8e0,0xbdfcf6),
            new Set("boss-noctiel",NativeFallenBoss.Archetype,"NOCTIEL","LUX CECIDIT",0xeeeaff,0xffffff)};
        public static readonly string[] Tiers=Sets.Select(s=>s.Tier).ToArray();
        public static bool IsTier(string tier)=>tier!=null&&Array.IndexOf(Tiers,tier)>=0;
        public static int SetOf(string tier)=>Array.IndexOf(Tiers,tier);
        public static bool Is(string id)=>Find(id)>=0;

        /// <summary>A relic: its id, set, name, icon, the card's short line and its green/red rows, the rule's words (docs/BOSS-CHIPS.md) and its mechanic id.</summary>
        public readonly struct Relic
        {
            public readonly string Id,Name,Icon,Short,Rule;public readonly int Set;public readonly ChipStatText[] Rows;
            public Relic(int set,string id,string name,string icon,string shortLine,string rule,params ChipStatText[] rows){Set=set;Id=id;Name=name;Icon=icon;Short=shortLine;Rule=rule;Rows=rows;}
            public string Tier=>Sets[Set].Tier;
        }
        private static ChipStatText Good(string t)=>new ChipStatText{text=t,tone="good"};
        private static ChipStatText Bad(string t)=>new ChipStatText{text=t,tone="bad"};
        public static readonly Relic[] Relics={
            new Relic(0,"boss-siren-last-verse","Last Verse","volume-2","Your kills sing.","A kill sends out a ring of her song: every enemy within 6 m is slowed 40% for 2 s.",Good("Kills slow enemies within 6 m by 40%"),Good("for 2 s")),
            new Relic(0,"boss-siren-lullaby","Lullaby","radio","They come when called.","Every 6th hit on the same enemy drags them 2.5 m toward you.",Good("Every 6th hit drags them 2.5 m to you")),
            new Relic(1,"boss-647-express","Express Service","gauge","Pick up speed.","Sprinting for 2 s brings you to full speed (+25% move speed). Your first hit at full speed deals +60% and throws them.",Good("+25% speed after 2 s of sprint"),Good("First hit at full speed +60% and a throw")),
            new Relic(1,"boss-647-mind-the-gap","Mind the Gap","zap","Your roll leaves a live rail.","A roll leaves a live rail along its path for 1.5 s. An enemy who crosses it takes 30 and is stunned .4 s, once per rail.",Good("A live rail behind your roll for 1.5 s"),Good("30 damage and a .4 s stun crossing it")),
            new Relic(2,"boss-converter-slag-heart","Slag Heart","flame","What hurts you, you keep.","Damage you take is stored as heat. Your next hit releases it: +1 damage for every 2 heat, at most +40.",Good("Next hit +1 per 2 damage taken"),Good("Up to +40")),
            new Relic(2,"boss-converter-pour","The Pour","radiation","Kills spill slag.","A kill spills molten slag 2.5 m wide for 3 s. Enemies standing in it take 10 a second.",Good("Kills spill slag for 3 s"),Good("10 damage a second to enemies in it")),
            new Relic(3,"boss-hum-max-hum","Max Hum-Buzz","activity","Every hit hums louder.","Each hit builds a hum in its target. The fifth bursts it: 30 damage to every enemy within 3 m of them.",Good("5th hit on one enemy bursts"),Good("30 damage within 3 m of them")),
            new Relic(3,"boss-hum-noclip","Noclip","ghost","Roll straight through them.","Your roll passes through enemies. The first one it passes through takes 20 and is shown to you through walls for 3 s.",Good("Roll through enemies: 20 damage"),Good("Seen through walls for 3 s")),
            new Relic(4,"boss-deepend-undertow","Undertow","waves","Your hits pull them under.","Your hits pull their target 1 m toward you, and every 4th hit on one enemy drags them 3 m.",Good("Hits pull them 1 m to you"),Good("Every 4th hit drags them 3 m")),
            new Relic(4,"boss-deepend-warm","The Water Is Warm","droplets","Be still. It's warm.","Standing still for 1 s heals you 4% of your max health a second, until you move or are hit.",Good("Still for 1 s: heal 4% max health a second"),Bad("Stops when you move or are hit")),
            new Relic(5,"boss-noctiel-feather-fall","Feather Fall","feather","Fall like a feather.","You fall slowly. Landing from 3 m or higher sends out a ring of light: 25 damage to enemies within 4 m.",Good("You fall slowly"),Good("Landing from 3 m: 25 damage within 4 m")),
            new Relic(5,"boss-noctiel-lux-cecidit","Lux Cecidit","sun","The light falls once.","Once a round, a blow that would kill you leaves you at 1 health instead, and you burst with light: 40 damage to enemies within 5 m.",Good("Once a round, survive a lethal blow at 1 health"),Good("40 damage within 5 m"))};
        public static int Find(string id){if(id==null||!id.StartsWith(Prefix,StringComparison.Ordinal))return -1;for(int i=0;i<Relics.Length;i++)if(Relics[i].Id==id)return i;return -1;}
        /// <summary>The relics of the boss with this archetype (none: an empty array).</summary>
        public static Relic[] Of(string boss)=>Relics.Where(r=>Sets[r.Set].Boss==boss).ToArray();
        /// <summary>Each relic's mechanic: the rule it needs (all of them: none is a plain stat).</summary>
        public static string[] Mechanics=>Relics.Select(r=>r.Id).ToArray();
        /// <summary>The card's copy (Core NativeChipCopy reads it).</summary>
        public static bool Copy(string id,out string shortLine,out ChipStatText[] rows){int i=Find(id);if(i<0){shortLine=null;rows=null;return false;}shortLine=Relics[i].Short;rows=Relics[i].Rows;return true;}
        public static string Icon(string id){int i=Find(id);return i<0?null:Relics[i].Icon;}
        public static ContentChip Chip(in Relic r)=>new ContentChip{id=r.Id,name=r.Name,description=r.Rule,rarity=r.Tier,max=1,icon=r.Icon,tags=new[]{"all"},excludes=Array.Empty<string>(),mechanic=r.Id,effects=Array.Empty<ChipEffect>(),author="",sourcePack="",sourceCard=""};
        public static ContentChip[] Chips()=>Relics.Select(r=>Chip(r)).ToArray();
        /// <summary>The boss an expedition boss reward came from: the zone in its id (the run's key, loop:area:boss:id:n, the final's loop:area:final:boss:n) and that zone's map. A zone with no zone
        /// boss of its own fights the Siren (as the runtime spawns it). Null when the layout doesn't know the zone.</summary>
        public static string RewardBoss(string rewardId,ExpeditionLayout layout)
        {
            if(string.IsNullOrEmpty(rewardId)||layout?.areas==null)return null;var parts=rewardId.Split(':');if(parts.Length<4)return null;
            var area=layout.Area(parts[1]);if(area==null)return null;return NativeZoneBossRules.Archetype(area.map)??"siren";
        }
        /// <summary>The rare drop: with <see cref="DropChance"/> one of the boss's relics the pilot may take (`eligible`), else null. The chance is drawn first, always.</summary>
        public static string Drop(string boss,Func<string,bool> eligible,Func<double> random)
        {
            if(random==null)return null;double roll=random();if(roll>=DropChance||boss==null)return null;
            var own=Of(boss).Where(r=>eligible==null||eligible(r.Id)).ToArray();if(own.Length==0)return null;
            return own[Math.Min(own.Length-1,(int)(Math.Max(0,random())*own.Length))].Id;
        }
        /// <summary>Puts a dropped relic in an offer list: in the last place (the rest stay the order they rolled), or added when the list is short.</summary>
        public static string[] Place(string[] offers,string relic,int count=3)
        {if(relic==null)return offers;offers=offers??Array.Empty<string>();if(offers.Length>=count){var o=(string[])offers.Clone();o[o.Length-1]=relic;return o;}return offers.Concat(new[]{relic}).ToArray();}
        /// <summary>Adds the rows to a content (nothing if they are there): the live content (Runtime NativeStructures) and the DEV Sandbox's preview.</summary>
        public static void Apply(ConversionContent content)
        {
            if(content.chips.Any(c=>Is(c.id)))return;content.chips=content.chips.Concat(Chips()).ToArray();
            if(content.rarities!=null&&!content.rarities.Any(r=>IsTier(r.id)))content.rarities=content.rarities.Concat(Sets.Select(s=>new RarityData{id=s.Tier,name=s.Name,color="#"+s.Rarity.ToString("x6"),weight=0,leetWeight=0})).ToArray();
            content.contentRevision=NativeMapRules.Revision(content.contentRevision+"\nboss-relics-v1");
        }
    }
}
