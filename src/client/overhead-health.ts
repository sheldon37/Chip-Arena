import {TEAMS,sameTeam} from '../shared/teams.js';
import * as T from 'three';
import type {Snapshot} from '../shared/simulation.js';
const plane=new T.PlaneGeometry(1,1);
/** World geometry uses depth testing so health never reveals players behind cover. */
export class OverheadHealth{
 private bars=new Map<string,{root:T.Group;fill:T.Mesh<T.PlaneGeometry,T.MeshBasicMaterial>;label?:T.Mesh<T.PlaneGeometry,T.MeshBasicMaterial>;labelKey?:string}>();
 private back=new T.MeshBasicMaterial({color:0x080b12,transparent:true,opacity:.7,depthWrite:false,toneMapped:false});
 constructor(private scene:T.Scene){}
 update(state:Snapshot,camera:T.Camera,visible:boolean,localId:string,first:boolean){
  for(const[id,b]of this.bars)if(!state.players.some(p=>p.id===id)){b.root.removeFromParent();b.fill.material.dispose();b.label?.material.map?.dispose();b.label?.material.dispose();this.bars.delete(id);}
  for(const p of state.players){let b=this.bars.get(p.id);if(!b){const root=new T.Group();root.name='health-'+p.id;const bg=new T.Mesh(plane,this.back);bg.scale.set(.78,.065,1);root.add(bg);const fill=new T.Mesh(plane,new T.MeshBasicMaterial({color:0x59dd83,depthWrite:false,toneMapped:false}));fill.position.z=.006;root.add(fill);b={root,fill};this.bars.set(p.id,b);this.scene.add(root);}
   const team=state.options.gameMode==='teams'?p.team:undefined,key=team?team+':'+(sameTeam(p,state.players.find(q=>q.id===localId),'teams')?'ALLY':'ENEMY'):'';
   if(b.labelKey!==key){b.label?.removeFromParent();b.label?.material.map?.dispose();b.label?.material.dispose();b.label=undefined;b.labelKey=key;
    if(team&&typeof document!=='undefined'&&document.createElement){const canvas=document.createElement('canvas');canvas.width=512;canvas.height=96;const ctx=canvas.getContext('2d');if(ctx){ctx.clearRect(0,0,512,96);ctx.fillStyle=TEAMS[team].color;ctx.shadowColor='#000';ctx.shadowBlur=4;ctx.font='bold 36px sans-serif';ctx.textAlign='center';ctx.textBaseline='middle';ctx.fillText(TEAMS[team].name.toUpperCase()+' · '+key.split(':')[1],256,48);const texture=new T.CanvasTexture(canvas);texture.colorSpace=T.SRGBColorSpace;b.label=new T.Mesh(plane,new T.MeshBasicMaterial({map:texture,transparent:true,depthTest:true,depthWrite:false,toneMapped:false}));b.label.name='team-label';b.label.position.y=.12;b.label.scale.set(.76,.142,1);b.root.add(b.label);}}
   }
   b.root.visible=visible&&p.hp>0&&p.connected&&!(first&&p.id===localId)&&camera.position.distanceToSquared(new T.Vector3(p.pos.x,p.pos.y,p.pos.z))<55*55;if(!b.root.visible)continue;
   if(b.label){const distance=camera.position.distanceTo(new T.Vector3(p.pos.x,p.pos.y,p.pos.z));b.label.visible=p.id!==localId&&distance<32;b.label.material.opacity=distance<20?.85:.85*(32-distance)/12;}
   b.root.position.set(p.pos.x,p.pos.y+1.3,p.pos.z);b.root.quaternion.copy(camera.quaternion);const health=Math.max(0,Math.min(1,p.hp/Math.max(1,p.maxHp)));b.fill.scale.set(.72*health,.035,1);b.fill.position.x=-.36*(1-health);b.fill.material.color.set(health<.25?0xff7381:state.options.gameMode==='teams'?p.color:0x59dd83);
  }
 }
 clear(){for(const b of this.bars.values()){b.root.removeFromParent();b.fill.material.dispose();b.label?.material.map?.dispose();b.label?.material.dispose();}this.bars.clear();}
}
