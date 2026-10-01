/** The public impact ragdoll consuming the body-damage contract (docs/BODY-DAMAGE-HANDOFF.md), with the real Rapier
 * world: severed parts with voxel caps, crushed parts as bounded fragments (loose hat/weapon), the kneel/fall handoff,
 * living wounds carried into the corpse, budgets, late snapshots and disposal. Cosmetic effects keep their behaviour. */
import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {v,type Vec,type SpecId} from '../src/shared/config.js';
import {initPhysics,type DeathPresentation,type PlayerView,type Snapshot} from '../src/shared/simulation.js';
import {resolveBodyWound,deathMotion,projectileBodyImpact,meleeBodyImpact,type BodyImpact,type BodyWound} from '../src/shared/body-damage.js';
import {KILL_EFFECTS} from '../src/client/death-effects.js';
import {BodyWounds} from '../src/client/body-wounds.js';
import {currentLayout} from '../src/client/voxel-kit.js';
import {createCharacterRig} from '../src/client/character-rig.js';
import {dressRig} from '../src/client/cosmetics.js';
import {buildWeapon} from '../src/client/weapon-visuals.js';
import type {Cosmetics} from '../src/shared/cosmetics.js';
await initPhysics();

const pos=v(0,.85,0),rifle=(speed:number)=>projectileBodyImpact({spec:'rifle',vel:v(0,0,-speed),radius:.045}),shield=(speed:number)=>({...meleeBodyImpact('shield'),speed});
type Case={impact:BodyImpact;local:Vec;dir:Vec;damage:number;headshot?:boolean;melee?:boolean;earlier?:BodyWound[];spec?:SpecId;hat?:string;effect?:string;air?:boolean};
function scenario(c:Case){
 const point=v(pos.x+c.local.x,pos.y+c.local.y,pos.z+c.local.z),fatal=resolveBodyWound({id:'fatal',at:1,position:pos,yaw:0,point,direction:c.dir,impact:c.impact,damage:c.damage,lethal:true,headshot:c.headshot});
 const velocity=c.air?v(0,2.4,-2):v(0,0,-.3),motion=deathMotion({wound:fatal,grounded:!c.air,incapacitated:false,yaw:0,velocity}),effect=c.effect||'impact-ragdoll';
 const death:DeathPresentation={wound:fatal,motion,id:'d',at:1,effect,seed:4242,pos,velocity,direction:c.dir,source:'a',kind:'direct',yaw:0,hitPoint:point,melee:!!c.melee,force:350,gibPart:effect==='impact-ragdoll'?fatal.destroyedPart:undefined};
 const cosmetics={bodyColor:'#e5e7eb',hat:c.hat||'none',killEffect:'impact-ragdoll'} as Cosmetics;
 const p={id:'b',name:'b',color:'#3fc6ff',class:'ranged',spec:c.spec||'pistol',pos,vel:v(0,0,0),yaw:0,pitch:0,hp:0,maxHp:100,mana:0,maxMana:0,ammo:0,maxAmmo:0,stamina:100,maxStamina:100,blockTime:0,blockCd:0,blockCooldown:0,blockDuration:0,lastBlock:-10,guarding:false,dashCd:0,secondaryCd:0,cooldown:0,reloadTime:0,score:0,roundWins:0,chips:{},ready:true,selected:true,offers:[],connected:true,bot:null,ack:0,grounded:!c.air,lastAttack:-10,lastSecondary:-10,reach:1,windup:0,statuses:[],cosmetics,wounds:[...(c.earlier||[]),fatal],death} as PlayerView;
 const state={generation:1,setNumber:1,round:1,time:1,players:[p],props:[]} as unknown as Snapshot;
 const make=()=>{const rig=createCharacterRig(p.color,cosmetics.bodyColor,true);dressRig(rig,cosmetics);buildWeapon(rig.weapon,p.spec);rig.group.position.set(pos.x,pos.y,pos.z);
  const layer=new BodyWounds(new T.Scene());layer.update(state,new Map([['b',rig]]),{quality:1,dt:1/60});return {rig,layer};};
 return {fatal,motion,death,p,state,make};
}
const run=(s:ReturnType<typeof scenario>,budget:number,quality=1)=>{const scene=new T.Scene(),{rig,layer}=s.make(),fx=KILL_EFFECTS[s.death.effect](scene,rig.group,s.p,s.death,quality,budget,s.state);return {scene,fx,rig,layer};};
const finite=(scene:T.Scene)=>{scene.updateMatrixWorld(true);let ok=true;scene.traverse(o=>{if(!o.matrixWorld.elements.every(Number.isFinite))ok=false;});return ok;};
const names=(scene:T.Scene,name:string)=>{const out:T.Object3D[]=[];scene.traverse(o=>{if(o.name===name)out.push(o);});return out;};
const earlier=[resolveBodyWound({id:'w0',at:.5,position:pos,yaw:0,point:v(-.04,1.01,.3),direction:v(0,0,-1),impact:rifle(150),damage:58,lethal:false})];
const CASES:Record<string,Case>={
 decap:{impact:rifle(270),local:v(0,.64,.3),dir:v(0,0,-1),damage:87,headshot:true},
 cavity:{impact:rifle(150),local:v(.03,.64,.3),dir:v(0,0,-1),damage:87,headshot:true,earlier},
 arm:{impact:meleeBodyImpact('greatsword'),local:v(.36,.12,.05),dir:v(-1,0,-.15),damage:52,melee:true},
 head:{impact:shield(22),local:v(0,.66,.3),dir:v(0,0,-1),damage:23,headshot:true,melee:true,hat:'top'},
 forearm:{impact:shield(20),local:v(-.36,-.18,.2),dir:v(.9,0,-.4),damage:23,melee:true,spec:'sword'},
 torso:{impact:projectileBodyImpact({spec:'pistol',vel:v(0,0,-52),radius:.045}),local:v(-.05,.1,.3),dir:v(0,0,-1),damage:23,earlier},
 airborne:{impact:rifle(150),local:v(.03,.64,.3),dir:v(0,0,-1),damage:87,headshot:true,air:true},
};

