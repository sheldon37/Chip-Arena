using System;

namespace ChipArena.UnityConversion
{
    /// <summary>The six flame tones of client/siren/flames.ts (start colour, then the fade colour each cube ages toward).</summary>
    public enum NativeFlameTone{Black,Soot,Smoke,Ember,White,Warn}

    /// <summary>
    /// Pure port of client/siren/flames.ts FlameField at 05950ea: a fixed pool of voxel flame tongues in the source's world
    /// frame (+x is the source's x; the Unity side mirrors x when it draws). A ring cursor overwrites the oldest slot and
    /// nothing is rejected. State is stored as float32 like the source's Float32Arrays, so a run fed the same random
    /// stream reproduces the source frame for frame (Tests/SirenFxChecks.cs). Colours are three.js linear values
    /// (Color.setHex converts sRGB, lerps run in linear). The material is a ShaderMaterial without output encoding, so the
    /// screen shows these linear values as display values (NativeSirenFx.shader "raw" colours). Allocation free.
    /// </summary>
    public sealed class NativeFlameField
    {
        public const int Ink=0x0a080e,SmokeTint=0x17121f,EmberTint=0xe6defa,WarnTint=0xff2f44;
        public readonly int Capacity;private int cursor;
        /// <summary>Cubes still burning after the last <see cref="Update"/>.</summary>
        public int Live;
        public readonly float[] Pos,Vel,Rot,Spin,Age,Life,Size,Buoy,Drag,C0,C1;
        /// <summary>The 0..1 random stream (the source's Math.random); fixtures inject a seeded one.</summary>
        public Func<double> Random;
        public NativeFlameField(int capacity,Func<double> random)
        {
            Capacity=Math.Max(1,capacity);Random=random??throw new ArgumentNullException(nameof(random));
            Pos=new float[Capacity*3];Vel=new float[Capacity*3];Rot=new float[Capacity*3];Spin=new float[Capacity*3];C0=new float[Capacity*3];C1=new float[Capacity*3];
            Age=new float[Capacity];Life=new float[Capacity];Size=new float[Capacity];Buoy=new float[Capacity];Drag=new float[Capacity];
            for(int i=0;i<Capacity;i++)Age[i]=Life[i]=1;
        }
        public int Cursor=>cursor;
        private static double Lin(int hex,int shift){double c=((hex>>shift)&255)/255.0;return c<0.04045?c*0.0773993808:Math.Pow(c*0.9478672986+0.0521327014,2.4);}
        private static void Lerp(int a,int b,double t,out double r,out double g,out double bl)
        {r=Lin(a,16);g=Lin(a,8);bl=Lin(a,0);r+=(Lin(b,16)-r)*t;g+=(Lin(b,8)-g)*t;bl+=(Lin(b,0)-bl)*t;}
        /// <summary>flames.ts TONES: the start colour (consumes the random draws the source does).</summary>
        public static void Start(NativeFlameTone tone,Func<double> random,out double r,out double g,out double b)
        {
            switch(tone)
            {
                case NativeFlameTone.Black:Lerp(Ink,SmokeTint,random()*.7,out r,out g,out b);return;
                case NativeFlameTone.Soot:Lerp(Ink,SmokeTint,random()*.5,out r,out g,out b);return;
                case NativeFlameTone.Smoke:Lerp(0x241c31,0x3a3150,random(),out r,out g,out b);return;
                case NativeFlameTone.Ember:{double k=.75+random()*.4;r=Lin(EmberTint,16)*k;g=Lin(EmberTint,8)*k;b=Lin(EmberTint,0)*k;return;}
                case NativeFlameTone.White:r=g=b=1;return;
                default:Lerp(WarnTint,0xffd0d6,random()*.3,out r,out g,out b);return;
            }
        }
        /// <summary>flames.ts FADES: the colour a cube reaches at the end of its life.</summary>
        public static int Fade(NativeFlameTone tone)=>tone==NativeFlameTone.Black?0x5b5078:tone==NativeFlameTone.Soot?0x2b2436:tone==NativeFlameTone.Smoke?0x7d7299:tone==NativeFlameTone.Ember?0x2a2233:tone==NativeFlameTone.White?0x8f86b0:0x4a0a12;
        /// <summary>flames.ts emit: one cube; size is the peak edge (m), buoyancy (m/s²) lifts it, drag (1/s) slows it.</summary>
        public void Emit(double x,double y,double z,double vx,double vy,double vz,double life,double size,NativeFlameTone tone=NativeFlameTone.Black,double buoyancy=2.2,double drag=1.6)
        {
            int i=cursor;cursor=(cursor+1)%Capacity;int k=i*3;
            Pos[k]=(float)x;Pos[k+1]=(float)y;Pos[k+2]=(float)z;Vel[k]=(float)vx;Vel[k+1]=(float)vy;Vel[k+2]=(float)vz;
            // Mostly upright tongues that twist about the vertical, not tumbling debris.
            Rot[k]=(float)((Random()-.5)*.5);Rot[k+1]=(float)(Random()*6.3);Rot[k+2]=(float)((Random()-.5)*.5);
            Spin[k]=(float)((Random()-.5)*1.2);Spin[k+1]=(float)((Random()-.5)*4);Spin[k+2]=(float)((Random()-.5)*1.2);
            Age[i]=0;Life[i]=(float)Math.Max(.05,life);Size[i]=(float)size;Buoy[i]=(float)buoyancy;Drag[i]=(float)drag;
            Start(tone,Random,out double r,out double g,out double b);C0[k]=(float)r;C0[k+1]=(float)g;C0[k+2]=(float)b;
            int fade=Fade(tone);C1[k]=(float)Lin(fade,16);C1[k+1]=(float)Lin(fade,8);C1[k+2]=(float)Lin(fade,0);
        }
        /// <summary>flames.ts burst: a spherical puff of n cubes around a point.</summary>
        public void Burst(double x,double y,double z,int n,double speed,double life,double size,NativeFlameTone tone=NativeFlameTone.Black,double up=1.2,double buoyancy=2.2)
        {
            for(int j=0;j<n;j++)
            {
                double a=Random()*Math.PI*2,u=Random()*2-1,r=Math.Sqrt(1-u*u),v=speed*(.35+Random()*.65);
                Emit(x,y,z,Math.Cos(a)*r*v,u*v*.6+up*(.4+Random()*.6),Math.Sin(a)*r*v,life*(.6+Random()*.6),size*(.6+Random()*.6),tone,buoyancy);
            }
        }
        /// <summary>flames.ts update: buoyancy, drag, spin and ageing (dt is the caller's clamped step).</summary>
        public void Update(double dt)
        {
            int live=0;
            for(int i=0;i<Capacity;i++)
            {
                if(Age[i]>=Life[i])continue;int k=i*3;Age[i]=(float)(Age[i]+dt);double t=(double)Age[i]/Life[i];
                if(t>=1)continue;live++;
                double d=Math.Exp(-Drag[i]*dt);Vel[k]=(float)(Vel[k]*d);Vel[k+1]=(float)(Vel[k+1]*d+Buoy[i]*dt);Vel[k+2]=(float)(Vel[k+2]*d);
                Pos[k]=(float)(Pos[k]+Vel[k]*dt);Pos[k+1]=(float)(Pos[k+1]+Vel[k+1]*dt);Pos[k+2]=(float)(Pos[k+2]+Vel[k+2]*dt);
                Rot[k]=(float)(Rot[k]+Spin[k]*dt);Rot[k+1]=(float)(Rot[k+1]+Spin[k+1]*dt);Rot[k+2]=(float)(Rot[k+2]+Spin[k+2]*dt);
            }
            Live=live;
        }
        /// <summary>
        /// The drawn cube i (source frame): centre, rotation (three.js Euler XYZ as a quaternion), the tall tongue scale
        /// (edge·.8, edge·1.7, edge·.8) and the linear colour lerp(c0, c1, t²). False for a hidden slot.
        /// </summary>
        public bool Pose(int i,out double x,out double y,out double z,out double qx,out double qy,out double qz,out double qw,out double sx,out double sy,out double sz,out double r,out double g,out double b)
        {
            int k=i*3;x=Pos[k];y=Pos[k+1];z=Pos[k+2];double t=(double)Age[i]/Life[i];
            if(Age[i]>=Life[i]||t>=1){qx=qy=qz=0;qw=1;sx=sy=sz=r=g=b=0;return false;}
            // Swell over the first fifth, then shrink to nothing: a flame tongue, not a falling cube.
            double grow=t<.2?t/.2:1-Math.Pow((t-.2)/.8,1.35),edge=Size[i]*Math.Max(0,grow);
            NativeFxMath.EulerQuaternion(Rot[k],Rot[k+1],Rot[k+2],out qx,out qy,out qz,out qw);sx=sz=edge*.8;sy=edge*1.7;
            double f=t*t;r=C0[k]+(C1[k]-C0[k])*f;g=C0[k+1]+(C1[k+1]-C0[k+1])*f;b=C0[k+2]+(C1[k+2]-C0[k+2])*f;return edge>0;
        }
        /// <summary>flames.ts clear: every slot hidden (the cursor keeps its place, as in the source).</summary>
        public void Clear(){for(int i=0;i<Capacity;i++)Age[i]=Life[i]=1;Live=0;}
    }

