import * as T from 'three';
import {WEAPON_INTROS,type WeaponIntro} from '../shared/weapon-intros.js';
import {introCurve,heroIdle,smooth,easeOutLive,type IntroCurveOptions} from './intro-curves.js';
import {braceWeapon,type CharacterRig} from './character-rig.js';
import {hand,foot,STANDING_REACH} from './rig-ik.js';
type Spec=typeof WEAPON_INTROS[WeaponIntro]['spec'];
/** Nine body keys per intro, timed to the weapon's own hit: start, settle-in, set, anticipation (hit-.35), contact (hit),
 * follow-through (hit+.32), recovery (hit+.85), hero (nameAt 3.2) and end. For a 1.8 s hit this is the original
 * [0,.5,1.05,1.45,1.8,2.12,2.65,3.2,4.8]; earlier hits compress the opening, later hits hold it. */
export const keysFor=(hit:number)=>{const s=Math.min(1,(hit-.35)/1.45);return [0,.5*s,1.05*s,hit-.35,hit,hit+.32,hit+.85,3.2,4.8];};
/** A planned foot step in world space: the foot lifts at `lift` and lands at `land` on (x,z) with heading `yaw`; `h` is the
 * lift height. */
type Step={left:boolean;lift:number;land:number;x:number;z:number;yaw:number;h:number};
/** Local stance: left foot (x,z,yaw) then right foot (x,z,yaw), in the root's frame. */
type Stance=[number,number,number,number,number,number];
/** A beat the footwork must hit: the foot lifts at `at` and lands `dur` later on the stance ideal for that moment. */
type Beat={left:boolean;at:number;dur:number;h:number};
interface Choreo{turn:number[];z:number[];crouch:number[];torque:number[];lean:number[];side:number;impact?:number[];
 /** Ideal stance over time (hit-relative windows): the planner steps whenever a planted foot drifts too far from it. */
 stance:(t:number,hit:number)=>Stance;beats?:(hit:number)=>Beat[];hop?:[number,number]}
