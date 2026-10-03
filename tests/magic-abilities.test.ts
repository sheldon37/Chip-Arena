// MAGIC stream (design §6, §8 school chips, §16.2 M tests): the rebalanced primaries, Soak, the true lightning chain, the
// four Q spells and four E spells, the school-chip effects, bots, the perfect-aim TTK band and the Black Hole escapes.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {idleInput,v,len,sub,type Input,type SpecId,type Vec} from '../src/shared/config.js';
import {WEAPONS,statsFor,magazineCapacity} from '../src/shared/content.js';
import {kitFor,abilityActive} from '../src/shared/abilities.js';
import {ABILITY_HANDLERS} from '../src/shared/ability-system.js';
import {MAGIC_KITS} from '../src/shared/magic-kit.js';
import {ballisticStep,ballisticPitch} from '../src/shared/projectiles.js';
import {cameraPosition,weaponMuzzle} from '../src/shared/view.js';
import {botAbilities} from '../src/shared/bot-abilities.js';
import {Horde} from '../src/shared/pve/horde.js';
import {fakeParts,withPveParts} from './helpers/pve-fakes.js';
await initPhysics();

const SCHOOLS=['fire','water','lightning','darkness'] as const;
const near=(a:number,b:number,eps=1e-6,msg='')=>assert.ok(Math.abs(a-b)<=eps,`${msg} ${a} vs ${b}`);
/** Flat floor; caster `a` (no mana regen, so costs read exactly) at the origin facing +z; pistol victim `b` `dist` ahead. */
function game(spec:SpecId,dist=7,chips:Record<string,number>={},victim:SpecId='pistol'){
 const s=new Simulation({sandbox:true}),a=s.addPlayer('a','A',WEAPONS[spec].class)!,b=s.addPlayer('b','B',WEAPONS[victim].class)!;a.spec=spec;a.chips=chips;b.spec=victim;s.resetRound();s.phase='fight';
 a.stats={...statsFor(chips,spec),manaRegen:0};a.mana=100;b.stats=statsFor({},victim);
 for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});
 a.body.setTranslation(v(0,.9,0),true);b.body.setTranslation(v(0,.9,dist),true);s.world.step();for(const p of [a,b])p.grounded=true;return {s,a,b};
}
const extra=(s:Simulation,id:string,at:Vec)=>{const p=s.addPlayer(id,id)!;p.body.setTranslation(at,true);s.world.step();return p;};
let seq=0;
/** One tick; every player gets a fresh idle input (keeping its aim) merged with `inputs[id]`. */
function step(s:Simulation,inputs:Record<string,Partial<Input>>={}){for(const p of s.players.values()){s.setInput(p.id,{...idleInput(),seq:++seq,yaw:p.input.yaw,pitch:p.input.pitch,camera:p.input.camera,...inputs[p.id]});p.inputAt=s.time;}s.step();}
const run=(s:Simulation,n:number,inputs:Record<string,Partial<Input>>={})=>{for(let i=0;i<n;i++)step(s,inputs);};
/** Points the first-person camera ray at `to`. */
function aim(p:Player,to:Vec){const pos=p.body.translation();let yaw=Math.atan2(to.x-pos.x,to.z-pos.z),pitch=0;for(let k=0;k<4;k++){const c=cameraPosition(pos,yaw,pitch,'first');pitch=Math.atan2(to.y-c.y,Math.hypot(to.x-c.x,to.z-c.z));}p.input={...p.input,yaw,pitch,camera:'first'};}
/** Presses and runs until the windup releases. */
function release(s:Simulation,a:Player,slot:'q'|'e'){assert.equal(s.abilities.press(a,slot),'cast');for(let i=0;i<60&&a.abilityCast;i++)step(s);assert.equal(a.abilityCast,undefined,'released');}
const motion=(s:Simulation,id:string)=>s.snapshot().players.find(p=>p.id===id)!.motionScale??1;
const hits=(s:Simulation,target:string,kind?:string)=>s.events.filter(e=>e.type==='hit'&&e.target===target&&(!kind||e.message===kind));
const feet=(p:Player)=>{const t=p.body.translation();return v(t.x,.02,t.z);};

