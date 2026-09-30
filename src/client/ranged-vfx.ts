import * as T from 'three';
import type {PlayerView} from '../shared/simulation.js';
import type {Vec} from '../shared/config.js';
import {WEAPONS} from '../shared/content.js';
import type {CharacterRig} from './character-rig.js';
import {spellPose} from './character-animation.js';
// Local copies of the weapon-visuals mesh helpers keep this module free of an import cycle with weapon-visuals.ts.
const sphere=new T.SphereGeometry(1,8,6),ring=new T.TorusGeometry(1,.055,4,16);
const mats=new Map<string,T.Material>();
function material(color:number,glow=false){const k=color+':'+glow;if(!mats.has(k))mats.set(k,glow?new T.MeshBasicMaterial({color}):new T.MeshLambertMaterial({color}));return mats.get(k)!;}
function mesh(parent:T.Object3D,shape:T.BufferGeometry,color:number,x:number,y:number,z:number,sx:number,sy:number,sz:number,glow=false){const m=new T.Mesh(shape,material(color,glow));m.position.set(x,y,z);m.scale.set(sx,sy,sz);m.castShadow=!glow;parent.add(m);return m;}
function socket(parent:T.Object3D,name:string,x:number,y:number,z:number){const g=new T.Group();g.name=name;g.position.set(x,y,z);parent.add(g);return g;}
const smooth01=(x:number)=>{x=Math.min(1,Math.max(0,x));return x*x*(3-2*x);};
/** Right-hand spell-charge orb (and the ansatsuken charge); weapon-visuals bodyModifiers calls it first.
 * The orb grows continuously with the cast's progress (no pops at the windup boundary), stays under ~.15 m world
 * radius with thin counter-rotating halos, flashes for 60 ms at the authoritative release and is gone by
 * release+.08 s so the projectile takes over. */
export function spellCharge(r:CharacterRig,p:PlayerView,time:number){
 let charge=r.rightHand.getObjectByName('spell-charge') as T.Group|undefined;
 if(charge?.userData.spec!==p.spec){charge?.removeFromParent();charge=socket(r.rightHand,'spell-charge',0,-.09,.02);charge.userData.spec=p.spec;mesh(charge,sphere,WEAPONS[p.spec].color,0,0,0,.1,.1,.1,true);for(let n=0;n<2;n++){const halo=mesh(charge,ring,WEAPONS[p.spec].color,0,0,0,.15,.15,.15,true);halo.scale.z=.5;halo.rotation.x=n*Math.PI/2;halo.name='spell-halo';}}
 const phrase=spellPose(p,time),age=phrase.age,R=phrase.releaseAt;
 if(p.spec==='ansatsuken'){charge.visible=p.hp>0&&p.arcade?.action==='charge';charge.rotation.set(0,time*8,0);if(charge.visible)charge.scale.setScalar(.5+p.arcade!.charge*1.2);return;}
 const live=phrase.active&&age<R+.08;charge.visible=p.hp>0&&live;if(!charge.visible)return;
 const progress=Math.min(1,age/Math.max(.01,R)),grow=(.35+.65*progress)*smooth01(age/.05);
 const flash=age>=R?1+.35*(1-Math.min(1,(age-R)/.06)):1,fade=age>R+.06?1-(age-R-.06)/.02:1;
 charge.scale.setScalar(Math.max(.01,grow*flash*Math.max(0,fade)));charge.rotation.set(0,0,0);
 let n=0;for(const halo of charge.children)if(halo.name==='spell-halo'){halo.rotation.set(n?Math.PI/2:0,(n?-7:9)*time,0);n++;}
}
/** Deterministic magic hand motes while casting (fixed-rate emitters per element; positions hashed from the emission index,
 * no Math.random here). burst is the renderer's pooled burst spawner. */
const moteClock=new WeakMap<CharacterRig,{t:number;n:number}>();
const hash=(x:number)=>{const v=Math.sin(x*91.37+17.1)*43758.5453;return v-Math.floor(v);};
const _tip=new T.Vector3();
export function spellMotes(r:CharacterRig,p:PlayerView,pose:{cast:number},dt:number,effectsQuality:number,burst:(pos:Vec,color:number,count:number,power:number)=>void){
 const w=WEAPONS[p.spec];if(!(p.hp>0&&w.class==='magic'&&pose.cast>.3&&effectsQuality>0)){moteClock.delete(r);return;}
 let c=moteClock.get(r);if(!c){c={t:0,n:0};moteClock.set(r,c);}
 const rate=p.spec==='lightning'?30:p.spec==='water'?18:22,step=1/rate;c.t+=Math.min(Math.max(dt,0),.05);
 while(c.t>=step){c.t-=step;const k=++c.n,h=hash(k+(p.lastAttack||0)),h2=hash(k*1.7+3.1);
  const a=k*(p.spec==='water'?.9:p.spec==='darkness'?-1.1:2.399),rad=p.spec==='darkness'?.14*(1-(k%6)/6):p.spec==='water'?.09:.05*h;
  r.rightHand.localToWorld(_tip.set(Math.cos(a)*rad,-.1+(p.spec==='fire'?.04*h2:0),.05+Math.sin(a)*rad));
  if(p.spec==='lightning')_tip.addScalar((h-.5)*.06);
  burst(_tip,w.color,p.spec==='lightning'?1:2,p.spec==='fire'?.7:p.spec==='darkness'?.2:.45);}
}
