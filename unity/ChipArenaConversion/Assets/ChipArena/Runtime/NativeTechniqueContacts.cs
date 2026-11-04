using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Virtual shot-only obstacles: players can walk through Bastion and a sentry without a solid capsule collider.
        private bool TechniqueObstacle(NativePilot shooter,Vector3 from,Vector3 delta,float radius,float damage,out Vector3 contact)
        {
            contact=from+delta;bool any=false;foreach(var p in Pilots)if(p.TechniqueHealth>0&&NativeTechniqueRules.Live(p.Technique,Time)&&(p.Technique.Kind=="bastion"||p.Technique.Kind=="sentry-drop")){any=true;break;}if(!any)return false;NativePilot best=null;float nearest=1.001f;
            // Geometry and actors must win when closer; do not let a wall behind a victim consume its shot.
            var solid=SweepNative(from,delta,radius,c=>c!=shooter?.Collider&&(c.GetComponent<NativePilotBody>()?.Pilot is NativePilot q?q.Hp>0&&CanHit(shooter,q)&&!Invulnerable(q):c.GetComponent<NativeMinionBody>()?.Minion is NativeMinion m?m.Hp>0&&CanHit(shooter,m):true));
            if(solid.HasValue)nearest=solid.Value.Distance/Math.Max(.00001f,delta.magnitude);
            foreach(var p in Pilots)
            {
                var k=p.Technique;if(p.Hp<=0||!NativeTechniqueRules.Live(k,Time)||p.TechniqueHealth<=0)continue;float f;
                bool hit=k.Kind=="bastion"?NativeTechniqueRules.Wall(Vec(from),Vec(delta),k,radius,out f):k.Kind=="sentry-drop"&&p!=shooter&&CanHit(shooter,p)?NativeTechniqueRules.SegmentSphere(Vec(from),Vec(delta),k.Point+new V3(0,.65f,0),.55f+radius,out f):NoTechniqueContact(out f);
                if(hit&&f<nearest){nearest=f;best=p;}
            }
            if(best==null)return false;contact=from+delta*nearest;best.TechniqueHealth=Math.Max(0,best.TechniqueHealth-damage);best.Technique.HitAt=Time;
            if(best.Technique.Kind=="bastion"){best.Technique.Arm=best.TechniqueHealth/120;best.Technique.To=Vec(contact);}
            if(best.TechniqueHealth<=0)EndTechnique(best);return true;
        }
        private static bool NoTechniqueContact(out float f){f=0;return false;}
        private bool TechniqueBullet(NativeBullet b,NativePilot owner,Vector3 delta)
        {
            Vector3 point;
            bool coinAvailable=false;foreach(var p in Pilots)if(p.Technique?.Kind=="ricochet-coin"&&p.Technique.HitAt<-50&&NativeTechniqueRules.Live(p.Technique,Time)){coinAvailable=true;break;}
            if(!coinAvailable||b.TechniqueCoinUsed||b.Detail=="sentry"||Content.Weapon(b.Spec).@class!="ranged")
            {if(TechniqueObstacle(owner,b.Position,delta,b.Radius,b.Damage,out point)){b.Position=point;EndBullet(b);return true;}return false;}
            var solid=SweepNative(b.Position,delta,b.Radius,c=>c!=owner.Collider);float closest=solid.HasValue?solid.Value.Distance/Math.Max(.00001f,delta.magnitude):1.001f;NativePilot coin=null;
            foreach(var p in Pilots)
            {var k=p.Technique;if(k?.Kind!="ricochet-coin"||!NativeTechniqueRules.Live(k,Time)||k.HitAt>-50)continue;var pos=NativeTechniques.CoinAt(k,Time);if(NativeTechniqueRules.SegmentSphere(Vec(b.Position),Vec(delta),new V3(pos.X,pos.Y,pos.Z),.6f+b.Radius,out float f)&&f<closest){closest=f;coin=p;}}
            if(TechniqueObstacle(owner,b.Position,delta*Math.Min(1,closest),b.Radius,b.Damage,out point)){b.Position=point;EndBullet(b);return true;}
            if(coin==null)return false;
            point=b.Position+delta*closest;var target=TechniqueTarget(owner,point,22);var record=coin.Technique;record.HitAt=Time;record.Point=Vec(point);record.To=Vec(target.id==0?point:target.at);record.Until=Time;
            b.Position=point;b.TechniqueCoinUsed=true;
            if(target.id!=0){b.Damage*=1.6f;b.TechniqueNoHead=true;b.Homing=0;b.Boomerang=b.Zigzag=false;b.Gravity=0;b.Velocity=(target.at-point).normalized*Math.Max(1,b.Velocity.magnitude);b.BaseVelocity=b.Velocity;b.Hit.Clear();}
            else b.Position+=delta.normalized*.05f;
            return true;
        }
        private void TechniqueMeleeObjects(NativePilot p,NativeMeleeSwing swing,Vector3 origin)
        {
            foreach(var q in Pilots)
            {var k=q.Technique;if(q==p||k?.Kind!="sentry-drop"||!NativeTechniqueRules.Live(k,Time)||q.TechniqueHealth<=0||!CanHit(p,q))continue;var at=Vec(k.Point)+Vector3.up*.6f;var delta=at-origin;if(!swing.Contact("sentry:"+q.Seat.id,Time,Vec(delta),.55f,MeleeVisible(origin,at,p,null),p.Grounded))continue;q.TechniqueHealth-=TechniqueAway(p)?6:(float)(p.Weapon.damage*p.Stats["damage"]*swing.DamageScale);k.HitAt=Time;if(q.TechniqueHealth<=0)EndTechnique(q);}
        }
        private void TechniqueBlastObjects(NativePilot owner,Vector3 at,float radius,float damage)
        {
            foreach(var p in Pilots)
            {var k=p.Technique;if(k?.Kind!="sentry-drop"||!NativeTechniqueRules.Live(k,Time)||p.TechniqueHealth<=0||!CanHit(owner,p))continue;var point=Vec(k.Point)+Vector3.up*.65f;float fall=NativeAbilityRules.BlastFalloff(Vector3.Distance(at,point),radius);if(fall<=0||!MeleeVisible(at,point,owner,null))continue;p.TechniqueHealth-=damage*fall;k.HitAt=Time;if(p.TechniqueHealth<=0)EndTechnique(p);}
        }
        private bool BotTechnique(NativePilot p,ref InputFrame input,Vector3 target,float distance,bool visible,int level)
        {
            var def=NativeTechniques.For(p.Seat.spec);if(def==null||!visible||TechniqueBlocked(p))return false;
            var k=p.Technique;
            if(NativeTechniqueRules.Recast(k,Time)&&!TechniqueBusy(p))
            {bool recast=k.Kind=="anchor-throw"&&distance>3||k.Kind=="static-recall"&&(p.Hp<p.Stats["hp"]*.45||distance<4);if(recast){input.SirenAbility=1;input.AbilityQ=input.AbilityE=input.Attack=input.Shoot=input.Roll=input.Block=false;return true;}}
            if(k?.Kind=="ricochet-coin"&&NativeTechniqueRules.Live(k,Time)&&k.HitAt<-50)
            {var coin=NativeTechniques.CoinAt(k,Time+.04f);var aim=new Vector3(coin.X,coin.Y,coin.Z)-p.Body.position-Vector3.up*.3f;input.Yaw=Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg;input.Pitch=Mathf.Atan2(aim.y,new Vector2(aim.x,aim.z).magnitude)*Mathf.Rad2Deg;input.AimTarget=new V3(coin.X,coin.Y,coin.Z);input.Shoot=true;input.AbilityQ=input.AbilityE=false;return true;}
            if(Time<p.TechniqueReady||NativeTechniqueRules.Live(k,Time)||TechniqueBusy(p)||random.NextDouble()>NativeBotRules.Chance(level)*.15)return false;
            bool use=def.Id=="riposte"?distance<4||input.Block:def.Id=="healing-spring"?p.Hp<p.Stats["hp"]*.75:def.Id=="smoke-veil"?p.Hp<p.Stats["hp"]*.6:def.Id=="ignition-leap"?distance<6:def.Id=="anchor-throw"?distance>3&&distance<16:def.Id=="shade-swap"?distance>4&&distance<18:distance<18;
            if(!use)return false;input.SirenAbility=1;input.AbilityQ=input.AbilityE=input.Attack=input.Shoot=input.Roll=input.Block=false;
            if(def.Id=="healing-spring"||def.Id=="gale-arrow"){input.Pitch=-80;input.AbilityTarget=Vec(p.Body.position-Vector3.up*.865f);}
            if(def.Id=="grapple-line")input.Pitch=25;
            return true;
        }
        private bool BotSentry(NativePilot p,out InputFrame input)
        {
            input=default;var enemy=Pilots.Where(q=>q!=p&&Enemy(p,q)&&q.Technique?.Kind=="sentry-drop"&&NativeTechniqueRules.Live(q.Technique,Time)&&q.TechniqueHealth>0&&Vector3.Distance(p.Body.position,Vec(q.Technique.Point))<24&&MeleeVisible(p.Body.position,Vec(q.Technique.Point)+Vector3.up*.65f,p,null)).OrderBy(q=>(Vec(q.Technique.Point)-p.Body.position).sqrMagnitude).FirstOrDefault();
            if(enemy==null)return false;input=BotFocus(p,Vec(enemy.Technique.Point)+Vector3.up*.65f,Vector3.zero,null,null,false);return true;
        }
    }
}