// ---- primaries ------------------------------------------------------------------------------------------------------
test('primaries: §6.2 numbers (fire cost stays 8), orb radii, the E id as secondary, and a 20 m drop ≤ 1.6 m',()=>{
 const want={fire:{damage:24,interval:.5,speed:34,gravity:9,cost:8,burn:4,blast:1.5},water:{damage:20,interval:.38,speed:42,gravity:10,cost:6,shotRadius:.16},
  lightning:{damage:15,interval:.27,speed:85,gravity:30,cost:5,chain:6},darkness:{damage:34,interval:.66,speed:26,gravity:3,cost:10,shotRadius:.2,drain:.2}};
 for(const spec of SCHOOLS){const w=WEAPONS[spec];for(const [k,x] of Object.entries(want[spec]))assert.equal((w as unknown as Record<string,number>)[k],x,`${spec}.${k}`);
  assert.equal(w.secondary,MAGIC_KITS[spec].e.id);assert.equal(w.secondaryCooldown,MAGIC_KITS[spec].e.cooldown);assert.ok(w.gravity>0,'every school still lands');
  const drop=-ballisticStep(v(w.speed,0,0),w.gravity,20/w.speed).displacement.y;assert.ok(drop<=1.6,`${spec} 20 m drop ${drop}`);
  const {s,a}=game(spec);try{s.spawnProjectile(a);const shot=[...s.projectiles.values()][0];near(shot.radius,w.shotRadius??.14);near(len(shot.vel),w.speed,1e-9);near(shot.gravity,w.gravity);near(shot.damage,w.damage);}finally{s.dispose();}}
});
test('Soak: a direct water hit sets motionScale .8 for .8 s, refreshed at most once per 1.5 s per target; fire never soaks',()=>{
 const {s,a,b}=game('water');try{const hit=()=>s.directHit(a,b,b.body.translation(),v(0,0,1),10,0,s.uid('t'));
  hit();const t0=s.time;assert.deepEqual({...b.slowed},{factor:.8,until:t0+.8,kind:'soak'});near(motion(s,'b'),.8);
  run(s,30);hit();near(b.slowed!.until,t0+.8,1e-9,'no refresh inside 1.5 s');
  run(s,24);assert.equal(motion(s,'b'),1,'expired after .8 s');assert.equal(b.slowImmuneUntil,undefined,'a weak slow grants no immunity');
  run(s,40);assert.ok(s.time-t0>=1.5);hit();near(b.slowed!.until,s.time+.8,1e-9,'refreshed after 1.5 s');near(motion(s,'b'),.8);
 }finally{s.dispose();}
 const f=game('fire');try{f.s.directHit(f.a,f.b,f.b.body.translation(),v(0,0,1),10,0,'x');assert.equal(f.b.slowed,undefined);}finally{f.s.dispose();}
});
test('lightning chain: 6 to the nearest enemy player within 4 m with LOS, else an enemy add, else a prop',()=>{
 const {s,a,b}=game('lightning');try{const c=extra(s,'c',v(2.5,.9,7)),d=extra(s,'d',v(-3.5,.9,7.5));
  s.directHit(a,b,b.body.translation(),v(0,0,1),15,0,'one');assert.equal(c.hp,94,'nearest enemy player');assert.equal(d.hp,100,'only one target');
  const arc=s.events.filter(e=>e.type==='arc').at(-1)!;near(arc.from!.x,c.body.translation().x);assert.equal(arc.source,'a');
  for(const p of [c,d])p.body.setTranslation(v(p.id==='c'?25:-25,.9,20),true);s.chaos.summon(c,1,'zombie',v(1.5,.9,7.5),30);const z=[...s.chaos.minions.values()][0];z.body.setTranslation(v(1.5,.9,7.5),true);s.world.step();const zhp=z.hp;
  s.directHit(a,b,b.body.translation(),v(0,0,1),15,0,'two');near(z.hp,zhp-6,1e-9,'enemy add');
  s.chaos.clear();const prop=s.addProp({id:'crate',material:'wood',pos:v(1.2,1,7.4),size:v(.8,.8,.8),dynamic:true})!;s.world.step();const php=prop.hp;
  s.directHit(a,b,b.body.translation(),v(0,0,1),15,0,'three');near(prop.hp,php-6,1e-9,'prop fallback');
 }finally{s.dispose();}
});

