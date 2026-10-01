import * as T from 'three';
import type {CharacterRig} from '../character-rig.js';
import {SIREN_INTRO} from '../../shared/siren-contract.js';
import {buildSiren,applySirenLook} from './model.js';
import {updateSirenFlames} from './motion.js';
import {tickSirenClock,glowMaterial,haloMaterial,setSirenRim} from './materials.js';

const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
const sm=(x:number)=>{x=clamp(x);return x*x*(3-2*x);};
const cue=(name:typeof SIREN_INTRO.cues[number]['cue'])=>SIREN_INTRO.cues.find(c=>c.cue===name)!.time;
const T_BULB=cue('bulb'),T_FLICKER=cue('flicker'),T_GLIMPSE=cue('glimpse'),T_REVEAL=cue('reveal'),T_BLACKOUT=cue('blackout'),T_SMILE=cue('smile');
const BEATS=[.1858,.6037,1.0159,1.428,1.8402,2.2523,2.6645,3.0883,3.5004,3.9184,4.3421,4.7543,5.178,5.596,6.0082,6.4261,6.8441,7.2678];

/**
 * dev-siren intro: an empty, dark cell. A bulb buzzes on; it starts to stutter on the off-beats; in one flicker the
 * Siren is suddenly in frame, then gone; on bar 3 the light steadies on it, chest-up, head cocked, blank white eyes.
 * Bar 4: the lights die, only the eyes remain, and the jagged grin opens in the dark as it starts to laugh.
 * Everything is a pure function of the intro age, so every viewer sees the same frame.
 */
export class SirenIntroScene {
 readonly group=new T.Group();private active=false;private saved=new Map<T.Light,number>();private savedBg:T.Color|T.Texture|null=null;private savedFog:T.Fog|T.FogExp2|null=null;
 private bulb=new T.PointLight(0xe9f0d8,0,6.5,1.6);private fill=new T.PointLight(0x6a5f8e,0,5,2);private ambient=new T.AmbientLight(0x2a2638,0);
 private bulbMesh:T.Mesh;private bulbMat=glowMaterial(0xfff6d8,{intensity:1});private bulbHalo=new T.Sprite(haloMaterial(0xfff1cf,.0));private cord:T.Mesh;
 private lastAge=0;private background=new T.Color(0x000000);private fog=new T.Fog(0x000000,2.5,7.5);
 constructor(private scene:T.Scene){
  this.group.name='siren-intro';this.group.visible=false;scene.add(this.group);
  // A bare concrete corner: floor, two walls, a low ceiling, scratched tally marks, a stain, a chair in the dark.
  const concrete=new T.MeshLambertMaterial({color:0x4a4a50}),dirty=new T.MeshLambertMaterial({color:0x34333a}),scratch=new T.MeshLambertMaterial({color:0x1a191e});
  const box=(w:number,h:number,d:number,x:number,y:number,z:number,m:T.Material)=>{const b=new T.Mesh(new T.BoxGeometry(w,h,d),m);b.position.set(x,y,z);b.receiveShadow=true;b.castShadow=true;this.group.add(b);return b;};
  box(7,.1,7,0,-.045,-1,dirty);box(7,3.4,.2,0,1.6,-2.2,concrete);box(.2,3.4,7,-2.2,1.6,-1,concrete);box(.2,3.4,7,2.6,1.6,-1,concrete);box(7,.2,7,0,3.1,-1,dirty);
  // Wall tiles: a faint grid of seams.
  for(let x=-2;x<=2.4;x+=.6)box(.02,3.2,.01,x,1.6,-2.09,scratch);for(let y=.3;y<3;y+=.6)box(4.8,.02,.01,.2,y,-2.09,scratch);
  // Tally marks scratched into the back wall, a few in a crazed hand.
  for(let g=0;g<6;g++)for(let i=0;i<5;i++){const m=box(.018,.28,.012,-1.5+g*.42+i*.055,1.55+(g%2)*.42,-2.085,scratch);if(i===4){m.rotation.z=1.15;m.position.x-=.11;m.scale.y=1.5;}}
  const stain=new T.Mesh(new T.CircleGeometry(.55,20),new T.MeshBasicMaterial({color:0x0c0b0e,transparent:true,opacity:.55,depthWrite:false}));stain.position.set(.8,1.1,-2.08);this.group.add(stain);
  const chair=new T.Group();chair.position.set(1.4,0,-1.4);chair.rotation.y=-.6;const steel=new T.MeshLambertMaterial({color:0x26262b});
  for(const [x,z] of [[-.2,-.2],[.2,-.2],[-.2,.2],[.2,.2]]){const l=new T.Mesh(new T.BoxGeometry(.04,.45,.04),steel);l.position.set(x,.22,z);chair.add(l);}
  const seat=new T.Mesh(new T.BoxGeometry(.46,.05,.46),steel);seat.position.y=.46;chair.add(seat);const back=new T.Mesh(new T.BoxGeometry(.46,.5,.04),steel);back.position.set(0,.72,-.21);chair.add(back);this.group.add(chair);
  // The bulb on its cord, above and in front of the Siren's face.
  this.cord=new T.Mesh(new T.BoxGeometry(.012,.92,.012),new T.MeshLambertMaterial({color:0x111111}));this.cord.position.set(.62,2.48,.32);this.group.add(this.cord);
  this.bulbMesh=new T.Mesh(new T.SphereGeometry(.055,12,8),this.bulbMat);this.bulbMesh.position.set(.62,2.02,.32);this.group.add(this.bulbMesh);
  this.bulbHalo.position.copy(this.bulbMesh.position);this.bulbHalo.scale.setScalar(.9);this.group.add(this.bulbHalo);
  this.bulb.position.copy(this.bulbMesh.position);this.bulb.castShadow=true;this.bulb.shadow.mapSize.set(512,512);this.group.add(this.bulb,this.fill,this.ambient);this.fill.position.set(-.9,1.2,1.4);
 }
 setActive(on:boolean){
  if(on===this.active)return;this.active=on;this.group.visible=on;
  if(on){this.savedBg=this.scene.background as T.Color|T.Texture|null;this.savedFog=this.scene.fog;this.scene.traverse(o=>{if(o instanceof T.Light&&!this.own(o))this.saved.set(o,o.intensity);});}
  else{for(const [l,i] of this.saved)l.intensity=i;this.saved.clear();this.scene.background=this.savedBg;this.scene.fog=this.savedFog;setSirenRim(1);}
 }
 private own(o:T.Object3D){for(let x:T.Object3D|null=o;x;x=x.parent)if(x===this.group)return true;return false;}

