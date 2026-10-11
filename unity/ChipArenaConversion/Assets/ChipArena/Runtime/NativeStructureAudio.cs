using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // §3.11 destruction cues on NativeAudio's existing 24 voices (no new AudioSource): Core NativeStructureSounds plans the
    // layers, at most six of them sound at once (the rubble voice cap), panned and dulled with distance from the listener,
    // delayed with PlayDelayed so a cue's layers (the chimney's creak, whoosh and four thuds) keep their timing. Synthesized
    // clips load from Resources/StructureAudio (tools/synth_structure_audio.py, asset-locked); "wood", "metal" and "dash" come
    // from the source bank, whose 83-clip gate is untouched.
    // Event hook, once Core/ArenaModel.cs has the structure kinds: in EventSounds, before the NativeEventSounds.Route call, add
    //   if(e.Kind==EventKind.StructureStress){StructureEvent(NativeStructureCue.Stress,e);return;}
    //   if(e.Kind==EventKind.StructureFall){StructureEvent(NativeStructureCue.Fall,e);return;}
    //   if(e.Kind==EventKind.StructureLand){StructureEvent(NativeStructureCue.Land,e);return;}
    public sealed partial class NativeAudio
    {
        private readonly NativeStructureLayer[] structureLayers=new NativeStructureLayer[NativeStructureSounds.MaxLayers];
        private AudioClip[] structureClip;private double[] structureEnd;private readonly Dictionary<string,NativeStructureMaterial> structureFalls=new Dictionary<string,NativeStructureMaterial>();
        private AudioClip StructureClip(string id)
        {
            if(!id.StartsWith(NativeStructureSounds.Prefix,StringComparison.Ordinal))return Clip("audio/"+id);
            if(!clips.TryGetValue(id,out var c)){c=Resources.Load<AudioClip>(NativeStructureSounds.Folder+id.Substring(NativeStructureSounds.Prefix.Length));clips[id]=c;}return c;
        }
        /// <summary>Plays a destruction cue at <paramref name="at"/> after <paramref name="delay"/> seconds (level: stress or energy 0..1).</summary>
        public void StructureCue(NativeStructureSound cue,Vector3 at,NativeStructureMaterial material,float level,float delay=0)
        {
            if(Muted||Effects<=0||voices==null)return;if(structureClip==null||structureClip.Length!=voices.Length){structureClip=new AudioClip[voices.Length];structureEnd=new double[voices.Length];}
            int n=NativeStructureSounds.Plan(cue,material,level,structureLayers);var d=at-listener;float distance=d.magnitude,pan=NativeEventSounds.Pan(NativeArena.Vec(d),yaw);double now=AudioSettings.dspTime;
            for(int i=0;i<n;i++)
            {
                var layer=structureLayers[i];float gain=NativeStructureSounds.Gain(layer,distance);if(gain<.01f)continue;var clip=StructureClip(layer.Clip);if(clip==null)continue;
                int busy=0,free=-1;for(int v=0;v<voices.Length;v++){if(voices[v].isPlaying){if(structureClip[v]==voices[v].clip&&now<structureEnd[v])busy++;}else if(free<0)free=v;}
                if(free<0||busy>=NativeStructureSounds.Voices)return;
                var voice=voices[free];keyedVoices[free]=false;float rate=layer.Rate>0?layer.Rate:NativeEventSounds.Rate(layer.Clip,UnityEngine.Random.value),wait=Math.Max(0,delay+layer.Delay);
                voice.clip=clip;voice.volume=Mathf.Clamp01(gain*Effects);voice.panStereo=pan;voice.pitch=rate;voice.PlayDelayed(wait);structureClip[free]=clip;structureEnd[free]=now+wait+clip.length/Math.Max(.05f,rate);
            }
        }
        /// <summary>A structure event's cue (§5.4 fields): stress groans, a fall collapses, a landing thuds by the fall's material.</summary>
        public void StructureEvent(NativeStructureCue cue,in ArenaEvent e)
        {
            string segment=e.Detail??"";if(cue==NativeStructureCue.Fall)structureFalls[segment]=NativeStructureFx.Classify(e.SurfaceId);
            if(!NativeStructureSounds.ForEvent(cue,segment,out var sound))return;structureFalls.TryGetValue(segment,out var material);
            StructureCue(sound,NativeArena.Vec(e.Position),material,cue==NativeStructureCue.Stress?.8f:cue==NativeStructureCue.Fall?Mathf.Clamp01(e.Amount/80):Mathf.Clamp01(e.Amount));
        }
    }
}
