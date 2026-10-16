using System;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private ExpeditionDirector expeditionDirector;private ExpeditionLayout expeditionM1Layout;private NativePveContent expeditionHorde;
        private string expeditionError;private long expeditionLoadedGeneration=-1;
        private bool ExpeditionM1=>expeditionDirector!=null;
        private void StartExpeditionM1()
        {
            if(server!=null||dedicated)return;
            var party=peers.Where(p=>p.Seat!=null&&p.Connection?.Closed==false).ToArray();
            var host=SanitizedSeat(1,Profile(),account?.Token);
            var seats=new[]{host}.Concat(party.Select(p=>p.Seat)).ToArray();
            string refusal=NativeExpeditionParty.Validate(seats.Select(s=>s.access?.AccountId),seats.All(s=>s.access!=null&&s.access.Expires>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),GuestsReady,party.All(p=>p.ExpeditionProtocol==NativeExpeditionParty.Protocol));
            if(refusal!=null){networkStatus=refusal;Notice(refusal);return;}
            prefs.pve=true;prefs.expedition=true;prefs.bots=0;
            try
            {
                EndPreview();ClearReplay();ResetExpeditionNetwork();arena?.Dispose();arena=null;match=null;expeditionDirector=null;expeditionError=null;
                Time.timeScale=1;endFlow.Reset();presentation.ResetPresentation();
                var layoutText=Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName)?.text;
                expeditionM1Layout=NativeExpeditionLayout.Parse(layoutText);
                expeditionHorde=JsonUtility.FromJson<NativePveContent>(Resources.Load<TextAsset>("PveContent").text);
                match=new NativeMatch(content){Cooperative=true,Teams=true,ShowIntros=false,FinalKillcams=false};
                foreach(var member in seats){member.team=0;member.chips.Clear();match.Seats.Add(member);}var seat=host;
                networkEpoch++;inputSequence=0;
                match.Seats.Add(new NativeSeat{id=900,name="TEST Expedition Siren",spec="siren",bot=true,team=1,AuthorityBoss=true});
                arena=new NativeArena(content,match,expeditionM1Layout.Area(expeditionM1Layout.world).map){LocalId=1};
                if(!match.Start())throw new InvalidOperationException("Expedition arena could not start.");
                expeditionM1Layout=arena.LoadExpeditionM1Layout(layoutText,expeditionHorde);
                // Explicit provisional M1 catalog: Ward Zero's 12-kill quota from the original Expedition preview.
                var run=NativeExpedition.Create("m1-"+Guid.NewGuid().ToString("N"),expeditionM1Layout.world,account.Access.AccountId,seat.spec,NativeMatch.NewSeed(),
                    expeditionM1Layout.instances.Select(id=>new ExpeditionInstance{id=id,target=12}).ToArray());
                foreach(var guest in party)if(!run.Join(guest.Seat.access.AccountId,guest.Seat.spec))throw new InvalidOperationException("Party member could not join the run.");
                var session=CreateExpeditionSession(run);
                expeditionDirector=new ExpeditionDirector(session,expeditionM1Layout,id=>arena?.Pilots.FirstOrDefault(p=>p.Seat.access?.AccountId==id) is NativePilot p?NativeArena.Vec(p.Body.position-Vector3.up*NativeLocomotionRules.HalfHeight):(V3?)null,
                    (a,b)=>arena.ExpeditionVisible(a,b),100,100,60);
                // Reprint evidence is measured by the same combat authority for every seat.
                session.ReprintEvidence=(payer,target)=>arena.ExpeditionReprintEvidence(payer,target);
                BindExpedition(session);if(Hosting)FlushLobby();expeditionLoadedGeneration=-1;expeditionDirector.BeginAreaLoad();
                menu=false;pending=new InputFrame();sprint=false;pitch=12;expeditionScreenOpen=false;
                ExpeditionM1Load();Notice("Expedition · F to interact · J for party votes or extraction · C for your chip choices.");
            }
            catch(Exception e){Debug.LogException(e);expeditionError=e.Message;networkStatus="Expedition M1 refused: "+e.Message;arena?.Dispose();arena=null;match=null;expeditionDirector=null;ResetExpeditionNetwork();menu=true;}
        }
        private bool ExpeditionM1Load()
        {
            if(!ExpeditionM1)return true;
            var s=expeditionSession.Run.Snapshot();var barrier=expeditionDirector.Barrier;
            if(!barrier.active)return expeditionError==null;
            if(expeditionError!=null)return false;
            try
            {
                if(expeditionLoadedGeneration!=barrier.generation)
                {
                    var view=NativeExpeditionView.Build(expeditionSession.Run,account.Access.AccountId,expeditionSession.Sources);
                    var ticket=ExpeditionAreaReady.Capture(view);var area=expeditionM1Layout.Area(barrier.target);
                    var vitals=arena.Pilots.Where(p=>!p.Seat.bot).ToDictionary(p=>p.Seat.id,p=>(p.Hp,p.Mana,p.Stamina,p.Ammo));
                    arena.ExpeditionMap(area.map);arena.BindExpeditionCombat(expeditionDirector,area,expeditionHorde);
                    foreach(var p in arena.Pilots)if(vitals.TryGetValue(p.Seat.id,out var v)){p.Hp=v.Hp;p.Mana=v.Mana;p.Stamina=v.Stamina;p.Ammo=v.Ammo;}
                    arena.RestoreExpeditionMembers();
                    Physics.SyncTransforms();
                    if(NativeExpeditionLayout.Compatibility(Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName).text,content)!=ticket.layout)throw new InvalidOperationException("Layout changed during load.");

                    expeditionLoadedGeneration=barrier.generation;
                }
                if(!StagePresentationReady||StagePresentation.Generation!=arena.Generation||StagePresentation.Map!=expeditionM1Layout.Area(barrier.target).map)return false;
                var readyTicket=ExpeditionAreaReady.Capture(NativeExpeditionView.Build(expeditionSession.Run,account.Access.AccountId,expeditionSession.Sources));
                if(ReportExpeditionAreaLoaded(readyTicket).state!=ExpeditionCommandState.Pending)throw new InvalidOperationException("Area readiness was rejected.");
                if(!expeditionDirector.CompleteAreaLoad((area,placements)=>arena.PlaceExpeditionParty(placements)))return false;
                expeditionDirector.Step();ExpeditionView=NativeExpeditionView.Build(expeditionSession.Run,account.Access.AccountId,expeditionSession.Sources);
                yaw=arena.Local.Yaw;generation=arena.Generation;visualClock.Reset(match.Time);presentation.ResetPresentation();NativeLighting.Apply(content.Map(arena.MapId),cameraView);return true;
            }
            catch(Exception e){Debug.LogException(e);expeditionError=e.Message;networkStatus="Expedition stopped: "+e.Message;Notice(networkStatus+" · Escape to leave and restart.");return false;}
        }
        private bool ExpeditionM1BeforeStep()
        {
            if(!ExpeditionM1)return true;if(!ExpeditionM1Load())return false;
            var s=expeditionSession.Run.Snapshot();
            if(s.phase==ExpeditionPhase.Ended||s.members.All(m=>m.extracted)||!s.members.Any(m=>m.alive&&m.connected&&!m.extracted))return false;
            arena.ExpeditionSyncClosures();arena.SyncExpeditionInventory();
            expeditionDirector.TryBeginFinal();return true;
        }
        private void ExpeditionM1AfterStep()
        {
            if(!ExpeditionM1||arena.Pve?.Holding==true)return;
            expeditionSession.Tick(1d/60);arena.ExpeditionSyncClosures();
            ExpeditionView=NativeExpeditionView.Build(expeditionSession.Run,account.Access.AccountId,expeditionSession.Sources);
        }
    }
}
