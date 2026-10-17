using UnityEngine;

namespace ChipArena.UnityConversion
{
    // THE CONVERTER's five loops (Core NativeIronworksBossCues.Loops: the idle burn, the pour's stream, the blow's rain, the charge's
    // gallop, the cooling hulk's ticking) on NativeAudio's existing voices (no new AudioSource): one low-priority looping bank voice
    // per loop while its level is up, easing in and out over .25 s, placed at its source; and the one-shot cues the presentation
    // queued (Core NativeIronworksBossCues) through the bank path. The bank's clips only: a key without clips is silent (no
    // stand-in). The presentation asks every frame; a frame without a request fades the loops out. Presentation only.
    public sealed partial class NativeAudio
    {
        private const int BossLoopCount=5;
        private readonly int[] bossLoopVoice={-1,-1,-1,-1,-1};private readonly AudioClip[] bossLoopClip=new AudioClip[BossLoopCount];
        private readonly float[] bossLoopLevel=new float[BossLoopCount],bossLoopWant=new float[BossLoopCount];private readonly Vector3[] bossLoopAt=new Vector3[BossLoopCount];
        private readonly IronworksBossCue[] bossCues=new IronworksBossCue[NativeIronworksBossCues.QueueSize];private float bossHurt=float.NegativeInfinity;
        // Called every frame from Render (after the hazard kit's audio): the presentation's queued cues, its loops, its music duck.
        private void IronworksBossLoopFrame(NativeArena arena,bool menu)
        {
            int cues=NativeIronworksBossCues.Drain(bossCues);
            bool asked=NativeIronworksBossCues.Frame>=Time.frameCount-2,live=asked&&arena!=null&&!menu&&!Muted&&Effects>0;float step=Time.unscaledDeltaTime/.25f;
            if(live)for(int i=0;i<cues;i++)
            {
                var c=bossCues[i];if(c.Key==NativeIronworksBossCues.Hurt){if(c.At-bossHurt<1.1f)continue;bossHurt=c.At;}
                Cue(NativeIronworksBossCues.Keys[c.Key],null,new Vector3(c.At3.X,c.At3.Y,c.At3.Z),c.Gain);
            }
            // The music under the blow's roar and the call's bellow, and its fade before the shift whistle: the hazard kit's music
            // level (set fresh by KitAudio every frame, just before this) for this frame only.
            if(live&&NativeIronworksBossCues.MusicDuck<.999f)kitMusic*=Mathf.Clamp01(NativeIronworksBossCues.MusicDuck);
            for(int i=0;i<BossLoopCount;i++){bossLoopWant[i]=asked?Mathf.Clamp01(NativeIronworksBossCues.LoopLevel[i]):0;var a=NativeIronworksBossCues.LoopAt[i];bossLoopAt[i]=new Vector3(a.X,a.Y,a.Z);}
            for(int i=0;i<BossLoopCount;i++)
            {
                float want=live?bossLoopWant[i]:0;bossLoopWant[i]=0;int v=bossLoopVoice[i];
                if(v>=0&&(voices==null||voices[v].clip!=bossLoopClip[i]||!voices[v].isPlaying)){bossLoopVoice[i]=-1;bossLoopLevel[i]=0;v=-1;}
                bossLoopLevel[i]=Mathf.MoveTowards(bossLoopLevel[i],want,step);
                if(v<0)
                {
                    if(want<=0||voices==null)continue;string key=NativeIronworksBossCues.Keys[NativeIronworksBossCues.LoopKeys[i]];var e=bank.Entry(key);
                    AudioClip clip=e!=null&&e.Count>0?BankClip(e.Path(bank.Next(key))):null;if(clip==null||clip.loadState==AudioDataLoadState.Failed)continue;
                    v=BankVoice(false,NativeSoundPriority.Low);if(v<0)continue;var voice=voices[v];keyedVoices[v]=false;
                    voice.clip=clip;voice.loop=true;voice.volume=0;voice.pitch=1;voice.Play();
                    bankVoiceClip[v]=clip;bankVoicePriority[v]=NativeSoundPriority.Low;bankVoiceEnd[v]=double.MaxValue;bossLoopVoice[i]=v;bossLoopClip[i]=clip;
                }
                var loop=voices[v];
                if(bossLoopLevel[i]<=0){loop.Stop();loop.loop=false;bankVoiceClip[v]=null;bossLoopVoice[i]=-1;continue;}
                var d=bossLoopAt[i]-listener;loop.volume=Mathf.Clamp01(bossLoopLevel[i]*.45f*NativeEventSounds.Falloff(d.magnitude)*Effects);loop.panStereo=NativeEventSounds.Pan(NativeArena.Vec(d),yaw);
            }
        }
    }
}
