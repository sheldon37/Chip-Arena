import * as T from 'three';
import {settleJointCovers,type CharacterRig} from '../../character-rig.js';
import {foot,hand} from '../../rig-ik.js';
import {tickSirenClock} from '../materials.js';
import {tickMinionClock} from './materials.js';
import {hash01,type MinionLook} from './look.js';
import {EMERGE_SECONDS,DEATH_SECONDS,DEATH_FX_SECONDS} from './fx.js';
import type {SirenMinion} from './model.js';
import {PVE_TIMING,LIMB} from '../../../shared/pve/contract.js';
import {VENT_CLIMB_BODY} from '../../../shared/pve/entering.js';
import {applyLostLimbs} from './limbs.js';

/**
 * Siren minion animation set. Every pose is a function of the authoritative fields a future minion view provides
 * (docs/SIREN-MINIONS.md: yaw, vel, spawnAt, attackAt, hitAt, deathAt), the seed and the time, plus ONE small state per
 * minion: its gait phase (the cadence follows speed, like the player's gait; see gaitAt). It never draws random numbers,
 * clamps dt, restarts on rewinds and time jumps, and gives the same feet at 30/60/120 Hz.
 *
 * Poses are evaluated into a flat buffer (joint Euler angles, body offset, foot and hand IK targets), so layers blend as
 * numbers: emergence -> locomotion, locomotion + attack + flinch, and death from the exact pose at deathAt.
 *  - Locomotion: a shambling walk (one dragged foot, lurching hunched torso, loose swinging or reaching arms) and a
 *    frenzied run, cross-faded by speed. Feet are ground-anchored: a planted foot moves backward in the rig exactly as
 *    fast as the body travels (constant-velocity plant, so no skating), then swings forward; legs use the shared IK.
 *  - Idle: weight sway, irregular breathing and Siren-like head and shoulder twitches (hashed event times).
 *  - Attacks: lunge-and-grab (hands, IK), a scrap club swing, a two-handed smash. Flinch: a short impulse response.
 *  - Emergence: claws up out of a black-flame pool, hands slam the floor, hauls itself out, jerks awake.
 *  - Death: recoils, knees buckle, topples forward, chars and burns away feet first into black flame and ash.
 */
export type MinionAttack='lunge'|'club'|'smash'|'swipe';
export type MinionEntryKind='vent'|'door'|'cell';
/** How a PvE add comes in: its own start time (`spawnAt`), whether it is the first through a closed entry (it pushes the
 * grate, or waits for the leaves / gate), and for vents the rig-local z of the rim edge it hauls itself over (negative:
 * the edge is behind the point where it ends up standing; default VENT_RIM_Z). */
export interface MinionEntry {kind:MinionEntryKind;at:number;first:boolean;rimZ?:number}
export interface SirenMinionView {
 yaw:number;vel:{x:number;y?:number;z:number};
 hp?:number;maxHp?:number;
 /** Authority time the minion started to emerge (absent: already standing). */
 spawnAt?:number;
 /** Last attack start (like MinionView.lastAttack) and its kind (default: from the scrap and the seed). */
 attackAt?:number;attack?:MinionAttack;
 /** Last damage taken and the world yaw the hit pushes toward (default: backward). */
 hitAt?:number;hitYaw?:number;
 /** Authority time of death (absent: alive). */
 deathAt?:number;
 /** PvE (design §8.3). The swiping arm in contract terms (1 the right arm, -1 the left; default right). */
 side?:1|-1;
 /** Severed limbs (contract LIMB mask): the joints are hidden by applyLostLimbs; poses adapt (limp, armless bite). */
 lost?:number;
 /** Crawling (a leg is gone) and when it fell (blends the fall; absent: already down). */
 crawl?:boolean;crawlAt?:number;
 /** Entering through a vent, door or cell (replaces the floor emergence). */
 entry?:MinionEntry;
 /** Started diving into a vent: pulls the grate, crouches, grips the rim and drops in; hidden after PVE_TIMING.vent.dive. */
 diveAt?:number;
}
/** Presentation timing the systems phase should match (seconds). `hit` is when each attack's claws/club arrive after
 * attackAt (where the authority should apply damage); `attack` is the full cycle (the chaos zombie's .9 s cooldown). */
export const MINION_TIMING={emerge:EMERGE_SECONDS,death:DEATH_SECONDS,deathFx:DEATH_FX_SECONDS,attack:.9,hit:{lunge:.34,club:.4,smash:.43,swipe:PVE_TIMING.swipeHit},flinch:.42,runFrom:2.0,runTo:3.0,
 vent:PVE_TIMING.vent,door:PVE_TIMING.door,cell:PVE_TIMING.cell,fall:.45,wake:.4} as const;
/** Default rim edge for a vent climb (rig-local z, behind the standing point). */
export const VENT_RIM_Z=-.38;

const TAU=Math.PI*2,ANKLE=-.795;
const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
const sm=(x:number)=>{x=clamp(x);return x*x*(3-2*x);};
const lerp=(a:number,b:number,t:number)=>a+(b-a)*t;
const frac=(x:number)=>x-Math.floor(x);
/** Rises over a..b, holds, falls over c..d. */
const win=(x:number,a:number,b:number,c:number,d:number)=>sm((x-a)/(b-a))*(1-sm((x-c)/(d-c)));
/** Smooth keyed curve (Catmull-Rom through the keys, flat at the ends). */
function keyed(t:number,k:readonly (readonly [number,number])[]){
 if(t<=k[0][0])return k[0][1];const n=k.length;if(t>=k[n-1][0])return k[n-1][1];
 let i=0;while(i<n-2&&t>k[i+1][0])i++;const [t1,p1]=k[i],[t2,p2]=k[i+1],p0=i>0?k[i-1][1]:p1,p3=i+2<n?k[i+2][1]:p2,u=(t-t1)/(t2-t1),dt=t2-t1;
 const t0=i>0?k[i-1][0]:t1-dt,t3=i+2<n?k[i+2][0]:t2+dt,m1=(p2-p0)/(t2-t0)*dt,m2=(p3-p1)/(t3-t1)*dt,u2=u*u,u3=u2*u;
 return (2*u3-3*u2+1)*p1+(u3-2*u2+u)*m1+(-2*u3+3*u2)*p2+(u3-u2)*m2;
}
/** Smooth deterministic 1D noise in [-1,1]. */
function noise(seed:number,x:number,salt=0){const i=Math.floor(x),f=x-i,u=f*f*(3-2*f);return 2*lerp(hash01(seed,i,salt),hash01(seed,i+1,salt),u)-1;}

// ---------------------------------------------------------------------------------------------------------------
// Pose buffer: Euler (x,y,z) per joint, the motion-root offset, foot targets (x,y,z,yaw,heel,weight) and hand targets
// (x,y,z in the rig group, then the wrist pitch and weight).
const J={body:0,pelvis:3,spine:6,chest:9,neck:12,head:15,lClav:18,rClav:21,lArm:24,rArm:27,lElbow:30,rElbow:33,lHand:36,rHand:39,lHip:42,rHip:45,lKnee:48,rKnee:51} as const;
const BODY=54,FOOT=57,HAND=69,SIZE=79;
type Pose=Float64Array;
const side=(base:number,s:number)=>s>0?base:base+3;
const blendPose=(out:Pose,a:Pose,b:Pose,t:number)=>{for(let i=0;i<SIZE;i++)out[i]=a[i]+(b[i]-a[i])*t;return out;};

// ---------------------------------------------------------------------------------------------------------------
// Twitches: one per window, at a hashed time inside it; snap in .045 s, hold, ease out by .5 s.
function twitch(look:MinionLook,t:number){
 const W=1/look.twitch,seed=look.seed^0x7a3;let head=0,nod=0,turn=0,shoulder=0,spasm=0;
 for(const k of [Math.floor(t/W),Math.floor(t/W)-1]){const at=k*W+hash01(seed,k,1)*W*.6,u=t-at;if(u<0||u>.5)continue;
  const a=(u<.045?sm(u/.045):u<.14?1:1-sm((u-.14)/.36))*look.jerk,kind=hash01(seed,k,2),dir=hash01(seed,k,3)<.5?-1:1;
  if(kind<.45){head+=dir*.55*a;turn+=dir*.32*a;}else if(kind<.7)nod-=.42*a;else if(kind<.88)shoulder+=dir*a;else spasm+=a;}
 return {head,nod,turn,shoulder,spasm};
}

// ---------------------------------------------------------------------------------------------------------------
// Locomotion (walk + run, cross-faded) into the pose. Group-local velocity (x right, z forward).
/** Gait cycles per second. Cadence rises with speed so a planted foot never has to travel farther than the short legs
 * reach (about .44 m walking, .5 m running); the seed sets the base rhythm. */
