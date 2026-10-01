import * as T from 'three';
import type {CharacterRig} from '../character-rig.js';
import type {PlayerView,Snapshot,GameEvent} from '../../shared/simulation.js';
import {SIREN,SIREN_TIMING,sirenOf,sirenShotOf,sirenWorldOf,type SirenFirePatch,type SirenGrab,type SirenMark,type SirenShot,type SirenEruption,type SirenRain} from '../../shared/siren-contract.js';
import {FlameField,type FlameTone} from './flames.js';
import {PALETTE,SIREN_CLOCK,glowMaterial,orbMaterial,haloMaterial,decalMaterial,radialTexture,tickSirenClock,flameShellMaterial} from './materials.js';
import {setSirenQuality} from './motion.js';

const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
const sm=(x:number)=>{x=clamp(x);return x*x*(3-2*x);};
const v3=new T.Vector3(),v4=new T.Vector3(),q0=new T.Quaternion(),up=new T.Vector3(0,1,0),INK_COLOR=new T.Color(0x0c0a10);

/** The lock-on beeps of Marked: intervals shrink from .3 s toward .07 s until the throw. Audio should use these too. */
export function markBeeps(lockAt:number,throwAt:number):number[]{
 const out:number[]=[];let t=lockAt,gap=.3;while(t<throwAt-.02&&out.length<40){out.push(t);t+=gap;gap=Math.max(.07,gap*.8);}return out;
}

/** A crescent band in the XZ plane (front = +z, sweeping from -x to +x); uv.x runs along the arc, uv.y across it. */
function arcGeometry(span=Math.PI*.85,seg=36){
 const pos:number[]=[],uv:number[]=[],idx:number[]=[];
 for(let i=0;i<=seg;i++){const u=i/seg,a=-span/2+span*u,s=Math.sin(a),c=Math.cos(a);for(const r of [.5,1]){pos.push(s*r,0,c*r);uv.push(u,r===1?1:0);}}
 for(let i=0;i<seg;i++){const a=i*2;idx.push(a,a+1,a+3,a,a+3,a+2);}
 const g=new T.BufferGeometry();g.setAttribute('position',new T.Float32BufferAttribute(pos,3));g.setAttribute('uv',new T.Float32BufferAttribute(uv,2));g.setIndex(idx);return g;
}
const ARC=arcGeometry();
/** Claw slash material: black band with a pale burning outer edge, revealed by uSweep and faded by uFade. */
function slashMaterial(){
 return new T.ShaderMaterial({uniforms:{uSweep:{value:0},uFade:{value:1},uInk:{value:new T.Color(PALETTE.ink)},uEdge:{value:new T.Color(PALETTE.ash)},uTime:SIREN_CLOCK},
  vertexShader:'varying vec2 vUv;void main(){vUv=uv;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.0);}',
  fragmentShader:`uniform float uSweep,uFade,uTime;uniform vec3 uInk,uEdge;varying vec2 vUv;
  void main(){float lead=uSweep-vUv.x;if(lead<0.0)discard;float tail=smoothstep(0.0,.06,lead)*(1.0-smoothstep(.3,.85,lead));
   // Three claw streaks: each a dark gouge with a burning pale upper edge, tapering at both ends of the arc.
   float k=fract(vUv.y*3.0),taper=sin(3.14159*clamp(vUv.x,0.0,1.0));float gouge=smoothstep(.12,.3,k)*(1.0-smoothstep(.62,.8,k));
   float rag=.5+.5*sin(vUv.x*70.0+uTime*24.0);float edge=smoothstep(.5,.62,k)*(1.0-smoothstep(.62,.8,k))*(.7+.3*rag);
   float a=max(gouge*.9,edge)*tail*uFade*smoothstep(.0,.25,taper*(.6+.4*k));if(a<.02)discard;gl_FragColor=vec4(mix(uInk,uEdge,edge),a);}`,
  transparent:true,depthWrite:false,side:T.DoubleSide,toneMapped:false});
}
const JET=new T.CylinderGeometry(.55,1,1,32,8,true).translate(0,.5,0),SHELL=new T.SphereGeometry(1,32,20),RING=new T.RingGeometry(.86,1,64),RING_THICK=new T.RingGeometry(.55,1,64),DISC=new T.CircleGeometry(1,48),ORB=new T.IcosahedronGeometry(1,2),COLUMN=new T.CylinderGeometry(.7,1,1,24,1,true),TICK=new T.BoxGeometry(1,1,1);
type Transient={obj:T.Object3D;born:number;life:number;tick(u:number,obj:T.Object3D):void;dispose?():void};
interface ShotView {mesh:T.Group;core:T.Mesh;halo:T.Sprite;kind:SirenShot['kind'];last:T.Vector3;born:number;light?:T.PointLight;marker?:T.Mesh}
interface PatchView {decal:T.Mesh;acc:number;seen:number}
interface MarkView {group:T.Group;ring:T.Mesh;inner:T.Mesh;ticks:T.Mesh[];beeps:number[]}
interface EruptionView {ring:T.Mesh;fill:T.Mesh;column:T.Mesh;burst:boolean;acc:number}

