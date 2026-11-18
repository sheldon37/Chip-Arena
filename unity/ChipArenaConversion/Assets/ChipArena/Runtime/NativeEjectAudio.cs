using UnityEngine;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The eject's sounds (Core NativeEjectSounds: the klaxon, the hatches, the drop, the tube's rush, the seat, the harness, the roof
    /// hatch's seal, the release and the white rush) played as the station's stage hears them (NativeDeployStage.EjectCues: my own
    /// pad's moments, each once), through the effects voices beside the machines' (Runtime/NativeDeployAudio.cs, Astra's, as it is):
    /// the shared limiter, the effects volume and mute, at most eight keyed voices. Leaving the station stops them.
    /// </summary>
    public sealed partial class NativeAudio
    {
        private readonly AudioClip[] ejectClips=new AudioClip[NativeEjectSounds.Count];private readonly AudioClip[] ejectVoiceClip=new AudioClip[24];private int ejectWarm;
        private AudioClip EjectClip(int index)
        {
            if(ejectClips[index]!=null)return ejectClips[index];var samples=NativeEjectSounds.Samples(index);
            var clip=AudioClip.Create("Eject synth "+index,samples.Length,1,NativeEjectSounds.SampleRate,false);clip.SetData(samples,0);return ejectClips[index]=clip;
        }
        /// <summary>Called after the station's draw each frame (the live station and the DEV preview).</summary>
        public void EjectFrame(NativeDeployStage stage)
        {
            if(voices==null||stage==null)return;
            if(ejectWarm<NativeEjectSounds.Count)EjectClip(ejectWarm++);
            for(int v=0;v<voices.Length&&v<ejectVoiceClip.Length;v++)if(ejectVoiceClip[v]!=null)
            {
                if(!voices[v].isPlaying||voices[v].clip!=ejectVoiceClip[v])ejectVoiceClip[v]=null;
                else if(Muted||Effects<=0){voices[v].Stop();ejectVoiceClip[v]=null;}
            }
            for(int i=0;i<stage.EjectCueCount;i++)
            {
                if(Muted||Effects<=0)break;var cue=stage.EjectCues[i];int index=(int)cue.Kind;
                float gain=(float)limiter.Cue(NativeEjectSounds.Key(cue.Kind),Time.unscaledTime,NativeEjectSounds.Gain(cue.Kind));if(gain<=0)continue;
                int free=-1,keyed=0;for(int v=0;v<voices.Length;v++){if(!voices[v].isPlaying){if(free<0)free=v;}else if(keyedVoices[v])keyed++;}if(free<0||keyed>=8)continue;
                var voice=voices[free];keyedVoices[free]=true;if(free<ejectVoiceClip.Length)ejectVoiceClip[free]=voice.clip=EjectClip(index);else voice.clip=EjectClip(index);
                voice.loop=false;voice.volume=Mathf.Clamp01(gain*Effects);voice.panStereo=cue.Pan;voice.pitch=1;voice.Play();
            }
        }
        /// <summary>Stops the eject's sounds (leaving the station).</summary>
        public void EjectStop()
        {
            if(voices!=null)for(int v=0;v<voices.Length&&v<ejectVoiceClip.Length;v++)if(ejectVoiceClip[v]!=null){if(voices[v].clip==ejectVoiceClip[v])voices[v].Stop();ejectVoiceClip[v]=null;}
        }
    }
}
