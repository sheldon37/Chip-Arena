using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// NOCTIEL's mask without its drawn brows and eye slits (the owner, 2026-11-08: "revise the face (Maybe just make it blink) so remove the little
    /// eyes and eyebrows"). The gear bank's long, narrow face (tools/gear/fallen_boss.py head_mask: the same rings, the same crack down from the left
    /// eye), blank above the eyes; phase 3's split down the middle as the bank's. Its eyes: two narrow almonds, dark with a soft light inside, set in the
    /// face, whose upper lids close now and then: a blink every few seconds, sometimes two together. In the skeleton's bind space (x right, y up, z
    /// forward; the bank's design frame with its y and z swapped), drawn on the head (Runtime NativeFallenMaskBody.cs) in the bank's mask's place.
    /// Presentation only. Pure.
    /// </summary>
    public static class NativeFallenMask
    {
        /// <summary>The bank's rings, chin to crown: height, half width, half depth, the centre's depth (forward), the superellipse's power, the ridge down
        /// the front (head_mask's numbers).</summary>
        public static readonly float[,] Rings=
        {
            {1.555f,.010f,.012f,.062f,2.0f,0f},{1.575f,.024f,.026f,.058f,2.0f,.004f},{1.6f,.040f,.046f,.05f,2.1f,.008f},{1.635f,.054f,.064f,.042f,2.2f,.012f},
            {1.67f,.062f,.074f,.032f,2.3f,.016f},{1.705f,.066f,.08f,.022f,2.3f,.012f},{1.74f,.06f,.074f,.012f,2.2f,.006f},{1.772f,.044f,.054f,.004f,2.1f,.002f},
            {1.79f,.018f,.022f,0f,2.0f,0f},
        };
        /// <summary>Phase 3's halves stand this far apart (the bank's split).</summary>
        public const float SplitGap=.009f;
        /// <summary>An eye: its middle across and up the face (the right eye; the left mirrors it), half its length, how far its outer corner rises across
        /// its length (a calm line, not the old slits' upward sweep), the dark almond's height above and below its line open.</summary>
        public const float EyeX=.031f,EyeY=1.69f,EyeHalf=.0175f,EyeRise=.12f,EyeTop=.0058f,EyeBottom=.0032f;
        /// <summary>How far the eye stands off the face (the dark almond, then its light).</summary>
        public const float EyeOff=.0016f,LightOff=.0021f;
        /// <summary>The light's core and rim (the glow takes the vertex colour).</summary>
        public const uint LightCore=0xFFFFFF,LightRim=0xA8A8A8;

        /// <summary>The bank's ring() with its ridge: a superellipse round the face's middle at that height, its front pushed to a ridge.</summary>
        private static Vector3[] Ring(int r,int n,float split,int side)
        {
            float y=Rings[r,0],rx=Rings[r,1],ry=Rings[r,2],oy=Rings[r,3],pw=Rings[r,4],keel=Rings[r,5];var pts=new Vector3[n];
            for(int i=0;i<n;i++)
            {
                double a=2*Math.PI*i/n;float ca=(float)Math.Cos(a),sa=(float)Math.Sin(a);
                float x=Math.Sign(ca)*(float)Math.Pow(Math.Abs(ca),2/pw)*rx,z=Math.Sign(sa)*(float)Math.Pow(Math.Abs(sa),2/pw)*ry;
                float front=Math.Max(0,sa);z+=keel*front*front*front*front*front*front;
                pts[i]=new Vector3(x+split*side,y,oy+z);
            }
            return pts;
        }
        /// <summary>The mask (phase 3's split halves when `split`): one closed solid each, in the bank's plate, its crack when whole. Tier 1 is coarser.</summary>
        public static NativeFallenKit Build(bool split,int tier)
        {
            var k=new NativeFallenKit();int n=tier==0?28:14;int count=Rings.GetLength(0);
            // Split, the bank moves each point of the one solid off the middle to its side (the points at the very front and back, a hair over
            // and under nought, go right and left), so the faces across the middle stretch over the gap as the bank's do.
            var rings=new List<Vector3[]>();var centres=new List<Vector3>();
            for(int r=0;r<count;r++)
            {
                var ring=Ring(r,n,0,1);if(split)for(int i=0;i<n;i++)ring[i].X+=ring[i].X>0?SplitGap:-SplitGap;
                rings.Add(ring);centres.Add(new Vector3(0,Rings[r,0],Rings[r,3]));
            }
            k.Loft(rings,centres,NativeFallenKit.Plate,capFirst:true,capLast:true);
            // The crack down from the left eye (the bank's, unchanged; not on the split mask).
            if(!split)
            {
                var c=new[]{new Vector3(-.03f,1.665f,.109f),new Vector3(-.033f,1.645f,.108f),new Vector3(-.027f,1.625f,.104f),new Vector3(-.031f,1.6f,.098f)};
                k.Tube(NativeFallenKit.Curve(c,3),.0012f,4,NativeFallenKit.Vein,false,false);
            }
            k.Finish();return k;
        }
        /// <summary>The front of the face (its depth, z) at `x` across and `y` up, from the rings either side of that height: where the eyes sit.</summary>
        public static float Front(float x,float y)
        {
            int count=Rings.GetLength(0);int r=0;while(r<count-2&&Rings[r+1,0]<y)r++;
            float t=Clamp01((y-Rings[r,0])/(Rings[r+1,0]-Rings[r,0]));float L(int c)=>Rings[r,c]+(Rings[r+1,c]-Rings[r,c])*t;
            float rx=L(1),ry=L(2),oy=L(3),pw=L(4),keel=L(5);float u=Math.Min(1,Math.Abs(x)/rx);
            float s=(float)Math.Pow(Math.Max(0,1-Math.Pow(u,pw)),1/pw);float sa=(float)Math.Pow(s,pw/2);
            return oy+ry*s+keel*(float)Math.Pow(sa,6);
        }
        /// <summary>The depth of the built face's own flat faces at `x` across and `y` up round the eyes (both halves: their faces split the other way,
        /// so they differ by a fraction of a millimetre), sampled once on a fine grid from the tier-0 mask's plate and read between its points, so the eyes stand the same small way off it everywhere
        /// (on the smooth curve of Front they stood up to 3 mm off the faces at their corners). Off the grid, Front.</summary>
        public static float FaceDepth(float x,float y)
        {
            var g=grid??(grid=DepthGrid());float u=(x-GridX0)/(GridX1-GridX0)*(GridW-1),v=(y-GridY0)/(GridY1-GridY0)*(GridH-1);
            if(u<0||v<0||u>GridW-1||v>GridH-1)return Front(x,y);
            int i=Math.Min(GridW-2,(int)u),j=Math.Min(GridH-2,(int)v);float fu=u-i,fv=v-j;
            float a=g[j*GridW+i],b=g[j*GridW+i+1],c=g[(j+1)*GridW+i],d=g[(j+1)*GridW+i+1];return (a+(b-a)*fu)+((c+(d-c)*fu)-(a+(b-a)*fu))*fv;
        }
        private const int GridW=113,GridH=15;private const float GridX0=-.056f,GridX1=.056f,GridY0=1.676f,GridY1=1.704f;private static float[] grid;
        private static float[] DepthGrid()
        {
            var k=Build(false,0);var g=new float[GridW*GridH];
            for(int j=0;j<GridH;j++)for(int i=0;i<GridW;i++)
            {
                float x=GridX0+(GridX1-GridX0)*i/(GridW-1),y=GridY0+(GridY1-GridY0)*j/(GridH-1),best=float.NaN;
                for(int t=0;t+2<k.Lit.Count;t+=3)
                {
                    if(k.Color[k.Lit[t]]!=NativeFallenKit.Plate)continue;var A=k.P[k.Lit[t]];var B=k.P[k.Lit[t+1]];var C=k.P[k.Lit[t+2]];
                    float d=(B.Y-C.Y)*(A.X-C.X)+(C.X-B.X)*(A.Y-C.Y);if(Math.Abs(d)<1e-12f)continue;
                    float w0=((B.Y-C.Y)*(x-C.X)+(C.X-B.X)*(y-C.Y))/d,w1=((C.Y-A.Y)*(x-C.X)+(A.X-C.X)*(y-C.Y))/d,w2=1-w0-w1;if(w0<-1e-5f||w1<-1e-5f||w2<-1e-5f)continue;
                    float z=w0*A.Z+w1*B.Z+w2*C.Z;if(float.IsNaN(best)||z>best)best=z;
                }
                g[j*GridW+i]=float.IsNaN(best)?Front(x,y):best;
            }
            return g;
        }
        /// <summary>Both eyes into `k` at `open` (1 open, 0 shut): each a dark almond whose upper lid comes down to its lower one, the light inside it
        /// going as it closes; shut, a hairline. Laid on the face from where they'd be seen head on.</summary>
        public static void Eyes(NativeFallenKit k,float open,int tier)
        {
            open=Clamp01(open);int m=tier==0?12:6;
            for(int side=1;side>=-1;side-=2)
            {
                float lid=Math.Max(.05f,open);
                Almond(k,side,m,1,EyeTop,EyeBottom,lid,NativeFallenKit.Black,NativeFallenKit.Black,EyeOff,false);
                if(open>.18f)Almond(k,side,m,.74f,EyeTop*.56f,EyeBottom*.5f,(open-.18f)/.82f,LightCore,LightRim,LightOff,true);
            }
        }
        // One almond: `m` steps along, its top above the line and its bottom below it at full height, the top brought down to `lid` of the way. The
        // light's has a middle row, its core; its edges are its rim.
        private static void Almond(NativeFallenKit k,int side,int m,float length,float top,float bottom,float lid,uint core,uint rim,float off,bool glow)
        {
            float ang=(float)Math.Atan(EyeRise),ca=(float)Math.Cos(ang),sa=(float)Math.Sin(ang);int start=k.Count;
            // Along the eye (outward is +s), then up across it; on the face, pushed out to stand off it.
            void Put(float s,float across,uint color,float more)
            {
                float x=(EyeX+s*EyeHalf*length*ca-across*sa)*side,y=EyeY+s*EyeHalf*length*sa+across*ca;k.V(new Vector3(x,y,FaceDepth(x,y)+off+more),color,false);
            }
            float H(float s)=>(float)Math.Pow(Math.Max(0,1-s*s),.85f);
            for(int i=0;i<=m;i++){float s=-1+2f*i/m,h=H(s),lo=-bottom*h;Put(s,lo,glow?rim:core,0);Put(s,lo+(top+bottom)*h*lid,glow?rim:core,0);}
            if(!glow){for(int i=0;i<m;i++){int a=start+2*i,b=start+2*(i+1);k.Quad(a,b,b+1,a+1,Vector3.UnitZ,false);}return;}
            int mid=k.Count;
            for(int i=0;i<=m;i++){float s=-1+2f*i/m,h=H(s);Put(s,-bottom*h+(top+bottom)*h*lid*.5f,core,.0002f);}
            for(int i=0;i<m;i++){int a=start+2*i,b=start+2*(i+1);k.Quad(a,b,mid+i+1,mid+i,Vector3.UnitZ,true);k.Quad(mid+i,mid+i+1,b+1,a+1,Vector3.UnitZ,true);}
        }

        // ---- the blink ----
        /// <summary>Blinks land one in each slot of this long (s), anywhere in its first part; now and then a second follows close behind.</summary>
        public const float Slot=4.3f,SlotJitter=2.1f,DoubleChance=.22f,DoubleAfter=.33f;
        /// <summary>The lid coming down, held shut, going up (s).</summary>
        public const float Close=.07f,Shut=.05f,Lift=.13f;
        public static float BlinkLength=>Close+Shut+Lift;
        /// <summary>In the intro's close-up of the mask (Core NativeFallenBossPreview, MaskAt to PullAt) one blink this long after it starts, face on,
        /// and no other.</summary>
        public const float IntroBlink=1.6f;
        /// <summary>How open the eyes are at `t` on the frame's clock (1 open, 0 shut); `intro` while the intro plays.</summary>
        public static float Blink(float t,bool intro)
        {
            if(intro&&t>=NativeFallenBossPreview.MaskAt&&t<NativeFallenBossPreview.PullAt)return Lid(t-NativeFallenBossPreview.MaskAt-IntroBlink);
            return Open(t);
        }
        /// <summary>How open the eyes are at `t` (1 open, 0 shut).</summary>
        public static float Open(float t)
        {
            int k=(int)Math.Floor(t/Slot);float open=1;
            for(int j=k-1;j<=k;j++)
            {
                float at=j*Slot+Hash(j)*SlotJitter;open=Math.Min(open,Lid(t-at));
                if(Hash(j*7+3)<DoubleChance)open=Math.Min(open,Lid(t-at-DoubleAfter));
            }
            return open;
        }
        private static float Lid(float u)
        {
            if(u<0||u>BlinkLength)return 1;if(u<Close)return 1-Smooth(u/Close);if(u<Close+Shut)return 0;return Smooth((u-Close-Shut)/Lift);
        }
        private static float Hash(int i){uint h=(uint)i*2654435761u;h^=h>>15;h*=2246822519u;h^=h>>13;return (h&0xFFFFFF)/16777216f;}
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
    }
}
