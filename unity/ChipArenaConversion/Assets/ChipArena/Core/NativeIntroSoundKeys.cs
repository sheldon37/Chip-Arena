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
