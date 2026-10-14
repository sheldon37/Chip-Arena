using System;
using System.Collections.Generic;
using System.Linq;

namespace ChipArena.UnityConversion
{
    // Expedition UI intents (Claude, 2026-10-14; docs/EXPEDITION-PRESENTATION.md). Screens never change the run: a control raises an
    // intent, an IExpeditionCommands sends it, and ExpeditionIntentTracker shows it as confirmed only once the authority's state (the
    // next ExpeditionView) reflects it. A refusal, a missing hook or a missing transport is shown as such, never as success.

    public enum ExpeditionIntent { None, VoteExit, CancelExit, VoteExtraction, CancelExtraction, Extract, NextLoop, Claim, Select, Reprint, VoteContinue, CancelContinue, Purchase }
    public enum ExpeditionCommandState { Idle, Pending, Confirmed, Refused, Unavailable, Failed }

    [Serializable] public struct ExpeditionCommand
    {
        public ExpeditionIntent intent;public string target, chip;
        public ExpeditionCommand(ExpeditionIntent intent,string target=null,string chip=null){this.intent=intent;this.target=target;this.chip=chip;}
        /// <summary>Commands that replace each other share a key (a vote and its cancel; one pick per reward).</summary>
        public string Key=>(intent==ExpeditionIntent.CancelContinue?ExpeditionIntent.VoteContinue:intent==ExpeditionIntent.CancelExit?ExpeditionIntent.VoteExit:intent==ExpeditionIntent.CancelExtraction?ExpeditionIntent.VoteExtraction:intent==ExpeditionIntent.Select?ExpeditionIntent.Claim:intent)+":"+target;
    }
    public struct ExpeditionCommandResult
    {
        public ExpeditionCommandState state;public string reason;
        public static ExpeditionCommandResult Of(ExpeditionCommandState state,string reason=null)=>new ExpeditionCommandResult{state=state,reason=reason};
        public static ExpeditionCommandResult Sent=>Of(ExpeditionCommandState.Pending);
        public bool Shown=>state!=ExpeditionCommandState.Idle;
    }

    /// <summary>Sends a viewer's intents to the Expedition authority.</summary>
    public interface IExpeditionCommands
    {
        string Viewer{get;}
        /// <summary>Null when the intent can be sent; otherwise why its control is unavailable (shown on the control).</summary>
        string Blocked(ExpeditionCommand command);
        /// <summary>Pending when sent (confirmation comes from the view), Refused / Unavailable / Failed otherwise.</summary>
        ExpeditionCommandResult Send(ExpeditionCommand command);
    }

    /// <summary>
    /// The integration operations a host-local intent needs beyond the rules commands. Each is optional: without it the intent
    /// is Unavailable with the reason below. The integration validates proximity, catalog, costs and persistence.
    /// </summary>
    public sealed class ExpeditionIntegrationHooks
    {
        /// <summary>(account, reward id) to host-generated, catalog-validated offers for Claim; null or empty refuses.</summary>
        public Func<string,string,string[]> Offers;
        /// <summary>(account, chip) catalog eligibility at selection time, for Select.</summary>
        public Func<string,string,bool> Eligible;
        /// <summary>The durable individual extraction transaction (save the tombstone, then publish). Pending on success.</summary>
        public Func<string,ExpeditionCommandResult> Extract;
        public Func<string,string,ExpeditionCommandResult> Purchase;
        /// <summary>(payer, victim) to the machine cost after proximity/permission checks; below 1 refuses. Remote recovery flag out.</summary>
        public Func<string,string,(int cost,bool remote)> Reprint;
        public const string NoOffers="Chip offers need the content catalog adapter (not connected).";
        public const string NoEligibility="Chip selection needs the catalog eligibility adapter (not connected).";
        public const string NoExtraction="Extraction needs the save-then-publish transaction (not connected).";
        public const string NoReprint="Reprints need the machine adapter (cost, range and permission; not connected).";
        public const string HostTransfer="The host leaves only through a host transfer, and endpoint migration is not implemented.";
    }

