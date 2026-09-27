import * as T from 'three';
import type {PlayerView,Snapshot} from '../shared/simulation.js';
import type {Vec} from '../shared/config.js';

/** Cosmetic only: every active flag comes from authoritative player statuses. */
export const STATUS_VFX={flames:8,embers:6,bubbles:9,reducedFlames:4,reducedEmbers:2,reducedBubbles:4,fireRise:1.45,bubbleRise:1.9,bubblePeriod:1.65} as const;
const flameShape=new T.Shape().moveTo(0,0).bezierCurveTo(-.4,.05,-.44,.32,-.15,.56).quadraticCurveTo(-.21,.37,-.02,.35).bezierCurveTo(.18,.58,-.08,.72,.1,1).bezierCurveTo(.13,.67,.45,.48,.23,.2).quadraticCurveTo(.39,.11,0,0);
const flameGeometry=new T.ShapeGeometry(flameShape,4),sparkGeometry=new T.OctahedronGeometry(1,0),bubbleGeometry=new T.SphereGeometry(1,8,6),ringGeometry=new T.RingGeometry(.88,1,16),glintGeometry=new T.SphereGeometry(1,5,3);
const fireMaterial=new T.MeshBasicMaterial({color:0xff6b24,transparent:true,opacity:.72,side:T.DoubleSide,depthWrite:false});
const coreMaterial=new T.MeshBasicMaterial({color:0xffdc78,transparent:true,opacity:.85,side:T.DoubleSide,depthWrite:false});
const emberMaterial=new T.MeshBasicMaterial({color:0xffb34f});
const bubbleMaterial=new T.MeshBasicMaterial({color:0x6ec134,transparent:true,opacity:.24,depthWrite:false});
const rimMaterial=new T.MeshBasicMaterial({color:0x94e94d,transparent:true,opacity:.75,side:T.DoubleSide,depthWrite:false});
const glintMaterial=new T.MeshBasicMaterial({color:0xe0ffbd,transparent:true,opacity:.8,depthWrite:false});
const fraction=(n:number)=>n-Math.floor(n);
class StatusRig {
 root=new T.Group();fire=new T.Group();poison=new T.Group();flames:T.Mesh[]=[];embers:T.Mesh[]=[];bubbles:T.Group[]=[];
 constructor(public firstPerson=false){this.root.name='player-status-effects';this.fire.name='burning-flames';this.poison.name='poison-bubbles';this.root.add(this.fire,this.poison);
  for(let n=0;n<STATUS_VFX.flames;n++){const flame=new T.Mesh(flameGeometry,fireMaterial),core=new T.Mesh(flameGeometry,coreMaterial);core.scale.set(.45,.64,1);core.position.set(.012,.015,.008);flame.add(core);this.fire.add(flame);this.flames.push(flame);}
  for(let n=0;n<STATUS_VFX.embers;n++){const spark=new T.Mesh(sparkGeometry,emberMaterial);this.fire.add(spark);this.embers.push(spark);}
  for(let n=0;n<STATUS_VFX.bubbles;n++){const bubble=new T.Group(),body=new T.Mesh(bubbleGeometry,bubbleMaterial),rim=new T.Mesh(ringGeometry,rimMaterial),glint=new T.Mesh(glintGeometry,glintMaterial);rim.position.z=.05;glint.scale.set(.18,.28,.13);glint.position.set(-.3,.35,.86);bubble.add(body,rim,glint);this.poison.add(bubble);this.bubbles.push(bubble);}
 }
 update(statuses:PlayerView['statuses'],time:number,camera:T.Camera,quality:number,active:boolean){
  this.fire.visible=active&&statuses.some(s=>s.element==='fire'&&s.remaining>0);this.poison.visible=active&&statuses.some(s=>s.element==='poison'&&s.remaining>0);
  const reduced=quality<.5,fp=this.firstPerson;
  if(this.fire.visible){for(let n=0;n<this.flames.length;n++){const mesh=this.flames[n];mesh.visible=n<(fp?3:reduced?STATUS_VFX.reducedFlames:STATUS_VFX.flames);if(!mesh.visible)continue;
   const angle=n*2.399,timeOffset=time*2.9+n*.83,pulse=.75+.25*Math.sin(timeOffset*2),radius=fp?.045:.28+(n%3)*.055;
   mesh.position.set(Math.cos(angle)*radius,fp?-.11+n*.035:-.65+(n%3)*.36,Math.sin(angle)*radius+(fp?.14:0));
   mesh.scale.set((fp?.17:.43)*pulse,(fp?.24:.75)*(.75+.25*Math.sin(timeOffset)),1);
   if(fp)mesh.rotation.set(0,Math.PI,Math.sin(timeOffset)*.15);else{mesh.rotation.set(0,Math.atan2(camera.position.x-this.root.position.x,camera.position.z-this.root.position.z),Math.sin(timeOffset)*.13);}
  }
  for(let n=0;n<this.embers.length;n++){const mesh=this.embers[n];mesh.visible=!fp&&n<(reduced?STATUS_VFX.reducedEmbers:STATUS_VFX.embers);const u=fraction(time*.8+n*.173),angle=n*2.4+time*.45;mesh.position.set(Math.cos(angle)*(.3+u*.12),-.25+u*STATUS_VFX.fireRise,Math.sin(angle)*(.3+u*.12));mesh.scale.setScalar(.027*(1-u)+.006);mesh.rotation.y=time*2+n;}
  }
  if(this.poison.visible)for(let n=0;n<this.bubbles.length;n++){const bubble=this.bubbles[n];bubble.visible=n<(fp?3:reduced?STATUS_VFX.reducedBubbles:STATUS_VFX.bubbles);if(!bubble.visible)continue;
   const u=fraction(time/STATUS_VFX.bubblePeriod+n*.117),angle=n*2.399+Math.sin(time+n)*.18,radius=fp?.08:.43+Math.sin(n*3)*.11,popping=u>.87;
   bubble.position.set(Math.cos(angle)*radius,fp?-.14+u*.36:-.55+u*STATUS_VFX.bubbleRise,Math.sin(angle)*radius+(fp?.12:0));
   const size=(fp?.028:.06+(n%3)*.025)*(1+u*.45)*(popping?1+(u-.87)*5:1);bubble.scale.set(size,size*(1+Math.sin(time*4+n)*.05),size);
   if(fp)bubble.rotation.set(0,Math.PI,0);else bubble.quaternion.copy(camera.quaternion);
   bubble.children[0].visible=!popping;bubble.children[2].visible=!popping;
  }
 }
}
export class PlayerStatusEffects {
 private actors=new Map<string,StatusRig>();private hands=new StatusRig(true);
 constructor(private scene:T.Scene,viewRoot:T.Group){viewRoot.add(this.hands.root);}
 update(state:Snapshot,me:string,first:boolean,camera:T.Camera,quality:number,localPos?:Vec){
  for(const [id,rig]of this.actors)if(!state.players.some(p=>p.id===id)){this.scene.remove(rig.root);this.actors.delete(id);}
  for(const p of state.players){let rig=this.actors.get(p.id);if(!rig){rig=new StatusRig();this.actors.set(p.id,rig);this.scene.add(rig.root);}
   const pos=p.id===me&&localPos?localPos:p.pos;rig.root.position.set(pos.x,pos.y,pos.z);rig.root.visible=!(first&&p.id===me);
   rig.update(p.statuses,state.time,camera,quality,p.hp>0&&state.phase==='fight');
  }
  const self=state.players.find(p=>p.id===me);this.hands.root.visible=first;this.hands.update(self?.statuses||[],state.time,camera,quality,!!self&&self.hp>0&&state.phase==='fight');
 }
 /** For developer diagnostics; these meshes have no physics bodies. */
 get visibleActors(){return [...this.actors.values()].filter(r=>r.root.visible&&(r.fire.visible||r.poison.visible)).length;}
}
