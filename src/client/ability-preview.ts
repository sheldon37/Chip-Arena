// Ground-target preview for point-targeted E abilities (design §11.2): voxel motes on the gameplay radius, computed with
// the server's camera, aim and groundAimPoint math (abilityAimPoint) through a solid Rapier ray like the server's.
import * as T from 'three';
import {abilityAimPoint,abilityRadius,kitFor} from '../shared/abilities.js';
import {statsFor} from '../shared/content.js';
import {add,mul,forward,type Vec} from '../shared/config.js';
import {cameraPosition,type CameraMode,type ViewRay} from '../shared/view.js';
import {RAPIER,type PlayerView} from '../shared/simulation.js';
/** Point-targeted E ids → ground projection (the frag lands where you aim, the spells project onto the ground). */
const POINT:Record<string,boolean>={'frag-grenade':false,'meteor-strike':true,'geyser-wall':true,singularity:true};
export interface PreviewCircle{pos:Vec;radius:number;clamped:boolean}
export function previewCircles(me:PlayerView,pos:Vec,yaw:number,pitch:number,camera:CameraMode,ray:ViewRay):PreviewCircle[]{
 const d=kitFor(me.spec)?.e;if(!d||!(d.id in POINT))return [];
 const {point,clamped}=abilityAimPoint(cameraPosition(pos,yaw,pitch,camera,ray),pos,forward(yaw,pitch),d.range,POINT[d.id],ray);
 const radius=abilityRadius(d,statsFor(me.chips,me.spec))*(d.id==='singularity'&&me.chips['abyssal-core']?1.25:1);
 // Geyser Wall: three geysers 3 m apart across the horizontal aim.
 return d.id==='geyser-wall'?[-3,0,3].map(k=>({pos:add(point,mul(forward(yaw-Math.PI/2),k)),radius,clamped})):[{pos:point,radius,clamped}];
}
/** The server's solid Rapier ray against mirrored prop colliders (the Predictor's world; its own capsule excluded), so a
 * point on a wall face resolves exactly as the server's aimPoint does. */
export const worldRay=(world:RAPIER.World,exclude?:RAPIER.Collider):ViewRay=>(o,d,max)=>world.castRay(new RAPIER.Ray(o,d),max,true,undefined,undefined,exclude)?.timeOfImpact??null;
export class AbilityPreview{
 readonly mesh=new T.InstancedMesh(new T.BoxGeometry(.14,.14,.14),new T.MeshBasicMaterial({color:0x8af4ff,transparent:true,opacity:.85,depthWrite:false,toneMapped:false}),120);private m=new T.Matrix4();
 constructor(scene:T.Scene){this.mesh.frustumCulled=false;this.mesh.count=0;this.mesh.renderOrder=3;scene.add(this.mesh);}
 /** Rising motes around each circle; a clamped aim (out of range, or the sky) draws a gapped ring. */
 update(circles:PreviewCircle[],time:number){let n=0;
  for(const c of circles){const k=Math.min(40,Math.max(14,Math.round(c.radius*6)));for(let i=0;i<k&&n<120;i++){if(c.clamped&&i%4>1)continue;
   const a=(i/k+time*.05)*Math.PI*2,u=(time*.7+i*.37)%1,sc=1-u*.75;this.m.makeScale(sc,sc,sc).setPosition(c.pos.x+Math.cos(a)*c.radius,c.pos.y+.05+u*.35,c.pos.z+Math.sin(a)*c.radius);this.mesh.setMatrixAt(n++,this.m);}}
  this.mesh.count=n;this.mesh.instanceMatrix.needsUpdate=true;}
}