    /// <summary>The host's own intents: the real NativeExpedition commands, with false shown as refused.</summary>
    public sealed class ExpeditionHostCommands:IExpeditionCommands
    {
        private readonly NativeExpedition run;private readonly ExpeditionIntegrationHooks hooks;
        public string Viewer{get;}
        public ExpeditionHostCommands(NativeExpedition run,string viewer,ExpeditionIntegrationHooks hooks=null)
        {this.run=run??throw new ArgumentNullException(nameof(run));Viewer=viewer;this.hooks=hooks??new ExpeditionIntegrationHooks();}
        public const string Refused="The authority refused it.";
        public string Blocked(ExpeditionCommand c)
        {
            switch(c.intent)
            {
                case ExpeditionIntent.Extract:
                    var s=run.Snapshot();if(s.host==Viewer&&s.members.Any(p=>p.account!=Viewer&&p.connected&&!p.extracted))return ExpeditionIntegrationHooks.HostTransfer;
                    return hooks.Extract==null?ExpeditionIntegrationHooks.NoExtraction:null;
                case ExpeditionIntent.NextLoop:return run.Snapshot().host==Viewer?null:"Only the host starts the next loop.";
                case ExpeditionIntent.Claim:return hooks.Offers==null?ExpeditionIntegrationHooks.NoOffers:null;
                case ExpeditionIntent.Select:return hooks.Eligible==null?ExpeditionIntegrationHooks.NoEligibility:null;
                case ExpeditionIntent.Purchase:return hooks.Purchase==null?"Purchases need the scene proximity adapter (not connected).":null;
                case ExpeditionIntent.Reprint:return hooks.Reprint==null?ExpeditionIntegrationHooks.NoReprint:null;
                case ExpeditionIntent.None:return "Nothing to send.";
                default:return null;
            }
        }
        public ExpeditionCommandResult Send(ExpeditionCommand c)
        {
            string blocked=Blocked(c);if(blocked!=null)return ExpeditionCommandResult.Of(ExpeditionCommandState.Unavailable,blocked);
            bool ok;
            switch(c.intent)
            {
                case ExpeditionIntent.VoteExit:ok=run.VoteExit(Viewer,true);break;
                case ExpeditionIntent.CancelExit:ok=run.VoteExit(Viewer,false);break;
                case ExpeditionIntent.VoteExtraction:ok=run.VoteExtraction(Viewer,true);break;
                case ExpeditionIntent.CancelExtraction:ok=run.VoteExtraction(Viewer,false);break;
                case ExpeditionIntent.NextLoop:ok=run.AdvanceWhenReady();break;
                case ExpeditionIntent.VoteContinue:ok=run.VoteContinue(Viewer,true);break;
                case ExpeditionIntent.CancelContinue:ok=run.VoteContinue(Viewer,false);break;
                case ExpeditionIntent.Extract:return hooks.Extract(Viewer);
                case ExpeditionIntent.Purchase:return hooks.Purchase(Viewer,c.target);
                case ExpeditionIntent.Claim:{var offers=hooks.Offers(Viewer,c.target);ok=offers!=null&&offers.Length>0&&run.Claim(Viewer,c.target,offers);break;}
                case ExpeditionIntent.Select:{var eligible=hooks.Eligible;ok=run.Select(Viewer,c.target,c.chip,chip=>eligible(Viewer,chip));break;}
                case ExpeditionIntent.Reprint:{var (cost,remote)=hooks.Reprint(Viewer,c.target);ok=cost>=1&&run.Reprint(Viewer,c.target,cost,remote);break;}
                default:return ExpeditionCommandResult.Of(ExpeditionCommandState.Unavailable,"Unknown intent.");
            }
            return ok?ExpeditionCommandResult.Sent:ExpeditionCommandResult.Of(ExpeditionCommandState.Refused,Refused);
        }
    }

