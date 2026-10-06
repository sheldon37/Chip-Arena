// Recruit and party-bot brains (design §4.5, §4.7; stream S3). Pure functions of (host, player, base input): they read the
// world and the horde's add refs and return an Input like Simulation.botInput builds one. The medic heal ledger lives here
// too. No clocks; the only sim.random() use is the reused botAbilities call (marked pve-allow-sim-random).
import {clamp,forward,idleInput,v,add,sub,len,type Input,type Vec} from '../config.js';
import {WEAPONS} from '../content.js';
import {ballisticPitch} from '../projectiles.js';
import {botAbilities} from '../bot-abilities.js';
import type {Player} from '../simulation.js';
import type {AddRef,PveHost,RecruitRole} from './contract.js';
import {ADD_KINDS,RECRUIT} from './balance.js';
import {activeAdd} from './machines.js';

const flat=(a:Vec,b:Vec)=>Math.hypot(a.x-b.x,a.z-b.z);
const dirTo=(a:Vec,b:Vec)=>{const d=v(b.x-a.x,0,b.z-a.z),l=len(d);return l<1e-6?v():mul(d,1/l);};
const mul=(a:Vec,s:number)=>v(a.x*s,a.y*s,a.z*s);
/** Target search radius around the anchor (hirer or nearest human) and the boss range. */
export const RECRUIT_AI={addRange:18, bossRange:30, rangedNear:6, rangedFar:10, blockRange:2, tankCrowd:3, tankCrowdRange:2.5,
 medicNear:3, medicFar:5, medicBehind:4} as const;

type Target={id:string;pos:Vec;add?:AddRef};
const living=(host:PveHost)=>host.horde.adds().filter(activeAdd);
const healthy=(p:Player|undefined):p is Player=>!!p&&p.hp>0;
/** The nearest living, connected human to pos (party bots and orphaned recruits follow it). */
export function nearestHuman(host:PveHost,pos:Vec):Player|undefined{
 let best:Player|undefined,d=Infinity;for(const h of host.humans())if(h.hp>0&&h.connected){const x=flat(h.body.translation(),pos);if(x<d){d=x;best=h;}}return best;
}
/** An add winding up on p within `range` (attackAt ≤ time < attackAt + its hit time). */
export function windupOn(host:PveHost,p:Player,adds:readonly AddRef[],range:number){
 const t=host.sim.time,pos=p.body.translation();
 return adds.some(a=>a.target===p.id&&a.attackAt!==undefined&&t>=a.attackAt-1e-9&&t<a.attackAt+ADD_KINDS[a.kind].hit&&flat(a.pos,pos)<=range);
}
/** dps/tank target: the nearest visible add within addRange of the anchor (adds chasing the anchor first), else the visible boss within bossRange. */
export function pickTarget(host:PveHost,p:Player,anchor:Player|undefined,adds:readonly AddRef[]):Target|undefined{
 const s=host.sim,pos=p.body.translation(),eye=add(pos,v(0,.5,0)),centre=anchor?anchor.body.translation():pos;
 const seen=adds.filter(a=>flat(a.pos,centre)<=RECRUIT_AI.addRange&&s.lineOfSight(eye,add(a.pos,v(0,.3,0)),p.id,a.id))
  .sort((a,b)=>(a.target===anchor?.id?0:1)-(b.target===anchor?.id?0:1)||flat(a.pos,pos)-flat(b.pos,pos));
 if(seen[0])return {id:seen[0].id,pos:seen[0].pos,add:seen[0]};
 const boss=host.boss();
 if(boss&&boss.hp>0&&!host.hidden(boss)&&flat(boss.body.translation(),pos)<=RECRUIT_AI.bossRange&&s.lineOfSight(eye,add(boss.body.translation(),v(0,.4,0)),p.id,boss.id))return {id:boss.id,pos:boss.body.translation()};
 return undefined;
}
/** Move vector toward a player id: straight when clear, else the nav field. */
function toward(host:PveHost,from:Vec,id:string,to:Vec):Vec{
 const nav=host.horde.nav;if(!nav.ready()||nav.clear(from,to))return dirTo(from,to);
 const d=nav.direction(from,id);return d?v(d.x,0,d.z):dirTo(from,to);
}
function aimAt(p:Player,i:Input,pos:Vec,target:Vec){
 const w=WEAPONS[p.spec],aim=sub(add(target,v(0,.3,0)),add(pos,v(0,.5,0))),h=Math.hypot(aim.x,aim.z);
 i.yaw=Math.atan2(aim.x,aim.z);i.pitch=Math.atan2(aim.y,h);
 if(w.class!=='melee')i.pitch=clamp(ballisticPitch(h,aim.y,w.speed*p.stats.projectileSpeed,w.gravity),-1.1,1.1);
}
const seqOf=(p:Player,base?:Input)=>base?.seq??p.input.seq+1;

