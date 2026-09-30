import * as T from 'three';
import {braceWeapon,type CharacterRig} from './character-rig.js';
import type {PlayerView} from '../shared/simulation.js';
import type {attackPose} from './character-animation.js';
import {poseCurve} from './pose-curves.js';
import {attackCommitment,attackTravel,meleeRecovery} from '../shared/souls-combat.js';
import {smoothMotion as ease,ankleInGroup} from './motion-quality.js';
import {stanceFor} from './stances.js';
import {meleeGuard,meleeEntry,GUARD_HOLD,GUARD_FADE} from './melee-guard.js';
import {foot,hand} from './rig-ik.js';
import {meleeHitAt} from './block-impacts.js';
export {foot,hand};
/** Phrase keys: 0..1 windup (swingAt at 1), 1..2 the active window (swingUntil at 2), 2..3 recovery (commitment end at 3).
 * 2.3 is the follow-through extreme: the blade still carries momentum past swingUntil and eases out instead of stopping
 * dead on the hit window's last frame; 2.65 is the return, which comes back low and close instead of retracing the cut. */
const keys=[0,.55,1,1.28,1.65,2,2.3,2.65,3];
/** Share of the greatsword's and the shield's recovery their follow-through drags on before the return (meleePhase `drag`). */
const DRAG=.4,SHIELD_DRAG=.35;
/** Phase speed kept at the drag extreme, as a share of the return's mean speed (the hang drifts; the two beats join C1). */
const DRIFT=1;
/** Cubic Hermite from p0 to p1 over s in 0..1 with end slopes t0, t1 (per unit s). */
const herm=(s:number,p0:number,p1:number,t0:number,t1:number)=>{const s2=s*s,s3=s2*s;return p0+(p1-p0)*(3*s2-2*s3)+t0*(s3-2*s2+s)+t1*(s3-s2);};
/** The fourth dagger cut's body turn: a full forward turn over .8..3 that never unwinds. */
const SPIN_TURN=[0,-.35,-.6,.9,2.4,3.9,5.0,5.9,Math.PI*2],SPIN_FROM=poseCurve(keys,SPIN_TURN,1.1);
type Arr=number[];
/** Phase of an attack from authoritative timestamps. The recovery's phase starts at the strike's phase speed (so the
 * follow-through keeps the blade's momentum) and settles with zero speed at the commitment end. */
export function meleePhase(a:Pick<ReturnType<typeof attackPose>,'age'|'windup'|'window'>,recoverySeconds:number,age=a.age,drag=0,accelerate=false){
 // `accelerate`: the windup eases in and reaches swingAt at the strike's phase speed (no speed step at the contact key).
 if(age<a.windup){const s=Math.max(0,age)/a.windup;return accelerate?herm(s,0,1,0,Math.min(3,a.windup/Math.max(.01,a.window))):s;}
 if(age<a.windup+a.window)return 1+(age-a.windup)/a.window;
 const u=Math.min(1,(age-a.windup-a.window)/Math.max(.01,recoverySeconds)),m=Math.min(accelerate?3:2.1,recoverySeconds/Math.max(.01,a.window));
 if(drag>0){
  // A heavy blade's recovery in two C1 beats: the follow-through keeps the strike's phase speed and decelerates onto the
  // drag extreme (2.65) over the first `drag` of the recovery, still drifting at DRIFT of the return's mean speed (a hang,
  // never a dead stop), then the return picks up from that drift and settles to zero speed at the guard, so the reset
  // reads as a deliberate lift back instead of a stop-then-whip or a fast second drop.
  const v=DRIFT*.35/(1-drag);
  return u<drag?herm(u/drag,2,2.65,m*drag,v*drag):herm((u-drag)/(1-drag),2.65,3,v*(1-drag),0);
 }
 return 2+u*(m+u*((3-2*m)+u*(m-2)));
}
/** A guard-anchored curve: the first and last keys are the guard value, the middle seven are the phrase (times `f`). The
 * keys are written into one reused array (poseCurve reads it at once), so a phrase frame allocates nothing per channel. */
const _k:Arr=new Array(9).fill(0),_ck:Arr=new Array(9).fill(0);
const w=(start:number,mid:Arr,f=1)=>{_k[0]=start;for(let i=0;i<7;i++)_k[i+1]=mid[i]*f;_k[8]=start;return _k;};
/** C1 cubic spline through the keys (weighted three-point tangents, zero at the ends): passing keys keep their velocity and
 * extremes round over with a little momentum instead of stopping dead, as poseCurve's monotone tangents make every reversal
 * do (the greatsword's heavy, whole-body arcs). */
