using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Substation 128's moving and lit parts for one frame (docs/arenas/CLUB.md "The look"), NativeBlockLit instances posed from
    /// <see cref="NativeClubShow"/> (the bar grid, the music's pulse and the hazards' replicated state) and the render clock:
    /// the LED dance floor's 270 tiles, the LED wall's 162 panels and the video cube's 64, the speaker stacks' cones (pumping
    /// with the kick, thrown out at the drop), the 16 moving heads aimed where the show points them, the laser wall's emitter
    /// posts riding their slots with the pass, the two mirror balls turning, the DJ behind the booth and the crowd in the
    /// galleries beyond the arches and in the pit beyond the barrier. Every part's linear colour is in <see cref="Rgb"/> (HDR:
    /// the bloom takes it past 1); NaN there keeps the kind's colour. Nothing here collides or is sim data.
    /// Allocation free (fixed capacity).
    /// </summary>
    public sealed class NativeClubRig
    {
        public const int Capacity=1800;
        public readonly NativeFactoryKit.Writer Parts;public readonly float[] Rgb=new float[Capacity*3];
        public int Tiles,Panels,Cones,Heads,Facets,Crowd;
        public NativeClubRig(NativeFactoryKit.Looks looks){Parts=new NativeFactoryKit.Writer(Capacity,false,looks);}
        private static double H(int a,int b=0,int c=0)=>NativeClubLooks.Hash(a,b,c);
        private static readonly double[] Sides={-1.0,1.0},LaserYs={.15,.35,.55},Horns={-.62,.62},Woofers={-.66,.66};
        private void Lit(double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,double r,double g,double b,bool shadow=false)
        {int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow,-1,1);if(Parts.Count>n){Rgb[n*3]=(float)r;Rgb[n*3+1]=(float)g;Rgb[n*3+2]=(float)b;}}
        private void Part(double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=false)
        {int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow,-1,1);if(Parts.Count>n){Rgb[n*3]=float.NaN;Rgb[n*3+1]=float.NaN;Rgb[n*3+2]=float.NaN;}}

        /// <summary>The frame: the show (built this frame), the render clock, the low tier (fewer facets and a thinner crowd),
        /// effects `quality` 0..1.</summary>
        public void Build(NativeClubShow s,double render,bool low,double quality)
        {
            Parts.Clear();Tiles=Panels=Cones=Heads=Facets=Crowd=0;if(s==null)return;var I=FactoryQ.Identity;
            // ---- the dance floor: each tile a hair over the glass, its colour the show's (telegraphs included: never reduced)
            for(int t=0;t<NativeClubLooks.Tiles;t++)
            {
                double x=NativeClubShow.TileX(t),z=NativeClubShow.TileZ(t);
                Lit(x,.007,z,.465,.006,.465,I,NativeClubLooks.Led,s.Floor[t*3],s.Floor[t*3+1],s.Floor[t*3+2]);Tiles++;
            }
            // ---- the LED wall (its face behind the stage) and the video cube's faces
            double pw=2*NativeClubLooks.WallX/NativeClubShow.WallW,ph=(NativeClubLooks.WallY1-NativeClubLooks.WallY0)/NativeClubShow.WallH;
            for(int row=0;row<NativeClubShow.WallH;row++)for(int c=0;c<NativeClubShow.WallW;c++)
            {int o=(c+row*NativeClubShow.WallW)*3;Lit(-NativeClubLooks.WallX+(c+.5)*pw,NativeClubLooks.WallY0+(row+.5)*ph,NativeClubLooks.WallZ-.012,pw/2-.03,ph/2-.03,.008,I,NativeClubLooks.Screen,s.Wall[o],s.Wall[o+1],s.Wall[o+2]);Panels++;}
            double ch=NativeClubLooks.CubeHalf,cy=NativeClubLooks.CubeY,chy=NativeClubLooks.CubeHalfY,cw=2*ch/NativeClubShow.CubeW,cv=2*chy/NativeClubShow.CubeH;
            for(int p=0;p<NativeClubShow.CubePanels;p++)
            {
                int side=p<NativeClubShow.CubeW*NativeClubShow.CubeH*4?p/(NativeClubShow.CubeW*NativeClubShow.CubeH):4,q=side<4?p%(NativeClubShow.CubeW*NativeClubShow.CubeH):p-NativeClubShow.CubeW*NativeClubShow.CubeH*4,c=q%NativeClubShow.CubeW,row=q/NativeClubShow.CubeW;
                double u=-ch+(c+.5)*cw,r=s.Cube[p*3],g=s.Cube[p*3+1],b=s.Cube[p*3+2],o=ch+.012;
                switch(side)
                {
                    case 0:Lit(o,cy-chy+(row+.5)*cv,u,.008,cv/2-.03,cw/2-.03,I,NativeClubLooks.Screen,r,g,b);break;
                    case 1:Lit(-o,cy-chy+(row+.5)*cv,-u,.008,cv/2-.03,cw/2-.03,I,NativeClubLooks.Screen,r,g,b);break;
                    case 2:Lit(-u,cy-chy+(row+.5)*cv,o,cw/2-.03,cv/2-.03,.008,I,NativeClubLooks.Screen,r,g,b);break;
                    case 3:Lit(u,cy-chy+(row+.5)*cv,-o,cw/2-.03,cv/2-.03,.008,I,NativeClubLooks.Screen,r,g,b);break;
                    default:Lit(u,cy-chy-.012,-ch+(row+.5)*cw,cw/2-.03,.008,cw/2-.03,I,NativeClubLooks.Screen,r,g,b);break;
                }
                Panels++;
            }
            Speakers(s,render);MovingHeads(s);LaserPosts(s);Balls(s,render,low);Dj(s,render);Crowds(s,render,low,quality);
        }

        // ---- the stacks: two woofers a cabinet (octagons: two turned boxes), surrounds and dust caps; horns in the top cabinet
        private void Speakers(NativeClubShow s,double render)
        {
            double beats=s.Beats,kick=Math.Exp(-NativeClubLooks.Frac(beats)*6);
            foreach(double side in Sides)
            {
                var st=side<0?s.DropW:s.DropE;double build=st.Phase==NativeFactoryHazards.Warning?st.Warn:0,burst=st.Phase==NativeFactoryHazards.Bursting?st.U:0;
                double x0=side*NativeClubLooks.SpeakerX0,x1=side*NativeClubLooks.SpeakerX1,cx=(x0+x1)/2,front=NativeClubLooks.SpeakerZ,cab=NativeClubLooks.SpeakerTop/4;
                // the cones pump with the kick; through the drop's warning they shake harder with each beat; the burst throws them out
                double pump=.015+.03*kick*(1+2*build)+.11*burst;
                for(int k=0;k<4;k++)
                {
                    double y=(k+.5)*cab;
                    if(k==3){foreach(double dx in Horns){Part(cx+dx,y,front-.06,.42,.28,.06,FactoryQ.Identity,NativeClubLooks.Dark);Part(cx+dx,y,front-.1,.3,.16,.02,FactoryQ.Identity,NativeClubLooks.Cone);}Cones+=4;continue;}
                    foreach(double dx in Woofers)
                    {
                        double ox=cx+dx,r=.44,depth=front-.02-pump;var t0=FactoryQ.Identity;var t45=FactoryQ.Axis(0,0,1,Math.PI/4);
                        Part(ox,y,front-.015,r,r*.414,.012,t0,NativeClubLooks.Rubber);Part(ox,y,front-.015,r,r*.414,.012,t45,NativeClubLooks.Rubber);Part(ox,y,front-.015,r*.414,r,.012,t0,NativeClubLooks.Rubber);
                        Part(ox,y,depth,r*.82,r*.34,.02,t0,NativeClubLooks.Cone);Part(ox,y,depth,r*.34,r*.82,.02,t0,NativeClubLooks.Cone);
                        Lit(ox,y,depth-.03,.1,.1,.02,t45,NativeClubLooks.Lens,.6+2.4*build+5*burst,.45+1.0*build+4*burst,.9+.3*build+5*burst);Cones+=6;
                    }
                }
            }
        }
        // ---- the moving heads: a clamp, the yoke turned to the pan, the head along the aim, its lens in the show's colour
        private void MovingHeads(NativeClubShow s)
        {
            for(int k=0;k<NativeClubLooks.Heads;k++)
            {
                double hx=s.HeadAt[k*3],hy=s.HeadAt[k*3+1],hz=s.HeadAt[k*3+2],dx=s.HeadAim[k*3]-hx,dy=s.HeadAim[k*3+1]-hy,dz=s.HeadAim[k*3+2]-hz,l=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(l<1e-3){dy=-1;l=1;}
                dx/=l;dy/=l;dz/=l;double yaw=Math.Atan2(dx,dz),pitch=Math.Asin(Math.Max(-1,Math.Min(1,-dy)));bool top=k>=12;double up=top?-1:1;
                var pan=FactoryQ.Axis(0,1,0,yaw);var aim=FactoryQ.YawPitch(yaw,pitch);
                Part(hx,hy+up*.32,hz,.16,.05,.16,FactoryQ.Identity,NativeClubLooks.Dark,true);
                pan.Rotate(.2,0,0,out double ax,out double ay,out double az);Part(hx+ax,hy+up*.15,hz+az,.025,.17,.06,pan,NativeClubLooks.Metal,true);Part(hx-ax,hy+up*.15,hz-az,.025,.17,.06,pan,NativeClubLooks.Metal,true);
                Part(hx,hy,hz,.15,.15,.24,aim,NativeClubLooks.Dark,true);
                aim.Rotate(0,0,.25,out double lx,out double ly,out double lz);double p=s.HeadPower[k]*3.2;
                Lit(hx+lx,hy+ly,hz+lz,.11,.11,.012,aim,NativeClubLooks.Lens,s.HeadColor[k*3]*p+.2,s.HeadColor[k*3+1]*p+.2,s.HeadColor[k*3+2]*p+.2);Heads++;
            }
        }
        // ---- the laser wall's two emitter posts: up out of their slots through the danger, riding with the pass, down after
        private void LaserPosts(NativeClubShow s)
        {
            var h=s.LaserHazard;if(h==null)return;var st=s.Laser;double rise=0,x;
            double near=h.center.x-st.Direction*h.size.x/2;
            if(st.Phase==NativeHazardPhase.Danger){rise=NativeClubLooks.Smooth(1-st.Until/Math.Max(.1,h.danger)*1.6);x=near;}
            else if(st.Phase==NativeHazardPhase.Active){rise=1;x=Math.Max(h.center.x-h.size.x/2,Math.Min(h.center.x+h.size.x/2,double.IsNaN(st.Head)?near:st.Head));}
            else if(st.Phase==NativeHazardPhase.Clear){rise=st.Level;x=h.center.x+st.Direction*h.size.x/2;}
            else return;
            if(rise<=.001)return;double top=NativeClubLooks.LaserTop*rise;
            foreach(double side in Sides)
            {
                double z=side*NativeClubLooks.LaserRailZ;Part(x,top/2-.01,z,.12,top/2+.01,.12,FactoryQ.Identity,NativeClubLooks.Dark,true);Part(x,top+.02,z,.15,.03,.15,FactoryQ.Identity,NativeClubLooks.Metal,true);
                foreach(double y in LaserYs)if(y<top-.04)Lit(x,y,z-side*.125,.05,.04,.008,FactoryQ.Identity,NativeClubLooks.Lens,5,.25,.3);
            }
        }
        // ---- the mirror balls: rings of facets turning; their pin spots and the specks they throw are the effects'
        public static double BallTurn(double render)=>render*.9;
        private void Balls(NativeClubShow s,double render,bool low)
        {
            int rings=low?5:7,per=low?10:14;double turn=BallTurn(render),R=NativeClubLooks.BallRadius;
            foreach(double side in Sides)
            {
                double bx=side*NativeClubLooks.BallX,by=NativeClubLooks.BallY,bz=NativeClubLooks.BallZ;
                Part(bx,by,bz,R*.92,R*.92,R*.92,FactoryQ.Identity,NativeClubLooks.Dark);
                for(int i=0;i<rings;i++)
                {
                    double lat=-Math.PI/2+(i+.5)*Math.PI/rings,cl=Math.Cos(lat);int n=Math.Max(4,(int)Math.Round(per*cl));
                    for(int j=0;j<n;j++)
                    {
                        double lon=turn*side+(j+.5)*2*Math.PI/n+i*.3;var q=FactoryQ.YawPitch(lon,-lat);q.Rotate(0,0,R,out double fx,out double fy,out double fz);
                        double size=Math.PI*R/rings*.46;Part(bx+fx,by+fy,bz+fz,size,size,.012,q,NativeClubLooks.Mirror);Facets++;
                    }
                }
            }
        }
        // ---- the DJ: nodding on the beat, a hand at the headphones, the other hand up through the build and the drop
        private void Dj(NativeClubShow s,double render)
        {
            double beats=s.Beats,nod=Math.Exp(-NativeClubLooks.Frac(beats)*5)*.05,x=NativeClubLooks.DjX,z=NativeClubLooks.DjZ,y0=NativeClubLooks.StageY;
            double lift=Math.Max(s.Rise,s.Blast>0?1:0);var I=FactoryQ.Identity;
            Part(x,y0+.45,z,.17,.45,.12,I,NativeClubLooks.Crowd,true);Part(x,y0+1.25,z-.02*nod,.22,.34,.13,I,NativeClubLooks.Crowd,true);
            Part(x,y0+1.73-nod,z-.04,.11,.13,.12,I,NativeClubLooks.Crowd,true);Part(x,y0+1.78-nod,z-.04,.13,.05,.05,I,NativeClubLooks.Metal);
            // right hand at the ear, left on the mixer or raised
            Part(x+.26,y0+1.5,z-.05,.05,.2,.05,FactoryQ.Axis(0,0,1,.4),NativeClubLooks.Crowd,true);
            double a=.3+2.2*lift+Math.Sin(beats*Math.PI)*.25*lift;var arm=FactoryQ.Axis(1,0,0,a);arm.Rotate(0,-.24,0,out double ox,out double oy,out double oz);
            Part(x-.27,y0+1.55+oy,z+oz,.05,.24,.05,arm,NativeClubLooks.Crowd,true);
        }
        // ---- the crowd: figures leaning on the galleries' rails beyond the arches (west, east) and packed in the pit beyond the
        //      barrier (north-west, north-east), bobbing on the beat, hands up as the drop builds and at the drop
        public static int CrowdCount(bool low)=>low?32:64;
        public static void CrowdAt(int k,out double x,out double y,out double z,out double yaw)
        {
            // 0..47 the galleries (24 a side: two rows of 12 along z), 48..63 the pit (8 a side)
            if(k<48){int side=k<24?-1:1,i=k%24,row=i/12,c=i%12;x=side*(27.6+row*.9+H(k,1)*.25);z=-16.5+c*3+row*1.5+(H(k,2)-.5)*.6;y=NativeClubLooks.GalleryY;yaw=side<0?Math.PI/2:-Math.PI/2;}
            else{int i=k-48,side=i<8?-1:1,j=i%8;x=side*(11.6+(j%4)*1.4+H(k,3)*.4);z=21.4+(j/4)*1.0+H(k,4)*.3;y=NativeClubScenery.PitFloor;yaw=Math.Atan2(-x*.4,-z+14)+Math.PI;}
        }
        private void Crowds(NativeClubShow s,double render,bool low,double quality)
        {
            int n=CrowdCount(low);double beats=s.Beats;double hype=Math.Max(s.Rise,s.Blast>0?1:0);
            for(int k=0;k<64;k++)
            {
                if(low&&(k&1)==1)continue;CrowdAt(k,out double x,out double y,out double z,out double yaw);
                double ph=H(k,7),bob=Math.Abs(Math.Sin((beats+ph*.3)*Math.PI))*.07*(1+hype),sway=Math.Sin(beats*Math.PI*.5+ph*6)*.06;
                double tall=.92+.16*H(k,9);var q=FactoryQ.Axis(0,1,0,yaw);var lean=q*FactoryQ.Axis(0,0,1,sway);
                q.Rotate(1,0,0,out double rx,out double ry,out double rz);
                Part(x,y+.42*tall+bob*.3,z,.16,.42*tall,.1,q,NativeClubLooks.Crowd);
                Part(x,y+1.1*tall+bob,z,.2,.3*tall,.11,lean,NativeClubLooks.Crowd);
                Part(x,y+1.55*tall+bob,z,.1,.12,.11,lean,NativeClubLooks.Crowd);
                bool up=hype>.2&&H(k,11)<.4+.6*hype||H(k,(int)Math.Floor(beats/8),13)<.12;
                foreach(double sd in Sides)
                {
                    double ax=x+rx*sd*.24,az=z+rz*sd*.24;
                    if(up){double wave=Math.Sin(beats*Math.PI+ph*4+sd)*.25;Part(ax,y+1.62*tall+bob,az,.045,.3,.045,q*FactoryQ.Axis(0,0,1,sd*(.25+wave)),NativeClubLooks.Crowd);}
                    else Part(ax,y+1.08*tall+bob,az,.045,.26,.045,q*FactoryQ.Axis(0,0,1,sd*.12),NativeClubLooks.Crowd);
                }
                Crowd++;
            }
            _=n;_=quality;_=render;
        }
    }
}
