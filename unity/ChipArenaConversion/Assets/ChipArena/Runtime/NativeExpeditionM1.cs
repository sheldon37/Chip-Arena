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
            if(account?.Developer!=true||Online||server!=null||dedicated){networkStatus="Expedition M1 requires a signed-in DEV account and a local session.";return;}
            try
            {
                EndPreview();ClearReplay();ResetExpeditionNetwork();arena?.Dispose();arena=null;match=null;expeditionDirector=null;expeditionError=null;
                Time.timeScale=1;endFlow.Reset();presentation.ResetPresentation();
                var layoutText=Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName)?.text;
                expeditionM1Layout=NativeExpeditionLayout.Parse(layoutText);
                expeditionHorde=JsonUtility.FromJson<NativePveContent>(Resources.Load<TextAsset>("PveContent").text);
                match=new NativeMatch(content){Cooperative=true,Teams=true,ShowIntros=false,FinalKillcams=false};
                var seat=SanitizedSeat(1,Profile(),account.Token);seat.team=0;seat.devCommandsRan=true;match.Seats.Add(seat);
                match.Seats.Add(new NativeSeat{id=900,name="TEST Expedition Siren",spec="siren",bot=true,team=1,AuthorityBoss=true});
                arena=new NativeArena(content,match,expeditionM1Layout.Area(expeditionM1Layout.world).map){LocalId=1};
                if(!match.Start())throw new InvalidOperationException("Expedition arena could not start.");
                expeditionM1Layout=arena.LoadExpeditionM1Layout(layoutText,expeditionHorde);
                // Explicit provisional M1 catalog: Ward Zero's 12-kill quota from the original Expedition preview.
                var run=NativeExpedition.Create("m1-"+Guid.NewGuid().ToString("N"),expeditionM1Layout.world,account.Access.AccountId,seat.spec,NativeMatch.NewSeed(),
                    expeditionM1Layout.instances.Select(id=>new ExpeditionInstance{id=id,target=12}).ToArray());
                var session=CreateExpeditionSession(run);
                expeditionDirector=new ExpeditionDirector(session,expeditionM1Layout,id=>arena?.Pilots.FirstOrDefault(p=>p.Seat.access?.AccountId==id) is NativePilot p?NativeArena.Vec(p.Body.position-Vector3.up*NativeLocomotionRules.HalfHeight):(V3?)null,
                    (a,b)=>arena.ExpeditionVisible(a,b),100,100,60);
                // These interactions require separate economic/combat binding. Do not offer fake evidence.
                session.CanClaim=(_,__)=>false;session.CanPurchase=(_,__)=>false;session.ReprintEvidence=(_,__)=>null;
                BindExpedition(session);expeditionLoadedGeneration=-1;expeditionDirector.BeginAreaLoad();
                menu=false;pending=new InputFrame();sprint=false;pitch=12;expeditionScreenOpen=false;
                if(!ExpeditionM1Load())return;Notice("DEV Expedition M1 · F at doors · J for exit, Continue or extraction. Local only; loot/reprint unavailable.");
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
                    var old=arena.Local;float hp=old.Hp,mana=old.Mana,stamina=old.Stamina;int ammo=old.Ammo;
                    arena.ExpeditionMap(area.map);arena.BindExpeditionCombat(expeditionDirector,area,expeditionHorde);
                    arena.Local.Hp=hp;arena.Local.Mana=mana;arena.Local.Stamina=stamina;arena.Local.Ammo=ammo;
                    Physics.SyncTransforms();
                    if(NativeExpeditionLayout.Compatibility(Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName).text,content)!=ticket.layout)throw new InvalidOperationException("Layout changed during load.");
                    if(ReportExpeditionAreaLoaded(ticket).state!=ExpeditionCommandState.Pending)throw new InvalidOperationException("Area readiness was rejected.");
                    expeditionLoadedGeneration=barrier.generation;
                }
                if(!expeditionDirector.CompleteAreaLoad((area,placements)=>placements.Count==1&&placements.All(p=>arena.ExpeditionPlace(p.Key,p.Value))))throw new InvalidOperationException("Party placement failed; area remains frozen.");
                expeditionDirector.Step();ExpeditionView=NativeExpeditionView.Build(expeditionSession.Run,account.Access.AccountId,expeditionSession.Sources);
                yaw=arena.Local.Yaw;generation=arena.Generation;visualClock.Reset(match.Time);presentation.ResetPresentation();NativeLighting.Apply(content.Map(arena.MapId),cameraView);return true;
            }
            catch(Exception e){Debug.LogException(e);expeditionError=e.Message;networkStatus="Expedition stopped: "+e.Message;Notice(networkStatus+" · Escape to leave and restart.");return false;}
        }
        private bool ExpeditionM1BeforeStep()
        {
            if(!ExpeditionM1)return true;if(!ExpeditionM1Load())return false;
            var s=expeditionSession.Run.Snapshot();
            if(s.phase==ExpeditionPhase.Ended||s.members.All(m=>m.extracted)||!s.members.Any(m=>m.alive&&!m.extracted))return false;
            arena.ExpeditionSyncClosures();
            if(pending.Interact&&s.phase==ExpeditionPhase.Town)
            {
                foreach(var door in expeditionM1Layout.Area(s.area).doors)if(expeditionDirector.EnterDoor(s.host,door.anchor)){pending=new InputFrame();return false;}
            }
            expeditionDirector.TryBeginFinal();return true;
        }
        private void ExpeditionM1AfterStep()
        {
            if(!ExpeditionM1)return;
            expeditionSession.Tick(1d/60);arena.ExpeditionSyncClosures();
            ExpeditionView=NativeExpeditionView.Build(expeditionSession.Run,account.Access.AccountId,expeditionSession.Sources);
        }
    }
}
