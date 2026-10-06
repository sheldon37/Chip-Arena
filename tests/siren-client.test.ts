import {test} from 'node:test';
import assert from 'node:assert/strict';
import {BossTheme,songDrift} from '../src/client/boss-theme.js';
import {BLACK_SIREN_PROTOCOL as track,barStart,loopedSongTime} from '../src/shared/boss-music.js';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {Predictor} from '../src/client/prediction.js';
import {sirenMotion} from '../src/shared/siren-motion.js';
import {idleInput,v} from '../src/shared/config.js';
import {createCharacterRig} from '../src/client/character-rig.js';
import {animateCharacter} from '../src/client/character-animation.js';
import {sirenify,animateSiren,SirenIntroStage} from '../src/client/siren/index.js';
import * as THREE from 'three';
await initPhysics();

test('boss audio seeks late joiners, keeps playback across rounds, and corrects drift only at a bar boundary',()=>{
 const s=new Simulation(),audio=new BossTheme(),calls:{offset:number;stopped:boolean}[]=[],gains:number[]=[];
 const context={state:'running',currentTime:10,destination:{},createGain:()=>({gain:{setTargetAtTime(n:number){gains.push(n);}},connect(){},disconnect(){}}),createBufferSource:()=>{const c={offset:-1,stopped:false};calls.push(c);return {loop:false,loopStart:0,loopEnd:0,buffer:null,connect(){},disconnect(){},start(_at:number,offset:number){c.offset=offset;},stop(){c.stopped=true;}};}};
 try{(audio as any).buffer={duration:track.duration};const state=s.snapshot();state.phase='fight';state.bossSong={track:'black-siren-protocol',startedAt:100};state.time=120;audio.update(state,context as any,.2);assert.equal(calls[0].offset,20);
  state.generation=2;state.round=2;state.time+=.1;context.currentTime+=.1;audio.update(state,context as any,.2);assert.equal(calls.length,1);
  context.currentTime+=.2;audio.update(state,context as any,0);assert.equal(calls.length,1);assert.equal(gains.at(-1),0);assert.ok(audio.drift>.19);
  const next=barStart(track,15)+.001;context.currentTime+=next-(state.time-100);state.time=100+next;audio.update(state,context as any,.2);assert.equal(calls.length,2);assert.ok(calls[0].stopped);assert.ok(Math.abs(calls[1].offset-next)<1e-8);
  audio.pause();state.time=100+track.loop.end+3;audio.update(state,context as any,.2);assert.ok(Math.abs(calls.at(-1)!.offset-loopedSongTime(track,track.loop.end+3))<1e-8);
  state.phase='match';audio.update(state,context as any,.2);assert.ok(calls.at(-1)!.stopped);assert.equal(audio.status,'idle');
 }finally{s.dispose();audio.stop();}
});

test('drift uses the short difference at the loop wrap',()=>{
 assert.ok(Math.abs(songDrift(track.loop.start+.01,track.loop.end-.01)-.02)<1e-9);
 assert.equal(songDrift(1.1,1),1.1-1);
});

test('prediction replays Siren roots/dashes and freezes a grabbed local victim',()=>{
 const s=new Simulation({sandbox:true}),a=s.addPlayer('a','Boss','dev',null,'#ffffff',{},true)!,b=s.addPlayer('b')!,p=new Predictor();
 try{a.spec='siren';s.phase='fight';a.body.setTranslation(v(0,1,0),true);a.siren={mode:'melee',modeAt:0,move:{id:'laugh',seq:1,at:0,hitAt:2,endAt:2.35,until:3.25,dir:v(0,0,1)}};
  let snap=s.snapshot(),view=snap.players[0];p.reconcile(snap,view);for(let n=0;n<30;n++)p.step({...idleInput(),seq:n+1,x:1,dash:true},1/60,view);assert.ok(Math.abs(p.pos.x)<.02);assert.equal(p.roll,undefined);
  assert.equal(sirenMotion(a.siren.move,2.1,1/60).step.z,20);assert.equal(sirenMotion(a.siren.move,3.3,1/60).locked,false);
  s.siren.grabs=[{owner:a.id,victim:b.id,at:0,crushAt:3.2,releaseAt:3.8}];snap=s.snapshot();view=snap.players[1];p.reconcile(snap,view);const held={...p.pos};p.step({...idleInput(),seq:200,x:1,jump:true},1/60,view);assert.deepEqual(p.pos,held);assert.equal(p.pending.length,0);
 }finally{p.dispose();s.dispose();}
});

test('Siren rig routes through the facade without ordinary weapon animation or camera crashes',()=>{
 const s=new Simulation(),a=s.addPlayer('a','Boss','dev',null,'#ffffff',{},true)!,r=createCharacterRig('#ffffff'),scene=new THREE.Scene(),camera=new THREE.PerspectiveCamera(),intro=new SirenIntroStage(scene);
 try{a.spec='siren';const snap=s.snapshot(),view=snap.players[0];sirenify(r);scene.add(r.group);for(const time of [0,.2,1,3,6.43]){animateCharacter(r,view,time,1/60);animateSiren(r,view,snap,time,1/60,0);intro.update(time,r,camera,1);scene.updateMatrixWorld(true);scene.traverse(o=>assert.ok(o.matrixWorld.elements.every(Number.isFinite)));assert.ok(camera.position.toArray().every(Number.isFinite));}intro.setActive(false);
 }finally{s.dispose();}
});
