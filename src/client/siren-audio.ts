import type {GameEvent,Snapshot} from '../shared/simulation.js';
import {introFrame} from '../shared/intros.js';
import {SIREN_INTRO} from '../shared/siren-contract.js';
/** Optional owner voice files. A missing clip is silent and does not disable the game audio. */
export class SirenAudio {
 private buffers=new Map<string,AudioBuffer|null>();private loading=new Set<string>();private played=new Set<string>();private beepAt=new Map<string,number>();private epoch='';
 private clip(key:string,name:string,context:AudioContext|null,master:GainNode|null){if(this.played.has(key)||!context||!master)return;this.played.add(key);const buffer=this.buffers.get(name);if(buffer){const source=context.createBufferSource();source.buffer=buffer;source.connect(master);source.onended=()=>source.disconnect();source.start();return;}if(this.buffers.has(name)||this.loading.has(name))return;this.load(name,context);}
 private load(name:string,context:AudioContext){this.loading.add(name);void(async()=>{try{const r=await fetch(`${(import.meta as any).env?.BASE_URL||'/'}audio/${name}.mp3`);this.buffers.set(name,r.ok?await context.decodeAudioData(await r.arrayBuffer()):null);}catch{this.buffers.set(name,null);}finally{this.loading.delete(name);}})();}
 event(e:GameEvent,context:AudioContext|null,master:GainNode|null){if(e.type==='siren-laugh')this.clip('event:'+e.id,'siren-laugh',context,master);if(e.type==='siren-mark')this.clip('event:'+e.id,'siren-you',context,master);}
 update(state:Snapshot,context:AudioContext|null,master:GainNode|null){if(!context)return;const epoch=String(state.bossSong?.startedAt??'')+':'+state.generation;if(this.epoch!==epoch){this.played.clear();this.beepAt.clear();this.epoch=epoch;}if(state.players.some(p=>p.spec==='siren'))for(const name of ['siren-laugh','siren-you'])if(!this.buffers.has(name)&&!this.loading.has(name))this.load(name,context);
  const intro=introFrame(state),at=SIREN_INTRO.cues.find(c=>c.cue==='reveal')!.time;if(intro?.style==='dev-siren'&&intro.age>=at&&intro.age-at<.3)this.clip('intro:'+state.intro?.started,'siren-laugh',context,master);
  for(const m of state.siren?.marks||[]){if(state.time<m.lockAt||state.time>=m.throwAt||state.time<(this.beepAt.get(m.id)??0)||!master||context.state!=='running')continue;const u=(state.time-m.lockAt)/(m.throwAt-m.lockAt),osc=context.createOscillator(),gain=context.createGain(),now=context.currentTime;osc.frequency.value=550+u*850;gain.gain.setValueAtTime(.055,now);gain.gain.exponentialRampToValueAtTime(.001,now+.055);osc.connect(gain).connect(master);osc.start(now);osc.stop(now+.06);osc.onended=()=>{osc.disconnect();gain.disconnect();};this.beepAt.set(m.id,state.time+.3-.22*u);}
 }
 clear(){this.played.clear();this.beepAt.clear();this.epoch='';}
}