const L=true,R=false;
const SQUARED:Stance=[.16,.06,.12,-.16,-.1,-.22],READY:Stance=[.17,.1,.18,-.17,-.12,-.3];
const phase=(t:number,...windows:[number,Stance][])=>{let s=windows[0][1];for(const [at,stance] of windows)if(t>=at)s=stance;return s;};
const TORQUE=[-.05,-.18,-.30,-.32,.35,.22,.08,-.05,-.05],LEAN=[.08,.10,.15,-.13,.35,.2,.04,0,0];
const caster=(t:number,h:number)=>phase(t,[0,SQUARED],[h-.4,[.19,.18,.2,-.16,-.12,-.3]],[h+.6,SQUARED]);
const CHOREO:Record<Spec,Choreo>={
 sword:{side:-1,turn:[-.9,-.8,-.7,-.95,.55,.8,.15,-.25,-.25],z:[-.95,-.62,-.44,-.42,-.1,-.06,-.02,0,0],crouch:[-.03,-.04,-.10,-.2,-.26,-.12,-.04,-.03,-.03],torque:TORQUE,lean:LEAN,
  stance:(t,h)=>phase(t,[0,READY],[h-.4,[.2,.32,.2,-.17,-.22,-.4]],[h+.5,SQUARED]),beats:h=>[{left:L,at:h-.33,dur:.27,h:.11}]},
 greatsword:{side:1,turn:[1.1,.85,.35,-.3,.5,.65,.25,-.25,-.25],z:[-.8,-.45,-.15,-.12,.08,.1,.04,0,0],crouch:[-.09,-.12,-.18,-.02,-.36,-.2,-.05,-.03,-.03],torque:TORQUE,lean:LEAN,impact:[4],
  stance:(t,h)=>phase(t,[0,READY],[h-.4,[.26,.24,.3,-.2,-.14,-.3]],[h+.6,SQUARED]),beats:h=>[{left:L,at:h-.32,dur:.24,h:.1}]},
 daggers:{side:1,turn:[-.3,-1.2,-2.8,-4.1,-5.3,-6.1,-6.5,-6.5,-6.5],z:[-.2,-.1,0,0,.1,.1,0,0,0],crouch:[-.28,-.2,.02,.14,-.17,-.32,-.12,-.05,-.05],torque:TORQUE,lean:LEAN,
  stance:()=>READY,hop:[1.04,1.44]},
 shield:{side:1,turn:[.5,.25,.1,.05,-.15,-.4,-.25,-.25,-.25],z:[-.2,-.1,0,0,.1,.1,0,0,0],crouch:[-.34,-.38,-.2,-.3,-.18,-.05,-.03,-.03,-.03],torque:TORQUE,lean:LEAN,impact:[4],
  stance:(t,h)=>phase(t,[0,[.18,.08,.2,-.18,-.12,-.25]],[h-.35,[.22,.18,.25,-.18,-.14,-.3]],[h+.6,SQUARED]),beats:h=>[{left:L,at:h-.3,dur:.28,h:.09}]},
 pistol:{side:1,turn:[-.7,-.4,-.25,-.1,.2,.1,-.1,-.2,-.2],z:[-.2,-.1,0,0,.1,.1,0,0,0],crouch:[-.03,-.04,-.10,-.15,-.13,-.06,-.03,-.03,-.03],torque:TORQUE,lean:LEAN,stance:caster},
 rifle:{side:-1,turn:[-.7,-.4,-.25,-.1,.2,.1,-.1,-.2,-.2],z:[-.2,-.1,0,0,.1,.1,0,0,0],crouch:[-.25,-.30,-.34,-.25,-.08,-.03,-.03,-.03,-.03],torque:TORQUE,lean:LEAN,stance:(t,h)=>phase(t,[0,[.17,.12,.2,-.17,-.14,-.3]],[h-.4,[.2,.2,.25,-.17,-.16,-.35]],[h+.6,SQUARED])},
 machinegun:{side:1,turn:[-.7,-.4,-.25,-.1,.2,.1,-.1,-.2,-.2],z:[-.2,-.1,0,0,.1,.1,0,0,0],crouch:[-.03,-.04,-.10,-.18,-.2,-.1,-.03,-.03,-.03],torque:TORQUE,lean:LEAN,stance:caster},
 fire:{side:1,turn:[.5,.25,-.15,-.4,.35,.15,-.2,-.2,-.2],z:[-.2,-.1,0,0,.1,.1,0,0,0],crouch:[-.03,-.04,-.10,-.15,-.13,-.06,-.03,-.03,-.03],torque:TORQUE,lean:LEAN,stance:caster},
 water:{side:1,turn:[.5,.25,-.15,-.4,.35,.15,-.2,-.2,-.2],z:[-.2,-.1,0,0,.1,.1,0,0,0],crouch:[-.03,-.04,-.10,-.15,-.13,-.06,-.03,-.03,-.03],torque:TORQUE,lean:LEAN,stance:caster},
 darkness:{side:1,turn:[.5,.25,-.15,-.4,.35,.15,-.2,-.2,-.2],z:[-.2,-.1,0,0,.1,.1,0,0,0],crouch:[-.03,-.04,-.10,-.15,-.13,-.06,-.03,-.03,-.03],torque:TORQUE,lean:LEAN,stance:caster},
 lightning:{side:1,turn:[.5,.25,-.15,-.4,.35,.15,-.2,-.2,-.2],z:[-.7,-.7,-.55,-.25,0,0,0,0,0],crouch:[0,0,0,0,-.04,-.1,-.04,-.03,-.03],torque:TORQUE,lean:LEAN,
  stance:(t,h)=>phase(t,[0,[.18,.1,.2,-.18,-.1,-.25]],[h+.6,SQUARED])},
};
const plans=new Map<Spec,Step[]>();
/** Deterministic footstep plan for a spec (a pure function of its choreography, memoised): walking the timeline at 60 Hz,
 * a planted foot steps when its world anchor has drifted more than 9 cm or .45 rad from the ideal stance, aiming where
 * that stance will be when it lands. One foot swings at a time (except an authored hop); beats force exact timings. */
