using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The cartridge's baked layers (NativeChipCartridge's design), rasterised engine-free at the exact physical pixel size they
    // are drawn so they stay crisp at every UI scale; Runtime NativeChipArt turns them into cached textures (built once per
    // tier and scale, never per frame) and animates over them with the kit. Each bake is a straight-alpha RGBA image, bottom
    // row first (texture order). Shapes are signed distance fields with one-pixel coverage; the metal is shaded per facet from
    // the field's gradient (lit from the top left, a cool studio reflection, a specular glint, concentric brushing), so the
    // bezel reads machined. Layers: the shell (the card's outline, bezel, inlay, seams and corner gems, as a nine-slice the
    // card stretches), the core (the hexagonal collar, the glass dome and the faceted crystal), the traces (white, tinted when
    // drawn), the connector pad, the fin, the ring (its back and front halves), the crest of each tier and the badge (the
    // whole cartridge small, for chip lists). Deterministic: the same inputs bake the same bytes.
    public static class NativeChipBake
    {
        // ---- a premultiplied canvas (y down while drawing) ----
        public sealed class Canvas
        {
            public readonly int W,H;public readonly float[] P;
            public Canvas(int w,int h){W=Math.Max(1,w);H=Math.Max(1,h);P=new float[W*H*4];}
            public void Over(int x,int y,Vector3 c,float a)
            {
                if(a<=0||x<0||y<0||x>=W||y>=H)return;a=Math.Min(1,a);int i=(y*W+x)*4;float k=1-a;P[i]=c.X*a+P[i]*k;P[i+1]=c.Y*a+P[i+1]*k;P[i+2]=c.Z*a+P[i+2]*k;P[i+3]=a+P[i+3]*k;
            }
            /// <summary>Straight-alpha RGBA, bottom row first (Texture2D order).</summary>
            public float[] Pixels()
            {
                var o=new float[W*H*4];
                for(int y=0;y<H;y++)for(int x=0;x<W;x++)
                {
                    int s=(y*W+x)*4,d=((H-1-y)*W+x)*4;float a=P[s+3];
                    if(a>1e-5f){o[d]=Math.Min(1,P[s]/a);o[d+1]=Math.Min(1,P[s+1]/a);o[d+2]=Math.Min(1,P[s+2]/a);o[d+3]=Math.Min(1,a);}
                }
                return o;
            }
        }
        public struct Image {public int W,H;public float[] Rgba;}
        static Image Done(Canvas c)=>new Image{W=c.W,H=c.H,Rgba=c.Pixels()};

        // ---- fields and helpers ----
        static Vector3 C(int hex)=>new Vector3(((hex>>16)&255)/255f,((hex>>8)&255)/255f,(hex&255)/255f);
        static float Clamp01(float v)=>v<0?0:v>1?1:v;
        static float Smooth(float a,float b,float x){float t=Clamp01((x-a)/(b-a));return t*t*(3-2*t);}
        static float Cover(float d)=>Clamp01(.5f-d);
        /// <summary>A line's coverage `d` px from its centre, `half` px half-wide (a pixel box filter).</summary>
        static float Line(float d,float half)=>Clamp01(half+.5f-d)*Math.Min(1,half*2);
        /// <summary>Signed distance to a polygon (negative inside), exact.</summary>
        public static float Poly(Vector2 p,Vector2[] v)
        {
            float d=Vector2.DistanceSquared(p,v[0]),s=1;
            for(int i=0,j=v.Length-1;i<v.Length;j=i,i++)
            {
                var e=v[j]-v[i];var w=p-v[i];var b=w-e*Clamp01(Vector2.Dot(w,e)/Math.Max(1e-9f,Vector2.Dot(e,e)));d=Math.Min(d,Vector2.Dot(b,b));
                bool c1=p.Y>=v[i].Y,c2=p.Y<v[j].Y,c3=e.X*w.Y>e.Y*w.X;if(c1&&c2&&c3||!c1&&!c2&&!c3)s=-s;
            }
            return s*(float)Math.Sqrt(d);
        }
        static float Segment(Vector2 p,Vector2 a,Vector2 b){var ab=b-a;float t=Clamp01(Vector2.Dot(p-a,ab)/Math.Max(1e-9f,ab.LengthSquared()));return Vector2.Distance(p,a+ab*t);}
        static Vector2 Grad(Func<Vector2,float> f,Vector2 p){const float h=.35f;var g=new Vector2(f(p+new Vector2(h,0))-f(p-new Vector2(h,0)),f(p+new Vector2(0,h))-f(p-new Vector2(0,h)));float l=g.Length();return l>1e-6f?g/l:Vector2.Zero;}
        static float Hash(float n){double s=Math.Sin(n*12.9898)*43758.5453;return (float)(s-Math.Floor(s));}
        static Vector2[] Scale(Vector2[] v,float k,Vector2 o){var r=new Vector2[v.Length];for(int i=0;i<v.Length;i++)r[i]=v[i]*k+o;return r;}

        // ---- shading ----
        // The light from the top left, toward the viewer (screen space, y down), and its half vector.
        static readonly Vector3 L=Vector3.Normalize(new Vector3(-.5f,-.78f,.62f)),Hv=Vector3.Normalize(L+Vector3.UnitZ);
        public struct Mat {public Vector3 Hi,Mid,Lo;public float Spec,Shine,Grain;}
        static Mat MetalOf(in NativeChipCartridge.Look l)
        {
            var m=new Mat{Hi=C(l.MetalHi),Mid=C(l.MetalMid),Lo=C(l.MetalLo),Spec=.55f,Shine=34,Grain=l.Brushed?.07f:.025f};
            switch(l.Metal){case NativeChipCartridge.Metal.Gold:m.Spec=.8f;m.Shine=26;break;case NativeChipCartridge.Metal.BlackChrome:m.Spec=.95f;m.Shine=48;break;case NativeChipCartridge.Metal.Void:m.Spec=.5f;m.Shine=60;break;case NativeChipCartridge.Metal.Phosphor:m.Spec=.3f;m.Shine=20;break;case NativeChipCartridge.Metal.Lacquer:m.Spec=.7f;m.Shine=40;break;}
            return m;
        }
        static Mat Gold()=>new Mat{Hi=C(0xfff1c4),Mid=C(0xd9a441),Lo=C(0x5e3d0c),Spec=.85f,Shine=24,Grain=.02f};
        /// <summary>A surface of normal `n` in material `m`: the tone ramp from shadow to lit, a cool studio reflection on tilted
        /// faces, the glint and the brushing (`grain` -1..1 at this pixel).</summary>
        static Vector3 Shade(Vector3 n,in Mat m,float grain)
        {
            float lit=Clamp01(.5f+.95f*(n.X*L.X+n.Y*L.Y)/Math.Max(.25f,1-n.Z*.55f)),spec=(float)Math.Pow(Math.Max(0,Vector3.Dot(n,Hv)),m.Shine)*m.Spec;
            var c=lit<.5f?Vector3.Lerp(m.Lo,m.Mid,lit*2):Vector3.Lerp(m.Mid,m.Hi,(lit-.5f)*2);
            // The studio's horizon: faces tilted up catch a cool sky, those tilted down a dark floor.
            float sky=Clamp01(-n.Y*1.4f);c=Vector3.Lerp(c,c*.55f+new Vector3(.55f,.62f,.72f)*.45f,sky*.18f);
            c*=1+m.Grain*grain;return Vector3.Clamp(c+new Vector3(spec),Vector3.Zero,Vector3.One);
        }
        /// <summary>A bevelled ring's normal at depth `e` px inside an outline with outward gradient `g`: the outer facet tilted out,
        /// the flat land, the inner facet tilted in (down to what it frames).</summary>
        static Vector3 Bevel(float e,Vector2 g,float outer,float land,float inner)
        {
            float tilt=e<outer?1f:e<outer+land?0:e<outer+land+inner?-.85f:0;var n=new Vector3(g.X*tilt,g.Y*tilt,1);return Vector3.Normalize(n);
        }

        // ---- the shell: the card's nine-slice (outline, bezel, inlay, seams, gems) ----
        /// <summary>The shell's geometry in virtual px: the corner regions (left = right, top, bottom) and the stretched middle.</summary>
        public static void ShellSlices(bool large,out float side,out float top,out float bottom,out float middle)
        {
            float tc=large?NativeChipCartridge.TopCutLarge:NativeChipCartridge.TopCut,bc=large?NativeChipCartridge.BottomCutLarge:NativeChipCartridge.BottomCut,bw=large?NativeChipCartridge.BezelLarge:NativeChipCartridge.Bezel;
            side=tc+bw+4;top=tc+bw+4;bottom=bc+bw+4;middle=4;
        }
        /// <summary>The shell at `scale` physical px per virtual px (a mini card side+middle+side wide and top+middle+bottom tall).</summary>
        public static Image Shell(string tier,float scale,bool large)
        {
            var l=NativeChipCartridge.Of(tier);ShellSlices(large,out float side,out float top,out float bottom,out float middle);
            float tc=(large?NativeChipCartridge.TopCutLarge:NativeChipCartridge.TopCut)*scale,bc=(large?NativeChipCartridge.BottomCutLarge:NativeChipCartridge.BottomCut)*scale,bw=(large?NativeChipCartridge.BezelLarge:NativeChipCartridge.Bezel)*scale;
            int W=(int)Math.Round((2*side+middle)*scale),H=(int)Math.Round((top+middle+bottom)*scale);var cv=new Canvas(W,H);
            var outline=NativeChipCartridge.Outline(W,H,tc,bc);float F(Vector2 p)=>Poly(p,outline);var m=MetalOf(l);var body=C(l.Body);var inlay=C(l.Inlay);var gold=l.Metal==NativeChipCartridge.Metal.Gold;
            float outer=bw*.32f,land=bw*.36f,inner=bw*.32f,px=1/scale;
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                var p=new Vector2(x+.5f,y+.5f);float d=F(p);if(d>1)continue;float cov=Cover(d),e=-d;
                // The body under everything, darkening into the frame (the face sits inside the bezel).
                if(e>bw-1){float shade=Clamp01((e-bw)/(5*scale));cv.Over(x,y,Vector3.Lerp(body*.45f,body,shade),1);}
                if(e<bw+1)
                {
                    var g=Grad(F,p);var n=Bevel(e,g,outer,land,inner);float grain=(Hash((float)Math.Floor(e*2.2f/Math.Max(.6f,scale))+l.Rarity%97)*2-1);
                    var c=Shade(n,m,grain);
                    // A crisp highlight on the outer lip and a dark line where the inner facet meets the body.
                    if(e<.9f*scale)c=Vector3.Lerp(c,Vector3.One,.18f*Clamp01(-g.Y-g.X*.4f));
                    cv.Over(x,y,c,cov*Clamp01(bw+.5f-e));
                }
                // The inlay: a lit line in the groove at the bezel's foot, its light falling a little onto the body and the inner facet.
                float gi=Math.Abs(e-(bw+.55f*scale));
                cv.Over(x,y,inlay,Line(gi,Math.Max(.5f,.45f*scale))*.95f);cv.Over(x,y,inlay,.22f*Clamp01(1-gi/(2.6f*scale)));
                if(l.SecondInlay){float g2=Math.Abs(e-outer-land*.5f);cv.Over(x,y,Vector3.Lerp(inlay,Vector3.One,.2f),Line(g2,.3f*scale)*.8f);}
                // Lit seams between the facets (Uncommon and up).
                if(l.Seams){float s1=Math.Abs(e-outer),s2=Math.Abs(e-outer-land);cv.Over(x,y,inlay,Math.Min(1,Line(s1,.2f*scale)+Line(s2,.2f*scale))*.5f);}
            }
            // Screws in the lower corners (all tiers), gems on the four cut corners (Legendary, Mythic).
            foreach(int sx in new[]{0,1})
            {
                var at=new Vector2(sx==0?bc+bw*1.6f:W-bc-bw*1.6f,H-bw*.5f-bc*.35f);Screw(cv,at,bw*.34f,m);
            }
            if(l.Gems)
            {
                var gem=l.Metal==NativeChipCartridge.Metal.Gold?(hi:0xfff0c8,mid:0xffa63d,lo:0x7a2e04):(hi:0xffd6dc,mid:0xff3a52,lo:0x4a0612);
                float rt=Math.Max(2.2f*scale,bw*.62f),rb=Math.Max(1.8f*scale,bw*.5f);
                Gem(cv,new Vector2(tc*.5f+bw*.18f,tc*.5f+bw*.18f),rt,gem.hi,gem.mid,gem.lo,gold);Gem(cv,new Vector2(W-tc*.5f-bw*.18f,tc*.5f+bw*.18f),rt,gem.hi,gem.mid,gem.lo,gold);
                Gem(cv,new Vector2(bc*.5f+bw*.15f,H-bc*.5f-bw*.15f),rb,gem.hi,gem.mid,gem.lo,gold);Gem(cv,new Vector2(W-bc*.5f-bw*.15f,H-bc*.5f-bw*.15f),rb,gem.hi,gem.mid,gem.lo,gold);
            }
            return Done(cv);
        }
        static void Screw(Canvas cv,Vector2 c,float r,in Mat m)
        {
            int x0=(int)(c.X-r-2),x1=(int)(c.X+r+2),y0=(int)(c.Y-r-2),y1=(int)(c.Y+r+2);
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
            {
                var p=new Vector2(x+.5f,y+.5f)-c;float d=p.Length()-r;if(d>1)continue;var g=p.Length()>1e-4f?Vector2.Normalize(p):Vector2.Zero;
                var col=Shade(Vector3.Normalize(new Vector3(-g.X*.6f,-g.Y*.6f,1)),m,0)*.85f;float slot=Math.Abs(p.X-p.Y)/1.414f;if(slot<r*.28f)col*=.35f;
                cv.Over(x,y,col,Cover(d));
            }
        }
        /// <summary>A round brilliant seen from above in a prong setting: the bezel ring, eight facets round a table, a sparkle.</summary>
        static void Gem(Canvas cv,Vector2 c,float r,int hi,int mid,int lo,bool gold)
        {
            var mt=gold?Gold():new Mat{Hi=C(0xffd2d8),Mid=C(0x5a4450),Lo=C(0x16101a),Spec=.9f,Shine=40};
            int x0=(int)(c.X-r-3),x1=(int)(c.X+r+3),y0=(int)(c.Y-r-3),y1=(int)(c.Y+r+3);
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
            {
                var p=new Vector2(x+.5f,y+.5f)-c;float len=p.Length(),d=len-r*1.22f;if(d>1)continue;var g=len>1e-4f?p/len:Vector2.Zero;
                if(len>r){cv.Over(x,y,Shade(Vector3.Normalize(new Vector3(g.X*.8f,g.Y*.8f,1)),mt,0),Cover(d));continue;}
                // Facets: eight sectors tilted outward round a flat table.
                double ang=Math.Atan2(p.Y,p.X);int sector=(int)Math.Floor((ang+Math.PI)/(Math.PI/4))%8;double mid8=-Math.PI+(sector+.5)*Math.PI/4;
                var n=len<r*.45f?Vector3.UnitZ:Vector3.Normalize(new Vector3((float)Math.Cos(mid8)*.7f,(float)Math.Sin(mid8)*.7f,1));
                float lit=Clamp01(.5f+1.1f*(n.X*L.X+n.Y*L.Y)/(1-n.Z*.55f));var col=lit<.5f?Vector3.Lerp(C(lo),C(mid),lit*2):Vector3.Lerp(C(mid),C(hi),(lit-.5f)*2);
                col+=C(hi)*.25f*(1-len/r);cv.Over(x,y,Vector3.Clamp(col,Vector3.Zero,Vector3.One),Cover(len-r));
            }
            // The sparkle: a tiny four-point star on the table.
            for(int k=-2;k<=2;k++){cv.Over((int)(c.X-r*.25f)+k,(int)(c.Y-r*.3f),Vector3.One,.7f-.25f*Math.Abs(k));cv.Over((int)(c.X-r*.25f),(int)(c.Y-r*.3f)+k,Vector3.One,.7f-.25f*Math.Abs(k));}
        }

        // ---- the core: the hexagonal collar, the glass dome and the crystal ----
        /// <summary>The core `px` physical px square (the collar's outer hex fills it, pointy-top).</summary>
        public static Image Core(string tier,int px)
        {
            var l=NativeChipCartridge.Of(tier);var cv=new Canvas(px,px);var c=new Vector2(px/2f,px/2f);float R=px*.5f-1.2f,Rin=R*.72f,Rc=Rin*.8f;
            var outer=NativeChipCartridge.Hex(c,R,true);var window=NativeChipCartridge.Hex(c,Rin,true);var crystal=NativeChipCartridge.Hex(c,Rc,false);var table=NativeChipCartridge.Hex(c,Rc*.46f,false);
            var m=MetalOf(l);var inlay=C(l.Inlay);var lo=C(l.CoreLo);var mid=C(l.Core);var hi=C(l.CoreHi);float bw=R-Rin,s=px/56f;
            bool fractured=l.Fractured,prism=l.Crest==NativeChipCartridge.Crest.Prism,voidCore=l.Metal==NativeChipCartridge.Metal.Void;
            float F(Vector2 p)=>Poly(p,outer);float Wn(Vector2 p)=>Poly(p,window);
            for(int y=0;y<px;y++)for(int x=0;x<px;x++)
            {
                var p=new Vector2(x+.5f,y+.5f);float d=F(p);if(d>2.5f*s){continue;}
                // A soft contact shadow round the collar, so the core sits on the body.
                if(d>0){cv.Over(x,y,Vector3.Zero,.55f*Clamp01(1-d/(2.5f*s)));continue;}
                float dw=Wn(p);
                if(dw>-.5f)
                {   // The collar: outer facet, land, inner facet down into the window (Uncommon and up: lit seams).
                    float e=-d;var g=Grad(F,p);var n=Bevel(e,g,bw*.28f,bw*.3f,bw*.42f);var col=Shade(n,m,Hash((float)Math.Floor(e*1.8f))*2-1);
                    cv.Over(x,y,col,Cover(d));
                    if(l.Seams){float s1=Math.Abs(e-bw*.28f),s2=Math.Abs(e-bw*.58f);cv.Over(x,y,inlay,Math.Min(1,Line(s1,.25f*s)+Line(s2,.25f*s))*.5f);}
                }
                if(dw<.5f)
                {
                    // The window: deep glass over the crystal, vignetted to its rim.
                    float r=(p-c).Length()/Rin;var glass=Vector3.Lerp(C(l.Body)*1.4f,lo,.55f)*(1-.55f*Smooth(.45f,1.05f,r));
                    if(voidCore)glass=C(0x07050d);
                    cv.Over(x,y,glass,Cover(dw));
                    float dc=Poly(p,crystal);
                    if(dc<.6f)
                    {
                        var q=p-c;double ang=Math.Atan2(q.Y,q.X);int sector=(int)Math.Floor((ang+Math.PI)/(Math.PI/3)+.5)%6;double facet=-Math.PI+sector*Math.PI/3;
                        bool onTable=Poly(p,table)<0;var n=onTable?Vector3.UnitZ:Vector3.Normalize(new Vector3((float)Math.Cos(facet)*.95f,(float)Math.Sin(facet)*.95f,1));
                        // Deep facets (the projected icon reads over them): from the deep tone to the core's, the lit ones up to a touch of the bright.
                        float lit=Clamp01(.5f+1.05f*(n.X*L.X+n.Y*L.Y)/(1-n.Z*.55f));var col=lit<.5f?Vector3.Lerp(lo*.8f,Vector3.Lerp(lo,mid,.55f),lit*2):Vector3.Lerp(Vector3.Lerp(lo,mid,.55f),Vector3.Lerp(mid,hi,.35f),(lit-.5f)*2);
                        if(onTable)col=Vector3.Lerp(lo,mid,.38f);
                        if(prism)col=Vector3.Lerp(Spectrum(sector/6f+.08f),Vector3.One,onTable?.35f:.12f*lit);
                        if(voidCore)col=onTable?Vector3.Lerp(C(0x140c2e),C(0xf3efff),Clamp01(1-q.Length()/(Rc*.4f))):Vector3.Lerp(C(0x05040a),C(0x2a1f55),lit);
                        // Lit from within: the table's centre glows toward the bright tone.
                        col+=mid*.16f*(float)Math.Pow(Clamp01(1-q.Length()/Rc),2);
                        // Fractured (Mythic): cracks across the crystal, each a dark line with a bright lip.
                        if(fractured){float k=Crack(q/Rc);col=Vector3.Lerp(col,lo*.4f,Clamp01(1-k/(.035f)))+hi*.55f*Clamp01(1-Math.Abs(k-.05f)/.025f);}
                        // The crystal's facet edges catch a hairline of light.
                        float edge=onTable?-Poly(p,table):-dc;col+=Vector3.One*.22f*Clamp01(.8f-edge/s)*(onTable?1:.6f);
                        cv.Over(x,y,Vector3.Clamp(col,Vector3.Zero,Vector3.One),Cover(dc));
                    }
                    // Holographic glass (Rare and up): a faint iridescent band across the dome.
                    if(l.Holo){float u=((p.X-c.X)*.6f+(p.Y-c.Y))/Rin;cv.Over(x,y,Spectrum(u*.35f+.55f),.13f*Clamp01(1-Math.Abs(u+.15f)/.55f)*Cover(dw));}
                    // The inlay ring at the window's rim.
                    cv.Over(x,y,inlay,Line(Math.Abs(dw+.3f*s),.4f*s)*.9f);
                    // The dome: a curved highlight in its upper left and a rim of the core's light in the lower right.
                    float rr=(p-c).Length()/Rin;var dir=(p-c)/Math.Max(1e-4f,(p-c).Length());
                    float arc=Clamp01(1-Math.Abs(rr-.78f)/.11f)*Clamp01((-dir.X*.6f-dir.Y*.8f-.35f)/.4f);cv.Over(x,y,Vector3.One,.42f*arc*Cover(dw));
                    float rim=Clamp01(1-Math.Abs(rr-.9f)/.1f)*Clamp01((dir.X*.6f+dir.Y*.8f-.2f)/.5f);cv.Over(x,y,Vector3.Lerp(hi,mid,.3f),.35f*rim*Cover(dw));
                }
            }
            // The specular dot on the dome.
            Dot(cv,c+new Vector2(-Rin*.42f,-Rin*.5f),Math.Max(1,Rin*.09f),Vector3.One,.85f);
            return Done(cv);
        }
        /// <summary>Distance to the Mythic crystal's cracks (unit crystal radius).</summary>
        static float Crack(Vector2 q)
        {
            float d=9;
            d=Math.Min(d,Segment(q,new Vector2(-.9f,-.2f),new Vector2(-.3f,.05f)));d=Math.Min(d,Segment(q,new Vector2(-.3f,.05f),new Vector2(.15f,-.4f)));d=Math.Min(d,Segment(q,new Vector2(.15f,-.4f),new Vector2(.6f,-.75f)));
            d=Math.Min(d,Segment(q,new Vector2(-.3f,.05f),new Vector2(.05f,.55f)));d=Math.Min(d,Segment(q,new Vector2(.05f,.55f),new Vector2(.5f,.8f)));d=Math.Min(d,Segment(q,new Vector2(.15f,-.4f),new Vector2(.85f,.1f)));
            return d;
        }
        static Vector3 Spectrum(float u)
        {
            u-=(float)Math.Floor(u);float r=Clamp01(Math.Abs(u*6-3)-1),g=Clamp01(2-Math.Abs(u*6-2)),b=Clamp01(2-Math.Abs(u*6-4));return Vector3.Lerp(new Vector3(r,g,b),Vector3.One,.35f);
        }
        static void Dot(Canvas cv,Vector2 c,float r,Vector3 col,float a)
        {
            for(int y=(int)(c.Y-r-2);y<=(int)(c.Y+r+2);y++)for(int x=(int)(c.X-r-2);x<=(int)(c.X+r+2);x++){float d=(new Vector2(x+.5f,y+.5f)-c).Length();cv.Over(x,y,col,a*Clamp01(1-d/(r+1)));}
        }

        // ---- the traces: etched lines radiating from the core, white (tinted when drawn) ----
        /// <summary>The traces for a core of radius `radius` virtual px at `scale`: the image's left edge is one radius left of the
        /// core's centre, its top one radius above it (TraceOrigin), out to the traces' ends.</summary>
        public static Image Traces(float radius,float scale)
        {
            float k=radius*scale,lw=Math.Max(.9f,.85f*scale);var o=new Vector2(TraceOrigin*k,TraceOrigin*k);
            int W=(int)Math.Ceiling((TraceSpanX-TraceOrigin+.4f)*k),H=(int)Math.Ceiling((TraceSpanY-TraceOrigin+.4f)*k);var cv=new Canvas(W,H);
            // Coverage is the maximum over the segments and the vias (rings at both ends), each rasterised in its own bounds.
            var cover=new float[W*H];
            void Stamp(Vector2 a,Vector2 b,bool via)
            {
                float m=via?3*scale:lw+1;int x0=Math.Max(0,(int)(Math.Min(a.X,b.X)-m)),x1=Math.Min(W-1,(int)(Math.Max(a.X,b.X)+m)),y0=Math.Max(0,(int)(Math.Min(a.Y,b.Y)-m)),y1=Math.Min(H-1,(int)(Math.Max(a.Y,b.Y)+m));
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    var p=new Vector2(x+.5f,y+.5f);float c=via?Clamp01(1.1f*scale+.5f-Math.Abs((p-a).Length()-1.6f*scale))*.9f:Clamp01(lw*.5f+.5f-Segment(p,a,b));
                    int i=y*W+x;if(c>cover[i])cover[i]=c;
                }
            }
            foreach(var t in NativeChipCartridge.Traces){for(int i=1;i<t.Length;i++)Stamp(o+t[i-1]*k,o+t[i]*k,false);var end=o+t[t.Length-1]*k;Stamp(end,end,true);}
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)cv.Over(x,y,Vector3.One,cover[y*W+x]);
            return Done(cv);
        }
        /// <summary>The traces image's extent in core radii (its origin is TraceOrigin radii up and left of the core's centre).</summary>
        public const float TraceOrigin=-1.2f,TraceSpanX=9.8f,TraceSpanY=4.9f;

        // ---- the edge connector's pad (one period: a gold finger and its gap) ----
        public static Image Pad(float scale)
        {
            int W=(int)Math.Round(NativeChipCartridge.PadPitch*scale),H=(int)Math.Round((NativeChipCartridge.ConnectorHeight-1.5f)*scale);var cv=new Canvas(W,H);float pw=NativeChipCartridge.PadWidth*scale,x0=(W-pw)/2;var g=Gold();
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float dx=Math.Max(x0-(x+.5f),x+.5f-(x0+pw)),dy=y+.5f-(H-1.2f*scale),d=Math.Max(dx,dy);
                // Rounded foot: the finger's bottom corners.
                if(dy>-1.2f*scale&&dx>-1.2f*scale){var q=new Vector2(Math.Max(0,dx+1.2f*scale),Math.Max(0,dy+1.2f*scale));d=q.Length()-1.2f*scale;}
                float u=(x+.5f-x0)/pw,v=(y+.5f)/H;var n=Vector3.Normalize(new Vector3((u-.5f)*1.1f,(v-.65f)*.5f,1));var c=Shade(n,g,0);c=Vector3.Lerp(c,Vector3.One,.25f*Clamp01(1-Math.Abs(u-.3f)/.12f));
                cv.Over(x,y,c,Cover(d));
            }
            return Done(cv);
        }

        // ---- the fin (Epic and up): a swept blade off the card's left side (the right is drawn mirrored) ----
        public const float FinWidth=7,FinHeight=40;
        public static Image Fin(string tier,float scale)
        {
            var l=NativeChipCartridge.Of(tier);int W=(int)Math.Round(FinWidth*scale),H=(int)Math.Round(FinHeight*scale);var cv=new Canvas(W,H);float w=W,h=H;
            // Outline: rooted along the right edge (the card's side), swept back to a point at the upper left, a notch below.
            var blade=new[]{new Vector2(w,h*.06f),new Vector2(w*.08f,h*.22f),new Vector2(w*.3f,h*.48f),new Vector2(0,h*.62f),new Vector2(w*.42f,h*.82f),new Vector2(w,h*.94f)};
            var m=MetalOf(l);var inlay=C(l.Inlay);float F(Vector2 p)=>Poly(p,blade);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                var p=new Vector2(x+.5f,y+.5f);float d=F(p);if(d>1)continue;var g=Grad(F,p);float e=-d;
                // A ridge down the blade: the upper half faces up-left, the lower half down-left.
                float ridge=(p.Y-h*.5f)/(h*.5f);var n=e<1.2f*scale?Vector3.Normalize(new Vector3(g.X,g.Y,1)):Vector3.Normalize(new Vector3(-.35f,ridge*.7f,1));
                var c=Shade(n,m,0);cv.Over(x,y,c,Cover(d));
                float edge=Math.Abs(e-.9f*scale);if(g.X<-.2f)cv.Over(x,y,inlay,Line(edge,.4f*scale)*.95f);
            }
            return Done(cv);
        }

        // ---- the ring (Epic and up): a tilted orbit round the core, its back and front halves (white, tinted when drawn) ----
        /// <summary>The ring for a core of radius `radius` virtual px: an ellipse RingX by RingY radii turned RingTilt, the image
        /// centred on the core, (front) the half toward the viewer or (back) the far half.</summary>
        public const float RingX=1.22f,RingY=.36f,RingTilt=-.52f;
        public static Image Ring(float radius,float scale,bool front)
        {
            float k=radius*scale;int S=(int)Math.Ceiling(2*RingX*k+6);var cv=new Canvas(S,S);var c=new Vector2(S/2f,S/2f);float cs=(float)Math.Cos(RingTilt),sn=(float)Math.Sin(RingTilt),lw=Math.Max(1,.9f*scale);
            for(int y=0;y<S;y++)for(int x=0;x<S;x++)
            {
                var p=new Vector2(x+.5f,y+.5f)-c;var q=new Vector2(p.X*cs+p.Y*sn,-p.X*sn+p.Y*cs);
                // Distance to the ellipse, approximated by the scaled radial error; the near half is y > 0 in the ring's frame.
                float rr=(float)Math.Sqrt(q.X*q.X/(RingX*RingX)+q.Y*q.Y/(RingY*RingY)),d=Math.Abs(rr-k)*RingY;bool near=q.Y>0;if(near!=front)continue;
                float depth=front?.75f+.25f*Clamp01(q.Y/(RingY*k)):.35f+.25f*Clamp01(-q.Y/(RingY*k)+.5f);
                cv.Over(x,y,Vector3.One,Clamp01(lw*.5f+.5f-d)*depth);cv.Over(x,y,Vector3.One,.18f*Clamp01(1-d/(3*scale))*depth);
            }
            return Done(cv);
        }

        // ---- the crest: the tier's ornament on the top edge's middle (CrestWidth x CrestHeight; CrestRise above the edge) ----
        public const float CrestWidth=78,CrestHeight=27,CrestRise=13;
        public static bool HasCrest(string tier)=>NativeChipCartridge.Of(tier).Crest!=NativeChipCartridge.Crest.None&&NativeChipCartridge.Of(tier).Crest!=NativeChipCartridge.Crest.Prism;
        public static Image Crest(string tier,float scale)
        {
            var l=NativeChipCartridge.Of(tier);int W=(int)Math.Round(CrestWidth*scale),H=(int)Math.Round(CrestHeight*scale);var cv=new Canvas(W,H);float k=scale*1.3f,cx=W/2f,edge=CrestRise*scale;
            switch(l.Crest)
            {
                case NativeChipCartridge.Crest.Crown:Crown(cv,l,cx,edge,k);break;
                case NativeChipCartridge.Crest.Shards:Shards(cv,l,cx,edge,k);break;
                case NativeChipCartridge.Crest.Bolts:Bolts(cv,l,cx,edge,k);break;
                case NativeChipCartridge.Crest.Knot:Knot(cv,l,cx,edge,k);break;
                case NativeChipCartridge.Crest.Terminal:Terminal(cv,l,cx,edge,k);break;
                case NativeChipCartridge.Crest.Flames:Flames(cv,l,cx,edge,k);break;
            }
            return Done(cv);
        }
        // Fill a field `f` (px; negative inside) in material `m` with facets from its gradient, `outline` dark rim px.
        static void Solid(Canvas cv,Func<Vector2,float> f,in Mat m,float bevel,float rim,int x0=0,int y0=0,int x1=-1,int y1=-1)
        {
            if(x1<0)x1=cv.W-1;if(y1<0)y1=cv.H-1;
            for(int y=Math.Max(0,y0);y<=Math.Min(cv.H-1,y1);y++)for(int x=Math.Max(0,x0);x<=Math.Min(cv.W-1,x1);x++)
            {
                var p=new Vector2(x+.5f,y+.5f);float d=f(p);if(d>1+rim)continue;
                if(rim>0)cv.Over(x,y,Vector3.Zero,.6f*Cover(d-rim));if(d>1)continue;var g=Grad(f,p);float e=-d;
                var n=e<bevel?Vector3.Normalize(new Vector3(g.X,g.Y,1)):Vector3.UnitZ;cv.Over(x,y,Shade(n,m,0),Cover(d));
            }
        }
        static void Crown(Canvas cv,in NativeChipCartridge.Look l,float cx,float edge,float k)
        {
            var g=Gold();
            // A band on the edge, three points (the middle tallest) with balls, and C-scrolls either side; the gem in the middle.
            var band=new[]{new Vector2(cx-22*k,edge+1*k),new Vector2(cx+22*k,edge+1*k),new Vector2(cx+19*k,edge+5*k),new Vector2(cx-19*k,edge+5*k)};
            var mid=new[]{new Vector2(cx-7*k,edge+1.5f*k),new Vector2(cx,edge-7.5f*k),new Vector2(cx+7*k,edge+1.5f*k)};
            var left=new[]{new Vector2(cx-17*k,edge+1.5f*k),new Vector2(cx-12*k,edge-4.5f*k),new Vector2(cx-7*k,edge+1.5f*k)};var right=Mirror(left,cx);
            float F(Vector2 p)
            {
                float d=Math.Min(Poly(p,band),Math.Min(Poly(p,mid),Math.Min(Poly(p,left),Poly(p,right))));
                d=Math.Min(d,(p-new Vector2(cx,edge-8.2f*k)).Length()-1.6f*k);d=Math.Min(d,(p-new Vector2(cx-12*k,edge-5.2f*k)).Length()-1.2f*k);d=Math.Min(d,(p-new Vector2(cx+12*k,edge-5.2f*k)).Length()-1.2f*k);
                // The scrolls: open rings either side of the band.
                foreach(int s in new[]{-1,1}){var q=p-new Vector2(cx+s*25*k,edge+.5f*k);float ring=Math.Abs(q.Length()-3*k)-.8f*k;if(q.Y*1>-1.5f*k||q.X*s>0)d=Math.Min(d,ring);}
                return d;
            }
            Solid(cv,F,g,1.1f*k,.8f*k);
            Gem(cv,new Vector2(cx,edge+.2f*k),2.3f*k,0xfff0c8,0xffa63d,0x7a2e04,true);
        }
        static void Shards(Canvas cv,in NativeChipCartridge.Look l,float cx,float edge,float k)
        {
            // Five crimson crystal shards jutting from the edge, the middle one tallest, each split into a lit and a shaded face.
            float[] at={-16,-8,0,8,16},tall={5,8.5f,11,7.5f,5.5f},lean={-.45f,-.2f,0,.22f,.42f},wide={2.6f,3,3.6f,3,2.6f};
            for(int i=0;i<5;i++)
            {
                var b=new Vector2(cx+at[i]*k,edge+3*k);var dir=Vector2.Normalize(new Vector2(lean[i],-1));var side=new Vector2(-dir.Y,dir.X);var tip=b+dir*tall[i]*k;
                var shape=new[]{b-side*wide[i]*k,tip,b+side*wide[i]*k};int x0=(int)(Math.Min(tip.X,b.X)-6*k),x1=(int)(Math.Max(tip.X,b.X)+6*k);
                for(int y=0;y<cv.H;y++)for(int x=Math.Max(0,x0);x<=Math.Min(cv.W-1,x1);x++)
                {
                    var p=new Vector2(x+.5f,y+.5f);float d=Poly(p,shape);if(d>1.6f*k)continue;if(d>0){cv.Over(x,y,Vector3.Zero,.5f*Cover(d-.8f*k));continue;}
                    bool lit=Vector2.Dot(p-b,side)<0;var col=lit?Vector3.Lerp(C(l.Core),C(l.CoreHi),.45f):Vector3.Lerp(C(l.CoreLo),C(l.Core),.5f);
                    col+=C(l.CoreHi)*.4f*Clamp01(1-(p-tip).Length()/(tall[i]*k*.6f));cv.Over(x,y,Vector3.Clamp(col,Vector3.Zero,Vector3.One),Cover(d));
                }
            }
            var band=new[]{new Vector2(cx-20*k,edge+1.5f*k),new Vector2(cx+20*k,edge+1.5f*k),new Vector2(cx+17*k,edge+5*k),new Vector2(cx-17*k,edge+5*k)};
            Solid(cv,p=>Poly(p,band),MetalOf(l),1*k,.6f*k);
        }
        static void Bolts(Canvas cv,in NativeChipCartridge.Look l,float cx,float edge,float k)
        {
            // Twin lightning bolts rising out of a steel plate like horns, in the Emperor's red.
            var plate=new[]{new Vector2(cx-14*k,edge+.5f*k),new Vector2(cx+14*k,edge+.5f*k),new Vector2(cx+10*k,edge+5*k),new Vector2(cx-10*k,edge+5*k)};
            Solid(cv,p=>Poly(p,plate),MetalOf(l),1.1f*k,.7f*k);
            // The left bolt (tip up and out), its base on the plate; the right one mirrored.
            var bolt=new[]{new Vector2(-2,8),new Vector2(1.5f,8),new Vector2(.2f,2.5f),new Vector2(3,2.5f),new Vector2(-3.5f,-8),new Vector2(-1,-1),new Vector2(-3.6f,-1)};
            var left=new Vector2[bolt.Length];for(int i=0;i<bolt.Length;i++)left[i]=new Vector2(cx-8.5f*k+bolt[i].X*k,edge-3.5f*k+bolt[i].Y*k*.8f);
            Fill(cv,left,C(0xff2448),C(0xffd0d8),.9f*k,.7f*k);Fill(cv,Mirror(left,cx),C(0xff2448),C(0xffd0d8),.9f*k,.7f*k);
        }
        static void Knot(Canvas cv,in NativeChipCartridge.Look l,float cx,float edge,float k)
        {
            // The headband's knot on the edge: a bow (two filled loops with their dark openings), the knot, and two tails
            // trailing off to the right; red cloth with light and dark folds.
            edge-=2.6f*k;var red=new Mat{Hi=C(0xff999d),Mid=C(0xff344f),Lo=C(0x7a0c1e),Spec=.35f,Shine=20};
            float Ellipse(Vector2 p,Vector2 c,float rx,float ry,float turn){var q=p-c;float cs=(float)Math.Cos(turn),sn=(float)Math.Sin(turn);var r=new Vector2(q.X*cs+q.Y*sn,-q.X*sn+q.Y*cs);float n=(float)Math.Sqrt(r.X*r.X/(rx*rx)+r.Y*r.Y/(ry*ry));return (n-1)*Math.Min(rx,ry);}
            var t1=new[]{new Vector2(cx+1*k,edge+1.5f*k),new Vector2(cx+15*k,edge+5*k),new Vector2(cx+23*k,edge+3.5f*k),new Vector2(cx+19*k,edge+7.5f*k),new Vector2(cx+1*k,edge+4.5f*k)};
            var t2=new[]{new Vector2(cx+.5f*k,edge+3*k),new Vector2(cx+9*k,edge+9.5f*k),new Vector2(cx+15*k,edge+10.5f*k),new Vector2(cx+9.5f*k,edge+11.5f*k),new Vector2(cx-.5f*k,edge+5*k)};
            Vector2 lc=new Vector2(cx-5.6f*k,edge-1.2f*k),rc=new Vector2(cx+5.6f*k,edge-1.2f*k);
            float F(Vector2 p)=>Math.Min(Math.Min(Ellipse(p,lc,4.6f*k,2.7f*k,.42f),Ellipse(p,rc,4.6f*k,2.7f*k,-.42f)),Math.Min(Poly(p,t1),Poly(p,t2)));
            Solid(cv,F,red,1.4f*k,.8f*k);
            // The loops' openings, then the knot over everything.
            for(int y=0;y<cv.H;y++)for(int x=0;x<cv.W;x++){var p=new Vector2(x+.5f,y+.5f);float d=Math.Min(Ellipse(p,lc+new Vector2(-.6f*k,.1f*k),2.3f*k,1f*k,.42f),Ellipse(p,rc+new Vector2(.6f*k,.1f*k),2.3f*k,1f*k,-.42f));cv.Over(x,y,C(0x3a0610),Cover(d)*.9f);}
            Solid(cv,p=>Ellipse(p,new Vector2(cx,edge),2.6f*k,3f*k,0),red,1.2f*k,.7f*k);
        }
        static void Terminal(Canvas cv,in NativeChipCartridge.Look l,float cx,float edge,float k)
        {
            // A terminal tab on the edge: a dark chamfered plate with a phosphor-green ">_" prompt and two status ticks.
            var tab=new[]{new Vector2(cx-13*k,edge+3*k),new Vector2(cx-10*k,edge-5*k),new Vector2(cx+10*k,edge-5*k),new Vector2(cx+13*k,edge+3*k)};
            Solid(cv,p=>Poly(p,tab),MetalOf(l),1*k,.7f*k);var green=C(0x5eff91);
            for(int y=0;y<cv.H;y++)for(int x=0;x<cv.W;x++)
            {
                var p=new Vector2(x+.5f,y+.5f);if(Poly(p,tab)>-.8f*k)continue;
                float d=Math.Min(Segment(p,new Vector2(cx-7*k,edge-3*k),new Vector2(cx-3.5f*k,edge-.5f*k)),Segment(p,new Vector2(cx-3.5f*k,edge-.5f*k),new Vector2(cx-7*k,edge+2*k)));
                d=Math.Min(d,Segment(p,new Vector2(cx-1.5f*k,edge+2*k),new Vector2(cx+4*k,edge+2*k)));
                float tick=Math.Min((p-new Vector2(cx+7.5f*k,edge-2.5f*k)).Length(),(p-new Vector2(cx+7.5f*k,edge+.8f*k)).Length())-.9f*k;
                cv.Over(x,y,green,Clamp01(.75f*k+.5f-d));cv.Over(x,y,green,.3f*Clamp01(1-d/(2.4f*k)));cv.Over(x,y,green,Cover(tick)*.85f);
            }
        }
        static void Flames(Canvas cv,in NativeChipCartridge.Look l,float cx,float edge,float k)
        {
            // Black flame tongues licking up from the edge, violet rims, the middle one tallest.
            float[] at={-17,-10,-3.5f,3.5f,10,17},tall={6,9.5f,12,10.5f,8,5.5f};int[] lean={1,-1,1,-1,1,-1};
            for(int i=0;i<6;i++)
            {
                var b=new Vector2(cx+at[i]*k,edge+3.5f*k);var tip=b+new Vector2(lean[i]*2.2f*k,-tall[i]*k);float w0=3.6f*k;
                for(int y=0;y<cv.H;y++)for(int x=(int)(b.X-8*k);x<=(int)(b.X+8*k);x++)
                {
                    if(x<0||x>=cv.W)continue;var p=new Vector2(x+.5f,y+.5f);float v=Clamp01((b.Y-p.Y)/(b.Y-tip.Y));
                    // A tongue: its half-width narrowing to the tip along a bend, rounded at the root.
                    float spine=b.X+(tip.X-b.X)*v*v,half=w0*(1-v)*(float)Math.Sqrt(Math.Max(0,1-v*.3f)),d=Math.Abs(p.X-spine)-half;if(p.Y>b.Y)d=Math.Max(d,(p-b).Length()-w0);if(p.Y<tip.Y)d=(p-tip).Length();
                    if(d>1.2f*k)continue;var rim=C(0xa99fe0);cv.Over(x,y,rim,Cover(d)*.95f);cv.Over(x,y,C(0x030205),Cover(d+.9f*k));
                    cv.Over(x,y,C(0x3b2786),Cover(d+.9f*k)*.35f*v);
                }
            }
        }
        static Vector2[] Mirror(Vector2[] v,float cx){var r=new Vector2[v.Length];for(int i=0;i<v.Length;i++)r[v.Length-1-i]=new Vector2(2*cx-v[i].X,v[i].Y);return r;}
        // A flat, glowing polygon: its fill, a bright core toward its middle and a soft glow round it.
        static void Fill(Canvas cv,Vector2[] shape,Vector3 col,Vector3 hot,float glow,float rim)
        {
            for(int y=0;y<cv.H;y++)for(int x=0;x<cv.W;x++)
            {
                var p=new Vector2(x+.5f,y+.5f);float d=Poly(p,shape);if(d>3*glow+1)continue;
                cv.Over(x,y,col,.35f*Clamp01(1-d/(3*glow)));cv.Over(x,y,Vector3.Zero,.55f*Cover(d-rim)*Clamp01(d));cv.Over(x,y,Vector3.Lerp(col,hot,Clamp01(-d/(1.6f*glow))),Cover(d));
            }
        }

        // ---- the badge: the whole cartridge small (chip lists and tags), BadgeW x BadgeH virtual px: the crest strip on top
        // (Legendary's crown, Mythic's shards, the kits' marks), the body with its bezel, inlay and glowing core, the rank's dots
        // under the core (1-6), and the connector's pads; so the tier reads by shape as well as colour at a glance ----
        public const float BadgeW=15,BadgeH=21;
        public static Image Badge(string tier,float scale)
        {
            var l=NativeChipCartridge.Of(tier);int W=(int)Math.Round(BadgeW*scale),H=(int)Math.Round(BadgeH*scale);var cv=new Canvas(W,H);float k=scale;
            float top=2.6f*k,tc=3.6f*k,bc=1.8f*k,bw=Math.Max(1.2f,1.5f*k),bottom=H-2.6f*k;var outline=NativeChipCartridge.Outline(W,bottom-top,tc,bc);for(int i=0;i<outline.Length;i++)outline[i].Y+=top;float F(Vector2 p)=>Poly(p,outline);
            var m=MetalOf(l);var inlay=C(l.Inlay);var c=new Vector2(W/2f,top+(bottom-top)*.4f);float R=W*.3f;var hex=NativeChipCartridge.Hex(c,R,true);var g=Gold();
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                var p=new Vector2(x+.5f,y+.5f);float d=F(p);
                // The connector's pads under the body.
                if(p.Y>bottom-.5f&&p.Y<H-.4f*k){float u=(p.X-2.4f*k)/(2.2f*k);int i=(int)Math.Floor(u);float f=u-i;if(i>=0&&p.X<W-2.4f*k&&f<.62f)cv.Over(x,y,Shade(Vector3.Normalize(new Vector3(0,(p.Y-bottom)/(2.6f*k)-.4f,1)),g,0),1);}
                if(d>1)continue;float e=-d;
                if(e<bw+.5f){var n=Bevel(e,Grad(F,p),bw*.45f,bw*.2f,bw*.35f);cv.Over(x,y,Shade(n,m,0),Cover(d));}
                else cv.Over(x,y,C(l.Body),1);
                cv.Over(x,y,inlay,Line(Math.Abs(e-bw-.3f*k),.35f*k)*.9f);
                float dh=Poly(p,hex);
                if(dh<.5f){float r=(p-c).Length()/R;var col=Vector3.Lerp(C(l.CoreHi),Vector3.Lerp(C(l.Core),C(l.CoreLo),.35f),Smooth(.1f,.95f,r));if(l.Crest==NativeChipCartridge.Crest.Prism)col=Spectrum((float)Math.Atan2(p.Y-c.Y,p.X-c.X)/6.283f);cv.Over(x,y,col,Cover(dh));}
                else if(dh<1.1f*k)cv.Over(x,y,Shade(Vector3.Normalize(new Vector3(0,0,1)),m,0),Cover(dh-1.1f*k)*.9f);
            }
            // The rank's dots in a row under the core.
            if(l.Rank>0){float pitch=Math.Min(1.9f*k,(W-2*bw-2)/6f),x0=W/2f-pitch*(l.Rank-1)/2,y0=c.Y+R+2.1f*k;for(int i=0;i<l.Rank;i++)Dot(cv,new Vector2(x0+i*pitch,y0),.42f*k,C(l.Inlay),1);}
            // The crest strip: the crown's three points, the shards, the kits' marks, all in the tier's accent.
            if(l.Crest!=NativeChipCartridge.Crest.None)
            {
                var acc=l.Metal==NativeChipCartridge.Metal.Gold?C(0xffd27a):C(l.Accent);int n=l.Crest==NativeChipCartridge.Crest.Flames||l.Crest==NativeChipCartridge.Crest.Shards?5:l.Crest==NativeChipCartridge.Crest.Bolts?2:3;float span=n==2?5.5f*k:7.5f*k;
                for(int i=0;i<n;i++)
                {
                    float x=W/2f-span/2+span*i/Math.Max(1,n-1),hgt=(i==n/2&&n%2==1?2.6f:1.9f)*k;var tri=new[]{new Vector2(x-1.1f*k,top+.6f*k),new Vector2(x,top+.6f*k-hgt),new Vector2(x+1.1f*k,top+.6f*k)};
                    for(int y=0;y<(int)Math.Ceiling(top+k);y++)for(int xx=Math.Max(0,(int)(x-2*k));xx<=Math.Min(W-1,(int)(x+2*k));xx++)cv.Over(xx,y,acc,Cover(Poly(new Vector2(xx+.5f,y+.5f),tri)));
                }
            }
            return Done(cv);
        }
    }
}
