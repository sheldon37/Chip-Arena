import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {Simulation,initPhysics,type PlayerView} from '../src/shared/simulation.js';
import {createCharacterRig} from '../src/client/character-rig.js';
import {animateCharacter} from '../src/client/character-animation.js';
await initPhysics();
const position=(node:T.Object3D)=>node.getWorldPosition(new T.Vector3());
function fixture(spec:'emperor'|'ansatsuken',rate=1,total=spec==='emperor'?2:3){
 const s=new Simulation({sandbox:true});s.addPlayer('a');const p:PlayerView={...s.snapshot().players[0],spec,attackRate:rate,grounded:true,vel:{x:0,y:0,z:0},lastAttack:-10,lastShot:-10};
 const first=spec==='emperor'?.075:.12,gap=spec==='emperor'?.17:total===10?.115:.25,end=(first+(total-1)*gap)/rate+(spec==='emperor'?.14/rate:.22);
 if(spec==='emperor')p.emperor={at:0,until:end,action:'jab',total,index:0,hits:0,lastHit:-10,comboDamage:0,electricAt:-10};
 else p.arcade={at:0,until:end,action:'combo',total,index:0,charge:0,hits:0,lastHit:-10,comboDamage:0,uppercutCd:0,hadokenCd:0,parryAt:-10,parryUntil:0};
 const sample=(time:number)=>{const r=createCharacterRig(p.color);animateCharacter(r,p,time,1/120);r.group.updateMatrixWorld(true);return r;};
 return {s,p,sample,first,gap,end};
}
test('both dev strings drive the striking shoulder, extend on authority contact, guard with the other fist and recoil',()=>{
 for(const spec of ['emperor','ansatsuken'] as const)for(const rate of [1,1.7]){
  const {s,sample,first,gap}=fixture(spec,rate);
  try{const idle=sample(0);
   for(let i=0;i<2;i++){
    const left=spec==='emperor'?i%2===0:i%2===1,hit=(first+i*gap)/rate,r=sample(hit);
    const shoulder=left?r.leftArm:r.rightArm,elbow=left?r.leftElbow:r.rightElbow,hand=left?r.leftHand:r.rightHand,other=left?r.rightHand:r.leftHand;
    const a=position(shoulder),b=position(elbow),c=position(hand),rest=position(left?idle.leftHand:idle.rightHand);
    assert.ok(c.z>rest.z+.28,spec+' must drive the fist forward');
    assert.ok(b.clone().sub(a).normalize().dot(c.clone().sub(b).normalize())>.96,'elbow extends at contact without stretching bones');
    assert.ok(a.z>position(left?idle.leftArm:idle.rightArm).z+.14,'hip and torso must carry the shoulder forward');
    assert.ok(position(other).z<c.z-.30,'opposite hand guards instead of double punching');
    const recovered=sample(hit+.135/rate);assert.ok(position(left?recovered.leftHand:recovered.rightHand).z<c.z-.20,'fist must visibly retract');
   }
  }finally{s.dispose();}
 }
});
test('donkey kick chambers, extends heel first on the final hit, rechambers and plants while the support foot stays low',()=>{
 for(const total of [3,10]){const {s,sample,first,gap,end}=fixture('ansatsuken',1,total);try{
  const hit=first+(total-1)*gap,guard=sample(0),chamber=sample(hit-Math.min(.16,gap*.68)*.43),contact=sample(hit),retract=sample(hit+.14),plant=sample(end-.001);
  const hip=position(contact.rightLeg),knee=position(contact.rightKnee),heel=position(contact.rightFoot);
  assert.ok(knee.clone().sub(hip).normalize().dot(heel.clone().sub(knee).normalize())>.97,'kicking leg extends');
  assert.ok(heel.z>position(chamber.rightFoot).z+.25,'heel thrust follows the chamber');
  assert.ok(position(retract.rightFoot).z<heel.z-.25,'leg rechambers before landing');
  assert.ok(position(plant.rightFoot).y<-.70,'kicking foot returns to the floor');
  assert.ok(position(contact.leftFoot).distanceTo(position(guard.leftFoot))<.035,'support foot stays planted through hip pivot');
  const sole=new T.Vector3(0,-1,0).applyQuaternion(contact.rightFoot.getWorldQuaternion(new T.Quaternion()));assert.ok(sole.z>.98,'heel/sole faces the target');
 }finally{s.dispose();}}
});
test('rapid strings stay finite and continuous at 30/60/120 Hz without changing combat state',()=>{
 for(const hz of [30,60,120])for(const spec of ['emperor','ansatsuken'] as const){const {s,p,end}=fixture(spec,1.7,spec==='emperor'?3:10);try{
  const original=JSON.stringify(p),r=createCharacterRig(p.color);let previous:T.Vector3|undefined;
  for(let i=0;i<Math.ceil((end+.2)*hz);i++){
   animateCharacter(r,p,i/hz,1/hz);r.group.updateMatrixWorld(true);r.group.traverse(n=>assert.ok(n.matrixWorld.elements.every(Number.isFinite)));
   const hand=position(r.rightHand);if(previous)assert.ok(hand.distanceTo(previous)<.75,'no detached or teleported fist');previous=hand;
   assert.equal(r.rightKnee.position.length(),.25);assert.equal(r.rightElbow.position.length(),.27);
  }
  assert.equal(JSON.stringify(p),original,'animation is presentation only');
 }finally{s.dispose();}}
});
