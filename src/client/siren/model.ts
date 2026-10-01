import * as T from 'three';
import type {CharacterRig} from '../character-rig.js';
import {InkMaterial,glowMaterial,orbMaterial,haloMaterial,decalMaterial,particleMaterial,PALETTE,SIREN_CLOCK} from './materials.js';
import {FlameField} from './flames.js';

/**
 * The Siren apparition, built onto the standard rig skeleton so every joint, socket and pose helper still applies:
 * a black-flame body with a pale burning rim, legs that thin into flame wisps, big blank white eyes, a jagged
 * white grin, long black claws (melee) that retract when the hands ignite into black-fire orbs (ranged).
 */
export interface SirenParts {
 root:T.Group;
 eyes:T.Mesh[];eyeHalos:T.Sprite[];
 grin:T.Group;grinUpper:T.Mesh;grinLower:T.Mesh;
 claws:[T.Group,T.Group];orbs:[T.Mesh,T.Mesh];orbHalos:[T.Sprite,T.Sprite];
 charge:T.Mesh;chargeHalo:T.Sprite;wisps:T.Mesh[];tongues:{group:T.Group;phase:number;height:number;lean:number}[];shadow:T.Mesh;flames:FlameField;
 /** Smoothed presentation state, advanced by animateSiren. */
 state:{ranged:number;open:number;squint:number;glow:number;emitAt:number;lastTime:number;twitchAt:number;twitchSide:number;flare:number;lean:number;time:number;anger:number;angerShown:number};
}
const PARTS=new WeakMap<T.Object3D,SirenParts>();
export const sirenParts=(rig:{group:T.Object3D})=>PARTS.get(rig.group);

const INK=new InkMaterial({},.2,.025);
/** Claws are thin: a stronger rim keeps their edges visible. */
const CLAW=new InkMaterial({color:0x07060a,side:T.DoubleSide},.75,.02);
const TONGUE=new InkMaterial({color:0x0b0911},.42,.1);
const EYE=glowMaterial(PALETTE.glow,{intensity:1.15});
const TEETH=glowMaterial(PALETTE.teeth,{intensity:1});
const ORB=orbMaterial(PALETTE.ink,PALETTE.ash,1.6);
const EYE_HALO=haloMaterial(0xf4f0ff,.42),ORB_HALO=haloMaterial(0xd8ceff,.32);
const SHADOW=decalMaterial(0x000000,.55);
const FLAME_MATERIAL=particleMaterial();
const box=new T.BoxGeometry(1,1,1),orbShape=new T.IcosahedronGeometry(1,2),shadowShape=new T.CircleGeometry(1,32);

/** Body parts that disappear: feet, heels, calves and toes become flame. */
const HIDDEN=/-(foot|heel|calf|shin)$|^armor-/;
/** Flame tongues: stacked voxels that lick upward from the crown, shoulders and back. [joint,x,y,z,height,lean] */
const TONGUES:[('head'|'leftShoulder'|'rightShoulder'|'chest'),number,number,number,number,number][]=[
 ['head',-.115,.33,.01,.85,-.2],['head',-.045,.35,-.03,1.2,-.32],['head',.04,.35,-.01,1.05,-.28],['head',.115,.33,.02,.8,-.18],['head',0,.31,-.12,1.0,-.62],['head',-.09,.29,-.13,.75,-.7],['head',.09,.29,-.12,.7,-.7],
 ['leftShoulder',.1,.07,0,.8,-.15],['leftShoulder',.17,.04,-.05,.6,-.3],
 ['rightShoulder',-.1,.07,0,.8,-.15],['rightShoulder',-.17,.04,-.05,.6,-.3],
 ['chest',-.11,.31,-.12,.95,-.45],['chest',.09,.32,-.13,1.05,-.5],['chest',0,.22,-.17,.85,-.75],['chest',-.2,.12,-.15,.6,-.7],['chest',.2,.12,-.15,.6,-.7],
];
const TONGUE_STEPS:[number,number][]=[[.074,.06],[.062,.062],[.05,.062],[.04,.06],[.03,.056],[.021,.05],[.012,.044]];
/** Below each knee the leg becomes a tapering, flickering wisp. [y,size] */
const WISP_STEPS:[number,number][]=[[-.06,.15],[-.14,.122],[-.21,.095],[-.28,.07],[-.34,.048],[-.39,.03]];

