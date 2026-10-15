using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // The Expedition chip pick on the Boss Test's personal chip screen (Claude, 2026-10-15; docs/EXPEDITION-PRESENTATION.md "Chip picks").
    // Owner: "We should also utilize the same chip selection screen (That we utilize in the Boss Test) instead of selecting on the J menu."
    // Presentation only. It reads the viewer's own ExpeditionView rewards (offers exist only while a pick is pending, so nothing here can
    // show another pilot's choice) and the intent tracker's state, and returns the Claim / Select intents for the caller to send through
    // ExpeditionCommands. The authority has no "choosing" state yet: the screen being open, Choose later and the local input pause are this
    // client's only (Runtime/NativeExpeditionChipScreen.cs draws it with PveChipScreen's tray, stage and follow-through).
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
    /// <summary>What a flow step asks of its caller: an intent to send (an open's Claim) and a notice to show.</summary>
    public struct ExpeditionPickAction { public ExpeditionCommand Send; public string Notice; }

    /// <summary>
    /// The pick screen's local state across frames: the Boss Test's open pick (NativePveRow.Open) for an Expedition.
    ///  - A reward newly claimed with offers (the pilot's own claim at a cache, a Ward machine or a drop) opens the screen, as the Boss Test
    ///    opens its next pick; rewards already waiting when the run is first seen never pop it.
    ///  - C opens it on the waiting rewards (Toggle, Show); C again, Escape or Choose later closes it on this client (Later); C reopens it.
    ///  - While open it stays on the shown reward while that can be picked, else the next ready one, else the next unopened one, which it
    ///    opens with a Claim (only when the screen can show; a refused or unconfirmed Claim closes it with a notice).
    ///  - A pick (Choose) sends Select and is in flight until the view shows the reward consumed (confirmed: the next waiting reward follows,
    ///    as the Boss Test opens its next queued pick, or the screen closes) or the tracker shows it ended (back on that reward's tray with
    ///    the caption). A death closes it, as the Boss Test's does.
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
        private string run;private readonly HashSet<string> seen=new HashSet<string>();

        public ExpeditionPickPhase Phase(ExpeditionView v)
        {
            if(Sent!=null)return ExpeditionPickPhase.Installing;if(!Open)return ExpeditionPickPhase.Closed;if(Claiming!=null)return ExpeditionPickPhase.Opening;
            return NativeExpeditionPick.Mine(v)&&v.mine.alive&&NativeExpeditionPick.Ready(Reward==null?null:v.Reward(Reward))?ExpeditionPickPhase.Choosing:ExpeditionPickPhase.Closed;
        }
        /// <summary>The reward the tray shows now, or null.</summary>
        public ExpeditionViewReward Choosing(ExpeditionView v)=>Phase(v)==ExpeditionPickPhase.Choosing?v.Reward(Reward):null;
        public void Reset(){Open=false;Reward=Claiming=Sent=Chip=Installed=Refusal=null;run=null;seen.Clear();}

        /// <summary>
        /// Follows the viewer's view once per frame. `state` / `last` are the intent tracker's for a command (Claim and Select share a key, so
        /// `last` says which was sent); `canShow` whether the screen could show now (an open's Claim is sent only then).
        /// </summary>
        public ExpeditionPickAction Step(ExpeditionView v,Func<ExpeditionCommand,ExpeditionCommandResult> state,Func<ExpeditionCommand,ExpeditionIntent> last,bool canShow)
        {
            var act=default(ExpeditionPickAction);
            if(v==null){Reset();return act;}
            if(v.run!=run){Reset();run=v.run;foreach(var r in v.mine.rewards)if(NativeExpeditionPick.Ready(r))seen.Add(r.id);}
            if(!NativeExpeditionPick.Mine(v)){Open=false;Reward=Claiming=Sent=Chip=Refusal=null;return act;}
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
            // A reward newly claimed with its offers opens the screen (once each).
            foreach(var r in v.mine.rewards)if(NativeExpeditionPick.Ready(r)&&seen.Add(r.id)&&!Open&&Sent==null&&v.mine.alive){Open=true;Reward=r.id;Refusal=null;}
            if(!v.mine.alive){Open=false;Claiming=null;}
            // Keep the open screen on a reward it can show, opening the next unopened one when none is ready.
            if(Open&&Sent==null&&Claiming==null)
            {
                var r=NativeExpeditionPick.Next(v,Reward);
                if(r==null){Open=false;Reward=Refusal=null;}
                else if(NativeExpeditionPick.Ready(r)){if(r.id!=Reward)Refusal=null;Reward=r.id;}
                else if(canShow){Reward=Claiming=r.id;Refusal=null;seen.Add(r.id);act.Send=new ExpeditionCommand(ExpeditionIntent.Claim,r.id);}
            }
            return act;
        }
        /// <summary>C: opens the screen on the waiting rewards, or closes it (Choose later).</summary>
        public void Toggle(ExpeditionView v){if(Open)Later();else Show(v);}
        /// <summary>Opens the screen when a reward waits (C from the J screen, its CHOOSE CHIP line); false when nothing waits.</summary>
        public bool Show(ExpeditionView v)
        {
            if(Open)return true;if(!NativeExpeditionPick.Mine(v)||!v.mine.alive||NativeExpeditionPick.Next(v,Reward)==null)return false;
            Open=true;Refusal=null;return true;
        }
        /// <summary>Choose later (C, Escape, the footer button): closes the screen on this client. An open or a pick in flight still settles.</summary>
        public void Later(){Open=false;Refusal=null;}
        /// <summary>A pick on the shown reward: the Select to send, or None when the screen is not choosing or the chip is not offered.</summary>
        public ExpeditionCommand Choose(ExpeditionView v,string chip)
        {
            var r=Choosing(v);if(r==null||chip==null||Array.IndexOf(r.offers,chip)<0)return default;
            Sent=r.id;Chip=chip;Refusal=null;return new ExpeditionCommand(ExpeditionIntent.Select,r.id,chip);
        }
    }
}
