using System;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using E=ChipArena.UnityConversion.NativeExpeditionHud;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// DEV PREVIEW ONLY (Claude, 2026-10-14): the Expedition preview (--ca-expedition-preview) over the scripted scenarios of
    /// Core/NativeExpeditionPreview: the minimal HUD on top, the shared Expedition screen layout (NativeExpeditionScreen) or the
    /// greybox world view of the anchors, the dice reveal over everything, and the preview bar (scenarios, steps, whose view).
    /// Controls send through the viewer's command path (host local; guests over the preview's loopback channel) and show the
    /// tracked state. It binds no session and opens no menu entry.
    /// </summary>
    public static class NativeExpeditionPreviewScreen
    {
        public sealed class State
        {
            public bool Auto, ExitRequested, Greybox;public float AutoAt;
            public readonly NativeExpeditionScreen.Dice Dice=new NativeExpeditionScreen.Dice();
            public readonly NativeExpeditionWorld World=new NativeExpeditionWorld();
            public Action Loaded;
            public void Load(NativeExpeditionPreview p,int scenario,float now){Loaded?.Invoke();p.Load(scenario);Dice.Reset(p.Shown());AutoAt=now;}
        }

        /// <summary>One preview frame. `dt` advances the authority clock (pass it on the repaint event only).</summary>
        public static void Frame(NativeExpeditionPreview p,State st,E.Controls controls,float W,float H,float now,float dt,bool reduced)
        {
            if(dt>0){p.Tick(dt);if(st.Auto&&now-st.AutoAt>4){st.AutoAt=now;if(!p.Next())st.Auto=false;}}
            var v=p.Shown();var commands=p.Commands();string viewer=p.Viewer;
            controls.View=v;controls.Blocked=commands.Blocked;controls.State=cmd=>p.Intents.State(cmd,v,p.Clock);controls.Last=cmd=>p.Intents.Last(viewer,cmd);
            st.Dice.Watch(v,now);
            // Backdrop: the void with a soft cyan pool, so the panels read as they will over a dark city.
            U.Fill(new Rect(0,0,W,H),C.Void(.94f));U.Radial(new Rect(W*.2f,-H*.4f,W*.6f,H*1.2f),C.Cyan(.08f));
            E.Minimal(v,W);
            var command=NativeExpeditionScreen.Panels(v,controls,W,120,H-162,st.Greybox?st.World:null,(float)p.Clock,reduced);
            // While the dice reveal is open it takes the pointer: presses under it are ignored.
            bool modal=st.Dice.Open;
            if(command.intent!=ExpeditionIntent.None&&!modal)p.Submit(command);
            Bar(W,H,p,st,v,modal,now);if(st.ExitRequested)return;
            NativeExpeditionScreen.DiceOverlay(st.Dice,v,controls,W,H,now,reduced);
        }

        // The preview bar: what this is, the scenarios, the scripted step, and whose view is shown.
        private static void Bar(float W,float H,NativeExpeditionPreview p,State st,ExpeditionView v,bool modal,float now)
        {
            var bar=new Rect(24,H-150,W-48,134);NativeHudChrome.Panel(bar,1,C.Warn(),NativeHudCyber.Cut,NativeHudChrome.Corners.Diagonal,true,.96f);
            float x=bar.x+18,y=bar.y+12;
            U.Text(new Rect(x,y,560,22),"EXPEDITION PREVIEW · DEV ONLY · NOT A GAME MODE",12,C.Warn(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
            U.Text(new Rect(x,y+18,760,16),"Real rules authority. Offers, prices, quotes, the save folder, greybox anchors, the guest loopback and transport progress are preview stand-ins.",11,C.Muted(),TextAnchor.MiddleLeft);
            if(U.Btn(new Rect(bar.xMax-18-120,y,120,28),"Exit preview",false,true,1,13)&&!modal){st.ExitRequested=true;return;}
            float vx=bar.xMax-18-120-12;var members=v.members;
            for(int i=members.Length-1;i>=0;i--)
            {
                string a=members[i].account;string label=a.ToUpperInvariant()+(members[i].host?" (HOST)":"");float w=U.Measure(label,12,true).x+24;vx-=w;
                if(U.Btn(new Rect(vx,y,w,28),label,a==p.Viewer,true,1,12)&&!modal){p.Viewer=a;st.Dice.Reset(p.Shown(a));}vx-=6;
            }
            if(vx-80>x+760)U.Text(new Rect(vx-80,y,74,28),"VIEW AS",11,C.CyanSoft(),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.12f);
            float tx=x,ty=bar.y+46;
            for(int i=0;i<NativeExpeditionPreview.Scenarios.Length;i++)
            {
                var s=NativeExpeditionPreview.Scenarios[i];float w=U.Measure(s.Title,12,true).x+26;if(tx+w>bar.xMax-18-150)break;
                if(U.Btn(new Rect(tx,ty,w,28),s.Title,i==p.ScenarioIndex,true,1,12)&&!modal)st.Load(p,i,now);tx+=w+8;
            }
            if(U.Btn(new Rect(bar.xMax-18-140,ty,140,28),st.Greybox?"Greybox: on":"Greybox: off",st.Greybox,true,1,12)&&!modal)st.Greybox=!st.Greybox;
            float cy=bar.y+84,controls=4*118;string step=p.Finished?"End of scenario. ":"Step "+p.StepIndex+" / "+p.Current.Steps.Length+". ";
            float ly=cy;foreach(var line in U.Wrap(step+p.Caption,13,bar.width-36-controls-16,false,2)){U.Text(new Rect(x,ly,bar.width-36-controls,20),line,13,C.Ink(),TextAnchor.MiddleLeft);ly+=20;}
            float bx=bar.xMax-18-controls;
            if(U.Btn(new Rect(bx,cy,110,32),p.Finished?"Done":"Next step",true,!p.Finished,1,13)&&!modal){p.Next();st.AutoAt=now;}bx+=118;
            if(U.Btn(new Rect(bx,cy,110,32),"Restart",false,true,1,13)&&!modal)st.Load(p,p.ScenarioIndex,now);bx+=118;
            if(U.Btn(new Rect(bx,cy,110,32),st.Auto?"Auto: on":"Auto: off",st.Auto,true,1,13)&&!modal){st.Auto=!st.Auto;st.AutoAt=now;}bx+=118;
            if(U.Btn(new Rect(bx,cy,110,32),"Replay dice",false,v.dice.Length>0,1,13)&&v.dice.Length>0&&!modal)st.Dice.Show(v.dice[v.dice.Length-1],now);
        }
    }
}
