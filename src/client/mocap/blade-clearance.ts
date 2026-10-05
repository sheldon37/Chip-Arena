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
/** Signed distance from chest-space point `p` to box `o` (negative inside); `n` gets the outward normal (chest space).
 * The normal is the nearest face's, blended with any face within INSIDE_BLEND of as near (`near`), so it turns smoothly
 * where the nearest face changes (a point deep in the head never flips its way out between frames). Outside, that blend
 * (weighted by INSIDE_BLEND) rides on the exact gradient, so the normal is continuous through the surface too: it matches
 * the inside one on the surface and is the gradient once the point is a blend width out. `medial`: inside, each face's
 * share also fades out within INSIDE_BLEND of the box's middle plane across it (where that face swaps for the opposite
 * one), so the normal shrinks to nothing there instead of flipping; it is then shorter than unit length. */
const INSIDE_BLEND=.02,near=(q:number,m:number)=>T.MathUtils.smoothstep(q-m,-INSIDE_BLEND,0),mid=(l:number)=>T.MathUtils.smoothstep(Math.abs(l),0,INSIDE_BLEND);
function sdf(o:typeof pose[number],p:T.Vector3,n:T.Vector3,medial=false){
 _l.copy(p).sub(o.c).applyQuaternion(o.qi);const qx=Math.abs(_l.x)-o.h.x,qy=Math.abs(_l.y)-o.h.y,qz=Math.abs(_l.z)-o.h.z,m=Math.max(qx,qy,qz),B=INSIDE_BLEND;
 const sx=Math.sign(_l.x)||1,sy=Math.sign(_l.y)||1,sz=Math.sign(_l.z)||1;
 if(m>0){const ox=Math.max(qx,0),oy=Math.max(qy,0),oz=Math.max(qz,0),d=Math.sqrt(ox*ox+oy*oy+oz*oz);
  n.set((ox+B*near(qx,m))*sx,(oy+B*near(qy,m))*sy,(oz+B*near(qz,m))*sz).normalize().applyQuaternion(o.q);return d;}
 if(medial){n.set(near(qx,m)*mid(_l.x)*sx,near(qy,m)*mid(_l.y)*sy,near(qz,m)*mid(_l.z)*sz);n.divideScalar(Math.max(1,n.length())).applyQuaternion(o.q);return m;}
 n.set(near(qx,m)*sx,near(qy,m)*sy,near(qz,m)*sz).normalize().applyQuaternion(o.q);return m;
}
/** The slab half-width of the call in progress (the blade's own: greatsword .09, sword .05, daggers .045). */
let halfWidth=BLADE_CLEARANCE.halfWidth;
/** One sampled point of the blade's slab: centre line and both edges, both flats (k 0..4) at `s` along the blade. */
function point(grip:T.Vector3,blade:T.Vector3,edge:T.Vector3,s:number,k:number,out:T.Vector3){
 const C=BLADE_CLEARANCE;return out.copy(grip).addScaledVector(blade,s).addScaledVector(edge,k===1?halfWidth:k===2?-halfWidth:0).addScaledVector(_y,k===3?C.halfThickness:k===4?-C.halfThickness:0);
}
/** Sample positions along the blade: the pommel and the blade, not the grip inside the fists. */
function* stations(from:number,to:number){const C=BLADE_CLEARANCE;for(let s=from;s<=-.05+1e-6;s+=C.step)yield s;for(let s=GUARD;s<=to+1e-6;s+=C.step)yield s;}
/** Where the blade (guard) starts along the weapon. */
export const GUARD=.22;
/** Smallest signed distance from the blade's sampled slab to the obstacles (negative: inside). */
export function bladeDistance(r:CharacterRig,grip:T.Vector3,blade:T.Vector3,edge:T.Vector3,from:number,to:number,count=boxPoses(r),width=BLADE_CLEARANCE.halfWidth){
 halfWidth=width;_y.crossVectors(blade,edge);let min=Infinity;
 for(const s of stations(from,to))for(let k=0;k<5;k++){point(grip,blade,edge,s,k,_p);for(let i=0;i<count;i++)min=Math.min(min,sdf(pose[i],_p,_hn));}
 return min;
}
const _hn=new T.Vector3(),_w=new T.Vector3(),_tr=new T.Vector3(),_na=new T.Vector3();
/** The floor in chest space: the rig's (group-space) clearance plane `FLOOR_CLEAR` as unit normal `floorN` (chest space)
 * and offset, so a chest-space point p is `p·floorN + floorD` above it. Group → body → pelvis → spine → chest. */
