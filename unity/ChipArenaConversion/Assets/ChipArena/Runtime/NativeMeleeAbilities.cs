using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private sealed class MeleeFissure
        {public NativePilot Owner;public NativeMeleeAction Action;public Vector3 Start,Direction;public float At,Until,Length,Travel;public bool Stopped;}
        private sealed class MeleeCarry
        {public NativePilot Owner,Target;public Vector3 Direction;public float Left=6;}
        private readonly List<MeleeFissure> meleeFissures=new List<MeleeFissure>();
        private readonly List<MeleeCarry> meleeCarries=new List<MeleeCarry>();

        private void StepMeleeInput(NativePilot p)
        {
            if(!NativeMeleeRules.Standard(p.Seat.spec))return;
            if(p.Input.Attack&&(NativeMeleeKitRules.BufferOpen(p.Swing,Time)||Time<p.RollEnd&&Time>=p.RollEnd-.2f))p.MeleeBufferedUntil=Time+NativeMeleeFeel.BufferSeconds;
            bool queued=p.MeleeBufferedUntil>=Time,chain=queued&&NativeMeleeKitRules.ChainReady(p.Swing,Time);
            // Holding retains the original full-recovery cadence; only a fresh edge can queue an early chain.
            if((p.Input.Attack||queued||p.Input.Shoot&&p.Swing==null)&&TryMelee(p,chain))p.MeleeBufferedUntil=-1;
        }
        private V3? MeleeLockPoint(NativePilot p,int id)
        {
            if(id==0)return null;var target=MeleeTarget(p,id);
            if(target.collider==null||Vector3.Distance(p.Body.position,target.at)>24||!MeleeVisible(p.Body.position,target.at,p,target.collider))return null;
            return Vec(target.at);
        }
        private V3? KunaiLockPoint(NativePilot p,int id)
        {
            if(id==0)return null;var target=MeleeTarget(p,id);
            if(target.collider==null||!target.collider.enabled||target.pilot!=null&&!Enemy(p,target.pilot)||target.minion!=null&&(!Enemy(p,target.minion)||target.minion.Pve!=null&&(target.minion.Pve.Travelling||Time<(NativeWardEntries.Drop(target.minion.Pve.Entry)?target.minion.Pve.ExposedAt:target.minion.Pve.ActiveAt))))return null;
            var point=target.at+Vector3.up*(target.minion?.Pve?.Crawl==true?0:.2f);
            if(Vector3.Distance(p.Body.position,point)>24||!MeleeVisible(p.Body.position+Vector3.up*.3f,point,p,target.collider))return null;
            return Vec(point);
        }
        private void AimMeleeStep(NativePilot p,bool chained)
        {
            if(p.Swing.SprintAttack)return;
            var point=MeleeLockPoint(p,p.Input.LockTarget);if(!point.HasValue)return;
            var delta=point.Value-Vec(p.Body.position);delta.y=0;
            // Do not lunge backwards to a lock behind the committed starting direction.
            if(V3.Dot(delta.Normalized,V3.Forward(p.Yaw))<=0)return;
            p.Swing.Distance=NativeMeleeFeel.Distance(p.Seat.spec,chained,delta.Length);
            p.Swing.OpenerLunge=!chained&&p.Swing.Distance>NativeMeleeFeel.DefaultStep(p.Seat.spec);
        }
        private void TurnMelee(NativePilot p)
        {
            var s=p.Swing;if(s==null||Time>=s.HitAt)return;
            var point=MeleeLockPoint(p,p.Input.LockTarget);
            float desired=point.HasValue?Mathf.Atan2(point.Value.x-p.Body.position.x,point.Value.z-p.Body.position.z)*Mathf.Rad2Deg:p.Input.Yaw;
            s.Yaw=NativeMeleeFeel.Turn(s.Yaw,desired,p.Seat.spec,Rules.Dt);p.Yaw=s.Yaw*Mathf.Rad2Deg;
        }
        private IEnumerable<(int id,NativePilot pilot,NativeMinion minion,Vector3 at,Collider collider)> MeleeTargets(NativePilot p)
        {
            foreach(var q in Pilots)if(q.Hp>0&&CanHit(p,q))yield return(q.Seat.id,q,null,q.Body.position,q.Collider);
            foreach(var m in Minions)if(m.Hp>0&&CanHit(p,m))yield return(m.Id,null,m,m.Body.position,m.Collider);
        }
        private (int id,NativePilot pilot,NativeMinion minion,Vector3 at,Collider collider) MeleeTarget(NativePilot p,int id)=>MeleeTargets(p).FirstOrDefault(t=>t.id==id);
        private bool MeleeRecastOpen(NativePilot p)=>NativeWhirlwindRules.CanRecast(p.Special,Time)||Time<p.MeleeKit.RecastUntil&&p.MeleeKit.Target!=0&&MeleeTarget(p,p.MeleeKit.Target).collider!=null;
        private void ClearMeleeMark(NativePilot p)
        {
            var k=p.MeleeKit;if(k.MarkTarget!=0)Emit(EventKind.Mark,p,k.MarkTarget,Pilots.Find(t=>t.Seat.id==k.MarkTarget)?.Body.position??Minions.Find(t=>t.Id==k.MarkTarget)?.Body.position??p.Body.position,0,"kunai-mark");
            k.MarkTarget=0;k.MarkUntil=0;if(k.RecastKind=="kunai-mark"){k.RecastUntil=0;k.RecastKind=null;k.Target=0;}
        }
        private void StepMeleeAbilityInput(NativePilot p)
        {
            var k=p.MeleeKit;
            if(k.MarkTarget!=0&&(Time>=k.MarkUntil||MeleeTarget(p,k.MarkTarget).collider==null))ClearMeleeMark(p);
            if(k.RecastKind=="rising-dragon"&&Time>=k.RecastUntil)
            {p.EReady=Math.Min(p.EReady,k.RisingCooldownAt);k.RecastKind=null;k.RecastUntil=0;k.Target=0;}
            if(p.Input.CancelAbilities){if(p.Special!=null&&Time<p.Special.Release)CancelSpecial(p);p.BufferedSlot=null;return;}
            if(p.Input.AbilityE&&MeleeRecastOpen(p)){TryMeleeRecast(p);return;}
            if(p.Input.AbilityQ)TryMeleeAbility(p,"q");
            if(p.Input.AbilityE)TryMeleeAbility(p,"e");
        }
        private bool TryMeleeAbility(NativePilot p,string slot)
        {
            if(!NativeMeleeRules.Standard(p.Seat.spec))return false;
            var d=Content.Ability(p.Seat.spec,slot);if(d==null||!d.live)return false;
            float cost=NativeAbilityRules.Cost(d,p.Stats);
            if(p.Hp<=0||!p.Seat.connected||Incapacitated(p)||SpecialBusy(p)||Time<p.Ready||Time<p.AttackEnd||Time<p.BlockEnd||Time<p.RollEnd||p.ReloadEnd>Time||p.CastAt>0||p.AbilityCast!=null||p.Stamina<cost||(slot=="q"?Time<p.QReady:Time<p.EReady))return false;
            if((d.id=="whirlwind"||d.id=="earthsplitter")&&!p.Grounded||(d.id=="aegis-ricochet"||d.id=="kunai-mark")&&ProjectileCount>=100)return false;
            if(slot=="e"&&MeleeRecastOpen(p))return false;
            CancelMelee(p);p.MeleeBufferedUntil=-1;p.Stamina-=cost;p.Yaw=p.Input.Yaw;
            float release=Time+NativeMeleeKitRules.Windup(d,p.Stats);
            var a=new NativeMeleeAction{Kind=slot=="e"?NativeSpecialRules.Kind(p.Seat.spec):d.id,Fx=d.id,Phase="windup",At=Time,Release=release,Yaw=p.Input.Yaw*Mathf.Deg2Rad,Until=release+(d.id=="flash-step"?.22f:d.id=="bulwark-charge"?.45f:slot=="e"?NativeSpecialRules.Recovery(p.Seat.spec)/(float)p.Stats["attackRate"]:.3f)};
            if(d.id=="whirlwind")a=NativeWhirlwindRules.Begin(Time,p.Input.Yaw*Mathf.Deg2Rad);
            a.Direction=V3.Forward(p.Input.Yaw);a.Aim=p.Input.AbilityTarget.Length>.01f?Vec((Vec(p.Input.AbilityTarget)-p.Body.position-Vector3.up*.3f).normalized):GunRules.Direction(p.Input.Yaw,p.Input.Pitch);
            if(d.id=="kunai-mark")
            {
                var point=KunaiLockPoint(p,p.Input.LockTarget);
                if(point.HasValue){a.LockTarget=p.Input.LockTarget;a.Aim=NativeMeleeKitRules.KunaiAim(Vec(p.Body.position+Vector3.up*.3f),point.Value,30*(float)p.Stats["projectileSpeed"],18*(float)p.Stats["projectileGravity"]);}
            }
            if(d.id=="shadow-step"){var move=new V3(p.Input.X,0,p.Input.Z);if(move.Length>.1f)a.Direction=move.Normalized;}
            p.Special=a;p.AttackAt=-100;p.LastAttack=Time;p.AttackEnd=p.Ready=NativeMeleeKitRules.End(a);p.MoveVelocity=Vector3.zero;p.RegenAt=p.Ready+.45f;
            if(slot=="q")p.QReady=Time+NativeAbilityRules.Cooldown(d,p.Stats);else{p.EReady=Time+NativeAbilityRules.Cooldown(d,p.Stats);p.ECharges=0;}
            p.MeleeKit.Absorbed=0;CaptureMeleeKit(p);Emit(EventKind.AbilityCast,p,0,p.Body.position,0,d.fx,normal:Vec(a.Direction));return true;
        }
        private bool TryMeleeRecast(NativePilot p)
        {
            var k=p.MeleeKit;
            if(p.Special?.Fx=="whirlwind")
            {
                if(Incapacitated(p)||!NativeWhirlwindRules.Recast(p.Special,Time))return false;
                p.AttackEnd=p.Ready=p.Special.RecoverUntil;p.RegenAt=p.Ready+.45f;p.MoveVelocity=Vector3.zero;
                Emit(EventKind.Recast,p,0,p.Body.position,0,"whirlwind");CaptureMeleeKit(p);return true;
            }
            if(!MeleeRecastOpen(p)||Incapacitated(p)||Time<p.RollEnd||Time<p.BlockEnd||p.Swing!=null||p.AbilityCast!=null||p.CastAt>0||p.Special!=null&&p.Special.Fx!=k.RecastKind)return false;
            var t=MeleeTarget(p,k.Target);bool sky=k.RecastKind=="rising-dragon";
            if((t.at-p.Body.position).magnitude>(sky?10:32)||!MeleeVisible(p.Body.position,t.at,p,t.collider))return false;
            // Skyfall follows only the target actually launched by this cast.
            if(sky&&t.pilot!=null&&(t.pilot.Down==null||t.pilot.Down.Source!=p.Seat.id||t.pilot.Down.Landed>=0))return false;
            string fx=k.RecastKind;int target=k.Target;
            if(!sky)ClearMeleeMark(p);k.RecastUntil=0;k.RecastKind=null;k.Target=0;
            p.Special=new NativeMeleeAction{Kind=sky?"skyfall":"kunai-recast",Fx=fx,Phase="recast",Target=target,At=Time,Release=Time+.2f,Until=Time+.65f,Yaw=p.Input.Yaw*Mathf.Deg2Rad};
            p.AttackEnd=p.Ready=p.Special.Until;p.RegenAt=p.Ready+.45f;p.MoveVelocity=Vector3.zero;
            Emit(EventKind.Recast,p,target,t.at,0,fx,from:p.Body.position);CaptureMeleeKit(p);return true;
        }
        private void CaptureMeleeKit(NativePilot p)
        {
            var k=p.MeleeKit;var a=p.Special;
            if(a!=null&&a.Phase=="recovery"&&a.RecoverAt<0)a.RecoverAt=Time;
            k.Kind=a?.Fx;k.Phase=a?.Phase;k.At=a?.At??-100;k.Release=a?.Release??0;k.Until=a?.Until??0;k.RecoverAt=a?.RecoverAt??-1;k.RecoverUntil=NativeMeleeKitRules.End(a);k.Tick=a?.Tick??0;k.Yaw=a?.Yaw??0;
            k.SuperArmour=false;
            if(a?.Fx=="whirlwind")
            {k.RecastKind=NativeWhirlwindRules.CanRecast(a,Time)?"whirlwind":null;k.RecastUntil=k.RecastKind!=null?a.Until:0;k.Target=0;}
            else if(k.RecastKind=="whirlwind"){k.RecastKind=null;k.RecastUntil=0;}
            var shield=MeleeShots.FirstOrDefault(s=>s.Original==p&&s.Shield);k.ShieldOut=shield!=null;k.ShieldUntil=shield==null?0:Time+(shield.Returning?Vector3.Distance(shield.Position,p.Body.position+Vector3.up*.25f)/40:Math.Min(shield.Life,shield.Travel/Math.Max(.01f,shield.Velocity.magnitude)));
            if(shield!=null){k.ShieldPosition=Vec(shield.Position);k.ShieldChain=shield.Chain.ToArray();}
            else k.ShieldChain=Array.Empty<int>();
        }
        private void StepMeleeAbility(NativePilot p,NativeMeleeAction a)
        {
            if(p.Hp<=0||!p.Seat.connected||Incapacitated(p)){CancelSpecial(p);return;}
            if(Time>=NativeMeleeKitRules.End(a)){CancelSpecial(p);return;}
            if(a.Fx=="whirlwind"){StepWhirlwind(p,a);CaptureMeleeKit(p);return;}
            if(!a.Released&&Time>=a.Release)
            {
                a.Released=true;a.Phase="active";
                if(a.Fx=="flash-step"||a.Fx=="bulwark-charge"){a.Until=Time+(a.Fx=="flash-step"?.22f:.45f);p.AttackEnd=p.Ready=a.Until;}
                switch(a.Fx)
                {
                    case "shadow-step":
                        p.MeleeKit.Afterimage=Vec(p.Body.position);p.MeleeKit.AfterimageUntil=Time+1;p.MeleeKit.EmpowerUntil=Time+1.5f;
                        MeleeTeleport(p,p.Body.position+Vec(a.Direction)*6,a.Fx);a.Phase="recovery";break;
                    case "rising-dragon":
                        if(a.Kind=="skyfall"){MeleeRecastStrike(p,a,true);break;}
                        SpecialStrike(p,a,(float)(p.Weapon.range*p.Stats["reach"]),240,"launch");a.Phase="recovery";
                        if(a.Target!=0){p.MeleeKit.Target=a.Target;p.MeleeKit.RecastKind=a.Fx;p.MeleeKit.RecastUntil=Time+.9f;p.MeleeKit.RisingCooldownAt=a.At+7*(float)Math.Max(.45,Math.Min(3,p.Stats["abilityCooldown"]));}
                        else p.EReady=a.At+7*(float)Math.Max(.45,Math.Min(3,p.Stats["abilityCooldown"]));break;
                    case "kunai-mark":if(a.Kind=="kunai-recast"){MeleeRecastStrike(p,a,false);if(p.Special==a)a.Phase="recovery";}else{ThrowMelee(p,a);a.Phase="recovery";}break;
                    case "aegis-ricochet":ThrowMelee(p,a);a.Phase="recovery";break;
                    case "earthsplitter":
                        Vector3 direction=Vec(a.Direction),start=p.Body.position;float length=10;
                        if(root.scene.GetPhysicsScene().Raycast(start,direction,out var wall,length,WorldMask,QueryTriggerInteraction.Ignore))length=wall.distance;
                        meleeFissures.Add(new MeleeFissure{Owner=p,Action=a,Start=start,Direction=direction,At=Time,Until=Time+2,Length=length});
                        p.MeleeKit.FissureStart=Vec(start);p.MeleeKit.FissureDirection=a.Direction;p.MeleeKit.FissureAt=Time;p.MeleeKit.FissureUntil=Time+2;p.MeleeKit.FissureSpeed=25;p.MeleeKit.FissureLength=length;
                        Emit(EventKind.FissureStart,p,0,start,25,a.Fx,.8f,normal:direction);a.Phase="recovery";break;
                }
            }
            if(p.Special!=a)return;
            CaptureMeleeKit(p);
        }
        private void StepWhirlwind(NativePilot p,NativeMeleeAction a)
        {
            NativeWhirlwindRules.Advance(a,Time);
            if(a.Phase=="active"){p.Yaw=p.Input.Yaw;a.Yaw=p.Yaw*Mathf.Deg2Rad;}
            while(p.Special==a&&p.Hp>0&&NativeWhirlwindRules.NextTick(a,Time))
            {
                a.Hit.Clear();Vector3 origin=p.Body.position;
                foreach(var t in MeleeTargets(p).ToArray())
                {
                    if(p.Special!=a||p.Hp<=0)break;
                    if(!NativeWhirlwindRules.Contact(Vec(t.at-origin),(float)p.Stats["reach"])||!MeleeVisible(origin,t.at,p,t.collider)||!a.Hit.Add(t.id.ToString()))continue;
                    Vector3 direction=t.at-origin;direction.y=0;
                    if(direction.sqrMagnitude<.0001f)direction=Vec(V3.Forward(p.Yaw));else direction.Normalize();
                    float actual=MeleeAbilityHit(p,t.id,NativeWhirlwindRules.Damage,direction,0,"whirlwind",t.collider.ClosestPoint(origin));
                    if(actual>0&&a.Tick==1&&t.pilot!=null)StaggerMelee(t.pilot,NativeWhirlwindRules.Stagger);
                }
                if(p.Special!=a||p.Hp<=0)break;
                foreach(var prop in Props.ToArray())
                {
                    if(prop==null||prop.Indestructible)continue;var collider=prop.GetComponent<Collider>();var point=collider.ClosestPoint(origin);
                    if(!NativeWhirlwindRules.Contact(Vec(point-origin),(float)p.Stats["reach"])||!MeleeVisible(origin,point,p,collider))continue;
                    DamageProp(prop,NativeWhirlwindRules.Damage*(float)(p.Stats["damage"]*p.Stats["demolition"]),Vector3.zero,point,p,"melee");
                }
            }
        }
        private bool MeleeTeleport(NativePilot p,Vector3 destination,string fx)
        {
            Vector3 origin=p.Body.position,delta=destination-origin;float distance=delta.magnitude;if(distance<.01f)return false;
            var scene=root.scene.GetPhysicsScene();
            if(scene.CapsuleCast(origin+Vector3.up*.43f,origin-Vector3.up*.43f,.42f,delta.normalized,out var wall,distance,WorldMask,QueryTriggerInteraction.Ignore))distance=Math.Max(0,wall.distance-.06f);
            Vector3 to=ClampMap(origin+delta.normalized*distance);
            if(!RoomFor(p,to)||Vector3.Distance(to,origin)<.05f)return false;
            p.Body.position=to;p.Body.linearVelocity=p.MoveVelocity=p.Knockback=Vector3.zero;Physics.SyncTransforms();ProbePilotGround(p);
            Emit(EventKind.Teleport,p,0,to,0,fx,from:origin);return true;
        }
        private void MeleeRecastStrike(NativePilot p,NativeMeleeAction a,bool sky)
        {
            var t=MeleeTarget(p,a.Target);if(t.collider==null)return;
            Vector3 away=sky?(p.Body.position-t.at).normalized:-Vec(V3.Forward(t.pilot?.Yaw??t.minion.Yaw));
            away.y=0;if(away.sqrMagnitude<.01f)away=Vector3.back;
            Vector3 destination=t.at+away.normalized*1.15f;
            if(sky)destination.y=Math.Max(destination.y,p.Body.position.y);
            if(Vector3.Distance(p.Body.position,destination)>.05f&&!MeleeTeleport(p,destination,a.Fx)||Vector3.Distance(p.Body.position,t.at)>2.5f||!MeleeVisible(p.Body.position,t.at,p,t.collider))return;
            Vector3 direction=(t.at-p.Body.position).normalized;a.Yaw=Mathf.Atan2(direction.x,direction.z);p.Yaw=a.Yaw*Mathf.Rad2Deg;
            float actual=MeleeAbilityHit(p,t.id,sky?22:28,direction,sky?250:95,a.Fx,t.collider.ClosestPoint(p.Body.position));
            if(actual<=0||p.Special!=a)return;
            if(sky)
            {
                // Existing knockdown handles real ground contact; shockwave waits for that contact.
                p.Knockback.y=-18-p.Body.linearVelocity.y;p.Grounded=false;
                if(t.pilot!=null&&t.pilot.Hp>0){t.pilot.Knockback.y=-18-t.pilot.Body.linearVelocity.y;t.pilot.Down=new NativeRecovery{At=Time,Until=Time+5,PickupUsed=true,Source=p.Seat.id};}
                if(t.minion!=null&&t.minion.Hp>0&&!t.minion.Body.isKinematic)t.minion.Body.linearVelocity=new Vector3(t.minion.Body.linearVelocity.x,-18,t.minion.Body.linearVelocity.z);
                a.Phase="fall";a.Until=Time+3;p.AttackEnd=p.Ready=a.Until;
            }
            else if(!sky&&t.pilot!=null)StaggerMelee(t.pilot,.4f);
        }
        private float MeleeMinionHit(NativeMinion m,NativePilot p,float damage,Vector3 direction,float impulse,Vector3? point=null,string kind="melee",bool empower=true,int projectile=0)
        {
            bool boosted=empower&&p.Seat.spec=="daggers"&&Time<p.MeleeKit.EmpowerUntil;
            float actual=HitMinion(m,p,damage*(boosted?1.4f:1),direction,impulse,point,kind,projectile:projectile,from:p.Body.position);
            if(boosted&&actual>0)p.MeleeKit.EmpowerUntil=0;return actual;
        }
        private float MeleeAbilityHit(NativePilot p,int id,float damage,Vector3 direction,float impulse,string fx,Vector3 point,bool wallSlam=false)
        {
            var t=MeleeTarget(p,id);if(t.collider==null)return 0;
            float scale=(float)p.Stats["damage"];
            float actual=t.pilot!=null?Damage(t.pilot,p,damage*scale,direction,impulse*(float)p.Stats["impulse"],false,melee:true,weaponHit:true,hitPoint:point,damageKind:"ability"):MeleeMinionHit(t.minion,p,damage*scale,direction,impulse*(float)p.Stats["impulse"],point,"melee");
            if(actual>0){Emit(EventKind.AbilityHit,p,id,point,actual,fx,normal:-direction,wallSlam:wallSlam);ImpactChips(p,t.pilot,null,point,direction,nextAttack++,nextAttack,t.minion);if(t.minion!=null)MinionWeaponHit(t.minion,p,actual,(float)p.Stats["burn"],(float)p.Stats["poison"]);}
            return actual;
        }
        // Shared sword/Kernel opener sweep: horizontal incremental travel with a small wall skin.
        private float ForwardAbilityStep(Vector3 origin,Vector3 direction,float distance,out bool wall)
        {
            wall=root.scene.GetPhysicsScene().CapsuleCast(origin+Vector3.up*.43f,origin-Vector3.up*.43f,.42f,direction,out var hit,distance+.03f,WorldMask,QueryTriggerInteraction.Ignore);
            return wall?Math.Max(0,Math.Min(distance,hit.distance-.03f)):distance;
        }
        private Vector3 MeleeAbilityMotion(NativePilot p)
        {
            var a=p.Special;if(a==null||!a.Released||a.Phase!="active"||(a.Fx!="flash-step"&&a.Fx!="bulwark-charge"))return Vector3.zero;
            const float dt=1f/60;bool charge=a.Fx=="bulwark-charge";float speed=charge?8/.45f:7/.22f;
            Vector3 direction=Vec(a.Direction);float distance=ForwardAbilityStep(p.Body.position,direction,Math.Min(speed*dt,(charge?8:7)-a.Distance),out bool wall);
            var t=MeleeTargets(p).Where(t=>Vector3.Dot(t.at-p.Body.position,direction)>=0&&Math.Abs(t.at.y-p.Body.position.y)<1.5f&&Vector3.Distance(t.at,p.Body.position+direction*Mathf.Clamp(Vector3.Dot(t.at-p.Body.position,direction),0,distance))<.95f&&MeleeVisible(p.Body.position,t.at,p,t.collider)).OrderBy(t=>(t.at-p.Body.position).sqrMagnitude).FirstOrDefault();
            if(t.collider!=null)
            {
                float actual=MeleeAbilityHit(p,t.id,charge?20:26,direction,0,a.Fx,t.collider.ClosestPoint(p.Body.position));
                if(actual>0)
                {
                    if(!charge)p.QReady=Math.Max(Time,p.QReady-NativeAbilityRules.Cooldown(Content.Ability(p.Seat.spec,"q"),p.Stats)*.5f);
                    if(t.pilot!=null&&t.pilot.Hp>0)
                    {
                        bool stagger=StaggerMelee(t.pilot,charge?.3f:.35f);
                        if(charge&&stagger&&!SirenArmor(t.pilot))meleeCarries.Add(new MeleeCarry{Owner=p,Target=t.pilot,Direction=direction});
                    }
                }
                if(p.Special==a){a.Phase="recovery";a.Until=Time+.2f;p.AttackEnd=p.Ready=a.Until;}return Vector3.zero;
            }
            a.Distance+=distance;
            if(wall||a.Distance>=(charge?8:7)-.01f){a.Phase="recovery";a.Until=Time+.2f;p.AttackEnd=p.Ready=a.Until;}
            return direction*(distance/dt);
        }
        private bool AbsorbCharge(NativePilot p,Vector3 velocity,Vector3 point,int projectile)
        {
            var a=p.Special;if(a?.Fx!="bulwark-charge"||!a.Released||a.Phase!="active"||Time>=a.Until||Vector3.Dot(-velocity.normalized,Vec(a.Direction))<.5f)return false;
            p.MeleeKit.Absorbed++;Emit(EventKind.Absorb,p,projectile,point,p.MeleeKit.Absorbed,a.Fx,normal:-velocity.normalized);
            if(p.MeleeKit.Absorbed>=3){a.Phase="recovery";a.Until=Time+.2f;p.AttackEnd=p.Ready=a.Until;}return true;
        }
        private bool HitMeleeKitShot(NativeMeleeShot s,MeleeContact hit)
        {
            var target=hit.Collider.GetComponent<NativePilotBody>()?.Pilot;
            var minion=hit.Collider.GetComponent<NativeMinionBody>()?.Minion;
            s.Position=hit.Point;
            if(target!=null&&AbsorbCharge(target,s.Velocity,hit.Point,s.Id))return true;
            if(target!=null&&Time<target.BlockEnd)
            {
                var attacker=s.Owner;BlockProc(target,attacker,hit.Point,"deflect");
                SuccessfulMeleeBlock(target,attacker,s.Damage,s.Velocity.normalized,s.Impulse);
                if(s.Shield){s.Returning=true;return false;}
                if(s.Reflections>=3)return true;
                s.Reflections++;s.Reflected=true;s.Owner=target;s.Damage=Math.Min(100,s.Damage*(float)target.Stats["reflectDamage"]);
                s.Velocity=-s.Velocity.normalized*Math.Min(120,s.Velocity.magnitude*(float)target.Stats["reflectSpeed"]);s.Position+=s.Velocity.normalized*.025f;return false;
            }
            int id=target?.Seat.id??minion?.Id??0;
            if(id!=0)
            {
                s.Chain.Add(id);
                float actual=target!=null?Damage(target,s.Owner,s.Damage,s.Velocity.normalized,s.Impulse,false,primary:!s.Reflected,melee:true,weaponHit:!s.Reflected,hitPoint:hit.Point,damageKind:"ability"):MeleeMinionHit(minion,s.Owner,s.Damage,s.Velocity.normalized,s.Impulse,hit.Point,"melee",empower:!s.Reflected);
                if(actual>0)
                {
                    Emit(EventKind.AbilityHit,s.Owner,id,hit.Point,actual,s.Fx,normal:hit.Normal);
                    if(!s.Reflected)ImpactChips(s.Owner,target,null,hit.Point,s.Velocity.normalized,s.Id,s.Id,minion);
                    if(minion!=null)MinionWeaponHit(minion,s.Owner,actual,s.Burn,s.Poison);
                    if(s.Shield&&s.Chain.Count==1&&target?.Hp>0)LaunchMelee(target,s.Owner,3.8f);
                    if(!s.Shield&&!s.Reflected&&s.Original.Hp>0)
                    {
                        var k=s.Original.MeleeKit;ClearMeleeMark(s.Original);k.Target=k.MarkTarget=id;k.MarkUntil=k.RecastUntil=Time+4;k.RecastKind="kunai-mark";
                        Emit(EventKind.Mark,s.Original,id,hit.Point,1,s.Fx);
                    }
                }
                if(s.Shield&&s.Chain.Count<3&&s.Original.Hp>0)
                {
                    var next=MeleeTargets(s.Owner).Where(t=>!s.Chain.Contains(t.id)&&Vector3.Distance(t.at,hit.Point)<=7&&MeleeVisible(hit.Point,t.at,s.Owner,t.collider)).OrderBy(t=>(t.at-hit.Point).sqrMagnitude).ThenBy(t=>t.id).FirstOrDefault();
                    if(next.collider!=null)
                    {
                        s.Velocity=(next.at-hit.Point).normalized*24*(float)s.Owner.Stats["projectileSpeed"];s.Travel=7;s.Life=3;
                        Emit(EventKind.Bounce,s.Owner,next.id,hit.Point,s.Chain.Count,s.Fx,from:hit.Point,normal:s.Velocity.normalized);return false;
                    }
                }
            }
            else DamageProp(hit.Collider.GetComponent<NativePropBody>(),s.Damage,s.Velocity.normalized*s.Impulse,hit.Point,s.Owner,"melee");
            if(s.Shield){s.Returning=true;return false;}return true;
        }
        private void MeleeShotFinished(NativeMeleeShot shot)
        {
            if(shot.Fx=="kunai-mark"&&shot.Original.MeleeKit.MarkTarget==0)
                shot.Original.EReady=Math.Min(shot.Original.EReady,shot.At+4*(float)Math.Max(.45,Math.Min(3,shot.Original.Stats["abilityCooldown"])));
        }
        private void StepMeleeKitWorld()
        {
            foreach(var c in meleeCarries.ToArray())
            {
                if(c.Target.Hp<=0||!CanHit(c.Owner,c.Target)){meleeCarries.Remove(c);continue;}
                float step=Math.Min(c.Left,20f/60);var pos=c.Target.Body.position;var scene=root.scene.GetPhysicsScene();
                bool wall=scene.CapsuleCast(pos+Vector3.up*.43f,pos-Vector3.up*.43f,.42f,c.Direction,out var hit,step+.03f,WorldMask,QueryTriggerInteraction.Ignore);
                if(wall)step=Math.Max(0,hit.distance-.03f);
                c.Target.Body.position=ClampMap(pos+c.Direction*step);c.Left-=step;
                if(wall){float actual=MeleeAbilityHit(c.Owner,c.Target.Seat.id,10,c.Direction,0,"bulwark-charge",hit.point,wallSlam:true);if(actual>0&&c.Target.Hp>0&&!SirenArmor(c.Target)){StunMelee(c.Target,.6f);c.Target.StaggerImmune=NativeMeleeRules.StaggerImmuneThrough(c.Target.StunEnd);}}
                if(wall||c.Left<=.001f)meleeCarries.Remove(c);
            }
            if(meleeCarries.Count>0)Physics.SyncTransforms();
            foreach(var f in meleeFissures.ToArray())
            {
                if(Time>=f.Until){meleeFissures.Remove(f);continue;}
                float end=Math.Min(f.Length,Math.Max(0,Time-f.At)*25);Vector3 front=f.Start+f.Direction*end;
                foreach(var t in MeleeTargets(f.Owner).ToArray())
                {
                    Vector3 d=t.at-f.Start;float along=Vector3.Dot(d,f.Direction);Vector3 near=f.Start+f.Direction*Mathf.Clamp(along,f.Travel,end);
                    if(f.Stopped||along<f.Travel-.45f||along>end+.45f||Vector3.Distance(t.at,near)>1.25f||!MeleeVisible(near,t.at,f.Owner,t.collider)||!f.Action.Hit.Add(t.id.ToString()))continue;
                    float actual=MeleeAbilityHit(f.Owner,t.id,30,f.Direction,0,"earthsplitter",t.collider.ClosestPoint(front));
                    if(actual>0&&t.pilot!=null&&StaggerMelee(t.pilot,.5f)){t.pilot.Knockback.y=5.75f-t.pilot.Body.linearVelocity.y;t.pilot.Grounded=false;}
                    if(Time<f.Owner.StunEnd){f.Stopped=true;break;}
                }
                f.Travel=end;
                foreach(var p in Pilots)
                {
                    Vector3 d=p.Body.position-f.Start;float along=Vector3.Dot(d,f.Direction);Vector3 side=d-f.Direction*along;
                    if(p.Hp>0&&along>=0&&along<=end&&Math.Abs(side.y)<1.3f&&new Vector2(side.x,side.z).magnitude<.8f)Slow(p,.7f,.06f);
                }
            }
            foreach(var p in Pilots)
            {
                if(p.MeleeKit.MarkTarget!=0&&(p.Hp<=0||Time>=p.MeleeKit.MarkUntil||MeleeTarget(p,p.MeleeKit.MarkTarget).collider==null))ClearMeleeMark(p);
                var a=p.Special;if(a?.Kind!="skyfall"||a.Phase!="fall")continue;
                var target=Pilots.Find(t=>t.Seat.id==a.Target);
                if(Time>a.Release&&(target?.Hp>0&&(target.Down?.Landed>=0||target.Grounded)||p.Grounded))
                {
                    Vector3 at=target?.Hp>0?target.Body.position:Minions.Find(t=>t.Id==a.Target&&t.Hp>0)?.Body.position??p.Body.position;
                    if(target?.Hp>0&&target.Down!=null){target.Knockback.y=4-target.Body.linearVelocity.y;target.Grounded=false;target.Down.Landed=-1;target.Down.At=Time;}
                    a.Phase="recovery";a.Landed=Time;a.Until=Time+.25f;p.AttackEnd=p.Ready=a.Until;
                    foreach(var t in MeleeTargets(p).ToArray()){if(p.Special!=a)break;if(t.id!=a.Target&&Vector3.Distance(t.at,at)<=2&&MeleeVisible(at,t.at,p,t.collider))MeleeAbilityHit(p,t.id,10,(t.at-at).normalized,180,a.Fx,t.collider.ClosestPoint(at));}
                    Emit(EventKind.Land,p,a.Target,at,0,a.Fx,2);
                }
            }
        }
    }
}
