import {VOXEL as U,bevel,type VoxelBox} from './voxel-kit.js';

/**
 * Voxel armor slots for the blank mannequin. Each rig part (by name, without the left-/right- prefix) is sculpted by a
 * builder that writes boxes in metres around the part's centre, inside the part's box (w,h,d) unless noted.
 * `s` is the side sign: +1 on the left (outward = +x), -1 on the right. Shades darken seams and cores so layered plates
 * read under any lighting; glow lives in the accent/armor materials, never in the shade.
 *
 * Slots group the parts a future customization option would swap together (head shell, chest plate, pauldrons,
 * gauntlets, belt, legs, boots, back). A new variant is data: copy DEFAULT_ARMOR, replace some builders, add the entry to
 * ARMOR_VARIANTS. Wiring it to players needs a shared, networked cosmetics field plus a Customize control; neither
 * exists in this revision, so every rig uses 'default'.
 */
export type ArmorBuilder=(out:VoxelBox[],w:number,h:number,d:number,s:number)=>void;
export type ArmorSlot='head'|'chest'|'back'|'belt'|'pauldrons'|'arms'|'gauntlets'|'legs'|'boots';

const H=U/2,CORE=.9,SEAM=.72;
const plate=(o:VoxelBox[],x:number,y:number,z:number,w:number,h:number,d:number,normal:'x'|'y'|'z',b=H,shade=1)=>bevel(o,x,y,z,w,h,d,b,shade,normal);

export const ARMOR_SLOTS:Record<ArmorSlot,readonly string[]>={
 head:['blank-head','crown','jaw','neck-joint'],
 chest:['ribcage','upper-chest','collar','sternum','abdomen-shell'],
 back:['back-panel','armor-back-light'],
 belt:['hip-shell','waist-band'],
 pauldrons:['shoulder-cap'],
 arms:['upper-arm','elbow-joint','forearm','cuff'],
 gauntlets:['palm','fingers','thumb'],
 legs:['hip-joint','thigh','knee-joint','kneecap','shin','calf','armor-shin-light'],
 boots:['heel','foot'],
};

