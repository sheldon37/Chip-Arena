import * as T from 'three';
import type {Snapshot} from '../shared/simulation.js';
const plane=new T.PlaneGeometry(1,1);
/** World geometry uses depth testing so health never reveals players behind cover. */
export class OverheadHealth{
 private bars=new Map<string,{root:T.Group;fill:T.Mesh<T.PlaneGeometry,T.MeshBasicMaterial>}>();
 private back=new T.MeshBasicMaterial({color:0x080b12,transparent:true,opacity:.7,depthWrite:false,toneMapped:false});
 constructor(private scene:T.Scene){}
 update(state:Snapshot,camera:T.Camera,visible:boolean,localId:string,first:boolean){
  for(const[id,b]of this.bars)if(!state.players.some(p=>p.id===id)){b.root.removeFromParent();b.fill.material.dispose();this.bars.delete(id);}
  for(const p of state.players){let b=this.bars.get(p.id);if(!b){const root=new T.Group();root.name='health-'+p.id;const bg=new T.Mesh(plane,this.back);bg.scale.set(.78,.065,1);root.add(bg);const fill=new T.Mesh(plane,new T.MeshBasicMaterial({color:0xdce5ee,depthWrite:false,toneMapped:false}));fill.position.z=.006;root.add(fill);b={root,fill};this.bars.set(p.id,b);this.scene.add(root);}
   b.root.visible=visible&&p.hp>0&&p.connected&&!(first&&p.id===localId)&&camera.position.distanceToSquared(new T.Vector3(p.pos.x,p.pos.y,p.pos.z))<55*55;if(!b.root.visible)continue;
   b.root.position.set(p.pos.x,p.pos.y+1.3,p.pos.z);b.root.quaternion.copy(camera.quaternion);const health=Math.max(0,Math.min(1,p.hp/Math.max(1,p.maxHp)));b.fill.scale.set(.72*health,.035,1);b.fill.position.x=-.36*(1-health);b.fill.material.color.set(health<.25?0xff7381:state.options.gameMode==='teams'?p.color:0xdce5ee);
  }
 }
 clear(){for(const b of this.bars.values()){b.root.removeFromParent();b.fill.material.dispose();}this.bars.clear();}
}
