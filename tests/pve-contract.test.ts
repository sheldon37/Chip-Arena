// PvE contract, codec, protocol, balance, layout, boss track, fixtures and determinism guards (design §11 F, acceptance 9).
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync,readdirSync} from 'node:fs';
import {join} from 'node:path';
import * as T from 'three';
import {ADD_KINDS_ORDER,ADD_STATES_ORDER,ADD_ATTACKS_ORDER,ADD_PREFIX,LIMB,PVE_EVENT,PVE_WIRE,PVE_CONTRACT,PVE_FORCED_OPTIONS,PVE_TIMING,
 decodeAdd,encodeAdd,interpolatePve,isPveWire,pveOf,pveOption,sanitizePveCommand,type PveAddView,type PveView} from '../src/shared/pve/contract.js';
import {ADD_KINDS,DROP_ODDS,OFFER_WEIGHTS,PVE,PVE_SIREN,WAVES,bossHp,chipPrice,combineMods,damageScale,partyFactor,revivePrice,waveScale,IDENTITY_MODS} from '../src/shared/pve/balance.js';
import {hash32,mulberry32,PveRng} from '../src/shared/pve/rng.js';
import {PVE_ENTRIES,PVE_LAYOUT,PVE_MACHINES,machineBlocks} from '../src/shared/pve/layout.js';
import {SIREN_BOSS,SIREN_BOSS_CHIPS} from '../src/shared/pve/bosses.js';
import {PVE_FIXTURE_NAMES,pveFixture,fixtureHordeRing,fixtureBossPlayer} from '../src/shared/pve/fixtures.js';
import {validPacket} from '../src/shared/relay-protocol.js';
import {CHIPS} from '../src/shared/content.js';import {SIREN_CHIPS} from '../src/shared/siren-chips.js';
import {arenaBlocks} from '../src/shared/arena.js';
import {SIREN_VENTS,SIREN_ADD_SPAWNS} from '../src/shared/siren-stage-layout.js';
import {ICON_PATHS} from '../src/client/icons.generated.js';
import {PveVisuals,playerShown,type PveRigRef,type PveVisualsContext} from '../src/client/pve/visuals.js';
import type {PlayerView,Snapshot} from '../src/shared/simulation.js';

