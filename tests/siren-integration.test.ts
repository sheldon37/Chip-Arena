import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {ArenaRenderer} from '../src/client/renderer.js';
import {BlockCooldownTint} from '../src/client/block-cooldown-tint.js';
import {createCharacterRig} from '../src/client/character-rig.js';
import {PlayerIntros} from '../src/client/player-intros.js';
import {SirenAudio} from '../src/client/siren-audio.js';
import {markBeeps} from '../src/client/siren/index.js';
import {SIREN_INTRO} from '../src/shared/siren-contract.js';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
await initPhysics();

test('renderer keeps one Siren first-person rig across frames and rebuilds on spec transitions only',()=>{
 const sim=new Simulation();const p=sim.addPlayer('me','Boss','dev',null,'#ffffff',{},true)!;p.spec='siren';
 // Exercise the renderer's real view-model path without constructing a GPU context.
 const renderer=Object.create(ArenaRenderer.prototype) as ArenaRenderer;
 Object.assign(renderer,{firstRig:createCharacterRig('#ffffff'),viewRoot:new T.Group(),viewSpec:'',cooldownTint:new BlockCooldownTint(),compatibility:false,budget:{mode:'balanced'},scoped:false});renderer.viewWeapon=renderer.firstRig.weapon;
 try{
  let state=sim.snapshot();renderer.updateViewModel(state.players[0],state,1/60);const siren=renderer.firstRig;
  assert.ok(siren.group.getObjectByName('siren-view-claws'));
  for(let i=1;i<=120;i++){state.time=i/60;renderer.updateViewModel(state.players[0],state,1/60);assert.equal(renderer.firstRig,siren);}
  assert.equal(renderer.viewRoot.children.length,1);
  p.spec='pistol';state=sim.snapshot();renderer.updateViewModel(state.players[0],state,1/60);
  assert.notEqual(renderer.firstRig,siren);assert.equal(renderer.firstRig.group.getObjectByName('siren-view-claws'),undefined);
  assert.equal(renderer.firstRig.weapon.visible,true);assert.ok(renderer.firstRig.weapon.children.length>0);
  const ordinary=renderer.firstRig;renderer.updateViewModel(state.players[0],state,1/60);assert.equal(renderer.firstRig,ordinary);
 }finally{renderer.cooldownTint.clear();sim.dispose();}
});

test('combined intro preserves viewport aspect, suppresses armor glow and restores ordinary intros',()=>{
 const sim=new Simulation(),p=sim.addPlayer('me','Boss','dev',null,'#ffffff',{},true)!,intro=new PlayerIntros();p.spec='siren';sim.beginIntros();
 try{
  for(const aspect of [16/9,21/9,4/3])for(const age of [.2,3.95,6.1]){sim.time=age;assert.equal(intro.update(sim.snapshot(),aspect),true);assert.equal(intro.camera.aspect,aspect);assert.ok(intro.camera.projectionMatrix.elements.every(Number.isFinite));assert.ok(intro.armor.lights.every(l=>l.intensity===0));
   const halos:T.Object3D[]=[];intro.scene.traverseVisible(o=>{if(o.name==='armor-radiance')halos.push(o);});assert.equal(halos.length,0);
  }
  p.spec='pistol';sim.time=.3;assert.equal(intro.update(sim.snapshot(),16/9),true);assert.equal(intro.siren.group.visible,false);assert.ok(intro.armor.lights.some(l=>l.intensity>0));
 }finally{intro.dispose();sim.dispose();}
});

function audioClock(){
 const tones:{start:number;cancelled:boolean}[]=[],voices:number[]=[];
 const context={currentTime:100,state:'running',createGain:()=>({gain:{setValueAtTime(){},exponentialRampToValueAtTime(){}},connect(){},disconnect(){}}),createOscillator:()=>{const tone={start:0,cancelled:false};tones.push(tone);return {frequency:{value:0},connect(){return {connect(){}};},disconnect(){},start(at:number){tone.start=at;},stop(at?:number){if(at===undefined)tone.cancelled=true;}};},createBufferSource:()=>({buffer:null,connect(){},disconnect(){},start(){voices.push(context.currentTime);}})};
 return {context,tones,voices,master:{} as GainNode};
}

test('lock audio follows the visual beep schedule exactly, deduplicates and cancels interrupted locks',()=>{
 const sim=new Simulation(),audio=new SirenAudio(),{context,tones,master}=audioClock(),state=sim.snapshot();
 state.siren={patches:[],eruptions:[],rains:[],grabs:[],marks:[{id:'m',owner:'a',target:'b',at:0,lockAt:1.2,throwAt:2.4}]};
 try{
  for(let i=0;i<=150;i++){state.time=i/60;context.currentTime=100+state.time;audio.update(state,context as unknown as AudioContext,master);audio.update(state,context as unknown as AudioContext,master);}
  const expected=markBeeps(1.2,2.4);assert.equal(tones.length,expected.length);expected.forEach((t,i)=>assert.ok(Math.abs(tones[i].start-(100+t))<1e-8));
  audio.clear();tones.length=0;state.generation=(state.generation??0)+1;state.time=0;context.currentTime=100;state.siren.marks[0]={id:'new',owner:'a',target:'b',at:0,lockAt:.04,throwAt:1.24};
  audio.update(state,context as unknown as AudioContext,master);assert.equal(tones.length,1);assert.equal(tones[0].start,100.04);state.siren.marks=[];audio.update(state,context as unknown as AudioContext,master);assert.equal(tones[0].cancelled,true);
 }finally{audio.clear();sim.dispose();}
});

test('intro laugh starts on Claude’s smile cue, once, rather than the earlier reveal',()=>{
 const sim=new Simulation(),p=sim.addPlayer('me','Boss','dev',null,'#ffffff',{},true)!,audio=new SirenAudio(),{context,voices,master}=audioClock();p.spec='siren';sim.beginIntros();
 (audio as any).buffers.set('siren-laugh',{});(audio as any).buffers.set('siren-you',null);
 try{
  sim.time=SIREN_INTRO.cues.find(c=>c.cue==='reveal')!.time;audio.update(sim.snapshot(),context as unknown as AudioContext,master);assert.equal(voices.length,0);
  sim.time=SIREN_INTRO.cues.find(c=>c.cue==='smile')!.time;audio.update(sim.snapshot(),context as unknown as AudioContext,master);audio.update(sim.snapshot(),context as unknown as AudioContext,master);assert.equal(voices.length,1);
 }finally{audio.clear();sim.dispose();}
});
