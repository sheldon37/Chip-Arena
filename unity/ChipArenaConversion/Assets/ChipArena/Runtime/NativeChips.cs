using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    public sealed class NativeChipField
    {public int Id;public NativePilot Owner,Attached;public NativeMinion Minion;public NativePropBody Prop;public string Kind,Effect;public Vector3 Position;public float Radius,Born,Until,Arm,Damage;}
    public sealed partial class NativeArena
    {
        public readonly List<NativeChipField> ChipFields=new List<NativeChipField>();
        private readonly NativeProcLedger procLedger=new NativeProcLedger();
        private readonly Dictionary<string,float> chipCooldowns=new Dictionary<string,float>();
        private static readonly (string id,string kind,float radius,float duration,float damage)[] fieldRules={
            ("black-hole","black-hole",5,3.5f,10),("acid-rain","acid",3,4,12),("wildfire","fire",2.5f,3,15),("blizzard","frost",3.5f,3,4),("time-warp","time",4,4,0),("repulsion-pad","repulsion",3.5f,3,0),("med-garden","heal",2.5f,4,6),("lightning-rod","storm",4,3,18),("bubble-mine","bomb",3,1,25),("sticky-bomb","bomb",3,1.2f,28),("proximity-mail","mine",2.5f,6,32),("delayed-gratification","bomb",4,2.2f,48),("implosion-bomb","implosion",4,.9f,30),("demolition-charge","bomb",3,1,18),("aftershock","bomb",3.5f,.45f,20)};
        private bool ProcOnce(string key)
        {
            if(Expedition?.Session.Run.SplitAreas==true&&(key.StartsWith("undo:")||key.EndsWith(":exit")||key.EndsWith(":panic")||key.EndsWith(":round-two")))
            {
                string number=key.StartsWith("undo:")?key.Substring(5):key.Substring(0,key.IndexOf(':'));
                if(int.TryParse(number,out int id)&&Pilots.Find(p=>p.Seat.id==id) is NativePilot pilot)return pilot.TransferProcs.Add(key);
            }
            return procLedger.Add(key);
        }
        private bool ChipReady(NativePilot p,string key,float seconds){key=p.Seat.id+":"+key;if(chipCooldowns.TryGetValue(key,out float until)&&Time<until)return false;chipCooldowns[key]=Time+seconds;return true;}
        private float Heal(NativePilot p,float amount,string detail=null){if(p==null||Expedition?.Session.Run.SplitAreas==true&&!Pilots.Contains(p)||p.Hp<=0||amount<=0)return 0;float actual=Math.Min(amount,Math.Max(0,(float)p.Stats["hp"]-p.Hp));p.Hp+=actual;if(actual>0)Emit(EventKind.Heal,p,p.Seat.id,p.Body.position,actual,detail);return actual;}
        private void BlockProc(NativePilot p,NativePilot attacker,Vector3 point,string detail="clash")
        {
            Emit(EventKind.Block,p,attacker?.Seat.id??0,point,0,detail,normal:attacker!=null?(attacker.Body.position-point).normalized:Vector3.up);if(Dev(p,"ansatsuken")){CancelFighter(p);p.BlockReady=p.BlockEnd=p.Ready=p.StunEnd=0;p.ShieldUntil=Time+.1f;}if(p.BlockProcUsed)return;p.BlockProcUsed=true;
            if(Has(p,NativeRoundsCards.Prefix+"phantom"))p.ShieldUntil=Math.Max(p.ShieldUntil,Time+.6f);
            Heal(p,(float)p.Stats["blockHeal"]);if(p.Weapon.ammo>0)p.Ammo=Math.Min(Magazine(p),p.Ammo+(int)p.Stats["blockAmmo"]);
            if(p.Stats["blockPulseDamage"]>0){Emit(EventKind.BlockPulse,p,0,p.Body.position,(float)p.Stats["blockPulseDamage"],radius:3);ChipBlast(p,p.Body.position,3,(float)p.Stats["blockPulseDamage"],(float)p.Stats["blockPulseImpulse"],effect:Has(p,"supernova")?"supernova":"block-pulse");}
            if(Has(p,"supernova"))p.BlockReady=Math.Min(p.BlockReady,Time+.6f);
            if(Has(p,"mirror-mine"))MineRing(p,p.Body.position,"mirror-mine");
            if(Has(p,"freezer-block")){Field(p,"time",p.Body.position,5,3,0,"freezer-block");foreach(var q in Pilots)if(q.Hp>0&&CanHit(p,q)&&Vector3.Distance(p.Body.position,q.Body.position)<5&&MeleeVisible(p.Body.position,q.Body.position,p,q.Collider)&&!SirenArmor(q)){Emit(EventKind.Freeze,p,q.Seat.id,q.Body.position,1.2f);q.FrozenUntil=Math.Max(q.FrozenUntil,Time+1.2f);StunMelee(q,1.2f);}}
            if(Has(p,"graveyard-shift")||Has(p,"corpse-party"))Summon(p,1,"zombie",p.Body.position,10);
            if(Has(p,"poltergeist"))foreach(var prop in Props)if(prop.Body!=null&&!prop.Body.isKinematic&&Vector3.Distance(prop.Body.position,p.Body.position)<6&&MeleeVisible(p.Body.position,prop.Body.position,p,prop.GetComponent<Collider>()))prop.Body.AddForce((prop.Body.position-p.Body.position+Vector3.up*.4f).normalized*900,ForceMode.Impulse);
        }
        private float ChipBlast(NativePilot owner,Vector3 at,float radius,float damage,float impulse,bool props=true,string effect="chip")
        {
            if(owner==null)return 0;float total=0;Emit(EventKind.Explosion,owner,0,at,damage,effect,radius);Fx(at,radius,owner.Pose.MagicVisual);
            foreach(var target in Pilots.ToArray())
            {Vector3 delta=target.Body.position-at;float fall=NativeProjectileRules.Falloff(delta.magnitude,radius);if(target.Hp<=0||!CanHit(owner,target)||fall<=0||HubCross(at,target.Body.position)||!MeleeVisible(at+delta.normalized*.1f,target.Body.position,owner,target.Collider))continue;total+=Damage(target,owner,damage*fall,delta.normalized,impulse*fall,false,direct:false,primary:false,damageKind:"blast");}
            foreach(var m in Minions.ToArray()){Vector3 d=m.Body.position-at;float f=NativeProjectileRules.Falloff(d.magnitude,radius);if(f>0&&CanHit(owner,m)&&!HubCross(at,m.Body.position)&&MeleeVisible(at,m.Body.position,owner,m.Collider))total+=HitMinion(m,owner,damage,d.normalized,impulse*f,at,"blast",radius,from:at);}
            if(props)foreach(var prop in Props.ToArray())
            {if(prop==null||prop.Indestructible)continue;if(!string.IsNullOrEmpty(prop.Definition.role)){StructureBlast(prop,owner,at,radius,damage,impulse);continue;}Vector3 delta=prop.transform.position-at;float reach=radius+prop.Definition.size.Length*.35f;if(delta.magnitude>=reach||!MeleeVisible(at,prop.transform.position,owner,prop.GetComponent<Collider>()))continue;float fall=1-Mathf.Clamp(delta.magnitude/reach,0,.85f);DamageProp(prop,damage*fall*(float)owner.Stats["demolition"],(delta+Vector3.up*.3f).normalized*impulse*fall,at,owner);}
            return total;
        }
        private void Vacuum(NativePilot owner,Vector3 at,float impulse)
        {
            foreach(var p in Pilots){Vector3 d=at-p.Body.position;float distance=d.magnitude;if(p.Hp<=0||!CanHit(owner,p)||SirenArmor(p)||distance<.1f||distance>5||!MeleeVisible(at-d.normalized*.12f,p.Body.position,owner,p.Collider))continue;p.Knockback+=d.normalized*Math.Min(2000,impulse)*(1-distance/5)/65*(float)p.Stats["knockbackTaken"];}
            foreach(var p in Props)if(p.Body!=null&&!p.Body.isKinematic){Vector3 d=at-p.Body.position;float distance=d.magnitude;if(distance>.1f&&distance<5&&MeleeVisible(at-d.normalized*.12f,p.Body.position,owner,p.GetComponent<Collider>()))p.Body.AddForce(d.normalized*Math.Min(2000,impulse)*(1-distance/5),ForceMode.Impulse);}
            Emit(EventKind.Vacuum,owner,0,at,impulse,radius:5);Fx(at,5,4);
        }
        private NativeChipField Field(NativePilot p,string kind,Vector3 point,float radius,float seconds,float damage,string effect,NativePilot target=null,NativePropBody prop=null,NativeMinion minion=null)
        {var f=new NativeChipField{Id=nextEvent++,Owner=p,Kind=kind,Position=point,Radius=radius,Damage=damage,Born=Time,Until=Time+seconds,Arm=Time+.6f,Effect=effect,Attached=target,Prop=prop,Minion=minion};ChipFields.Add(f);Emit(EventKind.Field,p,target?.Seat.id??minion?.Id??0,point,damage,effect,radius);return f;}
        private void MineRing(NativePilot p,Vector3 at,string effect)
        {for(int i=0;i<3;i++)Field(p,"mine",at+Quaternion.Euler(0,i*120,0)*Vector3.forward*1.6f,2.3f,6,16,effect);}
        private void ImpactChips(NativePilot owner,NativePilot target,NativePropBody prop,Vector3 point,Vector3 dir,int attack,int impact,NativeMinion minion=null)
        {
            if(owner==null)return;
            if(target!=null&&target.Hp>0&&CanHit(owner,target))
            {
                if(!SirenArmor(target)&&Has(owner,"uppercut"))target.Knockback.y+=8*(float)target.Stats["knockbackTaken"];
                if(!SirenArmor(target)&&Has(owner,"grappling-shot"))target.Knockback+=(owner.Body.position-target.Body.position).normalized*(1800f/65)*(float)target.Stats["knockbackTaken"];
                if(Has(owner,"coin-toss")){if(random.NextDouble()<.5){if(!SirenArmor(target))target.Knockback+=dir*(float)(owner.Weapon.impulse*owner.Stats["impulse"]/65*target.Stats["knockbackTaken"]);}else Heal(owner,6);}
                if(Has(owner,"swap-shop")&&ChipReady(owner,"swap",4)&&RoomFor(owner,target.Body.position,target)&&RoomFor(target,owner.Body.position,owner)){Vector3 a=owner.Body.position,b=target.Body.position;owner.Body.position=b;target.Body.position=a;owner.Knockback=target.Knockback=owner.MoveVelocity=target.MoveVelocity=Vector3.zero;owner.Body.linearVelocity=target.Body.linearVelocity=Vector3.zero;if(!SirenArmor(target)){target.FrozenUntil=Math.Max(target.FrozenUntil,Time+.5f);StunMelee(target,.5f);}Physics.SyncTransforms();Emit(EventKind.Teleport,owner,target.Seat.id,b,0,"swap",from:a);}
                if(Has(owner,"pickpocket")){if(target.Weapon.ammo>0){int n=Math.Min(1,target.Ammo);target.Ammo-=n;owner.Ammo=Math.Min(Magazine(owner),owner.Ammo+n);}else if(target.Weapon.@class=="magic")target.Mana=Math.Max(0,target.Mana-8);else target.Stamina=Math.Max(0,target.Stamina-8);}
                if(Has(owner,"mana-leech")){float n;if(target.Weapon.ammo>0){n=Math.Min(1,target.Ammo);target.Ammo-=(int)n;n*=8;}else if(target.Weapon.@class=="magic"){n=Math.Min(10,target.Mana);target.Mana-=n;}else{n=Math.Min(10,target.Stamina);target.Stamina-=n;}owner.Mana=Math.Min((float)owner.Stats["maxMana"],owner.Mana+n);}
            }
            if(minion!=null&&Has(owner,"coin-toss")&&random.NextDouble()>=.5)Heal(owner,6);
            foreach(var rule in fieldRules)if(Has(owner,rule.id)&&(rule.id!="demolition-charge"||prop!=null)&&ProcOnce(attack+":"+impact+":"+rule.id))Field(owner,rule.kind,point,rule.radius,rule.duration,rule.damage,rule.id,rule.id=="sticky-bomb"?target:null,rule.id=="sticky-bomb"?prop:null,rule.id=="sticky-bomb"?minion:null);
            if(Has(owner,"cluster-mine")&&ProcOnce(attack+":"+impact+":cluster"))MineRing(owner,point,"cluster-mine");
            if(Has(owner,"artillery")&&ProcOnce(attack+":"+impact+":artillery")&&ChipReady(owner,"artillery",1.5f))for(int i=0;i<5;i++)Field(owner,"bomb",point+Quaternion.Euler(0,i*2.4f*Mathf.Rad2Deg,0)*Vector3.forward*(i==0?0:1.1f+i*.35f),3,.85f+i*.3f,26,"artillery",i==0&&target?.Hp>0?target:null);
            if(Has(owner,"wormhole")&&prop!=null&&ProcOnce(attack+":"+impact+":portal")&&ChipReady(owner,"portal",1.2f)){ChipBlink(owner,point+Vector3.up-owner.Body.position);ChipBlast(owner,owner.Body.position,3,25,700,effect:"wormhole");}
            if(Has(owner,"puppet-master")&&ProcOnce(attack+":"+impact+":toy"))Summon(owner,1,"toy",point,6);
            if(owner.Stats["vacuum"]>0&&ProcOnce(attack+":vacuum"))Vacuum(owner,point,(float)owner.Stats["vacuum"]);
            if(owner.Stats["shrapnel"]>0&&ProcOnce(attack+":shrapnel"))ChipBlast(owner,point,1.8f,(float)owner.Stats["shrapnel"],220*(float)owner.Stats["impulse"],effect:"shrapnel");
        }
        private bool RoomFor(NativePilot p,Vector3 at,NativePilot ignore=null)
        {
            // Grounded capsules may touch their supporting floor; exclude contacts below their feet.
            var scene=root.scene.GetPhysicsScene();int n;while((n=scene.OverlapCapsule(at+Vector3.up*.63f,at-Vector3.up*.23f,.41f,shotOverlaps,ShotMask,QueryTriggerInteraction.Ignore))==shotOverlaps.Length)Array.Resize(ref shotOverlaps,shotOverlaps.Length*2);
            for(int i=0;i<n;i++){var c=shotOverlaps[i];if(c==p.Collider||c==ignore?.Collider||c.bounds.max.y<=at.y-.7f)continue;return false;}return true;
        }
        private void ChipBlink(NativePilot p,Vector3 delta,string effect=null)
        {
            if(KernelLocked(p))return;float length=Math.Min(12,delta.magnitude);if(length<.01f)return;var scene=root.scene.GetPhysicsScene();Vector3 origin=p.Body.position+Vector3.up*.12f;
            if(scene.CapsuleCast(origin+Vector3.up*.43f,origin-Vector3.up*.43f,.42f,delta.normalized,out var hit,length,WorldMask,QueryTriggerInteraction.Ignore)&&Vector3.Dot(hit.normal,delta.normalized)<-.05f)length=Math.Max(0,hit.distance-.15f);
            Vector3 target=origin+delta.normalized*length;target=ClampMap(target);if(!RoomFor(p,target))return;p.Body.position=target;p.Body.linearVelocity=p.Knockback=p.MoveVelocity=Vector3.zero;Physics.SyncTransforms();Emit(EventKind.Teleport,p,0,target,0,effect,from:origin);Fx(target,1,3,origin,p,"teleport");
        }
        private bool ChipSave(NativePilot p,float damage,bool forced)
        {if(forced||damage<p.Hp||!Has(p,"second-chance")||!ProcOnce(p.Seat.id+":exit"))return false;p.Hp=1;EmergencyExit(p);return true;}
        private void EmergencyExit(NativePilot p){Heal(p,40);ChipBlink(p,-Vec(V3.Forward(p.Input.Yaw))*6);p.ShieldUntil=Time+1.2f;}
        private void ChipDamaged(NativePilot p,NativePilot attacker,bool direct)
        {
            if(p.Hp<=0)return;
            if(Has(p,"second-chance")&&p.Hp<p.Stats["hp"]*.25&&ProcOnce(p.Seat.id+":exit"))EmergencyExit(p);
            if(Has(p,"panic-button")&&p.Hp<p.Stats["hp"]*.3&&ProcOnce(p.Seat.id+":panic")){Field(p,"repulsion",p.Body.position,6,4,0,"panic-button");p.BlockReady=p.RollReady=0;Heal(p,(float)p.Stats["hp"]*.3f);p.ShieldUntil=Time+2;}
            if(Has(p,"revenge-mail")&&direct&&attacker!=null&&ChipReady(p,"revenge",4))Field(p,"bomb",attacker.Body.position,3,1,25,"revenge-mail");
        }
        private void ChipRoll(NativePilot p)
        {
            Vector3 at=p.Body.position;if(p.Stats["dashBlast"]>0)ChipBlast(p,at,3,(float)p.Stats["dashBlast"],(float)p.Stats["dashBlast"]/14*380,effect:"dash-blast");
            if(Has(p,"bread-crumbs"))Field(p,"mine",at,2.8f,5,24,"bread-crumbs");if(Has(p,"hot-foot"))Field(p,"fire",at,3,3,15,"hot-foot");
            if(Has(p,"comet"))Field(p,"bomb",at,3.5f,.32f,45*(float)p.Stats["damage"],"comet",p);
            if(Has(p,"vampire-dash")){foreach(var q in Pilots)if(q.Hp>0&&CanHit(p,q)&&Vector3.Distance(at,q.Body.position)<3&&MeleeVisible(at,q.Body.position,p,q.Collider))Heal(p,Damage(q,p,12,Vector3.zero,0,false,direct:false,primary:false));foreach(var m in Minions.ToArray())if(m.Hp>0&&CanHit(p,m)&&Vector3.Distance(at,m.Body.position)<3&&MeleeVisible(at,m.Body.position,p,m.Collider))Heal(p,HitMinion(m,p,12,Vector3.zero,0));}
            if(Has(p,"thunderstep")){var targets=Pilots.Where(q=>q.Hp>0&&Enemy(p,q)&&Vector3.Distance(at,q.Body.position)<12&&MeleeVisible(at+Vector3.up*.5f,q.Body.position,p,q.Collider)).OrderBy(q=>(at-q.Body.position).sqrMagnitude).Take(2).ToArray();if(targets.Length==0)Field(p,"bomb",at,4,.5f,35,"thunderstep");foreach(var q in targets)Field(p,"bomb",q.Body.position,2.5f,.35f,30*(float)p.Stats["damage"],"thunderstep",q);}
        }
        private void ChipJump(NativePilot p)
        {if(!Has(p,"trampoline"))return;ChipBlast(p,p.Body.position,3,10,0,false,effect:"trampoline");foreach(var q in Pilots)if(q.Hp>0&&!SirenArmor(q)&&CanHit(p,q)&&Vector3.Distance(q.Body.position,p.Body.position)<3&&MeleeVisible(p.Body.position,q.Body.position,p,q.Collider))q.Knockback.y+=8*(float)q.Stats["knockbackTaken"];}
        private float ChipMeleeStart(NativePilot p)
        {
            float scale=1;if(Has(p,"blink-cut")){var aim=Vec(V3.Forward(p.Input.Yaw));var target=Pilots.Where(q=>q.Hp>0&&Enemy(p,q)&&Vector3.Distance(p.Body.position,q.Body.position)>1.6f&&Vector3.Distance(p.Body.position,q.Body.position)<7&&Vector3.Dot(aim,(q.Body.position-p.Body.position).normalized)>Mathf.Cos(40*Mathf.Deg2Rad)&&MeleeVisible(p.Body.position,q.Body.position,p,q.Collider)).OrderBy(q=>(q.Body.position-p.Body.position).sqrMagnitude).FirstOrDefault();if(target!=null&&ChipReady(p,"blink-cut",1)){Vector3 d=target.Body.position-p.Body.position;d.y=0;ChipBlink(p,d.normalized*Math.Max(0,d.magnitude-1.1f));p.Input.Yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;scale=1.5f;p.ShieldUntil=Time+.25f;}else ChipBlink(p,aim*2.5f);}
            if(Has(p,"sword-beam")&&ProjectileCount<100){var dir=Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch));Bullets.Add(new NativeBullet{Id=nextBullet++,Attack=nextAttack++,Owner=p.Seat.id,Original=p.Seat.id,Born=Time,Spec=p.Seat.spec,Position=p.Body.position+Vec(V3.Forward(p.Input.Yaw))*.7f,Velocity=dir*24,Damage=18*(float)p.Stats["damage"],Impulse=180*(float)p.Stats["impulse"],Life=1.25f,Travel=30,Radius=.4f,Direct=false,ChipImpact=true,EffectsUsed=true,Mods=new Dictionary<string,int>{{"sword-beam",1}}});}
            return scale;
        }
        private float ChipFieldSpeed(NativePilot p)
        {float speed=1;foreach(var f in ChipFields)if(CanHit(f.Owner,p)&&Vector3.Distance(f.Position,p.Body.position)<f.Radius){if(f.Kind=="frost")speed=Math.Min(speed,.45f);if(f.Kind=="time")speed=Math.Min(speed,.6f);}return speed;}
        private void StepChipFields()
        {
            const float dt=1f/60;
            foreach(var f in ChipFields.ToArray())
            {
                if(f.Attached!=null&&f.Attached.Hp>0)f.Position=f.Attached.Body.position;else if(f.Minion?.Hp>0)f.Position=f.Minion.Body.position;else if(f.Prop!=null)f.Position=f.Prop.transform.position;
                bool mine=f.Kind=="mine",trigger=mine&&Time>=f.Arm&&Pilots.Any(p=>p.Hp>0&&CanHit(f.Owner,p)&&Vector3.Distance(p.Body.position,f.Position)<f.Radius&&MeleeVisible(f.Position,p.Body.position,f.Owner,p.Collider));
                if(Time>=f.Until||trigger){ChipFields.Remove(f);if(trigger||f.Kind=="bomb"||f.Kind=="implosion")ChipBlast(f.Owner,f.Position+Vector3.up*.15f,f.Radius,f.Damage,f.Kind=="implosion"?0:1100,effect:f.Effect??f.Kind);if(f.Kind=="implosion")Vacuum(f.Owner,f.Position+Vector3.up*.15f,1300);continue;}
                if(mine||f.Kind=="bomb"||f.Kind=="implosion")continue;
                foreach(var p in Pilots){Vector3 d=p.Body.position-f.Position;if(p.Hp<=0||d.magnitude>f.Radius)continue;if(f.Kind=="heal"){if(p==f.Owner)Heal(p,f.Damage*dt);continue;}if(!CanHit(f.Owner,p)||!MeleeVisible(f.Position+Vector3.up*.2f,p.Body.position,f.Owner,p.Collider))continue;if(f.Damage>0)Damage(p,f.Owner,f.Damage*dt,Vector3.zero,0,false,direct:false,primary:false,damageKind:"field");if(!SirenArmor(p)&&(f.Kind=="black-hole"||f.Kind=="repulsion"))p.Knockback+=d.normalized*(f.Kind=="black-hole"?-35:65)*dt*(float)p.Stats["knockbackTaken"];}
                foreach(var m in Minions.ToArray())if(f.Damage>0&&f.Kind!="heal"&&CanHit(f.Owner,m)&&Vector3.Distance(m.Body.position,f.Position)<f.Radius)HitMinion(m,f.Owner,f.Damage*dt,Vector3.zero,0,kind:"field");
                if(f.Kind=="black-hole"||f.Kind=="repulsion"||f.Kind=="fire"||f.Kind=="acid")foreach(var prop in Props.ToArray())
                {if(prop==null||prop.Indestructible||Vector3.Distance(prop.transform.position,f.Position)>f.Radius||!MeleeVisible(f.Position+Vector3.up*.2f,prop.transform.position,f.Owner,prop.GetComponent<Collider>()))continue;Vector3 d=prop.transform.position-f.Position;if(f.Kind=="fire"||f.Kind=="acid")DamageProp(prop,f.Damage*dt,Vector3.zero,prop.transform.position,f.Owner);else if(prop.Body!=null&&!prop.Body.isKinematic)prop.Body.AddForce(d.normalized*(f.Kind=="black-hole"?-1:1)*prop.Body.mass*20*dt,ForceMode.Impulse);}
            }
        }
        private void SpawnChipShots(NativePilot p,List<NativeBullet> shots)
        {
            foreach(var s in shots)
            {
                if(s.Pong)continue;if(Has(p,"bowling-ball")){s.Velocity*=.45f;s.Radius*=2;s.Impulse*=3;s.Bounces+=4;}if(Has(p,"elevator-shot")){s.Gravity=-Math.Max(8,s.Gravity);s.Radius*=1.5f;}
                if(Has(p,"stop-go"))s.Life=6;if(Has(p,"accelerating-shot"))s.Life=5;s.BaseVelocity=s.Velocity;if(Has(p,"accelerating-shot")){s.Velocity*=.25f;s.MotionScale=.25f;}
            }
            var basis=shots.FirstOrDefault();if(basis==null||basis.Pong)return;
            void Extra(float angle,float damage,float side=0,bool satellite=false)
            {
                if(ProjectileCount>=100)return;var b=CopyBullet(basis);b.Direct=false;b.WeaponFx=true;b.ChipImpact=false;b.EffectsUsed=true;b.Damage*=damage;b.Velocity=Quaternion.Euler(0,angle,0)*basis.Velocity;b.Boomerang=false;b.Mods.Clear();
                if(side!=0){b.Position+=Quaternion.Euler(0,angle+90,0)*basis.Velocity.normalized*side*.35f;b.Mods["corkscrew"]=(int)side;}
                if(satellite){float a=Time*4+(side>0?0:Mathf.PI);Vector3 center=p.Body.position+Vector3.up*.55f;b.Position=center+new Vector3(Mathf.Sin(a)*.75f,.25f*Mathf.Sin(a*2),Mathf.Cos(a)*.75f);if(!MeleeVisible(center,b.Position,p,null))return;b.Velocity=b.Velocity.normalized*Math.Max(18,b.Velocity.magnitude*.8f);b.Homing=150;b.Gravity=0;b.Bounces=b.Pierces=0;b.Mods.Clear();b.Mods["orbitals"]=1;}
                Bullets.Add(b);
            }
            if(Has(p,"backfire"))Extra(180,.4f);if(Has(p,"crossfire"))for(int i=1;i<4;i++)Extra(i*90,.35f);if(Has(p,"helix")){Extra(-.1f*Mathf.Rad2Deg,.45f,-1);Extra(.1f*Mathf.Rad2Deg,.45f,1);}if(Has(p,"orbitals")){Extra(0,.4f,-1,true);Extra(0,.4f,1,true);}
        }
        private float ChipShotMotion(NativeBullet s,NativePilot owner,float dt)
        {
            if(s.Pong)return 1;float age=Time-s.Born;
            if(Has(s,"stop-go")&&s.Direct&&s.Reflections==0){if(age>=.25f&&age<.55f){s.Life-=dt;return 0;}if(age>=.55f&&!s.Relaunched){s.Relaunched=true;Vector3 aim=owner.Input.AimTarget.Length>.01f?Vec(owner.Input.AimTarget):owner.Body.position+Vec(GunRules.Direction(owner.Input.Yaw,owner.Input.Pitch))*60;s.Velocity=(aim-s.Position).normalized*Math.Min(160,s.BaseVelocity.magnitude*2);s.Gravity=0;s.Damage*=1.5f;s.Hit.Clear();}}
            if(Has(s,"accelerating-shot")){float scale=.25f+Math.Min(1,age)*1.75f;s.Velocity*=scale/s.MotionScale;s.MotionScale=scale;}
            if(Has(s,"corkscrew")){float a=.1f*s.Mods["corkscrew"]*Mathf.Sin(age*25);s.Velocity=Quaternion.Euler(0,a*Mathf.Rad2Deg,0)*s.Velocity;s.Velocity.y+=Mathf.Cos(age*25)*30*dt;}
            float speed=1;foreach(var f in ChipFields)if(CanHit(f.Owner,owner)&&Vector3.Distance(s.Position,f.Position)<f.Radius){if(f.Kind=="time")speed=.2f;if(f.Kind=="black-hole")s.Velocity+=(f.Position-s.Position).normalized*45*dt;}return speed;
        }
    }
}
