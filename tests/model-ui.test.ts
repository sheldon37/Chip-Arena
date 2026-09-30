import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import * as T from 'three';
import {initPhysics,Simulation} from '../src/shared/simulation.js';
import {OverheadHealth} from '../src/client/overhead-health.js';
import {dressRig} from '../src/client/cosmetics.js';
import {WEAPONS} from '../src/shared/content.js';
import {createCharacterRig,recolorCharacterRig,settleJointCovers,setRigDetail,RIG_LENGTHS} from '../src/client/character-rig.js';
import {buildWeapon,weaponModifiers,enchantWeapon,BLADE_LENGTH} from '../src/client/weapon-visuals.js';
import {animateCharacter} from '../src/client/character-animation.js';
import {animateLobbyPose} from '../src/client/lobby-poses.js';
import {framePortrait} from '../src/client/menu-portraits.js';
import {PROBE_RATES,probeBase,probeMotion,probeRig} from './helpers/motion-probe.js';

await initPhysics();
const meshes=(o:T.Object3D)=>{const list:T.Mesh[]=[];o.traverse(x=>{if(x instanceof T.Mesh)list.push(x);});return list;};
const part=(r:ReturnType<typeof createCharacterRig>,name:string)=>r.group.getObjectByName(name) as T.Mesh;
const lambert=(m:T.Mesh)=>m.material as T.MeshLambertMaterial;
const local=(weapon:T.Object3D,name:string)=>{const o=weapon.getObjectByName(name);assert.ok(o,name+' socket exists');weapon.updateMatrixWorld(true);return weapon.worldToLocal(o!.getWorldPosition(new T.Vector3()));};

test('the arena glow and the recolor path paint every body part the same way (the head is lit like the torso)',()=>{
 const glow=createCharacterRig('#70e1d0','#e5e7eb',true),plain=createCharacterRig('#70e1d0','#e5e7eb',false);recolorCharacterRig(plain,'#70e1d0','#e5e7eb');
 const glowAgain=createCharacterRig('#ff6b35','#e5e7eb',true);recolorCharacterRig(glowAgain,'#70e1d0','#e5e7eb');
 for(const name of ['blank-head','crown','left-thumb','right-thumb','collar','left-upper-arm','right-calf','abdomen-shell'])assert.equal(part(glow,name).material,part(glow,'ribcage').material,name+' shares the lit body shell');
 assert.ok(lambert(part(glow,'blank-head')).emissiveIntensity>.1,'the head is self-lit like the shell in the arena');
 for(const m of meshes(glow.group))assert.equal(m.material,part(glowAgain,m.name).material,m.name+': creation glow and recolor pick the same shared material');
 for(const m of meshes(plain.group))assert.equal(lambert(m).emissiveIntensity||0,0,m.name+' portrait parts stay unlit');
});

test('accents keep their hue under the arena tone mapping while the armor strips still burn',()=>{
 const r=createCharacterRig('#9ede79','#e5e7eb',true),sternum=lambert(part(r,'sternum')),strip=lambert(part(r,'armor-back-light'));
 assert.ok(sternum.emissiveIntensity>.5&&sternum.emissiveIntensity<1.5,'accent emissive '+sternum.emissiveIntensity);
 const hsl={h:0,s:0,l:0};sternum.emissive.getHSL(hsl);const base={h:0,s:0,l:0};new T.Color('#9ede79').getHSL(base);assert.ok(Math.abs(hsl.h-base.h)<.03&&hsl.s>base.s*.8,'emissive keeps the accent hue');
 assert.ok(strip.emissiveIntensity>sternum.emissiveIntensity*1.5,'thin strips stay the brightest glow');
 for(const name of ['waist-band','left-shoulder-cap','right-cuff','left-kneecap'])assert.equal(part(r,name).material,sternum,name+' is an accent');
});

