// PvE end-to-end flow (design §11 S7 item 3). Real parts, real Simulation, no fakes: lobby → picks → waves → chip and
// recruit machines → death, wave-clear respawn and paid channel revive → Siren reveal, intro and four phases → victory,
// results and Retry. A second run with the same seed must hash identically; a solo defeat variant uses the self-revive.
// Default variant is short (skip-wave after wave 1); PVE_LONG=1 plays every wave.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {idleInput,v,type Input} from '../src/shared/config.js';
import {BOSS_PLAYER_ID,PARTY_BOT_PREFIX,RECRUIT_PREFIX,type AddHitInfo,type PveOptions,type PveView} from '../src/shared/pve/contract.js';
import {PVE,PVE_SIREN,PRICES,chipPrice,revivePrice} from '../src/shared/pve/balance.js';
import {PVE_MACHINES,STAND_Y} from '../src/shared/pve/layout.js';
await initPhysics();

const LONG=process.env.PVE_LONG==='1';
const KEY=(i:number)=>(i.toString(16).padStart(2,'0')).repeat(16);
const TICK=1/60;

class Driver{
 readonly s:Simulation;private seq=new Map<string,number>();private held=new Map<string,Partial<Input>>();
 hashes:string[]=[];hits=0;log:string[]=[];
 constructor(o:Partial<PveOptions>,readonly humans:string[]){
  this.s=new Simulation({pve:{boss:'siren',seed:4242,debug:true,...o}});
  for(const id of humans){assert.ok(this.s.addPlayer(id,id.toUpperCase(),'ranged'),`seat ${id}`);
   assert.equal(this.s.pve!.command(id,{action:'hello',v:1,key:KEY(humans.indexOf(id)+1)},false),true);}
 }
 get view():PveView{return this.s.pve!.view();}
 player(id:string){return this.s.players.get(id)!;}
 /** Holds a partial input for a human until changed (re-sent every tick with a fresh seq). */
 hold(id:string,i:Partial<Input>){this.held.set(id,i);}
 step(n=1){
  for(let k=0;k<n;k++){
   const epoch=`${this.s.setNumber}:${this.s.round}:${this.s.generation}`;
   if(this.s.phase==='fight')for(const id of this.humans){const seq=(this.seq.get(id)??0)+1;this.seq.set(id,seq);
    this.s.queueInputs(id,[{...idleInput(),seq,...(this.held.get(id)??{})}],epoch);}
   this.s.step();
   if(this.s.tick%60===0)this.hashes.push(`${this.s.tick}:${this.s.phase}:${this.digest()}`);
  }
 }
 digest(){const snap=this.s.snapshot() as unknown as Record<string,unknown>;const {metrics:_m,...rest}=snap;return createHash('sha256').update(JSON.stringify(rest)).digest('hex').slice(0,16);}
 until(done:()=>boolean,max:number,what:string,each?:()=>void){for(let i=0;i<max;i++){if(done())return i;each?.();this.step();}assert.fail(`not reached: ${what} (stage ${this.view.stage}, t=${this.s.time.toFixed(2)})`);}
 /** Lobby → specialization (pistol) → intros → fight. */
 start(){
  for(const id of this.humans)this.s.setReady(id,true);
  this.until(()=>this.s.phase==='specialization'||this.s.phase==='fight'||this.s.phase==='intro'||this.s.phase==='countdown',600,'specialization');
  for(const id of this.humans)this.s.selectSpec(id,'pistol');
  this.until(()=>this.s.phase==='fight',6000,'fight');
 }
 teleport(id:string,x:number,z:number){const p=this.player(id);p.body.setTranslation(v(x,STAND_Y,z),true);p.body.setLinvel(v(),true);}
 /** One press of F (down for 2 ticks, then released). */
 press(id:string){this.hold(id,{interact:true});this.step(2);this.hold(id,{});this.step(2);}
 /** Picks offer 0 of the open pick. */
 pickFirst(id:string){
  const row=this.view.party[id],pick=row.picks.find(k=>k.id===row.open);assert.ok(pick?.offers?.length,`${id} has offers`);
  assert.equal(this.s.pve!.command(id,{action:'pick',pick:pick!.id,chip:pick!.offers![0]},false),true,`${id} picks`);
 }
 nearestAdd(from:string){
  const pos=this.player(from).body.translation();let best:{id:string;d:number}|undefined;
  for(const a of this.s.pve!.horde.adds()){if(a.state==='entering'||a.state==='vent'||a.state==='dead'||!this.s.chaos.minions.get(a.id))continue;
   const d=Math.hypot(a.pos.x-pos.x,a.pos.z-pos.z);if(!best||d<best.d)best={id:a.id,d};}
  return best?.id;
 }
 /** The scripted auto-fighter: one 30-damage hit every 0.25 s on the nearest add, a headshot point every 3rd hit. */
 fight(from:string,target?:string){
  if(this.s.tick%15!==0)return;
  const id=target??this.nearestAdd(from);const m=id?this.s.chaos.minions.get(id):undefined;if(!m)return;
  const pos=m.body.translation(),me=this.player(from).body.translation(),dir=v(pos.x-me.x,0,pos.z-me.z);
  const head=++this.hits%3===0,hit:AddHitInfo=head?{kind:'bullet',headshot:true,point:{x:pos.x,y:pos.y+.62,z:pos.z}}:{kind:'bullet',point:{x:pos.x,y:pos.y,z:pos.z}};
  this.s.chaos.hitMinion(m,30,from,0,dir,hit);
 }
 kill(id:string){const p=this.player(id);const god=this.s.pve!.godMode;this.s.pve!.debug('god:off');this.s.damage(p,p.hp+1e4,null,'test');if(god)this.s.pve!.debug('god:on');assert.ok(p.hp<=0,`${id} is down`);}
 dispose(){this.s.dispose();}
}