/** Head-local grin: a smiling crescent filled by interlocking sharp teeth (upper row points down, lower row up). */
const GRIN={halfWidth:.146,upperY:.08,upperK:2.6,lowerY:.018,lowerK:4.6,teeth:11,inset:.15,reach:.86};
const upperArc=(x:number)=>GRIN.upperY+GRIN.upperK*x*x,lowerArc=(x:number)=>GRIN.lowerY+GRIN.lowerK*x*x;
function grinRow(upper:boolean){
 const out:number[]=[],w=GRIN.halfWidth,n=GRIN.teeth,step=2*w/n,count=upper?n:n-1;
 for(let i=0;i<count;i++){
  const a=-w+(upper?i:i+.5)*step,b=a+step,mid=(a+b)/2;
  // Counter-clockwise seen from the front (+z), so the teeth face out of the head.
  const tri=upper?[[a,upperArc(a)],[mid,lowerArc(mid)+(upperArc(mid)-lowerArc(mid))*(1-GRIN.reach)],[b,upperArc(b)]]
   :[[a,lowerArc(a)],[b,lowerArc(b)],[mid,upperArc(mid)-(upperArc(mid)-lowerArc(mid))*(1-GRIN.reach)]];
  // Inset each tooth toward its centroid so thin black gaps separate them (the printed-mask look).
  const cx=(tri[0][0]+tri[1][0]+tri[2][0])/3,cy=(tri[0][1]+tri[1][1]+tri[2][1])/3;
  for(const [x,y] of tri)out.push(x+(cx-x)*GRIN.inset,y+(cy-y)*GRIN.inset,0);
 }
 const g=new T.BufferGeometry();g.setAttribute('position',new T.Float32BufferAttribute(out,3));g.setAttribute('uv',new T.Float32BufferAttribute(new Array(out.length/3*2).fill(0),2));g.computeVertexNormals();return g;
}
const GRIN_UPPER=grinRow(true),GRIN_LOWER=grinRow(false);
/** A blank oval eye whose top can be cut by a brow line sloping toward the nose: anger 0 is a pure oval (the blank
 * stare), anger 1 a slanted almond (attacks, laughter). The vertices are rewritten in place; side 1 = left eye. */
const EYE_SEGMENTS=40;
function eyeGeometry(){
 const g=new T.BufferGeometry();g.setAttribute('position',new T.Float32BufferAttribute(new Float32Array(EYE_SEGMENTS*9),3));g.setAttribute('uv',new T.Float32BufferAttribute(new Float32Array(EYE_SEGMENTS*6),2));g.setAttribute('normal',new T.Float32BufferAttribute(new Float32Array(EYE_SEGMENTS*9).map((_,i)=>i%3===2?1:0),3));return g;
}
export function shapeEye(g:T.BufferGeometry,side:number,anger:number){
 const pos=g.getAttribute('position') as T.BufferAttribute,cut=1.25-1.07*Math.min(1,Math.max(0,anger)),pt=(i:number)=>{const a=i/EYE_SEGMENTS*Math.PI*2,x=Math.cos(a);return [x,Math.min(Math.sin(a),cut+.5*x*side)];};
 for(let i=0;i<EYE_SEGMENTS;i++){const a=pt(i),b=pt(i+1);pos.setXYZ(i*3,0,-.15,0);pos.setXYZ(i*3+1,a[0],a[1],0);pos.setXYZ(i*3+2,b[0],b[1],0);}
 pos.needsUpdate=true;g.computeBoundingSphere();
}

/** A hooked claw blade along -y (down the finger line) curling toward +z, tapering to a point. */
function clawGeometry(length:number,width:number,thick:number,hook:number){
 const seg=7,pos:number[]=[],idx:number[]=[],ds=length/seg;let y=0,z=0;
 for(let i=0;i<=seg;i++){const t=i/seg;if(i){const a=hook*((t-.5/seg))**2;y-=Math.cos(a)*ds;z+=Math.sin(a)*ds;}
  const wv=width*(1-t)**.85+.002,th=thick*(1-t*.75);
  pos.push(-wv/2,y,z-th/2, wv/2,y,z-th/2, wv/2,y,z+th/2, -wv/2,y,z+th/2);}
 for(let i=0;i<seg;i++){const a=i*4,b=a+4;for(let k=0;k<4;k++){const k2=(k+1)%4;idx.push(a+k,b+k2,b+k, a+k,a+k2,b+k2);}}
 idx.push(0,2,1,0,3,2);const g=new T.BufferGeometry();g.setAttribute('position',new T.Float32BufferAttribute(pos,3));g.setIndex(idx);g.computeVertexNormals();return g;
}
const CLAW_LONG=clawGeometry(.30,.032,.022,1.1),CLAW_THUMB=clawGeometry(.17,.028,.02,1.25);

