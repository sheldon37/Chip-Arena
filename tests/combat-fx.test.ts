// Owner reports 2026-10-04: "Weapon impact effects seem to not work on the additional projectiles (and cap out)" and
// "Weapon effects + lifesteal isn't working on the minions of siren". Every projectile that lands on a Siren add now emits
// an impact and applies the weapon on-hit (burn/poison DoTs, life steal); chip extras/satellites/split fragments carry the
// weapon on-hit too (no chip procs); over-budget add hit events coalesce instead of dropping; adds keep DoTs and slows.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player,type GameEvent} from '../src/shared/simulation.js';
import {v,idleInput,type Input,type SpecId} from '../src/shared/config.js';
import {statsFor,WEAPONS,magazineCapacity} from '../src/shared/content.js';
import {ADD_STATUS,decodeAdd,encodeAdd} from '../src/shared/pve/contract.js';
import {Horde} from '../src/shared/pve/horde.js';
import {fakeParts,withPveParts} from './helpers/pve-fakes.js';
await initPhysics();

const near=(a:number,b:number,eps=1e-6,msg='')=>assert.ok(Math.abs(a-b)<=eps,`${msg} ${a} vs ${b}`);
function room(spec:SpecId,chips:Record<string,number>={},distance=6){
 const {factory}=fakeParts({horde:h=>new Horde(h)});
 return withPveParts(factory,()=>{
  const s=new Simulation({pve:{boss:'siren',seed:7}});s.addPlayer('h1','h1','ranged');s.pve!.command('h1',{action:'hello',v:1},false);s.phase='fight';s.fightStart=s.time;
  const p=s.players.get('h1')!,h=s.pve!.horde as Horde;p.spec=spec;p.class=WEAPONS[spec].class;p.chips=chips;p.stats=statsFor(chips,spec);p.stats.hp=5000;p.hp=1000;p.mana=p.stats.maxMana;p.stamina=p.stats.maxStamina;p.ammo=magazineCapacity(spec,p.stats);
  place(p);const n=h.spawn('brute',{entry:4})!;for(let i=0;i<600&&h.record(n)?.phase!=='active';i++){p.inputAt=s.time;s.step();}
  const add=h.record(n)!;assert.equal(add.phase,'active');add.hp=add.maxHp=40000;add.minion!.hp=add.minion!.maxHp=40000;
  return {s,p,h,add,n,park:()=>{place(p);add.minion!.body.setTranslation(v(0,1,distance),true);add.minion!.body.setLinvel(v(),true);add.ext={x:0,z:0};s.world.step();p.grounded=true;}};
 });
}
const place=(p:Player)=>{p.body.setTranslation(v(0,.9,0),true);p.body.setLinvel(v(),true);p.input={...p.input,yaw:0,pitch:0};p.ext=v();};
const lastId=(s:Simulation)=>s.events.at(-1)?.id??0;
const sum=(s:Simulation,from:number,type:string,f:(e:GameEvent)=>boolean=()=>true)=>s.events.filter(e=>e.type===type&&e.id>from&&f(e)).reduce((n,e)=>n+(e.amount||0),0);
const count=(s:Simulation,from:number,type:string,f:(e:GameEvent)=>boolean=()=>true)=>s.events.filter(e=>e.type===type&&e.id>from&&f(e)).length;
/** One tick at a time so no event leaves the 80-event ring unseen; returns the add HP lost, heals, impacts and hit events. */
function drive(r:ReturnType<typeof room>,n:number,input:(k:number)=>Partial<Input>,noDot=false){
 const {s,p,add}=r,hp0=add.hp;let seq=p.input.seq,healed=0,impacts=0,hits=0,addHits=0;
 for(let k=0;k<n;k++){const from=lastId(s);s.setInput('h1',{...idleInput(),seq:++seq,yaw:0,pitch:p.input.pitch,...input(k)});p.inputAt=s.time;r.add.minion?.body.setLinvel(v(),true);if(noDot){add.burns?.clear();add.poisons?.clear();}s.step();
  healed+=sum(s,from,'heal',e=>e.target==='h1');impacts+=count(s,from,'impact',e=>e.source==='h1');addHits+=sum(s,from,'minionHit',e=>e.target===add.id);}
 return {dealt:hp0-add.hp,healed,impacts,hits,addHits};
}
const aim=(r:ReturnType<typeof room>)=>{const a=r.add.minion!.body.translation(),e=r.p.body.translation();return Math.atan2(a.y-.1-(e.y+.5),Math.hypot(a.x-e.x,a.z-e.z));};
const shots=(s:Simulation)=>[...s.projectiles.values()].filter(q=>q.owner==='h1'&&!q.ability);