// ---- costs, cooldowns, gating ----------------------------------------------------------------------------------------
test('every Q/E is live, costs its mana and starts its cooldown at release, and Q casts during the primary cooldown',()=>{
 for(const spec of SCHOOLS)for(const slot of ['q','e'] as const){const def=MAGIC_KITS[spec][slot];assert.equal(abilityActive(spec,slot),true);assert.ok(ABILITY_HANDLERS[def.id],def.id);
  const {s,a,b}=game(spec,10);try{a.cooldown=5;a.reloadTime=0;aim(a,slot==='e'?feet(b):b.body.translation());release(s,a,slot);
   near(a.mana,100-def.cost,1e-9,`${def.id} cost`);if(slot==='q')near(a.abilityCd,def.cooldown,.05,def.id);else{near(a.secondaryCd,def.cooldown,.05,def.id);assert.equal(a.eCharges,0);}
   assert.ok(a.cooldown>4,'cast while the primary cooldown runs; the lockout never shortens it');}finally{s.dispose();}}
});

// ---- Q spells ---------------------------------------------------------------------------------------------------------
test('Flame Lance: 70 m/s shot pierces to a second target, 30 damage + 8 burn DPS each',()=>{
 const {s,a,b}=game('fire',8);try{const c=extra(s,'c',v(0,.9,13));aim(a,v(0,.75,8));release(s,a,'q');
  const shot=[...s.projectiles.values()].find(q=>q.ability?.id==='flame-lance')!;near(len(shot.vel),70,.05);assert.equal(shot.pierces,2);near(shot.radius,.2);
  run(s,30);for(const t of [b,c]){const h=hits(s,t.id,'ability');assert.equal(h.length,1,t.id);near(h[0].amount!,30,1e-9);assert.equal(t.burns.get('a')?.dps,8);}
  assert.ok(s.events.some(e=>e.type==='explosion'&&e.message==='flame-lance'));
 }finally{s.dispose();}
});
test('Tidal Wave: passes through every enemy once: 22 damage, knockback 950 along the wave, speedFactor .6 for 1.5 s; walls stop it',()=>{
 const {s,a,b}=game('water',6);try{const c=extra(s,'c',v(1.1,.9,9));release(s,a,'q');const t0=s.time;
  const shot=[...s.projectiles.values()].find(q=>q.ability?.id==='tidal-wave')!;near(shot.radius,1);near(len(shot.vel),24,1e-9);near(shot.life,.7,.02);
  run(s,40);for(const t of [b,c]){const h=hits(s,t.id,'ability');assert.equal(h.length,1,`${t.id} hit once`);near(h[0].amount!,22,1e-9);near(t.slowed!.until,h[0].time+1.5,1e-9);assert.equal(t.slowed!.kind,'tidal-wave');}
  near(motion(s,'b'),.6);assert.ok(b.body.translation().z>7,'pushed away');assert.ok(s.time-t0<1.5);
  const wall=game('water',9);try{wall.s.addProp({id:'wall',material:'metal',pos:v(0,1.5,4),size:v(6,3,.4)});wall.s.world.step();release(wall.s,wall.a,'q');run(wall.s,40);assert.equal(hits(wall.s,'b').length,0,'the wall stops the wave');}finally{wall.s.dispose();}
 }finally{s.dispose();}
});
test('Blink Strike: ≤ 9 m along the move input, 24 to enemies within 1.4 m of the path, arrival discharge; Storm Recall refunds 2 s per enemy (max 4)',()=>{
 for(const recall of [0,1]){const {s,a,b}=game('lightning',3,recall?{'storm-recall':1}:{});try{b.body.setTranslation(v(1.1,.9,3),true);const c=extra(s,'c',v(-1.1,.9,6)),far=extra(s,'far',v(3,.9,5));
   const start={...a.body.translation()};a.input={...a.input,z:1};assert.equal(s.abilities.press(a,'q'),'cast');for(let i=0;i<10&&a.abilityCast;i++)step(s,{a:{z:1}});
   const end=a.body.translation(),moved=Math.hypot(end.x-start.x,end.z-start.z);assert.ok(moved<=9.05&&moved>8,`blink ${moved}`);
   assert.ok(s.events.some(e=>e.type==='teleport'&&e.message==='blink-strike'&&e.source==='a'));
   for(const t of [b,c])near(hits(s,t.id,'ability')[0].amount!,24,1e-9,t.id);assert.equal(far.hp,100,'1.9 m from the path');
   near(a.abilityCd,recall?6-4:6,.05,'cooldown');}finally{s.dispose();}}
});
test('Shadow Shackles: 20 damage, speedFactor .4 for 1.2 s (then 1 s immunity), heals the caster 50 % of actual damage',()=>{
 const {s,a,b}=game('darkness',8);try{a.hp=50;aim(a,b.body.translation());release(s,a,'q');const shot=[...s.projectiles.values()].find(q=>q.ability?.id==='shadow-shackles')!;near(len(shot.vel),48,.05);near(shot.radius,.3);
  run(s,20);const h=hits(s,'b','ability');assert.equal(h.length,1);near(h[0].amount!,20,1e-9);near(a.hp,60,1e-9,'50 % heal');assert.equal(b.slowed!.kind,'shackles');near(motion(s,'b'),.4);
  near(b.slowed!.until,h[0].time+1.2,1e-9);run(s,80);assert.equal(motion(s,'b'),1);assert.ok(b.slowImmuneUntil!>s.time,'strong slow → immunity');
 }finally{s.dispose();}
});

