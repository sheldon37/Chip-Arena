using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The v2 name card's legibility on any set (Runtime NativeIntroCardScreen draws it; Tests/IntroV2Checks holds it). Whiteout's name
    /// and kicker read pale on white snow, and Threshold's name sat on a sunlit tan floor patch. So the card's lines stand on a soft dark
    /// backing, a soft-edged ellipse that is darkest at its middle and has no ring. Every glyph also has a thin dark outline that scales
    /// with its size. On a dark set both are dark on dark, so the card looks as it did. Both fade with the card and never flash. A small
    /// line whose colour is too dark to read on the darkened backing is lifted toward white (Legible), which a light colour never needs.
    /// Colours here are sRGB (0..1), blended as the UI blends them (NativeUi: sRGB alpha blending).
    /// </summary>
    public static class NativeIntroCardInk
    {
        /// <summary>The backing's darkness at its middle (black, times the card's alpha), and the share of it the card's farthest glyphs
        /// still sit on (the ellipse is sized so its middle 45% holds the lines at .8 or more of it).</summary>
        public const float Backing=.38f,AtText=.78f;
        /// <summary>The outline's darkness (black). The UI draws it as eight copies round each glyph, each at HaloShare of it.</summary>
        public const float Outline=1f,HaloShare=.35f;
        /// <summary>How far the outline reaches, as a share of the text's size (at least one pixel).</summary>
        public const float OutlineReach=.05f;
        /// <summary>The intro line's colour (the line under the name).</summary>
        public const int LineHex=0xb9bac4;
        /// <summary>The least contrast (WCAG ratio) the name must have against what is right behind its glyphs, on any backdrop, and the
        /// small lines (the kicker, the team, the intro line).</summary>
        public const float NameMinimum=4.5f,LineMinimum=3f;
        /// <summary>The backdrops the card is held to: white snow and a sunlit mid-tan floor, and a dark set (whose look must not change).</summary>
        public static readonly Vector3 White=new Vector3(1,1,1),Tan=new Vector3(.78f,.65f,.5f),Dark=new Vector3(.06f,.06f,.07f);

        /// <summary>What is right behind a glyph's edge over `backdrop`: the backing where the farthest glyphs sit, then two of the
        /// outline's copies (where neighbouring copies overlap at the edge).</summary>
        public static Vector3 Behind(Vector3 backdrop){float o=1-HaloShare*Outline;return backdrop*(1-Backing*AtText)*o*o;}
        /// <summary>The backdrop under the backing's middle alone (the change a dark set sees away from the glyphs).</summary>
        public static Vector3 Backed(Vector3 backdrop)=>backdrop*(1-Backing);
        /// <summary>The backing's alpha stops (radius 0..1 of its ellipse): darkest in the middle, easing out to nothing, no ring.</summary>
        public static readonly float[] BackingAt={0,.45f,1},BackingAlpha={1,.8f,0};
        /// <summary>A linear colour as sRGB (0..1).</summary>
        public static Vector3 Srgb(Vector3 linear)=>new Vector3(Gamma(linear.X),Gamma(linear.Y),Gamma(linear.Z));
        public static Vector3 Hex(int rgb)=>new Vector3(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f);
        private static float Gamma(float c){c=Math.Max(0,Math.Min(1,c));return c<=.0031308f?12.92f*c:1.055f*(float)Math.Pow(c,1/2.4)-.055f;}
        private static float Linear(float c)=>c<=.04045f?c/12.92f:(float)Math.Pow((c+.055f)/1.055f,2.4);
        /// <summary>The relative luminance of an sRGB colour (WCAG).</summary>
        public static float Luminance(Vector3 srgb)=>.2126f*Linear(srgb.X)+.7152f*Linear(srgb.Y)+.0722f*Linear(srgb.Z);
        /// <summary>The WCAG contrast ratio of two sRGB colours (1 to 21).</summary>
        public static float Contrast(Vector3 a,Vector3 b){float la=Luminance(a),lb=Luminance(b);return (Math.Max(la,lb)+.05f)/(Math.Min(la,lb)+.05f);}
        /// <summary>The worst contrast of a text colour (sRGB) over the held bright backdrops.</summary>
        public static float Worst(Vector3 srgb)=>Math.Min(Contrast(srgb,Behind(White)),Contrast(srgb,Behind(Tan)));
        /// <summary>A small line's colour (sRGB), lifted toward white just as far as it needs to reach LineMinimum on the backing
        /// (unchanged when it already does: every light colour).</summary>
        public static Vector3 Legible(Vector3 srgb)
        {
            if(Worst(srgb)>=LineMinimum)return srgb;float lo=0,hi=1;
            for(int i=0;i<14;i++){float mid=(lo+hi)*.5f;if(Worst(Vector3.Lerp(srgb,White,mid))>=LineMinimum)hi=mid;else lo=mid;}
            return Vector3.Lerp(srgb,White,hi);
        }
    }
}
