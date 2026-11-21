using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using P=ChipArena.UnityConversion.NativePartyLoadModel;
using R=ChipArena.UnityConversion.NativeHudCyber;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The party panel on the stage loading screen (Core NativePartyLoadModel and NativePartyLayout; Claude, 2026-10-18), in the
    /// cyber HUD kit under the loading screen's title block: a header (PARTY · n and the ready count), one row per member and a
    /// footer (WAITING FOR n PLAYERS… then EVERYONE'S IN). A row: the state stripe, the weapon's icon, the name (YOU or HOST
    /// beside it), the value (my percentage, a replicated one, a tick on arrival, or where an uncounted member is), the state
    /// chip and the bar: my real load; another member's indeterminate sweep while they load (the view has no per-member value
    /// yet), filling and ticking when they arrive; a dashed track when dropped. Rows rise and fade in on a CSS curve, staggered.
    /// Reduced effects: no rise, sweep, fill or pop. Repaint only; nothing is allocated per frame.
    /// </summary>
    public static class NativePartyLoadingHud
    {
        public const string Tick="check";
        public const float Rise=10;

        public static void Draw(in NativePartyLayout l,NativePartyLoadModel m,float a,float now,bool reduced)
        {
            if(Event.current.type!=EventType.Repaint||m==null||m.Count==0||a<=0||l.Shown<=0)return;
            var box=new Rect(l.Box.X,l.Box.Y,l.Box.W,l.Box.H);
            U.Glow(new Rect(box.x,box.y+14,box.width,box.height),new Color(0,0,0,.35f*a),26);
            H.Panel(box,a,C.Cyan(),R.Cut,H.Corners.Diagonal,true,.84f);
            float hs=l.Compact?9:10;var head=l.Header;var hr=new Rect(head.X,head.Y,head.W,head.H);
            U.Fill(new Rect(hr.x,hr.center.y-2,H.Snap(4),H.Snap(4)),C.Cyan(a));
            U.Text(new Rect(hr.x+10,hr.y,hr.width-10,hr.height),m.Header,hs,C.CyanSoft(.95f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            if(m.Aggregate!=null)U.Text(hr,m.Aggregate,hs,m.EveryoneIn?C.Good(a):C.Muted(a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.14f);
            for(int i=0;i<l.Shown&&i<m.Count;i++){var b=l.RowBox(i);Row(new Rect(b.X,b.Y,b.W,b.H),m.Rows[i],l.Compact,a,now,reduced);}
            Footer(l,m,a,now,reduced);
        }
        public static Color Tone(PartyLoadState s,float a=1)
        {
            switch(s)
            {
                case PartyLoadState.Waiting:return C.Warn(a);case PartyLoadState.Ready:case PartyLoadState.InWorld:return C.Good(a);
                case PartyLoadState.Dropped:return C.Danger(a);case PartyLoadState.Extracted:return C.Muted(a);default:return C.Cyan(a);
            }
        }
        private static void Row(Rect r,in PartyLoadRow p,bool compact,float a,float now,bool reduced)
        {
            float e=P.Entrance(p,reduced);if(e<=0)return;float ra=a*e;r.y+=(1-e)*Rise;
            bool dim=p.State==PartyLoadState.Dropped||p.State==PartyLoadState.Extracted||!p.Counted&&!p.You;float k=dim?.6f:1;var tone=Tone(p.State);
            if(p.You)U.Fill(new Rect(r.x,r.y+1,r.width,r.height-2),C.Cyan(.07f*ra));
            U.Fill(new Rect(r.x+4,r.yMax-H.Px,r.width-8,H.Px),C.Edge(.4f*ra));
            U.Fill(new Rect(r.x,r.y+5,H.Snap(2),r.height-10),U.A(tone,ra*k));
            float line=compact?15:18,top=r.y+(compact?3:5),ic=compact?15:19,ch=compact?14:16,cs=compact?8.5f:9;
            // the weapon's icon
            var icon=new Rect(r.x+10,top+(line-ic)/2,ic,ic);
            if(p.Weapon!=null)NativeIcons.Draw(icon,NativeIcons.Spec(p.Weapon),p.You?C.Cyan(ra):C.Ink(.85f*ra*k));
            float nx=icon.xMax+8;
            // the chip at the right
            float cw=Mathf.Max(compact?54:62,U.Measure(p.Chip,cs,true,true,.1f).x+18);var chip=new Rect(r.xMax-6-cw,top+(line-ch)/2,cw,ch);
            H.Cut(chip,C.Navy(.92f*ra),R.TagCut);if(p.Arrived)H.Cut(chip,U.A(tone,.16f*ra),R.TagCut);H.Outline(chip,U.A(tone,.65f*ra*k),R.TagCut);
            U.Fill(new Rect(chip.x+4,chip.y+(ch-7)/2,H.Snap(2),7),U.A(tone,ra*k));
            U.Text(new Rect(chip.x+9,chip.y,cw-10,ch),p.Chip,cs,U.A(tone,ra*k),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
            // the value: my percentage (or a replicated one) while loading, a tick once in, or where an uncounted member is
            float vx=chip.x-8;float vs=compact?10:11.5f;
            if(p.Counted&&p.Arrived)vx=TickMark(new Vector2(vx,top+line/2),p,compact,ra,reduced)-6;
            else if(p.Counted&&p.Progress>=0&&p.State!=PartyLoadState.Dropped){string pc=NativeStageLabels.Percent(p.Progress);float w=U.Measure(pc,vs,true,true).x;U.Text(new Rect(vx-w,top,w,line),pc,vs,C.Ink(ra),TextAnchor.MiddleRight,bold:true,mono:true);vx-=w+8;}
            else if(!p.Counted&&p.Place!=null&&p.State!=PartyLoadState.Dropped&&p.State!=PartyLoadState.Extracted){float w=U.Measure(p.Place,cs,true,true,.1f).x;U.Text(new Rect(vx-w,top,w,line),p.Place,cs,U.Hex(p.PlaceAccent,.9f*ra),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.1f);vx-=w+8;}
            // the name, fitted to the room left (smaller, then clipped), then YOU or HOST
            float tagW=p.You?34:p.Host?44:0,room=Mathf.Max(16,vx-nx-tagW),ns=compact?11.5f:13,nw=U.Measure(p.Name,ns,true,false,.04f).x;
            if(nw>room){ns=Mathf.Max(compact?9.5f:10.5f,ns*room/nw);nw=Mathf.Min(room,U.Measure(p.Name,ns,true,false,.04f).x);}
            U.BeginBand(new Rect(nx-2,top-3,room+2,line+6));
            U.Text(new Rect(nx,top,room+120,line),p.Name,ns,C.Ink(ra*k),TextAnchor.MiddleLeft,bold:true,tracking:.04f);
            U.EndBand();
            float tx=nx+nw+6;
            if(p.You){var yr=new Rect(tx,top+(line-13)/2,28,13);H.Cut(yr,C.Cyan(.92f*ra),2);U.Text(yr,"YOU",8.5f,C.Void(ra),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.08f);}
            else if(p.Host)U.Text(new Rect(tx,top,40,line),"HOST",8.5f,C.Muted(ra*k),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
            // the bar
            float bh=compact?2:3,by=r.yMax-(compact?7:10);Bar(new Rect(nx,by,r.xMax-6-nx,bh),p,tone,ra,k,now,reduced);
        }
        // The tick on arrival, popping in once the fill lands (whole with reduced effects); returns its left edge.
        private static float TickMark(Vector2 right,in PartyLoadRow p,bool compact,float a,bool reduced)
        {
            float pop=P.TickScale(p,reduced);if(pop<=0)return right.x;float s=(compact?11:13)*pop,box=compact?14:16;
            var b=new Rect(right.x-box,right.y-box/2,box,box);H.Cut(b,C.Good(.18f*a),3);H.Outline(b,C.Good(.7f*a),3);
            NativeIcons.Draw(new Rect(b.center.x-s/2,b.center.y-s/2,s,s),Tick,C.Good(a));return b.x;
        }
        private static void Bar(Rect b,in PartyLoadRow p,Color tone,float a,float k,float now,bool reduced)
        {
            U.Fill(b,C.Navy(.95f*a));U.Fill(new Rect(b.x,b.yMax+1,b.width,H.Px),C.Edge(.3f*a));
            switch(p.State)
            {
                case PartyLoadState.Dropped:Dashes(b,C.Danger(.5f*a));return;
                case PartyLoadState.Extracted:return;
                case PartyLoadState.Waiting:Dashes(b,C.Warn(.55f*a));return;
            }
            if(!p.Counted&&!p.You)
            {
                // Not in this load: where they are, as a thin rule in that place's accent (loading elsewhere: a slow sweep).
                if(p.State==PartyLoadState.Loading){float q=P.Sweep(now,p.Order+3,reduced),sw=b.width*.18f;U.Fill(new Rect(b.x+(b.width-sw)*q,b.y,sw,b.height),U.Hex(p.PlaceAccent,.55f*a));}
                else U.Fill(b,U.Hex(p.PlaceAccent,.28f*a));
                return;
            }
            if(p.Arrived)
            {
                // Arrival: the bar fills from where it stood and holds full.
                float f=P.ArrivalFill(p,reduced);var fill=new Rect(b.x,b.y,b.width*f,b.height);
                U.Glow(fill,C.Good(.22f*a),5);U.Fill(fill,C.Good(.95f*a));return;
            }
            if(p.Progress>=0)
            {
                // A real value: my own load (or a replicated one), the loading screen's segmented look.
                var fill=new Rect(b.x,b.y,b.width*Mathf.Clamp01(p.Progress),b.height);
                if(fill.width>0){U.Glow(fill,C.Cyan(.25f*a),5);U.Fill(fill,C.Cyan(a));}
                for(int i=1;i<10;i++){float x=b.x+b.width*i/10;U.Fill(new Rect(x,b.y,H.Px,b.height),C.Void(.9f*a));}
                if(p.Progress>0&&p.Progress<1)U.Fill(new Rect(fill.xMax-H.Px,b.y-2,H.Snap(2),b.height+4),C.Ink(a));
                return;
            }
            // Under way, no value known: an indeterminate sweep (still with reduced effects), each row on its own phase.
            float t=P.Sweep(now,p.Order,reduced),w=b.width*.26f,x0=b.x+(b.width+w)*t-w;
            var seg=Rect.MinMaxRect(Mathf.Max(b.x,x0),b.y,Mathf.Min(b.xMax,x0+w),b.yMax);
            if(seg.width>0){U.HGradient(new Rect(seg.x,seg.y,seg.width*.5f,seg.height),U.A(tone,.1f*a*k),U.A(tone,.9f*a*k));U.HGradient(new Rect(seg.x+seg.width*.5f,seg.y,seg.width*.5f,seg.height),U.A(tone,.9f*a*k),U.A(tone,.1f*a*k));}
        }
        // A dashed track (dropped, or waiting to start): 5 on, 4 off.
        private static void Dashes(Rect b,Color c){for(float x=b.x;x<b.xMax;x+=9)U.Fill(new Rect(x,b.y,Mathf.Min(5,b.xMax-x),b.height),c);}
        private static void Footer(in NativePartyLayout l,NativePartyLoadModel m,float a,float now,bool reduced)
        {
            var f=l.Footer;var r=new Rect(f.X,f.Y,f.W,f.H);float fs=l.Compact?9.5f:10.5f;
            U.Fill(new Rect(r.x,r.y-2,r.width,H.Px),C.Edge(.45f*a));
            if(m.EveryoneIn)
            {
                float s=l.Compact?11:12;NativeIcons.Draw(new Rect(r.x,r.center.y-s/2,s,s),Tick,C.Good(a));
                U.Text(new Rect(r.x+s+7,r.y,r.width-s-7,r.height),m.Footer,fs,C.Good(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
            }
            else
            {
                float blink=reduced?1:.45f+.55f*Mathf.Abs(Mathf.Sin(now*3.2f));U.Fill(new Rect(r.x+1,r.center.y-2.5f,5,5),C.Cyan(blink*a));
                U.Text(new Rect(r.x+12,r.y,r.width-12,r.height),m.Footer,fs,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
            }
            string extra=l.Clipped?P.More(l.Count-l.Shown):null;if(extra!=null)U.Text(r,extra,fs,C.Muted(a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.12f);
        }
    }
}
