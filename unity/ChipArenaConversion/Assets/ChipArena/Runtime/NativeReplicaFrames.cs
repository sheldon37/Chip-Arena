namespace ChipArena.UnityConversion
{
    // Read-only access to the replicated frames a guest still buffers (Runtime/NativeReplicaMotion.cs: replicaHistory, oldest
    // first), for presentation that follows its own snapshots: the horde's presented roots (Core/NativeHordeFlow,
    // docs/ZOMBIE-MOTION.md) take every add's position and yaw at each frame's authority time instead of the straight lerp the
    // bodies are placed on. Nothing here changes the buffer, the snapshot clock, the bodies or the network; it allocates nothing.
    public sealed partial class NativeArena
    {
        /// <summary>The replicated frames buffered (a guest; 0 on the host).</summary>
        public int ReplicaFrameCount=>Replica?replicaHistory.Count:0;
        /// <summary>Buffered frame `i`'s authority time.</summary>
        public double ReplicaFrameTime(int i)=>replicaHistory[i].time;
        /// <summary>Buffered frame `i`'s minions (the wire's records: id, position, yaw, ...). Read only.</summary>
        public NativeMinionFrame[] ReplicaFrameMinions(int i)=>replicaHistory[i].minions;
    }
}
