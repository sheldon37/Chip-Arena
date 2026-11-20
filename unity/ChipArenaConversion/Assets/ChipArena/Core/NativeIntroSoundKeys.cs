using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Intros v2's generated sound keys (docs/audio/intros/README.md): the effects bank's contract for the "intro2.&lt;intro&gt;.&lt;cue&gt;"
    /// keys the intros' cues name (NativeIntroCue.Key), so an imported clip under one plays (NativeSoundKeys.Find reads this too). One
    /// line per key, on your slot; each cap at least its request's targetSeconds.max. Until a key's clips are imported its cue plays its
    /// Alt bank key or its built-in Clip. Presentation only.
    /// </summary>
    public static class NativeIntroSoundKeys
    {
        public const string Prefix="intro2.";
        private const NativeSoundSpace F=NativeSoundSpace.Flat;private const NativeSoundPriority H=NativeSoundPriority.High,N=NativeSoundPriority.Normal;
        private static NativeSoundKeys.Def K(string key,float cap,NativeSoundPriority priority=NativeSoundPriority.Normal)=>new NativeSoundKeys.Def{Key=key,Space=F,Cap=cap,Priority=priority};
        public static readonly NativeSoundKeys.Def[] All={
            // ---- I0: the showcase (no request: it plays its Alt and Clip sounds) ----
            //
            // ---- I1 (dev) ----
            //
            // ---- I2 (generic) ----
            //
            // ---- I3 (melee) ----
            //
            // ---- I4 (ranged) ----
            //
            // ---- I5 (elemental) ----
            //
        };
        private static Dictionary<string,int> index;
        public static bool Find(string key,out NativeSoundKeys.Def def)
        {
            def=default;if(key==null||!key.StartsWith(Prefix,StringComparison.Ordinal))return false;
            if(index==null){var map=new Dictionary<string,int>(StringComparer.Ordinal);for(int i=0;i<All.Length;i++)map[All[i].Key]=i;index=map;}
            if(index.TryGetValue(key,out int at)){def=All[at];return true;}return false;
        }
    }
}
