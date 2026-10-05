// Captured melee sets for sword, daggers and shield (the owner's Explosive RPG Character pack, converted offline by
// scripts/mocap/convert-explosive.mjs; presentation only). The greatsword's set (the same pack's two-handed sword) has its
// own suite, tests/greatsword-mocap; the footwork and weapon-continuity tests here cover all four.
import test from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {WEAPONS} from '../src/shared/content.js';
import {meleeRecovery,attackCommitment,attackTravel} from '../src/shared/souls-combat.js';
import {initPhysics,type PlayerView} from '../src/shared/simulation.js';
import type {SpecId} from '../src/shared/config.js';
import {animateCharacter,animationPreviewPlayer} from '../src/client/character-animation.js';
import * as bladeClearance from '../src/client/mocap/blade-clearance.js';
import {probeBase,probeRig} from './helpers/motion-probe.js';
import {CH,CHANNEL_COUNT,decodeClip,sampleClip,clipDuration,type MocapClip} from '../src/client/mocap/mocap-clip.js';
import {MOCAP_SETS,cutClip,cutTime} from '../src/client/mocap/mocap-layer.js';
import {SWORD_CLIPS} from '../src/client/mocap/sword-clips.js';
import {DAGGERS_CLIPS} from '../src/client/mocap/daggers-clips.js';
import {SHIELD_CLIPS} from '../src/client/mocap/shield-clips.js';

await initPhysics();
const TABLES:Record<'sword'|'daggers'|'shield',Record<string,MocapClip>>={sword:SWORD_CLIPS,daggers:DAGGERS_CLIPS,shield:SHIELD_CLIPS};
const SPECS=Object.keys(TABLES) as ('sword'|'daggers'|'shield')[],CUTS:Record<string,number>={sword:3,daggers:4,shield:2,greatsword:2},window=.16;
const ALL=[...SPECS,'greatsword'] as const;

test('sword, dagger and shield clip tables decode to finite rows with ascending marks; every set is complete and one-handed', () => {
 for(const spec of SPECS){
  for(const [name,clip] of Object.entries(TABLES[spec])){
   const d=decodeClip(clip);assert.equal(d.length,clip.frames*CHANNEL_COUNT,`${spec} ${name}`);for(const x of d)assert.ok(Number.isFinite(x),`${spec} ${name}`);
   if(clip.marks)for(let i=1;i<clip.marks.length;i++)assert.ok(clip.marks[i]>clip.marks[i-1]&&clip.marks[i]<=clipDuration(clip)+1e-9,`${spec} ${name} marks ascend inside the clip`);
  }
  const set=MOCAP_SETS[spec]!;for(const k of ['idle','walk','run','block','blockHit'] as const)assert.ok(set[k],`${spec} ${k}`);
  assert.equal(set.cuts!.length,CUTS[spec]);assert.equal(set.fit?.oneHand,true);
  // Every cut starts on the guard frame; its windup pace keys ascend strictly inside (start, contact start).
  for(const c of set.cuts!){assert.equal(c.marks!.length,4);assert.equal(c.marks![0],0,`${spec} ${c.source} starts on the guard frame`);const keys=[c.marks![0],...c.pace!,c.marks![1]];for(let i=1;i<keys.length;i++)assert.ok(keys[i]>keys[i-1],`${spec} ${c.source} pace ascends`);}
 }
 assert.ok(MOCAP_SETS.sword!.stagger&&MOCAP_SETS.daggers!.stagger);assert.equal(MOCAP_SETS.shield!.fit!.clear,'none');
});

