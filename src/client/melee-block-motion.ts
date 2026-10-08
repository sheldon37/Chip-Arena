import * as T from 'three';
import {braceWeapon,type CharacterRig} from './character-rig.js';
import {foot,hand} from './primary-melee-motion.js';
import type {PlayerView} from '../shared/simulation.js';
import {meleeGuard,meleeReady,type GuardHand} from './melee-guard.js';
import {blockImpact} from './block-impacts.js';
import {timeJumped} from './motion-quality.js';
import {mocapBlock} from './mocap/mocap-layer.js';

/** Raised guard per weapon (chest-space hand targets and wrist Eulers, as in melee-guard; main is the right hand). */
const BLOCK:Record<string,{main:GuardHand;off?:GuardHand}>={
 // Two hands on the hilt, blade raised diagonally across the upper body.
 greatsword:{main:{x:-.03,y:.16,z:.24,rx:-1.10,ry:.88,rz:-.12}},
 shield:{main:{x:-.025,y:.16,z:.30,rx:-.10,ry:-.08,rz:0},off:{x:.25,y:.12,z:.18,rx:-.9,ry:-.2,rz:.25}},
 // Cross the two blades in front of the chest, with the wrists kept apart.
 daggers:{main:{x:-.20,y:.12,z:.32,rx:-.78,ry:.70,rz:-.2},off:{x:.20,y:.15,z:.34,rx:-.78,ry:-.70,rz:.2}},
 sword:{main:{x:-.27,y:.14,z:.31,rx:-1.05,ry:.76,rz:-.14},off:{x:.27,y:.17,z:.22,rx:-.7,ry:-.25,rz:.25}},
};
/** Raise envelope: ~80% in 60 ms (ease-out), a ~5% overshoot near 90 ms, settled by 130 ms. */
export function blockRaise(t:number){
 if(t<=0)return 0;if(t>=.13)return 1;
 if(t<.09){const u=t/.09;return 1.05*(1-(1-u)**2.6);}
 const u=(t-.09)/.04;return 1.05-.05*u*u*(3-2*u);
}
/** Recoil after a successful block: a fast shove (peak ~40 ms, leaving rest with zero speed so the first frame is not a
 * teleport) that decays by ~160 ms; heavy guards move less but longer. */
export function blockRecoil(t:number,heavy:boolean){const tp=heavy?.055:.04,u=t/tp;return t<=0||t>(heavy?.26:.2)?0:u*u*Math.exp(2*(1-u))*(1-T.MathUtils.smoothstep(t,heavy?.18:.14,heavy?.26:.2));}
type Raise={since:number;time:number};const raises=new WeakMap<object,Raise>();
/** When this guard started rising. A timed block starts at its authoritative lastBlock; a held guard has no timestamp, so
 * the rising edge is detected per rig (a gap of more than .2 s since the rig last showed a guard, however long; a timed
 * block that began in the gap rises from its lastBlock). A rig's first sample or a rewind shows the guard already raised. */
function raiseStart(r:object,p:PlayerView,time:number){
 let s=raises.get(r);const timed=p.blockTime>0&&p.lastBlock!==undefined&&p.lastBlock<=time&&time-p.lastBlock<.5;
 if(!s||time<s.time&&timeJumped(s.time,time)){s={since:time-1,time};raises.set(r,s);}else if(time-s.time>.2)s.since=timed&&p.lastBlock!>s.time?p.lastBlock!:time;
 s.time=Math.max(time,s.time);
 return timed?Math.max(s.since,p.lastBlock!):s.since;
}
const _v=new T.Vector3(),_e=new T.Euler(),lerp=T.MathUtils.lerp;
/** Defensive silhouettes keep the actual weapon between the face/chest and the threat. The guard rises from the combat-ready
 * guard with a quick overshoot and settle, breathes and sways slightly while held with the weight on the rear foot, and
 * gives under a successful block (the weapon shoves toward the chest, the chest rocks back, the knees take it). The fully
 * raised pose at rest is the same silhouette as before. */
