// PvE balance harness (design §11 S7 item 4). Not part of npm test: `npm run test:pve-balance`.
// For party sizes h=1..4: one human driven by the party-bot brain (partyBotInput, no leash) plus h-1 party bots; the
// human uses pistol, rifle or greatsword; seeds 1-5. Real parts, real Simulation. The human takes offer 0 of every pick
// and buys nothing (money is logged at boss start). Logs per-wave seconds, human deaths before the boss, money at boss
// start, seconds per boss phase and the outcome, then a median table per (h, weapon).
// Targets (design): waves 60-150 s each, boss 3-6 min, at most 1 expected solo death before the boss.
// Shopping (PVE_BAL_SHOP=0 turns it off): at each intermission the human hires recruits up to its cap, then buys and
// picks chips with what is left (it is teleported to the machines and back, standing in for the walk).
// Env: PVE_BAL_H=1,2  PVE_BAL_W=pistol  PVE_BAL_SEEDS=1,2  PVE_BAL_MAX_MIN=20  PVE_BAL_JSON=out.json
import {writeFileSync} from 'node:fs';
import {performance} from 'node:perf_hooks';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {idleInput} from '../src/shared/config.js';
import {BOSS_PLAYER_ID} from '../src/shared/pve/contract.js';
import {partyBotInput} from '../src/shared/pve/recruit-ai.js';
import {PVE_MACHINES,STAND_Y} from '../src/shared/pve/layout.js';
import {PRICES,chipPrice} from '../src/shared/pve/balance.js';
import {v} from '../src/shared/config.js';
await initPhysics();

const list=(name:string,def:string)=>(process.env[name]??def).split(',').map(x=>x.trim()).filter(Boolean);
const HS=list('PVE_BAL_H','1,2,3,4').map(Number),WEAPONS=list('PVE_BAL_W','pistol,rifle,greatsword'),SEEDS=list('PVE_BAL_SEEDS','1,2,3,4,5').map(Number);
const MAX_TICKS=Math.round(60*60*Number(process.env.PVE_BAL_MAX_MIN??20));
const CLS:Record<string,'ranged'|'melee'|'magic'>={pistol:'ranged',rifle:'ranged',machinegun:'ranged',greatsword:'melee',sword:'melee',daggers:'melee',shield:'melee',fire:'magic',water:'magic',lightning:'magic',darkness:'magic'};

interface Run{kills:number;bought:{recruits:number;chips:number};h:number;weapon:string;seed:number;waves:number[];deaths:number;bossMoney:number;bossEarned:number;earned:number;bossSpent:number;phases:number[];boss:number;outcome:string;total:number;ms:number;
 /** Damage taken by the human, by source kind (adds, the boss), waves vs boss stage. */ taken:Record<string,number>;error?:string}

