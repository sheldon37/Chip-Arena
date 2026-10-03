import * as T from 'three';
import type {PlayerView} from '../shared/simulation.js';
import {WEAPONS} from '../shared/content.js';
import {VIEW} from '../shared/view.js';
import {animateCharacter,type AnimatedRig} from './character-animation.js';
import {springStep,clockStep,reloadPhase} from './ranged-motion.js';
import {magicGuardWeight} from './magic-motion.js';
import {adsProfile,type AdsProfile} from '../shared/abilities.js';
/** First-person view model: animates the camera-attached rig, then places the view root (the renderer hides the rig's head,
 * legs and torso meshes once at construction and marks it with markFirstPersonRig).
 * Presentation only, per view root: the torso's big yaw/translation is damped so strikes and casts stay on screen, and a
 * subtle view layer adds stride bob, a look-sway spring from aim deltas, a landing kick, a recoil push and a reload dip.
 * Springs start at rest, clamp dt to .05 and reset on backward time or jumps over .35 s. */
/** View-model placement per class (exported for the numeric probes; presentation only). */
/** Staff: x .20 / y -.50, FP-only scale .85 and its top rolled 20° outward about the grip; FP chip modules ×.5 (design §10.3,
 * PROBE fp-staff2: 0 % crosshair cover, ~9 % of the left half against 12-35 % for the rejected x .24). */
export const FP_TUNE={meleeX:.16,meleeY:-.22,meleeZ:-.3,magicX:.2,magicY:-.5,magicStaffScale:.85,magicRoll:20,moduleScale:.5};
/** ADS view-model offsets (Δx, Δy) per gun (§12.1; PROBE: muzzle on the crosshair column, sights just under it). */
export const ADS_VIEW:Record<string,[number,number]>={pistol:[-.27,.19],machinegun:[-.31,.175],rifle:[-.33,.1]};
interface ViewState{t:number;yaw:number;pitch:number;sx:number;sxv:number;sy:number;syv:number;roll:number;rollv:number;land:number;landv:number;grounded:boolean;vy:number;ads:number;adsv:number}
const states=new WeakMap<object,ViewState>();
const _o:[number,number]=[0,0],_qa=new T.Quaternion(),_qb=new T.Quaternion(),_qc=new T.Quaternion(),_f=new T.Vector3();
const wrap=(a:number)=>Math.atan2(Math.sin(a),Math.cos(a));
/** Damp the rig's torso motion for the view model: strikes keep their arm and weapon arcs, but the camera-attached torso
 * no longer swings the hands off screen. */
function settleViewTorso(r:AnimatedRig){
 // Shoulder caps and upper arms (including armour added later) sit right in front of the camera: keep them hidden, so
 // the view model reads as forearms, hands and the weapon.
 for(const clavicle of [r.leftShoulder,r.rightShoulder,r.leftArm,r.rightArm])for(const child of clavicle.children)if((child as T.Mesh).isMesh)child.visible=false;
 r.body.rotation.y*=.2;r.pelvis.rotation.y*=.2;r.spine.rotation.y*=.2;
 r.body.position.x=Math.max(-.03,Math.min(.03,r.body.position.x));r.body.position.z=Math.max(-.03,Math.min(.03,r.body.position.z));r.body.position.y*=.5;
}
/** `aim`: the local ADS profile (null = not aiming); undefined derives it from the snapshot's adsSince (lab, probes). A legacy
 * scope (`scoped` without a profile) hides the view model as before. Returns the ADS blend (0..1). */
