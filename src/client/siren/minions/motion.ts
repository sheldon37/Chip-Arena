import * as T from 'three';
import {settleJointCovers,type CharacterRig} from '../../character-rig.js';
import {foot,hand} from '../../rig-ik.js';
import {tickSirenClock} from '../materials.js';
import {tickMinionClock} from './materials.js';
import {hash01,type MinionLook} from './look.js';
import {EMERGE_SECONDS,DEATH_SECONDS,DEATH_FX_SECONDS} from './fx.js';
import type {SirenMinion} from './model.js';

/**
 * Siren minion animation set. Every pose is a function of the authoritative fields a future minion view provides
 * (docs/SIREN-MINIONS.md: yaw, vel, spawnAt, attackAt, hitAt, deathAt), the seed and the time, plus ONE small state per
 * minion: its gait phases (the cadence follows speed, like the player's gait; see gaitAt). It never draws random numbers,
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
export type MinionAttack='lunge'|'club'|'smash';
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
}
/** Presentation timing the systems phase should match (seconds). `hit` is when each attack's claws/club arrive after
 * attackAt (where the authority should apply damage); `attack` is the full cycle (the chaos zombie's .9 s cooldown). */
export const MINION_TIMING={emerge:EMERGE_SECONDS,death:DEATH_SECONDS,deathFx:DEATH_FX_SECONDS,attack:.9,hit:{lunge:.34,club:.4,smash:.43},flinch:.42,runFrom:2.0,runTo:3.0} as const;

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
function locomotion(P:Pose,look:MinionLook,t:number,vx:number,vz:number,phi:number,phr:number,injured=0){
 const speed=Math.hypot(vx,vz),mv=sm((speed-.08)/.4),run=sm((speed-MINION_TIMING.runFrom)/(MINION_TIMING.runTo-MINION_TIMING.runFrom)),lead=-look.drag;
 const fw=walkCadence(look,speed),fr=runCadence(look,speed);
 // Feet (walk and run evaluated separately, then mixed).
 for(const sd of [1,-1]){
  const drag=sd===look.drag,o=FOOT+(sd>0?0:6);
  // Walk: the good leg plants for longer; the dragged leg scuffs forward low with its toe down and swings out.
  let c=drag?.56:.66,f=fw,ph=frac(phi+(drag?.5+.05*look.limp:0)),travel=Math.min(1,.46/Math.max(1e-6,speed*c/f));
  let hx=sd*.15,hz=drag?-.05*look.limp:.02,x:number,z:number,y=ANKLE,heel=0,yaw=sd*(drag?.28:.1);
  const span=(c/f)*travel;
  if(ph<c){const u=ph/c;x=hx-vx*span*(u-.5);z=hz-vz*span*(u-.5);}
  else{const w=(ph-c)/(1-c),e=w*w*(3-2*w),arc=Math.sin(Math.PI*w);x=hx+vx*span*(e-.5);z=hz+vz*span*(e-.5);
   if(drag){y+=(.034+.012*(1-look.limp))*arc*mv;heel=.24*arc*mv;x+=sd*.035*arc*look.limp*mv;}
   else{y+=(.05+.05*Math.min(1,speed/1.5))*Math.pow(arc,.8)*mv;heel=.28*arc*(1-2*w)*mv;}}
  // Run: frenzied, short contact, high knees, the bad leg still a little behind.
  c=drag?.38:.42;f=fr;ph=frac(phr+(drag?.5+.03*look.limp:0));travel=Math.min(1,.52/Math.max(1e-6,speed*c/f));const rspan=(c/f)*travel;
  let rx:number,rz:number,ry=ANKLE,rheel=0;hx=sd*.12;hz=.04;
  if(ph<c){const u=ph/c;rx=hx-vx*rspan*(u-.5);rz=hz-vz*rspan*(u-.5);}
  else{const w=(ph-c)/(1-c),e=w*w*(3-2*w),arc=Math.sin(Math.PI*w);rx=hx+vx*rspan*(e-.5);rz=hz+vz*rspan*(e-.5)+.06*arc;ry+=(drag?.11:.19)*Math.pow(arc,.7);const late=sm((w-.25)/.4);rheel=(.55*(1-late)-.25*late)*arc;}
  P[o]=lerp(x,rx,run);P[o+1]=lerp(y,ry,run);P[o+2]=lerp(z,rz,run);P[o+3]=lerp(yaw,sd*.06,run);P[o+4]=lerp(heel,rheel,run);P[o+5]=1;
 }
 // Body: hunched and lowered; dips on the bad leg, sways over the planted foot, rolls toward the bad side.
 const hunch=(look.hunch+.18*injured)*(1+.35*run),dragStance=frac(phi+.5+.05*look.limp),dip=-.035*look.limp*Math.sin(Math.PI*Math.min(1,dragStance/.56))*mv;
 const idle=1-mv,drift=Math.sin(t*.83+look.phase*6)+.4*Math.sin(t*1.37+look.phase*3),sway=.046*mv*Math.cos(TAU*(phi-.3))*lead*(1+.5*look.limp)+.026*idle*drift,bob=-.02*(.5+.5*Math.cos(2*TAU*phi))*mv;
 const rbob=-.04*(.5+.5*Math.cos(2*TAU*phr));
 P[BODY]=lerp(sway,.012*Math.sin(TAU*phr),run);P[BODY+1]=lerp(-.075-.05*look.hunch+bob+dip,-.085+rbob,run);P[BODY+2]=lerp(.0,.04,run);
 P[J.body]=lerp(.04*mv,.32,run);P[J.body+1]=lerp(.1*Math.sin(TAU*phi)*mv,.16*Math.sin(TAU*phr),run);P[J.body+2]=lerp(-look.drag*.05*look.limp-sway*2.2,.06*Math.sin(TAU*phr),run);
 // A lurch: the torso pitches forward each time the good leg plants.
 const lurch=.14*mv*Math.max(0,Math.sin(TAU*phi))**3+.05*idle*(.5+.5*Math.sin(t*.61+look.phase*4));
 P[J.pelvis]=.04;P[J.pelvis+1]=-.06*Math.sin(TAU*phi)*mv;P[J.pelvis+2]=0;
 const breath=.025*Math.sin(t*1.7+look.phase*6)+.012*noise(look.seed,t*.9,1);
 P[J.spine]=hunch*.5+lurch;P[J.spine+1]=0;P[J.spine+2]=.4*sway;
 P[J.chest]=hunch*.5+breath;P[J.chest+1]=lerp(-.08*Math.sin(TAU*phi)*mv,-.14*Math.sin(TAU*phr),run)+.05*noise(look.seed,t*.4,2);P[J.chest+2]=look.shoulderDrop;
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
  const rs=TAU*phr+(sd>0?0:Math.PI),fl=noise(look.seed,t*3.1,20+sd);
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
export function attackKind(look:MinionLook,s:SirenMinionView):MinionAttack{return s.attack??(look.scrap!=='none'?'club':hash01(look.seed,Math.round((s.attackAt||0)*20),31)<.65?'lunge':'smash');}
function attackLayer(P:Pose,look:MinionLook,s:SirenMinionView,a:number){
 const kind=attackKind(look,s),w=win(a,0,.16,.64,.9);if(w<=0)return;
 if(kind==='lunge'){
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
// Death: d = seconds since deathAt, starting from the exact pose at deathAt (so it never pops).
const DEATH={spine:[[0,-.45],[.28,-.35],[.6,.4],[.95,.25],[1.4,.15]],chest:[[0,-.22],[.3,-.1],[.6,.3],[1.4,.2]],head:[[0,-.75],[.3,-.55],[.62,.55],[1.0,.35],[1.4,.4]],
 bodyY:[[0,.02],[.3,-.04],[.62,-.3],[.92,-.42],[1.2,-.6],[1.45,-.64]],bodyZ:[[0,-.09],[.3,-.06],[.62,-.02],[.95,.18],[1.3,.36],[1.45,.38]],bodyX:[[0,-.12],[.3,-.08],[.62,.25],[.95,.72],[1.25,1.28],[1.45,1.36]],
 armX:[[0,-.7],[.3,-.4],[.62,.25],[.95,-.5],[1.3,-2.2],[1.45,-2.3]],armZ:[[0,.75],[.3,.6],[.62,.2],[1.3,.32]],elbow:[[0,-.15],[.3,-.4],[.62,-.6],[1.3,-.3]]} as const;
function deathPose(P:Pose,look:MinionLook,d:number){
 P[J.spine]=keyed(d,DEATH.spine);P[J.spine+1]=0;P[J.spine+2]=.15*look.drag*sm((d-.3)/.4);P[J.chest]=keyed(d,DEATH.chest);P[J.chest+1]=0;P[J.neck]=0;P[J.head]=keyed(d,DEATH.head);P[J.head+1]=.3*sm((d-.6)/.5);P[J.head+2]=look.headTilt*1.5;
 P[BODY]=0;P[BODY+1]=keyed(d,DEATH.bodyY);P[BODY+2]=keyed(d,DEATH.bodyZ);P[J.body]=keyed(d,DEATH.bodyX);P[J.body+1]=0;P[J.body+2]=-look.drag*.12*sm((d-.3)/.5);P[J.pelvis]=0;
 for(const sd of [1,-1]){
  P[side(J.lArm,sd)]=keyed(d,DEATH.armX)+(sd===look.drag?.3:0);P[side(J.lArm,sd)+1]=0;P[side(J.lArm,sd)+2]=sd*keyed(d,DEATH.armZ);P[side(J.lElbow,sd)]=keyed(d,DEATH.elbow);P[side(J.lHand,sd)]=.3;P[side(J.lClav,sd)+2]=0;
  // Knees buckle onto the floor: the feet slide back, toes down, and stay there while it topples.
  const o=FOOT+(sd>0?0:6),k=sm((d-.25)/.45);P[o]=sd*(.15+.03*k);P[o+1]=ANKLE+.03*k;P[o+2]=lerp(.02,-.36,k);P[o+3]=sd*.1;P[o+4]=lerp(0,1.25,k);P[o+5]=1;
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
 r.chest.position.set(0,.19,0);r.head.position.set(0,.10,0);r.weapon.position.set(0,0,0);r.weapon.quaternion.identity();
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
 * The only presentation state: each minion's gait phases (walk and run cycles), integrated with the speed-dependent
 * cadence like the player's distance-driven gait. Deterministic; dt clamped to .05 s; restarted from a pure phase when
 * time runs backward or jumps more than .35 s (kill-cam rewinds, late snapshots). Frozen at death, with the last living
 * velocity, so the death starts from the exact stride the minion was in.
 */
interface Gait {t:number;walk:number;run:number;vx:number;vz:number}
const gaits=new WeakMap<object,Gait>();
function gaitAt(m:SirenMinion,s:SirenMinionView,time:number,alive:boolean):Gait{
 const L=m.look,cy=Math.cos(s.yaw),sy=Math.sin(s.yaw),vx=s.vel.x*cy-s.vel.z*sy,vz=s.vel.x*sy+s.vel.z*cy;let g=gaits.get(m);const step=g?time-g.t:NaN;
 if(!g||!(step>=0)||step>.35){g={t:time,walk:time*L.cadence+L.phase,run:time*L.runCadence+L.phase*1.7,vx:alive?vx:0,vz:alive?vz:0};gaits.set(m,g);return g;}
 g.t=time;if(!alive)return g;
 const dt=Math.min(.05,step),speed=Math.hypot(vx,vz);g.walk+=dt*walkCadence(L,speed);g.run+=dt*runCadence(L,speed);g.vx=vx;g.vz=vz;return g;
}
/** Locomotion + attack + flinch + twitches at time t (the upright, living pose). */
function livingPose(P:Pose,m:SirenMinion,s:SirenMinionView,t:number,g:Gait){
 const L=m.look;
 // Badly hurt (below 40% health) it sags lower and its head hangs.
 const injured=s.maxHp&&s.hp!==undefined?1-sm(s.hp/s.maxHp/.4):0;
 P.fill(0);locomotion(P,L,t,g.vx,g.vz,g.walk,g.run,injured);addTwitch(P,L,t);P[J.head]+=.15*injured;
 if(s.attackAt!==undefined){const a=t-s.attackAt;if(a>=0&&a<MINION_TIMING.attack)attackLayer(P,L,s,a);}
 if(s.hitAt!==undefined)flinchLayer(P,L,s,t-s.hitAt);
}
const POINTS=[new T.Vector3(),new T.Vector3(),new T.Vector3(),new T.Vector3(),new T.Vector3(),new T.Vector3()];
/** Rig-group positions of a few joints (death flames rise from them). */
function bodyPoints(r:CharacterRig){const list=[r.head,r.chest,r.pelvis,r.leftKnee,r.rightKnee,r.leftElbow];list.forEach((n,i)=>{const p=POINTS[i].set(0,0,0);for(let o:T.Object3D|null=n;o&&o!==r.group;o=o.parent){p.applyQuaternion(o.quaternion).add(o.position);}});return POINTS;}

/**
 * Poses a minion at authority time `time` (call after setting rig.group's position to the minion's pos and rotation.y to
 * its yaw). Writes every joint, the tongues, the effects and the dying look. `quality` < .5 halves the effect cubes.
 */
export function animateSirenMinion(m:SirenMinion,s:SirenMinionView,time:number,quality=1){
 const r=m.rig,L=m.look;tickMinionClock(time);tickSirenClock(time);
 const e=s.spawnAt!==undefined?time-s.spawnAt:Infinity,emerging=e<EMERGE_SECONDS,d=s.deathAt!==undefined&&time>=s.deathAt?time-s.deathAt:undefined;
 const g=gaitAt(m,s,time,d===undefined);let P:Pose;
 if(d!==undefined){
  // The pose at deathAt (living or emerging), blended into the death keys over .2 s.
  const de=s.spawnAt!==undefined?s.deathAt!-s.spawnAt:Infinity;
  if(de<EMERGE_SECONDS)emergeBlend(POSE_A,m,s,s.deathAt!,Math.max(0,de),g);else livingPose(POSE_A,m,s,s.deathAt!,g);
  POSE_B.set(POSE_A);deathPose(POSE_B,L,d);P=blendPose(POSE_C,POSE_A,POSE_B,sm(d/.2));
 }else if(emerging)P=emergeBlend(POSE_C,m,s,time,Math.max(0,e),g);
 else{livingPose(POSE_C,m,s,time,g);P=POSE_C;}
 applyPose(r,P);
 // Tongues flicker, flare while emerging and as the body burns.
 // Flaring flames grow wider as well as taller, so they stay flames instead of turning into spikes.
 const flare=1+.5*win(e,.2,.6,1.6,EMERGE_SECONDS)+(d!==undefined?.7*win(d,.3,.8,1.6,2.1):0),wide=1.25*(1+.45*(flare-1));
 for(const g of m.tongues){const n=.5+.5*Math.sin(time*7.3+g.phase)*Math.sin(time*3.1+g.phase*1.9);g.mesh.scale.set(wide,1.25*g.height*(.65+.55*n)*flare,wide);
  _e.set(.18*Math.sin(time*2.6+g.phase),0,.26*Math.sin(time*3.4+g.phase*1.3));g.mesh.quaternion.copy(g.base).multiply(_q.setFromEuler(_e));}
 dyingLook(m,d);
 m.fx.update(e<EMERGE_SECONDS+.4?e:undefined,d,d!==undefined?bodyPoints(r):POINTS,quality);
}
function emergeBlend(out:Pose,m:SirenMinion,s:SirenMinionView,time:number,e:number,g:Gait){
 emergePose(POSE_B,m.look,e,m.floor,time);const w=sm((e-1.62)/(EMERGE_SECONDS-1.62));
 if(w<=0){out.set(POSE_B);return out;}
 livingPose(POSE_A,m,s,time,g);
 // The gait's feet take over through the IK weight; the folded legs fade out with it.
 return blendPose(out,POSE_B,POSE_A,w);
}

/** Char the body part by part, then burn it away feet first; the eyes go last. Restores everything while alive. */
function dyingLook(m:SirenMinion,d:number|undefined){
 const r=m.rig,parts=m.parts;let lo=Infinity,hi=-Infinity;for(const p of parts){lo=Math.min(lo,p.y);hi=Math.max(hi,p.y);}
 const gone=d!==undefined&&d>=DEATH_SECONDS;r.body.visible=!gone;
 parts.forEach((p,i)=>{
  const mat=d!==undefined&&d>.42+.7*hash01(m.seed,i,61)?p.charred:p.live;if(p.mesh.material!==mat)p.mesh.material=mat;
  const vanish=d===undefined?0:sm((d-(1.25+.8*(p.y-lo)/Math.max(1e-6,hi-lo)))/.3);
  if(vanish>0)p.mesh.scale.copy(p.scale).multiplyScalar(Math.max(.001,1-vanish));else if(!p.mesh.scale.equals(p.scale))p.mesh.scale.copy(p.scale);});
 m.tongues.forEach((g,i)=>{g.mesh.visible=!gone&&!(m.low&&i>=2);});if(m.scrap)m.scrap.visible=!(d!==undefined&&d>1.5);
}
