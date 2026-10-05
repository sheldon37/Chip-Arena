// Melee block riposte (owner 2026-10-04: "make blocking melee actually worth it ... enough to get a hit or 2"): a human
// standard-melee blocker's timed block of a melee swing keeps its guard until it attacks or rolls, and stuns the attacker
// until BLOCK_PUNISH.window s after the blocker's first strike could land. The counter ends that stun, so its own follow-up
// applies: Daggers land their pair, the Shield knocks down. Bots, ranged/magic blockers and the Siren keep the old stun.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {WEAPONS} from '../src/shared/content.js';
import {idleInput,v,type SpecId,type Input} from '../src/shared/config.js';
import {BLOCK_PUNISH,meleeBlockStun} from '../src/shared/blocking.js';
await initPhysics();

function duel(attacker:SpecId,blocker:SpecId,third?:SpecId){
 const s=new Simulation({sandbox:true,inputHoldSeconds:10}),a=s.addPlayer('a','A',WEAPONS[attacker].class,null,'#f55',{},true)!,b=s.addPlayer('b','B',WEAPONS[blocker].class,null,'#5f5',{},true)!;
 const c=third?s.addPlayer('c','C',WEAPONS[third].class,null,'#55f',{},true)!:undefined;
 a.spec=attacker;b.spec=blocker;if(c&&third)c.spec=third;s.phase='fight';for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(100,1,100),material:'foundation'});
 const gap=Math.max(1.05,WEAPONS[attacker].range*.8);a.body.setTranslation(v(0,.9,0),true);b.body.setTranslation(v(0,.9,gap),true);c?.body.setTranslation(v(1.2,.9,gap),true);
 for(const p of [a,b,c])if(p)p.hp=p.stats.hp=1e4;
 for(let i=0;i<20;i++)s.step();return {s,a,b,c};
}
const send=(s:Simulation,p:Player,patch:Partial<Input>)=>s.setInput(p.id,{...idleInput(),seq:p.input.seq+1,...patch});
const face=(from:Player,to:Player)=>{const f=from.body.translation(),t=to.body.translation();return Math.atan2(t.x-f.x,t.z-f.z);};
const STANDARD:SpecId[]=['daggers','sword','shield','greatsword'];
/** a swings at b; b presses Block on the tick before the strike; steps until a is stunned by the block. */
function blockSwing(s:Simulation,a:Player,b:Player){
 send(s,a,{attack:true,yaw:face(a,b)});s.step();const swing=a.swing!;assert.ok(swing,`${a.spec} swings`);
 while(s.time<swing.at-1/60){send(s,a,{yaw:face(a,b)});send(s,b,{yaw:face(b,a)});s.step();}
 send(s,b,{block:true,yaw:face(b,a)});send(s,a,{yaw:face(a,b)});s.step();
 for(let n=0;n<3&&!a.hitStunUntil;n++){send(s,b,{yaw:face(b,a)});send(s,a,{yaw:face(a,b)});s.step();}
 assert.ok((a.hitStunUntil||0)>s.time,`${a.spec} vs ${b.spec}: blocked and stunned`);return {blockedAt:s.time,until:a.hitStunUntil!};
}
const down=(p:Player,s:Simulation)=>(p.hitStunUntil||0)>s.time||!!(p.knockdown&&s.time<p.knockdown.until)||!!(p.juggle&&s.time<p.juggle.until);

