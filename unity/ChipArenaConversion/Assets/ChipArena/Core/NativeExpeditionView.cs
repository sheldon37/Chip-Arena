using System;
using System.Collections.Generic;
using System.Linq;

namespace ChipArena.UnityConversion
{
    // Expedition presentation contract (Claude, 2026-10-14; docs/EXPEDITION-PRESENTATION.md). The one projection every Expedition
    // screen reads: the run as ONE account sees it. Built where the authority lives (the host, from NativeExpedition); the same
    // field-only DTO is the replication contract for guests (JsonUtility and the managed codec both round-trip it).
    //  - Public run state for everyone: phase, area, buildings, clock, loop, scaling read-out, members, votes, countdowns, the
    //    dice records, ordinary drops and reserved boss rewards (owner and claimed state only), host-transfer state.
    //  - Personal state for the viewing account only: wallet, catch-up, held chips, its own rewards (offers only while a pick is
    //    pending), extraction eligibility and save confirmation.
    // It never carries another account's offers, wallet, chips, purchases or catch-up rewards. Presentation reads it and never
    // changes the authority: every number (prices, wallets, allocation, dice, eligibility, scaling) comes from there.

    [Serializable] public sealed class ExpeditionViewMember
    {
        public string account, name, weapon, carriedBy;public int reprintCost, seat;
        public bool connected, alive, safe, extracted, choosing, host, exitVote, extractVote;
    }
    [Serializable] public sealed class ExpeditionViewInstance
    {
        public string id;public int target, progress;public bool entered, cleared, current;
        public string[] bosses=Array.Empty<string>(), defeated=Array.Empty<string>();
    }
    /// <summary>Public loot: ordinary monster drops (source "monster": first pickup, owner is whoever took it) and reserved boss
    /// rewards (source "boss": owner is the survivor it was reserved for). No offers: those go to the owner only.</summary>
    [Serializable] public sealed class ExpeditionViewLoot
    {
        public string id, source, owner, boss;public bool claimed, consumed;
    }
    /// <summary>One of the viewer's own rewards (any source). Offers only while claimed and not yet selected.</summary>
    [Serializable] public sealed class ExpeditionViewReward
    {
        public string id, source, boss, selected;public bool claimed, consumed, highQuality;
        public string[] offers=Array.Empty<string>();
    }
    [Serializable] public sealed class ExpeditionViewPersonal
    {
        /// <summary>The viewing account; null when it is not a member of this run.</summary>
        public string account, carriedBy;
        public int wallet, catchup, initialCatchup;
        public bool connected, alive, safe, extracted, canExtract, extractionSaved;
        public string[] carrying=Array.Empty<string>(), perks=Array.Empty<string>();
        public ChipCount[] chips=Array.Empty<ChipCount>();
        public ExpeditionViewReward[] rewards=Array.Empty<ExpeditionViewReward>();
    }
    public enum ExpeditionTransferStage { None, Frozen, Committed, Aborted, Refused }
    [Serializable] public sealed class ExpeditionViewTransfer
    {
        /// <summary>The authority is frozen for a handoff (NativeExpedition.Migrating).</summary>
        public bool active;
        public ExpeditionTransferStage stage;
        public string id, oldHost, nextHost, reason;
        public int nextEpoch;public long revision;
        public string[] leaving=Array.Empty<string>();
        /// <summary>The transport can move the session endpoint. No build has this yet, so the ACK step shows as unavailable.</summary>
        public bool endpointMigration, packageStored, endpointReserved, ackReceived;
        /// <summary>Deadline and clock use the host transport monotonic seconds, never gameplay elapsed or guest time. -1 means absent.</summary>
        public double deadline=-1, clock=-1;
        /// <summary>Transport-reported connection quality (empty when the transport reports none).</summary>
        public ExpeditionHostCandidate[] candidates=Array.Empty<ExpeditionHostCandidate>();
    }
    [Serializable] public sealed class ExpeditionView
    {
        public ExpeditionHubView hub;
        public ExpeditionClosureView[] closures=Array.Empty<ExpeditionClosureView>();
        public ExpeditionBossArrivalView[] bossArrivals=Array.Empty<ExpeditionBossArrivalView>();
        public ExpeditionAreaBarrierView areaBarrier=new ExpeditionAreaBarrierView();
        public int schema=1, epoch, loop, living, extractNeeded, difficultyParty, difficultyStep;
        public long revision, savedRevision=-1;
        public string run, world, area, host, viewer;
        public ExpeditionPhase phase;
        public double elapsed, exitAt=-1, extractAt=-1, exitSeconds, extractSeconds;
        public bool loading, migrating, finalUnlocked, extractionUnlocked;
        public string[] exitVotes=Array.Empty<string>(), extractVotes=Array.Empty<string>(), continueVotes=Array.Empty<string>();
        public ExpeditionWorldAnchor[] anchors=Array.Empty<ExpeditionWorldAnchor>();
        public ExpeditionChestOffer[] chests=Array.Empty<ExpeditionChestOffer>();
        public ExpeditionDifficulty difficulty=new ExpeditionDifficulty();
        public ExpeditionViewMember[] members=Array.Empty<ExpeditionViewMember>();
        public ExpeditionViewInstance[] instances=Array.Empty<ExpeditionViewInstance>();
        public ExpeditionViewLoot[] loot=Array.Empty<ExpeditionViewLoot>();
        public ExpeditionDice[] dice=Array.Empty<ExpeditionDice>();
        public ExpeditionViewPersonal mine=new ExpeditionViewPersonal();
        public ExpeditionViewTransfer transfer=new ExpeditionViewTransfer();
        // Methods, not properties: the replicated JSON carries fields only.
        public bool ViewerIsHost()=>!string.IsNullOrEmpty(viewer)&&viewer==host;
        public ExpeditionViewMember Member(string account){foreach(var m in members)if(m.account==account)return m;return null;}
        public ExpeditionViewInstance CurrentBuilding(){foreach(var i in instances)if(i.current)return i;return null;}
        public ExpeditionViewReward Reward(string id){foreach(var r in mine.rewards)if(r.id==id)return r;return null;}
    }

