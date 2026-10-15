using System;
using System.Linq;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeExpedition
    {
        /// <summary>Authenticated combat authority calls once per authored boss entity. Eligibility freezes here.</summary>
        public bool DefeatBoss(string boss,int drops=-1)
        {
            if(!Mutable||s.phase!=ExpeditionPhase.Instance||!Current.bosses.Contains(boss)||Current.defeated.Contains(boss)||drops< -1||drops>256)return false;
            if(!IssueBossRewards(EventKey("boss:"+boss),drops))return false;
            Current.defeated=Add(Current.defeated,boss);Current.cleared=Current.defeated.Length==Current.bosses.Length;Changed();return true;
        }
        private bool IssueBossRewards(string key,int drops)
        {
            if(drops< -1||drops>256)return false;
            var eligible=Living();int total=drops<0?eligible.Length:drops;
            if(!Key(key)||key.Length>120||s.dice.Any(d=>d.boss==key)||s.dice.Length>=ExpeditionValidation.MaxRecords||s.rewards.Length+total>ExpeditionValidation.MaxRecords)return false;
            var random=new NativePveRandom(NativePveRandom.Hash(s.seed,s.run,key));
            var rolls=eligible.Select(a=>1+(int)(random.Next()*100)).ToArray();
            // Seeded random tie breakers avoid account-name priority when dice tie.
            var ties=eligible.Select(a=>random.Next()).ToArray();
            int each=eligible.Length==0?0:total/eligible.Length,remainder=eligible.Length==0?0:total%eligible.Length;
            var winners=Enumerable.Range(0,eligible.Length).OrderByDescending(i=>rolls[i]).ThenByDescending(i=>ties[i]).Take(remainder).Select(i=>eligible[i]).ToArray();
            s.dice=Add(s.dice,new ExpeditionDice{boss=key,eligible=eligible,rolls=rolls,winners=winners,each=each});
            int serial=0;
            foreach(string account in eligible)
                for(int i=0;i<each+(winners.Contains(account)?1:0);i++)
                    s.rewards=Add(s.rewards,new ExpeditionReward{id=key+":"+(serial++),source="boss",owner=account,highQuality=true});
            return true;
        }

        /// <summary>Monster/elite spawn ID must be unique per loop/area. Unreserved drops are first-pickup.</summary>
        public bool Drop(string spawnId)
        {
            string id=EventKey("drop:"+spawnId);if(!Mutable||!Key(spawnId)||!Key(id)||s.rewards.Length>=ExpeditionValidation.MaxRecords||s.rewards.Any(r=>r.id==id))return false;
            s.rewards=Add(s.rewards,new ExpeditionReward{id=id,source="monster"});Changed();return true;
        }

        /// <summary>The authority supplies a unique chest-spawn generation and price; repeat purchases cannot charge twice.</summary>
        public bool Purchase(string account,string chestGeneration,int price)
        {
            string id=EventKey("chest:"+chestGeneration);var buyer=Member(account);
            if(!Mutable||!Live(buyer)||IsChoosing(account)||price<1||buyer.wallet<price||!Key(chestGeneration)||!Key(id)||s.rewards.Length>=ExpeditionValidation.MaxRecords||s.rewards.Any(r=>r.id==id))return false;
            buyer.wallet-=price;s.rewards=Add(s.rewards,new ExpeditionReward{id=id,source="chest",owner=account});
            foreach(var offer in s.chests.Where(c=>c.generation==chestGeneration))offer.sold=true;
            Changed();return true;
        }

        public bool IssueCatchup(string account)
        {
            var p=Member(account);if(!Mutable||!Live(p)||p.catchup==0||s.rewards.Length>=ExpeditionValidation.MaxRecords)return false;
            string id="catchup:"+account+":"+(p.initialCatchup-p.catchup);
            if(!Key(id)||s.rewards.Any(r=>r.id==id))return false;
            p.catchup--;s.rewards=Add(s.rewards,new ExpeditionReward{id=id,source="catchup",owner=account});Changed();return true;
        }

        /// <summary>Host chooses and validates offers against the content catalog and held-chip exclusions before calling.
        /// Transport must send offers only to the owner, never broadcast the full authority snapshot.</summary>
        public bool Claim(string account,string rewardId,string[] offers)
        {
            if(!Mutable||!Live(Member(account))||offers==null||offers.Length<1||offers.Length>12||offers.Any(o=>!Key(o))||offers.Distinct().Count()!=offers.Length)return false;
            var reward=s.rewards.FirstOrDefault(r=>r.id==rewardId);
            if(reward==null||reward.claimed||reward.owner!=null&&reward.owner!=account)return false;
            reward.owner=account;reward.claimed=true;reward.offers=(string[])offers.Clone();Changed();return true;
        }

        // Host holds the chooser, without damage immunity or a world/clock pause.
        public const double ChoicePlantSeconds=NativeDraftPlant.ReturnEnd;
        public bool IsChoosing(string account)
        {var p=Member(account);return Mutable&&Live(p)&&p.choosing;}
        public bool IsChoosingSeat(int seat,Func<string,int> seatOf)
        {
            if(!Mutable||seatOf==null)return false;
            foreach(var p in s.members)if(p.choosing&&Live(p)&&seatOf(p.account)==seat)return true;
            return false;
        }
        public bool SetChoosing(string account,bool open,string rewardId=null)
        {
            var p=Member(account);if(!Running||p==null)return false;
            if(open&&(!Mutable||!Live(p)||!s.rewards.Any(r=>r.owner==account&&r.claimed&&!r.consumed&&(rewardId==null||r.id==rewardId))))return false;
            // The tray auto-closes after Select; its CloseChoice must not cut the final plant short.
            if(!open&&p.choosingUntil>s.elapsed)return true;
            p.choosingUntil=0;if(p.choosing!=open){p.choosing=open;Changed();}return true;
        }

        /// <summary>Authority supplies catalog eligibility at selection time; pending selections may change exclusions.</summary>
        public bool Select(string account,string rewardId,string chip,Func<string,bool> eligible)
        {
            if(!Mutable||!Live(Member(account))||eligible==null)return false;
            var reward=s.rewards.FirstOrDefault(r=>r.id==rewardId);var p=Member(account);
            if(reward==null||!reward.claimed||reward.consumed||reward.owner!=account||!reward.offers.Contains(chip)||!eligible(chip))return false;
            var held=p.chips.FirstOrDefault(c=>c.id==chip);
            if(held!=null&&held.count>=100000||held==null&&p.chips.Length>=2048)return false;
            if(held==null)p.chips=Add(p.chips,new ChipCount{id=chip,count=1});else held.count++;
            reward.selected=chip;reward.consumed=true;if(p.choosing&&!s.rewards.Any(r=>r.owner==account&&!r.consumed))p.choosingUntil=s.elapsed+ChoicePlantSeconds;Changed();return true;
        }
    }
}
