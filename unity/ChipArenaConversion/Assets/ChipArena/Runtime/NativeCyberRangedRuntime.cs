using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativePilot {public float CyberFieldTick;public bool CyberMineDone=true;}
    public sealed partial class NativeArena
    {
        private NativeCyberKit CyberSnapshot(NativePilot p)
        {
            var copy=p.Cyber.ViewCopy();
            if(p.Seat.spec=="cyberlord"){copy.Ranged??=new NativeCyberRangedState();copy.Ranged.Ready[CyberRangedSpec.Root]=p.KernelReady;if(copy.Version==2){copy.Kit2R.Ready[4]=copy.Kit2.Ready[6]=p.KernelReady;if(p.Kernel?.Id>0){copy.Kit2.Until[6]=p.Kernel.Until;if(p.Kernel.Outcome==3)copy.Kit2.CutAt[6]=p.Kernel.Until;copy.Kit2.Release[6]=Math.Min(copy.Kit2.Release[6],copy.Kit2.Until[6]);}}}
            return copy;
        }
        private bool CyberSwitching(NativePilot p)=>p.Seat.spec=="cyberlord"&&NativeCyberRangedRules.Switching(p.Cyber,Time);
        private bool CyberSwitch(NativePilot p)
        {
            bool blocked=p.Input.Block||p.Input.Roll||p.Input.CancelAbilities||CyberBusy(p)||p.AbilityCast!=null||p.Special!=null||p.Swing!=null||p.CastAt>0||p.Hp<=0||!p.Seat.connected;
            if(!NativeCyberRangedRules.Switch(p.Cyber,Time,blocked))return false;
            p.Ready=p.Cyber.Until;Emit(EventKind.AbilityCast,p,0,p.Body.position,0,"mode-switch");return true;
        }
        private void CyberRangedInput(NativePilot p)
        {
            var s=p.Cyber;var r=s.Ranged;int slot=NativeCyberRangedRules.Slot(p.Input);if(slot<0)return;
            r.Ready[4]=p.KernelReady;
            float cost=(float)(p.Stats["resourceCost"]*(slot==4?1:p.Stats["abilityCost"]));
            if(!NativeCyberRangedRules.Begin(s,slot,Time,ref p.Mana,(float)p.Stats["attackRate"],cost,(float)p.Stats["abilityCooldown"]))return;
            p.CyberAttack=nextAttack++;p.AttackAt=p.LastAttack=Time;p.AttackEnd=p.Ready=s.Until;s.Facing=V3.Forward(p.Input.Yaw);p.Yaw=p.Input.Yaw;
            if(slot==1){r.LanceFrom=Vec(p.Body.position+Vector3.up*.3f);r.LanceTo=r.LanceFrom+GunRules.Direction(p.Input.Yaw,p.Input.Pitch)*CyberRangedSpec.LanceRange;}
            if(slot==2){r.FieldFrom=Vec(p.Body.position+Vector3.up*.3f);r.FieldPoint=Vec(p.Body.position+Vector3.ClampMagnitude(Vec(p.Input.AbilityTarget)-p.Body.position,CyberRangedSpec.FieldRange));}
            if(slot==4){p.KernelReady=r.Ready[4];r.RootFrom=Vec(p.Body.position+Vector3.up*.3f);r.RootTo=r.RootFrom+GunRules.Direction(p.Input.Yaw,p.Input.Pitch)*CyberRangedSpec.RootRange;}
            Emit(EventKind.AbilityCast,p,0,p.Body.position,0,s.Kind);
        }
        private void CyberRangedRelease(NativePilot p,int slot)
        {
            var r=p.Cyber.Ranged;var from=p.Body.position+Vector3.up*.3f;
            if(slot==0)
            {
                if(ProjectileCount>=100)return;
                var dir=Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch));var side=Vector3.Cross(dir,Vector3.up).normalized;
                var start=from+side*(r.BoltHand==0?.18f:-.18f);
                if(SweepNative(from,start-from,CyberRangedSpec.BoltRadius,c=>c.GetComponent<NativePilotBody>()==null&&c.GetComponent<NativeMinionBody>()==null).HasValue)start=from;
                Bullets.Add(new NativeBullet{CyberPacket=true,Detail="bit-bolt",Id=nextBullet++,Attack=p.CyberAttack,Owner=p.Seat.id,Original=p.Seat.id,Spec="cyberlord",Born=Time,Position=start,Velocity=dir*CyberRangedSpec.BoltSpeed,BaseVelocity=dir*CyberRangedSpec.BoltSpeed,FixedSpeed=CyberRangedSpec.BoltSpeed,Damage=CyberRangedSpec.BoltDamage*(float)p.Stats["damage"],Radius=CyberRangedSpec.BoltRadius,Gravity=0,Life=CyberRangedSpec.BoltRange/CyberRangedSpec.BoltSpeed,Travel=CyberRangedSpec.BoltRange,Direct=false,WeaponFx=true});
                Emit(EventKind.Shot,p,0,start,CyberRangedSpec.BoltDamage,"bit-bolt");return;
            }
            if(slot==1||slot==4){p.Yaw=p.Input.Yaw;CyberBeam(p,slot,from);return;}
            if(slot==2)
            {
                var aim=Vec(p.Input.AbilityTarget);var delta=aim-from;var point=from+Vector3.ClampMagnitude(delta,CyberRangedSpec.FieldRange);
                var world=SweepNative(from,point-from,.1f,c=>c.GetComponent<NativePilotBody>()==null&&c.GetComponent<NativeMinionBody>()==null);
                if(world.HasValue)point=world.Value.Point;
                if(!root.scene.GetPhysicsScene().Raycast(point+Vector3.up*.3f,Vector3.down,out var floor,Math.Max(2,Map.bounds?.ceiling??24),WorldMask,QueryTriggerInteraction.Ignore)||floor.normal.y<.65f)return;
                point=floor.point;
                r.FieldFrom=Vec(from);r.FieldPoint=Vec(point);r.FieldLand=Time+CyberRangedSpec.FieldFlight(Vector3.Distance(from,point));r.FieldUntil=r.FieldLand+CyberRangedSpec.FieldForm+CyberRangedSpec.FieldSeconds;p.CyberFieldTick=r.FieldLand+CyberRangedSpec.FieldForm;return;
            }
            if(slot==3)
            {
                var direction=new Vector3(p.Input.X,0,p.Input.Z);if(direction.sqrMagnitude<.001f)direction=-Vec(V3.Forward(p.Input.Yaw));direction.Normalize();
                var start=p.Body.position;var to=KernelTravel(start,start+direction*CyberRangedSpec.SwapDistance);
                r.SwapFrom=Vec(start-Vector3.up*.865f);r.SwapTo=Vec(to-Vector3.up*.865f);r.MineAt=Time+CyberRangedSpec.MineFuse;p.CyberMineDone=false;
                p.Body.position=to;p.MoveVelocity=Vector3.zero;Emit(EventKind.Teleport,p,0,to,0,"proxy-swap",from:start);
            }
        }
        private void CyberBeam(NativePilot p,int slot,Vector3 from)
        {
            bool rootBeam=slot==4;float range=rootBeam?CyberRangedSpec.RootRange:CyberRangedSpec.LanceRange,radius=rootBeam?CyberRangedSpec.RootRadius:CyberRangedSpec.LanceRadius;
            Vector3 end=from+Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch))*range,finish=end;var hitIds=new HashSet<int>();int hits=rootBeam?1:CyberRangedSpec.LancePierce;
            for(int i=0;i<hits;i++)
            {
                var hit=SweepNative(from,end-from,radius,c=>{
                    var target=c.GetComponent<NativePilotBody>()?.Pilot;var add=c.GetComponent<NativeMinionBody>()?.Minion;
                    return !hitIds.Contains(c.GetInstanceID())&&c!=p.Collider&&(target==null||target.Hp>0&&CanHit(p,target)&&!Invulnerable(target))&&(add==null||add.Hp>0&&CanHit(p,add));});
                var stop=hit.HasValue?hit.Value.Point:end;NativePilot wall=null;float nearest=2;
                foreach(var q in Pilots)
                {if(q.Hp<=0||!CanHit(q,p)||!CyberAllowed(q)||Time>=q.Cyber.WallUntil)continue;if(NativeCyberRules.WallCross(Vec(from),Vec(stop),q.Cyber.WallPoint,q.Cyber.WallYaw,radius,out float t)&&t<nearest){wall=q;nearest=t;}}
                if(wall!=null){finish=Vector3.Lerp(from,stop,nearest);Emit(EventKind.Absorb,wall,p.Seat.id,finish,0,"firewall");break;}
                finish=stop;if(!hit.HasValue)break;var contact=hit.Value;hitIds.Add(contact.Collider.GetInstanceID());
                var pilot=contact.Collider.GetComponent<NativePilotBody>()?.Pilot;var minion=contact.Collider.GetComponent<NativeMinionBody>()?.Minion;
                if(pilot!=null)
                {
                    float block=rootBeam&&Time<pilot.BlockEnd?.5f:1;
                    float amount=rootBeam?Math.Min(CyberRangedSpec.RootCap,NativeCyberRangedRules.RootDamage(pilot.Hp,(float)pilot.Stats["hp"])*CyberScale(p))*block:CyberRangedSpec.LanceDamage*CyberScale(p);
                    Damage(pilot,p,amount,Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch)),0,false,direct:false,primary:false,hitPoint:contact.Point,damageKind:CyberRangedSpec.Ids[slot],finalCap:rootBeam?CyberRangedSpec.RootCap*block:float.PositiveInfinity);
                }
                else if(minion!=null)HitMinion(minion,p,(rootBeam?Math.Min(CyberRangedSpec.RootCap,NativeCyberRangedRules.RootDamage(minion.Hp,minion.MaxHp)*CyberScale(p)):CyberRangedSpec.LanceDamage*CyberScale(p)),(end-from).normalized,0,contact.Point,CyberRangedSpec.Ids[slot]);
                else{Emit(EventKind.Impact,p,0,contact.Point,0,CyberRangedSpec.Ids[slot],normal:contact.Normal);break;}
                if(i+1<hits)finish=end;
            }
            var r=p.Cyber.Ranged;if(rootBeam){r.RootFrom=Vec(from);r.RootTo=Vec(finish);}else{r.LanceFrom=Vec(from);r.LanceTo=Vec(finish);}
        }
        private void CyberRangedArea(NativePilot p,Vector3 at,float amount,string detail,Func<Vector3,bool> inside,float shove=0)
        {
            foreach(var q in Pilots.Where(q=>q.Hp>0&&CanHit(p,q)&&inside(q.Body.position)).ToArray())
            {
                if(!MeleeVisible(at+Vector3.up*.4f,q.Body.position,p,q.Collider))continue;
                var d=q.Body.position-at;d.y=0;
                float actual=Damage(q,p,amount*CyberScale(p),d.normalized,0,false,direct:false,primary:false,damageKind:detail);
                if(actual>0)q.Knockback+=d.normalized*shove;
            }
            foreach(var m in Minions.Where(m=>m.Hp>0&&CanHit(p,m)&&inside(m.Body.position)).ToArray())
            {
                if(!MeleeVisible(at+Vector3.up*.4f,m.Body.position,p,m.Collider))continue;
                var d=m.Body.position-at;d.y=0;
                float actual=HitMinion(m,p,amount*CyberScale(p),d.normalized,0,m.Body.position,detail);
                if(actual>0&&shove>0){if(m.Pve!=null)m.Pve.External+=d.normalized*shove;else if(!m.Body.isKinematic)m.Body.linearVelocity+=d.normalized*shove;}
            }
        }
        private void CyberRangedWorld(NativePilot p)
        {
            var r=p.Cyber.Ranged;if(r==null)return;
            if(p.Cyber.Kind=="root-access"&&!p.Cyber.Resolved){p.Yaw=p.Input.Yaw;r.RootFrom=Vec(p.Body.position+Vector3.up*.3f);r.RootTo=r.RootFrom+GunRules.Direction(p.Input.Yaw,p.Input.Pitch)*CyberRangedSpec.RootRange;}
            if(Time>=r.FieldLand+CyberRangedSpec.FieldForm&&Time<r.FieldUntil)
            {
                bool Inside(Vector3 at)=>Math.Abs(at.y-r.FieldPoint.y)<2.5f&&new Vector2(at.x-r.FieldPoint.x,at.z-r.FieldPoint.z).magnitude<=CyberRangedSpec.FieldRadius;
                foreach(var q in Pilots.Where(q=>q.Hp>0&&CanHit(p,q)&&Inside(q.Body.position))){q.SlowFactor=Math.Min(q.SlowFactor,1-CyberRangedSpec.FieldSlow);q.SlowEnd=Math.Max(q.SlowEnd,Time+2f/60);}
                foreach(var m in Minions.Where(m=>m.Hp>0&&CanHit(p,m)&&Inside(m.Body.position))){m.SlowFactor=Math.Min(m.SlowFactor,1-CyberRangedSpec.FieldSlow);m.SlowUntil=Time+2f/60;if(m.Pve!=null){m.Pve.SlowFactor=Math.Min(m.Pve.SlowFactor,1-CyberRangedSpec.FieldSlow);m.Pve.SlowUntil=Time+2f/60;}}
                while(p.CyberFieldTick<=Time+.0001f&&p.CyberFieldTick<r.FieldUntil){CyberRangedArea(p,Vec(r.FieldPoint),CyberRangedSpec.FieldDamage,"lag-field",Inside);p.CyberFieldTick+=CyberRangedSpec.FieldTick;}
            }
            if(!p.CyberMineDone&&Time>=r.MineAt)
            {
                p.CyberMineDone=true;var at=Vec(r.SwapFrom);
                CyberRangedArea(p,at,CyberRangedSpec.MineDamage,"proxy-swap",q=>Vector3.Distance(q,at+Vector3.up*.85f)<=CyberRangedSpec.MineRadius,CyberRangedSpec.MineShove);
                Emit(EventKind.Impact,p,0,at,0,"proxy-swap");
            }
        }
    }
}
