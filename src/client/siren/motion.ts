import * as T from 'three';
import type {CharacterRig} from '../character-rig.js';
import type {PlayerView,Snapshot} from '../../shared/simulation.js';
import {SIREN_HOVER,SIREN_TIMING,sirenOf,sirenWorldOf,type SirenMoveState} from '../../shared/siren-contract.js';
import {buildSiren,applySirenLook,type SirenParts} from './model.js';
import {tickSirenClock} from './materials.js';

/**
 * Siren animation. Every joint is written each frame (the base animateCharacter result is fully replaced), as a pure
 * function of the authoritative times in PlayerView.siren.move plus a little smoothed presentation state (mode blend,
 * mouth, lean) and seeded randomness for the psychotic twitches. Knockdowns and juggles keep the shared ragdoll-style
 * motion from animateCharacter; the look (flames, eyes) still applies.
 */
type V3=[number,number,number];
const JOINTS=['body','pelvis','spine','chest','neck','head','lClav','rClav','lArm','rArm','lElbow','rElbow','lHand','rHand','lLeg','rLeg','lKnee','rKnee'] as const;
type J=typeof JOINTS[number];
export type Pose=Record<J,V3>&{lift:number;fwd:number;side:number;open:number;squint:number;claw:number};

const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
const sm=(x:number)=>{x=clamp(x);return x*x*(3-2*x);};
const outCubic=(x:number)=>{x=clamp(x);return 1-(1-x)**3;};
const outExpo=(x:number)=>{x=clamp(x);return x>=1?1:1-2**(-10*x);};
const inCubic=(x:number)=>{x=clamp(x);return x*x*x;};
const outBack=(x:number,s=1.7)=>{x=clamp(x)-1;return 1+(s+1)*x*x*x+s*x*x;};
const mix=(a:number,b:number,t:number)=>a+(b-a)*t;
const span=(t:number,a:number,b:number)=>clamp((t-a)/Math.max(1e-4,b-a));
/** Deterministic value noise in [-1,1] (twitches stay identical for every viewer of the same timeline). */
const hash=(n:number)=>{const s=Math.sin(n*127.1+311.7)*43758.5453;return (s-Math.floor(s))*2-1;};
const noise=(t:number,seed:number)=>{const i=Math.floor(t),f=t-i,u=f*f*(3-2*f);return mix(hash(i+seed*17.3),hash(i+1+seed*17.3),u);};

function blank():Pose{const p={} as Pose;for(const j of JOINTS)p[j]=[0,0,0];p.lift=0;p.fwd=0;p.side=0;p.open=0;p.squint=0;p.claw=1;return p;}
function copy(src:Pose):Pose{const p=blank();for(const j of JOINTS)p[j]=[...src[j]] as V3;p.lift=src.lift;p.fwd=src.fwd;p.side=src.side;p.open=src.open;p.squint=src.squint;p.claw=src.claw;return p;}
function blend(a:Pose,b:Pose,w:number):Pose{if(w<=0)return a;if(w>=1)return b;const p=blank();for(const j of JOINTS)for(let k=0;k<3;k++)p[j][k]=mix(a[j][k],b[j][k],w);
 p.lift=mix(a.lift,b.lift,w);p.fwd=mix(a.fwd,b.fwd,w);p.side=mix(a.side,b.side,w);p.open=mix(a.open,b.open,w);p.squint=mix(a.squint,b.squint,w);p.claw=mix(a.claw,b.claw,w);return p;}
const set=(p:Pose,j:J,x:number,y:number,z:number)=>{p[j][0]=x;p[j][1]=y;p[j][2]=z;};
const add=(p:Pose,j:J,x:number,y=0,z=0)=>{p[j][0]+=x;p[j][1]+=y;p[j][2]+=z;};
/** Mirror helper: a right-arm pose for side=1, the same pose on the left arm for side=-1. */
function arm(p:Pose,side:number,shoulder:V3,elbow:number,hand:V3){
 const s=side>=0;p[s?'rArm':'lArm']=[shoulder[0],shoulder[1]*(s?1:-1),shoulder[2]*(s?1:-1)];p[s?'rElbow':'lElbow']=[elbow,0,0];p[s?'rHand':'lHand']=[hand[0],hand[1]*(s?1:-1),hand[2]*(s?1:-1)];
}

