import test from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {initPhysics,type PlayerView} from '../src/shared/simulation.js';
import {WEAPONS,CHIPS,statsFor} from '../src/shared/content.js';
import {animateCharacter,animationPreviewPlayer} from '../src/client/character-animation.js';
import {poseFirstPersonView,FP_TUNE} from '../src/client/first-person-view.js';
import {weaponModifiers,bodyModifiers} from '../src/client/weapon-visuals.js';
import {markFirstPersonRig} from '../src/client/pose-flags.js';
import {POCKET,reloadPhase} from '../src/client/ranged-motion.js';
import {probeBase,probeMotion,probeRig} from './helpers/motion-probe.js';
import {adsProfile,kitFor,castTime} from '../src/shared/abilities.js';
import type {SpecId} from '../src/shared/config.js';

// Ranged/magic presentation fluidity (ranged-magic workstream). Presentation only: every scenario drives existing
// PlayerView fields (reloadTime, guarding, lastAttack/lastShot, vel, pitch) through the real rig.
await initPhysics();
const GUNS=['pistol','rifle','machinegun'] as const,MAGIC=['fire','water','lightning','darkness'] as const;
const idle=(spec:string,t:number,patch:Partial<PlayerView>={})=>({...animationPreviewPlayer(probeBase(spec),'idle',t),hitStunUntil:-1e9,...patch}) as PlayerView;

test('reload fades in and out of the aim without a pose pop, and the magazine always comes back',()=>{
 for(const spec of GUNS){
  const R=WEAPONS[spec].reload,patch=(t:number)=>({reloadTime:t>=1&&t<1+R?1+R-t:0});
  const start=probeMotion({spec,mode:'idle',from:.95,to:1.25,patch}),end=probeMotion({spec,mode:'idle',from:1+R-.25,to:1+R+.15,patch});
  assert.ok(start.maxJointStepDeg<15,`${spec} reload start ${start.maxJointStepDeg.toFixed(1)} deg/frame on ${start.maxJointStepBone}`);
  assert.ok(end.maxJointStepDeg<15,`${spec} reload end ${end.maxJointStepDeg.toFixed(1)} deg/frame on ${end.maxJointStepBone}`);
  // Mid-reload the magazine is off its socket; after the reload, and after a cancelled reload, it is back in place.
  const r=probeRig(spec),mag=r.weapon.getObjectByName('magazine')!,rest=mag.position.clone();let moved=false;
  for(let i=0;i<=Math.round((R+.4)*60);i++){const t=1+i/60,p=idle(spec,t,{reloadTime:t<1+R?1+R-t:0});animateCharacter(r,p,t,1/60);const u=reloadPhase(p).u;if(u>.22&&u<.6&&mag.position.distanceTo(rest)>.05)moved=true;}
  assert.ok(moved,spec+' magazine leaves the gun during the reload');
  assert.ok(mag.position.distanceTo(rest)<1e-9&&mag.visible,spec+' magazine restored after the reload');
  const c=probeRig(spec),cm=c.weapon.getObjectByName('magazine')!;
  for(let i=0;i<60;i++){const t=1+i/60;animateCharacter(c,idle(spec,t,{reloadTime:t<1.5?R-(t-1):0}),t,1/60);}
  assert.ok(cm.position.distanceTo(rest)<1e-9&&cm.visible,spec+' magazine restored when the reload is cut short');
 }
});

test('the carried magazine rides in the support hand while it is fetched and seated',()=>{
 for(const spec of GUNS){
  const R=WEAPONS[spec].reload,r=probeRig(spec),mag=r.weapon.getObjectByName('magazine')!;let worst=0,n=0;
  for(let i=0;i<=Math.round(R*60);i++){const t=1+i/60,p=idle(spec,t,{reloadTime:1+R-t});animateCharacter(r,p,t,1/60);const u=reloadPhase(p).u;
   if(u>.52&&u<.6){r.group.updateMatrixWorld(true);worst=Math.max(worst,r.leftHand.getWorldPosition(new T.Vector3()).distanceTo((mag.children[0]??mag).getWorldPosition(new T.Vector3())));n++;}}
  assert.ok(n>0&&worst<.2,`${spec} magazine to support hand ${worst.toFixed(3)} m`);
 }
});

