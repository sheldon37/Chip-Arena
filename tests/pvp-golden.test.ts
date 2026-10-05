// Golden PvP traces (PvE foundation, design §11 F step 0). The expected hashes were generated on the untouched
// base b6d2f58 before any PvE edit. Every PvE hook must leave these four PvP traces byte-identical.
// Regenerate only on a deliberate PvP behaviour change: PVP_GOLDEN_PRINT=1 node --import tsx --test tests/pvp-golden.test.ts
// Regenerated once on 2026-10-04 (claude/abil-integration, ability overhaul, design §15): ffa-foundry, teams-moonlit,
// siren-ward-zero and necronomicon carry ranged/magic players, whose Q/E kits, magic primaries (speed/drop/damage, Soak,
// enemy chain), removed legacy E and retuned numbers deliberately change PvP behaviour. melee-dev and siren-melee stayed identical.
// Regenerated 2026-10-04 (claude/intros-refresh, owner bug: ADS sights must match impacts): ffa-foundry and teams-moonlit only.
// Their gun bots aim down sights; ADS shots are now launched on the arc that meets the crosshair point (ADS bots aim straight).
// Verified: with that zeroing disabled both traces match the previous hashes; the 3 s lobby return is not in any trace.
// Regenerated 2026-10-04 (claude/melee-stamina, owner: lower melee attack stamina by a good portion on ALL melee): melee-dev,
// siren-melee and siren-ward-zero only (they carry standard melee players). Primary swings cost 40% less and echo follow-ups 3 (was 5).
// Verified: with MELEE_COST_SCALE=1 and followupCost 5 all seven traces match the previous hashes.
// Regenerated 2026-10-04 (claude/summons-slender, owner: summons get real attacks and health): necronomicon only (its zombies
// now wind up and strike on a frame, chase the nearest foe, take DoTs, emit hit/death events and snapshot whole hp).
// The other six traces contain no summon and stayed byte-identical (checked before regenerating).
// Regenerated 2026-10-05 (codex/combat-flow-stamina): standard primary costs 7/14/16/20 and Echo Strike 2.
// Only siren-ward-zero, melee-dev and siren-melee change; the other three traces remain byte-identical.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {Simulation,initPhysics,type Options} from '../src/shared/simulation.js';
import {idleInput,type Input} from '../src/shared/config.js';
import {statsFor} from '../src/shared/content.js';
await initPhysics();

type Script=(k:number)=>Partial<Input>;
interface Trace{name:string;options:Options;setup:(s:Simulation)=>void;onFight?:(s:Simulation)=>void;steps:number;script?:Script;
 /** Sanity probe on every tick, so a trace cannot silently stop exercising the system it is named after. */
 seen?:(s:Simulation,seen:Set<string>)=>void;expectSeen?:string[]}

function digest(s:Simulation){const snap=s.snapshot() as unknown as Record<string,unknown>;const {metrics:_metrics,...rest}=snap;return createHash('sha256').update(JSON.stringify(rest)).digest('hex').slice(0,16);}

/** Drives lobby → specialization → intros → countdown → fight with the real flow, then `steps` fight ticks.
 *  Hashes every 60th tick of the whole run (pre-fight phases included) and the final tick. */
function run(t:Trace){
 const s=new Simulation(t.options),hashes:string[]=[],seen=new Set<string>();let seq=0,fightTicks=0,armed=false;
 try{
  t.setup(s);s.start();
  const host=s.players.get('host');if(host&&!host.bot)s.selectSpec('host',host.spec);
  for(let guard=0;guard<6000&&fightTicks<t.steps;guard++){
   if(s.phase==='fight'){
    if(!armed){armed=true;t.onFight?.(s);}
    if(host&&t.script){const input={...idleInput(),seq:++seq,...t.script(fightTicks)};s.queueInputs('host',[input],`${s.setNumber}:${s.round}:${s.generation}`);}
    fightTicks++;
   }
   s.step();t.seen?.(s,seen);
   if(s.tick%60===0)hashes.push(`${s.tick}:${s.phase}:${digest(s)}`);
  }
  hashes.push(`end:${s.tick}:${s.phase}:${digest(s)}`);
  assert.equal(fightTicks,t.steps,`${t.name} reached ${t.steps} fight-ready ticks`);
  for(const k of t.expectSeen??[])assert.ok(seen.has(k),`${t.name} exercised ${k}`);
  return hashes;
 }finally{s.dispose();}
}

const strafe:Script=k=>({x:Math.sin(k/37),z:Math.cos(k/53)*.6,yaw:k*.013,pitch:-.05,attack:k%45<6,jump:k%170===0,dash:k%240===120,camera:'third'});

