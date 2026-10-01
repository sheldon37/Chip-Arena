import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {cosmetics} from '../src/shared/cosmetics.js';
import {HostedRoom} from '../src/shared/host-room.js';
import {KILL_EFFECTS,DeathEffects} from '../src/client/death-effects.js';
import {ArenaAudio} from '../src/client/audio.js';
import {BASS_VAN,vanBass} from '../src/client/bass-van-timing.js';
await initPhysics();
function fixture(){const s=new Simulation({sandbox:true});s.addPlayer('dev','Dev','ranged',null,'#ff6633',{killEffect:'dev-bass-van'},true);s.addPlayer('other');s.phase='fight';s.damage(s.players.get('other')!,10000,'dev','preview');return s;}
test('Bass Van is developer-only, including forged host settings, and affects the defeated player',()=>{
 assert.equal(cosmetics({killEffect:'dev-bass-van'},false).killEffect,'impact-ragdoll');
 const room=new HostedRoom('ABCDEFGH',{name:'A',class:'ranged',color:'#ffffff',cosmetics:{bodyColor:'#ffffff',hat:'none',killEffect:'dev-bass-van'},authToken:'forged'},false,()=>{});
 assert.equal(room.sim.players.get('host')!.cosmetics.killEffect,'impact-ragdoll');room.dispose();
 const s=fixture();assert.equal(s.players.get('other')!.death!.effect,'dev-bass-van');assert.equal(s.players.get('dev')!.death,undefined);s.dispose();
});
test('Bass Van has finite rewindable poses, doors that close after boarding, waiting passengers, departure and cleanup at every quality',()=>{
 for(const quality of [.3,1]){
  const s=fixture(),p=s.snapshot().players[1],scene=new T.Scene(),effect=KILL_EFFECTS['dev-bass-van'](scene,new T.Group(),p,p.death!,quality,280);
  const van=scene.getObjectByName('bass-van-vehicle')!,passenger=scene.getObjectByName('bass-passenger')!,driver=scene.getObjectByName('bass-driver')!,cargo=scene.getObjectByName('bass-cargo-door')!,cab=scene.getObjectByName('bass-cab-door')!;
  for(let frame=0;frame<300;frame++){effect.update(frame/60);scene.updateMatrixWorld(true);scene.traverse(o=>assert.ok(o.matrixWorld.elements.every(Number.isFinite)));}
  effect.update(.3);assert.equal(cab.rotation.y,0);assert.equal(cargo.position.x,-.08);
  effect.update(1.5);assert.ok(cab.rotation.y>1);assert.ok(cargo.position.x<-1.5);
  effect.update((BASS_VAN.jumpStart+BASS_VAN.driverSeated)/2);assert.ok(driver.position.y>.18,'driver hops above the step');assert.ok(cab.rotation.y>1);
  effect.update(BASS_VAN.doorClose);scene.updateMatrixWorld(true);assert.ok(cab.rotation.y>1);assert.ok(van.worldToLocal(driver.getWorldPosition(new T.Vector3())).distanceTo(new T.Vector3(1.2,.12,.12))<1e-6);
  assert.ok(van.worldToLocal(passenger.getWorldPosition(new T.Vector3())).distanceTo(new T.Vector3(-.4,.13,.02))<1e-6);
  effect.update(BASS_VAN.musicStart);assert.equal(cab.rotation.y,0);assert.equal(cargo.position.x,-.08);assert.equal(cargo.position.z,.82);
  assert.equal(scene.getObjectByName('cargo-door-window'),undefined);const panel=scene.getObjectByName('cargo-door-solid-panel')! as T.Mesh;assert.ok(panel);assert.equal((panel.material as T.Material).transparent,false);
  effect.update(4.9);assert.ok(van.position.x>11);
  effect.update(2.9);scene.updateMatrixWorld(true);const pose=passenger.matrixWorld.clone();
  assert.ok(passenger.visible&&passenger.getObjectByName('blank-head')!.visible);assert.ok(driver.position.x-passenger.position.x>1.4);
  const local=van.worldToLocal(passenger.getWorldPosition(new T.Vector3()));assert.ok(local.distanceTo(new T.Vector3(-.4,.13,.02))<1e-6);
  assert.equal(scene.getObjectByName('bass-speaker-cone'),undefined);assert.equal(scene.getObjectByName('bass-music-note'),undefined);assert.equal(scene.getObjectByName('roof-speaker-cabinet'),undefined);assert.equal(scene.getObjectByName('boombox-handle'),undefined);for(let i=1;i<=3;i++){const waiting=scene.getObjectByName('bass-waiting-passenger-'+i)!;assert.ok(waiting&&waiting.parent===van);const box=new T.Box3().setFromObject(waiting);assert.ok(Number.isFinite(box.min.x));}
  effect.update(.5);assert.ok(van.position.x<.65);assert.ok(passenger.position.length()<1e-6);assert.equal(scene.getObjectByName('bass-music-note'),undefined);
  effect.update(2.9);scene.updateMatrixWorld(true);assert.deepEqual(passenger.matrixWorld.elements,pose.elements);
  effect.dispose();assert.equal(scene.children.length,0);s.dispose();
 }
});
test('Bass Van is removed at the five-second boundary and when the player is restored',()=>{
 const s=fixture(),state=s.snapshot(),scene=new T.Scene(),effects=new DeathEffects(scene),rigs=new Map(state.players.map(p=>[p.id,{group:new T.Group()}]));
 effects.update(state,rigs,1);assert.ok(scene.getObjectByName('bass-van'));
 state.time=5;effects.update(state,rigs,1);assert.equal(scene.children.length,0);
 effects.clear();state.time=0;effects.update(state,rigs,1);state.players[1].hp=100;effects.update(state,rigs,1);assert.equal(scene.children.length,0);s.dispose();
});
function audioFixture(){
 const calls:{start:number[];stopped:boolean}[]=[],duck:boolean[]=[],levels:{gain:{value:number}}[]=[];
 const context={state:'running',currentTime:10,createGain:()=>{const level={gain:{value:1},connect(){return this;},disconnect(){}};levels.push(level);return level;},createBufferSource:()=>{const call={start:[] as number[],stopped:false};calls.push(call);return {buffer:null,connect(){return this;},disconnect(){},start(...args:number[]){call.start=args;},stop(){call.stopped=true;}};}};
 const audio=new ArenaAudio();audio.context=context as any;audio.master={gain:{setTargetAtTime(){}}} as any;(audio as any).vanBuffer={duration:2.65};audio.music={setActive(){},setDucked(v:boolean){duck.push(v);}} as any;
 return {audio,calls,duck,levels,update:(state:any)=>(audio as any).updateLove(state)};
}
test('van music waits for the doors to close, ducks during playback, deduplicates, and cancels scheduled playback on reset',()=>{
 const s=fixture(),state=s.snapshot(),a=audioFixture();a.update(state);
 assert.equal(a.calls.length,1);assert.deepEqual(a.calls[0].start,[10+BASS_VAN.musicStart,0,2.65]);assert.equal(a.duck.at(-1),false);
 state.time=2.2;a.update(state);assert.equal(a.duck.at(-1),false);
 state.time=2.4;a.update(state);assert.equal(a.calls.length,1);assert.equal(a.duck.at(-1),true);
 a.audio.configure(.3,true);a.update(state);assert.equal(a.duck.at(-1),false);
 a.audio.reset();assert.ok(a.calls.every(c=>c.stopped));assert.equal(a.duck.at(-1),false);s.dispose();
});
test('late van snapshots seek the music and resurrection cancels it without a hit buffer dependency',()=>{
 const s=fixture(),state=s.snapshot(),a=audioFixture();state.time=3;a.update(state);
 assert.equal(a.calls.length,1);assert.ok(Math.abs(a.calls[0].start[1]-.65)<1e-8);assert.equal(a.calls[0].start[2],2);
 state.players[1].hp=100;a.update(state);assert.ok(a.calls[0].stopped);assert.equal(a.duck.at(-1),false);s.dispose();
});
test('bass envelope stays bounded and stops outside the audible timeline',()=>{
 assert.equal(vanBass(0),0);assert.equal(vanBass(5),0);let peak=0;
 for(let t=0;t<6;t+=1/120){const bass=vanBass(t);assert.ok(Number.isFinite(bass)&&bass>=0&&bass<=1);peak=Math.max(peak,bass);}assert.ok(peak>.9);
});
test('van drive-off keeps only the louder music playing',()=>{
 const s=fixture(),state=s.snapshot(),a=audioFixture();a.update(state);
 assert.equal(a.calls.length,1);assert.equal(a.levels[0].gain.value,2.3);
 state.time=BASS_VAN.drive+.2;a.update(state);assert.equal(a.calls.length,1);
 state.players[1].hp=100;a.update(state);assert.ok(a.calls[0].stopped);s.dispose();
});
