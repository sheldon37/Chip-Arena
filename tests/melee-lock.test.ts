// Melee lock-on (owner 2026-10-04): eligibility, targets, acquire/cycle/hold-release/auto-release and steering (pure module).
import test from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {idleInput,v} from '../src/shared/config.js';
import {LockOn,LOCK_ON,lockTargets,acquire,cycle,steer,lockEligible} from '../src/client/melee-lock.js';

await initPhysics();
function game(spec:'sword'|'daggers'|'pistol'='sword'){
 const s=new Simulation({sandbox:true,inputHoldSeconds:10}),a=s.addPlayer('a','A','melee',null,'#ff234f',{},true)!,b=s.addPlayer('b','B')!;a.spec=spec;
 s.resetRound();s.phase='fight';for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});
 a.body.setTranslation(v(0,1,0),true);a.input=idleInput();b.hp=b.stats.hp=1000;s.world.step();return {s,a,b};}
const place=(p:any,x:number,z:number)=>{p.body.setTranslation(v(x,1,z),true);};

test('eligibility: melee only, fight only',()=>{const {s}=game('sword');assert.ok(lockEligible(s.snapshot(),'a'));const g=game('pistol');assert.equal(lockEligible(g.s.snapshot(),'a'),false);s.phase='result';assert.equal(lockEligible(s.snapshot(),'a'),false);s.dispose();g.s.dispose();});

test('acquire: nearest in view only; cycle wraps; death releases',()=>{
 const {s,a,b}=game();const c=s.addPlayer('c','C')!;s.resetRound();s.phase='fight';place(a,0,0);place(b,1.5,4);place(c,-6,1);s.world.step();
 let snap=s.snapshot();const pos=snap.players.find(p=>p.id==='a')!.pos;
 assert.equal(acquire(lockTargets(snap,'a'),pos,0,-.06)?.id,'b','b ahead (20°), c at ~80° is out of view');
 assert.equal(acquire(lockTargets(snap,'a'),pos,Math.PI,-.06),undefined,'nobody behind');
 assert.equal(cycle(lockTargets(snap,'a'),pos,'p:b')?.id,'c');assert.equal(cycle(lockTargets(snap,'a'),pos,'p:c')?.id,'b');
 const lock=new LockOn();lock.press(0,snap,'a',pos,0,-.06);lock.release(100,snap,'a',pos);assert.equal(lock.key,'p:b','first tap acquires, its release does not cycle');
 lock.press(200,snap,'a',pos,0,-.06);lock.release(300,snap,'a',pos);assert.equal(lock.key,'p:c','tap cycles');
 lock.press(400,snap,'a',pos,0,-.06);assert.equal(lock.tick(700,snap,'a',pos)?.id,'c');assert.equal(lock.tick(400+LOCK_ON.holdRelease*1000,snap,'a',pos),undefined,'hold releases at the threshold');lock.release(1000,snap,'a',pos);assert.equal(lock.key,undefined,'key-up after hold does not re-lock');
 lock.press(1100,snap,'a',pos,0,-.06);lock.release(1150,snap,'a',pos);assert.equal(lock.key,'p:b');
 lock.press(1160,snap,'a',pos,0,-.06);lock.release(1160+LOCK_ON.holdRelease*1000+5,snap,'a',pos);assert.equal(lock.key,undefined,'a hold no frame saw still releases on key-up');
 lock.press(1700,snap,'a',pos,0,-.06);lock.release(1750,snap,'a',pos);assert.equal(lock.key,'p:b');
 b.hp=0;snap=s.snapshot();assert.equal(lock.tick(1200,snap,'a',pos),undefined,'target died: gone');
 s.dispose();
});

test('teams: allies are never targets, even with friendly fire',()=>{const s=new Simulation({sandbox:true,gameMode:'teams',friendlyFire:true}),a=s.addPlayer('a','A','melee')!,b=s.addPlayer('b','B')!;a.spec='sword';a.team='red';b.team='red';s.resetRound();s.phase='fight';
 assert.equal(lockTargets(s.snapshot(),'a').length,0);b.team='blue';assert.equal(lockTargets(s.snapshot(),'a').length,1);s.dispose();});

test('end to end: steered yaw (client only) turns a dagger whiff into a hit; the server only sees Input.yaw',()=>{
 for(const locked of [false,true]){
  const {s,a,b}=game('daggers');place(b,Math.sin(.8)*2,Math.cos(.8)*2);s.world.step();let yaw=0,pitch=-.06,seq=1;const lock=new LockOn();
  let snap=s.snapshot();const pos=()=>snap.players.find(p=>p.id==='a')!.pos;
  if(locked){lock.press(0,snap,'a',pos(),yaw,pitch);lock.release(80,snap,'a',pos());assert.equal(lock.key,'p:b');}
  for(let i=0;i<60;i++){const t=lock.tick(i*16.7,snap,'a',pos());if(t){const r=steer(yaw,pitch,pos(),t.point,1/60);yaw=r.yaw;pitch=r.pitch;}
   s.setInput('a',{...idleInput(),seq:++seq,yaw,pitch,attack:i>=30&&i<34});s.step(1/60);snap=s.snapshot();}
  const hp=snap.players.find(p=>p.id==='b')!.hp;
  if(locked)assert.ok(hp<1000,'locked swing connects: '+hp+' yaw '+yaw.toFixed(3));else assert.equal(hp,1000,'unlocked dagger cut at yaw 0 misses a target at .8 rad, 2 m');
  s.dispose();
 }
});

test('line of sight: no lock through a wall; a lock behind cover stops steering and releases after LOCK_ON.losGrace',()=>{
 const {s,a,b}=game('sword');place(b,.5,7);s.addProp({id:'wall',pos:v(0,2,3),size:v(8,4,.6),material:'stone'});s.world.step();
 const snap=s.snapshot(),pos=snap.players.find(p=>p.id==='a')!.pos,ray=(o:any,d:any,max:number)=>s.ray(o,d,max,'a')?.timeOfImpact??null;
 const visible=(from:any,to:any)=>{const dx=to.x-from.x,dy=to.y-from.y,dz=to.z-from.z,l=Math.hypot(dx,dy,dz),hit=ray(from,{x:dx/l,y:dy/l,z:dz/l},l);return hit===null||hit>=l-.7;};
 assert.equal(acquire(lockTargets(snap,'a'),pos,0,-.06,visible),undefined,'hidden behind the wall');assert.equal(acquire(lockTargets(snap,'a'),pos,0,-.06)?.id,'b','without a sight test it would lock');
 const lock=new LockOn();lock.press(0,snap,'a',pos,0,-.06);assert.equal(lock.key,'p:b');lock.visible=visible;
 assert.equal(lock.tick(100,snap,'a',pos),undefined,'behind cover: no target to steer at');assert.equal(lock.key,'p:b','still locked within the grace');
 assert.equal(lock.tick(100+LOCK_ON.losGrace*1000,snap,'a',pos),undefined);assert.equal(lock.key,undefined,'released after the grace');
 s.removeProp('wall');s.world.step();const open=s.snapshot();lock.press(2000,open,'a',pos,0,-.06);assert.equal(lock.key,'p:b','in sight again');assert.equal(lock.tick(2100,open,'a',pos)?.id,'b');
 s.dispose();
});
