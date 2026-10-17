using System;
using System.Collections.Generic;
using System.Linq;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class ExpeditionClosureView
    {
        public string id,area,state;public double began;
        // Block immediately on sealing; retain collision for the full opening second.
        public bool Blocking=>state!="open";
        public ExpeditionClosureView Copy()=>(ExpeditionClosureView)MemberwiseClone();
    }
    [Serializable] public sealed class ExpeditionAreaBarrierView
    {
        public string target,layout;public long generation;public int ready,required;public bool active;
        public ExpeditionAreaBarrierView Copy()=>(ExpeditionAreaBarrierView)MemberwiseClone();
    }
    [Serializable] public sealed class ExpeditionBossArrivalView
    {
        public string id,area,archetype;public double spawned;
        // Immutable world spawn position for arrival effects; health is refreshed by the combat authority.
        public float x,y,z;public double hp,maxHp;
        public ExpeditionBossArrivalView Copy()=>(ExpeditionBossArrivalView)MemberwiseClone();
    }
    public sealed partial class ExpeditionDirector
    {
        private readonly List<ExpeditionClosureView> closures=new List<ExpeditionClosureView>();
        private readonly List<ExpeditionBossArrivalView> arrivals=new List<ExpeditionBossArrivalView>();
        private readonly HashSet<string> readyMembers=new HashSet<string>(StringComparer.Ordinal);
        private ExpeditionAreaBarrierView barrier=new ExpeditionAreaBarrierView();
        private string observedArea,returnFrom;private int observedLoop;
        public ExpeditionClosureView[] Closures=>closures.Select(c=>c.Copy()).ToArray();
        public ExpeditionBossArrivalView[] BossArrivals=>arrivals.Select(c=>c.Copy()).ToArray();
        public ExpeditionAreaBarrierView Barrier=>barrier.Copy();
        // Transport supplies a verified layout fingerprint, not a peer-supplied revision label.
        public string LayoutIdentity{get;private set;}
        public void BindLayoutIdentity(string fingerprint)
        {
            if(string.IsNullOrEmpty(fingerprint)||fingerprint.Length!=64||fingerprint.Any(c=>!Uri.IsHexDigit(c))||barrier.active)throw new ArgumentException("Validated layout fingerprint required.");
            LayoutIdentity=fingerprint;barrier.layout=fingerprint;
        }
        private void InitializeState()
        {
            var s=Session.Run.Snapshot();observedArea=s.area;observedLoop=s.loop;
            foreach(var area in Layout.areas)
            {
                foreach(var door in area.doors)closures.Add(new ExpeditionClosureView{id=door.anchor,area=area.id,state=Closed(door.anchor,s)?"sealed":"open",began=s.elapsed});
                foreach(var gate in area.gates)closures.Add(new ExpeditionClosureView{id=gate.id,area=area.id,state=GateClosed(area.id,gate,s)?"sealed":"open",began=s.elapsed});
            }
            Session.Sources.Director=this;
        }
        private bool GateClosed(string area,ExpeditionGateLayout gate,ExpeditionSnapshot s)
        {
            if(gate.opens=="final")return !Session.Run.FinalUnlocked;
            var encounter=s.instances.FirstOrDefault(i=>i.id==area);
            return encounter==null||!encounter.cleared&&encounter.bosses.Length==0;
        }
        private bool Closed(string id,ExpeditionSnapshot s)
        {
            var a=Layout.areas.SelectMany(v=>v.anchors).FirstOrDefault(v=>v.id==id);
            return a!=null&&s.phase==ExpeditionPhase.Instance&&a.building==s.area&&!s.instances.First(i=>i.id==s.area).cleared;
        }
        private void UpdateState()
        {
            var s=Session.Run.Snapshot();
            bool newLoop=s.loop!=observedLoop;
            if(newLoop){arrivals.Clear();observedLoop=s.loop;}
            // A new loop needs a fresh load/placement even though victory and town share a map.
            if((newLoop||s.area!=observedArea)&&!Session.Run.SplitAreas)
            {
                returnFrom=newLoop?null:observedArea;observedArea=s.area;
                BeginAreaLoad();s=Session.Run.Snapshot();
            }
            if(barrier.active)
            {
                var required=s.members.Where(m=>m.connected&&!m.extracted).Select(m=>m.account).ToArray();
                readyMembers.RemoveWhere(a=>!required.Contains(a));barrier.required=required.Length;barrier.ready=readyMembers.Count;
            }
            foreach(var c in closures)
            {
                var gate=Layout.Area(c.area).gates.FirstOrDefault(g=>g.id==c.id);
                bool closed=gate!=null?GateClosed(c.area,gate,s):Closed(c.id,s);
                if(closed&&(c.state=="open"||c.state=="opening")){c.state="sealing";c.began=s.elapsed;}
                else if(!closed&&(c.state=="sealed"||c.state=="sealing")){c.state="opening";c.began=s.elapsed;}
                else if(s.elapsed-c.began>=1&&(c.state=="opening"||c.state=="sealing")){c.state=c.state=="opening"?"open":"sealed";c.began+=1;}
            }
        }
        public bool BeginAreaLoad()
        {
            var run=Session.Run;var s=run.Snapshot();
            if(run.SplitAreas||run.Migrating||barrier.active||s.phase==ExpeditionPhase.Ended||LayoutIdentity==null)return false;
            if(!run.SetLoading(true))return false;
            barrier=new ExpeditionAreaBarrierView{active=true,target=s.area,layout=LayoutIdentity,generation=checked(barrier.generation+1),required=s.members.Count(m=>m.connected&&!m.extracted)};
            readyMembers.Clear();return true;
        }
        public bool Ready(string authenticatedAccount,string target,long generation,string fingerprint)
        {
            if(!barrier.active||target!=barrier.target||generation!=barrier.generation||fingerprint!=barrier.layout||!Session.Run.Snapshot().members.Any(m=>m.account==authenticatedAccount&&m.connected&&!m.extracted))return false;
            readyMembers.Add(authenticatedAccount);UpdateState();return true;
        }
        // Authority invokes after its scene is loaded. Callback must atomically place the living party;
        // a failed scene/placement leaves the barrier frozen and can be retried with the same generation.
        public bool CompleteAreaLoad(Func<string,IReadOnlyDictionary<string,ExpeditionPoint>,bool> place)
        {
            UpdateState();if(!barrier.active||barrier.required==0||barrier.ready!=barrier.required||place==null)return false;
            var s=Session.Run.Snapshot();var area=Layout.Area(barrier.target);ExpeditionPoint[] slots;
            if(area.role=="instance")slots=area.doors.Single().entries;
            else if(returnFrom!=null&&returnFrom!=Layout.world)slots=area.doors.Single(d=>area.anchors.Single(a=>a.id==d.anchor).building==returnFrom).returns;
            else slots=area.slots;
            var members=s.members.Where(m=>m.connected&&!m.extracted&&m.alive).OrderBy(m=>m.account,StringComparer.Ordinal).ToArray();
            var assignments=new Dictionary<string,ExpeditionPoint>();
            for(int i=0;i<members.Length;i++){var p=slots[i];assignments.Add(members[i].account,new ExpeditionPoint{x=p.x,y=p.y,z=p.z,yaw=p.yaw});}
            if(!place(barrier.target,assignments)||!Session.Run.SetLoading(false))return false;
            barrier.active=false;returnFrom=null;return true;
        }
        public bool EnterDoor(string authenticatedAccount,string anchor)
        {
            var s=Session.Run.Snapshot();if(LayoutIdentity==null||barrier.active||s.loading||s.phase!=ExpeditionPhase.Town||!s.members.Any(m=>m.account==authenticatedAccount&&m.connected&&m.alive&&!m.extracted)||!AtDoor(authenticatedAccount,anchor))return false;
            var a=Layout.Area(s.area).anchors.FirstOrDefault(a=>a.id==anchor&&a.kind=="door");
            if(a==null||!Session.Run.Enter(a.building))return false;UpdateState();return barrier.active;
        }
        // Register only after the combat authority successfully creates this entity. Archetype is catalog-checked by the caller.
        public bool RecordBossArrival(string id,string archetype,Func<string,bool> knownArchetype,V3 spawn,double hp,double maxHp)
        {
            var s=Session.Run.Snapshot();if(!ValidBossHealth(hp,maxHp)||hp==0||!ExpeditionValidation.Number(spawn.x)||!ExpeditionValidation.Number(spawn.y)||!ExpeditionValidation.Number(spawn.z)||s.loading||Session.Run.Migrating||!ExpeditionValidation.Id(id)||knownArchetype?.Invoke(archetype)!=true||arrivals.Any(a=>a.id==id&&a.area==s.area)||arrivals.Count>=128)return false;
            if(s.phase!=ExpeditionPhase.FinalBoss&&(s.phase!=ExpeditionPhase.Instance||!s.instances.Single(i=>i.id==s.area).bosses.Contains(id)))return false;
            arrivals.Add(new ExpeditionBossArrivalView{id=id,area=s.area,archetype=archetype,spawned=s.elapsed,x=spawn.x,y=spawn.y,z=spawn.z,hp=hp,maxHp=maxHp});return true;
        }
        private static bool ValidBossHealth(double hp,double maxHp)=>ExpeditionValidation.Number(hp)&&ExpeditionValidation.Number(maxHp)&&maxHp>0&&hp>=0&&hp<=maxHp;
        /// <summary>Combat authority calls after damage/healing and before projection; never a guest command.
        /// Area scopes encounter ids. Does not reset spawned or replay the arrival effect.</summary>
        public bool UpdateBossHealth(string area,string id,double hp,double maxHp)
        {
            if(!ValidBossHealth(hp,maxHp))return false;
            var s=Session.Run.Snapshot();var arrival=arrivals.FirstOrDefault(a=>a.area==area&&a.id==id);
            if(arrival==null||area!=s.area||s.loading||Session.Run.Migrating||
                s.instances.Any(i=>i.id==area&&i.defeated.Contains(id))||area==Layout.world&&s.phase!=ExpeditionPhase.FinalBoss)return false;
            arrival.hp=hp;arrival.maxHp=maxHp;return true;
        }
        private void BossDown(string area,string id=null)
        {foreach(var a in arrivals.Where(a=>a.area==area&&(id==null||a.id==id)))a.hp=0;}
        public bool TryBeginFinal()
        {
            var s=Session.Run.Snapshot();var area=Layout.Area(s.area);
            if(s.area!=Layout.world||!Session.Run.FinalUnlocked||barrier.active||area.gates.Any(g=>closures.Single(c=>c.id==g.id).Blocking))return false;
            return s.members.Any(m=>m.connected&&m.alive&&!m.extracted&&position(m.account) is V3 p&&area.bossTrigger.Contains(p))&&Session.Run.BeginFinal();
        }
    }
}
