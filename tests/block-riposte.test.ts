// Melee block riposte (owner 2026-10-04: "make blocking melee actually worth it ... enough to get a hit or 2"): a human
// standard-melee blocker's timed block of a melee swing keeps its guard until it attacks or rolls, and stuns the attacker
// until BLOCK_PUNISH.window s after the blocker's first strike could land. The counter ends that stun, so its own follow-up
// applies: Daggers land their pair, the Shield knocks down. Bots, ranged/magic blockers and the Siren keep the old stun.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player} from '../src/shared/simulation.js';
import {WEAPONS} from '../src/shared/content.js';
import {CONFIG as C,idleInput,v,type SpecId,type Input} from '../src/shared/config.js';
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

test('every standard melee pairing, 0.15-0.65 s host-time reaction (human reaction plus online delay), with and without Bulwark: a free counter, Daggers two, Shield knocks down',()=>{
 // Free = landed before the attacker can act again. The block's rebound push is unchanged (block-force chips push farther and
 // the stun allows for walking that back), so the counter walks back into reach before it swings.
 for(const [react,force] of [[.15,1],[.35,1],[.55,1],[.65,1],[.15,1.8],[.55,1.8]])for(const attacker of STANDARD)for(const blocker of STANDARD){const {s,a,b}=duel(attacker,blocker);b.stats.blockForce=force;try{
  const {blockedAt,until}=blockSwing(s,a,b),info=`${attacker} attacker vs ${blocker} blocker at ${react} s, block force ×${force}`;
  assert.ok(b.blockTime>0,'the guard stays up through the impact');assert.ok(until-blockedAt>=meleeBlockStun(WEAPONS[attacker].damage,WEAPONS[attacker].impulse)-1e-9);
  let free=0,freeAgain=-1,knocked=false,hp=a.hp;
  for(let n=0;n<150;n++){if(freeAgain<0&&!down(a,s))freeAgain=s.time-blockedAt;
   const go=s.time-blockedAt>=react,d=Math.hypot(a.body.translation().x-b.body.translation().x,a.body.translation().z-b.body.translation().z),r=WEAPONS[blocker].range,walk=go&&d>=r*.8?1:0;
   send(s,b,{yaw:face(b,a),attack:go&&d<r*.95,x:Math.sin(face(b,a))*walk,z:Math.cos(face(b,a))*walk});send(s,a,{yaw:face(a,b)});s.step();
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
  assert.ok(a.hitReaction!.until<=Math.max(a.hitStunUntil||0,a.hitReaction!.at+.24)+1e-9,'the counter\'s hit reaction ends with its own stagger, not the riposte');
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
 near(at('daggers','sword').stun,.14+BLOCK_PUNISH.window+15*BLOCK_PUNISH.perDamage,'Sword blocker vs Daggers');
 near(at('daggers','greatsword').stun,BLOCK_PUNISH.cap,'Greatsword blocker vs Daggers reaches the cap');
 near(at('sword','sword',(_,b)=>{b.cooldown=1;}).stun,BLOCK_PUNISH.cap,'the cap');
 // Block-force chips: the extra rebound's walk-back time goes on top of the cap.
 const pushed=at('sword','sword',(_,b)=>{b.stats.blockForce=1.8;}).stun,extra=(Math.min(800,280*1.8)-280)/C.playerMass/C.externalDrag/C.speed;
 near(pushed,Math.min(BLOCK_PUNISH.cap,.14+BLOCK_PUNISH.window+30*BLOCK_PUNISH.perDamage)+extra,'Bulwark walk-back');
 // The attacker's own damage chips push it farther too (blocked damage 60 at ×2 damage: push 400 against the base 280).
 const {s:s2,a:a2,b:b2}=duel('sword','sword');try{a2.stats.damage=2;b2.blockTime=.3;b2.lastBlock=s2.time;s2.successfulBlock(b2,'melee',b2.body.translation(),a2.id,60,v(0,0,1),300);
  near(a2.hitStunUntil!-s2.time,Math.min(BLOCK_PUNISH.cap,.14+BLOCK_PUNISH.window+60*BLOCK_PUNISH.perDamage)+(400-280)/C.playerMass/C.externalDrag/C.speed,'damage-chip walk-back');}finally{s2.dispose();}
 assert.ok(at('sword','sword').riposte&&at('sword','sword').window>0,'window kept, riposte open');
 for(const [attacker,blocker,patch,m] of [['sword','sword',(_:Player,b:Player)=>{b.bot='hard';},'bot blocker'],['sword','pistol',()=>{},'ranged blocker'],['sword','fire',()=>{},'magic blocker'],['sword','sword',(a:Player)=>{a.spec='siren';},'Siren attacker']] as const){
  const r=at(attacker,blocker,patch);near(r.stun,meleeBlockStun(WEAPONS[attacker].damage,WEAPONS[attacker].impulse),m);assert.equal(r.riposte,false,m);}
});

test('the parried attacker\'s queued afterimages spare the riposting blocker but still hit others; a third party\'s hit keeps the riposte',()=>{
 {const {s,a,b,c}=duel('sword','sword','pistol');try{const echo=(source:string,pos:{x:number;y:number;z:number})=>({at:s.time,until:s.time+.1,source,pos,dir:v(0,0,1),radius:1.2,damage:5,impulse:0,remaining:1,gap:.1,id:source+':echo',impact:'punch' as const});
  c!.body.setTranslation(v(6,.9,0),true);s.world.step();s.arcade.echoes.push(echo('a',b.body.translation()),echo('a',c!.body.translation()));s.emperor.echoes.push({source:'a',pos:b.body.translation(),at:s.time,id:'e'});
  b.blockTime=.3;b.lastBlock=s.time;s.successfulBlock(b,'melee',b.body.translation(),a.id,30,v(0,0,1),300);b.blockTime=0;
  const bh=b.hp,ch=c!.hp;s.arcade.step();s.emperor.step();assert.equal(b.hp,bh,'the blocker is spared');assert.ok(c!.hp<ch,'a third player is still hit');}finally{s.dispose();}}
 {const {s,a,b,c}=duel('sword','sword','pistol');try{b.blockTime=.3;b.lastBlock=s.time;s.successfulBlock(b,'melee',b.body.translation(),a.id,30,v(0,0,1),300);const until=a.hitStunUntil!;
  s.directHit(c!,a,a.body.translation(),v(0,0,1),5,0,'third',false);assert.equal(a.hitStunUntil,until,'a third party does not end the riposte stun');assert.ok(a.riposte);
  s.directHit(b,a,a.body.translation(),v(0,0,1),5,0,'counter');assert.equal(a.riposte,undefined);assert.ok(a.hitStunUntil!<until,'the blocker\'s counter does');}finally{s.dispose();}}
});

test('review fixes: stagger immunity cannot void the counter, an absorbed counter keeps the stun, a failed action keeps the guard',()=>{
 // The attacker recently escaped a stagger: the riposte clears that immunity so the counter's own stagger replaces the stun.
 {const {s,a,b}=duel('sword','sword');try{a.staggerImmuneUntil=s.time+.6;const {until}=blockSwing(s,a,b);assert.equal(a.staggerImmuneUntil,0);
  const hp=a.hp;for(let n=0;n<40&&a.hp>=hp;n++){send(s,b,{attack:true,yaw:face(b,a)});send(s,a,{});s.step();}
  assert.ok(a.hp<hp&&s.time<until);assert.ok((a.hitStunUntil||0)>s.time,'the counter staggers instead of freeing the attacker');}finally{s.dispose();}}
 // A counter that deals nothing (shield, invulnerability) leaves the riposte stun in place.
 {const {s,a,b}=duel('sword','sword');try{b.blockTime=.3;b.lastBlock=s.time;s.successfulBlock(b,'melee',b.body.translation(),a.id,30,v(0,0,1),300);const until=a.hitStunUntil!;
  const real=s.damage.bind(s);(s as unknown as {damage:()=>number}).damage=()=>0;s.directHit(b,a,a.body.translation(),v(0,0,1),5,0,'absorbed');(s as unknown as {damage:typeof real}).damage=real;
  assert.equal(a.hitStunUntil,until);assert.ok(a.riposte);}finally{s.dispose();}}
 // No stamina: the pressed attack cannot start, so the riposte window (and its protection) stays.
 {const {s,a,b}=duel('daggers','sword');try{blockSwing(s,a,b);const kept=b.blockTime;assert.ok(kept>0);b.stamina=0;
  send(s,b,{attack:true,yaw:face(b,a)});send(s,a,{});s.step();assert.ok(b.blockTime>0,'window kept');assert.ok(!b.swing&&!b.attackCommitment);
  b.dashCd=5;send(s,b,{dash:true,yaw:face(b,a)});send(s,a,{});s.step();assert.ok(b.blockTime>0,'a roll that cannot start keeps it too');}finally{s.dispose();}}
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
