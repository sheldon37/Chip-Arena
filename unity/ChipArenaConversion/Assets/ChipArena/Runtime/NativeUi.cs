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
        // lands where the sRGB one does: a light layer takes LightAlpha (exact for white over the UI's dark base, #0f0f0f, and
        // close over the cards' substrates) and draws its ramp textures (fades, glows, radial gradients, polygons) with it too;
        // a dark layer takes 1-(1-a)^2.2 (exact for black over anything); mixed by the colour's lightness. Opaque draws,
        // solid colours and Gamma builds are unchanged.
        // Calibration (2026-10-09): the first remap gave light layers a^2.2, exact only over pure black and far too faint for
        // the 2-20% layers (a 2.4% brushed line or a 10% trace came out at a tenth of their CSS contrast). The owner's Windows
        // screenshot of the card measures sRGB blending with the plain alpha in the player itself (a footer rule of 8% white
        // over (21,15,20) reads (39,34,38): sRGB blend 40,34,39; linear blend with a^2.2 would read 28): GL.sRGBWrite is off
        // while the IMGUI pass draws, so LinearBlend is false there and Tone leaves every layer as authored. Tone stays for
        // any target that does blend in linear light.
        public static bool LinearBlend;
        public static Color Tone(Color c)
        {
            if(!LinearBlend||c.a<=0||c.a>=1)return c;float w=Lightness(c),a=c.a;
            c.a=Mathf.Lerp(1-Mathf.Pow(1-a,2.2f),LightAlpha(a),w);return c;
        }
        // The linear-light alpha that puts white over the UI's dark base (sRGB .06) where the sRGB blend of `a` puts it.
        private const float ToneBase=.06f;private static readonly float ToneBaseLinear=SrgbToLinear(ToneBase);
        private static float SrgbToLinear(float v)=>v<=.04045f?v/12.92f:Mathf.Pow((v+.055f)/1.055f,2.4f);
        public static float LightAlpha(float a)=>a<=0?0:a>=1?1:(SrgbToLinear(ToneBase+(1-ToneBase)*a)-ToneBaseLinear)/(1-ToneBaseLinear);
        private static float Lightness(Color c){float v=Mathf.Max(c.r,Mathf.Max(c.g,c.b));return Mathf.SmoothStep(0,1,Mathf.Clamp01((v-.08f)/.25f));}
        // Light layers in Linear blending use the ramp textures re-shaped by LightAlpha (the radial and polygon bakes take it when
        // their `power` flag is above 1).
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
            // The same ramps through LightAlpha, for light layers under Linear blending (see Tone).
            verticalL=Make(1,64,(x,y)=>new Color(1,1,1,LightAlpha(y/63f)));horizontalL=Make(64,1,(x,y)=>new Color(1,1,1,LightAlpha(x/63f)));
            radialL=Make(64,64,(x,y)=>{float dx=(x-31.5f)/31.5f,dy=(y-31.5f)/31.5f,d=Mathf.Clamp01(Mathf.Sqrt(dx*dx+dy*dy));return new Color(1,1,1,LightAlpha(1-d));});
            softL=Make(64,64,(x,y)=>{float dx=Mathf.Clamp01((Mathf.Abs(x-31.5f)-14)/18f),dy=Mathf.Clamp01((Mathf.Abs(y-31.5f)-14)/18f),d=Mathf.Clamp01(Mathf.Sqrt(dx*dx+dy*dy));return new Color(1,1,1,LightAlpha((1-d)*(1-d)));});
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
        /// <summary>Draws what follows through an affine map of the virtual canvas, point (x, y) to origin + axisU x + axisV y (virtual
        /// units; IMGUI maps the pointer back through it too): a HUD drawn onto a projected world screen. Pop ends it.</summary>
        public static void PushAffine(Vector2 origin,Vector2 axisU,Vector2 axisV)
        {
            stack.Push(current);var m=Matrix4x4.identity;m.m00=axisU.x;m.m10=axisU.y;m.m01=axisV.x;m.m11=axisV.y;m.m03=origin.x*S;m.m13=origin.y*S;current=current*m;
        }
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
        private static Texture2D RadialTexture(float[] at,float[] alpha,float power)=>Make(96,96,(x,y)=>{float dx=(x-47.5f)/47.5f,dy=(y-47.5f)/47.5f;float v=NativeUiMotion.Keys(Mathf.Sqrt(dx*dx+dy*dy),at,alpha);return new Color(1,1,1,power>1?LightAlpha(v):v);});
        private static Texture2D RampTexture(Color a,Color b,bool horizontal)=>horizontal?Make(64,1,(x,y)=>Tone(Color.Lerp(a,b,x/63f))):Make(1,64,(x,y)=>Tone(Color.Lerp(b,a,y/63f)));
        public static void RadialStops(Rect r,Color c,float[] at,float[] alpha)
        {
            if(c.a<=0||Event.current.type!=EventType.Repaint)return;float power=LightRamp(c)?2.2f:1;ulong key=Mix(KeySeed,power>1?6:1);foreach(float v in at)key=Mix(key,v);key=Mix(key,float.NaN);foreach(float v in alpha)key=Mix(key,v);
            if(!stops.TryGetValue(key,out var t)){t=RadialTexture(at,alpha,power);stops[key]=t;}
            var m=Enter();var o=GUI.color;GUI.color=Tone(c);GUI.DrawTexture(P(r),t);GUI.color=o;GUI.matrix=m;
        }
        public static void Radial(Rect r,Color c){if(c.a<=0||Event.current.type!=EventType.Repaint)return;var m=Enter();var o=GUI.color;GUI.color=Tone(c);GUI.DrawTexture(P(r),LightRamp(c)?radialL:radial);GUI.color=o;GUI.matrix=m;}
        public static void Image(Rect r,Texture t,Color c){if(t==null||c.a<=0||Event.current.type!=EventType.Repaint)return;var m=Enter();var o=GUI.color;GUI.color=Tone(c);GUI.DrawTexture(P(r),t);GUI.color=o;GUI.matrix=m;}
        // Part `uv` of texture `t` (texture coordinates, v up; a negative width mirrors it) stretched over `r`: nine-slices.
        public static void Slice(Rect r,Texture t,Rect uv,Color c){if(t==null||c.a<=0||Event.current.type!=EventType.Repaint)return;var m=Enter();var o=GUI.color;GUI.color=Tone(c);GUI.DrawTextureWithTexCoords(P(r),t,uv);GUI.color=o;GUI.matrix=m;}
        // A CSS repeating pattern: texture `t` (wrap mode Repeat) repeated over `r` every `period` virtual px from its top-left
        // corner shifted by `phase`, in one draw; `flipY` mirrors it vertically (scaleY(-1)).
        public static void Tile(Rect r,Texture t,Color c,Vector2 period,Vector2 phase=default,bool flipY=false)
        {
            if(t==null||c.a<=0||r.width<=0||r.height<=0||period.x<=0||period.y<=0||Event.current.type!=EventType.Repaint)return;var m=Enter();var o=GUI.color;GUI.color=Tone(c);
            float uw=r.width/period.x,vh=r.height/period.y,u=phase.x/period.x,v=phase.y/period.y;
            GUI.DrawTextureWithTexCoords(P(r),t,flipY?new Rect(u,1-v,uw,-vh):new Rect(u,1-v-vh,uw,vh));GUI.color=o;GUI.matrix=m;
        }
        public static void Glow(Rect r,Color c,float spread){if(c.a<=0||Event.current.type!=EventType.Repaint)return;var m=Enter();var o=GUI.color;GUI.color=Tone(c);GUI.DrawTexture(P(new Rect(r.x-spread,r.y-spread,r.width+spread*2,r.height+spread*2)),LightRamp(c)?softL:soft);GUI.color=o;GUI.matrix=m;}
        // CSS backdrop-filter: blur(px) under a translucent panel. NativeUiBlur keeps one shared blurred copy of the 3D frame
        // (a small and a large blur) while a panel asked for it in the last frames; this draws its texels under the panel's
        // rounded shape (corner rows stepped inside the radius), before the panel's own fill. The copy is the same scene the
        // panel covers, so its alpha is the panel's opacity as is (Tone is for colour layers). Without the copy (its first
        // frame, a missing shader, Gamma tests) nothing is drawn and the panel shows its plain translucent fill.
        public static Texture BlurSmall,BlurLarge;public static int BlurRequested=-100;
        public static void Backdrop(Rect r,float radius,float blur,float alpha)
        {
            if(alpha<=0||blur<=0||Event.current.type!=EventType.Repaint)return;BlurRequested=Time.frameCount;var t=blur>8?BlurLarge:BlurSmall;if(t==null)return;
            var m=Enter();var o=GUI.color;GUI.color=new Color(1,1,1,Mathf.Clamp01(alpha));var pr=P(r);float rad=Mathf.Min(radius*S,Mathf.Min(pr.width,pr.height)/2);
            BackdropSlice(new Rect(pr.x,pr.y+rad,pr.width,pr.height-2*rad),t);
            for(int i=0;i<4&&rad>0;i++)
            {   // Row i of each corner band: inset by the circle at its outer edge, so it stays inside the rounded shape.
                float d0=rad*i/4,h=rad/4,dy=rad-d0,inset=rad-Mathf.Sqrt(Mathf.Max(0,rad*rad-dy*dy));
                BackdropSlice(new Rect(pr.x+inset,pr.y+d0,pr.width-2*inset,h),t);BackdropSlice(new Rect(pr.x+inset,pr.yMax-d0-h,pr.width-2*inset,h),t);
            }
            GUI.color=o;GUI.matrix=m;
        }
        // A physical rect drawn with the texels of the screen pixels it covers: the kit transform, plus a deferred clip's offset
        // (IMGUI adds it before the matrix), gives its screen rect; the copy's v runs bottom-up.
        private static void BackdropSlice(Rect q,Texture t)
        {
            if(q.width<=0||q.height<=0)return;Vector3 a=current.MultiplyPoint3x4(q.min+pivotShift),b=current.MultiplyPoint3x4(q.max+pivotShift);float sw=Mathf.Max(1,Screen.width),sh=Mathf.Max(1,Screen.height);
            GUI.DrawTextureWithTexCoords(q,t,new Rect(Mathf.Min(a.x,b.x)/sw,1-Mathf.Max(a.y,b.y)/sh,Mathf.Abs(b.x-a.x)/sw,Mathf.Abs(b.y-a.y)/sh));
        }
        // CSS clip-path: polygon() filled with a horizontal alpha profile: `shape` in fractions of the box (x right, y down),
        // the profile's stops `at` (0..1 across) and `alpha`. The anti-aliased mask is baked once per shape and profile
        // (cached like RadialStops) and drawn stretched over `r` in `c`; light colours use the alpha^2.2 bake (see Tone).
        public static void Polygon(Rect r,Vector2[] shape,float[] at,float[] alpha,Color c)
        {
            if(c.a<=0||Event.current.type!=EventType.Repaint||shape==null||shape.Length<3)return;bool light=LightRamp(c);ulong key=Mix(KeySeed,light?8:7);
            foreach(var v in shape)key=Mix(Mix(key,v.x),v.y);key=Mix(key,float.NaN);foreach(float v in at)key=Mix(key,v);key=Mix(key,float.NaN);foreach(float v in alpha)key=Mix(key,v);
            if(!stops.TryGetValue(key,out var t)){t=PolygonTexture(shape,at,alpha,light?2.2f:1);stops[key]=t;}
            var m=Enter();var o=GUI.color;GUI.color=Tone(c);GUI.DrawTexture(P(r),t);GUI.color=o;GUI.matrix=m;
        }
        // Scanline coverage at four sub-rows per texel row (even-odd), exact horizontally; texture row 0 is the bottom.
        private static Texture2D PolygonTexture(Vector2[] shape,float[] at,float[] alpha,float power)
        {
            const int w=192,h=24,sub=4;var px=new Color[w*h];var row=new float[w];var xs=new List<float>(shape.Length);
            for(int y=0;y<h;y++)
            {
                Array.Clear(row,0,w);
                for(int k=0;k<sub;k++)
                {
                    float sy=(y+(k+.5f)/sub)/h;xs.Clear();
                    for(int i=0;i<shape.Length;i++){var a=shape[i];var b=shape[(i+1)%shape.Length];if((a.y<=sy)!=(b.y<=sy))xs.Add((a.x+(sy-a.y)/(b.y-a.y)*(b.x-a.x))*w);}
                    xs.Sort();
                    for(int n=0;n+1<xs.Count;n+=2)for(int x=Mathf.Max(0,(int)xs[n]);x<Mathf.Min(w,Mathf.CeilToInt(xs[n+1]));x++)row[x]+=Mathf.Max(0,Mathf.Min(x+1,xs[n+1])-Mathf.Max(x,xs[n]))/sub;
                }
                for(int x=0;x<w;x++){float v=Mathf.Clamp01(row[x]*NativeUiMotion.Keys((x+.5f)/w,at,alpha));px[(h-1-y)*w+x]=new Color(1,1,1,power>1?LightAlpha(v):v);}
            }
            var t=new Texture2D(w,h,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};t.SetPixels(px);t.Apply(false,true);return t;
        }
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
        // CSS .panel in the cyber palette: navy glass (NativeCyberTheme.Panel), a 1px edge, 10px radius, deep shadow,
        // backdrop-filter: blur(16px); `enter` 0..1 runs panel-in.
        public static void Panel(Rect r,float enter=1,Color fill=default,Color border=default)
        {
            if(fill.a<=0)fill=NativeCyberTheme.Panel(.95f);if(border.a<=0)border=NativeCyberTheme.Edge();
            Glow(new Rect(r.x,r.y+30,r.width,r.height),new Color(0,0,0,.45f*enter),60);Backdrop(r,10,16,enter);Round(r,A(fill,enter),10,A(border,enter));
        }
        public static float PanelIn(float age)=>NativeUiMotion.Bezier(.2f,.8f,.2f,1,NativeUiMotion.Clamp01(age/.26f));
        private static readonly Dictionary<int,float> hover=new Dictionary<int,float>();
        // Eased 0..1 hover amount for a region (CSS transition .15 s), keyed by caller id.
        public static float HoverAmount(int id,Rect r,bool enabled=true)
        {
            hover.TryGetValue(id,out float v);if(Event.current.type==EventType.Repaint){float target=enabled&&Hover(r)?1:0;v=Mathf.MoveTowards(v,target,Time.unscaledDeltaTime/.15f);hover[id]=v;}return v;
        }
        // CSS .btn / .btn.primary in the cyber palette (NativeCyberTheme): hover lifts 1 px and lights the border cyan; returns
        // true on click.
        public static bool Btn(Rect r,string label,bool primary=false,bool enabled=true,float alpha=1,float size=15)
        {
            int id=label.GetHashCode()^((int)r.x*73856093)^((int)r.y*19349663);float h=HoverAmount(id,r,enabled);var draw=new Rect(r.x,r.y-h,r.width,r.height);float a=alpha*(enabled?1:.45f);
            Color fill=primary?Mix(NativeCyberTheme.Cyan(),NativeCyberTheme.CyanSoft(),h):Mix(NativeCyberTheme.Indigo(.92f),Hex(0x1b2552,.95f),h),border=primary?NativeCyberTheme.CyanSoft():Mix(NativeCyberTheme.Edge(),NativeCyberTheme.Cyan(),h);
            if(primary&&h>0)Glow(new Rect(draw.x,draw.y+8,draw.width,draw.height),NativeCyberTheme.Cyan(.35f*h*a),16);
            Round(draw,A(fill,a),4,A(border,a));Text(draw,label,size,A(primary?NativeCyberTheme.Void():NativeCyberTheme.Ink(),a),TextAnchor.MiddleCenter,bold:true);
            return Pressed(r,enabled,alpha>.5f?NativeUiSounds.Button(label,primary):null)&&alpha>.5f;
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
        // Text in a virtual rect. `tracking` is CSS letter-spacing in em; `glow` draws a soft halo in that colour; `blur` is
        // CSS filter: blur(px) (entrance animations): the glyphs spread over a ring of dim copies that closes as it eases to 0.
        public static void Text(Rect r,string str,float size,Color c,TextAnchor align=TextAnchor.MiddleCenter,bool bold=false,bool mono=false,float tracking=0,Color glow=default,bool wrap=false,Color shadow=default,bool italic=false,float blur=0)
        {
            if(string.IsNullOrEmpty(str)||c.a<=0)return;var m=Enter();var s=Style(size,bold,mono,align,wrap&&tracking==0,italic);var pr=P(r);var o=GUI.contentColor;
            if(tracking!=0){TrackedText(pr,str,s,size,c,tracking,glow,shadow,blur);GUI.contentColor=o;GUI.matrix=m;return;}
            content.text=str;
            if(shadow.a>0){GUI.contentColor=Tone(shadow);GUI.Label(new Rect(pr.x+S,pr.y+S*1.5f,pr.width,pr.height),content,s);}
            if(glow.a>0){float g=Mathf.Max(1,size*S*.06f);GUI.contentColor=Tone(new Color(glow.r,glow.g,glow.b,glow.a*.35f));foreach(var d in Halo)GUI.Label(new Rect(pr.x+d.x*g,pr.y+d.y*g,pr.width,pr.height),content,s);}
            Blurred(pr,s,size,c,blur);GUI.contentColor=o;GUI.matrix=m;
        }
        // The text itself; with a blur, two rings of eight copies (at .45 and 1x the blur radius) around a dimmer core, the
        // outer ring faintest. The share leaving the core grows with the radius against the stroke width (~12% of the size),
        // as a Gaussian thins a stroke it outgrows.
        private static void Blurred(Rect q,GUIStyle s,float size,Color c,float blur)
        {
            float g=blur*S;if(g<.35f){GUI.contentColor=Tone(c);GUI.Label(q,content,s);return;}
            float f=Mathf.Clamp01(g/Mathf.Max(1,size*S*.12f));
            GUI.contentColor=Tone(new Color(c.r,c.g,c.b,c.a*Mathf.Lerp(.35f,.08f,f)));foreach(var d in Halo)GUI.Label(new Rect(q.x+d.x*g,q.y+d.y*g,q.width,q.height),content,s);
            GUI.contentColor=Tone(new Color(c.r,c.g,c.b,c.a*Mathf.Lerp(.55f,.14f,f)));foreach(var d in Halo)GUI.Label(new Rect(q.x+d.x*g*.45f,q.y+d.y*g*.45f,q.width,q.height),content,s);
            GUI.contentColor=Tone(new Color(c.r,c.g,c.b,c.a*Mathf.Lerp(.75f,.2f,f)));GUI.Label(q,content,s);
        }
        // CSS clip-path: inset() bands: kit drawing between BeginBand and EndBand is clipped to `r` (virtual units, under the
        // current transform). The clip's scroll offset cancels its origin, so callers keep their absolute rects; IMGUI maps a
        // clip with the matrix current at draw time, which the kit sets for each draw.
        public static void BeginBand(Rect r){var m=Enter();var p=P(r);GUI.BeginClip(p,-p.position,Vector2.zero,false);GUI.matrix=m;}
        public static void EndBand(){var m=Enter();GUI.EndClip();GUI.matrix=m;}
        // The Siren title glitch (chip-cards.css siren-split-a, or -b for the cyan copy; steps(1,end)): from 65% to 72% of its
        // cycle `k`, three steps each showing one horizontal band of the title's box (clip-path: inset, top/bottom fractions)
        // shifted by dx px at .85/.85/.7 opacity. False outside the window.
        public static bool SirenSplit(float k,bool cyan,out float dx,out float top,out float bottom,out float opacity)
        {
            dx=0;top=0;bottom=1;opacity=0;if(!(k>=.65f&&k<.72f))return false;int step=k<.67f?0:k<.69f?1:2;opacity=step==2?.7f:.85f;
            if(!cyan){dx=step==0?-3:step==1?4:-2;top=step==0?0:step==1?.4f:.7f;bottom=step==0?.45f:step==1?.8f:1;}
            else{dx=step==0?3:step==1?-4:2;top=step==0?.45f:step==1?0:.2f;bottom=step==0?1:step==1?.4f:.55f;}
            return true;
        }
        // One glitch copy: `line` is the text's rect, `block` the title's box the band is cut from (several lines for a wrapped
        // name). Repaint only (labels draw nothing in other events).
        public static void SplitCopy(Rect block,Rect line,float k,bool cyan,string str,float size,float alpha,TextAnchor align,float tracking=0)
        {
            if(Event.current.type!=EventType.Repaint||!SirenSplit(k,cyan,out float dx,out float top,out float bottom,out float op))return;
            BeginBand(new Rect(block.x-block.width,block.y+block.height*top,block.width*3,block.height*(bottom-top)));
            Text(new Rect(line.x+dx,line.y,line.width,line.height),str,size,Hex(cyan?0x2de2ff:0xff2d55,op*alpha),align,bold:true,tracking:tracking);EndBand();
        }
        private static readonly Vector2[] Halo={new Vector2(-1,0),new Vector2(1,0),new Vector2(0,-1),new Vector2(0,1),new Vector2(-.7f,-.7f),new Vector2(.7f,.7f),new Vector2(-.7f,.7f),new Vector2(.7f,-.7f)};
        // Tracked text draws a glyph at a time; the per-glyph strings and the width buffer are reused across frames.
        private static float[] trackWidths=new float[64];private static readonly Dictionary<char,string> glyphs=new Dictionary<char,string>();
        private static string Glyph(char c){if(!glyphs.TryGetValue(c,out var g)){g=c.ToString();glyphs[c]=g;}return g;}
        private static void TrackedText(Rect pr,string str,GUIStyle s,float size,Color c,float tracking,Color glow,Color shadow,float blur)
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
                Blurred(q,s,size,c,blur);x+=widths[i]+gap;
            }
            s.alignment=align;
        }
        // A clickable region in virtual space (respects the current transform for hit testing through GUI.matrix).
        public static bool Button(Rect r,bool enabled=true)=>Pressed(r,enabled,NativeUiSounds.Click);
        // Regions with their own focus/pick cues must not also emit a generic menu tick as they animate or become clickable.
        public static bool SilentButton(Rect r,bool enabled=true)=>Pressed(r,enabled,null);
        /// <summary>The one UI sound hook (NativeAudio listens; built-in hover, effects bank for the other cues): a press
        /// (click, back or confirm by the button), a press on a disabled button (error) and the pointer entering a button (hover).</summary>
        public static event Action<string> Sound;
        private static int hoverFrame=-10;private static bool hoverNow,hoverBefore;private static Vector2 hoverAt,hoverWas;
        private static bool Pressed(Rect r,bool enabled,string sound)
        {
            var m=Enter();bool e=GUI.enabled;GUI.enabled=enabled;var p=P(r);var ev=Event.current;
            if(sound!=null&&ev!=null)
            {
                bool over=p.Contains(ev.mousePosition);
                if(!enabled&&over&&ev.type==EventType.MouseDown&&ev.button==0)Sound?.Invoke(NativeUiSounds.Error);
                if(enabled&&over&&ev.type==EventType.Repaint)Hovered(p);
            }
            bool hit=GUI.Button(p,GUIContent.none,GUIStyle.none)||NativeControllerPointer.Click(p);GUI.enabled=e;GUI.matrix=m;
            if(hit&&enabled&&sound!=null)Sound?.Invoke(sound);
            return hit&&enabled;
        }
        // Hover edges: the first button under the pointer each frame; a sound when none was under it last frame or it moved to
        // another button (a button's own hover lift moves it by far less than half its size).
        private static void Hovered(Rect p)
        {
            int f=Time.frameCount;if(f!=hoverFrame){hoverBefore=hoverNow&&hoverFrame==f-1;hoverWas=hoverAt;hoverNow=false;hoverFrame=f;}
            if(hoverNow)return;hoverNow=true;hoverAt=p.center;
            if(!hoverBefore||(p.center-hoverWas).magnitude>Mathf.Max(8,Mathf.Min(p.width,p.height)*.5f))Sound?.Invoke(NativeUiSounds.Hover);
        }
        public static bool Hover(Rect r){var m=Enter();bool h=P(r).Contains(Event.current.mousePosition);GUI.matrix=m;return h;}
        public static Color Hex(int rgb,float a=1)=>new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f,a);
        public static Color Mix(Color a,Color b,float t)=>Color.Lerp(a,b,t);
        public static Color A(Color c,float a)=>new Color(c.r,c.g,c.b,c.a*a);
        // The virtual-canvas matrix legacy GUI code draws under.
        public static Matrix4x4 Canvas=>Matrix4x4.Scale(Vector3.one*S);
    }
}
