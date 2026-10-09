using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // One event cursor, bounded voices, and one music clock for local and replicated matches.
    public sealed partial class NativeAudio:MonoBehaviour
    {
        public float Effects=.3f,Music=.35f,Menu=.6f;public bool Muted;
        private readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        private readonly NativeSoundLimiter limiter=new NativeSoundLimiter();private bool[] keyedVoices;
        private readonly NativeEventSounds.Sound[] routed=new NativeEventSounds.Sound[NativeEventSounds.MaxSounds];private readonly NativeSpikeRises spikeRises=new NativeSpikeRises();
        private readonly Dictionary<string,float> cues=new Dictionary<string,float>();
        private AudioSource[] voices;private AudioSource music,introVoice;private string introKey="";private readonly Dictionary<int,AudioClip> beepClips=new Dictionary<int,AudioClip>();private string track="",phase="";private int cursor,generation=-1,countdown=-1,rotation=-1;private Vector3 listener;private float yaw;
        private readonly string[] rotationTracks={"foundry-rush","orange-line","kinetic-city","afterburner","night-circuit","critical-mass","jump-drive","neon-recoil","overpass","last-set"};
        private readonly Queue<string> bag=new Queue<string>();
        public void ResetCursor(){generation=-1;}
        private void Awake(){voices=new AudioSource[24];keyedVoices=new bool[voices.Length];for(int i=0;i<voices.Length;i++){voices[i]=gameObject.AddComponent<AudioSource>();voices[i].playOnAwake=false;voices[i].spatialBlend=0;}music=gameObject.AddComponent<AudioSource>();music.playOnAwake=false;music.priority=0;introVoice=gameObject.AddComponent<AudioSource>();introVoice.playOnAwake=false;}
        // A free voice for an unlimited, unpanned play (beeps, death tones); Play counts the limited cue voices itself.
        private AudioSource Voice(){for(int i=0;i<voices.Length;i++)if(!voices[i].isPlaying){keyedVoices[i]=false;return voices[i];}return null;}
        private AudioClip Clip(string id){if(!clips.TryGetValue(id,out var c)){c=Resources.Load<AudioClip>("Audio/"+id);clips[id]=c;}return c;}
        // audio.ts ArenaAudio.play / cue: `gate` names a SoundLimiter key (a limited cue, trimmed, at most 8 such voices of 24);
        // `rate` 0 takes the source's .97-1.03 per-play rate (1 for hit, ui, countdown and the hook/step clips).
        public void Play(string id,Vector3? position=null,float gain=1,float rate=0,string gate=null)
        {
            if(Muted||Effects<=0)return;var clip=Clip("audio/"+id);if(clip==null)return;
            if(gate!=null){gain=(float)limiter.Cue(gate,Time.unscaledTime,gain);if(gain<=0)return;}
            int free=-1,keyed=0;for(int i=0;i<voices.Length;i++){if(!voices[i].isPlaying){if(free<0)free=i;}else if(keyedVoices[i])keyed++;}if(free<0||gate!=null&&keyed>=8)return;
            var voice=voices[free];keyedVoices[free]=gate!=null;float pan=0;
            if(position.HasValue){var d=position.Value-listener;gain*=NativeEventSounds.Falloff(d.magnitude);pan=NativeEventSounds.Pan(NativeArena.Vec(d),yaw);}
            voice.clip=clip;voice.volume=Mathf.Clamp01(gain*Effects);voice.panStereo=pan;voice.pitch=rate>0?rate:NativeEventSounds.Rate(id,UnityEngine.Random.value);voice.Play();
        }
        public void Entrance(string key,string style,float age)
        {
            if(key==null||style=="dev-siren"||Muted||Music<=0){introVoice.Stop();introKey="";return;}introVoice.volume=Music;
            if(introKey==key)return;introKey=key;introVoice.Stop();introVoice.clip=Clip("audio/intros/"+style);if(introVoice.clip==null||age>=introVoice.clip.length)return;introVoice.time=Math.Max(0,age);introVoice.Play();
        }
        private void MarkBeeps(NativeArena arena)
        {
            foreach(var mark in arena.Marks())
            {float at=mark.lockAt,gap=.3f;for(int i=0;i<40&&at<mark.throwAt-.02f;i++,at+=gap,gap=Math.Max(.07f,gap*.8f))
             {if(at>arena.Time+.08f)break;if(!Once("mark:"+mark.id+":"+i,at)||at<arena.Time-.08f||arena.Time>=mark.throwAt||Muted||Effects<=0)continue;
              int frequency=(int)(550+Mathf.Clamp01((at-mark.lockAt)/(mark.throwAt-mark.lockAt))*850);if(!beepClips.TryGetValue(frequency,out var clip)){float[] data=new float[2880];for(int n=0;n<data.Length;n++){float t=n/48000f;data[n]=Mathf.Sin(2*Mathf.PI*frequency*t)*.055f*Mathf.Pow(.001f/.055f,Math.Min(1,t/.055f));}clip=AudioClip.Create("Siren lock pulse",data.Length,1,48000,false);clip.SetData(data,0);beepClips[frequency]=clip;}var voice=Voice();if(voice==null)continue;voice.clip=clip;voice.volume=Effects;voice.panStereo=0;voice.pitch=1;voice.PlayDelayed(Math.Max(0,at-arena.Time));}
            }
        }
        private void OnDestroy(){ClearSceneAudio();ClearSlender();foreach(var c in beepClips.Values)CapturedAnimation.Release(c);}
        private bool Once(string key,float at){if(cues.TryGetValue(key,out float old)&&old==at)return false;cues[key]=at;return true;}
        private void Track(string id,float volume,bool pause=false,double? clock=null)
        {
            if(track!=id){music.Stop();track=id;music.clip=string.IsNullOrEmpty(id)?null:Clip(id);music.loop=clock==null;}
            music.volume=Muted?0:volume;if(music.clip==null)return;if(pause){music.Pause();return;}
            if(clock.HasValue&&Math.Abs(music.time-clock.Value)>.12)music.time=Mathf.Clamp((float)clock.Value,0,Math.Max(0,music.clip.length-.02f));if(!music.isPlaying)music.Play();
        }
        public void Render(NativeArena arena,bool menu,Camera camera)
        {
            // audio.ts update: falloff and pan are heard from the local pilot (`listener=self.pos`), turned by the view yaw.
            if(camera!=null){listener=camera.transform.position;yaw=camera.transform.eulerAngles.y*Mathf.Deg2Rad;}if(arena?.Local?.Body!=null)listener=arena.Local.Body.position;
            Slender(arena,menu);if(arena==null){Track("music/menu-afterglow",Menu);return;}
            if(generation!=arena.Generation){generation=arena.Generation;cursor=0;deathAudio.Tick(0,true);cues.Clear();limiter.Clear();spikeRises.Clear();phase="";countdown=-1;foreach(var v in voices)v.Stop();}
            var me=arena.Local;var match=arena.Match;float t=arena.Time;SceneCues(arena,menu);if(!menu)MarkBeeps(arena);
            if(menu)Track("music/menu-afterglow",Menu);
            else if(arena.BossSongTime>=0)Track("music/black-siren-protocol",Music*.55f,arena.Pve?.Holding==true&&arena.Pve.Cinematic!="intro",arena.BossTrack.Looped(arena.BossSongTime));
            else if(arena.Pve!=null)Track("music/restricted-zone-pulse",Music*.55f,match.Phase==NativePhase.Match);
            else
            {
                string theme=arena.Content.Map(arena.MapId).music;
                if(string.IsNullOrEmpty(theme)){if(!track.StartsWith("music/")||track=="music/menu-afterglow"||music.clip!=null&&!music.isPlaying){if(bag.Count==0){foreach(var name in rotationTracks.OrderBy(_=>UnityEngine.Random.value))bag.Enqueue(name);if(bag.Peek()==rotationTracks[Math.Max(0,rotation)])bag.Enqueue(bag.Dequeue());}theme=bag.Dequeue();rotation=Array.IndexOf(rotationTracks,theme);}else theme=track.Substring(6);}
                Track("music/"+theme,Music*.55f,match.Phase==NativePhase.Draft||match.Phase==NativePhase.Match);
            }
            // audio.ts AudioCursor: each event once, played only while .4 s fresh (never ahead of the clock), routed by
            // NativeEventSounds (ArenaAudio.update's event branch).
            var events=arena.Events;int newest=0;for(int i=0;i<events.Count;i++)newest=Math.Max(newest,events[i].Id);if(newest>0&&newest<cursor)cursor=0;
            for(int i=0;i<events.Count;i++){var e=events[i];if(e.Id<=cursor||t-e.Time>.4f||t<e.Time)continue;EventSounds(arena,events,i,me);}
            cursor=Math.Max(cursor,newest);
            foreach(var p in arena.Pilots)
            {
                string id=p.Seat.id+":";Vector3 pos=p.Body.position;if(p==me&&t<p.ScareUntil&&Once(id+"scare",p.ScareAt)){Play("darkness",null,.9f);Play("metal",null,.6f);}
                if(p.RollAt>0&&t-p.RollAt<.15f&&Once(id+"roll",p.RollAt))Play(p.Seat.spec=="ansatsuken"?"demon-step":"dash",pos,.45f);
                if(p.AttackAt>0&&t-p.AttackAt<.15f&&p.Weapon.@class=="melee"&&Once(id+"attack",p.AttackAt))Play(p.Seat.spec=="emperor"?(p.Fighter?.Kind=="electric"?"electric-hook":"arcade-punch"):p.Seat.spec=="ansatsuken"?"sword":p.Seat.spec=="siren"?"darkness":p.Seat.spec,pos,.4f);
                var cast=p.AbilityCast;string ability=cast?.Definition.id??p.Pose.AbilityId;float release=cast?.Release??p.Pose.AbilityRelease;if(ability!=null&&t>=release&&t-release<.2f&&Once(id+"cast",release)){Play(ability=="frag-grenade"||ability=="blink-strike"?"dash":ability=="rocket"?"arcade-heavy":ability=="orbital-mark"?"lightning":p.Seat.spec,pos,.6f);}
                if(p.Death?.style=="dev-raging-demon"){float[] hits={.80f,1.02f,1.24f,1.46f,1.69f,1.94f,2.21f,2.56f};for(int i=0;i<hits.Length;i++){float at=p.Death.at+hits[i];if(t>=at&&t-at<.12f&&Once(id+"demon:"+i,at))Play(i==7?"arcade-heavy":i==3||i==5?"arcade-kick":"arcade-punch",p.Death.position,i==7?1.1f:.85f);}}
                var f=p.Fighter;if(f?.LastHit>0&&t-f.LastHit<.15f&&Once(id+"fighter",f.LastHit))Play(f.Kind=="electric"?"electric-hook-hit":f.Kind=="tatsu"?"arcade-kick":f.Index==f.Total-1?"arcade-heavy":"arcade-punch",pos,.9f);
            }
            // Source events Unity derives from state: stage-spikes-rise (stage-hazards.ts:21) and the PvP draft pick's `install`
            // (simulation.ts:156, message = the chip), whose Mishima/arcade cue routes like the PvE Install.
            int rose=spikeRises.Step(arena.MapId,match.Phase==NativePhase.Fight&&match.Time-match.FightAt>=2,match.Time);
            for(int bed=0;rose!=0&&bed<NativeSpikeRises.Beds;bed++)if((rose&1<<bed)!=0){var s=NativeEventSounds.SpikeRise;Play(s.Clip,NativeArena.Vec(NativeSpikeRises.Position(bed)),s.Gain,s.Rate,s.Gate);}
            if(match.Phase==NativePhase.Draft&&match.Chosen!=null&&match.Time>=match.ChosenAt&&match.Time-match.ChosenAt<.4&&Once("draft-install",(float)match.ChosenAt))
            {int routes=NativeEventSounds.Route(new NativeEventSounds.Context{Kind=EventKind.Install,Detail=match.Chosen},routed);for(int i=0;i<routes;i++){var s=routed[i];if(s.Tone)ArcadeTone();else Play(s.Clip,null,s.Gain,s.Rate,s.Gate);}}
            int n=match.Phase==NativePhase.Countdown?(int)Math.Ceiling(match.PhaseEnd-match.Time):0;if(n>0&&n!=countdown)Play("countdown",null,.7f);countdown=n;
            string current=match.Phase.ToString();if(current!=phase){if(match.Phase==NativePhase.Fight)Play("go",null,.7f);if((match.Phase==NativePhase.Result||match.Phase==NativePhase.Match&&phase!="Result")&&(match.Winner!=0||arena.Pve?.Outcome!=null))Play(arena.Pve!=null?arena.Pve.Outcome=="victory"?"win":"lose":match.SameSide(me?.Seat,match.Seat(match.Winner))?"win":"lose",null,.65f);phase=current;}
            // audio.ts PvE stage cues: a wave start counts in, a clear or the possession sounds "go" (my revive is its event).
            var stage=arena.Pve;if(stage!=null&&!stage.Holding)
            {
                if(stage.Stage=="wave"&&Once("pve-wave",stage.Wave))Play("countdown",null,.6f);else if(stage.Stage=="intermission"&&stage.Wave>0&&Once("pve-clear",stage.Wave))Play("go",null,.6f);
                if(stage.Stage=="boss"&&stage.Phase==2&&stage.Hidden&&Once("pve-possess",stage.Phase))Play("go",null,.5f);
            }
            if(cues.Count>1024)cues.Clear();
        }
        // One fresh event: resolve what the router needs from the arena (pilots, prop material, the block-pulse, teleport and
        // Blink Strike pairings, the saw) and play its cues. A pilot's death keeps the DEV kill clips on top of the source's
        // cue, at rate 1 like audio.ts updateLove (its beats are timed to the clip).
        private void EventSounds(NativeArena arena,List<ArenaEvent> events,int index,NativePilot me)
        {
            var e=events[index];NativePilot source=null,target=null;foreach(var p in arena.Pilots){if(p.Seat.id==e.Source)source=p;if(p.Seat.id==e.Target)target=p;}
            int local=me?.Seat.id??0;string material=null;int supernova=0;
            if(e.Kind==EventKind.Impact||e.Kind==EventKind.Break)foreach(var prop in arena.Props)if(prop.Definition.id==e.SurfaceId){material=prop.Definition.material;break;}
            if(e.Kind==EventKind.BlockPulse)source?.Seat.chips?.TryGetValue("supernova",out supernova);
            var context=new NativeEventSounds.Context{Kind=e.Kind,Detail=e.Detail,SourceSpec=source?.Seat.spec,Material=material,Amount=e.Amount,Radius=e.Radius,
                TargetMe=local>0&&e.Target==local,SourceMe=local>0&&e.Source==local,TargetPilot=target!=null,Ally=source!=null&&target!=null&&arena.Match.SameSide(source.Seat,target.Seat),Pve=arena.Pve!=null,
                Supernova=supernova>0,Saw=e.Kind==EventKind.HazardHit&&NativeEventFeedback.SawHit(arena.MapId,e.Time,e.Position),
                Shadowed=e.Kind==EventKind.Explosion&&NativeEventFeedback.PulseBlast(events,index)||e.Kind==EventKind.Arc&&NativeEventFeedback.TeleportBeam(events,index),
                BlinkStrike=e.Kind==EventKind.Teleport&&NativeEventFeedback.BlinkStrike(events,index)};
            Vector3 pos=NativeArena.Vec(e.Position);int n=NativeEventSounds.Route(context,routed);
            for(int i=0;i<n;i++){var s=routed[i];if(s.Tone)ArcadeTone();else Play(s.Clip,s.Positional?pos:(Vector3?)null,s.Gain,s.Rate,s.Gate);}
            if(e.Kind==EventKind.Death&&target!=null){if(source?.Seat.killEffect=="dev-clown-love")Play("clown-love",pos,.8f,1);if(source?.Seat.killEffect=="dev-bass-van")Play("bass-van",pos,.8f,1);}
        }
    }
}
