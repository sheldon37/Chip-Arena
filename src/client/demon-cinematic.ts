import * as T from 'three';
import type {Snapshot} from '../shared/simulation.js';
import {demonScreen,demonBeat,RAGING_DEMON as D} from '../shared/raging-demon.js';
import {createRagingDemon} from './raging-demon.js';
/** Private victim view; everyone else retains their own arena camera. */
export class DemonCinematic {
 scene=new T.Scene();camera=new T.PerspectiveCamera(43,1,.05,60);private key='';private effect:ReturnType<typeof createRagingDemon>|undefined;private backdrop=new T.Color();
 constructor(){this.scene.background=this.backdrop;}
 update(state:Snapshot,me:string,aspect:number,quality=1){const p=state.players.find(p=>p.id===me),frame=demonScreen(p,state.time);if(!p?.death||!frame){this.clear();return false;}const d=p.death,key=`${state.generation}:${d.id}`;if(this.key!==key){this.clear();this.key=key;this.effect=createRagingDemon(this.scene,p,d,quality,32,true);}this.effect!.update(frame.age);
  const beat=demonBeat(frame.age),pulse=beat.index>=0?Math.max(0,1-beat.age/.17):0;this.backdrop.set(frame.black?0x000000:frame.age<D.symbol?0x8b001a:0x11020e);if(!frame.black)this.backdrop.lerp(new T.Color(0xb80b29),pulse*.2);
  const root=this.scene.getObjectByName('raging-demon')!,center=root.position.clone().add(new T.Vector3(0,.18,0)),start=d.attacker?new T.Vector3(d.attacker.pos.x-d.pos.x,0,d.attacker.pos.z-d.pos.z):new T.Vector3(-3,0,0);if(start.length()<2)start.set(-Math.sin(d.yaw)*3,0,-Math.cos(d.yaw)*3);const yaw=Math.atan2(-start.x,-start.z),shot=frame.age<D.symbol?Math.max(0,Math.floor((beat.index+1)/2))%4:4,angles=[1.35,-1.25,.75,-.55,0],heights=[.35,.15,-.2,.4,.25],distance=shot===4?3.5:shot===2?2.8:2.5;
  const angle=yaw+angles[shot],eye=center.clone().add(new T.Vector3(Math.sin(angle)*distance,heights[shot],Math.cos(angle)*distance));if(aspect<1.35)eye.sub(center).multiplyScalar(1.35/Math.max(.65,aspect)).add(center);this.camera.position.copy(eye);this.camera.up.set(0,1,0);this.camera.lookAt(center);this.camera.aspect=aspect;this.camera.fov=shot===4?42:48;this.camera.updateProjectionMatrix();root.visible=!frame.black;return true;
 }
 clear(){this.effect?.dispose();this.effect=undefined;this.key='';}
}