    /// <summary>The three.js transform helpers the Siren effects need (row-major 4x4 doubles, source frame).</summary>
    public static class NativeFxMath
    {
        /// <summary>Quaternion.setFromEuler for order XYZ.</summary>
        public static void EulerQuaternion(double x,double y,double z,out double qx,out double qy,out double qz,out double qw)
        {
            double c1=Math.Cos(x/2),c2=Math.Cos(y/2),c3=Math.Cos(z/2),s1=Math.Sin(x/2),s2=Math.Sin(y/2),s3=Math.Sin(z/2);
            qx=s1*c2*c3+c1*s2*s3;qy=c1*s2*c3-s1*c2*s3;qz=c1*c2*s3+s1*s2*c3;qw=c1*c2*c3-s1*s2*s3;
        }
        /// <summary>m = T(position)·R(Euler XYZ)·S(scale) (Matrix4.compose with a makeRotationFromEuler XYZ rotation).</summary>
        public static void Compose(double[] m,double px,double py,double pz,double rx,double ry,double rz,double sx,double sy,double sz)
        {
            double a=Math.Cos(rx),b=Math.Sin(rx),c=Math.Cos(ry),d=Math.Sin(ry),e=Math.Cos(rz),f=Math.Sin(rz);
            double ae=a*e,af=a*f,be=b*e,bf=b*f;
            m[0]=c*e*sx;m[1]=-c*f*sy;m[2]=d*sz;m[3]=px;
            m[4]=(af+be*d)*sx;m[5]=(ae-bf*d)*sy;m[6]=-b*c*sz;m[7]=py;
            m[8]=(bf-ae*d)*sx;m[9]=(be+af*d)*sy;m[10]=a*c*sz;m[11]=pz;
            m[12]=0;m[13]=0;m[14]=0;m[15]=1;
        }
        /// <summary>o = a·b (o may not alias a or b).</summary>
        public static void Multiply(double[] o,double[] a,double[] b)
        {
            for(int r=0;r<4;r++)for(int c=0;c<4;c++)o[r*4+c]=a[r*4]*b[c]+a[r*4+1]*b[4+c]+a[r*4+2]*b[8+c]+a[r*4+3]*b[12+c];
        }
        /// <summary>o = a·b with a at offset ao in its array (a joint matrix in a packed rig array).</summary>
        public static void Multiply(double[] o,double[] a,int ao,double[] b)
        {
            for(int r=0;r<4;r++)for(int c=0;c<4;c++)o[r*4+c]=a[ao+r*4]*b[c]+a[ao+r*4+1]*b[4+c]+a[ao+r*4+2]*b[8+c]+a[ao+r*4+3]*b[12+c];
        }
        public static void Point(double[] m,int o,double x,double y,double z,out double wx,out double wy,out double wz)
        {wx=m[o]*x+m[o+1]*y+m[o+2]*z+m[o+3];wy=m[o+4]*x+m[o+5]*y+m[o+6]*z+m[o+7];wz=m[o+8]*x+m[o+9]*y+m[o+10]*z+m[o+11];}
        public static double Clamp01(double x)=>x<0?0:x>1?1:x;
        /// <summary>siren/fx.ts sm: a clamped smoothstep.</summary>
        public static double Smooth(double x){x=Clamp01(x);return x*x*(3-2*x);}
    }
}