interface FlowResult{hashes:string[];log:string[]}
/** The victory flow; returns the hash trace for the determinism check. */
function victoryFlow():FlowResult{
 const d=new Driver({partyBots:1},['h1','h2']);const {s}=d;
 try{
  d.start();
  const bot=[...s.players.values()].find(p=>p.id.startsWith(PARTY_BOT_PREFIX));assert.ok(bot,'party bot seated');
  s.pve!.debug('god:on');
  // Personal start picks: both humans are frozen on their open start pick; each picks offer 0.
  for(const id of d.humans){assert.ok(d.view.party[id].open,`${id} start pick open`);assert.equal(d.view.party[id].money,50);}
  for(const id of d.humans)d.pickFirst(id);
  d.until(()=>d.view.stage==='wave',60*PVE.openingSeconds+60,'wave 1');
  assert.equal(d.view.wave,1);
  // Death during wave 1, then the wave-clear respawn.
  d.until(()=>s.pve!.horde.alive()>=3,60*20,'adds on the field',()=>d.fight('h1'));
  d.kill('h2');
  const moneyBefore=d.view.party.h1.money;
  d.until(()=>d.view.stage==='intermission',60*PVE.waveMaxSeconds+60,'wave 1 clear',()=>d.fight('h1'));
  const h2=d.player('h2');assert.ok(h2.hp>0,'h2 respawned at the wave clear');assert.ok(Math.abs(h2.hp-h2.stats.hp*PVE.respawnHpFraction)<1,'respawn at 50%');
  const row1=d.view.party.h1;assert.ok(row1.kills>=10,`kills ${row1.kills}`);assert.ok(row1.money>moneyBefore,'money flows from kills and the clear');
  assert.ok(row1.heads>0,'headshot kills counted');
  d.log.push(`wave1 clear t=${s.time.toFixed(2)} kills=${row1.kills} money=${row1.money}`);
  // Chip machine 0: teleport, press F, grab the popped chip, open and pick offer 0.
  const m0=PVE_MACHINES[0];assert.ok(row1.money>=chipPrice(0),`can afford a chip (${row1.money})`);
  d.teleport('h1',m0.front.x,m0.front.z);d.press('h1');
  assert.equal(d.view.party.h1.chipsBought,1);assert.equal(d.view.party.h1.spent,chipPrice(0));
  d.step(Math.ceil(PRICES.dispenseDelay*60)+2);const pk=d.view.pickups.find(k=>k.owner==='h1');assert.ok(pk,'chip dispensed');
  d.teleport('h1',pk!.pos.x,pk!.pos.z);d.press('h1');assert.equal(d.view.pickups.filter(k=>k.owner==='h1').length,0,'grabbed');
  if(!d.view.party.h1.open)assert.equal(s.pve!.command('h1',{action:'open',n:1},false),true);
  const chipsBefore=Object.values(d.player('h1').chips).reduce((a,b)=>a+(b as number),0);d.pickFirst('h1');
  assert.equal(Object.values(d.player('h1').chips).reduce((a,b)=>a+(b as number),0),chipsBefore+1,'chip installed');
  // Recruit pod (machine 4): top up with the debug money op if the kills did not pay enough yet.
  if(d.view.party.h1.money<PRICES.recruit)s.pve!.debug(`money:${PRICES.recruit-d.view.party.h1.money}`);
  const m4=PVE_MACHINES[4];d.teleport('h1',m4.front.x,m4.front.z);d.press('h1');
  d.step(Math.ceil(PRICES.recruitDelay*60)+2);assert.ok([...s.players.keys()].some(id=>id.startsWith(RECRUIT_PREFIX)),'recruit hired');
  assert.equal(d.view.recruits.length,1);assert.equal(d.view.recruits[0].owner,'h1');
  // Second death and a paid channel revive (hold F for 2.5 s next to the body).
  d.kill('h2');const at=d.player('h2').death!.pos;
  if(d.view.party.h1.money<revivePrice(0))s.pve!.debug(`money:${revivePrice(0)}`);
  const paid=d.view.party.h1.spent;d.teleport('h1',at.x+.4,at.z);d.hold('h1',{interact:true});
  d.until(()=>d.player('h2').hp>0,60*(PRICES.reviveSeconds+1),'channel revive',()=>d.teleport('h1',at.x+.4,at.z));
  d.hold('h1',{});assert.equal(d.view.party.h1.spent-paid,revivePrice(0),'revive charged');assert.equal(d.view.party.h1.revives,1);
  d.log.push(`revive t=${s.time.toFixed(2)}`);
  // Waves 2-3: played in the long variant, skipped in the default one.
  if(LONG){d.until(()=>d.view.cinematic?.kind==='reveal',60*600,'last wave',()=>d.fight('h1'));}
  else{let guard=0;while(d.view.cinematic?.kind!=='reveal'&&guard++<8){if(!d.view.hold)assert.equal(s.pve!.debug('skip-wave'),true,'skip-wave');d.step(2);}}
  // Reveal 1 → boss intro → "Siren has Spawned".
  const seen:string[]=[];const note=()=>{const c=d.view.cinematic;const k=c?`${c.kind}${c.chip??''}`:d.view.stage;if(seen.at(-1)!==k)seen.push(k);};
  d.until(()=>d.view.stage==='boss'&&!d.view.hold,60*30,'boss stage',note);note();
  assert.deepEqual(seen,['reveal1','boss-intro','boss']);assert.equal(d.view.track.revealed,1);
  const boss=d.player(BOSS_PLAYER_ID),maxHp=boss.stats.hp;assert.equal(d.view.bossView?.phase,1);
  d.log.push(`boss t=${s.time.toFixed(2)} hp=${maxHp}`);
  const strike=()=>{if(s.tick%60===0&&!d.view.hold&&boss.hp>0&&!s.pve!.hidden(boss))s.damage(boss,400,'h1','test');};
  // Phase 1: the 75% floor holds until chip 2.
  d.until(()=>d.view.cinematic?.kind==='reveal',60*60,'chip 2 reveal',()=>{strike();d.fight('h1');assert.ok(boss.hp>=PVE_SIREN.thresholds[0]*maxHp-1e-6,'floor 75%');});
  assert.equal(d.view.cinematic?.chip,2);assert.ok(Math.abs(boss.hp-.75*maxHp)<1e-3);
  // Phase 2: possession; killing the possessed add gives the boss back at 50% and starts chip 3 on the same tick.
  d.until(()=>d.view.bossView?.phase===2&&!d.view.hold,60*10,'possession');
  assert.equal(d.view.bossView?.hidden,true);
  d.until(()=>d.view.bossView?.possessed!==undefined,60*20,'possessed add',()=>d.fight('h1'));
  const n=d.view.bossView!.possessed!;
  d.until(()=>d.view.bossView?.foundAt!==undefined,60*60,'possessed add killed',()=>{const id='add-'+n;const a=s.chaos.minions.get(id);if(a&&s.tick%15===0)d.teleport('h1',a.body.translation().x+2,a.body.translation().z);d.fight('h1',a?id:undefined);});
  assert.equal(d.view.cinematic?.kind,'reveal');assert.equal(d.view.cinematic?.chip,3);assert.equal(d.view.cinematic?.at,d.view.bossView!.foundAt);
  assert.ok(Math.abs(boss.hp-.5*maxHp)<1e-3,'found at 50%');assert.equal(s.pve!.hidden(boss),false);
  assert.ok(d.view.party.h1.earned>0);
  // Phase 3: "Am I beautiful?" grows the boss; the 25% floor holds until chip 4.
  d.until(()=>d.view.bossView?.phase===3&&!d.view.hold,60*10,'phase 3');
  assert.equal(boss.siren?.boost?.scale,PVE_SIREN.beautiful.scale,'grown');
  d.until(()=>d.view.cinematic?.kind==='reveal',60*60,'chip 4 reveal',()=>{strike();d.fight('h1');assert.ok(boss.hp>=PVE_SIREN.thresholds[2]*maxHp-1e-6,'floor 25%');});
  assert.equal(d.view.cinematic?.chip,4);
  // Phase 4: no cooldowns (D35: no Last Laugh floor either).
  d.until(()=>d.view.bossView?.phase===4&&!d.view.hold,60*10,'phase 4');
  const b=boss.siren!.boost!;assert.equal(b.cooldown,0);assert.equal(b.minRecast?.laugh,0);assert.equal(b.maxRains,PVE_SIREN.play.maxRains);
  // Boss death → victory outro → results.
  d.until(()=>d.view.cinematic?.kind==='outro',60*60,'victory outro',()=>{strike();d.fight('h1');});
  assert.equal(d.view.cinematic?.outcome,'victory');
  d.until(()=>s.phase==='match',60*(PVE.outroVictorySeconds+1),'match');
  const end=s.snapshot().pve!;assert.equal(end.outcome,'victory');assert.equal(end.stage,'ended');assert.ok(end.endedAt!==undefined);
  assert.ok(end.party.h1.bossDamage>0&&end.party.h1.kills>0,'results rows');assert.ok(end.party.h2&&end.party[bot!.id],'every member has a row');
  d.log.push(`victory t=${s.time.toFixed(2)} h1 kills=${end.party.h1.kills} boss=${end.party.h1.bossDamage}`);
  // Retry starts a new run.
  const run=s.pve!.runIndex;assert.equal(s.rematch(),true);
  d.until(()=>s.phase==='fight',6000,'retry fight',()=>{for(const id of d.humans)if(s.phase==='specialization')s.selectSpec(id,'pistol');});
  assert.equal(s.pve!.runIndex,run+1);assert.equal(d.view.stage,'opening');assert.equal(d.view.outcome,undefined);
  assert.equal(d.view.party.h1.money,50);assert.ok(!s.players.has(BOSS_PLAYER_ID));assert.equal(d.view.recruits.length,0);
  return {hashes:d.hashes,log:d.log};
 }finally{d.dispose();}
}

