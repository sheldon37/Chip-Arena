using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private sealed class ExpansionEcho {public NativePilot Owner;public NativeBullet Shot;public float At;public string Kind;public int Target;}
        private readonly List<ExpansionEcho> expansionEchoes=new List<ExpansionEcho>();
        private void ExpansionEchoShot(NativePilot p,Vector3 at,Vector3 direction,float damage,float delay,string kind,int target=0)
        {
            if(expansionEchoes.Count>=128)return;
            expansionEchoes.Add(new ExpansionEcho{Owner=p,At=Time+delay,Kind=kind,Target=target,Shot=new NativeBullet{Id=nextBullet++,Attack=nextAttack++,Owner=p.Seat.id,Original=p.Seat.id,Spec=p.Seat.spec,Detail="expansion-copy",Born=Time+delay,Position=at,Velocity=direction*100,BaseVelocity=direction*100,Damage=damage,Radius=.08f,Life=3,Travel=80,Direct=false,TechniqueNoHead=true}});
        }
        private void WildVolley(NativePilot p,List<NativeBullet> volley)
        {
            foreach(var b in volley)
            {
                b.ExpansionBaseDamage=b.Damage;b.ExpansionBaseRadius=b.Radius;b.ExpansionCurve=Has(p,"wild-curveball")?p.Wild.YawRate:0;
                if(Has(p,"wild-double-tap"))QueueWildShot(p,b,.3f,.35f,false,"wild-double-tap");
                if(Has(p,"wild-triple-tap")){QueueWildShot(p,b,.07f,1,true,"wild-triple-tap");QueueWildShot(p,b,.14f,1,true,"wild-triple-tap");}
                foreach(var twin in Minions.Where(m=>m.Owner==p&&m.Kind=="wild-evil-twin"&&m.Hp>0))
                {var dir=p.Input.AimTarget.Length>.01f?(Vec(p.Input.AimTarget)-twin.Body.position).normalized:b.Velocity.normalized;ExpansionEchoShot(p,twin.Body.position+dir*.4f,dir,b.Damage*.5f,0,"wild-evil-twin");twin.LastAttack=Time;}
            }
        }
        private void QueueWildShot(NativePilot p,NativeBullet b,float delay,float scale,bool primary,string kind)
        {
            if(expansionEchoes.Count>=128)return;var copy=CopyBullet(b);copy.Damage=b.Damage*scale;copy.ExpansionBaseDamage=copy.Damage;copy.ExpansionBaseRadius=copy.Radius;copy.ExpansionCurve=b.ExpansionCurve;copy.Born=Time+delay;copy.Direct=primary;
            if(!primary){copy.Mods.Clear();copy.Detail="expansion-copy";copy.ChipImpact=copy.WeaponFx=false;copy.Burn=copy.Poison=copy.Homing=0;copy.TechniqueNoHead=true;}
            expansionEchoes.Add(new ExpansionEcho{Owner=p,At=Time+delay,Shot=copy,Kind=kind});
        }
        private void StepWildEchoes()
        {
            foreach(var echo in expansionEchoes.Where(e=>e.At<=Time).ToArray())
            {expansionEchoes.Remove(echo);if(echo.Owner.Hp<=0||!echo.Owner.Seat.connected||ProjectileCount>=100)continue;if(echo.Target!=0){var t=MeleeTarget(echo.Owner,echo.Target);if(t.collider==null||SceneLinecast(echo.Shot.Position,t.at,WorldMask))continue;echo.Shot.Velocity=(t.at-echo.Shot.Position).normalized*echo.Shot.Velocity.magnitude;}Bullets.Add(echo.Shot);ChipProc(echo.Owner,echo.Kind,echo.Shot.Position,echo.Target,echo.Shot.Damage,direction:echo.Shot.Velocity.normalized,projectile:echo.Shot.Id);}
        }
        private void WildShotMotion(NativeBullet b,NativePilot p,float dt)
        {
            if(Has(b,"wild-steering-wheel")){var aim=p.Input.AimTarget.Length>.01f?Vec(p.Input.AimTarget):p.Body.position+Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch))*80;b.Velocity=Vector3.RotateTowards(b.Velocity, (aim-b.Position).normalized*b.Velocity.magnitude,240*Mathf.Deg2Rad*dt,0);}
            if(Has(b,"wild-curveball")&&Time-b.Born<1.5f){float turn=b.ExpansionCurve*dt;b.Velocity=Quaternion.Euler(0,turn,0)*b.Velocity;b.ExpansionCurveTotal+=Math.Abs(turn);if(b.ExpansionCurveTotal>=20&&!b.ExpansionCurveBoost){b.ExpansionCurveBoost=true;b.Damage*=1.2f;}}
            if(Has(b,"wild-snowball")){int steps=Math.Min(3,(int)(b.Travelled/10));if(steps>b.ExpansionSnow){int stacks=b.Mods.TryGetValue("wild-snowball",out int count)?count:1;float old=1+.3f*stacks*b.ExpansionSnow,next=1+.3f*stacks*steps;b.Damage*=next/old;b.Radius*=next/old;b.ExpansionSnow=steps;}}
            if(b.Detail=="spirit-hawk")
            {var t=MeleeTargets(p).Where(t=>Vector3.Distance(t.at,b.Position)<=12&&MeleeVisible(b.Position,t.at,p,t.collider)).OrderBy(t=>(t.at-b.Position).sqrMagnitude).FirstOrDefault();if(t.collider!=null)b.Velocity=Vector3.RotateTowards(b.Velocity,(t.at-b.Position).normalized*20,Mathf.PI*dt,0);}
        }
        private bool WildWall(NativeBullet b,NativePilot owner,NativeContact hit)
        {
            if(b.ExpansionWallUsed||!Has(b,"wild-wall-hack"))return false;
            var dir=b.Velocity.normalized;float span=hit.Collider.bounds.size.magnitude+1.6f;Vector3 far=hit.Point+dir*span;
            if(!hit.Collider.Raycast(new Ray(far,-dir),out var exit,span+.01f))return false;float thickness=span-exit.distance;
            if(thickness<0||thickness>1.5f||HubCross(hit.Point,exit.point))return false;
            b.Position=exit.point+dir*(b.Radius+.03f);b.Travel-=thickness;b.Travelled+=thickness;b.ExpansionWallUsed=true;return true;
        }
        private void WildBounce(NativeBullet b,NativePilot owner)
        {
            b.ExpansionBounces++;
            if(Has(b,"wild-trick-shot")){int stacks=b.Mods.TryGetValue("wild-trick-shot",out int count)?count:1;float before=1+.35f*stacks*(b.ExpansionBounces-1);b.Damage=Math.Min(1000,b.Damage*(1+.35f*stacks*b.ExpansionBounces)/before);}else b.Damage=Math.Min(1000,b.Damage*b.BounceRetention);
            if(Has(b,"wild-bank-error")){var t=MeleeTargets(owner).Where(t=>Vector3.Distance(t.at,b.Position)<=25&&MeleeVisible(b.Position,t.at,owner,t.collider)).OrderBy(t=>(t.at-b.Position).sqrMagnitude).FirstOrDefault();if(t.collider!=null){b.Velocity=(t.at-b.Position).normalized*b.Velocity.magnitude;ChipProc(owner,"wild-bank-error",b.Position,t.id,b.Damage,direction:b.Velocity.normalized,projectile:b.Id);}}
        }
        private void WildMeleeAttack(NativePilot p)
        {
            var swing=p.Swing;if(swing==null)return;swing.DamageScale*=WildAttackMultiplier(p);
            float damage=(float)(p.Weapon.damage*p.Stats["damage"])*swing.DamageScale;
            if(Has(p,"wild-double-tap"))ExpansionAreaAdd(p,"melee-echo",p.Body.position,Math.Max(0,swing.HitAt-Time)+.3f,0,damage*.35f,(float)(p.Weapon.range*p.Stats["reach"]),p.Body.position+Vec(V3.Forward(p.Yaw)));
            foreach(var twin in Minions.Where(m=>m.Owner==p&&m.Kind=="wild-evil-twin"&&m.Hp>0))ExpansionAreaAdd(p,"melee-echo",twin.Body.position,Math.Max(0,swing.HitAt-Time),0,damage*.5f,(float)(p.Weapon.range*p.Stats["reach"]),twin.Body.position+Vec(V3.Forward(p.Yaw)));
        }
    }
}