function planSteps(spec:Spec,hit:number):Step[]{
 const cached=plans.get(spec);if(cached)return cached;
 const ch=CHOREO[spec],keys=keysFor(hit),flow:IntroCurveOptions={pass:[1,2,7]},beats=ch.beats?.(hit)??[],steps:Step[]=[];
 const ideal=(time:number,left:boolean)=>{const z=introCurve(keys,ch.z,time,flow),yaw=introCurve(keys,ch.turn,time,flow),s=ch.stance(time,hit),o=left?0:3,cs=Math.cos(yaw),sn=Math.sin(yaw);return {x:s[o]*cs+s[o+1]*sn,z:z-s[o]*sn+s[o+1]*cs,yaw:yaw+s[o+2]};};
 const feet=[true,false].map(left=>({left,at:ideal(0,left),busy:-1,landed:-1}));
 const go=(f:typeof feet[0],t:number,dur:number,h:number)=>{const to=ideal(t+dur,f.left);steps.push({left:f.left,lift:t,land:t+dur,x:to.x,z:to.z,yaw:to.yaw,h});f.at=to;f.busy=f.landed=t+dur;};
 for(let n=0;n<=Math.round(4.6*60);n++){const t=n/60;
  if(ch.hop&&Math.abs(t-ch.hop[0])<1e-6){for(const f of feet)go(f,t,ch.hop[1]-ch.hop[0],.1);continue;}
  for(const b of beats){const f=feet[b.left?0:1];if(Math.abs(t-b.at)<.5/60&&f.busy<=t)go(f,t,b.dur,b.h);}
  if(feet.some(f=>f.busy>t))continue;
  const errors=feet.map(f=>{const want=ideal(t+.28,f.left),d=Math.hypot(want.x-f.at.x,want.z-f.at.z),a=Math.abs(Math.atan2(Math.sin(want.yaw-f.at.yaw),Math.cos(want.yaw-f.at.yaw)));
   const soon=beats.some(b=>b.left===f.left&&b.at>t&&b.at<t+.3)||(ch.hop&&t<ch.hop[0]&&t>ch.hop[0]-.3);return soon||t-f.landed<.15?0:Math.max(d/.09,a/.45);});
  const i=errors[0]>=errors[1]?0:1;if(errors[i]>1)go(feet[i],t,T.MathUtils.clamp(.2+errors[i]*.03,.24,.34),.07);}
 plans.set(spec,steps);return steps;
}
const ROOT_Y=.88,FLOOR=-.795,LEG_REACH=.485;
const _hip=new T.Vector3(),_t=new T.Vector3();
interface Foot{x:number;z:number;y:number;yaw:number;heel:number;weight:number}
/** Residual toe-up after a heel strike at `landed`, flattening over .14 s. */
const strike=(t:number,landed:number)=>landed<0||t>=landed+.14?0:-.22*(1-smooth((t-landed)/.14));
/** World-planted feet: a planted foot keeps its world anchor while the root moves and turns; a stepping foot travels on
 * a lift arc with toe-off and heel-strike roll. Pure function of time (anchors come from the memoised plan). */
