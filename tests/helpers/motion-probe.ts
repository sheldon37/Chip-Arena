/**
 * Shared presentation motion probe (foundation-owned; import it from new tests, do not copy it).
 * Steps a real rig through a scripted PlayerView timeline at a fixed rate (30/60/120 Hz) and reports:
 *  - joints: max per-frame WORLD rotation of every bone (degrees; also as deg/s) and which bone/time
 *  - feet:   planted-foot world slip (ankle below root.y+plantY, measured from touchdown), planted-foot speed,
 *            and the minimum ankle height relative to the root (floor penetration; braced ankles sit at -.795)
 *  - weapon: per-frame blade-tip / muzzle jump relative to the root (m; also m/s) and its minimum height
 *  - body:   per-frame jump of the motion-root joint (r.body local position, m; also m/s)
 * Ported from the audit probes (locomotion/metrics.mts, melee/metrics.mts, reactions-death/snapdetect.js).
 *
 * A snap is a single-frame jump, so it keeps its size as the rate rises, while smooth motion's per-frame
 * delta shrinks with dt: compare `max*Step` across probeRates() results, or bound the per-second values.
 * Times are on the scenario clock. By default the preview player's hitStunUntil is forced to -1e9 (the lab's
 * time-base fix), so warm-up at negative times never enters the hitstun flinch branch.
 * Call `await initPhysics()` (src/shared/simulation.ts) before probeBase().
 */
import * as T from 'three';
import {Simulation,type PlayerView} from '../../src/shared/simulation.js';
import {v,forward} from '../../src/shared/config.js';
import {attackCommitment,attackTravel,committed,rollVelocity,standardWeapon} from '../../src/shared/souls-combat.js';
import {createCharacterRig,type CharacterRig} from '../../src/client/character-rig.js';
import {animateCharacter,animationPreviewPlayer,attackPose} from '../../src/client/character-animation.js';
import {buildWeapon} from '../../src/client/weapon-visuals.js';

export const PROBE_BONES=['body','pelvis','spine','chest','neck','head','leftShoulder','rightShoulder','leftArm','rightArm','leftElbow','rightElbow','leftHand','rightHand','leftLeg','rightLeg','leftKnee','rightKnee','leftFoot','rightFoot','leftToe','rightToe','weapon','offWeapon'] as const;
export type ProbeBone=typeof PROBE_BONES[number];
/** Ankle height (relative to the root) below which a foot counts as planted, as in the melee audit. */
export const PLANT_Y=-.785;
export const PROBE_RATES=[30,60,120] as const;

const bases=new Map<string,PlayerView>();
/** A sandbox fight-phase player snapshot for the spec at yaw 0 (stats such as reach match the spec), built like
 * the visual lab's. Needs initPhysics() first. */
export function probeBase(spec='pistol'):PlayerView{
 let base=bases.get(spec);
 if(!base){
  const sim=new Simulation({sandbox:true});sim.addPlayer('probe','Probe','ranged',null,'#ff6b35',{});
  const player=sim.players.get('probe')!;player.spec=spec as PlayerView['spec'];sim.resetRound();sim.phase='fight';
  for(const prop of [...sim.props.values()])sim.removeProp(prop.id);
  sim.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});
  player.body.setTranslation(v(0,1.2,0),true);for(let i=0;i<90;i++)sim.step(1/60);
  base={...sim.snapshot().players.find(p=>p.id==='probe')!,yaw:0,pitch:0};sim.dispose();bases.set(spec,base);
 }
 return {...base};
}
/** A rig dressed with the spec's weapon(s), as the lab and renderer build it. */
export function probeRig(spec:string,glow=false):CharacterRig{
 const r=createCharacterRig('#ff6b35',undefined,glow);buildWeapon(r.weapon,spec);
 const paired=['daggers','ansatsuken','emperor'].includes(spec);r.offWeapon.visible=paired;if(paired)buildWeapon(r.offWeapon,spec,true);
 return r;
}

