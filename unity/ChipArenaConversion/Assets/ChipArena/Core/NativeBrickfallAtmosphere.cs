using System;

namespace ChipArena.UnityConversion
{
    /// <summary>What the destruction kit tells the Brickfall atmosphere about a structure (ARENA_PROPOSAL.md §5.4 events).</summary>
    public enum NativeBrickfallCue { Stress, Fall, Land }

    /// <summary>
    /// The Brickfall atmosphere's memory of what happened, on the match clock: per pigeon the last few times something within
    /// <see cref="NativeBrickfallAtmosphere.SpookRadius"/> m spooked it (any Explosion event, and structure stress, fall and
    /// landing cues), and the last <see cref="Collapses"/> structure landings (time, place, size, energy) for the drifting
    /// collapse dust (the dust coat is the round's one NativeDustCoat, fed by the same landings). Every write is idempotent on
    /// (time, place), so the presentation can feed the same
    /// event in every frame it stays in the event list, and the killcam replay can feed it again; reads take only entries at
    /// or before the clock they are asked about, so the killcam, replaying a past moment on the same match clock, sees the
    /// pigeons and dust exactly as they were. Fixed arrays: no allocation after construction.
    /// </summary>
    public sealed class NativeBrickfallMemory
    {
        public const int Perches=14,PerPigeon=4,Collapses=16;
        private readonly double[] spook=new double[Perches*PerPigeon];private readonly int[] spooks=new int[Perches];
        private readonly double[] ct=new double[Collapses],cx=new double[Collapses],cy=new double[Collapses],cz=new double[Collapses],cr=new double[Collapses],ce=new double[Collapses];private int count;
        public double Newest{get;private set;}=double.NegativeInfinity;
        public int CollapseCount=>count;
        public void Clear(){Array.Clear(spooks,0,spooks.Length);count=0;Newest=double.NegativeInfinity;}
        /// <summary>Something startling at (x, y, z) at `time`: every pigeon whose perch is within the spook radius remembers it.</summary>
        public void Spook(double time,double x,double y,double z)
        {
            if(double.IsNaN(time)||double.IsInfinity(time))return;
            for(int i=0;i<Perches;i++)
            {
                NativeBrickfallAtmosphere.Perch(i,out double px,out double py,out double pz,out _);double dx=px-x,dy=py-y,dz=pz-z;
                if(dx*dx+dy*dy+dz*dz>NativeBrickfallAtmosphere.SpookRadius*NativeBrickfallAtmosphere.SpookRadius)continue;
                int o=i*PerPigeon,n=spooks[i];bool seen=false;for(int k=0;k<n;k++)if(Math.Abs(spook[o+k]-time)<1e-3)seen=true;if(seen)continue;
                // Keep the newest PerPigeon times, sorted ascending (a full list drops its oldest for a newer time).
                if(n<PerPigeon)spooks[i]=++n;else if(time>spook[o]){for(int k=0;k<n-1;k++)spook[o+k]=spook[o+k+1];}else continue;
                int at=n-1;while(at>0&&spook[o+at-1]>time){spook[o+at]=spook[o+at-1];at--;}spook[o+at]=time;
            }
            Newest=Math.Max(Newest,time);
        }
        /// <summary>A structure landing (StructureLand): spooks the pigeons and starts the drifting dust haze.</summary>
        public void Collapse(double time,double x,double y,double z,double radius,double energy)
        {
            if(double.IsNaN(time)||double.IsInfinity(time))return;Spook(time,x,y,z);
            for(int k=0;k<count;k++)if(Math.Abs(ct[k]-time)<1e-3&&Math.Abs(cx[k]-x)<.1&&Math.Abs(cz[k]-z)<.1)return;
            int at=count<Collapses?count++:Oldest();ct[at]=time;cx[at]=x;cy[at]=y;cz[at]=z;cr[at]=Math.Max(1,Math.Min(20,radius));ce[at]=Math.Max(0,Math.Min(1,energy));
        }
        private int Oldest(){int o=0;for(int k=1;k<count;k++)if(ct[k]<ct[o])o=k;return o;}
        /// <summary>Pigeon i's spook times (oldest first).</summary>
        public int Spooks(int pigeon,double[] into){int n=spooks[pigeon];for(int k=0;k<n;k++)into[k]=spook[pigeon*PerPigeon+k];return n;}
        public void CollapseAt(int k,out double time,out double x,out double y,out double z,out double radius,out double energy){time=ct[k];x=cx[k];y=cy[k];z=cz[k];radius=cr[k];energy=ce[k];}
    }

