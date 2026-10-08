using System;
namespace ChipArena.UnityConversion
{
    // client/pve/beacons.ts and downed.ts: the revive beacon over every downed party member (a light column with rising
    // motes, a floor ring whose arc fills with the solo self-revive timer, a "F · REVIVE" / "$150" or "SELF-REVIVE 0:04"
    // label) and the bobbing HELP bubble for everyone but the downed player. Pure in time; no UnityEngine here.
    public static class NativeBeaconRules
    {
        // beacons.ts:12 BEACON.
        public const int Down=0xdff4ff,Self=0xffd27a,Segments=48,ColumnSegments=24,MoteWidth=64,MoteHeight=256,HelpWidthPx=256,HelpHeightPx=128;
        public const float Radius=.95f,Height=3.6f;
        // hologram.ts:35-37 HoloLabel text #dffaff, sub #ffffff and glow rgba(95,232,255,.9), each multiplied (linear) by
        // the sprite colour, the beacon colour (beacons.ts:92).
        public const int DownText=0xc3efff,DownSub=0xdff4ff,DownGlow=0x52deff,SelfText=0xdfce7a,SelfSub=0xffd27a,SelfGlow=0x5fbf7a;
        public const float GlowAlpha=.9f;
        // beacons.ts:65,72 label: a 320x96 canvas on a 1.5 x .45 m sprite; text 38 px at .36 of the height, sub 32 px at .76.
        public const float LabelWidth=1.5f,LabelHeight=.45f,LabelText=.45f*38/96,LabelSub=.45f*32/96;
        public const string DownLabel="F · REVIVE",SelfLabel="SELF-REVIVE",HelpText="HELP!";
        // downed.ts:38 DOWNED.help.
        public const float HelpBottom=2.62f,HelpBobAmplitude=.11f,HelpPeriod=1.15f,HelpK=.085f,HelpMin=5,HelpMax=26,HelpAspect=.5f;
        private static double Clamp01(double x)=>Math.Max(0,Math.Min(1,x));

        // beacons.ts:21 progress of the solo self-revive timer, (t - downAt) / (selfReviveAt - downAt).
        public static float Progress(double t,double downAt,double selfAt)=>(float)Clamp01((t-downAt)/Math.Max(1e-6,selfAt-downAt));
        // beacons.ts:16 clock(): whole seconds left, rounded up.
        public static int ClockSeconds(double remaining)=>(int)Math.Max(0,Math.Ceiling(remaining));
        public static string Clock(int seconds){seconds=Math.Max(0,seconds);return (seconds/60).ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+(seconds%60).ToString("00",System.Globalization.CultureInfo.InvariantCulture);}
        // beacons.ts:48 ringDrawCount: the arc segments drawn over the base ring.
        public static int ArcSegments(double progress)=>NativeGoreRules.Round(Clamp01(progress)*Segments);
        // beacons.ts:89-92 column opacity (pulse .5+.5 sin 3.2t, brighter while self-reviving), motes scroll, ring spin, label bob.
        public static float ColumnOpacity(double t,bool self)=>(float)(.32+.18*(.5+.5*Math.Sin(t*3.2))+(self?.2:0));
        public static float MoteScroll(double t){double s=-t*.35;return (float)(s-Math.Floor(s));}
        public static float RingAngle(double t)=>(float)(t*.4);
        public static float LabelY(double t)=>(float)(2.25+.06*Math.Sin(t*2));
        // downed.ts:58-60 and beacons.ts:95-96: width constant on screen between 5 and 26 m, bottom above the label, bob and tilt.
        public static float HelpWidth(float distance)=>HelpK*Math.Max(HelpMin,Math.Min(HelpMax,distance));
        public static float HelpBob(double t,uint idHash)=>(float)(HelpBobAmplitude*Math.Sin((t/HelpPeriod+idHash%997/997.0)*Math.PI*2));
        public static float HelpY(float width,float bob)=>HelpBottom+.06f*width+bob;
        public static float HelpTilt(double t,int idLength)=>(float)(.07*Math.Sin(t*2.3+idLength));

        // Linear-light product of two sRGB colours (three.js Color.multiply after hex decoding), back to sRGB hex.
        public static int MultiplyLinear(int a,int b)
        {
            int result=0;for(int shift=16;shift>=0;shift-=8){double x=Linear((a>>shift)&255)*Linear((b>>shift)&255);result|=Encode(x)<<shift;}return result;
        }
        private static double Linear(int c){double v=c/255.0;return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);}
        private static double Gamma(double l){l=Clamp01(l);return l<=.0031308?l*12.92:1.055*Math.Pow(l,1/2.4)-.055;}
        private static int Encode(double l)=>(int)Math.Round(Gamma(l)*255);
        // A beacon colour at a brightness level for a vertex colour: three multiplies vertex level and material colour in
        // linear light, so the level scales the linear colour; a gamma (sRGB) target gets it re-encoded (Unity does not
        // convert vertex colours).
        public static void Shade(int hex,float level,bool linear,out float r,out float g,out float b)
        {
            double R=Linear(hex>>16&255)*level,G=Linear(hex>>8&255)*level,B=Linear(hex&255)*level;
            if(linear){r=(float)R;g=(float)G;b=(float)B;}else{r=(float)Gamma(R);g=(float)Gamma(G);b=(float)Gamma(B);}
        }