    /// <summary>
    /// What the integration knows beyond the rules state. Every field is optional: absent sources show as "not confirmed" or
    /// "not available", never as success.
    /// </summary>
    public sealed class ExpeditionViewSources
    {
        /// <summary>The authority's scaling configuration (default: ExpeditionScaling's documented provisional values).</summary>
        public ExpeditionDirector Director;
        public Func<ExpeditionHubView> Hub;
        public ExpeditionScaling Scaling;
        /// <summary>The last savepoint NativeExpeditionSaveStore.Write durably wrote. Extraction is only "saved" once its tombstone is in it.</summary>
        public ExpeditionSavepoint Saved;
        /// <summary>The integration's record of its PrepareHandoff / CommitHandoff / AbortHandoff calls.</summary>
        public ExpeditionTransferLog Transfer;
        /// <summary>Transport-reported host candidates (reachable, capable, loss, latency, jitter).</summary>
        public ExpeditionHostCandidate[] Candidates;
        /// <summary>The transport can move the endpoint (no build has this yet).</summary>
        public bool EndpointMigration;
        public bool PackageStored, EndpointReserved, AckReceived;public double Deadline=-1, TransportClock=-1;
        public Func<string,int> ReprintCost;
        public Func<string,string> DisplayName;
        /// <summary>Current authority account-to-pilot mapping; zero means no bound seat.</summary>
        public Func<string,int> Seat;
        public ExpeditionWorldAnchor[] Anchors;
        public ExpeditionChestOffer[] Chests;
    }