    /// <summary>
    /// What the paper scraps must not slide through: world-space boxes (min, max; metres) the presentation refills each frame
    /// from the arena's props (Runtime/NativeBrickfallPresentation.cs: each prop's bounding box, so the killcam replay arena
    /// gives its own). A scrap whose height lies within a box's span (± <see cref="Skin"/>) fades out as it nears the box's
    /// footprint: gone within <see cref="Reach"/> m (about half a scrap), whole beyond another <see cref="Fade"/> m. A scrap
    /// lifting over low cover, or skating on a floor or the foundation below it, is untouched. Fixed capacity; boxes past it
    /// are ignored. No allocation after construction.
    /// </summary>
    public sealed class NativeBrickfallCover
    {
        public const int Capacity=512;public const double Skin=.02,Reach=.2,Fade=.4;
        private readonly double[] box=new double[Capacity*6];
        public int Count{get;private set;}
        public void Clear()=>Count=0;
        public bool Add(double minX,double minY,double minZ,double maxX,double maxY,double maxZ)
        {
            if(Count>=Capacity||!(maxX>=minX&&maxY>=minY&&maxZ>=minZ))return false;int o=Count*6;box[o]=minX;box[o+1]=minY;box[o+2]=minZ;box[o+3]=maxX;box[o+4]=maxY;box[o+5]=maxZ;Count++;return true;
        }
        /// <summary>A scrap's opacity factor at (x, y, z): 0 inside or within <see cref="Reach"/> m of a box it would cross, 1 clear of every box.</summary>
        public double Clearance(double x,double y,double z)
        {
            double near=double.MaxValue;
            for(int i=0,o=0;i<Count;i++,o+=6)
            {
                if(y<box[o+1]-Skin||y>box[o+4]+Skin)continue;
                double dx=Math.Max(0,Math.Max(box[o]-x,x-box[o+3])),dz=Math.Max(0,Math.Max(box[o+2]-z,z-box[o+5])),d=dx*dx+dz*dz;if(d<near)near=d;
            }
            if(near>=(Reach+Fade)*(Reach+Fade))return 1;double t=Math.Max(0,Math.Min(1,(Math.Sqrt(near)-Reach)/Fade));return t*t*(3-2*t);
        }
    }

    /// <summary>
    /// Brickfall's atmosphere (ARENA_PROPOSAL.md §2.2 "Atmosphere", §3.10 Atmosphere VFX), as pure functions of the match clock,
    /// the camera and <see cref="NativeBrickfallMemory"/>, so host, guests and the killcam draw the same air
    /// (Runtime/NativeBrickfallPresentation.cs draws it into one premultiplied and one solid NativeSirenFx batch):
    ///  - <see cref="Shafts"/> sun shafts slanting down the sun's direction through the lattice canopies and street gaps;
    ///  - <see cref="Motes"/> dust motes in a wrapped box around the camera, glinting, drifting east (none below the floor);
    ///  - <see cref="Scraps"/> paper scraps skittering east along open street runs clear of the §3.3 cover, lifting in gusts,
    ///    tumbling, and fading out at any prop in their way (<see cref="NativeBrickfallCover"/>, filled from the arena's props);
    ///  - <see cref="NativeBrickfallMemory.Perches"/> pigeons on the frontage tops that scatter from anything startling within
    ///    <see cref="SpookRadius"/> m, circle outside the walls and come back <see cref="ReturnAfter"/> s after the last scare;
    ///  - <see cref="Tarps"/> tarps flapping on the frontage above 4 m (decoration above 3 m never pretends to be cover);
    ///  - collapse dust from the destruction kit's landings drifting east at <see cref="DustDrift"/> m/s (<see cref="DustPuff"/>):
    ///    a thin haze above head height that follows the kit's landing dust wall, fades near the eye (<see cref="DustNear"/>)
    ///    and shares the §3.10 cap of <see cref="DustBillboardCap"/> dust billboards map-wide with that wall;
    ///  - not the dust coat: landings reach the round's one coat (Core NativeDustCoat, which NativeBlockLit reads) through
    ///    ArenaPresentation.StructureHeard, next to this memory.
    /// Counts follow the effects quality (Full 1, Reduced .3) and halve on graphics=performance, which also drops the shafts
    /// (<see cref="Count"/>). Unity frame, metres, seconds.
    /// </summary>
    public static class NativeBrickfallAtmosphere
    {
        public const int Shafts=6,Motes=200,Scraps=30,Tarps=6,TarpCols=5,TarpRows=5;
        public const double SpookRadius=18,ReturnAfter=25,TakeOff=1.2,Landing=4,DustDrift=.7,DustLife=16,DustPeak=.16,MoteBox=16;
        /// <summary>Drift puffs per landing, landings drifting at once, drift puffs in all, and the §3.10 cap on live dust
        /// billboards map-wide that the drift shares with the destruction kit's landing dust wall (the wall comes first).</summary>
        public const int DustPuffs=12,DustClouds=8,DustBudget=48,DustBillboardCap=120;
        private static double Hash(int a,int b=0,int c=0)=>NativeBrickfallScenery.Hash(a,b,c);
        private static double Range(double lo,double hi,int a,int b=0,int c=0)=>lo+(hi-lo)*Hash(a,b,c);
        private static double Frac(double v)=>v-Math.Floor(v);
        private static double Smooth(double x){x=Math.Max(0,Math.Min(1,x));return x*x*(3-2*x);}
        private static double Clamp01(double x)=>Math.Max(0,Math.Min(1,x));

