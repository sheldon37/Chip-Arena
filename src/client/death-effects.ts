import {SIREN_KILL_EFFECTS} from './siren/index.js';
import {RAGDOLL_STYLES,type RagdollStyle} from '../shared/ragdoll-styles.js';
import {ragdoll,ragdollVariant} from './ragdoll.js';
import {ragingDemon} from './raging-demon.js';
import {clownLove} from './clown-love.js';
import {bassVan} from './bass-van.js';
import {cinematic} from './kill-cinematics.js';
import * as T from 'three';
import type {DeathPresentation,PlayerView,Snapshot} from '../shared/simulation.js';
import {ARENA} from '../shared/arena.js';
import {ANIMATION} from './character-animation.js';
import type {CharacterRig} from './character-rig.js';
import {DOWNED,downedBodies,type DownedBodies} from './pve/downed.js';
import {VENT_KILL_EFFECTS} from './pve/vent-fall.js';

interface Voxel {mesh:T.Mesh;start:T.Vector3;velocity:T.Vector3;spin:T.Vector3;floor:number;size:number;}
interface Effect {update(age:number):void;dispose():void;count:number;
 /** PvE kept body (src/client/pve/downed.ts): the frame shown while its member stays down, from the death's real age. Without
  * it a kept body holds update(min(age, keepAge ?? DOWNED.keepAge)). */
 keep?(age:number):void;keepAge?:number;
 /** Loose scene objects (blood pools, fragments, flames) a regeneration cleans up; the effect still owns them. */
 extras?:T.Object3D[];
 /** A scripted body (e.g. the Siren crush victim) a regeneration can rebuild and stand up from. */
 rig?:CharacterRig;
 /** The effect's scene root (ragdolls: part groups named after the rig joints they copy). */
 root?:T.Object3D;}
export type KillEffectFactory=(scene:T.Scene,rig:T.Group,p:PlayerView,death:DeathPresentation,quality:number,budget:number,state?:Snapshot)=>Effect;
const geometry=new T.BoxGeometry(1,1,1);
function voxelBurst(scene:T.Scene,rig:T.Group,p:PlayerView,death:DeathPresentation,quality:number,budget:number):Effect {
 let seed=death.seed;const random=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;};
 const limit=Math.min(budget,quality<.5?ANIMATION.reducedDeathVoxels:ANIMATION.deathVoxels),candidates:{pos:T.Vector3;color:T.Color;size:number}[]=[];
 rig.updateMatrixWorld(true);
 rig.traverse(o=>{if(!(o instanceof T.Mesh)||o.geometry.type!=='BoxGeometry'||o.parent?.name==='enchantment'||o.userData.bodyWound)return;
  const scale=new T.Vector3();o.getWorldScale(scale);const counts=[scale.x,scale.y,scale.z].map(n=>Math.max(1,Math.ceil(n/.19)));
  for(let x=0;x<counts[0];x++)for(let y=0;y<counts[1];y++)for(let z=0;z<counts[2];z++){
   const pos=new T.Vector3((x+.5)/counts[0]-.5,(y+.5)/counts[1]-.5,(z+.5)/counts[2]-.5).applyMatrix4(o.matrixWorld);
   candidates.push({pos,color:((o.material as T.MeshLambertMaterial).color||new T.Color(p.color)).clone(),size:Math.min(.2,Math.max(.07,Math.min(scale.x/counts[0],scale.y/counts[1],scale.z/counts[2])))});
  }
 });
 const chunks:Voxel[]=[],mats=new Map<string,T.MeshLambertMaterial>();
 for(let i=0;i<Math.min(limit,candidates.length);i++){
  const c=candidates[Math.floor(i*candidates.length/Math.min(limit,candidates.length))],key=c.color.getHexString();let mat=mats.get(key);if(!mat){mat=new T.MeshLambertMaterial({color:c.color});mats.set(key,mat);}
  const mesh=new T.Mesh(geometry,mat),spread=c.pos.clone().sub(new T.Vector3(death.pos.x,death.pos.y,death.pos.z));mesh.scale.setScalar(c.size*.92);scene.add(mesh);
  const inherited=new T.Vector3(death.velocity.x,death.velocity.y,death.velocity.z).clampLength(0,7).multiplyScalar(.35);
  const velocity=spread.multiplyScalar(3.5).add(new T.Vector3((random()-.5)*3,2+random()*3,(random()-.5)*3)).add(inherited).add(new T.Vector3(death.direction.x,death.direction.y,death.direction.z).multiplyScalar(2));
  chunks.push({mesh,start:c.pos,velocity,spin:new T.Vector3(random()*9-4.5,random()*9-4.5,random()*9-4.5),floor:Math.abs(c.pos.x)<ARENA.width/2&&Math.abs(c.pos.z)<ARENA.depth/2?.05:-8,size:c.size});
 }
 return {count:chunks.length,update(age){const t=Math.max(0,age);for(const c of chunks){const hit=(c.velocity.y+Math.sqrt(Math.max(0,c.velocity.y*c.velocity.y+2*12*Math.max(0,c.start.y-c.floor))))/12;
   const travel=Math.min(t,hit)+Math.max(0,t-hit)*.1;c.mesh.position.copy(c.start).addScaledVector(c.velocity,travel);c.mesh.position.y=t<hit?c.start.y+c.velocity.y*t-6*t*t:c.floor+c.size*.5+Math.max(0,Math.sin(Math.min(Math.PI,(t-hit)*7)))*.15;
   c.mesh.rotation.set(c.spin.x*Math.min(t,hit+.3),c.spin.y*Math.min(t,hit+.3),c.spin.z*Math.min(t,hit+.3));c.mesh.scale.setScalar(c.size*.92*Math.min(1,Math.max(0,(ANIMATION.deathSeconds-t)/.7)));
  }},dispose(){for(const c of chunks)scene.remove(c.mesh);mats.forEach(m=>m.dispose());}};
}
/** Add future kill animations here. Only the server chooses the effect identifier.
 * Unknown identifiers fall back to voxel breakup. These pieces never deal damage. */
