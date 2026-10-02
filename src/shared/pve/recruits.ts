// SEED created by the foundation; owned by stream S3 (SUPPORT) after the foundation merges. Satisfies RecruitsApi and does
// nothing: no recruits can be hired, and recruits and party bots get no brain (the facade then idles them).
// S3 replaces the body (design §4.5, §4.7); keep the class name and constructor signature: the facade builds `new Recruits(host)`.
import type {Input,Vec} from '../config.js';
import type {Player} from '../simulation.js';
import type {PveHost,PveRecruitView,RecruitsApi} from './contract.js';
import {RECRUIT_PREFIX} from './contract.js';

export class Recruits implements RecruitsApi{
 constructor(readonly host:PveHost){}
 canHire(_owner:string):'ok'|'seats'|'limit'{return 'limit';}
 hire(_owner:string,_at:Vec,_yaw:number):string|null{return null;}
 inputFor(_p:Player,_base?:Input):Input|undefined{return undefined;}
 weaponAway(_p:Player){return false;}
 isRecruit(id:string){return id.startsWith(RECRUIT_PREFIX);}
 tauntNear(_pos:Vec){return false;}
 tauntTarget(_pos:Vec):string|null{return null;}
 newest():string|null{return null;}
 dismiss(_id:string):{owner:string}|null{return null;}
 step(_dt:number){}
 onDeath(_p:Player){}
 onHold(_active:boolean){}
 shift(_dt:number){}
 rekey(_from:string,_to:string){}
 views():PveRecruitView[]{return [];}
 reset(){}
}
