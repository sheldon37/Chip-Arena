using UnityEngine;

namespace ChipArena.UnityConversion
{
    // Top Dog Emporium's hazard cues (docs/arenas/EMPORIUM.md "Sound") on NativeAudio's existing voices: Core NativeEmporiumSound.Cues
    // for the window since the last frame on the match clock (the horn, slide whistle, rumble and boing of the wiener lanes; the
    // presses' ding, splort and pop; the griddles' clicks and sizzle; the geysers' pumps and sploosh), its synthesized samples made
    // once per key, positioned and faded with distance like every world cue. Map "emporium" only; a new round, a rewind or another
    // map just restarts the window. Allocation free after each clip's first use.
    public sealed partial class NativeAudio
    {
        private readonly EmporiumCue[] emporiumCues=new EmporiumCue[48];private double emporiumTime=double.NaN;private int emporiumGeneration=-1;
        private readonly AudioClip[] emporiumClips=new AudioClip[NativeEmporiumSound.Keys.Length];
        private void EmporiumCues(NativeArena arena,bool menu)
        {
            if(menu||arena==null||!NativeEmporiumLooks.Active(arena.MapId)){emporiumTime=double.NaN;return;}
            var match=arena.Match;double time=arena.Time;if(emporiumGeneration!=arena.Generation){emporiumGeneration=arena.Generation;emporiumTime=double.NaN;}
            bool fight=match.Phase==NativePhase.Fight||match.Phase==NativePhase.Result;
            if(fight&&!double.IsNaN(emporiumTime)&&time>emporiumTime&&time-emporiumTime<1)
            {
                int n=NativeEmporiumSound.Cues(arena.Map?.hazards,NativeStructureFx.RoundIndex(match.Set,match.Round),match.FightAt,emporiumTime,time,emporiumCues);
                for(int i=0;i<n;i++)EmporiumCue(emporiumCues[i]);
            }
            emporiumTime=fight?time:double.NaN;
        }
        private AudioClip EmporiumClip(int kind)
        {
            if(kind<0||kind>=emporiumClips.Length)return null;if(emporiumClips[kind]!=null)return emporiumClips[kind];
            var data=NativeEmporiumSound.Samples(kind);if(data==null)return null;var c=AudioClip.Create("Emporium "+NativeEmporiumSound.Keys[kind],data.Length,1,NativeEmporiumSound.Rate,false);c.SetData(data,0);return emporiumClips[kind]=c;
        }
        private void EmporiumCue(in EmporiumCue c)
        {
            if(Effects<=0||Muted)return;var at=new Vector3(c.At.x,c.At.y,c.At.z);var d=at-listener;float gain=NativeEmporiumSound.Falloff(c.Kind,d.magnitude);if(gain<=.01f)return;
            var clip=EmporiumClip(c.Kind);if(clip==null)return;int v=BankVoice(false,NativeSoundPriority.Normal);if(v<0)return;var voice=voices[v];keyedVoices[v]=false;
            voice.loop=false;voice.clip=clip;voice.volume=Mathf.Clamp01(gain*Effects);voice.panStereo=NativeEventSounds.Pan(NativeArena.Vec(d),yaw);voice.pitch=1+.03f*(c.Cycle%5-2);voice.Play();
            bankVoiceClip[v]=clip;bankVoicePriority[v]=NativeSoundPriority.Normal;bankVoiceEnd[v]=AudioSettings.dspTime+clip.length/voice.pitch+.02;
        }
    }
}
