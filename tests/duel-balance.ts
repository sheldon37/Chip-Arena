// Duel balance harness (design §7.2). Not part of npm test: `npm run test:duel-balance`. Deterministic: seeded Simulation
// bots only, no model calls, no wall-clock input to the simulation.
//
// Two hard bots with `preferredSpec` duel: `open` (foundry with every non-foundation prop removed, 45 s cap) or `cover`
// (moonlit/neon/freight/sawmill with their props, rotating by seed pair; 60 s cap). Seats swap on every other seed. A duel
// that times out scores .5 ± (HP% difference) / 2.
// Tables (DUEL_TABLES, default all):
//  1. cross-spec matrix: the 7 ranged/magic specs pairwise (open + cover) and against the 4 melee specs (cover);
//  2. per-spec Q/E casts, hit rate and ability damage share (from the table-1 duels), plus gun ADS forced on/off;
//  3. perfect-aim probe in a sandbox: still and 6.8 m/s strafer TTK at 8/15/25 m (primary only), the Q+E opener on a caught
//     target, and every E against a 6.8 m/s strafer without dodge AI at its bot mid-range;
//  4. chip delta of every new magic chip (mirror and vs pistol; schools rotate by seed; school chips use their school);
//  5. Pong holder vs the same spec without chips (open), per weapon.
// Statistics: each pairing reports its win rate with a Wilson 95 % CI. PASS: the CI lies inside band ± 5; FAIL: the CI lies
// entirely outside the band; INCONCLUSIVE otherwise (gate mode reruns it at n=600 and judges the point estimate against
// band ± 5). Mean end-HP differential and damage per 10 s are reported for every row (primary for tables 4 and 5).
// Bias control: tables 1 and 5 are repeated with BOT_LEAD [0,0,0] (DUEL_LEAD0=0 turns that off).
// Env: DUEL_SEEDS=20 (iteration n)  DUEL_GATE=1 (n=200 for gate/melee rows, reruns at 600)  DUEL_SPECS=fire,pistol
//      DUEL_TABLES=1,5  DUEL_WORKERS=4  DUEL_JSON=out.json  DUEL_SRC=<other checkout>/src (e.g. the pre-change base)
//      DUEL_BASE=<json from a DUEL_SRC base run> (melee rows: flag a drop of more than 10 points, CI-aware)
//      DUEL_PROBE_SEEDS=24 (48 in gate mode)  DUEL_REPORT_SEEDS (tables 2-ADS and 4; default DUEL_SEEDS)  DUEL_COVER_MAPS
//      Iteration only: DUEL_LEAD=0,.5,.8 (overrides the shipped BOT_LEAD)  DUEL_ARENAS=open  DUEL_MELEE=0
import {Worker,isMainThread,parentPort,workerData} from 'node:worker_threads';
import {writeFileSync,readFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {performance} from 'node:perf_hooks';

type Arena='open'|'cover';
interface DuelJob{kind:'duel';key:string;a:string;b:string;arena:Arena;seed:number;chipsA?:Record<string,number>;chipsB?:Record<string,number>;lead:number[];ads?:boolean}
interface ProbeJob{kind:'ttk'|'estrafe';key:string;spec:string;dist:number;seed:number;strafe?:'run'|'turn';opener?:boolean;aim?:'pos'|'lead'|'lead-feet'}
type Job=DuelJob|ProbeJob;
interface AbilityStats{dmg:number;total:number;casts:Record<string,number>;hits:Record<string,number>;abilityDmg:Record<string,number>}
interface DuelResult{win:number;t:number;hpDiff:number;dmgA:number;dmgB:number;abilA:AbilityStats;abilB:AbilityStats}
interface ProbeResult{value:number}

const SRC=process.env.DUEL_SRC||fileURLToPath(new URL('../src',import.meta.url));
/** Cover maps rotate by seed pair. Foundry is the open map only: with its props, two bots spawn on either side of its centre
 * wall and never path around it (measured: 60 s stalemates at full HP). */
const COVER=(process.env.DUEL_COVER_MAPS||'moonlit,neon,freight,sawmill').split(',');

// ---------------------------------------------------------------------------------------------------------------------
// Worker: runs one job at a time.
if(!isMainThread){
 // Loader hooks do not reach worker threads here: register tsx in the worker before importing TypeScript sources.
 const {register}=await import('tsx/esm/api');register();
 const {Simulation,initPhysics}=await import(SRC+'/shared/simulation.ts');
 const {WEAPONS,statsFor}=await import(SRC+'/shared/content.ts');
 const config=await import(SRC+'/shared/config.ts');
 const {ballisticPitch}=await import(SRC+'/shared/projectiles.ts');
 const {cameraPosition,weaponMuzzle}=await import(SRC+'/shared/view.ts');
 const abilities:any=await import(SRC+'/shared/abilities.ts').catch(()=>null);
 await initPhysics();
 const {v,idleInput}=config;
 const BOT_LEAD:number[]|undefined=abilities?.BOT_LEAD,FX_ABILITY:Record<string,string>=abilities?.FX_ABILITY??{},kitFor=abilities?.kitFor;
 const blank=():AbilityStats=>({dmg:0,total:0,casts:{},hits:{},abilityDmg:{}});

 function duel(j:DuelJob):DuelResult{
  if(BOT_LEAD)BOT_LEAD.splice(0,3,...j.lead);
  const swap=j.seed%2===1,seatA=swap?'b':'a',seatB=swap?'a':'b';
  const s=new Simulation({map:j.arena==='open'?'foundry':COVER[Math.floor(j.seed/2)%COVER.length],skipIntros:true,seed:j.seed,skipBotDrafts:true,countdown:.1} as any);
  const add=(seat:string,spec:string)=>s.addPlayer(seat,seat.toUpperCase(),WEAPONS[spec].class,'hard',seat==='a'?'#ff0000':'#0000ff',{preferredSpec:spec});
  if(seatA==='a'){add('a',j.a);add('b',j.b);}else{add('a',j.b);add('b',j.a);}
  s.start();
  const stats:Record<string,AbilityStats>={[seatA]:blank(),[seatB]:blank()};
  // Ability attribution: the ability whose code is running (shot, blast, detonation, field tick), else the source's latest cast.
  let ctx:string|undefined,inChaos=false;const last:Record<string,string>={},castIdx:Record<string,number>={},hitIdx:Record<string,number>={};
  const wrap=(o:any,name:string,id:(...x:any[])=>string|undefined)=>{if(!o?.[name])return;const f=o[name].bind(o);o[name]=(...x:any[])=>{const prev=ctx;ctx=id(...x)??prev;try{return f(...x);}finally{ctx=prev;}};};
  wrap(s.abilities,'stepShot',(shot:any)=>shot.ability?.id);wrap(s.abilities,'blast',(...x:any[])=>FX_ABILITY[x[5]]);wrap(s.abilities,'detonate',(f:any)=>FX_ABILITY[f.fx]);
  if(s.chaos?.step){const step=s.chaos.step.bind(s.chaos);s.chaos.step=(dt:number)=>{inChaos=true;try{return step(dt);}finally{inChaos=false;}};}
  const damage=s.damage.bind(s);
  s.damage=((p:any,amount:number,source:string,kind:string,...rest:any[])=>{const actual=damage(p,amount,source,kind,...rest);
   const st=stats[source];if(!st||source===p.id||!(actual>0))return actual;st.total+=actual;
   let id:string|undefined;
   if(kind==='ability')id=ctx??last[source];
   else if(inChaos){const pos=p.body.translation(),f=[...s.chaos.fields.values()].find((f:any)=>f.owner===source&&FX_ABILITY[f.fx]&&Math.hypot(f.pos.x-pos.x,f.pos.y-pos.y,f.pos.z-pos.z)<f.radius+.6);id=f?FX_ABILITY[f.fx]:undefined;}
   if(id){st.dmg+=actual;st.abilityDmg[id]=(st.abilityDmg[id]||0)+actual;const k=source+':'+id;if(castIdx[k]!==undefined&&hitIdx[k]!==castIdx[k]){hitIdx[k]=castIdx[k];st.hits[id]=(st.hits[id]||0)+1;}}
   return actual;}) as typeof s.damage;
  if(j.ads!==undefined){const bot=s.botInput.bind(s);s.botInput=((p:any)=>{const i=bot(p);if(p.id===seatA)i.ability=j.ads;return i;}) as typeof s.botInput;}
  const seen=new WeakSet<object>();let armed=false,ticks=0;const cap=(j.arena==='open'?45:60)*60;
  for(let g=0;g<30000;g++){
   if(s.phase==='fight'){
    if(!armed){armed=true;if(j.arena==='open')for(const pr of [...s.props.values()])if(pr.material!=='foundation')s.removeProp(pr.id);
     for(const [seat,chips] of [[seatA,j.chipsA],[seatB,j.chipsB]] as const){const p=s.players.get(seat);p.chips={...(chips||{})};p.stats=statsFor(p.chips,p.spec);p.hp=p.stats.hp;p.mana=p.stats.maxMana;p.stamina=p.stats.maxStamina;}}
    ticks++;const a=s.players.get('a'),b=s.players.get('b');if(a.hp<=0||b.hp<=0||ticks>cap)break;
   }else if(armed)break;
   s.step();
   for(const e of s.events as any[]){if(seen.has(e))continue;seen.add(e);
    if(e.type==='ability'&&stats[e.source]){const st=stats[e.source];st.casts[e.message]=(st.casts[e.message]||0)+1;last[e.source]=e.message;const k=e.source+':'+e.message;castIdx[k]=(castIdx[k]??-1)+1;}}
  }
  const A=s.players.get(seatA),B=s.players.get(seatB),fa=Math.max(0,A.hp)/A.stats.hp,fb=Math.max(0,B.hp)/B.stats.hp;
  const win=B.hp<=0&&A.hp>0?1:A.hp<=0&&B.hp>0?0:.5+(fa-fb)/2,t=ticks/60;
  const out={win,t,hpDiff:fa-fb,dmgA:stats[seatA].total,dmgB:stats[seatB].total,abilA:stats[seatA],abilB:stats[seatB]};
  s.dispose();return out;
 }

 // ---- table 3: sandbox perfect-aim probe (magic-abilities.test.ts recipe) ----
 let seq=0;
 function game(spec:string,dist:number,victim='sword'){
  const s=new Simulation({sandbox:true}),a=s.addPlayer('a','A',WEAPONS[spec].class),b=s.addPlayer('b','B',WEAPONS[victim].class);a.spec=spec;a.chips={};b.spec=victim;s.resetRound();s.phase='fight';
  a.stats=statsFor({},spec);a.hp=a.stats.hp;a.mana=a.stats.maxMana;b.stats=statsFor({},victim);b.hp=b.stats.hp;
  for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});
  a.body.setTranslation(v(0,.9,0),true);b.body.setTranslation(v(0,.9,dist),true);s.world.step();for(const p of [a,b])p.grounded=true;return {s,a,b};
 }
 const step=(s:any,inputs:Record<string,any>={})=>{for(const p of s.players.values()){s.setInput(p.id,{...idleInput(),seq:++seq,yaw:p.input.yaw,pitch:p.input.pitch,camera:p.input.camera,...inputs[p.id]});p.inputAt=s.time;}s.step();};
 const aimAt=(p:any,to:any)=>{const pos=p.body.translation();let yaw=Math.atan2(to.x-pos.x,to.z-pos.z),pitch=0;for(let k=0;k<4;k++){const c=cameraPosition(pos,yaw,pitch,'first');pitch=Math.atan2(to.y-c.y,Math.hypot(to.x-c.x,to.z-c.z));}return {yaw,pitch,camera:'first'};};
 /** Strafer models, both at full speed along ±x (no dodge AI): `turn` re-rolls its direction every .7 s (seeded LCG) and
  * stays within |x| < 9 (magic-abilities.test.ts); `run` keeps one direction (by seed) from x = ∓8, so an ideal lead is exact. */
 const strafer=(s:any,b:any,seed:number,mode:'run'|'turn')=>{let r=seed>>>0,dir=seed%2?1:-1,next=mode==='run'?Infinity:0;if(mode==='run'){const t=b.body.translation();b.body.setTranslation(v(-dir*8,t.y,t.z),true);}
  return ()=>{if(s.time>=next){r=(Math.imul(r,1664525)+1013904223)>>>0;dir=r/4294967296<.5?-1:1;next=s.time+.7;}const x=b.body.translation().x,edge=mode==='run'?28:9;if(x>edge)dir=-1;if(x<-edge)dir=1;return {x:dir,z:0,yaw:Math.PI};};};
 /** Ballistic aim at `t` with ideal lead from the shooter's muzzle (flight time iterated from the horizontal speed). */
 const primaryAim=(s:any,a:any,t:any,vel:any,spec:string,lead:boolean)=>{const w=WEAPONS[spec],speed=w.speed*a.stats.projectileSpeed,grav=w.gravity*a.stats.projectileGravity;let at=t;
  for(let k=0;k<3;k++){const pos=a.body.translation(),yaw=Math.atan2(at.x-pos.x,at.z-pos.z),m=weaponMuzzle(pos,yaw,(o:any,d:any,x:number)=>s.ray(o,d,x,a.id)?.timeOfImpact??null),flight=Math.hypot(at.x-m.x,at.z-m.z)/Math.max(1,speed);if(lead)at=v(t.x+vel.x*flight,t.y,t.z+vel.z*flight);}
  const pos=a.body.translation(),yaw=Math.atan2(at.x-pos.x,at.z-pos.z),m=weaponMuzzle(pos,yaw,(o:any,d:any,x:number)=>s.ray(o,d,x,a.id)?.timeOfImpact??null);
  return {yaw,pitch:ballisticPitch(Math.hypot(at.x-m.x,at.z-m.z),at.y-m.y,speed,grav),camera:'first'};};
 /** Presses `slot` with the aim already set and steps until the windup releases (magic-abilities.test.ts `release`). */
 const cast=(s:any,a:any,slot:'q'|'e',hold:any={})=>{if(s.abilities.press(a,slot)!=='cast')return false;for(let i=0;i<60&&a.abilityCast;i++)step(s,{a:{...hold,yaw:a.input.yaw,pitch:a.input.pitch,camera:'first'}});return true;};
 /** Primary-only TTK from the first shot (still or strafing victim, ideal lead), or the Q+E opener on a caught (still) target:
  * E at the feet, then the magic Q at the body (guns hold ADS throughout), then primaries; timed from the E press. */
 function ttk(j:ProbeJob):number{
  const {s,a,b}=game(j.spec,j.dist);try{const run=strafer(s,b,j.seed*104729+j.seed,j.strafe??'turn'),gun=WEAPONS[j.spec].class==='ranged',kit=kitFor?.(j.spec),hold=gun&&j.opener?{ability:true}:{};let first=-1;
   if(j.opener&&kit){first=s.time;const t=b.body.translation();if(kit.e.live){a.input={...a.input,...aimAt(a,v(t.x,kit.e.bot?.aim==='point'?.02:j.spec==='lightning'?.5:.9,t.z))};cast(s,a,'e',hold);}
    if(!gun&&kit.q.live&&b.hp>0){const u=b.body.translation();a.input={...a.input,...aimAt(a,v(u.x,.9,u.z))};cast(s,a,'q',hold);}}
   for(let i=0;i<60*10&&b.hp>0;i++){const input={...primaryAim(s,a,b.body.translation(),b.body.linvel(),j.spec,!!j.strafe),attack:true,...hold};
    step(s,{a:input,b:j.strafe?run():{yaw:Math.PI}});if(first<0&&s.events.some((e:any)=>e.type==='shot'&&e.source==='a'))first=s.time;}
   return b.hp<=0?s.time-first:Infinity;}finally{s.dispose();}
 }
 /** Damage per E cast against the strafer at the E's bot mid-range. Aim `pos`: its current position (feet for point casts,
  * .5 m for Ball Lightning, chest otherwise); `lead`: the same point led by the strafer's velocity × time to effect (windup
  * plus fuse, flight, or the frag's landing time; the dart flight only for Orbital Mark, whose strike tracks); `lead-feet`:
  * the led point at the feet (ballistic shots splash on the floor). */
 function estrafe(j:ProbeJob):number{
  const def=kitFor(j.spec).e,dist=(def.bot.min+def.bot.max)/2,{s,a,b}=game(j.spec,Math.min(dist,26));try{const run=strafer(s,b,j.seed*104729+j.seed,j.strafe??'turn');
   for(let i=0;i<20+j.seed%40;i++)step(s,{b:run()});const t=b.body.translation(),vel=b.body.linvel(),d=Math.hypot(t.x,t.z);
   const flight=(def.windup??0)+(def.kind==='mark'?d/(def.speed??150):(def.fuse??0)+(def.speed?d/def.speed:0)+(def.kind==='throw'?Math.min(1.1,Math.max(.45,d/16)):0));
   const to=j.aim==='pos'?t:v(t.x+vel.x*flight,t.y,t.z+vel.z*flight),y=def.bot.aim==='point'||j.aim==='lead-feet'?.02:j.spec==='lightning'?.5:.9;
   a.input={...a.input,...aimAt(a,v(to.x,y,to.z))};const hp=b.hp;if(s.abilities.press(a,'e')!=='cast')return NaN;for(let i=0;i<240;i++)step(s,{b:run()});return hp-Math.max(0,b.hp);}finally{s.dispose();}
 }
 parentPort!.on('message',(j:Job|null)=>{if(!j){process.exit(0);}
  const out=j.kind==='duel'?duel(j):{value:j.kind==='ttk'?ttk(j):estrafe(j)};parentPort!.postMessage(out);});
}

