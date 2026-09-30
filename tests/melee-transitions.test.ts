import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import * as T from 'three';
import {initPhysics,type PlayerView} from '../src/shared/simulation.js';
import {WEAPONS} from '../src/shared/content.js';
import {attackCommitment} from '../src/shared/souls-combat.js';
import {animateCharacter,animationPreviewPlayer} from '../src/client/character-animation.js';
import {probeBase,probeRig,rootTravel} from './helpers/motion-probe.js';

await initPhysics();
/** Transitions into and out of the running carry and the combat-ready guard: an attack, a stop or a hit out of a run must
 * not flip the weapon between the carry and the guard in a frame or two. Every scenario is stepped at 60 and 120 Hz: the
 * worst per-frame weapon turn must be bounded at 60 Hz and roughly halve at 120 Hz (continuous motion, not a pop that
 * lands in one frame whatever the rate). */
type View=PlayerView;
const attackAt=(spec:string,at:number,index=1)=>{const w=WEAPONS[spec as keyof typeof WEAPONS];return {attackIndex:index,lastAttack:at,swingAt:at+w.windup,swingUntil:at+w.windup+.16,attackCommitment:attackCommitment(spec as View['spec'],at,w.windup,.16,1,w.windup+.16),attackRate:1};};
const idle=(t:number,b:View)=>({...animationPreviewPlayer({...b},'idle',t),hitStunUntil:-1e9}) as View;
const running=(t:number,b:View)=>({...animationPreviewPlayer({...b},'run',t),hitStunUntil:-1e9}) as View;
type Step={weapon:number;off:number;tip:number;tipZ:number;tipY:number};
const bladeTip=(w:T.Object3D,out:T.Vector3)=>(w.getObjectByName('blade-tip')??w).getWorldPosition(out);
/** Worst per-frame world turn of each weapon (degrees; the off-hand weapon only for the daggers), tip step (m, either
 * blade) and the main tip's furthest forward and highest point (rig frame, relative to the root) over [from,to]; the rig
 * is warmed up for 1.5 s before `from`. */
function sweep(spec:string,hz:number,view:(t:number,b:View)=>View,from:number,to:number):Step{
 const base=probeBase(spec),r=probeRig(spec),root=new T.Vector3(),q=new T.Quaternion(),o=new T.Quaternion(),pq=new T.Quaternion(),po=new T.Quaternion(),tip=new T.Vector3(),pt=new T.Vector3(),off=new T.Vector3(),pOff=new T.Vector3(),dual=spec==='daggers';
 const out={weapon:0,off:0,tip:0,tipZ:-Infinity,tipY:-Infinity};let prev:number|undefined;
 for(let i=0;;i++){
  const t=from-1.5+i/hz;if(t>to)break;const p=view(t,base);
  if(prev!==undefined){const d=rootTravel(p,prev,t);root.x+=d.x;root.z+=d.z;}
  r.group.position.copy(root);r.group.rotation.y=0;animateCharacter(r,{...p,yaw:0},t,1/hz,p.pitch);r.group.updateMatrixWorld(true);
  r.weapon.getWorldQuaternion(q);r.offWeapon.getWorldQuaternion(o);bladeTip(r.weapon,tip).sub(root);bladeTip(r.offWeapon,off).sub(root);
  if(prev!==undefined&&t>=from){out.weapon=Math.max(out.weapon,q.angleTo(pq)*180/Math.PI);if(dual)out.off=Math.max(out.off,o.angleTo(po)*180/Math.PI);out.tip=Math.max(out.tip,tip.distanceTo(pt),dual?off.distanceTo(pOff):0);out.tipZ=Math.max(out.tipZ,tip.z);out.tipY=Math.max(out.tipY,tip.y);}
  pq.copy(q);po.copy(o);pt.copy(tip);pOff.copy(off);prev=t;
 }
 return out;
}
/** Bounded at 60 Hz and continuous: a positional pop keeps its size at any frame rate, continuous motion shrinks with the
 * step (about half at 120 Hz; up to ~3/4 where motion starts at speed, as when the stride collapses on an action). */
function smooth(label:string,a60:Step,a120:Step,maxTurn:number,maxTip:number){
 if(process.env.MELEE_DEBUG)console.log(label,JSON.stringify({a60,a120},(k,v)=>typeof v==='number'?+v.toFixed(2):v));
 assert.ok(a60.weapon<=maxTurn&&a60.off<=maxTurn,`${label}: weapon turns ${a60.weapon.toFixed(1)}/${a60.off.toFixed(1)} deg in one 60 Hz frame (<= ${maxTurn})`);
 assert.ok(a60.tip<=maxTip,`${label}: tip jumps ${a60.tip.toFixed(2)} m in one 60 Hz frame (<= ${maxTip})`);
 for(const k of ['weapon','off','tip'] as const)assert.ok(a120[k]<=a60[k]*.76+(k==='tip'?.01:1),`${label}: ${k} step does not shrink at 120 Hz (${a60[k].toFixed(2)} -> ${a120[k].toFixed(2)}), a pop`);
}
const SPECS=['sword','greatsword','daggers','shield'];
/** Bounds per spec at 60 Hz (degrees, metres). The sword's forehand windup moves its tip up to ~.95 m per frame even out
 * of the guard; the daggers' .09 s windup turns the reverse-grip carry into the forward grip in five frames. */
