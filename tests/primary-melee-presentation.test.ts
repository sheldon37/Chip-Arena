import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {Simulation,initPhysics,type PlayerView} from '../src/shared/simulation.js';
import {WEAPONS} from '../src/shared/content.js';
import {type SpecId,v} from '../src/shared/config.js';
import {createCharacterRig} from '../src/client/character-rig.js';
import {animateCharacter,animationPreviewPlayer} from '../src/client/character-animation.js';
import {buildWeapon} from '../src/client/weapon-visuals.js';
import {SnapshotBuffer} from '../src/client/snapshots.js';
import {DeathAnnouncements,skyBroadcastFrame} from '../src/client/death-announcements.js';
await initPhysics();
function fixture(){const sim=new Simulation({sandbox:true});sim.addPlayer('a','NOVA','melee');sim.addPlayer('b','Other','melee');sim.phase='fight';const snapshot=sim.snapshot();sim.dispose();return snapshot;}
function player(spec:SpecId,index=1):PlayerView{const w=WEAPONS[spec];return {...fixture().players[0],spec,class:'melee',grounded:true,vel:v(),yaw:0,pitch:0,lastAttack:0,attackIndex:index,windup:w.windup,swingAt:w.windup,swingUntil:w.windup+.16,reach:w.range};}
function rigFor(spec:SpecId){const r=createCharacterRig('#70e1d0');buildWeapon(r.weapon,spec);if(spec==='daggers')buildWeapon(r.offWeapon,spec,true);return r;}
function sample(spec:SpecId,age:number,index=1){const r=rigFor(spec);animateCharacter(r,player(spec,index),age,1/60);r.group.updateMatrixWorld(true);return r;}
test('primary contact plants the leading ankle while the hips crouch, turn and step through',()=>{
 for(const spec of ['daggers','sword','greatsword','shield'] as SpecId[]){const r=sample(spec,WEAPONS[spec].windup+.104),foot=r.leftFoot.getWorldPosition(new T.Vector3());assert.ok(Math.abs(foot.y+.795)<.008,spec+' planted ankle');assert.ok(foot.z>.29,spec+' forward step');assert.ok(r.body.position.y<-.16,spec+' body drop');assert.ok(r.body.rotation.y>.15,spec+' hips drive contact');assert.ok(r.leftKnee.quaternion.angleTo(new T.Quaternion())>.5,spec+' knee bends');}
});
test('daggers use both hands, a low third cut and a forward full turn on the fourth cut',()=>{
 const a=sample('daggers',.09,1),b=sample('daggers',.09,2),low=sample('daggers',.20,3);assert.ok(a.rightHand.getWorldPosition(new T.Vector3()).distanceTo(b.rightHand.getWorldPosition(new T.Vector3()))>.3);assert.ok(low.body.position.y<-.31);
 const turn=[.18,.25,.30,.34,.39].map(age=>sample('daggers',age,4).body.rotation.y);for(let i=1;i<turn.length;i++)assert.ok(turn[i]>turn[i-1],'spin follows through rather than unwinding');assert.ok(turn.at(-1)!>6.2);
});
test('greatsword sweeps low then rises; sword combo alternates turn and adds an overhead cut',()=>{
 const low=sample('greatsword',.48),rise=sample('greatsword',.8),tip=(r:ReturnType<typeof rigFor>)=>r.weapon.getObjectByName('blade-tip')!.getWorldPosition(new T.Vector3());assert.ok(tip(rise).y-tip(low).y>.6);assert.ok(low.body.position.y<-.30);assert.ok(rise.body.position.y>low.body.position.y+.1);
 const cuts=[1,2,3].map(i=>sample('sword',.14,i));assert.ok(cuts[0].body.rotation.y<-.7);assert.ok(cuts[1].body.rotation.y>.7);assert.ok(tip(cuts[2]).y>tip(cuts[0]).y+.8);
});
test('new combo and return-to-stance transitions preserve the previous pose at the boundary',()=>{
 const r=rigFor('daggers'),p=player('daggers');for(let t=0;t<.37;t+=1/120)animateCharacter(r,p,t,1/120);const before=r.body.position.clone(),arm=r.rightArm.quaternion.clone();animateCharacter(r,{...p,attackIndex:2,lastAttack:.37,swingAt:.46,swingUntil:.62},.37,1/120);assert.ok(r.body.position.distanceTo(before)<1e-8);assert.ok(r.rightArm.quaternion.angleTo(arm)<1e-6);
 const s=rigFor('sword'),q=player('sword'),end=.14+.16+.3;animateCharacter(s,q,end-.001,1/120);const prior=s.rightArm.quaternion.clone();animateCharacter(s,q,end+.001,1/120);assert.ok(s.rightArm.quaternion.angleTo(prior)<1e-6,'return blends at the true animation end');
});
test('remote interpolation never replaces an in-flight cut with a future combo; local animation uses the current clock',()=>{
 const base=fixture(),first={...base,time:10,tick:600,players:base.players.map(p=>({...p,spec:'daggers' as const,lastAttack:9.9,attackIndex:1,swingAt:9.99,swingUntil:10.15}))},second={...first,time:10.2,tick:612,players:first.players.map(p=>({...p,hp:72,lastAttack:10.18,attackIndex:2,swingAt:10.27,swingUntil:10.43}))},buffer=new SnapshotBuffer();buffer.push(first,1000);buffer.push(second,1200);let out=buffer.sample(second,'a',1200);assert.ok(out.time<10.18);assert.equal(out.players[1].attackIndex,1);assert.equal(out.players[1].lastAttack,9.9);assert.equal(out.players[1].hp,72,'newest health is retained');assert.equal(out.players[0].attackIndex,2);assert.equal(out.players[0].animationTime,10.2);
 for(let now=1216;now<=1472;now+=16)out=buffer.sample(second,'a',now);assert.ok(out.time>=10.18);assert.equal(out.players[1].attackIndex,2);assert.equal(out.players[1].animationTime,out.time);assert.ok(out.players[0].animationTime!>out.time);assert.equal(animationPreviewPlayer(out.players[0],'attack',12).animationTime,undefined);
});
test('a new remote windup begins at its timestamp, before the later packet sample time',()=>{
 const base=fixture(),first={...base,time:1,tick:60},second={...base,time:1.1,tick:66,players:base.players.map(p=>({...p,spec:'sword' as const,lastAttack:1.01,attackIndex:3,swingAt:1.15,swingUntil:1.31}))},buffer=new SnapshotBuffer();buffer.push(first,1000);buffer.push(second,1100);const frame=buffer.sample(second,'a',1140);assert.ok(frame.time>=1.01&&frame.time<1.1);assert.equal(frame.players[1].attackIndex,3);
});
test('sky broadcasts occupy large fixed locations above the skyline with separated simultaneous slots',()=>{
 const frames=[0,1,2].map(slot=>skyBroadcastFrame(slot));
 for(const frame of frames){assert.ok(frame.position.y-4.94*frame.scale>40);assert.ok(frame.position.z<-80);assert.ok(frame.scale*8.4>40);}
 for(let i=0;i<frames.length;i++)for(let j=i+1;j<frames.length;j++)assert.ok(frames[i].position.distanceTo(frames[j].position)>6.6*frames[i].scale);
});
test('portrait stays in the world when moving, turning or scoping, respects scenery depth and cleans up',()=>{
 const scene=new T.Scene(),camera=new T.PerspectiveCamera(60,1.6,.08,400),state=fixture(),p=state.players[0];state.time=1;p.hp=0;p.cosmetics={...p.cosmetics!,hat:'wizard'};p.death={id:'portrait-test',at:1,seed:3,pos:v(),velocity:v(),direction:v(),style:'voxel-burst',announcement:{victim:'NOVA',killer:null}} as any;
 const fx=new DeathAnnouncements(scene);fx.update(state,camera,true);state.time=2.2;fx.update(state,camera,true);const portrait=scene.getObjectByName('sky-portrait')!;assert.ok(portrait);assert.equal(portrait.getObjectByName('left-hip')!.visible,false);assert.ok(portrait.getObjectByName('hat'));portrait.traverseVisible(o=>{if(o instanceof T.Mesh||o instanceof T.Line){for(const m of Array.isArray(o.material)?o.material:[o.material])assert.equal(m.depthTest,true);}});const root=portrait.parent!,before=root.matrixWorld.clone();scene.updateMatrixWorld(true);before.copy(root.matrixWorld);camera.position.set(30,18,20);camera.rotation.set(.8,Math.PI,.2);camera.zoom=3;camera.updateProjectionMatrix();fx.update(state,camera,true);scene.updateMatrixWorld(true);assert.deepEqual(root.matrixWorld.elements,before.elements);
 const center=root.getWorldPosition(new T.Vector3());camera.lookAt(center);camera.updateMatrixWorld(true);assert.ok(center.clone().project(camera).length()<1.01);camera.lookAt(camera.position.clone().sub(center.clone().sub(camera.position)));camera.updateMatrixWorld(true);assert.ok(center.clone().sub(camera.position).dot(camera.getWorldDirection(new T.Vector3()))<0,'looking away must leave the hologram behind the viewer');
 const second=state.players[1];second.hp=0;second.death={...p.death!,id:'portrait-second',at:state.time};fx.update(state,camera,true);state.time+=1.6;fx.update(state,camera,true);scene.updateMatrixWorld(true);assert.deepEqual(root.matrixWorld.elements,before.elements,'another death must not shift an existing broadcast');
 fx.dispose();assert.equal(scene.children.length,0);
});

test('running cuts keep ankles above the floor throughout their active sweep',()=>{for(const spec of ['daggers','sword','greatsword','shield'] as SpecId[]){const r=rigFor(spec),p=player(spec);p.vel=v(0,0,6.8);for(let t=0;t<.4;t+=1/60)animateCharacter(r,{...p,lastAttack:-10,swingAt:undefined,swingUntil:undefined},t,1/60);for(let age=0;age<WEAPONS[spec].windup+.16;age+=1/60){animateCharacter(r,p,age,1/60);r.group.updateMatrixWorld(true);for(const f of [r.leftFoot,r.rightFoot])assert.ok(f.getWorldPosition(new T.Vector3()).y>-.81,spec+' ankle below floor');}}});
