using System.Threading.Tasks;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeAudio
    {
        // ATLAS's arrival (Runtime NativeAtlasScene.Sound; Claude's Core NativeAtlasSounds table): the flaps' clatter as line 1
        // arrives and the bell as it lands. Both clips are synthesized on workers when the audio wakes (once a session; Samples
        // costs tens of milliseconds, so it never runs on the main thread at a cue), made into AudioClips at the first cue heard
        // (both then, so the bell's frame only plays), and started where the picture is: a moment heard late (the frame's lag, a
        // stall) starts its clip that far in (NativeAtlasSounds.StartAt), so each click stays on its flap. Unpanned (the board
        // hangs over the middle of the view), on the shared effects volume, mute and limiter; a reopened station plays them
        // again, a late frame never replays them, and a clip not synthesized by its cue is skipped.
        private static Task<float[]>[] atlasSynth;
        private readonly AudioClip[] atlasClips=new AudioClip[NativeAtlasSounds.Count];
        private void AtlasAwake()
        {
            NativeAtlasScene.Sound+=AtlasHeard;
            if(atlasSynth!=null)return;atlasSynth=new Task<float[]>[NativeAtlasSounds.Count];
            for(int i=0;i<atlasSynth.Length;i++){int k=i;atlasSynth[i]=Task.Run(()=>NativeAtlasSounds.Samples(k));}
        }
        // Every synthesized clip not yet made, as an AudioClip (the main thread's part: a create and a copy).
        private void AtlasClips()
        {
            for(int i=0;i<atlasClips.Length;i++)
            {
                var job=atlasSynth?[i];if(atlasClips[i]!=null||job==null||!job.IsCompleted)continue;
                if(job.Status!=TaskStatus.RanToCompletion){Debug.LogWarning("An ATLAS sound did not synthesize: "+job.Exception?.GetBaseException().Message);atlasSynth[i]=null;continue;}
                var samples=job.Result;var clip=AudioClip.Create("ATLAS "+NativeAtlasSounds.Keys[i],samples.Length,1,NativeAtlasSounds.SampleRate,false);clip.SetData(samples,0);atlasClips[i]=clip;
            }
        }
        private void AtlasHeard(NativeAtlasBoard.Cue cue)
        {
            if(voices==null||Muted||Effects<=0)return;var sound=NativeAtlasSounds.Of(cue);if(sound.Index<0)return;
            AtlasClips();var clip=atlasClips[sound.Index];if(clip==null)return;
            int from=(int)(NativeAtlasSounds.StartAt(sound.Index,cue.Late)*NativeAtlasSounds.SampleRate);if(from>=clip.samples-1)return;
            float gain=(float)limiter.Cue(sound.Key,Time.unscaledTime,sound.Gain);if(gain<=0)return;
            var voice=Voice();if(voice==null)return;
            voice.clip=clip;voice.timeSamples=from;voice.volume=Mathf.Clamp01(gain*Effects);voice.panStereo=0;voice.pitch=1;voice.loop=false;voice.Play();
        }
        private void ClearAtlasClips(){foreach(var clip in atlasClips)if(clip!=null)CapturedAnimation.Release(clip);}
    }
}
