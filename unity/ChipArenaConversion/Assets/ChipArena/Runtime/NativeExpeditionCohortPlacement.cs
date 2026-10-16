using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativePilot { public bool ExpeditionTransferHeld; }
    public sealed partial class NativeArena
    {
        internal bool CanTransferPilot(NativePilot p)=>p!=null&&Pilots.Contains(p)&&!p.Seat.bot&&!p.Seat.AuthorityBoss&&p.Hp>0&&p.Death==null&&
            p.HeldBy==null&&p.Controlling==null&&p.ControlledBy==null&&p.Kernel?.Active!=true&&p.Juggle==null;
        internal void HoldTransferPilot(NativePilot p,bool hold)
        {
            ClearRemoteInput(p.Seat.id);originalInputs.Remove(p);p.Input=new InputFrame{Yaw=p.Yaw,Pitch=p.Pitch};
            p.ExpeditionTransferHeld=hold;p.FrozenPosition=null;
            if(hold)
            {
                CancelFighter(p);CancelMelee(p);CancelSpecial(p);CancelAbilities(p);CancelSiren(p);
                NativeCyberRules.Interrupt(p.Cyber,Time);
                p.Roll=null;p.RollEnd=p.ReloadEnd=p.BlockEnd=p.CastAt=0;p.Fan=null;p.BowDrawAt=-1;p.BowRainNext=0;
                p.Pose.Sprinting=false;p.Knockback=p.MoveVelocity=Vector3.zero;
                if(!p.Body.isKinematic)p.Body.linearVelocity=Vector3.zero;
            }
            else p.Body.useGravity=true;
        }
        internal void DetachTransferPilot(NativePilot p)
        {
            ClearRemoteInput(p.Seat.id);originalInputs.Remove(p);Pilots.Remove(p);Match.Seats.Remove(p.Seat);
            Pve?.Rows.Remove(p.Seat.id);p.FrozenPosition=null;
        }
        internal void AttachTransferPilot(NativePilot p,NativePveRow row)
        {
            if(Pilots.Any(x=>x.Seat.id==p.Seat.id)||Match.Seats.Any(x=>x.id==p.Seat.id))throw new InvalidOperationException("Duplicate destination seat.");
            p.Body.transform.SetParent(null);SceneManager.MoveGameObjectToScene(p.Body.gameObject,Root.scene);p.Body.transform.SetParent(Root.transform);
            p.Collider.sharedMaterial=surfaces["pilot"];Match.Seats.Add(p.Seat);Pilots.Add(p);
            if(Pve!=null)Pve.Rows[p.Seat.id]=row??new NativePveRow{Pilot=p};
        }
        public bool TransferSlotClear(ExpeditionPoint point)
        {
            if(point==null||!ExpeditionSupported(point.Position,NativeLocomotionRules.Radius,2*NativeLocomotionRules.HalfHeight))return false;
            var at=Vec(point.Position);float r=NativeLocomotionRules.Radius;
            return PhysicsWorld.OverlapCapsule(at+Vector3.up*(r+.04f),at+Vector3.up*(2*NativeLocomotionRules.HalfHeight-r+.04f),r,expeditionProbe,1<<ActorLayer,QueryTriggerInteraction.Ignore)==0;
        }
    }

    // A physical two-scene transaction. Production director/wire integration is
    // separately gated; constructing this alone does not turn split parties on.
    public sealed class NativeExpeditionCohortPlacement:IExpeditionCohortPlacement
    {
        private sealed class Entry
        {
            public NativePilot Pilot;public NativePveRow Row;public ExpeditionPoint Slot;
            public Vector3 Origin;public float Yaw;public bool Moved;
        }
        private readonly NativeExpeditionAreaRuntime source,destination;
        private readonly Entry[] entries;private readonly Action release;
        private bool applied,rolledBack,released;
        public NativeExpeditionCohortPlacement(NativeExpeditionAreaRuntime source,NativeExpeditionAreaRuntime destination,
            IReadOnlyDictionary<int,ExpeditionPoint> assignments,Action releaseReservation)
        {
            this.source=source??throw new ArgumentNullException(nameof(source));this.destination=destination??throw new ArgumentNullException(nameof(destination));
            release=releaseReservation??throw new ArgumentNullException(nameof(releaseReservation));
            if(source==destination||assignments==null||assignments.Count==0||assignments.Count>NativeExpeditionParty.Capacity)throw new ArgumentException("Distinct scenes and a nonempty cohort required.");
            entries=assignments.OrderBy(p=>p.Key).Select(pair=>
            {
                var pilot=source.Arena.Pilots.SingleOrDefault(p=>p.Seat.id==pair.Key);var slot=pair.Value;
                if(pilot==null||slot==null)throw new ArgumentException("Source pilot and destination slot required.");
                NativePveRow row=null;if(source.Arena.Pve!=null)source.Arena.Pve.Rows.TryGetValue(pair.Key,out row);
                return new Entry{Pilot=pilot,Row=row,Slot=new ExpeditionPoint{x=slot.x,y=slot.y,z=slot.z,yaw=slot.yaw},Origin=pilot.Body.position,Yaw=pilot.Yaw};
            }).ToArray();
            if(!Validate())throw new InvalidOperationException("Cohort placement is not safe.");
            foreach(var entry in entries)source.Arena.HoldTransferPilot(entry.Pilot,true);
        }
        public bool Validate()
        {
            if(rolledBack||applied||source.Load!=ExpeditionRuntimeLoad.Ready||destination.Load!=ExpeditionRuntimeLoad.Ready||
                source.Arena.Pve?.Holding==true||destination.Arena.Pve?.Holding==true||source.Scene==destination.Scene)return false;
            // Pilot state holds arena-clock deadlines. Until clock conversion is
            // implemented, unequal clocks must refuse instead of refreshing cooldowns.
            if(Math.Abs(source.Arena.Time-destination.Arena.Time)>.0001f)return false;
            if(destination.Arena.Pilots.Count(p=>!p.Seat.bot)+entries.Length>NativeExpeditionParty.Capacity)return false;
            for(int i=0;i<entries.Length;i++)
            {
                var e=entries[i];if(!source.Arena.CanTransferPilot(e.Pilot)||e.Pilot.Body.gameObject.scene!=source.Scene||
                    destination.Arena.Match.Seat(e.Pilot.Seat.id)!=null||!destination.Arena.TransferSlotClear(e.Slot))return false;
                for(int j=0;j<i;j++)if((e.Slot.Position-entries[j].Slot.Position).Length<2*NativeLocomotionRules.Radius+.1f)return false;
            }
            return true;
        }
        public void Apply()
        {
            if(!Validate())throw new InvalidOperationException("Cohort destination changed while loading.");
            foreach(var e in entries)
            {
                source.Arena.DetachTransferPilot(e.Pilot);e.Moved=true;
                destination.Arena.AttachTransferPilot(e.Pilot,e.Row);
                e.Pilot.Body.position=NativeArena.Vec(e.Slot.Position)+Vector3.up*(NativeLocomotionRules.HalfHeight+.04f);e.Pilot.Yaw=e.Slot.yaw;
                destination.Arena.HoldTransferPilot(e.Pilot,false);
            }
            Physics.SyncTransforms();RefreshOccupancy();applied=true;Release();
        }
        private void RefreshOccupancy()
        {source.SetOccupants(source.Arena.Pilots.Count(p=>!p.Seat.bot));destination.SetOccupants(destination.Arena.Pilots.Count(p=>!p.Seat.bot));}
        private void Release(){if(released)return;released=true;release();}
        public void Rollback()
        {
            if(rolledBack)return;
            foreach(var e in entries.Reverse())
            {
                if(e.Moved)
                {
                    destination.Arena.DetachTransferPilot(e.Pilot);source.Arena.AttachTransferPilot(e.Pilot,e.Row);
                    e.Pilot.Body.position=e.Origin;e.Pilot.Yaw=e.Yaw;e.Moved=false;
                }
                source.Arena.HoldTransferPilot(e.Pilot,false);
            }
            Physics.SyncTransforms();RefreshOccupancy();rolledBack=true;Release();
        }
    }
}