/** The fighting brain shared by dps and tank recruits and party bots: leash to `anchor`, target, approach or kite, attack, block. */
export function fighterInput(host:PveHost,p:Player,anchor:Player|undefined,role:'dps'|'tank',base?:Input):Input{
 const s=host.sim,i=idleInput();i.seq=seqOf(p,base);i.yaw=base?.yaw??p.input.yaw;i.pitch=base?.pitch??p.input.pitch;
 if(p.hp<=0||s.phase!=='fight')return i;
 const pos=p.body.translation(),w=WEAPONS[p.spec],adds=living(host),target=pickTarget(host,p,anchor,adds);
 let move=v();
 const leashed=anchor&&anchor.id!==p.id&&flat(anchor.body.translation(),pos)>RECRUIT.leash;
 if(leashed)move=toward(host,pos,anchor!.id,anchor!.body.translation());
 if(target){
  const d=flat(target.pos,pos);aimAt(p,i,pos,target.pos);
  if(!leashed){
   if(w.class==='melee'){if(d>w.range*.65)move=target.add?.target?toward(host,pos,target.add.target,target.pos):dirTo(pos,target.pos);}
   else{const out=dirTo(pos,target.pos);move=d>RECRUIT_AI.rangedFar?out:d<RECRUIT_AI.rangedNear?mul(out,-.8):v();
    move=add(move,mul(forward(i.yaw+Math.PI/2),Math.sin(s.time*1.1+p.id.length)>0?.6:-.6));}
  }
  i.attack=w.class==='melee'?d<w.range*1.25:true;
  i.reload=!!w.ammo&&p.ammo===0;
  botAbilities(s,p,i,d,true,1,target.pos);   // pve-allow-sim-random (reused bot ability brain draws from sim.random)
 }else if(anchor&&anchor.id!==p.id&&!leashed&&flat(anchor.body.translation(),pos)>RECRUIT_AI.medicFar)move=toward(host,pos,anchor.id,anchor.body.translation());
 const crowd=adds.filter(a=>flat(a.pos,pos)<=RECRUIT_AI.tankCrowdRange).length;
 if(windupOn(host,p,adds,RECRUIT_AI.blockRange)||role==='tank'&&crowd>=RECRUIT_AI.tankCrowd){i.block=true;i.attack=false;i.secondary=false;i.ability=false;}
 const l=len(move);if(l>1)move=mul(move,1/l);i.x=clamp(move.x,-1,1);i.z=clamp(move.z,-1,1);
 return i;
}
/** D41 follow target: the hirer; another party member only while it is below switchBelow and the hirer above hirerAbove. */
export function medicFocus(host:PveHost,p:Player,owner:string):Player|undefined{
 const hirer=host.sim.players.get(owner),frac=(q:Player)=>q.hp/Math.max(1,q.stats.hp);
 if(!healthy(hirer)||!hirer.connected&&host.isHuman(hirer))return nearestHuman(host,p.body.translation())??(healthy(hirer)?hirer:undefined);
 if(frac(hirer)>RECRUIT.medic.hirerAbove){
  const pos=p.body.translation(),low=host.party().filter(q=>q.id!==hirer.id&&q.id!==p.id&&q.hp>0&&frac(q)<RECRUIT.medic.switchBelow)
   .sort((a,b)=>flat(a.body.translation(),pos)-flat(b.body.translation(),pos))[0];
  if(low)return low;
 }
 return hirer;
}
/** Medic: follow 3–5 m behind the focus (away from the nearest add), roll from wind-ups, never attack. */
export function medicInput(host:PveHost,p:Player,owner:string,base?:Input):Input{
 const i=idleInput();i.seq=seqOf(p,base);i.yaw=base?.yaw??p.input.yaw;i.pitch=0;
 if(p.hp<=0||host.sim.phase!=='fight')return i;
 const pos=p.body.translation(),focus=medicFocus(host,p,owner),adds=living(host);if(!focus)return i;
 const fp=focus.body.translation(),threat=adds.slice().sort((a,b)=>flat(a.pos,fp)-flat(b.pos,fp))[0];
 const away=threat?dirTo(threat.pos,fp):dirTo(fp,pos),spot=add(fp,mul(len(away)>0?away:v(0,0,1),RECRUIT_AI.medicBehind)),d=flat(fp,pos);
 let move=v();
 if(d>RECRUIT.leash)move=toward(host,pos,focus.id,fp);
 else if(d>RECRUIT_AI.medicFar||d<RECRUIT_AI.medicNear||flat(spot,pos)>1.5)move=dirTo(pos,spot);
 const face=dirTo(pos,fp);if(len(face)>0)i.yaw=Math.atan2(face.x,face.z);
 i.x=clamp(move.x,-1,1);i.z=clamp(move.z,-1,1);
 if(windupOn(host,p,adds,RECRUIT_AI.blockRange)){i.dash=true;if(len(move)<.1){const side=forward(i.yaw+Math.PI/2);i.x=side.x;i.z=side.z;}}
 return i;
}
/** §4.5 per role. The hirer is the anchor; if it is gone or disconnected, the nearest human. */
export function recruitInput(host:PveHost,p:Player,role:RecruitRole,owner:string,base?:Input):Input{
 if(role==='medic')return medicInput(host,p,owner,base);
 const hirer=host.sim.players.get(owner),anchor=hirer&&hirer.connected&&hirer.hp>0?hirer:nearestHuman(host,p.body.translation())??hirer;
 return fighterInput(host,p,anchor,role,base);
}
/** §4.7 party bots: the dps brain with a 12 m leash to the nearest living human (no leash when none). Exported for the S7 balance harness. */
export function partyBotInput(host:PveHost,p:Player,base?:Input):Input{
 return fighterInput(host,p,nearestHuman(host,p.body.translation()),'dps',base);
}

