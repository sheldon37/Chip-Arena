import * as T from 'three';
import type {Snapshot,GameEvent} from '../shared/simulation.js';
const ring=new T.TorusGeometry(1,.035,4,18),cone=new T.ConeGeometry(1,1,5),orb=new T.SphereGeometry(1,8,5),cube=new T.BoxGeometry(1,1,1),materials=new Map<number,T.MeshBasicMaterial>();
function mat(c:number){if(!materials.has(c))materials.set(c,new T.MeshBasicMaterial({color:c}));return materials.get(c)!;}
function part(g:T.Object3D,shape:T.BufferGeometry,c:number,x:number,y:number,z:number,sx:number,sy:number,sz:number){const m=new T.Mesh(shape,mat(c));m.position.set(x,y,z);m.scale.set(sx,sy,sz);g.add(m);return m;}
/** Geometry, colors, and path shapes communicate the effect; no cosmetic part becomes a hitbox. */
export class ChipEffects{
 shots=new Map<string,T.Group>();pulses:{mesh:T.Mesh;at:number;life:number;size:number}[]=[];
 constructor(private scene:T.Scene){}
 update(state:Snapshot,time:number){
  const ids=new Set(state.projectiles.map(p=>p.id));for(const[id,g]of this.shots)if(!ids.has(id)){g.removeFromParent();this.shots.delete(id);}
  for(const p of state.projectiles){let g=this.shots.get(p.id);const c=p.mods||{};if(!g){g=new T.Group();this.scene.add(g);this.shots.set(p.id,g);
   if(c.ricochet)for(let n=0;n<Math.min(3,c.ricochet);n++)part(g,ring,0xffcc49,0,0,-.12-n*.15,.17,.17,.17);
   if(c.homing)for(let n=0;n<3;n++){const m=part(g,cone,0xf482d4,0,0,-.20,.06,.26,.06);m.rotation.x=-Math.PI/2;m.userData.orbit=n;}
   if(c.splitter)for(const side of [-1,1]){const m=part(g,cube,0x96ffb7,side*.10,0,-.10,.035,.035,.36);m.rotation.y=side*.45;}
   if(c.piercing)part(g,cone,0xf3ffff,0,0,.15,.075,.6,.075).rotation.x=Math.PI/2;
   if(c.velocity){part(g,cube,0x91f2ff,0,0,-.45,.035,.035,.9+.25*c.velocity);for(const side of [-1,1]){const m=part(g,cube,0x69d7ff,side*.10,0,-.4,.025,.025,.45);m.rotation.y=side*-.32;}}
   if(c.kinetic)part(g,ring,0xffaa64,0,0,-.15,.23,.23,.23);
   if(c.shrapnel)for(let n=0;n<3;n++)part(g,orb,0xff845c,Math.cos(n*2.1)*.13,Math.sin(n*2.1)*.13,-.15,.045,.045,.045);
   for(const e of p.elements||[]){if(e==='fire')for(let n=0;n<5;n++){const m=part(g,cone,n%2?0xffdb75:0xff692a,Math.sin(n*2.1)*.07,Math.cos(n*2.1)*.07,-.22-n*.055,.08,.42,.08);m.rotation.x=-Math.PI/2;m.userData.flame=n;}else for(let n=0;n<5;n++){const m=part(g,orb,0x80ed75,Math.sin(n*2.1)*.10,Math.cos(n*2.1)*.10,-n*.12,.06,.06,.06);m.userData.bubble=n;}}
  }
  g.position.set(p.pos.x,p.pos.y,p.pos.z);g.quaternion.setFromUnitVectors(new T.Vector3(0,0,1),new T.Vector3(p.vel.x,p.vel.y,p.vel.z).normalize());
  for(const m of g.children){if(m.userData.orbit!==undefined){const a=time*12+m.userData.orbit*2.1;m.position.x=Math.cos(a)*.15;m.position.y=Math.sin(a)*.15;}if(m.userData.flame!==undefined)m.scale.y=.3+Math.sin(time*28+m.userData.flame)*.12;if(m.userData.bubble!==undefined){const n=m.userData.bubble;m.position.x=Math.sin(time*7+n*2)*.12;m.position.y=Math.cos(time*7+n*2)*.12;}}
  }
  for(let n=this.pulses.length-1;n>=0;n--){const p=this.pulses[n],u=(time-p.at)/p.life;if(u>=1){p.mesh.removeFromParent();(p.mesh.material as T.Material).dispose();this.pulses.splice(n,1);}else{p.mesh.scale.setScalar(.1+p.size*u);(p.mesh.material as T.MeshBasicMaterial).opacity=1-u;}}
 }
 pulse(pos:{x:number;y:number;z:number},color:number,time:number,size:number,life=.35){if(this.pulses.length>=40)return;const m=new T.Mesh(ring,new T.MeshBasicMaterial({color,transparent:true,opacity:1,depthWrite:false}));m.position.set(pos.x,pos.y,pos.z);this.scene.add(m);this.pulses.push({mesh:m,at:time,size,life});}
 event(e:GameEvent,time:number){const c=e.mods||{};if(e.type==='ricochet')this.pulse(e.pos,0xffcc49,time,.8);if(e.type==='split')this.pulse(e.pos,0x96ffb7,time,1.1);if(e.type==='impact'||e.type==='slash'){if(c.kinetic)this.pulse(e.pos,0xffaa64,time,.65+Math.min(3,c.kinetic)*.25);if(c.demolition)this.pulse(e.pos,0xffd45f,time,.5+.12*c.demolition,.23);if(c.shrapnel)this.pulse(e.pos,0xff855c,time,1.4);}}
 clear(){for(const g of this.shots.values())g.removeFromParent();this.shots.clear();for(const p of this.pulses){p.mesh.removeFromParent();(p.mesh.material as T.Material).dispose();}this.pulses=[];}
}
