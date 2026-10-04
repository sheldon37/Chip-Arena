// Owner bugs 2026-10-04: "Emperor style sound is playing on lightning staff?" and "...also playing on rifle?". Root cause:
// the client mapped Blink Strike (lightning Q), its teleport and Orbital Mark (rifle E) to Emperor's electric-hook clips.
// Every non-Emperor spec presses Q (which also sends the legacy `electric` input) and E against a target through the real
// Simulation; no Emperor event may be emitted and the real ArenaAudio may never play an electric-hook clip.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Snapshot} from '../src/shared/simulation.js';
import {idleInput,v,type SpecId,type Input} from '../src/shared/config.js';
import {WEAPONS} from '../src/shared/content.js';
import {kitFor} from '../src/shared/abilities.js';
import {fakeAudio} from './helpers/fake-audio.js';
await initPhysics();
const specs=Object.keys(WEAPONS) as SpecId[];
function session(spec:SpecId){
 const s=new Simulation({sandbox:true}),a=s.addPlayer('a','A',WEAPONS[spec].class as never,null,'#fff',{})!,b=s.addPlayer('b','B')!;a.spec=spec;a.developer=true as never;s.resetRound();s.phase='fight';
 for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});
 a.body.setTranslation(v(0,.9,0),true);b.body.setTranslation(v(0,.9,8),true);s.world.step();
 const defs=[kitFor(spec)?.q,kitFor(spec)?.e].filter(Boolean),old=defs.map(d=>d!.live);defs.forEach(d=>d!.live=true);
 const f=fakeAudio(),events:Snapshot['events']=[];let seq=0;
 const param=()=>({value:0,setValueAtTime(){},linearRampToValueAtTime(){},exponentialRampToValueAtTime(){},setTargetAtTime(){},cancelScheduledValues(){}});
 const ctx=f.context as unknown as Record<string,()=>{gain:object}>,createGain=ctx.createGain;ctx.createGain=()=>{const n=createGain();Object.assign(n.gain,{setValueAtTime(){},linearRampToValueAtTime(){},exponentialRampToValueAtTime(){},cancelScheduledValues(){}});return n;};
 Object.assign(f.context,{createOscillator:()=>({type:'sine',frequency:param(),detune:param(),connect(){return this;},disconnect(){},start(){},stop(){},onended:null}),createBiquadFilter:()=>({type:'lowpass',frequency:param(),Q:param(),gain:param(),connect(){return this;},disconnect(){}})});
 try{for(let n=0;n<240;n++){const q=n<60,e=n>=90&&n<96,input:Input={...idleInput(),seq:++seq,yaw:0,pitch:0,camera:'first',ability:q,electric:q,secondary:e,attack:n>=120&&n%10===0};
   s.setInput('a',input);s.setInput('b',{...idleInput(),seq});for(const p of s.players.values())p.inputAt=s.time;s.step();const snap=s.snapshot();events.push(...snap.events);f.clock.now+=1/60;f.reap();f.audio.update(snap,'a',0);}}
 finally{defs.forEach((d,i)=>d!.live=old[i]);s.dispose();}
 return {events,clips:f.voices.map(x=>x.name)};
}
test('no non-Emperor spec triggers Emperor events or Emperor (electric-hook) audio on Q, E or primary',()=>{
 for(const spec of specs){if(spec==='emperor')continue;const {events,clips}=session(spec);
  assert.deepEqual([...new Set(events.filter(e=>e.type.startsWith('emperor')).map(e=>e.type))],[],spec+' emperor events');
  assert.deepEqual([...new Set(clips.filter(c=>c.startsWith('electric-hook')))],[],spec+' plays Emperor clips: '+clips.join(','));}
});
test('Emperor still plays its own electric cue',()=>{const {clips}=session('emperor');assert.ok(clips.includes('electric-hook'),clips.join(','));});
