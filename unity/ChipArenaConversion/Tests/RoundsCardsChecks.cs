using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class RoundsCardsChecks
{
    public static void Run(string root)
    {
        int checks=0;void Check(bool yes,string why){checks++;if(!yes)throw new Exception("Rounds adaptations: "+why);}
        var options=new JsonSerializerOptions{IncludeFields=true};
        ConversionContent Load(){var c=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),options);NativeMovementContent.Apply(c);return c;}
        var vanilla=Load();string revision=vanilla.contentRevision;var content=Load();NativeRoundsCards.Apply(content);content.Validate();
        var chips=NativeRoundsCards.Chips();Check(chips.Length==20&&chips.Select(c=>c.id).Distinct().Count()==20,"20 unique adaptations");
        Check(content.chips.Length==vanilla.chips.Length+20&&content.contentRevision!=revision,"optional catalog and handshake");
        var twin=Load();NativeRoundsCards.Apply(twin);Check(twin.contentRevision==content.contentRevision,"deterministic fingerprint");
        var empty=new Dictionary<string,int>();Check(!NativeRoundsCards.Has(empty,"orbitals"),"disabled means no grants");
        foreach(var chip in chips)
        {
            Check(chip.author=="willis81808"&&chip.sourceCard.Length>0&&NativeMods.Credit(chip)=="By willis81808","source credit "+chip.id);
            var decoded=JsonSerializer.Deserialize<ContentChip>(JsonSerializer.Serialize(chip,options),options);Check(decoded.sourceCard==chip.sourceCard&&decoded.author==chip.author,"credit serialization");
            var held=new Dictionary<string,int>{{chip.id,chip.max}};
            foreach(var spec in content.weapons.Select(w=>w.id))
            {
                var stats=NativeContentRules.Stats(content,spec,held);Check(content.baseStats.All(s=>!double.IsNaN(stats[s.name])&&!double.IsInfinity(stats[s.name])),"finite effects "+chip.id+" "+spec);
                Check(!NativeContentRules.Eligible(content,spec,held).Any(c=>c.id==chip.id),"max stacks enforced");
            }
            if(chip.mechanic!=null&&!chip.mechanic.StartsWith("rounds-"))
            {
                Check(NativeRoundsCards.Has(held,chip.mechanic),"mechanic grant "+chip.id);
                Check(!NativeContentRules.Eligible(content,"pistol",held).Any(c=>c.id==chip.mechanic),"original counterpart excluded");
                Check(!NativeContentRules.Eligible(content,"pistol",new Dictionary<string,int>{{chip.mechanic,1}}).Any(c=>c.id==chip.id),"reverse exclusion");
                held[chip.id]=0;Check(!NativeRoundsCards.Has(held,chip.mechanic),"zero stacks no proc");
            }
        }
        Check(!NativeContentRules.Eligible(content,"sword",empty).Any(c=>c.id==NativeRoundsCards.Prefix+"excalibur"),"projectile adaptation not offered to melee");
        var statsHare=NativeContentRules.Stats(content,"pistol",new Dictionary<string,int>{{NativeRoundsCards.Prefix+"hare",1}});Check(Math.Abs(statsHare["speed"]-1.2)<1e-9&&Math.Abs(statsHare["reloadSpeed"]-.8)<1e-9,"hare tuning");
        Check(NativeMods.Credit(new ContentChip())==""&&NativeMods.CreditValid(null),"legacy no-credit compatibility");
        Check(!NativeMods.CreditValid("<size=99>bad")&&!NativeMods.CreditValid("a\nb")&&!NativeMods.CreditValid(new string('a',49)),"unsafe/oversized credit rejected");
        var pack=new NativeChipPack{schema=1,id="author-pack",name="Author pack",version="1.0.0",author="Pack author",chips=new[]{new ContentChip{id="step",name="Step",description="Speed",rarity="common",max=1,tags=new[]{"all"},effects=new[]{NativeMovementContent.Effect("speed","mul",1.1)}}}};
        var ext=Load();NativeMods.Apply(ext,0,new[]{new NativeLoadedPack{Pack=pack,Digest=NativeMapRules.Revision("author fixture")}});Check(ext.Chip("author-pack:step").author=="Pack author","pack credit inheritance");
        pack.chips[0].author="Individual";ext=Load();NativeMods.Apply(ext,0,new[]{new NativeLoadedPack{Pack=pack,Digest=NativeMapRules.Revision("individual fixture")}});Check(ext.Chip("author-pack:step").author=="Individual","chip credit override");
        Console.WriteLine($"{checks} ROUNDS adaptation checks passed");
    }
}
