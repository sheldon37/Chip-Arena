// PvE vent pits, presentation (owner 2026-10-04; authority in src/shared/pve/vent-pits.ts). Two kill effects:
//  'vent-fall'  the body drops into the open vent's black shaft (arms up, flailing) and is gone below the floor. No body is
//               exposed: it is in the shaft, so a respawn during the shaft time simply materialises the member on the pad.
//  'vent-splat' VENT_PIT.splatAt s later the body falls from under the vault onto the clear spot the authority chose, lands
//               at exactly death.at + VENT_PIT.drop with a cartoon splat (stretch in the fall, squash and wobble on impact, a
//               flattened spread-eagle pose, a dust/voxel puff and a SPLAT! pop) and stays as an ordinary kept downed body:
//               its rig is exposed so the revive regeneration (downed.ts) scans it and the player stands up from it.
// Pure functions of age; HP, hitboxes, timing and the authority are untouched.
import * as T from 'three';
import type {KillEffectFactory} from '../death-effects.js';
import {cinematicVictim,DeathPoseCarry} from '../cinematic-victim.js';
import {dressRig} from '../cosmetics.js';
import {buildWeapon} from '../weapon-visuals.js';
import {VENT_PIT} from '../../shared/pve/vent-pits.js';
import {HOLO_FONT,makeCanvas} from './hologram.js';

const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
const sm=(x:number)=>{x=clamp(x);return x*x*(3-2*x);};
const cube=new T.BoxGeometry(1,1,1);
export const VENT_FX={
 /** Shaft drop: a short stumble, then gravity (m/s²); hidden once this far below the floor. */
 stumble:.1,gravity:24,hideBelow:4,
 /** Splat: fall height above the spot, the lying body's centre height, squash on impact and the flattened rest scale. */
 height:13,lie:.17,squash:.34,rest:.62,
 /** The kept body holds the settled pose from this age (impact + wobble + puff). */
 keepAge:VENT_PIT.drop+1.3,
 /** Dust puff voxels and the SPLAT! pop. */
 dust:{high:26,low:12,speed:3.4,life:.9},pop:{life:1.25,size:1.5},
} as const;

/** Into the pit: the victim keeps its last pose for an instant, throws its arms up and drops out of sight. */
export const ventFall:KillEffectFactory=(scene,original,p,d,_quality,_budget,state)=>{
 const root=new T.Group();root.name='vent-fall';scene.add(root);
 const victim=cinematicVictim(p,state);dressRig(victim,p.cosmetics);buildWeapon(victim.weapon,p.spec);root.add(victim.group);
 root.position.set(d.pos.x,d.pos.y,d.pos.z);root.rotation.y=d.yaw;const carry=new DeathPoseCarry(victim,original,.1);
 const frame=(age:number)=>{
  carry.reset();const t=Math.max(0,age-VENT_FX.stumble),drop=.5*VENT_FX.gravity*t*t,flail=Math.sin(age*31);
  root.position.y=d.pos.y-drop;root.visible=drop<VENT_FX.hideBelow;
  victim.leftArm.rotation.set(-.3,0,2.5+.35*flail);victim.rightArm.rotation.set(-.3,0,-2.5+.35*flail);victim.leftElbow.rotation.x=-.4;victim.rightElbow.rotation.x=-.4;
  victim.leftLeg.rotation.x=-.5+.6*flail;victim.rightLeg.rotation.x=-.5-.6*flail;victim.leftKnee.rotation.x=.7;victim.rightKnee.rotation.x=.7;
  victim.head.rotation.x=-.45;victim.group.rotation.y=age*1.6;
  carry.apply(age);
 };
 return {count:2,update:frame,keep:frame,dispose(){scene.remove(root);}};
};

let popMap:T.Texture|undefined;
function popTexture(){
 if(popMap)return popMap;const c=makeCanvas(256,128);
 if(c){const {ctx}=c;ctx.clearRect(0,0,256,128);ctx.textAlign='center';ctx.textBaseline='middle';ctx.font=`900 70px ${HOLO_FONT}`;ctx.lineJoin='round';
  ctx.save();ctx.translate(128,64);ctx.rotate(-.12);ctx.lineWidth=14;ctx.strokeStyle='#2b1030';ctx.strokeText('SPLAT!',0,4);ctx.fillStyle='#ffd23a';ctx.fillText('SPLAT!',0,0);
  ctx.lineWidth=3;ctx.strokeStyle='#fff6d0';ctx.strokeText('SPLAT!',0,0);ctx.restore();
  popMap=new T.CanvasTexture(c.canvas as HTMLCanvasElement);popMap.colorSpace=T.SRGBColorSpace;}
 else popMap=new T.Texture();
 return popMap;
}
/** Seeded dust directions. */
function dustDir(seed:number,i:number){let s=(seed^Math.imul(i+1,0x9e3779b1))>>>0;const r=()=>(s=(Math.imul(s,1664525)+1013904223)>>>0)/4294967296;
 const a=i/7.3+r()*.8;return {x:Math.sin(a),z:Math.cos(a),k:.55+.45*r(),up:.6+r()*1.6,size:.07+r()*.09,spin:r()*12-6};}

