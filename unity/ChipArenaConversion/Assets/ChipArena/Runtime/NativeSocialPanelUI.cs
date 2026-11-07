using System;
using System.Collections.Generic;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
using C=ChipArena.UnityConversion.NativeCyberTheme;
namespace ChipArena.UnityConversion
{
    // The Online Players chip and panel (docs/ONLINE-INVITES.md; presence and invitations are NativeAccountSocial.cs, handed over
    // for presentation: "the functional panel uses existing IMGUI controls. It needs the normal visual polish"). Owner 2026-11-03:
    // "can we fix the online players button? (Used for inviting) Style it a lot better and apply animations". The old button sat
    // at a fixed physical-pixel spot under the home screen's hexagons, clipped by their labels, over the default IMGUI skin.
    // Now, kit-drawn in the cyber palette on the virtual canvas:
    // - the chip: under the hexagon row on the home screen (its right edge on theirs), left of the connection pill in a room's
    //   lobby, else the panel screens' and pause menu's top-right corner. A breathing presence light with a sonar ring, ONLINE and
    //   the count, the invitations' magenta badge beating while any wait (a burst when one arrives), a chevron that turns as the
    //   panel opens; it drops in when the menu shows, lifts and lights on hover, presses in;
    // - the panel: navy glass under the chip (a caret to it), dropping open (and folding away) on the panel ease; a header with
    //   the busy spinner and close; the account service's status; the invitations as cards (initials, who, a countdown draining
    //   to their two-minute expiry, Accept / Decline; a new one slides in with a flash); the online players as rows staggered in
    //   (initials with a live light, Invite turning to SENT with a pop once sent; the hint when there is no online room to invite
    //   to); scrolled by the wheel inside its clip, with a thin thumb.
    // It opens and closes from the chip, its close button, a click outside it or Escape (SocialEscape, before the menu's own
    // Back). While it is open or hovered the menu under it is blocked (MenuBlocked). Actions are the existing ones.
    //
    // The invitation pop-up (owner 2026-11-07: "This will show a notification when players get invited", then "Pop-up with Accept"; docs/ONLINE-INVITES.md):
    // a card under the chip's corner when an invitation waits, on the menus and the end-of-match screens, never in a fight. Who, the countdown, Accept and
    // Decline, the same actions and the same rule as the panel's card (outside a room or match Accept joins; inside one it says so and waits); its close
    // only puts it away, and the invitation stays on the chip's badge. One at a time, the count of the others opening the panel.
    public sealed partial class ConversionGame
    {
        private bool socialOpen;
        private float socialOpenAt=-99,socialCloseAt=-99,socialShownAt=-99,socialSeenAt=-99,socialPingAt=-99,socialStatusAt=-99,socialScroll;
        private Rect socialChipRect,socialPanelRect;private bool socialPanelShown;private string socialStatusWas="";private int socialInvitesWas;
        private readonly Dictionary<string,float> socialInviteSeen=new Dictionary<string,float>(),socialSent=new Dictionary<string,float>();private readonly List<string> socialStale=new List<string>();
        private bool CanAcceptInvite=>arena==null&&!Hosting&&server==null&&connecting==null&&relayConnecting==null&&!StageInputHeld;
        private bool SocialCanInvite=>relayHost!=null&&!relayHost.Closed&&!UseManagedRelay&&arena==null&&!deploymentLocked;
        private const string SocialMail="<path d=\"M3.5 6.5H20.5V17.5H3.5Z\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2\"/><path d=\"M4 7.5L12 13L20 7.5\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2\"/>";
        private const string SocialSend="<path d=\"M21 3L10.5 13.5M21 3L14.5 21L10.5 13.5L3 9.5Z\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2\"/>";
        private const float SocialWidth=372,SocialChipH=30,SocialHeadH=54,SocialRowH=44,SocialCardH=80,SocialToastH=94;
        private NativePlayerInvite socialToast;private float socialToastAt=-99,socialToastOutAt=-1,socialToastSeen=-99;private Rect socialToastRect;private string socialToastJoining;
        private readonly HashSet<string> socialToastGone=new HashSet<string>();

        /// <summary>Update (before the menu's Back): Escape closes the open panel instead.</summary>
        private bool SocialEscape(){if(!socialOpen||!menu)return false;SocialClose();return true;}
        private void SocialClose(){if(!socialOpen)return;socialOpen=false;socialCloseAt=Time.unscaledTime;}
        private void SocialToggle(){float now=Time.unscaledTime;socialOpen=!socialOpen;if(socialOpen){socialOpenAt=now;socialScroll=0;}else socialCloseAt=now;}
        /// <summary>MenuBlocked: the pointer is over the chip or the shown panel (the menu under them takes no hover or click).</summary>
        private bool SocialCovers()=>Time.unscaledTime-socialSeenAt<.2f&&(U.Hover(socialChipRect)||socialPanelShown&&U.Hover(socialPanelRect))||Time.unscaledTime-socialToastSeen<.2f&&U.Hover(socialToastRect);
        // How open the panel is (0..1): the panel ease in, a quicker fold out.
        private float SocialOpenAmount(float now)=>socialOpen?U.PanelIn(now-socialOpenAt):1-M.Bezier(.4f,0,1,1,M.Clamp01((now-socialCloseAt)/.16f));