    /// <summary>
    /// The integration's record of a host-transfer transaction, so the overlay can show the outcome after the authority has left
    /// the frozen state. Outcomes are only reported once the authority reflects them (see NativeExpeditionView.Build).
    /// </summary>
    public sealed class ExpeditionTransferLog
    {
        public ExpeditionHandoff Handoff{get;private set;}
        public ExpeditionTransferStage Outcome{get;private set;}
        public string Reason{get;private set;}
        /// <summary>PrepareHandoff's result: the frozen proposal, or null when the authority refused it.</summary>
        public void Prepared(ExpeditionHandoff handoff,string refusal=null)
        {
            Handoff=handoff==null?null:new ExpeditionHandoff{id=handoff.id,oldHost=handoff.oldHost,nextHost=handoff.nextHost,nextEpoch=handoff.nextEpoch,revision=handoff.revision,leaving=(string[])handoff.leaving.Clone()};
            Outcome=handoff==null?ExpeditionTransferStage.Refused:ExpeditionTransferStage.Frozen;Reason=handoff==null?refusal??"The authority refused the transfer (no eligible connection, or a leaving player cannot extract).":null;
        }
        /// <summary>CommitHandoff returned true for this transaction.</summary>
        public void Committed(string transaction){if(Handoff!=null&&Handoff.id==transaction){Outcome=ExpeditionTransferStage.Committed;Reason=null;}}
        /// <summary>AbortHandoff was called for this transaction (timeout, failed ACK, failed package write...).</summary>
        public void Aborted(string transaction,string reason){if(Handoff!=null&&Handoff.id==transaction){Outcome=ExpeditionTransferStage.Aborted;Reason=reason;}}
        public void Clear(){Handoff=null;Outcome=ExpeditionTransferStage.None;Reason=null;}
    }

    public static class NativeExpeditionView
    {
        /// <summary>Strict majority of connected living participants (NativeExpedition.RefreshVotes).</summary>
        public static int Majority(int living)=>living/2+1;
        private static string DisplayName(string name,string fallback)
        {
            if(string.IsNullOrWhiteSpace(name))return fallback;
            var clean=new string(name.Where(c=>c!='<'&&c!='>'&&!char.IsControl(c)&&char.GetUnicodeCategory(c)!=System.Globalization.UnicodeCategory.Format).Take(20).ToArray()).Trim();
            return clean.Length==0?fallback:clean;
        }
        /// <summary>A boss reward's dice record key: the reward id without its ":serial" suffix (NativeExpedition.DefeatBoss).</summary>
        public static string BossKey(string rewardId){int i=rewardId==null?-1:rewardId.LastIndexOf(':');return i>0?rewardId.Substring(0,i):null;}

