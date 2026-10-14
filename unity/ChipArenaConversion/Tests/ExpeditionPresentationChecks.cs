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
            var k=P.Continue(v);b.Append(k.detail).Append(k.votes).Append(k.remaining).Append(k.voted).Append(k.canVote);foreach(var r in P.ContinueRows(v))b.Append(r.status).Append(r.you).Append('|');
            foreach(var r in P.Caches(v))b.Append(r.state).Append(r.detail).Append(r.bought).Append('|');foreach(var r in P.History(v))b.Append(r.chip).Append(r.from).Append('|');
            foreach(var r in P.Candidates(v))b.Append(r.rtt).Append(r.timeouts).Append(r.probe).Append(r.support).Append(r.elected).Append('|');
            foreach(var m in v.members)b.Append(P.Quote(m)).Append('|');
            var w=new NativeExpeditionWorld();w.Build(v);foreach(var pc in w.Pieces)b.Append(pc.kind).Append(pc.look).Append(pc.label).Append(pc.detail).Append(pc.extra).Append(pc.x).Append(pc.z).Append(pc.mine).Append('|');
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
            Privacy();Commands();Eligibility();Dice();Selections();Extraction();Transfer();Board();
            GuestChannel(root);GuestPreview();Continue();Caches();Quotes();World();FinalDice();NoAllocation();Previews();Sources(root);
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
            Check(p.View("astra").phase==ExpeditionPhase.FinalBoss&&p.Next()&&p.Next(),"kestrel down, then the world boss falls");
            var v=p.View("astra");Check(v.phase==ExpeditionPhase.Victory&&P.Extraction(v).can&&P.Objective(v,out _).StartsWith("VICTORY"),"victory offers extraction");
            var host=p.View("sheldon");var hx=P.Extraction(host);
            Check(host.mine.canExtract&&hx.hostNeedsTransfer&&hx.detail==P.HostCannotLeave&&hx.button.Contains("needs host transfer"),"the host's extraction is honestly refused while others stay");
            Check(p.Commands("sheldon").Blocked(new ExpeditionCommand(ExpeditionIntent.Extract))==ExpeditionIntegrationHooks.HostTransfer,"the host's extract control carries the refusal reason");
            Check(p.Next(),"astra extracts");v=p.View("astra");
            Check(v.mine.extracted&&v.mine.extractionSaved&&writes.Last().extracted.Contains("astra"),"astra extracted after a durable write that holds her tombstone");
            Check(p.Intents.State(new ExpeditionCommand(ExpeditionIntent.Extract),v,p.Clock).state==ExpeditionCommandState.Confirmed,"astra's extraction confirmed by the saved state");
            var nova=p.View("nova");Check(!nova.mine.extracted&&P.Roster(nova).First(r=>r.account=="astra").status=="EXTRACTED"&&P.Roster(nova).First(r=>r.you).status.StartsWith("IN RUN"),"nova continues and sees astra extracted");
            Check(p.Next(),"nova and kestrel continue");nova=p.View("nova");var ready=P.Continue(nova);
            Check(ready.votes==2&&ready.remaining==3&&ready.living==2&&!ready.allAgreed&&ready.voted&&nova.loop==0,"two of the three remaining ready: no loop yet");
            var kestrel=P.Continue(p.View("kestrel"));Check(kestrel.down&&kestrel.voted&&kestrel.canVote&&p.View("kestrel").continueVotes.Contains("kestrel"),"a downed pilot consents");
            var rows=P.ContinueRows(nova);Check(rows.First(r=>r.account=="kestrel").status=="CONTINUE · DOWN"&&rows.First(r=>r.account=="sheldon").status=="DECIDING"&&rows.First(r=>r.account=="astra").status=="EXTRACTED · NOT COUNTED","each pilot's choice");
            Check(p.Intents.State(new ExpeditionCommand(ExpeditionIntent.VoteContinue),p.View("nova"),p.Clock).state==ExpeditionCommandState.Confirmed,"nova's continue confirmed by the view");
            Check(p.Next(),"next loop");nova=p.View("nova");
            Check(nova.loop==1&&nova.phase==ExpeditionPhase.Town&&nova.instances.All(i=>!i.cleared&&i.progress==0)&&P.Final(nova).state=="LOCKED","loop 2 resets the board for those who stayed");
            Check(nova.difficulty.health>new ExpeditionScaling().Evaluate(nova.elapsed,nova.difficultyParty,0,false).health,"the harder loop raises the scaling read-out");
            Check(p.View("astra").mine.extracted&&p.View("astra").mine.extractionSaved,"extracted pilots stay extracted into the next loop");
            Check(p.Intents.State(new ExpeditionCommand(ExpeditionIntent.VoteContinue),p.View("sheldon"),p.Clock).state==ExpeditionCommandState.Confirmed&&nova.continueVotes.Length==0,"the host's continue confirmed by the loop advance; votes reset");
            Check(!p.Run.Join("astra","rifle"),"an extracted account cannot rejoin");
            // Town: one leaves alone, the group vote needs a strict majority, a failed save keeps the pilot in the run.
            p.Load(Array.FindIndex(NativeExpeditionPreview.Scenarios,s=>s.Id=="extract"));writes.Clear();
            Check(p.Next()&&p.View("astra").mine.extractionSaved&&p.View("sheldon").living==3,"individual extraction in town");
            Check(p.Next(),"two votes");var g=P.GroupExtraction(p.View("kestrel"));Check(g.active&&g.votes==2&&g.needed==2&&g.seconds==10&&!g.voted,"strict majority (2 of 3) starts the group countdown");
            Check(P.Active(p.View("kestrel")).label=="GROUP EXTRACTION","HUD shows the group countdown");
            Check(p.Next(),"cancel");Check(!P.GroupExtraction(p.View("kestrel")).active&&p.View("kestrel").extractAt<0,"below the majority it cancels");
            int saved=writes.Count;Check(p.Next(),"failed save");var k=p.View("kestrel");var fail=p.Intents.State(new ExpeditionCommand(ExpeditionIntent.Extract),k,p.Clock);
            Check(!k.mine.extracted&&fail.state==ExpeditionCommandState.Failed&&fail.reason==NativeExpeditionPreview.WriteFailure&&writes.Count==saved,"a failed save is reported and the pilot stays in the run");
            // Reprints: the machine's quote, none for Kestrel (price unavailable, never $0), the host pays the quote.
            Check(p.Next(),"nova down");var sv=p.View("sheldon");Check(sv.Member("nova").reprintCost==90&&P.Quote(sv.Member("nova"))=="$90","the machine quotes a remote recovery");
            Check(p.Next(),"kestrel down without a quote");sv=p.View("sheldon");Check(sv.Member("kestrel").reprintCost==0&&!P.HasQuote(sv.Member("kestrel"))&&P.Quote(sv.Member("kestrel"))=="price unavailable","no quote reads price unavailable");
            int wallet=sv.mine.wallet;Check(p.Next(),"sheldon reprints nova");sv=p.View("sheldon");
            Check(sv.Member("nova").alive&&sv.mine.wallet==wallet-90&&p.Intents.State(new ExpeditionCommand(ExpeditionIntent.Reprint,"nova"),sv,p.Clock).state==ExpeditionCommandState.Confirmed,"the reprint is paid at the quote and confirmed by the view");
        }

        // 7. Host transfer: frozen, aborted (failure shown, nobody extracted), refused, and a commit only when the authority shows it.
        private static void Transfer()
        {
            var p=new NativeExpeditionPreview();p.Load(Array.FindIndex(NativeExpeditionPreview.Scenarios,s=>s.Id=="transfer"));
            // Today's transport: measured links, nothing probed or supported, so the request is refused with the reason.
            Check(p.Next(),"refused today");var v=p.View("nova");var steps=P.Transfer(v,out string head,out var tone);
            Check(!v.migrating&&v.transfer.stage==ExpeditionTransferStage.Refused&&head=="HOST TRANSFER REFUSED"&&tone==ExpeditionTone.Danger&&steps.Single().detail.Contains("not supported yet"),"an unprobed transport refuses the transfer, with the reason");
            var cands=P.Candidates(v);Check(cands.Length==2&&cands.All(c=>!c.ready&&c.probe=="NOT PROBED"&&c.support=="NOT SUPPORTED YET"&&c.tone==ExpeditionTone.Warn),"reachable and capable false read as not probed / not supported yet");
            var astra=cands.First(c=>c.account=="astra");Check(astra.rtt=="RTT 38 MS"&&astra.jitter=="JITTER 4 MS"&&astra.timeouts=="PING TIMEOUTS 0%"&&cands.First(c=>c.account=="nova").timeouts=="PING TIMEOUTS 2%","measured RTT, jitter and ping timeouts shown as reported");
            Check(!p.View("sheldon").mine.extracted&&p.View("sheldon").host=="sheldon","the refusal extracts nobody");
            Check(p.Next(),"prepare (simulated probes)");v=p.View("nova");
            Check(v.migrating&&v.transfer.stage==ExpeditionTransferStage.Frozen&&v.transfer.nextHost=="astra"&&P.Objective(v,out _).StartsWith("HOST TRANSFER"),"frozen with the elected candidate");
            steps=P.Transfer(v,out head,out tone);
            Check(head=="TRANSFERRING HOST TO ASTRA"&&steps[0].state==ExpeditionStep.Done&&steps[1].state==ExpeditionStep.Done&&steps.Skip(2).Take(3).All(st=>st.state==ExpeditionStep.Active),"supported transport steps run, none done before it reports");
            Check(v.transfer.candidates.Length==2&&v.transfer.candidates.Any(c=>c.account=="nova"&&c.latencyMs==21),"transport candidates shown as reported");
            Check(p.Next(),"simulated progress");steps=P.Transfer(p.View("nova"),out head,out tone);
            Check(steps[2].state==ExpeditionStep.Done&&steps[3].state==ExpeditionStep.Done&&steps[4].state==ExpeditionStep.Active&&steps[4].detail.Contains("deadline set"),"reserved and stored only as reported; the ACK still waits");
            Check(p.Next(),"abort");v=p.View("nova");steps=P.Transfer(v,out head,out tone);
            Check(!v.migrating&&v.transfer.stage==ExpeditionTransferStage.Aborted&&head=="HOST TRANSFER FAILED"&&tone==ExpeditionTone.Danger&&steps.Last().state==ExpeditionStep.Failed,"abort shown as a failure");
            Check(v.host=="sheldon"&&!p.View("sheldon").mine.extracted&&steps.Last().detail.Contains("No destination ACK"),"abort keeps the host, nobody extracted, reason shown");
            Check(p.Next(),"simulated commit");v=p.View("nova");steps=P.Transfer(v,out head,out tone);
            Check(v.host=="astra"&&v.transfer.stage==ExpeditionTransferStage.Committed&&head=="HOST IS NOW ASTRA"&&tone==ExpeditionTone.Warn&&steps[2].state==ExpeditionStep.Unavailable&&steps[3].state==ExpeditionStep.Unavailable&&steps[4].state==ExpeditionStep.Unavailable,"committed rules shown, endpoint still unavailable");
            Check(p.View("sheldon").mine.extracted&&p.View("sheldon").mine.extractionSaved,"the leaving host is extracted with a saved tombstone");
            // A log that claims a commit the authority does not show is reported as a failure.
            var run=Party(2);Quota(run,"ward-zero","b");Check(run.DefeatBoss("b")&&run.VoteExit("p0",true)&&run.VoteExit("p1",true),"clear");run.Tick(.25);Check(run.SetSafe("p0",true)&&run.SetSafe("p1",true),"safe");
            var log=new ExpeditionTransferLog();var h=run.PrepareHandoff("t1",new[]{"p0"},new[]{new ExpeditionHostCandidate{account="p1",reachable=true,capable=true}});log.Prepared(h);
            Check(run.AbortHandoff("t1"),"abort");log.Committed("t1");var w=NativeExpeditionView.Build(run,"p1",new ExpeditionViewSources{Transfer=log});
            Check(w.transfer.stage==ExpeditionTransferStage.Refused&&w.transfer.reason.Contains("not reflected"),"an unreflected commit is not shown as success");
            log.Prepared(run.PrepareHandoff("t2",new[]{"p0"},Array.Empty<ExpeditionHostCandidate>()));w=NativeExpeditionView.Build(run,"p1",new ExpeditionViewSources{Transfer=log});
            P.Transfer(w,out head,out tone);Check(w.transfer.stage==ExpeditionTransferStage.Refused&&head=="HOST TRANSFER REFUSED"&&tone==ExpeditionTone.Danger,"no eligible connection is a refusal");
            log.Clear();Check(NativeExpeditionView.Build(run,"p1",new ExpeditionViewSources{Transfer=log}).transfer.stage==ExpeditionTransferStage.None,"no transfer, no overlay");
            // Nothing fabricated: progress flags without endpoint migration never read as done; with it, only the reported ones.
            log.Prepared(run.PrepareHandoff("t3",new[]{"p0"},new[]{new ExpeditionHostCandidate{account="p1",reachable=true,capable=true}}));
            var bogus=NativeExpeditionView.Build(run,"p1",new ExpeditionViewSources{Transfer=log,PackageStored=true,EndpointReserved=true,AckReceived=true});
            Check(P.Transfer(bogus,out _,out _).Count(st=>st.state==ExpeditionStep.Done)==2,"without endpoint migration no transport step reads done");
            var half=NativeExpeditionView.Build(run,"p1",new ExpeditionViewSources{Transfer=log,EndpointMigration=true,EndpointReserved=true});var hs=P.Transfer(half,out _,out _);
            Check(hs[2].state==ExpeditionStep.Done&&hs[3].state==ExpeditionStep.Active&&hs[4].state==ExpeditionStep.Active&&hs[4].detail.Contains("no deadline reported"),"only the reported transport step is done");
            Check(run.AbortHandoff("t3"),"abort t3");
            var unmeasured=NativeExpeditionView.Build(run,"p1",new ExpeditionViewSources{Candidates=new[]{new ExpeditionHostCandidate{account="p1",loss=1}}});
            Check(P.Candidates(unmeasured).Single().rtt=="NO ANSWERED PINGS"&&P.Candidates(unmeasured).Single().timeouts=="PING TIMEOUTS 100%","a link with no answered ping is not shown as 0 ms");
            Check(P.Candidates(NativeExpeditionView.Build(run,"p1")).Length==0,"no transport report, no candidates");
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

        private static T Wire<T>(T x)=>JsonSerializer.Deserialize<T>(Json(x),Options);
        private static NativeExpedition AtVictory(int n,params string[] downBeforeTheEnd)
        {
            var run=Party(n);
            foreach(var area in new[]{"ward-zero","station"}){Quota(run,area,"boss-"+area);Check(run.DefeatBoss("boss-"+area),"boss");foreach(var m in run.Snapshot().members.Where(m=>m.connected&&m.alive))Check(run.VoteExit(m.account,true),"leave");run.Tick(.25);}
            Check(run.BeginFinal(),"final");foreach(var a in downBeforeTheEnd)Check(run.Defeat(a),"down");Check(run.FinishFinal(),"world boss");
            foreach(var m in run.Snapshot().members.Where(m=>m.connected&&m.alive&&!m.extracted))Check(run.SetSafe(m.account,true),"safe");
            Check(run.Snapshot().phase==ExpeditionPhase.Victory,"victory");return run;
        }

        // 11. Guests over the real reply path: ExpeditionGuestCommands -> wire -> ExpeditionCommandRouter (a real session) -> reply,
        // with views through ExpeditionViewBatch and the paired ExpeditionViewReceiver. Pending, refused at once, accepted (still
        // pending), confirmed only by the replicated view, failed on timeout; a reply never substitutes for the state.
        private static void GuestChannel(string root)
        {
            var data=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),Options);
            var run=Party(2);Check(run.Enter("ward-zero"),"enter");var session=new ExpeditionSession(run,data,_=>{});var router=new ExpeditionCommandRouter(session);
            var receiver=new ExpeditionViewReceiver("p1");
            void Publish(){var b=ExpeditionViewBatch.Build(session.Run,new[]{"p1"},session.Sources);receiver.Public(Wire(b.Public));Check(receiver.Private(Wire(b.Private[0])),"paired view published");}
            Publish();var outbox=new List<ExpeditionCommandRequest>();var guest=new ExpeditionGuestCommands("p1",()=>receiver.View,r=>outbox.Add(Wire(r)));var tracker=new ExpeditionIntentTracker();
            Check(guest.Blocked(new ExpeditionCommand(ExpeditionIntent.VoteExit))==null,"a guest with a channel can send");
            ExpeditionCommandReply Deliver(){var reply=router.Execute("p1",outbox.Last());guest.Reply(Wire(reply));return reply;}
            // Refused at once: a departure vote while the door is sealed.
            var vote=new ExpeditionCommand(ExpeditionIntent.VoteExit);var sent=tracker.Submit(guest,vote,receiver.View,0);
            Check(sent.state==ExpeditionCommandState.Pending&&sent.reason==ExpeditionGuestCommands.SentToHost&&outbox.Count==1&&outbox[0].run==run.Snapshot().run&&outbox[0].epoch==run.Snapshot().epoch,"sent with the run/epoch fence, pending");
            Check(tracker.State(vote,receiver.View,.1).state==ExpeditionCommandState.Pending,"pending before the reply");
            var refused=Deliver();Check(refused.state==ExpeditionCommandState.Refused,"the host refuses the sealed-door vote");
            var shown=tracker.State(vote,receiver.View,.2);Check(shown.state==ExpeditionCommandState.Refused&&shown.reason==refused.reason,"refused immediately from the reply, with its reason");
            P.Feedback(shown,out var tone);Check(tone==ExpeditionTone.Danger,"a refusal reads as a failure");
            // Accepted: still pending until the replicated view shows the vote.
            Check(run.Progress("k",3)&&run.StartBosses(new[]{"b"})&&run.DefeatBoss("b"),"clear");Publish();
            tracker.Submit(guest,vote,receiver.View,1);var accepted=Deliver();Check(accepted.state==ExpeditionCommandState.Pending,"the host accepts");
            var waiting=tracker.State(vote,receiver.View,1.1);Check(waiting.state==ExpeditionCommandState.Pending&&waiting.reason==ExpeditionGuestCommands.AcceptedByHost&&!receiver.View.exitVotes.Contains("p1"),"accepted is pending while the state does not show it");
            P.Feedback(waiting,out tone);Check(tone!=ExpeditionTone.Good,"an accepted reply never reads as success");
            Publish();Check(receiver.View.exitVotes.Contains("p1")&&tracker.State(vote,receiver.View,1.2).state==ExpeditionCommandState.Confirmed,"confirmed by the replicated view");
            // Lost: no reply and no state change times out as a failure.
            string reward=run.Snapshot().rewards.First(r=>r.owner=="p1").id;var claim=new ExpeditionCommand(ExpeditionIntent.Claim,reward);
            tracker.Submit(guest,claim,receiver.View,2);outbox.Clear();
            Check(tracker.State(claim,receiver.View,2+ExpeditionIntentTracker.Timeout-.1).state==ExpeditionCommandState.Pending,"pending until the timeout");
            var lost=tracker.State(claim,receiver.View,2+ExpeditionIntentTracker.Timeout+.1);Check(lost.state==ExpeditionCommandState.Failed&&lost.reason==ExpeditionIntentTracker.NoConfirmation,"failed on timeout");
            // Accepted but never reflected: the acceptance is not success; it fails at the timeout too.
            tracker.Submit(guest,claim,receiver.View,10);Check(Deliver().state==ExpeditionCommandState.Pending,"the claim is accepted");
            Check(tracker.State(claim,receiver.View,10.5).reason==ExpeditionGuestCommands.AcceptedByHost,"accepted caption");
            Check(tracker.State(claim,receiver.View,10+ExpeditionIntentTracker.Timeout+.1).state==ExpeditionCommandState.Failed,"an accepted command the state never shows fails");
            // A reply to a command that has since been replaced (same key, new transaction) no longer applies.
            var pick=new ExpeditionCommand(ExpeditionIntent.Claim,reward);tracker.Submit(guest,pick,receiver.View,20);var old=outbox.Last();
            tracker.Submit(guest,pick,receiver.View,20.1);guest.Reply(new ExpeditionCommandReply{transaction=old.transaction,state=ExpeditionCommandState.Refused,reason="old"});
            var current=tracker.State(pick,receiver.View,20.2);Check(current.state==ExpeditionCommandState.Pending&&current.reason==ExpeditionGuestCommands.SentToHost,"a reply to a replaced command is ignored");
            // A Confirmed reply is only an acceptance: the view decides.
            guest.Reply(new ExpeditionCommandReply{transaction=outbox.Last().transaction,state=ExpeditionCommandState.Confirmed});
            current=tracker.State(pick,receiver.View,20.3);Check(current.state==ExpeditionCommandState.Pending&&current.reason==ExpeditionGuestCommands.AcceptedByHost,"a confirmed reply cannot confirm what the view does not show");
            var first=router.Execute("p1",outbox.Last());var second=router.Execute("p1",outbox.Last());Check(first.state==second.state&&first.reason==second.reason,"a retransmitted transaction gets the original reply");
            // The claim accepted at 10 s did reach the authority: once replicated, the view (not any reply) confirms the latest claim.
            Publish();Check(receiver.View.Reward(reward).claimed&&tracker.State(pick,receiver.View,20.4).state==ExpeditionCommandState.Confirmed,"the replicated state confirms it");
        }

        // 12. The preview's guest scenario: the same sender and reply path over its loopback channel, with latency and replication.
        private static void GuestPreview()
        {
            var p=new NativeExpeditionPreview();p.Load(Array.FindIndex(NativeExpeditionPreview.Scenarios,s=>s.Id=="guest"));
            var vote=new ExpeditionCommand(ExpeditionIntent.VoteExit);
            Check(p.Next()&&p.Intents.State(vote,p.Shown("sheldon"),p.Clock).state==ExpeditionCommandState.Refused,"the host's own vote is refused at once");
            Check(p.Next()&&p.Viewer=="astra"&&!p.IsHost("astra"),"astra is a guest");
            Check(p.Intents.State(vote,p.Shown("astra"),p.Clock).state==ExpeditionCommandState.Pending&&p.GuestMessagesInFlight==1,"pending while the command travels");
            for(int i=0;i<4;i++)p.Tick(.25);
            var st=p.Intents.State(vote,p.Shown("astra"),p.Clock);Check(st.state==ExpeditionCommandState.Refused&&p.Clock<ExpeditionIntentTracker.Timeout,"the host's reply refuses it, long before any timeout");
            Check(p.Next(),"astra votes after the boss");Check(p.Intents.State(vote,p.Shown("astra"),p.Clock).state==ExpeditionCommandState.Pending,"pending");
            bool sawAccepted=false;ExpeditionCommandResult now=default;
            for(int i=0;i<40&&now.state!=ExpeditionCommandState.Confirmed;i++){p.Tick(.05);now=p.Intents.State(vote,p.Shown("astra"),p.Clock);sawAccepted|=now.state==ExpeditionCommandState.Pending&&now.reason==ExpeditionGuestCommands.AcceptedByHost;}
            Check(now.state==ExpeditionCommandState.Confirmed&&p.Shown("astra").exitVotes.Contains("astra"),"confirmed once the replicated view shows the vote");
            Check(sawAccepted,"accepted by the host before the replicated view showed it");
            Check(p.Next(),"lost message");var claim=new ExpeditionCommand(ExpeditionIntent.Claim,p.Shown("astra").mine.rewards.First(r=>r.source=="boss").id);
            double sentAt=p.Clock;for(int i=0;i<10;i++)p.Tick(.25);Check(p.Intents.State(claim,p.Shown("astra"),p.Clock).state==ExpeditionCommandState.Pending&&p.GuestMessagesInFlight==0,"no reply: still pending");
            while(p.Clock-sentAt<=ExpeditionIntentTracker.Timeout+.1)p.Tick(.25);
            Check(p.Intents.State(claim,p.Shown("astra"),p.Clock).state==ExpeditionCommandState.Failed,"a lost command fails on the timeout");
            Check(!p.Run.Snapshot().rewards.First(r=>r.id==claim.target).claimed,"and the authority never saw it");
        }

        // 13. Continue votes: downed pilots consent, everyone remaining must agree, one pilot must be alive; votes replace each other.
        private static void Continue()
        {
            var run=AtVictory(3,"p2");var v=NativeExpeditionView.Build(run,"p0");var k=P.Continue(v);
            Check(k.open&&k.remaining==3&&k.living==2&&k.votes==0&&k.canVote&&!k.allAgreed,"three remaining, two alive, nobody ready");
            Check(new ExpeditionHostCommands(run,"p2").Send(new ExpeditionCommand(ExpeditionIntent.VoteContinue)).state==ExpeditionCommandState.Pending,"the authority takes a downed pilot's consent");
            var down=P.Continue(NativeExpeditionView.Build(run,"p2"));Check(down.down&&down.voted&&down.canVote&&down.button=="Cancel continue","a downed pilot can agree");
            Check(P.ContinueRows(v=NativeExpeditionView.Build(run,"p0")).First(r=>r.account=="p2").status=="CONTINUE · DOWN","shown as continuing while down");
            // Continue and the group-extraction vote replace each other; a withdrawn confirmed vote no longer reads confirmed.
            Check(run.VoteExtraction("p1",true),"p1 votes to extract");var tracker=new ExpeditionIntentTracker();var cmd=new ExpeditionCommand(ExpeditionIntent.VoteExtraction);
            tracker.Record("p1",cmd,ExpeditionCommandResult.Sent,NativeExpeditionView.Build(run,"p1"),0);Check(tracker.State(cmd,NativeExpeditionView.Build(run,"p1"),.1).state==ExpeditionCommandState.Confirmed,"extraction vote confirmed");
            Check(P.ContinueRows(NativeExpeditionView.Build(run,"p0")).First(r=>r.account=="p1").status=="VOTED TO EXTRACT","shown voting to extract");
            Check(run.VoteContinue("p1",true),"p1 chooses continue");var p1=NativeExpeditionView.Build(run,"p1");
            Check(!p1.extractVotes.Contains("p1")&&p1.continueVotes.Contains("p1")&&!P.VoteHeld(ExpeditionIntent.VoteExtraction,p1)&&P.Withdrawn(ExpeditionIntent.VoteExtraction,p1)=="Replaced by your Continue choice.","continue replaced the extraction vote, and says so");
            Check(run.Snapshot().loop==0&&P.Continue(p1).votes==2&&!P.Continue(p1).allAgreed,"not everyone yet: no loop");
            Check(run.VoteExtraction("p1",true)&&!NativeExpeditionView.Build(run,"p1").continueVotes.Contains("p1")&&P.Withdrawn(ExpeditionIntent.VoteContinue,NativeExpeditionView.Build(run,"p1"))=="Replaced by your extraction vote.","and the other way round");
            Check(run.VoteContinue("p1",true)&&run.VoteContinue("p0",true),"everyone remaining agrees");
            Check(P.Continue(NativeExpeditionView.Build(run,"p0")).allAgreed&&run.AdvanceWhenReady()&&run.Snapshot().loop==1,"all agreed with pilots alive: the authority starts the loop");
            // Extracted and offline pilots are not counted; a disconnect removes the vote.
            run=AtVictory(4);Check(run.Extract("p3")&&run.VoteContinue("p1",true)&&run.Disconnect("p1"),"p3 extracts, p1 votes then drops");
            v=NativeExpeditionView.Build(run,"p0");k=P.Continue(v);var rows=P.ContinueRows(v);
            Check(k.remaining==2&&k.votes==0&&rows.First(r=>r.account=="p3").status=="EXTRACTED · NOT COUNTED"&&rows.First(r=>r.account=="p1").status=="OFFLINE · NOT COUNTED","extracted and offline pilots are not counted; the drop removed the vote");
            var x=P.Continue(NativeExpeditionView.Build(run,"p3"));Check(x.extracted&&!x.canVote,"an extracted pilot has no continue control");
            // Everyone remaining agreed but nobody is alive: it waits and says why.
            run=AtVictory(2);Check(run.Defeat("p0")&&run.Defeat("p1")&&run.VoteContinue("p0",true)&&run.VoteContinue("p1",true),"both down, both agree");
            k=P.Continue(NativeExpeditionView.Build(run,"p0"));Check(k.allAgreed&&k.needsLiving&&k.detail.Contains("at least one pilot must be alive")&&!run.AdvanceWhenReady()&&run.Snapshot().loop==0,"no living pilot: no loop, and the reason");
            Check(!P.Continue(NativeExpeditionView.Build(Party(2),"p0")).open,"no continue outside victory");
        }

        // 14. Chip caches: generation, price and cooldown against elapsed before any purchase; restocking only from the data; never range.
        private static void Caches()
        {
            var run=Party(2);Check(run.Credit("p0","cash",300),"credit");
            Check(run.SetWorld(new[]{new ExpeditionWorldAnchor{id="c1",area="city",kind="chest",x=2},new ExpeditionWorldAnchor{id="c2",area="city",kind="chest",x=-2},new ExpeditionWorldAnchor{id="c3",area="station",kind="chest"}},
                new[]{new ExpeditionChestOffer{id="c1",generation="g1",price=100},new ExpeditionChestOffer{id="c2",generation="g7",price=400,cooldown=30},new ExpeditionChestOffer{id="c3",generation="s1",price=50}}),"world");
            var rows=P.Caches(NativeExpeditionView.Build(run,"p0"));
            Check(rows[0].state=="$100"&&rows[0].stock=="STOCK G1"&&!rows[0].restocking&&rows[0].affordable&&rows[0].here,"an offer shows price and stock");
            Check(rows[1].restocking&&rows[1].seconds==30&&rows[1].state=="RESTOCKING · 30s","a cooldown ahead of the clock reads restocking");
            Check(!rows[2].here&&rows[2].detail.EndsWith("In another area."),"a cache in another area says so");
            for(int i=0;i<40;i++)run.Tick(.25);rows=P.Caches(NativeExpeditionView.Build(run,"p0"));Check(rows[1].seconds==20&&rows[1].state=="RESTOCKING · 20s","the cooldown counts down with the authority clock");
            for(int i=0;i<80;i++)run.Tick(.25);rows=P.Caches(NativeExpeditionView.Build(run,"p0"));Check(!rows[1].restocking&&rows[1].state=="$400"&&!rows[1].affordable&&rows[1].detail.Contains("$100 short"),"restocked: the price, and the wallet's shortfall");
            Check(run.Purchase("p0","g1",100),"p0 buys c1");
            var mine=P.Caches(NativeExpeditionView.Build(run,"p0"));var theirs=P.Caches(NativeExpeditionView.Build(run,"p1"));
            Check(mine[0].bought&&mine[0].state=="BOUGHT · YOURS"&&!theirs[0].bought&&theirs[0].state=="$100","the buyer sees the purchase; it is private to them");
            Check(!Json(NativeExpeditionView.Build(run,"p1")).Contains(":chest:g1"),"another pilot's purchase never reaches the view");
            foreach(var r in mine.Concat(theirs))Check(!(r.state+r.detail).ToUpperInvariant().Contains("RANGE"),"purchase range is never claimed");
            var w=new NativeExpeditionWorld();w.Build(NativeExpeditionView.Build(run,"p0"));var pieces=w.Pieces.Where(x=>x.kind==ExpeditionPieceKind.Chest).ToArray();
            Check(pieces.Length==2&&pieces.First(x=>x.target=="c1").label=="BOUGHT · YOURS"&&pieces.First(x=>x.target=="c2").label=="$400"&&pieces.First(x=>x.target=="c2").extra=="$200 SHORT","cache pieces at their anchors, from the offer (wallet 200 after the purchase)");
            w.Build(NativeExpeditionView.Build(run,"p1"));Check(w.Pieces.First(x=>x.target=="c1").label=="$100","others see the cache unsold (purchases are private)");
            Check(run.SetWorld(run.Snapshot().anchors,new[]{new ExpeditionChestOffer{id="c1",generation="g2",price=120,cooldown=run.Snapshot().elapsed+15},new ExpeditionChestOffer{id="c2",generation="g7",price=400},new ExpeditionChestOffer{id="c3",generation="s1",price=50}}),"the scene restocks c1");
            w.Build(NativeExpeditionView.Build(run,"p0"));var c1=w.Pieces.First(x=>x.target=="c1");Check(c1.label=="RESTOCKING"&&c1.detail=="15s"&&c1.extra=="STOCK G2"&&c1.settled,"restocking piece from the cooldown");
            Check(P.NoPurchase.Contains("not connected"),"buying is not offered as a control");
        }

        // 15. Reprint quotes: Sources.ReprintCost per downed pilot; none (or below 1) is "price unavailable", never $0.
        private static void Quotes()
        {
            var run=Party(3);Check(run.Defeat("p1")&&run.Defeat("p2"),"two down");
            foreach(var src in new[]{new ExpeditionViewSources(),new ExpeditionViewSources{ReprintCost=a=>a=="p1"?0:75},new ExpeditionViewSources{ReprintCost=a=>a=="p1"?-5:75}})
            {
                var v=NativeExpeditionView.Build(run,"p0",src);var m=v.Member("p1");Check(m.reprintCost==0&&!P.HasQuote(m)&&P.Quote(m)=="price unavailable","no quote is price unavailable");
                var w=new NativeExpeditionWorld();Check(run.SetWorld(new[]{new ExpeditionWorldAnchor{id="m",area="city",kind="machine",x=1}},Array.Empty<ExpeditionChestOffer>()),"machine");w.Build(NativeExpeditionView.Build(run,"p0",src));
                var piece=w.Pieces.Single();Check(piece.kind==ExpeditionPieceKind.Machine&&piece.detail=="P1 · PRICE UNAVAILABLE"&&piece.extra=="+1 MORE DOWN","the machine piece shows the first downed pilot's quote");
                foreach(var mm in v.members)Check(P.Quote(mm)!="$0","never $0");
            }
            var q=NativeExpeditionView.Build(run,"p0",new ExpeditionViewSources{ReprintCost=a=>75});Check(P.Quote(q.Member("p2"))=="$75"&&q.Member("p0").reprintCost==0,"a quote shows; the living have none");
        }

        // 16. World pieces only at real anchors, with the look per reward kind and the viewer's eligibility; claiming is never local.
        private static void World()
        {
            var run=Party(3);var w=new NativeExpeditionWorld();
            Check(w.Build(NativeExpeditionView.Build(run,"p0"))==0&&w.Build(null)==0,"no anchors, nothing drawn");
            Check(run.SetWorld(new[]{new ExpeditionWorldAnchor{id="ward-zero",area="city",kind="door",x=-10,z=8},new ExpeditionWorldAnchor{id="station",area="city",kind="door",x=10,z=8},
                new ExpeditionWorldAnchor{id="mystery",area="city",kind="door",x=0,z=20},new ExpeditionWorldAnchor{id="wz-gate",area="ward-zero",kind="door",z=-6},
                new ExpeditionWorldAnchor{id="m1",area="city",kind="machine",x=3,z=-2}},Array.Empty<ExpeditionChestOffer>()),"anchors");
            w.Build(NativeExpeditionView.Build(run,"p0"));
            Check(w.Pieces.Count==3&&!w.Pieces.Any(x=>x.anchor=="mystery"||x.anchor=="wz-gate"),"only this area's anchors, and a door with no building is not drawn");
            var door=w.Pieces.First(x=>x.anchor=="ward-zero");Check(door.kind==ExpeditionPieceKind.Door&&door.look==ExpeditionPieceLook.DoorOpen&&door.label=="ENTER TOGETHER"&&door.detail=="QUOTA 3"&&door.x==-10&&door.z==8,"an entrance, open to enter, with its quota");
            Check(run.Enter("ward-zero"),"enter");w.Build(NativeExpeditionView.Build(run,"p0"));var gate=w.Pieces.Single();
            Check(gate.anchor=="wz-gate"&&gate.look==ExpeditionPieceLook.DoorSealed&&gate.label=="SEALED"&&gate.detail=="QUOTA 0 / 3","inside: the sealed door with the quota");
            Check(run.Progress("k",3)&&run.Drop("no-position"),"quota met, a drop with no position");w.Build(NativeExpeditionView.Build(run,"p0"));
            Check(!w.Pieces.Any(x=>x.kind==ExpeditionPieceKind.Pickup)&&NativeExpeditionView.Build(run,"p0").loot.Length==1,"a reward without an anchor is not drawn (no guessed origin)");
            Check(run.DropAt("w1",2,0,3),"drop at");w.Build(NativeExpeditionView.Build(run,"p1"));var drop=w.Pieces.Single(x=>x.kind==ExpeditionPieceKind.Pickup);
            Check(drop.look==ExpeditionPieceLook.Ordinary&&drop.label=="FIRST PICKUP"&&drop.tone==ExpeditionTone.Cyan&&drop.actionable&&drop.x==2&&drop.y==0&&drop.z==3,"an ordinary drop: cyan diamond, first pickup, exactly at its anchor");
            Check(run.Claim("p1",drop.target,new[]{"x"}),"p1 takes it");
            w.Build(NativeExpeditionView.Build(run,"p1"));Check(w.Pieces.Single(x=>x.target==drop.target).label=="YOURS","yours for the taker");
            w.Build(NativeExpeditionView.Build(run,"p0"));var taken=w.Pieces.Single(x=>x.target==drop.target);Check(taken.label=="TAKEN"&&taken.detail=="BY P1"&&taken.settled,"taken for the others");
            Check(run.StartBosses(new[]{"b"})&&run.Defeat("p2")&&run.DefeatBossAt("b",5,0,5),"p2 down, then the boss falls");
            w.Build(NativeExpeditionView.Build(run,"p0"));var reserved=w.Pieces.Where(x=>x.look==ExpeditionPieceLook.Reserved).ToArray();
            Check(reserved.Length==2&&reserved.All(x=>x.ax==5&&x.az==5)&&reserved[0].x!=reserved[1].x,"two reserved chips round the boss's death point");
            foreach(var x in reserved)Check(Math.Sqrt((x.x-5)*(x.x-5)+(x.z-5)*(x.z-5))<.5,"near their anchor");
            Check(reserved.Count(x=>x.label=="YOURS"&&x.mine&&x.tone==ExpeditionTone.Magenta)==1&&reserved.Count(x=>x.label=="FOR P1"&&!x.mine)==1,"yours, and for another survivor");
            var lead=reserved.Single(x=>x.labelAtAnchor);Check(lead.mine&&lead.extra=="ALSO FOR P1"&&reserved.Count(x=>x.quiet)==1&&reserved.All(x=>x.group==2),"one label for the cluster, at its anchor: yours, and whom the rest are for");
            w.Build(NativeExpeditionView.Build(run,"p2"));var ne=w.Pieces.Where(x=>x.kind==ExpeditionPieceKind.Pickup&&x.target!=drop.target).ToArray();
            Check(ne.Length==2&&ne.All(x=>x.look==ExpeditionPieceLook.NotEligible&&x.label=="NOT ELIGIBLE"&&x.tone==ExpeditionTone.Danger)&&ne.Single(x=>x.labelAtAnchor).detail=="FOR P0, P1","not eligible for the pilot who was down, every owner named once");
            w.Build(NativeExpeditionView.Build(run,"stranger"));Check(!w.Pieces.Any(x=>x.label=="YOURS"||x.look==ExpeditionPieceLook.NotEligible),"a non-member sees reservations, never 'yours'");
            string mine=run.Snapshot().rewards.First(r=>r.source=="boss"&&r.owner=="p0").id;Check(run.Claim("p0",mine,new[]{"y"}),"p0 opens his");
            w.Build(NativeExpeditionView.Build(run,"p0"));Check(w.Pieces.Single(x=>x.target==mine).detail=="OPENED · PICK A CHIP","opened state");
            w.Build(NativeExpeditionView.Build(run,"p1"));Check(w.Pieces.Single(x=>x.target==mine).detail=="CLAIMED","claimed state for the others");
            Check(run.Select("p0",mine,"y",c=>true),"p0 picks");w.Build(NativeExpeditionView.Build(run,"p0"));Check(!w.Pieces.Any(x=>x.target==mine),"a picked reward is gone");
            w.Build(NativeExpeditionView.Build(run,"p0"));Check(w.Pieces.Single(x=>x.anchor=="wz-gate").label=="OPEN","cleared: the door opens");
            // A private reward's anchor is a leak the audit flags, and it is never drawn.
            Check(run.VoteExit("p0",true)&&run.VoteExit("p1",true),"leave");run.Tick(.25);Check(run.SetSafe("p0",true)&&run.Credit("p0","c",100)&&run.Purchase("p0","g",50),"purchase");
            string chest=run.Snapshot().rewards.First(r=>r.source=="chest").id;Check(run.PlaceReward(chest,1,0,1),"the authority accepts a position for it");
            var v=NativeExpeditionView.Build(run,"p1");Check(NativeExpeditionView.Leaks(v,run.Snapshot()).Any(l=>l.Contains("world anchor of private reward")),"the privacy audit flags it");
            w.Build(v);Check(!w.Pieces.Any(x=>x.target==chest||x.anchor==chest),"and the world never draws it");
            // Geometry: every piece emits valid triangles; actionable pieces carry the motes column; reduced effects are static.
            var sink=new Collect();foreach(var x in w.Pieces){int before=sink.Vertices;NativeExpeditionWorld.Emit(sink,x,1.3,false);Check(sink.Vertices>before&&sink.Valid,"geometry for "+x.anchor);}
            var act=new Collect();var settle=new Collect();var a1=w.Pieces.First();a1.actionable=true;a1.settled=false;NativeExpeditionWorld.Emit(act,a1,0,false);a1.actionable=false;a1.settled=true;NativeExpeditionWorld.Emit(settle,a1,0,false);
            Check(act.Motes>0&&settle.Motes==0,"only an actionable piece stands in a light column");
            var r0=new Collect{Keep=true};var r1=new Collect{Keep=true};NativeExpeditionWorld.Emit(r0,w.Pieces[0],0,true);NativeExpeditionWorld.Emit(r1,w.Pieces[0],5,true);Check(r0.Same(r1),"reduced effects: no spin or bob");
            var m0=new Collect{Keep=true};var m1=new Collect{Keep=true};var spinning=w.Pieces.First(x=>x.kind==ExpeditionPieceKind.Machine);NativeExpeditionWorld.Emit(m0,spinning,0,false);NativeExpeditionWorld.Emit(m1,spinning,1,false);Check(!m0.Same(m1),"full effects animate");
            var far=w.Pieces[0];Check(NativeExpeditionWorld.Visible(far,far.x,far.y,far.z+80)&&!NativeExpeditionWorld.Visible(far,far.x,far.y,far.z+91)&&NativeExpeditionWorld.Labelled(far,far.x,0,far.z+25)&&!NativeExpeditionWorld.Labelled(far,far.x,0,far.z+27),"distance culling and label distance");
        }
        private sealed class Collect:IExpeditionWorldSink
        {
            public int Vertices, Motes;public bool Valid=true, Keep;public readonly List<float> Points=new List<float>();
            public int Vertex(float x,float y,float z,float u,float v,float flat,ExpeditionTone tone,float level,float alpha)
            {if(float.IsNaN(x+y+z+level+alpha)||alpha<0||alpha>1||level<0||level>1.01f)Valid=false;if(flat<.5f)Motes++;if(Keep){Points.Add(x);Points.Add(y);Points.Add(z);}return Vertices++;}
            public void Triangle(int a,int b,int c){if(a<0||b<0||c<0||a>=Vertices||b>=Vertices||c>=Vertices)Valid=false;}
            public bool Same(Collect o)=>Points.Count==o.Points.Count&&Points.Zip(o.Points,(x,y)=>Math.Abs(x-y)<1e-5f).All(b=>b);
        }
        private sealed class Count:IExpeditionWorldSink{public int Vertices, Triangles;public int Vertex(float x,float y,float z,float u,float v,float flat,ExpeditionTone tone,float level,float alpha)=>Vertices++;public void Triangle(int a,int b,int c){Triangles++;}}

        // 17. Final-boss rewards through the same loot, dice and world screens: the display equals the stored record.
        private static void FinalDice()
        {
            foreach(var (players,drops,down) in new[]{(3,5,1),(4,-1,0),(2,1,0),(5,8,2)})
            {
                var run=Party(players);foreach(var area in new[]{"ward-zero","station"}){Quota(run,area,"b-"+area);Check(run.DefeatBoss("b-"+area),"boss");foreach(var m in run.Snapshot().members.Where(m=>m.alive))Check(run.VoteExit(m.account,true),"leave");run.Tick(.25);}
                Check(run.BeginFinal(),"final");for(int i=0;i<down;i++)Check(run.Defeat("p"+(players-1-i)),"down");Check(run.FinishFinalAt(1,0,2,drops),"world boss at the plaza");
                var record=run.Snapshot().dice.Single(d=>d.boss.EndsWith(":final:boss",StringComparison.Ordinal));var v=NativeExpeditionView.Build(run,"p0");var d=v.dice.Single(x=>x.boss==record.boss);
                Check(Json(d)==Json(record)&&P.Name(d.boss)==P.WorldBoss,"world-boss dice equal the stored record ("+players+"/"+drops+")");
                int survivors=players-down,total=drops<0?survivors:drops;var s=P.Share(v,d);
                Check(s.name==P.WorldBoss&&s.total==total&&s.survivors==survivors&&s.each==total/survivors&&s.extra==total%survivors,"world-boss share from the record");
                for(float t=0;t<P.Dice.Duration(d)+.5f;t+=.1f)for(int i=0;i<d.eligible.Length;i++){int face=P.Dice.Face(d,i,t);Check(face==-1||face==d.rolls[i],"stored roll only");}
                Check(P.Loot(v).Count(r=>r.boss==d.boss&&r.kind==P.LootKind.Reserved)==total,"world-boss chips in the loot list");
                var w=new NativeExpeditionWorld();w.Build(v);Check(w.Pieces.Count(x=>x.look==ExpeditionPieceLook.Reserved||x.look==ExpeditionPieceLook.NotEligible)==total&&w.Pieces.All(x=>x.ax==1&&x.az==2&&(x.labelAtAnchor||x.extra==P.WorldBoss))&&w.Pieces.Count(x=>x.labelAtAnchor)==(total>1?1:0),"world-boss chips stand at its death point, one label for them");
                if(down>0){var dv=NativeExpeditionView.Build(run,"p"+(players-1));Check(!P.Share(dv,dv.dice.Single(x=>x.boss==record.boss)).eligible&&P.Loot(dv).Any(r=>r.state==P.LootState.NotEligible&&r.boss==record.boss),"down at the end: not eligible");}
            }
        }

        // 18. No allocation per frame: building, culling and emitting a steady view's pieces (warmed with the identical pass).
        private static void NoAllocation()
        {
            var run=Party(4);Check(run.SetWorld(new[]{new ExpeditionWorldAnchor{id="ward-zero",area="city",kind="door",x=-8},new ExpeditionWorldAnchor{id="m",area="city",kind="machine",x=4},new ExpeditionWorldAnchor{id="c",area="city",kind="chest",x=6}},
                new[]{new ExpeditionChestOffer{id="c",generation="g1",price=90,cooldown=12}}),"world");
            Check(run.Defeat("p3"),"down");Quota(run,"ward-zero","b");Check(run.DropAt("w1",1,0,1)&&run.DropAt("w2",2,0,2)&&run.Claim("p1",run.Snapshot().rewards.First(r=>r.source=="monster").id,new[]{"x"})&&run.DefeatBossAt("b",0,0,6,7),"rewards");
            var views=new[]{NativeExpeditionView.Build(run,"p0"),NativeExpeditionView.Build(run,"p3"),NativeExpeditionView.Build(run,"stranger")};
            Check(run.VoteExit("p0",true)&&run.VoteExit("p1",true)&&run.VoteExit("p2",true),"leave");run.Tick(.25);
            var town=NativeExpeditionView.Build(run,"p0",new ExpeditionViewSources{ReprintCost=a=>60});views=views.Append(town).ToArray();
            var world=new NativeExpeditionWorld();var sink=new Count();
            void Pass(){foreach(var v in views){world.Build(v);for(int i=0;i<world.Pieces.Count;i++){var p=world.Pieces[i];if(NativeExpeditionWorld.Visible(p,0,1.7f,-4)){NativeExpeditionWorld.Emit(sink,p,1.25,false);NativeExpeditionWorld.Labelled(p,0,1.7f,-4);NativeExpeditionWorld.Bob(p,1.25,false);}}}}
            Pass();Pass();long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<60;i++)Pass();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(bytes==0,"world pieces allocate nothing per frame ("+bytes+" bytes over 60 passes)");Check(sink.Triangles>0&&world.Pieces.Count>0,"the measured pass drew pieces");
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
                        foreach(var h in P.History(v))Check(snap.rewards.First(r=>r.id==h.reward).owner==viewer,"pick history is the viewer's own");
                        var world=new NativeExpeditionWorld();world.Build(v);
                        foreach(var pc in world.Pieces)
                        {
                            Check(v.anchors.Any(a=>a.id==pc.anchor&&a.area==v.area&&a.x==pc.ax&&a.y==pc.ay&&a.z==pc.az),"every piece stands at a real anchor of this area ("+p.Current.Id+")");
                            if(pc.kind==ExpeditionPieceKind.Pickup){var r=snap.rewards.First(x=>x.id==pc.target);Check(r.source=="monster"||r.source=="boss","only public loot in the world");Check(!pc.mine||r.owner==viewer,"'yours' only for the owner");}
                        }
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
            string screen=R("Runtime/NativeExpeditionPreviewScreen.cs"),layout=R("Runtime/NativeExpeditionScreen.cs"),live=R("Runtime/NativeExpeditionLiveUI.cs"),world3d=R("Runtime/NativeExpeditionWorldPresentation.cs"),world=R("Core/NativeExpeditionWorld.cs");
            foreach(var (name,src) in new[]{("HUD",hud),("preview screen",screen),("screen layout",layout),("live UI",live),("world pieces",world3d)})
            {
                Check(!Regex.IsMatch(src,@"0x[0-9a-fA-F]{6}")&&src.Contains("NativeCyberTheme"),name+" restates no colour (palette only)");
                foreach(var statement in src.Split(';'))if(statement.Contains("Text("))foreach(var tone in new[]{"Violet","Dim","Edge","CyanDeep"})Check(!statement.Contains("C."+tone+"(")&&!statement.Contains("NativeCyberTheme."+tone+"("),"no "+tone+" text in the Expedition "+name);
            }
            // The screens hold no authority reference at all; the view builder only reads it.
            foreach(var (name,src) in new[]{("HUD",hud),("rules",rules),("screen layout",layout),("live UI",live),("world pieces",world3d),("world model",world)})Check(!Regex.IsMatch(src,@"\bNativeExpedition\b"),name+" holds no authority reference");
            // Presentation never mutates the run, rolls dice, allocates rewards or binds a session; the live UI only sends intents.
            foreach(var (name,src) in new[]{("HUD",hud),("screen layout",layout),("live UI",live),("world pieces",world3d),("world model",world),("rules",rules)})
            {
                Check(!Regex.IsMatch(src,@"\b(run|Run|session|Session|expeditionSession|authority)\s*\.\s*(Purchase|DefeatBoss|DefeatBossAt|FinishFinal|FinishFinalAt|Drop|DropAt|PlaceReward|SetWorld|Credit|Claim|Select|Reprint|Extract|VoteContinue|VoteExit|VoteExtraction|NextLoop|AdvanceWhenReady|IssueCatchup|PrepareHandoff|CommitHandoff)\("),name+" calls no authority mutator");
                Check(!src.Contains("BindExpedition(")&&!src.Contains("CreateExpeditionSession("),name+" binds no session");
            }
            Check(live.Contains("expeditionIntents.Submit(")&&live.Contains("ExpeditionCommands"),"the live UI sends through the bound command path");
            var reads=new HashSet<string>{"Snapshot","Migrating","FinalUnlocked","ExtractionUnlocked","CanExtract"};
            foreach(Match m in Regex.Matches(view,@"\brun\.(\w+)"))Check(reads.Contains(m.Groups[1].Value),"the view builder only reads the authority (run."+m.Groups[1].Value+")");
            foreach(var src in new[]{hud,rules,view,screen,layout,live,world3d,world})Check(!src.Contains("Random")&&!src.Contains("NativePveRandom"),"no randomness in presentation");
            Check(!R("Runtime/NativeHomeMenu.cs").Contains("Expedition")&&ui.Contains("--ca-expedition-preview"),"no player-facing Expedition menu entry; preview behind its dev flag");
        }
    }
}
#endif
