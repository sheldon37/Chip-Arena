using System;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The stage loading screen's wiring (Core NativeStageLoading; owner 2026-10-15: "Introduce loading on stages"). A load
    /// begins two ways. A user's start or return from a menu (Start Match, Start Boss Fight, Start Sandbox, DEV Expedition
    /// M1, Restart, Return to Main Menu) is requested: the shutters close over the menu first and the action runs under them
    /// (RequestStage), so the stage's build never shows. Any other stage change is observed in LateUpdate and covered at once:
    /// a guest's first frame of a match, a match the end flow restarts, a map swap (an Expedition area, a new set's random
    /// map), an Expedition area barrier, a return to the menu. Behind the closed screen the presentation preloads step by step
    /// (Core StagePreloadPlan): the stage's kits (ArenaPresentation.PrewarmStage), two rendered frames, every shader once
    /// (NativeShaderWarmup) and the effects bank's queue. The screen opens when the preload is done and the game allows:
    /// at once on live play, a refused start or an error; after a game hold (an area barrier, Astra's StageLoadGate) up to
    /// its timeout; never past the clock's budget (NativeStageLoading.Budget), so the countdown and intros still play.
    /// StagePresentation is the readiness a client reports to an authority's load gate. No simulation, authority or
    /// network state is changed here; the deferred action is the same call the button made, a moment later.
    /// </summary>
    public sealed partial class ConversionGame
    {
        private readonly NativeStageLoading stageLoad=new NativeStageLoading();
        private readonly StagePreloadPlan stagePlan=new StagePreloadPlan();
        private readonly StageLoadView stageView=new StageLoadView();
        /// <summary>The authority's stage-load gate as this client sees it (Astra fills it from the network); the screen holds
        /// while it is Waiting and shows its Ready of Required. Clear without a gate.</summary>
        internal readonly StageLoadGate StageGate=new StageLoadGate();
        private NativeShaderWarmup shaderWarm;
        private Action stageAction;private NativeArena stageFrom;private bool stageActed,stageFailed;
        private NativeArena stageSeen;private string stageSeenMap;private bool stageSeenAny,stageBarrierSeen;
        private int stageFrames,stageSerial=-1,stageAudioStart=-1;private float stageStartedAt;private StageLoadPhase stagePhaseWas;
        private StageReadiness stageReadiness;

        /// <summary>The local presentation's readiness for the stage the arena holds (Astra: report it to the authority's gate for
        /// this epoch and map). Ready once the preload behind the loading screen finished, or the screen opened early.</summary>
        internal StageReadiness StagePresentation=>stageReadiness;
        /// <summary>The arena's current map is presented here: its load finished (or opened early) for this map.</summary>
        internal bool StagePresentationReady=>arena!=null&&stageReadiness.Ready&&stageReadiness.Map==arena.MapId&&stageReadiness.Epoch==networkEpoch;
        /// <summary>The loading screen is up (it holds the GUI's input while it closes and while it is closed).</summary>
        internal bool StageLoadingShown=>stageLoad.Visible;
        private bool StageInputHeld=>stageLoad.BlocksInput;

        /// <summary>
        /// A user's start or return from a menu: the loading screen closes first and <paramref name="action"/> runs under it
        /// (StageLoadingFrame), the same call the button made. A dedicated host or a fatal error runs it at once.
        /// </summary>
        private void RequestStage(StageLoadKind kind,Action action)
        {
            if(action==null)return;if(dedicated||cameraView==null||error.Length>0){action();return;}
            stageLoad.Request(kind,prefs.reducedEffects);stageAction=action;stageActed=false;StageBegan();
        }
        private StageLoadKind StageKindOf(NativeArena a)
        {
            if(a==null)return StageLoadKind.Return;if(ExpeditionM1||LiveExpedition!=null)return StageLoadKind.Expedition;
            if(a.Match?.Cooperative==true||a.Pve!=null)return StageLoadKind.Boss;if(a.Sandbox)return StageLoadKind.Sandbox;
            return a.Replica||Online?StageLoadKind.Online:StageLoadKind.Match;
        }
        // A new load (a new serial): the preload, the view and the readiness start over.
        private void StageBegan()
        {
            if(stageLoad.Serial==stageSerial)return;stageSerial=stageLoad.Serial;StageRestart();stageStartedAt=Time.unscaledTime;
            stageFailed=false;stageFrom=null;if(!stageLoad.Requested){stageAction=null;stageActed=false;}
        }
        private void StageRestart()
        {
            stagePlan.Reset(stageLoad.Kind!=StageLoadKind.Return);stageView.Begin(stageLoad.Kind,stageLoad.Serial);stageFrames=0;stageAudioStart=-1;stageReadiness=default;
        }
        // A stage change seen this frame: a closing or closed screen carries on (the stage it waits for changed, so its preload
        // starts over); otherwise the screen covers at once.
        private void StageObserved(StageLoadKind kind)
        {
            bool covering=stageLoad.Phase==StageLoadPhase.Cover||stageLoad.Phase==StageLoadPhase.Hold;
            stageLoad.Cover(kind,prefs.reducedEffects);if(covering)StageRestart();else StageBegan();
        }
        private void StageObserve()
        {
            var a=arena;string map=a?.MapId;bool barrier=LiveExpedition?.areaBarrier?.active==true;
            if(!stageSeenAny){stageSeenAny=true;stageSeen=a;stageSeenMap=map;stageBarrierSeen=barrier;return;}
            if(!ReferenceEquals(a,stageSeen))StageObserved(StageKindOf(a));
            else if(a!=null&&!string.Equals(map,stageSeenMap,StringComparison.Ordinal))StageObserved(ExpeditionM1||LiveExpedition!=null?StageLoadKind.Area:StageLoadKind.Change);
            else if(barrier&&!stageBarrierSeen)StageObserved(StageLoadKind.Area);
            stageSeen=a;stageSeenMap=map;stageBarrierSeen=barrier;
        }

        /// <summary>LateUpdate's last step, even when the frame before it threw: the loading screen never sticks on an error, it
        /// drops (the error screen or the notice shows what happened).</summary>
        private void StageLoadingStep()
        {
            try{StageLoadingFrame();}
            catch(Exception e){Debug.LogException(e);stageLoad.Cancel();stageAction=null;}
        }
        /// <summary>LateUpdate, after the frame is placed: the deferred action, the observers, the preload and the hold.</summary>
        private void StageLoadingFrame()
        {
            if(dedicated||content==null||cameraView==null||presentation==null||prefs==null)return;
            if(stageLoad.TakeAction())
            {
                stageFrom=arena;stageActed=true;var act=stageAction;stageAction=null;
                try{act?.Invoke();}catch(Exception e){Debug.LogException(e);stageFailed=true;}
            }
            StageObserve();
            if(!stageLoad.Visible){stagePhaseWas=StageLoadPhase.Idle;return;}
            if(stageLoad.Covered&&!stageLoad.ActionPending)StagePreload();
            var s=StageSignals();stageLoad.Step(Time.unscaledDeltaTime,s,stagePlan.Work);
            if(stagePlan.Done&&s.Stage&&!stageReadiness.Preloaded)StageReady(true);
            if(stageLoad.Phase==StageLoadPhase.Reveal&&stagePhaseWas!=StageLoadPhase.Reveal)
            {
                if(!stageReadiness.Ready&&s.Stage)StageReady(false);
                // The Expedition transfer screen stood behind this one: it does not come back once the barrier is down.
                if(LiveExpedition?.areaBarrier?.active!=true)expeditionTransferFade.Reset();
                // Opening onto the menu (a return, a refused start): its screen enters as the shutters open, not unseen under them.
                if(arena==null)menuScreenAt=Time.unscaledTime;
                Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"STAGE_LOADING serial={0} kind={1} map={2} reason={3} held={4:F2} preload={5} warmed={6}",
                    stageLoad.Serial,stageLoad.Kind,arena?.MapId??"menu",stageLoad.Reason,stageLoad.Held,stagePlan.Done?"done":stagePlan.Step.ToString(),shaderWarm?.Warmed??0);
            }
            stagePhaseWas=stageLoad.Phase;
        }
        private void StageReady(bool preloaded)=>stageReadiness=new StageReadiness{Serial=stageLoad.Serial,Map=arena?.MapId,Generation=arena?.Generation??-1,Epoch=networkEpoch,Ready=true,Preloaded=preloaded,Seconds=Time.unscaledTime-stageStartedAt};
        // The stage the load waits for is in place: the menu for a return; the arena (on the area's map for an area load).
        private bool StageInPlace=>stageLoad.Kind==StageLoadKind.Return?arena==null:arena!=null&&(stageLoad.Kind!=StageLoadKind.Area||expeditionMapReady);
        private StageLoadSignals StageSignals()
        {
            var k=stageLoad.Kind;bool back=k==StageLoadKind.Return;
            var s=new StageLoadSignals{Stage=StageInPlace,Ready=stagePlan.Done};
            s.Failed=stageFailed||error.Length>0||expeditionError!=null&&(k==StageLoadKind.Expedition||k==StageLoadKind.Area);
            if(stageActed)s.Failed|=back?arena!=null:arena==null||ReferenceEquals(arena,stageFrom);
            s.Waiting=!back&&(StageGate.Waiting||LiveExpedition?.areaBarrier?.active==true);
            bool intro=arena!=null&&IntroActive;
            s.Live=!back&&arena!=null&&match!=null&&match.Phase==NativePhase.Fight&&!intro&&arena.Pve?.Holding!=true;
            s.Budget=NativeStageLoading.Budget(back||arena==null||match==null?(NativePhase?)null:intro?NativePhase.Intro:match.Phase);
            return s;
        }
        // One preload step a frame, only behind the closed screen (Core StagePreloadPlan).
        private void StagePreload()
        {
            switch(stagePlan.Step)
            {
                case StagePreloadStep.Stage:
                    if(!StageInPlace)return;
                    if(stageLoad.Kind==StageLoadKind.Return)stageView.Returning(InRoom,Time.unscaledTime);
                    else stageView.Stage(content.Map(arena.MapId),Time.unscaledTime,StageAreaName());
                    // The kit builds in this same frame (the stage's own build frame), so even a load the game opens at once
                    // (live play) has its kit before the stage's first frame draws.
                    stagePlan.Next();if(stagePlan.Step==StagePreloadStep.Kit)goto case StagePreloadStep.Kit;return;
                case StagePreloadStep.Kit:
                {
                    long at=System.Diagnostics.Stopwatch.GetTimestamp();int kits=0;
                    try{kits=presentation.PrewarmStage(arena);}catch(Exception e){Debug.LogWarning("Stage prewarm skipped: "+e.Message);}
                    Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"STAGE_PRELOAD kit map={0} kits={1} ms={2:F1}",arena?.MapId,kits,HitchMs(at));
                    stagePlan.Next();stageFrames=0;return;
                }
                case StagePreloadStep.Frames:
                    stageFrames++;stagePlan.Set(stageFrames/(float)StagePreloadPlan.WarmFrames);if(stageFrames>=StagePreloadPlan.WarmFrames)stagePlan.Next();return;
                case StagePreloadStep.Shaders:
                {
                    if(shaderWarm==null)shaderWarm=new NativeShaderWarmup();int fog=NativeShaderWarmup.FogKey();
                    if(!shaderWarm.Needed(fog)){stagePlan.Next();return;}
                    shaderWarm.Step(cameraView,fog,NativeShaderWarmup.PerFrame);stagePlan.Set(shaderWarm.Progress);if(!shaderWarm.Needed(fog))stagePlan.Next();return;
                }
                case StagePreloadStep.Audio:
                {
                    int left=nativeAudio!=null?nativeAudio.PreloadPending:0;if(stageAudioStart<0)stageAudioStart=left;
                    if(left<=0){stagePlan.Next();return;}nativeAudio.Preload(24);left=nativeAudio.PreloadPending;
                    stagePlan.Set(stageAudioStart>0?1-left/(float)stageAudioStart:1);if(left<=0)stagePlan.Next();return;
                }
            }
        }
        // An Expedition area's own name (the barrier's target, as the transfer screen names it), else the map's.
        private string StageAreaName()
        {
            var b=LiveExpedition?.areaBarrier;if(b==null||string.IsNullOrEmpty(b.target))return null;
            return NativeExpeditionStates.AreaName(b.target);
        }

        /// <summary>OnGUI, before any screen: while the loading screen closes or is closed, clicks, keys and scrolls go nowhere.</summary>
        private void StageInputGate(){if(stageLoad.BlocksInput)BlockGuiInput();}
        /// <summary>Uses up this GUI event if it is a press, a key, a scroll or a drag (a release still lands, so no control stays held).</summary>
        internal static void BlockGuiInput()
        {
            var e=Event.current;if(e==null)return;var t=e.type;
            if(t==EventType.MouseDown||t==EventType.KeyDown||t==EventType.ScrollWheel||t==EventType.MouseDrag)e.Use();
        }
        /// <summary>OnGUI, after every screen and over the Expedition transfer screen: the loading screen, then the notice toast
        /// and the build label again (both pure draws) so they stay on top.</summary>
        private void StageLoadingGui()
        {
            if(Event.current.type!=EventType.Repaint||!stageLoad.Visible||error.Length>0)return;
            GUI.matrix=Matrix4x4.Scale(Vector3.one*U.S);
            bool gate=StageGate.Waiting,barrier=LiveExpedition?.areaBarrier?.active==true&&expeditionTransfer.Active,waiting=gate||barrier;
            string step=gate?NativeStageLabels.PilotsWaiting:barrier?NativeStageLabels.PartyWaiting:stagePlan.Label;
            string party=gate?NativeStageLabels.PilotsReady:barrier?expeditionTransfer.Ready:null,count=gate?NativeStageLabels.Count(StageGate.Ready,StageGate.Required):null;
            int ready=gate?StageGate.Ready:barrier?expeditionTransfer.ReadyCount:0,required=gate?StageGate.Required:barrier?expeditionTransfer.Required:0;
            float shown=stageLoad.Shown;
            NativeStageLoadingHud.Draw(stageLoad,stageView,step,NativeStageLabels.Percent(shown),shown,U.W,U.H,Time.unscaledTime,party,count,ready,required,waiting);
            NoticeToast();BuildCorner();
        }
        private void StageLoadingRelease(){shaderWarm?.Dispose();shaderWarm=null;}
    }
}
