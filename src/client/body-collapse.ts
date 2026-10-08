import * as T from 'three';
import type {DeathMotion} from '../shared/body-damage.js';

/**
 * The grounded fast-headshot death (docs/BODY-DAMAGE-HANDOFF.md; the authority chooses it with DeathMotion 'kneel-fall'):
 * impact jolt → knees buckle → forward fall → articulated ragdoll. It is forward kinematics over the ragdoll's own part
 * frames, a pure function of the time since death: late, repeated or skipped snapshots show the same frame, and the bodies
 * are handed to physics at a step boundary with the pose's own velocities, so the fall continues without a pop.
 * Hinged joints (knees, elbows, neck) only ever turn about their hinge axis, inside the ragdoll's limits. The ragdoll has
 * no ankle body, so the feet pivot visually inside the shin part (kept flat on the floor while the knees go down).
 */
export interface CollapseBox {center:T.Vector3;half:T.Vector3;quaternion:T.Quaternion;
 /** Part of the foot (turns about the ankle). */
 ankle?:boolean}
export interface CollapsePart {
 /** Rig joint name of the part (pelvis, spine, head, left-hip...). */
 name:string;parent?:string;
 /** Body frame at death (world). */
 position:T.Vector3;quaternion:T.Quaternion;
 /** Joint position in the parent's frame; constant while the joint holds. */
 anchor?:T.Vector3;
 /** Detached at death (a severed head): physics from the first frame, never driven. */
 free?:boolean;
 /** Body mesh boxes in the part frame (ground contact of the kneel). */
 boxes:CollapseBox[];
 /** Knee parts: the ankle joint in the part frame at death (pivot and rotation of the foot frame). */
 ankle?:{pivot:T.Vector3;frame:T.Quaternion};
}
export interface CollapsePose {position:T.Vector3;quaternion:T.Quaternion;
 /** Knee parts: rotation of the foot meshes about the ankle pivot, in the part frame, relative to the death pose. */
 ankle?:T.Quaternion}
export interface CollapseProp {pos:{x:number;y:number;z:number};size:{x:number;y:number;z:number};rot?:{x:number;y:number;z:number;w:number}}
interface Node {name:string;parent?:Node;children:Node[];p0:T.Vector3;q0:T.Quaternion;anchor:T.Vector3;rel0:T.Quaternion;relK:T.Quaternion;free:boolean;boxes:CollapseBox[];kind:'pelvis'|'spine'|'head'|'arm'|'elbow'|'hip'|'knee'|'other';side:number;
 ankle?:{pivot:T.Vector3;frame:T.Quaternion;foot0:T.Quaternion}}

const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
const smooth=(x:number)=>{const t=clamp(x);return t*t*(3-2*t);};
const X=new T.Vector3(1,0,0),Y=new T.Vector3(0,1,0);
const rx=(a:number,out=new T.Quaternion())=>out.setFromAxisAngle(X,a);
/** Shape of the collapse (art tuning in radians, metres and seconds). */
export const COLLAPSE={
 /** Shin pitch from vertical on the knees, and the feet's pitch (toes slightly down, heels just off the floor). */
 shinPitch:1.2,footPitch:.12,
 /** Forward lean of the pelvis on the knees, the slump of the trunk and the hanging head. */
 pelvisLean:.1,spineSlump:.08,headHang:.15,
 /** Arms hang slightly forward and out; elbows soften. */
 armForward:.12,armOut:.06,elbowBend:-.35,
 /** Forward pitch of the pelvis reached at the authority's release time (quadratic ease, so it accelerates); `hipShare`
  * of it bends at the hips, the rest tips the thighs forward about the knees, which stay planted. */
 fallAngle:1.45,hipShare:.15,
 /** Extra trunk and head bend through the fall; the limp arms swing back along the sides (negative: toward the hips). */
 fallSpine:.12,fallHead:.35,armTrail:-.15,
 /** Impact jolt: peak head pitch (rad) and how fast it rises and settles (s). */
 jolt:.38,joltRise:.05,joltSettle:.11,
 /** The legs give a moment after the hit (s); the remaining horizontal death velocity decays like a stumble (1/s). */
 buckle:.03,slide:5,
 /** Lowest kneecap/shin/foot corner above the floor once kneeling (m). */
 clearance:.006,
} as const;