        private void SocialGui()
        {
            SocialToastGui();
            if(account?.Account==null||dedicated||prefs==null||!menu||StageInputHeld||reportOpen){socialPanelShown=false;return;}
            var saved=GUI.matrix;
            try
            {
                float now=Time.unscaledTime;if(now-socialSeenAt>.5f){socialShownAt=now;socialInviteSeen.Clear();socialInvitesWas=-1;}socialSeenAt=now;
                SocialTrack(now);var anchor=SocialAnchor();
                float open=SocialOpenAmount(now);socialPanelShown=open>.01f;
                if(socialPanelShown)SocialPanel(anchor,open,now);
                SocialChip(anchor,open,now);
                // A click anywhere else closes the open panel (the click still reaches what it was on).
                var e=Event.current;if(socialOpen&&e.rawType==EventType.MouseDown&&!U.Hover(socialChipRect)&&!U.Hover(socialPanelRect))SocialClose();
            }
            finally{GUI.enabled=true;GUI.matrix=saved;}
        }
        // New invitations (a burst on the chip and the card's slide), the status line's changes, sent marks expiring.
        private void SocialTrack(float now)
        {
            int live=0;foreach(var i in account.Invitations){if(i==null||string.IsNullOrEmpty(i.id))continue;live++;if(!socialInviteSeen.ContainsKey(i.id)){socialInviteSeen[i.id]=socialInvitesWas<0?now-9:now;if(socialInvitesWas>=0)socialPingAt=now;}}
            socialInvitesWas=live;
            if(account.SocialStatus!=socialStatusWas){socialStatusWas=account.SocialStatus;socialStatusAt=now;}
            if(Time.frameCount%120!=0)return;socialStale.Clear();foreach(var p in socialSent)if(now-p.Value>8)socialStale.Add(p.Key);foreach(var k in socialStale)socialSent.Remove(k);
            if(socialInviteSeen.Count>40)socialInviteSeen.Clear();
        }
        // The chip's right edge and top (virtual px).
        private Vector2 SocialAnchor()
        {
            if(arena==null&&menuScreen==MenuScreen.Home)return new Vector2(U.W-HomeX+8,108);
            if(arena==null&&menuScreen==MenuScreen.Lobby&&InRoom)return new Vector2(U.W-30-ConnectionPillWidth(ConnectionPillText(out _))-12,27);
            return new Vector2(U.W-30,27);
        }

        // ---- the chip ----
        private void SocialChip(Vector2 anchor,float open,float now)
        {
            int online=0,invites=0;foreach(var p in account.OnlinePlayers)if(p!=null&&!string.IsNullOrEmpty(p.id))online++;foreach(var i in account.Invitations)if(i!=null&&!string.IsNullOrEmpty(i.id))invites++;
            const string label="ONLINE";string count=online.ToString(),badge=invites.ToString();
            float lw=U.Measure(label,9.5f,true,false,.2f).x,cw=U.Measure(count,11,true,true).x,bw=invites>0?U.Measure(badge,10,true,true).x+32:0;
            float w=16+8+10+lw+8+cw+14+(bw>0?bw+8:0)+12+12;var r=new Rect(anchor.x-w,anchor.y,w,SocialChipH);socialChipRect=r;
            float enter=M.Bezier(.2f,.8f,.2f,1,M.Clamp01((now-socialShownAt)/.38f)),h=U.HoverAmount(9701,r,true),lit=Mathf.Max(h,open);bool press=U.Hover(r)&&Input.GetMouseButton(0);
            // The invitations' beat (a heartbeat while any wait) and the burst when one arrives.
            float beat=Mathf.Repeat(now,1.8f),kick=invites>0?Mathf.Exp(-beat*7):0,ping=Mathf.Exp(-(now-socialPingAt)*3.2f);
            U.Push(r.center,new Vector2(0,-10*(1-enter)-h),press?.97f:1);
            float a=enter;
            U.Glow(new Rect(r.x+6,r.y+10,r.width-12,r.height-6),new Color(0,0,0,.55f*a),12);
            if(lit>0)U.Glow(r,C.Cyan(.16f*lit*a),12);if(ping>.01f)U.Glow(r,C.Magenta(.5f*ping*a),18);
            U.RoundGradient(r,U.Mix(C.Navy(.94f),U.Hex(0x16204a,.96f),lit),C.Void(.94f),15,U.A(U.Mix(C.CyanSoft(.42f),C.Cyan(.95f),lit),a));
            if(ping>.01f){float g=10*(1-ping);U.Round(new Rect(r.x-g,r.y-g,r.width+2*g,r.height+2*g),Color.clear,15,C.Magenta(.7f*ping*a));}
            // The presence light: breathing, a sonar ring every 2.6 s (dim and still with no one online).
            var dot=new Vector2(r.x+16+4,r.center.y);Color live=online>0?C.Good():C.Dim();float breathe=.5f+.5f*Mathf.Sin(now*2.4f);
            if(online>0){float s=Mathf.Repeat(now,2.6f)/1.2f;if(s<1){float rr=4+9*s;U.Round(new Rect(dot.x-rr,dot.y-rr,2*rr,2*rr),Color.clear,8,U.A(live,.55f*(1-s)*(1-s)*a));}U.Glow(new Rect(dot.x-4,dot.y-4,8,8),U.A(live,(.35f+.3f*breathe)*a),6);}
            U.Disc(new Rect(dot.x-4,dot.y-4,8,8),U.A(live,a));
            float x=r.x+16+8+10;U.Text(new Rect(x,r.y,lw+4,r.height),label,9.5f,U.A(U.Mix(C.Ink(.88f),Color.white,lit),a),TextAnchor.MiddleLeft,bold:true,tracking:.2f);x+=lw+8;
            U.Text(new Rect(x,r.y,cw+4,r.height),count,11,U.A(U.Mix(C.Cyan(),Color.white,.25f*lit),a),TextAnchor.MiddleLeft,bold:true,mono:true);x+=cw+14;
            // The invitations' badge: magenta, its envelope and count, beating; a ring off each beat.
            if(bw>0)
            {
                var b=new Rect(x,r.y+6,bw,r.height-12);float s=1+.14f*kick+.25f*ping;
                if(beat<.9f){float k2=beat/.9f,g=7*k2;U.Pill(new Rect(b.x-g,b.y-g,b.width+2*g,b.height+2*g),Color.clear,C.Magenta(.5f*(1-k2)*(1-k2)*a));}
                U.Push(b.center,Vector2.zero,s);U.Glow(b,C.Magenta((.25f+.35f*kick)*a),8);U.Pill(b,C.Magenta(.95f*a),U.Hex(0xffc2ec,.6f*a));
                NativeIcons.DrawMarkup(new Rect(b.x+8,b.center.y-6.5f,13,13),"social-mail",SocialMail,24,new Color(1,1,1,a));
                U.Text(new Rect(b.x+24,b.y,bw-28,b.height),badge,10,new Color(1,1,1,a),TextAnchor.MiddleLeft,bold:true,mono:true);U.Pop();x+=bw+8;
            }
            // The chevron, turned up as the panel opens.
            var cc=new Vector2(r.xMax-18,r.center.y+.5f);Color chev=U.A(U.Mix(C.CyanSoft(.7f),C.Cyan(),lit),a);
            U.Push(cc,Vector2.zero,1,180*open);Line(cc+new Vector2(-4,-2),cc+new Vector2(0,2),chev,1.5f);Line(cc+new Vector2(0,2),cc+new Vector2(4,-2),chev,1.5f);U.Pop();
            U.Pop();
            if(enter>.6f&&U.Button(r))SocialToggle();
        }

