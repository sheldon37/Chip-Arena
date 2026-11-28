#if ARENA_TESTS
using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    internal static class ExpeditionSplitChecks
    {
        public static void Run()
        {
            int n=0;void Check(bool ok,string why){if(!ok)throw new Exception("Split authority: "+why);n++;}
            var root=NativeExpedition.Create("split","hub","host","sword",17,new[]{new ExpeditionInstance{id="ward",target=2},new ExpeditionInstance{id="station",target=2},new ExpeditionInstance{id="foundry",target=2}});
            foreach(var id in new[]{"a","b","c"})Check(root.Join(id,"sword"),"join");
            root.EnableAreas();var ward=root.ForArea("ward");var station=root.ForArea("station");var foundry=root.ForArea("foundry");
            Check(!root.Enter("ward"),"legacy party teleport disabled");
            Check(root.MoveMembers("hub","ward",new[]{"a","b"}),"captured subset commits");
            Check(root.Snapshot().members.Select(m=>m.account).SequenceEqual(new[]{"host","c"})&&ward.Snapshot().members.Length==2,"bystanders remain hub members");
            Check(ward.Snapshot().instances.Single(i=>i.id=="ward").scalingParty==2,"first cohort locks encounter scaling");
            Check(root.MoveMembers("hub","station",new[]{"c"}),"second independent cohort");
            Check(root.HoldMembers(new[]{"a"},true)&&!ward.Spend("a","held",1)&&root.SetSafe("host",true),"transfer hold leaves hub active");
            Check(root.HoldMembers(new[]{"a"},false),"release transfer");
            for(int tick=0;tick<60;tick++){root.Tick(1d/60);ward.Tick(1d/60);station.Tick(1d/60);foundry.Tick(1d/60);}
            Check(Math.Abs(root.Elapsed-1)<1e-8&&root.Elapsed==ward.Elapsed,"one shared elapsed clock");
            Check(ward.Credit("a","kill",100)&&!ward.Credit("a","kill",100),"scoped kill credit once");
            Check(root.PartySnapshot().members.Single(m=>m.account=="a").wallet==100,"one shared wallet ledger");
            Check(!root.Spend("a","remote",1)&&ward.Spend("a","med",25),"commands constrained to member area");
            foreach(var area in new[]{ward,station,foundry})
            {Check(area.Progress("same-event",2)&&!area.Progress("same-event",2),"event dedupe per area");Check(area.StartBosses(new[]{"same-boss"})&&area.DefeatBoss("same-boss",1),"independent boss completion");}
            Check(root.FinalUnlocked&&!root.BeginFinal(),"all clears still require party home");
            Check(ward.VoteExit("a",true)&&station.Snapshot().exitAt<0&&root.Snapshot().exitAt<0,"exit vote scoped");
            Check(ward.VoteExit("b",true)&&ward.DueReturns().Length==2&&station.DueReturns().Length==0,"unanimous subset returns immediately");
            Check(root.MoveMembers("ward","hub",ward.DueReturns()),"subset return");
            Check(station.Defeat("c")&&root.Credit("host","rescue",100)&&root.SetSafe("host",true),"remote down and funded rescuer");
            root.ReprintPlacement=(_,__)=>false;
            Check(!root.Reprint("host","c",100,true)&&root.Snapshot().members.Single(m=>m.account=="host").wallet==100,"failed native placement does not debit or revive");
            root.ReprintPlacement=(_,target)=>root.MoveMembers("station","hub",new[]{target});
            Check(root.Reprint("host","c",100,true)&&!root.Reprint("host","c",100,true),"successful remote recovery debits once");
            Check(root.Snapshot().members.Single(m=>m.account=="c").alive&&root.Snapshot().members.Single(m=>m.account=="host").wallet==0,"recovered member shares committed ledger");
            Check(root.BeginFinal()&&root.FinishFinal(4)&&root.NextLoop(),"shared final and next loop");
            Check(root.Snapshot().loop==1&&ward.Snapshot().phase==ExpeditionPhase.Instance&&!root.FinalUnlocked&&root.Elapsed>0,"loop resets encounters while preserving time");
            var copy=root.PartySnapshot();copy.members[0].wallet=999;Check(root.PartySnapshot().members[0].wallet!=999,"detached projections");
            Console.WriteLine("PASS split area authority: "+n+" checks");
        }
    }
}
#endif