test('every standard melee pairing, 0.15-0.45 s reaction: at least one free counter, Daggers two, Shield knocks down',()=>{
 // Free = landed before the attacker can act again. The block's rebound push is unchanged, so the counter walks back in range.
 for(const react of [.15,.25,.35,.45])for(const attacker of STANDARD)for(const blocker of STANDARD){const {s,a,b}=duel(attacker,blocker);try{
  const {blockedAt,until}=blockSwing(s,a,b),info=`${attacker} attacker vs ${blocker} blocker at ${react} s`;
  assert.ok(b.blockTime>0,'the guard stays up through the impact');assert.ok(until-blockedAt>=meleeBlockStun(WEAPONS[attacker].damage,WEAPONS[attacker].impulse)-1e-9);
  let free=0,freeAgain=-1,knocked=false,hp=a.hp;
  for(let n=0;n<150;n++){if(freeAgain<0&&!down(a,s))freeAgain=s.time-blockedAt;
   const go=s.time-blockedAt>=react,d=Math.hypot(a.body.translation().x-b.body.translation().x,a.body.translation().z-b.body.translation().z),r=WEAPONS[blocker].range,walk=go&&d>=r*.9?1:0;
   send(s,b,{yaw:face(b,a),attack:go&&d<r+.45,x:Math.sin(face(b,a))*walk,z:Math.cos(face(b,a))*walk});send(s,a,{yaw:face(a,b)});s.step();
   knocked||=!!a.knockdown;if(a.hp<hp){hp=a.hp;if(freeAgain<0)free++;}}
  assert.ok(free>=(blocker==='daggers'?2:1),`${info}: ${free} free counters, attacker free again at ${freeAgain.toFixed(2)} s, stun ${(until-blockedAt).toFixed(2)} s`);
  if(blocker==='shield')assert.ok(knocked,`${info}: the Shield counter knocks down`);
 }finally{s.dispose();}}
});

test('the riposte window holds the guard until the blocker attacks; the counter ends the riposte stun',()=>{
 {const {s,a,b}=duel('sword','sword');try{const {until}=blockSwing(s,a,b);
  assert.ok(b.riposteAt!==undefined&&b.blockTime>0);assert.equal(s.snapshot().players.find(p=>p.id==='b')!.riposteAt,b.riposteAt,'live window is in the view');
  // Idle: the window simply runs out as before.
  for(let n=0;n<30&&b.blockTime>0;n++){send(s,b,{yaw:face(b,a)});send(s,a,{});s.step();}assert.equal(b.blockTime,0);
  assert.equal(s.snapshot().players.find(p=>p.id==='b')!.riposteAt,undefined,'no stale window in the view');assert.equal(a.hitStunUntil,until,'the stun runs on');
 }finally{s.dispose();}}
 {const {s,a,b}=duel('sword','sword');try{const {until}=blockSwing(s,a,b);assert.ok(b.blockTime>0);
  send(s,b,{attack:true,yaw:face(b,a)});send(s,a,{});s.step();assert.equal(b.blockTime,0,'attacking ends the window at once');assert.ok(b.swing,'and the counter starts that tick');
  const hp=a.hp;for(let n=0;n<40&&a.hp>=hp;n++){send(s,b,{attack:true,yaw:face(b,a)});send(s,a,{});s.step();}
  assert.ok(a.hp<hp&&s.time<until,'the counter lands inside the riposte stun');assert.notEqual(a.hitStunUntil,until,'and replaces it with its own stagger');
  assert.ok((a.hitStunUntil||0)<until,'the attacker recovers sooner than the full riposte stun');assert.equal(a.riposte,undefined);
 }finally{s.dispose();}}
 {const {s,a,b}=duel('sword','sword');try{blockSwing(s,a,b);send(s,b,{dash:true,yaw:face(b,a)});send(s,a,{});s.step();assert.equal(b.blockTime,0,'a roll ends the window');}finally{s.dispose();}}
});

test('until the blocker acts, the riposte window still blocks a second attacker',()=>{
 const {s,a,b,c}=duel('sword','sword','sword');try{blockSwing(s,a,b);assert.ok(b.blockTime>0);
  const hp=b.hp,blocks=s.events.filter(e=>e.type==='blockHit').length;send(s,c!,{attack:true,yaw:face(c!,b)});s.step();const swing=c!.swing!;assert.ok(swing);
  while(s.time<swing.at+2/60){assert.ok(b.blockTime>0||c!.hitStunUntil,'the strike comes inside the window');send(s,c!,{yaw:face(c!,b)});send(s,b,{yaw:face(b,a)});send(s,a,{});s.step();}
  assert.equal(b.hp,hp,'second attacker blocked');assert.ok(s.events.filter(e=>e.type==='blockHit').length>blocks);
 }finally{s.dispose();}
});

