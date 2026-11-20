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
            K("intro2.architect.strike",1.3f),
            K("intro2.architect.glitch",0.45f),
            K("intro2.architect.crack",0.9f),
            K("intro2.architect.dissolve",2.4f),
            K("intro2.architect.tear",1f),
            K("intro2.architect.presence",1.8f),
            K("intro2.architect.pulse",0.5f),
            K("intro2.architect.draft",1.6f),
            K("intro2.architect.condense",0.5f),
            K("intro2.architect.form",1f),
            K("intro2.architect.blueprint",2.6f),
            K("intro2.architect.gather",2.2f),
            K("intro2.architect.snap",0.3f),
            K("intro2.architect.inhale",0.5f),
            K("intro2.architect.lock",1.6f),
            K("intro2.architect.forge",1.5f),
            K("intro2.architect.settle",1.8f),
            //
            // ---- I2 (generic) ----
            K("intro2.cloudbreak.wind",2.85f),
            K("intro2.cloudbreak.boom",1.85f),
            K("intro2.cloudbreak.fall",2.25f),
            K("intro2.cloudbreak.flip",0.65f),
            K("intro2.cloudbreak.ignite",1.05f),
            K("intro2.cloudbreak.thrust",1.35f),
            K("intro2.cloudbreak.touch",1.05f),
            K("intro2.cloudbreak.gust",1.45f),
            K("intro2.cloudbreak.cool",1.45f),
            K("intro2.cloudbreak.sun",1.85f),
            K("intro2.cloudbreak.name",1.35f),
            K("intro2.cloudbreak.puff",0.5f),
            K("intro2.whiteout.storm",3.85f),
            K("intro2.whiteout.visor",0.65f),
            K("intro2.whiteout.heat",1.65f),
            K("intro2.whiteout.crack",1.05f),
            K("intro2.whiteout.steam",1.45f),
            K("intro2.whiteout.rise",1.05f),
            K("intro2.whiteout.pulse",1.25f),
            K("intro2.whiteout.calm",2.25f),
            K("intro2.whiteout.name",1.35f),
            K("intro2.slipstream.rush",2.85f),
            K("intro2.slipstream.burst",1.35f),
            K("intro2.slipstream.run",0.35f),
            K("intro2.slipstream.leap",0.65f),
            K("intro2.slipstream.slide",1.15f),
            K("intro2.slipstream.pass",1.65f),
            K("intro2.slipstream.stop",0.5f),
            K("intro2.slipstream.chime",1.45f),
            K("intro2.slipstream.name",1.35f),
            K("intro2.slipstream.board",1.05f),
            K("intro2.threshold.rumble",2.85f),
            K("intro2.threshold.crack",0.85f),
            K("intro2.threshold.charge",1.35f),
            K("intro2.threshold.split",1.85f),
            K("intro2.threshold.through",1.35f),
            K("intro2.threshold.step",0.5f),
            K("intro2.threshold.seal",1.25f),
            K("intro2.threshold.name",1.35f),
            K("intro2.flashbulb.crowd",3.85f),
            K("intro2.flashbulb.bulbs",0.65f),
            K("intro2.flashbulb.doors",1.05f),
            K("intro2.flashbulb.cheer",1.85f),
            K("intro2.flashbulb.photo",0.5f),
            K("intro2.flashbulb.burst",0.95f),
            K("intro2.flashbulb.confetti",1.35f),
            K("intro2.flashbulb.name",1.35f),
            K("intro2.flashbulb.shutter",0.35f),
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
