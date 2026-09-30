import * as T from 'three';
import type {PlayerView} from '../shared/simulation.js';
import {WEAPONS} from '../shared/content.js';
import type {CharacterRig} from './character-rig.js';
import type {attackPose} from './character-animation.js';
import type {ProjectileTrails} from './projectile-trails.js';
/** Melee blade-tip sweep trails, moved verbatim from the renderer's per-player loop.
 * The renderer constructs one, calls sample() right after animateCharacter for every player, and update() once per frame. */
export class MeleeTrails{
 constructor(readonly trails:ProjectileTrails){}
 sample(r:CharacterRig,p:PlayerView,pose:ReturnType<typeof attackPose>,visualTime:number){
  const w=WEAPONS[p.spec];if(p.hp>0&&w.class==='melee'&&(pose.active&&pose.strike>0&&pose.strike<1||p.meleeAction?.released&&['sword-launch','wolf-spin'].includes(p.meleeAction.kind))){r.group.updateMatrixWorld(true);const tip=(p.spec==='daggers'&&(p.attackIndex||1)%2===0?r.offWeapon:r.weapon).getObjectByName('blade-tip')?.getWorldPosition(new T.Vector3());if(tip)this.trails.sample('sweep-'+p.id+'-'+p.lastAttack,tip,p.chips.ember?0xff953e:p.chips.poison?0x88ed80:p.chips['echo-strike']?0xc1a1ff:0xc9fff3,visualTime,6);}
 }
 update(dt:number,time:number){void dt;void time;}
 dispose(){}
}
