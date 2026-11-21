namespace ChipArena.UnityConversion
{
    // Replenished only by a validated, ordered authority frame, never by heartbeat
    // traffic. A stalled host cannot drain 60 Hz input, even when its socket is alive.
    public sealed class NativeInputSendWindow
    {
        public const int Capacity=32;
        private int remaining;
        public void Reset()=>remaining=0;
        public void AuthorityFrame()=>remaining=Capacity;
        public bool TrySend(){if(remaining==0)return false;remaining--;return true;}
    }
}