/** Every Siren effect that is not part of the body: projectiles, fire on the ground, telegraphs, slashes, the grab. */
export class SirenFx {
 readonly group=new T.Group();
 private fire=new FlameField(1600);
 private transients:Transient[]=[];
 private shots=new Map<string,ShotView>();private patches=new Map<string,PatchView>();private marks=new Map<string,MarkView>();private eruptions=new Map<string,EruptionView>();
 private rainAcc=new Map<string,number>();private auraAt=new Map<string,number>();private grabFx=new Map<string,number>();
 private charred=new Map<string,{rig:CharacterRig;saved:Map<T.Mesh,T.Material|T.Material[]>;copies:{m:T.MeshLambertMaterial;color:T.Color;emissive:T.Color}[]}>();
 private quality=1;private time=0;private camera?:T.Camera;
 private ringDark=glowMaterial(0x000000,{transparent:true,opacity:.55,side:T.DoubleSide});
 private shotOrb=orbMaterial(PALETTE.ink,PALETTE.ash,1.8);private seekerOrb=orbMaterial(0x020103,0xffffff,3.2);
 private warnRing=glowMaterial(PALETTE.warn,{transparent:true,opacity:.9,side:T.DoubleSide,flicker:.15});
 private warnFill=glowMaterial(PALETTE.warnDeep,{transparent:true,opacity:.35,side:T.DoubleSide});
 private scorch=decalMaterial(0x000000,.75);
 constructor(private scene:T.Scene){this.group.name='siren-fx';scene.add(this.group);this.group.add(this.fire.mesh);}

 /** Projectiles this class renders (the renderer skips its default mesh and trail for these). */
 owns(shot:object){return !!sirenShotOf(shot);}

 update(state:Snapshot,time:number,rigs:ReadonlyMap<string,CharacterRig>,quality:number,camera?:T.Camera){
  const dt=clamp(time-this.time,0,.05);this.time=time;this.quality=quality;this.camera=camera;setSirenQuality(quality);tickSirenClock(time);
  const world=sirenWorldOf(state);
  this.updateShots(state,time,dt);
  this.updatePatches(world?.patches??[],time,dt);
  this.updateEruptions(world?.eruptions??[],time,dt);
  this.updateRains(world?.rains??[],time,dt);
  this.updateMarks(world?.marks??[],state,time,camera);
  this.updateGrabs(world?.grabs??[],state,rigs,time,dt);
  this.updateAuras(state,rigs,time);
  for(let i=this.transients.length-1;i>=0;i--){const tr=this.transients[i],u=(time-tr.born)/tr.life;if(u>=1||u<-.5){this.group.remove(tr.obj);tr.dispose?.();this.transients.splice(i,1);}else tr.tick(Math.max(0,u),tr.obj);}
  this.fire.update(dt);
 }

 event(e:GameEvent,time:number,quality=this.quality){
  this.quality=quality;const p=e.pos,q=quality<.5?.5:1;if(!p)return;
  switch(e.type){
   case 'siren-swipe':case 'siren-rend':this.slash(e,time);break;
   case 'siren-slam':{const r=e.radius??3.6;this.shock(p,r,.45,true);this.shock(p,r*.65,.3,false);this.cracks(p,r*.8,time);this.fire.burst(p.x,p.y+.1,p.z,Math.round(70*q),5.5,.9,.16,'black',.6,1.2);this.fire.burst(p.x,p.y+.1,p.z,Math.round(14*q),4,.7,.06,'ember',1.5);this.flash(p,2.6,.25,.5);break;}
   case 'siren-rush':this.fire.burst(p.x,p.y-.4,p.z,Math.round(30*q),3,.6,.12,'black',.4);this.shock({...p,y:p.y-.83},1.4,.25,false);break;
   case 'siren-laugh':case 'siren-mark':this.shock({...p,y:p.y-.83},2.2,.6,true);this.fire.burst(p.x,p.y+.5,p.z,Math.round(16*q),1.5,.8,.09,'black',1.4);break;
   case 'siren-lunge':this.fire.burst(p.x,p.y,p.z,Math.round(20*q),2.5,.5,.1,'black',.5);break;
   case 'siren-catch':this.fire.burst(p.x,p.y+.4,p.z,Math.round(30*q),2.2,.7,.1,'black',1);this.flash(p,1.2,.15,.6);break;
   case 'siren-crush':this.fire.burst(p.x,p.y,p.z,Math.round(60*q),4,.9,.12,'black',1.2);this.fire.burst(p.x,p.y,p.z,Math.round(16*q),5,.6,.05,'ember',1);this.flash(p,1.6,.2,.9);this.shock({...p,y:p.y-1.3},2.4,.4,true);break;
   case 'siren-whiff':this.fire.burst(p.x,p.y,p.z,Math.round(14*q),2,.5,.09,'smoke',.6);break;
   case 'siren-cast':this.fire.burst(p.x,p.y,p.z,Math.round((e.message==='damnation'?36:10)*q),e.message==='damnation'?3:1.6,.45,.09,'black',.8);this.flash(p,e.message==='damnation'?1.6:.7,.12,.7);break;
   case 'siren-explode':this.explosion(p,e.radius??3.5,time);break;
   case 'siren-eruption':{const r=e.radius??2.2;this.shock(p,r*1.4,.35,false);this.flash({...p,y:p.y+1},2.5,.2,.7);break;}
   case 'siren-drop':this.fire.burst(p.x,p.y+.05,p.z,Math.round(10*q),1.8,.45,.08,'black',.8);this.shock(p,.7,.2,false);break;
   case 'siren-lock':break;
   case 'siren-seeker-out':this.fire.burst(p.x,p.y,p.z,Math.round(70*q),4.5,1,.14,'smoke',1);this.flash(p,2.8,.3,1);this.shock({...p,y:Math.max(.02,p.y-1)},2.6,.4,false);break;
   case 'siren-seeker-hit':this.explosion(p,4.2,time);this.flash(p,3.6,.35,1);break;
   case 'siren-hit':this.fire.burst(p.x,p.y,p.z,Math.round(12*q),2.2,.45,.08,'black',.9);this.fire.burst(p.x,p.y,p.z,Math.round(4*q),3,.3,.04,'ember',.5);break;
  }
 }