export const FLOOR_CLEAR=-.78;
const floorN=new T.Vector3(),_fq=new T.Quaternion(),_fp=new T.Vector3();let floorD=0;
function floorPlane(r:CharacterRig){
 _fq.identity();_fp.set(0,0,0);for(const n of [r.body,r.pelvis,r.spine,r.chest]){_fp.add(_t.copy(n.position).applyQuaternion(_fq));_fq.multiply(n.quaternion);}
 floorN.set(0,1,0).applyQuaternion(_fq.invert());floorD=_fp.y-FLOOR_CLEAR;
}
/** The contact normals of a 'turn' clearance, per (station, slab point, box), as the captured pose has them; `stamp`
 * marks the ones this call has taken. */
const MAX_STATIONS=64,NB=BLADE_OBSTACLES.length,turnN=new Float32Array(3*MAX_STATIONS*5*NB),stamp=new Int32Array(MAX_STATIONS*5*NB),_st:number[]=[],inReach=new Uint8Array(NB);let call=0;
const _b0=new T.Vector3(),_e0=new T.Vector3(),_y0=new T.Vector3(),_p0=new T.Vector3(),_c=new T.Vector3();
/** Push the blade clear (in place). `from`/`to`: the weapon's extent along the blade from the grip (pommel, blade end).
 * Every penetrating sample asks for the turn about the grip that lifts it out along its surface normal (or, near the hilt,
 * where there is no lever, for the grip to move); the requests are averaged with the deepest weighing most (pen^3) and applied, a few times over. Each
 * request grows from zero at the clearance, so the correction is continuous in the pose (no frame-to-frame snaps).
 * A turn-only clearance (`move` off: cuts) keeps each sample's way out as the captured pose had it (its first iteration's
 * normals): a blade through the middle of a part, whose samples ask out both ways, then settles between them instead of
 * the first iteration's slight lean deciding over or under (which flipped between neighbouring frames); a sample on a
 * part's middle plane asks for nothing (sdf `medial`). It leaves the
 * pommel be: on its short lever a pommel against the chest would swing the whole blade, and against several faces at
 * once those swings never settle (the converter keeps a two-handed capture's pommel off the body instead).
 * Each iteration only tests the parts the blade's slab can reach (none of their samples could ask for anything); a part
 * a turn-only clearance first reaches on a later iteration takes its normals from the captured pose all the same. */
