using System;
using System.Collections.Generic;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using D=ChipArena.UnityConversion.NativeDeployPresentation;
using H=ChipArena.UnityConversion.NativeHudChrome;
using K=ChipArena.UnityConversion.NativeExpeditionCatalogue;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
using X=ChipArena.UnityConversion.NativeExpeditionsMenu;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Expeditions screen. Start Expedition opens it, and it is the co-op room's lobby as well (LobbyScreen hands a PvE room
    /// here), so the host and guests meet on the same screen. Full-canvas cyber layout (Core/NativeExpeditionsMenu Measure): the
    /// header; the party strip across the top (live 3D portraits, host crown, ready pills, bots the host adds and removes, open
    /// seats that invite); the browser, which follows the mode; the MODE panel with the loadout row; and the action bar (Solo,
    /// Host Room, Join Room or the room's code and invite; the launch, or a guest's Ready and Send Loadout). The LOADOUT drawer
    /// holds the party bots, mirror weapons and the class and weapon pickers.
    /// <list type="bullet">
    /// <item>START EXPEDITION (owner 2026-11-06: the run is rolled by the slot machines, one all-in-one expedition): THE RUN, a
    /// briefing card. Its art is a sample spin of the Deployment Station's two machines (the station's own reel motion and
    /// destination and mutator tables), labelled a sample; its details are how a run goes. No map, no carousel.</item>
    /// <item>BOSS BATTLE (owner 2026-11-06: "add boss battle option ... to test bosses"): the roster of every boss along a
    /// rail, the picked boss's card (its emblem on a turning ring, its intro card's lines, its moves from its own table) and
    /// what can start: the Siren straight away, the rest where they are fought today.</item>
    /// </list>
    /// Presentation only: every action is a call the game already made (BeginDeploymentStation, RequestStage(Boss, StartMatch)
    /// with skipWaves, Go(Create), Go(Join), ApplyRule, GuestReady, the profile message, LeaveRoom); no room field or message is
    /// added, so a guest sees the room's mode and the boss a room fights.
    /// </summary>
    public sealed partial class ConversionGame
    {
        private int expBoss=-1,expBossFrom,expBossDir=1,expPadFrame=-1,expSeenFrame=-10,expStep=-1,expSpinLit;private float expBossAt=-10,expDrawerAt=-10,expDrawerContent=400,expAge,expModeAt=-10,expTintAt=-1;
        private X.Mode expMode,expModeShown;private bool expDrawer;private Vector2 expDrawerScroll;private Color expTint=new Color(.227f,.129f,.4f,1);
        private long expSpinIndex=-1;private readonly int[] expSpinDest=new int[3],expSpinMut=new int[3],expSpinFrom=new int[3],expSpinMutFrom=new int[3];
        private readonly List<LobbyPilot> expParty=new List<LobbyPilot>(K.PartyCap);
        private const float ExpTurn=.42f,ExpTurnCalm=.16f,ExpModeTurn=.46f,ExpMutStop=.9f,ExpMutStagger=.35f;
        private static Rect ExpRect(X.Box b)=>new Rect(b.X,b.Y,b.W,b.H);
        private static X.Box ExpBox(Rect r)=>new X.Box(r.x,r.y,r.width,r.height);
        private X.Seat ExpSeat=>!InRoom?X.Seat.Solo:Hosting?X.Seat.Host:X.Seat.Guest;
        // Live controls: the screen is in and nothing covers it (the update card, the Connection report, the open drawer).
        private bool ExpLive(float a)=>a>.5f&&!MenuBlocked();
        private static float ExpHash(float x){float s=Mathf.Sin(x*12.9898f)*43758.5453f;return s-Mathf.Floor(s);}

        private void ExpeditionsScreen(float age)
        {
            float now=Time.unscaledTime;var seat=ExpSeat;bool room=seat!=X.Seat.Solo;if(room)roomGuest=seat==X.Seat.Guest;expAge=age;
            // (By frames, not seconds: a hitch while it stays open must not reopen it and lose the mode.)
            if(Time.frameCount-expSeenFrame>2)ExpOpen(seat);expSeenFrame=Time.frameCount;
            // Guests read the host's replicated mode. No room field carries a boss, so a guest sees the one a room fights.
            if(seat==X.Seat.Guest){expMode=X.ModeOf(receivedOptions?.expedition==true);expBoss=K.DefaultBoss;}
            if(expMode!=expModeShown){expModeShown=expMode;expModeAt=now;expStep=-1;}
            var L=X.Measure(U.W,U.H);float e=In(age,0,.26f);var boss=K.BossAt(expBoss);var party=room?LobbyRoster():ExpSoloParty();
            bool busy=seat==X.Seat.Guest?server==null:server!=null;
            var go=X.Resolve(expMode,boss,seat,busy,GuestsReady);
            ExpKeys(seat,go,ExpLive(e));
            // The open drawer takes the pointer from everything under it.
            bool held=menuBlockedNow;if(expDrawer)menuBlockedNow=true;
            U.Push(Vector2.zero,Vector2.zero,L.Scale);
            try
            {
                ExpHeader(L,seat,e*In(age,0,.3f),now);
                ExpPartyStrip(L,party,seat,e,now);
                ExpBrowser(L,seat,boss,e*In(age,.08f,.34f),now);
                ExpModePanel(L,seat,boss,go,e*In(age,.12f,.34f),now);
                ExpBar(L,seat,party,go,e*In(age,.16f,.34f),now);
            }
            finally{U.Pop();menuBlockedNow=held;}
            ExpDrawer(L,seat,e,now);
        }
        // Reopening the screen keeps the room's mode and the last boss picked.
        private void ExpOpen(X.Seat seat)
        {
            if(expBoss<0)expBoss=K.DefaultBoss;expDrawer=false;expDrawerAt=-10;expBossAt=-10;expStep=-1;
            expMode=X.ModeOf(prefs.expedition);expModeShown=expMode;expModeAt=-10;
        }
        // Offline, the party is the pilot and the party bots the match will seat (LobbyBots: the same names, weapons and colours).
        private List<LobbyPilot> ExpSoloParty()
        {
            var list=expParty;list.Clear();
            list.Add(new LobbyPilot{id=1,you=true,name=NativeCosmeticRules.Name(prefs.name),weapon=prefs.weapon,tag="YOU",ready=true,connected=true,accent=HexColor(prefs.accentColor,0x70e1d0),body=HexColor(prefs.bodyColor,0xe5e7eb)});
            foreach(var b in LobbyBots(1))list.Add(new LobbyPilot{id=b.id,name=b.name,weapon=b.spec,tag="BOT",bot=true,ready=true,connected=true,accent=HexColor(b.accentColor,0xb8aabb),body=HexColor(b.bodyColor,0xe5e7eb)});
            return list;
        }
        private static int ExpBots(List<LobbyPilot> party){int n=0;foreach(var p in party)if(p.bot)n++;return n;}
        private void ExpSetBots(int n){if(expMode!=X.Mode.Expedition)ApplyRule(RoomOpt.Bots,Mathf.Clamp(n,0,K.PartyCap-1),0);}
        // A mode is the room's Versus option (2: the run, 1: the co-op boss match); Boss Battle always starts at the boss.
        private void ExpSetMode(X.Mode m)
        {
            if(server!=null||m==expMode)return;expMode=m;ApplyRule(RoomOpt.Versus,m==X.Mode.Expedition?2:1,0);if(m==X.Mode.Boss)ApplyRule(RoomOpt.SkipWaves,1,0);Click();
        }
        private void ExpPickBoss(int index,int dir)
        {
            int next=K.WrapBoss(index);if(expMode!=X.Mode.Boss)ExpSetMode(X.Mode.Boss);if(next==expBoss)return;
            expBossFrom=expBoss;expBoss=next;expBossDir=dir<0?-1:1;expBossAt=Time.unscaledTime;Click();
        }
        private void ExpToggleDrawer(){expDrawer=!expDrawer;expDrawerAt=Time.unscaledTime;if(expDrawer){expDrawerScroll=Vector2.zero;expDrawerContent=400;}Click();}
        // The launch: the calls the boss-test screen and the room lobby made.
        private void ExpLaunch(X.Launch go)
        {
            if(!go.Enabled)return;
            switch(go.Kind)
            {
                case X.LaunchKind.ExpeditionM1:BeginDeploymentStation(K.Run.Id);break;
                case X.LaunchKind.BossFight:ExpStartBoss(K.BossAt(expBoss));break;
                case X.LaunchKind.Ready:GuestReady(!networkReady);break;
            }
        }
        // The boss fights that start from here. The Siren's is the boss-test match started at her (the old Wave Defense at the
        // Siren): no waves, solo with bots or the room. A boss whose catalogue entry becomes Direct needs its start added here
        // (docs/EXPEDITIONS-MENU.md, "Boss Battle direct starts"); until then it says so instead of starting someone else's.
        private void ExpStartBoss(K.Boss b)
        {
            switch(b?.Id)
            {
                case "siren":ApplyRule(RoomOpt.SkipWaves,1,0);prefs.skipWaves=true;prefs.expedition=false;prefs.pve=true;sandboxWanted=false;RequestStage(StageLoadKind.Boss,StartMatch);break;
                default:Notice(K.FightReason(b)??"This boss has no direct start yet.");break;
            }
        }

        // ---- Keys and controller ---------------------------------------------------------------------------------------
        // Q / E, A / D or the arrows turn the boss roster; Tab, W / S or up / down switch the mode; 1-6 pick a boss; L opens the
        // loadout; Enter or Space launches (a guest's Ready). A controller: LB / RB turn, Y switches, X opens the loadout, A
        // launches, B closes the drawer or goes back. Nothing while a text field has focus or the update card is open.
        private void ExpKeys(X.Seat seat,X.Launch go,bool live)
        {
            var ev=Event.current;bool edit=X.Editable(seat);
            if(ev.type==EventType.Layout&&expPadFrame!=Time.frameCount){expPadFrame=Time.frameCount;if(live)ExpPad(seat,go,edit);}
            if(!live||ev.type!=EventType.KeyDown||GUIUtility.keyboardControl!=0||updateOpen)return;
            var k=ev.keyCode;
            if(k==KeyCode.L){ev.Use();ExpToggleDrawer();return;}
            if(expDrawer)return;
            if(k==KeyCode.Q||k==KeyCode.A||k==KeyCode.LeftArrow){ev.Use();if(edit&&expMode==X.Mode.Boss)ExpPickBoss(expBoss-1,-1);}
            else if(k==KeyCode.E||k==KeyCode.D||k==KeyCode.RightArrow){ev.Use();if(edit&&expMode==X.Mode.Boss)ExpPickBoss(expBoss+1,1);}
            else if(k==KeyCode.Tab||k==KeyCode.W||k==KeyCode.S||k==KeyCode.UpArrow||k==KeyCode.DownArrow){ev.Use();if(edit)ExpSetMode(expMode==X.Mode.Boss?X.Mode.Expedition:X.Mode.Boss);}
            else if(k>=KeyCode.Alpha1&&k<=KeyCode.Alpha9&&k-KeyCode.Alpha1<K.BossCount){ev.Use();int i=k-KeyCode.Alpha1;if(edit)ExpPickBoss(i,i>=expBoss?1:-1);}
            else if(k>=KeyCode.Keypad1&&k<=KeyCode.Keypad9&&k-KeyCode.Keypad1<K.BossCount){ev.Use();int i=k-KeyCode.Keypad1;if(edit)ExpPickBoss(i,i>=expBoss?1:-1);}
            else if(k==KeyCode.Return||k==KeyCode.KeypadEnter||k==KeyCode.Space){ev.Use();ExpLaunch(go);}
        }
        // Once per frame (the old input's joystick buttons never reach IMGUI events).
        private void ExpPad(X.Seat seat,X.Launch go,bool edit)
        {
            if(Input.GetKeyDown(KeyCode.JoystickButton2)){ExpToggleDrawer();return;}
            if(Input.GetKeyDown(KeyCode.JoystickButton1)){if(expDrawer)ExpToggleDrawer();else if(seat==X.Seat.Solo)Go(MenuScreen.Home);return;}
            if(expDrawer)return;
            if(edit&&expMode==X.Mode.Boss&&Input.GetKeyDown(KeyCode.JoystickButton4))ExpPickBoss(expBoss-1,-1);
            if(edit&&expMode==X.Mode.Boss&&Input.GetKeyDown(KeyCode.JoystickButton5))ExpPickBoss(expBoss+1,1);
            if(edit&&Input.GetKeyDown(KeyCode.JoystickButton3))ExpSetMode(expMode==X.Mode.Boss?X.Mode.Expedition:X.Mode.Boss);
            if(Input.GetKeyDown(KeyCode.JoystickButton0))ExpLaunch(go);
        }

        // ---- Backdrop and header -----------------------------------------------------------------------------------------
        // Over the panel screens' shade: the map recedes under a void veil, a pool behind the browser that takes the picked boss's
        // colour in Boss Battle (easing toward it), a cyan one at the top right, and a faint scan band drifting down.
        private static readonly float[] ExpPoolAt={0f,.5f,1f},ExpPoolA={1f,.35f,0f};
        // Drawn by HomeMenu under the top bar and the footer (ExpScreenShown).
        private bool ExpScreenShown=>menuScreen==MenuScreen.Boss||menuScreen==MenuScreen.Lobby&&prefs.pve&&InRoom;
        private Color ExpTintTarget=>expMode==X.Mode.Boss?U.Mix(U.Hex(K.BossAt(expBoss<0?K.DefaultBoss:expBoss).Accent),U.Hex(0x3a2166),.5f):U.Hex(0x3a2166);
        private void ExpBackdrop(float e,float now)
        {
            float W=U.W,Hh=U.H;
            if(Event.current.type==EventType.Repaint){float dt=expTintAt<0?1:Mathf.Clamp(now-expTintAt,0,.1f);expTintAt=now;expTint=prefs.reducedEffects?ExpTintTarget:Color.Lerp(expTint,ExpTintTarget,1-Mathf.Exp(-dt*3.5f));}
            U.Fill(new Rect(0,0,W,Hh),C.Night(.5f*e));U.Fade(new Rect(0,0,W,Hh*.4f),C.Night(.45f*e));U.Fade(new Rect(0,Hh*.62f,W,Hh*.38f),C.Night(.5f*e),false);
            float r=Mathf.Max(W,Hh)*.62f;U.RadialStops(new Rect(W*.36f-r,Hh*.66f-r,2*r,2*r),U.A(expTint,.36f*e),ExpPoolAt,ExpPoolA);
            float c=r*.7f;U.RadialStops(new Rect(W*.86f-c,Hh*.18f-c,2*c,2*c),C.Cyan(.06f*e),ExpPoolAt,ExpPoolA);
            if(!prefs.reducedEffects){float y=Mathf.Repeat(now*34,Hh+240)-120;U.Fade(new Rect(0,y-70,W,70),C.Cyan(.022f*e),false);U.Fill(new Rect(0,y,W,1),C.Cyan(.05f*e));}
        }
        private void ExpHeader(X.Layout L,X.Seat seat,float a,float now)
        {
            if(a<=.01f)return;var h=ExpRect(L.Header);bool compact=L.Compact,room=seat!=X.Seat.Solo;float y=h.y,rise=8*(1-a);y+=rise;
            U.Fill(new Rect(h.x,y+5,H.Snap(6),H.Snap(6)),C.Cyan(a));U.Fill(new Rect(h.x+10,y+7,18,H.Snap(2)),C.Magenta(.85f*a));
            // The title follows the mode, rising in as it changes.
            bool boss=expMode==X.Mode.Boss;float mk=prefs.reducedEffects?1:M.Bezier(.2f,.8f,.2f,1,Mathf.Clamp01((now-expModeAt)/.35f));
            U.Text(new Rect(h.x+36,y,420,16),boss?"PVE · BOSS BATTLE":"PVE · EXPEDITIONS",12,C.CyanSoft(.95f*a*Mathf.Lerp(.4f,1,mk)),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.26f);
            float ts=compact?28:32,ty=y+18;string title=boss?"Boss Battle":"Expeditions";U.Text(new Rect(h.x,ty+6*(1-mk),520,ts*1.25f),title,ts,C.Ink(a*mk),TextAnchor.MiddleLeft,bold:true,tracking:-.02f,shadow:C.Void(.6f*a*mk));
            // Who this viewer is in the room, after the title.
            float tw=U.Measure(title,ts,true,false,-.02f).x;string code=room?ShownRoomCode():null;
            string chip=seat==X.Seat.Solo?"SOLO":(seat==X.Seat.Host?"HOSTING":"GUEST")+(string.IsNullOrEmpty(code)?"":" · ROOM "+code);
            float cw=U.Measure(chip,10,true,true,.18f).x+26;var tag=new Rect(h.x+tw+18,ty+ts*.62f-11,cw,22);H.Tag(tag,seat==X.Seat.Guest?C.Violet():C.Cyan(),a);
            U.Text(new Rect(tag.x+12,tag.y,cw-14,22),chip,10,U.A(seat==X.Seat.Guest?U.Mix(C.Violet(),Color.white,.45f):C.CyanSoft(),a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);
            // At the right: Customize and Settings (open over the room, as the lobby's did), the Connection report in a room, and
            // the close hexagon offline (a room is left with Leave Room).
            float bx=h.xMax,by=y+8;bool live=ExpLive(a);
            if(!room){var x=new Rect(bx-40,by-2,40,40);if(ExpClose(x,a,live)){Go(MenuScreen.Home);return;}bx-=52;}
            var items=room?new[]{("activity","REPORT",2),("settings","SETTINGS",1),("palette","CUSTOMIZE",0)}:new[]{("settings","SETTINGS",1),("palette","CUSTOMIZE",0)};
            foreach(var (icon,label,act) in items)
            {
                float w=U.Measure(label,10,true,false,.16f).x+46;bx-=w;
                if(ExpChipButton(new Rect(bx,by,w,34),icon,label,9300+act,a,live)){if(act==0)Go(MenuScreen.Customize);else if(act==1)Go(MenuScreen.Settings);else OpenConnectionReport();}bx-=8;
            }
        }
        // The panel screens' close X in its small hexagon.
        private bool ExpClose(Rect x,float a,bool live)
        {
            float h=U.HoverAmount(9399,x,live),box=18*48/22f;var hx=new Rect(x.center.x-box/2,x.center.y-box/2,box,box);
            NativeIcons.DrawMarkup(hx,"hex-fill",HexFill,48,new Color(1,1,1,.9f*a));NativeIcons.DrawMarkup(hx,"hex-edge",HexEdge,48,U.A(U.Mix(C.Edge(),C.Cyan(),h),a));if(h>0)U.Glow(new Rect(x.x+8,x.y+8,24,24),C.Cyan(.2f*h*a),10);
            NativeIcons.Draw(new Rect(x.center.x-9,x.center.y-9,18,18),"x",U.A(U.Mix(C.Ink(),Color.white,h),a));bool hit=live&&U.Button(x);if(hit)Click();return hit;
        }
        // A small chamfered button: an icon and a tracked label; hover lights the edge and lifts the ink. `primary` fills it cyan.
        private bool ExpChipButton(Rect r,string icon,string label,int id,float a,bool live,bool primary=false,bool enabled=true,float size=10)
        {
            bool on=live&&enabled;float h=U.HoverAmount(id,r,on),k=enabled?1:.45f;Color fill=primary?U.Mix(C.Cyan(.9f),C.CyanSoft(),h):U.Mix(C.Indigo(.9f),U.Hex(0x1b2552,.95f),h),ink=primary?C.Void():U.Mix(C.Ink(.9f),Color.white,h);
            if(primary&&h>0)U.Glow(r,C.Cyan(.3f*h*a),10);H.Cut(r,U.A(fill,a*k),6);H.Outline(r,U.A(primary?C.CyanSoft():U.Mix(C.Edge(),C.Cyan(),h),a*k),6);
            float iw=icon!=null?16:0,tw=U.Measure(label,size,true,false,.16f).x,x=r.center.x-(iw+(icon!=null?8:0)+tw)/2;
            if(icon!=null)NativeIcons.Draw(new Rect(x,r.center.y-8,16,16),icon,U.A(primary?C.Void():U.Mix(C.CyanSoft(),C.Cyan(),h),a*k));
            U.Text(new Rect(x+iw+(icon!=null?8:0),r.y,tw+4,r.height),label,size,U.A(ink,a*k),TextAnchor.MiddleLeft,bold:true,tracking:.16f);
            bool hit=on&&U.Button(r);if(hit)Click();return hit;
        }
        // A label row's title (returns where the row's rule may start) and a tag at its right (returns the tag's left edge).
        private static float ExpLabel(Rect lr,string label,float a){float lh=lr.height-8;U.Text(new Rect(lr.x,lr.y,320,lh),label,11,C.CyanSoft(.9f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.26f);return lr.x+U.Measure(label,11,true,true,.26f).x+12;}
        private static float ExpLabelTag(Rect lr,float right,string text,Color tone,float a)
        {
            float lh=lr.height-8,w=U.Measure(text,9.5f,true,true,.16f).x+26;var t=new Rect(right-w,lr.y-2,w,lh+4);H.Tag(t,tone,a);
            U.Text(new Rect(t.x+12,t.y,w-14,t.height),text,9.5f,U.A(U.Mix(tone,Color.white,.35f),a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);return t.x;
        }
        // A fact chip (an icon and a tracked line); false when it no longer fits before `right`.
        private static bool ExpFact(ref float x,float y,float right,float h,string icon,string text,float a)
        {
            float w=U.Measure(text,9.5f,true,true,.14f).x+34;if(x+w>right)return false;var f=new Rect(x,y,w,h);H.Cut(f,C.Indigo(.75f*a),5,H.Corners.All);H.Outline(f,C.Edge(.9f*a),5,H.Corners.All);
            NativeIcons.Draw(new Rect(f.x+9,f.center.y-6.5f,13,13),icon,C.CyanSoft(.9f*a));U.Text(new Rect(f.x+27,f.y,w-28,h),text,9.5f,C.Ink(.9f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);x+=w+6;return true;
        }

        // ---- Party strip -------------------------------------------------------------------------------------------------
        // PARTY n / 4 with the ready meter (a room) or the bot note (offline), then a card per member and the open seats.
        private void ExpPartyStrip(X.Layout L,List<LobbyPilot> party,X.Seat seat,float e,float now)
        {
            float a=e*In(expAge,.04f,.3f);if(a<=.01f)return;var p=ExpRect(L.Party);bool room=seat!=X.Seat.Solo,edit=X.Editable(seat);
            int n=Mathf.Min(party.Count,L.Slots),ready=0,bots=ExpBots(party);for(int i=0;i<n;i++)if(party[i].ready&&party[i].connected)ready++;
            float lh=L.PartyLabel-8,ly=p.y;
            U.Text(new Rect(p.x,ly,90,lh),"PARTY",11,C.CyanSoft(.9f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.26f);float lx=p.x+U.Measure("PARTY",11,true,true,.26f).x+12;
            string count=n+" / "+L.Slots;U.Text(new Rect(lx,ly,80,lh),count,11,C.Ink(.9f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);lx+=U.Measure(count,11,true,true,.1f).x+14;
            string note=expMode==X.Mode.Expedition?"1–4 SIGNED-IN PLAYERS · JOIN BEFORE LAUNCH":room?(bots>0?bots+(bots==1?" BOT":" BOTS")+" · A FRIEND WHO JOINS REPLACES THE NEWEST":"FRIENDS JOIN WITH THE ROOM CODE"):"BOTS FILL THE EMPTY SEATS";
            float nw=U.Measure(note,9.5f,true,true,.16f).x;
            if(room)
            {
                bool all=n>0&&ready>=n;string meter=ready+" / "+n+" READY";float mw=U.Measure(meter,10,true,true,.16f).x+30;var pill=new Rect(p.xMax-mw,ly-3,mw,lh+6);
                H.Tag(pill,all?C.Good():C.CyanSoft(),a);U.Text(new Rect(pill.x+12,pill.y,mw-14,pill.height),meter,10,U.A(all?C.Good():C.CyanSoft(),a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
                if(lx+nw+20<pill.x-14)U.Text(new Rect(pill.x-14-nw,ly,nw+4,lh),note,9.5f,C.Muted(.85f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
                U.Fill(new Rect(lx,ly+lh/2,Mathf.Max(0,pill.x-14-(lx+nw+20<pill.x-14?nw+14:0)-lx),H.Px),C.Edge(.7f*a));
            }
            else{U.Text(new Rect(p.xMax-nw-2,ly,nw+4,lh),note,9.5f,C.Muted(.85f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);U.Fill(new Rect(lx,ly+lh/2,Mathf.Max(0,p.xMax-nw-16-lx),H.Px),C.Edge(.7f*a));}
            int newest=-1;for(int i=0;i<n;i++)if(party[i].bot)newest=i;bool canAdd=edit&&expMode!=X.Mode.Expedition&&n<L.Slots&&bots<K.PartyCap-1;
            for(int i=0;i<L.Slots;i++)
            {
                float ce=a*In(expAge,.06f+i*.04f,.32f);var slot=ExpRect(L.Slot(i));slot.y+=10*(1-ce);
                if(i<n)ExpPartyCard(slot,party[i],i,ce,now,seat,edit&&i==newest,bots);else ExpOpenSeat(slot,i,seat,i==n,canAdd,ce,now);
            }
        }
        private const string CrownMark="<path d=\"M3 17.5L4.6 7.5L9.2 11.6L12 4.5L14.8 11.6L19.4 7.5L21 17.5Z\" fill=\"#fff\"/><rect x=\"3\" y=\"18.8\" width=\"18\" height=\"2.2\" fill=\"#fff\"/>";
        private const string LockMark="<rect x=\"5\" y=\"10.5\" width=\"14\" height=\"10.5\" rx=\"2\" fill=\"#fff\"/><path d=\"M8.2 10.5V7.8a3.8 3.8 0 0 1 7.6 0v2.7\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2.2\"/>";
        private static void ExpLock(Rect r,Color c)=>NativeIcons.DrawMarkup(r,"exp-lock",LockMark,24,c);
        // A member: the portrait stage (the live 3D portrait, else the emblem) at the left over the accent's glow; the seat
        // number, the role (the host's crown), the name, the weapon and class, and the ready pill (a room) or the role chip.
        private void ExpPartyCard(Rect r,LobbyPilot p,int index,float a,float now,X.Seat seat,bool removable,int bots)
        {
            if(a<=.01f)return;var w=LobbyWeapon(p.weapon);Color tone=w!=null?U.Hex((int)w.color):U.Hex(0xb8aabb);bool room=seat!=X.Seat.Solo,ready=p.ready&&p.connected,host=room&&p.id==1;
            float al=a*(p.connected?1:.55f);bool live=ExpLive(a);float hv=U.HoverAmount(9400+index,r,live&&p.you);
            var parts=X.Party(new X.Box(r.x,r.y,r.width,r.height));var stage=ExpRect(parts.Stage);var info=ExpRect(parts.Info);
            U.Glow(new Rect(r.x,r.y+16,r.width,r.height),new Color(0,0,0,.36f*al),34);if(room&&ready)U.Glow(r,U.A(ReadyGreen,.12f*al),18);
            H.Cut(r,C.Navy(.9f*al),12);U.RadialStops(stage,U.A(p.accent,.26f*al),ExpPoolAt,ExpPoolA);U.Fade(new Rect(stage.x,stage.yMax-stage.height*.35f,stage.width,stage.height*.35f),C.Void(.5f*al),false);
            H.Scan(r,C.Cyan(.025f*al),12);
            Color edge=room&&ready?ReadyGreen:p.you?C.Cyan():C.Edge();H.Outline(r,U.A(U.Mix(edge,C.CyanSoft(),hv*.5f),(p.you||room&&ready?.85f:.75f)*al),12);
            U.Fade(new Rect(r.x+1,r.y+12,H.Snap(2),(r.height-24)/2),U.A(p.accent,.85f*al),false);U.Fade(new Rect(r.x+1,r.center.y,H.Snap(2),(r.height-24)/2),U.A(p.accent,.85f*al));
            float ring=Mathf.Min(stage.height*.3f,stage.width*.32f);LobbyPortrait(stage,new Vector2(stage.center.x,stage.center.y-4),ring,p,index,al,now,w,tone,false);
            U.Fill(new Rect(stage.xMax,r.y+14,H.Px,r.height-28),C.Edge(.55f*al));
            // the details
            float y=info.y,compact=r.height<150?1:0;string num=(index+1).ToString("00");U.Text(new Rect(info.x,y,40,16),num,12,U.A(p.accent,al),TextAnchor.MiddleLeft,mono:true);
            float tx=info.x+U.Measure(num,12,false,true).x+8;if(host){NativeIcons.DrawMarkup(new Rect(tx,y+1,14,14),"exp-crown",CrownMark,24,C.Warn(al));tx+=19;}
            string role=host?(p.you?"HOST · YOU":"HOST"):p.you?"YOU":p.bot?"BOT":"PILOT";U.Text(new Rect(tx,y,info.xMax-tx-(removable?22:0),16),role,10,U.A(host?C.Warn():p.you?C.CyanSoft():C.Muted(),al),TextAnchor.MiddleLeft,bold:true,tracking:.18f);
            y+=compact>0?19:22;float ns=compact>0?17:19;U.Text(new Rect(info.x,y,info.width,ns*1.3f),Fit(p.name,ns,info.width,true,-.01f),ns,C.Ink(al),TextAnchor.MiddleLeft,bold:true,tracking:-.01f);
            y+=ns*1.3f+(compact>0?2:4);
            string cls=w!=null?ClassName(w.@class).ToUpperInvariant():"MIXED",wn=w!=null?w.name.ToUpperInvariant():"RANDOM";NativeIcons.Draw(new Rect(info.x,y+1,16,16),w!=null?NativeIcons.Spec(w.id):"shuffle",U.A(tone,al));
            float wx=info.x+22,ww=Mathf.Min(U.Measure(wn,11,true,false,.08f).x,info.xMax-wx);U.Text(new Rect(wx,y,ww+2,18),Fit(wn,11,info.xMax-wx,true,.08f),11,U.A(U.Mix(tone,Color.white,.4f),al),TextAnchor.MiddleLeft,bold:true,tracking:.08f);
            if(wx+ww+12<info.xMax-20)U.Text(new Rect(wx+ww+8,y,info.xMax-wx-ww-8,18),"· "+cls,10,C.Muted(.9f*al),TextAnchor.MiddleLeft,bold:true,tracking:.12f);
            if(compact<=0)ClassUnderline(new Rect(info.x,y+22,Mathf.Min(info.width,ww+22),2),w?.id,tone,al,now+index*.9f);
            // the foot: the ready pill in a room; offline, the pilot's loadout link and the bots' chip
            float fy=info.yMax-24;
            if(room){if(CanPromote(p.id))PromotionButton(new Rect(info.x,fy,info.width,24),p,al);else ReadyPill(new Rect(info.x,fy,PillWidth(p),24),p,al,out _);}
            else if(p.you){if(ExpChipButton(new Rect(info.x,fy,Mathf.Min(info.width,128),24),"palette","LOADOUT",9420,al,live,false,true,9.5f))ExpToggleDrawer();}
            else{string d="BOT · "+DifficultyLabels[DifficultyIndex(prefs.botDifficulty)].ToUpperInvariant();var t=new Rect(info.x,fy,U.Measure(d,9.5f,true,true,.14f).x+24,24);H.Tag(t,C.Muted(),al);U.Text(new Rect(t.x+10,t.y,t.width-12,t.height),d,9.5f,C.Muted(al),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);}
            if(room&&p.bot){string d="BOT";var t=new Rect(info.x+PillWidth(p)+8,fy,U.Measure(d,9.5f,true,true,.14f).x+24,24);if(t.xMax<=info.xMax){H.Tag(t,C.Muted(),al);U.Text(new Rect(t.x+10,t.y,t.width-12,t.height),d,9.5f,C.Muted(al),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);}}
            // the newest bot can be removed by whoever sets the party
            if(removable)
            {
                var x=new Rect(r.xMax-28,r.y+8,20,20);float h=U.HoverAmount(9430,x,live);H.Cut(x,U.A(U.Mix(C.Indigo(.8f),C.Danger(.35f),h),al),4,H.Corners.All);H.Outline(x,U.A(U.Mix(C.Edge(),C.Danger(),h),al),4,H.Corners.All);
                NativeIcons.Draw(new Rect(x.x+3,x.y+3,14,14),"x",U.A(U.Mix(C.Muted(),Color.white,h),al));if(live&&U.Button(x)){Click();ExpSetBots(bots-1);}
            }
            if(p.you&&live&&U.Button(r)){Click();ExpToggleDrawer();}
        }
        // An open seat: a dashed chamfer. The first offers ADD BOT (to whoever sets the party) and the invite: the room's code
        // to copy, or Host Room offline; the rest stay quiet.
        private void ExpOpenSeat(Rect r,int index,X.Seat seat,bool first,bool canAdd,float a,float now)
        {
            if(a<=.01f)return;bool room=seat!=X.Seat.Solo,live=ExpLive(a);float al=a*(first?1:.6f);
            H.Cut(r,C.Navy(.42f*al),12);H.Scan(r,C.Cyan(.02f*al),12);H.Outline(r,C.Edge(.55f*al),12);H.Brackets(r,C.CyanSoft(.45f*al),12);
            float cy=r.y+r.height*(first?.3f:.42f),hr=first?18:14;var hex=new Rect(r.center.x-hr*48/22f/2,cy-hr*48/22f/2,hr*48/22f,hr*48/22f);
            NativeIcons.DrawMarkup(hex,"hex-fill",HexFill,48,new Color(1,1,1,.8f*al));NativeIcons.DrawMarkup(hex,"hex-edge",HexEdge,48,C.CyanSoft(.55f*al));NativeIcons.Draw(new Rect(r.center.x-9,cy-9,18,18),room?"users":"bot",C.CyanSoft(.9f*al));
            string title=room?"INVITE":"OPEN SEAT";U.Text(new Rect(r.x,cy+hr+6,r.width,18),title,12,C.Ink(.92f*al),TextAnchor.MiddleCenter,bold:true,tracking:.22f);
            if(!first)return;
            string code=room?ShownRoomCode():null;string line=seat==X.Seat.Guest?"The host adds bots":room?(string.IsNullOrEmpty(code)?"Share the room code":"Code "+code):expMode==X.Mode.Expedition?"Host a room to bring friends":"Add a bot or host a room";
            U.Text(new Rect(r.x+8,cy+hr+24,r.width-16,16),Fit(line,11,r.width-16,false,0),11,C.Muted(.95f*al),TextAnchor.MiddleCenter);
            if(seat==X.Seat.Guest)return;
            float by=r.yMax-34,bw=(r.width-30)/2;bool invite=room&&!string.IsNullOrEmpty(code);
            if(ExpChipButton(new Rect(r.x+10,by,bw,26),"bot","ADD BOT",9440,a,live,false,canAdd,9.5f))ExpSetBots(ExpBotsShown()+1);
            if(room){if(ExpChipButton(new Rect(r.x+20+bw,by,bw,26),"copy",inviteLink!=null?"INVITE":"CODE",9441,a,live,true,invite,9.5f)){if(inviteLink!=null){GUIUtility.systemCopyBuffer=inviteLink;Notice("Invite link copied.");}else{GUIUtility.systemCopyBuffer=code;Notice("Room code copied.");}}}
            else if(ExpChipButton(new Rect(r.x+20+bw,by,bw,26),"network","HOST ROOM",9442,a,live,true,true,9.5f)){prefs.pve=true;Go(MenuScreen.Create);}
        }
        // The bots the party shows now (a friend who joins takes the newest bot's seat), so add and remove step from what is seen.
        private int ExpBotsShown(){int n=0;if(InRoom){foreach(var s in RoomSeats)if(s!=null&&s.bot)n++;return n;}return Mathf.Min(Mathf.Clamp(prefs.bots,0,K.PartyCap-1),K.PartyCap-1);}

        // ---- The browser ---------------------------------------------------------------------------------------------------
        private void ExpBrowser(X.Layout L,X.Seat seat,K.Boss boss,float a,float now)
        {
            if(a<=.01f)return;var b=ExpRect(L.Browser);
            float k=prefs.reducedEffects?1:Mathf.Clamp01((now-expModeAt)/ExpModeTurn),q=M.Bezier(.2f,.8f,.2f,1,k);
            if(q<1)
            {
                // The other mode scans in: its body slides from the side it was switched toward under a bright line sweeping the browser.
                float dir=expMode==X.Mode.Boss?1:-1;U.Push(Vector2.zero,new Vector2(dir*24*(1-q),0),1);
                try{ExpBrowserBody(L,seat,a*Mathf.Lerp(.08f,1,q),now);}finally{U.Pop();}
                float f=1-q,sx=dir>0?b.x+b.width*q:b.xMax-b.width*q,trail=Mathf.Min(120,b.width*q);
                U.HGradient(dir>0?new Rect(sx-trail,b.y,trail,b.height):new Rect(sx,b.y,trail,b.height),dir>0?Color.clear:C.Cyan(.09f*f*a),dir>0?C.Cyan(.09f*f*a):Color.clear);
                U.Glow(new Rect(sx-1,b.y,2,b.height),C.Cyan(.45f*f*a),12);U.Fill(new Rect(sx-H.Px,b.y,H.Snap(2),b.height),C.CyanSoft(.9f*f*a));
                return;
            }
            ExpBrowserBody(L,seat,a,now);
        }
        private void ExpBrowserBody(X.Layout L,X.Seat seat,float a,float now){if(expMode==X.Mode.Boss)ExpBossBrowser(L,seat,a,now);else ExpRunBrowser(L,a,now);}

        // ---- THE RUN: the briefing -------------------------------------------------------------------------------------------
        // One expedition, rolled at the station: the machines' sample spin at the left, how a run goes at the right.
        private void ExpRunBrowser(X.Layout L,float a,float now)
        {
            var b=X.Run(L.Browser,L.Compact);var lr=ExpRect(b.Label);float lh=lr.height-8;var x=K.Run;Color acc=U.Hex(x.Accent);
            float lx=ExpLabel(lr,"THE RUN",a),tx=ExpLabelTag(lr,lr.xMax,"ONE RUN · NEW EVERY TIME",C.Cyan(),a);U.Fill(new Rect(lx,lr.y+lh/2,Mathf.Max(0,tx-12-lx),H.Px),C.Edge(.7f*a));
            var r=ExpRect(b.Card);var k=X.CardParts(ExpBox(r),L.Compact);
            U.Glow(new Rect(r.x,r.y+26,r.width,r.height),new Color(0,0,0,.5f*a),50);H.Panel(r,a,acc,16,H.Corners.Diagonal,true,.93f);
            U.RadialStops(new Rect(r.xMax-r.width*.55f,r.y,r.width*.55f,r.height*.9f),U.A(acc,.07f*a),ExpPoolAt,ExpPoolA);
            float t=ExpMachine(ExpRect(k.Art),a,now,L.Compact);ExpRunInfo(ExpRect(k.Info),k,a,now,L.Compact,t);
        }
        // The station's two machines in miniature, spinning a sample every few seconds (Core X.SpinPicks; the reels move as the
        // station's do, NativeDeployPresentation.Reel). Reduced effects: the first sample, landed and still. Returns the spin's age.
        private float ExpMachine(Rect r,float a,float now,bool compact)
        {
            bool calm=prefs.reducedEffects;Color acc=C.Cyan();var m=X.MachineParts(ExpBox(r),compact);
            H.Cut(r,C.Night(.92f*a),10,H.Corners.TL);U.RadialStops(r,U.A(U.Mix(acc,U.Hex(0x3a2166),.6f),.34f*a),ExpPoolAt,ExpPoolA);
            long index=calm?0:X.SpinAt(now);float t=calm?99:X.SpinAge(now);int nd=D.DestinationCount,nm=D.MutatorCount;
            if(index!=expSpinIndex)
            {
                // Each spin runs on from where the last one landed, as the station's reels do (the cards keep the last result).
                bool first=expSpinIndex<0;if(!first)for(int i=0;i<3;i++){expSpinFrom[i]=expSpinDest[i];expSpinMutFrom[i]=expSpinMut[i];}
                X.SpinPicks(index,nd,nm,expSpinDest,expSpinMut);if(first)for(int i=0;i<3;i++){expSpinFrom[i]=0;expSpinMutFrom[i]=expSpinMut[i];}
                expSpinIndex=index;
            }
            // the head: the machine's name and what it draws from
            var head=ExpRect(m.Head);NativeIcons.Draw(new Rect(head.x,head.center.y-7,14,14),"dices",C.CyanSoft(.95f*a));
            U.Text(new Rect(head.x+20,head.y,head.width,head.height),K.InstanceMachine,compact?9.5f:10.5f,C.Ink(.92f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            string pool="3 OF "+nd+" DESTINATIONS";U.Text(new Rect(head.x,head.y,head.width,head.height),pool,8.5f,C.Muted(.85f*a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.14f);
            // the reels, under their entrance labels; the lever beside them
            int lit=0;
            for(int i=0;i<3;i++)
            {
                var w=ExpRect(X.ReelWindow(m,i));U.Text(new Rect(w.x,w.y-m.Label,w.width,m.Label),K.Entrances[i],8.5f,C.CyanSoft(.7f*a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.3f);
                var pose=D.Reel(i,t,expSpinFrom[i],expSpinDest[i],nd);if(pose.Landed)lit|=1<<expSpinDest[i];
                ExpReel(w,pose,t-D.StopTime(i),a,calm,compact);
            }
            expSpinLit=lit;
            ExpLever(ExpRect(m.Lever),calm?0:D.Lever(t),a);
            // the mutator machine: optional, its three cards flip after the destinations land
            var mh=ExpRect(m.MutHead);float mx=mh.x;NativeIcons.Draw(new Rect(mx,mh.center.y-6,12,12),"sparkles",U.A(U.Hex(D.PolarityColor(D.Polarity.Chaos)),.9f*a));mx+=18;
            U.Text(new Rect(mx,mh.y,mh.width,mh.height),K.MutatorMachine,compact?9:9.5f,C.Ink(.85f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);mx+=U.Measure(K.MutatorMachine,compact?9:9.5f,true,true,.18f).x+10;
            float ow=U.Measure(K.MutatorOptional,8,true,true,.16f).x+16;var opt=new Rect(mx,mh.center.y-8,ow,16);U.Dashed(new Rect(opt.x,opt.y,opt.width,H.Px),C.Muted(.7f*a));U.Dashed(new Rect(opt.x,opt.yMax-H.Px,opt.width,H.Px),C.Muted(.7f*a));
            U.Text(opt,K.MutatorOptional,8,C.Muted(.9f*a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.16f);
            float ms=t-X.MutatorsAt;for(int i=0;i<3;i++)ExpMutCard(ExpRect(X.MutCard(m,i)),i,ms,a,calm,compact);
            // the foot: what is rolling, else that this is a sample
            var foot=ExpRect(m.Foot);bool rolling=!calm&&t<D.RollDuration,mrolling=!calm&&ms>=0&&ms<ExpMutStop+2*ExpMutStagger+D.Settle;
            string f=rolling?"ROLLING · "+K.InstanceMachine:mrolling?"ROLLING · "+K.MutatorMachine:K.SampleSpin;
            U.Text(foot,Fit(f,8.5f,foot.width,true,.16f),8.5f,rolling||mrolling?C.CyanSoft(.95f*a):C.Muted(.85f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
            H.Outline(r,U.A(acc,.3f*a),10,H.Corners.TL);H.Brackets(r,U.A(acc,.55f*a),12,H.Corners.TL);
            return t;
        }
        // A destination emblem's markup (the station's own) and its texture cache key, built once per emblem.
        private static readonly Dictionary<string,(string key,string svg)> ExpEmblems=new Dictionary<string,(string,string)>(StringComparer.Ordinal);
        private static (string key,string svg) ExpEmblemOf(string emblem){emblem??="";if(!ExpEmblems.TryGetValue(emblem,out var e))ExpEmblems[emblem]=e=("exp-reel-"+emblem,NativeDeployArt.EmblemMarkup(emblem));return e;}
        // A reel window: the strip of destinations moving through it (blurred while it runs), the glass and payline over it, and
        // a flash in the destination's colour as it lands. `landedFor` is the time since this reel's stop (negative while it runs).
        private void ExpReel(Rect w,D.ReelPose pose,float landedFor,float a,bool calm,bool compact)
        {
            int nd=D.DestinationCount;float symH=w.height*.78f,cy=w.center.y;int baseIdx=Mathf.FloorToInt(pose.Offset);float frac=pose.Offset-baseIdx,blur=Mathf.Clamp01(pose.Speed/16f);
            var land=D.DestinationAt(((Mathf.RoundToInt(pose.Offset)%nd)+nd)%nd);Color lc=U.Hex(land.Color);
            H.Cut(w,C.Void(.92f*a),6,H.Corners.All);
            U.BeginBand(w);
            for(int j=-1;j<=2;j++)
            {
                int idx=((baseIdx+j)%nd+nd)%nd;float y=cy+(j-frac)*symH;var s=new Rect(w.x+4,y-symH/2+3,w.width-8,symH-6);if(s.yMax<w.y||s.y>w.yMax)continue;
                ExpReelSymbol(s,D.DestinationAt(idx),a*(1-.5f*blur),blur,compact);
            }
            U.EndBand();
            U.Fade(new Rect(w.x,w.y,w.width,w.height*.26f),C.Void(.9f*a));U.Fade(new Rect(w.x,w.yMax-w.height*.26f,w.width,w.height*.26f),C.Void(.9f*a),false);
            U.Fill(new Rect(w.x+2,w.y+3,w.width-4,H.Px),new Color(1,1,1,.08f*a));
            // the payline's ticks either side
            float tk=compact?4:6;U.Fill(new Rect(w.x,cy-H.Px,tk,H.Snap(2)),U.A(pose.Landed?lc:C.CyanSoft(),.9f*a));U.Fill(new Rect(w.xMax-tk,cy-H.Px,tk,H.Snap(2)),U.A(pose.Landed?lc:C.CyanSoft(),.9f*a));
            float flash=!calm&&landedFor>=0&&landedFor<.55f?1-landedFor/.55f:0;
            if(flash>0){U.Glow(w,U.A(lc,.5f*flash*a),14);U.Fill(new Rect(w.x,w.y,w.width,w.height),U.A(lc,.12f*flash*a));}
            H.Outline(w,U.A(pose.Landed?U.Mix(lc,C.Edge(),.35f-.35f*flash):C.Edge(),(.75f+.25f*flash)*a),6,H.Corners.All);
        }
        // A destination on the reel: the station's enamel band in its colour, its emblem (the station's own markup) and its name.
        private static void ExpReelSymbol(Rect s,D.Destination d,float a,float blur,bool compact)
        {
            Color c=U.Hex(d.Color);H.Cut(s,U.A(U.Mix(C.Night(),c,.12f),.96f*a),4,H.Corners.All);U.Fill(new Rect(s.x,s.y+3,H.Snap(3),s.height-6),U.A(c,a));
            float name=compact?8.5f:9.5f,es=Mathf.Round(Mathf.Min(s.height-name*1.6f-4,s.width*.6f));
            if(es>=12){var er=new Rect(Mathf.Round(s.center.x-es/2),s.y+2,es,es);var e=ExpEmblemOf(d.Emblem);NativeIcons.DrawMarkup(er,e.key,e.svg,100,U.A(c,a));}
            U.Text(new Rect(s.x+4,s.yMax-name*1.6f,s.width-8,name*1.4f),Fit(d.Name,name,s.width-8,true,.06f),name,U.A(U.Mix(c,Color.white,.55f),a),TextAnchor.MiddleCenter,bold:true,tracking:.06f);
            if(blur>.05f)U.Fade(new Rect(s.x,s.y,s.width,s.height),U.A(c,.16f*blur*a),false);
        }
        // The lever: a slot down its housing and the red ball that drops as a roll starts.
        private static void ExpLever(Rect r,float pull,float a)
        {
            var slot=new Rect(r.center.x-2,r.y+4,4,r.height-8);H.Cut(slot,C.Void(.9f*a),2,H.Corners.All);H.Outline(slot,C.Edge(.8f*a),2,H.Corners.All);
            float y=Mathf.Lerp(r.y+8,r.yMax-8,pull),d=Mathf.Min(r.width,14);var ball=new Rect(r.center.x-d/2,y-d/2,d,d);
            U.Fill(new Rect(r.center.x-H.Px,r.y+4,H.Snap(2),y-r.y-4),C.Muted(.6f*a));U.Glow(ball,C.Danger(.4f*a),6);U.Disc(ball,C.Danger(a));U.Disc(new Rect(ball.x+d*.22f,ball.y+d*.18f,d*.3f,d*.3f),new Color(1,1,1,.55f*a));
        }
        // A mutator card: its wild-card back until the machine rolls, then it flips through the catalogue (squashing on each
        // turn) and lands on its pick, bordered in the pick's polarity. `since`: time since the mutator roll began.
        private void ExpMutCard(Rect r,int i,float since,float a,bool calm,bool compact)
        {
            int nm=D.MutatorCount;float stop=ExpMutStop+i*ExpMutStagger;bool landed=calm||since>=stop;
            if(!calm&&since<0)
            {
                // Before the roll: the last spin's card, dimmed, as the station keeps a result until the next pull.
                ExpMutFace(r,D.MutatorAt(expSpinMutFrom[i]),a*.55f,0,compact);return;
            }
            if(landed){float fl=!calm&&since-stop<.5f?1-(since-stop)/.5f:0;ExpMutFace(r,D.MutatorAt(expSpinMut[i]),a,fl,compact);return;}
            float turns=since*13f+i*.37f;int k=Mathf.FloorToInt(turns)+i*7;float squash=Mathf.Abs(Mathf.Cos((turns-Mathf.Floor(turns))*Mathf.PI));
            U.Push(r.center,Vector2.zero,1,0,Mathf.Max(.06f,squash));try{ExpMutFace(r,D.MutatorAt(((k%nm)+nm)%nm),a*.85f,0,compact);}finally{U.Pop();}
        }
        private static void ExpMutFace(Rect r,D.Mutator mu,float a,float flash,bool compact)
        {
            Color c=U.Hex(mu.Color);if(flash>0)U.Glow(r,U.A(c,.45f*flash*a),10);
            H.Cut(r,U.A(U.Mix(C.Night(),c,.14f+.12f*flash),.95f*a),5,H.Corners.All);H.Outline(r,U.A(c,(.7f+.3f*flash)*a),5,H.Corners.All);U.Fill(new Rect(r.x+5,r.y+H.Snap(2),r.width-10,H.Snap(2)),U.A(c,.8f*a));
            bool two=r.height>=34;float ns=compact?8.5f:9.5f;
            if(two)U.Text(new Rect(r.x+4,r.y+r.height*.5f-ns*1.35f,r.width-8,ns*1.2f),D.PolarityLabel(mu.Polarity),7.5f,U.A(c,.95f*a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.2f);
            U.Text(new Rect(r.x+3,two?r.y+r.height*.5f-1:r.y,r.width-6,two?ns*1.5f:r.height),Fit(mu.Name,ns,r.width-6,true,.04f),ns,U.A(U.Mix(c,Color.white,.6f),a),TextAnchor.MiddleCenter,bold:true,tracking:.04f);
        }
        // The run's details: the kicker, the title, the tag line, the facts, then how a run goes (step 01 lights while the
        // sample spins; hover lights a step).
        private void ExpRunInfo(Rect r,X.Card k,float a,float now,bool compact,float spin)
        {
            var x=K.Run;Color acc=U.Hex(x.Accent),lifted=U.Mix(acc,Color.white,.4f);float y=r.y;
            U.Text(new Rect(r.x,y-2,r.width,18),x.Kicker,11,U.A(lifted,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.24f);
            {float tw=U.Measure("ALL-IN-ONE",9,true,true,.16f).x+22;var tag=new Rect(r.xMax-tw,y-2,tw,18);H.Tag(tag,C.Violet(),a,false);U.Text(new Rect(tag.x+10,tag.y,tw-12,18),"ALL-IN-ONE",9,U.A(U.Mix(C.Violet(),Color.white,.45f),a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);}
            y+=compact?14:18;
            U.Text(new Rect(r.x-1,y,r.width+2,k.Title*1.2f),x.Title,k.Title,C.Ink(a),TextAnchor.MiddleLeft,bold:true,tracking:.05f,glow:U.A(acc,.45f*a),shadow:C.Void(.6f*a));y+=k.Title*1.2f;
            var lines=U.Wrap(x.Tagline,k.Tagline,r.width,false,k.TaglineLines);foreach(var line in lines){U.Text(new Rect(r.x,y,r.width,k.Tagline*1.4f),line,k.Tagline,C.Muted(.95f*a),TextAnchor.MiddleLeft);y+=k.Tagline*1.4f;}
            y+=(k.TaglineLines-lines.Count)*k.Tagline*1.4f+(compact?4:8);
            {float fx=r.x,fh=compact?22:26;if(ExpFact(ref fx,y,r.xMax,fh,"users",x.Party,a)&&ExpFact(ref fx,y,r.xMax,fh,"hourglass",x.Duration,a))ExpFact(ref fx,y,r.xMax,fh,"shuffle","NEW CITY EVERY RUN",a);}
            // how a run goes, pinned to the foot
            int rows=K.Steps.Length;float rh=k.Row,ry=r.yMax-rows*rh;
            if(!compact){float hy=ry-18;U.Text(new Rect(r.x,hy,160,14),"HOW A RUN GOES",9.5f,C.CyanSoft(.85f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.26f);float lw=U.Measure("HOW A RUN GOES",9.5f,true,true,.26f).x;U.Fill(new Rect(r.x+lw+12,hy+7,Mathf.Max(0,r.width-lw-12),H.Px),C.Edge(.6f*a));}
            float nx=r.x+9;U.Fill(new Rect(nx-H.Px/2,ry+rh/2,H.Px,(rows-1)*rh),U.A(acc,.35f*a));
            bool spinning=!prefs.reducedEffects&&spin<X.MutatorsAt+ExpMutStop+2*ExpMutStagger+D.Settle;bool live=ExpLive(a);
            for(int i=0;i<rows;i++)ExpStepRow(new Rect(r.x,ry+i*rh,r.width,rh),i,K.Steps[i],acc,a,now,compact,live,i==0&&spinning);
        }
        private void ExpStepRow(Rect r,int i,K.Step s,Color acc,float a,float now,bool compact,bool live,bool lit)
        {
            float h=U.HoverAmount(9620+i,r,live);if(live&&U.Hover(r)&&Event.current.type==EventType.Repaint)expStep=i;bool on=lit||expStep==i&&h>0;
            if(h>0||lit)U.HGradient(new Rect(r.x,r.y+1,r.width*.75f,r.height-2),C.Cyan(.09f*Mathf.Max(h,lit?.8f:0)*a),Color.clear);
            float cy=r.center.y,nx=r.x+9,d=compact?14:18;var node=new Rect(nx-d/2,cy-d/2,d,d);bool last=i==K.Steps.Length-1,calm=prefs.reducedEffects;
            if(on&&!calm){float p=Mathf.Repeat(now*.9f,1);U.RadialStops(new Rect(nx-d/2-8*p,cy-d/2-8*p,d+16*p,d+16*p),C.Cyan(.45f*(1-p)*a),RingStopsAt,RingStopsA);}
            if(last)Skull(new Rect(nx-8,cy-8,16,16),a,on?1:0);
            else{U.Disc(node,C.Void(a));U.RadialStops(node,U.A(on?C.Cyan():acc,.9f*a),RingStopsAt2,RingStopsA2);U.Text(node,s.Num,compact?7:8,U.A(on?C.Cyan():U.Mix(acc,Color.white,.4f),a),TextAnchor.MiddleCenter,bold:true,mono:true);}
            float tx=r.x+26,size=compact?11.5f:12.5f;Color ink=U.Mix(C.Ink(.95f),Color.white,h);
            if(compact||r.height<26)
            {
                float nw=U.Measure(s.Name,size,true,false,.06f).x;U.Text(new Rect(tx,r.y,nw+4,r.height),s.Name,size,U.A(ink,a),TextAnchor.MiddleLeft,bold:true,tracking:.06f);
                float px=tx+nw+10,pr=r.xMax-px;if(pr>40)U.Text(new Rect(px,r.y,pr,r.height),Fit(s.Short,size-1.5f,pr,false,0),size-1.5f,C.Muted(.85f*a),TextAnchor.MiddleLeft);
            }
            else
            {
                U.Text(new Rect(tx,r.y+1,r.width-26,size*1.2f),s.Name,size,U.A(ink,a),TextAnchor.MiddleLeft,bold:true,tracking:.06f);
                U.Text(new Rect(tx,r.y+size*1.2f+1,r.width-26,r.height-size*1.2f-2),Fit(s.Line,11,r.width-26,false,0),11,C.Muted(.88f*a),TextAnchor.MiddleLeft);
            }
        }
        private static readonly float[] RingStopsAt={0f,.7f,.85f,1f},RingStopsA={0f,0f,1f,0f},RingStopsAt2={0f,.62f,.72f,.9f,1f},RingStopsA2={0f,0f,1f,1f,0f};

        // ---- BOSS BATTLE: the roster ---------------------------------------------------------------------------------------
        private void ExpBossBrowser(X.Layout L,X.Seat seat,float a,float now)
        {
            var b=X.Roster(L.Browser,L.Compact);bool edit=X.Editable(seat),calm=prefs.reducedEffects,live=ExpLive(a)&&edit;var lr=ExpRect(b.Label);float lh=lr.height-8;
            float lx=ExpLabel(lr,"BOSS BATTLE",a);string counter=(expBoss+1).ToString("00")+" / "+K.BossCount.ToString("00");U.Text(new Rect(lx,lr.y,80,lh),counter,11,C.Ink(.9f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);lx+=U.Measure(counter,11,true,true,.1f).x+14;
            // the arrows with their keys at the right (a guest's view is the room's fight: no arrows, the note instead)
            float rx=lr.xMax;
            if(edit)
            {
                for(int i=1;i>=0;i--)
                {
                    var bt=new Rect(rx-34,lr.y-3,34,lh+6);float h=U.HoverAmount(9460+i,bt,live);H.Cut(bt,U.A(U.Mix(C.Indigo(.9f),U.Hex(0x1b2552),h),a),5,H.Corners.All);H.Outline(bt,U.A(U.Mix(C.Edge(),C.Cyan(),h),a),5,H.Corners.All);
                    NativeIcons.DrawMarkup(new Rect(bt.center.x-8,bt.center.y-8,16,16),i==0?"chevron-left":"chevron-right",i==0?ChevronLeft:ChevronRight,24,U.A(U.Mix(C.CyanSoft(),Color.white,h),a));
                    if(live&&U.Button(bt)){if(i==0)ExpPickBoss(expBoss-1,-1);else ExpPickBoss(expBoss+1,1);}
                    rx-=40;
                }
                rx-=4;foreach(var key in new[]{"E","Q"}){float kw=H.KeyWidth(key,18);rx-=kw;H.Key(new Rect(rx,lr.y+(lh-18)/2,kw,18),key,.9f*a);rx-=4;}
                rx-=8;
            }
            else rx=ExpLabelTag(lr,rx,"THE ROOM'S FIGHT",C.Violet(),a)-12;
            U.Fill(new Rect(lx,lr.y+lh/2,Mathf.Max(0,rx-lx),H.Px),C.Edge(.7f*a));
            // the card, turning while the pick changes: the old one slides out toward the side it was turned from as the new one
            // arrives from the other (Reduced effects: a cross-fade in place)
            var card=ExpRect(b.Card);float since=now-expBossAt,k=Mathf.Clamp01(since/(calm?ExpTurnCalm:ExpTurn));
            if(k<1&&expBossFrom!=expBoss){float q=M.Bezier(.2f,.8f,.2f,1,k);ExpBossTurn(card,expBossFrom,q,true,a,now,calm,L.Compact,since);ExpBossTurn(card,expBoss,q,false,a,now,calm,L.Compact,since);}
            else ExpBossCard(card,K.BossAt(expBoss),a,now,L.Compact,since);
            ExpBossRail(ExpRect(b.Rail),edit,a,now,L.Compact);
        }
        private void ExpBossTurn(Rect card,int index,float q,bool leaving,float a,float now,bool calm,bool compact,float since)
        {
            float off=card.width*.16f,x=leaving?-expBossDir*off*q:expBossDir*off*(1-q),s=leaving?1-.05f*q:.95f+.05f*q,al=leaving?1-q:q;if(calm){x=0;s=1;}
            if(al<=.01f)return;U.Push(card.center,new Vector2(x,0),s);try{ExpBossCard(card,K.BossAt(index),a*al,now,compact,leaving?99:since);}finally{U.Pop();}
        }
        // The picked boss: its art at the left (the emblem on its ring, the intro card's line), the eyebrow, name, blurb, facts
        // and moves at the right, in its accent. `fresh`: seconds since it was picked (the arrival's pop, burst and glitch).
        private void ExpBossCard(Rect r,K.Boss x,float a,float now,bool compact,float fresh)
        {
            if(a<=.01f)return;Color acc=U.Hex(x.Accent);var k=X.CardParts(ExpBox(r),compact);
            U.Glow(new Rect(r.x,r.y+26,r.width,r.height),new Color(0,0,0,.5f*a),50);H.Panel(r,a,acc,16,H.Corners.Diagonal,true,.93f);
            U.RadialStops(new Rect(r.xMax-r.width*.6f,r.y,r.width*.6f,r.height),U.A(acc,.09f*a),ExpPoolAt,ExpPoolA);
            ExpBossArt(ExpRect(k.Art),x,a,now,compact,fresh);ExpBossInfo(ExpRect(k.Info),x,k,a,now,compact,fresh);
        }
        private static readonly float[] ExpAuraAt={0f,.42f,1f},ExpAuraA={1f,.4f,0f};
        private void ExpBossArt(Rect r,K.Boss x,float a,float now,bool compact,float fresh)
        {
            Color acc=U.Hex(x.Accent);bool calm=prefs.reducedEffects,dim=x.Fight==K.BossFight.Preview;float br=calm?.5f:.5f+.5f*Mathf.Sin(now*1.6f);
            H.Cut(r,C.Night(.95f*a),10,H.Corners.TL);
            float foot=compact?26:40;var stage=new Rect(r.x,r.y,r.width,r.height-foot);var cen=new Vector2(stage.center.x,stage.y+stage.height*.52f);float R=Mathf.Round(Mathf.Min(stage.width,stage.height)*.36f);
            U.BeginBand(r);
            U.RadialStops(new Rect(cen.x-R*2.4f,cen.y-R*2f,R*4.8f,R*4f),U.A(U.Mix(acc,U.Hex(0x3a2166),.4f),(.42f+.12f*br)*a),ExpAuraAt,ExpAuraA);
            // the floor: lines receding toward the horizon under the emblem
            float hz=cen.y+R*.9f;for(int i=0;i<7;i++){float t=i/6f,y=hz+(r.yMax-hz)*t*t;U.Fill(new Rect(r.x,y,r.width,H.Px),U.A(acc,(.05f+.12f*t)*a));}
            for(int i=-4;i<=4;i++){float bx=cen.x+i*R*.55f;U.Fill(new Rect(bx-H.Px/2,hz,H.Px,r.yMax-hz),U.A(acc,.05f*a));}
            // the rings: a fine ring, ticks turning slowly round it (majors every sixth) and a dotted arc turning the other way
            U.RadialStops(new Rect(cen.x-R*1.18f,cen.y-R*1.18f,R*2.36f,R*2.36f),U.A(acc,(.35f+.15f*br)*a),RingStopsAt,RingStopsA);
            float spin=calm?0:now*7f;
            for(int i=0;i<48;i++){bool major=i%6==0;float len=major?R*.14f:R*.06f;U.Push(cen,Vector2.zero,1,i*7.5f+spin);U.Fill(new Rect(cen.x-(major?1:.5f),cen.y-R*1.32f-len,major?2:1,len),U.A(acc,(major?.7f:.35f)*a));U.Pop();}
            float back=calm?0:-now*16f;for(int i=0;i<30;i++){if(i%10>6)continue;float ang=(i*12f+back)*Mathf.Deg2Rad;var d=new Rect(cen.x+Mathf.Sin(ang)*R*1.02f-1.5f,cen.y-Mathf.Cos(ang)*R*1.02f-1.5f,3,3);U.Disc(d,U.A(U.Mix(acc,Color.white,.3f),.55f*a));}
            // the arrival: a ring bursting outward and the emblem settling from a little larger
            if(!calm&&fresh>=0&&fresh<.9f){float f=fresh/.9f,rr=R*(.7f+1.3f*f);U.RadialStops(new Rect(cen.x-rr,cen.y-rr,2*rr,2*rr),U.A(Color.Lerp(acc,Color.white,.4f),.75f*(1-f)*a),RingStopsAt,RingStopsA);}
            float pop=calm||fresh<0?1:M.Bezier(.2f,.8f,.2f,1,Mathf.Clamp01(fresh/.55f));
            U.Radial(new Rect(cen.x-R*1.3f,cen.y-R,R*2.6f,R*2f),U.A(acc,(.22f+.1f*br)*a*pop));
            float es=Mathf.Round(R*1.6f);var er=new Rect(cen.x-es/2,cen.y-es/2,es,es);
            U.Push(cen,new Vector2(0,calm?0:Mathf.Sin(now*1.1f)*2),1.14f-.14f*pop);try{ExpEmblem(er,x,U.A(dim?U.Mix(acc,C.Muted(),.35f):acc,a*Mathf.Lerp(.2f,1,pop)));}finally{U.Pop();}
            // motes rising through the light, and a scan line
            if(!calm)
            {
                for(int i=0;i<16;i++){float life=Mathf.Repeat(now*(.07f+.06f*ExpHash(i*3.1f+.4f))+ExpHash(i*1.7f),1),mx=r.x+r.width*(.06f+.88f*ExpHash(i*5.3f)),my=stage.yMax-life*stage.height*.95f,ma=Mathf.Sin(life*Mathf.PI)*.8f,ds=i%3==0?3:2;
                    var d=new Rect(mx-ds/2,my-ds/2,ds,ds);U.Glow(d,U.A(acc,.3f*ma*a),3);U.Disc(d,U.A(U.Mix(acc,Color.white,.5f),ma*a));}
                float sy=r.y+Mathf.Repeat(now*.3f,1.3f)*r.height;U.Fade(new Rect(r.x,sy-44,r.width,44),U.A(acc,.05f*a),false);U.Fill(new Rect(r.x,sy,r.width,H.Px),U.A(acc,.16f*a));
            }
            U.EndBand();
            // the intro card's line at the foot between two rules
            var ft=new Rect(r.x+14,r.yMax-foot,r.width-28,foot-(compact?6:10));float ls=compact?8.5f:10;string line=x.Line??"";float lw=Mathf.Min(U.Measure(line,ls,true,true,.3f).x,ft.width-40);
            U.Fill(new Rect(ft.x,ft.center.y,Mathf.Max(0,ft.width/2-lw/2-12),H.Px),U.A(acc,.5f*a));U.Fill(new Rect(ft.center.x+lw/2+12,ft.center.y,Mathf.Max(0,ft.width/2-lw/2-12),H.Px),U.A(acc,.5f*a));
            U.Text(ft,line,ls,U.A(U.Mix(acc,Color.white,.55f),a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.3f,glow:U.A(acc,.3f*a));
            // the state stamp at the top left
            ExpFightStamp(new Vector2(r.x+12,r.y+12),x.Fight,a,now,compact);
            H.Outline(r,U.A(acc,.34f*a),10,H.Corners.TL);H.Brackets(r,U.A(acc,.62f*a),12,H.Corners.TL);
        }
        private static Color ExpFightTone(K.BossFight f)=>f==K.BossFight.Direct?C.Good():f==K.BossFight.InRun?C.Warn():C.Muted();
        // FIGHT NOW pulses; IN RUNS and IN DEVELOPMENT sit still.
        private void ExpFightStamp(Vector2 at,K.BossFight f,float a,float now,bool compact)
        {
            string s=K.FightLabel(f);Color tone=ExpFightTone(f);float fs=compact?8:9,w=U.Measure(s,fs,true,true,.16f).x+(f==K.BossFight.Direct?26:18),h=compact?16:18;var t=new Rect(at.x,at.y,w,h);
            H.Tag(t,tone,a,false);float dx=t.x+8;
            if(f==K.BossFight.Direct){float p=prefs.reducedEffects?0:Mathf.Repeat(now*.8f,1);var dot=new Rect(t.x+7,t.center.y-2.5f,5,5);if(p>0)U.RadialStops(new Rect(dot.x-6*p,dot.y-6*p,5+12*p,5+12*p),U.A(tone,.5f*(1-p)*a),RingStopsAt,RingStopsA);U.Disc(dot,U.A(tone,a));dx+=10;}
            U.Text(new Rect(dx,t.y,w,h),s,fs,U.A(U.Mix(tone,Color.white,.25f),a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
        }
        // Each boss's emblem (Core catalogue: white on a 100-unit box, near-black parts reading as cut-outs; the Siren's face is
        // the lobby's, on 200), tinted by the draw. Drawn at a fixed size per layout (scale goes through U.Push), so each is
        // rasterised once per size.
        private static void ExpEmblem(Rect r,K.Boss x,Color c)
        {
            if(x.Emblem==null)NativeIcons.DrawMarkup(new Rect(r.x,r.y+r.height*.14f,r.width,r.height),"siren-face",SirenFace,200,c);
            else NativeIcons.DrawMarkup(r,x.EmblemKey,x.Emblem,100,c);
        }
        private static readonly Dictionary<string,string> ExpMoveLines=new Dictionary<string,string>(StringComparer.Ordinal);
        private static string ExpMovesOf(K.Boss x){if(!ExpMoveLines.TryGetValue(x.Id,out var s))ExpMoveLines[x.Id]=s=string.Join("  ·  ",x.Moves??Array.Empty<string>());return s;}
        private void ExpBossInfo(Rect r,K.Boss x,X.Card k,float a,float now,bool compact,float fresh)
        {
            Color acc=U.Hex(x.Accent),lifted=U.Mix(acc,Color.white,.4f);float y=r.y;
            // the eyebrow and the subtitle; the state at the right
            Color tone=ExpFightTone(x.Fight);string st=K.FightLabel(x.Fight);float sw=U.Measure(st,9,true,true,.16f).x+20;var chip=new Rect(r.xMax-sw,y-2,sw,18);H.Tag(chip,tone,a,false);U.Text(new Rect(chip.x+10,chip.y,sw-12,18),st,9,U.A(U.Mix(tone,Color.white,.25f),a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
            float ew=U.Measure(x.Eyebrow,11,true,true,.24f).x;U.Text(new Rect(r.x,y-2,chip.x-r.x-8,18),x.Eyebrow,11,U.A(lifted,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.24f);
            string sub="·  "+x.Subtitle;float subw=U.Measure(sub,9.5f,true,true,.2f).x;if(r.x+ew+12+subw<chip.x-8)U.Text(new Rect(r.x+ew+10,y-2,subw+4,18),sub,9.5f,C.Muted(.9f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            y+=compact?14:18;
            // the name, glowing in the accent, with a glitch as it arrives
            var tr=new Rect(r.x-1,y,r.width+2,k.Title*1.2f);H.Glitch(tr,x.Name,k.Title,fresh-ExpTurn*.55f,a,TextAnchor.MiddleLeft,prefs.reducedEffects,true,false,.05f);
            U.Text(tr,x.Name,k.Title,C.Ink(a),TextAnchor.MiddleLeft,bold:true,tracking:.05f,glow:U.A(acc,.5f*a),shadow:C.Void(.6f*a));y+=k.Title*1.2f;
            var lines=U.Wrap(x.Blurb,k.Tagline,r.width,false,k.TaglineLines);foreach(var line in lines){U.Text(new Rect(r.x,y,r.width,k.Tagline*1.4f),line,k.Tagline,C.Muted(.95f*a),TextAnchor.MiddleLeft);y+=k.Tagline*1.4f;}
            y+=(k.TaglineLines-lines.Count)*k.Tagline*1.4f+(compact?4:8);
            {float fx=r.x,fh=compact?22:26;if(ExpFact(ref fx,y,r.xMax,fh,"target",x.Area,a)&&ExpFact(ref fx,y,r.xMax,fh,"layers",x.Phases+" PHASES",a))ExpFact(ref fx,y,r.xMax,fh,"swords",x.Moves.Length+" MOVES",a);}
            // its moves, pinned to the foot
            int ml=compact?1:2;float lh=compact?15:18,my=r.yMax-ml*lh;
            float hy=my-(compact?16:20);U.Text(new Rect(r.x,hy,200,14),"SIGNATURE MOVES",9.5f,C.CyanSoft(.85f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.26f);float hw=U.Measure("SIGNATURE MOVES",9.5f,true,true,.26f).x;U.Fill(new Rect(r.x+hw+12,hy+7,Mathf.Max(0,r.width-hw-12),H.Px),U.A(acc,.3f*a));
            float ms=compact?11:12;var moves=U.Wrap(ExpMovesOf(x),ms,r.width,true,ml);for(int i=0;i<moves.Count;i++)U.Text(new Rect(r.x,my+i*lh,r.width,lh),moves[i],ms,U.A(U.Mix(C.Ink(.9f),acc,.15f),a),TextAnchor.MiddleLeft,bold:true);
        }
        // The rail: every boss as a tile, the pick lit in its accent with brackets and its bar; hover lifts a tile, a click picks it.
        private void ExpBossRail(Rect rail,bool edit,float a,float now,bool compact)
        {
            int n=K.BossCount;bool live=ExpLive(a)&&edit;
            for(int i=0;i<n;i++)
            {
                var x=K.BossAt(i);var t=ExpRect(X.RailTile(ExpBox(rail),i,n));float te=a*In(expAge,.14f+i*.035f,.3f);if(te<=.01f)continue;bool on=i==expBoss;
                float h=U.HoverAmount(9600+i,t,live&&!on);t.y-=3*h+6*(1-te/Mathf.Max(.01f,a));Color acc=U.Hex(x.Accent),tone=ExpFightTone(x.Fight);
                if(on)U.Glow(t,U.A(acc,.22f*te),12);
                H.Cut(t,U.A(on?U.Mix(C.Navy(.95f),acc,.18f):U.Mix(C.Indigo(.78f),U.Hex(0x18244e,.9f),h),te),8);H.Scan(t,U.A(acc,.03f*te),8);
                H.Outline(t,U.A(on?acc:U.Mix(C.Edge(),acc,.2f+.5f*h),te*(on?1:.85f)),8);
                if(on){U.Fill(new Rect(t.x+10,t.yMax-H.Snap(2)-2,t.width-20,H.Snap(2)),U.A(acc,te));H.Brackets(new Rect(t.x-3,t.y-3,t.width+6,t.height+6),U.A(U.Mix(acc,Color.white,.3f),.85f*te),8);}
                float tx=t.x+10;bool art=t.width>=124;
                if(art){float es=Mathf.Round(Mathf.Min(t.height-14,compact?26:36));var er=new Rect(t.x+8,t.center.y-es/2,es,es);ExpEmblem(er,x,U.A(on?acc:U.Mix(acc,C.Muted(),.35f+.35f*(1-h)),te));tx=er.xMax+8;}
                float ns=compact?10:11.5f,room=t.xMax-8-tx;
                U.Text(new Rect(tx,t.center.y-(compact?14:17),room,ns*1.3f),Fit(x.Name,ns,room,true,.04f),ns,U.A(U.Mix(C.Ink(on?1:.85f),Color.white,h),te),TextAnchor.MiddleLeft,bold:true,tracking:.04f);
                var dot=new Rect(tx,t.center.y+(compact?5:7),5,5);U.Disc(dot,U.A(tone,te));
                U.Text(new Rect(tx+9,t.center.y+(compact?1:2),room-9,14),K.FightShort(x.Fight),compact?7.5f:8.5f,U.A(tone,.9f*te),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
                if(!compact)U.Text(new Rect(t.xMax-26,t.y+4,20,12),(i+1).ToString(),8.5f,C.Muted(.7f*te),TextAnchor.MiddleRight,bold:true,mono:true);
                if(live&&!on&&U.Button(t))ExpPickBoss(i,i>expBoss?1:-1);
            }
        }

        // ---- MODE ----------------------------------------------------------------------------------------------------------
        private void ExpModePanel(X.Layout L,X.Seat seat,K.Boss boss,X.Launch go,float a,float now)
        {
            if(a<=.01f)return;var m=X.Panel(L.Mode,L.Compact);bool edit=X.Editable(seat),compact=L.Compact,live=ExpLive(a);var lr=ExpRect(m.Label);float lh=lr.height-8;
            U.Text(new Rect(lr.x,lr.y,200,lh),"MODE",11,C.CyanSoft(.9f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.26f);float lx=lr.x+U.Measure("MODE",11,true,true,.26f).x+12,rx=lr.xMax;
            if(edit){U.Text(new Rect(rx-60,lr.y,60,lh),"SWITCH",9.5f,C.Muted(.85f*a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.16f);rx-=U.Measure("SWITCH",9.5f,true,true,.16f).x+8;float kw=H.KeyWidth("TAB",18);rx-=kw;H.Key(new Rect(rx,lr.y+(lh-18)/2,kw,18),"TAB",.9f*a);rx-=12;}
            U.Fill(new Rect(lx,lr.y+lh/2,Mathf.Max(0,rx-lx),H.Px),C.Edge(.7f*a));
            if(ExpModeTile(ExpRect(m.Expedition),0,"route",X.ExpeditionLabel,compact?X.ExpeditionLineShort:X.ExpeditionLine,"1–4 PLAYERS",C.Violet(),expMode==X.Mode.Expedition,edit,a,now,compact))ExpSetMode(X.Mode.Expedition);
            if(ExpModeTile(ExpRect(m.Boss),1,"boss",X.BossLabel,compact?X.BossLineShort:X.BossLine,"BOTS · UP TO 4",C.Good(),expMode==X.Mode.Boss,edit,a,now,compact))ExpSetMode(X.Mode.Boss);
            var d=ExpRect(m.Detail);if(expMode==X.Mode.Boss)ExpFightNotes(d,boss,go,edit,a,compact);else ExpMachinePool(d,go,a,now,compact);
            ExpLoadoutRow(ExpRect(m.Loadout),seat,a,now,live,compact);
        }
        // A mode: its constellation glyph in a hexagon, the title and line, the availability chip at the top right, a radio at
        // the right; picked: the cyan edge, glow and brackets. A guest's tiles are the host's mode, read-only.
        private bool ExpModeTile(Rect r,int index,string glyph,string title,string line,string chip,Color chipTone,bool on,bool edit,float a,float now,bool compact)
        {
            bool live=ExpLive(a)&&edit;float h=U.HoverAmount(9500+index,r,live),s=on?1:0;
            if(on)U.Glow(r,C.Cyan(.14f*a),14);H.Cut(r,U.A(U.Mix(U.Mix(C.Indigo(.78f),U.Hex(0x18244e,.9f),h),U.Hex(0x0b3049,.92f),s),a),10);H.Scan(r,C.Cyan(.03f*a),10);
            H.Outline(r,U.A(on?C.Cyan():U.Mix(C.Edge(),C.CyanSoft(),h*.7f),a),10);if(on){U.Fill(new Rect(r.x+1,r.y+10,H.Snap(3),r.height-20),C.Cyan(a));H.Brackets(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),C.CyanSoft(.8f*a),10);if(!prefs.reducedEffects)Sheen(r,.35f*a);}
            float hr=compact?14:19,cx=r.x+(compact?26:34),cy=r.center.y;var hex=new Rect(cx-hr*48/22f/2,cy-hr*48/22f/2,hr*48/22f,hr*48/22f);
            NativeIcons.DrawMarkup(hex,"hex-fill",HexFill,48,new Color(1,1,1,.9f*a));NativeIcons.DrawMarkup(hex,"hex-edge",HexEdge,48,U.A(on?C.Cyan():C.CyanSoft(.5f),a));
            Constellation(new Rect(cx-hr*.62f,cy-hr*.62f,hr*1.24f,hr*1.24f),glyph,on?C.Cyan():U.Mix(C.CyanSoft(.8f),C.Cyan(),h),a);
            float tx=cx+hr+(compact?12:16),cw=U.Measure(chip,8.5f,true,true,.14f).x+22,ty=compact?r.y+r.height/2-10:r.y+16;
            var c=new Rect(r.xMax-cw-(compact?40:46),compact?cy-9:r.y+12,cw,18);H.Tag(c,chipTone,a);U.Text(new Rect(c.x+10,c.y,cw-12,18),chip,8.5f,U.A(U.Mix(chipTone,Color.white,.25f),a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
            float ts=compact?14:17;U.Text(new Rect(tx,ty,c.x-tx-8,ts*1.25f),Fit(title,ts,c.x-tx-8,true,.08f),ts,U.A(U.Mix(C.Ink(on?1:.88f),Color.white,h),a),TextAnchor.MiddleLeft,bold:true,tracking:.08f);
            if(!compact)U.Text(new Rect(tx,ty+ts*1.25f+2,r.xMax-tx-50,18),Fit(line,12,r.xMax-tx-50,false,0),12,C.Muted(.95f*a),TextAnchor.MiddleLeft);
            else U.Text(new Rect(tx,ty+ts*1.2f,c.x-tx-8,14),Fit(line,10.5f,c.x-tx-8,false,0),10.5f,C.Muted(.9f*a),TextAnchor.MiddleLeft);
            // the radio
            var rr=new Rect(r.xMax-(compact?28:32),cy-8,16,16);U.RadialStops(rr,U.A(on?C.Cyan():C.Edge(),a),RingStopsAt2,RingStopsA2);if(on){var dot=new Rect(rr.x+4.5f,rr.y+4.5f,7,7);U.Glow(dot,C.Cyan(.5f*a),4);U.Disc(dot,C.Cyan(a));}
            return live&&U.Button(r);
        }
        // Under the run's tile: what the Instance Machine draws from, each destination with the boss at its end. The ones the
        // sample spin just landed on light up; then why the run cannot start, when it cannot.
        private void ExpMachinePool(Rect d,X.Launch go,float a,float now,bool compact)
        {
            float lh=compact?16:20,head=compact?16:20;int n=D.DestinationCount;
            U.Text(new Rect(d.x,d.y,d.width,head-4),"IN THE INSTANCE MACHINE",9.5f,C.CyanSoft(.85f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.24f);
            U.Text(new Rect(d.x,d.y,d.width,head-4),n+" DESTINATIONS · 3 A RUN",8.5f,C.Muted(.8f*a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.14f);
            bool note=!go.Enabled&&go.Kind!=X.LaunchKind.Ready&&!string.IsNullOrEmpty(go.Reason);float room=d.height-head-(note?lh+4:0);
            int perCol=Mathf.Max(1,Mathf.FloorToInt(room/lh)),cols=perCol>=n?1:2;perCol=Mathf.CeilToInt(n/(float)cols);float cw=(d.width-(cols-1)*14)/cols;
            for(int i=0;i<n;i++)
            {
                int col=i/perCol,row=i%perCol;var r=new Rect(d.x+col*(cw+14),d.y+head+row*lh,cw,lh);if(r.yMax>d.yMax+1)break;
                var dest=D.DestinationAt(i);Color c=U.Hex(dest.Color);bool lit=(expSpinLit&(1<<i))!=0;var boss=K.BossOf(dest.Id);
                if(lit)U.HGradient(new Rect(r.x,r.y+1,r.width*.8f,r.height-2),U.A(c,.12f*a),Color.clear);
                var dot=new Rect(r.x+3,r.center.y-3,6,6);if(lit)U.Glow(dot,U.A(c,.55f*a),5);U.Disc(dot,U.A(c,(lit?1:.65f)*a));
                float ns=compact?10.5f:11.5f,nw=U.Measure(dest.Name,ns,true,false,.06f).x;U.Text(new Rect(r.x+16,r.y,nw+4,r.height),dest.Name,ns,U.A(U.Mix(C.Ink(lit?1:.85f),c,lit?.25f:0),a),TextAnchor.MiddleLeft,bold:true,tracking:.06f);
                if(boss!=null){string b="→ "+boss.Name;float bx=r.x+16+nw+10,br=r.xMax-bx;if(br>40)U.Text(new Rect(bx,r.y,br,r.height),Fit(b,ns-1.5f,br,false,0),ns-1.5f,C.Muted(.85f*a),TextAnchor.MiddleRight);}
            }
            if(note){float y=d.yMax-lh;ExpNote(ref y,d,"circle-alert",go.Reason,C.Warn(),a,lh);}
        }
        // Under Boss Battle's tile: what this fight is today and how to reach it.
        private void ExpFightNotes(Rect d,K.Boss boss,X.Launch go,bool edit,float a,bool compact)
        {
            float lh=compact?16:19,head=compact?16:20,y=d.y+head;
            U.Text(new Rect(d.x,d.y,d.width,head-4),"THE FIGHT",9.5f,C.CyanSoft(.85f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.24f);
            U.Text(new Rect(d.x,d.y,d.width,head-4),edit?"1 – "+K.BossCount+" PICK":"SET BY THE HOST",8.5f,C.Muted(.8f*a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.14f);
            if(boss==null)return;
            switch(boss.Fight)
            {
                case K.BossFight.Direct:
                    ExpNote(ref y,d,"route-dot","Straight into her arena: no waves, no quota.",C.Ink(.9f),a,lh);
                    ExpNote(ref y,d,"route-dot","Bots fill the empty seats; friends join with the room code.",C.Muted(),a,lh);
                    if(!compact)ExpNote(ref y,d,"route-dot","Offline, Restart Match in the pause menu fights her again.",C.Muted(),a,lh);break;
                case K.BossFight.InRun:
                    ExpNote(ref y,d,"route-dot","Waits at the end of "+boss.Area+", once its kill quota is met.",C.Ink(.9f),a,lh);
                    ExpNote(ref y,d,"route-dot","To meet it: Start Expedition and roll "+K.DestinationName(boss.Destination)+".",C.Muted(),a,lh);break;
                default:
                    ExpNote(ref y,d,"route-dot","Its model, moves, cutscene and song are built.",C.Ink(.9f),a,lh);
                    ExpNote(ref y,d,"route-dot","DEV accounts can watch it: DEV panel · Camera · Final boss preview.",C.Muted(),a,lh);break;
            }
            if(!go.Enabled&&go.Kind!=X.LaunchKind.Ready&&!string.IsNullOrEmpty(go.Reason)&&y+lh<=d.yMax+1){y+=compact?2:4;ExpNote(ref y,d,"circle-alert",boss.Fight==K.BossFight.Direct?go.Reason:boss.Fight==K.BossFight.InRun?"No direct start yet: that is the next step.":"Not fightable until its rules are bound.",C.Warn(),a,lh);}
        }
        private void ExpNote(ref float y,Rect d,string icon,string text,Color tone,float a,float lh)
        {
            if(y+lh>d.yMax+1)return;if(icon=="route-dot"){U.Fill(new Rect(d.x+4,y+lh/2-2,4,4),C.CyanSoft(.8f*a));}else NativeIcons.Draw(new Rect(d.x,y+lh/2-7,14,14),icon,U.A(tone,a));
            var lines=U.Wrap(text,12,d.width-22,false,2);foreach(var l in lines){if(y+lh>d.yMax+1)return;U.Text(new Rect(d.x+20,y,d.width-20,lh),l,12,U.A(tone,a),TextAnchor.MiddleLeft);y+=lh;}
        }
        // LOADOUT: the weapon and class, the party bots and their weapons, and the drawer's button (L).
        private void ExpLoadoutRow(Rect r,X.Seat seat,float a,float now,bool live,bool compact)
        {
            var w=LobbyWeapon(prefs.weapon);Color tone=w!=null?U.Hex((int)w.color):C.CyanSoft();float h=U.HoverAmount(9540,r,live);
            H.Cut(r,U.A(U.Mix(C.Navy(.85f),U.Hex(0x131c40,.92f),h),a),8);H.Outline(r,U.A(U.Mix(C.Edge(),C.Cyan(),h*.8f),a),8);U.Fill(new Rect(r.x+1,r.y+8,H.Snap(2),r.height-16),U.A(tone,.8f*a));
            float ic=compact?24:30;NativeIcons.Draw(new Rect(r.x+14,r.center.y-ic/2,ic,ic),w!=null?NativeIcons.Spec(w.id):"shuffle",U.A(tone,a));float tx=r.x+14+ic+12;
            string wn=(w!=null?w.name:prefs.weapon).ToUpperInvariant()+(w!=null?"  ·  "+ClassName(w.@class).ToUpperInvariant():"");
            var o=RulesNow();int bots=ExpBotsShown();string party=expMode==X.Mode.Expedition?"NO BOTS IN A RUN":bots==0?"NO BOTS":bots+(bots==1?" BOT":" BOTS")+((o?.mirrorWeapons??prefs.mirrorWeapons)?" · MIRROR":" · OWN LOADOUTS");
            var btn=new Rect(r.xMax-(compact?118:134),r.y+(r.height-(compact?28:32))/2,compact?108:124,compact?28:32);
            U.Text(new Rect(tx,compact?r.y+4:r.y+8,btn.x-tx-8,16),Fit("LOADOUT  ·  "+party,9,btn.x-tx-8,true,.2f),9,C.CyanSoft(.85f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            U.Text(new Rect(tx,compact?r.y+18:r.y+24,btn.x-tx-8,20),Fit(wn,compact?12:13,btn.x-tx-8,true,.08f),compact?12:13,U.A(U.Mix(tone,Color.white,.5f),a),TextAnchor.MiddleLeft,bold:true,tracking:.08f);
            bool click=ExpChipButton(btn,"palette",expDrawer?"CLOSE":"EDIT",9541,a,live,false,true,10);float kw=H.KeyWidth("L",16);H.Key(new Rect(btn.xMax-kw-6,btn.center.y-8,kw,16),"L",.8f*a);
            if(click||live&&U.Button(r)){if(!click)Click();ExpToggleDrawer();}
        }

        // ---- The action bar --------------------------------------------------------------------------------------------------
        private void ExpBar(X.Layout L,X.Seat seat,List<LobbyPilot> party,X.Launch go,float a,float now)
        {
            if(a<=.01f)return;var r=ExpRect(L.Bar);r.y+=8*(1-a);bool live=ExpLive(a),compact=L.Compact;
            U.Glow(new Rect(r.x,r.y+14,r.width,r.height),new Color(0,0,0,.4f*a),30);H.Panel(r,a,C.Cyan(),12,H.Corners.Diagonal,true,.9f);
            float pad=12,x=r.x+pad,bh=r.height-16,by=r.y+8;
            if(seat==X.Seat.Solo)x=ExpPlaySegment(new Rect(x,by,0,bh),a,live,compact);else x=ExpRoomBlock(new Rect(x,by,0,bh),seat,a,live,compact);
            // the launch at the right (a guest: Ready and Send Loadout)
            float bw=compact?300:340,right=r.xMax-pad;Rect btn=new Rect(right-bw,by,bw,bh);
            if(seat==X.Seat.Guest)
            {
                if(ExpLaunchButton(btn,networkReady?"CANCEL READY":"READY",networkReady?"YOU ARE READY":"THE HOST STARTS",go.Enabled,!networkReady,a,now,live))ExpLaunch(go);
                if(networkReady)H.Outline(btn,U.A(ReadyGreen,.8f*a),10);
                float sw=compact?140:164;var send=new Rect(btn.x-10-sw,by,sw,bh);if(ExpChipButton(send,"download","SEND LOADOUT",9560,a,live,false,server!=null,10.5f))SendServer(new NativeNetMessage{type="profile",profile=Profile(),token=account?.Token});btn=new Rect(send.x,by,btn.xMax-send.x,bh);
            }
            else if(ExpLaunchButton(btn,go.Label,go.Detail,go.Enabled,true,a,now,live))ExpLaunch(go);
            ExpStatus(new Rect(x+16,r.y,Mathf.Max(0,btn.x-16-x-16),r.height),seat,party,go,a);
        }
        // Offline: the play segment, SOLO (this screen) | HOST ROOM (the co-op room: Create) | JOIN ROOM.
        private float ExpPlaySegment(Rect r,float a,bool live,bool compact)
        {
            float x=r.x,w=compact?118:136;var items=new[]{("person-standing","SOLO"),("network","HOST ROOM"),("corner-up-right","JOIN ROOM")};
            for(int i=0;i<items.Length;i++)
            {
                var s=new Rect(x,r.y,w,r.height);bool on=i==0;float h=U.HoverAmount(9570+i,s,live&&!on);var set=i==0?H.Corners.TL:i==2?H.Corners.BR:H.Corners.None;
                H.Cut(s,U.A(on?U.Hex(0x0b3049,.95f):U.Mix(C.Indigo(.85f),U.Hex(0x1b2552,.95f),h),a),8,set);H.Outline(s,U.A(on?C.Cyan():U.Mix(C.Edge(),C.Cyan(),h),a),8,set);
                if(on)U.Fill(new Rect(s.x+10,s.yMax-H.Snap(2)-2,s.width-20,H.Snap(2)),C.Cyan(a));
                float tw=U.Measure(items[i].Item2,10.5f,true,false,.14f).x,cx=s.center.x-(18+tw)/2;NativeIcons.Draw(new Rect(cx,s.center.y-8,16,16),items[i].Item1,U.A(on?C.Cyan():U.Mix(C.CyanSoft(),Color.white,h),a));
                U.Text(new Rect(cx+22,s.y,tw+4,s.height),items[i].Item2,10.5f,U.A(on?C.Ink():U.Mix(C.Ink(.85f),Color.white,h),a),TextAnchor.MiddleLeft,bold:true,tracking:.14f);
                if(!on&&live&&U.Button(s)){Click();if(i==1){prefs.pve=true;Go(MenuScreen.Create);}else Go(MenuScreen.Join);}
                x+=w-1;
            }
            return x;
        }
        // In a room: the code (with how it is reached and the LAN line), Copy Code / Copy Invite, Leave Room.
        private float ExpRoomBlock(Rect r,X.Seat seat,float a,bool live,bool compact)
        {
            RoomAddresses(420);string code=ShownRoomCode(),label=RoomCodeLabel();bool has=!string.IsNullOrEmpty(code);float x=r.x;
            float cw=Mathf.Max(U.Measure(label,9,true,true,.16f).x,U.Measure(has?code:"—",compact?18:21,true,true,.09f).x)+8;
            U.Text(new Rect(x,r.y-1,cw+40,13),label,9,C.Muted(.9f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
            U.Text(new Rect(x,r.y+11,cw+40,compact?22:26),has?code:"—",compact?18:21,C.Cyan(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.09f,glow:C.Cyan(.3f*a));
            if(lanLines.Count>0&&!compact)U.Text(new Rect(x,r.yMax-11,cw+60,12),lanLines[0],8.5f,C.Dim(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
            x+=cw+14;float bw=compact?88:100,bh=compact?26:30,y=r.center.y-bh/2;
            if(ExpChipButton(new Rect(x,y,bw,bh),"copy","CODE",9580,a,live,true,has,10)){GUIUtility.systemCopyBuffer=code;Notice("Room code copied.");}x+=bw+6;
            if(inviteLink!=null){if(ExpChipButton(new Rect(x,y,bw,bh),"copy","INVITE",9581,a,live,false,true,10)){GUIUtility.systemCopyBuffer=inviteLink;Notice("Invite link copied.");}x+=bw+6;}
            float lw=compact?100:116;if(ExpChipButton(new Rect(x,y,lw,bh),"log-out","LEAVE ROOM",9582,a,live,false,true,10))LeaveRoom();x+=lw;
            return x;
        }
        // The launch: cyan with its sheen when it can go, the label, its detail line and the Enter key; navy with a lock when not.
        private bool ExpLaunchButton(Rect r,string label,string detail,bool enabled,bool primary,float a,float now,bool live)
        {
            bool on=live&&enabled;float h=U.HoverAmount(9590,r,on);bool press=on&&U.Hover(r)&&Input.GetMouseButton(0);var d=new Rect(r.x,r.y-h,r.width,r.height);if(press){float s=.985f;d=new Rect(d.center.x-d.width*s/2,r.center.y-r.height*s/2,d.width*s,d.height*s);}
            bool lit=enabled&&primary;Color fill=lit?U.Mix(C.Cyan(),C.CyanSoft(),h):enabled?U.Mix(C.Indigo(.92f),U.Hex(0x1b2552,.95f),h):C.Indigo(.7f),ink=lit?C.Void():enabled?C.Ink():C.Muted(.8f);
            if(lit){U.Glow(new Rect(d.x+8,d.y+10,d.width-16,d.height-6),C.Cyan((.22f+.3f*h)*a),18);}
            H.Cut(d,U.A(fill,a),10);H.Outline(d,U.A(lit?C.CyanSoft():enabled?U.Mix(C.Edge(),C.Cyan(),h):C.Edge(.7f),a),10);if(lit&&!prefs.reducedEffects)Sheen(d,a);
            float x=d.x+18;if(!enabled){ExpLock(new Rect(x,d.center.y-8,16,16),U.A(ink,a));x+=26;}else{NativeIcons.Draw(new Rect(x,d.center.y-9,18,18),"play",U.A(ink,a));x+=28;}
            float kw=H.KeyWidth("ENTER",18),room=d.xMax-14-kw-8-x;bool two=!string.IsNullOrEmpty(detail)&&d.height>=40;float ls=U.Measure(label,16,true,false,.1f).x<=room?16:15;
            U.Text(new Rect(x,two?d.y+d.height*.5f-ls*.95f:d.y,room,two?ls*1.25f:d.height),Fit(label,ls,room,true,.1f),ls,U.A(ink,a),TextAnchor.MiddleLeft,bold:true,tracking:.1f);
            if(two)U.Text(new Rect(x,d.y+d.height*.5f+2,room,13),Fit(detail,9,room,true,.16f),9,U.A(ink,.7f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
            if(enabled)H.Key(new Rect(d.xMax-14-kw,d.center.y-9,kw,18),"ENTER",.85f*a,lit?C.Void():C.CyanSoft());
            bool hit=on&&U.Button(r);if(hit)Click();return hit;
        }
        // The bar's status: an error first (the Expedition's start, the room's), then why the launch is held, else who the room
        // is waiting for.
        private void ExpStatus(Rect r,X.Seat seat,List<LobbyPilot> party,X.Launch go,float a)
        {
            if(r.width<80)return;string lead,rest;Color tone;
            int n=Mathf.Min(party.Count,K.PartyCap),ready=0;foreach(var p in party)if(p.ready&&p.connected)ready++;int waiting=Mathf.Max(0,n-ready);
            NativeSeatFrame mine=null;if(seat==X.Seat.Guest)foreach(var s in RoomSeats)if(s!=null&&s.id==networkId)mine=s;
            if(seat==X.Seat.Solo&&!string.IsNullOrEmpty(expeditionError)){lead="Expedition could not start.";rest=expeditionError;tone=C.Danger();}
            else if(!string.IsNullOrEmpty(networkStatus)&&NetworkError){lead=networkStatus;rest="";tone=C.Danger();}
            else if(mine!=null&&mine.spec!=prefs.weapon){lead="Send Loadout";rest="so the host gives you "+(LobbyWeapon(prefs.weapon)?.name??prefs.weapon)+".";tone=C.CyanSoft();}
            else if(seat==X.Seat.Guest){lead=networkReady?"You are ready.":"Ready up";rest=networkReady?"Waiting for the host to start.":"when your loadout is set. The host picks the fight.";tone=networkReady?C.Good():C.CyanSoft();}
            else if(!go.Enabled&&!string.IsNullOrEmpty(go.Reason)){lead=go.Reason;rest="";tone=C.Warn();}
            else if(seat==X.Seat.Host&&waiting>0){lead="Waiting for "+waiting+" pilot"+(waiting==1?"":"s");rest="· start when every pilot is ready.";tone=C.CyanSoft();}
            else if(seat==X.Seat.Host){lead="Everyone is ready.";rest=n>1?"Launch when you are.":"Share the code, add a bot, or launch.";tone=C.Good();}
            else{int bots=ExpBots(party);lead="Party of "+n;rest=bots>0?"· "+bots+(bots==1?" bot fills":" bots fill")+" the empty seats.":expMode==X.Mode.Expedition?"· host a room to bring friends.":"· add a bot or host a room.";tone=C.CyanSoft();}
            var lines=U.Wrap(lead+(rest.Length>0?" "+rest:""),12.5f,r.width,false,2);float lh=17,y=r.center.y-lines.Count*lh/2;
            for(int i=0;i<lines.Count;i++){string line=lines[i];bool first=i==0;if(first&&line.StartsWith(lead,StringComparison.Ordinal))
                {float hw=U.Measure(lead,12.5f,true).x;U.Text(new Rect(r.x,y,hw+4,lh),lead,12.5f,U.A(tone,a),TextAnchor.MiddleLeft,bold:true);if(line.Length>lead.Length)U.Text(new Rect(r.x+hw+4,y,r.width-hw-4,lh),line.Substring(lead.Length).TrimStart(),12.5f,C.Muted(a),TextAnchor.MiddleLeft);}
                else U.Text(new Rect(r.x,y,r.width,lh),line,12.5f,C.Muted(a),TextAnchor.MiddleLeft);y+=lh;}
        }

        // ---- LOADOUT drawer ----------------------------------------------------------------------------------------------------
        // A sheet from the right over the screen (canvas units, outside the design scale, as the GUILayout forms need): party bots
        // and mirror weapons (a guest sees the host's, read-only), then the class and weapon pickers. A click outside, L, B or
        // Escape closes it.
        private void ExpDrawer(X.Layout L,X.Seat seat,float e,float now)
        {
            float k=expDrawer?Mathf.Clamp01((now-expDrawerAt)/.22f):1-Mathf.Clamp01((now-expDrawerAt)/.16f);if(k<=0||e<=0)return;float q=M.Bezier(.2f,.8f,.2f,1,k)*e,s=L.Scale,W=U.W;
            float top=L.Party.Y*s,bottom=L.Bar.Bottom*s,dw=Mathf.Min(640,Mathf.Max(420,W*.42f)),x=L.Content.Right*s-dw+(1-q)*48;var r=new Rect(x,top,dw,bottom-top);
            var ev=Event.current;if(expDrawer&&ev.type==EventType.MouseDown&&!U.Hover(r)&&!MenuBlocked()){ev.Use();ExpToggleDrawer();return;}
            U.Fill(new Rect(0,0,W,U.H),C.Void(.45f*q));U.Glow(new Rect(r.x,r.y+30,r.width,r.height),new Color(0,0,0,.5f*q),50);U.Backdrop(r,0,16,q);H.Panel(r,q,C.Cyan(),14,H.Corners.Diagonal,true,.96f);
            U.Text(new Rect(r.x+24,r.y+20,300,16),"LOADOUT",12,C.CyanSoft(.95f*q),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.26f);
            U.Text(new Rect(r.x+24,r.y+38,r.width-100,30),seat==X.Seat.Guest?"Your pilot":"Your pilot and the party",22,C.Ink(q),TextAnchor.MiddleLeft,bold:true,tracking:-.02f);
            if(expDrawer&&ExpClose(new Rect(r.xMax-24-40,r.y+18,40,40),q,!MenuBlocked()&&q>.5f)){ExpToggleDrawer();return;}
            var body=new Rect(r.x+24,r.y+82,r.width-48,r.height-82-18);if(!expDrawer)return;
            var c=GUI.color;GUI.color=new Color(c.r,c.g,c.b,c.a*q);GUILayout.BeginArea(body);U.BeginDeferred();expDrawerScroll=GUILayout.BeginScrollView(expDrawerScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar);
            try{ExpDrawerBody(seat,body.width);}
            finally{GUI.enabled=true;GUILayout.Space(4);if(Event.current.type==EventType.Repaint)expDrawerContent=GUILayoutUtility.GetLastRect().yMax;GUILayout.EndScrollView();GUILayout.EndArea();GUI.color=c;U.EndDeferred(body,expDrawerScroll);}
        }
        private void ExpDrawerBody(X.Seat seat,float width)
        {
            BeginRules(false,width);var o=rules;
            if(expMode==X.Mode.Expedition)Fine("Expedition supports 1–4 signed-in players. Bots are available in Boss Battle.");
            else if(o==null)Fine("The host sets the party bots. Ready up when your loadout is set.");
            else
            {
                RuleSelect(7580,RoomOpt.Bots,"Party bots",PartyBotLabels,Mathf.Clamp(o.bots,0,K.PartyCap-1));
                RuleFine(7584,"AI pilots fill the party (up to 4). A friend who joins replaces the newest bot.",0,6);
                RuleCheck(7586,RoomOpt.Mirror,rulesLocked?"Party bots use the host's weapon":"Party bots use my weapon (otherwise mixed loadouts)",o.mirrorWeapons);
            }
            ClassRow("Your class",ClassCardStyle.Stacked);WeaponCards("Your weapon");
            Fine(seat==X.Seat.Guest?"After a change, Send Loadout so the host gives you this weapon.":expMode==X.Mode.Boss?"Boss Battle drops the party straight into the fight. Intros play as normal.":"Intros play as normal. Earn money from minions to buy chips, heals, revives and recruits. F interacts · C opens your chips; chips you take wait there until you choose.");
        }
    }
}
