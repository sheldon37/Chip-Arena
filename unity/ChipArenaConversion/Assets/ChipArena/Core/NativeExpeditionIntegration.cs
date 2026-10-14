using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class ExpeditionWorldAnchor
    {
        public string id, area, kind;public float x,y,z;
        public ExpeditionWorldAnchor Copy()=>(ExpeditionWorldAnchor)MemberwiseClone();
    }
    [Serializable] public sealed class ExpeditionChestOffer
    {
        public string id, generation;public int price;public double cooldown;
        public ExpeditionChestOffer Copy()=>(ExpeditionChestOffer)MemberwiseClone();
    }
    /// <summary>Host scene supplies measured interaction evidence, never the guest command.</summary>
    public sealed class ExpeditionReprintEvidence
    {
        public int cost;public bool machine, medic, remote, uninterrupted;public double distance, range, channelSeconds, requiredSeconds;
        public bool Valid=>cost>0&&(machine||medic)&&(!remote||machine)&&uninterrupted&&
            new[]{distance,range,channelSeconds,requiredSeconds}.All(v=>ExpeditionValidation.Number(v)&&v>=0)&&distance<=range&&channelSeconds>=requiredSeconds;
    }

    /// <summary>Single-thread host integration. Replaces authority only after a durable extraction write.</summary>
    public sealed class ExpeditionSession
    {
        public NativeExpedition Run{get;private set;}
        public readonly ExpeditionViewSources Sources=new ExpeditionViewSources();
        private double nextExtractionAttempt;
        private readonly ConversionContent content;private readonly Action<ExpeditionSavepoint> write;
        public Func<string,string,ExpeditionReprintEvidence> ReprintEvidence;
        public Func<string,string,bool> CanClaim;
        public ExpeditionSession(NativeExpedition run,ConversionContent content,Action<ExpeditionSavepoint> write,ExpeditionSavepoint saved=null)
        {Run=run??throw new ArgumentNullException(nameof(run));this.content=content??throw new ArgumentNullException(nameof(content));this.write=write??throw new ArgumentNullException(nameof(write));
            if(saved!=null)
            {
                var state=run.Snapshot();
                if(!saved.Valid()||saved.checkpoint.run!=state.run||saved.checkpoint.seed!=state.seed||saved.checkpoint.world!=state.world||saved.extracted.Any(a=>!state.members.Any(m=>m.account==a&&m.extracted)))
                    throw new ArgumentException("Run does not preserve the saved extraction journal.");
                Sources.Saved=saved.Copy();
            }
        }
        public void Checkpoint()
        {
            var next=ExpeditionSavepoint.Capture(Run);
            if(Sources.Saved!=null)next.extracted=next.extracted.Concat(Sources.Saved.extracted).Distinct().ToArray();
            write(next);Sources.Saved=next.Copy();
        }
        public ExpeditionCommandResult Extract(string account)
        {
            if(!Run.CanExtract(account))return ExpeditionCommandResult.Of(ExpeditionCommandState.Refused,"Reach the sanctuary before extracting.");
            var proposed=new NativeExpedition(Run.Snapshot());if(!proposed.Extract(account))return ExpeditionCommandResult.Of(ExpeditionCommandState.Unavailable,ExpeditionIntegrationHooks.HostTransfer);
            try
            {
                // Capture before extraction: the last player may end the run. Never erase an earlier tombstone.
                var saved=Sources.Saved??ExpeditionSavepoint.Capture(Run);
                var next=saved.WithExtractions(proposed.Snapshot());write(next);
                Sources.Saved=next.Copy();Run=proposed;return ExpeditionCommandResult.Sent;
            }
            catch(Exception e)when(e is IOException||e is UnauthorizedAccessException||e is InvalidOperationException||e is ArgumentException)
            {return ExpeditionCommandResult.Of(ExpeditionCommandState.Failed,"Extraction could not be saved. You are still in the run.");}
        }
        public bool Eligible(string account,string chip)
        {
            var p=Run.Snapshot().members.FirstOrDefault(m=>m.account==account);
            return p!=null&&NativeContentRules.Eligible(content,p.weapon,p.chips.ToDictionary(c=>c.id,c=>c.count)).Any(c=>c.id==chip);
        }
        public string[] Offers(string account,string reward)
        {
            var s=Run.Snapshot();var p=s.members.FirstOrDefault(m=>m.account==account);var r=s.rewards.FirstOrDefault(v=>v.id==reward);
            if(p==null||r==null||r.claimed||r.owner!=null&&r.owner!=account)return null;
            // Ordinary drops require authoritative proximity. Reserved personal rewards can be opened anywhere.
            if(r.owner==null&&(CanClaim==null||!CanClaim(account,reward)))return null;
            var random=new NativePveRandom(NativePveRandom.Hash(s.seed,s.run,reward+":"+account));
            return NativeContentRules.Offers(content,p.weapon,p.chips.ToDictionary(c=>c.id,c=>c.count),random.Next,3,
                c=>!r.highQuality||c.rarity=="epic"||c.rarity=="legendary"||c.rarity=="unique").ToArray();
        }
        public ExpeditionIntegrationHooks Hooks()=>new ExpeditionIntegrationHooks
        {
            Offers=Offers,Eligible=Eligible,Extract=Extract,
            Reprint=(payer,target)=>{var e=ReprintEvidence?.Invoke(payer,target);return e?.Valid==true?(e.cost,e.remote):(0,false);}
        };
        public ExpeditionCommandResult Execute(string account,ExpeditionCommand command)=>new ExpeditionHostCommands(Run,account,Hooks()).Send(command);
        public void Tick(double dt)
        {
            if(!ExpeditionValidation.Number(dt)||dt<0)throw new ArgumentOutOfRangeException(nameof(dt));
            // A stalled host cannot advance an unbounded amount in one UI update.
            dt=Math.Min(dt,1);while(dt>0){double step=Math.Min(dt,.25);Run.Tick(step);dt-=step;}
            if(Run.Snapshot().elapsed>=nextExtractionAttempt)
                foreach(var who in Run.ExtractionDue())if(Extract(who).state!=ExpeditionCommandState.Pending)nextExtractionAttempt=Run.Snapshot().elapsed+1;
            Run.AdvanceWhenReady();
        }
        public void SetWorld(ExpeditionWorldAnchor[] anchors,ExpeditionChestOffer[] chests)
        {
            if(!Run.SetWorld(anchors,chests))throw new ArgumentException("Invalid authored Expedition world or frozen run.");
        }
    }
    public static class ExpeditionWorldRules
    {
        public static bool Valid(ExpeditionWorldAnchor[] anchors,ExpeditionChestOffer[] chests)=>!(anchors==null||chests==null||anchors.Length>ExpeditionValidation.MaxRecords||chests.Length>256||
                anchors.Any(a=>a==null||!ExpeditionValidation.Id(a.id)||!ExpeditionValidation.Id(a.area)||!new[]{"reward","door","chest","machine"}.Contains(a.kind)||!ExpeditionValidation.Number(a.x)||!ExpeditionValidation.Number(a.y)||!ExpeditionValidation.Number(a.z))||
                anchors.Select(a=>a.id).Distinct().Count()!=anchors.Length||chests.Where(c=>c!=null).Select(c=>c.id).Distinct().Count()!=chests.Length||chests.Any(c=>c==null||!ExpeditionValidation.Id(c.id)||!ExpeditionValidation.Id(c.generation)||c.price<1||!ExpeditionValidation.Number(c.cooldown)||c.cooldown<0||!anchors.Any(a=>a.id==c.id&&a.kind=="chest")));
    }
    public sealed partial class NativeExpedition
    {
        public bool SetWorld(ExpeditionWorldAnchor[] anchors,ExpeditionChestOffer[] chests)
        {
            if(!Mutable||!ExpeditionWorldRules.Valid(anchors,chests))return false;
            s.anchors=anchors.Select(a=>a.Copy()).ToArray();s.chests=chests.Select(c=>c.Copy()).ToArray();Changed();return true;
        }
        public bool DropAt(string spawn,float x,float y,float z)
        {
            if(!Position(x,y,z)||s.anchors.Length>=ExpeditionValidation.MaxRecords)return false;
            return Drop(spawn)&&PlaceReward(EventKey("drop:"+spawn),x,y,z);
        }
        public bool DefeatBossAt(string boss,float x,float y,float z,int drops=-1)
        {
            if(!Position(x,y,z)||s.anchors.Length+Math.Max(8,drops)>ExpeditionValidation.MaxRecords)return false;
            if(!DefeatBoss(boss,drops))return false;PlaceBossRewards(EventKey("boss:"+boss),x,y,z);return true;
        }
        public bool FinishFinalAt(float x,float y,float z,int drops=-1)
        {
            if(!Position(x,y,z)||s.anchors.Length+Math.Max(8,drops)>ExpeditionValidation.MaxRecords)return false;
            if(!FinishFinal(drops))return false;PlaceBossRewards(EventKey("final:boss"),x,y,z);return true;
        }
        private static bool Position(float x,float y,float z)=>ExpeditionValidation.Number(x)&&ExpeditionValidation.Number(y)&&ExpeditionValidation.Number(z);
        private void PlaceBossRewards(string boss,float x,float y,float z)
        {foreach(var reward in s.rewards.Where(r=>r.source=="boss"&&NativeExpeditionView.BossKey(r.id)==boss).ToArray())PlaceReward(reward.id,x,y,z);}
        /// <summary>Call from the authenticated encounter/drop adapter at its actual death/spawn point.</summary>
        public bool PlaceReward(string reward,float x,float y,float z)
        {
            if(!Mutable||!s.rewards.Any(r=>r.id==reward)||!ExpeditionValidation.Number(x)||!ExpeditionValidation.Number(y)||!ExpeditionValidation.Number(z))return false;
            var anchor=new ExpeditionWorldAnchor{id=reward,area=s.area,kind="reward",x=x,y=y,z=z};
            var anchors=s.anchors.Where(a=>a.id!=reward).Concat(new[]{anchor}).ToArray();return SetWorld(anchors,s.chests);
        }
    }
}
