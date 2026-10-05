// Ranged/magic Q+E ability framework (design §3, FOUNDATION). Fake handlers stand in for the GUNS/MAGIC kits: each test
// process registers them in ABILITY_HANDLERS and flips `live` on a kit row, so the real gating, costs, charges, windups,
// buffer, chip hooks, field pool, slows and prediction run exactly as they will for the real abilities.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {Simulation,initPhysics,sanitizeInput,type Player} from '../src/shared/simulation.js';
import {CONFIG as C,idleInput,v,len,sub,forward,add,mul,type Input,type SpecId,type Vec} from '../src/shared/config.js';
import {WEAPONS,BASE_STATS,CHIPS,statsFor,eligibleChips,type Stats} from '../src/shared/content.js';
import {coalesceInput,validPacket,RELAY} from '../src/shared/relay-protocol.js';
import {HostedRoom} from '../src/shared/host-room.js';
import {abilityClient,abilityUser,kitFor,abilityActive,abilityCost,abilityCooldown,abilityRadius,abilityScale,castTime,maxCharges,adsMove,adsSpread,shotBallistics,groundAimPoint,ABILITY_DEFS,ABILITY_FX,FX_ABILITY,HOST_CAPS,BOT_LEAD} from '../src/shared/abilities.js';
import {ABILITY_HANDLERS,type AbilityHandler,type CastContext} from '../src/shared/ability-system.js';
import {GUN_KITS} from '../src/shared/gun-kit.js';
import {MAGIC_KITS} from '../src/shared/magic-kit.js';
import {PONG} from '../src/shared/pong.js';
import {MAGIC_CHIPS} from '../src/shared/magic-chips.js';
import {Predictor} from '../src/client/prediction.js';
import {controlHints} from '../src/client/control-hints.js';
import {abilitySlots,abilitySlotsHtml} from '../src/client/ability-hud.js';
import {cameraPosition} from '../src/shared/view.js';
import {ballisticPitch} from '../src/shared/projectiles.js';
import {botAbilities} from '../src/shared/bot-abilities.js';
await initPhysics();

const near=(a:number,b:number,eps=1e-9,msg='')=>assert.ok(Math.abs(a-b)<=eps,`${msg} ${a} vs ${b}`);
/** Flat-floor duel: `a` (spec) at the origin facing +z, `b` 7 m ahead. */
function game(spec:SpecId='pistol',chips:Record<string,number>={}){
 const s=new Simulation({sandbox:true}),a=s.addPlayer('a','A',WEAPONS[spec].class)!,b=s.addPlayer('b','B')!;a.spec=spec;a.chips=chips;s.resetRound();s.phase='fight';
 for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});
 a.body.setTranslation(v(0,.9,0),true);b.body.setTranslation(v(0,.9,7),true);s.world.step();for(const p of [a,b]){p.grounded=true;p.inputAt=s.time;}return {s,a,b};
}
/** One tick with `a`'s input (others idle). */
function tick(s:Simulation,a:Player,input:Partial<Input>={},seq={n:0}){s.setInput(a.id,{...idleInput(),seq:++seq.n+a.input.seq,yaw:a.input.yaw,pitch:a.input.pitch,...input});for(const p of s.players.values())p.inputAt=s.time;s.step();
 // Hold-to-aim (2026-10-04): an aimed E casts on release, so a scripted tap is press + release.
 if(input.secondary&&s.abilities.holds(a,'e'))tick(s,a,{...input,secondary:false},seq);}
const run=(s:Simulation,a:Player,n:number,input:Partial<Input>={})=>{for(let i=0;i<n;i++)tick(s,a,input);};
/** A fake live handler for one kit def; returns the recorded release contexts. */
function fake(spec:SpecId,slot:'q'|'e',extra:Partial<AbilityHandler>={}){const def=kitFor(spec)![slot];def.live=true;const calls:CastContext[]=[];ABILITY_HANDLERS[def.id]={release:(_sys,ctx)=>{calls.push(ctx);},...extra};return {def,calls};}
const events=(s:Simulation,type:string)=>s.events.filter(e=>e.type===type);

// ---- protocol -------------------------------------------------------------------------------------------------------
test('protocol: strict sanitize, idle false, coalesce keeps taps, a 60-input batch with ability fits the guest limit',()=>{
 const base={seq:1,x:0,z:0,yaw:0,pitch:0};
 assert.equal(sanitizeInput({...base,ability:true})!.ability,true);
 for(const bad of ['true',1,{},null,false])assert.equal('ability'in sanitizeInput({...base,ability:bad})!,false,String(bad));
 assert.equal('ability'in sanitizeInput(base)!,false);
 assert.equal(idleInput().ability,false);
 assert.equal(coalesceInput({type:'input',input:{...base,ability:true}},{type:'input',input:{...base,seq:2,ability:false}}).input.ability,true);
 assert.equal(coalesceInput({type:'input',input:{...base}},{type:'input',input:{...base,seq:2}}).input.ability,false);
 const full=(n:number)=>({camera:'third',seq:1000000+n,x:-.7071067811865476,z:.7071067811865475,yaw:-2.356194490192345,pitch:-.08726646259971647,attack:true,block:false,sirenToggle:false,electric:true,hadoken:false,uppercut:false,secondary:true,jump:false,dash:false,reload:false,ability:true});
 assert.ok(JSON.stringify(full(1)).length<=330,`${JSON.stringify(full(1)).length}`);
 const packet={seq:1,ack:0,events:[],latest:{type:'inputs',epoch:'1:1:1',inputs:Array.from({length:60},(_,n)=>full(n))}};
 assert.ok(JSON.stringify(packet).length<=RELAY.maxGuestBytes,`${JSON.stringify(packet).length}`);assert.equal(validPacket(packet,false),true);
});
test('PvE forced idle literal and Block clear the ability bit',async()=>{
 const economy=await readFile('src/shared/pve/economy.ts','utf8'),recruit=await readFile('src/shared/pve/recruit-ai.ts','utf8'),sim=await readFile('src/shared/simulation.ts','utf8');
 assert.match(economy,/attack:false,ability:false,electric:false/);
 assert.match(recruit,/i\.block=true;i\.attack=false;i\.secondary=false;i\.ability=false;/);
 assert.match(sim,/if\(i\.block\)\{i\.attack=false;i\.secondary=false;i\.ability=false;\}/);
});

