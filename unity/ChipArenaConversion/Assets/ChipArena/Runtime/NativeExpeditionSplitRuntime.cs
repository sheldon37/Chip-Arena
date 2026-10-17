using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private ExpeditionAreaRegistry<NativeExpeditionAreaRuntime> splitAreas;
        private ExpeditionCohorts splitCohorts;
        private NativeExpeditionAreaTransfers splitTransfers;
        private readonly Dictionary<string,ExpeditionSession> splitSessions=new Dictionary<string,ExpeditionSession>();
        private readonly Dictionary<string,ExpeditionCommandRouter> splitRouters=new Dictionary<string,ExpeditionCommandRouter>();
        private int splitLoop;private NativeSeat[] splitSeats;private NativeExpedition splitRun;private long splitTick,splitReturn;
        private bool ExpeditionSplit=>splitAreas!=null;
        private bool SplitLocalLoading=>ExpeditionSplit&&arena?.Local==null;
        private NativeArena SplitPilotArena(string who)=>splitCohorts==null?null:splitAreas.Find(splitCohorts.Member(who)?.Area)?.Arena;
        private int SplitSeat(string who)=>splitSeats?.FirstOrDefault(s=>s.access?.AccountId==who)?.id??0;
        private void DisposeSplitAreas()
        {
            if(splitAreas==null)return;
            var old=splitAreas;splitAreas=null;old.Dispose();arena=null;match=null;
            splitCohorts=null;splitTransfers=null;splitRun=null;splitSeats=null;splitSessions.Clear();splitRouters.Clear();
        }
        private void StartSplitAreas(ExpeditionSession initial,NativeSeat[] seats)
        {
            // The validated loader has already checked each map's native geometry.
            arena.Dispose();arena=null;match=null;splitRun=initial.Run;splitSeats=seats;splitTick=splitReturn=0;splitLoop=0;
            splitRun.EnableAreas();
            splitCohorts=new ExpeditionCohorts(splitRun.PartySnapshot().run,splitRun.PartySnapshot().epoch,expeditionM1Layout.compatibility,expeditionM1Layout.areas.Select(a=>a.id));
            foreach(var seat in seats)splitCohorts.Add(seat.access.AccountId,expeditionM1Layout.world);
            splitAreas=new ExpeditionAreaRegistry<NativeExpeditionAreaRuntime>(expeditionM1Layout.areas.Select(a=>a.id),id=>
            {
                var area=expeditionM1Layout.Area(id);
                return new NativeExpeditionAreaRuntime(id,content,area.map,m=>
                {
                    m.Cooperative=m.Teams=true;m.ShowIntros=m.FinalKillcams=false;
                    if(id==expeditionM1Layout.world)m.Seats.AddRange(seats);
                    m.Seats.Add(new NativeSeat{id=900,name="Siren",spec="siren",bot=true,team=1,AuthorityBoss=true});
                });
            });
            foreach(var area in expeditionM1Layout.areas)
            {
                var runtime=splitAreas.Acquire(area.id);var sim=runtime.Arena;sim.LocalId=1;
                var session=new ExpeditionSession(splitRun.ForArea(area.id),content);
                session.Sources.Seat=SplitSeat;session.Sources.DisplayName=id=>seats.FirstOrDefault(p=>p.access?.AccountId==id)?.name;
                var director=new ExpeditionDirector(session,expeditionM1Layout,id=>sim.Pilots.FirstOrDefault(p=>p.Seat.id==SplitSeat(id)) is NativePilot p?NativeArena.Vec(p.Body.position-Vector3.up*NativeLocomotionRules.HalfHeight):(V3?)null,sim.ExpeditionVisible,100,100,60);
                session.ReprintEvidence=sim.ExpeditionReprintEvidence;session.Run.ReprintPlacement=SplitReprint;
                runtime.Ready(a=>{a.Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,cooperative=true,teams=true});a.Match.HoldResolution=()=>true;a.BindExpeditionCombat(director,area,expeditionHorde);});
                runtime.SetOccupants(sim.Pilots.Count(p=>!p.Seat.bot));
                splitSessions.Add(area.id,session);splitRouters.Add(area.id,new ExpeditionCommandRouter(session));
            }
            var hub=splitAreas.Find(expeditionM1Layout.world).Arena;
            var assignments=new Dictionary<string,ExpeditionPoint>();for(int i=0;i<seats.Length;i++)assignments.Add(seats[i].access.AccountId,expeditionM1Layout.Area(expeditionM1Layout.world).slots[i]);
            if(!hub.PlaceExpeditionParty(assignments))throw new InvalidOperationException("Hub placement refused.");
            splitTransfers=new NativeExpeditionAreaTransfers(splitAreas,expeditionM1Layout,splitCohorts,SplitSeat);
            BindSplitLocal(expeditionM1Layout.world);
            expeditionSession=splitSessions[expeditionM1Layout.world];expeditionLocal=new SplitLocalCommands(this,account.Access.AccountId);
            ExpeditionView=SplitView(account.Access.AccountId);expeditionNextSend=0;
        }
        private bool SplitReprint(string payer,string target)
        {
            var from=splitCohorts.Member(target);var to=splitCohorts.Member(payer);
            if(from==null||to==null||from.Pending!=null||!from.Connected||to.Area!=expeditionM1Layout.world)return false;
            var destination=splitAreas.Find(to.Area);var source=splitAreas.Find(from.Area);
            var slot=expeditionM1Layout.Area(to.Area).slots.FirstOrDefault(destination.Arena.TransferSlotClear);
            if(slot==null||!destination.Arena.ReprintFrom(source.Arena,target,slot))return false;
            if(!splitCohorts.Recover(target,to.Area)||!splitRun.MoveMembers(from.Area,to.Area,new[]{target}))throw new InvalidOperationException("Reprint membership failed.");
            source.SetOccupants(source.Arena.Pilots.Count(p=>!p.Seat.bot));destination.SetOccupants(destination.Arena.Pilots.Count(p=>!p.Seat.bot));return true;
        }
        private void BindSplitLocal(string area)
        {
            var sim=splitAreas.Find(area).Arena;if(ReferenceEquals(sim,arena))return;
            splitAreas.Present(area);arena=sim;match=sim.Match;expeditionDirector=splitSessions[area].Sources.Director;
            generation=arena.Generation;pending=new InputFrame();sprint=false;presentation.ResetPresentation();visualClock.Reset(match.Time);
            yaw=arena.Local?.Yaw??yaw;NativeLighting.Apply(content.Map(arena.MapId),cameraView);
        }
        private ExpeditionView AreaView(string area,string who)
        {
            var session=splitSessions[area];var view=NativeExpeditionView.Build(session.Run,who,session.Sources);
            view.closures=view.closures.Where(c=>c.area==area).ToArray();view.bossArrivals=view.bossArrivals.Where(c=>c.area==area).ToArray();
            string prefix=view.loop+":"+area+":";view.loot=view.loot.Where(r=>r.id.StartsWith(prefix,StringComparison.Ordinal)).ToArray();view.dice=view.dice.Where(d=>d.boss.StartsWith(prefix,StringComparison.Ordinal)).ToArray();
            var own=splitCohorts.Member(who);
            if(own!=null&&own.Area!=area)view.mine=NativeExpeditionView.Build(splitSessions[own.Area].Run,who,splitSessions[own.Area].Sources).mine;
            view.members=splitCohorts.Members.Select(m=>NativeExpeditionView.Build(splitSessions[m.Area].Run,null,splitSessions[m.Area].Sources).Member(m.Account)).Where(m=>m!=null).ToArray();
            return view;
        }
        private ExpeditionAreaRecipient SplitRecipient(string who)=>ExpeditionAreaProjection.Build(who,splitCohorts,AreaView,splitTransfers.Joins);
        private ExpeditionView SplitView(string who)
        {
            var value=SplitRecipient(who);value.Public.viewer=who;value.Public.mine=value.Private.mine;return value.Public;
        }
        private void SplitFixedFrame()
        {
            if(expeditionError!=null)return;
            var state=splitRun.PartySnapshot();if(state.phase==ExpeditionPhase.Ended||!state.members.Any(m=>m.alive&&m.connected&&!m.extracted)){ExpeditionView=SplitView(account.Access.AccountId);return;}
            try
            {
                var own=splitCohorts.Member(account.Access.AccountId);
                // Present the ticket destination while the actor remains reserved at source.
                // Local readiness requires the same collision and presentation gate as a guest.
                BindSplitLocal(own.Ticket.area);
                if(!own.Ready&&splitAreas.Find(own.Ticket.area)?.Load==ExpeditionRuntimeLoad.Ready&&ReferenceEquals(stageSeen,arena)&&StagePresentationReady&&StagePresentation.Generation==arena.Generation)splitCohorts.Acknowledge(own.Account,own.Ticket);
                var before=splitCohorts.Members;
                splitTransfers.Step(splitRun.Elapsed,id=>splitRun.PartySnapshot().members.FirstOrDefault(m=>m.account==id),id=>!splitRun.PartySnapshot().instances.Single(i=>i.id==id).cleared);
                foreach(var group in splitCohorts.Members.Where(m=>before.Single(p=>p.Account==m.Account).Area!=m.Area).GroupBy(m=>(source:before.Single(p=>p.Account==m.Account).Area,destination:m.Area)))
                    if(!splitRun.MoveMembers(group.Key.source,group.Key.destination,group.Select(m=>m.Account).ToArray()))throw new InvalidOperationException("Transfer membership commit failed.");
                foreach(var m in splitCohorts.Members)
                {
                    splitRun.HoldMembers(new[]{m.Account},m.Pending!=null||!m.Ready);
                    var p=SplitPilotArena(m.Account)?.Pilots.FirstOrDefault(p=>p.Seat.id==SplitSeat(m.Account));
                    if(p!=null&&p.ExpeditionTransferHeld!=(m.Pending!=null||!m.Ready))SplitPilotArena(m.Account).HoldTransferPilot(p,m.Pending!=null||!m.Ready);
                }
                own=splitCohorts.Member(account.Access.AccountId);BindSplitLocal(own.Ticket.area);
                if(before.Single(m=>m.Account==own.Account).Area!=own.Area&&arena.Local!=null){yaw=arena.Local.Yaw;pending=new InputFrame();}
                splitAreas.Find(own.Area).Input=own.Pending==null&&own.Ready&&StagePresentationReady?pending:new InputFrame();
                splitAreas.Step(++splitTick,dt=>splitSessions[expeditionM1Layout.world].Tick(dt));
                double clock=splitTick/60d;
                foreach(var area in expeditionM1Layout.areas)
                {
                    var runtime=splitAreas.Find(area.id);runtime.Arena.Match.SynchronizeExpeditionClock(clock);
                    var session=splitSessions[area.id];if(area.id!=expeditionM1Layout.world)session.Tick(1d/60);
                    runtime.Arena.ExpeditionSyncClosures();runtime.Arena.SyncExpeditionInventory();
                    var returning=session.Run.DueReturns().Where(a=>splitCohorts.Member(a)?.Pending==null).ToArray();
                    if(returning.Length>0)splitTransfers.Begin("return:"+(++splitReturn),area.id,expeditionM1Layout.world,null,returning);
                }
                if(splitRun.PartySnapshot().loop!=splitLoop)ResetSplitLoop();
                splitSessions[expeditionM1Layout.world].Sources.Director.TryBeginFinal();
                ExpeditionView=SplitView(account.Access.AccountId);pending=pending.HeldOnly();
            }
            catch(Exception e){Debug.LogException(e);expeditionError=e.Message;Notice("Expedition stopped: "+e.Message);}
        }
        private void ResetSplitLoop()
        {
            splitLoop=splitRun.PartySnapshot().loop;
            foreach(var area in expeditionM1Layout.areas)
            {
                var sim=splitAreas.Find(area.id).Arena;
                var vitals=sim.Pilots.Where(p=>!p.Seat.bot).ToDictionary(p=>p.Seat.id,p=>(p.Hp,p.Mana,p.Stamina,p.Ammo));
                sim.ExpeditionMap(area.map);sim.BindExpeditionCombat(splitSessions[area.id].Sources.Director,area,expeditionHorde);
                foreach(var p in sim.Pilots)if(vitals.TryGetValue(p.Seat.id,out var v)){p.Hp=v.Hp;p.Mana=v.Mana;p.Stamina=v.Stamina;p.Ammo=v.Ammo;}
                sim.RestoreExpeditionMembers();
            }
            var hub=splitAreas.Find(expeditionM1Layout.world).Arena;int index=0;
            foreach(var m in splitCohorts.Members){splitCohorts.Recover(m.Account,expeditionM1Layout.world);hub.ExpeditionPlace(m.Account,expeditionM1Layout.Area(expeditionM1Layout.world).slots[index++]);}
            presentation.ResetPresentation();
        }
        private NativeGameFrame SplitFrame(string who)
        {
            var member=splitCohorts.Member(who);var target=splitAreas.Find(member.Ticket.area).Arena;
            var frame=target.NetworkFrame();frame.areaIdentity=member.Ticket;
            if(member.Area!=member.Ticket.area)
            {
                // A non-simulated local preview lets this recipient load destination collision
                // while the authoritative pilot remains reserved at the source.
                var source=SplitPilotArena(who).NetworkFrame();int id=SplitSeat(who);
                var p=source.pilots.Single(p=>p.id==id);var pose=source.snapshot.Actors.Single(p=>p.Id==id);
                var point=expeditionM1Layout.Area(member.Ticket.area).role=="world"?expeditionM1Layout.Area(member.Ticket.area).slots[0]:expeditionM1Layout.Area(member.Ticket.area).doors.Single().entries[0];
                p.position=NativeArena.Vec(point.Position)+Vector3.up*NativeLocomotionRules.HalfHeight;p.velocity=Vector3.zero;pose.Position=NativeArena.Vec(p.position);pose.Velocity=new V3();
                frame.seats=frame.seats.Concat(source.seats.Where(s=>s.id==id)).ToArray();frame.pilots=frame.pilots.Concat(new[]{p}).ToArray();frame.snapshot.Actors=frame.snapshot.Actors.Concat(new[]{pose}).ToArray();
            }
            return frame;
        }
        private void SplitNetworkStep()
        {
            if(Time.unscaledTime<expeditionNextSend)return;expeditionNextSend=Time.unscaledTime+.05f;
            ExpeditionView=SplitView(account.Access.AccountId);
            foreach(var peer in peers.Where(p=>p.Connection?.Closed==false&&p.Seat?.access!=null))
            {
                var who=peer.Seat.access.AccountId;if(splitCohorts.Member(who)==null)continue;
                var view=SplitRecipient(who);var frame=SplitFrame(who);
                peer.Connection.Send(NativeNetCodec.Encode(new NativeNetMessage{type="expedition-public",epoch=networkEpoch,generation=frame.generation,expeditionPublic=view.Public}));
                peer.Connection.Send(NativeNetCodec.Encode(new NativeNetMessage{type="expedition-private",expeditionPrivate=view.Private}));
                var packet=NativeFrameBudget.Encode(frame,networkEpoch);if(packet==null)throw new InvalidOperationException("Area snapshot exceeded its budget.");peer.Connection.SendEncoded(packet,true);
            }
        }
        private bool SplitHostMessage(NativePeer peer,NativeNetMessage message)
        {
            var who=peer.Seat?.access?.AccountId;var member=splitCohorts.Member(who);
            if(message.type=="expedition-area-ready")
            {
                var t=message.expeditionAreaReady;
                if(member!=null&&peer.Changes.Allow(Time.unscaledTime)&&message.areaIdentity?.Matches(member.Ticket)==true&&t!=null&&t.run==member.Ticket.run&&t.epoch==member.Ticket.epoch&&t.target==member.Ticket.area&&t.layout==member.Ticket.layout&&t.generation==member.Ticket.serial)splitCohorts.Acknowledge(who,member.Ticket);
                return true;
            }
            if(message.type=="input"||message.type=="expedition-command")
            {
                if(member==null||peer.Seat.access.Expires<=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()||message.epoch!=networkEpoch||!splitCohorts.Accepts(who,message.areaIdentity))return true;
                var sim=SplitPilotArena(who);
                if(message.type=="input"){if(message.generation==sim.Generation)sim.AcceptInput(peer.Seat.id,message.input);return true;}
                if(!peer.Changes.Allow(Time.unscaledTime)||!member.Ticket.Matches(message.expeditionCommand?.areaIdentity))return true;
                var reply=splitRouters[member.Area].Execute(who,message.expeditionCommand);peer.Connection.Send(NativeNetCodec.Encode(new NativeNetMessage{type="expedition-reply",expeditionReply=reply}));return true;
            }
            // The area protocol owns readiness and combat commands. Legacy PvE / stage
            // commands cannot address the host's displayed arena on behalf of another area.
            return message.type=="dev-command"||message.type=="stage-ready"||message.type=="pve-pick"||message.type=="pve-open"||message.type=="pve-close"||message.type=="chip";
        }
        private sealed class SplitLocalCommands:IExpeditionCommands
        {
            private readonly ConversionGame game;public string Viewer{get;}
            public SplitLocalCommands(ConversionGame game,string viewer){this.game=game;Viewer=viewer;}
            private ExpeditionSession Session=>game.splitSessions[game.splitCohorts.Member(Viewer).Area];
            public string Blocked(ExpeditionCommand command)=>(game.splitCohorts.Member(Viewer).Pending!=null||!game.splitCohorts.Member(Viewer).Ready)?"Loading area":new ExpeditionHostCommands(Session.Run,Viewer,Session.Hooks()).Blocked(command);
            public ExpeditionCommandResult Send(ExpeditionCommand command)=>Blocked(command) is string why?ExpeditionCommandResult.Of(ExpeditionCommandState.Refused,why):Session.Execute(Viewer,command);
        }
    }
}
