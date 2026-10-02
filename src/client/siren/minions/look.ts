/**
 * Siren minions: the seeded look. A seed (any uint32; the authority would send one per add) decides every visual knob of
 * one zombie Pilot. Pure data and pure functions: no three.js, no random number calls, the same seed gives the same look on every
 * client and after every rebuild. docs/SIREN-MINIONS.md describes the knobs and the face options.
 */

/** Deterministic hash of a seed and up to three integers to [0,1). */
export function hash01(seed:number,a=0,b=0,c=0){
 let h=(seed^Math.imul(a+0x632be5ab,0x9e3779b1)^Math.imul(b+0x85157af5,0x85ebca77)^Math.imul(c+0x2545f491,0xc2b2ae3d))>>>0;
 h=Math.imul(h^(h>>>16),0x7feb352d)>>>0;h=Math.imul(h^(h>>>15),0x846ca68b)>>>0;return ((h^(h>>>16))>>>0)/4294967296;
}
/** A string as a uint32 seed (FNV-1a), for ids such as 'siren-add-12'. */
export function minionSeed(id:string|number){if(typeof id==='number')return id>>>0;let h=2166136261;for(let i=0;i<id.length;i++)h=Math.imul(h^id.charCodeAt(i),16777619);return h>>>0;}
/** A small seeded stream (xorshift on the hashed seed), for building one look in a fixed order. */
export function stream(seed:number,salt=0){let s=(Math.floor(hash01(seed,salt,0x51ed)*4294967296)>>>0)||0x9e3779b9;
 return ()=>{s^=s<<13;s>>>=0;s^=s>>>17;s^=s<<5;s>>>=0;return s/4294967296;};}

/** Face options (docs/SIREN-MINIONS.md). The pilots are blank-faced; 'cracked' splits the head shell and leaks black flame
 * from the crown; 'eyes' burns in faint white Siren-style eyes in dark sockets. */
export const MINION_FACES=['blank','eyes','cracked'] as const;
export type MinionFace=typeof MINION_FACES[number];
/** Default face (the owner's choice, 2026-10-02): blank, like every Pilot. Some seeds crack instead (seededFace); 'eyes' is
 * never in the seeded pool and only appears when MinionOptions.face asks for it. */
export const DEFAULT_MINION_FACE:MinionFace='blank';
/** Share of seeds whose head shell is cracked open (the rest stay blank). */
export const CRACKED_FACE_SHARE=.25;
/** The face a seed gets without an override: blank, or cracked for about a quarter of the seeds. Its own hash, so the
 * look stream (and every other knob of existing seeds) is unchanged. */
export function seededFace(seed:number):MinionFace{return hash01(seed>>>0,0xfa,1)<CRACKED_FACE_SHARE?'cracked':DEFAULT_MINION_FACE;}
export type MinionArms='reach'|'limp'|'mixed';
export type MinionScrap='none'|'sword'|'pistol'|'pipe';
export type MinionMissing='none'|'left-hand'|'right-hand'|'left-forearm'|'right-forearm'|'jaw';
/** What a body part shows. 'rot' is every part's base; the others replace it on a few parts. */
export type PartCondition='rot'|'burn'|'wound'|'stump'|'cracked'|'dead';
export interface PartLook {condition:PartCondition;variant:number}

export interface MinionLook {
 seed:number;face:MinionFace;
 /** 0..2: light, medium, heavy rot (quantized so the part layouts can be shared between minions). */
 rot:0|1|2;
 /** Material pool (0..MINION_POOLS-1): the dying glow of one minion flickers together, different minions out of step. */
 pool:number;
 parts:Record<string,PartLook>;
 missing:MinionMissing;scrap:MinionScrap;
 /** Body language (radians unless noted). */
 hunch:number;headTilt:number;shoulderDrop:number;arms:MinionArms;reachSide:1|-1;
 /** Gait: the dragged leg (+1 left, -1 right), how much it drags (0..1), walk and run cycles per second, phase offset. */
 drag:1|-1;limp:number;cadence:number;runCadence:number;phase:number;
 /** Head/shoulder twitch rate (events per second) and violence (0..1). */
 twitch:number;jerk:number;
 /** Torso sway amplitude (radians). */
 sway:number;
}

export const MINION_POOLS=4;
/** Variants per condition: the layout cache holds at most (parts x variants) entries however many minions spawn. */
export const VARIANTS:Record<PartCondition,number>={rot:4,burn:3,wound:3,stump:1,cracked:3,dead:1};

