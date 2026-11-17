import {test} from 'node:test';
import assert from 'node:assert/strict';
import {randomBytes} from 'node:crypto';
import {readdirSync,readFileSync} from 'node:fs';
import {LocalRoomDatabase} from '../src/relay/local.js';
import {routeOldAccounts,accountRateRequest} from '../src/relay/account-routing.js';
import worker from '../src/relay/account-worker.js';
import legacy from '../src/relay/worker.js';
import {verifyPlayerToken} from '../src/shared/account-auth.js';
import {accountOrigin} from '../src/shared/account-origin.js';

async function fixture(){
 const db=new LocalRoomDatabase();
 // Replace the legacy schema with the actual independent migrations.
 db.sql.exec('PRAGMA foreign_keys=OFF');
 for(const row of db.sql.prepare("SELECT name FROM sqlite_master WHERE type='table'").all())
  db.sql.exec('DROP TABLE "'+row.name+'"');
 db.sql.exec('PRAGMA foreign_keys=ON');
 for(const file of readdirSync('infra/accounts/migrations').sort())db.sql.exec(readFileSync('infra/accounts/migrations/'+file,'utf8'));
 const pair=await crypto.subtle.generateKey({name:'ECDSA',namedCurve:'P-256'},true,['sign','verify']);
 const publicKey=await crypto.subtle.exportKey('jwk',pair.publicKey);
 const env={DB:db,ACCOUNT_STATE:'active',ACCOUNT_ENV:'staging',DEV_SIGNING_KEY:JSON.stringify(await crypto.subtle.exportKey('jwk',pair.privateKey)),
  XP_ISSUER_SECRET:randomBytes(32).toString('hex'),ROOM_DIRECTORY_ORIGIN:'https://directory.test',STEAM_APP_ID:'1',STEAM_TICKET_IDENTITY:'isolated-test',STEAM_PUBLISHER_KEY:'test-only'};
 let counter=0;
 const call=(path:string,body?:unknown,token?:string)=>worker.fetch(new Request('https://account.test'+path,{
  method:body===undefined?'GET':'POST',headers:{'content-type':'application/json','CF-Connecting-IP':'fixture-'+(++counter),...(token?{authorization:'Bearer '+token}:{})},
  ...(body===undefined?{}:{body:JSON.stringify(body)})
 }),env);
 const api=(action:string,body?:unknown,token?:string)=>call('/api/accounts/'+action,body,token);
 const password=randomBytes(20).toString('hex');
 async function register(username:string){const r=await api('register',{username,password});assert.equal(r.status,200);return await r.json() as any;}
 return {db,env,publicKey,call,api,register,password};
}

test('maintenance fences account routes and still serves directory and transport',async()=>{
 const f=await fixture();
 try{
  assert.equal((await f.call('/healthz')).status,200);
  f.env.ACCOUNT_STATE='maintenance';
  assert.equal((await f.api('register',{})).status,503);
  assert.equal((await f.call('/internal/match-completions',{},f.env.XP_ISSUER_SECRET)).status,503);
  const oldDb=new LocalRoomDatabase();
  try{
   const env={DB:oldDb,ACCOUNT_ROUTING:'maintenance'};
   for(const path of ['/api/auth/login','/api/accounts/register','/api/accounts/social'])
    assert.equal((await legacy.fetch(new Request('https://site.test'+path,{method:'POST'}),env)).status,503);
   assert.equal((await legacy.fetch(new Request('https://site.test/api/directory/UNABCDEF'),env)).status,404);
   assert.equal((await legacy.fetch(new Request('https://site.test/api/network'),env)).status,200);
  }finally{oldDb.close();}
  f.env.ACCOUNT_STATE='active';f.env.ACCOUNT_ENV='production';
  assert.equal((await f.api('register',{})).status,503);
  assert.equal((await f.call('/healthz')).status,503);
 }finally{f.db.close();}
});

