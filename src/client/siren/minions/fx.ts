import * as T from 'three';
import {hash01} from './look.js';
import {FX_MATERIAL,MINION_CUBE,MINION_PALETTE} from './materials.js';

/**
 * Per-minion effect cubes in ONE instanced draw (child of the rig group, hidden when idle): the black-flame pool a minion
 * claws out of, and the flames, embers and ash it burns away into. A pure function of the emergence/death ages and the
 * seed (no particle state, no random number calls), so it is identical on every client, at any frame rate, and when rewound.
 */
export const EMERGE_SECONDS=1.95;
export const DEATH_SECONDS=2.3;
/** How long the death effects linger after the body is gone. */
export const DEATH_FX_SECONDS=3.1;
const POOL=8,FLAMES=36,SPARKS=20,COUNT=POOL+FLAMES+SPARKS;
const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
const sm=(x:number)=>{x=clamp(x);return x*x*(3-2*x);};
const m=new T.Matrix4(),q=new T.Quaternion(),e=new T.Euler(),p=new T.Vector3(),s=new T.Vector3(),c=new T.Color(),ZERO=new T.Matrix4().makeScale(0,0,0);
const INK=new T.Color(MINION_PALETTE.char),SMOKE=new T.Color(0x3b3150),RIM=new T.Color(0xcfc6ea),EMBER=new T.Color(MINION_PALETTE.ember),HOT=new T.Color(MINION_PALETTE.emberHot),ASH=new T.Color(0x8e8a86);

