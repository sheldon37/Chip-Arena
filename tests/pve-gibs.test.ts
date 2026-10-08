// PvE add hits, limbs and gibs (design §3.6, stream S1): projectiles, piercing, headshots, limbs, crawl, blasts, melee
// specials, assists and Necronomicon zombies through assistMinion (D38).
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {v,type Vec} from '../src/shared/config.js';
import {WEAPONS,statsFor} from '../src/shared/content.js';
import {LIMB,decodeAdd,type PveOptions} from '../src/shared/pve/contract.js';
import {ADD_KINDS,GIB,HORDE} from '../src/shared/pve/balance.js';
import {PVE_ENTRIES} from '../src/shared/pve/layout.js';
import {Horde,ADD_STAND_Y} from '../src/shared/pve/horde.js';
import {classifyAddHit,jointWorld,limbState,partAt} from '../src/shared/pve/gibs.js';
import {PveRng} from '../src/shared/pve/rng.js';
import {fakeParts,withPveParts} from './helpers/pve-fakes.js';
await initPhysics();

function hordeSim(o:Partial<PveOptions>={},ids=['h1'],cls:'ranged'|'melee'='ranged'){
 const {factory,made}=fakeParts({horde:h=>new Horde(h)});
 return withPveParts(factory,()=>{
  const s=new Simulation({pve:{boss:'siren',seed:9,...o}});
  for(const id of ids){s.addPlayer(id,id,cls);s.pve!.command(id,{action:'hello',v:1},false);}
  s.phase='fight';s.fightStart=s.time;s.pve!.debug('god:on');   // god humans: adds stand still (no target)
  return {s,made,h:s.pve!.horde as Horde};
 });
}
const place=(p:Player,x:number,z:number,yaw=0)=>{p.body.setTranslation(v(x,.9,z),true);p.body.setLinvel(v(),true);p.input={...p.input,yaw,pitch:0};};
/** An active add standing at (x,z) facing yaw. */
function addAt(s:Simulation,h:Horde,x:number,z:number,kind:'shambler'|'runner'|'brute'='shambler',yaw=0){
 const n=h.spawn(kind,{entry:PVE_ENTRIES.findIndex(e=>e.id==='door-nw')})??h.spawn(kind)!;
 for(let i=0;i<300&&h.record(n)?.phase!=='active';i++)s.step();
 const r=h.record(n)!;r.minion!.body.setTranslation(v(x,ADD_STAND_Y,z),true);r.minion!.body.setLinvel(v(),true);r.yaw=yaw;s.world.step();return r;
}
const shoot=(s:Simulation,p:Player,pierces=0)=>{p.cooldown=0;p.ammo=10;p.reloadTime=0;s.spawnProjectile(p,1);for(const shot of s.projectiles.values())shot.pierces=pierces;for(let i=0;i<60&&s.projectiles.size;i++)s.stepProjectiles(1/60);};

test('classification: gibPartAt thresholds upright, crawler rules, blasts split, no info is torso',()=>{
 const a={pos:v(0,.9,0),yaw:0,crawl:false,lost:0,maxHp:60},rng=new PveRng(1);
 assert.equal(partAt(a,v(0,1.4,0)),'head');assert.equal(partAt(a,v(.3,1.1,0)),'lShoulder');assert.equal(partAt(a,v(-.3,.8,0)),'rElbow');
 assert.equal(partAt(a,v(.1,.5,0)),'lHip');assert.equal(partAt(a,v(-.1,.2,0)),'rKnee');assert.equal(partAt(a,v(0,.9,0)),'torso');
 const c={...a,crawl:true};assert.equal(partAt(c,v(0,.5,.5)),'head');assert.equal(partAt(c,v(.4,.5,.1)),'lShoulder');assert.equal(partAt(c,v(0,.5,-.2)),'torso');
 assert.deepEqual(classifyAddHit(a,10,undefined,rng),[{part:'torso',damage:10}]);
 const blast=classifyAddHit(a,100,{point:v(0,.9,0),kind:'blast',radius:3},rng);
 assert.ok(Math.abs(blast[0].damage-60)<1e-9&&blast[0].part==='torso');assert.ok(Math.abs(blast.reduce((n,x)=>n+x.damage,0)-100)<1e-9);
 const far=classifyAddHit(a,100,{point:v(3,.9,0),kind:"blast",radius:3},rng);assert.ok(Math.abs(far.reduce((n,x)=>n+x.damage,0)-100*(1-Math.min(.85,3/3.4)))<1e-9,"falloff");
 assert.deepEqual(limbState(LIMB.lLeg),{crawl:true,limp:false,armless:false,oneArm:false,lArm:false,rArm:false,headless:false});
 assert.equal(limbState(LIMB.lShin).limp,true);assert.equal(limbState(LIMB.lShin|LIMB.rShin).crawl,true);
});

