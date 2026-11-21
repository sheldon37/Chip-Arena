using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>C12 part 2 transform helper: three's Quaternion.setFromUnitVectors (both vectors normalised by the caller).</summary>
    public static class NativeDevFxMath
    {
        public static void UnitRotation(double fx,double fy,double fz,double tx,double ty,double tz,out double qx,out double qy,out double qz,out double qw)
        {
            double r=fx*tx+fy*ty+fz*tz+1;
            if(r<2.220446049250313e-16){r=0;if(Math.Abs(fx)>Math.Abs(fz)){qx=-fy;qy=fx;qz=0;qw=r;}else{qx=0;qy=-fz;qz=fy;qw=r;}}
            else{qx=fy*tz-fz*ty;qy=fz*tx-fx*tz;qz=fx*ty-fy*tx;qw=r;}
            double l=Math.Sqrt(qx*qx+qy*qy+qz*qz+qw*qw);if(l==0){qx=qy=qz=0;qw=1;return;}qx/=l;qy/=l;qz/=l;qw/=l;
        }
        /// <summary>Vector3.normalize: divide by the length, or by 1 when it is zero.</summary>
        public static void Normalize(ref double x,ref double y,ref double z){double l=Math.Sqrt(x*x+y*y+z*z);if(l==0)l=1;x/=l;y/=l;z/=l;}
    }

    /// <summary>
    /// client/emperor-effects.ts: the two-tone electric ribbons. Each bolt is three 120°-apart branches of five segments that
    /// re-roll 24 times a second; every segment is drawn as a glow, dark and core box. At most 512 segments a frame; a bolt
    /// that would pass the budget stops there. Positions are in the source frame (the runtime mirrors x).
    /// </summary>
    public sealed class NativeEmperorBolts
    {
        public const int Budget=512,ImpactLimit=16,Stride=7;
        public const int GlowColor=0xff0730,DarkColor=0xff1637,CoreColor=0xfff5ee;public const double GlowOpacity=.14,DarkOpacity=.95,CoreOpacity=1;
        public const double FistSeconds=.35,JuggleSeconds=.28,ImpactSeconds=.3,FistSize=.5,JuggleSize=.7,TrailSize=.65,TrailHeight=.13,DefaultRadius=.75;
        /// <summary>Per segment: start xyz, end xyz, bolt size.</summary>
        public readonly double[] Segments=new double[Budget*Stride];public int Count;
        private readonly double[] impacts=new double[ImpactLimit*6];private int impactCount;
        public int Impacts=>impactCount;
        public void Begin(){Count=0;}
        public void Bolt(double cx,double cy,double cz,double time,double seed,double size)
        {
            double phase=Math.Floor(time*24);
            for(int b=0;b<3;b++)
            {
                double px=cx,py=cy,pz=cz;
                for(int j=1;j<=5;j++)
                {
                    double a=seed*2.4+b*2.094+j*.35,noise=Math.Sin((phase+seed*3+j*7+b*11)*2.17),u=j/5.0;
                    double nx=cx+Math.Cos(a)*u*size,ny=cy+(u-.25)*size+noise*.16*size,nz=cz+Math.Sin(a)*u*size;
                    if(Count>=Budget)return;int o=Count++*Stride;
                    Segments[o]=px;Segments[o+1]=py;Segments[o+2]=pz;Segments[o+3]=nx;Segments[o+4]=ny;Segments[o+5]=nz;Segments[o+6]=size;px=nx;py=ny;pz=nz;
                }
            }
        }
        /// <summary>event(): an emperor-hit or emperor-bound impact (size = its radius, .75 when it has none); the oldest of 16 goes.</summary>
        public void Impact(double x,double y,double z,double at,double seed,double size)
        {
            if(impactCount==ImpactLimit){Array.Copy(impacts,6,impacts,0,(ImpactLimit-1)*6);impactCount--;}
            int o=impactCount++*6;impacts[o]=x;impacts[o+1]=y;impacts[o+2]=z;impacts[o+3]=at;impacts[o+4]=seed;impacts[o+5]=size;
        }
        /// <summary>update(): the impact bolts shrink to a quarter by .3 s, then drop.</summary>
        public void DrawImpacts(double time)
        {
            int keep=0;
            for(int i=0;i<impactCount;i++)
            {
                int o=i*6;double age=time-impacts[o+3];if(age>=0&&age<ImpactSeconds)Bolt(impacts[o],impacts[o+1],impacts[o+2],time,impacts[o+4],impacts[o+5]*(1-age/.4));
                if(time-impacts[o+3]<ImpactSeconds){if(keep!=i)Array.Copy(impacts,o,impacts,keep*6,6);keep++;}
            }
            impactCount=keep;
        }
        public void Clear(){Count=0;impactCount=0;}
        /// <summary>Segment i's box in layer 0 glow, 1 dark, 2 core: centred on the segment, local +y along it.</summary>
        public void Box(int i,int layer,out double cx,out double cy,out double cz,out double qx,out double qy,out double qz,out double qw,out double sx,out double sy,out double sz)
        {
            int o=i*Stride;double dx=Segments[o+3]-Segments[o],dy=Segments[o+4]-Segments[o+1],dz=Segments[o+5]-Segments[o+2],len=Math.Sqrt(dx*dx+dy*dy+dz*dz),size=Segments[o+6];
            cx=(Segments[o]+Segments[o+3])*.5;cy=(Segments[o+1]+Segments[o+4])*.5;cz=(Segments[o+2]+Segments[o+5])*.5;
            NativeDevFxMath.Normalize(ref dx,ref dy,ref dz);NativeDevFxMath.UnitRotation(0,1,0,dx,dy,dz,out qx,out qy,out qz,out qw);
            double w=layer==0?.16:layer==1?.041:.013;sx=sz=w*size;sy=len+(layer==0?.08:.025);
        }
    }

    /// <summary>
    /// client/matrix-effects.ts MatrixEffects: the binary glyph pool shared by Leet projectile trails and the teleport/death
    /// static columns (640 glyphs, .85 s each, sinking .55 m/s, billboarded, green additive ramp), and the nuke blast, nuke
    /// field and tree numbers. Positions are in the source frame.
    /// </summary>
    public sealed class NativeGlyphPool
    {
        public const int Capacity=640;public const double Life=.85,Sink=.55;
        public readonly double[] X=new double[Capacity],Y=new double[Capacity],Z=new double[Capacity],At=new double[Capacity];public readonly int[] Bit=new int[Capacity];public int Count;
        private readonly Dictionary<int,double> next=new Dictionary<int,double>();private readonly Dictionary<int,int> seen=new Dictionary<int,int>();private readonly List<int> stale=new List<int>();private int frame;
        private void Push(double x,double y,double z,double at,int bit){if(Count>=Capacity)return;X[Count]=x;Y[Count]=y;Z[Count]=z;At[Count]=at;Bit[Count]=bit;Count++;}
        /// <summary>staticColumn: a 2.7 m spiral of 0s and 1s (36 glyphs, 13 on Reduced), pre-aged in four steps.</summary>
        public void StaticColumn(double x,double y,double z,double now,double quality)
        {
            int n=(int)Math.Floor(36*Math.Max(.35,quality)+.5);
            for(int i=0;i<n&&Count<Capacity;i++){double a=i*2.39996,r=.18+(i%5)*.07;Push(x+Math.Cos(a)*r,y+.1+(double)i/n*2.6,z+Math.Sin(a)*r,now-(i%4)*.08,i%2);}
        }
        public void BeginTrails(){frame++;}
        /// <summary>Three glyphs behind a Leet projectile every .045 s (.11 s on Reduced), offset along world x.</summary>
        public void Trail(int id,double x,double y,double z,double now,double quality)
        {
            seen[id]=frame;next.TryGetValue(id,out double due);if(now<due)return;next[id]=now+(quality<.5?.11:.045);
            long step=(long)Math.Floor(now*30);
            for(int j=0;j<3&&Count<Capacity;j++)Push(x+(j-1)*.1,y+Math.Sin(j*2+now*7)*.15,z,now,(int)(((j+step)%2+2)%2));
        }
        /// <summary>Forget the emission clock of projectiles that are gone.</summary>
        public void EndTrails()
        {
            stale.Clear();foreach(var pair in seen)if(pair.Value!=frame)stale.Add(pair.Key);
            foreach(int id in stale){seen.Remove(id);next.Remove(id);}
        }
        /// <summary>Drop glyphs .85 s old, keeping the pool's order.</summary>
        public void Expire(double now)
        {
            int keep=0;
            for(int i=0;i<Count;i++){if(now-At[i]>=Life)continue;if(keep!=i){X[keep]=X[i];Y[keep]=Y[i];Z[keep]=Z[i];At[keep]=At[i];Bit[keep]=Bit[i];}keep++;}
            Count=keep;
        }
        /// <summary>Glyph i this frame: position, uniform scale .20 to .16, and its linear colour (.13k, k, .32k).</summary>
        public void Pose(int i,double now,out double x,out double y,out double z,out double scale,out double r,out double g,out double b)
        {double age=now-At[i],k=1-age/Life;x=X[i];y=Y[i]-age*Sink;z=Z[i];scale=.16+k*.04;r=.13*k;g=k;b=.32*k;}
        public void Clear(){Count=0;next.Clear();seen.Clear();}
    }

    public static class NativeLeetFx
    {
        public const double BlastLife=1.7,TreeGrow=.8,BlastOpacity=.65;public const int FieldGreen=0x62ff98,BlastGreen=0x55ff88,Bark=0x111b18,FieldGlyphs=28,Branches=12;
        /// <summary>matrixGlyphs: '0' (a hollow .7 x 1.1 rectangle of four boxes) and '1', both .045 deep.</summary>
        public static readonly NativeShotMesh[] Glyphs={NativeShotGeometry.Vox(new[]{-.3,0,0,.1,1,.045},new[]{.3,0,0,.1,1,.045},new[]{0,.5,0,.6,.1,.045},new[]{0,-.5,0,.6,.1,.045}),NativeShotGeometry.Box(.12,1,.045)};
        public static readonly NativeShotMesh BlastSphere=NativeShotGeometry.Sphere(1,16,12),BlastRing=NativeShotGeometry.Torus(1,.035,5,64);
        /// <summary>The mushroom: group scale .4 + 2·age, the ground ring's own scale 2 + 4·age, opacity .65 fading over 1.7 s.</summary>
        public static void Blast(double age,out double scale,out double ring,out double opacity){scale=.4+age*2;ring=2+age*4;opacity=BlastOpacity*(1-age/BlastLife);}
        /// <summary>Nuke field glyph i (child i+1): rising and looping inside the blast radius, turning with the clock.</summary>
        public static void FieldGlyph(int i,double age,double now,out double x,out double y,out double z,out double turn)
        {double a=i*2.4,r=2+i%8;int c=i+1;x=Math.Cos(a)*r;z=Math.Sin(a)*r;y=.15+(age*3+c*.13)%1.5;turn=now+c;}
        /// <summary>Tree part k: 0 the trunk, 1..12 the branches (position, Euler XYZ, box size).</summary>
        public static void TreePart(int k,out double px,out double py,out double pz,out double rx,out double ry,out double rz,out double sx,out double sy,out double sz)
        {
            if(k==0){px=0;py=1.7;pz=0;rx=ry=rz=0;sx=.36;sy=3.4;sz=.38;return;}
            double a=(k-1)*2.4;px=Math.Cos(a)*.55;py=1.8+(k-1)*.13;pz=Math.Sin(a)*.55;rx=Math.Sin(a)*.8;ry=0;rz=Math.Cos(a)*.8;sx=.12;sy=1.6;sz=.12;
        }
        public static double TreeHeight(double age)=>Math.Min(1,age/TreeGrow);
    }

    /// <summary>client/hadouken-visual.ts: the domed leading edge, hot core, haze shell and nine tapered energy tongues.</summary>
    public static class NativeHadoukenFx
    {
        public const int Limit=36,ReducedLimit=16,Tongues=9,Points=13;public const double HotOpacity=.95,EnergyOpacity=.82,HazeOpacity=.23;
        public static int Hot(bool red)=>red?0xfff4b8:0xeaffff;public static int Energy(bool red)=>red?0xff5c18:0x32b8ff;public static int Haze(bool red)=>red?0xffab24:0x61e6ff;
        public static readonly NativeShotMesh Sphere=NativeShotGeometry.Sphere(1,16,10),Rim=NativeShotGeometry.Torus(1,.07,5,32);
        public static readonly int[] TongueIndices=BuildTongue();
        private static int[] BuildTongue(){var o=new int[12*6];for(int n=0;n<12;n++){int k=n*2;o[n*6]=k;o[n*6+1]=k+1;o[n*6+2]=k+2;o[n*6+3]=k+1;o[n*6+4]=k+3;o[n*6+5]=k+2;}return o;}
        public static void Frame(double radius,double time,out double r,out double pulse){r=radius*1.65;pulse=1+Math.Sin(time*27)*.045;}
        /// <summary>Core, shell and rim: local z and scale (rim uniform, rolled time·2 about z).</summary>
        public static void Core(double r,double pulse,out double z,out double sx,out double sy,out double sz){z=r*.25;sx=r*.68*pulse;sy=r*.7*pulse;sz=r*.96;}
        public static void Shell(double r,out double z,out double sx,out double sy,out double sz){z=-r*.25;sx=sy=r*1.12;sz=r*1.75;}
        public static void RimPart(double r,double pulse,double time,out double z,out double scale,out double roll){z=r*.35;scale=r*.9*pulse;roll=time*2;}
        public static bool TongueVisible(int i,double quality)=>quality>=.5||i<4;
        public static bool TongueHot(int i)=>i%3==0;
        /// <summary>Tongue i's 13 x 2 strip in the dome's frame (+z = travel): o[(n·2+side)·3 ..].</summary>
        public static void Tongue(int i,double r,double time,double[] o)
        {
            double angle=i*Math.PI*2/9+time*(i%2==1?.8:-.65);
            for(int n=0;n<=12;n++)
            {
                double u=n/12.0,rad=r*(.72+.3*Math.Sin(u*Math.PI))*(1-u*.72),curl=angle+Math.Sin(u*7-time*20+i)*.13+u*.5;
                double length=r*(3.1+(i%3)*.5),width=r*(.2*(1-u)+.01)*Math.Sin(Math.Min(1,u*6+.15)*Math.PI/2),z=r*.36-u*length;
                for(int side=0;side<2;side++){double sign=side==1?1:-1;int k=(n*2+side)*3;o[k]=Math.Cos(curl)*rad-Math.Sin(curl)*width*sign;o[k+1]=Math.Sin(curl)*rad+Math.Cos(curl)*width*sign;o[k+2]=z;}
            }
        }
    }

    /// <summary>
    /// client/demon-afterimages.ts: 32 ghost slots (12 on Reduced) used round-robin, a sample every .045 s (.09 s on Reduced)
    /// during the first .30 s of a Demon Step, each fading over .28 s. Slot colours alternate violet and indigo.
    /// </summary>
    public sealed class NativeDemonGhosts
    {
        public const int Slots=32,ReducedSlots=12,Violet=0x9c7cff,Indigo=0x594de7;public const double Seconds=.30,Life=.28,Sample=.045,Opacity=.45;
        public readonly double[] At=new double[Slots];public readonly int[] Owner=new int[Slots];private int index;
        private readonly Dictionary<int,double> sampled=new Dictionary<int,double>();
        public NativeDemonGhosts(){for(int i=0;i<Slots;i++)At[i]=-10;}
        public static int Color(int slot)=>slot%2==1?Indigo:Violet;
        /// <summary>A dashing step at stepAt takes a sample now (the caller checks alive, connected, dashing and a rig).</summary>
        public bool Due(int owner,double now,double stepAt,double quality)
        {if(now<stepAt||now-stepAt>Seconds)return false;return now-(sampled.TryGetValue(owner,out double last)?last:-10)>=Sample*(quality<.5?2:1);}
        public int Take(int owner,double now,double quality){int slot=index++%(quality<.5?ReducedSlots:Slots);At[slot]=now;Owner[slot]=owner;sampled[owner]=now;return slot;}
        /// <summary>Shown while younger than .28 s (and its owner alive).</summary>
        public bool Live(int slot,double now){double age=now-At[slot];return age>=0&&age<Life;}
        public static double Fade(double age)=>Opacity*Math.Pow(Math.Max(0,1-age/Life),1.4);
        /// <summary>clear(): every slot hidden and the sample clocks forgotten; the ring position is kept.</summary>
        public void Clear(){for(int i=0;i<Slots;i++)At[i]=-10;sampled.Clear();}
    }
}
