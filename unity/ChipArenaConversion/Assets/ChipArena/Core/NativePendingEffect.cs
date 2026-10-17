using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // Account-scoped pending choice, matching client/kill-effect-sync.ts. This never grants access.
    [Serializable] public sealed class NativePendingEffect
    {
        public string account,selected,remote;
        public static NativePendingEffect[] Remember(NativePendingEffect[] entries,string account,string selected,string remote,Func<string,bool> allowed)
        {
            var list=(entries??Array.Empty<NativePendingEffect>()).Where(e=>e!=null&&e.account!=account).Take(15).ToList();
            if(allowed(selected)&&selected!=remote)list.Add(new NativePendingEffect{account=account,selected=selected,remote=remote});return list.ToArray();
        }
        public static string Restore(NativePendingEffect[] entries,string account,string remote,Func<string,bool> allowed)
        {
            var entry=entries?.FirstOrDefault(e=>e!=null&&e.account==account);if(entry==null)return remote;
            if(!allowed(entry.selected)||entry.remote==null||remote!=entry.remote||remote==entry.selected){entry.account=null;return remote;}return entry.selected;
        }
    }
}
