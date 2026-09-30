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
import {KnockdownRagdolls} from '../src/client/knockdown-ragdolls.js';
import {KILL_EFFECTS} from '../src/client/death-effects.js';

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

const torsoHit=(at:number,until:number,extra:Record<string,unknown>={})=>({kind:'hit' as const,at,until,yaw:0,direction:{x:0,y:0,z:-1},point:{x:.12,y:.1,z:.3},zone:'torso' as const,turn:.35,strength:.85,...extra});

test('hit reactions start fast, follow through past neutral, settle by the stun end and keep blades from flipping up',()=>{
 for(const [spec,rise] of [['sword',.65],['greatsword',.4],['daggers',.6]] as const){
  const base=probeBase(spec),r=probeRig(spec),h=torsoHit(T0,T0+.34),tips:number[]=[],lean:number[]=[];let rest=0;
  for(let t=T0-.5;t<T0+.34;t+=1/120){const p={...animationPreviewPlayer(base,'idle',t),...(t>=T0?{hitReaction:h,hitStunUntil:h.until}:{hitStunUntil:-1e9})} as PlayerView;animateCharacter(r,p,t,1/120);r.group.updateMatrixWorld(true);
   if(t<T0)rest=weaponTip(r,p).y;else{tips.push(weaponTip(r,p).y);lean.push(r.body.rotation.x);}}
  // Baseline: the stunned arms slerped to a canned splay and the sword tip rose about 1.05 m within 80 ms.
  assert.ok(Math.max(...tips)-rest<rise,`${spec} blade tip rises ${(Math.max(...tips)-rest).toFixed(2)} m`);
  const peak=Math.min(...lean),early=lean[2];assert.ok(early<.3*peak,`${spec}: the impact moves the torso at once (${early.toFixed(3)} vs peak ${peak.toFixed(3)})`);
  assert.ok(Math.max(...lean.slice(lean.indexOf(peak)))>.02,`${spec}: the torso follows through past neutral`);assert.ok(Math.abs(lean[lean.length-1])<.01,`${spec} settles`);
  const probe=probeRates({spec,mode:'idle',from:T0-.1,to:T0+.5,patch:t=>t>=T0?{hitReaction:h,hitStunUntil:h.until}:{}},[60])[60];assert.ok(probe.maxJointStepDeg<=20,describeProbe(probe));
 }
});

test('a greatsword parry keeps both hands on the grip; a stun without a hitReaction uses the same smooth impulse',()=>{
 const base=probeBase('greatsword'),h={...torsoHit(T0,T0+.5),kind:'parry' as const,turn:-.35,strength:1},r=probeRig('greatsword');
 for(const f of [.25,.55]){const p={...animationPreviewPlayer(base,'idle',T0+.5*f),hitReaction:h,hitStunUntil:h.until} as PlayerView;animateCharacter(r,p,T0+.5*f,1/60);r.group.updateMatrixWorld(true);
  assert.ok(r.weapon.getObjectByName('support-grip')!.getWorldPosition(new T.Vector3()).distanceTo(r.leftHand.getWorldPosition(new T.Vector3()))<.05,'two-hand parry at '+f);}
 const flinch=probeRates({spec:'sword',mode:'idle',from:T0-.1,to:T0+.5,patch:t=>t>=T0?{hitStunUntil:T0+.3}:{}},[60,120]);
 assert.ok(flinch[60].maxJointStepDeg<=20,describeProbe(flinch[60]));assert.ok(flinch[120].maxJointStepDeg<=flinch[60].maxJointStepDeg*.65,describeProbe(flinch[120]));
 const s=probeRig('sword'),sb=probeBase('sword');let seen=0;for(let t=T0-.2;t<T0+.3;t+=1/60){animateCharacter(s,{...animationPreviewPlayer(sb,'idle',t),hitStunUntil:t>=T0?T0+.3:-1e9} as PlayerView,t,1/60);if(t>=T0+.08&&t<T0+.12)seen=Math.max(seen,Math.abs(s.body.rotation.x));}
 assert.ok(seen>.08,'the fallback flinch visibly recoils');
});

