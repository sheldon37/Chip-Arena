// S4 session (design §7.8, §7.9, D31-D33): hello, rejoin key and state machine, LocalRoom over a fake worker, fixtures.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import type {PlayerView,Snapshot} from '../src/shared/simulation.js';
import {BOSS_PLAYER_ID,PVE_CONTRACT,sanitizePveCommand,isPveWire} from '../src/shared/pve/contract.js';
import {PveHello,PveNonce,RejoinMachine,REJOIN,rejoinKey,KEY_PREFIX,DEBUG_OPS,PveFixtureInjector,type KeyStore} from '../src/client/pve/session.js';
import {LocalRoom,LOCAL_ROOM_CODE,type WorkerLike} from '../src/client/pve/local-room.js';

class Store implements KeyStore{map=new Map<string,string>();getItem(k:string){return this.map.get(k)??null;}setItem(k:string,v:string){this.map.set(k,v);}}
const pveState={options:{pve:{boss:'siren',seed:1}}} as unknown as Snapshot,pvpState={options:{}} as unknown as Snapshot;
let n=0;const random=()=>(++n).toString(16).padStart(32,'0');

test('exactly one hello per room entry, carrying the contract version and the room key',()=>{
 const h=new PveHello(),store=new Store();
 assert.equal(h.update(pveState,store,random),null,'no hello before a welcome');h.enter('ROOM1');
 assert.equal(h.update(pvpState,store,random),null,'no hello in a PvP room');
 const hello=h.update(pveState,store,random)!;assert.equal(hello.action,'hello');assert.equal((hello as {v:number}).v,PVE_CONTRACT);
 assert.ok(sanitizePveCommand(hello),'the authority accepts it');
 assert.equal(h.update(pveState,store,random),null,'only once');
 h.enter('ROOM1');const again=h.update(pveState,store,random) as {key:string};assert.equal(again.key,(hello as {key:string}).key,'a new entry sends a new hello with the same key');
 h.leave();assert.equal(h.update(pveState,store,random),null);
});

test('the rejoin key persists per room code (32 hex chars) and survives broken storage',()=>{
 const store=new Store(),a=rejoinKey('AAAA',store,random),b=rejoinKey('BBBB',store,random);
 assert.match(a,/^[0-9a-f]{32}$/);assert.notEqual(a,b);assert.equal(rejoinKey('AAAA',store,random),a);assert.equal(store.getItem(KEY_PREFIX+'AAAA'),a);
 store.setItem(KEY_PREFIX+'CCCC','not-hex');assert.match(rejoinKey('CCCC',store,random),/^[0-9a-f]{32}$/,'a corrupt key is replaced');
 const throwing:KeyStore={getItem:()=>null,setItem:()=>{throw Error('quota');}};assert.match(rejoinKey('DDDD',throwing,random),/^[0-9a-f]{32}$/);
 assert.match(rejoinKey('EEEE',undefined),/^[0-9a-f]{32}$/,'crypto default');
});

test('rejoin: WebSocket PvE fights only, 2 s apart, at most 3 attempts',()=>{
 const ctx={websocket:true,pve:true,phase:'fight',left:false,code:'ROOM1'};
 for(const bad of [{websocket:false},{pve:false},{phase:'lobby'},{left:true},{code:''}]){const m=new RejoinMachine();assert.equal(m.lost(0,{...ctx,...bad}),false,JSON.stringify(bad));assert.equal(m.active,false);}
 const m=new RejoinMachine();assert.equal(m.lost(1000,ctx),true);assert.equal(m.code,'ROOM1');
 assert.equal(m.due(2999),false);assert.equal(m.due(3000),true);assert.equal(m.phase,'connecting');assert.equal(m.due(3001),false,'one socket per attempt');
 m.failed(3500,'refused');assert.equal(m.phase,'waiting');assert.equal(m.due(5499),false);assert.equal(m.due(5500),true);
 m.failed(6000);assert.equal(m.due(8000),true);assert.equal(m.attempts,REJOIN.attempts);
 m.failed(8500,'gone');assert.equal(m.phase,'failed');assert.equal(m.active,false);assert.equal(m.due(99999),false,'never a fourth attempt');assert.equal(m.error,'gone');
 const ok=new RejoinMachine();ok.lost(0,ctx);ok.due(2000);ok.succeeded();assert.equal(ok.phase,'done');assert.equal(ok.active,false);
});

