using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // Pure port of the source's volumetric chip blasts (client/chip-blasts.ts at 05950ea; owner request 2026-10-03:
    // explosions must read as real explosions, not flat decals). Cosmetic only: callers key every blast to an
    // authoritative effect (position and gameplay radius) or a live field, and nothing here feeds back into the match.
    // Numbers, spawn order, the Float32 particle storage and the seeded generator follow the source, so one run
    // reproduces the source's voxels. Coordinates are taken as given: the converted maps keep the source's numbers,
    // so Unity world positions go in and Unity world positions come out.
    public enum NativeBlastStyle{Fire,Shock,Implosion,Frost,Toxic,Void,Nova,Kinetic,Quake,Meteor,Mine}
    public struct NativeBlastTint{public int Core,Hot,Cool;public NativeBlastTint(int core,int hot,int cool){Core=core;Hot=hot;Cool=cool;}}
    /// <summary>A live authoritative field as the source's FieldView presents it (chip fields and ability zones).</summary>
    public struct NativeBlastField{public long Id;public string Kind,Fx;public double X,Y,Z,Radius,Born,Until;public double? LockAt;}
    /// <summary>One pooled point light. Intensity and Distance are three.js PointLight values (decay 2).</summary>
    public sealed class NativeBlastFlash{public double X,Y,Z,Distance=8,At=-1e9,Peak,Life=.3,Intensity;public int Color=0xffffff;}

    /// <summary>The source's generator (chip-blasts.ts rng): a 32-bit LCG, so runs are repeatable.</summary>
    public sealed class NativeBlastRandom
    {
        public const uint SourceSeed=0x51a57;
        private uint state;
        public NativeBlastRandom(uint seed=SourceSeed){state=seed==0?1u:seed;}
        public double Next(){state=unchecked(state*1664525u+1013904223u);return state/4294967296.0;}
    }

    /// <summary>Growable voxel particle pool (structure of arrays, swap-remove on expiry) and its per-frame draw list.</summary>
    public sealed class NativeBlastPool
    {
        public const int A=10;// age,life,size0,size1,gravity,drag,delay,floor,stretch,spin
        /// <summary>Floats per drawn voxel in <see cref="Output"/>: position xyz, full edge lengths xyz, rotation xyzw, linear rgb.</summary>
        public const int Stride=13;
        public readonly bool Glow;public int Count,Spawned,Capacity,Drawn;
        /// <summary>Multiplies voxel sizes at spawn (the source's close-up stages).</summary>
        public double SizeScale=1;
        /// <summary>Drawn voxels after the last Update. Glow colours are already faded (additive: final colour = rgb).</summary>
        public float[] Output;
        private readonly int initial;private float[] p,v,c0,c1,a;
        public NativeBlastPool(int capacity,bool glow){Glow=glow;initial=capacity;Capacity=capacity;p=new float[capacity*3];v=new float[capacity*3];c0=new float[capacity*3];c1=new float[capacity*3];a=new float[capacity*A];Output=new float[capacity*Stride];}
        public void Spawn(double x,double y,double z,double vx,double vy,double vz,double life,double size0,double size1,int color0,int color1,double gravity=0,double drag=0,double delay=0,double floor=-1e9,double stretch=1,double spin=0)
        {
            if(Count==Capacity)Resize(Capacity*2);
            int i=Count++;Spawned++;int j=i*3,k=i*A;
            p[j]=(float)x;p[j+1]=(float)y;p[j+2]=(float)z;v[j]=(float)vx;v[j+1]=(float)vy;v[j+2]=(float)vz;
            NativeBlastSim.Linear(color0,out double r,out double g,out double b);c0[j]=(float)r;c0[j+1]=(float)g;c0[j+2]=(float)b;
            NativeBlastSim.Linear(color1,out r,out g,out b);c1[j]=(float)r;c1[j+1]=(float)g;c1[j+2]=(float)b;
            a[k]=(float)-delay;a[k+1]=(float)Math.Max(.01,life);a[k+2]=(float)(size0*SizeScale);a[k+3]=(float)(size1*SizeScale);a[k+4]=(float)gravity;a[k+5]=(float)drag;a[k+6]=(float)delay;a[k+7]=(float)floor;a[k+8]=(float)stretch;a[k+9]=(float)spin;
        }
        // Arrays are replaced only at a capacity boundary (doubling, as the source's InstancedMesh pools).
        private void Resize(int capacity)
        {
            float[] Grow(float[] old,int stride){var next=new float[capacity*stride];Array.Copy(old,next,Math.Min(old.Length,Math.Min(Count,capacity)*stride));return next;}
            p=Grow(p,3);v=Grow(v,3);c0=Grow(c0,3);c1=Grow(c1,3);a=Grow(a,A);Output=new float[capacity*Stride];Capacity=capacity;if(Drawn>capacity)Drawn=capacity;
        }
        /// <summary>Age and position of particle i (checks).</summary>
        public (double x,double y,double z,double age,double life) At(int i){int j=i*3;return (p[j],p[j+1],p[j+2],a[i*A],a[i*A+1]);}
        private void Kill(int i)
        {
            int last=--Count;if(i==last)return;int j=i*3,l=last*3;
            for(int n=0;n<3;n++){p[j+n]=p[l+n];v[j+n]=v[l+n];c0[j+n]=c0[l+n];c1[j+n]=c1[l+n];}
            int k=i*A,m=last*A;for(int n=0;n<A;n++)a[k+n]=a[m+n];
        }
        public void Update(double dt)
        {
            for(int i=Count-1;i>=0;i--)
            {
                int k=i*A;a[k]=(float)(a[k]+dt);if(a[k]>=a[k+1]){Kill(i);continue;}if(a[k]<0)continue;
                int j=i*3;double drag=Math.Exp(-a[k+5]*dt);
                v[j]=(float)(v[j]*drag);v[j+2]=(float)(v[j+2]*drag);v[j+1]=(float)(v[j+1]*drag-a[k+4]*dt);
                p[j]=(float)(p[j]+v[j]*dt);p[j+1]=(float)(p[j+1]+v[j+1]*dt);p[j+2]=(float)(p[j+2]+v[j+2]*dt);
                if(p[j+1]<a[k+7]){p[j+1]=a[k+7];if(v[j+1]<0){v[j+1]=(float)(v[j+1]*-.28);v[j]=(float)(v[j]*.6);v[j+2]=(float)(v[j+2]*.6);}}
            }
            int n=0;var o=Output;
            for(int i=0;i<Count;i++)
            {
                int k=i*A,j=i*3;double age=a[k];if(age<0)continue;
                double life=a[k+1],size0=a[k+2],size1=a[k+3],u=Math.Min(1,age/life),size=(size0+(size1-size0)*u)*(Glow?1:Math.Min(1,(1-u)*4));
                // three.js Euler(age*spin*1.3+i, age*spin+i*.7, 0) in XYZ order.
                double spin=a[k+9],ex=age*spin*1.3+i,ey=age*spin+i*.7,c1x=Math.Cos(ex/2),c2y=Math.Cos(ey/2),s1x=Math.Sin(ex/2),s2y=Math.Sin(ey/2);
                double qx=s1x*c2y,qy=c1x*s2y,qz=s1x*s2y,qw=c1x*c2y,sx=size,sy=size,sz=size,stretch=a[k+8];
                if(stretch!=1)
                {
                    double vx=v[j],vy=v[j+1],vz=v[j+2],l=Math.Sqrt(vx*vx+vy*vy+vz*vz);
                    if(l>.05)
                    {
                        // Quaternion.setFromUnitVectors(+Y, velocity) then normalize(), as three.js r180.
                        double tx=vx/l,ty=vy/l,tz=vz/l,r=ty+1;
                        if(r<1e-8){qx=0;qy=0;qz=1;qw=0;}else{qx=tz;qy=0;qz=-tx;qw=r;}
                        double len=Math.Sqrt(qx*qx+qy*qy+qz*qz+qw*qw);if(len==0){qx=qy=qz=0;qw=1;}else{len=1/len;qx*=len;qy*=len;qz*=len;qw*=len;}
                    }
                    sy=size*stretch;
                }
                double t=Math.Min(1,u*1.4),r0=c0[j],g0=c0[j+1],b0=c0[j+2];
                r0+=(c1[j]-r0)*t;g0+=(c1[j+1]-g0)*t;b0+=(c1[j+2]-b0)*t;
                // Additive voxels fade by dimming; opaque voxels fade by shrinking (size1 carries that).
                if(Glow){double fade=(1-u)*(1-u)*1.15;r0*=fade;g0*=fade;b0*=fade;}
                int w=n*Stride;
                o[w]=p[j];o[w+1]=p[j+1];o[w+2]=p[j+2];o[w+3]=(float)sx;o[w+4]=(float)sy;o[w+5]=(float)sz;
                o[w+6]=(float)qx;o[w+7]=(float)qy;o[w+8]=(float)qz;o[w+9]=(float)qw;o[w+10]=(float)r0;o[w+11]=(float)g0;o[w+12]=(float)b0;n++;
            }
            Drawn=n;
        }
        public void Clear(){Count=0;Drawn=0;if(Capacity!=initial){Count=0;Resize(initial);}}
    }

    public sealed class NativeBlastSim
    {
        /// <summary>Seconds the shock front takes to reach the gameplay radius. The front ends exactly at that radius.</summary>
        public const double FrontSeconds=.3;
        public const int SolidCapacity=900,GlowCapacity=700,LightCapacity=3;
        /// <summary>Voxels every blast puts on its gameplay radius however full the pools are (source §13.3).</summary>
        public const int MinVoxels=16;
        /// <summary>three.js PointLight intensity to Unity built-in intensity; the gain NativeCombatGlow already uses.</summary>
        public const float UnityLightGain=.35f;
        public readonly NativeBlastPool Solid=new NativeBlastPool(SolidCapacity,false),Glow=new NativeBlastPool(GlowCapacity,true);
        public readonly NativeBlastFlash[] Flashes=new NativeBlastFlash[LightCapacity];
        public double Time{get;private set;}
        public int FieldClocks=>fieldClock.Count;
        private Func<double> random;private int nextLight;
        private readonly Dictionary<long,double> fieldClock=new Dictionary<long,double>();
        private readonly HashSet<long> locked=new HashSet<long>();private readonly List<long> stale=new List<long>();
        public NativeBlastSim(Func<double> random=null){this.random=random??new NativeBlastRandom().Next;for(int i=0;i<LightCapacity;i++)Flashes[i]=new NativeBlastFlash();}

        private static readonly int[][] palette={
            /*fire*/new[]{0xfff6d2,0xffb340,0xd8401c,0x57504c,0x5b4636},/*shock*/new[]{0xffffff,0xbfe6ff,0x6f8dff,0x2a3048,0x8fd6ff},
            /*implosion*/new[]{0xffffff,0xd59bff,0x7a32d8,0x231834,0x6b45a8},/*frost*/new[]{0xffffff,0xc9f4ff,0x5fb8ef,0xb9dceb,0xe6fbff},
            /*toxic*/new[]{0xf2ffd0,0xb8f53c,0x4f9a25,0x3c5a2a,0x4a5a30},/*void*/new[]{0xf3e2ff,0xbc72ff,0x4a1d86,0x1c1028,0x7a4ac2},
            /*nova*/new[]{0xffffff,0xfff0a8,0xffa24a,0x4a3b30,0xffd27a},/*kinetic*/new[]{0xffffff,0xbff7ff,0x52c7ff,0x3b4650,0x8a99a6},
            /*quake*/new[]{0xfff1d8,0xffc27a,0x9a6a3a,0x6b5a48,0x5d4b3a},/*meteor*/new[]{0xfff2c4,0xff9a2e,0xc8321a,0x332b28,0x4a3a30},
            /*mine*/new[]{0xffe9e0,0xff6a5a,0xb8202c,0x352c2e,0x4b3d3a}};
        private struct Palette{public int Core,Hot,Cool,Smoke,Debris;}
        private static Palette Colors(NativeBlastStyle style,NativeBlastTint? tint)
        {var c=palette[(int)style];var p=new Palette{Core=c[0],Hot=c[1],Cool=c[2],Smoke=c[3],Debris=c[4]};if(tint.HasValue){p.Core=tint.Value.Core;p.Hot=tint.Value.Hot;p.Cool=tint.Value.Cool;}return p;}
        /// <summary>Maps an authoritative explosion message (the engine's blast kind, chip or ability fx tag) to a style.</summary>
        public static NativeBlastStyle StyleOf(string message)
        {
            switch(message)
            {
                case "thunder":case "thunderstep":case "static":case "storm":case "lightning-rod":case "orbital-mark":case "blink-strike":case "ball-lightning":return NativeBlastStyle.Shock;
                case "implosion":case "implosion-bomb":case "singularity":return NativeBlastStyle.Implosion;
                case "frost":case "blizzard":case "geyser-wall":return NativeBlastStyle.Frost;
                case "corpseParty":case "toxic":case "acid-rain":return NativeBlastStyle.Toxic;
                case "meteor":case "artillery":case "meteor-strike":return NativeBlastStyle.Meteor;
                case "mine":case "cluster-mine":case "mirror-mine":case "bread-crumbs":case "proximity-mail":return NativeBlastStyle.Mine;
                case "dashBlast":case "kinetic":case "bubble-mine":case "frag":case "tidal-wave":return NativeBlastStyle.Kinetic;
                case "quake":case "aftershock":case "trampoline":return NativeBlastStyle.Quake;
                case "vacuum":case "void":case "black-hole":case "wormhole":case "shackles":return NativeBlastStyle.Void;
                case "blockPulse":case "nova":return NativeBlastStyle.Nova;
                default:return NativeBlastStyle.Fire;
            }
        }
        /// <summary>Distinct looks for ability tags that share a style (source §13.1).</summary>
        public static NativeBlastTint? TintOf(string tag)
        {
            switch(tag)
            {
                case "frag":return new NativeBlastTint(0xfff3e2,0xcfc8bc,0x676b72);case "tidal-wave":return new NativeBlastTint(0xeaffff,0x8de9ee,0x2b8bd6);
                case "geyser-wall":return new NativeBlastTint(0xf2ffff,0x9cefff,0x2a9ae6);case "orbital-mark":return new NativeBlastTint(0xffffff,0xff9a8a,0xe8323a);
                case "blink-strike":return new NativeBlastTint(0xf2ffff,0x9ffcff,0x1fc4e6);case "ball-lightning":return new NativeBlastTint(0xfbf0ff,0xd7a6ff,0x8a4cff);
                case "flame-lance":return new NativeBlastTint(0xffffff,0xfff0b0,0xffc23a);default:return null;
            }
        }
        /// <summary>three.js Color.setHex: sRGB hex to the linear working space.</summary>
        public static void Linear(int hex,out double r,out double g,out double b){r=ToLinear(((hex>>16)&255)/255.0);g=ToLinear(((hex>>8)&255)/255.0);b=ToLinear((hex&255)/255.0);}
        private static double ToLinear(double c)=>c<0.04045?c*0.0773993808:Math.Pow(c*0.9478672986+0.0521327014,2.4);
        private static double ToSrgb(double c)=>c<0.0031308?c*12.92:1.055*Math.Pow(c,0.41666)-0.055;
        /// <summary>three.js Color.getHex from linear channels.</summary>
        public static int Hex(double r,double g,double b){int H(double c)=>(int)Round(Math.Max(0,Math.Min(255,ToSrgb(c)*255)));return H(r)*65536+H(g)*256+H(b);}
        /// <summary>JavaScript Math.round (halves round up), not banker's rounding.</summary>
        public static double Round(double v)=>Math.Floor(v+.5);
        private static double Clamp01(double v)=>Math.Max(0,Math.Min(1,v));
        public static float UnityLightIntensity(double three)=>(float)three*UnityLightGain;
        public static double ThreeLightIntensity(float unity)=>unity/UnityLightGain;

        private void Flash(double x,double y,double z,int color,double radius,double peak,double life,bool compat,double quality,bool minor=false)
        {
            if(compat||quality<.5)return;NativeBlastFlash f=null;
            if(minor){foreach(var c in Flashes)if(Time-c.At>=c.Life){f=c;break;}}else f=Flashes[nextLight++%Flashes.Length];
            if(f==null)return;f.X=x;f.Y=y+.4;f.Z=z;f.Color=color;f.Distance=Math.Max(4,radius*3);f.At=Time;f.Peak=peak;f.Life=life;
        }
        // Sphere of directions, roughly even (golden spiral) with jitter.
        private void Dir(int i,int n,bool hemi,out double x,out double y,out double z)
        {y=hemi?1-(i+.5)/n:1-2*(i+.5)/n;double r=Math.Sqrt(Math.Max(0,1-y*y)),phi=i*2.39996+random()*.4;x=Math.Cos(phi)*r;z=Math.Sin(phi)*r;}
        // Ground sweep that FILLS the blast disc (owner request 2026-10-04: no outlined area circle): each voxel slides from
        // the centre to its own random spot inside 92% of the radius within FrontSeconds and fades there.
        private void Ring(double px,double pz,double ground,double radius,int color0,int color1,int count,bool glow,double height=.08,double size=.16)
        {
            var pool=glow?Glow:Solid;double r0=Math.Min(.25,radius*.2);
            for(int i=0;i<count;i++){double a=(double)i/count*Math.PI*2+random()*.6,c=Math.Cos(a),s=Math.Sin(a),r=r0+(radius*.92-r0)*Math.Sqrt(random()),speed=(r-r0)/FrontSeconds;pool.Spawn(px+c*r0,ground+height,pz+s*r0,c*speed,0,s*speed,FrontSeconds*(1.1+random()),size,size*.3,color0,color1,0,0,0,-1e9,1,0);}
        }
        // Hemispherical shock shell: the volumetric read of the blast radius (ends exactly at it).
        private void Shell(double px,double pz,double ground,double radius,int color0,int color1,int count)
        {
            double r0=Math.Min(.25,radius*.2),speed=(radius-r0)/FrontSeconds;
            for(int i=0;i<count;i++){Dir(i,count,true,out double x,out double y,out double z);Glow.Spawn(px+x*r0,ground+y*r0,pz+z*r0,x*speed,y*speed,z*speed,FrontSeconds+.001,.2,.1,color0,color1);}
        }
        // Voxels that start on the radius and rush to the centre in t0 (implosions, vacuum); they never drop.
        private void Inward(double cx,double cy,double cz,double R,int count,int c0,int c1,double t0,double swirl=0,double lift=.7)
        {
            for(int i=0;i<count;i++)
            {
                Dir(i,count,true,out double x,out double y,out double z);double h=Math.Sqrt(x*x+z*z);if(h==0)h=1;double X=x/h*R,Z=z/h*R,Y=(y-.3)*R*lift;
                Glow.Spawn(cx+X,cy+Y,cz+Z,-X/t0-Z*swirl,-Y/t0,-Z/t0+X*swirl,t0,.16,.24,c0,c1,0,0,0,-1e9,1,swirl!=0?3:0);
            }
        }
        /// <summary>A line of glow voxels from a to b (arcs, teleport and rally streaks); sweep delays the far end so it travels.</summary>
        public void Line(double ax,double ay,double az,double bx,double by,double bz,int color0,int color1,double quality,double jitter=.25,double life=.14,double sweep=0)
        {
            double d=Math.Sqrt((bx-ax)*(bx-ax)+(by-ay)*(by-ay)+(bz-az)*(bz-az));int c=(int)Math.Min(40,Math.Max(3,Round(d*5*quality)));
            for(int i=0;i<=c;i++){double u=(double)i/c,j=i>0&&i<c?jitter:0;Glow.Spawn(ax+(bx-ax)*u+(random()-.5)*j,ay+(by-ay)*u+(random()-.5)*j,az+(bz-az)*u+(random()-.5)*j,0,0,0,life,.11,.05,color0,color1,0,0,u*sweep);}
        }
        /// <summary>Pong rally paddle flash: a 4x4 voxel square at the bounce point, facing and drifting along the rally.</summary>
        public void Paddle(double x,double y,double z,double dx,double dy,double dz,int color,double quality)
        {
            if(quality<=0)return;double l=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(l==0)l=1;double nx=dx/l,ny=dy/l,nz=dz/l,ux=-nz,uz=nx,h=Math.Sqrt(ux*ux+uz*uz);
            if(h<.01){ux=1;uz=0;}else{ux/=h;uz/=h;}double vx=-ny*uz,vy=nz*ux-nx*uz,vz=ny*ux;
            for(int i=0;i<4;i++)for(int j=0;j<4;j++){double a=(i-1.5)*.16,b=(j-1.5)*.16;Glow.Spawn(x+ux*a+vx*b,y+vy*b,z+uz*a+vz*b,nx*1.5,ny*1.5,nz*1.5,.15,.15,.06,0xffffff,color);}
        }
        /// <summary>Small volumetric impact (chip procs, splashes, field arrivals): a voxel front ending on radius, core, debris, motes, an idle light.</summary>
        public void Impact(double px,double py,double pz,double radius,int hot,double quality,bool compat=false,bool ground=false,int? cool=null)
        {
            int cold;if(cool.HasValue)cold=cool.Value;else{Linear(hot,out double lr,out double lg,out double lb);cold=Hex(lr*.45,lg*.45,lb*.45);}
            double q=Clamp01(quality)*(compat?.5:1);if(q<=.02)return;
            double R=Math.Max(.3,radius),t0=FrontSeconds,g=py-.95,y0=ground?g+.12:py,scale=Math.Min(1.5,.6+R*.2);int N(double b)=>(int)Math.Max(2,Round(b*q*scale));
            if(ground){double r0=Math.Min(.25,R*.2);for(int i=0,c=N(26);i<c;i++){double a=(double)i/c*Math.PI*2+random()*.6,x=Math.Cos(a),z=Math.Sin(a),s=(r0+(R*.92-r0)*Math.Sqrt(random())-r0)/t0;Glow.Spawn(px+x*r0,y0,pz+z*r0,x*s,0,z*s,t0*(1.1+random()),.17,.05,hot,cold);}}
            for(int i=0,c=N(ground?14:20);i<c;i++){Dir(i,c,ground,out double x,out double y,out double z);double s=(R-.1)/t0;Glow.Spawn(px+x*.1,y0+y*.1,pz+z*.1,x*s,y*s,z*s,t0+.001,.13,.06,0xffffff,hot);}
            for(int i=0,c=N(5);i<c;i++)Glow.Spawn(px,y0+.05,pz,(random()-.5)*1.2,(random()-.2)*1.2,(random()-.5)*1.2,.16,.26,.08,0xffffff,hot);
            for(int i=0,c=N(7);i<c;i++){Dir(i,c,true,out double x,out double y,out double z);double s=R*(1.5+random()*2);Solid.Spawn(px,y0,pz,x*s,Math.Abs(y)*s+1.5,z*s,.5+random()*.3,.07+random()*.06,.04,hot,cold,12,.6,0,g+.04,1,6);}
            for(int i=0,c=N(6);i<c;i++){double a=random()*Math.PI*2,r=R*Math.Sqrt(random())*.9;Glow.Spawn(px+Math.Cos(a)*r,y0,pz+Math.Sin(a)*r,0,1.4+random(),0,.5+random()*.3,.07,.03,hot,cold,0,.4,.05);}
            Flash(px,py,pz,hot,R,2.5+R,.22,compat,q*2,true);
        }
        /// <summary>
        /// One authoritative blast. radius is the gameplay radius from the effect; the shock front and shell end exactly
        /// on it, the fireball fills ~60% of it, debris and smoke may travel past it (cosmetic). Returns the voxels spawned.
        /// </summary>
        public int Blast(NativeBlastStyle style,double px,double py,double pz,double radius,double quality=1,bool compat=false,NativeBlastTint? tint=null)
        {
            double q=Clamp01(quality);if(q<=0)return 0;int spawned=Solid.Spawned+Glow.Spawned;var pal=Colors(style,tint);
            double R=Math.Max(.4,radius),k=q*(compat?.4:1)*Math.Min(1.6,.55+R*.18),ground=py-.95,cx=px,cy=Math.Max(ground+.35,py),cz=pz;
            int N(double b)=>(int)Math.Max(1,Round(b*k));int M(double b)=>(int)Math.Max(MinVoxels,Round(b*k));
            void Core(double count,double spread,double life,double size,double rise=0)
            {for(int i=0,c=N(count);i<c;i++){Dir(i,c,false,out double x,out double y,out double z);double s=spread*(.6+random()*.6);Glow.Spawn(cx,cy,cz,x*s,y*s*.8+rise,z*s,life*(.7+random()*.5),size*(.8+random()*.5),size*.3,pal.Core,pal.Hot,-rise*.5,3.5);}}
            void Fireball(double count,double reach,double life,double size,double rise)
            {for(int i=0,c=N(count);i<c;i++){Dir(i,c,false,out double x,out double y,out double z);double s=reach*(2.2+random()*1.6);Solid.Spawn(cx+x*.1,cy+Math.Abs(y)*.1,cz+z*.1,x*s,Math.Abs(y)*s*.7+rise,z*s,life*(.75+random()*.5),size*(.8+random()*.6),size*.12,pal.Hot,pal.Cool,-rise*.6,3.2,0,ground,1,2);}}
            void Smoke(double count,double reach,double delay)
            {for(int i=0,c=N(count);i<c;i++){Dir(i,c,false,out double x,out _,out double z);double s=reach*(.4+random()*.7);Solid.Spawn(cx+x*reach*.35,cy+.1,cz+z*reach*.35,x*s,1.1+random()*1.3,z*s,1.1+random()*.7,.18+random()*.12,.55+random()*.35*Math.Min(2,R/2),pal.Smoke,0x16181c,-.25,1.6,delay,ground,1,.6);}}
            void Debris(double count,double power,int color)
            {for(int i=0,c=N(count);i<c;i++){Dir(i,c,true,out double x,out double y,out double z);double s=power*(.5+random()*.8);Solid.Spawn(cx,cy,cz,x*s,Math.Abs(y)*s+2+random()*3,z*s,.9+random()*.6,.09+random()*.12,.06,color,color,14,.4,0,ground+.04,1,6);}}
            void Sparks(double count,double power,int color)
            {for(int i=0,c=N(count);i<c;i++){Dir(i,c,false,out double x,out double y,out double z);double s=power*(.6+random()*.9);Glow.Spawn(cx,cy,cz,x*s,Math.Abs(y)*s+1,z*s,.35+random()*.35,.06,.03,pal.Core,color,9,.8,0,ground+.03,4);}}
            switch(style)
            {
                case NativeBlastStyle.Fire:case NativeBlastStyle.Mine:case NativeBlastStyle.Meteor:
                    Core(18,R*2.2,.24,.5);Fireball(42,R*.6,.7,.62,1.4);Shell(px,pz,ground,R,pal.Hot,pal.Cool,M(34));Ring(px,pz,ground,R,pal.Hot,pal.Cool,M(36),true);Smoke(16,R*.6,.18);Debris(14,R*2.6,pal.Debris);Sparks(18,R*3.2,pal.Hot);
                    if(style==NativeBlastStyle.Meteor)for(int i=0,c=N(10);i<c;i++)Glow.Spawn(cx+(random()-.5)*.6,cy+.2,cz+(random()-.5)*.6,(random()-.5)*1.4,6+random()*5,(random()-.5)*1.4,.55,.22,.06,pal.Core,pal.Cool,8,.5);
                    Flash(cx,cy,cz,pal.Hot,R,9+R*2,.38,compat,q);break;
                case NativeBlastStyle.Kinetic:
                    Core(10,R*2,.16,.3);Shell(px,pz,ground,R,pal.Hot,pal.Cool,M(40));Ring(px,pz,ground,R,pal.Core,pal.Cool,M(40),true,.06,.2);Ring(px,pz,ground,R*.98,0xd9e6ee,0x6f7d88,M(26),false,.12,.22);Debris(10,R*2.2,pal.Debris);Sparks(14,R*3,pal.Cool);Smoke(6,R*.4,.05);Flash(cx,cy,cz,pal.Hot,R,6+R,.28,compat,q);break;
                case NativeBlastStyle.Quake:
                    Ring(px,pz,ground,R,pal.Hot,pal.Cool,M(30),true,.05,.18);
                    for(int i=0,c=N(26);i<c;i++){double a=(double)i/c*Math.PI*2,r=R*(.25+random()*.7),x=Math.Cos(a)*r,z=Math.Sin(a)*r;Solid.Spawn(px+x,ground,pz+z,x*.6,5+random()*4,z*.6,1.1,.2+random()*.2,.08,pal.Debris,0x3a3028,16,.3,random()*.12,ground+.05,1,4);}
                    Smoke(14,R*.7,0);Flash(cx,cy,cz,pal.Hot,R,4,.25,compat,q);break;
                case NativeBlastStyle.Shock:
                {
                    // A jagged bolt from the sky into the centre, a white core, a blue shock front and skittering arc sparks.
                    double top=ground+9,x=cx,z=cz;int steps=N(22);
                    for(int i=0;i<steps;i++)
                    {
                        double y=top-(top-ground)*((double)i/steps);x+=(random()-.5)*.45;z+=(random()-.5)*.45;if(i==steps-1){x=cx;z=cz;}
                        Glow.Spawn(x,y,z,0,0,0,.22+(double)i/steps*.08,.22,.12,pal.Core,pal.Hot,0,0,i*.004);
                        if(i%4==2)Glow.Spawn(x,y,z,(random()-.5)*8,(random()-.5)*3,(random()-.5)*8,.2,.1,.04,pal.Core,pal.Cool,0,2,i*.004);
                    }
                    Core(12,R*1.6,.2,.34);Shell(px,pz,ground,R,pal.Hot,pal.Cool,M(36));Ring(px,pz,ground,R,pal.Core,pal.Cool,M(36),true,.06,.18);Sparks(26,R*3.6,pal.Cool);
                    for(int arc=0;arc<N(5);arc++){double a=random()*Math.PI*2,len=R*(.5+random()*.5);for(int s=1;s<=6;s++){double u=s/6.0;Glow.Spawn(cx+Math.Cos(a)*len*u+(random()-.5)*.3,ground+.15+random()*.5,cz+Math.Sin(a)*len*u+(random()-.5)*.3,0,0,0,.16,.12,.05,pal.Core,pal.Cool,0,0,.03+u*.04);}}
                    Flash(cx,cy,cz,pal.Hot,R,12+R*2,.3,compat,q);break;
                }
                case NativeBlastStyle.Implosion:
                    // Voxels rush in from the radius, then the centre pops.
                    Inward(cx,cy,cz,R,M(44),pal.Cool,pal.Hot,.28);
                    Core(16,R*2.4,.3,.38);Shell(px,pz,ground,R,pal.Hot,pal.Cool,M(30));Sparks(16,R*2.8,pal.Hot);Smoke(8,R*.3,.15);Flash(cx,cy,cz,pal.Hot,R,10,.4,compat,q);break;
                case NativeBlastStyle.Void:
                    Inward(cx,cy,cz,R,M(40),pal.Cool,pal.Hot,.4,.56,.5);
                    for(int i=0,c=N(10);i<c;i++)Solid.Spawn(cx,cy,cz,0,0,0,.42,.05,.26+random()*.1,0x07030f,0x1c0c33,0,0,.05,-1e9,1,5);
                    Flash(cx,cy,cz,pal.Hot,R,5,.3,compat,q);break;
                case NativeBlastStyle.Nova:
                    Core(18,R*2.6,.25,.42);
                    for(int i=0,c=M(56);i<c;i++){Dir(i,c,false,out double x,out double y,out double z);double r0=.25,s=(R-r0)/FrontSeconds;Glow.Spawn(cx+x*r0,cy+y*r0,cz+z*r0,x*s,y*s,z*s,FrontSeconds+.001,.24,.12,pal.Core,pal.Cool);}
                    Ring(px,pz,ground,R,pal.Hot,pal.Cool,M(34),true);Sparks(20,R*3.4,pal.Hot);Flash(cx,cy,cz,pal.Hot,R,12,.4,compat,q);break;
                case NativeBlastStyle.Frost:
                    for(int i=0,c=N(26);i<c;i++){Dir(i,c,true,out double x,out double y,out double z);double s=R*(1.4+random()*1.4);Solid.Spawn(cx,cy,cz,x*s,Math.Abs(y)*s+1.5,z*s,.9+random()*.4,.12,.05,pal.Debris,pal.Cool,9,1.5,0,ground+.04,3.5);}
                    Core(12,R*1.6,.22,.3);Ring(px,pz,ground,R,pal.Core,pal.Cool,M(32),true);Smoke(10,R*.5,.05);Flash(cx,cy,cz,pal.Hot,R,6,.3,compat,q);break;
                case NativeBlastStyle.Toxic:
                    Core(10,R*1.6,.2,.3);Fireball(20,R*.5,.7,.4,.9);Shell(px,pz,ground,R,pal.Hot,pal.Cool,M(28));Ring(px,pz,ground,R,pal.Hot,pal.Cool,M(30),true);
                    for(int i=0,c=N(18);i<c;i++){Dir(i,c,false,out double x,out _,out double z);double s=R*(.3+random()*.5);Solid.Spawn(cx,cy,cz,x*s,.6+random()*.8,z*s,1.6+random()*.6,.2,.6+random()*.3,pal.Cool,pal.Smoke,-.15,1.3,.1,ground,1,.5);}
                    Debris(10,R*2,pal.Debris);Flash(cx,cy,cz,pal.Hot,R,7,.35,compat,q);break;
            }
            return Solid.Spawned+Glow.Spawned-spawned;
        }
        private static double KindRate(string kind)
        {
            switch(kind){case "storm":return 9;case "acid":return 36;case "fire":return 64;case "frost":return 26;case "time":return 14;case "black-hole":return 50;case "repulsion":return 18;case "heal":return 20;case "implosion":return 40;case "bomb":return 10;case "mine":return 6;default:return 0;}
        }
        /// <summary>
        /// Ambient emitters for a live authoritative field: telegraph motes inside the gameplay radius, an arrival impact
        /// sized to it (source §19.2) and the §13.1 ability emitters. Rate-limited per field and by quality.
        /// </summary>
        public void Field(NativeBlastField f,double time,double quality,bool compat=false)
        {
            string fx=f.Fx??"",kind=f.Kind??"";double q=Clamp01(quality)*(compat?.4:1);bool meteor=fx=="artillery"||fx=="meteor-strike",fused=kind=="bomb"&&(meteor||fx=="thunderstep"||fx=="comet"||fx=="orbital-mark"||fx=="geyser-wall");if(q<=0)return;
            double rate=fused?40:fx=="singularity"?70:KindRate(kind);if(rate==0)return;
            double R=f.Radius,ground=Math.Max(.03,f.Y-.95),ax=f.X,ay=ground+.95,az=f.Z,lockAt=f.LockAt??1e9;
            var style=kind=="bomb"||kind=="mine"?StyleOf(fx.Length>0?fx:kind):kind=="storm"?NativeBlastStyle.Shock:kind=="acid"||kind=="heal"?NativeBlastStyle.Toxic:kind=="frost"||kind=="time"?NativeBlastStyle.Frost:kind=="implosion"||fx=="singularity"?NativeBlastStyle.Implosion:kind=="black-hole"?NativeBlastStyle.Void:kind=="repulsion"?NativeBlastStyle.Mine:NativeBlastStyle.Fire;
            var pal=Colors(style,TintOf(fx));
            if(fx=="orbital-mark"&&time>=lockAt&&!locked.Contains(f.Id)){locked.Add(f.Id);Impact(ax,ay,az,R,pal.Core,q,false,true,pal.Cool);Flash(ax,ay,az,pal.Hot,R,8,.3,compat,q);}
            bool known=fieldClock.TryGetValue(f.Id,out double last);
            if(!known||time<last)
            {
                fieldClock[f.Id]=time;
                if(!known&&!fused&&time-f.Born<.3){if(kind=="implosion")Inward(ax,ay,az,R,4+(int)Round(24*q),pal.Hot,pal.Core,.5,0,.3);else Impact(ax,ay,az,R,pal.Hot,q*.7,compat,true,pal.Cool);}
                return;
            }
            int due=(int)Math.Min(6,Math.Floor((time-last)*rate*q));if(due<=0)return;fieldClock[f.Id]=last+due/(rate*q);
            double left=f.Until-time,u=Clamp01((time-f.Born)/Math.Max(.01,f.Until-f.Born));
            void Spot(out double sx,out double sz,out double sr){double a=random()*Math.PI*2,r=Math.Sqrt(random())*R*.95;sx=f.X+Math.Cos(a)*r;sz=f.Z+Math.Sin(a)*r;sr=r;}
            for(int n=0;n<due;n++)
            {
                // Telegraph: motes rise from anywhere inside the area (owner request 2026-10-04: no motes tracing the outline).
                if(random()<8/rate){Spot(out double mx,out double mz,out _);Glow.Spawn(mx,ground+.06,mz,0,.9+random()*.6,0,.6,.1,.03,pal.Hot,pal.Cool);}
                if(fused)
                {
                    // Meteor Shower / Meteor Strike: the meteor is visible falling onto its warning circle for the last 0.45 s.
                    if(meteor){if(left>.45)continue;double w=Math.Max(0,left/.45),m=Math.Max(1,R/2.8),y=ground+.6+w*6*m,x=f.X-w*2.4*m,z=f.Z-w*1.1*m;Glow.Spawn(x,y,z,(random()-.5)*.6,1.2,(random()-.5)*.6,.28,.42*m,.08,0xfff2c4,0xff7a2a);Solid.Spawn(x+(random()-.5)*.25*m,y+.2,z+(random()-.5)*.25*m,0,.4,0,.5,.2*m,.35*m,0x4a3a30,0x262020,-.2,1);}
                    // Thunder Step: the target spot crackles before the bolt.
                    else if(fx=="thunderstep"){double a=random()*Math.PI*2,r=R*(.5+random()*.5);Glow.Spawn(f.X+Math.Cos(a)*r,ground+.08,f.Z+Math.Sin(a)*r,-Math.Cos(a)*r*2,2+random()*2,-Math.Sin(a)*r*2,.22,.08,.03,0xffffff,0x8fb8ff);}
                    // Orbital Mark: a targeting column from the sky (the shrinking outline ring was removed, owner request 2026-10-04).
                    else if(fx=="orbital-mark"){for(int i=0;i<3;i++)Glow.Spawn(f.X+(random()-.5)*.2,ground+random()*9,f.Z+(random()-.5)*.2,0,-7,0,.3,time>=lockAt?.15:.1,.05,pal.Core,pal.Cool,0,0,0,-1e9,6);}
                    // Geyser Wall: water fills the ring from the centre as the geyser arms, so the three erupt in order.
                    else if(fx=="geyser-wall"){double a=random()*Math.PI*2,r=R*u*Math.Sqrt(random());Glow.Spawn(f.X+Math.Cos(a)*r,ground+.05,f.Z+Math.Sin(a)*r,(random()-.5)*.4,1+u*3+random(),(random()-.5)*.4,.35+u*.25,.12,.05,pal.Core,pal.Hot,3);}
                    // Comet Dash: the rolling pilot trails fire until the landing blast.
                    else Solid.Spawn(f.X+(random()-.5)*.4,f.Y-.3+random()*.6,f.Z+(random()-.5)*.4,0,.8,0,.45,.3,.05,0xffc04a,0xd8401c,-.4,.6,0,-1e9,1,4);
                    continue;
                }
                if(kind=="storm")
                {
                    Spot(out double sx,out double sz,out _);double x=sx,z=sz,top=ground+3.6;
                    for(int i=0;i<12;i++){double y=top-(top-ground)*(i/11.0);Glow.Spawn(x,y,z,0,0,0,.2,.24,.12,0xffffff,pal.Hot);x+=(random()-.5)*.4;z+=(random()-.5)*.4;}
                    Glow.Spawn(sx,ground+.15,sz,0,0,0,.18,.6,.2,0xffffff,pal.Hot);for(int i=0;i<5;i++)Glow.Spawn(sx,ground+.1,sz,(random()-.5)*5,2+random()*2,(random()-.5)*5,.3,.08,.03,0xffffff,pal.Cool,9);
                    for(int i=0;i<2;i++){Spot(out double cxs,out double czs,out _);Solid.Spawn(cxs,ground+3.5+random()*.5,czs,(random()-.5)*.3,0,(random()-.5)*.3,1.4,.45,.75,0x4a5378,0x2a3048,0,.5);}
                }
                else if(kind=="acid"){Spot(out double sx,out double sz,out _);Glow.Spawn(sx,ground+.05,sz,0,.9+random()*.8,0,.7+random()*.4,.14,.26,pal.Hot,pal.Cool,0,0);if(random()<.45)Solid.Spawn(sx,ground+.15,sz,(random()-.5)*.3,.45,(random()-.5)*.3,1.5,.18,.55,0x6f9a3a,0x2c3a22,-.1,1);}
                else if(kind=="fire"){Spot(out double sx,out double sz,out double sr);double hot=1-sr/R;Solid.Spawn(sx,ground+.05,sz,(random()-.5)*.4,1.4+hot*2+random()*1.2,(random()-.5)*.4,.55+hot*.4+random()*.3,.3+hot*.15,.04,random()<.3?0xffe28a:pal.Hot,pal.Cool,-.4,.5,0,-1e9,1,3);if(random()<.4)Glow.Spawn(sx,ground+.2,sz,random()-.5,2.5+random()*2,random()-.5,.6,.05,.02,pal.Core,pal.Hot);}
                else if(kind=="frost"){Spot(out double sx,out double sz,out _);Solid.Spawn(sx,ground+.02,sz,0,.35,0,1.6,.08,.22,0xe8fbff,0x8fd8ff,0,.8,0,-1e9,3.5,0);Glow.Spawn(f.X+(random()-.5)*R*1.6,ground+1.6+random(),f.Z+(random()-.5)*R*1.6,(random()-.5)*.3,-.9,(random()-.5)*.3,1.4,.05,.03,0xffffff,0xbfeaff);}
                else if(kind=="time"){double a=random()*Math.PI*2,y=random();Glow.Spawn(f.X+Math.Cos(a)*R*Math.Sqrt(1-y*y),ground+y*R*.8,f.Z+Math.Sin(a)*R*Math.Sqrt(1-y*y),0,0,0,.9,.1,.04,0xdbe8ff,0x83aeff);}
                else if(kind=="black-hole"){double a=random()*Math.PI*2,r=R*(.7+random()*.3),t0=.6,x=Math.Cos(a)*r,z=Math.Sin(a)*r;Glow.Spawn(f.X+x,ground+.7+(random()-.5),f.Z+z,-x/t0-z*.8,(random()-.5)*.4,-z/t0+x*.8,t0,.12,.04,pal.Hot,pal.Core,0,0,0,-1e9,2.5);}
                // Reverse Grenade: motes stream inward from the radius onto the spinning gravity bomb during the mark.
                else if(kind=="implosion"){double a=random()*Math.PI*2,x=Math.Cos(a)*R,z=Math.Sin(a)*R,t0=.5;Glow.Spawn(f.X+x,ground+.15+random()*.6,f.Z+z,-x/t0,0,-z/t0,t0,.13,.05,pal.Hot,pal.Core,0,0,0,-1e9,2.5);}
                else if(kind=="bomb")Glow.Spawn(f.X,ground+.85,f.Z,(random()-.5)*1.4,1.4+random(),(random()-.5)*1.4,.3,.06,.02,0xffffff,pal.Hot,6);
                else if(kind=="repulsion"){double a=random()*Math.PI*2;Glow.Spawn(f.X+Math.Cos(a)*.4,ground+.3+random()*.8,f.Z+Math.Sin(a)*.4,Math.Cos(a)*R/.5,0,Math.Sin(a)*R/.5,.45,.12,.04,0xffd0da,0xff728f,0,.9,0,-1e9,1.2);}
                else if(kind=="heal"){Spot(out double sx,out double sz,out _);Glow.Spawn(sx,ground+.1,sz,0,1.1,0,.9,.12,.05,0xd6ffe4,0x79ffa9);}
            }
        }
        /// <summary>Forget field clocks for fields that no longer exist. live is read only.</summary>
        public void Prune(HashSet<long> live)
        {
            stale.Clear();foreach(var id in fieldClock.Keys)if(!live.Contains(id))stale.Add(id);foreach(var id in stale)fieldClock.Remove(id);
            stale.Clear();foreach(var id in locked)if(!live.Contains(id))stale.Add(id);foreach(var id in stale)locked.Remove(id);
        }
        public void Update(double dt)
        {
            dt=Math.Max(0,Math.Min(.1,dt));Time+=dt;Solid.Update(dt);Glow.Update(dt);
            foreach(var f in Flashes){double u=(Time-f.At)/f.Life;f.Intensity=u>=0&&u<1?f.Peak*(1-u)*(1-u):0;}
        }
        public void Clear(){Solid.Clear();Glow.Clear();fieldClock.Clear();locked.Clear();foreach(var f in Flashes){f.At=-1e9;f.Intensity=0;}}
        /// <summary>Clear() plus a rewound clock and generator: replaying the same spawns afterwards gives identical voxels.</summary>
        public void Reset(Func<double> random=null){Clear();Time=0;nextLight=0;this.random=random??new NativeBlastRandom().Next;}

        // Unit cube topology for renderers: 8 corners (signs) and 12 triangles wound clockwise seen from outside
        // (Unity front faces), so a back-face-culled draw shows each voxel's three near faces exactly once.
        public static readonly sbyte[] CubeCorners={-1,-1,-1, 1,-1,-1, 1,1,-1, -1,1,-1, -1,-1,1, 1,-1,1, 1,1,1, -1,1,1};
        public static readonly int[] CubeTriangles={0,3,2,0,2,1, 4,5,6,4,6,7, 0,4,7,0,7,3, 1,2,6,1,6,5, 3,7,6,3,6,2, 0,1,5,0,5,4};
    }

    /// <summary>
    /// How Unity's existing effect records map onto the source's blast calls (event-vfx.ts blastEvent and event) until the
    /// records carry the source's event type and message (A21). An ability/chip effect keeps only its position, radius, the
    /// owner's magic school (0 none, 1 fire, 2 water, 3 lightning, 4 darkness) and a beam origin. Its source look comes from
    /// the authoritative Explosion event the match emits with it (same spot, same tick: ExplosionTag), or else from a
    /// replicated field, zone or ability shot that ends exactly there (the caller's job). The school never picks the look.
    /// </summary>
    public static class NativeBlastCues
    {
        public enum Cue{None,Blast,Teleport,Vacuum,Dud}
        public const double TeleportRadius=1.2,TeleportQuality=.5,TeleportImpactRadius=.9,TeleportLineJitter=.3,TeleportLineLife=.22,TeleportLineSweep=.08;
        public const int TeleportColor=0xb66cff;
        /// <summary>event-vfx.ts blastEvent: an explosion without a message (a bare weapon 'blast') draws fire at .55 quality.</summary>
        public const double UntaggedQuality=.55;
        /// <summary>event-vfx.ts blastEvent: the static discharge is an `arc` with a radius, a shock blast at .6 quality.</summary>
        public const double ArcQuality=.6;
        /// <summary>
        /// explosion: the match emitted an Explosion or Vacuum event at this record's spot in its tick (ChipBlast, AbilityBlast,
        /// Vacuum). tag: the source message recovered for it (ExplosionTag, a teleport landing, or a field, zone or ability shot).
        /// </summary>
        public static Cue Classify(bool beam,double radius,int element,string tag,bool siren=false,bool explosion=false)
        {
            // The Siren's own attacks (NativeBlastFrame.AtSiren). The source sends them as siren-* events that client/siren/fx.ts
            // draws as black fire, shock rings and cracks, never as chip blasts; NativeDevEffects draws those from the events, so
            // the record itself draws nothing (the first port's two flat darkness halos are gone).
            if(siren&&!beam)return Cue.None;
            // Blink and the wormhole blink record a radius-1 lightning beam; every other beam is an arc and draws no blast.
            if(beam)return radius>=.99&&element==3?Cue.Teleport:Cue.None;
            // Vacuum() always records (position, 5, darkness).
            if(element==4&&Math.Abs(radius-5)<1e-3)return Cue.Vacuum;
            // An unarmed rocket: the source's dud `impact` (gun-abilities.ts:23), a grey spark burst, not a blast.
            if(tag=="rocket"&&radius<.5)return Cue.Dud;
            // Owner playtest 2026-10-10 ("explosion effects seem to play randomly", Siren adds killed by guns): a record draws a
            // blast only where the source draws one, an authoritative explosion at its spot or an ability's own visual hit blast
            // (flame-lance, tidal-wave, shackles: a tag from its shot). Anything else is a first-port stand-in record (the old
            // gib, summon, revive and pong bounce records drew the owner's school, a fireball for school 0 or 1) and draws none.
            return explosion||tag!=null?Cue.Blast:Cue.None;
        }
        /// <summary>The blast recorded where a teleport beam lands: Blink Strike's (always lightning) or the wormhole chip's.</summary>
        public static string Colocated(int element)=>element==3?"blink-strike":"wormhole";
        /// <summary>chip-blasts.ts blastStyle for a source message; an explosion without one is fire (event-vfx.ts blastEvent).</summary>
        public static NativeBlastStyle Style(string tag)=>tag!=null?NativeBlastSim.StyleOf(tag):NativeBlastStyle.Fire;
        public static double Quality(string tag)=>tag=="flame-lance"?.5:1;
        /// <summary>
        /// The static discharge. Unity's untagged ChipBlast ("chip") is either the discharge on a summon or PvE add
        /// (NativeSummons.MinionWeaponHit, 3 m) or Corpse Party (3.5 m); the source emits the first as `arc`, the second as
        /// `corpseParty` (simulation.ts:264/275, chaos.ts:111).
        /// </summary>
        public static bool Static(string detail,double radius)=>detail=="static"||detail=="chip"&&Math.Abs(radius-3)<1e-3;
        /// <summary>
        /// The source message (tag) and quality of the blast for a Unity Explosion event (the ChipBlast/AbilityBlast effect in
        /// Detail), as simulation.ts explode() emits it and event-vfx.ts blastEvent draws it: kind 'static' is an `arc` (shock,
        /// .6 quality), 'blockPulse' its pulse (nova with Supernova, otherwise kinetic), a bare weapon 'blast' an explosion with
        /// no message (fire, .55), and any other kind an explosion whose message is its fx tag or kind (.5 for flame-lance).
        /// weapon: Detail names a weapon spec (the fire weapon's blast, NativeProjectiles). Unity's own names map to the source
        /// kinds (dash-blast: dashBlast, leet-block: root).
        /// </summary>
        public static string ExplosionTag(string detail,double radius,bool weapon,out double quality)
        {
            quality=1;
            if(string.IsNullOrEmpty(detail)||weapon){quality=UntaggedQuality;return null;}
            if(Static(detail,radius)){quality=ArcQuality;return "static";}
            switch(detail)
            {
                case "chip":return "corpseParty";
                case "block-pulse":return "kinetic";
                case "supernova":return "nova";
                case "dash-blast":return "dashBlast";
                case "leet-block":return "root";
                default:quality=Quality(detail);return detail;
            }
        }
    }

    /// <summary>
    /// Per-frame rules the blast presentation applies to Unity's effect records before they reach the sim: how far the
    /// blasts advance, duplicate records of one source event, and which records belong to the Siren.
    /// </summary>
    public static class NativeBlastFrame
    {
        /// <summary>Records closer than this (metres) are at the same spot: the match writes both from one Vector3.</summary>
        public const double SameSpot=.05;
        /// <summary>The source main loop caps a frame at 0.05 s before the renderer steps the blasts with it (main.ts frame()).</summary>
        public const double MaxFrame=.05;
        /// <summary>
        /// Seconds the blasts advance this frame. The source steps ChipBlasts with the render frame time, not the match clock;
        /// a guest's match clock only moves when a 20 Hz snapshot arrives. They stop only while the match itself is paused
        /// (the offline menu). An online match keeps running behind the menu, and so do its blasts.
        /// </summary>
        public static double Step(double frameSeconds,bool paused)=>paused||double.IsNaN(frameSeconds)?0:Math.Max(0,Math.Min(MaxFrame,frameSeconds));
        /// <summary>A record first seen this frame, with the cue, source tag and quality factor the presentation resolved for it.</summary>
        public struct Candidate{public double X,Y,Z,Radius,Quality;public string Tag;public NativeBlastCues.Cue Cue;}
        private static bool Same(Candidate a,Candidate b){double x=a.X-b.X,y=a.Y-b.Y,z=a.Z-b.Z;return x*x+y*y+z*z<SameSpot*SameSpot;}
        private static bool Explosive(NativeBlastCues.Cue cue)=>cue==NativeBlastCues.Cue.Blast||cue==NativeBlastCues.Cue.Dud;
        /// <summary>
        /// The match records some single source events more than once in a tick, and the source draws one cue for each:
        /// Blink Strike's teleport (ChipBlink, then Blink), a ball-lightning wall hit (its blast, then a 0.6 m hit) and an
        /// armed rocket on a summon (its blast, then the dud's 0.3 m hit). Sets Cue.None on a teleport that lands where an
        /// earlier one landed, and on a blast or dud at the spot of a larger one with the same source tag (the earlier one
        /// wins a tie). Untagged records are kept, because distinct chip blasts can share a spot.
        /// </summary>
        public static void Collapse(List<Candidate> fresh)
        {
            for(int i=0;i<fresh.Count;i++)
            {
                var a=fresh[i];
                if(a.Cue==NativeBlastCues.Cue.Teleport){for(int j=0;j<i;j++)if(fresh[j].Cue==NativeBlastCues.Cue.Teleport&&Same(a,fresh[j])){a.Cue=NativeBlastCues.Cue.None;break;}}
                else if(Explosive(a.Cue)&&a.Tag!=null)for(int j=0;j<fresh.Count;j++)
                {
                    var b=fresh[j];if(j==i||!Explosive(b.Cue)||b.Tag!=a.Tag||!Same(a,b))continue;
                    if(b.Radius>a.Radius||b.Radius==a.Radius&&j<i){a.Cue=NativeBlastCues.Cue.None;break;}
                }
                fresh[i]=a;
            }
        }
        /// <summary>
        /// Where the Siren's own attacks land: under a Siren pilot (slam), on its shots (seekers, rain drops, damnation), on
        /// its fields (eruption) and on the Siren stage's vents. A Below site matches on the ground plane under its centre.
        /// </summary>
        public struct SirenSite{public double X,Y,Z,Reach;public bool Below;}
        /// <summary>How far under a Below site's centre a record must lie (the slam lands on the ground under the body).</summary>
        public const double BelowDepth=.3;
        public static bool AtSiren(List<SirenSite> sites,double x,double y,double z)
        {
            for(int i=0;i<sites.Count;i++)
            {
                var s=sites[i];double dx=x-s.X,dy=y-s.Y,dz=z-s.Z,r=s.Reach*s.Reach;
                if(s.Below?dx*dx+dz*dz<=r&&dy<=-BelowDepth:dx*dx+dy*dy+dz*dz<=r)return true;
            }
            return false;
        }
    }
}
