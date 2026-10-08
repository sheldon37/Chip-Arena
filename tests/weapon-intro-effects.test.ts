import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {initPhysics} from '../src/shared/simulation.js';
import {WEAPON_INTROS,type WeaponIntro} from '../src/shared/weapon-intros.js';
import {introDuration,introNameAt} from '../src/shared/intros.js';
import {INTRO_HIT} from '../src/client/intro-choreography.js';
import {WeaponIntroEffects,INTRO_FX_STEP} from '../src/client/weapon-intro-effects.js';
import {ChipBlasts} from '../src/client/chip-blasts.js';
import {probeRig} from './helpers/motion-probe.js';
await initPhysics();
// Owner request 2026-10-04: weapon intros modernised onto the pooled voxel family (ChipBlasts) with the same timing.
const styles=Object.keys(WEAPON_INTROS) as WeaponIntro[];
const fingerprint=(b:ChipBlasts)=>{const out:number[]=[b.solid.count,b.glow.count];for(const pool of [b.solid,b.glow])for(let i=0;i<pool.count;i++){const p=pool.at(i);out.push(p.x,p.y,p.z,p.age);}return out;};
const play=(style:WeaponIntro,ages:number[],quality=1,compat=false)=>{const r=probeRig(WEAPON_INTROS[style].spec),fx=new WeaponIntroEffects(style,r,undefined);for(const a of ages)fx.update(a,r,quality,compat);return {fx,r};};

test('weapon intro timing marks are unchanged (hits, name card, duration)',()=>{
 assert.deepEqual(Object.fromEntries(styles.map(s=>[s,WEAPON_INTROS[s].hit])),{'weapon-sword':1.8,'weapon-daggers':2.12,'weapon-greatsword':1.8,'weapon-shield':1.45,'weapon-pistol':2.12,'weapon-rifle':1.8,'weapon-machinegun':2.12,'weapon-fire':1.8,'weapon-water':2.12,'weapon-lightning':1.45,'weapon-darkness':2.12});
 for(const s of styles){assert.equal(introDuration(s),4.8);assert.equal(introNameAt(s),3.2);assert.equal(INTRO_HIT[s],WEAPON_INTROS[s].hit);}
});

test('intro effects contain no flat ring/disc/line geometry: only the pooled voxel meshes (and dagger afterimages)',()=>{
 for(const s of styles){const {fx}=play(s,[0,WEAPON_INTROS[s].hit+.2]);
  fx.root.traverse(o=>{if(o.name==='phantom-afterimage'||o.parent?.name==='phantom-afterimage'||o.parent?.parent?.name==='phantom-afterimage')return;
   if(o.userData.__ghost)return;const g=(o as T.Mesh).geometry;if(!g)return;
   assert.ok(!(o instanceof T.Line),`${s}: line ${o.name}`);assert.ok(!['RingGeometry','CircleGeometry','TorusGeometry','ConeGeometry','PlaneGeometry','TubeGeometry','CylinderGeometry'].includes(g.type),`${s}: ${g.type} ${o.name}`);});
  const ghostFree=fx.root.children.filter(o=>o.name!=='phantom-afterimage');assert.ok(ghostFree.every(o=>o instanceof T.InstancedMesh||o instanceof T.PointLight),s);fx.dispose();}
});

test('each intro bursts on its own hit mark (voxels spawn within 30 ms of the hit; Belt Rattle rounds run at 24 Hz)',()=>{
 for(const s of styles){const hit=WEAPON_INTROS[s].hit,r=probeRig(WEAPON_INTROS[s].spec),fx=new WeaponIntroEffects(s,r);fx.update(hit-.03,r,1);const before=fx.blasts.solid.spawned+fx.blasts.glow.spawned;fx.update(hit+.03+INTRO_FX_STEP,r,1);const burst=fx.blasts.solid.spawned+fx.blasts.glow.spawned-before;assert.ok(burst>=25,`${s}: ${burst} voxels at the hit`);fx.dispose();}
});

test('intro effects are a pure function of age: frame rate, late joins and backwards scrubs give identical voxels',()=>{
 for(const s of styles){const at=WEAPON_INTROS[s].hit+.4,frames=(hz:number)=>Array.from({length:Math.floor(at*hz)},(_,i)=>(i+1)/hz).concat(at);
  const a=play(s,frames(60)),b=play(s,frames(144)),c=play(s,[at]),d=play(s,[4,1,at]);const ref=fingerprint(a.fx.blasts);
  for(const o of [b,c,d])assert.deepEqual(fingerprint(o.fx.blasts),ref,s);
  for(const o of [a,b,c,d])o.fx.dispose();}
});

test('effects quality and compatibility thin the voxels; pools stay within capacity over repeated playbacks',()=>{
 for(const s of styles){const at=WEAPON_INTROS[s].hit+.1,full=play(s,[at]).fx.blasts,low=play(s,[at],.3).fx.blasts,compat=play(s,[at],1,true).fx.blasts;
  const total=(b:ChipBlasts)=>b.solid.spawned+b.glow.spawned;assert.ok(total(low)<total(full),`${s} low ${total(low)} < ${total(full)}`);assert.ok(total(compat)<total(full),`${s} compat`);}
 const r=probeRig('machinegun'),fx=new WeaponIntroEffects('weapon-machinegun',r);for(let loop=0;loop<3;loop++)for(let t=0;t<=4.8;t+=1/60){fx.update(t,r,1);assert.ok(fx.blasts.solid.count<=fx.blasts.solid.capacity&&fx.blasts.glow.count<=fx.blasts.glow.capacity);}
 assert.ok(fx.root.children.length<=3+2,'no meshes added per frame');fx.dispose();
});