export const walkCadence=(look:MinionLook,speed:number)=>Math.max(look.cadence*.85,speed*.66/.44);
export const runCadence=(look:MinionLook,speed:number)=>Math.max(look.runCadence,speed*.42/.5);
/** Walk-to-run weight of a speed (m/s), and the one gait cadence: the walk's below the band, the run's above it. */
export const runWeight=(speed:number)=>sm((speed-MINION_TIMING.runFrom)/(MINION_TIMING.runTo-MINION_TIMING.runFrom));
export const gaitCadence=(look:MinionLook,speed:number)=>lerp(walkCadence(look,speed),runCadence(look,speed),runWeight(speed));
/** Turning: a planted foot stays put in the world while the body yaws at `wy` rad/s. `k` is the foot's travel factor
 * (seconds from mid-stance, negative later); the hip-relative point turns by `a` and the travel by `a/2` (rotation.y). */
let tx=0,tz=0;
function turned(x:number,z:number,dx:number,dz:number,a:number){const c=Math.cos(a),s=Math.sin(a),c2=Math.cos(a/2),s2=Math.sin(a/2),px=x-dx,pz=z-dz;tx=px*c+pz*s+dx*c2+dz*s2;tz=-px*s+pz*c-dx*s2+dz*c2;}
const turnAngle=(wy:number,k:number,travel:number)=>Math.max(-.6,Math.min(.6,wy*k/travel));
function locomotion(P:Pose,look:MinionLook,t:number,vx:number,vz:number,phi:number,injured=0,wy=0,stump:0|1|-1=0){
 const speed=Math.hypot(vx,vz),mv=sm((speed-.08)/.4),run=runWeight(speed),lead=-look.drag;
 const f=gaitCadence(look,speed);
 // Feet: one cycle whose stance, reach, lift and heel blend from the shamble to the run, so a planted foot stays planted
 // through the walk-run band too (two cycles mixed out of step slide).
 for(const sd of [1,-1]){
  const o=FOOT+(sd>0?0:6);
  if(sd===stump){
   // Limp: the shin is gone. The thigh swings loosely from the hip and the knee stump drags; no foot to plant.
   const sw=Math.sin(TAU*phi+.6);P[side(J.lHip,sd)]=-.3+.28*sw*mv-.1*run;P[side(J.lHip,sd)+2]=sd*.08;P[side(J.lKnee,sd)]=.5+.18*(.5+.5*sw)*mv;P[o+5]=0;continue;}
  const drag=!stump&&sd===look.drag;
  // Walk: the good leg plants for longer; the dragged leg scuffs forward low with its toe down and swings out.
  // Run: frenzied, short contact, high knees, the bad leg still a little behind.
  const c=stump?lerp(.68,.56,run):lerp(drag?.56:.66,drag?.38:.42,run),ph=frac(phi+(drag?.5+lerp(.05,.03,run)*look.limp:0)),travel=Math.min(1,lerp(.46,.52,run)/Math.max(1e-6,speed*c/f));
  const hx=sd*lerp(.15,.12,run),hz=lerp(drag?-.05*look.limp:.02,.04,run),span=(c/f)*travel;
  let x:number,z:number,y=ANKLE,heel=0,yaw=sd*lerp(drag?.28:.1,.06,run),k:number;
  if(ph<c){const u=ph/c;k=-span*(u-.5);x=hx+vx*k;z=hz+vz*k;}
  else{const w=(ph-c)/(1-c),e=w*w*(3-2*w),arc=Math.sin(Math.PI*w),late=sm((w-.25)/.4);k=span*(e-.5);x=hx+vx*k;z=hz+vz*k;let lift:number,wheel:number;
   if(drag){lift=(.034+.012*(1-look.limp))*arc*mv;wheel=.24*arc*mv;x+=sd*.035*arc*look.limp*mv*(1-run);}
   else if(stump){lift=.09*Math.pow(arc,.8)*mv;wheel=.2*arc*(1-2*w)*mv;}
   else{lift=(.05+.05*Math.min(1,speed/1.5))*Math.pow(arc,.8)*mv;wheel=.28*arc*(1-2*w)*mv;}
   z+=.06*arc*run;y+=lerp(lift,(drag?.11:.19)*Math.pow(arc,.7),run);heel=lerp(wheel,(.55*(1-late)-.25*late)*arc,run);}
  if(wy){const a=turnAngle(wy,k,travel)*mv;turned(x,z,vx*k,vz*k,a);x=tx;z=tz;yaw+=a;}
  P[o]=x;P[o+1]=y;P[o+2]=z;P[o+3]=yaw;P[o+4]=heel;P[o+5]=1;
  if(stump)P[o]-=sd*.05*(1-run);
 }
 // Body: hunched and lowered; dips on the bad leg, sways over the planted foot, rolls toward the bad side.
 const hunch=(look.hunch+.18*injured)*(1+.35*run),dragStance=frac(phi+.5+.05*look.limp),dip=-.035*look.limp*Math.sin(Math.PI*Math.min(1,dragStance/.56))*mv;
 const idle=1-mv,drift=Math.sin(t*.83+look.phase*6)+.4*Math.sin(t*1.37+look.phase*3),sway=.046*mv*Math.cos(TAU*(phi-.3))*lead*(1+.5*look.limp)+.026*idle*drift,bob=-.02*(.5+.5*Math.cos(2*TAU*phi))*mv;
 const rbob=-.04*(.5+.5*Math.cos(2*TAU*phi));
 P[BODY]=lerp(sway,.012*Math.sin(TAU*phi),run);P[BODY+1]=lerp(-.075-.05*look.hunch+bob+dip,-.085+rbob,run);P[BODY+2]=lerp(.0,.04,run);
 P[J.body]=lerp(.04*mv,.32,run);P[J.body+1]=lerp(.1*Math.sin(TAU*phi)*mv,.16*Math.sin(TAU*phi),run);P[J.body+2]=lerp(-look.drag*.05*look.limp-sway*2.2,.06*Math.sin(TAU*phi),run);
 if(stump){
  // A hop on the good leg: the body rises off it at push-off, lands heavily, and leans over it.
  const ph=frac(phi),hop=Math.sin(Math.PI*Math.min(1,ph/.68));P[BODY+1]+=(-.06+.07*hop)*mv-.03;P[BODY]-=stump*.035;P[J.body+2]+=stump*(.07+.05*mv);P[J.body]+=.06*mv;}
 // A lurch: the torso pitches forward each time the good leg plants.
 const lurch=.14*mv*Math.max(0,Math.sin(TAU*phi))**3+.05*idle*(.5+.5*Math.sin(t*.61+look.phase*4));
 P[J.pelvis]=.04;P[J.pelvis+1]=-.06*Math.sin(TAU*phi)*mv;P[J.pelvis+2]=0;
 const breath=.025*Math.sin(t*1.7+look.phase*6)+.012*noise(look.seed,t*.9,1);
 P[J.spine]=hunch*.5+lurch;P[J.spine+1]=0;P[J.spine+2]=.4*sway;
 P[J.chest]=hunch*.5+breath;P[J.chest+1]=lerp(-.08*Math.sin(TAU*phi)*mv,-.14*Math.sin(TAU*phi),run)+.05*noise(look.seed,t*.4,2);P[J.chest+2]=look.shoulderDrop;
 const loll=noise(look.seed,t*.6,3);
 P[J.neck]=-hunch*.55+.06-.5*run;P[J.neck+1]=.08*noise(look.seed,t*.3,4);P[J.neck+2]=0;
 P[J.head]=.12+.06*loll+lurch*.6-.25*run+.06*Math.sin(2*TAU*phi)*mv;P[J.head+1]=.1*noise(look.seed,t*.5,5);P[J.head+2]=look.headTilt+.05*Math.sin(TAU*phi)*mv+.06*loll;
 // Arms: limp pendulums that lag the body, or a classic reach; frenzied flailing when running.
 const pitch=hunch+.04*mv+lerp(0,.32,run);
 for(const sd of [1,-1]){
  const reach=look.arms==='reach'||(look.arms==='mixed'&&sd===look.reachSide),scrapHand=sd<0&&look.scrap!=='none';
  const swing=TAU*phi+(sd===lead?Math.PI:0)-.7,amp=.05+.24*mv,drift=noise(look.seed,t*.7,10+sd);
  let ax:number,ay:number,az:number,ex:number,hx2:number;
  if(reach&&!scrapHand){ax=-Math.PI/2-pitch*.85+.3+.12*Math.sin(swing*.5+sd)+.08*drift;ay=sd*.05;az=-sd*.1;ex=-.25-.15*(.5+.5*Math.sin(t*1.7+sd*2));hx2=.45+.1*drift;}
  else{ax=-pitch*.85+.08+amp*Math.sin(swing)+.04*drift;ay=sd*-.12;az=sd*(.1+.03*Math.sin(swing*2));ex=-.18-.22*(.5+.5*Math.sin(swing-.9))*(.4+mv);hx2=scrapHand?.85:.15+.18*Math.sin(swing-1.6);}
  // Run: wild alternating swings, elbows cocked, hands clawing.
  const rs=TAU*phi+(sd>0?0:Math.PI),fl=noise(look.seed,t*3.1,20+sd);
  const rax=reach&&!scrapHand?-1.55-pitch*.6+.35*Math.sin(rs)+.15*fl:-1.0-pitch*.5+.75*Math.sin(rs)+.18*fl,raz=sd*(.32+.15*Math.sin(2*rs+sd)),rex=-1.05+.35*Math.sin(rs+.8);
  P[side(J.lArm,sd)]=lerp(ax,rax,run);P[side(J.lArm,sd)+1]=lerp(ay,0,run);P[side(J.lArm,sd)+2]=lerp(az,raz,run);
  P[side(J.lElbow,sd)]=lerp(ex,rex,run);P[side(J.lHand,sd)]=lerp(hx2,scrapHand?.6:.3,run);P[side(J.lHand,sd)+1]=0;P[side(J.lHand,sd)+2]=0;
  P[side(J.lClav,sd)]=0;P[side(J.lClav,sd)+1]=0;P[side(J.lClav,sd)+2]=sd*.04*(look.shoulderDrop*sd>0?-1:1);
 }
 for(const sd of [1,-1]){const h=HAND+(sd>0?0:5);P[h]=P[h+1]=P[h+2]=P[h+3]=P[h+4]=0;}
}
/** Twitches layered on any upright pose. */
function addTwitch(P:Pose,look:MinionLook,t:number,k=1){
 const tw=twitch(look,t);P[J.head+2]+=tw.head*k;P[J.head+1]+=tw.turn*k;P[J.head]+=tw.nod*k;
 const s=tw.shoulder*k;if(s){P[side(J.lClav,s>0?1:-1)+2]+=Math.sign(s)*.22*Math.abs(s);P[J.chest+2]+=-.08*s;}
 if(tw.spasm){P[J.spine]-=.2*tw.spasm*k;for(const sd of [1,-1]){P[side(J.lArm,sd)+2]+=sd*.35*tw.spasm*k;P[side(J.lElbow,sd)]-=.3*tw.spasm*k;}}
}

