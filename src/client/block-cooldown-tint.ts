import * as T from 'three';
import type {PlayerView} from '../shared/simulation.js';

type Colored=T.MeshLambertMaterial|T.MeshBasicMaterial|T.MeshStandardMaterial;
type Tint={mesh:T.Mesh;original:Colored;material:Colored};
/** Private material copies prevent one player's cooldown from tinting another player's shared rig. */
export class BlockCooldownTint {
 private rigs=new Map<T.Group,Tint[]>();
 update(group:T.Group,p:Pick<PlayerView,'hp'|'blockCd'|'blockTime'>,time:number){
  const cooling=p.hp>0&&p.blockCd>0&&p.blockTime<=0;
  let entries=this.rigs.get(group);
  if(!cooling){this.release(group);return;}
  if(!entries){entries=[];group.traverse(o=>{if(!(o instanceof T.Mesh)||Array.isArray(o.material)||!('color'in o.material))return;
    for(let a:T.Object3D|null=o;a&&a!==group;a=a.parent)if(['weapon-socket','offhand-weapon-socket','timed-block','timed-guard','enchantment','spell-charge','body-chips'].includes(a.name))return;
    const original=o.material as Colored,material=original.clone() as Colored;o.material=material;entries!.push({mesh:o,original,material});
   });this.rigs.set(group,entries);}
  const pulse=.70+.30*Math.cos(time*Math.PI*2*1.5),factor=1-(1-pulse)*Math.min(1,p.blockCd/.25);
  for(const tint of entries){if(tint.mesh.material!==tint.material){tint.original=tint.mesh.material as Colored;tint.mesh.material=tint.material;}tint.material.color.copy(tint.original.color).multiplyScalar(factor);}
 }
 release(group:T.Group){const entries=this.rigs.get(group);if(!entries)return;for(const {mesh,original,material}of entries){if(mesh.material===material)mesh.material=original;material.dispose();}this.rigs.delete(group);}
 clear(){for(const group of this.rigs.keys())this.release(group);}
}
