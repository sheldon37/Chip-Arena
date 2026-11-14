using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // The Expedition chip pick on the Boss Test's personal chip screen (Claude, 2026-10-15; docs/EXPEDITION-PRESENTATION.md "Chip picks" and
    // "Chip screen authority contract"). Owner: "We should also utilize the same chip selection screen (That we utilize in the Boss Test)
    // instead of selecting on the J menu." Presentation only. It reads the viewer's own ExpeditionView rewards (offers exist only while a
    // pick is pending, so nothing here can show another pilot's choice), the public members[].choosing flag and the intent tracker's state,
    // and returns the intents for the caller to send through ExpeditionCommands: Claim (open an unopened reward), OpenChoice / CloseChoice
    // (the host's public choosing presence: host hold, no immunity; owner direction supersedes PR #234) and Select.
    // Runtime/NativeExpeditionChipScreen.cs draws it as the Boss Test's tray over the live view.
    public static class NativeExpeditionPick
    {
        /// <summary>The viewer is a member still in the run (an extracted pilot's unopened rewards can no longer be opened).</summary>
        public static bool Mine(ExpeditionView v)=>v?.mine!=null&&!string.IsNullOrEmpty(v.mine.account)&&!v.mine.extracted;
        /// <summary>Claimed, not yet picked, with its offers in the view: the screen can show it.</summary>
        public static bool Ready(ExpeditionViewReward r)=>r!=null&&r.claimed&&!r.consumed&&r.offers!=null&&r.offers.Length>0;
        /// <summary>Owned and not claimed yet (a reserved boss chip, a catch-up, an unopened purchase): the screen sends Claim to open it.</summary>
        public static bool Unopened(ExpeditionViewReward r)=>r!=null&&!r.claimed&&!r.consumed;
        /// <summary>The viewer's unpicked rewards (the HUD's CHIPS N and the J panel's count), counted as the minimal HUD counts them.</summary>
        public static int Waiting(ExpeditionView v){if(!Mine(v))return 0;int n=0;foreach(var r in v.mine.rewards)if(r!=null&&!r.consumed)n++;return n;}
        /// <summary>"N more waiting": the unpicked rewards other than the one on screen.</summary>
        public static int MoreWaiting(ExpeditionView v,string shown){var r=shown==null?null:v?.Reward(shown);return Math.Max(0,Waiting(v)-(r!=null&&!r.consumed?1:0));}
        /// <summary>The reward to show: `prefer` while it can still be picked or opened, else the first ready one in the view's order, else the
        /// first unopened one; null when nothing waits.</summary>
        public static ExpeditionViewReward Next(ExpeditionView v,string prefer=null)
        {
            if(!Mine(v))return null;var p=prefer==null?null:v.Reward(prefer);if(Ready(p)||Unopened(p))return p;
            foreach(var r in v.mine.rewards)if(Ready(r))return r;
            foreach(var r in v.mine.rewards)if(Unopened(r))return r;
            return null;
        }
        /// <summary>The rarity whose colour tints the header and the stage. Expedition offers carry no tier: a boss (high-quality) reward is
        /// epic or better, so it takes the epic colour; any other the neutral common one (as the Boss Test's beginning chip).</summary>
        public static string Tone(ExpeditionViewReward r)=>r!=null&&(r.highQuality||r.source=="boss")?"epic":"common";
        /// <summary>A Ward Zero chip machine's purchase (NativeExpeditionM1Arena: "ward-machine:" transactions).</summary>
        public const string WardMachine=":chest:ward-machine:";
        /// <summary>The header's eyebrow from the reward's source and kind (PveChipScreen's "DISPENSED · RARE" line, without a tier).</summary>
        public static string Eyebrow(ExpeditionViewReward r)
        {
            if(r==null)return "";bool high=r.highQuality||r.source=="boss";
            string name=r.source=="boss"?NativeExpeditionPresentation.Name(r.boss):"";
            string head=r.source=="boss"?"BOSS REWARD"+(name.Length>0?" · "+name:""):r.source=="chest"?(r.id!=null&&r.id.Contains(WardMachine)?"DISPENSED · WARD MACHINE":"CACHE · PURCHASED"):
                r.source=="monster"?"DROP · PICKED UP":r.source=="catchup"?"CATCH-UP CHIP":string.IsNullOrEmpty(r.source)?"REWARD":r.source.ToUpperInvariant();
            return high?head+" · EPIC OR BETTER":head;
        }
        /// <summary>A stable, non-zero id for a reward (the wrist look's slot and gesture seed; NativeChipSelect.Selecting reads 0 as none).</summary>
        public static int Id(string reward){unchecked{uint h=2166136261;if(reward!=null)foreach(char c in reward)h=(h^c)*16777619;int id=(int)(h>>1);return id==0?1:id;}}
        /// <summary>A tracked state that ends the attempt at once (refused, unavailable, or unconfirmed in time).</summary>
        public static bool Ended(ExpeditionCommandState s)=>s==ExpeditionCommandState.Refused||s==ExpeditionCommandState.Unavailable||s==ExpeditionCommandState.Failed;
        public const string NotSent="The command could not be sent.";
        /// <summary>The caption of an ended attempt ("Refused · ...", "Failed · ..."), or NotSent when the tracker never recorded it.</summary>
        public static string Caption(ExpeditionCommandResult s,bool sent)=>sent?NativeExpeditionPresentation.Feedback(s,out _)??NotSent:"Failed · "+NotSent;
    }

    public enum ExpeditionPickPhase { Closed, Opening, Choosing, Installing }
    /// <summary>What a flow step asks of its caller: an intent to send (Claim, OpenChoice or CloseChoice) and a notice to show.</summary>
    public struct ExpeditionPickAction { public ExpeditionCommand Send; public string Notice; }

    /// <summary>
    /// The pick screen's state across frames: the Boss Test's open pick (NativePveRow.Open) for an Expedition, against the choosing
    /// authority (PR #233, corrected by PR #234: presence only; docs/EXPEDITION-PRESENTATION.md "Chip screen authority contract").
    ///  - It opens only on request: C, or the J panel's CHOOSE CHIP line (Show / Toggle). Collecting a reward never opens it (the
    ///    contract: "do not open choosing merely because a reward was collected"); the CHIPS tag counts it, as the Boss Test queues a
    ///    dispensed chip.
    ///  - While open it stays on the shown reward while that can be picked, else the next ready one, else the next unopened one, which it
    ///    opens with a Claim (only when the screen can show; a refused or unconfirmed Claim closes it with a notice).
    ///  - Showing a ready reward sends OpenChoice(reward) unless the view already shows the viewer choosing (the host keeps it across
    ///    queued picks); a refused or unconfirmed OpenChoice closes the screen with a notice. Closing it (Later: C, Escape, Choose later,
    ///    or the flow closing itself) sends CloseChoice while the view still shows the viewer choosing, once.
    ///  - A pick (Choose) sends Select and is in flight until the view shows the reward consumed (confirmed: the next waiting reward
    ///    follows, as the Boss Test opens its next queued pick, or the screen closes) or the tracker shows it ended (back on that reward's
    ///    tray with the caption). A death closes it, as the Boss Test's does (the host clears choosing itself).
    /// </summary>
    public sealed class ExpeditionPickFlow
    {
        /// <summary>The screen is wanted on this client (it shows when the game can show it: ConversionGame.ExpeditionPickOpen).</summary>
        public bool Open{get;private set;}
        /// <summary>The reward on screen, being opened, or last shown (C reopens it).</summary>
        public string Reward{get;private set;}
        /// <summary>The reward whose Claim this screen sent, until its offers arrive.</summary>
        public string Claiming{get;private set;}
        /// <summary>The reward whose Select is in flight, and the chip picked.</summary>
        public string Sent{get;private set;}
        public string Chip{get;private set;}
        /// <summary>The last reward whose pick the view confirmed (the follow-through's "Upgrade secured").</summary>
        public string Installed{get;private set;}
        /// <summary>Why the last pick on Reward did not go through, shown on its tray; null otherwise.</summary>
        public string Refusal{get;private set;}
        /// <summary>The reward this screen last sent OpenChoice for (null once closed), and whether the view has shown it choosing since.</summary>
        public string Choice{get;private set;}
        private bool choiceSeen,closeSent;private string run;

        /// <summary>The view shows the viewer choosing (the host's public flag; held by the host, without damage immunity).</summary>
        public static bool ChoosingNow(ExpeditionView v)=>v?.viewer!=null&&v.Member(v.viewer)?.choosing==true;
        public ExpeditionPickPhase Phase(ExpeditionView v)
        {
            if(Sent!=null)return ExpeditionPickPhase.Installing;if(!Open)return ExpeditionPickPhase.Closed;if(Claiming!=null)return ExpeditionPickPhase.Opening;
            return NativeExpeditionPick.Mine(v)&&v.mine.alive&&NativeExpeditionPick.Ready(Reward==null?null:v.Reward(Reward))?ExpeditionPickPhase.Choosing:ExpeditionPickPhase.Closed;
        }
        /// <summary>The reward the tray shows now, or null.</summary>
        public ExpeditionViewReward Choosing(ExpeditionView v)=>Phase(v)==ExpeditionPickPhase.Choosing?v.Reward(Reward):null;
        public void Reset(){Open=false;Reward=Claiming=Sent=Chip=Installed=Refusal=Choice=null;choiceSeen=closeSent=false;run=null;}

        /// <summary>
        /// Follows the viewer's view once per frame and returns at most one intent to send. `state` / `last` are the intent tracker's for a
        /// command (Claim and Select share a key, so `last` says which was sent); `canShow` whether the screen could show now (an open's
        /// Claim and OpenChoice are sent only then).
        /// </summary>
        public ExpeditionPickAction Step(ExpeditionView v,Func<ExpeditionCommand,ExpeditionCommandResult> state,Func<ExpeditionCommand,ExpeditionIntent> last,bool canShow)
        {
            var act=default(ExpeditionPickAction);
            if(v==null){Reset();return act;}
            if(v.run!=run){if(run!=null)Reset();run=v.run;}
            bool choosing=ChoosingNow(v);if(!choosing)closeSent=false;else if(Choice!=null)choiceSeen=true;
            if(!NativeExpeditionPick.Mine(v)){Open=false;Reward=Claiming=Sent=Chip=Refusal=Choice=null;return act;}
            // The pick in flight: confirmed once the reward is consumed (or settled out of the view, as the tracker reads it).
            if(Sent!=null)
            {
                var r=v.Reward(Sent);var cmd=new ExpeditionCommand(ExpeditionIntent.Select,Sent,Chip);
                if(r==null||r.consumed){Installed=Sent;Sent=Chip=Refusal=null;}
                else{var s=state(cmd);bool sent=last(cmd)==ExpeditionIntent.Select;if(!sent||NativeExpeditionPick.Ended(s.state)){Refusal=NativeExpeditionPick.Caption(s,sent);Reward=Sent;Sent=Chip=null;}}
            }
            // The open this screen sent: its offers arrive in the view, or the attempt ends.
            if(Claiming!=null)
            {
                var r=v.Reward(Claiming);var cmd=new ExpeditionCommand(ExpeditionIntent.Claim,Claiming);
                if(NativeExpeditionPick.Ready(r)){Reward=Claiming;Claiming=null;}
                else if(!NativeExpeditionPick.Unopened(r))Claiming=null;
                else{var s=state(cmd);bool sent=last(cmd)==ExpeditionIntent.Claim;if(!sent||NativeExpeditionPick.Ended(s.state)){act.Notice="Chip could not be opened · "+NativeExpeditionPick.Caption(s,sent);Claiming=null;Open=false;}}
            }
            if(!v.mine.alive){Open=false;Claiming=null;}
            // Keep the open screen on a reward it can show, opening the next unopened one when none is ready.
            if(Open&&Sent==null&&Claiming==null)
            {
                var r=NativeExpeditionPick.Next(v,Reward);
                if(r==null){Open=false;Reward=Refusal=null;}
                else if(NativeExpeditionPick.Ready(r)){if(r.id!=Reward)Refusal=null;Reward=r.id;}
                else if(canShow&&!v.loading){Reward=Claiming=r.id;Refusal=null;act.Send=new ExpeditionCommand(ExpeditionIntent.Claim,r.id);return act;}
            }
            // The host's choosing presence follows the screen: OpenChoice when it shows a ready reward the view does not show choosing for (again after
            // the host cleared it: a load, the final plant completion), CloseChoice once when it closes while the view still shows choosing.
            if(Open&&Sent==null&&Claiming==null&&NativeExpeditionPick.Ready(v.Reward(Reward)))
            {
                if(!choosing&&canShow&&!v.loading)
                {
                    if(Choice!=Reward||choiceSeen){Choice=Reward;choiceSeen=closeSent=false;act.Send=new ExpeditionCommand(ExpeditionIntent.OpenChoice,Reward);return act;}
                    var cmd=new ExpeditionCommand(ExpeditionIntent.OpenChoice,Choice);var s=state(cmd);bool sent=last(cmd)==ExpeditionIntent.OpenChoice;
                    if(!sent||NativeExpeditionPick.Ended(s.state)){act.Notice="Chip choice could not open · "+NativeExpeditionPick.Caption(s,sent);Open=false;}
                }
            }
            if(!Open){Choice=null;choiceSeen=false;}
            // A close the view never reflected in time is sent again (only then: a refused or unavailable one is not retried every frame).
            var close=new ExpeditionCommand(ExpeditionIntent.CloseChoice);if(!Open&&choosing&&closeSent&&state(close).state==ExpeditionCommandState.Failed)closeSent=false;
            if(!Open&&choosing&&!closeSent){closeSent=true;act.Send=close;}
            return act;
        }
        /// <summary>C: opens the screen on the waiting rewards, or closes it (Choose later).</summary>
        public void Toggle(ExpeditionView v){if(Open)Later();else Show(v);}
        /// <summary>Opens the screen when a reward waits (C, the J panel's CHOOSE CHIP line); false when nothing waits.</summary>
        public bool Show(ExpeditionView v)
        {
            if(Open)return true;var next=NativeExpeditionPick.Mine(v)&&v.mine.alive?NativeExpeditionPick.Next(v,Reward):null;if(next==null)return false;
            Open=true;Reward=next.id;Refusal=null;return true;
        }
        /// <summary>Choose later (C, Escape, the footer button): closes the screen on this client; the next Step sends CloseChoice. An open
        /// or a pick in flight still settles.</summary>
        public void Later(){Open=false;Refusal=null;}
        /// <summary>A pick on the shown reward: the Select to send, or None when the screen is not choosing or the chip is not offered.</summary>
        public ExpeditionCommand Choose(ExpeditionView v,string chip)
        {
            var r=Choosing(v);if(r==null||chip==null||Array.IndexOf(r.offers,chip)<0)return default;
            Sent=r.id;Chip=chip;Refusal=null;return new ExpeditionCommand(ExpeditionIntent.Select,r.id,chip);
        }
    }

    /// <summary>
    /// Who is choosing a chip, by pilot seat, from the view's public members[].choosing (the host's flag; never anyone's offers), for the
    /// wrist look and the SELECTING CHIP tag over every choosing pilot, as the Boss Test drives them from each row's replicated open pick.
    /// Each run of choosing gets a stable non-zero pick id and the time it was first seen (the look's slot, gesture seed and beat).
    /// </summary>
    public sealed class ExpeditionChoosers
    {
        public struct Entry { public string Account; public int Seat, Pick; public double Since; public bool Mine; }
        private readonly Dictionary<string,Entry> open=new Dictionary<string,Entry>();private readonly Dictionary<string,int> runs=new Dictionary<string,int>();
        private readonly List<string> gone=new List<string>();private string run;
        public int Count=>open.Count;
        public void Step(ExpeditionView v,double now)
        {
            if(v==null||v.run!=run){open.Clear();runs.Clear();run=v?.run;if(v==null)return;}
            gone.Clear();foreach(var k in open.Keys)gone.Add(k);
            foreach(var m in v.members)
            {
                if(m==null||string.IsNullOrEmpty(m.account)||!m.choosing||m.seat<=0||!m.alive||!m.connected)continue;gone.Remove(m.account);
                if(open.TryGetValue(m.account,out var e)){if(e.Seat!=m.seat){e.Seat=m.seat;open[m.account]=e;}continue;}
                runs.TryGetValue(m.account,out int n);runs[m.account]=++n;
                open[m.account]=new Entry{Account=m.account,Seat=m.seat,Pick=NativeExpeditionPick.Id(m.account+"#"+n),Since=now,Mine=m.account==v.viewer};
            }
            foreach(var k in gone)open.Remove(k);
        }
        /// <summary>The choosing entry of the pilot in `seat`, if any.</summary>
        public bool At(int seat,out Entry entry)
        {
            foreach(var e in open.Values)if(e.Seat==seat){entry=e;return true;}
            entry=default;return false;
        }
    }
}