function mesh(geometry:T.BufferGeometry,material:T.Material,name:string,parent:T.Object3D){const m=new T.Mesh(geometry,material);m.name=name;parent.add(m);return m;}

export function buildSiren(rig:CharacterRig):SirenParts{
 const existing=PARTS.get(rig.group);if(existing)return existing;
 const root=new T.Group();root.name='siren-apparition';rig.group.add(root);
 // Re-skin the standard parts; nothing is removed, so ragdolls, voxel bursts and LOD keep working.
 rig.group.traverse(o=>{if(!(o instanceof T.Mesh)||o.name.startsWith('siren-'))return;o.material=INK;if(HIDDEN.test(o.name))o.visible=false;if(/-shin$/.test(o.name))o.scale.x*=.78,o.scale.z*=.78;});
 for(const s of [rig.sockets.head,rig.sockets.chest,rig.sockets.back,rig.weapon,rig.offWeapon])s.visible=false;

 // Eyes: big blank ovals, slanted down toward the nose, with soft halos.
 const eyes:T.Mesh[]=[],eyeHalos:T.Sprite[]=[];
 for(const side of [1,-1]){const geometry=eyeGeometry();shapeEye(geometry,side,.4);const eye=mesh(geometry,EYE,'siren-eye',rig.head);eye.userData.side=side;eye.position.set(side*.082,.206,.1645);eye.scale.set(.05,.08,1);eye.rotation.z=side*.12;eyes.push(eye);
  const halo=new T.Sprite(EYE_HALO);halo.name='siren-eye-halo';halo.position.set(side*.079,.21,.2);halo.scale.set(.26,.26,1);rig.head.add(halo);eyeHalos.push(halo);}
 // Grin: two rows so the jaw can open (laugh) without breaking the teeth.
 const grin=new T.Group();grin.name='siren-grin';grin.position.set(0,0,.1638);rig.head.add(grin);
 const grinUpper=mesh(GRIN_UPPER,TEETH,'siren-teeth-upper',grin),grinLower=mesh(GRIN_LOWER,TEETH,'siren-teeth-lower',grin);

 // Claws on each hand (three blades and a thumb hook), and the ranged palm orb.
 const claws:T.Group[]=[],orbs:T.Mesh[]=[],orbHalos:T.Sprite[]=[];
 for(const [hand,side] of [[rig.leftHand,1],[rig.rightHand,-1]] as const){
  const g=new T.Group();g.name='siren-claws';g.position.set(0,-.1,.06);hand.add(g);claws.push(g);
  for(const x of [-.046,0,.046]){const c=mesh(CLAW_LONG,CLAW,'siren-claw',g);c.position.set(x,0,0);c.rotation.z=x*1.6*side;c.scale.y=x===0?1.08:.95;}
  const thumb=mesh(CLAW_THUMB,CLAW,'siren-claw-thumb',g);thumb.position.set(-side*.075,.04,-.02);thumb.rotation.set(.2,0,-side*.55);
  const orb=mesh(orbShape,ORB,'siren-orb',hand);orb.position.set(0,-.13,.07);orb.scale.setScalar(.001);orbs.push(orb);
  const halo=new T.Sprite(ORB_HALO);halo.name='siren-orb-halo';halo.position.set(0,-.13,.07);halo.scale.setScalar(.001);hand.add(halo);orbHalos.push(halo);
 }
 // Legs thin into stepped flame wisps below the knees.
 const wisps:T.Mesh[]=[];
 for(const knee of [rig.leftKnee,rig.rightKnee])for(const [y,s] of WISP_STEPS){const w=mesh(box,TONGUE,'siren-wisp',knee);w.position.set(0,y,-.01);w.scale.set(s,.1,s);w.userData.base=[y,s];wisps.push(w);}
 // Flame tongues crown the head and lick off the shoulders and back.
 const tongues:SirenParts['tongues']=[];
 TONGUES.forEach(([joint,x,y,z,height,lean],i)=>{const g=new T.Group();g.name='siren-tongue';g.position.set(x,y,z);(rig[joint] as T.Object3D).add(g);let at=0;
  for(const [size,h] of TONGUE_STEPS){const b=mesh(box,TONGUE,'siren-tongue-step',g);b.position.set(0,at+h/2,0);b.scale.set(size,h,size);b.userData.at=at;at+=h*.9;}
  tongues.push({group:g,phase:i*1.7+x*9,height,lean});});
 // Damnation's swelling ball, held overhead between the hands during the windup.
 const charge=mesh(orbShape,ORB,'siren-charge',root);charge.visible=false;const chargeHalo=new T.Sprite(haloMaterial(0x8f78e8,.6));chargeHalo.name='siren-charge-halo';chargeHalo.visible=false;root.add(chargeHalo);
 // A dark pool on the floor under the hovering body.
 const shadow=mesh(shadowShape,SHADOW,'siren-shadow',root);shadow.rotation.x=-Math.PI/2;shadow.position.y=-.835;shadow.scale.setScalar(.8);shadow.renderOrder=-1;
 // Black flame in world space; the mesh rides the rig but cancels its transform.
 const flames=new FlameField(420,FLAME_MATERIAL);flames.mesh.matrixAutoUpdate=false;root.add(flames.mesh);
 rig.group.traverse(o=>{if(o.name.startsWith('siren-')&&o instanceof T.Mesh){o.castShadow=o.name!=='siren-shadow'&&!o.name.startsWith('siren-teeth')&&o.name!=='siren-eye'&&o.name!=='siren-flames';o.receiveShadow=false;}});
 const parts:SirenParts={root,eyes,eyeHalos,grin,grinUpper,grinLower,claws:claws as [T.Group,T.Group],orbs:orbs as [T.Mesh,T.Mesh],orbHalos:orbHalos as [T.Sprite,T.Sprite],charge,chargeHalo,wisps,tongues,shadow,flames,
  state:{ranged:0,open:0,squint:0,glow:1,emitAt:0,lastTime:NaN,twitchAt:2+Math.random()*3,twitchSide:1,flare:0,lean:0,time:0,anger:.4,angerShown:-1}};
 PARTS.set(rig.group,parts);return parts;
}