/** §4.5 heal ledger for one pulse: each party target sums its in-range, visible medics with falloff, capped per pulse,
 *  scaled under fire; one sim.heal per target. Returns the healed amounts by target id (tests and balance). */
export function medicPulse(host:PveHost,medics:readonly Player[],pulse:number=RECRUIT.medic.pulse):Map<string,number>{
 const s=host.sim,out=new Map<string,number>(),M=RECRUIT.medic;
 const alive=medics.filter(m=>m.hp>0);if(!alive.length)return out;
 const targets=[...host.members(),...host.party().filter(q=>host.isRecruit(q))];
 for(const t of targets){
  if(t.hp<=0||t.hp>=t.stats.hp)continue;const tp=t.body.translation();
  const c=alive.filter(m=>flat(m.body.translation(),tp)<=M.range&&(m.id===t.id||s.lineOfSight(add(m.body.translation(),v(0,.5,0)),add(tp,v(0,.4,0)),m.id,t.id)))
   .map(m=>({m,amount:M.rate*pulse})).sort((a,b)=>b.amount-a.amount||(a.m.id<b.m.id?-1:1));
  if(!c.length)continue;
  let total=c.reduce((n,x,k)=>n+x.amount*M.falloff**k,0);
  total=Math.min(total,M.capFraction*t.stats.hp);
  if(s.time-t.lastDamage<M.underFireSeconds)total*=M.underFireScale;
  if(total>0){const healed=s.heal(t,total,c[0].m.id);out.set(t.id,healed);}
 }
 return out;
}