        /// <summary>
        /// Projects the authority for one account. Runs on the host; guests receive the result. Pure read: the authority is
        /// unchanged (Snapshot is detached). Bounded: public loot keeps everything still open plus what was settled in the current
        /// loop and area; personal rewards keep the open ones plus those settled this loop; dice keep the current loop.
        /// </summary>
        public static ExpeditionView Build(NativeExpedition run,string viewer,ExpeditionViewSources sources=null)
        {
            if(run==null)throw new ArgumentNullException(nameof(run));
            var s=run.Snapshot();sources??=new ExpeditionViewSources();var scaling=sources.Scaling??new ExpeditionScaling();
            string here=s.loop+":"+s.area+":",loopKey=s.loop+":";
            bool Live(ExpeditionMember p)=>p.connected&&p.alive&&!p.extracted;
            int living=s.members.Count(Live),party=Math.Max(1,Math.Min(8,s.members.Count(p=>p.connected&&!p.extracted)));
            var v=new ExpeditionView
            {
                epoch=s.epoch,revision=s.revision,run=s.run,world=s.world,area=s.area,host=s.host,viewer=viewer,phase=s.phase,loop=s.loop,
                elapsed=s.elapsed,exitAt=s.exitAt,extractAt=s.extractAt,exitSeconds=NativeExpedition.ExitSeconds,extractSeconds=NativeExpedition.ExtractSeconds,loading=s.loading,migrating=run.Migrating,finalUnlocked=run.FinalUnlocked,
                extractionUnlocked=run.ExtractionUnlocked,living=living,extractNeeded=Majority(living),
                exitVotes=(string[])s.exitVotes.Clone(),extractVotes=(string[])s.extractVotes.Clone(),continueVotes=(string[])s.continueVotes.Clone(),
                anchors=(sources.Anchors??s.anchors).Where(a=>a!=null&&(a.kind!="reward"||s.rewards.Any(r=>r.id==a.id&&(r.source=="monster"||r.source=="boss")))).Select(a=>a.Copy()).ToArray(),chests=(sources.Chests??s.chests).Select(c=>{var o=c.Copy();o.sold|=s.rewards.Any(r=>r.source=="chest"&&r.id==s.loop+":"+(s.anchors.FirstOrDefault(a=>a.id==o.id&&a.kind=="chest")?.area??s.area)+":chest:"+o.generation);return o;}).ToArray(),
                difficultyParty=party,difficultyStep=(int)Math.Floor(s.elapsed/scaling.timeStepSeconds),
                difficulty=scaling.Evaluate(s.elapsed,party,s.loop,s.phase==ExpeditionPhase.Instance),
                savedRevision=sources.Saved!=null&&sources.Saved.checkpoint!=null&&sources.Saved.checkpoint.run==s.run?sources.Saved.checkpoint.revision:-1,
            };
            v.members=s.members.Select(p=>new ExpeditionViewMember{account=p.account,seat=Math.Max(0,sources.Seat?.Invoke(p.account)??0),name=DisplayName(sources.DisplayName?.Invoke(p.account),p.account),weapon=p.weapon,carriedBy=p.carriedBy,reprintCost=p.alive?0:Math.Max(0,sources.ReprintCost?.Invoke(p.account)??0),connected=p.connected,alive=p.alive,
                safe=p.safe,extracted=p.extracted,choosing=run.IsChoosing(p.account),host=p.account==s.host,exitVote=s.exitVotes.Contains(p.account),extractVote=s.extractVotes.Contains(p.account)}).ToArray();
            v.instances=s.instances.Select(i=>new ExpeditionViewInstance{id=i.id,target=i.target,progress=i.progress,entered=i.entered,cleared=i.cleared,
                current=s.phase==ExpeditionPhase.Instance&&i.id==s.area,bosses=(string[])i.bosses.Clone(),defeated=(string[])i.defeated.Clone()}).ToArray();
            bool Keep(ExpeditionReward r)=>!r.consumed||r.id.StartsWith(here,StringComparison.Ordinal);
            v.loot=s.rewards.Where(r=>(r.source=="monster"||r.source=="boss")&&Keep(r))
                .Select(r=>new ExpeditionViewLoot{id=r.id,source=r.source,owner=r.owner,boss=r.source=="boss"?BossKey(r.id):null,claimed=r.claimed,consumed=r.consumed}).ToArray();
            v.dice=s.dice.Where(d=>d.boss.StartsWith(loopKey,StringComparison.Ordinal)).Select(d=>d.Copy()).ToArray();
            var me=s.members.FirstOrDefault(p=>p.account==viewer);
            if(me!=null)
            {
                var saved=sources.Saved!=null&&sources.Saved.checkpoint!=null&&sources.Saved.checkpoint.run==s.run&&sources.Saved.extracted!=null&&sources.Saved.extracted.Contains(viewer);
                v.mine=new ExpeditionViewPersonal{account=me.account,carriedBy=me.carriedBy,wallet=me.wallet,catchup=me.catchup,initialCatchup=me.initialCatchup,
                    connected=me.connected,alive=me.alive,safe=me.safe,extracted=me.extracted,canExtract=run.CanExtract(viewer),extractionSaved=me.extracted&&saved,
                    carrying=s.members.Where(p=>p.carriedBy==viewer).Select(p=>p.account).ToArray(),perks=(string[])me.perks.Clone(),
                    chips=me.chips.Select(c=>new ChipCount{id=c.id,count=c.count}).ToArray(),
                    rewards=s.rewards.Where(r=>r.owner==viewer&&(!r.consumed||r.id.StartsWith(loopKey,StringComparison.Ordinal))).Select(r=>new ExpeditionViewReward{id=r.id,source=r.source,boss=r.source=="boss"?BossKey(r.id):null,
                        selected=r.selected,claimed=r.claimed,consumed=r.consumed,highQuality=r.highQuality,offers=r.claimed&&!r.consumed?(string[])r.offers.Clone():Array.Empty<string>()}).ToArray()};
            }
            v.hub=sources.Hub?.Invoke();
            v.closures=sources.Director?.Closures??Array.Empty<ExpeditionClosureView>();v.bossArrivals=sources.Director?.BossArrivals??Array.Empty<ExpeditionBossArrivalView>();v.areaBarrier=sources.Director?.Barrier??new ExpeditionAreaBarrierView();
            v.transfer=Transfer(run,s,sources);
            return v;
        }

