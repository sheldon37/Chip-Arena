import {scryptAsync} from '@noble/hashes/scrypt.js';
import {base64url} from '../shared/dev-auth.js';
import {GAME_CLAIM_MS} from '../shared/account-auth.js';
import {cleanGrants,cleanFlags,Access} from '../shared/access.js';
import {cosmetics} from '../shared/cosmetics.js';
import type {AuthEnv} from './auth.js';
const encoder=new TextEncoder(),SESSION_MS=12*3600000,REMEMBER_MS=30*24*3600000;
const bytes=(s:string)=>Uint8Array.from(atob(s.replace(/-/g,'+').replace(/_/g,'/')),c=>c.charCodeAt(0));
const random=()=>base64url(crypto.getRandomValues(new Uint8Array(32)));
const digest=async(s:string)=>base64url(await crypto.subtle.digest('SHA-256',encoder.encode(s)));
const equal=(a:Uint8Array,b:Uint8Array)=>{let d=a.length^b.length;for(let i=0;i<a.length;i++)d|=a[i]^(b[i]||0);return d===0;};
const fail=(message:string,status=400)=>Object.assign(Error(message),{status});
let hashes=0;
/** OWASP's 32 MiB scrypt configuration. Bound concurrent work in each Worker isolate. */
export async function passwordHash(password:string,salt=base64url(crypto.getRandomValues(new Uint8Array(16)))){
 if(hashes>=2)throw fail('Login is busy. Try again in a moment.',503);hashes++;
 try{return `scrypt$32768$8$3$${salt}$${base64url(await scryptAsync(password,bytes(salt),{N:32768,r:8,p:3,dkLen:32,maxmem:40*1024*1024,asyncTick:10}))}`;}finally{hashes--;}
}
async function passwordMatches(password:string,stored:string){const p=stored.split('$');if(p.length!==6||p.slice(0,4).join('$')!=='scrypt$32768$8$3')return false;const computed=await passwordHash(password,p[4]);return equal(bytes(computed.split('$')[5]),bytes(p[5]));}
async function legacyMatches(username:string,password:string,env:AuthEnv){if(!env.DEV_ACCOUNT_VERIFIER)return false;const a=JSON.parse(env.DEV_ACCOUNT_VERIFIER),key=await crypto.subtle.importKey('raw',encoder.encode(password),'PBKDF2',false,['deriveBits']);const actual=new Uint8Array(await crypto.subtle.deriveBits({name:'PBKDF2',hash:'SHA-256',salt:bytes(a.salt),iterations:a.iterations},key,256));return username===a.username.toLowerCase()&&equal(actual,bytes(a.hash));}
function access(row:any):Access{return {accountId:row.id,developer:row.developer===1,unlocks:cleanGrants(JSON.parse(row.unlocks)),flags:cleanFlags(JSON.parse(row.flags))};}
// Native profile ids are stored by the shared service without adding them to Electron's rendering catalog.
const NATIVE_INTROS=new Set(['orbital-drop','grid-rez','holo-projection','blackout','skyline-zip','hero-landing','overclock']);
const LOBBY_BACKS=new Set(['tick-ring','clean-glow','sunburst','hex-field','circuit','equalizer','warp','embers','orbitals','glitch','crown-halo','neon-diamond']);
// Unity weapon skins (unity/.../Core/NativeWeaponSkins.cs Normalize): "spec:skin" picks, comma separated, strictly allowlisted;
// only non-default picks are kept, once per entry (the last wins), in entry order: the weapons, then the pilot's body (Core/
// NativeBodySkins.cs: standard, prototype, or the DEV-only "sheldon" Sheldon Clothes, kept only on DEV accounts like the
// Sheldon Head). Anything else is the Standard skin ('').
const SKIN_WEAPONS=['pistol','rifle','machinegun','fire','water','lightning','darkness','sword','daggers','shield','greatsword','emperor','ansatsuken','leet','body'];
const WEAPON_SKINS=new Set(['standard','prototype']);
const BODY_SKINS=new Set(['standard','prototype','sheldon']);
function weaponSkinMap(raw:unknown,developer=false){
 if(typeof raw!=='string'||!raw)return '';const picks=new Map<string,string>();
 for(const entry of raw.slice(0,4096).split(',')){const i=entry.indexOf(':');if(i<=0)continue;const spec=entry.slice(0,i).trim(),skin=entry.slice(i+1).trim();if(SKIN_WEAPONS.includes(spec)&&(spec==='body'?BODY_SKINS:WEAPON_SKINS).has(skin))picks.set(spec,skin);}
 if(picks.get('body')==='sheldon'&&!developer)picks.delete('body');
 return SKIN_WEAPONS.filter(s=>picks.has(s)&&picks.get(s)!=='standard').map(s=>s+':'+picks.get(s)).join(',');
}
function accountCosmetics(raw:any,a:Access){
 const clean=cosmetics(raw,a);
 const tint=(value:unknown)=>typeof value==='string'&&/^#[0-9a-f]{6}$/i.test(value)?value:'';
 return {...clean,hatColor:tint(raw?.hatColor),backColor:tint(raw?.backColor),hat:raw?.hat==='sheldon-head'&&a.developer?'sheldon-head':clean.hat,lobbyBack:LOBBY_BACKS.has(raw?.lobbyBack)?raw.lobbyBack:'tick-ring',weaponSkins:weaponSkinMap(raw?.weaponSkins,a.developer),intro:raw?.intro==='sheldon-intro'&&a.developer?'sheldon-intro':NATIVE_INTROS.has(raw?.intro)?raw.intro:clean.intro};
}
function savedProfile(raw:any,a:Access){return {name:typeof raw?.name==='string'?raw.name.replace(/[<>\u0000-\u001f]/g,'').slice(0,20)||'Pilot':'Pilot',class:(['ranged','melee','magic'].includes(raw?.class)||raw?.class==='dev'&&a.developer)?raw.class:'ranged',color:/^#[0-9a-f]{6}$/i.test(raw?.color||'')?raw.color:'#ff6b35',cosmetics:accountCosmetics(raw?.cosmetics,a)};}
function view(row:any){return {id:row.id,username:row.username,profileSchema:2,...access(row),profile:savedProfile(JSON.parse(row.profile),access(row)),hasProfile:row.profile!=='{}',revision:row.revision,grantVersion:row.grant_version};}
async function sign(row:any,env:AuthEnv,now:number){const payload=base64url(encoder.encode(JSON.stringify({kind:'chip-arena-player',user:row.username,...access(row),iat:now,exp:now+GAME_CLAIM_MS}))),key=await crypto.subtle.importKey('jwk',JSON.parse(env.DEV_SIGNING_KEY!),{name:'ECDSA',namedCurve:'P-256'},false,['sign']);return payload+'.'+base64url(await crypto.subtle.sign({name:'ECDSA',hash:'SHA-256'},key,encoder.encode(payload)));}
async function rate(request:Request,env:AuthEnv,now:number,username:string,action:string){const ip=await digest(request.headers.get('CF-Connecting-IP')||'local');const sensitive=['login','register','recover'].includes(action);const identity=sensitive?username:await digest(request.headers.get('authorization')||ip);for(const [label,limit,window] of [[`account-ip:${sensitive?'auth':'api'}:${ip}`,sensitive?12:180,60000],[`account-user:${identity}:${action}`,sensitive?15:100,300000]] as const){const key=label+':'+Math.floor(now/window);const r=await env.DB.batch([env.DB.prepare('INSERT INTO dev_login_rates(id,hits,expires) VALUES(?,1,?) ON CONFLICT(id) DO UPDATE SET hits=hits+1').bind(key,now+window*2),env.DB.prepare('SELECT hits FROM dev_login_rates WHERE id=?').bind(key)]);if(r[1].results[0].hits>limit)throw fail('Too many requests. Try again in a few minutes.',429);}}
async function readBody(request:Request){if(request.method==='GET')return {};if(!request.headers.get('content-type')?.includes('application/json'))throw fail('Expected JSON.',415);const reader=request.body?.getReader();if(!reader)throw fail('Missing request.');let size=0,text='';const decoder=new TextDecoder();while(true){const r=await reader.read();if(r.done)break;size+=r.value.length;if(size>8192){await reader.cancel();throw fail('Request too large.',413);}text+=decoder.decode(r.value,{stream:true});}try{const value=JSON.parse(text+decoder.decode());if(!value||typeof value!=='object'||Array.isArray(value))throw Error();return value;}catch{throw fail('Invalid request.');}}
export async function accounts(request:Request,env:AuthEnv,now=Date.now()){
 // Public bearer-token API. No cookies or ambient credentials; game hosts receive only short-lived claims.
 const headers={'Access-Control-Allow-Origin':'*','Access-Control-Allow-Headers':'Content-Type, Authorization','Access-Control-Allow-Methods':'GET, POST, OPTIONS','Cache-Control':'no-store','X-Content-Type-Options':'nosniff'};
 const response=(b:unknown,status=200)=>Response.json(b,{status,headers});
 if(request.method==='OPTIONS')return new Response(null,{status:204,headers});
 try{
  if(!['GET','POST'].includes(request.method))throw fail('Method not allowed.',405);
  if(!env.DB||!env.DEV_SIGNING_KEY)throw fail('Account service is temporarily unavailable.',503);
  const action=new URL(request.url).pathname.replace('/api/accounts/',''),input:any=await readBody(request);
  const username=typeof input.username==='string'?input.username.trim().toLowerCase():'';
  if(username.length>32)throw fail('Username is too long.');
  await rate(request,env,now,username||'signed-in',action);
  if(['login','register','recover'].includes(action)){
   if(request.method!=='POST')throw fail('Use the account form.',405);
   if(!/^[a-z0-9_]{3,20}$/.test(username)||typeof input.password!=='string'||input.password.length>128||input.password.length<1)throw fail('Use a username of 3–20 letters, numbers or underscores, and a password.');
   let row=await env.DB.prepare('SELECT * FROM player_accounts WHERE username=?').bind(username).first();
   const reserved=env.DEV_ACCOUNT_VERIFIER?JSON.parse(env.DEV_ACCOUNT_VERIFIER).username.toLowerCase():'';
   let recoveryCode:string|undefined;
   if(action==='register'){
    if(row||username===reserved)throw fail('That username is unavailable.',409);
    const hash=await passwordHash(input.password);recoveryCode=random();const id=crypto.randomUUID();
    try{await env.DB.prepare('INSERT INTO player_accounts(id,username,password,recovery,created,updated) VALUES(?,?,?,?,?,?)').bind(id,username,hash,await digest(recoveryCode),now,now).run();}catch{throw fail('That username is unavailable.',409);}
    row=await env.DB.prepare('SELECT * FROM player_accounts WHERE id=?').bind(id).first();
   }else if(action==='recover'){
    if(typeof input.recoveryCode!=='string'||input.recoveryCode.length>80)throw fail('Enter your recovery code and a new password.');
    const recovery=await digest(input.recoveryCode.trim());if(!row||!equal(encoder.encode(recovery),encoder.encode(row.recovery)))throw fail('Username or recovery code is incorrect.',401);
    const hash=await passwordHash(input.password);recoveryCode=random();
    // Atomic compare-and-swap consumes each recovery code once, even across simultaneous requests.
    const result=await env.DB.batch([env.DB.prepare('UPDATE player_accounts SET password=?,recovery=?,updated=? WHERE id=? AND recovery=?').bind(hash,await digest(recoveryCode),now,row.id,recovery),env.DB.prepare('DELETE FROM player_sessions WHERE account_id=? AND EXISTS(SELECT 1 FROM player_accounts WHERE id=? AND password=?)').bind(row.id,row.id,hash)]);
    if(!result[0].meta.changes)throw fail('This recovery code was already used.',401);
    row=await env.DB.prepare('SELECT * FROM player_accounts WHERE id=?').bind(row.id).first();
   }else{
    if(!row&&username===reserved&&await legacyMatches(username,input.password,env)){
     const hash=await passwordHash(input.password);recoveryCode=random();const id=crypto.randomUUID();
     await env.DB.prepare('INSERT INTO player_accounts(id,username,password,recovery,developer,created,updated) VALUES(?,?,?,?,1,?,?) ON CONFLICT(username) DO NOTHING').bind(id,username,hash,await digest(recoveryCode),now,now).run();
     row=await env.DB.prepare('SELECT * FROM player_accounts WHERE username=?').bind(username).first();
     if(row.id!==id)recoveryCode=undefined;
    }else if(!row){await passwordHash(input.password,'AAAAAAAAAAAAAAAAAAAAAA');throw fail('Username or password is incorrect.',401);}
    if(!row||!await passwordMatches(input.password,row.password))throw fail('Username or password is incorrect.',401);
   }
   const session=random(),hash=await digest(session),expires=now+(input.remember===true?REMEMBER_MS:SESSION_MS);
   await env.DB.batch([env.DB.prepare('DELETE FROM player_sessions WHERE hash IN (SELECT hash FROM player_sessions WHERE expires<? LIMIT 100)').bind(now),env.DB.prepare('DELETE FROM dev_login_rates WHERE id IN (SELECT id FROM dev_login_rates WHERE expires<? LIMIT 100)').bind(now),env.DB.prepare('INSERT INTO player_sessions(hash,account_id,expires) VALUES(?,?,?)').bind(hash,row.id,expires)]);
   return response({session,expires:expires,account:view(row),token:await sign(row,env,now),recoveryCode});
  }
  const session=request.headers.get('authorization')?.replace(/^Bearer /,'');if(!session||!/^[-\w]{43}$/.test(session))throw fail('Please log in.',401);
  const hash=await digest(session);let row=await env.DB.prepare('SELECT a.* FROM player_accounts a JOIN player_sessions s ON s.account_id=a.id WHERE s.hash=? AND s.expires>?').bind(hash,now).first();if(!row)throw fail('Session expired. Please log in again.',401);
  if(action==='me'&&request.method==='GET')return response({account:view(row),token:await sign(row,env,now)});
  if(request.method!=='POST')throw fail('Method not allowed.',405);
  if(action==='logout'){await env.DB.prepare('DELETE FROM player_sessions WHERE hash=?').bind(hash).run();return response({ok:true});}
  if(action==='profile'){
   if(!Number.isSafeInteger(input.revision))throw fail('Reload your saved pilot before saving.',409);
   const profile=savedProfile(input.profile,access(row));const r=await env.DB.prepare('UPDATE player_accounts SET profile=?,revision=revision+1,updated=? WHERE id=? AND revision=?').bind(JSON.stringify(profile),now,row.id,input.revision).run();
   if(!r.meta.changes)throw fail('Your pilot changed on another device. Reload your saved pilot before saving again.',409);
   row=await env.DB.prepare('SELECT * FROM player_accounts WHERE id=?').bind(row.id).first();return response({account:view(row),token:await sign(row,env,now)});
  }
  if(!row.developer)throw fail('Only DEVs can manage registered players.',403);
  if(action==='players'){
   const query=typeof input.query==='string'?input.query.trim().toLowerCase():'';if(!/^[a-z0-9_]{2,20}$/.test(query))throw fail('Enter at least 2 username characters.');
   const r=await env.DB.batch([env.DB.prepare('SELECT * FROM player_accounts WHERE username>=? AND username<? ORDER BY username LIMIT 20').bind(query,query+'\uffff')]);return response({players:r[0].results.map(view)});
  }
  if(action==='grant'){
   if(typeof input.accountId!=='string'||!Number.isSafeInteger(input.grantVersion)||!Array.isArray(input.unlocks)||!Array.isArray(input.flags))throw fail('Invalid player grant.');
   const unlocks=cleanGrants(input.unlocks),flags=cleanFlags(input.flags);if(unlocks.length!==input.unlocks.length||flags.length!==input.flags.length||'developer'in input)throw fail('Choose only the available items and flags. DEV roles cannot be granted here.');
   const target=await env.DB.prepare('SELECT * FROM player_accounts WHERE id=?').bind(input.accountId).first();if(!target)throw fail('Registered player not found.',404);
   const auditId=crypto.randomUUID(),detail=JSON.stringify({before:{unlocks:JSON.parse(target.unlocks),flags:JSON.parse(target.flags)},after:{unlocks,flags}});
   const r=await env.DB.batch([env.DB.prepare('INSERT INTO account_audit(id,actor,target,action,detail,created) SELECT ?,?,?,?, ?,? WHERE EXISTS(SELECT 1 FROM player_accounts WHERE id=? AND grant_version=?)').bind(auditId,row.id,target.id,'grant',detail,now,target.id,input.grantVersion),env.DB.prepare('UPDATE player_accounts SET unlocks=?,flags=?,grant_version=grant_version+1,updated=? WHERE id=? AND grant_version=?').bind(JSON.stringify(unlocks),JSON.stringify(flags),now,target.id,input.grantVersion)]);
   if(!r[1].meta.changes)throw fail('This player changed. Search again before saving.',409);
   return response({account:view(await env.DB.prepare('SELECT * FROM player_accounts WHERE id=?').bind(target.id).first())});
  }
  throw fail('Unknown account action.',404);
 }catch(e:any){return response({error:e.status?e.message:'Account service is temporarily unavailable.'},e.status||503);}
}