/** Public default (docs/BODY-DAMAGE-HANDOFF.md): the ragdoll consumes the authority's fatal wound and death motion. Living
 * wounds already ride on the rig (body-wounds.ts); a severed part detaches with voxel caps, a crushed part fragments and the
 * gated fast-headshot death kneels and falls before the ragdoll takes over. */
const bodyRagdoll:KillEffectFactory=ragdollVariant('ragdoll',{impact:true});
export const KILL_EFFECTS:Record<string,KillEffectFactory>={...SIREN_KILL_EFFECTS,...VENT_KILL_EFFECTS,'impact-ragdoll':bodyRagdoll,ragdoll,'dev-gibber':ragdollVariant('ragdoll',{gib:true}),...Object.fromEntries(Object.keys(RAGDOLL_STYLES).map(id=>[id,ragdollVariant(id as RagdollStyle)])),'dev-raging-demon':ragingDemon,'voxel-burst':voxelBurst,'dev-clown-love':clownLove,'dev-bass-van':bassVan,...Object.fromEntries(['confetti','ascension','shrink','launch','ghost','dev-clown'].map(kind=>[kind,cinematic(kind)]))};
export class DeathEffects {
 private active=new Map<string,{effect:Effect;at:number;kept?:string;death?:DeathPresentation}>();private seen=new Set<string>();private epoch='';
 /** PvE: downed party members' bodies stay until they are back up, then regenerate (src/client/pve/downed.ts). */
 readonly keeper:DownedBodies;
 constructor(private scene:T.Scene){this.keeper=downedBodies(scene);}
 update(state:Snapshot,rigs:Map<string,{group:T.Group}>,quality:number){
  const epoch=String(state.generation)+':'+state.setNumber+':'+state.round;
  if(epoch!==this.epoch){this.clear();this.epoch=epoch;}
  let late=0;
  for(const p of state.players){const d=p.death,rig=rigs.get(p.id);if(!d||p.hp>0||!rig)continue;
   // A kept body is rebuilt whenever it is missing (late join, after a kill cam rewind), at most one fast-forward per frame.
   const kept=this.keeper.kept(p,state);if(kept?this.active.has(d.id):this.seen.has(d.id))continue;
   if(kept){if(state.time-d.at>=DOWNED.keepAge&&late++>=DOWNED.lateBuildsPerFrame)continue;}
   else if(state.time-d.at>=ANIMATION.deathSeconds||this.count>=ANIMATION.maxDeathVoxels){this.seen.add(d.id);continue;}
   this.seen.add(d.id);
   const factory=kept&&!DOWNED.keepable.includes(d.effect)?KILL_EFFECTS['impact-ragdoll']:KILL_EFFECTS[d.effect]||KILL_EFFECTS['voxel-burst'];
   const effect=factory(this.scene,rig.group,p,d,quality,kept?DOWNED.budget:ANIMATION.maxDeathVoxels-this.count,state);this.active.set(d.id,{effect,at:d.at,kept:kept?p.id:undefined,death:d});
  }
  for(const[id,e]of this.active){const down=state.players.find(p=>p.hp<=0&&p.death?.id===id);let age=state.time-e.at;
   if(e.kept){const hold=e.effect.keepAge??DOWNED.keepAge;
    if(down&&this.keeper.kept(down,state)){if(e.effect.keep)e.effect.keep(age);else e.effect.update(Math.min(age,hold));continue;}
    // Back up (revive, self-revive, respawn): the keeper regenerates the body and disposes it.
    if(!down&&e.death&&this.keeper.adopt(e.kept,e.effect,e.death,state)){this.active.delete(id);continue;}
    // Still down but no longer kept (disconnected, PvE over): the stock ending from the hold.
    e.kept=undefined;if(age>hold){e.at=state.time-hold;age=hold;}
   }
   if(age>=ANIMATION.deathSeconds||!down){e.effect.dispose();this.active.delete(id);}else e.effect.update(age);}
 }
 /** Effects counted against the shared budget (kept bodies have their own). */
 get count(){let n=0;for(const e of this.active.values())if(!e.kept)n+=e.effect.count;return n;}
 clear(){for(const e of this.active.values())e.effect.dispose();this.active.clear();this.seen.clear();this.keeper.clear();}
}
