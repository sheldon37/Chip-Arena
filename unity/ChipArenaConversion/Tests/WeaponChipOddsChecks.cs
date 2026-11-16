using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;

internal static class WeaponChipOddsChecks
{
    public static void Run(string root)
    {
        var options=new JsonSerializerOptions{IncludeFields=true};
        var content=JsonSerializer.Deserialize<ConversionContent>(
            File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),options);
        var fixtures=JsonSerializer.Deserialize<ParityFixtures>(
            File.ReadAllText(Path.Combine(root,"Tests/source-parity.json")),options);
        foreach(var row in fixtures.cases)
        {
            var held=row.chips.ToDictionary(c=>c.id,c=>c.count);
            var picks=NativeContentRules.Offers(content,row.spec,held,new SourceRandom(row.seed).Next);
            if(!picks.SequenceEqual(row.unityOffers??row.offers))throw new Exception("Seeded draft: "+row.spec+" "+row.seed);
        }
        NativeGlaiveChips.Apply(content);
        NativeKatanaChips.Apply(content);
        NativeFangsChips.Apply(content);
        NativeBombChips.Apply(content);
        NativeDogChips.Apply(content);
        var empty=new Dictionary<string,int>();
        foreach(var spec in content.exclusiveOdds.Select(o=>o.spec).Concat(new[]{"leet"}))
        {
            string tier=spec=="leet"?"l44t":spec=="emperor"?"mishima":spec;
            bool Both(ContentChip card)=>card.rarity==tier||card.rarity=="uncommon";
            foreach(var weights in new[]{null,new Dictionary<string,double>{{"uncommon",7}}})
            {
                int signature=0;
                for(int i=0;i<100;i++)
                {
                    var picks=NativeContentRules.Offers(content,spec,empty,()=>(i+.5)/100,1,Both,weights);
                    if(content.Chip(picks.Single()).rarity==tier)signature++;
                }
                if(signature!=50)throw new Exception(spec+" should split the two eligible tiers equally");
            }
            var excluded=NativeContentRules.Offers(content,spec,empty,()=>0,3,c=>c.rarity==tier,exclusives:false);
            if(excluded.Count!=0)throw new Exception(spec+" leaked an exclusive into a restricted draft");
            var rareOnly=new Dictionary<string,double>{{"rare",1}};
            if(NativeContentRules.Offers(content,spec,empty,()=>0,3,Both,rareOnly).Count!=0)
                throw new Exception(spec+" leaked a zero-weight tier");
        }
        Console.WriteLine("PASS weapon chip odds: 9 signature sets, default and custom weights, restricted offers and "+fixtures.cases.Length+" seeded drafts");
    }
}
