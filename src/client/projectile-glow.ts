import * as T from 'three';
import type {Snapshot} from '../shared/simulation.js';
import {WEAPONS} from '../shared/content.js';
import {ELEMENTS} from '../shared/elements.js';

function glowTexture(){
 const size=64,data=new Uint8Array(size*size*4);
 for(let y=0;y<size;y++)for(let x=0;x<size;x++){const r=Math.hypot((x+.5)/size*2-1,(y+.5)/size*2-1),i=(y*size+x)*4;data[i]=data[i+1]=data[i+2]=255;data[i+3]=Math.round(255*Math.exp(-r*r*4.5)*Math.max(0,1-r*r));}
 const texture=new T.DataTexture(data,size,size);texture.needsUpdate=true;texture.magFilter=texture.minFilter=T.LinearFilter;return texture;
}
/** Soft additive halos plus three pooled lights; no full-screen bloom or per-bullet light allocation. */
export class ProjectileGlow {
 private texture=glowTexture();private halos=new Map<string,T.Sprite>();private materials=new Map<number,T.SpriteMaterial>();
 readonly lights:T.PointLight[]=[];
 constructor(private scene:T.Scene){for(let i=0;i<3;i++){const light=new T.PointLight(0xffffff,0,5,2);light.name='projectile-light';scene.add(light);this.lights.push(light);}}
 update(shots:Snapshot['projectiles'],camera:T.Camera,time:number,quality:number){
  const ids=new Set(shots.map(p=>p.id));for(const[id,halo]of this.halos)if(!ids.has(id)||quality<=0){this.scene.remove(halo);this.halos.delete(id);}
  for(const p of quality>0?shots:[]){const elements=p.elements||[],element=elements.length?elements[Math.floor(time*20)%elements.length]:undefined,color=element?ELEMENTS[element].color:WEAPONS[p.spec].color;
   let material=this.materials.get(color);if(!material){material=new T.SpriteMaterial({map:this.texture,color,transparent:true,opacity:.88,blending:T.AdditiveBlending,depthWrite:false,depthTest:true,toneMapped:false});this.materials.set(color,material);}
   let halo=this.halos.get(p.id);if(!halo){halo=new T.Sprite(material);halo.name='projectile-glow';this.scene.add(halo);this.halos.set(p.id,halo);}halo.material=material;halo.position.set(p.pos.x,p.pos.y,p.pos.z);
   const size=Math.min(4,Math.max(.70,p.radius*8.5))*(quality<.5?.82:1);halo.scale.set(size,size,1);
  }
  const nearby=quality>0?[...shots].sort((a,b)=>camera.position.distanceToSquared(new T.Vector3(a.pos.x,a.pos.y,a.pos.z))-camera.position.distanceToSquared(new T.Vector3(b.pos.x,b.pos.y,b.pos.z))).slice(0,quality<.5?1:3):[];
  this.lights.forEach((light,i)=>{const p=nearby[i];light.intensity=0;if(!p)return;light.position.set(p.pos.x,p.pos.y,p.pos.z);light.color.copy(this.halos.get(p.id)!.material.color);light.intensity=WEAPONS[p.spec].class==='magic'?5:2.8;light.distance=WEAPONS[p.spec].class==='magic'?5:3.8;});
 }
 clear(){for(const halo of this.halos.values())this.scene.remove(halo);this.halos.clear();this.lights.forEach(l=>l.intensity=0);}
 dispose(){this.clear();this.lights.forEach(l=>this.scene.remove(l));this.materials.forEach(m=>m.dispose());this.materials.clear();this.texture.dispose();}
 get count(){return this.halos.size;}
}
