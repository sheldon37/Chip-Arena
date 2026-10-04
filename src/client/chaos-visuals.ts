import * as T from 'three';
import type {Snapshot} from '../shared/simulation.js';
import type {FieldView} from '../shared/chaos.js';
import {ABILITY_FX} from '../shared/abilities.js';
import {sameTeam} from '../shared/teams.js';
import type {CharacterRig} from './character-rig.js';
import type {MinionView} from '../shared/chaos.js';
import {buildSummon,poseSummon,poseSummonDeath,SUMMON_LOOK} from './summon-visuals.js';
import {SUMMON} from '../shared/chaos.js';
/** Renderer hooks for summon VFX (pooled sparks and tracer lines); optional so tools and tests can omit them. */
export interface SummonFx {burst(pos:{x:number;y:number;z:number},color:number,count:number,power:number):void;line(a:{x:number;y:number;z:number},b:{x:number;y:number;z:number},color0:number,color1:number):void;}
const COLORS:Record<string,number>={'black-hole':0xab67ff,acid:0xb8f53c,fire:0xff672d,frost:0x8fdfff,time:0x83aeff,repulsion:0xff728f,heal:0x79ffa9,storm:0xddadff,bomb:0xffa141,implosion:0xb068ff,mine:0xff515d};
/** Ability field telegraphs (§11.2): enemy warm red, own and allied cool cyan. */
export const TELEGRAPH={enemy:0xff5a46,ally:0x55dcff} as const;
/** Spacing of the voxel dashes on a field's gameplay radius (§19.2). */
export const RIM_SPACING=.42;
const box=new T.BoxGeometry(1,1,1),dome=new T.SphereGeometry(1,14,6,0,Math.PI*2,0,Math.PI/2),mats=new Map<string,T.Material>(),dummy=new T.Object3D();
/** Soft ground glow under a field: vertex alpha falls from the centre to exactly 0 at the radius, so the area reads (and
 * keeps the enemy/ally telegraph tint) without an outlined edge. One shared unit disc, scaled per field. */