// ---- capability flag ------------------------------------------------------------------------------------------------
test('caps: every host path advertises abilities in welcome; abilityClient truth table',async()=>{
 const sent:{target:string;message:any}[]=[];const room=new HostedRoom('ABCDEFGH',{name:'Host',class:'ranged',color:'#ffffff'},false,(target,message)=>sent.push({target,message}));
 try{room.welcome('host');assert.ok(room.join({name:'Guest',class:'magic',color:'#00ff00'}));const welcomes=sent.filter(m=>m.message.type==='welcome');assert.equal(welcomes.length,2);for(const w of welcomes)assert.deepEqual(w.message.caps,{abilities:1,aimHold:1,move:2});}finally{room.dispose();}
 const server=await readFile('src/server/index.ts','utf8'),welcomes=server.match(/type:'welcome'[^}]*\}/g)!;assert.equal(welcomes.length,2,'rejoin and create/join');for(const w of welcomes)assert.match(w,/caps:HOST_CAPS/);
 assert.deepEqual(HOST_CAPS,{abilities:1,aimHold:1,move:2});assert.equal(JSON.stringify({caps:HOST_CAPS}).length-2,43,'caps adds 43 chars to welcome (hold-to-aim and sprint/roll-cancel, 2026-10-04)');
 const users:SpecId[]=['pistol','rifle','machinegun','fire','water','lightning','darkness'],others:SpecId[]=['sword','daggers','shield','greatsword','emperor','ansatsuken','leet','siren'];
 for(const spec of users){assert.equal(abilityUser(spec),true,spec);assert.equal(abilityClient(HOST_CAPS,spec),true,spec);assert.equal(abilityClient({},spec),false,spec);assert.equal(abilityClient(undefined,spec),false,spec);assert.equal(abilityClient(null,spec),false,spec);}
 for(const spec of others){assert.equal(abilityUser(spec),false,spec);assert.equal(abilityClient(HOST_CAPS,spec),false,spec);}
 assert.equal(abilityClient(HOST_CAPS,undefined),false);
});

// ---- kits, contract ids, live/handler agreement ---------------------------------------------------------------------
test('kits: every ranged/magic spec has a Q/E row with the reserved ids; live defs and handlers agree (none in F)',()=>{
 const ids={pistol:['ads','frag-grenade'],machinegun:['ads','rocket'],rifle:['ads','orbital-mark'],fire:['flame-lance','meteor-strike'],water:['tidal-wave','geyser-wall'],lightning:['blink-strike','ball-lightning'],darkness:['shadow-shackles','singularity']} as const;
 for(const [spec,[q,e]] of Object.entries(ids)){const k=kitFor(spec as SpecId)!;assert.equal(k.q.id,q);assert.equal(k.e.id,e);assert.equal(k.q.slot,'q');assert.equal(k.e.slot,'e');assert.ok(!k.e.bot||k.e.live,spec);assert.ok(!k.q.bot||k.q.live||k.q.kind==='ads',spec);assert.ok(k.e.fx&&ABILITY_FX.has(k.e.fx),spec);}
 for(const spec of ['pistol','rifle','machinegun'] as const){assert.ok(kitFor(spec)!.ads);assert.equal(abilityActive(spec,'q'),true);}
 // State-agnostic since GUNS (design §17 G): an E is active exactly when its row is live (F: none; G: the gun E's; M: magic).
 for(const spec of Object.keys(ids) as SpecId[])assert.equal(abilityActive(spec,'e'),!!kitFor(spec)!.e.live,`${spec} E is legacy until live`);
 const live=Object.values(ABILITY_DEFS).filter(d=>d.live).map(d=>d.id).sort(),handled=Object.keys(ABILITY_HANDLERS).sort();assert.deepEqual(live,handled);for(const id of live)assert.ok(ABILITY_DEFS[id].bot||ABILITY_HANDLERS[id].bot,`${id}: a live kit carries bot data (row or custom handler.bot: Blink Strike)`);
 assert.deepEqual(new Set(Object.values(FX_ABILITY)),new Set([...Object.values(GUN_KITS),...Object.values(MAGIC_KITS)].flatMap(k=>[k.q,k.e]).filter(d=>d.kind!=='ads').map(d=>d.id)));
 for(const spec of ['sword','emperor','leet','siren','ansatsuken'] as SpecId[])assert.equal(kitFor(spec),undefined);
 assert.deepEqual(BOT_LEAD,[0,0,0]);
});

// ---- DEV specs stay inert -------------------------------------------------------------------------------------------
test('DEV inert: emperor, siren and leet ignore the ability bit; Emperor electric is unchanged',()=>{
 for(const spec of ['emperor','siren','leet'] as SpecId[]){
  const trace=(withAbility:boolean)=>{const {s,a}=game(spec);a.developer=true;a.unlocks=['weapon:'+spec];try{const seq={n:0};for(let i=0;i<90;i++)tick(s,a,{electric:i%30<3,attack:i%20<2,z:.5,...(withAbility?{ability:true}:{})},seq);return {pos:{...a.body.translation()},hp:a.hp,cd:a.cooldown,mana:a.mana,ads:a.adsSince,cast:a.abilityCast,events:s.events.map(e=>e.type+':'+(e.message||'')).join(',')};}finally{s.dispose();}};
  const plain=trace(false),held=trace(true);assert.equal(held.ads,undefined,spec);assert.equal(held.cast,undefined,spec);assert.deepEqual(held,plain,spec);
 }
});

// ---- ADS ------------------------------------------------------------------------------------------------------------
test('ADS: a human pistol holding Q is slower and tighter server-side, fires slower, and aims from the eye',()=>{
 const walk=(ability:boolean)=>{const {s,a}=game('pistol');try{run(s,a,60,{z:1,...(ability?{ability:true}:{})});return {z:a.body.translation().z,ads:a.adsSince,s:s.time};}finally{s.dispose();}};
 const hip=walk(false),aimed=walk(true);assert.ok(aimed.ads!==undefined);assert.ok(aimed.z<hip.z*.85&&aimed.z>hip.z*.6,`${aimed.z} vs ${hip.z}`);
 const {s,a}=game('pistol');try{
  run(s,a,2,{ability:true});const since=a.adsSince!;assert.ok(since>0);
  assert.equal(s.abilities.shotTuning(a).spread,adsSpread(a,'pistol',s.time,WEAPONS.pistol.spread));
  run(s,a,12,{ability:true});const settled=s.abilities.shotTuning(a);near(settled.spread,WEAPONS.pistol.spread*.25,1e-12,'settled pistol spread');assert.equal(settled.camera,'first');
  assert.equal(s.abilities.intervalScale(a),1.12);tick(s,a,{ability:true,attack:true});near(a.cooldown,WEAPONS.pistol.interval*1.12,1e-9,'pistol ADS interval');
  tick(s,a,{});assert.equal(a.adsSince,undefined);assert.equal(s.abilities.intervalScale(a),1);assert.equal(s.abilities.shotTuning(a).spread,WEAPONS.pistol.spread);
 }finally{s.dispose();}
 // Third-person rifle scoped: the server ray starts at the eye, not the third-person camera.
 const g=game('rifle');try{const {s:rs,a:r}=g;r.input.camera='third';run(rs,r,20,{ability:true,camera:'third',pitch:.05});rs.events=[];rs.projectiles.clear();
  const yaw=r.input.yaw,pitch=r.input.pitch,eye=cameraPosition(r.body.translation(),yaw,pitch,'first');tick(rs,r,{ability:true,camera:'third',attack:true,pitch:.05});
  const shot=[...rs.projectiles.values()][0];assert.ok(shot,'rifle fired');const aimPoint=add(eye,mul(forward(yaw,pitch),C.projectileMaxTravel));
  // Owner bug 2026-10-04 (ADS zeroing): the ray's end point (nothing hit: range + 5 m along the eye ray) is met on the low
  // ballistic arc, so the launch pitch is ballisticPitch toward it; the yaw still comes from the eye-origin aim.
  const target=add(eye,mul(forward(yaw,pitch),WEAPONS.rifle.range+5)),d=sub(target,shot.pos),lp=ballisticPitch(Math.hypot(d.x,d.z),d.y,WEAPONS.rifle.speed,WEAPONS.rifle.gravity),ly=Math.atan2(d.x,d.z);void aimPoint;
  const dir=mul(shot.vel,1/len(shot.vel)),expected=forward(ly,lp);assert.ok(len(sub(dir,expected))<.01,`eye-origin aim ${JSON.stringify(dir)} vs ${JSON.stringify(expected)}`);
 }finally{g.s.dispose();}
 // ADS never changes ballistics.
 for(const spec of ['pistol','rifle','machinegun'] as const)assert.deepEqual(shotBallistics(spec,BASE_STATS),{speed:WEAPONS[spec].speed,gravity:WEAPONS[spec].gravity});
 assert.equal(adsMove('pistol',{ability:true}),.75);assert.equal(adsMove('pistol',{ability:false}),1);assert.equal(adsMove('fire',{ability:true}),1);assert.equal(adsMove('emperor',{ability:true}),1);
});
test('ADS: machine-gun bloom grows per consecutive aimed shot up to the hip spread and resets after .25 s',()=>{
 const {s,a}=game('machinegun');try{run(s,a,20,{ability:true});const hip=WEAPONS.machinegun.spread,spreads:number[]=[];for(let i=0;i<12;i++)spreads.push(s.abilities.shotTuning(a).spread);
  near(spreads[0],hip*.4,1e-12,'first aimed shot');for(let i=1;i<spreads.length;i++)assert.ok(spreads[i]>=spreads[i-1]);assert.ok(spreads.at(-1)!<=hip+1e-12);near(spreads[3],Math.min(hip,hip*.4+.002*3),1e-12,'bloom +.002 per shot');
  run(s,a,20,{ability:true});near(s.abilities.shotTuning(a).spread,hip*.4,1e-12,'bloom reset');
 }finally{s.dispose();}
});

