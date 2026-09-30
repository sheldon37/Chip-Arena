import * as T from 'three';
import type {PlayerView} from '../shared/simulation.js';
import {WEAPONS} from '../shared/content.js';
import type {CharacterRig} from './character-rig.js';
import type {attackPose} from './character-animation.js';
import type {ProjectileTrails} from './projectile-trails.js';
/** Melee sweep trails. The renderer constructs one, calls sample() right after animateCharacter for every player and
 * update() once per frame.
 *
 * Swept blade (when the renderer passes `enabled` to update(), i.e. the projectile-trails setting): a two-edge ribbon
 * between a point ~35% along the blade and the blade tip, so the whole edge sweeps a surface instead of the tip drawing a
 * thin line (bright at the edge, fading toward the blade's middle and with age). Each frame inserts sub-frame samples by turning the blade direction about the grip (and moving the grip)
 * between the last two frames, so fast arcs stay curved at any frame rate. Sampling starts in the last 12% of the windup
 * and runs ~70 ms into the follow-through; the ribbon fades out per weapon (daggers .12 s, sword .16 s, greatsword .22 s).
 * effectsQuality below 1 drops the sub-samples and shortens the fade. Meshes are pooled and bounded (at most 16 live
 * sweeps of 48 samples), additive and unlit (no shadows, SVG-compatible Basic material), and disposed with the renderer.
 *
 * Without `enabled` (a renderer that does not pass the setting yet) it falls back to the previous tip ribbon through the
 * shared ProjectileTrails, which the renderer already hides when trails are off. */
