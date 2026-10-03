// GUNS (design §4.1, §5, §14, §16.2 G): ADS profiles, Frag Grenade, Underbarrel Rocket, Orbital Mark and the gun bots,
// all through the real Simulation (gating, ability shot stepper, ability fields, bot hooks).
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player,type Projectile} from '../src/shared/simulation.js';
import {CONFIG as C,idleInput,v,len,sub,add,mul,dot,norm,type Input,type SpecId,type Vec} from '../src/shared/config.js';
import {WEAPONS} from '../src/shared/content.js';
import {kitFor,abilityActive,shotBallistics,adsSpread,ABILITY_DEFS} from '../src/shared/abilities.js';
import {ABILITY_HANDLERS} from '../src/shared/ability-system.js';
import {GUN_KITS,ADS_PROFILES} from '../src/shared/gun-kit.js';
import {botAbilities} from '../src/shared/bot-abilities.js';
import {ballisticPitch} from '../src/shared/projectiles.js';
import {VIEW} from '../src/shared/view.js';
import {rollInvulnerable} from '../src/shared/souls-combat.js';
await initPhysics();

const GUNS=['pistol','machinegun','rifle'] as const;
const near=(a:number,b:number,eps=1e-9,msg='')=>assert.ok(Math.abs(a-b)<=eps,`${msg} ${a} vs ${b}`);
/** Flat 64×56 m floor; `a` (spec) at `from` facing +z, `b` at `at`. */
function game(spec:SpecId,at=v(0,.9,8),from=v(0,.9,0)){
 const s=new Simulation({sandbox:true}),a=s.addPlayer('a','A','ranged')!,b=s.addPlayer('b','B')!;a.spec=spec;s.resetRound();s.phase='fight';
 for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});
 a.body.setTranslation(from,true);b.body.setTranslation(at,true);s.world.step();for(const p of [a,b]){p.grounded=true;p.inputAt=s.time;}a.input.yaw=0;a.input.pitch=0;return {s,a,b};
}
let seq=0;
/** One tick: `a` gets `input` (its yaw/pitch kept, first person), `b` gets `other`. */
function tick(s:Simulation,a:Player,input:Partial<Input>={},other:Partial<Input>={}){
 const b=s.players.get('b')!;s.setInput(a.id,{...idleInput(),seq:++seq,yaw:a.input.yaw,pitch:a.input.pitch,camera:'first',...input});
 s.setInput(b.id,{...idleInput(),seq:++seq,yaw:b.input.yaw,pitch:b.input.pitch,...other});for(const p of s.players.values())p.inputAt=s.time;s.step();
}
const run=(s:Simulation,a:Player,n:number,input:Partial<Input>={},other:Partial<Input>={})=>{for(let i=0;i<n;i++)tick(s,a,input,other);};
const eye=(p:Player)=>add(p.body.translation(),v(0,VIEW.eyeHeight,0));
/** Points `p`'s crosshair at `to` (the eye ray). */
function aimAt(p:Player,to:Vec){const d=sub(to,eye(p));p.input.yaw=Math.atan2(d.x,d.z);p.input.pitch=Math.atan2(d.y,Math.hypot(d.x,d.z));}
const shotOf=(s:Simulation,id:string)=>[...s.projectiles.values()].find(q=>q.ability?.id===id);
const blasts=(s:Simulation,fx:string)=>s.events.filter(e=>e.type==='explosion'&&e.message===fx);
/** Ticks until `done`; `b` holds Block while the shot `id` is within `reach` m of it. */
function guarded(s:Simulation,a:Player,id:string,reach:number,done:()=>boolean,max=150){const b=s.players.get('b')!;for(let n=0;n<max&&!done();n++){const q=shotOf(s,id);tick(s,a,{},q&&len(sub(q.pos,b.body.translation()))<reach?{block:true}:{});}}
/** Runs until `done` or `max` ticks; returns the tick count. */
function until(s:Simulation,a:Player,done:()=>boolean,max=240,other:Partial<Input>={}){let n=0;while(n<max&&!done()){tick(s,a,{},other);n++;}return n;}

