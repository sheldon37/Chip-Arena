using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
using C=ChipArena.UnityConversion.NativeCyberTheme;
namespace ChipArena.UnityConversion
{
    // The PvP room (redesigned 2026-11-10 at the owner's request: "Improve UI for create room screen and pvp lobby ... give players the
    // options to change their weapon abilities in lobby as well ... a very clean / beautiful design"). The room fills the canvas: the room code
    // chip beside Customize and Settings in the header; the roster of pilot cards (portrait, name, weapon, both abilities, ready band, the team
    // chip in a teams room, open slots the host fills with a bot); the pilot's own loadout (the weapon and the Q and E slots, each opening its
    // picker in a sheet over the room) over the match setup (the host's rules, read-only for guests); the bar with Leave, the room's status
    // and Start or Ready. Every card, bots included, comes from the broadcast seats (LobbySeats). The roster, the loadout and the sheet are in
    // NativeLobbyRoomUI.cs. Drawing only: settings go through SetRoomOptions / ChangeTeam / GuestReady (NativeLobbyLogic.cs, PR #66); a change
    // of loadout reaches the room through the profile and ready messages the room already has (LbSync).
    public sealed partial class ConversionGame
    {
        private struct LobbyPilot{public string name,tag,weapon,techniques;public bool ready,connected,bot,you;public Color accent,body;public int team,id;}
        private Vector2 lobbyRosterScroll,lobbyRoomScroll;private float lobbyRosterContent=400,lobbyRoomContent=400;private bool lobbyAdvanced;
        private static readonly string[] TeamNames={"Red","Blue","Green","Yellow","Purple","Orange","Cyan","Pink"};
        private static readonly Color ReadyGreen=U.Hex(0x3ddc84);
        private static string ClassName(string c)=>c=="ranged"?"Ranged":c=="magic"?"Magic":c=="melee"?"Melee":"???";

        // Everyone in the room in seat order: the broadcast seats (the viewer's own card shows its local name and
        // colours), the host's own card if no broadcast has listed it yet, then the host's bots or party bots.
        // A bot seat (NativeLobbyRules.Bot: id 100 + its index) is a BOT card with the seat's colours and team.
        private readonly List<LobbyPilot> lobbyRoster=new List<LobbyPilot>(8);
        private List<LobbyPilot> LobbyRoster()
        {
            var list=lobbyRoster;list.Clear();int me=Hosting?(dedicated?-1:1):networkId;bool own=false;
            foreach(var s in RoomSeats)
            {
                if(s==null)continue;var w=LobbyWeapon(s.spec);bool you=s.id==me;own|=s.id==1;
                list.Add(new LobbyPilot{id=s.id,you=you,name=you?NativeCosmeticRules.Name(prefs.name):s.name,weapon=s.spec,techniques=s.weaponTechniques,tag=you?"YOU":s.bot?"BOT":s.id==1&&!Hosting?"ROOM OWNER":"PLAYER",bot=s.bot,ready=s.ready,connected=s.connected,team=s.team,
                    accent=you?HexColor(prefs.accentColor,0x70e1d0):HexColor(s.accentColor,w!=null?(int)w.color:0xb8aabb),body=you?HexColor(prefs.bodyColor,0xe5e7eb):HexColor(s.bodyColor,0xe5e7eb)});
            }
            if(Hosting&&!dedicated&&!own)list.Insert(0,new LobbyPilot{id=1,you=true,name=NativeCosmeticRules.Name(prefs.name),weapon=prefs.weapon,techniques=prefs.weaponTechniques,tag="YOU",ready=true,connected=true,team=prefs.team,accent=HexColor(prefs.accentColor,0x70e1d0),body=HexColor(prefs.bodyColor,0xe5e7eb)});
            return list;
        }
        private ContentWeapon LobbyWeapon(string id){if(id!=null)foreach(var w in content.weapons)if(w.id==id)return w;return null;}
        // A teams room as the viewer knows it: the host's own setting, or the guest's copy of the host's rules once this
        // connection has received them.
        private bool RoomTeams=>!prefs.pve&&(RulesGuest?GuestRules?.teams==true:prefs.teams);
        // main.ts:156 teamsBlocked: every pilot on one team.
        private static bool TeamsBlocked(List<LobbyPilot> roster){int mask=0,n=0;foreach(var p in roster){int bit=1<<TeamIndex(p.team);if((mask&bit)==0){mask|=bit;n++;}}return n<2;}

