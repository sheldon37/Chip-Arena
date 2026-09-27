import {INTRO_STYLES,INTRO,introFrame} from '../shared/intros.js';
import type {Snapshot} from '../shared/simulation.js';

/** One original sting per entrance, seeking into late snapshots and stopping on cancellation. */
export class IntroAudio {
 readonly buffers=new Map<string,AudioBuffer>();private loading:Promise<void>|null=null;private key='';private source:AudioBufferSourceNode|null=null;private gain:GainNode|null=null;private previousAge=-1;
 load(context:AudioContext,base:string){return this.loading??=Promise.all(Object.keys(INTRO_STYLES).map(async style=>{try{const response=await fetch(`${base}audio/intros/${style}.mp3`);if(response.ok)this.buffers.set(style,await context.decodeAudioData(await response.arrayBuffer()));}catch{/* A missing optional sting does not block play. */}})).then(()=>{});}
 update(state:Snapshot,context:AudioContext|null,volume:number){
  const frame=introFrame(state);if(!frame||!context||context.state!=='running'||volume<=0){this.stop();return;}
  const key=`${state.generation}:${state.intro!.started}:${frame.player.id}:${frame.style}`;
  if(key!==this.key||frame.age<this.previousAge-.25)this.stop();this.previousAge=frame.age;
  if(this.key===key){if(this.gain)this.gain.gain.setTargetAtTime(volume,context.currentTime,.04);return;}
  const buffer=this.buffers.get(frame.style);if(!buffer||frame.age>=buffer.duration)return;
  const source=context.createBufferSource(),gain=context.createGain();source.buffer=buffer;gain.gain.value=volume;source.connect(gain).connect(context.destination);this.key=key;this.source=source;this.gain=gain;
  source.onended=()=>{source.disconnect();gain.disconnect();if(this.source===source){this.source=null;this.gain=null;}};source.start(context.currentTime,frame.age);source.stop(context.currentTime+Math.min(buffer.duration-frame.age,INTRO.seconds-frame.age));
 }
 stop(){if(this.source){this.source.stop();this.source.disconnect();this.gain?.disconnect();}this.source=null;this.gain=null;this.key='';this.previousAge=-1;}
}
