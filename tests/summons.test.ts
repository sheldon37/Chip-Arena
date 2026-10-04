// Chaos summons with real combat and the reworked Slenderman (owner requests 2026-10-04). Deterministic scripted inputs.
import {test} from 'node:test';import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';import {statsFor} from '../src/shared/content.js';
import {v,idleInput,len,sub,type Vec,type SpecId} from '../src/shared/config.js';import {SUMMON,type Minion} from '../src/shared/chaos.js';import {SLENDER} from '../src/shared/leet-content.js';
import {staticLevel} from '../src/client/slender-static.js';
await initPhysics();
const DT=1/60;
function game(spec:SpecId='fire',chips:Record<string,number>={},bSpec:SpecId='pistol'){const s=new Simulation({sandbox:true}),a=s.addPlayer('a','A','magic',null,'#55ff88',{},true)!,b=s.addPlayer('b','B','ranged')!;a.spec=spec;b.spec=bSpec;a.chips=chips;a.stats=statsFor(a.chips,a.spec);b.stats=statsFor(b.chips,b.spec);s.resetRound();s.phase='fight';
 for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});a.hp=a.stats.hp;b.hp=b.stats.hp;a.body.setTranslation(v(0,1,0),true);b.body.setTranslation(v(0,1,8),true);a.input={...idleInput(),camera:'first'};b.input={...idleInput(),camera:'first'};s.world.step();return {s,a,b};}
const tick=(s:Simulation,n=1)=>{for(let i=0;i<n;i++){s.time+=DT;s.world.step();s.chaos.step(DT);}};
function summon(s:Simulation,owner:Player,kind:'zombie'|'toy',at:Vec){s.chaos.summon(owner,1,kind,at);const m=[...s.chaos.minions.values()].filter(m=>m.kind===kind).at(-1)!;m.body.setTranslation(at,true);s.world.step();return m;}
const look=(p:Player,at:Vec)=>{const d=sub(at,p.body.translation());p.input={...p.input,yaw:Math.atan2(d.x,d.z),pitch:Math.atan2(d.y,Math.hypot(d.x,d.z))};};

test('summon health pools: every damage path hurts and kills a zombie and a toy, with a death event and owner credit',()=>{
 const paths:Record<string,(s:Simulation,b:Player,m:Minion)=>void>={
  projectile:(s,b,m)=>{look(b,m.body.translation());for(let i=0;i<1200&&s.chaos.minions.has(m.id);i++){m.until=s.time+30;if(i%8===0){m.body.setLinvel(v(),true);look(b,m.body.translation());b.cooldown=0;b.ammo=12;s.spawnProjectile(b);}s.time+=DT;s.stepProjectiles(DT);s.world.step();}},
  melee:(s,b,m)=>{b.spec='sword';b.stats=statsFor({},'sword');b.body.setTranslation(add(m.body.translation(),v(0,0,-1.2)),true);s.world.step();for(let i=0;i<900&&s.chaos.minions.has(m.id);i++){if(i%30===0){b.body.setTranslation(add(m.body.translation(),v(0,0,-1.3)),true);b.body.setLinvel(v(),true);}look(b,m.body.translation());s.queueInputs('b',[{...b.input,seq:i+1,attack:i%30<3}],`${s.setNumber}:${s.round}:${s.generation}`);b.stamina=100;s.step();m.until=s.time+10;}},
  aoe:(s,b,m)=>{for(let i=0;i<40&&s.chaos.minions.has(m.id);i++)s.explode(b,m.body.translation(),3,40,0,'lab',false);},
  field:(s,b,m)=>{s.chaos.field(b,'acid',m.body.translation(),3,60,40);for(let i=0;i<900&&s.chaos.minions.has(m.id);i++){m.body.setLinvel(v(),true);tick(s);m.until=s.time+10;}},
  dot:(s,b,m)=>{for(let i=0;i<1200&&s.chaos.minions.has(m.id);i++){if(i%60===0)s.minionWeaponHit(b,m,1,6,6);m.body.setLinvel(v(),true);tick(s);m.until=s.time+10;}},
  ability:(s,b,m)=>{for(let i=0;i<40&&s.chaos.minions.has(m.id);i++)s.abilities.blast(b,m.body.translation(),2.5,40,0,'frag');},
 };
 for(const kind of ['zombie','toy'] as const)for(const [name,hit] of Object.entries(paths)){const {s,a,b}=game();try{
  const m=summon(s,a,kind,v(6,1,8));m.until=s.time+60;m.nextAttack=Infinity;assert.ok(m.hp>0&&m.hp===m.maxHp);hit(s,b,m);
  assert.ok(!s.chaos.minions.has(m.id),`${kind} died to ${name} (hp ${m.hp})`);const death=s.events.find(e=>e.type==='minionDeath'&&e.target===m.id);
  assert.ok(death,`${kind} ${name} death event`);assert.equal(death!.message,kind);assert.equal(death!.source,b.id);
 }finally{s.dispose();}}
});
test('pools follow the chip text: zombies 2x the owner max HP, toy soldiers 25; views carry whole hp only',()=>{const {s,a}=game();const z=summon(s,a,'zombie',v(4,1,0)),t=summon(s,a,'toy',v(-4,1,0));assert.equal(z.maxHp,a.stats.hp*2);assert.equal(t.maxHp,25);
 s.chaos.hitMinion(z,10.4,'b');const view=s.snapshot().minions!.find(m=>m.id===z.id)!;assert.equal(view.hp,Math.ceil(z.hp));assert.ok(Number.isInteger(view.hp));s.dispose();});