export function flowCurve(times:Arr,values:Arr,time:number){
 const last=times.length-1;if(time<=times[0])return values[0];if(time>=times[last])return values[last];let i=0;while(times[i+1]<time)i++;
 const tan=(n:number)=>{if(n===0||n===last)return 0;const h0=times[n]-times[n-1],h1=times[n+1]-times[n];return ((values[n]-values[n-1])/h0*h1+(values[n+1]-values[n])/h1*h0)/(h0+h1);};
 const h=times[i+1]-times[i];return herm((time-times[i])/h,values[i],values[i+1],tan(i)*h,tan(i+1)*h);
}
/** Seconds the phrase takes to blend in over the pose under it: .065 s out of the guard; out of the running carry or the
 * relaxed idle, which are further from the phrase, .16 s (.6 of a longer windup), never past .85 of the windup, so the
 * entry never snaps and the phrase is exact from swingAt. */
export function phraseEntry(r:object,p:PlayerView,windup:number){const base=Math.min(.065,windup),e=meleeEntry(r,p);return base+Math.max(0,Math.min(.85*windup,Math.max(.16,.6*windup))-base)*(1-e.guard);}
/** Combo-ready chamber: the share of the way from a phrase's first key (the guard) to its anticipation key (.55) where the
 * previous cut's recovery comes to rest, so the next cut starts from where the last one left the body and blade. */
export const CHAMBER=.55;
/** One channel of a combo index: nine keys (guard, the seven phrase keys, guard) and the scale the curve is multiplied by. */
type Chan={v:Arr;s:number;r?:number};
type ArmUse={left:boolean;set:ArmPhrase;mirror:boolean};
type Desc={sign:number;reverse:boolean;spin:boolean;overhead:boolean;low:boolean;ch:Record<string,Chan>;arms:ArmUse[]};
const full=(start:number,mid:Arr,f=1):Arr=>[start,...mid.map(x=>x*f),start];
const descs=new Map<string,Desc>();
/** The channels of one combo index (0-based), cached per weapon and index modulo the longest cycle (12 = lcm of 2, 3, 4). */
function describe(spec:string,index:number):Desc{
 const key=spec+':'+index%12;let d=descs.get(key);if(d)return d;
 const dagger=spec==='daggers',heavy=spec==='greatsword',shield=spec==='shield',reverse=dagger||heavy||shield?index%2===1:index%3===1,sign=reverse?-1:1,overhead=!heavy&&!dagger&&!shield&&index%3===2,spin=dagger&&index%4===3,low=heavy||dagger&&index%4===2;
 // Whole-body turn (hips lead) and torso twist on top of it (shoulders trail the hips, most for the greatsword).
 const turn=spin?SPIN_TURN:heavy?[0,-.45,-1.06,-.90,.46,.96,1.10,1.12,0]:overhead?[0,-.26,-.40,-.15,.14,.30,.42,.20,0]:shield?[0,.30,.62,.46,.20,-.20,-.34,-.26,0]:dagger?[0,-.25,-.45,-.15,.20,.40,.46,.34,0]:[0,-.45,-.85,-.35,.30,.62,1.02,.55,0];
 const twist=heavy?[0,-.24,-.50,-.62,-.36,.42,.52,.52,0]:shield?[0,.14,.28,.06,-.26,-.36,-.36,-.20,0]:dagger?[0,-.10,-.20,-.16,.06,.22,.26,.18,0]:[0,-.18,-.38,-.20,.24,.40,.44,.16,0];
 const ch:Record<string,Chan>={turn:{v:turn,s:spin?1:sign},twist:{v:twist,s:(overhead?.5:1)*sign},
  crouch:{v:heavy?[0,-.10,-.25,-.30,-.29,-.23,-.17,-.12,0]:overhead?[0,-.14,.10,-.06,-.34,-.42,-.40,-.22,0]:shield?[0,-.12,-.20,-.18,-.14,-.10,-.08,-.05,0]:low?[0,-.19,-.28,-.25,-.34,-.30,-.22,-.10,0]:dagger?[0,-.14,-.22,-.19,-.28,-.30,-.24,-.11,0]:[0,-.10,-.16,-.13,-.22,-.26,-.21,-.09,0],s:1},
  bx:{v:heavy?[0,-.07,-.12,-.07,.08,.13,.12,.07,0]:shield?[0,.03,.05,.01,-.05,-.08,-.07,-.03,0]:[0,-.035,-.07,-.02,.065,.09,.08,.035,0],s:sign},
  bz:{v:heavy?[0,-.07,-.13,-.07,.13,.19,.16,.08,0]:[0,-.05,-.08,0,.14,.17,.15,.07,0],s:1},
  rx:{v:heavy?[0,-.08,.20,.30,.28,.21,.14,.08,0]:shield?[0,.03,.07,.05,.06,.05,.04,.02,0]:overhead?[0,-.15,-.28,-.10,.38,.48,.42,.18,0]:[0,-.07,-.15,.04,.20,.26,.22,.10,0],s:1},
  rz:{v:heavy?[0,-.08,-.14,-.14,.10,.17,.15,.07,0]:shield?[0,.04,.07,.02,-.07,-.10,-.08,-.03,0]:[0,-.05,-.11,-.06,.12,.16,.13,.05,0],s:sign},
  spineX:{v:heavy?[0,-.12,-.18,-.04,.14,.20,.12,.05,0]:overhead?[0,-.03,-.12,-.08,.10,.18,.14,.05,0]:[0,-.025,-.08,-.03,.09,.13,.10,.04,0],s:1},
  // Side bend: the spine curls toward the loaded side and over the cut (the greatsword's weight pulls it furthest).
  chestZ:{v:heavy?[0,-.08,-.17,-.10,.12,.19,.17,.09,0]:[0,-.04,-.10,-.06,.07,.11,.09,.04,0],s:sign},
  lsx:{v:[0,.06,.13,.07,-.10,-.16,-.12,-.04,0],s:1},rsx:{v:[0,.08,.18,.11,-.15,-.21,-.16,-.05,0],s:1}};
 const G=meleeGuard(spec)!,arms:ArmUse[]=[];
 if(heavy){
  // Rear-leg load, stamped lead foot, low diagonal sweep, rise ~60 degrees over the lead shoulder, then a heavy drop
  // through the front.
  const m=G.main,g=MELEE_PHRASES.greatsword;ch['G.x']={v:full(m.x,g.x),s:1};ch['G.y']={v:full(m.y,g.y),s:1,r:.2};ch['G.z']={v:full(m.z,g.z),s:1};
  ch['G.rx']={v:full(m.rx,g.rx),s:1,r:.12};ch['G.ry']={v:full(m.ry,g.ry,sign),s:1};ch['G.rz']={v:full(m.rz,g.rz,sign),s:1};
 }else if(dagger){
  // The striking hand alternates: the right-hand tables are mirrored for the left. Chambers stay clear of the shoulder
  // joint (a hand target passing through the shoulder is what flipped the elbow).
  arms.push({left:reverse,set:low?MELEE_PHRASES.daggerLow:MELEE_PHRASES.dagger,mirror:reverse},{left:!reverse,set:MELEE_PHRASES.daggerOther,mirror:reverse});
 }else{
  const phrase=shield?reverse?'shieldRise':'shield':overhead?'swordOverhead':reverse?'swordBackhand':'swordForehand';
  arms.push({left:false,set:MELEE_PHRASES[phrase],mirror:false},{left:true,set:shield?MELEE_PHRASES.shieldOff:reverse?MELEE_PHRASES.swordOffTucked:MELEE_PHRASES.swordOff,mirror:false});
 }
 for(const u of arms){const f=u.mirror?-1:1,h=u.left?'L.':'R.',guard=u.left?G.off:G.main;for(const k of ['x','y','z','rx','ry','rz'] as const)ch[h+k]={v:full(guard[k],u.set[k],k==='x'||k==='ry'||k==='rz'?f:1),s:1};}
 d={sign,reverse,spin,overhead,low,ch,arms};descs.set(key,d);return d;
}
/** A channel's keys for this cut: its first key leans `ready` of the way to its chamber (the previous cut's hold) and its last
 * key is the NEXT cut's chamber (in this cut's scale), so each cut settles into the start of the next. */
