using System;
using System.Collections.Generic;
using System.Linq;
namespace ChipArena.UnityConversion
{
    public static class NativeExpeditionParty
    {
        public const int Capacity=4;
        // v4 adds bounded startup/loading protection before synchronous area construction.
        public const int Protocol=5;
        public static string Validate(IEnumerable<string> accounts,bool authenticated,bool ready,bool supported=true)
        {
            if(!supported)return "Every player must update to a build with Expedition co-op support.";
            var ids=accounts.ToArray();
            if(ids.Length<1||ids.Length>Capacity)return "Expedition supports one to four players.";
            if(!authenticated||ids.Any(string.IsNullOrEmpty))return "Every Expedition player must be signed in.";
            if(ids.Distinct(StringComparer.Ordinal).Count()!=ids.Length)return "Each Expedition player needs a different account.";
            return ready?null:"Wait for every pilot to ready up.";
        }
    }
    // One fixed deadline per load identity. Repeated packets cannot keep a dead peer alive forever.
    public sealed class NativeExpeditionLoadWindow
    {
        public const double Seconds=60,RetrySeconds=5;
        private string identity;private double until,next;
        public void Begin(string key,double now){if(key==null||key==identity)return;identity=key;until=now+Seconds;next=0;}
        public bool Active(double now)=>identity!=null&&now<until;
        public bool Send(double now){if(now<next)return false;next=now+RetrySeconds;return true;}
        public void Complete(){until=0;next=0;}
        public void Reset(){identity=null;Complete();}
    }

}
