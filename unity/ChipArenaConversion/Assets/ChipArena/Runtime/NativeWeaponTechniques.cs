using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativePilot
    {
        public NativeTechnique Technique;
        internal bool TechniqueReleased;internal float TechniqueNext,TechniqueHealth,TechniqueScale=1;
        internal Vector3 TechniquePosition,TechniqueMoveFrom;
    }
    public sealed partial class NativeArena
    {
        private ExpeditionAreaLayout techniqueReplicaArea;
        private bool TechniqueHubInside(Vector3 point)=>Replica?ExpeditionCollisionReady&&techniqueReplicaArea?.map==MapId&&techniqueReplicaArea.role=="world"&&ExpeditionHubRules.Inside(techniqueReplicaArea.sanctuary,Vec(point)):HubInside(point);
        private bool TechniqueHubCross(Vector3 from,Vector3 to)=>Replica?ExpeditionCollisionReady&&techniqueReplicaArea?.map==MapId&&techniqueReplicaArea.role=="world"&&ExpeditionHubRules.Crosses(techniqueReplicaArea.sanctuary,Vec(from),Vec(to),0):HubCross(from,to);
        private string TechniqueSlot(NativePilot p)=>p.Seat.bot?"":NativeTechniqueLoadout.Slot(p.Seat.weaponTechniques,p.Seat.spec);
        private float TechniqueCooldown(NativePilot p)=>TechniqueSlot(p)=="q"?p.QReady:p.EReady;
        private bool TechniqueBusy(NativePilot p)=>NativeTechniqueRules.Busy(p.Technique,Time);
        private bool TechniqueAway(NativePilot p)=>NativeTechniques.WeaponAway(p.Technique,Time);
        private bool TechniqueBlocked(NativePilot p)=>p.Hp<=0||!p.Seat.connected||Incapacitated(p)||Frozen(p)||Time<p.RollEnd||Time<p.BlockEnd||p.HeldBy!=null||p.Controlling!=null||p.ControlledBy!=null||KernelBusy(p);
        public bool TechniqueSmokeHidden(Vector3 observer,Vector3 target)=>Pilots.Any(p=>NativeTechniqueRules.Hidden(Vec(observer),Vec(target),p.Technique,Time));
        private void EndTechnique(NativePilot p)
        {var k=p.Technique;if(k==null)return;if(p.Ready<=k.RecoverUntil+.001f)p.Ready=Math.Min(p.Ready,Time);NativeTechniqueRules.End(k,Time);}
        private void StepTechniqueInput(NativePilot p)
        {
            var k=p.Technique;
            if(k!=null&&Time<k.End&&(p.Input.CancelAbilities||TechniqueBlocked(p))&&(Time<k.Release||TechniqueBusy(p)||NativeTechniqueRules.Moving(k,Time)))EndTechnique(p);
            if(k?.Kind==NativeTechniques.GrappleLine&&(p.Input.Jump||p.Input.Roll))EndTechnique(p);
            string slot=TechniqueSlot(p);bool press=slot=="q"?p.Input.AbilityQ:slot=="e"&&p.Input.AbilityE;
            if(slot=="q")p.Input.AbilityQ=false;else if(slot=="e"){p.Input.AbilityE=p.Input.AbilityEReleased=false;p.HoldAt=-1;}
            if(p.BufferedSlot==slot)p.BufferedSlot=null;
            if(press&&!p.Input.CancelAbilities)TryTechnique(p);
            if(p.Technique!=null&&!p.TechniqueReleased&&Time>=p.Technique.Release&&Time<p.Technique.Until)ReleaseTechnique(p);
        }
        // Geometric refusals only. No pilot/input/physics mutation; valid on guest scenes as well as authority.
        // The out-point overload lets the cast use the same query result without repeating the rays.
        public string TechniqueRefusal(NativePilot p,in InputFrame aim)=>TechniqueRefusal(p,in aim,out _);
        private string TechniqueRefusal(NativePilot p,in InputFrame aim,out Vector3 point)
        {
            point=default;if(p==null||TechniqueSlot(p)=="")return null;
            var old=p.Technique;Vector3 origin=p.Body.position,feet=origin-Vector3.up*.865f;point=feet;
            if(old?.Kind==NativeTechniques.StaticRecall&&NativeTechniqueRules.Recast(old,Time))
            {
                point=Vec(old.Point)+Vector3.up*.865f;
                if(!TechniqueDestination(p,point)||!RoomFor(p,point))return NativeTechniques.Blocked;
                var line=point-origin;
                return root.scene.GetPhysicsScene().CapsuleCast(origin+Vector3.up*.43f,origin-Vector3.up*.43f,.42f,line.normalized,out _,line.magnitude,WorldMask,QueryTriggerInteraction.Ignore)?NativeTechniques.Blocked:null;
            }
            var kind=NativeTechniques.For(p.Seat.spec)?.Id;
            if(kind==NativeTechniques.GrappleLine)
            {
                var direction=Vec(GunRules.Direction(aim.Yaw,aim.Pitch));
                if(!root.scene.GetPhysicsScene().Raycast(origin+Vector3.up*.3f,direction,out var hit,20,WorldMask,QueryTriggerInteraction.Ignore))return NativeTechniques.NoSurface;
                point=hit.point;return TechniqueHubCross(origin,point)?NativeTechniques.NoSurface:null;
            }
            if(kind==NativeTechniques.HealingSpring)
            {
                point=GroundPoint(AimedAbilityPoint(p,14,in aim));
                if(!root.scene.GetPhysicsScene().Raycast(point+Vector3.up*.1f,Vector3.down,out var floor,.3f,WorldMask,QueryTriggerInteraction.Ignore)||floor.normal.y<.5f)return NativeTechniques.NoFloor;
                return Vector3.Distance(point,feet)>14.5f||TechniqueHubInside(point)||TechniqueHubCross(origin,point)?NativeTechniques.NoFloor:null;
            }
            return null;
        }
        private bool TryTechnique(NativePilot p)
        {
            string slot=TechniqueSlot(p);if(slot==""||Replica||p.Input.CancelAbilities||TechniqueBlocked(p))return false;
            var old=p.Technique;
            if(NativeTechniqueRules.Recast(old,Time))
            {
                if(TechniqueBusy(p)||Time<p.AttackEnd||SpecialBusy(p)||p.AbilityCast!=null||p.CastAt>0||p.ReloadEnd>Time)return false;
                if(old.Kind==NativeTechniques.Bastion){EndTechnique(p);old.Phase="recall";return true;}
                if(old.Kind==NativeTechniques.AnchorThrow)
                {old.RecastAt=Time;old.Phase="leap";old.Until=old.RecoverUntil=Time+.45f;p.TechniqueMoveFrom=p.Body.position;return true;}
                if(old.Kind==NativeTechniques.StaticRecall)
                {
                    Vector3 origin=p.Body.position;
                    if(TechniqueRefusal(p,in p.Input,out var destination)!=null)return false;
                    if(!MeleeTeleport(p,destination,old.Kind))return false;
                    old.From=Vec(origin-Vector3.up*.865f);old.RecastAt=Time;old.Phase="bolt";old.Until=old.RecoverUntil=Time+.3f;
                    foreach(var t in MeleeTargets(p).ToArray())if(NativeTechniqueRules.LineDistance(Vec(t.at),Vec(origin),Vec(p.Body.position))<=.85f&&MeleeVisible(origin,t.at,p,t.collider))TechniqueHit(p,t.id,12,t.at,(p.Body.position-origin).normalized);
                    return true;
                }
            }
            var d=Content.Ability(p.Seat.spec,"technique");if(d==null||!d.live||NativeTechniques.For(p.Seat.spec)?.Id!=d.id||Time<TechniqueCooldown(p)||TechniqueBusy(p)||NativeTechniqueRules.Live(old,Time)||Time<p.Ready||Time<p.AttackEnd||SpecialBusy(p)||p.Fan!=null||p.AbilityCast!=null||p.CastAt>0||p.ReloadEnd>Time)return false;
            bool melee=p.Weapon.@class=="melee",magic=p.Weapon.@class=="magic";float cost=melee&&PilotMutator(p,"adrenaline")?0:NativeAbilityRules.Cost(d,p.Stats);
            if(melee&&p.Stamina<cost||magic&&p.Mana<cost||HubInside(p.Body.position))return false;
            if(TechniqueRefusal(p,in p.Input,out var point)!=null)return false;
            Vector3 direction=Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch));
            if(melee)p.Stamina-=cost;if(magic)p.Mana-=cost;p.RegenAt=Time+.45f;
            float ready=Time+(p.DevNoCooldowns?0:NativeAbilityRules.Cooldown(d,p.Stats));if(slot=="q")p.QReady=ready;else{p.EReady=ready;p.ECharges=0;}
            float release=Time+NativeAbilityRules.CastTime(d.windup,p.Stats);
            p.Technique=new NativeTechnique{Kind=d.id,Phase=TechniqueFirstPhase(d.id),At=Time,Release=release,Until=release+(float)d.duration,RecoverUntil=release+NativeAbilityRules.CastTime(d.lockout,p.Stats),From=Vec(p.Body.position+Vector3.up*.3f),Point=Vec(point),Dir=Vec(direction)};
            p.TechniqueReleased=false;p.TechniqueNext=release;p.TechniqueHealth=0;
            p.TechniqueScale=magic?NativeAbilityRules.Scale(p.Stats,p.Mana+cost>=.75f*p.Stats["maxMana"]):(float)p.Stats["damage"];
            p.Ready=Math.Max(p.Ready,p.Technique.RecoverUntil);return true;
        }
        private static string TechniqueFirstPhase(string id)
        {switch(id){case "riposte":return "guard";case "smoke-veil":case "anchor-throw":return "throw";case "bastion":return "plant";case "ricochet-coin":return "flick";case "sentry-drop":return "drop";case "grapple-line":return "fire";case "gale-arrow":return "draw";case "ignition-leap":return "crouch";case "static-recall":return "set";default:return "cast";}}
        private void ReleaseTechnique(NativePilot p)
        {
            p.TechniqueReleased=true;var k=p.Technique;if(TechniqueBlocked(p)){EndTechnique(p);return;}
            Vector3 feet=p.Body.position-Vector3.up*.865f,forward=Vec(V3.Forward(p.Input.Yaw));k.From=Vec(p.Body.position+Vector3.up*.3f);p.TechniquePosition=Vec(k.From);
            switch(k.Kind)
            {
                case "riposte":k.Until=k.Release+.5f;k.RecoverUntil=k.Until+.45f;k.Phase="guard";break;
                case "smoke-veil":k.Point=Vec(GroundPoint(feet));k.Phase="cloud";break;
                case "bastion":k.Point=Vec(GroundPoint(feet+forward*1.3f));k.Dir=Vec(forward);k.Arm=1;k.Phase="wall";p.TechniqueHealth=120;break;
                case "anchor-throw":k.Point=k.From+k.Dir*16;k.Arm=k.Release+16f/26;k.Until=k.Arm+3+NativeTechniques.BladeReturn;k.Phase="flight";break;
                case "ricochet-coin":k.Dir=Vec(forward);k.Phase="air";k.Until=k.Release+NativeTechniqueRules.CoinSeconds;break;
                case "sentry-drop":k.Point=Vec(GroundPoint(feet+forward));k.Dir=Vec(forward);k.To=k.Point+k.Dir*18;k.Arm=k.Release+.6f;k.Until=k.Arm+6;p.TechniqueNext=k.Arm;p.TechniqueHealth=40;break;
                case "grapple-line":k.Arm=k.Release+Vector3.Distance(Vec(k.From),Vec(k.Point))/60;k.Until=k.Arm+1.1f;k.RecoverUntil=k.Until;break;
                case "gale-arrow":k.Point=k.From+k.Dir*30;k.Arm=k.Release+1;k.Phase="flight";break;
                case "ignition-leap":k.Point=Vec(feet);k.Phase="flight";p.Knockback.y=0;var v=p.Body.linearVelocity;v.y=NativeTechniqueRules.LeapSpeed(23*(float)p.Stats["playerGravity"]);p.Body.linearVelocity=v;p.Grounded=false;TechniqueBurst(p,feet,3,14,0,true);break;
                case "healing-spring":k.Phase="pool";p.TechniqueNext=k.Release;break;
                case "static-recall":k.Point=Vec(feet);k.Phase="armed";break;
                case "shade-swap":k.Point=k.From+k.Dir*18;k.Phase="flight";k.Until=k.Release+18f/22;break;
            }
        }
        private void StepTechniqueWorld()
        {
            foreach(var p in Pilots)
            {
                var k=p.Technique;if(k==null)continue;
                if(p.Hp<=0||!p.Seat.connected){EndTechnique(p);continue;}
                if(!p.TechniqueReleased||Time<k.Release)continue;
                if(TechniqueBlocked(p)&&(TechniqueBusy(p)||NativeTechniqueRules.Moving(k,Time))){EndTechnique(p);continue;}
                // Sweep the final travel segment even when this fixed tick reaches the expiry clock.
                if(k.Phase=="flight"&&(k.Kind=="anchor-throw"||k.Kind=="gale-arrow"||k.Kind=="shade-swap"))
                {TechniqueFlight(p,k.Kind=="anchor-throw"?26:k.Kind=="gale-arrow"?30:22,k.Kind=="anchor-throw"?16:k.Kind=="gale-arrow"?30:18);continue;}
                if(Time>=k.Until)continue;
                switch(k.Kind)
                {
                    case "riposte":if(k.HitAt>-50)k.Phase="counter";break;
                    case "anchor-throw":if(!k.Recast(Time)&&k.Phase=="flight")TechniqueFlight(p,26,16);else if(!k.Recast(Time)&&Time>=k.Until-NativeTechniques.BladeReturn)k.Phase="return";break;
                    case "grapple-line":if(Time>=k.Arm)k.Phase="reel";break;
                    case "gale-arrow":if(k.Phase=="flight")TechniqueFlight(p,30,30);break;
                    case "shade-swap":if(k.Phase=="flight")TechniqueFlight(p,22,18);else if(k.Phase=="swap"&&Time>=k.HitAt+.25f)TechniqueSwap(p);break;
                    case "healing-spring":
                        if(Time+1e-4f>=p.TechniqueNext){p.TechniqueNext+=.25f;var at=Vec(k.Point);foreach(var q in Pilots.Where(q=>q.Hp>0&&q.Seat.connected&&Vector3.Distance(q.Body.position-Vector3.up*.865f,at)<=4&&MeleeVisible(at+Vector3.up*.2f,q.Body.position,p,q.Collider))){if(q==p||!Enemy(p,q))Heal(q,1.5f);else{q.SlowFactor=Math.Min(q.SlowFactor,.85f);q.SlowEnd=Math.Max(q.SlowEnd,Time+.3f);}}foreach(var m in Minions.Where(m=>m.Hp>0&&Enemy(p,m)&&Vector3.Distance(m.Body.position,at)<=4&&MeleeVisible(at+Vector3.up*.2f,m.Body.position,p,m.Collider)))SlowMinion(m,.85f,.3f);}break;
                    case "sentry-drop":
                        if(Time>=k.Arm){k.Phase="armed";if(Time>=p.TechniqueNext){p.TechniqueNext=Time+.5f;var target=TechniqueTarget(p,Vec(k.Point)+Vector3.up*.82f,18);if(target.id!=0){k.To=Vec(target.at);k.Dir=Vec((target.at-(Vec(k.Point)+Vector3.up*.82f)).normalized);TechniqueSentryShot(p,target.at);}}}break;
                }
            }
        }
        private (int id,Vector3 at,Collider collider) TechniqueTarget(NativePilot p,Vector3 origin,float range)
        {
            var target=MeleeTargets(p).Where(t=>(t.pilot!=null?Enemy(p,t.pilot):Enemy(p,t.minion))&&Vector3.Distance(origin,t.at)<=range&&!TechniqueSmokeHidden(origin,t.at)&&MeleeVisible(origin,t.at,p,t.collider)).OrderBy(t=>(t.at-origin).sqrMagnitude).ThenBy(t=>t.id).FirstOrDefault();return(target.id,target.at,target.collider);
        }
        private float TechniqueHit(NativePilot p,int id,float damage,Vector3 point,Vector3 direction,float impulse=0)
        {
            var t=MeleeTarget(p,id);if(t.collider==null)return 0;float amount=damage*p.TechniqueScale;
            float actual=t.pilot!=null?Damage(t.pilot,p,amount,direction,impulse,false,primary:false,hitPoint:point,damageKind:"ability"):HitMinion(t.minion,p,amount,direction,impulse,point,"energy");
            if(actual>0)Emit(EventKind.AbilityHit,p,id,point,actual,p.Technique.Kind);return actual;
        }
        private void TechniqueBurst(NativePilot p,Vector3 point,float radius,float damage,float impulse,bool burn=false,bool self=false)
        {
            foreach(var t in MeleeTargets(p).ToArray())if(Vector3.Distance(t.at,point)<=radius&&MeleeVisible(point+Vector3.up*.1f,t.at,p,t.collider))
            {
                Vector3 dir=(t.at-point).normalized;float actual=TechniqueHit(p,t.id,damage,t.at,dir,impulse);
                if(burn&&actual>0){if(t.pilot!=null)RefreshDot(t.pilot,p,"fire",3*p.TechniqueScale,2);else MinionWeaponHit(t.minion,p,actual,3*p.TechniqueScale,0);}
            }
            if(self)
            {
                foreach(var ally in Pilots)if(ally!=p&&ally.Hp>0&&!CanHit(p,ally)&&Vector3.Distance(ally.Body.position,point)<=radius&&MeleeVisible(point+Vector3.up*.1f,ally.Body.position,p,ally.Collider)){ally.Knockback+=(ally.Body.position-point).normalized*(impulse/65f);ally.Grounded=false;}
                foreach(var m in Minions)if(m.Hp>0&&!CanHit(p,m)&&!m.Body.isKinematic&&Vector3.Distance(m.Body.position,point)<=radius&&MeleeVisible(point+Vector3.up*.1f,m.Body.position,p,m.Collider))m.Body.AddForce((m.Body.position-point).normalized*impulse,ForceMode.Impulse);
                foreach(var prop in Props)if(prop!=null&&Vector3.Distance(prop.transform.position,point)<=radius&&MeleeVisible(point+Vector3.up*.1f,prop.transform.position,p,prop.GetComponent<Collider>())){var body=prop.GetComponent<Rigidbody>();if(body!=null&&!body.isKinematic)body.AddForce((body.position-point).normalized*impulse,ForceMode.Impulse);}
            }
            if(self&&Vector3.Distance(p.Body.position,point)<=radius){var push=(p.Body.position-point).normalized;if(push.y<.25f)push=(push+Vector3.up*.6f).normalized;p.Knockback+=push*(impulse/65f);p.Grounded=false;}
        }
        private void TechniqueFlight(NativePilot p,float speed,float range)
        {
            var k=p.Technique;float age=Time-k.Release;Vector3 from=p.TechniquePosition,desired=Vec(k.From)+Vec(k.Dir)*Math.Min(range,age*speed),delta=desired-from;
            if(HubInside(from)||HubCross(from,desired)){EndTechnique(p);return;}
            var hit=SweepNative(from,delta,.16f,c=>c!=p.Collider&&(c.GetComponent<NativePilotBody>()?.Pilot is NativePilot q?q.Hp>0&&CanHit(p,q)&&!Invulnerable(q):c.GetComponent<NativeMinionBody>()?.Minion is NativeMinion m?m.Hp>0&&CanHit(p,m):true));
            p.TechniquePosition=hit?.Point??desired;
            bool end=hit.HasValue||age*speed>=range;if(!end)return;k.Point=Vec(p.TechniquePosition);
            var target=hit?.Collider.GetComponent<NativePilotBody>()?.Pilot;var minion=hit?.Collider.GetComponent<NativeMinionBody>()?.Minion;int id=target?.Seat.id??minion?.Id??0;
            if(k.Kind=="anchor-throw"){k.Arm=Time;k.Until=Time+3+NativeTechniques.BladeReturn;k.Phase="stuck";if(id!=0)TechniqueHit(p,id,18,p.TechniquePosition,Vec(k.Dir));}
            else if(k.Kind=="gale-arrow"){k.Arm=Time;k.Until=Time+.9f;k.Phase="burst";TechniqueBurst(p,p.TechniquePosition,5,6,650,self:true);}
            else if(k.Kind=="shade-swap")
            {if(target!=null&&!target.Seat.AuthorityBoss&&!Incapacitated(target)&&Time>=target.RollEnd||minion!=null&&!IsZoneBoss(minion)&&Time>=(minion.Pve?.StaggerAt??0)){k.Target=id;k.HitAt=Time;k.Phase="swap";k.Until=Time+.3f;}else EndTechnique(p);}
        }
        private bool TechniqueDestination(NativePilot p,Vector3 at)=>!TechniqueHubInside(at)&&!TechniqueHubCross(p.Body.position,at)&&Vector3.Distance(ClampMap(at),at)<.05f;
        private void TechniqueSwap(NativePilot p)
        {
            var k=p.Technique;var t=MeleeTarget(p,k.Target);var origin=p.Body.position;
            if(t.collider!=null&&(t.pilot!=null?!t.pilot.Seat.AuthorityBoss&&!Incapacitated(t.pilot)&&Time>=t.pilot.RollEnd:!IsZoneBoss(t.minion)&&Time>=(t.minion.Pve?.StaggerAt??0))&&!TechniqueBlocked(p)&&TechniqueDestination(p,t.at)&&!HubInside(origin)&&MeleeVisible(origin,t.at,p,t.collider))
            {
                // Ignore only the two exchanging capsules; do not place either inside world geometry or another actor.
                bool destination=RoomFor(p,t.at,t.pilot);bool source=t.pilot!=null?RoomFor(t.pilot,origin,p):RoomFor(p,origin);
                if(t.minion!=null){bool enabled=t.collider.enabled;t.collider.enabled=false;destination=RoomFor(p,t.at);t.collider.enabled=enabled;}
                if(destination&&source){p.Body.position=t.at;if(t.pilot!=null){t.pilot.Body.position=origin;t.pilot.Body.linearVelocity=t.pilot.Knockback=t.pilot.MoveVelocity=Vector3.zero;}else{t.minion.Body.position=origin;if(!t.minion.Body.isKinematic)t.minion.Body.linearVelocity=Vector3.zero;}p.Body.linearVelocity=p.Knockback=p.MoveVelocity=Vector3.zero;Physics.SyncTransforms();Emit(EventKind.Teleport,p,t.id,t.at,0,k.Kind,from:origin);}
            }
            k.Phase="recovery";k.Until=Math.Max(k.Release,Time);
        }
        private void TechniqueMotion(NativePilot p,ref Vector3 velocity)
        {
            var k=p.Technique;if(!NativeTechniqueRules.Moving(k,Time)||TechniqueBlocked(p))return;
            Vector3 destination;
            if(k.Kind=="grapple-line")destination=Vec(k.Point)-(Vec(k.Point)-p.Body.position).normalized*.7f;
            else {float u=Mathf.Clamp01((Time+1f/60-k.RecastAt)/.45f);destination=Vector3.Lerp(p.TechniqueMoveFrom,Vec(k.Point)+Vector3.up*.9f,u)+Vector3.up*(Mathf.Sin(u*Mathf.PI)*2);}
            Vector3 delta=destination-p.Body.position;if(k.Kind=="grapple-line")delta=Vector3.ClampMagnitude(delta,16f/60);
            float distance=delta.magnitude;bool wall=false;if(distance>1e-5f){float allowed=ForwardAbilityStep(p.Body.position,delta.normalized,distance,out wall);delta=delta.normalized*allowed;}
            if(!TechniqueDestination(p,p.Body.position+delta)){EndTechnique(p);return;}
            velocity=delta*60;p.MoveVelocity=p.Knockback=Vector3.zero;p.Body.useGravity=false;p.Grounded=false;
            if(wall||k.Kind=="grapple-line"&&distance<.12f||k.Kind=="anchor-throw"&&Time+1f/60>=k.Until)
            {if(k.Kind=="anchor-throw")TechniqueBurst(p,p.Body.position+delta-Vector3.up*.865f,2,10,180);EndTechnique(p);}
        }
        private bool TryTechniqueParry(NativePilot p,Vector3 incoming,Vector3 point,NativePilot attacker=null)
        {
            var k=p.Technique;if(k?.Kind!="riposte"||!NativeTechniqueRules.Live(k,Time)||k.HitAt>-50||TechniqueBlocked(p)||!NativeTechniqueRules.Front(V3.Forward(p.Yaw),Vec(incoming)))return false;
            k.HitAt=Time;k.Point=Vec(point);k.Dir=Vec(incoming);k.Phase="counter";k.Until=Time;k.RecoverUntil=Time+.35f;p.Ready=Math.Min(p.Ready,k.RecoverUntil);
            if(attacker!=null&&attacker!=p){float actual=Damage(attacker,p,22*p.TechniqueScale,-incoming,0,false,primary:false,hitPoint:point,damageKind:"ability");if(actual>0)StunMelee(attacker,.35f);}
            return true;
        }
        private void TechniqueSentryShot(NativePilot p,Vector3 target)
        {
            var at=Vec(p.Technique.Point)+Vector3.up*.82f;var dir=(target-at).normalized;if(ProjectileCount>=100)return;
            Bullets.Add(new NativeBullet{Id=nextBullet++,Attack=nextAttack++,Owner=p.Seat.id,Original=p.Seat.id,Spec="machinegun",Detail="sentry",Born=Time,Position=at,Velocity=dir*70,BaseVelocity=dir*70,FixedSpeed=70,Damage=5,Radius=.045f,Life=1,Travel=18,Direct=false,TechniqueNoHead=true});
            Emit(EventKind.Shot,p,0,at,5,"sentry");
        }
    }
}