        private void LobbyScreen(float age)
        {
            bool pve=prefs.pve;roomGuest=!Hosting;
            // A PvE room meets on the Expeditions screen (NativeExpeditionsMenu.cs): party strip, pick, mode, code and Start / Ready.
            if(pve){ExpeditionsScreen(age);return;}
            // The room fills the canvas.
            screenContent=4000;
            // The team list and the picker sheet belong to this screen: they close when the lobby was not drawn a moment ago (left and re-entered).
            // (By frames, not time: a long frame, a portrait being made, is not a new visit.)
            if(Time.frameCount-lobbySeenFrame>3){teamPopupSeat=0;LbEnter();}lobbySeenFrame=Time.frameCount;lobbySeenAt=Time.unscaledTime;
            LbDrawerInput();if(!LbDrawerShown)TeamPopupInput();
            var l=BeginScreen("PRIVATE ROOM","Match lobby",Mathf.Min(1520,U.W-48),age,false,actions:false,close:false,lobby:true);lbPanel=l.Panel;float e=l.Enter;
            // While the picker sheet is open nothing under it takes the pointer or the pad: the pointer is moved off the canvas for the room's
            // controls (their layout stays as it is) and the pad's focus is clipped away from them.
            var ev=Event.current;bool cover=LbDrawerShown;var mouse=ev.mousePosition;if(cover){ev.mousePosition=new Vector2(-99999,-99999);NativeControllerPointer.SetClip(new Rect(-60,-60,1,1));}
            try
            {
                // Customize and Settings open over the room (their close returns here); saving a new look updates the room. Beside them, the code.
                float bx=l.Panel.xMax-28;foreach(var (icon,text,screen) in new[]{("settings","Settings",MenuScreen.Settings),("palette","Customize",MenuScreen.Customize)})
                {float w=U.Measure(text,13,true).x+19+18+28;bx-=w;if(MenuButton(new Rect(bx,l.Panel.y+28,w,42),icon,text,null,false,true,e,13,true))Go(screen);bx-=10;}
                RoomAddresses(LbCodeW-120);LbCodeChip(new Rect(bx-6-LbCodeW,l.Panel.y+23,LbCodeW,52),e);
                var b=l.Body;float W=U.W,gap=W<1250?22:28,side=W<1250?350:400,barH=74;
                var roster=LobbyRoster();bool teams=RoomTeams,blocked=teams&&TeamsBlocked(roster);TeamPopupCheck(roster,teams);
                var main=new Rect(b.x,b.y+4,b.width-side-gap,b.height-barH-22);var col=new Rect(main.xMax+gap,main.y,side,main.height);
                LbRoster(main,roster,e,teams);
                U.Fill(new Rect(col.x-gap/2f,col.y,1,col.height),C.Edge(.5f*e));
                LbLoadoutPanel(new Rect(col.x,col.y,col.width,LbLoadoutH),e);
                LobbyRoomColumn(new Rect(col.x,col.y+LbLoadoutH+14,col.width,col.height-LbLoadoutH-14),e);
                LobbyBar(new Rect(b.x,b.yMax-barH,b.width,barH),roster,e,blocked);
            }
            finally{if(cover){ev.mousePosition=mouse;NativeControllerPointer.ClearClip();}}
            LbDrawer(e);TeamPopupDraw();LbSync();
        }
        // The bar under the room: Leave Room at the left; Start (the host) or Ready (a guest) at the right; between them the room's status, the
        // network's message (copied, errors) above it. A teams room with a single team cannot start (main.ts:156 teamsBlocked).
        private void LobbyBar(Rect r,List<LobbyPilot> roster,float e,bool blocked)
        {
            U.Fill(new Rect(r.x,r.y,r.width,1),C.Edge(.7f*e));U.Fill(new Rect(r.x,r.y,90,1),C.Cyan(.85f*e));
            float y=r.y+16,h=52,lw=U.Measure("Leave Room",14,true).x+19+18+34;
            if(MenuButton(new Rect(r.x,y+2,lw,h-4),"log-out","Leave Room",null,false,true,e,14,true))LeaveRoom();
            float pw=Mathf.Min(330,r.width*.3f);var pr=new Rect(r.xMax-pw,y,pw,h);
            if(Hosting){if(MenuButton(pr,"play",trainingRoom?"Enter Training Room":prefs.pve?"Start Boss Fight":"Start Match",null,true,server==null&&GuestsReady&&!blocked,e,17,true))RequestStage(prefs.pve?StageLoadKind.Boss:StageLoadKind.Online,StartMatch);}
            else{if(MenuButton(pr,"check",networkReady?"Cancel Ready":"Ready",null,!networkReady,server!=null&&!blocked,e,17,true))GuestReady(!networkReady);if(networkReady){U.Glow(pr,C.Good(.2f*e),14);U.Round(pr,Color.clear,4,C.Good(.8f*e));}}
            float sx=r.x+lw+28,sw=pr.x-28-sx;int ready=0;foreach(var p in roster)if(p.ready&&p.connected)ready++;bool note=!string.IsNullOrEmpty(networkStatus);
            if(note)U.Text(new Rect(sx,y-2,sw,16),Fit(networkStatus,12,sw,false,0),12,NetworkError?C.Danger(e):C.Muted(e),TextAnchor.MiddleLeft);
            ReadyLine(new Rect(sx,note?y+10:y,sw,h),ready,roster.Count,e,blocked);
        }

