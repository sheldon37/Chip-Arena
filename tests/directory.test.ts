import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {directory} from '../src/relay/directory.js';
import {LocalRoomDatabase} from '../src/relay/local.js';
import {DirectoryLease} from '../src/server/directory-lease.js';
import {DIRECTORY_ORIGIN,DIRECTORY_TTL_MS,onlineRoomCode,formatOnlineCode} from '../src/shared/room-directory.js';
const {resolveJoin,roomCode}=createRequire(import.meta.url)('../desktop/join.cjs');
const code='ABCD2345',token='a'.repeat(64),other='b'.repeat(64),origin='https://friends.trycloudflare.com';
const request=(key=token,address=origin)=>new Request(`${DIRECTORY_ORIGIN}/api/directory/${code}`,{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+key},body:JSON.stringify({origin:address})});
const lookup=()=>new Request(`${DIRECTORY_ORIGIN}/api/directory/${code}`);
test('online codes resolve once, refresh only for their owner, expire and never reveal the host secret',async()=>{
 const db=new LocalRoomDatabase(),now=1000000;
 try{
  assert.equal((await directory(lookup(),db,now)).status,404);
  assert.equal((await directory(request(),db,now)).status,200);
  const found=await directory(lookup(),db,now+1);assert.equal(found.headers.get('Access-Control-Allow-Origin'),'*');assert.equal(found.headers.get('Cache-Control'),'no-store');assert.deepEqual(await found.json(),{code,origin,expires:now+DIRECTORY_TTL_MS});
  const saved=await db.prepare('SELECT * FROM online_rooms WHERE code=?').bind(code).first();assert.notEqual(saved.host_key,token);
  assert.equal((await directory(request(other),db,now+100)).status,409);
  assert.equal((await directory(request(token,'https://new.trycloudflare.com'),db,now+100)).status,200);
  assert.equal((await directory(lookup(),db,now+DIRECTORY_TTL_MS+101)).status,404);
  const race=await Promise.all([directory(request(other),db,now+DIRECTORY_TTL_MS+200),directory(request(token),db,now+DIRECTORY_TTL_MS+200)]);assert.deepEqual(race.map(r=>r.status).sort(),[200,409]);
 }finally{db.close();}
});
test('directory validates addresses, bounded registrations, authorization, and request origins',async()=>{
 const db=new LocalRoomDatabase();try{
  for(const value of ['http://friends.trycloudflare.com','https://evil.example','https://u:p@friends.trycloudflare.com','https://friends.trycloudflare.com/path','https://friends.trycloudflare.com/?room=bad'])assert.equal((await directory(request(token,value),db)).status,400);
  assert.equal((await directory(request('bad'),db)).status,401);
  const cross=request();cross.headers.set('Origin','https://unrelated.example');assert.equal((await directory(cross,db)).status,403);
  assert.equal((await directory(request(token,'x'.repeat(2100)),db)).status,413);
  assert.equal((await directory(new Request(`${DIRECTORY_ORIGIN}/api/directory/invalid`),db)).status,400);
 }finally{db.close();}
});
test('server lease registers in the background and native code resolves to the exact room invite',async()=>{
 const db=new LocalRoomDatabase();let registrations=0;
 const route=async(input:any,init?:any)=>{if(init?.method==='POST')registrations++;return directory(new Request(input,init),db);};
 try{
  const lease=new DirectoryLease(code,route as typeof fetch);assert.equal(lease.ready,false);
  await Promise.all([lease.refresh(origin),lease.refresh(origin),lease.refresh(origin)]);assert.equal(registrations,1);assert.equal(lease.ready,true);
  await lease.refresh(origin);assert.equal(registrations,1,'healthy listings refresh at most every 45 seconds');
  const value=await resolveJoin(formatOnlineCode(code),route);assert.equal(value,origin+'/?room='+code);
  assert.equal(await resolveJoin(' abcd-2345 ',route),value);assert.equal(onlineRoomCode(' ca-abcd-2345 '),code);assert.equal(roomCode(formatOnlineCode(code)),code);
  assert.equal(await resolveJoin('http://192.168.1.24:8788/?room='+code,()=>{throw Error('No lookup for complete invites');}),'http://192.168.1.24:8788/?room='+code);
  const offline=new DirectoryLease('EFGH6789',async()=>{throw Error('Offline');});await offline.refresh(origin);assert.equal(offline.ready,false);
 }finally{db.close();}
});
test('native code rejects expired rooms, mismatched rooms and unsafe returned addresses',async()=>{
 await assert.rejects(resolveJoin('CA-ABCD-2345',async()=>new Response('',{status:404})),/expired/);
 for(const data of [{code:'EFGH6789',origin},{code,origin:'file:///C:/secret'},{code,origin:'https://friends.trycloudflare.com.evil.example'},{code,origin:origin+'/?room=other'}])await assert.rejects(resolveJoin(code,async()=>Response.json(data)),/Invalid room lookup/);
 await assert.rejects(resolveJoin(code,async()=>{throw Error('Offline');}),/invite link/);
});