function run(h:number,weapon:string,seed:number):Run{
 const t0=performance.now();
 const s=new Simulation({pve:{boss:'siren',seed,...(h>1?{partyBots:(h-1) as 1|2|3}:{})}});
 const out:Run={h,weapon,seed,waves:[],deaths:0,bossMoney:NaN,bossEarned:NaN,earned:NaN,bossSpent:NaN,phases:[],boss:NaN,outcome:'timeout',total:NaN,ms:0,taken:{},bought:{recruits:0,chips:0},kills:0};
 try{
  s.addPlayer('h1','H1',CLS[weapon]);s.pve!.command('h1',{action:'hello',v:1},false);s.setReady('h1',true);
  for(let i=0;i<600&&s.phase!=='specialization';i++)s.step();
  s.selectSpec('h1',weapon as never);
  for(let i=0;i<6000&&s.phase!=='fight';i++)s.step();
  if(s.phase!=='fight')throw new Error('no fight');
  const pve=s.pve!,me=s.players.get('h1')!,fightAt=s.time;
  const damage=s.damage.bind(s);
  s.damage=((p,amount,source,kind,...rest)=>{const before=p.hp,r=damage(p,amount,source,kind,...rest);
   if(p===me&&before>p.hp){const who=source===BOSS_PLAYER_ID?'boss':source?.startsWith('add-')||source==='pve-horde'?'adds':'other';const k=`${pve.view().stage==='boss'?'B':'W'}:${who}${who==='boss'&&process.env.PVE_BAL_KINDS?':'+kind:''}`;out.taken[k]=(out.taken[k]??0)+before-p.hp;}
   return r;}) as typeof s.damage;let seq=0,stage='',waveAt=0,bossAt=NaN,phaseAt=NaN,phase=0,wasAlive=true;
  const SHOP=process.env.PVE_BAL_SHOP!=='0';
  const stepIdle=(n:number,interact=false)=>{for(let k=0;k<n;k++){s.queueInputs('h1',[{...idleInput(),seq:++seq,interact}],`${s.setNumber}:${s.round}:${s.generation}`);s.step();}};
  const at=(x:number,z:number)=>{me.body.setTranslation(v(x,STAND_Y,z),true);me.body.setLinvel(v(),true);};
  const press=()=>{stepIdle(2,true);stepIdle(2);};
  const pickOpen=()=>{const row=pve.view().party.h1,pick=row?.picks.find(k=>k.id===row.open);if(pick?.offers?.length)pve.command('h1',{action:'pick',pick:pick.id,chip:pick.offers[0]},false);};
  const shop=()=>{
   if(me.hp<=0)return;const home=me.body.translation(),hx=home.x,hz=home.z;
   for(let k=0;k<2&&pve.economy.money('h1')>=PRICES.recruit&&pve.recruits.canHire('h1')==='ok';k++){const m=PVE_MACHINES[4+k];at(m.front.x,m.front.z);press();stepIdle(Math.ceil(PRICES.recruitDelay*60)+2);out.bought.recruits=pve.recruits.views().filter(r=>r.owner==='h1').length;}
   for(let k=0;k<3&&pve.economy.money('h1')>=chipPrice(pve.view().party.h1.chipsBought);k++){const m=PVE_MACHINES[k];at(m.front.x,m.front.z);press();stepIdle(Math.ceil(PRICES.dispenseDelay*60)+2);
    const pk=pve.view().pickups.find(x=>x.owner==='h1');if(!pk)break;at(pk.pos.x,pk.pos.z);press();pickOpen();if(!pve.view().party.h1.open)out.bought.chips++;}
   at(hx,hz);
  };
  let shopped=-1;
  for(let tick=0;tick<MAX_TICKS&&s.phase==='fight';tick++){
   if(SHOP){const w0=pve.view();if(w0.stage==='intermission'&&!w0.hold&&shopped!==w0.wave&&s.time>=w0.stageAt+1){shopped=w0.wave;shop();if(s.phase!=='fight')break;}}
   const v=pve.view(),row=v.party.h1;
   if(row?.open){const pick=row.picks.find(k=>k.id===row.open);if(pick?.offers?.length)pve.command('h1',{action:'pick',pick:pick.id,chip:pick.offers[0]},false);}
   const base={...idleInput(),seq:++seq,yaw:me.input.yaw,pitch:me.input.pitch};
   const input={...partyBotInput(pve,me,base),seq};
   // A human looks for the boss: with nothing to shoot, walk toward the visible boss over the nav field.
   const boss=pve.boss();if(boss&&boss.hp>0&&!pve.hidden(boss)&&!input.attack&&me.hp>0){const d=pve.horde.nav.direction(me.body.translation(),boss.id,'party');
    const bp=boss.body.translation(),mp=me.body.translation(),far=Math.hypot(bp.x-mp.x,bp.z-mp.z);if(d&&far>8){input.x=d.x;input.z=d.z;}}
   s.queueInputs('h1',[input],`${s.setNumber}:${s.round}:${s.generation}`);
   s.step();
   const w=pve.view();
   if(w.stage!==stage){
    if(stage==='wave')out.waves.push(+(s.time-waveAt).toFixed(1));
    if(w.stage==='wave')waveAt=s.time;
    if(w.stage==='boss'){bossAt=s.time;phaseAt=s.time;phase=1;out.bossMoney=w.party.h1?.money??0;out.bossEarned=w.party.h1?.earned??0;out.bossSpent=w.party.h1?.spent??0;}
    stage=w.stage;
   }
   if(process.env.PVE_BAL_TRACE&&tick%1800===0){const b=pve.boss(),bp=b?.body.translation(),mp=me.body.translation();console.log(`  t=${s.time.toFixed(0)} stage=${w.stage} hold=${w.hold} cin=${w.cinematic?.kind??''} me=(${mp.x.toFixed(1)},${mp.y.toFixed(1)},${mp.z.toFixed(1)}) hp=${me.hp.toFixed(0)} boss=${bp?`(${bp.x.toFixed(1)},${bp.y.toFixed(1)},${bp.z.toFixed(1)}) hp=${b!.hp.toFixed(0)} hidden=${pve.hidden(b!)} conn=${b!.connected}`:'-'} adds=${pve.horde.alive()} q=${pve.horde.queued()} left=${w.waveLeft}`);}
   const bp=w.bossView?.phase??0;if(stage==='boss'&&bp!==phase&&bp>phase){out.phases.push(+(s.time-phaseAt).toFixed(1));phaseAt=s.time;phase=bp;}
   const alive=me.hp>0;if(wasAlive&&!alive&&!(bossAt<=s.time))out.deaths++;wasAlive=alive;
  }
  const end=pve.view();out.kills=end.party.h1?.kills??0;out.earned=end.party.h1?.earned??0;
  if(end.outcome){out.outcome=end.outcome;if(Number.isFinite(bossAt)){out.phases.push(+(s.time-phaseAt).toFixed(1));out.boss=+(s.time-bossAt).toFixed(1);}}
  else if(s.players.get(BOSS_PLAYER_ID)&&Number.isFinite(bossAt))out.boss=NaN;
  out.total=+(s.time-fightAt).toFixed(1);
 }catch(e){out.error=String((e as Error).stack??e).split('\n').filter(l=>!l.includes('wasm://')).slice(0,12).join(' | ');out.outcome='error';}
 finally{try{s.dispose();}catch{/* a failed step can leave Rapier borrowed */}}
 out.ms=Math.round(performance.now()-t0);return out;
}

