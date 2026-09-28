import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {readFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {ArmorGlow} from '../src/client/armor-glow.js';
import {createCharacterRig} from '../src/client/character-rig.js';
import {poseIntro,introCamera} from '../src/client/intro-choreography.js';
import {IntroAudio} from '../src/client/intro-audio.js';
import {ArenaMusic} from '../src/client/music.js';
import {INTRO,INTRO_STYLES,IntroStyle,introDuration,introNameAt} from '../src/shared/intros.js';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
await initPhysics();
test('armor lights illuminate the scene, pulse with cooldown, avoid first-person torso ghosts, and clean up',()=>{
 const scene=new T.Scene(),glow=new ArmorGlow(scene),rig=createCharacterRig('#ffaa44',undefined,true),view=createCharacterRig('#ffaa44',undefined,true),p={id:'a',color:'#ffaa44',hp:100,blockCd:2,blockTime:0};scene.add(rig.group);
 glow.update([{rig,player:p},{rig:view,player:{...p,id:'view'},light:false}],0,1);assert.equal(glow.count,2);assert.equal(glow.lights.filter(l=>l.intensity>0).length,1);assert.equal(glow.lights[0].intensity,8);const halo=rig.group.getObjectByName('armor-radiance') as T.Sprite;assert.ok(halo.material.depthTest);assert.equal(halo.material.depthWrite,false);assert.equal(view.chest.children.filter(o=>o.name==='armor-radiance').length,0);
 glow.update([{rig,player:p}],1/3,1);assert.equal(glow.count,1);assert.ok(glow.lights[0].intensity<3.3);glow.update([{rig,player:{...p,blockCd:0,color:'#33ccff'}}],.5,1);assert.equal(glow.lights[0].intensity,8);assert.ok(glow.lights[0].color.b>glow.lights[0].color.r);
 glow.update([{rig,player:p}],.6,.3,true);assert.ok(glow.lights.every(l=>l.intensity===0));assert.equal(halo.visible,false);glow.update([{rig,player:{...p,hp:0}}],1,1);assert.equal(glow.count,0);assert.equal(rig.group.getObjectByName('armor-radiance'),undefined);glow.dispose();assert.equal(scene.children.length,1);
});
test('all camera sequences are rewindable and keep the final full-body pose in frame at desktop aspect ratios',()=>{
 for(const style of Object.keys(INTRO_STYLES) as IntroStyle[]){const rig=createCharacterRig('#44ddff'),camera=new T.PerspectiveCamera(43,1,.1,80),shots=new Set<number>();
  for(let t=0;t<introDuration(style);t+=.1){poseIntro(rig,style,t);shots.add(introCamera(camera,style,t,rig.group.position,16/9).shot);assert.ok([...camera.position.toArray(),...camera.quaternion.toArray()].every(Number.isFinite));}assert.equal(shots.size,4,style);
  for(const aspect of [4/3,16/9,21/9])for(const t of [introNameAt(style),introDuration(style)-.1]){poseIntro(rig,style,t);introCamera(camera,style,t,rig.group.position,aspect);rig.group.updateMatrixWorld(true);camera.updateMatrixWorld(true);const box=new T.Box3().setFromObject(rig.group);for(const x of [box.min.x,box.max.x])for(const y of [box.min.y,box.max.y])for(const z of [box.min.z,box.max.z]){const screen=new T.Vector3(x,y,z).project(camera);assert.ok(Math.abs(screen.x)<.92&&Math.abs(screen.y)<.92&&screen.z>-1&&screen.z<1,`${style}: ${screen.toArray()}`);}}
  poseIntro(rig,style,2);introCamera(camera,style,2,rig.group.position,16/9);const expected=[...camera.position.toArray(),...camera.quaternion.toArray()];poseIntro(rig,style,.2);introCamera(camera,style,.2,rig.group.position,16/9);poseIntro(rig,style,2);introCamera(camera,style,2,rig.group.position,16/9);assert.deepEqual([...camera.position.toArray(),...camera.quaternion.toArray()],expected);
 }
});
test('intro songs seek late snapshots, deduplicate, switch players, honor music mute, and cancel previews',()=>{
 const s=new Simulation();s.addPlayer('a','A');s.addPlayer('b','B');const state=s.snapshot();state.phase='intro';state.intro={started:10,players:['a','b']};state.time=11;
 const calls:{start:number[];stops:number[]}[]=[],context={state:'running',currentTime:20,destination:{},createGain:()=>({gain:{value:0,setTargetAtTime(){}},connect(){return this;},disconnect(){}}),createBufferSource:()=>{const call={start:[] as number[],stops:[] as number[]};calls.push(call);return {connect(){return this;},disconnect(){},start(...args:number[]){call.start=args;},stop(at=0){call.stops.push(at);}};}},audio=new IntroAudio();audio.buffers.set('neon-drop',{duration:4.85} as AudioBuffer);
 audio.update(state,context as any,.3);assert.deepEqual(calls[0].start,[20,1]);audio.update(state,context as any,.3);assert.equal(calls.length,1);state.time=10+INTRO.seconds+.2;audio.update(state,context as any,.3);assert.equal(calls.length,2);assert.ok(Math.abs(calls[1].start[1]-.2)<1e-8);assert.equal(calls[0].stops.at(-1),0);
 audio.update(state,context as any,0);assert.equal(calls[1].stops.at(-1),0);audio.update(state,context as any,.3);assert.equal(calls.length,3);audio.stop();assert.equal(calls[2].stops.at(-1),0);audio.update({...state,phase:'fight'},context as any,.3);assert.equal(calls.length,3);
 const music=new ArenaMusic();music.configure(.5,false);assert.equal(music.cueVolume,.4);music.configure(.5,true);assert.equal(music.cueVolume,0);s.dispose();
});
test('all original intro music assets are distinct',()=>{
 const hashes=new Set<string>();for(const style of Object.keys(INTRO_STYLES)){const bytes=readFileSync(`public/audio/intros/${style}.mp3`);assert.ok(bytes.length>20000);hashes.add(createHash('sha256').update(bytes).digest('hex'));}assert.equal(hashes.size,7);
});