/** Apply the smoothed face/hand state to the meshes (called every frame by animateSiren). */
export function applySirenLook(parts:SirenParts){
 const s=parts.state,ranged=s.ranged;
 for(const g of parts.claws){const k=1-.68*ranged;g.scale.set(1,k,1);g.visible=k>.05;}
 for(let i=0;i<2;i++){const r=.085*(.15+.85*ranged)*(1+.12*Math.sin(SIREN_CLOCK.value*12+i))*(1+s.flare*.6);parts.orbs[i].scale.setScalar(ranged>.02?r:.001);parts.orbs[i].visible=ranged>.02;parts.orbHalos[i].scale.setScalar(ranged>.02?r*4.2:.001);parts.orbHalos[i].visible=ranged>.02;}
 parts.grinUpper.position.y=.006*s.open;parts.grinLower.position.y=-.03*s.open;parts.grinLower.scale.set(1+.06*s.open,1+.5*s.open,1);
 const anger=Math.round(s.anger*40)/40;if(anger!==s.angerShown){s.angerShown=anger;for(const e of parts.eyes)shapeEye(e.geometry,e.userData.side as number,anger);}
 for(const [i,e] of parts.eyes.entries()){e.scale.set(.05*(1+.06*s.open),.08*(1-.3*s.squint),1);(parts.eyeHalos[i].material as T.SpriteMaterial).opacity=.42*s.glow;}
 // Living flame: tongues flicker in height and whip sideways; moving bends them back.
 const t=s.time;
 for(const g of parts.tongues){const n=.5+.5*Math.sin(t*7.3+g.phase)*Math.sin(t*3.1+g.phase*1.9);g.group.scale.set(1,g.height*(.7+.55*n)*(1+s.flare*.5),1);
  g.group.rotation.set(g.lean-.5*s.lean+.14*Math.sin(t*2.6+g.phase),0,.24*Math.sin(t*3.4+g.phase*1.3));
  // Each step sways a little more than the one below it: the tongue writhes like a flame instead of standing like a post.
  g.group.children.forEach((c,i)=>{c.position.x=.012*i*Math.sin(t*6.1+g.phase+i*.7);c.position.z=.008*i*Math.sin(t*5.3+g.phase*1.3+i*.9);});}
 for(const [i,w] of parts.wisps.entries()){const [y,size]=w.userData.base as [number,number],k=i%WISP_STEPS.length,f=k/WISP_STEPS.length;
  w.position.set(.035*f*Math.sin(t*4.1+i*1.3),y-.01*Math.sin(t*5+i),-.01-.12*f*s.lean);w.scale.set(size*(.85+.25*Math.sin(t*6.2+i*2.1)),.1,size*(.85+.25*Math.sin(t*5.3+i)));}
}