// ---- kit rows -------------------------------------------------------------------------------------------------------
test('kit: gun E rows are live with handlers and §5 numbers; weapon rows name the new E; ADS profiles keep ballistics',()=>{
 const rows={pistol:['frag-grenade',8,.2,.3,60,4.2,1000],machinegun:['rocket',9,.12,.35,55,3.6,1300],rifle:['orbital-mark',13,.15,.35,85,3,1400]} as const;
 for(const spec of GUNS){const k=kitFor(spec)!,[id,cd,windup,lockout,damage,radius,impulse]=rows[spec];
  assert.deepEqual([k.e.id,k.e.cooldown,k.e.windup,k.e.lockout,k.e.damage,k.e.radius,k.e.impulse,k.e.cost],[id,cd,windup,lockout,damage,radius,impulse,0]);
  assert.equal(k.e.live,true);assert.ok(ABILITY_HANDLERS[id]?.release,id);assert.equal(abilityActive(spec,'e'),true);
  assert.equal(WEAPONS[spec].secondary,id);assert.equal(WEAPONS[spec].secondaryCooldown,cd);assert.ok(WEAPONS[spec].description.includes(k.e.name),spec);
  assert.ok(!/braced|burst/.test(WEAPONS[spec].description),spec);assert.equal(k.q.kind,'ads');assert.equal(k.ads,ADS_PROFILES[spec]);}
 assert.deepEqual(GUN_KITS.pistol.e.bot,{min:6,max:22,aim:'point',lead:.6});assert.deepEqual(GUN_KITS.machinegun.e.bot,{min:4,max:35,aim:'ballistic'});
 assert.deepEqual({...GUN_KITS.rifle.e.bot,lead:undefined},{min:10,max:90,aim:'ballistic',lead:undefined});
 assert.deepEqual(GUN_KITS.pistol.q.bot,{min:12,max:45,aim:'direct'});assert.deepEqual(GUN_KITS.machinegun.q.bot,{min:14,max:40,aim:'direct'});assert.deepEqual(GUN_KITS.rifle.q.bot,{min:8,max:140,aim:'direct'});
 assert.deepEqual(ADS_PROFILES.pistol,{fov:55,spread:.25,settle:.15,move:.75,sens:.68,overlay:'sight',kick:.5,interval:1.12});
 assert.deepEqual(ADS_PROFILES.machinegun,{fov:52,spread:.4,settle:.2,move:.55,sens:.63,overlay:'sight',kick:.45,bloom:.002});
 assert.deepEqual(ADS_PROFILES.rifle,{fov:28,spread:0,settle:.3,move:.5,sens:.36,overlay:'scope',kick:.6});
});

// ---- ADS --------------------------------------------------------------------------------------------------------------
/** RMS angle of `n` primary shots around their mean direction (MG bloom reset per shot: the settled first-shot spread). */
function sigma(s:Simulation,a:Player,n=300){const dirs:Vec[]=[];for(let i=0;i<n;i++){s.projectiles.clear();a.adsBloom=undefined;s.spawnProjectile(a);dirs.push(norm([...s.projectiles.values()][0].vel));}
 const mean=norm(dirs.reduce((m,d)=>add(m,d),v()));return Math.sqrt(dirs.reduce((t,d)=>t+Math.acos(Math.min(1,dot(d,mean)))**2,0)/n);}