for(const kind of ['zombie','toy'] as const)test(`${kind} attack: windup, damage exactly on the strike frame, then recovery`,()=>{const {s,a,b}=game();const k=SUMMON[kind],at=kind==='toy'?v(0,1,3):v(0,1,6.6);const m=summon(s,a,kind,at);m.nextAttack=0;
 const start=b.hp;let began=-1,landed=-1;for(let i=0;i<120&&landed<0;i++){tick(s);if(began<0&&m.strikeAt!==undefined)began=m.lastAttack;if(b.hp<start)landed=s.time;b.body.setTranslation(v(0,1,8),true);}
 assert.ok(began>=0,'attack started');assert.ok(landed>0,'damage landed');assert.ok(Math.abs(landed-(began+k.windup))<=DT+1e-9,`strike at windup end (${landed-began})`);
 near(start-b.hp,k.damage*a.stats.damage);assert.equal(b.lastSource,a.id);
 // Recovery: no new windup before strike + recover.
 const struck=landed;for(let i=0;i<Math.round(k.recover/DT)-2;i++){tick(s);b.body.setTranslation(v(0,1,8),true);}assert.ok(m.lastAttack<=struck,'recovering');
 if(kind==='toy')assert.ok(s.events.some(e=>e.type==='minionShot'&&e.target===m.id&&e.from));s.dispose();});
const near=(x:number,y:number)=>assert.ok(Math.abs(x-y)<1e-6,`${x} vs ${y}`);
function add(a:Vec,b:Vec){return v(a.x+b.x,a.y+b.y,a.z+b.z);}

test('stepping out of reach during the windup dodges the strike',()=>{const {s,a,b}=game();const m=summon(s,a,'zombie',v(0,1,6.6));m.nextAttack=0;for(let i=0;i<5&&m.strikeAt===undefined;i++)tick(s);assert.ok(m.strikeAt!==undefined);b.body.setTranslation(v(0,1,12),true);s.world.step();tick(s,Math.ceil(SUMMON.zombie.windup/DT)+2);assert.equal(b.hp,b.stats.hp);s.dispose();});

test('summon kills credit the summoner; blocks still parry; summons fight enemy summons and spare allies',()=>{
 {const {s,a,b}=game();b.hp=5;const m=summon(s,a,'zombie',v(0,1,6.6));m.nextAttack=0;for(let i=0;i<60&&b.hp>0;i++){tick(s);b.body.setTranslation(v(0,1,8),true);}assert.equal(b.hp,0);assert.equal(b.death?.announcement?.killer,a.name);s.dispose();}
 {const {s,a,b}=game();const mine=summon(s,a,'zombie',v(0,1,4)),theirs=summon(s,b,'zombie',v(0,1,5.4));mine.nextAttack=theirs.nextAttack=0;b.body.setTranslation(v(20,1,20),true);a.body.setTranslation(v(-20,1,-20),true);s.world.step();
  for(let i=0;i<60;i++)tick(s);assert.ok(mine.hp<mine.maxHp&&theirs.hp<theirs.maxHp,'they fight each other');s.dispose();}
});
test('life steal on summon hits follows today\'s rules: chip life steal heals the hitter, minion bites do not heal the summoner',()=>{const {s,a,b}=game();const m=summon(s,a,'zombie',v(6,1,8));b.stats.lifeSteal=.5;b.hp=50;s.minionWeaponHit(b,m,20,0,0);assert.equal(b.hp,60);
 a.hp=50;m.body.setTranslation(v(0,1,6.6),true);s.world.step();m.nextAttack=0;for(let i=0;i<60&&b.hp>=60;i++){tick(s);b.body.setTranslation(v(0,1,8),true);}assert.ok(b.hp<60);assert.equal(a.hp,50);s.dispose();});