test('captured poses stay inside joint limits, with unit, perpendicular blade axes', () => {
 const row=new Float32Array(CHANNEL_COUNT);
 for(const spec of SPECS)for(const [name,clip] of Object.entries(TABLES[spec]))for(let t=0;t<=clipDuration(clip);t+=1/60){
  sampleClip(clip,t,row);const at=`${spec} ${name} at ${t.toFixed(2)}`;
  for(const k of ['lknee','rknee'] as const)assert.ok(row[CH[k]]>-.15&&row[CH[k]]<2.7,`${at} ${k} ${row[CH[k]]}`);
  for(const j of ['pelvis','spine','chest','neck','head','lclav','rclav','lhip','rhip','lankle','rankle']){
   const i=CH[(j+'.x') as keyof typeof CH],a=Math.hypot(row[i],row[i+1],row[i+2]);
   const limit=j==='neck'||j==='head'||j.endsWith('clav')?1.3:j.endsWith('ankle')?1.4:j==='pelvis'?1.6:j.endsWith('hip')?2.4:1.2;
   assert.ok(a<limit,`${at} ${j} ${a.toFixed(2)} rad`);
  }
  const g=Math.hypot(row[CH['grip.x']]+.345,row[CH['grip.y']]-.18,row[CH['grip.z']]);assert.ok(g<.7,`${at} grip reach ${g.toFixed(2)}`);
  const b=[row[CH['blade.x']],row[CH['blade.y']],row[CH['blade.z']]],e=[row[CH['edge.x']],row[CH['edge.y']],row[CH['edge.z']]];
  assert.ok(Math.abs(Math.hypot(...b)-1)<.06&&Math.abs(Math.hypot(...e)-1)<.06,`${at} unit blade/edge`);assert.ok(Math.abs(b[0]*e[0]+b[1]*e[1]+b[2]*e[2])<.06,`${at} perpendicular`);
  assert.equal(row[CH.twohand],0,`${at}: one-handed`);
 }
});

/** A combat-ready rig in its captured guard, attacking at t=2 (attack index, rate), stepped at `hz`; the root rides the
 * authoritative step travel as the renderer places it. */
function cut(spec:SpecId,index:number,rate:number,hz:number,each:(r:ReturnType<typeof probeRig>,t:number,a:{windup:number;win:number})=>void){
 const w=WEAPONS[spec],base={...probeBase(spec),id:'mocap',grounded:true,vel:{x:0,y:0,z:0},hitStunUntil:-1e9,animationTime:undefined} as PlayerView,windup=w.windup/rate,win=window/rate;
 const c=attackCommitment(spec,2,windup,win,rate,w.interval/rate);
 const attack={...base,attackIndex:index,attackRate:rate,lastAttack:2,windup,swingAt:2+windup,swingUntil:2+windup+win,attackCommitment:c} as PlayerView,idle={...base,lastAttack:-10,lastBlock:1.2} as PlayerView;
 const r=probeRig(spec);
 for(let i=0;;i++){const t=1.7+i/hz;if(t>c.until+.3)break;r.group.position.set(0,0,t<2?0:attackTravel(c,t));animateCharacter(r,t<2?idle:attack,t,1/hz);r.group.updateMatrixWorld(true);each(r,t,{windup,win});}
}
/** The striking weapon's tip heading (0 = forward) in the rig frame: daggers strike with the left blade on even cuts. */
const _o=new T.Vector3(),_tip=new T.Vector3();
function tipHeading(r:ReturnType<typeof probeRig>,spec:SpecId,index:number){
 const w=spec==='daggers'&&index%2===0?r.offWeapon:r.weapon;(w.getObjectByName('blade-tip')??w).getWorldPosition(_tip);r.group.getWorldPosition(_o);
 return Math.atan2(_tip.x-_o.x,_tip.z-_o.z);
}
test('every captured cut strikes inside the server arc through the hit window; sword fore- and backhands sweep the server way', () => {
 for(const spec of SPECS)for(const rate of [.8,1,1.35])for(let index=1;index<=CUTS[spec];index++){
  const w=WEAPONS[spec],set=MOCAP_SETS[spec]!,clip=cutClip(set,index),m=clip.marks!,windup=w.windup/rate,win=window/rate,rec=meleeRecovery(spec)/rate;
  assert.ok(Math.abs(cutTime(clip,windup,windup,win,rec)-m[1])<1e-9&&Math.abs(cutTime(clip,windup+win,windup,win,rec)-m[2])<1e-9,'contact keys on the hit window');
  let prev=-Infinity;for(let t=0;t<=windup+win+rec;t+=.005){const c=cutTime(clip,t,windup,win,rec);assert.ok(c>=prev-1e-9,'clip time never runs backward');prev=c;}
  const arc=(spec==='sword'&&index%3===0?.8:w.arc)/2,heads:number[]=[];
  cut(spec,index,rate,120,(r,t,a)=>{const age=t-2;if(age>=a.windup+a.win*.25&&age<=a.windup+a.win*.75)heads.push(tipHeading(r,spec,index));});
  const mid=heads[Math.floor(heads.length/2)];assert.ok(Math.abs(mid)<arc+.35,`${spec} ${index} @${rate}x: strikes at ${(mid*180/Math.PI).toFixed(0)}° (arc ±${(arc*180/Math.PI).toFixed(0)}°)`);
  // Server sweep sign (simulation.ts melee): sword cuts 1 (+, right to left) and 2 (-).
  if(spec==='sword'&&index<3){const sweep=heads[heads.length-1]-heads[0];assert.ok(Math.sign(sweep)===(index===1?1:-1)&&Math.abs(sweep)>.3,`sword ${index} sweeps ${sweep.toFixed(2)} rad`);}
 }
});

