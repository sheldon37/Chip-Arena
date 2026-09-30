import {test} from 'node:test';
import assert from 'node:assert/strict';
import {initPhysics} from '../src/shared/simulation.js';
import {WEAPONS} from '../src/shared/content.js';
import * as rig from '../src/client/character-rig.js';
import * as ik from '../src/client/rig-ik.js';
import * as primary from '../src/client/primary-melee-motion.js';
import * as quality from '../src/client/motion-quality.js';
import * as transitions from '../src/client/pose-transitions.js';
import {animateCharacter,animationPreviewPlayer} from '../src/client/character-animation.js';
import {markLegsAuthored,legsAuthored,clearLegsAuthored,markFirstPersonRig,isFirstPersonRig} from '../src/client/pose-flags.js';
import {recordBlockHit,blockImpact,clearBlockImpacts} from '../src/client/block-impacts.js';
import {STANCES,NEUTRAL_STANCE,stanceFor} from '../src/client/stances.js';
import {PROBE_RATES,probeBase,probeMotion,probeRates,probeRig,describeProbe} from './helpers/motion-probe.js';

await initPhysics();
const standard=Object.values(WEAPONS).filter(w=>!w.devOnly).map(w=>w.id);

test('split modules keep one implementation behind every old import path',()=>{
 assert.equal(rig.supportHand,ik.supportHand);assert.equal(rig.braceWeapon,ik.braceWeapon);
 assert.equal(primary.foot,ik.foot);assert.equal(primary.hand,ik.hand);
 assert.equal(quality.finishPose,transitions.finishPose);
 assert.equal(typeof rig.settleJointCovers,'function');assert.equal(typeof rig.setRigDetail,'function');
});

test('pose flags: foot IK marks authored legs, each sample starts clear, first-person marking is per rig',()=>{
 const a=rig.createCharacterRig('#fff'),b=rig.createCharacterRig('#fff');
 assert.equal(legsAuthored(a),false);markLegsAuthored(a);assert.equal(legsAuthored(a),true);assert.equal(legsAuthored(b),false);clearLegsAuthored(a);assert.equal(legsAuthored(a),false);
 assert.equal(isFirstPersonRig(a),false);markFirstPersonRig(a);assert.equal(isFirstPersonRig(a),true);assert.equal(isFirstPersonRig(b),false);markFirstPersonRig(a,false);assert.equal(isFirstPersonRig(a),false);
 const r=probeRig('sword'),base=probeBase('sword'),at=(mode:string,t:number)=>{animateCharacter(r,{...animationPreviewPlayer(base,mode,t),hitStunUntil:-1e9},t,1/60);return legsAuthored(r);};
 assert.equal(at('idle',.5),false,'locomotion alone does not author the legs');
 assert.equal(at('release',.6),true,'the melee phrase plants its feet through rig-ik foot()');
 assert.equal(at('idle',.7),false,'the flag never leaks into the next sample');
});

test('block impacts are keyed by the defender (blockHit source), bounded in time and rewind-safe',()=>{
 clearBlockImpacts();
 recordBlockHit({type:'hit',time:1,pos:{x:0,y:0,z:1},source:'d',target:'a'},1);
 assert.equal(blockImpact('d',1.1),undefined,'only blockHit events are recorded');
 recordBlockHit({type:'blockHit',time:2,pos:{x:0,y:1,z:.5},source:'d',target:'a',message:'melee'},2.02,{x:0,y:.8,z:0});
 const hit=blockImpact('d',2.1);
 assert.ok(hit);assert.equal(hit.at,2);assert.equal(hit.attacker,'a');assert.equal(hit.kind,'melee');assert.deepEqual(hit.dir,{x:0,y:0,z:1});
 assert.equal(blockImpact('a',2.1),undefined,'the attacker is not the blocker');
 assert.equal(blockImpact('d',1.99),undefined,'a rewind before the impact shows nothing');
 assert.equal(blockImpact('d',2.5),undefined,'the default window is .4 s');
 assert.ok(blockImpact('d',2.5,1));
 clearBlockImpacts();assert.equal(blockImpact('d',2.1),undefined);
});

test('stance table covers every spec with finite, foot-sized values',()=>{
 for(const spec of Object.keys(WEAPONS)){
  const s=stanceFor(spec);assert.equal(s,STANCES[spec as keyof typeof STANCES]);
  for(const [name,value] of Object.entries({leadX:s.lead.x,leadZ:s.lead.z,rearX:s.rear.x,rearZ:s.rear.z,crouch:s.crouch,pelvisYaw:s.pelvisYaw,kneeSoft:s.kneeSoft,lean:s.lean,leadToe:s.lead.toeOut,rearToe:s.rear.toeOut}))
   assert.ok(Number.isFinite(value)&&Math.abs(value)<1.2,`${spec} ${name}=${value}`);
  assert.ok(Math.abs(s.lead.x)<.5&&Math.abs(s.rear.x)<.5&&Math.abs(s.lead.z)<.5&&Math.abs(s.rear.z)<.5,`${spec} feet stay under the body`);
 }
 assert.equal(stanceFor('unknown-spec'),NEUTRAL_STANCE);
});

test('motion probe: fresh rigs replay identically (no randomness or hidden global state in pose code)',()=>{
 const knockdown={at:.05,until:2,landedAt:.4,pickupUntil:.9,rollAt:1,rollUntil:1.4,rollX:1,rollZ:0};
 const cases=[
  {spec:'sword',mode:'attack',from:0,to:1.6,travel:true},
  {spec:'daggers',mode:'attack',from:0,to:1.4},
  {spec:'rifle',mode:'reload',from:5,to:6.5},
  {spec:'fire',mode:'attack',from:0,to:1.3},
  {spec:'pistol',mode:'run',from:0,to:1,travel:true},
  {spec:'sword',mode:'idle',from:0,to:2,patch:(t:number)=>t>=.05?{knockdown}:{}},
 ];
 for(const c of cases)for(const hz of PROBE_RATES){
  const a=probeMotion({...c,hz,keepFrames:true}),b=probeMotion({...c,hz,keepFrames:true});
  assert.deepEqual(b,a,`${c.spec} ${c.mode} at ${hz} Hz: ${describeProbe(a)} vs ${describeProbe(b)}`);
  assert.ok(a.frames>0&&[a.maxJointStepDeg,a.minAnkleY,a.maxFootSlip,a.maxBodyStep].every(Number.isFinite),describeProbe(a));
 }
});

test('standing idle keeps every standard spec on the floor at 30, 60 and 120 Hz',()=>{
 for(const spec of standard){
  const rates=probeRates({spec,mode:'idle',from:0,to:1});
  for(const hz of PROBE_RATES)assert.ok(rates[hz].minAnkleY>=-.81,`${spec} ${describeProbe(rates[hz])}`);
 }
});