        // Host-transfer state: the authority's frozen flag, plus the integration's log, reported only where the authority agrees.
        private static ExpeditionViewTransfer Transfer(NativeExpedition run,ExpeditionSnapshot s,ExpeditionViewSources sources)
        {
            var t=new ExpeditionViewTransfer{active=run.Migrating,endpointMigration=sources.EndpointMigration,packageStored=sources.PackageStored,endpointReserved=sources.EndpointReserved,ackReceived=sources.AckReceived,deadline=sources.Deadline,clock=sources.TransportClock,
                candidates=(sources.Candidates??Array.Empty<ExpeditionHostCandidate>()).Where(c=>c!=null).Select(c=>new ExpeditionHostCandidate{account=c.account,reachable=c.reachable,
                    capable=c.capable,loss=c.loss,latencyMs=c.latencyMs,jitterMs=c.jitterMs}).ToArray()};
            var log=sources.Transfer;var h=log?.Handoff;
            if(h!=null){t.id=h.id;t.oldHost=h.oldHost;t.nextHost=h.nextHost;t.nextEpoch=h.nextEpoch;t.revision=h.revision;t.leaving=(string[])h.leaving.Clone();}
            if(run.Migrating){t.stage=ExpeditionTransferStage.Frozen;return t;}
            if(log==null||log.Outcome==ExpeditionTransferStage.None)return t;
            t.reason=log.Reason;
            switch(log.Outcome)
            {
                case ExpeditionTransferStage.Refused:t.stage=ExpeditionTransferStage.Refused;break;
                // Confirm a commit only when the authority shows the new host at the new epoch.
                case ExpeditionTransferStage.Committed:
                    if(h!=null&&s.host==h.nextHost&&s.epoch==h.nextEpoch)t.stage=ExpeditionTransferStage.Committed;
                    else{t.stage=ExpeditionTransferStage.Refused;t.reason="The commit is not reflected by the authority.";}break;
                case ExpeditionTransferStage.Aborted:
                    t.stage=ExpeditionTransferStage.Aborted;if(h!=null&&s.host!=h.oldHost)t.reason=(t.reason??"Aborted")+" (host changed since).";break;
                // The log says frozen but the authority is not: the transaction ended without a recorded outcome.
                default:t.stage=ExpeditionTransferStage.Aborted;t.reason="The transfer ended without a commit.";break;
            }
            return t;
        }

