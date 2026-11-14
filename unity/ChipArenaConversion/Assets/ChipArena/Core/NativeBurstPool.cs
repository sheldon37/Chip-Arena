using System;

namespace ChipArena.UnityConversion
{
    /// <summary>mulberry32: a small seeded generator for cosmetic randomness (repeatable runs and source comparisons).</summary>
    public sealed class NativeShotRandom
    {
        private uint state;
        public NativeShotRandom(uint seed=0xc41b){state=seed==0?1u:seed;}
        public double Next()
        {
            unchecked
            {
                state+=0x6d2b79f5u;uint t=state;
                t=(t^(t>>15))*(t|1u);t^=t+(t^(t>>7))*(t|61u);
                return (t^(t>>14))/4294967296.0;
            }
        }
    }

    /// <summary>
    /// Pure port of the source's short-lived voxel sparks (client/burst-pool.ts) and transient beams (EventVfx.beam,
    /// event-vfx.ts). Sparks are unlit axis-aligned cubes in their spawn colour; beams are thin lit boxes that live
    /// 0.09 s. Both pools grow by doubling and keep their spawn order. Cosmetic only. Positions are source numbers.
    /// </summary>
    public sealed class NativeBurstPool
    {
        public const int InitialCapacity=180,BeamCapacity=64;
        public const double BeamLife=.09,BeamWidth=.022,Gravity=5;
        /// <summary>
        /// Doubles per spark: the source's position xyz (0), velocity xyz (3), edge (6), life left (7), linear rgb (8); then,
        /// beyond the source (owner 2026-10-11 effects overhaul, for a velocity-stretched streak draw): its draw role, always
        /// <see cref="NativeFxRole.Spark"/> (11), a stable seed counted from 0 after each Clear (12), its life at spawn (13) and
        /// its normalized age u in 0..1 (14). None of these touches a random draw, so the sparks stay the source's.
        /// </summary>
        public const int SparkStride=15;
        public const int SparkRoleAt=11,SparkSeedAt=12,SparkLifeAt=13,SparkAgeAt=14;
        /// <summary>Doubles per beam: position xyz, velocity xyz, scale xyz, rotation xyzw, life left, linear rgb.</summary>
        public const int BeamStride=17;
        public int Count{get;private set;}public int Capacity{get;private set;}public int Beams{get;private set;}
        public double[] Sparks{get;private set;}public double[] BeamData{get;private set;}
        private Func<double> random;private int nextSeed;
        public NativeBurstPool(Func<double> random=null){this.random=random??new NativeShotRandom().Next;Capacity=InitialCapacity;Sparks=new double[Capacity*SparkStride];BeamData=new double[BeamCapacity*BeamStride];}
        public void Reseed(Func<double> random){this.random=random??new NativeShotRandom().Next;}
        /// <summary>BurstPool.spawn: count*quality sparks (the source loop runs while i &lt; count*quality).</summary>
        public void Spawn(double x,double y,double z,int hex,double count,double power,double quality)
        {
            NativeBlastSim.Linear(hex,out double r,out double g,out double b);
            for(int i=0;i<count*quality;i++)
            {
                if(Count>=Capacity)Grow();
                int k=Count++*SparkStride;var s=Sparks;
                s[k]=x;s[k+1]=y;s[k+2]=z;s[k+3]=(random()-.5)*power;s[k+4]=random()*power;s[k+5]=(random()-.5)*power;
                s[k+6]=.035+random()*.065;s[k+7]=.25+random()*.5;s[k+8]=r;s[k+9]=g;s[k+10]=b;
                s[k+SparkRoleAt]=(double)NativeFxRole.Spark;s[k+SparkSeedAt]=nextSeed;nextSeed=(nextSeed+1)&NativeBlastPool.SeedMask;s[k+SparkLifeAt]=s[k+7];s[k+SparkAgeAt]=0;
            }
        }
        private void Grow(){var next=new double[Capacity*2*SparkStride];Array.Copy(Sparks,next,Count*SparkStride);Sparks=next;Capacity*=2;}
        /// <summary>EventVfx.beam: a .022 m box from a to b, skipped when quality is 0 or less.</summary>
        public void Beam(double ax,double ay,double az,double bx,double by,double bz,int hex,double quality)
        {
            if(quality<=0)return;
            if(Beams*BeamStride>=BeamData.Length){var next=new double[BeamData.Length*2];Array.Copy(BeamData,next,Beams*BeamStride);BeamData=next;}
            double dx=bx-ax,dy=by-ay,dz=bz-az,l=Math.Sqrt(dx*dx+dy*dy+dz*dz);
            NativeShotMath.FromUnitVectors(0,0,1,l>0?dx/l:0,l>0?dy/l:0,l>0?dz/l:0,out double qx,out double qy,out double qz,out double qw);
            NativeBlastSim.Linear(hex,out double r,out double g,out double b);
            int k=Beams++*BeamStride;var d=BeamData;
            d[k]=(ax+bx)*.5;d[k+1]=(ay+by)*.5;d[k+2]=(az+bz)*.5;d[k+3]=d[k+4]=d[k+5]=0;d[k+6]=BeamWidth;d[k+7]=BeamWidth;d[k+8]=l;
            d[k+9]=qx;d[k+10]=qy;d[k+11]=qz;d[k+12]=qw;d[k+13]=BeamLife;d[k+14]=r;d[k+15]=g;d[k+16]=b;
        }
        /// <summary>BurstPool.update and the EventVfx particle step: age, drop (5 m/s²) and shrink by max(.93, 1-1.2dt).</summary>
        public void Update(double dt)
        {
            double shrink=Math.Max(.93,1-dt*1.2);var s=Sparks;int write=0;
            for(int i=0;i<Count;i++)
            {
                int k=i*SparkStride;double life=s[k+7]-dt;if(life<=0)continue;
                if(write!=i)Array.Copy(s,k,s,write*SparkStride,SparkStride);int w=write*SparkStride;write++;
                s[w+7]=life;s[w]+=s[w+3]*dt;s[w+1]+=s[w+4]*dt;s[w+2]+=s[w+5]*dt;s[w+4]-=dt*Gravity;s[w+6]*=shrink;s[w+SparkAgeAt]=Math.Min(1,Math.Max(0,1-life/s[w+SparkLifeAt]));
            }
            Count=write;var d=BeamData;write=0;
            for(int i=0;i<Beams;i++)
            {
                int k=i*BeamStride;double life=d[k+13]-dt;if(life<=0)continue;
                if(write!=i)Array.Copy(d,k,d,write*BeamStride,BeamStride);int w=write*BeamStride;write++;
                d[w+13]=life;d[w]+=d[w+3]*dt;d[w+1]+=d[w+4]*dt;d[w+2]+=d[w+5]*dt;d[w+4]-=dt*Gravity;d[w+6]*=shrink;d[w+7]*=shrink;d[w+8]*=shrink;
            }
            Beams=write;
        }
        /// <summary>BurstPool.clear: no sparks, back to the initial capacity.</summary>
        public void Clear(){Count=0;Beams=0;nextSeed=0;if(Capacity!=InitialCapacity){Capacity=InitialCapacity;Sparks=new double[Capacity*SparkStride];}}
    }
}
