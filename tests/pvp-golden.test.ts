// Golden PvP traces (PvE foundation, design §11 F step 0). The expected hashes were generated on the untouched
// base b6d2f58 before any PvE edit. Every PvE hook must leave these four PvP traces byte-identical.
// Regenerate only on a deliberate PvP behaviour change: PVP_GOLDEN_PRINT=1 node --import tsx --test tests/pvp-golden.test.ts
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
 "ffa-foundry":["60:countdown:41ec0dab413b4a92","120:countdown:7202f2fb6f4fc2e3","180:countdown:d09ecf24c7179820","240:fight:cf95f0781ecd26d2","300:fight:f07c42a4b962577c","360:fight:11151d99e057a197","420:fight:ef3f01e93f1a1200","480:fight:a437df23123fd5d6","540:fight:fe1b3dae54579be8","600:fight:f62237fb37930560","660:fight:2413c470f6535456","720:fight:f692db42530b060f","780:fight:009c8cdb99c5b0b7","end:782:fight:f4fcfd2c55b21982"],
 "teams-moonlit":["60:countdown:a5735e740bc360af","120:countdown:a46c1d81032730a2","180:countdown:ffcf15f4fc7a5da3","240:fight:9d0c6e154b15e688","300:fight:433391d361226f5b","360:fight:730d060c62d6a821","420:fight:1cdf97d4caa081cc","480:fight:dc1830e1b01acac8","540:result:e96c00231f3c3ce4","600:result:40d0f47179d990c8","660:result:fa62e287223c2411","720:result:0491d77d6a1f10aa","780:result:9994b63db8f785a5","840:countdown:ac6d67594ca9411a","900:countdown:a9ea0fab26f565fa","960:countdown:b4ea770eb959c755","1020:fight:074706c7a6bd672b","1080:fight:e317528ea8019333","1140:fight:8365474efe07630e","1200:fight:00023c0162055f24","1260:fight:62be2da3d36aa452","end:1262:fight:4fb7db790476c2b3"],
 "siren-ward-zero":["60:intro:c8531e2ba03e4d7d","120:intro:e527521d7bf36a76","180:intro:96a85e229ca1fed0","240:intro:e2e61fc27d11bda7","300:intro:550824974b7fc531","360:intro:d5c2761d0cfaa1a1","420:intro:cf021f39d07d9538","480:intro:c72690c9dc31ea15","540:intro:903905597440ae9a","600:intro:b2abf7219266a745","660:intro:de7c8a9ed9dd7500","720:intro:8b753a6a96cc6382","780:countdown:36cc271e61f573b5","840:countdown:5aebb9e5b7570196","900:countdown:760d01d1f8fa744f","960:fight:4ff0a5de7129b1ae","1020:fight:d1dac0452aac72df","1080:fight:565b3c799c401d9c","1140:fight:bd9ebfd212a8d0e9","1200:fight:1ab14d129a14306b","1260:fight:ff5f961de734deb3","1320:fight:52ad82058f75e7a2","1380:fight:1cd40c18e6403256","1440:fight:b8904f85345b6869","1500:fight:993cebace6852899","1560:fight:4a96351abb6d47fb","1620:fight:096c2538dc7b06b9","1680:fight:85ee0b002fceb60a","1740:fight:a66d9aad16419f82","1800:fight:038cc6f94268bef6","end:1806:fight:661a5aba9ef8fd45"],
 "necronomicon":["60:countdown:867ab9a906df8a8f","120:countdown:e529b0c142af424e","180:countdown:8a4ccbd071808eec","240:fight:ce98adb1e7af122a","300:fight:7a910f30ecbcf696","360:fight:6a64d13fc9e0917b","420:fight:5308c0f23e76bba6","480:fight:1bcb34b8a492949e","540:fight:9c72d77703caff06","600:fight:ab8eea09779dce55","660:fight:3affe66e93c0c122","720:fight:083c98d6cbab8911","780:fight:fedcfb248e9189bb","end:782:fight:b56559f25c1d255a"],
 "melee-dev":["60:countdown:9b4577c0ef331c79","120:countdown:eff86d2a6dc46025","180:countdown:6b90d45ea449b5e4","240:fight:347e9fafbd2131bd","300:fight:c9b2166d54648b31","360:fight:14fb90ec98700c11","420:fight:635aa568dc3415ca","480:fight:1d73866a2eab6639","540:fight:71952ca03038f449","600:fight:420fa040c3c21f3b","660:fight:a11184ca5b9a31ed","720:fight:80b7515497435853","780:fight:f5433f9e8a1a25d2","840:fight:ad9f34479fa985b8","900:fight:b401406c8476782b","960:fight:8f6ae2fc036a6c0e","1020:fight:c3fb94a67642df33","1080:fight:587bde4401e161b5","end:1082:fight:926113eeca735f00"],
 "siren-melee":["60:countdown:0ec2fe710bd3c9eb","120:countdown:94bf62a1d99edc9e","180:countdown:e053fb2291c3ff8c","240:fight:686436b595649fa2","300:fight:6d04b392eb325d82","360:fight:cd686f3137984b7a","420:fight:740688847f557c2b","480:fight:64c2332ee101984c","540:fight:4f0fcf896c08aef2","600:fight:68ce004c80b7e7d3","660:fight:90be294133372ee5","720:fight:3d2e12e88298407b","780:fight:41e4b8a063605241","840:fight:e8e6691f3e1f538b","900:fight:ac8e3205d1933915","960:fight:06a32d01f776940c","1020:fight:c4d85fa79f17d3a9","1080:fight:9e12a18fd5c0be9a","end:1082:fight:8a32d0d5383268b2"]
};

for(const t of TRACES)test(`golden PvP trace: ${t.name}`,()=>{
 const hashes=run(t);
 if(process.env.PVP_GOLDEN_PRINT){console.log(JSON.stringify({[t.name]:hashes}));return;}
 assert.ok(EXPECTED[t.name],`missing expected hashes for ${t.name}`);
 assert.deepEqual(hashes,EXPECTED[t.name]);
});
test('golden traces are reproducible within one process',()=>{assert.deepEqual(run(TRACES[3]),run(TRACES[3]));});
