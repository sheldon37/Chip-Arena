using System;

namespace ChipArena.UnityConversion
{
    // The Locker's colour wheel and the skin tone's colour rules (owner 2026-10-28: "Give skin color customization, along with
    // a 'Color selector circle' for more color options").
    //  - The wheel: hue round the disc (0 at the right, counter-clockwise), saturation from the centre (white) to the rim
    //    (full), and brightness on its own track beside it. Runtime/NativeColorWheelUI.cs draws it and handles the drag.
    //  - Skin: the test shared by the presentation and the model's shader (Resources/NativeVisualBodyFx.cginc SkinShare) that
    //    tells a skin colour from any other in a skin part's palette: warm (red over green over blue, a colour with two channels level half way),
    //    somewhat saturated (not grey, not a pure hue), not black. A pilot's skin tone scales each skin colour by the tone over
    //    the part's base skin (the brightest colour of its palette that reads as skin), so the shade, the blush and the lips
    //    keep their relation to the skin while the eyes' whites and the straps keep theirs.
    // Engine free; nothing here is read by a rule, hit volume, timing or network authority.
    public static class NativeColorWheel
    {
        public static int Rgb(float hue,float saturation,float value)=>NativeHatLooks.FromHsv(hue,Clamp01(saturation),Clamp01(value));
        public static void Hsv(int rgb,out float hue,out float saturation,out float value)=>NativeHatLooks.ToHsv(rgb&0xffffff,out hue,out saturation,out value);
        /// <summary>The disc point (unit radius, x right, y up) of a hue and saturation.</summary>
        public static void Point(float hue,float saturation,out float x,out float y)
        {
            double a=(hue-Math.Floor(hue))*2*Math.PI;float s=Clamp01(saturation);x=(float)Math.Cos(a)*s;y=(float)Math.Sin(a)*s;
        }
        /// <summary>The hue and saturation at a disc point (unit radius; past the rim, the rim's).</summary>
        public static void FromPoint(float x,float y,out float hue,out float saturation)
        {
            float r=(float)Math.Sqrt(x*x+y*y);saturation=Math.Min(1f,r);
            if(r<1e-5f){hue=0;return;}
            double a=Math.Atan2(y,x)/(2*Math.PI);if(a<0)a+=1;hue=(float)a;
        }

        // ---- skin ----
        /// <summary>The model's own skin (the bake's palette skin), the base of a part drawn from its texture when no part of
        /// the body has a palette skin, and the Locker thumbnail's skin when no tone is picked.</summary>
        public const int ModelSkin=0xefd0bd;
        /// <summary>How much an sRGB colour (channels 0..1) reads as skin, 0..1.</summary>
        public static float SkinShare(float r,float g,float b)
        {
            float hi=Math.Max(r,Math.Max(g,b)),lo=Math.Min(r,Math.Min(g,b)),sat=(hi-lo)/Math.Max(hi,.0001f);
            float warm=Clamp01((r-g)*60+.5f)*Clamp01((g-b)*60+.5f);
            return warm*Smooth(.05f,.12f,sat)*(1-Smooth(.80f,.92f,sat))*Smooth(.03f,.08f,hi);
        }
        public static float SkinShare(int rgb)=>SkinShare((rgb>>16&255)/255f,(rgb>>8&255)/255f,(rgb&255)/255f);
        /// <summary>The base skin of a palette (0xRRGGBB entries, negative for none): its brightest colour that reads as skin
        /// (share at least .5), or -1 when none does.</summary>
        public static int BaseSkin(int[] palette)
        {
            int best=-1;float bright=-1;if(palette==null)return -1;
            foreach(int c in palette)
            {
                if(c<0||SkinShare(c)<.5f)continue;float l=Luma(c);if(l>bright){bright=l;best=c&0xffffff;}
            }
            return best;
        }
        /// <summary>A skin colour `c` under the tone `tone` for the base skin `baseSkin` (all 0xRRGGBB): each channel scaled by the
        /// tone over the base, clamped; any colour that does not read as skin unchanged, and a part of one in between. The CPU
        /// mirror of the shader's SkinToned (in sRGB here; the shader scales linear colours, which keeps the same ratios'
        /// order).</summary>
        public static int Toned(int c,int tone,int baseSkin)
        {
            if(tone<0||baseSkin<0)return c&0xffffff;float k=SkinShare(c);if(k<=0)return c&0xffffff;
            int o=0;
            for(int shift=16;shift>=0;shift-=8)
            {
                float v=(c>>shift&255)/255f,t=(tone>>shift&255)/255f,b=Math.Max((baseSkin>>shift&255)/255f,.016f);
                float to=Clamp01(v*t/b),mix=v+(to-v)*k;o|=(int)Math.Round(Clamp01(mix)*255)<<shift;
            }
            return o;
        }
        public static float Luma(int rgb)=>.2126f*(rgb>>16&255)/255f+.7152f*(rgb>>8&255)/255f+.0722f*(rgb&255)/255f;
        private static float Smooth(float a,float b,float x){float t=Clamp01((x-a)/(b-a));return t*t*(3-2*t);}
        private static float Clamp01(float v)=>v<0?0:v>1?1:v;
    }
}
