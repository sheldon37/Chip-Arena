using System;
using System.Collections.Generic;
using System.Linq;

namespace ChipArena.UnityConversion
{
    /// <summary>Host-only Expedition authority. Transport authenticates callers and validates location/combat events;
    /// presentation reads detached snapshots. No existing PvP/Siren simulation is changed by this module.</summary>
    public sealed partial class NativeExpedition
    {
        private ExpeditionSnapshot s;
        private sealed class MigrationState {public ExpeditionHandoff Handoff;public bool Restored;}
        private readonly MigrationState migration=new MigrationState();
        private ExpeditionHandoff handoff {get=>migration.Handoff;set=>migration.Handoff=value;}
        public const double ExitSeconds=10, ExtractSeconds=10; // Initial tuning, not owner-specified balance.
        public NativeExpedition(ExpeditionSnapshot state){ExpeditionValidation.Require(state);s=state.Copy();}
        public static NativeExpedition Create(string run,string world,string host,string weapon,uint seed,ExpeditionInstance[] instances)
        {
            return new NativeExpedition(new ExpeditionSnapshot{run=run,world=world,area=world,host=host,seed=seed,
                instances=instances?.Select(i=>i.Copy()).ToArray(),members=new[]{new ExpeditionMember{account=host,weapon=weapon,connected=true}}});
        }
        public ExpeditionSnapshot Snapshot()=>ScopedSnapshot();
        public double Elapsed=>s.elapsed;
        public bool Migrating=>handoff!=null;
        public bool FinalUnlocked=>s.instances.All(i=>i.cleared);
        public bool ExtractionUnlocked=>s.instances.Any(i=>i.cleared)||s.loop>0;
        private bool Running=>handoff==null&&Phase!=ExpeditionPhase.Ended;
        private bool Mutable=>Running&&!Loading;
        private ExpeditionMember Member(string account)=>s.members.FirstOrDefault(p=>p.account==account);
        private bool Live(ExpeditionMember p)=>p!=null&&p.connected&&p.alive&&!p.extracted&&InArea(p)&&!p.transferring;
        private string[] Living()=>s.members.Where(Live).Select(p=>p.account).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        private ExpeditionInstance Current=>s.instances.FirstOrDefault(i=>i.id==Area);
        public void DisableAccountXp(){if(Migrating)return;s.xpDisabled=true;Changed();}
        private void CompleteAccountXp(ExpeditionMember p,bool victory)
        {
            string id="exp_"+s.run+"_"+s.loop;
            if(p==null||s.xpDisabled||p.extracted||!p.connected||p.xpCompletion?.completionId==id)return;
            var completion=new AccountXpCompletion{completionId=id,mode="expedition",won=victory,seconds=(int)Math.Min(604800,s.elapsed),cleared=Math.Min(3,s.instances.Count(i=>i.cleared))};
            // Freeze even ineligible short completions so waiting on a result cannot make them eligible.
            p.xpCompletion=completion;
        }
        private void Changed(){s.revision=checked(s.revision+1);}
        private static T[] Add<T>(T[] list,T value)=>list.Concat(new[]{value}).ToArray();
        private static bool Key(string id)=>ExpeditionValidation.Id(id);
        private string EventKey(string id)=>s.loop+":"+Area+":"+id;
        private bool CanRecord(string id)=>Key(id)&&s.events.Length<ExpeditionValidation.MaxRecords&&!s.events.Contains(id);
        private void Record(string id){s.events=Add(s.events,id);}

