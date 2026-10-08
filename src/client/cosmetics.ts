import {auditorHalo,disposeAuditorHalo} from './auditor-halo.js';
import * as T from 'three';
import {CharacterRig,rigBox} from './character-rig.js';
import {Cosmetics,cosmetics} from '../shared/cosmetics.js';
/** Shared per colour like the rig palette, so re-dressing never leaks materials. */
const roundMaterials=new Map<string,T.MeshLambertMaterial>();
const roundMaterial=(col:T.ColorRepresentation)=>{const key=String(col);let m=roundMaterials.get(key);if(!m){m=new T.MeshLambertMaterial({color:col});roundMaterials.set(key,m);}return m;};
export function dressRig(r:CharacterRig,raw?:Cosmetics){const c=cosmetics(raw,true),key=JSON.stringify(c);if(r.group.userData.cosmeticKey===key)return;r.group.userData.cosmeticKey=key;disposeAuditorHalo(r);const g=r.sockets.head;g.traverse(o=>{if(o instanceof T.Mesh&&o.geometry.type!=='BoxGeometry')o.geometry.dispose();});g.clear();const color=0xff6b35,box=(name:string,x:number,y:number,z:number,sx:number,sy:number,sz:number,col:T.ColorRepresentation=color)=>rigBox(g,name,x,y,z,sx,sy,sz,col);
 const round=(name:string,geo:T.BufferGeometry,col:T.ColorRepresentation,y:number)=>{const m=new T.Mesh(geo,roundMaterial(col));m.name=name;m.position.y=y;g.add(m);return m;};
 if(c.hat==='auditor-halo')g.add(auditorHalo());
 if(c.hat==='cap'){box('cap',0,.39,0,.4,.12,.37,0xff6b35);box('brim',0,.345,.2,.43,.035,.23,0x24252d);}
 if(c.hat==='beanie'){box('beanie',0,.425,0,.36,.17,.34,0xd277ff);box('beanie-top',0,.525,0,.28,.05,.26,0xd277ff);box('cuff',0,.36,0,.38,.07,.36,0xb760e8);box('pom',0,.585,0,.095,.08,.095,0xf5dcff);}
 if(c.hat==='top'){box('brim',0,.355,0,.51,.05,.46,0x15151a);box('top',0,.53,0,.33,.32,.30,0x191a20);box('band',0,.425,0,.34,.055,.31,0xff6b35);}
 if(c.hat==='wizard'){round('brim',new T.CylinderGeometry(.34,.34,.04,10),0x6334a6,.365);round('hat',new T.ConeGeometry(.245,.6,7),0x904bea,.66);box('star',0,.54,.175,.07,.07,.025,0xffd767);}
 if(c.hat==='crown'){box('band',0,.385,0,.4,.10,.37,0xffbf3e);for(const [x,z,h] of [[-.17,.155,.17],[0,.165,.13],[.17,.155,.17],[.18,0,.13],[.17,-.155,.17],[0,-.165,.13],[-.17,-.155,.17],[-.18,0,.13]])box('point',x,.435+h/2,z,.055,h,.055,0xffd269);box('jewel',0,.385,.19,.07,.05,.02,0xe8344f);}
 if(c.hat==='cone'){round('base',new T.CylinderGeometry(.3,.3,.04,4),0xff662e,.36);round('cone',new T.ConeGeometry(.23,.6,8),0xff6b35,.66);round('stripe',new T.CylinderGeometry(.10,.13,.08,8),0xf5f5f5,.68);}
 if(c.hat==='antenna'){box('stem',0,.53,0,.03,.34,.03,0x9c9da8);round('beacon',new T.SphereGeometry(.09,6,4),0xff4949,.72);}
 if(c.hat==='halo'){const ring=round('halo',new T.TorusGeometry(.28,.028,4,18),0xffd971,.55);ring.rotation.x=Math.PI/2;}
 if(c.hat==='cowboy'){round('brim',new T.CylinderGeometry(.38,.38,.055,8),0x8e5938,.37).scale.z=.8;box('hat',0,.49,0,.34,.22,.29,0xbc8352);box('band',0,.415,0,.35,.05,.30,0x42322a);}
}
