// Distance impostors for PvE adds (design §8.2.3, stream S5). One InstancedMesh of unit boxes draws every far add as 11
// boxes (pelvis, chest, head, two upper arms, two forearms, two thighs, two shins) in one draw call, no shadows.
// impostorPose is a pure function of (view, time): the same output whatever frame rate sampled it, and after rewinds.
import * as T from 'three';
import {LIMB,PVE_TIMING,type PveAddView} from '../../shared/pve/contract.js';
import {MINION_PALETTE} from '../siren/minions/materials.js';
import {entryDuration,entryWalkStart,type MinionEntry} from '../siren/minions/motion.js';
import {DEATH_SECONDS} from '../siren/minions/fx.js';

/** Boxes per impostor, in this order. */
export const IMPOSTOR_PARTS=['pelvis','chest','head','lUpper','rUpper','lFore','rFore','lThigh','rThigh','lShin','rShin'] as const;
export const IMPOSTOR_BOXES=IMPOSTOR_PARTS.length;
/** Floats per box in an impostorPose buffer: a column-major 4x4 world matrix. */
export const IMPOSTOR_STRIDE=16;
/** What impostorPose reads: the decoded add plus the client-side entry memo and the ground height under it. */
export type ImpostorView=Pick<PveAddView,'pos'|'yaw'|'vel'|'state'|'lost'|'side'|'attack'|'kind'>&
 Partial<Pick<PveAddView,'attackAt'|'deathAt'|'diveAt'|'spawnAt'|'hitAt'|'hitYaw'>>&{entry?:MinionEntry;ground?:number};

const STAND=.85,CRAWL=.44,TAU=Math.PI*2;
const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
const sm=(x:number)=>{x=clamp(x);return x*x*(3-2*x);};
/** Ground height under an add from its capsule centre (upright .85, crawling .44 above the floor). */
export const addGround=(v:Pick<PveAddView,'pos'|'state'>)=>{const g=v.pos.y-(v.state==='crawl'?CRAWL:STAND);return Math.abs(g)<.1?0:g;};

// Box sizes (m) and the joint layout of the Pilot rig, flattened (character-rig.ts): pelvis at .62 above the floor.
const SIZE:Record<typeof IMPOSTOR_PARTS[number],[number,number,number]>={pelvis:[.42,.22,.28],chest:[.52,.42,.3],head:[.34,.36,.32],
 lUpper:[.17,.3,.18],rUpper:[.17,.3,.18],lFore:[.16,.34,.17],rFore:[.16,.34,.17],lThigh:[.19,.3,.21],rThigh:[.19,.3,.21],lShin:[.17,.36,.19],rShin:[.17,.36,.19]};
const LOST:Record<typeof IMPOSTOR_PARTS[number],number>={pelvis:0,chest:0,head:LIMB.head,lUpper:LIMB.lArm,rUpper:LIMB.rArm,lFore:LIMB.lArm|LIMB.lFore,rFore:LIMB.rArm|LIMB.rFore,
 lThigh:LIMB.lLeg,rThigh:LIMB.rLeg,lShin:LIMB.lLeg|LIMB.lShin,rShin:LIMB.rLeg|LIMB.rShin};

const mRoot=new T.Matrix4(),mBody=new T.Matrix4(),mChest=new T.Matrix4(),mTmp=new T.Matrix4(),mSeg=new T.Matrix4(),mJoint=new T.Matrix4();
const q=new T.Quaternion(),e=new T.Euler(),p=new T.Vector3(),s=new T.Vector3(),ONE=new T.Vector3(1,1,1);
const write=(out:Float32Array,i:number,m:T.Matrix4)=>{out.set(m.elements,i*IMPOSTOR_STRIDE);};
const hide=(out:Float32Array,i:number)=>{out.fill(0,i*IMPOSTOR_STRIDE,(i+1)*IMPOSTOR_STRIDE);};
/** A box hanging from a joint: joint matrix = parent · T(x,y,z) · R(rx,ry,rz); the box centre sits `len/2` down its local y. */
function limb(out:Float32Array,i:number,parent:T.Matrix4,x:number,y:number,z:number,rx:number,ry:number,rz:number,size:[number,number,number],child?:T.Matrix4){
 mJoint.compose(p.set(x,y,z),q.setFromEuler(e.set(rx,ry,rz)),ONE).premultiply(parent);
 mSeg.compose(p.set(0,-size[1]/2,0),q.identity(),s.set(...size)).premultiply(mJoint);write(out,i,mSeg);
 if(child)child.copy(mJoint);
}
const mElbow=new T.Matrix4();