/** From the vault onto the spot: stretched in the fall, squashed flat on impact, a wobble, then a pancake on the floor. */
export const ventSplat:KillEffectFactory=(scene,_original,p,d,quality,_budget,state)=>{
 const root=new T.Group();root.name='vent-splat';scene.add(root);const F=VENT_FX,impact=VENT_PIT.drop;
 const victim=cinematicVictim(p,state);dressRig(victim,p.cosmetics);buildWeapon(victim.weapon,p.spec);root.add(victim.group);
 const floor=d.pos.y-.85,ground=floor+F.lie;root.position.set(d.pos.x,ground,d.pos.z);root.rotation.y=d.yaw;
 const n=quality<.5?F.dust.low:F.dust.high,dust:{m:T.Mesh;dir:ReturnType<typeof dustDir>}[]=[];
 const dustMat=new T.MeshLambertMaterial({color:0x8d8597,transparent:true}),chip=new T.MeshLambertMaterial({color:p.color,transparent:true});
 for(let i=0;i<n;i++){const m=new T.Mesh(cube,i%5===0?chip:dustMat);m.visible=false;scene.add(m);dust.push({m,dir:dustDir(d.seed||7,i)});}
 const pop=new T.Sprite(new T.SpriteMaterial({map:popTexture(),transparent:true,depthTest:false,depthWrite:false,toneMapped:false}));pop.renderOrder=6;pop.visible=false;scene.add(pop);
 const frame=(age:number)=>{
  const fall=clamp(age/impact),since=age-impact,landed=since>=0;
  // The pose: face down, spread-eagle (arms and legs out, the head up a little); limbs flap while falling.
  const flap=landed?0:Math.sin(age*26)*.25;
  victim.group.rotation.set(Math.PI/2,0,0);victim.group.position.set(0,0,0);
  victim.leftArm.rotation.set(0,0,1.35+flap);victim.rightArm.rotation.set(0,0,-1.35-flap);victim.leftElbow.rotation.x=-.15;victim.rightElbow.rotation.x=-.15;
  victim.leftLeg.rotation.set(0,0,.42-flap);victim.rightLeg.rotation.set(0,0,-.42+flap);victim.leftKnee.rotation.x=.1;victim.rightKnee.rotation.x=.1;victim.head.rotation.x=landed?-.25:-.6;
  if(!landed){
   root.position.y=ground+F.height*(1-fall*fall);
   const st=1+.32*fall;root.scale.set(1/Math.sqrt(st),st,1/Math.sqrt(st));
  }else{
   root.position.y=ground;
   // Squash at once, then a damped wobble settling on the flattened rest scale.
   const y=F.rest+(F.squash-F.rest)*Math.exp(-since*7)*Math.cos(since*26);
   const xz=1/Math.sqrt(Math.max(.2,y));root.scale.set(xz,y,xz);
  }
  // Dust and voxel puff along the floor.
  for(const g of dust){const t=since,on=landed&&t<F.dust.life;g.m.visible=on;if(!on)continue;
   const r=F.dust.speed*g.dir.k*(1-Math.exp(-t*4))/4,h=Math.max(0,g.dir.up*t-3.2*t*t)*.6,fade=1-sm((t-.35)/(F.dust.life-.35));
   g.m.position.set(d.pos.x+g.dir.x*(.5+r),floor+.05+h+g.dir.size/2,d.pos.z+g.dir.z*(.5+r));g.m.rotation.set(g.dir.spin*t,g.dir.spin*.7*t,0);g.m.scale.setScalar(Math.max(.001,g.dir.size*(.6+.8*fade)));}
  dustMat.opacity=chip.opacity=landed?1-sm((since-.35)/(F.dust.life-.35)):1;
  // SPLAT! pops over the body: overshoot, hold, float up and fade.
  const pl=landed&&since<F.pop.life;pop.visible=pl;
  if(pl){const grow=since<.12?since/.12*1.25:since<.24?1.25-(since-.12)/.12*.25:1,s=F.pop.size*grow;pop.scale.set(s,s/2,1);pop.position.set(d.pos.x,floor+1.3+since*.45,d.pos.z);(pop.material as T.SpriteMaterial).opacity=1-sm((since-.8)/(F.pop.life-.8));}
 };
 return {count:n+2,rig:victim,root,extras:[...dust.map(g=>g.m),pop],keepAge:F.keepAge,update:frame,keep(age){frame(Math.min(age,F.keepAge));},
  dispose(){scene.remove(root,pop);for(const g of dust)scene.remove(g.m);dustMat.dispose();chip.dispose();(pop.material as T.Material).dispose();}};
};
export const VENT_KILL_EFFECTS:Record<string,KillEffectFactory>={'vent-fall':ventFall,'vent-splat':ventSplat};
