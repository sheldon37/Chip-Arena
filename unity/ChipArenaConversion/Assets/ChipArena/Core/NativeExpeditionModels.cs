using System;
using System.Linq;

namespace ChipArena.UnityConversion
{
    public enum ExpeditionPhase { Town, Instance, FinalBoss, Victory, Ended }

    // Field-only DTOs: both Unity JsonUtility and the managed test codec can round-trip these.
    [Serializable] public sealed class ExpeditionMember
    {
        public string account, weapon, area;
        public AccountXpCompletion xpCompletion;
        public bool transferring;
        public bool connected, alive=true, safe=true, extracted, choosing;
        public double choosingUntil; // Final Install time + wrist plant duration; cleared on lifecycle transitions.
        public int wallet, catchup, initialCatchup;
        public string carriedBy;
        public string[] perks=Array.Empty<string>();
        public ChipCount[] chips=Array.Empty<ChipCount>();
        public ExpeditionMember Copy()
        {
            var p=(ExpeditionMember)MemberwiseClone();p.xpCompletion=xpCompletion?.Copy();p.perks=(string[])perks.Clone();
            p.chips=chips.Select(c=>new ChipCount{id=c.id,count=c.count}).ToArray();return p;
        }
    }
    [Serializable] public sealed class ExpeditionInstance
    {
        public string id;public int target, progress,scalingParty;public bool entered, cleared;
        public string[] bosses=Array.Empty<string>(), defeated=Array.Empty<string>();
        public ExpeditionInstance Copy(){var x=(ExpeditionInstance)MemberwiseClone();x.bosses=(string[])bosses.Clone();x.defeated=(string[])defeated.Clone();return x;}
    }
    [Serializable] public sealed class ExpeditionReward
    {
        public string id, owner, source, selected;public bool claimed, consumed, highQuality;
        public string[] offers=Array.Empty<string>();
        public ExpeditionReward Copy(){var r=(ExpeditionReward)MemberwiseClone();r.offers=(string[])offers.Clone();return r;}
    }
    [Serializable] public sealed class ExpeditionDice
    {
        public string boss;public string[] eligible=Array.Empty<string>(), winners=Array.Empty<string>();
        public int[] rolls=Array.Empty<int>();public int each;
        public ExpeditionDice Copy(){var d=(ExpeditionDice)MemberwiseClone();d.eligible=(string[])eligible.Clone();d.winners=(string[])winners.Clone();d.rolls=(int[])rolls.Clone();return d;}
    }
    [Serializable] public sealed class ExpeditionSnapshot
    {
        public int schema=1, loop, epoch=1;public long revision;
        public uint seed;public string run, world, host, area;
        public ExpeditionPhase phase=ExpeditionPhase.Town;
        public double elapsed, exitAt=-1, extractAt=-1;
        public bool loading,xpDisabled;
        public ExpeditionWorldAnchor[] anchors=Array.Empty<ExpeditionWorldAnchor>();
        public ExpeditionChestOffer[] chests=Array.Empty<ExpeditionChestOffer>();
        public ExpeditionMember[] members=Array.Empty<ExpeditionMember>();
        public ExpeditionInstance[] instances=Array.Empty<ExpeditionInstance>();
        public ExpeditionReward[] rewards=Array.Empty<ExpeditionReward>();
        public ExpeditionDice[] dice=Array.Empty<ExpeditionDice>();
        public string[] events=Array.Empty<string>(), exitVotes=Array.Empty<string>(), extractVotes=Array.Empty<string>(), continueVotes=Array.Empty<string>();
        public ExpeditionSnapshot Copy()
        {
            var s=(ExpeditionSnapshot)MemberwiseClone();s.anchors=(anchors??Array.Empty<ExpeditionWorldAnchor>()).Select(a=>a.Copy()).ToArray();s.chests=(chests??Array.Empty<ExpeditionChestOffer>()).Select(c=>c.Copy()).ToArray();s.members=members.Select(p=>p.Copy()).ToArray();
            s.instances=instances.Select(i=>i.Copy()).ToArray();s.rewards=rewards.Select(r=>r.Copy()).ToArray();
            s.dice=dice.Select(d=>d.Copy()).ToArray();s.events=(string[])events.Clone();
            s.exitVotes=(string[])exitVotes.Clone();s.extractVotes=(string[])extractVotes.Clone();s.continueVotes=(string[])(continueVotes??Array.Empty<string>()).Clone();return s;
        }
    }
    [Serializable] public sealed class ExpeditionHostCandidate
    {
        public string account;public bool reachable, capable;
        public double loss, latencyMs, jitterMs;
    }
    [Serializable] public sealed class ExpeditionHandoff
    {
        public string id, oldHost, nextHost;public int nextEpoch;
        public long revision;public string[] leaving=Array.Empty<string>();
        public ExpeditionSnapshot state;
    }