/** The floating "evil" stance: hunched, shoulders up, head cocked hard to one side, claws hanging low and open. */
function stance(t:number,ranged:number):Pose{
 const p=blank(),breath=Math.sin(t*1.7),sway=Math.sin(t*.63),slow=Math.sin(t*.37);
 p.lift=SIREN_HOVER.height+Math.sin(t*Math.PI*2*SIREN_HOVER.bobHz)*SIREN_HOVER.bob;
 set(p,'body',.03+.02*sway,.05*slow,.03*sway);
 set(p,'pelvis',-.05,0,0);set(p,'spine',.24+.02*breath,.06*sway,0);set(p,'chest',.22+.035*breath,.05*slow,.02*sway);
 set(p,'lClav',0,0,.16+.03*breath);set(p,'rClav',0,0,-.16-.03*breath);
 set(p,'neck',-.3,0,0);set(p,'head',.08,.08*slow,.58+.04*sway);
 // Claws hang forward and a little out; the ranged pose holds the burning palms up and in front.
 const rx=mix(-.22,-.85,ranged),re=mix(-.42,-1.35,ranged);
 set(p,'lArm',rx-.04*breath,0,.13+.03*sway);set(p,'rArm',rx-.16-.04*breath,0,-.1-.03*sway);
 set(p,'lElbow',re,0,0);set(p,'rElbow',re-.18,0,0);
 set(p,'lHand',mix(-.5,.25,ranged),0,mix(.06,-.2,ranged));set(p,'rHand',mix(-.62,.25,ranged),0,mix(-.08,.2,ranged));
 // Legs dangle: hips slightly forward, knees bent, drifting out of phase like something hanging in water.
 set(p,'lLeg',-.3+.06*Math.sin(t*1.1),0,.05);set(p,'rLeg',-.16+.06*Math.sin(t*1.1+1.9),0,-.05);
 set(p,'lKnee',.85+.1*Math.sin(t*1.3+.4),0,0);set(p,'rKnee',1.02+.1*Math.sin(t*1.3+2.2),0,0);
 p.claw=1-ranged;return p;
}