const glowDisc=(()=>{const g=new T.CircleGeometry(1,40),n=g.attributes.position.count,c=new Float32Array(n*4);for(let i=0;i<n;i++){const r=Math.hypot(g.attributes.position.getX(i),g.attributes.position.getY(i));c.set([1,1,1,r<.01?1:0],i*4);}g.setAttribute('color',new T.BufferAttribute(c,4));return g;})(),glowMats=new Map<number,T.MeshBasicMaterial>();
export const FIELD_GLOW_ALPHA=.22;
function fieldGlow(radius:number,color:number){let m=glowMats.get(color);if(!m)glowMats.set(color,m=new T.MeshBasicMaterial({color,vertexColors:true,transparent:true,opacity:FIELD_GLOW_ALPHA,depthWrite:false,toneMapped:false}));const o=new T.Mesh(glowDisc,m);o.name='field-glow';o.rotation.x=-Math.PI/2;o.position.y=.03;o.scale.setScalar(radius);o.renderOrder=-1;return o;}
function mat(color:number,opacity=1,line=false){const key=color+':'+opacity+line;let m=mats.get(key);if(!m)mats.set(key,m=line?new T.LineBasicMaterial({color,transparent:true,opacity}):new T.MeshBasicMaterial({color,transparent:opacity<1,opacity,depthWrite:opacity>=1}));return m;}
function cube(g:T.Object3D,color:number,s:number,y=0,opacity=1){const m=new T.Mesh(box,mat(color,opacity));m.scale.setScalar(s);m.position.y=y;g.add(m);return m;}
export function telegraphColor(f:FieldView,s:Snapshot,me?:string){
 if(!f.fx||!ABILITY_FX.has(f.fx))return f.fx==='thunderstep'?0x9fd2ff:f.fx==='comet'?0xffc04a:COLORS[f.kind];
 if(me===undefined)return COLORS[f.kind];const owner=s.players.find(p=>p.id===f.owner),self=s.players.find(p=>p.id===me);return f.owner===me||s.pve||sameTeam(owner,self,s.options?.gameMode)?TELEGRAPH.ally:TELEGRAPH.enemy;
}
/** Voxel rim: dashes on the exact gameplay radius, built once per field from shared geometry. */
export function rim(radius:number,color:number){const n=Math.max(12,Math.min(96,Math.round(Math.PI*2*radius/RIM_SPACING))),m=new T.InstancedMesh(box,mat(color,.85),n);m.name='field-rim';for(let i=0;i<n;i++){const a=i/n*Math.PI*2;dummy.position.set(Math.sin(a)*radius,.04,Math.cos(a)*radius);dummy.rotation.set(0,a+Math.PI/2,0);dummy.scale.set(.07,.07,RIM_SPACING*.55);dummy.updateMatrix();m.setMatrixAt(i,dummy.matrix);}return m;}
export class ChaosVisuals {
 fields=new Map<string,T.Group>();minions=new Map<string,CharacterRig>();
 /** Killed summons keep their rig for the corpse pose and break-up (SUMMON_LOOK.death), keyed by minion id. */
 private dead=new Map<string,{rig:CharacterRig;kind:'zombie'|'toy';at:number;x:number;y:number;z:number;broke:boolean}>();
 private hits=new Map<string,number>();private views=new Map<string,MinionView>();private lastEvent=0;private generation=-1;private spot={x:0,y:0,z:0};private tip=new T.Vector3();private living=new Set<string>();
 constructor(readonly scene:T.Scene){}
 /** `me` (the local pilot) picks the enemy/ally tint of ability telegraphs; without it they keep the field colour. */
 update(s:Snapshot,me?:string,fx?:SummonFx){const live=new Set((s.fields||[]).map(f=>f.id));for(const [id,g]of this.fields)if(!live.has(id)){this.scene.remove(g);g.traverse(o=>{if(o instanceof T.InstancedMesh)o.dispose();});this.fields.delete(id);}
 for(const f of s.fields||[]){let g=this.fields.get(f.id);if(!g){g=new T.Group();const color=telegraphColor(f,s,me),ability=!!f.fx&&ABILITY_FX.has(f.fx);
 // Owner request 2026-10-04: areas are no longer outlined (no voxel rim on the radius, no dome wireframe); the field's own
 // volume (ChipBlasts motes, flames, smoke, the translucent dome, the cores) and a soft ground glow that fades to nothing at the radius show where it is. rim() stays for tools.
 g.add(fieldGlow(f.radius,color));

 // Voxel cores: black hole, chip bombs and mines, and the Reverse Grenade's spinning gravity bomb.
 if(f.kind==='black-hole'){cube(g,0x05020c,.62,.7).userData.core=1;cube(g,0xf3e2ff,.3,.7);for(let n=0;n<10;n++)cube(g,color,.13,.7).userData.disc=n;}
 else if(!ability&&['bomb','mine','implosion'].includes(f.kind)&&!['artillery','thunderstep','comet'].includes(f.fx||'')){const mine=f.kind==='mine',body=cube(g,f.kind==='implosion'?0x2a0f46:0x3a3434,mine?.5:.38,mine?.1:.4);if(mine)body.scale.y=.16;body.userData.body=1;cube(g,0xffffff,.1,mine?.22:.66).userData.fuse=1;if(f.kind==='implosion')for(let n=0;n<6;n++)cube(g,color,.12,.4).userData.orbit=n;}
 else if(f.kind==='time'||f.kind==='repulsion'){// A faceted dome at exactly the gameplay radius: the bubble reads as a volume, not a decal.
 const d=new T.Mesh(dome,mat(color,.13));d.name='field-dome';d.scale.setScalar(f.radius);g.add(d);}
 this.fields.set(f.id,g);this.scene.add(g);}
 g.position.set(f.pos.x,Math.max(.07,f.pos.y-.7),f.pos.z);const age=s.time-f.born,left=f.until-s.time,u=Math.min(1,Math.max(0,age/Math.max(.01,f.until-f.born)));
 for(const o of g.children){const d=o.userData;if(d.disc!==undefined){const a=d.disc*Math.PI/5+age*2.4,r=.95+(d.disc%2)*.3;o.position.set(Math.cos(a)*r,.7+Math.sin(a*2)*.08,Math.sin(a)*r);o.rotation.set(age*3,a,0);}
  else if(d.orbit!==undefined){const a=d.orbit*Math.PI/3+age*(6+u*10),r=.75-u*.5;o.position.set(Math.cos(a)*r,.4+Math.sin(a+d.orbit)*.18,Math.sin(a)*r);o.rotation.set(age*7,a,0);}
  else if(d.body){o.rotation.set(f.kind==='implosion'?age*2.5:0,age*2,0);if(f.kind!=='mine')o.scale.setScalar(.38*(1+Math.sin(age*(8+u*16))*.12));}
  else if(d.fuse)o.visible=Math.sin(age*(6+u*20))>-.2&&!(left<.5&&Math.sin(left*60)>.3);
  else if(d.core)o.rotation.set(age*.8,age*1.3,0);}
 }
 this.summons(s,fx);
 }
 /** Summon rigs: events (hit flinch, shot tracer, death), live poses and corpses. */
 private summons(s:Snapshot,fx?:SummonFx){
  if(this.generation!==(s.generation||0)){this.generation=s.generation||0;for(const d of this.dead.values())this.scene.remove(d.rig.group);this.dead.clear();this.hits.clear();}
  for(const e of s.events){if(e.id<=this.lastEvent)continue;this.lastEvent=e.id;if(!e.target||s.time-e.time>.5)continue;
   if(e.type==='minionHit'&&this.minions.has(e.target)){this.hits.set(e.target,e.time);const kind=this.views.get(e.target)?.kind;fx?.burst(e.pos,kind==='toy'?0xe8c25a:0x4d6b3c,3,1.6);}
   else if(e.type==='minionShot'&&e.from){const rig=this.minions.get(e.target),muzzle=rig?.weapon.getObjectByName('muzzle');if(muzzle){muzzle.getWorldPosition(this.tip);this.spot.x=this.tip.x;this.spot.y=this.tip.y;this.spot.z=this.tip.z;}fx?.line(muzzle?this.spot:e.from,e.pos,0xfff2b8,0xffa040);fx?.burst(muzzle?this.spot:e.from,0xffd27a,4,1.2);fx?.burst(e.pos,0xd8c8a0,3,1.4);}
   else if(e.type==='minionDeath'&&(e.message==='zombie'||e.message==='toy')){const rig=this.minions.get(e.target);if(rig){this.minions.delete(e.target);this.dead.set(e.target,{rig,kind:e.message,at:e.time,x:rig.group.position.x,y:rig.group.position.y,z:rig.group.position.z,broke:false});}}}
  const living=this.living;living.clear();for(const m of s.minions||[])living.add(m.id);
  for(const[id,r]of this.minions)if(!living.has(id)){this.scene.remove(r.group);this.minions.delete(id);this.views.delete(id);this.hits.delete(id);}
  for(const m of s.minions||[]){if(m.kind!=='zombie'&&m.kind!=='toy')continue;let r=this.minions.get(m.id);if(!r){r=buildSummon(m.kind);this.scene.add(r.group);this.minions.set(m.id,r);}this.views.set(m.id,m);
   r.group.position.set(m.pos.x,m.pos.y,m.pos.z);r.group.rotation.y=m.yaw;poseSummon(r,m,s.time,s.time-(this.hits.get(m.id)??-Infinity));}
  for(const[id,d]of this.dead){const age=s.time-d.at;if(age>=SUMMON_LOOK.death.gone||age<-.5){this.scene.remove(d.rig.group);this.dead.delete(id);continue;}
   d.rig.group.position.set(d.x,d.y,d.z);poseSummonDeath(d.rig,d.kind,Math.max(0,age));
   if(!d.broke&&age>=SUMMON_LOOK.death.breakAt){d.broke=true;d.rig.group.visible=false;const look=SUMMON_LOOK[d.kind];for(let i=0;i<3;i++){this.spot.x=d.x;this.spot.y=d.y-.3+i*.25*look.scale;this.spot.z=d.z;fx?.burst(this.spot,look.gib[i],d.kind==='toy'?6:8,2.6);}}}
 }
 /** Strike frame of a summon's current attack (presentation hooks and tests). */
 static strikeAt(m:MinionView){return m.lastAttack+(m.kind==='toy'?SUMMON.toy.windup:SUMMON.zombie.windup);}
}