for(const [spec,chips,label] of [['pistol',{multishot:3,'vampiric-rounds':1},'pistol multishot'],['rifle',{buckshot:1,'vampiric-rounds':1},'rifle buckshot'],['machinegun',{multishot:2,'vampiric-rounds':1},'machinegun multishot'],['water',{torrent:1,'vampiric-rounds':1},'water torrent']] as const)
 test(`${label}: every projectile on an add shows an impact, heals and every hit is reported`,()=>{
  const r=room(spec,chips,4);try{r.park();const pitch=aim(r);let launched=0;const seen=new Set<string>();
   const out=drive(r,70,k=>{for(const q of shots(r.s))if(!seen.has(q.id)){seen.add(q.id);launched++;}return {attack:k<1,pitch,camera:'first'};},true);
   assert.ok(launched>=3,`${label} volley ${launched}`);assert.ok(out.dealt>0,'volley hit');
   assert.ok(out.impacts>=3,`impacts ${out.impacts} for ${launched} projectiles`);
   near(out.healed,out.dealt*.08,1e-6,'life steal on every projectile');
   near(out.addHits,out.dealt,1e-6,'coalesced add hit events report the whole damage (none dropped)');
  }finally{r.s.dispose();}
 });

test('sustained machinegun multishot: add hit events never drop (coalesced), impacts match projectile hits',()=>{
 const r=room('machinegun',{multishot:3},4);try{r.park();const pitch=aim(r);r.p.stats.attackRate=4;r.p.ammo=999;
  const out=drive(r,180,k=>({attack:k%2===0,pitch,camera:'first',reload:false}));
  assert.ok(out.impacts>=30,`impacts ${out.impacts}`);near(out.addHits,out.dealt,1e-6,'all damage reported');assert.equal(r.h.stats.eventsDropped,0);
 }finally{r.s.dispose();}
});

test('Ember / Venom pistol: burn and poison DoTs tick on the add, credited to the shooter, and show in the add view',()=>{
 const r=room('pistol',{ember:2,poison:2},5);try{r.park();const pitch=aim(r);const hit=drive(r,12,k=>({attack:k<1,pitch,camera:'first'}));assert.ok(hit.dealt>0);
  assert.ok(r.add.burns?.get('h1')?.dps===6,'burn dps 6');assert.ok(r.add.poisons?.get('h1')?.dps===5,'poison dps 5');
  const view=decodeAdd(r.h.views([]).adds.find(t=>t[0]===r.n)!,[]);assert.equal(view.st,ADD_STATUS.burn|ADD_STATUS.poison);
  const hp=r.add.hp;const later=drive(r,60,()=>({}));assert.ok(later.dealt>5,`DoT damage ${later.dealt}`);assert.ok(r.add.hp<hp);
  assert.ok((r.add.dealt.get('h1')??0)>=hit.dealt+later.dealt-1e-6,'DoT credited to h1');
  drive(r,300,()=>({}));const after=decodeAdd(r.h.views([]).adds.find(t=>t[0]===r.n)!,[]);assert.equal(after.st,undefined,'statuses expire');
 }finally{r.s.dispose();}
});

test('Inferno sword swing burns the add and heals with Leech',()=>{
 const r=room('sword',{inferno:1,leech:1},2.2);try{r.park();const out=drive(r,40,k=>({attack:k<2}),true);assert.ok(out.dealt>0);near(out.healed,out.dealt*.12,1e-6,'leech');r.park();r.p.cooldown=0;r.p.stamina=r.p.stats.maxStamina;r.s.time+=2;drive(r,40,k=>({attack:k<2}));assert.ok((r.add.burns?.get('h1')?.dps??0)>=10,'burning');}finally{r.s.dispose();}
});

