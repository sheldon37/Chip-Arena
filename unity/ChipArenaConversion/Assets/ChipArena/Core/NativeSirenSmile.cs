using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // The Siren's smile (pve/siren-reveal.ts 'smile' window, owner playtest 2026-10-10: "looks like just a line"): the
    // client/siren-card.ts sirenCinematic() stage under chip-cards.css .siren-selection / .siren-cinema / .siren-stare and
    // pve/pve.css .pve-smile / .pve-reveal-backdrop at 05950ea. The room drops into the void, the stage's black flame row
    // climbs (siren-rise) with every tongue licking on its own beat (siren-lick), the face flickers in (siren-stare) and the
    // toothed grin opens from a slit (siren-grin: scaleX .04 -> 1 about 50% 40% of its fill-box) under the face's
    // drop-shadow(0 0 28px #b7a8ffaa) glow (a Gaussian of standard deviation 28 px, as Chromium draws it); scanlines and
    // two glitch tears cross it. Engine-free so Tests/SirenSmileChecks.cs pins the curves and the geometry to values the
    // source produces (Tests/siren-smile-parity.json). The shapes rasterise here into RGBA bytes (exact horizontal span
    // coverage on sub-sampled rows, nonzero fill, miter-joined teeth) for the Runtime textures; the glow is re-blurred from
    // the grin's current width while it opens, without allocating.
    public static class NativeSirenSmile
    {
        // ---- the face: siren-card.ts EYES and GRIN on its 200 x 120 view box ----
        public const string EyeLeftPath="M32 36 L86 48 C74 58 46 58 32 36 Z",EyeRightPath="M168 36 L114 48 C126 58 154 58 168 36 Z";
        public const string GrinPath="M22 70 C70 86 130 86 178 70 C140 116 60 116 22 70 Z";
        public const string TeethPath="M28 70.9 L37 82.7 L46 76.1 L55 93.2 L64 79.8 L73 100.2 L82 82.1 L91 103.8 L100 82.8 L109 103.8 L118 82.1 L127 100.2 L136 79.8 L145 93.2 L154 76.1 L163 82.7 L172 70.9";
        public const float ViewWidth=200,ViewHeight=120;
        // .siren-stare .siren-teeth: fill none, stroke #050409, width 3.4, miter joins (SVG miter limit 4), butt caps.
        public const float TeethWidth=3.4f,MiterLimit=4;
        public const int FaceRgb=0xf6f3ff,TeethRgb=0x050409,GlowRgb=0xb7a8ff;
        // drop-shadow(0 0 28px #b7a8ffaa): Chromium blurs with standard deviation 28 (measured against its renderer).
        public const float GlowAlpha=0xaa/255f,GlowSigma=28;
        // .siren-stare: left 50%, top 35% (the reveal backdrop's 30%), width min(96vw, 1200px), centred on that point.
        public const float StareTop=.35f,BackdropStareTop=.30f,BackdropStareOpacity=.35f,BackdropOpacity=.55f;
        public static float StareWidth(float viewportWidth)=>Math.Min(.96f*viewportWidth,1200);

        public static readonly List<float[]> Eyes=new List<float[]>(),Grin=new List<float[]>(),TeethLine=new List<float[]>(),TeethStroke=new List<float[]>();
        // The mouth group's fill-box (grin and teeth geometry) and its transform origin (50% 40%); the eyes' and the
        // stroked mouth's bounds for the textures.
        public static readonly float MouthBoxX0,MouthBoxY0,MouthBoxX1,MouthBoxY1,MouthOriginX,MouthOriginY;
        public static readonly float EyesX0,EyesY0,EyesX1,EyesY1,MouthX0,MouthY0,MouthX1,MouthY1;

        // ---- the stage's flame row: flames(400, 160, STAGE_FRONT, STAGE_BACK, '.9 .92') ----
        public const string TongueRight="M-10 0 C-14 -12 -9 -22 -5 -32 C-2 -40 -3 -50 0 -60 C7 -50 10 -42 8 -32 C12 -36 13 -42 12 -47 C19 -36 19 -16 10 0 Z";
        public const string TongueLeft="M10 0 C14 -12 9 -22 5 -32 C2 -40 3 -50 0 -60 C-7 -50 -10 -42 -8 -32 C-12 -36 -13 -42 -12 -47 C-19 -36 -19 -16 -10 0 Z";
        // [x, scale] pairs in document order; back tongues are written first (under the front row).
        public static readonly float[] StageFront={8,1.1f,40,1.6f,72,1.25f,104,1.85f,136,1.35f,168,2,200,1.5f,232,1.9f,264,1.3f,296,1.75f,328,1.2f,360,1.6f,392,1.15f};
        public static readonly float[] StageBack={0,.9f,24,1.1f,56,.85f,88,1.2f,120,.95f,152,1.25f,184,1,216,1.3f,248,.95f,280,1.2f,312,.9f,344,1.15f,376,.95f,400,1.05f};
        public const float FlameViewWidth=400,FlameViewHeight=160,CoreScaleX=.9f,CoreScaleY=.92f;
        // .siren-tongue .rim: fill #3b2786 (back #150d2c), stroke #ddd5ff (back #b7a8ff) at .75 (.siren-cinema overrides the
        // back row's .16: same specificity, later rule), .9 px non-scaling; .core fill #030205.
        public const int RimRgb=0x3b2786,BackRimRgb=0x150d2c,RimStrokeRgb=0xddd5ff,BackRimStrokeRgb=0xb7a8ff,CoreRgb=0x030205;
        public const float RimStrokeOpacity=.75f,RimStrokeWidth=.9f;
        public static int TongueCount=>(StageFront.Length+StageBack.Length)/2;
        public static readonly List<float[]> RightTongue=new List<float[]>(),LeftTongue=new List<float[]>(),RightCore=new List<float[]>(),LeftCore=new List<float[]>();
        // A tongue path's tight bounds (y runs -60 .. 0) and its fill-box bottom centre, the siren-lick transform origin.
        public static readonly float TongueX0,TongueX1,TongueY0,TongueY1,RightOriginX,LeftOriginX;

        static NativeSirenSmile()
        {
            Eyes.AddRange(Parse(EyeLeftPath,48));Eyes.AddRange(Parse(EyeRightPath,48));Grin.AddRange(Parse(GrinPath,64));TeethLine.AddRange(Parse(TeethPath,1));
            TeethStroke.AddRange(MiterStroke(TeethLine[0],TeethWidth,MiterLimit));
            Bounds(Grin,out float gx0,out float gy0,out float gx1,out float gy1);Bounds(TeethLine,out float tx0,out float ty0,out float tx1,out float ty1);
            MouthBoxX0=Math.Min(gx0,tx0);MouthBoxY0=Math.Min(gy0,ty0);MouthBoxX1=Math.Max(gx1,tx1);MouthBoxY1=Math.Max(gy1,ty1);
            MouthOriginX=MouthBoxX0+.5f*(MouthBoxX1-MouthBoxX0);MouthOriginY=MouthBoxY0+.4f*(MouthBoxY1-MouthBoxY0);
            Bounds(Eyes,out EyesX0,out EyesY0,out EyesX1,out EyesY1);Bounds(TeethStroke,out float sx0,out float sy0,out float sx1,out float sy1);
            MouthX0=Math.Min(gx0,sx0);MouthY0=Math.Min(gy0,sy0);MouthX1=Math.Max(gx1,sx1);MouthY1=Math.Max(gy1,sy1);
            RightTongue.AddRange(Parse(TongueRight,24));LeftTongue.AddRange(Parse(TongueLeft,24));
            foreach(var p in RightTongue)RightCore.Add(Scaled(p,CoreScaleX,CoreScaleY));foreach(var p in LeftTongue)LeftCore.Add(Scaled(p,CoreScaleX,CoreScaleY));
            Bounds(RightTongue,out float rx0,out TongueY0,out float rx1,out TongueY1);Bounds(LeftTongue,out float lx0,out _,out float lx1,out _);
            TongueX0=Math.Min(rx0,lx0);TongueX1=Math.Max(rx1,lx1);RightOriginX=(rx0+rx1)/2;LeftOriginX=(lx0+lx1)/2;
        }

        // ---- timelines (k: progress through the 1.4 s smile, or the reveal backdrop's 6 s clock) ----
        private static readonly float[] StareAt={0,.08f,.12f,.14f,.17f,.80f,1},StareV={0,0,.95f,.2f,.85f,.85f,0};
        // siren-stare (ease-out per segment): dark until 8%, a .95 flash at 12%, a dip to .2 at 14%, .85 from 17% to 80%, out.
        public static float StareOpacity(float k)=>Track(k,StareAt,StareV,0,0,.58f,1);
        private static readonly float[] GrinAt={0,.15f,.16f,.38f,1},GrinOpacityV={0,0,1,1,1},GrinScaleV={.04f,.04f,.04f,1,1};
        // siren-grin (cubic-bezier(.2,.8,.2,1) per segment): hidden until 15%, a .04-wide slit by 16%, wide open at 38%.
        public static void GrinOpen(float k,out float opacity,out float scaleX){opacity=Track(k,GrinAt,GrinOpacityV,.2f,.8f,.2f,1);scaleX=Track(k,GrinAt,GrinScaleV,.2f,.8f,.2f,1);}
        private static readonly float[] RiseAt={0,.30f,.85f,1},RiseV={.6f,0,0,.12f};
        // siren-rise: translateY(60%) -> none by 30%, held to 85%, sinking 12% by the end; a fraction of the flame box height.
        public static float Rise(float k)=>Track(k,RiseAt,RiseV,.2f,.8f,.2f,1);
        // siren-cine-tear, steps(1,end): opacity 1 at -30 px from 5%, .6 at +40 px from 7%, gone 9%..63%, .9 at -20 px to 65%.
        // The transform keyframes (5%, 7%, 63%) hold their value to the next transform keyframe (the implicit none at 100%).
        public static float Tear(float k,out float dx)
        {
            dx=k<.05f||k>=1?0:k<.07f?-30:k<.63f?40:-20;
            return k<.05f?0:k<.07f?1:k<.09f?.6f:k<.63f?0:k<.65f?.9f:0;
        }
        public const float TearA=.30f,TearB=.63f,TearHeight=.09f,TearBLead=.12f;
        // siren-lick (1.7 s ease-in-out infinite alternate, transform-origin 50% 100% of the fill-box): scaleY .76 / 1.08 / .9
        // with skewX -6 / 3 / 8 deg; .t1-.t6 change duration and (negative) delay. Clock t from when the markup was written.
        private static readonly float[] LickDuration={1.7f,1.3f,2.1f,1.5f,1.9f,1.4f,2.3f},LickDelay={0,-.4f,-1.2f,-.7f,-1.6f,-.2f,-.9f};
        private static readonly float[] LickAt={0,.5f,1},LickScaleV={.76f,1.08f,.9f},LickSkewV={-6,3,8};
        public static void Lick(float t,int n,out float scaleY,out float skewDegrees)
        {
            int c=n%7;double local=(t-LickDelay[c])/LickDuration[c];double iteration=Math.Floor(local);float p=(float)(local-iteration);if(((long)iteration&1)!=0)p=1-p;
            scaleY=Track(p,LickAt,LickScaleV,.42f,0,.58f,1);skewDegrees=Track(p,LickAt,LickSkewV,.42f,0,.58f,1);
        }
        // pve-spot-in (.7 s, cubic-bezier(.16,.8,.22,1) per segment) on the spotlight block: opacity in by 12%; scaleY .04
        // (scaleX 1.2 -> 1) until 12%, 1.05 at 30%, then translateX -8 px at 40%, +6 px at 48%, settled at 56%.
        private static readonly float[] SpotAt={0,.12f,.30f,.40f,.48f,.56f,1},SpotOpacityV={0,1,1,1,1,1,1},SpotSxV={1.2f,1,1,1,1,1,1},SpotSyV={.04f,.04f,1.05f,1,1,1,1},SpotTxV={0,0,0,-8,6,0,0};
        public static void SpotIn(float k,out float opacity,out float scaleX,out float scaleY,out float translateX)
        {opacity=Track(k,SpotAt,SpotOpacityV,.16f,.8f,.22f,1);scaleX=Track(k,SpotAt,SpotSxV,.16f,.8f,.22f,1);scaleY=Track(k,SpotAt,SpotSyV,.16f,.8f,.22f,1);translateX=Track(k,SpotAt,SpotTxV,.16f,.8f,.22f,1);}
        // .draft-reveal.rarity-siren siren-boot (1.25 s, cubic-bezier(.2,.8,.2,1) per segment) on the booting chip: a flattened
        // bar (scaleY .02, scaleX 1.12 -> 1) lit by 10%, unfolding to 1.06 at 22%, jolting -7 / +6 px at 27% / 31% (dimming to
        // .35 there), settled at 35%. `t` is seconds since the chip's reveal began.
        private static readonly float[] BootAt={0,.10f,.22f,.27f,.31f,.35f,1},BootSxV={1.12f,1,1,1,1,1,1},BootSyV={.02f,.02f,1.06f,1,1,1,1},BootTxV={0,0,0,-7,6,0,0};
        private static readonly float[] BootOpacityAt={0,.10f,.31f,.35f,1},BootOpacityV={0,1,.35f,1,1};
        public const float BootSeconds=1.25f;
        public static void SirenBoot(float t,out float opacity,out float scaleX,out float scaleY,out float translateX)
        {
            float k=t/BootSeconds;opacity=Track(k,BootOpacityAt,BootOpacityV,.2f,.8f,.2f,1);scaleX=Track(k,BootAt,BootSxV,.2f,.8f,.2f,1);scaleY=Track(k,BootAt,BootSyV,.2f,.8f,.2f,1);translateX=Track(k,BootAt,BootTxV,.2f,.8f,.2f,1);
        }
        // .draft-reveal .chip-icon's die flicker over the first second (as NativeChipCard.Entrance draws it for every tier):
        // dark until 22%, a 1.7 flash at 28%, .35 at 33%, 1.35 at 41%, settling to 1; linear between keys.
        private static readonly float[] BrightAt={0,.22f,.28f,.33f,.41f,1},BrightV={.12f,.12f,1.7f,.35f,1.35f,1};
        public static float BootBright(float t)=>Track(t,BrightAt,BrightV,0,0,1,1);
        // pve-locked (2.2 s ease-in-out infinite) on the "???": opacity .7 -> 1 -> .7, colour #4e4470 -> #6d5fb0 -> back.
        private static readonly float[] LockedAt={0,.5f,1},LockedOpacityV={.7f,1,.7f},LockedMixV={0,1,0};
        public static float LockedPulse(float t,out float colourMix){float p=(float)(t/2.2-Math.Floor(t/2.2));colourMix=Track(p,LockedAt,LockedMixV,.42f,0,.58f,1);return Track(p,LockedAt,LockedOpacityV,.42f,0,.58f,1);}
        // A CSS keyframe track: per-segment cubic-bezier easing, fill-mode both.
        public static float Track(float p,float[] at,float[] v,float x1,float y1,float x2,float y2)
        {
            if(!(p>at[0]))return v[0];int n=at.Length-1;if(p>=at[n])return v[n];int i=0;while(at[i+1]<=p)i++;
            float u=(p-at[i])/(at[i+1]-at[i]);return v[i]+(v[i+1]-v[i])*NativeUiMotion.Bezier(x1,y1,x2,y2,u);
        }

        // ---- layout (CSS px of the viewport = the virtual canvas) ----
        // The face's view box on the screen: its 200 x 120 box scaled to the stare's width, centred at (50%, top).
        public static void Face(float W,float H,float top,out float x,out float y,out float unit){float w=StareWidth(W);unit=w/ViewWidth;x=W/2-w/2;y=H*top-w*ViewHeight/ViewWidth/2;}
        // .siren-cinema > .siren-flames: left -5%, bottom -3%, 110% x 48%; its 400 x 160 view box sliced (xMidYMax) to the box.
        public static void FlameBox(float W,float H,out float x,out float y,out float w,out float h,out float unit,out float contentX)
        {x=-.05f*W;w=1.1f*W;h=.48f*H;y=1.03f*H-h;unit=Math.Max(w/FlameViewWidth,h/FlameViewHeight);contentX=x+(w-FlameViewWidth*unit)/2;}
        // Tongue i (back row first): front row, left-hand path, its scale, and the affine from its path units to the screen
        // at lick clock t and rise fraction `rise` (screen x = tx + xx*px + xy*py, y = ty + yy*py), with the path-unit height
        // above which the flame box clips it (the svg viewport's top edge). `still` drops siren-lick (reduced motion).
        public static void Tongue(int i,float W,float H,float t,float rise,bool still,out bool front,out bool left,out float scale,out float xx,out float xy,out float yy,out float tx,out float ty,out float clipY)
        {
            int nb=StageBack.Length/2;front=i>=nb;int n=front?i-nb:i;var row=front?StageFront:StageBack;float x=row[n*2];scale=row[n*2+1];left=(n&1)==1;
            FlameBox(W,H,out _,out float by,out _,out float bh,out float u,out float cx);float a=1,skew=0;if(!still)Lick(t,n,out a,out skew);
            float k=u*scale,tan=(float)Math.Tan(skew*Math.PI/180);
            // translate(x 160) scale(s) . translate(o) scaleY(a) skewX(b) translate(-o): the origin o (the fill-box's bottom centre)
            // sits on y = 0, so it cancels: x' = px + tan*py, y' = a*py.
            xx=k;xy=k*tan;yy=k*a;tx=cx+u*x;ty=by+bh+rise*bh;clipY=-bh/(k*a);
        }

        // ---- path data ----
        // Absolute M/L/C/Z path data (the subset these drawings use) to polylines, cubics flattened in `steps`.
        public static List<float[]> Parse(string d,int steps)
        {
            var paths=new List<float[]>();var cur=new List<float>();int i=0;char cmd=' ';float x=0,y=0;
            float N(){while(i<d.Length&&(d[i]==' '||d[i]==','))i++;int s=i;if(i<d.Length&&(d[i]=='-'||d[i]=='+'))i++;while(i<d.Length&&(char.IsDigit(d[i])||d[i]=='.'))i++;return float.Parse(d.Substring(s,i-s),System.Globalization.CultureInfo.InvariantCulture);}
            void Flush(){if(cur.Count>=4)paths.Add(cur.ToArray());cur.Clear();}
            while(true)
            {
                while(i<d.Length&&(d[i]==' '||d[i]==','))i++;if(i>=d.Length)break;
                if(char.IsLetter(d[i]))cmd=d[i++];
                switch(cmd)
                {
                    case 'M':Flush();x=N();y=N();cur.Add(x);cur.Add(y);cmd='L';break;
                    case 'L':x=N();y=N();cur.Add(x);cur.Add(y);break;
                    case 'C':{float x1=N(),y1=N(),x2=N(),y2=N(),x3=N(),y3=N();for(int k=1;k<=steps;k++){float t=k/(float)steps,u=1-t;cur.Add(u*u*u*x+3*u*u*t*x1+3*u*t*t*x2+t*t*t*x3);cur.Add(u*u*u*y+3*u*u*t*y1+3*u*t*t*y2+t*t*t*y3);}x=x3;y=y3;break;}
                    case 'Z':{if(cur.Count>=4&&cur[cur.Count-2]==cur[0]&&cur[cur.Count-1]==cur[1]){cur.RemoveAt(cur.Count-1);cur.RemoveAt(cur.Count-1);}Flush();break;}
                    default:throw new FormatException("Unsupported path command "+cmd);
                }
            }
            Flush();return paths;
        }
        public static void Bounds(List<float[]> paths,out float x0,out float y0,out float x1,out float y1)
        {
            x0=y0=float.MaxValue;x1=y1=float.MinValue;
            foreach(var p in paths)for(int k=0;k+1<p.Length;k+=2){x0=Math.Min(x0,p[k]);x1=Math.Max(x1,p[k]);y0=Math.Min(y0,p[k+1]);y1=Math.Max(y1,p[k+1]);}
        }
        private static float[] Scaled(float[] p,float sx,float sy){var o=new float[p.Length];for(int k=0;k+1<p.Length;k+=2){o[k]=p[k]*sx;o[k+1]=p[k+1]*sy;}return o;}
        // The outline of an open polyline stroked `width` wide with butt caps and miter joins (bevelled past the miter
        // limit): one quad per segment and one wedge per join, all wound the same way so the nonzero fill unions them.
        public static List<float[]> MiterStroke(float[] line,float width,float limit)
        {
            var o=new List<float[]>();float h=width/2;int n=line.Length/2;
            for(int s=0;s+1<n;s++)
            {
                float ax=line[s*2],ay=line[s*2+1],bx=line[s*2+2],by=line[s*2+3],dx=bx-ax,dy=by-ay,l=(float)Math.Sqrt(dx*dx+dy*dy);if(l<=0)continue;float nx=-dy/l*h,ny=dx/l*h;
                o.Add(Wound(new[]{ax+nx,ay+ny,bx+nx,by+ny,bx-nx,by-ny,ax-nx,ay-ny}));
            }
            for(int j=1;j+1<n;j++)
            {
                float px=line[j*2],py=line[j*2+1],d0x=px-line[j*2-2],d0y=py-line[j*2-1],d1x=line[j*2+2]-px,d1y=line[j*2+3]-py;
                float l0=(float)Math.Sqrt(d0x*d0x+d0y*d0y),l1=(float)Math.Sqrt(d1x*d1x+d1y*d1y);if(l0<=0||l1<=0)continue;d0x/=l0;d0y/=l0;d1x/=l1;d1y/=l1;
                float cross=d0x*d1y-d0y*d1x;if(Math.Abs(cross)<1e-6f)continue;float side=cross>0?-1:1;
                float o0x=-d0y*side,o0y=d0x*side,o1x=-d1y*side,o1y=d1x*side,mx=o0x+o1x,my=o0y+o1y,ml=(float)Math.Sqrt(mx*mx+my*my);mx/=ml;my/=ml;float cos=mx*o0x+my*o0y;
                if(1/cos<=limit)o.Add(Wound(new[]{px,py,px+o0x*h,py+o0y*h,px+mx*h/cos,py+my*h/cos,px+o1x*h,py+o1y*h}));
                else o.Add(Wound(new[]{px,py,px+o0x*h,py+o0y*h,px+o1x*h,py+o1y*h}));
            }
            return o;
        }
        private static float[] Wound(float[] p)
        {
            float area=0;int n=p.Length/2;for(int k=0;k<n;k++){int m=(k+1)%n;area+=p[k*2]*p[m*2+1]-p[m*2]*p[k*2+1];}
            if(area<0)for(int a=0,b=n-1;a<b;a++,b--){float tx=p[a*2],ty=p[a*2+1];p[a*2]=p[b*2];p[a*2+1]=p[b*2+1];p[b*2]=tx;p[b*2+1]=ty;}
            return p;
        }
        // Total area of the closed paths (each by its own winding).
        public static float Area(List<float[]> paths){float total=0;foreach(var p in paths){float a=0;int n=p.Length/2;for(int k=0;k<n;k++){int m=(k+1)%n;a+=p[k*2]*p[m*2+1]-p[m*2]*p[k*2+1];}total+=Math.Abs(a)/2;}return total;}

        // ---- rasteriser ----
        /// <summary>One painted layer: polygons (nonzero fill) or a hairline stroke `Width` pixels wide, mapped to pixels by
        /// px = x*Ax + Bx, py = y*Ay + By, in a straight-alpha colour.</summary>
        public struct Layer{public List<float[]> Paths;public bool Hairline;public float Width,Ax,Bx,Ay,By,R,G,B,A;}
        public static Layer Paint(List<float[]> paths,int rgb,float alpha,float ax,float bx,float ay,float by,float hairline=0)
            =>new Layer{Paths=paths,Hairline=hairline>0,Width=hairline,Ax=ax,Bx=bx,Ay=ay,By=by,R=(rgb>>16&255)/255f,G=(rgb>>8&255)/255f,B=(rgb&255)/255f,A=alpha};
        /// <summary>Composites the layers (source-over) into `rgba` (w x h x 4 bytes, straight alpha, bottom row first as a
        /// texture stores it; fully clear texels take `bleedRgb` so bilinear filtering does not darken the edges). Each pixel
        /// row is sampled on `sub` scanlines with exact horizontal coverage.</summary>
        public static void Render(byte[] rgba,int w,int h,Layer[] layers,int sub,int bleedRgb)
        {
            var edges=new float[layers.Length][];for(int l=0;l<layers.Length;l++)edges[l]=Edges(layers[l]);
            var row=new float[w*4];var cover=new float[w];var scans=new Scan[layers.Length];for(int l=0;l<layers.Length;l++)if(!layers[l].Hairline){scans[l]=new Scan(w,edges[l].Length/4);scans[l].Load(edges[l],edges[l].Length/4);}byte br=(byte)(bleedRgb>>16&255),bg=(byte)(bleedRgb>>8&255),bb=(byte)(bleedRgb&255);
            for(int y=0;y<h;y++)
            {
                Array.Clear(row,0,row.Length);
                for(int l=0;l<layers.Length;l++)
                {
                    var L=layers[l];if(L.Hairline)HairRow(edges[l],y,w,L.Width,cover);else scans[l].Row(y,sub,cover);
                    for(int x=0;x<w;x++){float a=cover[x]*L.A;if(a<=0)continue;int i=x*4;float keep=1-a;row[i]=L.R*a+row[i]*keep;row[i+1]=L.G*a+row[i+1]*keep;row[i+2]=L.B*a+row[i+2]*keep;row[i+3]=a+row[i+3]*keep;}
                }
                int o=(h-1-y)*w*4;
                for(int x=0;x<w;x++){int i=x*4;float a=row[i+3];if(a<.5f/255){rgba[o+i]=br;rgba[o+i+1]=bg;rgba[o+i+2]=bb;rgba[o+i+3]=0;continue;}rgba[o+i]=B8(row[i]/a);rgba[o+i+1]=B8(row[i+1]/a);rgba[o+i+2]=B8(row[i+2]/a);rgba[o+i+3]=B8(a);}
            }
        }
        private static byte B8(float v)=>(byte)(v<=0?0:v>=1?255:(int)(v*255+.5f));
        /// <summary>A texture's next horizontal level: each texel the premultiplied mean of two (the last one with clear when
        /// the width is odd), so the grin squeezed to a slit (scaleX .04) samples a pre-filtered copy instead of aliasing.</summary>
        public static byte[] HalveWidth(byte[] src,int w,int h,out int half,int bleedRgb)
        {
            half=(w+1)/2;var o=new byte[half*h*4];byte br=(byte)(bleedRgb>>16&255),bg=(byte)(bleedRgb>>8&255),bb=(byte)(bleedRgb&255);
            for(int y=0;y<h;y++)for(int x=0;x<half;x++)
            {
                int a=(y*w+2*x)*4,b=a+4;bool has=2*x+1<w;float aa=src[a+3]/255f,ab=has?src[b+3]/255f:0,al=(aa+ab)/2;int i=(y*half+x)*4;
                if(al<.5f/255){o[i]=br;o[i+1]=bg;o[i+2]=bb;o[i+3]=0;continue;}
                for(int c=0;c<3;c++)o[i+c]=B8((src[a+c]/255f*aa+(has?src[b+c]/255f*ab:0))/2/al);o[i+3]=B8(al);
            }
            return o;
        }
        private static float[] Edges(Layer L)
        {
            int count=0;foreach(var p in L.Paths)count+=p.Length/2;var e=new float[count*4];int k=0;
            foreach(var p in L.Paths){int n=p.Length/2;for(int j=0;j<n;j++){int m=(j+1)%n;e[k++]=p[j*2]*L.Ax+L.Bx;e[k++]=p[j*2+1]*L.Ay+L.By;e[k++]=p[m*2]*L.Ax+L.Bx;e[k++]=p[m*2+1]*L.Ay+L.By;}}
            return e;
        }
        // Hairline coverage of pixel row y: by distance from each pixel centre to the nearest segment (round joins).
        private static void HairRow(float[] e,int y,int w,float width,float[] cover)
        {
            Array.Clear(cover,0,w);float hw=width/2,cy=y+.5f;
            for(int k=0;k<e.Length;k+=4)
            {
                float x0=e[k],y0=e[k+1],x1=e[k+2],y1=e[k+3];if(Math.Min(y0,y1)>cy+hw+1||Math.Max(y0,y1)<cy-hw-1)continue;
                int a=Math.Max(0,(int)Math.Floor(Math.Min(x0,x1)-hw-1)),b=Math.Min(w-1,(int)Math.Ceiling(Math.Max(x0,x1)+hw+1));float dx=x1-x0,dy=y1-y0,ll=dx*dx+dy*dy;
                for(int x=a;x<=b;x++)
                {
                    float px=x+.5f-x0,py=cy-y0,t=ll>0?Math.Max(0,Math.Min(1,(px*dx+py*dy)/ll)):0,ex=px-dx*t,ey=py-dy*t,c=hw+.5f-(float)Math.Sqrt(ex*ex+ey*ey);
                    if(c>cover[x])cover[x]=c>1?1:c;
                }
            }
        }
        /// <summary>Row coverage of one edge list: an edge table sorted by top with an active list, so each sub-scanline
        /// visits only the edges it crosses. Load once, then ask for rows top to bottom. Reused; nothing allocates per row.</summary>
        public sealed class Scan
        {
            private readonly float[] acc,run;private float[] xs=new float[0],ey0=new float[0],ey1=new float[0],ex0=new float[0],slope=new float[0];
            private int[] dirs=new int[0],edir=new int[0],active=new int[0];private readonly int width;private int count,next,live;
            public Scan(int w,int edges){width=w;acc=new float[w+1];run=new float[w+2];Reserve(edges);}
            private void Reserve(int n)
            {
                n=Math.Max(4,n);if(ey0.Length>=n)return;
                xs=new float[n];ey0=new float[n];ey1=new float[n];ex0=new float[n];slope=new float[n];dirs=new int[n];edir=new int[n];active=new int[n];
            }
            /// <summary>Takes `n` edges (x0, y0, x1, y1 quadruples in pixels); horizontal ones never cross a scanline.</summary>
            public void Load(float[] e,int n)
            {
                Reserve(n);count=next=live=0;
                for(int k=0;k<n;k++)
                {
                    float x0=e[k*4],y0=e[k*4+1],x1=e[k*4+2],y1=e[k*4+3];if(y0==y1)continue;int d=1;if(y0>y1){d=-1;float tx=x0,ty=y0;x0=x1;y0=y1;x1=tx;y1=ty;}
                    int j=count++;while(j>0&&ey0[j-1]>y0){ey0[j]=ey0[j-1];ey1[j]=ey1[j-1];ex0[j]=ex0[j-1];slope[j]=slope[j-1];edir[j]=edir[j-1];j--;}
                    ey0[j]=y0;ey1[j]=y1;ex0[j]=x0;slope[j]=(x1-x0)/(y1-y0);edir[j]=d;
                }
            }
            public void Row(int y,int sub,float[] cover)
            {
                int w=width;Array.Clear(acc,0,w+1);Array.Clear(run,0,w+2);float wt=1f/sub;
                for(int s=0;s<sub;s++)
                {
                    float sy=y+(s+.5f)/sub;while(next<count&&ey0[next]<=sy)active[live++]=next++;int n=0;
                    for(int a=0;a<live;)
                    {
                        int i=active[a];if(ey1[i]<=sy){active[a]=active[--live];continue;}a++;
                        float x=ex0[i]+(sy-ey0[i])*slope[i];int j=n++;while(j>0&&xs[j-1]>x){xs[j]=xs[j-1];dirs[j]=dirs[j-1];j--;}xs[j]=x;dirs[j]=edir[i];
                    }
                    int wind=0;float start=0;
                    for(int j=0;j<n;j++){int before=wind;wind+=dirs[j];if(before==0&&wind!=0)start=xs[j];else if(before!=0&&wind==0)Span(start,xs[j],wt);}
                }
                float r=0;for(int x=0;x<w;x++){r+=run[x];float c=acc[x]+r;cover[x]=c>1?1:c<0?0:c;}
            }
            private void Span(float xa,float xb,float wt)
            {
                int w=width;if(xa<0)xa=0;if(xb>w)xb=w;if(xb<=xa)return;int ia=(int)xa,ib=(int)xb;if(ia>=w)return;
                if(ia==ib){acc[ia]+=(xb-xa)*wt;return;}
                acc[ia]+=(ia+1-xa)*wt;run[ia+1]+=wt;run[ib]-=wt;if(ib<w)acc[ib]+=(xb-ib)*wt;
            }
        }

        /// <summary>The face's drop-shadow: the eyes and the grin at its current width and opacity on a coarse grid
        /// (`Texel` virtual px), blurred by three box passes each way (a Gaussian of standard deviation GlowSigma), in the
        /// glow colour at GlowAlpha. Rebuilt only when its inputs change; buffers are kept, so a rebuild allocates nothing.</summary>
        public sealed class Glow
        {
            public const float Texel=6;
            public int Width,Height;public float X0,Y0,UnitsPerTexel;public byte[] Rgba=new byte[0];
            private float[] src=new float[0],tmp=new float[0],keep=new float[0],line=new float[0],cover=new float[0],eyeEdges=new float[0],mouthEdges=new float[0];private Scan eyeScan,mouthScan;
            private float lastUnit=-1,lastScale=-1,lastAlpha=-1;private byte[] lastCurve;private static readonly int[] Boxes=BoxSizes(GlowSigma/Texel,3);
            /// <summary>Returns true when the bytes changed. `unit` is virtual px per view-box unit; `curve` (256 entries, or
            /// null) remaps the alpha, for a target that blends in linear light (the kit's LightAlpha).</summary>
            public bool Build(float unit,float mouthScaleX,float mouthAlpha,byte[] curve=null)
            {
                if(unit==lastUnit&&mouthScaleX==lastScale&&mouthAlpha==lastAlpha&&curve==lastCurve)return false;lastCurve=curve;
                float margin=(3*GlowSigma+2*Texel)/unit;UnitsPerTexel=Texel/unit;X0=MouthX0-margin;Y0=EyesY0-margin;
                int w=(int)Math.Ceiling((MouthX1+margin-X0)/UnitsPerTexel),h=(int)Math.Ceiling((MouthY1+margin-Y0)/UnitsPerTexel);
                if(w!=Width||h!=Height||eyeScan==null)
                {
                    Width=w;Height=h;src=new float[w*h];tmp=new float[w*h];keep=new float[w*h];line=new float[Math.Max(w,h)];cover=new float[w];Rgba=new byte[w*h*4];
                    int ne=0;foreach(var p in Eyes)ne+=p.Length/2;eyeEdges=new float[ne*4];int nm=0;foreach(var p in Grin)nm+=p.Length/2;foreach(var p in TeethStroke)nm+=p.Length/2;mouthEdges=new float[nm*4];eyeScan=new Scan(w,ne);mouthScan=new Scan(w,nm);
                }
                lastUnit=unit;lastScale=mouthScaleX;lastAlpha=mouthAlpha;float a=1/UnitsPerTexel;
                Fill(eyeEdges,Eyes,null,a,-X0*a,a,-Y0*a);float ka=a*mouthScaleX;Fill(mouthEdges,Grin,TeethStroke,ka,(MouthOriginX*(1-mouthScaleX)-X0)*a,a,-Y0*a);
                eyeScan.Load(eyeEdges,eyeEdges.Length/4);mouthScan.Load(mouthEdges,mouthEdges.Length/4);
                for(int y=0;y<h;y++)
                {
                    eyeScan.Row(y,4,cover);for(int x=0;x<w;x++)src[y*w+x]=cover[x];
                    if(mouthAlpha>0){mouthScan.Row(y,4,cover);for(int x=0;x<w;x++){float m=cover[x]*mouthAlpha,e=src[y*w+x];src[y*w+x]=e+m-e*m;}}
                }
                // The face is one group under its filter and opacity: the shapes hide their own shadow. Knock the glow out
                // where the coarse coverage is solid all around (eroded by a texel, so the glow at the edges stays whole).
                for(int y=0;y<h;y++)for(int x=0;x<w;x++){int i=y*w+x;tmp[i]=x==0||x==w-1?0:Math.Min(src[i-1],Math.Min(src[i],src[i+1]));}
                for(int y=0;y<h;y++)for(int x=0;x<w;x++){int i=y*w+x;keep[i]=y==0||y==h-1?1:1-Math.Min(tmp[i-w],Math.Min(tmp[i],tmp[i+w]));}
                foreach(int b in Boxes){Blur(src,tmp,w,h,b,true);Blur(tmp,src,w,h,b,false);}
                byte r=(byte)(GlowRgb>>16&255),g=(byte)(GlowRgb>>8&255),bl=(byte)(GlowRgb&255);
                for(int y=0;y<h;y++){int o=(h-1-y)*w*4;for(int x=0;x<w;x++){int i=o+x*4;Rgba[i]=r;Rgba[i+1]=g;Rgba[i+2]=bl;byte v=B8(src[y*w+x]*keep[y*w+x]*GlowAlpha);Rgba[i+3]=curve!=null?curve[v]:v;}}
                return true;
            }
            private static void Fill(float[] e,List<float[]> a,List<float[]> b,float ax,float bx,float ay,float by)
            {int k=0;for(int pass=0;pass<2;pass++){var list=pass==0?a:b;if(list==null)continue;foreach(var p in list){int n=p.Length/2;for(int j=0;j<n;j++){int m=(j+1)%n;e[k++]=p[j*2]*ax+bx;e[k++]=p[j*2+1]*ay+by;e[k++]=p[m*2]*ax+bx;e[k++]=p[m*2+1]*ay+by;}}}}
            // One box pass of odd width `size` along rows (or columns), zero outside, from `from` into `to`.
            private void Blur(float[] from,float[] to,int w,int h,int size,bool rows)
            {
                int r=size/2,n=rows?w:h,lines=rows?h:w;float inv=1f/size;
                for(int l=0;l<lines;l++)
                {
                    for(int i=0;i<n;i++)line[i]=rows?from[l*w+i]:from[i*w+l];
                    float sum=0;for(int i=0;i<=r&&i<n;i++)sum+=line[i];
                    for(int i=0;i<n;i++){float v=sum*inv;if(rows)to[l*w+i]=v;else to[i*w+l]=v;int add=i+r+1,drop=i-r;if(add<n)sum+=line[add];if(drop>=0)sum-=line[drop];}
                }
            }
        }
        // Widths of `n` box blurs approximating a Gaussian of standard deviation `sigma` (in texels).
        public static int[] BoxSizes(float sigma,int n)
        {
            double ideal=Math.Sqrt(12*sigma*sigma/n+1);int wl=(int)Math.Floor(ideal);if(wl%2==0)wl--;int wu=wl+2;
            int m=(int)Math.Round((12*sigma*sigma-n*wl*wl-4*n*wl-3*n)/(-4.0*wl-4));var sizes=new int[n];for(int i=0;i<n;i++)sizes[i]=i<m?wl:wu;return sizes;
        }
        // A 2 x 2 matrix [[a, b], [c, d]] (positive determinant) as rotate(theta) . scale(sx, sy) . rotate(phi), angles in
        // degrees with Unity's TRS convention, for the kit's rotate-and-scale transforms (a skewed tongue as two pushes).
        public static void Decompose(float a,float b,float c,float d,out float theta,out float sx,out float sy,out float phi)
        {
            double e=(a+d)/2.0,f=(a-d)/2.0,g=(c+b)/2.0,h=(c-b)/2.0,q=Math.Sqrt(e*e+h*h),r=Math.Sqrt(f*f+g*g),a1=Math.Atan2(g,f),a2=Math.Atan2(h,e);
            sx=(float)(q+r);sy=(float)(q-r);theta=(float)((a2+a1)/2*180/Math.PI);phi=(float)((a2-a1)/2*180/Math.PI);
        }
    }
}
