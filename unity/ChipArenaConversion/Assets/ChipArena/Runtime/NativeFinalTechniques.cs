using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private bool FinalFlakContact(NativePilot shooter,Vector3 from,Vector3 delta,out Vector3 point)
        {
            point=from+delta;float nearest=1.001f;NativePilot best=null;
            var solid=SweepNative(from,delta,0,c=>c!=shooter?.Collider&&(c.GetComponent<NativePilotBody>()?.Pilot is NativePilot q?q.Hp>0&&CanHit(shooter,q):c.GetComponent<NativeMinionBody>()?.Minion is NativeMinion m?m.Hp>0&&CanHit(shooter,m):true));
            if(solid.HasValue)nearest=solid.Value.Distance/Math.Max(.00001f,delta.magnitude);
            foreach(var p in Pilots){var k=p.Technique;if(!ChannelLive(p,"flak-burst")||k.Phase!="cloud"||!CanHit(shooter,p)||!NativeTechniqueRules.SegmentSphere(Vec(from),Vec(delta),k.Point,2.5f,out float u)||u>=nearest)continue;var contact=from+delta*u;if(SceneLinecast(Vec(k.Point),contact,WorldMask))continue;best=p;nearest=u;}
            if(best==null)return false;point=from+delta*nearest;best.Technique.To=Vec(point);best.Technique.HitAt=Time;return true;
        }
        private bool FinalTechniqueLock(NativePilot p)=>ChannelLive(p,"chain-hook")||NativeCombatMarks.Has(p.TechniqueMarks,"dread",Time);
        private NativeMinion ExpansionDecoy(NativePilot p,string kind,float hp,float life,Vector3 at)
        {
            int first=nextMinion;Summon(p,1,kind,at,life,true);var m=Minions.Find(m=>m.Id>=first&&m.Owner==p&&m.Kind==kind);
            if(m!=null){Physics.IgnoreCollision(m.Collider,p.Collider,true);m.Hp=m.MaxHp=hp;m.Body.position=at;m.Speed=0;m.NextAttack=Time+life;m.StrikeAt=-1;}return m;
        }
        private readonly Collider[] cratePlacementHits=new Collider[8];
        private bool AmmoCratePoint(NativePilot p,in InputFrame aim,out Vector3 point)
        {
            var feet=p.Body.position-Vector3.up*.865f;point=feet+Vec(V3.Forward(aim.Yaw))*1.4f;var scene=root.scene.GetPhysicsScene();
            if(!scene.Raycast(point+Vector3.up*.5f,Vector3.down,out var floor,2,WorldMask,QueryTriggerInteraction.Ignore)||floor.normal.y<.5f)return false;
            point=floor.point;if(!TechniqueDestination(p,point+Vector3.up*.865f)||SceneLinecast(p.Body.position,point+Vector3.up*.4f,WorldMask))return false;
            // The drawn crate rotates randomly. Its horizontal circumradius covers every orientation.
            float horizontal=Mathf.Sqrt(NativeTechniqueFx.CrateHalfX*NativeTechniqueFx.CrateHalfX+NativeTechniqueFx.CrateHalfZ*NativeTechniqueFx.CrateHalfZ);
            return scene.OverlapBox(point+Vector3.up*(NativeTechniqueFx.CrateHalfY+.02f),new Vector3(horizontal,NativeTechniqueFx.CrateHalfY-.01f,horizontal),cratePlacementHits,Quaternion.identity,WorldMask,QueryTriggerInteraction.Ignore)==0;
        }
        private bool ReleaseFinalTechnique(NativePilot p)
        {
            var k=p.Technique;var feet=p.Body.position-Vector3.up*.865f;
            switch(k.Kind)
            {
                case "mirror-double":case "shadow-puppet":
                    bool mirror=k.Kind=="mirror-double";var m=ExpansionDecoy(p,k.Kind,mirror?1:30,mirror?2.5f:4,p.Body.position);
                    if(m==null){EndTechnique(p);return true;}k.Target=m.Id;k.From=k.Point=Vec(feet);k.To=Vec(m.Body.position);k.Dir=V3.Forward(p.Input.Yaw);k.Phase=mirror?"run":"puppet";k.Arm=mirror?Time+1:1;if(!mirror)k.Dir=GunRules.Direction(p.Input.Yaw,p.Input.Pitch);return true;
                case "chain-hook":k.Phase="flight";k.Point=k.From;k.Until=Time+(AbilityChip(p,"ab-barbed-chain")?16:12)/40f;k.RecoverUntil=k.Until;return true;
                case "flash-round":k.Phase="shot";k.Point=k.From;k.Until=Time+1;return true;
                case "slide-shot":
                    var dir=new Vector3(p.Input.X,0,p.Input.Z);if(dir.sqrMagnitude<.01f)dir=Vec(V3.Forward(p.Input.Yaw));dir.Normalize();
                    float length=TechniqueBodyStep(p,dir*6,true,out _,out _);k.From=Vec(feet);k.Dir=Vec(dir);k.To=k.From+Vec(dir*length);k.Phase="slide";k.Until=Time+.75f;return true;
                case "ammo-crate":if(!AmmoCratePoint(p,in p.Input,out var crate)){EndTechnique(p);return true;}p.ExpansionCrateUsers.Clear();k.Phase="open";k.Point=Vec(crate);k.Arm=1;p.TechniqueHealth=40;return true;
                case "flak-burst":k.Phase="fire";k.Point=k.From;k.To=Vec(AimedAbilityPoint(p,40));k.Arm=Time+Vector3.Distance(Vec(k.From),Vec(k.To))/50;k.Until=k.Arm+3;p.TechniqueNext=k.Arm;return true;
                case "flame-cloak":k.Phase="cloak";k.Arm=Time;return true;
                case "dread":
                    k.Phase="scream";k.HitAt=Time;k.Point=Vec(feet);k.Dir=V3.Forward(p.Input.Yaw);
                    foreach(var t in MeleeTargets(p).ToArray())if(Vector3.Distance(t.at,p.Body.position)<=6&&Vector3.Angle(Vec(k.Dir),t.at-p.Body.position)<=45&&MeleeVisible(p.Body.position,t.at,p,t.collider))
                    {
                        if(t.pilot?.Seat.AuthorityBoss==true||t.minion!=null&&IsZoneBoss(t.minion)){if(t.pilot!=null)Slow(t.pilot,.6f,.9f);else SlowMinion(t.minion,.6f,.9f);continue;}
                        if(t.pilot!=null&&Invulnerable(t.pilot))continue;PriorityMark(p,t.id,"dread",.9f);
                        if(t.pilot!=null){CancelAbilities(t.pilot);CancelSpecial(t.pilot);EndTechnique(t.pilot);StunMelee(t.pilot,.9f);}else{t.minion.NextAttack=Time+.9f;t.minion.StrikeAt=-1;if(t.minion.Pve!=null)t.minion.Pve.StaggerAt=Time+.9f;}
                    }return true;
                case "magnetize":
                    var targets=MeleeTargets(p).Where(t=>t.pilot?.Seat.AuthorityBoss!=true&&(t.minion==null||!IsZoneBoss(t.minion))&&Vector3.Distance(t.at,p.Body.position)<=16&&MeleeVisible(p.Body.position,t.at,p,t.collider)).OrderByDescending(t=>Vector3.Dot((t.at-p.Body.position).normalized,Vec(k.Dir))).Take(2).ToArray();
                    foreach(var t in targets)PriorityMark(p,t.id,"magnetize",1.5f);k.Target=targets.FirstOrDefault().id;k.Point=targets.Length==2?Vec((targets[0].at+targets[1].at)*.5f):Vec(AimedAbilityPoint(p,16)+Vector3.up*.865f);k.Arm=Time+.5f;k.Phase="pull";k.Path=targets.Select(t=>Vec(t.at)).ToArray();return true;
                default:return false;
            }
        }
        private bool StepFinalTechnique(NativePilot p)
        {
            var k=p.Technique;
            switch(k.Kind)
            {
                case "mirror-double":case "shadow-puppet":
                    if(!NativeTechniqueRules.Live(k,Time))return true;var m=Minions.Find(m=>m.Id==k.Target&&m.Hp>0);
                    if(m==null){if(k.Phase!="pop"){k.Phase="pop";if(k.Kind=="shadow-puppet")k.Arm=0;else k.HitAt=Time;k.Until=Time+.3f;}return true;}
                    if(k.Kind=="shadow-puppet"){k.Arm=Mathf.Clamp01(m.Hp/Math.Max(1,m.MaxHp));k.Point=Vec(m.Body.position-Vector3.up*.865f);k.Dir=Vec(PuppetAim(p,m,Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch))));}else k.To=Vec(m.Body.position);return true;
                case "chain-hook":case "flash-round":
                    if(k.Phase=="flight"||k.Phase=="shot")FinalTechniqueFlight(p);
                    if(k.Phase=="yank"&&Time<k.Until)
                    {
                        var t=MeleeTarget(p,k.Target);if(t.collider==null){EndTechnique(p);return true;}
                        bool heavy=t.pilot?.Seat.AuthorityBoss==true||t.minion!=null&&(IsZoneBoss(t.minion)||t.minion.Pve?.Stats.id=="hulk");
                        Vector3 goal=heavy?t.at-(t.at-p.Body.position).normalized*2:Vec(k.To),from=heavy?p.Body.position:t.at;
                        var delta=goal-from;float step=Math.Min(delta.magnitude,delta.magnitude/Math.Max(1f/60,k.Until-Time)/60);
                        if(heavy){float allowed=ForwardAbilityStep(from,delta.normalized,step,out _);if(TechniqueDestination(p,from+delta.normalized*allowed))p.Body.position=from+delta.normalized*allowed;}
                        else MobilityPush(p,t.id,delta.normalized,step);
                    }return true;
                case "slide-shot":if(k.Phase=="slide"&&Time>=k.Release+.55f){k.Phase="rise";k.RecoverUntil=k.Until;}return true;
                case "ammo-crate":
                    if(!NativeTechniqueRules.Live(k,Time))return true;
                    foreach(var q in Pilots.Where(q=>q.Hp>0&&q.Seat.connected&&(q==p||!Enemy(p,q))&&Vector3.Distance(q.Body.position,Vec(k.Point)+Vector3.up*.865f)<=1.8f&&MeleeVisible(Vec(k.Point)+Vector3.up*.5f,q.Body.position,p,q.Collider)))
                    {
                        if(!p.ExpansionCrateUsers.Add(q.Seat.id))continue;
                        q.TechniqueMarks=NativeCombatMarks.Put(q.TechniqueMarks,"ammo-crate-used",p.Seat.id,Time,k.Until);MarkTechnique(q,"ammo-crate-speed",p,5);q.Ammo=Magazine(q);q.ReloadEnd=0;k.To=Vec(q.Body.position);k.HitAt=Time;
                    }return true;
                case "flak-burst":
                    if(k.Phase=="fire")
                    {float u=Mathf.Clamp01((Time-k.Release)/Math.Max(.001f,k.Arm-k.Release));var end=Vector3.Lerp(Vec(k.From),Vec(k.To),u);var hit=SweepNative(Vec(k.Point),end-Vec(k.Point),.12f,c=>c!=p.Collider);k.Point=Vec(hit?.Point??end);if(hit.HasValue||Time>=k.Arm){k.Phase="cloud";k.Arm=Time;k.Until=Time+3;p.TechniqueNext=Time;}}
                    if(k.Phase=="cloud"&&Time<k.Until&&Time>=p.TechniqueNext){p.TechniqueNext=Time+.25f;foreach(var t in MeleeTargets(p).ToArray())if(Vector3.Distance(t.at,Vec(k.Point))<=2.5f&&MeleeVisible(Vec(k.Point),t.at,p,t.collider)){PriorityFieldHit(p,t.id,.5f,t.at);if(t.pilot!=null)Slow(t.pilot,.9f,.3f);else SlowMinion(t.minion,.9f,.3f);}}return true;
                case "magnetize":
                    if(k.Phase!="pull"||Time<k.Arm||Time>=k.Until)return true;
                    var targets=MeleeTargets(p).Where(t=>(t.pilot?.TechniqueMarks??t.minion?.TechniqueMarks)?.Any(m=>m.Kind=="magnetize"&&m.By==p.Seat.id&&m.Until>Time)==true).ToArray();
                    foreach(var t in targets){if(t.pilot!=null&&Time<t.pilot.RollEnd){t.pilot.TechniqueMarks=t.pilot.TechniqueMarks.Where(m=>m.Kind!="magnetize"||m.By!=p.Seat.id).ToArray();continue;}var delta=Vec(k.Point)-t.at;MobilityPush(p,t.id,delta.normalized,Math.Min(delta.magnitude,8f/60));}
                    k.Path=targets.Select(t=>Vec(t.at)).ToArray();if(targets.Length==2&&Vector3.Distance(targets[0].at,targets[1].at)<=1.2f){k.Phase="clash";k.HitAt=Time;foreach(var t in targets)if(TechniqueHit(p,t.id,10,t.at,Vector3.zero)>0){if(t.pilot!=null)StunMelee(t.pilot,.4f);else SlowMinion(t.minion,.01f,.4f);}}return true;
                case "dread":case "flame-cloak":return true;
                default:return false;
            }
        }
        private void FinalTechniqueFlight(NativePilot p)
        {
            var k=p.Technique;bool hook=k.Kind=="chain-hook";float range=hook?(AbilityChip(p,"ab-barbed-chain")?16:12):30,speed=hook?40:60;
            var end=Vec(k.From)+Vec(k.Dir)*Math.Min(range,(Time-k.Release)*speed);var from=Vec(k.Point);var delta=end-from;
            if(TechniqueHubCross(from,end)||TechniqueObstacle(p,from,delta,.14f,0,out _)){EndTechnique(p);return;}
            var hit=SweepNative(from,delta,.14f,c=>c!=p.Collider&&(c.GetComponent<NativePilotBody>()?.Pilot is NativePilot q?q.Hp>0&&CanHit(p,q):c.GetComponent<NativeMinionBody>()?.Minion is NativeMinion m?m.Hp>0&&CanHit(p,m):true));k.Point=Vec(hit?.Point??end);
            if(!hit.HasValue&&(Time-k.Release)*speed<range)return;
            if(!hook){FlashTechnique(p);return;}
            var pilot=hit?.Collider.GetComponent<NativePilotBody>()?.Pilot;var minion=hit?.Collider.GetComponent<NativeMinionBody>()?.Minion;k.Target=pilot?.Seat.id??minion?.Id??0;
            if(k.Target==0||TechniqueHit(p,k.Target,AbilityChip(p,"ab-barbed-chain")?12:6,Vec(k.Point),Vec(k.Dir))<=0){EndTechnique(p);return;}
            k.Phase="yank";k.Arm=k.HitAt=Time;k.Until=k.RecoverUntil=Time+.35f;k.To=Vec(p.Body.position)+V3.Forward(p.Input.Yaw)*2;
            if(AbilityChip(p,"ab-barbed-chain")){if(pilot!=null)Slow(pilot,.7f,1.35f);else SlowMinion(minion,.7f,1.35f);}
        }
        private void FlashTechnique(NativePilot p)
        {
            var k=p.Technique;k.Phase="flash";k.Arm=k.HitAt=Time;k.Until=Time+.3f;float radius=AbilityChip(p,"ab-blinder")?8:6,blind=AbilityChip(p,"ab-blinder")?1.4f:.9f;
            foreach(var q in Pilots.Where(q=>q.Hp>0&&Vector3.Distance(q.Body.position,Vec(k.Point))<=radius&&MeleeVisible(Vec(k.Point),q.Body.position,p,q.Collider)))
                if(Vector3.Angle(Vec(GunRules.Direction(q.Input.Yaw,q.Input.Pitch)),Vec(k.Point)-q.Body.position)<=70){MarkTechnique(q,"flash-round",p,blind);if(q.Seat.bot)MarkTechnique(q,"flash-confused",p,1.2f);}
            foreach(var m in Minions.Where(m=>m.Hp>0&&Vector3.Distance(m.Body.position,Vec(k.Point))<=radius&&MeleeVisible(Vec(k.Point),m.Body.position,p,m.Collider)))
            {m.TechniqueMarks=NativeCombatMarks.Put(m.TechniqueMarks,"flash-confused",p.Seat.id,Time,Time+1.2f);m.Foe=null;m.NextAttack=Time+1.2f;m.StrikeAt=-1;if(m.Pve!=null){m.Pve.Target=null;m.Pve.StaggerAt=Time+1.2f;}}
        }
        private bool FinalTechniqueMotion(NativePilot p,ref Vector3 v)
        {
            var k=p.Technique;
            if(ChannelLive(p,"chain-hook")){v=Vector3.zero;p.MoveVelocity=p.Knockback=Vector3.zero;return true;}
            if(ChannelLive(p,"slide-shot")&&k.Phase=="slide")
            {var desired=Vector3.Lerp(Vec(k.From),Vec(k.To),Mathf.Clamp01((Time+1f/60-k.Release)/.55f))-p.Body.position;desired.y=0;
                float allowed=TechniqueBodyStep(p,desired,true,out bool wall,out _);var step=desired.normalized*allowed;
                if(!TechniqueDestination(p,p.Body.position+step)){step=Vector3.zero;wall=true;}
                // GroundMotion/gravity owns height; correcting to the cast's old Y drove the sweep into the floor.
                v.x=step.x*60;v.z=step.z*60;p.MoveVelocity=p.Knockback=Vector3.zero;if(wall){k.Phase="rise";k.Until=k.RecoverUntil=Time+.2f;}return true;}return false;
        }
        private void FinalTechniqueConstraints()
        {
            foreach(var p in Pilots)
            {bool crouch=p.Hp>0&&ChannelLive(p,"slide-shot")&&p.Technique.Phase=="slide";if(crouch!=p.ExpansionCrouched){float scale=crouch?.55f:1/.55f;p.Collider.height*=scale;p.Collider.center=crouch?Vector3.down*(.865f-p.Collider.height*.5f):Vector3.zero;p.ExpansionCrouched=crouch;}}
            foreach(var p in Pilots)foreach(var mark in p.TechniqueMarks.Where(m=>m.Kind=="dread"&&m.Until>Time))
            {var owner=Pilots.Find(q=>q.Seat.id==mark.By);if(owner==null)continue;var dir=p.Body.position-owner.Body.position;dir.y=0;MobilityPush(owner,p.Seat.id,dir.normalized,3f/60);p.MoveVelocity=p.Knockback=Vector3.zero;}
            foreach(var m in Minions.Where(m=>m.Hp>0).ToArray())foreach(var mark in m.TechniqueMarks.Where(x=>x.Kind=="dread"&&x.Until>Time))
            {var owner=Pilots.Find(q=>q.Seat.id==mark.By);if(owner!=null)MobilityPush(owner,m.Id,(m.Body.position-owner.Body.position).normalized,3f/60);}
        }
        private bool StepExpansionMinion(NativeMinion m)
        {
            if(m.Kind!="mirror-double"&&m.Kind!="shadow-puppet"&&m.Kind!="wild-afterimage"&&m.Kind!="wild-evil-twin")return false;
            if(m.Kind=="wild-evil-twin"){var goal=m.Owner.Body.position+Quaternion.Euler(0,m.Owner.Yaw,0)*Vector3.right*3;var delta=goal-m.Body.position;float allowed=ForwardAbilityStep(m.Body.position,delta.normalized,Math.Min(delta.magnitude,12f/60),out _);if(!HubCross(m.Body.position,m.Body.position+delta.normalized*allowed))m.Body.position+=delta.normalized*allowed;}
            if(m.Kind=="mirror-double")
            {var dir=Vec(V3.Forward(m.Yaw));float allowed=ForwardAbilityStep(m.Body.position,dir,6.8f/60,out bool wall);if(wall){RemoveMinion(m,false);return true;}m.Body.position+=dir*allowed;}
            m.Body.linearVelocity=Vector3.zero;return true;
        }
        private static Vector3 PuppetAim(NativePilot p,NativeMinion m,Vector3 fallback)
        {var dir=p.Input.AimTarget.Length>.01f?Vec(p.Input.AimTarget)-m.Body.position:fallback;return dir.sqrMagnitude>.0001f?dir.normalized:Vector3.forward;}
        private void CopyTechniqueShot(NativePilot p,NativeBullet shot)
        {
            foreach(var m in Minions.Where(m=>m.Owner==p&&m.Kind=="shadow-puppet"&&m.Hp>0).ToArray())
            {
                if(ProjectileCount>=100)break;var dir=PuppetAim(p,m,shot.Velocity);
                Bullets.Add(new NativeBullet{Id=nextBullet++,Attack=nextAttack++,Owner=p.Seat.id,Original=p.Seat.id,Spec=p.Seat.spec,Detail="expansion-copy",Born=Time,Position=m.Body.position+dir*.4f,Velocity=dir*Math.Max(30,shot.Velocity.magnitude),Damage=shot.Damage*.4f,Radius=shot.Radius,Gravity=shot.Gravity,Life=3,Travel=80,Direct=false,TechniqueNoHead=true});m.LastAttack=Time;
                var k=p.Technique;if(k?.Kind=="shadow-puppet"&&k.Target==m.Id){k.Dir=Vec(dir);k.HitAt=Time;k.To=Vec(m.Body.position+dir*80);}
            }
        }
    }
}