test('juggle launches out of the caught pose, kicks without resetting, spins a whole turn from 0 and never snaps',()=>{
 const J=(t:number)=>{const r=t-T0,second=r>=.45;return {at:T0,until:T0+1.2,hits:second?2:1,lastHit:T0+(second?.45:0),source:'x',spinUntil:second?T0+.45+.65:0,bounced:false,wallUsed:false};};
 const rates=probeRates({spec:'sword',mode:'idle',from:T0-.1,to:T0+1.5,patch:t=>t>=T0?{juggle:J(t),grounded:false,vel:{x:0,y:6*(1.2-2*(t-T0)),z:0}}:{}});
 // Baseline: a 155 degree entry pop, a 27 degree jolt on every hit and a 44 degree exit snap.
 assert.ok(rates[60].maxJointStepDeg<=22,describeProbe(rates[60]));assert.ok(rates[120].maxJointStepDeg<=rates[60].maxJointStepDeg*.65,describeProbe(rates[120]));
 assert.ok([rates[30],rates[60],rates[120]].every(x=>Number.isFinite(x.minAnkleY)&&Number.isFinite(x.minTipY)),'finite poses');
 const r=probeRig('sword'),base=probeBase('sword'),yaw=(t:number)=>{animateCharacter(r,{...animationPreviewPlayer(base,'idle',t),hitStunUntil:-1e9,juggle:J(t),grounded:false} as PlayerView,t,1/60);return new T.Euler().setFromQuaternion(r.body.quaternion,'YXZ').y;};
 for(let t=T0;t<T0+.45;t+=1/60)yaw(t);const before=yaw(T0+.45);assert.ok(Math.abs(before-yaw(T0+.45+1/60))<.2,'the screw spin starts from the current heading');
});

test('a knockdown ragdoll hands its final limb pose back to the rig at the recovery roll, then blends into the roll',()=>{
 const s=new Simulation({sandbox:true});s.addPlayer('a','A','melee');s.addPlayer('b','B');s.phase='fight';
 try{
  const state=s.snapshot(),p=state.players[1],r=createCharacterRig(p.color),scene=new T.Scene(),fx=new KnockdownRagdolls(scene),rigs=new Map([[p.id,r]]),at=state.time;scene.add(r.group);
  p.knockdown={at,until:at+2,ragdoll:true,landedAt:at+.5,pickupUntil:at+.5,pickupUsed:true,rollAt:at+.9,rollUntil:at+1.3,rollX:0,rollZ:-1};p.vel={x:0,y:0,z:-4};p.hitStunUntil=0;
  const step=(t:number)=>{state.time=t;r.group.visible=true;animateCharacter(r,p,t,1/60);fx.update(state,rigs);r.group.updateMatrixWorld(true);};
  for(let t=at;t<at+.9-1e-9;t+=1/60)step(t);
  const ragdoll=scene.getObjectByName('knockdown-ragdoll')!,parts=['left-shoulder','right-knee','head'].map(n=>{const part=ragdoll.getObjectByName(n)!,q=part.getWorldQuaternion(new T.Quaternion());return part.userData.jointOffset?q.multiply(part.userData.jointOffset):q;});
  step(at+.9);assert.equal(scene.getObjectByName('knockdown-ragdoll'),undefined,'the ragdoll yields at rollAt');
  [r.leftArm,r.rightKnee,r.head].forEach((bone,i)=>assert.ok(bone.getWorldQuaternion(new T.Quaternion()).angleTo(parts[i])<.02,`${bone.name} shows the ragdoll's final pose on the handoff frame`));
  const handed=TRANSITION_BONES.map(b=>r[b].getWorldQuaternion(new T.Quaternion()));step(at+.9+1/60);
  const moved=Math.max(...TRANSITION_BONES.map((b,i)=>r[b].getWorldQuaternion(new T.Quaternion()).angleTo(handed[i])));assert.ok(moved<.35,`the rig blends from the ragdoll pose (max ${moved.toFixed(3)} rad in one frame)`);
  fx.clear();
 }finally{s.dispose();}
});

