import {base64url} from '../shared/dev-auth.js';
import type {Database} from './worker.js';
export interface AuthEnv {DB:Database;DEV_ACCOUNT_VERIFIER?:string;DEV_SIGNING_KEY?:string;}
const result=(body:unknown,status=200)=>Response.json(body,{status,headers:{'Cache-Control':'no-store','X-Content-Type-Options':'nosniff'}});
const from64=(s:string)=>Uint8Array.from(atob(s),c=>c.charCodeAt(0));
export async function login(request:Request,env:AuthEnv,now=Date.now()){
 if(request.method!=='POST')return result({error:'Use the login form.'},405);
 const origin=request.headers.get('origin');if(origin&&origin!==new URL(request.url).origin)return result({error:'Use this game’s login form.'},403);
 if(!env.DEV_ACCOUNT_VERIFIER||!env.DEV_SIGNING_KEY)return result({error:'Developer login is not configured.'},503);
 const text=await request.text();if(text.length>2048)return result({error:'Login request too large.'},413);let input:any;try{input=JSON.parse(text);}catch{return result({error:'Invalid login request.'},400);}
 if(typeof input.username!=='string'||typeof input.password!=='string'||input.username.length>64||input.password.length>128)return result({error:'Enter your username and password.'},400);
 const ip=request.headers.get('CF-Connecting-IP')||'local';const digest=base64url(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(ip)));
 for(const [id,limit,window]of [[digest,6,60000],['account',45,300000]] as const){const key=id+':'+Math.floor(now/window);await env.DB.prepare('INSERT INTO dev_login_rates(id,hits,expires) VALUES(?,1,?) ON CONFLICT(id) DO UPDATE SET hits=hits+1').bind(key,now+window*2).run();const count=await env.DB.prepare('SELECT hits FROM dev_login_rates WHERE id=?').bind(key).first();if(count.hits>limit)return result({error:'Too many attempts. Try again in a few minutes.'},429);}
 await env.DB.prepare('DELETE FROM dev_login_rates WHERE id IN (SELECT id FROM dev_login_rates WHERE expires<? LIMIT 50)').bind(now).run();
 const account=JSON.parse(env.DEV_ACCOUNT_VERIFIER);
 // Once migrated, old clients must not bypass the account password/recovery flow.
 if(await env.DB.prepare('SELECT id FROM player_accounts WHERE username=?').bind(account.username.toLowerCase()).first())return result({error:'This DEV account now uses saved accounts. Update the game, then use Log in / Register.'},409);
 const material=await crypto.subtle.importKey('raw',new TextEncoder().encode(input.password),'PBKDF2',false,['deriveBits']);const actual=new Uint8Array(await crypto.subtle.deriveBits({name:'PBKDF2',hash:'SHA-256',salt:from64(account.salt),iterations:account.iterations},material,256)),expected=from64(account.hash);let difference=actual.length^expected.length;for(let n=0;n<actual.length;n++)difference|=actual[n]^(expected[n]||0);
 if(difference||input.username!==account.username)return result({error:'Username or password is incorrect.'},401);
 const payload=base64url(new TextEncoder().encode(JSON.stringify({user:'Sheldon',role:'dev',iat:now,exp:now+8*3600000,nonce:crypto.randomUUID()}))),key=await crypto.subtle.importKey('jwk',JSON.parse(env.DEV_SIGNING_KEY),{name:'ECDSA',namedCurve:'P-256'},false,['sign']);const signature=await crypto.subtle.sign({name:'ECDSA',hash:'SHA-256'},key,new TextEncoder().encode(payload));return result({token:payload+'.'+base64url(signature),user:'Sheldon',expires:now+8*3600000});
}
