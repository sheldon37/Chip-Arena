using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // Belvedere Camp's sound bed (docs/audio/SOUND-REQUESTS.md "The camp's sound bed"; plan W12) on NativeAudio's existing voices (no
    // new AudioSource): the nearest two fires' crackle, the generator's hum behind the fuse board, the camp PA's radio and the murmur of
    // people round the camp, each a low-priority looping voice easing in and out over .5 s with its own reach, and the war board lamp's
    // click. The clips are the bank's camp.* once the owner's batch is generated and imported, else the built-in synthesized
    // placeholders (Core NativeCampSynth). The camp's drawer feeds it every frame (Runtime NativeCampDraw); a frame without a feed
    // fades it all out. Presentation only.
    public sealed partial class NativeAudio
    {
        private const int CampVoices=5;
        private struct CampLoop{public int Voice;public string Key;public AudioClip Clip;public float Level,Target,Gain;public Vector3 At;public int Source;}
        private readonly CampLoop[] campLoops=new CampLoop[CampVoices];private readonly Dictionary<string,AudioClip> campSynth=new Dictionary<string,AudioClip>();
        private readonly int[] campWant=new int[CampVoices];private readonly float[] campLevel=new float[32];
        private AudioClip CampClip(string key)
        {
            var e=bank.Entry(key);if(e!=null&&e.Count>0){var c=BankClip(e.Path(bank.Next(key)));if(c!=null&&c.loadState!=AudioDataLoadState.Failed)return c;}
            if(campSynth.TryGetValue(key,out var s))return s;var data=NativeCampSynth.Samples(key);
            if(data!=null){s=AudioClip.Create("Camp "+key,data.Length,1,NativeCampSynth.Rate,false);s.SetData(data,0);}campSynth[key]=s;return s;
        }
        /// <summary>A source's level at the listener: full within a fifth of its reach, gone at its reach.</summary>
        private static float CampFalloff(float d,float reach){float u=Mathf.Clamp01((d-reach*.2f)/(reach*.8f));return (1-u)*(1-u);}
        private void CampAudioFrame(NativeArena arena,bool menu)
        {
            for(int s=0;s<CampVoices;s++)campLoops[s].Target=0;
            var l=NativeCampAudioFeed.Layout;bool on=l!=null&&!menu&&arena!=null&&!Muted&&Effects>0&&Time.frameCount-NativeCampAudioFeed.Frame<=2;
            if(on)
            {
                // the loudest wanted sources keep a voice: every bed, and the two fires nearest the listener
                var sounds=l.Sounds;int n=Mathf.Min(sounds.Count,campLevel.Length);
                for(int i=0;i<n;i++){var src=sounds[i];campLevel[i]=CampFalloff((new Vector3(src.at.x,src.at.y,src.at.z)-listener).magnitude,src.reach)*src.gain;}
                for(int k=0;k<CampVoices;k++)
                {
                    campWant[k]=-1;int best=-1;float bs=.002f;int fires=0;for(int j=0;j<k;j++)if(campWant[j]>=0&&sounds[campWant[j]].key=="camp.fire")fires++;
                    for(int i=0;i<n;i++){if(campLevel[i]<=bs)continue;bool taken=false;for(int j=0;j<k;j++)if(campWant[j]==i)taken=true;if(taken||sounds[i].key=="camp.fire"&&fires>=2)continue;bs=campLevel[i];best=i;}
                    campWant[k]=best;
                }
                for(int k=0;k<CampVoices;k++)
                {
                    int i=campWant[k];if(i<0)continue;var src=sounds[i];int found=-1;for(int s=0;s<CampVoices;s++)if(campLoops[s].Voice>=0&&campLoops[s].Clip!=null&&campLoops[s].Source==i){found=s;break;}
                    if(found<0)for(int s=0;s<CampVoices;s++)if(campLoops[s].Clip==null){var clip=CampClip(src.key);if(clip==null)break;int v=BankVoice(false,NativeSoundPriority.Low);if(v<0)break;var voice=voices[v];keyedVoices[v]=false;
                        voice.clip=clip;voice.loop=true;voice.volume=0;voice.pitch=1;voice.timeSamples=(int)(clip.samples*(.13f*i%1f));voice.Play();bankVoiceClip[v]=clip;bankVoicePriority[v]=NativeSoundPriority.Low;bankVoiceEnd[v]=double.MaxValue;
                        campLoops[s]=new CampLoop{Voice=v,Key=src.key,Clip=clip,Level=0,Source=i};found=s;break;}
                    if(found<0)continue;campLoops[found].Target=1;campLoops[found].Gain=campLevel[i];campLoops[found].At=new Vector3(src.at.x,src.at.y,src.at.z);
                }
            }
            for(int s=0;s<CampVoices;s++)
            {
                ref var c=ref campLoops[s];if(c.Clip==null)continue;
                if(voices==null||voices[c.Voice].clip!=c.Clip||!voices[c.Voice].isPlaying){c=new CampLoop{Voice=-1};continue;}
                c.Level=Mathf.MoveTowards(c.Level,c.Target,Time.unscaledDeltaTime/.5f);var vc=voices[c.Voice];
                if(c.Level<=0&&c.Target<=0){vc.Stop();vc.loop=false;bankVoiceClip[c.Voice]=null;c=new CampLoop{Voice=-1};continue;}
                var d=c.At-listener;vc.volume=Mathf.Clamp01(c.Level*c.Gain*Effects);vc.panStereo=NativeEventSounds.Pan(NativeArena.Vec(d),yaw);
            }
            // the war board's desk lamp, clicking on or off
            if(NativeCampAudioFeed.Click){NativeCampAudioFeed.Click=false;if(on){var clip=CampClip("camp.lamp.click");var at=CampLampAt(l);var v=Voice();if(clip!=null&&v!=null){var d=at-listener;v.clip=clip;v.volume=Mathf.Clamp01(.55f*NativeEventSounds.Falloff(d.magnitude)*Effects);v.panStereo=NativeEventSounds.Pan(NativeArena.Vec(d),yaw);v.pitch=NativeCampAudioFeed.Lamp?1.05f:.92f;v.Play();}}}
        }
        private static Vector3 CampLampAt(NativeCamp.Layout l){foreach(var p in l.Pieces)if(p.Kind==NativeCamp.Kind.WarLamp)return new Vector3(p.Light.x,p.Light.y,p.Light.z);return Vector3.zero;}
    }
}
