// PvE adds while entering (2026-10-04): when an entering add is in sight and where its hitbox is. Shared by the authority
// (horde.ts) and the client's vent climb pose (siren/minions/motion.ts), so the hitbox follows the drawn body.
import {PVE_TIMING,type EntryKind} from './contract.js';

/** Vent climb body offsets (rig-local, metres) keyed on seconds into the FIRST add's climb timeline (followers start at
 *  vent.push): bodyY is the drop of the body origin under its standing height, bodyZ the offset behind the standing point. */
export const VENT_CLIMB_BODY={
 y:[[0,-2.25],[.2,-1.75],[.35,-1.4],[.6,-1.12],[.8,-1.02],[1.05,-.72],[1.3,-.46],[1.5,-.36],[1.7,-.16],[1.95,-.07]],
 z:[[0,-.55],[.8,-.55],[1.05,-.48],[1.3,-.38],[1.5,-.22],[1.72,-.05],[1.95,0]],
} as const;
/** Smooth keyed curve (Catmull-Rom through the keys, flat at the ends); the client's pose curves use the same function. */
export function keyed(t:number,k:readonly (readonly [number,number])[]){
 if(t<=k[0][0])return k[0][1];const n=k.length;if(t>=k[n-1][0])return k[n-1][1];
 let i=0;while(i<n-2&&t>k[i+1][0])i++;const [t1,p1]=k[i],[t2,p2]=k[i+1],p0=i>0?k[i-1][1]:p1,p3=i+2<n?k[i+2][1]:p2,u=(t-t1)/(t2-t1),dt=t2-t1;
 const t0=i>0?k[i-1][0]:t1-dt,t3=i+2<n?k[i+2][0]:t2+dt,m1=(p2-p0)/(t2-t0)*dt,m2=(p3-p1)/(t3-t1)*dt,u2=u*u,u3=u2*u;
 return (2*u3-3*u2+1)*p1+(u3-2*u2+u)*m1+(-2*u3+3*u2)*p2+(u3-u2)*m2;
}
/** Seconds into the vent climb (first-add timeline) when the grate is open and the head and hands are over the rim. */
export const VENT_EXPOSED_AGE=PVE_TIMING.vent.push+PVE_TIMING.vent.open;
/** The authority time an entering add comes into sight and becomes a target:
 *  vents: the grate is open and the head and hands are over the rim (climb age VENT_EXPOSED_AGE);
 *  doors and cells: the leaves / gate have finished opening and the add starts its walk (walkAt). Before that it is
 *  hidden under a closed grate, behind closed leaves or teeth, or underground. */
export function entryExposedAt(kind:EntryKind,walkAt:number,activeAt:number){
 return kind==='vent'?activeAt-(PVE_TIMING.vent.climb-VENT_EXPOSED_AGE):walkAt;
}
/** Vent climb body offset at `time` for an add whose climb ends (activates) at activeAt: {y: below standing, z: behind}. */
export function ventClimbOffset(time:number,activeAt:number){
 const a=PVE_TIMING.vent.climb-(activeAt-time);
 return {y:keyed(a,VENT_CLIMB_BODY.y),z:keyed(a,VENT_CLIMB_BODY.z)};
}