test('guard entry and exit are blended (no single-frame snap into the guard)',()=>{
 for(const spec of [...GUNS,...MAGIC]){
  const rep=probeMotion({spec,mode:'idle',from:.95,to:1.8,patch:(t:number)=>({guarding:t>=1&&t<1.4})});
  assert.ok(rep.maxJointStepDeg<(WEAPONS[spec].class==='magic'?20:15),`${spec} guard ${rep.maxJointStepDeg.toFixed(1)} deg/frame on ${rep.maxJointStepBone}`);
 }
});

test('magic recasts carry on from the previous phrase: per-frame steps shrink with the frame rate (no snaps)',()=>{
 for(const spec of MAGIC){
  const I=WEAPONS[spec].interval,casts=[1,1+I,1+2*I,1+3*I],cw=probeBase(spec)&&(animationPreviewPlayer(probeBase(spec),'attack',0).lastShot!-animationPreviewPlayer(probeBase(spec),'attack',0).lastAttack);
  const patch=(t:number)=>{const l=[...casts].reverse().find(c=>c<=t),rel=[...casts].reverse().find(c=>c+cw<=t);return l===undefined?{}:{lastAttack:l,lastShot:rel===undefined?-10:rel+cw};};
  const at60=probeMotion({spec,hz:60,mode:'idle',from:.95,to:1+4*I+.8,patch}),at120=probeMotion({spec,hz:120,mode:'idle',from:.95,to:1+4*I+.8,patch});
  assert.ok(at120.maxJointStepDeg<.72*at60.maxJointStepDeg,`${spec} recast steps ${at60.maxJointStepDeg.toFixed(1)} @60 vs ${at120.maxJointStepDeg.toFixed(1)} @120`);
 }
});

test('running keeps the arm pump but the gun holds a stable carry (no vertical or backward barrel)',()=>{
 for(const spec of GUNS){
  const r=probeRig(spec),base=probeBase(spec);let minPitch=Infinity,maxPitch=-Infinity,minZ=Infinity;
  for(let n=0;n<120;n++){const t=n/60;animateCharacter(r,{...animationPreviewPlayer(base,'run',t),hitStunUntil:-1e9},t,1/60);if(n<40)continue;r.group.updateMatrixWorld(true);
   const d=new T.Vector3(0,0,1).applyQuaternion(r.weapon.getWorldQuaternion(new T.Quaternion()));minPitch=Math.min(minPitch,Math.asin(d.y));maxPitch=Math.max(maxPitch,Math.asin(d.y));minZ=Math.min(minZ,d.z);}
  assert.ok(minPitch>-60*Math.PI/180&&maxPitch<15*Math.PI/180,`${spec} barrel pitch ${(minPitch*57.3).toFixed(0)}..${(maxPitch*57.3).toFixed(0)} deg`);
  assert.ok(minZ>.3,`${spec} barrel never points back (min z ${minZ.toFixed(2)})`);
 }
});