function comboKeys(name:string,d:Desc,n:Desc,ready:number,lean:number,afterSpin:boolean){
 const c=d.ch[name],v=c.v,m=n.ch[name];for(let i=0;i<9;i++)_ck[i]=v[i];
 if(!(afterSpin&&name==='turn'))_ck[0]=v[0]+(v[1]-v[0])*(c.r??CHAMBER)*ready;
 // The dagger spin ends its full turn exactly, and the cut after it starts its turn from there.
 if(m&&!(d.spin&&name==='turn'))_ck[8]=(m.v[0]+(m.v[1]-m.v[0])*(m.r??CHAMBER))*m.s/c.s;
 if(lean>0){_ck[0]+=(_ck[2]-_ck[0])*lean;_ck[1]+=(_ck[2]-_ck[1])*lean;}
 return _ck;
}
/** Combo readiness shown by each rig (recorded every melee frame) and the readiness its current cut was entered from. */
type ComboRec={time:number;k:number;attack:number;entry:number;ready:number};const combos=new WeakMap<object,ComboRec>();
function recordCombo(r:object,attack:number,time:number,k:number){let s=combos.get(r);if(!s){s={time,k,attack,entry:NaN,ready:0};combos.set(r,s);}s.time=time;s.k=k;s.attack=attack;}
/** How far into its chamber the current cut began: the combo-ready hold (or the late recovery of the previous cut) this rig
 * showed on the frame before the cut started; 0 for a rig that did not see it (a fresh rig, a rewind or a jump). */
