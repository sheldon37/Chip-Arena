using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // First native authority milestone. Unity owns rigid bodies and contact queries; no Node/Rapier process.
    // Conversion coverage is explicit: unsupported mechanic Chips never enter the playable draft.
    public sealed class NativePilotBody : MonoBehaviour { public NativePilot Pilot; }
    public sealed class NativePropBody : MonoBehaviour
    {
        public ContentBlock Definition;public float Health;public bool Indestructible;public Rigidbody Body;
    }
    public sealed class NativePilot
    {
        public NativeSeat Seat;public ContentWeapon Weapon;public ChipStats Stats;public Rigidbody Body;public CapsuleCollider Collider;
        public Actor Pose=new Actor();public InputFrame Input;public float Hp,Mana,Stamina,Yaw,Pitch;
        public int Ammo,Combo=-1,AimShots;public float Ready,RegenAt,ReloadAt,ReloadEnd,BlockEnd,BlockReady,RollAt=-100,RollEnd,RollReady,StunEnd,StaggerImmune,CastAt,SlowEnd,SoakedAt=-100;
        public float AttackAt=-100,HitAt,AttackEnd,LastAttack=-100,LastShot=-100,AimAt=-100,QReady,EReady;
        public Vector3 Knockback,RollDirection;public RollValues Roll;public bool HitPending,Aiming,SprintExhausted,Grounded;
        public readonly Dictionary<string,NativeDot> Dots=new Dictionary<string,NativeDot>();
    }
    public sealed class NativeDot { public int Source;public double Dps,Next,Until; }
    public sealed class NativeBullet
    {
        public int Id,Owner,Reflections;public Vector3 Position,Velocity;public float Damage,Gravity,Radius,Life=3,Travel=160,Impulse;
        public string Spec;public HashSet<int> Hit=new HashSet<int>();
    }
    public sealed class NativeArena : IDisposable
    {
        public const int ActorLayer=8,PropLayer=9;
        public const int WorldMask=1<<PropLayer,ShotMask=(1<<PropLayer)|(1<<ActorLayer);
        public readonly ConversionContent Content;public readonly NativeMatch Match;
        public readonly List<NativePilot> Pilots=new List<NativePilot>();public readonly List<NativeBullet> Bullets=new List<NativeBullet>();
        public readonly List<NativePropBody> Props=new List<NativePropBody>();
        public readonly List<ArenaEvent> Events=new List<ArenaEvent>();
        public readonly string MapId;public bool FriendlyFire,StationaryBots;
        public int Generation {get;private set;}
        private GameObject root;private int nextBullet=1,nextEvent=1;private readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        private readonly Dictionary<string,PhysicsMaterial> surfaces=new Dictionary<string,PhysicsMaterial>();
        private readonly Dictionary<string,GameObject> hazardViews=new Dictionary<string,GameObject>();
        private readonly Dictionary<string,float> hazardHits=new Dictionary<string,float>();
        public GameObject Root=>root;
        private readonly System.Random random=new System.Random(12345);private readonly RaycastHit[] rayHits=new RaycastHit[256];
        public float Time=>(float)Match.Time;
        public static readonly HashSet<string> SupportedStats=new HashSet<string>{"hp","speed","attackRate","damage","damageTaken","knockbackTaken","dashCooldown","maxMana","maxStamina","ammoCapacity","reloadSpeed","resourceCost","manaRegen","staminaRegen","projectileGravity","projectileSpeed","projectileSize","rollDistance","rollSpeed","rollCost","rollWindow","blockCooldown","blockWindow","reflectDamage","reflectSpeed","reach","meleeArc","impulse","jumpPower","playerGravity","lifeSteal","burn","poison","regeneration","thorns"};
        public static bool ImplementedChip(ContentChip c)=>string.IsNullOrEmpty(c.mechanic)&&c.effects.Length>0&&c.effects.All(e=>SupportedStats.Contains(e.stat));
        public NativeArena(ConversionContent content,NativeMatch match,string map)
        {Content=content;Match=match;MapId=map;Match.ResetRound=Reset;Match.DraftFilter=ImplementedChip;Physics.gravity=new Vector3(0,-23,0);}
        public static Color ColorOf(int value)=>new Color(((value>>16)&255)/255f,((value>>8)&255)/255f,(value&255)/255f);
        public static Vector3 Vec(V3 v)=>new Vector3(v.x,v.y,v.z);
        public static V3 Vec(Vector3 v)=>new V3(v.x,v.y,v.z);
        private void DestroyScene()
        {
            if(root!=null){foreach(var c in root.GetComponentsInChildren<Collider>())c.enabled=false;CapturedAnimation.Release(root);}
            Pilots.Clear();Props.Clear();Bullets.Clear();Events.Clear();hazardViews.Clear();hazardHits.Clear();
        }
        public void Reset()
        {
            Generation++;DestroyScene();root=new GameObject("Native match authority");
            var map=Content.Map(MapId);var shader=Resources.Load<Shader>("PrototypeLit");
            foreach(var definition in Content.materials)if(!materials.ContainsKey(definition.id))
            {materials[definition.id]=new Material(shader){color=ColorOf(definition.id=="foundation"?map.floor:definition.color)};surfaces[definition.id]=new PhysicsMaterial(definition.id){dynamicFriction=definition.friction,staticFriction=definition.friction,bounciness=0};}
            if(!surfaces.ContainsKey("pilot"))surfaces["pilot"]=new PhysicsMaterial("Pilot friction"){dynamicFriction=0,staticFriction=0,bounciness=0,frictionCombine=PhysicsMaterialCombine.Minimum};
            foreach(var block in map.blocks)
            {
                var def=Content.Material(block.material);var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=block.id;obj.layer=PropLayer;obj.transform.SetParent(root.transform);
                obj.transform.SetPositionAndRotation(Vec(block.pos),new Quaternion(block.rotation.x,block.rotation.y,block.rotation.z,block.rotation.w));obj.transform.localScale=Vec(block.size);
                obj.GetComponent<Renderer>().sharedMaterial=materials[block.material];obj.GetComponent<Collider>().sharedMaterial=surfaces[block.material];var prop=obj.AddComponent<NativePropBody>();prop.Definition=block;prop.Health=def.durability;prop.Indestructible=def.indestructible;
                if(!def.indestructible)
                {prop.Body=obj.AddComponent<Rigidbody>();prop.Body.mass=block.mass>0?block.mass:def.mass;prop.Body.isKinematic=!block.dynamic;prop.Body.collisionDetectionMode=block.dynamic?CollisionDetectionMode.ContinuousDynamic:CollisionDetectionMode.Discrete;}
                Props.Add(prop);
            }
            for(int i=0;i<Match.Seats.Count;i++)
            {
                var seat=Match.Seats[i];var stats=NativeContentRules.Stats(Content,seat.spec,seat.chips);var weapon=Content.Weapon(seat.spec);
                var obj=new GameObject(seat.name);obj.layer=ActorLayer;obj.transform.SetParent(root.transform);obj.transform.position=Vec(Content.spawns[i]);
                var capsule=obj.AddComponent<CapsuleCollider>();capsule.radius=.42f;capsule.height=1.70f;capsule.sharedMaterial=surfaces["pilot"];
                var body=obj.AddComponent<Rigidbody>();body.mass=65;body.constraints=RigidbodyConstraints.FreezeRotation;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.interpolation=RigidbodyInterpolation.Interpolate;
                var p=new NativePilot{Seat=seat,Weapon=weapon,Stats=stats,Body=body,Collider=capsule,Hp=(float)stats["hp"],Mana=(float)stats["maxMana"],Stamina=(float)stats["maxStamina"],Ammo=(int)Math.Ceiling(weapon.ammo*stats["ammoCapacity"]),Yaw=Mathf.Atan2(-obj.transform.position.x,-obj.transform.position.z)*Mathf.Rad2Deg};
                p.Pose.Id=seat.id;p.Pose.Bot=seat.bot;p.Pose.Weapon=seat.spec=="pistol"?WeaponId.Pistol:seat.spec=="rifle"?WeaponId.Rifle:seat.spec=="machinegun"?WeaponId.MachineGun:WeaponId.Sword;
                p.Pose.VisualSet=seat.spec=="daggers"?1:seat.spec=="greatsword"?2:seat.spec=="shield"?3:0;
                p.Pose.MagicVisual=seat.spec=="fire"?1:seat.spec=="water"?2:seat.spec=="lightning"?3:seat.spec=="darkness"?4:0;
                obj.AddComponent<NativePilotBody>().Pilot=p;Pilots.Add(p);
            }
            Physics.SyncTransforms();
        }
        public NativePilot Local=>Pilots.Find(p=>!p.Seat.bot);
        public void Step(InputFrame input)
        {
            Match.Tick(1d/60);StepHazards();if(Match.Phase!=NativePhase.Fight){foreach(var p in Pilots)if(p.Body!=null&&!p.Body.isKinematic)p.Body.linearVelocity=Vector3.zero;return;}
            if(Events.Count>128)Events.RemoveRange(0,Events.Count-128);
            // Establish every defense before resolving attacks.
            foreach(var p in Pilots)
            {
                if(p.Hp<=0)continue;p.Input=p.Seat.bot?Bot(p):input;p.Input=p.Input.Sanitized();
                p.Grounded=Physics.SphereCast(p.Body.position+Vector3.down*.4f,.39f,Vector3.down,out _,.17f,WorldMask,QueryTriggerInteraction.Ignore);
                if(p.Input.Block&&Time>=p.BlockReady&&Time>=p.RollEnd&&Time>=p.StunEnd)
                {p.BlockEnd=Time+(float)Math.Min(.65,.3+p.Stats["blockWindow"]);p.BlockReady=Time+(float)Math.Max(1.5,4*p.Stats["blockCooldown"]);p.HitPending=false;p.AttackEnd=0;}
                if(p.Input.Roll&&p.Grounded&&Time>=p.RollReady&&Time>=p.RollEnd&&Time>=p.StunEnd&&p.ReloadEnd<=Time)
                {
                    var roll=NativeContentRules.Roll(Content,p.Seat.spec,p.Stats);
                    if(p.Stamina>=roll.cost){p.Roll=roll;p.RollAt=Time;p.RollEnd=Time+(float)roll.duration;p.RollReady=Time+(float)roll.cooldown;p.Stamina-=(float)roll.cost;p.RegenAt=p.RollEnd+.45f;p.HitPending=false;p.AttackEnd=p.BlockEnd=0;p.RollDirection=new Vector3(p.Input.X,0,p.Input.Z).normalized;if(p.RollDirection.sqrMagnitude<.1f)p.RollDirection=Quaternion.Euler(0,p.Yaw,0)*Vector3.forward;}
                }
            }
            foreach(var p in Pilots)if(p.Hp>0){AdvanceDots(p);if(p.Hp>0)StepPilot(p);}
            StepBullets();
            foreach(var p in Pilots)if(p.Hp>0&&p.Body.position.y<-12)Damage(p,null,p.Hp+1,Vector3.zero,0,false,true);
            Match.Resolve();
        }
        private void StepPilot(NativePilot p)
        {
            var i=p.Input;float dt=1f/60;bool rolling=Time<p.RollEnd,locked=Time<p.AttackEnd||Time<p.StunEnd,swimming=NativeHazards.InLava(MapId,Vec(p.Body.position));
            if(!rolling&&!locked)p.Yaw=i.Yaw;p.Pitch=i.Pitch;
            if(p.ReloadEnd>0&&Time>=p.ReloadEnd){p.Ammo=Magazine(p);p.ReloadEnd=0;Emit(EventKind.ReloadEnd,p,0,p.Body.position,0);}
            bool aim=p.Weapon.@class=="ranged"&&i.Aim&&!rolling&&p.ReloadEnd==0&&Time>=p.BlockEnd&&Time>=p.StunEnd;
            if(aim&&!p.Aiming)p.AimAt=Time;p.Aiming=aim;
            if(i.Reload)Reload(p);
            if(p.CastAt>0&&Time>=p.CastAt){p.CastAt=0;Fire(p);}
            if(p.HitPending&&Time>=p.HitAt){p.HitPending=false;Melee(p);}
            if(!rolling&&!locked&&Time>=p.Ready&&p.ReloadEnd==0&&Time>=p.BlockEnd&&i.Shoot)
            {
                if(p.Weapon.@class=="melee")
                {
                    float cost=(float)(p.Weapon.cost*p.Stats["resourceCost"]);
                    if(p.Stamina>=cost){p.Stamina-=cost;p.Combo=Time-p.LastAttack<2.4f?p.Combo+1:0;p.LastAttack=p.AttackAt=Time;p.HitAt=Time+(float)(p.Weapon.windup/p.Stats["attackRate"]);p.HitPending=true;
                        float active=(float)(.16/p.Stats["attackRate"]),recovery=p.Seat.spec=="greatsword"?.5f:p.Seat.spec=="daggers"?.15f:.3f;
                        p.AttackEnd=p.Ready=Time+Math.Max((float)(p.Weapon.interval/p.Stats["attackRate"]),p.HitAt-Time+active+(float)(recovery/p.Stats["attackRate"]));p.RegenAt=p.AttackEnd+.45f;}
                }
                else if(p.Weapon.@class=="ranged"&&p.Ammo<=0)Reload(p);
                else if(p.Weapon.@class=="ranged"||p.Mana>=p.Weapon.cost*p.Stats["resourceCost"])
                {
                    if(p.Weapon.@class=="ranged"){p.Ammo--;Fire(p);}
                    else {p.Mana-=(float)(p.Weapon.cost*p.Stats["resourceCost"]);p.CastAt=Time+(float)((p.Seat.spec=="fire"?.16:p.Seat.spec=="water"?.12:p.Seat.spec=="lightning"?.065:.22)/p.Stats["attackRate"]);p.Ready=Time+(float)(p.Weapon.interval/p.Stats["attackRate"]);}
                }
            }
            var move=new Vector3(i.X,0,i.Z);if(!i.Sprint)p.SprintExhausted=false;
            bool sprint=i.Sprint&&!p.SprintExhausted&&!locked&&!rolling&&!i.Shoot&&!aim&&p.ReloadEnd==0&&Time>=p.BlockEnd&&p.Stamina>0&&move.magnitude>.5f&&Vector3.Dot(move.normalized,Quaternion.Euler(0,p.Yaw,0)*Vector3.forward)>=.6f;
            if(sprint){p.Stamina=Math.Max(0,p.Stamina-12*dt);p.RegenAt=Time+.45f;if(p.Stamina<=0)p.SprintExhausted=true;}
            if(Time>=p.RegenAt&&!sprint)p.Stamina=Math.Min((float)p.Stats["maxStamina"],p.Stamina+(float)(24*p.Stats["staminaRegen"]*dt));
            p.Mana=Math.Min((float)p.Stats["maxMana"],p.Mana+(float)(12*p.Stats["manaRegen"]*dt));
            p.Hp=Math.Min((float)p.Stats["hp"],p.Hp+(float)(p.Stats["regeneration"]*dt));
            float speed=(float)(6.8*p.Stats["speed"])*(sprint?1.35f:1)*(Time<p.BlockEnd?.45f:1)*(aim?(float)Content.Ads(p.Seat.spec).move:1)*(Time<p.SlowEnd?.8f:1)*(swimming?.6f:1);
            Vector3 velocity=p.Body.linearVelocity;
            if(rolling){float duration=p.RollEnd-p.RollAt,u=(Time-p.RollAt)/duration,q=(Time+dt-p.RollAt)/duration;var roll=p.RollDirection*((float)p.Roll.distance*(MathUtil.Smooth(q)-MathUtil.Smooth(u))/dt);velocity.x=roll.x;velocity.z=roll.z;}
            else {velocity.x=Mathf.MoveTowards(velocity.x,locked?0:move.x*speed,35*dt);velocity.z=Mathf.MoveTowards(velocity.z,locked?0:move.z*speed,35*dt);}
            if(swimming&&velocity.y<=2)velocity.y+=Mathf.Clamp((-.95f-p.Body.position.y)*12-velocity.y*4,-35,35)*dt;
            if(i.Jump&&(p.Grounded||swimming&&velocity.y<2)&&!rolling&&!locked)velocity.y=(float)((swimming?10:8.5)*p.Stats["jumpPower"]);
            p.Body.useGravity=!swimming||velocity.y>2;
            if(p.Body.useGravity)velocity.y-=(float)(23*(p.Stats["playerGravity"]-1)*dt);
            p.Knockback*=Mathf.Exp(-3.1f*dt);p.Body.linearVelocity=velocity+p.Knockback*dt*3.1f;
            if(p.Body.position.y>19&&velocity.y>0)p.Body.linearVelocity=new Vector3(velocity.x,-Math.Abs(velocity.y)*.7f,velocity.z);
            p.Pose.Sprinting=sprint;
        }
        private int Magazine(NativePilot p)=>(int)Math.Ceiling(p.Weapon.ammo*p.Stats["ammoCapacity"]);
        private void Reload(NativePilot p)
        {if(p.Weapon.@class!="ranged"||p.ReloadEnd>0||p.Ammo>=Magazine(p)||Time<p.RollEnd)return;p.ReloadAt=Time;p.ReloadEnd=Time+(float)(p.Weapon.reload*p.Stats["reloadSpeed"]);p.Aiming=false;Emit(EventKind.ReloadStart,p,0,p.Body.position,0);}
        private void Fire(NativePilot p)
        {
            if(Bullets.Count>=100)return;var w=p.Weapon;Vector3 start=p.Body.position+new Vector3(0,.3f,0)+Quaternion.Euler(0,p.Yaw,0)*new Vector3(.2f,0,.48f);
            Vector3 offset=p.Input.AimTarget.Length>.01f?Vec(p.Input.AimTarget)-start:Vec(GunRules.Direction(p.Yaw,p.Pitch))*(float)w.range;
            float yaw=Mathf.Atan2(offset.x,offset.z)*Mathf.Rad2Deg,distance=new Vector2(offset.x,offset.z).magnitude;
            float pitch=p.Aiming?GunRules.BallisticPitch(distance,offset.y,(float)(w.speed*p.Stats["projectileSpeed"]),(float)(w.gravity*p.Stats["projectileGravity"])):Mathf.Atan2(offset.y,distance)*Mathf.Rad2Deg;
            double spread=w.spread;var ads=Content.Ads(p.Seat.spec);if(p.Aiming){spread*=1+(ads.spread-1)*Mathf.Clamp01((Time-p.AimAt)/(float)ads.settle);if(ads.bloom>0&&Time-p.LastShot<=.25f)spread=Math.Min(w.spread,spread+ads.bloom*p.AimShots);}
            Vector3 dir=(Vec(GunRules.Direction(yaw,pitch))+new Vector3((float)(random.NextDouble()-.5),(float)(random.NextDouble()-.5),(float)(random.NextDouble()-.5))*(float)spread).normalized;
            p.AimShots=Time-p.LastShot>.25f?1:p.AimShots+1;p.LastShot=Time;if(w.@class!="magic")p.Ready=Time+(float)(w.interval/p.Stats["attackRate"])*(p.Aiming&&ads.interval>0?(float)ads.interval:1);p.Pose.ShotsFired++;
            Emit(EventKind.Shot,p,0,start,(int)p.Pose.Weapon);
            if(Physics.Linecast(p.Body.position+Vector3.up*.3f,start,out var obstruction,WorldMask)){DamageProp(obstruction.collider.GetComponent<NativePropBody>(),(float)(w.damage*p.Stats["damage"]),dir*(float)w.impulse,obstruction.point);return;}
            Bullets.Add(new NativeBullet{Id=nextBullet++,Owner=p.Seat.id,Spec=p.Seat.spec,Position=start,Velocity=dir*(float)(w.speed*p.Stats["projectileSpeed"]),Damage=(float)(w.damage*p.Stats["damage"]),Gravity=(float)(w.gravity*p.Stats["projectileGravity"]),Radius=(float)((w.shotRadius>0?w.shotRadius:w.@class=="magic"?.14:.045)*p.Stats["projectileSize"]),Impulse=(float)(w.impulse*p.Stats["impulse"])});
        }
        private bool Enemy(NativePilot a,NativePilot b)=>a!=b&&(FriendlyFire||!Match.SameSide(a.Seat,b.Seat));
        private void Melee(NativePilot p)
        {
            Vector3 origin=p.Body.position,forward=Quaternion.Euler(0,p.Yaw,0)*Vector3.forward;float reach=(float)(p.Weapon.range*p.Stats["reach"]),arc=(float)(p.Weapon.arc*p.Stats["meleeArc"]);
            foreach(var target in Pilots)
            {
                Vector3 d=target.Body.position-origin;
                if(target.Hp<=0||!Enemy(p,target)||d.magnitude>reach+.42f||Vector3.Dot(new Vector3(d.x,0,d.z).normalized,forward)<Mathf.Cos(arc*.5f)||Physics.Linecast(origin,target.Body.position,WorldMask))continue;
                Damage(target,p,(float)(p.Weapon.damage*p.Stats["damage"]),d.normalized,(float)(p.Weapon.impulse*p.Stats["impulse"]),false);
            }
            foreach(var prop in Props.ToArray())if(prop!=null&&!prop.Indestructible&&(prop.transform.position-origin).magnitude<reach+prop.transform.localScale.magnitude*.5f&&Vector3.Dot((prop.transform.position-origin).normalized,forward)>.5f)DamageProp(prop,(float)(p.Weapon.damage*p.Stats["damage"]),forward*(float)(p.Weapon.impulse*p.Stats["impulse"]),prop.transform.position);
        }
        private void StepBullets()
        {
            const float dt=1f/60;
            for(int i=Bullets.Count-1;i>=0;i--)
            {
                var b=Bullets[i];var owner=Pilots.Find(p=>p.Seat.id==b.Owner);var delta=b.Velocity*dt+Vector3.down*(.5f*b.Gravity*dt*dt);b.Velocity.y-=b.Gravity*dt;b.Life-=dt;b.Travel-=delta.magnitude;
                int n=Physics.SphereCastNonAlloc(b.Position,b.Radius,delta.normalized,rayHits,delta.magnitude,ShotMask,QueryTriggerInteraction.Ignore);RaycastHit? nearest=null;
                for(int k=0;k<n;k++){var body=rayHits[k].collider.GetComponent<NativePilotBody>();if(body!=null&&(body.Pilot.Seat.id==b.Owner||body.Pilot.Hp<=0||!Enemy(owner,body.Pilot)||Invulnerable(body.Pilot)))continue;if(!nearest.HasValue||rayHits[k].distance<nearest.Value.distance)nearest=rayHits[k];}
                bool remove=false;
                if(nearest.HasValue)
                {
                    var hit=nearest.Value;var body=hit.collider.GetComponent<NativePilotBody>();
                    if(body!=null&&Time<body.Pilot.BlockEnd&&b.Reflections<3)
                    {var target=body.Pilot;b.Owner=target.Seat.id;b.Reflections++;b.Damage=Math.Min(100,b.Damage*(float)target.Stats["reflectDamage"]);b.Velocity=-b.Velocity*(float)target.Stats["reflectSpeed"];b.Velocity=Vector3.ClampMagnitude(b.Velocity,120);b.Position=hit.point+b.Velocity.normalized*.9f;Emit(EventKind.Block,target,owner.Seat.id,hit.point,0);}
                    else
                    {
                        if(body!=null){bool head=hit.point.y>body.Pilot.Body.position.y+.40f;Damage(body.Pilot,owner,b.Damage*(head?1.5f:1),b.Velocity.normalized,b.Impulse,head);}
                        else DamageProp(hit.collider.GetComponent<NativePropBody>(),b.Damage,b.Velocity.normalized*b.Impulse,hit.point);
                        var weapon=Content.Weapon(b.Spec);if(weapon.blast>0)Blast(owner,hit.point,b.Radius>.2f?3:(float)weapon.blast,b.Damage*.38f,b.Impulse);
                        remove=true;
                    }
                }
                else b.Position+=delta;
                if(remove||b.Life<=0||b.Travel<=0||b.Position.y<-15)Bullets.RemoveAt(i);
            }
        }
        private bool Invulnerable(NativePilot p)=>p.Roll!=null&&Time>=p.RollAt+p.Roll.iframeStart&&Time<p.RollAt+p.Roll.iframeStart+p.Roll.iframeSeconds;
        private void Damage(NativePilot target,NativePilot source,float amount,Vector3 direction,float impulse,bool head,bool forced=false,bool direct=true)
        {
            if(target.Hp<=0||!forced&&direct&&Invulnerable(target))return;
            if(source!=null&&!forced&&direct&&Time<target.BlockEnd){Emit(EventKind.Block,target,source.Seat.id,target.Body.position,0);if(source.Weapon.@class=="melee"){source.StunEnd=Time+.55f;source.AttackEnd=0;source.HitPending=false;}return;}
            amount=forced?amount:amount*(float)target.Stats["damageTaken"];float actual=Math.Min(amount,target.Hp);target.Hp=Math.Max(0,target.Hp-amount);target.Pose.FlashUntil=Time+.14f;
            target.Knockback+=direction*(impulse/65f)*(float)target.Stats["knockbackTaken"];
            if(direct&&Time>=target.StaggerImmune){target.StunEnd=Time+.25f;target.StaggerImmune=target.StunEnd+.75f;target.HitPending=false;target.AttackEnd=0;}
            if(source!=null&&source.Hp>0&&direct&&actual>0)
            {
                source.Hp=Math.Min((float)source.Stats["hp"],source.Hp+actual*(float)(source.Stats["lifeSteal"]+source.Weapon.drain));
                RefreshDot(target,source,"fire",source.Weapon.burn+source.Stats["burn"],2);RefreshDot(target,source,"poison",source.Stats["poison"],4);
                if(target.Stats["thorns"]>0)Damage(source,target,actual*(float)target.Stats["thorns"],Vector3.zero,0,false,false,false);
                if(source.Seat.spec=="water"&&Time>=target.SoakedAt+1.5f){target.SoakedAt=Time;target.SlowEnd=Time+.8f;}
                if(source.Weapon.chain>0)
                {var next=Pilots.Where(p=>p!=target&&p.Hp>0&&Enemy(source,p)&&(p.Body.position-target.Body.position).magnitude<4&&!Physics.Linecast(target.Body.position,p.Body.position,WorldMask)).OrderBy(p=>(p.Body.position-target.Body.position).sqrMagnitude).FirstOrDefault();if(next!=null)Damage(next,source,(float)source.Weapon.chain,Vector3.zero,0,false,false,false);}
            }
            Emit(head?EventKind.Headshot:EventKind.Hit,source,target.Seat.id,target.Body.position,actual);
            if(target.Hp<=0){target.Seat.alive=false;target.Collider.enabled=false;target.Body.isKinematic=true;Emit(EventKind.Death,source,target.Seat.id,target.Body.position,0);}
        }
        private void RefreshDot(NativePilot target,NativePilot source,string kind,double dps,double duration)
        {
            if(dps<=0)return;string key=source.Seat.id+":"+kind;target.Dots.TryGetValue(key,out var old);
            target.Dots[key]=new NativeDot{Source=source.Seat.id,Dps=Math.Max(dps,old!=null&&old.Until>=Match.Time?old.Dps:0),Next=old!=null&&old.Until>=Match.Time?old.Next:Match.Time+.25,Until=Match.Time+duration};
        }
        private void AdvanceDots(NativePilot p)
        {foreach(var pair in p.Dots.ToArray()){var dot=pair.Value;while(dot.Next<=Match.Time+1e-8&&dot.Next<=dot.Until+1e-8){Damage(p,Pilots.Find(x=>x.Seat.id==dot.Source),(float)(dot.Dps*.25),Vector3.zero,0,false,false,false);dot.Next+=.25;}if(Match.Time>dot.Until+1e-8)p.Dots.Remove(pair.Key);}}
        private void Blast(NativePilot source,Vector3 position,float radius,float damage,float impulse)
        {
            foreach(var p in Pilots){Vector3 delta=p.Body.position-position;if(p.Hp<=0||!Enemy(source,p)||delta.magnitude>radius||Physics.Linecast(position+Vector3.up*.05f,p.Body.position,WorldMask))continue;float scale=1-delta.magnitude/radius;Damage(p,source,damage*scale,delta.normalized,impulse*scale,false,false,false);}
            foreach(var prop in Props.ToArray())if(prop!=null&&(prop.transform.position-position).magnitude<radius)DamageProp(prop,damage,(prop.transform.position-position).normalized*impulse,position);
        }
        private void StepHazards()
        {
            foreach(var h in NativeHazards.At(MapId,Match.Time))
            {
                if(!hazardViews.TryGetValue(h.id,out var visual))
                {
                    visual=GameObject.CreatePrimitive(h.kind=="saw"?PrimitiveType.Cylinder:PrimitiveType.Cube);visual.name=h.id;visual.transform.SetParent(root.transform);visual.GetComponent<Collider>().enabled=false;CapturedAnimation.Release(visual.GetComponent<Collider>());
                    if(!materials.ContainsKey("hazard"))materials["hazard"]=new Material(Resources.Load<Shader>("PrototypeLit")){color=new Color(1,.24f,.12f)};
                    visual.GetComponent<Renderer>().sharedMaterial=materials["hazard"];hazardViews[h.id]=visual;
                }
                visual.transform.position=new Vector3(h.x,h.y+(h.kind=="spikes"?h.extension*.5f:.015f),h.z);
                visual.transform.localScale=h.kind=="saw"?new Vector3(h.sx,.09f,h.sx):new Vector3(h.sx,h.kind=="spikes"?Math.Max(.03f,h.extension):.03f,h.sz);
                if(h.kind=="saw")visual.transform.rotation=Quaternion.Euler(90,0,Time*700);
                visual.SetActive(h.kind!="spikes"||h.active||h.warning);
                if(!h.active||Match.Phase!=NativePhase.Fight||Match.Time-Match.FightAt<2)continue;
                foreach(var p in Pilots)
                {
                    if(p.Hp<=0)continue;var pos=p.Body.position;string key=h.id+":"+p.Seat.id;
                    if(hazardHits.TryGetValue(key,out float until)&&Time<until)continue;
                    bool lava=h.kind=="lava",touch=lava?NativeHazards.InLavaChannel(h,Vec(pos)):h.kind=="saw"?Math.Abs(pos.z-h.z)<.62&&Math.Sqrt((pos.x-h.x)*(pos.x-h.x)+Math.Pow(Math.Max(0,Math.Abs(pos.y-h.y)-.43),2))<1.62:Math.Abs(pos.x-h.x)<h.sx/2+.3&&Math.Abs(pos.z-h.z)<h.sz/2+.3&&pos.y-.85<1.1*h.extension&&pos.y>-.5;
                    if(!touch)continue;hazardHits[key]=Time+(lava?.25f:.65f);Damage(p,null,lava?8:h.kind=="saw"?22:25,lava?Vector3.zero:new Vector3(Mathf.Sign(pos.x-h.x)*.4f,.35f,Mathf.Sign(pos.z-h.z)),lava?0:150,false);
                }
            }
        }
        private void DamageProp(NativePropBody prop,float amount,Vector3 impulse,Vector3 point)
        {
            if(prop==null)return;if(prop.Body!=null&&!prop.Body.isKinematic)prop.Body.AddForceAtPosition(impulse,point,ForceMode.Impulse);
            if(prop.Indestructible)return;prop.Health-=amount;
            if(prop.Health>0)return;Emit(EventKind.Break,null,0,prop.transform.position,0);Props.Remove(prop);
            foreach(var other in Props)if(other!=null&&other.Body!=null&&other.Body.isKinematic&&other.Definition.support.Contains(prop.Definition.id)){other.Body.isKinematic=false;other.Body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;}
            prop.GetComponent<Collider>().enabled=false;CapturedAnimation.Release(prop.gameObject);
        }
        private void Emit(EventKind kind,NativePilot source,int target,Vector3 position,float amount)
        {Events.Add(new ArenaEvent{Id=nextEvent++,Kind=kind,Source=source?.Seat.id??0,Target=target,Position=Vec(position),Amount=amount});}
        private InputFrame Bot(NativePilot p)
        {
            var target=Pilots.Where(x=>x.Hp>0&&Enemy(p,x)).OrderBy(x=>(x.Body.position-p.Body.position).sqrMagnitude).FirstOrDefault();if(target==null||StationaryBots)return new InputFrame{Yaw=p.Yaw};
            Vector3 delta=target.Body.position-p.Body.position;float distance=delta.magnitude;bool melee=p.Weapon.@class=="melee",clear=!Physics.Linecast(p.Body.position,target.Body.position,WorldMask);float desired=melee?(float)p.Weapon.range:12;
            Vector3 move=distance>desired?delta.normalized:!melee&&distance<8?-delta.normalized:Vector3.zero;
            return new InputFrame{X=move.x,Z=move.z,Yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,Pitch=Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg,AimTarget=Vec(target.Body.position),Shoot=clear&&(melee?distance<desired+.3f:true),Aim=!melee,Jump=!clear&&p.Grounded&&((int)(Time*60)+p.Seat.id)%80==0,Reload=p.Ammo==0,Block=distance<3&&((int)(Time*60)+p.Seat.id*11)%137==0};
        }
        public Snapshot Snapshot()
        {
            foreach(var p in Pilots)
            {
                var a=p.Pose;a.Hp=p.Hp;a.Stamina=p.Stamina;a.Position=Vec(p.Body.position-Vector3.up*.865f);a.Velocity=Vec(p.Body.linearVelocity);a.Yaw=p.Yaw;a.AimYaw=p.Yaw;a.Pitch=p.Pitch;a.Combo=Math.Max(0,p.Combo);a.AttackAt=p.AttackAt;a.AttackUntil=p.AttackEnd;a.BlockUntil=p.BlockEnd;a.BlockReady=p.BlockReady;a.RollAt=p.RollAt;a.RollDirection=Vec(p.RollDirection);a.StunUntil=p.StunEnd;a.Aiming=p.Aiming;a.AimAt=p.AimAt;a.Ammo=p.Ammo;a.LastShot=p.LastShot;a.ReloadAt=p.ReloadAt;a.ReloadUntil=p.ReloadEnd;
                a.Grounded=p.Grounded;a.VisualWindup=(float)(p.Weapon.windup/p.Stats["attackRate"]);a.VisualActive=(float)(.16/p.Stats["attackRate"]);a.VisualDuration=p.AttackEnd-p.AttackAt;a.VisualRollDuration=p.RollEnd-p.RollAt;
            }
            return new Snapshot{Tick=(int)Math.Round(Match.Time*60),Actors=Pilots.Select(p=>p.Pose).ToArray(),Covers=Array.Empty<Cover>(),Shots=Bullets.Select(b=>new Shot{Id=b.Id,Owner=b.Owner,Position=Vec(b.Position),Velocity=Vec(b.Velocity),Weapon=Content.Weapon(b.Spec).@class=="ranged"?WeaponId.Pistol:WeaponId.Sword}).ToArray(),Events=Events.ToArray()};
        }
        public void Dispose(){DestroyScene();foreach(var m in materials.Values)CapturedAnimation.Release(m);materials.Clear();foreach(var m in surfaces.Values)CapturedAnimation.Release(m);surfaces.Clear();}
    }
}