test('rifle and machine gun are shouldered: butt in the pocket, support hand on the grip, barrel on the aim',()=>{
 for(const spec of ['rifle','machinegun'])for(const speed of [0,6.8])for(const pitch of [-.7,0,.7]){
  const r=probeRig(spec);for(let n=0;n<40;n++){const t=2+n/60;animateCharacter(r,idle(spec,t,{pitch,vel:{x:0,y:0,z:speed},lastShot:t-.3,lastAttack:t-.3}),t,1/60,pitch);}
  r.group.updateMatrixWorld(true);
  const butt=r.chest.worldToLocal(r.weapon.getObjectByName('stock-butt')!.getWorldPosition(new T.Vector3()));
  assert.ok(butt.distanceTo(POCKET)<.05,`${spec} butt ${butt.distanceTo(POCKET).toFixed(3)} m from the shoulder pocket (v=${speed}, pitch ${pitch})`);
  const grip=r.leftHand.getWorldPosition(new T.Vector3()).distanceTo(r.weapon.getObjectByName('support-grip')!.getWorldPosition(new T.Vector3()));
  assert.ok(grip<.03,`${spec} support grip ${grip.toFixed(3)}`);
  const d=new T.Vector3(0,0,1).applyQuaternion(r.weapon.getWorldQuaternion(new T.Quaternion()));
  assert.ok(Math.abs(Math.asin(d.y)-pitch)<.08,`${spec} barrel pitch ${Math.asin(d.y).toFixed(2)} vs aim ${pitch}`);
 }
 // Isosceles pistol: both hands on the gun.
 const r=probeRig('pistol');for(let n=0;n<40;n++){const t=2+n/60;animateCharacter(r,idle('pistol',t),t,1/60);}
 r.group.updateMatrixWorld(true);assert.ok(r.aimWeight>.99);
 assert.ok(r.leftHand.getWorldPosition(new T.Vector3()).distanceTo(r.weapon.getObjectByName('support-grip')!.getWorldPosition(new T.Vector3()))<.03,'pistol support hand on the grip');
});

test('gun and cast layers are deterministic and consistent across 30/60/120 Hz',()=>{
 const scenario=(hz:number)=>{const r=probeRig('machinegun');for(let i=0;i<=hz*1.5;i++){const t=1+i/hz,n=Math.floor((t-1.1)/.085),l=t<1.1?-10:1.1+n*.085;animateCharacter(r,idle('machinegun',t,{lastShot:l,lastAttack:l,guarding:t>1.3&&t<1.9}),t,1/hz);}r.group.updateMatrixWorld(true);return r.weapon.getWorldQuaternion(new T.Quaternion());};
 assert.ok(scenario(60).angleTo(scenario(60))<1e-9,'same inputs, same pose');
 assert.ok(scenario(30).angleTo(scenario(120))<.05,'frame-rate independent');
});

test('first-person melee keeps the striking hand on screen',()=>{
 for(const spec of ['greatsword','daggers','sword','shield']){
  const cam=new T.PerspectiveCamera(75,16/9,.08,400),root=new T.Group();cam.add(root);const r=probeRig(spec);markFirstPersonRig(r);root.add(r.group);
  const base=probeBase(spec);let n=0,on=0;
  for(let i=0;i<=150;i++){const t=i/60;poseFirstPersonView(r,root,{...animationPreviewPlayer(base,'attack',t),hitStunUntil:-1e9},t,1/60,false);if(i<30)continue;cam.updateMatrixWorld(true);
   const h=r.rightHand.getWorldPosition(new T.Vector3()).project(cam);n++;if(Math.abs(h.x)<=1&&Math.abs(h.y)<=1)on++;}
  assert.ok(on/n>=.8,`${spec} right hand on screen ${(100*on/n).toFixed(0)}%`);
 }
});

test('first-person gun barrels point along the camera aim, idle and firing',()=>{
 for(const spec of GUNS)for(const mode of ['idle','attack'] as const){
  const cam=new T.PerspectiveCamera(75,16/9,.08,400),root=new T.Group();cam.add(root);const r=probeRig(spec);markFirstPersonRig(r);root.add(r.group);
  const muzzle=r.weapon.getObjectByName('muzzle');assert.ok(muzzle,`${spec} has a muzzle marker`);
  const a=new T.Vector3(),b=new T.Vector3();let worstYaw=0,sum=0,n=0;
  for(let i=0;i<=120;i++){const t=1+i/60;poseFirstPersonView(r,root,{...animationPreviewPlayer(probeBase(spec),mode,t),hitStunUntil:-1e9},t,1/60,false);if(i<30)continue;cam.updateMatrixWorld(true);
   muzzle!.getWorldPosition(a);r.weapon.getWorldPosition(b);const d=a.sub(b).normalize();
   // Yaw across the view is the defect axis (every frame); the recoil rise is vertical and intended, so the total angle is bounded on average.
   worstYaw=Math.max(worstYaw,Math.abs(Math.atan2(d.x,-d.z))*180/Math.PI);sum+=Math.acos(Math.max(-1,Math.min(1,-d.z)))*180/Math.PI;n++;}
  assert.ok(worstYaw<5,`${spec} ${mode}: first-person barrel yawed ${worstYaw.toFixed(1)} deg across the view`);
  assert.ok(sum/n<5,`${spec} ${mode}: first-person barrel ${(sum/n).toFixed(1)} deg off camera forward on average`);
 }
});