function comboEntry(r:object,p:PlayerView,time:number){
 const s=combos.get(r);if(!s)return 0;
 if(s.entry!==p.lastAttack){s.entry=p.lastAttack;s.ready=s.attack!==p.lastAttack&&time>=s.time&&time-s.time<.2?s.k:0;}
 return s.ready;
}
/** Combo-ready hold weight after a standard melee cut ends (a pure function of authoritative timestamps): 1 from the cut's
 * end (or from the interrupted cut's pose), easing out with the combat-ready guard (GUARD_HOLD then GUARD_FADE); 0 once a roll, hit reaction, knockdown, block,
 * guard or special started after the cut (those layers take over, and their transitions blend out of the hold). */
export function comboHold(p:PlayerView,time:number,end:number){
 if(!meleeGuard(p.spec)||!p.attackIndex||p.lastAttack>time)return 0;
 if(p.guarding||p.blockTime>0)return 0;
 // The first action after the cut releases the hold; it eases out over HOLD_RELEASE from that timestamp under the layer
 // that takes over (a roll or block replaces the pose outright; a hit reaction rides it).
 let cut=Infinity;for(const t of [p.roll?.at,p.hitReaction?.at,p.knockdown?.at,p.juggle?.at,p.meleeAction?.at,p.lastBlock])if(t!==undefined&&t>=p.lastAttack&&t<=time)cut=Math.min(cut,t);
 return (1-ease((time-end-GUARD_HOLD)/GUARD_FADE))*(cut<Infinity?1-ease((time-cut)/HOLD_RELEASE):1);
}
/** Seconds the combo-ready hold takes to release after another action starts. */
export const HOLD_RELEASE=.22;
const _hold={age:0,windup:0,window:0,strike:1} as ReturnType<typeof attackPose>;
/** The combo-ready hold: the last cut's end pose (the next cut's chamber) at its hold weight, over the guard meleeReady posed.
 * A hit reaction that interrupts a cut (the hitstun branch calls this too) releases the phrase from where it was instead. */
export function comboReadyMotion(r:CharacterRig,p:PlayerView,a:ReturnType<typeof attackPose>,time:number,carry:number){
 const rate=p.attackRate||1,end=p.lastAttack+a.windup+a.window+meleeRecovery(p.spec)/rate,k=comboHold(p,time,end)*ease((.85-carry)/.85);
 recordCombo(r,p.lastAttack,time,k);if(k<=.001)return;
 Object.assign(_hold,a);_hold.age=Math.min(a.age,end-p.lastAttack);primaryMeleeMotion(r,p,_hold,k);
}
/** Reference phrases: paired dagger slashes, low heavy sweeps, a stepping sword combo, a hip-driven shield slam. The active
 * sweep stays 1..2; anticipation, follow-through and recovery carry the body's weight around it. Every cut starts from the
 * combo-ready chamber the previous cut left (or the guard) and settles into the next cut's chamber (combo-ready), which is
 * held until the next cut, a roll or other action, or the combat-ready timeout (comboHold). `hold` poses that end pose at the
 * given weight. The greatsword is a kinetic chain on smooth splines: the hips lead the chest by ~60 ms, the blade's wrist
 * trails the hands, and its windup accelerates into the strike instead of changing speed at swingAt. */
