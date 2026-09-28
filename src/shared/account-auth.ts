import {DEV_PUBLIC_KEY} from './dev-public-key.js';
import {verifyDevToken} from './dev-auth.js';
import {Access,cleanGrants,cleanFlags} from './access.js';
export const GAME_CLAIM_MS=15*60*1000;
export interface GameClaim extends Access {kind:'chip-arena-player';user:string;iat:number;exp:number;}
const decode=(s:string)=>Uint8Array.from(atob(s.replace(/-/g,'+').replace(/_/g,'/')),c=>c.charCodeAt(0));
export async function verifyPlayerToken(token:unknown,now=Date.now(),publicKey:JsonWebKey=DEV_PUBLIC_KEY):Promise<Access>{
 const guest:Access={developer:false,unlocks:[]};
 try{
  if(typeof token!=='string'||token.length>3000)return guest;
  const parts=token.split('.');if(parts.length!==2)return guest;
  const p=JSON.parse(new TextDecoder().decode(decode(parts[0])));
  if(p.kind!=='chip-arena-player')return await verifyDevToken(token,now,publicKey)?{developer:true,unlocks:[]}:guest;
  if(typeof p.accountId!=='string'||p.accountId.length>64||typeof p.user!=='string'||typeof p.developer!=='boolean'||!Number.isFinite(p.iat)||!Number.isFinite(p.exp)||p.iat>now+30000||p.exp<=now||p.exp-p.iat>GAME_CLAIM_MS||p.exp<=p.iat)return guest;
  const key=await crypto.subtle.importKey('jwk',publicKey,{name:'ECDSA',namedCurve:'P-256'},false,['verify']);
  if(!await crypto.subtle.verify({name:'ECDSA',hash:'SHA-256'},key,decode(parts[1]),new TextEncoder().encode(parts[0])))return guest;
  return {accountId:p.accountId,developer:p.developer,unlocks:cleanGrants(p.unlocks),flags:cleanFlags(p.flags)};
 }catch{return guest;}
}
