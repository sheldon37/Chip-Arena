import {test} from 'node:test';
import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {once} from 'node:events';
import {createHash} from 'node:crypto';
import {createRequire} from 'node:module';
import {WebSocket} from 'ws';
import {testPilot} from '../src/shared/test-driver.js';
const {freePort}=createRequire(import.meta.url)('../desktop/policy.cjs');
const pause=(ms:number)=>new Promise(r=>setTimeout(r,ms));
async function until(check:()=>boolean,label:string){const start=Date.now();while(!check()){if(Date.now()-start>14000)throw Error('Timeout '+label);await pause(15);}}

test('eight fighting clients negotiate compression, receive identical states and keep ping responsive',async()=>{
 const port=await freePort(),base='http://127.0.0.1:'+port,server=spawn(process.execPath,['--import','tsx','src/server/index.ts'],{env:{...process.env,PORT:String(port),ALLOWED_ORIGINS:''},stdio:['ignore','pipe','pipe']});let output='';server.stdout!.on('data',b=>output+=b);server.stderr!.on('data',b=>output+=b);
 const clients:WebSocket[]=[],states:any[]=[],ids:string[]=[],rtts=Array.from({length:8},()=>[] as number[]),hashes=Array.from({length:8},()=>new Map<number,string>()),diagnostics:any[]=[],errors:string[]=[];let code='';let driver:ReturnType<typeof setInterval>|undefined,pings:ReturnType<typeof setInterval>|undefined;
 try{
  for(let n=0;n<100;n++){try{if((await fetch(base+'/health')).ok)break;}catch{}await pause(50);}
  for(let i=0;i<8;i++){
   const ws=new WebSocket(base.replace('http:','ws:')+'/socket',{perMessageDeflate:i!==7});clients.push(ws);
   ws.on('error',e=>errors.push(e.message));ws.on('message',raw=>{const m=JSON.parse(raw.toString());if(m.type==='welcome'){ids[i]=m.id;code=m.code;}if(m.type==='error')errors.push(m.message);if(m.type==='snapshot'){states[i]=m.state;hashes[i].set(m.state.tick,createHash('sha256').update(raw.toString()).digest('hex'));}if(m.type==='pong'){rtts[i].push(performance.now()-m.at);diagnostics[i]=m.network;}});
  }
  await until(()=>clients.every(c=>c.readyState===WebSocket.OPEN),'connect '+output);
  assert.ok(clients.slice(0,7).every(c=>c.extensions.includes('permessage-deflate')));assert.equal(clients[7].extensions,'');
  clients[0].send(JSON.stringify({type:'create',name:'Host',skipIntros:true}));await until(()=>!!code,'create');
  for(let i=1;i<8;i++)clients[i].send(JSON.stringify({type:'join',code,name:'Guest '+i,class:i%2?'magic':'ranged'}));
  await until(()=>states.length===8&&states.every(s=>s?.players.length===8),'join eight');clients.forEach(c=>c.send(JSON.stringify({type:'ready',ready:true})));
  await until(()=>states.every(s=>s.phase==='specialization'),'specialize');clients.forEach((c,i)=>c.send(JSON.stringify({type:'spec',id:i%2?'fire':'pistol'})));
  await until(()=>states.every(s=>s.phase==='fight'),'combat');
  // Lobby joins can broadcast different rosters at the same tick; compare combat only.
  hashes.forEach(h=>h.clear());
  const received=clients.map(c=>(c as any)._socket.bytesRead),began=performance.now();let seq=0;
  driver=setInterval(()=>{seq++;clients.forEach((c,i)=>{const s=states[i];if(s.phase==='fight')c.send(JSON.stringify({type:'inputs',epoch:`${s.setNumber}:${s.round}:${s.generation||0}`,inputs:[testPilot(s,ids[i],seq)]}));});},1000/60);
  pings=setInterval(()=>clients.forEach(c=>c.send(JSON.stringify({type:'ping',at:performance.now()}))),200);
  await pause(4000);clearInterval(driver);clearInterval(pings);driver=pings=undefined;
  const seconds=(performance.now()-began)/1000,wire=clients.map((c,i)=>(c as any)._socket.bytesRead-received[i]);
  const shared=[...hashes[0].keys()].filter(t=>hashes.every(h=>h.has(t)));assert.ok(shared.length>30);for(const tick of shared)assert.equal(new Set(hashes.map(h=>h.get(tick))).size,1);
  const ratio=wire[0]/wire[7];assert.ok(ratio<.4,'compression must materially reduce actual received TCP bytes');assert.ok(rtts.every(samples=>samples.length>=10));assert.deepEqual(errors,[]);
  assert.equal(diagnostics[0].compression,true);assert.equal(diagnostics[7].compression,false);
  const allRTT=rtts.flat().sort((a,b)=>a-b);assert.ok(allRTT[Math.floor(allRTT.length*.95)]<500,'local ping should not build a sustained queue');
  console.log(JSON.stringify({test:'eight-player combat transport',seconds,compressionReductionPercent:Math.round((1-ratio)*100),compressedMbps:wire[0]*8/seconds/1e6,uncompressedMbps:wire[7]*8/seconds/1e6,pingMedianMs:allRTT[Math.floor(allRTT.length*.5)],pingP95Ms:allRTT[Math.floor(allRTT.length*.95)],identicalSnapshots:shared.length,server:diagnostics[0]}));
 }finally{if(driver)clearInterval(driver);if(pings)clearInterval(pings);clients.forEach(c=>c.terminate());const exited=once(server,'exit');server.kill();await exited;}
});
