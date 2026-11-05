using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativePilot
    {
        internal HashSet<int> ExpansionCrateUsers=new HashSet<int>();internal bool ExpansionCrouched;internal float ExpansionKnifeUntil,ExpansionHookUntil,ExpansionBlinkUntil,ExpansionSkyUntil,ExpansionRainAt=-100;internal bool ExpansionSkyAir,ExpansionBlinkSecond,ExpansionDiving;internal Vector3 ExpansionStepFrom;internal NativeMeleeAction ExpansionStep;
    }
    public sealed partial class NativeArena
    {
        private sealed class ExpansionArea {public NativePilot Owner;public string Kind;public Vector3 Point,To;public float At,Until,Next,Damage,Radius;public int Target;}
        private readonly List<ExpansionArea> expansionAreas=new List<ExpansionArea>();
        private void ExpansionAreaAdd(NativePilot p,string kind,Vector3 at,float delay,float duration,float damage,float radius,Vector3? to=null,int target=0)
        {if(expansionAreas.Count>=128)expansionAreas.RemoveAt(0);expansionAreas.Add(new ExpansionArea{Owner=p,Kind=kind,Point=at,To=to??at,At=Time+delay,Until=Time+delay+duration,Next=Time+delay,Damage=damage,Radius=radius,Target=target});}
        private void ExpansionAreaHit(ExpansionArea a)
        {
            foreach(var t in MeleeTargets(a.Owner).ToArray())
            {
                bool inside=a.Kind=="afterimage-cut"?NativeTechniqueRules.LineDistance(Vec(t.at),Vec(a.Point),Vec(a.To))<=a.Radius:Vector3.Distance(t.at,a.Point)<=a.Radius;
                if(a.Kind=="melee-echo")inside=inside&&Vector3.Angle(a.To-a.Point,t.at-a.Point)<=60;
                if(!inside||!MeleeVisible(a.Point,t.at,a.Owner,t.collider))continue;
                if(t.pilot!=null)Damage(t.pilot,a.Owner,a.Damage,Vector3.zero,0,false,direct:false,primary:false,damageKind:"expansion-copy");else HitMinion(t.minion,a.Owner,a.Damage,Vector3.zero,0,t.at,"expansion-copy");
            }
        }
        private void StepAbilityExpansion()
        {
            foreach(var a in expansionAreas.ToArray())
            {
                if(Time<a.At)continue;if(Time>=a.Next){a.Next=Time+.25f;ExpansionAreaHit(a);}if(Time>=a.Until)expansionAreas.Remove(a);
            }
            foreach(var p in Pilots.Where(p=>p.Hp>0))
            {
                var k=p.Technique;
                if(ChannelLive(p,"smoke-veil")&&AbilityChip(p,"ab-choking-smoke")&&Time>=p.TechniqueNext)
                {p.TechniqueNext=Time+.25f;foreach(var t in MeleeTargets(p).ToArray())if(Vector3.Distance(t.at,Vec(k.Point)+Vector3.up*.8f)<=3&&MeleeVisible(Vec(k.Point)+Vector3.up*.2f,t.at,p,t.collider)){PriorityFieldHit(p,t.id,.5f,t.at);if(t.pilot!=null)Slow(t.pilot,.8f,.3f);else SlowMinion(t.minion,.8f,.3f);}}
                if(ChannelLive(p,"anchor-throw")&&k.Phase=="stuck"&&Time<k.Arm+.4f&&AbilityChip(p,"ab-deep-anchor"))foreach(var t in MeleeTargets(p).ToArray())if(Vector3.Distance(t.at,Vec(k.Point))<=4)MobilityPush(p,t.id,(Vec(k.Point)-t.at).normalized,8f/60);
                if(ChannelLive(p,"sonar-dart")&&AbilityChip(p,"ab-deep-scan")&&Time>=k.Arm&&Time<k.Until&&Time>=p.TechniqueNext)
                {p.TechniqueNext=Time+1.2f;foreach(var t in MeleeTargets(p))if(Vector3.Distance(t.at,Vec(k.Point))<=16)PriorityMark(p,t.id,"sonar-dart",.85f);}
                if(p.ExpansionSkyUntil>Time){if(!p.Grounded)p.ExpansionSkyAir=true;else if(p.ExpansionSkyAir){ExpansionAreaAdd(p,"sky-splitter",p.Body.position,0,0,10*(float)p.Stats["damage"],3);p.ExpansionSkyUntil=0;}}
                if(p.ExpansionStep!=null&&(p.Special!=p.ExpansionStep||p.ExpansionStep.Phase!="active"))
                {ExpansionAreaAdd(p,"afterimage-cut",p.ExpansionStepFrom,.4f,0,12*(float)p.Stats["damage"],.5f,p.Body.position);p.ExpansionStep=null;}
                if(p.BowRainAt>-90&&p.ExpansionRainAt!=p.BowRainAt&&Time>=p.BowRainAt+1.8f&&AbilityChip(p,"ab-burning-rain"))
                {p.ExpansionRainAt=p.BowRainAt;ExpansionAreaAdd(p,"burning-rain",p.BowRainTarget+Vector3.up*.6f,0,2.75f,(float)p.Stats["damage"],NativeBowRules.RainRadius);}
                if(p.ExpansionDiving)
                {if(p.Grounded){p.ExpansionDiving=false;ExpansionAreaAdd(p,"phoenix-dive",p.Body.position,0,0,16*p.TechniqueScale,3);}else{var v=p.Body.linearVelocity;v.y=-24;p.Body.linearVelocity=v;}}
            }
        }
        private void ExpansionMeleeRelease(NativePilot p,NativeMeleeAction a)
        {
            if(a.Fx=="shadow-step"&&AbilityChip(p,"ab-knife-in-the-dark"))p.ExpansionKnifeUntil=Time+1;
            if(a.Fx=="flash-step"&&AbilityChip(p,"ab-afterimage-cut")){p.ExpansionStep=a;p.ExpansionStepFrom=p.Body.position;}
            if(a.Fx=="rising-dragon"&&AbilityChip(p,"ab-sky-splitter")){p.ExpansionSkyUntil=Time+2;p.ExpansionSkyAir=true;p.Grounded=false;var v=p.Body.linearVelocity;v.y=7.4f*(float)Math.Sqrt(1.3);p.Body.linearVelocity=v;}
        }
        private void TwinHawks(NativePilot p)
        {
            for(int i=0;i<2&&ProjectileCount<100;i++){var dir=Quaternion.Euler(0,i==0?-4:4,0)*Vec(p.Technique.Dir);Bullets.Add(new NativeBullet{Id=nextBullet++,Attack=nextAttack++,Owner=p.Seat.id,Original=p.Seat.id,Spec="bow",Detail="spirit-hawk",Born=Time,Position=Vec(p.Technique.From),Velocity=dir*20,BaseVelocity=dir*20,FixedSpeed=20,Damage=8*p.TechniqueScale,Radius=.2f,Life=2.4f,Travel=48,Homing=4,Direct=false,TechniqueNoHead=true});}
        }
        private void ExpansionCluster(NativeAbilityShot s)
        {
            if(s.Definition.id!="frag-grenade"||!AbilityChip(s.Owner,"ab-cluster-frag"))return;
            for(int i=0;i<3;i++){var at=s.Position+Quaternion.Euler(0,i*120,0)*Vector3.forward*3;if(!SceneLinecast(s.Position,at,WorldMask)&&!HubCross(s.Position,at))ExpansionAreaAdd(s.Owner,"cluster-frag",at,.3f,0,15*s.Damage/60,2);}
        }
        private void ExpansionHeavyChains(NativeAbilityShot s,int target,Vector3 at)
        {
            if(s.Definition.id!="shadow-shackles"||!AbilityChip(s.Owner,"ab-heavy-chains"))return;
            var next=MeleeTargets(s.Owner).Where(t=>t.id!=target&&Vector3.Distance(t.at,at)<=5&&MeleeVisible(at,t.at,s.Owner,t.collider)).OrderBy(t=>(t.at-at).sqrMagnitude).FirstOrDefault();
            if(next.collider==null)return;if(next.pilot!=null)Slow(next.pilot,(float)s.Definition.speedFactor,(float)s.Definition.slowFor*.5f);else SlowMinion(next.minion,(float)s.Definition.speedFactor,(float)s.Definition.slowFor*.5f);
        }
    }
}
