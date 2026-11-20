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
            K("intro2.stillrain.rain",3.5f),
            K("intro2.stillrain.thunder",2.85f),
            K("intro2.stillrain.stance",0.85f),
            K("intro2.stillrain.koiguchi",0.45f),
            K("intro2.stillrain.cut",0.75f),
            K("intro2.stillrain.still",1.45f),
            K("intro2.stillrain.resume",3.95f),
            K("intro2.stillrain.bamboo",0.95f),
            K("intro2.stillrain.slide",1.15f),
            K("intro2.stillrain.fall",1.05f),
            K("intro2.stillrain.click",0.55f),
            K("intro2.stillrain.name",1.85f),
            K("intro2.apex.night",7.45f),
            K("intro2.apex.hiss",1.15f),
            K("intro2.apex.coil",0.75f),
            K("intro2.apex.leap",0.85f),
            K("intro2.apex.strike",0.75f),
            K("intro2.apex.venom",1.05f),
            K("intro2.apex.rise",0.75f),
            K("intro2.apex.name",1.55f),
            K("intro2.faultline.bed",8.05f),
            K("intro2.faultline.leap",0.85f),
            K("intro2.faultline.slam",1.95f),
            K("intro2.faultline.crack",1.75f),
            K("intro2.faultline.rumble",2.25f),
            K("intro2.faultline.hiss",1.55f),
            K("intro2.faultline.lift",0.85f),
            K("intro2.faultline.name",1.75f),
            K("intro2.severeddawn.hall",7.45f),
            K("intro2.severeddawn.grip",0.65f),
            K("intro2.severeddawn.cut",0.65f),
            K("intro2.severeddawn.sever",1.45f),
            K("intro2.severeddawn.flakes",2.05f),
            K("intro2.severeddawn.ribbon",0.95f),
            K("intro2.severeddawn.name",1.75f),
            K("intro2.holdtheline.wind",7.85f),
            K("intro2.holdtheline.plant",0.95f),
            K("intro2.holdtheline.launch",1.55f),
            K("intro2.holdtheline.hit",0.95f),
            K("intro2.holdtheline.miss",1.55f),
            K("intro2.holdtheline.hit2",0.95f),
            K("intro2.holdtheline.hit3",0.95f),
            K("intro2.holdtheline.miss2",1.55f),
            K("intro2.holdtheline.hit4",0.95f),
            K("intro2.holdtheline.miss3",1.55f),
            K("intro2.holdtheline.hit5",1.05f),
            K("intro2.holdtheline.hot",1.85f),
            K("intro2.holdtheline.bash",0.95f),
            K("intro2.holdtheline.name",1.55f),
            K("intro2.afterimage.lane",7.25f),
            K("intro2.afterimage.stop",0.55f),
            K("intro2.afterimage.split",0.95f),
            K("intro2.afterimage.cut",0.55f),
            K("intro2.afterimage.echo",0.55f),
            K("intro2.afterimage.echo2",0.55f),
            K("intro2.afterimage.lanterns",1.25f),
            K("intro2.afterimage.cut2",0.55f),
            K("intro2.afterimage.join",0.95f),
            K("intro2.afterimage.name",1.35f),
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
