using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>A backdrop box drawn through NativeBlockLit (like a kit part, but world-fixed): centre, half extents, a turn about
    /// y (degrees), look and flags as NativeBlockLooks.Resolve gives them, sRGB colour, emission gain, and its kind (0 structure,
    /// 1 neon, 2 holo screen the atmosphere animates, 3 a beacon anchor the atmosphere blinks).</summary>
    public struct NativeCyberBox {public float X,Y,Z,Hx,Hy,Hz,Yaw,Gain;public int Look,Flags,Color;public byte Kind;}

    /// <summary>
    /// Cyber City's backdrop (docs/arenas/CYBERCITY_PROPOSAL.md §8 "Backdrop"): a deterministic neon megacity around the 80 x 64
    /// slab, built once from integer hashes so every client, the killcam and the menu draw the same skyline. Nothing here is sim
    /// data and nothing collides: Runtime/NativeCyberCityPresentation.cs draws it as instanced NativeBlockLit boxes (procedural
    /// lit windows, panels and neon from the shader; a few draws, no shadows cast) for map id "cybercity" only. Only the ground
    /// plates reach the slab's edge (x ±40 / z ±32); everything standing lies beyond x ±<see cref="InnerX"/> / z
    /// ±<see cref="InnerZ"/>, and nothing taller than <see cref="OrbitCap"/> m comes within 9 m of the menu's overview orbit
    /// (Tests/CyberCityChecks.cs).
    ///  - The near ring: 18-24 m blocks right behind the frontage with lit window bands, rooftop neon and screens (the rim seen over
    ///    the frontage).
    ///  - The Helix Line's tunnels: beyond both portals, 80 m of lit tunnel (walls, roof, guide beam, light strips, signals).
    ///  - Two elevated expressways (their traffic is light streams in NativeCyberCityFx) and the mid and far towers: glass and
    ///    precast tiers with setbacks, neon crowns and fins, holo screens facing the arena, antenna masts with beacons; a few
    ///    megatowers reach 380 m into the haze.
    /// Moving parts are pure functions of the clock the caller passes (match time in a match; the render clock behind the menu):
    /// the flying traffic (<see cref="Car"/>), the expressway streams (<see cref="Stream"/>), the searchlights (<see cref="Searchlight"/>).
    /// Unity frame (x east, z north), metres.
    /// </summary>
    public static class NativeCyberCityScenery
    {
        public const double SlabX=40,SlabZ=32,InnerX=40.5,InnerZ=32.5,OrbitCap=24,OrbitClear=9;
        private const int Cy=NativeCyberCityLooks.Bank,LGlass=NativeCyberCityLooks.Glass,LPanel=NativeCyberCityLooks.Panel,LTube=NativeCyberCityLooks.Tube,LHolo=NativeCyberCityLooks.Holo,LMetal=NativeCyberCityLooks.Metal,LRoad=NativeCyberCityLooks.Road;
        private static readonly int[] Neons={0x22E6FF,0xFF3DCF,0x8B5CFF,0xC8F4FF,0xFF5A8A,0x5AFFD2,0xFF8A4D};
        private static readonly int[] Glasses={0x141A2C,0x18203A,0x101626,0x1C1F33,0x221A36};
        private static readonly int[] Panels={0x3A3D4A,0x2E3140,0x444859,0x33303F};

        private static uint Mix(uint x){unchecked{x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x;}}
        /// <summary>Deterministic hash of three integers in [0, 1).</summary>
        public static double Hash(int a,int b=0,int c=0){unchecked{return Mix(Mix(Mix((uint)a+0x9E3779B9U)^(uint)b*0x85EBCA6BU)+(uint)c*0xC2B2AE35U)/4294967296.0;}}
        private static double Range(double lo,double hi,int a,int b=0,int c=0)=>lo+(hi-lo)*Hash(a,b,c);
        private static int Pick(int[] list,int a,int b=0,int c=0)=>list[Math.Min(list.Length-1,(int)(Hash(a,b,c)*list.Length))];
        /// <summary>Horizontal distance from (x, z) to the menu's overview orbit (ArenaPresentation.OverviewCamera: 42 cos t, 32, 46 sin(t + .2)).</summary>
        public static double OrbitDistance(double x,double z)=>NativeBrickfallScenery.OrbitDistance(x,z);

        private sealed class Sink
        {
            public readonly List<NativeCyberBox> Boxes=new List<NativeCyberBox>();
            public void Add(double x,double y,double z,double sx,double sy,double sz,int look,int flags,int color,double gain=1,double yaw=0,byte kind=0)
            {if(sx>0&&sy>0&&sz>0)Boxes.Add(new NativeCyberBox{X=(float)x,Y=(float)y,Z=(float)z,Hx=(float)(sx/2),Hy=(float)(sy/2),Hz=(float)(sz/2),Yaw=(float)yaw,Look=look,Flags=flags,Color=color,Gain=(float)gain,Kind=kind});}
        }

        /// <summary>The whole static backdrop, in build order (ground, tunnels, near ring, expressways, towers). `layers`: faces
        /// that share a plane with another box's (a trim flush on its tower, two plates meeting) are drawn apart by whole 2 mm
        /// steps, a later box over an earlier one (Core NativeSurfaceLayers; off only for the surface audit's before count).</summary>
        public static NativeCyberBox[] Build(bool layers=true)
        {
            var s=new Sink();Ground(s);Tunnels(s);NearRing(s);Expressways(s);Towers(s);var o=s.Boxes.ToArray();if(layers)Layers(o);return o;
        }
        // The lifts: half along the box's own axes (turned by its yaw) moves the centre, half grows the half extent, so only the
        // lifted face moves. Two boxes of one look, colour and flags (not glowing) may lie either way over each other.
        private static void Layers(NativeCyberBox[] o)
        {
            var boxes=new NativeSurfaceLayers.Box[o.Length];
            for(int i=0;i<o.Length;i++){var b=o[i];double h=b.Yaw*Math.PI/360;boxes[i]=new NativeSurfaceLayers.Box{X=b.X,Y=b.Y,Z=b.Z,Hx=b.Hx,Hy=b.Hy,Hz=b.Hz,Qy=Math.Sin(h),Qw=Math.Cos(h),Material=b.Gain==1?NativeSurfaceLayers.Key(b.Look,b.Color,b.Flags):-1};}
            var lifts=NativeSurfaceLayers.Solve(boxes,0,null,false,out _,NativeSurfaceLayers.BackdropLift,NativeSurfaceLayers.Far);
            for(int i=0;i<o.Length;i++)
            {
                NativeSurfaceLayers.Apply(lifts,i,out float sx,out float sy,out float sz,out float gx,out float gy,out float gz);if(gx==0&&gy==0&&gz==0)continue;
                double t=o[i].Yaw*Math.PI/180,c=Math.Cos(t),n=Math.Sin(t);o[i].X+=(float)(c*sx+n*sz);o[i].Y+=sy;o[i].Z+=(float)(-n*sx+c*sz);o[i].Hx+=gx;o[i].Hy+=gy;o[i].Hz+=gz;
            }
        }
        private static void Ground(Sink s)
        {
            // Four wet street plates out to 460 m, flush with the slab's top and edge.
            const double R=460;
            s.Add(0,-.25,(SlabZ+R)/2,2*R,.5,R-SlabZ,LRoad,Cy,0x1E1F26);s.Add(0,-.25,-(SlabZ+R)/2,2*R,.5,R-SlabZ,LRoad,Cy,0x1E1F26);
            s.Add((SlabX+R)/2,-.25,0,R-SlabX,.5,2*SlabZ,LRoad,Cy,0x1E1F26);s.Add(-(SlabX+R)/2,-.25,0,R-SlabX,.5,2*SlabZ,LRoad,Cy,0x1E1F26);
        }
        private static void Tunnels(Sink s)
        {
            // The Cut continues 80 m under the near ring at both ends: floor, guide beam, walls, roof, light strips every 4 m, a red
            // signal at 60 m; the roof sits on the street plates' underside.
            for(int e=-1;e<=1;e+=2)
            {
                double x0=e*SlabX,x1=e*(SlabX+80),cx=(x0+x1)/2,len=80;
                s.Add(cx,-4.25,0,len,.5,8.4,NativeBlockLooks.ConcreteBoard,0,0x2C2C33);s.Add(cx,-3.85,0,len,.3,1.8,LMetal,Cy,0x323040);
                for(int w=-1;w<=1;w+=2){s.Add(cx,-2.4,w*3.3,len,3.2,.6,NativeBlockLooks.ConcreteBoard,0,0x34343F);for(double x=x0+e*2;Math.Abs(x)<Math.Abs(x1);x+=e*4)s.Add(x,-1.4,w*2.97,1.6,.06,.04,LTube,Cy,0xC8F4FF,1.4,0,1);}
                s.Add(cx,-.65,0,len,.3,6.6,NativeBlockLooks.ConcreteBoard,0,0x26262E);
                s.Add(e*(SlabX+60),-2.8,2.9,.3,.6,.12,LTube,Cy,0xFF2A3D,2.4,0,1);s.Add(e*(SlabX+60),-2.8,-2.9,.3,.6,.12,LTube,Cy,0xFF2A3D,2.4,0,1);
            }
        }
        private static void NearRing(Sink s)
        {
            // Blocks 10-12 m deep from 1 m behind each frontage, 18-24 m tall (never above the orbit cap), with lit window bands
            // from 17 m up on the faces toward the arena (they show over the 16 m frontage), a neon edge and a few screens. The
            // north and south rows run the full width; the east and west rows fill between them.
            int id=0;
            for(int side=0;side<4;side++)
            {
                bool alongX=side<2;int sign=side%2==0?1:-1;double half=alongX?SlabX+12:SlabZ,face=(alongX?SlabZ:SlabX)+1;
                for(double u=-half;u<half-1;id++)
                {
                    double w=Math.Min(Range(8,16,id,1),half-u),h=Range(18,22,id,2),depth=Range(10,12,id,3);if(w<3){u+=w;continue;}
                    double c=u+w/2,mid=sign*(face+depth/2),inner=sign*(face-.05);
                    void Box(double along,double across,double y,double lw,double sy,double ld,int look,int flags,int color,double gain=1,byte kind=0)
                    {if(alongX)s.Add(along,y,across,lw,sy,ld,look,flags,color,gain,0,kind);else s.Add(across,y,along,ld,sy,lw,look,flags,color,gain,0,kind);}
                    Box(c,mid,h/2,w-.3,h,depth,LPanel,Cy,Pick(Panels,id,4));
                    for(double y=17.2;y<h-1;y+=3.6)Box(c,inner,y+1,w-1.2,1.8,.1,LGlass,Cy,Pick(Glasses,id,(int)y));
                    Box(c,inner,h-.15,w-.3,.12,.12,LTube,Cy,Pick(Neons,id,5),2.2,1);
                    if(Hash(id,6)<.3&&w>9)Box(c,sign*(face-.12),h-3,w*.6,2.6,.2,LHolo,Cy,Pick(Neons,id,7),1.3,2);
                    // The roof (seen from the menu orbit and the high perches): plant, tanks and a lit vent or two, under the orbit cap.
                    for(int r=0;r<3;r++){double ru=c+(Hash(id,20+r)-.5)*(w-3),ra=sign*(face+1.5+Hash(id,30+r)*(depth-3)),rw=Range(1.2,2.6,id,40+r),rh=Range(.8,1.6,id,50+r);Box(ru,ra,h+rh/2,rw,rh,Range(1.2,2.4,id,60+r),LMetal,Cy,Pick(Panels,id,70+r));
                        if(Hash(id,80+r)<.4)Box(ru,ra,h+rh+.04,rw*.6,.08,.3,LTube,Cy,Pick(Neons,id,90+r),1.8,1);}
                    if(Hash(id,9)<.35)Box(c,mid,h+1.2,1.6,2.4,1.6,LPanel,Cy,0x2A2C36);
                    u+=w;
                }
            }
        }
        /// <summary>The two elevated expressways: (z or x of the deck's axis, deck top y, along x?).</summary>
        public static readonly double[] RoadAt={74,-80},RoadY={28,22};
        private static void Expressways(Sink s)
        {
            for(int r=0;r<2;r++)
            {
                double z=RoadAt[r],y=RoadY[r];
                s.Add(0,y-.6,z,900,1.2,14,LPanel,Cy,0x2A2C36);
                for(int k=-1;k<=1;k+=2){s.Add(0,y+.45,z+k*6.9,900,.9,.25,LMetal,Cy,0x3A3D4A);s.Add(0,y+.92,z+k*6.9,900,.06,.06,LTube,Cy,r==0?0xFF3DCF:0x22E6FF,1.8,0,1);}
                for(double x=-420;x<=420;x+=42){if(Math.Abs(x)<60&&Math.Abs(z)<60)continue;s.Add(x,(y-1.2)/2,z,3,y-1.2,4,NativeBlockLooks.ConcreteBoard,0,0x2E2E37);}
            }
        }
        /// <summary>Tower sites: a jittered 34 m grid over ±420 m outside the near zone, the orbit corridor and the expressways.</summary>
        public const double Cell=36,Reach=400;
        private static void Towers(Sink s)
        {
            int n=(int)(Reach/Cell);
            for(int i=-n;i<=n;i++)for(int j=-n;j<=n;j++)
            {
                double fill=Hash(i,j,1);double cx=i*Cell+Range(-8,8,i,j,2),cz=j*Cell+Range(-8,8,i,j,3),d=Math.Sqrt(cx*cx+cz*cz);
                if(fill<(d<120?.12:d<220?.22:.34))continue;
                double w=Range(13,26,i,j,4),dp=Range(13,26,i,j,5);
                if(Math.Abs(cx)-w/2<SlabX+16&&Math.Abs(cz)-dp/2<SlabZ+16)continue;
                bool road=false;for(int r=0;r<2;r++)if(Math.Abs(cz-RoadAt[r])<dp/2+10)road=true;if(road)continue;
                double h=d<90?Range(36,70,i,j,6):d<170?Range(55,150,i,j,6):Range(90,300,i,j,6);if(Hash(i,j,7)<.06&&d>140)h=Range(300,380,i,j,8);
                double orbit=OrbitDistance(cx,cz)-Math.Max(w,dp)*.75;if(orbit<OrbitClear+2)h=Math.Min(h,OrbitCap-1);
                Tower(s,i*1000+j,cx,cz,w,dp,h,d);
            }
        }
        private static void Tower(Sink s,int id,double cx,double cz,double w,double dp,double h,double d)
        {
            // One to three tiers, each narrower than the last above a dark gap band; glass or precast cladding; a neon crown on the top
            // tier's edges, sometimes a neon fin down a corner, a holo screen on the face toward the arena, a mast with a beacon.
            bool glass=Hash(id,1)<.72;int tiers=h>80?1+(int)(Hash(id,2)*3):1;double y=0,tw=w,td=dp;
            int clad=glass?Pick(Glasses,id,3):Pick(Panels,id,3),neon=Pick(Neons,id,4);
            for(int t=0;t<tiers;t++)
            {
                double th=t==tiers-1?h-y:h*Range(.35,.55,id,10+t);if(th<6)break;
                s.Add(cx,y+th/2,cz,tw,th,td,glass?LGlass:LPanel,Cy,clad);
                if(!glass)for(double yy=y+3;yy<y+th-2;yy+=10.8)s.Add(cx,yy,cz,tw+.1,.9,td+.1,LGlass,Cy,Pick(Glasses,id,(int)yy));
                y+=th;if(t<tiers-1){s.Add(cx,y+.6,cz,tw*.9,1.2,td*.9,LMetal,Cy,0x202230);y+=1.2;tw*=Range(.68,.85,id,20+t);td*=Range(.68,.85,id,30+t);}
            }
            // The crown: the two top edges that face the arena's middle (the far side is never seen from inside).
            s.Add(cx,y-.1,cz-Math.Sign(cz)*td/2,tw+.2,.2,.2,LTube,Cy,neon,2.4,0,1);s.Add(cx-Math.Sign(cx)*tw/2,y-.1,cz,.2,.2,td+.2,LTube,Cy,neon,2.4,0,1);
            if(Hash(id,5)<.35){double fh=Math.Min(h*.6,90);s.Add(cx+tw/2+.12,y-fh/2-2,cz+td/2+.12,.3,fh,.3,LTube,Cy,Pick(Neons,id,6),2.6,0,1);}
            if(h>40&&Hash(id,7)<(d<200?.45:.25))
            {
                // A screen on the face that looks most toward the arena's middle, high on the tower.
                double sw=Math.Min(Math.Max(w,dp)*.8,34),sh=sw*Range(.45,.65,id,8),sy=Math.Min(h*.75,Range(30,h-sh/2-4,id,9));
                bool xFace=Math.Abs(cx)>Math.Abs(cz);double fx=xFace?cx-Math.Sign(cx)*(w/2+.3):cx,fz=xFace?cz:cz-Math.Sign(cz)*(dp/2+.3);
                s.Add(fx,sy,fz,xFace?.4:sw,sh,xFace?sw:.4,LHolo,Cy,Pick(Neons,id,11),1.25,0,2);
            }
            if(h>60){double mast=Range(6,22,id,12);s.Add(cx,y+mast/2,cz,.6,mast,.6,LMetal,Cy,0x3A3D4A);s.Add(cx,y+mast+.3,cz,.5,.5,.5,LTube,Cy,0xFF2A3D,1,0,3);}
        }

        // ---------------- moving parts (pure functions of the clock) ----------------
        /// <summary>Flying traffic: lanes (closed ellipses round the arena) with their altitude, radii, speed (m/s), car count and colour.</summary>
        public static readonly double[] LaneY={34,46,58,72,96,128},LaneRx={78,96,118,150,190,240},LaneRz={70,88,104,132,176,224},LaneSpeed={26,-30,34,-38,42,-46};
        public static readonly int[] LaneCars={8,10,10,12,12,14};
        public const int Lanes=6;
        /// <summary>Car i of lane `lane` at `time`: position, heading (degrees from +z toward +x) and whether it carries a magenta (else cyan) underglow.</summary>
        public static void Car(int lane,int i,double time,out double x,out double y,out double z,out double yaw,out bool magenta)
        {
            double rx=LaneRx[lane],rz=LaneRz[lane],per=Math.PI*(3*(rx+rz)-Math.Sqrt((3*rx+rz)*(rx+3*rz)));
            double phase=(double)i/LaneCars[lane]+Hash(lane,i,1)*.06+LaneSpeed[lane]*time/per,a=2*Math.PI*phase;
            x=rx*Math.Cos(a);z=rz*Math.Sin(a);y=LaneY[lane]+3*Math.Sin(a*3+lane);
            double dx=-rx*Math.Sin(a)*Math.Sign(LaneSpeed[lane]),dz=rz*Math.Cos(a)*Math.Sign(LaneSpeed[lane]);yaw=Math.Atan2(dx,dz)*180/Math.PI;magenta=Hash(lane,i,2)<.5;
        }
        /// <summary>Expressway light streams: head and tail lights, `StreamLights` per lane, 2 lanes per road.</summary>
        public const int StreamLights=40;
        public static void Stream(int road,int lane,int i,double time,out double x,out double y,out double z,out bool head)
        {
            double speed=lane==0?24:-21,span=900;head=lane==0==(road==0);
            double u=((i+Hash(road,lane,i)*.6)/StreamLights*span+speed*time)%span;if(u<0)u+=span;
            x=u-span/2;y=RoadY[road]+.7;z=RoadAt[road]+(lane==0?-3:3);
        }
        /// <summary>Searchlights from three far towers: base and the beam's unit direction, swinging slowly.</summary>
        public const int Searchlights=3;
        public static readonly double[] SearchX={-190,230,40},SearchZ={240,-210,-300},SearchY={210,180,260};
        public static void Searchlight(int i,double time,out double x,out double y,out double z,out double dx,out double dy,out double dz)
        {
            x=SearchX[i];y=SearchY[i];z=SearchZ[i];double a=time*(.11+.03*i)+i*2.1,tilt=.85+.08*Math.Sin(time*.07+i);
            double cx=-x/Math.Sqrt(x*x+z*z),cz=-z/Math.Sqrt(x*x+z*z);double sa=Math.Sin(a)*.9;
            dx=(cx*Math.Cos(sa)-cz*Math.Sin(sa))*Math.Cos(tilt);dz=(cx*Math.Sin(sa)+cz*Math.Cos(sa))*Math.Cos(tilt);dy=Math.Sin(tilt);
        }
    }
}