// First-person staff probe (design §10.2/§10.5, port of probes/fp-staff2.mts): 75° FOV, 16:9, frames 30-150 at 60 Hz. The staff
// axis is sampled at 41 points; the projected convex hulls of every staff and chip-module mesh are rasterised on a 128×72 grid
// (left-half area, and any coverage of the crosshair box |x|<.1, |y|<.15 NDC). Worst-case chips: every chip that mounts a
// module on the weapon, at max copies.
const MODULE_STATS=new Set(['extraProjectiles','homing','split','pierces','pong','boomerang','vacuum']);
const WORST_CHIPS:Record<string,number>=Object.fromEntries(CHIPS.filter(c=>c.effects.some(f=>MODULE_STATS.has(f.stat))||['ricochet','velocity','cast-rate'].includes(c.id)).map(c=>[c.id,c.max]));
const GW=128,GH=72;
function hull(pts:[number,number][]){pts.sort((a,b)=>a[0]-b[0]||a[1]-b[1]);const cr=(o:number[],a:number[],b:number[])=>(a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0]);const lo:[number,number][]=[],up:[number,number][]=[];for(const p of pts){while(lo.length>=2&&cr(lo[lo.length-2],lo[lo.length-1],p)<=0)lo.pop();lo.push(p);}for(let i=pts.length-1;i>=0;i--){const p=pts[i];while(up.length>=2&&cr(up[up.length-2],up[up.length-1],p)<=0)up.pop();up.push(p);}up.pop();lo.pop();return lo.concat(up);}
function inside(h:[number,number][],x:number,y:number){if(h.length<3)return false;for(let i=0;i<h.length;i++){const a=h[i],b=h[(i+1)%h.length];if((b[0]-a[0])*(y-a[1])-(b[1]-a[1])*(x-a[0])<0)return false;}return true;}
/** Rasterise the visible meshes under `root` (convex hull per mesh); returns the grid. */
function raster(root:T.Object3D,cam:T.Camera,grid:Uint8Array){
 grid.fill(0);root.traverse(o=>{const m=o as T.Mesh;if(!m.isMesh)return;for(let a:T.Object3D|null=m;a;a=a.parent)if(!a.visible)return;const pos=(m.geometry as T.BufferGeometry).getAttribute('position');if(!pos)return;
  const pts:[number,number][]=[],tmp=new T.Vector3(),step=Math.max(1,Math.floor(pos.count/96));for(let k=0;k<pos.count;k+=step){tmp.fromBufferAttribute(pos,k);m.localToWorld(tmp);if(cam.worldToLocal(tmp.clone()).z>-.08)return;tmp.project(cam);pts.push([tmp.x,tmp.y]);}
  if(pts.length<3)return;const hl=hull(pts);let x0=1,x1=-1,y0=1,y1=-1;for(const p of hl){x0=Math.min(x0,p[0]);x1=Math.max(x1,p[0]);y0=Math.min(y0,p[1]);y1=Math.max(y1,p[1]);}
  for(let gy=Math.max(0,Math.floor((y0+1)/2*GH));gy<=Math.min(GH-1,Math.ceil((y1+1)/2*GH));gy++)for(let gx=Math.max(0,Math.floor((x0+1)/2*GW));gx<=Math.min(GW-1,Math.ceil((x1+1)/2*GW));gx++)if(!grid[gy*GW+gx]&&inside(hl,(gx+.5)/GW*2-1,(gy+.5)/GH*2-1))grid[gy*GW+gx]=1;});
 let left=0,cross=0;for(let gy=0;gy<GH;gy++)for(let gx=0;gx<GW;gx++){if(!grid[gy*GW+gx])continue;const cx=(gx+.5)/GW*2-1,cy=(gy+.5)/GH*2-1;if(cx<0)left++;if(Math.abs(cx)<.1&&Math.abs(cy)<.15)cross++;}
 return {left:left/(GW*GH/2),cross};
}
type FpFrame=(t:number,base:PlayerView)=>PlayerView;
/** One first-person staff scenario through the renderer's order (poseFirstPersonView, then weaponModifiers/bodyModifiers). */
export function fpStaffProbe(spec:string,frame:FpFrame,chips:boolean,opts:{from?:number;to?:number;hz?:number;release?:number}={}){
 const cam=new T.PerspectiveCamera(75,16/9,.08,400),root=new T.Group();cam.add(root);const r=probeRig(spec);markFirstPersonRig(r);root.add(r.group);
 const base={...probeBase(spec),chips:chips?{...WORST_CHIPS}:{}},hz=opts.hz??60,grid=new Uint8Array(GW*GH),from=opts.from??1.5,to=opts.to??3.5;
 let n=0,cross=0,crossAny=0,crossRelease=0,area=0,m=0,gem=0,gemX=0,hand=0,minD=9;
 for(let i=0;i<=Math.round((to-1)*hz);i++){const t=1+i/hz,p={...frame(t,base),chips:base.chips,hitStunUntil:-1e9};poseFirstPersonView(r,root,p,t,1/hz,false);weaponModifiers(r.weapon as T.Group,p,t);bodyModifiers(r,p,t);
  if(t<from)continue;cam.updateMatrixWorld(true);
  let hitC=false;for(let k=0;k<=40;k++){const w=r.weapon.localToWorld(new T.Vector3(0,0,-.36+1.24*k/40));minD=Math.min(minD,cam.worldToLocal(w.clone()).length());const s=w.project(cam);if(Math.abs(s.x)<.1&&Math.abs(s.y)<.15)hitC=true;}
  n++;if(hitC)cross++;
  const g=r.weapon.localToWorld(new T.Vector3(0,0,.72)).project(cam);if(Math.abs(g.x)<=1&&Math.abs(g.y)<=1)gem++;gemX+=g.x;
  const h=r.rightHand.getWorldPosition(new T.Vector3()).project(cam);if(Math.abs(h.x)<=1&&Math.abs(h.y)<=1)hand++;
  if(i%2===0||opts.release!==undefined){const a=raster(r.weapon,cam,grid);area+=a.left;m++;if(a.cross){crossAny++;if(opts.release!==undefined&&Math.abs(t-opts.release)<=.15)crossRelease++;}}
 }
 return {cross:cross/n,crossAny:crossAny/m,crossRelease,left:area/m,gem:gem/n,gemX:gemX/n,hand:hand/n,minD};
}
const preview=(mode:string):FpFrame=>(t,b)=>animationPreviewPlayer(b,mode,t);
test('first-person staff never covers the crosshair and covers less of the left than the rejected x .24 placement',()=>{
 const chosen={...FP_TUNE},measure=(chips:boolean)=>MAGIC.flatMap(spec=>(['idle','attack','block'] as const).map(mode=>({spec,mode,...fpStaffProbe(spec,preview(mode),chips)})));
 const ours=measure(false),oursChips=measure(true);
 Object.assign(FP_TUNE,{magicX:.24,magicY:-.48,magicStaffScale:1,magicRoll:0,moduleScale:1});
 let ref:ReturnType<typeof measure>,refChips:ReturnType<typeof measure>;try{ref=measure(false);refChips=measure(true);}finally{Object.assign(FP_TUNE,chosen);}
 const mean=(rows:{left:number}[])=>rows.reduce((s,r)=>s+r.left,0)/rows.length;
 for(const r of [...ours,...oursChips]){const id=`${r.spec} ${r.mode}`;
  assert.equal(r.cross,0,`${id}: staff axis crosses the crosshair on ${(100*r.cross).toFixed(0)}% of frames`);
  assert.equal(r.crossAny,0,`${id}: a staff or chip-module mesh covers the crosshair box on ${(100*r.crossAny).toFixed(0)}% of frames`);
  assert.ok(r.gem>=.95,`${id}: gem on screen ${(100*r.gem).toFixed(0)}%`);assert.ok(r.minD>=.12,`${id}: nearest staff point ${r.minD.toFixed(3)} m`);
  if(r.mode==='attack')assert.ok(r.hand>=.45,`${id}: cast hand on screen ${(100*r.hand).toFixed(0)}%`);
  // The roll tilts the top outward: the gem sits left of the crosshair (PROBE: NDC x ≈ -.42 idle), not back toward it.
  if(r.mode==='idle')assert.ok(r.gemX<-.3,`${id}: gem at NDC x ${r.gemX.toFixed(2)}`);}
 assert.ok(mean(ours)<=.85*mean(ref),`left-half area ${(100*mean(ours)).toFixed(1)}% vs x .24 reference ${(100*mean(ref)).toFixed(1)}%`);
 assert.ok(mean(oursChips)<=.5*mean(refChips),`left-half area with worst-case chips ${(100*mean(oursChips)).toFixed(1)}% vs reference ${(100*mean(refChips)).toFixed(1)}%`);
});

