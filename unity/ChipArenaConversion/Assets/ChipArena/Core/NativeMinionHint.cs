using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The PvE "Find me..." possession hints of client/siren/minions/hints.ts and the trail of client/pve/horde-view.ts at
    /// 05950ea (design D13 and §8.3), as pure functions of the replicated possession (NativePveAdd.Possessed and PossessedAt,
    /// which host, guests and killcam replays all carry since NativeMinionFrame.possessedAt) and the presentation clock, so
    /// every client shows the same hint for the same clock (Runtime/NativeMinionFxPresentation.cs draws them):
    ///  - <see cref="Level"/>: 1 for the first 15 s of possession, 2 to 35 s, then 3, and 0 before the start (a guest's clock
    ///    trails the frame that carried it). A frame from a host older than the replicated start (PossessedAt 0, the field's
    ///    default) keeps level 1, the hint that host's guests drew before;
    ///  - level 1: the crown seam and the violet halo pulse every ~4 s (NativeMinionFx.Hint and HintPulse);
    ///  - level 2 adds the floating translucent Siren grin (the boss card's grin, never its eyes) over the head within 25 m
    ///    (<see cref="GlyphShown"/>, <see cref="GlyphOpacity"/>, <see cref="GlyphLift"/>, the mesh in <see cref="Glyph"/>), and
    ///    horde-view.ts's black-flame trail behind the add while it walks (<see cref="Trail"/>: showRig emits it from level 2,
    ///    `level>=2`, as hints.ts's header lists it; the handoff notes that called it a level 3 effect do not match the source);
    ///  - level 3 brightens the seam, the halo and the grin by 1.35 (<see cref="Brightness"/>).
    /// The trail drips gore.ts's stump flame into the shared world flame pool on slots counted from the possession start, so
    /// any frame rate and every client emit the same flames along the same walk (their jitter is hashed per slot). Source
    /// frame (the runtime mirrors x). Allocation free after the glyph mesh is built once.
    /// </summary>
    public static class NativeMinionHint
    {
        /// <summary>hints.ts HINT_GLYPH_RANGE and HINT_LEVELS, and level 3's brightness.</summary>
        public const double GlyphRange=25,Level2At=15,Level3At=35,Boost=1.35;
        /// <summary>hints.ts hintLevel: seconds since possessedAt to a level (0 before it, or for NaN).</summary>
        public static int Level(double age)=>!(age>=0)?0:age<Level2At?1:age<Level3At?2:3;
        /// <summary>The level an add shows at clock t: 0 unpossessed, 1 without a replicated start (an older host's frame).</summary>
        public static int Level(bool possessed,double possessedAt,double t)=>!possessed?0:possessedAt>0?Level(t-possessedAt):1;
        /// <summary>possessHint's boost: 1.35 at level 3, applied to the seam, the halo and the grin (capped at opacity 1).</summary>
        public static double Brightness(int level)=>level>=3?Boost:1;
        /// <summary>The grin shows from level 2 while the camera is within 25 m of (x, floor + 1, z) (horde-view.ts dist).</summary>
        public static bool GlyphShown(int level,double distance)=>level>=2&&distance<=GlyphRange;
        /// <summary>The grin's opacity at clock t: .68 + .12·sin(2.3t), times the level's brightness, capped at 1.</summary>
        public static double GlyphOpacity(int level,double t)=>Math.Min(1,(.68+.12*Math.Sin(t*2.3))*Brightness(level));
        /// <summary>How far over the head joint the grin floats at clock t (upright in the rig group, whatever the head does).</summary>
        public static double GlyphLift(double t)=>.6+.05*Math.Sin(t*1.7);

        // ---- the trail (horde-view.ts showRig, gore.ts drip) ---------------------------------------------------------
        /// <summary>The trail's gate and flame: from TrailLevel, faster than .2 m/s, .25 m over the floor, at gore.drip(dt·2):
        /// twice the 6 a second stump rate, times max(.3, effects quality).</summary>
        public const int TrailLevel=2;public const double TrailSpeed=.2,TrailHeight=.25,TrailRate=2*NativeGoreFlames.DripRate;
        /// <summary>A presented span longer than this is a rewind or a jump and emits nothing (the project's stateful-layer rule).</summary>
        public const double MaxStep=.35;
        public static double TrailPerSecond(double quality)=>TrailRate*Math.Max(.3,quality);
        public static bool Walking(double vx,double vz)=>vx*vx+vz*vz>TrailSpeed*TrailSpeed;
        /// <summary>The last trail slot due by clock t: slot n falls at possessedAt + 15 s + n / TrailPerSecond (0 before the first).</summary>
        public static long TrailSlot(double possessedAt,double t,double quality){double k=(t-possessedAt-Level2At)*TrailPerSecond(quality);return k>=1?(long)Math.Floor(k):0;}
        public static double SlotTime(double possessedAt,long slot,double quality)=>possessedAt+Level2At+slot/TrailPerSecond(quality);
        /// <summary>
        /// The trail flames of one presented span (from, to]: the add stood at (x0, z0) at `from` and at (x1, z1) at `to`, on a
        /// floor at `floor`, moving at (vx, vz). Each slot in the span drips one gore.ts stump flame (falling .4 m/s, .5 s, .06 m,
        /// black, buoyancy 1.4, drag 2.2) where the add was at the slot's time, its jitter drawn from `random` reseeded for the add
        /// and the slot, so every client and frame rate emits the same flames along the same walk. Nothing below TrailLevel,
        /// standing, without a replicated start, or over a span past MaxStep. Returns the flames emitted.
        /// </summary>
        public static int Trail(NativeFlameField flames,SlotRandom random,int id,double possessedAt,double from,double to,double x0,double z0,double x1,double z1,double floor,double vx,double vz,double quality)
        {
            if(flames==null||random==null||!(to>from)||to-from>MaxStep||!Walking(vx,vz)||Level(true,possessedAt,to)<TrailLevel)return 0;
            long a=TrailSlot(possessedAt,from,quality),b=TrailSlot(possessedAt,to,quality);if(b<=a)return 0;
            var keep=flames.Random;flames.Random=random.Next;uint seed=SlotSeed(id);int n=0;
            try
            {
                for(long s=a+1;s<=b;s++)
                {
                    double k=(SlotTime(possessedAt,s,quality)-from)/(to-from);k=k<0?0:k>1?1:k;random.Reset(seed,s);
                    flames.Emit(x0+(x1-x0)*k,floor+TrailHeight,z0+(z1-z0)*k,0,-.4,0,.5,.06,NativeFlameTone.Black,1.4,2.2);n++;
                }
            }
            finally{flames.Random=keep;}
            return n;
        }
        /// <summary>The add's trail seed (its look seed, salted).</summary>
        public static uint SlotSeed(int id)=>NativeMinionFx.Seed(id)^0x7472u;
        /// <summary>A trail slot's random stream: the add's seed, the slot and the draw hashed, as the source's Math.random is
        /// used by FlameField.emit (rotation, spin, tone), so a slot's flame looks the same on every client.</summary>
        public sealed class SlotRandom
        {
            private uint seed;private int slot,draw;
            /// <summary>The stream as FlameField's Random (one delegate, made once).</summary>
            public readonly Func<double> Next;
            public SlotRandom(){Next=Draw;}
            public void Reset(uint s,long n){seed=s;slot=unchecked((int)n);draw=0;}
            private double Draw()=>NativeGoreRules.Hash01(seed,slot,draw++,0x74);
        }

        // ---- the grin (hints.ts glyph) -------------------------------------------------------------------------------
        /// <summary>
        /// hints.ts glyph(): the Siren card's grin path and interlocking teeth (siren-card.ts GRIN, no eyes) drawn into a 256 x 96
        /// canvas at (x + 28, y - 48), shown as a .78 x .29 m sprite. Rebuilt here as a camera-facing mesh in that sprite's
        /// plane (metres, +x right, +y up, the sprite's centre at 0), its pieces drawn in the canvas's order:
        ///  1. the mouth's violet shadow (shadowBlur 22, a Gaussian of sigma 11 px, filled twice): a vertex-alpha grid;
        ///  2. the dark mouth rgba(28,14,52,.9) filled twice, its colour carrying the second shadow beneath the second fill;
        ///  3. the lips' violet shadow (shadowBlur 8, sigma 4 px): a finer vertex-alpha grid, then 4. the pale lips (#ded4ff,
        ///     4.5 px);
        ///  5. the teeth's violet shadow, over the lips' inner edge as on the canvas, then 6. the white teeth (4 px).
        /// The strokes are miter-joined strips (miter limit 10) that never overlap themselves, so the translucent grin shows no
        /// double-blended seams (the lips' two sharp corners keep the canvas's outer spike and close their inner side at the
        /// offset edges' crossing). Chromium's canvas blurs shadowBlur b with a sigma of about b / 2 (measured 4.1 and 10.7).
        /// The shadows come from the canvas shapes rasterised (NativeSirenSmile.Render) and blurred once. A stroke can widen to
        /// keep a pixel of coverage far away (<see cref="StrokeScale"/>: vertex = centre + offset · scale, alpha / scale).
        /// </summary>
        public static class Glyph
        {
            public const double Width=.78,Height=.29;public const int CanvasWidth=256,CanvasHeight=96;public const double ShiftX=28,ShiftY=-48;
            public const int MouthRgb=0x1c0e34,LipRgb=0xded4ff,TeethRgb=0xffffff;
            public const double MouthAlpha=.9,LipWidth=4.5,TeethWidth=4,MouthBlur=22,StrokeBlur=8,MiterLimit=10;
            /// <summary>A stroke keeps at least this many screen pixels of width, widening at most MaxWiden times.</summary>
            public const double MinPixels=1,MaxWiden=2;
            /// <summary>Piece order (the draw order).</summary>
            public const int MouthGlow=0,Mouth=1,LipGlow=2,Lips=3,TeethGlow=4,Teeth=5;
            /// <summary>A piece: its vertices [Start, Start + Count), its indices [First, First + Indices) into <see cref="Index"/>
            /// (absolute vertex numbers), whether it is a soft glow, and a stroke's width in metres (0 otherwise).</summary>
            public struct Piece{public int Start,Count,First,Indices;public bool Glow;public double Stroke;}
            /// <summary>Per vertex: centre (m), a stroke's offset at its own width (m), sRGB colour and straight alpha.</summary>
            public static readonly float[] X,Y,OffsetX,OffsetY,R,G,B,A;public static readonly int[] Index;public static readonly Piece[] Pieces;
            /// <summary>The scale a stroke `stroke` m wide takes where a screen pixel spans `pixel` m (1 up close).</summary>
            public static double StrokeScale(double stroke,double pixel)=>stroke<=0||!(pixel>0)?1:Math.Max(1,Math.Min(MaxWiden,MinPixels*pixel/stroke));
            /// <summary>Builds the mesh if it is not built yet (the static constructor; pure managed work, safe off the main thread).</summary>
            public static void Warm(){}
            /// <summary>Canvas path units to glyph metres (the sprite's centre at canvas 128, 48).</summary>
            public static double MetresX(double x)=>(x+ShiftX-CanvasWidth/2.0)*Width/CanvasWidth;
            public static double MetresY(double y)=>(CanvasHeight/2.0-(y+ShiftY))*Height/CanvasHeight;

            // The shadow raster, in path units (canvas pixels): wide enough for three sigma of the mouth's blur all round.
            private const int RX0=-24,RY0=24,RW=248,RH=128;
            static Glyph()
            {
                var line=NativeSirenSmile.Grin[0];var teeth=NativeSirenSmile.TeethLine[0];
                var closed=new float[line.Length+4];Array.Copy(line,closed,line.Length);closed[line.Length]=line[0];closed[line.Length+1]=line[1];closed[line.Length+2]=line[2];closed[line.Length+3]=line[3];
                float[] mouth=Cover(NativeSirenSmile.Grin),lips=Cover(NativeSirenSmile.MiterStroke(closed,(float)LipWidth,(float)MiterLimit)),bite=Cover(NativeSirenSmile.MiterStroke(teeth,(float)TeethWidth,(float)MiterLimit));
                Blur(mouth,MouthBlur/2);Blur(lips,StrokeBlur/2);Blur(bite,StrokeBlur/2);
                var under=new float[RW*RH];for(int i=0;i<under.Length;i++){double s=MouthAlpha*mouth[i];under[i]=(float)(1-(1-s)*(1-s));}
                var b=new Builder();
                b.Grid(under,-14,214,34,142,6);
                b.Mouth(line,mouth);
                b.Grid(lips,7,193,57,121,3);
                b.Stroke(line,true,LipWidth,LipRgb);
                b.Grid(bite,14,185,57,117,3);
                b.Stroke(teeth,false,TeethWidth,TeethRgb);
                X=b.X.ToArray();Y=b.Y.ToArray();OffsetX=b.OX.ToArray();OffsetY=b.OY.ToArray();R=b.R.ToArray();G=b.G.ToArray();B=b.B.ToArray();A=b.A.ToArray();Index=b.I.ToArray();Pieces=b.P.ToArray();
            }
            // Coverage of path polygons (nonzero) on the raster, row 0 at path y = RY0.
            private static float[] Cover(List<float[]> paths)
            {
                var rgba=new byte[RW*RH*4];NativeSirenSmile.Render(rgba,RW,RH,new[]{NativeSirenSmile.Paint(paths,0xffffff,1,1,-RX0,1,-RY0)},4,0);
                var c=new float[RW*RH];for(int y=0;y<RH;y++)for(int x=0;x<RW;x++)c[y*RW+x]=rgba[((RH-1-y)*RW+x)*4+3]/255f;return c;
            }
            // Separable Gaussian (the canvas shadow: sigma = shadowBlur / 2), clear beyond the raster.
            private static void Blur(float[] f,double sigma)
            {
                int r=(int)Math.Ceiling(3*sigma);var k=new double[2*r+1];double sum=0;for(int i=-r;i<=r;i++)sum+=k[i+r]=Math.Exp(-i*i/(2*sigma*sigma));for(int i=0;i<k.Length;i++)k[i]/=sum;
                var t=new float[f.Length];
                for(int y=0;y<RH;y++)for(int x=0;x<RW;x++){double v=0;for(int i=-r;i<=r;i++){int u=x+i;if(u>=0&&u<RW)v+=k[i+r]*f[y*RW+u];}t[y*RW+x]=(float)v;}
                for(int y=0;y<RH;y++)for(int x=0;x<RW;x++){double v=0;for(int i=-r;i<=r;i++){int u=y+i;if(u>=0&&u<RH)v+=k[i+r]*t[u*RW+x];}f[y*RW+x]=(float)v;}
            }
            // Bilinear between pixel centres, clear outside.
            private static double Sample(float[] f,double x,double y)
            {
                double u=x-RX0-.5,v=y-RY0-.5;int i=(int)Math.Floor(u),j=(int)Math.Floor(v);double fu=u-i,fv=v-j;
                double At(int a,int c)=>a<0||c<0||a>=RW||c>=RH?0:f[c*RW+a];
                return At(i,j)*(1-fu)*(1-fv)+At(i+1,j)*fu*(1-fv)+At(i,j+1)*(1-fu)*fv+At(i+1,j+1)*fu*fv;
            }
            private sealed class Builder
            {
                public readonly List<float> X=new List<float>(),Y=new List<float>(),OX=new List<float>(),OY=new List<float>(),R=new List<float>(),G=new List<float>(),B=new List<float>(),A=new List<float>();
                public readonly List<int> I=new List<int>();public readonly List<Piece> P=new List<Piece>();private int start,first;
                private void Begin(){start=X.Count;first=I.Count;}
                private void End(bool glow,double stroke)=>P.Add(new Piece{Start=start,Count=X.Count-start,First=first,Indices=I.Count-first,Glow=glow,Stroke=stroke});
                private int V(double x,double y,double ox,double oy,int rgb,double a){X.Add((float)MetresX(x));Y.Add((float)MetresY(y));OX.Add((float)(ox*Width/CanvasWidth));OY.Add((float)(-oy*Height/CanvasHeight));R.Add((rgb>>16&255)/255f);G.Add((rgb>>8&255)/255f);B.Add((rgb&255)/255f);A.Add((float)Math.Max(0,Math.Min(1,a)));return X.Count-1;}
                private int V(double x,double y,double r,double g,double b,double a){int v=V(x,y,0,0,0,a);R[v]=(float)r;G[v]=(float)g;B[v]=(float)b;return v;}
                private void T(int a,int b,int c){I.Add(a);I.Add(b);I.Add(c);}
                // A violet vertex-alpha grid over the field, keeping only the cells with any glow.
                public void Grid(float[] field,int x0,int x1,int y0,int y1,int step)
                {
                    Begin();int nx=(x1-x0)/step+1,ny=(y1-y0)/step+1;var alpha=new double[nx*ny];var id=new int[nx*ny];
                    for(int j=0;j<ny;j++)for(int i=0;i<nx;i++){alpha[j*nx+i]=Sample(field,x0+i*step,y0+j*step);id[j*nx+i]=-1;}
                    for(int j=0;j+1<ny;j++)for(int i=0;i+1<nx;i++)
                    {
                        int a=j*nx+i,b=a+1,c=a+nx,d=c+1;if(Math.Max(Math.Max(alpha[a],alpha[b]),Math.Max(alpha[c],alpha[d]))<1/512.0)continue;
                        foreach(int k in new[]{a,b,c,d})if(id[k]<0)id[k]=V(x0+(k%nx)*step,y0+(k/nx)*step,0,0,NativeMinionFx.Violet,alpha[k]);
                        T(id[a],id[b],id[d]);T(id[a],id[d],id[c]);
                    }
                    End(true,0);
                }
                // The crescent between the top curve (path points 0..64) and the bottom one (128..64 backwards), each vertex coloured
                // as the canvas stacks shadow, fill, shadow, fill there (source-over, straight sRGB).
                public void Mouth(float[] line,float[] blurred)
                {
                    Begin();int n=line.Length/2,half=n/2;var top=new int[half+1];var bottom=new int[half+1];
                    for(int i=0;i<=half;i++){top[i]=Fill(line[i*2],line[i*2+1],blurred);int k=(n-i)%n;bottom[i]=i==0||i==half?top[i]:Fill(line[k*2],line[k*2+1],blurred);}
                    for(int i=0;i<half;i++){if(top[i]!=bottom[i])T(top[i],top[i+1],bottom[i]);if(top[i+1]!=bottom[i+1])T(top[i+1],bottom[i+1],bottom[i]);}
                    End(false,0);
                }
                private int Fill(double x,double y,float[] blurred)
                {
                    double s=MouthAlpha*Sample(blurred,x,y),cr=0,cg=0,cb=0,ca=0;
                    void Over(int rgb,double a){cr=(rgb>>16&255)/255.0*a+cr*(1-a);cg=(rgb>>8&255)/255.0*a+cg*(1-a);cb=(rgb&255)/255.0*a+cb*(1-a);ca=a+ca*(1-a);}
                    Over(NativeMinionFx.Violet,s);Over(MouthRgb,MouthAlpha);Over(NativeMinionFx.Violet,s);Over(MouthRgb,MouthAlpha);
                    return V(x,y,cr/ca,cg/ca,cb/ca,ca);
                }
                // A miter-joined strip `width` wide along the polyline (butt ends when open). At a sharp corner of a closed line the
                // inner offsets that would fold back past the other edge's offset (tested at MaxWiden) close on the corner's miter point.
                public void Stroke(float[] line,bool closed,double width,int rgb)
                {
                    Begin();int n=line.Length/2;double h=width/2;var ox=new double[n];var oy=new double[n];var cross=new double[n];
                    double Px(int i)=>line[((i%n+n)%n)*2];double Py(int i)=>line[((i%n+n)%n)*2+1];
                    for(int i=0;i<n;i++)
                    {
                        bool head=!closed&&i==0,tail=!closed&&i==n-1;
                        double ax=head?Px(1)-Px(0):Px(i)-Px(i-1),ay=head?Py(1)-Py(0):Py(i)-Py(i-1),bx=tail?ax:Px(i+1)-Px(i),by=tail?ay:Py(i+1)-Py(i);
                        double la=Math.Sqrt(ax*ax+ay*ay),lb=Math.Sqrt(bx*bx+by*by);ax/=la;ay/=la;bx/=lb;by/=lb;
                        double mx=-ay-by,my=ax+bx,ml=Math.Sqrt(mx*mx+my*my);if(ml<1e-9){mx=-ay;my=ax;ml=1;}mx/=ml;my/=ml;double cos=mx*-ay+my*ax,len=h/Math.Max(cos,1/MiterLimit);
                        ox[i]=mx*len;oy[i]=my*len;cross[i]=ax*by-ay*bx;
                    }
                    // Left (+offset) and right (-offset) vertices; a closed line's sharp corners snap their folding inner vertices.
                    var left=new int[n];var right=new int[n];var snap=new int[n];for(int i=0;i<n;i++)snap[i]=-1;
                    if(closed)for(int c=0;c<n;c++)
                    {
                        double ax=Px(c)-Px(c-1),ay=Py(c)-Py(c-1),bx=Px(c+1)-Px(c),by=Py(c+1)-Py(c),la=Math.Sqrt(ax*ax+ay*ay),lb=Math.Sqrt(bx*bx+by*by);
                        if((ax*bx+ay*by)/(la*lb)>0)continue;double sign=cross[c]>0?1:-1;
                        // tA: the outgoing edge, tB: the incoming edge reversed; each one's inward normal toward the other.
                        double tax=bx/lb,tay=by/lb,tbx=-ax/la,tby=-ay/la,dot=tax*tbx+tay*tby;
                        double nbx=tax-tbx*dot,nby=tay-tby*dot,nb=Math.Sqrt(nbx*nbx+nby*nby),nax=tbx-tax*dot,nay=tby-tay*dot,na=Math.Sqrt(nax*nax+nay*nay);nbx/=nb;nby/=nb;nax/=na;nay/=na;
                        double hk=h*MaxWiden,cx=Px(c),cy=Py(c);
                        for(int s=-1;s<=1;s+=2)for(int a=1;a<n/4;a++)
                        {
                            int i=((c+s*a)%n+n)%n;double qx=Px(i)+sign*ox[i]*MaxWiden-cx,qy=Py(i)+sign*oy[i]*MaxWiden-cy,d=s>0?qx*nbx+qy*nby:qx*nax+qy*nay;
                            if(d>=hk-1e-6)break;snap[i]=c;
                        }
                    }
                    for(int i=0;i<n;i++)
                    {
                        int c=snap[i];double s=c>=0&&cross[c]>0?1:-1;
                        left[i]=c>=0&&s>0?V(Px(c),Py(c),ox[c],oy[c],rgb,1):V(Px(i),Py(i),ox[i],oy[i],rgb,1);
                        right[i]=c>=0&&s<0?V(Px(c),Py(c),-ox[c],-oy[c],rgb,1):V(Px(i),Py(i),-ox[i],-oy[i],rgb,1);
                    }
                    for(int i=0;i<(closed?n:n-1);i++){int j=(i+1)%n;Quad(left[i],left[j],right[j],right[i]);}
                    End(false,width*Width/CanvasWidth);
                }
                // Two triangles, leaving out one that a snapped side collapses to nothing.
                private void Quad(int a,int b,int c,int d)
                {
                    if(Area(a,b,c)!=0)T(a,b,c);if(Area(a,c,d)!=0)T(a,c,d);
                }
                private double Area(int a,int b,int c)=>(X[b]+OX[b]-X[a]-OX[a])*(Y[c]+OY[c]-Y[a]-OY[a])-(X[c]+OX[c]-X[a]-OX[a])*(Y[b]+OY[b]-Y[a]-OY[a]);
            }
        }
    }
}
