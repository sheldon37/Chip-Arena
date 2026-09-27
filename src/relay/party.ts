import type {Database} from './worker.js';
import {CONFIG} from '../shared/config.js';
import {RELAY,cleanProfile,normalizeRoomCode,newRoomToken,validPacket} from '../shared/relay-protocol.js';
const protocol='chip-arena-party-v2';
const fail=(error:string,status=400)=>Object.assign(Error(error),{status});
const json=(body:any,status=200)=>Response.json({...body,protocol},{status,headers:{'Cache-Control':'no-store'}});
const hash=async(s:string)=>[...new Uint8Array(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(s)))].map(n=>n.toString(16).padStart(2,'0')).join('');
export async function party(request:Request,db:Database,now=Date.now()){
 if(request.method!=='POST')throw fail('Method not allowed.',405);
 const url=new URL(request.url);if(request.headers.get('origin')&&request.headers.get('origin')!==url.origin)throw fail('Use this server to connect.',403);
 const token=request.headers.get('authorization')?.replace(/^Bearer /,'');if(!/^[a-f0-9]{64}$/.test(token||''))throw fail('Invalid session.',401);
 const reader=request.body?.getReader();if(!reader)throw fail('Missing request.');let bytes=0,text='';const decoder=new TextDecoder();while(true){const r=await reader.read();if(r.done)break;bytes+=r.value.length;if(bytes>RELAY.maxHostBytes){await reader.cancel();throw fail('Message too large.',413);}text+=decoder.decode(r.value,{stream:true});}text+=decoder.decode();let input:any;try{input=JSON.parse(text);}catch{throw fail('Invalid JSON.');}
 const code=typeof input.code==='string'?normalizeRoomCode(input.code):null;if(!code||input.protocol!==protocol)throw fail('Reload the game and use a current room code.');
 const key=await hash(token!),action=url.pathname.split('/').at(-1);let room=await db.prepare('SELECT * FROM party_rooms WHERE code=?').bind(code).first();
 if(action==='create'){
  if(room){if(room.host_key===key&&!room.closed&&room.expires>now)return json({code});throw fail('Code already in use.',409);}
  const creator=await hash(request.headers.get('CF-Connecting-IP')||'local');
  const count=await db.prepare('SELECT COUNT(*) AS n, SUM(CASE WHEN creator=? THEN 1 ELSE 0 END) AS mine FROM party_rooms WHERE expires>? AND closed=0 AND host_seen>?').bind(creator,now,now-RELAY.timeoutMs).first();if(count.n>=512||count.mine>=12)throw fail('Too many active rooms.',429);
  await db.batch([db.prepare('DELETE FROM party_seats WHERE code IN (SELECT code FROM party_rooms WHERE expires<? OR closed=1)').bind(now),db.prepare('DELETE FROM party_rooms WHERE expires<? OR closed=1').bind(now),db.prepare('INSERT INTO party_rooms(code,host_key,creator,expires,host_seen) VALUES(?,?,?,?,?)').bind(code,key,creator,now+RELAY.ttlMs,now)]);return json({code,id:'host'},201);
 }
 if(!room||room.closed||room.expires<=now)throw fail('Room closed or not found.',404);
 const host=room.host_key===key;
 if(!host&&now-room.host_seen>RELAY.timeoutMs)throw fail('The host is offline.',410);
 let seat=host?null:await db.prepare('SELECT * FROM party_seats WHERE code=? AND seat_key=?').bind(code,key).first();
 if(action==='join'){
  if(host)throw fail('The host already has a seat.',403);
  if(seat&&!seat.has_left)return json({code,id:seat.id});
  if(seat?.has_left)throw fail('This session ended. Join again from the menu.',410);
  const id='p'+newRoomToken().slice(0,16);
  // One conditional INSERT serializes competing joins; a ninth connection never consumes a seat.
  const r=await db.prepare(`INSERT INTO party_seats(id,code,seat_key,profile,seen) SELECT ?,?,?,?,? WHERE EXISTS(SELECT 1 FROM party_rooms WHERE code=? AND phase='lobby' AND closed=0) AND (SELECT COUNT(*) FROM party_seats WHERE code=? AND has_left=0)<?`).bind(id,code,key,JSON.stringify(cleanProfile(input.profile)),now,code,code,CONFIG.maxPlayers-1).run();
  if(!r.meta.changes)throw fail('Room is full or the match has started.',409);return json({code,id});
 }
 if(!host&&(!seat||seat.has_left))throw fail('Seat is not authorized.',403);
 if(action==='leave'){await db.prepare(host?'UPDATE party_rooms SET closed=1 WHERE code=?':'UPDATE party_seats SET has_left=1 WHERE id=?').bind(host?code:seat.id).run();return json({ok:true});}
 if(action!=='exchange')throw fail('Unknown operation.',404);
 if(host){
  if(!Number.isSafeInteger(input.seq)||input.seq<1||!Array.isArray(input.peers)||input.peers.length>7)throw fail('Invalid host packet.');
  if(input.snapshot&&!validPacket({seq:1,ack:0,events:[],latest:input.snapshot},true))throw fail('Invalid snapshot.');
  for(const peer of input.peers)if(typeof peer.id!=='string'||!validPacket(peer.packet,true)||peer.packet.latest)throw fail('Invalid peer packet.');
  const statements=[db.prepare('UPDATE party_rooms SET host_seen=?,host_seq=?,snapshot=COALESCE(?,snapshot),phase=COALESCE(?,phase) WHERE code=? AND host_key=? AND host_seq<=?').bind(now,input.seq,input.snapshot?JSON.stringify(input.snapshot):null,input.snapshot?.state?.phase||null,code,key,input.seq)];
  for(const peer of input.peers)statements.push(db.prepare('UPDATE party_seats SET host_packet=? WHERE id=? AND code=? AND EXISTS(SELECT 1 FROM party_rooms WHERE code=? AND host_seq=?)').bind(JSON.stringify(peer.packet),peer.id,code,code,input.seq));
  statements.push(db.prepare('UPDATE party_seats SET has_left=1 WHERE code=? AND seen<?').bind(code,now-RELAY.timeoutMs));statements.push(db.prepare('SELECT * FROM party_seats WHERE code=?').bind(code));const rows=(await db.batch(statements)).at(-1).results;
  return json({serverNow:now,peers:rows.map((p:any)=>({id:p.id,profile:JSON.parse(p.profile),left:!!p.has_left,seen:p.seen,packet:p.guest_packet?JSON.parse(p.guest_packet):null}))});
 }
 if(bytes>RELAY.maxGuestBytes||!validPacket(input.packet,false))throw fail('Invalid guest packet.');
 const result=await db.batch([db.prepare('UPDATE party_seats SET guest_packet=?,guest_seq=?,seen=? WHERE id=? AND guest_seq<=?').bind(JSON.stringify(input.packet),input.packet.seq,now,seat.id,input.packet.seq),db.prepare('SELECT host_packet FROM party_seats WHERE id=?').bind(seat.id),db.prepare('SELECT snapshot,host_seen,closed FROM party_rooms WHERE code=?').bind(code)]);room=result[2].results[0];if(room.closed)throw fail('Host closed the room.',410);
 return json({serverNow:now,packet:result[1].results[0].host_packet?JSON.parse(result[1].results[0].host_packet):null,snapshot:room.snapshot?JSON.parse(room.snapshot):null});
}
