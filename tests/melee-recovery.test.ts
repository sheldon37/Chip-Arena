import test from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {initPhysics,type PlayerView} from '../src/shared/simulation.js';
import {WEAPONS} from '../src/shared/content.js';
import {attackCommitment,attackTravel,meleeRecovery} from '../src/shared/souls-combat.js';
import {probeBase,probeRig} from './helpers/motion-probe.js';
import {animateCharacter} from '../src/client/character-animation.js';
import {MOCAP_SETS} from '../src/client/mocap/mocap-layer.js';
await initPhysics();
const bones=['body','pelvis','spine','chest','rightArm','leftArm','rightHand','leftHand','weapon','offWeapon'] as const;
test('melee recoveries stay below visible wrist-whip speeds at normal frame rates',()=>{
 for(const spec of ['sword','shield','greatsword','daggers'] as const)for(const rate of [.8,1,1.35])for(const hz of [60,120])for(let index=1;index<=MOCAP_SETS[spec]!.cuts!.length;index++){
  const r=probeRig(spec),base={...probeBase(spec),id:'recovery',grounded:true,vel:{x:0,y:0,z:0},hitStunUntil:-1e9},w=WEAPONS[spec],wind=w.windup/rate,win=.16/rate,rec=meleeRecovery(spec)/rate,at=2,end=at+wind+win+rec;
  const c=attackCommitment(spec,at,wind,win,rate,w.interval/rate),p={...base,lastAttack:at,attackIndex:index,attackRate:rate,windup:wind,swingAt:at+wind,swingUntil:at+wind+win,attackCommitment:c};
  let prev:T.Quaternion[]|undefined,max=0;
  for(let n=0;n<Math.ceil((end+.32-1.7)*hz);n++){
   const t=1.7+n/hz;r.group.position.z=t<at?0:attackTravel(c,t);animateCharacter(r,t<at?{...base,lastAttack:-10,lastBlock:1.2}:p,t,1/hz);r.group.updateMatrixWorld(true);
   const q=bones.map(b=>r[b].getWorldQuaternion(new T.Quaternion()));
   // The first 60 ms are the tail of contact's momentum. Measure the recovery and its exit, including the next idle pose.
   if(prev&&t>=at+wind+win+.06/rate)max=Math.max(max,...q.map((v,i)=>v.angleTo(prev![i])*hz/rate));prev=q;
  }
  const limit=spec==='daggers'?index===4?48:32:spec==='greatsword'?24:spec==='shield'?18:23;
  assert.ok(max<limit,`${spec} cut ${index}, ${rate}x at ${hz} Hz: ${max.toFixed(2)} rad/s > ${limit}`);
 }
});
test('a new combo cut transitions from the actual floor-corrected pose, with no first-frame wrist reset',()=>{
 for(const spec of ['sword','daggers','shield','greatsword'] as const){const r=probeRig(spec),base={...probeBase(spec),id:'combo',grounded:true,vel:{x:0,y:0,z:0},hitStunUntil:-1e9},w=WEAPONS[spec],span=w.windup+.16+meleeRecovery(spec);let prev:T.Quaternion[]|undefined,previous=0,changes=0;
  for(let n=0;n<Math.ceil((span*5+1)*120);n++){
   const t=n/120,step=t<.5?0:Math.min(5,Math.floor((t-.5)/span)+1),at=.5+(step-1)*span;
   const p:PlayerView=step?{...base,lastAttack:at,attackIndex:step,windup:w.windup,swingAt:at+w.windup,swingUntil:at+w.windup+.16,attackCommitment:attackCommitment(spec,at,w.windup,.16,1,span)}:{...base,lastAttack:-10,lastBlock:0};
   animateCharacter(r,p,t,1/120);r.group.updateMatrixWorld(true);const q=[r.weapon,r.offWeapon].map(o=>o.getWorldQuaternion(new T.Quaternion()));
   if(prev&&step!==previous&&previous>0){changes++;const delta=Math.max(...q.map((v,i)=>v.angleTo(prev![i])));assert.ok(delta<.25,`${spec} combo ${previous}→${step}: ${delta.toFixed(3)} rad first-frame reset`);}prev=q;previous=step;
  }
  assert.equal(changes,4);
 }
});