test('ADS spread: after the settle pistol σ is 25 % and rifle 0 % of hip (≤ 30 %); MG keeps its designed 40 % first shot',()=>{
 for(const spec of GUNS){const {s,a}=game(spec);try{const hip=sigma(s,a);run(s,a,30,{ability:true});assert.ok(a.adsSince!==undefined);const ads=sigma(s,a),ratio=ads/hip;
  if(spec==='machinegun')assert.ok(ratio>.3&&ratio<.5,`${spec} ${ratio}`);else assert.ok(ratio<=.3,`${spec} ${ratio}`);if(spec==='rifle')near(ads,0,1e-6,'rifle exact');
 }finally{s.dispose();}}
});
test('ADS: MG bloom grows with each aimed shot, caps at hip and resets after .25 s; pistol fires ×1.12 slower only while aiming',()=>{
 const {s,a}=game('machinegun');try{const hip=WEAPONS.machinegun.spread;run(s,a,15,{ability:true});const base=adsSpread(a,'machinegun',s.time,hip);near(base,hip*.4,1e-12,'settled');
  const ammo=a.ammo;run(s,a,24,{ability:true,attack:true});const fired=ammo-a.ammo;assert.ok(fired>=4,`${fired}`);assert.equal(a.adsBloom?.n,fired);near(adsSpread(a,'machinegun',s.time,hip),Math.min(hip,base+.002*fired),1e-12,'bloom');
  a.adsBloom={n:40,at:s.time};near(adsSpread(a,'machinegun',s.time,hip),hip,1e-12,'capped at hip');run(s,a,16,{ability:true});near(adsSpread(a,'machinegun',s.time,hip),base,1e-12,'reset');
 }finally{s.dispose();}
 for(const spec of GUNS)for(const ads of [false,true]){const {s,a}=game(spec);try{if(ads)run(s,a,20,{ability:true});tick(s,a,{attack:true,...(ads?{ability:true}:{})});
  near(a.cooldown,WEAPONS[spec].interval*(ads&&spec==='pistol'?1.12:1),1e-9,`${spec} ads=${ads}`);}finally{s.dispose();}}
});
test('ADS: projectile speed and gravity are identical in and out of ADS; walking × profile; adsSince clears on release, roll and death',()=>{
 for(const spec of GUNS){const {s,a}=game(spec);try{const shot=()=>{s.projectiles.clear();s.spawnProjectile(a);const q=[...s.projectiles.values()][0];return [len(q.vel),q.gravity];};
  const hip=shot();run(s,a,25,{ability:true});const ads=shot();near(ads[0],hip[0],1e-9,spec+' speed');assert.equal(ads[1],hip[1]);assert.deepEqual(shotBallistics(spec,a.stats),{speed:WEAPONS[spec].speed,gravity:WEAPONS[spec].gravity});
 }finally{s.dispose();}
  const walk=(ability:boolean)=>{const {s,a}=game(spec,v(10,.9,-10));try{run(s,a,50,{z:1,...(ability?{ability:true}:{})});return a.body.linvel().z;}finally{s.dispose();}};near(walk(true)/walk(false),ADS_PROFILES[spec].move,.02,spec+' walk');}
 const {s,a}=game('pistol');try{run(s,a,5,{ability:true});assert.notEqual(a.adsSince,undefined);tick(s,a);assert.equal(a.adsSince,undefined,'release');
  run(s,a,5,{ability:true});assert.notEqual(a.adsSince,undefined);a.grounded=true;tick(s,a,{ability:true,dash:true,z:1});assert.ok(a.roll,'rolled');assert.equal(a.adsSince,undefined,'roll');
  run(s,a,60,{ability:true});assert.notEqual(a.adsSince,undefined);s.damage(a,1000,'b','test');assert.equal(a.adsSince,undefined,'death');run(s,a,3,{ability:true});assert.equal(a.adsSince,undefined,'dead pilots never aim');
 }finally{s.dispose();}
});