const ENTRY:Record<string,[number,number]>={sword:[66,.95],greatsword:[30,.7],daggers:[112,1.1],shield:[40,.3]};

test('an attack out of a run blends from the carry into the phrase without flipping through the guard',()=>{
 for(const spec of SPECS)for(const index of [1,2,3]){
  const windup=WEAPONS[spec as keyof typeof WEAPONS].windup,view=(t:number,b:View)=>{const p=running(t,b);return t<3?p:{...p,...attackAt(spec,3,index)} as View;};
  const a60=sweep(spec,60,view,2.98,3+windup+.01),a120=sweep(spec,120,view,2.98,3+windup+.01);
  smooth(`${spec} #${index} run -> attack`,a60,a120,...ENTRY[spec]);
  // The trailing sword never detours forward into the guard on the way to the load (the tip stays behind the chest).
  if(spec==='sword'&&index!==3)assert.ok(a60.tipZ<.1&&a120.tipZ<.1,`sword #${index}: tip swings forward to z=${Math.max(a60.tipZ,a120.tipZ).toFixed(2)} out of the run`);
 }
});

test('an attack out of the relaxed idle raises into the phrase over the windup instead of snapping into the guard',()=>{
 const bound:Record<string,[number,number]>={sword:[45,1.1],greatsword:[20,.5],daggers:[56,1.1],shield:[25,.25]};
 for(const spec of SPECS)for(const index of [1,2,3]){
  const windup=WEAPONS[spec as keyof typeof WEAPONS].windup,view=(t:number,b:View)=>({...idle(t,b),...(t<3?{lastAttack:-10}:attackAt(spec,3,index))}) as View;
  smooth(`${spec} #${index} idle -> attack`,sweep(spec,60,view,2.98,3+windup+.01),sweep(spec,120,view,2.98,3+windup+.01),...bound[spec]);
 }
});

test('stopping a combat-ready run and starting one from the guard ease the weapon between carry and guard',()=>{
 for(const spec of SPECS){
  const stop=(t:number,b:View)=>({...(t<3.3?running(t,b):idle(t,b)),...attackAt(spec,2)}) as View,start=(t:number,b:View)=>({...(t<3.2?idle(t,b):running(t-3.2,b)),...attackAt(spec,2)}) as View;
  smooth(`${spec} run -> stop (ready)`,sweep(spec,60,stop,3.28,4),sweep(spec,120,stop,3.28,4),spec==='daggers'||spec==='shield'?28:15,.35);
  smooth(`${spec} guard -> run`,sweep(spec,60,start,3.18,4),sweep(spec,120,start,3.18,4),28,.35);
 }
});

// The hit reaction rides the guard and carry only once sampleCharacter's hitstun branch calls meleeReady (integrator
// request 1 in the melee handoff); until then it falls to the relaxed hold, which this test would flag.
const wired=/hitStunUntil[^\n]*meleeReady\(/.test(readFileSync(new URL('../src/client/character-animation.ts',import.meta.url),'utf8'));
test('a hit while combat-ready (standing or running) rides the guard and carry instead of dropping them',{skip:wired?false:'needs integrator request 1 (meleeReady in the hitstun branch)'},()=>{
 const hit={kind:'hit',at:3,until:3.24,zone:'torso',turn:.3,strength:.6,yaw:0,direction:{x:0,y:0,z:1},point:{x:0,y:1,z:.3}};
 for(const spec of SPECS){
  const stand=(t:number,b:View)=>({...idle(t,b),...attackAt(spec,2),hitReaction:t>=3?hit:undefined,hitStunUntil:t>=3?3.24:-1e9}) as unknown as View;
  const run=(t:number,b:View)=>({...running(t,b),...attackAt(spec,1),hitReaction:t>=3?hit:undefined,hitStunUntil:t>=3?3.24:-1e9}) as unknown as View;
  smooth(`${spec} hit while ready`,sweep(spec,60,stand,2.98,3.5),sweep(spec,120,stand,2.98,3.5),18,.4);
  smooth(`${spec} hit while running and ready`,sweep(spec,60,run,2.98,3.5),sweep(spec,120,run,2.98,3.5),spec==='daggers'?50:32,.6);
 }
});