// ---- gating, windups, cancel, buffer --------------------------------------------------------------------------------
test('gating: a live E casts after its windup, is not gated by primary cooldown or reload, and spends at release',()=>{
 const {def,calls}=fake('pistol','e');const {s,a}=game('pistol');try{
  a.cooldown=5;a.reloadTime=1;a.ammo=0;tick(s,a,{secondary:true});assert.ok(a.abilityCast,'cast started during reload and cooldown');assert.equal(a.abilityCast!.id,'frag-grenade');
  near(a.abilityCast!.at-s.time,castTime(def.windup,a.stats),1e-9);assert.equal(events(s,'ability-cast').length,1);assert.equal(calls.length,0);assert.equal(a.secondaryCd,0);assert.equal(a.eCharges,1);
  tick(s,a,{secondary:false});run(s,a,Math.ceil(def.windup*60)+1);assert.equal(calls.length,1);assert.equal(a.abilityCast,undefined);assert.ok(a.secondaryCd>abilityCooldown(def,a.stats)-.2&&a.secondaryCd<=abilityCooldown(def,a.stats),`${a.secondaryCd}`);assert.equal(a.eCharges,0);assert.equal(events(s,'ability').filter(e=>e.message==='frag-grenade').length,1);
  assert.ok(a.cooldown>=castTime(def.lockout,a.stats)-.05);assert.equal(calls[0].scale,1);assert.equal(calls[0].echo,false);
  tick(s,a,{secondary:true});assert.equal(a.abilityCast,undefined,'no charge: refused');
 }finally{s.dispose();delete ABILITY_HANDLERS[def.id];def.live=undefined;}
});
test('gating: Block, death, frozen and stun refuse; Block and roll cancel a windup with nothing spent and the charge kept',()=>{
 const {def,calls}=fake('darkness','e');const fresh=()=>{const g=game('darkness');g.a.mana=100;return g;};
 try{
  {const {s,a}=fresh();try{a.blockTime=.3;assert.equal(s.abilities.press(a,'e'),'refused');a.blockTime=0;a.leet={frozenUntil:s.time+1};assert.equal(s.abilities.press(a,'e'),'refused');a.leet=undefined;a.hp=0;assert.equal(s.abilities.press(a,'e'),'refused');a.hp=100;
   a.mana=abilityCost(def,a.stats)-1;assert.equal(s.abilities.press(a,'e'),'refused','not enough mana');a.mana=100;
   a.hitStunUntil=s.time+1;assert.equal(s.abilities.press(a,'e'),'buffered','stun buffers');assert.equal(a.abilityCast,undefined);run(s,a,30);assert.equal(a.abilityCast,undefined,'stale buffer expired');assert.equal(a.pendingPress,undefined);assert.equal(calls.length,0);}finally{s.dispose();}}
  {const {s,a}=fresh();try{tick(s,a,{secondary:true});assert.ok(a.abilityCast);s.beginBlock(a);assert.equal(a.abilityCast,undefined);assert.equal(a.pendingPress,undefined);run(s,a,30);assert.equal(calls.length,0);near(a.mana,100,1e-9,'mana kept');assert.equal(a.eCharges,1);assert.equal(a.secondaryCd,0);assert.equal(events(s,'ability').length,0);}finally{s.dispose();}}
  {const {s,a}=fresh();try{tick(s,a,{secondary:true});assert.ok(a.abilityCast);a.grounded=true;tick(s,a,{dash:true,z:1});assert.ok(a.roll,'rolled');assert.equal(a.abilityCast,undefined,'roll cancels the windup');run(s,a,30);assert.equal(calls.length,0);near(a.mana,100,1e-9);assert.equal(a.eCharges,1);}finally{s.dispose();}}
  {const {s,a}=fresh();try{tick(s,a,{secondary:true});s.damage(a,1000,'b','test');assert.equal(a.abilityCast,undefined,'death cancels');run(s,a,30);assert.equal(calls.length,0);}finally{s.dispose();}}
 }finally{delete ABILITY_HANDLERS[def.id];def.live=undefined;}
});
test('press buffer: a Q tap during a darkness bolt windup casts within .3 s; a stale press expires',()=>{
 const {def,calls}=fake('darkness','q');try{
  {const {s,a}=game('darkness');try{a.mana=100;tick(s,a,{attack:true});assert.ok(a.cast,'bolt windup pending');tick(s,a,{ability:true});assert.equal(a.pendingPress?.slot,'q');const at=s.time;
   for(let i=0;i<30&&!calls.length;i++)tick(s,a,{});assert.equal(calls.length,1,'buffered Q cast');assert.ok(a.lastAbility-at<=.3,'cast started inside the buffer window');assert.ok(s.time-at<=.3+1e-9+castTime(def.windup,a.stats));assert.equal(a.pendingPress,undefined);}finally{s.dispose();}}
  {const {s,a}=game('darkness');try{calls.length=0;a.mana=100;a.cast={at:s.time+1,multiplier:1,secondary:false};tick(s,a,{ability:true});assert.equal(a.pendingPress?.slot,'q');run(s,a,20);assert.equal(a.pendingPress,undefined,'expired after .3 s');run(s,a,50);assert.equal(calls.length,0);assert.equal(a.lastAbility,-10);}finally{s.dispose();}}
 }finally{delete ABILITY_HANDLERS[def.id];def.live=undefined;}
});

