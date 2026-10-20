using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using F=ChipArena.UnityConversion.NativeExpeditionEndingFixtures;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    // The Expedition's end sequence, wired (Claude, 2026-10-20; docs/EXPEDITION-ENDING-UI.md). Presentation only:
    //  - ExpeditionLiveSync steps the model every frame from the bound view. An ending this client watched happen (victory, extraction,
    //    the run ended, the party wiped, a solo pilot down) opens the Expedition screen on it once; a first sight of a run already ended
    //    (a late joiner, a reconnect) shows it settled when the player opens J, as the victory screen did. While it holds the screen the
    //    account XP award docks beside the summary (AccountXpDocked) instead of popping as the card over it.
    //  - It draws in place of the board on the Expedition screen, so the pointer is released and spectating paused exactly as for the
    //    progress and XP screens (ExpeditionScreenOpen, PR #290). RUN BOARD (and the victory's EXTRACT OR GO AGAIN, whose choices are
    //    the board's victory panel) shows the board; its footer's RUN SUMMARY comes back. Escape and J close the screen as before.
    //  - Sound: the title's sting through the match end's own cues (ui.round-won / ui.round-lost), once per watched ending.
    //  - Keys: Space, Enter or a click skip the sequence; then left / right (A / D) move the focus and Enter or Space confirm.
    //  - Actions: the pause menu's own Return to Lobby (host or offline, not while a return counts down) and Return to Main Menu, the
    //    board, and close. Nothing here sends a command, decides an outcome or changes the run.
    //  - DEV panel · Previews · EXPEDITION QUOTA & ENDING: every quota and ending state from MOCK views (developers only).
    public sealed partial class ConversionGame
    {
        private readonly ExpeditionEndingModel expeditionEnding=new ExpeditionEndingModel();private bool expeditionEndingBoard,expeditionEndingHeld,expeditionEndingModal;
        /// <summary>The ending holds the Expedition screen (else the board shows).</summary>
        private bool ExpeditionEndingShowing=>expeditionEnding.Shown&&!expeditionEndingBoard;
        /// <summary>The pause menu's own conditions for its lobby and main menu actions.</summary>
        private ExpeditionEndContext ExpeditionEndContextNow()=>new ExpeditionEndContext{Host=Hosting,Offline=!Online,Returning=LobbyReturnSeconds>0,HostLost=arena!=null&&arena.Replica&&server==null};
        /// <summary>An account XP award will show beside the summary (the binding, or the DEV MOCK); its panel builds only when it docks.</summary>
        private bool ExpeditionEndingXp=>AccountXpPreviewShown||AccountXpAwardOf(AccountXpSurface.Expedition)!=null;

        /// <summary>ExpeditionLiveSync, last: the model follows the bound view; a watched ending opens the screen on it once.</summary>
        private void ExpeditionEndingSync(ExpeditionView v)
        {
            if(v==null){expeditionEnding.Clear();expeditionEndingBoard=expeditionEndingHeld=false;return;}
            expeditionEnding.Step(v,Time.unscaledTime,prefs.reducedEffects,ExpeditionEndContextNow());
            if(expeditionEnding.OpensScreen)
            {
                expeditionEndingBoard=false;expeditionScreenOpen=true;
                // The title's sting through the match end's own cues (their bank keys, else today's clips), once per watched ending.
                bool lost=expeditionEnding.Kind==ExpeditionEndKind.Wiped||expeditionEnding.Kind==ExpeditionEndKind.Failed;
                nativeAudio?.Cue(lost?"ui.round-lost":"ui.round-won",lost?"lose":"win",null,.55f,mine:true);
            }
            // An ending that held the screen and is gone (the next loop began, a pilot reconnected standing): back to the world.
            if(expeditionEndingHeld&&!expeditionEnding.Shown&&expeditionScreenOpen)expeditionScreenOpen=false;
            if(!expeditionEnding.Shown)expeditionEndingBoard=false;
            bool showing=ExpeditionEndingShowing&&expeditionScreenOpen;expeditionEndingHeld=showing;
            // While the ending holds the screen, the account XP award docks beside the summary instead of popping as the card over it; a
            // canvas too narrow for the column (720p at a large interface scale) keeps the card, opened over the summary once it reaches it.
            if(showing&&NativeAccountXpLayout.Fits(U.W,ExpeditionEndingLayout.HostMin))accountXpCardOpen=false;
        }
        /// <summary>ExpeditionLiveKeys (Update), before J: the ending's keys while it holds the open screen.</summary>
        private void ExpeditionEndingKeys()
        {
            if(!expeditionScreenOpen||!ExpeditionEndingShowing)return;var m=expeditionEnding;
            bool confirm=Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter)||Input.GetKeyDown(KeyCode.Space);
            if(m.Playing){if(confirm)m.Skip();return;}
            if(Input.GetKeyDown(KeyCode.LeftArrow)||Input.GetKeyDown(KeyCode.A))m.Move(-1);
            if(Input.GetKeyDown(KeyCode.RightArrow)||Input.GetKeyDown(KeyCode.D))m.Move(1);
            if(confirm&&!expeditionEndingModal){var a=m.Chosen;if(a!=ExpeditionEndAction.None)ExpeditionEndingAct(a,false);}
        }
        /// <summary>ExpeditionLiveGui, first: the ending in place of the board while it holds the screen (true), else nothing (false).</summary>
        private bool ExpeditionEndingGui(ExpeditionView v,float width,float height)
        {
            if(!ExpeditionEndingShowing)return false;ExpeditionEndingDraw(expeditionEnding,v,width,height,false);return true;
        }
        /// <summary>The board's footer: RUN SUMMARY back to the ending while one holds.</summary>
        private void ExpeditionEndingButton(Rect r,bool modal)
        {
            if(!expeditionEnding.Shown||!expeditionEndingBoard)return;
            if(U.Btn(r,"RUN SUMMARY",false,!modal,1,12)&&!modal)expeditionEndingBoard=false;
        }
        /// <summary>ExpeditionLiveHud: while an ending holds and the screen is closed, its compact strip in the top centre (J reopens it).</summary>
        private bool ExpeditionEndingTag(float width)
        {
            if(!expeditionEnding.Shown||expeditionScreenOpen)return false;if(Event.current.type==EventType.Repaint)NativeExpeditionEndingHud.Tag(expeditionEnding,width);return true;
        }
        private void ExpeditionEndingDraw(ExpeditionEndingModel m,ExpeditionView v,float W,float Hh,bool mock)
        {
            bool reduced=prefs.reducedEffects,xp=ExpeditionEndingXp;var l=ExpeditionEndingLayout.Of(W,Hh,xp,m.RowCount,m.ZoneCount,m.ContinueText!=null);var c=LiveExpeditionControls();
            NativeExpeditionEndingHud.Backdrop(m,W,Hh,reduced);
            NativeExpeditionEndingHud.Summary(m,l,c.ChipName,c.ChipTone,reduced);
            // The account XP column: the account XP panel itself, docked beside the summary once the sequence reaches it (its own
            // animation starts there, once per completion), pending, confirmed or failed as the binding says.
            if(xp&&l.Xp&&m.XpIn>0&&m.PanelIn>0)AccountXpDocked(AccountXpSurface.Expedition,l.X,l.W,l.Y,ExpeditionEndingLayout.HostMin,m.XpIn*m.PanelIn);
            NativeExpeditionEndingHud.Title(m,l,W,Hh,reduced);
            // The world boss's dice reveal (a stored record appearing at victory) waits for the rewards, then shows over them, modal; so
            // does the account XP card where the column cannot dock.
            bool dice=!mock&&expeditionLiveDice.Open&&m.Age>=NativeExpeditionEnding.RewardsAt,card=!mock&&xp&&!l.Xp&&AccountXpCardOpen&&m.Age>=NativeExpeditionEnding.XpAt;
            if(!mock)expeditionEndingModal=dice||card;
            int pick=NativeExpeditionEndingHud.Actions(m,l,!dice&&!card);NativeExpeditionEndingHud.Hints(m,l,W,Hh);
            if(mock&&Event.current.type==EventType.Repaint){var b=new Rect(W/2-230,8,460,22);H.Tag(b,C.Magenta(),1,false);U.Text(b,F.Banner,10,C.Magenta(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);}
            if(dice)NativeExpeditionScreen.DiceOverlay(expeditionLiveDice,v,c,W,Hh,Time.unscaledTime,reduced);
            else if(card)AccountXpExpeditionCard(W,Hh);
            if(pick>=0)ExpeditionEndingAct(m.Action[pick],mock);
            var e=Event.current;if(m.Playing&&e.type==EventType.MouseDown&&e.button==0){m.Skip();e.Use();}
        }
        /// <summary>An action: the board, close, or the pause menu's own lobby and main menu returns (the DEV preview only says so).</summary>
        private void ExpeditionEndingAct(ExpeditionEndAction a,bool mock)
        {
            if(mock){DevSay("MOCK · "+NativeExpeditionEnding.Label(a,0)+" (the preview changes nothing)",false);return;}
            switch(a)
            {
                case ExpeditionEndAction.Choose:case ExpeditionEndAction.Board:expeditionEndingBoard=true;break;
                case ExpeditionEndAction.Close:expeditionScreenOpen=false;break;
                case ExpeditionEndAction.Lobby:
                    if(!(Hosting||!Online)||LobbyReturnSeconds>0)return;if(!Online)afterMatch=OfflineLobby();RequestLobbyReturn();expeditionScreenOpen=false;break;
                case ExpeditionEndAction.Menu:
                {
                    bool lost=arena!=null&&arena.Replica&&server==null;afterMatch=lost?MenuScreen.Join:MenuScreen.Home;
                    RequestStage(StageLoadKind.Return,()=>{if(Online)LeaveRoom();else ReturnToLobby();});break;
                }
            }
        }

        // ---- DEV preview (MOCK views) ------------------------------------------------------------------------------------------------------
        private bool expeditionUiPreview;private int expeditionUiQuotaPick=(int)F.Quota.Last,expeditionUiEndPick=(int)F.End.Victory,expeditionUiKills=-1;
        private readonly ExpeditionQuotaModel expeditionQuotaPreview=new ExpeditionQuotaModel();private readonly ExpeditionEndingModel expeditionEndingPreview=new ExpeditionEndingModel();
        private ExpeditionView expeditionUiQuotaView,expeditionUiEndView,expeditionUiLead;
        private static readonly string[] ExpeditionUiSteps={"KILL +1","REPLAY BEAT","REPLAY ENDING"};
        /// <summary>The MOCK preview is on (developers only).</summary>
        private bool ExpeditionUiPreviewShown=>expeditionUiPreview&&account!=null&&account.Developer;
        private void ExpeditionUiPreviewToggle()
        {
            expeditionUiPreview=!expeditionUiPreview;if(expeditionUiPreview){ExpeditionUiQuotaLoad(-1);ExpeditionUiEndLoad();}
            DevSay(expeditionUiPreview?"Expedition quota & ending preview · on (MOCK views; presentation only, nothing sent or saved)":"Expedition quota & ending preview · off",false);
        }
        private void ExpeditionUiQuotaLoad(int kills){expeditionUiKills=kills;expeditionUiQuotaView=F.QuotaView((F.Quota)expeditionUiQuotaPick,kills);if(kills<0)expeditionQuotaPreview.Clear();}
        // The ending's MOCK: a running view of the same run first, so the model watches it happen (the cinematic); a late joiner's not.
        private void ExpeditionUiEndLoad(){expeditionEndingPreview.Clear();var e=(F.End)expeditionUiEndPick;expeditionUiEndView=F.EndView(e);expeditionUiLead=F.FirstSight(e)?null:F.QuotaView(F.Quota.Running);}
        private void ExpeditionUiPreviewStep(int step)
        {
            if(step==0){int now=expeditionUiQuotaView?.CurrentBuilding()?.progress??0;ExpeditionUiQuotaLoad(Mathf.Min(36,now+1));}
            else if(step==1){expeditionUiQuotaPick=(int)F.Quota.Last;ExpeditionUiQuotaLoad(-1);expeditionQuotaPreview.Step(F.QuotaView(F.Quota.Last,35),Time.unscaledTime,prefs.reducedEffects,U.W);ExpeditionUiQuotaLoad(36);}
            else ExpeditionUiEndLoad();
        }
        /// <summary>DEV panel · Previews: the pickers and the mocked steps.</summary>
        private float ExpeditionUiPreviewPanel(float x,float y,float w)
        {
            y=DevHead(x,y,w,"EXPEDITION QUOTA & ENDING (DEV PREVIEW · MOCK)");DevToggleAct(new Rect(x,y,w,32),"preview-expedition-ui",expeditionUiPreview);y+=38;
            int q=DevChoice(x,ref y,w,F.QuotaNames,expeditionUiQuotaPick,NativeDevState.Live,NativeDevActions.Local,4,"Quota module (MOCK)");if(q!=expeditionUiQuotaPick){expeditionUiQuotaPick=q;ExpeditionUiQuotaLoad(-1);}
            int e=DevChoice(x,ref y,w,F.EndNames,expeditionUiEndPick,NativeDevState.Live,NativeDevActions.Local,3,"Ending (MOCK)");if(e!=expeditionUiEndPick){expeditionUiEndPick=e;ExpeditionUiEndLoad();}
            var st=expeditionUiPreview?NativeDevState.Live:NativeDevState.Unavailable;float bw=(w-16)/3;
            for(int i=0;i<ExpeditionUiSteps.Length;i++)if(DevButton(new Rect(x+i*(bw+8),y,bw,30),ExpeditionUiSteps[i],st,expeditionUiPreview?NativeDevActions.Local:"Turn the preview on first")&&expeditionUiPreview)ExpeditionUiPreviewStep(i);
            y+=38;
            return DevNote(x,y,w,"MOCK views only: no count, reward, price or outcome here is real or proposed. The quota module draws at the top centre (KILL +1 runs a kill, REPLAY BEAT plays the quota-complete beat); the ending plays over the match (Space skips, arrows and Enter use its actions, which only say what they would do). LATE JOINER shows it settled. The account XP column uses the account XP preview's MOCK while that is on.",C.Muted());
        }
        /// <summary>DevGui: the MOCK quota module and ending over the match.</summary>
        private void ExpeditionUiPreviewHud(float W,float Hh)
        {
            if(!ExpeditionUiPreviewShown)return;float now=Time.unscaledTime;bool reduced=prefs.reducedEffects;
            if(expeditionUiQuotaView!=null)
            {
                expeditionQuotaPreview.Step(expeditionUiQuotaView,now,reduced,W);if(expeditionQuotaPreview.CueNow)nativeAudio?.Cue("pve.clear","go",null,.6f,mine:true);
                NativeExpeditionQuotaHud.Module(expeditionQuotaPreview,W,Hh,now,reduced,!presentation.LowGraphics);
            }
            if(expeditionUiEndView==null)return;
            if(expeditionUiLead!=null){expeditionEndingPreview.Step(expeditionUiLead,now,reduced,ExpeditionEndContextNow());expeditionUiLead=null;}
            var m=expeditionEndingPreview;m.Step(expeditionUiEndView,now,reduced,(F.End)expeditionUiEndPick==F.End.VictoryGuest?new ExpeditionEndContext():new ExpeditionEndContext{Host=true});
            var ev=Event.current;
            if(ev.type==EventType.KeyDown)
            {
                bool confirm=ev.keyCode==KeyCode.Return||ev.keyCode==KeyCode.KeypadEnter||ev.keyCode==KeyCode.Space;
                if(m.Playing&&confirm){m.Skip();ev.Use();}
                else if(ev.keyCode==KeyCode.LeftArrow){m.Move(-1);ev.Use();}else if(ev.keyCode==KeyCode.RightArrow){m.Move(1);ev.Use();}
                else if(confirm&&m.Chosen!=ExpeditionEndAction.None){ExpeditionEndingAct(m.Chosen,true);ev.Use();}
            }
            ExpeditionEndingDraw(m,expeditionUiEndView,W,Hh,true);
        }
    }
}