export function clearBlade(r:CharacterRig,grip:T.Vector3,blade:T.Vector3,edge:T.Vector3,from:number,to:number,move=true,width=BLADE_CLEARANCE.halfWidth,floor=false){
 const C=BLADE_CLEARANCE,count=boxPoses(r),list=boxes(r);halfWidth=width;if(floor)floorPlane(r);
 _st.length=0;for(let s=from;s<=-.05+1e-6;s+=C.step)if(move)_st.push(s);for(let s=GUARD;s<=to+1e-6&&_st.length<MAX_STATIONS;s+=C.step)_st.push(s);
 const fixed=!move;if(fixed){call=call+1|0;_b0.copy(blade);_e0.copy(edge);_y0.crossVectors(blade,edge);}
 // A part can ask for something only within this of the blade's centre line (its slab, the clearance and, on a carry's
 // first iteration, the roll's reach).
 const slab=Math.sqrt(width*width+C.halfThickness*C.halfThickness),lo=_st[0]??0,hi=_st[_st.length-1]??0;
 for(let it=0;it<10;it++){
  _y.crossVectors(blade,edge);_w.set(0,0,0);_tr.set(0,0,0);_na.set(0,0,0);let total=0,nw=0,rmin=Infinity;
  const reach=slab+C.clearance+.004+(move&&it===0?C.rollRange:0)+.005;
  for(let i=0;i<count;i++){const o=pose[i];_c.copy(o.c).sub(grip);const t=Math.min(hi,Math.max(lo,_c.dot(blade)));inReach[i]=_c.addScaledVector(blade,-t).length()-o.h.length()<reach?1:0;}
  for(let si=0;si<_st.length;si++){const s=_st[si];for(let pk=0;pk<5;pk++){point(grip,blade,edge,s,pk,_p);
   // The floor (`floor`): a cut's follow-through turns up about the grip (a pure turn: the fist stays where it is).
   if(floor&&s>0){const d=_p.dot(floorN)+floorD,pen=C.clearance+.004-d;if(pen>0){const k=pen*pen*pen;_t.crossVectors(blade,floorN).multiplyScalar(pen*k/s);_w.add(_t);total+=k;}}
   for(let i=0;i<count;i++){if(!inReach[i])continue;const d=sdf(pose[i],_p,_hn,fixed);
    if(fixed){const q=(si*5+pk)*NB+i,j=3*q;
     if(stamp[q]!==call){stamp[q]=call;if(it>0){_p0.copy(grip).addScaledVector(_b0,s).addScaledVector(_e0,pk===1?halfWidth:pk===2?-halfWidth:0).addScaledVector(_y0,pk===3?C.halfThickness:pk===4?-C.halfThickness:0);sdf(pose[i],_p0,_hn,true);}
      turnN[j]=_hn.x;turnN[j+1]=_hn.y;turnN[j+2]=_hn.z;}
     else _hn.set(turnN[j],turnN[j+1],turnN[j+2]);}
    // Inside the shoulders and chest a carried blade's way out is up: it comes to rest on top of them, not under or
    // through (eased in over the first 3 cm of depth, so the push turns smoothly as the blade enters). A point whose
    // nearest way out points down (under the ribcage: a pommel) keeps it (faded out from level to 30° down, so the lift
    // never cancels the normal and flips it). A cut (`move` off) takes the nearest way out.
    if(d<0&&move&&list[i].lift){const u=T.MathUtils.smoothstep(-d,0,.03)*T.MathUtils.smoothstep(_hn.y,-.5,0);if(u>0){_hn.y+=1.2*u;_hn.normalize();}}
    if(it===0&&move&&s>0&&d<C.clearance+C.rollRange){const u=1-T.MathUtils.smoothstep(d,C.clearance,C.clearance+C.rollRange);_na.addScaledVector(_hn,u);nw+=u;rmin=Math.min(rmin,d);}
    const pen=C.clearance+.004-d;if(pen<=0)continue;
    // A blade resting on the shoulders keeps its lie: the grip rises (the hands lift it onto them) rather than the tip.
    const lever=T.MathUtils.smoothstep(Math.abs(s),.15,.6)*(move&&list[i].lift?.35:1);
    const k=pen*pen*pen;_t.crossVectors(blade,_hn).multiplyScalar(pen*k*lever/s);_w.add(_t);_tr.addScaledVector(_hn,pen*k*(1-.6*lever));total+=k;}}}
  // The tip itself (8 cm past the slab's last station) on the floor too.
  if(floor){const tip=to+.08;point(grip,blade,edge,tip,0,_p);const d=_p.dot(floorN)+floorD,pen=C.clearance+.004-d;if(pen>0){const k=pen*pen*pen;_t.crossVectors(blade,floorN).multiplyScalar(pen*k/tip);_w.add(_t);total+=k;}}
  if(it===0&&rmin<C.clearance+C.rollRange&&_na.lengthSq()>1e-8){
   // Lay the flat on the surface (carries, `move`; a cut keeps its captured edge): the edge turns perpendicular to the
   // contact normal (the closer, the more), toward the nearer of the two such edges; an edge pointing nearly into the
   // surface (both equally far), or contacts pushing opposite ways (their normals cancel), leave it, so the roll never
   // swaps sides (a 180° wrist flip) between frames. Only the blade lies on a surface: the pommel's contacts (a carry's
   // hilt against the chest) push but never roll it.
   const k=(1-T.MathUtils.smoothstep(rmin,C.clearance,C.clearance+C.rollRange))*T.MathUtils.smoothstep(_na.length()/Math.max(nw,1e-9),.3,.7);_ax.crossVectors(blade,_na.normalize());
   if(_ax.lengthSq()>1e-4){_ax.normalize();const c=_ax.dot(edge);if(c<0)_ax.negate();edge.lerp(_ax,k*T.MathUtils.smoothstep(Math.abs(c),0,.35)).addScaledVector(blade,-edge.dot(blade)).normalize();}
  }
  if(total<=0)break;
  _w.divideScalar(total);_tr.divideScalar(total);if(move)grip.add(_tr);
  const a=Math.min(.5,_w.length());if(a>1e-6){_rq.setFromAxisAngle(_w.normalize(),a);blade.applyQuaternion(_rq).normalize();edge.applyQuaternion(_rq).addScaledVector(blade,-edge.dot(blade)).normalize();}
  // Settled (a contact that cannot be cleared further keeps asking for less than this): stop iterating.
  if(a<2e-4&&(!move||_tr.lengthSq()<1e-8))break;
 }
}
