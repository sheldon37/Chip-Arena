using System;
using System.Linq;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeExpedition
    {
        private static ExpeditionHandoff CopyHandoff(ExpeditionHandoff h)=>new ExpeditionHandoff{id=h.id,oldHost=h.oldHost,nextHost=h.nextHost,
            nextEpoch=h.nextEpoch,revision=h.revision,leaving=(string[])h.leaving.Clone(),state=h.state.Copy()};

        /// <summary>Freeze rules before the transport captures its remaining live simulation state.
        /// This is a control-state handoff contract, not a network transport or combat-state serializer.</summary>
        public ExpeditionHandoff PrepareHandoff(string transaction,string[] leaving,ExpeditionHostCandidate[] candidates)
        {
            if(!Mutable||!Key(transaction)||leaving==null||!leaving.Contains(s.host)||leaving.Distinct().Count()!=leaving.Length||leaving.Any(a=>!CanExtract(a))||candidates==null||s.epoch==int.MaxValue)return null;
            var next=candidates.Where(c=>c!=null&&c.capable&&c.reachable&&!leaving.Contains(c.account)&&Member(c.account)?.connected==true&&Member(c.account)?.extracted==false&&
                ExpeditionValidation.Number(c.loss)&&c.loss>=0&&c.loss<=1&&ExpeditionValidation.Number(c.latencyMs)&&c.latencyMs>=0&&
                ExpeditionValidation.Number(c.jitterMs)&&c.jitterMs>=0)
                .OrderBy(c=>c.loss).ThenBy(c=>c.latencyMs+c.jitterMs).ThenBy(c=>c.account,StringComparer.Ordinal).FirstOrDefault();
            if(next==null)return null;
            handoff=new ExpeditionHandoff{id=transaction,oldHost=s.host,nextHost=next.account,nextEpoch=s.epoch+1,revision=s.revision,
                leaving=(string[])leaving.Clone(),state=s.Copy()};return CopyHandoff(handoff);
        }

        /// <summary>Call only after destination has persisted the complete transport+combat package and confirmed its digest.
        /// Match transaction, source revision, and next epoch to reject stale acknowledgments.</summary>
        public bool CommitHandoff(string transaction,string nextHost,int epoch,long revision)
        {
            if(handoff==null||handoff.id!=transaction||handoff.nextHost!=nextHost||handoff.nextEpoch!=epoch||handoff.revision!=revision)return false;
            foreach(string account in handoff.leaving)ExtractMember(account);
            s.host=nextHost;s.epoch=epoch;handoff=null;RefreshVotes();Changed();return true;
        }
        public bool AbortHandoff(string transaction)
        {
            if(handoff==null||handoff.id!=transaction)return false;handoff=null;return true;
        }
    }

    [Serializable] public sealed class ExpeditionScaling
    {
        // Provisional tuning values. The city/encounter catalog will override these after playtesting.
        public double timeStepSeconds=300,healthPerStep=.12,damagePerStep=.06,healthPerExtraPlayer=.30,
            countPerExtraPlayer=.35,instanceMultiplier=1.25,loopMultiplier=.5,maxHealth=100,maxDamage=20,maxCount=4;
        public ExpeditionDifficulty Evaluate(double elapsed,int players,int loop,bool instance)
        {
            if(!ExpeditionValidation.Number(elapsed)||elapsed<0||players<1||players>8||loop<0||
                !ExpeditionValidation.Number(timeStepSeconds)||timeStepSeconds<=0||
                new[]{healthPerStep,damagePerStep,healthPerExtraPlayer,countPerExtraPlayer,loopMultiplier}.Any(v=>!ExpeditionValidation.Number(v)||v<0)||
                new[]{instanceMultiplier,maxHealth,maxDamage,maxCount}.Any(v=>!ExpeditionValidation.Number(v)||v<1))throw new ArgumentOutOfRangeException();
            double steps=elapsed/timeStepSeconds,tier=1+loop*loopMultiplier,location=instance?instanceMultiplier:1;
            return new ExpeditionDifficulty{health=Math.Min(maxHealth,(1+steps*healthPerStep)*(1+(players-1)*healthPerExtraPlayer)*tier*location),
                damage=Math.Min(maxDamage,(1+steps*damagePerStep)*tier*location),count=Math.Min(maxCount,1+(players-1)*countPerExtraPlayer)};
        }
    }
    [Serializable] public sealed class ExpeditionDifficulty {public double health,damage,count;}
}