/** Siren trace host: fights for 300 ticks, then retreats from the Siren (beyond 8 m) so the bot toggles to its ranged phase. */
let retreatSim:Simulation|undefined;
const retreat:Script=k=>{const s=retreatSim,host=s?.players.get('host'),boss=s?.players.get('boss');if(!host||!boss||k<300||k>=720)return {x:Math.sin(k/41)*.8,z:Math.cos(k/29)*.8,yaw:k*.011,attack:k%50<4,camera:'third'};
 const a=host.body.translation(),b=boss.body.translation(),dx=a.x-b.x,dz=a.z-b.z,l=Math.hypot(dx,dz)||1,edge=Math.abs(a.x)>20||Math.abs(a.z)>18;return {x:edge?-Math.sign(a.x)*.6:dx/l,z:edge?-Math.sign(a.z)*.6:dz/l,yaw:Math.atan2(-dx,-dz),attack:false,dash:k%90===0,camera:'third'};};

const TRACES:Trace[]=[
 {name:'ffa-foundry',options:{map:'foundry',skipIntros:true},steps:600,script:strafe,expectSeen:['projectile'],seen:(s,seen)=>{if(s.projectiles.size)seen.add('projectile');},setup:s=>{
  s.addPlayer('host','Host','ranged');
  s.addPlayer('bot','Comet Bot','ranged','hard','#70e1d0',{preferredSpec:'rifle'});
  s.addPlayer('bot-2','Circuit Bot','magic','normal','#e7ac78',{preferredSpec:'fire'});
  s.addPlayer('bot-3','Blitz Bot','melee','hard','#b591ef',{preferredSpec:'sword'});
 }},
 {name:'teams-moonlit',options:{map:'moonlit',gameMode:'teams',skipIntros:true},steps:600,expectSeen:['damage'],seen:(s,seen)=>{if(s.events.some(e=>e.type==='hit'||e.type==='death'))seen.add('damage');},setup:s=>{
  s.addPlayer('bot','Comet Bot','ranged','hard','#70e1d0',{preferredSpec:'machinegun'});
  s.addPlayer('bot-2','Circuit Bot','magic','hard','#e7ac78',{preferredSpec:'lightning'});
  s.addPlayer('bot-3','Blitz Bot','melee','normal','#b591ef',{preferredSpec:'greatsword'});
  s.addPlayer('bot-4','Echo Bot','ranged','normal','#e27999',{preferredSpec:'pistol'});
  assert.deepEqual([...s.players.values()].map(p=>p.team),['red','blue','red','blue']);
 }},
 {name:'siren-ward-zero',options:{},steps:900,expectSeen:['map:siren','boss-song','siren-move'],seen:(s,seen)=>{if(s.options.map==='siren')seen.add('map:siren');if(s.bossSong)seen.add('boss-song');if(s.players.get('boss')?.siren?.move)seen.add('siren-move');},script:k=>({x:Math.sin(k/41)*.8,z:Math.cos(k/29)*.8,yaw:k*.011,attack:k%60<4}),setup:s=>{
  s.addPlayer('host','Host','ranged');
  s.addPlayer('boss','Siren','melee','hard','#ffffff',{preferredSpec:'siren'},{developer:false,unlocks:['weapon:siren']});
  assert.equal(s.players.get('boss')!.spec,'siren');
  s.addPlayer('bot','Comet Bot','ranged','normal','#70e1d0',{preferredSpec:'pistol'});
  s.addPlayer('bot-2','Circuit Bot','magic','normal','#e7ac78',{preferredSpec:'water'});
  s.addPlayer('bot-3','Blitz Bot','melee','normal','#b591ef',{preferredSpec:'daggers'});
 }},
 {name:'necronomicon',options:{map:'foundry',skipIntros:true},steps:600,script:k=>({secondary:k>=20&&k<40||k>=380&&k<400,attack:k%50>=25&&k%50<29&&!(k>=10&&k<45||k>=370&&k<405),yaw:k*.01,x:k%200<100?.4:-.4}),setup:s=>{
  s.addPlayer('host','Host','magic',null,'#ff6b35',{preferredSpec:'fire'});
  s.addPlayer('bot','Comet Bot','ranged','normal','#70e1d0',{preferredSpec:'pistol'});
 },onFight:s=>{const p=s.players.get('host')!;p.chips={necronomicon:1};p.stats=statsFor(p.chips,p.spec);p.hp=p.stats.hp;p.mana=p.stats.maxMana;},
  expectSeen:['zombie'],seen:(s,seen)=>{for(const m of s.chaos.minions.values())seen.add(m.kind);}},
 // Invariance traces (ranged/magic Q+E overhaul, design §15): generated on the untouched base cfdce4b before any
 // ability edit. They contain no non-DEV ranged or magic spec and must never change: proof that melee, DEV, L33T and
 // the Siren did not change. Do not regenerate them for the ability overhaul.
 {name:'melee-dev',options:{map:'foundry',skipIntros:true},steps:900,script:k=>({x:Math.sin(k/31)*.7,z:Math.cos(k/43)*.7,yaw:k*.012,pitch:-.04,attack:k%40<5,secondary:k%180===90,block:k%150===75,jump:k%210===0,dash:k%240===120,camera:'third'}),
  expectSeen:['emperor-electric','root'],seen:(s,seen)=>{for(const e of s.events){if(e.type==='emperor-electric')seen.add('emperor-electric');if(e.type==='explosion'&&e.message==='root')seen.add('root');}},setup:s=>{
  s.addPlayer('host','Host','melee',null,'#ff6b35',{preferredSpec:'sword'});
  s.addPlayer('bot','Comet Bot','melee','hard','#70e1d0',{preferredSpec:'greatsword'});
  s.addPlayer('bot-2','Circuit Bot','melee','normal','#e7ac78',{preferredSpec:'daggers'});
  s.addPlayer('bot-3','Blitz Bot','melee','hard','#b591ef',{preferredSpec:'shield'});
  s.addPlayer('bot-4','Echo Bot','melee','hard','#e27999',{preferredSpec:'emperor'},{developer:false,unlocks:['weapon:emperor']});
  s.addPlayer('bot-5','Nova Bot','melee','hard','#9ede79',{preferredSpec:'ansatsuken'},{developer:false,unlocks:['weapon:ansatsuken']});
  s.addPlayer('bot-6','Flux Bot','magic','hard','#79aeeb',{preferredSpec:'leet'},{developer:false,unlocks:['weapon:leet']});
  assert.deepEqual([...s.players.values()].map(p=>p.spec),['sword','greatsword','daggers','shield','emperor','ansatsuken','leet']);
 }},
 {name:'siren-melee',options:{skipIntros:true},steps:900,script:retreat,expectSeen:['map:siren','siren-ranged'],seen:(s,seen)=>{if(s.options.map==='siren')seen.add('map:siren');if(s.players.get('boss')?.siren?.mode==='ranged')seen.add('siren-ranged');},setup:s=>{
  retreatSim=s;
  s.addPlayer('host','Host','melee',null,'#ff6b35',{preferredSpec:'sword'});
  s.addPlayer('boss','Siren','melee','hard','#ffffff',{preferredSpec:'siren'},{developer:false,unlocks:['weapon:siren']});
  s.addPlayer('bot','Comet Bot','melee','normal','#70e1d0',{preferredSpec:'daggers'});
  s.addPlayer('bot-2','Circuit Bot','melee','normal','#e7ac78',{preferredSpec:'greatsword'});
  assert.deepEqual([...s.players.values()].map(p=>p.spec),['sword','siren','daggers','greatsword']);
 }},
];