/** First-person gun at full ADS: muzzle NDC x, the highest projected gun vertex, and chip-module meshes in the crosshair box. */
export function adsProbe(spec:string,frame:FpFrame,opts:{chips?:boolean;from?:number;to?:number}={}){
 const P=adsProfile(spec as SpecId)!,cam=new T.PerspectiveCamera(P.fov,16/9,.08,400),root=new T.Group();cam.add(root);const r=probeRig(spec);markFirstPersonRig(r);root.add(r.group);
 const base={...probeBase(spec),chips:opts.chips?{...WORST_CHIPS}:{}},muzzle=r.weapon.getObjectByName('muzzle')!,grid=new Uint8Array(GW*GH),v=new T.Vector3();let maxX=0,top=-9,modules=0,hidden=0,n=0;
 for(let i=0;i<=Math.round(((opts.to??3.5)-1)*60);i++){const t=1+i/60,p={...frame(t,base),chips:base.chips,hitStunUntil:-1e9,adsSince:0};poseFirstPersonView(r,root,p,t,1/60,false,P);weaponModifiers(r.weapon as T.Group,p,t);
  if(t<(opts.from??1.5))continue;n++;if(!root.visible){hidden++;continue;}cam.updateMatrixWorld(true);maxX=Math.max(maxX,Math.abs(muzzle.getWorldPosition(v).project(cam).x));
  // The gun's own meshes (the transient muzzle flash is a firing effect, not the gun).
  r.weapon.traverse(o=>{const m=o as T.Mesh;if(!m.isMesh)return;for(let a:T.Object3D|null=m;a;a=a.parent)if(!a.visible||a.name==='muzzle-flash')return;const pos=(m.geometry as T.BufferGeometry).getAttribute('position');for(let k=0;k<pos.count;k++){v.fromBufferAttribute(pos,k);m.localToWorld(v).project(cam);if(Math.abs(v.x)<1)top=Math.max(top,v.y);}});
  const g=r.weapon.getObjectByName('chip-modules');if(g&&raster(g,cam,grid).cross)modules++;}
 return {maxX,top,modules,hidden:hidden/n};
}
test('ADS view model: muzzle on the crosshair column, sights under it, no chip module in the crosshair box, rifle behind its scope',()=>{
 for(const spec of ['pistol','machinegun'])for(const mode of ['idle','attack'] as const){const r=adsProbe(spec,preview(mode),{chips:true});
  assert.equal(r.hidden,0,`${spec} ${mode}: the sighted gun stays visible`);
  assert.ok(r.maxX<.06,`${spec} ${mode}: muzzle at NDC |x| ${r.maxX.toFixed(3)}`);
  assert.ok(r.top<=-.03,`${spec} ${mode}: gun top at NDC y ${r.top.toFixed(3)} (crosshair occluded)`);
  assert.equal(r.modules,0,`${spec} ${mode}: chip modules in the crosshair box`);}
 assert.equal(adsProbe('rifle',preview('attack'),{chips:true}).hidden,1,'the rifle view model hides behind the scope overlay');
});