// ---- Frag Grenade -----------------------------------------------------------------------------------------------------
test('frag: lands within 1.5 m of the aim point at 6/12/20 m, bounces short, and detonates for 60 at the centre after its fuse',()=>{
 for(const d of [6,12,20]){const {s,a}=game('pistol',v(25,.9,-20));try{const aim=v(0,0,d);aimAt(a,aim);a.reloadTime=1;a.ammo=0;tick(s,a,{secondary:true});assert.ok(a.abilityCast,'cast during reload');until(s,a,()=>!!shotOf(s,'frag-grenade'),30);assert.equal(a.ammo,0,'thrown with no ammo');near(a.secondaryCd,8,1/30,'cooldown');
  let contact:Vec|undefined,shot:Projectile|undefined,fuse=0;until(s,a,()=>{shot=shotOf(s,'frag-grenade')??shot;if(shot&&!contact&&shot.ability!.data!.c){contact={...shot.pos};fuse=shot.ability!.fuse!;}return blasts(s,'frag').length>0;});
  assert.ok(contact,`${d} m: no contact`);const miss=Math.hypot(contact.x-aim.x,contact.z-aim.z);assert.ok(miss<=1.5,`${d} m: landed ${miss.toFixed(2)} m off`);
  const boom=blasts(s,'frag')[0];assert.ok(boom,'detonated');assert.ok(Math.hypot(boom.pos.x-aim.x,boom.pos.z-aim.z)<=3.5,`${d} m: rolled to ${JSON.stringify(boom.pos)}`);near(boom.time,fuse,1/60+1e-9,'fuse');
 }finally{s.dispose();}}
 const {s,a,b}=game('pistol',v(0,.9,10));try{s.abilities.spawnShot(a,GUN_KITS.pistol.e,{cast:'c',pos:add(b.body.translation(),v(0,0,-.01)),vel:v(0,0,0),damage:60,fuse:s.time});s.stepProjectiles(1/60);near(100-b.hp,60,.5,'centre');}finally{s.dispose();}
});
test('frag: a standing enemy takes ≤ 10 on contact and the blast follows ≥ .45 s later; an add detonates it at once; Block reflects it',()=>{
 {const {s,a,b}=game('pistol',v(0,.9,7));try{aimAt(a,b.body.translation());tick(s,a,{secondary:true});let touched=-1,hp=100;
  until(s,a,()=>{if(touched<0&&b.hp<100){touched=s.time;hp=b.hp;}return blasts(s,'frag').length>0;});
  assert.ok(touched>0,'contact');assert.ok(100-hp<=10&&100-hp>0,`impact ${100-hp}`);const boom=blasts(s,'frag')[0];assert.ok(boom.time-touched>=.45-1e-9,`${boom.time-touched}`);assert.ok(b.hp<hp,'the blast still lands');
 }finally{s.dispose();}}
 {const {s,a,b}=game('pistol',v(20,.9,0));try{s.chaos.summon(b,1,'zombie',v(0,.9,6),30);s.world.step();const z=[...s.chaos.minions.values()][0];z.speed=0;z.body.setTranslation(v(0,.9,6),true);s.world.step();
  aimAt(a,z.body.translation());tick(s,a,{secondary:true});const release=a.abilityCast!.at,zHp=z.hp;until(s,a,()=>blasts(s,'frag').length>0,90);const boom=blasts(s,'frag')[0];assert.ok(boom,'add contact detonates');
  assert.ok(boom.time-release<.6,'at contact (flight .45 s), not after the fuse (≥ 1.05 s)');assert.ok(len(sub(boom.pos,v(0,.9,6)))<1.2);assert.ok(!s.chaos.minions.has(z.id)||z.hp<zHp);
 }finally{s.dispose();}}
 {const {s,a,b}=game('pistol',v(0,.9,6));try{aimAt(a,b.body.translation());tick(s,a,{secondary:true});let reflected=false;
  guarded(s,a,'frag-grenade',2.5,()=>{if(shotOf(s,'frag-grenade')?.owner==='b')reflected=true;return blasts(s,'frag').length>0;});
  assert.ok(reflected,'Block hands the grenade to the blocker');assert.equal(blasts(s,'frag')[0].source,'b');assert.equal(b.hp,100);
 }finally{s.dispose();}}
});