 clear(){for(const tr of this.transients){this.group.remove(tr.obj);tr.dispose?.();}this.transients=[];
  for(const s of this.shots.values())this.dropShot(s);this.shots.clear();for(const v of this.patches.values())this.group.remove(v.decal);this.patches.clear();
  for(const m of this.marks.values())this.group.remove(m.group);this.marks.clear();for(const e of this.eruptions.values())this.group.remove(e.ring,e.fill,e.column);this.eruptions.clear();this.fire.clear();}

 // ---- one-shot pieces ----------------------------------------------------------------------------------------
 private add(obj:T.Object3D,life:number,tick:Transient['tick'],dispose?:()=>void){this.group.add(obj);this.transients.push({obj,born:this.time,life,tick,dispose});}
 /** Expanding floor ring: dark (a soot shockwave) or pale (a burning edge). */
 private shock(p:{x:number;y:number;z:number},radius:number,life:number,dark:boolean){
  const mat=glowMaterial(dark?0x000000:PALETTE.ash,{transparent:true,opacity:dark?.6:.8,side:T.DoubleSide,additive:!dark});const m=new T.Mesh(dark?RING_THICK:RING,mat);m.rotation.x=-Math.PI/2;m.position.set(p.x,p.y+.04,p.z);
  this.add(m,life,(u,o)=>{const s=.2+radius*(1-(1-u)**3);o.scale.setScalar(s);mat.uniforms.uOpacity.value=(dark?.6:.8)*(1-u)**1.5;},()=>mat.dispose());
 }
 /** Bright (additive) or dark puff sprite. */
 private flash(p:{x:number;y:number;z:number},size:number,life:number,opacity:number,dark=false){
  const mat=dark?new T.SpriteMaterial({map:radialTexture(),color:0x000000,transparent:true,opacity,depthWrite:false}):haloMaterial(0xf2eeff,opacity);const s=new T.Sprite(mat);s.position.set(p.x,p.y,p.z);
  this.add(s,life,(u,o)=>{o.scale.setScalar(size*(.4+.6*Math.sqrt(u)));mat.opacity=opacity*(1-u)**2;},()=>mat.dispose());
 }
 /** Claw slash: a crescent that sweeps across in the swing direction and leaves small black flames burning in place. */
 private slash(e:GameEvent,_time:number){
  const rend=e.type==='siren-rend',side=(e.amount??1)>=0?1:-1,p=e.pos,reach=e.radius??(rend?3:2.3),src=this.lastPos.get(e.source??'');
  const yaw=src?Math.atan2(p.x-src.x,p.z-src.z):this.lastYaw.get(e.source??'')??0;
  // The band follows the claw tips: a crescent around the shoulder, about an arm and claws long.
  const mat=slashMaterial(),m=new T.Mesh(ARC,mat),outer=rend?1.6:1.2,span=Math.PI*.85;
  const pivot=new T.Group();pivot.position.set(src?src.x:p.x-Math.sin(yaw)*.8,(src?src.y:p.y-.2)+.38,src?src.z:p.z-Math.cos(yaw)*.8);pivot.rotation.set(0,yaw,0,'YXZ');
  m.position.set(0,0,.1);m.scale.set(outer*-side,1,outer);m.rotation.z=side*(rend?.9:.5);m.rotation.x=rend?.3:.1;pivot.add(m);
  const life=rend?.42:.24,flames=Math.round((rend?26:9)*(this.quality<.5?.5:1)),speed=rend?3.2:3.6;let laid=0;
  this.add(pivot,life,u=>{mat.uniforms.uSweep.value=Math.min(1.9,u*speed);mat.uniforms.uFade.value=1-sm((u-.45)/.55);
   // Lay the lingering flames along the part of the arc already swept.
   const want=Math.floor(Math.min(1,u*speed)*flames);if(laid>=want)return;pivot.updateWorldMatrix(true,true);
   for(;laid<want;laid++){const a=-span/2+span*(laid+.5)/flames,rr=.62+Math.random()*.34;v4.set(Math.sin(a)*rr,0,Math.cos(a)*rr);m.localToWorld(v4);
    for(let k=0;k<(rend?3:2);k++){const ember=Math.random()<.06;this.fire.emit(v4.x+(Math.random()-.5)*.1,v4.y+(Math.random()-.5)*.08,v4.z+(Math.random()-.5)*.1,(Math.random()-.5)*.2,.5+Math.random()*.6,(Math.random()-.5)*.2,(rend?.85:.5)+Math.random()*.35,(rend?.075:.055)*(.7+Math.random()*.5)*(ember?.45:1),ember?'ember':'soot',1.8,2.2);}}
  },()=>mat.dispose());
 }
 private lastYaw=new Map<string,number>();private lastPos=new Map<string,{x:number;y:number;z:number}>();
 /** Radiating ground cracks (Grave Slam). */
 private cracks(p:{x:number;y:number;z:number},radius:number,_time:number){
  const g=new T.Group();g.position.set(p.x,p.y+.03,p.z);const mat=new T.MeshBasicMaterial({color:0x000000,transparent:true,opacity:.85,depthWrite:false});
  for(let i=0;i<9;i++){const a=i/9*Math.PI*2+Math.random()*.4,len=radius*(.5+Math.random()*.5),c=new T.Mesh(TICK,mat);c.scale.set(.07+Math.random()*.05,.02,len);c.position.set(Math.sin(a)*len/2,0,Math.cos(a)*len/2);c.rotation.y=a;g.add(c);}
  this.add(g,1.6,u=>{mat.opacity=.85*(1-sm((u-.5)/.5));g.scale.setScalar(.6+.4*Math.min(1,u*8));},()=>mat.dispose());
 }
 /** Black-flame explosion (Damnation, seeker): a dark sphere that swells and tears into flame, rings, embers. */
 private explosion(p:{x:number;y:number;z:number},radius:number,_time:number){
  const q=this.quality<.5?.5:1;const mat=flameShellMaterial(),ball=new T.Mesh(SHELL,mat);ball.position.set(p.x,p.y+.2,p.z);mat.uniforms.uSpeed.value=2.2;
  this.add(ball,.7,(u,o)=>{const s=radius*.8*Math.sqrt(Math.min(1,u*2.4));o.scale.set(s,s*.85,s);o.rotation.y=u*2;mat.uniforms.uFade.value=1-sm((u-.35)/.65);},()=>mat.dispose());
  this.shock(p,radius*1.15,.5,true);this.shock(p,radius,.35,false);this.flash({...p,y:p.y+.6},radius*1.6,.3,.85);
  this.fire.burst(p.x,p.y+.4,p.z,Math.round(110*q),radius*2.2,1.1,.2,'black',1.2,1.6);this.fire.burst(p.x,p.y+.4,p.z,Math.round(40*q),radius*1.6,1.2,.14,'smoke',1.6,1.2);this.fire.burst(p.x,p.y+.4,p.z,Math.round(24*q),radius*2.5,.8,.05,'ember',2);
 }

