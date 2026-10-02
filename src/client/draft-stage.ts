import * as T from 'three';
import {createCharacterRig,settleJointCovers,setRigDetail,type CharacterRig} from './character-rig.js';
import {dressRig} from './cosmetics.js';
import {teamArmor} from './team-identity.js';
import {buildWeapon} from './weapon-visuals.js';
import {lobbyPlayer} from './lobby-poses.js';
import {sirenify,animateSiren} from './siren/index.js';
import {animateCosmetics,disposeAuditorHalo} from './auditor-halo.js';
import {ArmorGlow} from './armor-glow.js';
import {hand} from './rig-ik.js';
import {CHIPS,RARITIES} from '../shared/content.js';
import type {PlayerView,Snapshot} from '../shared/simulation.js';
import {grabTimeline} from './draft-timeline.js';

/** Card centres are mapped onto this plane in front of the pilot, so the head turns up and out towards the card on screen. */
export const DRAFT_LOOK_Z=.9;
/** The reaching hand aims at the card's point on this nearer plane, so the arm lifts up and out (on screen, a reach
 * towards the card above) instead of pointing straight at the camera. */
export const DRAFT_REACH_Z=.42;
/** Specs that keep their own boss pose on the draft stage (everyone else gets the same draft pose). */
const BOSS_SPECS=new Set(['siren']);
export const isBossSpec=(spec:string|undefined)=>!!spec&&BOSS_SPECS.has(spec);
/**
 * Chest-up camera. The pilot is framed large at the bottom centre with the cards above (the ROUNDS card pick): the top of
 * the head (and hat) sits just under the tray, and the bottom edge of the screen cuts across the upper belly when there
 * is room, rising to the shoulders when the tray leaves little height. `minWidth` keeps both shoulders in narrow portrait
 * windows (the frame then shows more torso instead). `arrive` is the push-in when a turn starts, `fade` the light-up.
 */
export const DRAFT_FRAME={fov:24,gap:.035,roomy:.56,cramped:.3,minWidth:1.3,headroom:.05,hatCap:.24,lift:.22,arrive:.75,fade:.4} as const;
const HEAD_UP=.165,REACH=.47,YAW_LIMIT=1.05,PITCH_DOWN=.8,PITCH_UP=.4;
/** Shoulder-to-wrist reach while considering a card (a soft elbow) and when taking it (nearly straight; ARM_MAX is .515). */
const CONSIDER=.495,TAKE=.512;
/** The reaching hand stays beside the head (world x), so a card near the middle never puts the hand over the face. */
const BESIDE=.36;
/** The cards sit above the pilot: the head turns fully towards them but only tips up part of the way (a blank face tipped
 * far back reads as the underside of a box, not a look). */
const LOOK_UP=.55;
const _local=new T.Vector3(),_ndc=new T.Vector2(),_w=new T.Quaternion(),_p=new T.Quaternion(),_q=new T.Quaternion(),_i=new T.Quaternion(),_f=new T.Vector3(),_d=new T.Vector3(),_e=new T.Vector3(),_s=new T.Vector3(),_t=new T.Vector3();
const _m=new T.Matrix4(),_x=new T.Vector3(),_y=new T.Vector3(),_z=new T.Vector3(),_box=new T.Box3(),_hold=new T.Quaternion(),_aim=new T.Quaternion(),_eu=new T.Euler(),_white=new T.Color(0xffffff),PALM_FACING=new T.Vector3(0,.15,.45);
/** Rotate one joint (in world space) part of the way towards a target; the next joint in the chain takes the rest. */
function share(node:T.Object3D,target:T.Vector3,weight:number,up=0){
 node.updateWorldMatrix(true,false);node.getWorldQuaternion(_w);_e.set(0,up,0);node.localToWorld(_e);
 _d.copy(target).sub(_e);if(_d.lengthSq()<1e-8)return;_d.normalize();_f.set(0,0,1).applyQuaternion(_w);
 _q.setFromUnitVectors(_f,_d);_i.identity();_q.slerp(_i,1-weight);
 node.parent!.getWorldQuaternion(_p);node.quaternion.copy(_p.invert().multiply(_q).multiply(_w));
}
/**
 * Look layer over an already posed rig: chest, neck and head share the turn towards a world point. The direction is
 * clamped to a comfortable yaw/pitch range so a card far to the side reads as a glance, not a broken neck.
 */