        /// <summary>
        /// How many of `full` to draw: scaled by the effects quality (at least a third of Full's count at Reduced .3 for the
        /// ambient life, so the city never looks dead), halved on graphics=performance; shafts are off on performance.
        /// </summary>
        public static int Count(int full,double quality,bool low,bool shafts=false)
        {
            if(shafts&&low)return 0;double q=Math.Max(.3,Math.Min(1,double.IsNaN(quality)?1:quality));if(shafts&&q<.5)q=.5;
            int n=(int)Math.Round(full*q*(low?.5:1));return Math.Max(0,Math.Min(full,n));
        }
        /// <summary>
        /// The round's start on the match clock for dust that a rebuild wipes: the coming fight while counting down (or in an
        /// intro), else the current fight. Landings before it belong to a map that has since been rebuilt.
        /// </summary>
        public static double RoundStart(NativePhase phase,double fightAt,double phaseEnd)=>phase==NativePhase.Countdown||phase==NativePhase.Intro?phaseEnd:fightAt;

        // ---------------- sun shafts ----------------
        private static readonly double[] ShaftX={-22,-17.5,22,17.5,-6.8,6.8},ShaftY={4.2,4.6,4.2,4.6,6.6,6.6},ShaftZ={3,-6,-3,6,9.5,-9.5},ShaftL={13.5,12,13.5,12,19,19},ShaftW={1.9,1.6,1.9,1.6,2.4,2.4};
        /// <summary>
        /// Shaft i: its centre, length and width (it lies along the sun's direction, which the caller supplies) and its opacity
        /// at `time`: a slow breathing with a hashed phase, as dust drifts through it.
        /// </summary>
        public static void Shaft(int i,double time,out double x,out double y,out double z,out double length,out double width,out double alpha)
        {x=ShaftX[i];y=ShaftY[i];z=ShaftZ[i];length=ShaftL[i];width=ShaftW[i];double ph=Hash(3100,i)*6.283;alpha=.09*(.75+.25*Math.Sin(time*.35+ph)+.08*Math.Sin(time*1.7+ph*3));}
        /// <summary>A shaft fades as the camera comes within 2.5 m of its axis or looks straight down it (a flat card edge-on).</summary>
        public static double ShaftView(double distance,double alongDot)=>Smooth((distance-1)/2.5)*Smooth((1-Math.Abs(alongDot))/.25);

        // ---------------- motes ----------------
        /// <summary>
        /// Mote i near a camera at (cx, cy, cz): a hashed point drifting east with a slow sway, wrapped into a
        /// <see cref="MoteBox"/> m box centred on the camera so motes are always around it; size in metres and an additive
        /// strength that glints and fades out toward the box edge and within half a metre of the eye; a mote below the floor has none.
        /// </summary>
        public static void Mote(int i,double cx,double cy,double cz,double time,out double x,out double y,out double z,out double size,out double alpha)
        {
            double ph=Hash(3200,i,3)*6.283,bx=Hash(3200,i)*MoteBox+time*(.22+.1*Hash(3200,i,4))+Math.Sin(time*.31+ph)*.6,by=Hash(3200,i,1)*MoteBox+Math.Sin(time*.23+ph*2)*.5-time*.03,bz=Hash(3200,i,2)*MoteBox+Math.Cos(time*.27+ph)*.6;
            x=cx+Wrap(bx-cx);y=cy+Wrap(by-cy);z=cz+Wrap(bz-cz);size=Range(.025,.06,3200,i,5);
            double dx=x-cx,dy=y-cy,dz=z-cz,d=Math.Sqrt(dx*dx+dy*dy+dz*dz),edge=1-Smooth((d-MoteBox*.32)/(MoteBox*.16)),near=Smooth((d-.5)/.8);
            alpha=y<.05?0:edge*near*(.35+.65*Math.Pow(.5+.5*Math.Sin(time*(1.3+Hash(3200,i,6)*2.2)+ph*5),3));
        }
        private static double Wrap(double v)=>v-MoteBox*Math.Floor(v/MoteBox+.5);

