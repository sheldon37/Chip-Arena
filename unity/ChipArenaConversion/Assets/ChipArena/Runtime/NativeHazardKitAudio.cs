using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The hazard kit's sound (docs/HAZARD-KIT.md "Audio") on NativeAudio's existing voices (no new AudioSource): Core
    // NativeKitSoundPlan picks the frame's loops and one-shots from the shared kit scene (NativeHazardKitWorld), with the kit's
    // own falloff (full near, silent past its outer radius), a duck under combat near the listener and a cap across alarms.
    // Clips are the licensed bank's (LocalAssets) when installed, else the built-in synthesized fallbacks (Core NativeKitSynth).
    // At most three kit loops hold voices (low priority: never one of the four reserved). An alarm with `react` that trips in
    // earshot dips and swells the music (NativeHazardKitClock.MusicReact, on top of the music volume and the mute); a beat-
    // locked klaxon picks the bank variant that fits two beats of the playing song (NativeHazardKitClock.FitVariant).
    // Presentation only: gameplay, timing and networking are unchanged.
    public sealed partial class NativeAudio
    {
        private readonly NativeKitSoundPlan kitPlan=new NativeKitSoundPlan();private double kitHeard=double.NegativeInfinity;private int kitGeneration=-2;private NativeArena kitArena;
        private struct KitLoopSlot{public bool Live;public int Piece,Voice;public string Key;public float Level,Target,Gain,Rate;public Vector3 At;public AudioClip Clip;}
        private readonly KitLoopSlot[] kitLoops=new KitLoopSlot[3];private readonly Dictionary<string,AudioClip> kitSynth=new Dictionary<string,AudioClip>(StringComparer.Ordinal);
        private readonly double[] kitTrips=new double[8];private int kitTrip;private float kitMusic=1;private readonly float[] kitLengths=new float[NativeSoundKeys.MaxClips];
        /// <summary>The music's gain from the kit's trip reactions this frame (1: none).</summary>
        public float KitMusicGain=>kitMusic;

        private void KitAudio(NativeArena arena,bool menu)
        {
            if(arena==null||menu||Muted){KitStopLoops();kitMusic=1;return;}
            var scene=NativeHazardKitWorld.For(arena,arena.Time);if(scene==null){KitStopLoops();kitMusic=1;return;}
            double now=arena.Time;if(!ReferenceEquals(arena,kitArena)||arena.Generation!=kitGeneration||now<kitHeard-.5){kitArena=arena;kitGeneration=arena.Generation;kitHeard=now-1/60.0;kitTrip=0;for(int i=0;i<kitTrips.Length;i++)kitTrips[i]=double.NaN;KitStopLoops();}
            kitPlan.Build(scene,NativeArena.Vec(listener),kitHeard,now,KitCombat(arena,now));kitHeard=now;
            for(int i=0;i<kitPlan.CueCount;i++)KitCue(kitPlan.Cues[i],now);
            KitLoopFrame();
            for(int i=0;i<kitPlan.TripCount;i++){kitTrips[kitTrip]=kitPlan.Trips[i];kitTrip=(kitTrip+1)%kitTrips.Length;}
            kitMusic=NativeHazardKitClock.MusicReact(kitTrips,kitTrips.Length,now,NativeHazardKitWorld.Flash);
        }
        // How much fighting surrounds the listener (0..1): shots, hits and blasts of the last 1.2 s within 25 m.
        private float KitCombat(NativeArena arena,double now)
        {
            var events=arena.Events;float sum=0;var ear=listener;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];double age=now-e.Time;if(age<0||age>1.2)continue;float w=e.Kind==EventKind.Explosion?2.5f:e.Kind==EventKind.Shot?.5f:e.Kind==EventKind.Hit||e.Kind==EventKind.Headshot||e.Kind==EventKind.Death?1:0;if(w<=0)continue;
                var d=NativeArena.Vec(e.Position)-ear;if(d.sqrMagnitude>625)continue;sum+=w*(float)(1-age/1.2);
            }
            return Mathf.Clamp01(sum/6);
        }
        private AudioClip KitSynthClip(string key)
        {
            if(kitSynth.TryGetValue(key,out var c))return c;var data=NativeKitSynth.Samples(key);
            if(data!=null){c=AudioClip.Create("Hazard kit "+key,data.Length,1,NativeKitSynth.Rate,false);c.SetData(data,0);}kitSynth[key]=c;return c;
        }
        // The bank's clip of `key` (variant: -1 the bag's next; a beat-locked klaxon the one that fits its room), else the fallback.
        private AudioClip KitClip(string key,int variant,float room,out float gain,out float rate)
        {
            gain=1;rate=1;var e=bank.Entry(key);
            if(e!=null&&e.Count>0)
            {
                int v=variant;
                if(room>0){int n=Math.Min(e.Count,kitLengths.Length);for(int k=0;k<kitLengths.Length;k++)kitLengths[k]=0;for(int k=0;k<n;k++){var c=BankClip(e.Path(k));kitLengths[k]=c!=null?Math.Min(c.length,e.MaxLength):0;}
                    v=NativeHazardKitClock.FitVariant(kitLengths,room/2,2,out rate);}
                if(v<0)v=bank.Next(key);var clip=BankClip(e.Path(v));if(clip!=null&&clip.loadState!=AudioDataLoadState.Failed){gain=e.Gain;rate*=NativeSoundBank.Rate(e,(float)bankRandom.NextDouble());return clip;}
            }
            var synth=KitSynthClip(key);if(synth!=null&&room>0&&synth.length>room)rate=Mathf.Min(1.06f,synth.length/room);return synth;
        }
        private void KitCue(in NativeKitCue c,double now)
        {
            if(Effects<=0)return;double delay=c.Time-now;if(delay<-.12||delay>.2)return;
            var clip=KitClip(c.Key,c.Variant,c.Room,out float trim,out float rate);if(clip==null)return;
            var p=NativeKitSoundPlan.Priority(c.Key);int v=BankVoice(false,p);if(v<0)return;var voice=voices[v];keyedVoices[v]=false;
            var at=NativeArena.Vec(c.At);var d=at-listener;voice.loop=false;voice.clip=clip;voice.volume=Mathf.Clamp01(c.Gain*trim*Effects);voice.panStereo=NativeEventSounds.Pan(NativeArena.Vec(d),yaw);voice.pitch=Mathf.Clamp(c.Rate*rate,.5f,2);
            if(delay>.005)voice.PlayDelayed((float)delay);else voice.Play();
            bankVoiceClip[v]=clip;bankVoicePriority[v]=p;bankVoiceEnd[v]=AudioSettings.dspTime+Math.Max(0,delay)+clip.length/voice.pitch+.02;
        }
        // The plan's loudest wanted loops keep a voice (at most three), easing in and out over .3 s; the rest fade.
        private void KitLoopFrame()
        {
            for(int s=0;s<kitLoops.Length;s++){if(!kitLoops[s].Live)continue;var vc=voices[kitLoops[s].Voice];if(vc.clip!=kitLoops[s].Clip||!vc.isPlaying){kitLoops[s].Live=false;continue;}kitLoops[s].Target=0;}
            for(int w=0;w<kitPlan.LoopCount;w++)
            {
                var want=kitPlan.Loops[w];int found=-1;for(int s=0;s<kitLoops.Length;s++)if(kitLoops[s].Live&&kitLoops[s].Piece==want.Piece&&kitLoops[s].Key==want.Key){found=s;break;}
                if(found>=0){kitLoops[found].Target=1;kitLoops[found].Gain=want.Gain;kitLoops[found].Rate=want.Rate;kitLoops[found].At=NativeArena.Vec(want.At);continue;}
                // A new loop takes a free slot, or the quietest slot when it is louder.
                int slot=-1;for(int s=0;s<kitLoops.Length;s++)if(!kitLoops[s].Live){slot=s;break;}
                if(slot<0){float quiet=want.Gain;for(int s=0;s<kitLoops.Length;s++)if(kitLoops[s].Gain*kitLoops[s].Level<quiet){quiet=kitLoops[s].Gain*kitLoops[s].Level;slot=s;}if(slot<0)continue;KitStop(slot);}
                var clip=KitClip(want.Key,-1,0,out float trim,out float rate);if(clip==null)continue;int v=BankVoice(false,NativeSoundPriority.Low);if(v<0)continue;var voice=voices[v];keyedVoices[v]=false;
                voice.clip=clip;voice.loop=true;voice.volume=0;voice.pitch=Mathf.Clamp(rate*want.Rate,.5f,2);voice.Play();voice.time=(float)(bankRandom.NextDouble()*Math.Max(0,clip.length-.05f));
                bankVoiceClip[v]=clip;bankVoicePriority[v]=NativeSoundPriority.Low;bankVoiceEnd[v]=double.MaxValue;
                kitLoops[slot]=new KitLoopSlot{Live=true,Piece=want.Piece,Key=want.Key,Voice=v,Level=0,Target=1,Gain=want.Gain*trim,Rate=want.Rate,At=NativeArena.Vec(want.At),Clip=clip};
            }
            float step=Time.unscaledDeltaTime/.3f;
            for(int s=0;s<kitLoops.Length;s++)
            {
                if(!kitLoops[s].Live)continue;var l=kitLoops[s];var voice=voices[l.Voice];l.Level=Mathf.MoveTowards(l.Level,l.Target,step);
                if(l.Level<=0&&l.Target<=0){KitStop(s);continue;}
                var d=l.At-listener;voice.volume=Mathf.Clamp01(l.Level*l.Gain*Effects);voice.panStereo=NativeEventSounds.Pan(NativeArena.Vec(d),yaw);kitLoops[s]=l;
            }
        }
        private void KitStop(int s){var l=kitLoops[s];if(!l.Live)return;var voice=voices[l.Voice];if(voice.clip==l.Clip){voice.Stop();voice.loop=false;}bankVoiceClip[l.Voice]=null;kitLoops[s].Live=false;}
        private void KitStopLoops(){if(voices==null)return;for(int s=0;s<kitLoops.Length;s++)KitStop(s);}
        private void ClearKitSynth(){foreach(var c in kitSynth.Values)CapturedAnimation.Release(c);kitSynth.Clear();}
    }
}