 // ---- persistent state ------------------------------------------------------------------------------------------
 private updateShots(state:Snapshot,time:number,dt:number){
  const live=new Set<string>(),q=this.quality<.5?.5:this.quality;
  for(const shot of state.projectiles){const s=sirenShotOf(shot);if(!s)continue;live.add(shot.id);let v=this.shots.get(shot.id);
   if(!v){v=this.makeShot(s);this.shots.set(shot.id,v);}
   v.mesh.position.set(shot.pos.x,shot.pos.y,shot.pos.z);v.last.copy(v.mesh.position);
   const age=time-s.born,pulse=.5+.5*Math.sin(time*(s.kind==='seeker'?9:20)),vel=v3.set(shot.vel.x,shot.vel.y,shot.vel.z),speed=vel.length();
   if(s.kind==='seeker'){v.core.scale.setScalar(.42*(1+.08*pulse));v.halo.scale.setScalar(2.6+.7*pulse);if(v.light)v.light.intensity=3+2*pulse;
    v.core.rotation.y+=dt*2;
    this.trail(shot.pos,vel,dt,48*q,.16,.9,'black',.35);this.trail(shot.pos,vel,dt,10*q,.06,.8,'ember',.45);}
   else if(s.kind==='drop'){if(speed>0)v.mesh.quaternion.setFromUnitVectors(up,vel.clone().normalize().negate());this.trail(shot.pos,vel,dt,40*q,.07,.25,'black',.05);
    if(v.marker&&s.land&&s.landAt!==undefined){const u=clamp(1-(s.landAt-time)/.9);v.marker.position.set(s.land.x,(s.land.y??0)+.05,s.land.z);v.marker.scale.setScalar(.75-.5*u);(v.marker.material as T.ShaderMaterial).uniforms.uOpacity.value=.35+.6*u;}}
   else{const big=s.kind==='damnation';v.core.scale.setScalar((big?.52:.27)*(1+.1*pulse));v.halo.scale.setScalar((big?3.4:1.5)*(1+.15*pulse));v.core.rotation.x+=dt*6;v.core.rotation.y+=dt*4;
    this.trail(shot.pos,vel,dt,(big?160:80)*q,big?.24:.13,big?.75:.5,'black',big?.4:.2);this.trail(shot.pos,vel,dt,(big?40:16)*q,big?.16:.09,big?.9:.6,'smoke',big?.5:.25);
    // Corona: black tongues licking off the surface, against the violet glow.
    const r=big?.5:.26;for(let k=0;k<Math.round((big?50:24)*q*dt*10);k++){const a=Math.random()*Math.PI*2,u=Math.random()*2-1,w=Math.sqrt(1-u*u);this.fire.emit(shot.pos.x+Math.cos(a)*w*r,shot.pos.y+u*r,shot.pos.z+Math.sin(a)*w*r,Math.cos(a)*w*.8-vel.x*.1,u*.8+.6,Math.sin(a)*w*.8-vel.z*.1,.18+Math.random()*.15,(big?.12:.07)*(.7+Math.random()*.5),'black',2,2.5);}
    if(Math.random()<dt*(big?14:6))this.trail(shot.pos,vel,dt,1/dt,.05,.4,'ember',.1);}
   void age;
  }
  for(const [id,v] of this.shots)if(!live.has(id)){if(time-v.born>.05&&v.kind!=='seeker'&&v.kind!=='drop')this.fire.burst(v.last.x,v.last.y,v.last.z,v.kind==='damnation'?20:10,2,.5,.1,'black',1);this.dropShot(v);this.shots.delete(id);}
 }
 private trail(p:{x:number;y:number;z:number},vel:T.Vector3,dt:number,rate:number,size:number,life:number,tone:FlameTone,spread:number){
  const n=rate*dt,count=Math.floor(n)+(Math.random()<n%1?1:0);
  for(let i=0;i<count;i++){const back=Math.random()*dt;this.fire.emit(p.x-vel.x*back+(Math.random()-.5)*spread,p.y-vel.y*back+(Math.random()-.5)*spread,p.z-vel.z*back+(Math.random()-.5)*spread,-vel.x*.08+(Math.random()-.5)*.4,.3+Math.random()*.6,-vel.z*.08+(Math.random()-.5)*.4,life*(.6+Math.random()*.6),size*(.7+Math.random()*.6),tone,1.8,2.2);}
 }
 private makeShot(s:SirenShot):ShotView{
  const g=new T.Group();g.name='siren-shot';const seeker=s.kind==='seeker',drop=s.kind==='drop';
  const core=new T.Mesh(ORB,seeker?this.seekerOrb:this.shotOrb);core.scale.setScalar(drop?.12:.2);if(drop)core.scale.set(.09,.24,.09);g.add(core);
  const halo=new T.Sprite(haloMaterial(seeker?0xffffff:drop?0xd6ccff:0x8f78e8,seeker?.95:drop?.3:.55));halo.scale.setScalar(drop?.6:1);g.add(halo);halo.renderOrder=-1;
  const v:ShotView={mesh:g,core,halo,kind:s.kind,last:new T.Vector3(),born:s.born};
  if(seeker){const light=new T.PointLight(0xf1ecff,4,9,2);g.add(light);v.light=light;const red=new T.Sprite(haloMaterial(PALETTE.warn,.55));red.scale.setScalar(1.3);g.add(red);}
  if(drop){const marker=new T.Mesh(RING_THICK,glowMaterial(PALETTE.warn,{transparent:true,opacity:.6,side:T.DoubleSide}));marker.rotation.x=-Math.PI/2;this.group.add(marker);v.marker=marker;}
  this.group.add(g);return v;
 }
 private dropShot(v:ShotView){this.group.remove(v.mesh);(v.halo.material as T.Material).dispose();if(v.marker){this.group.remove(v.marker);(v.marker.material as T.Material).dispose();}v.mesh.traverse(o=>{if(o instanceof T.Sprite&&o!==v.halo)(o.material as T.Material).dispose();});}

