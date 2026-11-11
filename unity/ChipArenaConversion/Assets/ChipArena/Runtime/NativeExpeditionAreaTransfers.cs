using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // Gate harness/service for the real physical subset path. Not the old global
    // EnterDoor/SetLoading path; caller must supply per-area runtime membership.
    public sealed class NativeExpeditionAreaTransfers
    {
        public ExpeditionCohorts Cohorts {get;}
        public ExpeditionJoinRules Joins {get;}
        private readonly ExpeditionAreaRegistry<NativeExpeditionAreaRuntime> runtimes;
        private readonly ExpeditionLayout layout;private readonly Func<string,int> seat;
        private readonly Dictionary<string,List<ExpeditionPoint>> reservations=new Dictionary<string,List<ExpeditionPoint>>(StringComparer.Ordinal);
        private long nextCohort;
        public string LastFailure {get;private set;}
        public NativeExpeditionAreaTransfers(ExpeditionAreaRegistry<NativeExpeditionAreaRuntime> runtimes,ExpeditionLayout layout,
            ExpeditionCohorts cohorts,Func<string,int> seat)
        {
            this.runtimes=runtimes??throw new ArgumentNullException(nameof(runtimes));this.layout=layout??throw new ArgumentNullException(nameof(layout));
            Cohorts=cohorts??throw new ArgumentNullException(nameof(cohorts));this.seat=seat??throw new ArgumentNullException(nameof(seat));Joins=new ExpeditionJoinRules(layout);
        }
        public void Step(double elapsed,Func<string,ExpeditionMember> member,Func<string,ExpeditionJoinClosedReason> admission)
        {
            if(member==null||admission==null)throw new ArgumentNullException();
            Cohorts.Advance(elapsed);
            // A death or extraction during loading cancels its immutable cohort.
            foreach(var transfer in Cohorts.Active)
                if(transfer.Members.Any(a=>member(a)==null||!member(a).alive||member(a).extracted))Cohorts.Cancel(transfer.Id);
            foreach(var transfer in Cohorts.Active)Cohorts.Commit(transfer.Id);
            var roster=new List<ExpeditionJoinMember>();
            foreach(var m in Cohorts.Members)
            {
                var state=member(m.Account);var area=runtimes.Find(m.Area);var pilot=area?.Arena.Pilots.SingleOrDefault(p=>p.Seat.id==seat(m.Account));
                if(state==null||pilot==null)continue;
                roster.Add(new ExpeditionJoinMember{Account=m.Account,Area=m.Area,Feet=NativeArena.Vec(pilot.Body.position)-new V3(0,.85f,0),
                    Alive=state.alive&&pilot.Hp>0,Connected=m.Connected&&state.connected,Extracted=state.extracted,Loading=m.Pending!=null||!m.Ready,Transferring=m.Pending!=null,
                    Choosing=state.choosing,Carrying=state.carriedBy!=null||Cohorts.Members.Any(other=>member(other.Account)?.carriedBy==m.Account)||!area.Arena.CanTransferPilot(pilot)});
            }
            Joins.Step(elapsed,roster,admission,(door,destination,accounts)=>Begin("join:"+checked(++nextCohort),layout.world,destination,door,accounts));
            foreach(var ring in Joins.Views.Where(r=>r.joinBlocked>0))foreach(var m in roster.Where(m=>m.Area==layout.world&&m.Alive&&m.Connected))
            {
                var shape=new ExpeditionJoinCylinder{x=ring.x,y=ring.y,z=ring.z,r=ring.r,h=ring.h};if(!shape.Contains(m.Feet+new V3(0,NativeLocomotionRules.HalfHeight,0)))continue;
                var sim=runtimes.Find(m.Area).Arena;if(sim.Pve!=null&&sim.Pve.Rows.TryGetValue(seat(m.Account),out var row))
                {row.Notice="Waiting for everyone in the circle: finish loading or chip selection; release carries and hires.";row.NoticeUntil=sim.Time+1;}
            }
        }
        public bool Begin(string id,string source,string destination,string door,string[] accounts)
        {
            LastFailure=null;
            var from=layout.Area(source);var to=layout.Area(destination);
            if(from==null||to==null){LastFailure="Unknown transfer area.";return false;}
            ExpeditionPoint[] slots;
            if(source==layout.world)
            {
                var anchor=from.anchors.SingleOrDefault(a=>a.id==door&&a.kind=="door"&&a.building==destination);
                if(anchor==null){LastFailure="Entrance anchor missing.";return false;}slots=to.doors.Single().entries;
            }
            else
            {
                if(destination!=layout.world){LastFailure="Return destination is not the outdoor world.";return false;}
                var entry=to.doors.SingleOrDefault(d=>to.anchors.Any(a=>a.id==d.anchor&&a.building==source));
                if(entry==null){LastFailure="Outdoor return door missing.";return false;}slots=entry.returns;
            }
            bool begun=Cohorts.Begin(id,source,destination,accounts,NativeExpeditionLoadWindow.Seconds,(s,d,cohort)=>Prepare(s,d,cohort,slots));
            if(!begun&&LastFailure==null)LastFailure="Cohort refused: check connection, readiness, pending transfer and source membership.";
            return begun;
        }
        private IExpeditionCohortPlacement Prepare(string source,string destination,string[] accounts,ExpeditionPoint[] slots)
        {
            var from=runtimes.Find(source);var to=runtimes.Find(destination);
            if(from?.Load!=ExpeditionRuntimeLoad.Ready||to?.Load!=ExpeditionRuntimeLoad.Ready){LastFailure="Source or destination runtime is not ready.";return null;}
            if(!reservations.TryGetValue(destination,out var reserved))reservations[destination]=reserved=new List<ExpeditionPoint>();
            var chosen=new List<ExpeditionPoint>();var assignments=new Dictionary<int,ExpeditionPoint>();
            foreach(var account in accounts)
            {
                var slot=slots.FirstOrDefault(p=>reserved.Concat(chosen).All(q=>(p.Position-q.Position).Length>=2*NativeLocomotionRules.Radius+.1f)&&to.Arena.TransferSlotClear(p));
                int id=seat(account);
                if(slot==null){LastFailure="No clear arrival slot for pilot "+id+" ("+chosen.Count+" of "+accounts.Length+" reserved).";return null;}
                if(id<1||assignments.ContainsKey(id)){LastFailure="Invalid or duplicate transfer seat.";return null;}
                var copy=new ExpeditionPoint{x=slot.x,y=slot.y,z=slot.z,yaw=slot.yaw};chosen.Add(copy);assignments.Add(id,copy);
            }
            reserved.AddRange(chosen);
            void Release(){foreach(var slot in chosen)reserved.Remove(slot);}
            try{return new NativeExpeditionCohortPlacement(from,to,assignments,Release);}
            catch(InvalidOperationException e){LastFailure=e.Message;Release();return null;}
            catch{Release();throw;}
        }
    }
}
