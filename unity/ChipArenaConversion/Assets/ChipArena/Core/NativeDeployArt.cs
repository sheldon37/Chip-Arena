using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ChipArena.UnityConversion
{
    // The Deployment Station's screen art (docs/EXPEDITION-DEPLOYMENT.md "Presentation"): the reel symbols (each destination's
    // emblem and name on the instance machine's dark enamel band), the mutator cards (polarity border, label and icon, the name),
    // the wild-card back the dials show when nothing is rolled, and the PENDING / ACTIVE stamps. SVG markup rasterised by
    // NativeIconRaster (Art mode: knockouts and opacity) over a procedural backdrop, engine-free, so the checks and the offline
    // renders see the pixels the player sees and no texture asset ships. Pixels are straight RGBA floats, bottom row first
    // (NativeIconRaster's order, a Unity texture's), tile 0 at the bottom of a strip (NativeDeployStage's _Scroll counts up).
    public static class NativeDeployArt
    {
        public const int TilePixels=256,StampRows=94;
        /// <summary>The strip entry for the dials' wild-card back (no mutator: before a roll, or skipped).</summary>
        public const string Back="#back";
        private static string F(float v)=>v.ToString("0.##",CultureInfo.InvariantCulture);
        // ---- the lettering: chamfered monoline capitals (the CHIP ARENA wordmark's style, NativeRooftopArt, completed to A-Z),
        // a 40-unit-tall cell, y down; absolute M/L/H/V/Z paths. The models build each stroke as a bar, the tiles as an SVG stroke.
        private static readonly Dictionary<char,(float w,string d)> Glyphs=new Dictionary<char,(float,string)>
        {
            ['A']=(22,"M1 39V11L11 1L21 11V39M1 25H21"),['B']=(21,"M1 39V1H13L19 7V14L13 20H1M13 20L21 26V33L15 39H1"),['C']=(22,"M22 1H7L1 7V33L7 39H22"),
            ['D']=(22,"M1 1V39H14L21 32V8L14 1Z"),['E']=(20,"M20 1H1V39H20M1 20H15"),['F']=(20,"M20 1H1V39M1 20H15"),['G']=(22,"M21 7L15 1H7L1 7V33L7 39H15L21 33V22H12"),
            ['H']=(22,"M1 1V39M21 1V39M1 20H21"),['I']=(2,"M1 1V39"),['J']=(20,"M19 1V33L13 39H7L1 33"),['K']=(21,"M1 1V39M21 1L1 24M8 17L21 39"),['L']=(19,"M1 1V39H19"),
            ['M']=(24,"M1 39V1L12 18L23 1V39"),['N']=(22,"M1 39V1L21 39V1"),['O']=(22,"M7 1H15L21 7V33L15 39H7L1 33V7Z"),['P']=(21,"M1 39V1H15L21 7V15L15 21H1"),
            ['Q']=(22,"M7 1H15L21 7V33L15 39H7L1 33V7ZM14 31L22 40"),['R']=(21,"M1 39V1H15L21 7V15L15 21H1M11 21L21 39"),['S']=(21,"M21 7L15 1H7L1 7V14L7 20H15L21 26V33L15 39H7L1 33"),
            ['T']=(22,"M1 1H21M11 1V39"),['U']=(22,"M1 1V33L7 39H15L21 33V1"),['V']=(22,"M1 1L11 39L21 1"),['W']=(26,"M1 1L7 39L13 15L19 39L25 1"),
            ['X']=(22,"M1 1L21 39M21 1L1 39"),['Y']=(22,"M1 1L11 20L21 1M11 20V39"),['Z']=(21,"M1 1H21L1 39H21"),['0']=(22,"M7 1H15L21 7V33L15 39H7L1 33V7ZM4 34L18 6"),
            ['1']=(12,"M3 7L9 1V39M3 39H15"),['2']=(21,"M1 7L7 1H15L21 7V15L1 39H21"),['3']=(21,"M1 1H21L11 17H15L21 23V33L15 39H7L1 33"),
            ['4']=(22,"M15 39V1L1 27H22"),['5']=(21,"M21 1H1V18H15L21 24V33L15 39H1"),['6']=(21,"M19 1H9L1 9V33L7 39H15L21 33V24L15 18H1"),
            ['7']=(21,"M1 1H21L7 39"),['8']=(21,"M7 1H15L20 6V14L15 19H7L2 14V6ZM7 19H15L21 25V33L15 39H7L1 33V25Z"),['9']=(21,"M21 21H7L1 15V7L7 1H15L21 7V31L13 39H3"),
            ['-']=(14,"M1 21H13"),['.']=(2,"M1 37V39"),['·']=(4,"M1 20H3"),
        };
        public const float GlyphHeight=40,GlyphSpacing=9;
        public static bool Lettered(char c)=>c==' '||Glyphs.ContainsKey(char.ToUpperInvariant(c));
        public static float Advance(char c)=>c==' '?GlyphSpacing*2:Glyphs.TryGetValue(char.ToUpperInvariant(c),out var g)?g.w+GlyphSpacing:0;
        /// <summary>A line's width in glyph units (40 tall).</summary>
        public static float TextWidth(string text){float w=0;foreach(char c in text??"")w+=Advance(c);return Math.Max(0,w-GlyphSpacing);}
        /// <summary>A glyph's strokes (glyph units, y down, from its cell's left edge).</summary>
        public static List<(float x0,float y0,float x1,float y1)> Strokes(char c)
        {
            var o=new List<(float,float,float,float)>();if(!Glyphs.TryGetValue(char.ToUpperInvariant(c),out var g))return o;
            float x=0,y=0,sx=0,sy=0;int i=0;string d=g.d;char cmd=' ';
            float N(){while(i<d.Length&&(d[i]==' '||d[i]==','))i++;int s0=i;if(i<d.Length&&d[i]=='-')i++;while(i<d.Length&&(char.IsDigit(d[i])||d[i]=='.'))i++;return float.Parse(d.Substring(s0,i-s0),CultureInfo.InvariantCulture);}
            while(i<d.Length)
            {
                char k=d[i];if(char.IsLetter(k)){cmd=k;i++;if(cmd=='Z'){o.Add((x,y,sx,sy));x=sx;y=sy;}continue;}
                if(k==' '||k==','){i++;continue;}
                switch(cmd)
                {
                    case 'M':x=N();y=N();sx=x;sy=y;cmd='L';break;
                    case 'L':{float nx=N(),ny=N();o.Add((x,y,nx,ny));x=nx;y=ny;break;}
                    case 'H':{float nx=N();o.Add((x,y,nx,y));x=nx;break;}
                    case 'V':{float ny=N();o.Add((x,y,x,ny));y=ny;break;}
                    default:i++;break;
                }
            }
            return o;
        }
        // A line of capitals as SVG strokes: centred on (cx, cy), `height` units tall, stroke `weight` of the height.
        private static void Text(StringBuilder sb,string text,float cx,float cy,float height,int color,float weight=.12f,float opacity=1)
        {
            float k=height/GlyphHeight,x=cx-TextWidth(text)*k/2,top=cy-height/2;
            foreach(char c in text)
            {
                if(Glyphs.TryGetValue(char.ToUpperInvariant(c),out var g))
                    sb.Append("<path d=\"").Append(NativeRooftopArt.Transform(g.d,k,x,top)).Append("\" fill=\"none\" stroke=\"").Append(Hex(color)).Append("\" stroke-width=\"").Append(F(height*weight)).Append("\" opacity=\"").Append(F(opacity)).Append("\"/>");
                x+=Advance(c)*k;
            }
        }
        private static string Hex(int c)=>"#"+(c&0xffffff).ToString("x6");
        /// <summary>A name split into one or two lines (at the space nearest the middle when it is long).</summary>
        public static string[] Lines(string name)
        {
            name=(name??"").Trim();if(name.Length<=10||name.IndexOf(' ')<0)return new[]{name};
            int best=-1;for(int i=0;i<name.Length;i++)if(name[i]==' '&&(best<0||Math.Abs(i-name.Length/2)<Math.Abs(best-name.Length/2)))best=i;
            return new[]{name.Substring(0,best),name.Substring(best+1)};
        }
        private static void Name(StringBuilder sb,string name,float cx,float cy,float width,float maxHeight,int color)
        {
            var lines=Lines(name);float longest=1;foreach(var l in lines)longest=Math.Max(longest,TextWidth(l));float h=Math.Min(maxHeight,width/longest*GlyphHeight);
            for(int i=0;i<lines.Length;i++)Text(sb,lines[i],cx,cy+(i-(lines.Length-1)*.5f)*h*1.45f,h,color);
        }
        // An emblem (NativeDeployPresentation.Destination.Emblem) centred at (50, 40) about 44 units across.
        private static string Emblem(string emblem,int c)
        {
            string C=Hex(c);var sb=new StringBuilder();
            switch(emblem)
            {
                case "biohazard":
                    foreach(int deg in new[]{-90,30,150}){double a=deg*Math.PI/180;sb.Append($"<circle cx=\"{F(50+11*(float)Math.Cos(a))}\" cy=\"{F(40+11*(float)Math.Sin(a))}\" r=\"13.5\" fill=\"{C}\"/>");}
                    foreach(int deg in new[]{-90,30,150}){double a=deg*Math.PI/180;sb.Append($"<circle cx=\"{F(50+15.5f*(float)Math.Cos(a))}\" cy=\"{F(40+15.5f*(float)Math.Sin(a))}\" r=\"9.5\" fill=\"#000\" data-knockout=\"1\"/>");}
                    foreach(int deg in new[]{90,210,330}){double a=deg*Math.PI/180;sb.Append($"<line x1=\"50\" y1=\"40\" x2=\"{F(50+9*(float)Math.Cos(a))}\" y2=\"{F(40+9*(float)Math.Sin(a))}\" stroke=\"#000\" stroke-width=\"2.2\" data-knockout=\"1\"/>");}
                    sb.Append("<circle cx=\"50\" cy=\"40\" r=\"4.2\" fill=\"#000\" data-knockout=\"1\"/>");
                    sb.Append($"<circle cx=\"50\" cy=\"40\" r=\"9.5\" fill=\"none\" stroke=\"{C}\" stroke-width=\"2.6\"/>");
                    break;
                case "rail":
                    sb.Append($"<circle cx=\"50\" cy=\"40\" r=\"16\" fill=\"none\" stroke=\"{C}\" stroke-width=\"7\"/>");
                    sb.Append($"<rect x=\"23\" y=\"35\" width=\"54\" height=\"10\" rx=\"1.5\" fill=\"{C}\"/>");
                    sb.Append("<rect x=\"30\" y=\"38.2\" width=\"40\" height=\"3.6\" fill=\"#000\" data-knockout=\"1\"/>");
                    break;
                case "anvil":
                    sb.Append($"<path d=\"M78 30 H30 C24 30 18 28 13 23 C15 32 22 38 30 40 H37 C40 44 42 48 42 52 H34 V59 H66 V52 H58 C58 46 61 42 69 40 C74 38 78 35 78 30 Z\" fill=\"{C}\"/>");
                    foreach(var (x,y,r) in new[]{(60f,18f,3.2f),(69f,12f,2.4f),(51f,11f,2f),(75f,21f,1.8f)})sb.Append($"<polygon points=\"{F(x)},{F(y-r)} {F(x+r)},{F(y)} {F(x)},{F(y+r)} {F(x-r)},{F(y)}\" fill=\"{C}\"/>");
                    break;
                case "crypt":
                    sb.Append($"<path d=\"M33 60 V34 C33 20 67 20 67 34 V60 Z\" fill=\"{C}\"/><path d=\"M40 60 V36 C40 27 60 27 60 36 V60 Z\" fill=\"#000\" data-knockout=\"1\"/>");
                    sb.Append($"<rect x=\"47.5\" y=\"8\" width=\"5\" height=\"16\" fill=\"{C}\"/><rect x=\"42\" y=\"12\" width=\"16\" height=\"4.5\" fill=\"{C}\"/>");
                    break;
                default:
                    sb.Append($"<path d=\"M40 30 C40 17 60 17 60 30 C60 38 50 39 50 47 V50\" fill=\"none\" stroke=\"{C}\" stroke-width=\"7\"/><circle cx=\"50\" cy=\"60\" r=\"4.5\" fill=\"{C}\"/>");
                    break;
            }
            return sb.ToString();
        }
        // A polarity icon (centred at 50, 44) for a mutator card.
        private static string Icon(NativeDeployPresentation.Polarity p,int c)
        {
            string C=Hex(c);
            switch(p)
            {
                case NativeDeployPresentation.Polarity.Boon:return $"<polyline points=\"36,50 50,36 64,50\" fill=\"none\" stroke=\"{C}\" stroke-width=\"6.5\"/><polyline points=\"36,62 50,48 64,62\" fill=\"none\" stroke=\"{C}\" stroke-width=\"6.5\" opacity=\".6\"/>";
                case NativeDeployPresentation.Polarity.Bane:return $"<polyline points=\"36,30 50,44 64,30\" fill=\"none\" stroke=\"{C}\" stroke-width=\"6.5\" opacity=\".6\"/><polyline points=\"36,42 50,56 64,42\" fill=\"none\" stroke=\"{C}\" stroke-width=\"6.5\"/>";
                case NativeDeployPresentation.Polarity.Chaos:return $"<path d=\"M55 24 L37 48 H48 L43 66 L64 40 H52 Z\" fill=\"{C}\"/>";
                default:return $"<path d=\"M42 36 C42 25 58 25 58 36 C58 43 50 44 50 50 V52\" fill=\"none\" stroke=\"{C}\" stroke-width=\"6\"/><circle cx=\"50\" cy=\"61\" r=\"4\" fill=\"{C}\"/>";
            }
        }
        private static float[] Raster(string svg)=>NativeIconRaster.Raster(NativeIconRaster.Parse(svg,1,NativeIconRaster.Mode.Art),TilePixels,100);
        // `top` over `under` (straight alpha), in place.
        private static void Over(float[] under,float[] top)
        {
            for(int i=0;i<under.Length;i+=4)
            {
                float a=top[i+3];if(a<=0)continue;float b=under[i+3]*(1-a),o=a+b;
                for(int k=0;k<3;k++)under[i+k]=o>0?(top[i+k]*a+under[i+k]*b)/o:0;under[i+3]=o;
            }
        }
        private static void Lin(int hex,out float r,out float g,out float b){r=((hex>>16)&255)/255f;g=((hex>>8)&255)/255f;b=(hex&255)/255f;}
        // The procedural backdrop: a vertical sheen from `mid` to `edge`, a soft glow of `glow` round (gx, gy) (units, y down),
        // brass separator rules at the tile's top and bottom when `rules`.
        private static float[] Backdrop(int mid,int edge,int glow,float glowAlpha,float gx,float gy,bool rules)
        {
            int px=TilePixels;var o=new float[px*px*4];Lin(mid,out float mr,out float mg,out float mb);Lin(edge,out float er,out float eg,out float eb);Lin(glow,out float gr,out float gg,out float gb);
            for(int y=0;y<px;y++)for(int x=0;x<px;x++)
            {
                float ux=(x+.5f)*100f/px,uy=100-(y+.5f)*100f/px,t=Math.Min(1,Math.Abs(uy-50)/50);t=t*t;
                float dx=ux-gx,dy=uy-gy,k=glowAlpha*(float)Math.Exp(-(dx*dx+dy*dy)/(30*30));
                float r=mr+(er-mr)*t+gr*k,g=mg+(eg-mg)*t+gg*k,b=mb+(eb-mb)*t+gb*k;
                if(rules){float d=Math.Min(uy,100-uy);if(d<2.2f){r=.16f;g=.11f;b=.05f;}else if(d<3.2f){r=.55f;g=.4f;b=.18f;}else if(d<4.4f){r*=.55f;g*=.55f;b*=.55f;}}
                int i=(y*px+x)*4;o[i]=Math.Min(1,r);o[i+1]=Math.Min(1,g);o[i+2]=Math.Min(1,b);o[i+3]=1;
            }
            return o;
        }
        private static int Dim(int hex,float k){int C(int s)=>(int)Math.Round(((hex>>s)&255)*k);return C(16)<<16|C(8)<<8|C(0);}
        private const int Ivory=0xf6ead4,Ink=0x0b0e14;

        /// <summary>A reel symbol: the destination's emblem in its colour over a dark enamel band with a glow behind it, its name
        /// in ivory capitals, brass rules where the symbols meet.</summary>
        public static float[] DestinationTile(string id,string theme)
        {
            var d=NativeDeployPresentation.DestinationOf(id,theme);var o=Backdrop(0x161b25,Ink,d.Color,.26f,50,40,true);
            Over(o,Raster(Emblem(d.Emblem,d.Color)));var sb=new StringBuilder();Name(sb,d.Name,50,Lines(d.Name).Length>1?79:80,82,13,Ivory);Over(o,Raster(sb.ToString()));
            return o;
        }
        /// <summary>A mutator card (its art in the tile's middle CardArt of the width): a polarity-tinted face with a border, the
        /// polarity label, its icon and the name.</summary>
        public static float[] MutatorTile(string id)
        {
            var m=NativeDeployPresentation.MutatorOf(id);int c=m.Color;var o=Backdrop(Dim(c,.16f),Dim(c,.06f),c,.22f,50,44,false);
            float l=50-NativeDeployModels.CardArt*50,r=50+NativeDeployModels.CardArt*50;var sb=new StringBuilder();
            sb.Append($"<rect x=\"{F(l+2.5f)}\" y=\"2.5\" width=\"{F(r-l-5)}\" height=\"95\" rx=\"5\" fill=\"none\" stroke=\"{Hex(c)}\" stroke-width=\"2.6\"/>");
            sb.Append($"<rect x=\"{F(l+6)}\" y=\"6\" width=\"{F(r-l-12)}\" height=\"88\" rx=\"3\" fill=\"none\" stroke=\"{Hex(c)}\" stroke-width=\".8\" opacity=\".55\"/>");
            Text(sb,NativeDeployPresentation.PolarityLabel(m.Polarity),50,14.5f,6.5f,c,.15f);
            sb.Append(Icon(m.Polarity,c));Name(sb,m.Name,50,Lines(m.Name).Length>1?79:81,r-l-16,11,Ivory);
            Over(o,Raster(sb.ToString()));Mask(o);return o;
        }
        /// <summary>The wild-card back: a violet lattice and a four-point star.</summary>
        public static float[] BackTile()
        {
            var o=Backdrop(0x1d1430,0x0c0816,0xb88cff,.3f,50,50,false);float l=50-NativeDeployModels.CardArt*50,r=50+NativeDeployModels.CardArt*50;var sb=new StringBuilder();
            for(int k=-6;k<=6;k++){float x=50+k*9;sb.Append($"<line x1=\"{F(x-50)}\" y1=\"0\" x2=\"{F(x+50)}\" y2=\"100\" stroke=\"#6d55a8\" stroke-width=\".7\" opacity=\".5\"/><line x1=\"{F(x+50)}\" y1=\"0\" x2=\"{F(x-50)}\" y2=\"100\" stroke=\"#6d55a8\" stroke-width=\".7\" opacity=\".5\"/>");}
            sb.Append($"<rect x=\"{F(l+2.5f)}\" y=\"2.5\" width=\"{F(r-l-5)}\" height=\"95\" rx=\"5\" fill=\"none\" stroke=\"#b88cff\" stroke-width=\"2.6\"/>");
            sb.Append("<path d=\"M50 24 L56 44 L74 50 L56 56 L50 76 L44 56 L26 50 L44 44 Z\" fill=\"#d4f2ff\"/><circle cx=\"50\" cy=\"50\" r=\"3\" fill=\"#b88cff\"/>");
            Over(o,Raster(sb.ToString()));Mask(o);return o;
        }
        // Outside the card's art the tile is the card's chrome edge colour (never seen; keeps the mip edges clean).
        private static void Mask(float[] o)
        {
            int px=TilePixels;float l=(50-NativeDeployModels.CardArt*50)/100*px,r=(50+NativeDeployModels.CardArt*50)/100*px;
            for(int y=0;y<px;y++)for(int x=0;x<px;x++)if(x<l||x>r){int i=(y*px+x)*4;o[i]=o[i+1]=o[i+2]=.08f;o[i+3]=1;}
        }
        /// <summary>A rubber stamp, StampRows high (the middle band of a tile): PENDING (amber) or ACTIVE (green), see-through round it.</summary>
        public static float[] StampTile(bool active)
        {
            int c=active?0x5dff9a:0xffb02e;string text=active?"ACTIVE":"PENDING";var sb=new StringBuilder();
            sb.Append($"<rect x=\"5\" y=\"33\" width=\"90\" height=\"34\" rx=\"4\" fill=\"{Hex(Dim(c,.12f))}\" opacity=\".7\" stroke=\"{Hex(c)}\" stroke-width=\"3.2\"/>");
            sb.Append($"<rect x=\"9\" y=\"37\" width=\"82\" height=\"26\" rx=\"2\" fill=\"none\" stroke=\"{Hex(c)}\" stroke-width=\"1\"/>");
            Text(sb,text,50,50,Math.Min(15,72/TextWidth(text)*GlyphHeight),c,.15f);
            var full=Raster(sb.ToString());int px=TilePixels,y0=(px-StampRows)/2;var o=new float[px*StampRows*4];Array.Copy(full,y0*px*4,o,0,o.Length);return o;
        }
        public const int GlowPixels=64;
        /// <summary>The soft glow (white, alpha a smooth radial falloff to zero at the edge): the floor's light pools.</summary>
        public static float[] GlowTile()
        {
            int px=GlowPixels;var o=new float[px*px*4];
            for(int y=0;y<px;y++)for(int x=0;x<px;x++){float dx=(x+.5f)/px*2-1,dy=(y+.5f)/px*2-1,r=Math.Min(1,(float)Math.Sqrt(dx*dx+dy*dy));float a=(1-r)*(1-r)*(1-r*.4f);int i=(y*px+x)*4;o[i]=o[i+1]=o[i+2]=1;o[i+3]=a;}
            return o;
        }
        /// <summary>Tiles stacked into one strip texture (TilePixels wide, the tiles' rows each; tile 0 at the bottom).</summary>
        public static float[] Stack(IReadOnlyList<float[]> tiles)
        {
            int n=0;foreach(var t in tiles)n+=t.Length;var o=new float[n];int at=0;foreach(var t in tiles){Array.Copy(t,0,o,at,t.Length);at+=t.Length;}return o;
        }
        /// <summary>A strip's tile (NativeDeployStage.Strip entries): a destination id with its theme, a mutator id, or Back.</summary>
        public static float[] Tile(string entry,bool mutator,string theme=null)=>entry==Back?BackTile():mutator?MutatorTile(entry):DestinationTile(entry,theme);
    }
}
