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
        void Row(string stat,string op,double value,string text,string tone)
        {var line=NativeChipStatText.Effect(NativeMovementContent.Effect(stat,op,value));Check(line!=null&&line.text==text&&line.tone==tone,"stat row "+stat+" "+value);}
        Row("speed","mul",1.25,"+25% Move speed","good");Row("damage","mul",.75,"-25% Damage","bad");
        Row("reloadSpeed","mul",.8,"-20% Reload time","good");Row("reloadSpeed","mul",1.2,"+20% Reload time","bad");
        Row("damageTaken","mul",1.15,"+15% Damage taken","bad");Row("blockCooldown","mul",.8,"-20% Block cooldown","good");
        Row("extraAirJumps","add",2,"+2 Air jumps","good");Row("blockWindow","add",.2,"+0.2 s Block window","good");
        Row("lifeSteal","add",.15,"+15% Life steal","good");Row("fanDegrees","add",12,"+12° Spread","neutral");
        Row("playerGravity","mul",.85,"-15% Gravity","neutral");Row("vacuum","add",700,"Hits pull enemies inward","good");
        Check(NativeChipStatText.Effect(NativeMovementContent.Effect("boomerang","add",1))==null,"switch description is not a made-up stat");
        Check(NativeChipStatText.Effect(NativeMovementContent.Effect("damage","mul",1))==null,"no zero delta row");
        var rowChips=NativeMovementContent.Chips().Concat(NativeRoundsCards.Chips()).Concat(NativeRoundsPacks.Chips()).ToArray();
        foreach(var c in rowChips)
        {
            string before=JsonSerializer.Serialize(c,options);var rows=NativeChipStatText.Lines(c);
            Check(rows.All(x=>x.tone=="good"||x.tone=="bad"||x.tone=="neutral"),"valid tones "+c.id);
            Check(rows.Length==c.effects.Count(f=>NativeChipStatText.Effect(f)!=null),"every numeric effect has a row "+c.id);
            Check(before==JsonSerializer.Serialize(c,options),"display leaves chip gameplay and hash unchanged "+c.id);
            Check(rows.Select(x=>x.tone=="good"?0:x.tone=="neutral"?1:2).SequenceEqual(rows.Select(x=>x.tone=="good"?0:x.tone=="neutral"?1:2).OrderBy(x=>x)),"benefits before penalties "+c.id);
        }
        var oldCulture=System.Globalization.CultureInfo.CurrentCulture;
        try{System.Globalization.CultureInfo.CurrentCulture=new System.Globalization.CultureInfo("fr-FR");Row("blockWindow","add",.2,"+0.2 s Block window","good");}
        finally{System.Globalization.CultureInfo.CurrentCulture=oldCulture;}
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
        var more=Load();NativeRoundsCards.Apply(more);NativeRoundsPacks.Apply(more,NativeRoundsPacks.Ids);more.Validate();
        var added=NativeRoundsPacks.Chips();Check(added.Length==118,"118 new reviewed adaptations");
        var reverse=Load();NativeRoundsCards.Apply(reverse);NativeRoundsPacks.Apply(reverse,NativeRoundsPacks.Ids.Reverse());Check(reverse.contentRevision==more.contentRevision,"pack selection order canonical");
        Check(more.chips.Select(c=>c.id).Distinct().Count()==more.chips.Length&&more.chips.Length+NativeMods.MaxChips<=NativeMods.MaxCatalog,"unique IDs and room for external packs");
        foreach(var chip in added)
        {
            Check(NativeMods.CreditValid(chip.author)&&!string.IsNullOrEmpty(chip.author)&&!string.IsNullOrEmpty(chip.sourceCard),"new pack attribution");
            foreach(var weapon in more.weapons)
            {
                var held=new Dictionary<string,int>{{chip.id,chip.max}};var stats=NativeContentRules.Stats(more,weapon.id,held);
                Check(more.baseStats.All(stat=>!double.IsNaN(stats[stat.name])&&!double.IsInfinity(stats[stat.name])),"new effects finite");
                Check(stats["ammoCapacity"]>0&&stats["hp"]>0&&stats["attackRate"]>0,"positive combat stats");
                Check(!NativeContentRules.Eligible(more,weapon.id,held).Any(c=>c.id==chip.id),"new stack cap");
            }
            if(chip.mechanic!=null)
            {
                var held=new Dictionary<string,int>{{chip.id,1}};Check(NativeRoundsCards.Has(held,chip.mechanic),"new trusted mechanic alias");
                foreach(var other in more.chips.Where(c=>c.id!=chip.id&&(c.id==chip.mechanic||c.mechanic==chip.mechanic)))
                {
                    Check(!NativeContentRules.Eligible(more,"pistol",held).Any(c=>c.id==other.id),"same mechanic excluded across packs");
                    Check(!NativeContentRules.Eligible(more,"pistol",new Dictionary<string,int>{{other.id,1}}).Any(c=>c.id==chip.id),"reverse cross-pack exclusion");
                }
                held[chip.id]=-1;Check(!NativeRoundsCards.Has(held,chip.mechanic),"negative adaptation count inactive");
            }
        }
        Check(!NativeContentRules.Eligible(more,"bow",empty).Any(c=>added.Any(n=>n.id==c.id&&n.tags.Contains("gun"))),"ammo-only adaptations never offered to bow");
        Check(NativeContentRules.Eligible(more,"pistol",empty).Any(c=>c.id=="rounds-keys:mending-reload"),"gun adaptation offered to gun");
        foreach(var selection in new[]{new[]{"missing"},new[]{"vecs","vecs"},new string[]{null}})
        {var invalid=Load();string original=invalid.contentRevision;bool rejected=false;try{NativeRoundsPacks.Apply(invalid,selection);}catch(ArgumentException){rejected=true;}Check(rejected&&invalid.contentRevision==original,"invalid selection rejected before mutation");}
        var noPacks=Load();NativeRoundsPacks.Apply(noPacks,null);Check(noPacks.contentRevision==revision,"null/default selection vanilla");
        foreach(var id in NativeRoundsPacks.Ids){var one=Load();NativeRoundsPacks.Apply(one,new[]{id});Check(one.chips.Where(c=>c.id.StartsWith("rounds-",StringComparison.Ordinal)).All(c=>c.id.StartsWith("rounds-"+id+":",StringComparison.Ordinal)),"independent pack toggle");Check(one.contentRevision!=more.contentRevision,"different pack selection rejects peers");}
        Check(NativeMods.Credit(new ContentChip())==""&&NativeMods.CreditValid(null),"legacy no-credit compatibility");
        Check(!NativeMods.CreditValid("<size=99>bad")&&!NativeMods.CreditValid("a\nb")&&!NativeMods.CreditValid(new string('a',49)),"unsafe/oversized credit rejected");
        var pack=new NativeChipPack{schema=1,id="author-pack",name="Author pack",version="1.0.0",author="Pack author",chips=new[]{new ContentChip{id="step",name="Step",description="Speed",rarity="common",max=1,tags=new[]{"all"},effects=new[]{NativeMovementContent.Effect("speed","mul",1.1)}}}};
        var ext=Load();NativeMods.Apply(ext,0,new[]{new NativeLoadedPack{Pack=pack,Digest=NativeMapRules.Revision("author fixture")}});Check(ext.Chip("author-pack:step").author=="Pack author","pack credit inheritance");
        pack.chips[0].author="Individual";ext=Load();NativeMods.Apply(ext,0,new[]{new NativeLoadedPack{Pack=pack,Digest=NativeMapRules.Revision("individual fixture")}});Check(ext.Chip("author-pack:step").author=="Individual","chip credit override");
        pack.id="rounds-vecs";bool reserved=false;try{NativeMods.Apply(Load(),0,new[]{new NativeLoadedPack{Pack=pack,Digest=NativeMapRules.Revision("reserved")}});}catch(ArgumentException){reserved=true;}Check(reserved,"external pack cannot impersonate trusted bundled mechanics");
        Console.WriteLine($"{checks} ROUNDS adaptation checks passed");
    }
}
