// Chaos summon presentation (owner request 2026-10-04: summons get real attacks and health, modelled on the Siren adds).
// Poses read only the authoritative view: lastAttack starts the windup and SUMMON (shared) times the strike frame, so the
// claw/shot lands on the same tick as the damage. Hit flinches come from minionHit events, deaths from minionDeath.
// Allocation-free per frame; the rigs reuse the shared rig cube and Lambert palette (no new shader programs).
import * as T from 'three';
import type {MinionView} from '../shared/chaos.js';
import {SUMMON} from '../shared/chaos.js';
import {createCharacterRig,rigBox,type CharacterRig} from './character-rig.js';

export const SUMMON_LOOK={
 zombie:{accent:'#465436',body:0x8da976,scale:.85,eyes:0xff5340,gib:[0x5d7a4c,0x34402a,0x8da976] as const},
 toy:{accent:'#e8c25a',body:0xb8322c,scale:.6,eyes:0x1a1a1a,gib:[0xb8322c,0xe8c25a,0x2b2b2b] as const},
 /** Corpse: collapse, then voxel break-up at `breakAt` s; the rig is gone at `gone`. */
 death:{breakAt:.75,gone:.9},
 hit:.28,
} as const;
const ease=(x:number)=>x<=0?0:x>=1?1:x*x*(3-2*x);
const clamp01=(x:number)=>x<0?0:x>1?1:x;

export function buildSummon(kind:'zombie'|'toy'):CharacterRig{
 const k=SUMMON_LOOK[kind],r=createCharacterRig(k.accent,k.body);r.group.scale.setScalar(k.scale);r.group.name='summon-'+kind;
 if(kind==='zombie'){rigBox(r.head,'left-eye',-.08,.19,.165,.05,.045,.02,k.eyes);rigBox(r.head,'right-eye',.08,.19,.165,.05,.045,.02,k.eyes);
  // Torn shirt flaps and a slack jaw read as undead at summon scale.
  rigBox(r.chest,'rag',.12,-.02,.16,.16,.2,.02,0x3d3a2c);rigBox(r.head,'jaw-gap',0,.06,.16,.16,.04,.02,0x1a1410);}
 else{
  // Tin soldier: black shako with a gold plate, white belts, a rifle with a bayonet on the weapon socket.
  rigBox(r.head,'shako',0,.44,0,.3,.3,.29,0x16161a);rigBox(r.head,'shako-plate',0,.42,.15,.12,.12,.02,0xe8c25a);rigBox(r.head,'shako-brim',0,.3,.12,.3,.03,.12,0x16161a);
  rigBox(r.head,'left-eye',-.07,.18,.165,.04,.04,.02,k.eyes);rigBox(r.head,'right-eye',.07,.18,.165,.04,.04,.02,k.eyes);
  rigBox(r.chest,'belt-a',0,.1,.16,.06,.34,.02,0xf2efe6).rotation.z=.7;rigBox(r.chest,'belt-b',0,.1,.16,.06,.34,.02,0xf2efe6).rotation.z=-.7;
  const gun=new T.Group();gun.name='toy-rifle';r.weapon.add(gun);gun.rotation.x=Math.PI/2;
  rigBox(gun,'rifle-stock',0,0,-.12,.07,.1,.3,0x6b4424);rigBox(gun,'rifle-barrel',0,.02,.28,.045,.045,.55,0x2b2b2b);rigBox(gun,'rifle-bayonet',0,.02,.62,.025,.03,.16,0xd8dde2);
  const muzzle=new T.Group();muzzle.name='muzzle';muzzle.position.set(0,.02,.56);gun.add(muzzle);
 }
 return r;
}