        // .pilot-card in .lobby-roster: top row (49 px, 40 px when large, 61 px wrapped), the portrait, the name (20 px; 17 px compact,
        // line-height 1.2), a 7 px gap, the class line (24 / 21 px) with its 8 px underline pad, and 17 px (14 px) below.
        private static float CardHeight(float top,bool compact,float portrait)=>top+portrait+(compact?20.4f:24)+7+(compact?24:28)+8+(compact?14:17);
        // `result`: the PvE results variant (pve.css .pve-result .pilot-card) without the ready pill or class line, the name
        // at 18 px.
        private void LobbyCard(Rect r,LobbyPilot p,int index,float portrait,float topH,bool wrap,bool compact,float e,float t,bool result=false)
        {
            var weapon=LobbyWeapon(p.weapon);Color style=weapon!=null?U.Hex((int)weapon.color):U.Hex(0xb8aabb);bool ready=p.ready&&p.connected;float a=e*(p.connected?1:.55f);
            // box-shadow 0 18px 45px #0005; ready: #3ddc84 edge with a 1 px #3ddc8466 ring and a 28 px #3ddc8426 glow.
            U.Glow(new Rect(r.x,r.y+18,r.width,r.height),new Color(0,0,0,.33f*a),45);
            if(ready){U.Glow(r,U.A(ReadyGreen,.17f*a),28);U.Round(new Rect(r.x-1,r.y-1,r.width+2,r.height+2),Color.clear,12,U.A(ReadyGreen,.4f*a));}
            U.RoundGradient(r,U.Hex(0x2d2832,a),U.Hex(0x15171e,a),11,ready?U.A(ReadyGreen,a):U.Hex(0x514955,a),.7f);
            // radial-gradient(ellipse at 50% 42%, accent 16%, transparent 64%), kept inside the card.
            U.RadialStops(new Rect(r.x+r.width*.5f-r.width*.72f,r.y+r.height*.42f-r.height*.66f,r.width*1.44f,r.height*1.32f),U.A(p.accent,.16f*a),new[]{0f,.64f,1f},new[]{1f,0f,0f});
            // :before — a 3 px accent bar down the left edge, transparent at both ends (green when ready).
            Color bar=ready?ReadyGreen:p.accent;float mid=r.y+r.height/2;U.Fade(new Rect(r.x+1,r.y+2,3,mid-r.y-2),U.A(bar,.75f*a),false);U.Fade(new Rect(r.x+1,mid,3,r.yMax-2-mid),U.A(bar,.75f*a));
            // .pilot-card-top: the seat number in the accent (14 px mono), the tag, and the ready pill at the right.
            float top=r.y+12,lh=wrap?17:26;string num=(index+1).ToString("00");U.Text(new Rect(r.x+12,top,40,lh),num,14,U.A(p.accent,a),TextAnchor.MiddleLeft,mono:true);
            float tx=r.x+12+U.Measure(num,14,false,true).x+6,pillX=r.xMax-12;if(!result)ReadyPill(new Rect(r.x,wrap?top+lh+6:top,r.width-12,26),p,a,out pillX);float room=(wrap||result?r.xMax-12:pillX-6)-tx;
            U.Text(new Rect(tx,top,Mathf.Max(10,room),lh),Fit(p.tag,10,Mathf.Max(10,room),false,.04f),10,U.Hex(0xc0b8c5,a),TextAnchor.MiddleLeft,tracking:.04f);
            // .portrait-stage: the emblem, sized to the portrait, its motion offset per seat.
            var stage=new Rect(r.x,r.y+topH,r.width,portrait);float ring=Mathf.Min(portrait*.3f,r.width*.27f);var cpos=new Vector2(stage.center.x,stage.center.y+2);
            if(portrait>=40)LobbyPortrait(stage,cpos,ring,p,index,a,t,weapon,style,result);
            // .pilot-card-name: the name, then the class line in the weapon colour (38% mix with white, glowing) over its
            // animated 2 px underline (.pilot-class-fx).
            float pad=compact?12:14,ny=stage.yMax+(result?4:0),ns=result?18:compact?17:20,nh=ns*1.2f;
            U.Text(new Rect(r.x+pad,ny,r.width-pad*2,nh),Fit(p.name,ns,r.width-pad*2,true,-.035f),ns,U.Hex(0xf4f4f5,a),TextAnchor.MiddleLeft,bold:true,tracking:-.035f);if(result)return;
            float cy=ny+nh+7,cs=compact?21:24,ch=compact?24:28,ic=compact?20:24;string cls=weapon!=null?ClassName(weapon.@class):"Mixed";
            Color clsColor=U.Mix(Color.white,style,.38f);float cw=Mathf.Min(r.width-pad*2,ic+(compact?6:9)+U.Measure(cls,cs,true).x);
            NativeIcons.Draw(new Rect(r.x+pad,cy+(ch-ic)/2,ic,ic),weapon!=null?NativeIcons.Class(weapon.@class):"shuffle",U.A(style,a));
            U.Text(new Rect(r.x+pad+ic+(compact?6:9),cy,cw,ch),cls,cs,U.A(clsColor,a),TextAnchor.MiddleLeft,bold:true,glow:U.A(style,.35f*a));
            ClassUnderline(new Rect(r.x+pad,cy+ch+6,cw,2),weapon?.id,style,a,t+index*.9f);
        }
        // .pilot-ready: NOT READY (clock) in the warm outline, READY (check) on solid #3ddc84; OFFLINE when disconnected.
        private static string PillText(LobbyPilot p)=>!p.connected?"OFFLINE":p.ready?"READY":"NOT READY";
        private static float PillWidth(LobbyPilot p)=>12+14+6+U.Measure(PillText(p),12,true,false,.12f).x+12;
        private static void ReadyPill(Rect row,LobbyPilot p,float a,out float x)
        {
            bool ready=p.ready&&p.connected;string text=PillText(p);string icon=!p.connected?"circle-dashed":ready?"check":"clock-3";
            float tw=U.Measure(text,12,true,false,.12f).x,w=PillWidth(p),h=row.height;var pill=new Rect(row.xMax-w,row.y,w,h);x=pill.x;
            if(ready)U.Glow(pill,U.A(ReadyGreen,.3f*a),16);
            U.Round(pill,ready?U.A(ReadyGreen,a):U.Hex(0x1b1a20,.9f*a),Mathf.Min(16,h/2),ready?U.A(ReadyGreen,a):!p.connected?new Color(1,1,1,.12f*a):U.Hex(0xf4b878,.4f*a));
            Color ink=ready?U.Hex(0x062413,a):!p.connected?U.Hex(0xa6a7ad,a):U.Hex(NativeCyberTheme.CyanSoftHex,a);
            NativeIcons.Draw(new Rect(pill.x+12,pill.center.y-7,14,14),icon,ink);U.Text(new Rect(pill.x+12+14+6,pill.y,tw+4,h),text,12,ink,TextAnchor.MiddleLeft,bold:true,tracking:.12f);
        }
        // .pilot-class-fx: a dim 2 px line in the weapon colour with a light that sweeps across it (lobby-metal, 3.6 s);
        // the source's per-weapon variants: a dashed pulse (machine gun, L33T), a short scanning segment (rifle), a
        // flickering current (lightning, Emperor) and a breathing tide (water, darkness, shield).
        // Lightning / Emperor: the 7 px bar's zigzag current (clip-path polygon) under linear-gradient(90deg, transparent, the
        // weapon colour, #fff8, transparent), drawn as the colour's and the white stop's shares of that gradient.
        private static readonly Vector2[] Bolt={new Vector2(0,.35f),new Vector2(.24f,.35f),new Vector2(.30f,0),new Vector2(.46f,.70f),new Vector2(.53f,.25f),new Vector2(.76f,.40f),new Vector2(1,.30f),new Vector2(1,.65f),new Vector2(.74f,.70f),new Vector2(.54f,.55f),new Vector2(.46f,1),new Vector2(.29f,.45f),new Vector2(.24f,.65f),new Vector2(0,.70f)};
        private static readonly float[] BoltAt={0,1/3f,2/3f,1},BoltColour={0,1,0,0},BoltWhite={0,0,.533f,0};
        private static void ClassUnderline(Rect r,string spec,Color style,float a,float t)
        {
            bool bolt=spec=="lightning"||spec=="emperor";if(bolt)r=new Rect(r.x,r.yMax-7,r.width,7);
            U.Fill(r,U.A(style,.2f*a));U.Glow(r,U.A(style,.18f*a),6);
            if(spec=="machinegun"||spec=="leet"){float pulse=.3f+.6f*(.5f+.5f*Mathf.Sin(t*Mathf.PI*2/1.8f));for(float x=r.x;x<r.xMax;x+=10)U.Fill(new Rect(x,r.y,Mathf.Min(5,r.xMax-x),r.height),U.A(style,pulse*a));return;}
            if(spec=="rifle"){float k=.5f-.5f*Mathf.Cos(t*Mathf.PI*2/3);float w=r.width*.25f;U.Fill(new Rect(r.x+k*(r.width-w),r.y,w,r.height),U.A(style,.9f*a));return;}
            if(bolt){float ph=Mathf.Repeat(t/2.8f,1);float o=.7f*(ph>.70f&&ph<.82f?.9f:ph>=.82f&&ph<.86f?.45f:.3f);U.Polygon(r,Bolt,BoltAt,BoltColour,U.A(style,o*a));U.Polygon(r,Bolt,BoltAt,BoltWhite,U.A(Color.white,o*a));return;}
            if(spec=="water"||spec=="darkness"||spec=="shield"){float period=spec=="water"?3:spec=="shield"?3.8f:4;float k=.5f-.5f*Mathf.Cos(t*Mathf.PI*2/period);float w=r.width*(.35f+.65f*k);var tide=new Rect(r.center.x-w/2,r.y,w,r.height);
                // Darkness: filter: blur(2px) spreads the tide into a soft band about a third as bright at its core.
                if(spec=="darkness")U.Glow(tide,U.A(style,.45f*(.4f+.6f*k)*a),4);else U.Fill(tide,U.A(style,(.4f+.6f*k)*a));return;}
            float dur=spec=="pistol"?2.8f:spec=="daggers"?1.8f:spec=="greatsword"?5f:3.6f,f=Mathf.Repeat(t/dur,1);float pos=f<.9f?Mathf.Lerp(-1,1,f/.9f):1,alpha=f<.2f?f/.2f:f<.8f?1:f<.9f?(.9f-f)/.1f:0;
            float sw=r.width*.45f,sx=r.x+(pos+1)/2*(r.width+sw)-sw;float x0=Mathf.Max(r.x,sx),x1=Mathf.Min(r.xMax,sx+sw);
            if(x1>x0){float half=(x0+x1)/2;U.HGradient(new Rect(x0,r.y,half-x0,r.height),Color.clear,U.A(Color.Lerp(style,Color.white,.4f),alpha*a));U.HGradient(new Rect(half,r.y,x1-half,r.height),U.A(Color.Lerp(style,Color.white,.4f),alpha*a),Color.clear);}
        }
        // .team-assignment under a card in a teams room (team-ui.ts teamPicker, lobby.css:87): the 9 px team dot with its 9 px
        // glow at 45% and "Team" (12 px #c7c1ce), and the 100 x 34 select at the right (#222329, its edge the team colour
        // mixed 45% into #333, the 12 px name and a chevron). Editable for the viewer's own card (ChangeTeam) and, for the
        // host, every seat, guests and bots (RequestTeam), as main.ts:156 teamPicker(p,p.id===id||roomOwner===id); a click
        // opens the team list. Others are drawn disabled, at half strength.
        private const string ChevronDown="<path d=\"M6 9 L12 15 L18 9\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>";
        private void TeamPicker(Rect r,LobbyPilot p,bool editable,float a,Vector2 toCanvas)
        {
            int team=TeamIndex(p.team);Color tc=TeamColor(team);var box=new Rect(r.xMax-100,r.y,100,r.height);float h=U.HoverAmount(7800+p.id,box,editable&&!MenuBlocked()),s=editable?1:.5f;
            if(Event.current.type==EventType.Repaint)
            {
                var dot=new Rect(r.x,r.center.y-4.5f,9,9);U.Radial(new Rect(dot.x-9,dot.y-9,27,27),U.A(tc,.3f*a));U.Disc(dot,U.A(tc,a));U.Text(new Rect(r.x+15,r.y,Mathf.Max(10,box.x-r.x-20),r.height),"Team",12,U.Hex(0xc7c1ce,a),TextAnchor.MiddleLeft);
                U.Round(box,U.Hex(NativeCyberTheme.IndigoHex,a),4,U.A(U.Mix(U.Mix(U.Hex(0x333333),tc,.45f),U.Hex(NativeCyberTheme.CyanHex),h),a*s));
                U.Text(new Rect(box.x+8,box.y,box.width-30,box.height),TeamNames[team],12,U.Hex(0xf5f5f7,a*s),TextAnchor.MiddleLeft);
                NativeIcons.DrawMarkup(new Rect(box.xMax-21,box.center.y-7,14,14),"chevron-down",ChevronDown,24,U.Hex(0xf5f5f7,.7f*a*s));
            }
            if(U.Button(box,editable)){Click();teamPopupSeat=p.id;teamPopupSelf=p.you;teamPopupTeam=team;teamPopupAnchor=new Rect(box.x+toCanvas.x,box.y+toCanvas.y,box.width,box.height);teamPopupScroll=lobbyRosterScroll;}
        }
        // The open team list (8 rows of 28 px, 140 px wide; #222329 with a #484952 edge, #303138 on hover, the current team
        // ticked), under its picker or above it at the canvas foot.
        private float lobbySeenAt=-1;private int teamPopupSeat,teamPopupTeam;private bool teamPopupSelf;private Rect teamPopupAnchor;private Vector2 teamPopupScroll;
        private Rect TeamPopupRect(){const float w=140,h=8*28+8;var a=teamPopupAnchor;float x=Mathf.Clamp(a.xMax-w,8,U.W-8-w),y=a.yMax+4;if(y+h>U.H-8)y=a.y-4-h;return new Rect(x,Mathf.Max(8,y),w,h);}
        private static Rect TeamPopupRow(Rect box,int t)=>new Rect(box.x+4,box.y+4+t*28,box.width-8,28);
        // Before the lobby draws a control, so the list takes the click from what lies under it: a row picks that team,
        // anywhere else closes the list; the hover under the list is suppressed. The list is anchored in canvas space, so
        // the wheel over it is taken too (it would scroll the roster beneath), and TeamPopupCheck closes it once the
        // roster scrolls any other way.
        private void TeamPopupInput()
        {
            if(teamPopupSeat==0)return;var box=TeamPopupRect();if(U.Hover(box))menuBlockedNow=true;var ev=Event.current;
            if(ev.type==EventType.ScrollWheel&&U.Hover(box)){ev.Use();return;}if(ev.type!=EventType.MouseDown)return;
            for(int t=0;t<8;t++)if(U.Hover(TeamPopupRow(box,t))){if(teamPopupSelf)ChangeTeam(t);else if(Hosting)RequestTeam(teamPopupSeat,t);Click();break;}
            teamPopupSeat=0;ev.Use();
        }
        // The list closes when its seat leaves, the room stops being a teams room or the roster has scrolled from under it.
        private void TeamPopupCheck(List<LobbyPilot> roster,bool teams){if(teamPopupSeat==0)return;bool found=false;foreach(var p in roster)found|=p.id==teamPopupSeat;if(!found||!teams||lobbyRosterScroll!=teamPopupScroll)teamPopupSeat=0;}
        private void TeamPopupDraw()
        {
            if(teamPopupSeat==0||Event.current.type!=EventType.Repaint)return;var box=TeamPopupRect();
            U.Glow(new Rect(box.x,box.y+8,box.width,box.height),new Color(0,0,0,.45f),20);U.Backdrop(box,4,12,1);U.Round(box,C.Navy(.97f),6,C.Edge());
            for(int t=0;t<8;t++)
            {
                var row=TeamPopupRow(box,t);if(U.Hover(row))U.Round(row,C.Indigo(),4);var dot=new Rect(row.x+9,row.center.y-4.5f,9,9);U.Disc(dot,TeamColor(t));
                U.Text(new Rect(row.x+26,row.y,row.width-50,row.height),TeamNames[t],12,U.Hex(0xf5f5f7),TextAnchor.MiddleLeft);if(t==teamPopupTeam)NativeIcons.Draw(new Rect(row.xMax-22,row.center.y-7,14,14),"check",U.Hex(NativeCyberTheme.CyanSoftHex));
            }
        }
        // A 1 px dashed outline (6 px dashes, 4 px gaps) along the straight edges of a rounded rect.
        private static void DashedRound(Rect r,Color c,float radius)
        {
            U.Dashed(new Rect(r.x+radius,r.y,r.width-2*radius,1),c);U.Dashed(new Rect(r.x+radius,r.yMax-1,r.width-2*radius,1),c);
            U.Dashed(new Rect(r.x,r.y+radius,1,r.height-2*radius),c,false);U.Dashed(new Rect(r.xMax-1,r.y+radius,1,r.height-2*radius),c,false);
            U.Round(r,Color.clear,radius,U.A(c,.35f));
        }
        // The room's status in the bar: who the room is waiting for. The host starts the match; a guest's loadout reaches the host on its own a
        // moment after a change (LbSync).
        private void ReadyLine(Rect r,int ready,int total,float e,bool blocked=false)
        {
            if(r.width<60)return;string lead,rest;Color tone=C.CyanSoft();int waiting=Mathf.Max(0,total-ready);
            if(blocked){lead="Choose at least two teams";rest="to start the match.";tone=C.Warn();}
            else if(total==0){lead=Hosting?"Waiting for pilots":"Joining the room…";rest=Hosting?"· Share your room code.":"";}
            else if(!Hosting&&lbPendingAt>=0){lead="Sending your loadout…";rest="";}
            else if(Hosting&&!prefs.pve&&total<2){lead="Add a bot";rest="or wait for a pilot to join.";}
            else if(waiting>0){lead="Waiting for "+waiting+" pilot"+(waiting==1?"":"s");rest="· "+(Hosting?"Start when every pilot is ready.":"The host starts the match.");}
            else{lead="Everyone is ready.";rest=Hosting?(prefs.pve?"Start the boss fight when you are.":"Start the match when you are."):"Waiting for the host to start.";tone=C.Good();}
            var lines=U.Wrap(lead+"\u0001 "+rest,15,r.width,true,2);float lh=22,y=r.center.y-lines.Count*lh/2;
            foreach(var line in lines)
            {
                int cut=line.IndexOf('\u0001');if(cut<0&&line.StartsWith(lead)==false){U.Text(new Rect(r.x,y,r.width,lh),line,15,C.Muted(e),TextAnchor.MiddleLeft);y+=lh;continue;}
                string head=cut>=0?line.Substring(0,cut):line,tail=cut>=0?line.Substring(cut+1):"";float hw=U.Measure(head,15,true).x;
                U.Text(new Rect(r.x,y,hw+4,lh),head,15,U.A(tone,e),TextAnchor.MiddleLeft,bold:true);if(tail.Length>0)U.Text(new Rect(r.x+hw,y,r.width-hw,lh),tail,15,C.Muted(e),TextAnchor.MiddleLeft);y+=lh;
            }
        }
        // One line that fits `width`, cut with an ellipsis.
        private static string Fit(string text,float size,float width,bool bold,float tracking)
        {
            text=text??"";if(U.Measure(text,size,bold,false,tracking).x<=width)return text;
            while(text.Length>1&&U.Measure(text+"…",size,bold,false,tracking).x>width)text=text.Substring(0,text.Length-1);return text.TrimEnd()+"…";
        }

