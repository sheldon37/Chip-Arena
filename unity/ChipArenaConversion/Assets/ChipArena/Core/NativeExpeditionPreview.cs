using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// DEV PREVIEW ONLY (--ca-expedition-preview; Claude, 2026-10-14). Scripted scenarios that drive a real NativeExpedition through
    /// its public commands so the Expedition screens can be viewed before the mode exists. Not a game mode, not reachable from
    /// the menus, and it binds no session. Preview stand-ins, labelled on screen: the offers, prices, machine quotes and the
    /// extraction transaction (the integration's catalog, chest/machine adapters and save-then-publish); the greybox anchors (the
    /// scene's SetWorld and reward positions); a loopback guest channel with latency (the real ExpeditionGuestCommands sender and
    /// reply path, executed through the host's commands as the router does); and simulated transport progress, which the live
    /// transport does not report yet. Every allocation, dice roll, vote, timer and loop advance is the authority's own.
    /// </summary>
    public sealed class NativeExpeditionPreview
    {
        public static readonly string[] Party={"sheldon","astra","nova","kestrel"};
        public sealed class Step{public string Caption;public Action<NativeExpeditionPreview> Act;public string Viewer;}
        public sealed class Scenario{public string Id, Title, Summary;public int Players=4;public Action<NativeExpeditionPreview> Setup;public Step[] Steps;}

        public NativeExpedition Run{get;private set;}
        public string Viewer;
        public readonly ExpeditionTransferLog Transfer=new ExpeditionTransferLog();
        public readonly ExpeditionIntentTracker Intents=new ExpeditionIntentTracker();
        public ExpeditionSavepoint Saved{get;private set;}
        public ExpeditionHostCandidate[] Candidates=Array.Empty<ExpeditionHostCandidate>();
        public int ScenarioIndex{get;private set;}
        public int StepIndex{get;private set;}
        public string Caption{get;private set;}="";
        /// <summary>Durable write of a savepoint; throws on failure. Runtime: the save store in a temporary folder.</summary>
        public readonly Action<ExpeditionSavepoint> Write;
        /// <summary>The next save write fails (scenario "save failure").</summary>
        public bool FailNextWrite;
        public readonly ExpeditionIntegrationHooks Hooks;
        public Scenario Current=>Scenarios[ScenarioIndex];
        public bool Finished=>StepIndex>=Current.Steps.Length;
        public double Clock{get;private set;}
        /// <summary>PREVIEW: simulated transport progress (the live transport reports none of it yet; docs/EXPEDITION-INTEGRATION.md).</summary>
        public bool SimulatedEndpoint, SimulatedReserved, SimulatedPackage, SimulatedAck;public double SimulatedDeadline=-1;
        /// <summary>PREVIEW: pilots the machine gives no quote for (shown "price unavailable", never $0).</summary>
        public readonly HashSet<string> NoQuote=new HashSet<string>();

        public NativeExpeditionPreview(Action<ExpeditionSavepoint> write=null)
        {
            Write=write??(_=>{});
            Hooks=new ExpeditionIntegrationHooks{Offers=PreviewOffers,Eligible=(account,chip)=>true,Extract=ExtractTransaction,Reprint=PreviewReprint};
            Load(0);
        }

        public ExpeditionViewSources Sources=>new ExpeditionViewSources{Saved=Saved,Transfer=Transfer,Candidates=Candidates,EndpointMigration=SimulatedEndpoint,
            EndpointReserved=SimulatedReserved,PackageStored=SimulatedPackage,AckReceived=SimulatedAck,Deadline=SimulatedDeadline,ReprintCost=Quote};
        /// <summary>The authority's projection for a viewer right now (what the host builds).</summary>
        public ExpeditionView View(string viewer=null)=>NativeExpeditionView.Build(Run,viewer??Viewer,Sources);
        public bool IsHost(string viewer)=>Run.Snapshot().host==viewer;
        /// <summary>What the viewer's screen shows: the host's own projection, or the latest view frame a guest has received (the host
        /// publishes one every Replication seconds of the preview clock; it arrives Latency later, behind any earlier command reply).</summary>
        public ExpeditionView Shown(string viewer=null)
        {
            viewer??=Viewer;if(IsHost(viewer))return View(viewer);var c=Channel(viewer);
            if(c.Shown==null)c.Shown=NativeExpeditionView.Normalize(View(viewer));
            return c.Shown;
        }
        /// <summary>The viewer's command path: the host's own commands; any other viewer is a guest on the loopback channel.</summary>
        public IExpeditionCommands Commands(string viewer=null)
        {
            viewer??=Viewer;return IsHost(viewer)?(IExpeditionCommands)new ExpeditionHostCommands(Run,viewer,Hooks):Channel(viewer).Guest;
        }
        /// <summary>Sends a viewer's intent through its real command path and records it for state-driven confirmation.</summary>
        public ExpeditionCommandResult Submit(ExpeditionCommand command,string viewer=null)
        {
            viewer??=Viewer;return Intents.Submit(Commands(viewer),command,Shown(viewer),Clock);
        }
        /// <summary>A scripted intent from another pilot, as if the host had received it: run through the host's path and recorded for that pilot.</summary>
        public ExpeditionCommandResult Scripted(string viewer,ExpeditionCommand command,Func<ExpeditionCommandResult> send)
        {
            var before=View(viewer);return Intents.Record(viewer,command,send(),before,Clock);
        }

        // ---- PREVIEW loopback guest channel --------------------------------------------------------------------------------
        /// <summary>One-way latency of the loopback channel and the guests' replication interval (preview clock seconds).</summary>
        public const double Latency=.35, Replication=.5;
        private sealed class GuestChannel{public ExpeditionGuestCommands Guest;public ExpeditionView Shown;public readonly List<(ExpeditionView view,double at)> Frames=new List<(ExpeditionView,double)>();}
        private double published=-1;
        private readonly Dictionary<string,GuestChannel> channels=new Dictionary<string,GuestChannel>();
        private readonly List<(string viewer,ExpeditionCommandRequest request,double at)> outbox=new List<(string,ExpeditionCommandRequest,double)>();
        private readonly List<(string viewer,ExpeditionCommandReply reply,double at)> inbox=new List<(string,ExpeditionCommandReply,double)>();
        /// <summary>PREVIEW fault: the channel loses the next guest command (no reply, no state change: it times out as Failed).</summary>
        public bool DropNextGuestCommand;
        public int GuestMessagesInFlight=>outbox.Count+inbox.Count;
        private GuestChannel Channel(string viewer)
        {
            if(!channels.TryGetValue(viewer,out var c))
            {
                c=new GuestChannel();channels[viewer]=c;
                c.Guest=new ExpeditionGuestCommands(viewer,()=>Shown(viewer),request=>outbox.Add((viewer,request,Clock)));
            }
            return c;
        }
        // The channel, in order: deliver what has arrived (replies before the view frames sent after them), publish the views when
        // due, then run the commands that have reached the host. The host side is ExpeditionCommandRouter's: the run/epoch fence,
        // the host's command path for that pilot, and the reply (the preview has no catalog session, so it runs
        // ExpeditionHostCommands with the preview hooks).
        private void Pump()
        {
            for(int i=0;i<inbox.Count;)
            {
                var (viewer,reply,at)=inbox[i];if(Clock-at<Latency){i++;continue;}inbox.RemoveAt(i);Channel(viewer).Guest.Reply(reply);
            }
            foreach(var c in channels.Values)
                for(int i=0;i<c.Frames.Count;){if(Clock-c.Frames[i].at<Latency){i++;continue;}c.Shown=c.Frames[i].view;c.Frames.RemoveAt(i);}
            if(published<0||Clock-published>=Replication)
            {
                published=Clock;foreach(var kv in channels)kv.Value.Frames.Add((NativeExpeditionView.Normalize(View(kv.Key)),Clock));
            }
            for(int i=0;i<outbox.Count;)
            {
                var (viewer,request,at)=outbox[i];if(Clock-at<Latency){i++;continue;}outbox.RemoveAt(i);
                if(DropNextGuestCommand){DropNextGuestCommand=false;continue;}
                var snap=Run.Snapshot();ExpeditionCommandReply reply;
                if(request.run!=snap.run||request.epoch!=snap.epoch)reply=new ExpeditionCommandReply{transaction=request.transaction,state=ExpeditionCommandState.Refused,reason="Invalid, stale, or unauthenticated Expedition command."};
                else{var r=new ExpeditionHostCommands(Run,viewer,Hooks).Send(new ExpeditionCommand(request.intent,request.target,request.chip));reply=new ExpeditionCommandReply{transaction=request.transaction,state=r.state,reason=r.reason};}
                inbox.Add((viewer,reply,Clock));
            }
        }

        public void Load(int scenario)
        {
            ScenarioIndex=Math.Max(0,Math.Min(Scenarios.Length-1,scenario));StepIndex=0;Clock=0;Saved=null;FailNextWrite=false;Transfer.Clear();Intents.Clear();Candidates=Array.Empty<ExpeditionHostCandidate>();
            channels.Clear();outbox.Clear();inbox.Clear();DropNextGuestCommand=false;NoQuote.Clear();published=-1;
            SimulatedEndpoint=SimulatedReserved=SimulatedPackage=SimulatedAck=false;SimulatedDeadline=-1;nextExtraction=0;
            var s=Current;Run=NativeExpedition.Create("preview-"+s.Id,"zombie-city",Party[0],"greatsword",20261014,new[]
                {new ExpeditionInstance{id="ward-zero",target=12},new ExpeditionInstance{id="metro-station",target=10},new ExpeditionInstance{id="clinic",target=8}});
            string[] weapons={"greatsword","rifle","water","daggers"};
            for(int i=1;i<s.Players;i++)Require(Run.Join(Party[i],weapons[i]),"join");
            int[] wallets={420,260,180,90};for(int i=0;i<s.Players;i++)Require(Run.Credit(Party[i],"preview-wallet",wallets[i]),"wallet");
            World();
            Viewer=Party[0];s.Setup?.Invoke(this);Caption=s.Summary;
        }
        /// <summary>Plays the next scripted step; false at the end.</summary>
        public bool Next()
        {
            if(Finished)return false;var step=Current.Steps[StepIndex++];if(step.Viewer!=null)Viewer=step.Viewer;step.Act?.Invoke(this);Caption=step.Caption;return true;
        }
        private double nextExtraction;
        /// <summary>Advances the authority clock as the host session does (steps of at most 0.25 s), carries the loopback channel,
        /// collects due group extractions through the extraction transaction, and starts the next loop once the continue policy holds.</summary>
        public void Tick(double dt)
        {
            if(!(dt>0))return;dt=Math.Min(dt,1);Clock+=dt;
            while(dt>0){double step=Math.Min(.25,dt);Run.Tick(step);dt-=step;}
            Pump();
            if(Run.Snapshot().elapsed>=nextExtraction)foreach(var who in Run.ExtractionDue())if(ExtractTransaction(who).state!=ExpeditionCommandState.Pending)nextExtraction=Run.Snapshot().elapsed+1;
            Run.AdvanceWhenReady();
        }

        // ---- Preview stand-ins for the integration layer --------------------------------------------------------------------
        private static readonly string[] BossPool={"splitter","echo-strike","homing","buckshot","executioner","last-word","blast-dash","vacuum","boomerang","fortress","comet","apex"};
        private static readonly string[] DropPool={"vital","overclock","ricochet","piercing","ember","static","shrapnel","multishot","leech","repair","bulldozer","marksman"};
        /// <summary>Three distinct offers per reward from a fixed preview list, keyed by the reward id (not the real catalog).</summary>
        private string[] PreviewOffers(string account,string reward)
        {
            var r=Run.Snapshot().rewards.FirstOrDefault(x=>x.id==reward);if(r==null)return null;var pool=r.source=="boss"?BossPool:DropPool;
            uint h=2166136261;foreach(char c in reward+"|"+account)h=(h^c)*16777619;
            var picks=new List<string>();for(int i=0;picks.Count<3&&i<pool.Length*2;i++){string id=pool[(int)((h+(uint)i*7)%(uint)pool.Length)];if(!picks.Contains(id))picks.Add(id);}
            return picks.ToArray();
        }
        /// <summary>PREVIEW machine quote for a downed pilot: $60 while carried to the machine, $90 for a remote recovery; 0 (no quote) for NoQuote.</summary>
        public int Quote(string victim)
        {
            var v=Run.Snapshot().members.FirstOrDefault(p=>p.account==victim);if(v==null||v.alive||v.extracted||NoQuote.Contains(victim))return 0;return v.carriedBy!=null?60:90;
        }
        // The reprint adapter charges exactly the quote: carried by the payer at the machine, or a remote recovery at the sanctuary.
        private (int,bool) PreviewReprint(string payer,string victim)
        {
            var v=Run.Snapshot().members.FirstOrDefault(p=>p.account==victim);int quote=Quote(victim);
            if(v==null||quote<1)return (0,false);return v.carriedBy==payer?(quote,false):v.carriedBy==null?(quote,true):(0,false);
        }
        public const string WriteFailure="The save could not be written; you are still in the run.";
        /// <summary>
        /// The documented extraction transaction (docs/EXPEDITION.md): prepare on a detached copy, add the tombstone to the safe
        /// checkpoint, write it durably, and only then publish the extraction to the live authority. A failed write keeps the old state.
        /// </summary>
        public ExpeditionCommandResult ExtractTransaction(string account)
        {
            if(!Run.CanExtract(account))return ExpeditionCommandResult.Of(ExpeditionCommandState.Refused,ExpeditionHostCommands.Refused);
            ExpeditionSavepoint checkpoint;
            try{checkpoint=ExpeditionSavepoint.Capture(Run);}catch(InvalidOperationException){checkpoint=Saved;}
            if(checkpoint==null)return ExpeditionCommandResult.Of(ExpeditionCommandState.Failed,"No safe checkpoint to record the extraction in.");
            var copy=new NativeExpedition(Run.Snapshot());if(!copy.Extract(account))return ExpeditionCommandResult.Of(ExpeditionCommandState.Refused,ExpeditionHostCommands.Refused);
            ExpeditionSavepoint archive;
            try{archive=checkpoint.WithExtractions(copy.Snapshot());}
            catch(Exception e)when(e is ArgumentException||e is InvalidOperationException){return ExpeditionCommandResult.Of(ExpeditionCommandState.Failed,"The extraction could not be added to the checkpoint.");}
            try{if(FailNextWrite){FailNextWrite=false;throw new IOException("Preview: simulated disk failure.");}Write(archive);}
            catch(Exception e)when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException||e is InvalidOperationException)
            {return ExpeditionCommandResult.Of(ExpeditionCommandState.Failed,WriteFailure);}
            Saved=archive;
            return Run.Extract(account)?ExpeditionCommandResult.Sent:ExpeditionCommandResult.Of(ExpeditionCommandState.Failed,"The run changed before the extraction was published.");
        }
        /// <summary>A safe checkpoint at the sanctuary (the sanctuary panel's "checkpoint saved" line).</summary>
        public void SaveCheckpoint()
        {
            var save=ExpeditionSavepoint.Capture(Run);if(Saved!=null)save=new ExpeditionSavepoint{checkpoint=save.checkpoint,extracted=Saved.extracted.Union(save.extracted).ToArray()};
            Write(save);Saved=save;
        }

        // ---- PREVIEW greybox world: the scene's anchors (SetWorld) and chip caches ------------------------------------------
        public const string World0="zombie-city";
        /// <summary>The greybox layout: building entrances, the reprint machine and two chip caches round the sanctuary plaza, and
        /// each building's inner gate. Metres, y up; reward positions are added by DropAt / DefeatBossAt / FinishFinalAt.</summary>
        private void World()
        {
            var anchors=new[]
            {
                new ExpeditionWorldAnchor{id="ward-zero",area=World0,kind=NativeExpeditionWorld.Door,x=-15,z=12},
                new ExpeditionWorldAnchor{id="metro-station",area=World0,kind=NativeExpeditionWorld.Door,x=0,z=17},
                new ExpeditionWorldAnchor{id="clinic",area=World0,kind=NativeExpeditionWorld.Door,x=15,z=12},
                new ExpeditionWorldAnchor{id="reprint-machine",area=World0,kind=NativeExpeditionWorld.Machine,x=7,z=-3},
                new ExpeditionWorldAnchor{id="cache-north",area=World0,kind=NativeExpeditionWorld.Chest,x=-7,z=-1},
                new ExpeditionWorldAnchor{id="cache-east",area=World0,kind=NativeExpeditionWorld.Chest,x=11,z=3},
                new ExpeditionWorldAnchor{id="ward-zero-gate",area="ward-zero",kind=NativeExpeditionWorld.Door,x=0,z=-9},
                new ExpeditionWorldAnchor{id="metro-gate",area="metro-station",kind=NativeExpeditionWorld.Door,x=0,z=-9},
                new ExpeditionWorldAnchor{id="clinic-gate",area="clinic",kind=NativeExpeditionWorld.Door,x=0,z=-9},
            };
            var caches=new[]{new ExpeditionChestOffer{id="cache-north",generation="n1",price=150,cooldown=0},new ExpeditionChestOffer{id="cache-east",generation="e3",price=220,cooldown=40}};
            Require(Run.SetWorld(anchors,caches),"world");
        }
        /// <summary>The scene restocks a cache: a new generation and a cooldown `seconds` ahead of the authority clock.</summary>
        public void Restock(string cache,string generation,double seconds)
        {
            var s=Run.Snapshot();var caches=s.chests.Select(c=>c.id==cache?new ExpeditionChestOffer{id=c.id,generation=generation,price=c.price,cooldown=s.elapsed+seconds}:c).ToArray();
            Require(Run.SetWorld(s.anchors,caches),"restock");
        }

        // ---- Scenario helpers ------------------------------------------------------------------------------------------------
        private static void Require(bool ok,string what){if(!ok)throw new InvalidOperationException("Expedition preview step refused: "+what);}
        private int serial;
        private void Kills(int n){Require(Run.Progress("preview-kills-"+(serial++),n),"progress");}
        private void Safe(){foreach(var p in Run.Snapshot().members.Where(p=>p.connected&&p.alive&&!p.extracted))Require(Run.SetSafe(p.account,true),"safe");}
        /// <summary>Fast-forward a cleared building's departure (unanimous vote), then everyone reaches the sanctuary.</summary>
        private void Leave()
        {
            foreach(var p in Run.Snapshot().members.Where(p=>p.connected&&p.alive&&!p.extracted))Require(Run.VoteExit(p.account,true),"vote");
            Tick(.25);Require(Run.Snapshot().phase==ExpeditionPhase.Town,"left");Safe();
        }
        private void ClearBuilding(string id,string boss,int drops=-1)
        {
            Require(Run.Enter(id),"enter");var target=Run.Snapshot().instances.First(i=>i.id==id).target;Kills(target);Require(Run.StartBosses(new[]{boss}),"bosses");Require(Run.DefeatBossAt(boss,0,0,7,drops),"boss");
        }
        private void Claim(string account,string reward){Require(Run.Claim(account,reward,PreviewOffers(account,reward)),"claim");}
        private string FirstReward(string owner,string source)=>Run.Snapshot().rewards.First(r=>r.owner==owner&&r.source==source&&!r.consumed).id;
        private string Generation(string cache)=>Run.Snapshot().chests.First(c=>c.id==cache).generation;

        public static readonly Scenario[] Scenarios=
        {
            new Scenario{Id="town",Title="Sanctuary, board and first building",Summary="Four pilots at the sanctuary. Each wallet is personal; only yours is shown. Caches show stock, price and cooldown before anyone buys.",
                Steps=new[]
                {
                    new Step{Caption="Sheldon buys the north cache for $150: the chip belongs to the buyer, and the cache restocks for 30 s.",Act=p=>
                    {Require(p.Run.Purchase("sheldon",p.Generation("cache-north"),150),"purchase");p.Claim("sheldon",p.FirstReward("sheldon","chest"));p.Restock("cache-north","n2",30);}},
                    new Step{Caption="The party enters Ward Zero together; the entrance seals behind them.",Act=p=>{Require(p.Run.Enter("ward-zero"),"enter");p.Kills(5);}},
                    new Step{Caption="Ordinary drops fall where the walkers died: first pickup. Astra grabs one; the other is still open.",Act=p=>{p.Kills(3);Require(p.Run.DropAt("walker-17",-4,0,3),"drop");Require(p.Run.DropAt("walker-23",3.5f,0,5),"drop");p.Claim("astra",p.Run.Snapshot().rewards.First(r=>r.source=="monster").id);}},
                    new Step{Caption="Quota met: the Zombie Scientist arrives. The door stays sealed until it dies.",Act=p=>{p.Kills(4);Require(p.Run.StartBosses(new[]{"zombie-scientist"}),"bosses");}},
                    new Step{Caption="Boss down where it fell: one reserved Epic+ chip for each survivor at that instant, standing round the spot.",Act=p=>Require(p.Run.DefeatBossAt("zombie-scientist",0,0,9),"boss")},
                    new Step{Caption="Sheldon votes to leave: one vote starts the shared 10 s timer.",Act=p=>Require(p.Run.VoteExit("sheldon",true),"vote")},
                    new Step{Caption="Back at the sanctuary: individual and group extraction are unlocked. Checkpoint saved.",Act=p=>{if(p.Run.Snapshot().phase==ExpeditionPhase.Instance)p.Leave();else p.Safe();p.SaveCheckpoint();}},
                }},
            new Scenario{Id="dice",Title="Boss loot: survivors and dice",Summary="Ward Zero, quota met, two bosses. Eligibility freezes when each boss dies.",
                Setup=p=>{Require(p.Run.Enter("ward-zero"),"enter");p.Kills(12);Require(p.Run.StartBosses(new[]{"zombie-scientist","lab-assistant"}),"bosses");},
                Steps=new[]
                {
                    new Step{Caption="Nova goes down before the Scientist dies.",Act=p=>Require(p.Run.Defeat("nova"),"defeat")},
                    new Step{Caption="Scientist down with 5 chips for 3 survivors: 1 each, 2 extra decided by the stored dice.",Act=p=>Require(p.Run.DefeatBossAt("zombie-scientist",-3,0,8,5),"boss")},
                    new Step{Caption="Kestrel falls after the Scientist died: her reserved chip stays hers.",Act=p=>Require(p.Run.Defeat("kestrel"),"defeat")},
                    new Step{Caption="Lab Assistant down with 1 chip for 2 survivors: fewer chips than survivors, the dice decide.",Act=p=>Require(p.Run.DefeatBossAt("lab-assistant",4,0,8,1),"boss")},
                    new Step{Caption="Each survivor opens their own reserved chips: offers are personal.",Act=p=>{p.Claim("sheldon",p.FirstReward("sheldon","boss"));p.Claim("astra",p.FirstReward("astra","boss"));}},
                    new Step{Caption="Independent picks: Sheldon and Astra each choose from their own offers. Your picks this loop list under your chips, for you only.",Act=p=>
                    {
                        foreach(var a in new[]{"sheldon","astra"}){var r=p.Run.Snapshot().rewards.First(x=>x.owner==a&&x.claimed&&!x.consumed);Require(p.Run.Select(a,r.id,r.offers[a=="sheldon"?0:1],c=>true),"select");}
                    }},
                }},
            new Scenario{Id="extract",Title="Extraction and reprints",Summary="Ward Zero cleared; the party is back at the sanctuary.",
                Setup=p=>{p.ClearBuilding("ward-zero","zombie-scientist");p.Leave();p.SaveCheckpoint();},
                Steps=new[]
                {
                    new Step{Caption="Astra extracts alone: the save is written first, then the run publishes it. The others continue.",Act=p=>Require(p.Scripted("astra",new ExpeditionCommand(ExpeditionIntent.Extract),()=>p.ExtractTransaction("astra")).state==ExpeditionCommandState.Pending,"extract")},
                    new Step{Caption="Group extraction needs a strict majority of connected living pilots: Sheldon and Nova vote.",Act=p=>{Require(p.Run.VoteExtraction("sheldon",true),"vote");Require(p.Run.VoteExtraction("nova",true),"vote");}},
                    new Step{Caption="Nova changes her mind: below the majority the countdown cancels.",Act=p=>Require(p.Run.VoteExtraction("nova",false),"cancel")},
                    new Step{Caption="Kestrel tries to extract but the save fails: she stays in the run and is told so.",Viewer="kestrel",Act=p=>{p.FailNextWrite=true;p.Scripted("kestrel",new ExpeditionCommand(ExpeditionIntent.Extract),()=>p.ExtractTransaction("kestrel"));}},
                    new Step{Caption="Nova goes down by the gate: the reprint machine quotes $90 for a remote recovery from the sanctuary.",Viewer="sheldon",Act=p=>Require(p.Run.Defeat("nova"),"defeat")},
                    new Step{Caption="Kestrel goes down too. The machine has no quote for her: price unavailable, never $0, and her reprint stays off.",Act=p=>{p.NoQuote.Add("kestrel");Require(p.Run.Defeat("kestrel"),"defeat");}},
                    new Step{Caption="Sheldon reprints Nova for the quoted $90 from his own wallet, through the host's command path.",Viewer="sheldon",Act=p=>p.Submit(new ExpeditionCommand(ExpeditionIntent.Reprint,"nova"),"sheldon")},
                }},
            new Scenario{Id="victory",Title="Victory: extract or continue",Summary="Every building is cleared; the world boss fight is on. Each pilot will choose.",
                Setup=p=>
                {
                    p.ClearBuilding("ward-zero","zombie-scientist");p.Leave();p.ClearBuilding("metro-station","conductor");p.Leave();p.ClearBuilding("clinic","head-nurse");p.Leave();
                    Require(p.Run.BeginFinal(),"final");
                },
                Steps=new[]
                {
                    new Step{Caption="Kestrel goes down in the final fight.",Act=p=>Require(p.Run.Defeat("kestrel"),"defeat")},
                    new Step{Caption="The world boss falls in the plaza: 5 chips for 3 survivors, 1 each and 2 by the stored dice. Kestrel was down: nothing is reserved for her.",Act=p=>
                    {Require(p.Run.FinishFinalAt(0,0,4,5),"final");p.Safe();p.SaveCheckpoint();}},
                    new Step{Caption="Astra extracts with her build; the save confirms it.",Act=p=>Require(p.Scripted("astra",new ExpeditionCommand(ExpeditionIntent.Extract),()=>p.ExtractTransaction("astra")).state==ExpeditionCommandState.Pending,"extract")},
                    new Step{Caption="Nova chooses Continue. Kestrel, still down, agrees too: downed pilots can consent. Everyone remaining must agree.",Act=p=>
                    {
                        Require(p.Scripted("nova",new ExpeditionCommand(ExpeditionIntent.VoteContinue),()=>new ExpeditionHostCommands(p.Run,"nova",p.Hooks).Send(new ExpeditionCommand(ExpeditionIntent.VoteContinue))).state==ExpeditionCommandState.Pending,"continue");
                        Require(p.Scripted("kestrel",new ExpeditionCommand(ExpeditionIntent.VoteContinue),()=>new ExpeditionHostCommands(p.Run,"kestrel",p.Hooks).Send(new ExpeditionCommand(ExpeditionIntent.VoteContinue))).state==ExpeditionCommandState.Pending,"continue");
                    }},
                    new Step{Caption="Sheldon (host) chooses Continue: everyone remaining agreed and pilots are alive, so loop 2 starts. The board resets; builds and the clock carry on.",Viewer="sheldon",Act=p=>
                    {Require(p.Submit(new ExpeditionCommand(ExpeditionIntent.VoteContinue),"sheldon").state==ExpeditionCommandState.Pending,"continue");p.Tick(.05);}},
                }},
            new Scenario{Id="transfer",Title="Host transfer",Summary="Sanctuary after Ward Zero. The host (Sheldon) wants to extract while the others continue.",Players=3,
                Setup=p=>
                {
                    p.ClearBuilding("ward-zero","zombie-scientist");p.Leave();p.SaveCheckpoint();
                    // As the live transport reports them: measured RTT, jitter and ping timeouts; no endpoint probed or supported.
                    p.Candidates=new[]{new ExpeditionHostCandidate{account="astra",reachable=false,capable=false,loss=0,latencyMs=38,jitterMs=4},
                        new ExpeditionHostCandidate{account="nova",reachable=false,capable=false,loss=.02,latencyMs=21,jitterMs=9}};
                },
                Steps=new[]
                {
                    new Step{Caption="Today: links are measured, but no endpoint is probed or supported, so the host's request to leave is refused, with the reason.",Act=p=>
                        p.Transfer.Prepared(p.Run.PrepareHandoff("preview-transfer-0",new[]{"sheldon"},p.Candidates),"No connection is reachable and capable: endpoint probes and migration are not supported yet.")},
                    new Step{Caption="PREVIEW ONLY: as if endpoints were probed and supported, the run freezes and the best connection (fewest timeouts, then RTT + jitter) is elected.",Act=p=>
                    {
                        p.Candidates=p.Candidates.Select(c=>new ExpeditionHostCandidate{account=c.account,reachable=true,capable=true,loss=c.loss,latencyMs=c.latencyMs,jitterMs=c.jitterMs}).ToArray();
                        p.SimulatedEndpoint=true;p.Transfer.Prepared(p.Run.PrepareHandoff("preview-transfer-1",new[]{"sheldon"},p.Candidates));
                    }},
                    new Step{Caption="PREVIEW ONLY (simulated transport progress): the endpoint is reserved and the package stored; the ACK deadline is set.",Act=p=>{p.SimulatedReserved=p.SimulatedPackage=true;p.SimulatedDeadline=p.Clock+8;}},
                    new Step{Caption="No ACK before the deadline: the transfer aborts. Nobody extracted; the host is unchanged.",Act=p=>
                    {Require(p.Run.AbortHandoff("preview-transfer-1"),"abort");p.Transfer.Aborted("preview-transfer-1","No destination ACK before the timeout.");p.SimulatedReserved=p.SimulatedPackage=false;p.SimulatedDeadline=-1;}},
                    new Step{Caption="PREVIEW ONLY: a simulated ACK lets the rules commit. No endpoint moves: the real transport still has to do that.",Act=p=>
                    {
                        p.SimulatedEndpoint=false;var h=p.Run.PrepareHandoff("preview-transfer-2",new[]{"sheldon"},p.Candidates);p.Transfer.Prepared(h);
                        Require(h!=null&&p.Run.CommitHandoff(h.id,h.nextHost,h.nextEpoch,h.revision),"commit");p.Transfer.Committed(h.id);
                        var save=p.Saved.WithExtractions(p.Run.Snapshot());p.Write(save);p.Saved=save;
                    }},
                }},
            new Scenario{Id="guest",Title="Guest commands",Summary="Viewing as Astra, a guest: her controls go to the host over the command channel (PREVIEW loopback with latency).",
                Setup=p=>{Require(p.Run.Enter("ward-zero"),"enter");p.Kills(4);p.Viewer="astra";},
                Steps=new[]
                {
                    new Step{Caption="As the host, a departure vote while the door is sealed is refused by the authority at once.",Viewer="sheldon",Act=p=>p.Submit(new ExpeditionCommand(ExpeditionIntent.VoteExit))},
                    new Step{Caption="As Astra (guest), the same vote goes to the host: pending, then the host's reply refuses it.",Viewer="astra",Act=p=>p.Submit(new ExpeditionCommand(ExpeditionIntent.VoteExit))},
                    new Step{Caption="Boss down. Astra votes to leave: sent, accepted by the host, and confirmed only once her replicated view shows the vote.",Viewer="astra",Act=p=>
                    {p.Kills(8);Require(p.Run.StartBosses(new[]{"zombie-scientist"}),"bosses");Require(p.Run.DefeatBossAt("zombie-scientist",0,0,8),"boss");p.Submit(new ExpeditionCommand(ExpeditionIntent.VoteExit));}},
                    new Step{Caption="PREVIEW FAULT: the channel loses Astra's next message. No reply and no state change: after 6 s it shows Failed.",Viewer="astra",Act=p=>
                    {p.DropNextGuestCommand=true;p.Submit(new ExpeditionCommand(ExpeditionIntent.Claim,p.FirstReward("astra","boss")));}},
                }},
        };
    }
}
