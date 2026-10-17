using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using T=ChipArena.UnityConversion.NativeScreenTransition;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Draws the menu screen transition (Core NativeScreenTransition) and the shutter base the stage loading screen stands on, in
    /// the cyber HUD kit: seven void bands with static scanlines, a hairline seam and a circuit trace each, their lead cut as a
    /// chevron (NativeHudChrome corners, crisp at any resolution) and lit by a bright cyan scan edge with a white core; the
    /// destination's eyebrow and title on the middle band (they slide with it), a chromatic glitch at the swap (cyan and
    /// magenta seam slices and the title's split copies) and a scan line down the closed screen. Reduced effects: a fade
    /// through the void with the title, no motion. Repaint only; every string is a constant, nothing is allocated.
    /// </summary>
    public static class NativeScreenTransitionHud
    {
        public const string Routing="ROUTING";

        /// <summary>The whole transition at <paramref name="now"/>, over a <paramref name="W"/> x <paramref name="Hh"/> canvas.</summary>
        public static void Draw(T t,float now,float W,float Hh,string eyebrow,string title)
        {
            if(Event.current.type!=EventType.Repaint||!t.Active(now))return;
            if(t.Reduced){Faded(W,Hh,t.Fade(now),t.TitleAlpha(now),eyebrow,title);return;}
            float px=H.Px,cut=T.Chamfer(Hh/T.Bands,U.S),k=cut/Mathf.Max(1,W),glitch=t.Glitch(now);bool leadLeft=t.LeadsLeft(now);
            for(int i=0;i<T.Bands;i++)
            {
                t.Band(i,now,k,out float l,out float r);if(r-l<=0)continue;T.BandRow(i,Hh,px,out float top,out float h);
                float jog=glitch>0?(i%2==0?1:-1)*glitch*(i==2||i==4?9:3):0;
                var band=new Rect(l*W+jog,top,(r-l)*W,h);float shift=t.Shift(i,now,k)*W+jog;Shutter(band,i,leadLeft,cut,1,shift,W,1,t.Motion(i,now));
                Motif(band,W,Hh,shift,1);
                if(i==T.Bands/2)Title(band,W,Hh,t.Shift(i,now,k)*W+jog,eyebrow,title,t.TitleAlpha(now),now-(t.SwapAt-T.GlitchLead),false);
            }
            if(glitch>0)Glitch(W,Hh,glitch,now);
            float scan=t.Scan(now);if(scan>=0)ScanLine(W,Hh,scan,1);
        }
        /// <summary>The reduced variant: the void at <paramref name="alpha"/>, the title faded in place.</summary>
        public static void Faded(float W,float Hh,float alpha,float titleAlpha,string eyebrow,string title)
        {
            if(alpha<=0)return;U.Fill(new Rect(0,0,W,Hh),C.Void(alpha));
            if(titleAlpha>0)Title(new Rect(0,0,W,Hh),W,Hh,0,eyebrow,title,titleAlpha*alpha,-1,true);
        }

        /// <summary>
        /// One shutter band in <paramref name="r"/> (it may run past the canvas on its trailing side): the void body, scanlines, a
        /// top seam, a circuit trace that rides with it (<paramref name="shift"/>), and the chevron lead (left or right) with its
        /// scan edge. <paramref name="alpha"/> fades it all; <paramref name="detail"/> dims the seam and the trace (the loading
        /// screen keeps them quiet under its content).
        /// </summary>
        public static void Shutter(Rect r,int index,bool leadLeft,float cut,float alpha,float shift,float W,float detail=1,float motion=0)
        {
            if(alpha<=0||r.width<=0)return;var set=leadLeft?H.Corners.TL|H.Corners.BL:H.Corners.TR|H.Corners.BR;float px=H.Px;
            H.Cut(r,C.Void(alpha),cut,set);
            // depth: a navy wash toward the lead, scanlines, the seam
            float wash=Mathf.Min(r.width,W*.42f);var wr=leadLeft?new Rect(r.x,r.y,wash,r.height):new Rect(r.xMax-wash,r.y,wash,r.height);
            U.BeginBand(new Rect(r.x+(leadLeft?cut:0),r.y,r.width-cut,r.height));
            U.HGradient(wr,leadLeft?C.Navy(.9f*alpha):Color.clear,leadLeft?Color.clear:C.Navy(.9f*alpha));
            U.EndBand();
            // the chevron's own wash, so the tip reads as the band's lit edge and not a darker wedge
            U.BeginBand(new Rect(leadLeft?r.x:r.xMax-cut,r.y,cut,r.height));H.Cut(r,C.Navy(.9f*alpha),cut,set);U.EndBand();
            H.Scan(r,C.Cyan(.035f*alpha),cut,set);
            U.Fill(new Rect(r.x+(leadLeft?cut:0),r.y,r.width-cut,px),C.Edge(.85f*alpha*detail));
            Trace(r,index,cut,alpha*detail,shift,W,leadLeft);
            // the scan edge's trail while the band moves: a cyan wash fading back from the lead
            if(motion>0){U.BeginBand(new Rect(leadLeft?r.x:r.xMax-cut,r.y,cut,r.height));H.Cut(r,C.Cyan(.16f*motion*alpha),cut,set);U.EndBand();
                float tl=Mathf.Min(r.width-cut,180*motion+40);if(tl>0)U.HGradient(leadLeft?new Rect(r.x+cut,r.y,tl,r.height):new Rect(r.xMax-cut-tl,r.y,tl,r.height),leadLeft?C.Cyan(.16f*motion*alpha):Color.clear,leadLeft?Color.clear:C.Cyan(.16f*motion*alpha));}
            // The lead: the chevron's edge in cyan (clipped to the lead), its glow and a white-hot core along the flat.
            float lx=leadLeft?r.x:r.xMax;var lead=new Rect(leadLeft?r.x-6:r.xMax-cut-2,r.y-2,cut+8,r.height+4);
            U.Glow(new Rect(lx-1,r.y+cut,2,Mathf.Max(0,r.height-2*cut)),C.Cyan(.55f*alpha),12);
            U.BeginBand(lead);H.Outline(r,C.Cyan(alpha),cut,set,2);U.EndBand();
            U.Fill(new Rect(leadLeft?lx+px:lx-2*px,r.y+cut,px,Mathf.Max(0,r.height-2*cut)),new Color(1,1,1,.85f*alpha));
            // a magenta tick on the band's tip
            float tip=Mathf.Min(10,r.height*.08f);U.Fill(new Rect(leadLeft?lx+3:lx-3-H.Snap(2),r.y+r.height/2-tip,H.Snap(2),2*tip),C.Magenta(.9f*alpha));
        }
        // A deterministic circuit trace per band: a hairline at a band-specific height with two nodes and a short riser, placed
        // in the band's own frame so it slides with it.
        private static void Trace(Rect r,int index,float cut,float alpha,float shift,float W,bool leadLeft)
        {
            float h1=Hash(index*12.9898f),h2=Hash(index*78.233f+3.1f),h3=Hash(index*37.719f+7.7f);
            float y=r.y+r.height*(.28f+.44f*h1),x0=W*(.08f+.5f*h2)+shift,len=W*(.18f+.22f*h3);
            float clipL=r.x+(leadLeft?cut+4:0),clipR=r.xMax-(leadLeft?0:cut+4);if(clipR<=clipL)return;
            U.BeginBand(new Rect(clipL,r.y,clipR-clipL,r.height));
            U.Fill(new Rect(x0,y,len,H.Px),C.CyanSoft(.16f*alpha));
            float n=H.Snap(4);U.Fill(new Rect(x0-n/2,y-n/2,n,n),C.Cyan(.5f*alpha));U.Fill(new Rect(x0+len-n/2,y-n/2,n,n),C.CyanSoft(.35f*alpha));
            float rise=r.height*.2f*(h1>.5f?1:-1);U.Fill(new Rect(x0+len*.62f,Mathf.Min(y,y+rise),H.Px,Mathf.Abs(rise)),C.CyanSoft(.12f*alpha));
            for(int k=0;k<4;k++)U.Fill(new Rect(x0+len*(.1f+.08f*k),y+3,H.Snap(2),H.Snap(2)),C.Cyan(.22f*alpha));
            U.EndBand();
        }
        private static float Hash(float v){float s=Mathf.Sin(v)*43758.5453f;return s-Mathf.Floor(s);}

        /// <summary>
        /// The chip behind the title: a chamfered package with its pins, die and a crosshair, faint, drawn into every band with
        /// that band's shift, so it assembles from the sliding shards and leaves with them.
        /// </summary>
        public static void Motif(Rect band,float W,float Hh,float shift,float alpha)
        {
            float side=Hh*.46f,cx=W/2+shift,cy=Hh/2;var pkg=new Rect(cx-side/2,cy-side/2,side,side);
            if(pkg.yMax<band.y||pkg.y>band.yMax)return;U.BeginBand(band);
            H.Outline(pkg,C.Cyan(.10f*alpha),side*.07f,H.Corners.All,1);
            var die=new Rect(cx-side*.2f,cy-side*.2f,side*.4f,side*.4f);H.Outline(die,C.CyanSoft(.07f*alpha),side*.03f,H.Corners.All,1);
            float pin=side*.07f,gap=side*.8f/8;
            for(int k=0;k<=8;k++)
            {
                float o=-side*.4f+k*gap;var c=C.Cyan(.09f*alpha);
                U.Fill(new Rect(cx+o-1,pkg.y-pin,H.Snap(2),pin),c);U.Fill(new Rect(cx+o-1,pkg.yMax,H.Snap(2),pin),c);
                U.Fill(new Rect(pkg.x-pin,cy+o-1,pin,H.Snap(2)),c);U.Fill(new Rect(pkg.xMax,cy+o-1,pin,H.Snap(2)),c);
            }
            U.Fill(new Rect(cx-side*.7f,cy,side*.42f,H.Px),C.Edge(.6f*alpha));U.Fill(new Rect(cx+side*.28f,cy,side*.42f,H.Px),C.Edge(.6f*alpha));
            U.EndBand();
        }
        /// <summary>
        /// The destination's eyebrow and title centred on the canvas, offset by <paramref name="shift"/> and clipped to
        /// <paramref name="band"/> (the middle band), so it rides the band in and out. <paramref name="glitchAge"/> &gt;= 0 runs
        /// the chromatic split on the title (none with <paramref name="reduced"/>).
        /// </summary>
        public static void Title(Rect band,float W,float Hh,float shift,string eyebrow,string title,float alpha,float glitchAge,bool reduced)
        {
            if(alpha<=0||string.IsNullOrEmpty(title))return;float size=Mathf.Clamp(Hh*.056f,26,54),cy=Hh/2;
            U.BeginBand(band);
            var line=new Rect(shift,cy-size*.62f+8,W,size*1.24f);
            if(!string.IsNullOrEmpty(eyebrow))
            {
                var eb=new Rect(shift,line.y-24,W,16);float ew=U.Measure(eyebrow,11,true,true,.32f).x;
                U.Fill(new Rect(shift+W/2-ew/2-16,eb.y+5,H.Snap(6),H.Snap(6)),C.Cyan(alpha));
                U.Text(eb,eyebrow,11,C.CyanSoft(.95f*alpha),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.32f);
            }
            if(!reduced&&glitchAge>=0)H.Glitch(line,title,size,glitchAge,alpha,TextAnchor.MiddleCenter,false,true,false,.08f);
            U.Text(line,title,size,C.Ink(alpha),TextAnchor.MiddleCenter,bold:true,tracking:.08f,glow:C.Cyan(.55f*alpha),shadow:C.Void(.8f*alpha));
            // the chip-pin rule under it: a cyan run, pins, a magenta end
            float rw=Mathf.Min(W*.22f,260),rx=shift+W/2-rw/2,ry=line.yMax+6;
            U.Fill(new Rect(rx,ry,rw,H.Px),C.CyanSoft(.35f*alpha));U.Fill(new Rect(rx,ry-H.Px,Mathf.Min(rw,36),H.Snap(2)),C.Cyan(alpha));U.Fill(new Rect(rx+rw-14,ry-H.Px,14,H.Snap(2)),C.Magenta(.9f*alpha));
            for(float p=rx+48;p<rx+rw-20;p+=9)U.Fill(new Rect(p,ry+H.Px,H.Px,3),C.CyanSoft(.3f*alpha));
            U.EndBand();
        }
        /// <summary>The swap's glitch: two displaced slices across the shutters and cyan/magenta seam hairlines, for a few frames.</summary>
        public static void Glitch(float W,float Hh,float g,float now)
        {
            int step=(int)(now*60)%3;float y=Hh*(step==0?.31f:step==1?.58f:.44f),h=Hh*(step==1?.035f:.02f),dx=(step==0?-14:step==1?18:-9)*g;
            U.Fill(new Rect(dx,y,W,h),C.Cyan(.10f*g));U.Fill(new Rect(-dx*.6f,y+h,W,H.Snap(1)),C.CyanSoft(.6f*g));
            U.Fill(new Rect(dx*.5f,Hh*.71f,W*.7f,H.Snap(1)),C.Magenta(.55f*g));U.Fill(new Rect(W*.3f-dx,Hh*.22f,W*.7f,H.Snap(1)),C.Cyan(.45f*g));
        }
        /// <summary>A bright scan line across the closed screen at <paramref name="at"/> (0 top .. 1 bottom).</summary>
        public static void ScanLine(float W,float Hh,float at,float alpha)
        {
            float y=Hh*at;U.Glow(new Rect(0,y,W,H.Snap(1)),C.Cyan(.18f*alpha),10);U.Fill(new Rect(0,y,W,H.Snap(1)),C.CyanSoft(.55f*alpha));
            U.Fade(new Rect(0,y-40,W,40),C.Cyan(.045f*alpha),false);
        }
    }
}
