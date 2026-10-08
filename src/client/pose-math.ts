import * as T from 'three';
import {clamp} from '../shared/config.js';
/** Tiny shared pose helpers, moved verbatim from character-animation.ts. Frozen after the foundation split:
 * new easing or spring helpers live in new modules, so existing poses keep their exact values. */
export const smooth=(a:number)=>{a=clamp(a,0,1);return a*a*(3-2*a);};
export function blendRotation(node:T.Object3D,x:number,y:number,z:number,weight:number){node.quaternion.slerp(new T.Quaternion().setFromEuler(new T.Euler(x,y,z)),clamp(weight,0,1));}
