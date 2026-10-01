import {accessOf,canUse,type AccessInput} from './access.js';
import {CLASS_INFO,WEAPONS} from './content.js';
import type {ClassId,SpecId} from './config.js';

export const LOBBY_POSES={weapon:'Standing with weapon',chair:'Sitting on chair',sleep:'Sleeping',crossed:'Arms crossed'} as const;
export type LobbyPose=keyof typeof LOBBY_POSES;
export function lobbyPose(value:unknown):LobbyPose{return typeof value==='string'&&Object.hasOwn(LOBBY_POSES,value)?value as LobbyPose:'weapon';}
export function allowedClass(value:unknown,access:AccessInput=false):ClassId {
 return typeof value==='string'&&Object.hasOwn(CLASS_INFO,value)&&(value!=='dev'||accessOf(access).developer)?value as ClassId:'ranged';
}
/** Preserve old saved melee/magic DEV loadouts and exact item grants. */
export function weaponInClass(spec:SpecId,cls:ClassId,access:AccessInput=false){
 const w=WEAPONS[spec];return !!w&&(cls==='dev'?accessOf(access).developer&&!!w.devOnly:w.class===cls)&&(!w.devOnly||canUse(access,'weapon:'+spec));
}
export function availableClasses(access:AccessInput=false):ClassId[]{return (['ranged','magic','melee','dev'] as ClassId[]).filter(c=>c!=='dev'||accessOf(access).developer);}
export function sirenTaken(players:Iterable<{id:string;spec:SpecId;connected:boolean}>,id:string){return [...players].some(p=>p.id!==id&&p.connected&&p.spec==='siren');}
export function preferredWeapon(value:unknown,cls:ClassId,access:AccessInput=false):SpecId{
 cls=allowedClass(cls,access);
 const weapon=typeof value==='string'&&Object.hasOwn(WEAPONS,value)?WEAPONS[value as SpecId]:undefined;
 return weapon&&weaponInClass(weapon.id,cls,access)?weapon.id:CLASS_INFO[cls].default;
}
export type BotWeapon=SpecId|'random';
/** Bots never acquire restricted account access through a match option. */
export function botWeapon(value:unknown):BotWeapon{return typeof value==='string'&&Object.hasOwn(WEAPONS,value)&&!WEAPONS[value as SpecId].devOnly?value as SpecId:'random';}
/** Only verified access enables a special weapon in the pose selector. */
export function preferredWeapons(access:AccessInput=false){return Object.values(WEAPONS).filter(w=>!w.devOnly||canUse(access,'weapon:'+w.id));}
export const BOT_WEAPONS=Object.values(WEAPONS).filter(w=>!w.devOnly);
