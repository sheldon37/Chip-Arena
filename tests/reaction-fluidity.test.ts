import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {Simulation,initPhysics,type PlayerView} from '../src/shared/simulation.js';
import {v,idleInput} from '../src/shared/config.js';
import {createCharacterRig,type CharacterRig} from '../src/client/character-rig.js';
import {animateCharacter,animationPreviewPlayer} from '../src/client/character-animation.js';
import {finishPose,seedTransition,transitionAction,transitionDuration,TRANSITION_BONES} from '../src/client/pose-transitions.js';
import {knockdownPhase} from '../src/client/knockdown-motion.js';
import {probeBase,probeRig,probeRates,describeProbe,weaponTip} from './helpers/motion-probe.js';

await initPhysics();
const quats=(r:CharacterRig)=>TRANSITION_BONES.map(b=>r[b].quaternion.clone());
const maxAngle=(a:T.Quaternion[],b:T.Quaternion[])=>Math.max(...a.map((q,i)=>q.angleTo(b[i])));
const pose=(r:CharacterRig,seed:number)=>{TRANSITION_BONES.forEach((b,i)=>r[b].quaternion.setFromEuler(new T.Euler(Math.sin(seed+i)*.8,Math.cos(seed*2+i)*.6,Math.sin(seed*3+i*.7)*.5)));r.body.position.set(Math.sin(seed)*.1,-.2+Math.cos(seed)*.1,0);};
const view=(spec='sword',extra:Partial<PlayerView>={}):PlayerView=>({...animationPreviewPlayer(probeBase(spec),'idle',1),hitStunUntil:0,...extra});

test('transition keys ignore grounded and classify juggle and knockdown by their active windows',()=>{
 const r=probeRig('sword');pose(r,1);finishPose(r,view('sword',{grounded:false}),1);pose(r,2);const landed=quats(r);finishPose(r,view('sword',{grounded:true}),1+1/60);
 assert.ok(maxAngle(quats(r),landed)<1e-6,'a landing is not a transition key: locomotion owns the landing');
 const juggle={at:1,until:2,hits:1,lastHit:1,source:'x',spinUntil:0,bounced:false,wallUsed:false};
 assert.equal(transitionAction(view('sword',{juggle}),1.5),'juggle');assert.equal(transitionAction(view('sword',{juggle}),2.01),'base','a juggle field past until is not an active juggle');
 const k={at:1,until:3,landedAt:1.5,pickupUntil:1.72,rollAt:1.9,rollUntil:2.3,rollX:0,rollZ:-1};
 const phases=[[1.2,'air'],[1.55,'legs-up'],[1.8,'flat'],[2,'roll'],[2.5,'rise']] as const;
 for(const [t,phase] of phases){assert.equal(knockdownPhase(k,t),phase);assert.equal(transitionAction(view('sword',{knockdown:k}),t),'down:'+phase);}
 assert.equal(knockdownPhase({...k,pickupUsed:true},1.55),'land');assert.equal(transitionAction(view('sword',{knockdown:k}),3.01),'base');
});

test('knockdown and juggle exits blend instead of snapping, and the rig never blends between two players',()=>{
 const r=probeRig('sword'),juggle={at:1,until:1.5,hits:1,lastHit:1,source:'x',spinUntil:0,bounced:false,wallUsed:false};let before:T.Quaternion[]=[];
 for(let t=1;t<1.6;t+=1/60){const p=view('sword',{juggle});animateCharacter(r,p,t,1/60);if(t<1.5-1e-9)before=quats(r);else break;}
 const fresh=probeRig('sword');animateCharacter(fresh,view('sword',{juggle}),1.5,1/60);
 assert.ok(maxAngle(quats(fresh),before)>.5,'juggle and stance poses differ a lot');assert.ok(maxAngle(quats(r),before)<.12,'the juggle exit starts from the juggle pose');
 const other=probeRig('sword');animateCharacter(other,view('sword',{juggle,id:'first'}),1.2,1/60);animateCharacter(other,view('sword',{id:'second'}),1.2+1/60,1/60);
 const stand=probeRig('sword');animateCharacter(stand,view('sword',{id:'second'}),1.2+1/60,1/60);assert.ok(maxAngle(quats(other),quats(stand))<1e-6,'a rig reused for another player starts fresh');
});