/** Classifies a ragdoll part by its rig joint name. */
function kindOf(name:string):Node['kind']{return name==='pelvis'?'pelvis':name==='spine'?'spine':name==='head'?'head':/-shoulder$/.test(name)?'arm':/-elbow$/.test(name)?'elbow':/-hip$/.test(name)?'hip':/-knee$/.test(name)?'knee':'other';}
const corner=new T.Vector3(),ankleQ=new T.Quaternion();
/** Lowest world y of a part's boxes at a pose (foot boxes turned about the ankle). */
function lowest(n:Node,pose:CollapsePose){let low=Infinity;
 for(const b of n.boxes)for(let i=0;i<8;i++){corner.set(i&1?b.half.x:-b.half.x,i&2?b.half.y:-b.half.y,i&4?b.half.z:-b.half.z).applyQuaternion(b.quaternion).add(b.center);
  if(b.ankle&&n.ankle&&pose.ankle)corner.sub(n.ankle.pivot).applyQuaternion(pose.ankle).add(n.ankle.pivot);corner.applyQuaternion(pose.quaternion).add(pose.position);low=Math.min(low,corner.y);}
 return low;}

export class KneelFall {
 /** Release time (s since death): the authority's releaseAt, rounded up to the physics step so the handoff is a step. */
 readonly release:number;readonly kneesAt:number;readonly releaseAt:number;
 /** Forward fall angle reached at release; less when cover or the floor is in the way. */
 get fallAngle(){return this.fall;}
 private fall=0;
 private nodes:Node[]=[];private byName=new Map<string,Node>();private root:Node;private knees:Node[];
 private facing:T.Quaternion;private lateral:T.Vector3;private pelvisK:T.Quaternion;private pelvisKPos:T.Vector3;private pivot=new T.Vector3();private footK:T.Quaternion;
 private slideVel:T.Vector3;private joltPitch:number;private joltSpine:T.Vector3;private ground:number;private low0=0;
 // Scratch (pose(), each() and velocity() are allocation-free after the first call).
 private qa=new T.Quaternion();private qb=new T.Quaternion();private qc=new T.Quaternion();private va=new T.Vector3();private vb=new T.Vector3();private lin=new T.Vector3();private ang=new T.Vector3();
 private poses=[new Map<string,CollapsePose>(),new Map<string,CollapsePose>(),new Map<string,CollapsePose>()];