        // ---------------- paper scraps ----------------
        // Open street runs (x0, x1, z centre, half width), point-symmetric pairs: the lanes' open stretches (west of the news
        // kiosk on the north lane), the square's north and south streets between the tenements and the hotel, and the market
        // street south of the stalls. Each run's sweep (x ± .25, z ± half + .35 of sway) keeps 0.4 m from every solid of the
        // §3.3 draft blockout (docs/arenas/scripts/brickfall_m1.py, Tests/test_brickfall_scraps.py); the real map's props fade
        // a scrap out on top of that (NativeBrickfallCover).
        private static readonly double[] LaneX0={-27.5,5.75,-20.25,2.5,-29,9.25},LaneX1={-5.75,27.5,-2.5,20.25,-9.25,29},LaneZ={23.75,-23.75,9.5,-9.5,3.125,-3.125},LaneHalf={.6,.6,1.4,1.4,.8,.8};
        /// <summary>Corridor `lane`'s run: west and east ends, centre line and half width (the scrap sways .25 m along it and .35 m across).</summary>
        public static void Corridor(int lane,out double x0,out double x1,out double z,out double half){x0=LaneX0[lane];x1=LaneX1[lane];z=LaneZ[lane];half=LaneHalf[lane];}
        public static int Corridors=>LaneX0.Length;
        /// <summary>
        /// Scrap i at `time`: it rides one corridor east at 1.2-2.6 m/s with gusts, skittering along the ground and lifting up
        /// to 3 m now and then, tumbling; it fades in at the corridor's west end and out at its east end, then starts over.
        /// The orientation is Unity Euler degrees. The caller also fades it at the arena's props (<see cref="NativeBrickfallCover"/>).
        /// </summary>
        public static void Scrap(int i,double time,out double x,out double y,out double z,out double pitch,out double yaw,out double roll,out double alpha)
        {
            int lane=i%LaneX0.Length;double speed=Range(1.2,2.6,3300,i),span=LaneX1[lane]-LaneX0[lane],period=span/speed,ph=Hash(3300,i,1);
            double cycle=time/period+ph,p=Frac(cycle),gust=Math.Sin(time*.5+ph*6.283)*.5+.5;int n=(int)Math.Floor(cycle);
            x=LaneX0[lane]+p*span+Math.Sin(time*1.7+ph*20)*.25;z=LaneZ[lane]+(Hash(3300,i,n)*2-1)*LaneHalf[lane]+Math.Sin(time*.8+ph*9)*.35;
            double hop=Math.Max(0,Math.Sin(time*(2.2+Hash(3300,i,2)*2)+ph*13))*.35,lift=Math.Pow(Math.Max(0,Math.Sin(time*.21+ph*4)),6)*3*gust;y=.04+hop*gust+lift;
            double spin=200+Hash(3300,i,3)*400;pitch=(time*spin*(.3+gust))%360;yaw=(time*37+ph*360)%360;roll=Math.Sin(time*3+ph*7)*70*(y>.1?1:.3);
            alpha=Smooth(p/.06)*Smooth((1-p)/.06);
        }