// ---- scaling, charges, chip effects ---------------------------------------------------------------------------------
test('scaling: cost/cooldown/radius clamps, castTime capped at 1.5×, abilityScale ≤ 2.25 with maximum stacks',()=>{
 const st=(o:Partial<Stats>):Stats=>({...BASE_STATS,...o}),def=MAGIC_KITS.fire.e;
 assert.equal(abilityCooldown(def,st({abilityCooldown:.1})),def.cooldown*.45);assert.equal(abilityCooldown(def,st({abilityCooldown:9})),def.cooldown*3);
 assert.equal(abilityCost(def,st({abilityCost:.5})),def.cost*.5);assert.equal(abilityRadius(def,st({abilityRadius:3})),def.radius!*1.5);assert.equal(abilityRadius(def,st({abilityRadius:.1})),def.radius!*.5);
 assert.equal(castTime(.3,st({attackRate:5})),.3/1.5);assert.equal(castTime(.3,st({attackRate:.15})),.3/.15);assert.equal(castTime(.3,BASE_STATS),.3);
 assert.equal(abilityScale(BASE_STATS,false),1);assert.equal(abilityScale(st({damage:100,abilityPower:2,arcaneSurge:1}),true),2.25);assert.equal(abilityScale(st({damage:100,abilityPower:2,arcaneSurge:1}),true,true),1.125);
 assert.equal(abilityScale(st({damage:.01}),false),.5);near(abilityScale(st({abilityPower:1.5,arcaneSurge:.5}),true),2,1e-12);
 const clamped=statsFor({});assert.equal(clamped.abilityCooldown,1);assert.equal(clamped.spellEcho,0);assert.equal(maxCharges(st({abilityCharges:9})),3);assert.equal(maxCharges(BASE_STATS),1);
 near(def.damage!*abilityScale(st({damage:100,abilityPower:2,arcaneSurge:1}),true),157.5,1e-9,'max-stacked Meteor centre ≤ 160');assert.ok(abilityRadius(def,st({abilityRadius:9}))<=6.75);
});
test('E charges: max 2 gives two casts back to back, then recharges one at a time',()=>{
 const {def,calls}=fake('pistol','e');const {s,a}=game('pistol');try{a.stats={...a.stats,abilityCharges:1};s.abilities.reset(a);assert.equal(a.eCharges,2);
  assert.equal(s.abilities.press(a,'e'),'cast');run(s,a,15);assert.equal(calls.length,1);assert.equal(a.eCharges,1);assert.ok(a.secondaryCd>0);
  assert.equal(s.abilities.press(a,'e'),'cast');run(s,a,15);assert.equal(calls.length,2);assert.equal(a.eCharges,0);assert.equal(s.snapshot().players[0].eCharges,0);
  assert.equal(s.abilities.press(a,'e'),'refused');run(s,a,Math.ceil(def.cooldown*60));assert.equal(a.eCharges,1);assert.ok(a.secondaryCd>0,'second charge recharging');run(s,a,Math.ceil(def.cooldown*60)+2);assert.equal(a.eCharges,2);assert.equal(a.secondaryCd,0);
 }finally{s.dispose();delete ABILITY_HANDLERS[def.id];def.live=undefined;}
});
test('chip effects: Spell Echo re-releases E at +.8 s at 50 %; Spellblade empowers the next ⌈.6/interval⌉ primary shots',()=>{
 const e=fake('fire','e'),q=fake('fire','q');try{const {s,a}=game('fire');try{a.mana=100;a.stats={...a.stats,spellEcho:1,qEmpower:.5};
  assert.equal(s.abilities.press(a,'e'),'cast');run(s,a,20);assert.equal(e.calls.length,1);const first=e.calls[0];run(s,a,40);assert.equal(e.calls.length,1,'echo waits .8 s');run(s,a,6);assert.equal(e.calls.length,2,'echo');const echo=e.calls[1];
  assert.equal(echo.echo,true);near(echo.scale,first.scale*.5,1e-12);assert.notEqual(echo.cast,first.cast);assert.deepEqual(echo.aim,first.aim);run(s,a,90);assert.equal(e.calls.length,2,'echo never re-echoes');
  a.mana=100;assert.equal(s.abilities.press(a,'q'),'cast');run(s,a,10);assert.equal(q.calls.length,1);const shots=Math.ceil(.6/WEAPONS.fire.interval);assert.deepEqual({mult:a.empower!.mult,shots:a.empower!.shots},{mult:1.5,shots});
  assert.equal(s.snapshot().players[0].empowerShots,shots);const t=s.abilities.shotTuning(a);assert.equal(t.damage,1.5);assert.equal(a.empower?.shots??0,shots-1);for(let i=1;i<shots;i++)s.abilities.shotTuning(a);assert.equal(a.empower,undefined);assert.equal(s.abilities.shotTuning(a).damage,1);
 }finally{s.dispose();}}finally{for(const f of [e,q]){delete ABILITY_HANDLERS[f.def.id];f.def.live=undefined;}}
});
test('leech and surge count each damage once: 1 player + 1 add heals .2 × (player + add actual) in one heal per .25 s',()=>{
 const {s,a,b}=game('fire');try{a.stats={...a.stats,abilityLeech:.2,manaSurge:.1};a.hp=40;a.mana=10;b.body.setTranslation(v(1.5,.9,4),true);s.chaos.summon(b,1,'zombie',v(-1.5,.9,4),30);s.world.step();
  const z=[...s.chaos.minions.values()][0];z.body.setTranslation(v(-1.5,.9,4),true);s.world.step();const bHp=b.hp,zHp=z.hp;s.events=[];
  const r=s.abilities.blast(a,v(0,.9,4),4,30,0,'meteor-strike');assert.ok(r.players>0&&r.minions>0,JSON.stringify(r));assert.deepEqual(r.hit,['b']);near(bHp-b.hp,r.players,1e-9,'player actual');near(zHp-z.hp,r.minions,1e-9,'add actual');
  near(a.mana,10+.1*(r.players+r.minions),1e-9,'surge once per damage');
  const hp0=a.hp;run(s,a,1);near(a.hp-hp0,.2*(r.players+r.minions),1e-9,'leech heals exactly once');assert.equal(events(s,'heal').filter(e=>e.target==='a').length,1);
  const hp1=a.hp;s.abilities.blast(a,v(0,.9,4),4,30,0,'meteor-strike');run(s,a,5);assert.equal(a.hp,hp1,'no second flush within .25 s');run(s,a,12);assert.ok(a.hp>hp1);
  const heals=s.events.filter(e=>e.type==='heal'&&e.target==='a').map(e=>e.time);for(let i=1;i<heals.length;i++)assert.ok(heals[i]-heals[i-1]>=.25-1e-9);
 }finally{s.dispose();}
});

