import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {initPhysics,type PlayerView} from '../src/shared/simulation.js';
import {WEAPONS} from '../src/shared/content.js';
import {attackCommitment,attackTravel} from '../src/shared/souls-combat.js';
import {animateCharacter} from '../src/client/character-animation.js';
import {type CharacterRig} from '../src/client/character-rig.js';
import {hitStop} from '../src/client/primary-melee-motion.js';
import {recordMeleeHit,recordBlockHit,clearBlockImpacts} from '../src/client/block-impacts.js';
import {probeBase,probeRig,weaponTip} from './helpers/motion-probe.js';

await initPhysics();
/** Souls-melee pass: reference phrases inside the authoritative timing, feet within reach, a heavy follow-through that never
 * rewinds through the front, a presentation-only hit-stop and a block recoil that reads. */
const COMBOS:[string,number][]=[['sword',1],['sword',2],['sword',3],['daggers',1],['daggers',2],['daggers',3],['daggers',4],['shield',1],['shield',2],['greatsword',1],['greatsword',2],['greatsword',3]];
const LIMBS=['leftElbow','rightElbow','leftKnee','rightKnee','leftArm','rightArm','leftLeg','rightLeg'] as const;
/** A standing player attacking at t=2 at `rate` (window .16/rate, as the server scales it), root following the travel. */
function scenario(spec:string,index:number,rate=1,id='souls'){
 const base={...probeBase(spec),id,grounded:true,vel:{x:0,y:0,z:0},hitStunUntil:-1e9,animationTime:undefined} as PlayerView,w=WEAPONS[spec as keyof typeof WEAPONS],windup=w.windup/rate,window=.16/rate,lastAttack=2;
 const c=attackCommitment(base.spec,lastAttack,windup,window,rate,w.interval/rate);
 const attack={...base,attackIndex:index,attackRate:rate,lastAttack,windup,swingAt:lastAttack+windup,swingUntil:lastAttack+windup+window,attackCommitment:c} as PlayerView;
 // Combat-ready (a block .8 s earlier keeps the guard up), as mid-fight: the phrase starts from the guard it is written from.
 const idle={...base,lastAttack:-10,lastBlock:1.2,swingAt:undefined,swingUntil:undefined,attackCommitment:undefined} as PlayerView;
 return {attack,idle,c,lastAttack,windup,window,end:c.until};
}
function run(s:ReturnType<typeof scenario>,hz:number,each:(r:CharacterRig,t:number,p:PlayerView)=>void,rig=probeRig(s.attack.spec),from=1.7){
 for(let i=0;;i++){const t=from+i/hz;if(t>s.end+.1)break;const p=t<s.lastAttack?s.idle:s.attack;rig.group.position.set(0,0,t<s.lastAttack?0:attackTravel(s.c,t));rig.group.rotation.y=0;animateCharacter(rig,p,t,1/hz);rig.group.updateMatrixWorld(true);each(rig,t,p);}
 return rig;
}

test('contact lands in the strike window for every combo at 0.7x, 1x and 1.5x attack rates',()=>{
 for(const rate of [.7,1,1.5])for(const [spec,index] of COMBOS){
  const s=scenario(spec,index,rate),hz=240;let prev:T.Vector3|undefined,peak=0,peakAt=0;
  run(s,hz,(r,t,p)=>{const tip=weaponTip(r,p).sub(r.group.position);if(prev&&t>s.lastAttack){const v=tip.distanceTo(prev)*hz;if(v>peak){peak=v;peakAt=t-s.lastAttack;}}prev=tip;});
  // The blade is fastest inside swingAt..swingUntil (a frame of slack at each edge), never in the windup or the recovery.
  if(spec==='daggers'&&index===4)continue; // The spin's authored full turn carries the blade round through the recovery.
  assert.ok(peakAt>=s.windup-1/120&&peakAt<=s.windup+s.window+1/120,`${spec} ${index} @${rate}x: fastest at ${peakAt.toFixed(3)} s, window ${s.windup.toFixed(3)}..${(s.windup+s.window).toFixed(3)}`);
 }
});

test('limbs stay continuous (no single-frame flips) at every attack rate',()=>{
 for(const rate of [.7,1,1.5])for(const [spec,index] of COMBOS){
  const worst=[60,120].map(hz=>{let prev:T.Quaternion[]|undefined,step=0;const s=scenario(spec,index,rate);
   run(s,hz,(r,t)=>{const q=LIMBS.map(b=>r[b].getWorldQuaternion(new T.Quaternion()));if(prev&&t>=1.95)q.forEach((x,k)=>{step=Math.max(step,x.angleTo(prev![k]));});prev=q;});return step;});
  // A snap keeps its size per frame as the rate doubles; continuous motion halves. The wave-2 speed bound (110 rad/s at
  // 1x) scales with the attack rate, which compresses the same phrase in time.
  assert.ok(worst[1]<=worst[0]*.7+.01,`${spec} ${index} @${rate}x: ${worst[0].toFixed(3)} rad/frame at 60 Hz vs ${worst[1].toFixed(3)} at 120 Hz`);
  assert.ok(worst[1]*120<110*Math.max(1,rate),`${spec} ${index} @${rate}x: ${(worst[1]*120).toFixed(0)} rad/s (wave-2 bound 110 at 1x)`);
 }
});

