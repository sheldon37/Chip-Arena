import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
import type {Snapshot} from '../shared/simulation.js';
import {DEMON_STEP} from '../shared/demon-step.js';
const cube=new T.BoxGeometry(1,1,1),limit=100;
/** Capture visible box meshes only; cosmetics keep their silhouette without extra simulation actors. */
export function boxPose(root:T.Object3D){root.updateMatrixWorld(true);const inverse=root.matrixWorld.clone().invert(),matrices:T.Matrix4[]=[];const visit=(node:T.Object3D)=>{if(node!==root&&!node.visible)return;if(node instanceof T.Mesh&&node.geometry.type==='BoxGeometry'&&matrices.length<limit){const p=(node.geometry as T.BoxGeometry).parameters;matrices.push(inverse.clone().multiply(node.matrixWorld).scale(new T.Vector3(p.width,p.height,p.depth)));};for(const child of node.children)visit(child);};visit(root);return matrices;}
export function setGhostPose(mesh:T.InstancedMesh,matrices:T.Matrix4[]){mesh.count=Math.min(limit,matrices.length);matrices.slice(0,limit).forEach((m,i)=>mesh.setMatrixAt(i,m));mesh.instanceMatrix.needsUpdate=true;}
export function ghostMesh(color:number,opacity=.4){const mesh=new T.InstancedMesh(cube,new T.MeshBasicMaterial({color,transparent:true,opacity,depthWrite:false,toneMapped:false}),limit);mesh.frustumCulled=false;mesh.count=0;return mesh;}
export class DemonAfterimages {
 private pool:{mesh:T.InstancedMesh;at:number;owner:string}[]=[];private index=0;private sampled=new Map<string,number>();private epoch='';
 constructor(private scene:T.Scene){for(let i=0;i<32;i++){const mesh=ghostMesh(i%2?0x594de7:0x9c7cff);mesh.name='demon-step-afterimage';mesh.visible=false;scene.add(mesh);this.pool.push({mesh,at:-10,owner:''});}}
 update(state:Snapshot,rigs:Map<string,CharacterRig>,quality:number){const epoch=`${state.generation}:${state.setNumber}:${state.round}`;if(epoch!==this.epoch){this.clear();this.epoch=epoch;}const count=quality<.5?12:32;
  for(const p of state.players){const at=p.arcade?.demonStepAt;if(p.spec!=='ansatsuken'||p.hp<=0||!p.connected||!p.dashTime||at===undefined||state.time<at||state.time-at>DEMON_STEP.seconds||state.time-(this.sampled.get(p.id)??-10)<DEMON_STEP.sampleSeconds*(quality<.5?2:1))continue;const rig=rigs.get(p.id);if(!rig)continue;const entry=this.pool[this.index++%count];entry.at=state.time;entry.owner=p.id;setGhostPose(entry.mesh,boxPose(rig.group));entry.mesh.position.copy(rig.group.position);entry.mesh.quaternion.copy(rig.group.quaternion);entry.mesh.scale.copy(rig.group.scale);this.sampled.set(p.id,state.time);}
  for(const e of this.pool){const age=state.time-e.at;e.mesh.visible=age>=0&&age<DEMON_STEP.afterimageSeconds&&state.players.some(p=>p.id===e.owner&&p.hp>0);(e.mesh.material as T.MeshBasicMaterial).opacity=.45*Math.max(0,1-age/DEMON_STEP.afterimageSeconds)**1.4;}
 }
 clear(){for(const e of this.pool){e.at=-10;e.mesh.visible=false;}this.sampled.clear();}
}