// ---- ability fields -------------------------------------------------------------------------------------------------
test('ability-field pool: 4 rapid casts with forced charges and echo all detonate; eviction detonates; chip caps unchanged',()=>{
 const {s,a}=game('fire');try{a.stats={...a.stats,abilityCharges:1,spellEcho:1};
  for(let i=0;i<4;i++)s.abilities.zone(a,'bomb',v(i*3-4,.2,10),2,1,20,'meteor-strike',{cast:'c'+i});assert.equal([...s.chaos.fields.values()].length,4);
  s.events=[];run(s,a,70);assert.equal(events(s,'explosion').filter(e=>e.message==='meteor-strike').length,4,'every cast produced its finale');assert.equal(s.chaos.fields.size,0);
  for(let i=0;i<4;i++)s.abilities.zone(a,'bomb',v(i*3-4,.2,10),2,5,20,'meteor-strike',{cast:'d'+i});s.events=[];s.abilities.zone(a,'bomb',v(8,.2,10),2,5,20,'meteor-strike',{cast:'d4'});
  assert.equal(events(s,'explosion').filter(e=>e.message==='meteor-strike').length,1,'oldest group detonated on eviction');assert.equal(s.chaos.fields.size,4);
  // Fields of one cast group share a slot; implosion eviction also vacuums.
  s.chaos.clear();a.stats={...a.stats,abilityCharges:0,spellEcho:0};for(const c of ['x','x','y'])s.abilities.zone(a,'implosion',v(0,.2,12),3,5,20,'singularity',{cast:c});assert.equal(s.chaos.fields.size,3);s.events=[];s.abilities.zone(a,'black-hole',v(0,.2,12),6,5,0,'singularity',{cast:'z'});
  assert.equal(events(s,'vacuum').length,2,'both implosions of the evicted group detonated');assert.equal(s.chaos.fields.size,2);
  // Chip pool: per kind 3, 40 global, untouched by ability fields.
  s.chaos.clear();for(let i=0;i<5;i++)s.chaos.field(a,'bomb',v(i,.2,0),2,9,1,undefined,'bubble-mine');assert.equal(s.chaos.fields.size,3);
  s.chaos.clear();const owners=[a,s.players.get('b')!];for(let i=0;i<50;i++)s.chaos.field(owners[i%2],(['acid','fire','frost','time','heal','storm','repulsion','black-hole'] as const)[Math.floor(i/2)%8],v(i%10,.2,i/10),1,9,0,undefined,'wildfire');assert.equal(s.chaos.fields.size,40);
  const before=[...s.chaos.fields.keys()];for(let i=0;i<6;i++)s.abilities.zone(a,'bomb',v(0,.2,-10),2,9,1,'frag',{cast:'f'+i});assert.deepEqual([...s.chaos.fields.values()].filter(f=>!ABILITY_FX.has(f.fx||'')).map(f=>f.id),before,'ability fields never evict chip fields');
 }finally{s.dispose();}
});
test('fields: lockAt freezes an attached field; start delays effects; damageKind and force apply; chip fields keep their defaults',()=>{
 const {s,a,b}=game('darkness');try{
  const f=s.abilities.zone(a,'bomb',b.body.translation(),3,1,40,'orbital-mark',{attach:'b',lockAt:s.time+.3,cast:'m'});run(s,a,6);assert.ok(len(sub(f.pos,b.body.translation()))<.05,'follows before lock');
  run(s,a,20);b.body.setTranslation(v(4,.9,7),true);s.world.step();run(s,a,2);assert.ok(len(sub(f.pos,b.body.translation()))>3,'locked position');
  s.chaos.clear();const g=s.abilities.zone(a,'black-hole',v(0,.2,7),6,3,8,'singularity',{force:18,start:s.time+.5,damageKind:'ability',cast:'h'});b.ext=v();const hp=b.hp;run(s,a,10);assert.equal(b.hp,hp,'no damage before start');
  run(s,a,30);assert.ok(b.hp<hp);const hit=s.events.filter(e=>e.type==='hit'&&e.target==='b').at(-1)!;assert.equal(hit.message,'ability');assert.equal(g.force,18);
  s.chaos.clear();s.abilities.zone(a,'frost',b.body.translation(),4,3,0,'singularity',{start:s.time+.5,speedFactor:.5,cast:'fr'});assert.equal(s.abilities.motionScale(b),1,'a delayed frost field does not slow before start');run(s,a,40);assert.equal(s.abilities.motionScale(b),.5);
  const chip=s.chaos.field(a,'black-hole',v(0,.2,0),5,3,10,undefined,'black-hole');assert.deepEqual(Object.keys(chip).sort(),['arm','attach','born','damage','fx','id','kind','owner','pos','radius','until']);
 }finally{s.dispose();}
});

test('field ends: handler.detonate sees every ability field end (eviction too); the default only explodes bombs, implosions and triggered mines',()=>{
 const {def}=fake('darkness','e');const ends:string[]=[];ABILITY_HANDLERS[def.id].detonate=(_sys,f,_owner,cause)=>{ends.push(f.kind+':'+cause);};
 try{const {s,a,b}=game('darkness');try{s.abilities.zone(a,'black-hole',v(0,.2,10),6,.5,0,'singularity',{cast:'h'});s.abilities.zone(a,'implosion',v(0,.2,10),4,.5,30,'singularity',{cast:'h'});run(s,a,40);assert.deepEqual(ends.sort(),['black-hole:until','implosion:until']);
  for(const c of ['p','q'])s.abilities.zone(a,'black-hole',v(0,.2,10),6,5,0,'singularity',{cast:c});ends.length=0;s.abilities.zone(a,'black-hole',v(0,.2,10),6,5,0,'singularity',{cast:'r'});assert.deepEqual(ends,['black-hole:evict'],'an evicted non-bomb field reaches handler.detonate');s.chaos.clear();
  delete ABILITY_HANDLERS[def.id].detonate;s.events=[];s.abilities.zone(a,'mine',v(0,.2,-12),2,.5,20,'frag',{cast:'m'});s.abilities.zone(a,'fire',v(0,.2,-12),2,.5,5,'frag',{cast:'m'});run(s,a,40);assert.equal(events(s,'explosion').length,0,'expired mine and fire end quietly');
  s.abilities.zone(a,'bomb',v(0,.2,-12),2,.5,20,'frag',{cast:'b'});run(s,a,40);assert.deepEqual(events(s,'explosion').map(e=>e.message),['frag']);
  b.body.setTranslation(v(0,.9,7),true);b.body.setLinvel(v(),true);b.ext=v();s.events=[];s.abilities.zone(a,'mine',v(0,.2,7),2,5,20,'frag',{cast:'d',start:s.time+1.2});run(s,a,Math.round(C.simulationHz*.9));assert.equal(events(s,'explosion').length,0,'an armed mine waits for its start');run(s,a,Math.round(C.simulationHz*.5));assert.equal(events(s,'explosion').length,1);
 }finally{s.dispose();}}finally{delete ABILITY_HANDLERS[def.id];def.live=undefined;}
});
// ---- slows ----------------------------------------------------------------------------------------------------------
test('slows: lowest factor wins with a .4 floor, never multiply with fields, 1 s immunity after a strong slow; pull ×.5 on slowed targets',()=>{
 const {s,a,b}=game('darkness');try{
  assert.ok(s.abilities.slow(b,.8,1,'soak','a'));assert.ok(s.abilities.slow(b,.6,.5,'tidal','a'));assert.deepEqual({f:b.slowed!.factor,k:b.slowed!.kind},{f:.6,k:'tidal'});near(b.slowed!.until-s.time,1,1e-9);
  s.abilities.slow(b,.2,.2,'shackles','a');assert.equal(b.slowed!.factor,.4,'floor');const view=()=>s.snapshot().players.find(p=>p.id==='b')!;assert.equal(view().motionScale,.4);assert.deepEqual(view().slowed,{...b.slowed});
  s.chaos.field(a,'frost',b.body.translation(),3,3,0,undefined,'blizzard');assert.equal(view().motionScale,.4,'never multiplies');
  run(s,a,70);assert.equal(b.slowed,undefined);assert.ok((b.slowImmuneUntil??0)>s.time,'immunity after a strong slow');assert.equal(s.abilities.slow(b,.5,1,'x','a'),false);assert.equal(view().motionScale,.45,'frost alone');
  run(s,a,65);assert.equal(b.slowImmuneUntil,undefined);assert.equal(s.abilities.slow(b,.9,.1,'light','a'),true);run(s,a,10);assert.equal(b.slowImmuneUntil,undefined,'weak slows grant no immunity');
  const boss=s.addPlayer('boss','Siren','melee',null,'#fff',{preferredSpec:'siren'},{developer:true,unlocks:['weapon:siren']})!;boss.spec='siren';assert.equal(s.abilities.slow(boss,.5,1,'x','a'),false,'Siren is immune');
  // Pull ×.5 on ability fields when the target is slowed below .6.
  s.chaos.clear();const pull=(slowed:boolean)=>{b.ext=v();b.slowed=slowed?{factor:.5,until:s.time+5,kind:'t'}:undefined;b.slowImmuneUntil=undefined;s.chaos.field(a,'black-hole',add(b.body.translation(),v(3,0,0)),6,2,0,undefined,'singularity',{force:18,cast:'p'+slowed});s.chaos.step(1/60);const x=b.ext.x;s.chaos.clear();return x;};
  const free=pull(false),held=pull(true);assert.ok(free>0);near(held,free*.5,1e-9,'halved pull');
 }finally{s.dispose();}
});

