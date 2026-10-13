using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private Collider[] shotOverlaps=new Collider[32];private RaycastHit[] shotSweeps=new RaycastHit[64];
        private struct NativeContact {public Collider Collider;public Vector3 Point,Contact,Normal;public float Distance;}
        private NativeContact? SweepNative(Vector3 from,Vector3 delta,float radius,Func<Collider,bool> accepts)
        {
            var scene=root.scene.GetPhysicsScene();int count;Collider overlap=null;
            while((count=scene.OverlapSphere(from,radius,shotOverlaps,ShotMask,QueryTriggerInteraction.Ignore))==shotOverlaps.Length)Array.Resize(ref shotOverlaps,shotOverlaps.Length*2);
            for(int i=0;i<count;i++)
            {
                var c=shotOverlaps[i];if(!accepts(c))continue;bool wall=c.GetComponent<NativePilotBody>()==null&&c.GetComponent<NativeMinionBody>()==null;
                if(overlap==null||wall&&(overlap.GetComponent<NativePilotBody>()!=null||overlap.GetComponent<NativeMinionBody>()!=null)||wall==(overlap.GetComponent<NativePilotBody>()==null&&overlap.GetComponent<NativeMinionBody>()==null)&&c.GetInstanceID()<overlap.GetInstanceID())overlap=c;
            }
            if(overlap!=null)return new NativeContact{Collider=overlap,Point=from,Contact=overlap.ClosestPoint(from-delta.normalized*radius),Normal=-delta.normalized};
            float distance=delta.magnitude;if(distance<.000001f)return null;
            while((count=scene.SphereCast(from,radius,delta.normalized,shotSweeps,distance,ShotMask,QueryTriggerInteraction.Ignore))==shotSweeps.Length)Array.Resize(ref shotSweeps,shotSweeps.Length*2);
            NativeContact? closest=null;
            for(int i=0;i<count;i++){var hit=shotSweeps[i];if(!accepts(hit.collider)||closest.HasValue&&hit.distance>=closest.Value.Distance)continue;closest=new NativeContact{Collider=hit.collider,Point=from+delta.normalized*hit.distance,Contact=hit.point,Normal=hit.normal,Distance=hit.distance};}
            return closest;
        }
        private bool Has(NativePilot p,string chip)=>p!=null&&p.Seat.chips.TryGetValue(chip,out int count)&&count>0;
        private bool Has(NativeBullet p,string chip)=>p.Mods.TryGetValue(chip,out int count)&&count!=0;
        private void Fire(NativePilot p,bool fan=false,NativeBowShot bow=null)
        {
            var w=p.Weapon;bool pong=bow?.Detail!="rain"&&p.Stats["pong"]>0&&!w.devOnly&&p.VolleyIndex++%Math.Max(1,(int)Math.Ceiling(.24/w.interval))==0;
            if(pong){var balls=Bullets.Where(b=>b.Pong&&b.Original==p.Seat.id).ToArray();if(balls.Length>=24)Bullets.Remove(balls[0]);}
            if(ProjectileCount>=100)return;
            Vector3 eye=p.Body.position+Vector3.up*.3f,start=eye+Quaternion.Euler(0,p.Yaw,0)*new Vector3(.2f,0,.48f);
            if(bow?.Origin!=null)start=bow.Origin.Value;
            Vector3 offset=p.Input.AimTarget.Length>.01f?Vec(p.Input.AimTarget)-start:Vec(GunRules.Direction(p.Yaw,p.Pitch))*(float)w.range;
            float yaw=Mathf.Atan2(offset.x,offset.z)*Mathf.Rad2Deg,distance=new Vector2(offset.x,offset.z).magnitude;
            bool returning=p.Stats["boomerang"]>0&&!pong;float speed=(float)((bow?.Speed??(float)w.speed)*p.Stats["projectileSpeed"]),gravity=pong||returning?0:(float)((bow?.Gravity??(float)w.gravity)*p.Stats["projectileGravity"]);
            if(pong)speed=Mathf.Clamp(speed,30,80);
            float pitch=p.Aiming&&gravity>0?GunRules.BallisticPitch(distance,offset.y,speed,gravity):Mathf.Atan2(offset.y,distance)*Mathf.Rad2Deg;
            bool overdrive=Overdriving(p);double spread=NativeRangedRules.Spread((double?)bow?.Spread??w.spread,fan);var ads=Content.Ads(p.Seat.spec);if(!fan&&!overdrive&&p.Aiming&&ads!=null){spread*=1+(ads.spread-1)*Mathf.Clamp01((Time-p.AimAt)/(float)ads.settle);if(ads.bloom>0&&Time-p.LastShot<=.25f)spread=Math.Min(w.spread,spread+ads.bloom*p.AimShots);}
            p.AimShots=overdrive?0:Time-p.LastShot>.25f?1:p.AimShots+1;p.LastShot=Time;if(bow==null&&w.@class!="magic")p.Ready=Time+NativeRangedRules.Interval(w.interval,p.Stats["attackRate"],overdrive)*(p.Aiming&&ads!=null&&ads.interval>0?(float)ads.interval:1);p.Pose.ShotsFired++;
            int count=NativeProjectileRules.Volley(p.Stats,pong),attack=nextAttack++;float empowerment=Time<p.EmpowerUntil&&p.EmpowerShots>0?1+(float)p.Stats["qEmpower"]:1;if(empowerment>1)p.EmpowerShots--;
            var volley=new List<NativeBullet>();
            for(int i=0;i<count&&ProjectileCount<100;i++)
            {
                Vector3 dir=(Vec(GunRules.Direction(yaw+(bow?.Yaw??0)+NativeProjectileRules.Fan(i,count,(float)p.Stats["fanDegrees"]),pitch))+new Vector3((float)(random.NextDouble()-.5),(float)(random.NextDouble()-.5),(float)(random.NextDouble()-.5))*(float)spread).normalized;
                if(bow?.Direction!=null)dir=bow.Direction.Value.normalized;
                var shot=new NativeBullet{Draw=bow?.Draw??0,Detail=bow?.Detail,Id=nextBullet++,Attack=attack,Owner=p.Seat.id,Original=p.Seat.id,Spec=p.Seat.spec,Born=Time,Position=start,Velocity=dir*speed,BaseVelocity=dir*speed,FixedSpeed=speed,Damage=(float)((bow?.Damage??(float)w.damage)*p.Stats["damage"])*(fan?NativeRangedRules.FanDamage:1)*(count>1?.75f:1)*(w.ammo>0&&p.Ammo==0?1+(float)p.Stats["lastRound"]:1)*empowerment,Gravity=gravity,Radius=(float)((w.shotRadius>0?w.shotRadius:w.@class=="magic"?.14:.045)*p.Stats["projectileSize"]),Impulse=(float)((bow?.Impulse??(float)w.impulse)*p.Stats["impulse"]),Bounces=(int)p.Stats["bounces"],Pierces=(int)p.Stats["pierces"],BounceRetention=(float)p.Stats["bounceRetention"],Burn=(float)(w.burn+p.Stats["burn"]),Poison=(float)p.Stats["poison"],Demolition=(float)p.Stats["demolition"],Homing=(float)p.Stats["homing"],Life=returning?4:3,Travel=returning?320:160,Boomerang=returning,Zigzag=p.Stats["zigzag"]>0,Mods=new Dictionary<string,int>(p.Seat.chips)};
                if(pong){shot.Pong=true;shot.RallyMark=start;shot.RallyCap=speed*1.6f;shot.RallyBonus=Math.Min(shot.Damage*.15f,8);shot.Bounces=Math.Min(6,(int)Math.Ceiling(3d/Math.Ceiling(.24/w.interval))+(int)p.Stats["bounces"]);shot.Life=3.5f;shot.Travel=200;shot.Homing=0;shot.Pierces=0;shot.Zigzag=false;}
                var obstruction=bow?.Origin!=null?null:SweepNative(eye,start-eye,shot.Radius,c=>c!=p.Collider&&c.GetComponent<NativePilotBody>()==null);
                if(obstruction.HasValue&&BulletImpact(shot,p,obstruction.Value))continue;Bullets.Add(shot);volley.Add(shot);if(bow!=null)Events.Add(new ArenaEvent{Id=nextEvent++,Kind=EventKind.Shot,Source=p.Seat.id,Time=Time,Position=Vec(start),Detail=bow.Detail,Draw=bow.Draw,Projectile=shot.Id});
            }
            SpawnChipShots(p,volley);LeetSpawned(p,volley);if(p.Stats["recoil"]>0)p.Knockback-=Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch))*Math.Min(15,(float)p.Stats["recoil"]);
            if(bow==null)Emit(EventKind.Shot,p,0,start,(int)p.Pose.Weapon,fan?"fan":overdrive?"overdrive":null);
        }
        private void StepBullets()
        {
            const float dt=1f/60;
            // A newly spawned fragment starts next tick, regardless of collection insertion order.
            foreach(var b in Bullets.ToArray())
            {
                if(!Bullets.Contains(b)||StepSirenShot(b))continue;var owner=Pilots.Find(p=>p.Seat.id==b.Owner);var original=Pilots.Find(p=>p.Seat.id==b.Original);
                if(owner==null||b.Life<=0||b.Travel<=0||b.Pong&&(original==null||original.Hp<=0)){Bullets.Remove(b);continue;}
                if(!LeetShotStep(b,owner,dt))continue;float scale=b.LeetKind!=null?1:ChipShotMotion(b,owner,dt);if(scale<=0)continue;float h=Math.Min(dt*scale,b.Life),age=Time-b.Born;
                bool turned=b.Boomerang&&age>=Math.Min(.65f,24/Math.Max(1,b.FixedSpeed));
                if(turned&&original!=null){if(b.ReturnAt<0){b.ReturnAt=Time;b.Hit.Clear();b.Damage*=1.4f;}b.Velocity=Vec(NativeProjectileRules.Return(Vec(b.Velocity),Vec(original.Body.position+Vector3.up*.3f-b.Position),Mathf.PI*4/3*dt));}
                if(b.Zigzag){float angle=.55f*(Mathf.Sin((age+dt)*Mathf.PI*12)-Mathf.Sin(age*Mathf.PI*12));b.Velocity=Quaternion.AngleAxis(angle*Mathf.Rad2Deg,Vector3.up)*b.Velocity;}
                if(b.Pong)b.Velocity=b.Velocity.normalized*b.FixedSpeed;
                if(b.Direct&&b.Homing>0&&!turned)
                    foreach(var target in Pilots)
                    {Vector3 d=target.Body.position+Vector3.up*.15f-b.Position;if(target.Hp<=0||!Enemy(owner,target)||b.Hit.Contains(target.Collider.GetInstanceID())||d.magnitude>16||Vector3.Dot(b.Velocity.normalized,d.normalized)<Mathf.Cos(50*Mathf.Deg2Rad)||!MeleeVisible(b.Position,target.Body.position,owner,target.Collider))continue;b.Velocity=Vec(NativeProjectileRules.Steer(Vec(b.Velocity),Vec(d),b.Homing*Mathf.Deg2Rad*h));break;}
                Vector3 before=b.Velocity;var velocity=Vec(before);Vector3 previous=b.Position;Vector3 delta=Vector3.ClampMagnitude(Vec(NativeSpecialRules.Ballistic(ref velocity,b.Gravity,h)),b.Travel);b.Velocity=Vec(velocity);b.Life-=h;b.Travel-=delta.magnitude;b.Travelled+=delta.magnitude;
                if(CyberAbsorb(owner,b.Position,b.Position+delta,b.Radius)){Bullets.Remove(b);continue;}
                var hit=SweepNative(b.Position,delta,b.Radius,c=>
                {var p=c.GetComponent<NativePilotBody>()?.Pilot;var m=c.GetComponent<NativeMinionBody>()?.Minion;return (m==null||m.Hp>0&&CanHit(owner,m))&& !b.Hit.Contains(c.GetInstanceID())&&(!b.Pong||!b.Contacts.TryGetValue(c.GetInstanceID(),out float contact)||Time-contact>.18f)&&(p==null||p.Hp>0&&CanHit(owner,p)&&!Invulnerable(p));});
                bool ceiling=NativeSpecialRules.Ceiling(Vec(b.Position),Vec(delta),b.Radius,out float fraction,out V3 center),remove=false;
                if(ceiling&&(!hit.HasValue||delta.magnitude*fraction<=hit.Value.Distance))
                {
                    b.Position=Vec(center);bool bounces=b.Pong||b.Bounces>0;
                    Emit(EventKind.CeilingHit,owner,0,b.Position,0,bounces?"BOUNCE":"ABSORB",color:bounces?0x75ddff:0xb5a3ff);
                    Vector3 reflected=before+Vector3.down*b.Gravity*h*fraction;reflected.y=-Math.Abs(reflected.y);
                    if(b.Pong)remove=PongBounce(b,owner,b.Position,reflected);else if(b.Bounces>0){b.Bounces--;b.Velocity=reflected;b.Damage=Math.Min(1000,b.Damage*b.BounceRetention);}else remove=true;
                }
                else if(hit.HasValue){b.Velocity=before+Vector3.down*b.Gravity*h*Mathf.Clamp01(hit.Value.Distance/Math.Max(.00001f,delta.magnitude));remove=BulletImpact(b,owner,hit.Value);}else b.Position+=delta;
                if(b.Pong&&!remove&&(Math.Abs(b.Position.x)>32||Math.Abs(b.Position.z)>28||b.Position.y<.2f||b.Position.y>24))
                {
                    Vector3 v=b.Velocity,pos=b.Position;if(pos.x<-32||pos.x>32){pos.x=Mathf.Clamp(pos.x,-32,32);v.x=Math.Abs(v.x)*(pos.x<0?1:-1);}if(pos.z<-28||pos.z>28){pos.z=Mathf.Clamp(pos.z,-28,28);v.z=Math.Abs(v.z)*(pos.z<0?1:-1);}if(pos.y<.2f||pos.y>24){pos.y=Mathf.Clamp(pos.y,.2f,24);v.y=Math.Abs(v.y)*(pos.y<1?1:-1);}remove=PongBounce(b,owner,pos,v);
                }
                if(!hit.HasValue&&turned&&original!=null&&Vector3.Distance(previous+delta.normalized*Mathf.Clamp(Vector3.Dot(original.Body.position+Vector3.up*.3f-previous,delta.normalized),0,delta.magnitude),original.Body.position+Vector3.up*.3f)<.8f)
                {
                    if(b.Direct&&ProcOnce("catch:"+b.Attack)){Emit(EventKind.Catch,original,0,b.Position,0);if(original.Weapon.@class=="ranged")original.Ammo=Math.Min(Magazine(original),original.Ammo+1);else original.Mana=Math.Min((float)original.Stats["maxMana"],original.Mana+(float)original.Weapon.cost*.5f);}remove=true;
                }
                if(remove||b.Life<=0||b.Travel<=0||b.Position.y<-15)Bullets.Remove(b);
            }
        }
        private bool PongBounce(NativeBullet b,NativePilot owner,Vector3 point,Vector3 velocity)
        {
            if(b.Bounces--<=0){var w=Content.Weapon(b.Spec);if(w.blast>0&&(b.Direct||b.Reflections>0))ChipBlast(owner,point,b.Radius>.2f?3:(float)w.blast,b.Damage*.38f,b.Impulse,effect:b.Spec);return true;}
            int rallyTarget=0;b.FixedSpeed=Math.Min(b.RallyCap,b.FixedSpeed*1.12f);Vector3 from=point+velocity.normalized*(b.Radius+.1f);
            if(!(b.Velocity.y<0&&velocity.y>0)&&Vector3.Distance(point,b.RallyMark)>=6)
            {
                var candidates=Pilots.Where(p=>p.Hp>0&&Enemy(owner,p)&&Vector3.Distance(from,p.Body.position)<12&&MeleeVisible(from,p.Body.position,owner,p.Collider)).OrderBy(p=>(p.Body.position-from).sqrMagnitude).Take(4);
                foreach(var target in candidates){Vector3 d=target.Body.position+Vector3.up*.15f-from;if(Vector3.Dot(d.normalized,velocity.normalized)<Mathf.Cos(30*Mathf.Deg2Rad))continue;velocity=d+target.Body.linearVelocity*(d.magnitude/b.FixedSpeed*.4f);b.Damage+=b.RallyBonus;rallyTarget=target.Seat.id;break;}
            }
            // The bounce's look comes from its Pong event (the source's `ricochet`: NativeBlastPresentation.Impacts); the first port's
            // fire-school ability effect here drew a fireball on every rally. Presentation feed only.
            b.RallyMark=point;b.Position=from;b.Velocity=velocity.normalized*b.FixedSpeed;Emit(EventKind.Pong,owner,rallyTarget,from,b.Damage,rallyTarget!=0?"rally":null,from:point);return false;
        }
        private bool BulletImpact(NativeBullet b,NativePilot owner,NativeContact hit)
        {
            b.Position=hit.Point;var minion=hit.Collider.GetComponent<NativeMinionBody>()?.Minion;var target=hit.Collider.GetComponent<NativePilotBody>()?.Pilot;var prop=hit.Collider.GetComponent<NativePropBody>();
            if(target!=null&&AbsorbCharge(target,b.Velocity,hit.Contact,b.Id))return true;
            if(target!=null&&Time<target.BlockEnd)
            {
                BlockProc(target,owner,hit.Contact,"deflect");if(b.Reflections>=3)return true;b.Reflections++;b.Owner=target.Seat.id;b.Direct=b.ChipImpact=b.Boomerang=false;b.Homing=0;b.Hit.Clear();b.Attack=nextAttack++;
                b.Damage=Math.Min(100,b.Damage*(float)target.Stats["reflectDamage"]);b.Velocity=-b.Velocity.normalized*(b.Pong?b.FixedSpeed:Math.Min(120,b.Velocity.magnitude*(float)target.Stats["reflectSpeed"]));b.Position+=b.Velocity.normalized*.025f;if(b.Pong)b.RallyMark=b.Position;return false;
            }
            if(b.LeetKind=="brain"){if(target!=null)Possess(owner,target);return true;}LeetImpact(owner,b,hit.Point);
            if(b.Pong&&target==null&&minion==null&&b.Bounces>0){b.Contacts[hit.Collider.GetInstanceID()]=Time;return PongBounce(b,owner,hit.Point,Vector3.Reflect(b.Velocity,hit.Normal));}
            if(minion!=null)
            {
                float actual=HitMinion(minion,owner,b.Damage,b.Velocity.normalized,b.Impulse,hit.Point,b.Detail=="bit-bolt"?"bit-bolt":b.Reflections>0?"reflected":b.Direct?"direct":"secondary");if(b.Direct||b.WeaponFx&&b.Reflections==0)MinionWeaponHit(minion,owner,actual,b.Burn,b.Poison);
                if((b.Direct||b.ChipImpact)&&b.Reflections==0)ImpactChips(owner,null,null,hit.Point,b.Velocity.normalized,b.Attack,b.Id,minion);
            }
            else if(target!=null)
            {
                bool head=!b.CyberPacket&&NativeCombatRules.Headshot(Vec(target.Body.position),target.Yaw,Vec(hit.Point-b.Velocity.normalized*.1f),Vec(b.Velocity),b.Radius,target.Down!=null||target.Juggle!=null);float actual=Damage(target,owner,b.Damage*(head?1.5f*(b.Spec=="bow"&&b.Draw>=1?1.25f:1):1),b.Velocity.normalized,b.Impulse,head,primary:b.Direct,impact:NativeBodyDamage.Projectile(b.Spec,b.Velocity.magnitude,b.Radius),hitPoint:hit.Point,weaponHit:b.Direct&&b.Reflections==0,damageKind:b.Detail=="bit-bolt"?"bit-bolt":b.CyberPacket?"packet-burst":b.Reflections>0?"reflected":b.Direct?"direct":"secondary");
                if(actual>0&&b.Detail=="bit-bolt")owner.Mana=Math.Min((float)owner.Stats["maxMana"],owner.Mana+CyberRangedSpec.BoltMana);
                if(actual>0&&b.Detail=="rain"){target.SlowFactor=Math.Min(target.SlowFactor,.8f);target.SlowEnd=Math.Max(target.SlowEnd,Time+1.5f);}
                FighterProjectileHit(b,owner,target,actual);
                if(actual>0&&!b.Direct&&(b.Reflections>0||b.WeaponFx)){RefreshDot(target,owner,"fire",b.Burn,2);RefreshDot(target,owner,"poison",b.Poison,4);if(b.WeaponFx&&b.Reflections==0)Heal(owner,actual*(float)(owner.Stats["lifeSteal"]+owner.Weapon.drain));}
                if(actual>0&&(b.Direct||b.ChipImpact)&&b.Reflections==0)ImpactChips(owner,target,null,hit.Point,b.Velocity.normalized,b.Attack,b.Id);
            }
            else
            {
                DamageProp(prop,b.Damage*b.Demolition,b.Velocity.normalized*b.Impulse*(b.Direct&&b.Reflections==0&&Leet(owner,"yeet")?1000:1),hit.Point,owner,"bullet");
                if((b.Direct||b.ChipImpact)&&b.Reflections==0)ImpactChips(owner,null,prop,hit.Point,b.Velocity.normalized,b.Attack,b.Id);
                if(prop!=null||b.Detail=="bit-bolt")Events.Add(new ArenaEvent{Id=nextEvent++,Kind=EventKind.Impact,Source=owner.Seat.id,Time=Time,Position=Vec(hit.Point),Normal=Vec(hit.Normal),Detail=b.Detail,SurfaceId=prop?.Definition.id,Color=(int)Content.Weapon(b.Spec).color,Projectile=b.Id});
            }
            if((b.Direct||b.Reflections>0)&&!b.EffectsUsed)
            {b.EffectsUsed=true;var w=Content.Weapon(b.Spec);if(w.blast>0)ChipBlast(owner,hit.Point,b.Radius>.2f?3:(float)w.blast,b.Damage*.38f,b.Impulse,effect:b.Spec);if(b.Direct&&!b.Pong)SplitBullet(owner,b,hit.Point,target?.Collider.GetInstanceID());}
            if(b.Pong)return true;
            if((target!=null||minion!=null)&&b.Pierces>0){b.Pierces--;b.Hit.Add(hit.Collider.GetInstanceID());b.Position+=b.Velocity.normalized*.08f;return false;}
            if(prop!=null&&b.Bounces>0&&Props.Contains(prop)){Emit(EventKind.Ricochet,owner,0,hit.Point,b.Damage);b.Bounces--;b.Velocity=Vector3.Reflect(b.Velocity,hit.Normal);b.Position+=b.Velocity.normalized*(b.Radius+.06f);b.Damage=Math.Min(1000,b.Damage*b.BounceRetention);return false;}
            return true;
        }
        private void SplitBullet(NativePilot owner,NativeBullet parent,Vector3 point,int? skip)
        {
            if(owner.Stats["split"]<=0)return;Emit(EventKind.Split,owner,0,point,parent.Damage);int count=2+(int)owner.Stats["split"];
            for(int i=0;i<count&&ProjectileCount<100;i++)
            {
                var b=CopyBullet(parent);b.Position=point-parent.Velocity.normalized*.18f;b.Velocity=Vec(GunRules.Direction(owner.Input.Yaw+(i-(count-1)/2f)*.32f*Mathf.Rad2Deg,owner.Input.Pitch))*parent.Velocity.magnitude*.75f;
                b.Damage*=.22f;b.Impulse*=.2f;b.Direct=b.WeaponFx=b.Pong=b.Boomerang=b.Zigzag=false;b.ChipImpact=Has(owner,"fireworks")&&i<5;b.EffectsUsed=true;b.Reflections=b.Bounces=b.Pierces=0;b.Burn=b.Poison=b.Homing=0;b.Life=1.2f;b.Travel=18;if(skip.HasValue)b.Hit.Add(skip.Value);Bullets.Add(b);
            }
        }
        private NativeBullet CopyBullet(NativeBullet b)=>new NativeBullet{CyberPacket=b.CyberPacket,Draw=b.Draw,Detail=b.Detail,Id=nextBullet++,Attack=b.Attack,Owner=b.Owner,Original=b.Original,Spec=b.Spec,Born=b.Born,Position=b.Position,Velocity=b.Velocity,BaseVelocity=b.BaseVelocity,Motion=b.Motion,Zigzag=b.Zigzag,Damage=b.Damage,Impulse=b.Impulse,Radius=b.Radius,Gravity=b.Gravity,Life=b.Life,Travel=b.Travel,Bounces=b.Bounces,Pierces=b.Pierces,BounceRetention=b.BounceRetention,FixedSpeed=b.FixedSpeed,Homing=b.Homing,Burn=b.Burn,Poison=b.Poison,Demolition=b.Demolition,Mods=new Dictionary<string,int>(b.Mods)};
    }
}
