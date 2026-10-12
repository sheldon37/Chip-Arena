using System;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // A narrow blade sweep, not an enlarged pilot hitbox. The ordinary body-hit spheres are shared with bullets.
        private void StepDownContacts(NativePilot p,NativeMeleeSwing swing)
        {
            var previous=Vec(swing.AirPrevious);swing.AirPrevious=Vec(p.Body.position);
            if(Time<swing.PlungeAt)return;
            var start=previous-Vector3.up*.865f;var end=p.Body.position-Vector3.up*1.215f;
            foreach(var target in Pilots)
            {
                if(target.Hp<=0||!CanHit(p,target))continue;
                var actor=new Actor{Position=Vec(target.Body.position-Vector3.up*.865f)};
                if(!GunRules.SegmentActor(Vec(start),Vec(end),actor,.08f,out float fraction,out _)||!MeleeVisible(start,target.Body.position,p,target.Collider))continue;
                if(!swing.DirectContact("pilot:"+target.Seat.id))continue;
                float fall=Math.Max(0,swing.AirStartY-Mathf.Lerp(previous.y,p.Body.position.y,fraction));
                float amount=(float)(p.Weapon.damage*p.Stats["damage"]*swing.DamageScale)*NativeAirAttackSimulation.HeightScale(fall)*1.25f;
                float actual=Damage(target,p,amount,Vector3.down,(float)(p.Weapon.impulse*p.Stats["impulse"]),false,melee:true,weaponHit:true,hitPoint:Vector3.Lerp(start,end,fraction));
                if(actual>0){ImpactChips(p,target,null,target.Body.position,Vector3.down,swing.Id,swing.Id);if(target.Hp>0)StaggerMelee(target,.4f);}
                if(p.Hp<=0||p.Swing!=swing)return;
            }
        }
    }
}
