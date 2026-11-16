import type {AuthEnv} from './auth.js';

export interface SteamConfig {STEAM_APP_ID?:string; STEAM_PUBLISHER_KEY?:string; STEAM_TICKET_IDENTITY?:string}
const fail=(message:string,status:number)=>Object.assign(Error(message),{status});

// Both the app and the ticket audience come from server configuration. The
// publisher key and the full upstream URL must never enter telemetry.
export async function steamIdentity(ticket:unknown,config:SteamConfig){
 if(!config.STEAM_PUBLISHER_KEY||!config.STEAM_TICKET_IDENTITY||!/^\d{1,10}$/.test(config.STEAM_APP_ID||''))
  throw fail('Steam sign-in is unavailable.',503);
 if(typeof ticket!=='string'||ticket.length<2||ticket.length>6144||!/^(?:[0-9a-f]{2})+$/i.test(ticket))
  throw fail('Invalid Steam ticket.',400);
 const url=new URL('https://partner.steam-api.com/ISteamUserAuth/AuthenticateUserTicket/v1/');
 url.search=new URLSearchParams({key:config.STEAM_PUBLISHER_KEY,appid:config.STEAM_APP_ID!,identity:config.STEAM_TICKET_IDENTITY,ticket}).toString();
 const r=await fetch(url,{redirect:'error',cache:'no-store',signal:AbortSignal.timeout(6000)});
 if(!r.ok)throw fail('Steam authentication is unavailable.',503);
 const result=(await r.json() as any)?.response?.params;
 const id=result?.steamid;
 if(result?.result!=='OK'||typeof id!=='string'||!/^[1-9]\d{0,19}$/.test(id)||BigInt(id)>18446744073709551615n)
  throw fail('Steam ticket did not verify.',401);
 const digest=Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(ticket.toLowerCase()))),x=>x.toString(16).padStart(2,'0')).join('');
 return {id,digest};
}

export async function consumeSteamTicket(env:AuthEnv,identity:{id:string;digest:string},now:number){
 // Retain the fingerprint so replays are rejected independently of ticket lifetime.
 const result=await env.DB.prepare('INSERT INTO steam_ticket_uses(digest,used_at) VALUES(?,?) ON CONFLICT DO NOTHING').bind(identity.digest,now).run();
 if(!result.meta.changes)throw fail('Request a fresh Steam ticket.',401);
}