 /** `parts` must contain pelvis, spine, both hips and both knees jointed (not free); check with KneelFall.supports(). */
 constructor(parts:CollapsePart[],motion:DeathMotion,o:{ground:number;step?:number;shot?:{x:number;y:number;z:number};severity?:number;props?:CollapseProp[]}){
  const step=o.step??1/60;this.kneesAt=Math.max(.05,Number.isFinite(motion.kneesAt)?motion.kneesAt:.32);this.releaseAt=Math.max(this.kneesAt+.05,Number.isFinite(motion.releaseAt)?motion.releaseAt:.82);this.release=Math.ceil(this.releaseAt/step-1e-6)*step;this.ground=o.ground;
  for(const p of parts){const n:Node={name:p.name,children:[],p0:p.position.clone(),q0:p.quaternion.clone(),anchor:(p.anchor||new T.Vector3()).clone(),rel0:new T.Quaternion(),relK:new T.Quaternion(),free:!!p.free,boxes:p.boxes,kind:kindOf(p.name),side:p.name.startsWith('left-')?1:p.name.startsWith('right-')?-1:0};
   if(p.ankle)n.ankle={pivot:p.ankle.pivot.clone(),frame:p.ankle.frame.clone(),foot0:p.quaternion.clone().multiply(p.ankle.frame)};this.nodes.push(n);this.byName.set(n.name,n);}
  for(const p of parts){const n=this.byName.get(p.name)!,parent=p.parent?this.byName.get(p.parent):undefined;if(parent&&!n.free){n.parent=parent;parent.children.push(n);n.rel0.copy(parent.q0).invert().multiply(n.q0);}}
  this.root=this.byName.get('pelvis')!;this.knees=this.nodes.filter(n=>n.kind==='knee'&&!n.free&&n.parent);
  // Facing frame: the authority's facing at death; the fall goes along its forward axis, about its lateral axis.
  const yaw=Number.isFinite(motion.facing)?motion.facing:0;this.facing=new T.Quaternion().setFromAxisAngle(Y,yaw);this.lateral=X.clone().applyQuaternion(this.facing);
  this.pelvisK=this.facing.clone().multiply(rx(COLLAPSE.pelvisLean));this.footK=this.facing.clone().multiply(rx(COLLAPSE.footPitch));
  // Kneel targets as joint rotations relative to the parent. Thighs hang vertical, shins pitch back onto the knees.
  for(const n of this.nodes){const k=n.kind,q=n.relK;
   if(k==='hip')q.copy(this.pelvisK).invert().multiply(this.facing);
   else if(k==='knee')rx(COLLAPSE.shinPitch,q);
   else if(k==='spine')rx(COLLAPSE.spineSlump,q);
   else if(k==='head')rx(COLLAPSE.headHang,q);
   else if(k==='arm')q.setFromEuler(new T.Euler(-COLLAPSE.armForward,0,n.side*COLLAPSE.armOut));
   else if(k==='elbow')rx(COLLAPSE.elbowBend,q);
   else q.copy(n.rel0);}
  // The jolt follows the fatal shot (victim-local travel direction): a shot from the front snaps the head back.
  const shot=new T.Vector3(o.shot?.x||0,o.shot?.y||0,o.shot?.z||0);if(shot.lengthSq()>1e-9)shot.normalize();const sev=clamp(o.severity??1);
  this.joltPitch=COLLAPSE.jolt*(.6+.4*sev)*clamp(shot.z*1.2,-1,1);this.joltSpine=new T.Vector3().crossVectors(Y,shot);
  const v=motion.velocity;this.slideVel=new T.Vector3(Number.isFinite(v.x)?v.x:0,0,Number.isFinite(v.z)?v.z:0);
  // The death pose's lowest leg corner (rig feet sit a little into the floor): the kneel starts from there without a pop.
  this.pelvisKPos=this.root.p0.clone();this.fall=0;const start=this.solve(0,this.poses[0],false);this.low0=Math.min(...this.knees.map(n=>lowest(n,start.get(n.name)!)));
  // Kneeling: the pelvis comes down until the lowest kneecap/shin/foot corner rests on the floor, and moves forward so the
  // feet end where they stood (they stay flat while the knees go down and forward).
  const feet=(pose:Map<string,CollapsePose>)=>{const c=new T.Vector3();let n=0;for(const k of this.knees){const p=pose.get(k.name)!;if(k.ankle){c.add(k.ankle.pivot.clone().applyQuaternion(p.quaternion).add(p.position));n++;}}return n?c.divideScalar(n):undefined;};
  const before=feet(start)?.clone(),kneel=this.solve(this.kneesAt+1e-6,this.poses[0],false),after=feet(kneel);
  if(before&&after){const forward=new T.Vector3(0,0,1).applyQuaternion(this.facing),shift=clamp(before.sub(after).dot(forward),-.1,.35);this.pelvisKPos.addScaledVector(forward,shift);}
  const kneel2=this.solve(this.kneesAt+1e-6,this.poses[0],false),low=Math.min(...this.knees.map(n=>lowest(n,kneel2.get(n.name)!)));
  if(Number.isFinite(low))this.pelvisKPos.y+=o.ground+COLLAPSE.clearance-low;
  // Pivot: the knee joints on the floor. Cover in front shortens the scripted fall; physics finishes it from the contact.
  const kneelPose=this.solve(this.kneesAt+1e-6,this.poses[0],false);
  this.pivot.set(0,0,0);for(const n of this.knees)this.pivot.add(kneelPose.get(n.name)!.position);this.pivot.divideScalar(Math.max(1,this.knees.length));
  this.fall=this.clearAngle(kneelPose,o.props||[]);
  // The floor: physics, not the script, puts the face on the ground. Largest fall that keeps the trunk, head and arms
  // above it until the release (bisection over a few sampled frames, once per death).
  const clear=(angle:number)=>{this.fall=angle;for(let i=0;i<=16;i++){const pose=this.solve(this.kneesAt+(this.release-this.kneesAt)*i/16,this.poses[1],true);
   for(const n of this.nodes)if(!n.free&&n.kind!=='knee'&&n.kind!=='hip'){const p=pose.get(n.name);if(p&&lowest(n,p)<o.ground+.025)return false;}}return true;};
  const most=this.fallAngle;if(!clear(most)){let lo=0,hi=most;for(let i=0;i<8;i++){const mid=(lo+hi)/2;if(clear(mid))lo=mid;else hi=mid;}this.fall=lo;}else this.fall=most;
 }
 /** True when the parts can kneel: pelvis, spine and both legs present and jointed. */
 static supports(parts:{name:string;free?:boolean}[]){const have=new Set(parts.filter(p=>!p.free).map(p=>p.name));return ['pelvis','spine','left-hip','left-knee','right-hip','right-knee'].every(n=>have.has(n));}
 /** Part names the choreography drives (all jointed parts; free parts are left to physics). */
 get driven(){return this.nodes.filter(n=>!n.free&&(n===this.root||n.parent)).map(n=>n.name);}
 /** Pose at `age` seconds after death (clamped to [0, release]). The returned map is reused by the next pose() call. */
 pose(age:number):Map<string,CollapsePose>{return this.solve(clamp(Number.isFinite(age)?age:0,0,this.release),this.poses[2],true);}
 /** World linear and angular velocity of a driven part at `age` (central difference of the pose). */
 velocity(age:number,name:string,linear:T.Vector3,angular:T.Vector3){
  const h=1/480,t=clamp(Number.isFinite(age)?age:0,0,this.release),a=Math.max(0,t-h),b=t+h,pa=this.solve(a,this.poses[0],true).get(name),pb=this.solve(b,this.poses[1],true).get(name);
  if(!pa||!pb){linear.set(0,0,0);angular.set(0,0,0);return;}
  this.rate(pa,pb,b-a,linear,angular);
 }
 /** Every driven part's pose and world velocities at `age`: what the ragdoll bodies are set to before each physics step. */
 each(age:number,visit:(name:string,pose:CollapsePose,linear:T.Vector3,angular:T.Vector3)=>void){
  const h=1/480,t=clamp(Number.isFinite(age)?age:0,0,this.release),a=Math.max(0,t-h),b=t+h,A=this.solve(a,this.poses[0],true),B=this.solve(b,this.poses[1],true),P=this.solve(t,this.poses[2],true);
  for(const [name,pose] of P){const pa=A.get(name),pb=B.get(name);if(!pa||!pb)continue;this.rate(pa,pb,b-a,this.lin,this.ang);visit(name,pose,this.lin,this.ang);}
 }
 /** Hinge angle (about the joint x axis) of a driven part relative to its parent at `age`: the motor rest at release. */
 hinge(age:number,name:string){const n=this.byName.get(name);if(!n?.parent)return 0;const pose=this.pose(age),q=this.qa.copy(pose.get(n.parent.name)!.quaternion).invert().multiply(pose.get(name)!.quaternion);if(q.w<0)q.set(-q.x,-q.y,-q.z,-q.w);return 2*Math.atan2(q.x,q.w);}
 private rate(pa:CollapsePose,pb:CollapsePose,dt:number,linear:T.Vector3,angular:T.Vector3){
  linear.copy(pb.position).sub(pa.position).divideScalar(dt);
  this.qa.copy(pb.quaternion).multiply(this.qb.copy(pa.quaternion).invert());if(this.qa.w<0)this.qa.set(-this.qa.x,-this.qa.y,-this.qa.z,-this.qa.w);
  const s=Math.sqrt(Math.max(0,1-this.qa.w*this.qa.w)),angle=2*Math.atan2(s,this.qa.w);if(s<1e-9)angular.set(0,0,0);else angular.set(this.qa.x/s,this.qa.y/s,this.qa.z/s).multiplyScalar(angle/dt);
 }