/** Mark the kit's Q and E live for `fn` (G/M set `live` when their handlers land; F leaves it unset). */
function liveKits<R>(fn:()=>R):R{const defs=[...GUNS,...MAGIC].flatMap(s=>[kitFor(s)!.q,kitFor(s)!.e]),old=defs.map(d=>d.live);defs.forEach(d=>d.live=true);try{return fn();}finally{defs.forEach((d,i)=>d.live=old[i]);}}
const PHRASES=[...MAGIC.flatMap(spec=>(['ability-q','ability-e'] as const).map(mode=>({spec,mode}))),...(['pistol','machinegun','rifle'] as const).map(spec=>({spec,mode:'ability-e' as const}))];
test('ability phrases (8 spells, frag throw, rocket, Orbital Mark) are deterministic and frame-rate consistent: no snaps',()=>liveKits(()=>{
 for(const {spec,mode} of PHRASES){
  const run=(hz:number)=>probeMotion({spec,mode,hz,from:2.45,to:3.7,keepFrames:true});const a=run(60),b=run(120),c=run(30);
  assert.ok(a.maxJointStepDeg>.5,`${spec} ${mode}: the phrase moves the rig (${a.maxJointStepDeg.toFixed(2)} deg/frame)`);
  assert.ok(b.maxJointStepDeg<.75*a.maxJointStepDeg,`${spec} ${mode}: ${a.maxJointStepDeg.toFixed(1)} deg/frame @60 vs ${b.maxJointStepDeg.toFixed(1)} @120 (${a.maxJointStepBone})`);
  const end=(r:typeof a)=>r.samples!.at(-1)!.tip!;assert.ok(end(c).distanceTo(end(b))<.03,`${spec} ${mode}: 30 Hz and 120 Hz end on the same pose (${end(c).distanceTo(end(b)).toFixed(3)} m)`);
  assert.deepEqual(run(60),a,`${spec} ${mode}: same inputs, same pose`);
 }
}));
test('first-person ability phrases keep the staff off the crosshair: ≤5 % of frames, none within ±.15 s of release',()=>liveKits(()=>{
 for(const spec of MAGIC)for(const slot of ['q','e'] as const){const def=kitFor(spec)![slot],release=2.5+castTime(def.windup,statsFor({},spec));
  for(const chips of [false,true]){const r=fpStaffProbe(spec,preview('ability-'+slot),chips,{from:2.45,to:3.7,release}),id=`${spec} ${def.id}${chips?' (worst-case chips)':''}`;
   assert.ok(r.crossAny<=.05,`${id}: staff in the crosshair box on ${(100*r.crossAny).toFixed(1)}% of frames`);
   assert.equal(r.crossRelease,0,`${id}: staff crosses the crosshair within .15 s of release`);assert.ok(r.minD>=.12,`${id}: nearest staff point ${r.minD.toFixed(3)} m`);}}
}));
test('ADS rocket recoil: the machine gun never rises into the crosshair (pitch ≤ .12 rad, push-back and dip)',()=>liveKits(()=>{
 const r=adsProbe('machinegun',preview('ability-e'),{chips:true,from:2.45,to:3.7});assert.ok(r.top<=-.03,`gun top at NDC y ${r.top.toFixed(3)}`);assert.ok(r.maxX<.06);
}));
