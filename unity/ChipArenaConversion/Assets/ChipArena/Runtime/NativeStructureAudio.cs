using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // §3.11 destruction cues on NativeAudio's existing 24 voices (no new AudioSource): Core NativeStructureSounds plans the
    // layers in priority order and Core NativeStructureMix keeps the rubble voice cap (at most six sounding at once, each layer
    // counted from its own start; one over the cap is skipped, not the rest of its cue; a collapse takes over its own segment's
    // groan voices). Layers are panned and dulled with distance from the listener and keep their offsets inside a cue with
    // PlayDelayed (the chimney's creak, whoosh and four thuds); a cue asked for later waits in a small queue until it is due, so
    // the cap sees it when it starts. Synthesized clips load from Resources/StructureAudio (tools/synth_structure_audio.py,
    // asset-locked); "wood", "metal" and "dash" come from the source bank, whose 83-clip gate is untouched.
    // NativeAudio.EventSounds routes the three authoritative structure events here before generic sound routing.
    // With the owner's effects bank (LocalAssets) each layer may take a recorded override (NativeSoundCues.Structure); the
    // ledger then counts the override's own length, and without the bank every layer is the synthesized one, as before.
    public sealed partial class NativeAudio
    {
        private struct StructurePending{public bool Live;public NativeStructureSound Cue;public Vector3 At;public NativeStructureMaterial Material;public float Level;public uint Owner;public double Due;}
        private readonly NativeStructureLayer[] structureLayers=new NativeStructureLayer[NativeStructureSounds.MaxLayers];private readonly NativeStructureMix structureMix=new NativeStructureMix();
        private readonly StructurePending[] structureQueue=new StructurePending[8];private int structureQueueGeneration=-1;
        private AudioClip[] structureClip;private readonly Dictionary<string,NativeStructureMaterial> structureFalls=new Dictionary<string,NativeStructureMaterial>();
        private AudioClip StructureClip(string id)
        {
            if(!id.StartsWith(NativeStructureSounds.Prefix,StringComparison.Ordinal))return Clip("audio/"+id);
            if(!clips.TryGetValue(id,out var c)){c=Resources.Load<AudioClip>(NativeStructureSounds.Folder+id.Substring(NativeStructureSounds.Prefix.Length));clips[id]=c;}return c;
        }
        /// <summary>Plays a destruction cue at <paramref name="at"/> (level: stress or energy 0..1) for <paramref name="owner"/>
        /// (NativeStructureSounds.Owner of the segment): now, or queued until <paramref name="delay"/> seconds from now.</summary>
        public void StructureCue(NativeStructureSound cue,Vector3 at,NativeStructureMaterial material,float level,float delay=0,uint owner=0)
        {
            if(Muted||Effects<=0||voices==null)return;
            if(delay>.02f)
            {
                double due=AudioSettings.dspTime+delay;int slot=-1;for(int i=0;i<structureQueue.Length;i++)if(!structureQueue[i].Live){slot=i;break;}if(slot<0)return;
                structureQueue[slot]=new StructurePending{Live=true,Cue=cue,At=at,Material=material,Level=level,Owner=owner,Due=due};return;
            }
            if(structureClip==null||structureClip.Length!=voices.Length)structureClip=new AudioClip[voices.Length];
            double now=AudioSettings.dspTime;int slots=Math.Min(voices.Length,NativeStructureMix.Slots);
            // Forget voices that ended or that another sound took over; a collapse silences its own segment's groan.
            for(int v=0;v<slots;v++)if(structureMix.Live(v)&&(voices[v].clip!=structureClip[v]||now>=structureMix.Start(v)+.05&&!voices[v].isPlaying))structureMix.Release(v);
            structureMix.Expire(now);for(int v=0;v<slots;v++)if(structureMix.Steals(v,owner,cue)){voices[v].Stop();structureMix.Release(v);}
            int n=NativeStructureSounds.Plan(cue,material,level,structureLayers);var d=at-listener;float distance=d.magnitude,pan=NativeEventSounds.Pan(NativeArena.Vec(d),yaw);
            for(int i=0;i<n;i++)
            {
                var layer=structureLayers[i];float gain=NativeStructureSounds.Gain(layer,distance);if(gain<.01f)continue;
                // A recorded override from the effects bank (NativeSoundCues.Structure, same plan, delays and ledger), else the synth.
                var clip=BankStructureClip(layer.Clip,out var banked);if(clip==null)clip=StructureClip(layer.Clip);if(clip==null)continue;
                float rate=banked!=null?NativeSoundBank.Rate(banked,(float)bankRandom.NextDouble()):layer.Rate>0?layer.Rate:NativeEventSounds.Rate(layer.Clip,UnityEngine.Random.value),wait=Math.Max(0,layer.Delay);double start=now+wait;
                float seconds=banked!=null?Math.Min(clip.length,banked.MaxLength):clip.length;if(banked!=null)gain*=banked.Gain;
                if(!structureMix.Fits(start,start+NativeStructureSounds.Length(seconds,rate)))continue;
                int free=-1;for(int v=0;v<slots;v++)if(!voices[v].isPlaying&&!structureMix.Live(v)){free=v;break;}if(free<0)break;
                var voice=voices[free];keyedVoices[free]=false;voice.loop=false;voice.clip=clip;voice.volume=Mathf.Clamp01(gain*Effects);voice.panStereo=pan;voice.pitch=rate;voice.PlayDelayed(wait);
                structureClip[free]=clip;structureMix.Claim(free,start,start+NativeStructureSounds.Length(seconds,rate),owner,cue);
                if(banked!=null){bankVoiceClip[free]=clip;bankVoicePriority[free]=NativeSoundPriority.Normal;bankVoiceEnd[free]=start+NativeStructureSounds.Length(seconds,rate)+.02;}
            }
        }
        // Once a frame from Render: plays the queued cues that are due; a new round or no arena drops them.
        private void StructureQueue(NativeArena arena)
        {
            int generation=arena?.Generation??-1;if(generation!=structureQueueGeneration){structureQueueGeneration=generation;for(int i=0;i<structureQueue.Length;i++)structureQueue[i].Live=false;structureMix.Clear();structureFalls.Clear();return;}
            double now=AudioSettings.dspTime;for(int i=0;i<structureQueue.Length;i++){var q=structureQueue[i];if(!q.Live||now<q.Due)continue;structureQueue[i].Live=false;StructureCue(q.Cue,q.At,q.Material,q.Level,0,q.Owner);}
        }
        /// <summary>A structure event's cue (§5.4 fields): stress groans, a fall collapses, a landing thuds by the fall's material.</summary>
        public void StructureEvent(NativeStructureCue cue,in ArenaEvent e)
        {
            string segment=e.Detail??"";if(cue==NativeStructureCue.Fall)structureFalls[segment]=NativeStructureFx.Classify(e.SurfaceId);
            if(!NativeStructureSounds.ForEvent(cue,segment,out var sound))return;structureFalls.TryGetValue(segment,out var material);
            StructureCue(sound,NativeArena.Vec(e.Position),material,cue==NativeStructureCue.Stress?.8f:cue==NativeStructureCue.Fall?Mathf.Clamp01(e.Amount/80):Mathf.Clamp01(e.Amount),0,NativeStructureSounds.Owner(segment));
        }
    }
}
