#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChipArena.UnityConversion
{
    // The Expedition chip pick on the Boss Test's chip screen (Core/NativeExpeditionPick.cs, Runtime/NativeExpeditionChipScreen.cs;
    // docs/EXPEDITION-PRESENTATION.md "Chip picks"), against the real NativeExpedition authority, the view builder, the host command
    // path and the intent tracker: the screen opens for a reward claimed with offers and never for a consumed one, rewards already
    // waiting never pop it, a pick sends Select with the shown reward and the chip, it is confirmed only by the view and chains to the
    // next reward as the Boss Test's queue does, a refusal returns to the tray with its caption, an unopened reserved reward is opened
    // with a Claim only when the screen can show, the "N more waiting" and CHIPS counts are the viewer's own, and source scans keep the
    // Boss Test path's lines, the shared chrome and the presentation-only wiring.
    internal static class ExpeditionPickChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Expedition pick: "+what);count++;}
        private static readonly string[] Offers={"vital","ricochet","piercing"};

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
            public ExpeditionView View()=>NativeExpeditionView.Build(Run,Viewer);
            public ExpeditionPickAction Step(bool canShow=true)=>Flow.Step(View(),c=>Tracker.State(c,View(),Now),c=>Tracker.Last(Viewer,c),canShow);
            public ExpeditionCommandResult Send(ExpeditionCommand c)=>Tracker.Submit(Host,c,View(),Now);
            public string Chest(string owner,string generation,int price=100){Check(Run.Credit(owner,"seed-"+generation,price)&&Run.Purchase(owner,generation,price),"purchase "+generation);return Run.Snapshot().rewards.Last(r=>r.owner==owner&&r.source=="chest").id;}
            // The scene's own claim (NativeExpeditionM1Arena: F at a cache, a Ward machine or a drop claims it directly).
            public void Claim(string owner,string reward)=>Check(Run.Claim(owner,reward,Offers),"claim "+reward);
        }

        public static void Run(string root)
        {
            count=0;
            Model();Opens();Picks();Refusals();Opening();Waiting();Sources(root);
            Console.WriteLine("PASS Expedition pick screen ("+count+" checks: opens for a claimed reward with offers and never a consumed one, Select with the shown reward and chip, view-confirmed and chained like the Boss Test's queue, refusals back on the tray, Claim-to-open only when shown, the viewer's own waiting counts, the Boss Test path and shared chrome unchanged)");
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
            Check(new ExpeditionPickFlow().Phase(null)==ExpeditionPickPhase.Closed&&new ExpeditionPickFlow().Choosing(null)==null,"no view: closed");
        }

        // 2. When it opens: a reward claimed with offers opens it; a consumed one, an unopened one and rewards already waiting do not.
        private static void Opens()
        {
            var g=new Rig();string chest=g.Chest("p0","cache-a");
            g.Step();Check(!g.Flow.Open&&g.Flow.Phase(g.View())==ExpeditionPickPhase.Closed,"an unopened purchase does not pop the screen");
            g.Claim("p0",chest);g.Step();var v=g.View();
            Check(g.Flow.Open&&g.Flow.Phase(v)==ExpeditionPickPhase.Choosing&&g.Flow.Choosing(v)?.id==chest,"claiming it with offers opens it on that reward");
            Check(g.Flow.Choosing(v).offers.SequenceEqual(Offers),"the tray's offers are the view's");
            g.Step();Check(g.Flow.Choosing(g.View())?.id==chest,"it stays open across frames");
            g.Flow.Later();g.Step();Check(!g.Flow.Open&&g.Flow.Choosing(g.View())==null,"Choose later closes it on this client");
            g.Step();Check(!g.Flow.Open,"and it does not pop again by itself");
            g.Flow.Toggle(g.View());g.Step();Check(g.Flow.Choosing(g.View())?.id==chest,"C reopens it on the same reward");
            g.Flow.Toggle(g.View());Check(!g.Flow.Open,"C again closes it");
            // Consumed elsewhere (the same reward picked on another path): the screen never shows it.
            g.Flow.Toggle(g.View());Check(g.Run.Select("p0",chest,"ricochet",c=>true),"picked directly");g.Step();
            Check(!g.Flow.Open&&g.Flow.Choosing(g.View())==null&&g.Flow.Phase(g.View())==ExpeditionPickPhase.Closed,"a consumed reward never opens the screen");
            Check(!g.Flow.Show(g.View()),"nothing waiting: C does nothing");
            // A run first seen with a reward already waiting (a restored run, a late bind): no pop, C opens it.
            var h=new Rig();string waiting=h.Chest("p0","cache-b");h.Claim("p0",waiting);h.Step();
            Check(!h.Flow.Open,"a reward already waiting when the run is first seen does not pop the screen");
            Check(h.Flow.Show(h.View())&&h.Step().Send.intent==ExpeditionIntent.None&&h.Flow.Choosing(h.View())?.id==waiting,"C opens it");
            // Another pilot's claim never opens mine (their rewards and offers are not in my view).
            var o=new Rig();string theirs=o.Chest("p1","cache-c");o.Step();o.Claim("p1",theirs);o.Step();
            Check(!o.Flow.Open&&NativeExpeditionPick.Waiting(o.View())==0&&o.View().mine.rewards.Length==0,"another pilot's reward never opens or counts on my screen");
            // A downed pilot: the screen closes and a claim made meanwhile does not pop it (the Boss Test closes the open pick on a death).
            var d=new Rig();string mine=d.Chest("p0","cache-d");d.Step();d.Claim("p0",mine);d.Step();Check(d.Flow.Open,"open before the death");
            Check(d.Run.Defeat("p0"),"downed");d.Step();Check(!d.Flow.Open&&d.Flow.Choosing(d.View())==null&&!d.Flow.Show(d.View()),"a downed pilot's screen closes and C cannot open it");
            // Extracted (or not a member): nothing opens, nothing waits.
            var x=new Rig();string left=x.Chest("p0","cache-e");x.Claim("p0",left);var ev=x.View();ev.mine.extracted=true;
            Check(x.Flow.Step(ev,c=>default,c=>ExpeditionIntent.None,true).Send.intent==ExpeditionIntent.None&&!x.Flow.Open&&NativeExpeditionPick.Waiting(ev)==0,"an extracted pilot's rewards never open");
            x.Viewer="stranger";x.Step();Check(!x.Flow.Open&&NativeExpeditionPick.Waiting(x.View())==0,"a non-member sees nothing");
        }

        // 3. A pick sends Select with the shown reward id and the chip; the view confirms it; the next reward follows.
        private static void Picks()
        {
            var g=new Rig();string a=g.Chest("p0","cache-a"),b=g.Chest("p0","cache-b");g.Step();g.Claim("p0",a);g.Claim("p0",b);g.Step();
            var v=g.View();Check(g.Flow.Choosing(v)?.id==a,"the first claimed reward shows first");
            Check(g.Flow.Choose(v,"glass").intent==ExpeditionIntent.None,"an offer not on the tray is never sent");
            var cmd=g.Flow.Choose(v,"ricochet");Check(cmd.intent==ExpeditionIntent.Select&&cmd.target==a&&cmd.chip=="ricochet","the pick is Select(shown reward, chip)");
            Check(g.Flow.Phase(v)==ExpeditionPickPhase.Installing&&g.Flow.Sent==a&&g.Flow.Choosing(v)==null&&g.Flow.Choose(v,"vital").intent==ExpeditionIntent.None,"one pick in flight: the tray is down");
            Check(g.Send(cmd).state==ExpeditionCommandState.Pending,"sent through the host command path");
            var s=g.Run.Snapshot().rewards.First(r=>r.id==a);Check(s.consumed&&s.selected=="ricochet"&&g.Run.Snapshot().members.First(m=>m.account=="p0").chips.Any(c=>c.id=="ricochet"&&c.count==1),"the authority installed exactly that chip on that reward");
            Check(g.Flow.Installed!=a,"not confirmed before the view shows it");
            g.Step();v=g.View();Check(g.Flow.Installed==a&&g.Flow.Sent==null&&g.Tracker.State(cmd,v,g.Now).state==ExpeditionCommandState.Confirmed,"confirmed once the view shows the reward consumed");
            Check(g.Flow.Open&&g.Flow.Choosing(v)?.id==b,"the next waiting reward follows, as the Boss Test opens its next queued pick");
            var second=g.Flow.Choose(v,"vital");Check(second.target==b&&second.chip=="vital","the second pick names its own reward");g.Send(second);g.Step();
            Check(g.Flow.Installed==b&&!g.Flow.Open&&g.Flow.Phase(g.View())==ExpeditionPickPhase.Closed,"nothing left: it closes");
            // Choose later during the follow-through: the pick still settles, nothing chains.
            var h=new Rig();string c1=h.Chest("p0","cache-c"),c2=h.Chest("p0","cache-d");h.Step();h.Claim("p0",c1);h.Claim("p0",c2);h.Step();
            var p=h.Flow.Choose(h.View(),"piercing");h.Flow.Later();h.Send(p);h.Step();
            Check(h.Flow.Installed==c1&&!h.Flow.Open&&h.Flow.Choosing(h.View())==null,"Choose later in the follow-through: the pick settles and the next waits for C");
        }

        // 4. Refused, unavailable, unsent or unconfirmed picks return to the reward's tray with the caption.
        private static void Refusals()
        {
            var g=new Rig(eligible:(a,c)=>false);string a=g.Chest("p0","cache-a");g.Step();g.Claim("p0",a);g.Step();
            var cmd=g.Flow.Choose(g.View(),"vital");Check(g.Send(cmd).state==ExpeditionCommandState.Refused,"the authority refuses an ineligible chip");
            g.Step();var v=g.View();Check(g.Flow.Choosing(v)?.id==a&&g.Flow.Sent==null&&g.Flow.Refusal!=null&&g.Flow.Refusal.StartsWith("Refused · "),"a refusal returns to the same tray with its caption ("+g.Flow.Refusal+")");
            Check(g.Flow.Installed==null&&!g.Run.Snapshot().rewards.First(r=>r.id==a).consumed,"nothing reads as installed");
            g.Flow.Later();Check(g.Flow.Refusal==null,"Choose later clears the caption");
            // A pick the caller never sent (no command path): failed, back on the tray.
            var h=new Rig();string b=h.Chest("p0","cache-b");h.Step();h.Claim("p0",b);h.Step();h.Flow.Choose(h.View(),"vital");h.Step();
            Check(h.Flow.Choosing(h.View())?.id==b&&h.Flow.Refusal=="Failed · "+NativeExpeditionPick.NotSent,"an unsent pick is shown as failed");
            // A pick sent but never reflected: the tracker's timeout fails it.
            var t=new Rig();string c=t.Chest("p0","cache-c");t.Step();t.Claim("p0",c);t.Step();var pick=t.Flow.Choose(t.View(),"vital");
            t.Tracker.Record("p0",pick,ExpeditionCommandResult.Sent,t.View(),0);t.Now=1;t.Step();Check(t.Flow.Phase(t.View())==ExpeditionPickPhase.Installing,"pending while within the timeout");
            t.Now=ExpeditionIntentTracker.Timeout+1;t.Step();Check(t.Flow.Choosing(t.View())?.id==c&&t.Flow.Refusal=="Failed · "+ExpeditionIntentTracker.NoConfirmation,"no confirmation in time: failed, back on the tray");
            // Unavailable (no eligibility adapter): the host path says why.
            var u=new Rig();u.Hooks.Eligible=null;string d=u.Chest("p0","cache-d");u.Step();u.Claim("p0",d);u.Step();
            Check(u.Host.Blocked(new ExpeditionCommand(ExpeditionIntent.Select,d))==ExpeditionIntegrationHooks.NoEligibility,"the screen's blocked caption comes from the command path");
            u.Send(u.Flow.Choose(u.View(),"vital"));u.Step();Check(u.Flow.Refusal=="Unavailable · "+ExpeditionIntegrationHooks.NoEligibility,"an unavailable pick shows its reason");
        }

        // 5. Unopened rewards (reserved boss chips, catch-up) are opened by the screen's Claim, only when it can show.
        private static void Opening()
        {
            var g=new Rig();Check(g.Run.Enter("ward-zero")&&g.Run.Progress("q",2)&&g.Run.StartBosses(new[]{"scientist"})&&g.Run.DefeatBoss("scientist",2),"boss down");
            g.Step();var boss=g.View().mine.rewards.First(r=>r.source=="boss");Check(NativeExpeditionPick.Unopened(boss)&&!g.Flow.Open,"a reserved boss chip waits unopened, the screen closed");
            Check(g.Flow.Show(g.View()),"C opens the screen on it");
            Check(g.Step(false).Send.intent==ExpeditionIntent.None&&g.Flow.Claiming==null,"no Claim while the screen cannot show (menu, hold, downed)");
            var act=g.Step();Check(act.Send.intent==ExpeditionIntent.Claim&&act.Send.target==boss.id&&g.Flow.Phase(g.View())==ExpeditionPickPhase.Opening,"the screen opens it with a Claim of that reward");
            // The caller sends it at once (ConversionGame.ExpeditionPickStep); the host claims it and the next view carries the offers.
            Check(g.Send(act.Send).state==ExpeditionCommandState.Pending,"sent through the host command path");
            var again=g.Step();var v=g.View();var shown=g.Flow.Choosing(v);Check(again.Send.intent==ExpeditionIntent.None,"the Claim is sent once");
            Check(shown?.id==boss.id&&shown.offers.SequenceEqual(Offers)&&NativeExpeditionPick.Eyebrow(shown).StartsWith("BOSS REWARD")&&NativeExpeditionPick.Tone(shown)=="epic","its offers arrive and the tray shows it as a boss reward");
            // A refused open closes the screen with a notice instead of looping.
            var r=new Rig(offers:(a,id)=>null);string chest=r.Chest("p0","cache-a");r.Step();r.Flow.Show(r.View());var open=r.Step();r.Send(open.Send);var after=r.Step();
            Check(open.Send.intent==ExpeditionIntent.Claim&&after.Notice!=null&&after.Notice.StartsWith("Chip could not be opened · Refused")&&!r.Flow.Open&&r.Step().Send.intent==ExpeditionIntent.None,"a refused open: a notice, closed, not resent ("+after.Notice+")");
            // Choose later while it opens: the offers arrive but the screen stays closed until C.
            var l=new Rig();string c=l.Chest("p0","cache-b");l.Step();l.Flow.Show(l.View());var o=l.Step();l.Flow.Later();l.Send(o.Send);l.Step();
            Check(!l.Flow.Open&&l.Flow.Choosing(l.View())==null&&NativeExpeditionPick.Ready(l.View().Reward(c)),"Choose later during the open: offers arrive, no pop");
            l.Flow.Toggle(l.View());l.Step();Check(l.Flow.Choosing(l.View())?.id==c,"C shows it");
            // Ready rewards come before unopened ones; the pick on a ready one then opens the unopened one (the Boss Test's queue).
            var q=new Rig();string u=q.Chest("p0","cache-c"),k=q.Chest("p0","cache-d");q.Step();q.Claim("p0",k);q.Step();
            Check(q.Flow.Choosing(q.View())?.id==k,"the ready reward shows first");q.Send(q.Flow.Choose(q.View(),"vital"));var next=q.Step();
            Check(next.Send.intent==ExpeditionIntent.Claim&&next.Send.target==u,"after the pick the next unopened reward is opened");
        }

        // 6. "N more waiting" and the CHIPS tag count the viewer's own unpicked rewards.
        private static void Waiting()
        {
            var g=new Rig(3);string a=g.Chest("p0","cache-a"),b=g.Chest("p0","cache-b");g.Chest("p1","cache-c");string c=g.Chest("p0","cache-d");
            g.Step();var v=g.View();Check(NativeExpeditionPick.Waiting(v)==3,"three of mine waiting (another pilot's not counted)");
            g.Claim("p0",a);g.Step();v=g.View();Check(g.Flow.Choosing(v)?.id==a&&NativeExpeditionPick.MoreWaiting(v,a)==2,"2 more waiting besides the shown one");
            Check(NativeExpeditionPick.MoreWaiting(v,null)==3&&NativeExpeditionPick.MoreWaiting(v,"not-mine")==3,"nothing shown: all of them");
            g.Send(g.Flow.Choose(v,"vital"));g.Step();v=g.View();Check(NativeExpeditionPick.Waiting(v)==2,"a confirmed pick leaves the count");
            Check(NativeExpeditionPick.Waiting(v)==v.mine.rewards.Count(r=>!r.consumed),"the count is the minimal HUD's (unconsumed rewards of mine)");
            Check(g.Run.Select("p0",a,"vital",x=>true)==false,"a consumed reward cannot be picked twice");
            v.mine.extracted=true;Check(NativeExpeditionPick.Waiting(v)==0&&NativeExpeditionPick.MoreWaiting(v,b)==0,"an extracted pilot has nothing waiting");
            _=c;
        }

        // 7. Source scans: the Boss Test path's lines and chrome are kept and shared; the Expedition branch is presentation only.
        private static void Sources(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            string screen=R("Runtime/NativeDraftScreen.cs"),exp=R("Runtime/NativeExpeditionChipScreen.cs"),stage=R("Runtime/NativeDraftPresentation.cs"),follow=R("Runtime/NativeDraftFollow.cs");
            string game=R("Runtime/ConversionGame.cs"),pveUi=R("Runtime/NativePveUI.cs"),live=R("Runtime/NativeExpeditionLiveUI.cs"),hud=R("Runtime/NativeExpeditionHud.cs"),wrist=R("Runtime/NativeChipSelectPresentation.cs"),world=R("Runtime/NativeExpeditionWorldPresentation.cs"),core=R("Core/NativeExpeditionPick.cs");
            int body=screen.IndexOf("private void PveChipScreen()",StringComparison.Ordinal),end=screen.IndexOf("private static void PickMeta(",StringComparison.Ordinal);Check(body>0&&end>body,"PveChipScreen found");
            string pve=screen.Substring(body,end-body);
            // The Boss Test path: the same open pick, header, tray, keys, pick message, follow-through record, footer and alarm.
            foreach(var line in new[]{
                "var v=arena.Pve;if(v==null||!v.Rows.TryGetValue(arena.Local.Seat.id,out var row)||row.Open==null||row.Open.Offers==null)return;var pick=row.Open;float W=U.W,H=U.H;",
                "if(pveOpenId!=pick.Id){pveOpenId=pick.Id;pveOpenAt=Time.unscaledTime;draftFocus=-1;ResetHolo();}",
                "string source=rainbow?\"RAINBOW · 8 MAX-RARITY CHOICES\":pick.Source==\"start\"?\"BEGINNING CHIP\":(pick.Source==\"wave\"?\"WAVE REWARD · \":\"DISPENSED · \")+TierName(pick.Tier).ToUpperInvariant();",
                "DraftHeader(source,tier,\"Make your next move\",\"Choose one Chip. This choice is yours alone.\",W,H,out float top);",
                "PickMeta(W,Math.Max(0,row.Picks.Count-1));",
                "pveTray=LayoutTray(chips,top,W,H);LowerTray(pveTray,top,H-142);",
                "int choice=DrawTray(pveTray,chips,i=>arena.Local.Seat.chips.TryGetValue(chips[i].id,out int n)?n:0,elapsed,true,-1,i=>false,true);int key=DraftKeys(chips.Count,true,true);",
                "if(choice>=0){RecordPvePick(chips,choice,pick);pveOpenId=-1;ChoosePveChip(pick.Id,chips[choice].id);return;}",
                "Nameplate(arena.Local.Seat,\"Your pick\",28,H-22-66);","if(PickFooter(W,H,chips.Count))PveOpen(false);","PickAlarm(W,H,PveUnderAttack());"})
                Check(pve.Contains(line),"the Boss Test path keeps: "+line.Substring(0,Math.Min(60,line.Length)));
            int f=pve.IndexOf("if(PveFollowing){PveFollowScreen(U.W,U.H);return;}",StringComparison.Ordinal),e=pve.IndexOf("if(ExpeditionPickOpen){ExpeditionChipScreen();return;}",StringComparison.Ordinal),b=pve.IndexOf("var v=arena.Pve;",StringComparison.Ordinal);
            Check(f>0&&e>f&&b>e,"the follow-through first, then the Expedition branch, then the Boss Test body");
            // The shared chrome is the Boss Test's text and layout.
            foreach(var s in new[]{"\"YOU ARE VULNERABLE WHILE CHOOSING\"","(queued+\" more waiting\").ToUpperInvariant()","\"Press 1–\"+Mathf.Clamp(count,1,9)+\" or click a chip\"","\"Choose later  C · Esc\"","\"WARNING — YOU ARE UNDER ATTACK\"","\"Choose later to defend yourself\""})Check(screen.Contains(s),"shared chrome: "+s);
            // The Expedition tray is the same screen: shades, header, meta, tray with badges and keys 1-9, nameplate, footer, alarm.
            foreach(var s in new[]{"DraftShades(pveTray,W,H);","\"Make your next move\",\"Choose one Chip. This choice is yours alone.\"","PickMeta(W,more);","pveTray=LayoutTray(chips,top,W,H);LowerTray(pveTray,top,H-142);",
                "DrawTray(pveTray,chips,i=>held(chips[i].id),elapsed,enabled,-1,i=>false,true)","DraftKeys(chips.Count,enabled,true)","Nameplate(seat,\"Your pick\",28,H-22-66);","PickFooter(W,H,chips.Count)","PickAlarm(W,H,attacked);",
                "NativeExpeditionPick.Eyebrow(reward)","NativeExpeditionPick.MoreWaiting(v,reward.id)","RecordExpeditionPick(chips,pick,reward,","ExpeditionLiveSubmit(cmd);","expeditionPick.Later();"})Check(exp.Contains(s),"the Expedition branch uses "+s);
            Check(Regex.Matches(exp,@"ExpeditionLiveSubmit\(").Count==2&&!exp.Contains("ChoosePveChip")&&!exp.Contains("PveOpen(")&&!exp.Contains("OpenPvePick")&&!exp.Contains("ChoosePve("),"the Expedition sends only through ExpeditionLiveSubmit (the Select and an open's Claim), never the PvE messages");
            foreach(var (name,src) in new[]{("chip screen",exp),("pick model",core)})
            {
                Check(!Regex.IsMatch(src,@"\bNativeExpedition\b"),name+" holds no authority reference");
                Check(!Regex.IsMatch(src,@"\b(run|Run|session|Session|expeditionSession|authority)\s*\.\s*(Purchase|Claim|Select|Credit|Drop|DefeatBoss|Reprint|Extract|Defeat)\("),name+" calls no authority mutator");
                Check(!src.Contains("Random")&&!Regex.IsMatch(src,@"0x[0-9a-fA-F]{6}"),name+": no randomness, no restated colour");
            }
            // The 3D stage and the follow-through take the Expedition branch; the Boss Test reads stay.
            Check(stage.Contains("(PveFollowing||PvePickOpen||ExpeditionPickOpen)&&arena.Pve?.Holding!=true&&arena.Local.Hp>0&&!ReplayActive&&match.Phase==NativePhase.Fight"),"the stage gate opens for the Expedition pick under the Boss Test's conditions");
            Check(stage.Contains("int count=follow?pveFollowChips.Count:exp?expeditionPickChips.Count:pve?row.Open?.Offers?.Count??0:seat.offers.Count;")&&stage.Contains("string tier=follow?pveFollowTier:exp?expeditionPickTone:row?.Open?.Tier;")&&stage.Contains("presentation.DraftHeadLine=draftHeadLine;presentation.DraftWard=pve;"),"DraftFrame: the Expedition's offers and tone, the PvE ward backdrop");
            Check(follow.Contains("pveFollowReward=pveFollowEyebrow=null;")&&follow.Contains("arena.Pve==null&&pveFollowReward==null||arena.Pve?.Holding==true||ExpeditionFollowEnded")&&follow.Contains("pveFollowReward==null?\"Upgrade secured\":ExpeditionFollowTitle(age)"),"the follow-through: Boss Test picks unchanged, an Expedition pick installs only on confirmation");
            Check(exp.Contains("expeditionPick.Installed==pveFollowReward&&age>=ExpeditionFollowSettle?\"Upgrade secured\":\"Installing\""),"\"Upgrade secured\" only once the view confirms it");
            // Keys and the local input gate (a client gate only, as the Boss Test's).
            Check(game.Contains("if(Input.GetKeyDown(KeyCode.C)&&arena?.Pve!=null&&LiveExpedition==null){if(PvePickOpen)PveOpen(false);else PveOpen(true);}"),"the Boss Test's C stays, not in an Expedition");
            Check(Regex.Matches(game,@"!PvePickOpen&&!ExpeditionPickOpen&&arena\.Pve\?\.Holding!=true").Count==2,"local play pauses while the Expedition tray is up (Update and LateFrame)");
            Check(live.Contains("ExpeditionPickStep();")&&live.Contains("if(Input.GetKeyDown(KeyCode.C)){bool fromJ=expeditionScreenOpen;expeditionScreenOpen=false;ExpeditionPickKey(fromJ);}")&&live.Contains("if(ExpeditionPickOpen&&Input.GetKeyDown(KeyCode.Escape)&&!StageInputHeld){expeditionPick.Later();return true;}"),"C toggles it (from J too) and Escape is Choose later");
            // The HUD tag is the Boss Test's; the J panel only opens the screen.
            Check(pveUi.Contains("if(row.Picks.Count>0&&!PvePickOpen)PveChipsTag(W,H,row.Picks.Count,Time.unscaledTime-pvePendingAt);")&&pveUi.Contains("string chips=\"CHIPS \"+count;")&&pveUi.Contains("NativeHudChrome.Key(new Rect(cr.xMax-26,cr.y+6,18,18),\"C\");"),"the Boss Test's CHIPS tag with its C key");
            Check(exp.Contains("if(n>0&&!expeditionPick.Open)PveChipsTag(U.W,U.H,n,Time.unscaledTime-expeditionChipsAt);")&&live.Contains("ExpeditionChipsTag(v);")&&live.Contains("E.Minimal(v,width,1,false);"),"the Expedition draws the same tag in the scene in place of the minimal HUD's");
            int chips=hud.IndexOf("public static ExpeditionCommand Chips(",StringComparison.Ordinal),sanctuary=hud.IndexOf("public static ExpeditionCommand Sanctuary(",StringComparison.Ordinal);Check(chips>0&&sanctuary>chips,"chip panel found");string panel=hud.Substring(chips,sanctuary-chips);
            Check(panel.Contains("\"C · CHOOSE CHIP (\"+waiting+\")\"")&&panel.Contains("c.Choose();")&&!panel.Contains("ExpeditionIntent.Select,reward.id")&&!panel.Contains("ExpeditionIntent.Claim,reward.id")&&!panel.Contains("\"PICK\"")&&panel.Contains("\"HELD\""),"the J panel opens the screen instead of picking, and keeps the held chips");
            Check(live.Contains("Choose=()=>{expeditionScreenOpen=false;ExpeditionPickKey(true);},"),"its CHOOSE CHIP line closes J and opens the screen");
            // The wrist look: only my own pick, through a hook Astra's public flag can feed; the PvE rows' line unchanged.
            int hook=wrist.IndexOf("if(ExpeditionChoosing!=null){pick=ExpeditionChoosing(id);",StringComparison.Ordinal),rows=wrist.IndexOf("NativeChipSelect.Selecting(true,row.Open?.Id??0,p.Hp>0,p.Seat.connected,v.Holding,!string.IsNullOrEmpty(v.Cinematic),v.Stage==\"ended\")",StringComparison.Ordinal);
            Check(hook>0&&rows>hook&&wrist.Contains("i<count&&pick.Offers!=null?arena?.PveChip(pick.Offers[i])?.rarity:null"),"the wrist look reads the Expedition hook first; an offer-less pick draws cyan cards");
            Check(exp.Contains("if(arena?.Local==null||id!=arena.Local.Seat.id||ExpeditionScreenOpen)return null;"),"only my own pick feeds the hook");
            Check(world.Contains("!ReplayActive&&!PvePickScreen;"),"the Expedition's world pieces stay out of the pick screen's stage");
        }
    }
}
#endif
