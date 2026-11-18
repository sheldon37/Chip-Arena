using System;
using System.Globalization;
using System.Text;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// ATLAS's one split-flap glyph atlas (docs/EXPEDITION-OVERHAUL.md W2): a flap tile for every glyph on the drum
    /// (NativeAtlasBoard.Drum, the station's chamfered monoline capitals from NativeDeployArt.Strokes, set bold for a flap), a
    /// blank emblem flap and every registry tileset's emblem (NativeTilesets markup). Every flap is drawn the same: a charcoal face
    /// with a sheen on each half, rounded corners, the dark split across its middle and the glyph in white (the draw tints it per
    /// line). Rasterised by NativeIconRaster (Art mode), engine-free and deterministic; Editor/NativeDeployCardBuild.cs bakes it
    /// once per build under <see cref="Key"/> (which changes with its inputs), and the station rasterises it on a worker when
    /// no bake matches. Straight RGBA floats, bottom row first (a Unity texture's order).
    /// </summary>
    public static class NativeAtlasArt
    {
        public const int TilePixels=96,Across=8;
        public static int Rows=>(NativeAtlasBoard.Tiles+Across-1)/Across;
        public static int Width=>Across*TilePixels;
        public static int Height=>Rows*TilePixels;
        public static int Bytes=>Width*Height*4;
        /// <summary>The glyph's height and its stroke on a flap (shares of the tile), and the split line's rows.</summary>
        public const float GlyphShare=.6f,StrokeShare=.135f;
        private static string F(float v)=>v.ToString("0.###",CultureInfo.InvariantCulture);

        /// <summary>Tile `tile`'s flap face in uv (u0, v0, u1, v1): a glyph flap is FlapAspect as wide as it is tall, centred; an
        /// emblem's is the square; one texel in from the tile's edge.</summary>
        public static void Rect(int tile,bool emblem,out float u0,out float v0,out float u1,out float v1)
        {
            int col=tile%Across,row=tile/Across,rowUp=Rows-1-row;float half=emblem?TilePixels/2f-1:TilePixels*NativeAtlasBoard.FlapAspect/2;
            u0=(col*TilePixels+TilePixels/2f-half)/Width;u1=(col*TilePixels+TilePixels/2f+half)/Width;v0=(rowUp*TilePixels+1)/(float)Height;v1=(rowUp*TilePixels+TilePixels-1)/(float)Height;
        }
        private static void Lin(int hex,out float r,out float g,out float b){r=((hex>>16)&255)/255f;g=((hex>>8)&255)/255f;b=(hex&255)/255f;}
        // The flap face (tile pixels, y up from the tile's bottom): outside it the cabinet's black; inside a charcoal sheen on each
        // half (lighter at its top), rounded corners, the split's dark gap and a fine highlight on the lower flap's edge.
        private static void Face(float[] o,int tile,bool emblem)
        {
            int px=TilePixels;float half=emblem?px/2f-1:px*NativeAtlasBoard.FlapAspect/2,x0=px/2f-half,x1=px/2f+half,y0=1,y1=px-1,mid=px/2f,radius=emblem?6:5;
            Lin(0x08090b,out float br,out float bg,out float bb);
            int col=tile%Across,rowUp=Rows-1-tile/Across;
            for(int y=0;y<px;y++)for(int x=0;x<px;x++)
            {
                float fx=x+.5f,fy=y+.5f;float dx=Math.Max(0,Math.Max(x0+radius-fx,fx-(x1-radius))),dy=Math.Max(0,Math.Max(y0+radius-fy,fy-(y1-radius)));
                float edge=radius-(float)Math.Sqrt(dx*dx+dy*dy);float inside=Math.Max(0,Math.Min(1,edge+.5f));
                if(fx<x0-.5f||fx>x1+.5f||fy<y0-.5f||fy>y1+.5f)inside=0;
                bool top=fy>mid;float t=top?(fy-mid)/(y1-mid):(fy-y0)/(mid-y0);
                float r,g,b;Lin(top?0x2c2f37:0x24262d,out float r0,out float g0,out float b0);Lin(top?0x1b1d22:0x15171b,out float r1,out float g1,out float b1);
                r=r1+(r0-r1)*t;g=g1+(g0-g1)*t;b=b1+(b0-b1)*t;
                // the split: a dark gap, the upper flap's shadowed lip above it, the lower flap's lit edge below
                float d=fy-mid;if(Math.Abs(d)<1.1f){r=g=b=.02f;}else if(d>0&&d<2.6f){r*=.7f;g*=.7f;b*=.7f;}else if(d<0&&d>-2.4f){r+=.05f;g+=.05f;b+=.055f;}
                int i=((rowUp*px+y)*Width+col*px+x)*4;
                o[i]=br+(r-br)*inside;o[i+1]=bg+(g-bg)*inside;o[i+2]=bb+(b-bb)*inside;o[i+3]=1;
            }
        }
        // `art` (a TilePixels square, straight RGBA, bottom row first) over tile `tile`, its colour forced to white times `ink`
        // (the draw tints the glyph per line), the split's gap kept dark through it.
        private static void Ink(float[] o,int tile,float[] art,float ink)
        {
            int px=TilePixels,col=tile%Across,rowUp=Rows-1-tile/Across;float mid=px/2f;
            for(int y=0;y<px;y++)for(int x=0;x<px;x++)
            {
                float a=art[(y*px+x)*4+3]*ink;if(a<=0)continue;if(Math.Abs(y+.5f-mid)<1.1f)a*=.04f;
                int i=((rowUp*px+y)*Width+col*px+x)*4;o[i]+=(1-o[i])*a;o[i+1]+=(1-o[i+1])*a;o[i+2]+=(1-o[i+2])*a;
            }
        }
        /// <summary>A drum glyph as SVG strokes on the 100-unit tile: GlyphShare tall, centred, stroke StrokeShare of its height.</summary>
        public static string GlyphMarkup(char c)
        {
            var sb=new StringBuilder();float h=GlyphShare*100,k=h/NativeDeployArt.GlyphHeight,w=(NativeDeployArt.Advance(c)-NativeDeployArt.GlyphSpacing)*k,x=50-w/2,top=50-h/2,sw=StrokeShare*h;
            foreach(var (a0,b0,a1,b1) in NativeDeployArt.Strokes(c))
                sb.Append("<line x1=\"").Append(F(x+a0*k)).Append("\" y1=\"").Append(F(top+b0*k)).Append("\" x2=\"").Append(F(x+a1*k)).Append("\" y2=\"").Append(F(top+b1*k)).Append("\" stroke=\"#fff\" stroke-width=\"").Append(F(sw)).Append("\"/>");
            return sb.ToString();
        }
        private static float[] Raster(string svg)=>NativeIconRaster.Raster(NativeIconRaster.Parse(svg,1,NativeIconRaster.Mode.Art),TilePixels,100);
        /// <summary>The atlas: Width x Height RGBA floats, bottom row first.</summary>
        public static float[] Atlas()
        {
            var o=new float[Bytes];
            for(int i=0;i<o.Length;i+=4){o[i]=o[i+1]=o[i+2]=.031f;o[i+3]=1;}
            for(int g=0;g<NativeAtlasBoard.Glyphs;g++)
            {
                Face(o,g,false);char c=NativeAtlasBoard.Drum[g];if(c==' ')continue;
                Ink(o,g,Raster(GlyphMarkup(c)),1);
            }
            Face(o,NativeAtlasBoard.BlankEmblem,true);
            for(int k=0;k<NativeTilesets.All.Length;k++)
            {
                int tile=NativeAtlasBoard.EmblemTile(k);Face(o,tile,true);var e=NativeTilesets.All[k].Emblem;
                if(!string.IsNullOrEmpty(e))Ink(o,tile,Raster("<g>"+e+"</g>"),.96f);
            }
            return o;
        }
        /// <summary>The bake's Resources key: it changes with everything the atlas is drawn from, so a stale bake is never loaded.</summary>
        public static string Key
        {
            get
            {
                var sb=new StringBuilder("atlas-flaps-v1|").Append(NativeAtlasBoard.Drum).Append('|').Append(TilePixels).Append('|').Append(Across).Append('|').Append(F(NativeAtlasBoard.FlapAspect)).Append('|').Append(F(GlyphShare)).Append('|').Append(F(StrokeShare));
                foreach(var t in NativeTilesets.All)sb.Append('|').Append(t.Id).Append(':').Append(t.Emblem);
                foreach(char ch in NativeAtlasBoard.Drum)foreach(var (a0,b0,a1,b1) in NativeDeployArt.Strokes(ch))sb.Append(F(a0)).Append(F(b0)).Append(F(a1)).Append(F(b1));
                uint h=2166136261u;foreach(char ch in sb.ToString()){h^=ch;h*=16777619u;}
                return NativeDeployCardCache.Folder+"atlas-flaps-"+h.ToString("x8",CultureInfo.InvariantCulture);
            }
        }
        /// <summary>The atlas quantised to bytes (the bake) and back.</summary>
        public static byte[] Pack(float[] pixels)=>NativeDeployCardCache.Pack(pixels);
        public static float[] Unpack(byte[] bytes)
        {
            if(bytes==null||bytes.Length!=Bytes)throw new FormatException("Invalid ATLAS flap atlas");var o=new float[bytes.Length];for(int i=0;i<o.Length;i++)o[i]=bytes[i]/255f;return o;
        }
    }
}