// ---- E spells ---------------------------------------------------------------------------------------------------------
test('Meteor Strike: bomb field 4.5 m / 70 after .9 s (+.3 within 4 m), then burning ground 3.2 m / 3 s / 12 DPS; Cinder Field ×1.5',()=>{
 for(const cinder of [0,1])for(const dist of [10,3]){const {s,a,b}=game('fire',dist,cinder?{'cinder-field':1}:{});try{aim(a,feet(b));release(s,a,'e');const t0=s.time;
  const bomb=[...s.chaos.fields.values()].find(f=>f.fx==='meteor-strike')!;assert.equal(bomb.kind,'bomb');near(bomb.radius,4.5);near(bomb.damage,70);near(bomb.until-t0,dist<4?1.2:.9,1e-9);
  for(let i=0;i<90&&s.chaos.fields.has(bomb.id);i++)step(s);const fire=[...s.chaos.fields.values()].find(f=>f.fx==='meteor-strike')!;
  assert.equal(fire.kind,'fire');near(fire.radius,3.2*(cinder?1.5:1),1e-9);near(fire.until-fire.born,3*(cinder?1.5:1),1e-9);near(fire.damage,12);assert.equal(fire.damageKind,'burn');
  if(dist===10)assert.ok(hits(s,'b','ability')[0].amount!>=40,'centre impact');}finally{s.dispose();}}
});
test('Geyser Wall: three 3 m geysers 3 m apart across the aim erupt left to right at +.45/.80/1.15 s (+.3 close); knock-up and speedFactor .6 for 1 s',()=>{
 for(const dist of [10,3]){const {s,a,b}=game('water',dist);try{aim(a,feet(b));release(s,a,'e');const t0=s.time;
  const g=[...s.chaos.fields.values()].filter(f=>f.fx==='geyser-wall').sort((x,y)=>x.until-y.until);assert.equal(g.length,3);
  const late=dist<4?.3:0;for(const [i,f] of g.entries()){assert.equal(f.kind,'bomb');near(f.until-t0,[.45,.8,1.15][i]+late,1e-9);near(f.radius,3);near(f.damage,60);assert.equal(f.cast,g[0].cast);}
  near(g[0].pos.x,3,.05,'left as the caster sees it (facing +z)');near(g[1].pos.x,0,.05);near(g[2].pos.x,-3,.05);near(len(sub(g[2].pos,g[1].pos)),3,.05);
  if(dist<4)continue;let top=0;for(let i=0;i<90;i++){step(s);top=Math.max(top,b.body.translation().y);}
  const h=hits(s,'b','ability'),mid=h.find(x=>Math.abs(x.time-t0-.8)<.02)!;assert.ok(h.every(x=>[.45,.8,1.15].some(t=>Math.abs(x.time-t0-t)<.02)),'hits only at eruptions');assert.ok(mid.amount!>=40,`${mid.amount}`);
  assert.ok(top>2,`knock-up ${top}`);assert.equal(b.slowed?.kind,'geyser-wall');near(b.slowed!.until,h.at(-1)!.time+1,1e-9);near(motion(s,'b'),.6);}finally{s.dispose();}}
});
test('Ball Lightning: 11 m/s orb passes through enemies, zaps at most 2 targets within 7 m per .3 s for 11 each, bursts 3.5 m on contact or expiry',()=>{
 const {s,a,b}=game('lightning',8);try{const c=extra(s,'c',v(2,.9,9)),d=extra(s,'d',v(-2.5,.9,10));aim(a,b.body.translation());release(s,a,'e');
  const shot=[...s.projectiles.values()].find(q=>q.ability?.id==='ball-lightning')!;near(len(shot.vel),11,1e-6);near(shot.radius,.45);near(shot.life,3,.02);
  run(s,200);const zaps=s.events.filter(e=>e.type==='arc'&&e.source==='a'),at=new Map<number,number>();for(const z of zaps)at.set(z.time,(at.get(z.time)||0)+1);
  assert.ok(zaps.length>=4,`${zaps.length} zaps`);for(const [t,n] of at){assert.ok(n<=2,`${n} zaps at ${t}`);}const times=[...at.keys()].sort((x,y)=>x-y);for(let i=1;i<times.length;i++)assert.ok(times[i]-times[i-1]>=.3-1e-9);
  for(const t of [b,c,d])for(const h of hits(s,t.id,'ability').filter(h=>h.amount!<12))near(h.amount!,11,1e-9);
  assert.ok(s.events.some(e=>e.type==='explosion'&&e.message==='ball-lightning'&&e.radius===3.5),'burst');assert.equal([...s.projectiles.values()].filter(q=>q.ability).length,0);
 }finally{s.dispose();}
});
test('Black Hole: 6 m vortex, 2.2 s, force 16 (tuned from 18 so case (a) holds for the heaviest roll), 8 DPS, then a 4 m / 55 collapse; escapes (a)-(d); Abyssal Core ×1.25 radius, ×1.2 collapse',()=>{
 // (a) unslowed, 5 m out, rolls outward at .2 s; (b) at the centre, roll i-frames cover the collapse; (c) shackled (.4) at 5 m, rolls at .2 s; (d) idle at 5 m.
 for(const victim of ['pistol','greatsword'] as SpecId[])for(const kase of ['a','b','c','d']){const {s,a,b}=game('darkness',10,{},victim);try{
  const centre=v(0,.02,10);b.body.setTranslation(kase==='b'?v(0,.9,10):v(5,.9,10),true);s.world.step();if(kase==='c'){s.abilities.slow(b,.4,5,'shackles','a');}
  aim(a,centre);release(s,a,'e');const f=[...s.chaos.fields.values()].find(x=>x.fx==='singularity')!;
  assert.equal(f.kind,'black-hole');near(f.radius,6);near(f.until-f.born,2.2,1e-9);assert.equal(f.force,16);near(f.damage,8);assert.equal(f.damageKind,'poison','DoT ticks never cancel a roll');
  const t0=s.time;let rolled=false,collapse=0;
  for(let i=0;i<200&&s.chaos.fields.has(f.id);i++){const t=s.time-t0;let dash:Partial<Input>={};
   if((kase==='a'||kase==='c')&&!rolled&&t>=.2){dash={dash:true,x:1,z:0};rolled=true;}
   if(kase==='b'&&!rolled&&s.time>=f.until-.075-.06){dash={dash:true,x:0,z:1};rolled=true;}
   const n=s.events.at(-1)?.id??0;step(s,{b:dash});if(!s.chaos.fields.has(f.id))collapse=s.events.filter(e=>e.id>n&&e.type==='hit'&&e.target==='b'&&e.message==='ability').reduce((x,e)=>x+(e.amount||0),0);}
  if(kase==='d')assert.ok(collapse>=40,`${victim} (d) takes the collapse: ${collapse}`);else assert.equal(collapse,0,`${victim} (${kase}) escapes`);
  assert.ok(s.events.some(e=>e.type==='vacuum'));}finally{s.dispose();}}
 for(const abyss of [0,1]){const {s,a,b}=game('darkness',10,abyss?{'abyssal-core':1}:{});try{const blasts:number[][]=[],blast=s.abilities.blast.bind(s.abilities);
  s.abilities.blast=(...x)=>{blasts.push([x[2],x[3]]);return blast(...x);};aim(a,feet(b));release(s,a,'e');near([...s.chaos.fields.values()][0].radius,6*(abyss?1.25:1));run(s,150);
  assert.deepEqual(blasts.map(([r,d])=>[+r.toFixed(6),+d.toFixed(6)]),[[4*(abyss?1.25:1),55*(abyss?1.2:1)]]);}finally{s.dispose();}}
});
test('every E deals ≥ 40 to a stationary target at the centre (Ball Lightning aimed at the lower body)',()=>{
 for(const spec of SCHOOLS){const {s,a,b}=game(spec,12);try{aim(a,spec==='lightning'?v(0,.5,12):feet(b));release(s,a,'e');run(s,300);assert.ok(100-b.hp>=40,`${spec} E ${100-b.hp}`);}finally{s.dispose();}}
});
/** §7.1 gate (the reason Geyser Wall is 60 / 3 m and Ball Lightning zaps 11 within 7 m instead of the design's 32 / 2.5 m and
 * 8 within 5.5 m): perfect aim at the strafer's current position, no lead, E pressed at its bot mid-range; the strafer runs
 * ±x at full speed and re-rolls its direction every .7 s (seeded LCG), kept within |x| < 9. */
