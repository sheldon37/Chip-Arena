#if UNITY_EDITOR
using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // The build gate's held guard (NativeGuard.cs) through the arena's own Damage: a hit from in front reduced, paid in stamina and never staggering, one
        // from behind whole, a guard out of stamina broken (and kept down a moment), a gun unable to guard, and the parry still taking all.
        public void ValidateGuard()
        {
            var a=Pilots[0];var b=Pilots[1];
            void Check(bool ok,string message){if(!ok)throw new Exception("Native guard gate: "+message);}
            void Position(NativePilot p,Vector3 pos){p.Body.position=pos;p.Body.linearVelocity=Vector3.zero;p.Knockback=p.MoveVelocity=Vector3.zero;Physics.SyncTransforms();}
            void Setup(string spec)
            {
                Match.Teams=false;
                foreach(var p in new[]{a,b})
                {
                    p.Seat.spec=p==a?spec:"sword";p.Weapon=Content.Weapon(p.Seat.spec);p.Stats=NativeContentRules.Stats(Content,p.Seat.spec,p.Seat.chips);
                    p.Wounds.Clear();p.Death=null;Events.Clear();p.Seat.bot=false;p.Seat.alive=p.Seat.connected=true;p.Hp=p.Stamina=p.Mana=100;p.Collider.enabled=true;p.Body.isKinematic=false;
                    p.Ready=p.AttackEnd=p.BlockEnd=p.BlockReady=p.RollEnd=p.RollReady=p.StunEnd=p.StaggerImmune=p.ReloadEnd=0;p.CastAt=0;p.RegenAt=Time+10;p.Swing=null;p.Special=null;p.Roll=null;
                    p.Guarding=false;p.GuardAt=-100;p.GuardBrokenUntil=-100;p.Input=new InputFrame{Yaw=p==a?0:180};p.Yaw=p.Input.Yaw;
                    Position(p,new Vector3(1000,100,p==a?1000:1002));
                }
            }
            try
            {
                Setup("sword");a.Input=new InputFrame{Yaw=0,Guard=true};StepGuard(a);Check(a.Guarding,"a sword holds the guard up");
                float hp=a.Hp,stamina=a.Stamina;Damage(a,b,20,Vector3.back,0,false,melee:true,weaponHit:true);
                float through=hp-a.Hp,paid=stamina-a.Stamina,share=NativeMeleeRules.GuardShare;
                Check(Math.Abs(through-20*(1-share))<.05f,"a hit from in front lets "+through+" through, not "+20*(1-share));
                Check(Math.Abs(paid-20*share*NativeMeleeRules.GuardStamina)<.05f,"the blocked part paid in stamina ("+paid+")");
                Check(Events.Exists(e=>e.Kind==EventKind.Block&&e.Detail=="hold"),"a blocked hit is an event");
                Check(!StaggerMelee(a,.3f)&&a.Guarding&&a.StunEnd==0,"a guarded hit does not stagger");StepGuard(a);Check(a.Guarding,"the guard stays up through a blocked hit");
                Position(b,new Vector3(1000,100,998));hp=a.Hp;Damage(a,b,20,Vector3.forward,0,false,melee:true,weaponHit:true);Check(Math.Abs(hp-a.Hp-20)<.05f,"a hit from behind is not guarded");
                Position(b,new Vector3(1000,100,1002));a.Stamina=3;hp=a.Hp;Damage(a,b,20,Vector3.back,0,false,melee:true,weaponHit:true);
                Check(!a.Guarding&&a.Stamina==0&&a.GuardBrokenUntil>Time&&Events.Exists(e=>e.Kind==EventKind.Block&&e.Detail=="guard-break"),"out of stamina the guard breaks");
                Check(hp-a.Hp>20*(1-share)+1,"a broken guard lets more through");
                a.Stamina=100;StepGuard(a);Check(!a.Guarding,"a broken guard stays down a moment");
                Setup("pistol");a.Input=new InputFrame{Yaw=0,Guard=true};StepGuard(a);Check(!a.Guarding,"a gun cannot guard");
                Setup("sword");Check(TryBlock(a),"the parry");a.Input=new InputFrame{Yaw=0,Guard=true};StepGuard(a);Check(!a.Guarding,"no guard inside the parry's window");
                hp=a.Hp;Damage(a,b,20,Vector3.back,0,false,melee:true,weaponHit:true);Check(a.Hp==hp,"the parry takes the whole hit");
            }
            finally{foreach(var p in new[]{a,b}){p.Guarding=false;p.Input=new InputFrame();}}
            Debug.Log("Native guard gate: the guard's share, stamina, arc and break; guns cannot guard; the parry beats it");
        }
    }
}
#endif
