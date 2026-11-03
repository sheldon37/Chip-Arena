using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private BackroomsLayout EntityDarkLayout=>GeneratedRun?.Backrooms?.Layout;
        private void WakeEntity(NativeMinion m)
        {
            var z=m.Pve.Zone;if(!z.dormant)return;z.dormant=false;z.wakeAt=Time;
            ZoneStart(m,m.Pve.Stats.id=="hound"?"howl":"wake",m.Pve.Stats.id=="hound"?.8f:m.Pve.Stats.id=="tiler"?.6f:.45f,1);
            m.Pve.ZoneBusy=m.Pve.ZoneStrike;m.Pve.RecoverAt=m.Pve.ZoneBusy;
        }
        private void InitBackroomsEnemy(NativeMinion m)
        {
            if(!NativeBackroomsKinds.Own(m.Pve.Stats.id))return;
            m.Pve.Zone.dormant=m.Pve.Stats.id=="faceling"||m.Pve.Stats.id=="hound"||m.Pve.Stats.id=="tiler";
            m.Pve.ZoneReady=m.Pve.ActiveAt+1;m.Pve.ZoneCallReady=m.Pve.ActiveAt+3;
        }
        private bool EntityObserved(Vector3 at)
        {
            foreach(var p in Pilots)if(ZonePilot(p))
            {var d=at-p.Body.position;if(d.magnitude<2)return true;if(Vector3.Dot(Vec(V3.Forward(p.Yaw)),d.normalized)>.35f&&MeleeVisible(p.Body.position,at,p,null,true))return true;}
            return false;
        }
        private bool EntityClear(NativeMinion m,Vector3 at)
        {
            return !Pilots.Any(p=>ZonePilot(p)&&Vector3.Distance(p.Body.position,at)<2)&&!Minions.Any(n=>n!=m&&n.Hp>0&&Vector3.Distance(n.Body.position,at)<1)
                &&ExpeditionSupported(Vec(at-Vector3.up*m.Collider.height/2),m.Collider.radius,m.Collider.height);
        }
        private Vector3 EntityMove(NativeMinion m,Vector3 move)
        {
            if(m.Pve.Stats.id=="smiler"&&!NativeBackroomsKinds.DarkPath(EntityDarkLayout,Vec(m.Body.position),Vec(m.Body.position+move/60)))return Vector3.zero;
            return move;
        }
        private float EntitySpeed(NativeMinion m)
        {
            if(m.Pve.Stats.id!="drowned")return PoolMove(m.Body.position,m.Collider.height/2);
            float depth=NativePoolroomsWater.Depth(Poolrooms,Vec(m.Body.position-Vector3.up*m.Collider.height/2));
            return depth>.8f?3.2f:depth>0?1.6f:1;
        }
        private void EntityTravel(NativeMinion m,float speed)
        {
            var delta=Vec(V3.Forward(m.Yaw))*speed/60;
            if(m.Body.SweepTest(delta.normalized,out var hit,delta.magnitude+.05f,QueryTriggerInteraction.Ignore))delta=Vector3.zero;
            if(!SupportedStageMove(m.Body.position-Vector3.up*m.Collider.height/2,delta*60))delta=Vector3.zero;
            delta=EntityMove(m,delta*60)/60;m.Body.position+=delta;m.Body.linearVelocity=Vector3.zero;
        }
        private void EntityDrag(NativePilot pilot,Vector3 goal,float speed)
        {
            var delta=Vector3.ClampMagnitude(goal-pilot.Body.position,speed/60);
            if(delta.sqrMagnitude>.0001f&&!pilot.Body.SweepTest(delta.normalized,out _,delta.magnitude+.05f,QueryTriggerInteraction.Ignore))pilot.Body.position+=delta;
            pilot.MoveVelocity=Vector3.zero;pilot.Body.linearVelocity=new Vector3(0,pilot.Body.linearVelocity.y,0);
            pilot.SlowEnd=Math.Max(pilot.SlowEnd,Time+.04f);pilot.SlowFactor=0;
        }
        private bool StepBackroomsEnemy(NativeMinion m,NativePilot target)
        {
            var a=m.Pve;var z=a.Zone;string kind=a.Stats.id;
            if(a.ZoneStrike>=0&&z.actionTarget>=0)target=Pilots.Find(p=>p.Seat.id==z.actionTarget&&ZonePilot(p));
            if(z.dormant)
            {
                bool wake=Pilots.Any(p=>ZoneVisible(m,p,kind=="tiler"?4:10))||a.LastDamage>=a.SpawnAt&&a.LastDamage>0;
                if(kind=="faceling")
                {
                    wake|=Pilots.Any(p=>ZonePilot(p)&&Time-p.LastShot<.15f&&Vector3.Distance(p.Body.position,m.Body.position)<=14);
                    foreach(var e in Events)if(e.Time>=a.SpawnAt&&Time-e.Time<.15f&&(e.Kind==EventKind.Impact||e.Kind==EventKind.Explosion)&&(e.Position-Vec(m.Body.position)).Length<=14){wake=true;break;}
                }
                if(wake)WakeEntity(m);ZoneStop(m);return true;
            }
            // Every special is cancelled by CC; grabs do not survive disconnected/dead/remote-area targets.
            if(Time<a.StaggerAt)
            {a.ZoneStrike=-100;a.ZoneBusy=Time;z.pullTarget=-1;ZoneRelease(m);z.attack=null;ZoneStop(m);return true;}
            if(z.grabTarget>=0)
            {
                var victim=Pilots.Find(p=>p.Seat.id==z.grabTarget);
                if(victim==null||!ZonePilot(victim)||Time>=z.releaseAt||!ZoneVisible(m,victim,2.4f))ZoneRelease(m);
                else
                {victim.SlowEnd=Math.Max(victim.SlowEnd,Time+.04f);victim.SlowFactor=0;if(a.ZoneTarget<2&&Time>=z.grabAt+.3f*(a.ZoneTarget+1)){a.ZoneTarget++;ZoneHit(m,victim,3,2.4f);}ZoneStop(m);return true;}
            }
            if(kind=="lifeguard"&&z.pullTarget>=0)
            {
                var victim=Pilots.Find(p=>p.Seat.id==z.pullTarget);
                if(victim==null||!ZonePilot(victim)||Time>=z.pullAt+1.1f||!ZoneVisible(m,victim,8))z.pullTarget=-1;
                else {EntityDrag(victim,new Vector3(z.lockX,victim.Body.position.y,z.lockZ),8);ZoneStop(m);return true;}
            }
            if(kind=="drowned")
            {
                bool deep=NativePoolroomsWater.Depth(Poolrooms,Vec(m.Body.position-Vector3.up*m.Collider.height/2))>.8f;
                bool submerged=deep&&Time>=z.aboardUntil&&Time>=a.ZoneBusy;
                if(z.submerged&&!submerged)z.surfaceAt=Time;z.submerged=submerged;
                float height=submerged?.9f:1.7f;if(Math.Abs(m.Collider.height-height)>.01f){m.Body.position+=Vector3.up*((height-m.Collider.height)/2);m.Collider.height=height;}
                // The .9 m capsule remains hittable under the water; surfacing restores full height.
            }
            if(kind=="drowned"&&z.attack=="lunge"&&Time>=m.LastAttack+.25f&&Time<m.LastAttack+.4f)EntityTravel(m,3);
            if(kind=="hound"&&z.attack=="pounce"&&Time>=m.LastAttack+.35f&&Time<m.LastAttack+.85f)EntityTravel(m,10);
            if(a.ZoneStrike>=0&&Time>=a.ZoneStrike)
            {
                a.ZoneStrike=-100;
                switch(z.attack)
                {
                    case "howl":foreach(var n in Minions)if(n.Hp>0&&n.Pve?.Stats.id=="hound"&&Vector3.Distance(n.Body.position,m.Body.position)<=25){n.Pve.Zone.dormant=false;n.Pve.Zone.wakeAt=Time;}break;
                    case "blink":
                        var dest=new Vector3(z.blinkX,z.blinkY,z.blinkZ);
                        if(NativeBackroomsKinds.DarkPath(EntityDarkLayout,Vec(m.Body.position),Vec(dest))&&!EntityObserved(dest)&&EntityClear(m,dest)&&!SceneLinecast(m.Body.position,dest,WorldMask))m.Body.position=dest;
                        break;
                    case "pounce":
                        if(ZoneHit(m,target,3,1.8f,100)>0&&!Minions.Any(n=>n!=m&&n.Pve?.Zone.grabTarget==target.Seat.id))
                        {z.grabTarget=target.Seat.id;z.grabAt=Time;z.releaseAt=Time+.9f;a.HoldDamage=a.ZoneOtherDamage=0;a.ZoneTarget=0;}
                        break;
                    case "whistle":foreach(var n in Minions)if(n.Hp>0&&n.Pve?.Stats.id=="drowned"&&Vector3.Distance(n.Body.position,m.Body.position)<=20){n.Pve.Zone.aboardUntil=Time+6;n.Pve.Zone.surfaceAt=Time;n.Pve.Zone.submerged=false;n.Pve.Target=target;}break;
                    case "hook":
                        if(!Minions.Any(n=>n!=m&&n.Pve!=null&&(n.Pve.Zone.grabTarget==target?.Seat.id||n.Pve.Zone.pullTarget==target?.Seat.id))&&ZoneHit(m,target,3,7,35)>0){z.pullTarget=target.Seat.id;z.pullAt=Time-.6f;
                            var water=Pve.Content.entries.Where(e=>e.allowed.Contains("drowned")).Select(e=>Vec(e.@exit.Position(.85f))).Where(p=>Vector3.Distance(p,m.Body.position)<6&&!SceneLinecast(target.Body.position,new Vector3(p.x,target.Body.position.y,p.z),WorldMask)).OrderBy(p=>Vector3.Distance(p,m.Body.position)).FirstOrDefault();
                            var goal=water==Vector3.zero?m.Body.position:water;z.lockX=goal.x;z.lockZ=goal.z;}
                        break;
                    case "sweep":foreach(var pilot in Pilots)if(ZonePilot(pilot))ZoneHit(m,pilot,15,2.6f,120,3);break;
                    case "lunge":if(ZoneHit(m,target,a.Stats.damage,a.Stats.reach,100)>0&&kind=="smiler"){z.dreadTarget=target.Seat.id;z.dreadUntil=Time+1.5f;}break;
                }
            }
            if(Time<a.ZoneBusy){ZoneStop(m);return true;}
            if(target==null||Time<a.ZoneReady||Time<a.RecoverAt){if(kind=="smiler")ZoneStop(m);return kind=="smiler";}
            float distance=Vector3.Distance(m.Body.position,target.Body.position);
            if(kind=="smiler")
            {
                if(Time>=a.ZoneCallReady)
                {
                    a.ZoneCallReady=Time+4+m.Id%3;
                    var entry=Pve.Content.entries.Where(e=>e.allowed.Contains("smiler")).Select(e=>Vec(e.@exit.Position(m.Collider.height/2)))
                        .Where(p=>Vector3.Distance(p,m.Body.position)<=12&&Vector3.Distance(p,m.Body.position)>1&&NativeBackroomsKinds.DarkPath(EntityDarkLayout,Vec(m.Body.position),Vec(p))&&!EntityObserved(p)&&EntityClear(m,p)&&!SceneLinecast(m.Body.position,p,WorldMask))
                        .OrderBy(p=>Vector3.Distance(p,target.Body.position)).FirstOrDefault();
                    if(entry!=Vector3.zero){ZoneStart(m,"blink",.5f,1);z.blinkAt=Time;z.blinkX=entry.x;z.blinkY=entry.y;z.blinkZ=entry.z;return true;}
                }
                if(ZoneVisible(m,target,2.6f)){ZoneStart(m,"lunge",.6f,2.5f);return true;}
                // A lit displacement cannot be used to chase across a bright corridor. Blink is the only recovery.
                if(!NativeBackroomsKinds.Dark(EntityDarkLayout,Vec(m.Body.position))){ZoneStop(m);return true;}
            }
            if(kind=="hound"&&distance>=3&&ZoneVisible(m,target,6))
            {ZoneStart(m,"pounce",.85f,5);a.ZoneBusy=m.LastAttack+1.65f;a.RecoverAt=a.ZoneBusy;return true;}
            if(kind=="drowned"&&ZoneVisible(m,target,2.2f))
            {ZoneStart(m,"lunge",.4f,1.4f);z.submerged=false;z.surfaceAt=Time;return true;}
            if(kind=="lifeguard")
            {
                if(Time>=a.ZoneCallReady&&Minions.Any(n=>n.Hp>0&&n.Pve?.Stats.id=="drowned"&&Vector3.Distance(n.Body.position,m.Body.position)<=20))
                {a.ZoneCallReady=Time+12;ZoneStart(m,"whistle",1,2);z.lastCallAt=Time;return true;}
                if(ZoneVisible(m,target,2.6f)){ZoneStart(m,"sweep",.5f,2);return true;}
                if(ZoneVisible(m,target,7)){ZoneStart(m,"hook",.6f,7);z.pullAt=Time;return true;}
            }
            return false;
        }
    }
}