// ---------------------------------------------------------------------------------------------------------------------
// Main thread: builds the job list, runs it on the worker pool, prints the tables.
if(isMainThread){
 const RANGED=['pistol','machinegun','rifle'],MAGIC=['fire','water','lightning','darkness'],MELEE=['sword','greatsword','daggers','shield'];
 const list=(name:string,def:string)=>(process.env[name]??def).split(',').map(x=>x.trim()).filter(Boolean);
 const GATE=process.env.DUEL_GATE==='1',N=Number(process.env.DUEL_SEEDS||20),NG=GATE?200:N,NR=Number(process.env.DUEL_REPORT_SEEDS||N),NP=Number(process.env.DUEL_PROBE_SEEDS||(GATE?48:24));
 const SPECS=list('DUEL_SPECS',[...RANGED,...MAGIC].join(',')),TABLES=new Set(list('DUEL_TABLES','1,2,3,4,5').map(Number)),LEAD0=process.env.DUEL_LEAD0!=='0';
 const WORKERS=Math.max(1,Number(process.env.DUEL_WORKERS||4));
 const {BOT_LEAD}=await import(SRC+'/shared/abilities.ts').catch(()=>({BOT_LEAD:[0,0,0]}));const SHIPPED=process.env.DUEL_LEAD?process.env.DUEL_LEAD.split(',').map(Number):[...BOT_LEAD],ZERO=[0,0,0];
 const {MAGIC_CHIPS}=await import(SRC+'/shared/magic-chips.ts').catch(()=>({MAGIC_CHIPS:[]}));
 const cls=(s:string)=>RANGED.includes(s)?'ranged':MAGIC.includes(s)?'magic':'melee';
 const band=(a:string,b:string):[number,number]|null=>{const x=cls(a),y=cls(b);if(x==='melee'||y==='melee')return null;if(x==='magic'&&y==='magic')return [40,60];if(x==='ranged'&&y==='ranged')return [30,70];return [35,65];};

 // ---- pool ----
 async function runAll(jobs:Job[],label:string):Promise<any[]>{
  if(!jobs.length)return [];const out:any[]=new Array(jobs.length);let next=0,done=0;const t0=performance.now();
  const est=jobs.reduce((x,j)=>x+(j.kind==='duel'?(j.arena==='open'?.45:.6):.15),0)/WORKERS;
  console.log(`# ${label}: ${jobs.length} jobs on ${WORKERS} workers; projected ≈ ${(est/60).toFixed(1)} min`);
  await Promise.all(Array.from({length:Math.min(WORKERS,jobs.length)},()=>new Promise<void>((resolve,reject)=>{
   const w=new Worker(new URL(import.meta.url),{env:process.env});let mine=-1;
   const feed=()=>{if(next>=jobs.length){w.postMessage(null);return;}mine=next++;w.postMessage(jobs[mine]);};
   w.on('message',r=>{out[mine]=r;done++;if(done%Math.max(1,Math.round(jobs.length/10))===0)console.log(`#   ${done}/${jobs.length} (${((performance.now()-t0)/60000).toFixed(1)} min)`);feed();});
   w.on('error',reject);w.on('exit',()=>resolve());feed();})));
  console.log(`# ${label}: done in ${((performance.now()-t0)/60000).toFixed(1)} min`);return out;
 }

 // ---- statistics ----
 const wilson=(p:number,n:number):[number,number]=>{const z=1.96,d=1+z*z/n,c=(p+z*z/(2*n))/d,h=z*Math.sqrt(p*(1-p)/n+z*z/(4*n*n))/d;return [Math.max(0,c-h)*100,Math.min(1,c+h)*100];};
 interface Row{chipsA?:Record<string,number>;chipsB?:Record<string,number>;ads?:boolean;key:string;a:string;b:string;arena:Arena;n:number;win:number;ci:[number,number];hpDiff:number;dpsA:number;dpsB:number;t:number;verdict?:string;band?:[number,number];abilA:AbilityStats;abilB:AbilityStats;lead:number[]}
 const merge=(xs:AbilityStats[])=>{const m:AbilityStats={dmg:0,total:0,casts:{},hits:{},abilityDmg:{}};for(const x of xs){m.dmg+=x.dmg;m.total+=x.total;for(const k of ['casts','hits','abilityDmg'] as const)for(const [id,n] of Object.entries(x[k]))m[k][id]=(m[k][id]||0)+n;}return m;};
 function row(job:DuelJob,rs:DuelResult[]):Row{const n=rs.length,p=rs.reduce((x,r)=>x+r.win,0)/n,t=rs.reduce((x,r)=>x+r.t,0);
  return {chipsA:job.chipsA,chipsB:job.chipsB,ads:job.ads,key:job.key,a:job.a,b:job.b,arena:job.arena,n,win:p*100,ci:wilson(p,n),hpDiff:rs.reduce((x,r)=>x+r.hpDiff,0)/n*100,dpsA:rs.reduce((x,r)=>x+r.dmgA,0)/t*10,dpsB:rs.reduce((x,r)=>x+r.dmgB,0)/t*10,t:t/n,abilA:merge(rs.map(r=>r.abilA)),abilB:merge(rs.map(r=>r.abilB)),lead:job.lead};}
 const judge=(r:Row,b:[number,number])=>{r.band=b;const [lo,hi]=r.ci;if(lo>=b[0]-5&&hi<=b[1]+5)return 'PASS';if(hi<b[0]||lo>b[1])return 'FAIL';return 'INCONCLUSIVE';};
 /** Runs `n` seeds of every pairing (seeds 1..n; seats swap on odd seeds) and aggregates one row per pairing. */
 async function pairings(ps:Omit<DuelJob,'kind'|'seed'>[],n:number,label:string,seed0=1):Promise<Row[]>{
  const jobs:DuelJob[]=ps.flatMap(p=>Array.from({length:n},(_,k)=>({...p,kind:'duel' as const,seed:seed0+k})));const rs=await runAll(jobs,label);
  return ps.map((p,i)=>row({...p,kind:'duel',seed:0},rs.slice(i*n,(i+1)*n)));
 }
 /** Gate rows: judge; in gate mode rerun INCONCLUSIVE rows at n=600 and judge the point estimate against band ± 5. */
 async function gate(rows:Row[],bandOf:(r:Row)=>[number,number]|null,label:string){
  for(const r of rows){const b=bandOf(r);if(b)r.verdict=judge(r,b);}
  const redo=rows.filter(r=>r.verdict==='INCONCLUSIVE');if(!GATE||!redo.length)return;
  const more=await pairings(redo.map(r=>({key:r.key,a:r.a,b:r.b,arena:r.arena,lead:r.lead,chipsA:r.chipsA,chipsB:r.chipsB,ads:r.ads})),600-redo[0].n,label+' rerun to n=600',1+redo[0].n);
  redo.forEach((r,i)=>{const m=more[i],n=r.n+m.n,p=(r.win*r.n+m.win*m.n)/n/100;Object.assign(r,{n,win:p*100,ci:wilson(p,n),hpDiff:(r.hpDiff*r.n+m.hpDiff*m.n)/n,abilA:merge([r.abilA,m.abilA]),abilB:merge([r.abilB,m.abilB])});
   r.verdict=(r.win>=r.band![0]-5&&r.win<=r.band![1]+5?'PASS':'FAIL')+' (n=600 point estimate)';});
 }
 const pct=(x:number)=>x.toFixed(1).padStart(5);
 const fmt=(r:Row)=>`${r.a.padEnd(10)} vs ${r.b.padEnd(10)} ${r.arena.padEnd(5)} n=${String(r.n).padEnd(4)} win ${pct(r.win)}% [${pct(r.ci[0])},${pct(r.ci[1])}]  hpΔ ${pct(r.hpDiff)}  dmg/10s ${pct(r.dpsA)}:${pct(r.dpsB)}  t ${r.t.toFixed(1)}s${r.band?`  band ${r.band[0]}-${r.band[1]}`:''}${r.verdict?'  '+r.verdict:''}`;
 const report:Record<string,unknown>={lead:SHIPPED,gate:GATE,n:N,ng:NG};

 // ---- table 1 (+ melee rows; table 5 Pong) ----
 const pairs:[string,string][]=[];for(let i=0;i<SPECS.length;i++)for(let k=i+1;k<SPECS.length;k++)pairs.push([SPECS[i],SPECS[k]]);
 const ARENAS=list('DUEL_ARENAS','open,cover') as Arena[],meleeRows=process.env.DUEL_MELEE==='0'?[]:MELEE.flatMap(m=>SPECS.map(s=>[m,s] as [string,string]));
 async function table1(lead:number[],n:number,tag:string){
  const ps=[...pairs.flatMap(([a,b])=>ARENAS.map(arena=>({key:`${a}|${b}|${arena}`,a,b,arena,lead}))),...meleeRows.map(([a,b])=>({key:`${a}|${b}|cover`,a,b,arena:'cover' as Arena,lead}))];
  const rows=await pairings(ps,n,`table 1 (${tag})`);await gate(rows,r=>r.arena==='open'?band(r.a,r.b):null,`table 1 (${tag})`);return rows;
 }
 async function table5(lead:number[],n:number,tag:string){
  const rows=await pairings(SPECS.map(s=>({key:`pong|${s}`,a:s,b:s,arena:'open' as Arena,lead,chipsA:{pong:1}})),n,`table 5 (${tag})`);await gate(rows,()=>[55,80],`table 5 (${tag})`);return rows;
 }
 const print1=(rows:Row[],title:string)=>{console.log(`\n## ${title}`);for(const r of rows)console.log(fmt(r));
  const m=(a:string,b:string,arena:Arena)=>{const r=rows.find(x=>x.arena===arena&&(x.a===a&&x.b===b||x.a===b&&x.b===a));if(!r)return '   - ';const w=r.a===a?r.win:100-r.win;return pct(w);};
  for(const arena of ['open','cover'] as Arena[]){console.log(`\n${arena}: row spec win % vs column`);console.log(''.padEnd(11)+[...SPECS,...MELEE].map(s=>s.slice(0,6).padStart(7)).join(''));
   for(const a of SPECS)console.log(a.padEnd(11)+[...SPECS,...MELEE].map(b=>a===b?'      ·':' '+m(a,b,arena).padStart(6)).join(''));}};
 const melee=(rows:Row[])=>rows.filter(r=>MELEE.includes(r.a));

 if(TABLES.has(1)||TABLES.has(2)){
  const rows=await table1(SHIPPED,NG,`BOT_LEAD ${JSON.stringify(SHIPPED)}`);report.table1=rows;print1(rows,`Table 1: cross-spec matrix, BOT_LEAD ${JSON.stringify(SHIPPED)} (gate: open rows)`);
  // Melee rows vs the pre-change baseline (DUEL_BASE json from a DUEL_SRC base run): flag a drop of more than 10 points (CI-aware).
  if(process.env.DUEL_BASE){const base:Row[]=(JSON.parse(readFileSync(process.env.DUEL_BASE,'utf8')).table1||[]);console.log('\nMelee rows vs pre-change baseline (flag: drop > 10 points with non-overlapping CI-shifted intervals)');
   for(const r of melee(rows)){const b=base.find(x=>x.key===r.key);if(!b)continue;const drop=b.win-r.win,flag=drop>10&&r.ci[1]<b.ci[0]+10?'FLAG to owner':drop>10?'flag (CI overlaps)':'ok';console.log(`${r.a.padEnd(10)} vs ${r.b.padEnd(10)} base ${pct(b.win)} [${pct(b.ci[0])},${pct(b.ci[1])}] now ${pct(r.win)} [${pct(r.ci[0])},${pct(r.ci[1])}]  Δ ${pct(-drop)}  ${flag}`);}}
  if(LEAD0&&TABLES.has(1)){const zero=await table1(ZERO,N,'BOT_LEAD [0,0,0]');report.table1zero=zero;print1(zero,'Table 1 (bias control): BOT_LEAD [0,0,0], n='+N);}
  if(TABLES.has(2)){
   console.log('\n## Table 2: per-spec Q/E casts per duel, hit rate, ability damage share (table-1 duels, BOT_LEAD shipped)');const per:Record<string,{n:number;s:AbilityStats[]}>={};
   for(const r of rows){for(const [spec,st] of [[r.a,r.abilA],[r.b,r.abilB]] as const){(per[spec]??={n:0,s:[]}).n+=r.n;per[spec].s.push(st);}}
   const share:Record<string,unknown>={};
   for(const spec of SPECS){const p=per[spec];if(!p)continue;const m=merge(p.s);share[spec]=m;const ids=Object.keys({...m.casts,...m.abilityDmg});
    console.log(`${spec.padEnd(10)} ability share ${pct(m.total?m.dmg/m.total*100:0)}% (target ${MAGIC.includes(spec)?'25-45':'15-30'})  `+ids.map(id=>`${id}: ${(m.casts[id]||0)/p.n<10?((m.casts[id]||0)/p.n).toFixed(2):'?'} casts/duel, hit ${pct((m.hits[id]||0)/Math.max(1,m.casts[id]||0)*100)}%, dmg/duel ${((m.abilityDmg[id]||0)/p.n).toFixed(1)}`).join('; '));}
   report.table2=share;
   const guns=SPECS.filter(s=>RANGED.includes(s));const ads=await pairings(guns.flatMap(g=>SPECS.filter(o=>o!==g).flatMap(o=>[true,false].map(on=>({key:`ads-${on?'on':'off'}|${g}|${o}`,a:g,b:o,arena:'open' as Arena,lead:SHIPPED,ads:on})))),NR,'table 2 ADS forced on/off');
   console.log('\nGun ADS forced on vs forced off (open; win % of the gun over all opponents)');
   for(const g of guns){const on=ads.filter(r=>r.a===g&&r.key.startsWith('ads-on')),off=ads.filter(r=>r.a===g&&r.key.startsWith('ads-off')),avg=(rs:Row[])=>rs.reduce((x,r)=>x+r.win,0)/Math.max(1,rs.length);
    console.log(`${g.padEnd(10)} ADS on ${pct(avg(on))}%  off ${pct(avg(off))}%  (per opponent n=${NR}; `+on.map((r,i)=>`${r.b} ${r.win.toFixed(0)}/${off[i].win.toFixed(0)}`).join(', ')+')');}
   report.table2ads=ads;
  }
 }

 // ---- table 3: perfect-aim probe ----
 if(TABLES.has(3)){
  const probe=SPECS.filter(s=>!MELEE.includes(s)),jobs:ProbeJob[]=[];
  const AIMS=['pos','lead','lead-feet'] as const,MODES=['run','turn'] as const;
  for(const spec of probe){for(const dist of [8,15,25]){jobs.push({kind:'ttk',key:`still|${spec}|${dist}`,spec,dist,seed:1});for(const m of MODES)for(let k=1;k<=Math.max(4,NP/4);k++)jobs.push({kind:'ttk',key:`strafe-${m}|${spec}|${dist}`,spec,dist,seed:k,strafe:m});}
   jobs.push({kind:'ttk',key:`opener|${spec}|15`,spec,dist:15,seed:1,opener:true});
   for(const m of MODES)for(const aim of AIMS)for(let k=1;k<=NP;k++)jobs.push({kind:'estrafe',key:`e-${m}-${aim}|${spec}`,spec,dist:0,seed:k,strafe:m,aim});}
  const rs=await runAll(jobs,'table 3 probe');const by:Record<string,number[]>={};jobs.forEach((j,i)=>(by[j.key]??=[]).push(rs[i].value));
  const mean=(xs:number[])=>xs.reduce((a,b)=>a+b,0)/xs.length,med=(xs:number[])=>{const s=[...xs].sort((a,b)=>a-b);return s[Math.floor(s.length/2)];};
  console.log('\n## Table 3: perfect-aim probe (sandbox, flat floor, sword victim 100 HP; strafer 6.8 m/s: run = one direction, turn = re-rolls every .7 s)');
  console.log('Primary TTK in s: still 8/15/25 m | strafer with ideal lead, median, run 8/15/25 | turn 8/15/25 | 15 m ratio vs pistol (run/turn; gate magic ≤ 1.3) | Q+E opener on a still target at 15 m (report: magic 1.0-1.4, guns 1.0-1.5)');
  const f2=(x:number)=>Number.isFinite(x)?x.toFixed(2):'>10',t3:Record<string,unknown>={},pist=(m:string)=>med(by[`strafe-${m}|pistol|15`]??[NaN]);
  for(const spec of probe){const still=[8,15,25].map(d=>by[`still|${spec}|${d}`][0]),run=[8,15,25].map(d=>med(by[`strafe-run|${spec}|${d}`])),turn=[8,15,25].map(d=>med(by[`strafe-turn|${spec}|${d}`])),ratio=[run[1]/pist('run'),turn[1]/pist('turn')],op=by[`opener|${spec}|15`][0];
   const gate=MAGIC.includes(spec)?(Math.max(...ratio)<=1.3?' PASS':' FAIL'):'';t3[spec]={still,run,turn,ratio,opener:op};
   console.log(`${spec.padEnd(10)} ${still.map(f2).join('/')} | ${run.map(f2).join('/')} | ${turn.map(f2).join('/')} | ${ratio.map(x=>x.toFixed(2)).join('/')}${gate} | ${f2(op)}`);}
  console.log(`\nE vs strafer at its bot mid-range (mean damage per cast, n=${NP} per cell): aim pos / lead / lead-feet; gate: best aim ≥ 35 (Orbital Mark ≥ 40) on the run strafer`);
  for(const spec of probe){const cell=(m:string)=>AIMS.map(a=>mean(by[`e-${m}-${a}|${spec}`])),run=cell('run'),turn=cell('turn'),best=Math.max(...run.filter(Number.isFinite)),need=spec==='rifle'?40:35;
   (t3[spec] as any).e={run,turn,best};console.log(`${spec.padEnd(10)} run ${run.map(x=>x.toFixed(1)).join(' / ').padEnd(20)} turn ${turn.map(x=>x.toFixed(1)).join(' / ').padEnd(20)} best ${best.toFixed(1)} ${best>=need?'PASS':'FAIL'} (≥ ${need})`);}
  report.table3=t3;
 }

 // ---- table 4: magic chip deltas ----
 if(TABLES.has(4)&&MAGIC_CHIPS.length){
  console.log(`\n## Table 4: new magic chip deltas (n=${NR} per row; schools rotate by seed; school chips use their school)`);const t4:Record<string,unknown>={};
  for(const chip of MAGIC_CHIPS){const school=(chip.tags as string[]).find(t=>t.startsWith('spell-'))?.slice(6),schools=school?[school]:MAGIC.filter(s=>SPECS.includes(s));
   const ps=schools.flatMap(s=>[{key:`chip|${chip.id}|${s}|mirror`,a:s,b:s,arena:'open' as Arena,lead:SHIPPED,chipsA:{[chip.id]:1}},{key:`chip|${chip.id}|${s}|pistol`,a:s,b:'pistol',arena:'open' as Arena,lead:SHIPPED,chipsA:{[chip.id]:1}},{key:`chip|none|${s}|pistol`,a:s,b:'pistol',arena:'open' as Arena,lead:SHIPPED}]);
   const per=Math.max(1,Math.round(NR/schools.length)),rows=await pairings(ps,per,`table 4 ${chip.id}`);const pool=(f:(r:Row)=>boolean)=>{const rs=rows.filter(f),n=rs.reduce((x,r)=>x+r.n,0),w=rs.reduce((x,r)=>x+r.win*r.n,0)/n,hp=rs.reduce((x,r)=>x+r.hpDiff*r.n,0)/n;return {n,win:w,ci:wilson(w/100,n),hp};};
   const mirror=pool(r=>r.key.endsWith('mirror')),vs=pool(r=>r.key.startsWith('chip|'+chip.id)&&r.key.endsWith('pistol')),none=pool(r=>r.key.startsWith('chip|none'));t4[chip.id]={mirror,vs,none};
   console.log(`${chip.id.padEnd(20)} mirror ${pct(mirror.win)}% [${pct(mirror.ci[0])},${pct(mirror.ci[1])}] hpΔ ${pct(mirror.hp)}   vs pistol ${pct(vs.win)}% (without ${pct(none.win)}%) Δ ${pct(vs.win-none.win)}  hpΔ ${pct(vs.hp-none.hp)}`);}
  report.table4=t4;
 }

 // ---- table 5: Pong ----
 if(TABLES.has(5)){
  const rows=await table5(SHIPPED,NG,`BOT_LEAD ${JSON.stringify(SHIPPED)}`);report.table5=rows;console.log(`\n## Table 5: Pong holder vs the same spec without chips (open; gate 55-80)`);for(const r of rows)console.log(fmt(r));
  if(LEAD0){const zero=await table5(ZERO,N,'BOT_LEAD [0,0,0]');report.table5zero=zero;console.log('\nTable 5 (bias control): BOT_LEAD [0,0,0], n='+N);for(const r of zero)console.log(fmt(r));}
  const t1=report.table1 as Row[]|undefined;if(t1){console.log('\nMelee rows (cover; report: magic 50-85 % vs melee; melee drop vs baseline flagged above when DUEL_BASE is set)');for(const r of melee(t1))console.log(fmt(r));}
 }
 const fails=[...((report.table1 as Row[])||[]),...((report.table5 as Row[])||[])].filter(r=>r.verdict&&!r.verdict.startsWith('PASS'));
 console.log(`\n# gate rows not PASS: ${fails.length}${fails.length?'\n'+fails.map(fmt).join('\n'):''}`);
 if(process.env.DUEL_JSON)writeFileSync(process.env.DUEL_JSON,JSON.stringify(report,null,1));
}
