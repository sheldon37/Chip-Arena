using System;
namespace ChipArena.UnityConversion
{
    // client/pve/gore.ts: Ward Zero adds bleed black ink and throw a tumbling limb chunk when a limb is severed (pve-gib).
    // Pure data and pure functions of the event time, so a chunk is rewind-safe and identical on every client. Coordinates
    // are the source's (three.js, right-handed); the runtime mirrors x into Unity space (CapturedRig.Mirror). The
    // black-flame FlameField puffs and stump drips of gore.ts belong to the Siren flame port (C12) and are not here.
    public static class NativeGoreRules
    {
        // gore.ts:15 GORE: at most 12 live chunks, each 4 s; BurstPool ink is capped here (the source pool grows).
        public const int ChunkMax=12,DropMax=192;public const double ChunkLife=4,Gravity=9.8;
        public const int Ink=0x0c0a10;
        // gore.ts:38 slot colours: slot i%3==0 rotOlive 0x8a8b6b, the others shell 0xa9ad99, each lerped .35 toward char
        // 0x0d0b0e in linear space (three Color.lerp), written back as sRGB.
        public const int ChunkOlive=0x717258,ChunkShell=0x8b8f7e;
        // shared/pve/contract.ts LIMB bit order (bit i = 1<<i).
        public static readonly string[] Limbs={"head","lArm","rArm","lFore","rFore","lLeg","rLeg","lShin","rShin"};
        private static readonly uint[] LimbHashes=MakeLimbHashes();
        // gore.ts:18-19 CHUNK box sizes (m) per limb.
        private static readonly double[] Sizes={.3,.3,.28, .16,.5,.17, .16,.5,.17, .15,.34,.16, .15,.34,.16, .19,.52,.21, .19,.52,.21, .17,.4,.19, .17,.4,.19};
        // shared/pve/gibs.ts:18-19 JOINT (body-local, x left) of each limb's part: head, shoulders, elbows, hips, knees.
        private static readonly double[] Joints={0,.6,0, .3,.28,0, -.3,.28,0, .36,-.05,0, -.36,-.05,0, .14,-.33,0, -.14,-.33,0, .14,-.66,0, -.14,-.66,0};
        private static uint[] MakeLimbHashes(){var h=new uint[Limbs.Length];for(int i=0;i<h.Length;i++)h[i]=Fnv(Limbs[i]);return h;}

        // siren/minions/look.ts:8-11 hash01: a seed and up to three integers to [0,1).
        public static double Hash01(uint seed,int a=0,int b=0,int c=0)
        {
            unchecked
            {
                uint h=seed^((uint)a+0x632be5abu)*0x9e3779b1u^((uint)b+0x85157af5u)*0x85ebca77u^((uint)c+0x2545f491u)*0xc2b2ae3du;
                h=(h^(h>>16))*0x7feb352du;h=(h^(h>>15))*0x846ca68bu;return (h^(h>>16))/4294967296.0;
            }
        }
        // gore.ts:80 hashId (FNV-1a with Math.imul).
        public static uint Fnv(string s){unchecked{uint h=2166136261;if(s!=null)for(int i=0;i<s.Length;i++)h=(h^s[i])*16777619;return h;}}
        // Fnv of the invariant decimal text of `value`, without allocating it.
        public static uint FnvDigits(int value)
        {
            unchecked
            {
                uint h=2166136261;long v=value;if(v<0){h=(h^'-')*16777619;v=-v;}long p=1;while(p*10<=v)p*=10;
                for(;p>0;p/=10){h=(h^(uint)('0'+v/p%10))*16777619;}return h;
            }
        }
        public static int DigitCount(int value){long v=Math.Abs((long)value);int n=value<0?2:1;while(v>=10){v/=10;n++;}return n;}
        public static uint LimbHash(int limb)=>LimbHashes[limb>=0&&limb<LimbHashes.Length?limb:3];
        // gore.ts:53 seed: (floor(time*1000) ^ hashId(target) ^ hashId(message)) >>> 0.
        public static uint GibSeed(double time,uint target,uint message){unchecked{return (uint)(long)Math.Floor(time*1000)^target^message;}}
        // JS Math.round.
        public static int Round(double x)=>(int)Math.Floor(x+.5);
        private static double Clamp(double x,double a,double b)=>Math.Max(a,Math.Min(b,x));
        // gore.ts:53-54 ink spray: round(10+8k) with k = clamp(quality,.3,1); the BurstPool gets ceil(spray*.6) drops.
        public static int Spray(float quality)=>Round(10+8*Clamp(quality,.3,1));
        public static int GibInk(float quality)=>(int)Math.Ceiling(Spray(quality)*.6);
        // gore.ts:44 drops(quality): 6-10 ink drops for a hit on a full-detail add.
        public static int HitInk(float quality)=>Round(6+4*Clamp(quality,0,1));
        // burst-pool.ts:10 spawns `count*quality` drops (for i < count*quality).
        public static int Spawned(int count,float quality){double n=count*(double)quality;return n<=0?0:(int)Math.Ceiling(n);}
        // The severed limbs of a loss: every new bit except a forearm/shin whose upper limb is lost with it (gibs.ts
        // SEVER_BITS: one pve-gib event, and so one chunk, per severed part).
        public static bool RootLimb(int fresh,int limb)
        {
            if((fresh&1<<limb)==0)return false;int parent=limb==3?1:limb==4?2:limb==7?5:limb==8?6:-1;
            return parent<0||(fresh&1<<parent)==0;
        }
        // gibs.ts:23-27 jointWorld: a part's joint in the world from the body position, yaw (radians) and crawl.
        public static void Joint(int limb,double px,double py,double pz,double yaw,bool crawl,out double x,out double y,out double z)
        {
            int i=(limb>=0&&limb<9?limb:3)*3;double lx=Joints[i],ly=Joints[i+1],lz=Joints[i+2],c=Math.Cos(yaw),s=Math.Sin(yaw);
            x=px+c*lx+s*lz;y=py+(crawl?Math.Min(ly,.1)*.4:ly);z=pz-s*lx+c*lz;
        }

