using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeAudio
    {
        // ATLAS's arrival (Runtime NativeAtlasScene.Sound; Claude's Core NativeAtlasSounds table, synthesized once on first use):
        // the flaps' clatter as line 1 arrives and the bell as it lands. Unpanned (the board hangs over the middle of the view),
        // on the shared effects volume, mute and limiter; a reopened station plays them again, a late frame never replays them.
        private readonly AudioClip[] atlasClips=new AudioClip[NativeAtlasSounds.Count];
        private void AtlasHeard(NativeAtlasBoard.Cue cue)
        {
            if(voices==null||Muted||Effects<=0)return;var sound=NativeAtlasSounds.Of(cue);if(sound.Index<0)return;
            float gain=(float)limiter.Cue(sound.Key,Time.unscaledTime,sound.Gain);if(gain<=0)return;
            var voice=Voice();if(voice==null)return;
            var clip=atlasClips[sound.Index];
            if(clip==null){var samples=NativeAtlasSounds.Samples(sound.Index);clip=AudioClip.Create("ATLAS "+sound.Key,samples.Length,1,NativeAtlasSounds.SampleRate,false);clip.SetData(samples,0);atlasClips[sound.Index]=clip;}
            voice.clip=clip;voice.volume=Mathf.Clamp01(gain*Effects);voice.panStereo=0;voice.pitch=1;voice.loop=false;voice.Play();
        }
        private void ClearAtlasClips(){foreach(var clip in atlasClips)if(clip!=null)CapturedAnimation.Release(clip);}
    }
}
