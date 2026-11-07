using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using R=ChipArena.UnityConversion.NativeHudCyber;
using T=ChipArena.UnityConversion.NativeScreenTransition;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The stage loading screen (Core NativeStageLoading, StageLoadView, NativeLoadingLayout, NativeStageSchematic), in the cyber
    /// HUD kit over the menu transition's shutters (NativeScreenTransitionHud.Shutter): the bands slam shut, a cyan impact flash
    /// runs along their seams, and the screen's content comes up on them: the eyebrow, the stage's name typed on behind a
    /// scanning cursor, the chip-pin rule, the mode tag and the map's tag line; for an Expedition load the party panel under them
    /// (NativePartyLoadingHud), else the pilots' readiness when the game holds the stage; at the right a holographic scan of the
    /// stage's plan, its blocks lit as the preload's sweep passes them, spawns and hazards marked; along the foot a spinner, the
    /// step, the percentage, a segmented bar and the tip. On the way out the content fades first, then the bands run out over the
    /// stage. A return shows a compact centred variant. Reduced effects: a fade through the void, no sweep, glitch or drift.
    /// Repaint only; nothing is allocated per frame.
    /// </summary>
    public static class NativeStageLoadingHud
    {
        /// <summary>The share of the reveal the content takes to fade before the shutters move, and the bands' stagger share.</summary>
        public const float ContentOut=.2f,Lag=.05f,ContentIn=.22f,Flash=.32f;
        public const string Brand="CHIP ARENA",Link="STAGE LINK";
        private static readonly float[] PoolAt={0f,.4f,1f},PoolAlpha={1f,.5f,0f},WashAt={0f,.5f,1f},WashAlpha={1f,.4f,0f};

        public static void Draw(NativeStageLoading load,StageLoadView v,string step,string percent,float shown,float W,float Hh,float now,
            string party,string partyCount,int ready,int required,bool waiting,NativePartyLoadModel roster=null)
        {
            if(Event.current.type!=EventType.Repaint||!load.Visible)return;bool reduced=load.Reduced;
            // ---- the base: shutters (or the reduced fade) ----
            float a=ContentAlpha(load);
            if(reduced)U.Fill(new Rect(0,0,W,Hh),C.Night(load.Coverage));
            else
            {
                bool revealing=load.Phase==StageLoadPhase.Reveal;float phase=load.Phase==StageLoadPhase.Cover?load.CoverProgress:revealing?Mathf.Clamp01((load.RevealProgress-ContentOut)/(1-ContentOut)):1;
                float px=H.Px,cut=T.Chamfer(Hh/T.Bands,U.S),k=cut/Mathf.Max(1,W);
                for(int i=0;i<T.Bands;i++)
                {
                    T.Span(i,phase,Lag,revealing,k,false,out float l,out float r);if(r-l<=0)continue;T.BandRow(i,Hh,px,out float top,out float h);
                    NativeScreenTransitionHud.Shutter(new Rect(l*W,top,(r-l)*W,h),i,!revealing,cut,1,T.ShiftOf(i,phase,Lag,revealing,k,false)*W,W,1-.7f*a,load.Phase==StageLoadPhase.Hold?0:T.MotionOf(i,phase,Lag));
                }
                // the impact: the seams flash as the bands land (or the screen slams shut at once)
                if(load.Phase==StageLoadPhase.Hold&&load.Age<Flash)
                {
                    float f=1-load.Age/Flash;f*=f;
                    for(int i=1;i<T.Bands;i++){float y=Hh*i/T.Bands;U.Glow(new Rect(0,y,W,H.Snap(1)),C.Cyan(.35f*f),8);U.Fill(new Rect(0,y,W,H.Snap(1)),C.CyanSoft(.8f*f));}
                }
            }
            if(a<=0)return;
            var l0=NativeLoadingLayout.Measure(W,Hh,U.SafeX,U.SafeW,v.Kind!=StageLoadKind.Return,roster!=null?roster.Count:0);
            Frame(W,Hh,a,reduced,now);
            if(v.Kind==StageLoadKind.Return){Compact(v,step,percent,shown,W,Hh,now,a,reduced);return;}
            Backdrop(l0,v,a,reduced,now);
            TitleBlock(l0,v,a,now,reduced);
            if(roster!=null)NativePartyLoadingHud.Draw(l0.Roster,roster,a,now,reduced);else if(waiting&&party!=null)Party(l0,party,partyCount,ready,required,a);
            if(l0.Scan){if(v.Scan!=null)Scan(l0.Panel,v,shown,a,now,reduced);else ScanPending(l0.Panel,a,now,reduced);}
            ProgressRow(l0,step,percent,shown,a,now,reduced);
            if(v.Tip!=null)TipLine(l0.Tip,v.Tip,a);
        }
        /// <summary>The content's alpha: up over ContentIn once the screen is closed, down over the first ContentOut of the reveal.</summary>
        public static float ContentAlpha(NativeStageLoading load)
        {
            switch(load.Phase)
            {
                case StageLoadPhase.Hold:return T.Smooth(load.Age/ContentIn);
                case StageLoadPhase.Reveal:return 1-T.Smooth(load.RevealProgress/ContentOut);
                default:return 0;
            }
        }
        // The frame: tick rules along the top and foot of the safe band, the brand and the link label.
        private static void Frame(float W,float Hh,float a,bool reduced,float now)
        {
            float x0=U.SafeX+Mathf.Max(28,U.SafeW*.055f),x1=U.SafeX+U.SafeW-Mathf.Max(28,U.SafeW*.055f),w=x1-x0;
            for(int i=0;i<2;i++)
            {
                float y=i==0?30:Hh-24;U.Fill(new Rect(x0,y,w,H.Snap(1)),C.Edge(.75f*a));
                for(int t=0;t<=24;t++){float x=x0+w*t/24;bool major=t%6==0;U.Fill(new Rect(x,y-(major?4:1),H.Snap(1),major?9:3),C.CyanSoft((major?.4f:.22f)*a));}
                U.Fill(new Rect(x0,y-H.Px,40,H.Snap(2)),C.Cyan(.9f*a));U.Fill(new Rect(x1-16,y-H.Px,16,H.Snap(2)),C.Magenta(.8f*a));
            }
            float tile=12;var b=new Rect(x0,10,tile,tile);U.Fill(b,C.Cyan(a));U.Fill(new Rect(b.x+1.5f,b.y+1.5f,tile-3,tile-3),C.Navy(a));U.Fill(new Rect(b.x+3.5f,b.y+3.5f,tile-7,tile-7),C.Cyan(a));
            U.Text(new Rect(x0+tile+8,8,240,16),Brand,10,C.Ink(.85f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.24f);
            float blink=reduced?1:.55f+.45f*Mathf.Abs(Mathf.Sin(now*3.1f));U.Fill(new Rect(x1-6,13,6,6),C.Good(blink*a));
            U.Text(new Rect(x1-240,8,226,16),Link,10,C.CyanSoft(.8f*a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.24f);
        }
        // Behind the content: the stage's accent pooled under the scan, a horizon rule and a slow chevron drift.
        private static void Backdrop(NativeLoadingLayout l,StageLoadView v,float a,bool reduced,float now)
        {
            var accent=U.Hex(v.Accent);
            if(l.Scan){var p=l.Panel;float rr=Mathf.Max(p.W,p.H)*.9f;U.RadialStops(new Rect(p.X+p.W/2-rr,p.Y+p.H/2-rr,2*rr,2*rr),U.A(accent,.10f*a),PoolAt,PoolAlpha);}
            float cx=l.Text.X+l.Text.W*.3f,ry=l.Text.Y+l.Text.H*.5f,rad=l.Text.W*.7f;U.RadialStops(new Rect(cx-rad,ry-rad,2*rad,2*rad),C.Cyan(.05f*a),WashAt,WashAlpha);
            float drift=reduced?0:(now*26)%64,y=l.Bar.Y-64;
            for(float x=l.Bar.X-64+drift;x<l.Bar.Right;x+=64){if(x<l.Bar.X)continue;U.Fill(new Rect(x,y,22,H.Snap(2)),C.Cyan(.07f*a));}
        }
        // The eyebrow, the name typed on, the rule, the mode tag and the tag line.
        private static void TitleBlock(NativeLoadingLayout l,StageLoadView v,float a,float now,bool reduced)
        {
            float x=l.Text.X,w=l.Text.W;
            U.Fill(new Rect(x,l.Eyebrow+5,H.Snap(6),H.Snap(6)),C.Cyan(a));
            U.Text(new Rect(x+16,l.Eyebrow,w-16,16),v.Eyebrow,12,C.CyanSoft(.95f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.26f);
            if(v.Title==null)
            {
                // the stage is not in place yet: a scanning placeholder
                float k=reduced?.5f:(now*1.6f)%1;var bar=new Rect(x,l.Title+l.TitleSize*.3f,w*.6f,l.TitleSize*.5f);U.Fill(bar,C.Navy(.8f*a));
                U.Fill(new Rect(bar.x+bar.width*k*.8f,bar.y,bar.width*.2f,bar.height),C.Cyan(.18f*a));
            }
            else
            {
                if(v.TitleSize<=0){float fit=U.Measure(v.Title,l.TitleSize,true,false,.04f).x;v.TitleSize=fit>w?Mathf.Max(22,l.TitleSize*w/fit):l.TitleSize;}
                float size=Mathf.Min(v.TitleSize,l.TitleSize),age=float.IsNaN(v.TitleAt)?9:now-v.TitleAt,type=reduced?1:Mathf.Clamp01(age/.42f);var line=new Rect(x,l.Title,w,size*1.2f);
                float tw=Mathf.Min(w,U.Measure(v.Title,size,true,false,.04f).x+4),shown=tw*T.OutCubic(type);
                U.BeginBand(new Rect(x-4,line.y-6,shown+4,line.height+12));
                if(!reduced&&age<R.GlitchLife)H.Glitch(line,v.Title,size,age,a,TextAnchor.MiddleLeft,false,true,false,.04f);
                U.Text(line,v.Title,size,C.Ink(a),TextAnchor.MiddleLeft,bold:true,tracking:.04f,glow:C.Cyan(.45f*a),shadow:C.Void(.8f*a));
                U.EndBand();
                if(type<1){var cur=new Rect(x+shown+2,line.y+size*.12f,Mathf.Max(3,size*.09f),size*.95f);U.Glow(cur,C.Cyan(.5f*a),10);U.Fill(cur,C.Cyan(a));}
            }
            // the chip-pin rule
            float rule=Mathf.Min(w,420)*(reduced?1:T.OutCubic(a)),ry=l.Rule;
            U.Fill(new Rect(x,ry,rule,H.Px),C.CyanSoft(.3f*a));U.Fill(new Rect(x,ry-H.Px,36,H.Snap(2)),C.Cyan(a));
            for(float p=x+48;p<x+rule-18;p+=9)U.Fill(new Rect(p,ry+H.Px,H.Px,3),C.CyanSoft(.28f*a));if(rule>60)U.Fill(new Rect(x+rule-14,ry-H.Px,14,H.Snap(2)),C.Magenta(.85f*a));
            // the mode tag
            if(v.Mode!=null){float mw=U.Measure(v.Mode,12,true,true,.18f).x+36;var tag=new Rect(x,l.Mode,mw,26);H.Tag(tag,C.Cyan(),a);U.Text(new Rect(tag.x+14,tag.y,mw-14,26),v.Mode,12,C.Cyan(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);}
            if(v.Tag!=null&&!l.TagHidden)U.Text(new Rect(x,l.Tag,w,40),v.Tag,15,C.Muted(.95f*a),TextAnchor.UpperLeft,wrap:true);
        }
        // The game's hold: one pip per client (at most eight), the line and the count.
        private static void Party(NativeLoadingLayout l,string line,string count,int ready,int required,float a)
        {
            float x=l.Text.X,y=l.Party;int n=Mathf.Min(8,Mathf.Max(0,required));float pip=22,gap=6;
            for(int i=0;i<n;i++){var c=new Rect(x+i*(pip+gap),y+5,pip,10);if(i<ready){U.Fill(c,C.Good(a));U.Glow(c,C.Good(.25f*a),4);}else{U.Fill(c,C.Navy(.9f*a));H.Outline(c,C.Edge(a),0,H.Corners.None);}}
            float tx=x+(n>0?n*(pip+gap)+8:0);bool done=required>0&&ready>=required;
            U.Text(new Rect(tx,y,l.Text.W,20),line,13,done?C.Good(a):C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
            if(!string.IsNullOrEmpty(count)){float cx=tx+U.Measure(line,13,true,true,.12f).x+14;U.Text(new Rect(cx,y,Mathf.Max(0,l.Text.Right-cx),20),count,13,C.Ink(a),TextAnchor.MiddleLeft,bold:true,mono:true);}
        }
        // The holographic plan: the panel, its header, a grid, every block lit once the sweep passed it, spawns and hazards, the sweep.
        private static void Scan(LoadingBox box,StageLoadView v,float shown,float a,float now,bool reduced)
        {
            var s=v.Scan;var r=new Rect(box.X,box.Y,box.W,box.H);var accent=U.Hex(v.Accent);
            U.Glow(new Rect(r.x,r.y+20,r.width,r.height),new Color(0,0,0,.4f*a),40);
            H.Panel(r,a,C.Cyan(),R.Cut,H.Corners.Diagonal,true,.86f);
            U.Text(new Rect(r.x+18,r.y+10,r.width-36,16),NativeStageLabels.Scan,10,C.CyanSoft(.9f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.24f);
            if(v.Count!=null)U.Text(new Rect(r.x+18,r.y+10,r.width-36,16),v.Count,10,C.Muted(a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.16f);
            var inner=new Rect(r.x+18,r.y+34,r.width-36,r.height-52);float aspect=Mathf.Max(.2f,s.Aspect),pw=inner.width,ph=pw/aspect;if(ph>inner.height){ph=inner.height;pw=ph*aspect;}
            var plan=new Rect(inner.center.x-pw/2,inner.center.y-ph/2,pw,ph);
            // the grid, tenths
            for(int i=0;i<=10;i++){float gx=plan.x+plan.width*i/10,gy=plan.y+plan.height*i/10;float ga=i%5==0?.22f:.1f;U.Fill(new Rect(gx,plan.y,H.Px,plan.height),C.Edge(ga*a));U.Fill(new Rect(plan.x,gy,plan.width,H.Px),C.Edge(ga*a));}
            float sweep=reduced?shown:Mathf.Clamp01(shown)*1.04f-.02f,px=H.Px;var box4=s.Box;var lv=s.Level;var kind=s.Kind;
            var deep=C.CyanDeep();var soft=C.CyanSoft();
            // A hologram: the slabs as the plate, decks as faint panes, volumes filled by height with a lit edge, cover in the
            // stage's accent; unscanned blocks a dim ghost. Edges only where a block is big enough to read one.
            float edgeMin=6*px;
            for(int i=0;i<s.Count;i++)
            {
                float u=box4[i*4],vv=box4[i*4+1],bw=box4[i*4+2],bh=box4[i*4+3];bool scanned=u+bw*.5f<=sweep;byte kd=kind[i];
                var q=new Rect(plan.x+u*plan.width,plan.y+vv*plan.height,Mathf.Max(px,bw*plan.width),Mathf.Max(px,bh*plan.height));
                if(kd==NativeStageSchematic.Plate){U.Fill(q,scanned?C.Indigo(.7f*a):C.Navy(.5f*a));continue;}
                if(!scanned){U.Fill(q,C.Edge(.16f*a));continue;}
                float level=lv[i];bool edge=q.width>=edgeMin&&q.height>=edgeMin;
                if(kd==NativeStageSchematic.Deck){U.Fill(q,C.Cyan(.07f*a));if(edge)H.Outline(q,C.CyanSoft(.3f*a),0,H.Corners.None);continue;}
                Color c=kd==NativeStageSchematic.Cover?accent:Color.Lerp(deep,soft,level);
                U.Fill(q,U.A(c,(kd==NativeStageSchematic.Cover?.4f:.16f+.34f*level)*a));
                if(edge)H.Outline(q,U.A(c,(.45f+.45f*level)*a),0,H.Corners.None);else if(level>.5f)U.Fill(q,U.A(c,.35f*a));
            }
            var sp=s.Spawns;for(int i=0;i+1<sp.Length;i+=2){if(sp[i]>sweep)continue;float d=5;var c=new Rect(plan.x+sp[i]*plan.width-d/2,plan.y+sp[i+1]*plan.height-d/2,d,d);U.Glow(c,C.Cyan(.4f*a),4);U.Fill(c,C.Cyan(a));}
            var hz=s.Hazards;for(int i=0;i+1<hz.Length;i+=2){if(hz[i]>sweep)continue;float d=7;var c=new Rect(plan.x+hz[i]*plan.width-d/2,plan.y+hz[i+1]*plan.height-d/2,d,d);H.Outline(c,C.Warn(.9f*a),0,H.Corners.None);U.Fill(new Rect(c.x+2.5f,c.y+2.5f,2,2),C.Warn(a));}
            // the sweep: a lit line with a trail, while the preload runs
            if(!reduced&&sweep<1)
            {
                float sx=plan.x+plan.width*Mathf.Clamp01(sweep);float trail=Mathf.Min(70,sx-plan.x);
                if(trail>0)U.HGradient(new Rect(sx-trail,plan.y,trail,plan.height),Color.clear,C.Cyan(.12f*a));
                U.Glow(new Rect(sx-1,plan.y,2,plan.height),C.Cyan(.45f*a),10);U.Fill(new Rect(sx-px,plan.y,H.Snap(2),plan.height),C.CyanSoft(.9f*a));
                U.Fill(new Rect(sx-4,plan.y-4,8,H.Snap(2)),C.Cyan(a));U.Fill(new Rect(sx-4,plan.yMax+2,8,H.Snap(2)),C.Cyan(a));
            }
            H.Brackets(new Rect(plan.x-6,plan.y-6,plan.width+12,plan.height+12),U.A(accent,.7f*a),12,H.Corners.None);
        }
        // The scan's panel before the stage is in place: its frame and a searching sweep.
        private static void ScanPending(LoadingBox box,float a,float now,bool reduced)
        {
            var r=new Rect(box.X,box.Y,box.W,box.H);H.Panel(r,a,C.Cyan(),R.Cut,H.Corners.Diagonal,true,.86f);
            U.Text(new Rect(r.x+18,r.y+10,r.width-36,16),NativeStageLabels.Scan,10,C.CyanSoft(.9f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.24f);
            var inner=new Rect(r.x+18,r.y+34,r.width-36,r.height-52);
            for(int i=0;i<=10;i++){U.Fill(new Rect(inner.x+inner.width*i/10,inner.y,H.Px,inner.height),C.Edge(.12f*a));U.Fill(new Rect(inner.x,inner.y+inner.height*i/10,inner.width,H.Px),C.Edge(.12f*a));}
            float k=reduced?.5f:(now*.7f)%1;U.Fill(new Rect(inner.x+inner.width*k,inner.y,H.Snap(2),inner.height),C.CyanSoft(.5f*a));
            U.Text(inner,NativeStageLabels.Locking,12,C.CyanSoft(.7f*a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.3f);
        }
        // The foot: the spinner, the step, the percentage, the bar.
        private static void ProgressRow(NativeLoadingLayout l,string step,string percent,float shown,float a,float now,bool reduced)
        {
            Spinner(new Vector2(l.Spinner.X+l.Spinner.W/2,l.Spinner.Y+l.Spinner.H/2),l.Spinner.W/2,shown,a,now,reduced);
            if(step!=null)U.Text(new Rect(l.Label.X,l.Label.Y,l.Label.W,l.Label.H),step,12,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);
            if(percent!=null)U.Text(new Rect(l.Label.X,l.Label.Y,l.Label.W,l.Label.H),percent,13,C.Ink(a),TextAnchor.MiddleRight,bold:true,mono:true);
            Bar(new Rect(l.Bar.X,l.Bar.Y,l.Bar.W,l.Bar.H),shown,a,now,reduced);
        }
        public static void Bar(Rect b,float shown,float a,float now,bool reduced)
        {
            U.Fill(b,C.Navy(.95f*a));float f=Mathf.Clamp01(shown);var fill=new Rect(b.x,b.y,b.width*f,b.height);
            if(f>0){U.Glow(fill,C.Cyan(.25f*a),6);U.Fill(fill,C.Cyan(a));}
            // the segments: a void notch every 5%
            for(int i=1;i<20;i++){float x=b.x+b.width*i/20;U.Fill(new Rect(x,b.y,H.Px,b.height),C.Void(.9f*a));}
            if(f>0&&f<1)U.Fill(new Rect(fill.xMax-H.Px,b.y-3,H.Snap(2),b.height+6),C.Ink(a));
            if(!reduced&&f>.05f){float k=(now*.9f)%1,cw=Mathf.Min(90,fill.width*.4f),cx=fill.x+(fill.width+cw)*k-cw;var c=Rect.MinMaxRect(Mathf.Max(fill.x,cx),b.y,Mathf.Min(fill.xMax,cx+cw),b.yMax);if(c.width>0)U.HGradient(c,Color.clear,new Color(1,1,1,.55f*a));}
            U.Fill(new Rect(b.x,b.yMax+3,b.width,H.Px),C.Edge(.5f*a));
        }
        private static void Spinner(Vector2 c,float radius,float shown,float a,float now,bool reduced)
        {
            const int n=12;float head=reduced?Mathf.Floor(shown*n):Mathf.Floor(now*14)%n;
            for(int i=0;i<n;i++)
            {
                float ang=i*Mathf.PI*2/n-Mathf.PI/2;float d=reduced?(i<=head?1:.2f):Mathf.Repeat(head-i,n)/n;float al=reduced?d:Mathf.Max(.15f,1-d*1.4f);
                var p=new Vector2(c.x+Mathf.Cos(ang)*radius,c.y+Mathf.Sin(ang)*radius);float s=i%3==0?3.2f:2.4f;
                U.Fill(new Rect(p.x-s/2,p.y-s/2,s,s),i%3==0?C.Cyan(al*a):C.CyanSoft(al*.85f*a));
            }
            float core=radius*.62f;var t=new Rect(c.x-core/2,c.y-core/2,core,core);U.Fill(t,C.Cyan(a));U.Fill(new Rect(t.x+1.5f,t.y+1.5f,core-3,core-3),C.Navy(a));U.Fill(new Rect(t.x+3,t.y+3,core-6,core-6),C.Cyan(.9f*a));
        }
        private static void TipLine(LoadingBox b,string tip,float a)
        {
            var key=new Rect(b.X,b.Y,H.KeyWidth(NativeStageLabels.TipKey,18),18);H.Key(key,NativeStageLabels.TipKey,a,C.CyanSoft());
            U.Text(new Rect(key.xMax+10,b.Y,b.W-key.width-10,b.H),tip,13,C.Muted(a),TextAnchor.MiddleLeft);
        }
        // A return: the eyebrow and the destination centred, the step, the percentage and a short bar under them.
        private static void Compact(StageLoadView v,string step,string percent,float shown,float W,float Hh,float now,float a,bool reduced)
        {
            float cy=Hh*.46f,size=Mathf.Clamp(Hh*.06f,28,56);
            U.Text(new Rect(0,cy-size*.6f-28,W,16),v.Eyebrow,12,C.CyanSoft(.95f*a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.3f);
            if(v.Title!=null)U.Text(new Rect(0,cy-size*.6f,W,size*1.2f),v.Title,size,C.Ink(a),TextAnchor.MiddleCenter,bold:true,tracking:.08f,glow:C.Cyan(.45f*a),shadow:C.Void(.8f*a));
            float bw=Mathf.Min(420,W*.5f),bx=W/2-bw/2,by=cy+size*.75f+28;
            Spinner(new Vector2(bx+8,by-20),9,shown,a,now,reduced);
            if(step!=null)U.Text(new Rect(bx+26,by-28,bw-26,16),step,11,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
            if(percent!=null)U.Text(new Rect(bx,by-28,bw,16),percent,12,C.Ink(a),TextAnchor.MiddleRight,bold:true,mono:true);
            Bar(new Rect(bx,by,bw,3),shown,a,now,reduced);
        }
    }
}
