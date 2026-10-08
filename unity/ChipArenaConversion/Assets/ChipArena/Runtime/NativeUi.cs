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
        private static Texture2D white,vertical,horizontal,radial,soft;private static readonly Dictionary<string,Texture2D> stops=new Dictionary<string,Texture2D>();private static Texture2D[] rounds=new Texture2D[4];
        private static GUIStyle text;private static Font mono;private static bool monoTried;
        private static readonly Stack<Matrix4x4> stack=new Stack<Matrix4x4>();private static Matrix4x4 current=Matrix4x4.identity;
        private static readonly GUIContent content=new GUIContent();
        public static NativeUiMotion.Layout Begin(float uiScale=1)
        {
            var l=NativeUiMotion.Measure(Screen.width,Screen.height,uiScale);S=l.Scale;W=l.Width;H=l.Height;SafeX=l.SafeX;SafeW=l.SafeWidth;
            stack.Clear();current=Matrix4x4.identity;Ensure();return l;
        }
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
            for(int i=0;i<rounds.Length;i++){int r=4+i*4,n=r*2+2;rounds[i]=Make(n,n,(x,y)=>{float cx=Mathf.Clamp(x+.5f,r,n-r),cy=Mathf.Clamp(y+.5f,r,n-r),d=Mathf.Sqrt((x+.5f-cx)*(x+.5f-cx)+(y+.5f-cy)*(y+.5f-cy));return new Color(1,1,1,Mathf.Clamp01(r-d+.5f));});}
            text=new GUIStyle(GUI.skin.label){wordWrap=true,richText=false,clipping=TextClipping.Overflow,padding=new RectOffset(0,0,0,0),margin=new RectOffset(0,0,0,0)};
        }
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
        // Push a transform about a virtual-space pivot: translate (virtual units), uniform scale and rotation (degrees).
        public static void Push(Vector2 pivot,Vector2 translate,float scale=1,float degrees=0,float scaleY=float.NaN)
        {
            stack.Push(current);Vector3 p=pivot*S;float sy=float.IsNaN(scaleY)?scale:scaleY;
            current=current*Matrix4x4.TRS(p+(Vector3)(translate*S),Quaternion.Euler(0,0,degrees),new Vector3(scale,sy,1))*Matrix4x4.Translate(-p);
        }
        public static void Pop(){current=stack.Count>0?stack.Pop():Matrix4x4.identity;}
        public static void Fill(Rect r,Color c){if(c.a<=0||Event.current.type!=EventType.Repaint)return;var m=Enter();var o=GUI.color;GUI.color=c;GUI.DrawTexture(P(r),white);GUI.color=o;GUI.matrix=m;}
        // Vertical gradient: `bottom` underneath, `top` laid over it with an alpha ramp that is full at the top edge.
        public static void Gradient(Rect r,Color top,Color bottom)
        {
            if(Event.current.type!=EventType.Repaint)return;var m=Enter();var o=GUI.color;var pr=P(r);
            if(bottom.a>0){GUI.color=bottom;GUI.DrawTexture(pr,white);}if(top.a>0){GUI.color=top;GUI.DrawTexture(pr,vertical);}
            GUI.color=o;GUI.matrix=m;
        }
        // Fade from `c` at the top to transparent at the bottom (or the reverse).
        public static void Fade(Rect r,Color c,bool fromTop=true)
        {
            if(c.a<=0||Event.current.type!=EventType.Repaint)return;var m=Enter();var o=GUI.color;GUI.color=c;var pr=P(r);
            if(fromTop)GUI.DrawTexture(pr,vertical);else GUI.DrawTextureWithTexCoords(pr,vertical,new Rect(0,1,1,-1));GUI.color=o;GUI.matrix=m;
        }
        // Horizontal gradient from `left` to `right`.
        public static void HGradient(Rect r,Color left,Color right)
        {
            if(Event.current.type!=EventType.Repaint)return;var m=Enter();var o=GUI.color;var pr=P(r);
            if(left.a>0){GUI.color=left;GUI.DrawTextureWithTexCoords(pr,horizontal,new Rect(1,0,-1,1));}if(right.a>0){GUI.color=right;GUI.DrawTexture(pr,horizontal);}
            GUI.color=o;GUI.matrix=m;
        }
        // CSS radial-gradient(ellipse ...) filling `r` (the ellipse's bounding box): alpha stops at normalised radii.
        public static void RadialStops(Rect r,Color c,float[] at,float[] alpha)
        {
            if(c.a<=0||Event.current.type!=EventType.Repaint)return;string key=string.Join(",",at)+"|"+string.Join(",",alpha);
            if(!stops.TryGetValue(key,out var t)){t=Make(96,96,(x,y)=>{float dx=(x-47.5f)/47.5f,dy=(y-47.5f)/47.5f;return new Color(1,1,1,NativeUiMotion.Keys(Mathf.Sqrt(dx*dx+dy*dy),at,alpha));});stops[key]=t;}
            var m=Enter();var o=GUI.color;GUI.color=c;GUI.DrawTexture(P(r),t);GUI.color=o;GUI.matrix=m;
        }
        public static void Radial(Rect r,Color c){if(c.a<=0||Event.current.type!=EventType.Repaint)return;var m=Enter();var o=GUI.color;GUI.color=c;GUI.DrawTexture(P(r),radial);GUI.color=o;GUI.matrix=m;}
        public static void Glow(Rect r,Color c,float spread){if(c.a<=0||Event.current.type!=EventType.Repaint)return;var m=Enter();var o=GUI.color;GUI.color=c;GUI.DrawTexture(P(new Rect(r.x-spread,r.y-spread,r.width+spread*2,r.height+spread*2)),soft);GUI.color=o;GUI.matrix=m;}
        // Rounded rectangle (radius 4/8/12/16 px class) with an optional border.
        public static void Round(Rect r,Color fill,float radius=8,Color border=default,float borderWidth=1)
        {
            if(Event.current.type!=EventType.Repaint)return;var m=Enter();int i=Mathf.Clamp(Mathf.RoundToInt(radius*S/4)-1,0,rounds.Length-1);var t=rounds[i];int rr=4+i*4;var o=GUI.color;var pr=P(r);
            if(border.a>0){Nine(pr,t,rr,border);float b=Mathf.Max(1,borderWidth*S);pr=new Rect(pr.x+b,pr.y+b,pr.width-2*b,pr.height-2*b);}
            if(fill.a>0)Nine(pr,t,rr,fill);GUI.color=o;GUI.matrix=m;
        }
        private static void Nine(Rect r,Texture2D t,int radius,Color c)
        {
            float k=Mathf.Min(radius,Mathf.Min(r.width,r.height)/2);float u=radius/(float)t.width;GUI.color=c;
            float[] xs={r.x,r.x+k,r.xMax-k,r.xMax},ys={r.y,r.y+k,r.yMax-k,r.yMax},us={0,u,1-u,1},vs={1,1-u,u,0};
            for(int i=0;i<3;i++)for(int j=0;j<3;j++){var q=new Rect(xs[i],ys[j],xs[i+1]-xs[i],ys[j+1]-ys[j]);if(q.width<=0||q.height<=0)continue;GUI.DrawTextureWithTexCoords(q,t,new Rect(us[i],vs[j+1],us[i+1]-us[i],vs[j]-vs[j+1]));}
        }
        public static void Pill(Rect r,Color fill,Color border){Round(r,fill,Mathf.Min(16,r.height/2),border);}
        // Filled circle segment from 12 o'clock clockwise (CSS conic-gradient fill), cached per 1/24 turn.
        public static void Pie(Rect r,Color c,float fraction)
        {
            if(c.a<=0||fraction<=0||Event.current.type!=EventType.Repaint)return;int k=Mathf.Clamp(Mathf.RoundToInt(fraction*24),1,24);string key="pie"+k;
            if(!stops.TryGetValue(key,out var t)){float f=k/24f;t=Make(48,48,(x,y)=>{float dx=x-23.5f,dy=y-23.5f,d=Mathf.Sqrt(dx*dx+dy*dy),a=Mathf.Repeat(Mathf.Atan2(dx,dy)/(2*Mathf.PI),1);return new Color(1,1,1,Mathf.Clamp01(23.5f-d)*(a<=f?1:0));});stops[key]=t;}
            var m=Enter();var o=GUI.color;GUI.color=c;GUI.DrawTexture(P(r),t);GUI.color=o;GUI.matrix=m;
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
        // Text in a virtual rect. `tracking` is CSS letter-spacing in em; `glow` draws a soft halo in that colour.
        public static void Text(Rect r,string str,float size,Color c,TextAnchor align=TextAnchor.MiddleCenter,bool bold=false,bool mono=false,float tracking=0,Color glow=default,bool wrap=false,Color shadow=default,bool italic=false)
        {
            if(string.IsNullOrEmpty(str)||c.a<=0)return;var m=Enter();var s=Style(size,bold,mono,align,wrap&&tracking==0,italic);var pr=P(r);var o=GUI.contentColor;
            if(tracking!=0){TrackedText(pr,str,s,size,c,tracking,glow,shadow);GUI.contentColor=o;GUI.matrix=m;return;}
            content.text=str;
            if(shadow.a>0){GUI.contentColor=shadow;GUI.Label(new Rect(pr.x+S,pr.y+S*1.5f,pr.width,pr.height),content,s);}
            if(glow.a>0){float g=Mathf.Max(1,size*S*.06f);GUI.contentColor=new Color(glow.r,glow.g,glow.b,glow.a*.35f);foreach(var d in Halo)GUI.Label(new Rect(pr.x+d.x*g,pr.y+d.y*g,pr.width,pr.height),content,s);}
            GUI.contentColor=c;GUI.Label(pr,content,s);GUI.contentColor=o;GUI.matrix=m;
        }
        private static readonly Vector2[] Halo={new Vector2(-1,0),new Vector2(1,0),new Vector2(0,-1),new Vector2(0,1),new Vector2(-.7f,-.7f),new Vector2(.7f,.7f),new Vector2(-.7f,.7f),new Vector2(.7f,-.7f)};
        private static void TrackedText(Rect pr,string str,GUIStyle s,float size,Color c,float tracking,Color glow,Color shadow)
        {
            var align=s.alignment;s.alignment=TextAnchor.UpperLeft;s.wordWrap=false;float gap=tracking*size*S,total=0;var widths=new float[str.Length];
            for(int i=0;i<str.Length;i++){content.text=str[i].ToString();widths[i]=s.CalcSize(content).x;total+=widths[i]+(i>0?gap:0);}
            content.text=str;float h=s.CalcSize(content).y;
            float x=align==TextAnchor.UpperLeft||align==TextAnchor.MiddleLeft||align==TextAnchor.LowerLeft?pr.x:align==TextAnchor.UpperRight||align==TextAnchor.MiddleRight||align==TextAnchor.LowerRight?pr.xMax-total:pr.center.x-total/2;
            float y=align==TextAnchor.UpperLeft||align==TextAnchor.UpperCenter||align==TextAnchor.UpperRight?pr.y:align==TextAnchor.LowerLeft||align==TextAnchor.LowerCenter||align==TextAnchor.LowerRight?pr.yMax-h:pr.center.y-h/2;
            for(int i=0;i<str.Length;i++)
            {
                content.text=str[i].ToString();var q=new Rect(x,y,widths[i]+2,h);
                if(shadow.a>0){GUI.contentColor=shadow;GUI.Label(new Rect(q.x+S,q.y+S*1.5f,q.width,q.height),content,s);}
                if(glow.a>0){float g=Mathf.Max(1,size*S*.06f);GUI.contentColor=new Color(glow.r,glow.g,glow.b,glow.a*.35f);foreach(var d in Halo)GUI.Label(new Rect(q.x+d.x*g,q.y+d.y*g,q.width,q.height),content,s);}
                GUI.contentColor=c;GUI.Label(q,content,s);x+=widths[i]+gap;
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
