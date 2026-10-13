using System;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private bool DropHeld(NativeMinion m)=>m.Pve!=null&&NativeWardEntries.Drop(m.Pve.Entry)&&Time<m.Pve.ExposedAt;
        // True only while hidden in the air. At touchdown the body becomes physical/hittable, still recovering.
        private bool StepPveDrop(NativeMinion m)
        {
            var a=m.Pve;var landing=Vec(a.Entry.@exit.Position());
            if(DropHeld(m))
            {m.Body.isKinematic=true;m.Collider.enabled=false;m.Body.position=landing+Vector3.up*.9f;return true;}
            if(!a.DropLanded)
            {
                a.DropLanded=true;m.Body.position=landing+Vector3.up*.9f;
                m.Body.isKinematic=false;m.Body.linearVelocity=Vector3.zero;m.Collider.enabled=true;
                m.Body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                PveDropSlam(m,landing);
            }
            return false;
        }
        private void PveSlamShove(NativePilot pilot,Vector3 delta,Vector3 fallback,float impulse,bool blocked)
        {
            if(pilot.Hp<=0||SirenArmor(pilot)||KernelLocked(pilot))return;
            pilot.Knockback+=Vec(NativeHordeDropRules.Shove(Vec(delta),Vec(fallback),impulse,blocked))*(float)pilot.Stats["knockbackTaken"];
            pilot.Grounded=pilot.GroundAttached=false;
        }
        private void PveDropSlam(NativeMinion m,Vector3 landing)
        {
            var forward=Vec(V3.Forward(m.Yaw));var sight=landing+Vector3.up*.45f;var centre=landing+Vector3.up*.9f;
            Emit(EventKind.Impact,m.Owner,m.Id,landing,0,"minion-drop",radius:NativeHordeDrop.SlamRadius);
            foreach(var q in Pilots)
            {
                Vector3 delta=q.Body.position-centre;
                if(q.Hp<=0||!q.Seat.connected||q.Seat.AuthorityBoss||!NativeHordeDropRules.Touch(Vec(delta))||Invulnerable(q)||KernelLocked(q)||
                    !MeleeVisible(sight,q.Body.position,m.Owner,q.Collider,true))continue;
                bool blocked=Time<q.BlockEnd;
                if(blocked)
                {BlockProc(q,m.Owner,q.Collider.ClosestPoint(sight),"clash");PveSlamShove(q,delta,forward,NativeHordeDropRules.Impulse,true);}
                else
                {
                    float distance=new Vector2(delta.x,delta.z).magnitude;
                    float actual=Damage(q,null,NativeHordeDropRules.Damage(distance,false),Vec(NativeHordeDropRules.Outward(Vec(delta),Vec(forward))),0,false,primary:false,melee:true);
                    if(actual>0)PveSlamShove(q,delta,forward,NativeHordeDropRules.Impulse,false);
                }
            }
            foreach(var other in Minions.Where(n=>n!=m&&n.Pve!=null&&n.Hp>0&&!n.Pve.Travelling&&!DropHeld(n)&&Time>=n.Pve.ExposedAt).ToArray())
            {
                Vector3 delta=other.Body.position-centre;
                if(!NativeHordeDropRules.Touch(Vec(delta))||!MeleeVisible(sight,other.Body.position,m.Owner,other.Collider,true))continue;
                var shove=Vec(NativeHordeDropRules.Outward(Vec(delta),Vec(forward)))*NativePveParity.AddImpulse(NativeHordeDropRules.AddImpulse);
                other.Pve.External=Vector3.ClampMagnitude(other.Pve.External+shove,6);
            }
        }
    }
}
