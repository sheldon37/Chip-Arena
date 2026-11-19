using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The drop pods' sounds (Core NativePodSounds: original, synthesized placeholders until the owner's ElevenLabs batch; the cues
    // come from each pod's plan, gathered by NativePodPresentation.cs as the presentation clock passes them, so every peer hears
    // the same crash): each played from where it happens, panned and faded by its distance from the listener (each sound carries
    // its own range); the cloud punch's boom reaches each listener late by its distance, at the speed of sound. One synthesized
    // clip warmed a frame while pods are in the arena. Presentation only.
    public sealed partial class NativeAudio
    {
        private readonly AudioClip[] podClips=new AudioClip[NativePodSounds.Count];private int podWarm;
        private struct PodPending{public int Sound;public Vector3 At;public float Due,Gain,Range,Rate;}
        private readonly PodPending[] podPending=new PodPending[12];private int podPendingCount;
        /// <summary>How fast a boom travels to the listener (m/s).</summary>
        public const float PodSoundSpeed=343;
        private AudioClip PodClip(int i)
        {
            if(podClips[i]!=null)return podClips[i];var samples=NativePodSounds.Samples(i);
            var clip=AudioClip.Create("Pod "+NativePodSounds.Keys[i],samples.Length,1,NativePodSounds.SampleRate,false);clip.SetData(samples,0);return podClips[i]=clip;
        }
        /// <summary>After Render: this frame's due pod sounds (`cues`; null with no pods in the arena) from where they happen, the
        /// booms held for their travel time.</summary>
        public void PodFrame(List<ArenaPresentation.PodSound> cues,Transform listener)
        {
            if(voices==null||cues==null&&podPendingCount==0)return;if(podWarm<NativePodSounds.Count)PodClip(podWarm++);float now=Time.unscaledTime;
            if(cues!=null&&listener!=null)foreach(var c in cues)
            {
                if(!c.Travels){PodPlay(c.Sound,c.At,c.Gain,c.Range,c.Rate,listener);continue;}
                if(podPendingCount<podPending.Length)podPending[podPendingCount++]=new PodPending{Sound=c.Sound,At=c.At,Due=now+Vector3.Distance(c.At,listener.position)/PodSoundSpeed,Gain=c.Gain,Range=c.Range,Rate=c.Rate};
            }
            for(int i=0;i<podPendingCount;i++)
            {
                if(podPending[i].Due>now)continue;var p=podPending[i];if(listener!=null)PodPlay(p.Sound,p.At,p.Gain,p.Range,p.Rate,listener);
                podPending[i]=podPending[--podPendingCount];i--;
            }
        }
        private void PodPlay(int sound,Vector3 at,float gain,float range,float rate,Transform listener)
        {
            if(Muted||Effects<=0||sound<0||sound>=NativePodSounds.Count)return;
            var d=at-listener.position;float dist=d.magnitude,fade=Mathf.Clamp01(1.1f-dist/Mathf.Max(1,range));if(fade<=0)return;fade*=fade;
            float pan=dist>.1f?Mathf.Clamp(Vector3.Dot(d/dist,listener.right)*.75f,-.85f,.85f):0;
            int free=-1;for(int v=0;v<voices.Length;v++)if(!voices[v].isPlaying){free=v;break;}if(free<0)return;
            var voice=voices[free];keyedVoices[free]=true;voice.clip=PodClip(sound);voice.loop=false;voice.volume=Mathf.Clamp01(gain*fade*Effects);voice.panStereo=pan;voice.pitch=rate;voice.Play();
        }
        private void ClearPodClips(){podPendingCount=0;foreach(var clip in podClips)if(clip!=null)CapturedAnimation.Release(clip);}
    }
}
