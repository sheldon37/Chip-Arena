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
 context:AudioContext|null=null;master:GainNode|null=null;buffers=new Map<Sound,AudioBuffer>();voices=new Set<AudioBufferSourceNode>();cursor=new AudioCursor();loading:Promise<void>|null=null;volume=.3;muted=false;played=0;error='';phase='';countdown=-1;listener:Vec={x:0,y:0,z:0};yaw=0;
 configure(volume:number,muted:boolean){this.volume=Math.max(0,Math.min(1,volume));this.muted=muted;if(this.master&&this.context)this.master.gain.setTargetAtTime(muted?0:this.volume,this.context.currentTime,.015);}
 async unlock(){
  if(!this.context)try{this.context=new AudioContext();this.master=this.context.createGain();const limiter=this.context.createDynamicsCompressor();limiter.threshold.value=-13;limiter.knee.value=14;limiter.ratio.value=5;limiter.attack.value=.004;limiter.release.value=.15;this.master.connect(limiter).connect(this.context.destination);this.configure(this.volume,this.muted);}catch{this.error='Audio unavailable';return;}
  try{await this.context.resume();}catch{this.error='Click Test Sound to enable audio';}
  if(!this.loading)this.loading=Promise.all(AUDIO_CLIPS.map(async name=>{const response=await fetch(`${(import.meta as any).env.BASE_URL}audio/${name}.wav`);if(!response.ok)throw Error(name);const buffer=await this.context!.decodeAudioData(await response.arrayBuffer());this.buffers.set(name,buffer);})).then(()=>{this.error='';}).catch(()=>{this.error='Some sound files could not load';});
  await this.loading;
 }
 get status(){return this.error||(this.context&&this.buffers.size<AUDIO_CLIPS.length?`Loading sound · ${this.buffers.size}/${AUDIO_CLIPS.length} effects`:!this.context?'Click Test Sound to enable audio':`${this.muted?'Muted':this.context.state==='running'?'Sound ready':'Audio paused'} · ${this.buffers.size}/${AUDIO_CLIPS.length} effects loaded`);}
 reset(){this.cursor.reset();this.phase='';this.countdown=-1;for(const voice of this.voices)voice.stop();this.voices.clear();}
 play(name:Sound,position?:Vec,gain=1){const context=this.context,buffer=this.buffers.get(name);if(!context||context.state!=='running'||!buffer||this.muted||!this.volume||this.voices.size>=24)return;
  let volume=gain,pan=0;if(position){const dx=position.x-this.listener.x,dy=position.y-this.listener.y,dz=position.z-this.listener.z,d=Math.hypot(dx,dy,dz);volume*=1/(1+Math.max(0,d-2)*.13);pan=Math.max(-.85,Math.min(.85,(-Math.cos(this.yaw)*dx+Math.sin(this.yaw)*dz)/Math.max(2,d)));}
  const source=context.createBufferSource(),level=context.createGain(),panner=context.createStereoPanner();source.buffer=buffer;source.playbackRate.value=name==='ui'||name==='countdown'?1:.97+Math.random()*.06;level.gain.value=volume;panner.pan.value=pan;source.connect(level).connect(panner).connect(this.master!);this.voices.add(source);source.onended=()=>{this.voices.delete(source);source.disconnect();level.disconnect();panner.disconnect();};source.start();this.played++;
 }
 update(state:Snapshot,me:string,yaw:number){const self=state.players.find(p=>p.id===me);if(self)this.listener=self.pos;this.yaw=yaw;
  for(const e of this.cursor.consume(state.events,state.time)){
   if(e.type==='shot'||e.type==='windup'){if(e.spec)this.play(e.spec,e.pos,.65);}
   else if(e.type==='reloadStart'||e.type==='reloadEnd'){if(e.source===me)this.play(e.type==='reloadStart'?'reload-start':'reload-end',undefined,.6);}
   else if(e.type==='hit'){if(e.message==='burn'||e.message==='poison')this.play(e.message==='burn'?'burn-tick':'poison-tick',e.pos,.24);else if(e.target===me)this.play('hurt',undefined,.6);else if(e.source===me)this.play('hit',undefined,.7);}
   else if(e.type==='impact')this.play(e.material==='metal'?'metal':e.material==='wood'?'wood':'stone',e.pos,.22);
   else if(e.type==='break')this.play(e.material==='metal'?'metal':e.material==='wood'?'wood':'stone',e.pos,.65);
   else if(e.type==='explosion'||e.type==='collapse')this.play('explosion',e.pos,.65);
   else if(e.type==='dash'||e.type==='jump'||e.type==='parry')this.play(e.type,e.pos,.5);
   else if(e.type==='debrisHit')this.play('stone',e.pos,.5);
   else if(e.type==='push')this.play('water',e.pos,.65);
   else if(e.type==='pull')this.play('darkness',e.pos,.65);
   else if(e.type==='nova'||e.type==='arc')this.play('lightning',e.pos,.5);
  }
  if(state.phase==='countdown'){const n=Math.ceil(state.remaining);if(n!==this.countdown&&n>0)this.play('countdown',undefined,.7);this.countdown=n;}else this.countdown=-1;
  if(this.phase!==state.phase){if(state.phase==='fight')this.play('go',undefined,.7);if(['result','match'].includes(state.phase)&&state.winner)this.play(state.winner===me?'win':'lose',undefined,.65);this.phase=state.phase;}
 }
}
