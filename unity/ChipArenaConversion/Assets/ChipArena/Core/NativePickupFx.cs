using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The PvE chip pickup's glow and light beam (client/pve/pickups.ts Pickup and glowTexture, client/pve/chip-model.ts
    /// tierColor and BEAM_TIERS at 05950ea) as pure rules:
    /// <list type="bullet">
    /// <item>An additive, camera-facing 1.15 m glow sprite .06 m under the chip, tinted in the tier colour, opacity
    /// (.95 for the owner, else .7) x (.8 + .2 sin 4t). Its 256 px canvas texture is a white radial gradient (centre
    /// (128, 96), radius 96: alpha .95, .45 at a quarter, 0 at the rim; filled over the top 192 rows) and, for the owner,
    /// "YOURS" in 700 34 px HOLO_FONT centred at (128, 226) with a white rgba(255,255,255,.9) shadow blurred 8 px.</item>
    /// <item>For epic and above, once the chip has landed (pose progress 1): a translucent additive open cylinder from the
    /// floor (y 0) to 7 m, radius .3 at the floor and .16 at the top, 12 sides, tier colour, opacity
    /// (.16 + .08 sin 3t) x (1 for the owner, else .6), seen from both sides.</item>
    /// <item>tierColor: the rarity colour, or for rainbow HSL(t * .25, .95, .62) taken as linear components (three.js
    /// setHSL works in the linear working space).</item>
    /// </list>
    /// No lights, no quality scaling (the source has none). Nothing here is read by the simulation or the network frame.
    /// </summary>
    public static class NativePickupFx
    {
        public const float GlowSize=1.15f,GlowLift=-.06f,GlowMine=.95f,GlowOther=.7f;
        public const float BeamTop=.16f,BeamBottom=.3f,BeamHeight=7,BeamBase=.16f,BeamSwing=.08f,BeamOther=.6f;public const int BeamSides=12;
        public const float RainbowRate=.25f,RainbowSaturation=.95f,RainbowLightness=.62f;
        /// <summary>The glow canvas: size, gradient centre and radius, filled rows; the label, its font size and centre.</summary>
        public const int Canvas=256,GradientX=128,GradientY=96,GradientRadius=96,GradientRows=192,LabelX=128,LabelY=226,LabelPx=34;
        public const float LabelShadowBlur=8,LabelShadowAlpha=.9f;
        /// <summary>textBaseline 'middle': the baseline .35 font sizes under the centre (as the other canvas text ports).</summary>
        public const float MiddleBaseline=.35f;
        public const string Label="YOURS";
        private static readonly float[] stopAt={0,.25f,1},stopAlpha={.95f,.45f,0};

        /// <summary>BEAM_TIERS: epic, legendary, mythic and rainbow.</summary>
        public static bool BeamTier(string tier)=>tier=="epic"||tier=="legendary"||tier=="mythic"||tier=="rainbow";
        public static bool BeamShown(string tier,float progress)=>BeamTier(tier)&&progress>=1;
        public static float GlowOpacity(bool mine,float t)=>(mine?GlowMine:GlowOther)*(.8f+.2f*(float)Math.Sin(t*4));
        public static float BeamOpacity(bool mine,float t)=>(BeamBase+BeamSwing*(float)Math.Sin(t*3))*(mine?1:BeamOther);
        /// <summary>The rainbow tier's colour at t, linear components (setHSL in the working colour space).</summary>
        public static void Rainbow(float t,out float r,out float g,out float b)
        {
            double h=((t*(double)RainbowRate)%1+1)%1,s=RainbowSaturation,l=RainbowLightness,p=l<=.5?l*(1+s):l+s-l*s,q=2*l-p;
            r=(float)Hue(q,p,h+1/3.0);g=(float)Hue(q,p,h);b=(float)Hue(q,p,h-1/3.0);
        }
        private static double Hue(double p,double q,double t)
        {if(t<0)t+=1;if(t>1)t-=1;if(t<1/6.0)return p+(q-p)*6*t;if(t<.5)return q;if(t<2/3.0)return p+(q-p)*6*(2/3.0-t);return p;}

        /// <summary>The radial gradient's alpha at a canvas point (pixel centres are x + .5, y + .5); 0 outside the fill.</summary>
        public static float Gradient(float x,float y)
        {
            if(y>=GradientRows||y<0||x<0||x>=Canvas)return 0;
            float dx=x-GradientX,dy=y-GradientY,t=(float)Math.Sqrt(dx*dx+dy*dy)/GradientRadius;
            if(t>=1)return stopAlpha[2];if(t<=0)return stopAlpha[0];
            for(int i=1;i<stopAt.Length;i++)if(t<=stopAt[i]){float k=(t-stopAt[i-1])/(stopAt[i]-stopAt[i-1]);return stopAlpha[i-1]+(stopAlpha[i]-stopAlpha[i-1])*k;}
            return 0;
        }
        /// <summary>
        /// The glow texture's alpha, Canvas x Canvas, top row first: the gradient, then (with <paramref name="label"/>, the
        /// text's coverage) the canvas shadow (the coverage blurred by a Gaussian of sigma shadowBlur / 2, times .9) and the
        /// text, each composited source-over.
        /// </summary>
        public static void Glow(float[] alpha,float[] label)
        {
            const int n=Canvas;
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)alpha[y*n+x]=Gradient(x+.5f,y+.5f);
            if(label==null)return;
            var shadow=new float[n*n];Blur(label,shadow,n,n,LabelShadowBlur*.5f);
            for(int i=0;i<n*n;i++){float a=alpha[i],s=shadow[i]*LabelShadowAlpha,c=label[i];a=s+a*(1-s);alpha[i]=c+a*(1-c);}
        }
        /// <summary>A separable Gaussian blur (radius 3 sigma), zero outside the canvas.</summary>
        public static void Blur(float[] source,float[] target,int w,int h,float sigma)
        {
            int radius=(int)Math.Ceiling(sigma*3);var kernel=new double[radius*2+1];double sum=0;
            for(int i=-radius;i<=radius;i++){kernel[i+radius]=Math.Exp(-i*i/(2.0*sigma*sigma));sum+=kernel[i+radius];}for(int i=0;i<kernel.Length;i++)kernel[i]/=sum;
            var temp=new double[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){double v=0;for(int i=-radius;i<=radius;i++){int sx=x+i;if(sx>=0&&sx<w)v+=source[y*w+sx]*kernel[i+radius];}temp[y*w+x]=v;}
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){double v=0;for(int i=-radius;i<=radius;i++){int sy=y+i;if(sy>=0&&sy<h)v+=temp[sy*w+x]*kernel[i+radius];}target[y*w+x]=(float)v;}
        }
        /// <summary>A glyph to draw into the label coverage: its rectangle in canvas pixels and the atlas uv of its corners.</summary>
        public struct Glyph{public float X0,X1,Top,Bottom,TopLeftU,TopLeftV,TopRightU,TopRightV,BottomLeftU,BottomLeftV,BottomRightU,BottomRightV;}
        /// <summary>
        /// Rasterizes a glyph into <paramref name="coverage"/> (Canvas x Canvas, top row first), sampling the alpha atlas
        /// (<paramref name="atlas"/>, aw x ah, bottom row first as Unity stores textures) bilinearly at 2 x 2 points per pixel;
        /// overlapping glyphs combine source-over, as canvas fills do.
        /// </summary>
        public static void Stamp(float[] coverage,byte[] atlas,int aw,int ah,in Glyph g)
        {
            if(g.X1<=g.X0||g.Bottom<=g.Top)return;
            int x0=Math.Max(0,(int)Math.Floor(g.X0)),x1=Math.Min(Canvas-1,(int)Math.Ceiling(g.X1)),y0=Math.Max(0,(int)Math.Floor(g.Top)),y1=Math.Min(Canvas-1,(int)Math.Ceiling(g.Bottom));
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
            {
                float sum=0;
                for(int k=0;k<4;k++)
                {
                    float px=x+(k%2==0?.25f:.75f),py=y+(k<2?.25f:.75f),s=(px-g.X0)/(g.X1-g.X0),t=(py-g.Top)/(g.Bottom-g.Top);if(s<0||s>1||t<0||t>1)continue;
                    float tu=g.TopLeftU+(g.TopRightU-g.TopLeftU)*s,tv=g.TopLeftV+(g.TopRightV-g.TopLeftV)*s,bu=g.BottomLeftU+(g.BottomRightU-g.BottomLeftU)*s,bv=g.BottomLeftV+(g.BottomRightV-g.BottomLeftV)*s;
                    sum+=Sample(atlas,aw,ah,(tu+(bu-tu)*t)*aw-.5f,(tv+(bv-tv)*t)*ah-.5f);
                }
                float a=sum*.25f;if(a<=0)continue;int i=y*Canvas+x;coverage[i]=a+coverage[i]*(1-a);
            }
        }
        private static float Sample(byte[] atlas,int w,int h,float x,float y)
        {
            int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y);float fx=x-ix,fy=y-iy;
            float At(int px,int py)=>px<0||py<0||px>=w||py>=h?0:atlas[py*w+px]/255f;
            return (At(ix,iy)*(1-fx)+At(ix+1,iy)*fx)*(1-fy)+(At(ix,iy+1)*(1-fx)+At(ix+1,iy+1)*fx)*fy;
        }
        /// <summary>
        /// Writes one glow variant into an RGBA32 texture (bottom row first) of width <paramref name="stride"/> at column
        /// <paramref name="column"/>: alpha quantised to 8 bits as the canvas stores it; white where it is not transparent
        /// (the un-premultiplied canvas).
        /// </summary>
        public static void Write(float[] alpha,byte[] rgba,int stride,int column)
        {
            const int n=Canvas;
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                int a=(int)Math.Round(Math.Max(0,Math.Min(1,alpha[y*n+x]))*255),o=(((n-1-y)*stride)+column+x)*4;byte c=(byte)(a>0?255:0);
                rgba[o]=c;rgba[o+1]=c;rgba[o+2]=c;rgba[o+3]=(byte)a;
            }
        }
        /// <summary>
        /// CylinderGeometry(.16, .3, 7, 12, 1, true) translated up 3.5: vertex (column 0..12, row 0 top / 1 floor) of the beam,
        /// in source axes relative to the floor under the chip. Faces: (a, b, d), (b, c, d) for a = (x, 0), b = (x, 1),
        /// c = (x + 1, 1), d = (x + 1, 0).
        /// </summary>
        public static void BeamVertex(int column,int row,out float x,out float y,out float z)
        {
            float radius=row*(BeamBottom-BeamTop)+BeamTop,theta=(float)(column/(double)BeamSides*Math.PI*2);
            x=radius*(float)Math.Sin(theta);y=-row*BeamHeight+BeamHeight*.5f+BeamHeight*.5f;z=radius*(float)Math.Cos(theta);
        }
    }
}