export function primaryMeleeMotion(r:CharacterRig,p:PlayerView,a:ReturnType<typeof attackPose>,hold?:number){
 const rate=p.attackRate||1,recoverySeconds=meleeRecovery(p.spec)/rate,heavy=p.spec==='greatsword',drag=heavy?DRAG:p.spec==='shield'?SHIELD_DRAG:0,stop=hold===undefined?hitStop(r,p,a):0,now=p.lastAttack+a.age;
 const index=Math.max(0,(p.attackIndex||1)-1),d=describe(p.spec,index),n=describe(p.spec,index+1),ready=hold===undefined?comboEntry(r,p,now):0;
 const weight=hold??ready+(1-ready)*ease(a.age/phraseEntry(r,p,a.windup)),lean=hold===undefined?meleeEntry(r,p).carry*(1-ready):0,bw=hold??1;
 const total=a.windup+a.window+recoverySeconds,ramp=heavy?ease(a.age/.3)*(1-ease((a.age-total+.32)/.28)):0,at=(off:number)=>meleePhase(a,recoverySeconds,a.age-stop+off*ramp,drag,heavy);
 const phase=at(0),hip=heavy?at(.06):phase,wrist=heavy?at(-.02):phase,curve=heavy?flowCurve:poseCurve;
 const afterSpin=index>0&&describe(p.spec,index-1).spin,val=(name:string,t:number,l=0)=>curve(keys,comboKeys(name,d,n,ready,l,afterSpin),t)*d.ch[name].s;
 const {sign,spin,overhead}=d,dagger=p.spec==='daggers';
 if(hold===undefined)recordCombo(r,p.lastAttack,now,ease(phase-2));
 // Where the incoming layers planted the ankles, before this phrase moves the body (the plant blends from there).
 if(weight<1){ankleInGroup(r,true,_in[0]);ankleInGroup(r,false,_in[1]);}
 // Kinetic chain (greatsword): the pelvis turns ~60 ms ahead of the phrase and the spine takes that lead back out, so the hips
 // open first and the chest, arms and finally the blade's wrist (20 ms behind the hands) follow on the phrase clock.
 const turn=val('turn',phase),twist=val('twist',phase),crouch=val('crouch',phase)*bw,hipLead=heavy?(val('turn',hip)-turn+(val('twist',hip)-twist)*.5)*bw:0;
 r.body.position.x+=val('bx',phase)*bw;r.body.position.z+=val('bz',phase)*bw;r.body.position.y+=crouch;
 r.body.rotation.y+=turn*bw;r.body.rotation.x+=val('rx',phase)*bw;r.body.rotation.z+=val('rz',phase)*bw;
 r.pelvis.rotation.y+=twist*.5*bw+hipLead;r.spine.rotation.y+=twist*.65*bw-hipLead;r.chest.rotation.y+=twist*.55*bw;
 r.spine.rotation.x+=val('spineX',phase)*bw;r.chest.rotation.z+=val('chestZ',phase)*bw;
 // Anti-collapse: a forward-pitched body counter-extends the spine and chest so the chest stays proud instead of curling.
 const pitch=r.body.rotation.x;if(pitch>.2){r.spine.rotation.x-=.45*(pitch-.2);r.chest.rotation.x-=.2*(pitch-.2);}
 r.leftShoulder.rotation.x+=val('lsx',phase)*bw;r.rightShoulder.rotation.x-=val('rsx',phase)*bw;
 r.leftShoulder.rotation.y+=twist*.35*bw;r.rightShoulder.rotation.y-=twist*.35*bw;
 // In the hold the feet are back on the guard stance and belong to the ground pass (it keeps them planted under the hips).
 if(hold===undefined)feet(r,p,a,phase,sign,heavy,dagger,overhead,spin,turn,weight);
 if(heavy){
  _t.set(val('G.x',phase,lean),val('G.y',phase,lean),val('G.z',phase,lean));
  braceWeapon(r,_t,_eu.set(val('G.rx',wrist,lean),val('G.ry',wrist,lean),val('G.rz',wrist,lean)),weight);
 }else for(const u of d.arms){
  const h=u.left?'L.':'R.',f=u.mirror?-1:1;_t.set(val(h+'x',phase,lean),val(h+'y',phase,lean),val(h+'z',phase,lean));_eu.set(val(h+'rx',phase,lean),val(h+'ry',phase,lean),val(h+'rz',phase,lean));
  let pole:T.Vector3|undefined;
  if(u.set.pole){const P=u.set.pole,k=(i:number)=>{for(let j=0;j<7;j++)_pk[j]=P[j][i];return poseCurve(keys,w(i===0?u.left?1:-1:i===1?-.65:-.3,_pk,i===0?f:1),phase);};_pole.set(k(0),k(1),k(2));pole=_pole;}
  hand(r,u.left,_t,_eu,weight,pole);
 }
 headTrack(r,p,a,recoverySeconds,val,spin,crouch,weight,stop,heavy);
 if(hold===undefined&&p.chips.hurricane&&!spin)r.body.rotation.y+=ease(a.strike)*Math.PI*2; // A complete turn never unwinds during recovery.
}
/** Hit-stop (presentation only): when this attack's blade lands (the attacker's 'hit' event, block-impacts.ts), the phrase
 * all but stops for a beat, then catches up. Returned is the phrase's lag behind the authoritative age: it rises at 1-slow
 * over `hold` seconds from the first frame that knew of the hit (so the pose never jumps backward), then eases back to zero
 * over `catchUp`, so the phrase is back on the authoritative clock within ~.16 s of contact; the pose clock never runs
 * backward (its speed is `slow` at least). The root travel, feet anchors and every authoritative timestamp are untouched.
 * A hit that reaches this rig more than ~80 ms after it happened (two 30 Hz frames: rigs animate before the frame's events are
 * read; later is a late network event) shows no stop rather than a pop. */
