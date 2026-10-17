using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private int lockTarget;private float lockPressed=-1,lockHidden=-1;private bool lockAcquired;private Vector3? lockPoint;
        private bool LockEligible=>arena?.Local!=null&&arena.Local.Hp>0&&arena.Local.Weapon.@class=="melee"&&arena.Local.Controlling==null&&arena.Local.ControlledBy==null&&!(arena.Local.Seat.spec=="siren"&&arena.Local.Siren?.Ranged==true)&&match.Phase==NativePhase.Fight&&arena.Pve?.Holding!=true;
        private IEnumerable<(int id,Vector3 point)> LockTargets()
        {
            foreach(var p in arena.Pilots)if(p!=arena.Local&&p.Hp>0&&p.Seat.connected&&!match.SameSide(p.Seat,arena.Local.Seat)&&!(p.Seat.AuthorityBoss&&arena.Pve?.Hidden==true))yield return(p.Seat.id,p.Body.transform.position+Vector3.up*.2f);
            foreach(var m in arena.Minions)if(m.Hp>0&&!match.SameSide(m.Owner.Seat,arena.Local.Seat)&&(m.Pve==null||!m.Pve.Travelling&&arena.Time>=(NativeWardEntries.Drop(m.Pve.Entry)?m.Pve.ExposedAt:m.Pve.ActiveAt)))yield return(m.Id,m.Body.transform.position+Vector3.up*(m.Pve?.Crawl==true?0:.2f));
        }
        private void UpdateLock()
        {
            lockPoint=null;if(!LockEligible||menu||ExpeditionCameraHeld){lockTarget=0;lockPressed=-1;return;}Vector3 eye=arena.Local.Body.transform.position+Vector3.up*NativeViewRules.EyeHeight;float now=Time.unscaledTime;
            bool Visible(Vector3 point)=>!NativeSceneQueries.Linecast(arena.PhysicsWorld,eye,point,NativeArena.WorldMask);
            var targets=LockTargets().ToArray();var nearby=targets.Where(t=>Vector3.Distance(t.point,eye)<=16&&Visible(t.point)).ToArray();
            if(Input.GetKeyDown(KeyCode.Tab))
            {
                lockPressed=now;lockAcquired=lockTarget==0;if(lockAcquired){Vector3 look=Quaternion.Euler(pitch,yaw,0)*Vector3.forward;lockTarget=nearby.Where(t=>Vector3.Angle(look,t.point-eye)<=50).OrderBy(t=>Vector3.Angle(look,t.point-eye)/50+Vector3.Distance(t.point,eye)/16).ThenBy(t=>t.id).Select(t=>t.id).FirstOrDefault();}
            }
            if(lockPressed>=0&&!lockAcquired&&now-lockPressed>=.4f){lockTarget=0;lockPressed=-1;}
            if(Input.GetKeyUp(KeyCode.Tab)&&lockPressed>=0){if(!lockAcquired&&lockTarget!=0){var current=targets.FirstOrDefault(t=>t.id==lockTarget);float angle=Mathf.Atan2(current.point.x-eye.x,current.point.z-eye.z)*Mathf.Rad2Deg;lockTarget=nearby.Where(t=>t.id!=lockTarget).OrderBy(t=>Mathf.Repeat(Mathf.Atan2(t.point.x-eye.x,t.point.z-eye.z)*Mathf.Rad2Deg-angle,360)).Select(t=>t.id).DefaultIfEmpty(lockTarget).First();}lockPressed=-1;}
            var target=targets.FirstOrDefault(t=>t.id==lockTarget);if(target.id==0||Vector3.Distance(target.point,eye)>24){lockTarget=0;return;}
            if(!Visible(target.point)){if(lockHidden<0)lockHidden=now;if(now-lockHidden>=1)lockTarget=0;return;}lockHidden=-1;lockPoint=target.point;LockLook(eye,target.point,out float wantYaw,out float wantPitch);float blend=1-Mathf.Exp(-14*Time.unscaledDeltaTime);yaw=Mathf.LerpAngle(yaw,wantYaw,blend);pitch=Mathf.Lerp(pitch,wantPitch,blend);
        }
        private void LockHud(float scale)
        {if(!lockPoint.HasValue)return;var p=cameraView.WorldToScreenPoint(lockPoint.Value);if(p.z<=0)return;GUI.Label(new Rect(p.x/scale-12,(Screen.height-p.y)/scale-14,28,30),"◇",heading);}
    }
}
