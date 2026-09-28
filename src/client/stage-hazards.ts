import * as T from 'three';
import {stageHazards,StageHazard} from '../shared/stage-hazards.js';
export class StageHazardVisuals {
 readonly group=new T.Group();private map:unknown;private items=new Map<string,T.Group>();
 constructor(scene:T.Scene){scene.add(this.group);this.group.name='stage-hazards';}
 private clear(){this.group.traverse(o=>{const m=o as T.Mesh;if(m.geometry)m.geometry.dispose();if(m.material)for(const mat of Array.isArray(m.material)?m.material:[m.material])mat.dispose();});this.group.clear();this.items.clear();}
 update(map:unknown,time:number){if(map!==this.map){this.clear();this.map=map;for(const h of stageHazards(map,time))this.create(h);}
 for(const h of stageHazards(map,time)){const g=this.items.get(h.id)!;g.position.set(h.x,h.y,h.z);if(h.kind==='saw'){g.rotation.z=-time*12;continue;}
 const surface=g.getObjectByName('surface') as T.Mesh,mat=surface.material as T.MeshStandardMaterial;
 if(h.kind==='spikes'){const spikes=g.getObjectByName('spikes')!;spikes.scale.y=h.active?1:.045;mat.emissive.setHex(h.active?0xff263b:h.warning?0xffb21e:0x122527);mat.emissiveIntensity=h.warning?.4+Math.sin(time*18)*.3:h.active?.7:.2;}
 else {mat.emissiveIntensity=.85+Math.sin(time*3+h.x)*.12;const cracks=g.getObjectByName('currents')!;cracks.position.z=Math.sin(time*.7+h.x)*.3;}
 }}
 private create(h:StageHazard){const g=new T.Group();g.name=h.id;this.items.set(h.id,g);this.group.add(g);
 const box=(parent:T.Group,name:string,x:number,y:number,z:number,sx:number,sy:number,sz:number,color:number,glow=0)=>{const m=new T.Mesh(new T.BoxGeometry(sx,sy,sz),new T.MeshStandardMaterial({color,roughness:.65,metalness:.3,emissive:glow,emissiveIntensity:.8}));m.name=name;m.position.set(x,y,z);parent.add(m);return m;};
 if(h.kind==='saw'){
 const track=new T.Group();track.position.z=h.z;this.group.add(track);box(track,'rail',0,.035,0,38,.07,.24,0x1d2631);for(const side of [-1,1])box(track,'warning',0,.025,side*.65,38,.05,.07,0xffbc46,0x754110);
 const shape=new T.Shape();for(let i=0;i<48;i++){const a=i/48*Math.PI*2,r=i%2?1:1.25,x=Math.cos(a)*r,y=Math.sin(a)*r;if(i)shape.lineTo(x,y);else shape.moveTo(x,y);}shape.closePath();const geo=new T.ExtrudeGeometry(shape,{depth:.16,bevelEnabled:true,bevelSize:.025,bevelThickness:.025,bevelSegments:1,steps:1});geo.translate(0,0,-.08);g.add(new T.Mesh(geo,new T.MeshStandardMaterial({color:0xc4d4e2,metalness:.85,roughness:.28})));
 const hub=new T.Mesh(new T.CylinderGeometry(.25,.25,.28,12),new T.MeshStandardMaterial({color:0xff694a,emissive:0xff3210,emissiveIntensity:.6}));hub.rotation.x=Math.PI/2;g.add(hub);
 }else if(h.kind==='spikes'){
 box(g,'surface',0,.035,0,h.sx,.07,h.sz,0x28313b,0x122527);const geo=new T.ConeGeometry(.19,1.1,5),mat=new T.MeshStandardMaterial({color:0xd6dfea,metalness:.8,roughness:.35}),mesh=new T.InstancedMesh(geo,mat,30),o=new T.Object3D();mesh.name='spikes';for(let i=0;i<30;i++){o.position.set(-2.5+i%6,.58,-2+Math.floor(i/6));o.updateMatrix();mesh.setMatrixAt(i,o.matrix);}g.add(mesh);for(const side of [-1,1])box(g,'edge',side*(h.sx/2+.1),.06,0,.12,.12,h.sz,0xeead46,0x442610);
 }else{
 box(g,'surface',0,0,0,h.sx,.06,h.sz,0xff5e19,0xff3910);const currents=new T.Group();currents.name='currents';g.add(currents);for(let i=0;i<30;i++){const c=box(currents,'crust',Math.sin(i*17)*1.4,.045,-h.sz/2+.65+i*1.1,.45+(i%3)*.23,.05,.6,0x3d1720);c.rotation.y=i*1.7;}for(const side of [-1,1])box(g,'edge',side*(h.sx/2+.1),.08,0,.2,.16,h.sz,0x70262a,0xb1300b);
 }
 }
 dispose(){this.clear();this.group.removeFromParent();}
}