test('every E averages ≥ 35 damage per cast against a 6.8 m/s strafer at its bot mid-range (48 seeds)',()=>{
 const out:Record<string,number>={};
 for(const spec of SCHOOLS){const def=MAGIC_KITS[spec].e,dist=(def.bot!.min+def.bot!.max)/2;let sum=0;
  for(let n=1;n<=48;n++){const seed=n*104729,{s,a,b}=game(spec,dist,{},'sword');try{let r=seed>>>0,dir=1,next=0;
   const strafe=()=>{if(s.time>=next){r=(Math.imul(r,1664525)+1013904223)>>>0;dir=r/4294967296<.5?-1:1;next=s.time+.7;}const x=b.body.translation().x;if(x>9)dir=-1;if(x<-9)dir=1;return {b:{x:dir,z:0,yaw:Math.PI}};};
   for(let i=0;i<30+seed%40;i++)step(s,strafe());const t=b.body.translation();aim(a,v(t.x,spec==='lightning'?.5:.02,t.z));
   assert.equal(s.abilities.press(a,'e'),'cast');for(let i=0;i<240;i++)step(s,strafe());sum+=100-b.hp;}finally{s.dispose();}}
  out[spec]=+(sum/48).toFixed(1);}
 console.log('E vs 6.8 m/s strafer (mean per cast):',JSON.stringify(out));for(const spec of SCHOOLS)assert.ok(out[spec]>=35,`${spec} ${out[spec]}`);
});
test('Undertow: Tidal Wave drags enemies toward the caster instead of pushing them',()=>{
 for(const undertow of [0,1]){const {s,a,b}=game('water',6,undertow?{undertow:1}:{});try{release(s,a,'q');let ext=0;for(let i=0;i<20&&!hits(s,'b').length;i++)step(s);ext=b.ext.z;
  assert.ok(undertow?ext<-5:ext>5,`ext.z ${ext}`);}finally{s.dispose();}}
});

