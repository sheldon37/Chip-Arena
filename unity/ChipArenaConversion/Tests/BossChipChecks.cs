using System;
using System.IO;
using System.Linq;
using ChipArena.UnityConversion;

// The boss relics (Core NativeBossChips; docs/BOSS-CHIPS.md): two per boss, one copy each, in the boss's own tier with its own look; staged out of every draft until the rules add them.
public static class BossChipChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("boss-chips: "+what);count++;}
    public static void Run(string root)
    {
        count=0;var relics=NativeBossChips.Relics;
        Check(relics.Length==12&&NativeBossChips.Sets.Length==6,"twelve relics, six bosses");
        Check(relics.Select(r=>r.Id).Distinct().Count()==relics.Length&&relics.Select(r=>r.Name).Distinct().Count()==relics.Length,"unique ids and names");
        for(int s=0;s<NativeBossChips.Sets.Length;s++)
        {
            var set=NativeBossChips.Sets[s];Check(relics.Count(r=>r.Set==s)==2,set.Name+" has two");Check(NativeBossChips.Of(set.Boss).Length==2,set.Name+" drops from its own boss");
            Check(NativeChipCartridge.Known(set.Tier)&&NativeChipCartridge.Rarity(set.Tier)==set.Rarity,set.Tier+" is a known tier in its colour");
            var look=NativeChipCartridge.Of(set.Tier);Check(look.Tier==set.Tier,set.Tier+" has its own look");Check(NativeBossChips.IsTier(set.Tier)&&!NativeBossChips.IsTier("siren")&&!NativeBossChips.IsTier("legendary"),"the tiers are the relics' own");
        }
        Check(NativeBossChips.Sets.Select(s=>NativeChipCartridge.Of(s.Tier).Inlay).Distinct().Count()==6,"six different looks");
        var icons=File.ReadAllText(Path.Combine(root,"Assets","Conversion","Resources","Icons.json"));
        foreach(var r in relics)
        {
            var chip=NativeBossChips.Chip(r);Check(chip.max==1&&chip.rarity==r.Tier&&chip.mechanic==r.Id,r.Id+": one copy, its tier, its rule");
            Check(NativeChipCopy.Find(r.Id,out var copy)&&copy.Rows!=null&&copy.Rows.Length>0&&copy.Rows.All(x=>x.tone=="good"||x.tone=="bad")&&!string.IsNullOrEmpty(copy.Short),r.Id+": a short line and green/red rows on its card");
            Check(NativeChipIcons.Native(r.Id)==r.Icon&&icons.Contains("{\"id\":\""+r.Icon+"\",\"markup\""),r.Id+": its icon is in the pinned set");
            Check(r.Rule.Length>30&&r.Rule.Length<220,r.Id+": the rule's words");
        }
        // Staged: no content offers them until the rules add the rows.
        var content=System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
        Check(!content.chips.Any(c=>NativeBossChips.Is(c.id)||NativeBossChips.IsTier(c.rarity)),"not in the live content");
        NativeBossChips.Apply(content);Check(content.chips.Count(c=>NativeBossChips.Is(c.id))==12,"the preview's copy takes all twelve");NativeBossChips.Apply(content);Check(content.chips.Count(c=>NativeBossChips.Is(c.id))==12,"once");
        Check(NativeBossChips.DropChance>0&&NativeBossChips.DropChance<.25f,"a rare chance");
        Console.WriteLine("PASS boss-chips ("+count+" checks)");
    }
}