test('commands carry a distinct nonce and travel as {type:chip,pve}; debug ops are well formed',()=>{
 const nonce=new PveNonce(),a=nonce.wrap({action:'open'}),b=nonce.wrap({action:'open'});
 assert.ok(isPveWire(a));assert.notEqual(a.pve.n,b.pve.n);assert.ok(sanitizePveCommand(a.pve));
 assert.deepEqual([DEBUG_OPS.skipWave(),DEBUG_OPS.skipToBoss(),DEBUG_OPS.phase(9),DEBUG_OPS.money(250.4),DEBUG_OPS.god(false),DEBUG_OPS.killAdds(),DEBUG_OPS.spawn('brute')],
  ['skip-wave','skip-to-boss','boss-phase:4','money:250','god:off','kill-adds','spawn:brute']);
 for(const op of ['skip-wave','boss-phase:4','spawn:brute'])assert.ok(sanitizePveCommand({action:'debug',op}));
});

class FakeWorker implements WorkerLike{posted:any[]=[];terminated=false;onmessage:((e:{data:any})=>void)|null=null;onerror:((e:unknown)=>void)|null=null;
 postMessage(m:unknown){this.posted.push(m);}terminate(){this.terminated=true;}}
test('LocalRoom: creates the local worker room, routes send/pause, delivers host and all messages, closes cleanly',()=>{
 const worker=new FakeWorker(),received:any[]=[],ended:string[]=[];let status='';
 const room=new LocalRoom({name:'Pilot',class:'ranged',color:'#ff6b35'},{pve:{boss:'siren',partyBots:2},local:true},m=>received.push(m),s=>{status=s;},r=>ended.push(r),{worker:()=>worker,skipIntros:true});
 assert.equal(room.host,true);assert.equal(room.code,LOCAL_ROOM_CODE);assert.equal(status,'LOCAL BOSS TEST');
 assert.deepEqual(worker.posted[0].type,'create');assert.deepEqual(worker.posted[0].settings,{pve:{boss:'siren',partyBots:2},local:true});assert.equal(worker.posted[0].skipIntros,true);
 room.send({type:'ready',ready:true});assert.deepEqual(worker.posted[1],{type:'message',id:'host',message:{type:'ready',ready:true}});
 room.pause(true);room.pause(true);room.pause(false);assert.deepEqual(worker.posted.slice(2),[{type:'pause',paused:true},{type:'pause',paused:false}],'pause posts only on change');
 worker.onmessage!({data:{target:'host',message:{type:'welcome',id:'host'}}});worker.onmessage!({data:{target:'all',message:{type:'snapshot'}}});worker.onmessage!({data:{target:'guest',message:{type:'x'}}});
 assert.deepEqual(received.map(m=>m.type),['welcome','snapshot'],'host and all only');
 worker.onerror!(new Error('boom'));assert.equal(ended.length,1);assert.equal(worker.terminated,true);assert.equal(room.closed,true);
 room.send({type:'ready'});assert.equal(worker.posted.length,4,'nothing is sent after close');
});

const me={id:'host',name:'Pilot',color:'#ff6b35',class:'ranged',spec:'pistol',pos:{x:1,y:.9,z:2},yaw:0,hp:100,maxHp:100,chips:{},bot:null,connected:true} as unknown as PlayerView;
const base=(time=50)=>({time,phase:'fight',players:[me],events:[],options:{},generation:0} as unknown as Snapshot);
test('fixture injection: party members exist as players, boss fixtures add the boss, results present the match phase, freeze keeps the moment',()=>{
 const f=new PveFixtureInjector();
 f.set('party',base(),'host');let s=f.apply(base(51));
 assert.ok(s.pve&&s.options.pve,'pve and options.pve are injected');assert.equal(s.players.length,4);assert.ok(s.players.some(p=>p.name==='Kestrel'&&p.hp===0),'dead fixture rows have hp 0');
 s=f.apply(f.apply(base(52)));assert.equal(s.players.length,4,'re-applying never duplicates');
 f.set('boss-2',s,'host');s=f.apply(base(53));assert.ok(s.players.some(p=>p.id===BOSS_PLAYER_ID));assert.equal(s.players.length,5);
 f.set('boss-intro',s,'host');s=f.apply(base(53));assert.equal(s.intro?.boss,true);assert.deepEqual(s.intro?.players,[BOSS_PLAYER_ID]);
 f.set('results-victory',s,'host');assert.equal(f.apply(base()).phase,'match');
 f.set('reveal-1',base(60),'host',{freeze:true,dt:-1});const a=f.apply(base(60)).pve!.cinematic!,b=f.apply(base(70)).pve!.cinematic!;assert.equal(70-b.at,60-a.at,'frozen age');
 f.set('wave',base(60),'host',{events:[{type:'pve-kill',source:'host',amount:10}]});const ev=f.apply(base(60)).events;assert.equal(ev.length,1);assert.equal(ev[0].type,'pve-kill');
 f.set(null,base(),'host');const plain=base();assert.equal(f.apply(plain),plain,'cleared');
});
