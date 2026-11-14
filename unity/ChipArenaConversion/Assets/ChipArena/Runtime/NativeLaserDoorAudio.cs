using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The instance exit's laser door hum (Claude, 2026-10-17; docs/EXPEDITION-PRESENTATION.md "The laser door") on NativeAudio's
    // existing voices (no new AudioSource): one low-priority looping voice while the door's beams stand within earshot, easing
    // in and out over .4 s. The clip is the bank's expedition.door.hum once the owner's clip is installed and registered, else the
    // hazard kit's built-in synthesized beacon hum (Core NativeKitSynth "alarm.beacon": 120 Hz with a slow wobble). The caller asks
    // again every frame; a frame without a request fades it out. Presentation only.
    public sealed partial class NativeAudio
    {
        public const string DoorHumKey="expedition.door.hum",DoorHumFallback="alarm.beacon";
        private int doorHumVoice=-1;private AudioClip doorHumClip;private float doorHumLevel;private Vector3 doorHumAt;private bool doorHumWanted;
        /// <summary>This frame: the laser door's beams hum at `at` (false or no call: the hum fades out).</summary>
        public void DoorHum(bool on,Vector3 at){doorHumWanted=on;if(on)doorHumAt=at;}
        private void DoorHumFrame(NativeArena arena,bool menu)
        {
            bool want=doorHumWanted&&arena!=null&&!menu&&!Muted&&Effects>0;doorHumWanted=false;
            if(doorHumVoice>=0&&(voices==null||voices[doorHumVoice].clip!=doorHumClip||!voices[doorHumVoice].isPlaying)){doorHumVoice=-1;doorHumLevel=0;}
            doorHumLevel=Mathf.MoveTowards(doorHumLevel,want?1:0,Time.unscaledDeltaTime/.4f);
            if(doorHumVoice<0)
            {
                if(!want||voices==null)return;
                var e=bank.Entry(DoorHumKey);AudioClip clip=e!=null&&e.Count>0?BankClip(e.Path(bank.Next(DoorHumKey))):null;if(clip==null||clip.loadState==AudioDataLoadState.Failed)clip=KitSynthClip(DoorHumFallback);if(clip==null)return;
                int v=BankVoice(false,NativeSoundPriority.Low);if(v<0)return;var voice=voices[v];keyedVoices[v]=false;
                voice.clip=clip;voice.loop=true;voice.volume=0;voice.pitch=1;voice.Play();
                bankVoiceClip[v]=clip;bankVoicePriority[v]=NativeSoundPriority.Low;bankVoiceEnd[v]=double.MaxValue;doorHumVoice=v;doorHumClip=clip;
            }
            var hum=voices[doorHumVoice];
            if(doorHumLevel<=0){hum.Stop();hum.loop=false;bankVoiceClip[doorHumVoice]=null;doorHumVoice=-1;return;}
            var d=doorHumAt-listener;hum.volume=Mathf.Clamp01(doorHumLevel*.28f*NativeEventSounds.Falloff(d.magnitude)*Effects);hum.panStereo=NativeEventSounds.Pan(NativeArena.Vec(d),yaw);
        }
    }
}