// ---- holds, snapshot omission ---------------------------------------------------------------------------------------
test('PvE hold shift moves every absolute ability time (cast, buffer, slow, immunity, empower, ADS, leech, fuses, fields, timeline)',()=>{
 const {s,a,b}=game('fire');try{let fired=0;s.abilities.schedule(s.time+1,()=>{fired++;});
  a.abilityCast={slot:'e',id:'meteor-strike',at:5,aim:{yaw:0,pitch:0,camera:'first'},cast:'c'};a.pendingPress={slot:'q',at:4};a.slowed={factor:.5,until:6,kind:'k'};a.slowImmuneUntil=7;a.empower={until:8,mult:1.2,shots:2};a.adsSince=3;a.adsBloom={n:1,at:3.5};a.leech={hp:2,at:2};
  const f=s.abilities.zone(a,'bomb',v(0,.2,0),2,3,10,'meteor-strike',{start:s.time+.5,lockAt:s.time+.4,cast:'z'}),chip=s.chaos.field(b,'bomb',v(0,.2,5),2,3,10,undefined,'bubble-mine');
  const shot=s.abilities.spawnShot(a,MAGIC_KITS.fire.q,{cast:'s',fuse:s.time+2})!;const before=JSON.parse(JSON.stringify({f,chip,born:shot.born,fuse:shot.ability!.fuse}));
  s.abilities.shift(2);
  assert.deepEqual([a.abilityCast.at,a.pendingPress.at,a.slowed.until,a.slowImmuneUntil,a.empower.until,a.adsSince,a.adsBloom.at,a.leech.at],[7,6,8,9,10,5,5.5,4]);
  for(const k of ['born','until','arm','start','lockAt'] as const)near((f as any)[k],before.f[k]+2,1e-9,k);assert.deepEqual(JSON.parse(JSON.stringify(chip)),before.chip,'chip fields keep today\'s behaviour');
  near(shot.born!,before.born+2,1e-9);near(shot.ability!.fuse!,before.fuse+2,1e-9);const t0=s.time;s.abilities.step(0);s.time=t0+2.9;s.abilities.step(0);assert.equal(fired,0,'timeline shifted by 2 s');s.time=t0+3.05;s.abilities.step(0);assert.equal(fired,1);
 }finally{s.dispose();}
});
test('snapshot omission: a fresh pistol snapshot carries none of the new keys; the ability extras appear only when set',()=>{
 const {s,a}=game('pistol');try{const keys=['abilityCd','lastAbility','abilityCastAt','adsSince','eCharges','slowed','slowImmuneUntil','empowerUntil','empowerShots'];
  const view=JSON.parse(JSON.stringify(s.snapshot()));for(const p of view.players)for(const k of keys)assert.equal(k in p,false,k);tick(s,a,{attack:true});for(const p of JSON.parse(JSON.stringify(s.snapshot())).projectiles)assert.equal('ability'in p,false);
  for(const p of s.snapshot().players)for(const k of keys)assert.equal(k in p,false,'no undefined-valued keys either: '+k);
  run(s,a,3,{ability:true});const me=s.snapshot().players.find(p=>p.id==='a')!;assert.ok(me.adsSince!==undefined);
  a.abilityCd=2;a.lastAbility=s.time;s.abilities.spawnShot(a,GUN_KITS.machinegun.e,{cast:'r',fuse:s.time+1});const snap=s.snapshot();assert.equal(snap.players.find(p=>p.id==='a')!.abilityCd,2);assert.deepEqual(snap.projectiles.find(p=>p.ability)!.ability,{id:'rocket',fuse:s.time+1});
 }finally{s.dispose();}
});

// ---- prediction parity ----------------------------------------------------------------------------------------------
function predicted(o:{abilities:boolean;strip?:boolean;input:Partial<Input>;setup?:(s:Simulation,guest:Player)=>void;ticks?:number}){
 const s=new Simulation({sandbox:true}),guest=s.addPlayer('guest','G','ranged')!,host=s.addPlayer('host','H','ranged')!;s.phase='fight';
 for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});
 guest.body.setTranslation(v(0,.9,0),true);host.body.setTranslation(v(0,.9,-20),true);for(let i=0;i<40;i++){for(const p of s.players.values())p.inputAt=s.time;s.step();}
 o.setup?.(s,guest);const predictor=new Predictor({abilities:o.abilities}),snap=s.snapshot(),me=snap.players.find(p=>p.id==='guest')!;predictor.reconcile(snap,me);let seq=guest.input.seq,worst=0;
 try{for(let n=0;n<(o.ticks??120);n++){const input={...idleInput(),seq:++seq,...o.input};const sent={...input};if(o.strip)delete (sent as Partial<Input>).ability;s.setInput('guest',sent);for(const p of s.players.values())p.inputAt=s.time;s.step();predictor.step(input,1/60,me);worst=Math.max(worst,len(sub(predictor.pos,guest.body.translation())));}
  return {worst,moved:len(sub(guest.body.translation(),me.pos)),guest};}finally{predictor.dispose();s.dispose();}
}
test('prediction: ADS walk parity < .01 m; caps-absent client against an old host (ability stripped) < .01 m',()=>{
 const ads=predicted({abilities:true,input:{z:1,x:.3,ability:true}});assert.ok(ads.worst<.01,`ADS parity ${ads.worst}`);assert.ok(ads.moved>8);
 const old=predicted({abilities:false,strip:true,input:{z:1,ability:true}});assert.ok(old.worst<.01,`caps-absent parity ${old.worst}`);
 const hip=predicted({abilities:true,input:{z:1}});assert.ok(ads.moved<hip.moved*.85,'ADS is slower');
});
test('prediction: black-hole pull parity < .05 m over 120 ticks for an ability black hole and a chip black hole',()=>{
 const ability=predicted({abilities:true,input:{},setup:(s,g)=>{s.chaos.field(s.players.get('host')!,'black-hole',add(g.body.translation(),v(4,-.7,0)),6,3,0,undefined,'singularity',{force:18,cast:'h'});}});
 assert.ok(ability.worst<.05,`ability pull parity ${ability.worst}`);assert.ok(ability.moved>.5,`pulled ${ability.moved}`);
 const chip=predicted({abilities:true,input:{},setup:(s,g)=>{s.chaos.field(s.players.get('host')!,'black-hole',add(g.body.translation(),v(4,-.7,0)),5,3,10,undefined,'black-hole');}});
 assert.ok(chip.worst<.05,`chip pull parity ${chip.worst}`);assert.ok(chip.moved>.5);
});

