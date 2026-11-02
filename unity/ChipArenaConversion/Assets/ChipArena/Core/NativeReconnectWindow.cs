namespace ChipArena.UnityConversion
{
    /// <summary>Bounded automatic seat recovery, shorter than the host's 90-second reservation.</summary>
    public sealed class NativeReconnectWindow
    {
        public bool Active { get; private set; }
        private double until,next;
        private int attempts;
        public void Begin(double now)
        {if(Active)return;Active=true;until=now+75;next=now+1;attempts=0;}
        public bool Expired(double now)=>Active&&now>=until;
        public bool TryAttempt(double now,bool busy)
        {
            if(!Active||Expired(now)||busy||now<next)return false;
            attempts++;next=now+System.Math.Min(5,attempts*2);return true;
        }
        public void Reset(){Active=false;until=next=0;attempts=0;}
    }
}
