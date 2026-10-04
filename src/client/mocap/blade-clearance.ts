import * as T from 'three';
import type {CharacterRig} from '../character-rig.js';
/** Blade clearance for captured weapon poses (presentation only). A human capture rests the blade on a narrow shoulder;
 * the voxel rig's shoulders, chest and head are wider and blockier, so a retargeted carry can run the blade through them.
 * Given the main grip (the wrist; the weapon origin) and the blade/edge directions in chest space, the blade is turned
 * about the grip (and, when the contact is at the hilt, the grip is shifted) until every sampled point of the blade's slab
 * clears the shoulder caps, chest shell and head by CLEARANCE; within ROLL_RANGE of a contact the blade rolls so its flat
 * faces the surface, so a carried blade lies flat on the pauldron instead of standing on its edge. The grip inside the fists
 * is not tested (the hands hold it against the body). Stateless: the same pose always gives the same blade. */
export const BLADE_CLEARANCE={clearance:.02,rollRange:.07,halfWidth:.09,halfThickness:.025,step:.07};
/** The body parts the blade must clear (rig part names, character-rig.ts). */
export const BLADE_OBSTACLES=['right-shoulder-cap','left-shoulder-cap','ribcage','upper-chest','collar','back-panel','sternum','blank-head','crown','jaw','neck-joint'] as const;
/** `lift`: the obstacle's way out is upward (shoulders and chest; the blade comes to rest on top of them). */
type Box={chain:T.Object3D[];mesh:T.Object3D;lift:boolean};
const cache=new WeakMap<CharacterRig,Box[]>();
function boxes(r:CharacterRig){
 let list=cache.get(r);if(list)return list;list=[];
 for(const name of BLADE_OBSTACLES){const mesh=r.group.getObjectByName(name);if(!mesh)continue;const chain:T.Object3D[]=[];for(let n=mesh.parent;n&&n!==r.chest;n=n.parent)chain.unshift(n);list.push({chain,mesh,lift:/shoulder-cap|upper-chest|collar|ribcage|back-panel/.test(name)});}
 cache.set(r,list);return list;
}
/** Box poses in chest space for this frame (centre, inverse rotation, half extents). */
const pose=Array.from({length:BLADE_OBSTACLES.length},()=>({c:new T.Vector3(),qi:new T.Quaternion(),h:new T.Vector3(),q:new T.Quaternion()}));
function boxPoses(r:CharacterRig){
 const list=boxes(r);
 for(let i=0;i<list.length;i++){const b=list[i],o=pose[i];o.c.set(0,0,0);o.q.identity();
  for(const n of b.chain){o.c.add(_t.copy(n.position).applyQuaternion(o.q));o.q.multiply(n.quaternion);}
  o.c.add(_t.copy(b.mesh.position).applyQuaternion(o.q));o.q.multiply(b.mesh.quaternion);o.qi.copy(o.q).invert();o.h.copy(b.mesh.scale).multiplyScalar(.5);}
 return list.length;
}
const _t=new T.Vector3(),_l=new T.Vector3(),_n=new T.Vector3(),_p=new T.Vector3(),_y=new T.Vector3(),_ax=new T.Vector3(),_rq=new T.Quaternion();
/** Signed distance from chest-space point `p` to box `o` (negative inside); `n` gets the outward normal (chest space). */
function sdf(o:typeof pose[number],p:T.Vector3,n:T.Vector3){
 _l.copy(p).sub(o.c).applyQuaternion(o.qi);const qx=Math.abs(_l.x)-o.h.x,qy=Math.abs(_l.y)-o.h.y,qz=Math.abs(_l.z)-o.h.z;
 if(qx>0||qy>0||qz>0){n.set(Math.max(qx,0)*Math.sign(_l.x),Math.max(qy,0)*Math.sign(_l.y),Math.max(qz,0)*Math.sign(_l.z));const d=n.length();n.divideScalar(d||1).applyQuaternion(o.q);return d;}
 if(qx>=qy&&qx>=qz)n.set(Math.sign(_l.x)||1,0,0);else if(qy>=qz)n.set(0,Math.sign(_l.y)||1,0);else n.set(0,0,Math.sign(_l.z)||1);n.applyQuaternion(o.q);return Math.max(qx,qy,qz);
}
/** One sampled point of the blade's slab: centre line and both edges, both flats (k 0..4) at `s` along the blade. */
function point(grip:T.Vector3,blade:T.Vector3,edge:T.Vector3,s:number,k:number,out:T.Vector3){
 const C=BLADE_CLEARANCE;return out.copy(grip).addScaledVector(blade,s).addScaledVector(edge,k===1?C.halfWidth:k===2?-C.halfWidth:0).addScaledVector(_y,k===3?C.halfThickness:k===4?-C.halfThickness:0);
}
/** Sample positions along the blade: the pommel and the blade, not the grip inside the fists. */
function* stations(from:number,to:number){const C=BLADE_CLEARANCE;for(let s=from;s<=-.05+1e-6;s+=C.step)yield s;for(let s=GUARD;s<=to+1e-6;s+=C.step)yield s;}
/** Where the blade (guard) starts along the weapon. */
export const GUARD=.22;
/** Smallest signed distance from the blade's sampled slab to the obstacles (negative: inside). */
export function bladeDistance(r:CharacterRig,grip:T.Vector3,blade:T.Vector3,edge:T.Vector3,from:number,to:number,count=boxPoses(r)){
 _y.crossVectors(blade,edge);let min=Infinity;
 for(const s of stations(from,to))for(let k=0;k<5;k++){point(grip,blade,edge,s,k,_p);for(let i=0;i<count;i++)min=Math.min(min,sdf(pose[i],_p,_hn));}
 return min;
}
const _hn=new T.Vector3(),_w=new T.Vector3(),_tr=new T.Vector3(),_na=new T.Vector3();
/** Push the blade clear (in place). `from`/`to`: the weapon's extent along the blade from the grip (pommel, blade end).
 * Every penetrating sample asks for the turn about the grip that lifts it out along its surface normal (or, near the hilt,
 * where there is no lever, for the grip to move); the requests are averaged with the deepest weighing most (pen^3) and applied, a few times over. Each
 * request grows from zero at the clearance, so the correction is continuous in the pose (no frame-to-frame snaps). */