const LIMBS=['leftElbow','rightElbow','leftKnee','rightKnee','leftArm','rightArm','leftLeg','rightLeg'] as const;
test('captured cuts keep every limb continuous (no single-frame flips) at 60 and 120 Hz', () => {
 for(const spec of SPECS)for(const rate of [.7,1,1.5])for(let index=1;index<=CUTS[spec];index++){
  const worst=[60,120].map(hz=>{let prev:T.Quaternion[]|undefined,step=0;cut(spec,index,rate,hz,(r,t)=>{const q=LIMBS.map(b=>r[b].getWorldQuaternion(new T.Quaternion()));if(prev&&t>=1.95)q.forEach((x,k)=>{step=Math.max(step,x.angleTo(prev![k]));});prev=q;});return step;});
  assert.ok(worst[1]<=worst[0]*.75+.01,`${spec} cut ${index} @${rate}x: ${worst[0].toFixed(3)} rad/frame at 60 Hz vs ${worst[1].toFixed(3)} at 120 Hz (a snap)`);
  // The speed cap is melee-fluidity's at the base rate; a faster attack rate plays the same motion faster.
  assert.ok(worst[1]*120<110*Math.max(1,rate),`${spec} cut ${index} @${rate}x: ${(worst[1]*120).toFixed(0)} rad/s`);
 }
});

test('captured cuts stand on planted feet: a foot on the floor holds its spot under the authoritative step travel', () => {
 // melee-fluidity's criterion for the procedural phrases (its suite runs with the captured sets off), 25 mm instead of 20:
 // the captured hips crouch deeper than the phrases', so a reach-limited step lands up to ~2 cm from its mark.
 for(const spec of ALL)for(const rate of [.8,1,1.35])for(let index=1;index<=CUTS[spec];index++){const down:(T.Vector3|undefined)[]=[undefined,undefined],slip=[0,0];
  cut(spec,index,rate,120,(r,t)=>{if(t<1.98)return;[r.leftFoot,r.rightFoot].forEach((f,k)=>{const a=f.getWorldPosition(new T.Vector3());if(a.y<-.785){if(!down[k])down[k]=a.clone();slip[k]=Math.max(slip[k],Math.hypot(a.x-down[k]!.x,a.z-down[k]!.z));}else down[k]=undefined;});});
  assert.ok(Math.max(...slip)<.025,`${spec} ${index} @${rate}x: planted slip ${slip.map(x=>(x*1000).toFixed(1)).join('/')} mm`);
 }
});

test('the weapon never jumps or flips its roll through a captured cut (sampled at 960 Hz)', () => {
 // The whole weapon orientation, roll included (the wrist carries it). The captured strikes whip a blade at a few hundred
 // rad/s for a few milliseconds (a single frame at 120 Hz), so the check is for discontinuities, sampled at 960 Hz: a snap
 // or a 180° roll flip keeps its whole size (1.5-3 rad) at any rate. The fastest continuous motion is the daggers'
 // reverse-grip blade turned up out of the floor in a crouched 60-90 ms windup, up to ~.95 rad in a millisecond.
 for(const spec of ALL)for(const rate of [.7,1,1.5])for(let index=1;index<=CUTS[spec];index++)for(const off of spec==='daggers'?[false,true]:[false]){
  let prev:T.Quaternion|undefined,step=0,at=0;cut(spec,index,rate,960,(r,t)=>{const q=(off?r.offWeapon:r.weapon).getWorldQuaternion(new T.Quaternion());if(prev&&t>=1.95){const a=q.angleTo(prev);if(a>step){step=a;at=t-2;}}prev=q;});
  assert.ok(step<1,`${spec} cut ${index} @${rate}x: ${off?'off-hand ':''}weapon jumps ${step.toFixed(3)} rad in 1/960 s at ${at.toFixed(4)} s`);
 }
});

