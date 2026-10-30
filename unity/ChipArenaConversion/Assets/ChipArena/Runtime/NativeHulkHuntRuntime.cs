using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private RaycastHit[] hulkChargeHits=new RaycastHit[64];
        private Collider[] hulkChargeOverlaps=new Collider[64];
        private readonly Dictionary<int,List<Collider>> hulkChargeIgnored=new Dictionary<int,List<Collider>>();
        private bool HulkKnows(NativeMinion m,NativePilot target,out Vector3 point)
        {
            var a=m.Pve;point=Vec(a.SeenPoint);
            if(target==null||target.Hp<=0||!target.Seat.connected||target.Seat.AuthorityBoss)return false;
            // Explicit Siren/director target assignment and DEV hunt are knowledge, even through walls.
            bool directed=(Pve!=null&&!a.Possessed&&a.Target==target)||m.DevMonster&&m.DevHunt==target.Seat.id;
            if(directed||MeleeVisible(m.Body.position,target.Body.position,m.Owner,target.Collider,true))
            {a.SeenAt=Time;a.SeenSeat=target.Seat.id;a.SeenPoint=Vec(target.Body.position);}
            point=Vec(a.SeenPoint);return a.SeenSeat==target.Seat.id&&Time-a.SeenAt<=NativeHulkMoves.ChargeMemory;
        }
        private float ChooseHulkMove(NativeMinion m,NativePilot target,float contentHit)
        {
            var a=m.Pve;var at=m.Body.position;var facing=Vec(V3.Forward(m.Yaw));int disc=0;
            var center=at+facing*NativeHordeKinds.HulkSmashAhead;
            foreach(var p in Pilots)if(p.Hp>0&&p.Seat.connected&&!p.Seat.AuthorityBoss)
            {var d=p.Body.position-center;d.y=0;if(d.magnitude<=NativeHordeKinds.HulkSmashRadius&&Math.Abs(p.Body.position.y-at.y)<2.5f&&MeleeVisible(at,p.Body.position,m.Owner,p.Collider,true))disc++;}
            var delta=target.Body.position-at;delta.y=0;
            a.Move=(int)NativeHulkMoves.Choose(delta.magnitude,Vector3.SignedAngle(facing,delta,Vector3.up),disc,a.Crawl,a.Lost);
            Emit(EventKind.Impact,m.Owner,m.Id,at,a.Move,NativeHulkMoves.MoveEvent);
            return NativeHulkMoves.Strike(NativeHulkMoves.Of(a.Move),contentHit,a.Crawl,a.Lost);
        }
        private void HulkShove(NativePilot p,Vector3 heading,float impulse,float pop,bool block)
        {
            if(p.Hp<=0||SirenArmor(p)||KernelLocked(p))return;
            p.Knockback+=(heading*(impulse/65f)+Vector3.up*pop)*(block?.5f:1)*(float)p.Stats["knockbackTaken"];
            p.Grounded=p.GroundAttached=false;
        }
        private void HulkHit(NativeMinion m,NativePilot p,Vector3 heading,float damage,float impulse,float pop)
        {
            if(Invulnerable(p))return;bool block=Time<p.BlockEnd;
            if(block)BlockProc(p,m.Owner,p.Body.position,"clash");
            else if(Damage(p,null,damage*m.Pve.DamageScale,heading,0,false,primary:false,melee:true,damageKind:m.DevMonster?"dev-monster":null)<=0)return;
            HulkShove(p,heading,impulse,pop,block);
        }
        private void HulkMoveStrike(NativeMinion m)
        {
            var a=m.Pve;var at=m.Body.position;var forward=Vec(V3.Forward(m.Yaw));var right=Vector3.Cross(Vector3.up,forward);
            foreach(var p in Pilots)
            {
                if(p.Hp<=0||!p.Seat.connected||p.Seat.AuthorityBoss)continue;
                var d=p.Body.position-at;float x=Vector3.Dot(d,right),z=Vector3.Dot(d,forward);
                bool punch=a.Move==1;
                if(!(punch?NativeHulkMoves.InPunch(x,z,d.y,p.Collider.radius):NativeHulkMoves.InSwipe(x,z,d.y,p.Collider.radius))||!MeleeVisible(at+Vector3.up*.45f,p.Body.position,m.Owner,p.Collider,true))continue;
                var outward=d;outward.y=0;
                var push=punch?forward:(-right*NativeHulkMoves.Side(m.LastAttack,a.Lost)+outward.normalized*NativeHulkMoves.SwipeOutward).normalized;
                HulkHit(m,p,push,punch?NativeHulkMoves.PunchDamage:NativeHulkMoves.SwipeDamage,punch?NativeHulkMoves.PunchImpulse:NativeHulkMoves.SwipeImpulse,punch?NativeHulkMoves.PunchPop:NativeHulkMoves.SwipePop);
            }
        }
        private void CancelHulkWind(NativeMinion m,float damage)
        {
            if(!m.Pve.CancelCharge(Time,damage,m.MaxHp))return;
            m.Pve.Struck=true;m.StrikeAt=-1;m.Pve.RecoverAt=Time+ m.Pve.Stats.stagger;
            Emit(EventKind.Impact,m.Owner,m.Id,m.Body.position,3,NativeHulkMoves.ChargeEndEvent);
        }
        private void EndHulkCharge(NativeMinion m,int how,Vector3 contact)
        {
            var a=m.Pve;a.EndCharge(Time,how);a.RecoverAt=Time+NativeHulkMoves.Stop(NativeHulkMoves.EndOf(how));
            if(how==2)a.StaggerAt=a.RecoverAt;
            a.External=Vector3.zero;m.Body.linearVelocity=Vector3.up*m.Body.linearVelocity.y;
            ClearHulkCollisions(m);
            Emit(EventKind.Impact,m.Owner,m.Id,contact,how,NativeHulkMoves.ChargeEndEvent);
        }
        private bool HulkIgnoring(NativeMinion m,Collider c)=>hulkChargeIgnored.TryGetValue(m.Id,out var list)&&list.Contains(c);
        private bool IgnoreHulkCollider(NativeMinion m,Collider c)
        {
            if(!hulkChargeIgnored.TryGetValue(m.Id,out var list)){list=new List<Collider>();hulkChargeIgnored.Add(m.Id,list);}
            if(list.Contains(c))return false;Physics.IgnoreCollision(m.Collider,c,true);list.Add(c);return true;
        }
        private void ClearHulkCollisions(NativeMinion m)
        {if(hulkChargeIgnored.TryGetValue(m.Id,out var list)){foreach(var c in list)if(c!=null&&m.Collider!=null)Physics.IgnoreCollision(m.Collider,c,false);hulkChargeIgnored.Remove(m.Id);}}
        private PveEntry HulkPanel(NativePropBody prop)
        {if(prop==null)return null;foreach(var e in (Pve?.Content??StageContent).entries)if(e.kind=="breach"&&e.block==prop.Definition.id)return e;return null;}
        // 0 solid, 1 interior block, 2 in-bounds entrance panel, 3 destructible prop/structure.
        private int HulkBreakKind(NativePropBody prop)
        {
            if(prop==null||prop.LocalFixture)return 0;
            if(prop.Fragment&&!prop.Indestructible)return 4;
            if(NativeHulkHunt.Protected(Map,prop.Definition))return 0;
            var e=HulkPanel(prop);
            if(e!=null)return NativeHulkHunt.Inside(Map,e.from.Position(),NativeHordeKinds.HulkRadius)&&NativeHulkHunt.Inside(Map,e.@exit.Position(),NativeHordeKinds.HulkRadius)?2:0;
            if(NativeHulkBreach.Eligible(Map,prop.Definition))return 1;
            return !prop.Indestructible&&Props.Contains(prop)?3:0;
        }
        private bool HulkFloor(NativeMinion m,Vector3 at)
        {
            if(!NativeHulkHunt.Inside(Map,Vec(at),m.Collider.radius))return false;
            var physics=root.scene.GetPhysicsScene();float floorY=m.Body.position.y-m.Collider.height/2;
            // Cover the width, not only a centre ray over a narrow bridge.
            var side=Vector3.Cross(Vector3.up,Vec(m.Pve.ChargeHeading))*m.Collider.radius*.9f;
            for(int i=-1;i<=1;i++)
            {var p=at+side*i;if(!physics.Raycast(p+Vector3.up*.1f,Vector3.down,out var hit,m.Collider.height/2+.35f,WorldMask,QueryTriggerInteraction.Ignore)||hit.normal.y<.8f||Math.Abs(hit.point.y-floorY)>.25f)return false;}
            return true;
        }
        private int HulkCast(NativeMinion m,Vector3 at,Vector3 dir,float distance)
        {
            float pole=m.Collider.height/2-m.Collider.radius;int n;var scene=root.scene.GetPhysicsScene();
            while((n=scene.CapsuleCast(at+Vector3.up*pole,at-Vector3.up*pole,m.Collider.radius-.025f,dir,hulkChargeHits,distance,WorldMask,QueryTriggerInteraction.Ignore))==hulkChargeHits.Length)Array.Resize(ref hulkChargeHits,hulkChargeHits.Length*2);
            Array.Sort(hulkChargeHits,0,n,HulkHitComparer.Instance);return n;
        }
        private sealed class HulkHitComparer : IComparer<RaycastHit>
        {public static readonly HulkHitComparer Instance=new HulkHitComparer();public int Compare(RaycastHit a,RaycastHit b)=>a.distance.CompareTo(b.distance);}
        private bool HulkLane(NativeMinion m,Vector3 point)
        {
            var at=m.Body.position;var d=point-at;d.y=0;float distance=d.magnitude;if(distance<NativeHulkMoves.ChargeMinRange)return false;
            float targetFloor=point.y-NativeLocomotionRules.HalfHeight;
            if(Math.Abs(targetFloor-(at.y-m.Collider.height/2))>.35f)return false;
            var dir=d.normalized;m.Pve.ChargeHeading=Vec(dir);
            float pole=m.Collider.height/2-m.Collider.radius;int overlaps;var physics=root.scene.GetPhysicsScene();
            while((overlaps=physics.OverlapCapsule(at+Vector3.up*pole,at-Vector3.up*pole,m.Collider.radius-.025f,hulkChargeOverlaps,WorldMask,QueryTriggerInteraction.Ignore))==hulkChargeOverlaps.Length)Array.Resize(ref hulkChargeOverlaps,hulkChargeOverlaps.Length*2);
            for(int i=0;i<overlaps;i++)if(HulkBreakKind(hulkChargeOverlaps[i].GetComponent<NativePropBody>())==0)return false;
            int n=HulkCast(m,at,dir,distance);
            for(int i=0;i<n;i++)if(HulkBreakKind(hulkChargeHits[i].collider.GetComponent<NativePropBody>())==0)return false;
            for(float t=0;t<distance;t+=.4f)if(!HulkFloor(m,at+dir*t))return false;
            return HulkFloor(m,at+dir*distance);
        }
        private bool HulkBreak(NativeMinion m,NativePropBody prop,Vector3 point)
        {
            int kind=HulkBreakKind(prop);if(kind==0)return false;
            if(kind==4)
            {
                IgnoreHulkCollider(m,prop.GetComponent<Collider>());
                if(prop.Body!=null&&!prop.Body.isKinematic)prop.Body.AddForce(Vec(m.Pve.ChargeHeading)*450,ForceMode.Impulse);
                return true;
            }
            if(kind==3)
            {
                string brokenId=prop.Definition.id;
                DamageProp(prop,Math.Max(1,prop.Health+1),Vec(m.Pve.ChargeHeading)*1000,point,null,"hulk-charge");
                bool broken=prop==null||!Props.Contains(prop)||!prop.GetComponent<Collider>().enabled;
                if(broken)foreach(var chunk in Props)if(chunk.Fragment&&chunk.Definition.parent==brokenId)IgnoreHulkCollider(m,chunk.GetComponent<Collider>());
                return broken;
            }
            // Reuse expired records; never evict a still-open entrance or dynamic wall.
            wardBreaches.RemoveAll(b=>Time>=NativeBreach.SolidAt(b.restoreAt));
            if(wardBreaches.Count>=64)return false;
            NativeBreachRecord record;
            if(kind==1)
            {record=NativeHulkBreach.Begin(prop.Definition,m.Id,Vec(prop.GetComponent<Collider>().ClosestPoint(point)),m.Pve.ChargeHeading,Time);record.breakAt=Time;record.restoreAt=NativeBreach.DefaultRestoreAt(Time);wardBreaches.RemoveAll(b=>b.Dynamic&&b.block==prop.Definition.id);}
            else
            {var e=HulkPanel(prop);wardBreaches.RemoveAll(b=>!b.Dynamic&&b.entry==e.e);record=new NativeBreachRecord{entry=e.e,actor=m.Id,breakAt=Time,restoreAt=NativeBreach.DefaultRestoreAt(Time),point=Vec(point),normal=m.Pve.ChargeHeading};}
            record.charged=true;wardBreaches.Add(record);prop.GetComponent<Collider>().enabled=false;wardBreachRevision++;
            Emit(EventKind.Impact,m.Owner,m.Id,point,0,"minion-breach",normal:Vec(m.Pve.ChargeHeading));return true;
        }
        private bool StepHulkHunt(NativeMinion m,NativePilot target)
        {
            var a=m.Pve;if(Replica||a.Stats.id!="hulk"||Time<a.ActiveAt)return false;
            if(!a.Charging)
            {
                if(a.Crawl||NativeHordeKinds.Armless(a.Lost)||a.Possessed||!a.Struck||Time<a.RecoverAt||Time<a.StaggerAt||Time<a.ChargeReadyAt||Time<a.ChargeProbeAt)return false;
                a.ChargeProbeAt=Time+.25f;
                if(!HulkKnows(m,target,out var aim)||!HulkLane(m,aim))return false;
                a.BeginCharge(Time,target.Seat.id,Vec(m.Body.position),Vec(aim));a.Struck=true;m.StrikeAt=-1;
                a.HasBreachShortcut=false;a.WallBreach=null;a.External=Vector3.zero;
                Emit(EventKind.Impact,m.Owner,m.Id,m.Body.position,target.Seat.id,NativeHulkMoves.ChargeEvent,normal:Vec(a.ChargeHeading));
            }
            if(!a.ChargeLocked)
            {
                var hunted=Pilots.Find(p=>p.Seat.id==a.ChargeTarget);
                if(HulkKnows(m,hunted,out var aim))a.TrackCharge(Vec(aim));
                a.LockCharge(Time);m.Yaw=Mathf.Atan2(a.ChargeHeading.x,a.ChargeHeading.z)*Mathf.Rad2Deg;
                m.Body.linearVelocity=Vector3.up*m.Body.linearVelocity.y;if(!a.ChargeLocked)return true;
            }
            m.Yaw=Mathf.Atan2(a.ChargeHeading.x,a.ChargeHeading.z)*Mathf.Rad2Deg;
            var at=m.Body.position;var heading=Vec(a.ChargeHeading);
            if(a.Spent(Vec(at),Time)){EndHulkCharge(m,3,at);return true;}
            float speed=NativeHulkMoves.ChargeSpeed*(Time<a.ChargeSlowUntil?NativeHulkMoves.ChargeSlow:1),distance=speed/60f;
            var to=at+heading*distance;NativePilot victim=null;float pilotFraction=2;
            foreach(var p in Pilots)if(p.Hp>0&&p.Seat.connected&&!p.Seat.AuthorityBoss&&!Invulnerable(p)&&NativeHulkHunt.Sweep(Vec(at),Vec(to),Vec(p.Body.position),m.Collider.radius+p.Collider.radius,out float t)&&t<pilotFraction){victim=p;pilotFraction=t;}
            if(!HulkFloor(m,at+heading*(distance*Math.Min(1,pilotFraction)))){EndHulkCharge(m,2,at);return true;}
            float pole=m.Collider.height/2-m.Collider.radius;int overlaps;var physics=root.scene.GetPhysicsScene();
            while((overlaps=physics.OverlapCapsule(at+Vector3.up*pole,at-Vector3.up*pole,m.Collider.radius-.025f,hulkChargeOverlaps,WorldMask,QueryTriggerInteraction.Ignore))==hulkChargeOverlaps.Length)Array.Resize(ref hulkChargeOverlaps,hulkChargeOverlaps.Length*2);
            for(int i=0;i<overlaps;i++)
            {var c=hulkChargeOverlaps[i];if(c==null||!c.enabled||HulkIgnoring(m,c))continue;var prop=c.GetComponent<NativePropBody>();int breakKind=HulkBreakKind(prop);if(!HulkBreak(m,prop,c.ClosestPoint(at))){EndHulkCharge(m,2,at);return true;}if(breakKind!=4)a.ChargeSlowUntil=Time+NativeHulkMoves.ChargeSlowFor;}
            int n=HulkCast(m,at,heading,distance);
            for(int i=0;i<n;i++)
            {
                var hit=hulkChargeHits[i];if(hit.collider==null||!hit.collider.enabled||HulkIgnoring(m,hit.collider))continue;if(hit.distance>distance*Math.Min(1,pilotFraction))break;
                var prop=hit.collider.GetComponent<NativePropBody>();int breakKind=HulkBreakKind(prop);
                if(!HulkBreak(m,prop,hit.point))
                {m.Body.position=at+heading*Math.Max(0,hit.distance-.01f);EndHulkCharge(m,2,hit.point);return true;}
                if(breakKind!=4)a.ChargeSlowUntil=Time+NativeHulkMoves.ChargeSlowFor;
            }
            if(victim!=null)
            {m.Body.position=at+heading*(distance*pilotFraction);HulkHit(m,victim,heading,NativeHulkMoves.ChargeDamage,NativeHulkMoves.ChargeImpulse,NativeHulkMoves.ChargePop);EndHulkCharge(m,1,victim.Body.position);return true;}
            foreach(var other in Minions)if(other!=m&&other.Hp>0&&other.Pve!=null&&NativeHulkHunt.Sweep(Vec(at),Vec(to+heading*.25f),Vec(other.Body.position),m.Collider.radius+other.Collider.radius,out _))
            {
                if(!IgnoreHulkCollider(m,other.Collider))continue;
                var side=Vector3.Cross(Vector3.up,heading);if(Vector3.Dot(other.Body.position-at,side)<0)side=-side;
                other.Pve.External+=side*NativePveParity.AddImpulse(450);
            }
            // No normal steering, attacks, hit shoves or crowd correction can replace this heading.
            a.External=Vector3.zero;speed=NativeHulkMoves.ChargeSpeed*(Time<a.ChargeSlowUntil?NativeHulkMoves.ChargeSlow:1);
            m.Body.linearVelocity=heading*speed+Vector3.up*m.Body.linearVelocity.y;return true;
        }
    }
}
