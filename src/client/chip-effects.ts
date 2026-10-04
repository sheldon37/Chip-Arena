import * as T from 'three';
import {statsFor} from '../shared/content.js';
import type {Snapshot,GameEvent} from '../shared/simulation.js';
const ring=new T.TorusGeometry(1,.035,4,18),cone=new T.ConeGeometry(1,1,5),orb=new T.SphereGeometry(1,8,5),cube=new T.BoxGeometry(1,1,1),materials=new Map<number,T.MeshBasicMaterial>();
function mat(c:number){if(!materials.has(c))materials.set(c,new T.MeshBasicMaterial({color:c}));return materials.get(c)!;}
function part(g:T.Object3D,shape:T.BufferGeometry,c:number,x:number,y:number,z:number,sx:number,sy:number,sz:number){const m=new T.Mesh(shape,mat(c));m.position.set(x,y,z);m.scale.set(sx,sy,sz);g.add(m);return m;}
/** Geometry, colors, and path shapes communicate the effect; no cosmetic part becomes a hitbox. Chip event visuals are voxels in EventVfx (§19.2); `pulses` stays empty. */
export class ChipEffects{
 shots=new Map<string,T.Group>();readonly pulses:{mesh:T.Mesh}[]=[];
 constructor(private scene:T.Scene){}
 update(state:Snapshot,time:number){
  const ids=new Set(state.projectiles.map(p=>p.id));for(const[id,g]of this.shots)if(!ids.has(id)){g.removeFromParent();this.shots.delete(id);}
  for(const p of state.projectiles){let g=this.shots.get(p.id);const c=p.mods||{};if(!g){const stats=statsFor(c);g=new T.Group();this.scene.add(g);this.shots.set(p.id,g);
   if(stats.bounces)for(let n=0;n<Math.min(3,stats.bounces);n++)part(g,ring,0xffcc49,0,0,-.12-n*.15,.17,.17,.17);
   if(stats.homing)for(let n=0;n<3;n++){const m=part(g,cone,0xf482d4,0,0,-.20,.06,.26,.06);m.rotation.x=-Math.PI/2;m.userData.orbit=n;}
   if(stats.split)for(const side of [-1,1]){const m=part(g,cube,0x96ffb7,side*.10,0,-.10,.035,.035,.36);m.rotation.y=side*.45;}
   if(stats.pierces)part(g,cone,0xf3ffff,0,0,.15,.075,.6,.075).rotation.x=Math.PI/2;
   if(c.velocity){part(g,cube,0x91f2ff,0,0,-.45,.035,.035,.9+.25*c.velocity);for(const side of [-1,1]){const m=part(g,cube,0x69d7ff,side*.10,0,-.4,.025,.025,.45);m.rotation.y=side*-.32;}}
   if(stats.impulse>1.3)part(g,ring,0xffaa64,0,0,-.15,.23,.23,.23);
   if(stats.shrapnel)for(let n=0;n<3;n++)part(g,orb,0xff845c,Math.cos(n*2.1)*.13,Math.sin(n*2.1)*.13,-.15,.045,.045,.045);
   if(stats.pong){part(g,orb,0xff5269,0,0,0,.18,.18,.18);for(let n=0;n<3;n++){const halo=part(g,ring,0xffa6b0,0,0,0,.32,.32,.32);halo.rotation.set(n*Math.PI/3,Math.PI/2,0);halo.userData.spin=n+1;}}
   if(stats.boomerang&&!stats.pong)for(const side of [-1,1]){const wing=part(g,cube,0x7fffe0,side*.2,0,-.05,.32,.045,.09);wing.rotation.y=side*.6;}
   if(stats.zigzag&&!stats.pong)for(let n=0;n<3;n++){const halo=part(g,ring,0x96b5ff,0,0,-n*.18,.15,.15,.15);halo.userData.spin=n+1;}
   if(stats.vacuum){part(g,orb,0x240b39,0,0,0,.22,.22,.22);const halo=part(g,ring,0xd58cff,0,0,0,.35,.35,.35);halo.userData.spin=2;}
   if(c.meteor)for(let n=0;n<6;n++){const tail=part(g,cone,0xffa050,Math.sin(n)*.12,Math.cos(n)*.12,-.3-n*.12,.15,.7,.15);tail.rotation.x=-Math.PI/2;tail.userData.flame=n;}
   if(stats.lifeSteal)part(g,ring,0xf273b5,0,0,-.1,.2,.2,.2);
   for(const e of p.elements||[]){if(e==='fire')for(let n=0;n<5;n++){const m=part(g,cone,n%2?0xffdb75:0xff692a,Math.sin(n*2.1)*.07,Math.cos(n*2.1)*.07,-.22-n*.055,.08,.42,.08);m.rotation.x=-Math.PI/2;m.userData.flame=n;}else for(let n=0;n<5;n++){const m=part(g,orb,0x80ed75,Math.sin(n*2.1)*.10,Math.cos(n*2.1)*.10,-n*.12,.06,.06,.06);m.userData.bubble=n;}}
  }
  g.position.set(p.pos.x,p.pos.y,p.pos.z);g.quaternion.setFromUnitVectors(new T.Vector3(0,0,1),new T.Vector3(p.vel.x,p.vel.y,p.vel.z).normalize());
  for(const m of g.children){if(m.userData.spin)m.rotation.z=time*m.userData.spin*3;if(m.userData.orbit!==undefined){const a=time*12+m.userData.orbit*2.1;m.position.x=Math.cos(a)*.15;m.position.y=Math.sin(a)*.15;}if(m.userData.flame!==undefined)m.scale.y=.3+Math.sin(time*28+m.userData.flame)*.12;if(m.userData.bubble!==undefined){const n=m.userData.bubble;m.position.x=Math.sin(time*7+n*2)*.12;m.position.y=Math.cos(time*7+n*2)*.12;}}
  }
 }
 /** Kept for the renderer's delegation; chip events render in EventVfx (V sweep). */
 event(_e:GameEvent,_time:number){}
 clear(){for(const g of this.shots.values())g.removeFromParent();this.shots.clear();}
}