const MAX_SWEEPS=16,MAX_SAMPLES=48,BASE=.35,LIFE:Record<string,number>={daggers:.12,sword:.16,greatsword:.22,shield:.16};
type Sample={g:T.Vector3;d:T.Vector3;len:number;at:number};
type Sweep={mesh:T.Mesh;samples:Sample[];seen:number;life:number};
const _g=new T.Vector3(),_t=new T.Vector3(),_d=new T.Vector3(),_q=new T.Quaternion(),_s=new T.Quaternion(),_c=new T.Color(),IDENTITY=new T.Quaternion();
/** The blade's grip and tip in world space (the off-hand blade on even dagger strikes), or undefined without a blade tip. */
function blade(r:CharacterRig,p:PlayerView){
 const weapon=p.spec==='daggers'&&(p.attackIndex||1)%2===0?r.offWeapon:r.weapon,tip=weapon.getObjectByName('blade-tip');if(!tip||!weapon.visible)return undefined;
 r.group.updateMatrixWorld(true);weapon.getWorldPosition(_g);tip.getWorldPosition(_t);return {grip:_g,tip:_t};
}
export class MeleeTrails{
 private sweeps=new Map<string,Sweep>();private pool:T.Mesh[]=[];private root?:T.Object3D;private enabled?:boolean;private quality=1;
 constructor(readonly trails:ProjectileTrails){}
 /** Whether this player's weapon is sweeping now: the end of the windup through the follow-through, or a launching special. */
 static sweeping(p:PlayerView,pose:ReturnType<typeof attackPose>){
  return p.hp>0&&WEAPONS[p.spec].class==='melee'&&(pose.active&&pose.age>=pose.windup*.88&&pose.age<=pose.windup+pose.window+.07||!!p.meleeAction?.released&&['sword-launch','wolf-spin'].includes(p.meleeAction.kind));
 }
 sample(r:CharacterRig,p:PlayerView,pose:ReturnType<typeof attackPose>,visualTime:number){
  const color=p.chips.ember?0xff953e:p.chips.poison?0x88ed80:p.chips['echo-strike']?0xc1a1ff:0xc9fff3;
  if(this.enabled===undefined){
   const w=WEAPONS[p.spec];if(p.hp>0&&w.class==='melee'&&(pose.active&&pose.strike>0&&pose.strike<1||p.meleeAction?.released&&['sword-launch','wolf-spin'].includes(p.meleeAction.kind))){const b=blade(r,p);if(b)this.trails.sample('sweep-'+p.id+'-'+p.lastAttack,b.tip,color,visualTime,6);}
   return;
  }
  if(!this.enabled||!MeleeTrails.sweeping(p,pose))return;
  const b=blade(r,p);if(!b)return;
  if(!this.root){let root:T.Object3D=r.group;while(root.parent)root=root.parent;if(root===r.group)return;this.root=root;}
  const key=p.id+':'+(p.meleeAction?.released?p.meleeAction.kind+p.meleeAction.at:p.lastAttack+':'+p.attackIndex);
  let s=this.sweeps.get(key);
  if(!s){if(this.sweeps.size>=MAX_SWEEPS)return;s={mesh:this.acquire(color),samples:[],seen:visualTime,life:(LIFE[p.spec]??.16)*(this.quality<1?.7:1)};this.sweeps.set(key,s);}
  (s.mesh.material as T.MeshBasicMaterial).color.setHex(color);
  const len=b.tip.distanceTo(b.grip),dir=_d.copy(b.tip).sub(b.grip).divideScalar(Math.max(1e-6,len)),last=s.samples.at(-1);
  // Sub-frame samples: the blade turns about the grip between frames, so fast arcs stay curved.
  if(last&&visualTime>last.at&&visualTime-last.at<.1){
   const steps=this.quality>=1?3:1;_q.setFromUnitVectors(last.d,dir);
   for(let i=1;i<=steps;i++){const f=i/(steps+1);_s.slerpQuaternions(IDENTITY,_q,f);s.samples.push({g:last.g.clone().lerp(b.grip,f),d:last.d.clone().applyQuaternion(_s),len:last.len+(len-last.len)*f,at:last.at+(visualTime-last.at)*f});}
  }
  if(!last||visualTime>last.at)s.samples.push({g:b.grip.clone(),d:dir.clone(),len,at:visualTime});
  if(s.samples.length>MAX_SAMPLES)s.samples.splice(0,s.samples.length-MAX_SAMPLES);s.seen=visualTime;
 }
 /** `enabled` is the renderer's trail setting and `quality` its effectsQuality; without them the legacy tip ribbon runs. */
 update(dt:number,time:number,enabled?:boolean,quality=1){
  void dt;if(enabled!==undefined){this.enabled=enabled;this.quality=quality;if(!enabled){this.clear();return;}}
  for(const [key,s] of this.sweeps){
   while(s.samples.length&&time-s.samples[0].at>s.life)s.samples.shift();
   if(s.samples.length<2){if(time-s.seen>s.life||time<s.seen-.1){this.release(s.mesh);this.sweeps.delete(key);}else s.mesh.geometry.setDrawRange(0,0);continue;}
   this.build(s,time);
  }
 }
 /** Live sweeps (tests and diagnostics). */
 get size(){return this.sweeps.size;}
 /** The ribbon meshes currently in the scene. */
 meshes(){return [...this.sweeps.values()].map(s=>s.mesh);}
 clear(){for(const s of this.sweeps.values())this.release(s.mesh);this.sweeps.clear();}
 dispose(){this.clear();for(const m of this.pool){m.geometry.dispose();(m.material as T.Material).dispose();}this.pool=[];this.root=undefined;}
 private acquire(color:number){
  let mesh=this.pool.pop();
  if(!mesh){
   const geometry=new T.BufferGeometry(),positions=new Float32Array(MAX_SAMPLES*2*3),colors=new Float32Array(MAX_SAMPLES*2*3),index:number[]=[];
   for(let i=0;i<MAX_SAMPLES-1;i++){const a=i*2;index.push(a,a+1,a+2,a+2,a+1,a+3);}
   geometry.setAttribute('position',new T.BufferAttribute(positions,3).setUsage(T.DynamicDrawUsage));geometry.setAttribute('color',new T.BufferAttribute(colors,3).setUsage(T.DynamicDrawUsage));geometry.setIndex(index);geometry.setDrawRange(0,0);
   mesh=new T.Mesh(geometry,new T.MeshBasicMaterial({color,vertexColors:true,transparent:true,opacity:.7,depthWrite:false,side:T.DoubleSide,blending:T.AdditiveBlending,toneMapped:false}));mesh.name='melee-sweep';mesh.frustumCulled=false;mesh.castShadow=mesh.receiveShadow=false;
  }
  this.root?.add(mesh);return mesh;
 }
 private release(mesh:T.Mesh){mesh.removeFromParent();mesh.geometry.setDrawRange(0,0);if(this.pool.length<MAX_SWEEPS)this.pool.push(mesh);else{mesh.geometry.dispose();(mesh.material as T.Material).dispose();}}
 private build(s:Sweep,time:number){
  const position=s.mesh.geometry.getAttribute('position') as T.BufferAttribute,color=s.mesh.geometry.getAttribute('color') as T.BufferAttribute,n=s.samples.length;
  for(let i=0;i<n;i++){
   const k=s.samples[i],fade=Math.max(0,1-(time-k.at)/s.life),glow=fade*fade*(.2+.8*(i+1)/n);
   position.setXYZ(i*2,k.g.x+k.d.x*k.len*BASE,k.g.y+k.d.y*k.len*BASE,k.g.z+k.d.z*k.len*BASE);position.setXYZ(i*2+1,k.g.x+k.d.x*k.len,k.g.y+k.d.y*k.len,k.g.z+k.d.z*k.len);
   _c.setScalar(glow*.04);color.setXYZ(i*2,_c.r,_c.g,_c.b);_c.setScalar(glow*.62);color.setXYZ(i*2+1,_c.r,_c.g,_c.b);
  }
  position.needsUpdate=true;color.needsUpdate=true;s.mesh.geometry.setDrawRange(0,Math.max(0,n-1)*6);
 }
}
