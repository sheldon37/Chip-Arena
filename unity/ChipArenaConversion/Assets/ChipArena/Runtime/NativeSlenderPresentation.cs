using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeAudio
    {
        private AudioSource stalkDrone,stalkNoise;private AudioClip droneClip,noiseClip,heartClip;private double beatAt;private float droneGain,noiseGain;
        private void Slender(NativeArena arena,bool menu)
        {
            var f=!menu&&arena?.Match.Phase==NativePhase.Fight?arena.LeetFields.FirstOrDefault(x=>x.Kind=="tree"&&x.Target==arena.Local&&x.Slender!=null):null;
            if(f==null&&stalkDrone==null)return;
            if(stalkDrone==null){AudioClip Clip(string name,float[] samples){var c=AudioClip.Create(name,samples.Length,1,48000,false);c.SetData(samples,0);return c;}
                droneClip=Clip("Slender low drone",NativeSlenderRules.Drone());noiseClip=Clip("Slender filtered static",NativeSlenderRules.Noise());var beat=new float[9600];for(int i=0;i<beat.Length;i++)beat[i]=NativeSlenderRules.Heartbeat(i/48000f);heartClip=Clip("Slender heartbeat",beat);
                AudioSource Loop(AudioClip clip){var s=gameObject.AddComponent<AudioSource>();s.playOnAwake=false;s.clip=clip;s.loop=true;s.volume=0;s.Play();return s;}stalkDrone=Loop(droneClip);stalkNoise=Loop(noiseClip);}
            float sanity=f?.Sanity??0,tension=NativeSlenderRules.Tension(sanity,f?.Hops??0),dt=Time.unscaledDeltaTime;
            droneGain=Mathf.Lerp(droneGain,f==null?0:.04f+.22f*tension,1-Mathf.Exp(-dt/.25f));noiseGain=Mathf.Lerp(noiseGain,f==null?0:sanity*(f.Seen?.32f:.12f),1-Mathf.Exp(-dt/.08f));
            stalkDrone.volume=Muted?0:droneGain*Effects;stalkNoise.volume=Muted?0:noiseGain*Effects;
            double now=AudioSettings.dspTime;if(f==null||now<beatAt||Muted||Effects<=0)return;beatAt=now+NativeSlenderRules.BeatGap(tension);
            // Two free voices for the lub-dub, cleared of any limited-cue mark so Play's 8-voice cue cap counts them as plays.
            for(int v=0,pulse=0;v<voices.Length&&pulse<2;v++){var voice=voices[v];if(voice.isPlaying)continue;keyedVoices[v]=false;voice.clip=heartClip;voice.volume=(.18f+.5f*tension)*(pulse==0?1:.7f)*Effects;voice.panStereo=0;voice.pitch=1;voice.PlayScheduled(now+.005+pulse*.17);pulse++;}
        }
        private void ClearSlender(){CapturedAnimation.Release(droneClip);CapturedAnimation.Release(noiseClip);CapturedAnimation.Release(heartClip);}
    }
    public sealed partial class ConversionGame
    {
        private Texture2D slenderNoise,scareImage;private int stalkField=-1,stalkHops;private float stalkHopAt=-100,stalkLevel;
        private void SlenderFrame()
        {
            var f=!menu&&arena?.Match.Phase==NativePhase.Fight?arena.LeetFields.FirstOrDefault(x=>x.Kind=="tree"&&x.Target==arena.Local&&x.Slender!=null):null;
            if(f!=null){if(stalkField==f.Id&&stalkHops<f.Hops)stalkHopAt=arena.Time;else if(stalkField!=f.Id)stalkHopAt=-100;stalkField=f.Id;stalkHops=f.Hops;stalkLevel=Mathf.Round(NativeSlenderRules.Static(f.Sanity,f.Seen,arena.Time-stalkHopAt)*50)/50;}else{stalkField=-1;stalkLevel=0;}
        }
        private bool SlenderGUI()
        {
            bool scare=!menu&&arena?.Local!=null&&arena.Local.ScareUntil>arena.Time;float age=scare?arena.Time-arena.Local.ScareAt:0;if(!scare&&(stalkLevel<=0||menu||arena==null))return false;var old=GUI.color;var matrix=GUI.matrix;GUI.matrix=Matrix4x4.identity;if(scare){GUI.color=new Color(.012f,.035f,.02f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;if(age>=.4f){if(scareImage==null)scareImage=Resources.Load<Texture2D>("SlenderScare");float scale=1+Mathf.Clamp01(age/2)*.15f;GUI.DrawTexture(new Rect(Screen.width*(1-scale)/2,Screen.height*(1-scale)/2,Screen.width*scale,Screen.height*scale),scareImage,ScaleMode.ScaleAndCrop);}}if(slenderNoise==null){slenderNoise=new Texture2D(128,128,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Point};var pixels=new Color32[128*128];uint seed=9137;for(int i=0;i<pixels.Length;i++){seed=unchecked(seed*1664525+1013904223);byte n=(byte)(seed>>24);pixels[i]=new Color32(n,n,n,255);}slenderNoise.SetPixels32(pixels);slenderNoise.Apply(false,true);}
            float level=scare?(age<.4f?1:.16f):stalkLevel;GUI.color=new Color(1,1,1,level*.36f);float frame=Mathf.Floor(Time.unscaledTime*18);GUI.DrawTextureWithTexCoords(new Rect(0,0,Screen.width,Screen.height),slenderNoise,new Rect(frame*.37f,frame*.61f,Screen.width/128f,Screen.height/128f));GUI.color=new Color(0,0,0,level*.13f);for(int y=0;y<Screen.height;y+=5)GUI.DrawTexture(new Rect(0,y,Screen.width,1),Texture2D.whiteTexture);GUI.color=old;GUI.matrix=matrix;return scare;
        }
    }
}
