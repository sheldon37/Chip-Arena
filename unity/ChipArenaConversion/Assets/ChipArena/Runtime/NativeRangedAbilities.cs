using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private bool Overdriving(NativePilot p)=>p.Seat.spec=="machinegun"&&NativeRangedRules.Overdrive(Time,p.OverdriveAt,p.OverdriveUntil);
        private bool Venting(NativePilot p)=>p.Seat.spec=="machinegun"&&NativeRangedRules.Vent(Time,p.OverdriveUntil,p.VentUntil);
        private bool RangedQReady(NativePilot p,ContentAbility d)=>d!=null&&d.live&&NativeRangedRules.Q(d.id)&&p.Hp>0&&p.Seat.connected&&Time>=p.QReady&&p.ReloadEnd==0&&!p.Input.Reload&&!p.Input.CancelAbilities&&Time>=p.BlockEnd&&!AbilityBusy(p)&&!Venting(p)&&(d.id!="fan-hammer"||p.Ammo>0&&ProjectileCount<100);
        private bool TryRangedQ(NativePilot p,ContentAbility d)
        {
            if(!RangedQReady(p,d))return false;
            p.Aiming=false;p.AimShots=0;p.BufferedSlot=null;
            p.QReady=Time+NativeAbilityRules.Cooldown(d,p.Stats);
            p.Pose.AbilityId=d.id;p.Pose.AbilityAt=p.Pose.AbilityRelease=Time;
            EmpowerQ(p);Emit(EventKind.AbilityCast,p,0,p.Body.position,0,d.id);
            if(d.id=="fan-hammer")
            {p.Fan=NativeRangedRules.Fan(Time,p.Ammo);StepFan(p);}
            else
            {p.OverdriveAt=Time;p.OverdriveUntil=Time+NativeRangedRules.OverdriveDuration;p.VentUntil=p.OverdriveUntil+NativeRangedRules.VentDuration;}
            return true;
        }
        private void EmpowerQ(NativePilot p)
        {if(p.Stats["qEmpower"]>0){p.EmpowerUntil=Time+3;p.EmpowerShots=(int)Math.Ceiling(.6/p.Weapon.interval);}}
        private void StepFan(NativePilot p)
        {
            var fan=p.Fan;if(fan==null)return;
            if(p.Seat.spec!="pistol"||p.Hp<=0||!p.Seat.connected||Time<p.RollEnd||Time<p.BlockEnd||Incapacitated(p)||p.ReloadEnd>0||p.Input.Reload||p.Input.CancelAbilities)
            {p.Fan=null;return;}
            // The host's fixed tick quantizes .075 s by at most one tick; anchor to cast time to avoid drift.
            while(fan.Due(Time))
            {
                if(!FireRound(p,true)){p.Fan=null;return;}
                fan.Shots++;
                if(p.Ammo<=0){p.Fan=null;Reload(p);return;}
            }
            if(fan.Shots>=fan.Limit)p.Fan=null;
        }
        private bool FireRound(NativePilot p,bool fan=false)
        {
            if(p.Ammo<=0||ProjectileCount>=100||Venting(p))return false;
            p.Ammo--;Fire(p,fan);return true;
        }
    }
}
