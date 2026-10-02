// SEED created by the foundation; owned by stream S3 (SUPPORT) after the foundation merges. Satisfies EconomyApi and does
// nothing: rows are empty (no money, no picks), no input is forced and every command is refused.
// S3 replaces the body (design §4, §5); keep the class name and constructor signature: the facade builds `new Economy(host)`.
import type {Input} from '../config.js';
import type {Player} from '../simulation.js';
import type {AddDeath,DropTier,EconomyApi,PickSource,PveCommand,PveHost,PveMachineUse,PvePartyView,PvePickupView} from './contract.js';
import {chipPrice,revivePrice} from './balance.js';

/** An empty party row (the seed's view of every member). S3 may reuse it for initial rows. */
export function emptyPartyRow(kind:PvePartyView['kind'],connected:boolean):PvePartyView{
 return {kind,connected,money:0,earned:0,spent:0,kills:0,assists:0,heads:0,addDamage:0,bossDamage:0,revives:0,revived:0,deaths:0,
  chipsBought:0,recruitsHired:0,picks:[],selfRevives:0,reviveCost:revivePrice(0),chipPrice:chipPrice(0),healReadyAt:0};
}

export class Economy implements EconomyApi{
 constructor(readonly host:PveHost){}
 onFightStart(){}
 onAddKilled(_d:AddDeath){}
 onBossDamage(_source:string,_amount:number,_maxHp:number){}
 grant(_id:string,_amount:number,_reason:string){}
 grantAll(_amount:number,_reason:string){}
 money(_id:string){return 0;}
 grantPick(_id:string,_tier:DropTier,_source:PickSource):string|null{return null;}
 command(_id:string,_cmd:PveCommand){return false;}
 inputFor(_p:Player):Input|undefined{return undefined;}
 choosing(_p:Player){return false;}
 weaponAway(_p:Player){return false;}
 onDamage(_p:Player,_actual:number,_source:string|null){}
 onDeath(_p:Player){}
 onRevive(_p:Player){}
 respawnAll(_reason:'wave'|'phase'){}
 pendingRevive(){return false;}
 pendingHires(){return 0;}
 step(_dt:number){}
 onHold(_active:boolean){}
 shift(_dt:number){}
 rekey(_from:string,_to:string){}
 partyView(id:string):PvePartyView{const p=this.host.sim.players.get(id);return emptyPartyRow(p&&this.host.isPartyBot(p)?'bot':'human',!!p?.connected);}
 machinesView():PveMachineUse[]{return [];}
 pickupsView():PvePickupView[]{return [];}
 reset(){}
 debug(_op:string){return false;}
}