 /** Forward-kinematics pose at time t. `fall`=false evaluates the kneel alone, without the fall, floor contact or slide (setup). */
 private solve(t:number,out:Map<string,CollapsePose>,fall:boolean){
  const knees=this.kneesAt,b=COLLAPSE.buckle,kLegs=smooth((t-b)/(knees-b)),kDrop=smooth(Math.pow(clamp((t-b)/(knees-b)),.8)),kArms=smooth(t/.22),kTrunk=smooth(t/knees);
  const f=fall?Math.max(0,(t-knees)/(this.releaseAt-knees)):0,phi=this.fall*f*f,fs=smooth(f);
  const pulse=t<COLLAPSE.joltRise?Math.sin(Math.PI/2*t/COLLAPSE.joltRise):Math.exp(-(t-COLLAPSE.joltRise)/COLLAPSE.joltSettle);
  const fallQ=this.qc.setFromAxisAngle(this.lateral,phi);
  // Pelvis: settle onto the knees (the drop leads the bend a little, so the floor contact below decides it), then rotate
  // forward about the knee line.
  const root=this.root,rp=entry(out,root.name);
  rp.quaternion.slerpQuaternions(root.q0,this.pelvisK,kLegs).premultiply(fallQ);
  rp.position.lerpVectors(root.p0,this.pelvisKPos,kDrop);
  const visit=(n:Node)=>{for(const c of n.children){const p=out.get(n.name)!,e=entry(out,c.name),w=c.kind==='arm'||c.kind==='elbow'?kArms:c.kind==='hip'||c.kind==='knee'?kLegs:kTrunk;
   const rel=this.qb.slerpQuaternions(c.rel0,c.relK,w);
   if(c.kind==='hip')rel.multiply(rx(-COLLAPSE.hipShare*phi,this.qa));
   else if(c.kind==='knee')rel.multiply(rx(-(1-COLLAPSE.hipShare)*phi,this.qa));
   else if(c.kind==='spine'){rel.multiply(rx(COLLAPSE.fallSpine*fs,this.qa));if(this.joltSpine.lengthSq()>1e-9)rel.premultiply(this.qa.setFromAxisAngle(this.vb.copy(this.joltSpine).normalize(),(this.headFree?.12:.05)*pulse));}
   else if(c.kind==='head')rel.multiply(rx(COLLAPSE.fallHead*fs+this.joltPitch*pulse,this.qa));
   else if(c.kind==='arm')rel.premultiply(rx(-COLLAPSE.armTrail*phi,this.qa));
   e.quaternion.copy(p.quaternion).multiply(rel);e.position.copy(c.anchor).applyQuaternion(p.quaternion).add(p.position);
   // Feet stay flat on the floor while the shin pitches, then settle on their toes (world orientation, like the shins).
   if(c.ankle){const foot=this.qa.slerpQuaternions(c.ankle.foot0,this.footK,kLegs);e.ankle=(e.ankle||new T.Quaternion()).copy(e.quaternion).invert().multiply(foot).multiply(ankleQ.copy(c.ankle.frame).invert());}
   visit(c);}};
  visit(root);
  // Falling: the knees stay planted where they knelt.
  if(phi>0){const mid=this.vb.set(0,0,0);for(const k of this.knees)mid.add(out.get(k.name)!.position);mid.divideScalar(Math.max(1,this.knees.length)).sub(this.pivot).negate();
   for(const [name,e] of out)if(!this.byName.get(name)?.free)e.position.add(mid);}
  // Floor contact while kneeling: the lowest leg corner never goes below the floor (from where it stood at death).
  if(fall&&t<knees+1e-6){const limit=this.low0+(this.ground+COLLAPSE.clearance-this.low0)*kLegs;let low=Infinity;for(const k of this.knees)low=Math.min(low,lowest(k,out.get(k.name)!));
   const lift=limit-low;if(lift>0)for(const [name,e] of out)if(this.byName.get(name)&&!this.byName.get(name)!.free)e.position.y+=lift;}
  // A remaining stumble slides the whole pose.
  if(fall){const slide=this.va.copy(this.slideVel).multiplyScalar((1-Math.exp(-COLLAPSE.slide*t))/COLLAPSE.slide);for(const [name,e] of out)if(!this.byName.get(name)?.free)e.position.add(slide);}
  return out;
 }
 /** Largest fall angle (up to COLLAPSE.fallAngle) before the head or chest would enter nearby cover. */
 private clearAngle(kneel:Map<string,CollapsePose>,props:CollapseProp[]){
  if(!props.length)return COLLAPSE.fallAngle;const probes:T.Vector3[]=[];
  for(const name of ['spine','head']){const n=this.byName.get(name),p=n&&kneel.get(name);if(!n||!p||n.free)continue;for(const b of n.boxes)probes.push(b.center.clone().applyQuaternion(p.quaternion).add(p.position),b.center.clone().add(new T.Vector3(0,b.half.y,b.half.z).applyQuaternion(b.quaternion)).applyQuaternion(p.quaternion).add(p.position));}
  const q=new T.Quaternion(),local=new T.Vector3(),inside=(x:T.Vector3)=>props.some(prop=>{local.set(x.x-prop.pos.x,x.y-prop.pos.y,x.z-prop.pos.z);if(prop.rot)local.applyQuaternion(q.set(prop.rot.x,prop.rot.y,prop.rot.z,prop.rot.w).invert());return Math.abs(local.x)<prop.size.x/2+.08&&Math.abs(local.y)<prop.size.y/2+.08&&Math.abs(local.z)<prop.size.z/2+.08;});
  if(probes.some(inside))return 0;
  const r=new T.Quaternion(),p=new T.Vector3();
  for(let a=.05;a<=COLLAPSE.fallAngle+1e-6;a+=.05){r.setFromAxisAngle(this.lateral,a);if(probes.some(x=>inside(p.copy(x).sub(this.pivot).applyQuaternion(r).add(this.pivot))))return Math.max(0,a-.1);}
  return COLLAPSE.fallAngle;
 }
 private get headFree(){return this.byName.get('head')?.free!==false;}
}
function entry(map:Map<string,CollapsePose>,name:string){let e=map.get(name);if(!e){e={position:new T.Vector3(),quaternion:new T.Quaternion()};map.set(name,e);}return e;}
/** Boxes of a part's body meshes in its own frame, from the mesh copies the ragdoll already made. */
export function partBoxes(group:T.Object3D):CollapseBox[]{
 const out:CollapseBox[]=[];for(const m of group.children){if(!(m instanceof T.Mesh)||m.userData.bodyWound||m.userData.collapseSkip)continue;out.push({center:m.position.clone(),half:m.scale.clone().multiplyScalar(.5),quaternion:m.quaternion.clone(),ankle:!!m.userData.ankle});}
 return out;
}
