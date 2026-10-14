using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// DEV PREVIEW ONLY (--ca-expedition-preview; Claude, 2026-10-14). Scripted scenarios that drive a real NativeExpedition through
    /// its public commands so the Expedition screens can be viewed before the mode exists. Not a game mode, not reachable from
    /// the menus. The offers, prices and the extraction transaction below are preview stand-ins for the integration layer
    /// (content catalog, chest/machine adapters, save-then-publish), clearly labelled on screen; the real ones are integration
    /// work (docs/EXPEDITION-PRESENTATION.md). Every allocation, dice roll, vote and timer is the authority's own.
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

        public NativeExpeditionPreview(Action<ExpeditionSavepoint> write=null)
        {
            Write=write??(_=>{});
            Hooks=new ExpeditionIntegrationHooks{Offers=PreviewOffers,Eligible=(account,chip)=>true,Extract=ExtractTransaction,Reprint=PreviewReprint};
            Load(0);
        }

        public ExpeditionViewSources Sources=>new ExpeditionViewSources{Saved=Saved,Transfer=Transfer,Candidates=Candidates,EndpointMigration=false};
        public ExpeditionView View(string viewer=null)=>NativeExpeditionView.Build(Run,viewer??Viewer,Sources);
        /// <summary>The viewer's command path: the host's own commands are local; every other viewer is a guest (no channel yet).</summary>
        public IExpeditionCommands Commands(string viewer=null)
        {
            viewer??=Viewer;return Run.Snapshot().host==viewer?(IExpeditionCommands)new ExpeditionHostCommands(Run,viewer,Hooks):new ExpeditionGuestCommands(viewer);
        }
        /// <summary>Sends a viewer's intent through its real command path and records it for state-driven confirmation.</summary>
        public ExpeditionCommandResult Submit(ExpeditionCommand command,string viewer=null)
        {
            viewer??=Viewer;return Intents.Submit(Commands(viewer),command,View(viewer),Clock);
        }
        /// <summary>A scripted intent from another pilot, as if the host had received it: run through the host's path and recorded for that pilot.</summary>
        public ExpeditionCommandResult Scripted(string viewer,ExpeditionCommand command,Func<ExpeditionCommandResult> send)
        {
            var before=View(viewer);return Intents.Record(viewer,command,send(),before,Clock);
        }

        public void Load(int scenario)
        {
            ScenarioIndex=Math.Max(0,Math.Min(Scenarios.Length-1,scenario));StepIndex=0;Clock=0;Saved=null;FailNextWrite=false;Transfer.Clear();Intents.Clear();Candidates=Array.Empty<ExpeditionHostCandidate>();
            var s=Current;Run=NativeExpedition.Create("preview-"+s.Id,"zombie-city",Party[0],"greatsword",20261014,new[]
                {new ExpeditionInstance{id="ward-zero",target=12},new ExpeditionInstance{id="metro-station",target=10},new ExpeditionInstance{id="clinic",target=8}});
            string[] weapons={"greatsword","rifle","water","daggers"};
            for(int i=1;i<s.Players;i++)Require(Run.Join(Party[i],weapons[i]),"join");
            int[] wallets={420,260,180,90};for(int i=0;i<s.Players;i++)Require(Run.Credit(Party[i],"preview-wallet",wallets[i]),"wallet");
            Viewer=Party[0];s.Setup?.Invoke(this);Caption=s.Summary;
        }
        /// <summary>Plays the next scripted step; false at the end.</summary>
        public bool Next()
        {
            if(Finished)return false;var step=Current.Steps[StepIndex++];if(step.Viewer!=null)Viewer=step.Viewer;step.Act?.Invoke(this);Caption=step.Caption;return true;
        }
        /// <summary>Advances the authority clock as the host does (steps of at most 0.25 s).</summary>
        public void Tick(double dt)
        {
            if(!(dt>0))return;dt=Math.Min(dt,1);Clock+=dt;
            while(dt>0){double step=Math.Min(.25,dt);Run.Tick(step);dt-=step;}
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
        private (int,bool) PreviewReprint(string payer,string victim)
        {
            var v=Run.Snapshot().members.FirstOrDefault(p=>p.account==victim);return v?.carriedBy==payer?(60,false):(90,true);
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
            Require(Run.Enter(id),"enter");var target=Run.Snapshot().instances.First(i=>i.id==id).target;Kills(target);Require(Run.StartBosses(new[]{boss}),"bosses");Require(Run.DefeatBoss(boss,drops),"boss");
        }
        private void Claim(string account,string reward){Require(Run.Claim(account,reward,PreviewOffers(account,reward)),"claim");}
        private string FirstReward(string owner,string source)=>Run.Snapshot().rewards.First(r=>r.owner==owner&&r.source==source&&!r.consumed).id;

        public static readonly Scenario[] Scenarios=
        {
            new Scenario{Id="town",Title="Sanctuary, board and first building",Summary="Four pilots at the sanctuary. Each wallet is personal; only yours is shown.",
                Steps=new[]
                {
                    new Step{Caption="Sheldon buys a chip cache for $150: the chip belongs to the buyer.",Act=p=>{Require(p.Run.Purchase("sheldon","cache-1",150),"purchase");p.Claim("sheldon",p.FirstReward("sheldon","chest"));}},
                    new Step{Caption="The party enters Ward Zero together; the entrance seals behind them.",Act=p=>{Require(p.Run.Enter("ward-zero"),"enter");p.Kills(5);}},
                    new Step{Caption="Ordinary drops are first pickup: Astra grabs one, the other is still open.",Act=p=>{p.Kills(3);Require(p.Run.Drop("walker-17"),"drop");Require(p.Run.Drop("walker-23"),"drop");p.Claim("astra",p.Run.Snapshot().rewards.First(r=>r.source=="monster").id);}},
                    new Step{Caption="Quota met: the Zombie Scientist arrives. The door stays sealed until it dies.",Act=p=>{p.Kills(4);Require(p.Run.StartBosses(new[]{"zombie-scientist"}),"bosses");}},
                    new Step{Caption="Boss down: one reserved Epic+ chip for each survivor at that instant.",Act=p=>Require(p.Run.DefeatBoss("zombie-scientist"),"boss")},
                    new Step{Caption="Sheldon votes to leave: one vote starts the shared 10 s timer.",Act=p=>Require(p.Run.VoteExit("sheldon",true),"vote")},
                    new Step{Caption="Back at the sanctuary: individual and group extraction are unlocked. Checkpoint saved.",Act=p=>{if(p.Run.Snapshot().phase==ExpeditionPhase.Instance)p.Leave();else p.Safe();p.SaveCheckpoint();}},
                }},
            new Scenario{Id="dice",Title="Boss loot: survivors and dice",Summary="Ward Zero, quota met, two bosses. Eligibility freezes when each boss dies.",
                Setup=p=>{Require(p.Run.Enter("ward-zero"),"enter");p.Kills(12);Require(p.Run.StartBosses(new[]{"zombie-scientist","lab-assistant"}),"bosses");},
                Steps=new[]
                {
                    new Step{Caption="Nova goes down before the Scientist dies.",Act=p=>Require(p.Run.Defeat("nova"),"defeat")},
                    new Step{Caption="Scientist down with 5 chips for 3 survivors: 1 each, 2 extra decided by the stored dice.",Act=p=>Require(p.Run.DefeatBoss("zombie-scientist",5),"boss")},
                    new Step{Caption="Kestrel falls after the Scientist died: her reserved chip stays hers.",Act=p=>Require(p.Run.Defeat("kestrel"),"defeat")},
                    new Step{Caption="Lab Assistant down with 1 chip for 2 survivors: fewer chips than survivors, the dice decide.",Act=p=>Require(p.Run.DefeatBoss("lab-assistant",1),"boss")},
                    new Step{Caption="Each survivor opens their own reserved chips: offers are personal.",Act=p=>{p.Claim("sheldon",p.FirstReward("sheldon","boss"));p.Claim("astra",p.FirstReward("astra","boss"));}},
                    new Step{Caption="Independent picks: Sheldon and Astra each choose from their own offers.",Act=p=>
                    {
                        foreach(var a in new[]{"sheldon","astra"}){var r=p.Run.Snapshot().rewards.First(x=>x.owner==a&&x.claimed&&!x.consumed);Require(p.Run.Select(a,r.id,r.offers[a=="sheldon"?0:1],c=>true),"select");}
                    }},
                }},
            new Scenario{Id="extract",Title="Extraction: alone and as a group",Summary="Ward Zero cleared; the party is back at the sanctuary.",
                Setup=p=>{p.ClearBuilding("ward-zero","zombie-scientist");p.Leave();p.SaveCheckpoint();},
                Steps=new[]
                {
                    new Step{Caption="Astra extracts alone: the save is written first, then the run publishes it. The others continue.",Act=p=>Require(p.Scripted("astra",new ExpeditionCommand(ExpeditionIntent.Extract),()=>p.ExtractTransaction("astra")).state==ExpeditionCommandState.Pending,"extract")},
                    new Step{Caption="Group extraction needs a strict majority of connected living pilots: Sheldon and Nova vote.",Act=p=>{Require(p.Run.VoteExtraction("sheldon",true),"vote");Require(p.Run.VoteExtraction("nova",true),"vote");}},
                    new Step{Caption="Nova changes her mind: below the majority the countdown cancels.",Act=p=>Require(p.Run.VoteExtraction("nova",false),"cancel")},
                    new Step{Caption="Kestrel tries to extract but the save fails: she stays in the run and is told so.",Viewer="kestrel",Act=p=>{p.FailNextWrite=true;p.Scripted("kestrel",new ExpeditionCommand(ExpeditionIntent.Extract),()=>p.ExtractTransaction("kestrel"));}},
                }},
            new Scenario{Id="victory",Title="Victory: extract or go again",Summary="Every building and the world boss are down. Each pilot chooses.",
                Setup=p=>
                {
                    p.ClearBuilding("ward-zero","zombie-scientist");p.Leave();p.ClearBuilding("metro-station","conductor");p.Leave();p.ClearBuilding("clinic","head-nurse");p.Leave();
                    Require(p.Run.BeginFinal()&&p.Run.FinishFinal(),"final");p.Safe();p.SaveCheckpoint();
                },
                Steps=new[]
                {
                    new Step{Caption="Astra extracts with her build; the save confirms it.",Act=p=>Require(p.Scripted("astra",new ExpeditionCommand(ExpeditionIntent.Extract),()=>p.ExtractTransaction("astra")).state==ExpeditionCommandState.Pending,"extract")},
                    new Step{Caption="Kestrel extracts too. Nova stays in the run.",Act=p=>Require(p.Scripted("kestrel",new ExpeditionCommand(ExpeditionIntent.Extract),()=>p.ExtractTransaction("kestrel")).state==ExpeditionCommandState.Pending,"extract")},
                    new Step{Caption="The host starts loop 2: buildings reset, the clock and builds carry on, enemies scale up.",Act=p=>Require(p.Run.NextLoop(),"loop")},
                }},
            new Scenario{Id="transfer",Title="Host transfer",Summary="Sanctuary after Ward Zero. The host (Sheldon) wants to extract while the others continue.",Players=3,
                Setup=p=>
                {
                    p.ClearBuilding("ward-zero","zombie-scientist");p.Leave();p.SaveCheckpoint();
                    p.Candidates=new[]{new ExpeditionHostCandidate{account="astra",reachable=true,capable=true,loss=0,latencyMs=38,jitterMs=4},
                        new ExpeditionHostCandidate{account="nova",reachable=true,capable=true,loss=.02,latencyMs=21,jitterMs=9}};
                },
                Steps=new[]
                {
                    new Step{Caption="The host asks to leave: the run freezes and the best connection (Astra) is elected.",Act=p=>p.Transfer.Prepared(p.Run.PrepareHandoff("preview-transfer-1",new[]{"sheldon"},p.Candidates))},
                    new Step{Caption="No endpoint migration exists yet, so no ACK arrives: the transfer times out and aborts. Nobody extracted.",Act=p=>{Require(p.Run.AbortHandoff("preview-transfer-1"),"abort");p.Transfer.Aborted("preview-transfer-1","No destination ACK before the timeout.");}},
                    new Step{Caption="PREVIEW ONLY: a simulated ACK lets the rules commit. The transport still has to move the endpoint.",Act=p=>
                    {
                        var h=p.Run.PrepareHandoff("preview-transfer-2",new[]{"sheldon"},p.Candidates);p.Transfer.Prepared(h);
                        Require(h!=null&&p.Run.CommitHandoff(h.id,h.nextHost,h.nextEpoch,h.revision),"commit");p.Transfer.Committed(h.id);
                        var save=p.Saved.WithExtractions(p.Run.Snapshot());p.Write(save);p.Saved=save;
                    }},
                }},
            new Scenario{Id="guest",Title="Guest view and refusals",Summary="Viewing as Astra, a guest: there is no guest command channel yet, so every control says so.",
                Setup=p=>{Require(p.Run.Enter("ward-zero"),"enter");p.Kills(4);p.Viewer="astra";},
                Steps=new[]
                {
                    new Step{Caption="As the host, a departure vote while the door is sealed is refused by the authority.",Viewer="sheldon",Act=p=>p.Submit(new ExpeditionCommand(ExpeditionIntent.VoteExit))},
                    new Step{Caption="As Astra (guest) the same vote cannot be sent: it shows unavailable, never as done.",Viewer="astra",Act=p=>p.Submit(new ExpeditionCommand(ExpeditionIntent.VoteExit))},
                }},
        };
    }
}
