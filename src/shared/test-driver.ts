import {ARENA} from './arena.js';
import {Snapshot} from './simulation.js';
import {idleInput,Input,clamp} from './config.js';
import {ballisticPitch} from './projectiles.js';
import {WEAPONS,statsFor} from './content.js';
// Input-only repeatable test pilot. No access to bodies, damage, Chips, or physics mutation.
export function testPilot(state:Snapshot,id:string,seq:number,stationary=false):Input{
 const p=state.players.find(p=>p.id===id),o=state.players.find(p=>p.id!==id),input=idleInput();input.seq=seq;if(!p||!o)return input;const dx=o.pos.x-p.pos.x,dz=o.pos.z-p.pos.z,distance=Math.hypot(dx,dz);input.yaw=Math.atan2(dx,dz);const w=WEAPONS[p.spec];input.pitch=ballisticPitch(distance,o.pos.y-p.pos.y-.22,w.speed*statsFor(p.chips).projectileSpeed,w.gravity);if(stationary)return input;const melee=WEAPONS[p.spec].class==='melee',range=melee?WEAPONS[p.spec].range*.65:6;
 if(distance>range){input.x=dx/(distance||1);input.z=dz/(distance||1);}input.attack=true;input.secondary=melee&&distance<5;input.reload=p.ammo===0;input.jump=state.tick%150<12&&distance>range+1;input.dash=melee&&distance>5&&distance<10; if(Math.abs(p.pos.x)>ARENA.width/2-3)input.x=-Math.sign(p.pos.x);if(Math.abs(p.pos.z)>ARENA.depth/2-3)input.z=-Math.sign(p.pos.z);return input;
}
