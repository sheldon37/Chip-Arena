import * as T from 'three';
import type {Snapshot} from '../shared/simulation.js';
const coreShape=new T.SphereGeometry(1,16,10),rimShape=new T.TorusGeometry(1,.07,5,32);
const glow=(color:number,opacity:number)=>new T.MeshBasicMaterial({color,transparent:true,opacity,blending:T.AdditiveBlending,depthWrite:false,side:T.DoubleSide,toneMapped:false});
/** A domed leading edge, hot inner core and tapered, moving energy tongues. */
export class HadoukenVisual{
 readonly group=new T.Group();private core:T.Mesh;private shell:T.Mesh;private rim:T.Mesh;private tongues:T.Mesh[]=[];
 private hot:T.MeshBasicMaterial;private energy:T.MeshBasicMaterial;private haze:T.MeshBasicMaterial;
 constructor(red:boolean){this.group.name='hadouken-wave';this.hot=glow(red?0xfff4b8:0xeaffff,.95);this.energy=glow(red?0xff5c18:0x32b8ff,.82);this.haze=glow(red?0xffab24:0x61e6ff,.23);
  this.core=new T.Mesh(coreShape,this.hot);this.shell=new T.Mesh(coreShape,this.haze);this.rim=new T.Mesh(rimShape,this.energy);this.group.add(this.shell,this.core,this.rim);
  for(let i=0;i<9;i++){const geometry=new T.BufferGeometry(),vertices=new Float32Array(13*2*3),indices:number[]=[];for(let n=0;n<12;n++){const k=n*2;indices.push(k,k+1,k+2,k+1,k+3,k+2);}geometry.setAttribute('position',new T.BufferAttribute(vertices,3));geometry.setIndex(indices);const ribbon=new T.Mesh(geometry,i%3===0?this.hot:this.energy);ribbon.frustumCulled=false;this.group.add(ribbon);this.tongues.push(ribbon);}
 }
 update(p:Snapshot['projectiles'][number],time:number,quality:number){const r=p.radius*1.65,pulse=1+Math.sin(time*27)*.045;this.group.position.set(p.pos.x,p.pos.y,p.pos.z);const forward=new T.Vector3(p.vel.x,p.vel.y,p.vel.z);if(forward.lengthSq()>.0001)this.group.quaternion.setFromUnitVectors(new T.Vector3(0,0,1),forward.normalize());
  this.core.position.z=r*.25;this.core.scale.set(r*.68*pulse,r*.7*pulse,r*.96);this.shell.position.z=-r*.25;this.shell.scale.set(r*1.12,r*1.12,r*1.75);this.rim.position.z=r*.35;this.rim.scale.setScalar(r*.9*pulse);this.rim.rotation.z=time*2;
  this.tongues.forEach((mesh,i)=>{mesh.visible=quality>=.5||i<4;if(!mesh.visible)return;const angle=i*Math.PI*2/9+time*(i%2?.8:-.65),a=mesh.geometry.getAttribute('position') as T.BufferAttribute;for(let n=0;n<=12;n++){const u=n/12,rad=r*(.72+.3*Math.sin(u*Math.PI))*(1-u*.72),curl=angle+Math.sin(u*7-time*20+i)*.13+u*.5;const length=r*(3.1+(i%3)*.5),width=r*(.2*(1-u)+.01)*Math.sin(Math.min(1,u*6+.15)*Math.PI/2),z=r*.36-u*length;for(let side=0;side<2;side++){const sign=side?1:-1;a.setXYZ(n*2+side,Math.cos(curl)*rad-Math.sin(curl)*width*sign,Math.sin(curl)*rad+Math.cos(curl)*width*sign,z);}}a.needsUpdate=true;});
 }
 dispose(){this.group.removeFromParent();for(const mesh of this.tongues)mesh.geometry.dispose();this.hot.dispose();this.energy.dispose();this.haze.dispose();}
}