// ---- Underbarrel Rocket -------------------------------------------------------------------------------------------------
test('rocket: a contact inside 3 m is a 15-damage dud with no blast; an armed hit blasts ≈ 55 and props take ×2; Block reflects it',()=>{
 {const {s,a,b}=game('machinegun',v(0,.9,2.2));try{aimAt(a,b.body.translation());tick(s,a,{secondary:true});until(s,a,()=>!a.abilityCast&&!shotOf(s,'rocket'),60);
  near(100-b.hp,15,1e-6,'dud');assert.equal(blasts(s,'rocket').length,0);assert.ok(s.events.some(e=>e.type==='impact'&&e.message==='rocket-dud'));}finally{s.dispose();}}
 {const {s,a,b}=game('machinegun',v(0,.9,12));try{aimAt(a,b.body.translation());tick(s,a,{secondary:true});assert.equal(a.ammo,WEAPONS.machinegun.ammo);until(s,a,()=>blasts(s,'rocket').length>0,90);
  assert.equal(blasts(s,'rocket').length,1);assert.ok(100-b.hp>=50&&100-b.hp<=55+1e-9,`direct ${100-b.hp}`);}finally{s.dispose();}}
 {const {s,a,b}=game('machinegun',v(20,.9,0));try{const wall=s.addProp({id:'wall',pos:v(0,1.5,10),size:v(4,3,.6),material:'stone'})!;s.world.step();const damages:number[]=[],orig=s.damageProp.bind(s);
  s.damageProp=(p,d,...rest)=>{if(p===wall)damages.push(d);return orig(p,d,...rest);};aimAt(a,v(0,1.5,9.7));tick(s,a,{secondary:true});until(s,a,()=>blasts(s,'rocket').length>0,90);
  const boom=blasts(s,'rocket')[0],dist=len(sub(v(0,1.5,10),boom.pos)),reach=3.6+len(wall.size)*.35,fall=1-Math.min(.85,Math.max(0,dist/reach));
  near(damages[0],55*fall*a.stats.demolition*2,1e-6,'props ×2');}finally{s.dispose();}}
 {const {s,a,b}=game('machinegun',v(0,.9,12));try{aimAt(a,b.body.translation());tick(s,a,{secondary:true});let owner='';
  guarded(s,a,'rocket',6,()=>{const r=shotOf(s,'rocket');if(r)owner=r.owner;return blasts(s,'rocket').length>0;});
  assert.equal(owner,'b','reflected');assert.equal(blasts(s,'rocket')[0].source,'b');}finally{s.dispose();}}
 {const {s,a,b}=game('machinegun',v(0,.9,10));try{s.abilities.spawnShot(a,GUN_KITS.machinegun.e,{cast:'c',pos:add(b.body.translation(),v(0,0,-.01)),vel:v(0,0,0),gravity:0,damage:55});s.stepProjectiles(1/60);near(100-b.hp,55,.5,'centre');}finally{s.dispose();}}
});

