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
            K("intro2.sundown.wind",3.6f),
            K("intro2.sundown.step",0.45f),
            K("intro2.sundown.toll",3.6f),
            K("intro2.sundown.crack",0.9f),
            K("intro2.sundown.collapse",2.2f),
            K("intro2.sundown.draw",0.6f),
            K("intro2.sundown.shot",2f),
            K("intro2.sundown.whiz",0.8f),
            K("intro2.sundown.ring",2.4f),
            K("intro2.sundown.bell",2.6f),
            K("intro2.sundown.thud",1f),
            K("intro2.sundown.roll",1.6f),
            K("intro2.sundown.blow",0.8f),
            K("intro2.sundown.ting",1.8f),
            K("intro2.onebreath.rain",6f),
            K("intro2.onebreath.thunder",3.5f),
            K("intro2.onebreath.drone",2.2f),
            K("intro2.onebreath.glint",0.6f),
            K("intro2.onebreath.lock",0.6f),
            K("intro2.onebreath.inhale",1.3f),
            K("intro2.onebreath.heart",0.55f),
            K("intro2.onebreath.shot",2.4f),
            K("intro2.onebreath.crack",0.8f),
            K("intro2.onebreath.hit",1.2f),
            K("intro2.onebreath.fall",2.2f),
            K("intro2.onebreath.case",0.6f),
            K("intro2.onebreath.hiss",1.2f),
            K("intro2.onebreath.crash",2.2f),
            K("intro2.onebreath.rise",0.8f),
            K("intro2.brass.belt",1f),
            K("intro2.brass.flood",1.2f),
            K("intro2.brass.hum",3f),
            K("intro2.brass.boot",0.5f),
            K("intro2.brass.rack",0.8f),
            K("intro2.brass.round",0.35f),
            K("intro2.brass.impacts",1.2f),
            K("intro2.brass.casings",2.4f),
            K("intro2.brass.drum",1f),
            K("intro2.brass.crate",1f),
            K("intro2.brass.dry",0.45f),
            K("intro2.brass.hiss",2.4f),
            K("intro2.brass.tink",0.6f),
            K("intro2.brass.name",1.4f),
            K("intro2.lightfall.grove",6f),
            K("intro2.lightfall.nock",0.8f),
            K("intro2.lightfall.seat",0.4f),
            K("intro2.lightfall.draw",1.3f),
            K("intro2.lightfall.gather",1.8f),
            K("intro2.lightfall.full",0.8f),
            K("intro2.lightfall.release",1.4f),
            K("intro2.lightfall.bloom",2.4f),
            K("intro2.lightfall.rain",1.6f),
            K("intro2.lightfall.land",1f),
            K("intro2.lightfall.lantern",0.8f),
            K("intro2.lightfall.name",1.8f),
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
