import {RELAY} from './relay-protocol.js';
import {Simulation,sanitizeInput} from './simulation.js';
import {CONFIG,ClassId} from './config.js';import {CLASS_INFO} from './content.js';
export interface PilotProfile {name:string;class:ClassId;color:string;}
export const ROOM_PROTOCOL='chip-arena-rooms-v1';
export {ROOM_ALPHABET,normalizeRoomCode,newRoomCode} from './relay-protocol.js';
export function profile(raw:any):PilotProfile {return {name:typeof raw?.name==='string'?raw.name.slice(0,20):'Pilot',class:raw&&Object.hasOwn(CLASS_INFO,raw.class)?raw.class:'ranged',color:typeof raw?.color==='string'&&/^#[0-9a-f]{6}$/i.test(raw.color)?raw.color:'#97e4d0'};}
/** The creator owns this authoritative simulation. Remote clients only submit intentions. */
export class HostedRoom {
 sim:Simulation;rematch=new Set<string>();rates=new Map<string,{at:number;count:number}>();
 constructor(readonly code:string,host:PilotProfile,bothDraft:boolean,private emit:(target:string|'all',message:any)=>void,draftChoices=6){this.sim=new Simulation({bothDraft,draftChoices,sandbox:false,inputHoldSeconds:RELAY.inputHoldSeconds});const p=profile(host);this.sim.addPlayer('host',p.name,p.class,null,p.color);}
 welcome(id:string){this.emit(id,{type:'welcome',id,owner:'host',code:this.code,transport:'https-relay'});this.broadcast();}
 join(raw:any){if(this.sim.players.has('guest')||this.sim.phase!=='lobby')return false;const p=profile(raw);this.sim.addPlayer('guest',p.name,p.class,null,p.color);this.welcome('guest');return true;}
 receive(id:string,raw:unknown,now=performance.now()){if(!this.sim.players.has(id))return false;let msg:any;try{if(typeof raw==='string'){if(raw.length>24000)return false;msg=JSON.parse(raw);}else {if(JSON.stringify(raw).length>24000)return false;msg=raw;}}catch{return false;}if(!msg||typeof msg.type!=='string')return false;
 let rate=this.rates.get(id);if(!rate||now-rate.at>=1000){rate={at:now,count:0};this.rates.set(id,rate);}if(++rate.count>CONFIG.maxInputRate+20)return false;
 if(msg.type==='inputs')return this.sim.queueInputs(id,msg.inputs,msg.epoch);
 if(msg.type==='input'){const i=sanitizeInput(msg.input);return !!i&&this.sim.setInput(id,i);}
 if(msg.type==='ping'){this.emit(id,{type:'pong',at:Number.isFinite(msg.at)?msg.at:0});return true;}
 if(msg.type==='ready')return this.sim.setReady(id,msg.ready===true);
 if(msg.type==='class')return this.sim.setClass(id,msg.class);
 if(msg.type==='spec')return this.sim.selectSpec(id,msg.id);
 if(msg.type==='chip')return this.sim.selectChip(id,msg.id);
 if(msg.type==='rematch'&&this.sim.phase==='match'){this.rematch.add(id);this.emit('all',{type:'rematchVotes',count:this.rematch.size});if(this.rematch.size===2&&[...this.sim.players.values()].every(p=>p.connected)){this.rematch.clear();this.sim.rematch();}return true;}
 if(msg.type==='lobby'&&this.sim.phase==='match'){this.sim.returnLobby();this.rematch.clear();return true;}
 if(msg.type==='debug')this.emit(id,{type:'error',message:'Debug commands are disabled in browser-hosted rooms.'});return false;
 }
 disconnect(){if(this.sim.phase==='lobby'){const p=this.sim.players.get('guest');if(p){this.sim.colliderOwners.delete(p.collider.handle);this.sim.world.removeRigidBody(p.body);this.sim.players.delete('guest');}this.sim.players.get('host')!.ready=false;}else this.sim.disconnect('guest');this.rates.delete('guest');this.emit('host',{type:'notice',message:`Guest disconnected. ${this.sim.phase==='lobby'?'Room is open again.':CONFIG.disconnectSeconds+' seconds to forfeit.'}`});this.broadcast();}
 step(){this.sim.step();if(this.sim.tick%(CONFIG.simulationHz/CONFIG.snapshotHz)===0)this.broadcast();}
 broadcast(){this.emit('all',{type:'snapshot',state:this.sim.snapshot()});}
 dispose(){this.sim.dispose();}
}
