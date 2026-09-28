import type {Database} from './worker.js';
import {normalizeRoomCode} from '../shared/relay-protocol.js';
import {publicInviteOrigin} from '../shared/invite-url.js';
import {DIRECTORY_TTL_MS} from '../shared/room-directory.js';
const json=(body:unknown,status=200)=>Response.json(body,{status,headers:{'Cache-Control':'no-store','Access-Control-Allow-Origin':'*','X-Content-Type-Options':'nosniff'}});
const hash=async(s:string)=>[...new Uint8Array(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(s)))].map(n=>n.toString(16).padStart(2,'0')).join('');
/** Only address discovery lives here. No inputs, snapshots or gameplay use this service. */
export async function directory(request:Request,db:Database,now=Date.now()){
 if(!db)return json({error:'Room lookup is temporarily unavailable. Use an invite link.'},503);
 const url=new URL(request.url),raw=url.pathname.slice('/api/directory/'.length),code=normalizeRoomCode(raw);
 if(!code)return json({error:'Enter the complete room code from your host.'},400);
 if(request.method==='GET'){
  const row=await db.prepare('SELECT origin,expires FROM online_rooms WHERE code=? AND expires>?').bind(code,now).first();
  if(!row)return json({error:'Room code expired or was not found. Ask the host to keep their room open and copy its code.'},404);
  return json({code,origin:row.origin,expires:row.expires});
 }
 if(request.method!=='POST')return json({error:'Method not allowed.'},405);
 if(request.headers.get('origin')&&request.headers.get('origin')!==url.origin)return json({error:'Room registration must come from the host server.'},403);
 const token=request.headers.get('authorization')?.replace(/^Bearer /,'');
 if(!/^[a-f0-9]{64}$/.test(token||''))return json({error:'Invalid host session.'},401);
 if(!request.headers.get('content-type')?.includes('application/json'))return json({error:'Expected JSON.'},415);
 const reader=request.body?.getReader();if(!reader)return json({error:'Missing registration.'},400);
 const decoder=new TextDecoder();let body='',bytes=0;while(true){const r=await reader.read();if(r.done)break;bytes+=r.value.length;if(bytes>2048){await reader.cancel();return json({error:'Registration too large.'},413);}body+=decoder.decode(r.value,{stream:true});}body+=decoder.decode();
 let input;try{input=JSON.parse(body);}catch{return json({error:'Invalid JSON.'},400);}
 const origin=publicInviteOrigin(input?.origin);if(!origin)return json({error:'Invalid public game address.'},400);
 const key=await hash(token!),creator=await hash(request.headers.get('CF-Connecting-IP')||'local-preview');
 const row=await db.prepare('SELECT host_key,expires FROM online_rooms WHERE code=?').bind(code).first();
 if(row&&row.expires>now&&row.host_key!==key)return json({error:'Room code is already in use. Create another room.'},409);
 if(!row||row.expires<=now){
  await db.prepare('DELETE FROM online_rooms WHERE code IN (SELECT code FROM online_rooms WHERE expires<=? LIMIT 100)').bind(now).run();
  const count=await db.prepare('SELECT COUNT(*) AS n, SUM(CASE WHEN creator=? THEN 1 ELSE 0 END) AS mine FROM online_rooms WHERE expires>?').bind(creator,now).first();
  if(count.n>=2048||count.mine>=12)return json({error:'Too many active rooms. Use an invite link or close an old room.'},429);
 }
 const expires=now+DIRECTORY_TTL_MS;
 const result=await db.prepare('INSERT INTO online_rooms(code,host_key,creator,origin,expires) VALUES(?,?,?,?,?) ON CONFLICT(code) DO UPDATE SET origin=excluded.origin,expires=excluded.expires,host_key=excluded.host_key,creator=excluded.creator WHERE online_rooms.host_key=excluded.host_key OR online_rooms.expires<=?').bind(code,key,creator,origin,expires,now).run();
 if(!result.meta.changes)return json({error:'Room code is already in use. Create another room.'},409);
 return json({code,expires});
}
