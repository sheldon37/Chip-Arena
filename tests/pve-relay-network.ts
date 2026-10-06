/** PvE relay smoke (design §11 F acceptance 11). Run: node --import tsx tests/pve-relay-network.ts
 *  A PartyRoom v2 guest talks to a browser-hosted PvE HostedRoom through the local relay (createLocalRelay, like
 *  party-network.ts). The guest's hello and tunnelled {type:'chip',pve} commands arrive at the authority, identical
 *  commands with different nonces are not de-duplicated, and the relay never answers 400 (D32). An outdated guest
 *  (no hello) gets a fatal update error on ready that ends its session.
 *  Part 2 (S7): a real skipWaves room (no stubs); the guest picks, reaches the Siren's boss stage through the relay, the
 *  horde stays under the relay cap (28) and validPacket accepts every host packet. Not part of npm test. */
import assert from 'node:assert/strict';
import {PartyRoom} from '../src/client/party-room.js';
import {HostedRoom} from '../src/shared/host-room.js';
import {createLocalRelay} from '../src/relay/local.js';
import {initPhysics} from '../src/shared/simulation.js';
import {idleInput} from '../src/shared/config.js';
import {PVE_CONTRACT,type PveCommand} from '../src/shared/pve/contract.js';
import {HORDE} from '../src/shared/pve/balance.js';
import {validPacket} from '../src/shared/relay-protocol.js';
await initPhysics();
const pause=(ms:number)=>new Promise(r=>setTimeout(r,ms));
async function until(fn:()=>boolean,label:string,timeout=15000){const at=Date.now();while(!fn()){if(Date.now()-at>timeout)throw Error('Timeout '+label);await pause(20);}}
const {server,db}=createLocalRelay();await new Promise<void>(r=>server.listen(0,'127.0.0.1',r));const base='http://127.0.0.1:'+(server.address() as {port:number}).port;
const statuses:number[]=[];
const watch:typeof fetch=async(url,init)=>{const r=await fetch(url,init);statuses.push(r.status);return r;};
const pilot={name:'Host',class:'ranged' as const,color:'#ff6633'},failures:string[]=[],guestFailures:string[]=[],oldFailures:string[]=[];
let authority:HostedRoom|undefined,clock:ReturnType<typeof setInterval>|undefined;const commands:{id:string;cmd:PveCommand}[]=[];
let guestId='',guestState:any,oldId='';
const host=new PartyRoom({host:true,code:'',profile:pilot,base,fetch:watch,status:()=>{},ended:s=>failures.push(s),receive:(m,id)=>authority!.receive(id,m),guest:(p,id)=>authority!.join(p,false,id),guestGone:id=>authority!.disconnect(id)});
const guests:PartyRoom[]=[];
try{
 await host.start();
 authority=new HostedRoom(host.code,pilot,false,(target,m)=>{if(target!=='host')host.send(m,target);},6,false,true,undefined,{pve:{boss:'siren'}});
 assert.equal(authority.sim.pve!.options.relay,true);authority.sim.options.countdown=.05;
 authority.sim.pve!.economy.command=(id,cmd)=>{commands.push({id,cmd});return true;};
 clock=setInterval(()=>authority!.step(),1000/60);
 const guest=new PartyRoom({host:false,code:host.code,profile:{...pilot,name:'Guest'},base,fetch:watch,status:()=>{},ended:s=>guestFailures.push(s),guest:()=>{},guestGone:()=>{},receive:m=>{if(m.type==='welcome')guestId=m.id;if(m.type==='snapshot')guestState=m.state;}});
 guests.push(guest);await guest.start();
 const old=new PartyRoom({host:false,code:host.code,profile:{...pilot,name:'Old'},base,fetch:watch,status:()=>{},ended:s=>oldFailures.push(s),guest:()=>{},guestGone:()=>{},receive:m=>{if(m.type==='welcome')oldId=m.id;}});
 guests.push(old);await old.start();
 await until(()=>!!guestId&&!!oldId&&guestState?.pve?.v===1,'guests welcomed with pve snapshots');
 // The outdated guest never says hello: ready ends its session with the update error.
 old.send({type:'ready',ready:true});await until(()=>oldFailures.length>0,'old guest refused');assert.equal(oldFailures[0],'Update the game to join Boss Test rooms.');
 await until(()=>!authority!.sim.players.has(oldId),'old guest left the lobby',20000);
 guest.send({type:'chip',pve:{action:'hello',v:PVE_CONTRACT,key:'abcdef0123456789abcdef0123456789',n:1}});authority.receive('host',{type:'chip',pve:{action:'hello',v:PVE_CONTRACT}});
 await until(()=>authority!.sim.pve!.helloed(guestId),'guest hello arrived');
 guest.send({type:'ready',ready:true});authority.receive('host',{type:'ready',ready:true});await until(()=>authority!.sim.phase==='specialization','spec');
 guest.send({type:'spec',id:'pistol'});authority.receive('host',{type:'spec',id:'pistol'});await until(()=>guestState?.phase==='fight','fight',20000);
 // Identical commands with distinct nonces both arrive (PartyRoom de-duplicates identical pending events).
 guest.send({type:'chip',pve:{action:'open',n:2}});guest.send({type:'chip',pve:{action:'open',n:3}});guest.send({type:'chip',pve:{action:'close',n:4}});
 guest.send({type:'chip',pve:{action:'pick',pick:'pick-1',chip:'vital',n:5}});
 for(let seq=1;seq<=30;seq++){const s=guestState;guest.send({type:'inputs',epoch:`${s.setNumber}:${s.round}:${s.generation}`,inputs:[{...idleInput(),seq,x:.2,interact:seq%10===0}]});await pause(1000/60);}
 await until(()=>commands.length===4,'tunnelled commands');assert.deepEqual(commands.map(c=>[c.id,c.cmd.action,c.cmd.n]),[[guestId,'open',2],[guestId,'open',3],[guestId,'close',4],[guestId,'pick',5]]);
 await until(()=>(authority!.sim.players.get(guestId)?.ack??0)>=20,'guest inputs');
 assert.ok(!statuses.includes(400),'no relay 400');assert.deepEqual(failures,[]);assert.deepEqual(guestFailures,[]);
 assert.ok(guestState.pve&&guestState.players.some((p:any)=>p.id===guestId));
 console.log(JSON.stringify({transport:'HTTPS party relay v2',pve:true,helloArrived:true,tunnelledCommands:commands.length,relay400:false,outdatedGuestEnded:true,exchanges:statuses.length}));
}finally{if(clock)clearInterval(clock);host.close();guests.forEach(g=>g.close());await pause(100);}

