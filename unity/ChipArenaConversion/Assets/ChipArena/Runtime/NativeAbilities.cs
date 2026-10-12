using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        public NativePilot Owner,Attached;public NativeMinion AttachedMinion;public ContentAbility Definition;public Vector3 Position;
        public string Kind;public int Id,Group;public float Born,Radius,Damage,Until,NextTick,LockAt;
    }
    public sealed class NativeAbilityFx
    { public Vector3 Position,From;public float Radius,Until;public int Element;public bool Beam; }

    // Native versions of the eleven standard gun/magic handlers. Special Chip handlers remain filtered out.
    public sealed partial class NativeArena
    {
        public readonly List<NativeAbilityShot> AbilityShots=new List<NativeAbilityShot>();
        public readonly List<NativeAbilityZone> AbilityZones=new List<NativeAbilityZone>();
        public readonly List<NativeAbilityFx> AbilityEffects=new List<NativeAbilityFx>();
        private readonly List<(float at,NativePilot owner,NativeAbilityCast cast,float scale)> abilityEchoes=new List<(float,NativePilot,NativeAbilityCast,float)>();
        private int abilityGroup;
        public void CancelLocalAbilities(){if(Local!=null)CancelAbilities(Local);}
        private void CancelAbilities(NativePilot p)
        { CancelWindup(p);p.BufferedSlot=null;p.HoldAt=-1; }
        private void CancelWindup(NativePilot p)
        {
            if(p.AbilityCast!=null){p.Pose.AbilityAt=-100;p.Pose.AbilityId=null;}
            p.AbilityCast=null;p.Fan=null;
            if(p.CastAt>0){p.CastAt=0;p.AttackAt=-100;}
        }
        private bool AbilityBusy(NativePilot p)=>p.Fan!=null||p.AbilityCast!=null||p.CastAt>0||Time<p.RollEnd||Time<p.AttackEnd||Incapacitated(p);
        private void StepAbilities(NativePilot p)
        {
            if(p.Seat.spec=="cyberlord"){StepCyberInput(p);return;}
            if(NativeMeleeRules.Standard(p.Seat.spec)){StepMeleeAbilityInput(p);return;}
            int max=NativeAbilityRules.Charges(p.Stats);
            if(Content.Ability(p.Seat.spec,"e")!=null&&p.ECharges<max&&Time>=p.EReady){p.ECharges++;if(p.ECharges<max)p.EReady=Time+NativeAbilityRules.Cooldown(Content.Ability(p.Seat.spec,"e"),p.Stats);}
            if(p.Input.CancelAbilities||Time<p.BlockEnd){CancelAbilities(p);return;}
            bool interrupted=Time<p.RollEnd||Incapacitated(p);
            if(interrupted){CancelWindup(p);p.HoldAt=-1;}
            if(p.AbilityCast!=null&&Time>=p.AbilityCast.Release)ReleaseAbility(p);
            if(p.BufferedSlot!=null)
            {if(Time-p.BufferedAt>.3f)p.BufferedSlot=null;else PressAbility(p,p.BufferedSlot,true);}
            if(p.Input.AbilityQ){p.HoldAt=-1;PressAbility(p,"q");}
            if(TryNecromancy(p))return;
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
            if(NativeRangedRules.Q(d?.id)){TryRangedQ(p,d);return;}
            float cooldown=slot=="q"?p.QReady-Time:p.ECharges>0?0:p.EReady-Time;
            var gate=NativeAbilityRules.Decide(d!=null&&d.kind!="ads"&&d.live,p.Hp>0,Time<p.BlockEnd,p.Mana,d==null?0:NativeAbilityRules.Cost(d,p.Stats),cooldown,AbilityBusy(p));
            if(gate==NativeAbilityRules.Press.Refused)
            {if(p.BufferedSlot==slot)p.BufferedSlot=null;return;}
            if(gate==NativeAbilityRules.Press.Buffered)
            {if(!retry||p.BufferedSlot==null){p.BufferedSlot=slot;p.BufferedAt=Time;}return;}
            Vector3 point=AimedAbilityPoint(p,(float)d.range);
            if(d.kind=="strike"||d.kind=="line"||d.kind=="zone")point=GroundPoint(point);
            Vector3 dir=Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch));
            if(d.id=="blink-strike"){dir=new Vector3(p.Input.X,0,p.Input.Z);if(dir.sqrMagnitude<.01f)dir=Quaternion.Euler(0,p.Input.Yaw,0)*Vector3.forward;point=p.Body.position+dir.normalized*(float)d.range;}
            p.AbilityCast=new NativeAbilityCast{Definition=d,Point=point,Direction=dir,Release=Time+NativeAbilityRules.CastTime(d.windup,p.Stats)};
            p.Pose.AbilityId=d.id;p.Pose.AbilityAt=Time;p.Pose.AbilityRelease=p.AbilityCast.Release;p.BufferedSlot=null;
        }
        private void ReleaseAbility(NativePilot p)
        {
            var cast=p.AbilityCast;p.AbilityCast=null;var d=cast.Definition;float cost=NativeAbilityRules.Cost(d,p.Stats);
            if(p.Hp<=0||p.Mana<cost||Time<p.BlockEnd||Incapacitated(p))return;
            float scale=NativeAbilityRules.Scale(p.Stats,p.Mana>=.75f*p.Stats["maxMana"]);
            p.Mana-=cost;
            if(d.slot=="q")p.QReady=Time+NativeAbilityRules.Cooldown(d,p.Stats);
            else {p.ECharges--;if(Time>=p.EReady)p.EReady=Time+NativeAbilityRules.Cooldown(d,p.Stats);}
            p.Ready=Math.Max(p.Ready,Time+NativeAbilityRules.CastTime(d.lockout,p.Stats));
            if(d.slot=="q")EmpowerQ(p);
            if(d.slot=="e"&&p.Stats["spellEcho"]>0)abilityEchoes.Add((Time+.8f,p,cast,scale*.5f));
            ExecuteAbility(p,cast,scale);
        }
        private void ExecuteAbility(NativePilot p,NativeAbilityCast cast,float scale)
        {
            var d=cast.Definition;int group=++abilityGroup;
            float damage=(float)d.damage*scale,radius=NativeAbilityRules.Radius(d,p.Stats);
            if(d.id=="singularity"&&Has(p,"abyssal-core"))radius*=1.25f;
            var groups=AbilityZones.Where(z=>z.Owner==p&&z.Definition.id==d.id).Select(z=>z.Group).Distinct().ToArray();
            int max=Mathf.Clamp((1+(int)p.Stats["abilityCharges"])*(1+(int)p.Stats["spellEcho"]),2,6);
            if(groups.Length>=max)foreach(var old in AbilityZones.Where(z=>z.Group==groups[0]))old.Until=Time;
            if(AbilityZones.Count>=64)foreach(var old in AbilityZones.Where(z=>z.Group==AbilityZones[0].Group))old.Until=Time;
            if(d.id=="blink-strike"){Blink(p,d,cast.Point,damage,radius);return;}
            if(d.id=="meteor-strike"||d.id=="geyser-wall"||d.id=="singularity")
            {
                float close=Vector3.Distance(cast.Point,p.Body.position)<4?(float)d.closeFuse:0;
                int count=d.id=="geyser-wall"?3:1;Vector3 side=Vector3.Cross(cast.Direction,Vector3.up).normalized;
                for(int i=0;i<count;i++)
                {
                    Vector3 at=cast.Point+(count==3?side*(3*i-3):Vector3.zero);
                    if(count==3&&root.scene.GetPhysicsScene().Raycast(at+Vector3.up*2,Vector3.down,out var hit,6,WorldMask))at=hit.point;
                    AbilityZones.Add(new NativeAbilityZone{Id=nextEvent++,Born=Time,Owner=p,Definition=d,Group=group,Position=at,Kind=d.id=="singularity"?"black-hole":"bomb",Damage=damage,Radius=radius,Until=Time+(d.id=="singularity"?(float)d.duration:(float)d.fuse+close+.35f*i),NextTick=Time+.25f});
                }
                return;
            }
            if(ProjectileCount>=100)return;
            Vector3 start=Muzzle(p),dir=(cast.Point-start).normalized;
            if(dir.sqrMagnitude<.01f)dir=cast.Direction;
            var shot=new NativeAbilityShot{Id=nextBullet++,Owner=p,Definition=d,Position=start,Velocity=dir*(float)d.speed,Damage=damage,Radius=(float)d.shotRadius,Gravity=(float)d.gravity,Life=d.id=="rocket"?2.5f:d.id=="orbital-mark"?1:d.id=="frag-grenade"?4:d.duration>0?(float)d.duration:3,MaxTravel=d.id=="frag-grenade"?200:(float)d.range,NextTick=Time+(float)d.tick,RemainingPierces=(int)d.pierce};
            if(d.id=="frag-grenade")
            {Vector3 delta=cast.Point-start;float t=Mathf.Clamp(delta.magnitude/16,.45f,1.1f);shot.Velocity=delta/t+Vector3.up*(float)d.gravity*t*.5f;shot.Fuse=Time+t+(float)d.fuse;}
            if(d.id=="tidal-wave")
            {dir=new Vector3(cast.Direction.x,0,cast.Direction.z).normalized;shot.Position=p.Body.position+dir*.6f+Vector3.up*.25f;shot.Velocity=dir*(float)d.speed;}
            // Cover between the pilot and muzzle must be hit before the new shot can escape it.
            Vector3 eye=p.Body.position+Vector3.up*.3f;
            if(SceneLinecast(eye,shot.Position,out var wall,WorldMask))
            {shot.Position=wall.point+wall.normal*(shot.Radius+.01f);if(AbilityContact(shot,wall))return;}
            AbilityShots.Add(shot);
        }
        private Vector3 GroundPoint(Vector3 point)
        {
            point=ClampMap(point);
            if(root.scene.GetPhysicsScene().Raycast(point+Vector3.up*.08f,Vector3.down,out var hit,40,WorldMask))point=hit.point;return point;
        }
        private Vector3 Muzzle(NativePilot p)=>p.Body.position+Vector3.up*.52f+Quaternion.Euler(0,p.Yaw,0)*new Vector3(.18f,0,.68f);
        // Owner's overnight request (2026-10-12), the shoulder cameras: AuthorityAim casts the ability point along the sent look
        // from the eye against the world only, so a crosshair on a pilot put the point on the wall behind it. When the client's
        // AbilityTarget lies on that same ray (Core NativeAimConvergence.OnLook: within .15 m, nearer than the world point) and
        // the authority's own ray meets a live pilot or minion there (not the caster), the ray stops at that body. The report
        // only picks a depth on the authority's own ray, so it cannot pass a wall or the range. Classic's crosshair ray never
        // lies on the eye ray and bots keep AuthorityAim, so both are unchanged. No wire change; guests do not predict it.
        private RaycastHit[] abilityAimHits=new RaycastHit[16];
        private Vector3 AimedAbilityPoint(NativePilot p,float range)
        {
            Vector3 point=AuthorityAim(p,range);if(p.Seat.bot||p.Input.AbilityTarget.Length<.01f)return point;
            Vector3 eye=p.Body.position+Vector3.up*.52f,look=Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch)).normalized;float world=Vector3.Distance(eye,point);var report=p.Input.AbilityTarget;
            if(!NativeAimConvergence.OnLook(new System.Numerics.Vector3(eye.x,eye.y,eye.z),new System.Numerics.Vector3(look.x,look.y,look.z),new System.Numerics.Vector3(report.x,report.y,report.z),world,out float along))return point;
            var physics=root.scene.GetPhysicsScene();int count;
            // A full nonalloc query may omit the nearest body; match MeleeVisible's saturation handling.
            while((count=physics.Raycast(eye,look,abilityAimHits,world,ShotMask,QueryTriggerInteraction.Ignore))==abilityAimHits.Length)
                Array.Resize(ref abilityAimHits,abilityAimHits.Length*2);
            float body=-1;Vector3 at=point;
            for(int i=0;i<count;i++)
            {
                var hit=abilityAimHits[i];var q=hit.collider.GetComponent<NativePilotBody>()?.Pilot;var m=hit.collider.GetComponent<NativeMinionBody>()?.Minion;
                if(q==p||!(q!=null&&q.Hp>0||m!=null&&m.Hp>0)||body>=0&&hit.distance>=body)continue;body=hit.distance;at=hit.point;
            }
            return NativeAimConvergence.Confirms(along,body,world)?at-look*.03f:point;
        }
        private void Slow(NativePilot p,float factor,float duration)
        {
            if(KernelLocked(p)||p.Seat.spec=="siren"||SirenArmor(p)||Time<p.SlowImmune)return;
            if(Time>=p.SlowEnd)p.SlowFactor=1;
            p.SlowFactor=Math.Min(p.SlowFactor,factor);p.SlowEnd=Math.Max(p.SlowEnd,Time+duration);
        }
        private float AbilityHit(NativePilot target,NativePilot owner,float damage,Vector3 dir,float impulse,bool head=false,bool direct=true,bool lifeOnHit=false,string damageKind="ability")
        {
            float actual=Damage(target,owner,damage,dir,impulse,head,false,direct,false,damageKind:damageKind);AbilityOnDamage(owner,actual);
            if(actual>0&&lifeOnHit&&owner.Hp>0)owner.Hp=Math.Min((float)owner.Stats["hp"],owner.Hp+actual*(float)owner.Stats["lifeSteal"]);
            return actual;
        }
        private void AbilityOnDamage(NativePilot p,float actual)
        {if(actual<=0)return;p.Mana=Math.Min((float)p.Stats["maxMana"],p.Mana+actual*(float)p.Stats["manaSurge"]);p.LeechPending+=actual*(float)p.Stats["abilityLeech"];}
        private List<NativePilot> AbilityBlast(NativePilot owner,Vector3 at,float radius,float damage,float impulse,int element,float propScale=1,Action onMinion=null,string effect="ability")
        {
            var hits=new List<NativePilot>();
            foreach(var p in Pilots)
            {
                Vector3 delta=p.Body.position-at;float fall=NativeAbilityRules.BlastFalloff(delta.magnitude,radius);
                if(p.Hp<=0||!p.Seat.connected||!CanHit(owner,p)||fall<=0||SceneLinecast(at,p.Body.position,WorldMask))continue;
                if(AbilityHit(p,owner,damage*fall,delta.normalized,impulse*fall,damageKind:"blast")>0)hits.Add(p);
            }
            foreach(var m in Minions.ToArray()){Vector3 delta=m.Body.position-at;float fall=NativeAbilityRules.BlastFalloff(delta.magnitude,radius);if(m.Hp>0&&CanHit(owner,m)&&fall>0&&MeleeVisible(at,m.Body.position,owner,m.Collider)){float actual=HitMinion(m,owner,damage,delta.normalized,impulse*fall,at,"blast",radius);AbilityOnDamage(owner,actual);if(actual>0)onMinion?.Invoke();}}
            foreach(var prop in Props.ToArray())if(prop!=null&&!prop.Indestructible)
            {
                if(!string.IsNullOrEmpty(prop.Definition.role)){StructureBlast(prop,owner,at,radius,damage*propScale,impulse);continue;}
                Vector3 delta=prop.transform.position-at;float reach=radius+prop.transform.localScale.magnitude*.35f;
                if(delta.magnitude>=reach)continue;float fall=1-Mathf.Clamp(delta.magnitude/reach,0,.85f);
                DamageProp(prop,damage*fall*propScale*(float)owner.Stats["demolition"],(delta+Vector3.up*.3f).normalized*impulse*fall,at,owner);
            }
            Emit(EventKind.Explosion,owner,0,at,damage,effect,radius);Fx(at,radius,element);return hits;
        }
        private void Fx(Vector3 at,float radius,int element,Vector3? from=null,NativePilot source=null,string detail=null)
        {if(from.HasValue&&source!=null&&(detail=="ball-lightning"||detail=="leet-lock"))Emit(EventKind.Arc,source,0,at,0,detail,radius:radius,from:from);AbilityEffects.Add(new NativeAbilityFx{Position=at,Radius=radius,Element=element,Until=Time+.22f,Beam=from.HasValue,From=from??at});}
        private void Blink(NativePilot p,ContentAbility d,Vector3 point,float damage,float radius)
        {
            Vector3 a=p.Body.position;ChipBlink(p,point-a,"blink-strike");Vector3 b=p.Body.position,delta=b-a,dir=delta.normalized;float distance=delta.magnitude;var struck=new HashSet<int>();
            foreach(var target in Pilots)
            {
                if(target.Hp<=0||!CanHit(p,target))continue;
                Vector3 nearest=a+dir*Mathf.Clamp(Vector3.Dot(target.Body.position-a,dir),0,distance);
                if(Vector3.Distance(target.Body.position,nearest)<=1.4f&&MeleeVisible(nearest,target.Body.position,p,target.Collider)&&AbilityHit(target,p,damage,target.Body.position-nearest,0)>0)struck.Add(target.Seat.id);
            }
            foreach(var m in Minions.ToArray()){Vector3 nearest=a+dir*Mathf.Clamp(Vector3.Dot(m.Body.position-a,dir),0,distance);if(CanHit(p,m)&&Vector3.Distance(m.Body.position,nearest)<=1.4f&&MeleeVisible(nearest,m.Body.position,p,m.Collider)){float actual=HitMinion(m,p,damage,Vector3.zero,0,kind:"ability");AbilityOnDamage(p,actual);if(actual>0)struck.Add(m.Id);}}
            bool blastMinion=false;Fx(b,1,3,a);foreach(var q in AbilityBlast(p,b,radius,damage*2/3,300,3,onMinion:()=>blastMinion=true,effect:d.fx??d.id))struck.Add(q.Seat.id);if(blastMinion)struck.Add(-1);if(Has(p,"storm-recall"))p.QReady=Math.Max(Time,p.QReady-Math.Min(4,struck.Count*2));
        }
        private void StepAbilityWorld()
        {
            const float dt=1f/60;
            AbilityEffects.RemoveAll(f=>f.Until<Time);
            foreach(var echo in abilityEchoes.Where(e=>e.at<=Time).ToArray()){abilityEchoes.Remove(echo);if(echo.owner.Hp>0)ExecuteAbility(echo.owner,echo.cast,echo.scale);}
            foreach(var p in Pilots)if(p.LeechPending>=1&&Time>=p.LeechAt+.25f){Heal(p,p.LeechPending);p.LeechPending=0;p.LeechAt=Time;}
            for(int i=AbilityShots.Count-1;i>=0;i--)
            {
                var s=AbilityShots[i];s.Life-=dt;bool remove=false;var d=s.Definition;
                if(d.id=="ball-lightning"&&Time>=s.NextTick)
                {
                    s.NextTick=Time+(float)d.tick;
                    foreach(var n in NearAbilityEnemies(s.Owner,s.Position,7,2)){float damage=11*s.Damage/(float)d.damage;if(n.p!=null)AbilityHit(n.p,s.Owner,damage,(n.at-s.Position).normalized,0);else AbilityOnDamage(s.Owner,HitMinion(n.m,s.Owner,damage,(n.at-s.Position).normalized,0,n.at,"energy"));Fx(n.at,.15f,3,s.Position,s.Owner,"ball-lightning");}
                }
                if(!s.Resting)
                {
                    Vector3 delta=Vector3.ClampMagnitude(s.Velocity*dt+Vector3.down*(.5f*s.Gravity*dt*dt),Math.Max(0,s.MaxTravel-s.Travel));s.Velocity.y-=s.Gravity*dt;
                    if(CyberAbsorb(s.Owner,s.Position,s.Position+delta,s.Radius)){AbilityShots.RemoveAt(i);continue;}
                    var hits=ProjectileHits(s.Position,s.Radius,delta);
                    bool contact=false;
                    foreach(var h in hits)
                    {
                        var body=h.collider.GetComponent<NativePilotBody>();
                        var minion=h.collider.GetComponent<NativeMinionBody>()?.Minion;if(minion!=null&&(minion.Hp<=0||!CanHit(s.Owner,minion)||s.Hit.Contains(minion.Id)))continue;
                        if(body!=null&&(body.Pilot==s.Owner||body.Pilot.Hp<=0||!CanHit(s.Owner,body.Pilot)||s.Hit.Contains(body.Pilot.Seat.id)||Invulnerable(body.Pilot)))continue;
                        s.Travel+=h.distance;s.Position+=delta.normalized*h.distance;
                        if(body!=null&&AbsorbCharge(body.Pilot,s.Velocity,h.point,s.Id)){remove=true;contact=true;break;}
                        if(body!=null&&Time<body.Pilot.BlockEnd&&d.id!="tidal-wave"&&s.Reflections<3)
                        {BlockProc(body.Pilot,s.Owner,h.point,"deflect");s.Owner=body.Pilot;s.Reflections++;s.Damage=Math.Min(100,s.Damage*(float)s.Owner.Stats["reflectDamage"]);s.Velocity=Vector3.ClampMagnitude(-s.Velocity*(float)s.Owner.Stats["reflectSpeed"],120);s.Position+=s.Velocity.normalized*(s.Radius+.5f);s.Hit.Clear();contact=true;break;}
                        remove=AbilityContact(s,h);contact=true;
                        // Piercing continues next tick from beyond this contact; no duplicate hit on the same pilot.
                        if(!remove&&(body!=null||minion!=null)&&d.id!="frag-grenade")s.Position+=delta.normalized*.05f;
                        break;
                    }
                    if(!contact){if(NativeSpecialRules.Ceiling(Vec(s.Position),Vec(delta),s.Radius,out float ceilingAt,out var ceilingPoint)){s.Position=Vec(ceilingPoint);remove=true;}else{s.Position+=delta;s.Travel+=delta.magnitude;}}
                }
                bool expired=s.Life<=0||s.Travel>=s.MaxTravel-.0001f||s.Position.y<-15||d.id=="frag-grenade"&&Time>=s.Fuse;
                if(!remove&&expired)
                {if(d.id=="frag-grenade"||d.id=="rocket"||d.id=="ball-lightning")AbilityBlast(s.Owner,s.Position,NativeAbilityRules.Radius(d,s.Owner.Stats),s.Damage,d.id=="ball-lightning"?400:(float)d.impulse,s.Owner.Pose.MagicVisual,d.id=="rocket"?2:1,effect:d.fx??d.id);remove=true;}
                if(remove)AbilityShots.RemoveAt(i);
            }
            for(int i=AbilityZones.Count-1;i>=0;i--)
            {
                var z=AbilityZones[i];var d=z.Definition;int element=z.Owner.Pose.MagicVisual;
                if(z.Attached!=null&&Time<z.LockAt)z.Position=z.Attached.Body.position;else if(z.AttachedMinion?.Body!=null&&Time<z.LockAt)z.Position=z.AttachedMinion.Body.position;
                if(z.Kind!="bomb"&&Time>=z.NextTick)
                {
                    z.NextTick+=.25f;
                    foreach(var m in Minions.ToArray())if(m.Hp>0&&CanHit(z.Owner,m)&&Vector3.Distance(z.Position,m.Body.position)<z.Radius&&MeleeVisible(z.Position+Vector3.up*.15f,m.Body.position,z.Owner,m.Collider))AbilityOnDamage(z.Owner,HitMinion(m,z.Owner,z.Kind=="fire"?z.Damage*.25f:(float)d.tick*z.Damage/(float)d.damage*.25f,Vector3.zero,0,null,"dot"));
                    foreach(var p in Pilots)
                    {
                        Vector3 delta=z.Position+Vector3.up*.15f-p.Body.position;
                        if(p.Hp<=0||!CanHit(z.Owner,p)||delta.magnitude>z.Radius||SceneLinecast(z.Position+Vector3.up*.15f,p.Body.position,WorldMask))continue;
                        AbilityHit(p,z.Owner,z.Kind=="fire"?z.Damage*.25f:(float)d.tick*z.Damage/(float)d.damage*.25f,Vector3.zero,0,false,false,damageKind:"field");
                    }
                }
                if(z.Kind=="black-hole")foreach(var m in Minions){Vector3 pull=z.Position+Vector3.up*.4f-m.Body.position;if(m.Hp>0&&CanHit(z.Owner,m)&&pull.magnitude<z.Radius&&MeleeVisible(z.Position+Vector3.up*.15f,m.Body.position,z.Owner,m.Collider)){if(m.Pve!=null)m.Pve.External+=pull.normalized*(float)d.force*dt;else if(!m.Body.isKinematic)m.Body.AddForce(pull.normalized*(float)d.force*dt,ForceMode.VelocityChange);}}
                if(z.Kind=="black-hole")foreach(var p in Pilots)
                {
                    Vector3 delta=z.Position+Vector3.up*.4f-p.Body.position;
                    if(p.Hp>0&&CanHit(z.Owner,p)&&delta.magnitude<z.Radius&&!SceneLinecast(p.Body.position,z.Position+Vector3.up*.15f,WorldMask))p.Knockback+=delta.normalized*(float)d.force*dt*(float)p.Stats["knockbackTaken"]*(p.SlowFactor<.6f?.5f:1);
                }
                if(Time<z.Until)continue;
                if(z.Kind=="bomb")
                {
                    var hit=AbilityBlast(z.Owner,z.Position+Vector3.up*.15f,z.Radius,z.Damage,(float)d.impulse,element,effect:d.fx??d.id);
                    if(d.id=="geyser-wall"){foreach(var p in hit)Slow(p,(float)d.speedFactor,(float)d.slowFor);foreach(var m in Minions)if(m.Hp>0&&CanHit(z.Owner,m)&&Vector3.Distance(m.Body.position,z.Position)<z.Radius&&MeleeVisible(z.Position+Vector3.up*.15f,m.Body.position,z.Owner,m.Collider))SlowMinion(m,(float)d.speedFactor,(float)d.slowFor);}
                    if(d.id=="meteor-strike")
                    {z.Id=nextEvent++;z.Born=Time;z.Kind="fire";z.Radius=3.2f*z.Radius/(float)d.radius*(Has(z.Owner,"cinder-field")?1.5f:1);z.Damage=12*z.Damage/(float)d.damage;z.Until=Time+(float)d.duration*(Has(z.Owner,"cinder-field")?1.5f:1);z.NextTick=Time+.25f;continue;}
                }
                else if(z.Kind=="black-hole")
                {
                    AbilityBlast(z.Owner,z.Position+Vector3.up*.15f,z.Radius*4/(float)d.radius,z.Damage*(Has(z.Owner,"abyssal-core")?1.2f:1),0,element,effect:d.fx??d.id);
                    Vector3 center=z.Position+Vector3.up*.15f;
                    foreach(var p in Pilots)
                    {Vector3 delta=center-p.Body.position;float distance=delta.magnitude;if(p.Hp>0&&CanHit(z.Owner,p)&&distance>.1f&&distance<5&&!SceneLinecast(p.Body.position,center,WorldMask))p.Knockback+=delta.normalized*(1300/65f)*(1-distance/5)*(float)p.Stats["knockbackTaken"];}
                    foreach(var prop in Props)if(prop!=null&&prop.Body!=null&&!prop.Body.isKinematic)
                    {Vector3 delta=center-prop.transform.position;float distance=delta.magnitude;if(distance>.1f&&distance<5)prop.Body.AddForce(delta.normalized*1300*(1-distance/5),ForceMode.Impulse);}
                }
                AbilityZones.RemoveAt(i);
            }
        }
        private bool AbilityContact(NativeAbilityShot s,AbilityContactHit hit)
        {
            var d=s.Definition;var body=hit.collider.GetComponent<NativePilotBody>();var target=body?.Pilot;int element=s.Owner.Pose.MagicVisual;
            Vector3 at=hit.point+hit.normal*.03f;var minion=hit.collider.GetComponent<NativeMinionBody>()?.Minion;if(minion!=null)return AbilityMinionContact(s,minion,hit);
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
            {if(s.Travel>=(float)d.arm)AbilityBlast(s.Owner,at,NativeAbilityRules.Radius(d,s.Owner.Stats),s.Damage,(float)d.impulse,element,2,effect:d.fx??d.id);else {if(target!=null)AbilityHit(target,s.Owner,s.Damage*15/55,s.Velocity.normalized,150,false,true,true);Fx(at,.3f,0);}return true;}
            if(d.id=="orbital-mark")
            {
                if(target!=null)AbilityHit(target,s.Owner,s.Damage*10/85,s.Velocity.normalized,150,false,true,true);
                AbilityZones.Add(new NativeAbilityZone{Id=nextEvent++,Born=Time,Owner=s.Owner,Definition=d,Attached=target,LockAt=Time+(float)d.duration,Position=target!=null?target.Body.position:at,Kind="bomb",Radius=NativeAbilityRules.Radius(d,s.Owner.Stats),Damage=s.Damage,Until=Time+(float)d.fuse});return true;
            }
            if(target!=null)
            {
                s.Hit.Add(target.Seat.id);
                if(d.id=="ball-lightning")return false;
                bool head=d.id=="flame-lance"&&NativeCombatRules.Headshot(Vec(target.Body.position),target.Yaw,Vec(hit.point-s.Velocity.normalized*.1f),Vec(s.Velocity),s.Radius,target.Down!=null||target.Juggle!=null);
                Vector3 impulse=s.Velocity.normalized*(d.id=="tidal-wave"&&Has(s.Owner,"undertow")?-700:(float)d.impulse)+(d.id=="tidal-wave"?Vector3.up*200:Vector3.zero);
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
            if(d.id=="ball-lightning")AbilityBlast(s.Owner,at,NativeAbilityRules.Radius(d,s.Owner.Stats),s.Damage,400,element,effect:d.fx??d.id);
            else if(d.id=="tidal-wave"&&hit.normal.y>.6f){s.Position.y=hit.point.y+s.Radius+.02f;return false;}
            else if(d.id=="tidal-wave"){var prop=hit.collider.GetComponent<NativePropBody>();if(prop?.Body!=null&&!prop.Body.isKinematic)prop.Body.AddForceAtPosition(s.Velocity.normalized*600,hit.point,ForceMode.Impulse);}
            // Of the shots that reach a surface here only Flame Lance shows a visual blast in the source (magic-abilities.ts:41
            // onSurface); Tidal Wave and Shadow Shackles stop without one and Ball Lightning's blast is recorded above.
            // Presentation feed only.
            if(d.id=="flame-lance")Fx(at,.6f,element);
            return true;
        }
    }
}