// ---------------------------------------------------------------------------------------------------------------
// Attacks, layered over locomotion by their own weight. `a` = seconds since attackAt.
const LUNGE={spine:[[0,0],[.18,-.22],[.33,.3],[.52,.25],[.78,.05]],bodyZ:[[0,0],[.18,-.07],[.33,.3],[.56,.24],[.86,0]],bodyY:[[0,0],[.18,.02],[.33,-.06],[.6,-.04],[.86,0]],head:[[0,0],[.18,-.35],[.33,.2],[.43,.45],[.52,.15],[.78,0]],
 reachX:[[0,.34],[.18,.38],[.33,.2],[.45,.1],[.7,.2]],reachY:[[0,.25],[.18,.38],[.33,.12],[.5,.08],[.7,0]],reachZ:[[0,.2],[.18,.12],[.33,.56],[.5,.5],[.7,.38]]} as const;
const CLUB={armX:[[0,-.2],[.27,-2.75],[.37,-1.25],[.47,.15],[.62,.2],[.9,-.1]],armZ:[[0,0],[.27,-.5],[.37,-.18],[.47,.15],[.62,.1],[.9,0]],elbow:[[0,-.3],[.27,-1.7],[.37,-.55],[.47,-.25],[.62,-.4],[.9,-.3]],
 wrist:[[0,.85],[.27,.15],[.37,.1],[.47,.55],[.62,.8],[.9,.85]],twist:[[0,0],[.27,.48],[.4,-.38],[.6,-.22],[.88,0]],spine:[[0,0],[.27,-.22],[.39,.32],[.6,.2],[.88,0]],bodyZ:[[0,0],[.27,-.05],[.39,.18],[.6,.12],[.88,0]]} as const;
const SMASH={armX:[[0,-.4],[.29,-2.95],[.41,-1.05],[.51,-.35],[.7,-.45],[.9,-.3]],elbow:[[0,-.3],[.29,-1.25],[.41,-.2],[.51,-.3],[.7,-.5],[.9,-.3]],spine:[[0,0],[.29,-.32],[.43,.42],[.66,.28],[.88,0]],bodyY:[[0,0],[.29,.03],[.43,-.1],[.66,-.07],[.88,0]],bodyZ:[[0,0],[.29,-.04],[.43,.16],[.66,.12],[.88,0]],head:[[0,0],[.29,-.4],[.43,.35],[.7,.1],[.88,0]]} as const;
// Swipe: a one-arm horizontal rake (values in the arm's own side units: +armZ swings it out to its side). The arm cocks
// out to the side and back with the elbow bent, rakes across the chest, the elbow snapping straight at contact
// (PVE_TIMING.swipeHit), the chest twisting through and the body leaning into it; then it recovers.
const SWIPE={armX:[[0,-.35],[.2,-1.5],[.3,-1.62],[.36,-1.5],[.46,-1.18],[.64,-.6],[.9,-.3]],armY:[[0,0],[.2,.45],[.3,.25],[.36,0],[.46,-.3],[.64,-.1],[.9,0]],
 armZ:[[0,.12],[.2,1.12],[.27,.75],[.34,.02],[.42,-.72],[.5,-.9],[.66,-.45],[.9,.05]],elbow:[[0,-.45],[.2,-.62],[.28,-.45],[.34,-.15],[.44,-.2],[.6,-.55],[.9,-.4]],
 wrist:[[0,.2],[.2,.55],[.34,-.25],[.5,.05],[.9,.2]],twist:[[0,0],[.2,.42],[.3,.18],[.36,-.15],[.46,-.45],[.64,-.22],[.9,0]],
 spine:[[0,0],[.2,-.1],[.34,.3],[.48,.34],[.7,.12],[.9,0]],bodyZ:[[0,0],[.2,-.05],[.34,.16],[.52,.13],[.9,0]],head:[[0,0],[.2,-.25],[.34,.25],[.6,.06],[.9,0]]} as const;
