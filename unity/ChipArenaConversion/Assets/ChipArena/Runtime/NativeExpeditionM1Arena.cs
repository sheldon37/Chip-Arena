using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // Local DEV slice. Expedition owns progression; the existing arena owns all combat and physics.
    public sealed partial class NativeArena
    {
        public ExpeditionDirector Expedition {get;private set;}
        private ExpeditionAreaLayout expeditionArea;
        private readonly Dictionary<string,BoxCollider> expeditionClosures=new Dictionary<string,BoxCollider>();
        private readonly Collider[] expeditionProbe=new Collider[32];
        private readonly Dictionary<string,bool> expeditionNavClear=new Dictionary<string,bool>();
        private string expeditionBoss;private int expeditionSpawned;private float expeditionSpawnAt;
        public ExpeditionLayout LoadExpeditionM1Layout(string json,NativePveContent horde)
        {
            string probeMap=null;
            void ProbeMap(string id){if(probeMap==id)return;ExpeditionMap(id);probeMap=id;}
            var looks=JsonUtility.FromJson<MapLookSet>(Resources.Load<TextAsset>("MapLooks").text);
            return NativeExpeditionLayout.Load(_=>json,Content,horde,id=>looks.maps.Any(l=>l.look==id),
                (id,p,r,h)=>{ProbeMap(id);if(!ExpeditionSupported(p,r,h))throw new InvalidOperationException($"Expedition placement blocked: {id} ({p.x}, {p.y}, {p.z}), radius {r}, height {h}.");return true;},
                (id,a,b,r,h)=>{ProbeMap(id);if(!ExpeditionReachable(a,b,r,h))throw new InvalidOperationException($"Expedition route blocked: {id} ({a.x}, {a.y}, {a.z}) to ({b.x}, {b.y}, {b.z}), radius {r}.");return true;});
        }
        public void ExpeditionMap(string map)
        {
            if(Replica||NativeMapRules.ArenaSelectable(Content.Map(map)))throw new InvalidOperationException("Expected an isolated Expedition map.");
            MapId=map;Reset();expeditionNavClear.Clear();expeditionClosures.Clear();expeditionBoss=null;expeditionSpawned=0;expeditionSpawnAt=Time;
        }
        public bool ExpeditionSupported(V3 floor,float radius,float height)
        {
            var at=Vec(floor);var physics=root.scene.GetPhysicsScene();
            if(!physics.Raycast(at+Vector3.up*.35f,Vector3.down,out var hit,.45f,WorldMask,QueryTriggerInteraction.Ignore)||hit.normal.y<.65f)return false;
            return physics.OverlapCapsule(at+Vector3.up*(radius+.04f),at+Vector3.up*(height-radius+.04f),radius,expeditionProbe,WorldMask,QueryTriggerInteraction.Ignore)==0;
        }
        public bool ExpeditionReachable(V3 from,V3 to,float radius,float height)
        {
            if(Map.nav==null)return false;var search=new NativeNavSearch(Map.nav);
            bool Clear(MapNavNode n){string key=radius+":"+height+":"+n.id;if(!expeditionNavClear.TryGetValue(key,out var ok))expeditionNavClear[key]=ok=ExpeditionSupported(n.pos-new V3(0,NativeLocomotionRules.HalfHeight,0),radius,height);return ok;}
            var offset=new V3(0,NativeLocomotionRules.HalfHeight,0);var a=search.Nearest(from+offset,Clear);var b=search.Nearest(to+offset,Clear);
            if(a==null||b==null||(a.pos-from-offset).Length>4||(b.pos-to-offset).Length>4)return false;
            var nodes=Map.nav.nodes.ToDictionary(n=>n.id);var path=new List<string>();
            search.Find(a.id,b.id,l=>
            {
                string key="link:"+radius+":"+height+":"+l.id;if(expeditionNavClear.TryGetValue(key,out var clear))return clear;
                expeditionNavClear[key]=false;if(!Clear(nodes[l.a])||!Clear(nodes[l.b]))return false;
                // Authored one-way drops intentionally leave the floor. Match the live authority's
                // drop policy after validating both capsule endpoints; a straight floor/LOS probe
                // through the ledge would reject the falling trajectory.
                if(l.kind=="drop")return expeditionNavClear[key]=NavLinkEnabled(l);
                var start=nodes[l.a].pos-offset;var end=nodes[l.b].pos-offset;int steps=Math.Max(1,(int)Math.Ceiling((end-start).Length/.4));
                for(int i=1;i<steps;i++)if(!ExpeditionSupported(start+(end-start)*((float)i/steps),radius,height))return false;
                return expeditionNavClear[key]=!SceneLinecast(Vec(start)+Vector3.up*(radius+.4f),Vec(end)+Vector3.up*(radius+.4f),WorldMask);
            },path);
            return path.Count>0;
        }
        public bool ExpeditionVisible(V3 from,V3 to)=>!SceneLinecast(Vec(from)+Vector3.up*.6f,Vec(to)+Vector3.up*.6f,WorldMask);
        public void BindExpeditionCombat(ExpeditionDirector director,ExpeditionAreaLayout area,NativePveContent data)
        {
            Expedition=director;expeditionArea=area;
            // Clone content so the ordinary Siren mode and authored resources remain unchanged.
            data=JsonUtility.FromJson<NativePveContent>(JsonUtility.ToJson(data));
            data.entries=NativeExpeditionLayout.ResolveSpawns(area,data).Select(s=>s.entry).ToArray();
            var bossPoint=area.bossPoints[0];data.boss=new PveBoss{spawn=new PvePoint{x=bossPoint.x,y=bossPoint.y,z=bossPoint.z},rescue=new PvePoint{x=bossPoint.x,y=bossPoint.y,z=bossPoint.z},yaw=bossPoint.yaw};
            if(area.role=="world")data.hideSpots=area.bossPoints.Select(p=>new PvePoint{x=p.x,y=p.y,z=p.z}).ToArray();
            Pve=new NativePveState{Content=data,Started=true,Stage="expedition",Cap=8,Rate=0,Floor=0,Phase=4,
                SpawnRandom=new NativePveRandom(director.Session.Run.Snapshot().seed),EconomyRandom=new NativePveRandom(12345)};
            Pve.Boss=Pilots.Single(p=>p.Seat.AuthorityBoss);SetBossHidden(true);
            foreach(var p in Pilots.Where(p=>!p.Seat.AuthorityBoss))Pve.Rows[p.Seat.id]=new NativePveRow{Pilot=p};
            foreach(var c in director.Closures.Where(c=>c.area==area.id))
            {
                var volume=area.gates.FirstOrDefault(g=>g.id==c.id)?.closure??area.doors.Single(d=>d.anchor==c.id).closure;
                var go=new GameObject("Expedition closure "+c.id);go.layer=PropLayer;go.transform.SetParent(root.transform);
                go.transform.position=new Vector3(volume.x,volume.y,volume.z);go.transform.rotation=Quaternion.Euler(0,volume.yaw,0);
                var box=go.AddComponent<BoxCollider>();box.size=new Vector3(volume.sx,volume.sy,volume.sz);box.enabled=c.Blocking;expeditionClosures.Add(c.id,box);
            }
        }
        public void ExpeditionSyncClosures()
        {if(Expedition!=null)foreach(var c in Expedition.Closures)if(expeditionClosures.TryGetValue(c.id,out var box))box.enabled=c.Blocking;}
        public bool ExpeditionPlace(string account,ExpeditionPoint slot)
        {
            var p=Pilots.SingleOrDefault(p=>p.Seat.access?.AccountId==account);
            if(p==null||!ExpeditionSupported(slot.Position,NativeLocomotionRules.Radius,2*NativeLocomotionRules.HalfHeight))return false;
            p.Body.position=Vec(slot.Position)+Vector3.up*(NativeLocomotionRules.HalfHeight+.04f);p.Yaw=slot.yaw;
            p.Knockback=p.MoveVelocity=p.Body.linearVelocity=Vector3.zero;Physics.SyncTransforms();return true;
        }
        private void StepExpeditionCombat()
        {
            var run=Expedition.Session.Run;var s=run.Snapshot();if(s.loading||s.phase==ExpeditionPhase.Ended)return;
            if(Local.Hp<=0){if(s.members.Single(m=>m.account==s.host).alive)run.Defeat(s.host);return;}
            if(s.phase==ExpeditionPhase.Instance)
            {
                var instance=s.instances.Single(i=>i.id==s.area);
                if(instance.progress<instance.target&&Time>=expeditionSpawnAt&&Minions.Count(m=>m.Pve!=null&&!m.DevMonster)<Pve.Cap)
                {
                    // Lost/out-of-world adds do not count as kills: replenish only enough live enemies to finish the quota.
                    if(instance.progress+Minions.Count(m=>m.Pve!=null&&!m.DevMonster)<instance.target)
                    {
                        string[] kinds={"shambler","runner","spitter","brute"};var add=SpawnPveAdd(kinds[expeditionSpawned%kinds.Length],true);
                        if(add!=null){var scale=new ExpeditionScaling().Evaluate(s.elapsed,1,s.loop,true);add.MaxHp*= (float)scale.health;add.Hp=add.MaxHp;add.Pve.DamageScale*=(float)scale.damage;expeditionSpawned++;}
                    }
                    expeditionSpawnAt=Time+1;
                }
                if(instance.progress>=instance.target&&instance.bosses.Length==0)run.StartBosses(new[]{"m1-ward-siren"});
                if(instance.progress>=instance.target&&!instance.cleared&&expeditionBoss==null)SpawnExpeditionBoss("m1-ward-siren");
            }
            if(s.phase==ExpeditionPhase.FinalBoss&&expeditionBoss==null)SpawnExpeditionBoss("m1-final-siren");
            if(expeditionBoss!=null)
            {
                var boss=Pve.Boss;Expedition.UpdateBossHealth(s.area,expeditionBoss,Math.Max(0,boss.Hp),Pve.MaxHp);
                if(boss.Hp<=0)
                {
                    bool accepted=s.phase==ExpeditionPhase.FinalBoss?Expedition.FinalDefeated(Vec(boss.Body.position)):Expedition.BossDefeated(expeditionBoss,Vec(boss.Body.position));
                    if(accepted){KillPveHorde();pveAcid.Clear();Bullets.Clear();SetBossHidden(true);}
                }
            }
            StepPveHorde();StepPveAcid();
        }
        private void SpawnExpeditionBoss(string id)
        {
            var s=Expedition.Session.Run.Snapshot();var scale=new ExpeditionScaling().Evaluate(s.elapsed,1,s.loop,s.phase==ExpeditionPhase.Instance);
            var point=expeditionArea.bossPoints[0];var boss=Pve.Boss;Pve.MaxHp=NativePveRules.BossHp(1)*(float)scale.health;
            boss.Stats["hp"]=Pve.MaxHp;boss.Stats["damage"]=NativePveRules.BossDamage(1)*(float)scale.damage;boss.Hp=Pve.MaxHp;boss.Seat.alive=true;
            SetBossHidden(false);boss.Body.position=Vec(point.Position)+Vector3.up*NativeLocomotionRules.HalfHeight;boss.Yaw=point.yaw;Pve.Stage="expedition";
            if(!Expedition.RecordBossArrival(id,"siren",kind=>Content.weapons.Any(w=>w.id==kind),Vec(boss.Body.position),boss.Hp,Pve.MaxHp))throw new InvalidOperationException("Boss arrival rejected.");
            expeditionBoss=id;
        }
        private void ExpeditionKilled(NativeMinion minion,NativePilot source)
        {if(minion.DevMonster)return;Expedition.MonsterDefeated("m1-"+Generation+"-"+minion.Id,Vec(minion.Body.position),1,false);Emit(EventKind.Death,source,minion.Id,minion.Body.position,0);}
    }
}
