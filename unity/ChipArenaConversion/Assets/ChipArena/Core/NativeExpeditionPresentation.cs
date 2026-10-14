using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ChipArena.UnityConversion
{
    /// <summary>Semantic tones of the Expedition screens; Runtime maps them onto NativeCyberTheme (text-safe tones only).</summary>
    public enum ExpeditionTone { Ink, Muted, Cyan, CyanSoft, Magenta, Warn, Good, Danger }
    public enum ExpeditionStep { Pending, Active, Done, Unavailable, Failed }

    /// <summary>
    /// What the Expedition screens say, from an ExpeditionView only (Claude, 2026-10-14; docs/EXPEDITION-PRESENTATION.md). Engine-free and
    /// pure: no rule is re-decided here. Counts, owners, eligibility, winners and times are read from the view; thresholds are the
    /// authority's documented ones as the view reports them. Runtime/NativeExpeditionHud draws these models.
    /// </summary>
    public static class NativeExpeditionPresentation
    {
        // ---- Text ----------------------------------------------------------------------------------------------------------
        /// <summary>A replicated id is present. Unity's JsonUtility decodes a null string as "", so empty means absent.</summary>
        public static bool Has(string s)=>!string.IsNullOrEmpty(s);
        private static string Who(string s)=>Has(s)?s.ToUpperInvariant():"?";
        /// <summary>"ward-zero" to "WARD ZERO"; a boss dice key ("0:ward-zero:boss:scientist") to its boss id.</summary>
        public static string Name(string id)
        {
            if(string.IsNullOrEmpty(id))return "";int b=id.LastIndexOf(":boss:",StringComparison.Ordinal);if(b>=0)id=id.Substring(b+6);
            return id.Replace('-',' ').Replace('_',' ').ToUpperInvariant();
        }
        /// <summary>Elapsed time as m:ss or h:mm:ss (never negative).</summary>
        public static string Clock(double seconds)
        {
            if(!(seconds>0))seconds=0;long t=(long)Math.Floor(seconds);long h=t/3600,m=t/60%60,s=t%60;
            return h>0?h+":"+m.ToString("00",CultureInfo.InvariantCulture)+":"+s.ToString("00",CultureInfo.InvariantCulture):m+":"+s.ToString("00",CultureInfo.InvariantCulture);
        }
        /// <summary>Whole seconds left on an authority deadline (rounded up; 0 when due).</summary>
        public static int Left(double at,double elapsed)=>at<0?0:Math.Max(0,(int)Math.Ceiling(at-elapsed-1e-9));
        public static string Multiplier(double x)=>"x"+x.ToString("0.00",CultureInfo.InvariantCulture);
        public static string Money(int amount)=>"$"+amount.ToString("N0",CultureInfo.InvariantCulture);

        // ---- World / town board ----------------------------------------------------------------------------------------------
        public struct BoardRow
        {
            public string id, name, state, detail;public ExpeditionTone tone;public float fill;public bool current, sealedDoor, cleared;
            public int progress, target, bossesDown, bosses;
        }
        /// <summary>One row per building: open, sealed in progress, boss fight, cleared (door open), from instances[] only.</summary>
        public static BoardRow[] Board(ExpeditionView v)
        {
            var rows=new BoardRow[v.instances.Length];
            for(int i=0;i<rows.Length;i++)
            {
                var b=v.instances[i];var r=new BoardRow{id=b.id,name=Name(b.id),current=b.current,cleared=b.cleared,progress=b.progress,target=b.target,
                    bossesDown=b.defeated.Length,bosses=b.bosses.Length,fill=b.target>0?Math.Min(1f,b.progress/(float)b.target):0};
                if(b.cleared){r.state="CLEARED";r.tone=ExpeditionTone.Good;r.fill=1;r.detail=b.current?"Door open · vote to leave":"Boss down";}
                else if(b.current&&b.bosses.Length>0){r.state="BOSS";r.tone=ExpeditionTone.Magenta;r.sealedDoor=true;r.detail="Door sealed · "+b.defeated.Length+" / "+b.bosses.Length+" bosses down";}
                else if(b.current){r.state="SEALED";r.tone=ExpeditionTone.Warn;r.sealedDoor=true;r.detail=b.progress>=b.target?"Quota met · boss incoming":"Door sealed · quota "+b.progress+" / "+b.target;}
                else if(b.entered){r.state="UNFINISHED";r.tone=ExpeditionTone.Warn;r.detail="Quota "+b.progress+" / "+b.target;}
                else{r.state="OPEN";r.tone=ExpeditionTone.Cyan;r.detail="Quota "+b.target+" · enter as a party";}
                rows[i]=r;
            }
            return rows;
        }
        /// <summary>The final outdoor boss area: locked until every building is cleared (FinalUnlocked), then open / engaged / won.</summary>
        public static BoardRow Final(ExpeditionView v)
        {
            int cleared=v.instances.Count(i=>i.cleared),total=v.instances.Length;
            var r=new BoardRow{id="final",name="WORLD BOSS",progress=cleared,target=total,fill=total>0?cleared/(float)total:0};
            if(v.phase==ExpeditionPhase.Victory||v.phase==ExpeditionPhase.Ended){r.state="DEFEATED";r.tone=ExpeditionTone.Good;r.fill=1;r.cleared=true;r.detail="Loop "+(v.loop+1)+" complete";}
            else if(v.phase==ExpeditionPhase.FinalBoss){r.state="ENGAGED";r.tone=ExpeditionTone.Magenta;r.current=true;r.fill=1;r.detail="Final area";}
            else if(v.finalUnlocked){r.state="UNLOCKED";r.tone=ExpeditionTone.Magenta;r.fill=1;r.detail="Every building cleared";}
            else{r.state="LOCKED";r.tone=ExpeditionTone.Muted;r.detail=cleared+" / "+total+" buildings cleared";}
            return r;
        }
        /// <summary>The one-line objective for the minimal HUD.</summary>
        public static string Objective(ExpeditionView v,out ExpeditionTone tone)
        {
            tone=ExpeditionTone.Cyan;
            if(v.migrating){tone=ExpeditionTone.Warn;return "HOST TRANSFER · RUN FROZEN";}
            if(v.loading){tone=ExpeditionTone.Muted;return "LOADING · CLOCK PAUSED";}
            switch(v.phase)
            {
                case ExpeditionPhase.Instance:
                {
                    var b=v.CurrentBuilding();if(b==null)return Name(v.area);string n=Name(b.id);
                    if(b.cleared){tone=ExpeditionTone.Good;return n+" CLEARED · VOTE TO LEAVE";}
                    if(b.bosses.Length>0){tone=ExpeditionTone.Magenta;return n+" · BOSS "+b.defeated.Length+" / "+b.bosses.Length+" DOWN";}
                    if(b.progress>=b.target){tone=ExpeditionTone.Warn;return n+" · QUOTA MET · BOSS INCOMING";}
                    return n+" · QUOTA "+b.progress+" / "+b.target;
                }
                case ExpeditionPhase.FinalBoss:tone=ExpeditionTone.Magenta;return "WORLD BOSS · LOOP "+(v.loop+1);
                case ExpeditionPhase.Victory:tone=ExpeditionTone.Warn;return "VICTORY · EXTRACT OR GO AGAIN";
                case ExpeditionPhase.Ended:tone=ExpeditionTone.Muted;return "RUN ENDED";
                default:
                    if(v.finalUnlocked){tone=ExpeditionTone.Magenta;return "SANCTUARY · FINAL AREA OPEN";}
                    return "SANCTUARY · "+v.instances.Count(i=>i.cleared)+" / "+v.instances.Length+" BUILDINGS CLEARED";
            }
        }

        // ---- Votes and countdowns ----------------------------------------------------------------------------------------------
        public struct Countdown
        {
            public bool active, unanimous, voted, open;public int seconds, votes, needed;public float fraction;public string label, detail;public ExpeditionTone tone;
        }
        /// <summary>The building departure vote: one living vote starts the shared timer, zero cancels, unanimity leaves at once.</summary>
        public static Countdown Departure(ExpeditionView v)
        {
            var b=v.CurrentBuilding();var c=new Countdown{open=v.phase==ExpeditionPhase.Instance&&b!=null&&b.cleared&&!v.migrating&&!v.loading,votes=v.exitVotes.Length,needed=v.living,
                voted=Has(v.viewer)&&v.exitVotes.Contains(v.viewer),label="LEAVE "+Name(v.area),tone=ExpeditionTone.Cyan};
            c.active=c.open&&v.exitAt>=0;c.unanimous=c.active&&c.votes>0&&c.votes>=c.needed;c.seconds=c.active?Left(v.exitAt,v.elapsed):0;c.fraction=Fraction(v.exitAt,v.elapsed,v.exitSeconds,c.active);
            c.detail=!c.open?"Voting opens once the door does.":c.unanimous?"Everyone voted · leaving now":c.active?c.votes+" / "+c.needed+" voted · leaving in "+c.seconds+"s":"No votes · one vote starts the timer";
            return c;
        }
        /// <summary>The group extraction vote: a strict majority of connected living players starts it; below that it cancels.</summary>
        public static Countdown GroupExtraction(ExpeditionView v)
        {
            bool safe=v.phase==ExpeditionPhase.Town||v.phase==ExpeditionPhase.Victory;
            var c=new Countdown{open=safe&&v.extractionUnlocked&&!v.migrating&&!v.loading,votes=v.extractVotes.Length,needed=v.extractNeeded,
                voted=Has(v.viewer)&&v.extractVotes.Contains(v.viewer),label="GROUP EXTRACTION",tone=ExpeditionTone.Warn};
            c.active=c.open&&v.extractAt>=0;c.seconds=c.active?Left(v.extractAt,v.elapsed):0;c.fraction=Fraction(v.extractAt,v.elapsed,v.extractSeconds,c.active);
            c.detail=v.mine.extracted?"You have left the run.":v.migrating?"Frozen during the host transfer.":!v.extractionUnlocked?"Unlocks after the first building is cleared.":!safe?"Only at the sanctuary.":c.active?c.votes+" voted · only they leave in "+c.seconds+"s":c.votes+" / "+c.needed+" votes to start · only voters leave";
            return c;
        }
        private static float Fraction(double at,double elapsed,double length,bool active)=>!active||!(length>0)?0:(float)Math.Max(0,Math.Min(1,(at-elapsed)/length));
        /// <summary>The countdown the minimal HUD shows (departure first, then group extraction), if any is running.</summary>
        public static Countdown Active(ExpeditionView v){var d=Departure(v);if(d.active)return d;var g=GroupExtraction(v);return g.active?g:default;}

        // ---- Roster ------------------------------------------------------------------------------------------------------------
        public struct RosterRow{public string account, status;public ExpeditionTone tone;public bool you, host;}
        /// <summary>Every member's run status (extracted ones included), from members[] only.</summary>
        public static RosterRow[] Roster(ExpeditionView v)
        {
            return v.members.Select(m=>new RosterRow{account=m.account,you=m.account==v.viewer,host=m.host,
                status=m.extracted?"EXTRACTED":!m.connected?"OFFLINE":v.phase==ExpeditionPhase.Victory&&v.continueVotes.Contains(m.account)?"CONTINUING":!m.alive?(Has(m.carriedBy)?"CARRIED BY "+Who(m.carriedBy):"DOWN"):m.safe?"IN RUN · SANCTUARY":"IN RUN",
                tone=m.extracted?ExpeditionTone.Warn:!m.connected?ExpeditionTone.Muted:!m.alive?ExpeditionTone.Danger:ExpeditionTone.Good}).ToArray();
        }

        // ---- Loot --------------------------------------------------------------------------------------------------------------
        public enum LootKind { Ordinary, Reserved }
        public enum LootState { Open, Taken, TakenByYou, ReservedForYou, ClaimedByYou, SelectedByYou, ReservedForOther, ClaimedByOther, NotEligible }
        public struct LootRow{public string id, label, detail, owner, boss;public LootKind kind;public LootState state;public ExpeditionTone tone;public bool mine;}
        /// <summary>
        /// Ordinary drops (first pickup: open, or taken by someone) and reserved boss rewards (yours or another survivor's,
        /// claimed or not), plus one NotEligible row per boss whose dice record does not list the viewer.
        /// </summary>
        public static List<LootRow> Loot(ExpeditionView v)
        {
            var rows=new List<LootRow>();string me=v.viewer;
            foreach(var l in v.loot)
            {
                var r=new LootRow{id=l.id,owner=l.owner,boss=l.boss,mine=Has(l.owner)&&l.owner==me};
                if(l.source=="monster")
                {
                    r.kind=LootKind.Ordinary;
                    if(!l.claimed){r.state=LootState.Open;r.label="DROP · FIRST PICKUP";r.detail="Anyone can take it";r.tone=ExpeditionTone.Cyan;}
                    else if(r.mine){r.state=LootState.TakenByYou;r.label="DROP · YOURS";r.detail=l.consumed?"Chip selected":"Pick a chip";r.tone=ExpeditionTone.Good;}
                    else{r.state=LootState.Taken;r.label="DROP · TAKEN";r.detail="Picked up by "+Who(l.owner);r.tone=ExpeditionTone.Muted;}
                }
                else
                {
                    r.kind=LootKind.Reserved;string boss=Name(l.boss);
                    if(r.mine)
                    {
                        r.state=l.consumed?LootState.SelectedByYou:l.claimed?LootState.ClaimedByYou:LootState.ReservedForYou;r.tone=l.consumed?ExpeditionTone.Good:ExpeditionTone.Magenta;
                        r.label="BOSS · RESERVED FOR YOU";r.detail=boss+(l.consumed?" · chip selected":l.claimed?" · pick a chip":" · unclaimed");
                    }
                    else
                    {
                        r.state=l.claimed?LootState.ClaimedByOther:LootState.ReservedForOther;r.tone=ExpeditionTone.Muted;
                        r.label="BOSS · RESERVED";r.detail=boss+" · for "+Who(l.owner)+(l.claimed?" · claimed":" · unclaimed");
                    }
                }
                rows.Add(r);
            }
            if(Has(me)&&Has(v.mine.account))foreach(var d in v.dice)
                if(!d.eligible.Contains(me))rows.Add(new LootRow{id=d.boss,boss=d.boss,kind=LootKind.Reserved,state=LootState.NotEligible,tone=ExpeditionTone.Danger,
                    label="BOSS · NOT ELIGIBLE",detail=Name(d.boss)+" · you were not alive and connected when it fell"});
            return rows;
        }

        // ---- Boss shares -------------------------------------------------------------------------------------------------------
        public struct BossShare{public string boss, name;public int total, each, extra, yours, yoursClaimed, yoursSelected, survivors;public bool eligible, wonDie, tieBreak;}
        /// <summary>
        /// One per dice record: the configured total (each x survivors + dice extras), the even share, the remainder the dice
        /// assigned, and the viewer's eligibility and reserved count. Read from dice[] and loot[], never recomputed.
        /// </summary>
        public static BossShare Share(ExpeditionView v,ExpeditionDice d)
        {
            string me=v.viewer;var mine=v.loot.Where(l=>l.boss==d.boss&&l.owner==me&&Has(me)).ToArray();
            var s=new BossShare{boss=d.boss,name=Name(d.boss),each=d.each,extra=d.winners.Length,survivors=d.eligible.Length,total=d.each*d.eligible.Length+d.winners.Length,
                eligible=Has(me)&&d.eligible.Contains(me),wonDie=Has(me)&&d.winners.Contains(me),yours=mine.Length,yoursClaimed=mine.Count(l=>l.claimed),yoursSelected=mine.Count(l=>l.consumed)};
            // A tie at the cut line was settled by the authority's seeded tie-break (not stored); say so instead of implying the roll decided it.
            if(d.winners.Length>0&&d.winners.Length<d.eligible.Length)
            {
                int low=d.winners.Min(w=>d.rolls[Array.IndexOf(d.eligible,w)]),high=d.eligible.Where(a=>!d.winners.Contains(a)).Max(a=>d.rolls[Array.IndexOf(d.eligible,a)]);
                s.tieBreak=low==high;
            }
            return s;
        }
        public static string ShareLine(BossShare s)
        {
            if(s.survivors==0)return s.total+" chips · no survivors";
            string chips=s.total==1?"1 chip":s.total+" chips",survivors=s.survivors==1?"1 survivor":s.survivors+" survivors";
            return s.extra==0?chips+" · "+survivors+" · "+s.each+" each":chips+" · "+survivors+" · "+s.each+" each + "+s.extra+" by dice";
        }

        // ---- Dice reveal -------------------------------------------------------------------------------------------------------
        /// <summary>
        /// The dice animation over a stored record. Faces are -1 (spinning, no number) until they lock onto the stored roll, in
        /// eligible order; winners light after the last lock. A pure function of (record, time): nothing is rolled or allocated.
        /// </summary>
        public static class Dice
        {
            public const float Lead=.35f, Spin=.8f, Step=.32f, Hold=.9f;
            public static float LockAt(int i)=>Lead+Spin+i*Step;
            public static float WinnersAt(ExpeditionDice d)=>LockAt(Math.Max(0,d.eligible.Length-1))+.25f;
            public static float Duration(ExpeditionDice d)=>WinnersAt(d)+Hold;
            public static int Face(ExpeditionDice d,int i,float t)=>i<0||i>=d.rolls.Length||t<LockAt(i)?-1:d.rolls[i];
            public static bool Locked(int i,float t)=>t>=LockAt(i);
            public static bool WinnersShown(ExpeditionDice d,float t)=>t>=WinnersAt(d);
            /// <summary>Whether this eligible account's die is shown as a winner at time t (only after every die has locked).</summary>
            public static bool Winner(ExpeditionDice d,int i,float t)=>WinnersShown(d,t)&&i>=0&&i<d.eligible.Length&&d.winners.Contains(d.eligible[i]);
            /// <summary>Cosmetic spin phase of an unlocked die (degrees); carries no value.</summary>
            public static float Tumble(int i,float t)=>Locked(i,t)?0:(t*540+i*47)%360;
        }

        // ---- Extraction and victory --------------------------------------------------------------------------------------------
        public struct ExtractionModel
        {
            public bool open, can, extracted, saved, hostNeedsTransfer, victory;public string title, detail, button;public ExpeditionTone tone;
        }
        /// <summary>The viewer's individual extraction: available (authority CanExtract), done-and-saved, done-awaiting-save, or why not.</summary>
        public static ExtractionModel Extraction(ExpeditionView v)
        {
            var me=v.mine;var m=new ExtractionModel{open=v.phase==ExpeditionPhase.Town||v.phase==ExpeditionPhase.Victory,can=me.canExtract,extracted=me.extracted,saved=me.extractionSaved,
                victory=v.phase==ExpeditionPhase.Victory,title="EXTRACT ALONE",button="Extract",tone=ExpeditionTone.Warn};
            bool peers=v.members.Any(p=>p.account!=v.viewer&&p.connected&&!p.extracted);m.hostNeedsTransfer=v.ViewerIsHost()&&peers;
            if(!Has(me.account)){m.detail="You are not in this run.";m.can=false;m.tone=ExpeditionTone.Muted;}
            else if(me.extracted){m.title=me.extractionSaved?"EXTRACTED · SAVED":"EXTRACTED · SAVE NOT CONFIRMED";m.tone=me.extractionSaved?ExpeditionTone.Good:ExpeditionTone.Warn;
                m.detail=me.extractionSaved?"Your build and rewards are banked. You cannot rejoin this run.":"Waiting for the save. Rewards are only final once it is confirmed.";}
            else if(v.migrating)m.detail="Frozen during the host transfer.";
            else if(!v.extractionUnlocked)m.detail="Unlocks after the first building is cleared.";
            else if(!m.open)m.detail="Only at the sanctuary.";
            else if(!me.alive)m.detail="Get reprinted first.";
            else if(!me.safe)m.detail="Reach the sanctuary first.";
            else if(m.hostNeedsTransfer)m.detail="You are the host: leaving moves the host to the best connection first.";
            else if(me.canExtract)m.detail="Leave now with your build. The others can continue.";
            else m.detail="Not available right now.";
            return m;
        }

        // ---- Host transfer -----------------------------------------------------------------------------------------------------
        public struct TransferStep{public string label, detail;public ExpeditionStep state;}
        /// <summary>
        /// The host-transfer overlay: election and freeze (authority), package and endpoint (transport: unavailable until it exists),
        /// destination ACK, commit or abort. Nothing reads as done unless the authority or the transport reports it.
        /// </summary>
        public static TransferStep[] Transfer(ExpeditionView v,out string headline,out ExpeditionTone tone)
        {
            var t=v.transfer;string next=Who(t.nextHost);bool ended=t.stage==ExpeditionTransferStage.Committed||t.stage==ExpeditionTransferStage.Aborted||t.stage==ExpeditionTransferStage.Refused;
            var steps=new List<TransferStep>();
            if(t.stage==ExpeditionTransferStage.Refused&&!Has(t.id))
            {
                headline="HOST TRANSFER REFUSED";tone=ExpeditionTone.Danger;
                steps.Add(new TransferStep{label="Elect the best connection",detail=Has(t.reason)?t.reason:"No eligible connection.",state=ExpeditionStep.Failed});return steps.ToArray();
            }
            steps.Add(new TransferStep{label="Elect the best connection",detail=Has(t.nextHost)?next+" (lowest loss, then latency + jitter)":"Waiting for the authority",state=Has(t.nextHost)?ExpeditionStep.Done:ExpeditionStep.Active});
            steps.Add(new TransferStep{label="Freeze the run",detail=t.active?"Clock and commands frozen at revision "+t.revision:ended?"Released":"Not frozen",state=t.active||ended?ExpeditionStep.Done:ExpeditionStep.Pending});
            var transport=t.endpointMigration?(t.active?ExpeditionStep.Active:ended&&t.stage==ExpeditionTransferStage.Committed?ExpeditionStep.Done:ExpeditionStep.Pending):ExpeditionStep.Unavailable;
            string missing="Not implemented in this build";
            steps.Add(new TransferStep{label="Store the live simulation at "+next,detail=t.endpointMigration?"Destination stores and verifies the package":missing,state=t.endpointMigration&&t.packageStored?ExpeditionStep.Done:transport});
            steps.Add(new TransferStep{label="Destination ACK",detail=t.endpointMigration?"Transaction, digest, revision and epoch "+t.nextEpoch:missing,state=t.endpointMigration&&t.ackReceived?ExpeditionStep.Done:transport});
            switch(t.stage)
            {
                case ExpeditionTransferStage.Committed:
                    headline="HOST IS NOW "+next;tone=t.endpointMigration?ExpeditionTone.Good:ExpeditionTone.Warn;
                    steps.Add(new TransferStep{label="Commit",detail="Authority host "+next+" at epoch "+t.nextEpoch+(t.endpointMigration?"":" · endpoint not moved (no transport)"),state=ExpeditionStep.Done});break;
                case ExpeditionTransferStage.Aborted:
                    headline="HOST TRANSFER FAILED";tone=ExpeditionTone.Danger;
                    steps.Add(new TransferStep{label="Aborted",detail=(Has(t.reason)?t.reason:"Aborted.")+" Nobody extracted; the host is unchanged.",state=ExpeditionStep.Failed});break;
                case ExpeditionTransferStage.Refused:
                    headline="HOST TRANSFER FAILED";tone=ExpeditionTone.Danger;
                    steps.Add(new TransferStep{label="Commit",detail=Has(t.reason)?t.reason:"Refused",state=ExpeditionStep.Failed});break;
                default:
                    headline=t.active?"TRANSFERRING HOST TO "+next:"HOST TRANSFER";tone=ExpeditionTone.Warn;
                    steps.Add(new TransferStep{label="Commit",detail=t.endpointMigration?"After the ACK":"Cannot complete without the transport",state=t.endpointMigration?ExpeditionStep.Pending:ExpeditionStep.Unavailable});break;
            }
            return steps.ToArray();
        }

        // ---- Command feedback --------------------------------------------------------------------------------------------------
        /// <summary>The caption under a control for its intent's state; null when nothing was sent.</summary>
        public static string Feedback(ExpeditionCommandResult r,out ExpeditionTone tone)
        {
            switch(r.state)
            {
                case ExpeditionCommandState.Pending:tone=ExpeditionTone.Muted;return r.reason??"Sent · waiting for the authority";
                case ExpeditionCommandState.Confirmed:tone=ExpeditionTone.Good;return r.reason??"Confirmed";
                case ExpeditionCommandState.Refused:tone=ExpeditionTone.Danger;return "Refused · "+(r.reason??"");
                case ExpeditionCommandState.Unavailable:tone=ExpeditionTone.Warn;return "Unavailable · "+(r.reason??"");
                case ExpeditionCommandState.Failed:tone=ExpeditionTone.Danger;return "Failed · "+(r.reason??"");
                default:tone=ExpeditionTone.Muted;return null;
            }
        }
    }
}
