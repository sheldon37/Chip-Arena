using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class ExpansionFoundationChecks
{
    public static void Run(string root)
    {
        int n=0;void Check(bool ok,string why){n++;if(!ok)throw new Exception("Expansion foundation: "+why);}
        var options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
        var data=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),options);
        NativeWildChips.Apply(data);NativeAbilityChips.Apply(data);NativeThirdTechniqueContent.Apply(data);data.Validate();
        Check(data.chips.Count(c=>NativeWildChips.Is(c.id))==50&&data.chips.Count(c=>NativeAbilityChips.Is(c.id))==48,"complete catalogs staged");
        Check(NativeTechniques.Third.Count(d=>data.abilities.Any(a=>a.id==d.Id&&a.live))==NativeThirdTechniqueContent.Ready.Length,"only completed technique content rows enabled");
        var held=new Dictionary<string,int>();string map=NativeTechniqueLoadout.With("","sword","q","riposte");
        Check(NativeAbilityChipRules.Slotted(data,"sword",map,"ab-perfect-form")&&!NativeAbilityChipRules.Slotted(data,"sword","","ab-perfect-form"),"technique chip follows selected slot");
        Check(NativeAbilityChipRules.Slotted(data,"machinegun","","ab-cooling-jacket"),"bots/default Q accepted");
        foreach(var c in data.chips.Where(c=>NativeAbilityChips.Requires(c.id)!=null&&!NativeAbilityChips.Is(c.id)))Check(!NativeAbilityChipRules.Slotted(data,"sword","",c.id),"old ability chip gated: "+c.id);
        Check(NativeContentRules.Eligible(data,"sword",held,map).Any(c=>c.id=="ab-perfect-form")&&!NativeContentRules.Eligible(data,"sword",held,"").Any(c=>c.id=="ab-perfect-form"),"melee ability tag offered only with required slot");
        var offers=NativeContentRules.Offers(data,"sword",held,()=>.1,3,c=>c.id=="ab-perfect-form"||c.id=="wild-top-shape",abilityLoadout:map);
        Check(offers[0]=="ab-perfect-form"&&offers.Distinct().Count()==offers.Count,"35 percent reservation uses eligible chip without duplicates");
        var run=NativeExpedition.Create("expansion-offers","city","host","water",42,new[]{new ExpeditionInstance{id="ward",target=1}});
        run.Enter("ward");run.Progress("quota",1);run.StartBosses(new[]{"boss"});run.DefeatBoss("boss");
        string loadout=NativeTechniqueLoadout.With("","water","e","healing-spring");var session=new ExpeditionSession(run,data,_=>{});
        session.AbilityLoadout=_=>loadout;session.ChipFilter=c=>c.id=="ab-sacred-spring";
        string reward=run.Snapshot().rewards.First(r=>r.owner=="host").id;var first=session.Offers("host",reward);
        Check(first.SequenceEqual(new[]{"ab-sacred-spring"}),"Expedition filters mechanics and uses current slot");
        loadout="";first[0]="mutated";Check(session.Offers("host",reward).SequenceEqual(new[]{"ab-sacred-spring"}),"opened offers stay fixed across slot swap and caller mutation");
        foreach(string id in NativeThirdTechniqueContent.Ready)
        {
            var def=NativeTechniques.Find(id);var row=data.abilities.Single(a=>a.id==id);
            string slotted=NativeTechniqueLoadout.With("",def.Spec,"e",id);
            Check(NativeTechniqueLoadout.Ability(data,slotted,def.Spec,"e").id==id&&row.cooldown==def.Cooldown&&row.cost==def.Cost,"third row/loadout/cost: "+id);
        }
        foreach(string id in new[]{"bipod","dragon-breath","soul-siphon"})
        {
            var k=new NativeTechnique{Kind=id,Phase=id=="bipod"?"planted":id=="dragon-breath"?"breath":"drain",At=10,Release=10.5f,Until=15.5f,RecoverUntil=10.5f};
            Check(k.Valid()&&NativeTechniqueRules.Recast(k,11)&&NativeTechniqueRules.Moving(k,11),"live channel recast and interruption: "+id);
            k.RecastAt=11;Check(!NativeTechniqueRules.Recast(k,11),"channel cannot recast twice: "+id);
        }
        foreach(string spec in new[]{"sword","shield","greatsword","rifle","bow","water"})
            Check(NativeThirdTechniqueContent.Ready.Count(id=>NativeTechniques.Find(id).Spec==spec)==3,"all three priority techniques enabled: "+spec);
        foreach(string id in new[]{"iaido","lodestone","blade-drag"})
        {
            var k=new NativeTechnique{Kind=id,Phase=id=="iaido"?"sheathe":id=="lodestone"?"draw":"drag",At=0,Release=.1f,Until=2.5f,RecoverUntil=.1f};
            Check(k.Valid()&&NativeTechniqueRules.Moving(k,1)&&NativeTechniqueRules.Recast(k,1),"charge interruption and recast: "+id);
            k.RecastAt=1;Check(!NativeTechniqueRules.Recast(k,1),"charged release is one-shot: "+id);
        }
        foreach(string id in new[]{"ab-moonlit-edge","ab-molten-trail","ab-hollow-point","ab-short-leash","ab-double-bubble"})
        {
            var ability=NativeTechniques.Find(NativeAbilityChips.Requires(id));var slots=NativeTechniqueLoadout.With("",ability.Spec,"e",ability.Id);
            Check(NativeAbilityChipRules.Slotted(data,ability.Spec,slots,id)&&!NativeAbilityChipRules.Slotted(data,ability.Spec,"",id),"new chip requires its exact equipped technique: "+id);
        }
        var jet=new NativeTechnique{Kind="hydro-jet",Phase="jet",At=0,Release=.1f,Until=1.6f,RecoverUntil=1.6f};
        Check(jet.Valid()&&NativeTechniqueRules.Busy(jet,1)&&NativeTechniqueRules.Moving(jet,1)&&NativeTechniqueRules.Recast(jet,1),"hydro jet movement, attack lock, recast");
        NativeTechniqueRules.End(jet,1);Check(!NativeTechniqueRules.Live(jet,1)&&!NativeTechniqueRules.Busy(jet,1)&&jet.Valid(),"interrupt releases jet lock");
        NativeTechniqueMark[] marks=null;for(int i=1;i<=8;i++)marks=NativeCombatMarks.Put(marks,"disarm",i,i,i+10);
        Check(marks.Length==6&&marks[0].By==3&&NativeCombatMarks.Has(marks,"disarm",9),"six-mark bound evicts oldest");
        marks=NativeCombatMarks.Put(marks,"disarm",8,9,20);Check(marks.Length==6&&marks.Last().Until==20,"same effect/source refreshes");
        var actor=new Actor{TechniqueMarks=marks,ChipGauges=new[]{new NativeChipGauge{Kind="wild-planted",Value=.5f}}};var copy=actor.ViewCopy();actor.TechniqueMarks[0].Until=99;actor.ChipGauges[0].Value=1;
        Check(copy.TechniqueMarks[0].Until!=99&&copy.ChipGauges[0].Value==.5f,"replay owns marks and gauges");
        var wire=JsonSerializer.Deserialize<Actor>(JsonSerializer.Serialize(copy,options),options);Check(NativeCombatMarks.Valid(wire.TechniqueMarks,wire.ChipGauges)&&wire.TechniqueMarks.Length==6,"wire fields round trip");
        wire.TechniqueMarks[0].Until=float.NaN;Check(!NativeCombatMarks.Valid(wire.TechniqueMarks,wire.ChipGauges),"corrupt mark rejected");
        Check(!NativeCombatMarks.Valid(null,new[]{new NativeChipGauge{Kind="wild-planted",Value=2}}),"out of range gauge rejected");
        Console.WriteLine("PASS expansion foundation: "+n+" checks");
    }
}