test('PvE flow: waves, machines, respawn and revive, the Siren in four phases, victory, results and Retry; deterministic',{timeout:600000},()=>{
 const a=victoryFlow();console.log(a.log.join('\n'));
 const b=victoryFlow();
 assert.equal(a.hashes.length,b.hashes.length);assert.deepEqual(b.hashes,a.hashes,'same seed, same snapshots');
});

test('PvE flow (defeat): a solo human uses the self-revive, then the party falls',{timeout:300000},()=>{
 const d=new Driver({skipWaves:true},['h1']);const {s}=d;try{
  d.start();assert.equal(s.pve!.solo,true);
  d.pickFirst('h1');d.step(30);
  assert.equal(d.view.party.h1.selfRevives,PVE.soloSelfRevives);
  d.kill('h1');assert.notEqual(d.view.party.h1.selfReviveAt,undefined,'self-revive pending');d.step(5);
  assert.notEqual(d.view.cinematic?.kind,'outro','no defeat while the self-revive is pending');
  // Holds (the chip-1 reveal and boss intro start meanwhile with skipWaves) pause the self-revive timer.
  d.until(()=>d.player('h1').hp>0,60*(PVE.selfReviveDelay+20),'self-revive');assert.equal(d.view.party.h1.selfRevives,0);
  d.step(60*3);d.kill('h1');
  d.until(()=>d.view.cinematic?.kind==='outro',120,'defeat outro');assert.equal(d.view.cinematic?.outcome,'defeat');
  d.until(()=>s.phase==='match',60*(PVE.outroDefeatSeconds+1),'match');
  assert.equal(s.snapshot().pve!.outcome,'defeat');assert.equal(s.pve!.horde.alive(),0,'the horde is cleared on defeat');
  assert.equal(s.rematch(),true);d.until(()=>s.phase==='fight',6000,'retry fight',()=>{if(s.phase==='specialization')s.selectSpec('h1','pistol');});
  assert.equal(d.view.outcome,undefined);assert.equal(d.view.party.h1.selfRevives,PVE.soloSelfRevives);
 }finally{d.dispose();}
});