 update(age:number,rig:CharacterRig,camera:T.PerspectiveCamera,quality:number){
  if(!this.active)this.setActive(true);
  // The generic intro lights and anything else lit in the intro scene go dark while the Siren owns it.
  this.scene.traverse(o=>{if(o instanceof T.Light&&!this.own(o)){if(!this.saved.has(o))this.saved.set(o,o.intensity);o.intensity=0;}if(o instanceof T.Sprite&&o.name==='armor-radiance')o.visible=false;});
  this.scene.background=this.background;this.scene.fog=this.fog;tickSirenClock(age);
  const parts=buildSiren(rig),dt=clamp(age-this.lastAge,0,.05);this.lastAge=age;

  // Light: off, a buzzing dim bulb, stutter on the off-beats, the glimpse, the steady sickly reveal, blackout.
  let light=0;
  if(age>=T_BULB&&age<T_FLICKER)light=.55+.08*Math.sin(age*90)+(age-T_BULB<.08?-.4:0);
  else if(age>=T_FLICKER&&age<T_REVEAL){let b=-1;for(let i=0;i<BEATS.length;i++)if(BEATS[i]<=age)b=i;const phase=b>=0&&b<BEATS.length-1?(age-BEATS[b])/(BEATS[b+1]-BEATS[b]):0;
   // Off-beat stutters (dark around each half beat), plus the hard flash that shows the Siren.
   const off=phase>.42&&phase<.62||(b%2===1&&phase>.15&&phase<.24);light=off?.04:.62+.1*Math.sin(age*130);if(age>=T_GLIMPSE&&age<T_GLIMPSE+.11)light=1.15;else if(age>=T_GLIMPSE+.11&&age<T_GLIMPSE+.32)light=0;}
  else if(age>=T_REVEAL&&age<T_BLACKOUT)light=.78+.05*Math.sin(age*70)-(age-T_REVEAL<.05?.5:0);
  this.bulb.intensity=light*38;this.fill.intensity=light*4;this.ambient.intensity=light>0?.12+.35*light:0;
  this.bulbMat.uniforms.uIntensity.value=.15+light*1.1;(this.bulbHalo.material as T.SpriteMaterial).opacity=.55*light;
  this.cord.rotation.z=.02*Math.sin(age*1.3);this.bulbMesh.position.x=.62+.025*Math.sin(age*1.3);this.bulb.position.x=this.bulbMesh.position.x;this.bulbHalo.position.x=this.bulbMesh.position.x;

  // Where the Siren is: absent, a single-flash glimpse, then right there.
  const present=age>=T_GLIMPSE&&age<T_GLIMPSE+.11||age>=T_REVEAL;rig.group.visible=present;
  rig.group.position.set(0,.86,0);rig.group.rotation.set(0,0,0);
  const laugh=sm((age-T_SMILE)/.25),shake=Math.sin((age-T_SMILE)*Math.PI*2*6.5)*laugh;
  for(const o of [rig.body,rig.pelvis,rig.spine,rig.chest,rig.neck,rig.head,rig.leftShoulder,rig.rightShoulder,rig.leftArm,rig.rightArm,rig.leftElbow,rig.rightElbow,rig.leftHand,rig.rightHand,rig.leftLeg,rig.rightLeg,rig.leftKnee,rig.rightKnee])o.rotation.set(0,0,0);
  const breath=Math.sin(age*1.6),still=sm((age-T_REVEAL)/.4);
  rig.body.position.set(0,.06+.012*breath,0);rig.spine.rotation.x=.16;rig.chest.rotation.set(.12+.02*breath-.08*laugh+.05*shake,0,.03*shake);rig.neck.rotation.x=-.18;
  // The head: cocked hard to the side, a slow creeping extra tilt during the stare, juddering with the laugh.
  rig.head.rotation.set(.06-.12*laugh+.06*shake,.05*shake,.62+.12*still*(1-laugh)+.05*shake);
  rig.leftShoulder.rotation.z=.18+.06*shake;rig.rightShoulder.rotation.z=-.18-.06*shake;
  rig.leftArm.rotation.set(-.3,0,.12);rig.rightArm.rotation.set(-.32,0,-.1);rig.leftElbow.rotation.x=-.5;rig.rightElbow.rotation.x=-.55;rig.leftKnee.rotation.x=.8;rig.rightKnee.rotation.x=.9;
  // Face: blank ovals during the stare; in the dark the grin draws itself open from the middle and the eyes narrow.
  const s=parts.state;s.time=age;s.ranged=0;s.open=.15*laugh+.25*Math.max(0,shake);s.squint=.45*laugh;s.anger=.0+.9*laugh;s.flare=0;s.glow=1;s.lean=0;applySirenLook(parts);
  parts.grin.visible=age>=T_SMILE;parts.grin.scale.set(Math.max(.001,sm((age-T_SMILE)/.3)),1,1);
  for(const e of parts.eyes)e.visible=present||age>=T_BLACKOUT;
  // Eyes glow a touch harder once the room goes dark.
  for(const h of parts.eyeHalos){h.visible=present;(h.material as T.SpriteMaterial).opacity=age>=T_BLACKOUT?.6:.35;}
  parts.shadow.visible=false;setSirenRim(age>=T_BLACKOUT?.25:1);
  rig.group.updateMatrixWorld(true);updateSirenFlames(rig,parts,age,dt,quality*(light>0||age<T_BLACKOUT?1:.25),!present);

  // Camera: chest-up, almost still, creeping closer; a tiny handheld drift.
  const push=sm(age/SIREN_INTRO.duration);camera.position.set(.04*Math.sin(age*.7),1.42+.01*Math.sin(age*1.1),2.05-.3*push);camera.up.set(0,1,0);camera.lookAt(0,1.4,0);camera.fov=40;camera.updateProjectionMatrix();
 }
}