// ---- Orbital Mark -----------------------------------------------------------------------------------------------------
test('Orbital Mark: the dart tags for 10, the strike follows until tag+.8 s, locks, and lands 85 at tag+1.0 s; surfaces tag points',()=>{
 {const {s,a,b}=game('rifle',v(0,.9,16),v(0,.9,-14));try{aimAt(a,b.body.translation());tick(s,a,{secondary:true});assert.equal(a.ammo,WEAPONS.rifle.ammo);
  until(s,a,()=>b.hp<100,60);const tagAt=s.time;near(100-b.hp,10,1e-6,'dart');const f=[...s.chaos.fields.values()].find(f=>f.fx==='orbital-mark')!;assert.ok(f,'strike field');
  assert.deepEqual([f.kind,f.attach,f.radius,f.damage,f.impulse,f.blastKind],['bomb','b',3,85,1400,'ability']);near(f.lockAt!,tagAt+.8,1e-9,'lock');near(f.until,tagAt+1,1e-9,'strike');
  b.body.setTranslation(v(1,.9,16),true);s.world.step();run(s,a,Math.round(.6*60));near(f.pos.x,1,.05,'follows');
  until(s,a,()=>s.time>=f.lockAt!,60);const locked={...f.pos};near(locked.x,1,.05,'locks on the target');b.body.setTranslation(v(2.5,.9,16),true);s.world.step();run(s,a,3);assert.deepEqual(f.pos,locked,'stays where it locked');
  b.body.setTranslation(v(locked.x,.9,locked.z),true);s.world.step();until(s,a,()=>blasts(s,'orbital-mark').length>0,30);const boom=blasts(s,'orbital-mark')[0];
  near(boom.time,tagAt+1,1/60+1e-9,'strike time');near(boom.pos.x,locked.x,1e-6);assert.ok(100-b.hp>=10+80,`centre strike ${100-b.hp-10}`);
 }finally{s.dispose();}}
 {const {s,a}=game('rifle',v(20,.9,0));try{const spot=v(0,0,8);aimAt(a,spot);tick(s,a,{secondary:true});until(s,a,()=>[...s.chaos.fields.values()].some(f=>f.fx==='orbital-mark'),60);
  const f=[...s.chaos.fields.values()].find(f=>f.fx==='orbital-mark')!;assert.equal(f.attach,undefined);assert.ok(f.pos.y<.2&&Math.hypot(f.pos.x-spot.x,f.pos.z-spot.z)<1,JSON.stringify(f.pos));/* the muzzle start and dart radius meet a grazing floor a little short */}finally{s.dispose();}}
 {const {s,a,b}=game('rifle',v(0,.9,20));try{aimAt(a,b.body.translation());tick(s,a,{secondary:true});guarded(s,a,'orbital-mark',40,()=>[...s.chaos.fields.values()].some(f=>f.fx==='orbital-mark'),60);
  const f=[...s.chaos.fields.values()].find(f=>f.fx==='orbital-mark')!;assert.equal(f.owner,'b','the blocker owns the reflected mark');assert.equal(f.attach,'a');}finally{s.dispose();}}
});
test('Orbital Mark: a dart that kills an add still calls the strike on the spot where it fell',()=>{
 const {s,a,b}=game('rifle',v(20,.9,0));try{s.chaos.summon(b,1,'zombie',v(0,.9,12),30);s.world.step();const z=[...s.chaos.minions.values()][0];z.speed=0;z.hp=5;z.body.setTranslation(v(0,.9,12),true);s.world.step();
  const at={...z.body.translation()};aimAt(a,at);tick(s,a,{secondary:true});until(s,a,()=>!s.chaos.minions.has(z.id),60);assert.equal(s.chaos.minions.has(z.id),false,'the dart kills it');
  const f=[...s.chaos.fields.values()].find(f=>f.fx==='orbital-mark')!;assert.ok(f&&len(sub(f.pos,at))<.3,'strike field where the add fell');until(s,a,()=>blasts(s,'orbital-mark').length>0,90);assert.equal(blasts(s,'orbital-mark').length,1);
 }finally{s.dispose();}
});
test('Orbital Mark: ≥ 40 per cast against a scripted 6.8 m/s strafer at 30 m with a perfect-aim lead (no dodge AI)',()=>{
 for(const dir of [1,-1]){const {s,a,b}=game('rifle',v(-dir*4,.9,16),v(0,.9,-14));try{run(s,a,30,{},{x:dir});const vel=b.body.linvel();near(Math.hypot(vel.x,vel.z),C.speed,.05,'strafer speed');
  const dist=len(sub(b.body.translation(),eye(a))),lead=.15+dist/150,to=add(b.body.translation(),mul(v(vel.x,0,vel.z),lead)),d=sub(to,eye(a)),h=Math.hypot(d.x,d.z);
  a.input.yaw=Math.atan2(d.x,d.z);a.input.pitch=ballisticPitch(h,d.y,150,6);tick(s,a,{secondary:true},{x:dir});until(s,a,()=>blasts(s,'orbital-mark').length>0,120,{x:dir});
  assert.ok(blasts(s,'orbital-mark').length===1,'struck');assert.ok(100-b.hp>=40,`${dir}: ${100-b.hp}`);
 }finally{s.dispose();}}
});

