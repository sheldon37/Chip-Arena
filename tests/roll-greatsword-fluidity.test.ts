import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {initPhysics} from '../src/shared/simulation.js';
import {animateCharacter} from '../src/client/character-animation.js';
import {lowestShell,ROLL_FLOOR} from '../src/client/dodge-roll-motion.js';
import {PROBE_RATES,probeMotion} from './helpers/motion-probe.js';

await initPhysics();
type Rig=Parameters<typeof lowestShell>[0];
const box=new T.Box3();
/** Lowest point (rig-group height) of the meshes whose names match `parts`. */
function lowest(r:Rig,parts:RegExp){let min=Infinity;r.body.traverse(o=>{if((o as T.Mesh).isMesh&&parts.test(o.name)){box.setFromObject(o);min=Math.min(min,box.min.y-r.group.position.y);}});return min;}
const LIMBS=/-(foot|heel|palm|fingers|thumb)$|^(blank-head|crown)$/;
const roll=(x:number,z:number,at=1.5,until=2)=>({at,until,invulnerableAt:at+.08,invulnerableUntil:at+.34,direction:{x,y:0,z},distance:3.2});
/** Samples of a roll at `hz`: u, the lowest body-shell point and the lowest foot/hand/head point relative to the sole plane. */
function sampleRoll(spec:string,x:number,z:number,hz:number){
 const R=roll(x,z),out:{t:number;u:number;shell:number;limb:number;body:number}[]=[];
 probeMotion({spec,mode:'idle',from:1.2,to:2.1,hz,travel:true,patch:()=>({roll:R}),animate:(r,p,t,dt)=>{animateCharacter(r,p,t,dt,p.pitch);r.group.updateMatrixWorld(true);
  const u=(t-R.at)/(R.until-R.at);if(u>=0&&u<1)out.push({t,u,shell:lowestShell(r)-ROLL_FLOOR,limb:lowest(r,LIMBS)-ROLL_FLOOR,body:r.body.position.y});}});
 return out;
}
const DIRS=[[0,1],[1,0],[-1,0],[0,-1],[.7,.7],[-.7,-.7]] as const;

test('the dodge roll hugs the floor: the tucked body touches it within 3 cm and no hand, foot or head goes through it',()=>{
 for(const spec of ['sword','greatsword'])for(const [x,z] of DIRS){
  const s=sampleRoll(spec,x,z,60),at=`${spec} roll (${x},${z})`;
  // Baseline (before the floor solve): the tucked body pivoted about the hips and floated up to 50 cm above the floor.
  for(const f of s.filter(f=>f.u>=.3&&f.u<=.62))assert.ok(Math.abs(f.shell)<.03,`${at} u=${f.u.toFixed(2)}: lowest body part ${(f.shell*100).toFixed(1)} cm from the floor`);
  for(const f of s){assert.ok(f.limb>-.015,`${at} u=${f.u.toFixed(2)}: a hand/foot/head ${(f.limb*100).toFixed(1)} cm under the floor`);assert.ok(f.shell>-.025,`${at} u=${f.u.toFixed(2)}: body ${(f.shell*100).toFixed(1)} cm under the floor`);}
  // The dive lowers the hips well below standing height before the tuck (a dip, not a hop).
  assert.ok(Math.min(...s.filter(f=>f.u<.3).map(f=>f.body))<-.2,`${at}: dips into the roll`);
 }
});

test('the roll is rate independent: the same authoritative phase gives the same contact at 30, 60 and 120 Hz',()=>{
 for(const [x,z] of [[0,1],[1,0],[0,-1]] as const){
  const runs=PROBE_RATES.map(hz=>new Map(sampleRoll('sword',x,z,hz).map(f=>[Math.round(f.t*120),f])));
  let compared=0;
  for(const [key,f] of runs[0]){const others=runs.slice(1).map(m=>m.get(key));if(others.some(o=>!o)||f.u<.1)continue;compared++;
   for(const o of others){assert.ok(Math.abs(o!.body-f.body)<.012,`(${x},${z}) u=${f.u.toFixed(2)}: body height ${f.body.toFixed(3)} vs ${o!.body.toFixed(3)}`);assert.ok(Math.abs(o!.shell-f.shell)<.012,`(${x},${z}) u=${f.u.toFixed(2)}: contact`);}}
  assert.ok(compared>8,'compared the shared 30 Hz frames');
 }
});
