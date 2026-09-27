import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {cosmetics} from '../src/shared/cosmetics.js';
import {HostedRoom} from '../src/shared/host-room.js';
import {KILL_EFFECTS} from '../src/client/death-effects.js';
import {ArenaAudio} from '../src/client/audio.js';
import {CLOWN_LOVE} from '../src/client/clown-love-timing.js';
await initPhysics();
function fixture(){const s=new Simulation({sandbox:true});s.addPlayer('dev','Dev','ranged',null,'#ff6633',{killEffect:'dev-clown-love'},true);s.addPlayer('other');s.phase='fight';s.damage(s.players.get('other')!,10000,'dev','preview');return s;}
test('Clown Love requires trusted developer status and is assigned to the defeated opponent',()=>{
 assert.equal(cosmetics({killEffect:'dev-clown-love'},false).killEffect,'voxel-burst');
 const room=new HostedRoom('ABCDEFGH',{name:'A',class:'ranged',color:'#ffffff',cosmetics:{bodyColor:'#ffffff',hat:'none',killEffect:'dev-clown-love'},authToken:'forged'},false,()=>{});
 assert.equal(room.sim.players.get('host')!.cosmetics.killEffect,'voxel-burst');room.dispose();
 const s=fixture();assert.equal(s.players.get('other')!.death!.effect,'dev-clown-love');assert.equal(s.players.get('dev')!.death,undefined);s.dispose();
});
test('Clown Love keeps partners facing one another, has finite poses, exits within five seconds and cleans up',()=>{
 const s=fixture(),p=s.snapshot().players[1],scene=new T.Scene(),effect=KILL_EFFECTS['dev-clown-love'](scene,new T.Group(),p,p.death!,1,280);
 const root=scene.getObjectByName('clown-love')!,partner=root.getObjectByName('love-partner')!,clown=root.getObjectByName('love-clown')!;
 for(let frame=0;frame<300;frame++){const t=frame/60;effect.update(t);scene.updateMatrixWorld(true);scene.traverse(o=>assert.ok(o.matrixWorld.elements.every(Number.isFinite)));
  if(t>=1.05&&t<3.65){assert.ok(partner.position.x-clown.position.x>.85);assert.ok(Math.abs(partner.rotation.y+Math.PI/2)<.001);assert.ok(Math.abs(clown.rotation.y-Math.PI/2)<.001);}
 }
 for(let i=0;i<CLOWN_LOVE.beats.length;i++){effect.update(CLOWN_LOVE.beats[i]+.02);assert.ok(root.getObjectByName('love-pop-'+i)!.visible);}
 effect.update(4.9);assert.ok(clown.position.x>8);effect.dispose();assert.equal(scene.children.length,0);s.dispose();
});
test('late snapshots seek violin, schedule only future dip sounds, deduplicate and cancel on reset',()=>{
 const s=fixture(),state=s.snapshot();state.time=2;
 const calls:{start:number[];stopped:boolean}[]=[];
 const context={state:'running',currentTime:10,createGain:()=>({gain:{value:1},connect(){return this;},disconnect(){}}),createBufferSource:()=>{const call={start:[] as number[],stopped:false};calls.push(call);return {buffer:null,connect(){return this;},disconnect(){},start(...args:number[]){call.start=args;},stop(){call.stopped=true;}};}};
 const audio=new ArenaAudio();audio.context=context as any;audio.master={} as any;(audio as any).loveBuffer={duration:5};audio.buffers.set('hit',{duration:.1} as AudioBuffer);audio.music={setActive(){},setDucked(){}} as any;
 (audio as any).updateLove(state);assert.equal(calls.length,5);assert.deepEqual(calls[0].start,[10,2,3]);assert.ok(Math.abs(calls[1].start[0]-10.1)<.001);
 (audio as any).updateLove(state);assert.equal(calls.length,5);audio.reset();assert.ok(calls.every(c=>c.stopped));s.dispose();
});
