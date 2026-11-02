using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // Host-owned Expedition: Expedition owns progression; the existing arena owns all combat and physics.
    public sealed partial class NativeArena
    {
        public ExpeditionDirector Expedition {get;private set;}
        private ExpeditionAreaLayout expeditionArea;
        private readonly Dictionary<string,BoxCollider> expeditionClosures=new Dictionary<string,BoxCollider>();
        private readonly Collider[] expeditionProbe=new Collider[32];
        private readonly Dictionary<string,bool> expeditionNavClear=new Dictionary<string,bool>();
        private readonly HashSet<string> expeditionDeaths=new HashSet<string>();
        private int expeditionZoneWave;private string expeditionBoss;private int expeditionEconomySerial;private int expeditionSpawned;private float expeditionSpawnAt;
        public ExpeditionRunBundle GeneratedRun {get;set;}
        public ExpeditionLayout LoadExpeditionM1Layout(string json,NativePveContent horde)
        {
            string probeMap=null;
            void ProbeMap(string id){if(probeMap==id)return;ExpeditionMap(id);probeMap=id;}
            var looks=JsonUtility.FromJson<MapLookSet>(Resources.Load<TextAsset>("MapLooks").text);
            if(GeneratedRun!=null)
            {
                var layout=GeneratedRun.Layout;
                foreach(var area in layout.areas)if(!looks.maps.Any(l=>l.look==Content.Map(area.map).look))throw new InvalidOperationException("Missing generated run look.");
                NativeExpeditionLayout.ValidateGeometry(layout,Content,horde,
                    (id,p,r,h)=>{ProbeMap(id);return ExpeditionSupported(p,r,h);},
                    (id,a,b,r,h)=>{ProbeMap(id);return ExpeditionReachable(a,b,r,h);});
                return layout;
            }
            return NativeExpeditionLayout.Load(_=>json,Content,horde,id=>looks.maps.Any(l=>l.look==id),
                (id,p,r,h)=>{ProbeMap(id);if(!ExpeditionSupported(p,r,h))throw new InvalidOperationException($"Expedition placement blocked: {id} ({p.x}, {p.y}, {p.z}), radius {r}, height {h}.");return true;},
                (id,a,b,r,h)=>{ProbeMap(id);if(!ExpeditionReachable(a,b,r,h))throw new InvalidOperationException($"Expedition route blocked: {id} ({a.x}, {a.y}, {a.z}) to ({b.x}, {b.y}, {b.z}), radius {r}.");return true;});
        }
        public void ExpeditionMap(string map)
        {
            if(Replica||NativeMapRules.ArenaSelectable(Content.Map(map)))throw new InvalidOperationException("Expected an isolated Expedition map.");
            if(Pve!=null)foreach(var recruit in Pve.Recruits)Match.Seats.Remove(recruit.Pilot.Seat);
            ResetZoneCombat();ResetHub();MapId=map;Reset();BossSongAt=-1;BossSongLead=0;expeditionNavClear.Clear();expeditionDeaths.Clear();expeditionClosures.Clear();expeditionBoss=null;expeditionSpawned=0;expeditionZoneWave=0;expeditionSpawnAt=Time;
        }
        public bool ExpeditionSupported(V3 floor,float radius,float height)
        {
            var at=Vec(floor);var physics=root.scene.GetPhysicsScene();
            if(!physics.Raycast(at+Vector3.up*.35f,Vector3.down,out var hit,.45f,WorldMask,QueryTriggerInteraction.Ignore)||hit.normal.y<.65f)return false;
            // A sphere resting on a slope needs radius / normal.y vertical clearance.
            // Flat-floor epsilon alone intersects steeper authored stairs, falsely disconnecting their navigation.
            float lift=.04f+radius*(1f/hit.normal.y-1f);
            return physics.OverlapCapsule(at+Vector3.up*(radius+lift),at+Vector3.up*(height-radius+lift),radius,expeditionProbe,WorldMask,QueryTriggerInteraction.Ignore)==0;
        }
        public bool ExpeditionReachable(V3 from,V3 to,float radius,float height)
        {
            if(Map.nav==null)return false;var search=new NativeNavSearch(Map.nav);
            bool Clear(MapNavNode n){string key=radius+":"+height+":"+n.id;if(!expeditionNavClear.TryGetValue(key,out var ok))expeditionNavClear[key]=ok=ExpeditionSupported(n.pos-new V3(0,NativeLocomotionRules.HalfHeight,0),radius,height);return ok;}
            var offset=new V3(0,NativeLocomotionRules.HalfHeight,0);var a=search.Nearest(from+offset,Clear);var b=search.Nearest(to+offset,Clear);
            if(a==null||b==null||(a.pos-from-offset).Length>4||(b.pos-to-offset).Length>4)return false;
            var nodes=Map.nav.nodes.ToDictionary(n=>n.id);var path=new List<string>();
            bool Active(MapNavLink l)
            {
                string key="link:"+radius+":"+height+":"+l.id;if(expeditionNavClear.TryGetValue(key,out var clear))return clear;
                expeditionNavClear[key]=false;if(!Clear(nodes[l.a])||!Clear(nodes[l.b]))return false;
                // Authored one-way drops intentionally leave the floor. Match the live authority's
                // drop policy after validating both capsule endpoints; a straight floor/LOS probe
                // through the ledge would reject the falling trajectory.
                if(l.kind=="drop")return expeditionNavClear[key]=NavLinkEnabled(l);
                var start=nodes[l.a].pos-offset;var end=nodes[l.b].pos-offset;int steps=Math.Max(1,(int)Math.Ceiling((end-start).Length/.4));
                for(int i=1;i<steps;i++)
                {
                    var sample=start+(end-start)*((float)i/steps);
                    // A link crosses a ramp/landing kink; its straight chord is not the floor.
                    if(!PhysicsWorld.Raycast(Vec(sample)+Vector3.up*.2f,Vector3.down,out var ground,.4f,WorldMask,QueryTriggerInteraction.Ignore)||ground.normal.y<.65f||!ExpeditionSupported(Vec(ground.point),radius,height))return false;
                }
                return expeditionNavClear[key]=!SceneLinecast(Vec(start)+Vector3.up*(radius+.4f),Vec(end)+Vector3.up*(radius+.4f),WorldMask);
            }
            search.Find(a.id,b.id,Active,path);
            if(path.Count==0)
            {
                var raw=new List<string>();search.Find(a.id,b.id,NavLinkEnabled,raw);int printed=0;
                for(int j=1;j<raw.Count&&printed<16;j++)
                {
                    var l=Map.nav.links.FirstOrDefault(e=>(e.a==raw[j-1]&&e.b==raw[j])||(e.b==raw[j-1]&&e.a==raw[j]));
                    if(l==null||Active(l))continue;printed++;
                    var start=nodes[l.a].pos-offset;var end=nodes[l.b].pos-offset;
                    string Detail(V3 p)
                    {
                        var at=Vec(p);if(!PhysicsWorld.Raycast(at+Vector3.up*.35f,Vector3.down,out var ground,.7f,WorldMask,QueryTriggerInteraction.Ignore))return "no floor";
                        float lift=.04f+radius*(1f/ground.normal.y-1f);int count=PhysicsWorld.OverlapCapsule(ground.point+Vector3.up*(radius+lift),ground.point+Vector3.up*(height-radius+lift),radius,expeditionProbe,WorldMask,QueryTriggerInteraction.Ignore);
                        return "floor="+ground.collider.name+" y="+ground.point.y+" normal="+ground.normal.y+" blockers="+string.Join(",",expeditionProbe.Take(count).Select(c=>c.name));
                    }
                    Debug.Log("Expedition blocked link "+MapId+" "+l.id+" "+Vec(start)+" -> "+Vec(end)+" endpoints="+Clear(nodes[l.a])+"/"+Clear(nodes[l.b])+" mid "+Detail((start+end)*.5f));
                }
            }
            return path.Count>0;
        }
        public bool ExpeditionVisible(V3 from,V3 to)=>!SceneLinecast(Vec(from)+Vector3.up*.6f,Vec(to)+Vector3.up*.6f,WorldMask);
        public void BindExpeditionCombat(ExpeditionDirector director,ExpeditionAreaLayout area,NativePveContent data)
        {
            Expedition=director;expeditionArea=area;
            director.Session.Installed=(account,chip,count)=>
            {
                SyncExpeditionInventory();
                var pilot=Pilots.FirstOrDefault(p=>p.Seat.id==director.Session.Sources.Seat?.Invoke(account));
                if(pilot!=null)Emit(EventKind.Install,pilot,pilot.Seat.id,pilot.Body.position,count,chip);
            };
            // Clone content so the ordinary Siren mode and authored resources remain unchanged.
            data=JsonUtility.FromJson<NativePveContent>(JsonUtility.ToJson(data));
            NativeExpeditionLayout.BindContent(area,data);
            if(NativeMapRules.ExpeditionInstance(MapId))BuildPveMachines(data);
            Pve=new NativePveState{Content=data,Started=true,Stage=area.role=="instance"?"wave":"intermission",Wave=1,Cap=8,Rate=0,Floor=0,Phase=1,
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
            BindHub();
        }
        public void ExpeditionSyncClosures()
        {if(Expedition!=null)foreach(var c in Expedition.Closures)if(expeditionClosures.TryGetValue(c.id,out var box))box.enabled=c.Blocking;}
        public bool ExpeditionPlace(string account,ExpeditionPoint slot)
        {
            var p=ExpeditionPilotFor(account);
            if(p==null||!ExpeditionSupported(slot.Position,NativeLocomotionRules.Radius,2*NativeLocomotionRules.HalfHeight))return false;
            p.Body.position=Vec(slot.Position)+Vector3.up*(NativeLocomotionRules.HalfHeight+.04f);p.Yaw=slot.yaw;
            p.Knockback=p.MoveVelocity=p.Body.linearVelocity=Vector3.zero;Physics.SyncTransforms();return true;
        }
        private ExpeditionMember ExpeditionMemberFor(NativePilot pilot)
            =>Expedition?.Session.Run.Snapshot().members.FirstOrDefault(m=>Expedition.Session.Sources.Seat?.Invoke(m.account)==pilot.Seat.id);
        public void SyncExpeditionInventory()
        {
            if(Expedition==null||Replica)return;
            var state=Expedition.Session.Run.Snapshot();if(state.loading)return;
            foreach(var member in state.members)
            {
                var pilot=Pilots.FirstOrDefault(p=>p.Seat.id==Expedition.Session.Sources.Seat?.Invoke(member.account));if(pilot==null)continue;
                if(Pve.Rows.TryGetValue(pilot.Seat.id,out var row)){row.Money=member.wallet;row.Bought=state.rewards.Count(r=>r.owner==member.account&&r.source=="chest"&&r.id.Contains(":chest:ward-machine:"));}
                if(pilot.Seat.chips.Count==member.chips.Length&&member.chips.All(c=>pilot.Seat.chips.TryGetValue(c.id,out int n)&&n==c.count))continue;
                pilot.Seat.chips.Clear();foreach(var chip in member.chips)pilot.Seat.chips.Add(chip.id,chip.count);RefreshLiveStats(pilot);
            }
        }
        public bool TryExpeditionInteract(NativePilot pilot,out string notice)
        {
            notice=null;if(Expedition==null||Replica||Pve==null||Pve.Holding||pilot==null||pilot.Hp<=0||!pilot.Seat.connected)return false;
            var session=Expedition.Session;var run=session.Run;var state=run.Snapshot();var member=ExpeditionMemberFor(pilot);
            if(pilot.ExpeditionTransferHeld||ExpeditionChoosing(pilot)||state.loading||member==null||!member.alive||member.extracted)return false;
            // Match the existing F-revive beacon, using the run wallet and durable alive state.
            var fallen=Pilots.Where(q=>q!=pilot&&q.Hp<=0&&q.Seat.connected&&!InShaft(q)&&!q.ExpeditionTransferHeld)
                .Where(q=>Math.Abs(q.Body.position.y-pilot.Body.position.y)<=1.7f&&new Vector2(q.Body.position.x-pilot.Body.position.x,q.Body.position.z-pilot.Body.position.z).magnitude<=2.2f)
                .OrderBy(q=>(q.Body.position-pilot.Body.position).sqrMagnitude).FirstOrDefault(q=>ExpeditionVisible(Vec(pilot.Body.position),Vec(q.Body.position)));
            if(fallen!=null)
            {
                RecordExpeditionDeaths();var target=ExpeditionMemberFor(fallen);
                if(target!=null&&Pve.Rows.TryGetValue(fallen.Seat.id,out var deadRow))
                {
                    int price=NativePveRules.RevivePrice(deadRow.Revived);
                    if(run.Revive(member.account,target.account,price))
                    {
                        deadRow.Revived++;if(Pve.Rows.TryGetValue(pilot.Seat.id,out var payer)){payer.Spent+=price;payer.Revives++;}
                        RevivePve(fallen);expeditionDown.Remove(target.account);SyncExpeditionInventory();notice="Teammate revived";
                    }
                    else notice=member.wallet<price?"Not enough money to revive · $"+price+" required":"This teammate cannot be revived here";
                    return true;
                }
            }
            if(ZoneLever(pilot)){notice="Signal levers · power cut for 60 s";return true;}
            if(HubInteract(pilot)){notice="Sanctuary gate";return true;}
            var feet=Vec(pilot.Body.position-Vector3.up*NativeLocomotionRules.HalfHeight);
            foreach(var reward in state.rewards.Where(r=>!r.claimed&&(r.owner==null||r.owner==member.account)))
                if(Expedition.CanClaim(member.account,reward.id))
                {var result=session.Execute(member.account,new ExpeditionCommand(ExpeditionIntent.Claim,reward.id));notice=result.state==ExpeditionCommandState.Pending?"Chip collected · C to choose":result.reason;return true;}
            var cache=expeditionArea.interactions.Where(i=>state.chests.Any(c=>c.id==i.anchor)&&Expedition.CanPurchase(member.account,i.anchor))
                .OrderBy(i=>(new V3(i.x,i.y,i.z)-feet).Length).FirstOrDefault();
            if(cache!=null)
            {
                var result=session.Execute(member.account,new ExpeditionCommand(ExpeditionIntent.Purchase,cache.anchor));
                if(result.state==ExpeditionCommandState.Pending)
                {OpenExpeditionPurchases(member.account);notice="Cache purchased · C to choose your chip";}
                else notice=result.reason;
                SyncExpeditionInventory();return true;
            }
            if(NativeMapRules.ExpeditionInstance(MapId))
            {
                var machine=Pve.Content.machines.Where(m=>(m.front.Position()-feet).Length<=2.2f&&ExpeditionVisible(feet,m.front.Position()))
                    .OrderBy(m=>(m.front.Position()-feet).Length).FirstOrDefault();
                if(machine!=null){notice=BuyExpeditionMachine(pilot,member,machine);SyncExpeditionInventory();return true;}
            }
            var reprint=expeditionArea.anchors.FirstOrDefault(a=>a.kind=="machine"&&Expedition.CanPurchase(member.account,a.id));
            if(reprint!=null){notice=BeginExpeditionReprint(pilot,member,reprint.id);return true;}
            return false;
        }
        private void OpenExpeditionPurchases(string account)
        {
            foreach(var reward in Expedition.Session.Run.Snapshot().rewards.Where(r=>r.source=="chest"&&r.owner==account&&!r.claimed))
                Expedition.Session.Execute(account,new ExpeditionCommand(ExpeditionIntent.Claim,reward.id));
        }
        private string BuyExpeditionMachine(NativePilot pilot,ExpeditionMember member,PveMachine machine)
        {
            var run=Expedition.Session.Run;var row=Pve.Rows[pilot.Seat.id];var state=run.Snapshot();
            // Count personal dispenser purchases from the durable run, not a row reset on area load.
            int bought=state.rewards.Count(r=>r.owner==member.account&&r.source=="chest"&&r.id.Contains(":chest:ward-machine:"));
            int cost=machine.kind=="chip"?NativePveRules.ChipPrice(bought):machine.kind=="recruit"?300:75;
            string Use(string result,string message)
            {Pve.Machines.RemoveAll(u=>Time-u.at>4);while(Pve.Machines.Count>=32)Pve.Machines.RemoveAt(0);Pve.Machines.Add(new NativeMachineUse{machine=machine.m,owner=pilot.Seat.id,result=result,at=Time});return message;}
            if(member.wallet<cost)return Use("deny","Not enough money · $"+cost+" required");
            if(machine.kind=="recruit"&&(Pve.Recruits.Count(r=>r.Owner==pilot)+Pve.Hires.Count(h=>h.owner==pilot)>=2||Pve.Recruits.Count+Pve.Hires.Count>=3||Pilots.Count+Pve.Hires.Count>=8))return Use("deny","Recruit limit reached");
            if(machine.kind=="med"&&pilot.Hp>=pilot.Stats["hp"])return Use("deny","Health is full");
            if(machine.kind=="med"&&Time<row.HealReady)return Use("deny","Med station is recharging");
            string transaction="ward-machine:"+machine.m+":"+state.revision;
            if(machine.kind=="chip")
            {
                if(!run.Purchase(member.account,transaction,cost))return Use("deny","Chip purchase refused");
                OpenExpeditionPurchases(member.account);row.Bought=bought+1;
            }
            else
            {
                if(machine.kind!="med"&&machine.kind!="recruit")return Use("deny","Unknown machine service");
                if(!run.Spend(member.account,transaction,cost))return Use("deny","Machine payment refused");
                if(machine.kind=="recruit")Pve.Hires.Add((pilot,Time+.8f,machine.m));
                else{row.HealAt=Time;row.HealReady=Time+12;row.HealPulses=0;}
            }
            row.Spent+=cost;Emit(EventKind.PveBuy,pilot,machine.m,pilot.Body.position,cost,machine.kind);
            return Use(machine.kind,machine.kind=="chip"?"Chip purchased · C to choose":machine.kind=="recruit"?"Recruit hired for this area":"Healing started");
        }
        private void StepExpeditionCombat()
        {
            var run=Expedition.Session.Run;var s=run.Snapshot();if(s.loading||s.phase==ExpeditionPhase.Ended)return;
            RecordExpeditionDeaths();s=run.Snapshot();if(!s.members.Any(m=>m.alive&&m.connected&&!m.extracted))return;
            StepExpeditionReprints();StepHubPopulation();
            if(s.phase==ExpeditionPhase.Instance)
            {
                var instance=s.instances.Single(i=>i.id==s.area);
                if(NativeZoneCombat.Active(MapId)){Pve.Wave=NativeZoneCombat.Wave(instance.progress,instance.target);Pve.Cap=16;}
                if(instance.progress<instance.target&&Time>=expeditionSpawnAt&&Minions.Count(m=>m.Pve!=null&&!m.DevMonster)<Pve.Cap)
                {
                    // Lost/out-of-world adds do not count as kills: replenish only enough live enemies to finish the quota.
                    if(instance.progress+Minions.Count(m=>m.Pve!=null&&!m.DevMonster)<instance.target)
                    {
                        string[] kinds={"shambler","runner","spitter","brute"};bool zone=NativeZoneCombat.Active(MapId),intro=zone&&expeditionZoneWave<Pve.Wave;
                        string kind=intro?(NativeZoneCombat.Station(MapId)?new[]{"conductor","linesman","undertow","pileup"}:new[]{"cinderling","rigger","slagback","ladleman"})[Pve.Wave-1]:zone?PickHordeKind(NativeZoneCombat.Encounter(MapId,Pve.Wave,false),Pve):kinds[expeditionSpawned%kinds.Length];
                        var add=SpawnPveAdd(kind,true);if(add!=null&&intro)expeditionZoneWave=Pve.Wave;
                        if(add!=null){var scale=new ExpeditionScaling().Evaluate(s.elapsed,Math.Max(1,s.instances.FirstOrDefault(i=>i.id==s.area)?.scalingParty>0?s.instances.First(i=>i.id==s.area).scalingParty:s.members.Count(m=>m.connected&&!m.extracted)),s.loop,true);add.MaxHp*= (float)scale.health;add.Hp=add.MaxHp;add.Pve.DamageScale*=(float)scale.damage;expeditionSpawned++;if(kind=="cinderling")for(int n=0;n<2&&instance.progress+Minions.Count(m=>m.Pve!=null&&!m.DevMonster)<instance.target;n++){var pack=SpawnPveAdd(kind,true,add.Pve.Entry.e);if(pack==null)break;pack.MaxHp*=(float)scale.health;pack.Hp=pack.MaxHp;pack.Pve.DamageScale*=(float)scale.damage;expeditionSpawned++;}}
                    }
                    expeditionSpawnAt=Time+1;
                }
                if(instance.progress>=instance.target&&instance.bosses.Length==0)run.StartBosses(new[]{"m1-ward-siren"});
                if(instance.progress>=instance.target&&!instance.cleared&&expeditionBoss==null)SpawnExpeditionBoss("m1-ward-siren");
            }
            if(s.phase==ExpeditionPhase.FinalBoss&&expeditionBoss==null)SpawnExpeditionBoss("m1-final-siren");
            if(Pve.Holding){if(ZoneHazards!=null)ZoneHazards.trainAt=Math.Max(ZoneHazards.trainAt,Time+4);return;}
            if(expeditionBoss!=null)
            {
                var boss=Pve.Boss;if(ZoneBoss!=null)StepZoneBoss();bool defeated=ZoneBoss!=null?ZoneBoss.deathAt>=0&&Time>=ZoneBoss.deathAt+(ZoneBoss.archetype==NativeZoneBossRules.Station?4:7):StepPveBossPhases();if(Pve.Holding)return;Expedition.UpdateBossHealth(s.area,expeditionBoss,Math.Max(0,boss.Hp),Pve.MaxHp);
                if(defeated)
                {
                    bool accepted=s.phase==ExpeditionPhase.FinalBoss?Expedition.FinalDefeated(Vec(boss.Body.position)):Expedition.BossDefeated(expeditionBoss,Vec(boss.Body.position));
                    if(accepted){KillPveHorde();pveAcid.Clear();Bullets.Clear();SetBossHidden(true);Pve.Stage="intermission";Pve.Rate=0;BossSongAt=-1;BossSongLead=0;}
                }
            }
            StepZoneHazards();StepZoneEvents();StepPveServices();StepPveHorde();StepPveAcid();
        }
        private void SpawnExpeditionBoss(string id)
        {
            foreach(var mob in Minions.Where(m=>hubMobs.ContainsKey(m.Id)).ToArray())RemoveMinion(mob,false);hubMobs.Clear();Pve.Cap=8;
            var s=Expedition.Session.Run.Snapshot();var scale=new ExpeditionScaling().Evaluate(s.elapsed,Math.Max(1,s.instances.FirstOrDefault(i=>i.id==s.area)?.scalingParty>0?s.instances.First(i=>i.id==s.area).scalingParty:s.members.Count(m=>m.connected&&!m.extracted)),s.loop,s.phase==ExpeditionPhase.Instance);
            var point=expeditionArea.bossPoints[0];var boss=Pve.Boss;Pve.MaxHp=NativePveRules.BossHp(Math.Max(1,s.instances.FirstOrDefault(i=>i.id==s.area)?.scalingParty>0?s.instances.First(i=>i.id==s.area).scalingParty:s.members.Count(m=>m.connected&&!m.extracted)))*(float)scale.health;
            boss.Stats["hp"]=Pve.MaxHp;boss.Stats["damage"]=NativePveRules.BossDamage(Math.Max(1,s.instances.FirstOrDefault(i=>i.id==s.area)?.scalingParty>0?s.instances.First(i=>i.id==s.area).scalingParty:s.members.Count(m=>m.connected&&!m.extracted)))*(float)scale.damage;boss.Hp=Pve.MaxHp;boss.Seat.alive=true;
            SetBossHidden(false);boss.Body.position=Vec(point.Position)+Vector3.up*NativeLocomotionRules.HalfHeight;boss.Yaw=point.yaw;Pve.Stage="boss";Pve.Phase=1;Pve.Floor=Pve.MaxHp*.75f;
            string archetype=NativeZoneBossRules.RuntimeEnabled?NativeZoneBossRules.Archetype(MapId)??"siren":"siren";if(archetype!="siren"){var position=archetype==NativeZoneBossRules.Station?new Vector3(0,0,1.5f):Vec(point.Position);SpawnZoneBoss(archetype,position,archetype==NativeZoneBossRules.Station?0:90);}
            if(!Expedition.RecordBossArrival(id,archetype,kind=>NativeZoneBossRules.Known(kind)||Content.weapons.Any(w=>w.id==kind),ZoneBoss==null?point.Position:Vec(zoneBossActor.Body.position),boss.Hp,Pve.MaxHp))throw new InvalidOperationException("Boss arrival rejected.");
            expeditionBoss=id;BeginPveHold("intro");
        }
        private void ExpeditionKilled(NativeMinion minion,NativePilot source)
        {
            if(minion.DevMonster)return;string kill="m1-"+Generation+"-"+minion.Id;if(!expeditionDeaths.Add(kill))return;
            Expedition.MonsterDefeated(kill,Vec(minion.Body.position),1,false);
            var add=minion.Pve;var recruit=Pve.Recruits.Find(r=>r.Pilot==source);var paid=recruit?.Owner??source;bool head=(add.Lost&1)!=0;bool hub=add.HubRing!=null;float reward=hub?(float)Math.Round(add.Stats.reward*ExpeditionHubRules.Reward(add.HubRing),MidpointRounding.AwayFromZero):add.Stats.reward;float headReward=hub?5:10;
            bool Pay(NativePilot pilot,float amount,string reason)
            {
                if(pilot==null||pilot.Seat.bot||HubOn&&!add.HubCredit)return false;
                var member=Expedition.Session.Run.Snapshot().members.FirstOrDefault(m=>Expedition.Session.Sources.Seat?.Invoke(m.account)==pilot.Seat.id);
                int money=NativePveRules.Money(pilot.Seat.chips,reason,amount);
                if(member==null||!Expedition.Session.Run.Credit(member.account,"money:"+kill+":"+reason,money))return false;
                if(Pve.Rows.TryGetValue(pilot.Seat.id,out var row))row.Earned+=money;
                Emit(EventKind.PveMoney,pilot,pilot.Seat.id,pilot.Body.position,money,reason);return true;
            }
            bool credited=recruit!=null?Pay(paid,(reward+(head?headReward:0))*.5f,"recruit"):Pay(paid,reward,"kill");
            if(recruit==null&&head)Pay(paid,headReward,"head");
            if(credited&&Pve.Rows.TryGetValue(paid.Seat.id,out var owner))owner.Kills++;
            foreach(var hit in add.Dealt)if(hit.Key!=source?.Seat.id&&hit.Key!=paid?.Seat.id&&hit.Value>=minion.MaxHp*.2f)
            {var pilot=Pilots.Find(p=>p.Seat.id==hit.Key);if(Pay(pilot,reward*.4f,"assist")&&Pve.Rows.TryGetValue(hit.Key,out var row))row.Assists++;}
            hubMobs.Remove(minion.Id);
            if(add.Possessed&&!hub)foreach(var pilot in Members())GrantPveMoney(pilot,pilot==paid?300:100,"possessed");
            Emit(EventKind.Death,source,minion.Id,minion.Body.position,0);
        }
    }
}
