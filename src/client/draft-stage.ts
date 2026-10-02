import * as T from 'three';
import {createCharacterRig,settleJointCovers,setRigDetail,type CharacterRig} from './character-rig.js';
import {dressRig} from './cosmetics.js';
import {teamArmor} from './team-identity.js';
import {buildWeapon} from './weapon-visuals.js';
import {animateLobbyPose,lobbyPlayer} from './lobby-poses.js';
import {sirenify,animateSiren} from './siren/index.js';
import {animateCosmetics,disposeAuditorHalo} from './auditor-halo.js';
import {ArmorGlow} from './armor-glow.js';
import {hand} from './rig-ik.js';
import {lobbyPose} from '../shared/lobby-presentation.js';
import {CHIPS,RARITIES} from '../shared/content.js';
import type {PlayerView,Snapshot} from '../shared/simulation.js';
import {grabTimeline} from './draft-timeline.js';

/** Card centres are mapped onto this plane in front of the pilot, so the head turns out towards the card on screen. */
export const DRAFT_LOOK_Z=1.6;
const HEAD_UP=.165,REACH=.47,YAW_LIMIT=1.05,PITCH_DOWN=.8,PITCH_UP=.4;
const _local=new T.Vector3(),_ndc=new T.Vector2(),_wrist=new T.Euler(),_w=new T.Quaternion(),_p=new T.Quaternion(),_q=new T.Quaternion(),_i=new T.Quaternion(),_f=new T.Vector3(),_d=new T.Vector3(),_e=new T.Vector3(),_s=new T.Vector3(),_t=new T.Vector3();
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
export function lookAt(r:CharacterRig,target:T.Vector3,weight:number){
 if(weight<=0)return;r.group.updateMatrixWorld(true);
 r.head.getWorldPosition(_s);_s.y+=HEAD_UP;_t.copy(target).sub(_s);const dist=Math.max(.3,_t.length());
 r.group.getWorldQuaternion(_w);_t.applyQuaternion(_w.invert());
 const yaw=Math.max(-YAW_LIMIT,Math.min(YAW_LIMIT,Math.atan2(_t.x,_t.z))),flat=Math.hypot(_t.x,_t.z),pitch=Math.max(-PITCH_DOWN,Math.min(PITCH_UP,Math.atan2(_t.y,flat)));
 _t.set(Math.sin(yaw)*Math.cos(pitch),Math.sin(pitch),Math.cos(yaw)*Math.cos(pitch)).multiplyScalar(dist);r.group.getWorldQuaternion(_w);_t.applyQuaternion(_w).add(_s);
 share(r.chest,_t,.22*weight);share(r.neck,_t,.4*weight);share(r.head,_t,weight,HEAD_UP);
}
/** Reach layer: the wrist goes towards a world point, clamped to arm's length so a far card gets an outstretched hand. */
export function reachFor(r:CharacterRig,left:boolean,target:T.Vector3,weight:number,orientation:T.Euler){
 if(weight<=0)return;r.group.updateMatrixWorld(true);(left?r.leftArm:r.rightArm).getWorldPosition(_s);
 _t.copy(target).sub(_s);const len=_t.length();if(len>1e-6)_t.multiplyScalar(Math.min(len,REACH)/len);_t.add(_s);
 hand(r,left,r.chest.worldToLocal(_local.copy(_t)),orientation,weight);
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
const PALM=new T.Vector3(0,-.035,.04);
/** The picker as the stage poses them: alive, standing and idle, whatever the last round left behind (set losers are
 * still at hp 0, possibly knocked down, mid-move or ragdolled, while they draft). */
export function stageView(p:PlayerView):PlayerView{
 return {...lobbyPlayer(p),hp:Math.max(1,p.maxHp||1),death:undefined,hitReaction:undefined,roll:undefined,attackCommitment:undefined,meleeAction:undefined,animationTime:undefined,siren:p.siren?{...p.siren,move:undefined} as PlayerView['siren']:undefined};
}
/**
 * Full-screen draft scene rendered by the main renderer in place of the frozen arena (the PlayerIntros pattern). It shows
 * the current picker: idle pose from their lobby choice, a head that follows the hovered (or booting / scanned) card,
 * and on the pick a reach, the chip landing in the palm, and a pull up to the chest while the details spotlight opens.
 */
export class DraftStage {
 readonly scene=new T.Scene();readonly camera=new T.PerspectiveCamera(30,1,.1,60);
 /** World points of the card centres on the look plane, in offer order (set by the DOM overlay each frame). */
 cards:T.Vector3[]=[];hover=-1;taken=-1;active=false;
 /** Viewport fractions (0 top, 1 bottom) of the heading's bottom edge and the tray's top edge, from the DOM overlay. */
 headLine=.14;trayLine=.74;private frameHead=-1;private frameTray=-1;
 private armor=new ArmorGlow(this.scene);private rig?:CharacterRig;private key='';private time=0;private side=1;
 private look=new T.Vector3(0,1.2,3);private goal=new T.Vector3();private weight=0;private chip=chipModel();
 private rings:T.Mesh[]=[];private backdrop:T.Mesh;private accent:T.PointLight;private ray=new T.Raycaster();private plane=new T.Plane(new T.Vector3(0,0,1),-DRAFT_LOOK_Z);
 private motion=typeof matchMedia==='function'?matchMedia('(prefers-reduced-motion: reduce)'):undefined;private low:boolean|undefined;private sidePick='';private glow=new T.PointLight(0xffffff,0,1.8,2);private tint=new T.Color();private chipColor=new T.Color();private hold=new T.Vector3();private reach=new T.Vector3();private chipAt=new T.Vector3();
 constructor(){
  this.scene.background=new T.Color(0x06070b);this.scene.fog=new T.Fog(0x06070b,7,18);
  this.scene.add(new T.HemisphereLight(0xdfe7ff,0x2a2433,1.25));
  const key=new T.DirectionalLight(0xffeedd,2.1);key.position.set(2.6,4,3.2);this.scene.add(key);
  const rim=new T.DirectionalLight(0x8aa2ff,1.6);rim.position.set(-3,2.5,-2.5);this.scene.add(rim);
  this.accent=new T.PointLight(0xff6b35,6,7,2);this.accent.position.set(0,1.6,-1.4);this.scene.add(this.accent);
  const floor=new T.Mesh(new T.CircleGeometry(9,48),new T.MeshLambertMaterial({color:0x0c0d13}));floor.rotation.x=-Math.PI/2;floor.position.y=-.065;this.scene.add(floor);
  const podium=new T.Mesh(new T.CylinderGeometry(.92,1,.07,40),new T.MeshLambertMaterial({color:0x1b1c24}));podium.position.y=-.03;this.scene.add(podium);
  const edge=new T.Mesh(new T.RingGeometry(.98,1.02,64),new T.MeshBasicMaterial({color:0xff6b35,transparent:true,opacity:.55,side:T.DoubleSide,toneMapped:false}));edge.rotation.x=-Math.PI/2;edge.position.y=.008;edge.name='podium-edge';this.scene.add(edge);
  // A soft coloured glow behind the pilot separates the silhouette from the dark backdrop.
  const g=new T.RingGeometry(0,3.2,48,6),pos=g.attributes.position,colors=new Float32Array(pos.count*4);for(let i=0;i<pos.count;i++){const r=Math.hypot(pos.getX(i),pos.getY(i))/3.2;colors.set([1,1,1,Math.max(0,.5*(1-r)**2)],i*4);}g.setAttribute('color',new T.BufferAttribute(colors,4));
  this.backdrop=new T.Mesh(g,new T.MeshBasicMaterial({vertexColors:true,transparent:true,depthWrite:false,toneMapped:false,blending:T.AdditiveBlending,color:0xff6b35}));this.backdrop.position.set(0,1.15,-2.2);this.scene.add(this.backdrop);
  for(let i=0;i<2;i++){const ring=new T.Mesh(new T.TorusGeometry(.13,.007,6,40),new T.MeshBasicMaterial({color:0xffffff,transparent:true,opacity:0,depthWrite:false,toneMapped:false,blending:T.AdditiveBlending}));ring.visible=false;this.scene.add(ring);this.rings.push(ring);}
  this.chip.visible=false;this.scene.add(this.glow);
 }
 private build(p:PlayerView,state:Snapshot){
  this.clear();const r=createCharacterRig(p.color,p.cosmetics?.bodyColor,true);dressRig(r,p.cosmetics);teamArmor(r,state.options.gameMode==='teams'?p.team:undefined);
  r.group.position.y=.88;r.sockets.rightHand.add(r.weapon);r.sockets.leftHand.add(r.offWeapon);buildWeapon(r.weapon,p.spec);if(['daggers','emperor','ansatsuken'].includes(p.spec))buildWeapon(r.offWeapon,p.spec,true);
  if(p.spec==='siren')sirenify(r);this.scene.add(r.group);this.rig=r;this.weight=0;
 }
 /** Frame the whole pilot between the heading and the tray: hat just under the heading, feet just above the cards,
  * whatever the tray height (one or two rows) and aspect. */
 private frameDt=0;
 private frame(aspect:number){
  if(this.frameHead<0){this.frameHead=this.headLine;this.frameTray=this.trayLine;}const k=1-Math.exp(-this.frameDt*6);this.frameHead+=(this.headLine-this.frameHead)*k;this.frameTray+=(this.trayLine-this.frameTray)*k;
  const top=Math.min(.4,Math.max(.06,this.frameHead+.03)),feet=Math.min(.97,Math.max(top+.24,this.frameTray-.015));
  const half=Math.max(1.85/(feet-top),2.1/aspect)/2,fov=30,dist=half/Math.tan(fov*Math.PI/360),centre=half*(2*feet-1);
  this.camera.aspect=aspect;this.camera.fov=fov;this.camera.position.set(0,centre+.3,dist);this.camera.lookAt(0,centre,0);this.camera.updateProjectionMatrix();this.camera.updateMatrixWorld();
 }
 update(state:Snapshot,aspect:number,dt:number,quality=1,compatibility=false,reduced=this.motion?.matches??false){
  const d=state.draft,live=d&&state.phase==='draft'?state.players.find(q=>q.id===d.player):undefined;
  if(!d||!live||compatibility){this.clear();this.active=false;return false;}
  const p=stageView(live),key=p.id+p.spec+JSON.stringify(p.cosmetics)+p.color+(state.options.gameMode==='teams'?p.team:'');
  if(key!==this.key||!this.rig){this.build(p,state);this.key=key;}
  const r=this.rig!;this.active=true;dt=Math.min(.05,Math.max(0,dt));if(!reduced)this.time+=dt;this.frameDt=dt;this.frame(aspect);
  const low=quality<.45;if(this.low!==low){setRigDetail(r,low);this.low=low;}
  const pose=lobbyPose(p.cosmetics?.lobbyPose),stand=pose==='chair'||pose==='sleep'?'weapon':pose;
  // Set losers draft while still at hp 0 from the last round: pose a cleaned-up view, never the fight state.
  if(p.spec==='siren')animateSiren(r,p,state,reduced?0:state.time,dt,0);else animateLobbyPose(r,p,this.time,dt,stand);
  animateCosmetics(r,this.time);
  const chosen=d.chosen?CHIPS.find(c=>c.id===d.chosen):undefined,g=chosen?grabTimeline(state.time-(d.chosenAt??state.time)):undefined;
  const card=this.taken>=0?this.cards[this.taken]:undefined;
  // The reaching hand is the one on the card's side, chosen once per pick (frame rate independent).
  const pick=`${d.player}:${d.turn}:${d.chosenAt}`;if(g&&card&&this.sidePick!==pick){this.sidePick=pick;this.side=card.x>=0?1:-1;}
  const left=this.side>0,socket=left?r.sockets.leftHand:r.sockets.rightHand;
  // Reach first, so the gaze below follows where the hand really is.
  if(g&&card){
   r.weapon.visible=false;r.offWeapon.visible=false;
   r.group.updateMatrixWorld(true);r.chest.localToWorld(this.hold.set(left?.11:-.11,.1,.33));
   reachFor(r,left,this.reach.copy(card).lerp(this.hold,g.pull),g.reach,_wrist.set(-1.45+.45*g.pull,0,(left?-1:1)*.5*g.pull));
  }
  // Where to look: the chip in hand, the chosen card, the hovered card, the card booting in, or a slow scan.
  r.group.updateMatrixWorld(true);
  if(g&&g.contact)socket.localToWorld(this.goal.copy(PALM));
  else if(g&&card)this.goal.copy(card);
  else if(this.hover>=0&&this.cards[this.hover])this.goal.copy(this.cards[this.hover]);
  else if(this.cards.length){const booting=state.time<d.readyAt,index=reduced?0:booting?Math.min(this.cards.length-1,Math.floor(Math.max(0,state.time-d.started)/.22)):Math.floor(this.time/1.8)%this.cards.length;this.goal.copy(this.cards[index]);}
  else this.goal.set(0,1.3,3);
  this.look.lerp(this.goal,1-Math.exp(-dt*(g?16:7)));this.weight+=(1-this.weight)*(1-Math.exp(-dt*4));
  lookAt(r,this.look,this.weight);settleJointCovers(r);
  // The chip in the palm, its light and a two-ring flash at contact. The light lives in the scene permanently, so the
  // light count (and every lit shader) stays the same when the chip appears.
  const rarity=chosen?RARITIES[chosen.rarity].color:p.color;this.chipColor.set(rarity);
  if(g&&g.contact){if(this.chip.parent!==socket)socket.add(this.chip);this.chip.position.copy(PALM);this.chip.rotation.set(Math.PI/2,0,0);
   const pop=Math.min(1,(g.e-.48)/.12);this.chip.scale.setScalar(.5+.5*pop+(reduced?0:.08*Math.sin(this.time*5)));this.chip.visible=true;
   ((this.chip.getObjectByName('die') as T.Mesh).material as T.MeshBasicMaterial).color.copy(this.chipColor);
   this.chip.updateWorldMatrix(true,false);this.chip.getWorldPosition(this.chipAt);
   this.glow.position.copy(this.chipAt);this.glow.position.y+=.08;this.glow.color.copy(this.chipColor);this.glow.intensity=low?0:2.4;
   this.rings.forEach((ring,i)=>{const t=(g.e-.48-i*.08)/.5;ring.visible=!reduced&&t>0&&t<1;if(!ring.visible)return;ring.position.copy(this.chipAt);ring.lookAt(this.camera.position);ring.scale.setScalar(.6+t*3.2);const m=ring.material as T.MeshBasicMaterial;m.color.copy(this.chipColor);m.opacity=.85*(1-t);});
  }else{this.chip.visible=false;this.glow.intensity=0;this.rings.forEach(ring=>ring.visible=false);}
  // Picker colour normally; the chip's rarity takes over once it is chosen.
  this.tint.set(p.color);if(chosen)this.tint.lerp(this.chipColor,Math.min(1,g!.e/.5));
  (this.backdrop.material as T.MeshBasicMaterial).color.copy(this.tint);this.accent.color.copy(this.tint);
  ((this.scene.getObjectByName('podium-edge') as T.Mesh).material as T.MeshBasicMaterial).color.copy(this.tint);
  this.armor.update(p.spec==='siren'||low?[]:[{rig:r,player:p,light:false}],state.time,quality,compatibility);
  return true;
 }
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
  this.rig=undefined;this.low=undefined;this.key='';this.taken=-1;this.hover=-1;this.cards=[];this.frameHead=this.frameTray=-1;
 }
}
