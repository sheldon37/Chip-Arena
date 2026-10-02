// SEED created by the foundation; owned by stream S1 (HORDE) after the foundation merges. Satisfies HordeApi and does
// nothing: it spawns nothing, its nav is not ready and returns null, and every shift/rekey is a no-op.
// S1 replaces the body (design §3); keep the class name and constructor signature: the facade builds `new Horde(host)`.
import type {Vec} from '../config.js';
import type {Minion} from '../chaos.js';
import type {Player} from '../simulation.js';
import type {AddHitInfo,AddKind,AddRef,HordeApi,HordeFlow,PveAddTuple,PveEntryView,PveHost,PveNav} from './contract.js';

/** Seed nav: never ready, no directions, infinite distance, never clear. */
export const NULL_NAV:PveNav={ready:()=>false,direction:()=>null,distance:()=>Infinity,clear:()=>false,dirty:()=>{}};

export class Horde implements HordeApi{
 readonly nav:PveNav=NULL_NAV;
 constructor(readonly host:PveHost){}
 alive(){return 0;}
 queued(){return 0;}
 adds():AddRef[]{return [];}
 get(_n:number):AddRef|undefined{return undefined;}
 setFlow(_flow:HordeFlow|null){}
 spawn(_kind:AddKind,_opts?:{entry?:number;possessed?:boolean}):number|null{return null;}
 possess():number|null{return null;}
 possessed():number|undefined{return undefined;}
 hit(_m:Minion,_damage:number,_attacker:string,_impulse:number,_dir:Vec,_hit?:AddHitInfo){}
 assistMinion(_m:Minion,_owner:Player,_dt:number){}
 step(_dt:number){}
 freeze(){}
 onHold(_active:boolean){}
 shift(_dt:number){}
 killAll(_cause:'boss-dead'|'defeat'){}
 rekey(_from:string,_to:string){}
 views(_slots:readonly string[]):{adds:PveAddTuple[];entries:PveEntryView[]}{return {adds:[],entries:[]};}
 reset(){}
 debug(_op:string){return false;}
}
