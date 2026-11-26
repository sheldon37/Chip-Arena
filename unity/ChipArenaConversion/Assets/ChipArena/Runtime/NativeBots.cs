using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private InputFrame Bot(NativePilot p,bool force=false)
        {
            if(NativeCombatMarks.Has(p.TechniqueMarks,"flash-confused",Time))return new InputFrame{Yaw=p.Yaw};
            var pos=p.Body.position;
            NativeMinion mirror=null;
            float mirrorDistance=float.PositiveInfinity;
            foreach(var m in Minions)
            {
                if(m.Hp<=0||(m.Kind!="mirror-double"&&m.Kind!="wild-afterimage")||!Enemy(p,m))continue;
                var at=m.Body.position;
                float distance=(at-pos).sqrMagnitude;
                if(distance<mirrorDistance&&MeleeVisible(pos,at,p,m.Collider)){mirror=m;mirrorDistance=distance;}
            }
            if(mirror!=null)return BotFocus(p,mirror.Body.position,MinionVelocity(mirror),mirror.Collider,null,force);
            var taunt=PriorityTaunt(p.TechniqueMarks);if(taunt!=null&&Enemy(p,taunt))return BotFocus(p,taunt.Body.position,PilotVelocity(taunt),taunt.Collider,taunt,force);
            // Select one brain before observing a target. Running the PvP brain first
            // consumes the reaction window on Siren, then PvE reuses that aim for an add.
            if(!StationaryBots&&p.Seat.difficulty!="dummy"&&BotSentry(p,out var sentryInput))return sentryInput;
            if(Pve!=null)return PveBot(p,new InputFrame{Yaw=p.Yaw});
            if((StationaryBots||p.Seat.difficulty=="dummy")&&!force)return new InputFrame{Yaw=p.Yaw};
            NativePilot target=null,decoy=null;
            float targetDistance=float.PositiveInfinity,decoyDistance=float.PositiveInfinity;
            foreach(var q in Pilots)
            {
                if(q.Hp<=0||!Enemy(p,q))continue;
                var at=q.Body.position;
                float distance=(at-pos).sqrMagnitude;
                if(distance<targetDistance&&!BladeUnseen(q)&&!TechniqueSmokeHidden(pos,at)){target=q;targetDistance=distance;}
                if(Time>=q.MeleeKit.AfterimageUntil)continue;
                var image=Vec(q.MeleeKit.Afterimage);
                if(Vector3.Distance(q.MeleeKit.Afterimage.Length>0?image:at,pos)>=30)continue;
                float imageDistance=(image-pos).sqrMagnitude;
                if(imageDistance<decoyDistance){decoy=q;decoyDistance=imageDistance;}
            }
            if(target==null)return new InputFrame{Yaw=p.Yaw};
            if(decoy!=null)return BotFocus(p,Vec(decoy.MeleeKit.Afterimage),Vector3.zero,decoy.Collider,decoy,force);
            NativeMinion summon=null;
            float summonRange=Math.Min(6,Mathf.Sqrt(targetDistance)-1.5f);
            float summonDistance=float.PositiveInfinity;
            foreach(var m in Minions)
            {
                if(m.Pve!=null||m.Hp<=0||!Enemy(p,m))continue;
                float distance=(m.Body.position-pos).sqrMagnitude;
                if(distance<summonDistance&&Mathf.Sqrt(distance)<summonRange){summon=m;summonDistance=distance;}
            }
            return BotFocus(p,summon?.Body.position??target.Body.position,summon!=null?MinionVelocity(summon):PilotVelocity(target),summon?.Collider??target.Collider,target,force);
        }
        private static Vector3 PilotVelocity(NativePilot p)=>p.Body.isKinematic?Vector3.zero:p.Body.linearVelocity;
        private static Vector3 MinionVelocity(NativeMinion m)=>m.Body.isKinematic?Vector3.zero:m.Body.linearVelocity;
        private InputFrame BotFocus(NativePilot p,Vector3 target,Vector3 targetVelocity,Collider collider,NativePilot opponent=null,bool force=false)
        {
            if(!force&&(p.Seat.difficulty=="dummy"||StationaryBots))return new InputFrame{Yaw=p.Yaw};
            int level=NativeBotRules.Level(p.Seat.difficulty);var pos=p.Body.position;var eye=pos+Vector3.up*.5f;var delta=target-pos;float distance=delta.magnitude;
            var right=Vector3.Cross(Vector3.up,delta).normalized;
            Vector3 observed=Vec(p.BotBrain.Observe(Time,p.Seat.difficulty,Vec(target),random.NextDouble,Vec(right),distance));
            bool melee=p.Weapon.@class=="melee";
            Vector3 headOffset=Vector3.zero;
            if(level==3&&!melee)
            {
                if(opponent!=null&&collider==opponent.Collider&&opponent.Down==null&&opponent.Juggle==null)
                    headOffset=Vector3.up*NativeCombatRules.HeadHeight;
                else if(collider!=null&&collider.TryGetComponent<NativeMinionBody>(out var body)&&body.Minion?.Pve!=null)
                    headOffset=body.Minion.Pve.Crawl?Vec(V3.Forward(body.Minion.Yaw))*.4f:Vector3.up*(body.Minion.Collider.height*.38f);
            }
            Vector3 aimAt=observed+headOffset;
            bool visible=!TechniqueSmokeHidden(eye,target)&&MeleeVisible(eye,target+headOffset,p,collider);
            if(level==3&&!melee&&!NativeProjectileRules.Instant(p.Weapon,p.Stats,false))
            {
                float speed=p.Seat.spec=="bow"?NativeBowRules.Speed(distance>15?.7f:distance<6?0:.35f):(float)p.Weapon.speed;
                speed*=(float)p.Stats["projectileSpeed"];
                if(speed>0)
                {
                    Vector3 point=aimAt;
                    for(int i=0;i<3;i++)aimAt=point+targetVelocity*Math.Min(3,Vector3.Distance(eye,aimAt)/speed);
                }
            }
            var aim=aimAt-eye;
            var input=new InputFrame{Yaw=Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg,Pitch=Mathf.Atan2(aim.y,new Vector2(aim.x,aim.z).magnitude)*Mathf.Rad2Deg,AimTarget=Vec(aimAt),AbilityTarget=Vec(aimAt),Aim=visible&&NativeBotRules.Ads(p.Seat.spec,distance)};
            if(!melee&&!input.Aim){float pitch=GunRules.BallisticPitch(new Vector2(aim.x,aim.z).magnitude,aim.y,(float)(p.Weapon.speed*p.Stats["projectileSpeed"]),NativeProjectileRules.Gravity(p.Weapon,p.Stats));input.Pitch=Mathf.Lerp(input.Pitch,pitch,level==3?1:level==2?.95f:level==1?.85f:.65f);}
            float desired=NativeWhirlwindRules.Active(p.Special,Time)?1:NativeBotRules.Desired(p.Weapon);var flat=new Vector3(delta.x,0,delta.z).normalized;Vector3 move=distance>desired+1?flat:distance<desired-1?-flat*.8f:Vector3.zero;
            if(level>0&&!melee&&visible)move+=Quaternion.Euler(0,input.Yaw+90,0)*Vector3.forward*(Mathf.Sin(Time*1.1f+p.Seat.id)>0?.65f:-.65f);
            Vector3 navGoal=target;
            bool planPosition=level==3||level==2&&!melee&&(!visible||p.ReloadEnd>Time);
            if(planPosition&&TacticalPosition(p,target,opponent,desired,melee,out var goal))
            {
                navGoal=goal;
                var toward=goal-pos;
                toward.y=0;
                move=toward.magnitude>.65f?toward.normalized:Vector3.zero;
            }
            else if(level>0&&level<3&&!melee&&(p.ReloadEnd>Time||p.Weapon.ammo>0&&p.Ammo<=1||p.Mana<18))
            {var cover=AllBlocks.Where(c=>c!=null&&!c.Fragment&&!c.Definition.dynamic&&c.Definition.material!="foundation"&&c.Definition.size.y>1).OrderBy(c=>(c.transform.position-pos).sqrMagnitude).FirstOrDefault();if(cover!=null){var safe=cover.transform.position+(cover.transform.position-target).normalized*(Math.Max(cover.Definition.size.x,cover.Definition.size.z)*.6f+1);move=Vector3.Distance(safe,pos)<1?Vector3.zero:new Vector3(safe.x-pos.x,0,safe.z-pos.z).normalized;}}
            if(Map.unity)move=StructureNavMove(p,navGoal,move);
            bool obstacle=move.sqrMagnitude>.01f&&root.scene.GetPhysicsScene().Raycast(pos-Vector3.up*.4f,move.normalized,out _,1.1f,WorldMask,QueryTriggerInteraction.Ignore);
            if(obstacle){if(!Map.unity)move=NativeMeleeRules.Standard(p.Seat.spec)?MeleeApproachLane(p,move):move*.25f+Quaternion.Euler(0,input.Yaw+90,0)*Vector3.forward*(Mathf.Sin(Time*.4f)>0?1:-1);input.Jump=p.Grounded||p.AirJumps<NativeJumpSimulation.Budget(p.Seat.spec,p.Stats.Get("extraAirJumps"))&&p.Body.linearVelocity.y<=2;}
            if(NativeWhirlwindRules.Active(p.Special,Time))move=distance>1.8f?move:Vector3.zero;
            else if(melee&&p.Stamina<p.Weapon.cost)move=-flat*.8f;
            var clamped=ClampMap(pos,true);if(clamped.x!=pos.x)move.x=Math.Sign(clamped.x-pos.x);if(clamped.z!=pos.z)move.z=Math.Sign(clamped.z-pos.z);
            input.X=Mathf.Clamp(move.x,-1,1);input.Z=Mathf.Clamp(move.z,-1,1);input.Shoot=melee?distance<p.Weapon.range*1.25:visible||level!=3&&obstacle;input.Reload=p.Weapon.ammo>0&&(p.Ammo==0||p.Seat.spec!="rifle"&&p.Ammo<2);
            if(NativeMeleeRules.Standard(p.Seat.spec))
            {
                var locked=Pilots.FirstOrDefault(q=>q.Collider==collider&&Vector3.Distance(q.Body.position,target)<.1f);
                var minion=Minions.FirstOrDefault(m=>m.Collider==collider);
                input.LockTarget=visible?(locked?.Seat.id??minion?.Id??0):0;
                float engage=(float)(p.Weapon.range*p.Stats["reach"])*.85f+NativeMeleeFeel.StepCap(p.Seat.spec,p.Swing!=null)-.4f;
                float cost=NativeMeleeFeel.Cost(p.Weapon,p.Stats);
                input.Shoot=visible&&distance<=engage&&p.Stamina>=cost;
                input.Attack=input.Shoot&&p.BotBrain.Reacted&&(p.Swing==null||NativeMeleeKitRules.BufferOpen(p.Swing,Time)&&Time>=NativeMeleeKitRules.ChainAt(p.Swing)-.1f&&p.MeleeBufferedUntil<Time);
                input.Sprint=!input.Shoot&&distance>engage&&p.Stamina>=cost*2&&!obstacle&&Vector3.Dot(move.normalized,flat)>.6f;
            }
            else input.Roll=level>0&&melee&&distance>4&&distance<11&&ClampMap(pos,true)==pos;
            if(p.BotBrain.Reacted&&Time>=p.BlockReady)
            {
                float window=level==3?.085f:.22f;
                var swing=opponent?.Swing;
                input.Block=swing!=null&&visible&&distance<opponent.Weapon.range+1&&swing.HitAt-Time<=window&&Time<=swing.HitUntil
                    &&NativeBotRules.Parry(level,p.Seat.id,opponent.Seat.id,swing.Id,false);
                if(!input.Block)foreach(var shot in Bullets)
                {
                    Vector3 offset=pos-shot.Position;
                    float eta=Vector3.Dot(offset,shot.Velocity)/Math.Max(1,shot.Velocity.sqrMagnitude);
                    if(eta<=0||eta>window||(offset-shot.Velocity*eta).magnitude>1+shot.Radius)continue;
                    if(!CanHit(Pilots.Find(q=>q.Seat.id==shot.Owner),p)||SceneLinecast(shot.Position,pos,WorldMask))continue;
                    if(NativeBotRules.Parry(level,p.Seat.id,shot.Owner,shot.Id,true)){input.Block=true;break;}
                }
            }
            // The held guard (NativeGuard.cs): a melee bot not parrying a blow on its way puts its guard up for it (a roll per blow, by level).
            if(!input.Block&&NativeMeleeRules.Guards(p.Weapon?.@class)&&opponent?.Swing!=null&&visible&&distance<opponent.Weapon.range+1.5f&&opponent.Swing.HitAt-Time<.45f&&opponent.Swing.Id!=p.BotBrain.GuardSwing)
            {p.BotBrain.GuardSwing=opponent.Swing.Id;if(random.NextDouble()<(level==3?0:level==2?.55:level==1?.25:.08))p.BotBrain.GuardUntil=Time+.7f;}
            input.Guard=!input.Block&&Time<p.BotBrain.GuardUntil&&NativeMeleeRules.Guards(p.Weapon?.@class);
            if(input.Guard){input.Attack=input.Shoot=input.Sprint=false;return input;}
            if(input.Block){input.Attack=input.Shoot=input.Roll=input.Sprint=false;if(level!=3&&CyberAllowed(p)&&Time>=p.Cyber.EReady&&p.Mana>=35){input.Block=false;input.AbilityE=true;}return input;}
            if(p.Seat.spec=="siren")return SirenDecision(p,input,observed,distance,visible,level);
            BotAbilities(p,ref input,observed,targetVelocity,distance,visible,level,headOffset);
            if(input.AbilityQ||input.AbilityE)input.Attack=input.Sprint=false;
            if(NativeMeleeRules.Standard(p.Seat.spec)&&visible&&!p.Grounded&&delta.y<=-2&&new Vector2(delta.x,delta.z).magnitude<=3&&NativeAirAttackSimulation.Starts(false,p.AirborneAt,p.AirAttackUsed,Time,new InputFrame{Attack=true}))
            {input.Pitch=Math.Min(-45,input.Pitch);input.Attack=input.Shoot=true;input.AbilityQ=input.AbilityE=input.Sprint=false;}
            if((pos-p.BotPrevious).sqrMagnitude<.000025f&&move.magnitude>.2f)p.BotBlocked+=1f/60;else p.BotBlocked=0;p.BotPrevious=pos;
            if(p.BotBlocked>.65f){input.Jump=p.Grounded||p.AirJumps<NativeJumpSimulation.Budget(p.Seat.spec,p.Stats.Get("extraAirJumps"))&&p.Body.linearVelocity.y<=2;input.X=Mathf.Sin(Time*3);input.Z=Mathf.Cos(Time*3);}if(!SupportedStageMove(pos,new Vector3(input.X,0,input.Z))){input.X=input.Z=0;input.Jump=input.Roll=input.Sprint=false;}return input;
        }
        // Instant shots and contacts between AI ticks have no telegraph for BotFocus to observe.
        private void GodlikeContactParry(NativePilot pilot)
        {
            if(pilot==null||!pilot.Seat.bot||pilot.Seat.difficulty!="godlike"||StationaryBots||TrainingDummy(pilot)||
                (pilot.Seat.devDummy&&pilot.DevInput!="ai")||Frozen(pilot)||Time<pilot.BlockEnd||Time<pilot.BlockReady||
                NativeCombatMarks.Has(pilot.TechniqueMarks,"flash-confused",Time))return;
            TryBlock(pilot);
        }

        // Sample local firing lanes on a timer. Keep travelling toward the selected floor point between plans.
        private bool TacticalPosition(NativePilot pilot,Vector3 target,NativePilot opponent,float desired,bool melee,out Vector3 goal)
        {
            var brain=pilot.BotBrain;
            bool cover=(pilot.Hp<pilot.Stats["hp"]*.35&&Time-pilot.LastDamage<1.2f)||pilot.ReloadEnd>Time||pilot.Weapon.ammo>0&&pilot.Ammo==0||
                (melee?pilot.Stamina<pilot.Weapon.cost:pilot.Weapon.@class=="magic"&&pilot.Mana<18);
            if(Time>=brain.PlanAt||cover!=brain.SeekingCover)
            {
                brain.SeekingCover=cover;
                brain.PlanAt=Time+.45f+.025f*(pilot.Seat.id%7);
                brain.HasGoal=false;
                var pos=pilot.Body.position;
                var scene=root.scene.GetPhysicsScene();
                float best=float.NegativeInfinity;
                for(int i=0;i<17;i++)
                {
                    var candidate=pos;
                    if(i>0)candidate+=Quaternion.Euler(0,(i-1)%8*45,0)*Vector3.forward*(i<=8?3:7);
                    if(!scene.Raycast(candidate+Vector3.up*2,Vector3.down,out var floor,4,WorldMask,QueryTriggerInteraction.Ignore)||floor.normal.y<.65f)continue;
                    candidate=floor.point+Vector3.up*(pilot.Collider.height*.5f+.03f);
                    if(UnsafeStagePath(pos,candidate)||Vector3.Distance(ClampMap(candidate,true),candidate)>.15f)continue;
                    Vector3 path=candidate-pos;
                    if(path.magnitude>.2f&&scene.CapsuleCast(pos+Vector3.up*.35f,pos-Vector3.up*.35f,.4f,path.normalized,out _,path.magnitude,WorldMask,QueryTriggerInteraction.Ignore))continue;
                    bool hidden=SceneLinecast(candidate+Vector3.up*.45f,target+Vector3.up*.45f,WorldMask);
                    if(cover!=hidden)continue;
                    float range=Vector3.Distance(candidate,target);
                    float score=-Math.Abs(range-desired)-.25f*path.magnitude;
                    if(!cover&&!melee)
                    {
                        var side=Vector3.Cross(Vector3.up,target-candidate).normalized*1.8f;
                        if(SceneLinecast(candidate+side,target,WorldMask)||SceneLinecast(candidate-side,target,WorldMask))score+=3;
                        score+=Mathf.Clamp(candidate.y-target.y,-2,3);
                        if(opponent!=null&&opponent.Weapon.@class!="melee")
                        {
                            var look=Vec(GunRules.Direction(opponent.Yaw,opponent.Pitch));
                            var offset=candidate-target;
                            float along=Vector3.Dot(offset,look);
                            if(along>0)score-=Mathf.Max(0,3-(offset-look*along).magnitude);
                        }
                    }
                    if(score<=best)continue;
                    best=score;brain.TacticalGoal=Vec(candidate);brain.HasGoal=true;
                }
            }
            goal=Vec(brain.TacticalGoal);
            return brain.HasGoal;
        }
        private Vector3 MeleeApproachLane(NativePilot p,Vector3 desired)
        {
            // Pick a capsule-wide clear route around cover; keep the chosen side until progress resumes.
            var pos=p.Body.position;var scene=root.scene.GetPhysicsScene();Vector3 best=Vector3.zero;float score=-1;
            int side=p.BotSide==0?(p.Seat.id%2==0?1:-1):p.BotSide;
            foreach(float angle in new[]{45f*side,80f*side,-45f*side,-80f*side})
            {
                var lane=Quaternion.Euler(0,angle,0)*desired.normalized;
                float free=scene.CapsuleCast(pos+Vector3.up*.43f,pos-Vector3.up*.43f,.46f,lane,out var hit,3,WorldMask,QueryTriggerInteraction.Ignore)?hit.distance:3;
                float value=free-.005f*Mathf.Abs(angle)+(Math.Sign(angle)==side?.1f:0);
                if(value>score){score=value;best=lane;p.BotSide=Math.Sign(angle);}
            }
            return score>.5f?best:Vector3.zero;
        }
        private void BotAbilities(NativePilot p,ref InputFrame input,Vector3 target,Vector3 velocity,float distance,bool visible,int level,Vector3 headOffset=default)
        {
            if(p.Seat.spec=="bow"){BotBow(p,ref input,target,velocity,distance,visible);return;}
            if(p.Seat.spec==NativeBombs.Spec){BotBomb(p,ref input,target,distance,visible);return;}if(p.Seat.spec==NativeTopDog.Spec){BotDog(p,ref input,target,distance,visible);return;}if(p.Seat.spec==NativeGlaive.Spec){BotGlaive(p,ref input,target,distance,visible);return;}if(NativeBlades.Is(p.Seat.spec)){BotBlade(p,ref input,target,distance,visible);return;}
            if(p.Seat.spec=="cyberlord"){BotCyber(p,ref input,target,distance,visible);return;}
            if(NativeMeleeRules.Standard(p.Seat.spec)){BotMeleeAbilities(p,ref input,target,distance,visible,level);return;}
            if(!FighterReady(p)||p.Fan!=null||p.AbilityCast!=null||p.CastAt>0||Time<p.AttackEnd&&!(Dev(p,"emperor")&&p.Fighter?.Kind=="wave"))return;
            if(p.Fighter?.Kind=="charge"){input.HadokenHeld=visible&&Time-p.Fighter.At<1.45f&&p.Stamina>=30*p.Stats["resourceCost"];input.Shoot=input.Roll=false;return;}
            if(!visible)return;
            bool Ready(ContentAbility d)=>d!=null&&d.live&&d.kind!="ads"&&(!NativeRangedRules.Q(d.id)||RangedQReady(p,d))&&(d.slot=="q"?Time>=p.QReady:p.ECharges>0)&&p.Mana>=NativeAbilityRules.Cost(d,p.Stats)&&distance>=NativeBotRules.Ability(d.id).min&&distance<=NativeBotRules.Ability(d.id).max;
            var q=Content.Ability(p.Seat.spec,"q");var e=Content.Ability(p.Seat.spec,"e");var cast=q?.id=="tidal-wave"&&distance<6&&Ready(q)?q:Ready(e)?e:Ready(q)?q:null;
            if(cast!=null&&random.NextDouble()<=NativeBotRules.Chance(level))
            {
                var b=NativeBotRules.Ability(cast.id);Vector3 point=target+new Vector3(velocity.x,0,velocity.z)*b.lead*(NativeAbilityRules.CastTime(cast.windup,p.Stats)+(float)cast.fuse);if(level==3&&cast.id=="flame-lance")
                {
                    point=target+headOffset;
                    if(cast.speed>0)point+=velocity*(NativeAbilityRules.CastTime(cast.windup,p.Stats)+distance/(float)cast.speed);
                }
                if(b.point)point.y-=.8f;
                var aim=point-p.Body.position-Vector3.up*.5f;input.Yaw=Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg;input.Pitch=b.ballistic?GunRules.BallisticPitch(new Vector2(aim.x,aim.z).magnitude,aim.y,(float)(cast.speed>0?cast.speed:30),(float)cast.gravity):Mathf.Atan2(aim.y,new Vector2(aim.x,aim.z).magnitude)*Mathf.Rad2Deg;
                input.AbilityTarget=Vec(point);input.AbilityQ=cast.slot=="q";input.AbilityE=cast.slot=="e";input.Shoot=input.Roll=false;return;
            }
            if(p.Ready>Time||p.ReloadEnd>Time){input.Shoot=false;return;}
            if(random.NextDouble()>NativeBotRules.Chance(level))return;
            if(Dev(p,"emperor")){input.AbilityQ=distance<=2.7*p.Stats["reach"];input.AbilityEHeld=distance>3.3&&distance<11;}
            else if(Dev(p,"ansatsuken")){if(distance>=4&&distance<=25&&Time>=(p.Fighter?.HadokenReady??0))input.Hadoken=true;else if(distance<=3&&Time>=(p.Fighter?.UppercutReady??0))input.Uppercut=true;else if(distance<=4.5&&Time>=p.EReady)input.AbilityE=true;}
            else if(p.Weapon.@class=="melee"&&Time>=p.EReady&&distance<=(p.Seat.spec=="daggers"?22:p.Seat.spec=="shield"?20:p.Seat.spec=="greatsword"?5:(float)(p.Weapon.range*p.Stats["reach"])+.25f))input.AbilityE=true;
            if(input.AbilityQ||input.AbilityE||input.AbilityEHeld||input.Hadoken||input.Uppercut)input.Shoot=input.Roll=false;
        }
        private void BotMeleeAbilities(NativePilot p,ref InputFrame input,Vector3 target,float distance,bool visible,int level)
        {
            if(Incapacitated(p)||Time<p.BlockEnd||Time<p.RollEnd)return;
            if(p.Special?.Fx=="whirlwind")
            {input.AbilityE=distance>4&&MeleeRecastOpen(p);input.Shoot=input.Attack=input.Roll=input.Sprint=input.Jump=false;return;}
            if(MeleeRecastOpen(p)&&visible&&random.NextDouble()<NativeBotRules.Chance(level))
            {input.AbilityE=true;input.Shoot=input.Roll=false;return;}
            if(SpecialBusy(p)||Time<p.AttackEnd||Time<p.Ready||!visible)return;
            var q=Content.Ability(p.Seat.spec,"q");var e=Content.Ability(p.Seat.spec,"e");
            bool Ready(ContentAbility a)=>a?.bot!=null&&a.live&&(a.slot=="q"?Time>=p.QReady:Time>=p.EReady)&&p.Stamina>=NativeAbilityRules.Cost(a,p.Stats)&&distance>=a.bot.min&&distance<=a.bot.max;
            var cast=e?.id=="whirlwind"&&Ready(e)?e:Ready(q)?q:Ready(e)?e:null;if(cast==null||random.NextDouble()>NativeBotRules.Chance(level))return;
            input.AbilityTarget=Vec(target);input.AbilityQ=cast.slot=="q";input.AbilityE=cast.slot=="e";input.Shoot=input.Roll=false;
            if(cast.id=="shadow-step"){var d=(target-p.Body.position).normalized;input.X=d.x;input.Z=d.z;}
            if(cast.bot.ballistic){var d=target-p.Body.position-Vector3.up*.3f;input.Pitch=GunRules.BallisticPitch(new Vector2(d.x,d.z).magnitude,d.y,30*(float)p.Stats["projectileSpeed"],18*(float)p.Stats["projectileGravity"]);input.AbilityTarget=new V3();}
        }
        private InputFrame SirenDecision(NativePilot p,InputFrame input,Vector3 target,float distance,bool visible,int level)
        {
            var s=Siren(p);input.AbilityTarget=Vec(target-Vector3.up*.85f);input.Shoot=visible&&distance<(s.Ranged?28:2.6f);
            if(s.Move!=null||Time<p.Ready)return input;
            if(distance>8&&!s.Ranged||distance<3.5f&&s.Ranged){input.AbilityQ=true;input.Shoot=input.Roll=false;return input;}
            if(!visible||random.NextDouble()>NativeBotRules.Chance(level))return input;
            var names=s.Ranged?NativeSirenRules.Ranged:NativeSirenRules.Melee;var choices=new List<int>();
            for(int i=0;i<4;i++)if((!s.Ready.TryGetValue(names[i],out float ready)||Time>=ready)&&(s.Ranged?distance<=28:distance<=(i==0?3:i==1?3.6f:i==2?6:7)*s.Reach)&&!(names[i]=="mark"&&Marks().Any(m=>m.owner==p.Seat.id)))choices.Add(i+1);
            if(choices.Count>0){input.SirenAbility=(byte)choices[random.Next(choices.Count)];input.Shoot=input.Roll=false;}return input;
        }
    }
}