test('trusted issuer credits once, player polls their own receipt and grants require DEV',async()=>{
 const f=await fixture();
 try{
  const u=await f.register('user_one'),other=await f.register('user_two');
  const c={version:1,completionId:'one_match',mode:'pvp',won:true,bots:false,seconds:70,cleared:0};
  assert.equal((await f.api('xp',c,u.session)).status,503);
  assert.equal((await f.api('grant',{developer:true},u.session)).status,403);
  assert.equal((await f.call('/internal/match-completions',{accountId:u.account.id,completion:c},u.session)).status,403);
  assert.equal(f.db.sql.prepare('SELECT count(*) AS n FROM account_xp_awards').get()?.n,0);
  for(let i=0;i<2;i++)assert.equal((await f.call('/internal/match-completions',{accountId:u.account.id,completion:c},f.env.XP_ISSUER_SECRET)).status,200);
  const own=await (await f.api('xp',{...c,won:false},u.session)).json() as any;
  assert.equal(own.award.newTotal,150);assert.equal(own.summary.revision,1);
  assert.equal((await f.api('xp',{...c,accountId:u.account.id},other.session)).status,503);
  const grant=await f.api('profile',{revision:0,developer:true,unlocks:['all'],profile:{name:'Pilot'}},u.session);
  const data=await grant.json() as any;
  assert.equal(data.account.developer,false);
  assert.equal((await verifyPlayerToken(data.token,Date.now(),f.publicKey)).accountId,u.account.id);
 }finally{f.db.close();}
});

test('Steam link requires password, session and Valve ticket; collisions, replays and client IDs fail',async(t)=>{
 const f=await fixture(),id='76561198000000001';
 let valid=true;
 t.mock.method(globalThis,'fetch',async(input:URL)=>{
  const url=new URL(input);
  assert.equal(url.origin,'https://partner.steam-api.com');
  assert.equal(url.searchParams.get('appid'),f.env.STEAM_APP_ID);
  assert.equal(url.searchParams.get('identity'),f.env.STEAM_TICKET_IDENTITY);
  return Response.json({response:{params:{result:valid?'OK':'Invalid',steamid:id}}});
 });
 try{
  const first=await f.register('linked_user'),other=await f.register('other_user');
  const link={password:f.password,ticket:'aabb',steamid:'76561198000000002'};
  assert.equal((await f.api('steam-login',{steamid:id})).status,400);
  assert.equal((await f.api('steam-link',link)).status,401);
  assert.equal((await f.api('steam-link',{...link,password:'wrong'},first.session)).status,401);
  valid=false;assert.equal((await f.api('steam-link',link,first.session)).status,401);valid=true;
  assert.equal((await f.api('steam-link',link,first.session)).status,200);
  assert.equal((await f.api('steam-login',{ticket:'AABB'})).status,401);
  assert.equal((await f.api('steam-link',{...link,ticket:'ccee'},other.session)).status,409);
  const both=await Promise.all([f.api('steam-login',{ticket:'aacc'}),f.api('steam-login',{ticket:'aaCC'})]);
  assert.deepEqual(both.map(r=>r.status).sort(),[200,401]);
  const data=await both.find(r=>r.ok)!.json() as any;
  assert.equal(data.account.id,first.account.id);
  assert.equal((await f.api('me',undefined,data.session)).status,200);
  assert.equal(f.db.sql.prepare('SELECT steam_id FROM steam_links').get()?.steam_id,id);
  f.env.STEAM_PUBLISHER_KEY='';assert.equal((await f.api('steam-login',{ticket:'cccc'})).status,503);
 }finally{f.db.close();}
});

