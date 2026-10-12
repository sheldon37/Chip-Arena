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
        private void PoseStageLoads(double now)
        {
            foreach(var h in Map.hazards??Array.Empty<MapHazard>())
            {
                if(h.kind!="crane")continue;var s=NativeFactoryHazards.State(h,StageRound,NativeFactoryHazards.Clock(Match.Phase,Match.FightAt,now));
                if(!stageLoads.TryGetValue(h.id,out var box))
                {var go=new GameObject(h.id+"-landed-collision");go.transform.SetParent(root.transform);go.layer=PropLayer;box=go.AddComponent<BoxCollider>();box.size=Vec(h.size);stageLoads.Add(h.id,box);}
                box.enabled=NativeStageHazards.Landed(s);if(box.enabled)box.transform.position=new Vector3((float)s.X,h.size.y/2,(float)s.Z);
            }
        }
        private void StepAuthoredHazards()
        {
            PoseStageLoads(Match.Time);if(Replica||Match.Phase!=NativePhase.Fight)return;
            foreach(var h in Map.hazards??Array.Empty<MapHazard>())
            {
                V3 center=default,size=default;bool lethal=h.lethal,active=false;int ticks=0;string kind=NativeStageHazards.City(h)?h.kind:"hazard";Vector3 velocity=Vector3.zero;
                if(h.kind=="maglev")
                {active=NativeStageHazards.TrainSweep(h,StageRound,Match.FightAt,Match.Time-1d/60,Match.Time,out center,out size);velocity=new Vector3(NativeCyberCityHazards.Evaluate(h,StageRound,Match.FightAt,Match.Time).Direction*30,6,0);}
                else if(h.kind=="grid")
                {ticks=NativeStageHazards.DamageTicks(h,StageRound,Match.FightAt,Match.Time-1d/60,Match.Time);active=ticks>0;center=h.center;size=h.size;}
                else
                {var s=NativeFactoryHazards.State(h,StageRound,NativeFactoryHazards.Clock(Match.Phase,Match.FightAt,Match.Time));active=NativeStageHazards.FactoryBox(h,s,out center,out size);if(h.kind=="vent"){ticks=NativeStageHazards.DamageTicks(h,StageRound,Match.FightAt,Match.Time-1d/60,Match.Time);active=h.damage>0&&ticks>0;}}
                if(!active)continue;
                foreach(var p in Pilots)
                {
                    if(p.Hp<=0||!p.Seat.connected||KernelLocked(p))continue;bool touch=h.kind=="grid"?NativeStageHazards.GridTouch(h,Vec(p.Body.position)):NativeStageHazards.Touch(Vec(p.Body.position),center,size);
                    if(!touch)continue;
                    if(lethal)p.Knockback+=velocity;
                    Damage(p,null,lethal?p.Hp:h.damage*ticks,velocity.normalized,0,false,forced:lethal,direct:false,primary:false,stage:true,rollAvoidable:false,damageKind:kind,stageHazard:h.id,stageLethal:lethal);
                }
                foreach(var m in Minions.ToArray())
                {
                    if(m.Hp<=0)continue;float half=m.Collider.height/2;bool touch=h.kind=="grid"?NativeStageHazards.GridTouch(h,Vec(m.Body.position),half):NativeStageHazards.Touch(Vec(m.Body.position),center,size,m.Collider.radius,half);
                    if(!touch)continue;float actual=Math.Min(m.Hp,lethal?m.Hp:h.damage*ticks);m.Hp-=actual;m.LastHitAt=Time;Emit(EventKind.HazardHit,null,m.Id,m.Body.position,actual,h.id,damageKind:kind);
                    if(m.Hp<=0){if(!m.Body.isKinematic)m.Body.linearVelocity+=velocity;var at=m.Body.position;bool possessed=m.Pve?.Possessed==true;Emit(EventKind.Death,null,m.Id,at,0,h.id,damageKind:kind);RemoveMinion(m,false);var death=MinionDeaths.LastOrDefault(d=>d.id==m.Id);if(death!=null)death.damageKind=kind;if(possessed)FoundPveBoss(at);}
                }
            }
        }
        private bool StageLinkOpen(MapNavLink link)
        {
            foreach(var id in link.hazards??Array.Empty<string>())
            {var h=Array.Find(Map.hazards??Array.Empty<MapHazard>(),x=>x.id==id);if(h==null||!NativeStageHazards.LinkOpen(h,Match.Phase,StageRound,Match.FightAt,Match.Time,navNodes[link.a].pos,navNodes[link.b].pos))return false;}return true;
        }
        private bool UnsafeStagePath(Vector3 from,Vector3 to,float margin=.45f)
        {
            foreach(var h in Map.hazards??Array.Empty<MapHazard>())
            {
                var a=Vec(from);var b=Vec(to);
                if(NativeStageHazards.City(h))
                {if(NativeStageHazards.Cross(a,b,h.center,h.size,margin)&&!NativeStageHazards.LinkOpen(h,Match.Phase,StageRound,Match.FightAt,Match.Time,a,b))return true;}
                else
                {
                    if(h.kind=="vent"&&h.damage<=0)continue;
                    if(h.kind=="crane")
                    {if((h.zones??Array.Empty<MapVolume>()).Any(z=>NativeStageHazards.Cross(a,b,z.center,z.size,margin))&&!NativeStageHazards.LinkOpen(h,Match.Phase,StageRound,Match.FightAt,Match.Time,a,b))return true;}
                    else
                    {V3 c=(h.a+h.b)*.5f,s=new V3(Math.Abs(h.a.x-h.b.x)+h.size.x,Math.Abs(h.a.y-h.b.y)+h.size.y,Math.Abs(h.a.z-h.b.z)+h.size.z);if(h.kind=="vent"){c=h.a+new V3(0,h.size.y/2,0);s=h.size;}if(NativeStageHazards.Cross(a,b,c,s,margin)&&!NativeStageHazards.LinkOpen(h,Match.Phase,StageRound,Match.FightAt,Match.Time,a,b))return true;}
                }
            }return false;
        }
        private bool SupportedStageMove(Vector3 pos,Vector3 move)
        {
            if(Map.hazards==null||move.sqrMagnitude<.001f)return true;
            var ahead=pos+move.normalized*.7f;
            return (UnsafeStagePath(pos,pos)||!UnsafeStagePath(pos,ahead))&&root.scene.GetPhysicsScene().Raycast(ahead+Vector3.up*.3f,Vector3.down,out _,1.65f,WorldMask,QueryTriggerInteraction.Ignore);
        }
    }
}