// ---- pong hooks -----------------------------------------------------------------------------------------------------
// Rally Pong v2 (chips stream, design §9.2/§9.3) replaced the extracted 40 % endless single ball on purpose; this case
// keeps checking the simulation → pong.ts hook path with the v2 contract. Rallies are covered in rarity-combat.test.ts.
test('pong hooks: clamped no-drop balls with a 3.5 s life, several balls in play, wall and bounds ricochets',()=>{
 const {s,a,b}=game('pistol',{pong:1});try{a.stats=statsFor(a.chips,a.spec);b.body.setTranslation(v(20,.9,-20),true);tick(s,a,{attack:true,pitch:.02});const ball=[...s.projectiles.values()][0];assert.ok(ball?.pong);
  near(len(ball.vel),Math.min(PONG.maxSpeed,Math.max(PONG.minSpeed,WEAPONS.pistol.speed)),1e-9);assert.ok(ball.life<=PONG.life);assert.ok(ball.distanceLeft<=PONG.travel);assert.equal(ball.gravity,0);assert.equal(ball.homing,0);assert.equal(ball.returning,false);
  run(s,a,30);assert.equal(s.projectiles.size,1);near(len(ball.vel),Math.min(PONG.maxSpeed,Math.max(PONG.minSpeed,WEAPONS.pistol.speed)),1e-9,'speed renormalised');run(s,a,30,{attack:true});assert.ok([...s.projectiles.values()].filter(q=>q.originOwner==='a').length>=2,'several balls');
  s.events=[];const q=[...s.projectiles.values()][0];q.pos=v(31.9,3,0);q.vel=v(52,0,0);run(s,a,2);assert.ok(events(s,'ricochet').length>=1);assert.ok(q.vel.x<0,'bounds bounce');
 }finally{s.dispose();}
});

