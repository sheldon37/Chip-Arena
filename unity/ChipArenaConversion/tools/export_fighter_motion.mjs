// Emperor wavedash parity fixtures (presentation only). Runs the pinned original character animation (locomotion in the
// Emperor stance with the dash flag, emperorLayers' wavedash crouch and its per-foot edits through applyFighterLegs, the
// ground pass and finishPose) for an Emperor wavedashing from a stand, chaining wavedashes on the run, and cancelling a
// wavedash into the Electric Hook (Q), and records every other frame's joints.
// Usage: node tools/export_fighter_motion.mjs <pinned source> <temporary output>  (the temporary output directory must
// resolve `three` r180: link a node_modules containing it into it). Writes Tests/fighter-motion-parity.json; with
// FIGHTER_DUMP=<file> it also writes every frame's local joint transforms (offline renders).
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath,pathToFileURL} from 'node:url';import {stripTypeScriptTypes} from 'node:module';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]),temp=path.resolve(process.argv[3]);
const inventory=JSON.parse(fs.readFileSync(path.join(root,'tools/conversion-inventory.json')));
fs.mkdirSync(temp,{recursive:true});fs.writeFileSync(path.join(temp,'package.json'),'{"type":"module"}');
for(const f of inventory.files.filter(f=>f.path.startsWith('src/')&&f.path.endsWith('.ts'))){const input=path.join(source,f.path);if(!fs.existsSync(input))continue;const b=fs.readFileSync(input);if(createHash('sha1').update(`blob ${b.length}\0`).update(b).digest('hex')!==f.gitBlob)throw Error('Source mismatch '+f.path);const out=path.join(temp,f.path.replace(/\.ts$/,'.js'));fs.mkdirSync(path.dirname(out),{recursive:true});let code=b.toString().replace(/import\s*\{([^}]+)\}\s*from\s*(['"])([^'"]+)\2/g,(whole,names,quote,module)=>{if(!module.startsWith('.'))return whole;let target=path.resolve(path.dirname(input),module.replace(/\.js$/,'.ts'));if(!fs.existsSync(target))return whole;let text=fs.readFileSync(target,'utf8'),kept=names.split(',').filter(n=>!new RegExp('(?:export\\s+)?(?:interface|type)\\s+'+n.trim().split(/\s+as\s+/)[0]+'\\b').test(text));return kept.length?'import {'+kept.join(',')+'} from '+quote+module+quote:'import '+quote+module+quote;});fs.writeFileSync(out,stripTypeScriptTypes(code,{mode:'transform'}));}
const load=async p=>import(pathToFileURL(path.join(temp,'src',p+'.js')));
const T=await import(pathToFileURL(path.join(temp,'node_modules/three/build/three.module.js')));
const {createCharacterRig}=await load('client/character-rig'),{buildWeapon}=await load('client/weapon-visuals'),{animateCharacter}=await load('client/character-animation');
const {presentedYaw}=await load('client/motion-quality'),{EMPEROR}=await load('shared/emperor-content'),{CONFIG:C}=await load('shared/config');
const BONES=['body','pelvis','spine','chest','neck','head','leftShoulder','leftArm','leftElbow','leftHand','rightShoulder','rightArm','rightElbow','rightHand','leftLeg','leftKnee','leftFoot','leftToe','rightLeg','rightKnee','rightFoot','rightToe'];
const r5=n=>Math.round(n*1e4)/1e4,DT=1/60,RECORD=2;
const forward=y=>({x:Math.sin(y),z:Math.cos(y)});
// Timelines start at 10 s: the guard idle clip was baked at 10 + its age and loops every 2 s, so its bounce is in phase here.
const T0=10,frameTime=f=>T0+f/60+.0013*((f*.618034)%1);
// One scripted timeline, stepped as the simulation steps an Emperor (shared/emperor.ts, shared/movement.ts): `steps` are
// [time, action] pairs applied at the first frame at or after their time. E (wave) sets the 'wave' action for
// EMPEROR.waveSeconds with an external push of EMPEROR.waveSpeed along the aim and dashTime .18 (no drag while it lasts);
// Q (electric) sets 'electric' for EMPEROR.electricRecovery with its strike at EMPEROR.electricHit, the grounded electric
// step (electricStepSpeed for electricStepSeconds) and clears waveTime (the wave's dashTime runs out on its own).
function run(name,duration,steps){
 const rig=createCharacterRig('#70e1d0','#e5e7eb',true);buildWeapon(rig.weapon,'emperor');buildWeapon(rig.offWeapon,'emperor',true);
 const id='7',yaw=.3,fw=forward(yaw),s={pos:{x:0,y:.865,z:0},want:0,move:{x:0,z:0},ext:{x:0,z:0},dashTime:0,waveTime:0,stepTime:0,lastAttack:-10,attackIndex:0,emperor:{at:0,until:0,index:0,total:0,singleHit:false,hits:0,lastHit:-10,comboDamage:0,electricAt:-10},strikeAt:-1};
 const frames=[],dump=[],pending=[...steps].sort((a,b)=>a[0]-b[0]);
 for(let f=0;f*DT<=duration+1e-9;f++){
  const t=frameTime(f),dt=f?t-frameTime(f-1):DT,e=s.emperor;
  while(pending.length&&pending[0][0]<=t-T0+1e-9){const [,a]=pending.shift(),at=t-.0041;
   if(a.move!==undefined)s.want=a.move;
   if(a.wave){Object.assign(e,{action:'wave',singleHit:false,at,until:at+EMPEROR.waveSeconds});s.waveTime=.18;s.dashTime=.18;s.ext={x:fw.x*EMPEROR.waveSpeed,z:fw.z*EMPEROR.waveSpeed};}
   if(a.electric){Object.assign(e,{action:'electric',at,until:at+EMPEROR.electricRecovery,index:0,total:1,singleHit:false,electricAt:at});s.lastAttack=at;s.attackIndex=0;s.waveTime=0;s.stepTime=EMPEROR.electricStepSeconds;s.strikeAt=at+EMPEROR.electricHit;}
  }
  if(s.strikeAt>0&&t>=s.strikeAt){s.lastAttack=s.strikeAt;s.attackIndex=1;s.strikeAt=-1;}
  if(e.action&&t>=e.until)e.action=undefined;
  const step=s.stepTime>0?EMPEROR.electricStepSpeed:0,vx=s.move.x+s.ext.x+fw.x*step,vz=s.move.z+s.ext.z+fw.z*step;
  const p={id,spec:'emperor',color:'#70e1d0',hp:100,pos:{...s.pos},vel:{x:vx,y:0,z:vz},yaw,pitch:0,grounded:true,dashTime:s.dashTime,waveTime:s.waveTime,electricStepTime:s.stepTime,sprinting:false,lastAttack:s.lastAttack,lastShot:-10,windup:.075,attackRate:1,
   attackIndex:s.attackIndex,chips:{},guarding:false,blockTime:0,hitStunUntil:0,reach:2.4,stats:{},reloadTime:0,weaponAway:false,emperor:{...e}};
  rig.group.position.set(s.pos.x,s.pos.y,s.pos.z);const shown=presentedYaw(rig,p,t,p.yaw);rig.group.rotation.y=shown;
  animateCharacter(rig,p,t,dt);
  // Inputs per frame: group x/z, yaw, velocity x/z, dashTime, action (0 none, 1 wave, 2 electric), its at/until, lastAttack, attackIndex.
  const frame={i:[s.pos.x,s.pos.z,yaw,vx,vz,s.dashTime,e.action==='wave'?1:e.action==='electric'?2:0,e.at,e.until,s.lastAttack,s.attackIndex].map(v=>+v.toPrecision(8))};
  if(f%RECORD===0){rig.group.updateMatrixWorld(true);const inv=new T.Matrix4().copy(rig.group.matrixWorld).invert(),pts=[];
   const push=(o,local)=>{const v=(local?local.clone():new T.Vector3()).applyMatrix4(o.matrixWorld).applyMatrix4(inv);pts.push(r5(v.x),r5(v.y),r5(v.z));};
   for(const b of BONES)push(rig[b]);push(rig.leftToe,new T.Vector3(0,0,.1));push(rig.rightToe,new T.Vector3(0,0,.1));
   frame.out={yaw:r5(shown),p:pts};}
  if(process.env.FIGHTER_DUMP)dump.push({t,group:[s.pos.x,s.pos.y,s.pos.z,shown],bones:BONES.map(b=>[...rig[b].position.toArray(),...rig[b].quaternion.toArray()].map(r5))});
  frames.push(frame);
  // Movement (shared/movement.ts prepareMotion): the walk input approaches its speed, the push holds while dashTime lasts
  // and then drags, the electric step runs out.
  const approach=(a,b)=>a+Math.max(-C.acceleration*DT,Math.min(C.acceleration*DT,b-a));s.move={x:approach(s.move.x,fw.x*s.want),z:approach(s.move.z,fw.z*s.want)};
  s.pos.x+=vx*DT;s.pos.z+=vz*DT;s.dashTime=Math.max(0,s.dashTime-DT);s.waveTime=Math.max(0,s.waveTime-DT);s.stepTime=Math.max(0,s.stepTime-DT);
  if(s.dashTime<=0){const k=Math.exp(-C.externalDrag*DT);s.ext={x:s.ext.x*k,z:s.ext.z*k};}
 }
 return {name,frames,dump};
}
const scenarios=[
 run('wavedash-standing',1.3,[[.5,{wave:true}]]),
 // Holding E chains a wavedash as each one ends (waveTime <= 0).
 run('wavedash-chain-run',1.8,[[.15,{move:C.speed}],[.6,{wave:true}],[.78,{wave:true}],[.96,{wave:true}],[1.5,{move:0}]]),
 run('wavedash-cancel',1.5,[[.5,{wave:true}],[.58,{electric:true}]]),
 run('wavedash-cancel-run',1.7,[[.15,{move:C.speed}],[.6,{wave:true}],[.7,{electric:true}],[1.4,{move:0}]]),
];
if(process.env.FIGHTER_DUMP)fs.writeFileSync(process.env.FIGHTER_DUMP,JSON.stringify(scenarios.map(s=>({name:s.name,frames:s.dump}))));
fs.writeFileSync(process.env.FIGHTER_OUT??path.join(root,'Tests/fighter-motion-parity.json'),JSON.stringify({source:inventory.sourceCommit,dt:DT,record:RECORD,points:BONES.length+2,scenarios:scenarios.map(({name,frames})=>({name,frames}))}));
console.log('fighter motion fixtures',scenarios.length,'scenarios',scenarios.reduce((n,s)=>n+s.frames.length,0),'frames');