test('gloves, boots and joints follow the body colour; the default body stays close to the classic grey',()=>{
 const light=createCharacterRig('#70e1d0','#e5e7eb'),dark=createCharacterRig('#70e1d0','#1b1d22');
 const lum=(r:ReturnType<typeof createCharacterRig>,n:string)=>{const c=lambert(part(r,n)).color;return .2126*c.r+.7152*c.g+.0722*c.b;};
 assert.ok(lum(dark,'left-foot')<lum(light,'left-foot')*.35,'dark bodies get dark boots');
 for(const r of [light,dark])assert.ok(lum(r,'left-knee-joint')<lum(r,'left-shin'),'joints stay darker than shells');
 const classic=new T.Color(0xb5c4ca),edge=lambert(part(light,'right-palm')).color;assert.ok(Math.abs(edge.r-classic.r)+Math.abs(edge.g-classic.g)+Math.abs(edge.b-classic.b)<.2,'default edge stays near the classic grey');
});

test('the model keeps the rig contract, closes the back and neck, and stays inside the mesh budget',()=>{
 const r=createCharacterRig('#70e1d0',undefined,true),plain=createCharacterRig('#70e1d0'),list=meshes(r.group);
 assert.ok(list.length<=48,'glow rig meshes '+list.length);assert.ok(meshes(plain.group).length<=45);
 const cube=list[0].geometry;for(const m of list){assert.equal(m.geometry,cube,m.name+' uses the shared cube');assert.equal(m.geometry.type,'BoxGeometry');}
 r.group.updateMatrixWorld(true);const head=part(r,'blank-head'),box=new T.Box3().setFromObject(head),centre=box.getCenter(new T.Vector3());
 assert.ok(Math.abs(centre.y-.64)<1e-6&&Math.abs(box.max.y-box.min.y-.34)<1e-6,'rest head centre and size match the authoritative headshot box');
 const at=(n:string)=>r.group.getObjectByName(n)!.getWorldPosition(new T.Vector3());
 assert.ok(Math.abs(at('left-elbow').distanceTo(at('left-shoulder'))-RIG_LENGTHS.upperArm)<1e-9&&Math.abs(at('left-knee').distanceTo(at('left-hip'))-RIG_LENGTHS.thigh)<1e-9);
 // Lower back: the abdomen shell's back face is flush with the hips and ribcage (no dark flex band from the chase camera).
 const back=(n:string)=>new T.Box3().setFromObject(part(r,n)).min.z;assert.ok(back('abdomen-shell')<=back('hip-shell')+.005&&back('abdomen-shell')<-.13,'abdomen shell covers the back');
 // Neck: nothing shows through between the upper chest and the neck joint.
 const top=new T.Box3().setFromObject(part(r,'upper-chest')).max.y,neck=new T.Box3().setFromObject(part(r,'neck-joint')).min.y;assert.ok(neck<=top,'neck joint reaches the chest');
 // Taper: the upper arm is at least as thick as the forearm.
 assert.ok(part(r,'left-upper-arm').scale.x>=part(r,'left-forearm').scale.x);
});

test('joint covers bisect the knee and elbow without moving any joint, and low detail hides only what it hid',()=>{
 const r=createCharacterRig('#70e1d0');r.leftKnee.rotation.x=1.4;r.rightElbow.rotation.x=-1.6;const before=[r.leftKnee.quaternion.clone(),r.rightElbow.quaternion.clone()];
 settleJointCovers(r);assert.ok(r.leftKnee.quaternion.equals(before[0])&&r.rightElbow.quaternion.equals(before[1]),'joints untouched');
 const cap=part(r,'left-kneecap'),elbow=part(r,'right-elbow-joint');assert.ok(Math.abs(cap.rotation.x+.7)<1e-9&&Math.abs(elbow.rotation.x-.8)<1e-9,'covers turn half the hinge');
 assert.ok(Math.abs(cap.position.length()-Math.hypot(.01,.105))<1e-9,'the kneecap orbits the pivot');
 r.leftKnee.rotation.x=0;settleJointCovers(r);assert.ok(Math.abs(cap.position.z-.105)<1e-9&&cap.rotation.x===0,'rest pose restores the cover');
 const details=meshes(r.group).filter(m=>m.userData.detail);assert.ok(details.length>=5&&details.every(m=>!m.castShadow));
 const collar=part(r,'collar');collar.visible=false;setRigDetail(r,true);assert.ok(details.every(m=>!m.visible));setRigDetail(r,false);
 assert.equal(collar.visible,false,'a part hidden by another layer (first-person torso) stays hidden');assert.ok(details.filter(m=>m!==collar).every(m=>m.visible));
});