// ---- hints and HUD --------------------------------------------------------------------------------------------------
test('control hints: today\'s hints without caps; with caps rifle/pistol/MG ≤ 10 keys, magic 8 (9 once its Q is live), no DEV names',()=>{
 const kbd=(h:string)=>(h.match(/<kbd>/g)||[]).length;
 for(const spec of Object.keys(WEAPONS) as SpecId[]){assert.equal(controlHints(spec,'KeyQ',undefined),controlHints(spec),spec);assert.equal(controlHints(spec,'KeyQ',{}),controlHints(spec),spec);}
 // Rifle keeps `Q Scope` and 10 keys; its E label is the kit name once the E is live (GUNS), else today's `Special`.
 {const h=controlHints('rifle','KeyQ',HOST_CAPS),e=kitFor('rifle')!.e;assert.equal(kbd(h),10);assert.match(h,/<kbd>Q<\/kbd>Scope/);assert.ok(h.includes(`<kbd>E</kbd>${e.live?e.name:'Special'}`),'rifle E label');if(!e.live)assert.equal(h,controlHints('rifle'),'rifle unchanged: Q Scope');}
 for(const spec of ['pistol','machinegun'] as const){const h=controlHints(spec,'KeyQ',HOST_CAPS);assert.equal(kbd(h),10);assert.match(h,/<kbd>Q<\/kbd>Aim/);}
 // MAGIC sets every magic Q/E live (design §17 M): 9 keys then; the fake-handler branch runs only while a school is not live.
 for(const spec of ['fire','water','lightning','darkness'] as const){const k=kitFor(spec)!;if(k.q.live&&k.e.live){const h=controlHints(spec,'KeyQ',HOST_CAPS);assert.equal(kbd(h),9);assert.ok(h.includes(`<kbd>Q</kbd>${k.q.name}`)&&h.includes(`<kbd>E</kbd>${k.e.name}`),spec);continue;}assert.equal(kbd(controlHints(spec,'KeyQ',HOST_CAPS)),8);const q=fake(spec,'q'),e=fake(spec,'e');try{const h=controlHints(spec,'KeyQ',HOST_CAPS);assert.equal(kbd(h),9);assert.ok(h.includes(`<kbd>Q</kbd>${q.def.name}`));assert.ok(h.includes(`<kbd>E</kbd>${e.def.name}`));}finally{for(const f of [q,e]){delete ABILITY_HANDLERS[f.def.id];f.def.live=undefined;}}}
 for(const spec of Object.keys(WEAPONS) as SpecId[]){const h=controlHints(spec,'KeyQ',HOST_CAPS);assert.ok(kbd(h)<=10,spec);if(!abilityUser(spec))assert.equal(h,controlHints(spec),spec);}
});
test('ability HUD: slot views and markup on capable hosts only',()=>{
 const {s,a}=game('pistol');try{const me=()=>s.snapshot().players.find(p=>p.id==='a')!;
  assert.deepEqual(abilitySlots(me(),s.time,{}),[]);assert.equal(abilitySlotsHtml([]),'');
  let slots=abilitySlots(me(),s.time,HOST_CAPS);assert.deepEqual(slots.map(x=>[x.key,x.mode,x.state]),[['Q','hold','ready'],['E','press','ready']]);
  run(s,a,2,{ability:true});slots=abilitySlots(me(),s.time,HOST_CAPS);assert.equal(slots[0].state,'active');a.secondaryCd=1.2;slots=abilitySlots(me(),s.time,HOST_CAPS);assert.equal(slots[1].state,'cooldown');
  const html=abilitySlotsHtml(slots);assert.match(html,/class="cooldown ability" data-slot="Q" data-state="active"/);assert.match(html,/data-slot="E" data-state="cooldown" style="--p:/);
  const f=s.players.get('a')!;f.spec='fire';f.chips={necronomicon:1};f.stats=statsFor(f.chips,f.spec);assert.deepEqual(abilitySlots(me(),s.time,HOST_CAPS).map(x=>x.short),['RAISE THE DEAD']);
  f.chips={necronomicon:1,efficient:1};f.stats=statsFor(f.chips,f.spec);f.mana=31;f.secondaryCd=0;{const [n]=abilitySlots(me(),s.time,HOST_CAPS);near(n.cost!,35*.86,1e-9,'the card shows the cost the gate uses');assert.equal(n.state,'ready');f.mana=29;assert.equal(abilitySlots(me(),s.time,HOST_CAPS)[0].state,'nomana');}
  for(const spec of ['sword','emperor','leet','siren'] as SpecId[]){f.spec=spec;assert.deepEqual(abilitySlots(me(),s.time,HOST_CAPS),[]);}
 }finally{s.dispose();}
});

// ---- chips plumbing ------------------------------------------------------------------------------------------------
test('chips: eligibility tags per kit and symmetric excludes; no shipped chip uses them yet (offers stay identical)',()=>{
 // The chips stream's MAGIC_CHIPS and Rally Pong v2 (design §8, §9.2) are the intended users; no other chip uses them.
 assert.ok(CHIPS.filter(c=>!MAGIC_CHIPS.includes(c)&&c.id!=='pong').every(c=>!c.excludes&&!c.prose&&!c.tags.some(t=>['ability','spellcraft','gunplay'].includes(t)||t.startsWith('spell-'))));
 const probe={id:'probe-a',rarity:'common' as const,name:'A',description:'',tags:['spellcraft'],max:1,icon:'+',effects:[],excludes:['probe-b']},other={id:'probe-b',rarity:'common' as const,name:'B',description:'',tags:['spell-fire'],max:1,icon:'+',effects:[]},gun={...other,id:'probe-c',tags:['gunplay']};
 CHIPS.push(probe,other,gun);try{
  const ids=(spec:SpecId,held:Record<string,number>={})=>eligibleChips(spec,held).map(c=>c.id).filter(id=>id.startsWith('probe-'));
  assert.deepEqual(ids('fire'),['probe-a','probe-b']);assert.deepEqual(ids('water'),['probe-a']);assert.deepEqual(ids('pistol'),['probe-c']);for(const spec of ['leet','sword','emperor'] as SpecId[])assert.deepEqual(ids(spec),[]);
  assert.deepEqual(ids('fire',{'probe-a':1}),[]);assert.deepEqual(ids('fire',{'probe-b':1}),[],'symmetric');
 }finally{CHIPS.splice(CHIPS.length-3,3);}
});

// ---- damage plumbing ------------------------------------------------------------------------------------------------
test('damage plumbing: explode returns per-target actual damage; hitMinion returns actual with the overkill clamp',()=>{
 const {s,a,b}=game('fire');try{s.chaos.summon(b,1,'zombie',v(1,.9,4),30);s.world.step();const z=[...s.chaos.minions.values()][0];z.hp=5;
  const r=s.explode(a,v(0,.9,5),4,30,0,'blast',false);assert.ok(r.players>0);assert.deepEqual(r.hit,['b']);near(r.minions,5,1e-9,'overkill clamp');
  s.chaos.summon(b,1,'zombie',v(-1,.9,4),30);const z2=[...s.chaos.minions.values()].at(-1)!;assert.equal(s.chaos.hitMinion(z2,12,'a'),12);assert.equal(s.chaos.hitMinion(z2,12,'b'),0,'own minion');
  const none=s.explode(a,v(0,.9,-20),2,30,0,'blast',false);assert.deepEqual(none,{players:0,minions:0,hit:[]});
 }finally{s.dispose();}
});
test('direct ability hits heal by chip life steal only; blasts and zones never do',()=>{
 const {s,a,b}=game('darkness');try{a.stats={...a.stats,lifeSteal:.25};a.hp=50;const actual=s.abilities.hit(a,b,20,v(0,0,1),0,b.body.translation(),{direct:true});near(a.hp,50+actual*.25,1e-9,'chip life steal, no darkness drain');
  a.hp=50;s.abilities.hit(a,b,20,v(0,0,1),0,b.body.translation());assert.equal(a.hp,50);a.hp=50;s.abilities.blast(a,b.body.translation(),3,20,0,'singularity');assert.equal(a.hp,50);
 }finally{s.dispose();}
});

// ---- bots -----------------------------------------------------------------------------------------------------------
test('bots: the live-kit branch never falls into the legacy rows; point casts need LOS (recruits pass visible=true)',()=>{
 const {def,calls}=fake('pistol','e');def.bot={min:6,max:22,aim:'point'};try{const {s,a,b}=game('pistol');try{a.bot='hard';
  const decide=(target:Vec)=>{const i={...idleInput(),attack:true};let cast=false;for(let k=0;k<40&&!cast;k++){i.secondary=false;botAbilities(s,a,i,len(sub(target,a.body.translation())),true,2,target);cast=i.secondary;}return {cast,i};};
  const clear=decide(b.body.translation());assert.equal(clear.cast,true,'open target');assert.equal(clear.i.camera,'first');assert.equal(clear.i.attack,false);
  s.addProp({id:'wall',pos:v(0,1.5,3.5),size:v(6,3,.4),material:'foundation'});s.world.step();const blocked=decide(b.body.translation());assert.equal(blocked.cast,false,'no point cast behind a wall');assert.deepEqual([blocked.i.yaw,blocked.i.pitch],[idleInput().yaw,idleInput().pitch],'a refused cast leaves the aim alone (no shot at the feet)');
  const close=decide(v(0,.9,3));assert.equal(close.cast,false,'below min keeps the primary');assert.equal(close.i.attack,true);
  a.reloadTime=1;s.removeProp('wall');s.world.step();const reloading=decide(b.body.translation());assert.equal(reloading.cast,true,'E works while reloading');assert.equal(calls.length,0);
 }finally{s.dispose();}}finally{delete ABILITY_HANDLERS[def.id];def.live=undefined;def.bot=undefined;}
 // F neutrality: a kit without live E or Q bot data keeps the legacy path and no ability bit (every kit in F; the magic kits
 // until MAGIC lands; GUNS asserts its live bots in gun-abilities.test.ts).
 for(const spec of (['pistol','rifle','machinegun','fire','water','lightning','darkness'] as SpecId[]).filter(spec=>!kitFor(spec)!.e.live&&!kitFor(spec)!.q.bot)){const {s,a,b}=game(spec);try{a.bot='hard';assert.equal(s.abilities.botKit(spec),false);assert.equal(s.abilities.botDesired(a),undefined);const i=idleInput();botAbilities(s,a,i,len(sub(b.body.translation(),a.body.translation())),true,2,b.body.translation());assert.equal(i.ability,false);}finally{s.dispose();}}
});

// ---- ground aim ----------------------------------------------------------------------------------------------------
test('groundAimPoint: crosshair hit, range clamp with ground projection, arena clamp',()=>{
 const floorRay=(o:Vec,d:Vec,max:number)=>{if(d.y>=0)return null;const t=(o.y-0)/-d.y;return t<=max?t:null;};
 const hit=groundAimPoint(v(0,2,0),{x:0,y:-Math.SQRT1_2,z:Math.SQRT1_2},40,true,floorRay);near(hit.point.z,2,1e-9);near(hit.point.y,0,1e-9);assert.equal(hit.clamped,false);
 const far=groundAimPoint(v(0,2,0),v(0,0,1),20,true,floorRay);near(far.point.z,20,1e-9);near(far.point.y,0,1e-9);assert.equal(far.clamped,true);
 const air=groundAimPoint(v(0,2,0),v(0,0,1),20,false,floorRay);near(air.point.y,2,1e-9);
 const edge=groundAimPoint(v(0,2,0),v(1,0,0),80,true,floorRay);assert.equal(edge.point.x,31);assert.equal(edge.clamped,true);
});
