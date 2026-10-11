using System;

namespace ChipArena.UnityConversion
{
    // Procedural textures for the effects overhaul (owner 2026-10-11: "The effects seem a bit outdated"), built in pure C#
    // with no UnityEngine and only exactly rounded double operations (+, -, *, /, sqrt, floor, abs, %) over integer-hashed
    // lattices (no Math.Sin/Exp/Pow), so a given seed gives the same bytes on every IEEE runtime (the managed check pins a
    // hash). Pure functions with no shared state: build them once at load (or on a worker thread) and upload them (RGBA32,
    // linear, bilinear, clamp); nothing here touches the match.

    public static class NativeVfxTextures
    {
        public const int SmokeSize=512,SmokeTiles=4,SmokeVariants=SmokeTiles*SmokeTiles;
        public const uint SmokeSeed=0x5a0e;
        /// <summary>Puffs fade to exactly 0 at this share of the tile's half-width from its centre, so every tile has a clear
        /// gutter (at least 4 px at 128 px tiles) and its border texels are 0 in every channel.</summary>
        public const double SmokeEdge=.92;
        /// <summary>Softness of the erosion edge in <see cref="Dissolve"/> (in life fraction).</summary>
        public const double DissolveSoft=.15;
        /// <summary>The light direction the self-shadow term is baked for, in tile space (x right, y up, z toward the viewer):
        /// from above and in front, so the top of a puff is brighter than its underside.</summary>
        public const double LightX=0,LightY=.75,LightZ=.66;

        /// <summary>
        /// The smoke puff atlas: size x size RGBA8, tiles x tiles puffs (default 512 px, 4 x 4 tiles of 128 px), 16 billowy
        /// variants from seeded blob clusters carved by fbm value noise, each with a soft round falloff to exactly 0 before
        /// the tile border. Layout: 4 bytes per texel (R, G, B, A), row-major, row 0 at the bottom (Unity's texture origin, so
        /// LoadRawTextureData with TextureFormat.RGBA32 takes it as is); tile t is column t % tiles, row t / tiles counted from
        /// the bottom (<see cref="TileRect"/>). Channels: R density (0 outside the puff); G a baked lighting term for light from
        /// above-front (Lambert on the density height field times a self-shadow from the density above, so a puff's top is
        /// brighter); B the erosion threshold, the life fraction at which the texel dissolves (low at the puff's thin edges
        /// and in noisy pockets, high in its core; see <see cref="Dissolve"/>); A an edge mask, 4R(1 - R), high where the puff
        /// is thin (rims and wisps, for rim light or a colour fringe). G, B and A are 0 wherever R is 0.
        /// </summary>
        public static byte[] SmokeAtlas(int size=SmokeSize,int tiles=SmokeTiles,uint seed=SmokeSeed)
        {
            if(tiles<1||size<tiles*8||size%tiles!=0)throw new ArgumentException("SmokeAtlas: size must be a multiple of tiles, at least 8 px per tile");
            int T=size/tiles;var data=new byte[size*size*4];var d=new double[T*T];var h=new double[T*T];var e=new double[T*T];double slope=T*.25*HeightSlope;
            for(int t=0;t<tiles*tiles;t++)
            {
                Puff(d,h,e,T,seed+(uint)t*0x9e37u);
                int ox=(t%tiles)*T,oy=(t/tiles)*T;
                for(int py=0;py<T;py++)for(int px=0;px<T;px++)
                {
                    double D=d[py*T+px];if(D<=0)continue;
                    // Lighting: the puff's billows as a height field (central differences), Lambert towards the light, times the
                    // transmittance of the density above (two taps towards the light, up the tile).
                    double gx=(At(h,T,px+1,py)-At(h,T,px-1,py))*slope,gy=(At(h,T,px,py+1)-At(h,T,px,py-1))*slope;
                    double nl=(-gx*LightX-gy*LightY+LightZ)/Math.Sqrt(gx*gx+gy*gy+1);
                    double above=.6*At(d,T,px,py+T/12)+.4*At(d,T,px,py+T/6),shadow=1/(1+1.4*above*above);
                    double G=Clamp01((.22+.78*Math.Max(0,nl))*(.4+.6*shadow));
                    // Erosion threshold: the core lasts longest; the puff's billow noise opens ragged pockets as it dissolves.
                    double B=Clamp01(.03+e[py*T+px]);
                    double A=Clamp01(4*D*(1-D));
                    int w=((oy+py)*size+ox+px)*4;data[w]=Byte(D);data[w+1]=Byte(G);data[w+2]=Byte(B);data[w+3]=Byte(A);
                    if(data[w]==0){data[w+1]=data[w+2]=data[w+3]=0;}
                }
            }
            return data;
        }
        /// <summary>The UV rectangle of atlas tile t (0..tiles²-1): u0, v0 at its bottom-left, u1, v1 at its top-right.</summary>
        public static void TileRect(int t,out double u0,out double v0,out double u1,out double v1,int tiles=SmokeTiles)
        {int i=((t%(tiles*tiles))+tiles*tiles)%(tiles*tiles);u0=(double)(i%tiles)/tiles;v0=(double)(i/tiles)/tiles;u1=u0+1.0/tiles;v1=v0+1.0/tiles;}
        /// <summary>
        /// The reference erosion for a puff at life fraction u (0..1): the texel's density times a soft step that is 1 while
        /// u is well below its threshold B and 0 once u passes it, so a puff thins from its edges and pockets inward and is
        /// fully gone at u = 1. A shader does the same: alpha = u < 1 ? R * smoothstep(0, 1, (B + soft - u*(1 + soft)) / soft) : 0.
        /// </summary>
        public static double Dissolve(double density,double threshold,double u,double soft=DissolveSoft)
        {if(u>=1)return 0;double e=Clamp01((threshold+soft-u*(1+soft))/soft);return density*e*e*(3-2*e);}

