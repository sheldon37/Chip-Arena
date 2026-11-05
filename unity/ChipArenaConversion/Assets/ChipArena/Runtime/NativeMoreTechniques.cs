using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private bool TechniqueActionLock(NativePilot p)=>ChannelLive(p,"bipod")||TechniqueBusy(p)&&(p.Technique.Kind=="dragon-breath"||p.Technique.Kind=="soul-siphon"||p.Technique.Kind=="shield-surf"||p.Technique.Kind=="iron-vow"||p.Technique.Kind=="grave-walk"||p.Technique.Kind=="recoil-burst"||p.Technique.Kind=="parting-line");
        private bool TechniqueRunning(NativePilot p)=>p.Technique?.Kind=="wildfire-run"&&Time>=p.Technique.Release&&Time<p.Technique.Arm;
        private bool TechniqueBrace(NativePilot p)=>p.Technique?.Kind=="iron-vow"&&p.Technique.Phase!="release"&&NativeTechniqueRules.Live(p.Technique,Time);
        private bool TechniqueGhost(NativePilot p)=>p.Technique?.Kind=="grave-walk"&&NativeTechniqueRules.Live(p.Technique,Time);
        private bool ReleaseMoreTechnique(NativePilot p,Vector3 feet,Vector3 forward)
        {
            var k=p.Technique;
            switch(k.Kind)
            {
                case "parting-line":p.TechniqueMoveFrom=p.Body.position;k.From=k.Point=Vec(GroundPoint(feet));k.Dir=Vec(forward);k.Arm=k.Release+.7f;k.Until=k.Arm+.3f;k.RecoverUntil=k.Release+.28f;k.Phase="hop";break;
                case "tripwire":k.From=Vec(feet+Vector3.up*.45f);k.Arm=k.Release+.32f;k.Until=k.Arm+8;p.TechniqueHealth=10;break;
                case "shield-surf":k.Dir=Vec(forward);k.Phase="ride";k.Until=k.Release+2.6f;k.RecoverUntil=k.Until;break;
                case "iron-vow":k.Point=Vec(GroundPoint(feet+forward*.62f));k.Dir=Vec(forward);k.Phase="brace";k.Until=k.Release+2.4f;k.RecoverUntil=k.Until+.2f;break;
                case "bounty-round":k.Phase="shot";k.Point=k.From+k.Dir*60;k.Until=k.Release+1;break;
                case "recoil-burst":
                    k.Phase="blast";k.Until=k.Release+.45f;k.RecoverUntil=k.Until+.1f;p.Ammo=0;
                    foreach(var t in MeleeTargets(p).ToArray())if(Vector3.Distance(t.at,Vec(k.From))<=6&&Vector3.Dot((t.at-Vec(k.From)).normalized,Vec(k.Dir))>=.65f&&MeleeVisible(Vec(k.From),t.at,p,t.collider))TechniqueHit(p,t.id,10,t.at,Vec(k.Dir));
                    break;
                case "sonar-dart":
                    // Resolve the normal from the committed hand-to-surface ray, not a later camera aim.
                    var ray=Vec(k.Point)-Vec(k.From);if(root.scene.GetPhysicsScene().Raycast(Vec(k.From),ray.normalized,out var hit,ray.magnitude+.1f,WorldMask,QueryTriggerInteraction.Ignore))k.Dir=Vec(hit.normal);
                    k.Arm=k.Release+.5f;k.Until=k.Arm+3*1.2f;p.TechniqueHealth=10;break;
                case "spirit-hawk":k.Phase="flight";k.Point=k.From+k.Dir*30;k.Arm=k.Release+1.5f;k.Until=k.Arm+.9f;break;
                case "wildfire-run":k.Phase="run";k.Arm=k.Release+3;k.Until=k.Arm+3-NativeTechniques.PathStep;k.Path=new[]{Vec(GroundPoint(feet))};p.TechniqueNext=k.Release+.25f;break;
                case "slipstream":
                    k.Phase="stream";k.From=Vec(GroundPoint(feet));var end=feet+forward*14;
                    if(root.scene.GetPhysicsScene().Raycast(feet+Vector3.up*.2f,forward,out var stop,14,WorldMask,QueryTriggerInteraction.Ignore))end=stop.point-forward*.2f;
                    k.To=Vec(GroundPoint(end));if(TechniqueHubCross(feet,end))k.To=k.From;break;
                case "lightning-rod":
                    k.Point=Vec(GroundPoint(AimedAbilityPoint(p,12)));if(TechniqueHubInside(Vec(k.Point))||TechniqueHubCross(p.Body.position,Vec(k.Point)))k.Point=Vec(GroundPoint(feet));
                    k.Arm=k.Release+.3f;k.Until=k.Arm+5;p.TechniqueHealth=60;break;
                case "grave-walk":k.From=Vec(feet);k.Phase="walk";k.Until=k.Release+1.5f;k.RecoverUntil=k.Until+.34f;break;
                default:return false;
            }
            p.Ready=Math.Max(p.Ready,k.RecoverUntil);return true;
        }
        private void FinishMoreTechnique(NativePilot p)
        {
            var k=p.Technique;if(k==null||k.Recast(Time))return;k.RecastAt=Time;
            if(k.Kind=="iron-vow")
            {
                k.Phase="release";k.Until=Time;k.RecoverUntil=Time+.2f;
                foreach(var t in MeleeTargets(p).ToArray())if(Vector3.Distance(p.Body.position,t.at)<=3.5f&&MeleeVisible(p.Body.position,t.at,p,t.collider))TechniqueShove(p,t.id,(t.at-p.Body.position).normalized,5);
            }
            else if(k.Kind=="shield-surf"){k.Phase="hop";k.Until=k.RecoverUntil=Time+.25f;var v=p.Body.linearVelocity;v.y=Math.Max(v.y,3);p.Body.linearVelocity=v;p.Grounded=false;}
            else if(k.Kind=="grave-walk"){k.Phase="rise";k.Until=Time;k.RecoverUntil=Time+.34f;}
            p.Ready=k.RecoverUntil;
        }
        private void TechniqueMarkedDeath(int id)
        {
            foreach(var p in Pilots){var k=p.Technique;if(k?.Kind!="bounty-round"||k.Phase!="marked"||k.Target!=id||!NativeTechniqueRules.Live(k,Time))continue;if(p.TechniqueCastSlot=="q")p.QReady=Time;else p.EReady=Time;k.Until=Time;k.Phase="recovery";}
        }
        private void TechniqueShove(NativePilot owner,int id,Vector3 direction,float speed)
        {
            var t=MeleeTarget(owner,id);if(t.pilot!=null&&!Invulnerable(t.pilot)&&!SirenArmor(t.pilot)&&CanHit(owner,t.pilot)){t.pilot.Knockback+=direction*speed;t.pilot.Grounded=false;}
            else if(t.minion!=null&&CanHit(owner,t.minion)&&!t.minion.Body.isKinematic)t.minion.Body.AddForce(direction*speed*65,ForceMode.Impulse);
        }
        private bool StepMoreTechnique(NativePilot p)
        {
            var k=p.Technique;if(k.Phase=="recovery")return true;
            switch(k.Kind)
            {
                case "parting-line":
                    if(k.Phase!="burst"&&Time>=k.Arm)
                    {
                        k.Phase="burst";var right=new V3(k.Dir.z,0,-k.Dir.x);var a=k.Point-right*1.5f;var b=k.Point+right*1.5f;
                        foreach(var t in MeleeTargets(p).ToArray())if(NativeTechniqueRules.LineDistance(Vec(t.at),a+new V3(0,.65f,0),b+new V3(0,.65f,0))<=1.2f&&MeleeVisible(Vec(k.Point)+Vector3.up*.2f,t.at,p,t.collider))TechniqueHit(p,t.id,14,t.at,Vec(k.Dir),325);
                    }else if(k.Phase=="hop"&&Time>=k.Release+.28f)k.Phase="line";return true;
                case "tripwire":
                    if(Time>=k.Until||k.HitAt>-50)return true;if(Time<k.Arm)return true;k.Phase="armed";
                    foreach(var t in MeleeTargets(p).OrderBy(t=>t.id))if((t.pilot!=null?Enemy(p,t.pilot):Enemy(p,t.minion))&&NativeTechniqueRules.LineDistance(Vec(t.at),k.From,k.Point)<.65f&&MeleeVisible(Vec(k.Point),t.at,p,t.collider))
                    {if(t.pilot!=null&&Invulnerable(t.pilot))continue;k.HitAt=Time;k.To=Vec(t.at);k.Phase="snap";k.Until=Time+.85f;p.TechniqueHealth=0;if(TechniqueHit(p,t.id,8,t.at,Vec(k.Dir))>0){if(t.pilot!=null){t.pilot.SlowFactor=.4f;t.pilot.SlowEnd=Math.Max(t.pilot.SlowEnd,Time+.8f);}else SlowMinion(t.minion,.4f,.8f);}break;}return true;
                case "shield-surf":case "iron-vow":case "grave-walk":if(Time>=k.Until&&!k.Recast(Time))FinishMoreTechnique(p);return true;
                case "bounty-round":
                    if(k.Phase=="shot")MoreTechniqueFlight(p,120,60,false);
                    if(k.Phase=="marked"&&Time<k.Until)
                    {var target=Pilots.FirstOrDefault(q=>q.Seat.id==k.Target);var m=Minions.FirstOrDefault(q=>q.Id==k.Target);if(target!=null&&target.Hp<=0||m!=null&&m.Hp<=0){if(p.TechniqueCastSlot=="q")p.QReady=Time;else p.EReady=Time;k.Until=Time;k.Phase="recovery";}}
                    return true;
                case "recoil-burst":if(Time>=k.Until&&k.Phase=="blast"){k.Phase="recovery";Reload(p,true);}return true;
                case "sonar-dart":if(Time>=k.Arm&&Time<k.Until)k.Phase="ping";return true;
                case "spirit-hawk":
                    if(k.Phase=="flight")MoreTechniqueFlight(p,20,30,true);
                    else if(k.Phase=="dive"&&k.HitAt<-50)
                    {
                        var t=MeleeTarget(p,k.Target);if(t.collider==null||Time>=k.Until){k.Phase="recovery";return true;}k.To=Vec(t.at);
                        var from=p.TechniquePosition;var delta=Vector3.ClampMagnitude(t.at-from,20f/60);var h=SweepNative(from,delta,.2f,c=>c!=p.Collider);p.TechniquePosition=h?.Point??from+delta;
                        if(h.HasValue||Vector3.Distance(p.TechniquePosition,t.at)<.45f)
                        {k.HitAt=Time;k.Until=Time+.3f;if((!h.HasValue||h.Value.Collider==t.collider)&&!(t.pilot!=null&&Invulnerable(t.pilot))&&TechniqueHit(p,t.id,10,t.at,delta.normalized)>0){if(t.pilot!=null)Slow(t.pilot,.8f,1.5f);else SlowMinion(t.minion,.8f,1.5f);}}
                    }return true;
                case "wildfire-run":
                    // Fixed sample timestamps are implicit in the array; never shift it while flames age out.
                    int wanted=Math.Min(24,Math.Max(1,(int)((Math.Min(Time,k.Arm-.001f)-k.Release)/NativeTechniques.PathStep)+1));
                    if(Time<k.Arm&&k.Path.Length<wanted){var points=k.Path;Array.Resize(ref points,wanted);for(int i=k.Path.Length;i<wanted;i++)points[i]=Vec(GroundPoint(p.Body.position-Vector3.up*.865f));k.Path=points;}
                    if(Time<k.Until&&Time>=p.TechniqueNext)
                    {
                        p.TechniqueNext=Time+.25f;
                        foreach(var t in MeleeTargets(p).ToArray())if(t.pilot!=null?Enemy(p,t.pilot):Enemy(p,t.minion))for(int i=0;i<k.Path.Length;i++)
                            if(Time<k.Release+i*NativeTechniques.PathStep+3&&Vector3.Distance(t.at,Vec(k.Path[i])+Vector3.up*.6f)<=1.15f&&MeleeVisible(Vec(k.Path[i])+Vector3.up*.2f,t.at,p,t.collider)){TechniqueHit(p,t.id,.75f,t.at,Vector3.zero);break;}
                    }return true;
                case "slipstream":return true;
                case "lightning-rod":if(Time>=k.Arm&&Time<k.Until)k.Phase=k.HitAt>Time-.15f?"zap":"rod";return true;
                default:return false;
            }
        }
        private void MoreTechniqueFlight(NativePilot p,float speed,float range,bool hawk)
        {
            var k=p.Technique;var from=p.TechniquePosition;var end=Vec(k.From)+Vec(k.Dir)*Math.Min(range,(Time-k.Release)*speed);var delta=end-from;
            if(TechniqueHubCross(from,end)){EndTechnique(p);return;}
            if(TechniqueObstacle(p,from,delta,.12f,(hawk?10:6)*p.TechniqueScale,out var blocked)){k.Point=Vec(blocked);k.Arm=Time;EndTechnique(p);return;}
            var hit=SweepNative(from,delta,.12f,c=>c!=p.Collider&&(c.GetComponent<NativePilotBody>()?.Pilot is NativePilot q?q.Hp>0&&CanHit(p,q)&&!Invulnerable(q):c.GetComponent<NativeMinionBody>()?.Minion is NativeMinion m?m.Hp>0&&CanHit(p,m):true));
            p.TechniquePosition=hit?.Point??end;
            if(hawk)
            {
                var target=MeleeTargets(p).Where(t=>(t.pilot!=null?Enemy(p,t.pilot)&&!Invulnerable(t.pilot):Enemy(p,t.minion))&&NativeTechniqueRules.LineDistance(Vec(t.at),Vec(from),Vec(p.TechniquePosition))<=6&&MeleeVisible(p.TechniquePosition,t.at,p,t.collider)).OrderBy(t=>(t.at-from).sqrMagnitude).ThenBy(t=>t.id).FirstOrDefault();
                if(target.id!=0){k.Point=Vec(p.TechniquePosition);k.Arm=Time;k.To=Vec(target.at);k.Target=target.id;k.Phase="dive";k.Until=Time+.9f;return;}
            }
            if(!hit.HasValue&&(Time-k.Release)*speed<range)return;k.Point=Vec(p.TechniquePosition);
            if(hawk){k.Arm=Time;k.Until=Time+.9f;k.Phase="recovery";return;}
            int id=hit?.Collider.GetComponent<NativePilotBody>()?.Pilot?.Seat.id??hit?.Collider.GetComponent<NativeMinionBody>()?.Minion?.Id??0;
            if(id!=0){k.Target=id;k.To=k.Point;k.HitAt=Time;k.Phase="marked";k.Until=Time+6;TechniqueHit(p,id,6,p.TechniquePosition,Vec(k.Dir));}
            else{k.Until=Time+.2f;k.Phase="recovery";}
        }
        private float MoreTechniqueSpeed(NativePilot p,Vector3 move)
        {
            return (ChannelLive(p,"dragon-breath")?.5f:ChannelLive(p,"soul-siphon")?.6f:1)*(TechniqueRunning(p)?1.35f:1)*TechniqueCurrentSpeed(p.Body.position-Vector3.up*.865f,move);
        }
        private float TechniqueCurrentSpeed(Vector3 feet,Vector3 move)
        {
            foreach(var owner in Pilots){var k=owner.Technique;if(k?.Kind!="slipstream"||!NativeTechniqueRules.Live(k,Time)||NativeTechniqueRules.LineDistance(Vec(feet),k.From,k.To)>1.2f)continue;float dot=V3.Dot(Vec(move.normalized),(k.To-k.From).Normalized);return dot>.1f?1.4f:dot<-.1f?.7f:1;}return 1;
        }
        private bool MoreTechniqueMotion(NativePilot p,ref Vector3 velocity)
        {
            var k=p.Technique;if(k==null||TechniqueBlocked(p)||Time<k.Release)return false;
            if(k.Kind=="bipod"&&NativeTechniqueRules.Live(k,Time)){velocity.x=velocity.z=0;p.MoveVelocity=Vector3.zero;return true;}
            if(NativeTechniqueRules.Rooted(k,Time)){velocity.x=velocity.z=0;p.Knockback=p.MoveVelocity=Vector3.zero;return true;}
            if(!NativeTechniqueRules.Live(k,Time))return false;Vector3 travel;
            switch(k.Kind)
            {
                case "parting-line":if(Time>=k.Release+.28f||k.Phase!="hop")return false;float u=Mathf.Clamp01((Time+1f/60-k.Release)/.28f);travel=(p.TechniqueMoveFrom-Vec(k.Dir)*(4.5f*u)+Vector3.up*(Mathf.Sin(u*Mathf.PI)*.55f)-p.Body.position)*60;break;
                case "shield-surf":if(k.Recast(Time))return false;k.Dir=V3.Forward(p.Input.Yaw);travel=Vec(k.Dir)*11;break;
                case "recoil-burst":travel=-Vec(k.Dir)*12;break;
                case "grave-walk":travel=Vector3.ClampMagnitude(new Vector3(p.Input.X,0,p.Input.Z),1)*7;break;
                default:return false;
            }
            var delta=travel/60;bool wall=false;float allowed=delta.magnitude>1e-5f?ForwardAbilityStep(p.Body.position,delta.normalized,delta.magnitude,out wall):0;
            if(k.Kind=="parting-line"&&wall&&delta.y<0&&root.scene.GetPhysicsScene().CapsuleCast(p.Body.position+Vector3.up*.43f,p.Body.position-Vector3.up*.43f,.42f,delta.normalized,out var floorHit,delta.magnitude+.03f,WorldMask,QueryTriggerInteraction.Ignore)&&floorHit.normal.y>.65f)
            {delta.y=0;allowed=delta.magnitude>1e-5f?ForwardAbilityStep(p.Body.position,delta.normalized,delta.magnitude,out wall):0;}
            if(!TechniqueDestination(p,p.Body.position+delta.normalized*allowed)){allowed=0;wall=true;}
            var result=delta.normalized*allowed*60;velocity.x=result.x;velocity.z=result.z;
            if(k.Kind=="recoil-burst"||k.Kind=="parting-line"){velocity.y=result.y;p.Body.useGravity=false;p.Grounded=false;}
            p.Knockback=p.MoveVelocity=Vector3.zero;
            if(wall&&k.Kind=="shield-surf")FinishMoreTechnique(p);if(wall&&k.Kind=="parting-line")k.Phase="line";return true;
        }
    }
}