test('kill cinematic victims are the live model and start from the pose the fighter died in, rewindably',()=>{
 const joints=['motion-root','spine','head','left-shoulder','right-elbow','left-hip','right-knee'];
 for(const effect of ['launch','shrink','ascension','ghost','confetti','dev-clown','dev-clown-love','dev-bass-van','dev-raging-demon']){
  const base=probeBase('sword'),live=probeRig('sword',true);for(let t=0;t<.6;t+=1/60)animateCharacter(live,{...animationPreviewPlayer(base,'run',t),hitStunUntil:-1e9} as PlayerView,t,1/60);live.group.updateMatrixWorld(true);
  const p={...base,hp:0} as PlayerView,death={id:'x',at:0,effect,seed:7,pos:{...base.pos},velocity:{x:0,y:0,z:0},direction:{x:0,y:.1,z:-1},source:'k',kind:'direct',yaw:0,melee:true,force:300,attacker:{pos:{x:0,y:base.pos.y,z:2},yaw:Math.PI,color:'#3af',cosmetics:base.cosmetics!,spec:'sword'}} as unknown as NonNullable<PlayerView['death']>;
  const scene=new T.Scene(),fx=KILL_EFFECTS[effect](scene,live.group,{...p,death},death,1,100);let victim:T.Object3D|undefined;
  scene.traverse(o=>{if(!victim&&o!==live.group&&(o.name==='love-partner'||o.name==='bass-passenger'||o.name==='blank-mannequin-v2'&&o.userData.armorGlow!==undefined&&o.getObjectByName('motion-root')))victim=o;});
  assert.ok(victim,effect);assert.ok(victim!.getObjectByName('armor-back-light'),`${effect}: the victim keeps the live rig's armor glow model`);
  const pose=()=>joints.map(n=>victim!.getObjectByName(n)!.quaternion.clone());
  fx.update(0);pose().forEach((q,i)=>assert.ok(q.angleTo(live.group.getObjectByName(joints[i])!.quaternion)<1e-6,`${effect}: ${joints[i]} starts in the death pose`));
  fx.update(1.3);const late=pose();fx.update(.1);fx.update(1.3);pose().forEach((q,i)=>assert.ok(q.angleTo(late[i])<1e-6,`${effect}: rewindable ${joints[i]}`));
  fx.dispose();
 }
});

test('death ragdolls: hinged elbows, knees and neck never hyperextend, the fling stays comedic, bodies sink instead of vanishing',()=>{
 const travel:number[]=[];
 for(const spec of ['sword','rifle'])for(const seed of [1,424242,99]){
  const base=probeBase(spec),rig=probeRig(spec,true);for(let t=0;t<.5;t+=1/60)animateCharacter(rig,{...animationPreviewPlayer(base,'run',t),hitStunUntil:-1e9} as PlayerView,t,1/60);rig.group.position.set(base.pos.x,base.pos.y,base.pos.z);rig.group.updateMatrixWorld(true);
  const death={id:'d',at:0,effect:'ragdoll',seed,pos:{...base.pos},velocity:{x:0,y:0,z:0},direction:{x:0,y:.1,z:-1},source:'k',kind:'direct',yaw:0,melee:true,force:300,hitPoint:{x:base.pos.x,y:base.pos.y+.2,z:base.pos.z+.3}} as unknown as NonNullable<PlayerView['death']>;
  const scene=new T.Scene(),fx=KILL_EFFECTS.ragdoll(scene,rig.group,{...base,hp:0,death} as PlayerView,death,1,100),group=scene.getObjectByName('ragdoll')!;
  const bend=(parent:string,child:string)=>{const a=group.getObjectByName(parent)!.quaternion,b=group.getObjectByName(child)!.quaternion,rel=a.clone().invert().multiply(b);return {twist:2*Math.atan2(rel.x,rel.w),swing:Math.hypot(rel.y,rel.z)};};
  for(let age=.1;age<=3;age+=.1){fx.update(age);
   for(const [parent,child,lo,hi] of [['left-shoulder','left-elbow',-2.65,.1],['right-shoulder','right-elbow',-2.65,.1],['left-hip','left-knee',-.1,2.65],['right-hip','right-knee',-.1,2.65]] as const){const b=bend(parent,child);assert.ok(b.twist>=lo&&b.twist<=hi&&b.swing<.05,`${spec} seed ${seed} ${child} at ${age.toFixed(1)}: ${b.twist.toFixed(2)} (swing ${b.swing.toFixed(3)})`);}}
  const pelvis=group.getObjectByName('pelvis')!;travel.push(Math.hypot(pelvis.position.x-base.pos.x,pelvis.position.z-base.pos.z));
  fx.update(4.95);group.children.forEach(part=>assert.ok(part.scale.x>=.85-1e-9,'parts shrink at most to .85'));fx.dispose();
 }
 // Owner direction: keep the comedic long melee fling (it was about 9.6 m on average before the hinge/collider work).
 assert.ok(travel.reduce((a,b)=>a+b,0)/travel.length>8,`mean melee fling ${(travel.reduce((a,b)=>a+b,0)/travel.length).toFixed(2)} m`);
});
