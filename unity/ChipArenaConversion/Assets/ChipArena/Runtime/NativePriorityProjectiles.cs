using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private bool ReleasePriorityProjectile(NativePilot p)
        {
            var k=p.Technique;
            switch(k.Kind)
            {
                case "lodestone":k.Phase="draw";k.Arm=0;return true;
                case "blade-drag":k.Phase="drag";k.Dir=V3.Forward(p.Input.Yaw);k.Path=new[]{Vec(p.Body.position-Vector3.up*.865f)};p.TechniqueNext=Time+NativeTechniques.PathStep;p.TechniqueHealth=Time+.25f;return true;
                case "breacher-round":BreacherFire(p);return true;
                case "bramble-arrow":case "tether-arrow":case "bubble-trap":k.Phase="flight";k.Point=k.From;k.Until=k.Release+(k.Kind=="bubble-trap"?20f/18:40f/45);return true;
                default:return false;
            }
        }
        private bool StepPriorityProjectile(NativePilot p)
        {
            var k=p.Technique;
            switch(k.Kind)
            {
                case "breacher-round":return true;
                case "lodestone":
                    if(k.Phase!="draw")return true;if(Time>=k.Until){FinishPriorityProjectile(p);return true;}k.Dir=V3.Forward(p.Input.Yaw);
                    return true;
                case "blade-drag":
                    if(k.Phase=="drag")
                    {
                        k.Dir=Vec(Vector3.RotateTowards(Vec(k.Dir),Vec(V3.Forward(p.Input.Yaw)),Mathf.PI/120,0));p.Yaw=Mathf.Atan2(k.Dir.x,k.Dir.z)*Mathf.Rad2Deg;
                        if(Time>=p.TechniqueNext&&k.Path.Length<32){k.Path=k.Path.Concat(new[]{Vec(p.Body.position-Vector3.up*.865f)}).ToArray();p.TechniqueNext+=NativeTechniques.PathStep;}
                        if(Time>=k.Until)FinishPriorityProjectile(p);
                    }
                    if(AbilityChip(p,"ab-molten-trail")&&Time<k.Until&&Time>=p.TechniqueHealth)
                    {
                        p.TechniqueHealth=Time+.25f;foreach(var t in MeleeTargets(p).ToArray())if(k.Path.Select((v,i)=>new{v,i}).Any(x=>Time<k.Release+x.i*NativeTechniques.PathStep+3&&Math.Abs(t.at.y-.865f-x.v.y)<.5f&&new Vector2(t.at.x-x.v.x,t.at.z-x.v.z).magnitude<=.5f)&&MeleeVisible(p.Body.position,t.at,p,t.collider))PriorityFieldHit(p,t.id,1,t.at);
                    }
                    return true;
                case "bramble-arrow":case "tether-arrow":case "bubble-trap":
                    if(k.Phase=="flight")PriorityFlight(p);
                    if(k.Phase=="thorns"&&Time<k.Until&&Time>=p.TechniqueNext)
                    {
                        p.TechniqueNext=Time+.25f;foreach(var t in MeleeTargets(p).ToArray())if(HedgeTouches(k,t.at)&&MeleeVisible(Vec(k.Point)+Vector3.up*.6f,t.at,p,t.collider))
                        {if(t.pilot!=null)Slow(t.pilot,.6f,.3f);else SlowMinion(t.minion,.6f,.3f);PriorityFieldHit(p,t.id,.75f,t.at);}
                    }
                    if(k.Phase=="bubble"&&(Time>=k.Until||MeleeTarget(p,k.Target).collider==null))PopBubble(p);
                    return true;
                default:return false;
            }
        }
        private bool LodestoneFraction(NativePilot p,Vector3 from,Vector3 delta,out float u)
        {
            u=0;var k=p.Technique;if(!ChannelLive(p,"lodestone")||k.Phase!="draw"||k.Arm>=8)return false;
            if((from-p.Body.position).magnitude>7&&!NativeTechniqueRules.SegmentSphere(Vec(from),Vec(delta),Vec(p.Body.position),7,out u))return false;
            var at=from+delta*u;return Vector3.Dot((at-p.Body.position).normalized,Vec(k.Dir))>.5f&&!SceneLinecast(p.Body.position,at,WorldMask)&&!HubCross(p.Body.position,at);
        }
        private bool TryLodestone(NativePilot owner,Vector3 from,Vector3 to,out Vector3 point)
        {
            point=from;Vector3 delta=to-from;
            if(root.scene.GetPhysicsScene().Raycast(from,delta.normalized,out var wall,delta.magnitude,WorldMask,QueryTriggerInteraction.Ignore))delta=wall.point-from;
            NativePilot caught=null;float nearest=2;
            foreach(var p in Pilots)
            {
                var k=p.Technique;if(p.Hp<=0||!ChannelLive(p,"lodestone")||k.Phase!="draw"||k.Arm>=8||!CanHit(owner,p))continue;
                if(!LodestoneFraction(p,from,delta,out float u)||u>=nearest)continue;
                Vector3 at=from+delta*u;
                caught=p;nearest=u;point=at;
            }
            if(caught==null)return false;caught.Technique.Arm++;caught.Technique.To=Vec(point);caught.Technique.HitAt=Time;return true;
        }
        private void FinishPriorityProjectile(NativePilot p)
        {
            if(TechniqueBlocked(p)){EndTechnique(p);return;}var k=p.Technique;if(k.Kind=="lodestone"&&k.Phase=="draw")
            {
                k.Phase="release";k.RecastAt=Time;k.Until=Time+.3f;
                foreach(var t in MeleeTargets(p).ToArray())if(Vector3.Distance(t.at,p.Body.position)<=6&&Vector3.Angle(Vec(k.Dir),t.at-p.Body.position)<=45&&MeleeVisible(p.Body.position,t.at,p,t.collider))TechniqueHit(p,t.id,Math.Min(24,k.Arm*3),t.at,Vec(k.Dir));
            }
            else if(k.Kind=="blade-drag"&&k.Phase=="drag")
            {
                k.Arm=Mathf.Clamp01((Time-k.Release)/2);k.Phase="rise";k.RecastAt=Time;k.Until=Time+(AbilityChip(p,"ab-molten-trail")?3:.3f);k.RecoverUntil=Time+.3f;p.Ready=k.RecoverUntil;
                foreach(var t in MeleeTargets(p).ToArray())if(Vector3.Distance(t.at,p.Body.position)<=2.6f&&Vector3.Angle(Vec(k.Dir),t.at-p.Body.position)<=45&&MeleeVisible(p.Body.position,t.at,p,t.collider)&&TechniqueHit(p,t.id,8+12*k.Arm,t.at,Vec(k.Dir))>0){k.HitAt=Time;TechniqueShove(p,t.id,(Vec(k.Dir)+Vector3.up*.4f).normalized,10);}
            }
        }
        private void BreacherFire(NativePilot p)
        {
            var k=p.Technique;Vector3 start=Vec(k.From),dir=Vec(k.Dir).normalized;float limit=80,budget=1.2f,cursor=0;var path=new List<V3>();
            // Each collider contributes its actual entry/exit interval, including overlapping walls.
            var hits=new RaycastHit[128];int count=root.scene.GetPhysicsScene().Raycast(start,dir,hits,80,WorldMask,QueryTriggerInteraction.Ignore);
            foreach(var hit in hits.Take(count).OrderBy(h=>h.distance))
            {
                if(hit.distance>=limit)break;var c=hit.collider;float far=hit.distance+c.bounds.size.magnitude+.1f;
                if(!c.Raycast(new Ray(start+dir*far,-dir),out var exit,far-hit.distance+.1f)){limit=hit.distance;break;}
                float end=far-exit.distance,thickness=Math.Max(0,end-hit.distance);
                path.Add(Vec(hit.point));if(thickness>budget){limit=hit.distance+budget;path.Add(Vec(start+dir*limit));break;}
                budget-=thickness;cursor=Math.Max(cursor,end);path.Add(Vec(start+dir*end));if(path.Count>=30){limit=cursor;break;}
            }
            k.To=Vec(start+dir*limit);k.Path=path.ToArray();k.Phase="fire";
            if(TechniqueHubCross(start,Vec(k.To))){EndTechnique(p);return;}
            foreach(var t in MeleeTargets(p).ToArray())
            {
                float along=Vector3.Dot(t.at-start,dir);if(along<0||along>limit||NativeTechniqueRules.LineDistance(Vec(t.collider.ClosestPoint(start+dir*along)),Vec(start),k.To)>.12f)continue;
                if(TechniqueHit(p,t.id,22,t.at,dir)>0){if(k.HitAt<-50)k.HitAt=Time;if(AbilityChip(p,"ab-hollow-point")){if(t.pilot!=null)Slow(t.pilot,.7f,2);else SlowMinion(t.minion,.7f,2);}}
            }
        }
        private bool HedgeTouches(NativeTechnique k,Vector3 at)
        {
            var flat=at-Vec(k.Point);var dir=Vec(k.Dir);dir.y=0;dir.Normalize();
            return flat.y>=0&&flat.y<=1.865f&&Math.Abs(Vector3.Dot(flat,dir))<=.5f&&Math.Abs(Vector3.Dot(flat,Vector3.Cross(Vector3.up,dir)))<=2.5f;
        }
        private void PriorityFlight(NativePilot p)
        {
            var k=p.Technique;bool bubble=k.Kind=="bubble-trap";float speed=bubble?18:45,range=bubble?20:40,age=Math.Max(0,Time-k.Release);
            Vector3 from=Vec(k.Point),end=Vec(k.From)+Vec(k.Dir)*Math.Min(range,age*speed);if(!bubble)end.y-=9*age*age;var delta=end-from;
            if(TechniqueHubCross(from,end)){EndTechnique(p);return;}
            if(TryLodestone(p,from,end,out var caught)){k.Point=Vec(caught);EndTechnique(p);return;}
            if(TechniqueObstacle(p,from,delta,.16f,k.Kind=="tether-arrow"?5*p.TechniqueScale:0,out var obstacle)){k.Point=Vec(obstacle);if(k.Kind=="bramble-arrow")PlantBramble(p);else EndTechnique(p);return;}
            var hit=SweepNative(from,delta,.16f,c=>c!=p.Collider&&(c.GetComponent<NativePilotBody>()?.Pilot is NativePilot q?q.Hp>0&&CanHit(p,q):c.GetComponent<NativeMinionBody>()?.Minion is NativeMinion m?m.Hp>0&&CanHit(p,m):true));
            k.Point=Vec(hit?.Point??end);if(!hit.HasValue&&age*speed<range)return;
            if(k.Kind=="bramble-arrow"){PlantBramble(p);return;}
            var pilot=hit?.Collider.GetComponent<NativePilotBody>()?.Pilot;var minion=hit?.Collider.GetComponent<NativeMinionBody>()?.Minion;k.Target=pilot?.Seat.id??minion?.Id??0;
            if(k.Target==0||pilot!=null&&Invulnerable(pilot)){EndTechnique(p);return;}
            bool boss=pilot?.Seat.AuthorityBoss==true||minion!=null&&IsZoneBoss(minion);k.HitAt=Time;
            if(!bubble)
            {
                if(TechniqueHit(p,k.Target,5,Vec(k.Point),Vec(k.Dir))<=0||boss){EndTechnique(p);return;}
                k.Phase="tether";k.Point=Vec((pilot?.Body??minion.Body).position-Vector3.up*.865f);k.Until=Time+3;PriorityMark(p,k.Target,k.Kind,3);return;
            }
            if(boss){if(pilot!=null)Slow(pilot,.6f,1.4f);else SlowMinion(minion,.6f,1.4f);EndTechnique(p);return;}
            k.Phase="bubble";k.Until=Time+(AbilityChip(p,"ab-double-bubble")?1.8f:1.4f);k.To=Vec((pilot?.Body??minion.Body).position);PriorityMark(p,k.Target,k.Kind,k.Until-Time);
            if(pilot!=null){CancelAbilities(pilot);CancelSpecial(pilot);EndTechnique(pilot);pilot.RollEnd=Time;pilot.MoveVelocity=pilot.Knockback=Vector3.zero;}
            else {minion.StrikeAt=-1;minion.NextAttack=Math.Max(minion.NextAttack,k.Until);if(minion.Pve!=null){minion.Pve.StaggerAt=k.Until;minion.Pve.Struck=true;}}
        }
        private void PriorityFieldHit(NativePilot p,int id,float damage,Vector3 at)
        {
            var t=MeleeTarget(p,id);if(t.pilot!=null)Damage(t.pilot,p,damage*p.TechniqueScale,Vector3.zero,0,false,direct:false,primary:false,damageKind:"field");
            else if(t.minion!=null)HitMinion(t.minion,p,damage*p.TechniqueScale,Vector3.zero,0,at,"field");
        }
        private void PlantBramble(NativePilot p){p.TechniqueNext=Time+.25f;var k=p.Technique;k.Point=Vec(GroundPoint(Vec(k.Point)));k.Arm=Time;k.Phase="thorns";k.Until=Time+5;}
        private bool BubbleHeld(int id)=>Pilots.Any(p=>p.Hp>0&&p.Technique?.Kind=="bubble-trap"&&p.Technique.Phase=="bubble"&&p.Technique.Target==id&&NativeTechniqueRules.Live(p.Technique,Time));
        private void PopVictimBubble(int id){foreach(var p in Pilots.Where(p=>p.Technique?.Kind=="bubble-trap"&&p.Technique.Phase=="bubble"&&p.Technique.Target==id).ToArray())PopBubble(p);}
        private void PopBubble(NativePilot owner)
        {
            var k=owner.Technique;var t=MeleeTarget(owner,k.Target);k.Phase="pop";k.RecastAt=Time;k.Until=Math.Max(k.Release,Time);
            if(t.pilot!=null)t.pilot.TechniqueMarks=t.pilot.TechniqueMarks.Where(m=>m.Kind!="bubble-trap"||m.By!=owner.Seat.id).ToArray();
            if(t.minion!=null){t.minion.TechniqueMarks=t.minion.TechniqueMarks.Where(m=>m.Kind!="bubble-trap"||m.By!=owner.Seat.id).ToArray();if(t.minion.Pve!=null)t.minion.Pve.StaggerAt=Math.Min(t.minion.Pve.StaggerAt,Time);}
            if(AbilityChip(owner,"ab-double-bubble"))TechniqueBurst(owner,t.collider!=null?t.at:Vec(k.Point),2,12,0);
        }
        private void StepPriorityProjectileConstraints()
        {
            foreach(var p in Pilots)
            {
                var k=p.Technique;if(k==null||!NativeTechniqueRules.Live(k,Time)||k.Phase!="bubble"&&k.Phase!="tether")continue;
                var t=MeleeTarget(p,k.Target);if(t.collider==null){EndTechnique(p);continue;}var body=t.pilot?.Body??t.minion.Body;
                if(k.Phase=="tether")
                {
                    if(t.pilot!=null&&Time<t.pilot.RollEnd){k.RecastAt=Time;EndTechnique(p);t.pilot.TechniqueMarks=t.pilot.TechniqueMarks.Where(m=>m.Kind!="tether-arrow"||m.By!=p.Seat.id).ToArray();TechniqueHit(p,t.id,AbilityChip(p,"ab-short-leash")?12:6,t.at,Vector3.zero);continue;}
                    var delta=body.position-(Vec(k.Point)+Vector3.up*.865f);float leash=AbilityChip(p,"ab-short-leash")?3.5f:5;
                    if(delta.magnitude>leash)MobilityPush(p,t.id,-delta.normalized,delta.magnitude-leash);
                }
                else
                {
                    Vector3 wanted=Vec(k.To)+Vector3.up*Mathf.Clamp01((Time-k.HitAt)/.2f);var delta=wanted-body.position;
                    MobilityPush(p,t.id,delta.normalized,delta.magnitude);if(!body.isKinematic)body.linearVelocity=Vector3.zero;
                    k.Point=Vec(body.position);if(t.pilot!=null)t.pilot.MoveVelocity=t.pilot.Knockback=Vector3.zero;
                    else if(t.minion.Pve!=null)t.minion.Pve.External=Vector3.zero;
                }
            }
        }
    }
}