// ---- PvE, Necronomicon, bots ------------------------------------------------------------------------------------------
/** A PvE room with one magic party member `h1` and one active shambler `dist` ahead (pve-lifesteal.test.ts recipe). */
function room(spec:SpecId,dist=9){
 const {factory}=fakeParts({horde:h=>new Horde(h)});
 return withPveParts(factory,()=>{
  const s=new Simulation({pve:{boss:'siren',seed:7}});s.addPlayer('h1','h1','magic');s.pve!.command('h1',{action:'hello',v:1},false);s.phase='fight';s.fightStart=s.time;
  const p=s.players.get('h1')!,h=s.pve!.horde as Horde;p.spec=spec;p.class='magic';p.chips={};p.stats=statsFor({},spec);p.hp=p.stats.hp;p.mana=p.stats.maxMana;p.ammo=magazineCapacity(spec,p.stats);
  const n=h.spawn('shambler',{entry:4})!;for(let i=0;i<600&&h.record(n)?.phase!=='active';i++){p.inputAt=s.time;s.step();}
  const add=h.record(n)!;assert.equal(add.phase,'active');add.hp=add.maxHp=4000;add.minion!.hp=add.minion!.maxHp=4000;
  p.body.setTranslation(v(0,.9,0),true);add.minion!.body.setTranslation(v(0,1,dist),true);add.minion!.body.setLinvel(v(),true);s.world.step();p.grounded=true;
  return {s,p,add};
 });
}
test('every E damages PvE adds (Meteor through explode on a siren-add)',()=>{
 for(const spec of SCHOOLS){const {s,p,add}=room(spec);try{const m=add.minion!.body.translation();aim(p,spec==='lightning'?v(m.x,.6,m.z):v(m.x,.02,m.z));const hp=add.hp;
  release(s,p,'e');for(let i=0;i<200;i++){add.minion!.body.setLinvel(v(),true);step(s);}assert.ok(add.hp<=hp-10,`${spec} E vs add: ${hp-add.hp}`);
  if(spec==='fire')assert.ok(s.events.some(e=>e.type==='explosion'&&e.message==='meteor-strike'));}finally{s.dispose();}}
});
test('Necronomicon still replaces magic E, and its bots wait for its cooldown instead of pressing E',()=>{
 for(const spec of SCHOOLS){const {s,a}=game(spec,10,{necronomicon:1});try{a.stats={...a.stats,manaRegen:1};s.secondary(a);assert.equal(s.chaos.minions.size,3,spec);assert.equal(s.chaos.fields.size,0);assert.equal(a.abilityCast,undefined);near(a.secondaryCd,10);
  const i={...idleInput(),attack:true};a.cooldown=0;botAbilities(s,a,i,10,true,2,v(0,.9,10));assert.equal(i.secondary,false,'necro on cooldown → no E press');}finally{s.dispose();}}
});
test('bots: a magic bot below its E bot.min keeps firing its primary and never sets secondary; rows use desired 9 m; Blink escapes',()=>{
 for(const spec of SCHOOLS)assert.equal(MAGIC_KITS[spec].e.bot?.desired,9);
 const {s,a,b}=game('fire',3);try{a.bot='hard';let fired=0;
  for(let k=0;k<240;k++){const i=s.botInput(a);assert.equal(i.secondary,false);assert.equal(i.ability,false,'Q min 4 m too');s.setInput('a',{...i,seq:++seq});b.inputAt=a.inputAt=s.time;const before=a.lastAttack;s.step();if(a.lastAttack>before)fired++;const at=a.body.translation();b.body.setTranslation(v(at.x,.9,at.z+3),true);}
  assert.ok(fired>=3,`${fired} primaries`);assert.equal(a.lastSecondary,-10);}finally{s.dispose();}
 const l=game('lightning',3);try{const i={...idleInput(),attack:true};botAbilities(l.s,l.a,i,3,true,2,l.b.body.translation());assert.equal(i.ability,true);assert.ok(i.z<-.9,'blink away from the enemy');assert.equal(i.attack,false);}finally{l.s.dispose();}
});

