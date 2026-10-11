using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The source menus rebuilt natively: the home screen (main.ts drawMenu with style.css / lobby.css / pve.css, the live
    // look after every override layer) over the orbiting map, its panel screens (Play vs Bots, Create / Join Room, the room
    // lobby, Boss Test, Customize, Settings, Account, DEV Menu, Catalogue) and the in-match pause menu. Chrome, navigation
    // and actions are kit-drawn; each panel body is the existing GUILayout form, inside its own area and scroll view, so
    // every setting keeps its validation and save path. Beyond the source: animated title letters and a light sweep,
    // drifting voxel motes, a sheen on the primary action, keys 1-4 for the four numbered entries, and a pilot card.
    public sealed partial class ConversionGame
    {
        private enum MenuScreen{Home,Play,Create,Join,Lobby,Boss,Customize,Settings,Account,Dev,Catalogue}
        private enum PauseScreen{Menu,Settings,Sandbox}
        private MenuScreen menuScreen;private PauseScreen pauseScreen;private float menuScreenAt=-1,menuSeen=-1,screenContent=400;private Vector2 screenScroll;
        private bool updateOpen,menuFromMatch,menuBlockedNow,roomGuest,playSandbox;private MenuScreen afterMatch=MenuScreen.Home;private Rect updateCard;private GUIStyle fieldLabel,fine,codeField;
        private void Click()=>nativeAudio?.UiCue("ui.click","ui",.4f);
        // CSS panel-in (.26 s cubic-bezier(.2,.8,.2,1)) progress for an element entering `delay` seconds into the screen.
        private static float In(float age,float delay,float duration)=>M.Bezier(.2f,.8f,.2f,1,M.Clamp01((age-delay)/duration));
        private void Go(MenuScreen screen)
        {
            // Leaving Settings keeps what was changed (applied and saved); leaving Create / Join abandons a pending connection.
            if(menuScreen==MenuScreen.Settings&&screen!=MenuScreen.Settings){ApplySettings();Save();}
            if((menuScreen==MenuScreen.Create||menuScreen==MenuScreen.Join)&&screen!=MenuScreen.Lobby&&!InRoom&&(connecting!=null||relayConnecting!=null||server!=null||listingRooms)){CloseNetwork();networkStatus="Connection cancelled.";}
            if(screen!=MenuScreen.Home)updateOpen=false;playSandbox=false;
            if(screen==MenuScreen.Play){prefs.pve=false;prefs.bots=Mathf.Clamp(prefs.bots,1,7);sandboxWanted=false;}
            if(screen==MenuScreen.Boss){prefs.pve=true;prefs.bots=Mathf.Clamp(prefs.bots,0,3);sandboxWanted=false;}
            // A room opens at the top of both lobby columns; Customize and Settings return to where the lobby was.
            if(screen==MenuScreen.Lobby&&menuScreen!=MenuScreen.Customize&&menuScreen!=MenuScreen.Settings){lobbyRosterScroll=lobbyRoomScroll=Vector2.zero;lobbyRosterContent=lobbyRoomContent=400;}
            // Entering the Locker takes the look it can revert to (Preview Intro's way back stays in the same session).
            if(screen==MenuScreen.Customize&&menuScreen!=MenuScreen.Customize)LockerEnter();
            menuScreen=screen;menuScreenAt=Time.unscaledTime;screenScroll=Vector2.zero;screenContent=400;GUIUtility.keyboardControl=0;
        }
        // Escape on the main menu: close the update card, else return to the home screen (the source close X); the room
        // lobby stays until Leave Room. The Locker asks before dropping unsaved changes (LockerBack).
        private void MenuBack(){if(updateOpen&&menuScreen==MenuScreen.Home){updateOpen=false;return;}if(menuScreen==MenuScreen.Customize){LockerBack();return;}if(menuScreen!=MenuScreen.Home&&menuScreen!=MenuScreen.Lobby)Go(MenuScreen.Home);}
        private void MenuStyles()
        {
            if(fieldLabel!=null)return;fieldLabel=new GUIStyle(small){fontSize=12,fontStyle=FontStyle.Bold,wordWrap=true};fieldLabel.normal.textColor=U.Hex(0xc0c1ca);fieldLabel.margin=new RectOffset(4,4,18,6);
            fine=new GUIStyle(small){fontSize=13,wordWrap=true};fine.normal.textColor=U.Hex(0xa6a7ad);
            codeField=new GUIStyle(GUI.skin.textField){fontSize=26,alignment=TextAnchor.MiddleCenter,font=U.MonoFont};codeField.padding=new RectOffset(12,12,10,10);
        }
        private void Field(string text)=>GUILayout.Label(text.ToUpperInvariant(),fieldLabel);
        private void Fine(string text)=>GUILayout.Label(text,fine);
        // A row of mutually exclusive choices drawn as toggle buttons (the skin's on-state is the source's active option).
        private int ChoiceRow(string[] labels,int selected)
        {GUILayout.BeginHorizontal();for(int i=0;i<labels.Length;i++)if(GUILayout.Toggle(i==selected,(i==selected?"✓ ":"")+labels[i],button)&&i!=selected){selected=i;Click();}GUILayout.EndHorizontal();return selected;}

        // ---- Routing -------------------------------------------------------------------------------------------------
        private void HomeMenu()
        {
            MenuStyles();float now=Time.unscaledTime;if(now-menuSeen>.25f)menuScreenAt=now;menuSeen=now;
            if(menuFromMatch){menuFromMatch=false;Go(afterMatch);afterMatch=MenuScreen.Home;}
            // In a room the lobby is home (Customize and Settings stay open over it); a room lost without Leave Room returns
            // to Join (guest) or Create (host), where the reason is shown.
            if(InRoom&&menuScreen!=MenuScreen.Lobby&&menuScreen!=MenuScreen.Customize&&menuScreen!=MenuScreen.Settings)Go(MenuScreen.Lobby);
            else if(!InRoom&&menuScreen==MenuScreen.Lobby)Go(roomGuest?MenuScreen.Join:MenuScreen.Create);
            // The open Connection report (NativeConnectionReport.cs) blocks everything under its backdrop, as its ::backdrop takes the pointer.
            menuBlockedNow=reportOpen||menuScreen==MenuScreen.Home&&updateOpen&&updateCard.width>0&&U.Hover(updateCard);
            float age=now-menuScreenAt;bool home=menuScreen==MenuScreen.Home;
            MenuShade(1);MenuMotes(home?1:.5f);TopBar(home);Footer();
            switch(menuScreen)
            {
                case MenuScreen.Home:HomeScreen(age);break;
                case MenuScreen.Play:PlayScreen(age);break;
                case MenuScreen.Create:CreateScreen(age);break;
                case MenuScreen.Join:JoinScreen(age);break;
                case MenuScreen.Lobby:LobbyScreen(age);break;
                case MenuScreen.Boss:BossScreen(age);break;
                case MenuScreen.Customize:CustomizeScreen(age);break;
                case MenuScreen.Settings:SettingsScreen(age,()=>Go(MenuScreen.Home));break;
                case MenuScreen.Account:AccountScreen(age);break;
                case MenuScreen.Dev:DevScreen(age);break;
                case MenuScreen.Catalogue:CatalogueScreen(age);break;
            }
            if(home)UpdateDropdown();
        }

        // ---- Backdrop ------------------------------------------------------------------------------------------------
        // .home-shade: linear-gradient(90deg,#101114fa 0%,#101114e8 34%,#10111422 78%) over the orbiting map, with a soft
        // top and bottom vignette so the bar and footer read at every aspect ratio.
        private void MenuShade(float strength)
        {
            float W=U.W,H=U.H;Color ink=U.Hex(0x101114);
            U.Fill(new Rect(0,0,W*.34f,H),U.A(ink,.91f*strength));U.HGradient(new Rect(0,0,W*.34f,H),U.A(ink,.78f*strength),Color.clear);
            U.Fill(new Rect(W*.34f,0,W*.66f,H),U.A(ink,.13f*strength));U.HGradient(new Rect(W*.34f,0,W*.44f,H),U.A(ink,.897f*strength),Color.clear);
            U.Fade(new Rect(0,0,W,150),U.Hex(0x0b0c0f,.55f*strength));U.Fade(new Rect(0,H-200,W,200),U.Hex(0x0b0c0f,.6f*strength),false);
        }
        // Voxel motes drifting up through the shaded side: deterministic per index, so they never pop between frames.
        private static void MenuMotes(float alpha)
        {
            float W=U.W,H=U.H,t=Time.unscaledTime;
            for(int i=0;i<26;i++)
            {
                float h1=Mathf.Repeat(Mathf.Sin(i*12.9898f)*43758.5453f,1),h2=Mathf.Repeat(Mathf.Sin(i*78.233f)*24634.6345f,1),h3=Mathf.Repeat(Mathf.Sin(i*39.425f)*11753.13f,1);
                float speed=7+h2*16,size=2+Mathf.Floor(h3*3),span=H+60,y=H+20-Mathf.Repeat(t*speed+h1*span,span),x=h1*W*.62f+Mathf.Sin(t*.35f+i)*10;
                float life=Mathf.Clamp01((H+20-y)/160)*Mathf.Clamp01(y/180),a=(.05f+.13f*h3)*life*alpha;
                U.Fill(new Rect(x,y,size,size),i%3==0?U.Hex(0xff8a5b,a*1.3f):new Color(1,1,1,a));
            }
        }

        // ---- Top bar and footer --------------------------------------------------------------------------------------
        // .topbar (24 px from the top, 30 px sides): the brandmark, CHIP ARENA and the PROTOTYPE badge; on the home screen
        // the Update Game pill at the right with its status line beneath (main.ts drawUpdateControl).
        private void TopBar(bool home)
        {
            float W=U.W,x=30,cy=24+19;
            Brandmark(new Rect(x,cy-14.5f,29,29));float tx=x+29+12;
            U.Text(new Rect(tx,cy-11,260,22),"CHIP ARENA",16,U.Hex(0xf4f4f5),TextAnchor.MiddleLeft,bold:true,tracking:.08f);
            float bx=tx+U.Measure("CHIP ARENA",16,true,false,.08f).x+18,bw=U.Measure("PROTOTYPE",11,false,true,.08f).x+14;var badge=new Rect(bx,cy-10.5f,bw,21);
            U.Round(badge,U.Hex(0x18191e,.91f),4,U.Hex(0x45464f));U.Text(badge,"PROTOTYPE",11,U.Hex(0xc3c4ce),TextAnchor.MiddleCenter,mono:true,tracking:.08f);
            if(home&&Updater!=null)UpdateControl(W-30,24);
            if(menuScreen==MenuScreen.Lobby&&InRoom)ConnectionPill(W-30,24);
        }
        // .brandmark: a 29 px orange square with a 5 px #152733 inset ring and 2 px dashed #a7cfc2 rules 5 px above and
        // below; the dashes march slowly here.
        private static void Brandmark(Rect r)
        {
            U.Fill(r,U.Hex(0xff6b35));U.Fill(new Rect(r.x+2,r.y+2,r.width-4,r.height-4),U.Hex(0x152733));U.Fill(new Rect(r.x+7,r.y+7,r.width-14,r.height-14),U.Hex(0xff6b35));
            float off=Mathf.Repeat(Time.unscaledTime*6,10),x0=r.x-5,x1=r.xMax+5;
            foreach(float y in new[]{r.y-5,r.yMax+3})for(float x=x0-10+off;x<x1;x+=10){float a=Mathf.Max(x,x0),b=Mathf.Min(x+6,x1);if(b>a)U.Fill(new Rect(a,y,b-a,2),U.Hex(0xa7cfc2));}
        }
        private string UpdateBadge(NativeUpdater u,out Color color,out bool pulse)
        {
            var s=u.State;pulse=s==NativeUpdateState.Available;color=pulse?U.Hex(0xffc590):s==NativeUpdateState.Error?U.Hex(0xff9a8d):s==NativeUpdateState.Downloading||s==NativeUpdateState.Verifying||s==NativeUpdateState.Preparing||s==NativeUpdateState.Restarting?U.Hex(0xffc590):U.Hex(0xb5b0be);
            switch(s)
            {
                case NativeUpdateState.Available:return "Update Available!";
                case NativeUpdateState.Current:return "Latest build";
                case NativeUpdateState.Checking:return "Checking for updates…";
                case NativeUpdateState.Downloading:return "Downloading "+Mathf.RoundToInt(Mathf.Clamp01(u.Progress)*100)+"%";
                case NativeUpdateState.Verifying:return "Verifying update…";
                case NativeUpdateState.Preparing:return "Preparing update…";
                case NativeUpdateState.Restarting:return "Restarting…";
                case NativeUpdateState.Error:return "Update check failed";
                // Idle covers three different outcomes; say which (the card shows the full status text).
                case NativeUpdateState.Idle:return NotPublished(u)?"No update published yet":(u.Status??"").IndexOf("cancel",StringComparison.OrdinalIgnoreCase)>=0?"Update cancelled":"Not checked yet";
                default:return "Updater off · open for details";
            }
        }
        // NativeUpdater reports a missing feed (HTTP 404 on latest.json) as Idle with this status.
        private static bool NotPublished(NativeUpdater u)=>u.State==NativeUpdateState.Idle&&(u.Status??"").StartsWith("No Unity update",StringComparison.Ordinal);
        // .update-pill + .update-alert. The source opens the desktop updater window; here the pill opens the update card
        // (UpdatePanel) beneath it, which stays open on its own while a download is verified, prepared and installed.
        private void UpdateControl(float right,float top)
        {
            var u=Updater;string label="Update Game";float w=17+15+8+U.Measure(label,12,true).x+17;var r=new Rect(right-w,top,w,38);
            float h=U.HoverAmount(9101,r,!MenuBlocked());
            U.Glow(new Rect(r.x,r.y+5,r.width,r.height),new Color(0,0,0,.27f),18);
            // Two fixed capsules (rest gradient, hover fill faded in by h) so the hover transition reuses cached textures.
            U.Capsule(r,new[]{0f,1f},new[]{U.Hex(0x302a32),U.Hex(0x191a22)},U.Mix(U.Hex(0x71616a),U.Hex(0xefb587),Mathf.Max(h,updateOpen?1:0)));
            if(h>0)U.Capsule(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),new[]{0f},new[]{U.Hex(0x393039)},default,h);
            U.Fill(new Rect(r.x+19,r.y+1,r.width-38,1),new Color(1,1,1,.05f));
            NativeIcons.Draw(new Rect(r.x+17,r.center.y-7.5f,15,15),"download",U.Hex(0xefb587));U.Text(new Rect(r.x+17+15+8,r.y,r.width,r.height),label,12,U.Hex(0xf4e6dc),TextAnchor.MiddleLeft,bold:true);
            // Opening the card also checks again (the automatic check runs once per launch), unless one is running,
            // an update is already waiting or the updater is off.
            if(U.Button(r)&&!MenuBlocked()){updateOpen=!updateOpen;Click();var st=u.State;if(updateOpen&&!u.Busy&&(st==NativeUpdateState.Idle||st==NativeUpdateState.Current||st==NativeUpdateState.Error))u.CheckForUpdates();}
            string text=UpdateBadge(u,out var color,out bool pulse);float a=pulse?M.Keys(Mathf.Repeat(Time.unscaledTime/1.7f,1),new[]{0f,.5f,1f},new[]{1f,.4f,1f},M.EaseInOut):1;
            float bw=U.Measure(text,12,pulse).x+16;var b=new Rect(r.center.x-bw/2,r.yMax+7,bw,20);
            if(b.xMax>right){b.x=right-bw;}U.Round(b,U.Hex(0x101114,.8f),6);U.Text(b,text,12,U.A(color,a),TextAnchor.MiddleCenter,bold:pulse,glow:pulse?U.Hex(0xffac68,.25f*a):default);
            var s=u.State;if(s==NativeUpdateState.Downloading||s==NativeUpdateState.Verifying||s==NativeUpdateState.Preparing||s==NativeUpdateState.Restarting)updateOpen=true;
            updateAnchor=new Vector2(right,b.yMax+10);
        }
        private Vector2 updateAnchor;
        private void UpdateDropdown(){if(Updater!=null&&updateOpen)UpdatePanel(updateAnchor.x,updateAnchor.y);else updateCard=Rect.zero;}
        // While the update card is open, controls underneath it ignore the pointer (IMGUI gives clicks to the first control).
        // Computed once per event at the top of HomeMenu, untransformed, from the card drawn last frame.
        private bool MenuBlocked()=>menuBlockedNow;
        // .bottom-line: a #415362 rule 23 px above the bottom with the build and arena counts in 12 px mono.
        private void Footer()
        {
            float W=U.W,H=U.H,y=H-23-14;U.Fill(new Rect(30,y-13,W-60,1),U.Hex(0x415362,.8f));
            string build=Updater?.Installed!=null?"BUILD "+NativeUpdater.Short(Updater.Installed.version):"NATIVE BUILD";
            U.Text(new Rect(30,y,W-60,14),build+"   /   DESKTOP CONTROLS",12,U.Hex(0xa8a9b3),TextAnchor.MiddleLeft,mono:true);
            U.Text(new Rect(30,y,W-60,14),content.maps.Count(m=>m.rotation)+" ARENAS   ·   2–8 PILOTS",12,U.Hex(0xa8a9b3),TextAnchor.MiddleRight,mono:true);
        }

        // ---- Home screen ---------------------------------------------------------------------------------------------
        // .home-lobby: a block up to 1160 px wide centred in the menu area (90 px top, 64 px bottom padding), the 460 px
        // menu column at the left and the Boss Test pill at the right, centred against the column; under 860 px the pill
        // moves below the column, right-aligned. Entrance stagger from style.css:139 and pve.css:21.
        private void HomeScreen(float age)
        {
            float W=U.W,H=U.H,blockW=Mathf.Min(1160,W-56),x0=(W-blockW)/2,colW=Mathf.Min(460,blockW);bool stacked=blockW<860;
            float size=Mathf.Clamp(W*.066f,68,104),line=size*.96f;bool dev=account.Developer;
            float colH=15+20+line*2+23+15*1.65f*2+27+3*48+16+9+40+46;
            var boss=BossPillSize();float totalH=stacked?colH+18+boss.y:colH,fit=Mathf.Clamp((H-150)/totalH,.6f,1),y0=Mathf.Max(80,90+(H-154-totalH*fit)/2);
            HomeKeys();
            // Short canvases (a large interface scale on a small window) scale the whole block to fit between bar and footer.
            U.Push(new Vector2(W/2,y0),Vector2.zero,fit);
            float ce=In(age,0,.26f);U.Push(new Vector2(x0+colW/2,y0+colH/2),new Vector2(0,10*(1-ce)),.99f+.01f*ce);float y=y0;
            // Warm light behind the title.
            U.RadialStops(new Rect(x0-160,y0-40,colW+320,line*2+200),U.Hex(0xff6b35,.075f*ce),new[]{0f,.55f,1f},new[]{1f,.35f,0f});
            // Eyebrow with an accent rule.
            float rule=18*In(age,.05f,.4f);U.Fill(new Rect(x0,y+6.5f,rule,2),U.Hex(0xff6b35,ce));
            U.Text(new Rect(x0+28,y,colW,15),"VOXEL COMBAT / UP TO 8 PILOTS",12,U.Hex(0xadb0b9,ce),TextAnchor.MiddleLeft,bold:true,tracking:.2f);y+=15+20;
            HomeTitle(x0,y,size,line,age,ce);y+=line*2+23;
            foreach(string text in new[]{"Install your edge.","Turn the arena into a weapon."}){U.Text(new Rect(x0,y,colW,15*1.65f),text,15,U.Hex(0xc0c1c7,ce),TextAnchor.MiddleLeft);y+=15*1.65f;}y+=27;
            var items=new[]{("swords","Play vs Bots","01",MenuScreen.Play),("network","Create Room","02",MenuScreen.Create),("users","Join Room","03",MenuScreen.Join)};
            for(int i=0;i<items.Length;i++)
            {
                float e=In(age,i*.04f,.3f);var r=new Rect(x0,y,colW,48);U.Push(r.center,new Vector2(0,10*(1-e)),.99f+.01f*e);
                if(MenuButton(r,items[i].Item1,items[i].Item2,items[i].Item3,i==0,true,ce*e))Go(items[i].Item4);U.Pop();y+=48+8;
            }
            y+=-8+9;
            // .submenus: Customize, Settings, the account tile and (developers) DEV Menu, sharing the column equally.
            string user=account.Account?.username;string accountLabel=user!=null?user+" · Account":dev?"DEV · Account":"Log in / Register";
            var tiles=new List<(string icon,string label,Action go)>{("palette","Customize",()=>Go(MenuScreen.Customize)),("settings","Settings",()=>Go(MenuScreen.Settings)),("users",accountLabel,()=>Go(MenuScreen.Account))};
            if(dev)tiles.Add((null,"DEV Menu",()=>{if(account.Account==null){accountNotice="Log in once to activate registered DEV tools.";Go(MenuScreen.Account);}else Go(MenuScreen.Dev);}));
            var widths=TileWidths(tiles.Select(t=>t.label).ToArray(),tiles.Select(t=>t.icon).ToArray(),colW,out bool icons);float tx=x0;
            for(int i=0;i<tiles.Count;i++){float e=In(age,.1f+i*.035f,.3f);var r=new Rect(tx,y,widths[i],40);U.Push(r.center,new Vector2(0,8*(1-e)));if(Tile(r,icons?tiles[i].icon:null,tiles[i].label,ce*e,i==2&&user!=null?user:null))tiles[i].go();U.Pop();tx+=widths[i]+8;}
            y+=40;
            // .subtle-link row: Developer Sandbox (developers, offline), the conversion catalogue (developers only: the source
            // has no such screen and its import notes are not for players) and Quit.
            float lx=x0;float le=ce*In(age,.22f,.3f);
            if(dev&&!Online&&SubtleLink(ref lx,y+15,"flask-conical","Developer Sandbox",le)){Go(MenuScreen.Play);playSandbox=true;}
            if(dev&&SubtleLink(ref lx,y+15,"book-open","Catalogue",le))Go(MenuScreen.Catalogue);
            if(SubtleLink(ref lx,y+15,"log-out","Quit",le)){Save();Application.Quit();}
            U.Pop();
            float be=In(age,.12f,.34f);var br=stacked?new Rect(x0+colW-boss.x,y0+colH+18,boss.x,boss.y):new Rect(x0+blockW-boss.x,y0+colH/2-boss.y/2,boss.x,boss.y);
            U.Push(br.center,new Vector2(0,10*(1-be)),.99f+.01f*be);if(BossPill(br,be))Go(MenuScreen.Boss);U.Pop();U.Pop();
        }
        // .submenus is a flex row of flex:1 tiles: equal shares, except a tile whose content needs more keeps its natural
        // width and the rest share what is left. When even that overflows, every icon goes (Tile then shortens or shrinks).
        private static float[] TileWidths(string[] labels,string[] icons,float total,out bool withIcons)
        {
            float gaps=8*(labels.Length-1);withIcons=true;
            for(int pass=0;pass<2;pass++)
            {
                bool useIcons=pass==0;var natural=labels.Select((l,i)=>16+U.Measure(l,13,true).x+(useIcons&&icons[i]!=null?19+18:0)).ToArray();if(natural.Sum()+gaps>total&&pass==0)continue;
                withIcons=useIcons;var widths=new float[labels.Length];var open=Enumerable.Range(0,labels.Length).ToList();float left=total-gaps;
                while(open.Count>0){float share=left/open.Count;var big=open.Where(i=>natural[i]>share).ToList();if(big.Count==0){foreach(int i in open)widths[i]=share;break;}foreach(int i in big){widths[i]=natural[i];left-=natural[i];open.Remove(i);}}
                if(left<0){float k=(total-gaps)/widths.Sum();for(int i=0;i<widths.Length;i++)widths[i]*=k;}return widths;
            }
            return labels.Select(_=>(total-gaps)/labels.Length).ToArray();
        }
        // Keys 1-4 open the four numbered entries (the source prints 01-04 but binds nothing).
        private void HomeKeys()
        {
            var e=Event.current;if(e.type!=EventType.KeyDown||GUIUtility.keyboardControl!=0||updateOpen)return;
            int k=e.keyCode>=KeyCode.Alpha1&&e.keyCode<=KeyCode.Alpha4?e.keyCode-KeyCode.Alpha1:e.keyCode>=KeyCode.Keypad1&&e.keyCode<=KeyCode.Keypad4?e.keyCode-KeyCode.Keypad1:-1;if(k<0)return;
            e.Use();Click();Go(k==0?MenuScreen.Play:k==1?MenuScreen.Create:k==2?MenuScreen.Join:MenuScreen.Boss);
        }
        // .title: CHIP / ARENA. at clamp(68px,6.6vw,104px), weight 900, tracking -.065em, line-height .96, ARENA. in #ff6b35.
        // Letters rise in one by one, and a light sweep crosses ARENA. every few seconds.
        private static void HomeTitle(float x,float y,float size,float line,float age,float alpha)
        {
            string[] words={"CHIP","ARENA."};int n=0;float gap=-.065f*size,sweep=Mathf.Repeat(Time.unscaledTime,6.5f)/1.4f;
            for(int w=0;w<2;w++)
            {
                float cx=x,total=0;foreach(char ch in words[w])total+=U.Measure(ch.ToString(),size,true).x+gap;
                if(w==1)U.RadialStops(new Rect(x-30,y+line-20,total+60,line+40),U.Hex(0xff6b35,.16f*alpha),new[]{0f,.6f,1f},new[]{1f,.3f,0f});
                foreach(char ch in words[w])
                {
                    string s=ch.ToString();float cw=U.Measure(s,size,true).x,e=In(age,.06f+n*.035f,.5f);var r=new Rect(cx,y,cw+size*.2f,line);
                    U.Push(new Vector2(cx+cw/2,y+line/2),new Vector2(0,22*(1-e)));
                    Color c=w==0?U.Hex(0xf4f4f5):U.Hex(0xff6b35);U.Text(r,s,size,U.A(c,alpha*e),TextAnchor.MiddleLeft,bold:true,shadow:new Color(0,0,0,.35f*alpha*e));
                    if(w==1&&sweep<1){float d=Mathf.Abs((cx+cw/2-x)/Mathf.Max(1,total)-sweep*1.4f+.2f);float k=Mathf.Clamp01(1-d*4);if(k>0)U.Text(r,s,size,U.Hex(0xffc4a3,alpha*e*k*.85f),TextAnchor.MiddleLeft,bold:true);}
                    U.Pop();cx+=cw+gap;n++;
                }
                y+=line;
            }
        }
        // .btn / .btn.primary (visual revision): icon, label and an optional mono key at the right; hover lifts 1 px and
        // warms the edge, press scales to .985, the primary warms to #ff865b with an orange underglow and carries a sheen.
        private bool MenuButton(Rect r,string icon,string text,string key=null,bool primary=false,bool enabled=true,float alpha=1,float size=15,bool center=false)
        {
            int id=text.GetHashCode()^((int)r.x*73856093)^((int)r.y*19349663);bool blocked=MenuBlocked();float h=U.HoverAmount(id,r,enabled&&!blocked);
            bool press=enabled&&!blocked&&U.Hover(r)&&Input.GetMouseButton(0);float a=alpha*(enabled?1:.45f);var d=new Rect(r.x,r.y-h,r.width,r.height);
            if(press){float s=.985f;d=new Rect(d.center.x-d.width*s/2,r.center.y-r.height*s/2,d.width*s,d.height*s);}
            Color fill=primary?U.Mix(U.Hex(0xff6b35),U.Hex(0xff865b),h):U.Mix(U.Hex(0x222329),U.Hex(0x303138),h),edge=primary?U.Hex(0xff6b35):U.Mix(U.Hex(0x484952),U.Hex(0xff784b),h),ink=primary?U.Hex(0x111216):U.Hex(0xf5f5f7);
            if(primary&&h>0)U.Glow(new Rect(d.x+6,d.y+10,d.width-12,d.height-8),U.Hex(0xff6b35,.45f*h*a),16);
            U.Round(d,U.A(fill,a),4,U.A(edge,a));if(primary&&enabled)Sheen(d,a);
            float iconSize=size>=15?19:17,textW=U.Measure(text,size,true).x,start=center?d.center.x-((icon!=null?iconSize+18:0)+textW)/2:d.x+18;
            if(icon!=null)NativeIcons.Draw(new Rect(start+(center?0:2*h),d.center.y-iconSize/2,iconSize,iconSize),icon,U.A(ink,a));
            float tx=start+(icon!=null?iconSize+(center?18:27):0);U.Text(new Rect(tx,d.y,d.xMax-tx,d.height),text,size,U.A(ink,a),TextAnchor.MiddleLeft,bold:true);
            if(key!=null)U.Text(new Rect(d.x,d.y,d.width-18,d.height),key,12,U.A(ink,a*(.55f+.3f*h)),TextAnchor.MiddleRight,mono:true);
            bool hit=enabled&&!blocked&&alpha>.5f&&U.Button(r);if(hit)Click();return hit;
        }
        // A soft light band crossing the primary action every 5.5 s.
        private static void Sheen(Rect d,float alpha)
        {
            float t=Mathf.Repeat(Time.unscaledTime,5.5f)/1.1f;if(t>=1)return;float band=70,bx=d.x-band+t*(d.width+band);
            var l=Rect.MinMaxRect(Mathf.Max(d.x+2,bx),d.y+2,Mathf.Min(d.xMax-2,bx+band/2),d.yMax-2);var r=Rect.MinMaxRect(Mathf.Max(d.x+2,bx+band/2),d.y+2,Mathf.Min(d.xMax-2,bx+band),d.yMax-2);
            var c=new Color(1,1,1,.22f*alpha);if(l.width>0)U.HGradient(l,Color.clear,c);if(r.width>0)U.HGradient(r,c,Color.clear);
        }
        // .submenus .btn: centred icon and 13 px label (the row drops icons together when any label would not fit); a label
        // still too long falls back to `shorter`, then shrinks to fit.
        private bool Tile(Rect r,string icon,string text,float alpha,string shorter=null)
        {
            float room=r.width-16,size=13,w=U.Measure(text,size,true).x;if(w>room&&shorter!=null){text=shorter;w=U.Measure(text,size,true).x;}if(icon!=null&&19+18+w>room)icon=null;if(w>room)size=Mathf.Max(10,size*room/w);
            return MenuButton(r,icon,text,null,false,true,alpha,size,true);
        }
        // .subtle-link: transparent, 13 px #a9aab5 brightening on hover, with its icon.
        private bool SubtleLink(ref float x,float y,string icon,string text,float alpha)
        {
            float w=16+7+U.Measure(text,13).x;var r=new Rect(x,y-4,w,26);x+=w+22;bool blocked=MenuBlocked();float h=U.HoverAmount(text.GetHashCode()^7919,r,!blocked);
            Color c=U.A(U.Mix(U.Hex(0xa9aab5),U.Hex(0xe6f4ec),h),alpha);NativeIcons.Draw(new Rect(r.x,y-.5f,16,16),icon,c);U.Text(new Rect(r.x+23,r.y,w,r.height),text,13,c,TextAnchor.MiddleLeft);
            bool hit=!blocked&&alpha>.5f&&U.Button(r);if(hit)Click();return hit;
        }
        // pve/menu.ts SKULL (48 x 48): the bone in a #fbf9ff → #d9d3e6 → #9d95ad gradient, sockets, nose and teeth in
        // #09080d; the eyes are drawn separately so they can brighten to white on hover as in pve.css.
        private const string SkullMarkup="<defs><linearGradient id=\"bone\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"#fbf9ff\"/><stop offset=\".62\" stop-color=\"#d9d3e6\"/><stop offset=\"1\" stop-color=\"#9d95ad\"/></linearGradient></defs>"
            +"<path fill=\"url(#bone)\" d=\"M24 4.5C13.2 4.5 6.5 12.1 6.5 21.4c0 5.5 2.5 9.5 6.3 11.9v4.6a3.2 3.2 0 0 0 3.2 3.2h16a3.2 3.2 0 0 0 3.2-3.2v-4.6c3.8-2.4 6.3-6.4 6.3-11.9C41.5 12.1 34.8 4.5 24 4.5Z\"/>"
            +"<path fill=\"#09080d\" d=\"M12.6 22.6c-.1-3.2 2.5-5.2 5.5-4.8 2.8.4 4.2 2.8 3.5 5.6-.6 2.6-2.8 4.4-5.4 4.1-2.3-.3-3.5-2.2-3.6-4.9Zm22.8 0c.1-3.2-2.5-5.2-5.5-4.8-2.8.4-4.2 2.8-3.5 5.6.6 2.6 2.8 4.4 5.4 4.1 2.3-.3 3.5-2.2 3.6-4.9ZM24 27.8l-2.6 4.6h5.2Z\"/>"
            +"<path fill=\"none\" stroke=\"#09080d\" stroke-width=\"1.5\" stroke-linecap=\"round\" d=\"M16.4 35.6h15.2M20 35.6v5.3M24 35.6v5.3M28 35.6v5.3\"/>";
        private const string SkullEyes="<circle cx=\"17.4\" cy=\"22.8\" r=\"1.7\" fill=\"#fff\"/><circle cx=\"30.6\" cy=\"22.8\" r=\"1.7\" fill=\"#fff\"/>";
        public static void Skull(Rect r,float alpha,float eyes)
        {
            NativeIcons.DrawMarkup(new Rect(r.x,r.y+1,r.width,r.height),"skull",SkullMarkup,48,new Color(0,0,0,.8f*alpha));NativeIcons.DrawMarkup(r,"skull",SkullMarkup,48,new Color(1,1,1,alpha));
            NativeIcons.DrawMarkup(r,"skull-eyes",SkullEyes,48,U.A(U.Mix(U.Hex(0xc9bcff),Color.white,eyes),alpha));
        }
        private static Vector2 BossPillSize(){float text=Mathf.Max(U.Measure("BOSS TEST",19,true,false,.17f).x,U.Measure("PVE · THE SIREN",11,true,false,.16f).x);return new Vector2(10+52+14+text+14+U.Measure("04",16).x+22,70);}
        // .boss-pill: a glossy black capsule (#3a3942 → #17171d 45% → #030305 55% → #0e0e13) with a gloss cap, violet aura
        // (#7d5cff), the skull in a 52 px radial socket, BOSS TEST / PVE · THE SIREN and its 04 key; hover lifts 2 px,
        // brightens the edge to #b7a8ff and runs the one-second sheen.
        private float bossHoverAt=-9;
        private bool BossPill(Rect r,float alpha)
        {
            bool blocked=MenuBlocked();float h=U.HoverAmount(9102,r,!blocked);if(h>0&&bossHoverAt<0)bossHoverAt=Time.unscaledTime;if(h<=0)bossHoverAt=-9;
            var d=new Rect(r.x,r.y-2*h,r.width,r.height);float breathe=.5f+.5f*Mathf.Sin(Time.unscaledTime*2.4f);
            U.Glow(new Rect(d.x,d.y+16,d.width,d.height),new Color(0,0,0,(.7f+.1f*h)*alpha),34);U.Glow(d,U.Hex(0x7d5cff,(.12f+.05f*breathe+.2f*h)*alpha),30+10*h);
            U.Capsule(new Rect(d.x-1,d.y-1,d.width+2,d.height+2),new[]{0f},new[]{Color.black},default,alpha);
            U.Capsule(d,new[]{0f,.45f,.55f,1f},new[]{U.Hex(0x3a3942),U.Hex(0x17171d),U.Hex(0x030305),U.Hex(0x0e0e13)},U.Mix(new Color(1,1,1,.19f),U.Hex(0xb7a8ff,.65f),h),alpha);
            U.Capsule(new Rect(d.x+16,d.y+3,d.width-32,d.height*.45f),new[]{0f,1f},new[]{new Color(1,1,1,.26f),new Color(1,1,1,.02f)},default,alpha);
            U.Fade(new Rect(d.x+d.height/2,d.yMax-10,d.width-d.height,8),new Color(0,0,0,.45f*alpha),false);
            if(bossHoverAt>=0){float t=M.EaseOut(M.Clamp01((Time.unscaledTime-bossHoverAt)/1f)),k=t<.25f?t/.25f:1-(t-.25f)/.75f;float bx=d.x+(-.45f+1.7f*t)*d.width,bw=d.width*.32f;
                var band=Rect.MinMaxRect(Mathf.Max(d.x+d.height/2,bx),d.y+6,Mathf.Min(d.xMax-d.height/2,bx+bw),d.yMax-6);if(band.width>0&&k>0){var c=new Color(1,1,1,.19f*k*alpha);U.HGradient(new Rect(band.x,band.y,band.width/2,band.height),Color.clear,c);U.HGradient(new Rect(band.center.x,band.y,band.width/2,band.height),c,Color.clear);}}
            var socket=new Rect(d.x+10,d.center.y-26,52,52);U.Radial(new Rect(socket.x-10,socket.y-10,72,72),U.Hex(0xb7a8ff,.15f*alpha));U.Disc(socket,U.A(U.Hex(0x060508),alpha));
            U.RadialStops(new Rect(socket.x-6,socket.y-14,64,64),U.Hex(0x302c39,alpha),new[]{0f,.72f,1f},new[]{1f,.0f,0f});U.RadialStops(socket,new Color(1,1,1,.09f*alpha),new[]{0f,.92f,.97f,1f},new[]{0f,0f,1f,0f});
            Skull(new Rect(socket.center.x-18,socket.center.y-18,36,36),alpha,Mathf.Max(h,.25f*breathe));
            float tx=socket.xMax+14;U.Text(new Rect(tx,d.center.y-21,200,22),"BOSS TEST",19,new Color(.965f,.953f,1,alpha),TextAnchor.MiddleLeft,bold:true,tracking:.17f,shadow:new Color(0,0,0,alpha));
            U.Text(new Rect(tx,d.center.y+5,200,14),"PVE · THE SIREN",11,U.Hex(0xc9bfff,alpha),TextAnchor.MiddleLeft,bold:true,tracking:.16f);
            U.Text(new Rect(d.x,d.y,d.width-22,d.height),"04",16,U.Hex(0xd9d2f0,alpha),TextAnchor.MiddleRight);
            bool hit=!blocked&&alpha>.5f&&U.Button(r);if(hit)Click();return hit;
        }

        // ---- Panel screens -------------------------------------------------------------------------------------------
        private struct ScreenLayout{public Rect Panel,Body,Card;public float Enter;public bool Closed;}
        // main.ts panel(): eyebrow, 30 px title and the close X over a .panel (#17181df5, #45464e edge, 10 px radius, 32 px
        // padding), at most 100dvh-158 tall, panel-in on entry. `showcase` adds the pilot card beside it (pilot-lobby-layout)
        // when the canvas has room; `head` reserves kit-drawn space under the title. The body is sized to its content.
        // `lobby` is the room's shell (lobby.css .menus[data-screen=lobby]>.panel.wide): 28 px padding, at most 100dvh-140
        // tall, on the 125deg #25212bf5 → #17191ff7 gradient (drawn left to right).
        private ScreenLayout BeginScreen(string eyebrow,string title,float width,float age,bool showcase=false,bool left=false,bool actions=true,float head=0,bool close=true,bool lobby=false)
        {
            float W=U.W,H=U.H,e=In(age,0,.26f),dy=10*(1-e),pad=lobby?28:32,top=lobby?94:108;
            float card=showcase&&W-56-width-26>=270?Mathf.Min(390,W-56-width-26):0,pw=Mathf.Min(width,W-56-(card>0?card+26:0)),total=pw+(card>0?26+card:0);
            float px=left?Mathf.Max(28,W*.05f):(W-total)/2,maxH=Mathf.Max(320,H-(lobby?140:158)),bottom=actions?26+20+48+30:pad-2,ph=Mathf.Min(maxH,top+head+screenContent+bottom),cardH=card>0?Mathf.Min(maxH,Mathf.Max(ph,520)):0;
            float py=Mathf.Max(80,90+(H-154-Mathf.Max(ph,cardH))/2);
            var panel=new Rect(px,py+dy,pw,ph);
            if(lobby){U.Glow(new Rect(panel.x,panel.y+30,panel.width,panel.height),new Color(0,0,0,.45f*e),60);U.Backdrop(panel,10,16,e);U.RoundGradient(panel,U.Hex(0x25212b,.96f*e),U.Hex(0x17191f,.97f*e),10,U.Hex(0x45464e,e),1,true);}
            else U.Panel(panel,e);
            U.Text(new Rect(panel.x+pad,panel.y+pad-2,pw-120,16),eyebrow,12,U.Hex(0xc0c1ca,e),TextAnchor.MiddleLeft,bold:true,tracking:lobby?.18f:.2f);
            U.Text(new Rect(panel.x+pad,panel.y+pad+20,pw-120,38),title,30,U.Hex(0xf4f4f5,e),TextAnchor.MiddleLeft,bold:true,tracking:-.03f);
            bool closed=false;if(close){var x=new Rect(panel.xMax-pad-40,panel.y+pad-4,40,40);float h=U.HoverAmount(9103,x);U.Round(x,U.A(U.Mix(U.Hex(0x222329),U.Hex(0x303138),h),e),4,U.A(U.Mix(U.Hex(0x45464e),U.Hex(0xff6b35),h),e));NativeIcons.Draw(new Rect(x.center.x-10.5f,x.center.y-10.5f,21,21),"x",U.Hex(0xf4f4f5,e));closed=U.Button(x);if(closed)Click();}
            var layout=new ScreenLayout{Panel=panel,Enter=e,Closed=closed,Body=new Rect(panel.x+pad,panel.y+top+head,pw-2*pad,ph-top-head-bottom),Card=card>0?new Rect(panel.xMax+26,panel.y,card,cardH):Rect.zero};
            return layout;
        }
        // The panel body: the existing GUILayout form inside its own area and scroll view, faded with the panel.
        private void ScreenBody(ScreenLayout l,Action draw)
        {
            var c=GUI.color;GUI.color=new Color(c.r,c.g,c.b,c.a*l.Enter);GUILayout.BeginArea(l.Body);
            U.BeginDeferred();screenScroll=GUILayout.BeginScrollView(screenScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar);draw();GUI.enabled=true;GUILayout.Space(4);
            if(Event.current.type==EventType.Repaint)screenContent=GUILayoutUtility.GetLastRect().yMax;GUILayout.EndScrollView();GUILayout.EndArea();GUI.color=c;U.EndDeferred(l.Body,screenScroll);
        }
        // .lobby-actions: a rule, then equal-width actions along the panel's foot; returns the index clicked, or -1.
        private int ScreenActions(ScreenLayout l,params (string icon,string label,bool primary,bool enabled)[] items)
        {
            var p=l.Panel;float y=p.yMax-30-48,x=p.x+32,iw=p.width-64,gap=10,w=(iw-gap*(items.Length-1))/items.Length;int hit=-1;
            U.Fill(new Rect(x,y-20,iw,1),U.Hex(0x4b414c,.7f*l.Enter));
            for(int i=0;i<items.Length;i++)if(MenuButton(new Rect(x+i*(w+gap),y,w,48),items[i].icon,items[i].label,null,items[i].primary,items[i].enabled,l.Enter,15,true))hit=i;
            return hit;
        }
        // A one-line status under the actions' rule (errors in the source's .connection-error red).
        private static void ScreenStatus(ScreenLayout l,string text,bool error=false)
        {if(string.IsNullOrEmpty(text))return;var p=l.Panel;var line=U.Wrap(text,13,p.width-64,false,1);U.Text(new Rect(p.x+32,p.yMax-30-48-20-24,p.width-64,18),line.Count>0?line[0]:text,13,error?U.Hex(0xff8a7a,l.Enter):U.Hex(0xa6a7ad,l.Enter),TextAnchor.MiddleLeft);}
        // Join's own entry checks are errors as well (the source shows a refused join entry in .connection-error), though
        // their wording names none of the failure words below.
        private const string JoinCodeShapeError="Enter the six-character room code or paste a room invite link.";
        private bool NetworkError=>networkStatus==JoinCodeShapeError||networkStatus.IndexOf("unavailable",StringComparison.OrdinalIgnoreCase)>=0||networkStatus.IndexOf("fail",StringComparison.OrdinalIgnoreCase)>=0||networkStatus.IndexOf("disconnect",StringComparison.OrdinalIgnoreCase)>=0||networkStatus.IndexOf("does not match",StringComparison.OrdinalIgnoreCase)>=0||networkStatus.IndexOf("is full",StringComparison.OrdinalIgnoreCase)>=0||networkStatus.IndexOf("add a bot",StringComparison.OrdinalIgnoreCase)>=0||networkStatus.IndexOf("both teams",StringComparison.OrdinalIgnoreCase)>=0||networkStatus.IndexOf("invalid",StringComparison.OrdinalIgnoreCase)>=0||networkStatus.IndexOf("could not",StringComparison.OrdinalIgnoreCase)>=0||networkStatus.IndexOf("choose a port",StringComparison.OrdinalIgnoreCase)>=0||networkStatus.IndexOf("rejected",StringComparison.OrdinalIgnoreCase)>=0;

        private void PlayScreen(float age)
        {
            // The Developer Sandbox link opens this screen in sandbox mode, so the arena and bots are chosen before it starts.
            var l=BeginScreen(playSandbox?"DEVELOPER SANDBOX":"LOCAL MATCH",playSandbox?"Developer Sandbox":"Play vs Bots",650,age,true);if(l.Closed){Go(MenuScreen.Home);return;}
            ScreenBody(l,()=>MatchRulesBody(false));if(l.Card.width>0)PilotCard(l.Card,"YOUR PILOT",l.Enter,playSandbox?"Local only. Sandbox tools are in the pause menu (Esc).":"Change your lobby look and preferred weapon in Customize.");
            // main.ts:147: in teams mode Start waits for two different teams; the reason is the team setup's note, shown once.
            bool blocked=PracticeTeamsBlocked();
            if(ScreenActions(l,("play",playSandbox?"Start Sandbox":"Start Match",true,server==null&&(!playSandbox||account.Developer)&&!blocked))==0){prefs.pve=false;sandboxWanted=playSandbox&&account.Developer;StartMatch();}
        }
        private void BossScreen(float age)
        {
            var l=BeginScreen("PVE · BOSS TEST","Test Boss",650,age,true,head:122);if(l.Closed){Go(MenuScreen.Home);return;}
            BossCard(new Rect(l.Panel.x+32,l.Panel.y+104,l.Panel.width-64,108),l.Enter);
            ScreenBody(l,()=>BossBody());if(l.Card.width>0)PilotCard(l.Card,"YOUR PILOT",l.Enter,"Up to four pilots: party bots fill the empty seats.");
            int a=ScreenActions(l,("play","Start Boss Fight",true,server==null),("network","Host Co-op Room",false,true));
            if(a==0){prefs.pve=true;sandboxWanted=false;StartMatch();}else if(a==1){prefs.pve=true;Go(MenuScreen.Create);}
        }
        // .pve-boss-card.active: the Siren on a violet radial with the skull, beside the greyed "More bosses" card.
        private static void BossCard(Rect r,float e)
        {
            float w=(r.width-12)*2/3;var c=new Rect(r.x,r.y,w,r.height);U.Round(c,U.Hex(0x0b0910,e),10,U.Hex(0xb7a8ff,.6f*e));U.RadialStops(new Rect(c.x+2,c.y+2,c.width-4,c.height-4),U.Hex(0x3a2166,.95f*e),new[]{0f,.55f,1f},new[]{1f,.45f,0f});
            float pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*2.1f);U.Radial(new Rect(c.x+14,c.center.y-40,80,80),U.Hex(0xb7a8ff,(.12f+.1f*pulse)*e));Skull(new Rect(c.x+26,c.center.y-28,56,56),e,pulse*.6f);
            U.Text(new Rect(c.x+100,c.y+22,c.width-110,26),"The Siren",19,U.Hex(0xf6f3ff,e),TextAnchor.MiddleLeft,bold:true);
            U.Text(new Rect(c.x+100,c.y+52,c.width-114,40),"Ward Zero · survive 3 waves of minions, then the Siren",12,U.Hex(0xc9bfff,e),TextAnchor.UpperLeft,wrap:true);
            var s=new Rect(c.xMax+12,r.y,r.width-w-12,r.height);U.Round(s,U.Hex(0x15161b,.45f*e),10,U.Hex(0x5a5b66,.45f*e));
            NativeIcons.Draw(new Rect(s.center.x-14,s.y+18,28,28),"circle-help",U.Hex(0xa6a7ad,.45f*e));U.Text(new Rect(s.x,s.y+54,s.width,20),"More bosses",14,U.Hex(0xe6e7ec,.45f*e),TextAnchor.MiddleCenter,bold:true);U.Text(new Rect(s.x,s.y+74,s.width,16),"Coming later",11,U.Hex(0xa6a7ad,.45f*e),TextAnchor.MiddleCenter);
        }
        private void CreateScreen(float age)
        {
            var l=BeginScreen("CHIP ARENA","Host a private room",650,age,true);if(l.Closed){CloseNetwork();Go(MenuScreen.Home);return;}
            ScreenBody(l,CreateBody);if(l.Card.width>0)PilotCard(l.Card,"YOUR PILOT",l.Enter,"Guests see your pilot in the room lobby.");
            bool busy=relayConnecting!=null;ScreenStatus(l,networkStatus,NetworkError);
            if(ScreenActions(l,("play",busy?"Starting server…":"Create Room",true,!busy&&connecting==null))==0){if(internetRooms)HostRelay();else HostRoom();}
        }
        private void JoinScreen(float age)
        {
            var l=BeginScreen("CHIP ARENA","Join a private room",650,age,true);if(l.Closed){CloseNetwork();Go(MenuScreen.Home);return;}
            ScreenBody(l,JoinBody);if(l.Card.width>0)PilotCard(l.Card,"YOUR PILOT",l.Enter,"Your loadout is sent to the host when you join.");
            bool busy=connecting!=null||server!=null&&!InRoom;ScreenStatus(l,networkStatus,NetworkError);MarkRoomStale();
            // A pasted room invite link joins through JoinInvite (its address, port and code); anything else is a room code:
            // Online is the default; Local network is an optional connection mode. Legacy CA- codes remain pasteable.
            if(ScreenActions(l,("play",busy?"Finding room…":"Join Room",true,!busy&&joinCode.Trim().Length>0))==0)
            {
                string entry=joinCode.Trim();if(JoinInvite(entry))return;
                if(entry.IndexOf("://",StringComparison.Ordinal)>=0)networkStatus="Could not find the room.";
                else if(entry.StartsWith("CA-",StringComparison.OrdinalIgnoreCase))JoinRoom();
                else if(!RoomCodeShape(entry))networkStatus=JoinCodeShapeError;
                else JoinRoom();
            }
        }
        private static bool RoomCodeShape(string code)=>NativeRoomCodes.TryNormalize(code,out _);
        // Customize is the Locker (NativeLockerUI.cs): categories, the pilot on its turntable, item cards, Save / Revert.
        private void CustomizeScreen(float age)=>LockerScreen(age);
        private void SettingsScreen(float age,Action back)
        {
            var l=BeginScreen("CHIP ARENA","Settings",640,age);if(l.Closed){back();return;}
            ScreenBody(l,SettingsBody);int a=ScreenActions(l,("check","Save Settings",true,true),("activity","Connection report",false,true));if(a==0)back();else if(a==1)OpenConnectionReport();
        }
        private void AccountScreen(float age)
        {
            var a=account.Account;string title=a!=null?a.username:account.Developer?"DEV account":accountAction=="register"?"Create your account":accountAction=="recover"?"Recover your account":"Log in";
            var l=BeginScreen("CHIP ARENA",title,640,age);if(l.Closed){Go(MenuScreen.Home);return;}
            ScreenBody(l,AccountBody);ScreenStatus(l,(account.Status+" "+accountNotice).Trim(),accountNotice.Length>0);
            if(a==null){if(ScreenActions(l,("users",accountAction=="register"?"Create account":accountAction=="recover"?"Reset password":"Log in",true,!account.Busy))==0)AccountSubmit();return;}
            AccountActions(l);
        }
        private void DevScreen(float age)
        {
            var l=BeginScreen("CHIP ARENA","DEV · Player grants",1020,age,actions:false);if(l.Closed||!account.Developer){Go(account.Developer?MenuScreen.Home:MenuScreen.Account);return;}
            ScreenBody(l,AdminBody);
        }
        private void CatalogueScreen(float age)
        {
            var l=BeginScreen("SOURCE REFERENCE","Catalogue",900,age,actions:false);if(l.Closed||!account.Developer){Go(MenuScreen.Home);return;}
            ScreenBody(l,Catalogue);
        }

        // ---- Pilot card ----------------------------------------------------------------------------------------------
        // .pilot-card beside the panel: the pilot's live 3D portrait in its lobby pose inside the turning ring lit by the
        // accent colour (NativePortraitUI.cs PilotStage; the emblem where it cannot render), name, weapon and class, the
        // two colours and the presentation picks, all updating live as the form changes.
        private static Color HexColor(string value,int fallback)=>value!=null&&value.Length==7&&value[0]=='#'&&int.TryParse(value.Substring(1),System.Globalization.NumberStyles.HexNumber,null,out int rgb)?U.Hex(rgb):U.Hex(fallback);
        private static string ClassLabel(string c)=>c=="ranged"?"RANGED · PRECISION & PRESSURE":c=="magic"?"MAGIC · ELEMENTS & CONTROL":c=="melee"?"MELEE · CLOSE & PERSONAL":"DEV CLASS";
        // The pilot emblem that stands in for the source's live 3D portrait: the weapon icon in its colour on a dark disc
        // edged in the body colour, inside a slowly turning tick ring and glow in the accent colour, with an orbiting
        // dot. `phase` offsets the motion so a row of cards does not turn in lockstep.
        private static void PilotEmblem(Vector2 c,float ring,Color accent,Color body,ContentWeapon weapon,float e,float t,float phase=0,int ticks=28)=>PilotEmblem(c,ring,accent,body,NativeIcons.Spec(weapon.id),U.Hex((int)weapon.color),e,t,phase,ticks);
        private static void PilotEmblem(Vector2 c,float ring,Color accent,Color body,string iconId,Color tone,float e,float t,float phase=0,int ticks=28)
        {
            t+=phase;
            U.Radial(new Rect(c.x-ring*1.6f,c.y-ring*1.6f,ring*3.2f,ring*3.2f),U.A(accent,.16f*e));
            for(int i=0;i<ticks;i++){float a=i/(float)ticks*360+t*12;U.Push(c,Vector2.zero,1,a);U.Fill(new Rect(c.x-1.5f,c.y-ring-3,3,i%7==0?9:5),U.A(accent,(i%7==0?.9f:.45f)*e));U.Pop();}
            U.Disc(new Rect(c.x-ring*.78f,c.y-ring*.78f,ring*1.56f,ring*1.56f),U.Hex(0x0f1015,.92f*e));U.RadialStops(new Rect(c.x-ring*.78f,c.y-ring*.78f,ring*1.56f,ring*1.56f),U.A(body,.1f*e),new[]{0f,.9f,.97f,1f},new[]{0f,0f,1f,0f});
            float orbit=t*.9f;U.Disc(new Rect(c.x+Mathf.Cos(orbit)*ring*.92f-3,c.y+Mathf.Sin(orbit)*ring*.92f-3,6,6),U.A(accent,e));
            float icon=ring*.86f;NativeIcons.Draw(new Rect(c.x-icon/2+1,c.y-icon/2+2,icon,icon),iconId,new Color(0,0,0,.5f*e));NativeIcons.Draw(new Rect(c.x-icon/2,c.y-icon/2,icon,icon),iconId,U.A(U.Mix(tone,Color.white,.15f),e));
        }
        private void PilotCard(Rect r,string tag,float e,string note)
        {
            var weapon=content.weapons.FirstOrDefault(w=>w.id==prefs.weapon)??content.weapons[0];Color accent=HexColor(prefs.accentColor,0x70e1d0),body=HexColor(prefs.bodyColor,0xe5e7eb),tone=U.Hex((int)weapon.color);float t=Time.unscaledTime;
            U.Glow(new Rect(r.x,r.y+18,r.width,r.height),new Color(0,0,0,.33f*e),45);U.Round(r,U.Hex(0x1b1a21,e),16,U.Hex(0x514955,e));
            U.RadialStops(new Rect(r.x+r.width*.05f,r.y+r.height*.06f,r.width*.9f,r.height*.72f),U.A(accent,.2f*e),new[]{0f,.64f,1f},new[]{1f,0f,0f});
            U.Text(new Rect(r.x+22,r.y+20,60,16),"01",12,U.Hex(0xa6a7ad,e),TextAnchor.MiddleLeft,mono:true);U.Text(new Rect(r.x,r.y+20,r.width-22,16),tag,11,U.Hex(0xff996f,e),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.16f);
            float y=PilotStage(r,accent,body,weapon,e,t,note);
            string name=NativeCosmeticRules.Name(prefs.name);float ns=Mathf.Clamp(r.width*.075f,22,30);
            U.Text(new Rect(r.x+16,y,r.width-32,ns*1.3f),name,ns,U.Hex(0xf4f4f5,e),TextAnchor.MiddleCenter,bold:true);y+=ns*1.3f+4;
            U.Text(new Rect(r.x+16,y,r.width-32,18),weapon.name.ToUpperInvariant(),13,U.A(U.Mix(tone,Color.white,.25f),e),TextAnchor.MiddleCenter,bold:true,tracking:.12f);y+=20;
            float under=60+30*(.5f+.5f*Mathf.Sin(t*1.6f));U.Fill(new Rect(r.center.x-under/2,y+2,under,2),U.A(tone,.85f*e));y+=12;
            U.Text(new Rect(r.x+16,y,r.width-32,16),ClassLabel(weapon.@class),10,U.Hex(0xa6a7ad,e),TextAnchor.MiddleCenter,mono:true,tracking:.1f);y+=30;
            float sw=(r.width-44-12)/2;for(int i=0;i<2;i++){var s=new Rect(r.x+22+i*(sw+12),y,sw,30);U.Round(s,U.Hex(0x111216,.7f*e),6,new Color(1,1,1,.07f*e));U.Disc(new Rect(s.x+9,s.y+8,14,14),U.A(i==0?body:accent,e));U.Text(new Rect(s.x+30,s.y,s.width-34,s.height),i==0?"BODY":"ACCENT",10,U.Hex(0xc0c1ca,e),TextAnchor.MiddleLeft,mono:true,tracking:.12f);}y+=44;
            foreach(var (k,v) in new[]{("HEADWEAR",prefs.hat),("KILL EFFECT",prefs.killEffect),("INTRO",prefs.overrideIntro?prefs.intro:"neon-drop")})
            {if(y+18>r.yMax-46)break;U.Text(new Rect(r.x+22,y,r.width-44,18),k,10,U.Hex(0x8f909a,e),TextAnchor.MiddleLeft,mono:true,tracking:.12f);U.Text(new Rect(r.x+110,y,r.width-132,18),Fit(NativeCosmeticRules.Label(v??"none").Replace(" · DEV ONLY",""),12,r.width-132,false,0),12,U.Hex(0xe6e7ec,e),TextAnchor.MiddleRight);y+=22;}
            if(note!=null){var lines=U.Wrap(note,12,r.width-44,false,3);float ny=r.yMax-18-lines.Count*17;foreach(var line in lines){U.Text(new Rect(r.x+22,ny,r.width-44,17),line,12,U.Hex(0xa6a7ad,e),TextAnchor.MiddleLeft);ny+=17;}}
        }

        // ---- Pause menu ----------------------------------------------------------------------------------------------
        // main.ts:157 "Match menu" (Escape during a match): the status line, Resume, Connection report, Settings, the
        // sandbox tools in a sandbox, Restart offline, Return to Lobby, Return to Main Menu, the control hints; with the
        // pilot board beside it outside the sandbox. Return to Lobby starts the cancellable three-second return
        // (RequestLobbyReturn) and closes the menu as the source's 'lobby' action does; it is disabled for guests and
        // while the return runs, whose banner (NativeConnectionReport.cs) shows over this menu. The panel enters once per
        // opening; there is no close X.
        private void PauseMenu()
        {
            if(inspectSeat!=0)return;MenuStyles();float now=Time.unscaledTime;if(now-menuSeen>.25f)menuScreenAt=now;menuSeen=now;float age=now-menuScreenAt,W=U.W,H=U.H;
            menuBlockedNow=reportOpen;updateCard=Rect.zero;updateOpen=false;MenuShade(.9f);
            if(pauseScreen==PauseScreen.Settings){SettingsScreen(age,()=>{ApplySettings();Save();pauseScreen=PauseScreen.Menu;menuScreenAt=Time.unscaledTime;});return;}
            if(pauseScreen==PauseScreen.Sandbox){if(arena.Sandbox&&account.Developer&&!Online){SandboxScreen(age);return;}pauseScreen=PauseScreen.Menu;}
            if(!arena.Sandbox)Board(W);
            // A guest whose host is gone keeps a frozen replica: say so and offer the way back to Join Room to reclaim the seat.
            bool lost=arena.Replica&&server==null;
            bool returning=LobbyReturnSeconds>0;
            // main.ts:157 checks the mode first: a local boss test is a room of one there (mode 'online' over a LocalRoom), so it
            // stays "The boss test is paused." while a return runs.
            string status=lost?(networkStatus.Length==0||networkStatus.StartsWith("Disconnected")?"The host disconnected. Return to the main menu, then use Join Room within 90 seconds to reclaim your seat.":networkStatus+" Return to the main menu."):Online?"The online match continues while this menu is open."+(networkStatus.Length>0?" "+networkStatus:""):arena.Pve!=null?"The boss test is paused.":returning?"The return countdown continues while this menu is open.":"Local practice is paused.";
            var items=new List<(string icon,string label,bool primary,bool enabled,Action act)>();if(!lost)items.Add(("play","Resume",true,true,()=>menu=false));
            items.Add(("activity","Connection report",false,true,OpenConnectionReport));
            items.Add(("settings","Settings",false,true,()=>{pauseScreen=PauseScreen.Settings;menuScreenAt=Time.unscaledTime;screenScroll=Vector2.zero;screenContent=400;}));
            if(arena.Sandbox&&account.Developer&&!Online)items.Add(("flask-conical","Sandbox Tools",false,true,()=>{pauseScreen=PauseScreen.Sandbox;menuScreenAt=Time.unscaledTime;screenScroll=Vector2.zero;screenContent=400;}));
            if(!Online&&!lost)items.Add(("rotate-cw","Restart Match",false,true,StartMatch));
            if(!lost)items.Add(("undo-2","Return to Lobby",false,(Hosting||!Online)&&!returning,()=>{if(!Online)afterMatch=OfflineLobby();RequestLobbyReturn();menu=false;}));
            items.Add(("log-out","Return to Main Menu",lost,true,()=>{afterMatch=lost?MenuScreen.Join:MenuScreen.Home;if(Online)LeaveRoom();else ReturnToLobby();}));
            string hints="WASD move · double-tap W sprint · LMB fire / attack · RMB block · Q / E abilities · R reload · Shift roll · Space jump · V camera · Tab lock-on / board · G scores · Esc menu";
            string diag=content.Map(arena.MapId).name+"  ·  SET "+match.Set+" · ROUND "+match.Round+"  ·  "+match.Phase+"  ·  "+arena.Bullets.Count+" projectiles  ·  step "+stepMs.ToString("0.00")+" ms  ·  "+presentation.RenderingMode;
            float pw=Mathf.Min(520,W-56),iw=pw-64;var statusLines=U.Wrap(status,16,iw);var hintLines=U.Wrap(hints,13,iw);var diagLines=MonoLines(diag,11,iw);bool guest=Online&&!Hosting;
            float ph=108+statusLines.Count*16*1.6f+16+items.Count*56-8+(guest?24:0)+20+hintLines.Count*13*1.55f+8+diagLines.Count*16+30,e=In(age,0,.26f);
            // The source's .panel scrolls past calc(100dvh - 158px); this one cannot, so on a short canvas (a large interface
            // scale on a small window) it scales about its top corner (top centre in the sandbox) to end 24 above the bottom,
            // as HomeScreen fits its block. The top stays at least 80 down, under the lobby-return banner. Push scales the
            // buttons' hit areas with them.
            float fit=Mathf.Min(1,(H-80-24)/ph);
            var p=new Rect(arena.Sandbox?(W-pw)/2:Mathf.Max(28,W*.05f),Mathf.Max(80,(H-ph*fit)/2)+10*(1-e),pw,ph);U.Push(new Vector2(arena.Sandbox?p.center.x:p.x,p.y),Vector2.zero,fit);U.Panel(p,e);
            U.Text(new Rect(p.x+32,p.y+30,iw,16),"CHIP ARENA",12,U.Hex(0xc0c1ca,e),TextAnchor.MiddleLeft,bold:true,tracking:.2f);U.Text(new Rect(p.x+32,p.y+52,iw,38),"Match menu",30,U.Hex(0xf4f4f5,e),TextAnchor.MiddleLeft,bold:true,tracking:-.03f);
            float y=p.y+108;foreach(var s in statusLines){U.Text(new Rect(p.x+32,y,iw,16*1.6f),s,16,U.Hex(0xbfc0c9,e),TextAnchor.MiddleLeft);y+=16*1.6f;}y+=16;
            // A guest's note sits under Return to Lobby, as in the source's menu list.
            Action chosen=null;for(int i=0;i<items.Count;i++){float be=In(age,i*.03f,.3f);var r=new Rect(p.x+32,y,iw,48);U.Push(r.center,new Vector2(0,8*(1-be)));if(MenuButton(r,items[i].icon,items[i].label,null,items[i].primary,items[i].enabled,e*be))chosen=items[i].act;U.Pop();y+=56;
                if(guest&&items[i].label=="Return to Lobby"){U.Text(new Rect(p.x+32,y-4,iw,18),"The host can return everyone to the lobby.",13,U.Hex(0xa6a7ad,e*be),TextAnchor.MiddleLeft);y+=24;}}y-=8;y+=20;
            foreach(var s in hintLines){U.Text(new Rect(p.x+32,y,iw,13*1.55f),s,13,U.Hex(0xa6a7ad,e),TextAnchor.MiddleLeft);y+=13*1.55f;}y+=8;
            foreach(var s in diagLines){U.Text(new Rect(p.x+32,y,iw,16),s,11,U.Hex(0x7c7d86,e),TextAnchor.MiddleLeft,mono:true);y+=16;}
            U.Pop();if(chosen!=null){pauseScreen=PauseScreen.Menu;chosen();}
        }
        // Greedy lines of `  ·  `-separated parts measured in the mono face they are drawn in (not cached: the values change).
        private static List<string> MonoLines(string text,float size,float width)
        {
            var lines=new List<string>();string line="";foreach(var part in text.Split(new[]{"  ·  "},StringSplitOptions.None)){string next=line.Length==0?part:line+"  ·  "+part;if(line.Length>0&&U.Measure(next,size,false,true).x>width){lines.Add(line);line=part;}else line=next;}
            if(line.Length>0)lines.Add(line);return lines;
        }
        private void SandboxScreen(float age)
        {
            var l=BeginScreen("DEVELOPER SANDBOX","Sandbox tools",760,age,actions:false);if(l.Closed){pauseScreen=PauseScreen.Menu;menuScreenAt=Time.unscaledTime;return;}
            ScreenBody(l,SandboxBody);
        }
    }
}
