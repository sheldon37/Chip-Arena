using System;
using System.IO;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using F=ChipArena.UnityConversion.NativeAccountXpFixtures;
using L=ChipArena.UnityConversion.NativeAccountXpLayout;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    // Permanent account XP, the presentation's wiring (Claude, 2026-10-19; owner: "please start the presentation side of permanent
    // account XP for Chip Arena"; contract proposed in docs/ACCOUNT-XP-PRESENTATION.md, awaiting Astra). Presentation only:
    //  - the XP panel docked beside the PvP match end (MatchEndPanel) and the PvE results (PveResultsPanel), and as a card on the
    //    Expedition screen (J), opening by itself once per completion and again when its award is confirmed;
    //  - the account level under the home nameplate (NativeHomeMenu Nameplate);
    //  - the DEV preview (DEV panel · Previews · ACCOUNT XP): MOCK fixtures for every state, never saved or sent.
    // It reads the three bindings below and the local record of completions already celebrated (UI state only, in the user data
    // folder); it computes no reward, curve or unlock, writes no account data and sends nothing. Until Astra binds them, the
    // bindings return null: no award or level is shown anywhere and every surface draws exactly as before.
    public sealed partial class ConversionGame
    {
        // ================================================================================================================
        // The bindings (Astra). Each is one line; AccountXpChecks requires them bound once account XP data exists anywhere.
        //  1. The viewer's award for the completion this results surface shows: pending as soon as the completion is recorded,
        //     confirmed once the account write succeeded, failed with the authority's retry state; the same completionId each
        //     time, its revision rising. Null: none. e.g.
        //       internal AccountXpAward AccountXpAwardOf(AccountXpSurface surface)=>accountXpFeed?.Award(account.Access.AccountId,surface);
        //  2. The signed-in account's saved XP for the menu (and its pending count). Null: unknown. e.g.
        //       internal AccountXpSummary AccountXpSummaryOf()=>account?.Account?.xp;
        //  3. Optional: the player's retry of a failed award whose retry is "manual" (called with its completionId). Null: none. e.g.
        //       internal Action<string> AccountXpRetryOf()=>accountXpFeed!=null?accountXpFeed.Retry:null;
        // ================================================================================================================
        internal AccountXpAward AccountXpAwardOf(AccountXpSurface surface)=>CurrentXpAward(surface);
        internal AccountXpSummary AccountXpSummaryOf()=>account?.XpSummary;
        internal Action<string> AccountXpRetryOf()=>account!=null?account.RetryXp:(Action<string>)null;

        private readonly NativeAccountXpPanel accountXp=new NativeAccountXpPanel();private readonly NativeAccountXpBadge accountXpBadge=new NativeAccountXpBadge();
        private NativeAccountXpSeen accountXpSeen;private int accountXpFrame=-1;private AccountXpSurface accountXpSurface;private bool accountXpShown;
        private string accountXpCardId,accountXpCardStatus;private bool accountXpCardOpen;

        private string AccountXpViewer=>account!=null&&!string.IsNullOrEmpty(account.Access.AccountId)?account.Access.AccountId:null;
        private bool AccountXpDev=>account!=null&&account.Developer;
        private string AccountXpSeenPath=>Path.Combine(NativeUserDataPath,NativeAccountXpSeen.File);
        // The local record, read once; written when a completion is first celebrated (never per frame, never account XP).
        private NativeAccountXpSeen AccountXpSeen()
        {
            if(accountXpSeen!=null)return accountXpSeen;
            try{var p=AccountXpSeenPath;accountXpSeen=File.Exists(p)?NativeAccountXpSeen.Parse(File.ReadAllText(p)):new NativeAccountXpSeen();}
            catch(Exception e)when(e is IOException||e is UnauthorizedAccessException){accountXpSeen=new NativeAccountXpSeen();}
            return accountXpSeen;
        }
        private void AccountXpSave()
        {
            var s=accountXpSeen;if(s==null||!s.Dirty)return;
            try{NativeFileStore.Write(AccountXpSeenPath,s.Serialize());}catch(Exception e)when(e is IOException||e is UnauthorizedAccessException){}
            s.Saved();
        }

        /// <summary>The award on `surface` this frame (the DEV preview's MOCK while it runs, else binding 1), built into the panel; null
        /// when there is none to draw. Built once per frame, at that frame's unscaled clock.</summary>
        private NativeAccountXpPanel AccountXpFor(AccountXpSurface surface)
        {
            int f=Time.frameCount;if(f==accountXpFrame&&surface==accountXpSurface)return accountXpShown?accountXp:null;
            accountXpFrame=f;accountXpSurface=surface;bool mock=AccountXpPreviewShown;var a=mock?accountXpMock:AccountXpAwardOf(surface);
            if(a==null){accountXp.Clear();accountXpShown=false;return null;}
            accountXp.Build(a,mock?null:AccountXpViewer,Time.unscaledTime,prefs.reducedEffects,AccountXpSeen(),AccountXpDev);AccountXpSave();
            accountXpShown=accountXp.Shown;return accountXpShown?accountXp:null;
        }
        private void AccountXpRetry(NativeAccountXpPanel m)
        {
            if(m.Mock){AccountXpPreviewRetry();return;}
            var retry=AccountXpRetryOf();if(retry!=null&&m.CompletionId!=null)retry(m.CompletionId);
        }

        // ---- PvP match end and PvE results: the XP column beside them ----------------------------------------------------------------
        /// <summary>The results panel's room: the canvas less the XP column while an award shows beside it (unchanged without one).</summary>
        private float AccountXpHostRoom(AccountXpSurface s,float W,float hostMin)=>L.HostRoom(W,hostMin,AccountXpFor(s)!=null);
        /// <summary>The results panel's left edge: centred together with the XP column, or alone as before.</summary>
        private float AccountXpHostX(AccountXpSurface s,float W,float hostW,float hostMin)=>L.HostLeft(W,hostW,hostMin,AccountXpFor(s)!=null);
        /// <summary>The XP panel beside the results panel at hostX, hostW, hostY (its own column, its top level with theirs).</summary>
        private void AccountXpDocked(AccountXpSurface s,float hostX,float hostW,float hostY,float hostMin,float enter)
        {
            var m=AccountXpFor(s);if(m==null)return;
            var l=L.Dock(U.W,U.H,hostX,hostW,hostY,hostMin,m.Rows,m.Status,m.Dev&&m.FlawText!=null);
            if(NativeAccountXpHud.Panel(l,m,enter,Time.unscaledTime,prefs.reducedEffects,m.Mock||AccountXpRetryOf()!=null,out _))AccountXpRetry(m);
        }

        // ---- the Expedition screen: the card ---------------------------------------------------------------------------------------
        /// <summary>The card is open over the Expedition screen (the screen's commands wait, as for the dice reveal).</summary>
        private bool AccountXpCardOpen=>accountXpCardOpen&&accountXpCardId!=null;
        /// <summary>ExpeditionLiveSync: a new completion's award, or its confirmation, opens the Expedition screen on the card (so the
        /// one celebration is seen). Reads the binding only; the panel itself is built when the card draws.</summary>
        private void AccountXpExpeditionSync()
        {
            var a=AccountXpAwardOf(AccountXpSurface.Expedition);if(a==null||string.IsNullOrEmpty(a.completionId))return;
            bool fresh=a.completionId!=accountXpCardId,confirmed=a.status==NativeAccountXpContract.Confirmed&&accountXpCardStatus!=NativeAccountXpContract.Confirmed;
            // A confirmed completion already celebrated (a rejoin, a restart) stays closed: ACCOUNT XP shows it on request.
            bool done=fresh&&a.status==NativeAccountXpContract.Confirmed&&AccountXpSeen().Contains(NativeAccountXpSeen.KeyOf(a.account,a.completionId));
            if(fresh||confirmed){accountXpCardId=a.completionId;accountXpCardOpen=!done;if(!done)expeditionScreenOpen=true;}
            accountXpCardStatus=a.status;
        }
        /// <summary>ExpeditionLiveGui, last: the card over the dimmed screen, with Close; true while it is open.</summary>
        private bool AccountXpExpeditionCard(float width,float height)
        {
            if(!AccountXpCardOpen)return false;var m=AccountXpFor(AccountXpSurface.Expedition);if(m==null){accountXpCardOpen=false;return false;}
            U.Fill(new Rect(0,0,width,height),C.Void(.55f));var l=L.CardOn(width,height,m.Rows,m.Status,m.Dev&&m.FlawText!=null);
            if(NativeAccountXpHud.Panel(l,m,1,Time.unscaledTime,prefs.reducedEffects,m.Mock||AccountXpRetryOf()!=null,out bool close))AccountXpRetry(m);
            if(close)accountXpCardOpen=false;return true;
        }
        /// <summary>The Expedition screen's footer: ACCOUNT XP reopens a closed card (its final state; nothing replays).</summary>
        private void AccountXpCardButton(Rect r,bool modal)
        {
            if(accountXpCardId==null||accountXpCardOpen||AccountXpAwardOf(AccountXpSurface.Expedition)==null)return;
            if(U.Btn(r,NativeAccountXpText.Eyebrow,false,!modal,1,12)&&!modal)accountXpCardOpen=true;
        }

        // ---- the home screen: the account level under the nameplate -------------------------------------------------------------------
        /// <summary>The nameplate's third line at (x, y): the saved level and progress, PENDING while awards wait; nothing when unknown.</summary>
        private void AccountXpNameplate(float x,float y,float alpha)
        {
            if(alpha<=0||Event.current.type!=EventType.Repaint)return;
            bool mock=AccountXpPreviewMenu;accountXpBadge.Build(mock?accountXpMockSummary:AccountXpSummaryOf(),mock?null:AccountXpViewer);if(!accountXpBadge.Shown)return;
            Ramp(new Rect(x-10,y-4,192,BadgeRamp),.72f,.55f,alpha,true);Ramp(new Rect(x+182,y-4,48,BadgeRamp),.55f,0,alpha,true);
            NativeAccountXpHud.Badge(x+1,y,accountXpBadge,alpha,Time.unscaledTime,prefs.reducedEffects);
        }
        private const float BadgeRamp=28;

        // ---- DEV preview (MOCK values) ----------------------------------------------------------------------------------------------
        private bool accountXpPreview;private int accountXpPreviewKind=(int)F.Kind.LevelUp,accountXpPreviewMenu;private AccountXpAward accountXpMock;private AccountXpSummary accountXpMockSummary;
        private static readonly string[] AccountXpPreviewSteps={"MOCK CONFIRM","MOCK FAIL","REPLAY"};
        /// <summary>The DEV preview's MOCK award replaces binding 1 on every surface (developers only).</summary>
        private bool AccountXpPreviewShown=>accountXpPreview&&AccountXpDev&&accountXpMock!=null;
        private bool AccountXpPreviewMenu=>AccountXpDev&&accountXpPreviewMenu!=(int)F.SummaryKind.Off;
        private void AccountXpPreviewToggle()
        {
            accountXpPreview=!accountXpPreview;if(accountXpPreview)AccountXpPreviewLoad();
            DevSay(accountXpPreview?"Account XP preview · on (MOCK values; presentation only, nothing saved or sent)":"Account XP preview · off",false);
        }
        // A fresh MOCK completion of the picked kind, played from the start (mock ids live in the session's memory only).
        private void AccountXpPreviewLoad()
        {
            accountXpMock=F.Make((F.Kind)accountXpPreviewKind,arena?.Pve!=null?NativeAccountXpContract.Pve:NativeAccountXpContract.Pvp);if(accountXpMock==null)return;
            AccountXpSeen().ForgetMock(NativeAccountXpSeen.KeyOf(accountXpMock.account,accountXpMock.completionId));accountXp.Replay(AccountXpSeen());accountXpFrame=-1;
        }
        // The authority's next issue of the same MOCK completion (confirm, or fail with an automatic retry; a manual retry goes pending).
        private void AccountXpPreviewStep(int step)
        {
            if(accountXpMock==null)return;
            if(step==0)accountXpMock=F.Reissue(accountXpMock,NativeAccountXpContract.Confirmed);
            else if(step==1)accountXpMock=F.Reissue(accountXpMock,NativeAccountXpContract.Failed,NativeAccountXpContract.Auto,12,"MOCK · The account service did not answer.");
            else AccountXpPreviewLoad();
            accountXpFrame=-1;
        }
        private void AccountXpPreviewRetry(){if(accountXpMock==null)return;accountXpMock=F.Reissue(accountXpMock,NativeAccountXpContract.Pending);accountXpFrame=-1;}
        /// <summary>DEV panel · Previews: the fixture pickers and the mocked authority steps.</summary>
        private float AccountXpPreviewPanel(float x,float y,float w)
        {
            y=DevHead(x,y,w,"ACCOUNT XP (DEV PREVIEW · MOCK VALUES)");DevToggleAct(new Rect(x,y,w,32),"preview-account-xp",accountXpPreview);y+=38;
            int k=DevChoice(x,ref y,w,F.Names,accountXpPreviewKind,NativeDevState.Live,NativeDevActions.Local,3,"Results panel fixture (MOCK)");
            if(k!=accountXpPreviewKind){accountXpPreviewKind=k;AccountXpPreviewLoad();}
            var st=accountXpPreview?NativeDevState.Live:NativeDevState.Unavailable;float bw=(w-16)/3;
            for(int i=0;i<AccountXpPreviewSteps.Length;i++)if(DevButton(new Rect(x+i*(bw+8),y,bw,30),AccountXpPreviewSteps[i],st,accountXpPreview?NativeDevActions.Local:"Turn the preview on first")&&accountXpPreview)AccountXpPreviewStep(i);
            y+=38;
            int s=DevChoice(x,ref y,w,F.SummaryNames,accountXpPreviewMenu,NativeDevState.Live,NativeDevActions.Local,3,"Main menu level (MOCK)");
            if(s!=accountXpPreviewMenu){accountXpPreviewMenu=s;accountXpMockSummary=F.Summary((F.SummaryKind)s);}
            return DevNote(x,y,w,"MOCK values only: no XP amount, level, threshold, perk or unlock here is real or proposed. The panel shows over the match (and beside the PvP/PvE results while on); MOCK CONFIRM and MOCK FAIL play the authority's next issue of the same completion, which animates once. The menu level shows under the home nameplate. Nothing is saved or sent.",C.Muted());
        }
        /// <summary>DevGui: the MOCK panel over the match (the results dock it themselves), left of the DEV panel while that is open.</summary>
        private void AccountXpPreviewHud(float W,float Hh)
        {
            if(!AccountXpPreviewShown||match==null||match.Phase==NativePhase.Match)return;var m=AccountXpFor(AccountXpSurface.Pvp);if(m==null)return;
            float hostW=Mathf.Min(640,L.HostRoom(W,480,true)-32),hostX=L.HostLeft(W,hostW,480,true);var l=L.Dock(W,Hh,hostX,hostW,96,480,m.Rows,m.Status,m.Dev&&m.FlawText!=null);
            if(devOpen&&!l.Mini){float dx=Mathf.Max(L.Margin,devPanelRect.x-24-l.Panel.W)-l.Panel.X;if(dx<0)l=L.Dock(W,Hh,hostX+dx,hostW,96,480,m.Rows,m.Status,m.Dev&&m.FlawText!=null);}
            NativeAccountXpHud.Banner(l.Panel.X+l.Panel.W/2,l.Panel.Y-32,1);
            if(NativeAccountXpHud.Panel(l,m,1,Time.unscaledTime,prefs.reducedEffects,true,out _))AccountXpPreviewRetry();
        }
    }
}