// Armless bite: the head and chest snap forward and down at the target, the shoulders hunched.
const BITE={neck:[[0,0],[.2,-.35],[.34,.55],[.5,.45],[.8,0]],head:[[0,0],[.2,-.45],[.32,.35],[.4,.6],[.55,.2],[.8,0]],chest:[[0,0],[.2,-.12],[.34,.18],[.6,.1],[.85,0]]} as const;
/** Contract side (1 right arm, -1 left) to the rig side (+1 left, -1 right); a lost arm swipes with the other one. */
export function swipeArm(s:SirenMinionView):1|-1{
 let sd:1|-1=s.side===-1?1:-1;const lost=s.lost??0,gone=(k:1|-1)=>!!(lost&(k>0?LIMB.lArm:LIMB.rArm));
 if(gone(sd)&&!gone(-sd as 1|-1))sd=-sd as 1|-1;return sd;
}
/** Both arms gone (whole arms or forearms: no hands to grab with). */
export const armless=(lost=0)=>!!(lost&(LIMB.lArm|LIMB.lFore))&&!!(lost&(LIMB.rArm|LIMB.rFore));
/** The leg whose shin is gone (rig side +1 left, -1 right; 0 none). A whole leg makes it crawl instead. */
export const stumpLeg=(lost=0):0|1|-1=>lost&(LIMB.lLeg|LIMB.rLeg)?0:lost&LIMB.lShin&&!(lost&LIMB.rShin)?1:lost&LIMB.rShin&&!(lost&LIMB.lShin)?-1:0;
export function attackKind(look:MinionLook,s:SirenMinionView):MinionAttack{return s.attack??(look.scrap!=='none'?'club':hash01(look.seed,Math.round((s.attackAt||0)*20),31)<.65?'lunge':'smash');}
function attackLayer(P:Pose,look:MinionLook,s:SirenMinionView,a:number){
 const kind=attackKind(look,s),w=win(a,0,.16,.64,.9);if(w<=0)return;
 if((kind==='lunge'||kind==='swipe')&&armless(s.lost)){
  // No arms left: it bites. Head and chest snap forward and down; the shoulders hunch.
  P[J.spine]+=w*keyed(a,LUNGE.spine);P[BODY+2]+=w*keyed(a,LUNGE.bodyZ)*1.2;P[BODY+1]+=w*keyed(a,LUNGE.bodyY);
  P[J.neck]+=w*keyed(a,BITE.neck);P[J.head]+=w*keyed(a,BITE.head);P[J.chest]+=w*keyed(a,BITE.chest);P[J.lClav+2]+=w*.18;P[J.rClav+2]-=w*.18;
 }else if(kind==='swipe'){
  const sd=swipeArm(s),arm=side(J.lArm,sd),el=side(J.lElbow,sd),hd=side(J.lHand,sd);
  P[J.spine]+=w*keyed(a,SWIPE.spine);P[BODY+2]+=w*keyed(a,SWIPE.bodyZ);P[J.head]+=w*keyed(a,SWIPE.head);
  // The rake stays at chest height however hunched the torso is: the arm's pitch takes the torso's lean out.
  const lean=P[J.body]+P[J.spine]+P[J.chest];
  P[arm]=lerp(P[arm],keyed(a,SWIPE.armX)-lean,w);P[arm+1]=lerp(P[arm+1],sd*keyed(a,SWIPE.armY),w);P[arm+2]=lerp(P[arm+2],sd*keyed(a,SWIPE.armZ),w);
  P[el]=lerp(P[el],keyed(a,SWIPE.elbow),w);P[hd]=lerp(P[hd],keyed(a,SWIPE.wrist),w);P[hd+2]=lerp(P[hd+2],0,w);
  const tw=keyed(a,SWIPE.twist);P[J.chest+1]+=w*sd*tw;P[J.spine+1]+=w*sd*tw*.4;P[J.head+1]-=w*sd*tw*.6;
  // The other arm hangs back and a little out, for balance.
  const o=side(J.lArm,-sd);P[o]=lerp(P[o],.25,w*.6);P[o+2]=lerp(P[o+2],-sd*.3,w*.6);
 }else if(kind==='lunge'){
  P[J.spine]+=w*keyed(a,LUNGE.spine);P[BODY+2]+=w*keyed(a,LUNGE.bodyZ);P[BODY+1]+=w*keyed(a,LUNGE.bodyY);P[J.head]+=w*keyed(a,LUNGE.head);
  // Both hands claw out to grab at chest height: rig-group targets ahead of the lunging chest.
  const cz=chestZ(P);
  for(const sd of [1,-1]){const h=HAND+(sd>0?0:5);_v.set(sd*keyed(a,LUNGE.reachX),keyed(a,LUNGE.reachY),cz+keyed(a,LUNGE.reachZ));P[h]=_v.x;P[h+1]=_v.y;P[h+2]=_v.z;P[h+3]=-.3;P[h+4]=win(a,0,.2,.6,.88);}
 }else if(kind==='club'){
  const sd=-1;P[side(J.lArm,sd)]=lerp(P[side(J.lArm,sd)],keyed(a,CLUB.armX),w);P[side(J.lArm,sd)+2]=lerp(P[side(J.lArm,sd)+2],keyed(a,CLUB.armZ),w);P[side(J.lArm,sd)+1]=lerp(P[side(J.lArm,sd)+1],0,w);
  P[side(J.lElbow,sd)]=lerp(P[side(J.lElbow,sd)],keyed(a,CLUB.elbow),w);P[side(J.lHand,sd)]=lerp(P[side(J.lHand,sd)],keyed(a,CLUB.wrist),w);
  P[J.chest+1]+=w*keyed(a,CLUB.twist);P[J.spine]+=w*keyed(a,CLUB.spine);P[BODY+2]+=w*keyed(a,CLUB.bodyZ);
  // The free arm swings out for balance.
  P[J.lArm+2]=lerp(P[J.lArm+2],.55,w*win(a,.05,.25,.4,.7));P[J.lArm]=lerp(P[J.lArm],-.6,w*.6);
 }else{
  for(const sd of [1,-1]){P[side(J.lArm,sd)]=lerp(P[side(J.lArm,sd)],keyed(a,SMASH.armX),w);P[side(J.lArm,sd)+2]=lerp(P[side(J.lArm,sd)+2],-sd*.12,w);P[side(J.lArm,sd)+1]=lerp(P[side(J.lArm,sd)+1],0,w);P[side(J.lElbow,sd)]=lerp(P[side(J.lElbow,sd)],keyed(a,SMASH.elbow),w);P[side(J.lHand,sd)]=lerp(P[side(J.lHand,sd)],-.2,w);}
  P[J.spine]+=w*keyed(a,SMASH.spine);P[BODY+1]+=w*keyed(a,SMASH.bodyY);P[BODY+2]+=w*keyed(a,SMASH.bodyZ);P[J.head]+=w*keyed(a,SMASH.head);
 }
}
const _cq=new T.Quaternion(),_ce=new T.Euler(),_cp=new T.Vector3(),_co=new T.Vector3();
/** Forward position (rig group z) of the chest joint for the torso in the pose buffer. */
function chestZ(P:Pose){
 _co.set(P[BODY],P[BODY+1],P[BODY+2]);_cq.setFromEuler(_ce.set(P[J.body],P[J.body+1],P[J.body+2]));
 for(const [y,i] of [[-.24,J.pelvis],[.105,J.spine]] as const){_co.add(_cp.set(0,y,0).applyQuaternion(_cq));_cq.multiply(_qc.setFromEuler(_ce.set(P[i],P[i+1],P[i+2])));}
 return _co.add(_cp.set(0,.19,0).applyQuaternion(_cq)).z;
}
const _qc=new T.Quaternion();
/** Hit flinch: a snap away from the hit that rings out over .42 s. */
function flinchLayer(P:Pose,look:MinionLook,s:SirenMinionView,h:number){
 if(h<0||h>MINION_TIMING.flinch)return;const k=(h<.06?sm(h/.06):Math.exp(-(h-.06)*7.5))*(1-sm((h-.3)/.12));
 const rel=(s.hitYaw??s.yaw+Math.PI)-s.yaw,back=-Math.cos(rel),lat=Math.sin(rel);
 P[J.spine]-=.42*k*Math.max(.3,back);P[J.spine+2]-=.3*k*lat;P[J.chest]-=.18*k;P[J.head]-=.5*k;P[J.head+2]-=.35*k*lat*(hash01(look.seed,Math.round((s.hitAt||0)*30))<.5?1:-1.3);
 P[BODY+2]-=.07*k*back;P[BODY]+=.05*k*lat;
 for(const sd of [1,-1]){P[side(J.lArm,sd)+2]+=sd*.5*k;P[side(J.lArm,sd)]-=.25*k;P[side(J.lElbow,sd)]-=.35*k;}
}

// ---------------------------------------------------------------------------------------------------------------
// Crawl (PvE: a leg is gone). Prone, the chest just off the floor, the head raised to look ahead; the arms pull it along
// alternately with planted hands (a planted hand moves back in the rig exactly as fast as the body travels, like a
// planted foot), the remaining legs trail and kick. The cadence follows speed (crawlCadence) so hands never over-reach.
export const crawlCadence=(speed:number)=>Math.max(.55,speed*.6/.4);
const CRAWL_PITCH=1.3;
function crawlPose(P:Pose,look:MinionLook,t:number,vx:number,vz:number,phi:number,floor:number){
 P.fill(0);const speed=Math.hypot(vx,vz),mv=sm((speed-.05)/.25),f=crawlCadence(speed);
 const roll=Math.sin(TAU*phi);
 P[J.body]=CRAWL_PITCH+.04*roll*mv;P[J.body+1]=.12*roll;P[J.body+2]=.1*roll;
 P[BODY]=.03*roll;P[BODY+1]=floor+.26+.025*Math.abs(Math.cos(TAU*phi))*mv;P[BODY+2]=-.08;
 P[J.spine]=-.08+.06*Math.sin(TAU*phi*2);P[J.spine+1]=-.1*roll;P[J.chest]=-.05;P[J.chest+1]=-.14*roll;
 // The head strains up to look where it is going.
 const loll=noise(look.seed,t*.6,3);P[J.neck]=-.62;P[J.head]=-.55+.08*loll;P[J.head+2]=look.headTilt*.6+.05*loll;P[J.head+1]=.1*noise(look.seed,t*.5,5);
 for(const sd of [1,-1]){
  // Arm cycle: stance (hand planted, sweeping back as the body passes over it), then a reach forward.
  const c=.6,ph=frac(phi+(sd>0?0:.5)),h=HAND+(sd>0?0:5),span=c/f*Math.max(speed,.0);
  let z:number,y=floor+.035;const base=.62;
  if(ph<c){const u=ph/c;z=base+.2-speed*(ph/f);void u;}
  else{const w=(ph-c)/(1-c),e=w*w*(3-2*w);z=base+.2-span+(span)*e;y+=.13*Math.sin(Math.PI*w)*(.35+.65*mv)+.05*Math.sin(Math.PI*w);}
  P[h]=sd*(.3+.03*Math.sin(TAU*ph));P[h+1]=y;P[h+2]=z;P[h+3]=-1.3;P[h+4]=1;
  P[side(J.lArm,sd)]=-2.6;P[side(J.lArm,sd)+2]=sd*.3;P[side(J.lElbow,sd)]=-.6;P[side(J.lHand,sd)]=-.5;
  // Legs drag behind, a weak frog kick on each pull.
  const k=Math.max(0,Math.sin(TAU*ph+1.2));P[side(J.lHip,sd)]=.05-.3*k*mv;P[side(J.lHip,sd)+2]=sd*(.12+.05*k);P[side(J.lKnee,sd)]=.25+.75*k*mv+.15*(1-mv);
  const o=FOOT+(sd>0?0:6);P[o+5]=0;
 }
}
/** A crawling claw (PvE crawlers swipe low): the arm on the swiping side rears up and rakes down at the floor ahead. */
const CRAWL_CLAW={y:[[0,.035],[.2,.42],[.34,.06],[.5,.035]],z:[[0,.72],[.2,.62],[.34,1.08],[.5,.9],[.9,.72]],head:[[0,0],[.2,-.3],[.34,.25],[.6,0]]} as const;
function crawlAttack(P:Pose,s:SirenMinionView,a:number,floor:number){
 const w=win(a,0,.12,.6,.9);if(w<=0)return;const sd=armless(s.lost)?0:swipeArm(s);
 P[J.head]+=w*keyed(a,CRAWL_CLAW.head);P[BODY+2]+=w*.12*win(a,.2,.34,.5,.8);
 if(!sd)return;const h=HAND+(sd>0?0:5);P[h]=lerp(P[h],sd*.18,w);P[h+1]=lerp(P[h+1],floor+keyed(a,CRAWL_CLAW.y),w);P[h+2]=lerp(P[h+2],keyed(a,CRAWL_CLAW.z),w);
 P[J.chest+1]+=w*sd*.25*win(a,.1,.2,.34,.6);
}

