using System;
using System.Collections.Generic;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using R=ChipArena.UnityConversion.NativeHudCyber;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The cyber HUD's drawing kit (owner 2026-10-12: "Also make the Hud match the cyber theme~"): chamfered holographic
    /// panels on navy glass with thin edge lines, scanlines and cyan corner brackets; segmented bars with a damage trail;
    /// keycaps, sweeps and the event glitch. It draws through NativeUi (virtual canvas, crisp at any resolution, inside any
    /// pushed transform) in the NativeCyberTheme palette, with the layout rules of Core/NativeHudCyber. Corner pieces are
    /// baked at their physical pixel size (cached), so a 1 px diagonal stays one sharp pixel at 1280x720 and at 3440x1440.
    /// </summary>
    public static class NativeHudChrome
    {
        [Flags] public enum Corners{None=0,TL=1,TR=2,BR=4,BL=8,Diagonal=TL|BR,Top=TL|TR,All=15}
        private static readonly Dictionary<long,Texture2D> corners=new Dictionary<long,Texture2D>();private static Texture2D scan;
        // One physical pixel in canvas units, and a canvas length snapped to whole physical pixels (at least one).
        public static float Px=>1/Mathf.Max(.01f,U.S);
        public static float Snap(float v)=>Mathf.Max(1,Mathf.Round(v*U.S))/Mathf.Max(.01f,U.S);

        // A corner piece `px` physical pixels square: the inside of the cut (band 0) or a band that thick along the cut, with
        // 4x4 supersampled coverage. corner: 0 TL, 1 TR, 2 BR, 3 BL (the corner point the cut removes).
        private static Texture2D Corner(int corner,int px,float band)
        {
            px=Mathf.Clamp(px,1,96);int b=Mathf.RoundToInt(band*8);long key=((long)corner<<40)|((long)px<<20)|(long)b;if(corners.TryGetValue(key,out var t)&&t!=null)return t;
            var pixels=new Color[px*px];
            for(int y=0;y<px;y++)for(int x=0;x<px;x++)
            {
                int hit=0;
                for(int sy=0;sy<4;sy++)for(int sx=0;sx<4;sx++)
                {
                    // Distance (pixels) from the sample to the corner point the cut removes, along the corner's diagonal.
                    float fx=x+(sx+.5f)/4,fy=y+(sy+.5f)/4,ux=corner==0||corner==3?fx:px-fx,uy=corner==0||corner==1?fy:px-fy,d=(ux+uy-px)/1.41421356f;
                    if(band<=0?d>=0:d>=0&&d<band)hit++;
                }
                // Row 0 is the bottom of a Unity texture.
                pixels[(px-1-y)*px+x]=new Color(1,1,1,hit/16f);
            }
            t=new Texture2D(px,px,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};t.SetPixels(pixels);t.Apply(false,true);corners[key]=t;return t;
        }
        private static float CutOf(Corners set,Corners one,float cut)=>(set&one)!=0?cut:0;
        // The chamfer at this resolution: whole physical pixels, never more than half the box.
        private static float Chamfer(Rect r,float cut)=>Mathf.Min(Mathf.Round(cut*U.S)/Mathf.Max(.01f,U.S),Mathf.Min(r.width,r.height)/2);

        /// <summary>A chamfered fill: the bands between the cut corners and each cut's inner triangle.</summary>
        public static void Cut(Rect r,Color fill,float cut,Corners set=Corners.Diagonal)
        {
            if(fill.a<=0||r.width<=0||r.height<=0||Event.current.type!=EventType.Repaint)return;float c=Chamfer(r,cut);if(c<=0||set==Corners.None){U.Fill(r,fill);return;}
            float tl=CutOf(set,Corners.TL,c),tr=CutOf(set,Corners.TR,c),br=CutOf(set,Corners.BR,c),bl=CutOf(set,Corners.BL,c);
            U.Fill(new Rect(r.x,r.y+c,r.width,r.height-2*c),fill);
            U.Fill(new Rect(r.x+tl,r.y,r.width-tl-tr,c),fill);U.Fill(new Rect(r.x+bl,r.yMax-c,r.width-bl-br,c),fill);
            int px=Mathf.RoundToInt(c*U.S);
            if(tl>0)U.Image(new Rect(r.x,r.y,c,c),Corner(0,px,0),fill);if(tr>0)U.Image(new Rect(r.xMax-c,r.y,c,c),Corner(1,px,0),fill);
            if(br>0)U.Image(new Rect(r.xMax-c,r.yMax-c,c,c),Corner(2,px,0),fill);if(bl>0)U.Image(new Rect(r.x,r.yMax-c,c,c),Corner(3,px,0),fill);
        }
        /// <summary>The chamfered shape's edge line, `w` canvas units (snapped to whole physical pixels) inside its bounds.</summary>
        public static void Outline(Rect r,Color edge,float cut,Corners set=Corners.Diagonal,float w=1)
        {
            if(edge.a<=0||r.width<=0||r.height<=0||Event.current.type!=EventType.Repaint)return;float c=Chamfer(r,cut),t=Snap(w);
            float tl=CutOf(set,Corners.TL,c),tr=CutOf(set,Corners.TR,c),br=CutOf(set,Corners.BR,c),bl=CutOf(set,Corners.BL,c);
            U.Fill(new Rect(r.x+tl,r.y,r.width-tl-tr,t),edge);U.Fill(new Rect(r.x+bl,r.yMax-t,r.width-bl-br,t),edge);
            U.Fill(new Rect(r.x,r.y+tl,t,r.height-tl-bl),edge);U.Fill(new Rect(r.xMax-t,r.y+tr,t,r.height-tr-br),edge);
            int px=Mathf.RoundToInt(c*U.S);float band=t*U.S*1.15f;
            if(tl>0)U.Image(new Rect(r.x,r.y,c,c),Corner(0,px,band),edge);if(tr>0)U.Image(new Rect(r.xMax-c,r.y,c,c),Corner(1,px,band),edge);
            if(br>0)U.Image(new Rect(r.xMax-c,r.yMax-c,c,c),Corner(2,px,band),edge);if(bl>0)U.Image(new Rect(r.x,r.yMax-c,c,c),Corner(3,px,band),edge);
        }
        /// <summary>Static scanlines (one line per ScanPeriod canvas units) inside the chamfered shape; no motion.</summary>
        public static void Scan(Rect r,Color c,float cut,Corners set=Corners.Diagonal)
        {
            if(c.a<=0||Event.current.type!=EventType.Repaint)return;
            if(scan==null){scan=new Texture2D(1,3,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Point,hideFlags=HideFlags.HideAndDontSave};scan.SetPixels(new[]{new Color(1,1,1,0),new Color(1,1,1,0),Color.white});scan.Apply(false,true);}
            float k=Chamfer(r,cut),tl=CutOf(set,Corners.TL,k),tr=CutOf(set,Corners.TR,k),br=CutOf(set,Corners.BR,k),bl=CutOf(set,Corners.BL,k);var period=new Vector2(1,R.ScanPeriod);
            U.Tile(new Rect(r.x+tl,r.y+1,r.width-tl-tr,k-1),scan,c,period,new Vector2(0,1));
            U.Tile(new Rect(r.x,r.y+k,r.width,r.height-2*k),scan,c,period,new Vector2(0,k));
            U.Tile(new Rect(r.x+bl,r.yMax-k,r.width-bl-br,k-1),scan,c,period,new Vector2(0,r.height-k));
        }
        /// <summary>L-shaped brackets on the corners that are not cut: `len` arms, `w` thick.</summary>
        public static void Brackets(Rect r,Color c,float len,Corners cut=Corners.Diagonal,float w=1)
        {
            if(c.a<=0)return;float t=Snap(w);
            if((cut&Corners.TL)==0){U.Fill(new Rect(r.x,r.y,len,t),c);U.Fill(new Rect(r.x,r.y,t,len),c);}
            if((cut&Corners.TR)==0){U.Fill(new Rect(r.xMax-len,r.y,len,t),c);U.Fill(new Rect(r.xMax-t,r.y,t,len),c);}
            if((cut&Corners.BR)==0){U.Fill(new Rect(r.xMax-len,r.yMax-t,len,t),c);U.Fill(new Rect(r.xMax-t,r.yMax-len,t,len),c);}
            if((cut&Corners.BL)==0){U.Fill(new Rect(r.x,r.yMax-t,len,t),c);U.Fill(new Rect(r.x,r.yMax-len,t,len),c);}
        }
        /// <summary>
        /// The holographic panel: navy glass at NativeHudCyber.PanelAlpha (the contrast floor every HUD text tone is checked
        /// against), static scanlines, a 1 px edge, a cyan accent run along the top from the cut, a faint inner top highlight
        /// and cyan brackets on the square corners. `accent` replaces the cyan (a state's tone); `alpha` fades it all.
        /// </summary>
        public static void Panel(Rect r,float alpha=1,Color accent=default,float cut=R.Cut,Corners set=Corners.Diagonal,bool scanlines=true,float fill=R.PanelAlpha)
        {
            if(alpha<=0)return;if(accent.a<=0)accent=C.Cyan();
            Cut(r,C.Navy(fill*alpha),cut,set);if(scanlines)Scan(r,C.Cyan(R.ScanAlpha*alpha),cut,set);
            Outline(r,C.Edge(.95f*alpha),cut,set);
            float c=Chamfer(r,cut),t=Snap(1),tl=CutOf(set,Corners.TL,c),run=Mathf.Min(r.width-tl-CutOf(set,Corners.TR,c),Mathf.Max(28,r.width*.34f));
            U.Fill(new Rect(r.x+tl,r.y,run,t),U.A(accent,.9f*alpha));U.Fill(new Rect(r.x+tl,r.y+t,r.width-tl-CutOf(set,Corners.TR,c),t),C.CyanSoft(.06f*alpha));
            Brackets(r,U.A(accent,.75f*alpha),Mathf.Min(10,Mathf.Min(r.width,r.height)*.3f),set);
        }
        /// <summary>A small chamfered tag (pills, state chips): fill, edge and optional left tick in `tone`.</summary>
        public static void Tag(Rect r,Color tone,float alpha=1,bool tick=true,float fill=R.PanelAlpha)
        {
            Cut(r,C.Navy(fill*alpha),R.TagCut);Outline(r,U.A(tone,.55f*alpha),R.TagCut);if(tick)U.Fill(new Rect(r.x+Snap(1)+1,r.y+r.height*.28f,Snap(2),r.height*.44f),U.A(tone,alpha));
        }
        /// <summary>A keycap: a small square-ish key with its legend, for SHIFT / RMB / Q / E / C / Space.</summary>
        public static float Key(Rect r,string key,float alpha=1,Color tone=default)
        {
            if(tone.a<=0)tone=C.CyanSoft();Cut(r,C.Indigo(.95f*alpha),2,Corners.BR);Outline(r,U.A(tone,.45f*alpha),2,Corners.BR);
            U.Text(r,key,Mathf.Min(10,r.height*.62f),U.A(tone,alpha),TextAnchor.MiddleCenter,bold:true,mono:true);return r.width;
        }
        public static float KeyWidth(string key,float h)=>Mathf.Max(h,U.Measure(key,Mathf.Min(10,h*.62f),true,true).x+8);

        /// <summary>
        /// A segmented bar: `max` split by NativeHudCyber.SegmentStep into at most `limit` cells with a `gap`, the track in
        /// `track`, the damage trail (`trail` >= value) in `ghost`, the value in `fill`. Values in bar units (HP, rounds).
        /// </summary>
        public static void Bar(Rect r,float value,float max,int limit,Color fill,Color track,float trail=-1,Color ghost=default,float gap=2)
        {
            if(r.width<=0||r.height<=0)return;float step=R.SegmentStep(max,limit);int n=R.SegmentCount(max,step);if(n*gap>r.width*.5f)gap=0;
            for(int i=0;i<n;i++)
            {
                R.SegmentSpan(i,step,max,out float a,out float b);float x0=r.x+r.width*a+(i>0?gap*.5f:0),x1=r.x+r.width*b-(i<n-1?gap*.5f:0);if(x1<=x0)continue;var cell=new Rect(x0,r.y,x1-x0,r.height);
                U.Fill(cell,track);
                if(trail>value&&ghost.a>0){float g=R.SegmentFill(i,step,max,trail);if(g>0)U.Fill(new Rect(cell.x,cell.y,cell.width*g,cell.height),ghost);}
                float f=R.SegmentFill(i,step,max,value);if(f>0)U.Fill(new Rect(cell.x,cell.y,cell.width*f,cell.height),fill);
            }
        }
        /// <summary>A thin continuous bar (resources): track, fill to `fraction`, and a bright leading pixel.</summary>
        public static void Line(Rect r,float fraction,Color fill,Color track)
        {
            U.Fill(r,track);fraction=Mathf.Clamp01(fraction);if(fraction<=0)return;var f=new Rect(r.x,r.y,r.width*fraction,r.height);U.Fill(f,fill);
            if(fraction<1)U.Fill(new Rect(f.xMax-Snap(1),r.y-Snap(1),Snap(1),r.height+2*Snap(1)),U.A(C.Ink(),fill.a));
        }

        /// <summary>
        /// The event glitch on a line of text: for NativeHudCyber.GlitchLife after the event, a cyan and a magenta copy, each
        /// a horizontal band of the text shifted a few pixels (NativeUi.SirenSplit's steps, the boss title's glitch). Nothing
        /// with Reduced effects or outside the burst. Draw it before the text itself.
        /// </summary>
        public static void Glitch(Rect line,string text,float size,float age,float alpha,TextAnchor align,bool reduced,bool bold=true,bool mono=true,float tracking=0)
        {
            if(reduced||!R.Glitching(age)||Event.current.type!=EventType.Repaint||string.IsNullOrEmpty(text))return;float k=R.GlitchCycle(age);
            for(int copy=0;copy<2;copy++)
            {
                bool cyan=copy==1;if(!U.SirenSplit(k,cyan,out float dx,out float top,out float bottom,out float op))continue;
                U.BeginBand(new Rect(line.x-line.width,line.y+line.height*top,line.width*3,line.height*(bottom-top)));
                U.Text(new Rect(line.x+dx,line.y,line.width,line.height),text,size,cyan?C.Cyan(op*alpha):C.Magenta(op*alpha),align,bold:bold,mono:mono,tracking:tracking);U.EndBand();
            }
        }
        /// <summary>A glitch slice across a panel: a band of the panel's width offset sideways for the burst (a hit taken).</summary>
        public static void Slice(Rect r,float age,float alpha,bool reduced,Color tone)
        {
            if(reduced||!R.Glitching(age))return;float k=Mathf.Clamp01(age/R.GlitchLife);int step=k<.34f?0:k<.67f?1:2;
            float y=r.y+r.height*(step==0?.18f:step==1?.56f:.36f),h=Mathf.Max(2,r.height*(step==1?.12f:.07f)),dx=step==0?-5:step==1?6:-3;
            U.Fill(new Rect(r.x+dx,y,r.width,h),U.A(tone,.28f*alpha));U.Fill(new Rect(r.x-dx*.5f,y+h,r.width*.6f,Snap(1)),U.A(C.CyanSoft(),.5f*alpha));
        }
        /// <summary>The magenta "ready" flare on a card: a glow under the card (`under`) and a bright edge over it, fading over
        /// NativeHudCyber.FlareLife (nothing with Reduced effects: the steady ready tick, dial and edge carry the state).</summary>
        public static void Flare(Rect r,float age,float alpha,bool reduced,bool under)
        {
            float f=reduced?0:R.Flare(age);if(f<=0)return;
            if(under)U.Glow(r,C.Magenta(.6f*f*alpha),10+10*(1-f));else Outline(r,C.Magenta(f*alpha),R.CardCut,Corners.Diagonal,2);
        }
    }
}
