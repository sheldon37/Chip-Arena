using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativePilot
    {
        /// <summary>The held guard (NativeArena.StepGuard): up while right click is held on a melee weapon, since `GuardAt`; broken
        /// (out of stamina) it cannot go up again before `GuardBrokenUntil`.</summary>
        public bool Guarding;public float GuardAt=-100,GuardBrokenUntil=-100;
    }

    public sealed partial class NativeArena
    {
        // The controls (owner 2026-11-09): "The shield/parry will be moved to F key, right click will now "aim" for ranged weapons (zoom
        // in and give shots less spread) while it will be a held block for melee." "The block for melee will essentially reduce damage by a
        // huge portion as long as user is facing where the damage is coming from. Stamina however will be used on blocked hits." The parry
        // (TryBlock) is unchanged and now on F, unless F is used for an interaction in reach (the owner's pick); this is the held guard.

        /// <summary>Each tick, before attacks resolve: the guard up while it is held on a melee weapon and the pilot is free to (not rolling,
        /// swinging, casting, stunned, broken or out of stamina); down otherwise. Holding it the pilot's stamina does not come back.</summary>
        private void StepGuard(NativePilot p)
        {
            bool free=Time>=p.RollEnd&&Time>=p.AttackEnd&&p.Swing==null&&Time>=p.BlockEnd&&!Incapacitated(p)&&!SpecialBusy(p)&&!TechniqueBusy(p)&&!BombBusy(p)&&!DogBusy(p)&&!DogHeld(p)&&p.AbilityCast==null&&p.CastAt<=0;
            bool want=p.Input.Guard&&NativeMeleeRules.Guards(p.Weapon?.@class)&&free&&Time>=p.GuardBrokenUntil&&p.Stamina>0;
            if(want&&!p.Guarding)p.GuardAt=Time;p.Guarding=want;
            if(p.Guarding)p.RegenAt=Math.Max(p.RegenAt,Time+NativeMeleeRules.GuardRegenPause);
        }

        /// <summary>Damage, for a guarding pilot: a hit from within the guard's arc of where it faces lets only part through (Core
        /// NativeMeleeRules.GuardShare blocked), the blocked part paid from its stamina; with too little stamina the guard breaks, blocks only
        /// what the stamina covers, and cannot go up again for a moment. Each blocked hit is an EventKind.Block "hold" (a break "guard-break")
        /// at the guard's face, its amount the damage stopped, for the effects.</summary>
        private float GuardDamage(NativePilot target,NativePilot source,float amount,Vector3 direction,Vector3? hitPoint)
        {
            if(!target.Guarding||amount<=0)return amount;
            Vector3 from;if(source!=null&&source!=target)from=source.Body.position;else if(hitPoint.HasValue)from=hitPoint.Value-direction.normalized*1.5f;else from=target.Body.position-direction.normalized*1.5f;
            var toward=from-target.Body.position;toward.y=0;if(toward.sqrMagnitude<1e-6f){toward=-direction;toward.y=0;}if(toward.sqrMagnitude<1e-6f)return amount;
            var faces=Vec(V3.Forward(target.Yaw));faces.y=0;if(!NativeMeleeRules.GuardFaces(faces.x,faces.z,toward.x,toward.z))return amount;
            float blocked=amount*NativeMeleeRules.GuardShare,cost=blocked*NativeMeleeRules.GuardStamina;var at=target.Body.position+Vector3.up*.35f+toward.normalized*.55f;
            if(target.Stamina>=cost){target.Stamina-=cost;target.RegenAt=Math.Max(target.RegenAt,Time+NativeMeleeRules.GuardRegenPause);Emit(EventKind.Block,target,0,at,blocked,"hold");return amount-blocked;}
            float covered=cost>0?blocked*target.Stamina/cost:0;target.Stamina=0;target.Guarding=false;target.GuardBrokenUntil=Time+NativeMeleeRules.GuardBreak;
            Emit(EventKind.Block,target,0,at,covered,"guard-break");return amount-covered;
        }
    }
}