    /// <summary>Guest sender. A reply can refuse immediately; success still requires the replicated view. An accepted reply only
    /// says so (still Pending): ExpeditionIntentTracker confirms from the view, and an unanswered, unreflected send times out.</summary>
    public sealed class ExpeditionGuestCommands:IExpeditionCommands
    {
        public const string NoChannel="Guest commands need the Expedition command channel (not built yet).";
        public const string SentToHost="Sent to the host · waiting for its reply.";
        public const string AcceptedByHost="Accepted by the host · waiting for the state to show it.";
        public string Viewer{get;}
        private readonly Func<ExpeditionView> view;private readonly Action<ExpeditionCommandRequest> send;
        private readonly Dictionary<string,ExpeditionCommandRequest> latest=new Dictionary<string,ExpeditionCommandRequest>();
        private readonly Dictionary<string,ExpeditionCommandResult> replies=new Dictionary<string,ExpeditionCommandResult>();
        public ExpeditionGuestCommands(string viewer,Func<ExpeditionView> view=null,Action<ExpeditionCommandRequest> send=null){Viewer=viewer;this.view=view;this.send=send;}
        public string Blocked(ExpeditionCommand c)=>send==null?NoChannel:view?.Invoke()==null?"Waiting for Expedition state.":null;
        public ExpeditionCommandResult Send(ExpeditionCommand c)
        {
            var blocked=Blocked(c);if(blocked!=null)return ExpeditionCommandResult.Of(ExpeditionCommandState.Unavailable,blocked);
            var v=view();var request=new ExpeditionCommandRequest{run=v.run,epoch=v.epoch,transaction=Guid.NewGuid().ToString("N"),intent=c.intent,target=c.target,chip=c.chip};
            if(latest.TryGetValue(c.Key,out var old))replies.Remove(old.transaction);
            latest[c.Key]=request;
            try{send(request);return ExpeditionCommandResult.Of(ExpeditionCommandState.Pending,SentToHost);}
            catch(Exception e)when(e is InvalidOperationException||e is System.IO.IOException){return ExpeditionCommandResult.Of(ExpeditionCommandState.Failed,"Could not send to the Expedition host.");}
        }
        public void Reply(ExpeditionCommandReply reply)
        {
            if(reply==null||!latest.Values.Any(c=>c.transaction==reply.transaction))return;
            // Never trust an ACK to confirm a UI action without state reflection.
            if(reply.state==ExpeditionCommandState.Refused||reply.state==ExpeditionCommandState.Unavailable||reply.state==ExpeditionCommandState.Failed)
                replies[reply.transaction]=ExpeditionCommandResult.Of(reply.state,reply.reason);
            // Accepted (the host's Pending; a Confirmed reply is treated the same): shown as accepted, still waiting for the state.
            else if(reply.state==ExpeditionCommandState.Pending||reply.state==ExpeditionCommandState.Confirmed)
                replies[reply.transaction]=ExpeditionCommandResult.Of(ExpeditionCommandState.Pending,AcceptedByHost);
        }
        public ExpeditionCommandResult Result(ExpeditionCommand command)=>latest.TryGetValue(command.Key,out var request)&&replies.TryGetValue(request.transaction,out var result)?result:default;
    }

    /// <summary>
    /// State-driven confirmation. An intent shows Pending after it is sent, Confirmed only when the authority's view reflects it,
    /// Failed when no confirmation arrives in time, and Refused / Unavailable / Failed straight from the sender.
    /// </summary>
    /// <summary>What the viewer's view showed when an intent was sent, so its effect can be recognised after the run moves on.</summary>
    public struct ExpeditionIntentBaseline
    {
        public int loop;public string area, generation;public bool reward;
        public static ExpeditionIntentBaseline Of(ExpeditionCommand c,ExpeditionView v)=>v==null?default:new ExpeditionIntentBaseline{loop=v.loop,area=v.area,reward=v.Reward(c.target)!=null,generation=v.chests.FirstOrDefault(o=>o.id==c.target)?.generation};
    }

    public sealed class ExpeditionIntentTracker
    {
        public const double Timeout=6;
        private sealed class Entry{public ExpeditionCommand command;public ExpeditionCommandResult result;public double at;public ExpeditionIntentBaseline before;public ExpeditionGuestCommands feedback;}
        private readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();
        public const string NoConfirmation="No confirmation from the authority.";
        public const string AwaitingSave="Extracted. Waiting for the save to be confirmed.";

