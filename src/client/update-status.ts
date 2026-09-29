import {GAME_BUILD} from './build-info.generated.js';
import {DIRECTORY_ORIGIN} from '../shared/room-directory.js';
export type UpdateStatus='checking'|'current'|'available'|'unavailable';
export function updateStatus(current:string,data:unknown):UpdateStatus{const m=data as any;return m&&typeof m.build==='string'&&/^[a-f0-9]{24}$/.test(m.build)&&typeof m.version==='string'?m.build===current?'current':'available':'unavailable';}
export async function checkGameUpdate(fetchImpl:typeof fetch=fetch):Promise<UpdateStatus>{try{const r=await fetchImpl(DIRECTORY_ORIGIN+'/api/update-check',{cache:'no-store',credentials:'omit',redirect:'error',signal:AbortSignal.timeout(6000)});return r.ok?updateStatus(GAME_BUILD,await r.json()):'unavailable';}catch{return 'unavailable';}}