/** A move's pose and how strongly it overrides the stance (0-1). */
function movePose(m:SirenMoveState,t:number,base:Pose,ranged:number,pitch:number):{pose:Pose;w:number}{
 const p=copy(base),a=m.at,h=m.hitAt,u=m.until,side=m.side??1;
 const w=sm(span(t,a,a+.07))*(1-sm(span(t,u-.18,u)));
 switch(m.id){
  case 'swipe':{
   // Load across the body, then rake through diagonally down: fast, vicious, leading with the elbow.
   const load=outCubic(span(t,a,h)),cut=outExpo(span(t,h,h+SIREN_TIMING.swipe.active*1.6)),rec=sm(span(t,h+SIREN_TIMING.swipe.active,u));
   const sx=mix(mix(-.4,-1.75,load),-.55,cut),sz=mix(mix(-.25,-1.25,load),.5,cut),el=mix(mix(-.6,-1.45,load),-.2,cut);
   arm(p,side,[mix(sx,base[side>0?'rArm':'lArm'][0],rec*.6),0,sz*(1-rec*.5)],el,[mix(-.2,.35,cut),0,mix(-.3,.2,cut)]);
   const tw=mix(mix(0,-.45,load),.5,cut)*side;add(p,'spine',.06*cut,tw*.45,0);add(p,'chest',.08*cut,tw*.6,0);add(p,'head',0,-tw*.3,0);
   p.fwd=.16*cut*(1-rec);p.squint=.6*(1-rec);p.open=.25*cut*(1-rec);break;}
  case 'rend':{
   // Rise and coil overhead, then a huge two-step diagonal slash that drops the body into it.
   const load=sm(span(t,a,h)),cut=outExpo(span(t,h,h+SIREN_TIMING.rend.active*1.5)),rec=sm(span(t,h+.2,u));
   arm(p,side,[mix(mix(-.4,-2.75,load),-.35,cut),0,mix(mix(-.2,-.55,load),.75,cut)],mix(mix(-.6,-.9,load),-.1,cut),[mix(.3,-.4,cut),0,0]);
   arm(p,-side,[mix(-.3,.45,load)*(1-cut*.5),0,.5*(1-cut)],-.8,[0,0,0]);
   const tw=mix(mix(0,-.65,load),.6,cut)*side;add(p,'spine',mix(-.18*load,.42,cut),tw*.45,0);add(p,'chest',mix(-.12*load,.3,cut),tw*.55,0);add(p,'head',mix(-.2*load,.15,cut),-tw*.3,-.2*load*side);
   p.lift+=.16*load*(1-cut)-.12*cut*(1-rec);p.fwd=.42*cut*(1-rec*.8);p.squint=.8*(1-rec);p.open=.5*cut*(1-rec);break;}
  case 'slam':{
   // Float up with both claws raised, then drive them into the floor and hunch over the impact.
   const load=sm(span(t,a,h)),hit=outExpo(span(t,h-.04,h+.08)),rec=sm(span(t,h+.32,u));
   for(const s of [1,-1])arm(p,s,[mix(mix(-.4,-2.9,load),-1.05,hit),0,mix(-.2,-.32,load)*(1-hit)+.18*hit],mix(-.5,-.15,hit),[mix(0,-.6,load)*(1-hit)+.5*hit,0,0]);
   add(p,'spine',mix(-.3*load,.55,hit)*(1-rec*.7),0,0);add(p,'chest',mix(-.2*load,.28,hit)*(1-rec*.7),0,0);add(p,'head',mix(-.35*load,-.35,hit),0,-.3*hit);
   add(p,'lKnee',.4*hit*(1-rec));add(p,'rKnee',.4*hit*(1-rec));add(p,'lLeg',-.3*hit*(1-rec));add(p,'rLeg',-.3*hit*(1-rec));
   p.lift+=.38*load*(1-hit)-.3*hit*(1-rec);p.fwd=.2*hit*(1-rec);p.open=.9*hit*(1-rec);p.squint=.4*load;break;}
  case 'rush':{
   // Coil with claws swept back, then streak forward claws-first, legs streaming behind.
   const end=m.endAt??h+SIREN_TIMING.rush.dash,load=sm(span(t,a,h)),go=outCubic(span(t,h,h+.08))*(1-sm(span(t,end-.04,end+.1))),rec=sm(span(t,end,u));
   for(const s of [1,-1])arm(p,s,[mix(mix(-.38,.9,load),-1.45,go),0,mix(-.25,-.45,load)*(1-go)+(-.55)*go],mix(-.6,-.12,go),[-.4*go,0,0]);
   add(p,'body',.42*go+.18*load,0,0);add(p,'spine',.22*go,0,0);add(p,'head',-.45*go,0,-.4*go);add(p,'lLeg',.95*go+.2*load);add(p,'rLeg',.8*go+.2*load);add(p,'lKnee',.35*go);add(p,'rKnee',.5*go);
   p.lift-=.1*load*(1-go);p.squint=.9*Math.max(load,go)*(1-rec);p.open=.4*go;break;}
  case 'laugh':{
   const lunge=m.hitAt,lungeEnd=m.endAt??lunge+SIREN_TIMING.laugh.lunge,spread=sm(span(t,a,a+.35))*(1-sm(span(t,lunge-.12,lunge)));
   // Arms flung out to the sides, head thrown back, shoulders bucking with the laugh.
   const shake=Math.sin((t-a)*Math.PI*2*6.2)*sm(span(t,a+.2,a+.5))*(1-sm(span(t,lunge-.2,lunge)));
   for(const s of [1,-1])arm(p,s,[mix(-.38,-.12,spread),0,mix(-.2,-1.45,spread)+.06*shake],mix(-.6,-.25,spread),[0,0,mix(0,-.5,spread)]);
   add(p,'chest',-.32*spread+.06*shake,0,0);add(p,'spine',-.18*spread,0,0);add(p,'head',-.42*spread+.1*shake,0,-.2*spread);add(p,'lClav',0,0,.12*shake);add(p,'rClav',0,0,-.12*shake);
   p.lift+=.12*spread+.02*shake;p.open=Math.max(.95*spread,0);p.squint=.85*spread;
   // Lunge: the right claw reaches for the throat.
   const go=outExpo(span(t,lunge,lungeEnd)),reach=go*(1-sm(span(t,lungeEnd,lungeEnd+.25)));
   if(t>=lunge-.12){arm(p,1,[mix(-.2,-1.55,go),0,-.15],mix(-.5,-.08,go),[-.3*go,0,0]);arm(p,-1,[mix(-.12,.55,go),0,mix(-1.2,-.35,go)],-.7,[0,0,0]);add(p,'body',.45*reach,0,0);add(p,'head',.35*go,0,.2*go);p.fwd=.4*reach;}
   if(m.caught&&m.caughtAt!==undefined){
    const c=m.caughtAt,crush=m.crushAt??c+SIREN_TIMING.laugh.crush,lift=sm(span(t,c,c+SIREN_TIMING.laugh.lift)),head=sm(span(t,c+SIREN_TIMING.laugh.lift*.6,c+SIREN_TIMING.laugh.head)),squeeze=outExpo(span(t,crush-.05,crush+.08)),drop=sm(span(t,crush+.12,u-.1));
    // Hold the victim up at arm's length; the other hand settles on their head; a final clench.
    arm(p,1,[mix(-1.55,-2.1,lift)*(1-drop)+(-.5)*drop,0,-.06],mix(-.08,-.04,lift),[-.15,0,.15*squeeze]);
    arm(p,-1,[mix(-.4,-2.2,head)*(1-drop)+(-.45)*drop,0,mix(-.5,.2,head)],mix(-.7,-.25,head)-.25*squeeze,[mix(0,.6,head),0,0]);
    const tremble=Math.sin(t*41)*.025*span(t,c+1,crush);set(p,'body',mix(.45,.08,lift),0,tremble);add(p,'chest',-.12*lift,0,0);
    p.head=[mix(.1,-.25,lift),0,.65+.15*squeeze];p.open=mix(.3,1,span(t,c+.9,crush))*(1-drop*.6);p.squint=.3+.6*squeeze;p.lift+=.18*lift;p.fwd=.1*(1-lift);
   }
   p.claw=1;break;}
  case 'hellfire':{
   // Cock the burning hand behind the shoulder and snap it forward.
   const load=outCubic(span(t,a,h)),thr=outExpo(span(t,h-.03,h+.09)),rec=sm(span(t,h+.1,u));
   arm(p,side,[mix(mix(-.85,.35,load),-1.45-pitch*.6,thr)*(1-rec*.5)+base[side>0?'rArm':'lArm'][0]*rec*.5,0,mix(-.35,-.15,thr)],mix(mix(-1.3,-1.85,load),-.15,thr),[mix(.4,-.3,thr),0,0]);
   const tw=mix(-.35*load,.3,thr)*side;add(p,'spine',.05*thr,tw*.4,0);add(p,'chest',-pitch*.3,tw*.55,0);add(p,'head',-pitch*.25,-tw*.3,0);p.fwd=.08*thr*(1-rec);break;}
  case 'damnation':{
   // Both hands raise the swelling ball overhead, the body arches, then hurls it with everything.
   const load=sm(span(t,a,h)),thr=outExpo(span(t,h-.04,h+.12)),rec=sm(span(t,h+.15,u));
   for(const s of [1,-1])arm(p,s,[mix(mix(-.85,-2.95,load),-1.25-pitch*.5,thr),0,mix(-.15,-.22,load)],mix(mix(-1.3,-.75,load),-.2,thr),[mix(.3,-.2,load),0,0]);
   add(p,'spine',mix(-.28*load,.45,thr)*(1-rec),0,0);add(p,'chest',mix(-.2*load,.25,thr)*(1-rec)-pitch*.25,0,0);add(p,'head',mix(-.4*load,.1,thr),0,-.25*load);
   p.lift+=.22*load*(1-thr);p.fwd=.2*thr*(1-rec);p.open=.6*load+.4*thr*(1-rec);p.squint=.7*load;break;}
  case 'eruption':{
   // Point the burning palm at the ground target and clench: the floor answers a second later.
   const load=sm(span(t,a,h)),hit=outBack(span(t,h-.05,h+.12)),rec=sm(span(t,h+.05,u));
   arm(p,1,[mix(-.85,-1.25,load)+.35*hit,0,-.18],mix(-1.3,-.15,load),[mix(.3,1.0,hit),0,0]);
   add(p,'chest',.12*load,-.2*load,0);add(p,'head',.25*load,0,.15*load);p.squint=.9*load*(1-rec);p.open=.3*hit*(1-rec);break;}
  case 'rain':{
   // Arms rise into a V, head back toward the sky, palms open; the cloud gathers.
   const load=sm(span(t,a,h)),fling=outExpo(span(t,h-.05,h+.15)),rec=sm(span(t,h+.15,u));
   for(const s of [1,-1])arm(p,s,[mix(-.85,-2.55,load)-.2*fling,0,mix(-.2,-.75,load)-.15*fling],mix(-1.3,-.2,load),[mix(.3,-.6,load),0,0]);
   add(p,'chest',-.3*load,0,0);add(p,'head',-.55*load,0,-.2*load);p.lift+=.15*load*(1-rec);p.open=.8*load*(1-rec);p.squint=.2;break;}
  case 'mark':{
   // "You...." - slowly raise a claw at the target and hold it while the beeps climb, then hurl the slow flame.
   const lock=m.endAt??a+SIREN_TIMING.mark.voice,point=sm(span(t,a,a+.9)),tension=span(t,lock,h),thr=outExpo(span(t,h-.06,h+.14)),wind=sm(span(t,h-.3,h-.06))*(1-thr),rec=sm(span(t,h+.15,u));
   const tremor=Math.sin(t*37)*.02*tension;
   arm(p,1,[mix(-.4,-1.48-pitch*.5,point)+.75*wind-.25*thr+tremor,0,mix(-.2,-.08,point)],mix(-.6,-.05,point)-.6*wind,[mix(0,-.1,point),0,0]);
   arm(p,-1,[-.3,0,.35],-.75,[0,0,0]);
   add(p,'head',.12*point,-.15*point,.25*point+.2*tension);add(p,'chest',-pitch*.3,-.12*point,0);
   const voice=span(t,a+.15,a+1.1);p.open=.35*Math.sin(Math.PI*clamp(voice))*(1-thr)+.3*thr*(1-rec);p.squint=.5*point+.4*tension;p.fwd=.12*thr*(1-rec);break;}
 }
 return {pose:p,w};
}

