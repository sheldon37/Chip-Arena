import * as T from 'three';
import {stageHazards,StageHazard} from '../shared/stage-hazards.js';
export class StageHazardVisuals {
 readonly group=new T.Group();private map:unknown;private items=new Map<string,T.Group>();
 constructor(scene:T.Scene){scene.add(this.group);this.group.name='stage-hazards';}
 private clear(){this.group.traverse(o=>{const m=o as T.Mesh;if(m.geometry)m.geometry.dispose();if(m.material)for(const mat of Array.isArray(m.material)?m.material:[m.material])mat.dispose();});this.group.clear();this.items.clear();}
 update(map:unknown,time:number){if(map!==this.map){this.clear();this.map=map;for(const h of stageHazards(map,time))this.create(h);}
 for(const h of stageHazards(map,time)){const g=this.items.get(h.id)!;g.position.set(h.x,h.y,h.z);if(h.kind==='saw'){g.rotation.z=-time*12;continue;}
 const surface=g.getObjectByName('surface') as T.Mesh,mat=surface.material as T.MeshStandardMaterial;
 if(h.kind==='spikes'){const spikes=g.getObjectByName('spikes')!;spikes.scale.y=.025+(h.extension||0)*.975;mat.emissive.setHex(h.active?0xff263b:h.warning?0xffb21e:0x122527);mat.emissiveIntensity=h.warning?.4+Math.sin(time*18)*.3:h.active?.7:.2;}
 else {mat.emissiveIntensity=.85+Math.sin(time*3+h.x)*.12;const cracks=g.getObjectByName('currents')!;cracks.position.z=Math.sin(time*.7+h.x)*.3;const bubbles=g.getObjectByName('bubbles')!;for(let i=0;i<bubbles.children.length;i++){const b=bubbles.children[i],phase=(time*.65+i*.137)%1;b.position.y=.05+phase*1.4;b.scale.setScalar(Math.sin(phase*Math.PI)*.14);}}
 }}
 private create(h:StageHazard){const g=new T.Group();g.name=h.id;this.items.set(h.id,g);this.group.add(g);
 const box=(parent:T.Group,name:string,x:number,y:number,z:number,sx:number,sy:number,sz:number,color:number,glow=0)=>{const m=new T.Mesh(new T.BoxGeometry(sx,sy,sz),new T.MeshStandardMaterial({color,roughness:.65,metalness:.3,emissive:glow,emissiveIntensity:.8}));m.name=name;m.position.set(x,y,z);parent.add(m);return m;};
 if(h.kind==='saw'){
 const track=new T.Group();track.position.z=h.z;this.group.add(track);box(track,'rail',0,.035,0,38,.07,.24,0x1d2631);for(const side of [-1,1])box(track,'warning',0,.025,side*.65,38,.05,.07,0xffbc46,0x754110);
 for(let i=0;i<19;i++)for(const side of [-1,1]){const mark=box(track,'caution-stripe',-18+i*2,.045,side*.9,.8,.04,.2,0xffca48,0x694810);mark.rotation.y=-.65;}
 const shape=new T.Shape();for(let i=0;i<48;i++){const a=i/48*Math.PI*2,r=i%2?1:1.25,x=Math.cos(a)*r,y=Math.sin(a)*r;if(i)shape.lineTo(x,y);else shape.moveTo(x,y);}shape.closePath();const geo=new T.ExtrudeGeometry(shape,{depth:.16,bevelEnabled:true,bevelSize:.025,bevelThickness:.025,bevelSegments:1,steps:1});geo.translate(0,0,-.08);g.add(new T.Mesh(geo,new T.MeshStandardMaterial({color:0xc4d4e2,metalness:.85,roughness:.28})));
 for(const side of [-1,1]){const ring=new T.Mesh(new T.TorusGeometry(.72,.035,4,24),new T.MeshStandardMaterial({color:0xffbd45,emissive:0xa14300,emissiveIntensity:.9}));ring.position.z=side*.11;g.add(ring);for(let n=0;n<6;n++){const spoke=box(g,'blade-slot',Math.cos(n*Math.PI/3)*.5,Math.sin(n*Math.PI/3)*.5,side*.105,.35,.075,.025,0x172431);spoke.rotation.z=n*Math.PI/3;}}
 const hub=new T.Mesh(new T.CylinderGeometry(.25,.25,.28,12),new T.MeshStandardMaterial({color:0xff694a,emissive:0xff3210,emissiveIntensity:.6}));hub.rotation.x=Math.PI/2;g.add(hub);
 }else if(h.kind==='spikes'){
 box(g,'surface',0,.035,0,h.sx,.07,h.sz,0x28313b,0x122527);const geo=new T.ConeGeometry(.28,1.1,5),mat=new T.MeshStandardMaterial({color:0xd6dfea,metalness:.8,roughness:.35}),mesh=new T.InstancedMesh(geo,mat,30),o=new T.Object3D();mesh.name='spikes';for(let i=0;i<30;i++){box(g,'spike-socket',-2.5+i%6,.077,-2+Math.floor(i/6),.65,.035,.65,0x090d12);o.position.set(-2.5+i%6,.58,-2+Math.floor(i/6));o.updateMatrix();mesh.setMatrixAt(i,o.matrix);}g.add(mesh);for(const side of [-1,1])box(g,'edge',side*(h.sx/2+.1),.06,0,.12,.12,h.sz,0xeead46,0x442610);
 }else{
 box(g,'surface',0,0,0,h.sx,.06,h.sz,0xff5e19,0xff3910);const currents=new T.Group();currents.name='currents';g.add(currents);for(let i=0;i<30;i++){const c=box(currents,'crust',Math.sin(i*17)*1.4,.045,-h.sz/2+.65+i*1.1,.45+(i%3)*.23,.05,.6,0x3d1720);c.rotation.y=i*1.7;}for(const side of [-1,1])box(g,'edge',side*(h.sx/2+.1),1.18,0,.2,.16,h.sz,0x70262a,0xb1300b);const bubbles=new T.Group();bubbles.name='bubbles';g.add(bubbles);for(let i=0;i<28;i++){const bubble=new T.Mesh(new T.SphereGeometry(1,6,4),new T.MeshBasicMaterial({color:i%2?0xffcb48:0xff7022,transparent:true,opacity:.8}));bubble.position.set(Math.sin(i*7)*1.6,0,-16+(i*3.71)%32);bubbles.add(bubble);}
 }
 }
 dispose(){this.clear();this.group.removeFromParent();}
}
