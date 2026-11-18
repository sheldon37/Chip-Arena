import {verifyClaimSignature} from './dev-public-key.js';
export const AUTH_ORIGIN='https://chip-arena-prototype.sheldon37.chatgpt.site';
const bytes=(s:string)=>Uint8Array.from(atob(s.replace(/-/g,'+').replace(/_/g,'/')),c=>c.charCodeAt(0));
export const base64url=(b:ArrayBuffer|Uint8Array)=>btoa(String.fromCharCode(...new Uint8Array(b))).replace(/=/g,'').replace(/\+/g,'-').replace(/\//g,'_');
export async function verifyDevToken(token:unknown,now=Date.now(),publicKey?:JsonWebKey){try{if(typeof token!=='string'||token.length>1500)return false;const parts=token.split('.');if(parts.length!==2)return false;const payload=JSON.parse(new TextDecoder().decode(bytes(parts[0])));if(payload.user!=='Sheldon'||payload.role!=='dev'||!Number.isFinite(payload.exp)||payload.exp<=now||payload.exp>now+8*3600000||payload.iat>now+30000)return false;return await verifyClaimSignature(parts[0],bytes(parts[1]),publicKey);}catch{return false;}}
