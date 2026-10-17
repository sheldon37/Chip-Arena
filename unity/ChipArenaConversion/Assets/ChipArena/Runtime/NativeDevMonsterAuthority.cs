using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private readonly Collider[] devMonsterOverlap=new Collider[64];
        private bool DevMonsterGround(Vector3 desired,float height,float radius,out Vector3 point,bool air=false)
        {
            point=default;var physics=root.scene.GetPhysicsScene();
            if(!physics.Raycast(desired+Vector3.up*2,Vector3.down,out var hit,12,WorldMask,QueryTriggerInteraction.Ignore)||hit.normal.y<.65f)return false;
            point=hit.point+Vector3.up*(height/2+.04f+(air?6:0));
            if((ClampMap(point)-point).sqrMagnitude>.01f||UnsafeStagePath(hit.point,hit.point))return false;
            if(air&&SceneLinecast(hit.point+Vector3.up*.1f,point+Vector3.up*height/2,WorldMask))return false;
            float half=Math.Max(0,height/2-radius);
            return physics.OverlapCapsule(point+Vector3.up*half,point-Vector3.up*half,radius,devMonsterOverlap,ShotMask,QueryTriggerInteraction.Ignore)==0;
        }
        private string DevSpawnMonsters(NativePilot actor,NativePilot target,string kind,int wanted,string place,string behaviour)
        {
            var data=StageContent;var stats=data?.adds?.FirstOrDefault(s=>s.id==kind);
            if(stats==null||!NativeHordeKinds.Known(kind))return "Monster kind is absent from the content catalog.";
            if(target.Hp<=0)return "Monster target must be alive.";
            wanted=NativeDevRules.MonsterAllowance(kind,wanted,Minions.Count(m=>m.DevMonster&&m.Hp>0),
                Minions.Count(m=>m.Hp>0&&m.Pve?.Stats.id=="hulk"),Minions.Count(m=>m.Hp>0&&m.Pve?.Stats.id=="spitter"));
            if(wanted<=0)return "Monster cap reached.";
            var entries=Map.id=="siren"?(Pve?.Content??data).entries.Where(e=>NativeWardEntries.Allows(e,kind)&&new[]{"vent","door","drop","breach"}.Contains(e.kind)).OrderBy(e=>(Vec(e.@exit.Position())-target.Body.position).sqrMagnitude).ToArray():Array.Empty<PveEntry>();
            if(place=="entries"&&entries.Length==0)return "This map has no compatible authored monster entrances.";
            Vector3 aim=target.Body.position;
            if(place=="aim")
            {
                var ray=Quaternion.Euler(-actor.Input.Pitch,actor.Input.Yaw,0)*Vector3.forward;
                if(!root.scene.GetPhysicsScene().Raycast(actor.Body.position+Vector3.up*.55f,ray,out var hit,40,WorldMask,QueryTriggerInteraction.Ignore))return "Aim ray did not hit world geometry within 40 metres.";
                aim=hit.point;
            }
            bool heavy=kind=="hulk";float height=heavy?NativeHordeKinds.HulkHeight:1.7f,radius=heavy?NativeHordeKinds.HulkRadius:.42f;int spawned=0;
            for(int attempt=0;attempt<64&&spawned<wanted;attempt++)
            {
                Vector3 desired=aim;PveEntry entry=null;
                if(place=="entries"){entry=entries[attempt%entries.Length];desired=Vec(entry.@exit.Position());}
                else if(place=="near"||place=="drop"){float angle=attempt*2.399963f,distance=place=="near"?4+attempt%4:attempt==0?0:1.5f+attempt%3;desired=target.Body.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*distance;}
                else if(attempt>0){float angle=attempt*2.399963f;desired+=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(1+attempt%3);}
                if(!DevMonsterGround(desired,height,radius,out var point,place=="drop"))continue;
                var obj=new GameObject("DEV monster "+kind);obj.layer=11;obj.transform.SetParent(root.transform);obj.transform.position=point;
                var col=obj.AddComponent<CapsuleCollider>();col.radius=radius;col.height=height;col.sharedMaterial=surfaces["pilot"];
                var body=obj.AddComponent<Rigidbody>();body.mass=heavy?160:40;body.constraints=RigidbodyConstraints.FreezeRotation;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.interpolation=RigidbodyInterpolation.Interpolate;
                var floor=point-Vector3.up*(height/2);entry=entry??new PveEntry{e=0,kind="door",from=new PvePoint{x=floor.x,y=floor.y,z=floor.z},@exit=new PvePoint{x=floor.x,y=floor.y,z=floor.z}};
                var m=new NativeMinion{Id=nextMinion++,Owner=actor,Kind="siren-add",DevMonster=true,DevBehaviour=behaviour,DevHunt=behaviour=="idle"?0:target.Seat.id,Body=body,Collider=col,Hp=stats.hp,MaxHp=stats.hp,Speed=stats.run,Until=float.PositiveInfinity,NextAttack=Time+1,
                    Pve=new NativePveAdd{Stats=stats,Entry=entry,HpScale=1,DamageScale=1,SpawnAt=Time,WalkAt=Time,ExposedAt=Time,ActiveAt=Time,Struck=true}};
                obj.AddComponent<NativeMinionBody>().Minion=m;Minions.Add(m);spawned++;Physics.SyncTransforms();
            }
            if(spawned==0)return "No clear, supported monster spawn position.";
            devActionMessage="Spawned "+spawned+" DEV monster"+(spawned==1?".":"s.");return null;
        }
        private void DevClearMonsters()
        {
            foreach(var m in Minions.Where(m=>m.DevMonster).ToArray())RemoveMinion(m,false);
            pveAcid.RemoveAll(a=>a.Record.devMonster);foreach(var p in Pilots)p.Dots.Remove("dev-monster:poison");
        }
        private float HitDevMonster(NativeMinion m,NativePilot source,float amount,Vector3 direction,float impulse,Vector3? point,string kind)
        {
            if((float.IsNaN(amount)||float.IsInfinity(amount))||amount<=0)return 0;float actual=Math.Min(amount,m.Hp);m.Hp-=actual;m.LastHitAt=Time;
            if(m.DevBehaviour=="idle"&&m.DevHunt==0&&source!=null)m.DevHunt=source.Seat.id;
            CancelHulkWind(m,actual);
            if(m.Pve.Stats.id!="hulk"||!m.Pve.Charging&&NativeHordeKinds.Staggers(NativeHordeKinds.Resolve(m.Pve.Stats.id),actual,m.MaxHp,impulse>=400)){m.Pve.StaggerAt=Time+m.Pve.Stats.stagger;m.StrikeAt=-1;m.Pve.Struck=true;}
            if(!m.Body.isKinematic&&!m.Pve.Charging)m.Body.AddForce(direction*impulse,ForceMode.Impulse);
            Emit(EventKind.Hit,source,m.Id,point??m.Body.position,actual,kind??"direct");
            if(m.Hp<=0)RemoveMinion(m,false);return actual; // no PvE economy, quota, XP, drops or kill receipt
        }
        private void StepDevMonsters()
        {
            if(Replica)return;if(Match.Phase!=NativePhase.Fight){DevClearMonsters();return;}
            if(Pve==null)StepPveAcid();
            foreach(var m in Minions.Where(m=>m.DevMonster).ToArray())
            {
                if(m.Hp<=0||m.Body.position.y<(Map.bounds?.killY??-6)){RemoveMinion(m,false);continue;}
                foreach(var pair in m.Dots.ToArray()){var dot=pair.Value;while(m.Hp>0&&dot.Next<=Time&&dot.Next<=dot.Until){HitDevMonster(m,Pilots.Find(p=>p.Seat.id==dot.Source),(float)(dot.Dps*.25),Vector3.zero,0,null,"field");dot.Next+=.25;}if(Time>dot.Until)m.Dots.Remove(pair.Key);}if(m.Hp<=0)continue;
                var target=m.DevBehaviour=="frenzy"?Pilots.Where(p=>p.Hp>0&&p.Seat.connected&&!p.Seat.AuthorityBoss).OrderBy(p=>(p.Body.position-m.Body.position).sqrMagnitude).FirstOrDefault():Pilots.Find(p=>p.Seat.id==m.DevHunt&&p.Hp>0&&p.Seat.connected);
                var at=m.Body.position;var velocity=m.Body.linearVelocity;var stats=m.Pve.Stats;m.Pve.Target=target;
                if(StepHulkHunt(m,target))continue;
                if(target==null||Time<m.Pve.StaggerAt||Time<m.Pve.RecoverAt){m.Body.linearVelocity=Vector3.up*velocity.y;m.StrikeAt=-1;continue;}
                var delta=target.Body.position-at;delta.y=0;float distance=delta.magnitude;var direction=delta.normalized;m.Yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
                if(m.StrikeAt>=0)
                {
                    m.Body.linearVelocity=Vector3.up*velocity.y;if(Time<m.StrikeAt)continue;m.StrikeAt=-1;m.Pve.Struck=true;m.NextAttack=stats.id=="hulk"?m.LastAttack+NativeHulkMoves.Recover(NativeHulkMoves.Of(m.Pve.Move),stats.cycle,m.Pve.Crawl,m.Pve.Lost):Time+Math.Max(.2f,stats.cycle-stats.hit);if(stats.id=="hulk")m.Pve.RecoverAt=m.NextAttack;
                    if(stats.id=="hulk"){PveAddStrike(m);continue;}
                    if(stats.id=="spitter"){LaunchPveSpit(m);continue;}
                    if(distance<=stats.reach&&Math.Abs(target.Body.position.y-at.y)<2.5f&&SummonVisible(m,at,target.Body.position,target.Collider))
                    {if(Time<target.BlockEnd)BlockProc(target,null,target.Body.position,"clash");else Damage(target,null,stats.damage,direction,stats.impulse,false,direct:false,primary:false,melee:true,damageKind:"dev-monster");}
                    continue;
                }
                if(distance<=(stats.id=="hulk"?NativeHulkMoves.PunchReach:stats.startRange)&&Time>=m.NextAttack&&SummonVisible(m,at,target.Body.position,target.Collider)){float hit=stats.id=="hulk"?ChooseHulkMove(m,target,stats.hit):stats.hit;m.LastAttack=Time;m.StrikeAt=Time+Math.Max(.1f,hit);m.Pve.Struck=false;m.Body.linearVelocity=Vector3.up*velocity.y;continue;}
                // Bounded direct chase on every map. Never request Ward graph state or walk blindly over a ledge/hazard.
                var next=at+direction*Math.Max(.8f,m.Collider.radius*2);bool clear=SummonVisible(m,at,next,null)&&!UnsafeStagePath(at,next)&&root.scene.GetPhysicsScene().Raycast(next,Vector3.down,out var floor,m.Collider.height/2+1,WorldMask,QueryTriggerInteraction.Ignore)&&floor.normal.y>.65f;
                float speed=clear?Math.Max(0,stats.run)*(Time<m.SlowUntil?m.SlowFactor:1):0;m.Body.linearVelocity=direction*speed+Vector3.up*velocity.y;
            }
        }
    }
}
