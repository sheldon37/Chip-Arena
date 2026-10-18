using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The menu screen transitions (Core NativeScreenTransition, drawn by NativeScreenTransitionHud; owner 2026-10-15: "When
    /// navigating rooms from menu, show a screen transition that matches our style"). Go keeps every side effect of a
    /// navigation where it was (Settings applied and saved, a pending connection dropped, the mode set, the Locker's revert
    /// point taken, the lobby's scroll reset): only the screen shown waits. The shutters close over the screen being left,
    /// the swap runs under them on an IMGUI layout event (so the new screen's first layout and its panel-in start hidden and
    /// no half-built frame shows), the destination's title flashes on, and the shutters open on the new screen. Routing reads
    /// MenuTarget (where the menu is going), so a pending swap is never retargeted by the room checks. No transition without
    /// a menu drawn a moment ago (the first menu frame, a return from a match), over a match or the appearance preview, or
    /// while the stage loading screen is up: those swap at once, as before.
    /// </summary>
    public sealed partial class ConversionGame
    {
        private readonly NativeScreenTransition menuTransition=new NativeScreenTransition();
        private MenuScreen? menuNext;private bool menuNextSandbox;
        // When the home menu last finished a pass (the first pass after a match or the preview sees an old value: no transition).
        private float homeSeen=-1000;
        /// <summary>The screen the menu is on, or going to while a transition covers the one it is leaving.</summary>
        private MenuScreen MenuTarget=>menuNext??menuScreen;
        // The transition's title and eyebrow per MenuScreen (Home, Play, Create, Join, Lobby, Boss, Customize, Settings,
        // Account, Dev, Catalogue, Mods), constants.
        private static readonly string[] MenuTitles={"MAIN MENU","PLAY VS BOTS","CREATE ROOM","JOIN ROOM","ROOM LOBBY","EXPEDITIONS","CUSTOMIZE","SETTINGS","ACCOUNT","DEV MENU","CATALOGUE","MODS"};
        private static readonly string[] MenuEyebrows={"CHIP ARENA","LOCAL MATCH","PRIVATE ROOM","PRIVATE ROOM","PRIVATE ROOM","PVE","LOCKER","SYSTEM","PILOT","DEVELOPER","SOURCE REFERENCE","CHIP PACKS"};
        private const string SandboxTitle="DEV SANDBOX",SandboxEyebrow="DEVELOPER";

        // Go's display half: through the transition, or at once.
        private void ShowScreen(MenuScreen screen,bool sandbox)
        {
            float now=Time.unscaledTime;
            bool animate=!dedicated&&arena==null&&previewArena==null&&!stageLoad.Visible&&error.Length==0&&now-homeSeen<=.25f&&screen!=MenuTarget;
            if(animate){menuNext=screen;menuNextSandbox=sandbox;menuTransition.Begin(now,prefs.reducedEffects,screen==MenuScreen.Home);return;}
            // Already on the way there: keep the running transition.
            if(menuNext.HasValue&&menuNext.Value==screen&&menuTransition.Active(now)){menuNextSandbox=sandbox;return;}
            if(menuNext.HasValue)menuTransition.Cancel();menuNext=null;ApplyScreen(screen,sandbox);
        }
        // The screen change itself (Go's last line before transitions).
        private void ApplyScreen(MenuScreen screen,bool sandbox)
        {
            if(screen!=MenuScreen.Home)updateOpen=false;playSandbox=sandbox;
            menuScreen=screen;menuScreenAt=Time.unscaledTime;screenScroll=Vector2.zero;screenContent=400;GUIUtility.keyboardControl=0;
        }
        /// <summary>HomeMenu, first: the swap once the screen is covered (or at once if the menu was away), on a layout event so
        /// IMGUI lays out the new screen before it draws; then, while covered, this event's clicks and keys go nowhere.</summary>
        private void MenuTransitionFrame(float now,bool seen)
        {
            if(menuNext.HasValue&&Event.current.type==EventType.Layout&&(!seen||menuTransition.SwapDue(now)||!menuTransition.Active(now)))
            {var s=menuNext.Value;menuNext=null;ApplyScreen(s,menuNextSandbox);if(seen)menuTransition.MarkSwapped();else menuTransition.Cancel();}
            if(menuTransition.BlocksInput(now))BlockGuiInput();
        }
        /// <summary>HomeMenu, last: the shutters over the menu (under the notice toast and the build label).</summary>
        private void MenuTransitionGui(float now)
        {
            if(Event.current.type!=EventType.Repaint||!menuTransition.Active(now))return;
            var dest=MenuTarget;bool sandbox=dest==MenuScreen.Play&&(menuNext.HasValue?menuNextSandbox:playSandbox);int i=(int)dest;
            string title=sandbox?SandboxTitle:i>=0&&i<MenuTitles.Length?MenuTitles[i]:null,eyebrow=sandbox?SandboxEyebrow:i>=0&&i<MenuEyebrows.Length?MenuEyebrows[i]:null;
            // A PvE room is the Expeditions screen as well (NativeExpeditionsMenu.cs): EXPEDITIONS / PVE either way.
            if(dest==MenuScreen.Boss||dest==MenuScreen.Lobby&&prefs.pve){eyebrow=MenuEyebrows[(int)MenuScreen.Boss];title=MenuTitles[(int)MenuScreen.Boss];}
            NativeScreenTransitionHud.Draw(menuTransition,now,U.W,U.H,eyebrow,title);
        }
    }
}
