using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeAudio
    {
        private readonly AudioClip[] deployClips=new AudioClip[NativeDeploySounds.Count];
        private readonly AudioClip[] deployVoiceClip=new AudioClip[24];
        private readonly float[] deployGain=new float[24];
        private readonly int[] deployMachine=new int[24];
        private int deployWarm;
        private AudioClip DeployClip(int index)
        {
            if(deployClips[index]!=null)return deployClips[index];var samples=NativeDeploySounds.Samples(index);
            var clip=AudioClip.Create("Deployment synth "+index,samples.Length,1,NativeDeploySounds.SampleRate,false);clip.SetData(samples,0);return deployClips[index]=clip;
        }
        // Called immediately after Draw, for both the live station and DEV preview. Draw owns all edge/late-join logic.
        public void DeployFrame(NativeDeployStage stage)
        {
            if(voices==null||stage==null)return;
            if(deployWarm<NativeDeploySounds.Count)DeployClip(deployWarm++);
            for(int v=0;v<voices.Length;v++)if(deployVoiceClip[v]!=null)
            {
                if(!voices[v].isPlaying||voices[v].clip!=deployVoiceClip[v])deployVoiceClip[v]=null;
                else if(Muted||Effects<=0){voices[v].Stop();deployVoiceClip[v]=null;}
                else voices[v].volume=Mathf.Clamp01(deployGain[v]*Effects);
            }
            for(int i=0;i<stage.CueCount;i++)
            {
                var cue=stage.Cues[i];var sound=NativeDeploySounds.Of(cue);if(sound.Key==null)continue;
                if(cue.Kind==NativeDeploySparks.CueKind.Spin)StopDeployMachine(cue.Machine);
                if(Muted||Effects<=0)continue;
                float gain=(float)limiter.Cue(sound.Key+":"+cue.Reel,Time.unscaledTime,sound.Gain);if(gain<=0)continue;
                int free=-1,keyed=0;for(int v=0;v<voices.Length;v++){if(!voices[v].isPlaying){if(free<0)free=v;}else if(keyedVoices[v])keyed++;}if(free<0||keyed>=8)continue;
                var voice=voices[free];keyedVoices[free]=true;deployVoiceClip[free]=voice.clip=DeployClip(sound.Index);deployGain[free]=gain;deployMachine[free]=cue.Machine;
                voice.loop=false;voice.volume=Mathf.Clamp01(gain*Effects);voice.panStereo=sound.Pan;voice.pitch=sound.Rate;voice.Play();
            }
        }
        private void StopDeployMachine(int machine)
        {if(voices!=null)for(int v=0;v<voices.Length;v++)if(deployVoiceClip[v]!=null&&(machine<0||deployMachine[v]==machine)){if(voices[v].clip==deployVoiceClip[v])voices[v].Stop();deployVoiceClip[v]=null;}}
        public void DeployStop()=>StopDeployMachine(-1);
        private void ClearDeployClips(){DeployStop();foreach(var clip in deployClips)CapturedAnimation.Release(clip);}
    }
}