const forward=new T.Vector3(),right=new T.Vector3(),vel=new T.Vector3(),wp=new T.Vector3(),inv=new T.Matrix4();
interface Smooth {fwd:number;lat:number;ranged:number;open:number;squint:number;lastMode:string;seq:number}
const SMOOTH=new WeakMap<object,Smooth>();
let quality=1;
/** Effects quality from the renderer (SirenEffects.update forwards it). */
export function setSirenQuality(q:number){quality=clamp(q,0,1);}

export function animateSirenRig(rig:CharacterRig,p:PlayerView,state:Snapshot,time:number,dt:number,pitch:number){
 const parts=buildSiren(rig);tickSirenClock(time);dt=clamp(dt,0,.05);
 const siren=sirenOf(p),move=siren?.move&&time<siren.move.until+.05?siren.move:undefined;
 let sm0=SMOOTH.get(rig.group);if(!sm0){sm0={fwd:0,lat:0,ranged:siren?.mode==='ranged'?1:0,open:0,squint:0,lastMode:siren?.mode??'melee',seq:-1};SMOOTH.set(rig.group,sm0);}
 // Mode blend follows the authoritative swap time so every viewer sees the same transformation.
 const toRanged=siren?.mode==='ranged',swap=siren?span(time,siren.modeAt,siren.modeAt+SIREN_TIMING.swap):1,targetRanged=toRanged?sm(swap):1-sm(swap);
 sm0.ranged=Number.isFinite(targetRanged)?targetRanged:sm0.ranged;
 // Local-frame velocity for the floating lean.
 const yaw=rig.group.rotation.y;forward.set(Math.sin(yaw),0,Math.cos(yaw));right.set(-Math.cos(yaw),0,Math.sin(yaw));vel.set(p.vel?.x??0,0,p.vel?.z??0);
 const k=1-Math.exp(-dt*6);sm0.fwd+=(vel.dot(forward)-sm0.fwd)*k;sm0.lat+=(vel.dot(right)-sm0.lat)*k;

 const parts2=parts as SirenParts;
 if(p.knockdown||p.juggle||p.hp<=0){finishLook(rig,parts2,sm0,0,0,time,dt,p,state);return;}
 const base=stance(time,sm0.ranged);
 // Floating locomotion: lean into the drift, arms and legs trail, no steps.
 const f=clamp(sm0.fwd/6.8,-1,1),l=clamp(sm0.lat/6.8,-1,1),speed=Math.hypot(f,l);
 add(base,'body',.28*f,0,-.22*l);add(base,'spine',.08*f,0,0);add(base,'head',-.18*f,0,0);
 for(const j of ['lArm','rArm'] as const)add(base,j,.5*f*(1-sm0.ranged*.6),0,0);
 add(base,'lLeg',.55*f+.15*speed,0,.12*l);add(base,'rLeg',.48*f+.15*speed,0,.12*l);add(base,'lKnee',.2*speed);add(base,'rKnee',.25*speed);
 if(!p.grounded){add(base,'lKnee',.35);add(base,'rKnee',.3);base.lift-=SIREN_HOVER.bob;}
 // Ranged aim follows the camera pitch a little.
 add(base,'chest',-pitch*.28*sm0.ranged,0,0);add(base,'head',-pitch*.22,0,0);
 // Swap: a violent shudder as the claws withdraw or the palms ignite.
 if(siren&&time-siren.modeAt<SIREN_TIMING.swap+.15){const k2=Math.sin(span(time,siren.modeAt,siren.modeAt+SIREN_TIMING.swap+.15)*Math.PI);add(base,'chest',.15*k2,0,Math.sin(time*50)*.05*k2);add(base,'lHand',-.6*k2,0,0);add(base,'rHand',-.6*k2,0,0);add(base,'head',0,0,-.3*k2);}
 // Psychotic twitch: every few seconds the head snaps the other way and back (deterministic per player).
 const seed=hashId(p.id),cycle=4.3,phase=(time+seed*7)%cycle,tw=phase<.06?phase/.06:phase<.24?1:phase<.5?1-sm((phase-.24)/.26):0,which=hash(Math.floor((time+seed*7)/cycle)+seed)>0?1:-1;
 if(!move||move.id==='hellfire'||move.id==='swipe')add(base,'head',.12*tw,.15*tw*which,-(.75+.25*which)*tw);
 // Hit flinch: snap back from the blow, flames flare.
 const hr=p.hitReaction as {at?:number}|undefined,hit=hr?.at!==undefined?span(time,hr.at,hr.at+.35):1,flinch=hit<1?Math.sin(Math.PI*hit)*(1-hit):0;
 add(base,'chest',-.35*flinch,0,0);add(base,'head',-.4*flinch,0,.2*flinch);add(base,'lArm',.3*flinch,0,.2*flinch);add(base,'rArm',.3*flinch,0,-.2*flinch);
 // Guarding: claws crossed in front of the face.
 if(p.blockTime>0||p.guarding){const g=sm(clamp(p.blockTime>0?1:.6));arm(base,1,[-1.35,0,.55],-1.4,[0,0,0]);arm(base,-1,[-1.3,0,.45],-1.45,[0,0,0]);add(base,'head',.2*g,0,-.3*g);}

 let pose=base;
 if(move){const mp=movePose(move,time,base,sm0.ranged,pitch);pose=blend(base,mp.pose,mp.w);}
 apply(rig,pose,f,l);
 finishLook(rig,parts2,sm0,pose.open,pose.squint,time,dt,p,state);
}

