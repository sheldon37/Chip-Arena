/** Run with node --import tsx tests/siren-network.ts; loopback only, disposable room. */
import {WebSocket,WebSocketServer} from 'ws';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {HostedRoom} from '../src/shared/host-room.js';
import {initPhysics,type Snapshot} from '../src/shared/simulation.js';
import {idleInput,v,type Input} from '../src/shared/config.js';
await initPhysics();
const peers=new Map<string,WebSocket>(),server=new WebSocketServer({host:'127.0.0.1',port:0});
await new Promise<void>(r=>server.once('listening',r));
const room=new HostedRoom('SIREN234',{name:'Boss',class:'dev',color:'#ffffff'},false,(target,m)=>{for(const [id,ws]of peers)if((target==='all'||id===target)&&ws.readyState===WebSocket.OPEN)ws.send(JSON.stringify(m));},6,true,true);
let count=0;server.on('connection',ws=>{const id=count++===0?'host':'p'+(count-1);peers.set(id,ws);if(id!=='host')room.join({name:id,class:'dev',developer:true},id==='p1',id);ws.on('message',raw=>room.receive(id,raw.toString()));});
const clients:WebSocket[]=[],states:Snapshot[]=[],hashes=Array.from({length:3},()=>new Map<number,string>()),seen=new Set<string>();
const pause=(ms:number)=>new Promise(r=>setTimeout(r,ms));
async function until(check:()=>boolean,label:string){const at=Date.now();while(!check()){if(Date.now()-at>6000)throw Error('Timeout: '+label);await pause(10);}}
const send=(i:number,message:unknown)=>clients[i].send(JSON.stringify(message));
let clock:ReturnType<typeof setInterval>|undefined,pump:ReturnType<typeof setInterval>|undefined;
try{
 for(let i=0;i<3;i++){const ws=new WebSocket('ws://127.0.0.1:'+(server.address() as {port:number}).port);clients.push(ws);ws.on('message',raw=>{const m=JSON.parse(raw.toString());if(m.type==='snapshot'){states[i]=m.state;hashes[i].set(m.state.tick,createHash('sha256').update(JSON.stringify(m.state)).digest('hex'));for(const e of m.state.events)seen.add(e.type);}});await new Promise<void>(r=>ws.once('open',r));}
 await until(()=>peers.size===3,'seats');assert.equal(room.sim.players.get('p2')!.class,'ranged');room.sim.options.countdown=.05;clock=setInterval(()=>room.step(),5);
 send(0,{type:'settings',botCount:4,botWeapons:['siren','siren','emperor','ansatsuken']});
 await until(()=>states.filter(Boolean).length===3&&states.every(s=>s.players.filter(p=>p.bot).length===4),'shared DEV bots');
 for(const state of states){assert.equal(state.players.filter(p=>p.spec==='siren').length,1);const boss=state.players.find(p=>p.bot&&p.spec==='siren')!;assert.equal(boss.hp,15000);assert.equal(boss.maxHp,15000);assert.equal(boss.developer,false);assert.deepEqual(boss.unlocks,['weapon:siren']);}
 send(1,{type:'settings',botCount:0,developer:true});await pause(40);assert.equal(room.sim.options.botCount,4,'a DEV guest cannot configure host bots');
 send(0,{type:'settings',botCount:0,gameMode:'teams'});await until(()=>states.every(s=>s.players.every(p=>!p.bot)&&s.options.gameMode==='teams'),'bot slot released');
 for(const target of ['host','p1','p2'])send(0,{type:'team',target,team:target==='host'?'blue':'red'});
 const teamsMatch=(s:Snapshot)=>s.options.gameMode==='teams'&&s.players.every(p=>p.team===(p.id==='host'?'blue':'red'));
 await until(()=>states.every(teamsMatch),'shared team assignments');
 for(let i=0;i<3;i++)send(i,{type:'ready',ready:true});await until(()=>states.filter(Boolean).length===3&&states.every(s=>s.phase==='specialization'),'selection');
 send(0,{type:'spec',id:'siren'});await until(()=>room.sim.players.get('host')!.selected,'boss selection');send(1,{type:'spec',id:'siren'});send(2,{type:'spec',id:'siren'});await pause(40);assert.notEqual(room.sim.players.get('p1')!.spec,'siren');assert.notEqual(room.sim.players.get('p2')!.spec,'siren');send(1,{type:'spec',id:'emperor'});send(2,{type:'spec',id:'pistol'});
 await until(()=>states.every(s=>s.phase==='fight'),'fight');assert.ok(states.every(s=>teamsMatch(s)&&s.options.map==='siren'&&s.bossSong&&s.players.find(p=>p.id==='host')?.maxHp===15000));const s=room.sim,a=s.players.get('host')!,b=s.players.get('p1')!;
 assert.equal(s.damage(s.players.get('p2')!,10,b.id,'direct'),0,'red teammates remain protected');
 for(const prop of [...s.props.values()])s.removeProp(prop.id);s.addProp({id:'test-floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});a.body.setTranslation(v(0,1,0),true);b.body.setTranslation(v(0,1,2),true);b.hp=b.stats.hp=1000;s.players.get('p2')!.body.setTranslation(v(15,1,15),true);s.world.propagateModifiedBodyPositionsToColliders();
 let seq=0,held:Partial<Input>={};pump=setInterval(()=>send(0,{type:'inputs',epoch:`${s.setNumber}:${s.round}:${s.generation}`,inputs:[{...idleInput(),seq:++seq,...held}]}),5);
 held={attack:true};await until(()=>states.every(x=>x.players.find(p=>p.id==='p1')!.hp<1000),'shared claw damage');held={};await pause(160);
 held={sirenToggle:true};await until(()=>states.every(x=>x.players.find(p=>p.id==='host')!.siren?.mode==='ranged'),'shared mode');held={};await pause(160);
 held={sirenAbility:4};await until(()=>states.every(x=>x.siren?.marks.some(m=>m.shot)&&x.projectiles.some(p=>p.siren?.kind==='seeker')),'shared mark and seeker');held={};
 assert.ok(seen.has('siren-lock'));const epoch=s.bossSong!.startedAt;s.resetRound();s.countdown();await until(()=>states.every(x=>x.generation===s.generation&&x.bossSong?.startedAt===epoch&&teamsMatch(x)),'song and teams survive reset');
 const shared=[...hashes[0].keys()].filter(t=>hashes.every(h=>h.has(t)));assert.ok(shared.length>30);for(const tick of shared)assert.equal(new Set(hashes.map(h=>h.get(tick))).size,1);
 console.log(JSON.stringify({clients:3,forgedAccessRejected:true,duplicateSirenRejected:true,devBotLoadoutsVerified:true,sirenHpVerified:15000,teamAssignmentsPreserved:true,alliedDamageBlocked:true,sharedMeleeDamage:true,sharedMode:true,sharedMarkedSeeker:true,songContinuous:true,identicalSnapshots:shared.length}));
}finally{if(pump)clearInterval(pump);if(clock)clearInterval(clock);for(const c of clients)c.close();await pause(25);await new Promise<void>(r=>server.close(()=>r()));room.dispose();}
