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
            K("intro2.fire.rumble",1.9f,H),
            K("intro2.fire.crack",1.4f,H),
            K("intro2.fire.erupt",2.1f,H),
            K("intro2.fire.column",2.7f),
            K("intro2.fire.step",0.6f),
            K("intro2.fire.crown",1.2f,H),
            K("intro2.fire.slam",1.3f,H),
            K("intro2.fire.name",1.5f),
            K("intro2.water.drips",1.6f),
            K("intro2.water.rise",1.6f,H),
            K("intro2.water.rear",1.3f,H),
            K("intro2.water.part",1.7f,H),
            K("intro2.water.freeze",1.5f,H),
            K("intro2.water.settle",0.9f),
            K("intro2.water.name",2.1f),
            K("intro2.storm.bed",2.6f),
            K("intro2.storm.charge",1.3f,H),
            K("intro2.storm.gather",0.7f,H),
            K("intro2.storm.strike",1.5f,H),
            K("intro2.storm.after",1.7f),
            K("intro2.storm.crackle",1f),
            K("intro2.storm.call",1.2f,H),
            K("intro2.storm.name",2.1f),
            K("intro2.void.wind",1.6f),
            K("intro2.void.pour",1.9f,H),
            K("intro2.void.totality",1.7f,H),
            K("intro2.void.emerge",1.1f,H),
            K("intro2.void.eye",1f,H),
            K("intro2.void.wings",1f),
            K("intro2.void.snap",0.8f,H),
            K("intro2.void.name",1.7f),
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
