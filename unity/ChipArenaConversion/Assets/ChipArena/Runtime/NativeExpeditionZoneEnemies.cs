using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private readonly List<(Vector3 at,float when)> zonePops=new List<(Vector3,float)>();
        private bool ZoneMovementHeld(NativePilot p)=>Minions.Any(m=>m.Hp>0&&m.Pve!=null&&(m.Pve.Zone.grabTarget==p.Seat.id&&Time<m.Pve.Zone.releaseAt||m.Pve.Zone.pullTarget==p.Seat.id&&Time>=m.Pve.Zone.pullAt+.84f&&Time<m.Pve.Zone.pullAt+1.59f));
        private float ZoneTargetPriority(NativeMinion m,NativePilot p)=>m.Pve.Stats.id=="stoker"?Pilots.Count(q=>q!=p&&ZonePilot(q)&&Vector3.Distance(q.Body.position,p.Body.position)<5)*5:m.Pve.Stats.id=="ladleman"?-Pilots.Count(q=>q!=p&&ZonePilot(q)&&Vector3.Distance(q.Body.position,p.Body.position)<4)*3:0;
        private bool ZoneKindRoom(string kind)=>!NativeZoneCombat.Active(MapId)||Minions.Count(m=>m.Hp>0&&m.Pve?.Stats.id==kind)<NativeZoneCombat.Cap(kind,Pve?.Wave??1,Pve?.Stage=="boss");
        private bool ZoneEntryAllowed(PveEntry e,string kind)
        {
            if(!NativeWardEntries.Allows(e,kind))return false;
            if(kind!="rigger")return true;
            if(e.id.Contains("bridge")&&Pve.Stage!="boss")return false;
            return !Minions.Any(m=>m.Hp>0&&(m.Pve?.Zone.hang==e.e||m.Pve?.Zone.climbTo==e.e));
        }
        private void InitZoneEnemy(NativeMinion m)
        {
            var a=m.Pve;var k=NativeHordeZones.Of(NativeHordeKinds.Resolve(a.Stats.id));
            if(k!=null){m.Collider.height=k.Height;m.Collider.radius=k.Radius;m.Body.mass=40*k.Mass;}
            if(a.Stats.id=="undertow"){var head=new GameObject("Undertow surfaced head");head.layer=11;head.transform.SetParent(m.Body.transform,false);head.transform.localPosition=Vector3.up*.55f;a.ZoneHead=head.AddComponent<CapsuleCollider>();a.ZoneHead.radius=.22f;a.ZoneHead.height=.45f;a.ZoneHead.enabled=false;head.AddComponent<NativeMinionBody>().Minion=m;}
            if(a.Stats.id=="rigger")
            {var chain=new GameObject("Rigger chain weak point");chain.layer=11;chain.transform.SetParent(m.Body.transform,false);chain.transform.localPosition=Vector3.up*1.2f;a.ZoneChain=chain.AddComponent<CapsuleCollider>();a.ZoneChain.radius=.10f;a.ZoneChain.height=.8f;chain.AddComponent<NativeMinionBody>().Minion=m;a.ActiveAt=a.SpawnAt;a.ExposedAt=a.SpawnAt;a.Zone.hang=a.Entry.e;a.ZoneReady=a.SpawnAt+4;m.Body.position=Vec(a.Entry.@exit.Position(m.Collider.height/2+NativeIronworksKinds.HangHeight(a.Entry)));}
        }
        private bool ZoneVisible(NativeMinion m,NativePilot p,float range)
            =>p!=null&&ZonePilot(p)&&Vector3.Distance(m.Body.position,p.Body.position)<=range&&MeleeVisible(m.Body.position,p.Body.position,m.Owner,p.Collider,true);
        private float ZoneHit(NativeMinion m,NativePilot p,float damage,float range,float cone=360,float knock=0,float stun=0)
        {
            if(!ZoneVisible(m,p,range)||Invulnerable(p))return 0;
            var d=p.Body.position-m.Body.position;d.y=0;var dir=d.normalized;
            if(d.sqrMagnitude>.001f&&Vector3.Dot(Vec(V3.Forward(m.Yaw)),dir)<Mathf.Cos(cone*.5f*Mathf.Deg2Rad))return 0;
            if(Time<p.BlockEnd){BlockProc(p,m.Owner,p.Body.position,"clash");return 0;}
            float actual=Damage(p,null,damage*m.Pve.DamageScale,dir,0,false,primary:false,melee:true);
            if(actual>0){p.Knockback+=dir*knock;p.StunEnd=Math.Max(p.StunEnd,Time+stun);}return actual;
        }
        private void ZoneStop(NativeMinion m)
        {if(!m.Body.isKinematic){m.Pve.External*=Mathf.Exp(-6/60f);m.Body.linearVelocity=new Vector3(m.Pve.External.x,m.Body.linearVelocity.y,m.Pve.External.z);}}
        private void ZoneStart(NativeMinion m,string attack,float wind,float cooldown)
        {var a=m.Pve;a.Zone.attack=attack;m.LastAttack=Time;m.StrikeAt=Time+wind;a.ZoneStrike=m.StrikeAt;a.ZoneReady=Time+cooldown;a.ZoneBusy=m.StrikeAt+.6f;a.Struck=true;a.RecoverAt=a.ZoneBusy;if(a.Target!=null){var d=a.Target.Body.position-m.Body.position;m.Yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;}}
        private void ZoneRelease(NativeMinion m)
        {var a=m.Pve;if(a.Zone.grabTarget<0)return;a.Zone.releaseAt=Time;a.Zone.grabTarget=-1;a.ZoneReady=Time+5;a.ZoneBusy=Time+.4f;a.HoldDamage=a.ZoneOtherDamage=0;}
        private void ZoneDrag(NativePilot p,Vector3 direction,float speed)
        {
            // Sweep the owning PhysicsScene before applying movement: hooks cannot cross walls or instance boundaries.
            var delta=direction.normalized*(speed/60);var at=p.Body.position;
            if(!SceneLinecast(at,at+delta*2,WorldMask)){p.Body.position=at+delta;p.Body.transform.position=p.Body.position;}
            p.MoveVelocity=Vector3.zero;p.Body.linearVelocity=new Vector3(0,p.Body.linearVelocity.y,0);
            p.SlowEnd=Math.Max(p.SlowEnd,Time+.04f);p.SlowFactor=0;
        }
        private bool StepZoneEnemy(NativeMinion m,NativePilot target)
        {
            var a=m.Pve;var z=a.Zone;string kind=a.Stats.id;
            if(!NativeZoneCombat.Active(MapId))return false;

            if(kind=="rigger")return StepRigger(m,target);
            if(kind=="linesman")
            {
                bool grounded=StationFlood(m.Body.position)||ZoneHazards!=null&&Time>=ZoneHazards.leverUntil&&StationRail(m.Body.position,m.Collider.height/2);
                if(grounded&&!z.grounded)a.StaggerAt=Math.Max(a.StaggerAt,Time+.8f);z.grounded=grounded;
            }
            if(kind=="undertow")
            {
                bool submerged=StationFlood(m.Body.position)||m.Body.position.y<.5f;if(z.submerged&&!submerged)z.surfaceAt=Time;z.submerged=submerged;if(a.ZoneHead!=null){a.ZoneHead.enabled=submerged;m.Collider.enabled=!submerged;}
                if(submerged)a.ZoneOutsideAt=-100;else if(a.ZoneOutsideAt<0)a.ZoneOutsideAt=Time;
                if(!submerged&&Time-a.ZoneOutsideAt>=3&&z.grabTarget<0)
                {var refuge=Pve.Content.entries.Where(e=>e.allowed!=null&&e.allowed.Contains("undertow")).OrderBy(e=>Vector3.Distance(m.Body.position,Vec(e.@exit.Position(.85f)))).FirstOrDefault();if(refuge!=null){var to=Vec(refuge.@exit.Position(.85f));var move=HubNavMove(m,m.Body.position-Vector3.up*.85f,to,(to-m.Body.position).normalized,null)*a.Stats.run;m.Body.linearVelocity=new Vector3(move.x,m.Body.linearVelocity.y,move.z);return true;}}
                if(z.grabTarget>=0)
                {
                    var p=Pilots.Find(p=>p.Seat.id==z.grabTarget);
                    if(p==null||!ZonePilot(p)||Time>=z.releaseAt||Time<a.StaggerAt||Vector3.Distance(p.Body.position,m.Body.position)>4)ZoneRelease(m);
                    else{Vector3 direction=new Vector3(0,0,-12.5f-p.Body.position.z);ZoneDrag(p,direction,2.7f);if(ZoneTouch(p.Seat.id,1000+m.Id,.25f))Damage(p,null,1.5f*a.DamageScale,Vector3.zero,0,false,primary:false);ZoneStop(m);return true;}
                }
            }
            if(kind=="stoker"&&z.pullAt>=0&&z.pullTarget>=0)
            {
                var phase=NativeIronworksKinds.Pull(z.pullAt,Time,true,out _);var p=Pilots.Find(p=>p.Seat.id==z.pullTarget);
                if(p==null||!ZonePilot(p)||Time<a.StaggerAt)z.pullTarget=-1;
                else if(phase==NativeIronworksKinds.PullPhase.Drag)ZoneDrag(p,m.Body.position-p.Body.position,2.5f/.35f);
                else if(phase==NativeIronworksKinds.PullPhase.Root){p.SlowEnd=Math.Max(p.SlowEnd,Time+.04f);p.SlowFactor=0;}
            }
            if(kind=="ladleman"&&z.pourAt>=0&&Time>=z.pourAt+1&&Time<z.pourAt+5.2f)
            {
                foreach(var p in Pilots)if(ZonePilot(p)&&Math.Abs(p.Body.position.y-m.Body.position.y)<1.8f&&NativeZoneCombat.PoolContains(new System.Numerics.Vector2(p.Body.position.x-m.Body.position.x,p.Body.position.z-m.Body.position.z),z.pourYaw,z.pourAt,Time)&&ZoneTouch(p.Seat.id,m.Id+2000,.25f))Damage(p,null,2,Vector3.zero,0,false,primary:false);
                ZoneStop(m);return true;
            }
            if(Time<a.StaggerAt){if(a.ZoneStrike>=0){a.ZoneStrike=-100;a.ZoneBusy=Time;z.pullTarget=-1;}ZoneStop(m);return true;}
            if(a.ZoneStrike>=0&&Time>=a.ZoneStrike)
            {
                a.ZoneStrike=-100;
                switch(kind)
                {
                    case "linesman":
                        var hits=new List<int>(3);var visited=new HashSet<int>();NativePilot current=target;
                        for(int i=0;i<3&&current!=null;i++)
                        {if(i==0&&!ZoneVisible(m,current,9))break;visited.Add(current.Seat.id);if(!Invulnerable(current)){Damage(current,null,(i==0?18:10)*a.DamageScale*(z.grounded?1.3f:1),Vector3.zero,0,false,primary:false);current.StunEnd=Math.Max(current.StunEnd,Time+.35f);hits.Add(current.Seat.id);}var last=current;current=Pilots.Where(p=>ZonePilot(p)&&!visited.Contains(p.Seat.id)&&p!=last&&Vector3.Distance(p.Body.position,last.Body.position)<=4&&MeleeVisible(last.Body.position,p.Body.position,m.Owner,p.Collider,true)).OrderBy(p=>Vector3.Distance(p.Body.position,last.Body.position)).FirstOrDefault();}
                        z.arcAt=Time;z.arcTargets=hits.ToArray();break;
                    case "conductor":
                        if(!z.lanternBroken)
                        {foreach(var n in Minions)if(n.Pve!=null&&n.Hp>0&&Vector3.Distance(n.Body.position,m.Body.position)<=14)n.Pve.Zone.aboardUntil=Time+6;}
                        break;
                    case "undertow":
                        if(ZoneVisible(m,target,2.6f)&&!Invulnerable(target)&&Time>=target.BlockEnd&&!Minions.Any(n=>n!=m&&n.Pve?.Zone.grabTarget==target.Seat.id))
                        {z.grabTarget=target.Seat.id;z.grabAt=Time;z.releaseAt=Time+(Pilots.Count(p=>ZonePilot(p)&&!p.Seat.bot)<=1?1:1.5f);a.HoldDamage=a.ZoneOtherDamage=0;}
                        break;
                    case "pileup":foreach(var p in Pilots)if(ZonePilot(p))ZoneHit(m,p,20,2.4f,70,6);break;
                    case "stoker":
                        if(ZoneVisible(m,target,4.5f))
                        {var rel=target.Body.position-m.Body.position;var fwd=Vec(V3.Forward(m.Yaw));float along=Vector3.Dot(rel,fwd);if(along>0&&(rel-fwd*along).magnitude<.6f&&Time>=target.BlockEnd&&!Invulnerable(target))z.pullTarget=target.Seat.id;}break;
                }
            }
            if(Time<a.ZoneBusy){ZoneStop(m);return true;}
            if(target==null||Time<a.RecoverAt||Time<a.ZoneReady||!a.Struck)return false;
            if(kind=="linesman"){var nearest=Pilots.Where(p=>ZoneVisible(m,p,9)).OrderBy(p=>Vector3.Distance(m.Body.position,p.Body.position)).FirstOrDefault();if(nearest!=null){target=nearest;a.Target=target;}}
            float distance=Vector3.Distance(m.Body.position,target.Body.position);
            switch(kind)
            {
                case "linesman":if(ZoneVisible(m,target,9)){ZoneStart(m,"arc",1.1f,z.grounded?3:6);return true;}break;
                case "conductor":
                    if(z.lanternBroken){var dir=m.Body.position-target.Body.position;dir.y=0;if(SupportedStageMove(m.Body.position-Vector3.up*m.Collider.height/2,dir.normalized*2.6f))m.Body.linearVelocity=dir.normalized*2.6f+Vector3.up*m.Body.linearVelocity.y;return true;}
                    if(Time>=a.ZoneCallReady&&Pve!=null)
                    {z.lastCallAt=Time;a.ZoneCallReady=Time+20;a.ZoneCallStrike=Time+1.2f;if(ZoneHazards!=null)ZoneHazards.lastCallAt=Time;}
                    if(Minions.Any(n=>n!=m&&n.Hp>0&&Vector3.Distance(n.Body.position,m.Body.position)<14)){ZoneStart(m,"whistle",.8f,12);return true;}break;
                case "undertow":if(distance<=2.6f){ZoneStart(m,"grab",.6f,5);return true;}break;
                case "pileup":if(distance<=2.4f){ZoneStart(m,"shove",.7f,4);return true;}break;
                case "stoker":if(distance>=2.5f&&ZoneVisible(m,target,4.5f)){ZoneStart(m,null,.84f,6);z.pullAt=Time;z.pullTarget=-1;a.ZoneBusy=Time+2.04f;return true;}break;
                case "ladleman":if(z.spilledAt<0&&ZoneVisible(m,target,4.6f)){ZoneStart(m,null,1,7);z.pourAt=Time;z.pourYaw=m.Yaw;a.ZoneBusy=Time+5.2f;return true;}break;
            }
            return false;
        }
        private void StepZoneEvents()
        {
            foreach(var d in MinionDeaths)if(d.zone!=null&&d.zone.pourAt>=0&&d.at>=d.zone.pourAt+1&&Time<d.zone.pourAt+5.2f)
            {foreach(var p in Pilots)if(ZonePilot(p)&&Math.Abs(p.Body.position.y-d.position.y)<1.8f&&NativeZoneCombat.PoolContains(new System.Numerics.Vector2(p.Body.position.x-d.position.x,p.Body.position.z-d.position.z),d.zone.pourYaw,d.zone.pourAt,Time)&&ZoneTouch(p.Seat.id,d.id+2000,.25f))Damage(p,null,2,Vector3.zero,0,false,primary:false);}
            for(int i=zonePops.Count-1;i>=0;i--)if(Time>=zonePops[i].when)
            {var pop=zonePops[i];foreach(var p in Pilots)if(ZonePilot(p)&&Vector3.Distance(p.Body.position,pop.at)<1.4f)Damage(p,null,6,(p.Body.position-pop.at).normalized,0,false,primary:false);zonePops.RemoveAt(i);}
            foreach(var m in Minions.ToArray())if(m.Pve!=null&&m.Hp>0&&m.Pve.ZoneCallStrike>=0&&Time>=m.Pve.ZoneCallStrike)
            {var a=m.Pve;a.ZoneCallStrike=-100;if(a.Zone.lanternBroken||Time<a.StaggerAt)continue;var entries=Pve.Content.entries.Where(e=>e.id.Contains("train-car")&&NativeWardEntries.Allows(e,"runner")).ToArray();for(int i=0;i<2&&entries.Length>0;i++)SpawnPveAdd("runner",true,entries[(m.Id+i)%entries.Length].e);}
        }
        private bool StepRigger(NativeMinion m,NativePilot target)
        {
            var a=m.Pve;var z=a.Zone;if(a.ZoneChain!=null)a.ZoneChain.enabled=z.hang>=0||z.climbTo>=0;
            if(z.hang>=0||z.climbTo>=0)
            {
                m.Body.isKinematic=true;var e=Pve.Content.entries.FirstOrDefault(e=>e.e==(z.climbTo>=0?z.climbTo:z.hang))??a.Entry;
                var floor=Vec(e.@exit.Position(m.Collider.height/2+.025f));float height=NativeIronworksKinds.HangHeight(e);
                if(z.chainCutAt>=0)
                {float y=NativeIronworksKinds.Fallen(z.chainCutAt,Time,a.ZoneFallHeight,out bool landed,out _);m.Body.position=new Vector3(m.Body.position.x,floor.y+y,m.Body.position.z);if(landed){z.hang=z.climbTo=-1;m.Body.isKinematic=false;a.StaggerAt=Time+1.5f;a.ZoneReady=Time+5;}return true;}
                if(z.climbTo>=0)
                {float u=NativeIronworksKinds.Climbed(z.climbAt,Time,height,out _);m.Body.position=Vector3.Lerp(a.ZoneClimbFrom,floor+Vector3.up*height,u);if(u>=1){z.hang=z.climbTo;z.climbTo=-1;z.climbAt=-100;z.lockAt=-100;a.ZoneReady=Time+4;}return true;}
                if(z.lockAt<0)
                {m.Body.position=floor+Vector3.up*height;if(Time<a.ZoneReady||target==null)return true;var choices=Pilots.Where(p=>ZonePilot(p)&&Math.Abs(p.Body.position.y-floor.y)<1.8f&&new Vector2(p.Body.position.x-floor.x,p.Body.position.z-floor.z).magnitude<=6&&!Minions.Any(n=>n!=m&&n.Pve?.Stats.id=="rigger"&&n.Pve.ZoneTarget==p.Seat.id&&Time<n.Pve.Zone.lockAt+1.6f)).OrderBy(p=>p.MoveVelocity.sqrMagnitude).ToArray();if(choices.Length==0)return true;target=choices[0];a.ZoneLanded=false;a.ZoneTarget=target.Seat.id;z.lockAt=Time+.4f;z.lockX=target.Body.position.x;z.lockZ=target.Body.position.z;a.ZoneLandingY=floor.y;}
                // Lock exactly after the audible rattle; the final 1.6 s is immovable and dodgeable.
                if(Time<z.lockAt){var p=Pilots.Find(p=>p.Seat.id==a.ZoneTarget);if(p!=null){z.lockX=p.Body.position.x;z.lockZ=p.Body.position.z;}}
                var landing=new Vector3(z.lockX,a.ZoneLandingY,z.lockZ);float slide=NativeIronworksKinds.Slid(z.lockAt,Time);m.Body.position=Vector3.Lerp(floor+Vector3.up*height,landing,slide);
                if(Time>=z.lockAt+1.6f){if(!a.ZoneLanded){a.ZoneLanded=true;a.ZoneTarget=-1;foreach(var p in Pilots)if(ZonePilot(p))ZoneHit(m,p,14,1.5f,360,0,.5f);}if(Time>=z.lockAt+2.1f){z.hang=-1;m.Body.isKinematic=false;a.ZoneReady=Time+1;}}return true;
            }
            if(Time<a.StaggerAt){ZoneStop(m);return true;}
            if(Time>=a.ZoneReady&&m.LastAttack>=a.SpawnAt&&a.ZoneClimbs<2&&m.Hp>=m.MaxHp*.4f&&!Pilots.Any(p=>ZonePilot(p)&&Vector3.Distance(p.Body.position,m.Body.position)<2))
            {var e=Pve.Content.entries.Where(e=>e.allowed!=null&&e.allowed.Contains("rigger")&&ZoneEntryAllowed(e,"rigger")&&Vector3.Distance(Vec(e.@exit.Position(.9f)),m.Body.position)<=8).OrderBy(e=>Vector3.Distance(Vec(e.@exit.Position(.9f)),m.Body.position)).FirstOrDefault();if(e!=null){z.climbTo=e.e;z.climbAt=Time;z.chainCutAt=-100;a.ZoneClimbFrom=m.Body.position;a.ZoneClimbs++;a.ZoneArmor=a.ZoneExposureDamage=0;return true;}}
            if(a.ZoneStrike>=0&&Time>=a.ZoneStrike)
            {a.ZoneStrike=-100;a.ZoneSwingHits.Clear();}
            NativeIronworksKinds.Swing(Time-m.LastAttack,out float angle,out _,out bool sweeping);
            if(sweeping){var forward=Quaternion.Euler(0,m.Yaw+angle*Mathf.Rad2Deg,0)*Vector3.forward;foreach(var p in Pilots){var d=p.Body.position-m.Body.position;d.y=0;if(ZonePilot(p)&&d.magnitude>=1&&Vector3.Dot(forward,d.normalized)>=Mathf.Cos(12*Mathf.Deg2Rad)&&!a.ZoneSwingHits.Contains(p.Seat.id)){if(ZoneHit(m,p,9,3.2f,360,3)>0)a.ZoneSwingHits.Add(p.Seat.id);}}}
            if(Time<a.ZoneBusy){ZoneStop(m);return true;}
            if(target!=null&&Time>=a.ZoneReady&&ZoneVisible(m,target,3.2f)){ZoneStart(m,null,.6f,5);return true;}
            return false;
        }
        private float ZoneIncoming(NativeMinion m,NativePilot source,float amount,Vector3 dir,Vector3? point,bool head,float impulse,string kind)
        {
            var a=m.Pve;var z=a.Zone;if(!NativeZoneCombat.Active(MapId))return amount;
            var forward=Vec(V3.Forward(m.Yaw));float facing=dir.sqrMagnitude>0?Vector3.Dot(-dir.normalized,forward):0;
            bool upper=point.HasValue&&point.Value.y>m.Body.position.y+.35f;
            switch(a.Stats.id)
            {
                case "cinderling":if(kind=="fire"||source?.Seat.spec=="fire"&&(kind=="energy"||kind=="ability"))return 0;if(!head&&point.HasValue&&(point.Value-m.Body.position).magnitude<.35f)amount*=2;break;
                case "linesman":if(facing<=-.5f)amount*=1.75f;break;
                case "pileup":if(!head&&facing>=Mathf.Cos(35*Mathf.Deg2Rad))amount*=.25f;break;
                case "conductor":if(!z.lanternBroken&&point.HasValue&&Math.Abs(point.Value.y-m.Body.position.y)<.5f){a.ZoneArmor+=amount;amount*=1.5f;if(a.ZoneArmor>=40){z.lanternBroken=true;a.ZoneStrike=-100;a.ZoneCallStrike=-100;}}break;
                case "slagback":if(z.shedAt<0&&facing>=.5f){a.ZoneArmor+=amount;amount*=.4f;if(a.ZoneArmor>=120){z.shedAt=Time;a.StaggerAt=Time+.8f;}}else if(facing<=-.5f)amount*=2;break;
                case "ladleman":if(z.spilledAt<0&&upper&&!head){a.ZoneArmor+=amount;if(a.ZoneArmor>=200){z.spilledAt=Time;a.StaggerAt=Time+2;a.ZoneStrike=-100;a.ZoneSelfDamage+=m.MaxHp*.35f;}}break;
                case "stoker":if(z.pullAt>=0&&Time<z.pullAt+.7f&&NativeIronworksKinds.PullInterrupted(amount,m.MaxHp,impulse>=400)){a.ZoneStrike=-100;z.pullTarget=-1;z.pullAt=-100;a.ZoneBusy=Time;a.StaggerAt=Time+.4f;}break;
                case "rigger":if(z.hang>=0||z.climbTo>=0){bool chain=point.HasValue&&point.Value.y>m.Body.position.y+.85f;if(chain){a.ZoneArmor+=amount;amount=0;}else{amount*=1.5f;a.ZoneExposureDamage+=amount;}if(z.chainCutAt<0&&(a.ZoneExposureDamage>=m.MaxHp*.3f||a.ZoneArmor>=40)){z.chainCutAt=Time;a.ZoneFallHeight=Math.Max(0,m.Body.position.y-a.Entry.@exit.y-m.Collider.height/2);a.ZoneSelfDamage+=m.MaxHp*.2f;}}break;
            }
            if(z.grabTarget>=0){if(source?.Seat.id==z.grabTarget)a.HoldDamage+=amount;else a.ZoneOtherDamage+=amount;if(a.HoldDamage>=40||a.ZoneOtherDamage>=25||impulse>=400)ZoneRelease(m);}
            return amount;
        }
        private void ZoneEnemyDeath(NativeMinion m,NativePilot source,bool melee)
        {
            var a=m.Pve;if(a==null||!NativeZoneCombat.Active(MapId))return;ZoneRelease(m);
            if(a.Stats.id=="cinderling"&&melee&&zonePops.Count<64)zonePops.Add((m.Body.position,Time+.45f));
            if(a.Stats.id=="pileup")
            {
                var children=new List<int>();a.Zone.splitAt=Time;
                for(int i=0;i<2;i++){var child=SpawnPveAdd("shambler",true);if(child==null)break;var floor=m.Body.position-Vector3.up*m.Collider.height/2+new Vector3((i-1)*.7f,0,.4f);if(!ExpeditionSupported(Vec(floor),.32f,.88f)){RemoveMinion(child,false);continue;}child.Body.position=floor+Vector3.up*.46f;child.Pve.Crawl=true;child.Pve.Lost=32|64;child.Collider.height=.88f;child.Collider.radius=.32f;child.Pve.SpawnAt=child.Pve.ActiveAt=child.Pve.ExposedAt=Time;children.Add(child.Id);}
                a.Zone.childIds=children.ToArray();
            }
        }
    }
}
