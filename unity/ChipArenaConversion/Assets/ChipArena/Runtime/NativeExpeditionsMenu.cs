using System;
using System.Collections.Generic;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using K=ChipArena.UnityConversion.NativeExpeditionCatalogue;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
using X=ChipArena.UnityConversion.NativeExpeditionsMenu;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Expeditions screen (owner 2026-10-15: "Redesign the "Start Expedition" Screen ... much more modernized ... Multiplayer
    /// capable ... a "Play Wave Defense" button for individual waves ... players / bots in the lobby all in the lobby, in the top
    /// ... a "Select Expedition" screen"). Start Expedition opens it, and it is the co-op room's lobby as well (LobbyScreen hands a
    /// PvE room here), so the host and guests meet on the same screen. Full-canvas cyber layout (Core/NativeExpeditionsMenu
    /// Measure): the header; the party strip across the top (live 3D portraits, host crown, ready pills, bots the host adds and
    /// removes, open seats that invite); SELECT EXPEDITION, a carousel of catalogue cards (Core/NativeExpeditionCatalogue: key art
    /// from the areas' stage scans, the route of three areas and the final boss, live or in development; placeholders locked
    /// behind silhouettes) that turns on Q/E, A/D, the arrows or a click; the MODE panel (START EXPEDITION, the full run; PLAY
    /// WAVE DEFENSE with its start row: Wave 1, Wave 2, Wave 3, the Siren) with the loadout row; and the action bar (Solo, Host
    /// Room, Join Room or the room's code and invite; the launch, or a guest's Ready and Send Loadout). The LOADOUT drawer holds
    /// the party bots, mirror weapons and the class and weapon pickers. Presentation only: every action is a call the old
    /// boss-test screen or room lobby made (RequestStage(Boss, StartMatch), RequestStage(Expedition, StartExpeditionM1),
    /// Go(Create), Go(Join), ApplyRule, GuestReady, the profile message, LeaveRoom); no room field or message is added, so a
    /// guest sees the default pick and Wave Defense, and Waves 2-3 stay locked (Astra contract in DEV_LOG).
    /// </summary>
    public sealed partial class ConversionGame
    {
        private int expPick=-1,expTurnFrom,expTurnDir=1,expArea=-1,expPadFrame=-1;private float expTurnAt=-10,expSeenAt=-10,expDrawerAt=-10,expDrawerContent=400,expAge;
        private X.Mode expMode;private bool expDrawer;private Vector2 expDrawerScroll;private int[] expWaveCounts;
        private readonly List<LobbyPilot> expParty=new List<LobbyPilot>(K.PartyCap);
        private const float ExpTurn=.38f,ExpTurnCalm=.16f;
        private static Rect ExpRect(X.Box b)=>new Rect(b.X,b.Y,b.W,b.H);
        private X.Seat ExpSeat=>!InRoom?X.Seat.Solo:Hosting?X.Seat.Host:X.Seat.Guest;
        // Live controls: the screen is in and nothing covers it (the update card, the Connection report, the open drawer).
        private bool ExpLive(float a)=>a>.5f&&!MenuBlocked();

        private void ExpeditionsScreen(float age)
        {
            float now=Time.unscaledTime;var seat=ExpSeat;bool room=seat!=X.Seat.Solo;if(room)roomGuest=seat==X.Seat.Guest;expAge=age;
            if(now-expSeenAt>.25f)ExpOpen(seat);expSeenAt=now;
            // Guests read the host's replicated mode. Only the implemented catalogue entry can launch.
            if(seat==X.Seat.Guest){expPick=K.Default;expMode=receivedOptions?.expedition==true?X.Mode.Expedition:X.Mode.Waves;}
            var L=X.Measure(U.W,U.H);float e=In(age,0,.26f);var pick=K.At(expPick);var party=room?LobbyRoster():ExpSoloParty();
            var o=RulesNow();int start=X.StartOf(o?.skipWaves??prefs.skipWaves),guests=0;foreach(var p in party)if(!p.bot&&!p.you&&p.connected)guests++;
            bool busy=seat==X.Seat.Guest?server==null:server!=null;
            var go=X.Resolve(pick,expMode,start,seat,account.Developer,busy,GuestsReady,guests);
            ExpKeys(seat,go,ExpLive(e));
            // The open drawer takes the pointer from everything under it.
            bool held=menuBlockedNow;if(expDrawer)menuBlockedNow=true;
            U.Push(Vector2.zero,Vector2.zero,L.Scale);
            try
            {
                ExpHeader(L,seat,e*In(age,0,.3f),now);
                ExpPartyStrip(L,party,seat,e,now);
                ExpBrowser(L,seat,e*In(age,.08f,.34f),now);
                ExpModePanel(L,seat,pick,start,go,e*In(age,.12f,.34f),now);
                ExpBar(L,seat,party,go,e*In(age,.16f,.34f),now);
            }
            finally{U.Pop();menuBlockedNow=held;}
            ExpDrawer(L,seat,e,now);
        }
        // Reopening the screen preserves the selected room mode.
        private void ExpOpen(X.Seat seat)
        {
            if(expPick<0)expPick=K.Default;expDrawer=false;expDrawerAt=-10;expTurnAt=-10;expArea=-1;
            expMode=prefs.expedition?X.Mode.Expedition:X.Mode.Waves;
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
        private void ExpSetMode(X.Mode m){if(server!=null)return;expMode=m;ApplyRule(RoomOpt.Versus,m==X.Mode.Expedition?2:1,0);Click();}
        private void ExpTurnTo(int index,int dir)
        {
            int next=K.Wrap(index);if(next==expPick)return;expTurnFrom=expPick;expPick=next;expTurnDir=dir<0?-1:1;expTurnAt=Time.unscaledTime;expArea=-1;Click();
        }
        private void ExpPickWave(int start,bool edit)
        {
            if(!edit)return;if(!X.WaveSupported(start)){Notice(X.WaveLocked);return;}
            ApplyRule(RoomOpt.SkipWaves,X.SkipWaves(start)?1:0,0);if(expMode!=X.Mode.Waves)ExpSetMode(X.Mode.Waves);
        }
        private void ExpToggleDrawer(){expDrawer=!expDrawer;expDrawerAt=Time.unscaledTime;if(expDrawer){expDrawerScroll=Vector2.zero;expDrawerContent=400;}Click();}
        // The launch: the calls the boss-test screen and the room lobby made.
        private void ExpLaunch(X.Launch go)
        {
            if(!go.Enabled)return;
            switch(go.Kind)
            {
                case X.LaunchKind.ExpeditionM1:RequestStage(StageLoadKind.Expedition,StartExpeditionM1);break;
                case X.LaunchKind.WaveMatch:prefs.expedition=false;prefs.pve=true;sandboxWanted=false;RequestStage(StageLoadKind.Boss,StartMatch);break;
                case X.LaunchKind.Ready:GuestReady(!networkReady);break;
            }
        }

        // ---- Keys and controller ---------------------------------------------------------------------------------------
        // Q / E, A / D or the arrows turn the carousel; Tab, W / S or up / down switch the mode; 1-4 pick the wave start; L opens
        // the loadout; Enter or Space launches (a guest's Ready). A controller: LB / RB turn, Y switches, X opens the loadout, A
        // launches, B closes the drawer or goes back. Nothing while a text field has focus or the update card is open.
        private void ExpKeys(X.Seat seat,X.Launch go,bool live)
        {
            var ev=Event.current;bool edit=X.Editable(seat);
            if(ev.type==EventType.Layout&&expPadFrame!=Time.frameCount){expPadFrame=Time.frameCount;if(live)ExpPad(seat,go,edit);}
            if(!live||ev.type!=EventType.KeyDown||GUIUtility.keyboardControl!=0||updateOpen)return;
            var k=ev.keyCode;
            if(k==KeyCode.L){ev.Use();ExpToggleDrawer();return;}
            if(expDrawer)return;
            if(k==KeyCode.Q||k==KeyCode.A||k==KeyCode.LeftArrow){ev.Use();if(edit)ExpTurnTo(expPick-1,-1);}
            else if(k==KeyCode.E||k==KeyCode.D||k==KeyCode.RightArrow){ev.Use();if(edit)ExpTurnTo(expPick+1,1);}
            else if(k==KeyCode.Tab||k==KeyCode.W||k==KeyCode.S||k==KeyCode.UpArrow||k==KeyCode.DownArrow){ev.Use();if(edit)ExpSetMode(expMode==X.Mode.Waves?X.Mode.Expedition:X.Mode.Waves);}
            else if(k>=KeyCode.Alpha1&&k<=KeyCode.Alpha4){ev.Use();ExpPickWave(k-KeyCode.Alpha1,edit);}
            else if(k>=KeyCode.Keypad1&&k<=KeyCode.Keypad4){ev.Use();ExpPickWave(k-KeyCode.Keypad1,edit);}
            else if(k==KeyCode.Return||k==KeyCode.KeypadEnter||k==KeyCode.Space){ev.Use();ExpLaunch(go);}
        }
        // Once per frame (the old input's joystick buttons never reach IMGUI events).
        private void ExpPad(X.Seat seat,X.Launch go,bool edit)
        {
            if(Input.GetKeyDown(KeyCode.JoystickButton2)){ExpToggleDrawer();return;}
            if(Input.GetKeyDown(KeyCode.JoystickButton1)){if(expDrawer)ExpToggleDrawer();else if(seat==X.Seat.Solo)Go(MenuScreen.Home);return;}
            if(expDrawer)return;
            if(edit&&Input.GetKeyDown(KeyCode.JoystickButton4))ExpTurnTo(expPick-1,-1);
            if(edit&&Input.GetKeyDown(KeyCode.JoystickButton5))ExpTurnTo(expPick+1,1);
            if(edit&&Input.GetKeyDown(KeyCode.JoystickButton3))ExpSetMode(expMode==X.Mode.Waves?X.Mode.Expedition:X.Mode.Waves);
            if(Input.GetKeyDown(KeyCode.JoystickButton0))ExpLaunch(go);
        }

        // ---- Backdrop and header -----------------------------------------------------------------------------------------
        // Over the panel screens' shade: the map recedes under a void veil, a violet pool behind the carousel and a cyan one
        // at the top right, and a faint scan band drifting down (still with Reduced effects).
        private static readonly float[] ExpPoolAt={0f,.5f,1f},ExpPoolA={1f,.35f,0f};
        // Drawn by HomeMenu under the top bar and the footer (ExpScreenShown).
        private bool ExpScreenShown=>menuScreen==MenuScreen.Boss||menuScreen==MenuScreen.Lobby&&prefs.pve&&InRoom;
        private void ExpBackdrop(float e,float now)
        {
            float W=U.W,Hh=U.H;U.Fill(new Rect(0,0,W,Hh),C.Void(.5f*e));U.Fade(new Rect(0,0,W,Hh*.4f),C.Void(.45f*e));U.Fade(new Rect(0,Hh*.62f,W,Hh*.38f),C.Void(.5f*e),false);
            float r=Mathf.Max(W,Hh)*.62f;U.RadialStops(new Rect(W*.36f-r,Hh*.66f-r,2*r,2*r),U.Hex(0x3a2166,.34f*e),ExpPoolAt,ExpPoolA);
            float c=r*.7f;U.RadialStops(new Rect(W*.86f-c,Hh*.18f-c,2*c,2*c),C.Cyan(.06f*e),ExpPoolAt,ExpPoolA);
            if(!prefs.reducedEffects){float y=Mathf.Repeat(now*34,Hh+240)-120;U.Fade(new Rect(0,y-70,W,70),C.Cyan(.022f*e),false);U.Fill(new Rect(0,y,W,1),C.Cyan(.05f*e));}
        }
        private void ExpHeader(X.Layout L,X.Seat seat,float a,float now)
        {
            if(a<=.01f)return;var h=ExpRect(L.Header);bool compact=L.Compact,room=seat!=X.Seat.Solo;float y=h.y,rise=8*(1-a);y+=rise;
            U.Fill(new Rect(h.x,y+5,H.Snap(6),H.Snap(6)),C.Cyan(a));U.Fill(new Rect(h.x+10,y+7,18,H.Snap(2)),C.Magenta(.85f*a));
            U.Text(new Rect(h.x+36,y,420,16),"PVE · EXPEDITIONS",12,C.CyanSoft(.95f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.26f);
            float ts=compact?28:32,ty=y+18;const string title="Expeditions";U.Text(new Rect(h.x,ty,520,ts*1.25f),title,ts,C.Ink(a),TextAnchor.MiddleLeft,bold:true,tracking:-.02f,shadow:C.Void(.6f*a));
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
            if(room){ReadyPill(new Rect(info.x,fy,PillWidth(p),24),p,al,out _);}
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
            string code=room?ShownRoomCode():null;string line=seat==X.Seat.Guest?"The host adds bots":room?(string.IsNullOrEmpty(code)?"Share the room code":"Code "+code):"Add a bot or host a room";
            U.Text(new Rect(r.x+8,cy+hr+24,r.width-16,16),Fit(line,11,r.width-16,false,0),11,C.Muted(.95f*al),TextAnchor.MiddleCenter);
            if(seat==X.Seat.Guest)return;
            float by=r.yMax-34,bw=(r.width-30)/2;bool invite=room&&!string.IsNullOrEmpty(code);
            if(ExpChipButton(new Rect(r.x+10,by,bw,26),"bot","ADD BOT",9440,a,live,false,canAdd,9.5f))ExpSetBots(ExpBotsShown()+1);
            if(room){if(ExpChipButton(new Rect(r.x+20+bw,by,bw,26),"copy",inviteLink!=null?"INVITE":"CODE",9441,a,live,true,invite,9.5f)){if(inviteLink!=null){GUIUtility.systemCopyBuffer=inviteLink;Notice("Invite link copied.");}else{GUIUtility.systemCopyBuffer=code;Notice("Room code copied.");}}}
            else if(ExpChipButton(new Rect(r.x+20+bw,by,bw,26),"network","HOST ROOM",9442,a,live,true,true,9.5f)){prefs.pve=true;Go(MenuScreen.Create);}
        }
        // The bots the party shows now (a friend who joins takes the newest bot's seat), so add and remove step from what is seen.
        private int ExpBotsShown(){int n=0;if(InRoom){foreach(var s in RoomSeats)if(s!=null&&s.bot)n++;return n;}return Mathf.Min(Mathf.Clamp(prefs.bots,0,K.PartyCap-1),K.PartyCap-1);}

        // ---- SELECT EXPEDITION ---------------------------------------------------------------------------------------------
        private void ExpBrowser(X.Layout L,X.Seat seat,float a,float now)
        {
            if(a<=.01f)return;var b=X.Carousel(L.Browser,L.Compact);bool edit=X.Editable(seat),calm=prefs.reducedEffects,live=ExpLive(a)&&edit;var lr=ExpRect(b.Label);float lh=lr.height-8;
            U.Text(new Rect(lr.x,lr.y,300,lh),"SELECT EXPEDITION",11,C.CyanSoft(.9f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.26f);float lx=lr.x+U.Measure("SELECT EXPEDITION",11,true,true,.26f).x+12;
            string counter=(expPick+1).ToString("00")+" / "+K.Count.ToString("00");U.Text(new Rect(lx,lr.y,80,lh),counter,11,C.Ink(.9f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);lx+=U.Measure(counter,11,true,true,.1f).x+14;
            // the arrows with their keys at the right (a guest's view is the host's pick: no arrows, the note instead)
            float rx=lr.xMax;
            if(edit)
            {
                for(int i=1;i>=0;i--)
                {
                    var bt=new Rect(rx-34,lr.y-3,34,lh+6);float h=U.HoverAmount(9460+i,bt,live);H.Cut(bt,U.A(U.Mix(C.Indigo(.9f),U.Hex(0x1b2552),h),a),5,H.Corners.All);H.Outline(bt,U.A(U.Mix(C.Edge(),C.Cyan(),h),a),5,H.Corners.All);
                    NativeIcons.DrawMarkup(new Rect(bt.center.x-8,bt.center.y-8,16,16),i==0?"chevron-left":"chevron-right",i==0?ChevronLeft:ChevronRight,24,U.A(U.Mix(C.CyanSoft(),Color.white,h),a));
                    if(live&&U.Button(bt)){if(i==0)ExpTurnTo(expPick-1,-1);else ExpTurnTo(expPick+1,1);}
                    rx-=40;
                }
                rx-=4;foreach(var key in new[]{"E","Q"}){float kw=H.KeyWidth(key,18);rx-=kw;H.Key(new Rect(rx,lr.y+(lh-18)/2,kw,18),key,.9f*a);rx-=4;}
                rx-=8;
            }
            else{const string host="THE ROOM'S RUN";float hw=U.Measure(host,9.5f,true,true,.16f).x+26;var t=new Rect(rx-hw,lr.y-2,hw,lh+4);H.Tag(t,C.Violet(),a);U.Text(new Rect(t.x+12,t.y,hw-14,t.height),host,9.5f,U.A(U.Mix(C.Violet(),Color.white,.45f),a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);rx=t.x-12;}
            U.Fill(new Rect(lx,lr.y+lh/2,Mathf.Max(0,rx-lx),H.Px),C.Edge(.7f*a));
            // the neighbours' spines, then the card (turning while the pick changes)
            var card=ExpRect(b.Card);float k=Mathf.Clamp01((now-expTurnAt)/(calm?ExpTurnCalm:ExpTurn));
            if(k<1){float q=M.Bezier(.2f,.8f,.2f,1,k);ExpTurnCard(card,expTurnFrom,q,true,a,now,calm,L.Compact);ExpTurnCard(card,expPick,q,false,a,now,calm,L.Compact);}
            else ExpCard(card,K.At(expPick),a,now,L.Compact,live);
            // the neighbours' spines over the card's edges (a turning card slides under them)
            float sa=a*(edit?1:.6f);ExpSpine(ExpRect(b.Prev),K.Wrap(expPick-1),-1,sa,now,live);ExpSpine(ExpRect(b.Next),K.Wrap(expPick+1),1,sa,now,live);
            // the dots: one bar per expedition, the pick long and lit, placeholders dim
            var d=ExpRect(b.Dots);float dw=0;for(int i=0;i<K.Count;i++)dw+=(i==expPick?34:12)+6;float dx=d.center.x-dw/2+3,dy=d.center.y;
            for(int i=0;i<K.Count;i++){float w=i==expPick?34:12;var bar=new Rect(dx,dy-1.5f,w,3);var x=K.At(i);if(i==expPick){U.Glow(bar,C.Cyan(.35f*a),5);U.Fill(bar,C.Cyan(a));}else U.Fill(bar,x.Locked?C.Edge(.9f*a):C.CyanSoft(.5f*a));
                if(live&&i!=expPick&&U.Button(new Rect(dx-3,dy-8,w+6,16)))ExpTurnTo(i,i>expPick?1:-1);dx+=w+6;}
        }
        // A card mid-turn: the old one leaves toward the side it was turned from as it narrows and fades, the new one arrives
        // from the other side widening in (Reduced effects: a cross-fade in place).
        private void ExpTurnCard(Rect card,int index,float q,bool leaving,float a,float now,bool calm,bool compact)
        {
            float off=card.width*.3f,x=leaving?-expTurnDir*off*q:expTurnDir*off*(1-q),sx=leaving?1-.42f*q:.58f+.42f*q,al=leaving?1-q:q;if(calm){x=0;sx=1;}
            if(al<=.01f)return;U.Push(card.center,new Vector2(x,0),sx,0,1);ExpCard(card,K.At(index),a*al,now,compact,false);U.Pop();
        }
        // A neighbour's spine: its number, accent, title running up the spine, live or locked; a click turns to it.
        private void ExpSpine(Rect r,int index,int dir,float a,float now,bool live)
        {
            var x=K.At(index);Color acc=U.Hex(x.Accent);float h=U.HoverAmount(9450+(dir>0?1:0),r,live);float al=a*(.62f+.38f*h);
            H.Cut(r,C.Navy(.72f*al),10,dir<0?H.Corners.TL:H.Corners.TR);U.HGradient(r,dir<0?Color.clear:U.A(acc,.1f*al),dir<0?U.A(acc,.1f*al):Color.clear);
            H.Outline(r,U.A(U.Mix(C.Edge(),acc,.35f+.4f*h),al),10,dir<0?H.Corners.TL:H.Corners.TR);U.Fill(new Rect(dir<0?r.xMax-H.Snap(2):r.x,r.y+12,H.Snap(2),r.height-24),U.A(acc,(.5f+.4f*h)*al));
            U.Text(new Rect(r.x,r.y+10,r.width,16),(index+1).ToString("00"),11,U.A(U.Mix(acc,Color.white,.3f),al),TextAnchor.MiddleCenter,bold:true,mono:true);
            if(x.Locked)ExpLock(new Rect(r.center.x-7,r.yMax-26,14,14),C.Muted(al));else{var dot=new Rect(r.center.x-4,r.yMax-22,8,8);U.Glow(dot,C.Good(.4f*al),4);U.Disc(dot,C.Good(al));}
            float len=r.height-64,ts=U.Measure(x.Title,11,true,false,.3f).x<=len?11:10,tk=ts<11?.18f:.3f;U.Push(r.center,Vector2.zero,1,dir<0?-90:90);U.Text(new Rect(r.center.x-len/2,r.center.y-9,len,18),Fit(x.Title,ts,len,true,tk),ts,C.Ink(.85f*al),TextAnchor.MiddleCenter,bold:true,tracking:tk);U.Pop();
            if(live&&U.Button(r))ExpTurnTo(index,dir);
        }
        // The selected expedition: the key art at the left, the kicker and threats, the title, the tag line, the facts and the
        // route at the right, in the entry's accent.
        private void ExpCard(Rect r,K.Expedition x,float a,float now,bool compact,bool live)
        {
            if(a<=.01f)return;Color acc=U.Hex(x.Accent);var k=X.CardParts(new X.Box(r.x,r.y,r.width,r.height),compact);
            U.Glow(new Rect(r.x,r.y+26,r.width,r.height),new Color(0,0,0,.5f*a),50);
            H.Panel(r,a,x.Locked?U.Mix(acc,C.Muted(),.35f):acc,16,H.Corners.Diagonal,true,.93f);
            U.RadialStops(new Rect(r.xMax-r.width*.55f,r.y,r.width*.55f,r.height*.9f),U.A(acc,.07f*a),ExpPoolAt,ExpPoolA);
            ExpArt(ExpRect(k.Art),x,a,now,compact);ExpInfo(ExpRect(k.Info),x,k,a,now,compact,live);
        }

        // ---- Key art -------------------------------------------------------------------------------------------------------
        private ContentMap ExpMapOf(string id){if(id==null||content?.maps==null)return null;foreach(var m in content.maps)if(m.id==id)return m;return null;}
        // The route row the art shows: the hovered or picked row with a scan, else the first live area.
        private int ExpPreviewArea(K.Expedition x)
        {
            if(expArea==-2||expArea>=0&&expArea<x.Areas.Length)return expArea;for(int i=0;i<x.Areas.Length;i++)if(x.Areas[i].State==K.AreaState.Live&&x.Areas[i].Map!=null)return i;return -1;
        }
        private void ExpArt(Rect r,K.Expedition x,float a,float now,bool compact)
        {
            Color acc=U.Hex(x.Accent);bool calm=prefs.reducedEffects;
            H.Cut(r,C.Void(.92f*a),10,H.Corners.TL);U.RadialStops(r,U.A(U.Mix(acc,U.Hex(0x3a2166),.5f),.42f*a),ExpPoolAt,ExpPoolA);
            if(x.Locked){ExpSilhouette(r,x,a,now,calm,compact);H.Outline(r,U.A(acc,.3f*a),10,H.Corners.TL);H.Brackets(r,U.A(acc,.55f*a),12,H.Corners.TL);return;}
            int ai=ExpPreviewArea(x);var area=ai>=0?x.Areas[ai]:null;bool hub=area==null||area.Map==null;var map=ExpMapOf(hub?x.HubMap:area.Map);var scan=map!=null?NativeStageSchematic.For(map):null;
            float top=compact?24:30,bottom=compact?22:28;var plan=new Rect(r.x+12,r.y+top,r.width-24,r.height-top-bottom);
            // The plan sits at the foot; above it, where there is room, the Siren's face over her expedition (else faint behind the plan).
            float band=x.WaveDefense?(compact?40:66):0,ph=scan!=null?Mathf.Min(plan.height,plan.width/Mathf.Max(.2f,scan.Aspect)):plan.height;
            if(band>0&&plan.height-ph<band&&plan.height-band>=plan.height*.6f)ph=plan.height-band;float free=plan.height-ph,pw=scan!=null?Mathf.Min(plan.width,ph*Mathf.Max(.2f,scan.Aspect)):plan.width;
            if(x.WaveDefense)
            {
                bool above=free>=(compact?36:60);float fs=above?Mathf.Min(free*1.45f,plan.width*.5f):Mathf.Min(plan.width,plan.height)*.7f,br=calm?.5f:.5f+.5f*Mathf.Sin(now*1.7f);
                var fc=above?new Vector2(plan.center.x,plan.y+free*.5f):plan.center;U.Radial(new Rect(fc.x-fs*.8f,fc.y-fs*.42f,fs*1.6f,fs*.84f),U.A(acc,(above?.18f+.07f*br:.1f)*a));
                NativeIcons.DrawMarkup(new Rect(fc.x-fs/2,fc.y-fs*.31f,fs,fs),"siren-face",SirenFace,200,new Color(1,1,1,(above?.85f:.12f)*a));
            }
            if(scan!=null)ExpScan(new Rect(plan.center.x-pw/2,plan.yMax-ph,pw,ph),scan,acc,a*(hub&&area!=null?.4f:1),now,calm);
            // the labels: the area over the scan, its block count and the scan mark under it
            string head=area==null?"HUB · "+x.Hub.ToUpperInvariant():(area.Boss?"FINAL · ":"AREA "+(ai+1).ToString("00")+" · ")+area.Name;
            U.Fill(new Rect(r.x+12,r.y+12,H.Snap(5),H.Snap(5)),U.A(acc,a));U.Text(new Rect(r.x+22,r.y+6,r.width-40,16),Fit(head,10,r.width-40,true,.2f),10,U.A(U.Mix(acc,Color.white,.55f),a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            if(scan!=null)U.Text(new Rect(r.x+12,r.yMax-bottom+4,r.width-24,16),(hub&&area!=null?"HUB SCAN · ":"STAGE SCAN · ")+scan.Label,9.5f,C.Muted(.9f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
            if(hub&&area!=null){var stamp=new Rect(r.center.x-80,plan.center.y-18,160,36);H.Cut(stamp,C.Void(.85f*a),6,H.Corners.All);H.Outline(stamp,C.Warn(.6f*a),6,H.Corners.All);
                U.Text(new Rect(stamp.x,stamp.y+3,stamp.width,16),"NO SCAN YET",10,C.Warn(a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.2f);U.Text(new Rect(stamp.x,stamp.y+18,stamp.width,14),K.InDevelopment,8.5f,C.Muted(a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.18f);}
            H.Outline(r,U.A(acc,.32f*a),10,H.Corners.TL);H.Brackets(r,U.A(acc,.6f*a),12,H.Corners.TL);
        }
        // The stage scan as a hologram (the loading screen's NativeStageSchematic, drawn smaller): the grid, the slabs as the
        // plate, decks as panes, volumes by height with lit edges, cover in the accent, the spawns; a sweep runs across it every
        // few seconds (still with Reduced effects).
        private static void ExpScan(Rect inner,NativeStageSchematic s,Color accent,float a,float now,bool calm)
        {
            float aspect=Mathf.Max(.2f,s.Aspect),pw=inner.width,ph=pw/aspect;if(ph>inner.height){ph=inner.height;pw=ph*aspect;}var plan=new Rect(inner.center.x-pw/2,inner.center.y-ph/2,pw,ph);float px=H.Px;
            for(int i=0;i<=10;i++){float gx=plan.x+plan.width*i/10,gy=plan.y+plan.height*i/10,ga=i%5==0?.2f:.09f;U.Fill(new Rect(gx,plan.y,px,plan.height),C.Edge(ga*a));U.Fill(new Rect(plan.x,gy,plan.width,px),C.Edge(ga*a));}
            float sweep=calm?2:Mathf.Repeat(now/4.2f,1.35f)-.1f;var box=s.Box;var lv=s.Level;var kind=s.Kind;Color deep=C.CyanDeep(),soft=C.CyanSoft();float edgeMin=7*px;
            for(int i=0;i<s.Count;i++)
            {
                float u=box[i*4],v=box[i*4+1],bw=box[i*4+2],bh=box[i*4+3];byte kd=kind[i];var q=new Rect(plan.x+u*plan.width,plan.y+v*plan.height,Mathf.Max(px,bw*plan.width),Mathf.Max(px,bh*plan.height));
                if(kd==NativeStageSchematic.Plate){U.Fill(q,C.Indigo(.55f*a));continue;}
                float lit=Mathf.Clamp01(1-Mathf.Abs(u+bw*.5f-sweep)*6);float level=lv[i];bool edge=q.width>=edgeMin&&q.height>=edgeMin;
                if(kd==NativeStageSchematic.Deck){U.Fill(q,C.Cyan((.05f+.08f*lit)*a));if(edge)H.Outline(q,C.CyanSoft((.22f+.3f*lit)*a),0,H.Corners.None);continue;}
                Color c=kd==NativeStageSchematic.Cover?accent:Color.Lerp(deep,soft,level);U.Fill(q,U.A(c,((kd==NativeStageSchematic.Cover?.36f:.14f+.3f*level)+.25f*lit)*a));
                if(edge)H.Outline(q,U.A(U.Mix(c,Color.white,.4f*lit),(.4f+.4f*level+.2f*lit)*a),0,H.Corners.None);
            }
            var sp=s.Spawns;for(int i=0;i+1<sp.Length;i+=2){const float d=4;var c=new Rect(plan.x+sp[i]*plan.width-d/2,plan.y+sp[i+1]*plan.height-d/2,d,d);U.Fill(c,C.Cyan(.9f*a));}
            if(!calm&&sweep>0&&sweep<1){float sx=plan.x+plan.width*sweep,trail=Mathf.Min(60,sx-plan.x);if(trail>0)U.HGradient(new Rect(sx-trail,plan.y,trail,plan.height),Color.clear,C.Cyan(.1f*a));U.Glow(new Rect(sx-1,plan.y,2,plan.height),C.Cyan(.35f*a),8);U.Fill(new Rect(sx-px,plan.y,H.Snap(2),plan.height),C.CyanSoft(.8f*a));}
            H.Brackets(new Rect(plan.x-5,plan.y-5,plan.width+10,plan.height+10),U.A(accent,.55f*a),10,H.Corners.None);
        }
        // A placeholder's art: its skyline as a silhouette rimmed in the accent under a dusk wash, interference lines, the lock in
        // a hexagon, SIGNAL LOST and the COMING SOON band.
        private const string ChoirSkyline="<path d=\"M0 100V72H6V64L9 56L12 64V72H18V60L24 44L30 60V68H35V50L41 30L47 50V60H53V50L59 30L65 50V68H70V60L76 44L82 60V72H88V64L91 56L94 64V72H100V100Z\" fill=\"#fff\"/>";
        private const string ParishSkyline="<path d=\"M0 100V80L8 72L16 80V74L27 63L38 74V84H42V70L50 62L58 70V80L66 74L74 80V66H77V80L84 76V70L92 64L100 70V100Z\" fill=\"#fff\"/><path d=\"M47 62L49 52L51 62Z\" fill=\"#fff\"/>";
        private const string MarrowSkyline="<path d=\"M0 100V62C8 58 14 58 20 62C26 54 34 54 40 62C46 50 54 50 60 62C66 54 74 54 80 62C86 58 92 58 100 62V100Z\" fill=\"#fff\"/>";
        private const string MarrowArches="<path d=\"M8 100V84A6 6 0 0 1 20 84V100ZM28 100V80A8 8 0 0 1 44 80V100ZM56 100V80A8 8 0 0 1 72 80V100ZM80 100V84A6 6 0 0 1 92 84V100Z\" fill=\"#fff\"/>";
        private void ExpSilhouette(Rect r,K.Expedition x,float a,float now,bool calm,bool compact)
        {
            Color acc=U.Hex(x.Accent);U.Fade(new Rect(r.x,r.y+r.height*.35f,r.width,r.height*.65f),U.A(acc,.16f*a),false);
            string id=x.Id,mark=id=="hollow-choir"?ChoirSkyline:id=="ashen-parish"?ParishSkyline:MarrowSkyline;float s=r.width;var sq=new Rect(r.x,r.yMax-s,s,s);
            U.BeginBand(r);
            float horizon=sq.y+s*.62f,gw=r.width*.75f;U.RadialStops(new Rect(r.center.x-gw,horizon-gw*.45f,2*gw,gw*.9f),U.A(acc,.28f*a),ExpPoolAt,ExpPoolA);
            NativeIcons.DrawMarkup(new Rect(sq.x,sq.y-2,sq.width,sq.height),"exp-sil-"+id,mark,100,U.A(acc,.7f*a));NativeIcons.DrawMarkup(sq,"exp-sil-"+id,mark,100,U.A(U.Mix(C.Void(),acc,.14f),.97f*a));U.Fade(new Rect(r.x,horizon+s*.08f,r.width,r.yMax-horizon-s*.08f),C.Void(.55f*a),false);
            if(id=="night-marrow")NativeIcons.DrawMarkup(sq,"exp-arch",MarrowArches,100,U.A(acc,.16f*a));
            // interference: thin lines drifting down through the art (still with Reduced effects)
            for(int i=0;i<7;i++){float y=r.y+Mathf.Repeat(i*.143f+(calm?0:now*.09f),1)*r.height;U.Fill(new Rect(r.x,y,r.width,H.Px),U.A(acc,(.05f+.04f*(i%3))*a));}
            U.EndBand();
            // the lock sits in the dark ground under the skyline, over the COMING SOON band
            float hr=compact?14:22,cy=r.y+r.height*(compact?.58f:.62f);var hex=new Rect(r.center.x-hr*48/22f/2,cy-hr*48/22f/2,hr*48/22f,hr*48/22f);
            U.Glow(new Rect(r.center.x-hr,cy-hr,2*hr,2*hr),U.A(acc,.18f*a),16);NativeIcons.DrawMarkup(hex,"hex-fill",HexFill,48,new Color(1,1,1,.92f*a));NativeIcons.DrawMarkup(hex,"hex-edge",HexEdge,48,U.A(acc,.8f*a));ExpLock(new Rect(r.center.x-hr*.5f,cy-hr*.5f,hr,hr),U.A(U.Mix(acc,Color.white,.4f),a));
            U.Text(new Rect(r.x,cy+hr+8,r.width,16),"SIGNAL LOST",10,U.A(U.Mix(acc,Color.white,.4f),.9f*a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.32f);
            var band=new Rect(r.x,r.yMax-(compact?30:38),r.width,compact?20:24);U.Fill(band,C.Void(.82f*a));U.Fill(new Rect(band.x,band.y,band.width,H.Px),U.A(acc,.6f*a));U.Fill(new Rect(band.x,band.yMax-H.Px,band.width,H.Px),U.A(acc,.6f*a));
            U.Text(band,K.ComingSoon,compact?10:11,U.A(U.Mix(acc,Color.white,.55f),a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.4f);
        }

        // ---- Details and route ---------------------------------------------------------------------------------------------
        private void ExpInfo(Rect r,K.Expedition x,X.Card k,float a,float now,bool compact,bool live)
        {
            Color acc=U.Hex(x.Accent),lifted=U.Mix(acc,Color.white,.4f);float y=r.y;
            // the kicker (and the working title while it fits), the threats at the right
            float tx=r.xMax;for(int i=x.Threats.Length-1;i>=0;i--){string t=x.Threats[i];float w=U.Measure(t,9,true,true,.16f).x+(compact?22:30);if(tx-w<r.x+120)break;tx-=w;var tag=new Rect(tx,y-2,w,18);H.Tag(tag,acc,a*(x.Locked?.6f:1),false);
                if(!compact)NativeIcons.Draw(new Rect(tag.x+6,tag.y+3,12,12),t=="SPIRITS"||t=="WRAITHS"?"ghost":t=="BURNERS"?"flame":"skull",U.A(lifted,a));U.Text(new Rect(tag.x+(compact?10:22),tag.y,w-(compact?12:24),18),t,9,U.A(lifted,a*(x.Locked?.7f:1)),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);tx-=6;}
            U.Text(new Rect(r.x,y-2,tx-r.x,18),x.Kicker,11,U.A(lifted,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.24f);
            float kw=U.Measure(x.Kicker,11,true,true,.24f).x;string sub=x.Locked?K.ComingSoon:K.WorkingTitle;float sw=U.Measure("·  "+sub,9.5f,true,true,.2f).x;
            if(r.x+kw+12+sw<tx-8)U.Text(new Rect(r.x+kw+10,y-2,sw+4,18),"·  "+sub,9.5f,x.Locked?C.Warn(.8f*a):C.Muted(.85f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            y+=compact?14:18;
            // the title, glowing in the accent; the tag line
            U.Text(new Rect(r.x-1,y,r.width+2,k.Title*1.2f),x.Title,k.Title,C.Ink(x.Locked?.7f*a:a),TextAnchor.MiddleLeft,bold:true,tracking:.05f,glow:U.A(acc,(x.Locked?.25f:.45f)*a),shadow:C.Void(.6f*a));y+=k.Title*1.2f;
            var lines=U.Wrap(x.Tagline,k.Tagline,r.width,false,k.TaglineLines);foreach(var line in lines){U.Text(new Rect(r.x,y,r.width,k.Tagline*1.4f),line,k.Tagline,C.Muted(.95f*a),TextAnchor.MiddleLeft);y+=k.Tagline*1.4f;}
            y+=(k.TaglineLines-lines.Count)*k.Tagline*1.4f+(compact?4:8);
            // the facts: the hub, the party and the length
            float fx=r.x,fh=compact?22:26;
            foreach(var (icon,text) in new[]{("house",x.Hub.ToUpperInvariant()),("users",x.Party),("hourglass",x.Duration)})
            {float w=U.Measure(text,9.5f,true,true,.14f).x+34;if(fx+w>r.xMax)break;var f=new Rect(fx,y,w,fh);H.Cut(f,C.Indigo(.75f*a),5,H.Corners.All);H.Outline(f,C.Edge(.9f*a),5,H.Corners.All);
             NativeIcons.Draw(new Rect(f.x+9,f.center.y-6.5f,13,13),icon,C.CyanSoft(.9f*a));U.Text(new Rect(f.x+27,f.y,w-28,fh),text,9.5f,C.Ink(.9f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);fx+=w+6;}
            // the route, pinned to the foot: the hub (when there is room), the three areas and the final boss
            int rows=x.Areas.Length+(compact?0:1);float rh=k.RouteRow,ry=r.yMax-rows*rh;
            if(!compact)
            {
                float hy=ry-18;U.Text(new Rect(r.x,hy,120,14),"ROUTE",9.5f,C.CyanSoft(.85f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.26f);
                string live2=x.Locked?"UNCHARTED":x.LiveAreas+" / "+x.Areas.Length+" LIVE";float lw=U.Measure(live2,9,true,true,.16f).x;U.Text(new Rect(r.xMax-lw-2,hy,lw+4,14),live2,9,x.Locked?C.Muted(.8f*a):C.Good(.85f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
                U.Fill(new Rect(r.x+56,hy+7,Mathf.Max(0,r.xMax-lw-14-r.x-56),H.Px),C.Edge(.6f*a));
            }
            // the spine through the nodes
            float nx=r.x+8;U.Fill(new Rect(nx-H.Px/2,ry+rh/2,H.Px,(rows-1)*rh),U.A(acc,.35f*a));
            int row=0;if(!compact){ExpRouteRow(new Rect(r.x,ry,r.width,rh),-2,"HUB",x.Hub.ToUpperInvariant(),x.Locked?"Signal lost":"The sanctuary and the area doors",x.Locked?K.AreaState.Uncharted:K.AreaState.Live,false,acc,a,now,live&&!x.Locked,x.HubMap!=null);row++;}
            for(int i=0;i<x.Areas.Length;i++){var ar=x.Areas[i];ExpRouteRow(new Rect(r.x,ry+row*rh,r.width,rh),i,ar.Boss?"FINAL":(i+1).ToString("00"),ar.Name,ar.Place,ar.State,ar.Boss,acc,a,now,live&&!x.Locked,ar.Map!=null||ar.State==K.AreaState.InDevelopment);row++;}
        }
        // A route row: its node on the spine, the number, the name and place, and the state at the right. Hover (or a click)
        // shows the row in the key art: its scan, the hub's scan for the hub, or NO SCAN YET.
        private void ExpRouteRow(Rect r,int area,string num,string name,string place,K.AreaState state,bool boss,Color acc,float a,float now,bool live,bool preview)
        {
            bool on=area==expArea&&area!=-1;float h=U.HoverAmount(9482+area,r,live&&preview);if(live&&preview&&U.Hover(r)&&Event.current.type==EventType.Repaint)expArea=area;
            if(h>0||on){U.HGradient(new Rect(r.x,r.y+1,r.width*.7f,r.height-2),C.Cyan(.10f*Mathf.Max(h,on?.6f:0)*a),Color.clear);}
            float cy=r.center.y,nx=r.x+8;bool calm=prefs.reducedEffects;
            if(boss){Skull(new Rect(nx-8,cy-8,16,16),a*(state==K.AreaState.Live?1:.75f),0);}
            else if(state==K.AreaState.Live){float p=calm?0:Mathf.Repeat(now*.8f+area*.3f,1);var d=new Rect(nx-4,cy-4,8,8);if(p>0)U.RadialStops(new Rect(nx-4-8*p,cy-4-8*p,8+16*p,8+16*p),C.Good(.45f*(1-p)*a),RingStopsAt,RingStopsA);U.Glow(d,C.Good(.5f*a),5);U.Disc(d,C.Good(a));}
            else if(state==K.AreaState.InDevelopment){var d=new Rect(nx-5,cy-5,10,10);U.Disc(d,C.Void(a));U.RadialStops(d,U.A(acc,.8f*a),RingStopsAt2,RingStopsA2);}
            else ExpLock(new Rect(nx-6,cy-6,12,12),C.Muted(.8f*a));
            bool small=r.height<22;float tx=r.x+24,size=small?11.5f:13;U.Text(new Rect(tx,r.y,44,r.height),num,9.5f,U.A(boss?U.Mix(acc,Color.white,.3f):C.CyanSoft(),.9f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);tx+=small?40:44;
            string chip=small?K.ShortLabel(state):K.StateLabel(state);float cw=U.Measure(chip,8.5f,true,true,.16f).x+(state==K.AreaState.Live?24:16);var c=new Rect(r.xMax-cw,cy-9,cw,18);
            Color tone=state==K.AreaState.Live?C.Good():state==K.AreaState.InDevelopment?C.Warn():C.Muted();H.Tag(c,tone,a*(state==K.AreaState.Live?1:.8f),false);
            if(state==K.AreaState.Live){U.Disc(new Rect(c.x+8,cy-2.5f,5,5),C.Good(a));}U.Text(new Rect(c.x+(state==K.AreaState.Live?16:8),c.y,cw-10,18),chip,8.5f,U.A(tone,a*(state==K.AreaState.Live?1:.85f)),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
            float room=c.x-10-tx,nw=Mathf.Min(U.Measure(name,size,true,false,.06f).x,room);U.Text(new Rect(tx,r.y,nw+2,r.height),Fit(name,size,room,true,.06f),size,U.A(U.Mix(C.Ink(state==K.AreaState.Uncharted?.6f:.95f),Color.white,h),a),TextAnchor.MiddleLeft,bold:true,tracking:.06f);
            float px=tx+nw+10,pr=c.x-10-px;if(!string.IsNullOrEmpty(place)&&U.Measure(place,size-1.5f).x<=pr)U.Text(new Rect(px,r.y,pr,r.height),place,size-1.5f,C.Muted(.85f*a),TextAnchor.MiddleLeft);
            if(live&&preview&&U.SilentButton(r)){expArea=area;}
        }
        private static readonly float[] RingStopsAt={0f,.7f,.85f,1f},RingStopsA={0f,0f,1f,0f},RingStopsAt2={0f,.62f,.72f,.9f,1f},RingStopsA2={0f,0f,1f,1f,0f};

        // ---- MODE ----------------------------------------------------------------------------------------------------------
        private void ExpModePanel(X.Layout L,X.Seat seat,K.Expedition pick,int start,X.Launch go,float a,float now)
        {
            if(a<=.01f)return;var m=X.Panel(L.Mode,L.Compact);bool edit=X.Editable(seat),compact=L.Compact,live=ExpLive(a);var lr=ExpRect(m.Label);float lh=lr.height-8;
            U.Text(new Rect(lr.x,lr.y,200,lh),"MODE",11,C.CyanSoft(.9f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.26f);float lx=lr.x+U.Measure("MODE",11,true,true,.26f).x+12,rx=lr.xMax;
            if(edit){U.Text(new Rect(rx-60,lr.y,60,lh),"SWITCH",9.5f,C.Muted(.85f*a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.16f);rx-=U.Measure("SWITCH",9.5f,true,true,.16f).x+8;float kw=H.KeyWidth("TAB",18);rx-=kw;H.Key(new Rect(rx,lr.y+(lh-18)/2,kw,18),"TAB",.9f*a);rx-=12;}
            U.Fill(new Rect(lx,lr.y+lh/2,Mathf.Max(0,rx-lx),H.Px),C.Edge(.7f*a));
            string runChip=pick.Locked?K.ComingSoon:"1–4 PLAYERS";Color runTone=pick.Locked?C.Muted():C.Violet();
            string waveChip=pick.Locked||!pick.WaveDefense?K.ComingSoon:"CO-OP · UP TO 4";Color waveTone=pick.Locked?C.Muted():C.Good();
            if(ExpModeTile(ExpRect(m.Expedition),0,"route",X.ExpeditionLabel,compact?"The full run":"The full run · hub, three areas, the final boss",runChip,runTone,expMode==X.Mode.Expedition,edit,a,now,compact))ExpSetMode(X.Mode.Expedition);
            if(ExpModeTile(ExpRect(m.Waves),1,"waves",X.WavesLabel,compact?"Waves, then the Siren":"Ward Zero's waves, then the Siren · today's boss test",waveChip,waveTone,expMode==X.Mode.Waves,edit,a,now,compact))ExpSetMode(X.Mode.Waves);
            var d=ExpRect(m.Detail);if(expMode==X.Mode.Waves)ExpWaveRow(d,pick,start,edit,a,now,compact);else ExpRunNotes(d,pick,go,a,compact);
            ExpLoadoutRow(ExpRect(m.Loadout),seat,a,now,live,compact);
        }
        // A mode: its constellation glyph in a hexagon, the title and line, the availability chip at the top right, a radio at
        // the right; picked: the cyan edge, glow and brackets. A guest's tiles are the host's mode, read-only.
        private bool ExpModeTile(Rect r,int index,string glyph,string title,string line,string chip,Color chipTone,bool on,bool edit,float a,float now,bool compact)
        {
            bool live=ExpLive(a)&&edit;float h=U.HoverAmount(9500+index,r,live),s=on?1:0;
            if(on)U.Glow(r,C.Cyan(.14f*a),14);H.Cut(r,U.A(U.Mix(U.Mix(C.Indigo(.78f),U.Hex(0x18244e,.9f),h),U.Hex(0x0b3049,.92f),s),a),10);H.Scan(r,C.Cyan(.03f*a),10);
            H.Outline(r,U.A(on?C.Cyan():U.Mix(C.Edge(),C.CyanSoft(),h*.7f),a),10);if(on){U.Fill(new Rect(r.x+1,r.y+10,H.Snap(3),r.height-20),C.Cyan(a));H.Brackets(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),C.CyanSoft(.8f*a),10);}
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
        // START AT: Wave 1, Wave 2, Wave 3 and the Siren; the room carries only skipWaves, so waves 2 and 3 stay locked.
        private void ExpWaveRow(Rect d,K.Expedition pick,int start,bool edit,float a,float now,bool compact)
        {
            bool live=ExpLive(a)&&edit&&!pick.Locked&&pick.WaveDefense;float lh=compact?16:18;
            U.Text(new Rect(d.x,d.y,200,lh),"START AT",9.5f,C.CyanSoft(.85f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.24f);
            string keys=edit?"1 – 4":"SET BY THE HOST";U.Text(new Rect(d.x,d.y,d.width,lh),keys,9.5f,C.Muted(.8f*a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.16f);
            var counts=ExpWaveCounts();float gap=8,th=compact?42:58,tw=(d.width-gap*3)/X.WaveStarts,ty=d.y+lh+(compact?4:6);
            for(int i=0;i<X.WaveStarts;i++)
            {
                var t=new Rect(d.x+i*(tw+gap),ty,tw,th);bool ok=X.WaveSupported(i)&&!pick.Locked&&pick.WaveDefense,on=i==start&&ok,siren=i==X.Siren;float h=U.HoverAmount(9520+i,t,live&&ok);
                if(on)U.Glow(t,siren?C.Violet(.25f*a):C.Cyan(.2f*a),10);
                H.Cut(t,U.A(on?U.Hex(siren?0x2a1650:0x0b3049,.95f):U.Mix(C.Indigo(ok?.8f:.45f),U.Hex(0x18244e,.9f),h),a),7);
                H.Outline(t,U.A(on?(siren?U.Mix(C.Violet(),Color.white,.3f):C.Cyan()):ok?U.Mix(C.Edge(),C.CyanSoft(),h*.7f):C.Edge(.55f),a),7);
                if(on)H.Brackets(new Rect(t.x-2,t.y-2,t.width+4,t.height+4),siren?C.Violet(.9f*a):C.CyanSoft(.85f*a),7);
                string label=X.WaveLabels[i];float ls=compact?12:14;Color ink=ok?U.Mix(C.Ink(on?1:.85f),Color.white,h):C.Muted(.6f);
                if(siren&&!compact)Skull(new Rect(t.center.x-9,t.y+6,18,18),a*(ok?1:.5f),on?1:0);
                U.Text(new Rect(t.x,siren&&!compact?t.y+22:t.y+(compact?5:10),t.width,ls*1.3f),label,ls,U.A(ink,a),TextAnchor.MiddleCenter,bold:true,tracking:.1f);
                string sub=!ok?"LOCKED":siren?"BOSS":(counts!=null&&i<counts.Length?counts[i]+" HOSTILES":"WAVE");
                if(!ok){float sw=U.Measure(sub,8.5f,true,true,.14f).x;ExpLock(new Rect(t.center.x-sw/2-14,t.yMax-(compact?15:18),10,10),C.Muted(.75f*a));U.Text(new Rect(t.center.x-sw/2,t.yMax-(compact?18:21),sw+4,14),sub,8.5f,C.Muted(.75f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);}
                else if(!(siren&&compact))U.Text(new Rect(t.x,t.yMax-(compact?18:21),t.width,14),sub,8.5f,U.A(siren?U.Mix(C.Violet(),Color.white,.4f):C.CyanSoft(),.85f*a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.14f);
                if(live&&ok&&U.Button(t)&&!on)ExpPickWave(i,edit);
                else if(ExpLive(a)&&edit&&!ok&&U.Hover(t)&&Event.current.type==EventType.MouseDown){Event.current.Use();Notice(X.WaveLocked);}
            }
            float ny=ty+th+(compact?4:8);if(ny+16<=d.yMax)
            {
                string note=pick.Locked||!pick.WaveDefense?X.Placeholder:start==X.Siren?"Starts at the Siren: no waves, straight to the boss.":"Three waves of minions, then the Siren and her four chips.";
                U.Text(new Rect(d.x,ny,d.width,16),Fit(note,11.5f,d.width,false,0),11.5f,C.Muted(.95f*a),TextAnchor.MiddleLeft);
                if(ny+34<=d.yMax)U.Text(new Rect(d.x,ny+18,d.width,16),Fit(X.WaveLocked,11,d.width,false,0),11,C.Dim(a),TextAnchor.MiddleLeft);
            }
        }
        private int[] ExpWaveCounts()
        {
            if(expWaveCounts!=null)return expWaveCounts;expWaveCounts=Array.Empty<int>();
            try{var t=Resources.Load<TextAsset>("PveContent");var pve=t!=null?JsonUtility.FromJson<NativePveContent>(t.text):null;if(pve?.waves!=null){var c=new int[pve.waves.Length];for(int i=0;i<c.Length;i++)c[i]=pve.waves[i]?.count??0;expWaveCounts=c;}}
            catch(Exception e){Debug.LogException(e);}
            return expWaveCounts;
        }
        // The full run's notes: what M1 plays today, the controls, and why it cannot start here (the honest reason).
        private void ExpRunNotes(Rect d,K.Expedition pick,X.Launch go,float a,bool compact)
        {
            float y=d.y;float lh=compact?16:19;
            if(pick.Locked){ExpNote(ref y,d,"circle-help",X.Placeholder,C.Muted(),a,lh);return;}
            ExpNote(ref y,d,"route-dot","Expedition City hub, then Ward Zero, then the final gate.",C.Ink(.9f),a,lh);
            if(!compact)ExpNote(ref y,d,"route-dot","The Siren waits at the hub checkpoint in M1.",C.Ink(.9f),a,lh);
            ExpNote(ref y,d,"route-dot","Stand in a circle to deploy · F at machines · J for the run board.",C.Muted(),a,lh);
            if(!go.Enabled&&go.Kind!=X.LaunchKind.Ready&&!string.IsNullOrEmpty(go.Reason)&&y+lh<=d.yMax+1){y+=compact?2:6;ExpNote(ref y,d,"circle-alert",go.Reason,C.Warn(),a,lh);}
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
            var o=RulesNow();int bots=ExpBotsShown();string party=bots==0?"NO BOTS":bots+(bots==1?" BOT":" BOTS")+((o?.mirrorWeapons??prefs.mirrorWeapons)?" · MIRROR":" · OWN LOADOUTS");
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
            else if(seat==X.Seat.Guest){lead=networkReady?"You are ready.":"Ready up";rest=networkReady?"Waiting for the host to start.":"when your loadout is set. The host picks the run.";tone=networkReady?C.Good():C.CyanSoft();}
            else if(!go.Enabled&&!string.IsNullOrEmpty(go.Reason)){lead=go.Reason;rest="";tone=C.Warn();}
            else if(seat==X.Seat.Host&&waiting>0){lead="Waiting for "+waiting+" pilot"+(waiting==1?"":"s");rest="· start when every pilot is ready.";tone=C.CyanSoft();}
            else if(seat==X.Seat.Host){lead="Everyone is ready.";rest=n>1?"Launch when you are.":"Share the code, add a bot, or launch.";tone=C.Good();}
            else{int bots=ExpBots(party);lead="Party of "+n;rest=bots>0?"· "+bots+(bots==1?" bot fills":" bots fill")+" the empty seats.":"· host a room to bring friends.";tone=C.CyanSoft();}
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
            if(expMode==X.Mode.Expedition)Fine("Expedition supports 1–4 signed-in players. Bots are available in Wave Defense.");
            else if(o==null)Fine("The host sets the party bots. Ready up when your loadout is set.");
            else
            {
                RuleSelect(7580,RoomOpt.Bots,"Party bots",PartyBotLabels,Mathf.Clamp(o.bots,0,K.PartyCap-1));
                RuleFine(7584,"AI pilots fill the party (up to 4). A friend who joins replaces the newest bot.",0,6);
                RuleCheck(7586,RoomOpt.Mirror,rulesLocked?"Party bots use the host's weapon":"Party bots use my weapon (otherwise mixed loadouts)",o.mirrorWeapons);
            }
            ClassRow("Your class",ClassCardStyle.Stacked);WeaponCards("Your weapon");
            Fine(seat==X.Seat.Guest?"After a change, Send Loadout so the host gives you this weapon.":"Intros play as normal. Earn money from minions to buy chips, heals, revives and recruits. F interacts · C opens your chips; chips you take wait there until you choose.");
        }
    }
}
