import type {GameEvent,Snapshot} from '../shared/simulation.js';
import {introFrame} from '../shared/intros.js';
import {SIREN_INTRO} from '../shared/siren-contract.js';
import {markBeeps} from './siren/index.js';
/** Optional owner voice files. A missing clip is silent and does not disable the game audio. */
export class SirenAudio {
 private buffers=new Map<string,AudioBuffer|null>();private loading=new Set<string>();private played=new Set<string>();private beeps=new Map<OscillatorNode,string>();private epoch='';
 private clip(key:string,name:string,context:AudioContext|null,master:GainNode|null){if(this.played.has(key)||!context||!master)return;this.played.add(key);const buffer=this.buffers.get(name);if(buffer){const source=context.createBufferSource();source.buffer=buffer;source.connect(master);source.onended=()=>source.disconnect();source.start();return;}if(this.buffers.has(name)||this.loading.has(name))return;this.load(name,context);}
 private load(name:string,context:AudioContext){this.loading.add(name);void(async()=>{try{const r=await fetch(`${(import.meta as any).env?.BASE_URL||'/'}audio/${name}.mp3`);this.buffers.set(name,r.ok?await context.decodeAudioData(await r.arrayBuffer()):null);}catch{this.buffers.set(name,null);}finally{this.loading.delete(name);}})();}
 event(e:GameEvent,context:AudioContext|null,master:GainNode|null){if(e.type==='siren-laugh')this.clip('event:'+e.id,'siren-laugh',context,master);if(e.type==='siren-mark')this.clip('event:'+e.id,'siren-you',context,master);}
 update(state:Snapshot,context:AudioContext|null,master:GainNode|null){if(!context)return;const epoch=String(state.bossSong?.startedAt??'')+':'+state.generation;if(this.epoch!==epoch){this.clear();this.epoch=epoch;}if(state.players.some(p=>p.spec==='siren'))for(const name of ['siren-laugh','siren-you'])if(!this.buffers.has(name)&&!this.loading.has(name))this.load(name,context);
  const intro=introFrame(state),at=SIREN_INTRO.cues.find(c=>c.cue==='smile')!.time;if(intro?.style==='dev-siren'&&intro.age>=at&&intro.age-at<.3)this.clip('intro:'+state.intro?.started,'siren-laugh',context,master);
  const marks=state.siren?.marks||[],active=new Set(marks.filter(m=>state.time<m.throwAt).map(m=>m.id));
  for(const [osc,id] of this.beeps)if(!active.has(id)){osc.stop();this.beeps.delete(osc);}
  if(!master||context.state!=='running')return;
  for(const m of marks)for(const [index,at] of markBeeps(m.lockAt,m.throwAt).entries()){
   const key='beep:'+m.id+':'+index;if(this.played.has(key)||at>state.time+.08)continue;this.played.add(key);
   // Schedule slightly ahead on the audio clock; stale snapshots never replay old pulses.
   if(at<state.time-.08||state.time>=m.throwAt)continue;
   const u=(at-m.lockAt)/(m.throwAt-m.lockAt),osc=context.createOscillator(),gain=context.createGain(),when=context.currentTime+Math.max(0,at-state.time);
   osc.frequency.value=550+u*850;gain.gain.setValueAtTime(.055,when);gain.gain.exponentialRampToValueAtTime(.001,when+.055);osc.connect(gain).connect(master);this.beeps.set(osc,m.id);osc.onended=()=>{this.beeps.delete(osc);osc.disconnect();gain.disconnect();};osc.start(when);osc.stop(when+.06);
  }
 }
 clear(){for(const osc of this.beeps.keys())osc.stop();this.beeps.clear();this.played.clear();this.epoch='';}
}
