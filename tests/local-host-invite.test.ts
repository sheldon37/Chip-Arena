import {test} from 'node:test';
import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {once} from 'node:events';
import http from 'node:http';
import net from 'node:net';
import type {Duplex} from 'node:stream';
import {createRequire} from 'node:module';
import {WebSocket} from 'ws';
import {publicInviteOrigin,roomInviteUrl} from '../src/shared/invite-url.js';
const {freePort}=createRequire(import.meta.url)('../desktop/policy.cjs');
const pause=(ms:number)=>new Promise(r=>setTimeout(r,ms));
async function until(check:()=>boolean|Promise<boolean>,label:string){const started=Date.now();while(!await check()){if(Date.now()-started>10000)throw Error('Timed out: '+label);await pause(20);}}

test('public room invites preserve the code without exposing a localhost address',()=>{
 assert.equal(roomInviteUrl('http://127.0.0.1:47001/?old=1#menu','ABCD2345','https://friends.trycloudflare.com'),'https://friends.trycloudflare.com/?room=ABCD2345');
 assert.equal(roomInviteUrl('https://game.example/path?old=1#menu','ABCD2345'),'https://game.example/path?room=ABCD2345');
 for(const url of ['javascript:alert(1)','https://u:p@friends.trycloudflare.com','https://evil.example','http://friends.trycloudflare.com','https://friends.trycloudflare.com/path','https://friends.trycloudflare.com/?wrong=1'])assert.equal(publicInviteOrigin(url),null);
});

test('real local server and proxied guest share a room; only launcher IPC can set the public invite',async()=>{
 const port=await freePort(),local='http://127.0.0.1:'+port;
 const server=spawn(process.execPath,['--import','tsx','src/server/index.ts'],{env:{...process.env,PORT:String(port),ALLOWED_ORIGINS:''},stdio:['ignore','pipe','pipe','ipc']});
 let logs='',announced='';server.stdout!.on('data',b=>logs+=b);server.stderr!.on('data',b=>logs+=b);server.on('message',(m:any)=>{if(m.type==='public-invite-ready')announced=m.url;});
 const sockets=new Set<Duplex>();const clients:WebSocket[]=[];
 const proxy=http.createServer((req,res)=>{const upstream=http.request(new URL(req.url||'/',local),{method:req.method,headers:req.headers},response=>{res.writeHead(response.statusCode||502,response.headers);response.pipe(res);});upstream.on('error',()=>{res.writeHead(502);res.end();});req.pipe(upstream);});
 proxy.on('upgrade',(req,socket,head)=>{const upstream=net.connect(port,'127.0.0.1',()=>{upstream.write(`${req.method} ${req.url} HTTP/1.1\r\n${Object.entries(req.headers).map(([k,v])=>k+': '+v).join('\r\n')}\r\n\r\n`);if(head.length)upstream.write(head);socket.pipe(upstream).pipe(socket);});sockets.add(socket);sockets.add(upstream);socket.on('error',()=>upstream.destroy());upstream.on('error',()=>socket.destroy());});
 try{
  await until(async()=>{try{return (await fetch(local+'/health')).ok;}catch{return false;}},'server health '+logs);
  assert.equal((await(await fetch(local+'/api/network')).json()).inviteOrigin,null);
  server.send({type:'public-invite',url:'https://not-a-tunnel.example'});await pause(30);assert.equal(announced,'');
  server.send({type:'public-invite',url:'https://friends.trycloudflare.com'});
  await until(()=>!!announced,'public invite acknowledged');
  const routing=await(await fetch(local+'/api/network')).json();assert.equal(routing.inviteOrigin,'https://friends.trycloudflare.com');assert.equal(routing.endpoint,'/socket');
  // The public HTTP interface must not accept the privileged launcher message.
  await fetch(local+'/api/network',{method:'POST',body:JSON.stringify({type:'public-invite',url:'https://evil.trycloudflare.com'})});
  assert.equal((await(await fetch(local+'/api/network')).json()).inviteOrigin,routing.inviteOrigin);
  await new Promise<void>(r=>proxy.listen(0,'127.0.0.1',r));const forwarded='http://127.0.0.1:'+(proxy.address() as net.AddressInfo).port;
  assert.equal((await(await fetch(forwarded+'/health')).json()).application,'chip-arena');
  const host=new WebSocket(local.replace('http:','ws:')+'/socket',{origin:local}),guest=new WebSocket(forwarded.replace('http:','ws:')+'/socket',{origin:routing.inviteOrigin});clients.push(host,guest);
  let code='';const states:any[]=[],welcomes:string[]=[],errors:string[]=[];
  clients.forEach((ws,i)=>ws.on('message',raw=>{const message=JSON.parse(raw.toString());if(message.type==='welcome'){welcomes[i]=message.id;code=message.code;}if(message.type==='snapshot')states[i]=message.state;if(message.type==='error')errors.push(message.message);}));
  await until(()=>clients.every(ws=>ws.readyState===WebSocket.OPEN),'local host and proxy guest connect');
  host.send(JSON.stringify({type:'create',name:'Local host',skipIntros:true}));await until(()=>!!code,'host creates room');
  const invite=roomInviteUrl(local,code,routing.inviteOrigin);assert.equal(new URL(invite).origin,routing.inviteOrigin);
  guest.send(JSON.stringify({type:'join',code:new URL(invite).searchParams.get('room'),name:'Tunnel guest'}));
  await until(()=>states.length===2&&states.every(s=>s?.players.length===2),'both clients see both players');
  assert.equal(new Set(welcomes).size,2);assert.deepEqual(states[0].players.map((p:any)=>p.id).sort(),states[1].players.map((p:any)=>p.id).sort());assert.deepEqual(errors,[]);
 }finally{clients.forEach(ws=>ws.terminate());sockets.forEach(s=>s.destroy());proxy.close();const ended=once(server,'exit');server.kill();await ended;}
});
