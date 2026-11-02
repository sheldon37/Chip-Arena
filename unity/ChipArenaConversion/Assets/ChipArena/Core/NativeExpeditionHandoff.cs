using System;
using System.Linq;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeExpedition
    {
        private static ExpeditionHandoff CopyHandoff(ExpeditionHandoff h)=>new ExpeditionHandoff{schema=h.schema,splitAreas=h.splitAreas,areas=h.areas.Select(a=>a.Copy()).ToArray(),id=h.id,oldHost=h.oldHost,nextHost=h.nextHost,
            nextEpoch=h.nextEpoch,revision=h.revision,leaving=(string[])h.leaving.Clone(),state=h.state.Copy()};

        /// <summary>Freeze rules before the transport captures its remaining live simulation state.
        /// This is a control-state handoff contract, not a network transport or combat-state serializer.</summary>
        public ExpeditionHandoff PrepareHandoff(string transaction,string[] leaving,ExpeditionHostCandidate[] candidates)
        {
            if(!Mutable||SplitAreas&&(Area!=s.world||areaStates.Values.Any(a=>a.Loading))||s.members.Any(p=>p.transferring)||!Key(transaction)||leaving==null||!leaving.Contains(s.host)||leaving.Distinct().Count()!=leaving.Length||leaving.Any(a=>!CanExtract(a))||candidates==null||s.epoch==int.MaxValue)return null;
            var next=candidates.Where(c=>c!=null&&c.capable&&c.reachable&&!leaving.Contains(c.account)&&Member(c.account)?.connected==true&&Member(c.account)?.extracted==false&&
                ExpeditionValidation.Number(c.loss)&&c.loss>=0&&c.loss<=1&&ExpeditionValidation.Number(c.latencyMs)&&c.latencyMs>=0&&
                ExpeditionValidation.Number(c.jitterMs)&&c.jitterMs>=0)
                .OrderBy(c=>c.loss).ThenBy(c=>c.latencyMs+c.jitterMs).ThenBy(c=>c.account,StringComparer.Ordinal).FirstOrDefault();
            if(next==null)return null;
            handoff=new ExpeditionHandoff{id=transaction,oldHost=s.host,nextHost=next.account,nextEpoch=s.epoch+1,revision=s.revision,
                leaving=(string[])leaving.Clone(),state=s.Copy(),splitAreas=SplitAreas,
                areas=SplitAreas?areaStates.Values.OrderBy(a=>a.Id,StringComparer.Ordinal).Select(a=>new ExpeditionAreaHandoff{
                    id=a.Id,phase=a.Phase,loading=a.Loading,exitAt=a.ExitAt,exitVotes=(string[])a.ExitVotes.Clone()}).ToArray():Array.Empty<ExpeditionAreaHandoff>()};return CopyHandoff(handoff);
        }

        /// <summary>Restore only the rules portion of an authenticated complete migration package.
        /// The destination remains frozen until CommitHandoff. On abort discard this instance;
        /// only the source may resume its old epoch. This does not restore combat, cohorts or transport.</summary>
        public static NativeExpedition RestorePreparedHandoff(ExpeditionHandoff value,string authenticatedNextHost)
        {
            if(value==null||value.schema!=1||!Key(value.id)||!ExpeditionValidation.Valid(value.state)||
                value.oldHost!=value.state.host||value.nextHost!=authenticatedNextHost||!Key(value.nextHost)||
                value.state.epoch==int.MaxValue||value.nextEpoch!=value.state.epoch+1||value.revision!=value.state.revision||
                value.areas==null||value.areas.Length>17||value.leaving==null||value.leaving.Length>8)
                throw new ArgumentException("Invalid Expedition handoff.");
            var run=new NativeExpedition(value.state);
            if(value.splitAreas)
            {
                var ids=run.s.instances.Select(i=>i.id).Concat(new[]{run.s.world}).ToArray();
                if(run.s.area!=run.s.world||value.areas.Length!=ids.Length||value.areas.Any(a=>a==null||!ids.Contains(a.id))||
                    value.areas.Select(a=>a.id).Distinct().Count()!=ids.Length||run.s.members.Any(p=>!ids.Contains(p.area)))
                    throw new ArgumentException("Invalid handoff area membership.");
                run.areaStates=new Dictionary<string,AreaState>(StringComparer.Ordinal);
                foreach(var a in value.areas)
                {
                    var encounter=run.s.instances.FirstOrDefault(i=>i.id==a.id);
                    if(a.loading||!ExpeditionValidation.Number(a.exitAt)||a.exitAt< -1||a.exitVotes==null||a.exitVotes.Length>8||
                        a.exitVotes.Distinct().Count()!=a.exitVotes.Length||
                        (a.id==run.s.world?(a.phase!=run.s.phase||a.exitAt!= -1||a.exitVotes.Length!=0):a.phase!=ExpeditionPhase.Instance)||
                        a.exitVotes.Any(id=>!run.s.members.Any(p=>p.account==id&&p.area==a.id&&p.alive&&p.connected&&!p.extracted&&!p.transferring))||
                        (a.exitVotes.Length==0)!=(a.exitAt<0)||a.exitVotes.Length>0&&encounter?.cleared!=true)
                        throw new ArgumentException("Invalid handoff area state.");
                    run.areaStates.Add(a.id,new AreaState{Id=a.id,Phase=a.phase,Loading=a.loading,ExitAt=a.exitAt,ExitVotes=(string[])a.exitVotes.Clone()});
                }
                run.scope=run.areaStates[run.s.world];
            }
            else if(value.areas.Length!=0||run.s.members.Any(p=>p.area!=null))
                throw new ArgumentException("Missing split handoff state.");
            // Reapply the source rules for departure and candidate eligibility. Reachability is
            // established by the authenticated coordinator, never by this control-state DTO.
            var prepared=run.PrepareHandoff(value.id,value.leaving,new[]{new ExpeditionHostCandidate{account=value.nextHost,capable=true,reachable=true}});
            if(prepared==null)throw new ArgumentException("Ineligible Expedition handoff.");
            run.migration.Restored=true;return run;
        }

        /// <summary>Call only after destination has persisted the complete transport+combat package and confirmed its digest.
        /// Match transaction, source revision, and next epoch to reject stale acknowledgments.</summary>
        public bool CommitHandoff(string transaction,string nextHost,int epoch,long revision)
        {
            if(SplitAreas&&Area!=s.world||handoff==null||s.revision!=handoff.revision||handoff.id!=transaction||handoff.nextHost!=nextHost||handoff.nextEpoch!=epoch||handoff.revision!=revision)return false;
            foreach(string account in handoff.leaving)ExtractMember(account);
            s.host=nextHost;s.epoch=epoch;handoff=null;migration.Restored=false;RefreshVotes();Changed();return true;
        }
        public bool AbortHandoff(string transaction)
        {
            if(migration.Restored||handoff==null||handoff.id!=transaction)return false;handoff=null;return true;
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