        // ---- the panel ----
        private void SocialPanel(Vector2 anchor,float open,float now)
        {
            var acc=account;bool canInvite=SocialCanInvite,canAccept=CanAcceptInvite;
            int invites=0,players=0;foreach(var i in acc.Invitations)if(i!=null&&!string.IsNullOrEmpty(i.id))invites++;foreach(var p in acc.OnlinePlayers)if(p!=null&&!string.IsNullOrEmpty(p.id))players++;
            string status=acc.SocialStatus??"";var statusLines=status.Length>0?U.Wrap(status,10.5f,SocialWidth-64,false,3):null;
            // The body's height: the status, the invitations (and the note when they cannot be taken), the players (the hint, the
            // rows or the empty line).
            float statusH=statusLines!=null?16+statusLines.Count*15:0;
            float invitesH=invites>0?28+invites*(SocialCardH+8)+(!canAccept?24:0)+6:0;
            float playersH=28+(!canInvite?42:0)+(players>0?players*SocialRowH:56)+8;
            float body=statusH+invitesH+playersH,top=anchor.y+SocialChipH+12,maxH=Mathf.Max(160,U.H-top-60);
            float ph=Mathf.Min(SocialHeadH+body+8,maxH);var r=new Rect(anchor.x-SocialWidth,top,SocialWidth,ph);socialPanelRect=r;
            // (Slid and faded, never squashed: a non-uniform scale tears the kit's nine-slices and smears the text mid-animation.)
            U.Push(new Vector2(r.center.x,r.y),new Vector2(0,-14*(1-open)));
            U.Panel(r,open,C.Panel(.96f),C.Edge(.9f));
            // The caret to the chip's chevron and the cyan accent along the top.
            float cx=anchor.x-18;U.Polygon(new Rect(cx-8,r.y-7,16,8),new[]{new Vector2(.5f,0),new Vector2(1,1),new Vector2(0,1)},new[]{0f,1f},new[]{1f,1f},C.Panel(.96f*open));
            Line(new Vector2(cx-8,r.y),new Vector2(cx,r.y-7),C.Edge(.9f*open));Line(new Vector2(cx,r.y-7),new Vector2(cx+8,r.y),C.Edge(.9f*open));
            U.HGradient(new Rect(r.x+10,r.y+1,(r.width-20)*.5f,2),Color.clear,C.Cyan(.75f*open));U.HGradient(new Rect(r.center.x,r.y+1,(r.width-20)*.5f,2),C.Cyan(.75f*open),Color.clear);
            SocialHeader(r,players,open,now);
            // The body: clipped and scrolled by the wheel.
            var view=new Rect(r.x+1,r.y+SocialHeadH,r.width-2,r.height-SocialHeadH-4);float maxScroll=Mathf.Max(0,body+4-view.height);
            var ev=Event.current;if(ev.type==EventType.ScrollWheel&&U.Hover(view)&&maxScroll>0){socialScroll=Mathf.Clamp(socialScroll+ev.delta.y*18,0,maxScroll);ev.Use();}socialScroll=Mathf.Clamp(socialScroll,0,maxScroll);
            U.BeginBand(view);
            float y=view.y-socialScroll,x=r.x+16,w=r.width-32;int row=0;
            if(statusLines!=null)
            {
                float se=M.Clamp01((now-socialStatusAt)/.3f);bool bad=status.IndexOf("unavailable",StringComparison.OrdinalIgnoreCase)>=0||status.IndexOf("Invalid",StringComparison.OrdinalIgnoreCase)>=0||status.IndexOf("need",StringComparison.OrdinalIgnoreCase)>=0;
                Color tone=bad?C.Warn():C.CyanSoft();var s=new Rect(x,y+6,w,statusH-8);U.Round(s,U.A(tone,.08f*open*se),6,U.A(tone,.28f*open*se));
                NativeIcons.Draw(new Rect(s.x+9,s.y+(s.height-14)/2,14,14),bad?"circle-alert":"check",U.A(tone,open*se));
                for(int l=0;l<statusLines.Count;l++)U.Text(new Rect(s.x+30,s.y+4+l*15,s.width-38,15),statusLines[l],10.5f,U.A(C.Ink(.88f),open*se),TextAnchor.MiddleLeft);
                y+=statusH;
            }
            if(invites>0)
            {
                SocialSection(new Rect(x,y,w,28),"INVITATIONS",invites,C.Magenta(),open);y+=28;
                foreach(var inv in acc.Invitations)
                {
                    if(inv==null||string.IsNullOrEmpty(inv.id))continue;SocialInvite(new Rect(x,y,w,SocialCardH),view,inv,canAccept,open,now,row++);y+=SocialCardH+8;
                }
                if(!canAccept){SocialNote(new Rect(x,y-2,w,22),"Leave your current room or match before accepting.",C.Warn(),open);y+=24;}
                y+=6;
            }
            SocialSection(new Rect(x,y,w,28),"PLAYERS ONLINE",players,C.Cyan(),open);y+=28;
            if(!canInvite)
            {
                var hint=new Rect(x,y+2,w,34);float he=SocialRowIn(now,row++);U.Round(hint,C.Cyan(.05f*open*he),6);
                U.Dashed(new Rect(hint.x,hint.y,hint.width,1),C.CyanSoft(.35f*open*he));U.Dashed(new Rect(hint.x,hint.yMax-1,hint.width,1),C.CyanSoft(.35f*open*he));
                NativeIcons.Draw(new Rect(hint.x+10,hint.center.y-7,14,14),"radio",C.CyanSoft(.85f*open*he));
                U.Text(new Rect(hint.x+32,hint.y,hint.width-40,hint.height),"Host an online room to invite players.",10.5f,C.Ink(.8f*open*he),TextAnchor.MiddleLeft);y+=42;
            }
            if(players==0)
            {
                float ee=SocialRowIn(now,row++);NativeIcons.Draw(new Rect(r.center.x-9,y+8,18,18),"users",C.Dim(.8f*open*ee));
                U.Text(new Rect(x,y+28,w,20),"No one else is online right now.",10.5f,C.Muted(.9f*open*ee),TextAnchor.MiddleCenter);y+=56;
            }
            foreach(var p in acc.OnlinePlayers){if(p==null||string.IsNullOrEmpty(p.id))continue;SocialPlayer(new Rect(r.x+6,y,r.width-12,SocialRowH),view,p,canInvite,open,now,row++);y+=SocialRowH;}
            U.EndBand();
            // The scroll thumb.
            if(maxScroll>0){float th=Mathf.Max(28,view.height*view.height/(body+4)),ty=view.y+(view.height-th)*(socialScroll/maxScroll);U.Round(new Rect(r.xMax-6,ty,3,th),C.Cyan(.45f*open),2);}
            if(maxScroll>0&&socialScroll<maxScroll-1)U.Fade(new Rect(view.x,view.yMax-22,view.width,22),C.Panel(.95f*open),false);
            U.Pop();
        }
        private void SocialHeader(Rect r,int players,float open,float now)
        {
            float a=open;NativeIcons.Draw(new Rect(r.x+16,r.y+14,18,18),"users",C.Cyan(a));
            U.Text(new Rect(r.x+42,r.y+10,200,16),"ONLINE PLAYERS",11.5f,C.Ink(.95f*a),TextAnchor.MiddleLeft,bold:true,tracking:.2f);
            U.Text(new Rect(r.x+42,r.y+27,220,14),players==1?"1 pilot signed in":players+" pilots signed in",10,C.Muted(.95f*a),TextAnchor.MiddleLeft);
            // The request in flight: a turning dashed ring.
            if(account.SocialBusy){var sc=new Vector2(r.xMax-56,r.y+24);U.Push(sc,Vector2.zero,1,now*300);NativeIcons.Draw(new Rect(sc.x-8,sc.y-8,16,16),"circle-dashed",C.CyanSoft(.85f*a));U.Pop();}
            var x=new Rect(r.xMax-38,r.y+11,26,26);float h=U.HoverAmount(9702,x,a>.5f);U.Round(x,U.A(U.Mix(C.Indigo(0),C.Indigo(.95f),h),a),13,U.A(C.Edge(),h*a));
            U.Push(x.center,Vector2.zero,1,90*h);NativeIcons.Draw(new Rect(x.center.x-7,x.center.y-7,14,14),"x",U.A(U.Mix(C.Muted(),Color.white,h),a));U.Pop();
            if(a>.5f&&U.Button(x))SocialClose();
            U.Fill(new Rect(r.x+14,r.y+SocialHeadH-1,r.width-28,1),C.Edge(.55f*a));
        }
        private static void SocialSection(Rect r,string title,int count,Color tone,float open)
        {
            float tw=U.Measure(title,9,true,false,.24f).x;U.Text(new Rect(r.x,r.y+4,tw+4,r.height-4),title,9,C.Muted(.95f*open),TextAnchor.MiddleLeft,bold:true,tracking:.24f);
            string n=count.ToString();float nw=U.Measure(n,9.5f,true,true).x+12;var chip=new Rect(r.x+tw+10,r.y+10,nw,15);U.Pill(chip,U.A(tone,.16f*open),U.A(tone,.5f*open));
            U.Text(chip,n,9.5f,U.A(U.Mix(tone,Color.white,.35f),open),TextAnchor.MiddleCenter,bold:true,mono:true);
            U.Fill(new Rect(chip.xMax+8,r.y+17.5f,r.xMax-chip.xMax-8,1),C.Edge(.4f*open));
        }
        private static void SocialNote(Rect r,string text,Color tone,float open)
        {NativeIcons.Draw(new Rect(r.x+2,r.center.y-6,12,12),"circle-alert",U.A(tone,.9f*open));U.Text(new Rect(r.x+20,r.y,r.width-20,r.height),text,10,U.A(tone,.9f*open),TextAnchor.MiddleLeft);}
        // A row's entrance: staggered after the panel opens.
        private float SocialRowIn(float now,int i)=>M.Bezier(.2f,.8f,.2f,1,M.Clamp01((now-socialOpenAt-.08f-i*.035f)/.24f));
        // An invitation: initials, who, a countdown to its expiry draining along its foot, Accept / Decline; a new one slides in.
        private void SocialInvite(Rect r,Rect view,NativePlayerInvite inv,bool canAccept,float open,float now,int i)
        {
            float e=SocialRowIn(now,i),fresh=socialInviteSeen.TryGetValue(inv.id,out float seen)?Mathf.Exp(-(now-seen)*2.5f):0,a=open*e;if(a<=.01f||r.yMax<view.y||r.y>view.yMax)return;
            float slide=18*(1-e)+40*Mathf.Pow(fresh,3);var d=new Rect(r.x+slide,r.y,r.width,r.height);int hid=9710+(inv.id.GetHashCode()&1023);float h=U.HoverAmount(hid,d,a>.5f);
            U.RoundGradient(d,U.Mix(U.Hex(0x2a1640,.85f),U.Hex(0x341a4e,.92f),h),C.Navy(.9f),8,U.A(U.Mix(C.Magenta(.32f),C.Magenta(.7f),Mathf.Max(h,fresh)),a),1,true);
            if(fresh>.02f)U.Glow(d,C.Magenta(.4f*fresh*a),12);U.Fill(new Rect(d.x,d.y+8,3,d.height-16),C.Magenta(.95f*a));
            SocialAvatar(new Vector2(d.x+30,d.y+24),14,inv.username,false,a,now);
            U.Text(new Rect(d.x+52,d.y+8,d.width-120,18),inv.username,12.5f,C.Ink(a),TextAnchor.MiddleLeft,bold:true);
            U.Text(new Rect(d.x+52,d.y+25,d.width-120,15),"invited you to their room",10,C.Muted(.95f*a),TextAnchor.MiddleLeft);
            // Two minutes to answer (the service's milliseconds; hidden when this clock disagrees).
            double left=(inv.expires-DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())/1000.0;
            if(left>0&&left<=125)
            {
                float f=Mathf.Clamp01((float)(left/120));int s=Mathf.CeilToInt((float)left);Color t=left<20?C.Warn():C.Magenta();
                NativeIcons.Draw(new Rect(d.xMax-58,d.y+11,11,11),"hourglass",U.A(t,.85f*a));U.Text(new Rect(d.xMax-46,d.y+8,36,16),(s/60)+":"+(s%60).ToString("00"),10,U.A(U.Mix(t,Color.white,.4f),a),TextAnchor.MiddleLeft,mono:true,bold:true);
                U.Fill(new Rect(d.x+8,d.yMax-3,(d.width-16),2),C.Edge(.35f*a));U.HGradient(new Rect(d.x+8,d.yMax-3,(d.width-16)*f,2),U.A(t,.35f*a),U.A(t,.95f*a));
            }
            bool busy=account.SocialBusy;float by=d.yMax-34,bw=92;
            bool usable=e>.9f&&d.y>=view.y&&d.yMax<=view.yMax;
            if(SocialButton(new Rect(d.xMax-10-bw,by,bw,24),"ACCEPT","check",0,canAccept&&!busy&&usable,a,hid*3+1))SocialAccept(inv);
            if(SocialButton(new Rect(d.xMax-10-bw*2-8,by,bw,24),"DECLINE","x",1,!busy&&usable,a,hid*3+2))account.AnswerInvite(inv.id,false,null);
        }
        // Accepting, from the panel's card or the pop-up: the one path.
        private void SocialAccept(NativePlayerInvite inv)=>account.AnswerInvite(inv.id,true,code=>{if(!CanAcceptInvite)return;internetRooms=true;relayOverride="";joinCode=code;socialToastJoining=null;socialToastGone.Add(inv.id);SocialClose();JoinRoom();});

