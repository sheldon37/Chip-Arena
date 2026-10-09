// First-person view-layer parity fixtures (presentation only). Runs the pinned original poseFirstPersonView
// (client/first-person-view.ts) on a camera-attached first-person rig over scripted timelines and records, per frame, the
// inputs it read and the view-layer values it wrote: the pre-alignment viewRoot position minus the class/ADS base (look sway,
// stride bob, landing kick, recoil push, reload dip, run-low, rocket kick), the viewRoot X/Z rotation (recoil pitch, run-low,
// rocket and mark flick, look roll, reload roll), the ADS blend and the rig's locomotion motion/stride.
// Usage: node tools/export_first_person.mjs <pinned source> <temporary output>  (the temporary output directory must
// resolve `three` r180: link a node_modules containing it into it). Writes Tests/first-person-sway-parity.json.
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath,pathToFileURL} from 'node:url';import {stripTypeScriptTypes} from 'node:module';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]),temp=path.resolve(process.argv[3]);
const inventory=JSON.parse(fs.readFileSync(path.join(root,'tools/conversion-inventory.json')));
fs.mkdirSync(temp,{recursive:true});fs.writeFileSync(path.join(temp,'package.json'),'{"type":"module"}');
for(const f of inventory.files.filter(f=>f.path.startsWith('src/')&&f.path.endsWith('.ts'))){const input=path.join(source,f.path);if(!fs.existsSync(input))continue;const b=fs.readFileSync(input);if(createHash('sha1').update(`blob ${b.length}\0`).update(b).digest('hex')!==f.gitBlob)throw Error('Source mismatch '+f.path);const out=path.join(temp,f.path.replace(/\.ts$/,'.js'));fs.mkdirSync(path.dirname(out),{recursive:true});let code=b.toString().replace(/import\s*\{([^}]+)\}\s*from\s*(['"])([^'"]+)\2/g,(whole,names,quote,module)=>{if(!module.startsWith('.'))return whole;let target=path.resolve(path.dirname(input),module.replace(/\.js$/,'.ts'));if(!fs.existsSync(target))return whole;let text=fs.readFileSync(target,'utf8'),kept=names.split(',').filter(n=>!new RegExp('(?:export\\s+)?(?:interface|type)\\s+'+n.trim().split(/\s+as\s+/)[0]+'\\b').test(text));return kept.length?'import {'+kept.join(',')+'} from '+quote+module+quote:'import '+quote+module+quote;});fs.writeFileSync(out,stripTypeScriptTypes(code,{mode:'transform'}));}
const load=async p=>import(pathToFileURL(path.join(temp,'src',p+'.js')));
const T=await import(pathToFileURL(path.join(temp,'node_modules/three/build/three.module.js')));
const {createCharacterRig}=await load('client/character-rig'),{buildWeapon}=await load('client/weapon-visuals'),{markFirstPersonRig}=await load('client/pose-flags');
const {poseFirstPersonView,FP_TUNE,ADS_VIEW}=await load('client/first-person-view'),{WEAPONS}=await load('shared/content'),{adsProfile,castTime}=await load('shared/abilities');
const {attackCommitment}=await load('shared/souls-combat');
const r8=n=>+n.toPrecision(8);
// Frame clocks: a display never ticks exactly 1/hz, so a small deterministic jitter keeps window edges off frames.
const clock=(hz,duration,start=1)=>{const out=[];for(let f=0;f/hz<=duration+1e-9;f++)out.push(start+f/hz+.0013*60/hz*((f*.618034)%1));return out;};
function run(name,spec,times,script){
 const rig=createCharacterRig('#70e1d0',undefined,true);markFirstPersonRig(rig);buildWeapon(rig.weapon,spec);rig.offWeapon.visible=spec==='daggers';if(spec==='daggers')buildWeapon(rig.offWeapon,spec,true);
 const camera=new T.PerspectiveCamera(75,16/9,.08,400),viewRoot=new T.Group();camera.add(viewRoot);viewRoot.add(rig.group);
 // Spy the first position/rotation write of each call: the view layer before the ADS sight alignment.
 let pos=null,rot=null;const ps=viewRoot.position.set.bind(viewRoot.position),rs=viewRoot.rotation.set.bind(viewRoot.rotation);
 viewRoot.position.set=(x,y,z)=>{if(!pos)pos=[x,y,z];return ps(x,y,z);};viewRoot.rotation.set=(x,y,z,o)=>{if(!rot)rot=[x,y,z];return rs(x,y,z,o);};
 const w=WEAPONS[spec],cls=w.class,fighter=spec==='ansatsuken'||spec==='emperor',P=adsProfile(spec),frames=[];let prev=times[0];
 for(const t of times){
  const s=script(t),dt=Math.max(0,t-prev);prev=t;
  const p={id:s.id??'pilot-'+spec,spec,color:'#70e1d0',hp:100,pos:{x:0,y:.865,z:0},vel:{x:s.vx??0,y:s.vy??0,z:s.vz??0},yaw:s.yaw??0,pitch:s.pitch??0,grounded:s.grounded??true,dashTime:0,sprinting:false,
   lastAttack:s.lastAttack??-10,lastShot:s.lastShot,attackRate:1,attackIndex:0,chips:{},guarding:false,blockTime:0,lastBlock:-10,hitStunUntil:s.stunUntil??0,reach:w.range,stats:{},
   reloadTime:s.reloadUntil!==undefined&&t<s.reloadUntil?s.reloadUntil-t:0,weaponAway:false,lastSecondary:s.abilityAt??-1e9,ammo:10};
  if(s.roll)p.roll={at:s.roll[0],until:s.roll[1],invulnerableAt:s.roll[0]+.075,invulnerableUntil:s.roll[0]+.255,direction:{x:0,y:0,z:1},distance:5.3};
  if(s.commit)p.attackCommitment={...attackCommitment(spec,s.commit,w.windup,.16,1,w.interval,0),yaw:p.yaw};
  pos=rot=null;const a=poseFirstPersonView(rig,viewRoot,p,t,dt,false,s.aim?P:null);
  const o=ADS_VIEW[spec]??[0,0],baseX=fighter?0:cls==='magic'?FP_TUNE.magicX:cls==='melee'?FP_TUNE.meleeX:.24+o[0]*a,baseY=fighter?-.48:cls==='melee'?FP_TUNE.meleeY:cls==='magic'?FP_TUNE.magicY:-.48+o[1]*a,baseZ=cls==='melee'&&!fighter?FP_TUNE.meleeZ:-.2;
  if(Math.abs(rot[1]-Math.PI)>1e-12)throw Error('Unexpected view yaw');
  const committed=!!p.attackCommitment&&t<p.attackCommitment.until,planted=committed||p.hitStunUntil>t||!!p.roll&&t>=p.roll.at&&t<p.roll.until;
  frames.push({t:r8(t),pid:p.id==='pilot-'+spec?1:2,yaw:r8(p.yaw),pitch:r8(p.pitch),vx:r8(p.vel.x),vy:r8(p.vel.y),vz:r8(p.vel.z),g:p.grounded?1:0,pl:planted?1:0,aim:s.aim?1:0,
   shot:r8(s.lastShot??p.lastAttack),ra:s.reloadAt??-100,ru:s.reloadUntil??0,ab:s.ability??null,aa:s.abilityAt??-100,ar:s.ability?r8(s.abilityAt+castTime(s.windup,{attackRate:1})):0,
   out:{a:r8(a),dx:r8(pos[0]-baseX),dy:r8(pos[1]-baseY),dz:r8(pos[2]-baseZ),rx:r8(rot[0]),rz:r8(rot[2]),motion:r8(rig.motion),stride:r8(rig.stride)}});
 }
 return {name,spec,cls:fighter?'fighter':cls,kick:P?.kick??1,frames};
}
// Scripted inputs: yaw/pitch in the source's radians (yaw rises turning left), world velocity, grounded, actions.
const fwd=(yaw,speed)=>({vx:Math.sin(yaw)*speed,vz:Math.cos(yaw)*speed});
const jumper=(t,starts,v0)=>{for(const [s,v] of starts.map((s,i)=>[s,v0[i]])){const u=t-s,land=2*v/23;if(u>=0&&u<land)return {grounded:false,vy:v-23*u};}return {grounded:true,vy:0};};
const fall=(t,at,depth)=>{const u=t-at,dur=Math.sqrt(2*depth/23);return u>=0&&u<dur?{grounded:false,vy:-23*u}:{grounded:true,vy:0};};
const lastOf=(t,list,fallback=-100)=>{let v=fallback;for(const x of list)if(x<=t)v=x;return v;};
const scenarios=[
 run('pistol look, shots and ADS at 60 Hz','pistol',clock(60,4),t=>{const u=t-1;return {yaw:.9*Math.sin(2.1*u)+.4*Math.sin(5.3*u),pitch:.25*Math.sin(1.7*u),aim:u>=2&&u<3,lastShot:lastOf(u,[.5,1,1.08,2.3,2.6,3.45],-101)+1};}),
 run('rifle run, reload, hitstun and roll at 144 Hz','rifle',clock(144,4.4),t=>{const u=t-1,yaw=.3+.25*u,dir=u<1.8?0:u<2.6?Math.PI:Math.PI/2,speed=u<.3?0:u<1.2?6.8*Math.min(1,(u-.3)/.5):u<1.6?9:u<3.9?4:0,v=fwd(yaw+dir,speed);
  return {yaw,pitch:-.1,...v,lastShot:lastOf(u,[1,1.9,3.95],-101)+1,reloadAt:u>=2.2?3.2:undefined,reloadUntil:u>=2.2?3.2+WEAPONS.rifle.reload:undefined,stunUntil:u>=3&&u<3.3?4.3:0,roll:u>=3.6&&u<4.2?[4.6,5.06]:undefined};}),
 run('machine gun jumps, falls, ADS fire and rocket at 30 Hz','machinegun',clock(30,5.2),t=>{const u=t-1,j=u<1.9?jumper(u,[.5],[8.5]):fall(u,2,4),v=fwd(-.4,u>=1&&u<2.8?5.5:0);
  return {yaw:-.4+.1*Math.sin(u*3),pitch:.05,...v,...j,aim:u>=3&&u<3.8,lastShot:u>=3&&u<3.6?1+3+Math.floor((u-3)/.1)*.1:lastOf(u,[1.2],-101)+1,ability:u>=4?'rocket':undefined,abilityAt:u>=4?5:undefined,windup:.12};}),
 run('rifle mark, scope and fast turns at 240 Hz','rifle',clock(240,3),t=>{const u=t-1;return {yaw:u<1?6*u:6-9*(u-1),pitch:u<1.5?.6*Math.sin(4*u):-.3,aim:u>=1.2&&u<2.2,lastShot:lastOf(u,[1.5,2.4],-101)+1,ability:u>=.5?'orbital-mark':undefined,abilityAt:u>=.5?1.5:undefined,windup:.15};}),
 run('fire staff walk and look at 60 Hz','fire',clock(60,3),t=>{const u=t-1,v=fwd(1.2,u>.4&&u<2.4?3.5:0);return {yaw:1.2+.5*Math.sin(3*u),pitch:.2*Math.cos(2*u),...v,lastAttack:lastOf(u,[.8,1.6],-11)+1,lastShot:lastOf(u,[.96,1.76],-11)+1};}),
 run('sword sprint, commit and landing at 90 Hz','sword',clock(90,3.4),t=>{const u=t-1,j=jumper(u,[2.2],[8.5]),v=fwd(-2,u<2.9?7.2:0);return {yaw:-2+.3*Math.sin(2*u),pitch:0,...v,...j,commit:u>=1.2&&u<1.9?2.2:undefined,lastAttack:u>=1.2?2.2:-10};}),
 // Another pilot on the same view root (spectating, possession): the rig's gait restarts, the view springs carry on.
 run('pilot change while running at 60 Hz','sword',clock(60,2.4),t=>{const u=t-1;return {id:u>=1.2?'pilot-other':undefined,yaw:.4*Math.sin(2*u),pitch:0,...fwd(u<1.2?0:Math.PI,6.5)};}),
 // Clock edges: a respawn-sized gap, a rewind, a small backward step and a pause that holds the presentation clock.
 run('pistol clock gaps, rewind and pause','pistol',[...clock(60,1),...clock(60,.5,2.6),...clock(60,.3,2.9),...clock(60,.3,3.15),...Array(20).fill(3.46),...clock(60,.5,3.46)],t=>{const u=t-1;return {yaw:2*Math.sin(1.3*u),pitch:.3*Math.sin(.7*u),...fwd(0,u>.2?4:0),lastShot:lastOf(u,[.4,1.7,2.5],-101)+1};}),
];
fs.writeFileSync(path.join(root,'Tests/first-person-sway-parity.json'),JSON.stringify({source:'src/client/first-person-view.ts',scenarios})+'\n');
console.log('Exported',scenarios.length,'first-person scenarios,',scenarios.reduce((n,s)=>n+s.frames.length,0),'frames');