const HIT_STOP:Record<string,[hold:number,slow:number,catchUp:number]>={greatsword:[.075,.1,.09],sword:[.05,.18,.08],shield:[.06,.14,.08],daggers:[.028,.3,.05]};
type StopGate={attack:number;at:number;from:number;on:boolean};const gates=new WeakMap<object,StopGate>();
export function hitStop(r:object,p:PlayerView,a:Pick<ReturnType<typeof attackPose>,'age'|'windup'|'window'>){
 const k=HIT_STOP[p.spec],t=p.lastAttack+a.age;if(!k||!p.id)return 0;const h=meleeHitAt(p.id,t,.5);
 if(h===undefined||h<p.lastAttack+a.windup-.03||h>p.lastAttack+a.windup+a.window+.05)return 0;
 let g=gates.get(r);if(!g||g.attack!==p.lastAttack||g.at!==h||t<g.from){g={attack:p.lastAttack,at:h,from:t,on:t-h<=.08};gates.set(r,g);}
 if(!g.on)return 0;const [hold,slow,catchUp]=k,u=t-g.from,max=hold*(1-slow);
 return u<=0?0:u<hold?u*(1-slow):u<hold+catchUp?max*(1-ease((u-hold)/catchUp)):0;
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
 // Finisher (reference: a leaping overhead slam): the blade is raised behind the head as the body rises off the floor, then
 // driven down through the front into a deep crouch with the tip planted at the floor, held, and drawn back up to the guard.
 swordOverhead:{x:[-.24,-.18,-.16,-.10,-.06,-.05,-.12],y:[.30,.58,.46,.04,-.22,-.26,-.16],z:[.20,-.06,.18,.42,.40,.36,.30],rx:[-1.1,-2.05,-1.65,-.6,-.45,-.35,-.1],ry:[-.1,-.1,0,.05,.1,.08,.02],rz:[-.18,-.32,-.08,.25,.38,.30,.14],pole:[PO,PO,PO,PO,PO,PO,PO]},
 swordOff:{x:[.38,.42,.44,.30,.22,.24,.26],y:[-.05,-.06,-.02,.04,.10,.08,0],z:[-.02,.02,.06,.24,.26,.24,.20],rx:[-.4,-.4,-.35,-.3,-.35,-.5,-.75],ry:[.4,.6,.4,-.2,-.45,-.35,-.05],rz:[.3,.3,.3,.3,.3,.28,.26]},
 swordOffTucked:{x:[.34,.36,.36,.32,.26,.26,.26],y:[-.10,-.12,-.08,.02,.08,.04,-.02],z:[.04,.02,.04,.14,.22,.22,.18],rx:[-.4,-.4,-.35,-.3,-.35,-.5,-.75],ry:[.4,.6,.4,-.2,-.45,-.35,-.05],rz:[.3,.3,.3,.3,.3,.28,.26]},
 // Shield slam (reference: a backhand bash): the shield is drawn across the chest to the far side in the windup, whips out
 // across the front to the shield side on the release and is held there, face out at shoulder height, before the return.
 shield:{x:[.02,.10,-.10,-.42,-.54,-.56,-.52],y:[.02,.04,.10,.15,.17,.17,.15],z:[.22,.18,.30,.26,.20,.19,.20],rx:[.10,.12,.05,0,0,0,0],ry:[.35,.60,.05,-.75,-1.10,-1.18,-1.05],rz:[.18,.22,.05,-.05,-.08,-.08,-.06]},
 // Second shield cut: the answer to the slam, a rising edge bash from low on the shield side up and across the front.
 shieldRise:{x:[-.40,-.48,-.30,-.05,.06,.08,.02],y:[-.02,-.06,.04,.16,.22,.22,.16],z:[.20,.14,.30,.38,.34,.30,.28],rx:[.25,.35,.1,-.25,-.4,-.4,-.25],ry:[-.6,-.8,-.4,.1,.35,.4,.25],rz:[.1,.15,.1,-.1,-.2,-.2,-.1]},
 shieldOff:{x:[.30,.28,.32,.36,.36,.35,.33],y:[-.06,-.04,-.08,-.12,-.12,-.12,-.10],z:[.14,.18,.06,-.06,-.08,-.06,0],rx:[-.5,-.4,-.4,-.5,-.6,-.7,-.8],ry:[.1,0,0,.1,.2,.2,.2],rz:[.25,.25,.25,.25,.25,.25,.25]},
 dagger:{x:[-.32,-.38,-.34,-.10,0,.02,-.06],y:[.08,.12,.10,-.04,-.14,-.18,-.14],z:[.18,.12,.28,.44,.38,.30,.28],rx:[-.55,-.72,-.55,.2,.45,.50,.35],ry:[-.1,-.3,-.35,.40,.72,.84,.66],rz:[-.2,-.35,-.15,.2,.3,.26,.18],pole:[PD,PD,PD,PD,PD,PD,PD]},
 daggerLow:{x:[-.32,-.38,-.34,-.10,0,.02,-.06],y:[.0,-.04,-.10,-.14,.06,.14,.06],z:[.18,.12,.28,.44,.36,.28,.26],rx:[-.1,.05,.12,-.15,-.2,-.25,-.2],ry:[-.1,-.3,-.35,.40,.72,.84,.66],rz:[-.2,-.35,-.15,.2,.3,.26,.18],pole:[PL,PL,PL,mix(PL,PR),PR,PR,PR]},
 daggerOther:{x:[.20,.24,.28,.32,.30,.24,.18],y:[.10,.16,.12,.08,.14,.16,.12],z:[.30,.26,.22,.10,.16,.22,.26],rx:[-.3,-.2,-.3,-.55,-.75,-.55,-.4],ry:[.3,.55,.85,.7,.25,.15,.1],rz:[.2,.3,.35,.25,.05,.05,.1]},
 greatsword:{x:[-.02,0,.02,.04,.07,.09,.10],y:[.10,.02,-.16,-.06,-.08,-.04,.06],z:[.14,.14,.24,.32,.30,.26,.22],rx:[-.75,-.45,-.33,-.40,-.20,-.30,-.45],ry:[-.25,-.15,.40,.75,.85,.92,.95],rz:[-.2,-.3,-.2,-.1,.1,.16,.2]},
} satisfies Record<string,ArmPhrase>;
const _t=new T.Vector3(),_eu=new T.Euler(),_pole=new T.Vector3(),_pk:Arr=new Array(7).fill(0);
const _q=new T.Quaternion(),_h=new T.Quaternion(),_e=new T.Euler(),_f=new T.Vector3();
/** Gaze stabilisation in the rig group's frame: the head follows a fraction of the lagged body turn (it trails the chest by
 * ~40 ms; a spin spots ahead instead), pitches with the aim and never rolls, however the torso under it pitches and rolls. */
