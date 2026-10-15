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
        private ExpeditionHandoff handoff;
        public const double ExitSeconds=10, ExtractSeconds=10; // Initial tuning, not owner-specified balance.
        public NativeExpedition(ExpeditionSnapshot state){ExpeditionValidation.Require(state);s=state.Copy();}
        public static NativeExpedition Create(string run,string world,string host,string weapon,uint seed,ExpeditionInstance[] instances)
        {
            return new NativeExpedition(new ExpeditionSnapshot{run=run,world=world,area=world,host=host,seed=seed,
                instances=instances?.Select(i=>i.Copy()).ToArray(),members=new[]{new ExpeditionMember{account=host,weapon=weapon,connected=true}}});
        }
        public ExpeditionSnapshot Snapshot()=>s.Copy();
        public double Elapsed=>s.elapsed;
        public bool Migrating=>handoff!=null;
        public bool FinalUnlocked=>s.instances.All(i=>i.cleared);
        public bool ExtractionUnlocked=>s.instances.Any(i=>i.cleared)||s.loop>0;
        private bool Running=>handoff==null&&s.phase!=ExpeditionPhase.Ended;
        private bool Mutable=>Running&&!s.loading;
        private ExpeditionMember Member(string account)=>s.members.FirstOrDefault(p=>p.account==account);
        private bool Live(ExpeditionMember p)=>p!=null&&p.connected&&p.alive&&!p.extracted;
        private string[] Living()=>s.members.Where(Live).Select(p=>p.account).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        private ExpeditionInstance Current=>s.instances.FirstOrDefault(i=>i.id==s.area);
        private void Changed(){s.revision=checked(s.revision+1);}
        private static T[] Add<T>(T[] list,T value)=>list.Concat(new[]{value}).ToArray();
        private static bool Key(string id)=>ExpeditionValidation.Id(id);
        private string EventKey(string id)=>s.loop+":"+s.area+":"+id;
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
            s.members=Add(s.members,p);RefreshVotes();Changed();return true;
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
            if(!Mutable)return false;var p=Member(account);if(!Live(p)||safe&&s.phase!=ExpeditionPhase.Town&&s.phase!=ExpeditionPhase.Victory)return false;
            p.safe=safe;RefreshVotes();Changed();return true;
        }
        public bool SetLoading(bool value){if(!Running)return false;s.loading=value;if(value){foreach(var p in s.members){p.safe=false;p.choosing=false;p.choosingUntil=0;}RefreshVotes();}Changed();return true;}
        public bool SetPerks(string account,string[] perks,Func<string,bool> unlocked)
        {
            if(!Mutable||s.elapsed!=0||s.phase!=ExpeditionPhase.Town||unlocked==null||perks==null||perks.Length>3||perks.Distinct().Count()!=perks.Length||perks.Any(id=>!Key(id)||!unlocked(id)))return false;
            var p=Member(account);if(!Live(p))return false;p.perks=(string[])perks.Clone();Changed();return true;
        }
        public bool Enter(string instance)
        {
            if(!Mutable||s.phase!=ExpeditionPhase.Town||Living().Length==0)return false;
            var area=s.instances.FirstOrDefault(i=>i.id==instance);if(area==null||area.cleared)return false;
            area.entered=true;s.area=instance;s.phase=ExpeditionPhase.Instance;ResetVotes();foreach(var p in s.members)p.safe=false;Changed();return true;
        }
        public bool Progress(string eventId,int points)
        {
            string key=EventKey(eventId);if(!Mutable||s.phase!=ExpeditionPhase.Instance||!Key(eventId)||points<1||points>1000000||Current.cleared||!CanRecord(key))return false;
            Current.progress=(int)Math.Min(Current.target,(long)Current.progress+points);Record(key);Changed();return true;
        }
        public bool StartBosses(string[] bossIds)
        {
            if(!Mutable||s.phase!=ExpeditionPhase.Instance||Current.progress<Current.target||Current.bosses.Length!=0||bossIds==null||bossIds.Length<1||bossIds.Length>32||bossIds.Any(id=>!Key(id))||bossIds.Distinct().Count()!=bossIds.Length)return false;
            Current.bosses=(string[])bossIds.Clone();Changed();return true;
        }
        public bool BeginFinal()
        {
            if(!Mutable||s.phase!=ExpeditionPhase.Town||!FinalUnlocked||Living().Length==0)return false;
            s.phase=ExpeditionPhase.FinalBoss;foreach(var p in s.members)p.safe=false;ResetVotes();Changed();return true;
        }
        public bool FinishFinal(int drops=-1)
        {
            if(!Mutable||s.phase!=ExpeditionPhase.FinalBoss||!IssueBossRewards(EventKey("final:boss"),drops))return false;
            s.phase=ExpeditionPhase.Victory;ResetVotes();Changed();return true;
        }
        public bool NextLoop()
        {
            if(!Mutable||s.phase!=ExpeditionPhase.Victory||s.loop>=100000||Living().Length==0)return false;
            s.loop++;foreach(var i in s.instances){i.entered=i.cleared=false;i.progress=0;i.bosses=i.defeated=Array.Empty<string>();}
            s.area=s.world;s.phase=ExpeditionPhase.Town;ResetVotes();foreach(var p in s.members)p.safe=false;Changed();return true;
        }
        public bool VoteContinue(string account,bool yes)
        {
            var p=Member(account);if(!Mutable||s.phase!=ExpeditionPhase.Victory||p==null||!p.connected||p.extracted)return false;
            s.continueVotes=Vote(s.continueVotes,account,yes);if(yes)s.extractVotes=Vote(s.extractVotes,account,false);RefreshVotes();Changed();return true;
        }
        public bool ContinueReady=>s.phase==ExpeditionPhase.Victory&&s.members.Any(p=>p.connected&&!p.extracted)&&
            s.members.Where(p=>p.connected&&!p.extracted).All(p=>s.continueVotes.Contains(p.account));
        public bool AdvanceWhenReady()=>ContinueReady&&NextLoop();
        public bool VoteExit(string account,bool yes)
        {
            if(!Mutable||s.phase!=ExpeditionPhase.Instance||!Current.cleared||!Live(Member(account)))return false;
            s.exitVotes=Vote(s.exitVotes,account,yes);RefreshVotes();Changed();return true;
        }
        public bool VoteExtraction(string account,bool yes)
        {
            if(!Mutable||!ExtractionUnlocked||!Live(Member(account))||!Member(account).safe)return false;
            s.extractVotes=Vote(s.extractVotes,account,yes);if(yes)s.continueVotes=Vote(s.continueVotes,account,false);RefreshVotes();Changed();return true;
        }
        private static string[] Vote(string[] votes,string account,bool yes)=>yes?votes.Contains(account)?votes:Add(votes,account):votes.Where(a=>a!=account).ToArray();
        private void ResetVotes(){s.exitVotes=s.extractVotes=s.continueVotes=Array.Empty<string>();s.exitAt=s.extractAt=-1;}
        private void RefreshVotes()
        {
            s.continueVotes=s.continueVotes.Where(a=>Member(a)?.connected==true&&!Member(a).extracted).ToArray();
            var live=Living();s.exitVotes=s.exitVotes.Where(live.Contains).ToArray();
            s.extractVotes=s.extractVotes.Where(a=>live.Contains(a)&&Member(a).safe).ToArray();
            if(s.exitVotes.Length==0)s.exitAt=-1;
            else if(s.exitVotes.Length==live.Length)s.exitAt=s.elapsed;
            else if(s.exitAt<0)s.exitAt=s.elapsed+ExitSeconds;
            if(s.extractVotes.Length<=live.Length/2)s.extractAt=-1;
            else if(s.extractAt<0)s.extractAt=s.elapsed+ExtractSeconds;
        }
        /// <summary>Advance active play only. Selecting chips and sitting in sanctuary do NOT pause difficulty.</summary>
        public void Tick(double dt)
        {
            if(!ExpeditionValidation.Number(dt)||dt<0||dt>.25)throw new ArgumentOutOfRangeException(nameof(dt));
            if(!Mutable||!s.members.Any(p=>p.connected&&!p.extracted)||dt==0)return;
            s.elapsed+=dt;
            foreach(var p in s.members)if(p.choosingUntil>0&&s.elapsed>=p.choosingUntil){p.choosing=false;p.choosingUntil=0;}
            if(s.exitAt>=0&&s.elapsed>=s.exitAt){s.phase=ExpeditionPhase.Town;s.area=s.world;ResetVotes();foreach(var p in s.members)if(Live(p))p.safe=false;}
            // Extraction is collected via ExtractionDue and committed by the adapter after a durable save/handoff.
            Changed();
        }
        public string[] ExtractionDue()=>s.extractAt>=0&&s.elapsed>=s.extractAt?(string[])s.extractVotes.Clone():Array.Empty<string>();
        public bool CanExtract(string account)=>Mutable&&ExtractionUnlocked&&Live(Member(account))&&Member(account).safe&&(s.phase==ExpeditionPhase.Town||s.phase==ExpeditionPhase.Victory);
        public bool Extract(string account)
        {
            if(!CanExtract(account)||account==s.host&&s.members.Any(p=>p.account!=account&&p.connected&&!p.extracted))return false;
            ExtractMember(account);if(!s.members.Any(p=>p.connected&&!p.extracted))s.phase=ExpeditionPhase.Ended;RefreshVotes();Changed();return true;
        }
        private void ExtractMember(string account)
        {
            var p=Member(account);p.extracted=true;p.connected=p.alive=p.safe=p.choosing=false;p.choosingUntil=0;p.carriedBy=null;ReleaseCarried(account);
        }
        private void ReleaseCarried(string account){foreach(var p in s.members)if(p.carriedBy==account)p.carriedBy=null;}
        public bool Defeat(string account)
        {
            if(!Mutable||!Live(Member(account)))return false;var p=Member(account);p.alive=false;p.safe=false;p.choosing=false;p.choosingUntil=0;
            ReleaseCarried(account);RefreshVotes();Changed();return true;
        }
        public bool Absorb(string carrier,string victim)
        {
            if(!Mutable||!Live(Member(carrier)))return false;var p=Member(victim);
            if(p==null||p.alive||p.extracted||p.carriedBy!=null||carrier==victim)return false;
            p.carriedBy=carrier;Changed();return true;
        }
        /// <summary>Adapter validates proximity, uninterrupted channel and machine/medic permission before calling.</summary>
        public bool Reprint(string payer,string victim,int cost,bool remoteSafeRecovery=false)
        {
            if(!Mutable||cost<1||!Live(Member(payer)))return false;var buyer=Member(payer);var target=Member(victim);
            if(target==null||target.alive||target.extracted||!target.connected||buyer.wallet<cost)return false;
            if(remoteSafeRecovery){if(!buyer.safe||s.phase!=ExpeditionPhase.Town&&s.phase!=ExpeditionPhase.Victory)return false;}
            else if(target.carriedBy!=payer)return false;
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