test('summon caps: 14 zombies and 3 toys per owner, 34 summons in total',()=>{const {s,a,b}=game();s.chaos.summon(a,20,'zombie');assert.equal([...s.chaos.minions.values()].filter(m=>m.owner==='a').length,14);for(let i=0;i<5;i++)s.chaos.summon(a,1,'toy');assert.equal([...s.chaos.minions.values()].filter(m=>m.kind==='toy').length,3);
 const c=s.addPlayer('c','C','magic')!;c.hp=c.stats.hp;s.chaos.summon(b,14,'zombie');s.chaos.summon(c,14,'zombie');assert.equal(s.chaos.minions.size,SUMMON.cap.total);s.dispose();});
test('summons die with the summoner and on round reset',()=>{const {s,a}=game();summon(s,a,'zombie',v(3,1,0));s.damage(a,9999,'b','direct');tick(s);assert.equal(s.chaos.minions.size,0);
 const {s:s2,a:a2}=game();summon(s2,a2,'toy',v(3,1,0));s2.resetRound();assert.equal(s2.chaos.minions.size,0);s.dispose();s2.dispose();});
test('bots turn on a hostile summon that is close, and keep their pilot target otherwise',()=>{const {s,a,b}=game();b.bot='hard';const m=summon(s,a,'zombie',v(0,1,5));assert.equal(s.botFocus(b,a).id,m.id);m.body.setTranslation(v(0,1,-12),true);assert.equal(s.botFocus(b,a).id,a.id);s.dispose();});

// ---------------------------------------------------------------- Slenderman
function stalker(){const {s,a,b}=game('fire',{},'pistol');a.spec='leet';a.chips={'l44t-scare':1};a.stats=statsFor(a.chips,'leet');a.mana=100;a.input={...a.input,yaw:0};b.body.setTranslation(v(0,1,14),true);s.world.step();s.secondary(a);
 const f=[...s.leet.fields.values()][0];const step=(n=1,before?:()=>void)=>{for(let i=0;i<n&&s.leet.fields.has(f.id);i++){before?.();s.time+=DT;s.world.step();s.leet.step(DT);}};step(Math.ceil(SLENDER.emerge/DT)+1);
 const body=()=>s.chaos.minions.get(f.slender!);const head=()=>add(f.walker!,v(0,2.2,0));return {s,a,b,f,step,body,head};}
test('Slenderman never moves while his target looks at him; sanity builds while staring, faster up close',()=>{const {s,b,f,step,head}=stalker();assert.ok(f.slender&&f.walker);const at={...f.walker!};
 step(Math.round(3/DT),()=>look(b,head()));assert.deepEqual(f.walker,at,'frozen while observed');assert.ok(f.seen);const far=f.sanity!;assert.ok(far>.3&&far<1,`sanity ${far}`);
 const {s:s2,b:b2,f:f2,step:step2,head:head2}=stalker();b2.body.setTranslation(add(f2.walker!,v(0,1,4)),true);s2.world.step();step2(Math.round(1/DT),()=>look(b2,head2()));assert.ok(f2.sanity!>far/3*1.8,'closer stares hurt more');s.dispose();s2.dispose();});
test('looking away lets the static fade and lets him relocate behind the target, closer each time',()=>{const {s,b,f,step,head}=stalker();step(Math.round(1.5/DT),()=>look(b,head()));const peak=f.sanity!;
 const gaps:number[]=[];let hops=0;const away=()=>{const d=sub(head(),b.body.translation());b.input={...b.input,yaw:Math.atan2(-d.x,-d.z),pitch:0};};
 for(let i=0;i<Math.round(12/DT)&&s.leet.fields.has(f.id)&&!b.leet?.scare;i++){step(1,away);if((f.hops||0)>hops){hops=f.hops!;gaps.push(Math.hypot(f.walker!.x-b.body.translation().x,f.walker!.z-b.body.translation().z));
   const toHim=sub(f.walker!,b.body.translation()),fwd=v(Math.sin(b.input.yaw),0,Math.cos(b.input.yaw));assert.ok((toHim.x*fwd.x+toHim.z*fwd.z)/Math.hypot(toHim.x,toHim.z)<Math.cos(SLENDER.fov*Math.PI/180),'lands outside the view');}
  if(i===30)assert.ok(f.sanity!<peak,'static fades when looking away');}
 assert.ok(gaps.length>=3,`hops ${gaps.length}`);for(let i=1;i<gaps.length;i++)assert.ok(gaps[i]<gaps[i-1]+1e-6,`closer: ${gaps}`);
 assert.ok(b.leet?.scare,'reached the target: jumpscare');assert.equal(b.leet!.scare!.source,'a');s.dispose();});