        // ---------------- pigeons ----------------
        // Perches on the frontage tops (14 m, indestructible) at the inner edge, heads over it; facing into the arena (yaw degrees).
        private static readonly double[] PerchX={-21.4,-20.6,-19.5,-18.8,20.7,19.9,18.9,-30.08,-30.08,-30.08,30.08,30.08,30.08,30.08},PerchZ={26.08,26.08,26.08,26.08,-26.08,-26.08,-26.08,7.8,8.5,9.6,-7.4,-8.3,-9.1,-10.2};
        public static void Perch(int i,out double x,out double y,out double z,out double yaw){x=PerchX[i];y=14.02;z=PerchZ[i];yaw=Math.Abs(z)>26?(z>0?180:0):(x>0?270:90);}
        /// <summary>How far pigeon i is from its perch (0 perched, 1 flying) at `time` given its spook times: the largest of
        /// each scare's take-off ramp, flight and landing ramp, so a scare while flying keeps it out without a jump.</summary>
        public static double Away(double time,double[] spooks,int count)
        {
            double away=0;
            for(int k=0;k<count;k++)
            {
                double a=time-spooks[k];if(a<0)continue;
                double g=a<TakeOff?Smooth(a/TakeOff):a<ReturnAfter?1:a<ReturnAfter+Landing?1-Smooth((a-ReturnAfter)/Landing):0;away=Math.Max(away,g);
            }
            return away;
        }
        /// <summary>
        /// Pigeon i at `time`: on its perch (pecking and turning now and then) blended toward a loop 9-13 m outside the wall and
        /// 8-12 m above it by `away`, lifting in an arc on the way; heading from its motion; wings folded when perched, beating
        /// fast on take-off and landing, gliding some of the loop. `flap` -1..1, `wings` 0 folded..1 open; `head` the peck dip.
        /// </summary>
        public static void Pigeon(int i,double time,double away,out double x,out double y,out double z,out double yaw,out double flap,out double wings,out double head)
        {
            Perch(i,out double px,out double py,out double pz,out double perchYaw);
            AirPoint(i,time,out double ax,out double ay,out double az);
            double s=Smooth(away);x=px+(ax-px)*s;y=py+(ay-py)*s+Math.Sin(Math.PI*s)*2.5;z=pz+(az-pz)*s;
            AirPoint(i,time+.05,out double bx,out double by,out double bz);double tx=(bx-ax)*s,tz=(bz-az)*s;
            double ph=Hash(3400,i)*6.283;
            if(s>.02){yaw=Math.Atan2(tx+(ax-px)*(s<1?.3:0),tz+(az-pz)*(s<1?.3:0))*180/Math.PI;}
            else{double turn=Math.Floor(time/Range(3,7,3400,i,1)+ph);yaw=perchYaw+(Hash(3400,i,(int)turn)-.5)*70;}
            double climbing=away>0&&away<1?1:0,glide=Smooth(Math.Sin(time*.6+ph)*2-.4)*(1-climbing);
            flap=s>.02?Math.Sin(time*(climbing>0?34:24)+ph*5)*(1-.8*glide):0;wings=s>.02?1:0;
            double peck=Frac(time/Range(1.6,3.4,3400,i,2)+ph);head=s>.02?0:peck<.18?Math.Sin(peck/.18*Math.PI):0;
        }
        private static void AirPoint(int i,double time,out double x,out double y,out double z)
        {
            Perch(i,out double px,out double py,out double pz,out _);double ox=Math.Abs(pz)>26?0:Math.Sign(px),oz=Math.Abs(pz)>26?Math.Sign(pz):0;
            double out_=Range(9,13,3500,i),cx=px+ox*out_,cz=pz+oz*out_,r=Range(5,8,3500,i,1),w=(Hash(3500,i,2)<.5?-1:1)*Range(.45,.7,3500,i,3),ph=Hash(3500,i,4)*6.283;
            x=cx+Math.Cos(time*w+ph)*r;z=cz+Math.Sin(time*w+ph)*r;y=py+Range(8,12,3500,i,5)+Math.Sin(time*.7+ph)*1.2;
        }

        // ---------------- tarps ----------------
        // Tarps hung from the frontage's inner faces (point-symmetric pairs): top edge centre, the wall's inward normal, colour.
        private static readonly double[] TarpX={-22,-6,22,6,-29.88,29.88},TarpZ={25.88,25.88,-25.88,-25.88,-6,6},TarpNx={0,0,0,0,1,-1},TarpNz={-1,-1,1,1,0,0};
        public const double TarpTop=12.6,TarpWidth=4,TarpHeight=5,TarpMinHeight=4;
        public static int TarpColor(int t)=>t%3==1?NativeBrickfallScenery.Rust:NativeBrickfallScenery.Dust;
        /// <summary>The inward normal of the wall tarp t hangs on (the side the arena sees).</summary>
        public static void TarpNormal(int t,out double nx,out double nz){nx=TarpNx[t];nz=TarpNz[t];}
        /// <summary>
        /// Vertex (col, row) of tarp t at `time` (row 0 the pinned top edge): the cloth hangs off the wall face and billows out
        /// with a travelling wave and gusts, more toward the free bottom edge; never below <see cref="TarpMinHeight"/>.
        /// </summary>
        public static void TarpVertex(int t,int col,int row,double time,out double x,out double y,out double z)
        {
            double nx=TarpNx[t],nz=TarpNz[t],ux=-nz,uz=nx,u=(col/(double)(TarpCols-1)-.5)*TarpWidth,v=row/(double)(TarpRows-1),ph=Hash(3600,t)*6.283;
            double gust=.55+.45*Math.Sin(time*.4+ph)*Math.Sin(time*.23+ph*2),wave=Math.Sin(time*2.6+ph-col*.9+row*.7)*.5+.5,billow=.08+Math.Pow(v,1.4)*(.15+.55*gust*wave);
            double sway=Math.Sin(time*1.9+ph+row*.5)*.12*v*gust;
            x=TarpX[t]+ux*(u+sway)+nx*(.06+billow);z=TarpZ[t]+uz*(u+sway)+nz*(.06+billow);y=Math.Max(TarpMinHeight,TarpTop-v*TarpHeight+billow*.25*v);
        }

