// PvE real-server suite (design §11 S7 item 5): `node tests/run-network.mjs pve` (ALLOW_TEST_ROOMS, testRoom:true).
// Two clients run the whole Siren flow with tunnelled debug ops from the room owner (god, skip-to-boss, boss-damage,
// kill-adds); every snapshot tick both clients saw must be identical; raw and deflated snapshot sizes are measured
// (p95 raw ≤ 64 KB); a client that drops mid-fight rejoins with its key and receives snapshots again; an old client
// without a hello is refused at ready; a third PvE room is refused. Not part of npm test.
import {WebSocket} from 'ws';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {deflateRawSync} from 'node:zlib';
import type {Snapshot} from '../src/shared/simulation.js';
import {PVE_CONTRACT,BOSS_PLAYER_ID,decodeAdd} from '../src/shared/pve/contract.js';
import {bossHp} from '../src/shared/pve/balance.js';

const endpoint=process.env.TEST_ENDPOINT||'ws://localhost:8792/socket',sleep=(n:number)=>new Promise(r=>setTimeout(r,n));
for(let i=0;i<50;i++){try{await fetch(endpoint.replace('ws:','http:').replace('socket','health'));break;}catch{await sleep(100);}}
async function wait(f:()=>boolean,label:string,max=20000){const start=Date.now();while(!f()){if(Date.now()-start>max)throw Error('Timeout '+label);await sleep(10);}}
const key=(i:number)=>createHash('sha256').update('pve-net-'+i).digest('hex').slice(0,32);

type Client={ws:WebSocket;id:string;code:string;state?:Snapshot;errors:string[];closed?:number;welcome?:{rejoined?:boolean};hashes:Map<number,string>;raw:number[];deflated:number[];snapshots:number};
const sockets:WebSocket[]=[];
async function connect():Promise<Client>{
 const ws=new WebSocket(endpoint);sockets.push(ws);const c:Client={ws,id:'',code:'',errors:[],hashes:new Map(),raw:[],deflated:[],snapshots:0};
 ws.on('message',raw=>{const text=raw.toString(),m=JSON.parse(text);
  if(m.type==='welcome'){c.id=m.id;c.code=m.code;c.welcome=m;}
  if(m.type==='error')c.errors.push(m.message);
  if(m.type==='snapshot'){c.state=m.state;c.snapshots++;c.hashes.set(m.state.tick,createHash('sha256').update(JSON.stringify(m.state)).digest('hex'));
   if(m.state.pve&&m.state.phase==='fight'){c.raw.push(text.length);if(c.snapshots%10===0)c.deflated.push(deflateRawSync(text,{level:1}).length);}}});
 ws.on('close',code=>{c.closed=code;});await new Promise<void>(r=>ws.once('open',r));return c;
}
const tx=(c:Client,m:unknown)=>c.ws.send(JSON.stringify(m));
let nonce=10;const op=(c:Client,o:string)=>tx(c,{type:'chip',pve:{action:'debug',op:o,n:++nonce}});
const pve=(c:Client)=>c.state?.pve;
const p95=(xs:number[])=>{const a=[...xs].sort((x,y)=>x-y);return a[Math.min(a.length-1,Math.floor(a.length*.95))]??0;};
const result:Record<string,unknown>={};

