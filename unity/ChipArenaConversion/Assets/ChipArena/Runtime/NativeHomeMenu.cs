using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
using C=ChipArena.UnityConversion.NativeCyberTheme;
namespace ChipArena.UnityConversion
{
    // The source menus rebuilt natively: the home screen, its panel screens (Play vs Bots, Create / Join Room, the room
    // lobby, Boss Test, Customize, Settings, Account, DEV Menu, Catalogue) and the in-match pause menu. Chrome, navigation
    // and actions are kit-drawn; each panel body is the existing GUILayout form, inside its own area and scroll view, so
    // every setting keeps its validation and save path. The home screen is the cyber redesign (owner 2026-10-12) over the
    // rooftop (NativeRooftopPresentation.cs): the vector wordmark, constellation entries (keys 1-4), the Update Game circle,
    // hexagon buttons, the Connection report pill and the pilot's nameplate; the panel screens keep the orbiting map behind
    // the cyan panel chrome (NativeCyberTheme).
    public sealed partial class ConversionGame
    {
        private enum MenuScreen{Home,Play,Create,Join,Lobby,Boss,Customize,Settings,Account,Dev,Catalogue}
        private enum PauseScreen{Menu,Settings,Sandbox}
        private MenuScreen menuScreen;private PauseScreen pauseScreen;private float menuScreenAt=-1,menuSeen=-1,screenContent=400;private Vector2 screenScroll;
        private bool updateOpen,menuFromMatch,menuBlockedNow,roomGuest,playSandbox;private MenuScreen afterMatch=MenuScreen.Home;private Rect updateCard;private GUIStyle fieldLabel,fine,codeField;
        private void Click()=>nativeAudio?.UiCue("ui.click","ui",.4f);
        // CSS panel-in (.26 s cubic-bezier(.2,.8,.2,1)) progress for an element entering `delay` seconds into the screen.
        private static float In(float age,float delay,float duration)=>M.Bezier(.2f,.8f,.2f,1,M.Clamp01((age-delay)/duration));
        private void Go(MenuScreen screen)=>Go(screen,false);
        // `sandbox`: the Play screen opens as the Developer Sandbox. The side effects run now, from the screen the menu is on
        // or going to (MenuTarget); the screen shown changes through the transition (NativeMenuTransitionUI.cs ShowScreen).
        private void Go(MenuScreen screen,bool sandbox)
        {
            var from=MenuTarget;
            // Leaving Settings keeps what was changed (applied and saved); leaving Create / Join abandons a pending connection.
            if(from==MenuScreen.Settings&&screen!=MenuScreen.Settings){ApplySettings();Save();}
            if((from==MenuScreen.Create||from==MenuScreen.Join)&&screen!=MenuScreen.Lobby&&!InRoom&&(connecting!=null||relayConnecting!=null||server!=null||listingRooms)){CloseNetwork();networkStatus="Connection cancelled.";}
            if(screen==MenuScreen.Play){prefs.pve=false;prefs.bots=Mathf.Clamp(prefs.bots,1,7);sandboxWanted=false;}
            if(screen==MenuScreen.Boss){prefs.pve=true;prefs.bots=Mathf.Clamp(prefs.bots,0,3);sandboxWanted=false;}
            // A room opens at the top of both lobby columns; Customize and Settings return to where the lobby was.
            if(screen==MenuScreen.Lobby&&from!=MenuScreen.Customize&&from!=MenuScreen.Settings){lobbyRosterScroll=lobbyRoomScroll=Vector2.zero;lobbyRosterContent=lobbyRoomContent=400;}
            // Entering the Locker takes the look it can revert to (Preview Intro's way back stays in the same session).
            if(screen==MenuScreen.Customize&&from!=MenuScreen.Customize)LockerEnter();
            ShowScreen(screen,sandbox);
        }
        // Escape on the main menu: close the update card, else return to the home screen (the source close X); the room
        // lobby stays until Leave Room. The Locker asks before dropping unsaved changes (LockerBack).
        private void MenuBack(){var at=MenuTarget;if(updateOpen&&at==MenuScreen.Home){updateOpen=false;return;}if(at==MenuScreen.Customize){LockerBack();return;}if(at!=MenuScreen.Home&&at!=MenuScreen.Lobby)Go(MenuScreen.Home);}
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
            // The transition's swap and its input hold (NativeMenuTransitionUI.cs); `seen`: this menu was drawn a moment ago.
            bool seen=now-homeSeen<=.25f;MenuTransitionFrame(now,seen);
            if(menuFromMatch){menuFromMatch=false;Go(afterMatch);afterMatch=MenuScreen.Home;}
            // In a room the lobby is home (Customize and Settings stay open over it); a room lost without Leave Room returns
            // to Join (guest) or Create (host), where the reason is shown. Both read where the menu is going (MenuTarget).
            var target=MenuTarget;
            if(InRoom&&target!=MenuScreen.Lobby&&target!=MenuScreen.Customize&&target!=MenuScreen.Settings)Go(MenuScreen.Lobby);
            else if(!InRoom&&target==MenuScreen.Lobby)Go(roomGuest?MenuScreen.Join:MenuScreen.Create);
            // The open Connection report (NativeConnectionReport.cs) blocks everything under its backdrop, as its ::backdrop takes the pointer.
            menuBlockedNow=reportOpen||menuScreen==MenuScreen.Home&&updateOpen&&updateCard.width>0&&U.Hover(updateCard);
            float age=now-menuScreenAt;bool home=menuScreen==MenuScreen.Home;
            MenuShade(1,home);if(!home)MenuMotes(.5f);TopBar(home);Footer();
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
            MenuTransitionGui(now);homeSeen=now;
        }

        // ---- Backdrop ------------------------------------------------------------------------------------------------
        // The cyber shade (owner 2026-10-12: "a cyber theme~ So cyan instead of orange"). Home: dark behind the menu column and
        // open over the rooftop vista, a top band and a soft pool under the hexagon buttons, a deep foot under the footer and a
        // light vignette (every label measured 6.5:1 or better on the concept renders). The panel screens keep the visual
        // revision's side shade, in the void navy.
        private void MenuShade(float strength,bool home=false)
        {
            float W=U.W,H=U.H;
            if(!home)
            {
                U.Fill(new Rect(0,0,W*.34f,H),C.Void(.91f*strength));U.HGradient(new Rect(0,0,W*.34f,H),C.Void(.78f*strength),Color.clear);
                U.Fill(new Rect(W*.34f,0,W*.66f,H),C.Void(.13f*strength));U.HGradient(new Rect(W*.34f,0,W*.44f,H),C.Void(.897f*strength),Color.clear);
                U.Fade(new Rect(0,0,W,150),C.Void(.55f*strength));U.Fade(new Rect(0,H-200,W,200),C.Void(.6f*strength),false);return;
            }
            Ramp(new Rect(0,0,280,H),.9f,.8f,strength,true);Ramp(new Rect(280,0,235,H),.8f,.44f,strength,true);Ramp(new Rect(515,0,265,H),.44f,0,strength,true);
            U.Fade(new Rect(0,0,W,170),C.Void(.62f*strength));
            float x0=HomeX;U.RadialStops(new Rect(W-x0-150-330,70-330,660,660),C.Void(.42f*strength),new[]{0f,.06f,1f},new[]{1f,1f,0f});
            Ramp(new Rect(0,H-230,W,138),0,.55f,strength,false);Ramp(new Rect(0,H-92,W,92),.55f,.9f,strength,false);
            float R=.75f*Mathf.Max(W,H),inner=.35f*Mathf.Min(W,H)/R;U.RadialStops(new Rect(W*.55f-R,H*.45f-R,2*R,2*R),C.Void(.38f*strength),new[]{0f,inner,1f},new[]{0f,0f,1f});
        }
        // A linear ramp of the void from alpha `a` to `b` (left to right, or top to bottom): the lower alpha as a flat fill under
        // a ramp to transparent, so the composite is exactly linear.
        private static void Ramp(Rect r,float a,float b,float k,bool horizontal)
        {
            a*=k;b*=k;float lo=Mathf.Min(a,b),hi=Mathf.Max(a,b);if(lo>0)U.Fill(r,C.Void(lo));if(hi<=lo)return;var c=C.Void((hi-lo)/(1-lo));
            if(horizontal){if(a>b)U.HGradient(r,c,Color.clear);else U.HGradient(r,Color.clear,c);}else U.Fade(r,c,a>b);
        }
        // Data motes drifting up through the shaded side of the panel screens (the home screen's rooftop has its own):
        // deterministic per index, so they never pop between frames.
        private static void MenuMotes(float alpha)
        {
            float W=U.W,H=U.H,t=Time.unscaledTime;
            for(int i=0;i<26;i++)
            {
                float h1=Mathf.Repeat(Mathf.Sin(i*12.9898f)*43758.5453f,1),h2=Mathf.Repeat(Mathf.Sin(i*78.233f)*24634.6345f,1),h3=Mathf.Repeat(Mathf.Sin(i*39.425f)*11753.13f,1);
                float speed=7+h2*16,size=2+Mathf.Floor(h3*3),span=H+60,y=H+20-Mathf.Repeat(t*speed+h1*span,span),x=h1*W*.62f+Mathf.Sin(t*.35f+i)*10;
                float life=Mathf.Clamp01((H+20-y)/160)*Mathf.Clamp01(y/180),a=(.05f+.13f*h3)*life*alpha;
                U.Fill(new Rect(x,y,size,size),i%3==0?C.Cyan(a*1.3f):i%7==0?C.Magenta(a):C.CyanSoft(a*.8f));
            }
        }

