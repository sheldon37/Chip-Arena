// The Siren boss body (design §6.1, §6.5-§6.7; stream S2): spawn with the seat fallback, spawn clearance, possession
// placement, grow and the no-cooldown boost, and boss pathing. The Director owns the timeline; these helpers only act on
// the boss Player and the sim. Deterministic: no clocks or Math.random.
import {CONFIG as C,idleInput,v,type Input,type Vec} from '../config.js';
import type {Player} from '../simulation.js';
import type {SirenBoost} from '../siren-contract.js';
import {BOSS_PLAYER_ID,type PveHost} from './contract.js';
import {PVE_SIREN,RECRUIT,bossDifficulty,bossHp,damageScale} from './balance.js';
import {PVE_LAYOUT} from './layout.js';
import {SIREN_BOSS} from './bosses.js';

export type SpawnResult={boss:Player;dismissed?:string}|{boss:null;error:string};

/** Creates the boss Player (§6.1 step 2). The seat is reserved, so addPlayer succeeds; if it still returns null, the newest
 *  recruit is dismissed, its hirer refunded, and the add is retried once. */
export function spawnBoss(host:PveHost):SpawnResult{
 const s=host.sim,h=host.partySize();
 const add=()=>{host.spawningBoss=true;try{return s.addPlayer(BOSS_PLAYER_ID,SIREN_BOSS.name,'melee',bossDifficulty(h),SIREN_BOSS.color,{preferredSpec:'siren'},{developer:false,unlocks:['weapon:siren']});}finally{host.spawningBoss=false;}};
 let boss=add(),dismissed:string|undefined;
 if(!boss){
  const id=host.recruits.newest();
  if(id){const out=host.recruits.dismiss(id);if(out){dismissed=id;host.economy.grant(out.owner,RECRUIT.price,'refund');}}
  boss=add();
 }
 if(boss&&!s.siren.active(boss)){s.removePlayer(BOSS_PLAYER_ID);boss=null;}
 if(!boss)return {boss:null,error:'Boss could not spawn'};
 const sp=PVE_LAYOUT.bossSpawn;
 boss.team='purple';boss.ready=true;boss.selected=true;
 boss.stats={...boss.stats,hp:bossHp(h),damage:boss.stats.damage*damageScale(h)};boss.hp=boss.stats.hp;
 boss.body.setTranslation(host.standAt(sp.x,sp.z,boss),true);boss.body.setLinvel(v(),true);boss.ext=v();boss.move=v();
 boss.input={...idleInput(),seq:boss.input.seq,yaw:sp.yaw};
 s.siren.state(boss);
 pushOut(host,{x:sp.x,z:sp.z},PVE_SIREN.spawnClear);
 return dismissed?{boss,dismissed}:{boss};
}

/** Living party members (and recruits) within `radius` of `at` move out to `radius` on the floor plane (spawn clearance). */
export function pushOut(host:PveHost,at:{x:number;z:number},radius:number){
 for(const p of host.party()){
  if(p.hp<=0)continue;const pos=p.body.translation(),dx=pos.x-at.x,dz=pos.z-at.z,d=Math.hypot(dx,dz);if(d>=radius)continue;
  // Straight away from the spawn; a member exactly on it goes north (toward the hall centre).
  const ux=d>1e-6?dx/d:0,uz=d>1e-6?dz/d:-1;
  p.body.setTranslation(v(at.x+ux*radius,pos.y,at.z+uz*radius),true);p.body.setLinvel(v(0,p.body.linvel().y,0),true);p.ext=v();p.move=v();
 }
}

/** The standing centre for the boss over (x,z) where a dead add stood: the floor under the add (a downward ray, so a crawling
 *  or lying add never puts the boss in the floor) plus the boss's own half extent. */
export function standOverAdd(host:PveHost,boss:Player,pos:Vec){
 const floor=host.sim.siren.ground(pos).y;
 return v(pos.x,floor+boss.collider.halfHeight()+boss.collider.radius()+.05,pos.z);
}

/** Shows the boss again over the dead possessed add with no motion (§6.5 found, step 2). */
export function revealAt(host:PveHost,boss:Player,pos:Vec){
 host.setHidden(boss,false);
 boss.body.setTranslation(standOverAdd(host,boss,pos),true);boss.body.setLinvel(v(),true);boss.ext=v();boss.move=v();
}

/** Chip 3 "Am I beautiful?": boost, bigger collider, lifted so the larger capsule does not start in the floor (§6.6). */
export function grow(boss:Player,time:number){
 const b=PVE_SIREN.beautiful,st=boss.siren??={mode:'melee',modeAt:-10,ready:{}};
 st.boost={scale:b.scale,reach:b.reach,cooldown:1,at:time};
 boss.collider.setHalfHeight(C.playerHalfHeight*b.scale);boss.collider.setRadius(C.playerRadius*b.scale);
 const pos=boss.body.translation();boss.body.setTranslation(v(pos.x,pos.y+(b.scale-1)*(C.playerHalfHeight+C.playerRadius),pos.z),true);
}

/** Chip 4 "Fine then... I'll play": no cooldowns (only the move's own duration gates a re-cast), concurrency caps (§6.7, D35). */
export function play(boss:Player,time:number){
 const p=PVE_SIREN.play,st=boss.siren??={mode:'melee',modeAt:-10,ready:{}},prev:SirenBoost=st.boost??{scale:1,reach:1,cooldown:1,at:time};
 st.boost={...prev,cooldown:p.cooldown,minRecast:{...p.minRecast},maxRains:p.maxRains,maxEruptions:p.maxEruptions};
 // Abilities already waiting on a long cooldown are ready as soon as the current move ends.
 if(st.ready)for(const id of Object.keys(st.ready) as (keyof typeof st.ready)[])st.ready[id]=Math.min(st.ready[id]??0,time);
}

/** The opponent the Siren bot chases: the same rule as Simulation.botInput (nearest connected, living enemy). */
export function bossTarget(host:PveHost,boss:Player):Player|undefined{
 const s=host.sim,pos=boss.body.translation(),d=(q:Player)=>{const t=q.body.translation();return Math.hypot(t.x-pos.x,t.y-pos.y,t.z-pos.z);};
 return [...s.players.values()].filter(x=>s.enemy(boss.id,x.id)&&x.connected&&x.hp>0).sort((a,b)=>d(a)-d(b))[0];
}

/** Boss pathing (§6, S2 brief): without a straight line to its target the bot's move is replaced by the flow-field
 *  direction on the party grid (the boss grid once grown). Idle while hidden. */
export function bossInput(host:PveHost,boss:Player,base:Input):Input{
 if(host.hidden(boss))return {...idleInput(),seq:base.seq,yaw:base.yaw,pitch:base.pitch,camera:base.camera};
 const target=bossTarget(host,boss),nav=host.horde.nav;if(!target||!nav.ready())return base;
 const grid=(boss.siren?.boost?.scale??1)>1?'boss':'party',from=boss.body.translation(),to=target.body.translation();
 if(nav.clear(from,to,grid))return base;
 const dir=nav.direction(from,target.id,grid);if(!dir)return base;
 return {...base,x:dir.x,z:dir.z};
}
