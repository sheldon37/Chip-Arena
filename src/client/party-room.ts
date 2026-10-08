import {RELAY,newRoomCode,newRoomToken,normalizeRoomCode,RelayEvent,RelayPacket} from '../shared/relay-protocol.js';
import {NETWORK} from '../shared/network-config.js';
type Peer={events:RelayEvent[];eventId:number;received:number;packetSeq:number;gone:boolean};
type Options={host:boolean;code:string;profile:any;receive:(message:any,id:string)=>void;guest:(profile:any,id:string)=>void;guestGone:(id:string)=>void;status:(label:string)=>void;ended:(reason:string)=>void;base?:string;fetch?:typeof fetch};
const peer=():Peer=>({events:[],eventId:0,received:0,packetSeq:0,gone:false});
/** One shared snapshot plus separate acknowledged input/event channels for each guest. */
export class PartyRoom {
 readonly code:string;private token=newRoomToken();id='host';closed=false;private peers=new Map<string,Peer>();private own=peer();private seq=0;private latest:any;private inputs:any;private timer:ReturnType<typeof setTimeout>|undefined;private controller:AbortController|undefined;private lastSuccess=performance.now();private started=false;private lastLabel='';private welcomed=false;private lastSnapshot=-1;
 constructor(private o:Options){this.code=o.host?newRoomCode():normalizeRoomCode(o.code)||'';if(!this.code)throw Error('Enter the 8-character room code.');}
 private label(s:string){if(s!==this.lastLabel){this.lastLabel=s;this.o.status(s);}}
 private async request(action:string,data:any,keepalive=false){const controller=new AbortController();if(!keepalive)this.controller=controller;const timer=setTimeout(()=>controller.abort(),RELAY.requestTimeoutMs);try{const r=await (this.o.fetch||fetch)((this.o.base||'')+'/api/rooms/v2/'+action,{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+this.token},body:JSON.stringify({protocol:'chip-arena-party-v2',code:this.code,...data}),signal:controller.signal,keepalive,credentials:'omit',cache:'no-store'});const b=await r.json();if(!r.ok)throw Object.assign(Error(b.error||'Room request failed.'),{status:r.status});return b;}finally{clearTimeout(timer);if(this.controller===controller)this.controller=undefined;}}
 async start(){if(this.started)return;this.started=true;this.label(this.o.host?'CREATING ROOM':'JOINING ROOM');try{const r=await this.request(this.o.host?'create':'join',{profile:this.o.profile});if(this.closed)return;this.id=r.id||'host';this.lastSuccess=performance.now();void this.pump();}catch(e:any){this.fail(e.message);}}
 send(message:any,target='all'){
  if(this.closed)return;if(message.type==='snapshot'){this.latest=message;return;}
  if(message.type==='inputs'){const previous=this.inputs?.epoch===message.epoch?this.inputs.inputs:[];this.inputs={...message,inputs:[...previous,...message.inputs].slice(-NETWORK.maxInputBatch)};return;}
  if(message.type==='input'){this.inputs=message;return;}
  const targets=this.o.host?[...this.peers].filter(([id,p])=>!p.gone&&(target==='all'||target===id)).map(([,p])=>p):[this.own];
  for(const p of targets){if(p.events.some(e=>JSON.stringify(e.message)===JSON.stringify(message)))continue;if(p.events.length>=RELAY.maxEvents){this.fail('Connection too slow. Rejoin the room.');return;}p.events.push({id:++p.eventId,message});}
 }
 private apply(p:Peer,packet:RelayPacket|null,id:string){if(!packet||packet.seq<=p.packetSeq)return;p.packetSeq=packet.seq;p.events=p.events.filter(e=>e.id>packet.ack);for(const e of packet.events){if(e.id<=p.received)continue;p.received=e.id;if(e.message.type==='error'&&e.message.fatal){this.fail(e.message.message);return;}if(e.message.type==='welcome')this.welcomed=true;this.o.receive(e.message,id);}if(packet.latest)this.o.receive(packet.latest,id);}
 private async pump(){if(this.closed)return;const started=performance.now(),seq=++this.seq;
 const packet=(p:Peer):RelayPacket=>({seq,ack:p.received,events:p.events.slice()});
 const payload=this.o.host?{seq,snapshot:this.latest,peers:[...this.peers].filter(([,p])=>!p.gone).map(([id,p])=>({id,packet:packet(p)}))}:{packet:{...packet(this.own),...(this.inputs?{latest:this.inputs}:{})}};
 while(!this.closed){try{const r=await this.request('exchange',payload);if(this.closed)return;this.lastSuccess=performance.now();
  if(this.o.host){for(const row of r.peers){let p=this.peers.get(row.id);if(!p){p=peer();this.peers.set(row.id,p);if(!row.left&&r.serverNow-row.seen<=RELAY.timeoutMs)this.o.guest(row.profile,row.id);else p.gone=true;}if(!p.gone&&(row.left||r.serverNow-row.seen>RELAY.timeoutMs)){p.gone=true;this.o.guestGone(row.id);}if(!p.gone)this.apply(p,row.packet,row.id);}}
  else{this.apply(this.own,r.packet,'host');if(this.closed)return;const state=r.snapshot?.state;if(state&&state.tick>this.lastSnapshot){this.lastSnapshot=state.tick;if(this.inputs?.type==='inputs'){const ack=state.players.find((p:any)=>p.id===this.id)?.ack||0;this.inputs={...this.inputs,inputs:this.inputs.inputs.filter((i:any)=>i.seq>ack)};if(!this.inputs.inputs.length)this.inputs=undefined;}this.o.receive(r.snapshot,'host');}}
  this.label(this.o.host?`HOST · ${1+[...this.peers.values()].filter(p=>!p.gone).length}/8 PLAYERS`:this.welcomed?'GUEST · HTTPS CONNECTED':'GUEST · SYNCING');break;
 }catch(e:any){if(this.closed)return;if([400,401,403,404,409,410,413,415].includes(e.status)||performance.now()-this.lastSuccess>RELAY.timeoutMs){this.fail(e.message);return;}this.label('HTTPS · RECONNECTING');await new Promise(r=>setTimeout(r,300));}}
 if(!this.closed)this.timer=setTimeout(()=>void this.pump(),Math.max(10,RELAY.intervalMs-(performance.now()-started)));
 }
 private fail(reason:string){if(this.closed)return;this.close();this.label('DISCONNECTED');this.o.ended(reason);}
 close(){if(this.closed)return;this.closed=true;this.controller?.abort();if(this.timer)clearTimeout(this.timer);void this.request('leave',{},true).catch(()=>{});}
}
