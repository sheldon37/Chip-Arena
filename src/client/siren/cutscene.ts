import * as T from 'three';
import type {Snapshot} from '../../shared/simulation.js';
import type {CharacterRig} from '../character-rig.js';
import {sirenWorldOf,sirenOf} from '../../shared/siren-contract.js';
import {bossVisualScale} from '../pve/boss-look.js';

/** Returns the distance to the first solid surface along a ray, or null (the renderer's prop raycast). */
export type CutsceneOcclusion=(origin:{x:number;y:number;z:number},dir:{x:number;y:number;z:number},distance:number)=>number|null;
type Role='victim'|'siren';
interface Story {owner:string;victim:string;at:number;crushAt:number;end:number;role:Role}
interface Shot {pos:T.Vector3;look:T.Vector3;fov:number;shake:number}

/** Seconds the victim's scene keeps playing after the crush: the head bursts, the body drops, the Siren stands over it. */
export const CUTSCENE_TAIL=1.75;
const BARS=.12,SLIDE=.3,FOLLOW=.3;
const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
const sm=(x:number)=>{x=clamp(x);return x*x*(3-2*x);};
const v=()=>new T.Vector3();

/**
 * The Last Laugh cutscene, seen only by the two people in it: the player being held and the player controlling the
 * Siren. From the catch the camera leaves the player and cuts between authored shots (the lift, a close-up, the grip,
 * the crush), with letterbox bars; everyone else keeps playing with the normal camera. Every shot stays on the Siren's
 * right so screen direction never flips. The victim's scene runs on past the crush as their death camera; the Siren
 * player's ends on the release, the moment it can move again. An interrupted hold (the Siren dies, the victim is
 * freed) cuts straight back. Shots follow authoritative time and positions, so both participants see the same timing;
 * only the facing is eased, so a Siren turning mid-hold can't whip the camera around.
 */
export class SirenCutscene {
 active=false;
 private story?:Story;
 private bars=new T.Group();private top:T.Mesh;private bottom:T.Mesh;private camera?:T.Camera;
 private anchor=v();private yaw=0;private aimed=false;private last=0;private sirenHead=v();private victimHead=v();private known=false;
 /** The Siren's visual size (PvE "Am I beautiful?" grows it to 1.5): every framing offset in metres scales with it. */
 private scale=1;
 constructor(){
  const mat=new T.MeshBasicMaterial({color:0x000000,depthTest:false,depthWrite:false,toneMapped:false});
  const plane=new T.PlaneGeometry(1,1);this.top=new T.Mesh(plane,mat);this.bottom=new T.Mesh(plane,mat);
  for(const m of [this.top,this.bottom]){m.renderOrder=1e6;m.frustumCulled=false;this.bars.add(m);}
  this.bars.name='siren-cutscene-bars';this.bars.visible=false;
 }
 /** Who the local viewer is in the current cutscene, if any. */
 get role():Role|undefined{return this.story?.role;}

 /** Decide, once per frame before the camera is chosen, whether the local viewer is in a grab cutscene. */
 check(state:Snapshot,me:string):boolean{
  const t=state.time,g=(sirenWorldOf(state)?.grabs??[]).find(x=>x.victim===me||x.owner===me);
  if(g&&(!this.story||this.story.at!==g.at||this.story.owner!==g.owner)){
   const role:Role=g.victim===me?'victim':'siren';
   this.story={owner:g.owner,victim:g.victim,at:g.at,crushAt:g.crushAt,end:role==='victim'?g.crushAt+CUTSCENE_TAIL:Math.max(g.crushAt+.3,g.releaseAt),role};this.known=false;this.aimed=false;
  }
  // The hold ended. After the crush the scene plays out; before it (interrupted, the Siren died) it cuts back now.
  else if(!g&&this.story&&t<this.story.crushAt-.08)this.story=undefined;
  if(this.story&&(t>=this.story.end||t<this.story.at-.5||!['fight','result'].includes(state.phase)))this.story=undefined;
  this.active=!!this.story;if(!this.active)this.bars.visible=false;return this.active;
 }
 /** End any cutscene immediately (leaving a match, previews). */
 stop(){this.story=undefined;this.active=false;this.bars.visible=false;}

 /** Place the camera for the current shot. rigs give exact head positions; occlusion keeps the camera out of walls. */
 update(state:Snapshot,camera:T.PerspectiveCamera,rigs?:ReadonlyMap<string,CharacterRig>,occlusion?:CutsceneOcclusion){
  const s=this.story;if(!s)return;
  this.track(state,rigs);const shot=this.shot(state.time-s.at,s.crushAt-s.at,s.role);
  // Keep the shot out of walls: pull the camera in toward its subject if something solid is in the way.
  if(occlusion){const dir=shot.pos.clone().sub(shot.look),d=dir.length();if(d>.05){dir.divideScalar(d);const hit=occlusion(shot.look,dir,d);if(hit!==null&&hit<d)shot.pos.copy(shot.look).addScaledVector(dir,Math.max(.35,hit-.18));}}
  camera.position.copy(shot.pos);camera.up.set(0,1,0);camera.lookAt(shot.look);
  if(shot.shake>0){const t=state.time*60;camera.position.x+=Math.sin(t*1.7)*shot.shake;camera.position.y+=Math.sin(t*2.3+1)*shot.shake;camera.rotateZ(Math.sin(t*1.3+2)*shot.shake*.6);}
  camera.fov=shot.fov;camera.updateProjectionMatrix();
  this.letterbox(camera,state.time);
 }