        public struct Chunk {public double At;public uint Seed;public int Limb;public double Px,Py,Pz,Vx,Vy,Vz,Sx,Sy,Sz,Wx,Wy,Wz;}
        // gore.ts:58-60 the chunk of one severed limb: thrown along `from` (default +z), up 2.2-3.7 m/s, hash-seeded spin.
        public static Chunk Gib(double time,uint seed,int limb,double px,double py,double pz,bool hasFrom,double fx,double fy,double fz)
        {
            if(!hasFrom){fx=0;fy=0;fz=1;}double len=Math.Sqrt(fx*fx+fy*fy+fz*fz);if(!(len>0))len=1;double dx=fx/len,dz=fz/len;
            int i=limb>=0&&limb<9?limb:3,k=i*3;double sp=2.4+1.6*Hash01(seed,7);
            return new Chunk{At=time,Seed=seed,Limb=i,Px=px,Py=py,Pz=pz,Vx=dx*sp+(Hash01(seed,8)-.5)*1.2,Vy=2.2+1.5*Hash01(seed,9),Vz=dz*sp+(Hash01(seed,10)-.5)*1.2,
                Sx=Sizes[k],Sy=Sizes[k+1],Sz=Sizes[k+2],Wx=(Hash01(seed,11)-.5)*14,Wy=(Hash01(seed,12)-.5)*10,Wz=(Hash01(seed,13)-.5)*14};
        }
        // gore.ts:24-30 chunkAt: a ballistic arc that lands on the floor at y = size.y/4, then skids to a stop over .35 s.
        public static bool ChunkAt(in Chunk c,double age,out double x,out double y,out double z)
        {
            double rest=c.Sy*.25,vy=c.Vy,disc=vy*vy+2*Gravity*Math.Max(0,c.Py-rest),tl=(vy+Math.Sqrt(disc))/Gravity;
            if(age<tl){x=c.Px+c.Vx*age;z=c.Pz+c.Vz*age;y=c.Py+vy*age-.5*Gravity*age*age;return false;}
            double k=Math.Min(age-tl,.35),slide=k-k*k/(2*.35);x=c.Px+c.Vx*tl+c.Vx*.35*slide;z=c.Pz+c.Vz*tl+c.Vz*.35*slide;y=rest;return true;
        }
        // gore.ts:73-74 the pose at `time`: spin stops .6 s into the flight once landed, the last .6 s shrink to nothing.
        // False before the event and after its life.
        public static bool Pose(in Chunk c,double time,out double x,out double y,out double z,out double qx,out double qy,out double qz,out double qw,out double sx,out double sy,out double sz)
        {
            double age=time-c.At;if(age<0||age>=ChunkLife){x=y=z=qx=qy=qz=sx=sy=sz=0;qw=1;return false;}
            bool landed=ChunkAt(c,age,out x,out y,out z);double spin=landed?Math.Min(age,.6):age,shrink=1-Math.Max(0,(age-ChunkLife+.6)/.6);
            EulerXyz(c.Wx*spin,c.Wy*spin,c.Wz*spin,out qx,out qy,out qz,out qw);sx=c.Sx*shrink;sy=c.Sy*shrink;sz=c.Sz*shrink;return true;
        }
        // three.js Quaternion.setFromEuler, order 'XYZ'.
        public static void EulerXyz(double ax,double ay,double az,out double x,out double y,out double z,out double w)
        {
            double c1=Math.Cos(ax/2),c2=Math.Cos(ay/2),c3=Math.Cos(az/2),s1=Math.Sin(ax/2),s2=Math.Sin(ay/2),s3=Math.Sin(az/2);
            x=s1*c2*c3+c1*s2*s3;y=c1*s2*c3-s1*c2*s3;z=c1*c2*s3+s1*s2*c3;w=c1*c2*c3-s1*s2*s3;
        }
        // HordeGore.list: push and shift past 12 (gore.ts:61); update keeps chunks younger than their life and not more
        // than .5 s in the future (a rewind drops them, gore.ts:71). Draw order = list order, skipping future chunks.
        public sealed class ChunkList
        {
            private readonly Chunk[] items=new Chunk[ChunkMax];public int Count{get;private set;}
            public Chunk this[int i]=>items[i];
            public void Add(in Chunk c){if(Count==ChunkMax){Array.Copy(items,1,items,0,ChunkMax-1);Count--;}items[Count++]=c;}
            public void Update(double time){int n=0;for(int i=0;i<Count;i++){var c=items[i];if(time-c.At<ChunkLife&&time>=c.At-.5)items[n++]=c;}Count=n;}
            public int Live(double time){int n=0;for(int i=0;i<Count;i++)if(time>=items[i].At&&time-items[i].At<ChunkLife)n++;return n;}
            public void Clear()=>Count=0;
        }

