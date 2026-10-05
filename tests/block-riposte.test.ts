// Melee block riposte (owner 2026-10-04: "make blocking melee actually worth it ... enough to get a hit or 2"): a human's
// timed block of a melee swing ends its block window and stuns the attacker until BLOCK_PUNISH.window s after the blocker's
// first strike could land (daggers: their second), so a counter at human reaction speed connects. Bots and the Siren keep
// the old stun.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {WEAPONS} from '../src/shared/content.js';
import {idleInput,v,type SpecId,type Input} from '../src/shared/config.js';
import {meleeBlockStun} from '../src/shared/blocking.js';
await initPhysics();

function duel(attacker:SpecId,blocker:SpecId){
 const s=new Simulation({sandbox:true,inputHoldSeconds:10}),a=s.addPlayer('a','A',WEAPONS[attacker].class,null,'#f55',{},true)!,b=s.addPlayer('b','B',WEAPONS[blocker].class,null,'#5f5',{},true)!;
 a.spec=attacker;b.spec=blocker;s.phase='fight';for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(100,1,100),material:'foundation'});
 const gap=Math.max(1.05,WEAPONS[attacker].range*.8);a.body.setTranslation(v(0,.9,0),true);b.body.setTranslation(v(0,.9,gap),true);a.hp=a.stats.hp=b.hp=b.stats.hp=1e4;
 for(let i=0;i<20;i++)s.step();return {s,a,b};
}
const send=(s:Simulation,p:Player,patch:Partial<Input>)=>s.setInput(p.id,{...idleInput(),seq:p.input.seq+1,...patch});
const face=(from:Player,to:Player)=>{const f=from.body.translation(),t=to.body.translation();return Math.atan2(t.x-f.x,t.z-f.z);};
const STANDARD:SpecId[]=['daggers','sword','shield','greatsword'];

test('every standard melee pairing: a human counter at 0.25 s reaction lands while the attacker is still stunned; daggers land two',()=>{
 for(const attacker of STANDARD)for(const blocker of STANDARD){const {s,a,b}=duel(attacker,blocker);try{
  send(s,a,{attack:true,yaw:face(a,b)});s.step();const swing=a.swing!;assert.ok(swing,`${attacker} swings`);
  while(s.time<swing.at-1/60){send(s,a,{yaw:face(a,b)});send(s,b,{yaw:face(b,a)});s.step();}
  send(s,b,{block:true,yaw:face(b,a)});send(s,a,{yaw:face(a,b)});s.step();
  for(let n=0;n<3&&!a.hitStunUntil;n++){send(s,b,{yaw:face(b,a)});s.step();}
  const until=a.hitStunUntil!,blockedAt=s.time,hp=a.hp;assert.ok(until>s.time,`${attacker} vs ${blocker}: blocked and stunned`);assert.equal(b.blockTime,0,'riposte frees the blocker');
  assert.ok(until-blockedAt>=meleeBlockStun(WEAPONS[attacker].damage,WEAPONS[attacker].impulse)-1e-9);
  const hits:number[]=[];let last=hp;for(let n=0;n<150&&s.time<until;n++){const react=s.time-blockedAt>=.25,d=Math.hypot(a.body.translation().x-b.body.translation().x,a.body.translation().z-b.body.translation().z),walk=react&&d>=WEAPONS[blocker].range*.9?1:0;
   send(s,b,{yaw:face(b,a),attack:react&&d<WEAPONS[blocker].range+.45,x:Math.sin(face(b,a))*walk,z:Math.cos(face(b,a))*walk});s.step();if(a.hp<last){hits.push(s.time-blockedAt);last=a.hp;}}
  // One sure counter for every weapon; the quick daggers (15 damage) get a second, about one sword counter's damage.
  const want=blocker==='daggers'?2:1,info=`${attacker} attacker vs ${blocker} blocker: counters at ${hits.map(t=>t.toFixed(2)).join(', ')||'none'} s, stun ${(until-blockedAt).toFixed(2)} s`;
  assert.equal(hits.length,want,info);
 }finally{s.dispose();}}
});

test('bots and the Siren keep the old block stun (no riposte)',()=>{
 {const {s,a,b}=duel('sword','sword');try{b.bot='hard';s.successfulBlock(b,'melee',b.body.translation(),a.id,30,v(0,0,1),300);b.blockTime=.2;
  assert.ok(Math.abs(a.hitStunUntil!-s.time-meleeBlockStun(30,300))<1e-9);}finally{s.dispose();}}
 {const {s,a,b}=duel('sword','sword');try{a.spec='siren';b.blockTime=.3;s.successfulBlock(b,'melee',b.body.translation(),a.id,30,v(0,0,1),100);
  assert.ok(Math.abs(a.hitStunUntil!-s.time-meleeBlockStun(30,100))<1e-9,'Siren attacker');assert.ok(b.blockTime>0,'no riposte against the Siren');}finally{s.dispose();}}
 {const {s,a,b}=duel('sword','sword');try{b.blockTime=.3;s.successfulBlock(b,'projectile',b.body.translation(),a.id);assert.ok(b.blockTime>0,'a blocked projectile keeps the window');}finally{s.dispose();}}
});
