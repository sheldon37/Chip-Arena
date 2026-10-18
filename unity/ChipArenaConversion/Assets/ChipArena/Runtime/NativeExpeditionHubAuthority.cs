using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private ExpeditionHubAuthority hubAuthority;
        private readonly List<BoxCollider> hubDoors=new List<BoxCollider>(),hubTops=new List<BoxCollider>();
        private readonly Dictionary<int,float> hubEntered=new Dictionary<int,float>();
        private readonly Dictionary<int,HubMob> hubMobs=new Dictionary<int,HubMob>();
        private readonly HashSet<string> hubBlockedLinks=new HashSet<string>();
        private readonly List<NativePilot> hubLiving=new List<NativePilot>();
        private float hubSpawnAt,hubMaintainAt;
        private bool hubNav;
        private sealed class HubMob{public string Ring;public Vector3 Home;public ExpeditionHubZone Zone;public Vector3[] Points;public HubZombieBrain Brain;public int Visible,Sound;public float SenseAt,FarAt=-1,IdleAt,LostAt=-1;}
        private bool HubOn=>Expedition!=null&&hubAuthority!=null&&expeditionArea?.role=="world";
        private double HubClock=>Expedition.Session.Run.Snapshot().elapsed;
        private bool HubInside(Vector3 p)=>HubOn&&ExpeditionHubRules.Inside(expeditionArea.sanctuary,Vec(p));
        private bool HubCross(Vector3 a,Vector3 b,float radius=0)=>HubOn&&ExpeditionHubRules.Crosses(expeditionArea.sanctuary,Vec(a),Vec(b),radius);
        private void ResetHub()
        {hubAuthority=null;hubDoors.Clear();hubTops.Clear();hubEntered.Clear();hubMobs.Clear();hubBlockedLinks.Clear();hubLiving.Clear();hubSounds.Clear();hubFeet.Clear();hubSoundId=0;hubNav=false;}
        private void BindHub()
        {
            ResetHub();Expedition.Session.Sources.Hub=()=>HubOn?hubAuthority.View():null;
            if(expeditionArea.role!="world")return;
            var layout=ExpeditionHubLayout.Parse(Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName).text,expeditionArea.id);if(layout==null)return;
            hubAuthority=new ExpeditionHubAuthority(layout,HubClock);hubSpawnAt=Time+(float)ExpeditionHubRules.SpawnInterval;hubMaintainAt=Time;
            // Dedicated collision layer: only horde bodies collide, never pilots, scenery or previews.
            const int wardLayer=28;for(int layer=0;layer<32;layer++)Physics.IgnoreLayerCollision(wardLayer,layer,layer!=11);
            foreach(var gate in layout.Gates)
            {
                BoxCollider Box(string name,int layer,float y,float sy)
                {
                    var obj=new GameObject(name+gate.Id);obj.layer=layer;obj.transform.SetParent(root.transform);
                    obj.transform.position=new Vector3(gate.Closure.x,y,gate.Closure.z);obj.transform.rotation=Quaternion.Euler(0,gate.Closure.yaw,0);
                    var box=obj.AddComponent<BoxCollider>();box.size=new Vector3(gate.Closure.sx,sy,gate.Closure.sz);return box;
                }
                hubDoors.Add(Box("Hub gate ",PropLayer,gate.Closure.y,gate.Closure.sy));
                hubTops.Add(Box("Hub gate no vault ",PropLayer,21.55f,36.9f));
                Box("Hub horde ward ",wardLayer,20,40);
            }
            if(Map.nav!=null)
            {
                var nodes=Map.nav.nodes.ToDictionary(n=>n.id);
                foreach(var l in Map.nav.links)if(layout.Gates.Any(g=>ExpeditionHubRules.Intersects(g.Closure,nodes[l.a].pos,nodes[l.b].pos,.5f))||HubCross(Vec(nodes[l.a].pos),Vec(nodes[l.b].pos)))hubBlockedLinks.Add(l.id);
            }
            StepHubGates();
        }
        private bool HubOccupied(ExpeditionVolume v)
        {
            foreach(var p in Pilots)if(p.Seat.connected&&!p.Seat.AuthorityBoss&&p.Body!=null)
            {
                var capsule=p.Collider;if(capsule==null||!capsule.enabled)continue;
                var scale=capsule.transform.lossyScale;float height=capsule.height*Math.Abs(scale.y),radius=capsule.radius*Math.Max(Math.Abs(scale.x),Math.Abs(scale.z));
                // Pilot capsules share the Rigidbody object. Its Transform may still be interpolated
                // or await the next physics step after a teleport; gate safety uses the physics pose.
                var center=p.Body.position+p.Body.rotation*Vector3.Scale(capsule.center,scale);
                if(ExpeditionHubRules.CapsuleOverlaps(v,Vec(center),height,radius))return true;
            }return false;
        }
        private void StepHubGates()
        {
            if(!HubOn)return;double now=HubClock;hubAuthority.Step(now,HubOccupied);
            for(int i=0;i<hubDoors.Count;i++){bool solid=hubAuthority.Blocking(i,now);hubDoors[i].enabled=hubTops[i].enabled=solid;}
        }
        private bool HubInteract(NativePilot p)
        {
            if(!HubOn||p.Hp<=0||!p.Seat.connected||p.Seat.AuthorityBoss||p.Seat.bot)return false;
            var before=hubAuthority.View();bool used=hubAuthority.Interact(Vec(p.Body.position),HubClock,HubOccupied);
            if(used){var after=hubAuthority.View();for(int i=0;i<after.Count;i++)if(after.Gates[i].Open&&!before.Gates[i].Open)HubNoise(p,p.Body.position,HubNoiseKind.Gate);StepHubGates();}return used;
        }
        private void HubInput(NativePilot p)
        {
            if(!HubOn)return;
            if(p.Input.Interact&&HubInteract(p))p.Input.Interact=false;
            if(!HubInside(p.Body.position)){hubEntered.Remove(p.Seat.id);return;}
            if(!hubEntered.TryGetValue(p.Seat.id,out float entered))hubEntered[p.Seat.id]=entered=Time;
            if(Time-entered<ExpeditionHubRules.HolsterDelay)return;
            p.Input=ExpeditionHubRules.Holster(p.Input);
            CancelMelee(p);CancelSpecial(p);CancelAbilities(p);CancelFighter(p);CancelSiren(p);p.BlockEnd=0;
            if(p.Kernel?.Active==true)EndKernel(p,3);
        }
        private bool HubCanTarget(NativePilot p,NativePilot previous)
        {
            if(!HubInside(p.Body.position))return true;
            return p==previous&&hubEntered.TryGetValue(p.Seat.id,out float entered)&&Time-entered<ExpeditionHubRules.AggroDelay;
        }
        // The damage boundary is enforced again at application, including melee, beams, fields, procs and recruits.
        private bool HubDamageBlocked(NativePilot source,Vector3 target,Vector3? origin=null,int projectile=0)
        {
            if(!HubOn)return false;if(HubInside(target))return true;
            Vector3 start=origin??source?.Body.position??target;
            if(projectile!=0){var bullet=Bullets.Find(b=>b.Id==projectile);if(bullet!=null)start=bullet.Position;}
            return HubCross(start,target)||HubInside(start);
        }
        private Vector3 HubNavMove(NativeMinion m,Vector3 from,Vector3 to,Vector3 fallback,int? crowd=null)
        {bool old=hubNav;hubNav=HubOn;try{return StructureNavMove(m.Id,from,to,fallback,HubOn?null:crowd);}finally{hubNav=old;}}
        private void StepHubPopulation()
        {
            if(!HubOn)return;StepHubGates();var state=Expedition.Session.Run.Snapshot();
            if(state.phase!=ExpeditionPhase.Town)return;StepHubNoise();
            hubLiving.Clear();foreach(var member in state.members)if(member.alive&&member.connected&&!member.extracted)
            {var p=Pilots.Find(pilot=>pilot.Seat.id==Expedition.Session.Sources.Seat?.Invoke(member.account));if(p!=null&&p.Hp>0)hubLiving.Add(p);}
            int lobby=Expedition.Session.Run.PartySnapshot().members.Count(m=>m.connected&&!m.extracted);
            Pve.Cap=ExpeditionHubRules.Cap(lobby,hubLiving.Count);Pve.Rate=0;
            if(Time>=hubMaintainAt)
            {
                hubMaintainAt=Time+.5f;
                var excess=Minions.Where(m=>hubMobs.ContainsKey(m.Id)).OrderByDescending(m=>hubLiving.Select(p=>(p.Body.position-m.Body.position).sqrMagnitude).DefaultIfEmpty(float.PositiveInfinity).Min()).ToArray();
                // Trim in batches when lobby size drops; never despawn dozens on one simulation frame.
                for(int i=0;i<Math.Min(8,excess.Length-Pve.Cap);i++){hubMobs.Remove(excess[i].Id);RemoveMinion(excess[i],false);}
                foreach(var m in Minions.ToArray())if(m.Pve!=null&&!m.DevMonster)
                {
                    if(HubInside(m.Body.position)){hubMobs.Remove(m.Id);RemoveMinion(m,false);continue;}
                    if(!hubMobs.TryGetValue(m.Id,out var info))continue;
                    // Euclidean >60 is a conservative lower bound on walked distance (never despawns a nearby pilot's mob).
                    bool far=hubLiving.All(p=>(p.Body.position-m.Body.position).sqrMagnitude>3600);
                    if(!far)info.FarAt=-1;else if(info.FarAt<0)info.FarAt=Time;
                    bool aggro=(info.Brain.View.State==HubZombieState.Alert||info.Brain.View.State==HubZombieState.Chase)&&m.Pve.Target!=null&&HubCanTarget(m.Pve.Target,m.Pve.Target);
                    if(aggro){info.IdleAt=Time;info.LostAt=-1;}else if(info.LostAt<0&&m.Pve.Target!=null)info.LostAt=Time;
                    if(info.FarAt>=0&&Time-info.FarAt>=ExpeditionHubRules.FarSeconds||Time-info.IdleAt>=ExpeditionHubRules.IdleSeconds||info.LostAt>=0&&Time-info.LostAt>=ExpeditionHubRules.LostSeconds)
                    {hubMobs.Remove(m.Id);RemoveMinion(m,false);}
                }
            }
            if(Time<hubSpawnAt)return;hubSpawnAt=Time+(float)ExpeditionHubRules.SpawnInterval;
            if(hubLiving.Count==0||Minions.Count(m=>m.Hp>0&&m.Pve!=null&&!m.DevMonster)>=Pve.Cap)return;
            // Rebuild candidates after each arrival: every zombie respects the same spawn clearance
            // and entry queue limits, including during a two-at-a-time population ramp.
            for(int spawned=0;spawned<ExpeditionHubRules.SpawnBurst&&Minions.Count(m=>m.Hp>0&&m.Pve!=null&&!m.DevMonster)<Pve.Cap;spawned++)
            {
            var candidates=new List<(ExpeditionHubZone zone,PveEntry entry)>();
            foreach(var zone in hubAuthority.Layout.Zones)foreach(int id in zone.Spawns)
            {
                var entry=Pve.Content.entries.FirstOrDefault(e=>e.e==id);if(entry==null||!WardLandingClear(entry)||EntryQueue(id)>0)continue;
                if(!Pve.Content.adds.Any(a=>ExpeditionHubRules.Kind(zone.Ring,a.id,state.elapsed)&&NativeWardEntries.Allows(entry,a.id)&&(a.id!="spitter"||Minions.Count(m=>m.Hp>0&&m.Pve?.Stats.id=="spitter")<NativeSpitterRules.Cap)))continue;
                var at=Vec(entry.@exit.Position(.85f));
                if(HubInside(at)||Minions.Any(m=>(m.Body.position-at).sqrMagnitude<9)||hubLiving.Any(p=>(p.Body.position-at).sqrMagnitude<400||((p.Body.position-at).sqrMagnitude<900&&!SceneLinecast(p.Body.position,at,WorldMask))))continue;
                candidates.Add((zone,entry));
            }
            if(candidates.Count==0)break;
            var zones=candidates.Select(x=>x.zone).Distinct().ToArray();double roll=Pve.SpawnRandom.Next()*zones.Sum(z=>ExpeditionHubRules.Weight(z.Ring));var chosen=zones[zones.Length-1];
            foreach(var zone in zones){roll-=ExpeditionHubRules.Weight(zone.Ring);if(roll<0){chosen=zone;break;}}
            var exits=candidates.Where(x=>x.zone==chosen).ToArray();var selected=exits[Math.Min(exits.Length-1,(int)(Pve.SpawnRandom.Next()*exits.Length))];
            var kinds=Pve.Content.adds.Where(a=>ExpeditionHubRules.Kind(chosen.Ring,a.id,state.elapsed)&&NativeWardEntries.Allows(selected.entry,a.id)&&(a.id!="spitter"||Minions.Count(m=>m.Hp>0&&m.Pve?.Stats.id=="spitter")<NativeSpitterRules.Cap)).Select(a=>a.id).ToArray();if(kinds.Length==0)break;
            double kindRoll=Pve.SpawnRandom.Next()*kinds.Sum(k=>k=="brute"?.15:1);string kind=kinds[kinds.Length-1];
            foreach(string candidate in kinds){kindRoll-=candidate=="brute"?.15:1;if(kindRoll<0){kind=candidate;break;}}
            var mob=SpawnPveAdd(kind,true,selected.entry.e);
            if(mob==null)break;
            {mob.Pve.HubRing=chosen.Ring;var home=Vec(selected.entry.@exit.Position(.85f));var brain=new HubZombieBrain(mob.Id,Time,Vec(home),mob.Yaw);
                var points=(Map.nav?.nodes??Array.Empty<MapNavNode>()).Where(n=>chosen.Volume.Contains(new V3(n.pos.x,chosen.Volume.y,n.pos.z))&&!HubInside(Vec(n.pos))).Select(n=>Vec(n.pos)).ToArray();
                mob.Pve.Hub=brain.View;hubMobs[mob.Id]=new HubMob{Ring=chosen.Ring,Zone=chosen,Points=points,Brain=brain,Home=home,IdleAt=Time,SenseAt=Time+(mob.Id%5)*.04f};}
            }
        }
    }
}
