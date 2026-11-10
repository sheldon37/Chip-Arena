using System;
using System.Collections.Generic;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The main menu's DEV screen (Runtime/NativeDevGrantsUI.cs): what a DEV can grant a registered player and what a save sends.
    /// The account server takes only its own keys (src/shared/access.ts GRANTABLE_CONTENT and ACCOUNT_FLAGS) and refuses a whole
    /// save that carries any other, so the screen offers every DEV class and every DEV look (a new DEV class shows up by itself)
    /// and lets a grant through only where <see cref="Server"/> has its key; the rest are marked for DEV accounts only.
    /// CoreTests --dev-grants keeps both lists equal to the server's.
    /// </summary>
    public static class NativeDevGrants
    {
        /// <summary>The account server's GRANTABLE_CONTENT keys, in its order.</summary>
        public static readonly string[] Server={"weapon:siren","intro:dev-siren","weapon:emperor","intro:dev-emperor","weapon:leet","weapon:ansatsuken","intro:dev-matrix","intro:dev-ansatsuken","hat:auditor-halo","effect:dev-singularity","effect:dev-raging-demon","effect:dev-clown","effect:dev-clown-love","effect:dev-bass-van","weapon:cyberlord","intro:dev-cyberlord","weapon:bombadier","intro:dev-bombadier","weapon:topdog","intro:dev-topdog","effect:dev-hotdog-friends"};
        /// <summary>The account server's ACCOUNT_FLAGS keys.</summary>
        public static readonly string[] Flags={"playtester","vip"};
        public static bool Grantable(string key)=>key!=null&&Array.IndexOf(Server,key)>=0;
        /// <summary>A look a grant would unlock: restricted (the dev- items and the Auditor Halo) and not one of the owner's own
        /// (the Gibber, the Sheldon Head, Intro and Idle need live DEV access itself, NativeCosmeticGate.DevOnly).</summary>
        public static bool Offered(string id)=>NativeCosmeticGate.Restricted(id)&&!NativeCosmeticGate.DevOnly(id);
        /// <summary>Whether <paramref name="key"/> is on once the DEV's changes apply: the change if there is one, else what the
        /// player has.</summary>
        public static bool On(string[] have,IReadOnlyDictionary<string,bool> changes,string key)=>changes!=null&&changes.TryGetValue(key,out bool want)?want:Has(have,key);
        public static bool Has(string[] have,string key)=>have!=null&&key!=null&&Array.IndexOf(have,key)>=0;
        /// <summary>Flips <paramref name="key"/>; a change back to what the player has is no change. A key the server would refuse
        /// cannot be turned on (it can always be turned off).</summary>
        public static void Toggle(string[] have,Dictionary<string,bool> changes,string key,string[] allowed)
        {
            if(changes==null||key==null)return;bool now=On(have,changes,key);if(!now&&Array.IndexOf(allowed,key)<0)return;
            if(!now==Has(have,key))changes.Remove(key);else changes[key]=!now;
        }
        /// <summary>Turns every key of <paramref name="keys"/> that <paramref name="allowed"/> holds on (or off).</summary>
        public static void SetAll(string[] have,Dictionary<string,bool> changes,IReadOnlyList<string> keys,bool on,string[] allowed)
        {
            if(changes==null||keys==null)return;
            for(int i=0;i<keys.Count;i++){string key=keys[i];if(key==null||Array.IndexOf(allowed,key)<0)continue;if(on==Has(have,key))changes.Remove(key);else changes[key]=on;}
        }
        /// <summary>How many keys the changes move.</summary>
        public static int Changes(string[] have,IReadOnlyDictionary<string,bool> changes)
        {
            if(changes==null)return 0;int n=0;foreach(var kv in changes)if(kv.Value!=Has(have,kv.Key))n++;return n;
        }
        /// <summary>What a save sends: the player's own keys as the server holds them, less those turned off, plus those turned on
        /// that <paramref name="allowed"/> holds. Keys the client does not know are kept, never dropped.</summary>
        public static string[] Merge(string[] have,IReadOnlyDictionary<string,bool> changes,string[] allowed)
        {
            var keys=new List<string>();
            if(have!=null)foreach(var key in have)if(key!=null&&!keys.Contains(key)&&!(changes!=null&&changes.TryGetValue(key,out bool want)&&!want))keys.Add(key);
            if(changes!=null)foreach(var kv in changes)if(kv.Value&&kv.Key!=null&&!keys.Contains(kv.Key)&&Array.IndexOf(allowed,kv.Key)>=0)keys.Add(kv.Key);
            return keys.ToArray();
        }
    }
}