/** Walk + attack + flinch pose at authoritative time t. hitAge: seconds since the last hit (Infinity: none). */
export function poseSummon(r:CharacterRig,m:MinionView,t:number,hitAge:number){
 const kind=m.kind==='toy'?'toy':'zombie',k=SUMMON[kind],a=t-m.lastAttack,gait=t*m.speed*2.2,walk=Math.sin(gait);
 // Rest pose then the walk.
 r.body.rotation.set(0,0,Math.sin(gait*.5)*.075);r.body.position.set(0,0,0);r.spine.rotation.set(.2,0,0);r.chest.rotation.set(0,0,0);r.chest.position.z=0;r.head.rotation.set(0,0,0);
 r.leftLeg.rotation.x=walk*.38;r.rightLeg.rotation.x=-walk*.38;r.leftKnee.rotation.x=Math.max(0,walk)*.5;r.rightKnee.rotation.x=Math.max(0,-walk)*.5;
 if(kind==='zombie'){r.leftArm.rotation.set(-1.25+walk*.12,0,0);r.rightArm.rotation.set(-1.25-walk*.12,0,0);r.leftElbow.rotation.x=-.18;r.rightElbow.rotation.x=-.3;r.head.rotation.z=Math.sin(t*1.7+m.id.length)*.12;}
 else{r.spine.rotation.x=0;r.leftArm.rotation.set(-.35,0,-.1);r.rightArm.rotation.set(-.2,0,.05);r.leftElbow.rotation.x=-1.3;r.rightElbow.rotation.x=-1.1;}
 const busy=a>=0&&a<k.windup+k.recover;
 if(busy){const W=k.windup;
  if(kind==='zombie'){// Lunge and claw: rear back with both claws high, lunge on the strike frame, then sag back.
   const wind=ease(a/W),strike=ease((a-W)/.12),rec=ease((a-W-.12)/(k.recover-.12)),hold=a<W?wind:1-rec,swing=a<W?0:strike*(1-rec);
   r.spine.rotation.x=.2-.38*hold+.78*swing;r.chest.position.z=.3*swing;r.head.rotation.x=-.3*hold+.25*swing;
   r.leftArm.rotation.x=-1.25-1.35*hold+1.9*swing;r.rightArm.rotation.x=-1.25-1.5*hold+2.05*swing;r.leftArm.rotation.z=.35*hold;r.rightArm.rotation.z=-.35*hold;
   r.leftElbow.rotation.x=-.18-.9*hold+.7*swing;r.rightElbow.rotation.x=-.3-.9*hold+.8*swing;r.leftLeg.rotation.x=.45*swing;r.rightLeg.rotation.x=-.55*swing;r.rightKnee.rotation.x=.4*swing;
  }else{// Aim and fire: shoulder the rifle over the windup, a sharp recoil on the shot, then port arms.
   const aim=a<W?ease(a/(W*.7)):1-ease((a-W-.18)/(k.recover-.18)),kick=a>=W?Math.max(0,1-(a-W)/.12):0;
   r.rightArm.rotation.x=-.2-1.3*aim+.35*kick;r.rightArm.rotation.y=.25*aim;r.rightElbow.rotation.x=-1.1+1*aim;r.leftArm.rotation.x=-.35-1.05*aim+.3*kick;r.leftArm.rotation.z=-.1-.35*aim;r.leftElbow.rotation.x=-1.3+.55*aim;
   r.head.rotation.set(.12*aim,.25*aim,0);r.spine.rotation.x=-.12*kick;r.body.position.z=-.06*kick;r.leftLeg.rotation.x=.3*aim;r.rightLeg.rotation.x=-.25*aim;
  }}
 if(hitAge<SUMMON_LOOK.hit){const h=Math.sin(clamp01(hitAge/SUMMON_LOOK.hit)*Math.PI);r.spine.rotation.x-=.4*h;r.head.rotation.x-=.35*h;r.body.position.z-=.08*h;r.leftArm.rotation.x+=.4*h;r.rightArm.rotation.x+=.3*h;}
}

/** Corpse pose: the zombie folds forward onto its knees, the toy topples stiffly backwards. */
export function poseSummonDeath(r:CharacterRig,kind:'zombie'|'toy',age:number){
 const f=ease(age/.5);
 if(kind==='zombie'){r.body.rotation.x=1.2*f;r.body.position.y=-.45*f;r.leftKnee.rotation.x=r.rightKnee.rotation.x=1.3*f;r.leftLeg.rotation.x=r.rightLeg.rotation.x=-.9*f;r.head.rotation.x=.5*f;r.leftArm.rotation.x=r.rightArm.rotation.x=-.4-.6*f;}
 else{r.body.rotation.x=-1.45*f;r.body.position.y=-.3*f;r.body.position.z=-.25*f;r.leftLeg.rotation.x=r.rightLeg.rotation.x=0;r.leftKnee.rotation.x=r.rightKnee.rotation.x=0;}
 // Jitter before the break-up.
 const shake=age>SUMMON_LOOK.death.breakAt-.2?Math.sin(age*90)*.03:0;r.group.position.x+=shake;
}
