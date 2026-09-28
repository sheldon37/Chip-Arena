import {cosmetics} from './cosmetics.js';
import {NETWORK} from './network-config.js';
/** HTTPS mailbox transport; combat remains in the creator's authoritative worker. */
export const RELAY = {protocol:'chip-arena-https-v1', intervalMs:50, inputHoldSeconds:.75, timeoutMs:12000, requestTimeoutMs:4000, maxEvents:48, maxHostBytes:384000, maxGuestBytes:24000, ttlMs:7200000} as const;
export const ROOM_ALPHABET='ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
export function normalizeRoomCode(value:string){const code=value.trim().toUpperCase().replace(/[\s-]/g,'');return /^[A-HJ-NP-Z2-9]{8}$/.test(code)?code:null;}
export function newRoomCode(){return [...crypto.getRandomValues(new Uint8Array(8))].map(n=>ROOM_ALPHABET[n%32]).join('');}
export function newRoomToken(){return [...crypto.getRandomValues(new Uint8Array(32))].map(n=>n.toString(16).padStart(2,'0')).join('');}
export type RelayEvent={id:number;message:any};
export type RelayPacket={seq:number;ack:number;events:RelayEvent[];latest?:any};
export function cleanProfile(p:any){return {cosmetics:cosmetics(p?.cosmetics,true),authToken:typeof p?.authToken==='string'&&p.authToken.length<=3000?p.authToken:undefined,name:typeof p?.name==='string'?p.name.replace(/[<>]/g,'').slice(0,20)||'Pilot':'Pilot',class:['ranged','magic','melee'].includes(p?.class)?p.class:'ranged',color:/^#[0-9a-f]{6}$/i.test(p?.color||'')?p.color:'#97e4d0'};}
const guestTypes=new Set(['ready','class','spec','chip','ping','rematch','lobby']);
const hostTypes=new Set(['welcome','error','notice','pong','rematchVotes']);
export function validPacket(p:any,host:boolean):p is RelayPacket {
 if(!p||!Number.isSafeInteger(p.seq)||p.seq<1||!Number.isSafeInteger(p.ack)||p.ack<0||!Array.isArray(p.events)||p.events.length>RELAY.maxEvents)return false;
 let id=0;for(const e of p.events){if(!Number.isSafeInteger(e?.id)||e.id<=id||!e.message||!(host?hostTypes:guestTypes).has(e.message.type))return false;id=e.id;if(JSON.stringify(e.message).length>4096)return false;}
 if(p.latest!==undefined){if(host){const s=p.latest;if(s?.type!=='snapshot'||!s.state||!Number.isFinite(s.state.tick)||!Array.isArray(s.state.players)||s.state.players.length>8||!Array.isArray(s.state.props)||s.state.props.length>300)return false;}
 else {const latest=p.latest;const inputs=latest?.type==='inputs'?latest.inputs:latest?.type==='input'?[latest.input]:null;
 if(!Array.isArray(inputs)||!inputs.length||inputs.length>NETWORK.maxInputBatch)return false;
 if(latest.type==='inputs'&&(typeof latest.epoch!=='string'||!/^\d+:\d+:\d+$/.test(latest.epoch)))return false;
 if(inputs.some((i:any,n:number)=>!i||!['seq','x','z','yaw','pitch'].every(k=>typeof i[k]==='number'&&Number.isFinite(i[k]))||!Number.isSafeInteger(i.seq)||i.seq<0||JSON.stringify(i).length>2000||(n>0&&i.seq<=inputs[n-1].seq)))return false;}}
 return true;
}
/** Keep brief action taps when 60 Hz intentions are coalesced into an HTTP exchange. */
export function coalesceInput(previous:any,next:any){if(!previous)return next;const input={...next.input};for(const key of ['attack','block','secondary','jump','dash','reload'])input[key]=input[key]===true||previous.input[key]===true;return {type:'input',input};}
