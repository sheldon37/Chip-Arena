/** PvE loopback smoke (design §11 F acceptance 10 and 13). Run: node --import tsx tests/pve-loopback-network.ts
 *  Part 1: in-process WebSocket server + HostedRoom (like siren-network.ts): hellos, hello-gated ready, tunnelled
 *  {type:'chip',pve} commands, 300 byte-identical snapshots with `pve`.
 *  Part 2: the real authoritative server (src/server/index.ts on TEST_PORT or 8794): PvE create with a server-chosen seed,
 *  the update error and socket close on ready without a hello, tunnelled commands, the PvE room cap and /health fields,
 *  and a WebSocket rejoin during the fight. Not part of npm test. */
import {WebSocket,WebSocketServer} from 'ws';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {spawn} from 'node:child_process';
import {HostedRoom} from '../src/shared/host-room.js';
import {initPhysics,type Snapshot} from '../src/shared/simulation.js';
import {idleInput} from '../src/shared/config.js';
import {PVE_CONTRACT,type PveCommand} from '../src/shared/pve/contract.js';
await initPhysics();
const pause=(ms:number)=>new Promise(r=>setTimeout(r,ms));
async function until(check:()=>boolean,label:string,timeout=12000){const at=Date.now();while(!check()){if(Date.now()-at>timeout)throw Error('Timeout: '+label);await pause(10);}}
const key=(i:number)=>createHash('sha256').update('pve-key-'+i).digest('hex').slice(0,32);
const result:Record<string,unknown>={};