export interface ProbeScenario{
 spec:string;
 /** Preview mode for animationPreviewPlayer (idle|walk|run|attack|...); ignored when `player` is given. */
 mode?:string;
 /** Full control of the view at time t (default: animationPreviewPlayer(base,mode,t) + time-base fix). */
 player?:(t:number,base:PlayerView)=>PlayerView;
 /** Fields merged over the view at time t (after `player`). */
 patch?:(t:number,p:PlayerView)=>Partial<PlayerView>;
 /** Measured window [from,to] (scenario clock); frames before `from` are warm-up. */
 from:number;to:number;warm?:number;
 /** Steps per second (default 60). */
 hz?:number;
 /** Integrate root travel like the lab's root=travel: velocity (unless planted), roll and melee attack travel. */
 travel?:boolean;
 /** Custom animation call (default animateCharacter(r,p,t,dt,p.pitch)). */
 animate?:(r:CharacterRig,p:PlayerView,t:number,dt:number)=>void;
 /** Custom rig (default probeRig(spec)). */
 rig?:CharacterRig;
 /** Keep the raw per-frame samples in the report (default false). */
 keepFrames?:boolean;
 /** Planted-ankle threshold relative to the root (default PLANT_Y). */
 plantY?:number;
}
export interface ProbeFrame{t:number;root:T.Vector3;ankles:[T.Vector3,T.Vector3];tip:T.Vector3|undefined;body:T.Vector3;}
export interface FootReport{maxSlip:number;maxPlantedSpeed:number;medianPlantedSpeed:number;plantedFrames:number;minY:number}
export interface ProbeReport{
 hz:number;frames:number;
 maxJointStepDeg:number;maxJointStepBone:ProbeBone|'';maxJointStepAt:number;maxJointSpeedDegPerSec:number;
 feet:{left:FootReport;right:FootReport};maxFootSlip:number;minAnkleY:number;
 maxTipStep:number;maxTipStepAt:number;maxTipSpeed:number;minTipY:number;
 maxBodyStep:number;maxBodyStepAt:number;maxBodySpeed:number;
 samples?:ProbeFrame[];
}
const scratchQ=PROBE_BONES.map(()=>new T.Quaternion()),prevQ=PROBE_BONES.map(()=>new T.Quaternion());
/** Blade tip (or the off-hand blade on even dagger strikes), else muzzle, else the weapon socket, in world space. */
export function weaponTip(r:CharacterRig,p?:PlayerView,out=new T.Vector3()){
 const weapon=p&&p.spec==='daggers'&&(p.attackIndex||1)%2===0?r.offWeapon:r.weapon;
 const socket=weapon.getObjectByName('blade-tip')??weapon.getObjectByName('muzzle');
 return (socket??weapon).getWorldPosition(out);
}
/** Root displacement over (prev,t]: roll displacement while rolling, else velocity unless planted, plus melee attack travel. */
export function rootTravel(p:PlayerView,prev:number,t:number){
 const gap=t-prev,out={x:0,z:0},roll=p.roll;
 if(roll&&prev<roll.until&&t>roll.at){const u=rollVelocity(roll,prev,gap);out.x+=u.x*gap;out.z+=u.z*gap;}
 else if(!(committed(p,t)||(p.hitStunUntil||0)>t)){out.x+=p.vel.x*gap;out.z+=p.vel.z*gap;}
 if(standardWeapon(p.spec)){const a=attackPose(p,t),c=p.attackCommitment||attackCommitment(p.spec,p.lastAttack,a.windup,a.window,p.attackRate||1,a.windup+a.window),d=attackTravel(c,t)-attackTravel(c,prev),f=forward(p.attackCommitment?c.yaw:p.yaw);out.x+=f.x*d;out.z+=f.z*d;}
 return out;
}
const median=(xs:number[])=>{if(!xs.length)return 0;const s=[...xs].sort((a,b)=>a-b);return s[Math.floor(s.length/2)];};
export function probeMotion(s:ProbeScenario):ProbeReport{
 const hz=s.hz??60,dt=1/hz,warm=s.warm??1,plantY=s.plantY??PLANT_Y,base=probeBase(s.spec),r=s.rig??probeRig(s.spec);
 const view=s.player??((t:number,b:PlayerView)=>({...animationPreviewPlayer({...b},s.mode??'idle',t),hitStunUntil:-1e9}));
 const animate=s.animate??((rig:CharacterRig,p:PlayerView,t:number,h:number)=>{animateCharacter(rig,p,t,h,p.pitch);});
 const t0=s.from-warm,steps=Math.round((s.to-t0)*hz);
 const report:ProbeReport={hz,frames:0,maxJointStepDeg:0,maxJointStepBone:'',maxJointStepAt:NaN,maxJointSpeedDegPerSec:0,feet:{left:{maxSlip:0,maxPlantedSpeed:0,medianPlantedSpeed:0,plantedFrames:0,minY:Infinity},right:{maxSlip:0,maxPlantedSpeed:0,medianPlantedSpeed:0,plantedFrames:0,minY:Infinity}},maxFootSlip:0,minAnkleY:Infinity,maxTipStep:0,maxTipStepAt:NaN,maxTipSpeed:0,minTipY:Infinity,maxBodyStep:0,maxBodyStepAt:NaN,maxBodySpeed:0};
 const samples:ProbeFrame[]=[],speeds:[number[],number[]]=[[],[]],touchdown:[T.Vector3|undefined,T.Vector3|undefined]=[undefined,undefined];
 const root=new T.Vector3(),prevAnkles=[new T.Vector3(),new T.Vector3()],prevTip=new T.Vector3(),prevBody=new T.Vector3();
 let prevT:number|undefined,measured=false,hadTip=false;
 for(let i=0;i<=steps;i++){
  const t=t0+i/hz;let p=view(t,base);if(s.patch)p={...p,...s.patch(t,p)} as PlayerView;
  if(s.travel&&prevT!==undefined){const d=rootTravel(p,prevT,t);root.x+=d.x;root.z+=d.z;}
  r.group.position.copy(root);r.group.rotation.y=p.yaw;animate(r,p,t,dt);r.group.updateMatrixWorld(true);
  const ankles:[T.Vector3,T.Vector3]=[r.leftFoot.getWorldPosition(new T.Vector3()),r.rightFoot.getWorldPosition(new T.Vector3())];
  const tipWorld=weaponTip(r,p),tip=r.weapon.visible||r.offWeapon.visible?tipWorld.clone().sub(root):undefined,body=r.body.position.clone();
  PROBE_BONES.forEach((b,k)=>r[b].getWorldQuaternion(scratchQ[k]));
  if(t>=s.from-1e-9){
   if(measured){
    PROBE_BONES.forEach((b,k)=>{const deg=scratchQ[k].angleTo(prevQ[k])*180/Math.PI;if(deg>report.maxJointStepDeg){report.maxJointStepDeg=deg;report.maxJointStepBone=b;report.maxJointStepAt=t;}});
    if(tip&&hadTip){const step=tip.distanceTo(prevTip);if(step>report.maxTipStep){report.maxTipStep=step;report.maxTipStepAt=t;}}
    const bodyStep=body.distanceTo(prevBody);if(bodyStep>report.maxBodyStep){report.maxBodyStep=bodyStep;report.maxBodyStepAt=t;}
    for(let f=0;f<2;f++){const a=ankles[f];if(a.y-root.y<plantY&&prevAnkles[f].y-root.y<plantY)speeds[f].push(Math.hypot(a.x-prevAnkles[f].x,a.z-prevAnkles[f].z)*hz);}
   }
   for(let f=0;f<2;f++){const a=ankles[f],foot=f?report.feet.right:report.feet.left;foot.minY=Math.min(foot.minY,a.y-root.y);
    if(a.y-root.y<plantY){foot.plantedFrames++;if(!touchdown[f])touchdown[f]=a.clone();foot.maxSlip=Math.max(foot.maxSlip,Math.hypot(a.x-touchdown[f]!.x,a.z-touchdown[f]!.z));}else touchdown[f]=undefined;}
   if(tip)report.minTipY=Math.min(report.minTipY,tip.y);
   report.frames++;measured=true;if(s.keepFrames)samples.push({t,root:root.clone(),ankles,tip:tip?.clone(),body});
  }
  PROBE_BONES.forEach((_,k)=>prevQ[k].copy(scratchQ[k]));prevAnkles[0].copy(ankles[0]);prevAnkles[1].copy(ankles[1]);if(tip)prevTip.copy(tip);hadTip=!!tip;prevBody.copy(body);prevT=t;
 }
 for(let f=0;f<2;f++){const foot=f?report.feet.right:report.feet.left;foot.maxPlantedSpeed=speeds[f].length?Math.max(...speeds[f]):0;foot.medianPlantedSpeed=median(speeds[f]);}
 report.maxFootSlip=Math.max(report.feet.left.maxSlip,report.feet.right.maxSlip);report.minAnkleY=Math.min(report.feet.left.minY,report.feet.right.minY);
 report.maxJointSpeedDegPerSec=report.maxJointStepDeg*hz;report.maxTipSpeed=report.maxTipStep*hz;report.maxBodySpeed=report.maxBodyStep*hz;
 if(s.keepFrames)report.samples=samples;
 return report;
}
/** Runs the same scenario at several rates (default 30/60/120 Hz), each on a fresh rig. */
export function probeRates(s:Omit<ProbeScenario,'hz'|'rig'>,rates:readonly number[]=PROBE_RATES):Record<number,ProbeReport>{
 return Object.fromEntries(rates.map(hz=>[hz,probeMotion({...s,hz})]));
}
/** One-line summary for assertion messages and ad-hoc logs. */
export function describeProbe(r:ProbeReport){
 const f=(x:number,d=3)=>Number.isFinite(x)?x.toFixed(d):String(x);
 return `${r.hz}Hz frames=${r.frames} joint=${f(r.maxJointStepDeg,1)}deg(${r.maxJointStepBone}@${f(r.maxJointStepAt)}) slip=${f(r.maxFootSlip)}m plantedSpeed=${f(Math.max(r.feet.left.maxPlantedSpeed,r.feet.right.maxPlantedSpeed),2)}m/s minAnkleY=${f(r.minAnkleY)} tip=${f(r.maxTipStep)}m(@${f(r.maxTipStepAt)}) minTipY=${f(r.minTipY)} body=${f(r.maxBodyStep)}m(@${f(r.maxBodyStepAt)})`;
}
