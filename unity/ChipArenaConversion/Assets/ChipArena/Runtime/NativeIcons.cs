using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The source's line icons (client/icons.ts: Lucide 24x24 SVG, 1.7-unit round strokes) rasterised at the exact pixel
    // size they are drawn, so they stay crisp at every UI scale. Supports the SVG subset those icons use: path (M L H V C S
    // Q T A Z, absolute and relative, minified arc flags), circle, ellipse, rect (rx/ry), line, polyline and polygon,
    // stroked and/or filled, plus solid and vertical linear-gradient fills for the source's few coloured drawings (the
    // Boss Test skull). Icon textures are white and tinted when drawn; built lazily, never in field initializers.
    public static class NativeIcons
    {
        private sealed class Shape {public readonly List<List<Vector2>> Lines=new List<List<Vector2>>();public bool Fill,Stroke=true;public float Width;public Color FillColor=Color.white,StrokeColor=Color.white;public float[] GradientAt;public Color[] GradientColors;}
        private static readonly Dictionary<string,List<Shape>> parsed=new Dictionary<string,List<Shape>>();
        // Rasterised textures by (id or markup key, markup, pixel size): a value key, so a cached draw builds no string.
        private static readonly Dictionary<(string,bool,int),Texture2D> textures=new Dictionary<(string,bool,int),Texture2D>();
        [Serializable] public sealed class IconEntry {public string id,markup,icon;}
        [Serializable] public sealed class IconSet {public string commit,license;public float strokeWidth;public IconEntry[] icons,chips,classes,specs,actions;}
        private static Dictionary<string,string> markup,chips,classes,specs,actions;private static float strokeWidth=1.7f;
        // Lucide's ISC notice, shipped with the icon data (shown by the settings credits).
        public static string License{get;private set;}
        private static void Ensure()
        {
            if(markup!=null)return;var asset=Resources.Load<TextAsset>("Icons");if(asset==null)throw new InvalidOperationException("Icons.json is missing");var set=JsonUtility.FromJson<IconSet>(asset.text);strokeWidth=set.strokeWidth>0?set.strokeWidth:1.7f;License=set.license;
            Dictionary<string,string> Map(IconEntry[] e,bool paths){var d=new Dictionary<string,string>();foreach(var x in e??new IconEntry[0])d[x.id]=paths?x.markup:x.icon;return d;}
            markup=Map(set.icons,true);chips=Map(set.chips,false);classes=Map(set.classes,false);specs=Map(set.specs,false);actions=Map(set.actions,false);
        }
        public static bool Has(string id){Ensure();return markup.ContainsKey(id??"");}
        // icon-map.ts lookups; unmapped ids fall back to cpu as icons.ts does (actions have no fallback).
        public static string Chip(string id){Ensure();return chips.TryGetValue(id??"",out var i)?i:"cpu";}
        public static string Class(string id){Ensure();return classes.TryGetValue(id??"",out var i)?i:"cpu";}
        public static string Spec(string id){Ensure();return specs.TryGetValue(id??"",out var i)?i:"cpu";}
        public static string Action(string id){Ensure();return actions.TryGetValue(id??"",out var i)?i:null;}
        // Draw icon `id` filling the virtual rect `r` (square), tinted `c`.
        public static void Draw(Rect r,string id,Color c)
        {
            if(c.a<=0||Event.current.type!=EventType.Repaint)return;Ensure();if(!markup.TryGetValue(id??"",out string svg)&&!markup.TryGetValue("cpu",out svg))return;
            int px=Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(r.width,r.height)*NativeUi.S),8,512);var key=(id,false,px);
            if(!textures.TryGetValue(key,out var t)){if(textures.Count>256)Clear();t=Texture(Raster(Shapes(id,svg,true),px,24),px);textures[key]=t;}
            NativeUi.Image(r,t,c);
        }
        // Draw coloured markup (its own fills and strokes; default fill black as in SVG) on a `box`-unit square viewBox.
        // `key` names the drawing for the cache; `c` tints it (white keeps its colours).
        // `maxPx` caps the raster size (soft art drawn large, such as the chip card's flame row, rasterises once at a lower size).
        public static void DrawMarkup(Rect r,string key,string svg,float box,Color c,int maxPx=512)
        {
            if(c.a<=0||Event.current.type!=EventType.Repaint)return;int px=Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(r.width,r.height)*NativeUi.S),8,Mathf.Clamp(maxPx,8,512));var k=(key,true,px);
            if(!textures.TryGetValue(k,out var t)){if(textures.Count>256)Clear();t=Texture(Raster(Shapes("svg:"+key,svg,false),px,box),px);textures[k]=t;}
            NativeUi.Image(r,t,c);
        }
        public static void Clear(){foreach(var t in textures.Values)if(t!=null)UnityEngine.Object.Destroy(t);textures.Clear();}
        private static List<Shape> Shapes(string id,string svg,bool lucide){if(!parsed.TryGetValue(id,out var list))parsed[id]=list=Parse(svg,lucide?strokeWidth:1,lucide);return list;}
        // Lucide markup inherits fill="none" stroke="currentColor" from icons.ts's <svg>; other markup uses SVG defaults.
        private static List<Shape> Parse(string svg,float width,bool lucide)
        {
            var list=new List<Shape>();var gradients=new Dictionary<string,(float[] at,Color[] colors)>();
            foreach(Match g in Regex.Matches(svg,@"<linearGradient\b([^>]*)>(.*?)</linearGradient>",RegexOptions.Singleline))
            {
                var ga=Attributes(g.Groups[1].Value);var at=new List<float>();var colors=new List<Color>();
                foreach(Match st in Regex.Matches(g.Groups[2].Value,@"<stop\b([^>]*?)/?>")){var sa=Attributes(st.Groups[1].Value);at.Add(sa.TryGetValue("offset",out var o)?Num(o):0);colors.Add(Paint(sa.TryGetValue("stop-color",out var sc)?sc:"#000",Color.black));}
                if(ga.TryGetValue("id",out var gid)&&at.Count>0)gradients[gid]=(at.ToArray(),colors.ToArray());
            }
            foreach(Match m in Regex.Matches(svg,@"<(path|circle|ellipse|rect|line|polyline|polygon)\b([^>]*?)/?>"))
            {
                var a=Attributes(m.Groups[2].Value);var s=new Shape{Width=width};string fill=a.TryGetValue("fill",out var f)?f:lucide?"none":"#000",stroke=a.TryGetValue("stroke",out var k)?k:lucide?"currentColor":"none";
                s.Fill=fill!="none";s.Stroke=stroke!="none";if(a.TryGetValue("stroke-width",out var sw))s.Width=Num(sw);s.FillColor=Paint(fill,Color.white);s.StrokeColor=Paint(stroke,Color.white);
                var url=Regex.Match(fill,@"url\(#([^)]+)\)");if(url.Success&&gradients.TryGetValue(url.Groups[1].Value,out var grad)){s.GradientAt=grad.at;s.GradientColors=grad.colors;}
                switch(m.Groups[1].Value)
                {
                    case "path":if(a.TryGetValue("d",out var d))PathLines(d,s.Lines);break;
                    case "circle":s.Lines.Add(EllipseLine(Num(a,"cx"),Num(a,"cy"),Num(a,"r"),Num(a,"r")));break;
                    case "ellipse":s.Lines.Add(EllipseLine(Num(a,"cx"),Num(a,"cy"),Num(a,"rx"),Num(a,"ry")));break;
                    case "line":s.Lines.Add(new List<Vector2>{new Vector2(Num(a,"x1"),Num(a,"y1")),new Vector2(Num(a,"x2"),Num(a,"y2"))});break;
                    case "rect":s.Lines.Add(RectLine(Num(a,"x"),Num(a,"y"),Num(a,"width"),Num(a,"height"),a.ContainsKey("rx")?Num(a,"rx"):a.ContainsKey("ry")?Num(a,"ry"):0,a.ContainsKey("ry")?Num(a,"ry"):a.ContainsKey("rx")?Num(a,"rx"):0));break;
                    case "polyline":case "polygon":{var p=Points(a.TryGetValue("points",out var pts)?pts:"");if(m.Groups[1].Value=="polygon"&&p.Count>0)p.Add(p[0]);s.Lines.Add(p);break;}
                }
                list.Add(s);
            }
            return list;
        }
        // currentColor (and anything unrecognised) is white so the caller's tint decides; #rgb and #rrggbb are honoured.
        private static Color Paint(string value,Color fallback)
        {
            value=(value??"").Trim();if(value.StartsWith("#")&&(value.Length==4||value.Length==7)&&int.TryParse(value.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out int rgb))
            {if(value.Length==4)rgb=((rgb>>8&15)*17<<16)|((rgb>>4&15)*17<<8)|((rgb&15)*17);return NativeUi.Hex(rgb);}
            return fallback;
        }
        private static Dictionary<string,string> Attributes(string text){var a=new Dictionary<string,string>();foreach(Match m in Regex.Matches(text,@"([\w:-]+)\s*=\s*""([^""]*)"""))a[m.Groups[1].Value]=m.Groups[2].Value;return a;}
        private static float Num(string s)=>float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out float v)?v:0;
        private static float Num(Dictionary<string,string> a,string k)=>a.TryGetValue(k,out var v)?Num(v):0;
        private static List<Vector2> Points(string text){var n=Numbers(text);var p=new List<Vector2>();for(int i=0;i+1<n.Count;i+=2)p.Add(new Vector2(n[i],n[i+1]));return p;}
        private static List<float> Numbers(string text){var n=new List<float>();foreach(Match m in Regex.Matches(text,@"[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?"))n.Add(Num(m.Value));return n;}
        private static List<Vector2> EllipseLine(float cx,float cy,float rx,float ry){var p=new List<Vector2>();for(int i=0;i<=64;i++){float a=i/64f*2*Mathf.PI;p.Add(new Vector2(cx+rx*Mathf.Cos(a),cy+ry*Mathf.Sin(a)));}return p;}
        private static List<Vector2> RectLine(float x,float y,float w,float h,float rx,float ry)
        {
            rx=Mathf.Min(rx,w/2);ry=Mathf.Min(ry,h/2);var p=new List<Vector2>();
            void Corner(float cx,float cy,float a0){for(int i=0;i<=8;i++){float a=a0+i/8f*Mathf.PI/2;p.Add(new Vector2(cx+rx*Mathf.Cos(a),cy+ry*Mathf.Sin(a)));}}
            if(rx<=0||ry<=0){p.Add(new Vector2(x,y));p.Add(new Vector2(x+w,y));p.Add(new Vector2(x+w,y+h));p.Add(new Vector2(x,y+h));p.Add(new Vector2(x,y));return p;}
            Corner(x+w-rx,y+ry,-Mathf.PI/2);Corner(x+w-rx,y+h-ry,0);Corner(x+rx,y+h-ry,Mathf.PI/2);Corner(x+rx,y+ry,Mathf.PI);p.Add(p[0]);return p;
        }
        // SVG path data to polylines (one per subpath); curves and arcs are flattened finely enough for icon sizes. A character
        // scanner rather than a number regex, because minified arcs pack their flags against the next number ("0 00-2.4").
        private static void PathLines(string d,List<List<Vector2>> lines)
        {
            int i=0;char cmd=' ',last=' ';Vector2 cur=Vector2.zero,start=Vector2.zero,ctrl=Vector2.zero;List<Vector2> line=null;
            void Skip(){while(i<d.Length&&(char.IsWhiteSpace(d[i])||d[i]==','))i++;}
            bool AtNumber(){Skip();return i<d.Length&&(char.IsDigit(d[i])||d[i]=='-'||d[i]=='+'||d[i]=='.');}
            float N()
            {
                Skip();int s0=i;if(i<d.Length&&(d[i]=='-'||d[i]=='+'))i++;bool dot=false;
                while(i<d.Length&&(char.IsDigit(d[i])||d[i]=='.'&&!dot)){if(d[i]=='.')dot=true;i++;}
                if(i<d.Length&&(d[i]=='e'||d[i]=='E')){int e=i++;if(i<d.Length&&(d[i]=='-'||d[i]=='+'))i++;if(i<d.Length&&char.IsDigit(d[i]))while(i<d.Length&&char.IsDigit(d[i]))i++;else i=e;}
                return Num(d.Substring(s0,i-s0));
            }
            bool Flag(){Skip();return i<d.Length&&d[i++]=='1';}
            void Begin(Vector2 p){line=new List<Vector2>{p};lines.Add(line);}void To(Vector2 p){if(line==null)Begin(cur);line.Add(p);}
            while(true)
            {
                Skip();if(i>=d.Length)break;
                if(char.IsLetter(d[i]))cmd=d[i++];else if(cmd==' '||cmd=='Z'||cmd=='z'||!AtNumber())break;bool rel=char.IsLower(cmd);Vector2 o=rel?cur:Vector2.zero;
                switch(char.ToUpperInvariant(cmd))
                {
                    case 'M':cur=o+new Vector2(N(),N());start=cur;Begin(cur);cmd=rel?'l':'L';break;
                    case 'L':cur=o+new Vector2(N(),N());To(cur);break;
                    case 'H':cur=new Vector2((rel?cur.x:0)+N(),cur.y);To(cur);break;
                    case 'V':cur=new Vector2(cur.x,(rel?cur.y:0)+N());To(cur);break;
                    case 'C':{var c1=o+new Vector2(N(),N());var c2=o+new Vector2(N(),N());var e=o+new Vector2(N(),N());Cubic(cur,c1,c2,e,To);ctrl=c2;cur=e;break;}
                    case 'S':{var c1="CcSs".IndexOf(last)>=0?2*cur-ctrl:cur;var c2=o+new Vector2(N(),N());var e=o+new Vector2(N(),N());Cubic(cur,c1,c2,e,To);ctrl=c2;cur=e;break;}
                    case 'Q':{var c=o+new Vector2(N(),N());var e=o+new Vector2(N(),N());Quad(cur,c,e,To);ctrl=c;cur=e;break;}
                    case 'T':{var c="QqTt".IndexOf(last)>=0?2*cur-ctrl:cur;var e=o+new Vector2(N(),N());Quad(cur,c,e,To);ctrl=c;cur=e;break;}
                    case 'A':{float rx=N(),ry=N(),rot=N();bool large=Flag(),sweep=Flag();var e=o+new Vector2(N(),N());Arc(cur,rx,ry,rot,large,sweep,e,To);cur=e;break;}
                    case 'Z':cur=start;To(cur);line=null;break;
                    default:return;
                }
                last=cmd;
            }
        }
        private static void Cubic(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Action<Vector2> to){for(int k=1;k<=16;k++){float t=k/16f,u=1-t;to(u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d);}}
        private static void Quad(Vector2 a,Vector2 b,Vector2 c,Action<Vector2> to){for(int k=1;k<=12;k++){float t=k/12f,u=1-t;to(u*u*a+2*u*t*b+t*t*c);}}
        // SVG arc (endpoint form) via the centre parameterisation of the SVG 1.1 implementation notes (F.6.5).
        private static void Arc(Vector2 p0,float rx,float ry,float rotDeg,bool large,bool sweep,Vector2 p1,Action<Vector2> to)
        {
            if(rx==0||ry==0||p0==p1){to(p1);return;}rx=Mathf.Abs(rx);ry=Mathf.Abs(ry);float phi=rotDeg*Mathf.Deg2Rad,cos=Mathf.Cos(phi),sin=Mathf.Sin(phi);
            var dp=(p0-p1)/2;float x1=cos*dp.x+sin*dp.y,y1=-sin*dp.x+cos*dp.y;float lambda=x1*x1/(rx*rx)+y1*y1/(ry*ry);if(lambda>1){float s=Mathf.Sqrt(lambda);rx*=s;ry*=s;}
            float num=rx*rx*ry*ry-rx*rx*y1*y1-ry*ry*x1*x1,den=rx*rx*y1*y1+ry*ry*x1*x1,co=Mathf.Sqrt(Mathf.Max(0,num/Mathf.Max(den,1e-9f)))*(large==sweep?-1:1);
            float cx1=co*rx*y1/ry,cy1=-co*ry*x1/rx;var c=new Vector2(cos*cx1-sin*cy1,sin*cx1+cos*cy1)+(p0+p1)/2;
            float Angle(Vector2 u,Vector2 v)=>Mathf.Atan2(u.x*v.y-u.y*v.x,Vector2.Dot(u,v));var a=new Vector2((x1-cx1)/rx,(y1-cy1)/ry);var b=new Vector2((-x1-cx1)/rx,(-y1-cy1)/ry);
            float t0=Angle(Vector2.right,a),dt=Angle(a,b);if(!sweep&&dt>0)dt-=2*Mathf.PI;else if(sweep&&dt<0)dt+=2*Mathf.PI;int n=Mathf.Max(4,Mathf.CeilToInt(Mathf.Abs(dt)/(Mathf.PI/16)));
            for(int k=1;k<=n;k++){float t=t0+dt*k/n;float ex=rx*Mathf.Cos(t),ey=ry*Mathf.Sin(t);to(new Vector2(cos*ex-sin*ey,sin*ex+cos*ey)+c);}
        }
        private static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b){var ab=b-a;float l=ab.sqrMagnitude;float t=l>0?Mathf.Clamp01(Vector2.Dot(p-a,ab)/l):0;return (p-(a+ab*t)).magnitude;}
        private static Texture2D Texture(Color[] pixels,int px)
        {var t=new Texture2D(px,px,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};t.SetPixels(pixels);t.Apply(false,true);return t;}
        // Coverage (alpha) of a Lucide icon, bottom row first (texture order). Pure managed code so the checks can run it.
        public static float[] Coverage(string svg,int px,float width=1.7f){var c=Raster(Parse(svg,width,true),px,24);var a=new float[c.Length];for(int i=0;i<c.Length;i++)a[i]=c[i].a;return a;}
        // Shapes composited in order (source-over): strokes by distance to the flattened segments (round caps and joins),
        // fills by nonzero winding with subpaths implicitly closed and a half-pixel edge ramp. Straight-alpha output.
        private static Color[] Raster(List<Shape> shapes,int px,float box)
        {
            var output=new Color[px*px];float scale=px/box;var bounds=new Rect[shapes.Count];
            for(int i=0;i<shapes.Count;i++){float x0=float.MaxValue,y0=float.MaxValue,x1=float.MinValue,y1=float.MinValue;foreach(var line in shapes[i].Lines)foreach(var q in line){x0=Mathf.Min(x0,q.x);y0=Mathf.Min(y0,q.y);x1=Mathf.Max(x1,q.x);y1=Mathf.Max(y1,q.y);}bounds[i]=Rect.MinMaxRect(x0,y0,x1,y1);}
            for(int y=0;y<px;y++)for(int x=0;x<px;x++)
            {
                var p=new Vector2((x+.5f)/scale,(px-y-.5f)/scale);float r=0,g=0,b=0,a=0;
                void Over(Color c,float cover){float sa=c.a*cover;if(sa<=0)return;r=c.r*sa+r*(1-sa);g=c.g*sa+g*(1-sa);b=c.b*sa+b*(1-sa);a=sa+a*(1-sa);}
                for(int i=0;i<shapes.Count;i++)
                {
                    // A pixel farther from a shape's bounds than its stroke half-width plus a pixel gets no coverage from it
                    // (fill and stroke both fade to zero within half a pixel), so it skips the shape's segments: large
                    // drawings with many small parts (the chip card's flame row) rasterise in a fraction of the time.
                    var s=shapes[i];var bb=bounds[i];float margin=(s.Stroke?s.Width/2:0)+1.5f/scale;
                    if(p.x<bb.xMin-margin||p.x>bb.xMax+margin||p.y<bb.yMin-margin||p.y>bb.yMax+margin)continue;
                    float d=float.MaxValue;int winding=0;
                    foreach(var line in s.Lines)for(int k=0;k<line.Count;k++)
                    {
                        bool closing=k==line.Count-1;if(closing&&!s.Fill)break;var q0=line[k];var q1=line[closing?0:k+1];
                        if(!closing)d=Mathf.Min(d,SegmentDistance(p,q0,q1));
                        if(s.Fill){float side=(q1.x-q0.x)*(p.y-q0.y)-(p.x-q0.x)*(q1.y-q0.y);if(q0.y<=p.y){if(q1.y>p.y&&side>0)winding++;}else if(q1.y<=p.y&&side<0)winding--;}
                    }
                    if(s.Fill&&s.Lines.Count>0)
                    {
                        float cover=winding!=0?1:Mathf.Clamp01(.5f-d*scale);var c=s.FillColor;
                        if(s.GradientAt!=null&&bounds[i].height>0){float t=Mathf.Clamp01((p.y-bounds[i].y)/bounds[i].height);c=s.GradientColors[0];for(int k=1;k<s.GradientAt.Length;k++)if(t>=s.GradientAt[k-1])c=t>=s.GradientAt[k]?s.GradientColors[k]:Color.Lerp(s.GradientColors[k-1],s.GradientColors[k],(t-s.GradientAt[k-1])/Mathf.Max(1e-5f,s.GradientAt[k]-s.GradientAt[k-1]));}
                        Over(c,cover);
                    }
                    if(s.Stroke&&d<float.MaxValue)Over(s.StrokeColor,Mathf.Clamp01((s.Width/2-d)*scale+.5f));
                }
                output[y*px+x]=a>0?new Color(r/a,g/a,b/a,a):Color.clear;
            }
            return output;
        }
    }
}