        // ---- Wordmark, top bar and footer ----------------------------------------------------------------------------
        // The home column's left edge (and the footer's margins): at least 64, 4.5% of the canvas width.
        private static float HomeX=>Mathf.Max(64,U.W*.045f);
        // The CHIP ARENA wordmark: the brandmark (a cyan tile, a navy inset ring and four pin ticks above and below) and
        // NativeRooftopArt's chamfered monoline capitals (the same strokes the rooftop's holo screens carry), CHIP in ink and
        // ARENA in cyan, each over a soft halo of its own colour; with `age` >= 0 the letters rise in one by one. `height` is the
        // cap height. Returns the width.
        private static readonly string[] wordmarkKeys=new string[128],haloKeys=new string[128];private static float wordmarkStroke=-1,haloStroke=-1;
        private static readonly Dictionary<char,string> wordmarkMarkup=new Dictionary<char,string>(),haloMarkup=new Dictionary<char,string>();
        private static float Wordmark(float x,float y,float height,float alpha,float age)
        {
            float gs=height/NativeRooftopArt.GlyphHeight,bm=height*.72f,by=y+(height-bm)/2;
            U.Glow(new Rect(x,by,bm,bm),C.Cyan(.32f*alpha),3+height*.2f);U.Fill(new Rect(x,by,bm,bm),C.Cyan(alpha));U.Fill(new Rect(x+2,by+2,bm-4,bm-4),C.Navy(alpha));
            float inset=bm*.27f;U.Fill(new Rect(x+inset,by+inset,bm-2*inset,bm-2*inset),C.Cyan(alpha));
            float pin=Mathf.Max(2,bm*.115f),step=(bm-2)/4;for(int k=0;k<4;k++){float px=x+3+k*step;U.Fill(new Rect(px,by-5,pin,2),C.CyanSoft(.85f*alpha));U.Fill(new Rect(px,by+bm+3,pin,2),C.CyanSoft(.85f*alpha));}
            // stroke in glyph units: 2.4 at the home size, never under ~1.5 px; the halo three times as wide
            float stroke=Mathf.Max(2.4f,1.5f/gs),halo=stroke*3.2f;
            if(stroke!=wordmarkStroke){wordmarkStroke=stroke;wordmarkMarkup.Clear();Array.Clear(wordmarkKeys,0,wordmarkKeys.Length);}if(halo!=haloStroke){haloStroke=halo;haloMarkup.Clear();Array.Clear(haloKeys,0,haloKeys.Length);}
            float lx=x+bm+height*.61f,u=0;bool second=false;int n=0;
            foreach(char ch in "CHIP ARENA")
            {
                if(ch==' '){u+=20;second=true;continue;}
                float w=NativeRooftopArt.LetterWidth(ch),e=age<0?1:In(age,.06f+n*.035f,.5f);var r=new Rect(lx+(u-(NativeRooftopArt.GlyphHeight-w)/2)*gs,y+14*gs*(1-e),40*gs,40*gs);
                if(!wordmarkMarkup.TryGetValue(ch,out var svg)){svg=NativeRooftopArt.Letter(ch,stroke);wordmarkMarkup[ch]=svg;wordmarkKeys[ch]="wm-"+ch+"-"+stroke.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);}
                if(!haloMarkup.TryGetValue(ch,out var hsvg)){hsvg=NativeRooftopArt.Letter(ch,halo);haloMarkup[ch]=hsvg;haloKeys[ch]="wmh-"+ch+"-"+halo.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);}
                Color ink=second?C.Cyan():C.Ink(),glow=second?C.Cyan():C.CyanSoft();
                NativeIcons.DrawMarkup(r,haloKeys[ch],hsvg,NativeRooftopArt.GlyphHeight,U.A(glow,(second?.26f:.16f)*alpha*e),192);NativeIcons.DrawMarkup(r,wordmarkKeys[ch],svg,NativeRooftopArt.GlyphHeight,U.A(ink,alpha*e));
                u+=w+13;n++;
            }
            return bm+height*.61f+(u-13)*gs;
        }
        // The top bar of the panel screens: the wordmark at 16 px (the home screen draws its own at 36); the room's
        // connection pill at the right in the lobby. The PROTOTYPE tag lives in the footer.
        private void TopBar(bool home)
        {
            if(home)return;float ww=Wordmark(30,31,16,1,-1);UpdatePill(30+ww+22,27);
            if(menuScreen==MenuScreen.Lobby&&InRoom)ConnectionPill(U.W-30,24);
        }
        // The update waiting, beside the panel screens' wordmark (owner 2026-10-14: "make it more visible when updates are
        // available~ Like make the button pulse"): the Update Game circle lives on the home screen only, so the other screens
        // carry a pill on the same beat. A click takes the player home with the update card open; in a room it only says
        // what is needed (installing waits for the room to close, NativeUpdater.CanInstall). While a download, verification or
        // preparation runs it shows that state steadily instead. Nothing else: no pill while idle, current or off.
        private void UpdatePill(float x,float y)
        {
            var u=Updater;if(u==null)return;var s=u.State;bool ready=s==NativeUpdateState.Available,busy=s==NativeUpdateState.Downloading||s==NativeUpdateState.Verifying||s==NativeUpdateState.Preparing;
            if(!ready&&!busy)return;UpdateBeatAt(ready,out float kick,out float breathe,out float wave);
            string label=ready?(InRoom?"UPDATE READY  ·  LEAVE THE ROOM TO INSTALL":"UPDATE READY"):UpdateChip(u);
            float w=U.Measure(label,10,true,true,.14f).x+44;var r=new Rect(x,y,w,24);bool can=ready&&!InRoom&&!MenuBlocked();float h=U.HoverAmount(9106,r,can);
            U.Glow(r,C.Cyan((ready?.10f+.26f*kick+.12f*breathe:.08f)+.14f*h),10);
            if(wave>=0){float g=8*wave;U.Pill(new Rect(r.x-g,r.y-g,r.width+2*g,r.height+2*g),Color.clear,C.Cyan(.55f*(1-wave)*(1-wave)));}
            U.Pill(r,C.Navy(.92f),U.A(U.Mix(C.CyanSoft(),C.Cyan(),Mathf.Max(h,kick)),.9f));
            NativeIcons.Draw(new Rect(r.x+9,r.y+4+2*kick,16,16),"download",ready?C.Cyan():C.CyanSoft());
            U.Text(new Rect(r.x+30,r.y,w-36,24),label,10,ready?U.Mix(C.Cyan(),Color.white,.5f*kick+.3f*h):C.CyanSoft(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
            if(s==NativeUpdateState.Downloading){float f=Mathf.Clamp01(u.Progress);if(f>0)U.Fill(new Rect(r.x+12,r.yMax-3,(r.width-24)*f,2),C.Cyan(.9f));}
            if(can&&U.Button(r)){Click();if(menuScreen==MenuScreen.Customize&&LockerDirty())LockerBack();else{Go(MenuScreen.Home);updateOpen=true;}}
        }
        // The update's beat (UpdateBeat seconds), shared by the circle and the pill: `kick` jumps to 1 on the beat and decays
        // (a heartbeat), `breathe` is a slow sine, `wave` the sonar ring's progress (-1 for none). Reduced effects keep the
        // breathing glow only: no kick, no ring.
        private const float UpdateBeat=1.6f;
        private void UpdateBeatAt(bool ready,out float kick,out float breathe,out float wave)
        {
            float t=Time.unscaledTime,beat=Mathf.Repeat(t/UpdateBeat,1);bool calm=prefs.reducedEffects;
            kick=ready&&!calm?Mathf.Exp(-6*beat):0;breathe=ready?.5f+.5f*Mathf.Sin(t*Mathf.PI*2/UpdateBeat):0;wave=ready&&!calm?beat:-1;
        }
        // The footer on every menu screen: a hairline, the PROTOTYPE tag, the build and controls at the left and the arena
        // and pilot counts at the right, in mono.
        private void Footer()
        {
            float W=U.W,H=U.H,x0=HomeX,fy=H-40;U.Fill(new Rect(x0,fy-14,W-2*x0,1),C.CyanSoft(.22f));
            float pw=U.Measure("PROTOTYPE",9,true,true,.15f).x+12;var tag=new Rect(x0,fy-9,pw,18);U.Round(tag,C.Navy(.8f),4,C.Cyan(.55f));U.Text(tag,"PROTOTYPE",9,C.Cyan(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.15f);
            string build=Updater?.Installed!=null?"BUILD "+NativeUpdater.Short(Updater.Installed.version):"NATIVE BUILD";
            U.Text(new Rect(x0+pw+14,fy-8,W-2*x0-pw-14,16),build+"   /   DESKTOP CONTROLS",10.5f,C.Ink(.78f),TextAnchor.MiddleLeft,mono:true);
            U.Text(new Rect(x0,fy-8,W-2*x0,16),content.maps.Count(m=>NativeMapRules.ArenaSelectable(m)&&m.rotation)+" ARENAS   ·   2–8 PILOTS",10.5f,C.Ink(.78f),TextAnchor.MiddleRight,mono:true);
        }
        // The Update Game control, a circle beside the menu (main.ts drawUpdateControl's role): a dotted ring turning slowly, a
        // navy disc with the state's icon (the download's percentage while downloading, its ring filling; a spinner while
        // checking, verifying, preparing or restarting) and a two-line state label to its upper left. A click opens the update
        // card (UpdatePanel) beside it and checks again (the automatic check runs once per launch) unless one is running, an
        // update is waiting or the updater is off; the card stays open on its own while an update downloads and installs.
        private void UpdateCircle(Vector2 c,float r,float alpha)
        {
            var u=Updater;var s=u.State;float t=Time.unscaledTime;bool spin=s==NativeUpdateState.Checking||s==NativeUpdateState.Verifying||s==NativeUpdateState.Preparing||s==NativeUpdateState.Restarting;
            bool busy=s==NativeUpdateState.Downloading||s==NativeUpdateState.Verifying||s==NativeUpdateState.Preparing||s==NativeUpdateState.Restarting,live=s==NativeUpdateState.Available||busy,ready=s==NativeUpdateState.Available;
            var hit=new Rect(c.x-r-12,c.y-r-12,2*r+24,2*r+24);bool blocked=MenuBlocked();float h=Mathf.Max(U.HoverAmount(9101,hit,!blocked),updateOpen?1:0);
            // An update waiting beats (owner 2026-10-14: "make the button pulse"): the halo swells on each beat, two sonar rings
            // run out from the disc, the disc kicks a little larger and the download arrow dips, a magenta pip marks it new, and
            // AVAILABLE flashes toward white. Reduced effects keep the breathing halo and the pip.
            UpdateBeatAt(ready,out float kick,out float breathe,out float wave);
            Color accent=s==NativeUpdateState.Error?C.Danger():live?C.Cyan():C.Muted();float swell=r*(2.1f+.5f*kick);
            U.RadialStops(new Rect(c.x-swell,c.y-swell,2*swell,2*swell),U.A(live?C.Cyan():C.Muted(),((live?.20f:.10f)+.08f*h+(ready?.10f+.22f*kick+.10f*breathe:0))*alpha),new[]{0f,.29f,1f},new[]{1f,1f,0f});
            if(wave>=0)for(int k=0;k<2;k++){float p=Mathf.Repeat(wave-.5f*k,1),q=1-(1-p)*(1-p),R=r+6+42*q,th=2.5f;U.RadialStops(new Rect(c.x-R,c.y-R,2*R,2*R),C.Cyan(.6f*(1-p)*(1-p)*alpha),new[]{0f,(R-2*th)/R,(R-th)/R,1f},new[]{0f,0f,1f,0f});}
            for(int k=0;k<48;k++){float a=k/48f*Mathf.PI*2+t*(ready?.3f:.12f),d=k%4==0?2.6f:1.6f;var dot=new Vector2(c.x+Mathf.Cos(a)*(r+9),c.y+Mathf.Sin(a)*(r+9));U.Disc(new Rect(dot.x-d/2,dot.y-d/2,d,d),live?C.CyanSoft((ready?.6f+.4f*Mathf.Max(kick,breathe):.6f)*alpha):C.Muted(.4f*alpha));}
            if(s==NativeUpdateState.Downloading){float p=Mathf.Clamp01(u.Progress);var ring=new Rect(c.x-r-4.5f,c.y-r-4.5f,2*r+9,2*r+9);U.Glow(ring,C.Cyan(.25f*alpha),6);U.Pie(ring,C.Cyan(alpha),p);}
            float rr=r*(1+.06f*kick);var disc=new Rect(c.x-rr,c.y-rr,2*rr,2*rr);U.Disc(disc,C.Void(.95f*alpha));U.RadialStops(disc,U.Hex(0x1e2c60,.95f*alpha),new[]{0f,.75f,1f},new[]{1f,.35f,0f});
            U.RadialStops(disc,U.A(U.Mix(C.CyanSoft(.45f),C.Cyan(),Mathf.Max(h,kick)),alpha),new[]{0f,.94f,.97f,1f},new[]{0f,0f,1f,0f});U.RadialStops(new Rect(c.x-rr+5,c.y-rr+5,2*rr-10,2*rr-10),C.Cyan((.12f+.2f*kick)*alpha),new[]{0f,.94f,.97f,1f},new[]{0f,0f,1f,0f});
            if(spin)for(int i=0;i<8;i++){float a=t*4-i*.32f;var d=new Rect(c.x+Mathf.Cos(a)*(r+3)-1.6f,c.y+Mathf.Sin(a)*(r+3)-1.6f,3.2f,3.2f);U.Disc(d,U.A(accent,alpha*(1-i/8f)));}
            if(s==NativeUpdateState.Downloading)U.Text(disc,Mathf.RoundToInt(Mathf.Clamp01(u.Progress)*100)+"%",15,C.Ink(alpha),TextAnchor.MiddleCenter,bold:true);
            else NativeIcons.Draw(new Rect(c.x-12,c.y-12+3*kick,24,24),s==NativeUpdateState.Current?"check":s==NativeUpdateState.Error?"circle-alert":"download",U.A(live?(ready?U.Mix(C.CyanSoft(),Color.white,.6f*kick):C.CyanSoft()):s==NativeUpdateState.Error?C.Danger():C.Muted(),alpha));
            if(ready){var pip=new Rect(c.x+rr*.7f-6,c.y-rr*.7f-6,12,12);U.Glow(pip,C.Magenta((.35f+.3f*breathe)*alpha),7);U.Disc(pip,C.Magenta(alpha));U.Disc(new Rect(pip.x+3.5f,pip.y+3.5f,5,5),new Color(1,1,1,.85f*alpha));}
            UpdateLines(u,out string top,out string bottom);float lx=c.x-r-14,ly=c.y-r-18;
            U.Text(new Rect(lx-260,ly-22,260,16),top,11,C.Ink(.94f*alpha),TextAnchor.MiddleRight,bold:true,tracking:.2f,shadow:C.Void(.8f*alpha));
            U.Text(new Rect(lx-260,ly-6,260,16),bottom,11,U.A(s==NativeUpdateState.Error?C.Danger():ready?U.Mix(C.Cyan(),Color.white,.55f*kick):live?C.Cyan():C.Ink(.94f),alpha),TextAnchor.MiddleRight,bold:true,tracking:.2f,shadow:C.Void(.8f*alpha));
            U.Polygon(new Rect(lx+4-3*kick,ly+6,10,12),Arrow,new[]{0f,1f},new[]{1f,1f},U.A(accent,(.85f+.15f*kick)*alpha));
            if(U.Button(hit)&&!blocked){updateOpen=!updateOpen;Click();if(updateOpen&&!u.Busy&&(s==NativeUpdateState.Idle||s==NativeUpdateState.Current||s==NativeUpdateState.Error))u.CheckForUpdates();}
            if(busy)updateOpen=true;
            updateAnchor=new Vector2(Mathf.Min(U.W-16,c.x+r+24+UpdateCardWidth),Mathf.Max(70,c.y-r-8));
        }
        private static readonly Vector2[] Arrow={new Vector2(0,.5f),new Vector2(1,0),new Vector2(1,1)};
        // The circle's two-line label: what the updater is doing in two words (the card carries the full status line).
        private static void UpdateLines(NativeUpdater u,out string top,out string bottom)
        {
            switch(u.State)
            {
                case NativeUpdateState.Available:top="UPDATE";bottom="AVAILABLE";return;
                case NativeUpdateState.Downloading:top="DOWNLOADING";bottom="UPDATE";return;
                case NativeUpdateState.Verifying:top="VERIFYING";bottom="UPDATE";return;
                case NativeUpdateState.Preparing:top="PREPARING";bottom="UPDATE";return;
                case NativeUpdateState.Restarting:top="RESTARTING";bottom="GAME";return;
                case NativeUpdateState.Checking:top="CHECKING";bottom="UPDATES";return;
                case NativeUpdateState.Current:top="LATEST";bottom="BUILD";return;
                case NativeUpdateState.Error:top="UPDATE";bottom="CHECK FAILED";return;
                // Idle covers three different outcomes; say which.
                case NativeUpdateState.Idle:if(NotPublished(u)){top="NO UPDATE";bottom="PUBLISHED";}else if((u.Status??"").IndexOf("cancel",StringComparison.OrdinalIgnoreCase)>=0){top="UPDATE";bottom="CANCELLED";}else{top="UPDATE";bottom="GAME";}return;
                default:top="UPDATER OFF";bottom="DETAILS";return;
            }
        }
        // NativeUpdater reports a missing feed (HTTP 404 on latest.json) as Idle with this status.
        private static bool NotPublished(NativeUpdater u)=>u.State==NativeUpdateState.Idle&&(u.Status??"").StartsWith("No Unity update",StringComparison.Ordinal);
        private Vector2 updateAnchor;
        private void UpdateDropdown(){if(Updater!=null&&updateOpen)UpdatePanel(updateAnchor.x,updateAnchor.y);else updateCard=Rect.zero;}
        // While the update card is open, controls underneath it ignore the pointer (IMGUI gives clicks to the first control).
        // Computed once per event at the top of HomeMenu, untransformed, from the card drawn last frame.
        private bool MenuBlocked()=>menuBlockedNow;

        // ---- Home screen ---------------------------------------------------------------------------------------------
        // The redesigned home (owner 2026-10-12, Duelyst-style reference) over the rooftop: the wordmark and its pin rule; the
        // four numbered entries on a hairline divider with their constellation glyphs (keys 1-4); the quiet rows (Developer
        // Sandbox offline and the Catalogue for developers, Quit); the Update Game circle; the hexagon buttons at the top right
        // (Customize, Settings, Account and, for developers, DEV); the Connection report pill at the top centre; and the pilot's
        // nameplate under its seated pilot, which opens Customize. Short canvases scale the column to fit above the footer.
        private void HomeScreen(float age)
        {
            float W=U.W,H=U.H,x0=HomeX;bool dev=account.Developer;HomeKeys();float ce=In(age,0,.26f);
            const float wy=86,gh=36,rh=58;float ry=wy+gh+16,top=ry+56,lastMain=top+rh*3;int quiet=(dev&&!Online?1:0)+(dev?1:0)+1;float bottom=lastMain+rh*.9f+6+34*(quiet-1)+18;
            float fit=Mathf.Clamp((H-70-wy+20)/(bottom-wy+20),.6f,1);U.Push(new Vector2(x0,wy-20),Vector2.zero,fit);
            float ww=Wordmark(x0,wy,gh,ce,age);
            // the chip-pin rule: a hairline with leg ticks, its first run in cyan and its last in magenta
            float rule=ww*In(age,.1f,.5f);U.Fill(new Rect(x0,ry,rule,1),C.CyanSoft(.30f*ce));U.Fill(new Rect(x0,ry-.5f,Mathf.Min(rule,26+10),2),C.Cyan(ce));
            for(float px=x0+48;px<x0+rule;px+=9)U.Fill(new Rect(px,ry+1,1,3),C.CyanSoft(.30f*ce));if(rule>=ww-.5f)U.Fill(new Rect(x0+ww-14,ry-.5f,14,2),C.Magenta(.85f*ce));
            // the entries on their divider
            float dx=x0+50;U.Fill(new Rect(dx,top-18,1,(lastMain-top+36)*In(age,.08f,.4f)),C.CyanSoft(.22f*ce));
            var items=HomeItems;
            for(int i=0;i<items.Length;i++){float e=In(age,.1f+i*.05f,.3f);U.Push(new Vector2(x0,top+i*rh),new Vector2(12*(1-e),0));if(HomeEntry(i,top+i*rh,items[i].Item1,items[i].Item2,items[i].Item3,items[i].Item4==MenuScreen.Boss?"PVE · CO-OP":null,ce*e))Go(items[i].Item4);U.Pop();}
            // .subtle-link's quiet rows: Developer Sandbox (developers, offline), the conversion catalogue (developers only: the
            // source has no such screen and its import notes are not for players) and Quit.
            float qy=lastMain+rh*.9f+6,qe=ce*In(age,.3f,.3f);
            if(dev&&!Online&&QuietRow(ref qy,"sandbox","SANDBOX","DEV",qe))Go(MenuScreen.Play,true);
            if(dev&&QuietRow(ref qy,"catalogue","CATALOGUE","DEV",qe))Go(MenuScreen.Catalogue);
            if(QuietRow(ref qy,"quit","QUIT",null,qe)){Save();Application.Quit();}
            if(Updater!=null)UpdateCircle(new Vector2(x0+500,top+rh*1.5f),36,ce*In(age,.2f,.4f));
            // The update card is drawn outside the fitted column: carry its anchor through the same scale.
            U.Pop();if(Updater!=null){var pivot=new Vector2(x0,wy-20);updateAnchor=pivot+(updateAnchor-pivot)*fit;}
            HexRow(age,ce,out float hexLeft);ReportPill(hexLeft,ce);Nameplate(ce*In(age,.35f,.4f));
        }
        // One numbered entry: its constellation glyph, a 21 px tracked label and its mono key; hover lights a cyan band from
        // the divider with a glowing bar, brightens the glyph and lifts the label to white. Start Expedition carries its PVE
        // line in violet.
        private bool HomeEntry(int index,float y,string glyph,string label,string key,string sub,float alpha)
        {
            float x0=HomeX,ix=x0+2,dx=x0+50,lx=x0+72;var hit=new Rect(ix-6,y-rh2,dx+398-ix+6,rh2*2);bool blocked=MenuBlocked();
            float h=U.HoverAmount(9120+index,hit,!blocked&&alpha>.5f);bool boss=sub!=null;
            if(h>0){U.HGradient(new Rect(dx+1,y-22,420,44),C.Cyan(.20f*h*alpha),Color.clear);U.Glow(new Rect(dx-1,y-22,3,44),C.Cyan(.5f*h*alpha),7);U.Fill(new Rect(dx-1,y-22,3,44),C.Cyan(h*alpha));}
            Color tint=boss?U.Hex(0xb7a3ff,.9f):C.CyanSoft(.7f);Constellation(new Rect(ix,y-13,26,26),glyph,U.Mix(tint,C.Cyan(),h),alpha);
            float ty=boss?y-6:y,shift=3*h;Color ink=U.Mix(C.Ink(.94f),Color.white,h);
            U.Text(new Rect(lx+shift,ty-14,460,28),label,21,U.A(ink,alpha),TextAnchor.MiddleLeft,tracking:.17f,glow:h>0?C.Cyan(.6f*h*alpha):default,shadow:C.Void(.7f*alpha));
            float tw=U.Measure(label,21,false,false,.17f).x;
            if(boss)U.Text(new Rect(lx+1+shift,y+6,300,16),sub,10,U.Hex(0xc4b2ff,alpha),TextAnchor.MiddleLeft,bold:true,tracking:.24f,shadow:C.Void(.7f*alpha));
            U.Text(new Rect(lx+tw+16+shift,ty-7,40,16),key,10,U.A(U.Mix(C.Ink(.78f),C.CyanSoft(),h),alpha),TextAnchor.MiddleLeft,mono:true);
            bool clicked=!blocked&&alpha>.5f&&U.Button(hit);if(clicked)Click();return clicked;
        }
        private const float rh2=26;
        // A quiet row under the entries: a small constellation glyph and a 13.5 px tracked label (the DEV tag in magenta);
        // advances `y` by a row.
        private bool QuietRow(ref float y,string glyph,string label,string tag,float alpha)
        {
            float x0=HomeX,lx=x0+72;var hit=new Rect(x0,y-14,lx-x0+U.Measure(label,13.5f,false,false,.2f).x+(tag!=null?50:12),28);bool blocked=MenuBlocked();
            float h=U.HoverAmount(label.GetHashCode()^7919,hit,!blocked&&alpha>.5f);
            Constellation(new Rect(x0+7,y-8,16,16),glyph,U.Mix(C.Muted(.75f),C.Cyan(),h),alpha);
            float tw=U.Measure(label,13.5f,false,false,.2f).x;U.Text(new Rect(lx+2*h,y-10,300,20),label,13.5f,U.A(U.Mix(C.Ink(.72f),Color.white,h),alpha),TextAnchor.MiddleLeft,tracking:.2f,shadow:C.Void(.6f*alpha));
            if(tag!=null)U.Text(new Rect(lx+tw+12+2*h,y-7,60,14),tag,8.5f,U.Hex(0xff7ddf,alpha),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
            y+=34;bool clicked=!blocked&&alpha>.5f&&U.Button(hit);if(clicked)Click();return clicked;
        }
        // Constellation glyphs (24 x 24, our own): hairlines between nodes at .75 of the colour, the nodes as dots in full.
        private static readonly Dictionary<string,(string lines,string dots)> constellations=Constellations();
        private static Dictionary<string,(string lines,string dots)> Constellations()
        {
            var src=new Dictionary<string,(string l,float[] d)>
            {
                ["play"]=("M4 20L20 4M4 4L20 20M7 13L11 17M13 7L17 11",new float[]{4,20,20,4,4,4,20,20,12,12,7,13,17,11}),
                ["create"]=("M12 4L5 18M12 4L19 18M5 18H19M12 4V12M5 18L12 12L19 18",new float[]{12,4,5,18,19,18,12,12}),
                ["join"]=("M3 12H15M11 8L15 12L11 16M18 4V20M18 4L21 6V18L18 20",new float[]{3,12,15,12,11,8,11,16,18,4,18,20,21,6,21,18}),
                ["boss"]=("M12 3L21 19H3Z M9 13L12 10L15 13L12 16Z",new float[]{12,3,21,19,3,19,12,13}),
                ["sandbox"]=("M10 3V9L5 19H19L14 9V3M7 15H17",new float[]{10,3,14,3,5,19,19,19,10,9,14,9}),
                ["catalogue"]=("M12 6V20M12 6L4 4V18L12 20L20 18V4Z",new float[]{4,4,20,4,4,18,20,18,12,6,12,20}),
                ["quit"]=("M10 4H4V20H10M9 12H20M16 8L20 12L16 16",new float[]{4,4,4,20,20,12,16,8,16,16}),
            };
            var map=new Dictionary<string,(string,string)>();
            foreach(var kv in src)
            {
                var dots=new System.Text.StringBuilder();for(int i=0;i+1<kv.Value.d.Length;i+=2)dots.Append("<circle cx=\"").Append(kv.Value.d[i]).Append("\" cy=\"").Append(kv.Value.d[i+1]).Append("\" r=\"1.55\" fill=\"#fff\"/>");
                map[kv.Key]=("<path d=\""+kv.Value.l+"\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.1\"/>",dots.ToString());
            }
            return map;
        }
        private static void Constellation(Rect r,string id,Color c,float alpha)
        {
            if(!constellations.TryGetValue(id,out var g))return;
            NativeIcons.DrawMarkup(r,"cst-l-"+id,g.lines,24,U.A(c,.75f*alpha));NativeIcons.DrawMarkup(r,"cst-d-"+id,g.dots,24,U.A(c,alpha));
        }
        // Hexagon buttons at the top right (24-unit circumradius on a 48 box, a point up): Customize, Settings, the account
        // (its name once logged in) and, for developers, DEV (which asks for one login first, as the DEV Menu did).
        private const string HexFill="<defs><linearGradient id=\"hx\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"#131a3d\"/><stop offset=\"1\" stop-color=\"#05070f\"/></linearGradient></defs><path d=\"M24 2L43.05 13V35L24 46L4.95 35V13Z\" fill=\"url(#hx)\"/>";
        private const string HexEdge="<path d=\"M24 2L43.05 13V35L24 46L4.95 35V13Z\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.3\"/>";
        private const string HexInner="<path d=\"M24 6L39.6 15V33L24 42L8.4 33V15Z\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1\"/>";
        private void HexRow(float age,float ce,out float left)
        {
            float W=U.W,x0=HomeX;bool dev=account.Developer;string user=account.Account?.username;
            var hexes=new List<(string icon,string label,Action go)>{("palette","CUSTOMIZE",()=>Go(MenuScreen.Customize)),("settings","SETTINGS",()=>Go(MenuScreen.Settings)),("users",user!=null?user.ToUpperInvariant():dev?"DEV ACCOUNT":"LOG IN",()=>Go(MenuScreen.Account))};
            if(dev)hexes.Add(("terminal","DEV",()=>{if(account.Account==null){accountNotice="Log in once to activate registered DEV tools.";Go(MenuScreen.Account);}else Go(MenuScreen.Dev);}));
            const float hr=25,hs=92,hy=54;float hx=W-x0-14-(hexes.Count-1)*hs;left=hx-hr;
            for(int i=0;i<hexes.Count;i++){float e=ce*In(age,.12f+i*.04f,.3f);U.Push(new Vector2(hx,hy),new Vector2(0,-8*(1-e)));if(Hex(new Vector2(hx,hy),hr,hexes[i].icon,hexes[i].label,e,9130+i))hexes[i].go();U.Pop();hx+=hs;}
        }
        private bool Hex(Vector2 c,float r,string icon,string label,float alpha,int id)
        {
            float box=r*48/22;var rect=new Rect(c.x-box/2,c.y-box/2,box,box);var hit=new Rect(c.x-r,c.y-r,2*r,2*r+24);bool blocked=MenuBlocked();float h=U.HoverAmount(id,hit,!blocked&&alpha>.5f);
            U.Glow(new Rect(c.x-r*.8f,c.y-r*.6f,r*1.6f,r*1.6f),new Color(0,0,0,.55f*alpha),14);if(h>0)U.Glow(new Rect(c.x-r*.7f,c.y-r*.7f,r*1.4f,r*1.4f),C.Cyan(.22f*h*alpha),14);
            NativeIcons.DrawMarkup(rect,"hex-fill",HexFill,48,new Color(1,1,1,.86f*alpha));NativeIcons.DrawMarkup(rect,"hex-edge",HexEdge,48,U.A(U.Mix(C.CyanSoft(.6f),C.Cyan(),h),alpha));NativeIcons.DrawMarkup(rect,"hex-inner",HexInner,48,C.Cyan((.16f+.2f*h)*alpha));
            NativeIcons.Draw(new Rect(c.x-11,c.y-11,22,22),icon,U.A(U.Mix(C.Ink(),Color.white,h),alpha));
            string text=Fit(label,9.5f,88,true,.16f);U.Text(new Rect(c.x-60,c.y+r+7,120,16),text,9.5f,U.A(U.Mix(C.Ink(),C.CyanSoft(),h),alpha),TextAnchor.MiddleCenter,bold:true,tracking:.16f,shadow:C.Void(.8f*alpha));
            bool clicked=!blocked&&alpha>.5f&&U.Button(hit);if(clicked)Click();return clicked;
        }
        // The Connection report pill at the top centre (the source's diagnostics, also in Settings), left out when the canvas is
        // too narrow to keep it clear of the hexagons.
        private void ReportPill(float hexLeft,float alpha)
        {
            const string label="CONNECTION REPORT";float tw=U.Measure(label,9.5f,true,false,.16f).x,pw=tw+12+14+16,px=U.W/2-pw/2;if(px+pw>hexLeft-24)return;
            var r=new Rect(px,16,pw,24);bool blocked=MenuBlocked();float h=U.HoverAmount(9140,r,!blocked&&alpha>.5f);
            U.Round(r,C.Navy(.72f*alpha),12,U.A(U.Mix(C.CyanSoft(.32f),C.Cyan(),h),alpha));NativeIcons.Draw(new Rect(px+10,22,12,12),"activity",C.Cyan(alpha));
            U.Text(new Rect(px+28,16,tw+8,24),label,9.5f,U.A(U.Mix(C.Ink(.92f),Color.white,h),alpha),TextAnchor.MiddleLeft,bold:true,tracking:.16f);
            if(!blocked&&alpha>.5f&&U.Button(r)){Click();OpenConnectionReport();}
        }
        // The pilot's nameplate (owner 2026-10-12: kept; it opens Customize): under the seated pilot's projected feet on the
        // rooftop (a fixed spot over the map orbit), a leader line to a rule with the class icon, the name and the weapon and
        // class in the accent colour lifted toward white so it reads on the night.
        private void Nameplate(float alpha)
        {
            if(alpha<=0)return;if(!RooftopFeet(out var feet))feet=new Vector2(U.W*.64f,U.H*.6f);
            var weapon=content.weapons.FirstOrDefault(w=>w.id==prefs.weapon)??content.weapons[0];Color accent=HexColor(prefs.accentColor,C.CyanHex),lifted=U.Mix(accent,Color.white,.45f);
            float nx=feet.x+46,ny=feet.y+18;var hit=new Rect(nx-10,ny-30,240,52);bool blocked=MenuBlocked();float h=U.HoverAmount(9141,hit,!blocked&&alpha>.5f);
            Ramp(new Rect(nx-10,ny-30,192,52),.72f,.55f,alpha,true);Ramp(new Rect(nx+182,ny-30,48,52),.55f,0,alpha,true);
            Color line=U.A(U.Mix(C.CyanSoft(.45f),C.Cyan(),h),alpha);Line(new Vector2(feet.x+10,feet.y+4),new Vector2(nx-6,ny),line);U.Fill(new Rect(nx-6,ny-.5f,196,1),line);
            NativeIcons.Draw(new Rect(nx,ny-25,22,22),NativeIcons.Spec(weapon.id),U.A(accent,alpha));float tx=nx+30;
            U.Text(new Rect(tx,ny-21,220,18),NativeCosmeticRules.Name(prefs.name).ToUpperInvariant(),11.5f,U.A(U.Mix(C.Ink(.94f),Color.white,h),alpha),TextAnchor.MiddleLeft,bold:true,tracking:.22f,shadow:C.Void(.8f*alpha));
            string cls=weapon.@class=="ranged"?"RANGED":weapon.@class=="magic"?"MAGIC":weapon.@class=="melee"?"MELEE":"DEV";
            U.Text(new Rect(tx,ny+4,220,16),weapon.name.ToUpperInvariant()+"  ·  "+cls,9.5f,U.A(lifted,alpha),TextAnchor.MiddleLeft,bold:true,tracking:.2f,shadow:C.Void(.8f*alpha));
            if(!blocked&&alpha>.5f&&U.Button(hit)){Click();Go(MenuScreen.Customize);}
        }
        // A hairline from a to b (virtual px).
        private static void Line(Vector2 a,Vector2 b,Color c,float width=1)
        {var d=b-a;float len=d.magnitude;if(len<.5f||c.a<=0)return;U.Push(a,Vector2.zero,1,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);U.Fill(new Rect(a.x,a.y-width/2,len,width),c);U.Pop();}
        // Keys 1-4 open the four numbered entries.
        // The home entries in order (owner 2026-10-15: "Rename Boss Test on main menu to "Start Expedition" - Place this as
        // top item on menu."): the PvE entry first, then the versus rooms; the 1-4 keys follow the order.
        private static readonly (string,string,string,MenuScreen)[] HomeItems={("boss","START EXPEDITION","01",MenuScreen.Boss),("play","PLAY VS BOTS","02",MenuScreen.Play),("create","CREATE ROOM","03",MenuScreen.Create),("join","JOIN ROOM","04",MenuScreen.Join)};
        private void HomeKeys()
        {
            var e=Event.current;if(e.type!=EventType.KeyDown||GUIUtility.keyboardControl!=0||updateOpen)return;
            int k=e.keyCode>=KeyCode.Alpha1&&e.keyCode<=KeyCode.Alpha4?e.keyCode-KeyCode.Alpha1:e.keyCode>=KeyCode.Keypad1&&e.keyCode<=KeyCode.Keypad4?e.keyCode-KeyCode.Keypad1:-1;if(k<0)return;
            e.Use();Click();Go(HomeItems[k].Item4);
        }
        // .btn / .btn.primary in the cyber palette: icon, label and an optional mono key at the right; hover lifts 1 px and
        // lights the edge cyan, press scales to .985; the primary is cyan with void ink, a cyan underglow and a sheen.
        private bool MenuButton(Rect r,string icon,string text,string key=null,bool primary=false,bool enabled=true,float alpha=1,float size=15,bool center=false)
        {
            int id=text.GetHashCode()^((int)r.x*73856093)^((int)r.y*19349663);bool blocked=MenuBlocked();float h=U.HoverAmount(id,r,enabled&&!blocked);
            bool press=enabled&&!blocked&&U.Hover(r)&&Input.GetMouseButton(0);float a=alpha*(enabled?1:.45f);var d=new Rect(r.x,r.y-h,r.width,r.height);
            if(press){float s=.985f;d=new Rect(d.center.x-d.width*s/2,r.center.y-r.height*s/2,d.width*s,d.height*s);}
            Color fill=primary?U.Mix(C.Cyan(),C.CyanSoft(),h):U.Mix(C.Indigo(.92f),U.Hex(0x1b2552,.95f),h),edge=primary?C.CyanSoft():U.Mix(C.Edge(),C.Cyan(),h),ink=primary?C.Void():C.Ink();
            if(primary&&h>0)U.Glow(new Rect(d.x+6,d.y+10,d.width-12,d.height-8),C.Cyan(.45f*h*a),16);
            U.Round(d,U.A(fill,a),4,U.A(edge,a));if(!primary&&h>0)U.Fill(new Rect(d.x+1,d.y+1,3,d.height-2),C.Cyan(h*a));if(primary&&enabled)Sheen(d,a);
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
            // The cyber panel (the HUD's holographic chrome, as the pause menu wears it): navy glass over the blurred frame,
            // chamfered corners, scanlines, the cyan run along the top and brackets; the room's shell a shade deeper.
            U.Glow(new Rect(panel.x,panel.y+30,panel.width,panel.height),new Color(0,0,0,.45f*e),60);U.Backdrop(panel,0,16,e);
            NativeHudChrome.Panel(panel,e,default,14,NativeHudChrome.Corners.Diagonal,true,lobby?.96f:.94f);
            U.Text(new Rect(panel.x+pad,panel.y+pad-2,pw-120,16),eyebrow,12,C.CyanSoft(.9f*e),TextAnchor.MiddleLeft,bold:true,tracking:lobby?.18f:.2f);
            U.Text(new Rect(panel.x+pad,panel.y+pad+20,pw-120,38),title,30,C.Ink(e),TextAnchor.MiddleLeft,bold:true,tracking:-.03f);
            // The close X in a small hexagon (the home screen's buttons), lit cyan on hover.
            bool closed=false;if(close){var x=new Rect(panel.xMax-pad-40,panel.y+pad-4,40,40);float h=U.HoverAmount(9103,x);float box=18*48/22f;var hx=new Rect(x.center.x-box/2,x.center.y-box/2,box,box);
                NativeIcons.DrawMarkup(hx,"hex-fill",HexFill,48,new Color(1,1,1,.9f*e));NativeIcons.DrawMarkup(hx,"hex-edge",HexEdge,48,U.A(U.Mix(C.Edge(),C.Cyan(),h),e));if(h>0)U.Glow(new Rect(x.x+8,x.y+8,24,24),C.Cyan(.2f*h*e),10);
                NativeIcons.Draw(new Rect(x.center.x-9,x.center.y-9,18,18),"x",U.A(U.Mix(C.Ink(),Color.white,h),e));closed=U.Button(x);if(closed)Click();}
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
            U.Fill(new Rect(x,y-20,iw,1),C.Edge(.9f*l.Enter));U.Fill(new Rect(x,y-20,28*l.Enter,1),C.Cyan(.8f*l.Enter));
            for(int i=0;i<items.Length;i++)if(MenuButton(new Rect(x+i*(w+gap),y,w,48),items[i].icon,items[i].label,null,items[i].primary,items[i].enabled,l.Enter,15,true))hit=i;
            return hit;
        }
        // A one-line status under the actions' rule (errors in the source's .connection-error red).
        private static void ScreenStatus(ScreenLayout l,string text,bool error=false)
        {if(string.IsNullOrEmpty(text))return;var p=l.Panel;var line=U.Wrap(text,13,p.width-64,false,1);U.Text(new Rect(p.x+32,p.yMax-30-48-20-24,p.width-64,18),line.Count>0?line[0]:text,13,error?U.Hex(0xff8aa0,l.Enter):C.Muted(l.Enter),TextAnchor.MiddleLeft);}
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
            if(ScreenActions(l,("play",playSandbox?"Start Sandbox":"Start Match",true,server==null&&(!playSandbox||account.Developer)&&!blocked))==0){prefs.pve=false;sandboxWanted=playSandbox&&account.Developer;RequestStage(sandboxWanted?StageLoadKind.Sandbox:StageLoadKind.Match,StartMatch);}
        }
        private void BossScreen(float age)
        {
            var l=BeginScreen("PVE · EXPEDITION","Start Expedition",650,age,true,head:122);if(l.Closed){Go(MenuScreen.Home);return;}
            BossCard(new Rect(l.Panel.x+32,l.Panel.y+104,l.Panel.width-64,108),l.Enter);
            ScreenBody(l,()=>BossBody());if(l.Card.width>0)PilotCard(l.Card,"YOUR PILOT",l.Enter,"Up to four pilots: party bots fill the empty seats.");
            if(!string.IsNullOrEmpty(expeditionError))ScreenStatus(l,"Expedition could not start: "+expeditionError,true);
            int a=account.Developer?ScreenActions(l,("play","Start Boss Fight",true,server==null),("network","Host Co-op Room",false,true),("play","DEV Expedition M1",false,!Online)):ScreenActions(l,("play","Start Boss Fight",true,server==null),("network","Host Co-op Room",false,true));
            if(a==2){RequestStage(StageLoadKind.Expedition,StartExpeditionM1);return;}
            if(a==0){prefs.pve=true;sandboxWanted=false;RequestStage(StageLoadKind.Boss,StartMatch);}else if(a==1){prefs.pve=true;Go(MenuScreen.Create);}
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
            U.Glow(new Rect(r.x,r.y+18,r.width,r.height),new Color(0,0,0,.33f*e),45);U.Round(r,C.Panel(e),16,C.Edge(e));
            U.RadialStops(new Rect(r.x+r.width*.05f,r.y+r.height*.06f,r.width*.9f,r.height*.72f),U.A(accent,.2f*e),new[]{0f,.64f,1f},new[]{1f,0f,0f});
            U.Text(new Rect(r.x+22,r.y+20,60,16),"01",12,C.Muted(e),TextAnchor.MiddleLeft,mono:true);U.Text(new Rect(r.x,r.y+20,r.width-22,16),tag,11,C.Cyan(e),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.16f);
            float y=PilotStage(r,accent,body,weapon,e,t,note);
            string name=NativeCosmeticRules.Name(prefs.name);float ns=Mathf.Clamp(r.width*.075f,22,30);
            U.Text(new Rect(r.x+16,y,r.width-32,ns*1.3f),name,ns,U.Hex(0xf4f4f5,e),TextAnchor.MiddleCenter,bold:true);y+=ns*1.3f+4;
            U.Text(new Rect(r.x+16,y,r.width-32,18),weapon.name.ToUpperInvariant(),13,U.A(U.Mix(tone,Color.white,.25f),e),TextAnchor.MiddleCenter,bold:true,tracking:.12f);y+=20;
            float under=60+30*(.5f+.5f*Mathf.Sin(t*1.6f));U.Fill(new Rect(r.center.x-under/2,y+2,under,2),U.A(tone,.85f*e));y+=12;
            U.Text(new Rect(r.x+16,y,r.width-32,16),ClassLabel(weapon.@class),10,U.Hex(0xa6a7ad,e),TextAnchor.MiddleCenter,mono:true,tracking:.1f);y+=30;
            float sw=(r.width-44-12)/2;for(int i=0;i<2;i++){var s=new Rect(r.x+22+i*(sw+12),y,sw,30);U.Round(s,C.Navy(.8f*e),6,C.Edge(.8f*e));U.Disc(new Rect(s.x+9,s.y+8,14,14),U.A(i==0?body:accent,e));U.Text(new Rect(s.x+30,s.y,s.width-34,s.height),i==0?"BODY":"ACCENT",10,U.Hex(0xc0c1ca,e),TextAnchor.MiddleLeft,mono:true,tracking:.12f);}y+=44;
            foreach(var (k,v) in new[]{("HEADWEAR",prefs.hat),("KILL EFFECT",prefs.killEffect),("INTRO",prefs.overrideIntro&&NativeIntroRules.Pickable(prefs.intro)?prefs.intro:"weapon-signature")})
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
            // The sandbox tools moved to the DEV panel overlay (NativeDevPanel.cs).
            if(pauseScreen==PauseScreen.Sandbox)pauseScreen=PauseScreen.Menu;
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
            // DEV Tools (NativeDevPanel.cs): the offline Developer Sandbox's tools, and with Settings · Developer · DEV Abuse any match's.
            if(DevPanelAvailable)items.Add(("flask-conical","DEV Tools",false,true,()=>DevOpen(true)));
            if(!Online&&!lost)items.Add(("rotate-cw",ExpeditionM1?"Restart Expedition M1":"Restart Match",false,true,()=>{if(ExpeditionM1)RequestStage(StageLoadKind.Expedition,StartExpeditionM1);else RequestStage(StageKindOf(arena),StartMatch);}));
            if(!lost)items.Add(("undo-2","Return to Lobby",false,(Hosting||!Online)&&!returning,()=>{if(!Online)afterMatch=OfflineLobby();RequestLobbyReturn();menu=false;}));
            items.Add(("log-out","Return to Main Menu",lost,true,()=>{afterMatch=lost?MenuScreen.Join:MenuScreen.Home;RequestStage(StageLoadKind.Return,()=>{if(Online)LeaveRoom();else ReturnToLobby();});}));
            string hints="WASD move · double-tap W sprint · LMB fire / attack · RMB block · Q / E abilities · R reload · Shift roll · Space jump · V camera · X shoulder · Tab lock-on / board · G scores · Esc menu";
            string diag=content.Map(arena.MapId).name+"  ·  SET "+match.Set+" · ROUND "+match.Round+"  ·  "+match.Phase+"  ·  "+arena.Bullets.Count+" projectiles  ·  step "+stepMs.ToString("0.00")+" ms  ·  "+presentation.RenderingMode;
            float pw=Mathf.Min(520,W-56),iw=pw-64;var statusLines=U.Wrap(status,16,iw);var hintLines=U.Wrap(hints,13,iw);var diagLines=MonoLines(diag,11,iw);bool guest=Online&&!Hosting;
            float ph=108+statusLines.Count*16*1.6f+16+items.Count*56-8+(guest?24:0)+20+hintLines.Count*13*1.55f+8+diagLines.Count*16+30,e=In(age,0,.26f);
            // The source's .panel scrolls past calc(100dvh - 158px); this one cannot, so on a short canvas (a large interface
            // scale on a small window) it scales about its top corner (top centre in the sandbox) to end 24 above the bottom,
            // as HomeScreen fits its block. The top stays at least 80 down, under the lobby-return banner. Push scales the
            // buttons' hit areas with them.
            float fit=Mathf.Min(1,(H-80-24)/ph);
            // The in-match panel in the cyber HUD chrome (NativeHudChrome); the buttons stay the shared MenuButton.
            var p=new Rect(arena.Sandbox?(W-pw)/2:Mathf.Max(28,W*.05f),Mathf.Max(80,(H-ph*fit)/2)+10*(1-e),pw,ph);U.Push(new Vector2(arena.Sandbox?p.center.x:p.x,p.y),Vector2.zero,fit);
            U.Glow(new Rect(p.x,p.y+30,p.width,p.height),new Color(0,0,0,.45f*e),60);U.Backdrop(p,0,16,e);NativeHudChrome.Panel(p,e,default,14,NativeHudChrome.Corners.Diagonal,true,.94f);
            U.Text(new Rect(p.x+32,p.y+30,iw,16),"CHIP ARENA",12,NativeCyberTheme.CyanSoft(e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);U.Text(new Rect(p.x+32,p.y+52,iw,38),"Match menu",30,NativeCyberTheme.Ink(e),TextAnchor.MiddleLeft,bold:true,tracking:-.03f);
            float y=p.y+108;foreach(var s in statusLines){U.Text(new Rect(p.x+32,y,iw,16*1.6f),s,16,NativeCyberTheme.Muted(e),TextAnchor.MiddleLeft);y+=16*1.6f;}y+=16;
            // A guest's note sits under Return to Lobby, as in the source's menu list.
            Action chosen=null;for(int i=0;i<items.Count;i++){float be=In(age,i*.03f,.3f);var r=new Rect(p.x+32,y,iw,48);U.Push(r.center,new Vector2(0,8*(1-be)));if(MenuButton(r,items[i].icon,items[i].label,null,items[i].primary,items[i].enabled,e*be))chosen=items[i].act;U.Pop();y+=56;
                if(guest&&items[i].label=="Return to Lobby"){U.Text(new Rect(p.x+32,y-4,iw,18),"The host can return everyone to the lobby.",13,NativeCyberTheme.Muted(e*be),TextAnchor.MiddleLeft);y+=24;}}y-=8;y+=20;
            foreach(var s in hintLines){U.Text(new Rect(p.x+32,y,iw,13*1.55f),s,13,NativeCyberTheme.Muted(e),TextAnchor.MiddleLeft);y+=13*1.55f;}y+=8;
            foreach(var s in diagLines){U.Text(new Rect(p.x+32,y,iw,16),s,11,NativeCyberTheme.Muted(.8f*e),TextAnchor.MiddleLeft,mono:true);y+=16;}
            U.Pop();if(chosen!=null){pauseScreen=PauseScreen.Menu;chosen();}
        }
        // Greedy lines of `  ·  `-separated parts measured in the mono face they are drawn in (not cached: the values change).
        private static List<string> MonoLines(string text,float size,float width)
        {
            var lines=new List<string>();string line="";foreach(var part in text.Split(new[]{"  ·  "},StringSplitOptions.None)){string next=line.Length==0?part:line+"  ·  "+part;if(line.Length>0&&U.Measure(next,size,false,true).x>width){lines.Add(line);line=part;}else line=next;}
            if(line.Length>0)lines.Add(line);return lines;
        }
    }
}
