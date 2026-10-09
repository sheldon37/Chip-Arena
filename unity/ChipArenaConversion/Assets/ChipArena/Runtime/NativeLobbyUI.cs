using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The room lobby rebuilt from the source (main.ts screen==='lobby', pve/menu.ts pveLobbyBody, lobby-ui.ts pilotCard,
    // room-options.ts readySummary / readyStatus / roomAdvanced, lobby.css after its override layers): a roster of pilot
    // cards with ready pills and the READY meter beside the room column (the code card, then the host's controls or the
    // rules note), the weapon picker under the roster, and the Advanced Options fold for the detailed rules. The cards
    // show the 2D pilot emblem in place of the live 3D portrait. The host lists its bots from its own settings; guests see
    // the seats the host broadcasts, which do not carry bots, colours or rules yet (each guest card uses its weapon's
    // colour). Drawing only: every setting, message and action is the one the lobby already had.
    public sealed partial class ConversionGame
    {
        private struct LobbyPilot{public string name,tag,weapon;public bool ready,connected,bot;public Color accent,body;public int team;}
        private Vector2 lobbyRosterScroll,lobbyRoomScroll;private float lobbyRosterContent=400,lobbyRoomContent=400;private bool lobbyAdvanced;
        private static readonly string[] TeamNames={"Red","Blue","Green","Yellow","Purple","Orange","Cyan","Pink"};
        private static readonly Color ReadyGreen=U.Hex(0x3ddc84);
        private static string ClassName(string c)=>c=="ranged"?"Ranged":c=="magic"?"Magic":c=="melee"?"Melee":"???";

        // Everyone in the room in seat order: the broadcast seats (the viewer's own card shows its local name and
        // colours), the host's own card if no broadcast has listed it yet, then the host's bots or party bots.
        private List<LobbyPilot> LobbyRoster()
        {
            var list=new List<LobbyPilot>();int me=Hosting?(dedicated?-1:1):networkId;
            foreach(var s in lobbySeats)
            {
                var w=content.weapons.FirstOrDefault(x=>x.id==s.spec);bool you=s.id==me;
                list.Add(new LobbyPilot{name=you?NativeCosmeticRules.Name(prefs.name):s.name,weapon=s.spec,tag=you?"YOU":s.bot?"BOT":s.id==1&&!Hosting?"ROOM OWNER":"PLAYER",bot=s.bot,ready=s.ready,connected=s.connected,team=s.team,
                    accent=you?HexColor(prefs.accentColor,0x70e1d0):HexColor(s.accentColor,w!=null?(int)w.color:0xb8aabb),body=you?HexColor(prefs.bodyColor,0xe5e7eb):HexColor(s.bodyColor,0xe5e7eb)});
            }
            if(Hosting&&!dedicated&&!lobbySeats.Any(s=>s.id==1))list.Insert(0,new LobbyPilot{name=NativeCosmeticRules.Name(prefs.name),weapon=prefs.weapon,tag="YOU",ready=true,connected=true,accent=HexColor(prefs.accentColor,0x70e1d0),body=HexColor(prefs.bodyColor,0xe5e7eb)});
            return list;
        }

        private void LobbyScreen(float age)
        {
            bool pve=prefs.pve;roomGuest=!Hosting;screenContent=Mathf.Max(lobbyRosterContent+90,lobbyRoomContent);
            var l=BeginScreen(pve?"BOSS TEST LOBBY":"PRIVATE ROOM",pve?"Boss test · The Siren":"Match lobby",1370,age,false,actions:false,close:false,lobby:true);
            // Customize and Settings open over the room (their close returns here); saving a new look updates the room.
            float bx=l.Panel.xMax-28;foreach(var (icon,text,screen) in new[]{("settings","Settings",MenuScreen.Settings),("palette","Customize",MenuScreen.Customize)})
            {float w=U.Measure(text,13,true).x+19+18+28;bx-=w;if(MenuButton(new Rect(bx,l.Panel.y+26,w,40),icon,text,null,false,true,l.Enter,13,true))Go(screen);bx-=10;}
            // .lobby-room: the roster column (with the Ready bar at its foot) beside the 300 px room column, which runs the
            // panel's full height (260 px and 20 px gaps below 1100 px).
            var b=l.Body;float W=U.W,gap=W<1100?20:26,side=W<1100?260:300,leftW=b.width-side-gap;
            var roster=LobbyRoster();LobbyRosterColumn(new Rect(b.x,b.y,leftW,b.height-77-12),roster,l.Enter);LobbyRoomColumn(new Rect(b.xMax-side,b.y,side,b.height),l.Enter);
            LobbyBar(new Rect(b.x,b.yMax-77,leftW,77),roster,l.Enter);
        }
        // .lobby-actions at the foot of the roster column: a #4b414c rule, then Ready (280 px, 56 px tall, 18 px) or the
        // host's Start, Leave Room and, for guests, Send Loadout; the ready status fills the rest of the line. A network
        // message (copied, errors) sits above the rule.
        private void LobbyBar(Rect r,List<LobbyPilot> roster,float e)
        {
            if(!string.IsNullOrEmpty(networkStatus))U.Text(new Rect(r.x,r.y-26,r.width,18),Fit(networkStatus,13,r.width,false,0),13,NetworkError?U.Hex(0xff8a7a,e):U.Hex(0xa6a7ad,e),TextAnchor.MiddleLeft);
            U.Fill(new Rect(r.x,r.y,r.width,1),U.Hex(0x4b414c,e));float y=r.y+21,x=r.x;
            if(Hosting)
            {
                float w=Mathf.Min(280,r.width*.4f);if(MenuButton(new Rect(x,y,w,56),"play",prefs.pve?"Start Boss Fight":"Start Match",null,true,server==null,e,18,true))StartMatch();x+=w+10;
            }
            else
            {
                float w=Mathf.Min(280,r.width*.34f);var rb=new Rect(x,y,w,56);
                if(MenuButton(rb,"check",networkReady?"Cancel Ready":"Ready",null,!networkReady,server!=null,e,18,true)){GuestReady(!networkReady);}
                if(networkReady)U.Round(rb,Color.clear,4,U.Hex(0x3ddc84,.67f*e));x+=w+10;
                float sw=U.Measure("Send Loadout",15,true).x+19+18+36;if(MenuButton(new Rect(x,y+4,sw,48),"download","Send Loadout",null,false,server!=null,e,15,true))SendServer(new NativeNetMessage{type="profile",profile=Profile(),token=account?.Token});x+=sw+10;
            }
            float lw=U.Measure("Leave Room",15,true).x+19+18+36;if(MenuButton(new Rect(x,y+4,lw,48),"log-out","Leave Room",null,false,true,e,15,true))LeaveRoom();x+=lw+16;
            int ready=roster.Count(p=>p.ready&&p.connected);ReadyLine(new Rect(x,y,r.xMax-x,56),ready,roster.Count,e);
        }

        // ---- Roster column ---------------------------------------------------------------------------------------------
        private void LobbyRosterColumn(Rect area,List<LobbyPilot> roster,float e)
        {
            var c=GUI.color;GUI.color=new Color(c.r,c.g,c.b,c.a*e);U.BeginDeferred();GUILayout.BeginArea(area);
            lobbyRosterScroll=GUILayout.BeginScrollView(lobbyRosterScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar);
            int count=roster.Count,ready=roster.Count(p=>p.ready&&p.connected),max=prefs.pve?4:8;float t=Time.unscaledTime;
            var head=GUILayoutUtility.GetRect(0,48,GUILayout.ExpandWidth(true));U.Defer(()=>RosterHeading(head,prefs.pve?"Party":"Pilots",count+" / "+max+" joined",ready,count,e));
            // .lobby-roster[data-density]: large (1-2 pilots, 2 columns), medium (3 in 3 columns, 4 in 4), compact (5-8 in 4);
            // narrower columns step down (lobby.css 1100 px rules); short canvases use the max-height:820px portraits.
            int slots=count<2?count+1:count;string density=count<=2?"large":count<=4?"medium":"compact";bool compact=density=="compact",large=density=="large",teams=prefs.teams&&!prefs.pve;
            int cols=large?2:density=="medium"?(count==3?3:4):4;float estimate=area.width-14;if(estimate<640){if(density=="medium")cols=2;else if(compact)cols=3;}if(estimate<420)cols=Mathf.Min(cols,2);
            bool low=U.H<=820;float portrait=large?(low?205:280):density=="medium"?(low?170:220):(low?135:165),g=compact?11:14;int rows=Mathf.CeilToInt(slots/(float)cols);
            // .pilot-card-top wraps: when a card is too narrow for its tag and ready pill on one line, the pill drops to a
            // second line (every card in the roster takes the taller top so the rows stay even).
            float est=(estimate-g*(cols-1))/cols;bool wrap=roster.Any(p=>12+U.Measure("00",14,false,true).x+6+U.Measure(p.tag,10,false,false,.04f).x+10+PillWidth(p)+12>est);float topH=wrap?61:large?40:49;
            // The portraits give way first so the roster, its status line and the weapon picker's first row fit the
            // column (the source's 1080p panel shows them together); they never go below about 60% of their size.
            float rest=CardHeight(topH,compact,0)+(teams?43:0),fit=(area.height-48-100-(rows-1)*g)/rows-rest;portrait=Mathf.Clamp(fit,portrait*.6f,portrait);
            float cardH=CardHeight(topH,compact,portrait),slotH=cardH+(teams?43:0);
            var grid=GUILayoutUtility.GetRect(0,rows*slotH+(rows-1)*g,GUILayout.ExpandWidth(true));
            U.Defer(()=>
            {
                if(Event.current.type!=EventType.Repaint)return;float cw=(grid.width-g*(cols-1))/cols;
                for(int i=0;i<slots;i++)
                {
                    var r=new Rect(grid.x+(i%cols)*(cw+g),grid.y+(i/cols)*(slotH+g),cw,cardH);
                    if(i<count){LobbyCard(r,roster[i],i,portrait,topH,wrap,compact,e,t);if(teams)TeamTag(new Rect(r.x+2,r.yMax+9,r.width-4,34),roster[i],e);}else LobbyEmpty(new Rect(r.x,r.y,r.width,slotH),e);
                }
            });
            ClassRow("Your class",ClassCardStyle.Lobby);WeaponCards("Your weapon");
            GUI.enabled=true;GUILayout.Space(4);if(Event.current.type==EventType.Repaint)lobbyRosterContent=GUILayoutUtility.GetLastRect().yMax;
            GUILayout.EndScrollView();GUILayout.EndArea();GUI.color=c;U.EndDeferred(area,lobbyRosterScroll);
        }
        // .pilot-card in .lobby-roster: top row (49 px, 40 px when large, 61 px wrapped), the portrait, the name (20 px; 17 px compact,
        // line-height 1.2), a 7 px gap, the class line (24 / 21 px) with its 8 px underline pad, and 17 px (14 px) below.
        private static float CardHeight(float top,bool compact,float portrait)=>top+portrait+(compact?20.4f:24)+7+(compact?24:28)+8+(compact?14:17);
        // .section-title: "Pilots" (18 px), "2 / 8 joined" and the READY meter at the right (room-options.ts readySummary).
        private static void RosterHeading(Rect r,string title,string joined,int ready,int total,float e)
        {
            float y=r.y+4,h=36;U.Text(new Rect(r.x,y,200,h),title,18,U.Hex(0xf4f4f5,e),TextAnchor.MiddleLeft,bold:true,tracking:-.02f);
            float tx=r.x+U.Measure(title,18,true,false,-.02f).x+14;U.Text(new Rect(tx,y+2,240,h),joined,14,U.Hex(0xb8aabb,e),TextAnchor.MiddleLeft);
            bool all=total>0&&ready>=total;string n=ready+"/"+total;float nw=U.Measure(n,22,true,false,.02f).x,lw=U.Measure("READY",13,true,false,.16f).x,w=16+nw+8+lw+16;
            var pill=new Rect(r.xMax-w,y,w,h);if(all)U.Glow(pill,U.Hex(0x3ddc84,.25f*e),20);
            U.Round(pill,all?U.Hex(0x11301f,e):U.Hex(0x2a201c,e),Mathf.Min(16,h/2),all?U.Hex(0x3ddc84,e):U.Hex(0xf4b878,.4f*e));
            U.Text(new Rect(pill.x+16,y,nw+4,h),n,22,U.Hex(0xffffff,e),TextAnchor.MiddleLeft,bold:true,tracking:.02f);
            U.Text(new Rect(pill.x+16+nw+8,y+2,lw+4,h),"READY",13,all?U.Hex(0x8ff0b6,e):U.Hex(0xf4c8a0,e),TextAnchor.MiddleLeft,bold:true,tracking:.16f);
        }
        // `result`: the PvE results variant (pve.css .pve-result .pilot-card) without the ready pill or class line, the name
        // at 18 px.
        private void LobbyCard(Rect r,LobbyPilot p,int index,float portrait,float topH,bool wrap,bool compact,float e,float t,bool result=false)
        {
            var weapon=p.weapon!=null?content.weapons.FirstOrDefault(w=>w.id==p.weapon):null;Color style=weapon!=null?U.Hex((int)weapon.color):U.Hex(0xb8aabb);bool ready=p.ready&&p.connected;float a=e*(p.connected?1:.55f);
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
            Color ink=ready?U.Hex(0x062413,a):!p.connected?U.Hex(0xa6a7ad,a):U.Hex(0xf4c8a0,a);
            NativeIcons.Draw(new Rect(pill.x+12,pill.center.y-7,14,14),icon,ink);U.Text(new Rect(pill.x+12+14+6,pill.y,tw+4,h),text,12,ink,TextAnchor.MiddleLeft,bold:true,tracking:.12f);
        }
        // .pilot-class-fx: a dim 2 px line in the weapon colour with a light that sweeps across it (lobby-metal, 3.6 s);
        // the source's per-weapon variants: a dashed pulse (machine gun, L33T), a short scanning segment (rifle), a
        // flickering current (lightning, Emperor) and a breathing tide (water, darkness, shield).
        private static void ClassUnderline(Rect r,string spec,Color style,float a,float t)
        {
            U.Fill(r,U.A(style,.2f*a));U.Glow(r,U.A(style,.18f*a),6);
            if(spec=="machinegun"||spec=="leet"){float pulse=.3f+.6f*(.5f+.5f*Mathf.Sin(t*Mathf.PI*2/1.8f));for(float x=r.x;x<r.xMax;x+=10)U.Fill(new Rect(x,r.y,Mathf.Min(5,r.xMax-x),r.height),U.A(style,pulse*a));return;}
            if(spec=="rifle"){float k=.5f-.5f*Mathf.Cos(t*Mathf.PI*2/3);float w=r.width*.25f;U.Fill(new Rect(r.x+k*(r.width-w),r.y,w,r.height),U.A(style,.9f*a));return;}
            if(spec=="lightning"||spec=="emperor"){float ph=Mathf.Repeat(t/2.8f,1);float o=ph>.70f&&ph<.82f?.9f:ph>=.82f&&ph<.86f?.45f:.3f;U.Fill(new Rect(r.x,r.y-2,r.width,r.height+4),U.A(style,o*.6f*a));U.Fill(r,U.A(Color.white,o*.5f*a));return;}
            if(spec=="water"||spec=="darkness"||spec=="shield"){float period=spec=="water"?3:spec=="shield"?3.8f:4;float k=.5f-.5f*Mathf.Cos(t*Mathf.PI*2/period);float w=r.width*(.35f+.65f*k);U.Fill(new Rect(r.center.x-w/2,r.y,w,r.height),U.A(style,(.4f+.6f*k)*a));return;}
            float dur=spec=="pistol"?2.8f:spec=="daggers"?1.8f:spec=="greatsword"?5f:3.6f,f=Mathf.Repeat(t/dur,1);float pos=f<.9f?Mathf.Lerp(-1,1,f/.9f):1,alpha=f<.2f?f/.2f:f<.8f?1:f<.9f?(.9f-f)/.1f:0;
            float sw=r.width*.45f,sx=r.x+(pos+1)/2*(r.width+sw)-sw;float x0=Mathf.Max(r.x,sx),x1=Mathf.Min(r.xMax,sx+sw);
            if(x1>x0){float half=(x0+x1)/2;U.HGradient(new Rect(x0,r.y,half-x0,r.height),Color.clear,U.A(Color.Lerp(style,Color.white,.4f),alpha*a));U.HGradient(new Rect(half,r.y,x1-half,r.height),U.A(Color.Lerp(style,Color.white,.4f),alpha*a),Color.clear);}
        }
        // .team-assignment under a card in a teams room: the team dot and name (chosen by the host for now).
        private static void TeamTag(Rect r,LobbyPilot p,float a)
        {
            bool known=p.team>=0;Color team=known?TeamColor(p.team):U.Hex(0x8f909a);
            U.Disc(new Rect(r.x,r.center.y-4.5f,9,9),U.A(team,a));U.Glow(new Rect(r.x,r.center.y-4.5f,9,9),U.A(team,.35f*a),6);U.Text(new Rect(r.x+15,r.y,80,r.height),"Team",12,U.Hex(0xc7c1ce,a),TextAnchor.MiddleLeft);
            float w=Mathf.Min(120,r.width*.55f);var box=new Rect(r.xMax-w,r.y,w,r.height);U.Round(box,U.Hex(0x111216,.85f*a),4,U.A(U.Mix(U.Hex(0x333333),team,.45f),a));
            U.Text(new Rect(box.x+10,box.y,box.width-20,box.height),known?TeamNames[TeamIndex(p.team)]:"Auto",12,U.Hex(0xe6e7ec,a),TextAnchor.MiddleLeft);
        }
        // .lobby-empty: a dashed card holding the next seat while the room has fewer than two pilots.
        private void LobbyEmpty(Rect r,float e)
        {
            U.Round(r,U.Hex(0x1b1921,.5f*e),11);DashedRound(r,U.Hex(0x685766,e),11);
            float cy=r.center.y-36;NativeIcons.Draw(new Rect(r.center.x-17,cy,34,34),"users",U.Hex(0xb9acbd,e));
            U.Text(new Rect(r.x+25,cy+46,r.width-50,24),"Waiting for a pilot",17,U.Hex(0xe6dfe8,e),TextAnchor.MiddleCenter,bold:true);
            var lines=U.Wrap(Hosting?"Share your room code or add a bot.":"Share the room code with a friend.",14,r.width-50,false,3);float y=cy+46+30;foreach(var l in lines){U.Text(new Rect(r.x+25,y,r.width-50,21),l,14,U.Hex(0xb9acbd,e),TextAnchor.MiddleCenter);y+=21;}
        }
        // A 1 px dashed outline (6 px dashes, 4 px gaps) along the straight edges of a rounded rect.
        private static void DashedRound(Rect r,Color c,float radius)
        {
            U.Dashed(new Rect(r.x+radius,r.y,r.width-2*radius,1),c);U.Dashed(new Rect(r.x+radius,r.yMax-1,r.width-2*radius,1),c);
            U.Dashed(new Rect(r.x,r.y+radius,1,r.height-2*radius),c,false);U.Dashed(new Rect(r.xMax-1,r.y+radius,1,r.height-2*radius),c,false);
            U.Round(r,Color.clear,radius,U.A(c,.35f));
        }
        // room-options.ts readyStatus under the roster: who the room is waiting for. The host starts the match here (the
        // source starts it on its own once everyone is ready); a guest whose weapon differs from the one the host has is
        // reminded to send the loadout.
        private void ReadyLine(Rect r,int ready,int total,float e)
        {
            int waiting=Mathf.Max(0,total-ready);string lead,rest;Color tone;
            var mine=!Hosting?lobbySeats.FirstOrDefault(s=>s.id==networkId):null;string want=content.weapons.FirstOrDefault(w=>w.id==prefs.weapon)?.name??prefs.weapon;
            if(total==0){lead=Hosting?"Waiting for pilots":"Joining the room…";rest=Hosting?"· Share your room code.":"";tone=U.Hex(0xf4c8a0);}
            else if(mine!=null&&mine.spec!=prefs.weapon){lead="Send Loadout";rest="so the host gives you "+want+".";tone=U.Hex(0xf4c8a0);}
            else if(Hosting&&!prefs.pve&&total<2){lead="Add a bot";rest="or wait for a pilot to join.";tone=U.Hex(0xf4c8a0);}
            else if(waiting>0){lead="Waiting for "+waiting+" pilot"+(waiting==1?"":"s");rest="· "+(Hosting?"Start when every pilot is ready.":"The host starts the match.");tone=U.Hex(0xf4c8a0);}
            else{lead="Everyone is ready.";rest=Hosting?(prefs.pve?"Start the boss fight when you are.":"Start the match when you are."):"Waiting for the host to start.";tone=U.Hex(0x8ff0b6);}
            // 14 px, line-height 1.5: the lead in its tone, then the rest; both wrap within the space beside the buttons.
            if(r.width<60)return;var lines=U.Wrap(lead+"\u0001 "+rest,14,r.width,true,3);float lh=21,y=r.center.y-lines.Count*lh/2;
            foreach(var line in lines)
            {
                int cut=line.IndexOf('\u0001');if(cut<0&&line.StartsWith(lead)==false){U.Text(new Rect(r.x,y,r.width,lh),line,14,U.Hex(0xa6a7ad,e),TextAnchor.MiddleLeft);y+=lh;continue;}
                string head=cut>=0?line.Substring(0,cut):line,tail=cut>=0?line.Substring(cut+1):"";float hw=U.Measure(head,14,true).x;
                U.Text(new Rect(r.x,y,hw+4,lh),head,14,U.A(tone,e),TextAnchor.MiddleLeft,bold:true);if(tail.Length>0)U.Text(new Rect(r.x+hw,y,r.width-hw,lh),tail,14,U.Hex(0xa6a7ad,e),TextAnchor.MiddleLeft);y+=lh;
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
            var frame=open?new Rect(r.x,r.y,r.width,r.height+extra+12):r;if(open)U.UnderRound(frame,U.Hex(0x13141a,.4f*GUI.color.a),9);
            U.Defer(()=>
            {
                if(Event.current.type==EventType.Repaint)
                {
                    float h=U.HoverAmount(7390,r);
                    U.Round(frame,open?Color.clear:U.Hex(0x13141a,.4f),9,U.Hex(0x4c434e));if(h>0)U.Round(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),new Color(1,1,1,.03f*h),9);if(open)U.Fill(new Rect(r.x+1,r.yMax-1,r.width-2,1),U.Hex(0x4c434e));
                    var c=new Vector2(r.x+19,r.y+23);U.Push(c,Vector2.zero,1,open?45:-45);U.Fill(new Rect(c.x+2,c.y-4,2,8),U.Hex(0xefb587));U.Fill(new Rect(c.x-4,c.y+2,8,2),U.Hex(0xefb587));U.Pop();
                    U.Text(new Rect(r.x+33,r.y+12,r.width-45,22),"Advanced Options",15,U.Hex(0xded5e0),TextAnchor.MiddleLeft,bold:true);
                    var lines=U.Wrap(note,12,r.width-48,false,2);float y=r.y+35;foreach(var l in lines){U.Text(new Rect(r.x+33,y,r.width-45,16),l,12,U.Hex(0xb3a8b6),TextAnchor.MiddleLeft);y+=16;}
                }
                if(U.Button(r)){lobbyAdvanced=!lobbyAdvanced;Click();}
            });
            // .room-advanced>.match-options: the open content sits 14 px inside the frame on both sides.
            foldOpen=open;if(open){GUILayout.Space(10);GUILayout.BeginHorizontal();GUILayout.Space(18);GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(120,lobbyRoomInner-28)));}
            return open;
        }
        private float advancedTop;private bool foldOpen;
        // Called after the open fold's content: closes its inset and keeps its height for the next paint's frame.
        private void AdvancedEnd()
        {
            if(!foldOpen)return;foldOpen=false;GUILayout.EndVertical();GUILayout.Space(10);GUILayout.EndHorizontal();
            if(Event.current.type==EventType.Repaint)lobbyAdvancedHeight=Mathf.Max(0,GUILayoutUtility.GetLastRect().yMax-advancedTop);
        }

        // .lobby-room-settings>.match-options: the essentials box (10 px corners, the border colour, 125deg #23242ae6 →
        // #1a1b20e6, 12 / 16 / 6 px padding) with Match mode and ARENA as the source's selects and the arena's description.
        private float lobbyRoomInner=254;
        private void LobbyEssentials()
        {
            var maps=content.maps??new ContentMap[0];int mi=Mathf.Max(0,Array.FindIndex(maps,m=>m.id==prefs.map));var map=maps.Length>0?maps[mi]:null;
            var desc=U.Wrap(map?.tag??"",14,lobbyRoomInner-32,false,3);float h=12+21+6+44+14+16+8+44+8+desc.Count*20+12;
            GUILayout.Space(12);var r=GUILayoutUtility.GetRect(0,h,GUILayout.ExpandWidth(true));GUILayout.Space(6);
            U.Defer(()=>
            {
                bool paint=Event.current.type==EventType.Repaint;var b=new Rect(r.x+4,r.y,r.width-8,r.height);float x=b.x+16,iw=b.width-32,y=b.y+12;
                if(paint){U.RoundGradient(b,U.Hex(0x23242a,.9f),U.Hex(0x1a1b20,.9f),10,U.Hex(0x45464e),1,true);U.Text(new Rect(x,y,iw,21),"Match mode",14,U.Hex(0xe6e6ea),TextAnchor.MiddleLeft);}
                y+=27;prefs.teams=SelectField(7410,new Rect(x,y,iw,44),new[]{"Free-for-all","Teams"},prefs.teams?1:0)==1;y+=44+14;
                if(paint)U.Text(new Rect(x,y,iw,16),"ARENA",12,U.Hex(0xc0c1ca),TextAnchor.MiddleLeft,bold:true);y+=24;
                if(maps.Length>0){int pick=SelectField(7420,new Rect(x,y,iw,44),maps.Select(m=>m.name).ToArray(),mi);if(pick!=mi)prefs.map=maps[pick].id;}y+=44+8;
                if(paint)foreach(var l in desc){U.Text(new Rect(x,y,iw,20),l,14,U.Hex(0xa6a7ad),TextAnchor.MiddleLeft);y+=20;}
            });
        }
        // The source's select (#222329, #484952 edge warming to #ff784b on hover, 4 px corners) as a stepper: the choice,
        // then ‹ › at the right; a click on the choice steps forward. Returns the new index.
        private const string ChevronLeft="<path d=\"M15 6 L9 12 L15 18\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>";
        private const string ChevronRight="<path d=\"M9 6 L15 12 L9 18\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>";
        private int SelectField(int id,Rect r,string[] labels,int index)
        {
            int n=labels.Length;if(n==0)return index;index=Mathf.Clamp(index,0,n-1);
            var prev=new Rect(r.xMax-74,r.y+(r.height-32)/2,32,32);var next=new Rect(r.xMax-38,prev.y,32,32);var name=new Rect(r.x,r.y,r.width-78,r.height);
            float hp=U.HoverAmount(id+1,prev),hn=U.HoverAmount(id+2,next),hf=U.HoverAmount(id,r);
            if(Event.current.type==EventType.Repaint)
            {
                U.Round(r,U.Mix(U.Hex(0x222329),U.Hex(0x26272d),hf),4,U.Mix(U.Hex(0x484952),U.Hex(0xff784b),hf));
                U.Text(new Rect(r.x+14,r.y,name.width-16,r.height),Fit(labels[index],15,name.width-18,false,0),15,U.Hex(0xf5f5f7),TextAnchor.MiddleLeft);
                U.Fill(new Rect(r.xMax-79,r.y+10,1,r.height-20),U.Hex(0x484952));
                if(hp>0)U.Round(prev,new Color(1,1,1,.07f*hp),4);if(hn>0)U.Round(next,new Color(1,1,1,.07f*hn),4);
                NativeIcons.DrawMarkup(new Rect(prev.x+7,prev.y+7,18,18),"chevron-left",ChevronLeft,24,U.Mix(U.Hex(0xb9bac2),Color.white,hp));
                NativeIcons.DrawMarkup(new Rect(next.x+7,next.y+7,18,18),"chevron-right",ChevronRight,24,U.Mix(U.Hex(0xb9bac2),Color.white,hn));
            }
            int step=U.Button(prev)?-1:U.Button(next)||U.Button(name)?1:0;if(step!=0){index=(index+step+n)%n;Click();}return index;
        }

        // #connection .top-meta in the lobby's top bar (main.ts drawHud; style.css .top-meta,.light): a 6 px light
        // (#59dd83, #ef9b7a when disconnected), the connection and " / " the room code, in 12 px mono #c3c4ce on
        // #18191ee8 with a #45464f edge and 10 / 14 px padding, right-aligned at `right`. The source appends
        // " · N MS"; Unity measures no round trip yet (A27), so the reading is left off.
        private void ConnectionPill(float right,float top)
        {
            // As main.ts: DISCONNECTED once the link closes, CONNECTION STALLED after 3 s without a message from the host.
            bool lost=!Hosting&&(server==null||server.Closed),stalled=!Hosting&&!lost&&(DateTime.UtcNow-server.LastReceived).TotalSeconds>3;string code=Hosting?roomCode:joinCode;
            string text=(lost?"DISCONNECTED":stalled?"CONNECTION STALLED":listener!=null?"LAN · HOST":Hosting?"INTERNET RELAY · HOST":internetRooms?"INTERNET RELAY":"LOCAL NETWORK")+(string.IsNullOrEmpty(code)?"":" / "+code);
            float w=14+6+8+U.Measure(text,12,false,true).x+14;var r=new Rect(right-w,top,w,36);
            U.Fill(r,U.Hex(0x45464f));U.Fill(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),U.Hex(0x18191e,.91f));
            U.Round(new Rect(r.x+14,r.center.y-3,6,6),lost?U.Hex(0xef9b7a):U.Hex(0x59dd83),3);
            U.Text(new Rect(r.x+28,r.y,w-42,r.height),text,12,U.Hex(0xc3c4ce),TextAnchor.MiddleLeft,mono:true);
        }

        // ---- Room column -----------------------------------------------------------------------------------------------
        // .lobby-room-settings: the room code card, then HOST CONTROLS / MATCH RULES (or BOSS TEST · The Siren) and,
        // for the host, the mode and essential rules with the detailed ones under Advanced Options.
        private void LobbyRoomColumn(Rect r,float e)
        {
            // The code card scrolls with the rules, so short canvases keep the whole column reachable.
            U.Round(r,U.Hex(0x11131a,.53f*e),12,U.Hex(0x514353,e));var area=new Rect(r.x+16,r.y+18,r.width-30,r.height-30);
            var c=GUI.color;GUI.color=new Color(c.r,c.g,c.b,c.a*e);U.BeginDeferred();GUILayout.BeginArea(area);
            lobbyRoomScroll=GUILayout.BeginScrollView(lobbyRoomScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar);
            string label=RoomCodeLabel();bool two=U.Measure(label,10,false,false,.16f).x>area.width-12-32;
            var invite=GUILayoutUtility.GetRect(0,two?146:132,GUILayout.ExpandWidth(true));string code=Hosting?roomCode:joinCode;U.Defer(()=>RoomInvite(new Rect(invite.x+4,invite.y+2,invite.width-8,invite.height-2),code,label,two,e));GUILayout.Space(16);
            if(listener!=null)Fine("LAN address · "+string.Join(", ",LanAddresses)+" · port "+listener.Port);
            var head=GUILayoutUtility.GetRect(0,RoomHeadingHeight(),GUILayout.ExpandWidth(true));if(Event.current.type==EventType.Repaint)lobbyRoomInner=head.width-8;U.Defer(()=>RoomHeading(head,e));
            if(Hosting)
            {
                // Versus or Boss Test as a select, so the narrow column never wraps the choice.
                Field("Mode");var mode=GUILayoutUtility.GetRect(0,44,GUILayout.ExpandWidth(true));
                U.Defer(()=>{bool pve=SelectField(7430,new Rect(mode.x+4,mode.y,mode.width-8,mode.height),new[]{"Versus","Boss Test"},prefs.pve?1:0)==1;if(pve!=prefs.pve){prefs.pve=pve;prefs.bots=Mathf.Clamp(prefs.bots,0,pve?3:7);}});
                choiceColumns=2;if(prefs.pve)BossBody(true);else MatchRulesBody(true);choiceColumns=3;
            }
            else{GUILayout.BeginHorizontal();GUILayout.Label(prefs.pve?"The host sets the party bots and where the run starts. Ready up when your loadout is set.":"The host sets the mode, arena and rules. Ready up when your loadout is set.",fine,GUILayout.Width(lobbyRoomInner));GUILayout.EndHorizontal();}
            GUI.enabled=true;GUILayout.Space(4);if(Event.current.type==EventType.Repaint)lobbyRoomContent=GUILayoutUtility.GetLastRect().yMax;
            GUILayout.EndScrollView();GUILayout.EndArea();GUI.color=c;U.EndDeferred(area,lobbyRoomScroll);
        }
        // .lobby-room .room-invite in the settings column: ROOM CODE, the code (24 px mono, .09em, #ff996f) and Copy Code,
        // on the warm 110deg #392b26 → #1a1b23 card with its #6e554c edge.
        private string RoomCodeLabel()=>!Hosting?"ROOM CODE":listener!=null?"ROOM CODE · LOCAL NETWORK · PORT "+listener.Port:"ROOM CODE · INTERNET RELAY";
        private void RoomInvite(Rect r,string code,string label,bool two,float e)
        {
            U.RoundGradient(r,U.Hex(0x392b26,e),U.Hex(0x1a1b23,e),10,U.Hex(0x6e554c,e),1,true);
            // The label breaks after ROOM CODE when the column is too narrow for one line.
            if(two){int cut=label.IndexOf(" · ",StringComparison.Ordinal);U.Text(new Rect(r.x+16,r.y+16,r.width-32,14),label.Substring(0,cut),10,U.Hex(0x8da7ae,e),TextAnchor.MiddleLeft,tracking:.16f);U.Text(new Rect(r.x+16,r.y+30,r.width-32,14),Fit(label.Substring(cut+3),10,r.width-32,false,.16f),10,U.Hex(0x8da7ae,e),TextAnchor.MiddleLeft,tracking:.16f);}
            else U.Text(new Rect(r.x+16,r.y+16,r.width-32,14),label,10,U.Hex(0x8da7ae,e),TextAnchor.MiddleLeft,tracking:.16f);
            bool has=!string.IsNullOrEmpty(code);U.Text(new Rect(r.x+16,r.y+(two?48:34),r.width-32,32),has?code:"—",24,U.Hex(0xff996f,e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.09f);
            if(MenuButton(new Rect(r.x+16,r.yMax-16-40,r.width-32,40),"copy","Copy Code",null,true,has,e,14,true)){GUIUtility.systemCopyBuffer=code;Notice("Room code copied.");}
        }
        // siren-card.ts: the eyes and grin (#f6f3ff) with the teeth line (#050409, 3.4) on a 200 × 120 view box; drawn on
        // a 200 square, so the face sits in its top 60 %.
        private const string SirenFace="<path d=\"M32 36 L86 48 C74 58 46 58 32 36 Z\" fill=\"#f6f3ff\"/><path d=\"M168 36 L114 48 C126 58 154 58 168 36 Z\" fill=\"#f6f3ff\"/>"
            +"<path d=\"M22 70 C70 86 130 86 178 70 C140 116 60 116 22 70 Z\" fill=\"#f6f3ff\"/>"
            +"<path d=\"M28 70.9 L37 82.7 L46 76.1 L55 93.2 L64 79.8 L73 100.2 L82 82.1 L91 103.8 L100 82.8 L109 103.8 L118 82.1 L127 100.2 L136 79.8 L145 93.2 L154 76.1 L163 82.7 L172 70.9\" fill=\"none\" stroke=\"#050409\" stroke-width=\"3.4\"/>";
        private string[] SirenNotes()=>new[]{"Ward Zero · 3 waves of minions, then the Siren and his four chips."+(prefs.skipWaves?" Starts at the Siren.":""),"Intros on · personal chip screens · money from kills."};
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