test('thrown dagger with Venom poisons the add',()=>{
 const r=room('daggers',{poison:1,leech:1},5);try{r.park();const pitch=aim(r);const out=drive(r,60,k=>({secondary:k<2,pitch}));assert.ok(out.dealt>0);assert.ok(r.add.poisons?.has('h1'),'poisoned');}finally{r.s.dispose();}
});

test('Helix / Orbitals / Splitter secondary shots carry the weapon burn and life steal onto adds',()=>{
 for(const chip of ['helix','orbitals','splitter']){
  const r=room('pistol',{[chip]:1,ember:1,'vampiric-rounds':1},4);try{r.park();const pitch=aim(r);
   r.s.step();const extra=()=>shots(r.s).filter(q=>!q.direct);let carried=false;
   drive(r,40,k=>{for(const q of extra())if(q.weaponFx&&(q.burnDps??0)>0)carried=true;return {attack:k<1,pitch,camera:'first'};});
   assert.ok(carried,`${chip} secondary shots carry the weapon burn`);
  }finally{r.s.dispose();}
 }
});

test('water soak and an ability slow reach adds and slow their chase',()=>{
 const r=room('water',{},6);try{r.park();const m=r.add.minion!;assert.equal(r.s.abilities.slow(m,.6,1,'test','h1'),true);
  assert.ok((r.add.slowUntil??0)>r.s.time);near(r.add.slowFactor!,.6);const view=decodeAdd(r.h.views([]).adds.find(t=>t[0]===r.n)!,[]);assert.equal(view.st,ADD_STATUS.slow);
 }finally{r.s.dispose();}
});

test('flame lance burns an add',()=>{
 const r=room('fire',{},4);try{r.park();const m=r.add.minion!;r.s.minionStatus('h1',m,{burn:4});assert.equal(r.add.burns?.get('h1')?.dps,4);}finally{r.s.dispose();}
});

test('add status bits round-trip through the compact tuple without changing its length',()=>{
 const base={n:3,id:'pve-add-3',pos:{x:1,y:1,z:1},yaw:0,vel:{x:0,z:0},hp:.5,kind:'brute' as const,state:'active' as const,attack:'smash' as const,side:1 as const,lost:0,entry:-1,headless:false};
 for(const st of [undefined,1,2,4,7]){const t=encodeAdd({...base,...(st?{st}:{}),target:'a'},['x','a']);assert.equal(t.length,15);const d=decodeAdd(t,['x','a']);assert.equal(d.st,st);assert.equal(d.target,'a');assert.equal(d.kind,'brute');}
});

test('PvP: Helix extras with Ember burn the target player and heal with life steal',()=>{
 const s=new Simulation();try{s.addPlayer('a','a','ranged');s.addPlayer('b','b','ranged');s.phase='fight';s.fightStart=s.time;
  const a=s.players.get('a')!,b=s.players.get('b')!;a.spec='pistol';a.chips={helix:1,ember:1,'vampiric-rounds':1};a.stats=statsFor(a.chips,'pistol');a.hp=50;b.stats.hp=5000;b.hp=5000;
  a.body.setTranslation(v(0,.9,0),true);b.body.setTranslation(v(0,.9,6),true);s.world.step();
  s.spawnProjectile(a);const extras=[...s.projectiles.values()].filter(q=>!q.direct);assert.ok(extras.length>=2,'helix extras');
  assert.ok(extras.every(q=>q.weaponFx&&(q.burnDps??0)>0&&q.elements?.includes('fire')),'extras carry the weapon on-hit');
  const q=extras[0];for(const other of [...s.projectiles.keys()])if(other!==q.id)s.projectiles.delete(other);
  q.pos=v(0,.9,5.3);q.vel=v(0,0,60);const hp0=b.hp,heal0=a.hp;for(let k=0;k<6&&s.projectiles.has(q.id);k++){b.inputAt=a.inputAt=s.time;s.step();}
  const dealt=hp0-b.hp;assert.ok(dealt>0,'extra hit');assert.ok(b.burns.has('a'),'burning');assert.ok(a.hp>heal0,'life steal');
 }finally{s.dispose();}
});