// ---------------------------------------------------------------------------------------------------------------
// Emergence: e = seconds since spawnAt. The body starts under the floor (the floor hides it), claws up and climbs out.
const EMERGE={bodyY:[[0,-2.05],[.2,-1.85],[.5,-1.3],[.75,-.97],[.9,-.82],[1.15,-.62],[1.42,-.38],[1.68,-.1],[1.95,-.07]],bodyZ:[[0,0],[.8,-.1],[1.15,-.18],[1.45,-.05],[1.95,0]],
 bodyX:[[0,-.15],[.6,-.12],[.9,.3],[1.15,.45],[1.42,.3],[1.68,.1],[1.95,.04]],spine:[[0,-.2],[.6,-.12],[.86,.25],[1.15,.36],[1.42,.3],[1.95,.2]],
 head:[[0,-.6],[.58,-.5],[.82,.2],[1.15,.32],[1.42,-.1],[1.62,.22],[1.76,-.3],[1.95,.1]],armUp:[[.2,-2.95],[.55,-3.05],[.72,-2.5],[.9,-1.4]],
 hip:[[0,-1.25],[.82,-1.4],[1.2,-1.55],[1.42,-.9],[1.6,-.3]],knee:[[0,1.7],[.82,1.9],[1.2,2.0],[1.42,1.3],[1.6,.5]]} as const;
function emergePose(P:Pose,look:MinionLook,e:number,floor:number,tAbs:number){
 P.fill(0);P[BODY+1]=keyed(e,EMERGE.bodyY);P[BODY+2]=keyed(e,EMERGE.bodyZ);P[J.body]=keyed(e,EMERGE.bodyX);
 P[J.spine]=keyed(e,EMERGE.spine);P[J.chest]=.15;P[J.neck]=-.1;P[J.head]=keyed(e,EMERGE.head);P[J.head+2]=look.headTilt*sm((e-1.2)/.6);
 for(const sd of [1,-1]){
  // Arms: thrust straight up out of the pool, clawing alternately, then reach down to the floor.
  const claw=.25*Math.sin(e*14+sd*1.3)*win(e,.3,.4,.7,.8);
  P[side(J.lArm,sd)]=keyed(e,EMERGE.armUp)+claw;P[side(J.lArm,sd)+2]=sd*.12;P[side(J.lElbow,sd)]=-.15-.4*win(e,.45,.55,.62,.72)*(sd>0?1:.6);P[side(J.lHand,sd)]=-.3;
  // Hands slam on the floor and haul (IK).
  const hw=win(e,.78,.9,1.36,1.62),h=HAND+(sd>0?0:5);_v.set(sd*(.3+.04*look.limp),floor+.07,.32-.12*sm((e-.9)/.5));P[h]=_v.x;P[h+1]=_v.y;P[h+2]=_v.z;P[h+3]=-.9;P[h+4]=hw;
  // Legs: folded under the floor, then a knee comes up and steps out (the gait takes over through foot IK).
  const lead=sd===-look.drag,delay=lead?0:.14;P[side(J.lHip,sd)]=keyed(e-delay,EMERGE.hip);P[side(J.lKnee,sd)]=keyed(e-delay,EMERGE.knee);P[side(J.lHip,sd)+2]=sd*.08;
  const o=FOOT+(sd>0?0:6),fw=sm((e-1.38-delay)/.3);P[o]=sd*.15;P[o+1]=ANKLE+.12*win(e-delay,1.36,1.46,1.5,1.66);P[o+2]=lead?.12:-.02;P[o+3]=sd*.12;P[o+4]=0;P[o+5]=fw;
 }
 // It jerks awake as it stands.
 addTwitch(P,look,tAbs,sm((e-1.5)/.3));
}

// ---------------------------------------------------------------------------------------------------------------
// Vent climb (PvE), keyed on PVE_TIMING.vent; `a` = seconds into the first add's timeline (followers start at vent.push,
// the grate already open). The standing point is the rig origin; the rim edge is the line z=rimZ behind it, the shaft
// beyond. 0-push: the hands rise under the grate and shove it (hidden under the floor); push-push+open: the grate
// swings and the hands grip the rim; then the haul: head and shoulders rise, a knee comes over the rim, it stands and
// jerks awake. The body origin starts 1.4 m under the floor.
// bodyY/bodyZ are shared with the authority's entering hitbox (shared/pve/entering.ts).
const CLIMB={bodyY:VENT_CLIMB_BODY.y,bodyZ:VENT_CLIMB_BODY.z,pitch:[[0,-.05],[.8,0],[1.05,.35],[1.3,.62],[1.5,.55],[1.72,.25],[1.95,.04]],
 spine:[[0,-.15],[.35,-.2],[.8,0],[1.1,.3],[1.5,.25],[1.95,.2]],head:[[0,-.6],[.35,-.75],[.6,-.2],[.8,.25],[1.05,.5],[1.3,.15],[1.55,-.2],[1.72,.25],[1.95,.1]],
 hip:[[0,-.1],[1.05,-.2],[1.3,-1.1],[1.5,-1.55],[1.72,-.8],[1.95,-.2]],knee:[[0,.35],[1.05,.6],[1.3,1.7],[1.5,1.9],[1.72,1.0],[1.95,.3]]} as const;
function ventClimbPose(P:Pose,look:MinionLook,a:number,floor:number,rimZ:number,tAbs:number){
 const V=PVE_TIMING.vent,grip=V.push+V.open;
 P.fill(0);P[BODY+1]=keyed(a,CLIMB.bodyY);P[BODY+2]=(rimZ-.17)*keyed(a,CLIMB.bodyZ)/-.55;P[J.body]=keyed(a,CLIMB.pitch);
 P[J.spine]=keyed(a,CLIMB.spine);P[J.chest]=.1;P[J.neck]=-.1;P[J.head]=keyed(a,CLIMB.head);P[J.head+2]=look.headTilt*sm((a-1.2)/.6);
 for(const sd of [1,-1]){
  const h=HAND+(sd>0?0:5),x=sd*(.27+.03*look.limp);
  // Under the grate: palms up, pushing; then onto the rim edge: fingers over it, the grip held through the haul.
  const up=sm(a/V.push),onto=sm((a-V.push)/.12);
  P[h]=x;P[h+1]=lerp(floor-.55+.53*up,floor+.035,onto);P[h+2]=lerp(rimZ-.07,rimZ+.05,onto);P[h+3]=lerp(.6,-1.1,onto);P[h+4]=1-sm((a-1.45-(sd>0?0:.08))/.22);
  P[side(J.lArm,sd)]=-2.9+1.3*sm((a-.9)/.6);P[side(J.lArm,sd)+2]=sd*.15;P[side(J.lElbow,sd)]=-.4;P[side(J.lHand,sd)]=-.2;
  // Legs hang in the shaft, then the lead knee comes up over the rim and the other follows; the feet take over by IK.
  const lead=sd===-look.drag,delay=lead?0:.16;P[side(J.lHip,sd)]=keyed(a-delay,CLIMB.hip);P[side(J.lKnee,sd)]=keyed(a-delay,CLIMB.knee);P[side(J.lHip,sd)+2]=sd*.08;
  const o=FOOT+(sd>0?0:6),fw=sm((a-1.55-delay)/.3);P[o]=sd*.15;P[o+1]=ANKLE+.1*win(a-delay,1.5,1.6,1.65,1.8);P[o+2]=lead?.1:-.03;P[o+3]=sd*.12;P[o+4]=0;P[o+5]=fw;
 }
 // Hand contact shoves the grate: a jolt through the shoulders.
 const jolt=win(a,V.push-.04,V.push,V.push+.04,V.push+.2);P[J.spine]-=.12*jolt;P[BODY+1]-=.04*jolt;
 addTwitch(P,look,tAbs,sm((a-1.6)/.3));
}
// Vent dive: d = seconds since diveAt. It crouches and pulls the grate up, grips the rim, drops in feet first; gone at vent.dive.
const DIVE={bodyY:[[0,-.07],[.18,-.3],[.4,-.42],[.55,-.55],[.72,-1.15],[.9,-2.1]],pitch:[[0,.05],[.18,.55],[.4,.75],[.55,.45],[.72,.2],[.9,.1]],
 hip:[[0,-.2],[.18,-1.2],[.45,-1.45],[.6,-.6],[.9,-.1]],knee:[[0,.3],[.18,1.6],[.45,1.9],[.6,.9],[.9,.3]]} as const;
