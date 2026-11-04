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
            contact=from+delta;bool any=false;foreach(var p in Pilots)if(p.TechniqueHealth>0&&NativeTechniqueRules.Live(p.Technique,Time)&&(p.Technique.Kind=="bastion"||TechniqueObject(p.Technique))){any=true;break;}if(!any&&!Pilots.Any(p=>p.BombFlights.Any(b=>b.Record.Kind==NativeBombs.Mine)))return false;NativePilot best=null;BombFlight mine=null;NativePilot mineOwner=null;float nearest=1.001f;
            // Geometry and actors must win when closer; do not let a wall behind a victim consume its shot.
            var solid=SweepNative(from,delta,radius,c=>c!=shooter?.Collider&&(c.GetComponent<NativePilotBody>()?.Pilot is NativePilot q?q.Hp>0&&CanHit(shooter,q)&&!Invulnerable(q):c.GetComponent<NativeMinionBody>()?.Minion is NativeMinion m?m.Hp>0&&CanHit(shooter,m):true));
            if(solid.HasValue)nearest=solid.Value.Distance/Math.Max(.00001f,delta.magnitude);
            foreach(var p in Pilots)foreach(var b in p.BombFlights)if(b.Record.Kind==NativeBombs.Mine&&Time>=b.Record.At&&NativeTechniqueRules.SegmentSphere(Vec(from),Vec(delta),b.Record.Pos,.22f+radius,out float fraction)&&fraction<nearest){nearest=fraction;mine=b;mineOwner=p;}
            foreach(var p in Pilots)
            {
                var k=p.Technique;if(p.Hp<=0||!NativeTechniqueRules.Live(k,Time)||p.TechniqueHealth<=0)continue;float f;
                bool hit=k.Kind=="bastion"?NativeTechniqueRules.Wall(Vec(from),Vec(delta),k,radius,out f):TechniqueObject(k)&&p!=shooter&&CanHit(shooter,p)&&Time>=k.Release&&(k.Kind!="lightning-rod"||Time>=k.Arm)?NativeTechniqueRules.SegmentSphere(Vec(from),Vec(delta),k.Point+new V3(0,TechniqueObjectHeight(k),0),(k.Kind=="lightning-rod"?3:k.Kind=="sentry-drop"?.55f:.25f)+radius,out f):NoTechniqueContact(out f);
                if(hit&&f<nearest&&(k.Kind!="lightning-rod"||Enemy(shooter,p)&&MeleeVisible(from+delta*f,Vec(k.Point)+Vector3.up*.6f,shooter,null))){nearest=f;best=p;mine=null;}
            }
            if(mine!=null){contact=from+delta*nearest;mine.Hp-=damage;if(mine.Hp<=0)ExplodeBomb(mineOwner,mine);return true;}
            if(best==null)return false;contact=from+delta*nearest;DamageTechniqueObject(best,damage,contact,true);return true;
        }
        private static bool TechniqueObject(NativeTechnique k)=>k!=null&&(k.Kind=="sentry-drop"||k.Kind=="tripwire"||k.Kind=="sonar-dart"||k.Kind=="lightning-rod");
        private static float TechniqueObjectHeight(NativeTechnique k)=>k.Kind=="sentry-drop"?.65f:k.Kind=="lightning-rod"?.6f:0;
        private void DamageTechniqueObject(NativePilot p,float damage,Vector3 contact,bool shot=false)
        {
            var k=p.Technique;p.TechniqueHealth=Math.Max(0,p.TechniqueHealth-damage);
            if(k.Kind=="bastion"){k.Arm=p.TechniqueHealth/120;k.To=Vec(contact);k.HitAt=Time;}
            if(k.Kind=="sentry-drop")k.HitAt=Time;
            if(k.Kind=="lightning-rod"&&shot){k.To=Vec(contact);k.HitAt=Time;}
            if(p.TechniqueHealth>0)return;
            if(k.Kind=="tripwire"){k.HitAt=Time;k.To=k.Point;k.Phase="snap";k.Until=Time+.85f;}
            else{EndTechnique(p);if(k.Kind=="sonar-dart")k.HitAt=k.Until=Time;}
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
            {var k=q.Technique;if(q==p||!TechniqueObject(k)||!NativeTechniqueRules.Live(k,Time)||q.TechniqueHealth<=0||!CanHit(p,q))continue;var at=Vec(k.Point)+Vector3.up*TechniqueObjectHeight(k);var delta=at-origin;if(!swing.Contact("sentry:"+q.Seat.id,Time,Vec(delta),.55f,MeleeVisible(origin,at,p,null),p.Grounded))continue;DamageTechniqueObject(q,TechniqueAway(p)?6:(float)(p.Weapon.damage*p.Stats["damage"]*swing.DamageScale),at);}
        }
        private void TechniqueBlastObjects(NativePilot owner,Vector3 at,float radius,float damage)
        {
            BombTouchMines(owner,at,radius);
            foreach(var p in Pilots)
            {var k=p.Technique;if(!TechniqueObject(k)||!NativeTechniqueRules.Live(k,Time)||p.TechniqueHealth<=0||!CanHit(owner,p))continue;var point=Vec(k.Point)+Vector3.up*TechniqueObjectHeight(k);float fall=NativeAbilityRules.BlastFalloff(Vector3.Distance(at,point),radius);if(fall<=0||!MeleeVisible(at,point,owner,null))continue;DamageTechniqueObject(p,damage*fall,point);}
        }
        private bool BotSentry(NativePilot p,out InputFrame input)
        {
            input=default;var enemy=Pilots.Where(q=>q!=p&&Enemy(p,q)&&q.Technique?.Kind=="sentry-drop"&&NativeTechniqueRules.Live(q.Technique,Time)&&q.TechniqueHealth>0&&Vector3.Distance(p.Body.position,Vec(q.Technique.Point))<24&&MeleeVisible(p.Body.position,Vec(q.Technique.Point)+Vector3.up*.65f,p,null)).OrderBy(q=>(Vec(q.Technique.Point)-p.Body.position).sqrMagnitude).FirstOrDefault();
            if(enemy==null)return false;input=BotFocus(p,Vec(enemy.Technique.Point)+Vector3.up*.65f,Vector3.zero,null,null,false);return true;
        }
    }
}