const randomView=(rng:PveRng,slots:string[]):PveAddView=>{
 const opt=(p:number)=>rng.next()<p?Math.round((.01+rng.next()*500)*1000)/1000:undefined,hitAt=opt(.5);
 return {n:1+rng.int(100000),id:'',pos:{x:(rng.next()-.5)*64,y:rng.next()*3,z:(rng.next()-.5)*56},yaw:(rng.next()-.5)*2*Math.PI,vel:{x:(rng.next()-.5)*10,z:(rng.next()-.5)*10},
  hp:rng.next(),kind:rng.pick(ADD_KINDS_ORDER),state:rng.pick(ADD_STATES_ORDER),attack:rng.pick(ADD_ATTACKS_ORDER),side:rng.next()<.5?1:-1,lost:rng.int(512),
  entry:rng.int(17)-1,headless:rng.next()<.3,target:rng.next()<.7?rng.pick(slots):undefined,spawnAt:opt(.6),attackAt:opt(.5),hitAt,hitYaw:hitAt===undefined?undefined:(rng.next()-.5)*6,deathAt:opt(.2),diveAt:opt(.2)};
};
test('add codec round-trips 1,000 random views with target slots within quantization',()=>{
 const rng=new PveRng(77),slots=['h1','h2','party-bot-1','h3','recruit-1','recruit-2','recruit-3'];
 for(let i=0;i<1000;i++){
  const a=randomView(rng,slots),t=encodeAdd(a,slots),b=decodeAdd(t,slots);
  assert.equal(t.length,15);assert.ok(t.every(x=>typeof x==='number'&&Number.isFinite(x)));
  assert.equal(b.n,a.n);assert.equal(b.id,ADD_PREFIX+a.n);
  for(const k of ['x','y','z'] as const)assert.ok(Math.abs(b.pos[k]-a.pos[k])<=.005+1e-9,`pos.${k}`);
  assert.ok(Math.abs(b.yaw-a.yaw)<=.005+1e-9);assert.ok(Math.abs(b.hp-a.hp)<=.005+1e-9);
  assert.ok(Math.abs(b.vel.x-a.vel.x)<=.05+1e-9&&Math.abs(b.vel.z-a.vel.z)<=.05+1e-9);
  assert.deepEqual([b.kind,b.state,b.attack,b.side,b.lost,b.entry,b.headless,b.target],[a.kind,a.state,a.attack,a.side,a.lost,a.entry,a.headless,a.target]);
  for(const k of ['spawnAt','attackAt','hitAt','deathAt','diveAt'] as const){if(a[k]===undefined)assert.equal(b[k],undefined,k);else assert.ok(Math.abs(b[k]!-a[k]!)<=.005+1e-9,k);}
  if(a.hitAt!==undefined)assert.ok(Math.abs(b.hitYaw!-a.hitYaw!)<=.05+1e-9);
 }
 // A target that is not in slots encodes as none; slot order is the only mapping.
 const lone=encodeAdd({...randomView(rng,['x']),target:'nobody'},slots);assert.equal(decodeAdd(lone,slots).target,undefined);
});
test('interpolatePve lerps add positions by n, yaw the short way, and keeps everything else from current',()=>{
 const base=pveFixture('wave',{time:10}),slots=base.slots;
 const mk=(x:number,yaw:number,hp:number)=>encodeAdd({n:5,id:'add-5',pos:{x,y:1,z:0},yaw,vel:{x:2,z:0},hp,kind:'runner',state:'active',attack:'lunge',side:1,lost:0,entry:-1,headless:false},slots);
 const a:PveView={...base,adds:[mk(0,3,1)]},b:PveView={...base,adds:[mk(4,-3,.5)],waveLeft:3},current:PveView={...b,adds:[mk(4,-3,.5),mk(9,0,1).map((x,i)=>i===0?6:x) as typeof b.adds[0]]};
 const out=interpolatePve(a,b,current,.25,.1)!;
 assert.equal(out.waveLeft,3);assert.equal(out.adds.length,2);
 assert.equal(out.adds[0][1],0+(4-0)*.25+2*.1);assert.equal(out.adds[0][7],50,'hp from current');
 assert.ok(Math.abs(out.adds[0][4]-(3+(2*Math.PI-6)*.25))<1e-9,'shortest yaw');
 assert.deepEqual(out.adds[1],current.adds[1],'adds missing from a stay as current');
 assert.equal(interpolatePve(undefined,b,current,.5,0),current);assert.equal(interpolatePve(a,b,undefined,.5,0),undefined);
});
test('sanitizePveCommand accepts every action and rejects junk',()=>{
 const key='0123456789abcdef0123456789abcdef';
 assert.deepEqual(sanitizePveCommand({action:'hello',v:1,key,n:3,extra:'x'}),{action:'hello',v:1,key,n:3});
 assert.deepEqual(sanitizePveCommand({action:'hello',v:PVE_CONTRACT}),{action:'hello',v:1});
 assert.deepEqual(sanitizePveCommand({action:'open'}),{action:'open'});
 assert.deepEqual(sanitizePveCommand({action:'open',pick:'pick-2',n:1}),{action:'open',pick:'pick-2',n:1});
 assert.deepEqual(sanitizePveCommand({action:'close',n:2}),{action:'close',n:2});
 assert.deepEqual(sanitizePveCommand({action:'pick',pick:'pick-1',chip:'vital'}),{action:'pick',pick:'pick-1',chip:'vital'});
 assert.deepEqual(sanitizePveCommand({action:'prefs',autoOpen:false}),{action:'prefs',autoOpen:false});
 assert.deepEqual(sanitizePveCommand({action:'debug',op:'skip-wave'}),{action:'debug',op:'skip-wave'});
 for(const junk of [null,undefined,1,'hello',[],{},{action:'nope'},{action:'hello'},{action:'hello',v:1.5},{action:'hello',v:1,key:'XYZ'},
  {action:'hello',v:1,key:'abc'},{action:'hello',v:1,key:'0123456789ABCDEF0123456789ABCDEF'},{action:'hello',v:1,key:'a'.repeat(65)},
  {action:'open',pick:''},{action:'open',pick:'p'.repeat(65)},{action:'pick',pick:'pick-1'},{action:'pick',pick:'pick-1',chip:'c'.repeat(65)},
  {action:'prefs',autoOpen:'yes'},{action:'debug',op:''},{action:'debug',op:'o'.repeat(41)},{action:'close',n:1.5},{action:'close',n:'1'}])
  assert.equal(sanitizePveCommand(junk),null,JSON.stringify(junk));
});
test('wire form rides the relay chip type (D32): guest chip+pve packets pass, a pve type is rejected',()=>{
 const packet=(message:unknown)=>({seq:1,ack:0,events:[{id:1,message}]});
 assert.equal(PVE_WIRE,'chip');
 assert.equal(validPacket(packet({type:'chip',pve:{action:'open',n:1}}),false),true);
 assert.equal(validPacket(packet({type:'pve',pve:{action:'open',n:1}}),false),false);
 assert.equal(isPveWire({type:'chip',pve:{action:'open'}}),true);
 for(const m of [{type:'chip',id:'vital'},{type:'chip',pve:null},{type:'pve',pve:{}},null,'chip'])assert.equal(isPveWire(m),false);
});
test('pveOption reads only boss, skipWaves and partyBots from the client; authority values come from ctx',()=>{
 assert.deepEqual(pveOption({boss:'siren',seed:1,debug:true},{seed:9}),{boss:'siren',seed:9});
 assert.deepEqual(pveOption({boss:'siren',skipWaves:true,partyBots:7,relay:true,local:true},{seed:5,debug:true,relay:true}),{boss:'siren',seed:5,skipWaves:true,partyBots:3,debug:true,relay:true});
 assert.deepEqual(pveOption({boss:'siren',partyBots:2.7},{seed:2**32+3,local:true}),{boss:'siren',seed:3,partyBots:2,local:true});
 for(const raw of [undefined,null,true,'siren',{},{boss:'emperor'}])assert.equal(pveOption(raw,{seed:1}),undefined);
 assert.equal(PVE_FORCED_OPTIONS.map,'siren');assert.equal(PVE_FORCED_OPTIONS.gameMode,'teams');assert.equal(PVE_FORCED_OPTIONS.sandbox,false);
});
test('rng: hash32 and mulberry32 are stable, forks are independent and weighted picks follow weights',()=>{
 assert.equal(hash32(1,'horde'),hash32(1,'horde'));assert.notEqual(hash32(1,'horde'),hash32(1,'director'));assert.notEqual(hash32(1,2),hash32(2,1));
 const a=mulberry32(42),b=mulberry32(42);for(let i=0;i<100;i++){const x=a();assert.equal(x,b());assert.ok(x>=0&&x<1);}
 const root=new PveRng(7),h1=root.fork('horde'),h2=new PveRng(7).fork('horde');
 for(let i=0;i<50;i++)assert.equal(h1.next(),h2.next());
 const d=root.fork('director');d.next();d.next();assert.equal(root.fork('horde').next(),new PveRng(hash32(7,'horde')).next());
 const w=new PveRng(3),counts={a:0,b:0,c:0};for(let i=0;i<20000;i++)counts[w.weighted({a:1,b:3,c:0})]++;
 assert.equal(counts.c,0);assert.ok(Math.abs(counts.b/counts.a-3)<.25);
 assert.throws(()=>w.weighted({a:0}));
});
test('balance tables match the design and the owner decisions',()=>{
 assert.deepEqual([1,2,3,4].map(bossHp),[6000,9000,12000,15000],'S7 balance retune (design 6000/10200/14400/18600)');
 assert.deepEqual([1,2,3,4].map(damageScale),[.2,.22,.24,.26],'S7 balance retune (design .6/.7/.8/.9)');
 assert.deepEqual([1,2,3,4].map(h=>Math.round(partyFactor(h)*100)/100),[1,1.6,2.2,2.8]);
 assert.equal(PVE_SIREN.play.minRecast.laugh,0,'D35: no Last Laugh floor in phase 4');
 assert.equal(PVE_SIREN.play.cooldown,0);assert.equal(PVE_SIREN.play.maxRains,2);assert.equal(PVE_SIREN.play.maxEruptions,4);
 assert.equal(PVE.maxParty,4);assert.equal(PVE.maxRoomsPerServer,2);
 assert.deepEqual(OFFER_WEIGHTS.rainbow,{mythic:1});
 for(const tier of ['common','uncommon','rare','epic','legendary','mythic'] as const)assert.ok((OFFER_WEIGHTS[tier].mythic??0)>0,`${tier} can show max rarity`);
 assert.equal(Object.values(DROP_ODDS).reduce((a,b)=>a+b,0),100);
 assert.deepEqual([0,1,10].map(chipPrice),[175,200,350]);assert.deepEqual([0,1,9].map(revivePrice),[150,200,400]);   // owner money retune 2026-10-03
 assert.deepEqual(waveScale(1),IDENTITY_MODS);assert.equal(waveScale(3).hp,1.24);
 assert.deepEqual(combineMods(waveScale(2),{hp:2}),{...waveScale(2),hp:2*1.12});
 assert.equal(WAVES.length,PVE.wavesBeforeBoss);assert.equal(ADD_KINDS.shambler.hit,PVE_TIMING.swipeHit);assert.equal(ADD_KINDS.brute.hit,PVE_TIMING.smashHit);
});
test('layout: 16 entries, 8 machines with colliders clear of Ward Zero static blocks, 8 pads and the boss spawn',()=>{
 assert.equal(PVE_ENTRIES.length,16);assert.deepEqual(PVE_ENTRIES.map(e=>e.e),[...Array(16).keys()]);
 assert.deepEqual(PVE_ENTRIES.slice(0,8).map(e=>e.id),SIREN_VENTS.map(v=>v.id));
 assert.deepEqual(PVE_ENTRIES.slice(8).map(e=>e.id),['cell-wn','cell-ws','cell-en','cell-es','door-nw','door-ne','door-sw','door-se']);
 for(const e of PVE_ENTRIES.slice(8)){const s=SIREN_ADD_SPAWNS.find(a=>a.id===e.id)!;assert.equal(e.yaw,s.yaw);assert.ok(Math.hypot(e.exit.x-e.from.x,e.exit.z-e.from.z)>1.5,'walk-out distance');}
 for(const e of PVE_ENTRIES.filter(e=>e.kind==='door'))assert.ok(e.leaf&&Math.abs(e.leaf.z)<Math.abs(e.from.z)&&Math.abs(e.leaf.z)>Math.abs(e.exit.z),'leaf between the walk start and the exit (S7: rendered leaf plane)');
 for(const e of PVE_ENTRIES.filter(e=>e.kind==='vent'))assert.ok(Math.hypot(e.exit.x-e.from.x,e.exit.z-e.from.z)>1.4,'vent exit is past the pit edge');
 assert.equal(PVE_MACHINES.length,8);assert.deepEqual(PVE_MACHINES.map(m=>m.kind),['chip','chip','chip','chip','recruit','recruit','med','med']);
 const blocks=machineBlocks();assert.equal(blocks.length,8);assert.deepEqual(blocks.map(b=>b.id),PVE_MACHINES.map(m=>`pve-machine-${m.m}`));
 assert.ok(blocks.every(b=>b.material==='foundation'&&!b.dynamic&&b.size.y===2.2));
 const statics=arenaBlocks('siren').filter(b=>!b.dynamic&&b.pos.y-b.size.y/2<1.8&&b.pos.y+b.size.y/2>.35&&!b.id.includes('floor'));
 for(const m of PVE_MACHINES){const near=Math.min(...statics.map(b=>Math.max(Math.abs(m.x-b.pos.x)-b.size.x/2,Math.abs(m.z-b.pos.z)-b.size.z/2)));assert.ok(near>=1,`${m.id} clear of static blocks (${near.toFixed(2)})`);
  assert.ok(Math.abs(Math.atan2(-m.x,-m.z)-m.yaw)<1e-9,'faces the centre');}
 assert.equal(PVE_LAYOUT.pads.length,8);assert.deepEqual(PVE_LAYOUT.bossSpawn,{x:0,z:24.2,yaw:Math.PI});
 assert.ok(arenaBlocks('siren').length+blocks.length<=300,'relay prop cap');
});
test('Siren boss track: four siren-rarity chips outside CHIPS with existing icons',()=>{
 assert.deepEqual(SIREN_BOSS_CHIPS.map(c=>c.name),['The Siren is here...','Find me...','Am I beautiful?',"Fine then... I'll play"]);
 assert.deepEqual(SIREN_BOSS_CHIPS.map(c=>c.mechanic),['spawn','possess','grow','nocd']);
 for(const c of SIREN_BOSS_CHIPS){assert.equal(c.rarity,'siren');assert.deepEqual(c.tags,['siren-boss']);assert.deepEqual(c.effects,[]);assert.ok(ICON_PATHS[c.icon],c.icon);assert.ok(!CHIPS.some(x=>x.id===c.id));}
 assert.equal(SIREN_CHIPS.length,3);assert.equal(SIREN_BOSS.track,SIREN_BOSS_CHIPS);assert.deepEqual([...SIREN_BOSS.thresholds],[.75,.5,.25]);
});
test('every named fixture builds a deterministic, codec-valid PveView',()=>{
 assert.ok(PVE_FIXTURE_NAMES.length>=25);
 for(const name of PVE_FIXTURE_NAMES){
  const a=pveFixture(name,{time:50,seed:4}),b=pveFixture(name,{time:50,seed:4});
  assert.deepEqual(a,b,name);assert.equal(a.v,1);assert.equal(pveOf({pve:a}),a);
  for(const t of a.adds){const d=decodeAdd(t,a.slots);assert.deepEqual(encodeAdd(d,a.slots),t,name);}
  JSON.stringify(a);
 }
 assert.equal(pveFixture('horde-crowd').adds.length,60);
 assert.ok(pveFixture('reveal-2').cinematic?.chip===2&&pveFixture('outro-defeat').cinematic?.outcome==='defeat');
 assert.equal(pveFixture('possession').bossView?.hidden,true);
 assert.ok(fixtureHordeRing(8,{gore:false}).every(a=>a.lost===0));
 const boss=fixtureBossPlayer({id:'me',pos:{x:0,y:0,z:0}} as PlayerView,4);assert.equal(boss.siren?.boost?.cooldown,0);assert.equal(boss.id,'pve-boss');
});
/** Allowed only on a line that also carries the marker comment `pve-allow-sim-random` (design §2.7: reused bot brains such
 *  as botInput/botAbilities draw from sim.random(); a PvE line that deliberately calls into one must say so). */