        /// <summary>
        /// A small tileable noise texture (size x size RGBA8, default 64; same byte layout as the atlas): four independent
        /// periodic fbm value-noise channels (R coarse, G medium, B fine, A cellular), wrapping seamlessly on both axes, for
        /// breaking up the shock shell's rim and flickering fire puffs without per-pixel noise math on Shader Model 3.5.
        /// </summary>
        public static byte[] NoiseTile(int size=64,uint seed=0x7e11)
        {
            if(size<8)throw new ArgumentException("NoiseTile: at least 8 px");
            var data=new byte[size*size*4];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                double u=(double)x/size,v=(double)y/size;int w=(y*size+x)*4;
                data[w]=Byte(Periodic(u,v,4,seed,3));data[w+1]=Byte(Periodic(u,v,8,seed^0x51u,3));data[w+2]=Byte(Periodic(u,v,16,seed^0xa7u,2));
                data[w+3]=Byte(PeriodicCells(u,v,6,seed^0x3c9u));
            }
            return data;
        }
        /// <summary>FNV-1a 64-bit hash of a byte array (determinism checks).</summary>
        public static ulong Hash(byte[] data){ulong h=14695981039346656037UL;foreach(byte b in data){h^=b;h*=1099511628211UL;}return h;}

        // ---- the puff shape ----
        // Height-field slope for the baked lighting (per unit of the billow height over the tile's half-width).
        private const double HeightSlope=.35;
        // One puff into d (density), h (billow height for the lighting) and e (the erosion noise term), T x T, row 0 at the
        // bottom: a cluster of dome-shaped blobs whose summed height is carved by fbm and billowy noise into a lumpy silhouette,
        // inside a round window. The noise only acts on the blobs (no stray islands in the gutter).
        private static void Puff(double[] d,double[] h,double[] e,int T,uint seed)
        {
            uint s=seed;double Next(){s=unchecked(s*1664525u+1013904223u);return s/4294967296.0;}
            int k=7+(int)(Next()*5);var bx=new double[k];var by=new double[k];var br=new double[k];
            // Blobs: a big central one, the rest around it, biased upward a little (billows pile up), all inside the window.
            double squash=.88+Next()*.24;
            for(int i=0;i<k;i++)
            {
                double a=Next()*Math.PI*2,r=i==0?Next()*.05:.2+Next()*.24,rad=i==0?.5+Next()*.08:.26+Next()*.16;
                double x=Cos(a)*r*squash,y=Sin(a)*r/squash+(i==0?-.03:.05);double reach=Math.Sqrt(x*x+y*y)+rad;if(reach>.9){double f=.9/reach;x*=f;y*=f;rad*=f;}
                bx[i]=x;by[i]=y;br[i]=rad;
            }
            double fx=Next()*64,fy=Next()*64;uint noise=s,detail=s^0x6d2bu;
            for(int py=0;py<T;py++)for(int px=0;px<T;px++)
            {
                double x=(px+.5)/T*2-1,y=(py+.5)/T*2-1,rr=Math.Sqrt(x*x+y*y);int o=py*T+px;
                d[o]=0;h[o]=0;e[o]=0;if(rr>=SmokeEdge)continue;
                double H=0;for(int i=0;i<k;i++){double dx=x-bx[i],dy=y-by[i],q=1-(dx*dx+dy*dy)/(br[i]*br[i]);if(q>0)H+=q*q;}
                if(H<=0)continue;
                double n=Fbm(x*2.6+fx,y*2.6+fy,noise,4),b=1-Math.Abs(2*Fbm(x*5.2+fy,y*5.2+fx,detail,3)-1);
                double field=H+((n-.5)*.9+(b-.5)*.35)*Smooth(H*6),window=1-Smooth((rr-.62)/(SmokeEdge-.62));
                d[o]=Clamp01(Smooth((field-.12)/.63)*(.8+.2*b)*window);
                h[o]=(H*.6+n*.5+b*.22)*window;e[o]=Clamp01((field-.12)/1.25)*.92+.4*(b-.5)+.2*(n-.5);
            }
        }
        private static double At(double[] d,int T,int x,int y)=>x<0||y<0||x>=T||y>=T?0:d[y*T+x];

