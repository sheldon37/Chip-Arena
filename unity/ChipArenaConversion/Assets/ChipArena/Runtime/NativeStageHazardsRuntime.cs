using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private readonly Dictionary<string,BoxCollider> stageLoads=new Dictionary<string,BoxCollider>();
        private int StageRound=>NativeStructureFx.RoundIndex(Match.Set,Match.Round);
        private NativePilot StageCredit(NativePilot victim,float seconds)=>victim.LastAttacker!=null&&victim.LastAttacker!=victim&&Time-victim.LastAttackerAt>=0&&Time-victim.LastAttackerAt<=seconds?victim.LastAttacker:null;
        private void PoseStageLoads(double now,bool advance=false)
        {
            foreach(var h in Map.hazards??Array.Empty<MapHazard>())
            {
                if(h.kind=="shutter"){PoseShutter(h,now,advance);continue;}if(h.kind!="crane")continue;var s=NativeFactoryHazards.State(h,StageRound,NativeFactoryHazards.Clock(Match.Phase,Match.FightAt,now));
                if(!stageLoads.TryGetValue(h.id,out var box))
                {var go=new GameObject(h.id+"-landed-collision");go.transform.SetParent(root.transform);go.layer=PropLayer;box=go.AddComponent<BoxCollider>();box.size=Vec(h.size);stageLoads.Add(h.id,box);}
                box.enabled=NativeStageHazards.Landed(s);if(box.enabled)box.transform.position=new Vector3((float)s.X,h.size.y/2,(float)s.Z);
            }
        }
        private void PoseShutter(MapHazard h,double now,bool advance)
        {
            if(!stageLoads.TryGetValue(h.id,out var box))
            {var go=new GameObject(h.id+"-collision");go.layer=PropLayer;go.transform.SetParent(root.transform);box=go.AddComponent<BoxCollider>();box.size=Vec(h.size);box.transform.position=Vec(h.center);stageLoads.Add(h.id,box);}
            var s=NativeWardZeroHazards.At(h,Match.Phase,StageRound,Match.FightAt,now);
            bool shut=s.Phase==NativeHazardPhase.Danger||s.Phase==NativeHazardPhase.Active||s.Phase==NativeHazardPhase.Clear;
            box.enabled=false;if(!shut)return;
            bool occupied=false;var normal=h.size.x<h.size.z?Vector3.right:Vector3.forward;
            foreach(var p in Pilots.Where(p=>p.Hp>0&&p.Seat.connected))
            {
                if(!NativeStageHazards.Touch(Vec(p.Body.position),h.center,h.size))continue;
                if(advance&&!Replica&&!KernelLocked(p))
                {
                    float side=Vector3.Dot(p.Body.position-Vec(h.center),normal)>=0?1:-1;
                    p.Body.position=NativeGroundMotor.Constrain(root.scene.GetPhysicsScene(),p.Body.position,p.Body.position+normal*(side*h.push/60));
                }
                if(NativeStageHazards.Touch(Vec(p.Body.position),h.center,h.size))occupied=true;
            }
            foreach(var m in Minions.Where(m=>m.Hp>0))
            {
                if(!NativeStageHazards.Touch(Vec(m.Body.position),h.center,h.size,m.Collider.radius,m.Collider.height/2))continue;
                if(advance&&!Replica)
                {
                    float side=Vector3.Dot(m.Body.position-Vec(h.center),normal)>=0?1:-1;
                    var delta=normal*(side*h.push/60);
                    if(!m.Body.SweepTest(delta.normalized,out _,delta.magnitude,QueryTriggerInteraction.Ignore))m.Body.position+=delta;
                }
                if(NativeStageHazards.Touch(Vec(m.Body.position),h.center,h.size,m.Collider.radius,m.Collider.height/2))occupied=true;
            }
            // Obstruction interlock: never enable a solid box through an occupied capsule.
            // Re-evaluated on both authority and replicas; the authored visual clock is unchanged.
            box.enabled=!occupied;
        }
        private void StepAuthoredHazards()
        {
            PoseWardBreaches();PoseStageLoads(Match.Time,true);if(Replica||Match.Phase!=NativePhase.Fight)return;
            foreach(var h in Map.hazards??Array.Empty<MapHazard>())
            {
                V3 center=default,size=default;bool lethal=h.lethal,active=false;int ticks=0;string kind=NativeStageHazards.City(h)||NativeStageHazards.Ward(h)?h.kind:"hazard";Vector3 velocity=Vector3.zero;
                if(h.kind=="maglev")
                {active=NativeStageHazards.TrainSweep(h,StageRound,Match.FightAt,Match.Time-1d/60,Match.Time,out center,out size);velocity=new Vector3(NativeCyberCityHazards.Evaluate(h,StageRound,Match.FightAt,Match.Time).Direction*30,6,0);}
                else if(h.kind=="shutter")continue;
                else if(h.kind=="grid"||NativeWardZeroHazards.IsBurst(h))
                {ticks=NativeStageHazards.DamageTicks(h,StageRound,Match.FightAt,Match.Time-1d/60,Match.Time);active=ticks>0;center=h.center;size=h.size;}
                else
                {var s=NativeFactoryHazards.State(h,StageRound,NativeFactoryHazards.Clock(Match.Phase,Match.FightAt,Match.Time));active=NativeStageHazards.FactoryBox(h,s,out center,out size);if(h.kind=="vent"){ticks=NativeStageHazards.DamageTicks(h,StageRound,Match.FightAt,Match.Time-1d/60,Match.Time);active=h.damage>0&&ticks>0;}}
                if(!active)continue;
                foreach(var p in Pilots)
                {
                    if(p.Hp<=0||!p.Seat.connected||KernelLocked(p))continue;bool touch=h.kind=="grid"?NativeStageHazards.GridTouch(h,Vec(p.Body.position)):NativeWardZeroHazards.IsBurst(h)?NativeStageHazards.BurstTouch(h,Vec(p.Body.position)):NativeStageHazards.Touch(Vec(p.Body.position),center,size);
                    if(!touch)continue;
                    if(lethal)p.Knockback+=velocity;
                    Damage(p,null,lethal?p.Hp:h.damage*ticks,velocity.normalized,0,false,forced:lethal,direct:false,primary:false,stage:true,rollAvoidable:false,damageKind:kind,stageHazard:h.id,stageLethal:lethal);
                }
                foreach(var m in Minions.ToArray())
                {
                    if(m.Hp<=0)continue;float half=m.Collider.height/2;bool touch=h.kind=="grid"?NativeStageHazards.GridTouch(h,Vec(m.Body.position),half):NativeWardZeroHazards.IsBurst(h)?NativeStageHazards.BurstTouch(h,Vec(m.Body.position),m.Collider.radius,half):NativeStageHazards.Touch(Vec(m.Body.position),center,size,m.Collider.radius,half);
                    if(!touch)continue;float actual=Math.Min(m.Hp,lethal?m.Hp:h.damage*ticks);m.Hp-=actual;m.LastHitAt=Time;Emit(EventKind.HazardHit,null,m.Id,m.Body.position,actual,h.id,damageKind:kind);
                    if(m.Hp<=0){if(!m.Body.isKinematic)m.Body.linearVelocity+=velocity;var at=m.Body.position;bool possessed=m.Pve?.Possessed==true;var credit=Pve!=null&&m.Pve?.LastHit!=null&&Time-m.Pve.LastDamage<=7?m.Pve.LastHit:null;if(credit!=null)PveAddKilled(m,credit);else Emit(EventKind.Death,null,m.Id,at,0,h.id,damageKind:kind);RemoveMinion(m,false);var death=MinionDeaths.LastOrDefault(d=>d.id==m.Id);if(death!=null)death.damageKind=kind;if(possessed)FoundPveBoss(at);}
                }
            }
        }
        private bool StageLinkOpen(MapNavLink link)
        {
            foreach(var id in link.hazards??Array.Empty<string>())
            {MapHazard h=null;foreach(var candidate in Map.hazards??Array.Empty<MapHazard>())if(candidate.id==id){h=candidate;break;}if(h==null||!NativeStageHazards.LinkOpen(h,Match.Phase,StageRound,Match.FightAt,Match.Time,navNodes[link.a].pos,navNodes[link.b].pos))return false;}return true;
        }
        private bool UnsafeStagePath(Vector3 from,Vector3 to,float margin=.45f)
        {
            var a=Vec(from);var b=Vec(to);
            foreach(var h in Map.hazards??Array.Empty<MapHazard>())
            {
                bool cross=NativeStageHazards.CrossesEnvelope(h,a,b,margin);
                if(cross&&!NativeStageHazards.LinkOpen(h,Match.Phase,StageRound,Match.FightAt,Match.Time,a,b))return true;
            }return false;
        }
        private bool SupportedStageMove(Vector3 pos,Vector3 move)
        {
            if(Map.hazards==null||move.sqrMagnitude<.001f)return true;
            var ahead=pos+move.normalized*.7f;
            bool drop=(Map.navHints?.drops??Array.Empty<MapDrop>()).Any(d=>
            {
                if(pos.y<d.b.y+.5f||pos.y>d.a.y+2)return false;
                var a=new Vector3(d.a.x,pos.y,d.a.z);var b=new Vector3(d.b.x,pos.y,d.b.z);
                return Vector3.Dot(move,b-a)>0&&NativeCyberRules.SegmentDistance(Vec(pos),Vec(a),Vec(b))<1.5f;
            });
            return (UnsafeStagePath(pos,pos)||!UnsafeStagePath(pos,ahead))&&(drop||root.scene.GetPhysicsScene().Raycast(ahead+Vector3.up*.3f,Vector3.down,out _,1.65f,WorldMask,QueryTriggerInteraction.Ignore));
        }
    }
}