test('covers follow a run cycle at every rate: the kneecap bisects the knee on every frame, with no lag',()=>{
 for(const hz of PROBE_RATES){const r=probeRig('sword');let frames=0;
  probeMotion({spec:'sword',mode:'run',from:1,to:1.6,hz,rig:r,animate:(rig,p,t,dt)=>{animateCharacter(rig,p,t,dt,p.pitch);if(t<1)return;frames++;
   for(const [cap,knee] of [[part(rig,'left-kneecap'),rig.leftKnee],[part(rig,'right-kneecap'),rig.rightKnee]] as const)assert.ok(Math.abs(cap.rotation.x+knee.rotation.x*.5)<1e-9,hz+' Hz: kneecap bisects the knee at '+t.toFixed(3));
   assert.ok(Math.abs(part(rig,'left-elbow-joint').rotation.x+rig.leftElbow.rotation.x*.5)<1e-9);}});
  assert.ok(frames>=hz*.5);}
});

test('weapon sockets: blade lengths, melee grips and the ranged/magic contracts',()=>{
 for(const spec of ['sword','greatsword','daggers'] as const){const w=new T.Group();buildWeapon(w,spec);const tip=local(w,'blade-tip'),grip=local(w,'support-grip');
  assert.ok(Math.abs(tip.z-(.34+BLADE_LENGTH[spec]))<1e-9&&tip.x===0&&tip.y===0,spec+' blade-tip');assert.ok(grip.distanceTo(new T.Vector3(0,0,-.11))<1e-9,spec+' support grip');}
 const shield=new T.Group();buildWeapon(shield,'shield');assert.ok(local(shield,'blade-tip').distanceTo(new T.Vector3(0,.4,.29))<1e-9);
 const pistol=new T.Group();buildWeapon(pistol,'pistol');assert.ok(local(pistol,'support-grip').distanceTo(new T.Vector3(.06,-.10,-.01))<.02,'pistol support grip');
 for(const spec of ['rifle','machinegun']){const w=new T.Group();buildWeapon(w,spec);const butt=local(w,'stock-butt'),grip=local(w,'support-grip');
  assert.ok(butt.z<=-.28&&butt.z>=-.30,spec+' stock butt '+butt.z);assert.ok(grip.z>=.08&&grip.z<=.12,spec+' support grip '+grip.z);
  const box=new T.Box3().setFromObject(w);assert.ok(box.min.z>-.34,spec+' stock ends near the butt');}
 for(const [spec,tip] of [['pistol',.51],['rifle',1.22],['machinegun',.91]] as const){const w=new T.Group();buildWeapon(w,spec);assert.ok(local(w,'muzzle').distanceTo(new T.Vector3(0,0,tip))<1e-9,spec+' muzzle');assert.ok(w.getObjectByName('magazine'),spec+' magazine');assert.ok(local(w,'magazine').distanceTo(new T.Vector3(0,-.19,.16))<1e-9,spec+' magazine socket keeps the frozen (0,-.19,.16) origin the reload returns it to');}
 for(const spec of ['fire','water','lightning','darkness']){const w=new T.Group();buildWeapon(w,spec);assert.ok(local(w,'muzzle').distanceTo(new T.Vector3(0,0,.88))<1e-9);const guard=local(w,'guard-grip');assert.ok(guard.length()>.3&&guard.length()<.55,spec+' guard grip');}
 const great=new T.Group();buildWeapon(great,'greatsword');assert.ok(meshes(great).length<=13);assert.ok(meshes(shield).length<=10);
 const enchanted=new T.Group();buildWeapon(enchanted,'greatsword');enchantWeapon(enchanted,['fire'],0);const flames=enchanted.getObjectByName('enchantment')!.children.map(m=>m.position.z);assert.ok(Math.abs(Math.max(...flames)-(.28+BLADE_LENGTH.greatsword))<1e-9,'enchantment spans the blade');
});