 private updatePatches(list:SirenFirePatch[],time:number,dt:number){
  const live=new Set<string>(),q=this.quality<.5?.45:this.quality;
  for(const f of list){live.add(f.id);let v=this.patches.get(f.id);
   if(!v){const decal=new T.Mesh(DISC,this.scorch);decal.rotation.x=-Math.PI/2;decal.renderOrder=-1;this.group.add(decal);v={decal,acc:0,seen:time};this.patches.set(f.id,v);
    if(time-f.at<.3)this.fire.burst(f.pos.x,f.pos.y+.1,f.pos.z,Math.round(12+16*f.radius),2+f.radius,.6,.11,'black',1);}
   const fade=1-sm((time-(f.until-.8))/.8),grow=sm((time-f.at)/.25);v.decal.position.set(f.pos.x,f.pos.y+.025,f.pos.z);v.decal.scale.setScalar(f.radius*1.25*Math.max(.05,grow));v.decal.visible=fade>.02;
   // Flames rise from all over the patch; density follows its area.
   v.acc+=dt*q*fade*(f.kind==='trail'?16:26)*Math.max(.6,f.radius*f.radius);
   while(v.acc>=1){v.acc-=1;const a=Math.random()*Math.PI*2,r=Math.sqrt(Math.random())*f.radius*.95;this.fire.emit(f.pos.x+Math.cos(a)*r,f.pos.y+.05,f.pos.z+Math.sin(a)*r,(Math.random()-.5)*.25,.5+Math.random()*.9,(Math.random()-.5)*.25,.45+Math.random()*.45,(.08+Math.random()*.08)*(f.kind==='scorch'?1.3:1),Math.random()<.04?'ember':Math.random()<.15?'smoke':'black',2.4,1.8);}
  }
  for(const [id,v] of this.patches)if(!live.has(id)){this.group.remove(v.decal);this.patches.delete(id);}
 }