test('captured cuts keep every blade tip above the floor', () => {
 const tip=new T.Vector3(),root=new T.Vector3();
 for(const spec of ALL)for(const rate of [.8,1,1.35])for(let index=1;index<=CUTS[spec];index++)cut(spec,index,rate,120,(r,t)=>{if(t<2)return;r.group.getWorldPosition(root);
  for(const w of spec==='daggers'?[r.weapon,r.offWeapon]:[r.weapon]){const s=w.getObjectByName('blade-tip');if(!s)continue;s.getWorldPosition(tip);assert.ok(tip.y-root.y>-.80,`${spec} ${index} @${rate}x: tip ${(tip.y-root.y).toFixed(3)} at ${(t-2).toFixed(3)}`);}});
});

test('captured loops wrap continuously: the elbow poles and blades step no more across the seam than inside the loop', () => {
 const ang=(d:Float32Array,a:number,b:number,c:number)=>{const x=[0,1,2].map(i=>d[a*CHANNEL_COUNT+c+i]),y=[0,1,2].map(i=>d[b*CHANNEL_COUNT+c+i]),l=Math.hypot(...x)*Math.hypot(...y);return Math.acos(Math.max(-1,Math.min(1,(x[0]*y[0]+x[1]*y[1]+x[2]*y[2])/(l||1))));};
 for(const spec of ALL){const set=MOCAP_SETS[spec]!;for(const k of ['idle','walk','run','block'] as const){const c=set[k];if(!c?.loop)continue;const d=decodeClip(c),n=c.frames;
  for(const ch of ['rpole.x','lpole.x','blade.x','lfwd.x'] as const){let max=0;for(let f=1;f<n-1;f++)max=Math.max(max,ang(d,f-1,f,CH[ch]));const seam=ang(d,n-2,0,CH[ch]);
   assert.ok(seam<=max*1.5+.02,`${spec} ${k} ${ch}: seam ${seam.toFixed(3)} rad vs ${max.toFixed(3)} inside`);}}}
});

test('guard, walk, run and the stops between them keep the weapons continuous (60 vs 120 Hz)', () => {
 // Standing, walking off, breaking into a run, slowing and stopping: the carries blend over the captured guard.
 const speed=(t:number)=>t<1.5?0:t<2?(t-1.5)/.5*2.8:t<2.6?2.8:t<3?2.8+(t-2.6)/.4*4:t<3.6?6.8:t<4.2?6.8*(1-(t-3.6)/.6):0;
 for(const spec of ALL)for(const off of spec==='daggers'?[false,true]:[false]){
  const worst=[60,120].map(hz=>{const r=probeRig(spec),base={...probeBase(spec),id:'carry'} as PlayerView;let prev:T.Quaternion|undefined,step=0,at=0;
   for(let i=0;i<=hz*4;i++){const t=1+i/hz,v=speed(t);animateCharacter(r,{...animationPreviewPlayer(base,'idle',t),hitStunUntil:-1e9,vel:{x:0,y:0,z:v}} as PlayerView,t,1/hz);r.group.updateMatrixWorld(true);
    const q=(off?r.offWeapon:r.weapon).getWorldQuaternion(new T.Quaternion());if(prev&&t>1.2){const a=q.angleTo(prev);if(a>step){step=a;at=t;}}prev=q;}
   return {step,at};});
  assert.ok(worst[1].step<=worst[0].step*.75+.01,`${spec} ${off?'off-hand ':''}carry: ${worst[0].step.toFixed(3)} rad/frame at 60 Hz vs ${worst[1].step.toFixed(3)} at 120 Hz (a snap at ${worst[1].at.toFixed(3)} s)`);
 }
});

