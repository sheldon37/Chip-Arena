using System;
using System.Collections.Generic;
using System.Threading;

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
    ///    (<see cref="GlyphShown"/>, <see cref="GlyphOpacity"/>, <see cref="GlyphLift"/>; <see cref="Glyph"/> bakes hints.ts's
    ///    canvas into one image, which fades as one as the source's sprite does), and
    ///    horde-view.ts's black-flame trail behind the add while it walks (<see cref="Trail"/>: showRig emits it from level 2,
    ///    `level>=2`, as hints.ts's header lists it; the handoff notes that called it a level 3 effect do not match the source);
    ///  - level 3 brightens the seam, the halo and the grin by 1.35 (<see cref="Brightness"/>).
    /// The trail drips gore.ts's stump flame into the shared world flame pool on slots counted from the possession start, so
    /// any frame rate and every client emit the same flames along the same walk (their jitter is hashed per slot). Source
    /// frame (the runtime mirrors x). Allocation free; the grin is baked once, off the main thread.
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
        /// canvas at (x + 28, y - 48) and shown as a .78 x .29 m sprite whose material opacity fades the finished canvas as one
        /// image. Baked here once into that image at <see cref="Scale"/> times the canvas (sharp up close), painting the canvas's
        /// operations in order (source-over in straight sRGB, as the canvas composites):
        ///  1. the grin filled twice in rgba(28,14,52,.9), each fill over its violet shadow (shadowBlur 22);
        ///  2. the pale lips (#ded4ff, the closed path stroked 4.5 px with miter joins, limit 10) over their shadow (shadowBlur 8);
        ///  3. the white teeth (stroked 4 px) over theirs.
        /// Shapes are covered exactly at the texture's size (<see cref="Sub"/> scanlines a texel row, exact across). A shadow is its
        /// operation's coverage times its alpha on the 256 x 96 canvas itself, blurred by a Gaussian of sigma shadowBlur / 2
        /// (Chromium's canvas; measured 4.1 and 10.7) and sampled bilinearly, so it ends at the canvas's edge as the source's does.
        /// A texel holds its colour premultiplied in linear light and sRGB-encoded (an sRGB texture decodes, filters and fades it
        /// correctly) and its alpha; each mip level is the premultiplied box mean of the one above in linear light. Rows run bottom
        /// first and level 0 first, as Texture2D.LoadRawTextureData takes them. The bake is pure managed work: <see cref="Prepare"/>
        /// starts it once on the thread pool and <see cref="Ready"/> hands it over, never waiting for it.
        /// </summary>
        public static class Glyph
        {
            public const double Width=.78,Height=.29,ShiftX=28,ShiftY=-48;public const int CanvasWidth=256,CanvasHeight=96,Scale=2,Sub=4;
            public const int MouthRgb=0x1c0e34,LipRgb=0xded4ff,TeethRgb=0xffffff;
            public const double MouthAlpha=.9,LipWidth=4.5,TeethWidth=4,MouthBlur=22,StrokeBlur=8,MiterLimit=10;
            /// <summary>A baked grin: Levels mip levels of RGBA32 texels in Rgba, level l Widths[l] x Heights[l] from Offset[l].</summary>
            public sealed class Image
            {
                public readonly int Width,Height,Levels;public readonly int[] Widths,Heights,Offset;public readonly byte[] Rgba;
                internal Image(int[] widths,int[] heights,int[] offset,byte[] rgba){Widths=widths;Heights=heights;Offset=offset;Rgba=rgba;Width=widths[0];Height=heights[0];Levels=widths.Length;}
                /// <summary>Texel (x, y) of a level, y counted from the top as on the canvas, as the canvas holds it: straight sRGB
                /// colour and alpha (0 colour where clear).</summary>
                public void Texel(int level,int x,int y,out double r,out double g,out double b,out double a)
                {
                    int w=Widths[level],i=Offset[level]+((Heights[level]-1-y)*w+x)*4;a=Rgba[i+3]/255.0;r=g=b=0;if(a<=0)return;
                    r=Math.Min(1,Srgb(Linear(Rgba[i]/255.0)/a));g=Math.Min(1,Srgb(Linear(Rgba[i+1]/255.0)/a));b=Math.Min(1,Srgb(Linear(Rgba[i+2]/255.0)/a));
                }
            }
            private static volatile Image ready;private static volatile string failure;private static int started;
            /// <summary>The baked grin once <see cref="Prepare"/>'s bake has finished, else null (never blocks).</summary>
            public static Image Ready=>ready;
            /// <summary>Why the bake failed (null while it runs or after it succeeds): the grin then stays hidden.</summary>
            public static string Failure=>failure;
            /// <summary>Starts the bake on the thread pool, once (later calls return at once).</summary>
            public static void Prepare()
            {
                if(Volatile.Read(ref started)!=0||Interlocked.Exchange(ref started,1)!=0)return;
                ThreadPool.QueueUserWorkItem(_=>{try{ready=Bake(Scale);}catch(Exception e){failure=e.GetType().Name+": "+e.Message;}});
            }
            /// <summary>The exact sRGB transfer functions.</summary>
            public static double Linear(double c)=>c<=.04045?c/12.92:Math.Pow((c+.055)/1.055,2.4);
            public static double Srgb(double l)=>l<=.0031308?l*12.92:1.055*Math.Pow(l,1/2.4)-.055;
            /// <summary>Bakes the grin at `scale` times the canvas (see the class notes).</summary>
            public static Image Bake(int scale)
            {
                if(scale<1)throw new ArgumentOutOfRangeException(nameof(scale));int w=CanvasWidth*scale,h=CanvasHeight*scale;var line=NativeSirenSmile.Grin[0];
                // The closed path's stroke joins at its start too: the line runs on round to its second point.
                var closed=new float[line.Length+4];Array.Copy(line,closed,line.Length);Array.Copy(line,0,closed,line.Length,4);
                List<float[]> lips=NativeSirenSmile.MiterStroke(closed,(float)LipWidth,(float)MiterLimit),teeth=NativeSirenSmile.MiterStroke(NativeSirenSmile.TeethLine[0],(float)TeethWidth,(float)MiterLimit);
                float[] mouth=Cover(NativeSirenSmile.Grin,scale),lip=Cover(lips,scale),tooth=Cover(teeth,scale),mouthShadow=Cover(NativeSirenSmile.Grin,1),lipShadow=Cover(lips,1),toothShadow=Cover(teeth,1);
                for(int i=0;i<mouthShadow.Length;i++)mouthShadow[i]*=(float)MouthAlpha;
                Blur(mouthShadow,MouthBlur/2);Blur(lipShadow,StrokeBlur/2);Blur(toothShadow,StrokeBlur/2);
                var top=new double[w*h*4];
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                {
                    int i=y*w+x,o=i*4;double u=(x+.5)/scale,v=(y+.5)/scale,shadow=Sample(mouthShadow,u,v),fill=MouthAlpha*mouth[i],r=0,g=0,b=0,a=0;
                    Over(NativeMinionFx.Violet,shadow,ref r,ref g,ref b,ref a);Over(MouthRgb,fill,ref r,ref g,ref b,ref a);Over(NativeMinionFx.Violet,shadow,ref r,ref g,ref b,ref a);Over(MouthRgb,fill,ref r,ref g,ref b,ref a);
                    Over(NativeMinionFx.Violet,Sample(lipShadow,u,v),ref r,ref g,ref b,ref a);Over(LipRgb,lip[i],ref r,ref g,ref b,ref a);
                    Over(NativeMinionFx.Violet,Sample(toothShadow,u,v),ref r,ref g,ref b,ref a);Over(TeethRgb,tooth[i],ref r,ref g,ref b,ref a);
                    if(a>0){top[o]=Linear(r/a)*a;top[o+1]=Linear(g/a)*a;top[o+2]=Linear(b/a)*a;top[o+3]=a;}
                }
                // The mip chain (Unity's: halved, at least 1, down to 1 x 1), each texel the mean of the texels it covers above.
                int levels=1;for(int lw=w,lh=h;lw>1||lh>1;levels++){lw=Math.Max(1,lw/2);lh=Math.Max(1,lh/2);}
                var widths=new int[levels];var heights=new int[levels];var offset=new int[levels];int total=0;
                for(int l=0;l<levels;l++){widths[l]=l==0?w:Math.Max(1,widths[l-1]/2);heights[l]=l==0?h:Math.Max(1,heights[l-1]/2);offset[l]=total;total+=widths[l]*heights[l]*4;}
                var rgba=new byte[total];var level=top;
                for(int l=0;l<levels;l++)
                {
                    int lw=widths[l],lh=heights[l];
                    if(l>0)
                    {
                        int pw=widths[l-1],ph=heights[l-1];var next=new double[lw*lh*4];
                        for(int y=0;y<lh;y++)for(int x=0;x<lw;x++)
                        {
                            int x0=x*pw/lw,x1=(x+1)*pw/lw,y0=y*ph/lh,y1=(y+1)*ph/lh,o=(y*lw+x)*4;double n=(x1-x0)*(y1-y0);
                            for(int c=0;c<4;c++){double s=0;for(int yy=y0;yy<y1;yy++)for(int xx=x0;xx<x1;xx++)s+=level[(yy*pw+xx)*4+c];next[o+c]=s/n;}
                        }
                        level=next;
                    }
                    for(int y=0;y<lh;y++)for(int x=0;x<lw;x++){int s=(y*lw+x)*4,d=offset[l]+((lh-1-y)*lw+x)*4;for(int c=0;c<3;c++)rgba[d+c]=B8(Srgb(Math.Min(1,level[s+c])));rgba[d+3]=B8(level[s+3]);}
                }
                return new Image(widths,heights,offset,rgba);
            }
            private static byte B8(double v)=>(byte)(v<=0?0:v>=1?255:(int)(v*255+.5));
            // Source-over of an sRGB colour at alpha k onto premultiplied (r, g, b, a).
            private static void Over(int rgb,double k,ref double r,ref double g,ref double b,ref double a)
            {if(k<=0)return;double keep=1-k;r=(rgb>>16&255)/255.0*k+r*keep;g=(rgb>>8&255)/255.0*k+g*keep;b=(rgb&255)/255.0*k+b*keep;a=k+a*keep;}
            // Coverage of path polygons (nonzero) on the canvas at `scale`, rows from the top.
            private static float[] Cover(List<float[]> paths,int scale)
            {
                int w=CanvasWidth*scale,h=CanvasHeight*scale,n=0;foreach(var p in paths)n+=p.Length/2;var e=new float[n*4];int k=0;
                foreach(var p in paths){int m=p.Length/2;for(int j=0;j<m;j++){int q=(j+1)%m;e[k++]=(float)((p[j*2]+ShiftX)*scale);e[k++]=(float)((p[j*2+1]+ShiftY)*scale);e[k++]=(float)((p[q*2]+ShiftX)*scale);e[k++]=(float)((p[q*2+1]+ShiftY)*scale);}}
                var scan=new NativeSirenSmile.Scan(w,n);scan.Load(e,n);var row=new float[w];var cover=new float[w*h];
                for(int y=0;y<h;y++){scan.Row(y,Sub,row);Array.Copy(row,0,cover,y*w,w);}
                return cover;
            }
            // Separable Gaussian on the canvas, clear beyond it.
            private static void Blur(float[] f,double sigma)
            {
                int w=CanvasWidth,h=CanvasHeight,r=(int)Math.Ceiling(3*sigma);var k=new double[2*r+1];double sum=0;for(int i=-r;i<=r;i++)sum+=k[i+r]=Math.Exp(-i*i/(2*sigma*sigma));for(int i=0;i<k.Length;i++)k[i]/=sum;
                var t=new float[f.Length];
                for(int y=0;y<h;y++)for(int x=0;x<w;x++){double v=0;for(int i=-r;i<=r;i++){int u=x+i;if(u>=0&&u<w)v+=k[i+r]*f[y*w+u];}t[y*w+x]=(float)v;}
                for(int y=0;y<h;y++)for(int x=0;x<w;x++){double v=0;for(int i=-r;i<=r;i++){int u=y+i;if(u>=0&&u<h)v+=k[i+r]*t[u*w+x];}f[y*w+x]=(float)v;}
            }
            // Bilinear between canvas pixel centres, held at the edge pixels.
            private static double Sample(float[] f,double u,double v)
            {
                int w=CanvasWidth,h=CanvasHeight;u-=.5;v-=.5;int i=(int)Math.Floor(u),j=(int)Math.Floor(v);double fu=u-i,fv=v-j;
                int i0=Math.Max(0,Math.Min(w-1,i)),i1=Math.Max(0,Math.Min(w-1,i+1)),j0=Math.Max(0,Math.Min(h-1,j)),j1=Math.Max(0,Math.Min(h-1,j+1));
                return (f[j0*w+i0]*(1-fu)+f[j0*w+i1]*fu)*(1-fv)+(f[j1*w+i0]*(1-fu)+f[j1*w+i1]*fu)*fv;
            }
        }
    }
}