function ventDivePose(P:Pose,look:MinionLook,d:number,floor:number){
 P.fill(0);P[BODY+1]=keyed(d,DIVE.bodyY);P[BODY+2]=-.05;P[J.body]=keyed(d,DIVE.pitch);P[J.spine]=.3;P[J.neck]=-.2;P[J.head]=.3-.4*sm((d-.5)/.3);P[J.head+2]=look.headTilt;
 for(const sd of [1,-1]){
  const h=HAND+(sd>0?0:5),pull=win(d,.08,.2,.3,.42);
  P[h]=sd*.28;P[h+1]=floor+.04+.35*pull;P[h+2]=.42-.08*sm((d-.45)/.2);P[h+3]=-1.0;P[h+4]=win(d,.02,.14,.68,.8);
  P[side(J.lArm,sd)]=-1.4-1.2*sm((d-.5)/.4);P[side(J.lArm,sd)+2]=sd*.15;P[side(J.lElbow,sd)]=-.5;
  P[side(J.lHip,sd)]=keyed(d,DIVE.hip);P[side(J.lKnee,sd)]=keyed(d,DIVE.knee);P[side(J.lHip,sd)+2]=sd*.1;
  const o=FOOT+(sd>0?0:6);P[o]=sd*.15;P[o+1]=ANKLE;P[o+2]=.02;P[o+3]=sd*.1;P[o+5]=1-sm((d-.26)/.16);
 }
}
// Door and cell walk-out: hunched and dormant behind the threshold, it jerks awake (MINION_TIMING.wake) just before its
// walk starts and stagger-walks out (the authority moves it; the walk velocity comes from the entry's geometry).
function dormantPose(P:Pose,look:MinionLook,t:number){
 locomotion(P,look,t,0,0,0,1,0);P[J.spine]+=.25;P[J.neck]+=.2;P[J.head]+=.55;P[J.head+2]=look.headTilt*1.4;
 for(const sd of [1,-1]){P[side(J.lArm,sd)]=.08;P[side(J.lArm,sd)+2]=sd*.06;P[side(J.lElbow,sd)]=-.12;}
 P[BODY+1]-=.03;
}
/** Seconds after `at` when a door/cell add starts walking (first add: after the leaves / gate open). */
export const entryWalkStart=(e:MinionEntry)=>e.kind==='vent'||!e.first?0:(e.kind==='door'?PVE_TIMING.door.open:PVE_TIMING.cell.open);
/** Total entry duration (seconds after `at` until the add is a normal, active add). */
export function entryDuration(e:MinionEntry){
 if(e.kind==='vent')return PVE_TIMING.vent.climb-(e.first?0:PVE_TIMING.vent.push);
 return entryWalkStart(e)+(e.kind==='door'?PVE_TIMING.door.walk:PVE_TIMING.cell.walk);
}

// ---------------------------------------------------------------------------------------------------------------
// Death: d = seconds since deathAt, starting from the exact pose at deathAt (so it never pops).
const DEATH={spine:[[0,-.45],[.28,-.35],[.6,.4],[.95,.25],[1.4,.15]],chest:[[0,-.22],[.3,-.1],[.6,.3],[1.4,.05]],head:[[0,-.75],[.3,-.55],[.62,.55],[1.0,.1],[1.4,-.45]],
 bodyY:[[0,.02],[.3,-.04],[.62,-.3],[.92,-.42],[1.2,-.6],[1.45,-.64]],bodyZ:[[0,-.09],[.3,-.06],[.62,-.02],[.95,.18],[1.3,.36],[1.45,.38]],bodyX:[[0,-.12],[.3,-.08],[.62,.25],[.95,.72],[1.25,1.28],[1.45,1.36]],
 armX:[[0,-.7],[.3,-.4],[.62,.25],[.95,-.1],[1.3,.15],[1.45,.2]],armZ:[[0,.75],[.3,.6],[.62,.2],[1.3,.32]],elbow:[[0,-.15],[.3,-.4],[.62,-.6],[1.3,-.3]]} as const;
function deathPose(P:Pose,look:MinionLook,d:number){
 P[J.spine]=keyed(d,DEATH.spine);P[J.spine+1]=0;P[J.spine+2]=.15*look.drag*sm((d-.3)/.4);P[J.chest]=keyed(d,DEATH.chest);P[J.chest+1]=0;P[J.neck]=0;P[J.head]=keyed(d,DEATH.head);P[J.head+1]=.3*sm((d-.6)/.5);P[J.head+2]=look.headTilt*1.5;
 P[BODY]=0;P[BODY+1]=keyed(d,DEATH.bodyY);P[BODY+2]=keyed(d,DEATH.bodyZ);P[J.body]=keyed(d,DEATH.bodyX);P[J.body+1]=0;P[J.body+2]=-look.drag*.12*sm((d-.3)/.5);P[J.pelvis]=0;
 for(const sd of [1,-1]){
  P[side(J.lArm,sd)]=keyed(d,DEATH.armX)+(sd===look.drag?.3:0);P[side(J.lArm,sd)+1]=0;P[side(J.lArm,sd)+2]=sd*keyed(d,DEATH.armZ);P[side(J.lElbow,sd)]=keyed(d,DEATH.elbow);P[side(J.lHand,sd)]=.3;P[side(J.lClav,sd)+2]=0;
  // Knees buckle onto the floor: the feet slide back, toes down, and stay there while it topples.
  const o=FOOT+(sd>0?0:6),k=sm((d-.25)/.45);P[o]=sd*(.15+.03*k);P[o+1]=ANKLE+.14*k;P[o+2]=lerp(.02,-.45,k);P[o+3]=sd*.1;P[o+4]=lerp(0,1.0,k);P[o+5]=1;
  const h=HAND+(sd>0?0:5);P[h+4]=0;
 }
}

// ---------------------------------------------------------------------------------------------------------------
const _v=new T.Vector3(),_o=new T.Vector3(),_q=new T.Quaternion(),_qi=new T.Quaternion(),_e=new T.Euler(),_p=new T.Vector3();
const _fk=[new T.Quaternion(),new T.Quaternion(),new T.Quaternion(),new T.Quaternion()];
const POSE_A=new Float64Array(SIZE),POSE_B=new Float64Array(SIZE),POSE_C=new Float64Array(SIZE);
const CHAIN=['body','pelvis','spine','chest'] as const;
/** Chest frame in the rig group from the joints' local transforms (after the torso has been set). */
function chestFrame(r:CharacterRig){_o.set(0,0,0);_q.identity();for(const k of CHAIN){const n=r[k];_p.copy(n.position).applyQuaternion(_q);_o.add(_p);_q.multiply(n.quaternion);}_qi.copy(_q).invert();}
/** Rig-group point to the chest frame (in place). */
const toChest=(p:T.Vector3)=>p.sub(_o).applyQuaternion(_qi);

function applyPose(r:CharacterRig,P:Pose){
 r.body.position.set(P[BODY],P[BODY+1],P[BODY+2]);
 const set=(n:T.Object3D,i:number)=>n.rotation.set(P[i],P[i+1],P[i+2]);
 set(r.body,J.body);set(r.pelvis,J.pelvis);set(r.spine,J.spine);set(r.chest,J.chest);set(r.neck,J.neck);set(r.head,J.head);
 set(r.leftShoulder,J.lClav);set(r.rightShoulder,J.rClav);set(r.leftArm,J.lArm);set(r.rightArm,J.rArm);
 r.leftElbow.rotation.set(Math.min(0,P[J.lElbow]),0,0);r.rightElbow.rotation.set(Math.min(0,P[J.rElbow]),0,0);set(r.leftHand,J.lHand);set(r.rightHand,J.rHand);
 r.chest.position.set(0,.19,0);r.head.position.set(0,.10,0);r.weapon.position.set(0,0,0);r.weapon.quaternion.identity();r.offWeapon.position.set(0,0,0);r.offWeapon.quaternion.identity();
 // Legs: forward kinematics first (emergence, death), then foot IK blended in by weight.
 for(const sd of [1,-1]){const hip=sd>0?r.leftLeg:r.rightLeg,knee=sd>0?r.leftKnee:r.rightKnee,ankle=sd>0?r.leftFoot:r.rightFoot,toe=sd>0?r.leftToe:r.rightToe,o=FOOT+(sd>0?0:6);
  set(hip,side(J.lHip,sd));knee.rotation.set(Math.max(0,P[side(J.lKnee,sd)]),0,0);ankle.rotation.set(0,0,0);toe.rotation.set(0,0,0);
  // A partial weight blends the solved leg from the FK leg joint by joint (a weighted IK target would re-hinge the knee).
  const w=Math.min(1,P[o+5]);if(w<=1e-3)continue;if(w<1){_fk[0].copy(hip.quaternion);_fk[1].copy(knee.quaternion);_fk[2].copy(ankle.quaternion);_fk[3].copy(toe.quaternion);}
  foot(r,sd>0,_v.set(P[o],P[o+1],P[o+2]),P[o+3],P[o+4],1);
  if(w<1){hip.quaternion.copy(_fk[0].slerp(hip.quaternion,w));knee.quaternion.copy(_fk[1].slerp(knee.quaternion,w));ankle.quaternion.copy(_fk[2].slerp(ankle.quaternion,w));toe.quaternion.copy(_fk[3].slerp(toe.quaternion,w));}}
 // Hands: IK onto rig-group targets (grabs, floor plants).
 let framed=false;
 for(const sd of [1,-1]){const h=HAND+(sd>0?0:5),w=P[h+4];if(w<=1e-3)continue;if(!framed){chestFrame(r);framed=true;}
  _v.set(P[h],P[h+1],P[h+2]);toChest(_v);hand(r,sd>0,_v,_e.set(P[h+3],0,0),Math.min(1,w));
  // The wrist keeps its pose relative to the forearm (a chest-space wrist target flips through 180 degrees when a
  // clawing arm folds past the shoulder).
  set(sd>0?r.leftHand:r.rightHand,side(J.lHand,sd));}
 settleJointCovers(r);
}

