// PvE add hits, limbs and gibs (design §3.6, stream S1). Pure: classification of a hit into body parts, damage scaling,
// limb pools, sever rules and the consequences (crawl, limp, arm loss, head death). The horde applies the results.
import {gibPartAt,type GibPart} from '../body-hit.js';
import type {Vec} from '../config.js';
import {LIMB,type AddHitInfo,type LimbName} from './contract.js';
import {GIB} from './balance.js';

export type HitPart='torso'|'head'|'lShoulder'|'rShoulder'|'lElbow'|'rElbow'|'lHip'|'rHip'|'lKnee'|'rKnee';
/** Pool and severed limb of each part (the shoulder pool removes the whole arm, the elbow pool the forearm, …). */
export const PART_LIMB:Record<Exclude<HitPart,'torso'>,LimbName>={head:'head',lShoulder:'lArm',rShoulder:'rArm',lElbow:'lFore',rElbow:'rFore',
 lHip:'lLeg',rHip:'rLeg',lKnee:'lShin',rKnee:'rShin'};
/** Bits removed when a limb is severed (a whole arm or leg implies its forearm or shin). */
export const SEVER_BITS:Record<LimbName,number>={head:LIMB.head,lArm:LIMB.lArm|LIMB.lFore,rArm:LIMB.rArm|LIMB.rFore,lFore:LIMB.lFore,rFore:LIMB.rFore,
 lLeg:LIMB.lLeg|LIMB.lShin,rLeg:LIMB.rLeg|LIMB.rShin,lShin:LIMB.lShin,rShin:LIMB.rShin};
const FROM_GIB:Record<GibPart,HitPart>={head:'head',spine:'torso','left-shoulder':'lShoulder','right-shoulder':'rShoulder','left-elbow':'lElbow',
 'right-elbow':'rElbow','left-hip':'lHip','right-hip':'rHip','left-knee':'lKnee','right-knee':'rKnee'};
/** Joint offsets in the add's local frame (capsule centre origin; +x = the add's left, three.js yaw convention). */
const JOINT:Record<HitPart,[number,number,number]>={torso:[0,.1,0],head:[0,.6,0],lShoulder:[.3,.28,0],rShoulder:[-.3,.28,0],lElbow:[.36,-.05,0],
 rElbow:[-.36,-.05,0],lHip:[.14,-.33,0],rHip:[-.14,-.33,0],lKnee:[.14,-.66,0],rKnee:[-.14,-.66,0]};

export interface AddBodyState{pos:Vec;yaw:number;crawl:boolean;lost:number;maxHp:number}
/** World position of a part's joint (gib event position). */
export function jointWorld(a:{pos:Vec;yaw:number;crawl:boolean},part:HitPart):Vec{
 const [lx,ly,lz]=JOINT[part],c=Math.cos(a.yaw),s=Math.sin(a.yaw);
 const y=a.crawl?Math.min(ly,.1)*.4:ly;
 return {x:a.pos.x+c*lx+s*lz,y:a.pos.y+y,z:a.pos.z-s*lx+c*lz};
}
/** Severed parts are gone: a hit there counts on the torso. */
export function partLost(lost:number,part:HitPart){
 if(part==='torso')return false;const limb=PART_LIMB[part];return (lost&SEVER_BITS[limb])===SEVER_BITS[limb]||(lost&LIMB[limb])!==0;
}
/** Part from a hit point (§3.6.1). Upright adds use gibPartAt (body-hit.ts); crawlers: local z>.3 head, |x|>.25 arm. */
export function partAt(a:{pos:Vec;yaw:number;crawl:boolean},point:Vec):HitPart{
 if(!a.crawl)return FROM_GIB[gibPartAt(a.pos,a.yaw,point)];
 const dx=point.x-a.pos.x,dz=point.z-a.pos.z,c=Math.cos(a.yaw),s=Math.sin(a.yaw),x=c*dx-s*dz,z=s*dx+c*dz;
 if(z>.3)return 'head';if(Math.abs(x)>.25)return x>=0?(z>0?'lShoulder':'lElbow'):(z>0?'rShoulder':'rElbow');return 'torso';
}
export interface PartShare{part:HitPart;damage:number}
/** Splits a hit into per-part damage before the part multipliers (§3.6.1–2). Blasts: falloff by distance, 60% torso and
 *  40% over 1–2 parts weighted by blast height. No hit info: torso. A headshot flag forces the head. */
export function classifyAddHit(a:AddBodyState,damage:number,hit:AddHitInfo|undefined,rng:{next():number}):PartShare[]{
 if(!hit)return [{part:'torso',damage}];
 if(hit.kind==='blast'){
  const point=hit.point??a.pos,d=Math.hypot(point.x-a.pos.x,point.y-a.pos.y,point.z-a.pos.z),radius=hit.radius??3;
  const fall=1-Math.min(.85,Math.max(0,d/(radius+.4))),total=damage*fall,torso=total*GIB.blastTorsoShare,rest=total-torso;
  const low=point.y<.4;   // blast centre below 0.4 m (the Ward Zero floor is y=0): favour legs
  const pool:HitPart[]=a.crawl?['head','lShoulder','rShoulder','lElbow','rElbow']:low?['lKnee','rKnee','lHip','rHip','lKnee','rKnee','lElbow','rElbow']
   :['head','lShoulder','rShoulder','lElbow','rElbow','lHip','rHip','lKnee','rKnee'];
  const count=rng.next()<.5?1:2,out:PartShare[]=[{part:'torso',damage:torso}];
  for(let i=0;i<count;i++){const part=pool[Math.min(pool.length-1,Math.floor(rng.next()*pool.length))];out.push({part,damage:rest/count});}
  return out;
 }
 if(hit.headshot)return [{part:'head',damage}];
 if(!hit.point)return [{part:'torso',damage}];
 return [{part:partAt(a,hit.point),damage}];
}
/** Part damage multiplier: head ×1.5, limbs ×0.9, torso ×1. */
export const partMultiplier=(part:HitPart)=>part==='head'?GIB.headMultiplier:part==='torso'?1:GIB.limbMultiplier;
/** Sever threshold of a part's pool. */
export const severAt=(part:HitPart,maxHp:number,limbFrac:number,headFrac:number)=>part==='head'?headFrac*maxHp:limbFrac*maxHp;

/** Movement and attack consequences of the lost bits (§3.6.4, §3.5). */
export function limbState(lost:number){
 const lLeg=(lost&(LIMB.lLeg|LIMB.lShin))!==0,rLeg=(lost&(LIMB.rLeg|LIMB.rShin))!==0;
 const crawl=(lost&(LIMB.lLeg|LIMB.rLeg))!==0||lLeg&&rLeg;
 const limp=!crawl&&(lLeg||rLeg);
 const lArm=(lost&(LIMB.lArm|LIMB.lFore))!==0,rArm=(lost&(LIMB.rArm|LIMB.rFore))!==0;
 return {crawl,limp,armless:lArm&&rArm,oneArm:lArm!==rArm,lArm,rArm,headless:(lost&LIMB.head)!==0};
}