function hashId(id:string){let h=0;for(let i=0;i<id.length;i++)h=(h*31+id.charCodeAt(i))|0;return (h>>>0)/4294967296;}

function apply(r:CharacterRig,p:Pose,_f:number,_l:number){
 const e=(o:T.Object3D,v:V3)=>o.rotation.set(v[0],v[1],v[2]);
 r.body.position.set(0,p.lift,p.fwd);e(r.body,p.body);e(r.pelvis,p.pelvis);e(r.spine,p.spine);e(r.chest,p.chest);e(r.neck,p.neck);e(r.head,p.head);
 e(r.leftShoulder,p.lClav);e(r.rightShoulder,p.rClav);e(r.leftArm,p.lArm);e(r.rightArm,p.rArm);e(r.leftElbow,p.lElbow);e(r.rightElbow,p.rElbow);e(r.leftHand,p.lHand);e(r.rightHand,p.rHand);
 e(r.leftLeg,p.lLeg);e(r.rightLeg,p.rLeg);e(r.leftKnee,p.lKnee);e(r.rightKnee,p.rKnee);
 // Feet are hidden; keep the ankles neutral so the wisps hang straight down.
 r.leftFoot.rotation.set(.5,0,0);r.rightFoot.rotation.set(.5,0,0);r.pelvis.position.y=-.24;
}

