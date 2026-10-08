import type {Vec} from './config.js';
export type GibPart='head'|'spine'|'left-shoulder'|'right-shoulder'|'left-elbow'|'right-elbow'|'left-hip'|'right-hip'|'left-knee'|'right-knee';
/** Capsule hit coordinates are classified once by the authority, never from an observer's animated pose. */
export function gibPartAt(position:Vec,yaw:number,hit:Vec):GibPart{
 const dx=hit.x-position.x,dz=hit.z-position.z,x=dx*Math.cos(yaw)-dz*Math.sin(yaw),y=hit.y-position.y,side=x>=0?'left':'right';
 if(y>.40)return 'head';if(y<-.28)return `${side}-${y<-.56?'knee':'hip'}`;
 if(Math.abs(x)>.235)return `${side}-${y>.02?'shoulder':'elbow'}`;return 'spine';
}
