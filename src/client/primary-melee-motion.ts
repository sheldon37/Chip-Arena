import * as T from 'three';
import {braceWeapon,type CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import type {attackPose} from './character-animation.js';
import {poseCurve} from './pose-curves.js';
import {attackCommitment,attackTravel,meleeRecovery} from '../shared/souls-combat.js';
import {smoothMotion as ease,ankleInGroup} from './motion-quality.js';
import {stanceFor} from './stances.js';
import {meleeGuard,type GuardHand} from './melee-guard.js';
import {foot,hand} from './rig-ik.js';
export {foot,hand};
/** Phrase keys: 0..1 windup (swingAt at 1), 1..2 the active window (swingUntil at 2), 2..3 recovery (commitment end at 3).
 * 2.3 is the follow-through extreme: the blade still carries momentum past swingUntil and eases out instead of stopping
 * dead on the hit window's last frame; 2.65 is the return, which comes back low and close instead of retracing the cut. */
const keys=[0,.55,1,1.28,1.65,2,2.3,2.65,3];
/** The fourth dagger cut's body turn: a full forward turn over .8..3 that never unwinds. */
const SPIN_TURN=[0,-.35,-.6,.9,2.4,3.9,5.0,5.9,Math.PI*2],SPIN_FROM=poseCurve(keys,SPIN_TURN,1.1);
type Arr=number[];
/** Phase of an attack from authoritative timestamps. The recovery's phase starts at the strike's phase speed (so the
 * follow-through keeps the blade's momentum) and settles with zero speed at the commitment end. */
export function meleePhase(a:Pick<ReturnType<typeof attackPose>,'age'|'windup'|'window'>,recoverySeconds:number,age=a.age){
 if(age<a.windup)return Math.max(0,age)/a.windup;
 if(age<a.windup+a.window)return 1+(age-a.windup)/a.window;
 const u=Math.min(1,(age-a.windup-a.window)/Math.max(.01,recoverySeconds)),m=Math.min(2.1,recoverySeconds/Math.max(.01,a.window));
 return 2+u*(m+u*((3-2*m)+u*(m-2)));
}
/** A guard-anchored curve: the first and last keys are the guard value, the middle seven are the phrase. */
const w=(start:number,mid:Arr)=>[start,...mid,start];
/** Reference phrases: paired dagger slashes, low heavy sweeps, a stepping sword combo, a hip-driven shield slam. The active
 * sweep stays 1..2; anticipation, follow-through and recovery carry the body's weight around it, and every phrase starts and
 * ends in the weapon's combat-ready guard (melee-guard.ts), so a held combo flows through one stance. */
export function primaryMeleeMotion(r:CharacterRig,p:PlayerView,a:ReturnType<typeof attackPose>){
 const rate=p.attackRate||1,recoverySeconds=meleeRecovery(p.spec)/rate,phase=meleePhase(a,recoverySeconds),c=(values:Arr)=>poseCurve(keys,values,phase),weight=ease(a.age/Math.min(.065,a.windup));
 const index=Math.max(0,(p.attackIndex||1)-1),dagger=p.spec==='daggers',heavy=p.spec==='greatsword',shield=p.spec==='shield',reverse=dagger||heavy?index%2===1:!shield&&index%3===1,sign=reverse?-1:1,overhead=!heavy&&!dagger&&!shield&&index%3===2,spin=dagger&&index%4===3,low=heavy||dagger&&index%4===2;
 const G=meleeGuard(p.spec)!;
 // Where the incoming layers planted the ankles, before this phrase moves the body (the plant blends from there).
 if(weight<1){ankleInGroup(r,true,_in[0]);ankleInGroup(r,false,_in[1]);}
 // Whole-body turn (hips lead) and torso twist on top of it (shoulders trail the hips, most for the greatsword).
 const turnArr=spin?SPIN_TURN:heavy?[0,-.42,-1.05,-.90,.45,.95,1.32,1.12,0]:overhead?[0,-.26,-.40,-.15,.14,.30,.42,.20,0]:shield?[0,-.36,-.62,-.24,.45,.66,.95,.40,0]:dagger?[0,-.25,-.45,-.15,.20,.40,.46,.34,0]:[0,-.45,-.85,-.35,.30,.62,1.02,.55,0];
 const twistArr=heavy?[0,-.22,-.48,-.62,-.45,.40,.52,.35,0]:dagger?[0,-.10,-.20,-.16,.06,.22,.26,.18,0]:[0,-.18,-.38,-.20,.24,.40,.44,.16,0];
 const turnScale=spin?1:sign,twistScale=(overhead?.5:1)*sign;
 const turn=c(turnArr)*turnScale,twist=c(twistArr)*twistScale;
 const crouch=c(heavy?[0,-.12,-.30,-.37,-.33,-.18,-.10,-.06,0]:overhead?[0,-.12,.06,0,-.24,-.30,-.28,-.14,0]:shield?[0,-.05,-.12,-.13,-.22,-.25,-.20,-.09,0]:low?[0,-.19,-.28,-.25,-.34,-.30,-.22,-.10,0]:[0,-.10,-.16,-.13,-.22,-.26,-.21,-.09,0]);
 r.body.position.x+=c(heavy?[0,-.05,-.10,-.08,.07,.11,.10,.06,0]:[0,-.035,-.07,-.02,.065,.09,.08,.035,0])*sign;
 r.body.position.z+=c(heavy?[0,-.06,-.12,-.09,.13,.18,.15,.08,0]:[0,-.05,-.08,0,.14,.17,.15,.07,0]);r.body.position.y+=crouch;
 r.body.rotation.y+=turn;r.body.rotation.x+=c(heavy?[0,-.09,.22,.30,.26,0,-.08,-.06,0]:overhead?[0,-.15,-.26,-.14,.34,.40,.34,.16,0]:[0,-.07,-.15,.04,.20,.26,.22,.10,0]);
 r.body.rotation.z+=c(heavy?[0,-.10,-.20,-.24,.16,.22,.18,.08,0]:[0,-.05,-.11,-.06,.12,.16,.13,.05,0])*sign;
 r.pelvis.rotation.y+=twist*.5;r.spine.rotation.y+=twist*.65;r.chest.rotation.y+=twist*.55;
 r.spine.rotation.x+=c(heavy?[0,-.10,-.16,-.04,.14,.18,.10,.04,0]:overhead?[0,-.03,-.12,-.08,.10,.18,.14,.05,0]:[0,-.025,-.08,-.03,.09,.13,.10,.04,0]);
 r.chest.rotation.z+=c([0,-.04,-.10,-.06,.07,.11,.09,.04,0])*sign;
 // Anti-collapse: a forward-pitched body counter-extends the spine and chest so the chest stays proud instead of curling.
 const pitch=r.body.rotation.x;if(pitch>.2){r.spine.rotation.x-=.45*(pitch-.2);r.chest.rotation.x-=.2*(pitch-.2);}
 r.leftShoulder.rotation.x+=c([0,.06,.13,.07,-.10,-.16,-.12,-.04,0]);r.rightShoulder.rotation.x-=c([0,.08,.18,.11,-.15,-.21,-.16,-.05,0]);
 r.leftShoulder.rotation.y+=twist*.35;r.rightShoulder.rotation.y-=twist*.35;
 feet(r,p,a,phase,sign,heavy,dagger,overhead,spin,weight);
 if(heavy){
  // Rear-leg load, stamped lead foot, low diagonal sweep, rise ~60 degrees over the lead shoulder, then a heavy drop
  // through the front into the two-hand low guard. The blade's yaw trails the hands (it accelerates last).
  const m=G.main,g=MELEE_PHRASES.greatsword;_t.set(c(w(m.x,g.x)),c(w(m.y,g.y)),c(w(m.z,g.z)));
  braceWeapon(r,_t,_eu.set(c(w(m.rx,g.rx)),c(w(m.ry,g.ry.map(v=>v*sign))),c(w(m.rz,g.rz.map(v=>v*sign)))),weight);
 }else if(dagger){
  // The striking hand alternates: the right-hand tables are mirrored for the left. Chambers stay clear of the shoulder
  // joint (a hand target passing through the shoulder is what flipped the elbow).
  arm(r,reverse,low?MELEE_PHRASES.daggerLow:MELEE_PHRASES.dagger,reverse?G.off:G.main,reverse,c,weight);
  arm(r,!reverse,MELEE_PHRASES.daggerOther,reverse?G.main:G.off,reverse,c,weight);
 }else{
  const phrase=shield?'shield':overhead?'swordOverhead':reverse?'swordBackhand':'swordForehand',set=MELEE_PHRASES[phrase];
  arm(r,false,set,G.main,false,c,weight);arm(r,true,shield?MELEE_PHRASES.shieldOff:reverse?MELEE_PHRASES.swordOffTucked:MELEE_PHRASES.swordOff,G.off,false,c,weight);
 }
 headTrack(r,p,a,recoverySeconds,turnArr,twistArr,turnScale,twistScale,crouch,spin,weight);
 if(p.chips.hurricane&&!spin)r.body.rotation.y+=ease(a.strike)*Math.PI*2; // A complete turn never unwinds during recovery.
}
/** One arm's phrase: the seven middle keys (.55..2.65) of the chest-space hand target (x is the rig's left) and the wrist's
 * chest-space Euler; the first and last keys are the guard's. Dagger tables are written for a right-hand strike and are
 * mirrored (x, yaw, roll and pole x negate) when the left hand strikes. `pole` keys the elbow's bend
 * direction on the same seven keys where the hand sweeps near the default pole's singular directions (the guard's ends
 * use the default pole, so the phrase leaves and rejoins the guard continuously). */
export type ArmPhrase={x:Arr;y:Arr;z:Arr;rx:Arr;ry:Arr;rz:Arr;pole?:[number,number,number][]};
const PR:[number,number,number]=[-1,-.65,-.3],PB:[number,number,number]=[-.3,-1,.35],PO:[number,number,number]=[-1,.1,.45],PD:[number,number,number]=[-.6,-1,.2],PL:[number,number,number]=[-.4,-.2,-1];
const mix=(a:[number,number,number],b:[number,number,number]):[number,number,number]=>{const x=a[0]+b[0],y=a[1]+b[1],z=a[2]+b[2],l=Math.hypot(x,y,z);return [x/l,y/l,z/l];};
/** Reference phrases (right-hand coordinates). Forehand: chamber out and back beside the shoulder, sweep across to the
 * left hip, return low in front (a figure 8, never retracing the cut). Backhand: chamber across at the left shoulder
 * (elbow down and forward), sweep out to the right. Overhead: raise behind the head (elbow out), chop, finish low in
 * front. Daggers: short arm-led slashes chambered beside the ribs, the other hand pulls back to the hip. */
export const MELEE_PHRASES={
 swordForehand:{x:[-.62,-.60,-.52,-.16,.04,.18,-.04],y:[.16,.28,.16,.02,-.08,-.18,-.22],z:[.10,-.16,.22,.44,.42,.28,.28],rx:[-.30,-.50,-.20,.16,.22,.20,.25],ry:[-.75,-1.10,-.55,.30,.68,1.10,.45],rz:[-.20,-.30,-.10,.20,.30,.34,.12]},
 swordBackhand:{x:[-.04,.08,-.06,-.28,-.42,-.52,-.36],y:[.16,.24,.14,.02,-.06,-.12,-.18],z:[.18,.10,.26,.44,.34,.20,.22],rx:[-.35,-.50,-.20,.16,.22,.18,.20],ry:[.75,1.15,.55,-.35,-.75,-1.10,-.45],rz:[.20,.30,.10,-.18,-.28,-.32,-.10],pole:[PB,PB,PB,mix(PB,PR),PR,PR,PR]},
 swordOverhead:{x:[-.24,-.18,-.16,-.10,-.06,-.04,-.12],y:[.30,.58,.46,.04,-.12,-.26,-.18],z:[.20,-.06,.18,.42,.42,.36,.30],rx:[-1.1,-2.05,-1.65,-.7,-.50,-.36,-.2],ry:[-.1,-.1,0,.05,.1,.08,.02],rz:[-.18,-.32,-.08,.25,.38,.30,.14],pole:[PO,PO,PO,PO,PO,PO,PO]},
 swordOff:{x:[.38,.42,.44,.30,.22,.24,.26],y:[-.05,-.06,-.02,.04,.10,.08,0],z:[-.02,.02,.06,.24,.26,.24,.20],rx:[-.4,-.4,-.35,-.3,-.35,-.5,-.75],ry:[.4,.6,.4,-.2,-.45,-.35,-.05],rz:[.3,.3,.3,.3,.3,.28,.26]},
 swordOffTucked:{x:[.34,.36,.36,.32,.26,.26,.26],y:[-.10,-.12,-.08,.02,.08,.04,-.02],z:[.04,.02,.04,.14,.22,.22,.18],rx:[-.4,-.4,-.35,-.3,-.35,-.5,-.75],ry:[.4,.6,.4,-.2,-.45,-.35,-.05],rz:[.3,.3,.3,.3,.3,.28,.26]},
 shield:{x:[-.14,-.06,-.20,-.40,-.48,-.52,-.32],y:[.06,.04,.09,.15,.14,.12,.08],z:[.22,.12,.24,.32,.30,.26,.24],rx:[.10,.18,.10,0,-.06,-.06,-.03],ry:[-.2,-.3,-.3,-.55,-.76,-1.02,-.5],rz:[-.18,-.25,-.1,.12,.18,.14,.06]},
 shieldOff:{x:[.36,.39,.38,.36,.34,.33,.32],y:[-.10,-.16,-.13,-.04,0,-.04,-.08],z:[.02,-.10,-.04,.04,.12,.12,.12],rx:[-.5,-.4,-.4,-.5,-.6,-.7,-.8],ry:[.1,0,0,.1,.2,.2,.2],rz:[.25,.25,.25,.25,.25,.25,.25]},
 dagger:{x:[-.50,-.58,-.36,-.10,0,.02,-.06],y:[.14,.16,.10,-.04,-.14,-.18,-.14],z:[.14,.02,.26,.44,.38,.30,.28],rx:[-.7,-.95,-.55,.2,.45,.50,.35],ry:[-.55,-.8,-.4,.40,.72,.84,.66],rz:[-.2,-.35,-.15,.2,.3,.26,.18],pole:[PD,PD,PD,PD,PD,PD,PD]},
 daggerLow:{x:[-.50,-.58,-.36,-.10,0,.02,-.06],y:[.02,-.06,-.10,-.14,.06,.14,.06],z:[.14,.02,.26,.44,.36,.28,.26],rx:[.1,.2,.12,-.15,-.2,-.25,-.2],ry:[-.55,-.8,-.4,.40,.72,.84,.66],rz:[-.2,-.35,-.15,.2,.3,.26,.18],pole:[PL,PL,PL,mix(PL,PR),PR,PR,PR]},
 daggerOther:{x:[.20,.24,.28,.34,.30,.24,.18],y:[.06,.10,0,-.12,-.04,.02,.06],z:[.30,.26,.22,.10,.16,.22,.26],rx:[-.3,-.2,-.3,-.55,-.75,-.55,-.4],ry:[.3,.55,.85,.7,.25,.15,.1],rz:[.2,.3,.35,.25,.05,.05,.1]},
 greatsword:{x:[-.02,0,.02,.04,.06,.05,.04],y:[.10,.02,-.16,-.06,.13,.31,.36],z:[.14,.14,.24,.32,.28,.22,.20],rx:[-.75,-.45,-.33,-.40,-.64,-.97,-1.05],ry:[-.25,-.15,.40,.75,.50,.05,-.15],rz:[-.2,-.3,-.2,-.1,.1,.18,.2]},
} satisfies Record<string,ArmPhrase>;
const _t=new T.Vector3(),_eu=new T.Euler(),_pole=new T.Vector3();
/** Pose one arm through a phrase from and back to its guard hand (`mirror` flips a right-hand table onto the left). */
function arm(r:CharacterRig,left:boolean,set:ArmPhrase,guard:GuardHand,mirror:boolean,c:(values:Arr)=>number,weight:number){
 const f=mirror?-1:1;_t.set(c(w(guard.x,set.x.map(v=>v*f))),c(w(guard.y,set.y)),c(w(guard.z,set.z)));_eu.set(c(w(guard.rx,set.rx)),c(w(guard.ry,set.ry.map(v=>v*f))),c(w(guard.rz,set.rz.map(v=>v*f))));
 let pole:T.Vector3|undefined;
 if(set.pole){const ends=[left?1:-1,-.65,-.3],k=(i:number)=>c(w(ends[i],set.pole!.map(v=>i===0?v[0]*f:v[i])));_pole.set(k(0),k(1),k(2));pole=_pole;}
 hand(r,left,_t,_eu,weight,pole);
}
const _q=new T.Quaternion(),_h=new T.Quaternion(),_e=new T.Euler(),_f=new T.Vector3();
/** Gaze stabilisation in the rig group's frame: the head follows a fraction of the lagged body turn (it trails the chest by
 * ~40 ms; a spin spots ahead instead), pitches with the aim and never rolls, however the torso under it pitches and rolls. */
function headTrack(r:CharacterRig,p:PlayerView,a:ReturnType<typeof attackPose>,recoverySeconds:number,turnArr:Arr,twistArr:Arr,turnScale:number,twistScale:number,crouch:number,spin:boolean,weight:number){
 const lagged=meleePhase(a,recoverySeconds,a.age+(spin?.04:-.04)),turn=poseCurve(keys,turnArr,lagged)*turnScale,twist=poseCurve(keys,twistArr,lagged)*twistScale;
 // The neck takes up about half of the chest's pitch and roll (it stays over the chest's heading), so the head sits
 // upright on a proud neck instead of being carried forward by a pitched torso.
 _h.identity();for(let n:T.Object3D|null=r.chest;n&&n!==r.group;n=n.parent)_h.premultiply(n.quaternion);_f.set(0,0,1).applyQuaternion(_h);
 _q.setFromEuler(_e.set(0,Math.atan2(_f.x,_f.z),0,'YXZ'));r.neck.quaternion.slerp(_h.invert().multiply(_q),.55*weight);
 _q.setFromEuler(_e.set(-p.pitch*.55-.05*crouch,spin?turn:.30*turn+.20*twist,0,'YXZ'));
 _h.identity();for(let n:T.Object3D|null=r.head.parent;n&&n!==r.group;n=n.parent)_h.premultiply(n.quaternion);
 r.head.quaternion.slerp(_h.invert().multiply(_q),.8*weight);
}
const _v=new T.Vector3(),_lead=new T.Vector3(),_rear=new T.Vector3(),_in=[new T.Vector3(),new T.Vector3()];
/** Feet anchored to the floor, not to the travelling root: a planted foot holds its world position (the authored root travel
 * is subtracted), and a foot only moves while it is lifted. The lead foot steps from its guard spot and lands with zero
 * floor speed on the contact frame; the rear foot holds, then drags up behind the lunge on its toes; after the cut the
 * rear foot has already settled and the lead foot steps back, ending in the staggered guard from stances.ts. */
function feet(r:CharacterRig,p:PlayerView,a:ReturnType<typeof attackPose>,phase:number,sign:number,heavy:boolean,dagger:boolean,overhead:boolean,spin:boolean,weight:number){
 const commitment=p.attackCommitment||attackCommitment(p.spec,p.lastAttack,a.windup,a.window,p.attackRate||1,a.windup+a.window),now=p.lastAttack+a.age,travel=attackTravel(commitment,now),plantTravel=attackTravel(commitment,p.lastAttack+a.windup+a.window*.65),full=attackTravel(commitment,commitment.stepUntil);
 const st=stanceFor(p.spec),leadingLeft=sign>0,lf=leadingLeft===st.leadLeft?st.lead:st.rear,rf=leadingLeft===st.leadLeft?st.rear:st.lead,lx=(leadingLeft?1:-1)*Math.abs(lf.x),rx=(leadingLeft?-1:1)*Math.abs(rf.x);
 const lyaw0=(leadingLeft?1:-1)*lf.toeOut,ryaw0=(leadingLeft?-1:1)*rf.toeOut;
 // Each step moves its foot only while it is lifted: the horizontal travel runs (eased) from 10% to 85% of the step while
 // the lift is a full arc, so the foot leaves and meets the floor vertically instead of skimming it.
 const step=(u:number)=>ease((u-.1)/.75),arc=(u:number)=>Math.sin(Math.PI*u);
 // Lead: a lifted step to the contact spot, planted (world-fixed) from the contact frame, a lifted step back to its guard spot.
 const cx=(leadingLeft?1:-1)*(heavy?.30:.25),worldPlant=.31+plantTravel,su=T.MathUtils.clamp(phase/1.65,0,1),bu=T.MathUtils.clamp((phase-2.45)/.5,0,1),s=step(su),back=step(bu);
 const lift=(overhead?.18:heavy?.14:dagger?.07:.09)*arc(su)+.07*arc(bu),cyaw=.15*sign;
 const lz=phase<2.45?lf.z+(worldPlant-lf.z)*s-travel:(worldPlant-full)*(1-back)+lf.z*back;
 _lead.set(phase<2.45?lx+(cx-lx)*s:cx+(lx-cx)*back,-.795+lift,lz);
 const lyaw=phase<2.45?lyaw0+(cyaw-lyaw0)*s:cyaw+(lyaw0-cyaw)*back,lheel=-.12*arc(su)+.22*arc(bu);
 // Rear: holds its floor spot while the root travels, drags up on its toes through the lunge and settles by 2.4.
 const qu=T.MathUtils.clamp((phase-.75)/1.65,0,1),q=step(qu);
 _rear.set(rx*(1+.18*arc(qu)),-.795+(heavy?.05:.065)*arc(qu),rf.z+full*q-travel);
 const ryaw=ryaw0-.35*sign*arc(qu),rheel=(heavy?.38:.32)*Math.sin(Math.PI*Math.min(1,qu*1.15));
 const lin=weight<1?_in[leadingLeft?0:1]:undefined,rin=weight<1?_in[leadingLeft?1:0]:undefined;
 if(spin){
  // The spin is a hopped turn: both feet hold their floor spots through the windup, leave the floor as the turn speeds up,
  // circle with the body (in the air, so nothing skates) and land back on the guard stance as the turn completes.
  const u=T.MathUtils.clamp((phase-1.1)/1.75,0,1),b=step(u),hop=arc(u),rot=SPIN_FROM+(Math.PI*2-SPIN_FROM)*b,cxs=r.body.position.x,czs=r.body.position.z,c=Math.cos(rot),sn=Math.sin(rot);
  r.body.position.y+=.045*hop;
  const place=(out:T.Vector3,x:number,z:number)=>{const ox=x-cxs,oz=z-czs,px=cxs+ox*c+oz*sn,pz=czs-ox*sn+oz*c;out.set(x+(px-x)*b,-.795+.075*hop,(z-travel)+(pz-(z-travel))*b);};
  place(_lead,lx,lf.z);place(_rear,rx,rf.z);
  foot(r,leadingLeft,_lead,lyaw0+rot*b,.3*hop,weight,lin);foot(r,!leadingLeft,_rear,ryaw0+rot*b,.3*hop,weight,rin);return;
 }
 foot(r,leadingLeft,_lead,lyaw,lheel,weight,lin);foot(r,!leadingLeft,_rear,ryaw,rheel,weight,rin);
}