test('impact ragdoll consumes the authority outcome: sever, crush, kneel or plain ragdoll',()=>{
 const s=Object.fromEntries(Object.entries(CASES).map(([k,c])=>[k,scenario(c)]));
 assert.deepEqual([s.decap.fatal.kind,s.decap.motion.kind,s.cavity.fatal.kind,s.cavity.motion.kind,s.arm.fatal.kind,s.head.fatal.kind,s.forearm.fatal.kind,s.torso.fatal.kind,s.torso.motion.kind,s.airborne.motion.kind],
  ['sever','kneel-fall','cavity','kneel-fall','sever','crush','crush','puncture','ragdoll','ragdoll']);
 // Severed: the struck part detaches with a matching cap on each side and the bounded spray.
 for(const key of ['decap','arm']){const {scene,fx,layer}=run(s[key],220),root=scene.getObjectByName('ragdoll')!;assert.equal(root.userData.gibPart,s[key].fatal.destroyedPart);assert.equal(names(scene,'gibber-wound').length,2,key);
  fx.update(.35);const part=root.getObjectByName(s[key].fatal.destroyedPart!)!,parent=root.getObjectByName('spine')!;assert.ok(part.position.distanceTo(parent.position)>.25,`${key} detached`);assert.ok(names(scene,'gibber-spray').length===1);
  assert.ok(finite(scene));fx.dispose();fx.dispose();layer.dispose();assert.equal(scene.children.length,0);}
 // Crushed: no body for the struck part, bounded fragments, its hat or weapon thrown loose, ragged stumps.
 for(const [key,loose] of [['head','crush-head-cosmetic'],['forearm','crush-weapon-socket']] as const){const {scene,fx,layer}=run(s[key],220),root=scene.getObjectByName('ragdoll')!,crushed=s[key].fatal.destroyedPart!;
  assert.equal(root.getObjectByName(crushed),undefined,`${key}: no body for the crushed part`);assert.ok(scene.getObjectByName('crush-fragments'),key);assert.ok(scene.getObjectByName(loose),`${key}: ${loose}`);assert.ok(names(scene,'gibber-wound').length>=1,key);
  for(const age of [.1,.5,2,4.9]){fx.update(age);assert.ok(finite(scene),`${key} ${age}`);}fx.dispose();layer.dispose();assert.equal(scene.children.length,0);}
 // Not destroyed: every part stays, no caps; a pistol death is never an explosion.
 for(const key of ['torso','airborne','cavity']){const {scene,fx,layer}=run(s[key],220);assert.equal(scene.getObjectByName('ragdoll')!.children.length,11,key);assert.equal(names(scene,'gibber-wound').length,0,key);assert.equal(scene.getObjectByName('crush-fragments'),undefined);assert.ok(fx.count<=20,`${key} ${fx.count}`);fx.dispose();layer.dispose();}
});