test('every gun E is avoidable: roll i-frames take none of the frag, rocket or strike blast at their centres',()=>{
 for(const id of ['frag-grenade','rocket','orbital-mark']){const {s,a,b}=game('pistol',v(0,.9,10));try{b.grounded=true;tick(s,a,{},{dash:true,z:1});assert.ok(b.roll,'rolled');until(s,a,()=>rollInvulnerable(b,s.time),20,{z:1});assert.ok(rollInvulnerable(b,s.time));
  const at=add(b.body.translation(),v(0,0,-.01)),def=ABILITY_DEFS[id];
  if(id==='orbital-mark'){s.abilities.zone(a,'bomb',at,3,0,85,'orbital-mark',{impulse:1400,blastKind:'ability',cast:'c'});s.chaos.step(1/60);}
  else{s.abilities.spawnShot(a,def,{cast:'c',pos:at,vel:v(),gravity:0,damage:def.damage,fuse:s.time});s.stepProjectiles(1/60);}
  assert.equal(blasts(s,def.fx).length,1,id);assert.equal(b.hp,100,id);
 }finally{s.dispose();}}
});
// ---- bots -------------------------------------------------------------------------------------------------------------
test('bots: ADS inside the Q range only; below the E minimum the primary keeps firing and secondary stays off; E while reloading',()=>{
 for(const spec of GUNS){const k=kitFor(spec)!,q=k.q.bot!,e=k.e.bot!;const {s,a,b}=game(spec,v(0,.9,(e.min+Math.min(e.max,30))/2));try{a.bot='hard';s.random=()=>0;
  const think=(distance:number,visible=true)=>{const i={...idleInput(),attack:true};botAbilities(s,a,i,distance,visible,2,b.body.translation(),v());return i;};
  for(const [d,want] of [[q.min-1,false],[q.min+1,true],[q.max-1,true],[q.max+1,false]] as const)assert.equal(think(d).ability,want,`${spec} ADS at ${d}`);
  assert.equal(think((q.min+q.max)/2,false).ability,false,'not visible');
  for(let n=0;n<40;n++){const i=think(e.min-1);assert.equal(i.secondary,false,`${spec} below min`);assert.equal(i.attack,true,`${spec} keeps firing`);}
  a.reloadTime=1;a.ammo=0;const i=think(len(sub(b.body.translation(),a.body.translation())));assert.equal(i.secondary,true,`${spec} E while reloading`);assert.equal(i.camera,'first');
 }finally{s.dispose();}}
});
function botArena(spec:SpecId,distance:number){
 const s=new Simulation({skipIntros:true,sandbox:true});s.addPlayer('host','DEV','ranged',null,'#ffffff',{},true);s.configureLobby({botCount:1,botWeapons:[spec],botDifficulty:'hard'},'host');s.resetRound();s.phase='fight';
 for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});
 const bot=s.players.get('bot')!,target=s.players.get('host')!;bot.body.setTranslation(v(0,.9,-distance/2),true);target.body.setTranslation(v(0,.9,distance/2),true);target.hp=target.stats.hp=100000;s.world.propagateModifiedBodyPositionsToColliders();return {s,bot,target};
}
test('hard pistol, MG and rifle bots aim down sights at range and use E within 10 s of visible contact',()=>{
 for(const [spec,distance] of [['pistol',20],['machinegun',22],['rifle',34]] as const){const {s,bot,target}=botArena(spec,distance);try{const q=kitFor(spec)!.q.bot!;let aimed=0,first=-1,released=false;
  for(let n=0;n<600&&!(first>=0&&aimed&&released);n++){s.step();const d=len(sub(target.body.translation(),bot.body.translation()));if(bot.input.ability&&d>=q.min)aimed++;if(first<0&&bot.lastSecondary>=0)first=s.time;released||=s.events.some(e=>e.type==='ability'&&e.source===bot.id&&e.message===kitFor(spec)!.e.id);}
  assert.ok(aimed>0,spec+' never aimed at range');assert.ok(first>=0,spec+' never used E');assert.ok(released,spec+' E released');
 }finally{s.dispose();}}
});
