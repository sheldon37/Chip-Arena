using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Hollow Sanctum's stained glass (docs/arenas/SANCTUM.md "The glass"): every window's panes baked into ChipArena/SourceLit
    /// sector meshes (Runtime draws them on NativeSceneryBuilder.BackdropLayer without shadows), the leading a dark backing plane
    /// behind panes cut 3 cm short of their cells. Each lancet's two lights carry a border, a ground and three medallions; each rose
    /// its rings, petals and its gold eye, in one of eight designs of a jewel palette. The emission is HDR (the bloom takes it): a
    /// window the moon faces glows brightest toward its head, one turned from it glows low with the sky. The panes stand in the
    /// openings of the walls, 8 m and more over every floor, so nothing here ever enters the play space. Deterministic;
    /// allocates only while building (once per map).
    /// </summary>
    public static class NativeSanctumScenery
    {
        /// <summary>Sectors: the south windows, the north windows, the ends (the four roses).</summary>
        public const int Sectors=3;
        public const double Plane=.3,Lead=.03;
        private static readonly int[] Ruby={0xD0182E,0x9A0C1E},Sapphire={0x2246D8,0x10288A},Emerald={0x1AAE52,0x0E6A34},Amber={0xF4A824,0xC07414},Violet={0x8A34C8,0x52207E},Pale={0xDCE4F4,0xB8C4DC};
        private static int[][] Palette(int design)
        {
            switch(design%8)
            {
                case 0:return new[]{Sapphire,Ruby,Amber,Pale};case 1:return new[]{Ruby,Sapphire,Amber,Emerald};case 2:return new[]{Emerald,Amber,Ruby,Pale};case 3:return new[]{Violet,Amber,Sapphire,Ruby};
                case 4:return new[]{Sapphire,Ruby,Amber,Emerald};case 5:return new[]{Ruby,Amber,Sapphire,Violet};case 6:return new[]{Sapphire,Amber,Ruby,Emerald};default:return new[]{Violet,Ruby,Amber,Sapphire};
            }
        }
        public static NativeSceneryMesh[] Build()
        {
            var b=new NativeSceneryBuilder(Sectors);
            foreach(var w in NativeSanctumLooks.Windows){b.Current=w.Rose?2:(w.Z<0?0:1);if(w.Rose)Rose(b,w);else Lancet(b,w);}
            var m=new NativeSceneryMesh[Sectors];for(int i=0;i<Sectors;i++)m[i]=b.Bake(i);return m;
        }
        /// <summary>The glass's light level at height share `v` (0 the sill .. 1 the head) for a window: the moon's share brightens
        /// it toward its head; a window turned from the moon keeps the sky's low glow.</summary>
        public static double Level(in NativeSanctumLooks.Window w,double v)
        {
            double moon=NativeSanctumLooks.MoonShare(w);return .55+moon*(1.7+1.3*v)+.25*v;
        }
        // One pane: a box in the window's plane at (u, v) of half sizes (hu, hv), facing inward only.
        private static void Pane(NativeSceneryBuilder b,in NativeSanctumLooks.Window w,double u,double y,double hu,double hv,int hex,double level,double dPlane)
        {
            bool zn=w.Nz!=0;double x=w.X+(zn?u:0)-w.Nx*dPlane,z=w.Z+(zn?0:u)-w.Nz*dPlane;
            // only the face toward the hall: +z, -z, +x or -x (local; the boxes are unrotated)
            int keep=zn?(w.Nz>0?16:32):(w.Nx>0?1:2);int skip=63&~keep;
            double sx=zn?2*hu:.02,sz=zn?.02:2*hu;b.Box(x,y,z,sx,2*hv,sz,Darken(hex,.22),0,0,0,hex,level,1,0,skip);
        }
        private static int Darken(int hex,double k){int C(int s)=>(int)Math.Round(((hex>>s)&255)*k);return C(16)<<16|C(8)<<8|C(0);}
        private static void Backing(NativeSceneryBuilder b,in NativeSanctumLooks.Window w,double u0,double u1,double y0,double y1)
        {
            bool zn=w.Nz!=0;double d=Plane+.025;double x=w.X+(zn?(u0+u1)/2:0)-w.Nx*d,z=w.Z+(zn?0:(u0+u1)/2)-w.Nz*d;int keep=zn?(w.Nz>0?16:32):(w.Nx>0?1:2);
            b.Box(x,(y0+y1)/2,z,zn?u1-u0:.02,y1-y0,zn?.02:u1-u0,0x0A090C,0,0,0,0,0,1,0,63&~keep);
        }
        private static double H(int a,int b,int c)=>NativeSanctumLooks.Hash(a,b,c);
        private static void Lancet(NativeSceneryBuilder b,in NativeSanctumLooks.Window w)
        {
            var pal=Palette(w.Design);double y0=w.Y-w.HalfY+.16,y1=w.Y+w.HalfY,hw=w.Half-.14;int seed=(int)Math.Round(w.X*7+w.Z*13);
            Backing(b,w,-hw,hw,y0,y1);
            const double cw=.245,ch=.3;
            for(int light=-1;light<=1;light+=2)
            {
                double u0=light<0?-hw:.09,u1=light<0?-.09:hw,lw=u1-u0;int cols=Math.Max(3,(int)Math.Round(lw/cw));double cu=lw/cols;
                int rows=(int)((y1-y0)/ch);
                for(int r=0;r<rows;r++)for(int c=0;c<cols;c++)
                {
                    double u=u0+cu*(c+.5),y=y0+ch*(r+.5),v=(y-y0)/(y1-y0);
                    int[] col;int shade=0;
                    // three medallions per light (circles), a border column at each side, the ground between
                    double best=9;double mcu=(u0+u1)/2;for(int m=0;m<3;m++){double my=y0+(y1-y0-1.8)*(m+.5)/3;double d=Math.Sqrt((u-mcu)*(u-mcu)/(lw*lw*.16)+(y-my)*(y-my)/.45);best=Math.Min(best,d);}
                    if(c==0||c==cols-1){col=(r%2==0)?pal[1]:pal[2];shade=r%4<2?0:1;}
                    else if(best<.55){col=pal[3];shade=H(seed,r,c)<.5?0:1;if(best<.3)col=pal[1];}
                    else if(best<.75){col=pal[2];shade=0;}
                    else{col=pal[0];shade=(r+c)%3==0?0:1;}
                    double lv=Level(w,v)*(.82+.36*H(seed+light,r,c))*(shade==0?1:.8);
                    if(y>y1-1.75){double sp=y-(y1-1.75);double a=Math.Max(0,hw-sp*hw/1.7);if(Math.Abs(u)>a+.05)continue;}
                    Pane(b,w,u,y,cu/2-Lead,ch/2-Lead,col[shade],lv,Plane);
                }
            }
        }
        private static void Rose(NativeSceneryBuilder b,in NativeSanctumLooks.Window w)
        {
            var pal=Palette(w.Design);double r=w.Half-.18;int seed=(int)Math.Round(w.X*5+w.Z*11);int spokes=w.Design%2==0?12:16;
            Backing(b,w,-r,r,w.Y-r,w.Y+r);
            const double cell=.27;int n=(int)Math.Ceiling(r/cell);
            for(int i=-n;i<n;i++)for(int j=-n;j<n;j++)
            {
                double u=(i+.5)*cell,v=(j+.5)*cell,rr=Math.Sqrt(u*u+v*v);if(rr>r-.05)continue;
                double a=Math.Atan2(v,u);if(a<0)a+=2*Math.PI;double sector=a/(2*Math.PI)*spokes,fs=sector-Math.Floor(sector);int si=(int)Math.Floor(sector);
                int[] col;int shade=0;double t=rr/r;
                if(t<.24)col=t<.12?pal[2]:pal[1];
                else if(t<.6){col=Math.Abs(fs-.5)<.28?pal[3]:pal[0];shade=si%2;}
                else if(t<.86){double pr=Math.Sqrt((t-.81)*(t-.81)*16+(fs-.5)*(fs-.5)*1.4);col=pr<.32?(si%2==0?pal[1]:pal[3]):pal[0];shade=pr<.32?0:1;}
                else{col=si%2==0?pal[1]:pal[2];shade=(int)(fs*3)%2;}
                double lv=Level(w,.5+v/(2*r))*(.85+.3*H(seed,i,j))*(shade==0?1.1:.85);
                Pane(b,w,u,w.Y+v,cell/2-Lead,cell/2-Lead,col[shade],lv,Plane);
            }
        }
    }
}
