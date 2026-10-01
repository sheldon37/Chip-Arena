/** Real loopback WebSocket peers consuming the HostedRoom broadcast contract. */
import {WebSocket,WebSocketServer} from 'ws';
import assert from 'node:assert/strict';
import {HostedRoom} from '../src/shared/host-room.js';
import {initPhysics,type Snapshot} from '../src/shared/simulation.js';
import {v,add} from '../src/shared/config.js';
import {projectileBodyImpact} from '../src/shared/body-damage.js';
await initPhysics();
const server=new WebSocketServer({host:'127.0.0.1',port:0}),peers:WebSocket[]=[],clients:WebSocket[]=[];
await new Promise<void>(r=>server.once('listening',r));
server.on('connection',ws=>peers.push(ws));
const room=new HostedRoom('BODY2345',{name:'Shooter',class:'ranged',color:'#ffffff'},false,(_target,m)=>{for(const ws of peers)if(ws.readyState===WebSocket.OPEN)ws.send(JSON.stringify(m));});
try{
  room.join({name:'Target',class:'ranged',color:'#ff6633'},false,'target');
  for(let i=0;i<3;i++){const ws=new WebSocket('ws://127.0.0.1:'+(server.address() as {port:number}).port);clients.push(ws);await new Promise<void>((r,j)=>{ws.once('open',r);ws.once('error',j);});}
  const s=room.sim,a=s.players.get('host')!,b=s.players.get('target')!;s.phase='fight';s.time=1;b.grounded=true;b.hp=1000;
  a.body.setTranslation(v(0,1,0),true);b.body.setTranslation(v(0,1,3),true);
  let compared=0;
  async function broadcast(){
    const waiting=clients.map(ws=>new Promise<Snapshot>((resolve,reject)=>{const timeout=setTimeout(()=>reject(Error('snapshot timeout')),3000);ws.once('message',raw=>{clearTimeout(timeout);resolve(JSON.parse(raw.toString()).state);});}));
    room.broadcast();const frames=await Promise.all(waiting);for(const frame of frames)assert.deepEqual(frame,frames[0]);compared++;return frames[0];
  }
  const bullet=projectileBodyImpact({spec:'rifle',vel:v(0,0,150),radius:.045}),point=add(b.body.translation(),v(0,.65,0));
  s.damage(b,87,a.id,'direct',v(0,0,1),0,point,false,false,true,[],bullet);
  let frame=await broadcast();assert.equal(frame.players.find(p=>p.id==='target')?.wounds?.[0].kind,'cavity');
  b.hp=1;s.damage(b,87,a.id,'direct',v(0,0,1),350,point,false,false,true,[],bullet);
  frame=await broadcast();const death=frame.players.find(p=>p.id==='target')!.death!;
  assert.equal(death.motion?.kind,'kneel-fall');assert.equal(death.wound?.kind,'cavity');assert.equal(death.effect,'impact-ragdoll');
  s.time+=1;frame=await broadcast();assert.equal(frame.players.find(p=>p.id==='target')!.death!.id,death.id);
  s.resetRound();frame=await broadcast();assert.ok(frame.players.every(p=>!p.death&&!p.wounds));
  console.log(JSON.stringify({clients:3,identicalHostedSnapshots:compared,liveWound:true,lethalOutcome:true,lateDeathStable:true,roundReset:true}));
}finally{for(const c of clients)c.terminate();for(const p of peers)p.terminate();await new Promise<void>(r=>server.close(()=>r()));room.dispose();}