        // burst-pool.ts:10,17 one ink drop: random velocity ((r-.5)P, rP, (r-.5)P), size .035-.1, life .25-.75 s, gravity 5,
        // size *= max(.93, 1-1.2 dt) per frame. Here the randoms are hashed from the event (seed, index) and the motion is
        // the closed form of that integration at 60 frames per second, so drops are pure in time as well.
        public struct Drop {public double At,Px,Py,Pz,Vx,Vy,Vz,Scale,Life;}
        public static Drop MakeDrop(double at,uint seed,int index,double px,double py,double pz,double power)
        {
            return new Drop{At=at,Px=px,Py=py,Pz=pz,Vx=(Hash01(seed,index,20)-.5)*power,Vy=Hash01(seed,index,21)*power,Vz=(Hash01(seed,index,22)-.5)*power,
                Scale=.035+Hash01(seed,index,23)*.065,Life=.25+Hash01(seed,index,24)*.5};
        }
        public static bool DropAt(in Drop d,double time,out double x,out double y,out double z,out double scale)
        {
            double age=time-d.At;if(age<0||age>=d.Life){x=y=z=scale=0;return false;}
            x=d.Px+d.Vx*age;y=d.Py+d.Vy*age-2.5*age*age;z=d.Pz+d.Vz*age;scale=d.Scale*Math.Pow(.98,60*age);return true;
        }
        // The drop pool: oldest first; past DropMax the oldest drop goes.
        public sealed class DropList
        {
            private readonly Drop[] items=new Drop[DropMax];public int Count{get;private set;}
            public Drop this[int i]=>items[i];
            public void Add(in Drop d){if(Count==DropMax){Array.Copy(items,1,items,0,DropMax-1);Count--;}items[Count++]=d;}
            public void Update(double time){int n=0;for(int i=0;i<Count;i++){var d=items[i];if(time-d.At<d.Life&&time>=d.At-.5)items[n++]=d;}Count=n;}
            public void Clear()=>Count=0;
        }
    }
}
