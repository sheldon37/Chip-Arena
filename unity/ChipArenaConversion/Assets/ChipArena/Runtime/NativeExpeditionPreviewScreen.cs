using System;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using E=ChipArena.UnityConversion.NativeExpeditionHud;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// DEV PREVIEW ONLY (Claude, 2026-10-14): the layout of the Expedition preview (--ca-expedition-preview) over the scripted scenarios of
    /// Core/NativeExpeditionPreview: the minimal HUD on top; the world board, the phase's panel with the host-transfer overlay and
    /// the loot, and the personal chip panel; the dice reveal over everything; and the preview bar (scenarios, steps, whose view).
    /// Controls send through the viewer's real command path (host local, guest unavailable) and show the tracked state.
    /// </summary>
    public static class NativeExpeditionPreviewScreen
    {
        public sealed class State
        {
            public bool Auto, ExitRequested;public float AutoAt, DiceAt=-100;public int DiceSeen;public ExpeditionDice Dice;
            public Action Loaded;
            public void Load(NativeExpeditionPreview p,int scenario,float now){Loaded?.Invoke();p.Load(scenario);DiceSeen=p.View().dice.Length;Dice=null;AutoAt=now;}
        }

        /// <summary>One preview frame. `dt` advances the authority clock (pass it on the repaint event only).</summary>
        public static void Frame(NativeExpeditionPreview p,State st,E.Controls controls,float W,float H,float now,float dt,bool reduced)
        {
            if(dt>0){p.Tick(dt);if(st.Auto&&now-st.AutoAt>4){st.AutoAt=now;if(!p.Next())st.Auto=false;}}
            var v=p.View();var commands=p.Commands();controls.Blocked=commands.Blocked;controls.State=cmd=>p.Intents.State(cmd,v,p.Clock);
            if(v.dice.Length>st.DiceSeen){st.Dice=v.dice[v.dice.Length-1];st.DiceAt=now;}st.DiceSeen=v.dice.Length;
            // Backdrop: the void with a soft cyan pool, so the panels read as they will over a dark city.
            U.Fill(new Rect(0,0,W,H),C.Void(.94f));U.Radial(new Rect(W*.2f,-H*.4f,W*.6f,H*1.2f),C.Cyan(.08f));
            E.Minimal(v,W);
            var command=default(ExpeditionCommand);float top=120,bottom=H-162,gap=16,side=Mathf.Clamp((W-48-2*gap)*.27f,280,380),cx=24+side+gap,cw=W-2*(24+side+gap);
            E.Board(new Rect(24,top,side,bottom-top),v,controls);
            var chip=E.Chips(new Rect(W-24-side,top,side,bottom-top),v,controls);if(chip.intent!=ExpeditionIntent.None)command=chip;
            float y=top;
            if(v.transfer.stage!=ExpeditionTransferStage.None){var tr=new Rect(cx,y,cw,Mathf.Min(280,112+26*5+20+18*v.transfer.candidates.Length));E.Transfer(tr,v,controls);y=tr.yMax+gap;}
            float panel=Mathf.Min(v.phase==ExpeditionPhase.Victory?470:v.phase==ExpeditionPhase.Town?390:360,bottom-y);
            var ctx=new Rect(cx,y,cw,panel);ExpeditionCommand pick=default;
            switch(v.phase)
            {
                case ExpeditionPhase.Instance:pick=E.Building(ctx,v,controls);break;
                case ExpeditionPhase.Victory:pick=E.Victory(ctx,v,controls);break;
                case ExpeditionPhase.Town:pick=E.Sanctuary(ctx,v,controls);break;
                default:E.Phase(ctx,v);break;
            }
            if(pick.intent!=ExpeditionIntent.None)command=pick;
            if(bottom-ctx.yMax-gap>140)E.Loot(new Rect(cx,ctx.yMax+gap,cw,bottom-ctx.yMax-gap),v);
            // While the dice reveal is open it takes the pointer: presses under it are ignored.
            bool modal=st.Dice!=null;
            if(command.intent!=ExpeditionIntent.None&&!modal)p.Submit(command);
            Bar(W,H,p,st,v,modal,now);if(st.ExitRequested)return;
            if(st.Dice!=null)
            {
                float t=now-st.DiceAt,dh=Mathf.Min(H-80,160+44*st.Dice.eligible.Length);var dr=new Rect(W/2-290,H/2-dh/2-30,580,dh);
                U.Fill(new Rect(0,0,W,H),C.Void(.55f));E.Dice(dr,v,st.Dice,t,controls,reduced);
                if(U.Btn(new Rect(dr.xMax-128,dr.y+14,110,30),"Close",false,true,1,13)||t>NativeExpeditionPresentation.Dice.Duration(st.Dice)+6)st.Dice=null;
            }
        }

        // The preview bar: what this is, the scenarios, the scripted step, and whose view is shown.
        private static void Bar(float W,float H,NativeExpeditionPreview p,State st,ExpeditionView v,bool modal,float now)
        {
            var bar=new Rect(24,H-150,W-48,134);NativeHudChrome.Panel(bar,1,C.Warn(),NativeHudCyber.Cut,NativeHudChrome.Corners.Diagonal,true,.96f);
            float x=bar.x+18,y=bar.y+12;
            U.Text(new Rect(x,y,560,22),"EXPEDITION PREVIEW · DEV ONLY · NOT A GAME MODE",12,C.Warn(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
            U.Text(new Rect(x,y+18,620,16),"Real rules authority. Offers, prices and the save folder are preview stand-ins.",11,C.Muted(),TextAnchor.MiddleLeft);
            if(U.Btn(new Rect(bar.xMax-18-120,y,120,28),"Exit preview",false,true,1,13)&&!modal){st.ExitRequested=true;return;}
            float vx=bar.xMax-18-120-12;var members=v.members;
            for(int i=members.Length-1;i>=0;i--)
            {
                string a=members[i].account;string label=a.ToUpperInvariant()+(members[i].host?" (HOST)":"");float w=U.Measure(label,12,true).x+24;vx-=w;
                if(U.Btn(new Rect(vx,y,w,28),label,a==p.Viewer,true,1,12)&&!modal)p.Viewer=a;vx-=6;
            }
            if(vx-80>x+560)U.Text(new Rect(vx-80,y,74,28),"VIEW AS",11,C.CyanSoft(),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.12f);
            float tx=x,ty=bar.y+46;
            for(int i=0;i<NativeExpeditionPreview.Scenarios.Length;i++)
            {
                var s=NativeExpeditionPreview.Scenarios[i];float w=U.Measure(s.Title,12,true).x+26;if(tx+w>bar.xMax-18)break;
                if(U.Btn(new Rect(tx,ty,w,28),s.Title,i==p.ScenarioIndex,true,1,12)&&!modal)st.Load(p,i,now);tx+=w+8;
            }
            float cy=bar.y+84,controls=4*118;string step=p.Finished?"End of scenario. ":"Step "+p.StepIndex+" / "+p.Current.Steps.Length+". ";
            float ly=cy;foreach(var line in U.Wrap(step+p.Caption,13,bar.width-36-controls-16,false,2)){U.Text(new Rect(x,ly,bar.width-36-controls,20),line,13,C.Ink(),TextAnchor.MiddleLeft);ly+=20;}
            float bx=bar.xMax-18-controls;
            if(U.Btn(new Rect(bx,cy,110,32),p.Finished?"Done":"Next step",true,!p.Finished,1,13)&&!modal){p.Next();st.AutoAt=now;}bx+=118;
            if(U.Btn(new Rect(bx,cy,110,32),"Restart",false,true,1,13)&&!modal)st.Load(p,p.ScenarioIndex,now);bx+=118;
            if(U.Btn(new Rect(bx,cy,110,32),st.Auto?"Auto: on":"Auto: off",st.Auto,true,1,13)&&!modal){st.Auto=!st.Auto;st.AutoAt=now;}bx+=118;
            if(U.Btn(new Rect(bx,cy,110,32),"Replay dice",false,v.dice.Length>0,1,13)&&v.dice.Length>0&&!modal){st.Dice=v.dice[v.dice.Length-1];st.DiceAt=now;}
        }
    }
}
