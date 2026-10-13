import {spawn} from 'node:child_process';
import {setTimeout as pause} from 'node:timers/promises';
const kinds=new Set(['maps','party','intros','first-block','chaos','rarity','motion','block','debris','elements']);
const kind=process.argv[2];
if(kind&&!kinds.has(kind))throw Error('Unknown network suite: '+kind);
const port=process.env.TEST_PORT||'8792';
if(!/^\d+$/.test(port)||+port<1024||+port>65535)throw Error('Invalid TEST_PORT');
const env={...process.env,PORT:port,ALLOW_TEST_ROOMS:'true'};
try{await fetch(`http://127.0.0.1:${port}/health`,{signal:AbortSignal.timeout(500)});throw Error('TEST_PORT is already in use.');}
catch(e){if(e.message==='TEST_PORT is already in use.')throw e;}
const server=spawn(process.execPath,['--import','tsx','src/server/index.ts'],{stdio:'inherit',env});
let client,serverExit,serverError;
server.on('error',e=>{serverError=e;});
server.on('exit',(code,signal)=>{serverExit={code,signal};client?.kill();});
const stop=()=>{client?.kill();server.kill();};
process.on('SIGINT',()=>{process.exitCode=130;stop();});
process.on('SIGTERM',()=>{process.exitCode=143;stop();});
try{
 let ready=false;
 for(let i=0;i<100;i++){
  if(serverError)throw serverError;
  if(serverExit)throw Error('Test server exited before readiness.');
  try{const r=await fetch(`http://127.0.0.1:${port}/health`,{signal:AbortSignal.timeout(500)});ready=r.ok&&(await r.json()).application==='chip-arena';}catch{}
  if(ready)break;
  await pause(100);
 }
 if(!ready)throw Error('Test server readiness timed out.');
 const file=kind?`tests/${kind}-network.ts`:'tests/network.ts';
 const result=await new Promise(resolve=>{
  client=spawn(process.execPath,['--import','tsx',file],{stdio:'inherit',env:{...process.env,TEST_ENDPOINT:`ws://localhost:${port}/socket`}});
  client.on('error',e=>{console.error(e.message);});
  client.on('close',(code,signal)=>resolve({code,signal}));
 });
 if(result.code!==0||serverExit||serverError)process.exitCode=result.code||1;
}finally{stop();}