test('attack entries always decay before the contact frame, even at high attack rates',()=>{
 for(const spec of ['daggers','sword','greatsword','shield'])for(const rate of [1,2.5]){
  const base=probeBase(spec),windup=base.windup/rate,lastAttack=2,attack={...base,attackRate:rate,attackIndex:1,lastAttack,swingAt:lastAttack+windup,swingUntil:lastAttack+windup+.16/rate,hitStunUntil:0,animationTime:undefined};
  const r=probeRig(spec);for(let t=1;t<lastAttack;t+=1/120)animateCharacter(r,{...attack,lastAttack:-10,swingAt:undefined,swingUntil:undefined},t,1/120);
  for(let t=lastAttack;t<attack.swingAt;t+=1/120)animateCharacter(r,attack,t,1/120);animateCharacter(r,attack,attack.swingAt,1/120);
  const d=transitionDuration(attack,lastAttack,'attack-1','base');assert.ok(d>0&&d<=Math.min(.09,.55*windup)+1e-9&&lastAttack+d<=attack.swingAt,`${spec} x${rate} entry ${d}`);
  const fresh=probeRig(spec);animateCharacter(fresh,attack,attack.swingAt,1/120);assert.ok(maxAngle(quats(r),quats(fresh))<1e-6,`${spec} x${rate}: no entry offset remains at swingAt`);
 }
});

test('the flat knockdown is exact at landedAt+.25, even on a rig that just showed another fighter',()=>{
 const s=new Simulation({sandbox:true}),a=s.addPlayer('a','Fighter','melee',null,'#ff3355',{},true)!,b=s.addPlayer('b','Rival')!;a.spec='ansatsuken';s.resetRound();s.phase='fight';
 for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});a.body.setTranslation(v(0,1,0),true);b.body.setTranslation(v(0,1,1.5),true);a.input=idleInput();b.hp=b.stats.hp=1000;s.world.step();
 try{
  const r=createCharacterRig('#ff3355');s.arcade.start(a,'shoryuken');s.time+=.4;animateCharacter(r,s.snapshot().players[0],s.time,1/60);assert.ok(r.body.rotation.y>1);assert.ok(r.rightArm.rotation.x<-2.9);
  s.arcade.knockDown(b);b.knockdown!.landedAt=s.time;b.knockdown!.until=s.time+.9;animateCharacter(r,s.snapshot().players[1],s.time+.25,1/60);assert.equal(r.body.rotation.x,-Math.PI/2);
  // The same player's own continuous timeline, with and without the legs-up pickup window.
  for(const pickup of [true,false]){const k={at:4,until:5.4,landedAt:4.4,pickupUntil:pickup?4.62:4.4,pickupUsed:!pickup,rollAt:4.8,rollUntil:5.2,rollX:0,rollZ:-1},rig=probeRig('sword');
   for(let t=3.9;t<=4.65+1e-9;t+=1/60)animateCharacter(rig,view('sword',{knockdown:t>=4?k:undefined,grounded:t<4||t>=4.4}),t,1/60);
   animateCharacter(rig,view('sword',{knockdown:k}),4.4+.25,1/60);assert.equal(rig.body.rotation.x,-Math.PI/2,'pickup '+pickup);}
 }finally{s.dispose();}
});

test('seedTransition hands an external pose to the rig and decays it over the given duration',()=>{
 const r=probeRig('sword'),p=view('sword');for(let t=1;t<1.2;t+=1/60)animateCharacter(r,p,t,1/60);
 const external=probeRig('sword');pose(external,5);const seeded=quats(external);seedTransition(r,seeded,external.body.position,.16);
 animateCharacter(r,p,1.2,1/60);assert.ok(maxAngle(quats(r),seeded)<1e-6,'the first frame after seeding shows the seeded pose');assert.ok(r.body.position.distanceTo(external.body.position)<1e-9);
 const reference=probeRig('sword');for(let t=1;t<1.2+.17;t+=1/60)animateCharacter(reference,p,t,1/60);
 for(let t=1.2+1/60;t<1.2+.17;t+=1/60)animateCharacter(r,p,t,1/60);assert.ok(maxAngle(quats(r),quats(reference))<1e-6,'the seeded offset is gone after its duration');
 const byName=probeRig('sword');animateCharacter(byName,p,1,1/60);seedTransition(byName,{head:new T.Quaternion().setFromEuler(new T.Euler(.7,0,0))},undefined,.1);animateCharacter(byName,p,1+1/60,1/60);
 assert.ok(Math.abs(byName.head.quaternion.angleTo(new T.Quaternion().setFromEuler(new T.Euler(.7,0,0))))<1e-6,'a partial map seeds only the named joints');
});