function headTrack(r:CharacterRig,p:PlayerView,a:ReturnType<typeof attackPose>,recoverySeconds:number,val:(name:string,t:number)=>number,spin:boolean,crouch:number,weight:number,stop:number,heavy:boolean){
 const lagged=meleePhase(a,recoverySeconds,a.age-stop+(spin?.04*ease(a.age/.12):-.04*(1-ease((a.age-a.windup-a.window-recoverySeconds+.12)/.12))),heavy?DRAG:p.spec==='shield'?SHIELD_DRAG:0,heavy),turn=val('turn',lagged),twist=val('twist',lagged),spot=spin?ease((lagged-.4)/.8):0;
 // The neck takes up about half of the chest's pitch and roll (it stays over the chest's heading), so the head sits
 // upright on a proud neck instead of being carried forward by a pitched torso.
 _h.identity();for(let n:T.Object3D|null=r.chest;n&&n!==r.group;n=n.parent)_h.premultiply(n.quaternion);_f.set(0,0,1).applyQuaternion(_h);
 _q.setFromEuler(_e.set(0,Math.atan2(_f.x,_f.z),0,'YXZ'));r.neck.quaternion.slerp(_h.invert().multiply(_q),.55*weight);
 _q.setFromEuler(_e.set(-p.pitch*.55-.05*crouch,spin?spot*turn+(1-spot)*(.3*turn+.2*twist):(heavy?.4:.3)*turn+.20*twist,0,'YXZ'));
 _h.identity();for(let n:T.Object3D|null=r.head.parent;n&&n!==r.group;n=n.parent)_h.premultiply(n.quaternion);
 r.head.quaternion.slerp(_h.invert().multiply(_q),.8*weight);
}
const _v=new T.Vector3(),_lead=new T.Vector3(),_rear=new T.Vector3(),_in=[new T.Vector3(),new T.Vector3()];
/** Feet anchored to the floor, not to the travelling root: a planted foot holds its world position (the authored root travel
 * is subtracted), and a foot only moves while it is lifted. The lead foot steps from its guard spot and lands with zero
 * floor speed on the contact frame; the rear foot holds, then drags up behind the lunge on its toes; after the cut the
 * rear foot has already settled and the lead foot steps back, ending in the staggered guard from stances.ts. */
