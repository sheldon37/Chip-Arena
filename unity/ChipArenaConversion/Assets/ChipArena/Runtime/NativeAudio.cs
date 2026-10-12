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
        private readonly NativeEventSounds.Sound[] routed=new NativeEventSounds.Sound[NativeEventSounds.MaxSounds];private readonly NativeKitSound[] kitSounds=new NativeKitSound[NativeMeleeKitSounds.Max];private readonly NativeSpikeRises spikeRises=new NativeSpikeRises();private readonly NativeInstallCue installCue=new NativeInstallCue();
        private readonly Dictionary<string,float> cues=new Dictionary<string,float>();
        private AudioSource[] voices;private AudioSource music,introVoice;private string introKey="";private float introGain=1;private readonly Dictionary<int,AudioClip> beepClips=new Dictionary<int,AudioClip>();private string track="",phase="",resumeTrack;private float resumeTime;private bool musicHeld;private int cursor,generation=-1,countdown=-1,rotation=-1;private Vector3 listener;private float yaw;
        private readonly string[] rotationTracks={"foundry-rush","orange-line","kinetic-city","afterburner","night-circuit","critical-mass","jump-drive","neon-recoil","overpass","last-set"};
        private readonly Queue<string> bag=new Queue<string>();
        private readonly NativeMusicSync musicSync=new NativeMusicSync();private bool musicPaused,musicStarting,musicPlayed;private double musicStartedAt,musicEpoch;private object musicClockOwner;
        private double MusicPosition=>music.clip!=null?NativeMusicSync.Position(music.timeSamples,music.clip.frequency):-1;
        private void SeekMusic(double seconds){music.timeSamples=NativeMusicSync.Sample(seconds,music.clip.frequency,music.clip.samples);}
        public void ResetCursor(){generation=-1;}
        private void Awake(){NativeUi.Sound+=UiHeard;voices=new AudioSource[24];keyedVoices=new bool[voices.Length];for(int i=0;i<voices.Length;i++){voices[i]=gameObject.AddComponent<AudioSource>();voices[i].playOnAwake=false;voices[i].spatialBlend=0;}music=gameObject.AddComponent<AudioSource>();music.playOnAwake=false;music.priority=0;introVoice=gameObject.AddComponent<AudioSource>();introVoice.playOnAwake=false;}
        // A free voice for an unlimited, unpanned play (beeps, death tones); Play counts the limited cue voices itself.
        private AudioSource Voice(){for(int i=0;i<voices.Length;i++)if(!voices[i].isPlaying){keyedVoices[i]=false;voices[i].loop=false;return voices[i];}return null;}
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
            voice.loop=false;voice.clip=clip;voice.volume=Mathf.Clamp01(gain*Effects);voice.panStereo=pan;voice.pitch=rate>0?rate:NativeEventSounds.Rate(id,UnityEngine.Random.value);voice.Play();
        }
        // The entrance's music from intro age `age` (a late joiner seeks into it): the installed LocalAssets clip of the style's
        // intro key when there is one (IntroClip, with its manifest gain trim), else today's sting at the music volume.
        public void Entrance(string key,string style,float age)
        {
            string sting=NativeIntroRules.IntroSting(style);
            if(key==null||sting==null||Muted||Music<=0){introVoice.Stop();introKey="";return;}introVoice.volume=Mathf.Clamp01(Music*introGain);
            if(introKey==key)return;introKey=key;introVoice.Stop();introVoice.clip=IntroClip(style,sting,out introGain);introVoice.volume=Mathf.Clamp01(Music*introGain);if(introVoice.clip==null||age>=introVoice.clip.length)return;introVoice.time=Math.Max(0,age);introVoice.Play();
        }
        // audio.ts previewIntro: the Customize preview holds the menu music; the dev-siren entrance plays its boss song from
        // the preview's age instead of a sting (the cell's flashes are cued to it), every other entrance its sting.
        // The track the song takes over (the menu music) resumes where it was when the preview closes, as the source's paused
        // menu element does (music.ts setMenuActive), instead of restarting from 0:00.
        public void PreviewIntro(string key,string style,float age)
        {
            string song=NativeIntroRules.IntroSong(style);
            if(song==null){Entrance(key,style,age);HoldMusic();return;}
            Entrance(null,null,0);string from=track;float at=(float)Math.Max(0,MusicPosition);Track(song,Music*.55f,false,age,key);
            if(from!=song&&!string.IsNullOrEmpty(from)){resumeTrack=from;resumeTime=at;}
        }
        /// <summary>
        /// The playback time (s) of music track `id` while it plays (muted or not), else -1: the boss song's beats as this machine
        /// hears them, which the dev-siren entrance is drawn on (NativeIntroClock).
        /// </summary>
        public double SongPlayback(string id)=>id!=null&&track==id&&music!=null&&music.clip!=null&&music.isPlaying?MusicPosition:-1;
        private void MarkBeeps(NativeArena arena)
        {
            foreach(var mark in arena.Marks())
            {float at=mark.lockAt,gap=.3f;for(int i=0;i<40&&at<mark.throwAt-.02f;i++,at+=gap,gap=Math.Max(.07f,gap*.8f))
             {if(at>arena.Time+.08f)break;if(!Once("mark:"+mark.id+":"+i,at)||at<arena.Time-.08f||arena.Time>=mark.throwAt||Muted||Effects<=0)continue;
              int frequency=(int)(550+Mathf.Clamp01((at-mark.lockAt)/(mark.throwAt-mark.lockAt))*850);if(!beepClips.TryGetValue(frequency,out var clip)){float[] data=new float[2880];for(int n=0;n<data.Length;n++){float t=n/48000f;data[n]=Mathf.Sin(2*Mathf.PI*frequency*t)*.055f*Mathf.Pow(.001f/.055f,Math.Min(1,t/.055f));}clip=AudioClip.Create("Siren lock pulse",data.Length,1,48000,false);clip.SetData(data,0);beepClips[frequency]=clip;}var voice=Voice();if(voice==null)continue;voice.clip=clip;voice.volume=Effects;voice.panStereo=0;voice.pitch=1;voice.PlayDelayed(Math.Max(0,at-arena.Time));}
            }
        }
        private void OnDestroy(){NativeUi.Sound-=UiHeard;ClearSceneAudio();ClearSlender();ClearHoverClips();foreach(var c in beepClips.Values)CapturedAnimation.Release(c);}
        private bool Once(string key,float at){if(cues.TryGetValue(key,out float old)&&old==at)return false;cues[key]=at;return true;}
        private void Track(string id,float volume,bool pause=false,double? clock=null,object clockOwner=null,double clockEpoch=0)
        {
            if(track!=id||clock.HasValue&&(!Equals(musicClockOwner,clockOwner)||musicEpoch!=clockEpoch))
            {
                if(track!=id&&id!=resumeTrack)resumeTrack=null;
                music.Stop();musicPaused=musicStarting=musicPlayed=false;musicSync.Reset();track=id;music.clip=string.IsNullOrEmpty(id)?null:Clip(id);
            }
            musicClockOwner=clockOwner;musicEpoch=clockEpoch;
            music.volume=Muted?0:volume;musicHeld=pause;if(music.clip==null)return;music.loop=clock==null;
            if(pause){HoldMusic();return;}
            // Do not repeatedly Play or seek a clip still loading in the background. Once ready, cue the latest age,
            // not the age at which Resources.Load was first requested (especially on a guest joining the entrance).
            if(music.clip.loadState==AudioDataLoadState.Unloaded)music.clip.LoadAudioData();
            if(music.clip.loadState!=AudioDataLoadState.Loaded)return;
            if(musicPaused){music.UnPause();musicPaused=false;}
            double now=AudioSettings.dspTime;
            if(musicStarting){if(music.isPlaying)musicStarting=false;else if(now-musicStartedAt<1)return;else musicStarting=false;}
            if(!music.isPlaying)
            {
                if(clock.HasValue)SeekMusic(clock.Value);else if(id==resumeTrack)SeekMusic(resumeTime);
                if(id==resumeTrack)resumeTrack=null;musicSync.Reset();music.Play();musicPlayed=musicStarting=true;musicStartedAt=now;return;
            }
            if(clock.HasValue&&musicSync.Seek(MusicPosition,clock.Value,now))SeekMusic(clock.Value);
        }
        // main.ts intro-preview: neither the menu nor the arena music plays under a previewed intro (setMenuActive(false) and
        // previewIntro's setActive(false)); the next Render resumes the held track.
        public void HoldMusic(){musicHeld=true;if(music.isPlaying||musicStarting){music.Pause();musicPaused=true;musicStarting=false;}}
        public void Render(NativeArena arena,bool menu,Camera camera)
        {
            // audio.ts update: falloff and pan are heard from the local pilot (`listener=self.pos`), turned by the view yaw.
            if(camera!=null){listener=camera.transform.position;yaw=camera.transform.eulerAngles.y*Mathf.Deg2Rad;}if(arena?.Local?.Body!=null)listener=arena.Local.Body.position;
            Slender(arena,menu);StructureQueue(arena);EffectsFrame(arena,menu);if(arena==null){Track("music/menu-afterglow",Menu);return;}
            if(generation!=arena.Generation){generation=arena.Generation;cursor=0;deathAudio.Tick(0,true);cues.Clear();limiter.Clear();spikeRises.Clear();installCue.Clear();phase="";countdown=-1;foreach(var v in voices){v.Stop();v.loop=false;}}
            var me=arena.Local;var match=arena.Match;float t=arena.Time;SceneCues(arena,menu);if(!menu)MarkBeeps(arena);
            if(menu)Track("music/menu-afterglow",Menu);
            else if(arena.BossSongTime>=0)Track("music/black-siren-protocol",Music*.55f,arena.Pve?.Holding==true&&arena.Pve.Cinematic!="intro",arena.BossTrack.Looped(arena.BossSongTime),arena,arena.BossSongAt);
            else if(arena.Pve!=null)Track("music/restricted-zone-pulse",Music*.55f*NativeStageMusic.Dip(match.Phase,true),!NativeStageMusic.Plays(match.Phase,true,false));
            else
            {
                string theme=arena.Content.Map(arena.MapId).music;
                if(string.IsNullOrEmpty(theme)){if(!track.StartsWith("music/")||track=="music/menu-afterglow"||music.clip!=null&&musicPlayed&&!musicStarting&&!music.isPlaying&&!musicHeld){if(bag.Count==0){foreach(var name in rotationTracks.OrderBy(_=>UnityEngine.Random.value))bag.Enqueue(name);if(bag.Peek()==rotationTracks[Math.Max(0,rotation)])bag.Enqueue(bag.Dequeue());}theme=bag.Dequeue();rotation=Array.IndexOf(rotationTracks,theme);}else theme=track.Substring(6);}
                Track("music/"+theme,Music*.55f,!NativeStageMusic.Plays(match.Phase,false,false));
            }
            FactoryCues(arena,menu);
            // audio.ts AudioCursor: each event once, played only while .4 s fresh (never ahead of the clock), routed by
            // NativeEventSounds (ArenaAudio.update's event branch).
            // The Siren pick's follow-through holds my own Install cue for its plant (NativeInstallCue).
            string held=installCue.Step(FollowChip,FollowStarted,FollowAge);if(held!=null)InstallCue(held);
            var events=arena.Events;int newest=0;for(int i=0;i<events.Count;i++)newest=Math.Max(newest,events[i].Id);if(newest>0&&newest<cursor)cursor=0;
            for(int i=0;i<events.Count;i++){var e=events[i];if(e.Id<=cursor||t-e.Time>.4f||t<e.Time)continue;EventSounds(arena,events,i,me);}
            cursor=Math.Max(cursor,newest);BowFlightCues(arena);
            foreach(var p in arena.Pilots)
            {
                string id=p.Seat.id+":";Vector3 pos=p.Body.position;if(p==me&&t<p.ScareUntil&&Once(id+"scare",p.ScareAt)){Play("darkness",null,.9f);Play("metal",null,.6f);}bool mine=p==me;
                if(p.RollAt>0&&t-p.RollAt<.15f&&Once(id+"roll",p.RollAt)){string roll=p.Seat.spec=="ansatsuken"?"demon-step":"dash";Cue(NativeSoundCues.Roll(roll),roll,pos,.45f,mine:mine);}
                if(p.AttackAt>0&&t-p.AttackAt<.15f&&p.Weapon.@class=="melee"&&Once(id+"attack",p.AttackAt)){string swing=p.Seat.spec=="emperor"?(p.Fighter?.Kind=="electric"?"electric-hook":"arcade-punch"):p.Seat.spec=="ansatsuken"?"sword":p.Seat.spec=="siren"?"darkness":p.Seat.spec;Cue(NativeSoundCues.Swing(p.Seat.spec,swing),swing,pos,.4f,cover:NativeSoundCues.SwingCover(p.Seat.spec),mine:mine);}
                var cast=p.AbilityCast;string ability=cast?.Definition.id??p.Pose.AbilityId;float release=cast?.Release??p.Pose.AbilityRelease;if(ability!=null&&!NativeRangedRules.Q(ability)&&t>=release&&t-release<.2f&&Once(id+"cast",release)){string clip=ability=="frag-grenade"||ability=="blink-strike"?"dash":ability=="rocket"?"arcade-heavy":ability=="orbital-mark"?"lightning":p.Seat.spec;Cue(NativeSoundCues.Cast(ability,p.Seat.spec,out string covered),clip,pos,.6f,cover:covered,mine:mine);}
                if(p.Death?.style=="dev-raging-demon"){float[] hits={.80f,1.02f,1.24f,1.46f,1.69f,1.94f,2.21f,2.56f};for(int i=0;i<hits.Length;i++){float at=p.Death.at+hits[i];if(t>=at&&t-at<.12f&&Once(id+"demon:"+i,at)){string beat=i==7?"arcade-heavy":i==3||i==5?"arcade-kick":"arcade-punch";Cue(NativeSoundCues.Fighter(beat),beat,p.Death.position,i==7?1.1f:.85f);}}}
                // A melee Q/E cast's release (the dash, throw, slash or leap leaving): the replicated kit's Release, once per cast.
                var kit=p.Pose?.MeleeKit;if(kit!=null&&MeleeKitMotion.Known(kit.Kind)&&t>=kit.Release&&t-kit.Release<.15f&&kit.Phase!="windup"&&kit.Phase!="recast"&&Once(id+"kit",kit.Release)){bool recast=kit.Phase=="fall"||p.Pose.SpecialKind=="skyfall"||p.Pose.SpecialKind=="kunai-recast";int k=NativeMeleeKitSounds.Release(kit.Kind,recast,kitSounds);for(int i=0;i<k;i++)Cue(kitSounds[i].Key,kitSounds[i].Clip,pos,kitSounds[i].Gain,mine:mine);}
                // The Whirlwind's whoosh each turn and its heavy stop (Core/NativeMeleeKitSounds.Spin), each cue once.
                if(p.Pose!=null){int w=NativeMeleeKitSounds.Spin(p.Pose,t,.15f,kitSounds,out float whirlAt);if(w>0&&Once(id+"whirl",whirlAt))for(int i=0;i<w;i++)Cue(kitSounds[i].Key,kitSounds[i].Clip,pos,kitSounds[i].Gain,mine:mine);}
                // The pistol / machine-gun Q kit (Core/NativeGunKit): each fanned round's hammer slap (the round is the normal shot
                // cue), Overdrive's spin-up whine and the overheat vent's hiss, from the replicated kit times, once each.
                var gun=NativeGunKit.Of(p.Pose,t);
                for(int r=0;r<gun.FanShots;r++){float at=gun.FanShot(r);if(t>=at&&t-at<.1f&&Once(id+"fan"+r,gun.FanAt))Cue(NativeGunKit.Slap.Key,NativeGunKit.Slap.Clip,pos,NativeGunKit.Slap.Gain,mine:mine);}
                if(gun.HasOverdrive&&t>=gun.OverdriveAt&&t-gun.OverdriveAt<.2f&&Once(id+"overdrive",gun.OverdriveAt))Cue(NativeGunKit.Whine.Key,NativeGunKit.Whine.Clip,pos,NativeGunKit.Whine.Gain,NativeGunKit.WhineRate,mine:mine);
                if(gun.HasOverdrive&&t>=gun.OverdriveUntil&&t-gun.OverdriveUntil<.2f&&Once(id+"vent",gun.OverdriveUntil))Cue(NativeGunKit.Hiss.Key,NativeGunKit.Hiss.Clip,pos,NativeGunKit.Hiss.Gain,mine:mine);
                // The DEV Cyber Lord's state cues (Core/NativeCyberLordSounds), each once.
                CyberLordCues(p,pos,mine,t);
                BowCues(p,id,t,pos,mine);
                var f=p.Fighter;if(f?.LastHit>0&&t-f.LastHit<.15f&&Once(id+"fighter",f.LastHit)){string hit=f.Kind=="electric"?"electric-hook-hit":f.Kind=="tatsu"?"arcade-kick":f.Index==f.Total-1?"arcade-heavy":"arcade-punch";Cue(NativeSoundCues.Fighter(hit),hit,pos,.9f,mine:mine);}
            }
            // Source events Unity derives from state: stage-spikes-rise (stage-hazards.ts:21) and the PvP draft pick's `install`
            // (simulation.ts:156, message = the chip), whose Mishima/arcade cue routes like the PvE Install.
            int rose=spikeRises.Step(arena.MapId,match.Phase==NativePhase.Fight&&match.Time-match.FightAt>=2,match.Time);
            for(int bed=0;rose!=0&&bed<NativeSpikeRises.Beds;bed++)if((rose&1<<bed)!=0){var s=NativeEventSounds.SpikeRise;Cue("hazard.spikes",s.Clip,NativeArena.Vec(NativeSpikeRises.Position(bed)),s.Gain,s.Rate,s.Gate);}
            // It lands on the stage's wrist plant (NativeDraftPlant.SoundAt after the pick), as the pilot presses the chip in.
            if(match.Phase==NativePhase.Draft&&match.Chosen!=null&&NativeInstallCue.DraftDue(match.ChosenAt,match.Time)&&Once("draft-install",(float)match.ChosenAt))InstallCue(match.Chosen);
            int n=match.Phase==NativePhase.Countdown?(int)Math.Ceiling(match.PhaseEnd-match.Time):0;if(n>0&&n!=countdown)Cue("ui.countdown","countdown",null,.7f);countdown=n;
            string current=match.Phase.ToString();if(current!=phase){if(match.Phase==NativePhase.Fight)Cue("ui.go","go",null,.7f);if((match.Phase==NativePhase.Result||match.Phase==NativePhase.Match&&phase!="Result")&&(match.Winner!=0||arena.Pve?.Outcome!=null)){string result=arena.Pve!=null?arena.Pve.Outcome=="victory"?"win":"lose":match.SameSide(me?.Seat,match.Seat(match.Winner))?"win":"lose";Cue(result=="win"?"ui.round-won":"ui.round-lost",result,null,.65f);}phase=current;}
            // audio.ts PvE stage cues: a wave start counts in, a clear or the possession sounds "go" (my revive is its event).
            var stage=arena.Pve;if(stage!=null&&!stage.Holding)
            {
                if(stage.Stage=="wave"&&Once("pve-wave",stage.Wave))Cue("pve.wave","countdown",null,.6f);else if(stage.Stage=="intermission"&&stage.Wave>0&&Once("pve-clear",stage.Wave))Cue("pve.clear","go",null,.6f);
                if(stage.Stage=="boss"&&stage.Phase==2&&stage.Hidden&&Once("pve-possess",stage.Phase))Cue("siren.reveal","go",null,.5f);
            }
            if(cues.Count>1024)cues.Clear();
        }
        // The local Siren / Ward Zero pick follow-through (NativeDraftFollow sets these each frame; FollowChip null when none).
        [NonSerialized] public string FollowChip;[NonSerialized] public float FollowStarted,FollowAge;
        private void InstallCue(string chip){int routes=NativeEventSounds.Route(new NativeEventSounds.Context{Kind=EventKind.Install,Detail=chip},routed);for(int i=0;i<routes;i++){var s=routed[i];if(s.Tone)ArcadeTone();else Play(s.Clip,null,s.Gain,s.Rate,s.Gate);}if(routes==0&&!NativeSoundCues.DevInstall(chip))Cue("ui.draft.plant",null,null,.6f,mine:true);}
        // One fresh event: resolve what the router needs from the arena (pilots, an impact's prop material; a Break carries
        // its material, an Explosion its fx tag, a HazardHit its hazard kind, a Teleport/Arc its source) and play its cues.
        // A pilot's death keeps the DEV kill clips on top of the source's cue, at rate 1 like audio.ts updateLove (its beats
        // are timed to the clip).
        private void EventSounds(NativeArena arena,List<ArenaEvent> events,int index,NativePilot me)
        {
            var e=events[index];
            if(e.Kind==EventKind.StructureStress){StructureEvent(NativeStructureCue.Stress,e);return;}
            if(e.Kind==EventKind.StructureFall){StructureEvent(NativeStructureCue.Fall,e);return;}
            if(e.Kind==EventKind.StructureLand){StructureEvent(NativeStructureCue.Land,e);return;}
            NativePilot source=null,target=null;foreach(var p in arena.Pilots){if(p.Seat.id==e.Source)source=p;if(p.Seat.id==e.Target)target=p;}
            int local=me?.Seat.id??0;if(e.Kind==EventKind.Install&&local>0&&e.Source==local&&installCue.Hold(e.Detail))return;string material=e.Kind==EventKind.Break?e.Detail:null;int supernova=0;
            if(e.Kind==EventKind.Impact)foreach(var prop in arena.AllBlocks)if(prop.Definition.id==e.SurfaceId){material=prop.Definition.material;break;}
            if(e.Kind==EventKind.BlockPulse)source?.Seat.chips?.TryGetValue("supernova",out supernova);
            var context=new NativeEventSounds.Context{Kind=e.Kind,Detail=e.Detail,SourceSpec=source?.Seat.spec,Material=material,Amount=e.Amount,Radius=e.Radius,
                TargetMe=local>0&&e.Target==local,SourceMe=local>0&&e.Source==local,TargetPilot=target!=null,Ally=source!=null&&target!=null&&arena.Match.SameSide(source.Seat,target.Seat),Pve=arena.Pve!=null,
                Supernova=supernova>0};
            Vector3 pos=NativeArena.Vec(e.Position);BowEventCue(arena,e,source,pos);int n=NativeEventSounds.Route(context,routed);
            // The bank plan (NativeSoundPlan): with no LocalAssets bank, exactly the routed sounds above.
            string style=e.Kind!=EventKind.Death?null:target?.Death?.style??(string.IsNullOrEmpty(source?.Seat.killEffect)?null:source.Seat.killEffect);
            var detail=new NativeSoundDetail{WeaponDetail=e.Kind==EventKind.Explosion&&WeaponDetail(arena,e.Detail),KillStyle=style};
            int plays=NativeSoundPlan.Event(bank,context,detail,routed,n,planned);Vector3? from=source?.Body!=null?source.Body.position:(Vector3?)null;
            for(int i=0;i<plays;i++)Perform(planned[i],pos,from,arena.Time-e.Time,context.SourceMe||context.TargetMe);
            // The melee Q/E kit's cues (Core/NativeMeleeKitSounds): bank keys with today's built-in clips as their fallback.
            if(NativeMeleeKitFx.Kinds(e.Kind)&&NativeMeleeKitFx.Tag(e.Detail)){int k=NativeMeleeKitSounds.Event(e.Kind,e.Detail,e.Amount,context.TargetMe,kitSounds);for(int i=0;i<k;i++)Cue(kitSounds[i].Key,kitSounds[i].Clip,pos,kitSounds[i].Gain,mine:context.SourceMe||context.TargetMe);}
            CyberLordEvent(e,pos,context.TargetMe,context.SourceMe||context.TargetMe);
            if(e.Kind==EventKind.Death&&target!=null){if(source?.Seat.killEffect=="dev-clown-love")Play("clown-love",pos,.8f,1);if(source?.Seat.killEffect=="dev-bass-van")Play("bass-van",pos,.8f,1);}
        }
    }
}
