import {WebSocket} from 'ws';import assert from 'node:assert/strict';
import {Snapshot} from '../src/shared/simulation.js';import {testPilot} from '../src/shared/test-driver.js';import {idleInput} from '../src/shared/config.js';
const endpoint=process.env.TEST_ENDPOINT||'ws://localhost:8793/socket',sleep=(n:number)=>new Promise(r=>setTimeout(r,n));
for(let n=0;n<50;n++){try{await fetch(endpoint.replace('ws:','http:').replace('socket','health'));break;}catch{await sleep(100);}}
const clients=[new WebSocket(endpoint),new WebSocket(endpoint)],ids=['',''],states:(Snapshot|null)[]=[null,null],errors=['',''];let code='',seq=0;const snapshots=[new Map<number,Snapshot>(),new Map<number,Snapshot>()];
for(let i=0;i<2;i++)clients[i].on('message',raw=>{const m=JSON.parse(raw.toString());if(m.type==='welcome'){ids[i]=m.id;if(i===0)code=m.code;}if(m.type==='snapshot'){states[i]=m.state;snapshots[i].set(m.state.tick,m.state);}if(m.type==='error')errors[i]=m.message;});
const send=(i:number,m:any)=>clients[i].send(JSON.stringify(m));async function wait(f:()=>boolean,label:string,max=6000){const start=Date.now();while(!f()){if(Date.now()-start>max)throw Error('Timeout '+label);await sleep(15);}}
try{
 await wait(()=>clients.every(c=>c.readyState===1),'open');send(0,{type:'create',testRoom:true,class:'ranged',name:'Enchanted Pilot'});await wait(()=>!!code,'code');send(1,{type:'join',code,class:'melee',name:'Target'});await wait(()=>ids.every(Boolean),'ids');send(0,{type:'ready',ready:true});send(1,{type:'ready',ready:true});await wait(()=>states[0]?.phase==='specialization','spec');send(0,{type:'spec',id:'pistol'});send(1,{type:'spec',id:'sword'});await wait(()=>states.every(s=>s?.phase==='fight'),'fight');
 send(1,{type:'debug',command:{action:'chip',id:'poison'}});await wait(()=>!!errors[1],'reject nonowner');assert.match(errors[1],/disabled/);
 send(0,{type:'debug',command:{action:'chip',id:'ember'}});send(0,{type:'debug',command:{action:'chip',id:'poison'}});await wait(()=>states.every(s=>s?.players[0].chips.poison===1),'chips replicated');
 const aim={...testPilot(states[0]!,ids[0],++seq),camera:'first',x:0,z:0,dash:false,jump:false,secondary:false,attack:true};send(0,{type:'input',input:aim});await sleep(40);send(0,{type:'input',input:{...idleInput(),seq:++seq,camera:'first',yaw:aim.yaw,pitch:aim.pitch}});
 await wait(()=>states.every(s=>s!.players[1].statuses.length===2),'both DoTs applied');
 const tick=[...snapshots[0].keys()].reverse().find(t=>snapshots[1].has(t)&&snapshots[0].get(t)!.players[1].statuses.length===2)!;assert.ok(tick);const a=snapshots[0].get(tick)!,b=snapshots[1].get(tick)!;assert.deepEqual(a.players,b.players);const initialHp=a.players[1].hp;
 await wait(()=>states.every(s=>s!.players[1].hp<initialHp-4),'DoT health decrease');const later=[...snapshots[0].keys()].reverse().find(t=>snapshots[1].has(t))!;assert.deepEqual(snapshots[0].get(later)!.players,snapshots[1].get(later)!.players);
 const afterHp=snapshots[0].get(later)!.players[1].hp;send(0,{type:'debug',command:{action:'reset'}});await wait(()=>states.every(s=>s!.players[1].hp===100&&s!.players[1].statuses.length===0),'reset');
 console.log(JSON.stringify({test:'networked elemental shot',camera:'first',sameAuthoritativeStatuses:true,statuses:a.players[1].statuses,initialHp,afterDotHp:afterHp,resetClearsBoth:true,nonOwnerGrantRejected:true},null,2));
}finally{clients.forEach(c=>c.close());}
