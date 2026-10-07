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
        private readonly Dictionary<string,(float at,float pending,float activity)> gates=new Dictionary<string,(float,float,float)>();
        private readonly Dictionary<string,float> cues=new Dictionary<string,float>();
        private AudioSource[] voices;private AudioSource music,introVoice;private string introKey="";private readonly Dictionary<int,AudioClip> beepClips=new Dictionary<int,AudioClip>();private string track="",phase="";private int cursor,generation=-1,countdown=-1,rotation=-1;private Vector3 listener;private float yaw;
        private readonly string[] rotationTracks={"foundry-rush","orange-line","kinetic-city","afterburner","night-circuit","critical-mass","jump-drive","neon-recoil","overpass","last-set"};
        private readonly Queue<string> bag=new Queue<string>();
        public void ResetCursor(){generation=-1;}
        private void Awake(){voices=new AudioSource[24];for(int i=0;i<voices.Length;i++){voices[i]=gameObject.AddComponent<AudioSource>();voices[i].playOnAwake=false;voices[i].spatialBlend=0;}music=gameObject.AddComponent<AudioSource>();music.playOnAwake=false;music.priority=0;introVoice=gameObject.AddComponent<AudioSource>();introVoice.playOnAwake=false;}
        private AudioClip Clip(string id){if(!clips.TryGetValue(id,out var c)){c=Resources.Load<AudioClip>("Audio/"+id);clips[id]=c;}return c;}
        public void Play(string id,Vector3? position=null,float gain=1,float pitch=1,string gate=null)
        {
            if(Muted||Effects<=0)return;var clip=Clip("audio/"+id);if(clip==null)return;float now=Time.unscaledTime;
            if(gate!=null){gates.TryGetValue(gate,out var g);float gap=gate=="hit"?.06f:gate=="hurt"?.08f:gate=="explosion"?.07f:.05f;float dt=now-g.at;if(g.at>0&&dt<gap){gates[gate]=(g.at,Math.Min(2,g.pending+gain),g.activity);return;}float act=g.activity*Mathf.Exp(-dt/.4f)+1;float adjusted=Math.Min(gain*1.5f,gain+g.pending*.3f)/(1+.15f*(act-1));gates[gate]=(now,0,act);gain=Math.Max(gain*.35f,adjusted)*.8f;}
            var voice=voices.FirstOrDefault(v=>!v.isPlaying);if(voice==null)return;float pan=0;
            if(position.HasValue){var d=position.Value-listener;gain/=1+Math.Max(0,d.magnitude-2)*.13f;pan=Mathf.Clamp((-Mathf.Cos(yaw)*d.x+Mathf.Sin(yaw)*d.z)/Math.Max(2,d.magnitude),-.85f,.85f);}
            voice.clip=clip;voice.volume=Mathf.Clamp01(gain*Effects);voice.panStereo=pan;voice.pitch=pitch;voice.Play();
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
              int frequency=(int)(550+Mathf.Clamp01((at-mark.lockAt)/(mark.throwAt-mark.lockAt))*850);if(!beepClips.TryGetValue(frequency,out var clip)){float[] data=new float[2880];for(int n=0;n<data.Length;n++){float t=n/48000f;data[n]=Mathf.Sin(2*Mathf.PI*frequency*t)*.055f*Mathf.Pow(.001f/.055f,Math.Min(1,t/.055f));}clip=AudioClip.Create("Siren lock pulse",data.Length,1,48000,false);clip.SetData(data,0);beepClips[frequency]=clip;}var voice=voices.FirstOrDefault(v=>!v.isPlaying);if(voice==null)continue;voice.clip=clip;voice.volume=Effects;voice.panStereo=0;voice.pitch=1;voice.PlayDelayed(Math.Max(0,at-arena.Time));}
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
            if(camera!=null){listener=camera.transform.position;yaw=camera.transform.eulerAngles.y*Mathf.Deg2Rad;}
            Slender(arena,menu);if(arena==null){Track("music/menu-afterglow",Menu);return;}
            if(generation!=arena.Generation){generation=arena.Generation;cursor=0;deathAudio.Tick(0,true);cues.Clear();gates.Clear();phase="";countdown=-1;foreach(var v in voices)v.Stop();}
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
            foreach(var e in arena.Events.OrderBy(e=>e.Id))
            {
                if(e.Id<=cursor)continue;cursor=e.Id;if(t-e.Time>.4f||t<e.Time)continue;Vector3 pos=NativeArena.Vec(e.Position);var source=arena.Pilots.Find(p=>p.Seat.id==e.Source);string spec=source?.Seat.spec;
                switch(e.Kind)
                {
                    case EventKind.Shot:if(spec!="siren")Play(spec=="leet"||spec=="emperor"?"lightning":spec=="ansatsuken"?"fire":spec,pos,.65f);break;
                    case EventKind.Block:Play(spec=="ansatsuken"?"arcade-parry":"parry",pos,.65f);break;
                    case EventKind.ReloadStart:case EventKind.ReloadEnd:if(e.Source==me?.Seat.id)Play(e.Kind==EventKind.ReloadStart?"reload-start":"reload-end",null,.6f);break;
                    case EventKind.Hit:case EventKind.Headshot:if(e.Target==me?.Seat.id)Play("hurt",null,.6f,1,"hurt");else if(e.Source==me?.Seat.id)Play("hit",null,e.Target>=10000?.55f:.9f,1,"hit");break;
                    case EventKind.Impact:case EventKind.Break:var prop=arena.Props.Find(p=>p.Definition.id==e.SurfaceId);string mat=prop?.Definition.material;Play(mat=="wood"?"wood":mat=="metal"?"metal":"stone",pos,e.Kind==EventKind.Break?.65f:.22f,1,"impact");break;
                    case EventKind.Death:Play("stone",pos,.65f);Play("explosion",pos,.3f);if(source?.Seat.killEffect=="dev-clown-love")Play("clown-love",pos,.8f);if(source?.Seat.killEffect=="dev-bass-van")Play("bass-van",pos,.8f);break;
                }
            }
            foreach(var p in arena.Pilots)
            {
                string id=p.Seat.id+":";Vector3 pos=p.Body.position;if(p==me&&t<p.ScareUntil&&Once(id+"scare",p.ScareAt)){Play("darkness",null,.9f);Play("metal",null,.6f);}
                if(p.RollAt>0&&t-p.RollAt<.15f&&Once(id+"roll",p.RollAt))Play(p.Seat.spec=="ansatsuken"?"demon-step":"dash",pos,.45f);
                if(p.AttackAt>0&&t-p.AttackAt<.15f&&p.Weapon.@class=="melee"&&Once(id+"attack",p.AttackAt))Play(p.Seat.spec=="emperor"?(p.Fighter?.Kind=="electric"?"electric-hook":"arcade-punch"):p.Seat.spec=="ansatsuken"?"sword":p.Seat.spec=="siren"?"darkness":p.Seat.spec,pos,.4f);
                var cast=p.AbilityCast;string ability=cast?.Definition.id??p.Pose.AbilityId;float release=cast?.Release??p.Pose.AbilityRelease;if(ability!=null&&t>=release&&t-release<.2f&&Once(id+"cast",release)){Play(ability=="frag-grenade"||ability=="blink-strike"?"dash":ability=="rocket"?"arcade-heavy":ability=="orbital-mark"?"lightning":p.Seat.spec,pos,.6f);}
                if(p.Death?.style=="dev-raging-demon"){float[] hits={.80f,1.02f,1.24f,1.46f,1.69f,1.94f,2.21f,2.56f};for(int i=0;i<hits.Length;i++){float at=p.Death.at+hits[i];if(t>=at&&t-at<.12f&&Once(id+"demon:"+i,at))Play(i==7?"arcade-heavy":i==3||i==5?"arcade-kick":"arcade-punch",p.Death.position,i==7?1.1f:.85f);}}
                var f=p.Fighter;if(f?.LastHit>0&&t-f.LastHit<.15f&&Once(id+"fighter",f.LastHit))Play(f.Kind=="electric"?"electric-hook-hit":f.Kind=="tatsu"?"arcade-kick":f.Index==f.Total-1?"arcade-heavy":"arcade-punch",pos,.9f);
            }
            int n=match.Phase==NativePhase.Countdown?(int)Math.Ceiling(match.PhaseEnd-match.Time):0;if(n>0&&n!=countdown)Play("countdown",null,.7f);countdown=n;
            string current=match.Phase.ToString();if(current!=phase){if(match.Phase==NativePhase.Fight)Play("go",null,.7f);if(match.Phase==NativePhase.Result||match.Phase==NativePhase.Match&&phase!="Result")Play(arena.Pve!=null?arena.Pve.Outcome=="victory"?"win":"lose":match.SameSide(me?.Seat,match.Seat(match.Winner))?"win":"lose",null,.65f);phase=current;}
            if(cues.Count>1024)cues.Clear();
        }
    }
}