        /// <summary>Sends through the viewer's command path and records the result.</summary>
        public ExpeditionCommandResult Submit(IExpeditionCommands commands,ExpeditionCommand command,ExpeditionView before,double now)
        {var result=Record(commands.Viewer,command,commands.Send(command),before,now);entries[commands.Viewer+"|"+command.Key].feedback=commands as ExpeditionGuestCommands;return result;}
        /// <summary>Records an intent's send result for a viewer (also for intents the host received from that viewer).</summary>
        public ExpeditionCommandResult Record(string viewer,ExpeditionCommand command,ExpeditionCommandResult result,ExpeditionView before,double now)
        {
            entries[viewer+"|"+command.Key]=new Entry{command=command,result=result,at=now,before=ExpeditionIntentBaseline.Of(command,before)};return result;
        }
        /// <summary>The displayed state of the latest intent sharing this command's key (Idle when none was sent).</summary>
        public ExpeditionCommandResult State(ExpeditionCommand command,ExpeditionView view,double now)
        {
            if(view==null||!entries.TryGetValue(view.viewer+"|"+command.Key,out var e))return default;
            if(e.result.state==ExpeditionCommandState.Pending)
            {
                // A refusal or failure from the host shows at once; an accepted reply only updates the pending caption.
                var feedback=e.feedback?.Result(e.command)??default;
                if(feedback.Shown&&feedback.state!=ExpeditionCommandState.Pending){e.result=feedback;return e.result;}
                if(feedback.state==ExpeditionCommandState.Pending)e.result.reason=feedback.reason;
                bool extracted=e.command.intent==ExpeditionIntent.Extract&&view.mine.extracted;
                if(Reflected(e.command,view,e.before))e.result=ExpeditionCommandResult.Of(ExpeditionCommandState.Confirmed,"Confirmed by the authority (revision "+view.revision+").");
                else if(extracted)e.result.reason=AwaitingSave;
                else if(now-e.at>Timeout)e.result=ExpeditionCommandResult.Of(ExpeditionCommandState.Failed,NoConfirmation);
            }
            return e.result;
        }
        /// <summary>The latest intent sent under this command's key (a vote or its cancel), or None.</summary>
        public ExpeditionIntent Last(string viewer,ExpeditionCommand command)=>entries.TryGetValue(viewer+"|"+command.Key,out var e)?e.command.intent:ExpeditionIntent.None;
        public void Clear()=>entries.Clear();
        /// <summary>A vote or its cancel: the authority can withdraw it later (a replacing vote, a downed or departed pilot).</summary>
        public static bool IsVote(ExpeditionIntent i)=>i==ExpeditionIntent.VoteExit||i==ExpeditionIntent.CancelExit||i==ExpeditionIntent.VoteExtraction||
            i==ExpeditionIntent.CancelExtraction||i==ExpeditionIntent.VoteContinue||i==ExpeditionIntent.CancelContinue;

        /// <summary>
        /// Whether the authority's view shows the intent's effect. A departure vote also counts once the party has left that
        /// building (unanimity resets the votes on the same tick); a group-extraction vote once the viewer has extracted; a pick
        /// once its reward is consumed or, having been in the view when sent, has left it as settled history.
        /// </summary>
        public static bool Reflected(ExpeditionCommand c,ExpeditionView v,ExpeditionIntentBaseline before=default)
        {
            string me=v.viewer;bool left=before.area!=null&&(v.area!=before.area||v.loop!=before.loop);
            switch(c.intent)
            {
                case ExpeditionIntent.VoteExit:return v.exitVotes.Contains(me)||left;
                case ExpeditionIntent.CancelExit:return !v.exitVotes.Contains(me);
                case ExpeditionIntent.VoteExtraction:return v.extractVotes.Contains(me)||v.mine.extracted;
                case ExpeditionIntent.CancelExtraction:return !v.extractVotes.Contains(me);
                // Extraction is complete only after the durable save holds its tombstone (docs/EXPEDITION.md save transaction).
                case ExpeditionIntent.Extract:return v.mine.extracted&&v.mine.extractionSaved;
                case ExpeditionIntent.NextLoop:return v.loop>before.loop;
                case ExpeditionIntent.VoteContinue:return v.continueVotes.Contains(me)||v.loop>before.loop;
                case ExpeditionIntent.CancelContinue:return !v.continueVotes.Contains(me);
                case ExpeditionIntent.Claim:{var r=v.Reward(c.target);return r!=null?r.claimed:before.reward;}
                case ExpeditionIntent.Select:{var r=v.Reward(c.target);return r!=null?r.consumed:before.reward;}
                case ExpeditionIntent.Purchase:return before.generation!=null&&v.mine.rewards.Any(r=>r.source=="chest"&&r.id==before.loop+":"+before.area+":chest:"+before.generation);
                case ExpeditionIntent.Reprint:return v.Member(c.target)?.alive==true;
                default:return false;
            }
        }
    }
}