function footAt(left:boolean,t:number,start:{x:number;z:number;yaw:number},steps:Step[]):Foot{
 let anchor=start,heel=0,lastLand=-1;
 for(const step of steps){if(step.left!==left)continue;
  if(t>=step.land){anchor=step;lastLand=step.land;continue;}
  if(t>=step.lift){const u=(t-step.lift)/(step.land-step.lift),e=smooth(u),turn=Math.atan2(Math.sin(step.yaw-anchor.yaw),Math.cos(step.yaw-anchor.yaw)),from=.3+strike(step.lift,lastLand);
   return {x:anchor.x+(step.x-anchor.x)*e,z:anchor.z+(step.z-anchor.z)*e,yaw:anchor.yaw+turn*e,y:4*step.h*u*(1-u),heel:from+(-.22-from)*smooth(u*1.15),weight:u<.5?1-smooth(u/.3):smooth((u-.7)/.3)};}
  if(t>step.lift-.1)heel=.3*smooth((t-step.lift+.1)/.1);
  break;}
 heel+=strike(t,lastLand);
 return {x:anchor.x,z:anchor.z,yaw:anchor.yaw,y:0,heel,weight:1};
}
/** Lightning falls from above frame: hovering high, then a gravity-accelerated drop that lands exactly on the hit. */
export function lightningHeight(age:number,hit:number){const fall=hit-.35,drop=2.2,speed=2*drop/(hit-fall);if(age>=hit){const tau=age-hit,w=speed/(.4*Math.E);return -speed*tau*Math.exp(-w*tau);}if(age<=fall)return drop+.3*(1-smooth(age/fall))+.06*Math.sin(age*2.3)*(1-smooth(age/fall));const u=(age-fall)/(hit-fall);return drop*(1-u*u);}
/** Authored full-body entrance phrases. Absolute sampling makes previews and late joins identical. */
export function poseWeaponIntro(r:CharacterRig,style:WeaponIntro,age:number){
 const spec=WEAPON_INTROS[style].spec,hit=WEAPON_INTROS[style].hit,keys=keysFor(hit),t=Math.max(0,age),ch=CHOREO[spec],magic=['fire','water','lightning','darkness'].includes(spec),gun=['pistol','rifle','machinegun'].includes(spec);
 const flow:IntroCurveOptions={pass:[1,2,7]},impact:IntroCurveOptions={impact:ch.impact,pass:[1,2,7]},strike:IntroCurveOptions={impact:ch.impact,pass:[1,2,4,7]};
 /** Keyed channel sampled `lag` seconds late (positive: trails the action; negative: leads it). */
 const c=(v:number[],lag=0,o:IntroCurveOptions=flow)=>introCurve(keys,v,t-lag,o);
 for(const node of [r.body,r.pelvis,r.spine,r.chest,r.neck,r.head,r.leftShoulder,r.rightShoulder,r.leftArm,r.rightArm,r.leftElbow,r.rightElbow,r.leftHand,r.rightHand,r.leftLeg,r.rightLeg,r.leftKnee,r.rightKnee,r.leftFoot,r.rightFoot,r.leftToe,r.rightToe])node.rotation.set(0,0,0);
 r.group.scale.setScalar(1);r.body.position.set(0,0,0);r.weapon.visible=true;r.offWeapon.visible=spec==='daggers';r.weapon.position.set(0,0,0);r.weapon.rotation.set(0,0,0);r.offWeapon.position.set(0,0,0);r.offWeapon.rotation.set(0,0,0);r.weapon.scale.setScalar(1);r.offWeapon.scale.setScalar(1);
 const root=(time:number)=>({z:introCurve(keys,ch.z,time,flow),yaw:introCurve(keys,ch.turn,time,flow)}),g=root(t);
 r.group.position.set(0,ROOT_Y,g.z);r.group.rotation.set(0,g.yaw,0);
 // Kinetic chain: hips lead, spine and chest follow, the striking hand lands on the hit, the off hand and head trail.
 const hop=ch.hop&&t>ch.hop[0]&&t<ch.hop[1]?(t-ch.hop[0])/(ch.hop[1]-ch.hop[0]):0,air=spec==='lightning'?lightningHeight(t,hit):hop?.13*Math.sin(Math.PI*hop):0;
 r.body.position.y=c(ch.crouch,0,impact)+air;
 const torque=(lag:number)=>c(ch.torque,lag);r.pelvis.rotation.y=torque(-.07)*.4;r.spine.rotation.y=torque(-.05)*.6;r.chest.rotation.y=torque(-.03)*.45;r.head.rotation.y=-torque(.06)*.7;
 r.body.rotation.x=c(ch.lean,-.02,impact);r.head.rotation.x=-c(ch.lean,.06)*.5;r.spine.rotation.x=Math.sin(t*2.2)*.008;
 const idle=heroIdle(t);r.spine.rotation.x+=idle.breath;r.neck.rotation.x-=idle.breath*.5;r.head.rotation.z+=idle.headRoll;r.head.rotation.y+=idle.headYaw;
 // Feet: world anchors, weight shift toward the support foot, then drop the pelvis just enough that planted legs reach.
 const steps=planSteps(spec,hit),start=(left:boolean)=>{const s=ch.stance(0,hit),o=left?0:3,z0=root(0),c0=Math.cos(z0.yaw),s0=Math.sin(z0.yaw);return {x:s[o]*c0+s[o+1]*s0,z:z0.z-s[o]*s0+s[o+1]*c0,yaw:z0.yaw+s[o+2]};},feet=[footAt(true,t,start(true),steps),footAt(false,t,start(false),steps)],cs=Math.cos(g.yaw),sn=Math.sin(g.yaw);
 const local=feet.map(f=>{const dx=f.x,dz=f.z-g.z;return new T.Vector3(dx*cs-dz*sn,FLOOR+f.y+(f.heel>0?.08*Math.sin(f.heel):.05*Math.sin(-f.heel)),dx*sn+dz*cs);});
 if(air>0)for(const p of local)p.y+=Math.max(0,air-.1);
 const total=feet[0].weight+feet[1].weight,support=(local[0].x*feet[0].weight+local[1].x*feet[1].weight)/Math.max(.01,total);
 const shift=T.MathUtils.clamp(support*.4,-.07,.07)+idle.shift;r.body.position.x=shift;r.pelvis.rotation.z=shift*.7+idle.roll;r.chest.rotation.z=-(shift*.7+idle.roll)*1.15;
 r.body.updateMatrix();r.pelvis.updateMatrix();let deficit=-1;
 feet.forEach((f,i)=>{const leg=i===0?r.leftLeg:r.rightLeg;_hip.copy(leg.position).applyMatrix4(r.pelvis.matrix).applyMatrix4(r.body.matrix);const p=local[i],dh=Math.hypot(p.x-_hip.x,p.z-_hip.z),allowed=Math.sqrt(Math.max(0,(LEG_REACH-.005)**2-dh*dh));deficit=Math.max(deficit,f.weight*(_hip.y-(p.y+allowed)));});
 const drop=Math.min(.32,(deficit+Math.sqrt(deficit*deficit+.0009))/2);r.body.position.y-=drop;
 feet.forEach((f,i)=>foot(r,i===0,local[i],f.yaw-g.yaw,f.heel,1,undefined,STANDING_REACH));
 const h=(left:boolean,x:number,y:number,z:number,rx=0,ry=0,rz=0,w=1)=>hand(r,left,_t.set(x,y,z),new T.Euler(rx,ry,rz),w);
 const off=.05,sway=idle.sway;
 if(spec==='greatsword'){
  braceWeapon(r,new T.Vector3(c([-.28,-.30,-.12,-.14,-.06,.0,-.18,-.22,-.22],0,strike),c([-.30,-.28,.04,.40,-.23,-.04,.32,.3,.3],0,strike)+sway,c([-.05,.0,.2,-.16,.43,.32,.05,.1,.1],0,strike)),new T.Euler(c([.85,.7,-.1,-2.4,.8,.1,-.25,-.28,-.28],0,strike),c([.2,.25,-.25,-.3,.45,.7,2.6,2.8,2.8]),-.1),1);
 }else if(spec==='sword'){
  // Hand to hilt on the walk-in, a crouched draw, the cut through the hit, a flourish, then the blade laid on the shoulder.
  h(false,c([-.22,-.05,.10,.16,-.27,-.45,-.34,-.18,-.18],0,strike),c([-.26,-.22,-.2,-.17,.13,.2,.18,.02,.02],0,strike)+sway,c([.06,.1,.13,.13,.42,.24,.26,.16,.16],0,strike),c([.3,.1,-.1,-.2,.15,.25,-.4,-2.55,-2.55],0,strike),c([0,-.6,-1.4,-1.6,.8,1.5,.6,-.35,-.35],0,strike));
  h(true,c([.24,.22,.16,.13,.3,.28,.22,.24,.24],off),c([-.2,-.2,-.19,-.17,-.02,-.08,-.14,-.18,-.18],off),c([.08,.1,.13,.14,-.04,.04,.1,.1,.1],off),c([0,0,0,.1,-.3,-.2,0,0,0],off),0,c([.3,.3,.3,.2,.6,.5,.35,.3,.3],off));
 }else if(spec==='daggers'){
  const twirl=c([0,0,Math.PI,Math.PI*2,Math.PI*2,Math.PI*2,Math.PI*2,Math.PI*2,Math.PI*2]);
  for(const left of [true,false]){const sign=left?1:-1,lag=left?off:0;h(left,sign*c([.22,.32,.45,.43,.24,.10,.08,.12,.12],lag),c([-.12,.08,.28,.42,.28,.12,.2,.16,.16],lag)+sway,c([-.12,-.02,.05,.15,.25,.33,.25,.28,.28],lag),twirl+c([.2,-.3,-.9,-1.2,-.6,0,-.2,-.2,-.2],lag),sign*c([.4,.8,1.1,.8,.1,-.65,-.65,-.65,-.65],lag),sign*.18);}
 }else if(spec==='shield'){
  h(false,-.22,c([-.27,-.28,.10,-.15,.06,.20,.15,.15,.15],0,impact)+sway,c([.32,.34,.18,.47,.42,.30,.27,.30,.30]),c([.15,.2,.1,.02,-.05,-.1,-.08,-.08,-.08]),c([.15,.1,-.3,-.1,.2,.4,.25,.25,.25]));h(true,.37,c([-.18,-.22,-.05,.05,.12,.13,.1,.05,.05],off),.02,-.5,0,.35);
 }else if(gun){
  if(spec==='pistol'){
   h(false,c([-.28,-.30,-.18,-.14,-.18,-.25,-.24,-.24,-.24]),c([-.1,.1,.4,.38,.15,.1,.12,.12,.12])+sway,c([.12,.18,.12,.1,.25,.43,.34,.34,.34]),c([.3,-.35,-.9,-.7,-.15,0,-.1,-.1,-.1]),0,c([0,0,Math.PI,Math.PI*2,Math.PI*2,Math.PI*2,Math.PI*2,Math.PI*2,Math.PI*2]));h(true,.25,-.12,.08,-.2,0,.15);
  }else{
   const heavy=spec==='machinegun';braceWeapon(r,new T.Vector3(c([-.15,-.14,-.10,-.1,-.03,-.02,-.04,-.06,-.06]),c(heavy?[.37,.38,.24,.18,.14,.12,.14,.15,.15]:[-.08,-.06,.12,.14,.14,.13,.14,.15,.15])+sway*.5,c([.08,.12,.22,.24,.28,.27,.25,.25,.25])),new T.Euler(c(heavy?[-1,-.9,-.5,-.25,-.3,-.4,-.15,-.12,-.12]:[.2,.12,.04,-.05,-.1,-.15,-.10,-.10,-.10]),.1,0),1);
   if(spec==='rifle'){const w=1-smooth((t-1.55)/.25);if(w>0)h(true,c([.3,.28,-.05,-.12,.1,.1,.1,.1,.1],off),.07,c([.16,.2,.04,-.08,.12,.12,.12,.12,.12],off),-.3,0,.2,w);}
  }
 }else if(magic){
  h(false,-.42,c([.05,.12,.3,.45,.15,.13,.15,.14,.14],off),.12,-Math.PI/2,.12,0);
  const y=spec==='fire'?c([-.1,.02,.15,.35,.5,.35,.24,.2,.2]):spec==='water'?c([-.1,.12,.34,.25,.02,.25,.3,.22,.22]):spec==='lightning'?c([.1,.35,.5,-.24,.06,.2,.32,.25,.25]):c([-.3,-.13,.08,.3,.38,.30,.20,.17,.17]);
  h(true,c([.2,.3,.39,.4,.3,.28,.28,.27,.27]),y+sway,c([.10,.14,.18,.18,.28,.30,.25,.25,.25]),-Math.PI/2,0,.3);r.leftHand.rotation.y+=Math.sin(t*1.8)*.1;
 }
 const flash=r.weapon.getObjectByName('muzzle-flash');if(flash)flash.visible=spec==='machinegun'&&t>1.93&&t<2.33&&Math.floor((t-1.93)*24)%2===0;
}
/** Which side of the root the camera favours for each weapon (the weapon arm faces the lens at the hit). */
export const introSide=(style:WeaponIntro)=>CHOREO[WEAPON_INTROS[style].spec].side;
/** Four authored shots: a weapon close-up, a travelling action shot, an opposite-side low impact shot from just before
 * the hit, and the hero shot under the name card. Moves ease out but are still travelling when the next shot cuts in. */
