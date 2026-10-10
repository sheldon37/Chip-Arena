using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The killed summon's break-up and the hit puff (client/chaos-visuals.ts summons() and client/summon-visuals.ts
    /// SUMMON_LOOK at 05950ea), as pure rules:
    /// <list type="bullet">
    /// <item>poseSummonDeath jitters the corpse from SUMMON_LOOK.death.breakAt - .2 s: the rig group's x moves by
    /// sin(age * 90) * .03 m (source axes; Unity x is mirrored).</item>
    /// <item>At breakAt (.75 s) the rig hides and three voxel bursts fire up the body, one per gib colour (zombie 0x5d7a4c,
    /// 0x34402a, 0x8da976; toy 0xb8322c, 0xe8c25a, 0x2b2b2b) at y - .3 + i * .25 * scale (zombie .85, toy .6), 8 (toy 6)
    /// voxels at power 2.6 each, scaled by the effects quality like every BurstPool spawn. The rig is gone at .9 s.</item>
    /// <item>A minionHit on a live summon puffs 3 voxels at power 1.6 at the hit: 0xe8c25a on a toy, 0x4d6b3c on a zombie.
    /// Events older than .5 s draw nothing.</item>
    /// </list>
    /// Nothing here is read by the simulation, its timing or the network frame.
    /// </summary>
    public static class NativeSummonBreak
    {
        public const float BreakAt=.75f,Gone=.9f,ShakeLead=.2f,ShakeRate=90,ShakeAmplitude=.03f;
        public const float BurstDrop=.3f,BurstStep=.25f,BurstPower=2.6f,ZombieScale=.85f,ToyScale=.6f;
        public const int Bursts=3,ZombieBurstCount=8,ToyBurstCount=6;
        public const int ToyHitColor=0xe8c25a,ZombieHitColor=0x4d6b3c,HitCount=3;public const float HitPower=1.6f,HitFresh=.5f;
        private static readonly int[] zombieGib={0x5d7a4c,0x34402a,0x8da976},toyGib={0xb8322c,0xe8c25a,0x2b2b2b};
        /// <summary>SUMMON_LOOK[kind].gib[i].</summary>
        public static int Gib(bool toy,int i)=>(toy?toyGib:zombieGib)[i];
        /// <summary>Summons are the chaos zombie and toy soldier (ChaosVisuals draws no other minion kind).</summary>
        public static bool Summon(string kind)=>kind=="zombie"||kind=="toy";
        /// <summary>A death record's model is "summon-zombie" or "summon-toy" (NativeArena.RecordMinionDeath).</summary>
        public static bool SummonModel(string model,out bool toy)
        {toy=model=="summon-toy";return toy||model=="summon-zombie";}
        /// <summary>poseSummonDeath's jitter: the rig group's x offset (source axes) at corpse age <paramref name="age"/>.</summary>
        public static float Shake(float age)=>age>BreakAt-ShakeLead?(float)Math.Sin(age*ShakeRate)*ShakeAmplitude:0;
        /// <summary>The corpse rig shows until the break-up (group.visible = false at breakAt).</summary>
        public static bool RigShown(float age)=>age<BreakAt;
        /// <summary>Burst <paramref name="i"/> (0..2) of the break-up: its height above the death position, colour and count.</summary>
        public static void Burst(bool toy,int i,out float lift,out int color,out int count)
        {lift=-BurstDrop+i*BurstStep*(toy?ToyScale:ZombieScale);color=Gib(toy,i);count=toy?ToyBurstCount:ZombieBurstCount;}
        /// <summary>The minionHit puff colour (a summon whose kind is unknown takes the zombie's).</summary>
        public static int HitColor(bool toy)=>toy?ToyHitColor:ZombieHitColor;
        /// <summary>The source skips an event when s.time - e.time &gt; .5.</summary>
        public static bool Fresh(float now,float eventTime)=>!(now-eventTime>HitFresh);
    }

    /// <summary>
    /// Which summon deaths have broken up (the source's per-corpse `broke` flag), keyed by death id and seed, without
    /// allocating: a fixed ring of the most recent deaths. A death bursts once, on the first frame its age is in
    /// [breakAt, gone); seeing it younger than breakAt again (the killcam replaying it) re-arms it.
    /// </summary>
    public sealed class NativeSummonBreakups
    {
        public const int Capacity=64;
        private readonly int[] ids=new int[Capacity],seeds=new int[Capacity];private readonly bool[] broke=new bool[Capacity],used=new bool[Capacity];private int next;
        public void Clear(){Array.Clear(used,0,Capacity);next=0;}
        private int Find(int id,int seed){for(int i=0;i<Capacity;i++)if(used[i]&&ids[i]==id&&seeds[i]==seed)return i;return -1;}
        private int Slot(int id,int seed){int i=Find(id,seed);if(i>=0)return i;i=next;next=(next+1)%Capacity;used[i]=true;ids[i]=id;seeds[i]=seed;broke[i]=false;return i;}
        /// <summary>True exactly once per showing of a death: the frame the break-up fires.</summary>
        public bool Due(int id,int seed,float age)
        {
            if(age<NativeSummonBreak.BreakAt){int k=Find(id,seed);if(k>=0)broke[k]=false;else if(age>=0)broke[Slot(id,seed)]=false;return false;}
            if(age>=NativeSummonBreak.Gone)return false;
            int s=Slot(id,seed);if(broke[s])return false;broke[s]=true;return true;
        }
    }
}
