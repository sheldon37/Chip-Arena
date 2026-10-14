using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    /// <summary>Scene-independent orchestration. Physics and combat events are supplied only by the authority.
    /// This object never accepts guest positions, reward contents or cache prices.</summary>
    public sealed partial class ExpeditionDirector
    {
        public readonly ExpeditionSession Session;public readonly ExpeditionLayout Layout;
        private readonly Func<string,V3?> position;private readonly Func<V3,V3,bool> visible;
        private readonly int cachePrice,reprintCost;private readonly double restockSeconds;
        private string checkpointStamp;private double retryCheckpoint;
        public string SaveFailure{get;private set;}
        public ExpeditionDirector(ExpeditionSession session,ExpeditionLayout layout,Func<string,V3?> position,
            Func<V3,V3,bool> visible,int cachePrice,int reprintCost,double restockSeconds)
        {
            Session=session??throw new ArgumentNullException(nameof(session));Layout=layout??throw new ArgumentNullException(nameof(layout));
            this.position=position??throw new ArgumentNullException(nameof(position));this.visible=visible??throw new ArgumentNullException(nameof(visible));
            if(cachePrice<1||reprintCost<1||!ExpeditionValidation.Number(restockSeconds)||restockSeconds<1)throw new ArgumentException("Invalid Expedition economy tuning.");
            this.cachePrice=cachePrice;this.reprintCost=reprintCost;this.restockSeconds=restockSeconds;
            var s=session.Run.Snapshot();if(s.world!=layout.world||!s.instances.Select(i=>i.id).OrderBy(i=>i).SequenceEqual(layout.instances.OrderBy(i=>i)))throw new ArgumentException("Run and layout differ.");
            if(s.anchors.Length==0)session.SetWorld(layout.areas.SelectMany(a=>a.anchors).ToArray(),layout.areas.SelectMany(a=>a.anchors).Where(a=>a.kind=="chest").Select(a=>new ExpeditionChestOffer{id=a.id,generation=s.loop+":"+a.id+":0",price=cachePrice}).ToArray());
            session.CanClaim=CanClaim;session.CanPurchase=CanPurchase;session.Sources.ReprintCost=_=>reprintCost;InitializeState();if(layout.compatibility!=null)BindLayoutIdentity(layout.compatibility);
        }
        private bool Reach(string account,V3 point,double range)
        {var p=position(account);return p.HasValue&&(p.Value-point).Length<=range&&visible(p.Value,point);}
        public bool CanClaim(string account,string reward)
        {
            var s=Session.Run.Snapshot();var a=s.anchors.FirstOrDefault(a=>a.id==reward&&a.area==s.area&&a.kind=="reward");
            return a!=null&&s.members.Any(m=>m.account==account&&m.connected&&m.alive&&!m.extracted)&&Reach(account,new V3(a.x,a.y,a.z),2.5);
        }
        public bool CanPurchase(string account,string cache)
        {
            var s=Session.Run.Snapshot();var i=Layout.Area(s.area)?.interactions.FirstOrDefault(i=>i.anchor==cache);
            return i!=null&&s.members.Any(m=>m.account==account&&m.connected&&m.alive&&!m.extracted)&&Reach(account,new V3(i.x,i.y,i.z),i.range);
        }
        public bool Sanctuary(string account)
        {var s=Session.Run.Snapshot();var p=position(account);return p.HasValue&&(s.phase==ExpeditionPhase.Town||s.phase==ExpeditionPhase.Victory)&&Layout.Area(s.area).sanctuary.Any(v=>v.Contains(p.Value));}
        public bool AtDoor(string account,string anchor)
        {
            var s=Session.Run.Snapshot();var door=Layout.Area(s.area).doors.FirstOrDefault(d=>d.anchor==anchor);var p=position(account);
            return door!=null&&p.HasValue&&door.interact.Contains(p.Value)&&visible(p.Value,door.approach.Position);
        }
        public ExpeditionReprintEvidence MachineEvidence(string payer,string machine,double channelSeconds,bool uninterrupted)
        {
            var s=Session.Run.Snapshot();var area=Layout.Area(s.area);var i=area.interactions.FirstOrDefault(i=>i.anchor==machine&&area.anchors.Any(a=>a.id==machine&&a.kind=="machine"));var p=position(payer);
            if(i==null||!p.HasValue||!CanPurchase(payer,machine))return null;
            return new ExpeditionReprintEvidence{cost=reprintCost,machine=true,remote=Sanctuary(payer),uninterrupted=uninterrupted,distance=(p.Value-new V3(i.x,i.y,i.z)).Length,range=i.range,channelSeconds=channelSeconds,requiredSeconds=3};
        }
        /// <summary>Call once from an authenticated encounter death, never a DEV monster or UI callback.</summary>
        public bool MonsterDefeated(string spawn,V3 at,int progress,bool drop)
        {
            var run=Session.Run;if(!run.Progress("kill:"+spawn,progress))return false;
            if(drop&&!run.DropAt(spawn,at.x,at.y,at.z))throw new InvalidOperationException("Encounter drop could not be recorded.");return true;
        }
        public bool BossDefeated(string boss,V3 at)
        {var area=Session.Run.Snapshot().area;if(!Session.Run.DefeatBossAt(boss,at.x,at.y,at.z))return false;BossDown(area,boss);return true;}
        public bool FinalDefeated(V3 at)
        {if(!Session.Run.FinishFinalAt(at.x,at.y,at.z))return false;BossDown(Layout.world);return true;}
        /// <summary>Run after combat and before projection. Session owns the elapsed clock; do not tick it twice.</summary>
        public void Step()
        {
            UpdateState();var run=Session.Run;var s=run.Snapshot();if(run.Migrating||s.loading||s.phase==ExpeditionPhase.Ended)return;
            foreach(var member in s.members.Where(m=>m.connected&&m.alive&&!m.extracted))
            {bool safe=Sanctuary(member.account);if(safe!=member.safe)run.SetSafe(member.account,safe);}
            // The generation includes rule revision; every restock is unique across save/resume without a second journal.
            // Sold caches receive their next availability deadline immediately, then become purchasable on that clock.
            foreach(var offer in s.chests.Where(c=>c.sold))run.RestockChest(offer.id,s.loop+":"+offer.id+":"+run.Snapshot().revision,cachePrice,run.Elapsed+restockSeconds);
            s=run.Snapshot();bool safeParty=s.members.Where(m=>m.connected&&!m.extracted).All(m=>m.alive&&m.safe);
            if(safeParty&&(s.phase==ExpeditionPhase.Town||s.phase==ExpeditionPhase.Victory)&&s.elapsed>=retryCheckpoint&&
                Stamp(s)!=checkpointStamp)Checkpoint();
        }
        private static string Stamp(ExpeditionSnapshot s)=>s.epoch+"|"+s.loop+"|"+s.area+"|"+s.phase+"|"+s.events.Length+"|"+
            string.Join(";",s.members.Select(m=>m.account+":"+m.connected+":"+m.alive+":"+m.safe+":"+m.extracted+":"+m.wallet+":"+m.catchup+":"+m.carriedBy+":"+string.Join(",",m.chips.Select(c=>c.id+"="+c.count))))+"|"+
            string.Join(";",s.rewards.Select(r=>r.id+":"+r.owner+":"+r.claimed+":"+r.consumed+":"+r.selected))+"|"+
            string.Join(";",s.chests.Select(c=>c.generation+":"+c.sold));
        public bool Checkpoint()
        {
            try{Session.Checkpoint();var s=Session.Run.Snapshot();checkpointStamp=Stamp(s);retryCheckpoint=s.elapsed+5;SaveFailure=null;return true;}
            catch(Exception e)when(e is System.IO.IOException||e is UnauthorizedAccessException||e is InvalidOperationException||e is ArgumentException)
            {SaveFailure="Expedition checkpoint failed: "+e.Message;retryCheckpoint=Session.Run.Elapsed+5;return false;}
        }
    }
}