export class MinionFx {
 readonly mesh:T.InstancedMesh;
 constructor(readonly seed:number,readonly floor:number){
  this.mesh=new T.InstancedMesh(MINION_CUBE,FX_MATERIAL,COUNT);this.mesh.name='siren-minion-fx';this.mesh.frustumCulled=false;this.mesh.castShadow=false;this.mesh.receiveShadow=false;
  this.mesh.instanceMatrix.setUsage(T.DynamicDrawUsage);for(let i=0;i<COUNT;i++){this.mesh.setMatrixAt(i,ZERO);this.mesh.setColorAt(i,c.setRGB(0,0,0));}
  this.mesh.instanceColor!.setUsage(T.DynamicDrawUsage);this.mesh.visible=false;
 }
 /**
  * emerge: seconds since the emergence started (undefined when not emerging); death: seconds since death (undefined when
  * alive); points: rig-group positions of a few body joints (flames rise from them while the body burns).
  */
 update(emerge:number|undefined,death:number|undefined,points:readonly T.Vector3[],quality=1){
  const em=emerge!==undefined&&emerge>=0&&emerge<EMERGE_SECONDS+.4,de=death!==undefined&&death>=0&&death<DEATH_FX_SECONDS;
  if(!em&&!de){if(this.mesh.visible){this.mesh.visible=false;}return;}
  this.mesh.visible=true;const seed=this.seed,lowQ=quality<.5;let live=0;
  // The pool: overlapping rotated slabs make a ragged black disc; under a dying minion it is the scorch it leaves.
  const R=em?.78*sm(emerge!/.3)*(1-sm((emerge!-EMERGE_SECONDS+.2)/.5)):.62*sm((death!-.6)/.8)*(1-sm((death!-2.5)/.6));
  for(let i=0;i<POOL;i++){if(R<.01){this.mesh.setMatrixAt(i,ZERO);continue;}const a=i*Math.PI/POOL+.3*hash01(seed,i,1),r=R*(.82+.3*hash01(seed,i,2));
   q.setFromEuler(e.set(0,a,0));m.compose(p.set(0,this.floor+.004+i*.0004,0),q,s.set(2*r,.006,.62*r));this.mesh.setMatrixAt(i,m);this.mesh.setColorAt(i,c.copy(INK).lerp(SMOKE,i===0?.25:.05));live++;}
  // Flames: tongues that swell then shrink as they rise. Emerging: around the rim; dying: off the body.
  const flames=lowQ?FLAMES/2:FLAMES;
  for(let i=0;i<FLAMES;i++){const k=POOL+i;if(i>=flames){this.mesh.setMatrixAt(k,ZERO);continue;}
   const life=.45+.4*hash01(seed,i,3),span=em?EMERGE_SECONDS-.2:1.9,start=em?0:.45,age=em?emerge!:death!;
   // Each flame repeats with its own period inside the active window, so the fire never empties until the end.
   const period=life+.15*hash01(seed,i,4),off=hash01(seed,i,5)*period,cycle=Math.floor((age-start-off)/period),born=start+off+cycle*period,u=(age-born)/life;
   if(cycle<0||born>start+span||u<0||u>1){this.mesh.setMatrixAt(k,ZERO);continue;}
   const h1=hash01(seed,i,cycle,6),h2=hash01(seed,i,cycle,7),h3=hash01(seed,i,cycle,8);let x=0,y=0,z=0,size=.045+.045*h3;
   if(em){const a=h1*Math.PI*2,r=R*(.75+.25*h2);x=Math.cos(a)*r;z=Math.sin(a)*r;y=this.floor;size*=.9+.4*sm((emerge!-.3)/.6);}
   else{const pt=points[Math.floor(h1*points.length)%points.length];if(!pt){this.mesh.setMatrixAt(k,ZERO);continue;}x=pt.x+(h2-.5)*.28;y=pt.y+(h3-.5)*.2;z=pt.z+(hash01(seed,i,cycle,9)-.5)*.28;size*=.9+.4*sm((death!-.5)/.6);}
   const rise=.9*u+1.3*u*u,grow=u<.22?u/.22:1-((u-.22)/.78)**1.4,edge=size*Math.max(0,grow);
   q.setFromEuler(e.set((h2-.5)*.4,h1*6.3+u*2*(h3-.5),(h3-.5)*.4));m.compose(p.set(x+(h2-.5)*.12*u,y+rise*.55,z+(h1-.5)*.12*u),q,s.set(edge*.8,edge*1.8,edge*.8));this.mesh.setMatrixAt(k,m);
   this.mesh.setColorAt(k,c.copy(INK).lerp(h1<.12?RIM:SMOKE,u*u*u*(h1<.12?.6:.8)));live++;
  }
  // Sparks: pale violet embers rising (emerging) and ash flakes drifting off the burning body (dying).
  for(let i=0;i<SPARKS;i++){const k=POOL+FLAMES+i;if(lowQ&&i%2){this.mesh.setMatrixAt(k,ZERO);continue;}
   const age=em?emerge!:death!,born=(em?.2:.6)+hash01(seed,i,10)*(em?1.4:1.6),life=.7+.6*hash01(seed,i,11),u=(age-born)/life;
   if(u<0||u>1){this.mesh.setMatrixAt(k,ZERO);continue;}
   const h1=hash01(seed,i,12),h2=hash01(seed,i,13);let x:number,y:number,z:number;
   if(em){const a=h1*Math.PI*2,r=R*(.3+.6*h2);x=Math.cos(a)*r;z=Math.sin(a)*r;y=this.floor+.05+u*(.6+.8*h2);}
   else{const pt=points[i%Math.max(1,points.length)];if(!pt){this.mesh.setMatrixAt(k,ZERO);continue;}x=pt.x+(h1-.5)*.5*u+(h1-.5)*.2;z=pt.z+(h2-.5)*.5*u;y=pt.y+u*(.5+.6*h1)-u*u*.3;}
   const size=em?.018+.014*h2:.022+.02*h2,fade=1-sm((u-.6)/.4);q.setFromEuler(e.set(u*7*h1,u*5*h2,u*3));
   m.compose(p.set(x,y,z),q,s.set(size*fade,(em?size:size*.25)*fade,size*fade));this.mesh.setMatrixAt(k,m);
   this.mesh.setColorAt(k,em?c.copy(EMBER).lerp(HOT,h1<.25?.7:0):c.copy(ASH).lerp(h2<.2?EMBER:INK,h2<.2?.8:.45*u));live++;
  }
  this.mesh.instanceMatrix.needsUpdate=true;this.mesh.instanceColor!.needsUpdate=true;this.mesh.visible=live>0;
 }
 dispose(){this.mesh.removeFromParent();this.mesh.dispose();}
}
