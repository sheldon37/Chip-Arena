import {normalizeRoomCode} from './relay-protocol.js';
export const DIRECTORY_ORIGIN='https://chip-arena-prototype.sheldon37.chatgpt.site';
export const DIRECTORY_TTL_MS=180000;
export function onlineRoomCode(value:unknown){if(typeof value!=='string')return null;const compact=value.trim().toUpperCase().replace(/[\s-]/g,'');return compact.startsWith('CA')&&compact.length===10?normalizeRoomCode(compact.slice(2)):null;}
export function formatOnlineCode(code:string){return `CA-${code.slice(0,4)}-${code.slice(4)}`;}
