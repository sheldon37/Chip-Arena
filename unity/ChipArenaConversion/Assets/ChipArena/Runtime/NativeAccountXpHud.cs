using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using M=ChipArena.UnityConversion.NativeAccountXpPanel;
using R=ChipArena.UnityConversion.NativeHudCyber;
using T=ChipArena.UnityConversion.NativeAccountXpText;
using U=ChipArena.UnityConversion.NativeUi;
using X=ChipArena.UnityConversion.NativeAccountXpTone;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The account XP panel and the menu's level, in the cyber HUD kit (Core NativeAccountXpPanel, NativeAccountXpLayout,
    /// NativeAccountXpBadge; Claude, 2026-10-19). XP has its own ultraviolet (NativeAccountXpTone), never the wallet's gold or the
    /// chips' cyan, and its own words. The panel: ACCOUNT XP and the results' tag; the level hexagon, LEVEL n and the gain; the
    /// bar (the saved fill, a hatched ghost for a pending or failed gain, the fill animating inside the supplied thresholds with a
    /// flash and a pop at every level crossed); the progress line; the breakdown as given; the status (SAVED only once confirmed,
    /// CONFIRMING REWARD… while pending, the authority's reason and retry state when failed). Reduced effects: no rise, blink, pop,
    /// burst or flash (the model shows the final state at once). Repaint draws build no strings: every word is the model's.
    /// </summary>
    public static class NativeAccountXpHud
    {
        public static Color Tone(float a=1)=>U.Hex(X.Bar,a);
        public static Color Soft(float a=1)=>U.Hex(X.Soft,a);
        public static Color Glow(float a=1)=>U.Hex(X.Glow,a);
        public static Color Deep(float a=1)=>U.Hex(X.Deep,a);
        public static Color Hatch(float a=1)=>U.Hex(X.Hatch,a);
        public const string Pending="PENDING",NotSaved="NOT SAVED",Mock="MOCK",Close="Close";
        // The level hexagon (24-unit circumradius on a 48 box, point up: the home screen's hexagon buttons' shape, our own markup).
        private const string HexFill="<path d=\"M24 2L43.05 13V35L24 46L4.95 35V13Z\" fill=\"#fff\"/>";
        private const string HexEdge="<path d=\"M24 2L43.05 13V35L24 46L4.95 35V13Z\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2\"/>";
        private const string HexInner="<path d=\"M24 7L38.7 15.5V32.5L24 41L9.3 32.5V15.5Z\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.2\"/>";

        /// <summary>
        /// The panel at l (Docked, Card or Mini). Returns true when Try again was pressed (a failed award the player may retry, with a
        /// bound retry path); `close` when the card's Close was pressed. Controls run on every event, the drawing on Repaint only.
        /// </summary>
        public static bool Panel(in NativeAccountXpLayout l,M m,float a,float now,bool reduced,bool retryBound,out bool close)
        {
            close=false;if(m==null||!m.Shown||a<=0)return false;
            var p=new Rect(l.Panel.X,l.Panel.Y,l.Panel.W,l.Panel.H);
            if(Event.current.type==EventType.Repaint)
            {
                U.Glow(new Rect(p.x,p.y+14,p.width,p.height),new Color(0,0,0,.42f*a),30);
                H.Panel(p,a,m.Valid?Tone():C.Magenta(),R.Cut,H.Corners.Diagonal,true,.95f);
                Head(l,m,a);Title(l,m,a,reduced);Bar(l,m,a);Progress(l,m,a);
                Rule(l,l.RuleA,a);if(!l.Mini){Lines(l,m,a);Rule(l,l.RuleB,a);}
                Status(l,m,a,now,reduced);
                if(m.Dev&&m.FlawText!=null)U.Text(new Rect(l.InnerX,l.DevY,l.InnerW,l.Compact?12:14),m.FlawText,l.Compact?8.5f:9,C.Magenta(a),TextAnchor.MiddleLeft,bold:true,mono:true);
                if(l.Card)U.Fill(new Rect(l.InnerX,l.FootY+2,l.InnerW,H.Px),C.Edge(.6f*a));
            }
            bool retry=false;
            if(!l.Mini&&m.Valid&&m.Status==AccountXpStatus.Failed&&m.CanRetry)
            {
                float bh=l.Compact?20:24;var b=new Rect(l.InnerX,l.RetryY+(l.Compact?2:2),Mathf.Min(150,l.InnerW),bh);
                retry=U.Btn(b,T.TryAgain,true,retryBound,a,l.Compact?11:12)&&retryBound;
            }
            if(l.Card){float bh=l.Compact?24:28;close=U.Btn(new Rect(l.InnerX+l.InnerW-110,l.FootY+(l.Compact?6:8),110,bh),Close,false,true,a,13);}
            return retry;
        }

        // ACCOUNT XP with the XP mark, the results' tag at the right (MOCK before it for a DEV fixture).
        private static void Head(in NativeAccountXpLayout l,M m,float a)
        {
            float h=l.Compact?14:16,y=l.HeadY;Diamond(new Vector2(l.InnerX+4,y+h/2),l.Compact?3.2f:3.8f,Tone(a));
            U.Text(new Rect(l.InnerX+13,y,l.InnerW-13,h),T.Eyebrow,l.Compact?10:11,Soft(.95f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            float right=l.InnerX+l.InnerW;
            if(m.Mode!=null){float w=U.Measure(m.Mode,9,true,true,.14f).x+18;var t=new Rect(right-w,y-1,w,h+2);H.Tag(t,C.Muted(),a,false);U.Text(t,m.Mode,9,C.Muted(a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.14f);right=t.x-6;}
            if(m.Mock){float w=U.Measure(Mock,9,true,true,.14f).x+16;var t=new Rect(right-w,y-1,w,h+2);H.Tag(t,C.Magenta(),a,false);U.Text(t,Mock,9,C.Magenta(a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.14f);}
        }
        // The level hexagon (popping at every level-up, a burst ring around it), LEVEL n and its line, the gain at the right.
        private static void Title(in NativeAccountXpLayout l,M m,float a,bool reduced)
        {
            float s=l.BadgeSize,y=l.TitleY+(l.TitleH-s)/2;var c=new Vector2(l.InnerX+s/2,y+s/2);bool saved=m.Valid&&m.Status==AccountXpStatus.Confirmed;
            float pop=1,burst=-1;
            if(!reduced&&m.UpAge<M.Pop)pop=1+.3f*(1-NativeUiMotion.OutCubic(m.UpAge/M.Pop));
            if(!reduced&&m.UpAge<M.Burst)burst=m.UpAge/M.Burst;
            if(burst>=0){float r=s*(.55f+.75f*NativeUiMotion.OutCubic(burst));U.Glow(new Rect(c.x-r,c.y-r,2*r,2*r),Glow(.55f*(1-burst)*a),10);}
            U.Push(c,Vector2.zero,pop);
            var hex=new Rect(c.x-s/2,c.y-s/2,s,s);U.Glow(new Rect(hex.x+4,hex.y+4,s-8,s-8),Tone((saved?.3f:.12f)*a),8);
            NativeIcons.DrawMarkup(hex,"xp-hex-fill",HexFill,48,Deep(.95f*a));NativeIcons.DrawMarkup(hex,"xp-hex-edge",HexEdge,48,(m.Usable?Tone(a):C.Muted(.7f*a)));
            NativeIcons.DrawMarkup(hex,"xp-hex-inner",HexInner,48,Glow(.28f*a));
            if(m.Usable)U.Text(hex,m.BadgeText,s*.36f,C.Ink(a),TextAnchor.MiddleCenter,bold:true,mono:true);
            U.Pop();
            float tx=c.x+s/2+12,right=l.InnerX+l.InnerW,top=l.TitleY+(l.Compact?1:3);
            // the gain at the right: XP tone once saved; muted while pending or failed, with its chip under it
            float gs=l.Compact?16:19,gw=0;
            if(m.GainText!=null)
            {
                gw=U.Measure(m.GainText,gs,true,true).x;var gr=new Rect(right-gw,top,gw,gs*1.2f);
                U.Text(gr,m.GainText,gs,saved?Soft(a):C.Muted(a),TextAnchor.MiddleRight,bold:true,mono:true,glow:saved?Tone(.35f*a):default);
                if(!saved&&m.Valid)
                {
                    string tag=m.Status==AccountXpStatus.Failed?NotSaved:Pending;Color tone=m.Status==AccountXpStatus.Failed?C.Danger():C.CyanSoft();
                    float tw=U.Measure(tag,8.5f,true,true,.12f).x+16;var t=new Rect(right-tw,gr.yMax+2,tw,l.Compact?13:15);H.Tag(t,tone,a,false);U.Text(t,tag,8.5f,U.A(tone,a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);
                }
            }
            // LEVEL n, and under it the climb (final), the chain's LEVEL UP ×k (while it runs) or nothing
            float ls=l.Compact?17:21,room=Mathf.Max(40,right-gw-12-tx);string title=m.Usable?m.LevelText:T.Eyebrow;
            float lw=U.Measure(title,ls,true,false,.04f).x;if(lw>room)ls=Mathf.Max(11,ls*room/lw);
            float glow=reduced||m.UpAge>=M.Pop?0:.6f*(1-m.UpAge/M.Pop);
            U.Text(new Rect(tx,top,room,(l.Compact?17:21)*1.2f),title,ls,C.Ink(a),TextAnchor.MiddleLeft,bold:true,tracking:.04f,glow:glow>0?Tone(glow*a):default);
            float sy=top+(l.Compact?17:21)*1.2f+(l.Compact?1:3),ss=l.Compact?9.5f:10.5f;
            if(m.Summary!=null&&m.SummaryIn>0)U.Text(new Rect(tx,sy,room,ss*1.4f),m.Summary,ss,Soft(m.SummaryIn*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.08f);
            else if(m.UpText!=null){float k=reduced||m.UpAge>=M.Pop?1:.6f+.4f*NativeUiMotion.OutCubic(m.UpAge/M.Pop);U.Text(new Rect(tx,sy,room,ss*1.4f),m.UpText,ss,Soft(k*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f,glow:Tone(.4f*k*a));}
        }
        // The bar: the track, the hatched ghost of a pending or failed gain, the saved/animated fill, the level-up flash.
        private static void Bar(in NativeAccountXpLayout l,M m,float a)
        {
            float bh=l.Compact?6:8;var b=new Rect(l.InnerX,l.BarY+(l.Compact?1:1),l.InnerW,bh);
            H.Cut(b,Deep(.9f*a),2);H.Outline(b,C.Edge(.8f*a),2);
            if(m.Fill<0){Dashes(b,C.Muted(.35f*a));return;}
            float f=Mathf.Clamp01(m.Fill),x1=b.x+b.width*f;
            if(m.Ghost>f)
            {
                // Hatched: the gain that is not saved (pending, or failed), from the saved fill to where it would land.
                float g=Mathf.Clamp01(m.Ghost),gx=b.x+b.width*g;bool failed=m.Status==AccountXpStatus.Failed;Color hc=failed?C.Danger(.5f*a):Hatch(.8f*a);
                for(float x=x1+1;x<gx;x+=4)U.Fill(new Rect(x,b.y+1,Mathf.Min(2,gx-x),b.height-2),hc);
                U.Fill(new Rect(gx-H.Px,b.y-2,H.Snap(1),b.height+4),failed?C.Danger(.7f*a):Soft(.7f*a));
            }
            if(f>0)
            {
                var fill=new Rect(b.x,b.y,b.width*f,b.height);U.Glow(fill,Tone(.28f*a),6);U.HGradient(fill,Tone(.95f*a),Glow(a));
                U.Fill(new Rect(fill.x,fill.y,fill.width,H.Snap(1)),C.Ink(.35f*a));
                if(f<1)U.Fill(new Rect(fill.xMax-H.Px,b.y-2,H.Snap(2),b.height+4),C.Ink(a));
            }
            for(int i=1;i<4;i++){float x=b.x+b.width*i/4;U.Fill(new Rect(x,b.y,H.Px,b.height),C.Void(.55f*a));}
            if(m.UpAge<M.Pop)U.Fill(b,C.Ink(.55f*(1-m.UpAge/M.Pop)*a));
        }
        private static void Progress(in NativeAccountXpLayout l,M m,float a)
        {
            float h=l.Compact?14:16,y=l.ProgressY,k=m.ProgressIn*a;if(k<=0)return;
            if(m.ProgressText!=null)U.Text(new Rect(l.InnerX,y,l.InnerW*.62f,h),m.ProgressText,l.Compact?9.5f:10.5f,C.Muted(k),TextAnchor.MiddleLeft,bold:true,mono:true);
            if(m.NextText!=null)U.Text(new Rect(l.InnerX+l.InnerW*.4f,y,l.InnerW*.6f,h),m.NextText,l.Compact?9:9.5f,C.Muted(.85f*k),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.08f);
        }
        private static void Rule(in NativeAccountXpLayout l,float y,float a)=>U.Fill(new Rect(l.InnerX,y,l.InnerW,H.Px),C.Edge(.75f*a));
        // The breakdown: each line's label as account code gave it and its amount; rising in, its amount locking to the XP tone once
        // saved. A clipped list ends with "+n MORE"; an empty one says so.
        private static void Lines(in NativeAccountXpLayout l,M m,float a)
        {
            if(!m.Usable)return;float ls=l.Compact?11.5f:13,as_=l.Compact?11.5f:13;
            if(m.Rows==0){var r0=l.LineBox(0);U.Text(new Rect(r0.X,r0.Y,r0.W,r0.H),T.NoLines,l.Compact?9.5f:10.5f,C.Muted(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);return;}
            int fit=Mathf.Max(1,l.ShownLines),rows=m.Rows,lines=rows<=fit?m.Lines:fit-1;
            for(int i=0;i<lines;i++)
            {
                var bx=l.LineBox(i);float e=m.LineIn[i];if(e<=0)continue;float ra=a*e,y=bx.Y+(1-e)*8,lk=m.LineLock[i];
                Diamond(new Vector2(bx.X+4,y+bx.H/2),2.6f,U.Mix(C.Muted(ra),Tone(ra),lk));
                float aw=U.Measure(m.LineAmount[i],as_,true,true).x,room=bx.W-14-aw-10;
                Color at=m.LineNegative[i]?U.Mix(C.Muted(ra),C.Danger(ra),lk):U.Mix(C.Muted(ra),Soft(ra),lk);
                if(lk>0&&lk<1)U.Glow(new Rect(bx.X+bx.W-aw,y+4,aw,bx.H-8),Tone(.4f*(1-Mathf.Abs(2*lk-1))*ra),6);
                U.Text(new Rect(bx.X+bx.W-aw-2,y,aw+2,bx.H),m.LineAmount[i],as_,at,TextAnchor.MiddleRight,bold:true,mono:true);
                U.BeginBand(new Rect(bx.X+12,y,room,bx.H));U.Text(new Rect(bx.X+14,y,room+200,bx.H),m.LineLabel[i],ls,C.Ink(.92f*ra),TextAnchor.MiddleLeft);U.EndBand();
            }
            int more=rows<=fit?m.More:m.Lines+m.More-lines;string mt=rows<=fit?m.MoreText:T.More(more);
            if(mt!=null){var bx=l.LineBox(lines);U.Text(new Rect(bx.X+14,bx.Y,bx.W-14,bx.H),mt,l.Compact?9:10,C.Muted(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);}
        }
        // The status: SAVED (confirmed), CONFIRMING REWARD… (pending, blinking), or the failure with its reason and retry state.
        private static void Status(in NativeAccountXpLayout l,M m,float a,float now,bool reduced)
        {
            float h=l.Compact?18:20,y=l.StatusY,ic=l.Compact?13:15,fs=l.Compact?10:11;string icon;Color tone;float ia=a;
            if(!m.Valid){tone=C.Magenta();icon="circle-alert";}
            else switch(m.Status)
            {
                case AccountXpStatus.Confirmed:tone=C.Good();icon="check";ia=a*(m.Final?1:.55f+.45f*m.SummaryIn);break;
                case AccountXpStatus.Pending:tone=C.CyanSoft();icon="hourglass";ia=a*(reduced?1:.55f+.45f*Mathf.Abs(Mathf.Sin(now*2.6f)));break;
                default:tone=C.Danger();icon="circle-alert";break;
            }
            NativeIcons.Draw(new Rect(l.InnerX,y+(h-ic)/2,ic,ic),icon,U.A(tone,ia));
            // The status line has the row to itself (its size fitted to the column); the attempt sits on the detail or retry row.
            float room=l.InnerW-ic-8,sw=U.Measure(m.StatusText,fs,true,true,.12f).x;if(sw>room)fs=Mathf.Max(8,fs*room/sw);
            U.Text(new Rect(l.InnerX+ic+8,y,room,h),m.StatusText,fs,U.A(tone,m.Status==AccountXpStatus.Confirmed?ia:a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
            if(l.Mini)return;
            float dh=l.Compact?14:16,aw=m.AttemptText!=null?U.Measure(m.AttemptText,9,true,true,.1f).x:0;bool failed=m.Valid&&m.Status==AccountXpStatus.Failed;
            if(aw>0)U.Text(failed?new Rect(l.InnerX+l.InnerW-aw,l.RetryY,aw,l.Compact?24:28):new Rect(l.InnerX+l.InnerW-aw,l.DetailY,aw,dh),m.AttemptText,9,C.Muted(a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.1f);
            if(m.DetailText!=null)
            {
                int n=m.Status==AccountXpStatus.Failed?2:1;var d=new Rect(l.InnerX,l.DetailY,l.InnerW-(failed||aw<=0?0:aw+8),n*dh);
                U.BeginBand(d);U.Text(d,m.DetailText,l.Compact?10:11,C.Muted(a),TextAnchor.UpperLeft,wrap:n>1);U.EndBand();
            }
            if(m.Valid&&m.Status==AccountXpStatus.Failed&&!m.CanRetry&&m.RetryText!=null)
            {
                bool auto=m.Retry==AccountXpRetry.Auto;Color rt=auto?C.CyanSoft():m.Retry==AccountXpRetry.None?C.Danger(.85f):C.Muted();float rh=l.Compact?24:28,rx=l.InnerX;
                if(auto){float s=l.Compact?11:13,spin=reduced?0:now*200%360;var rc=new Vector2(rx+s/2,l.RetryY+rh/2);U.Push(rc,Vector2.zero,1,spin);NativeIcons.Draw(new Rect(rc.x-s/2,rc.y-s/2,s,s),"rotate-cw",U.A(rt,a));U.Pop();rx+=s+8;}
                U.Text(new Rect(rx,l.RetryY,l.InnerX+l.InnerW-rx-(m.AttemptText!=null?U.Measure(m.AttemptText,9,true,true,.1f).x+8:0),rh),m.RetryText,l.Compact?9.5f:10.5f,U.A(rt,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
            }
        }
        private static void Diamond(Vector2 c,float r,Color col){U.Push(c,Vector2.zero,1,45);U.Fill(new Rect(c.x-r,c.y-r,2*r,2*r),col);U.Pop();}
        private static void Dashes(Rect b,Color c){for(float x=b.x+2;x<b.xMax-2;x+=9)U.Fill(new Rect(x,b.y+b.height/2-.5f,Mathf.Min(5,b.xMax-2-x),H.Snap(1)),c);}

        // ---- the menu's account level -------------------------------------------------------------------------------------------------
        /// <summary>The compact level row's size (the home nameplate's third line).</summary>
        public const float BadgeW=196,BadgeH=20;
        /// <summary>
        /// The account level under the pilot's nameplate: a small XP hexagon with the level, LEVEL n, a thin bar of the saved progress
        /// (none when the thresholds do not hold the total, full at the top level) and a PENDING marker while awards wait for
        /// confirmation (never added to the bar). MOCK beside it for a DEV fixture. Nothing when the summary is unknown.
        /// </summary>
        public static void Badge(float x,float y,NativeAccountXpBadge b,float a,float now,bool reduced)
        {
            if(b==null||!b.Shown||a<=0||Event.current.type!=EventType.Repaint)return;
            float s=BadgeH;var hex=new Rect(x,y,s,s);
            NativeIcons.DrawMarkup(hex,"xp-hex-fill",HexFill,48,Deep(.95f*a));NativeIcons.DrawMarkup(hex,"xp-hex-edge",HexEdge,48,Tone(a));
            U.Text(hex,b.Number,b.Level>=100?6.5f:8,C.Ink(a),TextAnchor.MiddleCenter,bold:true,mono:true);
            float tx=x+s+8,lw=U.Measure(b.LevelText,9.5f,true,false,.2f).x;
            U.Text(new Rect(tx,y,lw+6,s),b.LevelText,9.5f,Soft(a),TextAnchor.MiddleLeft,bold:true,tracking:.2f,shadow:C.Void(.8f*a));
            float bx=tx+lw+10,right=x+BadgeW;
            if(b.HasPending)
            {
                float pw=U.Measure(Pending,8,true,true,.12f).x+14;var t=new Rect(right-pw,y+(s-13)/2,pw,13);float k=reduced?1:.6f+.4f*Mathf.Abs(Mathf.Sin(now*2.2f));
                H.Tag(t,C.CyanSoft(),a*k,false);U.Text(t,Pending,8,C.CyanSoft(k*a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);right=t.x-6;
            }
            if(b.Bar&&right-bx>24)
            {
                var bar=new Rect(bx,y+s/2-1.5f,right-bx,3);U.Fill(bar,Deep(.9f*a));float f=Mathf.Clamp01(b.Fill);
                if(f>0){var fill=new Rect(bar.x,bar.y,bar.width*f,bar.height);U.Glow(fill,Tone(.3f*a),4);U.HGradient(fill,Tone(.95f*a),Glow(a));}
            }
            if(b.Mock){float mw=U.Measure(Mock,8,true,true,.12f).x+12;var t=new Rect(x+BadgeW+6,y+(s-13)/2,mw,13);H.Tag(t,C.Magenta(),a,false);U.Text(t,Mock,8,C.Magenta(a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);}
        }

        /// <summary>The DEV preview's banner over the panel (magenta, the other DEV previews' look).</summary>
        public static void Banner(float cx,float y,float a)
        {
            if(Event.current.type!=EventType.Repaint||a<=0)return;string label=NativeAccountXpFixtures.Banner;var size=U.Measure(label,11,true,true,.12f);
            var r=new Rect(cx-size.x/2-14,y,size.x+28,24);U.Fill(r,C.Void(.75f*a));H.Outline(r,C.Magenta(.9f*a),0,H.Corners.None);U.Text(r,label,11,C.Magenta(a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);
        }
    }
}