/** Eyes, mouth, claws/orbs and the black flame emission. */
function finishLook(rig:CharacterRig,parts:SirenParts,s:Smooth,open:number,squint:number,time:number,dt:number,p:PlayerView,state:Snapshot){
 const k=1-Math.exp(-dt*18);s.open+=(open-s.open)*k;s.squint+=(squint-s.squint)*k;
 const st=parts.state;st.ranged=s.ranged;st.open=s.open;st.squint=s.squint;st.anger=clamp(.3+s.squint*.9+s.open*.5);st.glow=p.hp>0?1:0;st.time=time;st.lean=clamp(s.fwd/6.8,-1,1);
 const move=sirenOf(p)?.move;st.flare=move&&(move.id==='hellfire'||move.id==='damnation')?Math.max(0,1-Math.abs(time-move.hitAt)/.2):0;
 applySirenLook(parts);
 // Damnation: the ball swells overhead through the windup and leaves the hands at release.
 const dm=move&&move.id==='damnation'&&time<move.hitAt+.02?clamp((time-move.at)/Math.max(.05,move.hitAt-move.at)):-1;parts.charge.visible=parts.chargeHalo.visible=dm>=0;
 if(dm>=0){const r=.08+.44*dm*dm;parts.charge.position.set(0,1.42-(1-dm)*.4,.12);parts.charge.scale.setScalar(r*(1+.08*Math.sin(time*30)));parts.charge.rotation.y=time*5;parts.chargeHalo.position.copy(parts.charge.position);parts.chargeHalo.scale.setScalar(r*6);
  rig.group.updateMatrixWorld(true);wp.copy(parts.charge.position);parts.root.localToWorld(wp);for(let i=0;i<Math.round(60*dt*quality*(1+dm*3));i++){const a=Math.random()*Math.PI*2,u=Math.random()*2-1,w=Math.sqrt(1-u*u);parts.flames.emit(wp.x+Math.cos(a)*w*r,wp.y+u*r,wp.z+Math.sin(a)*w*r,Math.cos(a)*w*.5,u*.5+.8,Math.sin(a)*w*.5,.25+Math.random()*.2,.06+.08*dm,Math.random()<.08?'ember':'black',2.4,2);}}
 // Grabbed victims and the shadow pool read the world state.
 const grabbing=sirenWorldOf(state)?.grabs.some(g=>g.owner===p.id)??false;
 (parts.shadow.material as T.MeshBasicMaterial).opacity=p.grounded?.55:.25;
 rig.group.updateMatrixWorld(true);updateSirenFlames(rig,parts,time,dt,quality,p.hp<=0,p.vel,grabbing);
}