const median=(xs:number[])=>{const a=xs.filter(Number.isFinite).sort((x,y)=>x-y);if(!a.length)return NaN;const m=a.length>>1;return a.length%2?a[m]:(a[m-1]+a[m])/2;};
const f=(n:number,d=0)=>Number.isFinite(n)?n.toFixed(d):'—';
const runs:Run[]=[];
for(const h of HS)for(const weapon of WEAPONS)for(const seed of SEEDS){
 const r=run(h,weapon,seed);runs.push(r);
 console.log(`h=${h} ${weapon.padEnd(10)} seed=${seed} waves=[${r.waves.join(', ')}] deaths=${r.deaths} money@boss=${f(r.bossMoney)} earned@boss=${f(r.bossEarned)} spent@boss=${f(r.bossSpent)} earned=${f(r.earned)} phases=[${r.phases.join(', ')}] boss=${f(r.boss,1)} ${r.outcome} total=${f(r.total,1)}s kills=${r.kills} bought=${r.bought.recruits}r/${r.bought.chips}c taken=${JSON.stringify(Object.fromEntries(Object.entries(r.taken).map(([k,x])=>[k,Math.round(x)])))} (${r.ms} ms)${r.error?' ERROR '+r.error:''}`);
}
console.log('\n| h | weapon | wave 1 s | wave 2 s | wave 3 s | deaths before boss | money at boss | earned at boss | earned total | P1 s | P2 s | P3 s | P4 s | boss s | victories |');
console.log('|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|');
for(const h of HS)for(const weapon of WEAPONS){
 const rs=runs.filter(r=>r.h===h&&r.weapon===weapon),col=(fn:(r:Run)=>number,d=0)=>f(median(rs.map(fn)),d);
 console.log(`| ${h} | ${weapon} | ${col(r=>r.waves[0]??NaN)} | ${col(r=>r.waves[1]??NaN)} | ${col(r=>r.waves[2]??NaN)} | ${col(r=>r.deaths,1)} | ${col(r=>r.bossMoney)} | ${col(r=>r.bossEarned)} | ${col(r=>r.earned)} | ${col(r=>r.phases[0]??NaN)} | ${col(r=>r.phases[1]??NaN)} | ${col(r=>r.phases[2]??NaN)} | ${col(r=>r.phases[3]??NaN)} | ${col(r=>r.boss)} | ${rs.filter(r=>r.outcome==='victory').length}/${rs.length} |`);
}
if(process.env.PVE_BAL_JSON)writeFileSync(process.env.PVE_BAL_JSON,JSON.stringify(runs,null,1));
process.exit(0);