// ---------- Part 1: in-process HostedRoom ----------
{
 const peers=new Map<string,WebSocket>(),server=new WebSocketServer({host:'127.0.0.1',port:0});await new Promise<void>(r=>server.once('listening',r));
 const room=new HostedRoom('PVELOOP1',{name:'Host',class:'ranged',color:'#ff6633'},false,(target,m)=>{for(const [id,ws]of peers)if((target==='all'||id===target)&&ws.readyState===WebSocket.OPEN)ws.send(JSON.stringify(m));},6,false,true,undefined,{pve:{boss:'siren'}});
 const commands:{id:string;cmd:PveCommand}[]=[];room.sim.pve!.economy.command=(id,cmd)=>{commands.push({id,cmd});return true;};
 let count=0;server.on('connection',ws=>{const id=count++===0?'host':'p'+(count-1);peers.set(id,ws);if(id!=='host')room.join({name:id,class:'ranged'},false,id);ws.on('message',raw=>room.receive(id,raw.toString()));ws.on('close',()=>{peers.delete(id);room.disconnect(id);});});
 const clients:WebSocket[]=[],states:Snapshot[]=[],errors:string[][]=[[],[],[]],hashes=[new Map<number,string>(),new Map<number,string>()];
 const send=(i:number,m:unknown)=>clients[i].send(JSON.stringify(m));
 let clock:ReturnType<typeof setInterval>|undefined;
 try{
  for(let i=0;i<3;i++){const ws=new WebSocket('ws://127.0.0.1:'+(server.address() as {port:number}).port);clients.push(ws);
   ws.on('message',raw=>{const m=JSON.parse(raw.toString());if(m.type==='error'){errors[i].push(m.message);if(m.fatal)ws.close();}if(m.type==='snapshot'){states[i]=m.state;if(i<2)hashes[i].set(m.state.tick,createHash('sha256').update(JSON.stringify(m.state)).digest('hex'));}});
   await new Promise<void>(r=>ws.once('open',r));await until(()=>peers.size===i+1,'seat '+i);}
  room.sim.options.countdown=.05;clock=setInterval(()=>room.step(),5);
  await until(()=>states.filter(Boolean).length===3&&states.every(s=>s.pve?.v===1&&s.players.length===3),'pve lobby snapshots');
  // An outdated client (no hello) is refused on ready with the update error and leaves the lobby.
  send(2,{type:'ready',ready:true});await until(()=>errors[2].length>0,'update error');assert.equal(errors[2][0],'Update the game to join Boss Test rooms.');
  await until(()=>room.sim.players.size===2,'outdated client dropped');
  for(let i=0;i<2;i++)send(i,{type:'chip',pve:{action:'hello',v:PVE_CONTRACT,key:key(i),n:1}});
  await until(()=>states[0].pve!.helloed.length===2,'hellos');assert.deepEqual(states[0].pve!.helloed.sort(),['host','p1']);
  for(let i=0;i<2;i++)send(i,{type:'ready',ready:true});await until(()=>room.sim.phase==='specialization','specialization');
  for(let i=0;i<2;i++)send(i,{type:'spec',id:'pistol'});await until(()=>states.slice(0,2).every(s=>s.phase==='fight'),'fight',20000);
  send(1,{type:'chip',pve:{action:'open',n:2}});send(1,{type:'chip',pve:{action:'close',n:3}});send(0,{type:'chip',pve:{action:'open',pick:'pick-1',n:4}});
  await until(()=>commands.length===3,'tunnelled commands');assert.deepEqual(commands.map(c=>[c.id,c.cmd.action]),[['p1','open'],['p1','close'],['host','open']]);
  const startTick=room.sim.tick;
  for(let seq=1;room.sim.tick<startTick+960;seq++){for(let i=0;i<2;i++){const s=states[i];send(i,{type:'inputs',epoch:`${s.setNumber}:${s.round}:${s.generation}`,inputs:[{...idleInput(),seq,x:Math.sin(seq/20)*(i?1:-1),yaw:seq*.02}]});}await pause(16);}
  await pause(100);const shared=[...hashes[0].keys()].filter(t=>t>startTick&&hashes[1].has(t));
  assert.ok(shared.length>=300,`shared snapshots ${shared.length}`);for(const t of shared)assert.equal(hashes[0].get(t),hashes[1].get(t),'tick '+t);
  assert.ok(states.slice(0,2).every(s=>s.pve&&s.phase==='fight'));assert.deepEqual(errors.slice(0,2),[[],[]]);
  result.loopback={identicalSnapshots:shared.length,hellos:2,updateErrorOnReady:true,tunnelled:commands.length,snapshotBytes:JSON.stringify(states[0]).length};
  // Boss half (S7): authority-side debug ops (this room has no debug option; the server suite covers tunnelled ops).
  const pv=room.sim.pve!;pv.debug('god:on');assert.equal(pv.debug('skip-to-boss'),true);const bossStart=room.sim.tick;
  const seenKinds=new Set<string>();
  for(let seq=2000;!(states[0].pve?.stage==='boss'&&!states[0].pve.hold)&&room.sim.tick<bossStart+60*25;seq++){
   for(let i=0;i<2;i++){const s=states[i];send(i,{type:'inputs',epoch:`${s.setNumber}:${s.round}:${s.generation}`,inputs:[{...idleInput(),seq}]});}
   const c=states[0].pve?.cinematic;if(c)seenKinds.add(c.kind+(c.chip??''));await pause(16);}
  assert.equal(states[0].pve?.stage,'boss');assert.ok(seenKinds.has('reveal1')&&seenKinds.has('boss-intro'),[...seenKinds].join());
  assert.ok(states.slice(0,2).every(s=>s.players.some(p=>p.id==='pve-boss')),'both see the boss');
  const boss=room.sim.players.get('pve-boss')!;pv.debug(`boss-damage:${Math.ceil(boss.stats.hp)}`);
  await until(()=>states.slice(0,2).every(s=>s.pve?.cinematic?.kind==='reveal'&&s.pve.cinematic.chip===2),'chip 2 on both clients');
  await pause(200);const bossShared=[...hashes[0].keys()].filter(t=>t>bossStart&&hashes[1].has(t));
  assert.ok(bossShared.length>=200,`boss-half shared snapshots ${bossShared.length}`);for(const t of bossShared)assert.equal(hashes[0].get(t),hashes[1].get(t),'boss tick '+t);
  result.loopbackBoss={identicalSnapshots:bossShared.length,reveal:true,intro:true,chip2:true};
 }finally{if(clock)clearInterval(clock);clients.forEach(c=>c.close());await pause(50);server.close();room.dispose();}
}

