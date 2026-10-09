// Magic gait parity fixtures (presentation only). Runs the pinned original character animation (locomotion in the magic
// stance, sprint lean, magicLayer with its staff carry, cast phrase and stride sway, ground pass) for magic pilots walking,
// running, sprinting, turning, strafing, jumping and casting on the move, and records every few frames' joints.
// Usage: node tools/export_magic_motion.mjs <pinned source> <temporary output>  (the temporary output directory must
// resolve `three` r180: link a node_modules containing it into it). Writes Tests/magic-motion-parity.json.
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath,pathToFileURL} from 'node:url';import {stripTypeScriptTypes} from 'node:module';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]),temp=path.resolve(process.argv[3]);
const inventory=JSON.parse(fs.readFileSync(path.join(root,'tools/conversion-inventory.json')));
fs.mkdirSync(temp,{recursive:true});fs.writeFileSync(path.join(temp,'package.json'),'{"type":"module"}');
for(const f of inventory.files.filter(f=>f.path.startsWith('src/')&&f.path.endsWith('.ts'))){const input=path.join(source,f.path);if(!fs.existsSync(input))continue;const b=fs.readFileSync(input);if(createHash('sha1').update(`blob ${b.length}\0`).update(b).digest('hex')!==f.gitBlob)throw Error('Source mismatch '+f.path);const out=path.join(temp,f.path.replace(/\.ts$/,'.js'));fs.mkdirSync(path.dirname(out),{recursive:true});let code=b.toString().replace(/import\s*\{([^}]+)\}\s*from\s*(['"])([^'"]+)\2/g,(whole,names,quote,module)=>{if(!module.startsWith('.'))return whole;let target=path.resolve(path.dirname(input),module.replace(/\.js$/,'.ts'));if(!fs.existsSync(target))return whole;let text=fs.readFileSync(target,'utf8'),kept=names.split(',').filter(n=>!new RegExp('(?:export\\s+)?(?:interface|type)\\s+'+n.trim().split(/\s+as\s+/)[0]+'\\b').test(text));return kept.length?'import {'+kept.join(',')+'} from '+quote+module+quote:'import '+quote+module+quote;});fs.writeFileSync(out,stripTypeScriptTypes(code,{mode:'transform'}));}
const load=async p=>import(pathToFileURL(path.join(temp,'src',p+'.js')));
const T=await import(pathToFileURL(path.join(temp,'node_modules/three/build/three.module.js')));
const {createCharacterRig}=await load('client/character-rig'),{buildWeapon}=await load('client/weapon-visuals'),{animateCharacter}=await load('client/character-animation');
const {presentedYaw}=await load('client/motion-quality'),{WEAPONS,castWindup}=await load('shared/content');
const BONES=['body','pelvis','spine','chest','neck','head','leftShoulder','leftArm','leftElbow','leftHand','rightShoulder','rightArm','rightElbow','rightHand','leftLeg','leftKnee','leftFoot','leftToe','rightLeg','rightKnee','rightFoot','rightToe'];
const r5=n=>Math.round(n*1e4)/1e4,DT=1/60,RECORD=4;
const forward=y=>({x:Math.sin(y),z:Math.cos(y)});
const frameTime=f=>1+f/60+.0013*((f*.618034)%1);
// One scripted timeline: `steps` are [time, action] pairs applied at the first frame at or after their time. A cast's
// projectile leaves at its authoritative release (lastShot = lastAttack + castWindup), as the simulation stamps it.
function run(spec,name,duration,steps){
 const rig=createCharacterRig('#70e1d0','#e5e7eb',true);buildWeapon(rig.weapon,spec);rig.offWeapon.clear();rig.offWeapon.visible=false;
 // The pilot id seeds the living idle's phase; the port reads the native actor id (7) as its string.
 const id='7',s={yaw:.3,pos:{x:0,y:.865,z:0},speed:0,dir:0,vy:0,grounded:true,sprinting:false,lastAttack:-10,lastShot:-10,pending:false};
 const frames=[],pending=[...steps].sort((a,b)=>a[0]-b[0]);
 for(let f=0;f*DT<=duration+1e-9;f++){
  const t=frameTime(f),dt=f?t-frameTime(f-1):DT;
  while(pending.length&&pending[0][0]<=t-1+1e-9){const [,a]=pending.shift();const at=t-.0041;
   if(a.move){s.speed=a.move[0];s.dir=a.move[1]??0;}if(a.yaw!==undefined)s.yaw=a.yaw;if(a.sprint!==undefined)s.sprinting=a.sprint;
   if(a.cast){s.lastAttack=at;s.pending=true;}
   if(a.jump){s.grounded=false;s.vy=8.5;}
  }
  if(s.pending&&t>=s.lastAttack+castWindup(spec)){s.lastShot=s.lastAttack+castWindup(spec);s.pending=false;}
  let vx=0,vz=0;if(s.speed>0){const fw=forward(s.yaw+s.dir);vx=fw.x*s.speed;vz=fw.z*s.speed;}
  if(!s.grounded){s.vy-=23*DT;if(s.pos.y+s.vy*DT<=.865){s.pos.y=.865;s.vy=0;s.grounded=true;}}
  const p={id,spec,color:'#70e1d0',hp:100,pos:{...s.pos},vel:{x:vx,y:s.vy,z:vz},yaw:s.yaw,pitch:0,grounded:s.grounded,dashTime:0,sprinting:s.sprinting,lastAttack:s.lastAttack,lastShot:s.lastShot,windup:0,attackRate:1,
   attackIndex:0,chips:{},guarding:false,blockTime:0,hitStunUntil:0,reach:WEAPONS[spec].range,stats:{},reloadTime:0,weaponAway:false};
  rig.group.position.set(s.pos.x,s.pos.y,s.pos.z);const yaw=presentedYaw(rig,p,t,p.yaw);rig.group.rotation.y=yaw;
  animateCharacter(rig,p,t,dt);
  // Inputs per frame: group x/z, yaw, velocity, grounded, sprinting, lastAttack, lastShot.
  const frame={i:[s.pos.x,s.pos.z,p.yaw,vx,s.vy,vz,s.grounded?1:0,s.sprinting?1:0,s.lastAttack,s.lastShot].map(v=>+v.toPrecision(8))};
  if(f%RECORD===0){rig.group.updateMatrixWorld(true);const inv=new T.Matrix4().copy(rig.group.matrixWorld).invert(),pts=[],q=new T.Quaternion(),g=new T.Quaternion();
   const push=(o,local)=>{const v=(local?local.clone():new T.Vector3()).applyMatrix4(o.matrixWorld).applyMatrix4(inv);pts.push(r5(v.x),r5(v.y),r5(v.z));};
   for(const b of BONES)push(rig[b]);push(rig.rightHand,new T.Vector3(0,0,1));push(rig.head,new T.Vector3(0,.3,0));push(rig.rightHand,new T.Vector3(0,.2,0));
   push(rig.leftToe,new T.Vector3(0,0,.1));push(rig.rightToe,new T.Vector3(0,0,.1));
   // The staff's frame in the rig group (the port holds it on the wrist: compare its shaft and face axes).
   rig.weapon.getWorldQuaternion(q).premultiply(rig.group.getWorldQuaternion(g).invert());
   for(const axis of [new T.Vector3(0,0,1),new T.Vector3(0,1,0)]){const v=axis.applyQuaternion(q);pts.push(r5(v.x),r5(v.y),r5(v.z));}
   frame.out={yaw:r5(yaw),p:pts};}
  frames.push(frame);
  s.pos.x+=vx*DT;s.pos.z+=vz*DT;if(!s.grounded)s.pos.y+=s.vy*DT;
 }
 return {spec,name,frames};
}
const scenarios=[
 run('fire','stand-walk-run-sprint-turn-stop',3.2,[[.4,{move:[2.8]}],[1.1,{move:[6.8],sprint:true}],[1.9,{yaw:.9}],[2.2,{move:[0],sprint:false}]]),
 run('lightning','walk-cast-streak',2.4,[[.2,{move:[2.8]}],[.6,{cast:true}],[.88,{cast:true}],[1.16,{cast:true}],[2,{move:[0]}]]),
 run('water','strafe-jump',2.2,[[.2,{move:[4,1.4]}],[.8,{jump:true}],[1.6,{move:[3,Math.PI]}],[2,{move:[0]}]]),
 run('darkness','run-cast',2.4,[[.1,{move:[6.8]}],[.7,{cast:true}],[1.5,{cast:true}],[2.1,{move:[0]}]]),
];
fs.writeFileSync(process.env.MAGIC_OUT??path.join(root,'Tests/magic-motion-parity.json'),JSON.stringify({source:inventory.sourceCommit,dt:DT,record:RECORD,points:BONES.length+5+2,scenarios}));
console.log('magic motion fixtures',scenarios.length,'scenarios',scenarios.reduce((n,s)=>n+s.frames.length,0),'frames');
