#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ChipArena.UnityConversion
{
    internal static class ExpeditionChecks
    {
        private static int assertions;
        private static void Check(bool value,string reason){if(!value)throw new Exception("Expedition: "+reason);assertions++;}
        private static NativeExpedition NewRun(int count=2)
        {
            var run=NativeExpedition.Create("test-run","city","p0","sword",73,new[]{new ExpeditionInstance{id="ward-zero",target=3},new ExpeditionInstance{id="station",target=2}});
            for(int i=1;i<count;i++)Check(run.Join("p"+i,"sword"),"join supported party size");return run;
        }
        private static void Advance(NativeExpedition run,int seconds){for(int i=0;i<seconds*4;i++)run.Tick(.25);}
        private static void Clear(NativeExpedition run,string area="ward-zero",int drops=-1)
        {
            Check(run.Enter(area),"enter building");Check(!run.VoteExit("p0",true),"door remains locked");
            Check(!run.StartBosses(new[]{"scientist"}),"quota gates boss");
            Check(run.Progress("kill",3)&&!run.Progress("kill",3),"kill event counted once");
            Check(run.StartBosses(new[]{"scientist"}),"boss activates at quota");
            Check(run.DefeatBoss("scientist",drops)&&!run.DefeatBoss("scientist",drops),"boss death counted once");
        }
        private static void Exit(NativeExpedition run)
        {
            Check(run.VoteExit("p0",true),"one vote sufficient");Advance(run,10);
            Check(run.Snapshot().phase==ExpeditionPhase.Town,"instance exits at timer");
            foreach(var p in run.Snapshot().members.Where(p=>p.connected&&p.alive&&!p.extracted))Check(run.SetSafe(p.account,true),"arrive at sanctuary");
        }
        private static string Json(object value)=>JsonSerializer.Serialize(value,new JsonSerializerOptions{IncludeFields=true});
        private static ExpeditionSavepoint Decode(string json){try{return JsonSerializer.Deserialize<ExpeditionSavepoint>(json,new JsonSerializerOptions{IncludeFields=true});}catch(JsonException){return null;}}
        public static void Run()
        {
            assertions=0;
            for(int n=1;n<=8;n++)
            {
                var run=NewRun(n);if(n==8)Check(!run.Join("overflow","sword"),"eight-player ceiling");
                Check(!run.CanExtract("p0")&&!run.BeginFinal(),"initial progression gates");
                var detached=run.Snapshot();detached.members[0].wallet=777;Check(run.Snapshot().members[0].wallet==0,"snapshot ownership isolated");
                Clear(run,drops:n+1);var state=run.Snapshot();var dice=state.dice.Single();
                Check(dice.eligible.Length==n&&state.rewards.Length==n+1,"boss reward conservation");
                Check(state.rewards.All(r=>r.owner!=null&&r.highQuality)&&dice.winners.Length==(n+1)%n,"reserved high-quality rewards and remainder");
                var amounts=state.rewards.GroupBy(r=>r.owner).Select(g=>g.Count()).ToArray();Check(amounts.Max()-amounts.Min()<=1,"even split");
                var replica=NewRun(n);Clear(replica,drops:n+1);Check(Json(replica.Snapshot().dice)==Json(state.dice),"seeded dice reproducible");
                Check(run.VoteExit("p0",true)&&run.VoteExit("p0",false),"departure cancellation");Advance(run,11);Check(run.Snapshot().phase==ExpeditionPhase.Instance,"zero votes cancel departure");
                Exit(run);Check(!run.FinalUnlocked,"remaining instance gates world boss");
                Clear(run,"station");Exit(run);Check(run.FinalUnlocked&&run.BeginFinal()&&run.FinishFinal(),"world boss progression");
                double elapsed=run.Snapshot().elapsed;Check(run.NextLoop()&&run.Snapshot().loop==1&&!run.FinalUnlocked&&run.Snapshot().elapsed==elapsed,"loop preserves difficulty time and resets objectives");
                Check(ExpeditionValidation.Valid(run.Snapshot()),"complete loop remains serializable");
            }
            var party=NewRun(4);Check(party.SetPerks("p0",new[]{"medic"},p=>p=="medic"),"three-slot unlocked perk contract");
            Check(!party.SetPerks("p0",new[]{"medic","medic"},p=>true)&&!party.SetPerks("p0",new[]{"locked"},p=>false),"duplicate and locked perks rejected");
            Check(party.Credit("p0","starter",200)&&!party.Credit("p0","starter",200),"personal credit idempotence");
            Check(party.Purchase("p0","chest-0",100)&&!party.Purchase("p1","chest-0",100),"chest generation purchased once");
            var chest=party.Snapshot().rewards.Single();Check(party.Snapshot().members[0].wallet==100&&party.Snapshot().members[1].wallet==0,"wallet isolation");
            Check(!party.Claim("p1",chest.id,new[]{"health"})&&party.Claim("p0",chest.id,new[]{"health"}),"buyer owns selection");
            Check(!party.Select("p0",chest.id,"health",c=>false)&&party.Select("p0",chest.id,"health",c=>true)&&!party.Select("p0",chest.id,"health",c=>true),"eligible selection exactly once");
            Check(party.Drop("zombie-0"),"monster reward spawned");var drop=party.Snapshot().rewards.Last();
            Check(party.Claim("p1",drop.id,new[]{"speed"})&&!party.Claim("p2",drop.id,new[]{"speed"}),"first pickup wins");
            Check(party.Join("newcomer","bow"),"late join");
            Check(party.Defeat("p2")&&party.Absorb("p1","p2"),"wrist rescue");
            Check(!party.Reprint("p1","p2",20)&&party.Credit("p1","rescue",20)&&party.Reprint("p1","p2",20),"paid carrier revive");
            Check(party.Defeat("p2")&&party.Reprint("p0","p2",20,true),"unabsorbed safe-machine recovery");
            Check(party.Defeat("p3"),"dead player before boss");Clear(party);
            Check(!party.Snapshot().dice.Single().eligible.Contains("p3"),"dead at death receives no boss chip");
            Check(party.Reprint("p0","p3",20,true)==false,"remote recovery unavailable inside instance");
            Check(party.Absorb("p0","p3")&&party.Reprint("p0","p3",20),"revive after boss");
            Check(!party.Snapshot().rewards.Any(r=>r.source=="boss"&&r.owner=="p3"),"late revival does not change boss allocation");
            Exit(party);
            Check(party.VoteExtraction("p0",true)&&party.VoteExtraction("p1",true)&&party.Snapshot().extractAt<0,"minority cannot start group extraction");
            Check(party.VoteExtraction("p2",true)&&party.Snapshot().extractAt>=0,"strict majority starts group countdown");
            Check(party.VoteExtraction("p2",false)&&party.Snapshot().extractAt<0,"loss of majority cancels countdown");
            Check(party.VoteExtraction("p2",true),"restart majority");Advance(party,10);Check(party.ExtractionDue().Length==3,"only consenting players are due");

            var archive=ExpeditionSavepoint.Capture(party);Check(party.Extract("p1")&&!party.Join("p1","sword"),"successful extraction cannot rejoin");
            archive=archive.WithExtractions(party.Snapshot());var restored=archive.Restore("p0");
            Check(!restored.Join("p1","sword")&&restored.Join("p2","sword"),"rollback preserves extraction tombstone and allows reconnect");
            Check(restored.Snapshot().members.Single(p=>p.account=="p0").wallet==archive.checkpoint.members.Single(p=>p.account=="p0").wallet,"restore checkpoint wallet");
            Check(!party.Extract("p0"),"host cannot leave peers without handoff");
            var candidates=new[]{new ExpeditionHostCandidate{account="p2",capable=true,reachable=true,loss=0,latencyMs=50},new ExpeditionHostCandidate{account="p3",capable=true,reachable=true,loss=.1,latencyMs=1}};
            var handoff=party.PrepareHandoff("transfer-1",new[]{"p0"},candidates);Check(handoff!=null&&handoff.nextHost=="p2","eligible connection quality election");
            double before=party.Snapshot().elapsed;party.Tick(.25);Check(party.Snapshot().elapsed==before&&!party.Credit("p0","frozen",1),"handoff freezes authority");
            handoff.state.members[0].wallet=999;Check(party.Snapshot().members[0].wallet!=999,"handoff snapshot detached");
            Check(!party.CommitHandoff(handoff.id,handoff.nextHost,handoff.nextEpoch+1,handoff.revision),"stale acknowledgement rejected");
            Check(party.AbortHandoff("transfer-1")&&!party.Migrating&&!party.Snapshot().members[0].extracted,"abort keeps host in run");
            handoff=party.PrepareHandoff("transfer-2",new[]{"p0"},candidates);
            Check(party.CommitHandoff(handoff.id,handoff.nextHost,handoff.nextEpoch,handoff.revision)&&party.Snapshot().host=="p2"&&!party.Join("p0","sword"),"committed host departure preserves continuing run");
            Check(!party.CommitHandoff(handoff.id,handoff.nextHost,handoff.nextEpoch,handoff.revision),"commit idempotence");
            archive=archive.WithExtractions(party.Snapshot());Check(archive.Restore("p2").Snapshot().host=="p2","new host resumes last savepoint");

            string directory=Path.Combine(Path.GetTempPath(),"expedition-check-"+Guid.NewGuid().ToString("N"));
            try
            {
                var store=new NativeExpeditionSaveStore(directory,"test-run",s=>Json(s),Decode);store.Write(archive);
                Check(Json(store.Read())==Json(archive),"disk and codec round trip");store.Write(archive);Check(File.Exists(Path.Combine(directory,"test-run.expedition.bak")),"atomic replacement backup");
                File.WriteAllText(Path.Combine(directory,"test-run.expedition"),"corrupt");Check(store.Read()==null,"corruption fails closed instead of reverting extraction journal");
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
            var invalid=party.Snapshot();invalid.members[0].wallet=-1;Check(!ExpeditionValidation.Valid(invalid),"malformed wallet rejected");
            invalid=party.Snapshot();invalid.elapsed=double.NaN;Check(!ExpeditionValidation.Valid(invalid),"nonfinite clock rejected");
            invalid=party.Snapshot();invalid.members[0].chips=null;Check(!ExpeditionValidation.Valid(invalid),"missing fields rejected");
            var catchup=NewRun(1);var built=catchup.Snapshot();built.members[0].chips=new[]{new ChipCount{id="health",count=5}};catchup=new NativeExpedition(built);
            Check(catchup.Join("p1","bow")&&catchup.Snapshot().members[1].catchup==5,"newcomer receives lower-median catchup");
            Check(catchup.IssueCatchup("p1")&&catchup.Disconnect("p1")&&catchup.Join("p1","bow")&&catchup.Snapshot().members[1].catchup==4,"reconnect does not issue catchup again");
            Check(catchup.SetSafe("p1",true),"reconnected pilot returns to sanctuary");
            var catchupSave=ExpeditionSavepoint.Capture(catchup);var resumed=catchupSave.Restore("p0");Check(resumed.Join("p1","bow")&&resumed.Snapshot().members[1].catchup==4,"catchup entitlement persists across checkpoint");
            Check(catchup.Defeat("p1")&&catchup.Disconnect("p1")&&catchup.Join("p1","bow")&&!catchup.Snapshot().members[1].alive,"reconnect cannot bypass death");
            var multi=NewRun(2);Check(multi.Enter("ward-zero")&&multi.Progress("quota",3)&&multi.StartBosses(new[]{"a","b"}),"multi-boss setup");
            Check(multi.DefeatBoss("a")&&!multi.Snapshot().instances[0].cleared&&!multi.VoteExit("p0",true),"first of multiple bosses cannot unlock exit");
            Check(multi.DefeatBoss("b")&&multi.VoteExit("p0",true)&&multi.VoteExit("p1",true),"all bosses and unanimous exit");multi.Tick(.01);Check(multi.Snapshot().phase==ExpeditionPhase.Town,"unanimity skips countdown");
            var scaling=new ExpeditionScaling();var easy=scaling.Evaluate(0,1,0,false);var hard=scaling.Evaluate(3600,8,1,true);
            Check(hard.health>easy.health&&hard.damage>easy.damage&&hard.count>easy.count,"time, party, instance and loop scaling");
            var cap=scaling.Evaluate(1e10,8,100000,true);Check(cap.health<=scaling.maxHealth&&cap.damage<=scaling.maxDamage&&cap.count<=scaling.maxCount,"scaling caps bounded");
            var paused=NewRun();paused.SetLoading(true);Advance(paused,1);Check(paused.Snapshot().elapsed==0,"loading pauses clock");paused.SetLoading(false);Advance(paused,1);Check(paused.Snapshot().elapsed==1,"safe-zone idle still scales difficulty");
            Console.WriteLine("PASS Expedition ("+assertions+" assertions)");
        }
    }
}
#endif
