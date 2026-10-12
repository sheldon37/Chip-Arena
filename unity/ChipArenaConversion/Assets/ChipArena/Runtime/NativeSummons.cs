using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    public sealed class NativeMinionBody:MonoBehaviour { public NativeMinion Minion; }
    public sealed class NativeMinion
    {
        public float LastHitAt=-100;public Vector3 NetworkVelocity;public NativePveAdd Pve;public int Id;public NativePilot Owner,Foe;public NativeMinion Other;public string Kind;
        public float SlowUntil,SlowFactor=1,SoakAt=-10;public Rigidbody Body;public CapsuleCollider Collider;public float Hp,MaxHp,Speed,Until,Yaw,LastAttack=-10,NextAttack,StrikeAt=-1;
        public readonly Dictionary<string,NativeDot> Dots=new Dictionary<string,NativeDot>();
    }
    public sealed partial class NativeArena
    {
        public readonly List<NativeMinion> Minions=new List<NativeMinion>();private int nextMinion=10000;
        private void Summon(NativePilot owner,int count,string kind,Vector3 center,float life,bool replace=false)
        {
            if(replace)foreach(var old in Minions.Where(m=>m.Owner==owner&&m.Kind==kind).ToArray())RemoveMinion(old,false);
            for(int i=0;i<count;i++)
            {
                var own=Minions.Where(m=>m.Owner==owner&&m.Kind==kind).ToArray();if(own.Length>=(kind=="toy"?3:14))RemoveMinion(own[0],false);if(Minions.Count(m=>m.Pve==null&&m.Kind!="slender")>=34)break;
                Vector3 pos=center+Quaternion.Euler(0,owner.Input.Yaw+i*2.4f*Mathf.Rad2Deg,0)*Vector3.forward*(1.3f+i%3*.45f);pos.y=Math.Max(1,pos.y+.3f);
                var obj=new GameObject(kind);obj.layer=11;obj.transform.SetParent(root.transform);obj.transform.position=pos;
                var c=obj.AddComponent<CapsuleCollider>();c.radius=.28f;c.height=1.2f;c.sharedMaterial=surfaces["pilot"];
                var body=obj.AddComponent<Rigidbody>();body.mass=35;body.constraints=RigidbodyConstraints.FreezeRotation;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.interpolation=RigidbodyInterpolation.Interpolate;
                float hp=kind=="toy"?25:(float)owner.Stats["hp"]*2;
                // The Summon event is the source's `summon`, drawn as its green ground impact (NativeBlastPresentation.Impacts); the
                // first port's extra darkness ability effect here drew an implosion blast. Presentation feed only.
                var m=new NativeMinion{Id=nextMinion++,Owner=owner,Kind=kind,Body=body,Collider=c,Hp=hp,MaxHp=hp,Speed=6.8f*(float)owner.Stats["speed"]*(kind=="toy"?.7f:.5f),Until=Time+life,NextAttack=Time+.7f,Yaw=owner.Input.Yaw};obj.AddComponent<NativeMinionBody>().Minion=m;Minions.Add(m);Emit(EventKind.Summon,owner,m.Id,pos,hp,kind);
            }
            Physics.SyncTransforms();
        }
        private void RemoveMinion(NativeMinion m,bool explode=true)
        {
            if(!Minions.Remove(m))return;RecordMinionDeath(m);Vector3 pos=m.Body.position;m.Hp=0;m.Collider.enabled=false;CapturedAnimation.Release(m.Body.gameObject);
            if(explode&&m.Kind=="zombie"&&Has(m.Owner,"corpse-party")&&Match.Phase==NativePhase.Fight)ChipBlast(m.Owner,pos,3.5f,30,300,effect:"corpse-party");
        }
        private float HitMinion(NativeMinion m,NativePilot owner,float damage,Vector3 direction,float impulse,Vector3? point=null,string kind=null,float radius=0,bool head=false)
        {
            if(m.Hp<=0||!CanHit(owner,m))return 0;if(m.Pve!=null)return HitPveAdd(m,owner,damage,direction,impulse,point,kind,radius,head);float actual=Math.Max(0,Math.Min(m.Hp,damage));m.Hp-=actual;m.LastHitAt=Time;if(!m.Body.isKinematic)m.Body.AddForce(direction*impulse,ForceMode.Impulse);Emit(EventKind.Hit,owner,m.Id,m.Body.position,actual,kind=="energy"?"ability":kind=="projectile"||kind==null?"direct":kind=="dot"?"field":kind);MinionImpact(m,owner,actual,point,kind,direction);if(m.Hp<=0)RemoveMinion(m);return actual;
        }
        private void MinionImpact(NativeMinion m,NativePilot owner,float actual,Vector3? point,string kind,Vector3 direction)
        {
            if(actual<=0||owner==null||!(kind==null||kind=="direct"||kind=="projectile"||kind=="secondary"||kind=="reflected"||kind=="melee"||kind=="energy"))return;
            Emit(EventKind.Impact,owner,m.Id,point??m.Body.position,actual,kind,normal:-direction.normalized);
        }
        private void MinionWeaponHit(NativeMinion m,NativePilot owner,float actual,float burn,float poison)
        {
            if(actual<=0||owner==null)return;owner.DirectHits++;if(owner.Stats["staticEvery"]>0&&owner.DirectHits%(int)owner.Stats["staticEvery"]==0)ChipBlast(owner,m.Body.position,3,(float)owner.Stats["staticDamage"],60,false,effect:"static");if(owner.Seat.spec=="water"&&Time>=m.SoakAt+1.5f){m.SoakAt=Time;SlowMinion(m,.8f,.8f);}
            if(owner.Weapon.chain>0){var near=NearAbilityEnemies(owner,m.Body.position,4,2).FirstOrDefault(x=>x.m!=m);if(near.p!=null)Damage(near.p,owner,(float)owner.Weapon.chain,Vector3.zero,0,false,direct:false,primary:false);else if(near.m!=null)HitMinion(near.m,owner,(float)owner.Weapon.chain,Vector3.zero,0,kind:"chain");}
            Heal(owner,actual*(float)(owner.Weapon.drain+owner.Stats["lifeSteal"]));if(m.Hp<=0)return;
            foreach(var item in new[]{(kind:"fire",dps:burn,duration:2f),(kind:"poison",dps:poison,duration:4f)})
            {if(item.dps<=0)continue;string key=owner.Seat.id+":"+item.kind;m.Dots.TryGetValue(key,out var old);bool live=old!=null&&old.Until>=Time;m.Dots[key]=new NativeDot{Source=owner.Seat.id,Dps=Math.Max(item.dps,live?old.Dps:0),Next=live?old.Next:Time+.25,Until=Time+item.duration};}
        }
        private bool SummonVisible(NativeMinion from,Vector3 a,Vector3 b,Collider target)
        {
            Vector3 delta=b-a;int count;while((count=root.scene.GetPhysicsScene().Raycast(a,delta.normalized,meleeSightHits,delta.magnitude,ShotMask,QueryTriggerInteraction.Ignore))==meleeSightHits.Length)Array.Resize(ref meleeSightHits,meleeSightHits.Length*2);
            RaycastHit? hit=null;for(int i=0;i<count;i++)if(meleeSightHits[i].collider!=from.Collider&&(!hit.HasValue||meleeSightHits[i].distance<hit.Value.distance))hit=meleeSightHits[i];return !hit.HasValue||hit.Value.collider==target;
        }
        private void StepMinions()
        {
            foreach(var m in Minions.ToArray())
            {
                if(!Minions.Contains(m)||m.Pve!=null)continue;if(m.Owner.Hp<=0||Time>=m.Until||m.Body.position.y<-6){RemoveMinion(m);continue;}
                foreach(var pair in m.Dots.ToArray()){var d=pair.Value;while(m.Hp>0&&d.Next<=Time+1e-8&&d.Next<=d.Until+1e-8){HitMinion(m,Pilots.Find(p=>p.Seat.id==d.Source),(float)(d.Dps*.25),Vector3.zero,0,kind:"field");d.Next+=.25;}if(Time>d.Until)m.Dots.Remove(pair.Key);}if(m.Hp<=0||m.Kind=="slender")continue;
                bool toy=m.Kind=="toy";float windup=toy?.45f:.42f,recover=toy?.55f:.48f;Vector3 at=m.Body.position,velocity=m.Body.linearVelocity;
                if(m.StrikeAt>=0)
                {
                    Vector3? goal=m.Foe?.Hp>0?m.Foe.Body.position:m.Other?.Hp>0?m.Other.Body.position:(Vector3?)null;
                    if(goal.HasValue){Vector3 d=goal.Value-at;m.Yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;}
                    if(Time<m.StrikeAt){m.Body.linearVelocity=new Vector3(0,velocity.y,0);continue;}
                    m.StrikeAt=-1;m.NextAttack=Time+recover;if(!toy)m.Body.linearVelocity=Quaternion.Euler(0,m.Yaw,0)*Vector3.forward*4.5f+Vector3.up*velocity.y;
                    if(!goal.HasValue)continue;Vector3 origin=at+Vector3.up*(toy?.25f:0),target=goal.Value+Vector3.up*(toy?.35f:0),aim=target-origin;Collider collider=m.Foe?.Hp>0?m.Foe.Collider:m.Other.Collider;
                    if(toy)Fx(target,.1f,0,origin,m.Owner,"minionShot");if(aim.magnitude>(toy?9.5f:2.4f)||!SummonVisible(m,origin+Vector3.up*(toy?0:.3f),target+Vector3.up*(toy?0:.3f),collider))continue;
                    float damage=(toy?8:12)*(float)m.Owner.Stats["damage"],impulse=toy?50:120;Vector3 flat=new Vector3(aim.x,0,aim.z).normalized;
                    if(m.Foe?.Hp>0){if(Time<m.Foe.BlockEnd){BlockProc(m.Foe,m.Owner,m.Foe.Collider.ClosestPoint(at),"clash");if(!toy)m.Body.AddForce((at-m.Foe.Body.position).normalized*400,ForceMode.Impulse);}else Damage(m.Foe,m.Owner,damage,flat,impulse*(float)m.Owner.Stats["impulse"],false,direct:false,primary:false);}
                    else if(m.Other?.Hp>0)HitMinion(m.Other,m.Owner,damage,flat,impulse);continue;
                }
                if(Time<m.NextAttack&&m.LastAttack>=0&&Time-m.LastAttack<windup+recover){if(Time-m.LastAttack>windup+.12f)m.Body.linearVelocity=new Vector3(0,velocity.y,0);continue;}
                m.Foe=null;m.Other=null;float best=float.PositiveInfinity;
                foreach(var p in Pilots)if(p.Hp>0&&p.Seat.connected&&Enemy(m.Owner,p)){float d=(p.Body.position-at).sqrMagnitude;if(d<best){best=d;m.Foe=p;}}
                foreach(var q in Minions)if(q!=m&&q.Hp>0&&Enemy(m.Owner,q)){float d=(q.Body.position-at).sqrMagnitude;if(d<best){best=d;m.Other=q;m.Foe=null;}}
                if(m.Foe==null&&m.Other==null){m.Body.linearVelocity=new Vector3(0,velocity.y,0);continue;}
                Vector3 destination=m.Foe!=null?m.Foe.Body.position:m.Other.Body.position,diff=destination-at;Collider end=m.Foe!=null?m.Foe.Collider:m.Other.Collider;float distance=new Vector2(diff.x,diff.z).magnitude;
                if(distance<(toy?7:1.75f)&&Time>=m.NextAttack&&SummonVisible(m,at+Vector3.up*.3f,destination+Vector3.up*.3f,end)){m.LastAttack=Time;m.StrikeAt=Time+windup;m.Yaw=Mathf.Atan2(diff.x,diff.z)*Mathf.Rad2Deg;m.Body.linearVelocity=new Vector3(0,velocity.y,0);continue;}
                Vector3 dir=new Vector3(diff.x,0,diff.z).normalized;
                if(!SummonVisible(m,at+Vector3.up*.15f,at+Vector3.up*.15f+dir,end))dir=Quaternion.Euler(0,Mathf.Sin(Time*.9f+m.Id)>0?90:-90,0)*dir;
                m.Yaw=Mathf.Atan2(dir.x,dir.z)*Mathf.Rad2Deg;float speed=distance>(toy?5:1.25f)?m.Speed*(Time<m.SlowUntil?m.SlowFactor:1):0;m.Body.linearVelocity=dir*speed+Vector3.up*velocity.y;
            }
        }
        private bool TryNecromancy(NativePilot p)
        {
            if(!Has(p,"necronomicon")||p.Weapon.@class!="magic")return false;
            if(!p.Input.AbilityE||AbilityBusy(p)||Time<p.BlockEnd||Time<p.EReady)return true;float cost=35*(float)p.Stats["resourceCost"];if(p.Mana<cost)return true;
            p.Mana-=cost;p.EReady=Time+10;p.Ready=Time+.5f;p.AttackAt=Time;Summon(p,Math.Min(14,3+(int)p.Stats["extraProjectiles"]),"zombie",p.Body.position,14,true);return true;
        }
    }
}
