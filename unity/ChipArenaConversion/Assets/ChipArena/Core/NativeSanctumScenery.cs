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
        // One pane: a box in the window's plane at (u, v) of half sizes (hu, hv), turned `deg` in that plane, facing inward only.
        private static void Pane(NativeSceneryBuilder b,in NativeSanctumLooks.Window w,double u,double y,double hu,double hv,int hex,double level,double dPlane,double deg=0)
        {
            bool zn=w.Nz!=0;double x=w.X+(zn?u:0)-w.Nx*dPlane,z=w.Z+(zn?0:u)-w.Nz*dPlane;
            // only the face toward the hall: +z, -z, +x or -x (local; a turn in the window's plane keeps it)
            int keep=zn?(w.Nz>0?16:32):(w.Nx>0?1:2);int skip=63&~keep;
            double sx=zn?2*hu:.02,sz=zn?.02:2*hu;b.Box(x,y,z,sx,2*hv,sz,Darken(hex,.22),zn?0:-deg,0,zn?deg:0,hex,level,1,0,skip);
        }
        private static int Darken(int hex,double k){int C(int s)=>(int)Math.Round(((hex>>s)&255)*k);return C(16)<<16|C(8)<<8|C(0);}
        private static void Backing(NativeSceneryBuilder b,in NativeSanctumLooks.Window w,double u0,double u1,double y0,double y1)
        {
            bool zn=w.Nz!=0;double d=Plane+.025;double x=w.X+(zn?(u0+u1)/2:0)-w.Nx*d,z=w.Z+(zn?0:(u0+u1)/2)-w.Nz*d;int keep=zn?(w.Nz>0?16:32):(w.Nx>0?1:2);
            b.Box(x,(y0+y1)/2,z,zn?u1-u0:.02,y1-y0,zn?.02:u1-u0,0x0A090C,0,0,0,0,0,1,0,63&~keep);
        }
        private static double H(int a,int b,int c)=>NativeSanctumLooks.Hash(a,b,c);
        // A lancet's two lights: a border of pearls, a ground of diamond quarries, three medallions (a ring, four petals round a
        // jewel), small lozenges between them, a rosette in each light's head under the pointed arch.
        private static void Lancet(NativeSceneryBuilder b,in NativeSanctumLooks.Window w)
        {
            var pal=Palette(w.Design);double y0=w.Y-w.HalfY+.16,y1=w.Y+w.HalfY,hw=w.Half-.14,spring=y1-1.6;int seed=(int)Math.Round(w.X*7+w.Z*13);
            Backing(b,w,-hw,hw,y0,y1);
            double L=hw,rise=1.6,c=(rise*rise-L*L)/(2*L),R=L+c;
            double Arch(double y)=>y<=spring?hw:Math.Max(0,Math.Sqrt(Math.Max(0,R*R-(y-spring)*(y-spring)))-c);
            for(int light=-1;light<=1;light+=2)
            {
                double u0=light<0?-hw:.09,u1=light<0?-.09:hw,lw=u1-u0,cu=(u0+u1)/2,top=spring+.55;
                double[] my={y0+(top-y0)*.18,y0+(top-y0)*.5,y0+(top-y0)*.82};const double mr=.34;
                bool Inside(double u,double y,double m)=>Math.Abs(u)<=Arch(y)-m&&u>=u0+m&&u<=u1-m&&y>=y0+m&&y<=y1-m;
                // the pearled border
                for(double y=y0+.08;y<y1;y+=.16)foreach(double u in new[]{u0+.05,u1-.05}){if(!Inside(u,y,.03))continue;int k=(int)Math.Round((y-y0)/.16);Pane(b,w,u,y,.035,.035,(k%2==0?pal[1]:pal[2])[0],Level(w,(y-y0)/(y1-y0))*1.05,Plane,45);}
                // the ground's quarries, staggered, clear of the medallions
                const double a=.24;int rows=(int)((y1-y0)/(a/2))+2,cols=(int)(lw/a)+2;
                for(int j=0;j<rows;j++)for(int i=-1;i<cols;i++)
                {
                    double u=u0+.11+a*(i+.5*(j&1)),y=y0+.06+a/2*j;if(!Inside(u,y,.12))continue;double best=9;foreach(double m in my)best=Math.Min(best,Math.Sqrt((u-cu)*(u-cu)+(y-m)*(y-m)));if(best<mr+.08)continue;
                    if(y>top-.1&&Math.Abs(y-(spring+1.0))<.28&&Math.Abs(u-cu)<.28)continue;
                    int shade=(i+j)%5==0?0:1;var col=H(seed,i,j)<.08?pal[3]:pal[0];Pane(b,w,u,y,a*.35-Lead*.7,a*.35-Lead*.7,col[shade],Level(w,(y-y0)/(y1-y0))*(.8+.35*H(seed+light,i,j))*(shade==0?1.15:.9),Plane,45);
                }
                // the medallions and the lozenges between them
                for(int m=0;m<3;m++)
                {
                    double y=my[m],lv=Level(w,(y-y0)/(y1-y0))*1.1;int ring=m==1?2:1,petal=m==1?1:3;
                    for(int k=0;k<14;k++){double t=2*Math.PI*k/14;Pane(b,w,cu+Math.Cos(t)*mr,y+Math.Sin(t)*mr,.075,.035,pal[ring][k%2],lv,Plane,t*180/Math.PI+90);}
                    for(int k=0;k<4;k++){double t=Math.PI/2*k+Math.PI/4;Pane(b,w,cu+Math.Cos(t)*.15,y+Math.Sin(t)*.15,.1,.05,pal[petal][0],lv*1.1,Plane,t*180/Math.PI);}
                    for(int k=0;k<4;k++){double t=Math.PI/2*k;Pane(b,w,cu+Math.Cos(t)*.2,y+Math.Sin(t)*.2,.04,.04,pal[3][1],lv,Plane,45);}
                    Pane(b,w,cu,y,.065,.065,pal[2][0],lv*1.4,Plane,45);
                    if(m<2){double ym=(my[m]+my[m+1])/2;Pane(b,w,cu,ym,.09,.09,pal[2][0],lv,Plane,45);Pane(b,w,cu,ym,.045,.045,pal[1][0],lv*1.2,Plane,45);}
                }
                // the head's rosette
                double hy=spring+1.0;for(int k=0;k<6;k++){double t=2*Math.PI*k/6;Pane(b,w,cu+Math.Cos(t)*.14,hy+Math.Sin(t)*.14,.07,.04,pal[1][k%2],Level(w,.95)*1.2,Plane,t*180/Math.PI);}Pane(b,w,cu,hy,.05,.05,pal[2][0],Level(w,.95)*1.5,Plane,45);
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