 /** Follow the two bodies while they exist; hold the last known framing once the victim is gone. */
 private track(state:Snapshot,rigs?:ReadonlyMap<string,CharacterRig>){
  const s=this.story!,owner=state.players.find(p=>p.id===s.owner),victim=state.players.find(p=>p.id===s.victim),sr=rigs?.get(s.owner),vr=rigs?.get(s.victim);
  this.scale=owner?bossVisualScale(sirenOf(owner)?.boost,state.time):this.scale;const k=this.scale;
  if(owner){this.anchor.set(owner.pos.x,owner.pos.y,owner.pos.z);
   // Ease toward the Siren's facing instead of copying it: the camera operator follows a turn, it doesn't snap.
   const target=sr?sr.group.rotation.y:owner.yaw,dt=clamp(state.time-this.last,0,.1);
   if(!this.aimed){this.yaw=target;this.aimed=true;}else this.yaw+=Math.atan2(Math.sin(target-this.yaw),Math.cos(target-this.yaw))*(1-Math.exp(-dt/FOLLOW));}
  this.last=state.time;
  const f=v().set(Math.sin(this.yaw),0,Math.cos(this.yaw));
  if(sr&&sr.group.visible){sr.group.updateMatrixWorld(true);sr.head.getWorldPosition(this.sirenHead);this.sirenHead.y+=.12*k;}else this.sirenHead.copy(this.anchor).add(v().set(0,.95*k,0));
  if(vr&&vr.group.visible&&victim&&victim.hp>0){vr.group.updateMatrixWorld(true);vr.head.getWorldPosition(this.victimHead);this.victimHead.y+=.12;this.known=true;}
  else if(!this.known)this.victimHead.copy(this.anchor).addScaledVector(f,.95*k).add(v().set(0,.75*k,0));
 }

 /** The shot list, by seconds since the catch (crush = the crush time on that clock). */
 private shot(t:number,crush:number,role:Role):Shot{
  const k=this.scale,a=this.anchor,f=v().set(Math.sin(this.yaw),0,Math.cos(this.yaw)),r=v().set(-Math.cos(this.yaw),0,Math.sin(this.yaw)),ground=a.y-.85*k;
  const at=(ahead:number,right:number,y:number)=>a.clone().addScaledVector(f,ahead*k).addScaledVector(r,right*k).setY(y);
  const up=(y:number)=>v().set(0,y*k,0);
  const mid=this.sirenHead.clone().lerp(this.victimHead,.5);
  // 1. The lift: low, in profile on the Siren's right, looking up as the victim is hauled off the floor.
  if(t<.9){const u=sm(t/.9);return {pos:at(.8,2.8-.35*u,ground+.55*k),look:mid.clone().add(up(-.15)),fov:46,shake:0};}
  // 2. The close-up. The victim's last view is the grin from below, between the Siren's raised arms. The Siren player
  //    gets the reverse: its own grin from high on the front right, looking down past the victim's head in the grip.
  //    (The victim hangs right in front of the Siren's face, so a front camera has to be high to see past them.)
  if(t<2.2){const u=sm((t-.9)/1.3);
   if(role==='victim')return {pos:this.sirenHead.clone().addScaledVector(f,(.6-.06*u)*k).add(up(-.5+.05*u)),look:this.sirenHead.clone().add(up(.1)),fov:46-5*u,shake:.002};
   return {pos:at(2.3-.2*u,1.7-.2*u,this.sirenHead.y+(1.4-.15*u)*k),look:this.sirenHead.clone().add(up(-.05)),fov:36-4*u,shake:.002};}
  // 3. The grip: a two-shot at eye level, almost in profile, the Siren on the left and the victim hanging at arm's
  //    length on the right while the black fire climbs. It creeps in and the frame starts to tremble.
  if(t<crush-.12){const u=sm((t-2.2)/Math.max(.1,crush-2.32));return {pos:at(1.6-.15*u,3.2-.6*u,mid.y+(-.2+.05*u)*k),look:mid.clone().add(up(-.15)),fov:40-6*u,shake:.002+.008*u};}
  // 4. The crush and what follows, in one shot: a hard cut to a high angle off the front right (from up there the
  //    hanging victim can't hide the grin), a jolt as the head goes, then a slow crane up and back, widening, while the
  //    body drops at the Siren's feet. (The victim is player-sized, so where it lies does not scale.)
  const c=t-crush,u=sm((c-.3)/(CUTSCENE_TAIL-.3)),fallen=a.clone().addScaledVector(f,.85*k).setY(ground+.75);
  const look=this.victimHead.clone().add(v().set(0,-.3,0)).lerp(fallen,sm((c-.35)/1.1));
  return {pos:at(2.7+.7*u,2.7+.7*u,ground+(3.3+.8*u)*k),look,fov:38+6*u,shake:c>=0?.06*clamp(1-c/.35)**2:0};
 }

 /** Black bars at the top and bottom that slide in at the start and out at the end. */
 private letterbox(camera:T.PerspectiveCamera,time:number){
  const s=this.story!;if(this.camera!==camera){this.bars.removeFromParent();camera.add(this.bars);this.camera=camera;}
  const k=Math.min(sm((time-s.at)/SLIDE),sm((s.end-time)/SLIDE)),d=.12,h=2*d*Math.tan(T.MathUtils.degToRad(camera.fov)/2),w=h*camera.aspect*1.1,bar=BARS*h;
  this.bars.visible=k>0;this.top.scale.set(w,bar,1);this.bottom.scale.set(w,bar,1);
  this.top.position.set(0,h/2-bar/2+(1-k)*bar,-d);this.bottom.position.set(0,-h/2+bar/2-(1-k)*bar,-d);
 }
}