const SIM_RANDOM_MARKER='pve-allow-sim-random';
test('PvE shared code never reads clocks, Math.random or sim.random (determinism, design §2.7)',()=>{
 const root=join(import.meta.dirname,'../src/shared/pve'),files:string[]=[];
 const walk=(dir:string)=>{for(const e of readdirSync(dir,{withFileTypes:true})){const p=join(dir,e.name);if(e.isDirectory())walk(p);else if(e.name.endsWith('.ts'))files.push(p);}};walk(root);
 assert.ok(files.length>=11);
 for(const file of files){
  const lines=readFileSync(file,'utf8').replace(/\/\*[\s\S]*?\*\//g,'').split('\n');
  lines.forEach((raw,i)=>{const allowed=raw.includes(SIM_RANDOM_MARKER),line=raw.replace(/\/\/.*$/,'');
   for(const bad of ['Math.random','Date.now','performance.now','crypto.'])assert.ok(!line.includes(bad),`${file}:${i+1} uses ${bad}`);
   assert.ok(allowed||!/\.random\(/.test(line),`${file}:${i+1} calls .random( (use host.rng(label))`);});
 }
});
test('PVE_EVENT names are unique pve-* strings',()=>{const names=Object.values(PVE_EVENT);assert.equal(new Set(names).size,names.length);assert.ok(names.every(n=>n.startsWith('pve-')));assert.equal(LIMB.rShin,256);});
test('client facade: renderer-shaped context type-checks, no-op without pve, boss hidden until foundAt+0.4',()=>{
 // Type-level: the renderer passes rigs:Map<string,Rig> and burst:(pos,color,count,power)=>void (compiled by check:types).
 type RigLike={group:T.Group;head:T.Group;rightArm:T.Group;weapon:T.Group;leftArm:T.Group;spec:string};
 const rigs=new Map<string,RigLike>();const ctx:PveVisualsContext={camera:new T.PerspectiveCamera(),me:'p1',dt:1/60,quality:1,compatibility:false,replay:false,rigs,burst:()=>{}};
 const scene=new T.Scene(),visuals=new PveVisuals(scene),rig:PveRigRef={group:new T.Group(),head:new T.Group(),rightArm:new T.Group(),weapon:new T.Group()};
 const pvp={time:1,players:[],events:[]} as unknown as Snapshot;
 visuals.update(pvp,ctx);assert.equal(visuals.decoratePlayer(rig,{id:'pve-boss'} as PlayerView,pvp),true);assert.equal(scene.children.length,0);
 const view=pveFixture('found',{time:20});const s={...pvp,time:20,pve:view} as Snapshot;
 visuals.update(s,ctx);assert.equal(visuals.metrics().adds,view.adds.length);assert.ok(visuals.metrics().fullRigs+visuals.metrics().nearImpostors<=view.adds.length);
 assert.equal(playerShown({id:'pve-boss'},view,view.bossView!.foundAt!+.39),false);assert.equal(playerShown({id:'pve-boss'},view,view.bossView!.foundAt!+.41),true);
 assert.equal(playerShown({id:'pve-boss'},pveFixture('possession'),100),false);
 const rec=pveFixture('economy',{time:30});assert.equal(playerShown({id:'recruit-1'},{...rec,recruits:[{...rec.recruits[0],at:31}]},30),false);
 visuals.clear();
});