const EXPECTED:Record<string,string[]>={
 "ffa-foundry":["60:countdown:41ec0dab413b4a92","120:countdown:7202f2fb6f4fc2e3","180:countdown:d09ecf24c7179820","240:fight:3dce81294f99e8fc","300:fight:9014284898dc453d","360:fight:a58a5ce7f9fb9ea6","420:fight:71bbe8343b7912fd","480:fight:4225118297e2e5e9","540:fight:e9ec50e21ce5b726","600:fight:bcaa02a1f8058e9e","660:fight:91ba03b78ed78c3a","720:fight:42d96752fd30a6f7","780:fight:2d082429607c3da8","end:782:fight:f918f7731bae7d96"],
 "teams-moonlit":["60:countdown:a5735e740bc360af","120:countdown:a46c1d81032730a2","180:countdown:ffcf15f4fc7a5da3","240:fight:e092fc043a231736","300:fight:d0f5c6695ae7e942","360:fight:00c8241c30cde5a7","420:fight:638f2055dd16397b","480:result:0642724239b573d6","540:result:09dcdde5d39dcb32","600:result:f14a7bf832dd9423","660:result:1c272b1b9a955d44","720:result:4ebf8f64146ceae5","780:countdown:f42135393fd682da","840:countdown:0b2165ce65dfb468","900:countdown:ae8ca8053937af55","960:fight:bccbccadf6892170","1020:fight:da04ec0d2d70f49c","1080:fight:ce46d4434cfee236","1140:fight:04ee83859799811b","1200:fight:9b3f6145c3743601","1260:result:f1cc8a8cf27c95af","1320:result:0c49cf9a0df501a4","1380:result:a140ad45ccbcfe56","1440:result:6ff2622add3d3879","1500:result:48e47cb0c4d43ad2","1560:draft:27c8145f8469add9","1620:draft:b59dfa568dc1890d","1680:draft:8af2fe1929505e5f","1740:draft:a9d0f03873ca4a6c","1800:draft:b89316be8f89666f","1860:draft:47f884dbf15e2228","1920:draft:fca16f377dafbdc7","1980:draft:f89fbd1506119fd2","2040:draft:6f33ee56b1894d15","2100:draft:4e5c554dfc3e03a6","2160:draft:6ae310287727f455","2220:draft:89eb6d3d8763d39a","2280:draft:22e462ba057b75d2","2340:draft:f301b3d3382f9c3c","2400:countdown:a62160474ca5bfd4","2460:countdown:fc46a09067e6c284","2520:countdown:5b3e02c8ca39b32b","end:2552:fight:6cf833352513ed21"],
 "siren-ward-zero":["60:intro:c8531e2ba03e4d7d","120:intro:e527521d7bf36a76","180:intro:96a85e229ca1fed0","240:intro:e2e61fc27d11bda7","300:intro:550824974b7fc531","360:intro:d5c2761d0cfaa1a1","420:intro:cf021f39d07d9538","480:intro:c72690c9dc31ea15","540:intro:903905597440ae9a","600:intro:b2abf7219266a745","660:intro:de7c8a9ed9dd7500","720:intro:8b753a6a96cc6382","780:countdown:36cc271e61f573b5","840:countdown:5aebb9e5b7570196","900:countdown:760d01d1f8fa744f","960:fight:038d3d12b509e03f","1020:fight:390b529a028d8647","1080:fight:0377a5ef5c46d7f3","1140:fight:04f59d3aa3eb4d2b","1200:fight:723396efe285a548","1260:fight:831f18a1705d4a46","1320:fight:c0b8e6d8eef8a1f4","1380:fight:134b9de3e00e4ced","1440:fight:2870d46b516c9254","1500:fight:a0f9fd3174dcdeec","1560:fight:534185888af46d15","1620:fight:1c32ab519c8c319c","1680:fight:d1bdf8b2188ea53a","1740:fight:512cae5fc0103451","1800:fight:2abd5cfcc88ce407","end:1806:fight:1f27e410555e56ab"],
 "necronomicon":["60:countdown:867ab9a906df8a8f","120:countdown:e529b0c142af424e","180:countdown:8a4ccbd071808eec","240:fight:416ffcf47bc68006","300:fight:278ca303866f89ab","360:fight:0191ba7da86de903","420:fight:4a65f402d58ab961","480:fight:b6c97ffd0618c096","540:fight:d2e959271e9e4219","600:fight:9800ece19fc24c19","660:fight:9aae0a6e1aea2ba9","720:fight:05ca0bcf7b97f9bb","780:fight:91525b3f4e5644a1","end:782:fight:ef194582d8d4d5e9"],
 "melee-dev":["60:countdown:9b4577c0ef331c79","120:countdown:eff86d2a6dc46025","180:countdown:6b90d45ea449b5e4","240:fight:3022c5d94fc7fe23","300:fight:5c84ae7e72099308","360:fight:b3522ac87c2e388a","420:fight:5c3c826881b1610e","480:fight:8308f7abac721597","540:fight:9585489483986218","600:fight:b96f2733d47108b4","660:fight:dbaabfb198bdcbc1","720:fight:8739eaf2b46faf36","780:fight:bf97478152c91dad","840:fight:3b4ff3567f741465","900:fight:ac1e28ec7a7fd893","960:fight:35759217de75a2b3","1020:fight:75a4b742bcae4f52","1080:fight:3eb4342e9060689a","end:1082:fight:4397b9bbeb8299ff"],
 "siren-melee":["60:countdown:0ec2fe710bd3c9eb","120:countdown:94bf62a1d99edc9e","180:countdown:e053fb2291c3ff8c","240:fight:ae63c3008a41a59b","300:fight:b6d5ee85ba95c072","360:fight:e1e3fd477b4132a1","420:fight:eea4268edb1c8bcf","480:fight:64453383b66e972d","540:fight:0a59608e2a8ddb3c","600:fight:344b90485323f29c","660:fight:a9110984e2f91381","720:fight:972d4a046392fc06","780:fight:0bbb6a19d33f035c","840:fight:9c27308effdb62df","900:fight:d66d9692e603e8dc","960:fight:8f9a7f70a430cb9d","1020:fight:6c474ec0d9e4f462","1080:fight:a07b1ee41faeb931","end:1082:fight:55f872e81297c2d4"]
};

for(const t of TRACES)test(`golden PvP trace: ${t.name}`,()=>{
 const hashes=run(t);
 if(process.env.PVP_GOLDEN_PRINT){console.log(JSON.stringify({[t.name]:hashes}));return;}
 assert.ok(EXPECTED[t.name],`missing expected hashes for ${t.name}`);
 assert.deepEqual(hashes,EXPECTED[t.name]);
});
test('golden traces are reproducible within one process',()=>{assert.deepEqual(run(TRACES[3]),run(TRACES[3]));});
