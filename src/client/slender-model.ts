// Scare Tactics' Slenderman model (owner request 2026-10-04): a tall, thin, faceless figure in a black suit, white shirt and
// black tie, pale featureless head, over-long arms and legs and six retractable back tentacles. Rigid voxel parts on the
// shared rig cube and Lambert palette (the pilots' program: nothing new to compile). Animation is allocation-free.
import * as T from 'three';
import {rigBox} from './character-rig.js';

const SUIT=0x0b0b0e,SHIRT=0xe6e6e0,TIE=0x030304,SKIN=0xf4f1ea,SHOE=0x050505;
export const SLENDER_LOOK={height:2.55,tentacles:6,segments:5} as const;
function joint(parent:T.Object3D,name:string,x:number,y:number,z:number){const g=new T.Group();g.name=name;g.position.set(x,y,z);parent.add(g);return g;}

/** Feet at the group origin, facing +z. */
export function buildSlender(){
 const root=new T.Group();root.name='slender';const body=joint(root,'slender-body',0,0,0);
 for(const side of [-1,1]){const hip=joint(body,side<0?'slender-hip-r':'slender-hip-l',side*.1,1.24,0);
  rigBox(hip,'slender-thigh',0,-.31,0,.13,.64,.15,SUIT);const knee=joint(hip,'slender-knee',0,-.62,0);rigBox(knee,'slender-shin',0,-.3,0,.12,.6,.14,SUIT);rigBox(knee,'slender-shoe',0,-.6,.07,.13,.07,.3,SHOE);}
 const torso=joint(body,'slender-torso',0,1.22,0);
 rigBox(torso,'slender-trousers',0,.04,0,.34,.16,.2,SUIT);rigBox(torso,'slender-jacket',0,.42,0,.4,.66,.22,SUIT);rigBox(torso,'slender-shoulders',0,.72,0,.54,.09,.24,SUIT);
 rigBox(torso,'slender-shirt',0,.55,.112,.15,.4,.01,SHIRT);rigBox(torso,'slender-tie',0,.52,.12,.055,.38,.012,TIE);rigBox(torso,'slender-knot',0,.72,.122,.07,.05,.014,TIE);
 for(const side of [-1,1]){const lapel=rigBox(torso,'slender-lapel',side*.085,.56,.118,.06,.36,.012,0x17171c);lapel.rotation.z=side*.22;}
 rigBox(torso,'slender-neck',0,.83,0,.08,.14,.08,SKIN);
 const head=joint(torso,'slender-head',0,.9,0);rigBox(head,'slender-skull',0,.18,0,.23,.34,.25,SKIN);rigBox(head,'slender-crown',0,.37,0,.19,.06,.21,SKIN);rigBox(head,'slender-chin',0,-.01,.01,.17,.05,.19,SKIN);
 for(const side of [-1,1]){const shoulder=joint(torso,side<0?'slender-arm-r':'slender-arm-l',side*.27,.7,0);rigBox(shoulder,'slender-upper-arm',0,-.32,0,.1,.64,.11,SUIT);
  const elbow=joint(shoulder,'slender-elbow',0,-.64,0);rigBox(elbow,'slender-forearm',0,-.3,0,.09,.6,.1,SUIT);rigBox(elbow,'slender-cuff',0,-.6,0,.085,.04,.095,SHIRT);
  rigBox(elbow,'slender-hand',0,-.7,0,.07,.16,.04,SKIN);for(let i=0;i<3;i++)rigBox(elbow,'slender-finger',(i-1)*.022,-.86,0,.018,.18,.018,SKIN);}
 // Tentacles: chains of segments rooted between the shoulder blades; extension scales the segments from 0 to full.
 const back=joint(torso,'slender-back',0,.62,-.12);
 for(let i=0;i<SLENDER_LOOK.tentacles;i++){const a=(i/(SLENDER_LOOK.tentacles-1)-.5)*2.4;let parent:T.Object3D=joint(back,'slender-tentacle',0,0,0);parent.rotation.set(-.5+Math.abs(a)*.1,a*.6,a*.5);parent.userData.index=i;
  for(let n=0;n<SLENDER_LOOK.segments;n++){const seg=joint(parent,'slender-seg',0,0,n?-.26:0);rigBox(seg,'slender-tendril',0,0,-.13,.045-n*.006,.045-n*.006,.27,SUIT);parent=seg;}}
 root.traverse(o=>{if(o instanceof T.Mesh)o.castShadow=true;});
 return root;
}

/** t: authoritative time; seen: his target looks at him; hopAge: s since the last teleport (Infinity: none); reach 0..1. */
export function animateSlender(g:T.Object3D,t:number,seen:boolean,hopAge:number,reach:number){
 const body=g.children[0],torso=body.getObjectByName('slender-torso')!,head=torso.getObjectByName('slender-head')!;
 // Observed: dead still apart from a slow head tilt toward the viewer. Unobserved: the long sway.
 const sway=seen?0:1;body.rotation.z=Math.sin(t*.7)*.035*sway;torso.rotation.x=.05+Math.sin(t*.45)*.02*sway;
 head.rotation.z=.28+Math.sin(t*.31)*.08*sway+(seen?.12:0);head.rotation.x=-.06+(seen?.1:0);
 for(const o of torso.children)if(o.name==='slender-arm-l'||o.name==='slender-arm-r'){const s=o.name==='slender-arm-l'?1:-1;o.rotation.set(Math.sin(t*.6+s)*.05*sway,0,s*.06+reach*s*.25);(o.children[1]as T.Object3D).rotation.x=-.08-reach*.6;}
 // Tentacles unfurl around a teleport and as he closes in.
 const ext=Math.max(Math.max(0,1-hopAge/.9),reach*.8,seen?0:.15),back=torso.getObjectByName('slender-back')!;
 back.visible=ext>.02;if(back.visible)for(const root of back.children){const i=root.userData.index as number;let seg:T.Object3D|undefined=root.children[0];let n=0;
  while(seg){seg.scale.setScalar(Math.max(.001,ext));seg.rotation.set(Math.sin(t*2.3+i*1.7+n*.8)*.35*ext,Math.cos(t*1.9+i+n)*.3*ext,0);seg=seg.children[1];n++;}}
 // Teleport glitch: the figure smears sideways and flickers for .4 s after a hop.
 const glitch=hopAge<.4?1-hopAge/.4:0;if(glitch>0){g.visible=Math.sin(hopAge*140)<=.35;body.position.x=glitch*Math.sin(hopAge*97)*.18;body.scale.set(1+glitch*.35*Math.abs(Math.sin(hopAge*61)),1,1);}else{g.visible=true;body.position.x=0;body.scale.set(1,1,1);}
}