test('impact ragdoll respects every budget and quality, stays finite and disposes',()=>{
 for(const [key,c] of Object.entries(CASES))for(const budget of [0,1,12,40,100,220])for(const quality of [1,.3]){const {scene,fx,layer}=run(scenario(c),budget,quality);assert.ok(fx.count<=budget,`${key} ${budget} ${quality}: ${fx.count}`);
  for(const age of [0,.2,.5,.9,2,4.99])fx.update(age);assert.ok(finite(scene),`${key} ${budget}`);fx.dispose();fx.dispose();layer.dispose();assert.equal(scene.children.length,0,`${key} ${budget} disposed`);}
});

test('kneel/fall: scripted until the step-aligned release, identical for late and stepped snapshots, then continuous physics',()=>{
 for(const key of ['cavity','decap']){const s=scenario(CASES[key]),stepped=run(s,220),late=run(s,220),pelvis=(x:{scene:T.Scene})=>x.scene.getObjectByName('ragdoll')!.getObjectByName('pelvis')!;
  const start=pelvis(stepped).position.clone();
  for(let n=0;n<=30;n++)stepped.fx.update(n/60);const kneel=pelvis(stepped).position.clone();assert.ok(kneel.y<start.y-.08,`${key} on the knees (${start.y.toFixed(2)} -> ${kneel.y.toFixed(2)})`);
  // Late snapshot straight to .5 s: the same scripted pose.
  late.fx.update(.5);stepped.fx.update(.5);assert.ok(pelvis(late).position.distanceTo(pelvis(stepped).position)<1e-9,`${key} late == stepped before release`);
  const chest=(x:{scene:T.Scene})=>x.scene.getObjectByName('ragdoll')!.getObjectByName('spine')!.position.clone();
  // Through the handoff: no frame jumps more than a fall can travel in one step.
  let prev=chest(stepped);for(let n=31;n<=90;n++){stepped.fx.update(n/60);const now=chest(stepped);assert.ok(now.distanceTo(prev)<.2,`${key} continuous at ${(n/60).toFixed(3)}`);prev=now;}
  late.fx.update(1.5);assert.ok(pelvis(late).position.distanceTo(pelvis(stepped).position)<1e-6,`${key} late == stepped after release`);
  assert.ok(chest(stepped).z>start.z+.2,`${key} fell forward`);for(const x of [stepped,late]){x.fx.dispose();x.layer.dispose();}}
});

test('living wounds ride into the corpse; cosmetic finishers keep their own death',()=>{
 const s=scenario(CASES.torso),{scene,fx,layer}=run(s,220),spine=scene.getObjectByName('ragdoll')!.getObjectByName('spine')!;let cells=0;spine.traverse(o=>{if(o.name==='wound-cells')cells++;});
 assert.ok(cells>=1,'flesh cells of the earlier cavity and the fatal puncture');assert.ok(currentLayout(spine.getObjectByName('ribcage')!)?.custom,'the corpse keeps the carved chest');fx.dispose();layer.dispose();
 const finisher=scenario({...CASES.decap,earlier,effect:'ragdoll-ember'}),f=run(finisher,220);assert.equal(f.scene.getObjectByName('ragdoll')!.children.length,11,'a cosmetic ragdoll severs nothing');assert.equal(names(f.scene,'gibber-wound').length,0);
 let carried=0;f.scene.getObjectByName('ragdoll')!.traverse(o=>{if(o.name==='wound-cells')carried++;});assert.ok(carried>=1,'earlier wounds still ride along');f.fx.dispose();f.layer.dispose();
});