        // ---- the pop-up ----
        // Where it may show: a menu (the pause menu too) or a match's end, never mid-fight; the invitation waits on the service meanwhile (two minutes).
        private bool SocialToastSurface=>account?.Account!=null&&!dedicated&&prefs!=null&&!StageInputHeld&&!reportOpen&&(menu||arena!=null&&(match.Phase==NativePhase.Match&&arena.Pve==null||ExpeditionEndingShowing));
        private void SocialToastGui()
        {
            if(!SocialToastSurface){socialToast=null;socialToastOutAt=-1;socialToastJoining=null;return;}
            float now=Time.unscaledTime;
            // The answer came back without a join: say so (the panel's status line isn't showing).
            if(socialToastJoining!=null&&!account.SocialBusy){socialToastGone.Add(socialToastJoining);socialToastJoining=null;Notice("That invitation couldn't be accepted. It may have expired.");}
            // The open panel shows them all: what it has shown doesn't pop up again.
            int waiting=0;NativePlayerInvite next=null;bool still=false;
            foreach(var i in account.Invitations)
            {
                if(i==null||string.IsNullOrEmpty(i.id))continue;if(socialOpen)socialToastGone.Add(i.id);
                if(socialToast!=null&&i.id==socialToast.id)still=true;if(socialToastGone.Contains(i.id))continue;waiting++;if(next==null)next=i;
            }
            if(Time.frameCount%120==0)socialToastGone.RemoveWhere(id=>Array.FindIndex(account.Invitations,i=>i!=null&&i.id==id)<0);
            if(socialToast!=null&&socialToastOutAt<0&&(!still||socialToastGone.Contains(socialToast.id)))socialToastOutAt=now;
            if(socialToast!=null&&socialToastOutAt>=0&&now-socialToastOutAt>.18f){socialToast=null;socialToastOutAt=-1;}
            if(socialToast==null&&next!=null){socialToast=next;socialToastAt=now;socialToastOutAt=-1;nativeAudio?.Cue("ui.notice",null,null,.5f,mine:true);}
            if(socialToast==null){socialToastRect=default;return;}
            var saved=GUI.matrix;
            try{SocialToastCard(socialToast,Mathf.Max(0,waiting-1),now);}
            finally{GUI.matrix=saved;}
        }
        private void SocialToastCard(NativePlayerInvite inv,int others,float now)
        {
            float enter=M.Bezier(.2f,.8f,.2f,1,M.Clamp01((now-socialToastAt)/.34f)),leave=socialToastOutAt>=0?M.Clamp01((now-socialToastOutAt)/.18f):0,a=enter*(1-leave);
            var anchor=SocialAnchor();var r=new Rect(anchor.x-SocialWidth+36*(1-enter)+24*leave,anchor.y+SocialChipH+12,SocialWidth,SocialToastH);socialToastRect=r;socialToastSeen=now;
            float h=U.HoverAmount(9703,r,a>.5f),fresh=Mathf.Exp(-(now-socialToastAt)*2.2f),beat=Mathf.Repeat(now-socialToastAt,1.8f),kick=Mathf.Exp(-beat*7);bool usable=enter>.7f&&leave<=0,joining=socialToastJoining==inv.id,canAccept=CanAcceptInvite;
            // Glass over any screen, its edge lit on the arrival and on each beat while it waits.
            U.Glow(new Rect(r.x+6,r.y+12,r.width-12,r.height-6),new Color(0,0,0,.6f*a),18);U.Glow(r,C.Magenta((.14f+.5f*fresh+.12f*kick)*a),16);
            U.RoundGradient(r,U.A(U.Mix(U.Hex(0x2a1640,.97f),U.Hex(0x341a4e,.98f),h),a),U.A(C.Navy(.97f),a),10,C.Magenta((.5f+.45f*Mathf.Max(fresh,Mathf.Max(h,kick)))*a),1,true);
            if(fresh>.03f){float g=14*(1-fresh);U.Round(new Rect(r.x-g,r.y-g,r.width+2*g,r.height+2*g),Color.clear,10+g,C.Magenta(.7f*fresh*a));}
            U.Fill(new Rect(r.x,r.y+10,3,r.height-20),C.Magenta(.95f*a));
            // Who, with the envelope on their initials; what it is, or why it can't be taken yet.
            var c=new Vector2(r.x+32,r.y+30);SocialAvatar(c,16,inv.username,false,a,now);
            var mail=new Rect(c.x+7,c.y+7,15,15);U.Disc(new Rect(mail.x-1,mail.y-1,mail.width+2,mail.height+2),C.Panel(a));U.Disc(mail,C.Magenta(a));NativeIcons.DrawMarkup(new Rect(mail.x+2.5f,mail.y+2.5f,10,10),"social-mail",SocialMail,24,new Color(1,1,1,a));
            U.Text(new Rect(r.x+58,r.y+10,r.width-150,19),inv.username,13,C.Ink(a),TextAnchor.MiddleLeft,bold:true);
            if(canAccept||joining)U.Text(new Rect(r.x+58,r.y+29,r.width-98,15),"invited you to their room",10.5f,C.Ink(.8f*a),TextAnchor.MiddleLeft);
            else U.Text(new Rect(r.x+58,r.y+29,r.width-74,15),"Leave your room or match to accept",10.5f,C.Warn(.95f*a),TextAnchor.MiddleLeft);
            // Two minutes to answer (the service's milliseconds; hidden when this clock disagrees).
            double left=(inv.expires-DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())/1000.0;
            if(left>0&&left<=125)
            {
                float f=Mathf.Clamp01((float)(left/120));int s=Mathf.CeilToInt((float)left);Color t=left<20?C.Warn():C.Magenta();
                U.Text(new Rect(r.xMax-74,r.y+11,36,16),(s/60)+":"+(s%60).ToString("00"),10,U.A(U.Mix(t,Color.white,.4f),a),TextAnchor.MiddleRight,mono:true,bold:true);
                U.Fill(new Rect(r.x+10,r.yMax-4,r.width-20,2),C.Edge(.35f*a));U.HGradient(new Rect(r.x+10,r.yMax-4,(r.width-20)*f,2),U.A(t,.35f*a),U.A(t,.95f*a));
            }
            // Put away: the invitation stays on the chip's badge until it is answered or runs out.
            var x=new Rect(r.xMax-32,r.y+8,24,24);float xh=U.HoverAmount(9704,x,usable&&!joining);U.Round(x,U.A(U.Mix(C.Indigo(0),C.Indigo(.95f),xh),a),12,U.A(C.Edge(),xh*a));
            U.Push(x.center,Vector2.zero,1,90*xh);NativeIcons.Draw(new Rect(x.center.x-6,x.center.y-6,12,12),"x",U.A(U.Mix(C.Muted(),Color.white,xh),a));U.Pop();
            if(usable&&!joining&&U.Button(x))socialToastGone.Add(inv.id);
            float by=r.yMax-34,bw=92;
            if(joining)
            {
                var sc=new Vector2(r.xMax-96,by+12);U.Push(sc,Vector2.zero,1,now*300);NativeIcons.Draw(new Rect(sc.x-7,sc.y-7,14,14),"circle-dashed",C.CyanSoft(a));U.Pop();
                U.Text(new Rect(sc.x+13,by,80,24),"JOINING",9.5f,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,tracking:.18f);return;
            }
            bool busy=account.SocialBusy;
            if(SocialButton(new Rect(r.xMax-12-bw,by,bw,24),"ACCEPT","check",0,canAccept&&!busy&&usable,a,9705)){socialToastJoining=inv.id;SocialAccept(inv);}
            if(SocialButton(new Rect(r.xMax-12-bw*2-8,by,bw,24),"DECLINE","x",1,!busy&&usable,a,9706)){account.AnswerInvite(inv.id,false,null);socialToastGone.Add(inv.id);}
            // The others wait in the panel.
            if(others>0)
            {
                string more="+"+others+" MORE";float mw=U.Measure(more,9.5f,true,false,.12f).x+16;var m=new Rect(r.x+14,by+1,mw,22);float mh=U.HoverAmount(9707,m,usable);
                U.Pill(m,C.Magenta((.14f+.3f*mh)*a),C.Magenta((.5f+.4f*mh)*a));U.Text(m,more,9.5f,U.A(U.Mix(C.Ink(.9f),Color.white,mh),a),TextAnchor.MiddleCenter,bold:true,tracking:.12f);
                if(usable&&U.Button(m))SocialToggle();
            }
        }
        // An online player: initials with a live light, the name, Invite (SENT once sent, with a pop).
        private void SocialPlayer(Rect r,Rect view,NativeOnlinePlayer p,bool canInvite,float open,float now,int i)
        {
            float e=SocialRowIn(now,i),a=open*e;if(a<=.01f||r.yMax<view.y||r.y>view.yMax)return;var d=new Rect(r.x+16*(1-e),r.y,r.width,r.height);
            int hid=9800+(p.id.GetHashCode()&4095);float h=U.HoverAmount(hid,new Rect(r.x,Mathf.Max(r.y,view.y),r.width,Mathf.Min(r.yMax,view.yMax)-Mathf.Max(r.y,view.y)),a>.5f);
            if(h>0){U.Round(new Rect(d.x,d.y+2,d.width,d.height-4),C.Cyan(.06f*h*a),6);U.Fill(new Rect(d.x,d.y+10,2,(d.height-20)*h),C.Cyan(h*a));}
            SocialAvatar(new Vector2(d.x+28,d.center.y),13,p.username,true,a,now);
            U.Text(new Rect(d.x+50,d.y,d.width-150,d.height),p.username,12.5f,U.A(U.Mix(C.Ink(.92f),Color.white,h),a),TextAnchor.MiddleLeft,bold:true);
            var b=new Rect(d.xMax-10-86,d.center.y-12,86,24);bool usable=e>.9f&&r.y>=view.y&&r.yMax<=view.yMax;
            if(socialSent.TryGetValue(p.id,out float sentAt)&&now-sentAt<5)
            {
                float k=M.Clamp01((now-sentAt)/.35f),pop=1+.18f*Mathf.Sin(k*Mathf.PI)*(1-k);U.Push(b.center,Vector2.zero,pop);
                if(k<1){float g=8*k;U.Pill(new Rect(b.x-g,b.y-g,b.width+2*g,b.height+2*g),Color.clear,C.Good(.6f*(1-k)*a));}
                U.Pill(b,C.Good(.16f*a),C.Good(.7f*a));NativeIcons.Draw(new Rect(b.x+14,b.center.y-6,12,12),"check",C.Good(a));
                U.Text(new Rect(b.x+30,b.y,b.width-34,b.height),"SENT",9.5f,U.A(U.Mix(C.Good(),Color.white,.4f),a),TextAnchor.MiddleLeft,bold:true,tracking:.18f);U.Pop();
            }
            else if(SocialButton(b,"INVITE",null,2,canInvite&&!account.SocialBusy&&usable,a,hid*3)&&NativeRoomCodes.Valid(roomCode)){account.InvitePlayer(p.id,roomCode);socialSent[p.id]=now;}
        }
        // A button: 0 accept (cyan, void ink), 1 ghost, 2 invite (a cyan outline filling on hover, the paper plane).
        private bool SocialButton(Rect r,string label,string icon,int style,bool enabled,float alpha,int id)
        {
            float h=U.HoverAmount(id,r,enabled&&alpha>.5f),a=alpha*(enabled?1:.4f);bool press=enabled&&U.Hover(r)&&Input.GetMouseButton(0);var d=new Rect(r.x,r.y-h,r.width,r.height);
            U.Push(d.center,Vector2.zero,press?.96f:1);
            Color fill,edge,ink;
            if(style==0){fill=U.Mix(C.Cyan(.9f),C.CyanSoft(),h);edge=C.CyanSoft();ink=C.Void();if(h>0)U.Glow(new Rect(d.x+4,d.y+8,d.width-8,d.height-6),C.Cyan(.4f*h*a),12);}
            else if(style==1){fill=U.Mix(C.Indigo(.6f),U.Hex(0x1b2552,.95f),h);edge=U.Mix(C.Edge(),C.CyanSoft(.8f),h);ink=U.Mix(C.Ink(.85f),Color.white,h);}
            else{fill=U.Mix(C.Cyan(.08f),C.Cyan(.92f),h);edge=U.Mix(C.Cyan(.6f),C.CyanSoft(),h);ink=U.Mix(C.Cyan(),C.Void(),h);if(h>0)U.Glow(new Rect(d.x+4,d.y+8,d.width-8,d.height-6),C.Cyan(.35f*h*a),12);}
            U.Pill(d,U.A(fill,a),U.A(edge,a));
            float tw=U.Measure(label,9.5f,true,false,.18f).x,iw=13,start=d.center.x-(tw+iw+6)/2;
            if(icon!=null)NativeIcons.Draw(new Rect(start,d.center.y-iw/2,iw,iw),icon,U.A(ink,a));
            else NativeIcons.DrawMarkup(new Rect(start+2*h,d.center.y-iw/2-h,iw,iw),"social-send",SocialSend,24,U.A(ink,a));
            U.Text(new Rect(start+iw+6,d.y,tw+6,d.height),label,9.5f,U.A(ink,a),TextAnchor.MiddleLeft,bold:true,tracking:.18f);
            U.Pop();return U.Button(r,enabled)&&alpha>.5f;
        }
        // Initials in a ring tinted by the name, a live light at its foot for an online player.
        private static readonly int[] SocialTones={0x2de2ff,0xff4fd8,0x9d7bff,0x37e8a0,0xffc857,0x7ad7ff};
        private static void SocialAvatar(Vector2 c,float rad,string name,bool live,float a,float now)
        {
            int hsh=0;foreach(char ch in name??"")hsh=hsh*31+ch;Color tone=U.Hex(SocialTones[(hsh&int.MaxValue)%SocialTones.Length]);var r=new Rect(c.x-rad,c.y-rad,2*rad,2*rad);
            // (Two discs, not a ring: the kit's rounded ring nine-slices into a broken circle this small.)
            U.Disc(r,U.A(tone,.8f*a));U.Disc(new Rect(r.x+1.6f,r.y+1.6f,r.width-3.2f,r.height-3.2f),U.A(U.Mix(C.Void(),tone,.2f),a));
            U.Text(r,SocialInitials(name),rad*.78f,U.A(U.Mix(tone,Color.white,.55f),a),TextAnchor.MiddleCenter,bold:true);
            if(live){var l=new Rect(c.x+rad*.72f-3.5f,c.y+rad*.72f-3.5f,7,7);U.Disc(new Rect(l.x-1.5f,l.y-1.5f,10,10),C.Panel(a));U.Glow(l,C.Good((.3f+.25f*Mathf.Sin(now*2.4f+hsh))*a),4);U.Disc(l,C.Good(a));}
        }
        // Two initials: the first character and the start of its second word (after _ . - or a space, a capital after a small
        // letter, or a digit after a letter), else its second character.
        private static string SocialInitials(string name)
        {
            if(string.IsNullOrEmpty(name))return "?";char first='\0',second='\0';char prev='\0';bool gap=false;
            foreach(char ch in name)
            {
                if(!char.IsLetterOrDigit(ch)){if(first!='\0')gap=true;prev=ch;continue;}
                if(first=='\0'){first=ch;prev=ch;continue;}
                if(gap||char.IsUpper(ch)&&char.IsLower(prev)||char.IsDigit(ch)&&char.IsLetter(prev)){second=ch;break;}prev=ch;
            }
            if(first=='\0')return "?";if(second=='\0')foreach(char ch in name)if(char.IsLetterOrDigit(ch)&&ch!=first){second=ch;break;}
            return (first.ToString()+(second=='\0'?"":second.ToString())).ToUpperInvariant();
        }
    }
}