    public static class ExpeditionValidation
    {
        public const int MaxMembers=512, MaxRecords=16384;
        public static bool Id(string id)=>!string.IsNullOrEmpty(id)&&id.Length<=128&&id.All(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_'||c=='.'||c==':');
        public static bool Number(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
        private static bool Ids(string[] a,int max)=>a!=null&&a.Length<=max&&a.All(Id)&&a.Distinct(StringComparer.Ordinal).Count()==a.Length;
        public static bool Valid(ExpeditionSnapshot s)
        {
            if(s==null||s.schema!=1||!Id(s.run)||!Id(s.world)||!Id(s.host)||!Id(s.area)||s.epoch<1||s.revision<0||s.loop<0||s.loop>100000||
                !Enum.IsDefined(typeof(ExpeditionPhase),s.phase)||!Number(s.elapsed)||s.elapsed<0||s.elapsed>1e10||
                !Number(s.exitAt)||s.exitAt< -1||!Number(s.extractAt)||s.extractAt< -1)return false;
            if(!ExpeditionWorldRules.Valid(s.anchors??Array.Empty<ExpeditionWorldAnchor>(),s.chests??Array.Empty<ExpeditionChestOffer>()))return false;
            if(s.members==null||s.members.Length<1||s.members.Length>MaxMembers||s.instances==null||s.instances.Length<1||s.instances.Length>16||
                s.rewards==null||s.rewards.Length>MaxRecords||s.dice==null||s.dice.Length>MaxRecords||!Ids(s.events,MaxRecords)||
                !Ids(s.exitVotes,8)||!Ids(s.extractVotes,8)||!Ids(s.continueVotes??Array.Empty<string>(),8))return false;
            if(s.members.Any(p=>p==null||!Id(p.account)||!Id(p.weapon)||p.wallet<0||p.wallet>1000000000||p.catchup<0||p.catchup>256||p.initialCatchup<p.catchup||p.initialCatchup>256||
                !Ids(p.perks,3)||p.chips==null||p.chips.Length>2048||p.chips.Any(c=>c==null||!Id(c.id)||c.count<1||c.count>100000)||p.chips.Select(c=>c.id).Distinct().Count()!=p.chips.Length||
                p.carriedBy!=null&&!Id(p.carriedBy)||p.extracted&&(p.connected||p.alive)))return false;
            var accounts=s.members.Select(p=>p.account).ToArray();
            if(accounts.Distinct().Count()!=accounts.Length||!accounts.Contains(s.host)||s.members.Count(p=>p.connected&&!p.extracted)>8)return false;
            if(s.members.Any(p=>p.carriedBy!=null&&(p.alive||p.extracted||!s.members.Any(c=>c.account==p.carriedBy&&c.alive&&c.connected&&!c.extracted))))return false;
            if(s.instances.Any(i=>i==null||!Id(i.id)||i.id==s.world||i.target<1||i.target>1000000||i.progress<0||i.progress>i.target||!Ids(i.bosses,32)||!Ids(i.defeated,32)||
                i.defeated.Any(b=>!i.bosses.Contains(b))||i.cleared&&(i.bosses.Length==0||i.defeated.Length!=i.bosses.Length)))return false;
            if(s.instances.Select(i=>i.id).Distinct().Count()!=s.instances.Length)return false;
            if(s.phase==ExpeditionPhase.Instance&&!s.instances.Any(i=>i.id==s.area&&i.entered))return false;
            if(s.phase!=ExpeditionPhase.Instance&&(s.exitVotes.Length>0||s.exitAt>=0))return false;
            if(s.exitVotes.Length>0&&!s.instances.Any(i=>i.id==s.area&&i.cleared))return false;
            if(s.phase!=ExpeditionPhase.Town&&s.phase!=ExpeditionPhase.Victory&&(s.extractVotes.Length>0||s.extractAt>=0))return false;
            if((s.phase==ExpeditionPhase.Town||s.phase==ExpeditionPhase.Victory)&&s.area!=s.world)return false;
            if(s.rewards.Any(r=>r==null||!Id(r.id)||!Id(r.source)||r.owner!=null&&!accounts.Contains(r.owner)||r.claimed&&r.owner==null||r.consumed&&!r.claimed||
                !Ids(r.offers,12)||!string.IsNullOrEmpty(r.selected)&&(!r.consumed||!r.offers.Contains(r.selected))||!r.claimed&&r.offers.Length>0)||s.rewards.Select(r=>r.id).Distinct().Count()!=s.rewards.Length)return false;
            if(s.dice.Any(d=>d==null||!Id(d.boss)||!Ids(d.eligible,8)||!Ids(d.winners,8)||d.rolls==null||d.rolls.Length!=d.eligible.Length||d.rolls.Any(r=>r<1||r>100)||d.each<0||d.each>256||
                d.eligible.Any(a=>!accounts.Contains(a))||d.winners.Any(a=>!d.eligible.Contains(a)))||s.dice.Select(d=>d.boss).Distinct().Count()!=s.dice.Length)return false;
            if((s.continueVotes??Array.Empty<string>()).Any(a=>s.phase!=ExpeditionPhase.Victory||!s.members.Any(p=>p.account==a&&p.connected&&!p.extracted)))return false;
            return s.exitVotes.Concat(s.extractVotes).All(a=>s.members.Any(p=>p.account==a&&p.alive&&p.connected&&!p.extracted));
        }
        public static void Require(ExpeditionSnapshot s){if(!Valid(s))throw new ArgumentException("Invalid or incompatible Expedition state.");}
    }
}
