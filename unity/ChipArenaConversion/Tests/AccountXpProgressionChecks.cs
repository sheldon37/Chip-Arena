#if ARENA_TESTS
using System;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    internal static class AccountXpProgressionChecks
    {
        private static int count;
        private static void Check(bool b,string why){if(!b)throw new Exception("Account XP progression: "+why);count++;}
        private static NativeExpedition Run()=>NativeExpedition.Create("xp-run","city","one","pistol",12,new[]{new ExpeditionInstance{id="ward",target=1}});
        private static void Advance(NativeExpedition r,int seconds){for(int i=0;i<seconds*4;i++)r.Tick(.25);}
        private static void Clear(NativeExpedition r){Check(r.Enter("ward"),"enter");Check(r.Progress("kill",1),"quota");Check(r.StartBosses(new[]{"boss"}),"boss");Check(r.DefeatBoss("boss",0),"clear");Check(r.VoteExit("one",true),"exit");Advance(r,10);Check(r.SetSafe("one",true),"safe");}
        public static void RunChecks(string root)
        {
            count=0;
            var data=JsonSerializer.Deserialize<ConversionContent>(System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new JsonSerializerOptions{IncludeFields=true});
            var match=new NativeMatch(data,1){RoundsPerSet=1,SetsToWin=1};match.Seats.Add(new NativeSeat{id=1,name="One"});match.Seats.Add(new NativeSeat{id=2,name="Two"});Check(match.Start(),"match starts");string first=match.CompletionId;
            for(int i=0;i<300;i++)match.Tick(.25);match.Seat(2).alive=false;match.Resolve();for(int i=0;i<50;i++)match.Tick(.25);
            Check(match.Phase==NativePhase.Match&&match.CompletionSeconds>=75,"terminal duration");int seconds=match.CompletionSeconds;for(int i=0;i<300;i++)match.Tick(.25);Check(match.CompletionSeconds==seconds,"results waiting never increases eligibility");
            var guest=new NativeMatch(data,2);guest.ApplyNetwork(new NativeMatchFrame{completionId=first,completionSeconds=seconds,completionStarted=match.CompletionStarted,phase=NativePhase.Match,time=match.Time});Check(guest.CompletionId==first&&guest.CompletionSeconds==seconds,"guest uses host completion");Check(match.Start()&&match.CompletionId!=first&&match.CompletionSeconds==0,"rematch fresh identity");
            for(int level=1;level<100;level++){long next=NativeAccountXpProgression.Start(level+1);Check(NativeAccountXpProgression.Level(next-1)==level,"before threshold");Check(NativeAccountXpProgression.Level(next)==level+1,"exact threshold");}
            var c=new AccountXpCompletion{completionId="pvp_test",mode="pvp",won=true,seconds=120};var a=NativeAccountXpProgression.Pending("one",c,NativeAccountXpProgression.Summary("one",200));
            Check(a.status=="pending"&&a.previousTotal==200&&a.newTotal==350&&a.newLevel==2,"estimated band crossing");c.bots=true;Check(NativeAccountXpProgression.Pending("one",c,NativeAccountXpProgression.Summary("one")).newTotal==75,"bots half");Check(!NativeAccountXpContract.Check(NativeAccountXpProgression.Pending("one",c,null),"one").Usable,"unknown saved balance is not invented");c.seconds=59;Check(!NativeAccountXpProgression.Valid(c),"short refused");c.seconds=120;
            var capped=NativeAccountXpProgression.Pending("one",c,NativeAccountXpProgression.Summary("one",NativeAccountXpProgression.Cap-10));Check(capped.lines.Sum(l=>l.xp)==10&&capped.newLevel==100,"cap conserves breakdown");
            var r=Run();Advance(r,70);Clear(r);Check(r.BeginFinal()&&r.FinishFinal(0),"victory");var receipt=r.Snapshot().members[0].xpCompletion;Check(receipt.won&&receipt.cleared==1&&receipt.seconds==80,"frozen authority completion");
            Advance(r,3);Check(r.SetSafe("one",true)&&r.Extract("one"),"extract after victory");Check(r.Snapshot().members[0].xpCompletion.won&&r.Snapshot().members[0].xpCompletion.seconds==80,"extract does not replace victory");
            var snapshot=r.Snapshot();snapshot.members[0].xpCompletion.seconds=999;Check(r.Snapshot().members[0].xpCompletion.seconds==80,"snapshot isolated");
            var json=JsonSerializer.Serialize(r.Snapshot(),new JsonSerializerOptions{IncludeFields=true});var restored=new NativeExpedition(JsonSerializer.Deserialize<ExpeditionSnapshot>(json,new JsonSerializerOptions{IncludeFields=true}));Check(restored.Snapshot().members[0].xpCompletion.completionId==receipt.completionId,"stable on reconnect/handoff");
            var loop=Run();Advance(loop,70);Clear(loop);Check(loop.BeginFinal()&&loop.FinishFinal(0),"loop victory");Check(loop.SetSafe("one",true)&&loop.NextLoop(),"continue");Clear(loop);Check(loop.BeginFinal()&&loop.FinishFinal(0),"next victory");Check(loop.Snapshot().members[0].xpCompletion.completionId!=receipt.completionId,"new loop new ID");
            var tainted=Run();Advance(tainted,70);tainted.DisableAccountXp();Clear(tainted);Check(tainted.Extract("one"),"DEV extraction");Check(tainted.Snapshot().members[0].xpCompletion==null,"DEV excluded across areas");
            var wipe=Run();Check(wipe.Join("two","pistol"),"party");Advance(wipe,70);Check(wipe.Defeat("one"),"one down");Check(wipe.Snapshot().members[0].xpCompletion==null,"down is not completion");Check(wipe.Defeat("two"),"wipe");Check(wipe.Snapshot().members.All(m=>m.xpCompletion!=null&&!m.xpCompletion.won),"whole party receives wipe completion");
            Console.WriteLine("PASS account XP progression ("+count+" checks)");
        }
    }
}
#endif
