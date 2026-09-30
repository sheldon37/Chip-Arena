import * as T from 'three';
import type {PlayerView} from '../shared/simulation.js';
import {WEAPONS} from '../shared/content.js';
import {animateCharacter,type AnimatedRig} from './character-animation.js';
import {springStep,clockStep,reloadPhase} from './ranged-motion.js';
/** First-person view model: animates the camera-attached rig, then places the view root (the renderer hides the rig's head,
 * legs and torso meshes once at construction and marks it with markFirstPersonRig).
 * Presentation only, per view root: the torso's big yaw/translation is damped so strikes and casts stay on screen, and a
 * subtle view layer adds stride bob, a look-sway spring from aim deltas, a landing kick, a recoil push and a reload dip.
 * Springs start at rest, clamp dt to .05 and reset on backward time or jumps over .35 s. */
/** View-model placement per class (exported for the numeric probes; presentation only). */
export const FP_TUNE={meleeX:.16,meleeY:-.22,meleeZ:-.3,magicX:.3,magicY:-.48};
interface ViewState{t:number;yaw:number;pitch:number;sx:number;sxv:number;sy:number;syv:number;roll:number;rollv:number;land:number;landv:number;grounded:boolean;vy:number}
const states=new WeakMap<object,ViewState>();
const _o:[number,number]=[0,0];
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
export function poseFirstPersonView(firstRig:AnimatedRig,viewRoot:T.Object3D,self:PlayerView,time:number,dt:number,scoped:boolean){
 animateCharacter(firstRig,self,time,dt,0);
 const fighter=self.spec==='ansatsuken'||self.spec==='emperor',w=WEAPONS[self.spec],cls=w?.class;
 if(!fighter)settleViewTorso(firstRig);
 let s=states.get(viewRoot);const clock=clockStep(s,time),h=clock.dt;
 if(!s||clock.fresh){s={t:time,yaw:self.yaw,pitch:self.pitch,sx:0,sxv:0,sy:0,syv:0,roll:0,rollv:0,land:0,landv:0,grounded:self.grounded,vy:self.vel.y};states.set(viewRoot,s);}
 // Look sway: the weapon lags a turn slightly, then springs back (a few centimetres at most).
 const dyaw=wrap(self.yaw-s.yaw),dpitch=self.pitch-s.pitch,rate=h>0?1/h:0;
 const tx=Math.max(-.03,Math.min(.03,-dyaw*rate*.004)),ty=Math.max(-.025,Math.min(.025,-dpitch*rate*.004)),tr=Math.max(-.05,Math.min(.05,dyaw*rate*.008));
 if(h>0){springStep(s.sx,s.sxv,tx,14,.8,h,_o);s.sx=_o[0];s.sxv=_o[1];springStep(s.sy,s.syv,ty,14,.8,h,_o);s.sy=_o[0];s.syv=_o[1];springStep(s.roll,s.rollv,tr,14,.8,h,_o);s.roll=_o[0];s.rollv=_o[1];
  // Landing: the grounded edge kicks the view model down in proportion to the fall speed, then it springs back.
  if(self.grounded&&!s.grounded)s.landv-=Math.min(1,Math.max(0,-s.vy)/9)*.9;
  springStep(s.land,s.landv,0,16,.55,h,_o);s.land=_o[0];s.landv=_o[1];}
 s.t=time;s.yaw=self.yaw;s.pitch=self.pitch;s.grounded=self.grounded;if(!self.grounded)s.vy=self.vel.y;
 // Stride bob from the rig's own stride phase, scaled by how much it is running (subtle).
 const run=Math.min(1,firstRig.motion),bx=Math.sin(firstRig.stride)*.012*run,by=-Math.abs(Math.cos(firstRig.stride))*.015*run;
 // Recoil push and reload dip for guns.
 const shotAge=time-(self.lastShot??self.lastAttack),kick=cls==='ranged'&&shotAge>=0?Math.exp(-shotAge*(self.spec==='rifle'?14:20))*(self.spec==='rifle'?1.4:self.spec==='machinegun'?.6:1):0;
 const reload=cls==='ranged'?reloadPhase(self,w).envelope:0,runLow=cls==='ranged'?run*Math.min(1,Math.max(0,(shotAge-.3)/.3))*(1-reload):0;
 // Per class: guns sit right of centre (the renderer's original placement); the staff hand moves right so its crystal no
 // longer fills the left of the view; melee is raised a little so strikes read.
 const t=FP_TUNE,baseX=fighter?0:cls==='magic'?t.magicX:cls==='melee'?t.meleeX:.24,baseY=fighter?-.48:cls==='melee'?t.meleeY:cls==='magic'?t.magicY:-.48,baseZ=cls==='melee'&&!fighter?t.meleeZ:-.2;
 viewRoot.position.set(baseX+bx+s.sx,baseY+by+s.sy+s.land*.05-.06*reload-.03*runLow,baseZ+.04*kick);
 viewRoot.rotation.set(.06*kick-.1*runLow,Math.PI,s.roll+.25*reload);viewRoot.scale.setScalar(.78);viewRoot.visible=!scoped;
}