export function meleeBlockMotion(r:CharacterRig,p:PlayerView,time:number){
 const heavy=p.spec==='greatsword',shield=p.spec==='shield',daggers=p.spec==='daggers',G=meleeGuard(p.spec),B=BLOCK[p.spec]??BLOCK.sword;
 meleeReady(r,p,time);
 const since=raiseStart(r,p,time),raise=blockRaise(time-since),rw=Math.min(1,raise),arm=Math.min(1,raise*1.6);
 const impact=blockImpact(p.id,time,.3),k=impact?blockRecoil(time-impact.at,heavy||shield)*(daggers?1.3:heavy?.75:shield?.8:1):0;
 const breath=Math.sin(time*2.15)*.015,sway=Math.sin(time*1.7+(p.id?p.id.length:0))*.018,still=1-T.MathUtils.clamp(r.motion/.32,0,1);
 // A weapon with a captured block (mocap/mocap-layer.ts) raises, holds and takes blocked hits from the capture.
 if(mocapBlock(r,p,time,since,impact?.at,p.grounded?still:0))return;
 // The hit drives the whole body: the knees absorb it (drop), the hips are pushed back and the torso rocks back over them.
 r.body.position.y-=(heavy?.13:shield?.11:.09)*raise+.045*k;
 r.body.position.z-=.035*raise+.05*k;r.body.position.x-=.012*still*rw;
 r.body.rotation.x+=.09*raise-.04*k;
 r.body.rotation.y=lerp(r.body.rotation.y,shield?-.22:daggers?0:-.18,rw);
 r.pelvis.rotation.y=lerp(r.pelvis.rotation.y,-.10,rw);r.spine.rotation.set(lerp(r.spine.rotation.x,-.06,rw),lerp(r.spine.rotation.y,.12,rw),r.spine.rotation.z*(1-rw));
 r.chest.rotation.set(lerp(r.chest.rotation.x,-.04+breath,rw)-.08*k,lerp(r.chest.rotation.y,.10,rw)+.05*k,r.chest.rotation.z*(1-rw));r.spine.rotation.x-=.05*k;
 // The head rides the jolt a beat late (it keeps its eyes on the threat, so it tips forward against the chest's rock).
 r.head.rotation.y=lerp(r.head.rotation.y,-.06,rw);r.head.rotation.x=lerp(r.head.rotation.x,-p.pitch*.35-.06,rw)+.06*k;
 r.leftShoulder.rotation.set(lerp(r.leftShoulder.rotation.x,-.06,rw),lerp(r.leftShoulder.rotation.y,-.10,rw),lerp(r.leftShoulder.rotation.z,.06,rw));
 r.rightShoulder.rotation.set(lerp(r.rightShoulder.rotation.x,-.06,rw),lerp(r.rightShoulder.rotation.y,.10,rw),lerp(r.rightShoulder.rotation.z,-.06,rw));
 // Keep walking feet live; at rest settle into a staggered, bent-knee brace.
 if(p.grounded&&still>.001){
  foot(r,true,_v.set(heavy?.22:.19,-.795,.14),.16,0,still);
  foot(r,false,_v.set(heavy?-.22:-.19,-.795,-.12),-.28,0,still);
 }
 // Hands rise from the guard (overshooting slightly), sway while held and are shoved toward the chest by an impact.
 // The weapon is knocked back toward the chest and up, and its tip is driven back past the guard (the wrist gives).
 const put=(b:GuardHand,g:GuardHand|undefined)=>{const s=g??b;_v.set(s.x+(b.x-s.x)*raise,s.y+(b.y-s.y)*raise+.025*k+breath*.3,s.z+(b.z-s.z)*raise-.09*k);_e.set(s.rx+(b.rx-s.rx)*raise+sway-.12*k,s.ry+(b.ry-s.ry)*raise+sway*.6,s.rz+(b.rz-s.rz)*raise);};
 if(heavy){put(B.main,G?.main);braceWeapon(r,_v,_e,arm);return;}
 put(B.main,G?.main);hand(r,false,_v,_e,arm);
 put(B.off!,G?.off);hand(r,true,_v,_e,arm);
}