test('muzzle flash: two frames per shot, rolled from the shot time, identical on replay',()=>{
 const base=probeBase('rifle'),w=new T.Group();buildWeapon(w,'rifle');const flash=w.getObjectByName('muzzle-flash')!;
 const sample=(shot:number,t:number)=>{weaponModifiers(w,{...base,lastShot:shot},t);return [flash.visible,flash.rotation.y,flash.scale.x,flash.scale.y] as const;};
 const a=sample(3,3.01),b=sample(3,3.05),again=sample(3,3.01),other=sample(4,4.01);
 assert.ok(a[0]&&b[0]);assert.deepEqual(a,again,'deterministic');assert.notEqual(a[1],b[1],'second frame turns');assert.notEqual(a[1],other[1],'each shot rolls differently');assert.equal(sample(3,3.08)[0],false);
});

const source=(file:string)=>readFileSync(new URL('../src/client/'+file,import.meta.url),'utf8');

test('the HUD is rewritten only when its markup changes, and the intro card is timed by CSS from authoritative age',()=>{
 const main=source('main.ts'),css=source('style.css');
 assert.equal((main.match(/hud\.innerHTML=/g)||[]).length,1,'every HUD write goes through setHud');assert.match(main,/if\(html===lastHudHtml\)return false/);
 const intro=main.slice(main.indexOf('<div class="intro-overlay"'),main.indexOf('syncIntroClock(hud,intro.age)'));
 assert.ok(intro.length>0&&!/opacity:\$\{|transform:translate\(-50%,\$\{/.test(intro),'no per-redraw inline opacity/transform steps');
 for(const v of ['--intro-age','--name-delay','--exit-delay','--intro-duration'])assert.ok(intro.includes(v),v+' keyed from intro age');
 assert.match(main,/--tick-delay/);assert.match(css,/@keyframes count-tick/);assert.match(css,/@keyframes intro-name-in/);
 const reduced=[...css.matchAll(/@media\(prefers-reduced-motion:reduce\)\{([\s\S]*?)\}\s*\n/g)].map(m=>m[1]).join(' ');for(const k of ['.intro-name','.intro-progress','.countdown b'])assert.ok(reduced.includes(k),k+' has a reduced-motion fallback');
 assert.match(main,/data-action="update-game"/);assert.match(main,/control.hidden=screen!=='home'/);
});

test('UI bug fixes: special card art is not flattened, help text is small print, panels clear the footer, the board interpolates',()=>{
 const css=source('style.css');
 assert.ok(!/\.circuit-chip(:hover)?\{background:[^}]*!important/.test(css),'no !important card background');assert.ok(!/\.circuit-chip p\{[^}]*!important/.test(css));
 assert.match(css,/\.panel \.fine,\.panel p\.fine\{font:13px/);assert.match(css,/\.menus \.panel\{max-height:calc\(100dvh - 158px\)/);assert.ok(!css.includes('.menus .panel{max-height:90vh'));
 assert.ok(!/'[^'`]*\$\{this\.state[^'`]*'/.test(source('player-board.ts')),'the empty state is a template literal');
});

test('lobby hero stances: feet planted, weapon clear of the floor, two-hand grips held, and a pure function of time',()=>{
 const joints=['body','pelvis','spine','chest','neck','head','leftArm','rightArm','leftElbow','rightElbow','leftHand','rightHand','leftLeg','rightLeg','leftKnee','rightKnee','leftFoot','rightFoot'] as const;
 for(const spec of ['greatsword','sword','daggers','shield','pistol','rifle','machinegun','fire','water']){
  const p={...probeBase(spec)},live=probeRig(spec),fresh=probeRig(spec);
  for(let n=0;n<=90;n++)animateLobbyPose(live,p,n/30,1/30,'weapon');animateLobbyPose(fresh,p,3,1/30,'weapon');
  for(const j of joints)assert.ok(live[j].quaternion.angleTo(fresh[j].quaternion)<1e-6,spec+' '+j+' depends only on time');
  live.group.updateMatrixWorld(true);const y=(o:T.Object3D)=>live.group.worldToLocal(o.getWorldPosition(new T.Vector3())).y;
  assert.ok(Math.abs(y(live.rightFoot)+.795)<.006,spec+' weight foot planted '+y(live.rightFoot).toFixed(3));assert.ok(y(live.leftFoot)>=-.796&&y(live.leftFoot)<-.74,spec+' free foot rests on its toes');
  const knees=[live.leftKnee.rotation.x,live.rightKnee.rotation.x];assert.ok(Math.min(...knees)>-.01&&Math.max(...knees)<.75,spec+' relaxed standing knees '+knees.map(k=>k.toFixed(2)));
  assert.ok(Math.abs(knees[0]-knees[1])>.08,spec+' contrapposto: one knee relaxed');
  const box=new T.Box3();live.weapon.traverseVisible(o=>{if(o instanceof T.Mesh)box.expandByObject(o,false);});const floor=live.group.worldToLocal(box.min.clone()).y;assert.ok(live.weapon.visible&&floor>-.88,spec+' weapon stays above the floor '+floor.toFixed(3));
  const grip=live.weapon.getObjectByName('support-grip');if(['rifle','machinegun'].includes(spec))assert.ok(grip!.getWorldPosition(new T.Vector3()).distanceTo(live.leftHand.getWorldPosition(new T.Vector3()))<.03,spec+' both hands on the weapon');
 }
});

test('lobby hero blades stay out of the torso, and the sleeper lies on the podium with the head on the lower hand',()=>{
 const inside=(point:T.Vector3,m:T.Mesh)=>{const q=point.clone().applyMatrix4(m.matrixWorld.clone().invert()),s=new T.Vector3().setFromMatrixScale(m.matrixWorld);return Math.min((.5-Math.abs(q.x))*s.x,(.5-Math.abs(q.y))*s.y,(.5-Math.abs(q.z))*s.z);};
 const grid=[-.5,-.25,0,.25,.5];
 for(const spec of ['greatsword','sword']){const r=probeRig(spec);for(const t of [0,1.3,2.9]){animateLobbyPose(r,probeBase(spec),t,1/30,'weapon');r.group.updateMatrixWorld(true);
  const torso=['ribcage','upper-chest','abdomen-shell','hip-shell','collar'].map(n=>part(r,n)).filter(Boolean);let depth=0;
  for(const w of meshes(r.weapon))if(!(w.material as T.Material&{isMeshBasicMaterial?:boolean}).isMeshBasicMaterial)for(const a of grid)for(const b of grid)for(const c of grid){const p=new T.Vector3(a,b,c).applyMatrix4(w.matrixWorld);for(const m of torso)depth=Math.max(depth,inside(p,m));}
  assert.ok(depth<.03,spec+' weapon sinks '+depth.toFixed(3)+' into the torso at '+t);}}
 const r=probeRig('sword'),podium=-.9075;for(const t of [0,1.7,3.4]){animateLobbyPose(r,probeBase('sword'),t,1/30,'sleep');r.group.updateMatrixWorld(true);
  const low=(n:string)=>new T.Box3().setFromObject(part(r,n)).min.y;let lowest=Infinity;for(const m of meshes(r.group))if(m.visible&&!r.weapon.getObjectById(m.id))lowest=Math.min(lowest,new T.Box3().setFromObject(m).min.y);
  assert.ok(lowest>podium-.02&&lowest<podium+.03,'the sleeper rests on the podium '+lowest.toFixed(3));assert.ok(low('hip-shell')<podium+.1&&low('right-shoulder-cap')<podium+.03,'hip and lower shoulder bear the weight');
  const head=new T.Box3().setFromObject(part(r,'blank-head')),palm=new T.Box3().setFromObject(part(r,'right-palm'));assert.ok(Math.abs(head.min.y-palm.max.y)<.03&&palm.max.x>head.min.x&&palm.min.x<head.max.x&&palm.max.z>head.min.z&&palm.min.z<head.max.z,'the head lies on the lower hand');}
});

test('portrait framing grows with a tall hat and keeps the front camera',()=>{
 const bare=new T.OrthographicCamera(),tall=new T.OrthographicCamera();framePortrait(bare,'weapon',.8);framePortrait(tall,'weapon',.8,2.45);
 for(const c of [bare,tall]){assert.equal(c.position.x,0);assert.ok(c.position.z>0&&c.right>=.95);}
 const top=(c:T.OrthographicCamera)=>{const d=new T.Vector3();c.getWorldDirection(d);return c.position.y+d.y*(c.position.z/Math.abs(d.z))+c.top;};
 assert.ok(top(tall)>2.45&&top(bare)>1.72,'hat and head stay inside the card');assert.ok(bare.top<1.28,'a bare pilot fills more of the card than the fixed framing');
});

test('UI polish: every action button has a real icon, the result banner is keyed to authoritative time, new motion respects reduced motion',async()=>{
 const {ACTION_ICONS}=await import('../src/client/icon-map.js'),{ICON_PATHS}=await import('../src/client/icons.generated.js');
 for(const id of ['login','account','lobby','test-driver','ready','rematch','copy-code','copy-invite','connect','start','spectate'])assert.ok(ICON_PATHS[ACTION_ICONS[id]],id+' icon');
 const main=source('main.ts'),css=source('style.css'),hud=source('match-hud.css');
 assert.match(main,/class="result-sequence" data-outcome="\$\{/);assert.match(main,/syncClock\(result,Number\(result\.dataset\.left\)-state\.remaining\)/);assert.ok(!main.includes("'#97f0d0'"),'connection light uses the good token');
 const reduced=(text:string)=>[...text.matchAll(/@media\(prefers-reduced-motion:reduce\)\{([\s\S]*?)\}\s*\n/g)].map(m=>m[1]).join(' ');
 for(const sel of ['.notice','.result-sequence .eyebrow','.result-sequence p:after','.click-resume'])assert.ok(reduced(css).includes(sel),sel+' has a reduced-motion fallback');
 assert.ok(reduced(hud).includes('.death-announcement.is-new'));assert.match(hud,/\.death-announcement\{font:500 13px/);
});

test('overhead health: the fill stays children[1]; a damage-lag segment holds, drains on authoritative time and resets on a jump',()=>{
 const sim=new Simulation({skipIntros:true});try{sim.addPlayer('a');sim.addPlayer('b','B','melee','dummy');const scene=new T.Scene(),camera=new T.PerspectiveCamera(),bars=new OverheadHealth(scene);
  const snap=(time:number,hp:number)=>{const s=sim.snapshot();return {...s,time,players:s.players.map(p=>p.id==='b'?{...p,hp,pos:{x:0,y:0,z:-3}}:p)};};
  bars.update(snap(10,100),camera,true,'a',false);const root=scene.getObjectByName('health-b')!,fill=root.children[1] as T.Mesh,lag=root.getObjectByName('health-lag')!;
  assert.equal((fill.material as T.MeshBasicMaterial).color.getHex(),0x59dd83);assert.equal(lag.visible,false);
  const max=sim.snapshot().players.find(p=>p.id==='b')!.maxHp;let t=10;const step=(until:number,hp:number)=>{for(;t<until-1e-9;t+=1/60)bars.update(snap(t,hp),camera,true,'a',false);};
  step(10.1,max*.5);const held=lag.scale.x;assert.ok(lag.visible&&Math.abs(held-.72)<.02,'the lost health is held at first');
  step(10.45,max*.5);assert.ok(lag.scale.x<held-.05&&lag.scale.x>fill.scale.x,'then drains toward the new health');
  step(11.5,max*.5);assert.equal(lag.visible,false,'and settles on the fill');
  bars.update(snap(11.6,max*.2),camera,true,'a',false);bars.update(snap(3,max*.2),camera,true,'a',false);assert.equal(lag.visible,false,'a backward time jump resets the lag');bars.clear();
 }finally{sim.dispose();}
});

test('blocky hats: beanie and crown are shared-cube boxes and re-dressing reuses hat materials',()=>{
 const r=createCharacterRig('#70e1d0');dressRig(r,{hat:'beanie',bodyColor:'#e5e7eb',killEffect:'voxel-burst'} as never);assert.ok(meshes(r.sockets.head).every(m=>m.geometry.type==='BoxGeometry'));
 dressRig(r,{hat:'crown',bodyColor:'#e5e7eb',killEffect:'voxel-burst'} as never);const crown=meshes(r.sockets.head);assert.ok(crown.length>=9&&crown.every(m=>m.geometry.type==='BoxGeometry'),'crown points all round');
 const a=createCharacterRig('#fff'),b=createCharacterRig('#fff');for(const x of [a,b])dressRig(x,{hat:'wizard',bodyColor:'#e5e7eb',killEffect:'voxel-burst'} as never);assert.equal((a.sockets.head.getObjectByName('hat') as T.Mesh).material,(b.sockets.head.getObjectByName('hat') as T.Mesh).material,'round hat materials are shared');
});
