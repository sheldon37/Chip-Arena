using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeAudio
    {
        private readonly NativeDeathBroadcast deathAudio=new NativeDeathBroadcast();private AudioClip deathLaunch,deathBurst;
        private static AudioClip DeathTone(bool burst)
        {
            const int rate=48000;var samples=new float[(int)(rate*(burst?.92:1))];int tones=burst?3:1;
            for(int n=0;n<tones;n++){double start=n*.11,f0=burst?660+n*220:240,f1=burst?650+n*220:1700,ramp=burst?.4:.8,fade=burst?.65:.95,length=burst?.7:1,peak=burst?.14:.10,ratio=f1/f0,log=Math.Log(ratio);
                for(int i=(int)(start*rate);i<samples.Length;i++){double t=i/(double)rate-start;if(t<0||t>=length)continue;double x=Math.Min(t,ramp),cycles=f0*ramp/log*(Math.Exp(log*x/ramp)-1)+Math.Max(0,t-ramp)*f1,wave=burst?Math.Sin(2*Math.PI*cycles):2/Math.PI*Math.Asin(Math.Sin(2*Math.PI*cycles)),gain=t<.025?.001+(peak-.001)*t/.025:peak*Math.Pow(.001/peak,Math.Min(1,(t-.025)/(fade-.025)));samples[i]+=(float)(wave*gain);}}
            var clip=AudioClip.Create(burst?"Death broadcast burst":"Death broadcast launch",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        private void SceneCues(NativeArena arena,bool menu)
        {
            float t=arena.Time;deathAudio.Tick(t,menu||arena.Match.Phase==NativePhase.Lobby||arena.Match.Phase==NativePhase.Intro);if(menu)return;
            foreach(var p in arena.Pilots.Where(p=>p.Hp<=0&&p.Death!=null).OrderBy(p=>p.Death.at).ThenBy(p=>p.Death.seed))deathAudio.Add(p.Death.seed,p.Seat.id,p.Death.owner,p.Death.at,t,p.Seat.name,null);
            foreach(var item in deathAudio.Items)for(int i=0;i<2;i++){float at=item.At+(i==0?0:1.05f);if(t<at||t-at>.25f||!Once("broadcast:"+item.Seed+":"+i,at)||Muted||Effects<=0)continue;var voice=Voice();if(voice!=null){voice.clip=i==0?(deathLaunch??(deathLaunch=DeathTone(false))):(deathBurst??(deathBurst=DeathTone(true)));voice.pitch=1;voice.panStereo=0;voice.volume=Effects;voice.Play();}if(i==1)Cue("death.broadcast","explosion",null,.7f);}
            foreach(var e in arena.Entries)if(t>=e.openAt&&t-e.openAt<.4f&&Once("entry:"+e.entry,e.openAt))Cue("hazard.entry","metal",null,.18f);
            // pve-deny has no Unity event: my denied machine use plays from the machine log. Buys, the reveal phase and my
            // revive play from their PveBuy / PvePhase / PveRevive events, and the wave count-in from NativeAudio.Render.
            if(arena.Pve!=null)
                foreach(var use in arena.Pve.Machines)if(use.result=="deny"&&use.owner==arena.LocalId&&t>=use.at&&t-use.at<.3f&&Once("machine:"+use.machine+":"+use.result,use.at))Cue("ui.error","ui",null,.3f,mine:true);
        }
        // audio.ts arcadeTone(5, true, true) for an arcade-* install: a sawtooth sweeping 120 -> 920 Hz over .25 s, its gain
        // falling .09 -> .001 over .35 s, stopped at .38 s.
        private AudioClip arcadeTone;
        private void ArcadeTone()
        {
            if(Muted||Effects<=0)return;
            if(arcadeTone==null)
            {
                const int rate=48000;var samples=new float[(int)(rate*.38f)];double phase=0;
                for(int i=0;i<samples.Length;i++){double t=i/(double)rate,f=t<.25?120*Math.Pow(920/120.0,t/.25):920,g=t<.35?.09*Math.Pow(.001/.09,t/.35):.001;phase+=f/rate;samples[i]=(float)(2*(phase-Math.Floor(phase+.5))*g);}
                arcadeTone=AudioClip.Create("Arcade install tone",samples.Length,1,rate,false);arcadeTone.SetData(samples,0);
            }
            var voice=Voice();if(voice==null)return;voice.clip=arcadeTone;voice.pitch=1;voice.panStereo=0;voice.volume=Effects;voice.Play();
        }
        private void ClearSceneAudio(){CapturedAnimation.Release(deathLaunch);CapturedAnimation.Release(deathBurst);CapturedAnimation.Release(arcadeTone);}
    }
}
