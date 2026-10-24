using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativePilot { public readonly HashSet<string> TransferProcs=new HashSet<string>();public bool ExpeditionTransferHeld;public readonly Dictionary<string,float> TransferCooldowns=new Dictionary<string,float>(); }
    public sealed partial class NativeArena
    {
        internal bool CanTransferPilot(NativePilot p)=>p!=null&&Pilots.Contains(p)&&!p.Seat.bot&&!p.Seat.AuthorityBoss&&p.Hp>0&&p.Death==null&&
            p.HeldBy==null&&p.Controlling==null&&p.ControlledBy==null&&p.Kernel?.Active!=true&&p.Juggle==null&&p.ScareBy==null&&Time>=p.FrozenUntil&&Time>=p.LaunchUntil&&Pve?.Hires.Any(h=>h.owner==p)!=true;
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
            ClearRemoteInput(p.Seat.id);originalInputs.Remove(p);
            CancelTransferReferences(p);
            string prefix=p.Seat.id+":";foreach(var pair in chipCooldowns.Where(c=>c.Key.StartsWith(prefix,StringComparison.Ordinal)).ToArray()){p.TransferCooldowns[pair.Key]=pair.Value;chipCooldowns.Remove(pair.Key);}
            Pilots.Remove(p);Match.Seats.Remove(p.Seat);
            Pve?.Rows.Remove(p.Seat.id);p.FrozenPosition=null;
        }
        internal void AttachTransferPilot(NativePilot p,NativePveRow row)
        {
            if(Pilots.Any(x=>x.Seat.id==p.Seat.id)||Match.Seats.Any(x=>x.id==p.Seat.id))throw new InvalidOperationException("Duplicate destination seat.");
            p.Body.transform.SetParent(null);SceneManager.MoveGameObjectToScene(p.Body.gameObject,Root.scene);p.Body.transform.SetParent(Root.transform);
            p.Collider.sharedMaterial=surfaces["pilot"];Match.Seats.Add(p.Seat);Pilots.Add(p);
            foreach(var pair in p.TransferCooldowns)chipCooldowns[pair.Key]=Math.Max(chipCooldowns.TryGetValue(pair.Key,out float old)?old:0,pair.Value);
            if(Pve!=null)Pve.Rows[p.Seat.id]=row??new NativePveRow{Pilot=p};
        }
        internal void PlaceTransferredPilot(NativePilot p,Vector3 position,float yaw)
        {
            // Reparenting across scenes dirties the Transform. Publish the teleport to
            // both representations before SyncTransforms can import the old hub pose.
            // Clear interpolation history as this is a discontinuous scene crossing.
            var interpolation=p.Body.interpolation;p.Body.interpolation=RigidbodyInterpolation.None;
            p.Body.transform.position=position;p.Body.position=position;
            p.Yaw=yaw;p.FrozenPosition=null;p.Ground=default;p.GroundPosition=position;
            p.GroundAttached=p.Grounded=false;p.Knockback=p.MoveVelocity=Vector3.zero;
            if(!p.Body.isKinematic){p.Body.linearVelocity=Vector3.zero;p.Body.angularVelocity=Vector3.zero;}
            Physics.SyncTransforms();p.Body.interpolation=interpolation;
            if((p.Body.position-position).sqrMagnitude>.0001f||
                (p.Body.transform.position-position).sqrMagnitude>.0001f)
                throw new InvalidOperationException("Expedition arrival position did not synchronize.");
        }
        private void CancelTransferReferences(NativePilot p)
        {
            // A crossing cancels outgoing area-owned attacks and summons. Cooldowns and
            // existing DOT damage survive; foreign source IDs cannot alias destination actors.
            p.Rewind.Clear();p.LastAttacker=null;foreach(var key in lastPilotDamage.Where(k=>k.Key==p.Seat.id||k.Value.pilot==p.Seat.id).Select(k=>k.Key).ToArray())lastPilotDamage.Remove(key);foreach(var dot in p.Dots.Values)dot.Source=0;
            Bullets.RemoveAll(b=>b.Owner==p.Seat.id||b.SirenTarget==p.Seat.id);
            AbilityShots.RemoveAll(a=>a.Owner==p);AbilityZones.RemoveAll(a=>a.Owner==p||a.Attached==p);abilityEchoes.RemoveAll(a=>a.owner==p);
            MeleeShots.RemoveAll(a=>a.Owner==p||a.Original==p);ChipFields.RemoveAll(a=>a.Owner==p||a.Attached==p);
            FighterEchoes.RemoveAll(a=>a.Owner==p||a.Spare==p);StormTrails.RemoveAll(a=>a.Owner==p);
            SirenFields.RemoveAll(a=>a.Owner==p);foreach(var f in LeetFields.Where(f=>f.Owner==p||f.Target==p).ToArray())RemoveLeetField(f);
            meleeCarries.RemoveAll(a=>a.Owner==p||a.Target==p);meleeFissures.RemoveAll(a=>a.Owner==p);
            contacts.RemoveAll(c=>c.pilot==p||c.prop?.Source==p);
            foreach(var prop in AllBlocks)if(prop.Source==p)prop.Source=null;
            foreach(var m in Minions.ToArray())
            {
                if(m.Owner==p){RemoveMinion(m,false);continue;}if(m.Foe==p)m.Foe=null;
                if(m.Pve!=null){if(m.Pve.Target==p)m.Pve.Target=null;if(m.Pve.LastHit==p)m.Pve.LastHit=null;}
            }
            foreach(var other in Pilots)if(other!=p){if(other.LastAttacker==p)other.LastAttacker=null;if(other.Siren?.Move?.Target==p)CancelSiren(other);}
            if(Pve!=null)
            {
                Pve.Hires.RemoveAll(h=>h.owner==p);
                foreach(var recruit in Pve.Recruits.Where(r=>r.Owner==p).ToArray()){recruit.Pilot.Hp=0;recruit.Pilot.Collider.enabled=false;recruit.Pilot.Body.isKinematic=true;}
            }
        }
        internal bool ReprintFrom(NativeArena source,string account,ExpeditionPoint slot)
        {
            var p=source.ExpeditionPilotFor(account);if(p==null||p.Hp>0||!TransferSlotClear(slot)||Pilots.Any(x=>x.Seat.id==p.Seat.id))return false;
            NativePveRow row=null;source.Pve?.Rows.TryGetValue(p.Seat.id,out row);
            var origin=p.Body.position;var originYaw=p.Yaw;
            try
            {
                source.DetachTransferPilot(p);AttachTransferPilot(p,row);PlaceTransferredPilot(p,Vec(slot.Position)+Vector3.up*(NativeLocomotionRules.HalfHeight+.04f),slot.yaw);
                expeditionDown.Add(account);source.expeditionDown.Remove(account);Physics.SyncTransforms();return true;
            }
            catch
            {
                if(Pilots.Contains(p))DetachTransferPilot(p);if(!source.Pilots.Contains(p))source.AttachTransferPilot(p,row);source.PlaceTransferredPilot(p,origin,originYaw);throw;
            }
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
                destination.Arena.PlaceTransferredPilot(e.Pilot,NativeArena.Vec(e.Slot.Position)+Vector3.up*(NativeLocomotionRules.HalfHeight+.04f),e.Slot.yaw);
                if(!destination.Arena.ExpeditionSupported(NativeArena.Vec(e.Pilot.Body.position-Vector3.up*(NativeLocomotionRules.HalfHeight+.04f)),NativeLocomotionRules.Radius,2*NativeLocomotionRules.HalfHeight))
                    throw new InvalidOperationException("Expedition arrival lost destination floor support.");
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
                    source.Arena.PlaceTransferredPilot(e.Pilot,e.Origin,e.Yaw);e.Moved=false;
                }
                source.Arena.HoldTransferPilot(e.Pilot,false);
            }
            Physics.SyncTransforms();RefreshOccupancy();rolledBack=true;Release();
        }
    }
}
