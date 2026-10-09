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
            var target=Pilots.Where(q=>q.Hp>0&&q.Seat.connected&&Enemy(p,q)).OrderBy(q=>(q.Body.position-p.Body.position).sqrMagnitude).FirstOrDefault();
            if(target==null||(StationaryBots||p.Seat.difficulty=="dummy")&&!force)return new InputFrame{Yaw=p.Yaw};
            var summon=Minions.Where(m=>m.Pve==null&&m.Hp>0&&Enemy(p,m)&&Vector3.Distance(m.Body.position,p.Body.position)<Math.Min(6,Vector3.Distance(target.Body.position,p.Body.position)-1.5f)).OrderBy(m=>(m.Body.position-p.Body.position).sqrMagnitude).FirstOrDefault();
            return BotFocus(p,summon?.Body.position??target.Body.position,summon!=null?MinionVelocity(summon):PilotVelocity(target),summon?.Collider??target.Collider,target,force);
        }
        private static Vector3 PilotVelocity(NativePilot p)=>p.Body.isKinematic?Vector3.zero:p.Body.linearVelocity;
        private static Vector3 MinionVelocity(NativeMinion m)=>m.Body.isKinematic?Vector3.zero:m.Body.linearVelocity;
        private InputFrame BotFocus(NativePilot p,Vector3 target,Vector3 targetVelocity,Collider collider,NativePilot opponent=null,bool force=false)
        {
            if(!force&&(p.Seat.difficulty=="dummy"||StationaryBots))return new InputFrame{Yaw=p.Yaw};
            int level=NativeBotRules.Level(p.Seat.difficulty);var pos=p.Body.position;var eye=pos+Vector3.up*.5f;var delta=target-pos;float distance=delta.magnitude;
            Vector3 aimAt=Vec(p.BotBrain.Observe(Time,p.Seat.difficulty,Vec(target),random.NextDouble)),aim=aimAt-eye;bool visible=MeleeVisible(eye,target,p,collider),melee=p.Weapon.@class=="melee";
            var input=new InputFrame{Yaw=Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg,Pitch=Mathf.Atan2(aim.y,new Vector2(aim.x,aim.z).magnitude)*Mathf.Rad2Deg,AimTarget=Vec(aimAt),AbilityTarget=Vec(aimAt),Aim=visible&&NativeBotRules.Ads(p.Seat.spec,distance)};
            if(!melee&&!input.Aim){float pitch=GunRules.BallisticPitch(new Vector2(aim.x,aim.z).magnitude,aim.y,(float)(p.Weapon.speed*p.Stats["projectileSpeed"]),(float)(p.Weapon.gravity*p.Stats["projectileGravity"]));input.Pitch=Mathf.Lerp(input.Pitch,pitch,level==2?.95f:level==1?.85f:.65f);}
            float desired=NativeBotRules.Desired(p.Weapon);var flat=new Vector3(delta.x,0,delta.z).normalized;Vector3 move=distance>desired+1?flat:distance<desired-1?-flat*.8f:Vector3.zero;
            if(level>0&&!melee&&visible)move+=Quaternion.Euler(0,input.Yaw+90,0)*Vector3.forward*(Mathf.Sin(Time*1.1f+p.Seat.id)>0?.65f:-.65f);
            if(level>0&&!melee&&(p.ReloadEnd>Time||p.Weapon.ammo>0&&p.Ammo<=1||p.Mana<18))
            {var cover=Props.Where(c=>c!=null&&!c.Fragment&&!c.Definition.dynamic&&c.Definition.material!="foundation"&&c.Definition.size.y>1).OrderBy(c=>(c.transform.position-pos).sqrMagnitude).FirstOrDefault();if(cover!=null){var safe=cover.transform.position+(cover.transform.position-target).normalized*(Math.Max(cover.Definition.size.x,cover.Definition.size.z)*.6f+1);move=Vector3.Distance(safe,pos)<1?Vector3.zero:new Vector3(safe.x-pos.x,0,safe.z-pos.z).normalized;}}
            bool obstacle=move.sqrMagnitude>.01f&&root.scene.GetPhysicsScene().Raycast(pos-Vector3.up*.4f,move.normalized,out _,1.1f,WorldMask,QueryTriggerInteraction.Ignore);
            if(obstacle){move=move*.25f+Quaternion.Euler(0,input.Yaw+90,0)*Vector3.forward*(Mathf.Sin(Time*.4f)>0?1:-1);input.Jump=p.Grounded;}
            if(melee&&p.Stamina<p.Weapon.cost)move=-flat*.8f;
            if(Math.Abs(pos.x)>28)move.x=-Math.Sign(pos.x);if(Math.Abs(pos.z)>24)move.z=-Math.Sign(pos.z);
            input.X=Mathf.Clamp(move.x,-1,1);input.Z=Mathf.Clamp(move.z,-1,1);input.Shoot=melee?distance<p.Weapon.range*1.25:visible||obstacle;input.Reload=p.Weapon.ammo>0&&(p.Ammo==0||p.Seat.spec!="rifle"&&p.Ammo<2);
            input.Roll=level>0&&melee&&distance>4&&distance<11&&Math.Abs(pos.x)<28&&Math.Abs(pos.z)<24;
            if(p.BotBrain.Reacted&&Time>=p.BlockReady)
            {
                bool threat=opponent?.Swing!=null&&distance<opponent.Weapon.range+1&&opponent.Swing.HitAt-Time<.24&&visible;
                foreach(var shot in Bullets){var owner=Pilots.Find(q=>q.Seat.id==shot.Owner);if(!CanHit(owner,p))continue;Vector3 d=pos-shot.Position;float eta=Vector3.Dot(d,shot.Velocity.normalized)/Math.Max(1,shot.Velocity.magnitude);if(eta>0&&eta<(level==2?.24f:level==1?.3f:.38f)&&(d-shot.Velocity*eta).magnitude<.9f&&!SceneLinecast(shot.Position,pos,WorldMask)){threat=true;break;}}
                input.Block=threat&&random.NextDouble()<(level==2?.85:level==1?.65:.35);
            }
            if(input.Block){input.Shoot=input.Roll=false;return input;}
            if(p.Seat.spec=="siren")return SirenDecision(p,input,target,distance,visible,level);
            BotAbilities(p,ref input,target,targetVelocity,distance,visible,level);
            if((pos-p.BotPrevious).sqrMagnitude<.000025f&&move.magnitude>.2f)p.BotBlocked+=1f/60;else p.BotBlocked=0;p.BotPrevious=pos;
            if(p.BotBlocked>.65f){input.Jump=true;input.X=Mathf.Sin(Time*3);input.Z=Mathf.Cos(Time*3);}return input;
        }
        private void BotAbilities(NativePilot p,ref InputFrame input,Vector3 target,Vector3 velocity,float distance,bool visible,int level)
        {
            if(!FighterReady(p)||p.AbilityCast!=null||p.CastAt>0||Time<p.AttackEnd)return;
            if(p.Fighter?.Kind=="charge"){input.HadokenHeld=visible&&Time-p.Fighter.At<1.45f&&p.Stamina>=30*p.Stats["resourceCost"];input.Shoot=input.Roll=false;return;}
            if(!visible)return;
            bool Ready(ContentAbility d)=>d!=null&&d.live&&d.kind!="ads"&&(d.slot=="q"?Time>=p.QReady:p.ECharges>0)&&p.Mana>=NativeAbilityRules.Cost(d,p.Stats)&&distance>=NativeBotRules.Ability(d.id).min&&distance<=NativeBotRules.Ability(d.id).max;
            var q=Content.Ability(p.Seat.spec,"q");var e=Content.Ability(p.Seat.spec,"e");var cast=q?.id=="tidal-wave"&&distance<6&&Ready(q)?q:Ready(e)?e:Ready(q)?q:null;
            if(cast!=null&&random.NextDouble()<=NativeBotRules.Chance(level))
            {
                var b=NativeBotRules.Ability(cast.id);Vector3 point=target+new Vector3(velocity.x,0,velocity.z)*b.lead*(NativeAbilityRules.CastTime(cast.windup,p.Stats)+(float)cast.fuse);if(b.point)point.y-=.8f;
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