/**
 * The only presentation state: each minion's gait phase (one cycle that blends walk into run), integrated with the speed-dependent
 * cadence like the player's distance-driven gait. Deterministic; dt clamped to .05 s; restarted from a pure phase when
 * time runs backward or jumps more than .35 s (kill-cam rewinds, late snapshots). Frozen at death, with the last living
 * velocity, so the death starts from the exact stride the minion was in.
 */
interface Swing {at:number;attack?:MinionAttack;side?:1|-1}
interface Gait {t:number;walk:number;vx:number;vz:number;f:number;yaw:number;wy:number;cur?:Swing;prev?:Swing}
const gaits=new WeakMap<object,Gait>();
function gaitAt(m:SirenMinion,s:SirenMinionView,time:number,alive:boolean):Gait{
 const L=m.look,cy=Math.cos(s.yaw),sy=Math.sin(s.yaw),vx=s.vel.x*cy-s.vel.z*sy,vz=s.vel.x*sy+s.vel.z*cy;let g=gaits.get(m);const step=g?time-g.t:NaN;
 const f=s.crawl?crawlCadence(Math.hypot(vx,vz)):gaitCadence(L,Math.hypot(vx,vz));
 if(!g||!(step>=0)||step>.35){g={t:time,walk:time*L.cadence+L.phase,vx:alive?vx:0,vz:alive?vz:0,f,yaw:s.yaw,wy:0};gaits.set(m,g);return g;}
 g.t=time;if(!alive)return g;
 // Trapezoidal steps (this cadence and the last), so a timestamp gives the same stride at 30, 60 and 120 Hz after a
 // speed change; the yaw rate (wrapped, smoothed over ~.06 s) keeps planted feet put while the minion turns.
 const dt=Math.min(.05,step);g.walk+=dt*.5*(g.f+f);g.f=f;
 if(step>0){const turn=Math.atan2(Math.sin(s.yaw-g.yaw),Math.cos(s.yaw-g.yaw))/step;g.wy+=(Math.max(-8,Math.min(8,turn))-g.wy)*(1-Math.exp(-step/.06));}
 g.yaw=s.yaw;g.vx=vx;g.vz=vz;return g;
}
const PREV:SirenMinionView={yaw:0,vel:{x:0,z:0}},POSE_U=new Float64Array(SIZE),POSE_K=new Float64Array(SIZE);
/** Remembers the attack before the current one, so a new swing that starts before the last has faded (alternating
 * swipes) layers over its tail instead of cutting it off. Reset with the gait on rewinds. */
function swings(g:Gait,s:SirenMinionView){
 if(s.attackAt===undefined)return;
 if(!g.cur||g.cur.at!==s.attackAt){if(g.cur&&g.cur.at<s.attackAt)g.prev=g.cur;g.cur={at:s.attackAt,attack:s.attack,side:s.side};}
}
function attacks(P:Pose,L:MinionLook,s:SirenMinionView,t:number,g:Gait,crawl:boolean,floor=0){
 const layer=(v:SirenMinionView,at:number)=>{const a=t-at;if(a>=0&&a<MINION_TIMING.attack){if(crawl)crawlAttack(P,v,a,floor);else attackLayer(P,L,v,a);}};
 const p=g.prev;if(p&&s.attackAt!==undefined&&p.at<s.attackAt&&t-p.at<MINION_TIMING.attack){PREV.yaw=s.yaw;PREV.lost=s.lost;PREV.attack=p.attack;PREV.side=p.side;PREV.attackAt=p.at;layer(PREV,p.at);}
 if(s.attackAt!==undefined)layer(s,s.attackAt);
}
/** Locomotion (or the crawl) + attack + flinch + twitches at time t (the living pose). */
function livingPose(P:Pose,m:SirenMinion,s:SirenMinionView,t:number,g:Gait){
 const L=m.look;swings(g,s);
 // Badly hurt (below 40% health) it sags lower and its head hangs.
 const injured=s.maxHp&&s.hp!==undefined?1-sm(s.hp/s.maxHp/.4):0;
 if(s.crawl){
  crawlPose(P,L,t,g.vx,g.vz,g.walk,m.floor);
  const k=s.crawlAt!==undefined?sm((t-s.crawlAt)/MINION_TIMING.fall):1;
  if(k<1){POSE_K.set(P);uprightPose(POSE_U,m,s,t,g,injured);blendPose(P,POSE_U,POSE_K,k);
   // The feet let go of the floor early in the fall (a planted foot under a body that pitches flat would flip the leg).
   const fw=Math.max(0,1-k*3);for(const o of [FOOT,FOOT+6]){for(let j=0;j<5;j++)P[o+j]=POSE_U[o+j];P[o+5]=POSE_U[o+5]*fw;}}
  attacks(P,L,s,t,g,true,m.floor);P[J.head+2]+=.5*twitch(L,t).head;
 }else uprightPose(P,m,s,t,g,injured);
 if(s.hitAt!==undefined)flinchLayer(P,L,s,t-s.hitAt);
}
function uprightPose(P:Pose,m:SirenMinion,s:SirenMinionView,t:number,g:Gait,injured:number){
 const L=m.look;P.fill(0);locomotion(P,L,t,g.vx,g.vz,g.walk,injured,g.wy,stumpLeg(s.lost));addTwitch(P,L,t);P[J.head]+=.15*injured;
 attacks(P,L,s,t,g,false);
}
const POINTS=[new T.Vector3(),new T.Vector3(),new T.Vector3(),new T.Vector3(),new T.Vector3(),new T.Vector3()];
/** Rig-group positions of a few joints (death flames rise from them). */
function bodyPoints(r:CharacterRig){const list=[r.head,r.chest,r.pelvis,r.leftKnee,r.rightKnee,r.leftElbow];list.forEach((n,i)=>{const p=POINTS[i].set(0,0,0);for(let o:T.Object3D|null=n;o&&o!==r.group;o=o.parent){p.applyQuaternion(o.quaternion).add(o.position);}});return POINTS;}

const GROUND=/^(left-|right-)(kneecap|knee-joint)$|^(ribcage|upper-chest|collar|abdomen-shell|blank-head|crown|jaw)$/,grounded=new WeakMap<object,T.Mesh[]>(),_c=new T.Vector3();
/** Lowest rig-group height of the parts a fallen body rests on (knees, chest, head): unit-box corners up the joint chain. */
function restingLow(m:SirenMinion){
 let list=grounded.get(m);if(!list){const found:T.Mesh[]=[];m.rig.group.traverse(o=>{if(o instanceof T.Mesh&&GROUND.test(o.name))found.push(o);});grounded.set(m,list=found);}
 let low=Infinity;for(const mesh of list){let shown=mesh.visible;for(let o=mesh.parent;shown&&o&&o!==m.rig.group;o=o.parent)shown=o.visible;if(!shown)continue;for(let i=0;i<8;i++){_c.set(i&1?.5:-.5,i&2?.5:-.5,i&4?.5:-.5).multiply(mesh.scale).applyQuaternion(mesh.quaternion).add(mesh.position);
  for(let o=mesh.parent;o&&o!==m.rig.group;o=o.parent)_c.multiply(o.scale).applyQuaternion(o.quaternion).add(o.position);low=Math.min(low,_c.y);}}
 return low;
}
const SCRAP_TIP={sword:new T.Vector3(0,0,.47),pistol:new T.Vector3(0,-.1,.32),pipe:new T.Vector3(0,0,.565)},UP=new T.Vector3(0,1,0);
const _h=new T.Vector3(),_t=new T.Vector3(),_ax=new T.Vector3(),_pq=new T.Quaternion(),_rq=new T.Quaternion();
/** Rig-group position of a point given in `node`'s frame (in place), walking the joint chain up to the group. */
function toGroup(m:SirenMinion,node:T.Object3D,p:T.Vector3){for(let o:T.Object3D|null=node;o&&o!==m.rig.group;o=o.parent)p.multiply(o.scale).applyQuaternion(o.quaternion).add(o.position);return p;}
/** The scrap never spears the floor: its tip swings up in its own vertical plane until it rests on the floor (a pipe
 * dragged along, a sword lying flat once the body is down). A pure function of the pose, like the rest. */
