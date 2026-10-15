using System;
using System.Collections.Generic;
using System.Linq;
namespace ChipArena.UnityConversion
{
    public static class NativeExpeditionParty
    {
        public const int Capacity=4;
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
}
