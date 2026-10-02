// SEED created by the foundation; owned by stream S2 (DIRECTOR) after the foundation merges. Satisfies DirectorApi and
// does nothing: it stays in the 'opening' stage forever, never holds, defines no HP floor and passes boss input through.
// S2 replaces the body (design §6); keep the class name and constructor signature: the facade builds `new Director(host)`.
import type {Input} from '../config.js';
import type {Player} from '../simulation.js';
import type {AddDeath,DirectorApi,HordeModifiers,PveHost,PveView} from './contract.js';
import {IDENTITY_MODS,PVE} from './balance.js';

export class Director implements DirectorApi{
 private stageAt=0;
 constructor(readonly host:PveHost){}
 onFightStart(){this.stageAt=this.host.sim.time;}
 step(_dt:number){}
 stepHold(_dt:number){}
 holding(){return false;}
 hpFloor(_p:Player):number|undefined{return undefined;}
 bossInput(_p:Player,base:Input):Input{return base;}
 modifiers():HordeModifiers{return {...IDENTITY_MODS};}
 onAddKilled(_d:AddDeath){}
 onDeath(_p:Player){}
 onDamage(_p:Player,_actual:number,_source:string|null){}
 view():ReturnType<DirectorApi['view']>{
  const v:Pick<PveView,'stage'|'stageAt'|'wave'|'waves'|'waveTotal'|'waveLeft'|'hold'|'track'>={stage:'opening',stageAt:this.stageAt,wave:0,waves:PVE.wavesBeforeBoss,waveTotal:0,waveLeft:0,hold:false,track:{revealed:0,at:0}};
  return v;
 }
 reset(){this.stageAt=0;}
 debug(_op:string){return false;}
}