        /// <summary>
        /// Receiving side of the contract: Unity's JsonUtility decodes absent strings as "". Restores null for the optional ids so a
        /// decoded view equals the host-built one (presentation also treats "" as absent).
        /// </summary>
        public static ExpeditionView Normalize(ExpeditionView v)
        {
            if(v==null)return null;static string N(string s)=>string.IsNullOrEmpty(s)?null:s;
            v.closures??=Array.Empty<ExpeditionClosureView>();v.bossArrivals??=Array.Empty<ExpeditionBossArrivalView>();v.areaBarrier??=new ExpeditionAreaBarrierView();v.areaBarrier.target=N(v.areaBarrier.target);v.areaBarrier.layout=N(v.areaBarrier.layout);
            v.continueVotes??=Array.Empty<string>();v.anchors??=Array.Empty<ExpeditionWorldAnchor>();v.chests??=Array.Empty<ExpeditionChestOffer>();
            v.members??=Array.Empty<ExpeditionViewMember>();v.loot??=Array.Empty<ExpeditionViewLoot>();v.instances??=Array.Empty<ExpeditionViewInstance>();v.dice??=Array.Empty<ExpeditionDice>();v.exitVotes??=Array.Empty<string>();v.extractVotes??=Array.Empty<string>();
            v.viewer=N(v.viewer);v.mine??=new ExpeditionViewPersonal();v.transfer??=new ExpeditionViewTransfer();v.difficulty??=new ExpeditionDifficulty();
            v.mine.rewards??=Array.Empty<ExpeditionViewReward>();v.mine.chips??=Array.Empty<ChipCount>();v.mine.perks??=Array.Empty<string>();v.mine.carrying??=Array.Empty<string>();
            v.mine.account=N(v.mine.account);v.mine.carriedBy=N(v.mine.carriedBy);
            foreach(var m in v.members){m.carriedBy=N(m.carriedBy);m.name=N(m.name)??m.account;}
            foreach(var a in v.anchors)a.building=N(a.building);
            foreach(var l in v.loot){l.owner=N(l.owner);l.boss=N(l.boss);}
            foreach(var r in v.mine.rewards){r.boss=N(r.boss);r.selected=N(r.selected);r.offers??=Array.Empty<string>();}
            var t=v.transfer;t.id=N(t.id);t.oldHost=N(t.oldHost);t.nextHost=N(t.nextHost);t.reason=N(t.reason);
            return v;
        }

        /// <summary>
        /// Privacy audit for checks and integration asserts: the reasons (empty when clean) this view would leak another account's
        /// private state given the authority snapshot it came from.
        /// </summary>
        public static List<string> Leaks(ExpeditionView v,ExpeditionSnapshot s)
        {
            var leaks=new List<string>();string viewer=v.viewer;
            if(!string.IsNullOrEmpty(v.mine.account)&&v.mine.account!=viewer)leaks.Add("personal block belongs to "+v.mine.account);
            foreach(var r in v.mine.rewards)
            {
                var a=s.rewards.FirstOrDefault(x=>x.id==r.id);
                if(a==null||a.owner!=viewer)leaks.Add("reward "+r.id+" is not the viewer's");
                if(r.offers.Length>0&&(a==null||!a.claimed||a.consumed))leaks.Add("offers outside a pending pick on "+r.id);
            }
            foreach(var l in v.loot)
            {
                var a=s.rewards.FirstOrDefault(x=>x.id==l.id);
                if(a==null||a.source!="monster"&&a.source!="boss")leaks.Add("private reward "+l.id+" in public loot");
            }
            // A reward anchor names its reward in public: only public loot may have one (a purchase's or catch-up's would reveal it).
            foreach(var a in v.anchors)
                if(a!=null&&a.kind=="reward"){var r=s.rewards.FirstOrDefault(x=>x.id==a.id);if(r!=null&&r.source!="monster"&&r.source!="boss")leaks.Add("world anchor of private reward "+a.id);}
            var mine=s.members.FirstOrDefault(p=>p.account==viewer);
            if(mine==null&&(v.mine.wallet!=0||v.mine.chips.Length>0||v.mine.rewards.Length>0))leaks.Add("personal state for a non-member");
            if(mine!=null&&v.mine.wallet!=mine.wallet)leaks.Add("wallet is not the viewer's");
            return leaks;
        }
    }
}
