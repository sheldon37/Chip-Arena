import * as T from 'three';
import {bossScaleOf} from '../pve/boss-look.js';
import type {KillEffectFactory} from '../death-effects.js';
import type {CharacterRig} from '../character-rig.js';
import {createCharacterRig} from '../character-rig.js';
import {cinematicVictim,DeathPoseCarry} from '../cinematic-victim.js';
import {dressRig} from '../cosmetics.js';
import {buildWeapon} from '../weapon-visuals.js';
import {FlameField} from './flames.js';
import {buildSiren,applySirenLook} from './model.js';
import {haloMaterial,tickSirenClock} from './materials.js';

const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
const sm=(x:number)=>{x=clamp(x);return x*x*(3-2*x);};
const mix=(a:number,b:number,t:number)=>a+(b-a)*t;
const cube=new T.BoxGeometry(1,1,1),wp=new T.Vector3();
let seed=1;const rand=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;};

/** Burns over a rig's joints: black flame rising off the body at `rate` per second. */
function burnRig(fire:FlameField,r:CharacterRig,rate:number,dt:number,acc:{v:number},size=1){
 acc.v+=rate*dt;if(acc.v<1)return;r.group.updateMatrixWorld(true);const joints=[r.head,r.chest,r.spine,r.pelvis,r.leftArm,r.rightArm,r.leftElbow,r.rightElbow,r.leftLeg,r.rightLeg,r.leftKnee,r.rightKnee];
 while(acc.v>=1){acc.v-=1;const j=joints[Math.floor(rand()*joints.length)];wp.set((rand()-.5)*.22,(rand()-.5)*.22,(rand()-.5)*.22);j.localToWorld(wp);
  fire.emit(wp.x,wp.y,wp.z,(rand()-.5)*.3,1+rand()*.9,(rand()-.5)*.3,.5+rand()*.4,(.07+rand()*.07)*size,rand()<.05?'ember':rand()<.15?'smoke':'black',2.6,2);}
}

/** A kept (PvE downed) crush victim holds this age: lying on its back, charred, before it comes apart. */
export const SIREN_CRUSH_KEEP=2.6;
/**
 * Last Laugh kill: the head is crushed in the Siren's grip (voxel gibs and a black-flame burst), the body hangs limp
 * for a beat, drops, keeps burning on the floor and finally falls apart into black flame. Pure function of age.
 */