export function poseFirstPersonView(firstRig:AnimatedRig,viewRoot:T.Object3D,self:PlayerView,time:number,dt:number,scoped:boolean,aim?:AdsProfile|null){
 animateCharacter(firstRig,self,time,dt,0);
 const fighter=self.spec==='ansatsuken'||self.spec==='emperor',w=WEAPONS[self.spec],cls=w?.class;
 if(!fighter)settleViewTorso(firstRig);
 let s=states.get(viewRoot);const clock=clockStep(s,time),h=clock.dt;
 const prof=aim===undefined?self.adsSince!==undefined?adsProfile(self.spec):undefined:aim,adsT=prof&&cls==='ranged'?1:0;
 if(!s||clock.fresh){s={t:time,yaw:self.yaw,pitch:self.pitch,sx:0,sxv:0,sy:0,syv:0,roll:0,rollv:0,land:0,landv:0,grounded:self.grounded,vy:self.vel.y,ads:adsT,adsv:0};states.set(viewRoot,s);}
 // Look sway: the weapon lags a turn slightly, then springs back (a few centimetres at most).
 const dyaw=wrap(self.yaw-s.yaw),dpitch=self.pitch-s.pitch,rate=h>0?1/h:0;
 const tx=Math.max(-.03,Math.min(.03,-dyaw*rate*.004)),ty=Math.max(-.025,Math.min(.025,-dpitch*rate*.004)),tr=Math.max(-.05,Math.min(.05,dyaw*rate*.008));
 if(h>0){springStep(s.sx,s.sxv,tx,14,.8,h,_o);s.sx=_o[0];s.sxv=_o[1];springStep(s.sy,s.syv,ty,14,.8,h,_o);s.sy=_o[0];s.syv=_o[1];springStep(s.roll,s.rollv,tr,14,.8,h,_o);s.roll=_o[0];s.rollv=_o[1];
  // Landing: the grounded edge kicks the view model down in proportion to the fall speed, then it springs back.
  if(self.grounded&&!s.grounded)s.landv-=Math.min(1,Math.max(0,-s.vy)/9)*.9;
  springStep(s.land,s.landv,0,16,.55,h,_o);s.land=_o[0];s.landv=_o[1];
  // ADS: the camera snaps (renderer); the view model blends over ~.12 s.
  springStep(s.ads,s.adsv,adsT,26,1,h,_o);s.ads=Math.max(0,Math.min(1,_o[0]));s.adsv=_o[1];}
 s.t=time;s.yaw=self.yaw;s.pitch=self.pitch;s.grounded=self.grounded;if(!self.grounded)s.vy=self.vel.y;
 // Stride bob from the rig's own stride phase, scaled by how much it is running (subtle).
 const run=Math.min(1,firstRig.motion),bx=Math.sin(firstRig.stride)*.012*run,by=-Math.abs(Math.cos(firstRig.stride))*.015*run;
 // Recoil push and reload dip for guns.
 const a=s.ads,P=prof??adsProfile(self.spec),shotAge=time-(self.lastShot??self.lastAttack),kick=cls==='ranged'&&shotAge>=0?Math.exp(-shotAge*(self.spec==='rifle'?14:20))*(self.spec==='rifle'?1.4:self.spec==='machinegun'?.6:1)*(1+((P?.kick??1)-1)*a):0;
 const reload=cls==='ranged'?reloadPhase(self,w).envelope:0,runLow=cls==='ranged'?run*Math.min(1,Math.max(0,(shotAge-.3)/.3))*(1-reload):0;
 // Per class: guns sit right of centre (the renderer's original placement); the staff hand moves right so its crystal no
 // longer fills the left of the view; melee is raised a little so strikes read.
 const t=FP_TUNE,o=ADS_VIEW[self.spec]??[0,0],baseX=fighter?0:cls==='magic'?t.magicX:cls==='melee'?t.meleeX:.24+o[0]*a,baseY=fighter?-.48:cls==='melee'?t.meleeY:cls==='magic'?t.magicY:-.48+o[1]*a,baseZ=cls==='melee'&&!fighter?t.meleeZ:-.2;
 viewRoot.position.set(baseX+(bx+s.sx)*(1-a*.7),baseY+(by+s.sy)*(1-a*.7)+s.land*.05-.06*reload-.03*runLow,baseZ+.04*kick);
 // Narrower ADS FOV: counter-scale so the gun does not balloon; the rifle hides behind its scope overlay.
 viewRoot.rotation.set(.06*kick-.1*runLow,Math.PI,s.roll+.25*reload);viewRoot.scale.setScalar(.78*(1+(P&&a?Math.tan(P.fov*Math.PI/360)/Math.tan(VIEW.firstFov*Math.PI/360)-1:0)*a));
 viewRoot.visible=!(scoped&&!prof)&&!(P?.overlay==='scope'&&a>.6);
 // Staff: FP-only scale and roll about the grip around the camera's view axis (top toward the left edge), faded out in the guard.
 const wpn=firstRig.weapon;if(cls==='magic'){const g=magicGuardWeight(firstRig);wpn.scale.setScalar(t.magicStaffScale+(1-t.magicStaffScale)*g);
  if(t.magicRoll&&g<1){const qp=wpn.parent!.getWorldQuaternion(_qa).invert(),qw=wpn.getWorldQuaternion(_qb),cam=viewRoot.parent;_f.set(0,0,1);if(cam)_f.applyQuaternion(cam.getWorldQuaternion(_qc));
   wpn.quaternion.copy(qp.multiply(_qc.setFromAxisAngle(_f,t.magicRoll*(1-g)*Math.PI/180)).multiply(qw));}}
 // Chip modules: half size in first person (every class), hidden over half ADS.
 for(const w of [wpn,firstRig.offWeapon]){const m=w.getObjectByName('chip-modules');if(m){m.scale.setScalar(t.moduleScale);m.visible=a<=.5;}}
 return a;
}
