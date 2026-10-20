using System;
using System.Collections.Generic;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using E=ChipArena.UnityConversion.NativeExpeditionHud;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    // The live Expedition presentation (Claude, 2026-10-14; docs/EXPEDITION-PRESENTATION.md "Live binding"). Draws only while a session
    // is bound: ConversionGame.ExpeditionView on the host after BindExpedition, or the guest's paired receiver view, with the
    // intents sent through ExpeditionCommands (host: the session's own commands; guest: the command channel, whose refusals show at
    // once and whose acceptances stay pending until the replicated view shows them). No menu entry leads here and nothing binds a
    // session yet: Expedition stays unplayable until a scene and director exist. In the scene: the world pieces, the minimal HUD, a
    // compact transfer line and the dice reveal. J opens the Expedition screen (board, phase panel, chips, caches, loot); victory
    // opens it once per loop for the choice. Chip picks happen on the Boss Test's chip tray over the live view
    // (C, NativeExpeditionChipScreen.cs), not on J. The DEV preview (--ca-expedition-preview) is separate and binds nothing.
    public sealed partial class ConversionGame
    {
        private readonly ExpeditionIntentTracker expeditionIntents=new ExpeditionIntentTracker();
        private readonly NativeExpeditionScreen.Dice expeditionLiveDice=new NativeExpeditionScreen.Dice();
        private NativeExpeditionHud.Controls expeditionLiveControls;private Dictionary<string,ContentChip> expeditionLiveChips;
        private IExpeditionCommands expeditionLiveCommands;private object expeditionLiveBinding;
        private ExpeditionView expeditionLiveView;private float expeditionLiveNow;
        private string expeditionLiveRun;private int expeditionVictoryLoop=-1;private bool expeditionScreenOpen;

        /// <summary>The DEV preview is on screen (it draws its own scripted authority instead).</summary>
        private bool ExpeditionPreviewShown=>ExpeditionPreviewRequested&&!expeditionClosed&&arena==null&&menu;
        /// <summary>The bound session's view, or null (nothing bound, or the DEV preview is showing).</summary>
        private ExpeditionView LiveExpedition=>ExpeditionPreviewShown?null:ExpeditionView;
        /// <summary>The Expedition screen is open (it takes the pointer and pauses local play input, as the PvE chip screen does).</summary>
        private bool ExpeditionScreenOpen=>expeditionScreenOpen&&LiveExpedition!=null;

        /// <summary>The viewer's command path, made once per binding (host: the session's commands; guest: the channel sender).</summary>
        private IExpeditionCommands LiveExpeditionCommands()
        {
            object binding=(object)expeditionSession??expeditionGuest;if(binding==null){expeditionLiveCommands=null;expeditionLiveBinding=null;return null;}
            if(expeditionLiveCommands==null||!ReferenceEquals(binding,expeditionLiveBinding)){expeditionLiveBinding=binding;expeditionLiveCommands=ExpeditionCommands;}
            return expeditionLiveCommands;
        }
        private NativeExpeditionHud.Controls LiveExpeditionControls()
        {
            if(expeditionLiveControls==null)
            {
                expeditionLiveChips=new Dictionary<string,ContentChip>();if(content?.chips!=null)foreach(var c in content.chips)if(c!=null&&c.id!=null)expeditionLiveChips[c.id]=c;
                expeditionLiveControls=new NativeExpeditionHud.Controls
                {
                    Name=id=>expeditionLiveView?.Member(id)?.name??id,
                    ChipName=id=>id!=null&&expeditionLiveChips.TryGetValue(id,out var c)&&!string.IsNullOrEmpty(c.name)?c.name:id,
                    ChipTone=id=>id!=null&&expeditionLiveChips.TryGetValue(id,out var c)?NativeChipCard.Rarity(c.rarity):C.CyanSoft(),
                    Blocked=cmd=>{var commands=LiveExpeditionCommands();return commands==null?"Expedition is not bound.":commands.Blocked(cmd);},
                    State=cmd=>expeditionIntents.State(cmd,expeditionLiveView,expeditionLiveNow),
                    Last=cmd=>expeditionLiveView?.viewer==null?ExpeditionIntent.None:expeditionIntents.Last(expeditionLiveView.viewer,cmd),
                    Choose=()=>{expeditionScreenOpen=false;ExpeditionPickKey(true);},
                };
            }
            expeditionLiveControls.View=expeditionLiveView;return expeditionLiveControls;
        }
        /// <summary>Follows the bound view: a new run resets the tracked intents and the dice watch; victory opens the screen once per loop.</summary>
        private void ExpeditionLiveSync()
        {
            var v=LiveExpedition;expeditionLiveView=v;expeditionLiveNow=Time.unscaledTime;
            if(v==null){if(expeditionLiveRun!=null){expeditionIntents.Clear();expeditionLiveDice.Reset(null);expeditionLiveRun=null;expeditionScreenOpen=false;expeditionVictoryLoop=-1;}return;}
            if(v.run!=expeditionLiveRun){expeditionIntents.Clear();expeditionLiveDice.Reset(v);expeditionLiveRun=v.run;expeditionVictoryLoop=v.phase==ExpeditionPhase.Victory?v.loop:-1;}
            expeditionLiveDice.Watch(v,expeditionLiveNow);
            if(v.phase==ExpeditionPhase.Victory&&expeditionVictoryLoop!=v.loop&&NativeExpeditionPresentation.Has(v.mine.account)&&!v.mine.extracted){expeditionVictoryLoop=v.loop;expeditionScreenOpen=true;}
            // A completion's account XP opens the screen on its card, once, and again when it is confirmed (NativeAccountXpUI.cs).
            AccountXpExpeditionSync();
        }
        private void ExpeditionLiveSubmit(ExpeditionCommand command)
        {
            var commands=LiveExpeditionCommands();if(commands==null||expeditionLiveView==null||command.intent==ExpeditionIntent.None)return;
            expeditionIntents.Submit(commands,command,expeditionLiveView,Time.unscaledTime);
        }
        /// <summary>Update: J toggles the Expedition screen; Escape closes it. C opens or closes the chip tray (from J it closes J and opens
        /// it), and Escape over the tray is Choose later, as on the Boss Test's. True only for those Escapes (consumed: no pause menu).</summary>
        private bool ExpeditionLiveKeys()
        {
            // Resolve completion/XP screen changes before Update chooses cursor ownership, not first during OnGUI.
            ExpeditionLiveSync();
            ExpeditionPickStep();
            if(LiveExpedition==null){expeditionScreenOpen=false;return false;}if(GUIUtility.keyboardControl!=0)return false;
            if(Input.GetKeyDown(KeyCode.J))expeditionScreenOpen=!expeditionScreenOpen;
            if(expeditionScreenOpen&&Input.GetKeyDown(KeyCode.Escape)){expeditionScreenOpen=false;return true;}
            if(Input.GetKeyDown(KeyCode.C)){bool fromJ=expeditionScreenOpen;expeditionScreenOpen=false;ExpeditionPickKey(fromJ);}
            if(ExpeditionPickShown&&Input.GetKeyDown(KeyCode.Escape)&&!StageInputHeld){expeditionPick.Later();return true;}
            return false;
        }

        /// <summary>GuiFrame: the Expedition screen in place of the frame while it is open (true), else nothing (false).</summary>
        private bool ExpeditionLiveGui(float width,float height)
        {
            ExpeditionLiveSync();var v=expeditionLiveView;if(v==null||!expeditionScreenOpen)return false;
            var controls=LiveExpeditionControls();bool reduced=prefs.reducedEffects;
            U.Fill(new Rect(0,0,width,height),C.Void(arena!=null&&!menu?.78f:.94f));
            E.Minimal(v,width);
            var command=NativeExpeditionScreen.Panels(v,controls,width,120,height-64);
            bool modal=expeditionLiveDice.Open||AccountXpCardOpen;if(!modal)ExpeditionLiveSubmit(command);
            var foot=new Rect(24,height-48,width-48,32);NativeHudChrome.Tag(foot,C.Cyan(),1);
            U.Text(new Rect(foot.x+14,foot.y,foot.width-160,foot.height),"EXPEDITION · J OR ESC CLOSES · "+(v.ViewerIsHost()?"YOU ARE THE HOST":"GUEST · COMMANDS GO TO THE HOST"),11,C.CyanSoft(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
            AccountXpCardButton(new Rect(foot.xMax-266,foot.y+1,126,30),modal);
            if(U.Btn(new Rect(foot.xMax-130,foot.y+1,120,30),"Close",false,!modal,1,13)&&!modal)expeditionScreenOpen=false;
            NativeExpeditionScreen.DiceOverlay(expeditionLiveDice,v,controls,width,height,Time.unscaledTime,reduced);
            AccountXpExpeditionCard(width,height);
            return true;
        }

        /// <summary>The in-scene Expedition HUD: world piece labels, the minimal HUD, the transfer line and the dice reveal.</summary>
        private void ExpeditionLiveHud(float width,float height)
        {
            ExpeditionLiveSync();var v=expeditionLiveView;if(v==null||expeditionScreenOpen)return;
            ExpeditionWorldLabels();
            // The last enemies of a kill quota, marked (NativeQuotaMarksPresentation.cs): under the HUD, over the world labels.
            QuotaMarksHud(v,width,height);
            // Chips waiting: the Boss Test's CHIPS tag with its C key (ExpeditionChipsTag) in place of the minimal HUD's.
            E.Minimal(v,width,1,false);
            bool countdown=NativeExpeditionPresentation.Active(v).active;
            if(v.transfer.stage!=ExpeditionTransferStage.None)E.TransferBanner(new Rect(width/2-220,countdown?116:58,440,46),v);
            // The SAFE chip, the boss plates and intro, the final gate's notice (NativeExpeditionStatesPresentation.cs).
            ExpeditionStatesHud(v,width,height,58+(countdown?58:0)+(v.transfer.stage!=ExpeditionTransferStage.None?54:0));
            // The party list, the join circle panel, the deploy notes and my deploy flash (NativeExpeditionJoinPresentation.cs).
            ExpeditionJoinHud(width,height);
            var hint=new Rect(width-24-150,24,150,28);NativeHudChrome.Tag(hint,C.Cyan(),.9f);U.Text(new Rect(hint.x+12,hint.y,hint.width-12,hint.height),"J · EXPEDITION",11,C.CyanSoft(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
            ExpeditionChipsTag(v);
            // The reveal of a new stored dice record: not modal in the scene (the pointer stays with play); it closes by itself.
            if(expeditionLiveDice.Open)NativeExpeditionScreen.DiceOverlay(expeditionLiveDice,v,LiveExpeditionControls(),width,height,Time.unscaledTime,prefs.reducedEffects,false);
            // The chip tray over the live view, on top of the HUD (NativeExpeditionChipScreen.cs).
            ExpeditionPickGui(width,height);
        }
    }
}
