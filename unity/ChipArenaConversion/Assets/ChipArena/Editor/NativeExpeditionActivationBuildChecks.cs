using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        private static void CheckExpeditionActivation(ConversionContent content)
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Expedition activation: "+why);}
            string json=Resources.Load<TextAsset>("ExpeditionLayout").text;
            var layout=NativeExpeditionLayout.Parse(json);layout.compatibility=NativeExpeditionLayout.Compatibility(json,content);
            var joinWire=new ExpeditionView{joins=new[]{new ExpeditionJoinView{h=4,deployedAt=3,deployedAccounts=new[]{"p2"},closedReason=ExpeditionJoinClosedReason.Reserved}},members=new[]{new ExpeditionViewMember{account="p2",area=layout.world,pendingArea=layout.instances[0]}}};
            var decodedJoin=JsonUtility.FromJson<ExpeditionView>(JsonUtility.ToJson(joinWire));
            Check(decodedJoin.joins[0].h==4&&decodedJoin.joins[0].deployedAt==3&&decodedJoin.joins[0].deployedAccounts.SequenceEqual(new[]{"p2"})&&decodedJoin.joins[0].closedReason==ExpeditionJoinClosedReason.Reserved&&decodedJoin.members[0].pendingArea==layout.instances[0],"join presentation fields survive Unity wire serialization");
            var horde=JsonUtility.FromJson<NativePveContent>(Resources.Load<TextAsset>("PveContent").text);
            var run=NativeExpedition.Create("native-split",layout.world,"p1","pistol",17,layout.instances.Select(id=>new ExpeditionInstance{id=id,target=12}).ToArray());
            foreach(int id in new[]{2,3,4})Check(run.Join("p"+id,"pistol"),"join fixture pilot");
            var initial=new ExpeditionSession(run,content);new ExpeditionDirector(initial,layout,_=>null,(_,__)=>true,100,100,60);run.EnableAreas();
            var sessions=new Dictionary<string,ExpeditionSession>();
            using(var registry=new ExpeditionAreaRegistry<NativeExpeditionAreaRuntime>(layout.areas.Select(a=>a.id),area=>new NativeExpeditionAreaRuntime(area,content,layout.Area(area).map,match=>
            {
                match.Cooperative=match.Teams=true;match.ShowIntros=match.FinalKillcams=false;
                if(area==layout.world)foreach(int id in new[]{1,2,3,4})match.Seats.Add(new NativeSeat{id=id,name="Split "+id,spec="pistol",team=0});
                var boss=new NativeSeat{id=900,name="Siren",spec="siren",bot=true,team=1};
                typeof(NativeSeat).GetField("AuthorityBoss",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(boss,true);match.Seats.Add(boss);
            },_=>EditorSceneManager.NewPreviewScene(),s=>EditorSceneManager.ClosePreviewScene(s))))
            {
                int Seat(string id)=>int.Parse(id.Substring(1));
                foreach(var area in layout.areas)
                {
                    var runtime=registry.Acquire(area.id);var sim=runtime.Arena;
                    var session=new ExpeditionSession(run.ForArea(area.id),content);session.Sources.Seat=Seat;
                    var director=new ExpeditionDirector(session,layout,id=>sim.Pilots.FirstOrDefault(p=>p.Seat.id==Seat(id)) is NativePilot p?NativeArena.Vec(p.Body.position-Vector3.up*NativeLocomotionRules.HalfHeight):(V3?)null,sim.ExpeditionVisible,100,100,60);
                    runtime.Ready(a=>{a.Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,cooperative=true,teams=true});a.Match.HoldResolution=()=>true;a.BindExpeditionCombat(director,area,horde);});
                    runtime.SetOccupants(area.id==layout.world?4:0);sessions.Add(area.id,session);
                }
                var hub=registry.Find(layout.world);Check(hub.Arena.PlaceExpeditionParty(Enumerable.Range(1,4).ToDictionary(id=>"p"+id,id=>layout.Area(layout.world).slots[id-1])),"hub placement");
                var cohorts=new ExpeditionCohorts("native-split",1,layout.compatibility,layout.areas.Select(a=>a.id));
                foreach(int id in new[]{1,2,3,4}){cohorts.Add("p"+id,layout.world);Check(cohorts.Acknowledge("p"+id,cohorts.Member("p"+id).Ticket),"initial ready");}
                var transfers=new NativeExpeditionAreaTransfers(registry,layout,cohorts,Seat);
                int seat=2;
                foreach(var area in layout.areas.Where(a=>a.role=="instance"))
                {
                    string who="p"+seat++,door=layout.Area(layout.world).anchors.Single(a=>a.building==area.id).id;
                    // Start at the actual outlying hub circle, not the safe centre of town.
                    var join=layout.Area(layout.world).doors.Single(d=>d.anchor==door).join;
                    Check(hub.Arena.ExpeditionPlace(who,new ExpeditionPoint{x=join.x,y=join.y,z=join.z}),"stand at actual hub circle "+area.id);
                    var old=cohorts.Member(who).Ticket;
                    Check(transfers.Begin("fixture-"+who,layout.world,area.id,door,new[]{who}),"reserve "+area.id);
                    var ticket=cohorts.Member(who).Ticket;
                    var ready=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="expedition-area-ready",epoch=7,areaIdentity=ticket,expeditionAreaReady=new ExpeditionAreaReadyRequest{run=ticket.run,epoch=ticket.epoch,target=ticket.area,layout=ticket.layout,generation=ticket.serial}}));
                    Check(ready.epoch==7&&ticket.Matches(ready.areaIdentity)&&cohorts.Acknowledge(who,ready.areaIdentity),"ready wire identity");
                    Check(cohorts.Commit("fixture-"+who)&&run.MoveMembers(layout.world,area.id,new[]{who}),"commit "+area.id);
                    Check(!cohorts.Accepts(who,old)&&cohorts.Accepts(who,ticket),"old area input refused");
                    var command=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="expedition-command",epoch=7,areaIdentity=ticket,expeditionCommand=new ExpeditionCommandRequest{areaIdentity=ticket}}));
                    Check(command.epoch==7&&ticket.Matches(command.areaIdentity)&&ticket.Matches(command.expeditionCommand.areaIdentity),"command wire identity");
                    var sim=registry.Find(area.id).Arena;var frame=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=7,frame=sim.NetworkFrame()})).frame;
                    var arriving=sim.Pilots.Single(p=>p.Seat.id==Seat(who));
                    var arrival=NativeArena.Vec(area.doors.Single().entries[0].Position)+Vector3.up*(NativeLocomotionRules.HalfHeight+.04f);
                    Check((arriving.Body.position-arrival).sqrMagnitude<.0001f&&(arriving.Body.transform.position-arrival).sqrMagnitude<.0001f,"physics and transform arrive inside "+area.id);
                    Check(frame.pilots.Any(p=>p.id==Seat(who))&&!frame.pilots.Any(p=>p.id==1),"destination frame owns only local pilot");
                    Check(sim.Pve.Content.machines.Length>0&&sim.Pve.Content.entries.Length>0,"playable encounter services "+area.id);
                    var scene=EditorSceneManager.NewPreviewScene();NativeArena replica=null;
                    try
                    {
                        replica=new NativeArena(content,new NativeMatch(content),area.map){Replica=true,LocalId=Seat(who),ReplicaScene=scene};
                        Check(frame.Valid(content),"valid recipient frame");replica.ApplyFrame(frame,Seat(who));
                        var view=NativeExpeditionView.Build(sessions[area.id].Run,who,sessions[area.id].Sources);
                        replica.ApplyExpeditionCollision(view,area,json);
                        Check(replica.ExpeditionCollisionReady&&replica.Local!=null&&replica.Local.Seat.id==Seat(who)&&replica.Pve.Content.machines.Length==sim.Pve.Content.machines.Length,"guest area collision and content bound");
                        Check(hub.Arena.Pilots.Any(p=>p.Seat.id==1),"guest loading preserves host area");
                    }
                    finally{replica?.Dispose();EditorSceneManager.ClosePreviewScene(scene);}
                }
                Check(hub.Occupants==1&&hub.Arena.Pilots.Any(p=>p.Seat.id==1)&&registry.Count==4,"hub bystander remains with three occupied instances");
                var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
                var timer=System.Diagnostics.Stopwatch.StartNew();double worst=0;var samples=new List<double>();long packetBytes=0;long memoryBefore=GC.GetTotalMemory(true);
                try
                {
                    for(int tick=1;tick<=1800;tick++)
                    {
                        long start=timer.ElapsedTicks;
                        registry.Step(tick,dt=>sessions[layout.world].Tick(dt));
                        foreach(var area in layout.areas){registry.Find(area.id).Arena.Match.SynchronizeExpeditionClock(tick/60d);if(area.id!=layout.world)sessions[area.id].Tick(1d/60);}

                        // Catch a body left at hub coordinates falling outside any destination.
                        if(tick<=120)foreach(var area in layout.areas.Where(a=>a.role=="instance"))
                        {
                            var sim=registry.Find(area.id).Arena;var p=sim.Pilots.Single(x=>!x.Seat.bot);
                            Check(p.Hp>0&&p.Body.position.y>=-.1f,"arrival stays alive above the entry floor: "+area.id+", tick="+tick+", position="+p.Body.position);
                        }
                        if(tick%3==0)foreach(var area in layout.areas){var frame=registry.Find(area.id).Arena.NetworkFrame();string packet=NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=7,frame=frame});packetBytes+=System.Text.Encoding.UTF8.GetByteCount(packet);Check(packet.Length<1048576,"per-area wire budget");}
                        double ms=(timer.ElapsedTicks-start)*1000d/System.Diagnostics.Stopwatch.Frequency;worst=Math.Max(worst,ms);if(tick>120)samples.Add(ms);
                    }
                }
                finally{Physics.simulationMode=mode;}
                Check(Math.Abs(run.Elapsed-30)<1e-7&&layout.areas.All(a=>Math.Abs(registry.Find(a.id).Arena.Time-30)<1e-5),"one clock across four occupied areas");
                foreach(var area in layout.areas.Where(a=>a.role=="instance"))Check(registry.Find(area.id).Arena.Minions.Count>0,"native horde spawned in "+area.id);
                foreach(var area in layout.areas){var r=registry.Find(area.id);Debug.Log("Split area "+area.id+": pilots="+r.Occupants+", mobs="+r.Arena.Minions.Count+", step ms="+r.StepMs.ToString("F2")+", physics ms="+r.PhysicsMs.ToString("F2")+", navigation ms="+r.NavigationMs.ToString("F2"));}
                foreach(var area in layout.areas.Where(a=>NativeZoneCombat.Active(a.map)))
                {
                    var scene=EditorSceneManager.NewPreviewScene();NativeArena guest=null;
                    try
                    {
                        guest=new NativeArena(content,new NativeMatch(content),area.map){Replica=true,ReplicaScene=scene};
                        registry.Find(area.id).Arena.ValidateZoneCombat(frame=>
                        {
                            var packet=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=7,frame=frame})).frame;
                            Check(packet.Valid(content),"zone event wire accepted");guest.ApplyFrame(packet,packet.pilots.First(p=>p.id!=900).id);
                            foreach(var m in packet.minions){var decoded=guest.Minions.Single(n=>n.Id==m.id);Check(decoded.Pve!=null&&decoded.Pve.Zone.arcAt==m.zone.arcAt&&decoded.Pve.Zone.pullAt==m.zone.pullAt&&decoded.Pve.Zone.chainCutAt==m.zone.chainCutAt&&decoded.Pve.Zone.grabTarget==m.zone.grabTarget,"guest applies zone enemy state");}
                            Check(guest.ZoneHazards?.clock==packet.pve.zone?.clock,"guest applies owning instance hazard clock");
                        });
                        registry.Find(area.id).Arena.ValidateZoneBossAuthority(frame=>
                        {
                            var packet=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=7,frame=frame})).frame;
                            Check(packet.Valid(content),"zone boss packet accepted");guest.ApplyFrame(packet,packet.pilots.First(p=>p.id!=900).id);
                            Check(guest.ZoneBoss!=null&&guest.ZoneBoss.actor==packet.pve.zoneBoss.actor&&guest.ZoneBoss.hitAt==packet.pve.zoneBoss.hitAt,"guest applies boss identity and contact clock");
                            Check(packet.pve.zoneBoss.archetype==NativeZoneBossRules.Station?guest.StationZoneBossView().hitAt==packet.pve.zoneBoss.hitAt:guest.IronworksZoneBossView().phase==packet.pve.zoneBoss.phase,"guest binds the matching presentation record");
                        });
                    }
                    finally{guest?.Dispose();EditorSceneManager.ClosePreviewScene(scene);}
                }
                Debug.Log("PASS native Expedition activation: four simultaneous areas, subset ownership, independent encounters, shared clock, stale input and ready/command codec. 1800 ticks ms="+timer.Elapsed.TotalMilliseconds.ToString("F1")+", worst="+worst.ToString("F1")+", p95="+samples.OrderBy(ms=>ms).ElementAt((int)(samples.Count*.95)).ToString("F1")+", raw packet bytes="+packetBytes+", managed memory delta="+(GC.GetTotalMemory(false)-memoryBefore)+". Automated fixture; not a human multiplayer playtest.");
            }
        }
    }
}