test('riposte stun values: ready + window + damage term, capped; bots, ranged blockers and the Siren keep the old stun',()=>{
 const at=(attacker:SpecId,blocker:SpecId,patch:(a:Player,b:Player)=>void=()=>{})=>{const {s,a,b}=duel(attacker,blocker);try{patch(a,b);b.blockTime=.3;b.lastBlock=s.time;
  s.successfulBlock(b,'melee',b.body.translation(),a.id,WEAPONS[attacker].damage,v(0,0,1),WEAPONS[attacker].impulse);return {stun:a.hitStunUntil!-s.time,riposte:b.riposteAt!==undefined,window:b.blockTime};}finally{s.dispose();}};
 const near=(x:number,y:number,m:string)=>assert.ok(Math.abs(x-y)<1e-9,`${m}: ${x} vs ${y}`);
 near(at('greatsword','sword').stun,.14+BLOCK_PUNISH.window+52*BLOCK_PUNISH.perDamage,'Sword blocker vs Greatsword');
 near(at('daggers','greatsword').stun,.42+BLOCK_PUNISH.window+15*BLOCK_PUNISH.perDamage,'Greatsword blocker vs Daggers');
 near(at('sword','sword',(_,b)=>{b.cooldown=1;}).stun,BLOCK_PUNISH.cap,'the cap');
 assert.ok(at('sword','sword').riposte&&at('sword','sword').window>0,'window kept, riposte open');
 for(const [attacker,blocker,patch,m] of [['sword','sword',(_:Player,b:Player)=>{b.bot='hard';},'bot blocker'],['sword','pistol',()=>{},'ranged blocker'],['sword','fire',()=>{},'magic blocker'],['sword','sword',(a:Player)=>{a.spec='siren';},'Siren attacker']] as const){
  const r=at(attacker,blocker,patch);near(r.stun,meleeBlockStun(WEAPONS[attacker].damage,WEAPONS[attacker].impulse),m);assert.equal(r.riposte,false,m);}
});

test('a riposte drops the parried attacker\'s queued afterimages; a third party\'s hit keeps the riposte for the counter',()=>{
 {const {s,a,b}=duel('sword','sword');try{const echo=(source:string)=>({at:s.time+.2,until:s.time+.3,source,pos:b.body.translation(),dir:v(0,0,1),radius:1,damage:5,impulse:0,remaining:1,gap:.1,id:source+':echo',impact:'punch' as const});
  s.arcade.echoes.push(echo('a'),echo('x'));s.emperor.echoes.push({source:'a',pos:b.body.translation(),at:s.time+.1,id:'e'});
  b.blockTime=.3;b.lastBlock=s.time;s.successfulBlock(b,'melee',b.body.translation(),a.id,30,v(0,0,1),300);
  assert.deepEqual(s.arcade.echoes.map(e=>e.source),['x']);assert.equal(s.emperor.echoes.length,0);}finally{s.dispose();}}
 {const {s,a,b,c}=duel('sword','sword','pistol');try{b.blockTime=.3;b.lastBlock=s.time;s.successfulBlock(b,'melee',b.body.translation(),a.id,30,v(0,0,1),300);const until=a.hitStunUntil!;
  s.directHit(c!,a,a.body.translation(),v(0,0,1),5,0,'third',false);assert.equal(a.hitStunUntil,until,'a third party does not end the riposte stun');assert.ok(a.riposte);
  s.directHit(b,a,a.body.translation(),v(0,0,1),5,0,'counter');assert.equal(a.riposte,undefined);assert.ok(a.hitStunUntil!<until,'the blocker\'s counter does');}finally{s.dispose();}}
});

test('the client predictor ends a live riposte window on an attack, as the host does',async()=>{
 const {Predictor}=await import('../src/client/prediction.js');
 const {s,a,b}=duel('sword','sword');const pred=new Predictor();try{blockSwing(s,a,b);
  const snapshot=s.snapshot(),view=snapshot.players.find(p=>p.id==='b')!;assert.ok(view.blockTime>0&&view.riposteAt!==undefined);pred.reconcile(snapshot,view);
  pred.step({...idleInput(),seq:b.input.seq+1,yaw:face(b,a),attack:true},1/60,view);assert.ok(pred.attackCommitment,'predicted counter starts inside the window');
  const closed={...view,riposteAt:undefined},other=new Predictor();try{other.reconcile(snapshot,closed);other.step({...idleInput(),seq:b.input.seq+1,yaw:face(b,a),attack:true},1/60,closed);
   assert.equal(other.attackCommitment,undefined,'an ordinary Block window still holds the attack');}finally{other.dispose();}
 }finally{pred.dispose();s.dispose();}
});