 private updateEruptions(list:SirenEruption[],time:number,dt:number){
  const live=new Set<string>(),q=this.quality<.5?.5:1;
  for(const e of list){live.add(e.id);let v=this.eruptions.get(e.id);
   if(!v){const ring=new T.Mesh(RING,this.warnRing),fill=new T.Mesh(DISC,this.warnFill),column=new T.Mesh(JET,flameShellMaterial());ring.rotation.x=fill.rotation.x=-Math.PI/2;column.visible=false;this.group.add(ring,fill,column);v={ring,fill,column,burst:false,acc:0};this.eruptions.set(e.id,v);}
   const warn=time<e.burstAt,u=clamp((time-e.at)/Math.max(.05,e.burstAt-e.at)),pulse=.5+.5*Math.sin(time*(10+16*u));
   v.ring.visible=v.fill.visible=warn;v.ring.position.set(e.pos.x,e.pos.y+.06,e.pos.z);v.fill.position.set(e.pos.x,e.pos.y+.05,e.pos.z);
   v.ring.scale.setScalar(e.radius*(1+.06*pulse));v.fill.scale.setScalar(Math.max(.01,e.radius*u));
   if(warn){v.acc+=dt*q*(8+30*u);while(v.acc>=1){v.acc-=1;const a=Math.random()*Math.PI*2,r=Math.sqrt(Math.random())*e.radius;this.fire.emit(e.pos.x+Math.cos(a)*r,e.pos.y+.05,e.pos.z+Math.sin(a)*r,0,.6+u*1.6,0,.35,.06+.05*u,Math.random()<.1?'warn':'black',2,2);}}
   else{const b=clamp((time-e.burstAt)/Math.max(.05,e.until-e.burstAt)),h=Math.max(.01,5.6*Math.sqrt(sm(b*3)));v.column.visible=b<1;v.column.position.set(e.pos.x,e.pos.y,e.pos.z);v.column.scale.set(e.radius*.8*(1-.3*b),h,e.radius*.8*(1-.3*b));v.column.rotation.y=time*3;const cm=v.column.material as T.ShaderMaterial;cm.uniforms.uFade.value=1-sm((b-.55)/.45);cm.uniforms.uSpeed.value=1.6;
    if(!v.burst){v.burst=true;this.fire.burst(e.pos.x,e.pos.y+.2,e.pos.z,Math.round(60*q),2.2,.8,.16,'black',7,1);this.fire.burst(e.pos.x,e.pos.y+.2,e.pos.z,Math.round(14*q),3,.7,.05,'ember',8);}
    v.acc+=dt*q*120*(1-b);while(v.acc>=1){v.acc-=1;const a=Math.random()*Math.PI*2,r=Math.sqrt(Math.random())*e.radius*.7;this.fire.emit(e.pos.x+Math.cos(a)*r,e.pos.y+.1,e.pos.z+Math.sin(a)*r,(Math.random()-.5)*.6,8+Math.random()*6,(Math.random()-.5)*.6,.45+Math.random()*.3,.14+Math.random()*.12,Math.random()<.06?'ember':Math.random()<.2?'smoke':'black',-2,1.2);}}
  }
  for(const [id,v] of this.eruptions)if(!live.has(id)){this.group.remove(v.ring,v.fill,v.column);(v.column.material as T.Material).dispose();this.eruptions.delete(id);}
 }