        // Geometry is written through a sink (the runtime fills reused lists; checks collect arrays). uv.z = 1 marks an
        // untextured vertex (the ring), 0 a vertex that samples the motes map.
        public interface IMeshSink {int Vertex(float x,float y,float z,float u,float v,float flat,float r,float g,float b,float a);void Triangle(int a,int b,int c);}
        // beacons.ts:70-71 the column: an open CylinderGeometry(.665, .8075, 3.6, 24) standing on the floor. three's
        // vertex order (top row, bottom row; u = x/24, v = 1 at the top) and faces (a,b,d), (b,c,d). xSign -1 mirrors into
        // Unity space.
        public static void Column(IMeshSink sink,float cx,float cz,float xSign,int color,bool linear,float alpha)
        {
            float top=Radius*.7f,bottom=Radius*.85f;int first=-1;Shade(color,1,linear,out float r,out float g,out float b);
            for(int y=0;y<=1;y++)for(int x=0;x<=ColumnSegments;x++)
            {
                float u=x/(float)ColumnSegments,radius=y*(bottom-top)+top;double theta=u*Math.PI*2;
                int n=sink.Vertex(cx+xSign*radius*(float)Math.Sin(theta),y==0?Height:0,cz+radius*(float)Math.Cos(theta),u,1-y,0,r,g,b,alpha);if(first<0)first=n;
            }
            for(int x=0;x<ColumnSegments;x++){int a=first+x,bb=first+ColumnSegments+1+x,c=bb+1,d=a+1;sink.Triangle(a,bb,d);sink.Triangle(bb,c,d);}
        }
        // beacons.ts:41-48,71,91 the floor ring at y .03 turning .4 rad/s: the full dim base ring (r .89-.95, level .35) and
        // the bright progress arc (r 1.0-1.11, level 1) over its first ArcSegments(progress) segments.
        public static void Ring(IMeshSink sink,float cx,float cz,float xSign,double t,double progress,int color,bool linear)
        {
            double spin=RingAngle(t);Band(sink,cx,cz,xSign,spin,Radius-.06f,Radius,Segments,.35f,color,linear);Band(sink,cx,cz,xSign,spin,Radius+.05f,Radius+.16f,ArcSegments(progress),1,color,linear);
        }
        private static void Band(IMeshSink sink,float cx,float cz,float xSign,double spin,float r0,float r1,int count,float level,int color,bool linear)
        {
            Shade(color,level,linear,out float r,out float g,out float b);
            for(int i=0;i<count;i++)
            {
                double a0=i/(double)Segments*Math.PI*2+spin,a1=(i+1)/(double)Segments*Math.PI*2+spin;
                int V(float radius,double a)=>sink.Vertex(cx+xSign*radius*(float)Math.Sin(a),.03f,cz+radius*(float)Math.Cos(a),0,0,1,r,g,b,1);
                int p=V(r0,a0),q=V(r1,a0),s=V(r1,a1),w=V(r0,a1);sink.Triangle(p,q,s);sink.Triangle(p,s,w);
            }
        }

        // beacons.ts:32-39 the motes map, 64x256 RGBA: white with a vertical alpha gradient (0 at the top, .18 at 60%, .55 at
        // the bottom) and 26 seeded white flecks (alpha .5-1) composited over it. Rows top-down unless bottomUp (Unity).
        public static void Motes(byte[] rgba,bool bottomUp)
        {
            int W=MoteWidth,H=MoteHeight;var alpha=new double[W*H];
            for(int y=0;y<H;y++){double t=(y+.5)/H,a=t<.6?.18*t/.6:.18+.37*(t-.6)/.4;for(int x=0;x<W;x++)alpha[y*W+x]=a;}
            uint s=7;double R(){unchecked{s=s*1664525u+1013904223u;}return s/4294967296.0;}
            for(int i=0;i<26;i++)
            {
                double x0=R()*64,y0=R()*256,w=1.5+R()*2.5,h=w*1.6,ga=.5+R()*.5;
                for(int y=Math.Max(0,(int)Math.Floor(y0));y<Math.Min(H,(int)Math.Ceiling(y0+h));y++)
                {double cy=Math.Max(0,Math.Min(y+1,y0+h)-Math.Max(y,y0));for(int x=Math.Max(0,(int)Math.Floor(x0));x<Math.Min(W,(int)Math.Ceiling(x0+w));x++){double cx=Math.Max(0,Math.Min(x+1,x0+w)-Math.Max(x,x0)),sa=cx*cy*ga;int k=y*W+x;alpha[k]=sa+alpha[k]*(1-sa);}}
            }
            for(int y=0;y<H;y++)for(int x=0;x<W;x++){int o=((bottomUp?H-1-y:y)*W+x)*4;rgba[o]=rgba[o+1]=rgba[o+2]=255;rgba[o+3]=(byte)Math.Round(alpha[y*W+x]*255);}
        }

