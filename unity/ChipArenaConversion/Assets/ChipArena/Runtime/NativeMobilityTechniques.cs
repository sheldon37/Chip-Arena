using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private bool MobilityActionLock(NativePilot p)
        {
            var k=p.Technique;return NativeTechniqueRules.Live(k,Time)&&(k.Kind=="hydro-jet"||k.Kind=="disarm"&&Time<k.RecoverUntil||k.Kind=="ground-slam"&&k.Phase=="leap"||k.Kind=="vault-shot"&&Time<k.RecoverUntil);
        }
        private bool ReleaseMobilityTechnique(NativePilot p)
        {
            var k=p.Technique;Vector3 feet=p.Body.position-Vector3.up*.865f,forward=Vec(V3.Forward(p.Input.Yaw));
            switch(k.Kind)
            {
                case "disarm":
                    k.From=Vec(feet);k.Dir=Vec(forward);k.Phase="lunge";k.Arm=k.Release+.22f;k.RecoverUntil=k.Release+.72f;break;
                case "ground-slam":case "vault-shot":
                    k.From=Vec(feet);k.Dir=k.Kind=="vault-shot"?Vec(-forward):Vec(forward);
                    var delta=k.Kind=="vault-shot"?-forward*5:Vector3.ClampMagnitude(AimedAbilityPoint(p,5)-feet,5);delta.y=0;
                    float allowed=ForwardAbilityStep(p.Body.position,delta.normalized,delta.magnitude,out _);
                    var destination=feet+delta.normalized*allowed;if(!TechniqueDestination(p,destination+Vector3.up*.865f))destination=feet;
                    k.To=k.Point=Vec(destination);k.Arm=k.Release+(k.Kind=="vault-shot"?.225f:.5f);k.Phase=k.Kind=="vault-shot"?"vault":"leap";
                    k.RecoverUntil=k.Release+(k.Kind=="vault-shot"?.45f:.5f);break;
                case "reaping-sweep":
                    k.Phase="sweep";k.Point=Vec(feet);k.Dir=Vec(forward);
                    foreach(var t in MeleeTargets(p).ToArray())
                    {
                        Vector3 offset=t.at-p.Body.position;
                        bool grounded=t.pilot!=null?t.pilot.Grounded:Math.Abs(t.at.y-p.Body.position.y)<.65f&&Math.Abs(t.minion.Body.linearVelocity.y)<1;
                        if(!grounded||offset.magnitude>4||Vector3.Angle(forward,offset)>150||!MeleeVisible(p.Body.position,t.at,p,t.collider))continue;
                        if(TechniqueHit(p,t.id,10,t.at,offset.normalized)<=0)continue;k.HitAt=Time;
                        // Pull along the clear segment, never teleport through cover or into the caster.
                        float distance=Math.Max(0,offset.magnitude-1.6f);TechniqueShove(p,t.id,-offset.normalized,Math.Min(8,distance/.3f));
                        if(t.pilot!=null){MarkTechnique(t.pilot,"reaping-sweep",p,.5f);StunMelee(t.pilot,.5f);CancelAbilities(t.pilot);CancelSpecial(t.pilot);EndTechnique(t.pilot);}
                        else {SlowMinion(t.minion,.01f,.5f);t.minion.NextAttack=Math.Max(t.minion.NextAttack,Time+.5f);t.minion.StrikeAt=-1;if(t.minion.Pve!=null){t.minion.Pve.StaggerAt=Time+.5f;t.minion.Pve.Struck=true;}}
                    }
                    return true;
                case "bolas-round":k.Phase="flight";k.Point=k.From;k.Until=k.Release+40f/45;return true;
                case "hydro-jet":k.Phase="jet";k.RecoverUntil=k.Until;break;
                default:return false;
            }
            p.Ready=Math.Max(p.Ready,k.RecoverUntil);return true;
        }
        private bool StepMobilityTechnique(NativePilot p)
        {
            var k=p.Technique;
            switch(k.Kind)
            {
                case "disarm":
                    if(k.Phase=="lunge")
                    {
                        Vector3 start=Vec(k.From)+Vector3.up*.865f,end=p.Body.position+Vec(k.Dir)*.4f;
                        foreach(var t in MeleeTargets(p).OrderBy(t=>(t.at-start).sqrMagnitude).ToArray())
                        {
                            if(NativeTechniqueRules.LineDistance(Vec(t.at),Vec(start),Vec(end))>.7f||!MeleeVisible(start,t.at,p,t.collider))continue;
                            if(TechniqueHit(p,t.id,8,t.at,Vec(k.Dir))<=0)continue;
                            k.Target=t.id;k.Point=Vec(t.at);k.HitAt=Time;k.Phase="flick";k.Until=Time+2.5f;k.RecoverUntil=Time+.12f;p.Ready=k.RecoverUntil;
                            if(t.pilot!=null)MarkTechnique(t.pilot,"disarm",p,2.5f);break;
                        }
                        if(k.Phase=="lunge"&&Time>=k.Arm){k.Phase="recovery";k.Until=k.RecoverUntil=Math.Max(k.Release,Time+.5f);p.Ready=k.RecoverUntil;}
                    }
                    return true;
                case "ground-slam":
                    if(k.Phase=="leap"&&Time>=k.Arm)
                    {
                        k.Phase="slam";k.HitAt=Time;k.Point=Vec(p.Body.position-Vector3.up*.865f);k.Until=Time+(AbilityChip(p,"ab-aftershock-slam")?.8f:.3f);p.TechniqueNext=Time+.5f;
                        foreach(var t in MeleeTargets(p).ToArray())if((t.pilot==null||t.pilot.Grounded)&&Vector3.Distance(t.at,Vec(k.Point)+Vector3.up*.65f)<=4&&MeleeVisible(p.Body.position,t.at,p,t.collider)&&TechniqueHit(p,t.id,12,t.at,Vector3.up)>0)
                        {TechniqueShove(p,t.id,Vector3.up,4.6f);if(t.pilot!=null)StunMelee(t.pilot,.4f);else {SlowMinion(t.minion,.01f,.4f);t.minion.NextAttack=Math.Max(t.minion.NextAttack,Time+.4f);}}
                    }
                    if(k.Phase=="slam"&&AbilityChip(p,"ab-aftershock-slam")&&Time>=p.TechniqueNext&&p.TechniqueNext>k.HitAt){p.TechniqueNext=0;TechniqueBurst(p,Vec(k.Point)+Vector3.up*.65f,6,6,0);}
                    return true;
                case "vault-shot":
                    if(k.Phase=="vault"&&Time>=k.Arm)
                    {
                        k.Phase="shot";var from=p.Body.position+Vector3.up*.3f;var dir=Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch));k.Dir=Vec(dir);
                        // The arrow remains in this record, so late frames/killcam retain its actual contact.
                        k.Point=Vec(from);p.TechniquePosition=from;k.Path=new[]{Vec(from)};
                    }
                    if(k.Phase=="shot"&&k.HitAt<-50&&k.Path?.Length==1)MobilityFlight(p,45,40,14,true);
                    return true;
                case "bolas-round":
                    if(k.Phase=="flight")MobilityFlight(p,45,40,4,false);
                    if(k.Phase=="bound"&&Time<k.Until)
                    {
                        var t=MeleeTarget(p,k.Target);if(t.collider==null){EndTechnique(p);return true;}
                        if(t.pilot!=null&&Time<t.pilot.BlockEnd){t.pilot.TechniqueMarks=t.pilot.TechniqueMarks.Where(m=>m.Kind!="bolas-round"||m.By!=p.Seat.id).ToArray();EndTechnique(p);}
                        else if(t.minion!=null)SlowMinion(t.minion,IsZoneBoss(t.minion)?.6f:.01f,.05f);
                    }
                    return true;
                case "hydro-jet":
                    if(!NativeTechniqueRules.Live(k,Time))return true;
                    k.Dir=GunRules.Direction(p.Input.Yaw,p.Input.Pitch);
                    foreach(var t in MeleeTargets(p).ToArray())if(NativeTechniqueRules.LineDistance(Vec(t.at),Vec(p.Body.position),Vec(p.Body.position)+k.Dir*6)<.6f&&MeleeVisible(p.Body.position,t.at,p,t.collider))MobilityPush(p,t.id,Vec(k.Dir),.1f);
                    return true;
                case "reaping-sweep":return true;
                default:return false;
            }
        }
        private void MobilityFlight(NativePilot p,float speed,float range,float damage,bool vault)
        {
            var k=p.Technique;Vector3 origin=vault?Vec(k.Path[0]):Vec(k.From);float elapsed=Math.Max(0,Time-(vault?k.Arm:k.Release));
            Vector3 from=Vec(k.Point),end=origin+Vec(k.Dir)*Math.Min(range,elapsed*speed);if(vault)end.y-=9*elapsed*elapsed;Vector3 delta=end-from;
            if(TechniqueHubCross(from,end)){EndTechnique(p);return;}
            if(TechniqueObstacle(p,from,delta,.16f,damage*p.TechniqueScale,out var blocked)){k.Point=Vec(blocked);k.HitAt=Time;EndTechnique(p);return;}
            var hit=SweepNative(from,delta,.16f,c=>c!=p.Collider&&(c.GetComponent<NativePilotBody>()?.Pilot is NativePilot q?q.Hp>0&&CanHit(p,q):c.GetComponent<NativeMinionBody>()?.Minion is NativeMinion m?m.Hp>0&&CanHit(p,m):true));
            p.TechniquePosition=hit?.Point??end;k.Point=Vec(p.TechniquePosition);
            if(!hit.HasValue&&elapsed*speed<range)return;k.Point=Vec(p.TechniquePosition);k.HitAt=Time;
            var target=hit?.Collider.GetComponent<NativePilotBody>()?.Pilot;var minion=hit?.Collider.GetComponent<NativeMinionBody>()?.Minion;int id=target?.Seat.id??minion?.Id??0;k.Target=id;
            if(id==0||TechniqueHit(p,id,damage,p.TechniquePosition,Vec(k.Dir))<=0){k.Phase="recovery";k.Until=Math.Max(k.Release,Time);return;}
            if(vault){k.Until=Math.Max(k.Release+.45f,Time+.15f);return;}
            k.Phase="bound";k.Until=Time+1.2f;
            if(target!=null){MarkTechnique(target,"bolas-round",p,1.2f);if(target.Seat.AuthorityBoss)Slow(target,.6f,1.2f);}
            else SlowMinion(minion,IsZoneBoss(minion)?.6f:.01f,1.2f);
        }
        private void MobilityPush(NativePilot owner,int id,Vector3 direction,float distance)
        {
            var t=MeleeTarget(owner,id);if(t.collider==null||t.pilot!=null&&(Invulnerable(t.pilot)||SirenArmor(t.pilot))||t.minion!=null&&IsZoneBoss(t.minion))return;
            var body=t.pilot?.Body??t.minion.Body;
            float step=ForwardAbilityStep(body.position,direction.normalized,distance,out _);var at=body.position+direction.normalized*step;
            if(HubInside(at)||HubCross(body.position,at))return;
            body.position=ClampMap(at);
        }
        private void StepBolasConstraints()
        {
            foreach(var owner in Pilots)
            {
                var k=owner.Technique;if(k?.Kind!="bolas-round"||k.Phase!="bound"||!NativeTechniqueRules.Live(k,Time))continue;
                var t=MeleeTarget(owner,k.Target);
                if(t.pilot!=null){var v=t.pilot.Body.linearVelocity;BolasMotion(t.pilot,ref v);t.pilot.Body.linearVelocity=v;}
                else if(t.minion!=null&&!IsZoneBoss(t.minion)&&!t.minion.Body.isKinematic)
                {var v=t.minion.Body.linearVelocity;t.minion.Body.linearVelocity=new Vector3(0,v.y,0);if(t.minion.Pve!=null)t.minion.Pve.External=Vector3.zero;}
            }
        }
        private void BolasMotion(NativePilot p,ref Vector3 velocity)
        {
            if(p.Seat.AuthorityBoss)return;
            foreach(var owner in Pilots)
            {var k=owner.Technique;if(k?.Kind!="bolas-round"||k.Phase!="bound"||k.Target!=p.Seat.id||!NativeTechniqueRules.Live(k,Time)||Time<p.BlockEnd)continue;velocity=Vector3.zero;p.MoveVelocity=p.Knockback=Vector3.zero;return;}
        }
        private readonly RaycastHit[] techniqueBodyHits=new RaycastHit[32];
        private float TechniqueBodyStep(NativePilot p,Vector3 delta,bool floorIsSupport,out bool blocked,out RaycastHit contact)
        {
            blocked=false;contact=default;float distance=delta.magnitude;if(distance<.00001f)return 0;
            var c=p.Collider;var scale=c.transform.lossyScale;float radius=c.radius*Math.Max(Math.Abs(scale.x),Math.Abs(scale.z));
            float half=Math.Max(0,c.height*Math.Abs(scale.y)*.5f-radius);var center=p.Body.position+c.transform.TransformVector(c.center);
            // Inset the query skin so a resting capsule does not start overlapped with its supporting floor.
            radius=Math.Max(.01f,radius-NativeLocomotionRules.Skin);
            // Use the current crouched/shrunk capsule; the older opener sweep assumes a standing body.
            var hits=techniqueBodyHits;int count=root.scene.GetPhysicsScene().CapsuleCast(center+Vector3.up*half,center-Vector3.up*half,radius,delta/distance,hits,distance+2*NativeLocomotionRules.Skin,WorldMask,QueryTriggerInteraction.Ignore);
            float nearest=float.PositiveInfinity;
            for(int i=0;i<count;i++){var h=hits[i];if(floorIsSupport&&h.normal.y>.5f)continue;if(h.distance<nearest){nearest=h.distance;contact=h;blocked=true;}}
            return blocked?Math.Max(0,Math.Min(distance,nearest-NativeLocomotionRules.Skin)):distance;
        }
        private bool MobilityTechniqueMotion(NativePilot p,ref Vector3 velocity)
        {
            var k=p.Technique;if(!NativeTechniqueRules.Live(k,Time)||TechniqueBlocked(p))return false;
            Vector3 desired;
            if(k.Kind=="hydro-jet")desired=-Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch))*9/60;
            else if(k.Kind=="disarm"&&k.Phase=="lunge"&&Time<k.Arm)desired=Vec(k.Dir)*(2.2f/.22f/60);
            else if((k.Kind=="ground-slam"&&k.Phase=="leap")||(k.Kind=="vault-shot"&&Time<k.RecoverUntil))
            {
                float duration=k.Kind=="vault-shot"?.45f:.5f,u=Mathf.Clamp01((Time+1f/60-k.Release)/duration),height=k.Kind=="vault-shot"?1.5f:3;
                desired=Vector3.Lerp(Vec(k.From),Vec(k.To),u)+Vector3.up*(.865f+4*height*u*(1-u))-p.Body.position;
            }
            else return false;
            RaycastHit contact=default;bool wall;float allowed=k.Kind=="ground-slam"?TechniqueBodyStep(p,desired,false,out wall,out contact):ForwardAbilityStep(p.Body.position,desired.normalized,desired.magnitude,out wall);var step=desired.normalized*allowed;
            if(!TechniqueDestination(p,p.Body.position+step)){step=Vector3.zero;wall=true;contact=default;}
            velocity=step*60;p.MoveVelocity=p.Knockback=Vector3.zero;p.Body.useGravity=false;p.Grounded=false;
            // Collision ends the move. Slam resolves at the actual reachable landing, never through a wall.
            if(wall)
            {
                if(k.Kind=="ground-slam")
                {
                    if(desired.y<0&&contact.collider!=null&&contact.normal.y>.5f)k.Arm=Time;
                    else EndTechnique(p); // A ceiling/side collision cancels; it must not detonate in midair.
                }
                else if(k.Kind=="disarm")k.Arm=Time;
                else if(k.Kind=="hydro-jet"){k.RecastAt=Time;EndTechnique(p);}
                else {k.RecoverUntil=Time;p.Ready=Time;}
            }
            return true;
        }
    }
}