function restScrap(m:SirenMinion){
 const w=m.rig.weapon,tip=SCRAP_TIP[m.look.scrap as keyof typeof SCRAP_TIP];if(!tip)return;
 toGroup(m,w,_h.set(0,0,0));toGroup(m,w,_t.copy(tip)).sub(_h);const len=_t.length();if(len<1e-4)return;
 const want=Math.min(.95,(m.floor+.03-_h.y)/len),have=_t.y/len;if(have>=want||want<=-1)return;
 _ax.crossVectors(_t,UP);if(_ax.lengthSq()<1e-8)return;_ax.normalize();
 _pq.identity();for(let o=w.parent;o&&o!==m.rig.group;o=o.parent)_pq.premultiply(o.quaternion);
 _rq.setFromAxisAngle(_ax,Math.asin(want)-Math.asin(Math.max(-1,have)));
 // In the socket's parent frame: Q^-1 R Q, applied before the socket's own rotation.
 w.quaternion.premultiply(_pq.clone().invert().multiply(_rq).multiply(_pq));
}
/**
 * Poses a minion at authority time `time` (call after setting rig.group's position to the minion's pos and rotation.y to
 * its yaw). Writes every joint, the tongues, the effects and the dying look. `quality` < .5 halves the effect cubes.
 */
/** Seconds into the entry (vents: on the first add's timeline), or undefined when the entry is over or absent. */
function entryAge(s:SirenMinionView,time:number){
 const en=s.entry;if(!en)return undefined;const age=time-en.at;if(age>=entryDuration(en))return undefined;
 return Math.max(0,age)+(en.kind==='vent'&&!en.first?PVE_TIMING.vent.push:0);
}
const POSE_E=new Float64Array(SIZE),POSE_F=new Float64Array(SIZE);
/** The pose before death: entering (vent, door, cell), emerging from the floor, diving into a vent, or living. */
function basePose(out:Pose,m:SirenMinion,s:SirenMinionView,time:number,g:Gait):Pose{
 const en=s.entry,ea=entryAge(s,time);
 if(s.diveAt!==undefined&&time>=s.diveAt){
  const d=time-s.diveAt;ventDivePose(POSE_F,m.look,d,m.floor);if(d>=.2){out.set(POSE_F);return out;}
  livingPose(POSE_E,m,s,s.diveAt,g);return blendPose(out,POSE_E,POSE_F,sm(d/.2));
 }
 if(en&&ea!==undefined){
  if(en.kind==='vent'){
   ventClimbPose(POSE_F,m.look,ea,m.floor,en.rimZ??VENT_RIM_Z,time);const w=sm((ea-1.62)/(PVE_TIMING.vent.climb-1.62));
   if(w<=0){out.set(POSE_F);return out;}livingPose(POSE_E,m,s,time,g);return blendPose(out,POSE_F,POSE_E,w);
  }
  // Doors and cells: dormant until it jerks awake, just before its walk starts.
  const wake=entryWalkStart(en)-MINION_TIMING.wake,age=time-en.at,k=sm((age-wake)/MINION_TIMING.wake);
  livingPose(POSE_E,m,s,time,g);if(k<1){dormantPose(POSE_F,m.look,time);blendPose(out,POSE_F,POSE_E,k);}else out.set(POSE_E);
  const j=win(age-wake,0,.05,.1,.42);
  if(j>0){out[J.head]-=.7*j;out[J.head+2]+=.4*j*(m.look.headTilt>0?-1:1);out[J.spine]-=.25*j;for(const sd of [1,-1]){out[side(J.lArm,sd)+2]+=sd*.6*j;out[side(J.lElbow,sd)]-=.5*j;out[side(J.lClav,sd)+2]+=sd*.15*j;}}
  return out;
 }
 const e=!en&&s.spawnAt!==undefined?time-s.spawnAt:Infinity;
 if(e<EMERGE_SECONDS)return emergeBlend(out,m,s,time,Math.max(0,e),g);
 livingPose(out,m,s,time,g);return out;
}
/** A crawler dies where it lies: the head drops, the arms go slack, it sags flat. */
function crawlDeath(P:Pose,d:number){const k=sm(d/.6);P[J.head]+=.7*k;P[J.neck]+=.4*k;P[BODY+1]-=.07*k;P[J.body]+=.12*k;for(const sd of [1,-1]){const h=HAND+(sd>0?0:5);P[h+1]-=.02*k;P[h+4]*=1-.4*k;P[side(J.lArm,sd)+2]+=sd*.3*k;}}
/** A door or cell walk-out starts from a standstill: its walk velocity ramps in over the first .45 s (the authority's
 * velocity steps from 0 to the walk speed), so the legs speed up instead of snapping into the stride. */
function rampedEntry(s:SirenMinionView,time:number):SirenMinionView{
 const en=s.entry;if(!en||en.kind==='vent')return s;const a=time-en.at-entryWalkStart(en);if(a>=.45)return s;const k=sm(a/.45);
 return {...s,vel:{x:s.vel.x*k,z:s.vel.z*k}};
}
export function animateSirenMinion(m:SirenMinion,view:SirenMinionView,time:number,quality=1){
 const r=m.rig,L=m.look,s=rampedEntry(view,time);tickMinionClock(time);tickSirenClock(time);
 applyLostLimbs(m,s.lost??0);
 const d=s.deathAt!==undefined&&time>=s.deathAt?time-s.deathAt:undefined;
 const g=gaitAt(m,s,time,d===undefined);let P:Pose;
 if(d!==undefined){
  // The pose at deathAt (living, entering, emerging or diving), blended into the death keys over .2 s.
  basePose(POSE_A,m,s,s.deathAt!,g);POSE_B.set(POSE_A);
  if(s.crawl)crawlDeath(POSE_B,d);else deathPose(POSE_B,L,d);P=blendPose(POSE_C,POSE_A,POSE_B,sm(d/.2));
 }else P=basePose(POSE_C,m,s,time,g);
 applyPose(r,P);
 // A falling body rests on the floor: lift it by whatever a knee, the chest or the head would push through, then re-solve the legs.
 if(d!==undefined){const sink=m.floor-restingLow(m);if(sink>0){P[BODY+1]+=sink;applyPose(r,P);}}
 const e=s.entry?(s.entry.kind==='vent'?time-s.entry.at+(s.entry.first?0:PVE_TIMING.vent.push):Infinity):s.spawnAt!==undefined?time-s.spawnAt:Infinity,emerging=e<EMERGE_SECONDS;
 if(m.scrap&&(d!==undefined||!emerging&&!(s.diveAt!==undefined&&time>=s.diveAt)))restScrap(m);
 // Tongues flicker, flare while emerging and as the body burns.
 // Flaring flames grow wider as well as taller, so they stay flames instead of turning into spikes.
 const flare=1+.5*win(e,.2,.6,1.6,EMERGE_SECONDS)+(d!==undefined?.7*win(d,.3,.8,1.6,2.1):0),wide=1.25*(1+.45*(flare-1));
 for(const g of m.tongues){const n=.5+.5*Math.sin(time*7.3+g.phase)*Math.sin(time*3.1+g.phase*1.9);g.mesh.scale.set(wide,1.25*g.height*(.65+.55*n)*flare,wide);
  _e.set(.18*Math.sin(time*2.6+g.phase),0,.26*Math.sin(time*3.4+g.phase*1.3));g.mesh.quaternion.copy(g.base).multiply(_q.setFromEuler(_e));}
 dyingLook(m,d,s.diveAt!==undefined&&time>=s.diveAt+PVE_TIMING.vent.dive);
 m.fx.update(e<EMERGE_SECONDS+.4?e:undefined,d,d!==undefined?bodyPoints(r):POINTS,quality);
}
function emergeBlend(out:Pose,m:SirenMinion,s:SirenMinionView,time:number,e:number,g:Gait){
 emergePose(POSE_B,m.look,e,m.floor,time);const w=sm((e-1.62)/(EMERGE_SECONDS-1.62));
 if(w<=0){out.set(POSE_B);return out;}
 livingPose(POSE_A,m,s,time,g);
 // The gait's feet take over through the IK weight; the folded legs fade out with it.
 return blendPose(out,POSE_B,POSE_A,w);
}

/** Char the body part by part, then burn it away feet first; the head goes last. Restores everything while alive. */
function dyingLook(m:SirenMinion,d:number|undefined,dived=false){
 const r=m.rig,parts=m.parts;let lo=Infinity,hi=-Infinity;for(const p of parts){lo=Math.min(lo,p.y);hi=Math.max(hi,p.y);}
 const gone=d!==undefined&&d>=DEATH_SECONDS||dived;r.body.visible=!gone;
 parts.forEach((p,i)=>{
  const mat=d!==undefined&&d>.42+.7*hash01(m.seed,i,61)?p.charred:p.live;if(p.mesh.material!==mat)p.mesh.material=mat;
  const vanish=d===undefined?0:sm((d-(1.25+.8*(p.y-lo)/Math.max(1e-6,hi-lo)))/.3);
  if(vanish>0)p.mesh.scale.copy(p.scale).multiplyScalar(Math.max(.001,1-vanish));else if(!p.mesh.scale.equals(p.scale))p.mesh.scale.copy(p.scale);});
 m.tongues.forEach((g,i)=>{g.mesh.visible=!gone&&!(m.low&&i>=2);});if(m.scrap)m.scrap.visible=!(d!==undefined&&d>1.5);
}