        // .room-advanced: the remembered "Advanced Options" fold (session-long here) with a note that summarises the
        // essentials; returns whether it is open. The frame wraps the open content using its height from the last paint.
        private float lobbyAdvancedHeight;
        private bool AdvancedFold(string note)
        {
            GUILayout.Space(14);int noteLines=Mathf.Max(1,U.Wrap(note,12,lobbyRoomInner-48,false,2).Count);var r=GUILayoutUtility.GetRect(0,48+noteLines*16,GUILayout.ExpandWidth(true));r=new Rect(r.x+4,r.y,r.width-8,r.height);bool open=lobbyAdvanced;if(open&&Event.current.type==EventType.Repaint)advancedTop=r.yMax;float extra=lobbyAdvancedHeight;
            // The open fold's backdrop goes down now, under the rules that follow; its edge and header are deferred.
            var frame=open?new Rect(r.x,r.y,r.width,r.height+extra+12):r;if(open)U.UnderRound(frame,C.Navy(.55f*GUI.color.a),9);
            U.Defer(()=>
            {
                if(Event.current.type==EventType.Repaint)
                {
                    float h=U.HoverAmount(7390,r,!MenuBlocked());
                    U.Round(frame,open?Color.clear:C.Navy(.55f),9,U.Mix(C.Edge(),C.Cyan(),.5f*h));if(h>0)U.Round(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),C.Cyan(.04f*h),9);if(open)U.Fill(new Rect(r.x+1,r.yMax-1,r.width-2,1),C.Edge());
                    FoldChevron(new Vector2(r.x+19,r.y+23),open,1);
                    U.Text(new Rect(r.x+33,r.y+12,r.width-45,22),"Advanced Options",15,C.Ink(),TextAnchor.MiddleLeft,bold:true);
                    var lines=U.Wrap(note,12,r.width-48,false,2);float y=r.y+35;foreach(var l in lines){U.Text(new Rect(r.x+33,y,r.width-45,16),l,12,C.Muted(),TextAnchor.MiddleLeft);y+=16;}
                }
                if(U.Button(r)){lobbyAdvanced=!lobbyAdvanced;Click();}
            });
            // .room-advanced>.match-options: the open content sits 14 px inside the frame on both sides.
            foldOpen=open;if(open){GUILayout.Space(10);GUILayout.BeginHorizontal();GUILayout.Space(18);GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(120,lobbyRoomInner-28)));}
            return open;
        }
        private float advancedTop;private bool foldOpen;
        // The fold's #efb587 chevron (› closed, ⌄ open) at `c`, as unrotated markup: a rotated draw inside the scroll view's
        // clip has its clip mapped by the rotation too (Unity's draw-time clip), which cut it away near the column edge.
        private const string FoldOpenMark="<path d=\"M7 9.5 L12 14.5 L17 9.5\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2.6\" stroke-linecap=\"square\" stroke-linejoin=\"miter\"/>";
        private const string FoldClosedMark="<path d=\"M9.5 7 L14.5 12 L9.5 17\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2.6\" stroke-linecap=\"square\" stroke-linejoin=\"miter\"/>";
        private static void FoldChevron(Vector2 c,bool open,float e)=>NativeIcons.DrawMarkup(new Rect(c.x-8,c.y-8,16,16),open?"fold-open":"fold-closed",open?FoldOpenMark:FoldClosedMark,24,U.Hex(NativeCyberTheme.CyanSoftHex,e));
        // Called after the open fold's content: closes its inset and keeps its height for the next paint's frame.
        private void AdvancedEnd()
        {
            if(!foldOpen)return;foldOpen=false;GUILayout.EndVertical();GUILayout.Space(10);GUILayout.EndHorizontal();
            if(Event.current.type==EventType.Repaint)lobbyAdvancedHeight=Mathf.Max(0,GUILayoutUtility.GetLastRect().yMax-advancedTop);
        }
        // The room column's inner width from the last repaint (the rows' and notes' wrap width before the column is laid out).
        private float lobbyRoomInner=254;

        // The source's select (#222329, #484952 edge warming to #ff784b on hover, 4 px corners) as a stepper: the choice,
        // then ‹ › at the right; a click on the choice steps forward. Returns the new index. Disabled (a guest's view of the
        // host's rules) it keeps its value at about half strength and takes no clicks; `alpha` is the panel's fade.
        private const string ChevronLeft="<path d=\"M15 6 L9 12 L15 18\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>";
        private const string ChevronRight="<path d=\"M9 6 L15 12 L9 18\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>";
        private int SelectField(int id,Rect r,string[] labels,int index,bool enabled=true,float alpha=1)
        {
            int n=labels?.Length??0;if(n==0)return index;index=Mathf.Clamp(index,0,n-1);
            // Under 200 px (the room column at a large interface scale) the arrows take 26 px each, leaving the value room.
            float bw=r.width<200?26:32,zone=2*bw+14;var prev=new Rect(r.xMax-6-2*bw-4,r.y+(r.height-32)/2,bw,32);var next=new Rect(r.xMax-6-bw,prev.y,bw,32);var name=new Rect(r.x,r.y,r.width-zone,r.height);
            float hp=U.HoverAmount(id+1,prev,enabled&&!MenuBlocked()),hn=U.HoverAmount(id+2,next,enabled&&!MenuBlocked()),hf=U.HoverAmount(id,r,enabled&&!MenuBlocked());
            if(Event.current.type==EventType.Repaint)
            {
                float a=alpha,t=enabled?1:.55f,c=enabled?1:.35f;
                U.Round(r,U.Hex(NativeCyberTheme.IndigoHex,a),4,U.A(U.Mix(U.Hex(NativeCyberTheme.EdgeHex),U.Hex(NativeCyberTheme.CyanHex),hf),a*(enabled?1:.7f)));if(hf>0)U.Round(r,C.Indigo(.6f*hf*a),4);
                string text=labels[index];float size=U.Measure(text,15).x<=name.width-18?15:14;U.Text(new Rect(r.x+14,r.y,name.width-16,r.height),size>14?text:Fit(text,14,name.width-18,false,0),size,U.Hex(0xf5f5f7,a*t),TextAnchor.MiddleLeft);
                U.Fill(new Rect(name.xMax-1,r.y+10,1,r.height-20),C.Edge(a));
                if(hp>0)U.Round(prev,new Color(1,1,1,.07f*hp*a),4);if(hn>0)U.Round(next,new Color(1,1,1,.07f*hn*a),4);
                NativeIcons.DrawMarkup(new Rect(prev.center.x-9,prev.y+7,18,18),"chevron-left",ChevronLeft,24,U.A(U.Mix(U.Hex(0xb9bac2),Color.white,hp),a*c));
                NativeIcons.DrawMarkup(new Rect(next.center.x-9,next.y+7,18,18),"chevron-right",ChevronRight,24,U.A(U.Mix(U.Hex(0xb9bac2),Color.white,hn),a*c));
            }
            bool back=U.Button(prev,enabled),forward=U.Button(next,enabled),cycle=U.Button(name,enabled);int step=back?-1:forward||cycle?1:0;if(step!=0){index=(index+step+n)%n;Click();}return index;
        }

        // #connection .top-meta in the lobby's top bar (main.ts drawHud; style.css .top-meta,.light): a 6 px light
        // (#59dd83, #ef9b7a when disconnected), the connection, " / " the room code and the source's " · N MS" round trip
        // from ConnectionMetrics (a room is main.ts mode online; the host's own reading is 0, as the source host's is),
        // in 12 px mono #c3c4ce on #18191ee8 with a #45464f edge and 10 / 14 px padding, right-aligned at `right`.
        // The text comes cached from ConnectionPillText (NativeConnectionReport.cs).
        // (Its width, which the Online Players chip keeps clear of: NativeSocialPanelUI.cs.)
        private static float ConnectionPillWidth(string text)=>14+6+8+U.Measure(text,12,false,true).x+14;
        private void ConnectionPill(float right,float top)
        {
            string text=ConnectionPillText(out bool lost);
            float w=ConnectionPillWidth(text);var r=new Rect(right-w,top,w,36);
            // Cyber chrome: a chamfered navy tag, the light good (danger once disconnected) with its glow.
            NativeHudChrome.Cut(r,NativeCyberTheme.Navy(.91f),NativeHudCyber.TagCut);NativeHudChrome.Outline(r,NativeCyberTheme.Edge(),NativeHudCyber.TagCut);
            var light=new Rect(r.x+14,r.center.y-3,6,6);Color tone=lost?NativeCyberTheme.Danger():NativeCyberTheme.Good();U.Glow(light,U.A(tone,.5f),4);U.Round(light,tone,3);
            U.Text(new Rect(r.x+28,r.y,w-42,r.height),text,12,NativeCyberTheme.Ink(.9f),TextAnchor.MiddleLeft,mono:true);
        }

        // ---- Match setup -------------------------------------------------------------------------------------------------
        // Under the pilot's loadout: the host's mode and the essential rules with the detailed ones under Advanced Options; a guest sees the
        // host's rules read-only once the first lobby message has carried them. Its own scroll, in a quiet frame.
        private void LobbyRoomColumn(Rect r,float e)
        {
            LbHeading(new Rect(r.x,r.y,r.width,28),"MATCH SETUP",Hosting?"· YOU HOST":"· SET BY THE HOST",e);
            var box=new Rect(r.x,r.y+36,r.width,r.height-36);U.Round(box,C.Navy(.5f*e),10,C.Edge(.65f*e));var area=new Rect(box.x+10,box.y+6,box.width-14,box.height-10);
            var c=GUI.color;GUI.color=new Color(c.r,c.g,c.b,c.a*e);U.BeginDeferred();GUILayout.BeginArea(area);
            lobbyRoomScroll=GUILayout.BeginScrollView(lobbyRoomScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar);
            var probe=GUILayoutUtility.GetRect(0,2,GUILayout.ExpandWidth(true));if(Event.current.type==EventType.Repaint)lobbyRoomInner=probe.width-8;
            if(Hosting)
            {
                LbTrainingRow();
                if(trainingRoom)LbFine("Calibration Bay · up to 8 players. Ready up, then enter together. Targets respawn; no match score.");
                else{BeginRules(true,lobbyRoomInner+8);RuleField(7476,"MODE");RuleSelect(7472,RoomOpt.Versus,null,VersusLabels,prefs.pve?1:0);choiceColumns=2;if(prefs.pve)BossBody(true);else MatchRulesBody(true);choiceColumns=3;}
            }
            else if(trainingRoom)LbFine("Training Room · Calibration Bay. Ready up to join the co-op sandbox.");
            else if(GuestRules==null)LbFine(prefs.pve?"The host sets the party bots and where the run starts. Ready up when your loadout is set.":"The host sets the mode, arena and rules. Ready up when your loadout is set.");
            else if(prefs.pve)BossBody(true);else MatchRulesBody(true);
            GUI.enabled=true;GUILayout.Space(4);if(Event.current.type==EventType.Repaint)lobbyRoomContent=GUILayoutUtility.GetLastRect().yMax;
            GUILayout.EndScrollView();GUILayout.EndArea();GUI.color=c;U.EndDeferred(area,lobbyRoomScroll);
        }
        // The host's Training Room switch, as a row of the setup.
        private void LbTrainingRow()
        {
            float e=GUI.color.a;var r=GUILayoutUtility.GetRect(0,52,GUILayout.ExpandWidth(true));r=new Rect(r.x+4,r.y+6,r.width-8,40);bool on=trainingRoom;
            U.Defer(()=>
            {
                float h=U.HoverAmount(7468,r,!MenuBlocked());
                if(Event.current.type==EventType.Repaint)
                {
                    U.Round(r,U.A(U.Mix(C.Navy(),C.Indigo(),.5f+.5f*h),e),8,U.A(U.Mix(C.Edge(),C.Cyan(),on?.7f:h*.6f),e));
                    U.Text(new Rect(r.x+14,r.y,r.width-80,r.height),"Training Room (co-op)",14,C.Ink(e),TextAnchor.MiddleLeft,bold:true);
                    var sw=new Rect(r.xMax-14-40,r.center.y-11,40,22);U.Round(sw,on?C.Cyan(.9f*e):C.Navy(e),11,U.A(on?C.CyanSoft():C.Edge(),e));var knob=new Rect(on?sw.xMax-19:sw.x+3,sw.y+3,16,16);U.Round(knob,on?C.Navy(e):C.Muted(e),8);
                }
                if(U.Button(r,!MenuBlocked())){Click();var options=CaptureRoomOptions();options.training=!on;SetRoomOptions(options);}
            });
        }
        // A muted note in the setup column, wrapped to it.
        private void LbFine(string text)
        {
            float e=GUI.color.a;var lines=U.Wrap(text,13,Mathf.Max(80,lobbyRoomInner),false,6);var r=GUILayoutUtility.GetRect(0,lines.Count*20+16,GUILayout.ExpandWidth(true));
            U.Defer(()=>{if(Event.current.type!=EventType.Repaint)return;float y=r.y+8;foreach(var l in lines){U.Text(new Rect(r.x+4,y,r.width-8,20),l,13,C.Muted(e),TextAnchor.MiddleLeft);y+=20;}});
        }
        private string RoomCodeLabel()=>!Hosting?"ROOM CODE":listener!=null?"ROOM CODE · LAN":"ROOM CODE · ONLINE";
        // The LAN address lines and the invite link, refreshed on a layout pass when the room changes and every 5 s
        // (LanAddresses walks the network interfaces): one line when it fits the card, else an address per line and the port.
        private float lanCheckedAt=-99;private int lanPort=-1;private string lanCode,inviteLink;private readonly List<string> lanLines=new List<string>(4);
        private void RoomAddresses(float width)
        {
            if(Event.current.type!=EventType.Layout&&lanPort>=0)return;int port=listener!=null?listener.Port:0;if(port==lanPort&&roomCode==lanCode&&Time.unscaledTime-lanCheckedAt<5)return;
            lanCheckedAt=Time.unscaledTime;lanPort=port;lanCode=roomCode;lanLines.Clear();inviteLink=null;if(listener==null)return;
            // With no LAN address InviteUrl falls back to 127.0.0.1, which would send a friend to their own machine: no
            // invite then, only the code (Copy Invite Link and the fold's field give way to the code note).
            var addresses=LanAddresses;string at="PORT "+port;
            if(addresses==null||addresses.Length==0){lanLines.Add("NO LAN ADDRESS · "+at);return;}inviteLink=InviteUrl;
            string one="LAN "+string.Join(", ",addresses)+" · "+at;if(U.Measure(one,10,false,false,.16f).x<=width){lanLines.Add(one);return;}
            for(int i=0;i<addresses.Length&&i<3;i++)lanLines.Add((i==0?"LAN ":"")+addresses[i]);lanLines.Add(at);
        }
        // The fold's INVITE LINK (main.ts:156): the read-only link and its note for a LAN host; a relay room and a guest have
        // no LAN invite (InviteUrl), only the code note.
        private void RoomInviteLink()
        {
            if(Hosting&&inviteLink!=null){RuleField(7780,"INVITE LINK");InviteField(inviteLink);RuleFine(7782,"Share this invite with friends on the same local network.",8,6);}
            else RuleFine(7784,"Friends can enter your code in Join Room. Keep the host’s game open.",8,6);
        }
        // siren-card.ts: the eyes and grin (#f6f3ff) with the teeth line (#050409, 3.4) on a 200 × 120 view box; drawn on
        // a 200 square, so the face sits in its top 60 %.
        internal const string SirenFace="<path d=\"M32 36 L86 48 C74 58 46 58 32 36 Z\" fill=\"#f6f3ff\"/><path d=\"M168 36 L114 48 C126 58 154 58 168 36 Z\" fill=\"#f6f3ff\"/>"
            +"<path d=\"M22 70 C70 86 130 86 178 70 C140 116 60 116 22 70 Z\" fill=\"#f6f3ff\"/>"
            +"<path d=\"M28 70.9 L37 82.7 L46 76.1 L55 93.2 L64 79.8 L73 100.2 L82 82.1 L91 103.8 L100 82.8 L109 103.8 L118 82.1 L127 100.2 L136 79.8 L145 93.2 L154 76.1 L163 82.7 L172 70.9\" fill=\"none\" stroke=\"#050409\" stroke-width=\"3.4\"/>";
        // pveLobbyBody's two fine lines, from the room's rules (a guest's copy of the host's); cached per combination.
        private readonly string[] sirenNotes=new string[2];private int sirenNotesKey=-1;
        private string[] SirenNotes()
        {
            var o=RulesNow();bool skip=o?.skipWaves??prefs.skipWaves,off=o?.skipPlayerIntros??prefs.skipPlayerIntros;int key=(skip?2:0)+(off?1:0);
            if(key!=sirenNotesKey){sirenNotesKey=key;sirenNotes[0]="Ward Zero · 3 waves of minions, then the Siren and his four chips."+(skip?" Starts at the Siren.":"");sirenNotes[1]=(off?"Intros off":"Intros on")+" · personal chip screens · money from kills.";}
            return sirenNotes;
        }
        private float RoomHeadingHeight(){if(!prefs.pve)return 62;float h=126;foreach(var text in SirenNotes())h+=U.Wrap(text,14,lobbyRoomInner,false,4).Count*20+6;return h;}
        private void RoomHeading(Rect r,float e)
        {
            string eyebrow=prefs.pve?"BOSS TEST":Hosting?"HOST CONTROLS":"MATCH RULES";U.Text(new Rect(r.x+4,r.y,r.width-8,16),eyebrow,12,U.Hex(0xc0c1ca,e),TextAnchor.MiddleLeft,bold:true,tracking:.2f);
            U.Text(new Rect(r.x+4,r.y+20,r.width-8,34),prefs.pve?"The Siren":"Match setup",23,U.Hex(0xf4f4f5,e),TextAnchor.MiddleLeft,bold:true,tracking:-.025f);
            if(!prefs.pve)return;
            // .pve-boss-face.small: the Siren's face (siren-card.ts, 200 × 120 view box met into 96 × 52, 6 px margins) with
            // its #b7a8ff88 12 px glow, breathing to 1.04 over 3.6 s; then the two fine lines across the column.
            float breathe=.5f-.5f*Mathf.Cos(Time.unscaledTime*2*Mathf.PI/3.6f),sc=1+.04f*breathe;var c=new Vector2(r.x+4+48,r.y+68+26);
            U.Radial(new Rect(c.x-62,c.y-36,124,72),U.Hex(0xb7a8ff,(.26f+.06f*breathe)*e));
            NativeIcons.DrawMarkup(new Rect(c.x-43.33f*sc,c.y-26*sc,86.67f*sc,86.67f*sc),"siren-face",SirenFace,200,new Color(1,1,1,e));
            float y=r.y+126,w=r.width-8;foreach(var text in SirenNotes())
            {foreach(var l in U.Wrap(text,14,w,false,4)){U.Text(new Rect(r.x+4,y,w,20),l,14,U.Hex(0xa6a7ad,e),TextAnchor.MiddleLeft);y+=20;}y+=6;}
        }
    }
}