 private updateRains(list:SirenRain[],time:number,dt:number){
  const q=this.quality<.5?.45:1;
  for(const r of list){if(time>r.until)continue;let acc=this.rainAcc.get(r.id)??0;const k=sm((time-r.at)/.8)*(1-sm((time-(r.until-1))/1));acc+=dt*q*55*k*(r.radius/7)**2;
   while(acc>=1){acc-=1;const a=Math.random()*Math.PI*2,d=Math.sqrt(Math.random())*r.radius*1.05;this.fire.emit(r.center.x+Math.cos(a)*d,13.2+Math.random()*1.2,r.center.z+Math.sin(a)*d,(Math.random()-.5)*.4,-.1+Math.random()*.2,(Math.random()-.5)*.4,1.6+Math.random()*1,.35+Math.random()*.35,Math.random()<.25?'smoke':'black',.05,.8);}
   this.rainAcc.set(r.id,acc);
   // The edge of the storm on the floor.
   if(Math.random()<dt*q*20){const a=Math.random()*Math.PI*2;this.fire.emit(r.center.x+Math.cos(a)*r.radius,r.center.y+.05,r.center.z+Math.sin(a)*r.radius,0,.4,0,.6,.07,'warn',.6,2);}
  }
  for(const id of [...this.rainAcc.keys()])if(!list.some(r=>r.id===id))this.rainAcc.delete(id);
 }

 private updateMarks(list:SirenMark[],state:Snapshot,time:number,camera?:T.Camera){
  const live=new Set<string>();
  for(const m of list){const target=state.players.find(p=>p.id===m.target);if(!target||time<m.lockAt)continue;live.add(m.id);let v=this.marks.get(m.id);
   if(!v){const g=new T.Group();g.name='siren-crosshair';const mat=glowMaterial(PALETTE.warn,{transparent:true,opacity:.95,side:T.DoubleSide,depthWrite:false});mat.depthTest=false;
    const ring=new T.Mesh(RING,mat),inner=new T.Mesh(new T.RingGeometry(.12,.17,4),mat);inner.rotation.z=Math.PI/4;g.add(ring,inner);const ticks:T.Mesh[]=[];
    for(let i=0;i<4;i++){const t=new T.Mesh(TICK,mat);t.scale.set(.06,.32,.01);const a=i*Math.PI/2;t.position.set(Math.sin(a)*.95,Math.cos(a)*.95,0);t.rotation.z=-a;g.add(t);ticks.push(t);}
    g.renderOrder=999;g.traverse(o=>{o.renderOrder=999;});this.group.add(g);v={group:g,ring,inner,ticks,beeps:markBeeps(m.lockAt,m.throwAt)};this.marks.set(m.id,v);}
   const thrown=time>=m.throwAt,u=clamp((time-m.lockAt)/Math.max(.1,m.throwAt-m.lockAt)),size=thrown?.85:1.6-.75*sm(u);
   let beep=0;for(const b of v.beeps)if(time>=b&&time-b<.09)beep=1-(time-b)/.09;
   v.group.position.set(target.pos.x,target.pos.y+.35,target.pos.z);if(camera){v.group.quaternion.copy(camera.quaternion);}
   v.group.scale.setScalar(size*(1+.12*beep));v.ring.rotation.z=time*(thrown?.8:2.4);
   const mat=v.ring.material as T.ShaderMaterial;mat.uniforms.uColor.value.setHex(beep>.3?0xffffff:PALETTE.warn);mat.uniforms.uOpacity.value=thrown?.75+.2*Math.sin(time*6):.85+.15*beep;
   v.ticks.forEach((t,i)=>{const a=i*Math.PI/2+(thrown?0:(1-u)*.8);t.position.set(Math.sin(a)*.98,Math.cos(a)*.98,0);t.rotation.z=-a;});
  }
  for(const [id,v] of this.marks)if(!live.has(id)){this.group.remove(v.group);this.marks.delete(id);}
 }

