using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Per-id memory that the projectile models (NativeShotPresentation) share with their halos, lights and trails
    /// (NativeCombatGlow). Cosmetic only.
    /// <para>Identity: what a projectile looked like when it was first seen. That is every NativeShotView field except its
    /// position, velocity, radius and fuse. The source builds a shot's mesh once, at creation, and a block reflect changes
    /// only shot.owner (shared/simulation.ts reflectProjectile), so p.spec, p.arcade, p.elements and p.mods never change.
    /// The port derives some of these from the current owner, so without this memory a reflected shot would take on the
    /// reflector's look.</para>
    /// <para>Replica motion (guest, killcam): the positions of the frames each shot was seen in. The shot is then drawn at
    /// the snapshot render clock, as client/snapshots.ts SnapshotBuffer.sample does: at mix(a, b) when it is in both
    /// frames around the cursor, otherwise where the newest frame has it (see Present for the time past the newest frame).
    /// </para>
    /// Ids that were not seen in a frame are forgotten at the next Begin. Allocation free once warm.
    /// </summary>
    public sealed class NativeShotTracker
    {
        /// <summary>SnapshotBuffer keeps the newest 40 samples; a shot keeps its newest 16 (0.8 s of 20 Hz frames).</summary>
        public const int Frames=40,Samples=16;
        /// <summary>NETWORK.extrapolationMaxMs: how far past the newest frame a shot is carried by its velocity.</summary>
        public const double ExtrapolationMax=.08;
        private sealed class Track
        {
            public NativeShotView Identity;public int Frame,Count,Head;public double FirstSeen;public NativeShotLaunch.State Launch;
            public readonly double[] T=new double[Samples],M=new double[Samples*6];
            public void Reset(){Identity=default;Count=Head=0;FirstSeen=double.NaN;Launch=default;}
            public double Newest=>Count==0?double.NaN:T[(Head+Count-1)%Samples];
            public void Push(double time,in NativeShotView v)
            {
                int i;if(Count<Samples)i=(Head+Count++)%Samples;else{i=Head;Head=(Head+1)%Samples;}
                T[i]=time;int k=i*6;M[k]=v.X;M[k+1]=v.Y;M[k+2]=v.Z;M[k+3]=v.VX;M[k+4]=v.VY;M[k+5]=v.VZ;
            }
            public int Find(double time){for(int n=Count-1;n>=0;n--){int i=(Head+n)%Samples;if(T[i]==time)return i;}return -1;}
        }
        private readonly Dictionary<long,Track> tracks=new Dictionary<long,Track>();private readonly Stack<Track> spare=new Stack<Track>();private readonly List<long> stale=new List<long>();
        private readonly double[] frames=new double[Frames];private int frameHead,frameCount;
        private object owner;private int generation=int.MinValue,frame;private bool replica;
        /// <summary>
        /// The gun muzzles drawn this frame (seat id to world point): the presentation records each posed gun's crown (the
        /// first-person view weapon's mapped into the match camera) before the rounds are drawn; a round first seen this frame
        /// leaves from its firer's (NativeShotLaunch). The presentation clears it at the start of each frame.
        /// </summary>
        public readonly Dictionary<int,System.Numerics.Vector3> Muzzles=new Dictionary<int,System.Numerics.Vector3>();
        /// <summary>The statsFor(mods) module inputs per distinct chip set (NativeShotViews.Modules).</summary>
        public readonly Dictionary<long,NativeShotModules> ModuleCache=new Dictionary<long,NativeShotModules>();
        public int Live=>tracks.Count;
        public int FrameCount=>frameCount;
        public void Clear()
        {
            foreach(var t in tracks.Values){t.Reset();spare.Push(t);}tracks.Clear();frameHead=frameCount=0;owner=null;generation=int.MinValue;
            if(ModuleCache.Count>256)ModuleCache.Clear();
        }
        private double FrameAt(int i)=>frames[(frameHead+i)%Frames];
        private double NewestFrame=>frameCount==0?double.NaN:FrameAt(frameCount-1);
        /// <summary>
        /// Starts a render frame, once for both drawers. arena and generation: a different arena or generation clears
        /// everything. On a replica, frameTime is the time of the newest frame applied (arena.Time). A frame time that runs
        /// backwards also clears everything.
        /// </summary>
        public void Begin(object arena,int generation,bool replica,double frameTime)
        {
            if(!ReferenceEquals(arena,owner)||generation!=this.generation||replica!=this.replica||replica&&frameTime<NewestFrame)
            {Clear();owner=arena;this.generation=generation;this.replica=replica;}
            stale.Clear();foreach(var pair in tracks)if(pair.Value.Frame!=frame)stale.Add(pair.Key);
            foreach(long key in stale){var t=tracks[key];tracks.Remove(key);t.Reset();spare.Push(t);}
            frame++;
            if(!replica||double.IsNaN(frameTime)||frameCount>0&&frameTime==NewestFrame)return;
            if(frameCount<Frames)frames[(frameHead+frameCount++)%Frames]=frameTime;else{frames[frameHead]=frameTime;frameHead=(frameHead+1)%Frames;}
        }
        /// <summary>
        /// For an id already seen, puts its first identity into v and keeps v's position, velocity, radius and fuse.
        /// Returns false for a new id; the caller then builds the identity and calls Remember.
        /// </summary>
        public bool Recall(ref NativeShotView v)
        {
            if(!tracks.TryGetValue(NativeShotScene.Key(v.Kind,v.Id),out var t))return false;
            var m=t.Identity;m.X=v.X;m.Y=v.Y;m.Z=v.Z;m.VX=v.VX;m.VY=v.VY;m.VZ=v.VZ;m.Radius=v.Radius;m.FuseLeft=v.FuseLeft;v=m;t.Frame=frame;return true;
        }
        /// <summary>
        /// A gun round seen for the first time (Remember first): drawn from its firer's muzzle this frame when there is one,
        /// joining its simulated path from o (the simulation's start). Nothing changes without a recorded muzzle.
        /// </summary>
        public bool Launch(in NativeShotView v,int firer,double ox,double oy,double oz)
        {
            if(!NativeShotLaunch.Gun(v)||!Muzzles.TryGetValue(firer,out var m)||!tracks.TryGetValue(NativeShotScene.Key(v.Kind,v.Id),out var t)||t.Launch.Live)return false;
            return NativeShotLaunch.Begin(m.X,m.Y,m.Z,ox,oy,oz,v.VX,v.VY,v.VZ,out t.Launch);
        }
        /// <summary>Moves v onto its drawn launch path (after Present); unchanged for a round without one.</summary>
        public void Launched(ref NativeShotView v)
        {
            if(v.Kind!=NativeShotKind.Bullet||!tracks.TryGetValue(NativeShotScene.Key(v.Kind,v.Id),out var t)||!t.Launch.Live)return;
            NativeShotLaunch.Apply(ref t.Launch,ref v);
        }
        public void Remember(in NativeShotView v)
        {
            long key=NativeShotScene.Key(v.Kind,v.Id);if(!tracks.TryGetValue(key,out var t)){t=spare.Count>0?spare.Pop():new Track();t.Reset();tracks.Add(key,t);}
            t.Identity=v;t.Frame=frame;
        }
        /// <summary>
        /// Replicas only: records v (the newest frame's position and velocity) once per frame, then moves v to the
        /// SnapshotBuffer position at clock. The host's v is left as it is. Past the newest frame (a guest's buffer
        /// underrun, and always in the killcam, which applies the frame before its clock) the shot is carried by its
        /// velocity for at most 80 ms. SnapshotBuffer does that only for shots already in its oldest sample (2 s back)
        /// and otherwise leaves them at the newest frame; the killcam's sampleReplay mixes towards the next frame, which
        /// a replica arena does not hold.
        /// </summary>
        public void Present(ref NativeShotView v,double clock)
        {
            if(!replica||frameCount==0||!tracks.TryGetValue(NativeShotScene.Key(v.Kind,v.Id),out var t))return;
            double newest=NewestFrame;
            if(!(t.Newest==newest)){if(double.IsNaN(t.FirstSeen))t.FirstSeen=newest;t.Push(newest,v);}
            // SnapshotBuffer.sample: fewer than two samples shows the current state.
            if(frameCount<2)return;
            var m=t.M;int kb=t.Find(newest)*6;
            if(clock>newest)
            {
                double ahead=Math.Min(ExtrapolationMax,clock-newest);
                v.X=m[kb]+m[kb+3]*ahead;v.Y=m[kb+1]+m[kb+4]*ahead;v.Z=m[kb+2]+m[kb+5]*ahead;return;
            }
            double a=FrameAt(0),b=newest;
            for(int i=1;i<frameCount;i++){double f=FrameAt(i);if(f>=clock){a=FrameAt(i-1);b=f;break;}}
            // Mixed only when the shot is in both frames. The tracker drops an id once a frame lacks it, so the shot was
            // in frame a if it was first seen at or before a.
            if(!(t.FirstSeen<=a))return;int ia=t.Find(a),ib=t.Find(b);if(ia<0||ib<0)return;
            double mix=Math.Min(1,Math.Max(0,(clock-a)/Math.Max(.00001,b-a)));int ka=ia*6;kb=ib*6;
            v.X=m[ka]+(m[kb]-m[ka])*mix;v.Y=m[ka+1]+(m[kb+1]-m[ka+1])*mix;v.Z=m[ka+2]+(m[kb+2]-m[ka+2])*mix;
            v.VX=m[ka+3]+(m[kb+3]-m[ka+3])*mix;v.VY=m[ka+4]+(m[kb+4]-m[ka+4])*mix;v.VZ=m[ka+5]+(m[kb+5]-m[ka+5])*mix;
        }
    }
}
