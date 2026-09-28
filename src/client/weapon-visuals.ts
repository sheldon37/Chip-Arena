import {matrixStaff,animateStaff} from './matrix-effects.js';
import * as T from 'three';
import {spellPose} from './character-animation.js';
import {WEAPONS,statsFor} from '../shared/content.js';
import {ElementId} from '../shared/elements.js';
import type {PlayerView} from '../shared/simulation.js';
import type {CharacterRig} from './character-rig.js';
const cube=new T.BoxGeometry(1,1,1),sphere=new T.SphereGeometry(1,8,6),flame=new T.ConeGeometry(1,1,5),ring=new T.TorusGeometry(1,.055,4,16);
const mats=new Map<string,T.Material>();
function material(color:number,glow=false){const k=color+':'+glow;if(!mats.has(k))mats.set(k,glow?new T.MeshBasicMaterial({color}):new T.MeshLambertMaterial({color}));return mats.get(k)!;}
function mesh(parent:T.Object3D,shape:T.BufferGeometry,color:number,x:number,y:number,z:number,sx:number,sy:number,sz:number,glow=false){const m=new T.Mesh(shape,material(color,glow));m.position.set(x,y,z);m.scale.set(sx,sy,sz);m.castShadow=!glow;parent.add(m);return m;}
const box=(p:T.Object3D,c:number,x:number,y:number,z:number,sx:number,sy:number,sz:number,g=false)=>mesh(p,cube,c,x,y,z,sx,sy,sz,g);
function socket(parent:T.Object3D,name:string,x:number,y:number,z:number){const g=new T.Group();g.name=name;g.position.set(x,y,z);parent.add(g);return g;}
export function buildWeapon(weapon:T.Group,spec:string,offhand=false){
 weapon.clear();weapon.userData={spec,enchant:'',modifierKey:''};const w=WEAPONS[spec as keyof typeof WEAPONS];const steel=0x81999f,dark=0x263a43,gold=0xe3b779;
 if(spec==='ansatsuken'){box(weapon,0x931f31,0,-.015,-.01,.205,.175,.19);box(weapon,0xec3c43,0,-.04,.07,.205,.13,.065);box(weapon,0xe8d6ad,0,.06,-.01,.21,.045,.2);socket(weapon,'blade-tip',0,-.045,.115);}
 else if(spec==='leet'){matrixStaff(weapon);}else if(w.class==='ranged'){
  const rifle=spec==='rifle',mg=spec==='machinegun',tip=rifle?1.18:mg?.87:.47;
  box(weapon,dark,0,0,.16,.16,.20,rifle?.64:mg?.7:.39);box(weapon,steel,0,.09,.16,.145,.075,rifle?.68:mg?.7:.38);
  box(weapon,dark,0,0,tip-.17,.075,.075,rifle?.60:.34);box(weapon,gold,0,0,tip,.12,.12,.07);
  box(weapon,dark,0,-.13,-.03,.12,.24,.12);const mag=box(weapon,mg?gold:steel,0,-.19,.16,mg?.28:.10,mg?.28:.22,.17);mag.name='magazine';
  if(rifle||mg){box(weapon,dark,0,-.025,-.35,.13,.17,.35);box(weapon,steel,0,.10,.43,.16,.07,.30);socket(weapon,'support-grip',.025,-.03,.20);}
  if(rifle){box(weapon,dark,0,.225,.10,.13,.13,.37);box(weapon,gold,0,.225,.31,.17,.17,.06);box(weapon,0x86ddd8,0,.225,.345,.105,.105,.025,true);box(weapon,steel,-.12,.03,-.06,.12,.035,.055);}
  const muzzle=socket(weapon,'muzzle',0,0,tip+.04);const flash=mesh(muzzle,flame,0xffdb90,0,0,.14,.11,.30,.11,true);flash.rotation.x=Math.PI/2;flash.name='muzzle-flash';flash.visible=false;
 }else if(w.class==='magic'){
  box(weapon,dark,0,0,.23,.075,.085,.67);box(weapon,gold,0,0,.46,.16,.15,.10);
  mesh(weapon,new T.OctahedronGeometry(.16),w.color,0,0,.72,1,1,1,true);
  for(let n=0;n<3;n++){const a=n*Math.PI*2/3;box(weapon,steel,Math.cos(a)*.17,Math.sin(a)*.17,.61,.06,.06,.29);}
  socket(weapon,'muzzle',0,0,.88);
 }else if(spec==='shield'){
  box(weapon,dark,0,.035,.18,.60,.78,.13);box(weapon,steel,0,.035,.255,.50,.68,.06);box(weapon,gold,0,.035,.30,.12,.52,.035);box(weapon,gold,0,.035,.30,.38,.10,.035);socket(weapon,'blade-tip',0,.4,.29);
 }else{
  const great=spec==='greatsword',dagger=spec==='daggers',length=dagger?.5:great?1.35:.94;
  box(weapon,dark,0,0,.02,.09,.09,great?.4:.27);box(weapon,gold,0,0,.22,great?.44:dagger?.22:.34,.065,.08);
  box(weapon,steel,0,0,.26+length/2,great?.18:.10,.05,length);box(weapon,0xeaf6f4,-(great?.095:.052),0,.26+length/2,.025,.035,length);
  box(weapon,0xeaf6f4,0,0,.26+length,great?.10:.06,.045,.14);socket(weapon,'blade-tip',0,0,.34+length);socket(weapon,'support-grip',0,0,-.11);
 }
 weapon.userData.offhand=offhand;
}
/** Modular equipment silhouettes communicate builds even before an attack fires. */
export function weaponModifiers(weapon:T.Group,p:PlayerView,time:number){
 if(p.spec==='ansatsuken')return;
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
  if(s.reach>1){const length=p.spec==='daggers'?.5:p.spec==='greatsword'?1.35:.94;box(group,0x92f5e5,.12,0,.26+length/2,.025,.06,length,true);}
  if(s.extraStrikes)for(let side of [-1,1])box(group,0xc1a1ff,side*.16,0,.6,.025,.04,.6,true);
 }
 const flash=weapon.getObjectByName('muzzle-flash');if(flash){const age=time-(p.lastShot??-10);flash.visible=age>=0&&age<.075;flash.scale.x=flash.scale.z=p.spec==='rifle'?.16:.10;}
 const group=weapon.getObjectByName('chip-modules');if(group){const gear=group.getObjectByName('rate-coil');if(gear)gear.rotation.z=time*8;for(const child of group.children)if(child.userData.orbit!==undefined){const a=time*2+child.userData.orbit*2.1;child.position.y=Math.sin(a)*.15;}}
}
export function bodyModifiers(r:CharacterRig,p:PlayerView,time:number){
 let charge=r.rightHand.getObjectByName('spell-charge') as T.Group|undefined;
 if(charge?.userData.spec!==p.spec){charge?.removeFromParent();charge=socket(r.rightHand,'spell-charge',0,-.09,.02);charge.userData.spec=p.spec;mesh(charge,sphere,WEAPONS[p.spec].color,0,0,0,.14,.14,.14,true);for(let n=0;n<2;n++){const halo=mesh(charge,ring,WEAPONS[p.spec].color,0,0,0,.22,.22,.22,true);halo.rotation.x=n*Math.PI/2;}}
 const phrase=spellPose(p,time);charge.visible=p.hp>0&&(p.spec==='ansatsuken'?p.arcade?.action==='charge':phrase.active);charge.scale.setScalar(Math.max(.01,phrase.weight*(phrase.drive<.2?1+phrase.coil:.8)));charge.rotation.y=time*8;if(p.spec==='ansatsuken'&&p.arcade?.action==='charge')charge.scale.setScalar(.5+p.arcade.charge*1.2);

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
 if(spec==='ansatsuken'){weapon.getObjectByName('enchantment')?.removeFromParent();return;}
 if(weapon.userData.enchant!==key){weapon.userData.enchant=key;weapon.getObjectByName('enchantment')?.removeFromParent();const g=socket(weapon,'enchantment',0,0,0),length=spec==='daggers'?.5:spec==='greatsword'?1.35:spec==='shield'?.7:.94;
  for(const e of elements){const count=10+Math.min(6,stacks[e==='fire'?'ember':'poison']||1);for(let n=0;n<count;n++){const z=spec==='shield'?.34:.28+n/(count-1)*length,y=spec==='shield'?-.30+n/(count-1)*.7:0,side=n%2?1:-1;
   const m=mesh(g,e==='fire'?flame:sphere,e==='fire'?(n%3?0xff6c27:0xffd06a):0x88f37f,side*(spec==='shield'?.27:.10),y,z,e==='fire'?.065:.045,e==='fire'?.24:.045,e==='fire'?.08:.045,true);m.userData={element:e,baseY:y,phase:n*.81};
  }}
 }
 weapon.getObjectByName('enchantment')?.children.forEach(m=>{const fire=m.userData.element==='fire',phase=time*(fire?7:1.2)+m.userData.phase,p=(Math.sin(phase)+1)/2;m.position.y=m.userData.baseY+(fire?p*.06:p*.20);if(fire){m.scale.y=.16+p*.25;m.rotation.z=Math.sin(phase)*.3;}else m.scale.setScalar(.025+p*.045);});
}