export function lookAt(r:CharacterRig,target:T.Vector3,weight:number,chestShare=.22,neckShare=.4){
 if(weight<=0)return;r.group.updateMatrixWorld(true);
 r.head.getWorldPosition(_s);_s.y+=HEAD_UP;_t.copy(target).sub(_s);const dist=Math.max(.3,_t.length());
 r.group.getWorldQuaternion(_w);_t.applyQuaternion(_w.invert());
 const yaw=Math.max(-YAW_LIMIT,Math.min(YAW_LIMIT,Math.atan2(_t.x,_t.z))),flat=Math.hypot(_t.x,_t.z),pitch=Math.max(-PITCH_DOWN,Math.min(PITCH_UP,Math.atan2(_t.y,flat)));
 _t.set(Math.sin(yaw)*Math.cos(pitch),Math.sin(pitch),Math.cos(yaw)*Math.cos(pitch)).multiplyScalar(dist);r.group.getWorldQuaternion(_w);_t.applyQuaternion(_w).add(_s);
 share(r.chest,_t,chestShare*weight);share(r.neck,_t,neckShare*weight);share(r.head,_t,weight,HEAD_UP);
}
/** Reach layer: the wrist goes towards a world point, clamped to arm's length so a far card gets an outstretched hand. */
export function reachFor(r:CharacterRig,left:boolean,target:T.Vector3,weight:number,orientation:T.Euler){
 if(weight<=0)return;r.group.updateMatrixWorld(true);(left?r.leftArm:r.rightArm).getWorldPosition(_s);
 _t.copy(target).sub(_s);const len=_t.length();if(len>1e-6)_t.multiplyScalar(Math.min(len,REACH)/len);_t.add(_s);
 hand(r,left,r.chest.worldToLocal(_local.copy(_t)),orientation,weight);
}
/**
 * The same draft pose for every pilot, whatever their class, weapon or lobby pose: standing tall with the chest lifted
 * and the shoulders drawn back and open, arms loose at the sides (below the chest-up frame until one reaches), weapons
 * put away, and a slow breath that lifts the chest and shoulders. Every joint is written, so nothing from another pose
 * leaks in. Pure function of time; the look and reach layers go on top.
 */
