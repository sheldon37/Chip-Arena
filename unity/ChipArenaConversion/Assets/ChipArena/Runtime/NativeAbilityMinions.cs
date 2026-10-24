using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private struct AbilityContactHit {public Collider collider;public Vector3 point,normal;public float distance;public static implicit operator AbilityContactHit(RaycastHit h)=>new AbilityContactHit{collider=h.collider,point=h.point,normal=h.normal,distance=h.distance};}
        private void SurfaceImpact(AbilityContactHit hit,NativePilot source,int projectile,int color)
        {var prop=hit.collider.GetComponent<NativePropBody>();if(prop!=null)Events.Add(new ArenaEvent{Id=nextEvent++,Kind=EventKind.Impact,Source=source?.Seat.id??0,Time=Time,Position=Vec(hit.point),Normal=Vec(hit.normal),SurfaceId=prop.Definition.id,Color=color,Projectile=projectile});}
        private IEnumerable<AbilityContactHit> ProjectileHits(Vector3 from,float radius,Vector3 delta)
        {
            var hits=new List<AbilityContactHit>();var scene=root.scene.GetPhysicsScene();int count;
            while((count=scene.OverlapSphere(from,radius,shotOverlaps,ShotMask,QueryTriggerInteraction.Ignore))==shotOverlaps.Length)Array.Resize(ref shotOverlaps,shotOverlaps.Length*2);
            for(int i=0;i<count;i++){var c=shotOverlaps[i];Vector3 at=c.ClosestPoint(from),normal=(from-at).normalized;if(normal.sqrMagnitude<.001f)normal=-delta.normalized;hits.Add(new AbilityContactHit{collider=c,point=at,normal=normal});}
            if(delta.sqrMagnitude>1e-12f){while((count=scene.SphereCast(from,radius,delta.normalized,shotSweeps,delta.magnitude,ShotMask,QueryTriggerInteraction.Ignore))==shotSweeps.Length)Array.Resize(ref shotSweeps,shotSweeps.Length*2);for(int i=0;i<count;i++){var h=shotSweeps[i];if(!hits.Any(x=>x.collider==h.collider))hits.Add(new AbilityContactHit{collider=h.collider,point=h.point,normal=h.normal,distance=h.distance});}}
            return hits.OrderBy(h=>h.distance).ThenBy(h=>h.collider.gameObject.layer==PropLayer?0:1).ToArray();
        }
        private IEnumerable<(NativePilot p,NativeMinion m,Vector3 at)> NearAbilityEnemies(NativePilot owner,Vector3 at,float radius,int limit)
        {
            var all=new List<(NativePilot p,NativeMinion m,Vector3 at,Collider collider)>();
            foreach(var p in Pilots)if(p.Hp>0&&p.Seat.connected&&Enemy(owner,p)&&Vector3.Distance(p.Body.position,at)<=radius)all.Add((p,null,p.Body.position,p.Collider));
            foreach(var m in Minions)if(m.Hp>0&&Enemy(owner,m)&&Vector3.Distance(m.Body.position,at)<=radius)all.Add((null,m,m.Body.position,m.Collider));
            int hits=0;foreach(var n in all.OrderBy(n=>(n.at-at).sqrMagnitude).Take(4))if(!HubCross(at,n.at)&&MeleeVisible(at,n.at,owner,n.collider)){yield return(n.p,n.m,n.at);if(++hits>=limit)yield break;}
        }
        private void SlowMinion(NativeMinion m,float factor,float duration)
        {if(m.Hp<=0||duration<=0)return;if(Time>=m.SlowUntil)m.SlowFactor=1;m.SlowFactor=Math.Min(m.SlowFactor,Mathf.Clamp(factor,.4f,1));m.SlowUntil=Math.Max(m.SlowUntil,Time+duration);if(m.Pve!=null){m.Pve.SlowUntil=m.SlowUntil;m.Pve.SlowFactor=m.SlowFactor;}}
        private void BurnMinion(NativeMinion m,NativePilot owner,float dps)
        {if(m.Hp<=0||dps<=0||HubDamageBlocked(owner,m.Body.position))return;string key=owner.Seat.id+":fire";m.Dots.TryGetValue(key,out var old);bool live=old!=null&&old.Until>=Time;m.Dots[key]=new NativeDot{HubNoReward=HubInside(owner.Body.position),Source=owner.Seat.id,Dps=Math.Max(dps,live?old.Dps:0),Next=live?old.Next:Time+.25,Until=Time+2};}
        private float AbilityMinionHit(NativeAbilityShot shot,NativeMinion target,float damage,Vector3 at,Vector3 dir,float impulse,bool life=false)
        {float actual=HitMinion(target,shot.Owner,damage,dir,impulse,at,"energy",from:shot.Position);AbilityOnDamage(shot.Owner,actual);if(life)Heal(shot.Owner,actual*(float)shot.Owner.Stats["lifeSteal"]);return actual;}
        private bool AbilityMinionContact(NativeAbilityShot shot,NativeMinion target,AbilityContactHit hit)
        {
            var d=shot.Definition;var owner=shot.Owner;Vector3 at=hit.point+hit.normal*.03f;int element=owner.Pose.MagicVisual;
            if(d.id=="frag-grenade"){AbilityBlast(owner,at,NativeAbilityRules.Radius(d,owner.Stats),shot.Damage,(float)d.impulse,element,effect:d.fx??d.id);return true;}
            if(d.id=="rocket"){if(shot.Travel>=d.arm)AbilityBlast(owner,at,NativeAbilityRules.Radius(d,owner.Stats),shot.Damage,(float)d.impulse,element,2,effect:d.fx??d.id);else AbilityMinionHit(shot,target,shot.Damage*15/55,at,shot.Velocity.normalized,150,true);Fx(at,.3f,element);return true;}
            if(d.id=="orbital-mark"){Vector3 pos=target.Body.position;AbilityMinionHit(shot,target,shot.Damage*10/85,at,shot.Velocity.normalized,150,true);AbilityZones.Add(new NativeAbilityZone{Id=nextEvent++,Born=Time,Owner=owner,Definition=d,AttachedMinion=target,LockAt=Time+(float)d.duration,Position=pos,Kind="bomb",Radius=NativeAbilityRules.Radius(d,owner.Stats),Damage=shot.Damage,Until=Time+(float)d.fuse});return true;}
            shot.Hit.Add(target.Id);if(d.id=="ball-lightning")return false;
            Vector3 impulse=shot.Velocity.normalized*(d.id=="tidal-wave"&&Has(owner,"undertow")?-700:(float)d.impulse)+(d.id=="tidal-wave"?Vector3.up*200:Vector3.zero);
            float actual=AbilityMinionHit(shot,target,shot.Damage,at,impulse.normalized,impulse.magnitude,d.id=="flame-lance"||d.id=="shadow-shackles");
            if(actual>0){if(d.burn>0)BurnMinion(target,owner,(float)(d.burn*shot.Damage/d.damage));if(d.speedFactor>0)SlowMinion(target,(float)d.speedFactor,(float)d.slowFor);if(d.id=="shadow-shackles")Heal(owner,actual*.5f);}
            Fx(at,.6f,element);return d.id!="tidal-wave"&&!(d.id=="flame-lance"&&shot.RemainingPierces-->0);
        }
    }
}
