#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChipArena.UnityConversion
{
    // The Expedition chip pick on the Boss Test's chip tray over the live view (Core/NativeExpeditionPick.cs,
    // Runtime/NativeExpeditionChipScreen.cs; docs/EXPEDITION-PRESENTATION.md "Chip picks"), against the real NativeExpedition authority with
    // its choosing presence (PR #233 / #234), the view builder, the host command path and the intent tracker: collecting a reward never
    // opens the tray or choosing; C opens it for a claimed reward with offers (never a consumed one) and sends OpenChoice once, Choose later
    // sends CloseChoice; a pick sends Select with the shown reward and chip, is confirmed only by the view and chains to the next reward as
    // the Boss Test's queue does; refusals return to the tray with their caption; an unopened reserved reward is opened with a Claim only
    // when the tray can show; "N more waiting" and CHIPS count the viewer's own; other pilots' choosing comes from the public flag only; and
    // source scans keep the Boss Test path untouched, the overlay off the 3D stage, and the input routing free of any movement lock.
    internal static class ExpeditionPickChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Expedition pick: "+what);count++;}
        private static readonly string[] Offers={"vital","ricochet","piercing"};
        private static int SeatOf(string account)=>account=="p0"?1:account=="p1"?2:account=="p2"?3:0;

        // One host-local session: the real authority with stand-in offer / eligibility hooks, the tracker and the flow's inputs.
        private sealed class Rig
        {
            public NativeExpedition Run;public ExpeditionIntegrationHooks Hooks;public ExpeditionHostCommands Host;public readonly ExpeditionIntentTracker Tracker=new ExpeditionIntentTracker();
            public readonly ExpeditionPickFlow Flow=new ExpeditionPickFlow();public double Now;public string Viewer="p0";
            public Rig(int members=2,Func<string,string,string[]> offers=null,Func<string,string,bool> eligible=null)
            {
                Run=NativeExpedition.Create("pick-run","city","p0","sword",17,new[]{new ExpeditionInstance{id="ward-zero",target=2}});
                for(int i=1;i<members;i++)Check(Run.Join("p"+i,"sword"),"join");
                Hooks=new ExpeditionIntegrationHooks{Offers=offers??((a,r)=>Offers),Eligible=eligible??((a,c)=>true)};Host=new ExpeditionHostCommands(Run,"p0",Hooks);
            }
            public ExpeditionView View()=>NativeExpeditionView.Build(Run,Viewer,new ExpeditionViewSources{Seat=SeatOf});
            public ExpeditionPickAction Step(bool canShow=true)=>Flow.Step(View(),c=>Tracker.State(c,View(),Now),c=>Tracker.Last(Viewer,c),canShow);
            public ExpeditionCommandResult Send(ExpeditionCommand c)=>Tracker.Submit(Host,c,View(),Now);
            // One frame as ConversionGame.ExpeditionPickStep runs it: the step, then its intent sent at once.
            public ExpeditionPickAction Pump(bool canShow=true){var a=Step(canShow);if(a.Send.intent!=ExpeditionIntent.None)Send(a.Send);return a;}
            public bool Choosing=>ExpeditionPickFlow.ChoosingNow(View());
            public string Chest(string owner,string generation,int price=100){Check(Run.Credit(owner,"seed-"+generation,price)&&Run.Purchase(owner,generation,price),"purchase "+generation);return Run.Snapshot().rewards.Last(r=>r.owner==owner&&r.source=="chest").id;}
            // The scene's own claim (NativeExpeditionM1Arena: F at a cache, a Ward machine or a drop claims it directly).
            public void Claim(string owner,string reward,string[] offers=null)=>Check(Run.Claim(owner,reward,offers??Offers),"claim "+reward);
        }

        public static void Run(string root)
        {
            count=0;
            Model();Opens();Picks();Refusals();Opening();Waiting();Choosers();Sources(root);
            Console.WriteLine("PASS Expedition pick tray ("+count+" checks: never opened by collecting, C opens a claimed reward with offers and never a consumed one, OpenChoice once and CloseChoice on close, Select with the shown reward and chip, view-confirmed and chained like the Boss Test's queue, refusals back on the tray, Claim-to-open only when shown, the viewer's own waiting counts, choosers from the public flag only, the Boss Test path untouched, an overlay off the 3D stage and digit routing without a movement lock)");
        }

        // 1. The reward rules and the header.
        private static void Model()
        {
            var ready=new ExpeditionViewReward{id="0:city:chest:a",source="chest",claimed=true,offers=Offers};
            Check(NativeExpeditionPick.Ready(ready)&&!NativeExpeditionPick.Unopened(ready),"claimed with offers is ready");
            Check(!NativeExpeditionPick.Ready(new ExpeditionViewReward{id="x",claimed=true,consumed=true,offers=Offers}),"a consumed reward is never ready");
            Check(!NativeExpeditionPick.Ready(new ExpeditionViewReward{id="x",claimed=true}),"claimed without offers is not ready");
            Check(NativeExpeditionPick.Unopened(new ExpeditionViewReward{id="x"})&&!NativeExpeditionPick.Unopened(new ExpeditionViewReward{id="x",consumed=true}),"unclaimed and unconsumed is unopened");
            Check(NativeExpeditionPick.Eyebrow(ready)=="CACHE · PURCHASED"&&NativeExpeditionPick.Tone(ready)=="common","a cache purchase: its source, the neutral tone");
            Check(NativeExpeditionPick.Eyebrow(new ExpeditionViewReward{id="0:expedition-ward-zero:chest:ward-machine:3:9",source="chest"})=="DISPENSED · WARD MACHINE","a Ward machine purchase reads as dispensed");
            var boss=new ExpeditionViewReward{id="0:ward-zero:boss:scientist:0",source="boss",boss="0:ward-zero:boss:scientist",highQuality=true};
            Check(NativeExpeditionPick.Eyebrow(boss)=="BOSS REWARD · SCIENTIST · EPIC OR BETTER"&&NativeExpeditionPick.Tone(boss)=="epic","a boss reward names its boss, epic or better, the epic tone");
            Check(NativeExpeditionPick.Eyebrow(new ExpeditionViewReward{id="d",source="monster"})=="DROP · PICKED UP"&&NativeExpeditionPick.Eyebrow(new ExpeditionViewReward{id="c",source="catchup"})=="CATCH-UP CHIP","drops and catch-up");
            foreach(var e in new[]{NativeExpeditionPick.Eyebrow(ready),NativeExpeditionPick.Eyebrow(boss)})foreach(var tier in new[]{"COMMON","UNCOMMON","RARE","LEGENDARY"})Check(!e.Contains(tier),"no tier is invented ("+e+")");
            Check(NativeExpeditionPick.Id("0:city:chest:a")==NativeExpeditionPick.Id("0:city:chest:a")&&NativeExpeditionPick.Id("0:city:chest:a")!=NativeExpeditionPick.Id("0:city:chest:b")&&NativeExpeditionPick.Id(null)>0&&NativeExpeditionPick.Id("")>0,"stable, distinct, non-zero pick ids");
            Check(new ExpeditionPickFlow().Phase(null)==ExpeditionPickPhase.Closed&&new ExpeditionPickFlow().Choosing(null)==null&&!ExpeditionPickFlow.ChoosingNow(null),"no view: closed");
        }

        // 2. When it opens: only on C; OpenChoice once when it shows, CloseChoice once when it closes; never for a consumed reward.
        private static void Opens()
        {
            var g=new Rig();string chest=g.Chest("p0","cache-a");g.Pump();Check(!g.Flow.Open&&g.Flow.Phase(g.View())==ExpeditionPickPhase.Closed,"an unopened purchase does not open the tray");
            g.Claim("p0",chest);var collected=g.Pump();
            Check(!g.Flow.Open&&collected.Send.intent==ExpeditionIntent.None&&!g.Choosing,"collecting a reward never opens the tray or choosing (the contract)");
            Check(NativeExpeditionPick.Waiting(g.View())==1,"it waits on the CHIPS tag, as the Boss Test queues a dispensed chip");
            Check(g.Flow.Show(g.View()),"C opens the tray");var v=g.View();Check(g.Flow.Phase(v)==ExpeditionPickPhase.Choosing&&g.Flow.Choosing(v)?.id==chest&&g.Flow.Choosing(v).offers.SequenceEqual(Offers),"on the claimed reward with the view's offers");
            Check(g.Step(false).Send.intent==ExpeditionIntent.None&&!g.Choosing,"no OpenChoice while it cannot show (menu, J, intro, hold)");
            var open=g.Pump();Check(open.Send.intent==ExpeditionIntent.OpenChoice&&open.Send.target==chest,"OpenChoice(shown reward) when it shows");
            Check(g.Choosing&&g.View().members.First(m=>m.account=="p0").choosing,"the host publishes me choosing");
            Check(g.Pump().Send.intent==ExpeditionIntent.None,"OpenChoice is sent once");
            Check(new ExpeditionPickFlow().Step(g.View(),c=>default,c=>ExpeditionIntent.None,true).Send.intent==ExpeditionIntent.CloseChoice,"a tray that is not open (a restarted UI) closes a stale choosing");
            var lv=g.View();lv.loading=true;foreach(var m in lv.members)m.choosing=false;
            Check(g.Flow.Step(lv,c=>default,c=>ExpeditionIntent.None,true).Send.intent==ExpeditionIntent.None&&g.Flow.Open,"a load clears choosing; nothing is reopened during it");
            g.Flow.Later();var close=g.Pump();Check(close.Send.intent==ExpeditionIntent.CloseChoice&&!g.Choosing&&!g.Flow.Open,"Choose later closes it and sends CloseChoice");
            Check(g.Pump().Send.intent==ExpeditionIntent.None&&!g.Flow.Open,"CloseChoice once, and the tray does not pop again by itself");
            g.Flow.Toggle(g.View());var again=g.Pump();Check(again.Send.intent==ExpeditionIntent.OpenChoice&&g.Choosing&&g.Flow.Choosing(g.View())?.id==chest,"C reopens it on the same reward with a new OpenChoice");
            g.Flow.Toggle(g.View());Check(!g.Flow.Open&&g.Pump().Send.intent==ExpeditionIntent.CloseChoice,"C again closes it (CloseChoice)");
            // Consumed elsewhere: the tray never shows it, and the host's clear needs no close.
            g.Flow.Toggle(g.View());g.Pump();Check(g.Run.Select("p0",chest,"ricochet",c=>true),"picked directly");var after=g.Pump();
            Check(!g.Flow.Open&&g.Flow.Choosing(g.View())==null&&!g.Choosing&&after.Send.intent==ExpeditionIntent.None,"a consumed reward never shows; the host's own clear needs no CloseChoice");
            Check(!g.Flow.Show(g.View()),"nothing waiting: C does nothing");
            // Another pilot's claim never opens mine (their rewards and offers are not in my view).
            var o=new Rig();string theirs=o.Chest("p1","cache-c");o.Pump();o.Claim("p1",theirs);o.Pump();
            Check(!o.Flow.Open&&NativeExpeditionPick.Waiting(o.View())==0&&o.View().mine.rewards.Length==0,"another pilot's reward never opens or counts on my tray");
            // A downed pilot: the host clears choosing and the tray closes (the Boss Test closes the open pick on a death).
            var d=new Rig();string mine=d.Chest("p0","cache-d");d.Claim("p0",mine);d.Flow.Show(d.View());d.Pump();Check(d.Flow.Open&&d.Choosing,"open and choosing before the death");
            Check(d.Run.Defeat("p0"),"downed");var dead=d.Pump();Check(!d.Flow.Open&&!d.Choosing&&dead.Send.intent==ExpeditionIntent.None&&!d.Flow.Show(d.View()),"a downed pilot's tray closes and C cannot open it");
            // Extracted (or not a member): nothing opens, nothing waits.
            var x=new Rig();string left=x.Chest("p0","cache-e");x.Claim("p0",left);var ev=x.View();ev.mine.extracted=true;
            Check(!x.Flow.Show(ev)&&x.Flow.Step(ev,c=>default,c=>ExpeditionIntent.None,true).Send.intent==ExpeditionIntent.None&&NativeExpeditionPick.Waiting(ev)==0,"an extracted pilot's rewards never open");
            x.Viewer="stranger";x.Pump();Check(!x.Flow.Open&&NativeExpeditionPick.Waiting(x.View())==0&&!x.Flow.Show(x.View()),"a non-member sees nothing");
        }

        // 3. A pick sends Select with the shown reward id and the chip; the view confirms it; the next reward follows.
        private static void Picks()
        {
            var g=new Rig();string a=g.Chest("p0","cache-a"),b=g.Chest("p0","cache-b");g.Claim("p0",a);g.Claim("p0",b);g.Flow.Show(g.View());g.Pump();
            var v=g.View();Check(g.Flow.Choosing(v)?.id==a&&g.Choosing,"the first claimed reward shows first, choosing");
            Check(g.Flow.Choose(v,"glass").intent==ExpeditionIntent.None,"an offer not on the tray is never sent");
            var cmd=g.Flow.Choose(v,"ricochet");Check(cmd.intent==ExpeditionIntent.Select&&cmd.target==a&&cmd.chip=="ricochet","the pick is Select(shown reward, chip)");
            Check(g.Flow.Phase(v)==ExpeditionPickPhase.Installing&&g.Flow.Sent==a&&g.Flow.Choosing(v)==null&&g.Flow.Choose(v,"vital").intent==ExpeditionIntent.None,"one pick in flight: the tray is down");
            Check(g.Send(cmd).state==ExpeditionCommandState.Pending,"sent through the host command path");
            var s=g.Run.Snapshot().rewards.First(r=>r.id==a);Check(s.consumed&&s.selected=="ricochet"&&g.Run.Snapshot().members.First(m=>m.account=="p0").chips.Any(c=>c.id=="ricochet"&&c.count==1),"the authority installed exactly that chip on that reward");
            Check(g.Flow.Installed!=a,"not confirmed before the view shows it");
            var chained=g.Pump();v=g.View();Check(g.Flow.Installed==a&&g.Flow.Sent==null&&g.Tracker.State(cmd,v,g.Now).state==ExpeditionCommandState.Confirmed,"confirmed once the view shows the reward consumed");
            Check(g.Flow.Open&&g.Flow.Choosing(v)?.id==b&&g.Choosing&&chained.Send.intent==ExpeditionIntent.None,"the next waiting reward follows (the host keeps choosing across queued picks: no new OpenChoice)");
            var second=g.Flow.Choose(v,"vital");Check(second.target==b&&second.chip=="vital","the second pick names its own reward");g.Send(second);var last=g.Pump();
            Check(g.Flow.Installed==b&&!g.Flow.Open&&!g.Choosing&&last.Send.intent==ExpeditionIntent.None,"nothing left: it closes; the host cleared choosing with the final pick");
            // Choose later during the follow-through: the pick still settles, CloseChoice is sent, nothing chains.
            var h=new Rig();string c1=h.Chest("p0","cache-c"),c2=h.Chest("p0","cache-d");h.Claim("p0",c1);h.Claim("p0",c2);h.Flow.Show(h.View());h.Pump();
            var p=h.Flow.Choose(h.View(),"piercing");h.Flow.Later();h.Send(p);var closed=h.Pump();
            Check(h.Flow.Installed==c1&&!h.Flow.Open&&h.Flow.Choosing(h.View())==null&&closed.Send.intent==ExpeditionIntent.CloseChoice,"Choose later in the follow-through: the pick settles, CloseChoice, the next waits for C");
            Check(!h.Choosing,"no longer choosing");
        }

        // 4. Refused, unavailable, unsent or unconfirmed picks and opens.
        private static void Refusals()
        {
            var g=new Rig(eligible:(a,c)=>false);string a=g.Chest("p0","cache-a");g.Claim("p0",a);g.Flow.Show(g.View());g.Pump();
            var cmd=g.Flow.Choose(g.View(),"vital");Check(g.Send(cmd).state==ExpeditionCommandState.Refused,"the authority refuses an ineligible chip");
            g.Pump();var v=g.View();Check(g.Flow.Choosing(v)?.id==a&&g.Flow.Sent==null&&g.Flow.Refusal!=null&&g.Flow.Refusal.StartsWith("Refused · "),"a refusal returns to the same tray with its caption ("+g.Flow.Refusal+")");
            Check(g.Flow.Installed==null&&!g.Run.Snapshot().rewards.First(r=>r.id==a).consumed&&g.Choosing,"nothing reads as installed; still choosing");
            g.Flow.Later();Check(g.Flow.Refusal==null,"Choose later clears the caption");
            // A pick the caller never sent (no command path): failed, back on the tray.
            var h=new Rig();string b=h.Chest("p0","cache-b");h.Claim("p0",b);h.Flow.Show(h.View());h.Pump();h.Flow.Choose(h.View(),"vital");h.Pump();
            Check(h.Flow.Choosing(h.View())?.id==b&&h.Flow.Refusal=="Failed · "+NativeExpeditionPick.NotSent,"an unsent pick is shown as failed");
            // A pick sent but never reflected: the tracker's timeout fails it.
            var t=new Rig();string c=t.Chest("p0","cache-c");t.Claim("p0",c);t.Flow.Show(t.View());t.Pump();var pick=t.Flow.Choose(t.View(),"vital");
            t.Tracker.Record("p0",pick,ExpeditionCommandResult.Sent,t.View(),0);t.Now=1;t.Step();Check(t.Flow.Phase(t.View())==ExpeditionPickPhase.Installing,"pending while within the timeout");
            t.Now=ExpeditionIntentTracker.Timeout+1;t.Step();Check(t.Flow.Choosing(t.View())?.id==c&&t.Flow.Refusal=="Failed · "+ExpeditionIntentTracker.NoConfirmation,"no confirmation in time: failed, back on the tray");
            // Unavailable (no eligibility adapter): the host path says why.
            var u=new Rig();u.Hooks.Eligible=null;string d=u.Chest("p0","cache-d");u.Claim("p0",d);u.Flow.Show(u.View());u.Pump();
            Check(u.Host.Blocked(new ExpeditionCommand(ExpeditionIntent.Select,d))==ExpeditionIntegrationHooks.NoEligibility,"the tray's blocked caption comes from the command path");
            u.Send(u.Flow.Choose(u.View(),"vital"));u.Pump();Check(u.Flow.Refusal=="Unavailable · "+ExpeditionIntegrationHooks.NoEligibility,"an unavailable pick shows its reason");
            // A refused OpenChoice closes the tray with a notice instead of looping.
            var r=new Rig();string e=r.Chest("p0","cache-e");r.Claim("p0",e);r.Flow.Show(r.View());var oc=r.Step();Check(oc.Send.intent==ExpeditionIntent.OpenChoice,"OpenChoice due");
            r.Tracker.Record("p0",oc.Send,ExpeditionCommandResult.Of(ExpeditionCommandState.Refused,"Not now."),r.View(),0);var refused=r.Step();
            Check(refused.Notice!=null&&refused.Notice.StartsWith("Chip choice could not open · Refused")&&!r.Flow.Open&&r.Step().Send.intent==ExpeditionIntent.None,"a refused OpenChoice: a notice, closed, not resent ("+refused.Notice+")");
            // An OpenChoice never reflected (dropped) fails after the timeout.
            var f=new Rig();string k=f.Chest("p0","cache-f");f.Claim("p0",k);f.Flow.Show(f.View());var drop=f.Step();f.Tracker.Record("p0",drop.Send,ExpeditionCommandResult.Sent,f.View(),0);
            f.Now=1;Check(f.Step().Notice==null&&f.Flow.Open,"pending OpenChoice: the tray stays");f.Now=ExpeditionIntentTracker.Timeout+1;Check(f.Step().Notice!=null&&!f.Flow.Open,"an unreflected OpenChoice fails with a notice");
        }

        // 5. Unopened rewards (reserved boss chips, catch-up) are opened by the tray's Claim, only when it can show; then OpenChoice.
        private static void Opening()
        {
            var g=new Rig();Check(g.Run.Enter("ward-zero")&&g.Run.Progress("q",2)&&g.Run.StartBosses(new[]{"scientist"})&&g.Run.DefeatBoss("scientist",2),"boss down");
            g.Pump();var boss=g.View().mine.rewards.First(r=>r.source=="boss");Check(NativeExpeditionPick.Unopened(boss)&&!g.Flow.Open,"a reserved boss chip waits unopened, the tray closed");
            Check(g.Flow.Show(g.View()),"C opens the tray on it");
            Check(g.Step(false).Send.intent==ExpeditionIntent.None&&g.Flow.Claiming==null,"no Claim while the tray cannot show (menu, hold, downed)");
            var act=g.Step();Check(act.Send.intent==ExpeditionIntent.Claim&&act.Send.target==boss.id&&g.Flow.Phase(g.View())==ExpeditionPickPhase.Opening,"the tray opens it with a Claim of that reward");
            Check(g.Send(act.Send).state==ExpeditionCommandState.Pending,"sent through the host command path");
            var again=g.Pump();var v=g.View();var shown=g.Flow.Choosing(v);
            Check(again.Send.intent==ExpeditionIntent.OpenChoice&&again.Send.target==boss.id&&g.Choosing,"the Claim is sent once; once its offers arrive, OpenChoice");
            Check(shown?.id==boss.id&&shown.offers.SequenceEqual(Offers)&&NativeExpeditionPick.Eyebrow(shown).StartsWith("BOSS REWARD")&&NativeExpeditionPick.Tone(shown)=="epic","the tray shows it as a boss reward");
            // A refused open closes the tray with a notice instead of looping.
            var r=new Rig(offers:(a,id)=>null);r.Chest("p0","cache-a");r.Flow.Show(r.View());var open=r.Step();r.Send(open.Send);var after=r.Step();
            Check(open.Send.intent==ExpeditionIntent.Claim&&after.Notice!=null&&after.Notice.StartsWith("Chip could not be opened · Refused")&&!r.Flow.Open&&r.Step().Send.intent==ExpeditionIntent.None,"a refused open: a notice, closed, not resent ("+after.Notice+")");
            // Choose later while it opens: the offers arrive but the tray stays closed and choosing never opens.
            var l=new Rig();string c=l.Chest("p0","cache-b");l.Flow.Show(l.View());var o=l.Step();l.Flow.Later();l.Send(o.Send);var settled=l.Pump();
            Check(!l.Flow.Open&&l.Flow.Choosing(l.View())==null&&NativeExpeditionPick.Ready(l.View().Reward(c))&&!l.Choosing&&settled.Send.intent==ExpeditionIntent.None,"Choose later during the open: offers arrive, no tray, no choosing");
            l.Flow.Toggle(l.View());l.Pump();Check(l.Flow.Choosing(l.View())?.id==c&&l.Choosing,"C shows it");
            // Ready rewards come before unopened ones; the pick on a ready one then opens the unopened one (the Boss Test's queue).
            var q=new Rig();string u=q.Chest("p0","cache-c"),k=q.Chest("p0","cache-d");q.Claim("p0",k);q.Flow.Show(q.View());q.Pump();
            Check(q.Flow.Choosing(q.View())?.id==k,"the ready reward shows first");q.Send(q.Flow.Choose(q.View(),"vital"));var next=q.Pump();
            Check(next.Send.intent==ExpeditionIntent.Claim&&next.Send.target==u,"after the pick the next unopened reward is opened");
            var reopen=q.Pump();Check(reopen.Send.intent==ExpeditionIntent.OpenChoice&&reopen.Send.target==u&&q.Choosing,"and choosing reopens on it (the host cleared it with the last claimed pick)");
        }

        // 6. "N more waiting" and the CHIPS tag count the viewer's own unpicked rewards.
        private static void Waiting()
        {
            var g=new Rig(3);string a=g.Chest("p0","cache-a"),b=g.Chest("p0","cache-b");g.Chest("p1","cache-c");g.Chest("p0","cache-d");
            g.Pump();var v=g.View();Check(NativeExpeditionPick.Waiting(v)==3,"three of mine waiting (another pilot's not counted)");
            g.Claim("p0",a);g.Flow.Show(g.View());g.Pump();v=g.View();Check(g.Flow.Choosing(v)?.id==a&&NativeExpeditionPick.MoreWaiting(v,a)==2,"2 more waiting besides the shown one");
            Check(NativeExpeditionPick.MoreWaiting(v,null)==3&&NativeExpeditionPick.MoreWaiting(v,"not-mine")==3,"nothing shown: all of them");
            g.Send(g.Flow.Choose(v,"vital"));g.Pump();v=g.View();Check(NativeExpeditionPick.Waiting(v)==2,"a confirmed pick leaves the count");
            Check(NativeExpeditionPick.Waiting(v)==v.mine.rewards.Count(r=>!r.consumed),"the count is the minimal HUD's (unconsumed rewards of mine)");
            v.mine.extracted=true;Check(NativeExpeditionPick.Waiting(v)==0&&NativeExpeditionPick.MoreWaiting(v,b)==0,"an extracted pilot has nothing waiting");
        }

        // 7. Who is choosing, from the public flag only: seats, stable picks, a new pick per open, mine marked, no offers.
        private static void Choosers()
        {
            var g=new Rig(3);string theirs=g.Chest("p1","cache-a");g.Claim("p1",theirs,new[]{"p1-secret-a","p1-secret-b"});var p1=new ExpeditionHostCommands(g.Run,"p1",g.Hooks);
            var ch=new ExpeditionChoosers();ch.Step(g.View(),1);Check(ch.Count==0&&!ch.At(2,out _),"nobody choosing");
            Check(p1.Send(new ExpeditionCommand(ExpeditionIntent.OpenChoice,theirs)).state==ExpeditionCommandState.Pending,"p1 opens its choice");
            var v=g.View();ch.Step(v,1);Check(ch.At(2,out var e)&&e.Account=="p1"&&e.Seat==2&&!e.Mine&&e.Pick!=0&&e.Since==1&&!ch.At(1,out _)&&!ch.At(3,out _),"p1 is choosing at its seat, not mine");
            string json=JsonSerializer.Serialize(v,new JsonSerializerOptions{IncludeFields=true});Check(!json.Contains("p1-secret")&&v.members.First(m=>m.account=="p1").choosing,"the flag is public, the offers are not");
            ch.Step(g.View(),2);Check(ch.At(2,out var same)&&same.Pick==e.Pick&&same.Since==1,"a stable pick while it stays open");
            Check(p1.Send(new ExpeditionCommand(ExpeditionIntent.CloseChoice)).state==ExpeditionCommandState.Pending,"p1 closes");ch.Step(g.View(),3);Check(!ch.At(2,out _)&&ch.Count==0,"closed: gone");
            p1.Send(new ExpeditionCommand(ExpeditionIntent.OpenChoice,theirs));ch.Step(g.View(),4);Check(ch.At(2,out var reopened)&&reopened.Pick!=e.Pick&&reopened.Since==4,"a new open: a new pick, a new beat");
            string mine=g.Chest("p0","cache-b");g.Claim("p0",mine);g.Flow.Show(g.View());g.Pump();ch.Step(g.View(),5);Check(ch.At(1,out var me)&&me.Mine&&ch.Count==2,"mine is marked as mine");
            Check(g.Run.Defeat("p1"),"p1 downed");ch.Step(g.View(),6);Check(!ch.At(2,out _)&&ch.At(1,out _),"a downed pilot stops choosing");
            ch.Step(null,7);Check(ch.Count==0,"unbound: nobody");
        }

        // 8. Source scans.
        private static void Sources(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            string screen=R("Runtime/NativeDraftScreen.cs"),stage=R("Runtime/NativeDraftPresentation.cs"),follow=R("Runtime/NativeDraftFollow.cs"),exp=R("Runtime/NativeExpeditionChipScreen.cs");
            string game=R("Runtime/ConversionGame.cs"),pveUi=R("Runtime/NativePveUI.cs"),live=R("Runtime/NativeExpeditionLiveUI.cs"),hud=R("Runtime/NativeExpeditionHud.cs"),wrist=R("Runtime/NativeChipSelectPresentation.cs"),world=R("Runtime/NativeExpeditionWorldPresentation.cs"),core=R("Core/NativeExpeditionPick.cs");
            // The Boss Test path is untouched: its screen, stage and follow-through know nothing of the Expedition pick.
            foreach(var (name,src) in new[]{("NativeDraftScreen",screen),("NativeDraftPresentation",stage),("NativeDraftFollow",follow),("NativeExpeditionWorldPresentation",world)})Check(!src.Contains("ExpeditionPick")&&!src.Contains("ExpeditionChip"),name+" is unchanged by the Expedition pick");
            Check(stage.Contains("private bool PvePickScreen=>!menu&&arena!=null&&(PveFollowing||PvePickOpen)&&arena.Pve?.Holding!=true&&arena.Local.Hp>0&&!ReplayActive&&match.Phase==NativePhase.Fight;"),"the Boss Test's stage gate is the original");
            foreach(var line in new[]{"if(choice>=0){RecordPvePick(chips,choice,pick);pveOpenId=-1;ChoosePveChip(pick.Id,chips[choice].id);return;}","\"Choose later  C · Esc\",false,true,1,14))PveOpen(false);","string warn=\"YOU ARE VULNERABLE WHILE CHOOSING\";","string alarm=\"WARNING — YOU ARE UNDER ATTACK\""})Check(screen.Contains(line),"the Boss Test screen keeps: "+line.Substring(0,Math.Min(50,line.Length)));
            Check(game.Contains("if(Input.GetKeyDown(KeyCode.C)&&arena?.Pve!=null&&LiveExpedition==null){if(PvePickOpen)PveOpen(false);else PveOpen(true);}"),"the Boss Test's C stays, not in an Expedition");
            Check(pveUi.Contains("if(row.Picks.Count>0&&!PvePickOpen)PveChipsTag(W,H,row.Picks.Count,Time.unscaledTime-pvePendingAt);")&&pveUi.Contains("string chips=\"CHIPS \"+count;")&&pveUi.Contains("NativeHudChrome.Key(new Rect(cr.xMax-26,cr.y+6,18,18),\"C\");"),"the Boss Test's CHIPS tag (shared, unchanged)");
            // The overlay: the same cards over the live view, never the 3D stage or a hidden arena.
            foreach(var banned in new[]{"HideLive","DraftFrame","PvePickScreen","RenderDraft","Root.SetActive","DraftShades(","viewModelCamera"})Check(!exp.Contains(banned),"the overlay never uses "+banned);
            foreach(var s in new[]{"DrawTray(t,chips,i=>held(chips[i].id),elapsed,enabled,-1,i=>false,true)","DraftKeys(chips.Count,enabled,true)","LayoutTray(chips,0,band,h)","PickedTray(t,expeditionFollowChips,expeditionFollowIndex,expeditionFollowOwned,age)",
                "\"YOU ARE VULNERABLE WHILE CHOOSING\"","WARNING — YOU ARE UNDER ATTACK\"","(more+\" more waiting\").ToUpperInvariant()","HOLD ALT TO CLICK","\"Choose later  C · Esc\"","NativeExpeditionPick.Eyebrow(reward)","NativeExpeditionPick.MoreWaiting(v,reward.id)","expeditionPick.Later();","ExpeditionLiveSubmit(cmd);"})Check(exp.Contains(s),"the overlay uses "+s);
            Check(live.Contains("ExpeditionPickGui(width,height);")&&live.IndexOf("ExpeditionPickGui(width,height);",StringComparison.Ordinal)>live.IndexOf("ExpeditionChipsTag(v);",StringComparison.Ordinal),"drawn over the in-scene HUD");
            Check(Regex.Matches(exp,@"ExpeditionLiveSubmit\(").Count==2&&!exp.Contains("ChoosePveChip")&&!exp.Contains("PveOpen(")&&!exp.Contains("OpenPvePick")&&!exp.Contains("ChoosePve("),"intents only through ExpeditionLiveSubmit (the flow's and the Select), never the PvE messages");
            Check(core.Contains("new ExpeditionCommand(ExpeditionIntent.OpenChoice,Reward)")&&core.Contains("new ExpeditionCommand(ExpeditionIntent.CloseChoice)"),"the flow sends OpenChoice and CloseChoice");
            // Input: no movement lock; digits (and the mouse while Alt is held) are routed to the tray, client-side only.
            Check(game.Contains("&&arena.Local.Hp>0&&!PvePickOpen&&arena.Pve?.Holding!=true&&!DevHoldsPilot;")&&game.Contains("bool play=!menu&&match.Phase==NativePhase.Fight&&p.Hp>0&&!PvePickOpen&&arena.Pve?.Holding!=true;")&&!Regex.IsMatch(game,@"play=[^;]*Expedition(Pick|Choos)"),"no choosing-based play gate: the pilot keeps moving");
            int route=exp.IndexOf("private void ExpeditionPickRoute()",StringComparison.Ordinal),routeEnd=exp.IndexOf("private void ExpeditionPickKey(",StringComparison.Ordinal);Check(route>0&&routeEnd>route,"routing found");string body=exp.Substring(route,routeEnd-route);
            Check(body.Contains("pending.Hadoken=pending.HadokenHeld=pending.Uppercut=false;pending.SirenAbility=0;")&&body.Contains("if(ExpeditionPickPointer){pending.Shoot=pending.Attack=pending.Block=false;}"),"the digits' abilities and, with Alt, the mouse buttons go to the tray");
            foreach(var kept in new[]{"pending.X","pending.Z","pending.Jump","pending.Sprint","pending.Roll","pending.AbilityQ","pending.AbilityE","pending.Interact","pending.Reload","new InputFrame"})Check(!body.Contains(kept),"routing leaves "+kept+" alone");
            Check(game.Contains("ExpeditionPickRoute();\n")&&game.IndexOf("ExpeditionPickRoute();",StringComparison.Ordinal)>game.IndexOf("pending.Jump|=Input.GetKeyDown(KeyCode.Space);",StringComparison.Ordinal),"routed after the play input is read");
            Check(game.Contains("bool look=!BoardOpen&&!ExpeditionPickPointer&&DevLook(")&&game.Contains("!cinematic.Directing&&!ExpeditionPickPointer){yaw+="),"Alt frees the cursor and pauses mouse look only while held");
            Check(exp.Contains("Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt)")&&exp.Contains("if(!pointer)e.mousePosition=new Vector2(-1e5f,-1e5f);"),"without Alt the locked cursor never hovers or clicks a card");
            Check(live.Contains("ExpeditionPickStep();")&&live.Contains("if(Input.GetKeyDown(KeyCode.C)){bool fromJ=expeditionScreenOpen;expeditionScreenOpen=false;ExpeditionPickKey(fromJ);}")&&live.Contains("if(ExpeditionPickShown&&Input.GetKeyDown(KeyCode.Escape)&&!StageInputHeld){expeditionPick.Later();return true;}"),"C toggles it (from J too) and Escape is Choose later");
            // HUD: the Boss Test's tag in the scene; the J panel only opens the tray.
            Check(exp.Contains("if(n>0&&!expeditionPick.Open)PveChipsTag(U.W,U.H,n,Time.unscaledTime-expeditionChipsAt);")&&live.Contains("ExpeditionChipsTag(v);")&&live.Contains("E.Minimal(v,width,1,false);"),"the Expedition draws the same CHIPS tag in place of the minimal HUD's");
            int chips=hud.IndexOf("public static ExpeditionCommand Chips(",StringComparison.Ordinal),sanctuary=hud.IndexOf("public static ExpeditionCommand Sanctuary(",StringComparison.Ordinal);Check(chips>0&&sanctuary>chips,"chip panel found");string panel=hud.Substring(chips,sanctuary-chips);
            Check(panel.Contains("\"C · CHOOSE CHIP (\"+waiting+\")\"")&&panel.Contains("c.Choose();")&&!panel.Contains("ExpeditionIntent.Select,reward.id")&&!panel.Contains("ExpeditionIntent.Claim,reward.id")&&!panel.Contains("\"PICK\"")&&panel.Contains("\"HELD\""),"the J panel opens the tray instead of picking, and keeps the held chips");
            Check(live.Contains("Choose=()=>{expeditionScreenOpen=false;ExpeditionPickKey(true);},"),"its CHOOSE CHIP line closes J and opens the tray");
            // The wrist look and tag: every choosing pilot from the public flag; the PvE rows' line unchanged.
            int hook=wrist.IndexOf("if(ExpeditionChoosing!=null){pick=ExpeditionChoosing(id);",StringComparison.Ordinal),rows=wrist.IndexOf("NativeChipSelect.Selecting(true,row.Open?.Id??0,p.Hp>0,p.Seat.connected,v.Holding,!string.IsNullOrEmpty(v.Cinematic),v.Stage==\"ended\")",StringComparison.Ordinal);
            Check(hook>0&&rows>hook&&wrist.Contains("i<count&&pick.Offers!=null?arena?.PveChip(pick.Offers[i])?.rarity:null"),"the wrist look reads the Expedition hook first; an offer-less pick draws cyan cards");
            Check(exp.Contains("if(!expeditionChoosers.At(id,out var e))return null;")&&exp.Contains("expeditionChoosers.Step(v,arena?.Time??0);")&&exp.Contains("if(FirstPersonView&&(ExpeditionPickOverlay||ExpeditionFollowing))return null;"),"choosers from the view's flag; my first-person wrist steps aside under the tray");
            foreach(var (name,src) in new[]{("chip overlay",exp),("pick model",core)})
            {
                Check(!Regex.IsMatch(src,@"\bNativeExpedition\b"),name+" holds no authority reference");
                Check(!Regex.IsMatch(src,@"\b(run|Run|session|Session|expeditionSession|authority)\s*\.\s*(Purchase|Claim|Select|SetChoosing|Credit|Drop|DefeatBoss|Reprint|Extract|Defeat)\("),name+" calls no authority mutator");
                Check(!src.Contains("Random")&&!Regex.IsMatch(src,@"0x[0-9a-fA-F]{6}"),name+": no randomness, no restated colour");
            }
        }
    }
}
#endif
