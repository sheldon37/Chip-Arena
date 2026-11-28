using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The quota markers' cue (Claude, 2026-10-18; Runtime/NativeQuotaMarksPresentation.cs): one soft ping when the last enemies of a
    // kill quota are first marked. Through NativeAudio's bank path with its own key (NativeQuotaMarks.CueKey,
    // "expedition.quota.mark") once the owner's clip is installed; until then, and in every build without LocalAssets, the
    // built-in synthesized ping (Core NativeQuotaMarks.PingSamples), so it is never silent or missing. Not positional (a HUD cue),
    // on NativeAudio's existing voices (no new AudioSource). Presentation only.
    public sealed partial class NativeAudio
    {
        private AudioClip quotaMarkClip;
        /// <summary>Plays the quota markers' ping once (the caller asks once per quota).</summary>
        public void QuotaMark()
        {
            if(!bankLoaded)LoadBank();if(BankPlay(NativeQuotaMarks.CueKey,null,NativeQuotaMarks.PingGain,0,null,true))return;
            if(Muted||Effects<=0||voices==null)return;
            if(quotaMarkClip==null){var data=NativeQuotaMarks.PingSamples();quotaMarkClip=AudioClip.Create("Quota mark",data.Length,1,NativeQuotaMarks.PingRate,false);quotaMarkClip.SetData(data,0);}
            int v=BankVoice(false,NativeSoundPriority.Normal);if(v<0)return;var voice=voices[v];keyedVoices[v]=false;
            voice.loop=false;voice.clip=quotaMarkClip;voice.volume=Mathf.Clamp01(NativeQuotaMarks.PingGain*Effects);voice.panStereo=0;voice.pitch=1;voice.Play();
            bankVoiceClip[v]=quotaMarkClip;bankVoicePriority[v]=NativeSoundPriority.Normal;bankVoiceEnd[v]=AudioSettings.dspTime+quotaMarkClip.length+.02;
        }
        private void ClearQuotaMark(){CapturedAnimation.Release(quotaMarkClip);quotaMarkClip=null;}
    }
}
