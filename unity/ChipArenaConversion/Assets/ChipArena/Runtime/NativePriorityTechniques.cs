using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private bool PriorityActionLock(NativePilot p)=>NativeTechniqueRules.Live(p.Technique,Time)&&(p.Technique.Kind=="iaido"||p.Technique.Kind=="blade-drag"&&p.Technique.Phase=="drag");
        private bool IceAt(NativePilot owner,Vector3 at)=>ChannelLive(owner,"flash-freeze")&&Math.Abs(at.y-.865f-owner.Technique.Point.y)<.45f&&new Vector2(at.x-owner.Technique.Point.x,at.z-owner.Technique.Point.z).magnitude<=3;
        private float PriorityGrip(NativePilot p)=>p.Grounded&&Pilots.Any(q=>q!=p&&CanHit(q,p)&&IceAt(q,p.Body.position))?.25f:1;
        private float PrioritySpeed(NativePilot p,Vector3 move)
        {
            var k=p.Technique;float speed=1;
            if(NativeTechniqueRules.Live(k,Time))
            {
                if(k.Kind=="iaido"&&k.Phase=="sheathe")speed*=.5f;
                if(k.Kind=="blade-drag"&&k.Phase=="drag")speed*=1.2f;
                if(k.Kind=="lodestone"&&!k.Recast(Time))speed*=.6f;
                if(k.Kind=="challenge"&&k.Phase=="duel"){var t=MeleeTarget(p,k.Target);if(t.collider!=null&&Vector3.Dot(move.normalized,(t.at-p.Body.position).normalized)>.5f)speed*=1.15f;}
            }
            if(k?.Kind=="breacher-round"&&Time>=k.At&&Time<k.Release)speed*=.4f;
            if(p.Grounded){if(IceAt(p,p.Body.position))speed*=1.2f;if(Pilots.Any(q=>q!=p&&CanHit(q,p)&&IceAt(q,p.Body.position)))speed*=.85f;}
            return speed;
        }
        private void PriorityMark(NativePilot owner,int id,string kind,float seconds)
        {
            var t=MeleeTarget(owner,id);if(t.pilot!=null)MarkTechnique(t.pilot,kind,owner,seconds);
            else if(t.minion!=null)t.minion.TechniqueMarks=NativeCombatMarks.Put(t.minion.TechniqueMarks,kind,owner.Seat.id,Time,Time+seconds);
        }
        private void EndChallenge(NativePilot p)
        {
            int target=p.Technique.Target;var t=MeleeTarget(p,target);
            p.TechniqueMarks=p.TechniqueMarks.Where(m=>m.Kind!="challenge"||m.By!=target).ToArray();
            if(t.pilot!=null)t.pilot.TechniqueMarks=t.pilot.TechniqueMarks.Where(m=>m.Kind!="challenge"||m.By!=p.Seat.id).ToArray();
            if(t.minion!=null)t.minion.TechniqueMarks=t.minion.TechniqueMarks.Where(m=>m.Kind!="challenge"||m.By!=p.Seat.id).ToArray();EndTechnique(p);
        }
        private bool ReleasePriorityTechnique(NativePilot p)
        {
            var k=p.Technique;var feet=p.Body.position-Vector3.up*.865f;
            switch(k.Kind)
            {
                case "iaido":k.Phase="sheathe";k.Until=k.Release+(AbilityChip(p,"ab-moonlit-edge")?1:1.5f);k.RecoverUntil=k.Until;break;
                case "challenge":
                    k.Target=ThirdAimTarget(p,p.Input,14,bossAllowed:false);if(k.Target==0){EndTechnique(p);return true;}
                    k.Phase="duel";p.TechniqueNext=Time;PriorityMark(p,k.Target,"challenge",6);
                    p.TechniqueMarks=NativeCombatMarks.Put(p.TechniqueMarks,"challenge",k.Target,Time,k.Until);break;
                case "war-cry":
                    k.Phase="rally";k.Point=Vec(feet);k.HitAt=Time;
                    foreach(var t in MeleeTargets(p).ToArray())if(Vector3.Distance(t.at,p.Body.position)<=8&&MeleeVisible(p.Body.position,t.at,p,t.collider))PriorityMark(p,t.id,"war-cry",4);break;
                case "sunder":
                    k.Phase="smash";k.Point=Vec(feet);k.Dir=V3.Forward(p.Input.Yaw);k.HitAt=Time;
                    foreach(var t in MeleeTargets(p).ToArray())if(Vector3.Distance(t.at,p.Body.position)<=3&&Vector3.Angle(Vec(k.Dir),t.at-p.Body.position)<=45&&MeleeVisible(p.Body.position,t.at,p,t.collider)&&TechniqueHit(p,t.id,14,t.at,Vec(k.Dir))>0)PriorityMark(p,t.id,"sunder",4);break;
                case "ghillie":k.Phase="hidden";k.Point=Vec(feet);break;
                case "flash-freeze":k.Phase="ice";k.Point=Vec(GroundPoint(feet));break;
                default:return ReleasePriorityProjectile(p);
            }
            return true;
        }
        private bool StepPriorityTechnique(NativePilot p)
        {
            var k=p.Technique;
            switch(k.Kind)
            {
                case "iaido":if(k.Phase=="sheathe"&&Time>=k.Until)FinishPriorityTechnique(p);return true;
                case "challenge":
                    if(k.Phase!="duel")return true;var t=MeleeTarget(p,k.Target);
                    if(t.collider==null||Time>=k.Until){EndChallenge(p);return true;}
                    if(MeleeVisible(p.Body.position,t.at,p,t.collider))p.TechniqueNext=Time;
                    if(Time-p.TechniqueNext>=2)EndChallenge(p);return true;
                case "ghillie":if(ChannelLive(p,"ghillie")&&p.Body.linearVelocity.magnitude>=.5f)EndTechnique(p);return true;
                case "flash-freeze":
                    if(NativeTechniqueRules.Live(k,Time))foreach(var m in Minions.Where(m=>m.Hp>0&&CanHit(p,m)&&IceAt(p,m.Body.position)).ToArray())SlowMinion(m,.85f,.05f);return true;
                case "war-cry":case "sunder":return true;
                default:return StepPriorityProjectile(p);
            }
        }
        private void FinishPriorityTechnique(NativePilot p)
        {
            if(TechniqueBlocked(p)){EndTechnique(p);return;}var k=p.Technique;if(k.Kind!="iaido"){FinishPriorityProjectile(p);return;}if(k.Phase!="sheathe")return;
            float charge=Mathf.Clamp01((Time-k.Release)/(AbilityChip(p,"ab-moonlit-edge")?1:1.5f));
            Vector3 start=p.Body.position,dir=Vec(V3.Forward(p.Input.Yaw));float length=ForwardAbilityStep(start,dir,AbilityChip(p,"ab-moonlit-edge")?8:5,out _);
            if(!TechniqueDestination(p,start+dir*length))length=0;
            k.From=Vec(start-Vector3.up*.865f);k.To=k.From+Vec(dir*length);k.Dir=Vec(dir);k.Arm=charge;k.RecastAt=Time;k.Phase="draw";k.Until=k.RecoverUntil=Time+.3f;p.Ready=k.RecoverUntil;
            foreach(var t in MeleeTargets(p).ToArray())if(NativeTechniqueRules.LineDistance(Vec(t.at),Vec(start),Vec(start+dir*length))<=.6f&&MeleeVisible(start,t.at,p,t.collider))TechniqueHit(p,t.id,8+14*charge,t.at,dir);
            k.HitAt=Time;
        }
        private bool PriorityTechniqueMotion(NativePilot p,ref Vector3 velocity)
        {
            var k=p.Technique;if(!NativeTechniqueRules.Live(k,Time)||k.Kind!="iaido"||k.Phase!="draw"||Time>=k.RecastAt+.12f)return false;
            Vector3 desired=Vector3.Lerp(Vec(k.From),Vec(k.To),Mathf.Clamp01((Time+1f/60-k.RecastAt)/.12f))+Vector3.up*.865f-p.Body.position;
            float length=ForwardAbilityStep(p.Body.position,desired.normalized,desired.magnitude,out _);velocity=desired.normalized*length*60;p.MoveVelocity=p.Knockback=Vector3.zero;p.Body.useGravity=false;return true;
        }
        private float PriorityDamage(NativePilot source,NativeMinion attacker,int target,NativeTechniqueMark[] marks,float amount)
        {
            int by=attacker?.Id??source?.Seat.id??0;amount*=WildOutgoing(by);
            if(NativeCombatMarks.Has(marks,"sunder",Time))amount*=1.2f;
            // A challenge remains reciprocal only while its owner is alive and the duel is active.
            if(by!=0&&Pilots.Any(p=>p.Hp>0&&ChannelLive(p,"challenge")&&p.Technique.Phase=="duel"&&((p.Seat.id==by&&p.Technique.Target==target)||(p.Seat.id==target&&p.Technique.Target==by))))amount*=1.2f;
            var outgoing=attacker?.TechniqueMarks??source?.TechniqueMarks;
            if(outgoing!=null&&outgoing.Any(m=>m.Kind=="war-cry"&&m.At<=Time&&Time<m.Until&&m.By!=target))amount*=.75f;
            var victim=Pilots.Find(p=>p.Seat.id==target);if(victim!=null&&ChannelLive(victim,"war-cry"))amount*=.85f;
            if(BubbleHeld(target))amount*=.5f;return amount;
        }
        private NativePilot PriorityTaunt(NativeTechniqueMark[] marks)=>Pilots.FirstOrDefault(p=>p.Hp>0&&p.Seat.connected&&marks!=null&&marks.Any(m=>m.Kind=="war-cry"&&m.By==p.Seat.id&&m.At<=Time&&Time<m.Until));
        private void StepPriorityConstraints()
        {
            foreach(var m in Minions.Where(m=>m.Hp>0).ToArray())
            {var taunt=PriorityTaunt(m.TechniqueMarks);if(taunt!=null&&CanHit(taunt,m)){m.Foe=taunt;m.Other=null;if(m.Pve!=null){m.Pve.Target=taunt;m.Pve.ThinkAt=Math.Max(m.Pve.ThinkAt,Time+.1f);}}}
            StepPriorityProjectileConstraints();FinalTechniqueConstraints();
        }
    }
}
