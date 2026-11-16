import {randomBytes,scryptSync} from 'node:crypto';
import {accounts,readBody} from './accounts.js';
import {login,type AuthEnv} from './auth.js';
import {accountMaintenance,accountRateRequest} from './account-routing.js';
import type {SteamConfig} from './account-steam.js';
import {confirmXp} from './account-xp.js';
import {accountOrigin} from '../shared/account-origin.js';
import {DEV_PUBLIC_KEY} from '../shared/dev-public-key.js';

interface Env extends AuthEnv,SteamConfig {
 ACCOUNT_BRIDGE_SECRET?:string; ACCOUNT_STATE?:string; ACCOUNT_ENV?:string; ROOM_DIRECTORY_ORIGIN?:string; XP_ISSUER_SECRET?:string;
}
const actions=new Set(['register','login','logout','recover','profile','me','players','grant','xp','social','invite','invite-answer','steam-login','steam-link']);
const json=(value:unknown,status=200)=>Response.json(value,{status,headers:{'Cache-Control':'no-store'}});

// Validate the signing pair once per isolate/config before accepting a write.
// A malformed or mismatched secret must never consume a recovery code.
let cached:{raw:string;stage:string;ready:Promise<boolean>}|undefined;
// One native KDF invocation at a time on this isolate's JS thread. The old
// Site uses an async JS KDF guarded by a shared counter, which can remain busy
// after requests are interrupted. Keep the exact public v70 encoding and cost.
async function nativePasswordHash(password:string,salt=randomBytes(16).toString('base64url')){
 const derived=scryptSync(password,Buffer.from(salt,'base64url'),32,{N:32768,r:8,p:3,maxmem:40*1024*1024});
 return `scrypt$32768$8$3$${salt}$${derived.toString('base64url')}`;
}
function keyReady(env:Env){
 const raw=env.DEV_SIGNING_KEY||'',stage=env.ACCOUNT_ENV||'';
 if(cached?.raw===raw&&cached.stage===stage)return cached.ready;
 const ready=validateKey(raw,stage);
 cached={raw,stage,ready};
 return ready;
}
async function validateKey(raw:string,stage:string){
 try{
  const key=JSON.parse(raw);
  const productionKey=key.x===DEV_PUBLIC_KEY.x&&key.y===DEV_PUBLIC_KEY.y;
  if(!key.d||key.kty!=='EC'||key.crv!=='P-256'||
      !(stage==='production'?productionKey:['development','staging'].includes(stage)&&!productionKey))return false;
  const alg={name:'ECDSA',namedCurve:'P-256'};
  const signing=await crypto.subtle.importKey('jwk',key,alg,false,['sign']);
  const verifying=await crypto.subtle.importKey('jwk',{kty:'EC',crv:'P-256',x:key.x,y:key.y},alg,false,['verify']);
  const data=new TextEncoder().encode('Chip Arena account signer readiness');
  const signature=await crypto.subtle.sign({name:'ECDSA',hash:'SHA-256'},signing,data);
  return await crypto.subtle.verify({name:'ECDSA',hash:'SHA-256'},verifying,signature,data);
 }catch{return false;}
}
export async function directoryHasRoom(origin:string|undefined,code:string,now:number){
 if(!origin)throw Error('Missing directory');
 const r=await fetch(accountOrigin(origin)+'/api/directory/'+encodeURIComponent(code),{
  redirect:'error',cache:'no-store',signal:AbortSignal.timeout(4000)
 });
 if(r.status===404)return false;
 if(!r.ok)throw Error('Directory unavailable');
 const result=await r.json() as any;
 return result.code===code&&Number.isSafeInteger(result.expires)&&result.expires>now;
}
async function issuerAuthorized(request:Request,secret?:string){
 if(!secret||secret.length<43)return false;
 const value=request.headers.get('authorization')||'';
 if(value.length>512)return false;
 const sha=(s:string)=>crypto.subtle.digest('SHA-256',new TextEncoder().encode(s));
 const a=new Uint8Array(await sha(value)),b=new Uint8Array(await sha('Bearer '+secret));
 let diff=0;for(let i=0;i<a.length;i++)diff|=a[i]^b[i];return diff===0;
}

export default {async fetch(request:Request,env:Env){
 let operation='route';
 try{
  const path=new URL(request.url).pathname;
  if(path==='/healthz'&&request.method==='GET'){
   const ready=await env.DB.prepare('SELECT COUNT(*) AS n FROM sqlite_master WHERE type=\'table\' AND name IN (\'player_accounts\',\'steam_links\',\'account_xp_awards\',\'steam_ticket_uses\',\'account_audit\',\'account_xp\',\'player_sessions\',\'player_invites\',\'player_presence\',\'dev_login_rates\')').first();
   const healthy=ready?.n===10&&await keyReady(env);
   return json({ready:healthy,state:env.ACCOUNT_STATE,environment:env.ACCOUNT_ENV},healthy?200:503);
  }
  if(env.ACCOUNT_STATE!=='active'||!await keyReady(env))return accountMaintenance();
  let result:Response;
  if(path==='/internal/match-completions'&&request.method==='POST'){
   operation='verified-result';
   if(!await issuerAuthorized(request,env.XP_ISSUER_SECRET))return json({error:'Unauthorized.'},403);
   const data=await readBody(request);
   if(typeof data.accountId!=='string'||data.accountId.length>64)return json({error:'Invalid recipient.'},400);
   result=json(await confirmXp(env.DB,data.accountId,data.completion,Date.now()));
  }else if(path==='/api/auth/login'){
   operation='legacy-login';result=await login(await accountRateRequest(request,env.ACCOUNT_BRIDGE_SECRET),env);
  }else{
   const action=path.slice('/api/accounts/'.length);
   if(!path.startsWith('/api/accounts/')||!actions.has(action))return json({error:'Unknown route.'},404);
   operation=action;
   result=await accounts(await accountRateRequest(request,env.ACCOUNT_BRIDGE_SECRET),env,Date.now(),{hashPassword:nativePasswordHash,steam:env,roomExists:(code,now)=>directoryHasRoom(env.ROOM_DIRECTORY_ORIGIN,code,now)});
  }
  if(result.status>=500)console.warn('account_service',{operation,status:result.status});
  return result;
 }catch(error){
  const status=(error as {status?:number})?.status;
  if(status===400||status===409||status===415||status===413)return json({error:'Invalid result submission.'},status);
  console.error('account_service',{operation,status:503});
  return json({error:'Account service is unavailable.'},503);
 }
}};
