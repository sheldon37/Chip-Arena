/** Three real socket consumers: recorded victim playback stays separate from HostedRoom's live state. */
import {WebSocket,WebSocketServer} from 'ws';
import assert from 'node:assert/strict';
import {HostedRoom} from '../src/shared/host-room.js';
import {initPhysics,type Snapshot} from '../src/shared/simulation.js';
import {v} from '../src/shared/config.js';
import {Killcam} from '../src/client/killcam.js';
import {sirenSuperArmor} from '../src/shared/siren-motion.js';
await initPhysics();
const server=new WebSocketServer({host:'127.0.0.1',port:0}),peers:WebSocket[]=[],clients:WebSocket[]=[];
await new Promise<void>(r=>server.once('listening',r));server.on('connection',ws=>peers.push(ws));
const room=new HostedRoom('REPLAY23',{name:'Boss',class:'dev',color:'#ffffff'},true,(_target,m)=>{for(const ws of peers)if(ws.readyState===WebSocket.OPEN)ws.send(JSON.stringify(m));},6,true);
try{
 room.join({name:'Victim',class:'ranged'},false,'victim');room.join({name:'Observer',class:'ranged'},false,'observer');
 for(let i=0;i<3;i++){const ws=new WebSocket('ws://127.0.0.1:'+(server.address() as {port:number}).port);clients.push(ws);await new Promise<void>((r,j)=>{ws.once('open',r);ws.once('error',j);});}
 const s=room.sim,boss=s.players.get('host')!,victim=s.players.get('victim')!;boss.spec='siren';s.resetRound();s.phase='fight';s.time=1;
 const cams=clients.map(()=>new Killcam()),ids=['victim','host','observer'];let compared=0;
 async function broadcast(){
  const waiting=clients.map(ws=>new Promise<Snapshot>((resolve,reject)=>{const timeout=setTimeout(()=>reject(Error('snapshot timeout')),3000);ws.once('message',raw=>{clearTimeout(timeout);resolve(JSON.parse(raw.toString()).state);});}));
  room.broadcast();const frames=await Promise.all(waiting);for(let i=0;i<frames.length;i++){assert.deepEqual(frames[i],frames[0]);cams[i].observe(frames[i],ids[i],s.time*1000);}compared++;return frames[0];
 }
 assert.ok(s.siren.start(boss,'rend'));const hp=boss.hp;s.damage(boss,25,victim.id,'direct',v(1,0,0),500,boss.body.translation(),true);s.standardMelee.launch(boss,victim,8);
 let live=await broadcast();assert.equal(boss.hp,hp-25);assert.deepEqual(boss.ext,v());assert.ok(sirenSuperArmor(live.players.find(p=>p.id==='host')!,live.time));assert.equal(boss.knockdown,undefined);
 s.siren.cancel(boss);
 // Death at 4 s; playback starts after the 1.2 s live hold (5.2 s) from 2 s; the 4 s aftermath streams in during playback.
 for(let i=1;i<=84;i++){s.time=1+i/20;s.tick++;
  if(i===60)s.damage(victim,victim.hp+1,boss.id,'direct',v(0,0,1),300,victim.body.translation());
  live=await broadcast();
 }
 const before=JSON.stringify(s.snapshot()),replay=cams[0].sample(live,'victim',5200)!;
 assert.ok(replay);assert.ok(replay.time<live.time);assert.equal(replay.players.find(p=>p.id==='victim')!.hp,100);assert.equal(live.players.find(p=>p.id==='victim')!.hp,0);
 assert.equal(cams[0].info!.killerId,'host');assert.equal(cams[1].sample(live,'host',5200),undefined);assert.equal(cams[2].sample(live,'observer',5200),undefined);
 assert.equal(JSON.stringify(s.snapshot()),before,'replay never rewinds the authority');
 s.time+=.1;s.tick++;live=await broadcast();assert.equal(cams[0].active,true,'live packets continue during replay');
 const after=cams[0].sample(live,'victim',7700)!;assert.ok(after.time>4.4&&after.time<=4.6,'replay continues past the kill');assert.equal(after.players.find(p=>p.id==='victim')!.hp,0);assert.equal(cams[0].info!.end,8);cams[0].skip();assert.equal(cams[0].sample(live,'victim',5300),undefined);
 s.resetRound();s.phase='fight';s.tick++;live=await broadcast();assert.equal(cams[0].active,false);assert.equal(cams[0].bufferedFrames,1);
 console.log(JSON.stringify({clients:3,identicalSnapshots:compared,armorDamage:true,armorNoLaunch:true,victimOnlyReplay:true,authorityUnchanged:true,livePacketsDuringReplay:true,aftermath:true,skip:true,roundReset:true}));
}finally{for(const c of clients)c.terminate();for(const p of peers)p.terminate();await new Promise<void>(r=>server.close(()=>r()));room.dispose();}
