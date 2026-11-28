import type {Database} from './worker.js';
const fail=(message:string,status=400)=>Object.assign(Error(message),{status});
const ONLINE_MS=60000,INVITE_MS=120000;
/** Authenticated identity comes only from accounts(), never the submitted body. */
export async function accountSocial(db:Database,who:string,action:string,input:any,now:number){
 if(action==='social'){
  await db.batch([
   db.prepare('INSERT INTO player_presence(account_id,seen) VALUES(?,?) ON CONFLICT(account_id) DO UPDATE SET seen=excluded.seen').bind(who,now),
   db.prepare('DELETE FROM player_invites WHERE id IN (SELECT id FROM player_invites WHERE expires<=? LIMIT 100)').bind(now),
   db.prepare('DELETE FROM player_presence WHERE account_id IN (SELECT account_id FROM player_presence WHERE seen<=? LIMIT 100)').bind(now-ONLINE_MS)
  ]);
  const rows=await db.batch([
   db.prepare('SELECT a.id,a.username FROM player_presence p JOIN player_accounts a ON a.id=p.account_id WHERE p.seen>? AND a.id<>? ORDER BY a.username LIMIT 100').bind(now-ONLINE_MS,who),
   db.prepare("SELECT i.id,a.username,i.expires FROM player_invites i JOIN player_accounts a ON a.id=i.sender WHERE i.recipient=? AND i.state='pending' AND i.expires>? ORDER BY i.created DESC LIMIT 20").bind(who,now)
  ]);
  return {players:rows[0].results,invites:rows[1].results};
 }
 if(action==='invite'){
  if(typeof input.target!=='string'||input.target===who||input.target.length>64||typeof input.code!=='string'||!/^([A-HJ-NP-Z2-9]{6}|[A-HJ-NP-Z2-9]{8})$/.test(input.code))throw fail('Choose an online player and an open online room.');
  const target=await db.prepare('SELECT account_id FROM player_presence WHERE account_id=? AND seen>?').bind(input.target,now-ONLINE_MS).first();
  if(!target)throw fail('That player is no longer online.',409);
  // Codes resolve only through the existing trusted room directory; no arbitrary endpoint or credential is accepted.
  const room=await db.prepare('SELECT code FROM online_rooms WHERE code=? AND expires>?').bind(input.code,now).first();
  if(!room)throw fail('The room is no longer available.',409);
  const id=crypto.randomUUID();
  const result=await db.prepare(`INSERT INTO player_invites(id,sender,recipient,code,created,expires)
   SELECT ?,?,?,?,?,? WHERE NOT EXISTS(SELECT 1 FROM player_invites WHERE sender=? AND created>?)
   AND NOT EXISTS(SELECT 1 FROM player_invites WHERE sender=? AND recipient=? AND state='pending' AND expires>?)
   AND (SELECT COUNT(*) FROM player_invites WHERE recipient=? AND state='pending' AND expires>?)<20`).bind(id,who,input.target,input.code,now,now+INVITE_MS,who,now-5000,who,input.target,now,input.target,now).run();
  if(!result.meta.changes)throw fail('An invite is already pending, or invites are arriving too quickly.',429);
  return {ok:true};
 }
 if(action==='invite-answer'){
  if(typeof input.id!=='string'||input.id.length>64||typeof input.accept!=='boolean')throw fail('Invalid invitation.');
  const result=await db.batch([
   db.prepare("UPDATE player_invites SET state=? WHERE id=? AND recipient=? AND state='pending' AND expires>?").bind(input.accept?'accepted':'declined',input.id,who,now),
   db.prepare('SELECT code FROM player_invites WHERE id=? AND recipient=?').bind(input.id,who)
  ]);
  if(!result[0].meta.changes)throw fail('This invite expired or has already been answered.',409);
  return input.accept?{ok:true,code:result[1].results[0].code}:{ok:true};
 }
 throw fail('Unknown social action.',404);
}
