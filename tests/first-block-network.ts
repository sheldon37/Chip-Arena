import {WebSocket} from 'ws';
import assert from 'node:assert/strict';
import type {Snapshot} from '../src/shared/simulation.js';
import {testPilot} from '../src/shared/test-driver.js';
import {idleInput} from '../src/shared/config.js';
import {FIRST_BLOCK_CHIPS} from '../src/shared/blocking.js';
const endpoint=process.env.TEST_ENDPOINT||'ws://localhost:8792/socket',sleep=(n:number)=>new Promise(r=>setTimeout(r,n));
for(let n=0;n<50;n++){try{await fetch(endpoint.replace('ws:','http:').replace('socket','health'));break;}catch{await sleep(100);}}
const clients=[new WebSocket(endpoint),new WebSocket(endpoint)],ids=['',''],states:(Snapshot|null)[]=[null,null],seen=[new Map<number,Snapshot>(),new Map<number,Snapshot>()];let code='';
for(let i=0;i<2;i++)clients[i].on('message',raw=>{const m=JSON.parse(raw.toString());if(m.type==='welcome'){ids[i]=m.id;if(!i)code=m.code;}if(m.type==='snapshot'){states[i]=m.state;seen[i].set(m.state.tick,m.state);}});
const send=(i:number,m:any)=>clients[i].send(JSON.stringify(m));
async function wait(f:()=>boolean,label:string){const start=Date.now();while(!f()){if(Date.now()-start>7000)throw Error('Timeout '+label);await sleep(10);}}
try{
 await wait(()=>clients.every(c=>c.readyState===1),'connect');send(0,{type:'create',testRoom:true,class:'ranged'});await wait(()=>!!code,'room');send(1,{type:'join',code,class:'ranged'});await wait(()=>ids.every(Boolean),'join');
 send(0,{type:'ready',ready:true});send(1,{type:'ready',ready:true});await wait(()=>states.every(s=>s?.phase==='specialization'),'specialization');send(0,{type:'spec',id:'pistol'});send(1,{type:'spec',id:'pistol'});await wait(()=>states.every(s=>s?.phase==='fight'),'fight');
 for(const id of FIRST_BLOCK_CHIPS)send(0,{type:'debug',command:{action:'chip',id}});
 await wait(()=>states.every(s=>FIRST_BLOCK_CHIPS.every(id=>s?.players[0].chips[id]===1)),'install test chips');
 send(0,{type:'input',input:{...idleInput(),seq:1,block:true}});await wait(()=>states.every(s=>(s?.players[0].blockTime||0)>0),'block window');
 const aim={...testPilot(states[1]!,ids[1],1),camera:'first',x:0,z:0,dash:false,jump:false,secondary:false,attack:true};send(1,{type:'input',input:aim});await sleep(40);send(1,{type:'input',input:{...idleInput(),seq:2,camera:'first',yaw:aim.yaw,pitch:aim.pitch}});
 await wait(()=>states.every(s=>s?.events.some(e=>e.type==='blockProc'&&e.source===ids[0])),'shared first-block event');
 const tick=[...seen[0].keys()].reverse().find(t=>seen[1].has(t)&&seen[0].get(t)!.events.some(e=>e.type==='blockPulse'))!;assert.ok(tick);
 const a=seen[0].get(tick)!,b=seen[1].get(tick)!;assert.deepEqual(a,b);const proc=a.events.filter(e=>e.type==='blockProc');assert.equal(proc.length,1);assert.deepEqual(Object.keys(proc[0].mods!).sort(),[...FIRST_BLOCK_CHIPS].sort());
 assert.equal(a.events.filter(e=>e.type==='blockPulse').length,1);assert.equal(a.events.find(e=>e.type==='blockPulse')!.mods?.supernova,1);assert.ok(a.events.some(e=>e.type==='reflect'));assert.equal(a.fields!.filter(f=>f.kind==='mine').length,3);assert.equal(a.fields!.filter(f=>f.kind==='time').length,1);assert.equal(a.minions!.length,1);assert.equal(a.players[0].hp,100);
 console.log(JSON.stringify({test:'first-block projectile network contract',sharedTick:tick,exactAgreement:true,firstBlockChips:9,supernovaPulse:true,reflected:true,mines:3,timeBubbles:1,zombies:1}));
}finally{clients.forEach(c=>c.close());}
