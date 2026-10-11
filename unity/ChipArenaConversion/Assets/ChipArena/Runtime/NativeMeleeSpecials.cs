using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChipArena.UnityConversion
{
    public sealed class NativeMeleeShot
    {
        public int Id,Reflections;public NativePilot Original,Owner;public string Spec;public bool Shield,Returning,Reflected;
        public Vector3 Position,Velocity;public float At,Damage,Impulse,Radius,Gravity,Life,Travel,Burn,Poison;
    }
    public sealed class NativeMeleeImpact {public Vector3 Position;public float At,Radius;}
    public sealed partial class NativeArena
    {
        public readonly List<NativeMeleeShot> MeleeShots=new List<NativeMeleeShot>();
        public readonly List<NativeMeleeImpact> MeleeImpacts=new List<NativeMeleeImpact>();
        public bool Downed(NativePilot p)=>p.Down!=null&&Time<p.Down.Until;
        public bool Incapacitated(NativePilot p)=>Time<p.StunEnd||Downed(p)||p.Juggle!=null;
        private bool SpecialBusy(NativePilot p)=>p.Special!=null&&Time<p.Special.Until;
        private int ProjectileCount=>Bullets.Count+AbilityShots.Count+MeleeShots.Count;
        private void CancelSpecial(NativePilot p){if(p.Special!=null){p.Special=null;p.AttackEnd=0;}}
        private bool TrySpecial(NativePilot p)
        {
            float cost=(float)(p.Weapon.secondaryCost*p.Stats["resourceCost"]);
            if(p.Hp<=0||Incapacitated(p)||Time<p.Ready||Time<p.EReady||p.ReloadEnd>Time||p.CastAt>0||p.AbilityCast!=null||Time<p.BlockEnd&&!RiposteOpen(p))return false;
            if(!NativeSpecialRules.CanBegin(p.Seat.spec,p.Grounded,SpecialBusy(p),Time<p.AttackEnd,Time<p.RollEnd,ProjectileCount,p.Stamina,cost))return false;
            p.Stamina-=cost;CancelMelee(p);p.Special=NativeSpecialRules.Begin(p.Seat.spec,(float)p.Stats["attackRate"],Time,p.Input.Yaw*Mathf.Deg2Rad);
            p.Yaw=p.Input.Yaw;p.AttackEnd=p.Special.Until;p.Ready=Time+(NativeSpecialRules.Windup(p.Seat.spec)+NativeSpecialRules.Recovery(p.Seat.spec))/(float)p.Stats["attackRate"];
            p.EReady=Time+(float)p.Weapon.secondaryCooldown;p.LastAttack=Time;p.AttackAt=-100;p.RegenAt=p.Ready+.45f;p.MoveVelocity=Vector3.zero;p.BlockEnd=0;return true;
        }
        private void StepRecovery(NativePilot p)
        {
            var k=p.Down;if(k==null)return;if(p.Hp<=0||Time>=k.Until){p.Down=null;return;}
            p.StaggerImmune=Math.Max(p.StaggerImmune,Time+.75f);
            if(k.Landed<0&&Time-k.At>.18f&&p.Grounded&&p.Body.linearVelocity.y<.5f)NativeSpecialRules.Land(k,Time);
            NativeSpecialRules.RecoveryMotion(k,Time,new V3(p.Input.X,0,p.Input.Z),p.Input.Yaw);
        }
        private bool LaunchMelee(NativePilot target,NativePilot owner,float lift,bool pickup=false)
        {
            if(SirenArmor(target))return false;bool used=target.Down!=null&&(target.Down.PickupUsed||NativeSpecialRules.Pickup(target.Down,Time));
            if(!pickup&&!StaggerMelee(target,Math.Max(.24f,.16f+(float)owner.Weapon.weight*.018f)))return false;
            StunMelee(target,.24f);target.Down=new NativeRecovery{At=Time,Until=Time+5,PickupUsed=used,Source=owner.Seat.id,JuggleUntil=Time+.9f};
            target.Knockback.y=Math.Max(0,lift-target.Body.linearVelocity.y);target.Grounded=false;return true;
        }
        private void PrimaryMeleeImpact(NativePilot owner,NativePilot target)
        {
            if(NativeSpecialRules.Pickup(target.Down,Time))LaunchMelee(target,owner,4.8f,true);
            else if(target.Down!=null&&target.Down.Landed<0&&Time<target.Down.JuggleUntil&&target.Down.JuggleHits<2)
            {target.Down.JuggleHits++;target.Knockback.y=Math.Max(0,2.5f-target.Body.linearVelocity.y);}
            else if(owner.Seat.spec=="shield")LaunchMelee(target,owner,3.8f);
            else StaggerMelee(target,NativeMeleeRules.HitStun(owner.Weapon,(float)owner.Stats["attackRate"]));
        }
        private void StepSpecial(NativePilot p)
        {
            var a=p.Special;if(a==null)return;
            if(p.Hp<=0||!p.Seat.connected||Incapacitated(p)||Time<p.BlockEnd){CancelSpecial(p);return;}
            if(a.Kind=="shield-throw"&&a.Shot!=0&&MeleeShots.Any(s=>s.Id==a.Shot))a.Until=Math.Max(a.Until,Time+.1f);
            if(Time>=a.Until){CancelSpecial(p);return;}
            if(!a.Released&&Time>=a.Release)
            {
                a.Released=true;
                if(a.Kind=="dagger-throw"||a.Kind=="shield-throw")ThrowMelee(p,a);
                else if(a.Kind=="sword-launch")SpecialStrike(p,a,(float)(p.Weapon.range*p.Stats["reach"]),240,"launch");
                else {p.Knockback=Quaternion.Euler(0,a.Yaw*Mathf.Rad2Deg,0)*Vector3.forward*7+Vector3.up*(10-p.Body.linearVelocity.y);p.Grounded=false;}
            }
            if(p.Special!=a||p.Hp<=0)return;
            if(a.Kind=="wolf-spin"&&a.Released&&a.Landed<0&&Time>a.Release+.04f)SpecialStrike(p,a,2.15f*(float)p.Stats["reach"],760,"spin-direct");
            if(p.Special!=a||p.Hp<=0)return;
            if(NativeSpecialRules.CanLand(a,Time,p.Grounded,p.Body.linearVelocity.y))
            {
                a.Landed=Time;a.Until=Time+.55f/(float)p.Stats["attackRate"];p.AttackEnd=a.Until;p.RegenAt=a.Until+.45f;p.Ready=Math.Max(p.Ready,a.Until);p.Knockback.x*=.25f;p.Knockback.z*=.25f;
                float radius=3.7f*(float)p.Stats["reach"];SpecialStrike(p,a,radius,760,"slam");
                MeleeImpacts.Add(new NativeMeleeImpact{At=Time,Position=p.Body.position-Vector3.up*.75f,Radius=radius});
            }
        }
        private void SpecialStrike(NativePilot p,NativeMeleeAction action,float range,float impulse,string kind)
        {
            Vector3 origin=p.Body.position+Vector3.up*.1f;
            foreach(var target in Pilots)
            {
                string id="pilot:"+target.Seat.id;Vector3 delta=target.Body.position-origin;
                if(target.Hp<=0||!CanHit(p,target)||NativeSpecialRules.Protected(target.Down,Time)||action.Hit.Contains(id)||!NativeSpecialRules.Strike(Vec(delta),range,.45f,action.Yaw,kind)||!MeleeVisible(origin,target.Body.position,p,target.Collider))continue;
                action.Hit.Add(id);float actual=Damage(target,p,(float)(p.Weapon.damage*p.Stats["damage"])*1.12f,(delta+Vector3.up*(kind=="slam"?.2f:.5f)).normalized,impulse*(float)p.Stats["impulse"],false,melee:true,weaponHit:true);
                if(actual>0)ImpactChips(p,target,null,target.Body.position,delta.normalized,(int)(action.At*60)*1000+p.Seat.id,nextAttack++);
                if(actual>0&&target.Hp>0)
                {
                    if(kind=="launch")LaunchMelee(target,p,7.4f);
                    else if(LaunchMelee(target,p,kind=="spin-direct"?6.1f:4.4f)){target.Down.Ragdoll=kind=="spin-direct";target.Knockback+=new Vector3(delta.x,0,delta.z).normalized*3;}
                }
                if(p.Hp<=0||p.Special!=action)return;
            }
            foreach(var m in Minions.ToArray())
            {
                string key="minion:"+m.Id;Vector3 d=m.Body.position-origin;
                if(m.Hp<=0||!CanHit(p,m)||action.Hit.Contains(key)||!NativeSpecialRules.Strike(Vec(d),range,.35f,action.Yaw,kind)||!MeleeVisible(origin,m.Body.position,p,m.Collider))continue;
                action.Hit.Add(key);Vector3 at=m.Body.position;float actual=HitMinion(m,p,(float)(p.Weapon.damage*p.Stats["damage"])*1.12f,(d+Vector3.up*.5f).normalized,impulse*(float)p.Stats["impulse"],at,"melee");
                MinionWeaponHit(m,p,actual,(float)p.Stats["burn"],(float)p.Stats["poison"]);if(actual>0)ImpactChips(p,null,null,at,d.normalized,(int)(action.At*60)*1000+p.Seat.id,nextAttack++);
            }
            foreach(var prop in Props.ToArray())
            {
                if(prop==null||prop.Indestructible)continue;string id="prop:"+prop.Definition.id;Vector3 delta=prop.transform.position-origin;
                if(action.Hit.Contains(id)||!NativeSpecialRules.Strike(Vec(delta),range,Math.Min(.8f,prop.Definition.size.Length*.3f),action.Yaw,kind)||!MeleeVisible(origin,prop.transform.position,p,prop.GetComponent<Collider>()))continue;
                action.Hit.Add(id);DamageProp(prop,(float)(p.Weapon.damage*p.Stats["damage"])*1.12f,(delta+Vector3.up*(kind=="slam"?.2f:.5f)).normalized*impulse*(float)p.Stats["impulse"],prop.transform.position,p);
            }
        }
        private void ThrowMelee(NativePilot p,NativeMeleeAction action)
        {
            if(ProjectileCount>=100)return;bool shield=action.Kind=="shield-throw";Vector3 start=p.Body.position+Vector3.up*.3f;
            Vector3 direction=p.Input.AbilityTarget.Length>.01f?(Vec(p.Input.AbilityTarget)-start).normalized:Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch));
            var shot=new NativeMeleeShot{Id=nextBullet++,Original=p,Owner=p,Spec=p.Seat.spec,Shield=shield,At=Time,Position=start,Velocity=direction*(shield?24:30)*(float)p.Stats["projectileSpeed"],Damage=(float)(p.Weapon.damage*p.Stats["damage"]),Impulse=(shield?280:95)*(float)p.Stats["impulse"],Radius=shield?.28f:.095f,Life=shield?3:2,Travel=shield?19:32,Gravity=shield?0:18*(float)p.Stats["projectileGravity"],Burn=(float)p.Stats["burn"],Poison=(float)p.Stats["poison"]};
            MeleeShots.Add(shot);action.Shot=shot.Id;p.LastShot=Time;
        }
        private Collider[] meleeOverlaps=new Collider[16];private RaycastHit[] meleeSweeps=new RaycastHit[32];
        private struct MeleeContact {public Collider Collider;public Vector3 Point,Normal;public float Distance;}
        private bool ShotCollider(NativeMeleeShot s,Collider collider)
        {var m=collider.GetComponent<NativeMinionBody>();if(m!=null)return m.Minion.Hp>0&&CanHit(s.Owner,m.Minion);var p=collider.GetComponent<NativePilotBody>();return p==null||p.Pilot.Hp>0&&CanHit(s.Owner,p.Pilot)&&!Invulnerable(p.Pilot);}
        private MeleeContact? SweepMeleeShot(NativeMeleeShot s,Vector3 delta)
        {
            var physics=root.scene.GetPhysicsScene();int n;
            // SphereCast alone misses starting overlaps. Resolve those before sweeping the flight chord.
            while((n=physics.OverlapSphere(s.Position,s.Radius,meleeOverlaps,ShotMask,QueryTriggerInteraction.Ignore))==meleeOverlaps.Length)Array.Resize(ref meleeOverlaps,meleeOverlaps.Length*2);
            // If a shot starts inside cover and a target, cover wins regardless of query order.
            Collider overlap=null;
            for(int i=0;i<n;i++)if(ShotCollider(s,meleeOverlaps[i]))
            {
                var candidate=meleeOverlaps[i];bool wall=candidate.GetComponent<NativePilotBody>()==null&&candidate.GetComponent<NativeMinionBody>()==null;
                if(overlap==null||wall&&(overlap.GetComponent<NativePilotBody>()!=null||overlap.GetComponent<NativeMinionBody>()!=null)||wall==(overlap.GetComponent<NativePilotBody>()==null&&overlap.GetComponent<NativeMinionBody>()==null)&&candidate.GetInstanceID()<overlap.GetInstanceID())overlap=candidate;
            }
            if(overlap!=null)return new MeleeContact{Collider=overlap,Point=s.Position,Normal=-s.Velocity.normalized,Distance=0};
            float length=delta.magnitude;if(length<=.000001f)return null;
            while((n=physics.SphereCast(s.Position,s.Radius,delta.normalized,meleeSweeps,length,ShotMask,QueryTriggerInteraction.Ignore))==meleeSweeps.Length)Array.Resize(ref meleeSweeps,meleeSweeps.Length*2);
            MeleeContact? closest=null;
            for(int i=0;i<n;i++){var hit=meleeSweeps[i];if(!ShotCollider(s,hit.collider)||closest.HasValue&&hit.distance>=closest.Value.Distance)continue;closest=new MeleeContact{Collider=hit.collider,Point=s.Position+delta.normalized*hit.distance,Normal=hit.normal,Distance=hit.distance};}
            return closest;
        }
        private bool HitMeleeShot(NativeMeleeShot s,MeleeContact hit)
        {
            var target=hit.Collider.GetComponent<NativePilotBody>()?.Pilot;s.Position=hit.Point;
            if(target!=null&&Time<target.BlockEnd)
            {
                BlockProc(target,s.Owner,hit.Point);
                if(s.Shield){s.Returning=true;return false;}
                if(s.Reflections>=3)return true;
                s.Owner=target;s.Reflections++;s.Reflected=true;s.Damage=Math.Min(100,s.Damage*(float)target.Stats["reflectDamage"]);
                s.Velocity=-s.Velocity.normalized*Math.Min(120,s.Velocity.magnitude*(float)target.Stats["reflectSpeed"]);s.Position+=s.Velocity.normalized*.025f;return false;
            }
            var minion=hit.Collider.GetComponent<NativeMinionBody>()?.Minion;
            if(minion!=null){float actual=HitMinion(minion,s.Owner,s.Damage,s.Velocity.normalized,s.Impulse,hit.Point,"melee");MinionWeaponHit(minion,s.Owner,actual,s.Burn,s.Poison);if(actual>0&&!s.Reflected)ImpactChips(s.Owner,null,null,hit.Point,s.Velocity.normalized,s.Id,s.Id);}
            else if(target!=null)
            {
                bool head=!s.Shield&&NativeCombatRules.Headshot(Vec(target.Body.position),target.Yaw,Vec(hit.Point-s.Velocity.normalized*.1f),Vec(s.Velocity),s.Radius,target.Down!=null||target.Juggle!=null);
                float actual=Damage(target,s.Owner,s.Damage*(head?1.5f:1),s.Velocity.normalized,s.Impulse,head,primary:!s.Reflected,melee:true,weaponHit:!s.Reflected,hitPoint:hit.Point);
                if(actual>0&&!s.Reflected)ImpactChips(s.Owner,target,null,hit.Point,s.Velocity.normalized,s.Id,s.Id);
                if(actual>0&&s.Reflected){RefreshDot(target,s.Owner,"fire",s.Burn,2);RefreshDot(target,s.Owner,"poison",s.Poison,4);}
                if(actual>0&&target.Hp>0){if(s.Shield)LaunchMelee(target,s.Owner,3.8f);else StaggerMelee(target,.16f+(float)s.Owner.Weapon.weight*.018f);}
            }
            else
            {
                var prop=hit.Collider.GetComponent<NativePropBody>();DamageProp(prop,s.Damage,s.Velocity.normalized*s.Impulse,hit.Point,s.Owner);
                if(prop!=null)Events.Add(new ArenaEvent{Id=nextEvent++,Kind=EventKind.Impact,Source=s.Owner.Seat.id,Time=Time,Position=Vec(hit.Point),Normal=Vec(hit.Normal),SurfaceId=prop.Definition.id,Color=(int)s.Original.Weapon.color,Projectile=s.Id});
            }
            if(s.Shield){s.Returning=true;return false;}return true;
        }
        private void StepMeleeShots()
        {
            const float dt=1f/60;MeleeImpacts.RemoveAll(f=>Time-f.At>1.1f);
            for(int i=MeleeShots.Count-1;i>=0;i--)
            {
                var s=MeleeShots[i];bool remove=false;
                if(s.Shield&&(s.Original.Hp<=0||!s.Original.Seat.connected)){MeleeShots.RemoveAt(i);continue;}
                if(s.Shield&&(s.Life<=dt||s.Travel<=0))s.Returning=true;
                if(s.Returning)
                {
                    Vector3 delta=s.Original.Body.position+Vector3.up*.25f-s.Position;float distance=delta.magnitude;
                    if(distance<40*dt+.4f){remove=true;if(s.Original.Special?.Shot==s.Id)s.Original.Special.Until=Time+.15f;}
                    else {s.Velocity=delta.normalized*40;s.Position+=s.Velocity*dt;}
                }
                else if(s.Life<=0||s.Travel<=0)remove=true;
                else
                {
                    float h=Math.Min(dt,s.Life);var velocity=Vec(s.Velocity);Vector3 delta=Vec(NativeSpecialRules.Ballistic(ref velocity,s.Gravity,h));s.Velocity=Vec(velocity);s.Life-=h;
                    delta=Vector3.ClampMagnitude(delta,s.Travel);s.Travel-=delta.magnitude;var hit=SweepMeleeShot(s,delta);
                    bool ceiling=MapCeiling(Vec(s.Position),Vec(delta),s.Radius,out float fraction,out V3 center);
                    if(ceiling&&(!hit.HasValue||delta.magnitude*fraction<=hit.Value.Distance)){s.Position=Vec(center);Emit(EventKind.CeilingHit,s.Owner,0,s.Position,0,"ABSORB",color:0x75ddff);if(s.Shield)s.Returning=true;else remove=true;}
                    else if(hit.HasValue)remove=HitMeleeShot(s,hit.Value);else s.Position+=delta;
                }
                if(Structures!=null&&ShotOutside(s.Position)){if(s.Shield)s.Returning=true;else remove=true;}
                if(remove)MeleeShots.RemoveAt(i);
            }
        }
    }
}