export function clearBlade(r:CharacterRig,grip:T.Vector3,blade:T.Vector3,edge:T.Vector3,from:number,to:number,move=true){
 const C=BLADE_CLEARANCE,count=boxPoses(r),list=boxes(r);
 for(let it=0;it<10;it++){
  _y.crossVectors(blade,edge);_w.set(0,0,0);_tr.set(0,0,0);_na.set(0,0,0);let total=0,min=Infinity;
  for(const s of stations(from,to))for(let k=0;k<5;k++){point(grip,blade,edge,s,k,_p);
   for(let i=0;i<count;i++){const d=sdf(pose[i],_p,_hn);min=Math.min(min,d);
    // Inside the shoulders and chest the way out is up: the blade comes to rest on top of them, not under or through.
    if(d<0&&list[i].lift){_hn.y+=1.2;_hn.normalize();}
    if(it===0&&d<C.clearance+C.rollRange)_na.addScaledVector(_hn,1-T.MathUtils.smoothstep(d,C.clearance,C.clearance+C.rollRange));
    const pen=C.clearance+.004-d;if(pen<=0)continue;
    // A blade resting on the shoulders keeps its lie: the grip rises (the hands lift it onto them) rather than the tip.
    const lever=T.MathUtils.smoothstep(Math.abs(s),.15,.6)*(move&&list[i].lift?.35:1);
    const k=pen*pen*pen;_t.crossVectors(blade,_hn).multiplyScalar(pen*k*lever/s);_w.add(_t);_tr.addScaledVector(_hn,pen*k*(1-.6*lever));total+=k;}}
  if(it===0&&min<C.clearance+C.rollRange&&_na.lengthSq()>1e-8){
   // Lay the flat on the surface: the edge turns perpendicular to the contact normal (the closer, the more).
   const k=1-T.MathUtils.smoothstep(min,C.clearance,C.clearance+C.rollRange);_ax.crossVectors(blade,_na.normalize());
   if(_ax.lengthSq()>1e-4){_ax.normalize();if(_ax.dot(edge)<0)_ax.negate();edge.lerp(_ax,k).addScaledVector(blade,-edge.dot(blade)).normalize();}
  }
  if(total<=0)break;
  _w.divideScalar(total);_tr.divideScalar(total);if(move)grip.add(_tr);
  const a=Math.min(.5,_w.length());if(a>1e-6){_rq.setFromAxisAngle(_w.normalize(),a);blade.applyQuaternion(_rq).normalize();edge.applyQuaternion(_rq).addScaledVector(blade,-edge.dot(blade)).normalize();}
 }
}