test('one-handed grips: the free hand never takes the sword\'s support grip; the dagger 4th cut turns a full circle', () => {
 const grip=new T.Vector3(),palm=new T.Vector3();
 for(const index of [1,2,3])cut('sword',index,1,60,r=>{r.weapon.getObjectByName('support-grip')!.getWorldPosition(grip);r.leftHand.getWorldPosition(palm);assert.ok(grip.distanceTo(palm)>.12,`sword ${index}: support hand on the hilt`);});
 // The turn's peak: a complete circle (≡ the guard heading, so the recovery hands back without unwinding).
 let first:number|undefined,peak=0;cut('daggers',4,1,60,(r,t)=>{const y=r.body.rotation.y;if(t>=2&&first===undefined)first=y;if(first!==undefined)peak=Math.max(peak,y-first);});
 assert.ok(Math.abs(peak-Math.PI*2)<.2,`dagger 4 turns ${peak.toFixed(2)} rad`);
});

test('guard, walk, run and block: blades clear the body and the floor; the raised shield faces forward, top up', () => {
 const {BLADE_OBSTACLES,BLADE_CLEARANCE:C}=bladeClearance,width={sword:.05,daggers:.045};
 const pos=new T.Vector3(),quat=new T.Quaternion(),scale=new T.Vector3(),inv=new T.Quaternion(),l=new T.Vector3(),p=new T.Vector3(),wq=new T.Quaternion(),wo=new T.Vector3(),X=new T.Vector3(),Y=new T.Vector3(),Z=new T.Vector3(),root=new T.Vector3();
 for(const spec of SPECS)for(const mode of ['idle','walk','run','block']){
  const r=probeRig(spec),base={...probeBase(spec),id:'clear'} as PlayerView,boxes=BLADE_OBSTACLES.map(n=>r.group.getObjectByName(n)!).filter(Boolean);let worst=Infinity,at='';
  for(let i=0;i<=360;i++){const t=i/120;animateCharacter(r,{...animationPreviewPlayer(base,mode,t),hitStunUntil:-1e9} as PlayerView,t,1/120);if(t<1)continue;r.group.updateMatrixWorld(true);r.group.getWorldPosition(root);
   for(const weapon of spec==='daggers'?[r.weapon,r.offWeapon]:[r.weapon]){const tip=weapon.getObjectByName('blade-tip')!.getWorldPosition(new T.Vector3());assert.ok(tip.y-root.y>-.80,`${spec} ${mode}: tip under the floor at ${t.toFixed(2)}`);}
   if(spec==='shield'){if(mode==='block'){r.weapon.getWorldQuaternion(wq);const face=new T.Vector3(0,0,1).applyQuaternion(wq),top=new T.Vector3(0,1,0).applyQuaternion(wq);assert.ok(face.z>.5&&top.y>.5,`shield block at ${t.toFixed(2)}: face ${face.toArray().map(x=>x.toFixed(2))} top ${top.toArray().map(x=>x.toFixed(2))}`);}continue;}
   r.weapon.getWorldPosition(wo);r.weapon.getWorldQuaternion(wq);const sz=r.weapon.scale.z,tipZ=r.weapon.getObjectByName('blade-tip')!.position.z-.08;
   X.set(1,0,0).applyQuaternion(wq);Y.set(0,1,0).applyQuaternion(wq);Z.set(0,0,1).applyQuaternion(wq);
   for(let s=-.19*sz;s<=tipZ*sz;s+=.05){if(s>-.05&&s<bladeClearance.GUARD)continue;for(let k=0;k<5;k++){
    p.copy(wo).addScaledVector(Z,s).addScaledVector(X,k===1?width[spec]:k===2?-width[spec]:0).addScaledVector(Y,k===3?C.halfThickness:k===4?-C.halfThickness:0);
    for(const b of boxes){b.matrixWorld.decompose(pos,quat,scale);l.copy(p).sub(pos).applyQuaternion(inv.copy(quat).invert());
     const q=[Math.abs(l.x)-scale.x/2,Math.abs(l.y)-scale.y/2,Math.abs(l.z)-scale.z/2],d=Math.max(...q)>0?Math.hypot(...q.map(x=>Math.max(x,0))):Math.max(...q);
     if(d<worst){worst=d;at=`${b.name} at ${t.toFixed(3)} s, ${s.toFixed(2)} m along the blade`;}}
   }}
  }
  if(spec!=='shield')assert.ok(worst>=.004,`${spec} ${mode}: blade ${(-worst*1000).toFixed(0)} mm into ${at}`);
 }
});