export const sirenCrush:KillEffectFactory=(scene,original,p,d,quality,budget,state)=>{
 seed=d.seed||7;const root=new T.Group();root.name='siren-crush';scene.add(root);const owned:T.Material[]=[];
 const fire=new FlameField(quality<.5?220:420);scene.add(fire.mesh);
 const victim=cinematicVictim(p,state);dressRig(victim,p.cosmetics);root.add(victim.group);buildWeapon(victim.weapon,p.spec);
 root.position.set(d.pos.x,d.pos.y,d.pos.z);root.rotation.y=d.yaw;const carry=new DeathPoseCarry(victim,original,.08);
 // Head gibs: chunks in the head's colours, blown out of the grip.
 const n=Math.max(6,Math.min(36,Math.floor(budget*.5),quality<.5?18:36)),gibs:{m:T.Mesh;v:T.Vector3;s:T.Vector3;spin:T.Vector3;size:number}[]=[];
 const headMat=new T.MeshLambertMaterial({color:p.cosmetics?.bodyColor??0xdce5e8}),accent=new T.MeshLambertMaterial({color:p.color}),char=new T.MeshLambertMaterial({color:0x0a080e});
 root.updateMatrixWorld(true);victim.head.getWorldPosition(wp);const headAt=wp.clone().add(new T.Vector3(0,.16,0));
 for(let i=0;i<n;i++){const m=new T.Mesh(cube,i%5===0?accent:i%3===0?char:headMat);const size=.045+rand()*.06;m.scale.setScalar(size);m.position.copy(headAt);m.castShadow=true;scene.add(m);
  const a=rand()*Math.PI*2,u=rand()*2-1,r=Math.sqrt(1-u*u),sp=2.5+rand()*3.5;gibs.push({m,size,v:new T.Vector3(Math.cos(a)*r*sp,Math.abs(u)*sp*.6+1.5,Math.sin(a)*r*sp),s:headAt.clone(),spin:new T.Vector3(rand()*14-7,rand()*14-7,rand()*14-7)});}
 const flash=new T.Sprite(haloMaterial(0xf3eeff,.9));flash.position.copy(headAt);scene.add(flash);
 const fall={start:d.pos.y,ground:.86},acc={v:0};let last=0,burst=false;
 // The body arrives already charred by the hold.
 victim.group.traverse(o=>{if(o instanceof T.Mesh&&o.material instanceof T.MeshLambertMaterial){const m=o.material.clone();m.color.lerp(new T.Color(0x0c0a10),.85);m.emissive.multiplyScalar(.2);o.material=m;owned.push(m);}});
 // `clock` drives the flames; `age` the body. A PvE downed member's body is kept lying, still smouldering (pve/downed.ts).
 const frame=(clock:number,age:number)=>{
  const dt=clamp(clock-last,0,.05);last=clock;tickSirenClock(clock);
  if(!burst){burst=true;fire.burst(headAt.x,headAt.y,headAt.z,quality<.5?40:80,4.2,.9,.12,'black',1.4);fire.burst(headAt.x,headAt.y,headAt.z,18,5,.6,.05,'ember',1);}
  carry.reset();victim.head.visible=false;
  // Limp hang (.45 s), the drop to the floor, then a crumple onto the back.
  const drop=clamp((age-.45)/.42),stand=Math.max(.86,fall.ground),lie=sm((age-.8)/.45);
  root.position.y=mix(fall.start-(fall.start-stand)*drop*drop,.2,lie);victim.group.rotation.x=-lie*1.48;victim.group.position.y=0;
  victim.spine.rotation.x=.25*(1-lie);victim.chest.rotation.x=.2*(1-lie);victim.neck.rotation.x=.4*(1-lie);
  victim.leftArm.rotation.set(-.15-.5*lie,0,.2+.6*lie);victim.rightArm.rotation.set(-.1-.4*lie,0,-.25-.7*lie);victim.leftElbow.rotation.x=-.3;victim.rightElbow.rotation.x=-.45;
  victim.leftLeg.rotation.set(-.1-.3*lie,0,.1);victim.rightLeg.rotation.set(-.05-.15*lie,0,-.12);victim.leftKnee.rotation.x=.25+.4*lie;victim.rightKnee.rotation.x=.15+.6*lie;
  carry.apply(age);
  // Burn, then come apart into black flame.
  const gone=sm((age-3.2)/1.2);victim.group.scale.setScalar(Math.max(.001,1-gone));
  burnRig(fire,victim,((quality<.5?30:70)*(age<3.6?1:1-gone)+(age>3.2&&age<4.4?120*(1-gone):0))*(clock>age+1e-6?.3:1),dt,acc);
  for(const g of gibs){const t=age;let y=g.s.y+g.v.y*t-6*t*t;const floor=.04;if(y<floor)y=floor;g.m.position.set(g.s.x+g.v.x*t*(y>floor?1:.6),y,g.s.z+g.v.z*t*(y>floor?1:.6));g.m.rotation.set(g.spin.x*Math.min(t,.9),g.spin.y*Math.min(t,.9),g.spin.z*Math.min(t,.9));g.m.visible=age<4.6;g.m.scale.setScalar(Math.max(.001,g.size*(1-sm((age-3.8)/.8))));}
  flash.scale.setScalar(1.8*Math.sqrt(clamp(age/.15)));(flash.material as T.SpriteMaterial).opacity=.9*(1-clamp(age/.3))**2;
  fire.update(dt);
 };
 return {count:n+2,rig:victim,root,extras:[fire.mesh,...gibs.map(g=>g.m)],keepAge:SIREN_CRUSH_KEEP,update(age){frame(age,age);},keep(age){frame(age,Math.min(age,SIREN_CRUSH_KEEP));},
 dispose(){scene.remove(root,fire.mesh,flash);for(const g of gibs)scene.remove(g.m);fire.dispose();(flash.material as T.Material).dispose();headMat.dispose();accent.dispose();char.dispose();for(const m of owned)m.dispose();}};
};

