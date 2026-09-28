import * as T from 'three';
import type {GameEvent,Snapshot} from '../shared/simulation.js';
/** Fixed-capacity two-tone electric ribbons. Four draw calls irrespective of players/hits. */
export class EmperorEffects {
 group=new T.Group();private core:T.InstancedMesh;private dark:T.InstancedMesh;private object=new T.Object3D();private cursor=0;private impacts:{pos:T.Vector3;at:number;seed:number;size:number}[]=[];
 constructor(scene:T.Scene){scene.add(this.group);const box=new T.BoxGeometry(1,1,1);this.core=new T.InstancedMesh(box,new T.MeshBasicMaterial({color:0xff1e43,toneMapped:false,transparent:true,opacity:1,depthWrite:false}),512);this.dark=new T.InstancedMesh(box,new T.MeshBasicMaterial({color:0x130008,transparent:true,opacity:.8,depthWrite:false}),512);for(const m of [this.dark,this.core]){m.frustumCulled=false;m.count=0;this.group.add(m);}this.dark.renderOrder=1;this.core.renderOrder=2;}
 begin(){this.cursor=0;}
 bolt(center:T.Vector3,time:number,seed:number,size=1){
  const phase=Math.floor(time*24),count=5;for(let b=0;b<3;b++){let prev=center.clone();for(let j=1;j<=count;j++){const a=seed*2.4+b*2.094+j*.35,noise=Math.sin((phase+seed*3+j*7+b*11)*2.17),next=center.clone().add(new T.Vector3(Math.cos(a)*(j/count)*size,(j/count-.25)*size+noise*.16*size,Math.sin(a)*(j/count)*size));if(this.cursor>=512)return;const delta=next.clone().sub(prev);this.object.position.copy(prev).add(next).multiplyScalar(.5);this.object.quaternion.setFromUnitVectors(new T.Vector3(0,1,0),delta.clone().normalize());this.object.scale.set(.043*size,delta.length()+.025,.043*size);this.object.updateMatrix();this.dark.setMatrixAt(this.cursor,this.object.matrix);this.object.scale.x=this.object.scale.z=.018*size;this.object.updateMatrix();this.core.setMatrixAt(this.cursor++,this.object.matrix);prev=next;}}
 }
 end(){for(const m of [this.dark,this.core]){m.count=this.cursor;m.instanceMatrix.needsUpdate=true;}}
 event(e:GameEvent,time:number){if(!['emperor-hit','emperor-bound'].includes(e.type))return;this.impacts.push({pos:new T.Vector3(e.pos.x,e.pos.y,e.pos.z),at:time,seed:e.id,size:e.radius||.75});if(this.impacts.length>16)this.impacts.shift();}
 update(state:Snapshot,time:number,rigs:Map<string,{rightHand:T.Group}>,quality:number){this.begin();for(const p of state.players){if(p.hp<=0)continue;const a=p.emperor,age=state.time-(a?.electricAt??-10);if(p.spec==='emperor'&&age>=0&&age<.35){const rig=rigs.get(p.id),pos=rig?.rightHand.getWorldPosition(new T.Vector3())||new T.Vector3(p.pos.x,p.pos.y+.4,p.pos.z);this.bolt(pos,time,p.id.length,.5);}
  if(p.juggle&&state.time-p.juggle.lastHit<.28)this.bolt(new T.Vector3(p.pos.x,p.pos.y,p.pos.z),time,p.juggle.hits,.7);
 }for(const hit of this.impacts){const age=time-hit.at;if(age>=0&&age<.3)this.bolt(hit.pos,time,hit.seed,hit.size*(1-age/.4));}this.impacts=this.impacts.filter(h=>time-h.at<.3);
 if(quality>=.5)for(const f of state.emperorTrails||[])this.bolt(new T.Vector3(f.pos.x,.13,f.pos.z),time,Math.floor(f.at*10),.65);this.end();}
 clear(){this.impacts=[];this.begin();this.end();}
}
