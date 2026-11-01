#if ARENA_TESTS
using System;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    internal static class ExpeditionHandoffChecks
    {
        private static readonly JsonSerializerOptions Codec=new JsonSerializerOptions{IncludeFields=true};
        private static string Json(object value)=>JsonSerializer.Serialize(value,Codec);
        private static ExpeditionHandoff Copy(ExpeditionHandoff value)=>JsonSerializer.Deserialize<ExpeditionHandoff>(Json(value),Codec);
        public static void Run()
        {
            int n=0;void Check(bool ok,string why){if(!ok)throw new Exception("Handoff: "+why);n++;}
            var root=NativeExpedition.Create("handoff","hub","host","sword",17,new[]{new ExpeditionInstance{id="ward",target=2},new ExpeditionInstance{id="station",target=2}});
            foreach(var id in new[]{"next","a","b"})Check(root.Join(id,"sword"),"join");
            root.EnableAreas();var ward=root.ForArea("ward");var station=root.ForArea("station");
            Check(root.MoveMembers("hub","ward",new[]{"a","b"})&&root.MoveMembers("hub","station",new[]{"next"}),"split members");
            Check(ward.Progress("kills",2)&&ward.StartBosses(new[]{"boss"})&&ward.DefeatBossAt("boss",3,4,5,3),"reward ledger and anchors");
            Check(ward.Credit("a","credit",100)&&ward.VoteExit("a",true)&&root.SetSafe("host",true),"wallet, scoped countdown, extraction eligibility");
            var candidates=new[]{new ExpeditionHostCandidate{account="next",capable=true,reachable=true}};
            Check(station.SetLoading(true)&&root.PrepareHandoff("loading",new[]{"host"},candidates)==null&&station.SetLoading(false),"wait for every area load");
            Check(root.HoldMembers(new[]{"b"},true)&&root.PrepareHandoff("moving",new[]{"host"},candidates)==null&&root.HoldMembers(new[]{"b"},false),"wait for cohort movement");
            Check(ward.PrepareHandoff("wrong-scope",new[]{"host"},candidates)==null,"prepare only from world authority");
            var h=root.PrepareHandoff("transfer",new[]{"host"},candidates);
            Check(h!=null&&h.splitAreas&&h.areas.Length==3&&ward.Migrating&&station.Migrating,"one freeze across existing area facades");
            string before=Json(root.PartySnapshot()),wardBefore=Json(ward.Snapshot());
            root.Tick(.25);ward.Tick(.25);station.Tick(.25);ward.DisableAccountXp();
            Check(!ward.Credit("a","frozen",1)&&!ward.VoteExit("b",true)&&!station.SetLoading(true)&&!root.HoldMembers(new[]{"a"},true)&&!root.MoveMembers("ward","hub",new[]{"a"}),"all mutation paths frozen");
            Check(Json(root.PartySnapshot())==before&&Json(ward.Snapshot())==wardBefore&&root.ForArea("ward").Migrating,"freeze preserves state and new facade");
            var encoded=Copy(h);var restored=NativeExpedition.RestorePreparedHandoff(encoded,"next");
            Check(restored.Migrating&&restored.ForArea("ward").Migrating&&Json(restored.PartySnapshot())==before&&Json(restored.ForArea("ward").Snapshot())==wardBefore,"serialized all-area restore is exact and frozen");
            encoded.state.members[0].wallet=999;encoded.areas.Single(a=>a.id=="ward").exitVotes[0]="bad";
            Check(Json(restored.PartySnapshot())==before&&Json(restored.ForArea("ward").Snapshot())==wardBefore,"restore owns detached state");
            Check(!restored.AbortHandoff(h.id)&&restored.Migrating,"destination abort cannot revive source epoch");
            Check(!restored.ForArea("ward").CommitHandoff(h.id,h.nextHost,h.nextEpoch,h.revision)&&!restored.CommitHandoff(h.id,h.nextHost,h.nextEpoch+1,h.revision),"wrong scope or epoch cannot commit");
            Check(restored.CommitHandoff(h.id,h.nextHost,h.nextEpoch,h.revision)&&!restored.Migrating&&!restored.ForArea("ward").Migrating,"commit releases all destination facades");
            Check(restored.PartySnapshot().host=="next"&&restored.PartySnapshot().epoch==2&&restored.PartySnapshot().members.Single(m=>m.account=="host").extracted,"host and extraction committed");
            Check(!restored.CommitHandoff(h.id,h.nextHost,h.nextEpoch,h.revision)&&!restored.ForArea("ward").Credit("a","credit",100),"commit and prior rewards remain idempotent");
            double elapsed=restored.Elapsed;restored.Tick(.25);Check(restored.Elapsed==elapsed+.25&&restored.ForArea("ward").Snapshot().exitAt==h.areas.Single(a=>a.id=="ward").exitAt,"clock resumes without resetting countdown");
            Check(restored.MoveMembers("station","hub",new[]{"next"})&&restored.SetSafe("next",true),"new host returns home");
            var again=restored.PrepareHandoff("next-transfer",new[]{"next"},new[]{new ExpeditionHostCandidate{account="a",capable=true,reachable=true}});
            Check(again!=null&&restored.AbortHandoff(again.id)&&!restored.ForArea("ward").Migrating,"committed destination can later abort as source");
            Check(root.AbortHandoff(h.id)&&!ward.Migrating&&ward.Credit("a","resumed",1),"source abort releases all areas");
            void Reject(Action<ExpeditionHandoff> change,string why)
            {
                var bad=Copy(h);change(bad);bool rejected=false;
                try{NativeExpedition.RestorePreparedHandoff(bad,"next");}catch(ArgumentException){rejected=true;}
                Check(rejected,why);
            }
            Reject(v=>v.schema++,"schema");Reject(v=>v.revision++,"revision");Reject(v=>v.nextEpoch++,"epoch");
            Reject(v=>v.oldHost="next","source identity");Reject(v=>v.nextHost="a","authenticated destination");
            Reject(v=>v.leaving=new[]{"host","next"},"target cannot leave");Reject(v=>v.leaving=null,"missing departures");
            Reject(v=>v.areas=null,"missing areas");Reject(v=>v.areas=v.areas.Take(2).ToArray(),"incomplete areas");
            Reject(v=>v.areas[1].id=v.areas[0].id,"duplicate area");Reject(v=>v.areas[0].id="unknown","unknown area");
            Reject(v=>v.state.members[0].area="unknown","unknown member area");Reject(v=>v.state.members[0].transferring=true,"pending transfer");
            Reject(v=>v.areas.Single(a=>a.id=="ward").exitVotes=new[]{"host"},"cross-area vote");
            Reject(v=>v.areas.Single(a=>a.id=="ward").exitAt=double.NaN,"invalid clock");
            Reject(v=>v.areas[0].loading=true,"loading area");Reject(v=>v.areas[0].exitVotes=null,"missing votes");
            Reject(v=>{v.splitAreas=false;v.areas=Array.Empty<ExpeditionAreaHandoff>();},"stripped split checkpoint");
            var flat=NativeExpedition.Create("flat","hub","host","sword",1,new[]{new ExpeditionInstance{id="ward",target=1}});
            Check(flat.Join("next","sword")&&flat.Enter("ward")&&flat.Progress("kill",1)&&flat.StartBosses(new[]{"boss"})&&flat.DefeatBoss("boss")&&flat.VoteExit("host",true)&&flat.VoteExit("next",true),"legacy party setup");
            flat.Tick(.25);Check(flat.SetSafe("host",true),"legacy safe exit");var flatHandoff=flat.PrepareHandoff("flat-transfer",new[]{"host"},candidates);
            var flatRestore=NativeExpedition.RestorePreparedHandoff(Copy(flatHandoff),"next");Check(!flatRestore.SplitAreas&&flatRestore.Migrating&&flatRestore.CommitHandoff(flatHandoff.id,"next",flatHandoff.nextEpoch,flatHandoff.revision),"legacy control-state round trip");
            Console.WriteLine("PASS Expedition handoff: "+n+" checks");
        }
    }
}
#endif
