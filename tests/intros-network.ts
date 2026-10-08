import {WebSocket} from 'ws';
import assert from 'node:assert/strict';
import type {Snapshot} from '../src/shared/simulation.js';
import {introFrame} from '../src/shared/intros.js';
import {idleInput} from '../src/shared/config.js';
const endpoint=process.env.TEST_ENDPOINT||'ws://localhost:8792/socket',pause=(ms:number)=>new Promise(r=>setTimeout(r,ms));
async function until(fn:()=>boolean,label:string,timeout=15000){const at=Date.now();while(!fn()){if(Date.now()-at>timeout)throw Error('Timeout '+label);await pause(20);}}
for(let n=0;n<50;n++){try{await fetch(endpoint.replace('ws:','http:').replace('socket','health'));break;}catch{await pause(100);}}
for(const skipIntros of [false,true]){
 const clients=[new WebSocket(endpoint),new WebSocket(endpoint)],states:(Snapshot|null)[]=[null,null],seen=[new Map<number,Snapshot>(),new Map<number,Snapshot>()];let code='';
 clients.forEach((ws,i)=>ws.on('message',raw=>{const m=JSON.parse(raw.toString());if(m.type==='welcome'&&!i)code=m.code;if(m.type==='snapshot'){states[i]=m.state;seen[i].set(m.state.tick,m.state);}}));
 const send=(i:number,m:unknown)=>clients[i].send(JSON.stringify(m));
 try{
  await until(()=>clients.every(c=>c.readyState===WebSocket.OPEN),'sockets');send(0,{type:'create',name:'NEON HOST',class:'ranged',skipIntros,cosmetics:{intro:'signal-spin'}});await until(()=>!!code,'code');send(1,{type:'join',code,name:'PULSE GUEST',class:'ranged',skipIntros:!skipIntros,cosmetics:{intro:'pulse-wave'}});await until(()=>states.every(s=>s?.players.length===2),'join');
  send(0,{type:'ready',ready:true});send(1,{type:'ready',ready:true});await until(()=>states.every(s=>s?.phase==='specialization'),'spec');send(0,{type:'spec',id:'pistol'});send(1,{type:'spec',id:'pistol'});
  await until(()=>states.every(s=>s?.phase===(skipIntros?'countdown':'intro')),'presentation');assert.ok(states.every(s=>s!.options.skipIntros===skipIntros));
  if(!skipIntros){send(1,{type:'input',input:{...idleInput(),seq:1,attack:true,block:true,x:1}});await until(()=>states.every(s=>s?.phase==='countdown'),'intro completion');
   const shared=[...seen[0]].filter(([tick,s])=>s.phase==='intro'&&seen[1].has(tick));assert.ok(shared.length>100);
   const names=new Set<string>();for(const [tick,a]of shared){const b=seen[1].get(tick)!;assert.deepEqual(a,b);assert.equal(a.projectiles.length,0);assert.ok(a.players.every(p=>p.hp===100&&p.blockCd===0));const frame=introFrame(a)!;if(frame.nameVisible)names.add(frame.player.name);}assert.deepEqual([...names],['NEON HOST','PULSE GUEST']);
   console.log(JSON.stringify({intros:true,sharedIdenticalIntroSnapshots:shared.length,names:[...names],combatLocked:true}));
  }else {assert.ok([...seen[0].values()].every(s=>s.phase!=='intro'));console.log(JSON.stringify({skipIntros:true,hostSettingOnBothClients:true}));}
  await until(()=>states.every(s=>s?.phase==='fight'),'fight');
 }finally{clients.forEach(ws=>ws.close());}
}