export function draftPose(r:CharacterRig,time:number){
 const breath=Math.sin(time*1.65),sway=Math.sin(time*.72),drift=Math.sin(time*.41+1.3);
 r.body.position.set(0,.002*breath,0);r.body.rotation.set(0,0,.006*sway);r.pelvis.rotation.set(0,0,0);
 r.spine.rotation.set(-.015+.006*breath,0,0);r.chest.rotation.set(-.045+.014*breath,.015*drift,-.005*sway);r.neck.rotation.set(.04,0,0);r.head.rotation.set(.03-.006*breath,.04*drift,.012*sway);
 r.leftShoulder.rotation.set(0,.06,.025+.012*breath);r.rightShoulder.rotation.set(0,-.06,-.025-.012*breath);
 r.leftArm.rotation.set(.04,0,.09+.006*breath);r.rightArm.rotation.set(.04,0,-.09-.006*breath);r.leftElbow.rotation.set(-.24,0,0);r.rightElbow.rotation.set(-.24,0,0);
 r.leftHand.rotation.set(0,.12,0);r.rightHand.rotation.set(0,-.12,0);
 r.leftLeg.rotation.set(0,0,.05);r.rightLeg.rotation.set(0,0,-.05);r.leftKnee.rotation.set(.03,0,0);r.rightKnee.rotation.set(.03,0,0);r.leftFoot.rotation.set(-.03,0,0);r.rightFoot.rotation.set(-.03,0,0);r.leftToe.rotation.set(0,0,0);r.rightToe.rotation.set(0,0,0);
 r.weapon.visible=false;r.offWeapon.visible=false;
}
/** A small processor package: dark substrate, metal legs and a lit die in the chip's rarity colour. */
function chipModel(){
 const g=new T.Group();g.name='draft-chip';
 const body=new T.Mesh(new T.BoxGeometry(.17,.024,.12),new T.MeshLambertMaterial({color:0x17181e}));g.add(body);
 const die=new T.Mesh(new T.BoxGeometry(.085,.012,.06),new T.MeshBasicMaterial({color:0xffffff,toneMapped:false}));die.position.y=.017;die.name='die';g.add(die);
 const pin=new T.BoxGeometry(.012,.008,.026),metal=new T.MeshLambertMaterial({color:0xb9bcc6});
 for(let i=0;i<6;i++)for(const side of [-1,1]){const leg=new T.Mesh(pin,metal);leg.position.set(-.065+i*.026,-.012,side*.07);g.add(leg);}
 return g;
}
/** A soft additive disc (alpha falls off from the centre); no textures, so it also builds outside a browser. */
function softDisc(radius:number,falloff:number,peak:number){
 const g=new T.RingGeometry(0,radius,40,6),pos=g.attributes.position,colors=new Float32Array(pos.count*4);
 for(let i=0;i<pos.count;i++){const r=Math.hypot(pos.getX(i),pos.getY(i))/radius;colors.set([1,1,1,Math.max(0,peak*(1-r)**falloff)],i*4);}
 g.setAttribute('color',new T.BufferAttribute(colors,4));
 return new T.Mesh(g,new T.MeshBasicMaterial({vertexColors:true,transparent:true,depthWrite:false,toneMapped:false,blending:T.AdditiveBlending,color:0xff6b35}));
}
/** Out-of-focus stage lights behind the pilot: [x, y, z, radius, opacity, drift speed, phase]. */
const BOKEH:[number,number,number,number,number,number,number][]=[[-2.6,2.3,-4.2,.42,.16,.05,0],[-1.7,.9,-3.6,.3,.12,.07,1.7],[-.9,2.9,-4.6,.5,.1,.04,3.1],[1.1,2.6,-4.1,.36,.14,.06,.8],[2.2,1.3,-3.8,.46,.12,.05,2.4],[2.9,2.8,-4.8,.6,.08,.03,4.2],[-3.3,1.6,-4.6,.55,.08,.035,5.3],[.3,3.3,-5,.7,.07,.03,1.1],[3.6,.7,-4.4,.38,.1,.06,3.7]];
const PALM=new T.Vector3(0,-.035,.04);
const smooth=(x:number)=>{x=Math.min(1,Math.max(0,x));return x*x*(3-2*x);};
const outCubic=(x:number)=>{x=Math.min(1,Math.max(0,x));return 1-(1-x)**3;};
/** Exact critically damped spring step: a span gives the same result in one step or many (30/60/120 Hz agree). */
export function spring(x:number,v:number,target:number,omega:number,dt:number):[number,number]{
 const d=x-target,k=(v+omega*d)*dt,e=Math.exp(-omega*dt);return [target+(d+k)*e,(v-omega*k)*e];
}
class Spring3 {
 readonly at=new T.Vector3();private v=new T.Vector3();
 snap(to:T.Vector3){this.at.copy(to);this.v.set(0,0,0);}
 step(to:T.Vector3,omega:number,dt:number){for(const k of ['x','y','z'] as const){const [x,v]=spring(this.at[k],this.v[k],to[k],omega,dt);this.at[k]=x;this.v[k]=v;}return this.at;}
}
/** One arm's reach towards the considered card: blend weight and aim point, both springs. */
interface Arm {w:number;v:number;aim:Spring3;aimed:boolean}
/** Chest-up framing anchors (world y), measured once per pilot from their own stage pose (bosses differ). */
export interface DraftAnchors {top:number;chest:number;shoulder:number}
/** The picker as the stage poses them: alive, standing and idle, whatever the last round left behind (set losers are
 * still at hp 0, possibly knocked down, mid-move or ragdolled, while they draft). */
