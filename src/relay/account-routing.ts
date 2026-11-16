import {accountOrigin} from '../shared/account-origin.js';

export interface AccountRouting {ACCOUNT_ROUTING?:string; ACCOUNT_UPSTREAM?:string; ACCOUNT_BRIDGE_SECRET?:string}
export function accountMaintenance(){
 return Response.json({error:'Account maintenance in progress. Please retry.'},{status:503,headers:{
  'Retry-After':'60','Cache-Control':'no-store','Access-Control-Allow-Origin':'*'
 }});
}

const ipHeader='X-Chip-Client-IP',timeHeader='X-Chip-Forwarded-At',proofHeader='X-Chip-Forwarded-Proof';
const encoder=new TextEncoder();
const bridgeKey=(secret:string)=>crypto.subtle.importKey('raw',encoder.encode(secret),{name:'HMAC',hash:'SHA-256'},false,['sign','verify']);
const proofData=(request:Request,ip:string,time:string)=>encoder.encode([ip,time,request.method,new URL(request.url).pathname].join('\n'));

// Cross-zone Worker requests share a Cloudflare source IP. Honor an original
// edge IP for throttling only when the old server authenticated that identity.
export async function accountRateRequest(request:Request,secret?:string){
 const ip=request.headers.get(ipHeader),time=request.headers.get(timeHeader),proof=request.headers.get(proofHeader);
 if(!secret||secret.length<43||!ip||!/^[0-9a-f:.]{2,48}$/i.test(ip)||!time||!/^\d{13}$/.test(time)||
    Math.abs(Date.now()-Number(time))>60000||!proof||!/^[0-9a-f]{64}$/.test(proof))return request;
 const signature=Uint8Array.from(proof.match(/../g)!,part=>parseInt(part,16));
 if(!await crypto.subtle.verify('HMAC',await bridgeKey(secret),signature,proofData(request,ip,time)))return request;
 const headers=new Headers(request.headers);
 headers.set('CF-Connecting-IP',ip);
 return new Request(request,{headers});
}

// Only called for account routes on the old Site. Never fall back to old writes
// when the independent service is unhealthy.
export async function routeOldAccounts(request:Request,env:AccountRouting){
 if(!env.ACCOUNT_ROUTING||env.ACCOUNT_ROUTING==='local')return null;
 if(env.ACCOUNT_ROUTING!=='forward'||!env.ACCOUNT_UPSTREAM||!env.ACCOUNT_BRIDGE_SECRET||env.ACCOUNT_BRIDGE_SECRET.length<43)return accountMaintenance();
 try{
  const incoming=new URL(request.url),origin=accountOrigin(env.ACCOUNT_UPSTREAM);
  if(!origin.startsWith('https://')||origin===incoming.origin)return accountMaintenance();
  if(!['GET','POST','OPTIONS'].includes(request.method))return new Response(null,{status:405});
  const headers=new Headers();
  for(const name of ['authorization','content-type','origin']){
   const value=request.headers.get(name);
   if(value)headers.set(name,name==='origin'&&value===incoming.origin?origin:value);
  }
  const ip=request.headers.get('CF-Connecting-IP');
  if(ip&&/^[0-9a-f:.]{2,48}$/i.test(ip)){
   const time=String(Date.now());
   const mac=await crypto.subtle.sign('HMAC',await bridgeKey(env.ACCOUNT_BRIDGE_SECRET),proofData(request,ip,time));
   headers.set(ipHeader,ip);headers.set(timeHeader,time);
   headers.set(proofHeader,Array.from(new Uint8Array(mac),b=>b.toString(16).padStart(2,'0')).join(''));
  }
  return await fetch(origin+incoming.pathname,{method:request.method,headers,body:request.body,
   redirect:'error',signal:AbortSignal.timeout(18000)});
 }catch{return accountMaintenance();}
}
