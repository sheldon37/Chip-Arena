using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The final killcam's slowed sound (Core NativeFinalKillcam.Pitch/Duck): after the replay's Render, every playing one-shot
    // effects voice is pitched down with the playback rate (Unity's pitch also slows it) and the music ducks; before the next
    // Render (and when the replay ends) each voice this slowed gets its own pitch back, so new cues start at their own rate
    // and are slowed once. Loops (the field beds) keep their own levels and pitch. Arrays are made once.
    public sealed partial class NativeAudio
    {
        private float[] replayScale;private AudioClip[] replayClip;private int[] replaySample;
        /// <summary>Gives each voice ReplaySlow pitched its own pitch back (a voice that has since restarted or changed clip
        /// already has its own).</summary>
        public void ReplayRestore()
        {
            if(replayScale==null||voices==null)return;
            for(int i=0;i<replayScale.Length&&i<voices.Length;i++)
            {
                if(replayScale[i]==1)continue;var v=voices[i];
                if(v!=null&&v.isPlaying&&v.clip==replayClip[i]&&v.timeSamples>=replaySample[i])v.pitch/=replayScale[i];
                replayScale[i]=1;replayClip[i]=null;
            }
        }
        /// <summary>Pitches every playing one-shot effects voice by `pitch` and the music level by `duck` (this frame).</summary>
        public void ReplaySlow(float pitch,float duck)
        {
            if(voices==null)return;
            if(replayScale==null){replayScale=new float[voices.Length];replayClip=new AudioClip[voices.Length];replaySample=new int[voices.Length];for(int i=0;i<replayScale.Length;i++)replayScale[i]=1;}
            if(pitch<.999f)for(int i=0;i<voices.Length&&i<replayScale.Length;i++)
            {
                var v=voices[i];if(v==null||!v.isPlaying||v.loop)continue;
                v.pitch*=pitch;replayScale[i]=pitch;replayClip[i]=v.clip;replaySample[i]=v.timeSamples;
            }
            if(music!=null&&duck<1)music.volume*=duck;
        }
    }
}