/**
 * Writes the 11 box matrices of an impostor for `v` at authority time `t` into `out` (IMPOSTOR_BOXES·16 floats from
 * `offset`). Returns false when nothing shows (fully burned away, dived, still deep in a vent). Lost limbs get a zero
 * matrix. Covers the gait phase from |vel|, swipe/lunge/smash arm raises from attackAt, the crawl, the vent climb
 * (from the entry), the door/cell walk-out (the authority moves it), the vent dive and the death sink from deathAt.
 */
export function impostorPose(v:ImpostorView,t:number,out:Float32Array,offset=0):boolean{
 const o=offset/IMPOSTOR_STRIDE,ground=v.ground??addGround(v),speed=Math.hypot(v.vel.x,v.vel.z),crawl=v.state==='crawl';
 let rise=0,pitch=crawl?1.3:.12,hunch=.2;
 // Entry, dive and death adjust the body height and pitch.
 const en=v.entry;
 if(en&&t-en.at<entryDuration(en)){const a=t-en.at+(en.kind==='vent'&&!en.first?PVE_TIMING.vent.push:0);
  if(en.kind==='vent'){rise=-1.4*(1-sm((a-.2)/1.6));pitch=.12+.5*Math.sin(Math.PI*clamp((a-.9)/.9));}
  else{const wake=entryWalkStart(en)-.4;hunch=.2+.5*(1-sm((t-en.at-wake)/.4));}}
 if(v.diveAt!==undefined&&t>=v.diveAt){const d=t-v.diveAt;if(d>=PVE_TIMING.vent.dive)return blank(out,o);rise=-2*sm((d-.45)/.45)-.3*sm(d/.3);pitch=.12+.6*sm(d/.3);}
 let fall=0;
 if(v.deathAt!==undefined&&t>=v.deathAt){const d=t-v.deathAt;if(d>=DEATH_SECONDS)return blank(out,o);fall=crawl?0:sm((d-.25)/.7);rise-=.45*fall+.9*sm((d-1.3)/1)*1;}
 if(rise<-1.85)return blank(out,o);
 // Root: ground under the add, yaw; the body leans (crawl: lies prone) and drops when it falls.
 mRoot.compose(p.set(v.pos.x,ground,v.pos.z),q.setFromEuler(e.set(0,v.yaw,0)),ONE);
 const f=crawl?Math.max(.55,speed*.6/.4):Math.max(.95,speed*.66/.44),ph=TAU*t*f,mv=sm((speed-.08)/.4);
 const bob=crawl?0:-.03*Math.abs(Math.cos(ph))*mv;
 const bodyY=crawl?.26:.62+bob,bodyPitch=pitch+(crawl?0:1.25*fall);
 mBody.compose(p.set(0,bodyY+rise,crawl?-.1:0),q.setFromEuler(e.set(bodyPitch,crawl?.1*Math.sin(ph):.08*Math.sin(ph)*mv,0)),ONE).premultiply(mRoot);
 // Pelvis and chest boxes; head on top, tilted forward by the hunch.
 mTmp.compose(p.set(0,0,0),q.identity(),s.set(...SIZE.pelvis)).premultiply(mBody);write(out,o+0,mTmp);
 mChest.compose(p.set(0,.12,0),q.setFromEuler(e.set(hunch,crawl?-.12*Math.sin(ph):0,0)),ONE).premultiply(mBody);
 mTmp.compose(p.set(0,.25,0),q.identity(),s.set(...SIZE.chest)).premultiply(mChest);write(out,o+1,mTmp);
 if(v.lost&LIMB.head)hide(out,o+2);else{mTmp.compose(p.set(0,.66,.02),q.setFromEuler(e.set(crawl?-1.1:-.1,0,0)),s.set(...SIZE.head)).premultiply(mChest);write(out,o+2,mTmp);}
 // Arms: gait swing (a crawl pulls alternately overhead), raised by attacks.
 const a=v.attackAt!==undefined?t-v.attackAt:-1,atk=a>=0&&a<.9?Math.sin(Math.PI*clamp(a/.9)):0;
 for(const sd of [1,-1] as const){
  const L=sd>0,up=L?'lUpper':'rUpper',fo=L?'lFore':'rFore',swing=crawl?-2.4+.5*Math.sin(ph+(L?0:Math.PI)):-.35-.35*Math.sin(ph+(L?Math.PI:0))*mv;
  const armSide=v.side===-1?1:-1,raise=v.attack==='swipe'?(sd===armSide?atk:0):atk;
  const ax=swing-1.4*raise,az=sd*(.12+(v.attack==='swipe'?.9:.15)*raise*(a<.34?1:-1));
  if(v.lost&LOST[up])hide(out,o+(L?3:4));else limb(out,o+(L?3:4),mChest,sd*.34,.43,0,ax,0,az,SIZE[up],mElbow);
  if(v.lost&LOST[fo]||v.lost&LOST[up])hide(out,o+(L?5:6));else limb(out,o+(L?5:6),mElbow,0,-.27,0,-.5-.4*raise,0,0,SIZE[fo]);
 }
 // Legs: walking gait (crawl: trailing); a lost shin limps.
 for(const sd of [1,-1] as const){
  const L=sd>0,th=L?'lThigh':'rThigh',sh=L?'lShin':'rShin',k=Math.sin(ph+(L?0:Math.PI));
  const hx=crawl?.1+.15*Math.max(0,k):(.45*k*mv)-.6*fall,knee=crawl?.3:.15+.55*Math.max(0,-Math.cos(ph+(L?0:Math.PI)))*mv+1.2*fall;
  if(v.lost&LOST[th])hide(out,o+(L?7:8));else limb(out,o+(L?7:8),mBody,sd*.13,-.06,0,-hx,0,sd*.04,SIZE[th],mElbow);
  if(v.lost&LOST[sh]||v.lost&LOST[th])hide(out,o+(L?9:10));else limb(out,o+(L?9:10),mElbow,0,-.27,0,knee,0,0,SIZE[sh]);
 }
 return true;
}
function blank(out:Float32Array,o:number){out.fill(0,o*IMPOSTOR_STRIDE,(o+IMPOSTOR_BOXES)*IMPOSTOR_STRIDE);return false;}