test('account invitations validate the existing directory without forwarding bearer sessions',async(t)=>{
 const f=await fixture();let calls=0,exists=false;
 t.mock.method(globalThis,'fetch',async(input:string,init:RequestInit)=>{
  assert.equal(String(input),'https://directory.test/api/directory/UNABCDEF');
  assert.equal(new Headers(init.headers).get('authorization'),null);assert.equal(init.redirect,'error');
  calls++;return exists?Response.json({code:'UNABCDEF',expires:Date.now()+30000}):new Response(null,{status:404});
 });
 try{
  const a=await f.register('sender'),b=await f.register('recipient');
  await f.api('social',{},b.session);
  const invite={target:b.account.id,code:'UNABCDEF'};
  assert.equal((await f.api('invite',invite,a.session)).status,409);
  exists=true;assert.equal((await f.api('invite',invite,a.session)).status,200);assert.equal(calls,2);
  const inbox=await (await f.api('social',{},b.session)).json() as any;
  const reply=await f.api('invite-answer',{id:inbox.invites[0].id,accept:true},b.session);
  assert.equal((await reply.json() as any).code,invite.code);
 }finally{f.db.close();}
});

test('compatibility route never writes to stale D1 after forwarding is enabled',async(t)=>{
 const db=new LocalRoomDatabase();
 try{
  t.mock.method(globalThis,'fetch',async(url:string,init:RequestInit)=>{
   assert.equal(url,'https://new.test/api/accounts/login');assert.equal(init.redirect,'error');throw Error('offline');
  });
  const r=await legacy.fetch(new Request('https://old.test/api/accounts/login',{method:'POST',body:'{}'}),
   {DB:db,ACCOUNT_ROUTING:'forward',ACCOUNT_UPSTREAM:'https://new.test',ACCOUNT_BRIDGE_SECRET:randomBytes(32).toString('hex')});
  assert.equal(r.status,503);assert.equal(db.sql.prepare('SELECT count(*) AS n FROM dev_login_rates').get()?.n,0);
  assert.throws(()=>accountOrigin('http://example.test'));assert.throws(()=>accountOrigin('https://user@new.test'));
  assert.equal(accountOrigin('http://127.0.0.1:8787'),'http://127.0.0.1:8787');
 }finally{db.close();}
});

test('legacy proxy authenticates the original rate-limit address across Cloudflare zones',async(t)=>{
 const secret=randomBytes(32).toString('hex'),ip='192.0.2.7';
 const url='https://old.test/api/accounts/me';
 const req=new Request(url,{headers:{'CF-Connecting-IP':ip,'X-Chip-Client-IP':'192.0.2.8','X-Chip-Forwarded-Proof':'untrusted'}});
 t.mock.method(globalThis,'fetch',async(target:string,init:RequestInit)=>{
  const headers=new Headers(init.headers);
  headers.set('CF-Connecting-IP','2a06:98c0:3600::103');
  const downstream=new Request(target,{headers});
  const verified=await accountRateRequest(downstream,secret);
  assert.equal(verified.headers.get('CF-Connecting-IP'),ip);
  assert.equal(await accountRateRequest(downstream,'x'.repeat(43)),downstream);
  headers.set('X-Chip-Client-IP','192.0.2.9');
  const tampered=new Request(target,{headers});
  assert.equal(await accountRateRequest(tampered,secret),tampered);
  return Response.json({ok:true});
 });
 const r=await routeOldAccounts(req,{ACCOUNT_ROUTING:'forward',ACCOUNT_UPSTREAM:'https://new.test',ACCOUNT_BRIDGE_SECRET:secret});
 assert.equal(r?.status,200);
});

test('practice clears route records only the signed-in pilot and survives a new session',async()=>{
 const f=await fixture();
 try{
  const user=await (await f.api('register',{username:'practice_one',password:f.password})).json();
  const other=await (await f.api('register',{username:'practice_two',password:f.password})).json();
  const clear=await f.api('boss-clear',{boss:'noctiel',accountId:other.account.id},user.session);
  assert.equal(clear.status,200);
  assert.equal((await clear.json()).bossClears,32);
  const again=await (await f.api('login',{username:'practice_one',password:f.password})).json();
  assert.equal(again.account.bossClears,32);
  assert.equal((await (await f.api('me',undefined,other.session)).json()).account.bossClears,0);
 }finally{f.db.close();}
});