test('projectiles kill adds and credit the shooter; a piercing shot passes through',()=>{
 const {s,h,made}=hordeSim();try{
  const p=s.players.get('h1')!;place(p,0,6,Math.PI);
  const a=addAt(s,h,0,3);s.world.step();
  for(let i=0;i<20&&h.record(a.n);i++)shoot(s,p);
  assert.equal(h.record(a.n),undefined,'killed');const k=made.director!.kills.at(-1)!;assert.equal(k.killer,'h1');assert.equal(k.n,a.n);
  s.step();const ev=s.events.find(e=>e.type==='pve-kill'&&e.target===a.id)!;assert.equal(ev.source,'h1');assert.equal(ev.amount,ADD_KINDS.shambler.reward);
  // Piercing: two adds in line both take the same shot.
  const b=addAt(s,h,0,3,'brute'),c=addAt(s,h,0,0,'brute');place(p,0,6,Math.PI);s.world.step();
  shoot(s,p,1);assert.ok(b.hp<b.maxHp,'first add hit');assert.ok(c.hp<c.maxHp,'pierced into the second add');
 }finally{s.dispose();}
});

test('a pistol headshot kills a fresh shambler (headless); limb loss gives lost bits, one gib event each, limp then crawl',()=>{
 const {s,h,made}=hordeSim();try{
  const p=s.players.get('h1')!;p.spec='pistol';p.stats=statsFor({},'pistol');place(p,0,10,Math.PI);
  const a=addAt(s,h,0,3);const head=v(0,ADD_STAND_Y+.55,3);
  s.chaos.hitMinion(a.minion!,WEAPONS.pistol.damage*p.stats.damage,'h1',WEAPONS.pistol.impulse,v(0,0,-1),{point:head,kind:'bullet'});
  assert.equal(h.record(a.n),undefined,'one headshot kills');const k=made.director!.kills.at(-1)!;assert.equal(k.headless,true);assert.equal(k.cause,'head');
  s.step();const corpse=s.snapshot().pve!.adds.find(t=>t[0]===a.n)!;const view=decodeAdd(corpse,[]);assert.equal(view.headless,true);assert.ok(view.lost&LIMB.head);assert.equal(view.state,'dead');
  // Legs: left shin → limp; right shin → crawl with the shrunken collider and ×0.35 speed.
  const b=addAt(s,h,0,-6,'shambler',0);const before=s.events.filter(e=>e.type==='pve-gib').length;
  const hitPart=(part:'lKnee'|'rKnee'|'lShoulder')=>{const pos=b.minion!.body.translation(),pt=jointWorld({pos,yaw:b.yaw,crawl:b.crawl},part);s.chaos.hitMinion(b.minion!,25,'h1',0,v(0,0,1),{point:pt,kind:'bullet'});};
  hitPart('lKnee');assert.equal(b.lost,LIMB.lShin);assert.equal(b.limp,true);assert.equal(b.crawl,false);
  hitPart('rKnee');assert.equal(b.lost,LIMB.lShin|LIMB.rShin);assert.equal(b.crawl,true);
  assert.ok(Math.abs(b.minion!.collider.halfHeight()-HORDE.collider.crawlHalfHeight)<1e-6);assert.ok(Math.abs(b.minion!.collider.radius()-HORDE.collider.crawlRadius)<1e-6);
  s.step();s.step();assert.equal(s.events.filter(e=>e.type==='pve-gib').length-before,2,'one gib event per loss');
  const gib=s.events.filter(e=>e.type==='pve-gib').at(-1)!;assert.equal(gib.target,b.id);assert.equal(gib.message,'rShin');
  assert.equal(decodeAdd(s.snapshot().pve!.adds.find(t=>t[0]===b.n)!,[]).state,'crawl');
  // Crawl speed: a full-HP crawler chasing a target beyond frenzy range walks at walk×0.35.
  b.hp=b.maxHp;s.pve!.debug('god:off');p.hp=p.stats.hp=1e7;place(p,0,8);let sp=0,count=0;
  for(let i=0;i<90;i++){s.step();place(p,0,8);if(i>45){const vel=b.minion!.body.linvel();sp+=Math.hypot(vel.x,vel.z);count++;}}
  assert.ok(Math.abs(sp/count-ADD_KINDS.shambler.walk*GIB.crawlSpeed)<.15,`crawl speed ${(sp/count).toFixed(2)}`);
 }finally{s.dispose();}
});

