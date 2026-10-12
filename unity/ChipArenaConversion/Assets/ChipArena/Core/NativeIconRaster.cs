using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The engine-free SVG-subset parser and rasteriser behind NativeIcons (Runtime keeps the textures, caching and
    // drawing), so the managed checks can render every icon. A float-for-float port of the Runtime code it replaced, with
    // only exact shortcuts added (squared distances, per-subpath bounds): Lucide and DrawMarkup output are bit-identical.
    // Supports path (M L H V C S Q T A Z, absolute and relative, minified arc flags), circle, ellipse, rect (rx/ry), line,
    // polyline and polygon, stroked and/or filled, solid and vertical linear-gradient fills. Art mode (the original class
    // and weapon icons, ClassIcons.json) adds three opt-in features no older markup uses: fill-rule="evenodd", opacity /
    // fill-opacity / stroke-opacity (the duotone second tone) and data-knockout="1" shapes that erase what is under them
    // (clean negative-space gaps between overlapping parts); its fills also anti-alias across the edge, as strokes always
    // have, instead of only outside it.
    public static class NativeIconRaster
    {
        public enum Mode {Lucide,Markup,Art}
        public struct Pt
        {
            public float x,y;public Pt(float x,float y){this.x=x;this.y=y;}
            public static Pt operator+(Pt a,Pt b)=>new Pt(a.x+b.x,a.y+b.y);public static Pt operator-(Pt a,Pt b)=>new Pt(a.x-b.x,a.y-b.y);
            public static Pt operator*(Pt a,float d)=>new Pt(a.x*d,a.y*d);public static Pt operator*(float d,Pt a)=>new Pt(a.x*d,a.y*d);public static Pt operator/(Pt a,float d)=>new Pt(a.x/d,a.y/d);
            public static float Dot(Pt a,Pt b)=>a.x*b.x+a.y*b.y;public float SqrMagnitude=>x*x+y*y;public float Magnitude=>(float)Math.Sqrt(x*x+y*y);
            public static bool Near(Pt a,Pt b){float dx=a.x-b.x,dy=a.y-b.y;return dx*dx+dy*dy<1e-5f*1e-5f;}
        }
        public struct Rgba {public float r,g,b,a;public Rgba(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}public static readonly Rgba White=new Rgba(1,1,1,1);}
        public sealed class Shape
        {
            public readonly List<List<Pt>> Lines=new List<List<Pt>>();public bool Fill,Stroke=true,EvenOdd,Erase,Centered;public float Width,FillAlpha=1,StrokeAlpha=1;
            public Rgba FillColor=Rgba.White,StrokeColor=Rgba.White;public float[] GradientAt;public Rgba[] GradientColors;
        }
        // Art icons draw their simplified `small` markup at or below this many physical pixels (ClassIcons.json may override).
        public const int SmallPx=22;
        public static bool UseSmall(string small,int px,int smallPx)=>!string.IsNullOrEmpty(small)&&px<=smallPx;
        // A class or spec id to its icon: the game's art map, then the source's Lucide map, then the art fallback (the DEV
        // chip), else Lucide's cpu as icons.ts does.
        public static string Resolve(IDictionary<string,string> art,IDictionary<string,string> lucide,string fallback,string id)
        {id=id??"";return art!=null&&art.TryGetValue(id,out var a)?a:lucide!=null&&lucide.TryGetValue(id,out var l)?l:fallback??"cpu";}
        private const float PI=3.14159274f,Deg2Rad=PI*2f/360f;
        private static float Clamp01(float v)=>v<0?0:v>1?1:v;
        // Mathf.Min / Mathf.Max semantics (not Math's NaN rules), so the port stays float-identical.
        private static float Min(float a,float b)=>a<b?a:b;private static float Max(float a,float b)=>a>b?a:b;
        private static Rgba Lerp(Rgba a,Rgba b,float t){t=Clamp01(t);return new Rgba(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);}
        // Lucide markup inherits fill="none" stroke="currentColor" from icons.ts's <svg>; other markup uses SVG defaults.
        public static List<Shape> Parse(string svg,float width,Mode mode)
        {
            bool lucide=mode==Mode.Lucide,art=mode==Mode.Art;var list=new List<Shape>();var gradients=new Dictionary<string,(float[] at,Rgba[] colors)>();
            foreach(Match g in Regex.Matches(svg,@"<linearGradient\b([^>]*)>(.*?)</linearGradient>",RegexOptions.Singleline))
            {
                var ga=Attributes(g.Groups[1].Value);var at=new List<float>();var colors=new List<Rgba>();
                foreach(Match st in Regex.Matches(g.Groups[2].Value,@"<stop\b([^>]*?)/?>")){var sa=Attributes(st.Groups[1].Value);at.Add(sa.TryGetValue("offset",out var o)?Num(o):0);colors.Add(Paint(sa.TryGetValue("stop-color",out var sc)?sc:"#000",new Rgba(0,0,0,1)));}
                if(ga.TryGetValue("id",out var gid)&&at.Count>0)gradients[gid]=(at.ToArray(),colors.ToArray());
            }
            foreach(Match m in Regex.Matches(svg,@"<(path|circle|ellipse|rect|line|polyline|polygon)\b([^>]*?)/?>"))
            {
                var a=Attributes(m.Groups[2].Value);var s=new Shape{Width=width};string fill=a.TryGetValue("fill",out var f)?f:lucide?"none":"#000",stroke=a.TryGetValue("stroke",out var k)?k:lucide?"currentColor":"none";
                s.Fill=fill!="none";s.Stroke=stroke!="none";if(a.TryGetValue("stroke-width",out var sw))s.Width=Num(sw);s.FillColor=Paint(fill,Rgba.White);s.StrokeColor=Paint(stroke,Rgba.White);
                var url=Regex.Match(fill,@"url\(#([^)]+)\)");if(url.Success&&gradients.TryGetValue(url.Groups[1].Value,out var grad)){s.GradientAt=grad.at;s.GradientColors=grad.colors;}
                if(art)
                {
                    s.Centered=true;s.EvenOdd=a.TryGetValue("fill-rule",out var rule)&&rule=="evenodd";s.Erase=a.TryGetValue("data-knockout",out var ko)&&ko=="1";
                    float all=a.ContainsKey("opacity")?Clamp01(Num(a,"opacity")):1;s.FillAlpha=all*(a.ContainsKey("fill-opacity")?Clamp01(Num(a,"fill-opacity")):1);s.StrokeAlpha=all*(a.ContainsKey("stroke-opacity")?Clamp01(Num(a,"stroke-opacity")):1);
                }
                switch(m.Groups[1].Value)
                {
                    case "path":if(a.TryGetValue("d",out var d))PathLines(d,s.Lines);break;
                    case "circle":s.Lines.Add(EllipseLine(Num(a,"cx"),Num(a,"cy"),Num(a,"r"),Num(a,"r")));break;
                    case "ellipse":s.Lines.Add(EllipseLine(Num(a,"cx"),Num(a,"cy"),Num(a,"rx"),Num(a,"ry")));break;
                    case "line":s.Lines.Add(new List<Pt>{new Pt(Num(a,"x1"),Num(a,"y1")),new Pt(Num(a,"x2"),Num(a,"y2"))});break;
                    case "rect":s.Lines.Add(RectLine(Num(a,"x"),Num(a,"y"),Num(a,"width"),Num(a,"height"),a.ContainsKey("rx")?Num(a,"rx"):a.ContainsKey("ry")?Num(a,"ry"):0,a.ContainsKey("ry")?Num(a,"ry"):a.ContainsKey("rx")?Num(a,"rx"):0));break;
                    case "polyline":case "polygon":{var p=Points(a.TryGetValue("points",out var pts)?pts:"");if(m.Groups[1].Value=="polygon"&&p.Count>0)p.Add(p[0]);s.Lines.Add(p);break;}
                }
                list.Add(s);
            }
            return list;
        }
        // currentColor (and anything unrecognised) is white so the caller's tint decides; #rgb and #rrggbb are honoured.
        private static Rgba Paint(string value,Rgba fallback)
        {
            value=(value??"").Trim();if(value.StartsWith("#")&&(value.Length==4||value.Length==7)&&int.TryParse(value.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out int rgb))
            {if(value.Length==4)rgb=((rgb>>8&15)*17<<16)|((rgb>>4&15)*17<<8)|((rgb&15)*17);return new Rgba(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f,1);}
            return fallback;
        }
        private static Dictionary<string,string> Attributes(string text){var a=new Dictionary<string,string>();foreach(Match m in Regex.Matches(text,@"([\w:-]+)\s*=\s*""([^""]*)"""))a[m.Groups[1].Value]=m.Groups[2].Value;return a;}
        private static float Num(string s)=>float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out float v)?v:0;
        private static float Num(Dictionary<string,string> a,string k)=>a.TryGetValue(k,out var v)?Num(v):0;
        private static List<Pt> Points(string text){var n=Numbers(text);var p=new List<Pt>();for(int i=0;i+1<n.Count;i+=2)p.Add(new Pt(n[i],n[i+1]));return p;}
        private static List<float> Numbers(string text){var n=new List<float>();foreach(Match m in Regex.Matches(text,@"[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?"))n.Add(Num(m.Value));return n;}
        private static List<Pt> EllipseLine(float cx,float cy,float rx,float ry){var p=new List<Pt>();for(int i=0;i<=64;i++){float a=i/64f*2*PI;p.Add(new Pt(cx+rx*(float)Math.Cos(a),cy+ry*(float)Math.Sin(a)));}return p;}
        private static List<Pt> RectLine(float x,float y,float w,float h,float rx,float ry)
        {
            rx=Min(rx,w/2);ry=Min(ry,h/2);var p=new List<Pt>();
            void Corner(float cx,float cy,float a0){for(int i=0;i<=8;i++){float a=a0+i/8f*PI/2;p.Add(new Pt(cx+rx*(float)Math.Cos(a),cy+ry*(float)Math.Sin(a)));}}
            if(rx<=0||ry<=0){p.Add(new Pt(x,y));p.Add(new Pt(x+w,y));p.Add(new Pt(x+w,y+h));p.Add(new Pt(x,y+h));p.Add(new Pt(x,y));return p;}
            Corner(x+w-rx,y+ry,-PI/2);Corner(x+w-rx,y+h-ry,0);Corner(x+rx,y+h-ry,PI/2);Corner(x+rx,y+ry,PI);p.Add(p[0]);return p;
        }
        // SVG path data to polylines (one per subpath); curves and arcs are flattened finely enough for icon sizes. A character
        // scanner rather than a number regex, because minified arcs pack their flags against the next number ("0 00-2.4").
        private static void PathLines(string d,List<List<Pt>> lines)
        {
            int i=0;char cmd=' ',last=' ';Pt cur=default,start=default,ctrl=default;List<Pt> line=null;
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
            void Begin(Pt p){line=new List<Pt>{p};lines.Add(line);}void To(Pt p){if(line==null)Begin(cur);line.Add(p);}
            while(true)
            {
                Skip();if(i>=d.Length)break;
                if(char.IsLetter(d[i]))cmd=d[i++];else if(cmd==' '||cmd=='Z'||cmd=='z'||!AtNumber())break;bool rel=char.IsLower(cmd);Pt o=rel?cur:default;
                switch(char.ToUpperInvariant(cmd))
                {
                    case 'M':cur=o+new Pt(N(),N());start=cur;Begin(cur);cmd=rel?'l':'L';break;
                    case 'L':cur=o+new Pt(N(),N());To(cur);break;
                    case 'H':cur=new Pt((rel?cur.x:0)+N(),cur.y);To(cur);break;
                    case 'V':cur=new Pt(cur.x,(rel?cur.y:0)+N());To(cur);break;
                    case 'C':{var c1=o+new Pt(N(),N());var c2=o+new Pt(N(),N());var e=o+new Pt(N(),N());Cubic(cur,c1,c2,e,To);ctrl=c2;cur=e;break;}
                    case 'S':{var c1="CcSs".IndexOf(last)>=0?2*cur-ctrl:cur;var c2=o+new Pt(N(),N());var e=o+new Pt(N(),N());Cubic(cur,c1,c2,e,To);ctrl=c2;cur=e;break;}
                    case 'Q':{var c=o+new Pt(N(),N());var e=o+new Pt(N(),N());Quad(cur,c,e,To);ctrl=c;cur=e;break;}
                    case 'T':{var c="QqTt".IndexOf(last)>=0?2*cur-ctrl:cur;var e=o+new Pt(N(),N());Quad(cur,c,e,To);ctrl=c;cur=e;break;}
                    case 'A':{float rx=N(),ry=N(),rot=N();bool large=Flag(),sweep=Flag();var e=o+new Pt(N(),N());Arc(cur,rx,ry,rot,large,sweep,e,To);cur=e;break;}
                    case 'Z':cur=start;To(cur);line=null;break;
                    default:return;
                }
                last=cmd;
            }
        }
        private static void Cubic(Pt a,Pt b,Pt c,Pt d,Action<Pt> to){for(int k=1;k<=16;k++){float t=k/16f,u=1-t;to(u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d);}}
        private static void Quad(Pt a,Pt b,Pt c,Action<Pt> to){for(int k=1;k<=12;k++){float t=k/12f,u=1-t;to(u*u*a+2*u*t*b+t*t*c);}}
        // SVG arc (endpoint form) via the centre parameterisation of the SVG 1.1 implementation notes (F.6.5).
        private static void Arc(Pt p0,float rx,float ry,float rotDeg,bool large,bool sweep,Pt p1,Action<Pt> to)
        {
            if(rx==0||ry==0||Pt.Near(p0,p1)){to(p1);return;}rx=Math.Abs(rx);ry=Math.Abs(ry);float phi=rotDeg*Deg2Rad,cos=(float)Math.Cos(phi),sin=(float)Math.Sin(phi);
            var dp=(p0-p1)/2;float x1=cos*dp.x+sin*dp.y,y1=-sin*dp.x+cos*dp.y;float lambda=x1*x1/(rx*rx)+y1*y1/(ry*ry);if(lambda>1){float s=(float)Math.Sqrt(lambda);rx*=s;ry*=s;}
            float num=rx*rx*ry*ry-rx*rx*y1*y1-ry*ry*x1*x1,den=rx*rx*y1*y1+ry*ry*x1*x1,co=(float)Math.Sqrt(Max(0,num/Max(den,1e-9f)))*(large==sweep?-1:1);
            float cx1=co*rx*y1/ry,cy1=-co*ry*x1/rx;var c=new Pt(cos*cx1-sin*cy1,sin*cx1+cos*cy1)+(p0+p1)/2;
            float Angle(Pt u,Pt v)=>(float)Math.Atan2(u.x*v.y-u.y*v.x,Pt.Dot(u,v));var a=new Pt((x1-cx1)/rx,(y1-cy1)/ry);var b=new Pt((-x1-cx1)/rx,(-y1-cy1)/ry);
            float t0=Angle(new Pt(1,0),a),dt=Angle(a,b);if(!sweep&&dt>0)dt-=2*PI;else if(sweep&&dt<0)dt+=2*PI;int n=Math.Max(4,(int)Math.Ceiling(Math.Abs(dt)/(PI/16)));
            for(int k=1;k<=n;k++){float t=t0+dt*k/n;float ex=rx*(float)Math.Cos(t),ey=ry*(float)Math.Sin(t);to(new Pt(cos*ex-sin*ey,sin*ex+cos*ey)+c);}
        }
        // Squared distance to a segment: the minimum is taken squared and rooted once (the root is monotonic, so the result is
        // the same float as rooting each distance).
        private static float SegmentDistance2(Pt p,Pt a,Pt b){var ab=b-a;float l=ab.SqrMagnitude;float t=l>0?Clamp01(Pt.Dot(p-a,ab)/l):0;return (p-(a+ab*t)).SqrMagnitude;}
        // Coverage (alpha) of a Lucide icon, bottom row first (texture order).
        public static float[] Coverage(string svg,int px,float width=1.7f)=>Alpha(Raster(Parse(svg,width,Mode.Lucide),px,24));
        // Coverage of parsed shapes on a `box`-unit square view box, bottom row first.
        public static float[] Coverage(List<Shape> shapes,int px,float box)=>Alpha(Raster(shapes,px,box));
        private static float[] Alpha(float[] rgba){var a=new float[rgba.Length/4];for(int i=0;i<a.Length;i++)a[i]=rgba[i*4+3];return a;}
        // Shapes composited in order (source-over; knockout shapes erase instead): strokes by distance to the flattened
        // segments (round caps and joins), fills by nonzero (or even-odd) winding with subpaths implicitly closed and a
        // half-pixel edge ramp (outside only, or across the edge in art mode). Straight-alpha RGBA, bottom row first.
        public static float[] Raster(List<Shape> shapes,int px,float box)
        {
            var output=new float[px*px*4];float scale=px/box;var bx=new float[shapes.Count];var by=new float[shapes.Count];var bw=new float[shapes.Count];var bh=new float[shapes.Count];
            // Per-subpath bounds as well: a subpath whose box is no nearer than the best distance so far cannot lower it, and
            // one the pixel's row misses adds no winding, so the pixel skips its segments (an exact shortcut).
            var lb=new float[shapes.Count][];
            for(int i=0;i<shapes.Count;i++)
            {
                float x0=float.MaxValue,y0=float.MaxValue,x1=float.MinValue,y1=float.MinValue;var lines=shapes[i].Lines;lb[i]=new float[lines.Count*4];
                for(int li=0;li<lines.Count;li++){float lx0=float.MaxValue,ly0=float.MaxValue,lx1=float.MinValue,ly1=float.MinValue;foreach(var q in lines[li]){lx0=Min(lx0,q.x);ly0=Min(ly0,q.y);lx1=Max(lx1,q.x);ly1=Max(ly1,q.y);}lb[i][li*4]=lx0;lb[i][li*4+1]=ly0;lb[i][li*4+2]=lx1;lb[i][li*4+3]=ly1;x0=Min(x0,lx0);y0=Min(y0,ly0);x1=Max(x1,lx1);y1=Max(y1,ly1);}
                bx[i]=x0;by[i]=y0;bw[i]=x1-x0;bh[i]=y1-y0;
            }
            for(int y=0;y<px;y++)for(int x=0;x<px;x++)
            {
                var p=new Pt((x+.5f)/scale,(px-y-.5f)/scale);float r=0,g=0,b=0,a=0;
                void Over(Rgba c,float cover){float sa=c.a*cover;if(sa<=0)return;r=c.r*sa+r*(1-sa);g=c.g*sa+g*(1-sa);b=c.b*sa+b*(1-sa);a=sa+a*(1-sa);}
                for(int i=0;i<shapes.Count;i++)
                {
                    // A pixel farther from a shape's bounds than its stroke half-width plus a pixel gets no coverage from it
                    // (fill and stroke both fade to zero within half a pixel), so it skips the shape's segments: large
                    // drawings with many small parts (the chip card's flame row) rasterise in a fraction of the time.
                    var s=shapes[i];float margin=(s.Stroke?s.Width/2:0)+1.5f/scale;
                    if(p.x<bx[i]-margin||p.x>bx[i]+bw[i]+margin||p.y<by[i]-margin||p.y>by[i]+bh[i]+margin)continue;
                    float d2=float.MaxValue;int winding=0;var sb=lb[i];
                    for(int li=0;li<s.Lines.Count;li++)
                    {
                        var line=s.Lines[li];float ex=Max(Max(sb[li*4]-p.x,p.x-sb[li*4+2]),0),ey=Max(Max(sb[li*4+1]-p.y,p.y-sb[li*4+3]),0);
                        bool near=ex*ex+ey*ey<d2,crosses=s.Fill&&p.y>=sb[li*4+1]&&p.y<=sb[li*4+3];if(!near&&!crosses)continue;
                        for(int k=0;k<line.Count;k++)
                        {
                            bool closing=k==line.Count-1;if(closing&&!s.Fill)break;var q0=line[k];var q1=line[closing?0:k+1];
                            if(!closing&&near)d2=Min(d2,SegmentDistance2(p,q0,q1));
                            if(s.Fill){float side=(q1.x-q0.x)*(p.y-q0.y)-(p.x-q0.x)*(q1.y-q0.y);if(q0.y<=p.y){if(q1.y>p.y&&side>0)winding++;}else if(q1.y<=p.y&&side<0)winding--;}
                        }
                    }
                    float d=d2==float.MaxValue?float.MaxValue:(float)Math.Sqrt(d2);
                    float fillCover=0,strokeCover=0;
                    if(s.Fill&&s.Lines.Count>0){bool inside=s.EvenOdd?(winding&1)!=0:winding!=0;fillCover=inside?s.Centered?Clamp01(.5f+d*scale):1:Clamp01(.5f-d*scale);}
                    if(s.Stroke&&d<float.MaxValue)strokeCover=Clamp01((s.Width/2-d)*scale+.5f);
                    if(s.Erase){float cut=Max(fillCover*s.FillAlpha,strokeCover*s.StrokeAlpha);if(cut>0){float keep=1-cut;r*=keep;g*=keep;b*=keep;a*=keep;}continue;}
                    if(s.Fill&&s.Lines.Count>0)
                    {
                        var c=s.FillColor;
                        if(s.GradientAt!=null&&bh[i]>0){float t=Clamp01((p.y-by[i])/bh[i]);c=s.GradientColors[0];for(int k=1;k<s.GradientAt.Length;k++)if(t>=s.GradientAt[k-1])c=t>=s.GradientAt[k]?s.GradientColors[k]:Lerp(s.GradientColors[k-1],s.GradientColors[k],(t-s.GradientAt[k-1])/Max(1e-5f,s.GradientAt[k]-s.GradientAt[k-1]));}
                        Over(c,fillCover*s.FillAlpha);
                    }
                    if(s.Stroke&&d<float.MaxValue)Over(s.StrokeColor,strokeCover*s.StrokeAlpha);
                }
                int o=(y*px+x)*4;if(a>0){output[o]=r/a;output[o+1]=g/a;output[o+2]=b/a;output[o+3]=a;}
            }
            return output;
        }
    }
}
