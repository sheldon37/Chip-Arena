// Event VFX (design §3.4b): the renderer's per-event body, the authoritative blast and field styles, bursts, beams and
// short-lived particles, moved verbatim from renderer.ts by FOUNDATION; owned by the CHIP VFX SWEEP (V) afterwards. V edits
// only the chip, ability and legacy-E branches; the stage, `leet-*`, Siren and DEV branches stay byte-identical.
// The renderer keeps the event loop header, `lastEvent`, recordBlockHit, the delegations and the hit/shake flags.
// V sweep (§19.2): chip and ability events draw pooled ChipBlasts voxels only; legacy E push/pull visuals are gone.
import * as T from 'three';
import {BurstPool} from './burst-pool.js';
import {ChipBlasts,blastStyle,blastTint} from './chip-blasts.js';
import type {Vec} from '../shared/config.js';
import type {Snapshot,GameEvent} from '../shared/simulation.js';
const tv=(v:Vec)=>new T.Vector3(v.x,v.y,v.z);
const boxGeometry=new T.BoxGeometry(1,1,1);
const materials=new Map<string,T.MeshLambertMaterial>();
function material(color:T.ColorRepresentation,emissive=false){const key=String(color)+emissive;if(!materials.has(key))materials.set(key,new T.MeshLambertMaterial({color,emissive:emissive?color:0,emissiveIntensity:emissive?.7:0}));return materials.get(key)!;}
interface Particle{mesh:T.Mesh;velocity:T.Vector3;life:number;max:number;}
/** Renderer options read on every call (graphics settings change at runtime). */
export interface VfxOptions{quality:number;compat:boolean;trails:boolean}
export class EventVfx{
 readonly chipBlasts:ChipBlasts;readonly bursts:BurstPool;particles:Particle[]=[];
 constructor(readonly scene:T.Scene,private options:()=>VfxOptions){this.chipBlasts=new ChipBlasts(scene);this.bursts=new BurstPool(scene);}
 /** Volumetric chip blasts keyed to authoritative events; the event radius is the gameplay radius. */
 blastEvent(e:GameEvent){const {quality,compat}=this.options(),o={quality,compat};if(e.type==='explosion')this.chipBlasts.blast(e.message?blastStyle(e.message):'fire',e.pos,e.radius||2,e.message?{...o,quality:o.quality*(e.message==='flame-lance'?.5:1),tint:blastTint(e.message)}:{...o,quality:o.quality*.55});else if(e.type==='blockPulse')this.chipBlasts.blast(e.mods?.supernova?'nova':'kinetic',e.pos,e.radius||3,o);else if(e.type==='vacuum')this.chipBlasts.blast('void',e.pos,e.radius||5,o);else if(e.type==='arc'&&!e.from&&e.radius)this.chipBlasts.blast('shock',e.pos,e.radius,{...o,quality:o.quality*.6});else if(e.type==='freeze')this.chipBlasts.blast('frost',e.pos,e.radius||1,{...o,quality:o.quality*.7});else if(e.type==='nova'&&e.message==='panic-button')this.chipBlasts.blast('kinetic',e.pos,e.radius||6,o);else if(e.type==='teleport')this.chipBlasts.blast('void',e.pos,1.2,{...o,quality:o.quality*.5});}
 fields(state:Snapshot){const {quality,compat}=this.options();const live=new Set<string>();for(const f of state.fields||[]){live.add(f.id);this.chipBlasts.field(f,state.time,quality,compat);}this.chipBlasts.prune(live);}
 burst(pos:Vec,color:number,count=8,power=3){this.bursts.spawn(pos,color,count,power,this.options().quality);}
 beam(a:Vec,b:Vec,color:number){if(this.particles.length>=180*this.options().quality)return;const start=tv(a),end=tv(b),d=end.clone().sub(start);const mesh=new T.Mesh(boxGeometry,material(color,true));mesh.position.copy(start.add(end).multiplyScalar(.5));mesh.scale.set(.022,.022,d.length());mesh.quaternion.setFromUnitVectors(new T.Vector3(0,0,1),d.normalize());this.scene.add(mesh);this.particles.push({mesh,velocity:new T.Vector3(),life:.09,max:.09});}
 /** Everything the renderer drew per event after its delegations. Returns the local-pilot flags (hit marker, camera shake). */
 event(event:GameEvent,state:Snapshot,me:string):{hit?:boolean;shake?:boolean}{const out:{hit?:boolean;shake?:boolean}={},o=this.options(),b=this.chipBlasts,c=event.mods||{},t=event.type,q={quality:o.quality,compat:o.compat},hit=(color:number,radius:number,ground=false)=>b.impact(event.pos,radius,color,{...q,ground});if(event.type.startsWith('leet-')){this.burst(event.pos,0x55ff88,event.type==='leet-nuke'?70:24,event.type==='leet-nuke'?22:5);if(event.from)this.beam(event.pos,event.from,0x55ff88);}if(event.type==='stage-saw-hit'){this.burst(event.pos,0xffcb62,32,6);this.burst(event.pos,0xe9f6ff,14,4);for(let n=0;n<7;n++){const a=n*Math.PI*2/7;this.beam(event.pos,{x:event.pos.x+Math.cos(a)*1.1,y:event.pos.y+Math.sin(a)*1.1,z:event.pos.z+.12},0xffe5ad);}}if(event.type==='stage-spikes-rise')this.burst(event.pos,0xb2a89b,18,3);if(event.type==='stage-lava-hit')this.burst(event.pos,0xff9c22,8,2);
  if(t==='teleport'&&event.from){b.impact(event.from,.9,0xb66cff,q);b.line(event.from,event.pos,0xffffff,0xb66cff,o.quality,.3,.22,.08);}if(t==='summon')hit(0x93db67,1.2,true);if(event.type==='hit'&&event.source===me)out.hit=true;if(o.trails&&event.type==='impact'&&event.from)this.beam(event.from,event.pos,event.color||0xffedb8);
  if(['impact','break','hit','debrisHit','parry','blockHit','reflect','blockAbsorb','collapse'].includes(t)&&!(t==='hit'&&['burn','poison'].includes(event.message||''))){this.burst(event.pos,event.color||0xfbc98b,t==='break'?18:6,3);if(event.target===me)out.shake=true;}
  if(t==='impact'&&state.players.find(p=>p.id===event.source)?.spec==='water')hit(0x8de9ee,.6);if(t==='impact'||t==='slash'){if(c.kinetic)hit(0xffaa64,.65+Math.min(3,c.kinetic)*.25);if(c.demolition)hit(0xffd45f,.5+.12*c.demolition);}
  if(t==='critical')hit(0xffd66c,1.5);if(t==='catch')hit(0x7fffe0,.9);if(t==='split')hit(0x96ffb7,1.1);if(t==='meleeRebound')hit(0x91f7eb,event.radius||1.5);if(t==='slowed')hit(event.message==='shackles'?0xa04dff:0x8de9ee,.8);
  if(t==='ricochet'){if(event.message==='rally')this.rally(event,state);else hit(c.pong?0xff5269:0xffcc49,c.pong?1.2:.8);}
  if(t==='arc'){if(event.from)b.line(event.pos,event.from,0xffffff,event.color||0xc4b4ff,o.quality);else if(!event.radius)for(let i=0;i<4;i++){const a=i*1.57+.4;b.line(event.pos,{x:event.pos.x+Math.cos(a),y:event.pos.y+.5,z:event.pos.z+Math.sin(a)},0xffffff,0xc4b4ff,o.quality);}}
  // The legacy lightning E's nova keeps its burst until INTEGRATION removes that E.
  if(t==='nova'&&event.message!=='panic-button')this.burst(event.pos,event.color||0x8bf0ff,28,8);return out;}
 /** Pong rally (contract from pong.ts): a voxel paddle flash at the bounce point and a 0.15 s streak toward the rally target. */
 private rally(e:GameEvent,state:Snapshot){const from=e.from||e.pos,to=state.players.find(p=>p.id===e.target)?.pos,d=to?{x:to.x-e.pos.x,y:to.y+.3-e.pos.y,z:to.z-e.pos.z}:{x:e.pos.x-from.x,y:e.pos.y-from.y,z:e.pos.z-from.z},l=Math.hypot(d.x,d.y,d.z)||1,k=Math.min(6,l)/l,q=this.options().quality;
  this.chipBlasts.paddle(from,d,0xff5269,q);this.chipBlasts.line(e.pos,{x:e.pos.x+d.x*k,y:e.pos.y+d.y*k,z:e.pos.z+d.z*k},0xffffff,0xff5269,q,.05,.15,.12);}
 /** Pools and particles; call once per frame after the events. */
 update(dt:number){this.chipBlasts.update(dt);this.bursts.update(dt);for(let i=this.particles.length-1;i>=0;i--){const p=this.particles[i];p.life-=dt;if(p.life<=0){this.scene.remove(p.mesh);if(p.mesh.geometry!==boxGeometry)p.mesh.geometry.dispose();this.particles.splice(i,1);}else{p.mesh.position.addScaledVector(p.velocity,dt);p.velocity.y-=dt*5;p.mesh.scale.multiplyScalar(Math.max(.93,1-dt*1.2));}}}
 clear(){this.bursts.clear();this.chipBlasts.clear();for(const p of this.particles){p.mesh.removeFromParent();if(p.mesh.geometry!==boxGeometry)p.mesh.geometry.dispose();}this.particles=[];}
}