test('blasts fall off with distance; greatsword wolf-spin slam hits adds; assists need 20% of max HP',()=>{
 const {s,h,made}=hordeSim({},['h1','h2','h3'],'melee');try{
  const [p1,p2,p3]=['h1','h2','h3'].map(id=>s.players.get(id)!);place(p1,10,10);place(p2,12,10);place(p3,14,10);
  const near=addAt(s,h,-5,-5,'brute'),far=addAt(s,h,5,-5,'brute');
  s.explode(p1,v(-5,ADD_STAND_Y,-5),3,40,0,'blast');s.explode(p1,v(5-2.5,ADD_STAND_Y,-5),3,40,0,'blast');
  const dn=near.maxHp-near.hp,df=far.maxHp-far.hp;assert.ok(dn>0&&df>0);assert.ok(dn>2*df,`near ${dn.toFixed(1)} far ${df.toFixed(1)}`);
  // Wolf spin (greatsword special) lands on an add in front.
  p1.spec='greatsword';p1.class='melee';p1.stats=statsFor({},'greatsword');place(p1,0,8,Math.PI);
  const w=addAt(s,h,0,5.6,'brute');for(let i=0;i<10;i++){s.step();place(p1,0,8,Math.PI);}
  s.secondary(p1);assert.equal(p1.meleeAction?.kind,'wolf-spin');for(let i=0;i<240;i++)s.step();
  assert.ok((w.dealt.get('h1')??0)>0||!h.record(w.n),'the slam hit the add');
  // Assists: h2 deals 25%, h3 8%, h1 kills.
  const x=addAt(s,h,-8,0);s.chaos.hitMinion(x.minion!,15,'h2');s.chaos.hitMinion(x.minion!,5,'h3');s.chaos.hitMinion(x.minion!,1e3,'h1');
  const k=made.director!.kills.at(-1)!;assert.equal(k.killer,'h1');assert.deepEqual(k.assists,['h2']);assert.ok(s.pve!.stats('h2').addDamage>=15);
 }finally{s.dispose();}
});

test('a Necronomicon zombie kills an add through assistMinion; the kill is credited to its owner',()=>{
 const {s,h,made}=hordeSim();try{
  const p=s.players.get('h1')!;place(p,0,10);
  const a=addAt(s,h,3,0);s.chaos.summon(p,1,'zombie',v(0,1,0),30);
  const zombie=[...s.chaos.minions.values()].find(m=>m.kind==='zombie')!;assert.ok(zombie);
  for(let i=0;i<60*10&&h.record(a.n);i++)s.step();
  assert.equal(h.record(a.n),undefined,'the zombie killed the add');const k=made.director!.kills.at(-1)!;assert.equal(k.killer,'h1');assert.equal(k.n,a.n);
  assert.ok(s.chaos.minions.has(zombie.id),'the zombie is still alive');
 }finally{s.dispose();}
});