// ---------- Part 2 (S7): a real skipWaves run to the boss through the relay ----------
{
 let room:HostedRoom|undefined,tick:ReturnType<typeof setInterval>|undefined,gid='',gstate:any,packets=0,invalid=0,maxAdds=0;const bad:string[]=[],gbad:string[]=[];
 const checked:typeof fetch=async(url,init)=>{
  try{const body=typeof init?.body==='string'?JSON.parse(init.body):undefined;if(body?.snapshot){packets++;if(!validPacket({seq:1,ack:0,events:[],latest:body.snapshot},true))invalid++;}for(const peer of Array.isArray(body?.peers)?body.peers:[]){packets++;if(!validPacket(peer.packet,true))invalid++;}}catch{/* not JSON */}
  const r=await fetch(url,init);statuses.push(r.status);return r;};
 const h2=new PartyRoom({host:true,code:'',profile:pilot,base,fetch:checked,status:()=>{},ended:x=>bad.push(x),receive:(m,id)=>room!.receive(id,m),guest:(p,id)=>room!.join(p,false,id),guestGone:id=>room!.disconnect(id)});
 let g2:PartyRoom|undefined;
 try{
  await h2.start();
  room=new HostedRoom(h2.code,pilot,false,(target,m)=>{if(target!=='host')h2.send(m,target);},6,false,true,undefined,{pve:{boss:'siren',skipWaves:true,debug:true}});
  assert.equal(room.sim.pve!.options.relay,true);room.sim.options.countdown=.05;tick=setInterval(()=>room!.step(),1000/60);
  g2=new PartyRoom({host:false,code:h2.code,profile:{...pilot,name:'Guest'},base,fetch:watch,status:()=>{},ended:x=>gbad.push(x),guest:()=>{},guestGone:()=>{},
  receive:m=>{if(m.type==='welcome')gid=m.id;if(m.type==='snapshot'){gstate=m.state;maxAdds=Math.max(maxAdds,m.state.pve?.adds?.length??0);}}});
  await g2.start();await until(()=>!!gid&&gstate?.pve?.v===1,'guest 2 welcomed');
  g2!.send({type:'chip',pve:{action:'hello',v:PVE_CONTRACT,key:'0123456789abcdef0123456789abcdef',n:1}});room.receive('host',{type:'chip',pve:{action:'hello',v:PVE_CONTRACT}});
  await until(()=>room!.sim.pve!.helloed(gid),'guest 2 hello');
  g2!.send({type:'ready',ready:true});room.receive('host',{type:'ready',ready:true});await until(()=>room!.sim.phase==='specialization','spec 2');
  g2!.send({type:'spec',id:'pistol'});room.receive('host',{type:'spec',id:'pistol'});await until(()=>gstate?.phase==='fight','fight 2',20000);
  room.receive('host',{type:'chip',pve:{action:'debug',op:'god:on',n:2}});
  await until(()=>!!gstate.pve.party[gid]?.picks?.[0]?.offers,'guest offers',10000);const pick=gstate.pve.party[gid].picks[0];
  g2!.send({type:'chip',pve:{action:'pick',pick:pick.id,chip:pick.offers[0],n:3}});await until(()=>!gstate.pve.party[gid].open,'guest picked',10000);
  const host=room.sim.pve!.view().party.host;if(host?.open){const k=host.picks.find(x=>x.id===host.open)!;room.receive('host',{type:'chip',pve:{action:'pick',pick:k.id,chip:k.offers![0],n:4}});}
  for(let seq=1;!(gstate.pve.stage==='boss'&&!gstate.pve.hold);seq++){if(seq>60*60)throw Error('Timeout boss stage through the relay');
   const s=gstate;g2!.send({type:'inputs',epoch:`${s.setNumber}:${s.round}:${s.generation}`,inputs:[{...idleInput(),seq}]});await pause(1000/30);}
  assert.ok(gstate.players.some((p:any)=>p.id==='pve-boss'),'guest sees the boss');
  await pause(4000);
  assert.ok(maxAdds<=HORDE.maxAliveRelay+HORDE.maxCorpses,`adds in view ${maxAdds}`);assert.ok(room.sim.pve!.horde.alive()<=HORDE.maxAliveRelay);
  assert.equal(invalid,0,`invalid host packets ${invalid}/${packets}`);assert.ok(packets>50);assert.deepEqual(bad,[]);assert.deepEqual(gbad,[]);assert.ok(!statuses.includes(400));
  console.log(JSON.stringify({part2:{guestReachedBoss:true,maxAddsInView:maxAdds,hostPackets:packets,invalidHostPackets:invalid}}));
 }finally{if(tick)clearInterval(tick);h2.close();g2?.close();await pause(100);room?.dispose();}
}
await new Promise<void>(r=>server.close(()=>r()));db.close();authority?.dispose();
