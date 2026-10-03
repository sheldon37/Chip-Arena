import test from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type GameEvent,type Snapshot} from '../src/shared/simulation.js';
import {v} from '../src/shared/config.js';
import {SoundLimiter,HIT_TRIM,ArenaAudio} from '../src/client/audio.js';
import {HitmarkerTrack} from '../src/client/hitmarker.js';
import {fakeAudio} from './helpers/fake-audio.js';

// Owner follow-up (2026-10-03): stop the hitmarker "ear-rape" from chip procs and lower hit/impact sounds ~2 dB.
await initPhysics();
const sim=new Simulation({sandbox:true});sim.addPlayer('me','Me','ranged',null,'#fff',{});sim.addPlayer('foe','Foe','ranged',null,'#f00',{});sim.resetRound();sim.phase='fight';
const base=sim.snapshot();sim.dispose();
let nextId=1;
const hit=(time:number,message='field',extra:Partial<GameEvent>={}):GameEvent=>({id:nextId++,type:'hit',time,pos:v(0,1,4),source:'me',target:'foe',amount:3,message,...extra});
/** Feed `rate` events per second for `seconds` through ArenaAudio.update at 60 fps; returns the plays per second. */
function feed(audio:ArenaAudio,f:ReturnType<typeof fakeAudio>,make:(t:number)=>GameEvent,rate:number,seconds:number,clip='hit'){
 const start=f.clock.now,perSec:number[]=Array(seconds).fill(0),first=f.voices.length;let due=0;
 for(let i=0;i<Math.round(seconds*60);i++){const t=start+i/60;f.clock.now=t;f.reap();const events:GameEvent[]=[];for(due+=rate/60;due>=1;due--)events.push(make(t));
  audio.update({...base,time:t,events} as Snapshot,'me',0);}
 const voices=f.voices.slice(first).filter(x=>x.name===clip);for(const voice of voices)perSec[Math.floor(voice.at-start)]++;
 f.clock.now=start+seconds;return {perSec,voices};
}

test('SoundLimiter: 200 gates per second play at most ~12 per second, bursts fold into a capped louder play, repeats duck',()=>{
 const l=new SoundLimiter();let plays=0,peak=0;const gains:number[]=[];
 for(let i=0;i<400;i++){const g=l.gate('proc',i/200,.3);if(g>0){plays++;peak=Math.max(peak,g);gains.push(g);}}
 assert.ok(plays<=24&&plays>=20,`${plays} plays in 2 s`);assert.ok(peak<=.3*1.5+1e-9,`peak gain ${peak}`);
 const fresh=new SoundLimiter(),solo=fresh.gate('hit',0,.9);assert.equal(solo,.9,'a lone hit plays at full gain');
 const second=fresh.gate('hit',.1,.9),third=fresh.gate('hit',.2,.9);assert.ok(second<solo&&third<second,'repeated plays duck');
 assert.ok(third>=.9*.35,'ducking has a floor');assert.ok(Math.abs(fresh.gate('hit',5,.9)-.9)<.02,'after a pause the hit is crisp again');
});

test('ArenaAudio: 200 proc hits/s give at most ~12 quiet, pitched proc ticks per second; a direct hit stays a crisp hitmarker',()=>{
 const f=fakeAudio(),{perSec,voices}=feed(f.audio,f,t=>hit(t,'field'),200,3);
 assert.ok(perSec.every(n=>n<=12),`proc plays per second ${perSec}`);assert.ok(perSec.every(n=>n>=8),'procs are still audible: '+perSec);
 assert.ok(voices.every(x=>x.name==='hit'&&x.rate===1.55),'procs use the distinct pitched tick');
 const peak=Math.max(...voices.map(x=>x.gain));assert.ok(peak<=.32*HIT_TRIM*1.5+1e-9,`proc peak gain ${peak}`);
 // Chain arcs and chip blasts are limited too (and stay audible: nova/arc keep their lightning cue).
 const arcs=feed(f.audio,f,t=>({id:nextId++,type:'arc',time:t,pos:v(0,1,3),source:'me'}),120,1,'lightning').voices;assert.ok(arcs.length>=8&&arcs.length<=12&&arcs.every(x=>x.name==='lightning'),'arc plays '+arcs.length);
 f.clock.now+=2;const direct=feed(f.audio,f,t=>hit(t,'direct'),1,1).voices;
 assert.equal(direct.length,1);assert.equal(direct[0].rate,1);assert.ok(Math.abs(direct[0].gain-.9*HIT_TRIM)<1e-9,'a lone direct hit: the crisp hitmarker, ~2 dB quieter than before ('+direct[0].gain+')');
 // Sustained machine-gun direct hits (12/s) each play, ducked but never below the floor.
 f.clock.now+=2;const mg=feed(f.audio,f,t=>hit(t,'direct'),12,2).voices;assert.ok(mg.length>=22,'every MG hit is heard: '+mg.length);assert.ok(mg.every(x=>x.gain>=.9*HIT_TRIM*.35-1e-9&&x.gain<=.9*HIT_TRIM+1e-9));
});