        // ---------------- collapse dust ----------------
        /// <summary>
        /// Puff p of a landing at (lx, ly, lz) of size `radius` and `energy` 0..1, `age` s after it: the thin haze the
        /// destruction kit's landing dust wall leaves behind (the wall is .5 opaque for 1.5 s, .2 by 3 s, gone by 7 s; this is
        /// at most <see cref="DustPeak"/>, about .14 by 3 s and .05 by 8 s, and gone after <see cref="DustLife"/> s). Puffs start
        /// spread over the footprint, lift out of the pilots' 0-2.5 m band within 4 s (centre at least 2.5 m + .3 × size above
        /// the landing point), keep rising, drift east at <see cref="DustDrift"/> m/s and swell. The caller fades each puff by
        /// its distance from the eye (<see cref="DustNear"/>).
        /// </summary>
        public static void DustPuff(int seed,int p,double lx,double ly,double lz,double radius,double energy,double age,out double x,out double y,out double z,out double size,out double alpha)
        {
            double a=Hash(3700+seed,p)*6.283,r=Math.Sqrt(Hash(3700+seed,p,1))*radius*.7,ph=Hash(3700+seed,p,2)*6.283;age=Math.Max(0,age);
            size=(3+radius*.35)*(1+age*.06)*(.8+.4*Hash(3700+seed,p,4));
            x=lx+Math.Cos(a)*r+DustDrift*age+Math.Sin(age*.3+ph)*.6;z=lz+Math.Sin(a)*r+Math.Cos(age*.25+ph)*.6;y=ly+1+Hash(3700+seed,p,3)*radius*.25+Smooth(age/4)*(1.5+size*.3)+.15*age;
            alpha=Clamp01(energy*.9+.1)*DustPeak*Smooth(age/2.2)*Math.Exp(-Math.Max(0,age-2.5)/5)*Smooth((DustLife-age)/5);
        }
        /// <summary>
        /// A drift puff's opacity factor by the eye's distance from its centre: 0 until the eye is max(3 m, half the puff) away,
        /// so a camera-facing puff never covers the screen from inside or pops as the eye passes its centre; full past another
        /// max(1.5 m, a quarter of the puff).
        /// </summary>
        public static double DustNear(double distance,double size)=>Smooth((distance-Math.Max(3,size*.5))/Math.Max(1.5,size*.25));

        // ---------------- lighting the CPU-built pieces ----------------
        /// <summary>
        /// Lambert for the flat-coloured pieces (pigeons, scraps, tarps, birds, smoke and dust), all linear: albedo × (trilight
        /// ambient by the normal's height + sun × max(0, N·L), or |N·L| × `wrap` for thin cloth and paper lit from behind).
        /// </summary>
        public static void Shade(double ar,double ag,double ab,double nx,double ny,double nz,double lx,double ly,double lz,double sr,double sg,double sb,double[] ambient,double wrap,out double r,out double g,out double b)
        {
            double d=nx*lx+ny*ly+nz*lz;if(d<0)d=wrap>0?-d*wrap:0;
            double up=Math.Max(0,ny),down=Math.Max(0,-ny),mid=1-up-down;
            double kr=ambient[0]*up+ambient[3]*mid+ambient[6]*down+sr*d,kg=ambient[1]*up+ambient[4]*mid+ambient[7]*down+sg*d,kb=ambient[2]*up+ambient[5]*mid+ambient[8]*down+sb*d;
            r=ar*kr;g=ag*kg;b=ab*kb;
        }
    }
}
