import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {Simulation,initPhysics,GameEvent,Snapshot} from '../src/shared/simulation.js';
import {INTRO,INTRO_STYLES,introFrame,IntroStyle} from '../src/shared/intros.js';
import {cosmetics} from '../src/shared/cosmetics.js';
import {HostedRoom} from '../src/shared/host-room.js';
import {idleInput,v} from '../src/shared/config.js';
import {createCharacterRig,recolorCharacterRig} from '../src/client/character-rig.js';
import {PlayerIntros,poseIntro} from '../src/client/player-intros.js';
import {ImpactGlows} from '../src/client/impact-glows.js';
import {BlockCooldownTint} from '../src/client/block-cooldown-tint.js';
await initPhysics();
function ready(s:Simulation){s.addPlayer('a','NEON & ONE','ranged',null,'#40dfff',{intro:'signal-spin'});s.addPlayer('b','SECOND','melee',null,'#ff55ce',{intro:'arc-salute'});s.setReady('a',true);s.setReady('b',true);s.selectSpec('a','pistol');s.selectSpec('b','sword');}
test('intros default on, lock combat, sequence both names from authority time, then play the opening countdown',()=>{
 const s=new Simulation();ready(s);assert.equal(s.phase,'intro');assert.deepEqual(s.snapshot().intro?.players,['a','b']);const started=s.time;
 assert.equal(introFrame(s.snapshot())?.nameVisible,false);s.time=started+INTRO.nameAt+.1;assert.equal(introFrame(s.snapshot())?.player.name,'NEON & ONE');assert.equal(introFrame(s.snapshot())?.nameVisible,true);
 s.setInput('a',{...idleInput(),seq:1,x:1,attack:true,block:true});for(let i=0;i<10;i++)s.step();assert.equal(s.projectiles.size,0);assert.equal(s.players.get('a')!.blockCd,0);assert.equal(s.players.get('a')!.body.translation().x,0);
 assert.equal(s.queueInputs('a',[{...idleInput(),seq:2}],`${s.setNumber}:${s.round}:${s.generation}`),false);
 s.time=started+INTRO.seconds+.6;const late=structuredClone(s.snapshot());assert.equal(introFrame(late)?.player.id,'b');assert.ok(Math.abs(introFrame(late)!.age-.6)<.001);assert.equal(introFrame(late)?.style,'arc-salute');
 s.time=started+2*INTRO.seconds;s.advance();assert.equal(s.phase,'countdown');assert.equal(s.intro,undefined);s.time=s.phaseEnd;s.advance();assert.equal(s.phase,'fight');
 s.beginNextSet();assert.equal(s.phase,'countdown','later sets do not replay intros');s.phase='match';s.rematch();s.selectSpec('a','pistol');s.selectSpec('b','sword');assert.equal(s.phase,'intro','rematch does replay intros');s.dispose();
});
test('skip is a host-owned boolean; old and malformed cosmetics default safely; all five styles propagate',()=>{
 assert.equal(cosmetics({intro:'__proto__'}).intro,'neon-drop');assert.equal(cosmetics({}).intro,'neon-drop');assert.equal(Object.keys(INTRO_STYLES).length,5);
 for(const intro of Object.keys(INTRO_STYLES))assert.equal(cosmetics({intro}).intro,intro);
 for(const skip of [false,true,'true']){const s=new Simulation({skipIntros:skip as boolean});ready(s);assert.equal(s.phase,skip===true?'countdown':'intro');s.dispose();}
 const room=new HostedRoom('ABCDEFGH',{name:'Host',class:'ranged',color:'#12ccff',cosmetics:cosmetics({intro:'pulse-wave'})},false,()=>{},6,false,true);room.join({name:'Guest',cosmetics:{intro:'vector-slide'},skipIntros:false});assert.equal(room.receive('guest',{type:'options',skipIntros:false}),false);assert.equal(room.sim.snapshot().options.skipIntros,true);assert.equal(room.sim.snapshot().players[1].cosmetics?.intro,'vector-slide');room.dispose();
});
test('every intro is finite, deterministic after a time jump, distinctive and leaves the actual player untouched',()=>{
 const s=new Simulation();ready(s);const state=s.snapshot(),stage=new PlayerIntros(),root=state.players[0].pos,poses=new Set<string>();
 for(const style of Object.keys(INTRO_STYLES) as IntroStyle[]){const r=createCharacterRig('#44ddff',undefined,true);for(const age of [0,.35,.65,1,1.4,2.25,3.59]){poseIntro(r,style,age);r.group.updateMatrixWorld(true);r.group.traverse(o=>assert.ok(o.matrixWorld.elements.every(Number.isFinite),style));}poseIntro(r,style,1);poses.add(JSON.stringify([r.group.position.toArray(),r.group.rotation.toArray(),r.rightArm.rotation.toArray()]));const expected=r.group.matrixWorld.clone();poseIntro(r,style,.2);poseIntro(r,style,3.59);r.group.updateMatrixWorld(true);assert.deepEqual(r.group.matrixWorld.elements,expected.elements);
  state.players[0].cosmetics=cosmetics({intro:style});state.time=2.6;assert.ok(stage.update(state,16/9));assert.deepEqual(state.players[0].pos,root);
 }assert.equal(poses.size,5);stage.update({...state,phase:'fight'},16/9);assert.equal(stage.scene.getObjectByName('blank-mannequin-v2'),undefined);stage.dispose();s.dispose();
});
test('armor emits its accent and still pulses during block cooldown without changing another pilot',()=>{
 const a=createCharacterRig('#55ddff','#111111',true),b=createCharacterRig('#55ddff','#111111',true),tint=new BlockCooldownTint(),mesh=a.group.getObjectByName('sternum') as T.Mesh,other=b.group.getObjectByName('sternum') as T.Mesh,original=mesh.material as T.MeshLambertMaterial,base=original.emissive.clone();assert.ok(original.emissiveIntensity>.5);assert.ok(base.r+base.g+base.b>0);
 tint.update(a.group,{hp:100,blockCd:2,blockTime:0},1/3);assert.ok((mesh.material as T.MeshLambertMaterial).emissive.r<base.r*.5);assert.equal(other.material,original);assert.ok(original.emissive.equals(base));tint.release(a.group);assert.equal(mesh.material,original);recolorCharacterRig(a,'#ff4488','#222222');assert.ok((mesh.material as T.MeshLambertMaterial).emissiveIntensity>.5);
});
test('surface glows attach to moving cover, respect depth and quality budgets, expire and reset',()=>{
 const scene=new T.Scene(),prop=new T.Group(),props=new Map([['wall',prop]]);scene.add(prop);const glow=new ImpactGlows(scene),state={generation:1,props:[{id:'wall'}]} as Snapshot;
 glow.update(state,0,props,1);const e:GameEvent={id:1,time:0,type:'impact',pos:v(0,1,.1),surface:v(0,1,0),normal:v(0,0,1),target:'wall',material:'metal',color:0x64eeff};
 for(let n=0;n<80;n++)glow.event(e,0,props,1);assert.equal(glow.marks.length,32);const m=glow.marks[0],mat=m.halo.material as T.MeshBasicMaterial;assert.equal(mat.depthTest,true);assert.equal(mat.depthWrite,false);assert.equal(mat.toneMapped,false);
 prop.position.x=3;prop.rotation.y=Math.PI/2;glow.update(state,.2,props,1);assert.ok(Math.abs(m.group.position.x-3.018)<.001);assert.ok(Math.abs(m.group.position.z)<.001);assert.ok(glow.lights.filter(l=>l.intensity>0).length<=2);
 glow.update(state,1,props,1);assert.equal(glow.marks.length,0);for(let n=0;n<20;n++)glow.event(e,1,props,.3);assert.equal(glow.marks.length,12);glow.update({...state,generation:2},1.1,props,1);assert.equal(glow.marks.length,0);assert.ok(glow.lights.every(l=>l.intensity===0));glow.event(e,2,props,1);glow.update({...state,props:[]},2.1,props,1);assert.equal(glow.marks.length,0);glow.dispose();assert.equal(scene.children.length,1);
});
test('real projectile sweeps report the actual map face and outward normal, including rotated cover and ricochets',()=>{
 for(const angle of [0,.6]){const s=new Simulation({sandbox:true});s.addPlayer('a');for(const p of s.props.values()){s.colliderOwners.delete(p.collider.handle);s.world.removeRigidBody(p.body);}s.props.clear();
  const normal=v(-Math.sin(angle),0,-Math.cos(angle));s.addProp({id:'wall',pos:v(0,3,0),size:v(5,5,.4),material:'foundation',rotation:{x:0,y:Math.sin(angle/2),z:0,w:Math.cos(angle/2)}});s.world.step();
  s.projectiles.set('test',{id:'test',pos:v(normal.x*3,3,normal.z*3),vel:v(-normal.x*20,0,-normal.z*20),owner:'a',spec:'pistol',damage:1,impulse:0,life:3,radius:.1,direct:false,attackId:'test',bounces:1,pierces:0,hit:new Set(),effectsUsed:true,homing:0,distanceLeft:20,gravity:0});
  s.stepProjectiles(.2);const e=s.events.find(e=>e.type==='impact')!;assert.ok(e?.surface&&e.normal);assert.equal(e.target,'wall');assert.ok(Math.hypot(e.normal.x-normal.x,e.normal.z-normal.z)<.001);assert.ok(Math.abs(e.surface.x*normal.x+e.surface.z*normal.z-.2)<.001);assert.ok(s.projectiles.get('test')!.vel.x*normal.x+s.projectiles.get('test')!.vel.z*normal.z>0,'bounce travels away from the surface');s.dispose();
 }
});
