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
/** Right-hand spell-charge orb (and the ansatsuken charge), moved verbatim from weapon-visuals bodyModifiers, which calls it first. */
export function spellCharge(r:CharacterRig,p:PlayerView,time:number){
 let charge=r.rightHand.getObjectByName('spell-charge') as T.Group|undefined;
 if(charge?.userData.spec!==p.spec){charge?.removeFromParent();charge=socket(r.rightHand,'spell-charge',0,-.09,.02);charge.userData.spec=p.spec;mesh(charge,sphere,WEAPONS[p.spec].color,0,0,0,.14,.14,.14,true);for(let n=0;n<2;n++){const halo=mesh(charge,ring,WEAPONS[p.spec].color,0,0,0,.22,.22,.22,true);halo.rotation.x=n*Math.PI/2;}}
 const phrase=spellPose(p,time);charge.visible=p.hp>0&&(p.spec==='ansatsuken'?p.arcade?.action==='charge':phrase.active);charge.scale.setScalar(Math.max(.01,phrase.weight*(phrase.drive<.2?1+phrase.coil:.8)));charge.rotation.y=time*8;if(p.spec==='ansatsuken'&&p.arcade?.action==='charge')charge.scale.setScalar(.5+p.arcade.charge*1.2);
}
/** Magic hand motes while casting, moved verbatim from the renderer. burst is the renderer's pooled burst spawner. */
export function spellMotes(r:CharacterRig,p:PlayerView,pose:{cast:number},dt:number,effectsQuality:number,burst:(pos:Vec,color:number,count:number,power:number)=>void){
 const w=WEAPONS[p.spec];if(p.hp>0&&w.class==='magic'&&pose.cast>.3&&effectsQuality>0){const tip=r.rightHand.localToWorld(new T.Vector3(0,-.1,.05));if(Math.random()<dt*22)burst(tip,w.color,2,.5);}
}
