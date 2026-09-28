import {IntroAudio} from './intro-audio.js';
import {ArenaMusic} from './music.js';
import {CLOWN_LOVE} from './clown-love-timing.js';
import {BASS_VAN} from './bass-van-timing.js';
import type {GameEvent,Snapshot} from '../shared/simulation.js';
import type {Vec} from '../shared/config.js';
export const AUDIO_CLIPS=['pistol','rifle','machinegun','fire','water','lightning','darkness','sword','daggers','shield','greatsword','reload-start','reload-end','dash','jump','hit','hurt','parry','wood','stone','metal','explosion','burn-tick','poison-tick','ui','install','countdown','go','win','lose'] as const;
export type Sound=typeof AUDIO_CLIPS[number];
/** Event IDs are server-issued; never replay old snapshots or reconnect history. */
export class AudioCursor {
 last=0;
 consume(events:GameEvent[],time:number){const fresh:GameEvent[]=[];for(const e of events){if(e.id<=this.last)continue;this.last=e.id;if(time-e.time<=.4&&time>=e.time)fresh.push(e);}return fresh;}
 reset(){this.last=0;}
}
export class ArenaAudio {
 music=new ArenaMusic();intros=new IntroAudio();
 private loveBuffer:AudioBuffer|null=null;private vanBuffer:AudioBuffer|null=null;private loveSources=new Map<string,Set<AudioBufferSourceNode>>();private loveSeen=new Set<string>();private loveEpoch='';
 context:AudioContext|null=null;master:GainNode|null=null;buffers=new Map<Sound,AudioBuffer>();voices=new Set<AudioBufferSourceNode>();cursor=new AudioCursor();loading:Promise<void>|null=null;volume=.3;muted=false;played=0;error='';phase='';countdown=-1;listener:Vec={x:0,y:0,z:0};yaw=0;
 configure(volume:number,muted:boolean){this.volume=Math.max(0,Math.min(1,volume));this.muted=muted;if(this.master&&this.context)this.master.gain.setTargetAtTime(muted?0:this.volume,this.context.currentTime,.015);}
 async unlock(){
  if(!this.context)try{this.context=new AudioContext();this.master=this.context.createGain();const limiter=this.context.createDynamicsCompressor();limiter.threshold.value=-13;limiter.knee.value=14;limiter.ratio.value=5;limiter.attack.value=.004;limiter.release.value=.15;this.master.connect(limiter).connect(this.context.destination);this.configure(this.volume,this.muted);}catch{this.error='Audio unavailable';return;}
  try{await this.context.resume();}catch{this.error='Click Test Sound to enable audio';}
  if(!this.loading)this.loading=Promise.all(AUDIO_CLIPS.map(async name=>{const response=await fetch(`${(import.meta as any).env.BASE_URL}audio/${name}.wav`);if(!response.ok)throw Error(name);const buffer=await this.context!.decodeAudioData(await response.arrayBuffer());this.buffers.set(name,buffer);})).then(()=>{this.error='';}).catch(()=>{this.error='Some sound files could not load';});
  await this.loading;
  if(!this.loveBuffer)try{const r=await fetch(`${(import.meta as any).env.BASE_URL}audio/clown-love.mp3`);if(r.ok)this.loveBuffer=await this.context.decodeAudioData(await r.arrayBuffer());}catch{/* Ordinary game sounds remain available if the optional clip fails. */}
  if(!this.vanBuffer)try{const r=await fetch(`${(import.meta as any).env.BASE_URL}audio/bass-van.mp3`);if(r.ok)this.vanBuffer=await this.context.decodeAudioData(await r.arrayBuffer());}catch{/* Other effects remain available if the optional clip fails. */}
  void this.intros.load(this.context,(import.meta as any).env.BASE_URL);this.music.unlock();
 }
 private stopLove(){for(const sources of this.loveSources.values())for(const source of sources)source.stop();this.loveSources.clear();this.loveSeen.clear();this.music.setDucked(false);}
 private updateLove(state:Snapshot){
  const epoch=String(state.generation)+':'+state.setNumber+':'+state.round;if(epoch!==this.loveEpoch){this.stopLove();this.loveEpoch=epoch;}
  const deaths=state.players.filter(p=>p.hp<=0&&p.death&&['dev-clown-love','dev-bass-van'].includes(p.death.effect)&&state.time>=p.death.at&&state.time-p.death.at<CLOWN_LOVE.end).map(p=>p.death!);
  for(const [id,sources]of this.loveSources)if(!deaths.some(d=>d.id===id)){for(const source of sources)source.stop();this.loveSources.delete(id);}
  this.music.setDucked(!this.muted&&this.volume>0&&deaths.some(d=>d.effect==='dev-bass-van'?!!this.vanBuffer&&state.time-d.at>=BASS_VAN.musicStart:!!this.loveBuffer));
  const context=this.context,master=this.master,hit=this.buffers.get('hit');if(!context||context.state!=='running'||!master)return;
  for(const death of deaths){const van=death.effect==='dev-bass-van',age=state.time-death.at,now=context.currentTime;
   const schedule=(key:string,buffer:AudioBuffer|null|undefined,at:number,gain:number)=>{const cue=death.id+':'+key;if(!buffer||this.loveSeen.has(cue))return;const delay=Math.max(0,at-age),offset=Math.max(0,age-at),duration=Math.min(buffer.duration-offset,CLOWN_LOVE.end-age-delay);this.loveSeen.add(cue);if(duration<=0)return;
    const sources=this.loveSources.get(death.id)||new Set<AudioBufferSourceNode>();this.loveSources.set(death.id,sources);const source=context.createBufferSource(),level=context.createGain();source.buffer=buffer;level.gain.value=gain;source.connect(level).connect(master);sources.add(source);source.onended=()=>{sources.delete(source);source.disconnect();level.disconnect();};source.start(now+delay,offset,duration);
   };
   if(van){schedule('music',this.vanBuffer,BASS_VAN.musicStart,2.3);}
   else{schedule('music',this.loveBuffer,0,1.15);for(const at of CLOWN_LOVE.beats)if(at>=age)schedule('hit-'+at,hit,at,.9);}
  }
 }
 clownLaugh(){const c=this.context;if(!c||!this.master||this.muted)return;for(let n=0;n<9;n++){const at=c.currentTime+1.65+n*.22,voice=c.createOscillator(),formant=c.createBiquadFilter(),gain=c.createGain();voice.type='sawtooth';voice.frequency.setValueAtTime(185+(n%3)*30,at);voice.frequency.exponentialRampToValueAtTime(115+(n%2)*30,at+.17);formant.type='bandpass';formant.frequency.value=800;formant.Q.value=.9;gain.gain.setValueAtTime(0,at);gain.gain.linearRampToValueAtTime(.22,at+.025);gain.gain.exponentialRampToValueAtTime(.001,at+.18);voice.connect(formant).connect(gain).connect(this.master);voice.start(at);voice.stop(at+.2);voice.onended=()=>{voice.disconnect();formant.disconnect();gain.disconnect();};}}
 get status(){return this.error||(this.context&&this.buffers.size<AUDIO_CLIPS.length?`Loading sound · ${this.buffers.size}/${AUDIO_CLIPS.length} effects`:!this.context?'Click Test Sound to enable audio':`${this.muted?'Muted':this.context.state==='running'?'Sound ready':'Audio paused'} · ${this.buffers.size}/${AUDIO_CLIPS.length} effects loaded`);}
 pausePresentation(){this.intros.stop();this.music.setActive(false);}
 previewIntro(state:Snapshot){this.music.setActive(false);this.intros.update(state,this.context,this.music.cueVolume);}
 reset(){this.intros.stop();this.stopLove();this.music.setActive(false);this.cursor.reset();this.phase='';this.countdown=-1;for(const voice of this.voices)voice.stop();this.voices.clear();}
 play(name:Sound,position?:Vec,gain=1){const context=this.context,buffer=this.buffers.get(name);if(!context||context.state!=='running'||!buffer||this.muted||!this.volume||this.voices.size>=24)return;
  let volume=gain,pan=0;if(position){const dx=position.x-this.listener.x,dy=position.y-this.listener.y,dz=position.z-this.listener.z,d=Math.hypot(dx,dy,dz);volume*=1/(1+Math.max(0,d-2)*.13);pan=Math.max(-.85,Math.min(.85,(-Math.cos(this.yaw)*dx+Math.sin(this.yaw)*dz)/Math.max(2,d)));}
  const source=context.createBufferSource(),level=context.createGain(),panner=context.createStereoPanner();source.buffer=buffer;source.playbackRate.value=name==='ui'||name==='countdown'?1:.97+Math.random()*.06;level.gain.value=volume;panner.pan.value=pan;source.connect(level).connect(panner).connect(this.master!);this.voices.add(source);source.onended=()=>{this.voices.delete(source);source.disconnect();level.disconnect();panner.disconnect();};source.start();this.played++;
 }
 private arcadeVoices=0;
 arcadeTone(hit:number,move:boolean,superMove=false){const c=this.context;if(!c||!this.master||this.muted||c.state!=='running'||this.arcadeVoices>=8)return;this.arcadeVoices++;const osc=c.createOscillator(),gain=c.createGain(),at=c.currentTime;osc.type=move?'sawtooth':'square';osc.frequency.setValueAtTime(move?120:360+Math.min(hit,10)*55,at);osc.frequency.exponentialRampToValueAtTime(move?(superMove?920:420):160,at+(move?.25:.08));gain.gain.setValueAtTime(move?.09:.06,at);gain.gain.exponentialRampToValueAtTime(.001,at+(move?.35:.10));osc.connect(gain).connect(this.master);osc.start();osc.stop(at+(move?.38:.12));osc.onended=()=>{this.arcadeVoices--;osc.disconnect();gain.disconnect();};}
 update(state:Snapshot,me:string,yaw:number,presentation=state){this.intros.update(presentation,this.context,this.music.cueVolume);this.music.setActive(['fight','countdown','result'].includes(state.phase));this.updateLove(state);const self=state.players.find(p=>p.id===me);if(self)this.listener=self.pos;this.yaw=yaw;
  for(const e of this.cursor.consume(state.events,state.time)){
   if(e.type==='shot'||e.type==='windup'){if(e.spec)this.play(e.spec==='leet'?'lightning':e.spec==='ansatsuken'?'fire':e.spec,e.pos,.65);}
   else if(e.type==='arcade-hit'){this.play('hit',e.pos,.5);this.arcadeTone(e.amount||1,false,e.message==='FINISH');}
   else if(e.type==='arcade-move'){if(e.message)this.arcadeTone(3,true,e.message.includes('SHINKU'));}
   else if(e.type==='arcade-strike')this.play('sword',e.pos,.25);
   else if(e.type==='leet-scare'&&e.target===me){this.play('darkness',undefined,.9);this.play('metal',undefined,.6);}
   else if(e.type==='leet-lock'||e.type==='leet-control'||e.type==='leet-brain')this.play('lightning',e.pos,.35);
   else if(e.type==='leet-nuke')this.play('explosion',e.pos,1);
   else if(e.type==='leet-freeze'||e.type==='leet-undo')this.play('darkness',e.pos,.6);
   else if(e.type==='reloadStart'||e.type==='reloadEnd'){if(e.source===me)this.play(e.type==='reloadStart'?'reload-start':'reload-end',undefined,.6);}
   else if(e.type==='hit'){if(e.message==='burn'||e.message==='poison')this.play(e.message==='burn'?'burn-tick':'poison-tick',e.pos,.24);else if(e.target===me)this.play('hurt',undefined,.6);else if(e.source===me)this.play('hit',undefined,.7);}
   else if(e.type==='impact')this.play(e.material==='metal'?'metal':e.material==='wood'?'wood':'stone',e.pos,.22);
   else if(e.type==='break')this.play(e.material==='metal'?'metal':e.material==='wood'?'wood':'stone',e.pos,.65);
   else if(e.type==='explosion'||e.type==='collapse')this.play('explosion',e.pos,.65);
   else if(e.type==='dash'||e.type==='jump'||e.type==='parry')this.play(e.type,e.pos,.5);
   else if(e.type==='meleeRebound')this.play('metal',e.pos,Math.min(.7,.2+(e.amount||0)/2000));
   else if(e.type==='blockPulse')this.play('explosion',e.pos,e.mods?.supernova?.8:.35);
   else if(e.type==='blockStart')this.play('parry',e.pos,.3);
   else if(['blockHit','reflect','blockAbsorb'].includes(e.type))this.play('parry',e.pos,.65);
   else if(e.type==='death'){this.play('stone',e.pos,.65);this.play('explosion',e.pos,.3);if(state.players.find(p=>p.id===e.target)?.death?.effect==='dev-clown')this.clownLaugh();}
   else if(e.type==='debrisHit')this.play('stone',e.pos,.5);
   else if(e.type==='push')this.play('water',e.pos,.65);
   else if(e.type==='pull')this.play('darkness',e.pos,.65);
   else if(e.type==='nova'||e.type==='arc')this.play('lightning',e.pos,.5);
  }
  if(state.phase==='countdown'){const n=Math.ceil(state.remaining);if(n!==this.countdown&&n>0)this.play('countdown',undefined,.7);this.countdown=n;}else this.countdown=-1;
  if(this.phase!==state.phase){if(state.phase==='fight')this.play('go',undefined,.7);if((state.phase==='result'||state.phase==='match'&&this.phase!=='result')&&state.winner)this.play(state.winner===me?'win':'lose',undefined,.65);this.phase=state.phase;}
 }
}