// The audit's scripted knockdown: launched at T0, lands at +.55 with a .22 s pickup window, rolls .95-1.35, stands at 1.55.
const T0=3,L=.55;
const knockdown=(r:number,rollX=0,rollZ=-1)=>({at:T0,landedAt:r>=L?T0+L:undefined,pickupUntil:r>=L?T0+L+.22:undefined,rollAt:r>=L?T0+L+.4:undefined,rollUntil:r>=L?T0+L+.8:undefined,until:r>=L?T0+L+1:T0+5,rollX,rollZ});
const knockdownPatch=(rollX=0,rollZ=-1)=>(t:number):Partial<PlayerView>=>{const r=t-T0;return r<0?{}:{knockdown:knockdown(r,rollX,rollZ),grounded:r>=L,vel:{x:0,y:r<L?15.9*(L-2*r):0,z:0}};};

test('knockdown fall, landing, roll-to-crouch and rise are continuous at 30, 60 and 120 Hz (no pops or teleports)',()=>{
 for(const [rollX,rollZ] of [[0,-1],[1,0]]){
  const rates=probeRates({spec:'sword',mode:'idle',from:T0-.1,to:T0+1.8,patch:knockdownPatch(rollX,rollZ)}),at=`roll (${rollX},${rollZ})`;
  // Baseline: a .64 m body teleport at landing and 39-180 degree single-frame flips. The fall and landing move the body
  // at most .06 m per 60 Hz frame; the fast roll itself (the hips pass over the head in about .15 s) stays under .08.
  const landing=probeRates({spec:'sword',mode:'idle',from:T0-.1,to:T0+L+.4,patch:knockdownPatch(rollX,rollZ)},[60])[60];
  assert.ok(landing.maxBodyStep<=.06,`${at}: ${describeProbe(landing)}`);assert.ok(rates[60].maxBodyStep<=.08,`${at}: ${describeProbe(rates[60])}`);
  assert.ok(rates[120].maxJointStepDeg<=20,`${at}: ${describeProbe(rates[120])}`);
  assert.ok(rates[120].maxJointStepDeg<=rates[60].maxJointStepDeg*.65&&rates[60].maxJointStepDeg<=rates[30].maxJointStepDeg*.65,`${at}: steps shrink with the frame time, so nothing snaps: ${describeProbe(rates[30])} / ${describeProbe(rates[60])} / ${describeProbe(rates[120])}`);
 }
 const juggleFree=probeRates({spec:'sword',mode:'idle',from:T0-.1,to:T0+1.8,patch:knockdownPatch()},[60])[60];assert.ok(juggleFree.maxJointStepDeg<=32,describeProbe(juggleFree));
});

test('knockdown poses: flat on the floor, the roll keeps limbs and blade above the floor and opens into a planted crouch',()=>{
 for(const spec of ['sword','daggers','greatsword','shield']){
  const r=probeRig(spec),base=probeBase(spec),patch=knockdownPatch();let crouch=false;
  for(let t=T0-.5;t<=T0+1.6;t+=1/60){const p={...animationPreviewPlayer(base,'idle',t),hitStunUntil:-1e9,...patch(t)} as PlayerView;r.group.rotation.y=p.yaw;animateCharacter(r,p,t,1/60);r.group.updateMatrixWorld(true);const rel=t-T0;
   if(rel>=L+.25&&rel<L+.32){assert.equal(r.body.rotation.x,-Math.PI/2);assert.ok(Math.abs(r.body.position.y+.64)<1e-9,'lying on the floor');}
   if(rel>=L+.12){for(const n of [r.leftFoot,r.rightFoot,r.leftHand,r.rightHand])assert.ok(n.getWorldPosition(new T.Vector3()).y>=-.81,`${spec} ${n.name} at +${rel.toFixed(3)}`);
    assert.ok(weaponTip(r,p).y>=-.84,`${spec} blade tip at +${rel.toFixed(3)}: ${weaponTip(r,p).y.toFixed(3)}`);}
   if(!crouch&&rel>=L+.8-1e-9){crouch=true;assert.ok(Math.abs(r.leftFoot.getWorldPosition(new T.Vector3()).y+.795)<.01,`${spec} lead foot planted`);
    assert.ok(r.body.quaternion.angleTo(new T.Quaternion().setFromEuler(new T.Euler(.3,0,0)))<.15,`${spec} the roll ends upright in a crouch`);assert.ok(r.body.position.y<-.25,'crouched');}
  }
 }
});
