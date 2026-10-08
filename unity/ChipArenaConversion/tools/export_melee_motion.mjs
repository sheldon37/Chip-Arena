// Captured-melee animation parity fixtures (presentation only). Runs the pinned original character animation (locomotion,
// captured melee layers, footwork, blade clearance, hit reactions, ground pass, transitions, floor guard) over scripted
// timelines for the four captured weapons and records every frame's input view and the posed joints.
// Usage: node tools/export_melee_motion.mjs <pinned source> <temporary output>  (the temporary output directory must
// resolve `three` r180: link a node_modules containing it into it). Writes Tests/melee-motion-parity.json.
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath,pathToFileURL} from 'node:url';import {stripTypeScriptTypes} from 'node:module';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]),temp=path.resolve(process.argv[3]);
const inventory=JSON.parse(fs.readFileSync(path.join(root,'tools/conversion-inventory.json')));
fs.mkdirSync(temp,{recursive:true});fs.writeFileSync(path.join(temp,'package.json'),'{"type":"module"}');
for(const f of inventory.files.filter(f=>f.path.startsWith('src/')&&f.path.endsWith('.ts'))){const input=path.join(source,f.path);if(!fs.existsSync(input))continue;const b=fs.readFileSync(input);if(createHash('sha1').update(`blob ${b.length}\0`).update(b).digest('hex')!==f.gitBlob)throw Error('Source mismatch '+f.path);const out=path.join(temp,f.path.replace(/\.ts$/,'.js'));fs.mkdirSync(path.dirname(out),{recursive:true});let code=b.toString().replace(/import\s*\{([^}]+)\}\s*from\s*(['"])([^'"]+)\2/g,(whole,names,quote,module)=>{if(!module.startsWith('.'))return whole;let target=path.resolve(path.dirname(input),module.replace(/\.js$/,'.ts'));if(!fs.existsSync(target))return whole;let text=fs.readFileSync(target,'utf8'),kept=names.split(',').filter(n=>!new RegExp('(?:export\\s+)?(?:interface|type)\\s+'+n.trim().split(/\s+as\s+/)[0]+'\\b').test(text));return kept.length?'import {'+kept.join(',')+'} from '+quote+module+quote:'import '+quote+module+quote;});fs.writeFileSync(out,stripTypeScriptTypes(code,{mode:'transform'}));}
const load=async p=>import(pathToFileURL(path.join(temp,'src',p+'.js')));
const T=await import(pathToFileURL(path.join(temp,'node_modules/three/build/three.module.js')));
const {createCharacterRig}=await load('client/character-rig'),{buildWeapon}=await load('client/weapon-visuals'),{animateCharacter}=await load('client/character-animation');
const {presentedYaw}=await load('client/motion-quality'),{recordMeleeHit,recordBlockHit,clearBlockImpacts}=await load('client/block-impacts');
const {attackCommitment,attackTravel,meleeRecovery}=await load('shared/souls-combat'),{hitReaction,parryReaction}=await load('shared/hit-reaction'),{WEAPONS}=await load('shared/content');
const BONES=['body','pelvis','spine','chest','neck','head','leftShoulder','leftArm','leftElbow','leftHand','rightShoulder','rightArm','rightElbow','rightHand','leftLeg','leftKnee','leftFoot','leftToe','rightLeg','rightKnee','rightFoot','rightToe'];
const r5=n=>Math.round(n*1e4)/1e4,DT=1/60,RECORD=+(process.env.MELEE_RECORD??4);
const forward=y=>({x:Math.sin(y),z:Math.cos(y)});
export const frameTime=f=>1+f/60+.0013*((f*.618034)%1);
// One scripted timeline: `steps` are [time, action] pairs applied at the first frame at or after their time.
function run(spec,name,duration,steps,opts={}){
 clearBlockImpacts();const rig=createCharacterRig('#70e1d0','#e5e7eb',true);buildWeapon(rig.weapon,spec);rig.offWeapon.clear();rig.offWeapon.visible=spec==='daggers';if(spec==='daggers')buildWeapon(rig.offWeapon,spec,true);
 const w=WEAPONS[spec],rate=opts.rate??1,id='pilot-'+spec;
 const s={yaw:opts.yaw??.3,pos:{x:0,y:.865,z:0},speed:0,dir:0,vy:0,grounded:true,sprinting:false,lastAttack:-10,attackIndex:0,swingAt:undefined,swingUntil:undefined,commit:undefined,followups:0,remaining:0,
  hitStunUntil:0,hitReaction:undefined,blockUntil:0,lastBlock:-10,hurricane:!!opts.hurricane};
 const frames=[],pending=[...steps].sort((a,b)=>a[0]-b[0]);let maxWeapon=0;
 const startAttack=(t,followup=false)=>{
  const windup=followup?.08/rate:w.windup/rate,window=.16/rate;s.attackIndex++;s.lastAttack=t;s.swingAt=t+windup;s.swingUntil=t+windup+window;
  const cooldown=followup?s.commit.until-t:Math.max(w.interval/rate,(w.windup+.16+s.followups*(.08+.16)+.12)/rate);
  s.commit={...attackCommitment(spec,t,windup,window,rate,cooldown,followup?s.remaining-1:0),yaw:s.yaw};if(followup){s.commit.followup=true;s.remaining--;}else s.remaining=s.followups;};
 for(let f=0;f*DT<=duration+1e-9;f++){
  // Frame times carry a small deterministic jitter (a display clock never ticks exactly 1/60 s), so no threshold lands
  // exactly on a frame where double and float arithmetic could round to different sides.
  const t=frameTime(f),dt=f?t-frameTime(f-1):DT;const events=[];
  // Actions begin a little before the frame that shows them (as an authoritative tick does), so no window edge lands
  // exactly on a frame, where double and float timestamps could round to different sides.
  while(pending.length&&pending[0][0]<=t-1+1e-9){const [,a]=pending.shift();const at=t-.0041;
   if(a.move){s.speed=a.move[0];s.dir=a.move[1]??0;}if(a.yaw!==undefined)s.yaw=a.yaw;if(a.sprint!==undefined)s.sprinting=a.sprint;
   if(a.attack){s.followups=a.followups??0;startAttack(at);}
   if(a.block){s.blockUntil=at+.3;s.lastBlock=at;}
   if(a.blockHit){recordBlockHit({type:'blockHit',time:at,pos:{x:s.pos.x,y:s.pos.y,z:s.pos.z+.5},source:id,target:'foe'},t);events.push(['block',at]);}
   if(a.landed){recordMeleeHit({type:'hit',time:at,source:id});events.push(['hit',at]);}
   if(a.hit){const h=a.hit,dir={x:Math.sin(h.from),y:.1,z:Math.cos(h.from)},point={x:s.pos.x+(h.px??0),y:s.pos.y+(h.py??.1),z:s.pos.z+(h.pz??0)};
    const facing=s.hitReaction&&s.hitStunUntil>t?s.hitReaction.yaw:s.commit&&s.commit.until>t?s.commit.yaw:s.yaw;
    if(h.stun){s.hitStunUntil=at+h.stun;s.commit=undefined;s.swingAt=s.swingUntil=undefined;s.lastAttack=-10;s.blockUntil=0;}
    s.hitReaction=hitReaction(s.pos,facing,dir,point,at,h.damage??30,!!h.head);s.hitReaction.until=Math.max(s.hitReaction.until,s.hitStunUntil);}
   if(a.parry){s.hitStunUntil=at+a.parry;s.hitReaction=parryReaction(s.pos,s.yaw,{x:-Math.sin(s.yaw),y:0,z:-Math.cos(s.yaw)},at,s.hitStunUntil);s.commit=undefined;s.swingAt=s.swingUntil=undefined;s.lastAttack=-10;}
   if(a.jump){s.grounded=false;s.vy=8.5;}
  }
  // Echo Strike follow-ups start as the previous swing's hit window closes (simulation melee()).
  if(s.remaining>0&&s.swingUntil!==undefined&&t>s.swingUntil)startAttack(t-.0041,true);
  const committed=s.commit&&t<s.commit.until,stunned=s.hitStunUntil>t;
  let vx=0,vz=0;
  if(committed){const travel=(attackTravel(s.commit,t+DT)-attackTravel(s.commit,t))/DT,fw=forward(s.commit.yaw);vx=fw.x*travel;vz=fw.z*travel;}
  else if(!stunned&&s.speed>0){const fw=forward(s.yaw+s.dir);vx=fw.x*s.speed;vz=fw.z*s.speed;}
  if(!s.grounded){s.vy-=23*DT;if(s.pos.y+s.vy*DT<=.865){s.pos.y=.865;s.vy=0;s.grounded=true;}}
  const p={id,spec,color:'#70e1d0',hp:100,pos:{...s.pos},vel:{x:vx,y:s.vy,z:vz},yaw:s.yaw,pitch:0,grounded:s.grounded,dashTime:0,sprinting:s.sprinting,lastAttack:s.lastAttack,windup:w.windup/rate,swingAt:s.swingAt,swingUntil:s.swingUntil,attackRate:rate,
   attackIndex:s.attackIndex,attackCommitment:s.commit,chips:s.hurricane?{hurricane:1}:{},guarding:false,blockTime:Math.max(0,s.blockUntil-t),lastBlock:s.lastBlock,hitStunUntil:s.hitStunUntil,hitReaction:s.hitReaction,reach:w.range,stats:{},reloadTime:0,weaponAway:false};
  rig.group.position.set(s.pos.x,s.pos.y,s.pos.z);const yaw=presentedYaw(rig,p,t,p.yaw);rig.group.rotation.y=yaw;
  animateCharacter(rig,p,t,dt);
  for(const o of [rig.weapon,rig.offWeapon])if(o.visible!==false||o===rig.weapon)maxWeapon=Math.max(maxWeapon,o.position.clone().sub(new T.Vector3(0,.065,-.055)).length(),o.quaternion.angleTo(new T.Quaternion()));
  const view={gx:s.pos.x,gz:s.pos.z,yaw:p.yaw,vx,vy:s.vy,vz,g:s.grounded?1:0,sp:s.sprinting?1:0,la:s.lastAttack,ai:s.attackIndex,sa:s.swingAt??null,su:s.swingUntil??null,
   bt:p.blockTime,lb:s.lastBlock,hs:s.hitStunUntil,ev:events};
  if(s.commit)Object.assign(view,{ca:s.commit.at,cu:s.commit.until,cy:s.commit.yaw,sta:s.commit.stepAt,stu:s.commit.stepUntil,sd:s.commit.distance,fu:s.commit.followup?1:0});
  if(s.hitReaction){const h=s.hitReaction;Object.assign(view,{rk:h.kind==='parry'?1:0,ra:h.at,ru:h.until,ry:h.yaw,rdx:h.direction.x,rdy:h.direction.y,rdz:h.direction.z,rpx:h.point.x,rpy:h.point.y,rpz:h.point.z,rz:['torso','head','legs','left-arm','right-arm'].indexOf(h.zone),rt:h.turn,rs:h.strength});}
  for(const k of Object.keys(view))if(typeof view[k]==='number')view[k]=+view[k].toPrecision(8);
  // Delta-encode: a frame lists only the inputs that changed since the previous frame (readers carry the rest forward).
  if(!s.cleared){s.cleared=new Set();}if(!s.prev)s.prev={};const delta={};
  for(const k of ['gx','gz','yaw','vx','vy','vz','g','sp','la','ai','sa','su','bt','lb','hs','ca','cu','cy','sta','stu','sd','fu','rk','ra','ru','ry','rdx','rdy','rdz','rpx','rpy','rpz','rz','rt','rs']){const v=k in view?view[k]:null;if(s.prev[k]!==v){delta[k]=v;s.prev[k]=v;}}
  if(events.length)delta.ev=events;
  if(f%RECORD===0){rig.group.updateMatrixWorld(true);const inv=new T.Matrix4().copy(rig.group.matrixWorld).invert(),pts=[];
   const push=(o,local)=>{const v=(local?local.clone():new T.Vector3()).applyMatrix4(o.matrixWorld).applyMatrix4(inv);pts.push(r5(v.x),r5(v.y),r5(v.z));};
   for(const b of BONES)push(rig[b]);push(rig.rightHand,new T.Vector3(0,0,1));push(rig.leftHand,new T.Vector3(0,0,1));push(rig.head,new T.Vector3(0,.3,0));push(rig.rightHand,new T.Vector3(0,.2,0));push(rig.leftHand,new T.Vector3(0,.2,0));
   push(rig.leftToe,new T.Vector3(0,0,.1));push(rig.rightToe,new T.Vector3(0,0,.1));
   delta.out={yaw:r5(yaw),p:pts};}
  frames.push(delta);
  if(!committed){s.pos.x+=vx*DT;s.pos.z+=vz*DT;}else{s.pos.x+=vx*DT;s.pos.z+=vz*DT;}
  if(!s.grounded)s.pos.y+=s.vy*DT;
 }
 return {spec,name,rate,windup:w.windup/rate,hurricane:!!opts.hurricane,weaponDeviation:r5(maxWeapon),frames};
}
const scenarios=[];
for(const spec of ['sword','daggers','shield','greatsword']){
 const cuts=spec==='daggers'?4:spec==='sword'?3:2,interval=Math.max(WEAPONS[spec].interval,WEAPONS[spec].windup+.16+meleeRecovery(spec))+.12;
 scenarios.push(run(spec,'stand-walk-run-stop',3.2,[[.4,{move:[2.8]}],[1.1,{move:[6.8],sprint:true}],[1.9,{yaw:.9}],[2.2,{move:[0],sprint:false}]]));
 scenarios.push(run(spec,'combo',cuts*interval+1,Array.from({length:cuts},(_,i)=>[.2+i*interval,{attack:true}]).concat([[.2+WEAPONS[spec].windup+.02,{landed:true}]])));
 scenarios.push(run(spec,'run-attack-block',2.6,[[.1,{move:[6.8]}],[.7,{attack:true}],[.7+interval,{block:true,move:[0]}],[.7+interval+.12,{blockHit:true}]]));
 scenarios.push(run(spec,'hits',2.4,[[.3,{hit:{from:Math.PI*.9,stun:.42,damage:30}}],[1.1,{hit:{from:-1.2,damage:12,px:.3}}],[1.5,{parry:.6}]]));
 scenarios.push(run(spec,'strafe-jump',2.2,[[.2,{move:[4,1.4]}],[.8,{jump:true}],[1.6,{move:[3,Math.PI]}],[2,{move:[0]}]]));
}
scenarios.push(run('sword','echo-hurricane',2.4,[[.2,{attack:true,followups:2}],[1.4,{attack:true}]],{hurricane:true}));
scenarios.push(run('greatsword','fast-rate',2.2,[[.2,{attack:true}],[.9,{attack:true}]],{rate:1.6}));
const only=process.env.MELEE_ONLY;if(only)scenarios.splice(0,scenarios.length,...scenarios.filter(s=>s.spec+':'+s.name===only));
fs.writeFileSync(process.env.MELEE_OUT??path.join(root,'Tests/melee-motion-parity.json'),JSON.stringify({source:inventory.sourceCommit,dt:DT,record:RECORD,points:BONES.length+7,scenarios}));
console.log('melee motion fixtures',scenarios.length,'scenarios',scenarios.reduce((n,s)=>n+s.frames.length,0),'frames; max weapon-grip deviation',Math.max(...scenarios.map(s=>s.weaponDeviation)));