/** Emit and advance the black flame on a Siren rig (the rig's world matrix must be current). */
export function updateSirenFlames(rig:CharacterRig,parts:SirenParts,time:number,dt:number,q:number,hidden=false,vel?:{x:number;z:number},grabbing=false){
 if(!hidden)emitFlames(rig,parts,time,dt,q,vel?.x??0,vel?.z??0,grabbing);
 inv.copy(parts.root.matrixWorld).invert();parts.flames.update(dt,inv);
}

type Emitter={joint:keyof CharacterRig;x:number;y:number;z:number;rate:number;size:number;up:number;spread:number;tail?:boolean;hands?:boolean};
const EMITTERS:Emitter[]=[
 {joint:'head',x:0,y:.34,z:-.02,rate:38,size:.075,up:1.7,spread:.13},
 {joint:'head',x:0,y:.24,z:-.15,rate:16,size:.065,up:1.3,spread:.12},
 {joint:'leftShoulder',x:.1,y:.06,z:0,rate:26,size:.07,up:1.4,spread:.09},
 {joint:'rightShoulder',x:-.1,y:.06,z:0,rate:26,size:.07,up:1.4,spread:.09},
 {joint:'chest',x:0,y:.3,z:-.12,rate:30,size:.08,up:1.5,spread:.16},
 {joint:'chest',x:0,y:.1,z:-.17,rate:16,size:.07,up:1.2,spread:.14},
 {joint:'spine',x:0,y:.05,z:-.14,rate:10,size:.065,up:1,spread:.12},
 {joint:'chest',x:.27,y:.12,z:0,rate:12,size:.05,up:1.2,spread:.06},
 {joint:'chest',x:-.27,y:.12,z:0,rate:12,size:.05,up:1.2,spread:.06},
 {joint:'pelvis',x:.16,y:-.04,z:0,rate:8,size:.05,up:1,spread:.06},
 {joint:'pelvis',x:-.16,y:-.04,z:0,rate:8,size:.05,up:1,spread:.06},
 {joint:'leftArm',x:0,y:-.12,z:-.06,rate:9,size:.055,up:1,spread:.05},
 {joint:'rightArm',x:0,y:-.12,z:-.06,rate:9,size:.055,up:1,spread:.05},
 {joint:'leftElbow',x:0,y:-.08,z:-.05,rate:8,size:.05,up:1,spread:.05},
 {joint:'rightElbow',x:0,y:-.08,z:-.05,rate:8,size:.05,up:1,spread:.05},
 {joint:'leftHand',x:0,y:-.12,z:.06,rate:7,size:.05,up:.9,spread:.05,hands:true},
 {joint:'rightHand',x:0,y:-.12,z:.06,rate:7,size:.05,up:.9,spread:.05,hands:true},
 {joint:'leftKnee',x:0,y:-.5,z:0,rate:30,size:.07,up:.2,spread:.06,tail:true},
 {joint:'rightKnee',x:0,y:-.5,z:0,rate:30,size:.07,up:.2,spread:.06,tail:true},
 {joint:'leftKnee',x:0,y:-.2,z:0,rate:14,size:.06,up:.5,spread:.07,tail:true},
 {joint:'rightKnee',x:0,y:-.2,z:0,rate:14,size:.06,up:.5,spread:.07,tail:true},
];
const acc=new WeakMap<object,number[]>(),TIPS=new WeakMap<object,number[]>();
function emitFlames(rig:CharacterRig,parts:SirenParts,_time:number,dt:number,quality:number,vx:number,vz:number,grabbing:boolean){
 if(dt<=0)return;let a=acc.get(rig.group);if(!a){a=EMITTERS.map(()=>Math.random());acc.set(rig.group,a);}
 const q=quality<.5?.45:quality,ranged=parts.state.ranged,boost=1+parts.state.flare*2+(grabbing?.5:0);
 // Each flame tongue sheds small tongues from its tip, so the fire keeps rising off the body in columns.
 let tips=TIPS.get(rig.group);if(!tips){tips=parts.tongues.map(()=>Math.random());TIPS.set(rig.group,tips);}
 for(let i=0;i<parts.tongues.length;i++){const g=parts.tongues[i];tips[i]+=7*q*boost*dt;if(tips[i]<1)continue;wp.set(0,.36*g.group.scale.y,0);g.group.localToWorld(wp);
  while(tips[i]>=1){tips[i]-=1;parts.flames.emit(wp.x+(Math.random()-.5)*.04,wp.y,wp.z+(Math.random()-.5)*.04,-vx*.2+(Math.random()-.5)*.08,1.1+Math.random()*.8,-vz*.2+(Math.random()-.5)*.08,.32+Math.random()*.25,.05+Math.random()*.025,Math.random()<.05?'ember':'black',2.8,2.4);}}
 for(let i=0;i<EMITTERS.length;i++){const e=EMITTERS[i];let rate=e.rate*q*boost*.65;if(e.hands)rate*=1+ranged*3.2;a[i]+=rate*dt;
  if(a[i]<1)continue;const joint=rig[e.joint] as T.Object3D;wp.set(e.x,e.y,e.z);joint.localToWorld(wp);
  while(a[i]>=1){a[i]-=1;const r=e.spread,jx=(Math.random()-.5)*2*r,jy=(Math.random()-.5)*r,jz=(Math.random()-.5)*2*r,ember=Math.random()<(e.hands&&ranged>.5?.16:.06);
   const trail=e.tail?.45:.22;
   parts.flames.emit(wp.x+jx,wp.y+jy,wp.z+jz,-vx*trail+(Math.random()-.5)*.16,e.up*(.6+Math.random()*.6)*(e.tail?.5:1),-vz*trail+(Math.random()-.5)*.16,
    (e.tail?.4:.55)*(.7+Math.random()*.6),e.size*.85*(ember?.45:1)*(.75+Math.random()*.5),ember?'ember':Math.random()<.18?'smoke':'black',e.tail?.6:2.6,e.tail?2.6:2.2);}
 }
}