        public bool Join(string account,string weapon)
        {
            if(!Mutable||!Key(account)||!Key(weapon))return false;var p=Member(account);
            if(p?.extracted==true||p?.connected==true||s.members.Count(m=>m.connected&&!m.extracted)>=8)return false;
            if(p!=null){p.connected=true;RefreshVotes();Changed();return true;}
            if(s.members.Length>=ExpeditionValidation.MaxMembers)return false;
            var amounts=s.members.Where(m=>!m.extracted).Select(m=>m.chips.Sum(c=>(long)c.count)).OrderBy(n=>n).ToArray();
            int count=amounts.Length==0?0:(int)Math.Min(256,amounts[(amounts.Length-1)/2]);
            // Lower median is provisional catch-up balance; the one-time entitlement is persisted.
            p=new ExpeditionMember{account=account,weapon=weapon,connected=true,catchup=count,initialCatchup=count,safe=false};
            p.area=SplitAreas?s.world:null;s.members=Add(s.members,p);RefreshVotes();Changed();return true;
        }
        public bool Reconnect(string account)
        {
            var p=Member(account);if(!Running||p==null||p.connected||p.extracted)return false;
            p.connected=true;RefreshVotes();Changed();return true;
        }
        public bool Disconnect(string account)
        {
            if(!Running)return false;var p=Member(account);if(p==null||!p.connected)return false;
            p.connected=false;p.safe=false;p.choosing=false;p.choosingUntil=0;ReleaseCarried(account);RefreshVotes();Changed();return true;
        }
        public bool SetSafe(string account,bool safe)
        {
            if(!Mutable)return false;var p=Member(account);if(!Live(p)||safe&&Phase!=ExpeditionPhase.Town&&Phase!=ExpeditionPhase.Victory)return false;
            p.safe=safe;RefreshVotes();Changed();return true;
        }
        public bool SetLoading(bool value){if(!Running)return false;Loading=value;if(value){foreach(var p in s.members.Where(InArea)){p.safe=false;p.choosing=false;p.choosingUntil=0;}RefreshVotes();}Changed();return true;}
        public bool SetPerks(string account,string[] perks,Func<string,bool> unlocked)
        {
            if(!Mutable||s.elapsed!=0||Phase!=ExpeditionPhase.Town||unlocked==null||perks==null||perks.Length>3||perks.Distinct().Count()!=perks.Length||perks.Any(id=>!Key(id)||!unlocked(id)))return false;
            var p=Member(account);if(!Live(p))return false;p.perks=(string[])perks.Clone();Changed();return true;
        }
        public bool Enter(string instance)
        {
            if(SplitAreas||!Mutable||Phase!=ExpeditionPhase.Town||Living().Length==0)return false;
            var area=s.instances.FirstOrDefault(i=>i.id==instance);if(area==null||area.cleared)return false;
            area.entered=true;Area=instance;Phase=ExpeditionPhase.Instance;ResetVotes();foreach(var p in s.members)p.safe=false;Changed();return true;
        }
        public bool Progress(string eventId,int points)
        {
            string key=EventKey(eventId);if(!Mutable||Phase!=ExpeditionPhase.Instance||!Key(eventId)||points<1||points>1000000||Current.cleared||!CanRecord(key))return false;
            Current.progress=(int)Math.Min(Current.target,(long)Current.progress+points);Record(key);Changed();return true;
        }
        public bool StartBosses(string[] bossIds)
        {
            if(!Mutable||Phase!=ExpeditionPhase.Instance||Current.progress<Current.target||Current.bosses.Length!=0||bossIds==null||bossIds.Length<1||bossIds.Length>32||bossIds.Any(id=>!Key(id))||bossIds.Distinct().Count()!=bossIds.Length)return false;
            Current.bosses=(string[])bossIds.Clone();Changed();return true;
        }
        public bool BeginFinal()
        {
            if(!Mutable||Phase!=ExpeditionPhase.Town||!FinalUnlocked||Living().Length==0||!AllHome)return false;
            Phase=ExpeditionPhase.FinalBoss;foreach(var p in s.members)p.safe=false;ResetVotes();Changed();return true;
        }
        public bool FinishFinal(int drops=-1)
        {
            if(!Mutable||Phase!=ExpeditionPhase.FinalBoss||!IssueBossRewards(EventKey("final:boss"),drops))return false;
            Phase=ExpeditionPhase.Victory;foreach(var p in s.members)CompleteAccountXp(p,true);ResetVotes();Changed();return true;
        }
        public bool NextLoop()
        {
            if(!Mutable||Phase!=ExpeditionPhase.Victory||s.loop>=100000||Living().Length==0||!AllHome)return false;
            s.loop++;foreach(var i in s.instances){i.entered=i.cleared=false;i.progress=0;i.bosses=i.defeated=Array.Empty<string>();}
            if(!SplitAreas)Area=s.world;else foreach(var a in areaStates.Values){a.Loading=false;a.ExitAt=-1;a.ExitVotes=Array.Empty<string>();a.Phase=a.Id==s.world?ExpeditionPhase.Town:ExpeditionPhase.Instance;}Phase=ExpeditionPhase.Town;ResetVotes();foreach(var p in s.members)p.safe=false;Changed();return true;
        }
        public bool VoteContinue(string account,bool yes)
        {
            var p=Member(account);if(!Mutable||Phase!=ExpeditionPhase.Victory||p==null||!p.connected||p.extracted)return false;
            s.continueVotes=Vote(s.continueVotes,account,yes);if(yes)s.extractVotes=Vote(s.extractVotes,account,false);RefreshVotes();Changed();return true;
        }
        public bool ContinueReady=>Phase==ExpeditionPhase.Victory&&s.members.Any(p=>p.connected&&!p.extracted)&&
            s.members.Where(p=>p.connected&&!p.extracted).All(p=>s.continueVotes.Contains(p.account));
        public bool AdvanceWhenReady()=>ContinueReady&&NextLoop();
        public bool VoteExit(string account,bool yes)
        {
            if(!Mutable||Phase!=ExpeditionPhase.Instance||!Current.cleared||!Live(Member(account)))return false;
            ExitVotes=Vote(ExitVotes,account,yes);RefreshVotes();Changed();return true;
        }
        public bool VoteExtraction(string account,bool yes)
        {
            if(!Mutable||Area!=s.world||!ExtractionUnlocked||!Live(Member(account))||!Member(account).safe)return false;
            s.extractVotes=Vote(s.extractVotes,account,yes);if(yes)s.continueVotes=Vote(s.continueVotes,account,false);RefreshVotes();Changed();return true;
        }
        private static string[] Vote(string[] votes,string account,bool yes)=>yes?votes.Contains(account)?votes:Add(votes,account):votes.Where(a=>a!=account).ToArray();
        private void ResetVotes(){ExitVotes=Array.Empty<string>();ExitAt=-1;if(!SplitAreas||Area==s.world){s.extractVotes=s.continueVotes=Array.Empty<string>();s.extractAt=-1;}}
        private void RefreshVotes()
        {
            s.continueVotes=s.continueVotes.Where(a=>Member(a)?.connected==true&&!Member(a).extracted).ToArray();
            var live=Living();ExitVotes=ExitVotes.Where(live.Contains).ToArray();
            if(!SplitAreas||Area==s.world)s.extractVotes=s.extractVotes.Where(a=>live.Contains(a)&&Member(a).safe).ToArray();
            if(ExitVotes.Length==0)ExitAt=-1;
            else if(ExitVotes.Length==live.Length)ExitAt=s.elapsed;
            else if(ExitAt<0)ExitAt=s.elapsed+ExitSeconds;
            if(SplitAreas&&Area!=s.world)return;
            if(s.extractVotes.Length<=live.Length/2)s.extractAt=-1;
            else if(s.extractAt<0)s.extractAt=s.elapsed+ExtractSeconds;
        }
        /// <summary>Advance active play only. Selecting chips and sitting in sanctuary do NOT pause difficulty.</summary>
        public void Tick(double dt)
        {
            if(!ExpeditionValidation.Number(dt)||dt<0||dt>.25)throw new ArgumentOutOfRangeException(nameof(dt));
            if(!Mutable||!s.members.Any(p=>p.connected&&!p.extracted)||dt==0)return;
            if(!SplitAreas||Area==s.world)s.elapsed+=dt;
            foreach(var p in s.members)if(p.choosingUntil>0&&s.elapsed>=p.choosingUntil){p.choosing=false;p.choosingUntil=0;}
            if(!SplitAreas&&ExitAt>=0&&s.elapsed>=ExitAt){Phase=ExpeditionPhase.Town;Area=s.world;ResetVotes();foreach(var p in s.members)if(Live(p))p.safe=false;}
            // Extraction is collected via ExtractionDue and committed by the adapter after a durable save/handoff.
            Changed();
        }
        public string[] ExtractionDue()=>!Migrating&&(!SplitAreas||Area==s.world)&&s.extractAt>=0&&s.elapsed>=s.extractAt?(string[])s.extractVotes.Clone():Array.Empty<string>();
        public bool CanExtract(string account)=>Mutable&&ExtractionUnlocked&&Live(Member(account))&&Member(account).safe&&(Phase==ExpeditionPhase.Town||Phase==ExpeditionPhase.Victory);
        public bool Extract(string account)
        {
            if(!CanExtract(account)||account==s.host&&s.members.Any(p=>p.account!=account&&p.connected&&!p.extracted))return false;
            ExtractMember(account);if(!s.members.Any(p=>p.connected&&!p.extracted))s.phase=ExpeditionPhase.Ended;RefreshVotes();Changed();return true;
        }
        private void ExtractMember(string account)
        {
            var p=Member(account);CompleteAccountXp(p,false);p.extracted=true;p.connected=p.alive=p.safe=p.choosing=false;p.choosingUntil=0;p.carriedBy=null;ReleaseCarried(account);
        }
        private void ReleaseCarried(string account){foreach(var p in s.members)if(p.carriedBy==account)p.carriedBy=null;}
        public bool Defeat(string account)
        {
            if(!Mutable||!Live(Member(account)))return false;var p=Member(account);p.alive=false;p.safe=false;p.choosing=false;p.choosingUntil=0;
            ReleaseCarried(account);if(!s.members.Any(m=>m.connected&&m.alive&&!m.extracted))foreach(var m in s.members)CompleteAccountXp(m,false);RefreshVotes();Changed();return true;
        }
        public bool Absorb(string carrier,string victim)
        {
            if(!Mutable||!Live(Member(carrier)))return false;var p=Member(victim);
            if(p==null||!InArea(p)||p.alive||p.extracted||p.carriedBy!=null||carrier==victim)return false;
            p.carriedBy=carrier;Changed();return true;
        }
        /// <summary>Authority validates body proximity and line of sight before an in-place revive.</summary>
        public bool Revive(string payer,string victim,int cost)
        {
            var buyer=Member(payer);var target=Member(victim);
            if(!Mutable||cost<1||!Live(buyer)||target==null||!InArea(target)||target.transferring||target.alive||target.extracted||!target.connected||target.carriedBy!=null||buyer.wallet<cost)return false;
            buyer.wallet-=cost;target.alive=true;target.safe=buyer.safe;Changed();return true;
        }
        /// <summary>Adapter validates proximity, uninterrupted channel and machine/medic permission before calling.</summary>
        public bool Reprint(string payer,string victim,int cost,bool remoteSafeRecovery=false)
        {
            if(!Mutable||cost<1||!Live(Member(payer)))return false;var buyer=Member(payer);var target=Member(victim);
            if(target==null||target.alive||target.extracted||!target.connected||buyer.wallet<cost)return false;
            if(remoteSafeRecovery){if(!buyer.safe||Phase!=ExpeditionPhase.Town&&Phase!=ExpeditionPhase.Victory)return false;}
            else if(!InArea(target)||target.carriedBy!=payer)return false;
            if(SplitAreas&&target.area!=buyer.area&&(ReprintPlacement?.Invoke(payer,victim)!=true))return false;
            buyer.wallet-=cost;target.alive=true;target.safe=buyer.safe;target.carriedBy=null;Changed();return true;
        }
        /// <summary>Authority-only service payment, after live range/capacity checks. Event ID prevents duplicate debits.</summary>
        public bool Spend(string account,string eventId,int amount)
        {
            string key=EventKey("spend:"+eventId)+":"+account;var buyer=Member(account);
            if(!Mutable||!Live(buyer)||!Key(eventId)||amount<1||buyer.wallet<amount||!CanRecord(key))return false;
            buyer.wallet-=amount;Record(key);Changed();return true;
        }
        public bool Credit(string account,string eventId,int amount)
        {
            string key=EventKey(eventId)+":"+account;if(!Mutable||!Key(eventId)||amount<1||!CanRecord(key))return false;
            var p=Member(account);if(p==null||p.extracted||(long)p.wallet+amount>1000000000)return false;
            p.wallet+=amount;Record(key);Changed();return true;
        }
    }
}