export function weaponIntroCamera(camera:T.PerspectiveCamera,style:WeaponIntro,age:number,root:T.Vector3,aspect:number,shake=false){
 const spec=WEAPON_INTROS[style].spec,hit=WEAPON_INTROS[style].hit,side=CHOREO[spec].side,closeEnd=Math.min(1.05,hit-.6),impactAt=hit-.15;
 const shot=age>=3.2?3:age>=impactAt?2:age>=closeEnd?1:0,from=[0,closeEnd,impactAt,3.2][shot],to=[closeEnd,impactAt,3.2,4.8][shot],u=easeOutLive((age-from)/(to-from));
 const lift=spec==='lightning'?T.MathUtils.clamp(lightningHeight(age,hit),0,1.6):0;let eye:T.Vector3,look:T.Vector3,fov:number;
 if(shot===0){eye=new T.Vector3(side*(1.05-u*.25),.45+lift*.6,2.3-u*.35);look=new T.Vector3(side*.12,.3+lift,0);fov=44-u*3;}
 else if(shot===1){eye=new T.Vector3(side*(1.9-u*.4),.35+lift*.5,3.3-u*.2);look=new T.Vector3(0,.15+lift*.8,0);fov=42;}
 else if(shot===2){const push=u*.25;eye=new T.Vector3(-side*(1.75-push*.6),-.08,3.1-push);look=new T.Vector3(0,.02,0);fov=40;}
 else{eye=new T.Vector3(side*(1.15-u*.45),.2,3.9);look=new T.Vector3(0,.1,0);fov=40;}
 if(aspect<1.35)eye.multiplyScalar(1.35/Math.max(.65,aspect));
 const target=root.clone().add(look);camera.position.copy(root).add(eye);
 if(shake&&(spec==='greatsword'||spec==='shield'||spec==='lightning')&&age>hit){const a=age-hit,k=.035*Math.exp(-12*a);camera.position.x+=k*Math.sin(53*a);camera.position.y+=k*Math.sin(71*a);}
 camera.up.set(0,1,0);camera.lookAt(target);camera.fov=fov;camera.aspect=aspect;camera.updateProjectionMatrix();return {shot,target};
}
