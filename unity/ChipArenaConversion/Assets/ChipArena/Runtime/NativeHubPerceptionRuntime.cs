using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private struct HubSound {public int Id,Source;public float At;public Vector3 Position;public HubNoiseKind Kind;}
        private sealed class HubFootsteps {public Vector3 Position;public bool Grounded;public float WalkAt,AttackAt=-100;}
        private readonly List<HubSound> hubSounds=new List<HubSound>(64);
        private readonly Dictionary<int,HubFootsteps> hubFeet=new Dictionary<int,HubFootsteps>();
        private int hubSoundId;
        private void HubNoise(NativePilot p,Vector3 at,HubNoiseKind kind)
        {
            if(!HubOn||p==null||p.Seat.AuthorityBoss||!p.Seat.connected||HubInside(p.Body.position)||HubInside(at)||HubCross(p.Body.position,at))return;
            if(hubSounds.Count==64)hubSounds.RemoveAt(0);
            hubSounds.Add(new HubSound{Id=++hubSoundId,Source=p.Seat.id,At=Time,Position=at,Kind=kind});
        }
        private void HubEventNoise(EventKind kind,NativePilot p,Vector3 at)
        {
            if(!HubOn)return;
            if(kind==EventKind.Shot)HubNoise(p,at,p?.Weapon.@class=="magic"?HubNoiseKind.Cast:HubNoiseKind.Fire);
            else if(kind==EventKind.Explosion)HubNoise(p,at,HubNoiseKind.Explosion);
            else if(kind==EventKind.AbilityCast)HubNoise(p,at,HubNoiseKind.Cast);
            else if(kind==EventKind.Land)HubNoise(p,at,HubNoiseKind.Land);
        }
        private void StepHubNoise()
        {
            for(int i=hubSounds.Count-1;i>=0;i--)if(Time-hubSounds[i].At>HubPerception.NoiseLifetime)hubSounds.RemoveAt(i);
            foreach(var p in Pilots)
            {
                if(p.Hp<=0||!p.Seat.connected||p.Seat.AuthorityBoss)continue;
                if(!hubFeet.TryGetValue(p.Seat.id,out var f))hubFeet[p.Seat.id]=f=new HubFootsteps{Position=p.Body.position,Grounded=p.Grounded};
                float moved=HubPerception.FlatDistance(Vec(f.Position),Vec(p.Body.position));
                if(p.Grounded&&!f.Grounded)HubNoise(p,p.Body.position,HubNoiseKind.Land);
                if(p.Grounded&&moved>.005f&&moved<1&&Time>=f.WalkAt)
                {bool sprint=p.Pose.Sprinting;HubNoise(p,p.Body.position,sprint?HubNoiseKind.Sprint:HubNoiseKind.Walk);f.WalkAt=Time+(sprint?.3f:.6f);}
                if(p.LastAttack>f.AttackAt&&Time-p.LastAttack<.25f)
                {if(p.Weapon.@class=="melee")HubNoise(p,p.Body.position,HubNoiseKind.Fight);else if(p.Weapon.@class=="magic")HubNoise(p,p.Body.position,HubNoiseKind.Cast);}
                f.AttackAt=p.LastAttack;f.Position=p.Body.position;f.Grounded=p.Grounded;
            }
        }
        private bool HubSees(NativeMinion m,NativePilot p)
        {
            if(p==null||p.Hp<=0||!p.Seat.connected||HubInside(p.Body.position))return false;
            var at=m.Body.position;var to=p.Body.position;
            // Cheap geometry before the scene query; close notice is omnidirectional but still cannot see through a wall.
            if(!HubPerception.Sees(Vec(at),m.Yaw,Vec(to),true,HubCross(at,to)))return false;
            return !SceneLinecast(at+Vector3.up*.45f,to+Vector3.up*.45f,WorldMask);
        }
        private static NativePilot HubEnemy(List<NativePilot> enemies,int id)
        {foreach(var p in enemies)if(p.Seat.id==id)return p;return null;}
        private void HubSense(NativeMinion m,HubMob info,List<NativePilot> enemies)
        {
            var brain=info.Brain;NativePilot seen=null;float best=float.PositiveInfinity;
            foreach(var p in enemies)
            {
                if(!HubSees(m,p))continue;
                float score=(p.Body.position-m.Body.position).sqrMagnitude;if(p.Seat.id==brain.Target)score=-1;
                if(score<best){best=score;seen=p;}
            }
            info.Visible=seen?.Seat.id??0;
            if(seen!=null){brain.Observe(Time,seen.Seat.id,Vec(seen.Body.position),Vec(m.Body.position));info.Sound=hubSoundId;return;}
            // Each bounded noise event can be consumed only once by a zombie, including while it is searching.
            HubSound? heard=null;float loudest=-1;int newest=info.Sound;
            foreach(var n in hubSounds)
            {
                if(n.Id<=info.Sound)continue;newest=Math.Max(newest,n.Id);
                var source=HubEnemy(enemies,n.Source);if(source==null||HubInside(source.Body.position))continue;
                if(!HubPerception.Hears(Vec(m.Body.position),Vec(n.Position),n.Kind,HubCross(m.Body.position,n.Position)))continue;
                float strength=HubPerception.Radius(n.Kind)-(n.Position-m.Body.position).magnitude;
                if(strength>loudest){loudest=strength;heard=n;}
            }
            info.Sound=newest;if(heard.HasValue){var n=heard.Value;brain.Observe(Time,n.Source,Vec(n.Position),Vec(m.Body.position));}
        }
        private void HubWanderGoal(NativeMinion m,HubMob info)
        {
            // Authored nav nodes supply grounded destinations; try local points first so roaming stays a slow zone patrol.
            if(!info.Zone.Volume.Contains(new V3(m.Body.position.x,info.Zone.Volume.y,m.Body.position.z))){info.Brain.Wander(Time,Vec(info.Home));return;}
            var nodes=info.Points;int start=info.Brain.Pick(nodes.Length);Vector3 goal=info.Home;
            for(int i=0;i<nodes.Length;i++)
            {
                var at=nodes[(start+i)%nodes.Length];float distance=HubPerception.FlatDistance(Vec(m.Body.position),Vec(at));
                if(distance<3||distance>14||HubCross(m.Body.position,at,m.Collider.radius))continue;goal=at;break;
            }
            info.Brain.Wander(Time,Vec(goal));
        }
        // True means this tick was handled here, so ordinary horde omniscient targeting/strikes cannot run.
        private bool StepHubMind(NativeMinion m,List<NativePilot> enemies,out NativePilot target)
        {
            target=null;if(!HubOn||!hubMobs.TryGetValue(m.Id,out var info))return false;
            var a=m.Pve;var brain=info.Brain;
            if(Time>=info.SenseAt){info.SenseAt=Time+HubPerception.SenseInterval;HubSense(m,info,enemies);}
            var remembered=HubEnemy(enemies,brain.Target);
            bool valid=remembered!=null&&HubCanTarget(remembered,remembered);
            var previous=brain.View.State;brain.Step(Time,Vec(m.Body.position),valid);
            if((previous==HubZombieState.Chase||previous==HubZombieState.Alert)&&brain.View.State==HubZombieState.Search&&info.LostAt<0)info.LostAt=Time;
            if(brain.NeedsGoal)HubWanderGoal(m,info);
            a.Hub=brain.View;a.Target=valid?remembered:null;
            if(brain.View.State==HubZombieState.Chase&&valid&&info.Visible==remembered.Seat.id&&!HubInside(remembered.Body.position))
            {target=remembered;return false;}
            a.Struck=true; // Alert, memory pursuit and search never attack a pilot whose current position is unknown.
            var at=m.Body.position;var navAt=Vec(NativeNavSearch.BodyAnchor(Vec(at),m.Collider.height/2));
            Vector3 move=Vector3.zero;bool roam=brain.View.State==HubZombieState.Wander;
            bool travel=roam||brain.View.State==HubZombieState.Chase||brain.View.State==HubZombieState.Search&&!brain.SearchInPlace;
            if(travel&&Time>=a.StaggerAt&&Time>=a.RecoverAt)
            {
                var goal=Vec(brain.Goal);var d=goal-at;d.y=0;
                if(d.magnitude>1)move=HubNavMove(m,navAt,goal,d.normalized)*(roam?Math.Min(1.1f,a.Stats.walk*.55f):a.Stats.walk)*(a.Crawl?.35f:a.Limp?.55f:1);
                if(roam&&info.Zone.Volume.Contains(new V3(at.x,info.Zone.Volume.y,at.z))&&!info.Zone.Volume.Contains(new V3(at.x+move.x/60,info.Zone.Volume.y,at.z+move.z/60)))move=Vector3.zero;
                if(move.sqrMagnitude>.001f)brain.View.LookYaw=Mathf.Atan2(move.x,move.z)*Mathf.Rad2Deg;
            }
            if(brain.SearchInPlace)brain.View.LookYaw=Mathf.Repeat(brain.View.LookYaw+45f/60,360);
            m.Yaw=Mathf.MoveTowardsAngle(m.Yaw,brain.View.LookYaw,180f/60);
            float factor=Time<a.SlowUntil?a.SlowFactor:1;
            foreach(var field in ChipFields)if(CanHit(field.Owner,m)&&(field.Position-at).sqrMagnitude<field.Radius*field.Radius)factor=Math.Min(factor,field.Kind=="frost"?.45f:field.Kind=="time"?.6f:1);move*=factor;
            a.External*=Mathf.Exp(-6f/60);move+=Vector3.ClampMagnitude(a.External,6);
            if(!SupportedStageMove(navAt,move)||HubCross(at,at+move/60,m.Collider.radius))move=Vector3.zero;
            m.Body.linearVelocity=new Vector3(move.x,m.Body.linearVelocity.y,move.z);return true;
        }
    }
}
