using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // Shared IMGUI drawing kit for the native overlays. Callers lay out in the virtual canvas (NativeUiMotion.Measure:
    // 900 units tall at 16:9); the kit converts to physical pixels and draws text at its final pixel size with an identity
    // matrix, so text stays sharp at every resolution (3440x1440 included) instead of being stretched by GUI.matrix.
    // Animated groups push a transform (translate/scale/rotate about a pivot) that applies to everything drawn inside.
    // Textures are created lazily on the first GUI event, never in field initializers.
    public static class NativeUi
    {
        public static float S=1,W=1600,H=900,SafeX,SafeW=1600;
        private static Texture2D white,vertical,horizontal,radial,soft,verticalL,horizontalL,radialL,softL;private static readonly Dictionary<ulong,Texture2D> stops=new Dictionary<ulong,Texture2D>();private static Texture2D[] rounds=new Texture2D[4];
        private static GUIStyle text;private static Font mono;private static bool monoTried;
        private static readonly Stack<Matrix4x4> stack=new Stack<Matrix4x4>();private static Matrix4x4 current=Matrix4x4.identity;
        private static readonly GUIContent content=new GUIContent();
        public static NativeUiMotion.Layout Begin(float uiScale=1)
        {
            var l=NativeUiMotion.Measure(Screen.width,Screen.height,uiScale);S=l.Scale;W=l.Width;H=l.Height;SafeX=l.SafeX;SafeW=l.SafeWidth;
            stack.Clear();current=Matrix4x4.identity;deferring=0;deferred.Clear();pivotShift=Vector2.zero;LinearBlend=QualitySettings.activeColorSpace==ColorSpace.Linear&&GL.sRGBWrite;Ensure();return l;
        }
        // Colour space. The overlays reproduce CSS, which blends translucent layers on sRGB values. Since build #7 the player
        // renders in Linear colour space: the backbuffer is sRGB and IMGUI's alpha blending happens in linear light, so every
        // translucent layer lands elsewhere. A light layer over the dark UI comes out far stronger (a 19% white sheen reads as
        // ~47% grey, a 26% rarity glow spreads into a haze) and a dark layer weaker (a 55% shadow darkens by ~30%), flattening
        // the chip card's glows, sheens, shadows and text halos. Tone remaps a translucent draw's alpha so the linear blend
        // lands where the sRGB one does: a light layer takes a^2.2 (exact over black, as the dark UI mostly is) and draws its
        // ramp textures (fades, glows, radial gradients) with alpha^2.2 too; a dark layer takes 1-(1-a)^2.2 (exact for black
        // over anything); mixed by the colour's lightness. Opaque draws, solid colours and Gamma builds are unchanged.
        public static bool LinearBlend;
        public static Color Tone(Color c)
        {
            if(!LinearBlend||c.a<=0||c.a>=1)return c;float w=Lightness(c),a=c.a;
            c.a=Mathf.Lerp(1-Mathf.Pow(1-a,2.2f),Mathf.Pow(a,2.2f),w);return c;
        }
        private static float Lightness(Color c){float v=Mathf.Max(c.r,Mathf.Max(c.g,c.b));return Mathf.SmoothStep(0,1,Mathf.Clamp01((v-.08f)/.25f));}
        // Light layers in Linear blending use the ramp textures re-shaped to alpha^2.2.
        private static bool LightRamp(Color c)=>LinearBlend&&Lightness(c)>.5f;
        private static Texture2D Make(int w,int h,Func<int,int,Color> f)
        {
            var t=new Texture2D(w,h,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
            var px=new Color[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++)px[y*w+x]=f(x,y);t.SetPixels(px);t.Apply(false,true);return t;
        }
        private static void Ensure()
        {
            if(white!=null)return;
            white=Make(2,2,(x,y)=>Color.white);
            vertical=Make(1,64,(x,y)=>new Color(1,1,1,y/63f));                                   // alpha 0 at the bottom row, 1 at the top
            horizontal=Make(64,1,(x,y)=>new Color(1,1,1,x/63f));                                 // alpha 0 at the left, 1 at the right
            radial=Make(64,64,(x,y)=>{float dx=(x-31.5f)/31.5f,dy=(y-31.5f)/31.5f,d=Mathf.Clamp01(Mathf.Sqrt(dx*dx+dy*dy));return new Color(1,1,1,1-d);});
            soft=Make(64,64,(x,y)=>{float dx=Mathf.Clamp01((Mathf.Abs(x-31.5f)-14)/18f),dy=Mathf.Clamp01((Mathf.Abs(y-31.5f)-14)/18f),d=Mathf.Clamp01(Mathf.Sqrt(dx*dx+dy*dy));return new Color(1,1,1,(1-d)*(1-d));});
            // The same ramps with alpha^2.2, for light layers under Linear blending (see Tone).
            verticalL=Make(1,64,(x,y)=>new Color(1,1,1,Mathf.Pow(y/63f,2.2f)));horizontalL=Make(64,1,(x,y)=>new Color(1,1,1,Mathf.Pow(x/63f,2.2f)));
            radialL=Make(64,64,(x,y)=>{float dx=(x-31.5f)/31.5f,dy=(y-31.5f)/31.5f,d=Mathf.Clamp01(Mathf.Sqrt(dx*dx+dy*dy));return new Color(1,1,1,Mathf.Pow(1-d,2.2f));});
            softL=Make(64,64,(x,y)=>{float dx=Mathf.Clamp01((Mathf.Abs(x-31.5f)-14)/18f),dy=Mathf.Clamp01((Mathf.Abs(y-31.5f)-14)/18f),d=Mathf.Clamp01(Mathf.Sqrt(dx*dx+dy*dy));return new Color(1,1,1,Mathf.Pow((1-d)*(1-d),2.2f));});
            for(int i=0;i<rounds.Length;i++){int r=4+i*4,n=r*2+2;rounds[i]=Make(n,n,(x,y)=>{float cx=Mathf.Clamp(x+.5f,r,n-r),cy=Mathf.Clamp(y+.5f,r,n-r),d=Mathf.Sqrt((x+.5f-cx)*(x+.5f-cx)+(y+.5f-cy)*(y+.5f-cy));return new Color(1,1,1,Mathf.Clamp01(r-d+.5f));});}
            text=new GUIStyle(GUI.skin.label){wordWrap=true,richText=false,clipping=TextClipping.Overflow,padding=new RectOffset(0,0,0,0),margin=new RectOffset(0,0,0,0)};
            // White base so GUI.contentColor is the exact colour drawn (the menu skin's label colour would tint every overlay).
            text.normal.textColor=Color.white;
        }
        public static Font MonoFont=>Mono();
        private static Font Mono()
        {
            if(!monoTried){monoTried=true;try{mono=Font.CreateDynamicFontFromOSFont(new[]{"Consolas","Cascadia Mono","DejaVu Sans Mono","Courier New"},14);}catch(Exception){mono=null;}}
            return mono;
        }
        // Virtual rect to physical pixels.
        public static Rect P(Rect v)=>new Rect(v.x*S,v.y*S,v.width*S,v.height*S);
        public static Vector2 P(Vector2 v)=>v*S;
        // Every draw sets the kit's transform and restores the caller's GUI.matrix, so legacy virtual-canvas GUI code
        // interleaves freely with kit drawing.
        private static Matrix4x4 Enter(){var m=GUI.matrix;GUI.matrix=current;return m;}
        // Kit drawing for content laid out inside a GUILayout area and scroll view. IMGUI clips such a group in virtual
        // units under GUI.matrix and maps that clip with the matrix current at draw time, so the kit (physical-pixel
        // matrix, crisp text) cannot draw inside it. The body reserves space with GUILayoutUtility.GetRect and queues its
        // kit drawing with Defer; EndDeferred, called at the root after GUILayout.EndArea, replays the queue inside a
        // physical-pixel clip of the area, shifted by the scroll, so rects stay in the scroll content's space as GetRect
        // returned them. The queue runs in every event, so kit buttons and hover work as anywhere else.
        private static readonly List<Action> deferred=new List<Action>();private static int deferring;private static Vector2 pivotShift;
        public static void BeginDeferred(){if(deferring++==0)deferred.Clear();}
        public static void Defer(Action draw){if(deferring>0)deferred.Add(draw);else draw();}
        public static void EndDeferred(Rect area,Vector2 scroll)
        {
            if(deferring>0&&--deferring>0)return;if(deferred.Count==0)return;var actions=deferred.ToArray();deferred.Clear();
            // IMGUI adds the group's offset before GUI.matrix, so rotations pushed during the replay pivot about the pivot
            // plus that offset (Push applies pivotShift).
            var m=GUI.matrix;GUI.matrix=Matrix4x4.identity;var pa=P(area);var offset=new Vector2(-Mathf.Round(scroll.x*S),-Mathf.Round(scroll.y*S));GUI.BeginClip(pa,offset,Vector2.zero,false);GUI.matrix=m;pivotShift=pa.position+offset;
            try{foreach(var a in actions)a();}finally{pivotShift=Vector2.zero;GUI.matrix=Matrix4x4.identity;GUI.EndClip();GUI.matrix=m;}
        }
        // Push a transform about a virtual-space pivot: translate (virtual units), uniform scale and rotation (degrees).
        public static void Push(Vector2 pivot,Vector2 translate,float scale=1,float degrees=0,float scaleY=float.NaN)
        {
            stack.Push(current);Vector3 p=pivot*S+pivotShift;float sy=float.IsNaN(scaleY)?scale:scaleY;
            current=current*Matrix4x4.TRS(p+(Vector3)(translate*S),Quaternion.Euler(0,0,degrees),new Vector3(scale,sy,1))*Matrix4x4.Translate(-p);
        }
        public static void Pop(){current=stack.Count>0?stack.Pop():Matrix4x4.identity;}
        public static void Fill(Rect r,Color c){if(c.a<=0||Event.current.type!=EventType.Repaint)return;var m=Enter();var o=GUI.color;GUI.color=Tone(c);GUI.DrawTexture(P(r),white);GUI.color=o;GUI.matrix=m;}
        // Vertical gradient: `bottom` underneath, `top` laid over it with an alpha ramp that is full at the top edge.
        public static void Gradient(Rect r,Color top,Color bottom)
        {
            if(Event.current.type!=EventType.Repaint)return;var m=Enter();var o=GUI.color;var pr=P(r);
            if(bottom.a>0){GUI.color=Tone(bottom);GUI.DrawTexture(pr,white);}if(top.a>0){GUI.color=Tone(top);GUI.DrawTexture(pr,LightRamp(top)?verticalL:vertical);}
            GUI.color=o;GUI.matrix=m;
        }
        // Fade from `c` at the top to transparent at the bottom (or the reverse).
        public static void Fade(Rect r,Color c,bool fromTop=true)
        {
            if(c.a<=0||Event.current.type!=EventType.Repaint)return;var m=Enter();var o=GUI.color;GUI.color=Tone(c);var pr=P(r);var ramp=LightRamp(c)?verticalL:vertical;
            if(fromTop)GUI.DrawTexture(pr,ramp);else GUI.DrawTextureWithTexCoords(pr,ramp,new Rect(0,1,1,-1));GUI.color=o;GUI.matrix=m;
        }
        // Horizontal gradient from `left` to `right`.
        public static void HGradient(Rect r,Color left,Color right)
        {
            if(Event.current.type!=EventType.Repaint)return;var m=Enter();var o=GUI.color;var pr=P(r);
            if(left.a>0){GUI.color=Tone(left);GUI.DrawTextureWithTexCoords(pr,LightRamp(left)?horizontalL:horizontal,new Rect(1,0,-1,1));}if(right.a>0){GUI.color=Tone(right);GUI.DrawTexture(pr,LightRamp(right)?horizontalL:horizontal);}
            GUI.color=o;GUI.matrix=m;
        }
        // CSS radial-gradient(ellipse ...) filling `r` (the ellipse's bounding box): alpha stops at normalised radii.
        // Texture caches are keyed by a 64-bit FNV-1a hash of their inputs, so a lookup allocates nothing.
        private const ulong KeySeed=14695981039346656037UL;
        private static ulong Mix(ulong h,float v){unchecked{h^=(uint)BitConverter.SingleToInt32Bits(v);return h*1099511628211UL;}}
        private static ulong Mix(ulong h,Color c)=>Mix(Mix(Mix(Mix(h,c.r),c.g),c.b),c.a);
        // Texture builders live in their own methods: a lambda capturing the drawing call's parameters would allocate its
        // closure on every call, cache hit or not.
        private static Texture2D RadialTexture(float[] at,float[] alpha,float power)=>Make(96,96,(x,y)=>{float dx=(x-47.5f)/47.5f,dy=(y-47.5f)/47.5f;return new Color(1,1,1,Mathf.Pow(NativeUiMotion.Keys(Mathf.Sqrt(dx*dx+dy*dy),at,alpha),power));});
        private static Texture2D RampTexture(Color a,Color b,bool horizontal)=>horizontal?Make(64,1,(x,y)=>Tone(Color.Lerp(a,b,x/63f))):Make(1,64,(x,y)=>Tone(Color.Lerp(b,a,y/63f)));
        public static void RadialStops(Rect r,Color c,float[] at,float[] alpha)
        {
            if(c.a<=0||Event.current.type!=EventType.Repaint)return;float power=LightRamp(c)?2.2f:1;ulong key=Mix(KeySeed,power>1?6:1);foreach(float v in at)key=Mix(key,v);key=Mix(key,float.NaN);foreach(float v in alpha)key=Mix(key,v);
            if(!stops.TryGetValue(key,out var t)){t=RadialTexture(at,alpha,power);stops[key]=t;}
            var m=Enter();var o=GUI.color;GUI.color=Tone(c);GUI.DrawTexture(P(r),t);GUI.color=o;GUI.matrix=m;
        }
        public static void Radial(Rect r,Color c){if(c.a<=0||Event.current.type!=EventType.Repaint)return;var m=Enter();var o=GUI.color;GUI.color=Tone(c);GUI.DrawTexture(P(r),LightRamp(c)?radialL:radial);GUI.color=o;GUI.matrix=m;}
        public static void Image(Rect r,Texture t,Color c){if(t==null||c.a<=0||Event.current.type!=EventType.Repaint)return;var m=Enter();var o=GUI.color;GUI.color=Tone(c);GUI.DrawTexture(P(r),t);GUI.color=o;GUI.matrix=m;}
        public static void Glow(Rect r,Color c,float spread){if(c.a<=0||Event.current.type!=EventType.Repaint)return;var m=Enter();var o=GUI.color;GUI.color=Tone(c);GUI.DrawTexture(P(new Rect(r.x-spread,r.y-spread,r.width+spread*2,r.height+spread*2)),LightRamp(c)?softL:soft);GUI.color=o;GUI.matrix=m;}
        // Rounded rectangle (radius 4/8/12/16 px class) with an optional border.
        public static void Round(Rect r,Color fill,float radius=8,Color border=default,float borderWidth=1)
        {
            if(Event.current.type!=EventType.Repaint)return;var m=Enter();int i=Mathf.Clamp(Mathf.RoundToInt(radius*S/4)-1,0,rounds.Length-1);var t=rounds[i];int rr=4+i*4;var o=GUI.color;var pr=P(r);
            // CSS paints the background under the border (background-clip: border-box), then the border ring over it, so a
            // translucent fill stays translucent inside its edge.
            if(fill.a>0)Nine(pr,t,rr,fill);if(border.a>0)Nine(pr,Ring(i,Mathf.Max(1,borderWidth*S)),rr,border);GUI.color=o;GUI.matrix=m;
        }
        // A rounded fill drawn now in the current GUI space (inside a GUILayout group, under its matrix and clip), for a
        // backdrop that must sit beneath GUILayout controls: deferred kit drawing lands above them.
        public static void UnderRound(Rect r,Color fill,float radius=8)
        {if(Event.current.type!=EventType.Repaint||fill.a<=0)return;Ensure();var o=GUI.color;int i=Mathf.Clamp(Mathf.RoundToInt(radius/4)-1,0,rounds.Length-1);Nine(r,rounds[i],4+i*4,fill);GUI.color=o;}
        private static readonly Dictionary<int,Texture2D> rings=new Dictionary<int,Texture2D>();
        // The outline of rounds[i], `w` physical pixels thick: the outer contour less the inner one (radius r - w).
        private static Texture2D Ring(int i,float w)
        {
            int key=i*1000+Mathf.RoundToInt(w*8);if(rings.TryGetValue(key,out var t)&&t!=null)return t;t=BuildRing(4+i*4,w);rings[key]=t;return t;
        }
        private static Texture2D BuildRing(int r,float w)
        {
            int n=r*2+2;w=Mathf.Min(w,r);
            return Make(n,n,(x,y)=>{float cx=Mathf.Clamp(x+.5f,r,n-r),cy=Mathf.Clamp(y+.5f,r,n-r),d=Mathf.Sqrt((x+.5f-cx)*(x+.5f-cx)+(y+.5f-cy)*(y+.5f-cy));return new Color(1,1,1,Mathf.Clamp01(r-d+.5f)-Mathf.Clamp01(r-w-d+.5f));});
        }
        // Rounded rectangle filled with a linear gradient (CSS linear-gradient under border-radius): from `a` at the top
        // (left when `horizontal`) to `b`, reached at `until` of the length and held after it. The corner bands take the
        // nearer end colour; the straight middle carries the ramp. Optional border as Round.
        public static void RoundGradient(Rect r,Color a,Color b,float radius=8,Color border=default,float until=1,bool horizontal=false,float borderWidth=1)
        {
            if(Event.current.type!=EventType.Repaint)return;var m=Enter();int i=Mathf.Clamp(Mathf.RoundToInt(radius*S/4)-1,0,rounds.Length-1);var t=rounds[i];int rr=4+i*4;var o=GUI.color;var pr=P(r);
            float k=Mathf.Min(rr,Mathf.Min(pr.width,pr.height)/2),u=rr/(float)t.width;
            for(int band=0;band<2;band++)
            {
                GUI.color=Tone(band==0?a:b);
                for(int c=0;c<3;c++)
                {
                    // Along the gradient: the band's corner slice; across it: the three slices of that edge.
                    float[] xs={pr.x,pr.x+k,pr.xMax-k,pr.xMax},ys={pr.y,pr.y+k,pr.yMax-k,pr.yMax},us={0,u,1-u,1},vs={1,1-u,u,0};
                    Rect q,uv;
                    if(!horizontal){int j=band==0?0:2;q=new Rect(xs[c],ys[j],xs[c+1]-xs[c],ys[j+1]-ys[j]);uv=new Rect(us[c],vs[j+1],us[c+1]-us[c],vs[j]-vs[j+1]);}
                    else{int j=band==0?0:2;q=new Rect(xs[j],ys[c],xs[j+1]-xs[j],ys[c+1]-ys[c]);uv=new Rect(us[j],vs[c+1],us[j+1]-us[j],vs[c]-vs[c+1]);}
                    if(q.width>0&&q.height>0)GUI.DrawTextureWithTexCoords(q,t,uv);
                }
            }
            var mid=horizontal?new Rect(pr.x+k,pr.y,pr.width-2*k,pr.height):new Rect(pr.x,pr.y+k,pr.width,pr.height-2*k);
            if(mid.width>0&&mid.height>0)
            {
                float span=Mathf.Clamp01(until)*(horizontal?pr.width:pr.height)-k;
                Rect ramp=horizontal?new Rect(mid.x,mid.y,Mathf.Clamp(span,0,mid.width),mid.height):new Rect(mid.x,mid.y,mid.width,Mathf.Clamp(span,0,mid.height));
                // One layer with the ramp baked into a cached texture (overlaying two translucent layers would compound).
                ulong key=Mix(Mix(Mix(KeySeed,(horizontal?2:3)+(LinearBlend?8:0)),a),b);
                if(!stops.TryGetValue(key,out var ramp1)){ramp1=RampTexture(a,b,horizontal);stops[key]=ramp1;}
                GUI.color=Color.white;if(ramp.width>0&&ramp.height>0)GUI.DrawTexture(ramp,ramp1);
                var rest=horizontal?new Rect(ramp.xMax,mid.y,mid.xMax-ramp.xMax,mid.height):new Rect(mid.x,ramp.yMax,mid.width,mid.yMax-ramp.yMax);if(rest.width>0&&rest.height>0){GUI.color=Tone(b);GUI.DrawTexture(rest,white);}
            }
            if(border.a>0)Nine(pr,Ring(i,Mathf.Max(1,borderWidth*S)),rr,border);
            GUI.color=o;GUI.matrix=m;
        }
        // A 1 px dashed line (6 px dashes, 4 px gaps at virtual scale) tiled from one cached texture: one draw per edge.
        private static Texture2D dashH,dashV;
        public static void Dashed(Rect r,Color c,bool horizontal=true)
        {
            if(c.a<=0||Event.current.type!=EventType.Repaint)return;
            if(dashH==null){dashH=Make(10,1,(x,y)=>new Color(1,1,1,x<6?1:0));dashH.wrapMode=TextureWrapMode.Repeat;dashH.filterMode=FilterMode.Point;dashV=Make(1,10,(x,y)=>new Color(1,1,1,y>=4?1:0));dashV.wrapMode=TextureWrapMode.Repeat;dashV.filterMode=FilterMode.Point;}
            var m=Enter();var o=GUI.color;GUI.color=Tone(c);float len=horizontal?r.width:r.height;
            GUI.DrawTextureWithTexCoords(P(r),horizontal?dashH:dashV,horizontal?new Rect(0,0,len/10,1):new Rect(0,0,1,len/10));GUI.color=o;GUI.matrix=m;
        }
        private static void Nine(Rect r,Texture2D t,int radius,Color c)
        {
            float k=Mathf.Min(radius,Mathf.Min(r.width,r.height)/2);float u=radius/(float)t.width;GUI.color=Tone(c);
            float[] xs={r.x,r.x+k,r.xMax-k,r.xMax},ys={r.y,r.y+k,r.yMax-k,r.yMax},us={0,u,1-u,1},vs={1,1-u,u,0};
            for(int i=0;i<3;i++)for(int j=0;j<3;j++){var q=new Rect(xs[i],ys[j],xs[i+1]-xs[i],ys[j+1]-ys[j]);if(q.width<=0||q.height<=0)continue;GUI.DrawTextureWithTexCoords(q,t,new Rect(us[i],vs[j+1],us[i+1]-us[i],vs[j]-vs[j+1]));}
        }
        public static void Pill(Rect r,Color fill,Color border){Round(r,fill,Mathf.Min(16,r.height/2),border);}
        // A full capsule (border-radius 999px) filled with a vertical gradient (CSS linear-gradient(180deg, stops)),
        // optionally with a 1 px border; three-sliced so the caps stay round at any width. `tint` multiplies everything.
        public static void Capsule(Rect r,float[] at,Color[] colors,Color border=default,float tint=1)
        {
            if(Event.current.type!=EventType.Repaint||r.width<=0||r.height<=0)return;var m=Enter();var o=GUI.color;var pr=P(r);
            if(border.a>0){CapsuleSlices(pr,CapsuleTexture(new[]{0f},new[]{Color.white}),A(border,tint));float b=Mathf.Max(1,S);pr=new Rect(pr.x+b,pr.y+b,pr.width-2*b,pr.height-2*b);}
            CapsuleSlices(pr,CapsuleTexture(at,colors),new Color(1,1,1,tint));GUI.color=o;GUI.matrix=m;
        }
        private static Texture2D CapsuleTexture(float[] at,Color[] colors)
        {
            ulong k=Mix(KeySeed,4);for(int i=0;i<at.Length;i++)k=Mix(Mix(k,at[i]),colors[i]);
            if(stops.TryGetValue(k,out var t))return t;t=BuildCapsule(at,colors);stops[k]=t;return t;
        }
        private static Texture2D BuildCapsule(float[] at,Color[] colors)
        {
            const int n=128;
            return Make(n,n,(x,y)=>{float v=1-(y+.5f)/n,cx=x+.5f,cy=y+.5f,d=Mathf.Sqrt((cx-n/2f)*(cx-n/2f)+(cy-n/2f)*(cy-n/2f));var c=colors[0];
                for(int i=1;i<at.Length;i++)if(v>=at[i-1])c=v>=at[i]?colors[i]:Color.Lerp(colors[i-1],colors[i],(v-at[i-1])/Mathf.Max(1e-5f,at[i]-at[i-1]));
                c.a*=Mathf.Clamp01(n/2f-d+.5f);return c;});
        }
        private static void CapsuleSlices(Rect r,Texture2D t,Color c)
        {
            GUI.color=Tone(c);float cap=Mathf.Min(r.height/2,r.width/2);
            GUI.DrawTextureWithTexCoords(new Rect(r.x,r.y,cap,r.height),t,new Rect(0,0,.5f,1));
            if(r.width>2*cap)GUI.DrawTextureWithTexCoords(new Rect(r.x+cap,r.y,r.width-2*cap,r.height),t,new Rect(.5f,0,0,1));
            GUI.DrawTextureWithTexCoords(new Rect(r.xMax-cap,r.y,cap,r.height),t,new Rect(.5f,0,.5f,1));
        }
        // A filled disc with a soft edge (CSS border-radius:50%).
        public static void Disc(Rect r,Color c)=>RadialStops(r,c,new[]{0f,.96f,1f},new[]{1f,1f,0f});
        // Filled circle segment from 12 o'clock clockwise (CSS conic-gradient fill), cached per 1/24 turn.
        public static void Pie(Rect r,Color c,float fraction)
        {
            if(c.a<=0||fraction<=0||Event.current.type!=EventType.Repaint)return;int k=Mathf.Clamp(Mathf.RoundToInt(fraction*24),1,24);ulong key=Mix(Mix(KeySeed,5),k);
            if(!stops.TryGetValue(key,out var t)){float f=k/24f;t=Make(48,48,(x,y)=>{float dx=x-23.5f,dy=y-23.5f,d=Mathf.Sqrt(dx*dx+dy*dy),a=Mathf.Repeat(Mathf.Atan2(dx,dy)/(2*Mathf.PI),1);return new Color(1,1,1,Mathf.Clamp01(23.5f-d)*(a<=f?1:0));});stops[key]=t;}
            var m=Enter();var o=GUI.color;GUI.color=Tone(c);GUI.DrawTexture(P(r),t);GUI.color=o;GUI.matrix=m;
        }
        // CSS .panel (visual revision): #17181df5, 1px #45464e, 10px radius, deep shadow; `enter` 0..1 runs panel-in.
        public static void Panel(Rect r,float enter=1,Color fill=default,Color border=default)
        {
            if(fill.a<=0)fill=Hex(0x17181d,.96f);if(border.a<=0)border=Hex(0x45464e);
            Glow(new Rect(r.x,r.y+30,r.width,r.height),new Color(0,0,0,.45f*enter),60);Round(r,A(fill,enter),10,A(border,enter));
        }
        public static float PanelIn(float age)=>NativeUiMotion.Bezier(.2f,.8f,.2f,1,NativeUiMotion.Clamp01(age/.26f));
        private static readonly Dictionary<int,float> hover=new Dictionary<int,float>();
        // Eased 0..1 hover amount for a region (CSS transition .15 s), keyed by caller id.
        public static float HoverAmount(int id,Rect r,bool enabled=true)
        {
            hover.TryGetValue(id,out float v);if(Event.current.type==EventType.Repaint){float target=enabled&&Hover(r)?1:0;v=Mathf.MoveTowards(v,target,Time.unscaledDeltaTime/.15f);hover[id]=v;}return v;
        }
        // CSS .btn / .btn.primary (visual revision): hover lifts 1 px and warms the border; returns true on click.
        public static bool Btn(Rect r,string label,bool primary=false,bool enabled=true,float alpha=1,float size=15)
        {
            int id=label.GetHashCode()^((int)r.x*73856093)^((int)r.y*19349663);float h=HoverAmount(id,r,enabled);var draw=new Rect(r.x,r.y-h,r.width,r.height);float a=alpha*(enabled?1:.45f);
            Color fill=primary?Mix(Hex(0xff6b35),Hex(0xff865b),h):Mix(Hex(0x222329),Hex(0x303138),h),border=primary?Hex(0xff6b35):Mix(Hex(0x484952),Hex(0xff784b),h);
            if(primary&&h>0)Glow(new Rect(draw.x,draw.y+8,draw.width,draw.height),A(Hex(0xff6b35),.35f*h*a),16);
            Round(draw,A(fill,a),4,A(border,a));Text(draw,label,size,A(primary?Hex(0x111216):Hex(0xf5f5f7),a),TextAnchor.MiddleCenter,bold:true);
            return Button(r,enabled)&&alpha>.5f;
        }
        private static GUIStyle Style(float size,bool bold,bool mono,TextAnchor align,bool wrap,bool italic=false)
        {
            var s=text;s.font=mono?Mono():null;s.fontSize=Mathf.Max(1,Mathf.RoundToInt(size*S));s.fontStyle=bold?(italic?FontStyle.BoldAndItalic:FontStyle.Bold):italic?FontStyle.Italic:FontStyle.Normal;s.alignment=align;s.wordWrap=wrap;return s;
        }
        public static Vector2 Measure(string str,float size,bool bold=false,bool mono=false,float tracking=0)
        {
            var s=Style(size,bold,mono,TextAnchor.UpperLeft,false);content.text=str??"";var v=s.CalcSize(content)/S;if(tracking!=0&&str!=null)v.x+=tracking*size*Math.Max(0,str.Length-1);return v;
        }
        // Keyed by value, so a lookup allocates nothing (menus re-wrap the same strings every event).
        private static readonly Dictionary<(string,float,float,bool,int,float),List<string>> wraps=new Dictionary<(string,float,float,bool,int,float),List<string>>();
        // Greedy word wrap at the current pixel size (cached), clamped to `maxLines` with an ellipsis like -webkit-line-clamp.
        public static List<string> Wrap(string str,float size,float width,bool bold=false,int maxLines=int.MaxValue)
        {
            if(maxLines<=0)return new List<string>();str=str??"";var key=(str,size,width,bold,maxLines,S);if(wraps.TryGetValue(key,out var cached))return cached;if(wraps.Count>4096)wraps.Clear();
            var s=Style(size,bold,false,TextAnchor.UpperLeft,false);float limit=width*S;var lines=new List<string>();var line="";
            float Width(string t){content.text=t;return s.CalcSize(content).x;}
            foreach(string word in str.Split(' '))
            {
                if(word.Length==0)continue;string next=line.Length==0?word:line+" "+word;if(Width(next)<=limit||line.Length==0){line=next;continue;}
                lines.Add(line);line=word;if(lines.Count>=maxLines)break;
            }
            if(lines.Count<maxLines&&line.Length>0)lines.Add(line);
            else if(lines.Count>=maxLines&&maxLines>0){string last=lines[maxLines-1];while(last.Length>1&&Width(last+"…")>limit)last=last.Substring(0,last.Length-1).TrimEnd();lines[maxLines-1]=last+"…";lines.RemoveRange(maxLines,lines.Count-maxLines);}
            wraps[key]=lines;return lines;
        }
        // Text in a virtual rect. `tracking` is CSS letter-spacing in em; `glow` draws a soft halo in that colour.
        public static void Text(Rect r,string str,float size,Color c,TextAnchor align=TextAnchor.MiddleCenter,bool bold=false,bool mono=false,float tracking=0,Color glow=default,bool wrap=false,Color shadow=default,bool italic=false)
        {
            if(string.IsNullOrEmpty(str)||c.a<=0)return;var m=Enter();var s=Style(size,bold,mono,align,wrap&&tracking==0,italic);var pr=P(r);var o=GUI.contentColor;
            if(tracking!=0){TrackedText(pr,str,s,size,c,tracking,glow,shadow);GUI.contentColor=o;GUI.matrix=m;return;}
            content.text=str;
            if(shadow.a>0){GUI.contentColor=Tone(shadow);GUI.Label(new Rect(pr.x+S,pr.y+S*1.5f,pr.width,pr.height),content,s);}
            if(glow.a>0){float g=Mathf.Max(1,size*S*.06f);GUI.contentColor=Tone(new Color(glow.r,glow.g,glow.b,glow.a*.35f));foreach(var d in Halo)GUI.Label(new Rect(pr.x+d.x*g,pr.y+d.y*g,pr.width,pr.height),content,s);}
            GUI.contentColor=Tone(c);GUI.Label(pr,content,s);GUI.contentColor=o;GUI.matrix=m;
        }
        private static readonly Vector2[] Halo={new Vector2(-1,0),new Vector2(1,0),new Vector2(0,-1),new Vector2(0,1),new Vector2(-.7f,-.7f),new Vector2(.7f,.7f),new Vector2(-.7f,.7f),new Vector2(.7f,-.7f)};
        // Tracked text draws a glyph at a time; the per-glyph strings and the width buffer are reused across frames.
        private static float[] trackWidths=new float[64];private static readonly Dictionary<char,string> glyphs=new Dictionary<char,string>();
        private static string Glyph(char c){if(!glyphs.TryGetValue(c,out var g)){g=c.ToString();glyphs[c]=g;}return g;}
        private static void TrackedText(Rect pr,string str,GUIStyle s,float size,Color c,float tracking,Color glow,Color shadow)
        {
            var align=s.alignment;s.alignment=TextAnchor.UpperLeft;s.wordWrap=false;float gap=tracking*size*S,total=0;if(trackWidths.Length<str.Length)trackWidths=new float[Math.Max(str.Length,trackWidths.Length*2)];var widths=trackWidths;
            for(int i=0;i<str.Length;i++){content.text=Glyph(str[i]);widths[i]=s.CalcSize(content).x;total+=widths[i]+(i>0?gap:0);}
            content.text=str;float h=s.CalcSize(content).y;
            float x=align==TextAnchor.UpperLeft||align==TextAnchor.MiddleLeft||align==TextAnchor.LowerLeft?pr.x:align==TextAnchor.UpperRight||align==TextAnchor.MiddleRight||align==TextAnchor.LowerRight?pr.xMax-total:pr.center.x-total/2;
            float y=align==TextAnchor.UpperLeft||align==TextAnchor.UpperCenter||align==TextAnchor.UpperRight?pr.y:align==TextAnchor.LowerLeft||align==TextAnchor.LowerCenter||align==TextAnchor.LowerRight?pr.yMax-h:pr.center.y-h/2;
            for(int i=0;i<str.Length;i++)
            {
                content.text=Glyph(str[i]);var q=new Rect(x,y,widths[i]+2,h);
                if(shadow.a>0){GUI.contentColor=Tone(shadow);GUI.Label(new Rect(q.x+S,q.y+S*1.5f,q.width,q.height),content,s);}
                if(glow.a>0){float g=Mathf.Max(1,size*S*.06f);GUI.contentColor=Tone(new Color(glow.r,glow.g,glow.b,glow.a*.35f));foreach(var d in Halo)GUI.Label(new Rect(q.x+d.x*g,q.y+d.y*g,q.width,q.height),content,s);}
                GUI.contentColor=Tone(c);GUI.Label(q,content,s);x+=widths[i]+gap;
            }
            s.alignment=align;
        }
        // A clickable region in virtual space (respects the current transform for hit testing through GUI.matrix).
        public static bool Button(Rect r,bool enabled=true){var m=Enter();bool e=GUI.enabled;GUI.enabled=enabled;bool hit=GUI.Button(P(r),GUIContent.none,GUIStyle.none);GUI.enabled=e;GUI.matrix=m;return hit&&enabled;}
        public static bool Hover(Rect r){var m=Enter();bool h=P(r).Contains(Event.current.mousePosition);GUI.matrix=m;return h;}
        public static Color Hex(int rgb,float a=1)=>new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f,a);
        public static Color Mix(Color a,Color b,float t)=>Color.Lerp(a,b,t);
        public static Color A(Color c,float a)=>new Color(c.r,c.g,c.b,c.a*a);
        // The virtual-canvas matrix legacy GUI code draws under.
        public static Matrix4x4 Canvas=>Matrix4x4.Scale(Vector3.one*S);
    }
}