function feet(r:CharacterRig,p:PlayerView,a:ReturnType<typeof attackPose>,phase:number,sign:number,heavy:boolean,dagger:boolean,overhead:boolean,spin:boolean,turn:number,weight:number){
 const commitment=p.attackCommitment||attackCommitment(p.spec,p.lastAttack,a.windup,a.window,p.attackRate||1,a.windup+a.window),now=p.lastAttack+a.age,travel=attackTravel(commitment,now),plantTravel=attackTravel(commitment,p.lastAttack+a.windup+a.window*.65),full=attackTravel(commitment,commitment.stepUntil);
 const st=stanceFor(p.spec),leadingLeft=sign>0,lf=leadingLeft===st.leadLeft?st.lead:st.rear,rf=leadingLeft===st.leadLeft?st.rear:st.lead,lx=(leadingLeft?1:-1)*Math.abs(lf.x),rx=(leadingLeft?-1:1)*Math.abs(rf.x);
 const lyaw0=(leadingLeft?1:-1)*lf.toeOut,ryaw0=(leadingLeft?-1:1)*rf.toeOut;
 // Each step moves its foot only while it is lifted: the horizontal travel runs (eased) from 10% to 85% of the step while
 // the lift is a full arc, so the foot leaves and meets the floor vertically instead of skimming it.
 const step=(u:number)=>ease((u-.1)/.75),arc=(u:number)=>Math.sin(Math.PI*u);
 // The lead step rides the authoritative travel (its progress is the share of the travel done by the plant), so the foot
 // never runs ahead of the hips it hangs from: it stays within leg reach and lands with zero floor speed as the travel plants.
 // Lead: a lifted step to the contact spot, planted (world-fixed) from the contact frame, a lifted step back to its guard spot.
 const cx=(leadingLeft?1:-1)*(heavy?.27:dagger?.29:.25),worldPlant=.31+plantTravel,su=T.MathUtils.clamp(travel/Math.max(.01,plantTravel),0,1),bu=T.MathUtils.clamp((phase-2.45)/.5,0,1),s=su*su*(3-2*su),back=step(bu);
 const lift=(overhead?.18:heavy?.14:dagger?.07:.09)*arc(su)+.07*arc(bu),cyaw=.15*sign;
 const lz=phase<2.45?lf.z+(worldPlant-lf.z)*s-travel:(worldPlant-full)*(1-back)+lf.z*back;
 _lead.set(phase<2.45?lx+(cx-lx)*s:cx+(lx-cx)*back,-.795+lift,lz);
 const lyaw=phase<2.45?lyaw0+(cyaw-lyaw0)*s:cyaw+(lyaw0-cyaw)*back,lheel=-.12*arc(su)+.22*arc(bu);
 // Rear: holds its floor spot while the root travels, drags up on its toes through the lunge and settles by 2.4.
 const qu=T.MathUtils.clamp((phase-.75)/1.65,0,1),q=step(qu);
 _rear.set(rx*(1+(heavy?.3:.18)*arc(qu)),-.795+(heavy?.05:.065)*arc(qu)+(overhead?.09*arc(T.MathUtils.clamp((phase-.45)/.85,0,1)):0),rf.z+full*q-travel-(heavy?.1:0)*arc(qu));
 const ryaw=ryaw0-.35*sign*arc(qu),rheel=(heavy?.38:.32)*Math.sin(Math.PI*Math.min(1,qu*1.15));
 const lin=weight<1?_in[leadingLeft?0:1]:undefined,rin=weight<1?_in[leadingLeft?1:0]:undefined;
 if(spin){
  // The spin is a hopped turn: both feet hold their floor spots through the windup, leave the floor as the turn speeds up,
  // circle about the body just behind its turn (in the air, so nothing skates; never across the chord, which put a foot
  // far outside leg reach) and land back on the guard stance as the turn completes.
  const u=T.MathUtils.clamp((phase-1.1)/1.75,0,1),b=step(u),hop=arc(u),air=ease((u-.05)/.25),rot=air*(SPIN_FROM+(Math.PI*2-SPIN_FROM)*Math.max(b,(turn-SPIN_FROM)/(Math.PI*2-SPIN_FROM))),cxs=r.body.position.x,czs=r.body.position.z,c=Math.cos(rot),sn=Math.sin(rot);
  r.body.position.y+=.045*hop;
  const place=(out:T.Vector3,x:number,z:number)=>{const ox=x-cxs,oz=z-travel*(1-air)-czs;out.set(cxs+ox*c+oz*sn,-.795+.075*hop,czs-ox*sn+oz*c);};
  place(_lead,lx,lf.z);place(_rear,rx,rf.z);reachable(r,leadingLeft,_lead);reachable(r,!leadingLeft,_rear);
  // In the air the feet turn with the body (they leave the floor at its heading and land a full turn later).
  const spinYaw=turn*b;foot(r,leadingLeft,_lead,lyaw0+spinYaw,.3*hop,weight,lin);foot(r,!leadingLeft,_rear,ryaw0+spinYaw,.3*hop,weight,rin);return;
 }
 reachable(r,leadingLeft,_lead);reachable(r,!leadingLeft,_rear);
 foot(r,leadingLeft,_lead,lyaw,lheel,weight,lin);foot(r,!leadingLeft,_rear,ryaw,rheel,weight,rin);
}
/** Leg reach a lifted step keeps inside (just under rig-ik's combat LEG_MAX .485, so the leg never locks straight). */
const STEP_REACH=.475,_hip=new T.Vector3(),_to=new T.Vector3();
/** A lifted foot target the hip cannot reach (a step still in flight while the hips load back or turn away) is drawn toward
 * the hip, which lifts and shortens the step instead of leaving the foot hanging short of its path. The pull fades out as
 * the foot comes down (it is zero on the floor), so a planted foot is never moved and the landing stays continuous. */
function reachable(r:CharacterRig,left:boolean,target:T.Vector3){
 const k=T.MathUtils.clamp((target.y+.795)/.03,0,1);if(k<=0)return;
 _hip.set(0,0,0);for(let n:T.Object3D|null=left?r.leftLeg:r.rightLeg;n&&n!==r.group;n=n.parent)_hip.applyQuaternion(n.quaternion).add(n.position);
 _to.subVectors(target,_hip);const d=_to.length();if(d<=STEP_REACH)return;
 target.addScaledVector(_to,(STEP_REACH/d-1)*k);
}
