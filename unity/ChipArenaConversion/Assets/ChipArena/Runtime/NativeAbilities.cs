using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed class NativeAbilityCast
    { public ContentAbility Definition;public float Release;public Vector3 Point,Direction; }
    public sealed class NativeAbilityShot
    {
        public int Id;public NativePilot Owner;public ContentAbility Definition;public Vector3 Position,Velocity;
        public float Damage,Radius,Gravity,Life,Travel,MaxTravel,Fuse,NextTick;public int Reflections,RemainingPierces;
        public bool Touched,DirectHit,Resting;public readonly HashSet<int> Hit=new HashSet<int>();
    }
    public sealed class NativeAbilityZone
    {
        public NativePilot Owner,Attached;public ContentAbility Definition;public Vector3 Position;
        public string Kind;public float Radius,Damage,Until,NextTick,LockAt;
    }
    public sealed class NativeAbilityFx
    { public Vector3 Position,From;public float Radius,Until;public int Element;public bool Beam; }

    // Native versions of the eleven standard gun/magic handlers. Special Chip handlers remain filtered out.
    public sealed partial class NativeArena
    {
        public readonly List<NativeAbilityShot> AbilityShots=new List<NativeAbilityShot>();
        public readonly List<NativeAbilityZone> AbilityZones=new List<NativeAbilityZone>();
        public readonly List<NativeAbilityFx> AbilityEffects=new List<NativeAbilityFx>();
        public void CancelLocalAbilities(){if(Local!=null)CancelAbilities(Local);}
        private void CancelAbilities(NativePilot p)
        { CancelWindup(p);p.BufferedSlot=null;p.HoldAt=-1; }
        private void CancelWindup(NativePilot p)
        {
            if(p.AbilityCast!=null){p.Pose.AbilityAt=-100;p.Pose.AbilityId=null;}
            p.AbilityCast=null;
            if(p.CastAt>0){p.CastAt=0;p.AttackAt=-100;}
        }
        private bool AbilityBusy(NativePilot p)=>p.AbilityCast!=null||p.CastAt>0||Time<p.RollEnd||Time<p.AttackEnd||Time<p.StunEnd||Time<p.Ready||p.ReloadEnd>Time;
        private void StepAbilities(NativePilot p)
        {
            int max=NativeAbilityRules.Charges(p.Stats);
            if(p.ECharges<max&&Time>=p.EReady){p.ECharges++;if(p.ECharges<max)p.EReady=Time+NativeAbilityRules.Cooldown(Content.Ability(p.Seat.spec,"e"),p.Stats);}
            if(p.Input.CancelAbilities||Time<p.BlockEnd){CancelAbilities(p);return;}
            bool interrupted=Time<p.RollEnd||Time<p.StunEnd;
            if(interrupted){CancelWindup(p);p.HoldAt=-1;}
            if(p.AbilityCast!=null&&Time>=p.AbilityCast.Release)ReleaseAbility(p);
            if(p.BufferedSlot!=null)
            {if(Time-p.BufferedAt>.3f)p.BufferedSlot=null;else PressAbility(p,p.BufferedSlot,true);}
            if(p.Input.AbilityQ){p.HoldAt=-1;PressAbility(p,"q");}
            var e=Content.Ability(p.Seat.spec,"e");
            if(e==null)return;
            if(NativeAbilityRules.HoldAim(e.id)&&!p.Seat.bot)
            {
                if(p.Input.AbilityE&&!interrupted)p.HoldAt=Time;
                if(p.Input.AbilityEReleased&&p.HoldAt>=0){p.HoldAt=-1;PressAbility(p,"e");}
            }
            else if(p.Input.AbilityE)PressAbility(p,"e");
        }
        private void PressAbility(NativePilot p,string slot,bool retry=false)
        {
            var d=Content.Ability(p.Seat.spec,slot);
            float cooldown=slot=="q"?p.QReady-Time:p.ECharges>0?0:p.EReady-Time;
            var gate=NativeAbilityRules.Decide(d!=null&&d.kind!="ads"&&d.live,p.Hp>0,Time<p.BlockEnd,p.Mana,d==null?0:NativeAbilityRules.Cost(d,p.Stats),cooldown,AbilityBusy(p));
            if(gate==NativeAbilityRules.Press.Refused)
            {if(p.BufferedSlot==slot)p.BufferedSlot=null;return;}
            if(gate==NativeAbilityRules.Press.Buffered)
            {if(!retry||p.BufferedSlot==null){p.BufferedSlot=slot;p.BufferedAt=Time;}return;}
            Vector3 point=Vec(p.Input.AbilityTarget);
            if(point.sqrMagnitude<.001f)point=p.Body.position+Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch))*(float)d.range;
            point=p.Body.position+Vector3.ClampMagnitude(point-p.Body.position,(float)d.range);
            if(d.kind=="strike"||d.kind=="line"||d.kind=="zone")point=GroundPoint(point);
            Vector3 dir=Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch));
            if(d.id=="blink-strike"){dir=new Vector3(p.Input.X,0,p.Input.Z);if(dir.sqrMagnitude<.01f)dir=Quaternion.Euler(0,p.Input.Yaw,0)*Vector3.forward;point=p.Body.position+dir.normalized*(float)d.range;}
            p.AbilityCast=new NativeAbilityCast{Definition=d,Point=point,Direction=dir,Release=Time+NativeAbilityRules.CastTime(d.windup,p.Stats)};
            p.Pose.AbilityId=d.id;p.Pose.AbilityAt=Time;p.Pose.AbilityRelease=p.AbilityCast.Release;p.BufferedSlot=null;
        }
        private void ReleaseAbility(NativePilot p)
        {
            var cast=p.AbilityCast;p.AbilityCast=null;var d=cast.Definition;float cost=NativeAbilityRules.Cost(d,p.Stats);
            if(p.Hp<=0||p.Mana<cost||Time<p.BlockEnd||Time<p.StunEnd)return;
            float scale=NativeAbilityRules.Scale(p.Stats,p.Mana>=.75f*p.Stats["maxMana"]);
            p.Mana-=cost;
            if(d.slot=="q")p.QReady=Time+NativeAbilityRules.Cooldown(d,p.Stats);
            else {p.ECharges--;if(Time>=p.EReady)p.EReady=Time+NativeAbilityRules.Cooldown(d,p.Stats);}
            p.Ready=Math.Max(p.Ready,Time+NativeAbilityRules.CastTime(d.lockout,p.Stats));
            float damage=(float)d.damage*scale,radius=NativeAbilityRules.Radius(d,p.Stats);
            if(d.id=="blink-strike"){Blink(p,cast.Point,damage,radius);return;}
            if(d.id=="meteor-strike"||d.id=="geyser-wall"||d.id=="singularity")
            {
                float close=Vector3.Distance(cast.Point,p.Body.position)<4?(float)d.closeFuse:0;
                int count=d.id=="geyser-wall"?3:1;Vector3 side=Vector3.Cross(cast.Direction,Vector3.up).normalized;
                for(int i=0;i<count;i++)
                {
                    Vector3 at=cast.Point+(count==3?side*(3*i-3):Vector3.zero);
                    if(count==3&&Physics.Raycast(at+Vector3.up*2,Vector3.down,out var hit,6,WorldMask))at=hit.point;
                    AbilityZones.Add(new NativeAbilityZone{Owner=p,Definition=d,Position=at,Kind=d.id=="singularity"?"black-hole":"bomb",Damage=damage,Radius=radius,Until=Time+(d.id=="singularity"?(float)d.duration:(float)d.fuse+close+.35f*i),NextTick=Time+.25f});
                }
                return;
            }
            if(AbilityShots.Count>=100)return;
            Vector3 start=Muzzle(p),dir=(cast.Point-start).normalized;
            if(dir.sqrMagnitude<.01f)dir=cast.Direction;
            var shot=new NativeAbilityShot{Id=nextBullet++,Owner=p,Definition=d,Position=start,Velocity=dir*(float)d.speed,Damage=damage,Radius=(float)d.shotRadius,Gravity=(float)d.gravity,Life=d.id=="rocket"?2.5f:d.id=="orbital-mark"?1:d.id=="frag-grenade"?4:d.duration>0?(float)d.duration:3,MaxTravel=d.id=="frag-grenade"?200:(float)d.range,NextTick=Time+(float)d.tick,RemainingPierces=(int)d.pierce};
            if(d.id=="frag-grenade")
            {Vector3 delta=cast.Point-start;float t=Mathf.Clamp(delta.magnitude/16,.45f,1.1f);shot.Velocity=delta/t+Vector3.up*(float)d.gravity*t*.5f;shot.Fuse=Time+t+(float)d.fuse;}
            if(d.id=="tidal-wave")
            {dir=new Vector3(cast.Direction.x,0,cast.Direction.z).normalized;shot.Position=p.Body.position+dir*.6f+Vector3.up*.25f;shot.Velocity=dir*(float)d.speed;}
            // Cover between the pilot and muzzle must be hit before the new shot can escape it.
            Vector3 eye=p.Body.position+Vector3.up*.3f;
            if(Physics.Linecast(eye,shot.Position,out var wall,WorldMask))
            {shot.Position=wall.point+wall.normal*(shot.Radius+.01f);if(AbilityContact(shot,wall))return;}
            AbilityShots.Add(shot);
        }
        private Vector3 GroundPoint(Vector3 point)
        {
            point.x=Mathf.Clamp(point.x,-31,31);point.z=Mathf.Clamp(point.z,-27,27);
            if(Physics.Raycast(point+Vector3.up*.08f,Vector3.down,out var hit,40,WorldMask))point=hit.point;return point;
        }
        private Vector3 Muzzle(NativePilot p)=>p.Body.position+Vector3.up*.52f+Quaternion.Euler(0,p.Yaw,0)*new Vector3(.18f,0,.68f);
        private void Slow(NativePilot p,float factor,float duration)
        {
            if(Time<p.SlowImmune)return;
            if(Time>=p.SlowEnd)p.SlowFactor=1;
            p.SlowFactor=Math.Min(p.SlowFactor,factor);p.SlowEnd=Math.Max(p.SlowEnd,Time+duration);
        }
        private float AbilityHit(NativePilot target,NativePilot owner,float damage,Vector3 dir,float impulse,bool head=false,bool direct=true,bool lifeOnHit=false)
        {
            float actual=Damage(target,owner,damage,dir,impulse,head,false,direct,false);
            if(actual>0&&lifeOnHit&&owner.Hp>0)owner.Hp=Math.Min((float)owner.Stats["hp"],owner.Hp+actual*(float)owner.Stats["lifeSteal"]);
            return actual;
        }
        private List<NativePilot> AbilityBlast(NativePilot owner,Vector3 at,float radius,float damage,float impulse,int element,float propScale=1)
        {
            var hits=new List<NativePilot>();
            foreach(var p in Pilots)
            {
                Vector3 delta=p.Body.position-at;float fall=NativeAbilityRules.BlastFalloff(delta.magnitude,radius);
                if(p.Hp<=0||!Enemy(owner,p)||fall<=0||Physics.Linecast(at,p.Body.position,WorldMask))continue;
                if(AbilityHit(p,owner,damage*fall,delta.normalized,impulse*fall)>0)hits.Add(p);
            }
            foreach(var prop in Props.ToArray())if(prop!=null&&!prop.Indestructible)
            {
                Vector3 delta=prop.transform.position-at;float reach=radius+prop.transform.localScale.magnitude*.35f;
                if(delta.magnitude>=reach)continue;float fall=1-Mathf.Clamp(delta.magnitude/reach,0,.85f);
                DamageProp(prop,damage*fall*propScale,(delta+Vector3.up*.3f).normalized*impulse*fall,at);
            }
            Fx(at,radius,element);return hits;
        }
        private void Fx(Vector3 at,float radius,int element,Vector3? from=null)
        {AbilityEffects.Add(new NativeAbilityFx{Position=at,Radius=radius,Element=element,Until=Time+.22f,Beam=from.HasValue,From=from??at});}
        private void Blink(NativePilot p,Vector3 point,float damage,float radius)
        {
            Vector3 a=p.Body.position,delta=Vector3.ClampMagnitude(point-a,9),dir=delta.normalized;float distance=delta.magnitude;
            foreach(var hit in Physics.CapsuleCastAll(a+Vector3.up*.43f,a-Vector3.up*.43f,.42f,dir,distance,WorldMask))
                if(Vector3.Dot(hit.normal,dir)<-.05f)distance=Math.Min(distance,Math.Max(0,hit.distance-.03f));
            Vector3 b=a+dir*distance;p.Body.position=b;p.Body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
            foreach(var target in Pilots)
            {
                if(target.Hp<=0||!Enemy(p,target))continue;
                Vector3 nearest=a+dir*Mathf.Clamp(Vector3.Dot(target.Body.position-a,dir),0,distance);
                if(Vector3.Distance(target.Body.position,nearest)<=1.4f&&!Physics.Linecast(nearest,target.Body.position,WorldMask))AbilityHit(target,p,damage,target.Body.position-nearest,0);
            }
            Fx(b,1,3,a);AbilityBlast(p,b,radius,damage*2/3,300,3);
        }
        private void StepAbilityWorld()
        {
            const float dt=1f/60;
            AbilityEffects.RemoveAll(f=>f.Until<Time);
            for(int i=AbilityShots.Count-1;i>=0;i--)
            {
                var s=AbilityShots[i];s.Life-=dt;bool remove=false;var d=s.Definition;
                if(d.id=="ball-lightning"&&Time>=s.NextTick)
                {
                    s.NextTick=Time+(float)d.tick;
                    foreach(var p in Pilots.Where(p=>p.Hp>0&&Enemy(s.Owner,p)&&Vector3.Distance(p.Body.position,s.Position)<7&&!Physics.Linecast(s.Position,p.Body.position,WorldMask)).OrderBy(p=>(p.Body.position-s.Position).sqrMagnitude).Take(2))
                    {AbilityHit(p,s.Owner,11*s.Damage/(float)d.damage,(p.Body.position-s.Position).normalized,0);Fx(p.Body.position,.15f,3,s.Position);}
                }
                if(!s.Resting)
                {
                    Vector3 delta=Vector3.ClampMagnitude(s.Velocity*dt+Vector3.down*(.5f*s.Gravity*dt*dt),Math.Max(0,s.MaxTravel-s.Travel));s.Velocity.y-=s.Gravity*dt;
                    var hits=Physics.SphereCastAll(s.Position,s.Radius,delta.normalized,delta.magnitude,ShotMask,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance);
                    bool contact=false;
                    foreach(var h in hits)
                    {
                        var body=h.collider.GetComponent<NativePilotBody>();
                        if(body!=null&&(body.Pilot==s.Owner||body.Pilot.Hp<=0||!Enemy(s.Owner,body.Pilot)||s.Hit.Contains(body.Pilot.Seat.id)||Invulnerable(body.Pilot)))continue;
                        s.Travel+=h.distance;s.Position+=delta.normalized*h.distance;
                        if(body!=null&&Time<body.Pilot.BlockEnd&&d.id!="tidal-wave"&&s.Reflections<3)
                        {s.Owner=body.Pilot;s.Reflections++;s.Damage=Math.Min(100,s.Damage*(float)s.Owner.Stats["reflectDamage"]);s.Velocity=Vector3.ClampMagnitude(-s.Velocity*(float)s.Owner.Stats["reflectSpeed"],120);s.Position+=s.Velocity.normalized*(s.Radius+.5f);s.Hit.Clear();contact=true;break;}
                        remove=AbilityContact(s,h);contact=true;
                        // Piercing continues next tick from beyond this contact; no duplicate hit on the same pilot.
                        if(!remove&&body!=null&&d.id!="frag-grenade")s.Position+=delta.normalized*.05f;
                        break;
                    }
                    if(!contact){s.Position+=delta;s.Travel+=delta.magnitude;}
                }
                bool expired=s.Life<=0||s.Travel>=s.MaxTravel-.0001f||s.Position.y<-15||d.id=="frag-grenade"&&Time>=s.Fuse;
                if(!remove&&expired)
                {if(d.id=="frag-grenade"||d.id=="rocket"||d.id=="ball-lightning")AbilityBlast(s.Owner,s.Position,NativeAbilityRules.Radius(d,s.Owner.Stats),s.Damage,d.id=="ball-lightning"?400:(float)d.impulse,s.Owner.Pose.MagicVisual,d.id=="rocket"?2:1);remove=true;}
                if(remove)AbilityShots.RemoveAt(i);
            }
            for(int i=AbilityZones.Count-1;i>=0;i--)
            {
                var z=AbilityZones[i];var d=z.Definition;int element=z.Owner.Pose.MagicVisual;
                if(z.Attached!=null&&Time<z.LockAt)z.Position=z.Attached.Body.position;
                if(z.Kind!="bomb"&&Time>=z.NextTick)
                {
                    z.NextTick+=.25f;
                    foreach(var p in Pilots)
                    {
                        Vector3 delta=z.Position+Vector3.up*.15f-p.Body.position;
                        if(p.Hp<=0||!Enemy(z.Owner,p)||delta.magnitude>z.Radius||Physics.Linecast(z.Position+Vector3.up*.15f,p.Body.position,WorldMask))continue;
                        AbilityHit(p,z.Owner,z.Kind=="fire"?z.Damage*.25f:(float)d.tick*z.Damage/(float)d.damage*.25f,Vector3.zero,0,false,false);
                    }
                }
                if(z.Kind=="black-hole")foreach(var p in Pilots)
                {
                    Vector3 delta=z.Position+Vector3.up*.4f-p.Body.position;
                    if(p.Hp>0&&Enemy(z.Owner,p)&&delta.magnitude<z.Radius&&!Physics.Linecast(p.Body.position,z.Position+Vector3.up*.15f,WorldMask))p.Body.AddForce(delta.normalized*(float)d.force*dt,ForceMode.VelocityChange);
                }
                if(Time<z.Until)continue;
                if(z.Kind=="bomb")
                {
                    var hit=AbilityBlast(z.Owner,z.Position+Vector3.up*.15f,z.Radius,z.Damage,(float)d.impulse,element);
                    if(d.id=="geyser-wall")foreach(var p in hit)Slow(p,(float)d.speedFactor,(float)d.slowFor);
                    if(d.id=="meteor-strike")
                    {z.Kind="fire";z.Radius=3.2f*z.Radius/(float)d.radius;z.Damage=12*z.Damage/(float)d.damage;z.Until=Time+(float)d.duration;z.NextTick=Time+.25f;continue;}
                }
                else if(z.Kind=="black-hole")
                {
                    AbilityBlast(z.Owner,z.Position+Vector3.up*.15f,z.Radius*4/(float)d.radius,z.Damage,0,element);
                    Vector3 center=z.Position+Vector3.up*.15f;
                    foreach(var p in Pilots)
                    {Vector3 delta=center-p.Body.position;float distance=delta.magnitude;if(p.Hp>0&&Enemy(z.Owner,p)&&distance>.1f&&distance<5&&!Physics.Linecast(p.Body.position,center,WorldMask))p.Knockback+=delta.normalized*(1300/65f)*(1-distance/5)*(float)p.Stats["knockbackTaken"];}
                    foreach(var prop in Props)if(prop!=null&&prop.Body!=null&&!prop.Body.isKinematic)
                    {Vector3 delta=center-prop.transform.position;float distance=delta.magnitude;if(distance>.1f&&distance<5)prop.Body.AddForce(delta.normalized*1300*(1-distance/5),ForceMode.Impulse);}
                }
                AbilityZones.RemoveAt(i);
            }
        }
        private bool AbilityContact(NativeAbilityShot s,RaycastHit hit)
        {
            var d=s.Definition;var body=hit.collider.GetComponent<NativePilotBody>();var target=body?.Pilot;int element=s.Owner.Pose.MagicVisual;
            Vector3 at=hit.point+hit.normal*.03f;
            if(target==null)SurfaceImpact(hit,s.Owner,s.Id,(int)s.Owner.Weapon.color);
            if(d.id=="frag-grenade")
            {
                if(!s.Touched){s.Touched=true;s.Fuse=Math.Max(s.Fuse,Time+.45f);}
                if(target!=null){if(!s.DirectHit){AbilityHit(target,s.Owner,s.Damage*8/60,s.Velocity.normalized,150,false,true,true);s.DirectHit=true;}s.Velocity*=.3f;}
                else {Vector3 normal=Vector3.Project(s.Velocity,hit.normal);s.Velocity=(s.Velocity-normal)*.25f+normal*.4f;}
                s.Velocity=Vector3.Reflect(s.Velocity,hit.normal);s.Position=hit.point+hit.normal*(s.Radius+.02f);
                if(hit.normal.y>.6f&&s.Velocity.magnitude<1.5f){s.Resting=true;s.Velocity=Vector3.zero;}return false;
            }
            if(d.id=="rocket")
            {if(s.Travel>=(float)d.arm)AbilityBlast(s.Owner,at,NativeAbilityRules.Radius(d,s.Owner.Stats),s.Damage,(float)d.impulse,element,2);else {if(target!=null)AbilityHit(target,s.Owner,s.Damage*15/55,s.Velocity.normalized,150,false,true,true);Fx(at,.3f,0);}return true;}
            if(d.id=="orbital-mark")
            {
                if(target!=null)AbilityHit(target,s.Owner,s.Damage*10/85,s.Velocity.normalized,150,false,true,true);
                AbilityZones.Add(new NativeAbilityZone{Owner=s.Owner,Definition=d,Attached=target,LockAt=Time+(float)d.duration,Position=target!=null?target.Body.position:at,Kind="bomb",Radius=NativeAbilityRules.Radius(d,s.Owner.Stats),Damage=s.Damage,Until=Time+(float)d.fuse});return true;
            }
            if(target!=null)
            {
                s.Hit.Add(target.Seat.id);
                if(d.id=="ball-lightning")return false;
                bool head=d.id=="flame-lance"&&hit.point.y>target.Body.position.y+.4f;
                Vector3 impulse=s.Velocity.normalized*(float)d.impulse+(d.id=="tidal-wave"?Vector3.up*200:Vector3.zero);
                float actual=AbilityHit(target,s.Owner,s.Damage*(head?1.5f:1),impulse.normalized,impulse.magnitude,head,true,d.id=="flame-lance"||d.id=="shadow-shackles");
                if(actual>0)
                {
                    if(d.burn>0)RefreshDot(target,s.Owner,"fire",d.burn*s.Damage/d.damage,2);
                    if(d.speedFactor>0)Slow(target,(float)d.speedFactor,(float)d.slowFor);
                    if(d.id=="shadow-shackles"&&s.Owner.Hp>0)s.Owner.Hp=Math.Min((float)s.Owner.Stats["hp"],s.Owner.Hp+actual*.5f);
                }
                Fx(at,.6f,element);
                return d.id!="tidal-wave"&&!(d.id=="flame-lance"&&s.RemainingPierces-->0);
            }
            if(d.id=="ball-lightning")AbilityBlast(s.Owner,at,NativeAbilityRules.Radius(d,s.Owner.Stats),s.Damage,400,element);
            else if(d.id=="tidal-wave"&&hit.normal.y>.6f){s.Position.y=hit.point.y+s.Radius+.02f;return false;}
            else DamageProp(hit.collider.GetComponent<NativePropBody>(),s.Damage,s.Velocity.normalized*(float)(d.id=="tidal-wave"?600:d.impulse),hit.point);
            Fx(at,.6f,element);return true;
        }
    }
}