test('the leading foot is planted from the plant through the end of the strike window',()=>{
 for(const [spec,index] of COMBOS){if(spec==='daggers'&&index===4)continue;const s=scenario(spec,index);
  run(s,120,(r,t)=>{const age=t-s.lastAttack;if(age<s.windup+s.window*.7||age>s.windup+s.window)return;
   const y=Math.min(r.leftFoot.getWorldPosition(new T.Vector3()).y,r.rightFoot.getWorldPosition(new T.Vector3()).y);
   const lead=(spec==='daggers'||spec==='greatsword'||spec==='shield'?(index-1)%2===1:(index-1)%3===1)?r.rightFoot:r.leftFoot,ly=lead.getWorldPosition(new T.Vector3()).y;
   assert.ok(Math.abs(ly+.795)<.012,`${spec} ${index}: lead ankle ${ly.toFixed(3)} at ${age.toFixed(3)}`);assert.ok(y>-.806);});
 }
});

test('combat steps stay within leg reach: every ankle lands where its step put it',()=>{
 // A foot the leg cannot reach stops short of its target; the hip-to-ankle distance then sits on the IK clamp. Planted
 // ankles at the floor with the leg short of the clamp mean the step targets are reachable.
 for(const [spec,index] of COMBOS){const s=scenario(spec,index);let clamped=0,frames=0;
  run(s,60,(r,t)=>{if(t<s.lastAttack)return;frames++;for(const [hip,ankle] of [[r.leftLeg,r.leftFoot],[r.rightLeg,r.rightFoot]] as const){const d=hip.getWorldPosition(new T.Vector3()).distanceTo(ankle.getWorldPosition(new T.Vector3()));if(d>.4845){clamped++;break;}}});
  assert.ok(clamped/frames<=.1,`${spec} ${index}: legs at full reach in ${clamped}/${frames} frames (integration audit: 17-38%)`);
 }
});

test('the greatsword follows through around the body and never comes back over the top through the front',()=>{
 for(const index of [1,2,3]){const s=scenario('greatsword',index);let worst=-Infinity,at=0;
  run(s,120,(r,t,p)=>{const age=t-s.lastAttack;if(age<=s.windup+s.window)return;const tip=weaponTip(r,p).sub(r.group.position);
   // In front of the body (inside a 60 degree cone ahead) the tip stays low all through the recovery.
   if(tip.z>Math.abs(tip.x)*.58&&tip.y>worst){worst=tip.y;at=age;}});
  assert.ok(worst<.2,`greatsword ${index}: tip ${worst.toFixed(2)} m up in front at ${at.toFixed(2)} s after the cut (before: 1.1 m, a second chop)`);
 }
});

test('hit-stop is a presentation-only pause that never runs the pose clock backward and returns to identity',()=>{
 clearBlockImpacts();
 const s=scenario('greatsword',1,1,'stopper'),h=s.attack.swingAt!+.05;recordMeleeHit({type:'hit',time:h,source:'stopper'});
 const lags:number[][]=[];
 for(const hz of [30,60,120]){const r={},out:number[]=[];let prevClock=-Infinity;
  for(let i=0;;i++){const t=s.lastAttack+i/hz;if(t>s.end)break;const a={age:t-s.lastAttack,windup:s.windup,window:s.window},lag=hitStop(r,s.attack,a);out.push(lag);
   assert.ok(lag>=0&&lag<.08,`lag ${lag}`);const clock=a.age-lag;assert.ok(clock>=prevClock-1e-9,`${hz} Hz: the pose clock runs backward at ${t.toFixed(3)}`);prevClock=clock;
   if(t>h+.2)assert.equal(lag,0,`${hz} Hz: back on the authoritative clock by ${(t-h).toFixed(3)} s`);}
  lags.push(out);}
 const peak=lags.map(l=>Math.max(...l));assert.ok(peak.every(x=>x>.05),'a greatsword hit holds the blade for a beat');assert.ok(Math.max(...peak)-Math.min(...peak)<.02,`rate independent: ${peak.map(x=>x.toFixed(3)).join('/')}`);
 // A hit event that arrives late (the local prediction's case) shows no stop rather than popping the pose back.
 const late={};assert.equal(hitStop(late,s.attack,{age:h+.09-s.lastAttack,windup:s.windup,window:s.window}),0);
 // Hits of other players, or outside this attack's strike window, never stop it.
 assert.equal(hitStop({},{...s.attack,id:'other'},{age:h+.03-s.lastAttack,windup:s.windup,window:s.window}),0);
 clearBlockImpacts();recordMeleeHit({type:'hit',time:s.lastAttack+.9,source:'stopper'});assert.equal(hitStop({},s.attack,{age:.92,windup:s.windup,window:s.window}),0);
 recordMeleeHit({type:'hit',time:h,source:'burner',message:'burn'});assert.equal(hitStop({},{...s.attack,id:'burner'},{age:h+.01-s.lastAttack,windup:s.windup,window:s.window}),0,'damage-over-time ticks are not hits');
 clearBlockImpacts();
});