export const DEFAULT_ARMOR:Record<string,ArmorBuilder>={
 // Head: a blank block with stepped edges. No face, visor, eyes or ears; the back plate is the only facing cue.
 'blank-head':(o,w,h,d)=>{bevel(o,0,0,0,w,h,d,U);plate(o,0,-.01,-d/2-.004,w-.14,h-.16,.008,'z',H,CORE);},
 'crown':(o,w,h,d)=>{bevel(o,0,0,0,w,h,d,U,1,'y');},
 'jaw':(o,w,h,d)=>{bevel(o,0,0,0,w,h,d,U,1,'y');},
 'neck-joint':(o,w,h,d)=>{bevel(o,0,0,0,w,h,d,H);},

 // Chest: a broad ribcage that steps in at the waist, two layered pectoral plates beside the glowing sternum core.
 'ribcage':(o,w,h,d)=>{bevel(o,0,.05,0,w,h-.1,d,U,CORE);bevel(o,0,-h/2+.05,-.005,w-.06,.1,d-.03,U,CORE,'y');
  for(const x of [-1,1])plate(o,x*.135,.045,d/2+.004,.19,.13,.012,'z',U);
  for(const x of [-1,1])plate(o,x*(w/2+.003),.06,0,.008,.16,d-.08,'x',H);},
 'upper-chest':(o,w,h,d)=>{bevel(o,0,0,0,w,h,d,H,1,'xz');},
 'collar':(o,w,h,d)=>{bevel(o,0,0,0,w,h,d,H,1,'y');plate(o,0,h/2-.008,0,w-.08,.016,d-.07,'y',H,SEAM);},
 'sternum':(o,w,h,d)=>{o.push([0,0,0,w*.45,h,d]);bevel(o,0,.02,.004,w,.06,d,.012,1,'z');for(const x of [-1,1])o.push([x*.135,-.055,-.004,.17,.012,.012]);},
 'abdomen-shell':(o,w,h,d)=>{bevel(o,0,0,0,w,h,d,.008,CORE);o.push([0,h/2+.0075,0,w-.02,.015,d-.02,SEAM]);for(const y of [-.03,.03])plate(o,0,y,d/2+.003,.2,.048,.008,'z',H);},

 // Back: a plate split by the glowing spine line.
 'back-panel':(o,w,h,d)=>{bevel(o,0,0,0,w,h,d,U,CORE,'z');for(const x of [-1,1])plate(o,x*.075,-.01,-d/2-.004,.11,.16,.008,'z',H);},
 'armor-back-light':(o,w,h,d)=>{o.push([0,.065,0,w,.02,d],[0,-.0225,0,.025,.175,d]);},

 // Belt: hips with tassets, a glowing belt and buckle.
 'hip-shell':(o,w,h,d)=>{bevel(o,0,0,0,w,h,d,U,CORE);for(const x of [-1,1])plate(o,x*.1,-.035,d/2+.005,.16,.12,.01,'z',H);
  for(const x of [-1,1])plate(o,x*(w/2+.005),-.02,0,.01,.13,.18,'x',H);},
 'waist-band':(o,w,h,d)=>{bevel(o,0,0,0,w,h,d,H,1,'y');bevel(o,0,0,d/2+.006,.085,.075,.014,.012,1,'z');},

 // Pauldrons: three stepped layers, widest at the bottom.
 'shoulder-cap':(o,w,h,d,s)=>{plate(o,0,-h/2+.03,0,w-.004,.06,d,'y',.015);plate(o,-s*.005,.015,0,w-.03,.04,d-.03,'y',H);plate(o,-s*.012,h/2-.0125,0,w-.07,.025,d-.08,'y',H);},

 // Arms: tapered sleeve with an outer plate, a bracer with a flared top.
 'upper-arm':(o,w,h,d,s)=>{bevel(o,0,0,0,w,h,d,.017,CORE);plate(o,s*(w/2+.004),.01,0,.008,.14,d-.06,'x',H);},
 'elbow-joint':(o,w,h,d)=>{bevel(o,0,0,0,w,h,d,H);},
 'forearm':(o,w,h,d,s)=>{bevel(o,0,-.01,0,w,h-.02,d,.02,CORE);plate(o,0,h/2-.02,0,w+.012,.04,d+.012,'y',H);plate(o,s*(w/2+.004),-.03,0,.008,.11,d-.06,'x',H);},
 'cuff':(o,w,h,d)=>{bevel(o,0,0,0,w,h,d,H,1,'y');},

 // Gauntlets: an armored back of the hand, four fingers with knuckles wrapped round the grip, a thumb.
 'palm':(o,w,h,d)=>{bevel(o,0,0,0,w,h,d,.015);plate(o,0,.01,-d/2-.004,w-.04,h-.04,.008,'z',H,CORE);},
 'fingers':(o,w,h,d)=>{const f=w/4;for(let i=0;i<4;i++){const x=-w/2+f*(i+.5);o.push([x,0,0,f-.006,h,d,i%2?1:.93]);o.push([x,h/2-.012,d/2-.005,f-.004,.024,.014,1]);}o.push([0,0,-.01,w,h-.02,d-.03,SEAM]);},
 'thumb':(o,w,h,d)=>{bevel(o,0,0,0,w,h,d,.012);},

 // Legs: thigh plates, stepped knee guards, greaves with the glowing shin line.
 'hip-joint':(o,w,h,d)=>{bevel(o,0,0,0,w,h,d,H);},
 'thigh':(o,w,h,d,s)=>{bevel(o,0,0,0,w,h,d,.017,CORE);plate(o,0,.015,d/2+.005,w-.05,.15,.01,'z',H);plate(o,s*(w/2+.004),.03,0,.008,.14,d-.07,'x',H);},
 'knee-joint':(o,w,h,d)=>{bevel(o,0,0,0,w,h,d,H);},
 'kneecap':(o,w,h,d)=>{plate(o,0,0,-d*.2,w,h,d*.6,'z',.015);plate(o,0,-.005,d*.2,w-.045,h-.035,d*.4,'z',H);},
 'shin':(o,w,h,d)=>{bevel(o,0,0,0,w,h,d,.02,CORE);plate(o,0,-.01,d/2+.004,w-.045,.18,.008,'z',H);},
 'calf':(o,w,h,d)=>{bevel(o,0,0,0,w,h,d,H);},
 'armor-shin-light':(o,w,h,d)=>{o.push([0,0,.004,w,h,d]);},

 // Boots: dark soles, an ankle cuff and a stepped toe cap.
 'heel':(o,w,h,d)=>{bevel(o,0,.01,0,w,h-.02,d,H,1,'y');o.push([0,-h/2+.01,0,w,.02,d,.55]);},
 'foot':(o,w,h,d)=>{o.push([0,-h/2+.01,0,w,.02,d,.55]);bevel(o,0,.01,-.006,w-.01,h-.02,d-.012,H);plate(o,0,-.006,d/2-.028,w-.03,.04,.06,'y',H);},
};

export const ARMOR_VARIANTS:Record<string,Record<string,ArmorBuilder>>={default:DEFAULT_ARMOR};

/** The builder for a rig part name ('left-forearm' -> 'forearm'), or undefined for plain parts. */
export function armorBuilder(name:string,variant='default'):ArmorBuilder|undefined{
 const set=ARMOR_VARIANTS[variant]||DEFAULT_ARMOR;return set[name.replace(/^(left|right)-/,'')];
}
