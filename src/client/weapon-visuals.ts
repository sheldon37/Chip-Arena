import {matrixStaff,animateStaff} from './matrix-effects.js';
import * as T from 'three';
import {spellCharge} from './ranged-vfx.js';
import {WEAPONS,statsFor} from '../shared/content.js';
import {ElementId} from '../shared/elements.js';
import type {PlayerView} from '../shared/simulation.js';
import type {CharacterRig} from './character-rig.js';
const cube=new T.BoxGeometry(1,1,1),sphere=new T.SphereGeometry(1,8,6),flame=new T.ConeGeometry(1,1,5),ring=new T.TorusGeometry(1,.055,4,16);
const gem=new T.OctahedronGeometry(.16),mats=new Map<string,T.Material>();
/** Lambert (or unlit glow) materials. sheen adds a small self-lit term (a fraction of the colour) so edges and steel still read under the night moon. */
function material(color:number,glow=false,sheen=0){const k=color+':'+glow+(sheen?':'+sheen:'');if(!mats.has(k))mats.set(k,glow?new T.MeshBasicMaterial({color}):new T.MeshLambertMaterial({color,emissive:sheen?new T.Color(color).multiplyScalar(sheen):0}));return mats.get(k)!;}
function mesh(parent:T.Object3D,shape:T.BufferGeometry,color:number,x:number,y:number,z:number,sx:number,sy:number,sz:number,glow=false){const m=new T.Mesh(shape,material(color,glow));m.position.set(x,y,z);m.scale.set(sx,sy,sz);m.castShadow=!glow;parent.add(m);return m;}
const box=(p:T.Object3D,c:number,x:number,y:number,z:number,sx:number,sy:number,sz:number,g=false)=>mesh(p,cube,c,x,y,z,sx,sy,sz,g);
/** Small trim: no shadow; optional sheen and rotation (x, y, z). */
function trim(p:T.Object3D,c:number,x:number,y:number,z:number,sx:number,sy:number,sz:number,sheen=0,rx=0,ry=0,rz=0){const m=box(p,c,x,y,z,sx,sy,sz);if(sheen)m.material=material(c,false,sheen);m.castShadow=false;m.rotation.set(rx,ry,rz);return m;}
/** Visual blade lengths. Reach modules and enchantment tracks use the same numbers; meshes never define reach or hit volumes. */
export const BLADE_LENGTH={daggers:.5,sword:.94,greatsword:1.35,shield:.7} as const;
const bladeLength=(spec:string,shield=true)=>spec==='daggers'?BLADE_LENGTH.daggers:spec==='greatsword'?BLADE_LENGTH.greatsword:spec==='shield'&&shield?BLADE_LENGTH.shield:BLADE_LENGTH.sword;
/** Deterministic 0..1 hash of an authoritative timestamp (per-shot flash variation without Math.random). */
const hash=(t:number)=>{const x=Math.sin(t*127.1+311.7)*43758.5453;return x-Math.floor(x);};
function socket(parent:T.Object3D,name:string,x:number,y:number,z:number){const g=new T.Group();g.name=name;g.position.set(x,y,z);parent.add(g);return g;}
export function buildWeapon(weapon:T.Group,spec:string,offhand=false){
 weapon.clear();weapon.userData={spec,enchant:'',modifierKey:''};const w=WEAPONS[spec as keyof typeof WEAPONS];const steel=0x81999f,dark=0x263a43,gold=0xe3b779,edge=0xeaf6f4,black=0x151c21,wrap=0x3b2a24;
 if(spec==='emperor'){box(weapon,0x1b1b25,0,-.01,0,.22,.20,.23);box(weapon,0x62636f,0,.075,-.01,.225,.08,.215);box(weapon,0xef1e43,0,-.04,.12,.22,.05,.025,true);for(let i=0;i<3;i++)box(weapon,0xa9aab3,(i-1)*.07,-.10,.1,.045,.04,.05);socket(weapon,'blade-tip',0,-.045,.14);}
 else if(spec==='ansatsuken'){const fist=socket(weapon,'fighter-fist',0,0,0);box(fist,0x931f31,0,-.015,-.01,.205,.175,.19);box(fist,0xec3c43,0,-.04,.07,.205,.13,.065);box(weapon,0xe8d6ad,0,.06,-.01,.21,.045,.2);const palm=socket(weapon,'fighter-palm',0,0,0);palm.visible=false;box(palm,0xb42638,0,-.01,0,.19,.14,.08);for(let i=0;i<4;i++){const finger=box(palm,0xec3c43,(i-1.5)*.049,-.14+(i===0||i===3?.012:0),0,.039,.15,.066);finger.rotation.z=(i-1.5)*-.045;}const thumb=box(palm,0xec3c43,.125,-.018,.035,.057,.095,.075);thumb.rotation.z=.5;socket(weapon,'blade-tip',0,-.045,.115);}
 else if(spec==='leet'){matrixStaff(weapon);}else if(w.class==='ranged'){
  // Every bore sits on y=0 through the muzzle socket; the hand socket is the pistol grip.
  const rifle=spec==='rifle',mg=spec==='machinegun',tip=rifle?1.18:mg?.87:.47,gunmetal=0x1d2a31,guard=0x5d7079;
  const mag=socket(weapon,'magazine',0,-.19,spec==='pistol'?-.06:mg?.2:.16);
  if(spec==='pistol'){
   // Slide over a short frame, a raked grip that holds the magazine, a trigger guard, sights and a steel muzzle ring.
   box(weapon,steel,0,.035,.14,.125,.10,.40);box(weapon,dark,0,-.045,.12,.11,.06,.30);box(weapon,gunmetal,0,0,.40,.06,.06,.14);trim(weapon,steel,0,0,.465,.08,.08,.03,.18);
   box(weapon,dark,0,-.14,-.045,.11,.22,.12).rotation.x=.22;trim(weapon,guard,0,-.14,-.045,.118,.15,.085,0,.22);trim(weapon,steel,0,-.105,.07,.03,.022,.12);
   trim(weapon,steel,0,.095,.32,.022,.02,.025,.2);trim(weapon,steel,0,.095,-.03,.07,.02,.025,.2);
   trim(mag,steel,0,-.03,-.005,.095,.13,.10,0,.22);socket(weapon,'support-grip',.06,-.10,-.01);
  }else{
   // Receiver, pistol grip and a short dropped stock (butt ~.30 behind the grip, marked 'stock-butt').
   box(weapon,dark,0,mg?.01:0,.09,mg?.17:.15,mg?.22:.16,mg?.44:.40);trim(weapon,steel,0,mg?.13:.09,.09,mg?.15:.10,.025,mg?.42:.40,.12);
   box(weapon,dark,0,-.13,-.035,.11,.22,.11).rotation.x=.2;trim(weapon,steel,0,-.095,.06,.03,.02,.10);
   box(weapon,dark,0,-.03,-.20,.12,.15,.20);box(weapon,black,0,-.045,-.295,.13,.19,.03);socket(weapon,'stock-butt',0,-.04,-.30);socket(weapon,'support-grip',.025,-.03,.10);
   if(rifle){
    // Long barrel with a muzzle brake, handguard, folded bipod, scope with bell and lens, bolt knob, cheek riser.
    box(weapon,gunmetal,0,0,.745,.06,.06,.85);box(weapon,guard,0,-.005,.46,.12,.12,.32);box(weapon,gold,0,0,1.16,.11,.085,.10);trim(weapon,steel,0,-.075,.74,.06,.02,.26);
    box(weapon,dark,0,.175,.12,.085,.085,.36);trim(weapon,steel,0,.12,.12,.035,.03,.24);box(weapon,gold,0,.175,.32,.13,.13,.07);box(weapon,0x86ddd8,0,.175,.357,.09,.09,.012,true);trim(weapon,dark,0,.175,-.075,.10,.10,.05);
    trim(weapon,steel,-.11,.035,-.02,.12,.03,.03,.15);trim(weapon,steel,-.17,.035,-.02,.045,.045,.045,.15);trim(weapon,dark,0,.055,-.19,.08,.04,.14);
    trim(mag,steel,0,0,0,.08,.22,.15,0,-.12);
   }else{
    // Tall receiver with a carry handle, perforated barrel shroud, flash hider, and a drum feeding a short belt.
    box(weapon,gunmetal,0,.01,.48,.11,.11,.40);for(const z of [.40,.52])trim(weapon,black,0,.01,z,.115,.035,.06);box(weapon,gunmetal,0,0,.77,.05,.05,.18);box(weapon,gold,0,0,.865,.10,.10,.07);
    trim(weapon,steel,0,.235,.09,.035,.03,.22);for(const z of [.0,.18])trim(weapon,steel,0,.19,z,.03,.07,.03);
    box(mag,gold,0,0,0,.10,.24,.22);trim(mag,steel,-.01,.12,-.10,.06,.04,.08).rotation.x=-.4;
   }
  }
  const muzzle=socket(weapon,'muzzle',0,0,tip+.04);const flash=mesh(muzzle,flame,0xffdb90,0,0,.14,.11,.30,.11,true);flash.rotation.x=Math.PI/2;flash.name='muzzle-flash';flash.visible=false;
 }else if(w.class==='magic'){
  // One long shaft held a third of the way up ('guard-grip' marks the second hand), a gold collar and the element head around the gem.
  box(weapon,dark,0,0,.13,.065,.065,.94);box(weapon,gold,0,0,-.355,.085,.085,.05);trim(weapon,wrap,0,0,0,.082,.082,.17);trim(weapon,wrap,0,0,.40,.08,.08,.11);socket(weapon,'guard-grip',0,0,.40);
  box(weapon,gold,0,0,.53,.15,.14,.08);mesh(weapon,gem,w.color,0,0,.72,1,1,1,true);
  if(spec==='water'){for(const x of [-.19,.19])box(weapon,steel,x,0,.725,.035,.035,.32);box(weapon,steel,0,0,.885,.415,.035,.035);trim(weapon,w.color,0,0,.57,.26,.03,.03,.3);}
  else if(spec==='lightning'){for(const x of [-1,1])trim(weapon,steel,x*.09,0,.70,.035,.035,.32,.1,0,x*.32,0);for(const z of [.24,.30,.36])trim(weapon,0xb87333,0,0,z-.05,.095,.095,.022,.12);}
  else if(spec==='darkness'){const crook=0x5a4a70;trim(weapon,crook,-.13,0,.72,.04,.04,.30,.15);trim(weapon,crook,-.045,0,.885,.21,.04,.04,.15);trim(weapon,crook,.045,0,.835,.04,.04,.10,.15);trim(weapon,0x5b3c73,0,0,.58,.20,.03,.03,.2);}
  else{for(let n=0;n<3;n++){const a=n*Math.PI*2/3;box(weapon,steel,Math.cos(a)*.17,Math.sin(a)*.17,.61,.06,.06,.29);}trim(weapon,gold,0,0,.585,.19,.19,.04);}
  socket(weapon,'muzzle',0,0,.88);
 }else if(spec==='shield'){
  // Heater shield: steel rim around a dark field, stepped point, gold cross emblem and boss.
  box(weapon,steel,0,.13,.18,.60,.58,.12);box(weapon,steel,0,-.20,.18,.46,.10,.12);box(weapon,steel,0,-.29,.18,.28,.09,.12);
  box(weapon,dark,0,.13,.225,.50,.48,.035);box(weapon,dark,0,-.19,.225,.36,.16,.035);
  trim(weapon,gold,0,.05,.25,.09,.62,.025,.12);trim(weapon,gold,0,.17,.25,.36,.08,.025,.12);box(weapon,gold,0,.17,.27,.14,.14,.06).material=material(gold,false,.12);socket(weapon,'blade-tip',0,.4,.29);
 }else{
  // Grip wraps and a gold pommel, a guard, then a steel core between two bright edges, a fuller, and a stepped point.
  const great=spec==='greatsword',dagger=spec==='daggers',length=bladeLength(spec),width=great?.18:dagger?.09:.10,end=.26+length;
  box(weapon,dark,0,0,.02,.09,.09,great?.4:.27);for(const z of great?[-.06,.12]:dagger?[.04]:[-.04,.08])trim(weapon,wrap,0,0,z,.097,.097,.03);box(weapon,gold,0,0,great?-.225:-.15,great?.10:.075,great?.10:.075,great?.09:.07);
  box(weapon,gold,0,0,.22,great?.40:dagger?.20:.34,.065,.08);if(great){for(const x of [-1,1])trim(weapon,gold,x*.225,-.005,.245,.10,.05,.07,0,0,-x*.35,0);box(weapon,steel,0,0,.32,.14,.06,.12);}
  box(weapon,steel,0,0,(.26+end-.08)/2,width*.6,.05,length-.08).material=material(steel,false,.08);box(weapon,edge,0,0,(.26+end-.08)/2,width,.03,length-.08).material=material(edge,false,.25);
  if(!dagger)trim(weapon,0x4c5f66,0,0,.26+.06+length*.3,.022,.054,length*.6);
  if(dagger)trim(weapon,edge,.01,0,end-.015,width*.8,.03,.15,.25,0,.35,0);else{trim(weapon,edge,0,0,end-.04,width*.66,.035,.08,.25);trim(weapon,edge,0,0,end+.035,width*.33,.035,.07,.25);}
  socket(weapon,'blade-tip',0,0,.34+length);socket(weapon,'support-grip',0,0,-.11);
 }
 weapon.userData.offhand=offhand;
}
/** Modular equipment silhouettes communicate builds even before an attack fires. */
export function weaponModifiers(weapon:T.Group,p:PlayerView,time:number){
 if(p.spec==='ansatsuken'||p.spec==='emperor')return;
 if(p.spec==='leet')animateStaff(weapon,time);const key=JSON.stringify(p.chips);if(weapon.userData.modifierKey!==key){weapon.userData.modifierKey=key;weapon.getObjectByName('chip-modules')?.removeFromParent();const group=socket(weapon,'chip-modules',0,0,0),c={...p.chips},s=statsFor(c),w=WEAPONS[p.spec],tip=weapon.getObjectByName('muzzle')?.position.z??.8;
  for(let n=0;n<Math.min(6,s.extraProjectiles);n++){const x=(n%2?1:-1)*(.18+Math.floor(n/2)*.14);if(w.class==='ranged'){box(group,0x647e89,x,0,tip-.23,.11,.13,.48);box(group,0xffd88d,x,0,tip+.015,.12,.14,.075,true);}else{const orb=mesh(group,sphere,w.color,x,.08,tip-.06,.085,.085,.085,true);orb.userData.orbit=n;}}
  for(let n=0;n<(c.ricochet||0);n++)mesh(group,ring,0xffcc49,0,0,tip-.08-n*.11,.17,.17,.17,true);
  for(let n=0;n<(c.velocity||0);n++)mesh(group,ring,0x79eaff,0,0,tip-.35-n*.09,.13,.13,.13,true);
  if(s.homing)for(let side of [-1,1]){const fin=mesh(group,flame,0xf482d4,side*.19,0,tip-.15,.10,.25,.06,true);fin.rotation.z=side*-.9;}
  if(s.split)for(let side of [-1,1]){const fork=box(group,0x96ffb7,side*.16,-.13,tip-.05,.06,.06,.30,true);fork.rotation.y=side*.3;}
  if(s.pierces)box(group,0xffffff,0,.12,tip+.1,.035,.035,.35,true);
  if(c.magazine)box(group,0xe3b779,0,-.30,.1,.15+Math.min(c.magazine,8)*.025,.26,.19);
  if(s.pong)for(let n=0;n<3;n++){const halo=mesh(group,ring,0xff5269,0,0,tip-.10+n*.09,.27,.27,.27,true);halo.rotation.y=n*Math.PI/3;}
  if(s.boomerang)for(const side of [-1,1]){const wing=box(group,0x7fffe0,side*.2,0,tip-.1,.08,.04,.4,true);wing.rotation.y=side*.6;}
  if(s.vacuum){const halo=mesh(group,ring,0xbc72ff,0,0,tip,.3,.3,.3,true);halo.rotation.x=Math.PI/2;}
  const rate=(c['fire-rate']||c['cast-rate']||c['swing-rate']||0)+(c.overclock||0);if(rate){const gear=mesh(group,ring,0xffae64,0,0,.12,.21,.21,.21,true);gear.name='rate-coil';}
  if(s.impulse>1.3)for(let n=0;n<2;n++)box(group,0xffad68,(n?1:-1)*.12,.02,.35,.085,.085,.45,true);
  if(c.demolition)for(let n=0;n<3;n++)box(group,0xffd45f,(n-1)*.09,.12,.35,.045,.04,.20,true);
  if(s.shrapnel)mesh(group,sphere,0xff855c,0,-.12,.35,.13,.13,.13,true);
  if(s.staticEvery)for(let n=0;n<3;n++)box(group,0xc4a0ff,(n-1)*.08,-.12,.08,.04,.12,.04,true);
  if(s.reach>1){const length=bladeLength(p.spec,false);box(group,0x92f5e5,.12,0,.26+length/2,.025,.06,length,true);}
  if(s.extraStrikes)for(let side of [-1,1])box(group,0xc1a1ff,side*.16,0,.6,.025,.04,.6,true);
 }
 // Two flash frames per shot, rolled and sized from a hash of the authoritative shot time.
 const flash=weapon.getObjectByName('muzzle-flash');if(flash){const shot=p.lastShot??-10,age=time-shot;flash.visible=age>=0&&age<.075;if(flash.visible){const h=hash(shot),late=age>=.0375,size=(p.spec==='rifle'?.16:.10)*(late?.72+.2*h:1+.12*h);flash.scale.set(size,late?.24:.30+.06*h,size);flash.rotation.set(Math.PI/2,h*Math.PI*2+(late?.9:0),0);}}
 const group=weapon.getObjectByName('chip-modules');if(group){const gear=group.getObjectByName('rate-coil');if(gear)gear.rotation.z=time*8;for(const child of group.children)if(child.userData.orbit!==undefined){const a=time*2+child.userData.orbit*2.1;child.position.y=Math.sin(a)*.15;}}
}
export function bodyModifiers(r:CharacterRig,p:PlayerView,time:number){
 spellCharge(r,p,time);

 const c=p.chips,s=statsFor(c),key=JSON.stringify(c);if(r.group.userData.bodyChips!==key){r.group.userData.bodyChips=key;for(const parent of [r.chest,r.leftFoot,r.rightFoot,r.leftArm,r.rightArm])parent.getObjectByName('body-chips')?.removeFromParent();
  const chest=socket(r.chest,'body-chips',0,0,0);for(let n=0;n<Math.min(6,c.vital||0);n++)box(chest,0xff7478,(n%3-1)*.12,.12+Math.floor(n/3)*.1,.18,.085,.06,.045,true);
  if(c.frame||c.fortress||c.juggernaut||c.thorns){box(chest,0x718899,0,.1,.19,.59,.25,.09);for(const arm of [r.leftArm,r.rightArm])box(socket(arm,'body-chips',0,0,0),0x718899,0,-.065,0,.26,.16,.26);}
  for(let n=0;n<Math.min(6,Math.ceil((s.maxMana-100)/25));n++){const orb=mesh(chest,sphere,0xad8cff,0,.2,-.28,.065,.065,.065,true);orb.userData.orbit=n;}
  if(s.maxStamina>100)for(const side of [-1,1])for(let n=0;n<Math.min(5,Math.ceil((s.maxStamina-100)/25));n++)box(chest,0x8bffb6,side*.25,.02+n*.05,.18,.035,.025,.025,true);
  for(const foot of [r.leftFoot,r.rightFoot]){const g=socket(foot,'body-chips',0,0,0);if(s.knockbackTaken<1||s.debrisTaken<1)box(g,c.salvage?0xdab078:0x7e97ab,0,.06,0,.26,.13,.28);if(s.dashCooldown<1){const jet=mesh(g,flame,0x63e5ff,0,0,-.17,.075,.30,.075,true);jet.rotation.x=-Math.PI/2;jet.name='dash-jet';}}
  if(s.blockForce>1||s.blockHeal||s.blockAmmo||s.blockCooldown!==1||s.blockWindow||s.reflectDamage>1||s.blockPulseDamage)for(let n=0;n<3;n++)box(chest,0x78f7e7,(n-1)*.12,.27,.17,.075,.04,.045,true);
 }
 const nodes=r.chest.getObjectByName('body-chips');nodes?.children.forEach(o=>{if(o.userData.orbit!==undefined){const a=time*1.5+o.userData.orbit*Math.PI/3;o.position.set(Math.cos(a)*.40,.26+Math.sin(a*2)*.10,Math.sin(a)*.36);}});
 for(const foot of [r.leftFoot,r.rightFoot]){const jet=foot.getObjectByName('dash-jet');if(jet){jet.visible=(p.dashTime||0)>0;jet.scale.y=(p.dashTime||0)>0?.5:.1;}}
}
export function enchantWeapon(weapon:T.Group,elements:ElementId[],time:number,stacks:Record<string,number>={}){
 const key=elements.join(',')+':'+(stacks.ember||0)+':'+(stacks.poison||0),spec=weapon.userData.spec;
 if(spec==='ansatsuken'||spec==='emperor'){weapon.getObjectByName('enchantment')?.removeFromParent();return;}
 if(weapon.userData.enchant!==key){weapon.userData.enchant=key;weapon.getObjectByName('enchantment')?.removeFromParent();const g=socket(weapon,'enchantment',0,0,0),length=bladeLength(spec);
  for(const e of elements){const count=10+Math.min(6,stacks[e==='fire'?'ember':'poison']||1);for(let n=0;n<count;n++){const z=spec==='shield'?.34:.28+n/(count-1)*length,y=spec==='shield'?-.30+n/(count-1)*.7:0,side=n%2?1:-1;
   const m=mesh(g,e==='fire'?flame:sphere,e==='fire'?(n%3?0xff6c27:0xffd06a):0x88f37f,side*(spec==='shield'?.27:.10),y,z,e==='fire'?.065:.045,e==='fire'?.24:.045,e==='fire'?.08:.045,true);m.userData={element:e,baseY:y,phase:n*.81};
  }}
 }
 weapon.getObjectByName('enchantment')?.children.forEach(m=>{const fire=m.userData.element==='fire',phase=time*(fire?7:1.2)+m.userData.phase,p=(Math.sin(phase)+1)/2;m.position.y=m.userData.baseY+(fire?p*.06:p*.20);if(fire){m.scale.y=.16+p*.25;m.rotation.z=Math.sin(phase)*.3;}else m.scale.setScalar(.025+p*.045);});
}
