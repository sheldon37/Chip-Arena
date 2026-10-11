using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Brickfall's backdrop (docs/arenas/ARENA_PROPOSAL.md §2.2 "Backdrop", §3.10 Backdrop and Landmarks outside the walls): a
    /// deterministic, box-built condemned district around the 64×56 slab, built once from integer hashes so every client, the
    /// killcam and the menu draw the same city. Nothing here is sim data and nothing collides: Runtime/NativeBrickfallPresentation.cs
    /// bakes it into a few SourceLit meshes on layer 0, for map id "brickfall" only. Every box lies beyond x ±<see cref="InnerX"/> /
    /// z ±<see cref="InnerZ"/> (the slab plus 2 m; <see cref="NativeSceneryBuilder"/> throws otherwise); the slewing crane jibs,
    /// the train and the bird flocks stay out too at every time (Tests/BrickfallSceneryChecks.cs).
    ///  - <see cref="Near"/>: the street ring, its ground frame out to the fog, kerbs, lamps, wrecks, the brick rail viaduct behind
    ///    the south frontage (arches, cornice, parapets, catenary masts), the Mercer Plaza vacant lot with its billboard;
    ///  - <see cref="North"/>..<see cref="West"/>: the second ring of 1920s walk-ups (window grids with sills, string courses,
    ///    stepped cornices with dentils, parapets, storefronts with fascias and awnings, fire escapes on the facades that face
    ///    the arena, water towers, setbacks, ghost signs), six half-demolished blocks (one in each near row, two far: exposed
    ///    slab edges, rebar, wallpapered rooms, broken brick edges, rubble), a lower far row, the skyline ring with its warm rim
    ///    and the two 45 m tower-crane masts. Sector meshes stay clear of the arena's point lights: north/south beyond |z| 38,
    ///    east/west beyond |x| 42.
    /// Moving parts are pure functions of the clock the caller passes (match time in a match, so host, guests and the killcam
    /// agree; the render clock behind the menu): the train crossing the viaduct every <see cref="TrainPeriod"/> s
    /// (<see cref="Train"/>, mesh <see cref="BuildTrain"/>), the crane jibs slewing between picks (<see cref="CraneSlew"/>, mesh
    /// <see cref="BuildJib"/>) with blinking red aviation lights (<see cref="AviationOn"/>), the bird flocks (<see cref="Bird"/>)
    /// and the two distant smoke columns (<see cref="SmokePuff"/>). Palette: §3.10. Unity frame (x east, z north), metres.
    /// </summary>
    public static class NativeBrickfallScenery
    {
        public const string MapId="brickfall";
        public static bool Active(string mapId)=>mapId==MapId;
        /// <summary>The slab (±32 / ±28) plus 2 m: nothing of the backdrop enters |x| &lt; InnerX and |z| &lt; InnerZ.</summary>
        public const double InnerX=34,InnerZ=30;
        public const int Near=0,North=1,East=2,South=3,West=4,Sectors=5;
        // §3.10 palette, plus a few tones of the same families for variety.
        public const int BrickRed=0x9C4A30,BrickDark=0x7E3A26,Mortar=0xCDBB9E,Buff=0xB8774E,Terracotta=0xE3C9A0,Concrete=0x8F8A80,ConcreteDark=0x5F5A53,FreshConcrete=0xC9C2B4,FreshBrick=0xD2BFA3,
            Green=0x3E6B5A,Rust=0x7A3E22,Timber=0xB4834F,Asphalt=0x3A3836,Cobble=0x6B625A,Tar=0x2B2A2E,LanePaint=0xE8D9A8,Yellow=0xF4B321,Black=0x1C1A18,Danger=0xD6332A,
            Glass=0x6E8F96,Lit=0xFFB45E,DarkGlass=0x1F2A30,Neon=0x4FA8FF,Coral=0xFF5A4E,Dust=0xB8A68F,Soot=0x4A403A,Iron=0x2A2624,SkyGlow=0xF29A5E,SunRim=0xFFC38A,Sodium=0xFF9A3C,Flood=0xFFF1D6;
        private static readonly int[] Bricks={0x9C4A30,0x8A4430,0xA0573A,0x7E3A26,0xB8774E,0x6E3B2E,0x9A6248,0x8E5040};
        private static readonly int[] Trims={0xE3C9A0,0xCDBB9E,0xC9C2B4,0xD2BFA3};
        private static readonly int[] Wallpapers={0xB07A7A,0x8C9A78,0xC49A48,0x7C8EA0,0xD8C8A8,0x9A7A98};
        private static readonly int[] Fascias={0x2B2A2E,0x7A3E22,0x3E6B5A,0xE3C9A0,0x9C4A30,0x4A403A};
        private static readonly int[] Canvas={0xFF5A4E,0x3E6B5A,0xE3C9A0,0x7A3E22};
        /// <summary>§3.10 sun bearing (degrees from +z toward +x): the skyline's warm rim is baked brightest on its far side.</summary>
        public const double SunBearing=322;

        // ---------------- hashing ----------------
        private static uint Mix(uint x){unchecked{x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x;}}
        /// <summary>Deterministic hash of three integers in [0, 1).</summary>
        public static double Hash(int a,int b=0,int c=0){unchecked{return Mix(Mix(Mix((uint)a+0x9E3779B9U)^(uint)b*0x85EBCA6BU)+(uint)c*0xC2B2AE35U)/4294967296.0;}}
        private static double Range(double lo,double hi,int a,int b=0,int c=0)=>lo+(hi-lo)*Hash(a,b,c);
        private static int Pick(int[] list,int a,int b=0,int c=0)=>list[Math.Min(list.Length-1,(int)(Hash(a,b,c)*list.Length))];
        private static double Frac(double v)=>v-Math.Floor(v);
        private static double Smooth(double x){x=Math.Max(0,Math.Min(1,x));return x*x*(3-2*x);}

        // ---------------- the menu orbit (ArenaPresentation.OverviewCamera) ----------------
        /// <summary>Horizontal distance from (x, z) to the menu's overview orbit (42 cos t, 32, 46 sin(t + .2)).</summary>
        public static double OrbitDistance(double x,double z)
        {
            double best=double.MaxValue;for(int i=0;i<720;i++){double t=i*Math.PI/360,ox=42*Math.Cos(t),oz=46*Math.Sin(t+.2);best=Math.Min(best,Math.Sqrt((x-ox)*(x-ox)+(z-oz)*(z-oz)));}
            return best;
        }
        /// <summary>A footprint within 8 m of the orbit stays at or below this height (the camera flies at 32 m).</summary>
        public const double OrbitCap=24;
        private static double FootprintOrbit(double x0,double x1,double z0,double z1)
        {double best=double.MaxValue;for(int i=0;i<=4;i++)for(int j=0;j<=4;j++)best=Math.Min(best,OrbitDistance(x0+(x1-x0)*i/4,z0+(z1-z0)*j/4));return best;}

        // ---------------- facade frames ----------------
        // A facade's frame: u along the wall, v up, w out of the wall toward the street; (Ox, Oz) is u = 0 on the wall plane.
        private struct Frame
        {
            public double Ox,Oz,Ux,Uz,Wx,Wz;
            public static Frame Make(double ox,double oz,double wx,double wz)=>new Frame{Ox=ox,Oz=oz,Wx=wx,Wz=wz,Ux=-wz,Uz=wx};
            public void World(double u,double w,out double x,out double z){x=Ox+Ux*u+Wx*w;z=Oz+Uz*u+Wz*w;}
            // The local face of a box that points into the wall (-W), dropped from facade pieces.
            public int Back=>Wz<-.5?16:Wz>.5?32:Wx<-.5?1:2;
        }
        private static void FBox(NativeSceneryBuilder b,Frame f,double u0,double u1,double v0,double v1,double w0,double w1,int hex,double shade=1,int glow=0,double level=0,double grime=0,bool back=true)
        {
            if(u1<u0){double t=u0;u0=u1;u1=t;}if(u1-u0<1e-3||v1-v0<1e-3||w1-w0<1e-3)return;
            f.World((u0+u1)/2,(w0+w1)/2,out double x,out double z);bool alongX=Math.Abs(f.Ux)>.5;
            b.Box(x,(v0+v1)/2,z,alongX?u1-u0:w1-w0,v1-v0,alongX?w1-w0:u1-u0,hex,glow:glow,level:level,shade:shade,grime:grime,skip:back?f.Back:0);
        }
        /// <summary>A bar of section tx × ty between two points (local z along the bar, local x horizontal).</summary>
        private static void Bar(NativeSceneryBuilder b,double x0,double y0,double z0,double x1,double y1,double z1,double tx,double ty,int hex,double shade=1,int glow=0,double level=0)
        {
            double dx=x1-x0,dy=y1-y0,dz=z1-z0,l=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(l<1e-4)return;
            double yaw=Math.Atan2(dx,dz)*180/Math.PI,pitch=Math.Asin(Math.Max(-1,Math.Min(1,-dy/l)))*180/Math.PI;
            b.Box((x0+x1)/2,(y0+y1)/2,(z0+z1)/2,tx,ty,l,hex,pitch,yaw,glow:glow,level:level,shade:shade);
        }
        private static void FBar(NativeSceneryBuilder b,Frame f,double u0,double v0,double w0,double u1,double v1,double w1,double tx,double ty,int hex,double shade=1)
        {f.World(u0,w0,out double x0,out double z0);f.World(u1,w1,out double x1,out double z1);Bar(b,x0,v0,z0,x1,v1,z1,tx,ty,hex,shade);}

        // ---------------- buildings ----------------
        private struct Style
        {
            public int Brick,Trim,Fascia,Seed;public double Shade,Bay,WinW,WinH,Ground,Floor;public bool Quoins,Dentils,Escape,Sills,Shops,Upper;
        }
        private static Style MakeStyle(int seed,bool near)
        {
            var s=new Style{Seed=seed,Brick=Pick(Bricks,seed,1),Trim=Pick(Trims,seed,2),Fascia=Pick(Fascias,seed,3),Shade=Range(.86,1.08,seed,4),Bay=Range(2.5,3.2,seed,5),Ground=near?4.4:4.0,Floor=3.3};
            s.WinW=s.Bay*Range(.42,.52,seed,6);s.WinH=Range(1.7,2.0,seed,7);s.Quoins=Hash(seed,8)<.25;s.Dentils=near&&Hash(seed,9)<.6;s.Escape=near&&Hash(seed,10)<.55;s.Sills=near;s.Shops=near;return s;
        }
        // The window grid: columns centred in the width at the style's bay.
        private static int Columns(double width,Style s)=>Math.Max(1,(int)((width-1.0)/s.Bay));
        private static double ColumnU(double width,Style s,int i){int n=Columns(width,s);return (width-n*s.Bay)/2+s.Bay*(i+.5);}
        /// <summary>
        /// One facade: storefront (unless `Upper`), window grid (a dark glass sheet set 0.3 m back behind brick bands and piers;
        /// lit, sky-lit, boarded and broken panes; sills), string courses, stepped cornice, dentils, quoins, parapet and coping,
        /// and a fire escape. `keep[k]` clips upper floor k to u ≤ keep[k] (a half-demolished front); null keeps the whole width.
        /// </summary>
        private static void Facade(NativeSceneryBuilder b,Frame f,double width,double height,int floors,Style s,int detail,double[] keep=null)
        {
            const double D=.3;double g=s.Ground,fh=s.Floor;int n=Columns(width,s);double K(int k)=>keep==null?width:keep[Math.Max(0,Math.Min(keep.Length-1,k))];
            int brick=s.Brick;double sh=s.Shade,margin=(width-n*s.Bay)/2;
            if(!s.Upper)
            {
                // Storefront: stall risers, glass, piers on the bay lines, fascia with faux lettering, a terracotta course, awnings.
                FBox(b,f,0,width,0,.55,-D,0,brick,sh*.8,grime:.2);
                FBox(b,f,0,width,.55,g-.95,-D,-D+.04,DarkGlass);
                for(int i=0;i<=n;i++){double edge=margin+s.Bay*i,u0=i==0?0:edge-.25,u1=i==n?width:edge+.25;if(i==0)u1=Math.Max(u1,.5);if(i==n)u0=Math.Min(u0,width-.5);FBox(b,f,u0,u1,.55,g-.95,-D,0,brick,sh*.92,grime:.15);}
                if(s.Shops)for(int i=0;i<n;i++)
                {
                    double u0=margin+s.Bay*i+.25,u1=u0+s.Bay-.5,h=Hash(s.Seed,40,i);
                    if(h<.22)FBox(b,f,u0,u1,.55,g-.95,-D+.04,-.1,Timber,Range(.7,1,s.Seed,41,i));
                    else if(h<.4)FBox(b,f,u0,u1,.55,g-.95,-D+.04,-.12,0x6A665F,Range(.8,1,s.Seed,42,i));
                    else if(h<.55){FBox(b,f,(u0+u1)/2-.55,(u0+u1)/2+.55,0,2.3,-D-.2,-D+.06,Tar,1.1);FBox(b,f,u0,u1,2.4,g-.95,-D+.04,-D+.06,Lit,.25,Lit,.5);}
                    else if(h<.62)FBox(b,f,u0,u1,.6,g-1,-D+.04,-D+.06,Glass,.8,SkyGlow,.1);
                }
                FBox(b,f,0,width,g-.95,g-.15,0,.12,s.Fascia);
                if(s.Shops){int letters=4+(int)(Hash(s.Seed,43)*6);double lw=Math.Min(.55,width*.6/letters),start=width/2-letters*lw/2;int light=s.Fascia==Terracotta?Tar:LanePaint;
                    for(int i=0;i<letters;i++)FBox(b,f,start+i*lw+.06,start+(i+1)*lw-.06,g-.75,g-.35,.12,.16,light);}
                FBox(b,f,-.05,width+.05,g-.15,g+.05,0,.18,s.Trim);
                if(s.Shops)for(int i=0;i<n;i++)if(Hash(s.Seed,44,i)<.3)
                {
                    double u0=margin+s.Bay*i+.3,u1=u0+s.Bay-.6;int c0=Pick(Canvas,s.Seed,45,i);const int stripes=4;double yaw=Math.Atan2(f.Wx,f.Wz)*180/Math.PI;
                    for(int k=0;k<stripes;k++){double a=u0+(u1-u0)*k/stripes,c=u0+(u1-u0)*(k+1)/stripes;f.World((a+c)/2,.62,out double x,out double z);b.Box(x,g-1.35,z,c-a,.05,1.4,k%2==0?c0:LanePaint,28,yaw,shade:.95);}
                }
            }
            // Upper floors: glass sheet, bands with string courses, piers, panes, sills.
            if(floors>0)
            {
                double top=g+floors*fh;
                if(keep==null)FBox(b,f,0,width,g,top,-D,-D+.04,DarkGlass,1.4);
                else for(int k=0;k<floors;k++)FBox(b,f,0,K(k),g+k*fh,g+(k+1)*fh,-D,-D+.04,DarkGlass,1.4);
                for(int k=0;k<=floors;k++)
                {
                    double v0=k==0?g:g+(k-1)*fh+.9+s.WinH,v1=k==floors?height:g+k*fh+.9,kw=k==floors?K(floors-1):Math.Min(K(k),K(k-1));
                    FBox(b,f,0,kw,v0,v1,-D,0,brick,sh,grime:k==0?.12:0);
                    if(k>0&&k<floors&&detail>0)FBox(b,f,-.04,kw+.04,g+k*fh-.08,g+k*fh+.08,0,.07,s.Trim,.95);
                }
                for(int k=0;k<floors;k++)
                {
                    double wb=g+k*fh+.9,wt=wb+s.WinH,kw=K(k),last=0;
                    for(int i=0;i<n;i++)
                    {
                        double c=ColumnU(width,s,i),u0=c-s.WinW/2,u1=c+s.WinW/2;if(u1>kw-.2)break;
                        FBox(b,f,last,u0,wb,wt,-D,-.02,brick,sh*.97);last=u1;
                        double h=Hash(s.Seed,k*31+i,77);
                        if(h<.1)FBox(b,f,u0,u1,wb,wt,-D+.04,-D+.07,0x3A2E22,1,Lit,Range(.7,1.3,s.Seed,k,i));
                        else if(h<.3)FBox(b,f,u0,u1,wb,wt,-D+.04,-D+.07,Glass,.9,SkyGlow,Range(.06,.2,s.Seed,k,i+50));
                        else if(h<.5){FBox(b,f,u0,u1,wb,wt,-D+.04,-.12,Timber,Range(.62,.95,s.Seed,k,i+90));if(detail>0)FBar(b,f,u0+.08,wb+.1,-.1,u1-.08,wt-.1,-.1,.14,.04,Timber,.75);}
                        else if(h<.62)FBox(b,f,u0,u1,wb,wt,-D+.04,-D+.06,0x111316);
                        if(s.Sills)FBox(b,f,u0-.08,u1+.08,wb-.1,wb,-D,.12,s.Trim,.95);
                    }
                    if(kw-last>.01)FBox(b,f,last,kw,wb,wt,-D,-.02,brick,sh*.97);
                }
            }
            // Roofline: stepped cornice, dentils, parapet and coping; corner quoins.
            double kt=K(floors-1);
            FBox(b,f,0,kt,height-.6,height-.38,0,.16,s.Trim);FBox(b,f,-.1,kt+.1,height-.38,height-.16,0,.32,s.Trim,.96);FBox(b,f,-.2,kt+.2,height-.16,height,0,.48,s.Trim,.92);
            if(s.Dentils&&detail>0)for(double u=.3;u<kt-.3;u+=.62)FBox(b,f,u,u+.26,height-.9,height-.6,0,.14,s.Trim,.9);
            FBox(b,f,0,kt,height,height+.9,-D,0,brick,sh*.95);FBox(b,f,-.05,kt+.05,height+.9,height+1.02,-D-.05,.06,Concrete,1.05);
            if(s.Quoins&&keep==null)for(int side=0;side<2;side++)for(int k=0;g+k*.62<height-.7;k++){double v=g+k*.62,qw=k%2==0?.5:.85,u0=side==0?0:width-qw;FBox(b,f,u0,u0+qw,v,v+.56,0,.05,s.Trim,.98);}
            // Fire escape on one window column, floor 1 to the top, with a drop ladder.
            if(s.Escape&&detail>0&&floors>=3&&n>=3)
            {
                int col=1+(int)(Hash(s.Seed,60)*(n-2));double c=ColumnU(width,s,col);if(keep!=null&&c+1.7>K(floors-1))return;
                for(int k=1;k<floors;k++)
                {
                    double v=g+k*fh+.08;
                    FBox(b,f,c-1.6,c+1.6,v,v+.08,.02,1.15,Iron,back:false);FBox(b,f,c-1.6,c+1.6,v+1.02,v+1.07,1.08,1.13,Iron);FBox(b,f,c-1.6,c+1.6,v+.55,v+.59,1.08,1.13,Iron);
                    for(int p=0;p<3;p++){double u=c-1.58+p*1.58;FBox(b,f,u-.025,u+.025,v+.08,v+1.02,1.08,1.13,Iron);}
                    FBox(b,f,c-1.6,c-1.55,v+1.02,v+1.07,.02,1.08,Iron);FBox(b,f,c+1.55,c+1.6,v+1.02,v+1.07,.02,1.08,Iron);
                    if(k<floors-1){bool right=k%2==0;FBar(b,f,right?c-1.15:c+1.15,v+.1,.75,right?c+1.05:c-1.05,v+fh,.75,.62,.05,Iron);}
                }
                double lv=g+fh+.08;FBox(b,f,c+1.1,c+1.14,lv-2.6,lv,.95,.99,Iron);FBox(b,f,c+1.5,c+1.54,lv-2.6,lv,.95,.99,Iron);
                for(int r=0;r<6;r++)FBox(b,f,c+1.14,c+1.5,lv-2.5+r*.42,lv-2.46+r*.42,.95,.99,Iron);
            }
        }
        private static void WaterTower(NativeSceneryBuilder b,double x,double y,double z,int seed)
        {
            double leg=3+Hash(seed,1)*1.2;int wood=Hash(seed,2)<.5?0x8A6A48:0x7E6A52;
            for(int i=0;i<4;i++){double lx=x+(i%2==0?-1.1:1.1),lz=z+(i<2?-1.1:1.1);b.Box(lx,y+leg/2,lz,.16,leg,.16,Iron);}
            Bar(b,x-1.1,y+.3,z-1.1,x+1.1,y+leg-.3,z-1.1,.06,.06,Iron);Bar(b,x+1.1,y+.3,z+1.1,x-1.1,y+leg-.3,z+1.1,.06,.06,Iron);
            Bar(b,x-1.1,y+.3,z+1.1,x-1.1,y+leg-.3,z-1.1,.06,.06,Iron);Bar(b,x+1.1,y+.3,z-1.1,x+1.1,y+leg-.3,z+1.1,.06,.06,Iron);
            b.Box(x,y+leg+.06,z,3,.12,3,Iron);double h=3.2,t=y+leg+.12;
            b.Box(x,t+h/2,z,2.7,h,2.7,wood,shade:.95);b.Box(x,t+h/2,z,2.7,h,2.7,wood,yaw:45,shade:.9);
            for(int r=0;r<3;r++){double hy=t+.4+r*1.15;b.Box(x,hy,z,2.78,.07,2.78,Iron);b.Box(x,hy,z,2.78,.07,2.78,Iron,yaw:45);}
            for(int r=0;r<3;r++){double s=2.3-r*.7,cy=t+h+.17+r*.34;b.Box(x,cy,z,s,.34,s,Tar);b.Box(x,cy,z,s,.34,s,Tar,yaw:45);}
            b.Box(x,t+h+1.22,z,.12,.4,.12,Iron);
        }
        /// <summary>Roof surface at `y` (the body top, inside the parapet) with a stair house, chimney stacks and maybe a water tower.</summary>
        private static void Roof(NativeSceneryBuilder b,double x0,double x1,double z0,double z1,double y,int seed,bool tower)
        {
            b.Box((x0+x1)/2,y+.03,(z0+z1)/2,x1-x0-.7,.06,z1-z0-.7,Tar);
            double w=x1-x0,d=z1-z0;
            if(Hash(seed,20)<.7){double bx=x0+w*Range(.2,.8,seed,21),bz=z0+d*Range(.3,.7,seed,22);b.Box(bx,y+1.4,bz,2.8,2.8,2.4,Pick(Bricks,seed,23),shade:.85);b.Box(bx,y+2.86,bz,3,.12,2.6,Concrete);}
            int stacks=1+(int)(Hash(seed,24)*3);for(int i=0;i<stacks;i++){double cx=x0+.8+(w-1.6)*Hash(seed,25,i),cz=z0+.8+(d-1.6)*Hash(seed,26,i),h=Range(1.2,2.6,seed,27,i);b.Box(cx,y+h/2,cz,.8,h,.8,BrickDark,grime:.3);b.Box(cx,y+h+.06,cz,.95,.12,.95,Concrete);}
            if(tower&&w>7&&d>7)WaterTower(b,x0+w*Range(.25,.75,seed,28),y,z0+d*Range(.3,.7,seed,29),seed);
        }
        /// <summary>A ghost sign: a faded painted advertisement on a side wall (background and two letter bars).</summary>
        private static void GhostSign(NativeSceneryBuilder b,Frame f,double u0,double u1,double v0,double v1,int seed)
        {
            FBox(b,f,u0,u1,v0,v1,0,.025,Mortar,Range(.78,.92,seed,1));int ink=Hash(seed,2)<.5?0x8C5A4A:0x5A6A7A;
            FBox(b,f,u0+.4,u1-.4,v0+(v1-v0)*.55,v0+(v1-v0)*.82,.025,.045,ink,.85);FBox(b,f,u0+.8,u1-.8,v0+(v1-v0)*.2,v0+(v1-v0)*.4,.025,.045,ink,.75);
        }
        /// <summary>The imprint a demolished neighbour left on a party wall: wallpaper per floor, a stair, a soot flue.</summary>
        private static void PartyGhost(NativeSceneryBuilder b,Frame f,double width,double height,int seed)
        {
            const double fh=3.3;for(int k=0;4.0+k*fh<height-1;k++){double v=4.0+k*fh,split=width*Range(.35,.65,seed,k);
                FBox(b,f,.3,split-.1,v+.15,v+fh-.2,0,.03,Pick(Wallpapers,seed,k,1),.8);FBox(b,f,split+.1,width-.3,v+.15,v+fh-.2,0,.03,Pick(Wallpapers,seed,k,2),.75);}
            FBar(b,f,width*.15,1,.04,width*.6,Math.Min(height-2,11),.04,.5,.02,0x3A2E2A,.9);
            FBox(b,f,width*.78,width*.78+.7,0,height,0,.045,Soot,.9);
        }
        // The frame of a lot's side `face` (0: -z side facing south, 1: -x side facing west, 2: +z facing north, 3: +x facing east).
        private static void FaceFrame(double x0,double x1,double z0,double z1,int face,out Frame f,out double width)
        {
            switch(face&3)
            {
                case 0:f=Frame.Make(x0,z0,0,-1);width=x1-x0;break;
                case 1:f=Frame.Make(x0,z1,-1,0);width=z1-z0;break;
                case 2:f=Frame.Make(x1,z1,0,1);width=x1-x0;break;
                default:f=Frame.Make(x1,z0,1,0);width=z1-z0;break;
            }
        }
        private static void Inset(ref double x0,ref double x1,ref double z0,ref double z1,int face,double d){if(face==0)z0+=d;else if(face==1)x0+=d;else if(face==2)z1-=d;else x1-=d;}
        /// <summary>
        /// A walk-up block on a lot: body, a detailed facade on the side `face` toward the arena, a setback tier for tall far
        /// blocks, roof clutter (water towers only where `towers`: none under a crane's jib) and, on some, a ghost sign high on a side wall.
        /// </summary>
        private static void Building(NativeSceneryBuilder b,double x0,double x1,double z0,double z1,int face,int floors,int seed,bool near,bool setback,bool towers=true)
        {
            var s=MakeStyle(seed,near);double height=s.Ground+floors*s.Floor;const double D=.3;
            FaceFrame(x0,x1,z0,z1,face,out var f,out double width);
            int sb=floors>=9&&setback?floors-2-(int)(Hash(seed,30)*2):floors;double lower=s.Ground+sb*s.Floor;
            double bx0=x0,bx1=x1,bz0=z0,bz1=z1;Inset(ref bx0,ref bx1,ref bz0,ref bz1,face,D);
            b.Box((bx0+bx1)/2,lower/2,(bz0+bz1)/2,bx1-bx0,lower,bz1-bz0,s.Brick,shade:s.Shade*.92,grime:.18);
            Facade(b,f,width,lower,sb,s,near?1:0);
            if(sb<floors)
            {
                // The setback: the top floors stand 2.6 m back behind a terrace with their own facade, cornice and roof.
                double ux0=x0,ux1=x1,uz0=z0,uz1=z1;Inset(ref ux0,ref ux1,ref uz0,ref uz1,face,2.6);double ubx0=ux0,ubx1=ux1,ubz0=uz0,ubz1=uz1;Inset(ref ubx0,ref ubx1,ref ubz0,ref ubz1,face,D);
                b.Box((ubx0+ubx1)/2,(lower+height)/2,(ubz0+ubz1)/2,ubx1-ubx0,height-lower,ubz1-ubz0,s.Brick,shade:s.Shade*.9);
                b.Box((x0+x1)/2,lower+.03,(z0+z1)/2,x1-x0-.7,.06,z1-z0-.7,Tar);
                FaceFrame(ux0,ux1,uz0,uz1,face,out var f2,out double w2);var s2=s;s2.Ground=lower;s2.Upper=true;s2.Escape=false;s2.Quoins=false;
                Facade(b,f2,w2,height,floors-sb,s2,near?1:0);
                Roof(b,ux0,ux1,uz0,uz1,height,seed,towers);
            }
            else Roof(b,x0,x1,z0,z1,height,seed,towers&&floors>=5);
            if(Hash(seed,31)<.3&&floors>=5){FaceFrame(x0,x1,z0,z1,(face+1)%4,out var fs,out double ws);double gw=Math.Min(ws-1,Range(5,9,seed,32));GhostSign(b,fs,ws/2-gw/2,ws/2+gw/2,height-1.2-gw*.55,height-1.2,seed);}
        }
        /// <summary>
        /// A half-demolished block: the arena side of its upper floors is stepped away, leaving the rear wall with wallpapered
        /// rooms, dado rails, doorways, chimney breasts and broken partitions, floor slabs whose fresh edges overhang with drooping
        /// rebar (some slabs hang from the rear wall, some are gone), a broken brick edge on every floor and, with `spill`,
        /// rubble at its foot (in the Near mesh, which already takes the arena's point lights).
        /// </summary>
        private static void Ruin(NativeSceneryBuilder b,double x0,double x1,double z0,double z1,int face,int floors,int seed,bool near,bool spill)
        {
            var s=MakeStyle(seed,near);s.Escape=false;s.Quoins=false;const double D=.3;
            FaceFrame(x0,x1,z0,z1,face,out var f,out double width);double depth=face==0||face==2?z1-z0:x1-x0;
            var keep=new double[floors];for(int k=0;k<floors;k++)keep[k]=k<1?width:Math.Max(width*.3,Math.Min(width,width*(1.02-.13*k)-Hash(seed,k,3)*1.5));
            void Piece(double u0,double u1,double w0,double w1,double v0,double v1,int hex,double shade=1)=>FBox(b,f,u0,u1,v0,v1,w0,w1,hex,shade,back:false);
            Piece(0,width,-depth,-D,0,s.Ground,s.Brick,s.Shade*.92);
            for(int k=0;k<floors;k++)
            {
                double v=s.Ground+k*s.Floor,kw=keep[k];Piece(0,kw,-depth,-D,v,v+s.Floor,s.Brick,s.Shade*.9);
                if(kw>=width-.1)continue;
                Piece(kw,width,-depth,-depth+.35,v,v+s.Floor,s.Brick,s.Shade*.88);
                double slab=Hash(seed,k,5);
                if(slab<.7)
                {
                    double over=Range(.1,.9,seed,k,6);Piece(kw,width,-depth+.35,over,v-.3,v,FreshConcrete,Range(.82,1,seed,k,7));
                    int bar=0;for(double u=kw+.4;u<width-.2;u+=Range(.55,.9,seed,k,bar),bar++)
                    {double l=Range(.4,1.3,seed,k,bar+100),pitch=Range(.15,.65,seed,k,bar+200);FBar(b,f,u,v-.15,over,u+Range(-.2,.2,seed,k,bar+300),v-.15-l*Math.Sin(pitch),over+l*Math.Cos(pitch),.035,.035,Rust);}
                }
                else if(slab<.85){double drop=s.Floor*Range(.5,.9,seed,k,8);FBar(b,f,(kw+width)/2,v-.15,-depth+.35,(kw+width)/2,v-.15-drop,-.1,width-kw,.3,FreshConcrete,.9);}
                // Rooms on the rear wall: wallpaper, dado, a doorway or a chimney breast; broken partitions between them.
                double u0=kw;int room=0;
                while(u0<width-.6)
                {
                    double u1=Math.Min(width,u0+Range(3,4.6,seed,k,room+20));
                    Piece(u0+.06,u1-.06,-depth+.35,-depth+.38,v+.02,v+s.Floor-.32,Pick(Wallpapers,seed,k,room),Range(.75,1,seed,k,room+30));
                    Piece(u0+.06,u1-.06,-depth+.38,-depth+.42,v+.86,v+.94,0x6A5040);
                    if(Hash(seed,k,room+40)<.45){double dc=(u0+u1)/2+Range(-.6,.6,seed,k,room+41);Piece(dc-.45,dc+.45,-depth+.38,-depth+.4,v+.02,v+2.1,0x2A2220);}
                    else if(Hash(seed,k,room+42)<.6){double dc=(u0+u1)/2;Piece(dc-.7,dc+.7,-depth+.38,-depth+.75,v,v+s.Floor-.3,s.Brick,.8);Piece(dc-.4,dc+.4,-depth+.75,-depth+.77,v+.1,v+.9,0x161414);}
                    if(u1<width-.3){double ph=s.Floor*Range(.35,1,seed,k,room+50),pl=(depth-.35)*Range(.25,.8,seed,k,room+51);Piece(u1-.06,u1+.06,-depth+.35,-depth+.35+pl,v,v+ph,Mortar,.9);}
                    u0=u1;room++;
                }
                int row=0;for(double bv=v;bv<v+s.Floor-.05;bv+=.34,row++){double l=Range(.05,.75,seed,k,row+400);Piece(kw,kw+l,-D,0,bv,bv+.3,s.Brick,s.Shade*Range(.85,1,seed,k,row+500));}
            }
            double top=s.Ground+floors*s.Floor;
            Facade(b,f,width,top,floors,s,near?1:0,keep);
            Piece(0,keep[floors-1],-depth,-D,top,top+.06,Tar);
            if(!spill)return;
            int saved=b.Current;b.Current=Near;int count=near?70:26;double cu=(keep[floors-1]+width)/2;
            for(int i=0;i<count;i++)
            {
                double a=Hash(seed,i,90)*Math.PI*2,r=Math.Sqrt(Hash(seed,i,91)),su=cu+Math.Cos(a)*r*Math.Min(6,width*.35),sw=.6+Math.Abs(Math.Sin(a))*r*4.2,mound=2.4*(1-r*.85),size=Range(.3,1.4,seed,i,92);
                f.World(su,sw,out double rx,out double rz);if(Math.Abs(rx)<InnerX+1.5&&Math.Abs(rz)<InnerZ+1.5)continue;
                double hh=Hash(seed,i,93);int hex=hh<.45?BrickRed:hh<.65?FreshBrick:hh<.85?FreshConcrete:Timber;
                b.Box(rx,Math.Max(size*.25,mound*Hash(seed,i,94)),rz,size,size*Range(.35,.8,seed,i,95),size*Range(.5,1.2,seed,i,96),hex,Range(-30,30,seed,i,97),Range(0,180,seed,i,98),Range(-25,25,seed,i,99),shade:Range(.75,1.05,seed,i,100));
            }
            b.Current=saved;
        }

        // ---------------- rows ----------------
        /// <summary>
        /// A row of lots along one side, `a` running along the row from a0 to a1, fronts on the line `front` (distance from the
        /// centre); lots overlapping [skip0, skip1] are left out (the crane sites); lot number `ruin` is half-demolished.
        /// </summary>
        private static void Row(NativeSceneryBuilder b,int side,double a0,double a1,double front,double dmin,double dmax,int fmin,int fmax,int seed,bool near,double skip0,double skip1,int ruin)
        {
            double a=a0;int lot=0;
            while(a<a1-4)
            {
                double w=Range(near?9:12,near?19:25,seed,lot,1);if(a+w>a1)w=a1-a;if(w<6)break;
                if(a<skip1&&a+w>skip0){a=Math.Max(a+w,skip1);lot++;continue;}
                double depth=Range(dmin,dmax,seed,lot,2);int floors=fmin+(int)(Hash(seed,lot,3)*(fmax-fmin+1));
                double x0,x1,z0,z1;int face;
                switch(side)
                {
                    case North:x0=a;x1=a+w;z0=front;z1=front+depth;face=0;break;
                    case South:x0=-a-w;x1=-a;z0=-front-depth;z1=-front;face=2;break;
                    case East:x0=front;x1=front+depth;z0=-a-w;z1=-a;face=1;break;
                    default:x0=-front-depth;x1=-front;z0=a;z1=a+w;face=3;break;
                }
                // Within 8 m of the menu orbit: no higher than the orbit cap; within 45 m of a crane mast: under the hook.
                double height=4.4+floors*3.3+1;
                while(floors>2&&((FootprintOrbit(x0,x1,z0,z1)<8&&height>OrbitCap)||(NearCrane(x0,x1,z0,z1)&&height>36))){floors--;height=4.4+floors*3.3+1;}
                if(lot==ruin)Ruin(b,x0,x1,z0,z1,face,floors,seed*97+lot,near,true);
                else Building(b,x0,x1,z0,z1,face,floors,seed*97+lot,near,!near,!NearCrane(x0,x1,z0,z1));
                a+=w+(Hash(seed,lot,4)<.3?3:0);lot++;
            }
        }
        private static bool NearCrane(double x0,double x1,double z0,double z1)
        {
            for(int c=0;c<2;c++){double mx=CraneX[c],mz=CraneZ[c],dx=Math.Max(0,Math.Max(x0-mx,mx-x1)),dz=Math.Max(0,Math.Max(z0-mz,mz-z1));if(dx*dx+dz*dz<45*45)return true;}
            return false;
        }
        /// <summary>A far, plain block: body, glass bands per floor and a few lit windows on the arena side, a cornice, maybe a water tower (not under a jib).</summary>
        private static void Plain(NativeSceneryBuilder b,double x0,double x1,double z0,double z1,int face,double height,int seed)
        {
            b.Box((x0+x1)/2,height/2,(z0+z1)/2,x1-x0,height,z1-z0,Pick(Bricks,seed,1),shade:Range(.8,1,seed,2),grime:.2);
            FaceFrame(x0,x1,z0,z1,face,out var f,out double width);
            for(double v=5;v<height-2;v+=3.3)FBox(b,f,.6,width-.6,v,v+1.6,0,.03,DarkGlass,1.2);
            int lit=(int)(height/9);for(int i=0;i<lit;i++){double v=5+3.3*(int)(Hash(seed,i,3)*((height-7)/3.3)),u=.8+Hash(seed,i,4)*(width-2.4);FBox(b,f,u,u+1.1,v+.1,v+1.5,.03,.05,0x3A2E22,1,Lit,1.1);}
            FBox(b,f,-.1,width+.1,height-.3,height,0,.3,Pick(Trims,seed,5),.9);
            if(Hash(seed,6)<.4&&!NearCrane(x0,x1,z0,z1))WaterTower(b,(x0+x1)/2+Range(-2,2,seed,7),height,(z0+z1)/2,seed);
        }

        // ---------------- the skyline ----------------
        /// <summary>
        /// The skyline ring (126-168 m out): towers in up to three tiers with Deco piers and sparse lit windows on the side facing
        /// the arena, spires with red lamps or water tanks on top, and a warm emissive rim cap on every tier, brightest on the
        /// towers between the arena and the §3.10 sun (backlit silhouettes).
        /// </summary>
        private static void Skyline(NativeSceneryBuilder b)
        {
            double sun=SunBearing*Math.PI/180,sx=Math.Sin(sun),sz=Math.Cos(sun);int i=0;
            for(double ang=0;ang<360;i++)
            {
                double a=ang*Math.PI/180,r=Range(126,168,900,i),w=Range(13,28,900,i,1),d=Range(13,26,900,i,2),h=Range(34,92,900,i,3);
                double cx=Math.Sin(a)*r,cz=Math.Cos(a)*r;ang+=Range(5,9.5,900,i,4);
                b.Current=Math.Abs(cx)*(InnerZ/InnerX)>Math.Abs(cz)?(cx>0?East:West):(cz>0?North:South);
                double rim=.5+1.6*Math.Max(0,Math.Sin(a)*sx+Math.Cos(a)*sz);int tone=Hash(900,i,5)<.5?0x4A4550:0x524840;
                // The face toward the arena: the dominant axis of the direction back to the centre.
                bool alongX=Math.Abs(cx)>Math.Abs(cz);double fx=alongX?-Math.Sign(cx):0,fz=alongX?0:-Math.Sign(cz);
                int tiers=h>60?3:h>44?2:1;double y=0,tw=w,td=d;
                for(int t=0;t<tiers;t++)
                {
                    double th=t==0?h*(tiers==1?1:.62):h*(t==1?.24:.14),half=alongX?tw/2:td/2,span=alongX?td:tw;
                    b.Box(cx,y+th/2,cz,tw,th,td,tone,shade:Range(.85,1.1,900,i,6+t),grime:.15);
                    b.Box(cx,y+th+.12,cz,tw+.3,.24,td+.3,SunRim,glow:SunRim,level:rim*(t==tiers-1?1:.6),shade:.6);
                    double px=cx+fx*(half+.08),pz=cz+fz*(half+.08);
                    for(int p=-2;p<=2;p++){double off=p*span*.2;b.Box(px+(alongX?0:off),y+th/2,pz+(alongX?off:0),alongX?.16:.7,th*.94,alongX?.7:.16,tone,shade:1.15);}
                    int lit=(int)(th/12);for(int k=0;k<lit;k++){double ly=y+3+Hash(900,i*13+t,k)*(th-5),lo=(Hash(900,i*17+t,k)-.5)*span*.8;b.Box(cx+fx*(half+.1)+(alongX?0:lo),ly,cz+fz*(half+.1)+(alongX?lo:0),alongX?.12:1.2,1.1,alongX?1.2:.12,0x3A2E22,glow:Lit,level:1.4);}
                    y+=th;tw*=.72;td*=.72;
                }
                if(Hash(900,i,9)<.35){double sp=Range(6,16,900,i,10);b.Box(cx,y+sp/2,cz,.35,sp,.35,ConcreteDark);b.Box(cx,y+sp+.3,cz,.5,.5,.5,Danger,glow:Danger,level:1.2);}
                else if(Hash(900,i,11)<.5)WaterTower(b,cx,y,cz,900+i);
            }
            b.Current=Near;
        }

        // ---------------- the viaduct and the train ----------------
        public const double ViaductNorth=-34.5,ViaductSouth=-43.5,ViaductSpring=11.3,DeckY=18,RailY=18.25,ViaductX0=-306,ViaductX1=306;
        private static void Viaduct(NativeSceneryBuilder b)
        {
            double zc=(ViaductNorth+ViaductSouth)/2,depth=ViaductNorth-ViaductSouth;const double pitch=12,pier=2.4,r=(pitch-pier)/2;int span=0;
            for(double x=ViaductX0;x<ViaductX1;x+=pitch,span++)
            {
                b.Box(x,ViaductSpring/2,zc,pier,ViaductSpring,depth,BrickRed,shade:Range(.9,1,700,span),grime:.25);
                b.Box(x,ViaductSpring+.15,zc,pier+.3,.3,depth+.3,Terracotta,shade:.9);
                b.Box(x,DeckY/2-.5,ViaductNorth+.12,1.2,DeckY-1,.24,BrickDark,shade:.95);b.Box(x,DeckY/2-.5,ViaductSouth-.12,1.2,DeckY-1,.24,BrickDark,shade:.95);
                if(x+pitch>=ViaductX1)break;
                // Arch: twelve strips stepping up the semicircle, voussoirs ringing it on the arena face.
                const int strips=12;double sw=(pitch-pier)/strips;
                for(int j=0;j<strips;j++)
                {
                    double u=x+pier/2+sw*(j+.5),d=u-(x+pitch/2),y=ViaductSpring+.3+Math.Sqrt(Math.Max(0,r*r-d*d))*.98;
                    b.Box(u,(y+17)/2,zc,sw+.002,17-y,depth,BrickRed,shade:Range(.9,1.02,700,span,j),grime:.1);
                    b.Box(u,y+.22,ViaductNorth+.05,sw+.002,.44,.1,FreshBrick,shade:.88);
                }
                // A third of the arches are bricked up into workshops: wall, door, a lit window.
                if(Hash(700,span,3)<.32)
                {
                    for(int j=0;j<strips;j++){double u=x+pier/2+sw*(j+.5),d=u-(x+pitch/2),y=ViaductSpring+.3+Math.Sqrt(Math.Max(0,r*r-d*d))*.98;b.Box(u,y/2,ViaductNorth-.8,sw+.002,y,.4,BrickDark,shade:.9,grime:.3);}
                    b.Box(x+pitch/2-1.3,1.6,ViaductNorth-.56,2.6,3.2,.1,Hash(700,span,4)<.5?Green:Rust,shade:.8);
                    b.Box(x+pitch/2+2.6,4.6,ViaductNorth-.56,1.2,1.2,.08,0x3A2E22,glow:Lit,level:1.2);
                }
            }
            // Deck, arena-side cornice with corbels, parapets and copings, ballast and rails.
            for(double x=ViaductX0;x<ViaductX1;x+=78)
            {
                double l=Math.Min(78,ViaductX1-x),cx=x+l/2;
                b.Box(cx,17.5,zc,l,1,depth,BrickRed,shade:.94);
                b.Box(cx,17.05,ViaductNorth+.18,l,.5,.36,Terracotta,shade:.92);
                b.Box(cx,18.55,ViaductNorth-.22,l,1.1,.45,BrickRed,shade:.97);b.Box(cx,19.18,ViaductNorth-.22,l,.15,.6,Terracotta);
                b.Box(cx,18.55,ViaductSouth+.22,l,1.1,.45,BrickRed,shade:.9);b.Box(cx,19.18,ViaductSouth+.22,l,.15,.6,Terracotta);
                b.Box(cx,18.06,TrainNorthZ,l,.12,2.8,Cobble);b.Box(cx,18.06,TrainSouthZ,l,.12,2.8,Cobble);
                for(int t=0;t<2;t++){double tz=t==0?TrainNorthZ:TrainSouthZ;b.Box(cx,18.18,tz-.72,l,.14,.08,ConcreteDark,shade:1.3);b.Box(cx,18.18,tz+.72,l,.14,.08,ConcreteDark,shade:1.3);}
            }
            for(double x=ViaductX0+3;x<ViaductX1;x+=3)b.Box(x,16.68,ViaductNorth+.1,.3,.28,.2,Terracotta,shade:.85);
            // Catenary masts with cross arms over both tracks, and the two contact wires.
            for(double x=ViaductX0+6;x<ViaductX1;x+=36)
            {
                b.Box(x,22,ViaductNorth-.7,.26,8,.26,Green);Bar(b,x,25.6,ViaductNorth-.7,x,25.6,ViaductSouth+1.2,.16,.16,Green);
                Bar(b,x,25.6,ViaductNorth-1.6,x,24.4,ViaductNorth-.7,.08,.08,Green);
                b.Box(x,25.1,TrainNorthZ,.06,.9,.06,Iron);b.Box(x,25.1,TrainSouthZ,.06,.9,.06,Iron);
            }
            b.Box(0,24.62,TrainNorthZ,ViaductX1-ViaductX0,.04,.04,Iron);b.Box(0,24.62,TrainSouthZ,ViaductX1-ViaductX0,.04,.04,Iron);
            // A signal post near the east end.
            b.Box(58,21.2,ViaductNorth-.6,.18,6.4,.18,Iron);b.Box(58,24.2,ViaductNorth-.45,.6,1.3,.35,Tar);b.Box(58,24.5,ViaductNorth-.26,.22,.22,.04,Danger,glow:Danger,level:2.2);
        }
        public const double TrainPeriod=45,TrainSpeed=26,TrainStart=4,TrainFrom=-240,TrainTo=240,TrainNorthZ=-37.5,TrainSouthZ=-40.5;
        public const int TrainCars=4;public const double CarLength=14.5,CarGap=.6,TrainLength=TrainCars*CarLength+(TrainCars-1)*CarGap;
        /// <summary>
        /// The viaduct train at `time`: every <see cref="TrainPeriod"/> s a four-car train starts across at 26 m/s, eastbound on
        /// the north track in even periods and westbound on the south track in odd ones. `head` is the leading end's x;
        /// the body trails behind it. False while no train is on the viaduct.
        /// </summary>
        public static bool Train(double time,out double head,out bool east)
        {
            double n=Math.Floor(time/TrainPeriod),a=time-n*TrainPeriod-TrainStart;east=((long)n&1)==0;head=0;
            if(double.IsNaN(a)||a<0||a*TrainSpeed>TrainTo-TrainFrom+TrainLength)return false;
            head=east?TrainFrom+a*TrainSpeed:TrainTo-a*TrainSpeed;return true;
        }
        /// <summary>The train in its own frame: head at x = 0 facing +x, rail top at y = 0, track centre z = 0.</summary>
        public static NativeSceneryMesh BuildTrain()
        {
            var b=new NativeSceneryBuilder(1);
            for(int c=0;c<TrainCars;c++)
            {
                double x1=-c*(CarLength+CarGap),x0=x1-CarLength,cx=(x0+x1)/2;
                b.Box(cx,2.6,0,CarLength,3.2,2.9,Green);b.Box(cx,.85,0,CarLength-.6,.3,2.8,Tar);
                b.Box(cx,2.75,1.465,CarLength-.3,1.1,.03,Terracotta);b.Box(cx,2.75,-1.465,CarLength-.3,1.1,.03,Terracotta);
                b.Box(cx,4.38,0,CarLength-.2,.36,2.6,ConcreteDark,shade:1.2);b.Box(cx,4.66,0,CarLength-2.4,.2,1.2,ConcreteDark);
                for(int side=0;side<2;side++)
                {
                    double z=side==0?1.49:-1.49;
                    for(int w=0;w<9;w++){double wx=x0+1.1+w*(CarLength-2.2)/8.6;bool lit=Hash(400,c*20+w,side)<.82;b.Box(wx,2.75,z,.95,.8,.03,lit?0x3A2E22:DarkGlass,glow:lit?Lit:0,level:1.3);}
                    for(int dr=0;dr<2;dr++){double dx=x0+CarLength*(dr==0?.3:.7);b.Box(dx,2.45,z*1.005,1.1,2.9,.03,0x2F5446);b.Box(dx,3.1,z*1.01,.6,.7,.02,0x3A2E22,glow:Lit,level:1.1);}
                }
                for(int bg=0;bg<2;bg++){double bx=bg==0?x0+2.4:x1-2.4;b.Box(bx,.45,0,2.6,.55,2.3,Iron);b.Box(bx-.8,.35,1.2,.8,.5,.1,Iron);b.Box(bx+.8,.35,1.2,.8,.5,.1,Iron);b.Box(bx-.8,.35,-1.2,.8,.5,.1,Iron);b.Box(bx+.8,.35,-1.2,.8,.5,.1,Iron);}
                if(c<TrainCars-1)b.Box(x0-CarGap/2,1.2,0,CarGap+.2,.3,.3,Iron);
            }
            // Motor car front: windscreens, destination board, headlight; pantograph on its roof; tail lamps on the last car.
            b.Box(.01,3,.62,.03,.85,.95,0x3A2E22,glow:SunRim,level:.25);b.Box(.01,3,-.62,.03,.85,.95,0x3A2E22,glow:SunRim,level:.25);
            b.Box(.01,3.85,0,.03,.3,1.4,0x3A2E22,glow:Terracotta,level:.9);b.Box(.02,1.5,0,.04,.32,.32,Flood,glow:Flood,level:3.2);
            for(int s=-1;s<=1;s+=2){Bar(b,-4.9,4.76,s*.5,-4,6.3,s*.5,.05,.05,Iron);Bar(b,-3.1,4.76,s*.5,-4,6.3,s*.5,.05,.05,Iron);}
            b.Box(-4,4.8,0,2,.1,1.2,Iron);b.Box(-4,6.33,0,.08,.06,1.9,Iron);
            double tail=-TrainLength;b.Box(tail-.01,1.5,.9,.04,.24,.24,Danger,glow:Danger,level:2.4);b.Box(tail-.01,1.5,-.9,.04,.24,.24,Danger,glow:Danger,level:2.4);
            return b.Bake(0);
        }

        // ---------------- the tower cranes ----------------
        /// <summary>The two 45 m tower cranes: north (washing the hotel's north face) and its point-symmetric partner south.</summary>
        public static readonly double[] CraneX={13,-13},CraneZ={62,-70};
        public const double MastHeight=45,JibLength=40,CounterJib=14,ApexHeight=7.2;
        /// <summary>
        /// The jib's bearing (degrees from +z toward +x) at `time`: a 150 s cycle of hold (35%), ease across (15%), hold, ease
        /// back, between two picks whose bearings keep the jib, trolley and hook out of the arena: the north crane swings
        /// -100° to +90° through north, the south crane 80° to 270° through south, 0.37 of a cycle out of step.
        /// </summary>
        public static double CraneSlew(int crane,double time)
        {
            double p=Frac(time/150+(crane==0?0:.37)),a=crane==0?-100:80,c=crane==0?90:270,s;
            if(p<.35)s=0;else if(p<.5)s=Smooth((p-.35)/.15);else if(p<.85)s=1;else s=1-Smooth((p-.85)/.15);
            return a+(c-a)*s;
        }
        /// <summary>Aviation obstruction light: red flashes at 40 per minute (0.6 s on per 1.5 s), offset per crane and light.</summary>
        public static bool AviationOn(int crane,int light,double time)=>Frac((time+crane*.73+light*.11)/1.5)<.4;
        public const int AviationLights=3;
        /// <summary>Aviation light `light` (0 apex, 1 jib tip, 2 counter-jib tip) in the jib frame.</summary>
        public static void AviationLocal(int light,out double x,out double y,out double z){x=0;y=light==0?ApexHeight+.25:light==1?2.45:1.2;z=light==0?0:light==1?JibLength:-CounterJib;}
        /// <summary>Jib-frame point to world: the crane's mast top, turned by its slew.</summary>
        public static void JibToWorld(int crane,double time,double lx,double ly,double lz,out double x,out double y,out double z)
        {double t=CraneSlew(crane,time)*Math.PI/180,c=Math.Cos(t),s=Math.Sin(t);x=CraneX[crane]+lx*c+lz*s;y=MastHeight+ly;z=CraneZ[crane]-lx*s+lz*c;}
        private static void Mast(NativeSceneryBuilder b,double mx,double mz,int seed)
        {
            b.Box(mx,.7,mz,6.4,1.4,6.4,Concrete,grime:.2);for(int i=0;i<4;i++)b.Box(mx+(i%2==0?-2:2),1.9,mz+(i<2?-2:2),1.6,1,1.6,ConcreteDark);
            const double h=.9;double y0=1.4;
            for(int i=0;i<4;i++)b.Box(mx+(i%2==0?-h:h),(y0+MastHeight)/2,mz+(i<2?-h:h),.2,MastHeight-y0,.2,Yellow,shade:.95);
            double[] cx={-h,h,h,-h},cz={-h,-h,h,h};int level=0;
            for(double y=y0;y<MastHeight-.5;y+=3,level++)
            {
                double y1=Math.Min(MastHeight,y+3);
                // Each face: a horizontal tie at the top of the 3 m level and one diagonal, alternating direction level to level.
                for(int s=0;s<4;s++)
                {
                    int e=(s+1)%4;Bar(b,mx+cx[s],y1,mz+cz[s],mx+cx[e],y1,mz+cz[e],.09,.09,Yellow,.9);
                    if((level+s)%2==0)Bar(b,mx+cx[s],y,mz+cz[s],mx+cx[e],y1,mz+cz[e],.08,.08,Yellow,.88);else Bar(b,mx+cx[e],y,mz+cz[e],mx+cx[s],y1,mz+cz[s],.08,.08,Yellow,.88);
                }
            }
            b.Box(mx,MastHeight-1.2,mz,2.3,1.6,2.3,Yellow,shade:.85);
        }
        /// <summary>The slewing part of a crane in its jib frame (mast top at the origin, +z along the jib).</summary>
        public static NativeSceneryMesh BuildJib()
        {
            var b=new NativeSceneryBuilder(1);
            b.Box(0,.3,0,2.8,.6,2.8,Yellow,shade:.85);b.Box(1.75,-.4,1.3,1.5,2.2,1.9,Terracotta,shade:.95);b.Box(1.75,-.1,2.26,1.3,1.1,.04,0x3A2E22,glow:SunRim,level:.2);
            for(int i=0;i<4;i++)Bar(b,i%2==0?-.8:.8,.6,i<2?-.8:.8,0,ApexHeight,0,.16,.16,Yellow);
            b.Box(0,ApexHeight+.1,0,.4,.3,.4,Yellow);
            // Jib: two bottom chords, a top chord, zigzag diagonals and cross ties every 2 m.
            b.Box(.75,.68,(1.3+JibLength)/2,.16,.16,JibLength-1.3,Yellow);b.Box(-.75,.68,(1.3+JibLength)/2,.16,.16,JibLength-1.3,Yellow);b.Box(0,2.2,(1.3+JibLength-1.5)/2,.16,.16,JibLength-2.8,Yellow);
            for(double z=1.3;z<JibLength-1;z+=2)
            {
                double z1=Math.Min(JibLength-1.5,z+1),z2=Math.Min(JibLength,z+2);
                for(int s=-1;s<=1;s+=2){Bar(b,s*.75,.68,z,0,2.2,z1,.07,.07,Yellow,.92);Bar(b,0,2.2,z1,s*.75,.68,z2,.07,.07,Yellow,.92);}
                b.Box(0,.68,z,1.5,.07,.07,Yellow,shade:.9);
            }
            b.Box(0,1.4,JibLength,1.7,1.6,.12,Yellow,shade:.9);
            // Counter-jib: deck, rails, machinery, counterweights.
            b.Box(0,.55,-(1.3+CounterJib)/2,2.2,.25,CounterJib-1.3,Yellow,shade:.9);
            for(int s=-1;s<=1;s+=2){b.Box(s*1.1,1.6,-(1.3+CounterJib)/2,.06,.06,CounterJib-1.3,Yellow);for(double z=-1.5;z>-CounterJib;z-=2)b.Box(s*1.1,1.1,z,.06,1.1,.06,Yellow);}
            b.Box(0,1.3,-6,1.8,1.3,2.4,ConcreteDark);b.Box(0,1.3,-6,1.85,.1,2.45,Yellow);
            for(int k=0;k<4;k++)b.Box(0,-.5,-CounterJib+.5+k*.72,2.3,2.6,.68,Concrete,shade:Range(.85,1,500,k));
            // Pendant ties from the apex to the jib and counter-jib.
            Bar(b,0,ApexHeight,0,0,2.2,22,.07,.07,Iron);Bar(b,0,ApexHeight,0,0,2.2,36,.07,.07,Iron);
            for(int s=-1;s<=1;s+=2)Bar(b,s*.2,ApexHeight,0,s*.9,1.0,-CounterJib+.6,.07,.07,Iron);
            // Trolley, hoist ropes and hook block.
            b.Box(0,.5,24,1.3,.45,1.5,Yellow,shade:.8);b.Box(-.25,-1.6,24,.04,4,.04,Iron);b.Box(.25,-1.6,24,.04,4,.04,Iron);
            b.Box(0,-3.9,24,.7,.9,.45,Yellow);b.Box(0,-3.9,24.24,.7,.3,.02,Black);b.Box(0,-4.6,24,.14,.5,.14,Iron);
            return b.Bake(0);
        }
        /// <summary>Every box corner of the slewing part is outside the arena at this time (managed check helper).</summary>
        public static bool JibClear(int crane,double time,NativeSceneryMesh jib)
        {
            for(int i=0;i<jib.Positions.Length;i+=3){JibToWorld(crane,time,jib.Positions[i],jib.Positions[i+1],jib.Positions[i+2],out double x,out _,out double z);if(Math.Abs(x)<InnerX&&Math.Abs(z)<InnerZ)return false;}
            return true;
        }
        private static void CraneSite(NativeSceneryBuilder b,int crane)
        {
            double mx=CraneX[crane],mz=CraneZ[crane],sz=crane==0?1:-1;int seed=600+crane;
            Mast(b,mx,mz,seed);
            // Site cabins stacked two high, material stacks, a fenced edge toward the street.
            for(int i=0;i<4;i++){double cx=mx+(i%2==0?-8.5:-2.2)*sz,cy=1.3+(i<2?0:2.62),cz=mz-sz*10;b.Box(cx,cy,cz,6,2.6,2.4,Terracotta,shade:Range(.8,.95,seed,i),grime:.15);b.Box(cx,cy+.2,cz-sz*1.21,4.6,.9,.04,DarkGlass);b.Box(cx,cy+1.34,cz,6.1,.08,2.5,ConcreteDark);}
            b.Box(mx+6*sz,.5,mz-6*sz,3.2,1,1.4,Timber,shade:.8);b.Box(mx+6*sz,1.2,mz-6*sz,3.2,.4,1.4,Timber,shade:.7);b.Box(mx+8*sz,.4,mz-2*sz,2.4,.8,2.4,Rust);
            b.Box(mx+4*sz,1.5,mz+5*sz,1.4,3,1.4,FreshConcrete);
            // Hoarding along the street front of the site, with a gate.
            double fz=crane==0?40.62:-52.62;
            for(double x=2;x<25.9;x+=2.4){if(x>11&&x<16)continue;double px=(crane==0?x:-x)+sz*1.2;int k=(int)(x*10);b.Box(px,1.2,fz,2.36,2.4,.08,Timber,shade:Range(.7,.95,seed,k));
                if(Hash(seed,k,1)<.5)b.Box(px,1.25,fz-sz*.05,Range(.8,1.6,seed,k,2),Range(.8,1.5,seed,k,3),.02,Pick(new[]{Terracotta,Coral,Mortar,0x5A6A7A},seed,k,4),shade:.85);}
        }

        // ---------------- the Mercer Plaza lot and billboard ----------------
        /// <summary>The billboard's face: x = BillboardX facing the arena (west), z BillboardZ0..Z1, y BillboardY0..Y1.</summary>
        public const double BillboardX=49.55,BillboardZ0=-20,BillboardZ1=4,BillboardY0=21.5,BillboardY1=31.5;
        // 5×7 glyph rows (bit 4 = leftmost column) for the billboard's two lines.
        private static int[] Glyph(char c)
        {
            switch(c)
            {
                case 'M':return new[]{17,27,21,21,17,17,17};case 'E':return new[]{31,16,16,30,16,16,31};case 'R':return new[]{30,17,17,30,20,18,17};
                case 'C':return new[]{14,17,16,16,16,17,14};case 'P':return new[]{30,17,17,30,16,16,16};case 'L':return new[]{16,16,16,16,16,16,31};
                case 'A':return new[]{14,17,17,31,17,17,17};case 'Z':return new[]{31,1,2,4,8,16,31};case 'O':return new[]{14,17,17,17,17,17,14};
                case 'I':return new[]{14,4,4,4,4,4,14};case 'N':return new[]{17,17,25,21,19,17,17};case 'G':return new[]{14,17,16,23,17,17,15};
                case 'S':return new[]{15,16,16,14,1,1,30};case '-':return new[]{0,0,0,31,0,0,0};default:return new[]{0,0,0,0,0,0,0};
            }
        }
        /// <summary>A line of glyphs on the billboard face, read from the arena (left = +z): horizontal pixel runs merged into boxes.</summary>
        private static void Text(NativeSceneryBuilder b,string text,double centreZ,double baseY,double pixel,int hex,double proud,int glow=0,double level=0)
        {
            double width=(text.Length*6-1)*pixel,left=centreZ+width/2;
            for(int ci=0;ci<text.Length;ci++)
            {
                var rows=Glyph(text[ci]);
                for(int r=0;r<7;r++)
                {
                    int c=0;while(c<5){if(((rows[r]>>(4-c))&1)==0){c++;continue;}int start=c;while(c<5&&((rows[r]>>(4-c))&1)!=0)c++;
                        double z0=left-(ci*6+start)*pixel,z1=left-(ci*6+c)*pixel,y=baseY+(6-r)*pixel;b.Box(BillboardX-proud/2,y+pixel/2,(z0+z1)/2,proud,pixel*.96,z0-z1-pixel*.04,hex,glow:glow,level:level);}
                }
            }
        }
        private static void MercerLot(NativeSceneryBuilder b)
        {
            // The lot: hoarding along the street with posters and a danger board, rubble, a long-reach excavator.
            for(double z=-21.5;z<5.5;z+=2.4)
            {
                if(z>-10.2&&z<-6)continue;int seed=(int)(z*10);b.Box(44.6,1.2,z+1.2,.08,2.4,2.36,Timber,shade:Range(.7,.95,800,seed));b.Box(44.75,1.2,z+1.2,.12,2.4,.1,Iron);
                if(Hash(800,seed,1)<.6)b.Box(44.54,1.25,z+1.2,.02,Range(.8,1.5,800,seed,2),Range(.8,1.6,800,seed,3),Pick(new[]{Terracotta,Coral,Mortar,0x5A6A7A},800,seed,4),shade:.85);
            }
            b.Box(44.5,1.5,-1.3,.02,.9,1.6,Danger);b.Box(44.48,1.62,-1.3,.02,.18,1.2,Mortar);b.Box(44.48,1.32,-1.3,.02,.12,.9,Mortar);
            for(int i=0;i<90;i++)
            {
                double a=Hash(801,i)*Math.PI*2,r=Math.Sqrt(Hash(801,i,1)),cx=i<50?54:56,cz=i<50?-15:0,rx=cx+Math.Cos(a)*r*6,rz=cz+Math.Sin(a)*r*6,mound=3.2*(1-r*.8),size=Range(.3,1.6,801,i,2);
                double hh=Hash(801,i,3);int hex=hh<.45?BrickRed:hh<.65?FreshBrick:hh<.85?FreshConcrete:Timber;
                b.Box(rx,Math.Max(size*.25,mound*Hash(801,i,4)),rz,size,size*Range(.35,.8,801,i,5),size*Range(.5,1.2,801,i,6),hex,Range(-30,30,801,i,7),Range(0,180,801,i,8),Range(-25,25,801,i,9),shade:Range(.75,1.05,801,i,10));
            }
            // Excavator: tracks, body, cab, boom, stick and crusher reaching at the north party wall.
            double ex=55,ez=-7;b.Box(ex,.5,ez-1.4,4.6,1,.9,Iron);b.Box(ex,.5,ez+1.4,4.6,1,.9,Iron);b.Box(ex,1.6,ez,3.6,1.3,3.1,Yellow,shade:.9);b.Box(ex+1.2,2.8,ez-.9,1.3,1.4,1.1,Yellow);b.Box(ex+1.2,2.9,ez-.32,1.1,.9,.05,DarkGlass,1.3);b.Box(ex-1.6,1.9,ez,.8,1.1,2.9,ConcreteDark);
            Bar(b,ex+.8,2.4,ez+.5,ex-1,11.5,ez+3.6,.55,.55,Yellow,.92);Bar(b,ex-1,11.5,ez+3.6,ex-2.2,7.5,ez+8.6,.4,.4,Yellow,.92);b.Box(ex-2.3,6.8,ez+9,1,1.4,.9,Iron);
            // Billboard: two steel columns with X bracing, a frame, the panel, its border, the blue band, text, catwalk and lamps.
            for(int i=0;i<2;i++){double cz=i==0?-14:-2;b.Box(51.6,(BillboardY0+.2)/2,cz,.8,BillboardY0+.2,.8,ConcreteDark,shade:.9);b.Box(51.6,.4,cz,2,.8,2,Concrete);}
            for(double y=2;y<BillboardY0-2;y+=6.5){Bar(b,51.6,y,-14,51.6,y+6,-2,.14,.14,Iron);Bar(b,51.6,y+6,-14,51.6,y,-2,.14,.14,Iron);}
            for(double z=BillboardZ0+.5;z<BillboardZ1;z+=3.9)b.Box(50.6,(BillboardY0+BillboardY1)/2,z,.16,BillboardY1-BillboardY0,.16,Iron);
            for(int k=0;k<3;k++){double y=BillboardY0+.4+k*(BillboardY1-BillboardY0-.8)/2;b.Box(50.6,y,(BillboardZ0+BillboardZ1)/2,.16,.16,BillboardZ1-BillboardZ0,Iron);}
            b.Box(BillboardX+.1,(BillboardY0+BillboardY1)/2,(BillboardZ0+BillboardZ1)/2,.2,BillboardY1-BillboardY0,BillboardZ1-BillboardZ0,Terracotta);
            b.Box(BillboardX-.04,BillboardY1-.12,(BillboardZ0+BillboardZ1)/2,.08,.24,BillboardZ1-BillboardZ0+.2,Tar);b.Box(BillboardX-.04,BillboardY0+.12,(BillboardZ0+BillboardZ1)/2,.08,.24,BillboardZ1-BillboardZ0+.2,Tar);
            b.Box(BillboardX-.04,(BillboardY0+BillboardY1)/2,BillboardZ0+.02,.08,BillboardY1-BillboardY0,.24,Tar);b.Box(BillboardX-.04,(BillboardY0+BillboardY1)/2,BillboardZ1-.02,.08,BillboardY1-BillboardY0,.24,Tar);
            b.Box(BillboardX-.01,BillboardY0+1.9,(BillboardZ0+BillboardZ1)/2,.02,3.1,BillboardZ1-BillboardZ0-.3,Neon,glow:Neon,level:.12);
            Text(b,"MERCER PLAZA",(BillboardZ0+BillboardZ1)/2,BillboardY1-3.5,.3,Tar,.08);
            Text(b,"- COMING SOON -",(BillboardZ0+BillboardZ1)/2,BillboardY0+1.12,.2,Terracotta,.07);
            // A torn corner peeling off the top right (seen from the arena: the -z end).
            b.Box(BillboardX-.14,BillboardY1-1.1,BillboardZ0+1.1,.03,1.5,1.7,Terracotta,yaw:28,roll:12,shade:.85);
            b.Box(BillboardX-.5,BillboardY0-.3,(BillboardZ0+BillboardZ1)/2,1,.08,BillboardZ1-BillboardZ0,Iron);b.Box(BillboardX-.98,BillboardY0+.75,(BillboardZ0+BillboardZ1)/2,.05,.05,BillboardZ1-BillboardZ0,Iron);
            for(int i=0;i<5;i++){double z=BillboardZ0+2.4+i*(BillboardZ1-BillboardZ0-4.8)/4;Bar(b,BillboardX+.05,BillboardY1+.1,z,BillboardX-1.5,BillboardY1+.7,z,.07,.07,Iron);b.Box(BillboardX-1.6,BillboardY1+.6,z,.5,.3,.7,Iron);b.Box(BillboardX-1.6,BillboardY1+.44,z,.42,.04,.6,Flood,glow:Flood,level:2.6);}
        }

        // ---------------- the street ring ----------------
        private static void Streets(NativeSceneryBuilder b)
        {
            const double far=330;
            b.Box(0,-.27,(InnerZ+far)/2,far*2,.5,far-InnerZ,Asphalt);b.Box(0,-.27,-(InnerZ+far)/2,far*2,.5,far-InnerZ,Asphalt);
            b.Box((InnerX+far)/2,-.27,0,far-InnerX,.5,InnerZ*2,Asphalt);b.Box(-(InnerX+far)/2,-.27,0,far-InnerX,.5,InnerZ*2,Asphalt);
            // Pavements with kerbs in front of the second ring; cobbled lots and the street beyond the viaduct.
            b.Box(0,.06,39.25,89,.16,2.5,Concrete);b.Box(0,.06,38.1,89,.17,.2,ConcreteDark);
            b.Box(43.25,.06,2.75,2.5,.16,69.5,Concrete);b.Box(42.1,.06,2.75,.2,.17,69.5,ConcreteDark);
            b.Box(-43.25,.06,2.75,2.5,.16,69.5,Concrete);b.Box(-42.1,.06,2.75,.2,.17,69.5,ConcreteDark);
            b.Box(0,.06,-51.25,180,.16,2.5,Concrete);b.Box(0,.06,-50.1,180,.17,.2,ConcreteDark);
            b.Box(0,-.01,-39,ViaductX1-ViaductX0,.04,9,Cobble);
            // Faded lane paint: dashed centre lines.
            for(double x=-150;x<150;x+=7){b.Box(x,-.005,34,3,.03,.15,LanePaint,shade:.75);b.Box(x,-.005,-47,3,.03,.15,LanePaint,shade:.75);}
            for(double z=-28;z<36;z+=7){b.Box(38,-.005,z,.15,.03,3,LanePaint,shade:.75);b.Box(-38,-.005,z,.15,.03,3,LanePaint,shade:.75);}
            // Sodium street lamps on the far pavements (just lit at golden hour).
            void Lamp(double x,double z,double ax,double az)
            {b.Box(x,3.3,z,.18,6.6,.18,Iron);Bar(b,x,6.5,z,x+ax*1.4,6.7,z+az*1.4,.1,.1,Iron);b.Box(x+ax*1.5,6.62,z+az*1.5,.5,.22,.36,Iron);b.Box(x+ax*1.5,6.49,z+az*1.5,.36,.04,.26,Sodium,glow:Sodium,level:2.6);}
            for(double x=-40;x<=40;x+=20)if(Math.Abs(x-14)>8)Lamp(x,39.6,0,-1);
            for(double z=-20;z<=36;z+=18){Lamp(43.6,z,-1,0);Lamp(-43.6,z,1,0);}
            for(double x=-60;x<=60;x+=20)Lamp(x,-51.6,0,1);
            // Abandoned cars, a skip, jersey barriers.
            void Car(double x,double z,double yaw,int seed)
            {
                int body=Pick(new[]{Rust,0x5A6A7A,0x6E5A48,Green,Terracotta},seed);double c=Math.Cos(yaw*Math.PI/180),s=Math.Sin(yaw*Math.PI/180);
                b.Box(x,.75,z,1.8,.8,4.3,body,yaw:yaw,shade:.85,grime:.25);b.Box(x-s*.2,1.4,z-c*.2,1.6,.55,2.2,body,yaw:yaw,shade:.8);b.Box(x-s*.2,1.42,z-c*.2,1.62,.42,2,DarkGlass,yaw:yaw,shade:1.3);
                for(int w=0;w<4;w++){double wx=(w%2==0?-.9:.9),wz=(w<2?-1.35:1.35);b.Box(x+wx*c+wz*s,.35,z-wx*s+wz*c,.25,.7,.7,Black,yaw:yaw);}
            }
            Car(38.5,-12,4,1);Car(-39.2,8,178,2);Car(14,35.6,93,3);Car(-24,36.2,88,4);Car(-30,-46.5,271,5);Car(22,-47.4,268,6);
            b.Box(-38,.7,26,1.8,1.4,3.8,Rust,shade:.8,grime:.3);b.Box(-38,1.42,26,1.9,.06,3.9,Iron);
            for(int i=0;i<4;i++){b.Box(-16+i*2.1,.42,31.2,2,.84,.6,FreshConcrete,shade:.9);b.Box(36.1,.42,-24+i*2.1,.6,.84,2,FreshConcrete,shade:.9);}
        }

        // ---------------- the whole backdrop ----------------
        /// <summary>Builds the five static sector meshes (Near, North, East, South, West).</summary>
        public static NativeSceneryMesh[] Build()
        {
            var b=new NativeSceneryBuilder(Sectors){ExcludeX=InnerX,ExcludeZ=InnerZ};
            b.Current=Near;Streets(b);Viaduct(b);MercerLot(b);
            // East: the E row around the Mercer lot (z -22..6), whose neighbours show party-wall ghosts, and the far rows.
            b.Current=East;
            Row(b,East,-37.5,-22,44.5,11,14,3,5,11,true,99,99,-1);
            Building(b,44.5,57.5,13,22,1,5,1302,true,false);Building(b,44.5,57,6,13,1,4,1301,true,false);Ruin(b,44.5,58,-32,-22,1,5,1303,true,false);
            PartyGhost(b,Frame.Make(44.5,6,0,-1),12.5,4.4+4*3.3,1310);PartyGhost(b,Frame.Make(58,-22,0,1),13.5,4.4+5*3.3,1311);
            Row(b,East,-55,34,62,14,18,6,10,15,false,99,99,-1);
            for(int i=0;i<6;i++){double z0=-32+i*15;Plain(b,84,84+Range(10,18,1500,i),z0,z0+Range(9,14,1501,i),1,Range(16,34,1502,i),1503+i);}
            b.Current=West;
            Row(b,West,-32,37.5,44.5,11,14,3,5,21,true,99,99,4);
            Row(b,West,-34,55,62,14,18,6,10,25,false,99,99,-1);
            for(int i=0;i<6;i++){double z0=-32+i*15;Plain(b,-84-Range(10,18,2500,i),-84,z0,z0+Range(9,14,2501,i),3,Range(16,34,2502,i),2503+i);}
            b.Current=North;
            Row(b,North,-58,58,40.5,11,15,3,5,31,true,2,26,2);
            Row(b,North,-150,150,59,14,18,6,11,35,false,2,26,9);
            for(int i=0;i<18;i++){double x0=-170+i*19;Plain(b,x0,x0+Range(12,17,3500,i),81,81+Range(10,16,3501,i),0,Range(16,34,3502,i),3503+i);}
            CraneSite(b,0);
            b.Current=South;
            Row(b,South,-90,90,52.5,11,14,3,6,41,true,2,26,5);
            Row(b,South,-150,150,70,14,18,6,11,45,false,2,26,11);
            for(int i=0;i<18;i++){double x0=-170+i*19;Plain(b,x0,x0+Range(12,17,4500,i),-91-Range(10,16,4501,i),-91,2,Range(16,34,4502,i),4503+i);}
            CraneSite(b,1);
            Skyline(b);
            var meshes=new NativeSceneryMesh[Sectors];for(int i=0;i<Sectors;i++)meshes[i]=b.Bake(i);return meshes;
        }

        // ---------------- birds and smoke ----------------
        public const int Flocks=3,FlockBirds=16;
        private static readonly double[] FlockX={72,-96,38},FlockY={52,46,64},FlockZ={92,-38,-112},FlockR={30,26,32},FlockW={.32,-.27,.3};
        /// <summary>
        /// A bird of a flock at `time`: the flock circles its own centre outside the walls on a lazy ellipse, each bird holds a
        /// loose hashed slot with its own wobble; `yaw` is its heading (degrees), `flap` -1..1 its wing beat (gliding now and then).
        /// </summary>
        public static void Bird(int flock,int bird,double time,out double x,out double y,out double z,out double yaw,out double flap)
        {
            double w=FlockW[flock],t=time*w+flock*2.1,r=FlockR[flock];
            double ox=Range(-7,7,1700+flock,bird),oy=Range(-2.5,2.5,1700+flock,bird,1),oz=Range(-7,7,1700+flock,bird,2),ph=Hash(1700+flock,bird,3)*6.283;
            x=FlockX[flock]+r*Math.Cos(t)+ox+Math.Sin(time*1.3+ph)*.8;z=FlockZ[flock]+r*.75*Math.Sin(t)+oz+Math.Cos(time*1.1+ph)*.8;y=FlockY[flock]+3*Math.Sin(time*w*.37+flock)+oy+Math.Sin(time*.9+ph)*.5;
            double dx=-r*Math.Sin(t)*w,dz=r*.75*Math.Cos(t)*w;yaw=Math.Atan2(dx,dz)*180/Math.PI;
            double glide=Smooth(Math.Sin(time*.45+ph*2)*2-.6);flap=Math.Sin(time*21+ph*9)*(1-.85*glide);
        }
        public const int SmokeColumns=2,SmokePuffs=32;public const double SmokeLife=26;
        private static readonly double[] SmokeX={-66,72},SmokeZ={68,-60};
        /// <summary>
        /// Puff `puff` of distant smoke column `column` at `time`: puffs rise from a fire in the far row (some 90 m out, inside the
        /// fog's reach), slow as they climb, lean east with the wind (stronger aloft), grow and thin out over <see cref="SmokeLife"/> s;
        /// `alpha` 0..1.
        /// </summary>
        public static void SmokePuff(int column,int puff,double time,out double x,out double y,out double z,out double size,out double alpha)
        {
            double a=Frac(time/SmokeLife+puff/(double)SmokePuffs+column*.13)*SmokeLife,ph=Hash(1800+column,puff)*6.283;
            y=8+4.4*a-.045*a*a;x=SmokeX[column]+.9*a+.018*a*a+Math.Sin(a*.4+ph)*1.4;z=SmokeZ[column]+Math.Cos(a*.33+ph)*1.6;
            size=4.5+.8*a;alpha=Smooth(a/2.5)*Math.Pow(1-a/SmokeLife,1.1);
        }
    }
}