 /** Last Laugh hold: the victim hangs from the Siren's claw, kicks and claws at the wrist, and burns. */
 private updateGrabs(list:SirenGrab[],state:Snapshot,rigs:ReadonlyMap<string,CharacterRig>,time:number,dt:number){
  const q=this.quality<.5?.5:1;
  // Release the char on anyone no longer held (alive): the hold broke, they keep their own look.
  for(const [id,c] of this.charred)if(!list.some(g=>g.victim===id)){for(const [m,mat] of c.saved)m.material=mat;for(const k of c.copies)k.m.dispose();this.charred.delete(id);}
  for(const g of list){const siren=rigs.get(g.owner),victim=rigs.get(g.victim),vp=state.players.find(p=>p.id===g.victim);if(!siren||!victim||!vp||vp.hp<=0||time>=g.crushAt)continue;
   // The throat sits at the claw tips, at arm's length: the victim hangs clear of the Siren's body.
   siren.group.updateMatrixWorld(true);v3.set(0,-.36,.16);siren.rightHand.localToWorld(v3);
   const age=time-g.at,lift=sm(age/SIREN_TIMING.laugh.lift),struggle=sm((age-.15)/.4)*(1-sm((time-(g.crushAt-.3))/.3)*.6),burn=sm((age-SIREN_TIMING.laugh.head)/1.6);
   victim.group.position.set(v3.x,v3.y-.44-.05*(1-lift),v3.z);victim.group.rotation.set(0,siren.group.rotation.y+Math.PI,0);
   // Struggle: hands at the wrist, legs kicking out of phase, head thrown back then pinned.
   const k=time*9;victim.body.position.set(0,0,0);victim.body.rotation.set(.12*Math.sin(k*.7)*struggle,0,.1*Math.sin(k*.5)*struggle);
   victim.spine.rotation.set(-.15+.08*Math.sin(k)*struggle,0,0);victim.chest.rotation.set(-.1,0,.06*Math.sin(k*1.3)*struggle);victim.neck.rotation.set(-.25,0,0);victim.head.rotation.set(-.35+.2*burn,.25*Math.sin(k*.8)*struggle*(1-burn),0);
   victim.leftArm.rotation.set(-2.5+.25*Math.sin(k*1.7)*struggle,0,.25);victim.rightArm.rotation.set(-2.4+.25*Math.sin(k*1.9+1)*struggle,0,-.25);victim.leftElbow.rotation.set(-1.6+.3*Math.sin(k*2.1)*struggle,0,0);victim.rightElbow.rotation.set(-1.7+.3*Math.sin(k*2.3)*struggle,0,0);
   victim.leftLeg.rotation.set(-.35+.75*Math.sin(k*1.1)*struggle,0,.08);victim.rightLeg.rotation.set(-.35+.75*Math.sin(k*1.1+2.2)*struggle,0,-.08);victim.leftKnee.rotation.set(.5+.5*Math.max(0,Math.sin(k*1.1+1))*struggle,0,0);victim.rightKnee.rotation.set(.5+.5*Math.max(0,Math.sin(k*1.1+3))*struggle,0,0);
   // Black flame climbs over the victim from the Siren's grip.
   let acc=(this.grabFx.get(g.victim)??0)+dt*q*(10+90*burn);victim.group.updateMatrixWorld(true);
   const joints=[victim.head,victim.chest,victim.spine,victim.leftArm,victim.rightArm,victim.leftElbow,victim.rightElbow,victim.leftLeg,victim.rightLeg,victim.leftKnee,victim.rightKnee];
   while(acc>=1){acc-=1;const j=joints[Math.floor(Math.random()*(burn>.3?joints.length:3))];v4.set((Math.random()-.5)*.2,(Math.random()-.5)*.2,(Math.random()-.5)*.2);j.localToWorld(v4);
    this.fire.emit(v4.x,v4.y,v4.z,(Math.random()-.5)*.3,1.2+Math.random()*.8,(Math.random()-.5)*.3,.45+Math.random()*.35,.07+Math.random()*.06,Math.random()<.05?'ember':'black',2.6,2);}
   this.grabFx.set(g.victim,acc);
   // The burn chars the victim: private copies of their materials darken toward ink.
   let c=this.charred.get(g.victim);if(!c||c.rig!==victim){c={rig:victim,saved:new Map(),copies:[]};const copies=new Map<T.Material,T.MeshLambertMaterial>();
    victim.group.traverse(o=>{if(!(o instanceof T.Mesh)||Array.isArray(o.material)||!(o.material instanceof T.MeshLambertMaterial))return;let k=copies.get(o.material);if(!k){k=o.material.clone();copies.set(o.material,k);c!.copies.push({m:k,color:o.material.color.clone(),emissive:o.material.emissive.clone()});}c!.saved.set(o,o.material);o.material=k;});this.charred.set(g.victim,c);}
   for(const k of c.copies){k.m.color.copy(k.color).lerp(INK_COLOR,.85*burn);k.m.emissive.copy(k.emissive).multiplyScalar(1-.8*burn);}
  }
 }

 /** Laugh / mark windups: soot rings pulse off the floor with each burst of laughter. */
 private updateAuras(state:Snapshot,rigs:ReadonlyMap<string,CharacterRig>,time:number){
  for(const p of state.players as PlayerView[]){const s=sirenOf(p);if(!s)continue;const rig=rigs.get(p.id);this.lastYaw.set(p.id,rig?rig.group.rotation.y:p.yaw);
   // The Siren never wears the armor glow (the renderer should exclude it; this catches a halo that slipped through).
   rig?.group.traverse(o=>{if(o instanceof T.Sprite&&o.name==='armor-radiance')o.visible=false;});this.lastPos.set(p.id,{...p.pos});const m=s.move;if(!m||p.hp<=0)continue;
   if(m.id==='laugh'&&time>=m.at&&time<m.hitAt){const last=this.auraAt.get(p.id)??-1;if(time-last>.32){this.auraAt.set(p.id,time);this.shock({x:p.pos.x,y:p.pos.y-.83,z:p.pos.z},1.9,.5,true);}}
  }
 }
}
export const SIREN_FX_DEATHS=SIREN.deaths;
