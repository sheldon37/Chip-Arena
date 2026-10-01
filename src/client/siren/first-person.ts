import * as T from 'three';
import type {CharacterRig} from '../character-rig.js';
import type {PlayerView} from '../../shared/simulation.js';
import {SIREN,SIREN_TIMING,sirenOf} from '../../shared/siren-contract.js';
import {FlameField} from './flames.js';
import {InkMaterial,orbMaterial,glowMaterial,PALETTE,tickSirenClock} from './materials.js';

const ARM=/-(upper-arm|forearm|palm|fingers|thumb|cuff|elbow-joint|shoulder-cap)$/;
const INK=new InkMaterial({},.35,.03),CLAW=new InkMaterial({color:0x07060a,side:T.DoubleSide},.8,.02),ORB=orbMaterial(PALETTE.ink,PALETTE.ash,1.6),EDGE=glowMaterial(PALETTE.ash,{intensity:.55});
const blade=new T.BoxGeometry(1,1,1),ball=new T.IcosahedronGeometry(1,2),wp=new T.Vector3();
interface ViewModel {on:boolean;saved:Map<T.Mesh,T.Material|T.Material[]>;claws:T.Group[];orbs:T.Mesh[];flames:FlameField;acc:number;last:number}
const VIEWS=new WeakMap<object,ViewModel>();

function build(view:CharacterRig):ViewModel{
 const claws:T.Group[]=[],orbs:T.Mesh[]=[];
 for(const [hand,side] of [[view.leftHand,1],[view.rightHand,-1]] as const){
  const g=new T.Group();g.name='siren-view-claws';g.position.set(0,-.1,.06);hand.add(g);claws.push(g);
  // First-person claws: simple tapered blades (the full hooked claws are drawn on the world rig).
  for(const x of [-.045,0,.045]){const c=new T.Mesh(blade,CLAW);c.position.set(x*1.2,-.2,.06);c.scale.set(.03,.42,.018);c.rotation.set(.35,0,x*2.2*side);g.add(c);
   const edge=new T.Mesh(blade,EDGE);edge.position.set(x*1.2,-.2,.072);edge.scale.set(.006,.4,.004);edge.rotation.copy(c.rotation);g.add(edge);}
  const orb=new T.Mesh(ball,ORB);orb.name='siren-view-orb';orb.position.set(0,-.13,.07);orb.scale.setScalar(.001);hand.add(orb);orbs.push(orb);
 }
 const flames=new FlameField(120);flames.mesh.name='siren-view-flames';view.group.add(flames.mesh);
 return {on:false,saved:new Map(),claws,orbs,flames,acc:0,last:0};
}

/**
 * First-person Siren arms: inked arms, claws in melee mode, burning black orbs in the palms in ranged mode, a few
 * flames licking off the hands. Call every frame after poseFirstPersonView; for a non-Siren player (or undefined) it
 * restores the normal view model.
 */
export function sirenViewModel(view:CharacterRig,p:PlayerView|undefined,time:number,dt:number){
 const on=!!p&&(p.spec as string)===SIREN.spec&&p.hp>0;let v=VIEWS.get(view.group);
 if(!on){if(v?.on){for(const [m,mat] of v.saved)m.material=mat;v.saved.clear();for(const g of v.claws)g.visible=false;for(const o of v.orbs)o.visible=false;v.flames.clear();v.flames.mesh.visible=false;v.on=false;}return;}
 if(!v){v=build(view);VIEWS.set(view.group,v);}
 if(!v.on){view.group.traverse(o=>{if(o instanceof T.Mesh&&ARM.test(o.name)){v!.saved.set(o,o.material);o.material=INK;}});v.flames.mesh.visible=true;v.on=true;}
 tickSirenClock(time);view.weapon.visible=false;view.offWeapon.visible=false;
 const s=sirenOf(p!),swap=s?Math.min(1,Math.max(0,(time-s.modeAt)/SIREN_TIMING.swap)):1,ranged=s?.mode==='ranged'?swap:1-swap;
 // Bring the claws (or burning palms) up into view: forearms forward, wrists cocked so the claws point ahead.
 for(const [arm,elbow,hand,side] of [[view.leftArm,view.leftElbow,view.leftHand,1],[view.rightArm,view.rightElbow,view.rightHand,-1]] as const){
  const sway=Math.sin(time*1.7+side)*.03;arm.rotation.set(-1.05+sway,0,side*.18);elbow.rotation.set(-1.0-.25*ranged,0,0);hand.rotation.set(.55-1.25*ranged,0,side*(.15+.2*ranged));}
 for(const g of v.claws){g.visible=ranged<.95;g.scale.set(1,1-.75*ranged,1);}
 for(const [i,o] of v.orbs.entries()){o.visible=ranged>.05;o.scale.setScalar(.075*ranged*(1+.12*Math.sin(time*12+i)));}
 // Flames off the hands, in view-rig space so they stay with the arms.
 v.acc+=Math.max(0,Math.min(.05,dt))*(10+30*ranged);view.group.updateMatrixWorld(true);
 while(v.acc>=1){v.acc-=1;const hand=Math.random()<.5?view.leftHand:view.rightHand;wp.set((Math.random()-.5)*.08,-.12,.06);hand.localToWorld(wp);view.group.worldToLocal(wp);
  v.flames.emit(wp.x,wp.y,wp.z,(Math.random()-.5)*.1,.35+Math.random()*.3,(Math.random()-.5)*.1,.35+Math.random()*.2,.025+Math.random()*.02,Math.random()<.1?'ember':'black',1.2,2.4);}
 v.flames.update(Math.max(0,Math.min(.05,dt)));v.last=time;
}
