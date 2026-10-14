#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using P=ChipArena.UnityConversion.NativeExpeditionPresentation;

namespace ChipArena.UnityConversion
{
    // The Expedition presentation contract (docs/EXPEDITION-PRESENTATION.md) against the real NativeExpedition authority: the
    // single-account view never carries another account's offers or personal state and survives the replication codec; host and
    // guest command paths; state-driven confirmation (refusals, missing hooks and missing transport never read as success);
    // individual extraction choices while others continue, with the save-before-publish confirmation; boss eligibility frozen at
    // the boss's death; uneven allocation where the dice display shows exactly the authority's stored rolls and winners;
    // independent personal picks; host-transfer freeze / abort / commit; the dev preview scenarios for every viewer; and source
    // scans that keep the HUD on the palette and the screens off the authority's mutators.
    internal static class ExpeditionPresentationChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Expedition presentation: "+what);count++;}
        private static readonly JsonSerializerOptions Options=new JsonSerializerOptions{IncludeFields=true};
        private static string Json(object v)=>JsonSerializer.Serialize(v,Options);
        private static ExpeditionView Replicate(ExpeditionView v)=>JsonSerializer.Deserialize<ExpeditionView>(Json(v),Options);
        private static string Public(ExpeditionView v){var c=Replicate(v);c.viewer=null;c.mine=new ExpeditionViewPersonal();return Json(c);}
        // Unity's JsonUtility writes a null string as "" (and decodes it so): the same view as a guest decoding it would see it.
        private static object Emptied(object o)
        {
            if(o==null)return null;var t=o.GetType();if(t==typeof(string)||t.IsPrimitive||t.IsEnum)return o;
            if(o is Array a){for(int i=0;i<a.Length;i++)a.SetValue(Emptied(a.GetValue(i)),i);return o;}
            foreach(var f in t.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))
                f.SetValue(o,f.FieldType==typeof(string)?(f.GetValue(o)??""):Emptied(f.GetValue(o)));
            return o;
        }
        // Everything the screens say for a view (text, states, enablement), to compare host-built and decoded views.
        private static string Says(ExpeditionView v)
        {
            var b=new System.Text.StringBuilder();
            foreach(var r in P.Board(v))b.Append(r.state).Append(r.detail).Append(r.sealedDoor).Append('|');var f=P.Final(v);b.Append(f.state).Append(f.detail).Append(P.Objective(v,out var tone)).Append(tone);
            foreach(var r in P.Loot(v))b.Append(r.label).Append(r.detail).Append(r.state).Append(r.mine).Append('|');foreach(var r in P.Roster(v))b.Append(r.status).Append(r.you).Append(r.host).Append('|');
            var x=P.Extraction(v);b.Append(x.title).Append(x.detail).Append(x.can).Append(x.hostNeedsTransfer);var d=P.Departure(v);var g=P.GroupExtraction(v);b.Append(d.detail).Append(d.voted).Append(g.detail).Append(g.voted);
            var steps=P.Transfer(v,out string head,out _);foreach(var st in steps)b.Append(st.label).Append(st.detail).Append(st.state).Append('|');b.Append(head);
            foreach(var dd in v.dice){var sh=P.Share(v,dd);b.Append(P.ShareLine(sh)).Append(sh.eligible).Append(sh.yours);}
            b.Append(v.ViewerIsHost()).Append(NativeExpeditionPresentation.Has(v.mine.account));return b.ToString();
        }
        private sealed class Silent:IExpeditionCommands{public string Viewer{get;set;}public string Blocked(ExpeditionCommand c)=>null;public ExpeditionCommandResult Send(ExpeditionCommand c)=>ExpeditionCommandResult.Sent;}

        private static NativeExpedition Party(int n=4)
        {
            var run=NativeExpedition.Create("view-run","city","p0","sword",91,new[]{new ExpeditionInstance{id="ward-zero",target=3},new ExpeditionInstance{id="station",target=2}});
            for(int i=1;i<n;i++)Check(run.Join("p"+i,"sword"),"join");return run;
        }
        private static void Quota(NativeExpedition run,string area,params string[] bosses)
        {Check(run.Enter(area)&&run.Progress("q-"+area,run.Snapshot().instances.First(i=>i.id==area).target)&&run.StartBosses(bosses),"quota and bosses");}

        public static void Run(string root)
        {
            count=0;
            Privacy();Commands();Eligibility();Dice();Selections();Extraction();Transfer();Board();Previews();Sources(root);
            Console.WriteLine("PASS Expedition presentation ("+count+" checks)");
        }

        // 1. One account's view: public state for all, personal state for the viewer only; the replicated DTO is identical.
        private static void Privacy()
        {
            var run=Party();
            Check(run.Credit("p0","seed",500)&&run.Credit("p1","seed",300)&&run.Credit("p2","seed",120),"wallets");
            Check(run.Purchase("p0","cache-a",150)&&run.Purchase("p1","cache-b",100),"purchases");
            var s=run.Snapshot();string Chest(string owner)=>s.rewards.First(r=>r.owner==owner&&r.source=="chest").id;
            Check(run.Claim("p0",Chest("p0"),new[]{"p0-secret-a","p0-secret-b"})&&run.Claim("p1",Chest("p1"),new[]{"p1-secret-a"}),"chest offers");
            Check(run.Drop("walker-1")&&run.Drop("walker-2"),"drops");var drop=run.Snapshot().rewards.First(r=>r.source=="monster").id;
            Check(run.Claim("p2",drop,new[]{"p2-secret-a","p2-secret-b"}),"first pickup");
            Quota(run,"ward-zero","scientist");Check(run.DefeatBoss("scientist",6),"boss");
            var boss=run.Snapshot().rewards.First(r=>r.source=="boss"&&r.owner=="p3").id;Check(run.Claim("p3",boss,new[]{"p3-secret-a"}),"boss offers");
            var before=Json(run.Snapshot());var secrets=new Dictionary<string,string[]>{{"p0",new[]{"p0-secret"}},{"p1",new[]{"p1-secret"}},{"p2",new[]{"p2-secret"}},{"p3",new[]{"p3-secret"}}};
            string pub=null;var snapshot=run.Snapshot();
            foreach(var viewer in new[]{"p0","p1","p2","p3"})
            {
                var v=NativeExpeditionView.Build(run,viewer);string json=Json(v);
                foreach(var other in secrets.Where(k=>k.Key!=viewer))foreach(var secret in other.Value)Check(!json.Contains(secret),viewer+" view carries no offer of "+other.Key);
                Check(json.Contains(viewer+"-secret"),viewer+" view carries its own pending offers");
                Check(NativeExpeditionView.Leaks(v,snapshot).Count==0,viewer+" view passes the privacy audit");
                Check(v.mine.account==viewer&&v.mine.wallet==snapshot.members.First(p=>p.account==viewer).wallet,viewer+" wallet is its own");
                Check(v.mine.rewards.All(r=>snapshot.rewards.First(x=>x.id==r.id).owner==viewer),viewer+" rewards are its own");
                Check(v.loot.All(l=>l.source=="monster"||l.source=="boss"),"purchases and catch-up never public");
                Check(!v.loot.Any(l=>l.id.Contains("chest")),"no chest in public loot");
                // The guest receives the host-built DTO through the codec: identical, field for field.
                var g=Replicate(v);Check(Json(g)==json,viewer+" view survives the replication codec unchanged");
                // Public part identical for every viewer (the same sanitized frame), personal part differs.
                string p=Public(v);if(pub==null)pub=p;else Check(p==pub,"public state is the same for every viewer");
            }
            // The host reads exactly what it would send a guest for its own account.
            var host=NativeExpeditionView.Build(run,"p0");Check(Json(Replicate(host))==Json(host)&&host.ViewerIsHost(),"host view is the replicated contract");
            Check(Json(run.Snapshot())==before,"building views never changes the authority");
            var outsider=NativeExpeditionView.Build(run,"stranger");Check(outsider.mine.account==null&&outsider.mine.rewards.Length==0&&outsider.mine.wallet==0&&NativeExpeditionView.Leaks(outsider,snapshot).Count==0,"non-member gets no personal state");
            // Settled history is bounded: consumed rewards of earlier loops/areas drop out, open ones stay.
            Check(run.Select("p3",boss,"p3-secret-a",c=>true),"select");var after=NativeExpeditionView.Build(run,"p3");Check(after.mine.rewards.First(r=>r.id==boss).offers.Length==0,"offers cleared after the pick");
            var drop2=run.Snapshot().rewards.First(r=>r.source=="monster"&&!r.claimed).id;Check(after.loot.Any(l=>l.id==drop2&&!l.claimed&&l.owner==null),"open ordinary drop is public and unowned");
        }

        // 2. Host commands call the real authority; guests and missing hooks are unavailable; nothing pending reads as done.
        private static void Commands()
        {
            var run=Party(3);Check(run.Enter("ward-zero"),"enter");var tracker=new ExpeditionIntentTracker();
            var host=new ExpeditionHostCommands(run,"p0");var guest=new ExpeditionGuestCommands("p1");
            var vote=new ExpeditionCommand(ExpeditionIntent.VoteExit);
            var r=tracker.Submit(host,vote,NativeExpeditionView.Build(run,"p0"),0);Check(r.state==ExpeditionCommandState.Refused,"sealed-door vote refused by the authority");
            Check(tracker.State(vote,NativeExpeditionView.Build(run,"p0"),20).state==ExpeditionCommandState.Refused,"a refusal stays a refusal");
            P.Feedback(tracker.State(vote,NativeExpeditionView.Build(run,"p0"),1),out var tone);Check(tone==ExpeditionTone.Danger,"refusal shown in danger");
            foreach(ExpeditionIntent intent in Enum.GetValues(typeof(ExpeditionIntent)))
            {
                var c=new ExpeditionCommand(intent,"p0","vital");Check(guest.Blocked(c)==ExpeditionGuestCommands.NoChannel,"guest "+intent+" unavailable");
                Check(guest.Send(c).state==ExpeditionCommandState.Unavailable,"guest send never succeeds: "+intent);
            }
            var gv=tracker.Submit(guest,vote,NativeExpeditionView.Build(run,"p1"),0);Check(gv.state==ExpeditionCommandState.Unavailable&&gv.reason==ExpeditionGuestCommands.NoChannel,"guest vote unavailable with its reason");
            Check(tracker.State(vote,NativeExpeditionView.Build(run,"p1"),1).state==ExpeditionCommandState.Unavailable&&tracker.State(vote,NativeExpeditionView.Build(run,"p0"),1).state==ExpeditionCommandState.Refused,"intents are tracked per viewer");
            Check(host.Blocked(new ExpeditionCommand(ExpeditionIntent.Claim,"x"))==ExpeditionIntegrationHooks.NoOffers&&host.Blocked(new ExpeditionCommand(ExpeditionIntent.Select,"x","y"))==ExpeditionIntegrationHooks.NoEligibility
                &&host.Blocked(new ExpeditionCommand(ExpeditionIntent.Reprint,"p1"))==ExpeditionIntegrationHooks.NoReprint,"missing integration hooks block their controls");
            Check(host.Blocked(new ExpeditionCommand(ExpeditionIntent.Extract))==ExpeditionIntegrationHooks.HostTransfer,"host with peers cannot extract without a transfer");
            Check(new ExpeditionHostCommands(run,"p1").Blocked(new ExpeditionCommand(ExpeditionIntent.NextLoop))!=null,"only the host starts a loop");
            // Pending until the authority reflects it, then confirmed; never confirmed by the button press itself.
            Check(run.Progress("k",3)&&run.StartBosses(new[]{"b"})&&run.DefeatBoss("b"),"clear");
            r=tracker.Submit(host,vote,NativeExpeditionView.Build(run,"p0"),2);Check(r.state==ExpeditionCommandState.Pending,"a sent vote is pending");
            Check(!ExpeditionIntentTracker.Reflected(vote,Replicate(NativeExpeditionView.Build(run,"p1"))),"another viewer's view does not confirm my vote");
            Check(tracker.State(vote,NativeExpeditionView.Build(run,"p0"),2.1).state==ExpeditionCommandState.Confirmed,"confirmed once the view shows the vote");
            var cancel=new ExpeditionCommand(ExpeditionIntent.CancelExit);Check(tracker.Submit(host,cancel,NativeExpeditionView.Build(run,"p0"),3).state==ExpeditionCommandState.Pending&&tracker.Last("p0",vote)==ExpeditionIntent.CancelExit,"cancel replaces the vote");
            Check(tracker.State(vote,NativeExpeditionView.Build(run,"p0"),3.1).state==ExpeditionCommandState.Confirmed,"cancel confirmed by the view");
            // A sender that accepts but never changes the authority times out as a failure.
            var silent=new Silent{Viewer="p0"};var claim=new ExpeditionCommand(ExpeditionIntent.Claim,"nothing");tracker.Submit(silent,claim,NativeExpeditionView.Build(run,"p0"),10);
            Check(tracker.State(claim,NativeExpeditionView.Build(run,"p0"),12).state==ExpeditionCommandState.Pending,"pending while waiting");
            var timedOut=tracker.State(claim,NativeExpeditionView.Build(run,"p0"),10+ExpeditionIntentTracker.Timeout+.1);Check(timedOut.state==ExpeditionCommandState.Failed&&timedOut.reason==ExpeditionIntentTracker.NoConfirmation,"no confirmation is a failure, not a success");
            foreach(ExpeditionCommandState st in Enum.GetValues(typeof(ExpeditionCommandState)))
            {P.Feedback(ExpeditionCommandResult.Of(st,"x"),out var t);Check((t==ExpeditionTone.Good)==(st==ExpeditionCommandState.Confirmed),"only a confirmation reads as success: "+st);}
            // Extraction: extracted but unsaved stays pending ("awaiting save"), even past the timeout.
            var solo=Party(1);Quota(solo,"ward-zero","b");Check(solo.DefeatBoss("b")&&solo.VoteExit("p0",true),"solo clear");solo.Tick(.25);Check(solo.SetSafe("p0",true),"sanctuary");
            var extract=new ExpeditionCommand(ExpeditionIntent.Extract);var hooks=new ExpeditionIntegrationHooks{Extract=a=>solo.Extract(a)?ExpeditionCommandResult.Sent:ExpeditionCommandResult.Of(ExpeditionCommandState.Refused)};
            var t2=new ExpeditionIntentTracker();t2.Submit(new ExpeditionHostCommands(solo,"p0",hooks),extract,NativeExpeditionView.Build(solo,"p0"),0);
            var unsaved=t2.State(extract,NativeExpeditionView.Build(solo,"p0"),60);Check(unsaved.state==ExpeditionCommandState.Pending&&unsaved.reason==ExpeditionIntentTracker.AwaitingSave,"extraction without a durable save is never shown complete");
            var model=P.Extraction(NativeExpeditionView.Build(solo,"p0"));Check(model.extracted&&!model.saved&&model.title.Contains("NOT CONFIRMED"),"extraction panel says the save is not confirmed");
            // The last vote makes the departure unanimous: the authority leaves on the next tick and resets the votes; still confirmed.
            var duo=Party(2);Quota(duo,"ward-zero","b");Check(duo.DefeatBoss("b")&&duo.VoteExit("p1",true),"duo clear and first vote");var t3=new ExpeditionIntentTracker();
            Check(t3.Submit(new ExpeditionHostCommands(duo,"p0"),vote,NativeExpeditionView.Build(duo,"p0"),0).state==ExpeditionCommandState.Pending,"final vote sent");duo.Tick(.25);
            var gone=NativeExpeditionView.Build(duo,"p0");Check(gone.phase==ExpeditionPhase.Town&&gone.exitVotes.Length==0&&t3.State(vote,gone,.3).state==ExpeditionCommandState.Confirmed,"a vote that completed the departure is confirmed, not timed out");
            // A pick whose reward leaves the bounded view once settled (catch-up ids carry no loop prefix) is still confirmed.
            var built=Party(1).Snapshot();built.members[0].chips=new[]{new ChipCount{id="vital",count=3}};var cu=new NativeExpedition(built);Check(cu.Join("p1","bow")&&cu.IssueCatchup("p1"),"catch-up");
            var cuId=cu.Snapshot().rewards.Single().id;Check(cu.Claim("p1",cuId,new[]{"vital","ricochet"}),"catch-up offers");var t4=new ExpeditionIntentTracker();
            var cuHooks=new ExpeditionIntegrationHooks{Eligible=(a,c)=>true};var sel=new ExpeditionCommand(ExpeditionIntent.Select,cuId,"ricochet");
            var cuHost=new ExpeditionHostCommands(cu,"p0",cuHooks);Check(cuHost.Blocked(sel)==null,"host-side select available");
            // The host's own path selects only for the host; record p1's pick as the host would receive it.
            var beforePick=NativeExpeditionView.Build(cu,"p1");Check(beforePick.Reward(cuId)!=null,"pending pick in view");
            Check(t4.Record("p1",sel,cu.Select("p1",cuId,"ricochet",c=>true)?ExpeditionCommandResult.Sent:ExpeditionCommandResult.Of(ExpeditionCommandState.Refused),beforePick,0).state==ExpeditionCommandState.Pending,"pick sent");
            var settled=NativeExpeditionView.Build(cu,"p1");Check(settled.Reward(cuId)==null&&t4.State(sel,settled,.1).state==ExpeditionCommandState.Confirmed,"a settled pick that left the view is confirmed");
            Check(!ExpeditionIntentTracker.Reflected(new ExpeditionCommand(ExpeditionIntent.Select,"never-seen","x"),settled),"an unknown reward is never confirmed");
        }

        // 3. Boss eligibility freezes at the boss's death: dead then is ineligible; dying later keeps the reservation.
        private static void Eligibility()
        {
            var run=Party();Quota(run,"ward-zero","scientist");Check(run.Defeat("p3"),"p3 down before the boss dies");Check(run.DefeatBoss("scientist"),"boss");Check(run.Defeat("p2"),"p2 down after");
            var dead=NativeExpeditionView.Build(run,"p3");var rows=P.Loot(dead);
            Check(rows.Any(r=>r.state==P.LootState.NotEligible&&r.boss==dead.dice[0].boss),"dead-at-death viewer sees NOT ELIGIBLE");
            Check(!rows.Any(r=>r.mine),"dead-at-death viewer has nothing reserved");
            var share=P.Share(dead,dead.dice[0]);Check(!share.eligible&&share.yours==0,"share: ineligible, none reserved");
            var later=NativeExpeditionView.Build(run,"p2");var lr=P.Loot(later);
            Check(lr.Count(r=>r.state==P.LootState.ReservedForYou)==1&&!lr.Any(r=>r.state==P.LootState.NotEligible),"dying after the boss keeps the reserved chip");
            Check(P.Share(later,later.dice[0]).eligible,"share: eligible");
            Check(P.Loot(NativeExpeditionView.Build(run,"p0")).Count(r=>r.state==P.LootState.ReservedForOther)==2,"others' reservations are shown, not offered");
            Check(run.Absorb("p0","p3")&&run.Credit("p0","cash",50)&&run.Reprint("p0","p3",50),"late revival");
            Check(P.Share(NativeExpeditionView.Build(run,"p3"),run.Snapshot().dice[0]).eligible==false,"revival does not change eligibility");
            Check(P.Roster(NativeExpeditionView.Build(run,"p0")).First(r=>r.account=="p2").status=="DOWN","roster shows the downed pilot");
        }

        // 4. Uneven allocation: the display shows the authority's split and stored dice exactly; the reveal rolls nothing.
        private static void Dice()
        {
            foreach(var (players,drops) in new[]{(3,5),(4,6),(2,1),(5,2),(8,11),(3,3)})
            {
                var run=Party(players);Quota(run,"ward-zero","boss");Check(run.DefeatBoss("boss",drops),"boss");
                var authority=run.Snapshot().dice.Single();var v=NativeExpeditionView.Build(run,"p0");var d=v.dice.Single();
                Check(Json(d)==Json(authority),"view dice equal the authority's record ("+players+"/"+drops+")");
                var s=P.Share(v,d);Check(s.total==drops&&s.each==drops/players&&s.extra==drops%players&&s.survivors==players,"share line from the record ("+players+"/"+drops+")");
                foreach(var a in d.eligible)Check(v.loot.Count(l=>l.boss==d.boss&&l.owner==a)==d.each+(d.winners.Contains(a)?1:0),"reserved count per survivor = even share + dice");
                string frozen=Json(v);float end=P.Dice.Duration(d);
                for(float t=0;t<=end+1;t+=.05f)for(int i=0;i<d.eligible.Length;i++)
                {
                    int face=P.Dice.Face(d,i,t);Check(face==-1||face==d.rolls[i],"a die shows nothing or its stored roll");
                    Check(!P.Dice.Winner(d,i,t)||d.winners.Contains(d.eligible[i]),"only stored winners light");
                }
                for(int i=0;i<d.eligible.Length;i++){Check(P.Dice.Face(d,i,end)==d.rolls[i],"every die locks on its stored roll");Check(P.Dice.Winner(d,i,end)==d.winners.Contains(d.eligible[i]),"winners exactly the stored ones");}
                Check(!Enumerable.Range(0,d.eligible.Length).Any(i=>P.Dice.Winner(d,i,P.Dice.WinnersAt(d)-.01f)),"no winner before every die has locked");
                Check(Json(v)==frozen&&Json(run.Snapshot().dice.Single())==Json(authority),"the reveal changes nothing");
                Check(P.ShareLine(s).Contains(drops==1?"1 chip":drops+" chips"),"share line names the total");
            }
            var tie=new ExpeditionDice{boss="0:a:boss:x",eligible=new[]{"a","b","c"},rolls=new[]{50,50,10},winners=new[]{"b"},each=0};
            var noTie=new ExpeditionDice{boss="0:a:boss:y",eligible=new[]{"a","b","c"},rolls=new[]{70,50,10},winners=new[]{"a"},each=0};
            var empty=new ExpeditionView{viewer="a"};Check(P.Share(empty,tie).tieBreak&&!P.Share(empty,noTie).tieBreak,"a tie at the cut is labelled as the seeded tie-break");
        }

        // 5. Personal selections: each owner claims and picks from their own offers; the views stay separate.
        private static void Selections()
        {
            var run=Party(2);Quota(run,"ward-zero","boss");Check(run.DefeatBoss("boss"),"boss");var tracker=new ExpeditionIntentTracker();
            var hooks=new ExpeditionIntegrationHooks{Offers=(a,r)=>new[]{a+"-pick-1",a+"-pick-2"},Eligible=(a,c)=>c.StartsWith(a,StringComparison.Ordinal)};
            var host=new ExpeditionHostCommands(run,"p0",hooks);string mine=run.Snapshot().rewards.First(r=>r.owner=="p0").id,theirs=run.Snapshot().rewards.First(r=>r.owner=="p1").id;
            Check(tracker.Submit(host,new ExpeditionCommand(ExpeditionIntent.Claim,theirs),NativeExpeditionView.Build(run,"p0"),0).state==ExpeditionCommandState.Refused,"cannot open another player's reserved chip");
            Check(tracker.Submit(host,new ExpeditionCommand(ExpeditionIntent.Claim,mine),NativeExpeditionView.Build(run,"p0"),0).state==ExpeditionCommandState.Pending,"claim sent");
            var v0=NativeExpeditionView.Build(run,"p0");Check(tracker.State(new ExpeditionCommand(ExpeditionIntent.Claim,mine),v0,.1).state==ExpeditionCommandState.Confirmed&&v0.Reward(mine).offers.SequenceEqual(new[]{"p0-pick-1","p0-pick-2"}),"claim confirmed with own offers");
            Check(run.Claim("p1",theirs,new[]{"p1-pick-1","p1-pick-2"}),"p1 claims (host-generated offers)");
            var v1=NativeExpeditionView.Build(run,"p1");Check(!Json(v1).Contains("p0-pick")&&!Json(NativeExpeditionView.Build(run,"p0")).Contains("p1-pick"),"pending offers stay with their owner");
            var pick=new ExpeditionCommand(ExpeditionIntent.Select,mine,"p0-pick-2");Check(tracker.Submit(host,new ExpeditionCommand(ExpeditionIntent.Select,mine,"p1-pick-1"),v0,1).state==ExpeditionCommandState.Refused,"another player's offer cannot be picked");
            Check(tracker.Submit(host,pick,v0,1).state==ExpeditionCommandState.Pending,"pick sent");
            Check(run.Select("p1",theirs,"p1-pick-1",c=>true),"p1 picks independently");
            v0=NativeExpeditionView.Build(run,"p0");v1=NativeExpeditionView.Build(run,"p1");
            Check(tracker.State(pick,v0,1.1).state==ExpeditionCommandState.Confirmed,"pick confirmed by the view");
            Check(v0.mine.chips.Single().id=="p0-pick-2"&&v1.mine.chips.Single().id=="p1-pick-1","each view holds only its own pick");
            Check(!Json(v0).Contains("p1-pick")&&!Json(v1).Contains("p0-pick"),"picks are personal");
        }

        // 6. Extraction choices while others continue (preview transaction = the documented save-then-publish order).
        private static void Extraction()
        {
            var writes=new List<ExpeditionSavepoint>();var p=new NativeExpeditionPreview(s=>writes.Add(s.Copy()));p.Load(Array.FindIndex(NativeExpeditionPreview.Scenarios,s=>s.Id=="victory"));
            var v=p.View("astra");Check(v.phase==ExpeditionPhase.Victory&&P.Extraction(v).can&&P.Objective(v,out _).StartsWith("VICTORY"),"victory offers extraction");
            Check(p.View("sheldon").mine.canExtract&&P.Extraction(p.View("sheldon")).hostNeedsTransfer,"the host's extraction needs a transfer while others stay");
            Check(p.Next(),"astra extracts");v=p.View("astra");
            Check(v.mine.extracted&&v.mine.extractionSaved&&writes.Last().extracted.Contains("astra"),"astra extracted after a durable write that holds her tombstone");
            Check(p.Intents.State(new ExpeditionCommand(ExpeditionIntent.Extract),v,p.Clock).state==ExpeditionCommandState.Confirmed,"astra's extraction confirmed by the saved state");
            var nova=p.View("nova");Check(!nova.mine.extracted&&P.Roster(nova).First(r=>r.account=="astra").status=="EXTRACTED"&&P.Roster(nova).First(r=>r.you).status.StartsWith("IN RUN"),"nova continues and sees astra extracted");
            Check(p.Next(),"kestrel extracts");Check(p.View("kestrel").mine.extractionSaved&&!p.View("nova").mine.extracted,"different choices: kestrel out, nova in");
            Check(p.Next(),"next loop");nova=p.View("nova");
            Check(nova.loop==1&&nova.phase==ExpeditionPhase.Town&&nova.instances.All(i=>!i.cleared&&i.progress==0)&&P.Final(nova).state=="LOCKED","loop 2 resets the board for those who stayed");
            Check(nova.difficulty.health>new ExpeditionScaling().Evaluate(nova.elapsed,nova.difficultyParty,0,false).health,"the harder loop raises the scaling read-out");
            Check(p.View("astra").mine.extracted&&p.View("astra").mine.extractionSaved,"extracted pilots stay extracted into the next loop");
            Check(!p.Run.Join("astra","rifle"),"an extracted account cannot rejoin");
            // Town: one leaves alone, the group vote needs a strict majority, a failed save keeps the pilot in the run.
            p.Load(Array.FindIndex(NativeExpeditionPreview.Scenarios,s=>s.Id=="extract"));writes.Clear();
            Check(p.Next()&&p.View("astra").mine.extractionSaved&&p.View("sheldon").living==3,"individual extraction in town");
            Check(p.Next(),"two votes");var g=P.GroupExtraction(p.View("kestrel"));Check(g.active&&g.votes==2&&g.needed==2&&g.seconds==10&&!g.voted,"strict majority (2 of 3) starts the group countdown");
            Check(P.Active(p.View("kestrel")).label=="GROUP EXTRACTION","HUD shows the group countdown");
            Check(p.Next(),"cancel");Check(!P.GroupExtraction(p.View("kestrel")).active&&p.View("kestrel").extractAt<0,"below the majority it cancels");
            int saved=writes.Count;Check(p.Next(),"failed save");var k=p.View("kestrel");var fail=p.Intents.State(new ExpeditionCommand(ExpeditionIntent.Extract),k,p.Clock);
            Check(!k.mine.extracted&&fail.state==ExpeditionCommandState.Failed&&fail.reason==NativeExpeditionPreview.WriteFailure&&writes.Count==saved,"a failed save is reported and the pilot stays in the run");
        }

        // 7. Host transfer: frozen, aborted (failure shown, nobody extracted), refused, and a commit only when the authority shows it.
        private static void Transfer()
        {
            var p=new NativeExpeditionPreview();p.Load(Array.FindIndex(NativeExpeditionPreview.Scenarios,s=>s.Id=="transfer"));
            Check(p.Next(),"prepare");var v=p.View("nova");
            Check(v.migrating&&v.transfer.stage==ExpeditionTransferStage.Frozen&&v.transfer.nextHost=="astra"&&P.Objective(v,out _).StartsWith("HOST TRANSFER"),"frozen with the elected candidate");
            var steps=P.Transfer(v,out string head,out var tone);
            Check(head=="TRANSFERRING HOST TO ASTRA"&&steps[0].state==ExpeditionStep.Done&&steps[1].state==ExpeditionStep.Done&&steps[2].state==ExpeditionStep.Unavailable&&steps[3].state==ExpeditionStep.Unavailable&&steps[4].state==ExpeditionStep.Unavailable,"missing transport steps are unavailable, not done");
            Check(v.transfer.candidates.Length==2&&v.transfer.candidates.Any(c=>c.account=="nova"&&c.latencyMs==21),"transport candidates shown as reported");
            Check(p.Next(),"abort");v=p.View("nova");steps=P.Transfer(v,out head,out tone);
            Check(!v.migrating&&v.transfer.stage==ExpeditionTransferStage.Aborted&&head=="HOST TRANSFER FAILED"&&tone==ExpeditionTone.Danger&&steps.Last().state==ExpeditionStep.Failed,"abort shown as a failure");
            Check(v.host=="sheldon"&&!p.View("sheldon").mine.extracted&&steps.Last().detail.Contains("No destination ACK"),"abort keeps the host, nobody extracted, reason shown");
            Check(p.Next(),"simulated commit");v=p.View("nova");steps=P.Transfer(v,out head,out tone);
            Check(v.host=="astra"&&v.transfer.stage==ExpeditionTransferStage.Committed&&head=="HOST IS NOW ASTRA"&&tone==ExpeditionTone.Warn&&steps[3].state==ExpeditionStep.Unavailable,"committed rules shown, endpoint still unavailable");
            Check(p.View("sheldon").mine.extracted&&p.View("sheldon").mine.extractionSaved,"the leaving host is extracted with a saved tombstone");
            // A log that claims a commit the authority does not show is reported as a failure.
            var run=Party(2);Quota(run,"ward-zero","b");Check(run.DefeatBoss("b")&&run.VoteExit("p0",true)&&run.VoteExit("p1",true),"clear");run.Tick(.25);Check(run.SetSafe("p0",true)&&run.SetSafe("p1",true),"safe");
            var log=new ExpeditionTransferLog();var h=run.PrepareHandoff("t1",new[]{"p0"},new[]{new ExpeditionHostCandidate{account="p1",reachable=true,capable=true}});log.Prepared(h);
            Check(run.AbortHandoff("t1"),"abort");log.Committed("t1");var w=NativeExpeditionView.Build(run,"p1",new ExpeditionViewSources{Transfer=log});
            Check(w.transfer.stage==ExpeditionTransferStage.Refused&&w.transfer.reason.Contains("not reflected"),"an unreflected commit is not shown as success");
            log.Prepared(run.PrepareHandoff("t2",new[]{"p0"},Array.Empty<ExpeditionHostCandidate>()));w=NativeExpeditionView.Build(run,"p1",new ExpeditionViewSources{Transfer=log});
            P.Transfer(w,out head,out tone);Check(w.transfer.stage==ExpeditionTransferStage.Refused&&head=="HOST TRANSFER REFUSED"&&tone==ExpeditionTone.Danger,"no eligible connection is a refusal");
            log.Clear();Check(NativeExpeditionView.Build(run,"p1",new ExpeditionViewSources{Transfer=log}).transfer.stage==ExpeditionTransferStage.None,"no transfer, no overlay");
        }

        // 8. Board, objective and departure countdown follow the authority.
        private static void Board()
        {
            var run=Party(4);var v=NativeExpeditionView.Build(run,"p1");
            Check(P.Board(v).All(r=>r.state=="OPEN")&&P.Final(v).state=="LOCKED"&&P.Final(v).detail=="0 / 2 buildings cleared","fresh board");
            Check(run.Enter("ward-zero"),"enter");v=NativeExpeditionView.Build(run,"p1");var row=P.Board(v)[0];
            Check(row.state=="SEALED"&&row.sealedDoor&&row.current&&!P.Departure(v).open,"sealed while the quota runs");
            Check(run.Progress("k",2),"progress");v=NativeExpeditionView.Build(run,"p1");Check(P.Board(v)[0].progress==2&&Math.Abs(P.Board(v)[0].fill-2/3f)<1e-6&&P.Objective(v,out _)=="WARD ZERO · QUOTA 2 / 3","quota progress");
            Check(run.Progress("k2",1)&&run.StartBosses(new[]{"a","b"})&&run.DefeatBoss("a"),"one of two bosses");v=NativeExpeditionView.Build(run,"p1");
            Check(P.Board(v)[0].state=="BOSS"&&P.Board(v)[0].sealedDoor&&P.Objective(v,out var t)=="WARD ZERO · BOSS 1 / 2 DOWN"&&t==ExpeditionTone.Magenta,"boss phase keeps the door sealed");
            Check(run.DefeatBoss("b"),"both down");v=NativeExpeditionView.Build(run,"p1");Check(P.Board(v)[0].state=="CLEARED"&&!P.Board(v)[0].sealedDoor&&P.Departure(v).open&&!P.Departure(v).active,"cleared opens the door");
            Check(run.VoteExit("p1",true),"vote");v=NativeExpeditionView.Build(run,"p2");var d=P.Departure(v);
            Check(d.active&&d.votes==1&&d.needed==4&&d.seconds==10&&!d.voted&&P.Departure(NativeExpeditionView.Build(run,"p1")).voted,"one vote starts the shared timer");
            for(int i=0;i<16;i++)run.Tick(.25);v=NativeExpeditionView.Build(run,"p2");Check(P.Departure(v).seconds==6,"countdown from the authority clock");
            Check(run.VoteExit("p1",false),"cancel");Check(!P.Departure(NativeExpeditionView.Build(run,"p2")).active,"zero votes cancel");
            Check(P.Clock(0)=="0:00"&&P.Clock(65.9)=="1:05"&&P.Clock(3725)=="1:02:05"&&P.Clock(double.NaN)=="0:00","clock text");
            Check(P.Name("ward-zero")=="WARD ZERO"&&P.Name("0:ward-zero:boss:zombie-scientist")=="ZOMBIE SCIENTIST","names");
            var sc=new ExpeditionScaling();var a=NativeExpeditionView.Build(run,"p0");
            Check(Math.Abs(a.difficulty.health-sc.Evaluate(a.elapsed,4,0,true).health)<1e-12&&a.difficultyParty==4&&a.difficultyStep==0,"scaling read-out is the authority's function");
        }

        // 9. Every preview scenario, every step, every viewer: valid, leak-free and identical through the replication codec.
        private static void Previews()
        {
            var p=new NativeExpeditionPreview();
            for(int s=0;s<NativeExpeditionPreview.Scenarios.Length;s++)
            {
                p.Load(s);int guard=0;
                do
                {
                    Check(ExpeditionValidation.Valid(p.Run.Snapshot()),"preview "+p.Current.Id+" step "+p.StepIndex+" valid");var snap=p.Run.Snapshot();
                    foreach(var viewer in NativeExpeditionPreview.Party)
                    {
                        var v=p.View(viewer);Check(NativeExpeditionView.Leaks(v,snap).Count==0,"preview "+p.Current.Id+" leak-free for "+viewer);
                        Check(Json(Replicate(v))==Json(v),"preview view codec round trip");
                        var decoded=(ExpeditionView)Emptied(Replicate(v));Check(Says(decoded)==Says(v),"screens read a JsonUtility-style decoded view the same ("+p.Current.Id+"/"+viewer+")");
                        Check(Json(NativeExpeditionView.Normalize(decoded))==Json(v),"Normalize restores the host-built view");
                        P.Board(v);P.Final(v);P.Objective(v,out _);P.Loot(v);P.Roster(v);P.Extraction(v);P.Departure(v);P.GroupExtraction(v);P.Transfer(v,out _,out _);foreach(var d in v.dice)P.Share(v,d);
                        Check(Json(v).Length<64*1024,"preview view stays small");
                    }
                    p.Tick(.5);
                }
                while(p.Next()&&++guard<32);
                Check(p.Finished,"preview "+p.Current.Id+" plays to the end");
            }
        }

        // 10. Source scans: palette-only HUD, no mutators or randomness in presentation, no player-facing menu entry.
        private static void Sources(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            string hud=R("Runtime/NativeExpeditionHud.cs"),ui=R("Runtime/NativeExpeditionPreviewUI.cs"),rules=R("Core/NativeExpeditionPresentation.cs"),view=R("Core/NativeExpeditionView.cs");
            string screen=R("Runtime/NativeExpeditionPreviewScreen.cs");
            foreach(var (name,src) in new[]{("HUD",hud),("preview screen",screen)})
            {
                Check(!Regex.IsMatch(src,@"0x[0-9a-fA-F]{6}")&&src.Contains("NativeCyberTheme"),name+" restates no colour (palette only)");
                foreach(var statement in src.Split(';'))if(statement.Contains("Text("))foreach(var tone in new[]{"Violet","Dim","Edge","CyanDeep"})Check(!statement.Contains("C."+tone+"(")&&!statement.Contains("NativeCyberTheme."+tone+"("),"no "+tone+" text in the Expedition "+name);
            }
            // The screens hold no authority reference at all; the view builder only reads it.
            foreach(var (name,src) in new[]{("HUD",hud),("rules",rules)})Check(!Regex.IsMatch(src,@"\bNativeExpedition\b"),name+" holds no authority reference");
            var reads=new HashSet<string>{"Snapshot","Migrating","FinalUnlocked","ExtractionUnlocked","CanExtract"};
            foreach(Match m in Regex.Matches(view,@"\brun\.(\w+)"))Check(reads.Contains(m.Groups[1].Value),"the view builder only reads the authority (run."+m.Groups[1].Value+")");
            foreach(var src in new[]{hud,rules,view,screen})Check(!src.Contains("Random")&&!src.Contains("NativePveRandom"),"no randomness in presentation");
            Check(!R("Runtime/NativeHomeMenu.cs").Contains("Expedition")&&ui.Contains("--ca-expedition-preview"),"no player-facing Expedition menu entry; preview behind its dev flag");
        }
    }
}
#endif