test('ArenaAudio: keyed hit/impact voices are capped at 8; volume and mute settings are respected; the master limiter is a limiter',async()=>{
 const f=fakeAudio();f.clock.now=50;const events:GameEvent[]=['impact','explosion','arc','nova','debrisHit','impact','explosion','arc','nova','debrisHit','impact','explosion'].map((type,i)=>({id:nextId++,type,time:50,pos:v(i*4,0,0),source:'x',material:'stone'}));
 f.audio.update({...base,time:50,events:[...events,hit(50,'direct'),hit(50,'chain')]} as Snapshot,'me',0);
 assert.ok(f.voices.filter(x=>!x.ended).length<=8,'at most 8 keyed voices at once: '+f.voices.length);
 f.audio.configure(.5,false);assert.equal(f.master.at(-1),.5,'master gain follows the volume setting');
 f.audio.configure(.5,true);const before=f.voices.length;f.clock.now=60;f.audio.update({...base,time:60,events:[hit(60,'direct')]} as Snapshot,'me',0);assert.equal(f.voices.length,before,'muted: nothing plays');
 // unlock() builds master gain → DynamicsCompressor (limiter) → destination.
 const made:Record<string,number>={};const param=(k:string)=>({set value(v:number){made[k]=v;},get value(){return made[k];}});
 class Ctx{state='running';currentTime=0;destination={};createGain(){return {gain:{value:1,setTargetAtTime(){}},connect:(n:unknown)=>n};}createDynamicsCompressor(){return {threshold:param('threshold'),knee:param('knee'),ratio:param('ratio'),attack:param('attack'),release:param('release'),connect:(n:unknown)=>n};}resume(){return Promise.resolve();}decodeAudioData(){return Promise.reject(new Error('no audio'));}}
 const g=globalThis as unknown as {AudioContext?:unknown;fetch:unknown},oldCtx=g.AudioContext,oldFetch=g.fetch;g.AudioContext=Ctx;g.fetch=()=>Promise.resolve({ok:false});
 try{const a=new ArenaAudio(),stub=new Proxy({},{get:()=>()=>undefined});Object.assign(a,{music:stub,intros:stub});await a.unlock().catch(()=>{});}finally{g.AudioContext=oldCtx;g.fetch=oldFetch;}
 assert.ok(made.ratio>=10&&made.threshold<=-10&&made.attack<=.003,'master limiter '+JSON.stringify(made));
});

test('visual hitmarker: proc hits coalesce into the small marker at most every .22 s; direct hits retrigger the full cross',()=>{
 const track=new HitmarkerTrack();let retriggers=0,last=-1,small=true;
 for(let i=0;i<120;i++){const t=20+i/60,events=[hit(t,'field'),hit(t,'field'),hit(t,'chain'),hit(t,'ability')];const m=track.update({...base,time:t,events} as Snapshot,'me');if(track.at!==last){retriggers++;last=track.at;}small&&=m.dot;}
 assert.ok(retriggers<=10,`proc retriggers in 2 s: ${retriggers}`);assert.ok(small,'procs show the small tick marker');
 const m=track.update({...base,time:22.05,events:[hit(22.05,'direct',{headshot:true})]} as Snapshot,'me');assert.equal(m.dot,false);assert.equal(m.headshot,true);assert.equal(track.at,22.05,'a direct hit retriggers immediately');
});