        // ---- noise: integer-hashed lattices, smoothstep-interpolated ----
        private static double Lattice(int x,int y,uint seed)
        {
            unchecked{uint h=(uint)x*0x8da6b343u^(uint)y*0xd8163841u^seed*0xcb1ab31fu;h^=h>>13;h*=0x5bd1e995u;h^=h>>15;h*=0x27d4eb2du;h^=h>>16;return (h&0xffffff)/16777216.0;}
        }
        private static double Value(double x,double y,uint seed,int period=0)
        {
            int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y);double fx=x-ix,fy=y-iy;fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy);
            int x0=ix,x1=ix+1,y0=iy,y1=iy+1;if(period>0){x0=Wrap(x0,period);x1=Wrap(x1,period);y0=Wrap(y0,period);y1=Wrap(y1,period);}
            double a=Lattice(x0,y0,seed),b=Lattice(x1,y0,seed),c=Lattice(x0,y1,seed),e=Lattice(x1,y1,seed);
            return a+(b-a)*fx+(c-a)*fy+(a-b-c+e)*fx*fy;
        }
        private static int Wrap(int v,int p)=>((v%p)+p)%p;
        private static double Fbm(double x,double y,uint seed,int octaves)
        {double sum=0,amp=.5,norm=0;for(int i=0;i<octaves;i++){sum+=Value(x,y,seed+(uint)i*0x632bu)*amp;norm+=amp;amp*=.5;x*=2.03;y*=2.03;}return sum/norm;}
        // Worley F1 (nearest jittered cell point), scaled to about 0..1.
        private static double Cells(double x,double y,uint seed,int period=0)
        {
            int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y);double best=9;
            for(int j=-1;j<=1;j++)for(int i=-1;i<=1;i++)
            {
                int cx=ix+i,cy=iy+j,hx=period>0?Wrap(cx,period):cx,hy=period>0?Wrap(cy,period):cy;
                double px=cx+Lattice(hx,hy,seed),py=cy+Lattice(hx,hy,seed^0x9e3779b9u),dx=px-x,dy=py-y,dd=dx*dx+dy*dy;if(dd<best)best=dd;
            }
            return Clamp01(Math.Sqrt(best)/1.1);
        }
        private static double Periodic(double u,double v,int period,uint seed,int octaves)
        {double sum=0,amp=.5,norm=0;int p=period;for(int i=0;i<octaves;i++){sum+=Value(u*p,v*p,seed+(uint)i*0x632bu,p)*amp;norm+=amp;amp*=.5;p*=2;}return sum/norm;}
        private static double PeriodicCells(double u,double v,int period,uint seed)=>Cells(u*period,v*period,seed,period);

        // ---- small helpers (no Math.Sin/Cos/Exp/Pow in the per-texel path; the blob placement uses these polynomial ones) ----
        private static double Clamp01(double v)=>v<0?0:v>1?1:v;
        private static double Smooth(double x){x=Clamp01(x);return x*x*(3-2*x);}
        private static byte Byte(double v)=>(byte)Math.Floor(Clamp01(v)*255+.5);
        // Bhaskara-style sine on [0, 2pi) (max error ~.0016): enough for placing blobs, and identical on every IEEE runtime.
        private static double Sin(double a)
        {
            a%=2*Math.PI;if(a<0)a+=2*Math.PI;double sign=1;if(a>Math.PI){a-=Math.PI;sign=-1;}
            return sign*16*a*(Math.PI-a)/(5*Math.PI*Math.PI-4*a*(Math.PI-a));
        }
        private static double Cos(double a)=>Sin(a+Math.PI/2);
    }
}