        // beacons.ts:52-62 the HELP bubble, 256x128 RGBA without its lettering (drawn as text by the HUD): a rounded rect
        // (10,8,236,84; r 26) with a tail to (128,120), filled #fff8ee over a rgba(0,0,0,.45) shadow (blur 8, y +3) and
        // stroked 7 px #ff3346 with round joins. Rows top-down unless bottomUp.
        public static void HelpBubble(byte[] rgba,bool bottomUp)
        {
            int W=HelpWidthPx,H=HelpHeightPx;var fill=new double[W*H];var stroke=new double[W*H];
            for(int y=0;y<H;y++)for(int x=0;x<W;x++){double px=x+.5,py=y+.5,d=BubbleDistance(px,py);bool inside=BubbleInside(px,py);fill[y*W+x]=Clamp01(.5+(inside?d:-d));stroke[y*W+x]=Clamp01(4-d);}
            // Canvas shadows blur with a Gaussian of sigma = shadowBlur / 2.
            const double sigma=4;const int radius=12;var kernel=new double[radius*2+1];double sum=0;for(int i=-radius;i<=radius;i++){kernel[i+radius]=Math.Exp(-i*i/(2*sigma*sigma));sum+=kernel[i+radius];}for(int i=0;i<kernel.Length;i++)kernel[i]/=sum;
            var temp=new double[W*H];var shadow=new double[W*H];
            for(int y=0;y<H;y++)for(int x=0;x<W;x++){double v=0;for(int i=-radius;i<=radius;i++){int sx=x+i;if(sx>=0&&sx<W)v+=fill[y*W+sx]*kernel[i+radius];}temp[y*W+x]=v;}
            for(int y=0;y<H;y++)for(int x=0;x<W;x++){double v=0;for(int i=-radius;i<=radius;i++){int sy=y-3+i;if(sy>=0&&sy<H)v+=temp[sy*W+x]*kernel[i+radius];}shadow[y*W+x]=.45*v;}
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                int k=y*W+x;double a=shadow[k],r=0,g=0,b=0;   // premultiplied, black shadow
                void Over(double sr,double sg,double sb,double sa){r=sr*sa+r*(1-sa);g=sg*sa+g*(1-sa);b=sb*sa+b*(1-sa);a=sa+a*(1-sa);}
                Over(1,248/255.0,238/255.0,fill[k]);Over(1,.2,70/255.0,stroke[k]);
                int o=((bottomUp?H-1-y:y)*W+x)*4;rgba[o+3]=(byte)Math.Round(a*255);
                rgba[o]=(byte)(a>0?Math.Round(Math.Min(1,r/a)*255):0);rgba[o+1]=(byte)(a>0?Math.Round(Math.Min(1,g/a)*255):0);rgba[o+2]=(byte)(a>0?Math.Round(Math.Min(1,b/a)*255):0);
            }
        }
        private static bool BubbleInside(double px,double py)
        {
            double qx=Math.Abs(px-128)-(118-26),qy=Math.Abs(py-50)-(42-26),outside=Math.Sqrt(Math.Max(qx,0)*Math.Max(qx,0)+Math.Max(qy,0)*Math.Max(qy,0))+Math.Min(Math.Max(qx,qy),0)-26;
            if(outside<0)return true;
            // Tail triangle (112,92) (142,92) (128,120).
            double E(double ax,double ay,double bx,double by)=>(bx-ax)*(py-ay)-(by-ay)*(px-ax);
            double e0=E(112,92,142,92),e1=E(142,92,128,120),e2=E(128,120,112,92);return e0>=0&&e1>=0&&e2>=0||e0<=0&&e1<=0&&e2<=0;
        }
        // Unsigned distance to the bubble's outline (the path of beacons.ts:55, tail included, no edge across the tail's mouth).
        private static double BubbleDistance(double px,double py)
        {
            double Seg(double ax,double ay,double bx,double by){double dx=bx-ax,dy=by-ay,t=Math.Max(0,Math.Min(1,((px-ax)*dx+(py-ay)*dy)/(dx*dx+dy*dy)));double ex=px-ax-t*dx,ey=py-ay-t*dy;return Math.Sqrt(ex*ex+ey*ey);}
            double Arc(double cx,double cy,double sx,double sy){double dx=px-cx,dy=py-cy;if(dx*sx>=0&&dy*sy>=0)return Math.Abs(Math.Sqrt(dx*dx+dy*dy)-26);return double.MaxValue;}
            double d=Seg(36,8,220,8);d=Math.Min(d,Seg(246,34,246,66));d=Math.Min(d,Seg(220,92,142,92));d=Math.Min(d,Seg(142,92,128,120));d=Math.Min(d,Seg(128,120,112,92));
            d=Math.Min(d,Seg(112,92,36,92));d=Math.Min(d,Seg(10,66,10,34));
            d=Math.Min(d,Arc(220,34,1,-1));d=Math.Min(d,Arc(220,66,1,1));d=Math.Min(d,Arc(36,66,-1,1));d=Math.Min(d,Arc(36,34,-1,-1));return d;
        }
    }
}