/** Colours per box (sRGB hex): the ashen shell, darker joints and a sootier head, so far adds read as the same family. */
const BOX_COLORS=[0x8d917f,0xa3a793,0xa9ad99,0x9a9e8a,0x9a9e8a,0x8a8d7b,0x8a8d7b,0x969a86,0x969a86,0x7f8272,0x7f8272];

/** The impostor batch: one InstancedMesh (BoxGeometry(1,1,1), MeshLambertMaterial, instanceColor), capacity max·11. */
export class ImpostorBatch {
 readonly mesh:T.InstancedMesh;private buf=new Float32Array(IMPOSTOR_BOXES*IMPOSTOR_STRIDE);private used=0;private m=new T.Matrix4();
 /** Adds drawn this frame. */
 adds=0;
 constructor(readonly max:number){
  this.mesh=new T.InstancedMesh(new T.BoxGeometry(1,1,1),new T.MeshLambertMaterial({color:0xffffff,emissive:0x23241f}),max*IMPOSTOR_BOXES);this.mesh.name='pve-add-impostors';
  this.mesh.castShadow=false;this.mesh.receiveShadow=false;this.mesh.frustumCulled=false;this.mesh.instanceMatrix.setUsage(T.DynamicDrawUsage);
  const c=new T.Color(),soot=new T.Color(MINION_PALETTE.soot);
  for(let i=0;i<max*IMPOSTOR_BOXES;i++)this.mesh.setColorAt(i,c.setHex(BOX_COLORS[i%IMPOSTOR_BOXES]).lerp(soot,.08));
  this.mesh.count=0;
 }
 begin(){this.used=0;this.adds=0;}
 /** Adds one impostor; false when full or nothing shows. */
 add(v:ImpostorView,t:number){
  if(this.adds>=this.max)return false;if(!impostorPose(v,t,this.buf))return false;
  for(let k=0;k<IMPOSTOR_BOXES;k++){this.m.fromArray(this.buf,k*IMPOSTOR_STRIDE);this.mesh.setMatrixAt(this.used++,this.m);}
  this.adds++;return true;
 }
 end(){this.mesh.count=this.used;this.mesh.instanceMatrix.needsUpdate=true;this.mesh.visible=this.used>0;}
 dispose(){this.mesh.removeFromParent();this.mesh.geometry.dispose();(this.mesh.material as T.Material).dispose();this.mesh.dispose();}
}
