import {canUse,type AccessInput} from './access.js';
import {CLASS_INFO,WEAPONS} from './content.js';
import type {ClassId,SpecId} from './config.js';

export const LOBBY_POSES={weapon:'Standing with weapon',chair:'Sitting on chair',sleep:'Sleeping',crossed:'Arms crossed'} as const;
export type LobbyPose=keyof typeof LOBBY_POSES;
export function lobbyPose(value:unknown):LobbyPose{return typeof value==='string'&&Object.hasOwn(LOBBY_POSES,value)?value as LobbyPose:'weapon';}
export function preferredWeapon(value:unknown,cls:ClassId,access:AccessInput=false):SpecId{
 const weapon=typeof value==='string'&&Object.hasOwn(WEAPONS,value)?WEAPONS[value as SpecId]:undefined;
 return weapon&&weapon.class===cls&&(!weapon.devOnly||canUse(access,'weapon:'+weapon.id))?weapon.id:CLASS_INFO[cls].default;
}
export type BotWeapon=SpecId|'random';
/** Bots never acquire restricted account access through a match option. */
export function botWeapon(value:unknown):BotWeapon{return typeof value==='string'&&Object.hasOwn(WEAPONS,value)&&!WEAPONS[value as SpecId].devOnly?value as SpecId:'random';}
/** Only verified access enables a special weapon in the pose selector. */
export function preferredWeapons(access:AccessInput=false){return Object.values(WEAPONS).filter(w=>!w.devOnly||canUse(access,'weapon:'+w.id));}
export const BOT_WEAPONS=Object.values(WEAPONS).filter(w=>!w.devOnly);