export function stageView(p:PlayerView):PlayerView{
 return {...lobbyPlayer(p),hp:Math.max(1,p.maxHp||1),death:undefined,hitReaction:undefined,roll:undefined,attackCommitment:undefined,meleeAction:undefined,animationTime:undefined,siren:p.siren?{...p.siren,move:undefined} as PlayerView['siren']:undefined};
}
/**
 * Full-screen draft scene rendered by the main renderer in place of the frozen arena (the PlayerIntros pattern). It shows
 * the current picker chest-up and large below the cards: every pilot in the same draft pose (bosses keep their own), a
 * head that follows the hovered (or booting / scanned) card, the arm on that card's side lifting to reach for it, and on
 * the pick the hand closing on the chip and pulling it up to the chest while the details spotlight opens.
 */
export class DraftStage {
 readonly scene=new T.Scene();readonly camera=new T.PerspectiveCamera(DRAFT_FRAME.fov,1,.1,60);
 /** World points of the card centres on the look plane, in offer order (set by the DOM overlay each frame). */
 cards:T.Vector3[]=[];hover=-1;taken=-1;active=false;
 /** Viewport fractions (0 top, 1 bottom) of the heading's bottom edge and the tray's top and bottom edges, from the DOM
  * overlay. The pilot is framed between the tray's bottom edge and the bottom of the screen. */
 headLine=.14;trayLine=.2;trayBottom=.36;private frameTop=-1;
 private armor=new ArmorGlow(this.scene);private rig?:CharacterRig;private key='';private time=0;private side=1;private boss=false;
 private look=new Spring3();private goal=new T.Vector3();private weight=0;private chip=chipModel();anchors:DraftAnchors={top:1.76,chest:.86,shoulder:1.07};
 private arms:[Arm,Arm]=[{w:0,v:0,aim:new Spring3(),aimed:false},{w:0,v:0,aim:new Spring3(),aimed:false}];private last=NaN;
 private rings:T.Mesh[]=[];private halo:T.Mesh;private glowBack:T.Mesh;private bokeh:T.Mesh[]=[];private accent:T.PointLight;private rims:T.DirectionalLight[]=[];private lights:[T.Light,number][]=[];
 private ray=new T.Raycaster();private plane=new T.Plane(new T.Vector3(0,0,1),-DRAFT_LOOK_Z);
 private motion=typeof matchMedia==='function'?matchMedia('(prefers-reduced-motion: reduce)'):undefined;private low:boolean|undefined;private sidePick='';private glow=new T.PointLight(0xffffff,0,1.8,2);private tint=new T.Color();private rimTint=new T.Color();private chipColor=new T.Color();private hold=new T.Vector3();private reach=new T.Vector3();private chipAt=new T.Vector3();private point=new T.Vector3();private gaze=new T.Vector3();
 constructor(){
  this.scene.background=new T.Color(0x06070b);this.scene.fog=new T.Fog(0x06070b,6,16);
  // Portrait lighting: a key from high front-right and a low fill, so the faces of the blank head shade differently and
  // a turn of the head reads.
  const fill=new T.HemisphereLight(0xdfe7ff,0x2a2433,.8);this.scene.add(fill);
  const key=new T.DirectionalLight(0xffeedd,2.4);key.position.set(1.6,2.8,3.4);this.scene.add(key);
  // Two rim lights from behind in the pilot's accent colour outline the head and shoulders against the dark stage.
  for(const x of [-1,1]){const rim=new T.DirectionalLight(0xff6b35,2.2);rim.position.set(2.4*x,2.6,-3.2);this.scene.add(rim);this.rims.push(rim);}
  this.accent=new T.PointLight(0xff6b35,5,7,2);this.accent.position.set(0,1.9,-1.3);this.scene.add(this.accent);
  this.lights=[[fill,fill.intensity],[key,key.intensity],...this.rims.map(r=>[r,r.intensity] as [T.Light,number]),[this.accent,this.accent.intensity]];
  // A wide accent glow and a tighter halo behind the head and shoulders, plus a few drifting out-of-focus lights: a
  // blurred, darkened stage behind the close-up instead of an empty podium.
  this.glowBack=softDisc(4.4,1.8,.26);this.glowBack.position.set(0,1.1,-4);this.scene.add(this.glowBack);
  this.halo=softDisc(1.5,2.2,.4);this.halo.position.set(0,1.45,-2.2);this.scene.add(this.halo);
  for(const [x,y,z,radius,opacity] of BOKEH){const b=softDisc(radius,1.2,opacity);b.position.set(x,y,z);this.scene.add(b);this.bokeh.push(b);}
  for(let i=0;i<2;i++){const ring=new T.Mesh(new T.TorusGeometry(.13,.007,6,40),new T.MeshBasicMaterial({color:0xffffff,transparent:true,opacity:0,depthWrite:false,toneMapped:false,blending:T.AdditiveBlending}));ring.visible=false;this.scene.add(ring);this.rings.push(ring);}
  this.chip.visible=false;this.scene.add(this.glow);
 }
 private build(p:PlayerView,state:Snapshot){
  this.clear();const r=createCharacterRig(p.color,p.cosmetics?.bodyColor,true);dressRig(r,p.cosmetics);teamArmor(r,state.options.gameMode==='teams'?p.team:undefined);
  r.group.position.y=.88;r.sockets.rightHand.add(r.weapon);r.sockets.leftHand.add(r.offWeapon);buildWeapon(r.weapon,p.spec);if(['daggers','emperor','ansatsuken'].includes(p.spec))buildWeapon(r.offWeapon,p.spec,true);
  this.boss=isBossSpec(p.spec);if(p.spec==='siren')sirenify(r);this.scene.add(r.group);this.rig=r;this.weight=0;
  // Framing anchors from the pilot's own stage pose (a boss floats, hunches and burns above the head) and their hat.
  if(this.boss)animateSiren(r,p,state,0,0,0);else draftPose(r,0);
  r.group.updateMatrixWorld(true);const head=r.head.localToWorld(_s.set(0,HEAD_UP,0)).y,chest=r.chest.getWorldPosition(_t).y;
  _box.makeEmpty();for(const o of r.sockets.head.children)_box.expandByObject(o);const hat=_box.isEmpty()?0:Math.min(DRAFT_FRAME.hatCap,Math.max(0,_box.max.y-(head+.19)));
  this.anchors={top:head+.19+DRAFT_FRAME.headroom+hat+(this.boss?.14:0),chest:chest-.13,shoulder:chest+.135};
 }
 /** Chest-up camera between the tray's bottom edge and the bottom of the screen; `arrive` (0-1) pushes in from wider. */
 private frameDt=0;
 private frame(aspect:number,arrive:number){
  if(this.frameTop<0)this.frameTop=this.trayBottom;else this.frameTop+=(this.trayBottom-this.frameTop)*(1-Math.exp(-this.frameDt*6));
  const F=DRAFT_FRAME,a=this.anchors,top=Math.min(.72,Math.max(.06,this.frameTop+F.gap)),room=1-top;
  // Plenty of height: frame down to the upper belly. Little height (two rows, short windows): just head and shoulders.
  const bottom=a.shoulder+(a.chest-a.shoulder)*smooth((room-F.cramped)/(F.roomy-F.cramped));
  let height=Math.max((a.top-bottom)/room,F.minWidth/Math.max(.2,aspect));
  height*=1+.18*(1-arrive);const centre=a.top+(top-.5)*height-.1*(1-arrive),dist=height/2/Math.tan(F.fov*Math.PI/360);
  this.camera.aspect=aspect;this.camera.fov=F.fov;this.camera.position.set(0,centre+F.lift,dist);this.camera.lookAt(0,centre,0);this.camera.updateProjectionMatrix();this.camera.updateMatrixWorld();
 }
 /** A card's world point (look plane) carried along its camera ray to the nearer reach plane. */
 private reachPoint(card:T.Vector3,out:T.Vector3){
  const c=this.camera.position,k=(c.z-DRAFT_REACH_Z)/Math.max(1e-3,c.z-card.z);return out.copy(card).sub(c).multiplyScalar(k).add(c);
 }
 /** Chest-space wrist orientation with the fingers along `dir` (world) and the palm turned in towards the midline, thumb
  * up: an open reach for the card rather than a raised fist. */
 private aim(r:CharacterRig,dir:T.Vector3,out:T.Quaternion,left:boolean){
  r.chest.getWorldQuaternion(_w);_y.copy(dir).applyQuaternion(_w.invert()).normalize().negate();
  _f.set(left?-1:1,PALM_FACING.y,PALM_FACING.z);_z.copy(_f).addScaledVector(_y,-_y.dot(_f));if(_z.lengthSq()<1e-6)_z.set(1,0,0);_z.normalize();_x.crossVectors(_y,_z).normalize();_z.crossVectors(_x,_y);
  return out.setFromRotationMatrix(_m.makeBasis(_x,_y,_z));
 }
 update(state:Snapshot,aspect:number,dt:number,quality=1,compatibility=false,reduced=this.motion?.matches??false){
  const d=state.draft,live=d&&state.phase==='draft'?state.players.find(q=>q.id===d.player):undefined;
  if(!d||!live||compatibility){this.clear();this.active=false;return false;}
  const p=stageView(live),key=p.id+p.spec+JSON.stringify(p.cosmetics)+p.color+(state.options.gameMode==='teams'?p.team:'');
  if(key!==this.key||!this.rig){this.build(p,state);this.key=key;}
  const r=this.rig!;this.active=true;dt=Math.min(.05,Math.max(0,dt));if(!reduced)this.time+=dt;this.frameDt=dt;
  // Springs restart at their targets on the first frame and whenever the clock jumps or runs backwards.
  const jump=!(state.time>=this.last&&state.time-this.last<=.35);this.last=state.time;
  // A new turn pushes in from a wider shot while the stage lights come up (authoritative turn start, any frame rate).
  const since=Math.max(0,state.time-d.started),arrive=reduced?1:outCubic(since/DRAFT_FRAME.arrive),fade=.2+.8*smooth(since/DRAFT_FRAME.fade);
  this.frame(aspect,arrive);
  const low=quality<.45;if(this.low!==low){setRigDetail(r,low);this.low=low;}
  // Set losers draft while still at hp 0 from the last round: pose a cleaned-up view, never the fight state.
  if(this.boss)animateSiren(r,p,state,reduced?0:state.time,dt,0);
  else{draftPose(r,this.time);const rise=1-arrive;r.chest.rotation.x+=.1*rise;r.head.rotation.x+=.28*rise;}
  animateCosmetics(r,this.time);
  const chosen=d.chosen?CHIPS.find(c=>c.id===d.chosen):undefined,g=chosen?grabTimeline(state.time-(d.chosenAt??state.time)):undefined;
  const card=this.taken>=0?this.cards[this.taken]:undefined;
  // The reaching hand is the one on the card's side, chosen once per pick (frame rate independent).
  const pick=`${d.player}:${d.turn}:${d.chosenAt}`;if(g&&card&&this.sidePick!==pick){this.sidePick=pick;this.side=card.x>=0?1:-1;}
  if(g&&card){r.weapon.visible=false;r.offWeapon.visible=false;}
  // The considered card: the one being taken, else the hovered one (own turn). The arm on its side lifts towards it.
  const hovered=!g&&this.hover>=0?this.cards[this.hover]:undefined,focus=g?card:hovered,focusSide=g?this.side:hovered?(hovered.x>=0?1:-1):0;
  r.group.updateMatrixWorld(true);
  for(let i=0;i<2;i++){
   const arm=this.arms[i],left=i===0,mine=!!focus&&focusSide===(left?1:-1),target=!this.boss&&mine?1:0;
   if(mine)this.reachPoint(focus!,this.point);
   if(jump){arm.w=target;arm.v=0;if(mine){arm.aim.snap(this.point);arm.aimed=true;}}
   else{[arm.w,arm.v]=spring(arm.w,arm.v,target,9,dt);if(mine){if(arm.aimed)arm.aim.step(this.point,11,dt);else{arm.aim.snap(this.point);arm.aimed=true;}}}
   const grabbing=!!(g&&card)&&mine,w=Math.min(1,Math.max(0,grabbing?Math.max(arm.w,g!.reach):arm.w));if(w<=.001||!arm.aimed)continue;
   // A soft elbow while considering; fully outstretched to take the card; then drawn in to the chest. The shoulder
   // rises with the arm.
   (left?r.leftShoulder:r.rightShoulder).rotation.z+=(left?1:-1)*.2*w*(1-(grabbing?g!.pull:0));
   r.chest.localToWorld(this.hold.set(left?.15:-.15,.07,.36));
   (left?r.leftArm:r.rightArm).getWorldPosition(_s);_d.copy(arm.aim.at).sub(_s);const len=_d.length()||1;_d.divideScalar(len);
   // The hand gives a little as it closes on the card (catching its momentum), then draws it in.
   const catchDip=grabbing?.05*Math.sin(Math.PI*Math.min(1,Math.max(0,(g!.e-.42)/.2))):0,ext=(grabbing?CONSIDER+(TAKE-CONSIDER)*g!.reach:CONSIDER)-catchDip,pull=grabbing?g!.pull:0;
   this.reach.copy(_s).addScaledVector(_d,Math.min(len,ext));this.reach.x=left?Math.max(BESIDE,this.reach.x):Math.min(-BESIDE,this.reach.x);_d.copy(this.reach).sub(_s).normalize();
   // Drawn in on an outward arc (down first, then across), so the hand passes beside the face rather than over it.
   this.reach.set(this.reach.x+(this.hold.x-this.reach.x)*pull*pull,this.reach.y+(this.hold.y-this.reach.y)*pull,this.reach.z+(this.hold.z-this.reach.z)*pull);
   this.aim(r,_d,_aim,left);if(pull>0)_aim.slerp(_hold.setFromEuler(_eu.set(-1.45+.45*pull,0,(left?-1:1)*.5*pull)),pull);
   hand(r,left,r.chest.worldToLocal(_local.copy(this.reach)),_eu.setFromQuaternion(_aim),w);
  }
  const socket=this.side>0?r.sockets.leftHand:r.sockets.rightHand;
  // Where to look: the chip in hand, the chosen card, the hovered card, the card booting in, or a slow scan.
  r.group.updateMatrixWorld(true);
  if(g&&g.contact)socket.localToWorld(this.goal.copy(PALM));
  else if(g&&card)this.goal.copy(card);
  else if(hovered)this.goal.copy(hovered);
  else if(this.cards.length){const booting=state.time<d.readyAt,index=reduced?0:booting?Math.min(this.cards.length-1,Math.floor(Math.max(0,state.time-d.started)/.22)):Math.floor(this.time/1.8)%this.cards.length;this.goal.copy(this.cards[index]);}
  else this.goal.set(0,1.6,3);
  if(jump)this.look.snap(this.goal);else this.look.step(this.goal,g?18:9,dt);this.weight+=(1-this.weight)*(1-Math.exp(-dt*4));
  r.head.getWorldPosition(_s);const eye=_s.y+HEAD_UP,rise=this.look.at.y-eye;this.gaze.copy(this.look.at).setY(eye+(rise>0?rise*LOOK_UP:rise));
  // The head is a blank box, so the chest turns further with the look: the sternum stripe shows where the pilot faces.
  lookAt(r,this.gaze,this.weight,this.boss?.22:.36,.42);settleJointCovers(r);
  // The chip in the palm, its light and a two-ring flash at contact. The light lives in the scene permanently, so the
  // light count (and every lit shader) stays the same when the chip appears.
  const rarity=chosen?RARITIES[chosen.rarity].color:p.color;this.chipColor.set(rarity);
  if(g&&g.contact){if(this.chip.parent!==socket)socket.add(this.chip);this.chip.position.copy(PALM);this.chip.rotation.set(Math.PI/2,0,0);
   const pop=Math.min(1,(g.e-.48)/.12);this.chip.scale.setScalar(.5+.5*pop+(reduced?0:.08*Math.sin(this.time*5)));this.chip.visible=true;
   ((this.chip.getObjectByName('die') as T.Mesh).material as T.MeshBasicMaterial).color.copy(this.chipColor);
   this.chip.updateWorldMatrix(true,false);this.chip.getWorldPosition(this.chipAt);
   this.glow.position.copy(this.chipAt);this.glow.position.y+=.08;this.glow.color.copy(this.chipColor);this.glow.intensity=low?0:2.4;
   this.rings.forEach((ring,i)=>{const t=(g.e-.48-i*.08)/.5;ring.visible=!reduced&&t>0&&t<1;if(!ring.visible)return;ring.position.copy(this.chipAt);ring.lookAt(this.camera.position);ring.scale.setScalar(.5+t*1.7);const m=ring.material as T.MeshBasicMaterial;m.color.copy(this.chipColor);m.opacity=.85*(1-t);});
  }else{this.chip.visible=false;this.glow.intensity=0;this.rings.forEach(ring=>ring.visible=false);}
  // Picker colour normally; the chip's rarity takes over once it is chosen.
  this.tint.set(p.color);if(chosen)this.tint.lerp(this.chipColor,Math.min(1,g!.e/.5));this.rimTint.copy(this.tint).lerp(_white,.25);
  for(const m of [this.halo,this.glowBack,...this.bokeh])(m.material as T.MeshBasicMaterial).color.copy(this.tint).multiplyScalar(fade);
  this.accent.color.copy(this.tint);for(const rim of this.rims)rim.color.copy(this.rimTint);
  for(const [light,base] of this.lights)light.intensity=base*fade;
  // The out-of-focus lights drift slowly upwards and wrap (pure function of the presentation clock).
  const drift=reduced?0:this.time;BOKEH.forEach(([x,y,,,,speed,phase],i)=>{const b=this.bokeh[i],span=3.4;b.position.y=.4+((y-.4+drift*speed)%span+span)%span;b.position.x=x+.08*Math.sin(drift*.2+phase);});
  this.armor.update(this.boss||low?[]:[{rig:r,player:p,light:false}],state.time,quality,compatibility);
  return true;
 }
 /** The staged picker's rig (tests and tools read it). */
 get pilot(){return this.rig;}
 /** Viewport pixels → a world point on the plane z = DRAFT_LOOK_Z (for card centres). */
 toWorld(x:number,y:number,out:T.Vector3){
  this.ray.setFromCamera(_ndc.set(x/innerWidth*2-1,-(y/innerHeight)*2+1),this.camera);return this.ray.ray.intersectPlane(this.plane,out);
 }
 /** Viewport pixels of the reaching palm, for flying the chosen card into the hand. */
 palmScreen(out:{x:number;y:number}){
  if(!this.rig||!this.active)return false;const socket=this.side>0?this.rig.sockets.leftHand:this.rig.sockets.rightHand;
  socket.updateWorldMatrix(true,false);socket.localToWorld(_e.copy(PALM)).project(this.camera);
  out.x=(_e.x+1)/2*innerWidth;out.y=(1-_e.y)/2*innerHeight;return true;
 }
 clear(){
  this.chip.removeFromParent();this.chip.visible=false;this.glow.intensity=0;this.armor.clear();this.sidePick='';
  if(this.rig){disposeAuditorHalo(this.rig);this.rig.group.removeFromParent();this.rig.sockets.head.traverse(o=>{if(o instanceof T.Mesh&&o.geometry.type!=='BoxGeometry')o.geometry.dispose();});}
  this.rig=undefined;this.low=undefined;this.key='';this.taken=-1;this.hover=-1;this.cards=[];this.frameTop=-1;this.last=NaN;this.boss=false;
  for(const arm of this.arms){arm.w=arm.v=0;arm.aimed=false;}
 }
}