// ---------- Part 2: the real server ----------
{
 const port=process.env.TEST_PORT||'8794',base=`http://127.0.0.1:${port}`,endpoint=`ws://127.0.0.1:${port}/socket`;
 const server=spawn(process.execPath,['--import','tsx','src/server/index.ts'],{stdio:['ignore','ignore','inherit'],env:{...process.env,PORT:port,ALLOW_TEST_ROOMS:'true'}});
 const sockets:WebSocket[]=[];
 try{
  let ready=false;for(let i=0;i<150&&!ready;i++){try{ready=(await fetch(base+'/health')).ok;}catch{await pause(100);}}assert.ok(ready,'server ready');
  type Client={ws:WebSocket;id:string;code:string;state?:Snapshot;errors:string[];closed?:number;welcome?:any};
  const connect=async()=>{const ws=new WebSocket(endpoint);sockets.push(ws);const c:Client={ws,id:'',code:'',errors:[]};
   ws.on('message',raw=>{const m=JSON.parse(raw.toString());if(m.type==='welcome'){c.id=m.id;c.code=m.code;c.welcome=m;}if(m.type==='error')c.errors.push(m.message);if(m.type==='snapshot')c.state=m.state;});
   ws.on('close',code=>{c.closed=code;});await new Promise<void>(r=>ws.once('open',r));return c;};
  const tx=(c:Client,m:unknown)=>c.ws.send(JSON.stringify(m));
  const a=await connect();tx(a,{type:'create',name:'Ana',class:'ranged',testRoom:true,skipIntros:true,pve:{boss:'siren',seed:1,debug:true,partyBots:1}});
  await until(()=>!!a.state?.pve,'pve room');const opts=a.state!.options.pve!;assert.notEqual(opts.seed,1,'server picks the seed');assert.equal(opts.debug,true,'test room owner may debug');assert.equal(opts.partyBots,1);
  assert.ok(a.state!.players.some(p=>p.id==='party-bot-1'));
  const b=await connect();tx(b,{type:'join',code:a.code,name:'Old',class:'ranged'});await until(()=>!!b.id,'join');
  tx(b,{type:'ready',ready:true});await until(()=>b.errors.length>0&&b.closed!==undefined,'old client removed');assert.equal(b.errors[0],'Update the game to join Boss Test rooms.');assert.equal(b.closed,4001);
  const k=key(9);tx(a,{type:'chip',pve:{action:'hello',v:PVE_CONTRACT,key:k,n:1}});await until(()=>!!a.state?.pve?.helloed.includes(a.id),'hello');
  tx(a,{type:'chip',pve:{action:'debug',op:'god:on',n:2}});tx(a,{type:'ready',ready:true});await until(()=>a.state?.phase==='specialization','spec');
  tx(a,{type:'spec',id:'pistol'});await until(()=>a.state?.phase==='fight','fight',20000);
  // Room cap and /health.
  const c2=await connect();tx(c2,{type:'create',name:'Two',pve:{boss:'siren'}});await until(()=>!!c2.state?.pve,'second pve room');
  const c3=await connect();tx(c3,{type:'create',name:'Three',pve:{boss:'siren'}});await until(()=>c3.errors.length>0,'third refused');assert.equal(c3.errors[0],'Boss test rooms are busy on this server.');
  const pvp=await connect();tx(pvp,{type:'create',name:'PvP'});await until(()=>!!pvp.state&&!pvp.state.pve,'PvP rooms still open');
  const health=await (await fetch(base+'/health')).json();assert.equal(health.pveRooms,2);assert.equal(typeof health.maxTickMs,'number');
  // Rejoin during the fight with the same key.
  const oldId=a.id;a.ws.close();await pause(300);
  const r=await connect();tx(r,{type:'join',code:a.code,rejoin:k,name:'Ana'});await until(()=>!!r.welcome,'rejoin welcome');assert.equal(r.welcome.rejoined,true);
  await until(()=>!!r.state&&r.state.players.some(p=>p.id===r.id&&p.connected),'rejoined seat');assert.ok(!r.state!.players.some(p=>p.id===oldId));
  assert.ok(r.state!.events.some(e=>e.type==='pve-rejoin'&&e.target===r.id));
  const bad=await connect();tx(bad,{type:'join',code:a.code,rejoin:'f'.repeat(32)});await until(()=>bad.errors.length>0,'bad rejoin');assert.equal(bad.errors[0],'That boss run has ended or the seat is gone.');
  result.server={pveSeedFromServer:true,updateErrorClose:4001,thirdCreateRefused:true,health:{pveRooms:health.pveRooms,maxTickMs:health.maxTickMs},rejoined:true};
 }finally{sockets.forEach(s=>s.close());server.kill();}
}
console.log(JSON.stringify(result));
process.exit(0);
