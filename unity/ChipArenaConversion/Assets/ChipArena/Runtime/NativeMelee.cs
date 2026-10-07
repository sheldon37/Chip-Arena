using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private RaycastHit[] meleeSightHits=new RaycastHit[32];
        public bool RiposteOpen(NativePilot p)=>Time<p.BlockEnd&&p.RiposteAt>=p.BlockAt;
        private void CancelMelee(NativePilot p){p.Swing=null;p.HitPending=false;p.AttackEnd=0;}
        private bool TryBlock(NativePilot p)
        {
            if(Time<p.BlockReady||Time<p.BlockEnd||Time<p.RollEnd||Incapacitated(p)||Time<p.AttackEnd)return false;
            p.BlockAt=Time;p.BlockEnd=Time+NativeMeleeRules.BlockDuration(p.Stats);p.BlockReady=Time+NativeMeleeRules.BlockCooldown(p.Stats);
            p.RiposteAt=-100;CancelMelee(p);CancelSpecial(p);CancelAbilities(p);return true;
        }
        private bool TryRoll(NativePilot p)
        {
            bool live=Time<p.AttackEnd||p.Swing!=null,cancel=!p.Seat.bot&&NativeMeleeRules.Standard(p.Seat.spec)&&live;
            if(SpecialBusy(p)||!p.Grounded||Time<p.RollReady||Time<p.RollEnd||Incapacitated(p)||p.ReloadEnd>Time||p.AbilityCast!=null||p.CastAt>0||live&&!cancel||Time<p.BlockEnd&&!RiposteOpen(p))return false;
            var roll=NativeContentRules.Roll(Content,p.Seat.spec,p.Stats);if(p.Stamina<roll.cost)return false;
            if(cancel&&NativeMeleeRules.BeforeStrike(p.Swing,Time))p.Ready=Math.Min(p.Ready,Time+(float)roll.duration);
            p.Roll=roll;p.RollAt=Time;p.RollEnd=Time+(float)roll.duration;p.RollReady=Time+(float)roll.cooldown;p.Stamina-=(float)roll.cost;
            CancelAbilities(p);CancelMelee(p);p.RegenAt=p.RollEnd+.45f;p.BlockEnd=0;p.MoveVelocity=Vector3.zero;
            p.RollDirection=new Vector3(p.Input.X,0,p.Input.Z).normalized;if(p.RollDirection.sqrMagnitude<.01f)p.RollDirection=Quaternion.Euler(0,p.Input.Yaw,0)*Vector3.forward;
            return true;
        }
        private bool TryMelee(NativePilot p)
        {
            if(SpecialBusy(p)||!NativeMeleeRules.Standard(p.Seat.spec)||Time<p.RollEnd||Incapacitated(p)||Time<p.AttackEnd||Time<p.Ready||p.ReloadEnd>Time||p.AbilityCast!=null||p.CastAt>0||Time<p.BlockEnd&&!RiposteOpen(p))return false;
            float cost=(float)(p.Weapon.cost*p.Stats["resourceCost"]);if(p.Stamina<cost)return false;
            p.Stamina-=cost;p.Combo++;p.LastAttack=p.AttackAt=Time;p.Yaw=p.Input.Yaw;
            p.Swing=NativeMeleeRules.Begin(p.Weapon,p.Stats,Time,p.Combo+1,p.Input.Yaw*Mathf.Deg2Rad);
            p.HitAt=p.Swing.HitAt;p.HitPending=true;p.AttackEnd=p.Ready=p.Swing.Until;p.RegenAt=p.AttackEnd+.45f;
            p.BlockEnd=0;p.MoveVelocity=Vector3.zero;return true;
        }
        private bool MeleeVisible(Vector3 origin,Vector3 target,NativePilot owner,Collider destination)
        {
            Vector3 delta=target-origin;float distance=delta.magnitude;if(distance<.0001f)return true;
            var physics=root.scene.GetPhysicsScene();int n;
            // Grow on saturation: a full nonalloc query is not guaranteed to contain the closest hit.
            while((n=physics.Raycast(origin,delta.normalized,meleeSightHits,distance,ShotMask,QueryTriggerInteraction.Ignore))==meleeSightHits.Length)
                Array.Resize(ref meleeSightHits,meleeSightHits.Length*2);
            RaycastHit? nearest=null;
            for(int i=0;i<n;i++)if(meleeSightHits[i].collider!=owner.Collider&&(!nearest.HasValue||meleeSightHits[i].distance<nearest.Value.distance))nearest=meleeSightHits[i];
            return !nearest.HasValue||nearest.Value.collider==destination||nearest.Value.distance>=distance-.12f;
        }
        private void StepMelee(NativePilot p)
        {
            var swing=p.Swing;if(swing==null)return;
            if(Time>swing.HitUntil){p.HitPending=false;if(Time>=swing.Until)p.Swing=null;return;}
            if(Time<swing.HitAt)return;
            Vector3 origin=p.Body.position+Vector3.up*.15f;
            foreach(var target in Pilots)
            {
                if(target.Hp<=0||!Enemy(p,target))continue;var delta=target.Body.position-origin;
                if(!NativeMeleeRules.Contact(swing,Time,Vec(delta),.45f)||!swing.Contact("pilot:"+target.Seat.id,Time,Vec(delta),.45f,MeleeVisible(origin,target.Body.position,p,target.Collider)))continue;
                float actual=Damage(target,p,(float)(p.Weapon.damage*p.Stats["damage"]),(delta+Vector3.up*.15f).normalized,(float)(p.Weapon.impulse*p.Stats["impulse"]),false,melee:true);
                if(actual>0&&target.Hp>0)PrimaryMeleeImpact(p,target);
                if(p.Hp<=0||p.Swing!=swing)return; // A block or thorns death interrupts remaining contacts.
            }
            foreach(var prop in Props.ToArray())
            {
                if(prop==null||prop.Indestructible)continue;Vector3 delta=prop.transform.position-origin;float extra=Math.Min(.8f,prop.Definition.size.Length*.3f);
                if(!NativeMeleeRules.Contact(swing,Time,Vec(delta),extra)||!swing.Contact("prop:"+prop.Definition.id,Time,Vec(delta),extra,MeleeVisible(origin,prop.transform.position,p,prop.GetComponent<Collider>())))continue;
                DamageProp(prop,(float)(p.Weapon.damage*p.Stats["damage"]),(delta+Vector3.up*.15f).normalized*(float)(p.Weapon.impulse*p.Stats["impulse"]),prop.transform.position);
            }
        }
        private void StunMelee(NativePilot p,float seconds)
        {
            CancelMelee(p);CancelSpecial(p);CancelAbilities(p);p.Roll=null;p.RollEnd=Time;p.BlockEnd=0;p.LastAttack=-100;p.StunEnd=Math.Max(p.StunEnd,Time+seconds);
            p.MoveVelocity=Vector3.zero;p.Pose.Sprinting=false;
            if(!p.Body.isKinematic)p.Body.linearVelocity=new Vector3(p.Knockback.x,p.Body.linearVelocity.y,p.Knockback.z);
        }
        private bool StaggerMelee(NativePilot p,float seconds)
        {
            if(p.Hp<=0||Incapacitated(p)||Time<p.StaggerImmune)return false;
            StunMelee(p,seconds);p.StaggerImmune=p.StunEnd+.75f;return true;
        }
        private void SuccessfulMeleeBlock(NativePilot blocker,NativePilot attacker,float damage,Vector3 direction,float impulse)
        {
            float force=NativeMeleeRules.Rebound(damage,(float)blocker.Stats["blockForce"]);
            float travel=NativeMeleeRules.ReboundTravel(damage,force,(float)attacker.Stats["damage"],(float)attacker.Stats["knockbackTaken"],(float)blocker.Stats["speed"]);
            bool riposte=!blocker.Seat.bot&&NativeMeleeRules.Standard(blocker.Seat.spec);
            float stun=NativeMeleeRules.BlockStun(damage,impulse);
            if(riposte)stun=Math.Max(stun,NativeMeleeRules.BlockPunish(damage,Math.Max(0,blocker.Ready-Time)+(float)(blocker.Weapon.windup/blocker.Stats["attackRate"]),travel));
            StunMelee(attacker,stun);
            if(riposte){blocker.RiposteAt=Time;attacker.RiposteBy=blocker.Seat.id;attacker.RiposteUntil=attacker.StunEnd;attacker.StaggerImmune=0;}
            Vector3 away=(attacker.Body.position-blocker.Body.position).normalized;if(away.sqrMagnitude<.0001f)away=-direction;
            attacker.Knockback+=away*(force/65)*(float)attacker.Stats["knockbackTaken"];attacker.Ready=Math.Max(attacker.Ready,Time+.18f);
        }
        private void SettleRiposte(NativePilot target,NativePilot hitter,float actual)
        {
            if(target.RiposteBy<0)return;bool live=target.StunEnd==target.RiposteUntil&&Time<target.RiposteUntil;
            if(live&&(target.RiposteBy!=hitter.Seat.id||actual<=0))return;
            target.RiposteBy=-1;if(live)target.StunEnd=Time;
        }
    }
}