/**
 * Siren's own death: it recoils, rises and burns away from the feet up into black flame. The eyes and the grin hang
 * in the air last, then blink out.
 */
export const sirenDissipate:KillEffectFactory=(scene,original,p,d,quality)=>{
 seed=d.seed||11;const root=new T.Group();root.name='siren-dissipate';scene.add(root);root.position.set(d.pos.x,d.pos.y,d.pos.z);root.rotation.y=d.yaw;
 // A grown PvE Siren ("Am I beautiful?") burns away at its own size: the rig scales about the capsule centre like the live one.
 root.scale.setScalar(bossScaleOf(p));
 const rig=createCharacterRig(p.color,undefined,true),parts=buildSiren(rig);root.add(rig.group);parts.flames.mesh.visible=false;
 const fire=new FlameField(quality<.5?260:520);scene.add(fire.mesh);
 const carry=new DeathPoseCarry(rig,original,.12);root.updateMatrixWorld(true);
 const meshes:{m:T.Mesh;y:number;s:T.Vector3;done:boolean}[]=[];rig.group.traverse(o=>{if(o instanceof T.Mesh&&o.visible&&!/siren-(eye|teeth|flames|shadow)/.test(o.name)){o.getWorldPosition(wp);meshes.push({m:o,y:wp.y-d.pos.y,s:o.scale.clone(),done:false});}});
 const acc={v:0};let last=0;
 return {count:40,update(age){
  const dt=clamp(age-last,0,.05);last=age;tickSirenClock(age);carry.reset();
  // Recoil, then a slow rise with the arms spreading and the head falling back.
  const recoil=Math.sin(Math.PI*clamp(age/.45))*(1-clamp(age/.45)*.5),rise=sm((age-.3)/1.8);
  rig.body.position.set(0,.24+.7*rise,-.15*recoil);rig.body.rotation.set(-.4*recoil,0,0);rig.chest.rotation.set(-.35*recoil-.2*rise,0,0);rig.head.rotation.set(-.6*rise-.3*recoil,0,.5*(1-rise));
  rig.leftArm.rotation.set(-.4,0,.3+1.1*rise);rig.rightArm.rotation.set(-.4,0,-.3-1.1*rise);rig.leftElbow.rotation.x=-.4;rig.rightElbow.rotation.x=-.4;rig.leftKnee.rotation.x=1;rig.rightKnee.rotation.x=1.1;
  carry.apply(age);parts.state.open=clamp(age/.5);parts.state.squint=0;parts.state.anger=0;parts.state.time=age;applySirenLook(parts);
  // Burn away bottom-up: each part bursts into flame when the front passes it.
  const front=-1+2.4*sm((age-.5)/1.6);rig.group.updateMatrixWorld(true);
  for(const e of meshes){if(e.done)continue;if(e.y<front){e.done=true;e.m.getWorldPosition(wp);fire.burst(wp.x,wp.y,wp.z,quality<.5?4:9,1.6,.8,.1,'black',1.6);e.m.visible=false;}}
  burnRig(fire,rig,(quality<.5?40:90)*(1-sm((age-2)/.6)),dt,acc,1.2);
  // Eyes and grin linger, then blink out.
  const fade=1-sm((age-2.35)/.45);for(const eye of parts.eyes)eye.scale.multiplyScalar(1);parts.grin.scale.setScalar(Math.max(.001,fade));for(const eye of parts.eyes)eye.visible=fade>.05&&!(age>2.1&&Math.sin(age*40)>.6);
  for(const h of parts.eyeHalos)(h.material as T.SpriteMaterial).opacity=.42*fade;
  for(const t of parts.tongues)t.group.scale.set(1,t.height*(1+1.2*rise)*(1-sm((age-1.4)/.8)),1);
  fire.update(dt);
 },dispose(){scene.remove(root,fire.mesh);fire.dispose();}};
};