try{
 const a=await connect(),b=await connect();
 tx(a,{type:'create',name:'Ana',class:'ranged',testRoom:true,skipIntros:true,pve:{boss:'siren',debug:true}});
 await wait(()=>!!pve(a)&&!!a.code,'pve room');assert.equal(a.state!.options.pve!.debug,true);
 tx(b,{type:'join',code:a.code,name:'Bo',class:'ranged'});await wait(()=>!!b.id&&!!pve(b),'join');
 // An old client (no hello) is refused at ready with the update error and socket close 4001.
 const old=await connect();tx(old,{type:'join',code:a.code,name:'Old',class:'ranged'});await wait(()=>!!old.id,'old join');
 tx(old,{type:'ready',ready:true});await wait(()=>old.errors.length>0&&old.closed!==undefined,'old client refused');
 assert.equal(old.errors[0],'Update the game to join Boss Test rooms.');assert.equal(old.closed,4001);
 // Room cap: a second PvE room opens, a third is refused, PvP rooms stay open.
 const two=await connect();tx(two,{type:'create',name:'Two',pve:{boss:'siren'}});await wait(()=>!!pve(two),'second pve room');
 const three=await connect();tx(three,{type:'create',name:'Three',pve:{boss:'siren'}});await wait(()=>three.errors.length>0,'third refused');
 assert.equal(three.errors[0],'Boss test rooms are busy on this server.');two.ws.close();three.ws.close();
 // Hellos, ready, pistol, fight.
 const keys=[key(1),key(2)];[a,b].forEach((c,i)=>tx(c,{type:'chip',pve:{action:'hello',v:PVE_CONTRACT,key:keys[i],n:i+1}}));
 await wait(()=>pve(a)!.helloed.length===2,'hellos');
 for(const c of [a,b])tx(c,{type:'ready',ready:true});await wait(()=>a.state?.phase==='specialization','specialization');
 for(const c of [a,b])tx(c,{type:'spec',id:'pistol'});await wait(()=>[a,b].every(c=>c.state?.phase==='fight'),'fight',30000);
 const fightTick=a.state!.tick;
 // Start picks: both choose offer 0 (personal screens), then god and skip to the boss.
 for(const c of [a,b]){await wait(()=>!!pve(c)?.party[c.id]?.open,'start pick '+c.id);const row=pve(c)!.party[c.id],pick=row.picks.find(k=>k.id===row.open)!;
  await wait(()=>!!pve(c)!.party[c.id].picks.find(k=>k.id===pick.id)?.offers,'offers');
  tx(c,{type:'chip',pve:{action:'pick',pick:pick.id,chip:pve(c)!.party[c.id].picks.find(k=>k.id===pick.id)!.offers![0],n:++nonce}});}
 await wait(()=>[a,b].every(c=>!pve(c)!.party[c.id].open),'picks made');
 op(a,'god:on');await wait(()=>pve(a)!.stage==='wave',"wave 1",20000);
 // A non-owner cannot run debug ops.
 op(b,'skip-to-boss');await sleep(300);assert.equal(pve(a)!.stage,'wave','guest debug ignored');
 // Both clients must observe the same empty living horde after a wave-clear transition.
 await wait(()=>pve(a)!.adds.some(t=>decodeAdd(t,pve(a)!.slots).state==='active'),'active wave enemies');
 const beforeClear=pve(a)!.adds.filter(t=>t[7]>0).length;assert.ok(beforeClear>0);
 op(a,'skip-wave');await wait(()=>[a,b].every(c=>pve(c)!.stage==='intermission'),'wave clear on both clients');
 for(const c of [a,b])assert.ok(pve(c)!.adds.every(t=>decodeAdd(t,pve(c)!.slots).state==='dead'&&t[7]===0),'no surviving or entering adds');
 await sleep(500);
 for(const c of [a,b])assert.ok(pve(c)!.adds.every(t=>t[7]===0),'cancelled arrivals stay cancelled');
 result.waveClear={clients:2,liveAddsBefore:beforeClear,liveAddsAfter:0};
 op(a,'skip-to-boss');await wait(()=>pve(a)!.cinematic?.kind==='reveal'&&pve(a)!.cinematic?.chip===1,'reveal 1');
 await wait(()=>pve(a)!.cinematic?.kind==='boss-intro','boss intro',10000);
 await wait(()=>pve(a)!.stage==='boss'&&!pve(a)!.hold,'boss stage',20000);
 const hp=bossHp(2),boss=()=>a.state!.players.find(p=>p.id===BOSS_PLAYER_ID);assert.ok(boss(),'boss seated');
 // Drop B mid-fight and rejoin with its key.
 const oldB=b.id;b.ws.close();await sleep(400);
 const r=await connect();tx(r,{type:'join',code:a.code,rejoin:keys[1],name:'Bo'});await wait(()=>!!r.welcome,'rejoin welcome');
 assert.equal(r.welcome!.rejoined,true);await wait(()=>!!r.state&&r.state.players.some(p=>p.id===r.id&&p.connected),'rejoined seat');
 assert.ok(!r.state!.players.some(p=>p.id===oldB),'old id gone');const before=r.snapshots;await sleep(500);assert.ok(r.snapshots>before+5,'snapshots flow again');
 // Phase 1 → floor 75% → chip 2 (possession) → kill-adds finds the Siren → chip 3 (grow) → 25% → chip 4 → death.
 op(a,`boss-damage:${hp}`);await wait(()=>pve(a)!.cinematic?.chip===2,'chip 2');
 assert.ok(Math.abs(boss()!.hp-.75*hp)<1,'floor 75%');
 await wait(()=>pve(a)!.bossView?.phase===2&&pve(a)!.bossView?.possessed!==undefined&&!pve(a)!.hold,'possession',20000);
 assert.equal(pve(a)!.bossView!.hidden,true);
 for(let i=0;i<40&&pve(a)!.bossView?.foundAt===undefined;i++){op(a,'kill-adds');await sleep(500);}assert.notEqual(pve(a)!.bossView?.foundAt,undefined,'found');
 await wait(()=>pve(a)!.bossView?.phase===3&&!pve(a)!.hold,'phase 3',20000);
 op(a,`boss-damage:${hp}`);await wait(()=>pve(a)!.cinematic?.chip===4,'chip 4');
 await wait(()=>pve(a)!.bossView?.phase===4&&!pve(a)!.hold,'phase 4',20000);
 op(a,`boss-damage:${hp}`);await wait(()=>pve(a)!.cinematic?.kind==='outro','outro');assert.equal(pve(a)!.cinematic!.outcome,'victory');
 await wait(()=>a.state?.phase==='match'&&r.state?.phase==='match','match',15000);
 assert.equal(pve(a)!.outcome,'victory');assert.equal(pve(r)!.outcome,'victory');assert.ok(pve(a)!.party[a.id].bossDamage>0);
 await sleep(300);
 // Identical snapshots: every tick both clients received (A with B before the drop, A with the rejoined B after).
 const compare=(x:Client,y:Client)=>{const shared=[...x.hashes.keys()].filter(t=>t>=fightTick&&y.hashes.has(t));for(const t of shared)assert.equal(x.hashes.get(t),y.hashes.get(t),'tick '+t);return shared.length;};
 const sharedBefore=compare(a,b),sharedAfter=compare(a,r);assert.ok(sharedBefore>=100&&sharedAfter>=300,`shared ${sharedBefore}/${sharedAfter}`);
 const rawP95=p95(a.raw);assert.ok(rawP95<=64*1024,`p95 raw ${rawP95}`);
 result.pve={sharedTicks:sharedBefore+sharedAfter,snapshots:a.snapshots,rawP95,rawMax:Math.max(...a.raw),deflatedP95:p95(a.deflated),deflatedMax:Math.max(...a.deflated),
  oldClientRefused:true,thirdRoomRefused:true,rejoined:true,outcome:'victory'};
 console.log(JSON.stringify(result));
}finally{sockets.forEach(s=>s.close());}
process.exit(0);