test('a landed hit stops the rig for a beat without snapping any limb, the feet or the root',()=>{
 clearBlockImpacts();
 for(const spec of ['sword','greatsword','shield','daggers']){
  const s=scenario(spec,1,1,'hs-'+spec),h=s.attack.swingAt!+s.window*.4;recordMeleeHit({type:'hit',time:h,source:'hs-'+spec});
  const tips:Record<number,T.Vector3[]>={};
  for(const hz of [60,120]){let prev:T.Quaternion[]|undefined,step=0;const list:T.Vector3[]=[];const plain=probeRig(spec),stopped=probeRig(spec);let differ=0;
   run(s,hz,(r,t)=>{const q=LIMBS.map(b=>r[b].getWorldQuaternion(new T.Quaternion()));if(prev&&t>=1.95)q.forEach((x,k)=>{step=Math.max(step,x.angleTo(prev![k]));});prev=q;list.push(weaponTip(r,s.attack).sub(r.group.position));},stopped);
   assert.ok(step*hz<110,`${spec} ${hz} Hz: ${(step*hz).toFixed(0)} rad/s through the stop`);tips[hz]=list;
   // Past the stop the rig is exactly the unstopped pose again.
   clearBlockImpacts();const noHit:T.Vector3[]=[];run(s,hz,(r)=>{noHit.push(weaponTip(r,s.attack).sub(r.group.position));},plain);recordMeleeHit({type:'hit',time:h,source:'hs-'+spec});
   list.forEach((v,i)=>{const t=1.7+i/hz;if(t>h+.2)assert.ok(v.distanceTo(noHit[i])<.01,`${spec}: back on the unstopped pose after the stop (${v.distanceTo(noHit[i]).toFixed(4)} m at ${(t-h).toFixed(3)} s)`);else if(v.distanceTo(noHit[i])>.05)differ++;});
   assert.ok(differ>0,`${spec}: the stop is visible`);
  }
 }
 clearBlockImpacts();
});

test('a blocked hit knocks the guard back far enough to read, then settles',()=>{
 clearBlockImpacts();
 for(const spec of ['sword','greatsword','shield','daggers']){
  const base={...probeBase(spec),id:'wall-'+spec,hitStunUntil:-1e9,lastAttack:-10,guarding:true,blockTime:0} as PlayerView;
  const tip=(rig:CharacterRig,t:number)=>{animateCharacter(rig,base,t,1/60);rig.group.updateMatrixWorld(true);return weaponTip(rig,base);};
  // Against the same guard unhit (the held guard sways slowly on its own), sampled before the block is recorded.
  const calm=probeRig(spec),still:T.Vector3[]=[];for(let n=0;n<54;n++)still.push(tip(calm,3+n/60));
  const r=probeRig(spec);for(let n=0;n<30;n++)tip(r,3+n/60);recordBlockHit({type:'blockHit',time:3.5,pos:{x:0,y:1,z:1},source:base.id,target:'a'},3.5);
  let shove=0,settled=0,jump=0,prev=still[29];for(let n=30;n<54;n++){const t=3+n/60,now=tip(r,t),d=now.distanceTo(still[n]);jump=Math.max(jump,now.distanceTo(prev)-still[n].distanceTo(still[n-1]));prev=now;if(t<3.62)shove=Math.max(shove,d);else settled=d;}
  if(process.env.SM_DEBUG)console.log(spec,shove,jump);assert.ok(jump<.3,`${spec}: the shove is a hit, not a teleport (${(jump*100).toFixed(0)} cm in one 60 Hz frame)`);
  assert.ok(shove>.2,`${spec}: blade tip knocked ${(shove*100).toFixed(0)} cm, ${(jump*100).toFixed(0)} cm/frame max (integration audit: ~15 cm)`);assert.ok(settled<.01,`${spec}: back in the guard (${settled.toFixed(3)} m)`);
 }
 clearBlockImpacts();
});