// ---- balance probe ---------------------------------------------------------------------------------------------------
/** Perfect-aim still target (MAP §3 recipe): exact ballistic pitch from the muzzle, TTK from the first shot event. */
function ttk(spec:SpecId,dist:number){
 const {s,a,b}=game(spec,dist,{},'sword');try{a.stats=statsFor({},spec);const w=WEAPONS[spec];let first=-1;
  for(let i=0;i<60*6&&b.hp>0;i++){const pos=a.body.translation(),t=b.body.translation(),yaw=Math.atan2(t.x-pos.x,t.z-pos.z),m=weaponMuzzle(pos,yaw,(o,d,x)=>s.ray(o,d,x,a.id)?.timeOfImpact??null);
   const pitch=ballisticPitch(Math.hypot(t.x-m.x,t.z-m.z),t.y-m.y,w.speed,w.gravity);step(s,{a:{yaw,pitch,attack:true,camera:'first'},b:{yaw:Math.PI}});
   if(first<0&&s.events.some(e=>e.type==='shot'&&e.source==='a'))first=s.time;}
  assert.equal(b.hp,0,`${spec} kills at ${dist} m`);return s.time-first;}finally{s.dispose();}
}
test('perfect-aim still-target TTK of each school is inside 1.5–2.1 s at 8 and 15 m',()=>{
 const out:Record<string,number[]>={};for(const spec of SCHOOLS){out[spec]=[8,15].map(d=>+ttk(spec,d).toFixed(3));for(const t of out[spec])assert.ok(t>=1.5&&t<=2.1,`${spec} ${out[spec]}`);}
 console.log('perfect-aim still TTK (8 m, 15 m):',JSON.stringify(out));
});
