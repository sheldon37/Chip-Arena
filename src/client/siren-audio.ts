import type {Snapshot} from '../shared/simulation.js';
import {markBeeps} from './siren/index.js';
/** Synthesized lock-on beeps follow the visual pulses. Siren has no voice clips. */
export class SirenAudio {
 private played=new Set<string>();private beeps=new Map<OscillatorNode,string>();private epoch='';
 update(state:Snapshot,context:AudioContext|null,master:GainNode|null){if(!context)return;const epoch=String(state.bossSong?.startedAt??'')+':'+state.generation;if(this.epoch!==epoch){this.clear();this.epoch=epoch;}
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