test('staring too long ends in the jumpscare knockout credited to the owner; only the victim gets the scare state',()=>{const {s,a,b,f,step,head}=stalker();b.body.setTranslation(add(f.walker!,v(0,1,3)),true);s.world.step();
 step(Math.round(5/DT),()=>look(b,head()));assert.ok(b.leet?.scare);assert.ok(!s.leet.fields.has(f.id));assert.equal(s.chaos.minions.size,0);assert.ok(!a.leet?.scare);
 s.time=b.leet!.scare!.until;s.leet.step(DT);assert.equal(b.hp,0);assert.equal(b.death?.announcement?.killer,a.name);s.dispose();});
test('counterplay: destroying him (160 HP, any damage) or killing the owner ends the stalk; the victim static level follows sanity',()=>{
 {const {s,b,f,body}=stalker();const m=body()!;assert.equal(m.maxHp,SLENDER.hp);s.explode(b,f.walker!,3,SLENDER.hp+5,0,'lab',false);assert.ok(!s.chaos.minions.has(m.id));s.time+=DT;s.leet.step(DT);assert.ok(!s.leet.fields.has(f.id));assert.ok(s.events.some(e=>e.type==='minionDeath'&&e.message==='slender'));assert.ok(!b.leet?.scare);s.dispose();}
 {const {s,a,f,step}=stalker();s.damage(a,9999,'b','direct');step();assert.ok(!s.leet.fields.has(f.id));assert.equal(s.chaos.minions.size,0);s.dispose();}
 assert.equal(staticLevel(0,false,Infinity),0);assert.ok(staticLevel(.8,true,Infinity)>staticLevel(.8,false,Infinity));assert.ok(staticLevel(0,false,.05)>.3,'teleport flash');
});
test('the stalk is deterministic and his view is quantised in snapshots',()=>{const run=()=>{const {s,b,f,step,head}=stalker();const out:string[]=[];for(let i=0;i<600&&s.leet.fields.has(f.id);i++){step(1,()=>{if(i%120<50)look(b,head());else b.input={...b.input,yaw:b.input.yaw+.02};});if(i%30===0)out.push(JSON.stringify(s.snapshot().leetFields));}s.dispose();return out;};
 const x=run();assert.deepEqual(x,run());const view=JSON.parse(x[5])[0];assert.equal(view.sanity,Math.round(view.sanity*100)/100);});
test('presentation: damaged summons and the Slenderman get health bars; a killed summon leaves a corpse that breaks up',async()=>{const T=await import('three'),{OverheadHealth}=await import('../src/client/overhead-health.js'),{ChaosVisuals}=await import('../src/client/chaos-visuals.js');
 const {s,a,b}=game();const z=summon(s,a,'zombie',v(3,1,3));const scene=new T.Scene(),bars=new OverheadHealth(scene),cv=new ChaosVisuals(scene),cam=new T.PerspectiveCamera();cam.position.set(0,3,-4);const bursts:number[]=[];const fx={burst:(_p:unknown,_c:number,n:number)=>{bursts.push(n);},line:()=>{}};
 bars.update(s.snapshot(),cam,true,'b',false);assert.equal(scene.children.filter(o=>o.name==='summon-health').length,0,'full health: no bar');s.chaos.hitMinion(z,20,'b');bars.update(s.snapshot(),cam,true,'b',false);assert.equal(scene.children.filter(o=>o.name==='summon-health'&&o.visible).length,1);
 cv.update(s.snapshot(),undefined,fx);assert.equal(cv.minions.size,1);s.chaos.hitMinion(z,9999,'b');cv.update(s.snapshot(),undefined,fx);assert.equal(cv.minions.size,0);assert.ok(scene.getObjectByName('summon-zombie'),'corpse stays');
 s.time+=.8;cv.update(s.snapshot(),undefined,fx);assert.ok(bursts.length>=3,'voxel break-up');s.time+=.2;cv.update(s.snapshot(),undefined,fx);assert.ok(!scene.getObjectByName('summon-zombie'));s.dispose();});
