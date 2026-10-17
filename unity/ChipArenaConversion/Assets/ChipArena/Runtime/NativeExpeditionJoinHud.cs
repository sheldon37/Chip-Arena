using System;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using R=ChipArena.UnityConversion.NativeHudCyber;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The join circles' HUD in the cyber kit (Claude, 2026-10-17; docs/EXPEDITION-HUB.md "Join circle presentation"), drawn from
    /// Core NativeExpeditionJoinModel only:
    ///  - the party list (left, under the wallet and SAFE chip): every member, where they are (HUB, WARD ZERO, CENTRAL STATION,
    ///    CITY IRONWORKS from members[].area, in the zone's accent), what they are doing (deploying with the seconds, on the way,
    ///    back in the hub, down, offline, extracted), mine marked YOU, a return flashing its line;
    ///  - in a circle and counted: DEPLOYING TO and the zone, the seconds, twelve segments, who goes and how to stay;
    ///    in it but not counted: why (down, picking a chip, carrying, loading, waiting for the host); at a cleared circle: CLEARED;
    ///    near one: a prompt, with whoever is deploying there and the seconds;
    ///  - the notes: who deployed where and who came back;
    ///  - my deploy flash over the last moment of my countdown, handing off to the stage loading screen's cut.
    /// Pure draws: nothing here sends a command or reads a rule.
    /// </summary>
    public static class NativeExpeditionJoinHud
    {
        private static Color Hex(int rgb,float a=1)=>U.Hex(rgb,a);
        private static Color Tone(ExpeditionTone t,float a=1)=>NativeExpeditionHud.Tone(t,a);
        private static readonly string[] tenths=MakeTenths();
        private static string[] MakeTenths(){var s=new string[61];for(int i=0;i<s.Length;i++)s[i]=(i/10).ToString(System.Globalization.CultureInfo.InvariantCulture)+"."+(i%10).ToString(System.Globalization.CultureInfo.InvariantCulture);return s;}
        /// <summary>Seconds to one decimal (rounded up, so it never reads 0.0 before the deploy).</summary>
        public static string Tenths(float s){int k=(int)Math.Ceiling(Math.Max(0,s)*10-1e-4);return tenths[Math.Max(0,Math.Min(tenths.Length-1,k))];}
        private static readonly string[] partyHead={"PARTY","PARTY · 1","PARTY · 2","PARTY · 3","PARTY · 4","PARTY · 5","PARTY · 6","PARTY · 7","PARTY · 8"};
        private static readonly string[] pilots={"0 / 4 PILOTS","1 / 4 PILOTS","2 / 4 PILOTS","3 / 4 PILOTS","4 / 4 PILOTS"};

        // ---- the party list ------------------------------------------------------------------------------------------------
        public const float PartyWidth=256,RowHeight=38;
        /// <summary>The party list at (x, y): returns the y under it. Drawn when the party spans more than one member or the run
        /// splits areas (the circles are in the view); a solo pilot in one area needs no list.</summary>
        public static float Party(NativeExpeditionJoinModel m,float x,float y,float time,bool reduced)
        {
            if(m==null||m.PartyCount==0||m.PartyCount<2&&!m.InHub)return y;
            float h=30+m.PartyCount*RowHeight+6;var r=new Rect(x,y,PartyWidth,h);H.Panel(r,1,C.Cyan(),R.Cut,H.Corners.Diagonal,true,.82f);
            U.Text(new Rect(r.x+14,r.y+8,r.width-28,14),partyHead[Math.Min(partyHead.Length-1,m.PartyCount)],10,C.CyanSoft(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);
            U.Text(new Rect(r.x+14,r.y+8,r.width-28,14),"WHERE",9,C.Muted(.8f),TextAnchor.MiddleRight,mono:true,tracking:.16f);
            float ry=r.y+28;
            for(int i=0;i<m.PartyCount;i++){Row(new Rect(r.x+10,ry,r.width-20,RowHeight-4),m,m.Party[i],time,reduced);ry+=RowHeight;}
            return r.yMax;
        }
        private static void Row(Rect r,NativeExpeditionJoinModel m,in ExpeditionPartyRow p,float time,bool reduced)
        {
            float dim=p.Offline||p.Extracted?.55f:1;var tone=Tone(p.Tone);var accent=Hex(p.Accent);
            // A return flashes the line in the hub's green, fading over the note's time.
            if(p.Returned>=0){float k=1-p.Returned/NativeExpeditionJoinModel.ReturnSeconds;U.Fill(r,Hex(NativeExpeditionJoinZones.HubHex,.16f*k));if(!reduced)U.Glow(r,Hex(NativeExpeditionJoinZones.HubHex,.1f*k),6);}
            if(p.You)U.Fill(r,C.Cyan(.06f));
            U.Fill(new Rect(r.x,r.y+4,H.Snap(2),r.height-8),U.A(tone,dim));
            // Name (YOU / HOST tags), the place chip at the right, the status under the name.
            float nx=r.x+10;U.Text(new Rect(nx,r.y+2,150,17),p.Name,12,C.Ink(dim),TextAnchor.MiddleLeft,bold:true,tracking:.04f);
            float nw=Math.Min(150,U.Measure(p.Name,12,true,false,.04f).x);
            if(p.You){var yr=new Rect(nx+nw+6,r.y+4,30,13);H.Cut(yr,C.Cyan(.9f),2);U.Text(yr,"YOU",9,C.Void(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.08f);}
            else if(p.Host)U.Text(new Rect(nx+nw+6,r.y+4,40,13),"HOST",9,C.Muted(dim),TextAnchor.MiddleLeft,mono:true,tracking:.1f);
            float cw=Math.Max(46,U.Measure(p.Place,9,true,true,.1f).x+20);var chip=new Rect(r.xMax-cw,r.y+3,cw,16);
            H.Cut(chip,C.Navy(.92f),R.TagCut);H.Outline(chip,U.A(accent,.6f*dim),R.TagCut);U.Fill(new Rect(chip.x+3,chip.y+4,H.Snap(2),8),U.A(accent,dim));
            U.Text(new Rect(chip.x+8,chip.y,cw-10,chip.height),p.Place,9,U.A(accent,dim),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
            U.Text(new Rect(nx,r.y+19,r.width-20,14),p.Status,10,U.A(tone,dim),TextAnchor.MiddleLeft,mono:true,tracking:.08f);
            // Deploying: the count fills under the line in the circle's accent.
            if(p.Deploying&&p.Ring>=0&&p.Ring<m.RingCount){var ring=m.Rings[p.Ring];H.Line(new Rect(nx,r.yMax-2,r.width-14,2),ring.Charge,Hex(ring.Zone.Accent),C.Edge(.5f));}
        }

        // ---- the circle panel ------------------------------------------------------------------------------------------------
        /// <summary>The panel for my circle: counted (deploying), standing in it uncounted, at a cleared one, or near one (a prompt).
        /// Centred at `y` (its top), clear of the crosshair and the health box.</summary>
        public static void Circle(NativeExpeditionJoinModel m,float W,float y,float time,bool reduced)
        {
            if(m==null||m.ViewerRing<0||m.ViewerRing>=m.RingCount)return;var ring=m.Rings[m.ViewerRing];var accent=Hex(ring.Zone.Accent);
            switch(m.Viewer)
            {
                case ExpeditionJoinViewer.Deploying:Deploying(m,ring,W,y,time,reduced);break;
                case ExpeditionJoinViewer.NotCounted:
                {
                    var r=new Rect(W/2-210,y,420,66);H.Panel(r,1,C.Warn(),R.Cut);
                    U.Text(new Rect(r.x+16,r.y+10,r.width-32,14),"IN THE CIRCLE · NOT DEPLOYING",11,C.Warn(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
                    U.Text(new Rect(r.x+16,r.y+27,r.width-32,20),m.ViewerReason??NativeExpeditionJoinModel.Waiting,15,C.Ink(),TextAnchor.MiddleLeft,bold:true,tracking:.04f);
                    U.Text(new Rect(r.x+16,r.y+47,r.width-32,13),ring.Title,10,accent,TextAnchor.MiddleLeft,mono:true,tracking:.14f);
                    break;
                }
                case ExpeditionJoinViewer.Cleared:Prompt(W,y+20,ring.Title,"CLEARED · NO DEPLOY",C.Good(),null);break;
                case ExpeditionJoinViewer.Near:
                    if(ring.Phase==ExpeditionJoinPhase.Charging&&ring.Names!=null)Prompt(W,y+20,ring.Names,"DEPLOYING · "+Tenths(ring.Left)+"s · STEP IN TO GO WITH THEM",C.Good(),accent);
                    else if(ring.Phase==ExpeditionJoinPhase.Cancelled)Prompt(W,y+20,ring.Title,"CANCELLED · THE CIRCLE EMPTIED",C.Warn(),accent);
                    else Prompt(W,y+20,ring.Title,"STEP INTO THE CIRCLE TO DEPLOY",accent,accent);
                    break;
            }
        }
        private static void Deploying(NativeExpeditionJoinModel m,in ExpeditionJoinRing ring,float W,float y,float time,bool reduced)
        {
            var accent=Hex(ring.Zone.Accent);float pulse=reduced?1:.75f+.25f*(float)(.5+.5*Math.Cos(time*2*Math.PI*(1+ring.Charge*2)));
            var r=new Rect(W/2-230,y,460,96);U.Glow(r,U.A(accent,.14f*pulse),12);H.Panel(r,1,accent,R.Cut,H.Corners.Diagonal,true,.92f);
            U.Text(new Rect(r.x+18,r.y+10,260,14),"DEPLOYING TO",11,C.CyanSoft(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);
            U.Text(new Rect(r.x+18,r.y+24,300,30),ring.Title,24,accent,TextAnchor.MiddleLeft,bold:true,tracking:.05f,glow:U.A(accent,.3f));
            U.Text(new Rect(r.xMax-140,r.y+10,122,44),Tenths(ring.Left),34,C.Ink(),TextAnchor.MiddleRight,bold:true,mono:true,glow:U.A(accent,.35f*pulse));
            // Twelve segments, as the hologram over the pad.
            float bx=r.x+18,bw=r.width-36,gap=3,seg=(bw-gap*11)/12;int lit=(int)Math.Floor(ring.Charge*12+1e-4f);
            for(int k=0;k<12;k++){var cell=new Rect(bx+k*(seg+gap),r.y+58,seg,6);U.Fill(cell,k<lit?U.Mix(accent,C.Ink(),.35f*ring.Charge):C.Void(.8f));if(k==lit&&!reduced)U.Fill(new Rect(cell.x,cell.y,seg*(ring.Charge*12-lit),6),U.A(accent,.6f));}
            U.Text(new Rect(r.x+18,r.y+70,r.width-160,16),ring.Names??"YOU",11,C.Ink(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.08f);
            U.Text(new Rect(r.xMax-150,r.y+70,132,16),pilots[Math.Max(0,Math.Min(pilots.Length-1,ring.Inside))],10,C.Muted(),TextAnchor.MiddleRight,mono:true,tracking:.1f);
            U.Text(new Rect(r.x,r.yMax+6,r.width,14),"STEP OUT OF THE CIRCLE TO STAY IN THE HUB",10,C.Muted(),TextAnchor.MiddleCenter,mono:true,tracking:.12f,shadow:new Color(0,0,0,.7f));
        }
        private static void Prompt(float W,float y,string head,string line,Color tone,Color? tick)
        {
            float hw=U.Measure(head,12,true,true,.14f).x,lw=U.Measure(line,11,false,true,.1f).x,w=hw+lw+60;var r=new Rect(W/2-w/2,y,w,30);
            H.Tag(r,tick??tone,1);U.Text(new Rect(r.x+16,r.y,hw+4,r.height),head,12,tick??tone,TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
            U.Fill(new Rect(r.x+24+hw,r.y+9,H.Snap(1),12),C.Edge());
            U.Text(new Rect(r.x+32+hw,r.y,lw+8,r.height),line,11,tone,TextAnchor.MiddleLeft,mono:true,tracking:.1f);
        }

        // ---- the notes and my flash ---------------------------------------------------------------------------------------------
        /// <summary>The deploy and return notes, newest on top, right-aligned under `y`; each fades in and out.</summary>
        public static void Toasts(NativeExpeditionJoinModel m,float W,float y)
        {
            if(m==null)return;
            for(int i=0;i<m.ToastCount;i++)
            {
                var n=m.Toasts[i];float a=Math.Min(1,n.Age/.2f)*Math.Min(1,(NativeExpeditionJoinModel.ToastSeconds-n.Age)/.6f);if(a<=0)continue;var accent=Hex(n.Accent);
                float w=U.Measure(n.Text,11,true,true,.1f).x+34;var r=new Rect(W-24-w+(1-Math.Min(1,n.Age/.2f))*16,y,w,26);
                H.Tag(r,accent,a);U.Text(new Rect(r.x+14,r.y,w-14,r.height),n.Text,11,U.A(accent,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);y+=30;
            }
        }
        /// <summary>My deploy flash: the screen whitens toward the circle's light over the last moment of my countdown; the stage
        /// loading screen's cut lands on it.</summary>
        public static void Flash(NativeExpeditionJoinModel m,float W,float Hh,bool reduced)
        {
            if(m==null||m.Flash<=.001f)return;int accent=m.ViewerRing>=0&&m.ViewerRing<m.RingCount?m.Rings[m.ViewerRing].Zone.Accent:0xCFF2FF;
            float k=m.Flash;var tint=U.Mix(Hex(accent),Color.white,.55f);
            U.Fill(new Rect(0,0,W,Hh),U.A(tint,(reduced?.55f:.8f)*k));
            if(!reduced){float e=Mathf.Min(W,Hh)*.12f;U.Glow(new Rect(-e,0,e,Hh),U.A(tint,.5f*k),e*1.5f);U.Glow(new Rect(W,0,e,Hh),U.A(tint,.5f*k),e*1.5f);}
        }
    }
}