/** Parts that can show a burn or a wound (large enough shell faces). */
export const BURNABLE=['ribcage','abdomen-shell','hip-shell','blank-head','left-upper-arm','right-upper-arm','left-forearm','right-forearm','left-thigh','right-thigh','left-shin','right-shin','back-panel'] as const;
export const WOUNDABLE=['ribcage','abdomen-shell','left-upper-arm','right-upper-arm','left-thigh','right-thigh','left-forearm','right-forearm','hip-shell'] as const;
export const ACCENTS=['waist-band','sternum','left-shoulder-cap','right-shoulder-cap','left-cuff','right-cuff','left-kneecap','right-kneecap'] as const;
/** Meshes hidden by each missing-part option, and the part that shows the stump. */
export const MISSING:Record<Exclude<MinionMissing,'none'>,{hide:string[];stump:string}>={
 'left-hand':{hide:['left-cuff','left-palm','left-fingers','left-thumb'],stump:'left-forearm'},
 'right-hand':{hide:['right-cuff','right-palm','right-fingers','right-thumb'],stump:'right-forearm'},
 'left-forearm':{hide:['left-forearm','left-cuff','left-palm','left-fingers','left-thumb'],stump:'left-elbow-joint'},
 'right-forearm':{hide:['right-forearm','right-cuff','right-palm','right-fingers','right-thumb'],stump:'right-elbow-joint'},
 'jaw':{hide:['jaw'],stump:'blank-head'},
};

export interface MinionOptions {
 face?:MinionFace;
 /** 0..1 overrides (default: from the seed). */
 rot?:number;burn?:number;
 /** Number of carved wounds (default 1..3 from the seed). */
 wounds?:number;
 missing?:MinionMissing|'auto';scrap?:MinionScrap|'auto';
 hunch?:number;arms?:MinionArms;
}

const pick=<T,>(r:()=>number,list:readonly T[])=>list[Math.min(list.length-1,Math.floor(r()*list.length))];
const lerp=(a:number,b:number,t:number)=>a+(b-a)*t;

/** The full look of one minion. Every seed differs; all read as the same family (same palette, same rules). */
export function minionLook(seed:number,o:MinionOptions={}):MinionLook{
 seed=seed>>>0;const r=stream(seed,1);
 const rotAmount=o.rot??r(),burnAmount=o.burn??r(),rot=(rotAmount<.34?0:rotAmount<.67?1:2) as 0|1|2;
 const pool=Math.floor(r()*4)%MINION_POOLS;
 const parts:Record<string,PartLook>={};
 // Burns: one to three parts (a few more with a high burn amount); never the same part twice.
 const burnCount=Math.max(1,Math.round(lerp(1,3.4,burnAmount)+(r()-.5)*.8));
 const burnable=[...BURNABLE];for(let i=0;i<burnCount&&burnable.length;i++){const k=Math.floor(r()*burnable.length);const name=burnable.splice(k,1)[0];parts[name]={condition:'burn',variant:Math.floor(r()*VARIANTS.burn)};}
 // Wounds: carved openings on parts that are not burned.
 const woundCount=Math.max(0,Math.min(4,o.wounds??(1+Math.floor(r()*2.6))));
 const woundable=WOUNDABLE.filter(n=>!parts[n]);for(let i=0;i<woundCount&&woundable.length;i++){const k=Math.floor(r()*woundable.length);const name=woundable.splice(k,1)[0];parts[name]={condition:'wound',variant:Math.floor(r()*VARIANTS.wound)};}
 // Missing parts: about a third of the minions lose a hand, a forearm or the jaw plate.
 const roll=r(),missing:MinionMissing=o.missing&&o.missing!=='auto'?o.missing:roll<.12?pick(r,['left-hand','right-hand'] as const):roll<.24?pick(r,['left-forearm','right-forearm'] as const):roll<.33?'jaw':'none';
 if(missing!=='none'){const m=MISSING[missing];parts[m.stump]={condition:m.stump==='blank-head'?(parts['blank-head']?.condition==='burn'?'burn':'rot'):'stump',variant:0};for(const h of m.hide)delete parts[h];}
 // The dying glow: most accent plates are cracked and flickering, some are dead.
 for(const name of ACCENTS)if(!parts[name])parts[name]=r()<.22?{condition:'dead',variant:0}:{condition:'cracked',variant:Math.floor(r()*VARIANTS.cracked)};
 const rightHand=!(missing==='right-hand'||missing==='right-forearm');
 const scrap:MinionScrap=o.scrap&&o.scrap!=='auto'?(rightHand?o.scrap:'none'):rightHand&&r()<.55?pick(r,['sword','pistol','pipe'] as const):'none';
 const arms:MinionArms=o.arms??(scrap!=='none'?pick(r,['limp','limp','mixed'] as const):pick(r,['reach','limp','mixed','reach'] as const));
 return {seed,face:o.face??seededFace(seed),rot,pool,parts,missing,scrap,
  hunch:o.hunch??lerp(.18,.5,r()),headTilt:(r()-.5)*.7,shoulderDrop:(r()<.5?-1:1)*lerp(.04,.16,r()),arms,reachSide:r()<.5?1:-1,
  drag:r()<.5?1:-1,limp:lerp(.35,1,r()),cadence:lerp(.92,1.12,r()),runCadence:lerp(1.62,1.86,r()),phase:r(),
  twitch:lerp(.25,.6,r()),jerk:lerp(.5,1,r()),sway:lerp(.06,.13,r())};
}
/** The condition of a rig part for a look (rot when the look does not name it). */
export function partLook(look:MinionLook,name:string):PartLook{return look.parts[name]||{condition:'rot',variant:Math.floor(hash01(look.seed,minionSeed(name),7)*VARIANTS.rot)};}
