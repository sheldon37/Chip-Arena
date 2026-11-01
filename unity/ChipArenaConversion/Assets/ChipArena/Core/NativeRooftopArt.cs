using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ChipArena.UnityConversion
{
    // The redesigned menu's vector art, as SVG markup for NativeIconRaster (Markup mode), so the same drawings render in the
    // engine and in the managed checks, and no font or texture asset ships:
    //  - the chamfered monoline capitals of the CHIP ARENA wordmark (a 40-unit-tall cell), shared by the home screen's title
    //    and the rooftop scene's holo screens;
    //  - the holo screens' ads (Chip Arena, the RICOCHET chip card, The Siren with the Boss Test skull, the CHIP banner, the
    //    ARENA blade sign), drawn on a 100-unit square; non-square screens sample the sub-rectangle AdUv returns.
    public static class NativeRooftopArt
    {
        public const float GlyphHeight=40;
        private struct Glyph {public float W;public string D;}
        // Absolute M/L/H/V/Z only, so they can be placed by Transform.
        private static readonly Dictionary<char,Glyph> glyphs=new Dictionary<char,Glyph>
        {
            ['C']=new Glyph{W=22,D="M22 1H7L1 7V33L7 39H22"},['H']=new Glyph{W=22,D="M1 1V39M21 1V39M1 20H21"},['I']=new Glyph{W=2,D="M1 1V39"},
            ['P']=new Glyph{W=21,D="M1 39V1H15L21 7V15L15 21H1"},['A']=new Glyph{W=22,D="M1 39V11L11 1L21 11V39M1 25H21"},['R']=new Glyph{W=21,D="M1 39V1H15L21 7V15L15 21H1M11 21L21 39"},
            ['E']=new Glyph{W=20,D="M20 1H1V39H20M1 20H15"},['N']=new Glyph{W=22,D="M1 39V1L21 39V1"},['B']=new Glyph{W=21,D="M1 39V1H13L19 7V14L13 20H1M13 20L21 26V33L15 39H1"},
            ['O']=new Glyph{W=22,D="M7 1H15L21 7V33L15 39H7L1 33V7Z"},['S']=new Glyph{W=21,D="M21 7L15 1H7L1 7V14L7 20H15L21 26V33L15 39H7L1 33"},['T']=new Glyph{W=22,D="M1 1H21M11 1V39"},
        };
        public static bool Has(char c)=>glyphs.ContainsKey(c)||c==' ';
        public static float Advance(char c,float spacing)=>c==' '?spacing*1.6f:glyphs.TryGetValue(c,out var g)?g.W+spacing:0;
        public static float Width(string text,float spacing){float w=0;for(int i=0;i<text.Length;i++)w+=Advance(text[i],spacing);return Math.Max(0,w-spacing);}
        private static string F(float v)=>v.ToString("0.###",CultureInfo.InvariantCulture);
        // Scales then offsets an absolute M/L/H/V/Z path.
        public static string Transform(string d,float scale,float dx,float dy)
        {
            var sb=new StringBuilder();int i=0;char cmd=' ';var nums=new List<float>();
            void Flush()
            {
                if(cmd==' ')return;sb.Append(cmd);
                if(cmd=='H')for(int k=0;k<nums.Count;k++)sb.Append(F(nums[k]*scale+dx)).Append(' ');
                else if(cmd=='V')for(int k=0;k<nums.Count;k++)sb.Append(F(nums[k]*scale+dy)).Append(' ');
                else for(int k=0;k+1<nums.Count;k+=2)sb.Append(F(nums[k]*scale+dx)).Append(' ').Append(F(nums[k+1]*scale+dy)).Append(' ');
                nums.Clear();
            }
            while(i<d.Length)
            {
                char c=d[i];
                if(char.IsLetter(c)){Flush();cmd=char.ToUpperInvariant(c);if(cmd!='M'&&cmd!='L'&&cmd!='H'&&cmd!='V'&&cmd!='Z')throw new FormatException("Glyph paths use M L H V Z only: "+c);i++;continue;}
                if(char.IsDigit(c)||c=='-'||c=='.'){int s=i;i++;while(i<d.Length&&(char.IsDigit(d[i])||d[i]=='.'))i++;nums.Add(float.Parse(d.Substring(s,i-s),CultureInfo.InvariantCulture));continue;}
                i++;
            }
            Flush();return sb.ToString().Trim();
        }
        // A text run as stroked paths, its cap height `height`, starting at (x, y) top-left; returns its width.
        public static float Text(StringBuilder sb,string text,float x,float y,float height,float spacing,string color,float stroke)
        {
            float scale=height/GlyphHeight,cx=x;
            foreach(char ch in text)
            {
                if(glyphs.TryGetValue(ch,out var g))sb.Append("<path d=\"").Append(Transform(g.D,scale,cx,y)).Append("\" fill=\"none\" stroke=\"").Append(color).Append("\" stroke-width=\"").Append(F(stroke)).Append("\"/>");
                cx+=Advance(ch,spacing)*scale;
            }
            return Width(text,spacing)*scale;
        }
        // One wordmark letter centred on a 40-unit square (the home screen draws the title letter by letter with
        // NativeIcons.DrawMarkup, white, tinted per word). `stroke` in view-box units.
        public static string Letter(char c,float stroke)
        {
            if(!glyphs.TryGetValue(c,out var g))return "";float dx=(GlyphHeight-g.W)/2;
            return "<path d=\""+Transform(g.D,1,dx,0)+"\" fill=\"none\" stroke=\"#fff\" stroke-width=\""+F(stroke)+"\"/>";
        }
        public static float LetterWidth(char c)=>glyphs.TryGetValue(c,out var g)?g.W:0;
        // ---- the holo screens' ads (100-unit square, top-left origin) ----
        public const int AdPixels=384;
        // The screen's (u0, v0, du, dv) of the square texture (bottom row first, as NativeIconRaster writes it).
        public static (float u0,float v0,float du,float dv) AdUv(string kind)=>kind=="chip"?(0f,.125f,1f,.75f):kind=="banner"?(1/3f,0f,1/3f,1f):kind=="sign"?(.32f,0f,.36f,1f):(0f,0f,1f,1f);
        public static string Ad(string kind)
        {
            var sb=new StringBuilder();
            switch(kind)
            {
                case "siren":
                    sb.Append("<rect x=\"0\" y=\"0\" width=\"100\" height=\"100\" fill=\"#140826\"/><circle cx=\"50\" cy=\"40\" r=\"32\" fill=\"#2c1554\"/><circle cx=\"50\" cy=\"40\" r=\"22\" fill=\"#3e1f72\"/>");
                    // the Boss Test skull, from primitives: cranium, jaw, sockets with magenta eyes, nose, teeth
                    sb.Append("<circle cx=\"50\" cy=\"36\" r=\"17\" fill=\"#efe9ff\"/><rect x=\"38\" y=\"40\" width=\"24\" height=\"16\" rx=\"3\" fill=\"#efe9ff\"/>");
                    sb.Append("<circle cx=\"43\" cy=\"38\" r=\"5\" fill=\"#120a1e\"/><circle cx=\"57\" cy=\"38\" r=\"5\" fill=\"#120a1e\"/><circle cx=\"43\" cy=\"38\" r=\"1.8\" fill=\"#ff4fd8\"/><circle cx=\"57\" cy=\"38\" r=\"1.8\" fill=\"#ff4fd8\"/>");
                    sb.Append("<path d=\"M50 43L47.5 47.5H52.5Z\" fill=\"#120a1e\"/><path d=\"M43 52H57M46 52V56M50 52V56M54 52V56\" fill=\"none\" stroke=\"#120a1e\" stroke-width=\"1.1\"/>");
                    {float w=Width("THE SIREN",9)*(9/GlyphHeight);Text(sb,"THE SIREN",50-w/2,74,9,9,"#f4ecff",1.4f);}
                    {float w=Width("BOSS TEST",9)*(5.5f/GlyphHeight);Text(sb,"BOSS TEST",50-w/2,88,5.5f,9,"#ff6be3",1.1f);}
                    break;
                case "arena":
                    sb.Append("<rect x=\"0\" y=\"0\" width=\"100\" height=\"100\" fill=\"#03121a\"/>");
                    {float w=Width("CHIP",12)*(20/GlyphHeight);Text(sb,"CHIP",50-w/2,18,20,12,"#5ff4ff",2.2f);}
                    {float w=Width("ARENA",11)*(16/GlyphHeight);Text(sb,"ARENA",50-w/2,46,16,11,"#ff4fd8",1.9f);}
                    sb.Append("<rect x=\"18\" y=\"72\" width=\"64\" height=\"1.4\" fill=\"#5ff4ff\"/><rect x=\"18\" y=\"72\" width=\"10\" height=\"1.4\" fill=\"#ff4fd8\"/>");
                    break;
                case "chip":
                    sb.Append("<rect x=\"0\" y=\"12.5\" width=\"100\" height=\"75\" fill=\"#050812\"/>");
                    for(int k=0;k<7;k++){float x=10+k*5.6f;sb.Append("<rect x=\""+F(x)+"\" y=\"20\" width=\"2\" height=\"6\" fill=\"#c9a24a\"/><rect x=\""+F(x)+"\" y=\"66\" width=\"2\" height=\"6\" fill=\"#c9a24a\"/>");}
                    sb.Append("<rect x=\"6\" y=\"26\" width=\"44\" height=\"40\" fill=\"#0c2a28\" stroke=\"#c9a24a\" stroke-width=\"1\"/><rect x=\"13\" y=\"32\" width=\"30\" height=\"28\" fill=\"#8c939d\"/>");
                    sb.Append("<rect x=\"19\" y=\"38\" width=\"18\" height=\"16\" fill=\"#5ff4ff\"/><rect x=\"23.5\" y=\"42\" width=\"9\" height=\"8\" fill=\"none\" stroke=\"#04161a\" stroke-width=\"1.2\"/>");
                    Text(sb,"RICOCHET",55,33,6.2f,8,"#e8fbff",1.1f);Text(sb,"RARE",55,46,4.6f,8,"#70b5ff",.9f);
                    sb.Append("<rect x=\"55\" y=\"58\" width=\"36\" height=\"1.2\" fill=\"#5ff4ff\"/>");
                    break;
                case "banner":
                    sb.Append("<rect x=\"33.4\" y=\"0\" width=\"33.2\" height=\"100\" fill=\"#090212\"/>");
                    {int i=0;foreach(char c in "CHIP"){float gw=LetterWidth(c)*(16/GlyphHeight);Text(sb,c.ToString(),50-gw/2,8+i*23,16,0,"#ff4fd8",1.8f);i++;}}
                    break;
                case "sign":
                    sb.Append("<rect x=\"32\" y=\"0\" width=\"36\" height=\"100\" fill=\"#120018\"/>");
                    {int i=0;foreach(char c in "ARENA"){float gw=LetterWidth(c)*(13/GlyphHeight);Text(sb,c.ToString(),50-gw/2,6+i*19,13,0,"#ff6be3",1.7f);i++;}}
                    break;
            }
            return sb.ToString();
        }
    }
}
