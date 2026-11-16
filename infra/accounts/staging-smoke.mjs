// Run against a freshly provisioned staging service. Never print API bodies.
import assert from 'node:assert/strict';
import {randomBytes,webcrypto} from 'node:crypto';
import {readFileSync} from 'node:fs';

const origin=new URL(process.env.CHIP_STAGING_ORIGIN);
if(origin.protocol!=='https:'||origin.href!==origin.origin+'/'||origin.username||origin.password)
 throw Error('Set CHIP_STAGING_ORIGIN to the HTTPS staging origin.');
const publicKey=JSON.parse(readFileSync(process.env.CHIP_STAGING_PUBLIC_JWK,'utf8'));
if(publicKey.d)throw Error('Supply only the staging public key.');
const key=await webcrypto.subtle.importKey('jwk',publicKey,{name:'ECDSA',namedCurve:'P-256'},false,['verify']);
async function request(path,body,session,expected=200){
 const r=await fetch(new URL(path,origin),{method:body===undefined?'GET':'POST',redirect:'error',cache:'no-store',signal:AbortSignal.timeout(20000),
  headers:{'Content-Type':'application/json',...(session?{authorization:'Bearer '+session}:{})},body:body===undefined?undefined:JSON.stringify(body)});
 if(r.status!==expected)throw Error(path+' returned '+r.status+', expected '+expected);
 return r.json();
}
const health=await request('/healthz');
if(!health.ready||health.environment!=='staging'||health.state!=='active')throw Error('An active staging service is required.');
const api=(action,...args)=>request('/api/accounts/'+action,...args);
const username='stage_'+randomBytes(6).toString('hex'),password=randomBytes(32).toString('hex');
const u=await api('register',{username,password});
const [payload,signature]=u.token.split('.');
assert.ok(await webcrypto.subtle.verify({name:'ECDSA',hash:'SHA-256'},key,Buffer.from(signature,'base64url'),Buffer.from(payload)));
assert.ok(JSON.parse(Buffer.from(payload,'base64url')).accountId===u.account.id);
const v=await api('login',{username,password});
assert.ok(v.account.id===u.account.id);
await api('profile',{revision:v.account.revision,profile:{name:'Staging pilot'}},v.session);
assert.ok((await api('me',undefined,v.session)).account.profile.name==='Staging pilot');
await api('grant',{},v.session,403);
await api('social',{},v.session);
const c={version:1,mode:'pvp',completionId:crypto.randomUUID(),won:true,bots:false,seconds:80,cleared:0};
await api('xp',c,v.session,503);
if(process.env.XP_ISSUER_SECRET){
 for(let i=0;i<2;i++)await request('/internal/match-completions',{accountId:v.account.id,completion:c},process.env.XP_ISSUER_SECRET);
 assert.ok((await api('xp',c,v.session)).summary.total===150);
}else throw Error('Set the staging XP_ISSUER_SECRET on the protected test runner to complete the reward check.');
const next=await api('recover',{username,password:randomBytes(32).toString('hex'),recoveryCode:u.recoveryCode});
await api('me',undefined,v.session,401);
await api('logout',{},next.session);
await api('me',undefined,next.session,401);
console.log('Staging claim, login, persistence, permissions, presence, XP, recovery and logout passed. One synthetic account was created.');
