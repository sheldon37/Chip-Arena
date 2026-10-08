import * as T from 'three';
import type {CharacterRig} from './character-rig.js';
/** Three broken red arcs and black tongues of flame; a fixed 12-flame budget per hat. */
export function auditorHalo(){
 const g=new T.Group();g.name='auditor-halo';g.position.y=.46;g.scale.setScalar(.72);g.rotation.z=-.12;
 const red=new T.MeshBasicMaterial({color:0xff1635,toneMapped:false}),glow=new T.MeshBasicMaterial({color:0xff1735,transparent:true,opacity:.15,depthWrite:false,blending:T.AdditiveBlending,toneMapped:false}),black=new T.MeshBasicMaterial({color:0x080007,side:T.DoubleSide});
 for(let i=0;i<3;i++){const arc=new T.Mesh(new T.TorusGeometry(.32,.019,4,22,Math.PI*.57),red);arc.rotation.set(Math.PI/2,0,i*2.1);arc.position.y=i===1?.025:0;g.add(arc);const aura=new T.Mesh(new T.TorusGeometry(.32,.052,4,22,Math.PI*.57),glow);aura.rotation.copy(arc.rotation);aura.position.copy(arc.position);g.add(aura);}
 const shape=new T.Shape();shape.moveTo(-.06,0);shape.bezierCurveTo(-.15,.16,.09,.16,0,.43);shape.bezierCurveTo(.2,.24,.13,.11,.06,0);shape.closePath();const flame=new T.ShapeGeometry(shape);
 for(let i=0;i<12;i++){const m=new T.Mesh(flame,black),a=i*Math.PI/6;m.name='auditor-flame';m.position.set(Math.cos(a)*.3,0,Math.sin(a)*.3);m.rotation.y=-a;m.userData.flame=i;g.add(m);}
 return g;
}
export function animateCosmetics(r:CharacterRig,time:number){const g=r.sockets.head.getObjectByName('auditor-halo');if(!g)return;g.position.y=.46+Math.sin(time*2.3)*.012;g.rotation.y=time*.18;for(const o of g.children)if(o.userData.flame!==undefined){const n=o.userData.flame,pulse=.65+.35*Math.sin(time*6+n*1.7);o.scale.set(.65+.25*Math.sin(time*4+n),pulse,1);o.rotation.z=Math.sin(time*5+n)*.17;}}

export function disposeAuditorHalo(r:CharacterRig){const g=r.sockets.head.getObjectByName('auditor-halo');if(!g)return;const geometries=new Set<T.BufferGeometry>(),materials=new Set<T.Material>();g.traverse(o=>{if(o instanceof T.Mesh){geometries.add(o.geometry);for(const m of Array.isArray(o.material)?o.material:[o.material])materials.add(m);}});geometries.forEach(g=>g.dispose());materials.forEach(m=>m.dispose());g.removeFromParent();}
