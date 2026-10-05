import test from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,type Player,type Snapshot} from '../src/shared/simulation.js';
import {CONFIG as C,idleInput,v,type SpecId,type Input} from '../src/shared/config.js';
import {WEAPONS,statsFor} from '../src/shared/content.js';
import {meleeSpecialCost,rollProfile} from '../src/shared/souls-combat.js';
import {SPRINT} from '../src/shared/sprint.js';
import {Predictor} from '../src/client/prediction.js';
import {sprintStatus} from '../src/client/sprint-hud.js';
await initPhysics();
function arena(spec:SpecId='pistol'){
 const s=new Simulation({sandbox:true,inputHoldSeconds:10}),p=s.addPlayer('p','Pilot',WEAPONS[spec].class,null,'#ffffff',{},true)!;
 p.spec=spec;const other=s.addPlayer('other')!;other.body.setTranslation(v(200,.9,200),true);s.phase='fight';for(const prop of [...s.props.values()])s.removeProp(prop.id);
 s.addProp({id:'floor',pos:v(0,-.5,0),size:v(1000,1,1000),material:'foundation'});p.body.setTranslation(v(0,.9,0),true);
 for(let i=0;i<20;i++)s.step();return {s,p};
}
function tick(s:Simulation,p:Player,patch:Partial<Input>={}){s.setInput(p.id,{...idleInput(),seq:p.input.seq+1,yaw:0,...patch});s.step();}
const near=(a:number,b:number)=>assert.ok(Math.abs(a-b)<1e-6,`${a} vs ${b}`);
test('standard primary attacks and echoes are cheaper; specials and rolls retain their costs',()=>{
 for(const [spec,cost,special] of [['daggers',7,30],['sword',14,38],['shield',16,45],['greatsword',20,55]] as const){
  const {s,p}=arena(spec);try{p.chips={'echo-strike':1};p.stats=statsFor(p.chips,p.spec);p.stamina=100;s.primary(p);
   assert.equal(WEAPONS[spec].cost,cost);near(p.stamina,100-cost-2*p.stats.extraStrikes);assert.equal(meleeSpecialCost(spec),special);
   assert.equal(rollProfile(spec,p.stats).cost,Math.ceil(20+({daggers:2,sword:5,shield:8,greatsword:12}[spec])*.75));
  }finally{s.dispose();}
 }
});
test('sprint spends 12 stamina per second for every class; no simultaneous regeneration',()=>{
 for(const spec of ['pistol','fire','sword','ansatsuken'] as const){const {s,p}=arena(spec);try{
  p.stamina=80;p.staminaAt=-10;for(let i=0;i<120;i++)tick(s,p,{z:1,sprint:true});near(p.stamina,56);assert.ok(p.sprinting,spec);
  assert.ok(Math.abs(Math.hypot(p.move.x,p.move.z)-C.speed*SPRINT.speed)<.01);const spent=p.stamina;
  for(let i=0;i<20;i++)tick(s,p);near(p.stamina,spent);for(let i=0;i<40;i++)tick(s,p);assert.ok(p.stamina>spent,'normal delayed regen resumes');
 }finally{s.dispose();}}
});
test('only actual sprint drains stamina; attacks, blocks, strafes and rolls do not double-charge',()=>{
 for(const patch of [{x:1,sprint:true},{z:-1,sprint:true},{z:1,sprint:true,ability:true},{sprint:true}]){const {s,p}=arena();try{p.stamina=80;p.staminaAt=s.time+10;for(let i=0;i<20;i++)tick(s,p,patch);near(p.stamina,80);assert.equal(p.sprinting,false);}finally{s.dispose();}}
 const {s,p}=arena('sword');try{tick(s,p,{z:1,sprint:true,attack:true});near(p.stamina,86);assert.equal(p.sprinting,false);const before=p.stamina;tick(s,p,{z:1,sprint:true,dash:true,rollCancel:true});near(p.stamina,before-rollProfile(p.spec,p.stats).cost);assert.equal(p.sprinting,false);}finally{s.dispose();}
});
test('empty stamina stops sprint, never goes negative, and cannot oscillate on regen while W remains held',()=>{
 const {s,p}=arena();try{p.stamina=1;p.staminaAt=s.time;for(let i=0;i<6;i++)tick(s,p,{z:1,sprint:true});near(p.stamina,0);assert.equal(p.sprintExhausted,true);assert.equal(p.sprinting,false);
  assert.equal(s.snapshot().players[0].sprintExhausted,true);for(let i=0;i<150;i++){tick(s,p,{z:1,sprint:true});assert.equal(p.sprinting,false);assert.ok(p.stamina>=0);}assert.ok(p.stamina>20);
  tick(s,p);assert.equal(p.sprintExhausted,false);const before=p.stamina;tick(s,p,{z:1,sprint:true});assert.equal(p.sprinting,true);near(p.stamina,before-.2);
  s.resetRound();assert.equal(p.sprintExhausted,false);assert.equal(p.sprinting,false);near(p.stamina,p.stats.maxStamina);
 }finally{s.dispose();}
});
test('stamina, exhaustion and sprint movement match prediction through JSON snapshots, delayed replay and restart',()=>{
 const {s,p}=arena();const pred=new Predictor({rollCancel:true,sprintStamina:true});try{
  p.stamina=8;let snap=s.snapshot(),me=snap.players[0];pred.reconcile(snap,me);
  const queue:{at:number;snap:Snapshot}[]=[];let max=0;
  for(let n=1;n<=210;n++){
   const i={...idleInput(),seq:p.input.seq+1,z:1,yaw:0,sprint:n!==130};pred.step(i,1/60,me);s.setInput(p.id,i);s.step();
   max=Math.max(max,Math.hypot(pred.pos.x-p.body.translation().x,pred.pos.z-p.body.translation().z));near(pred.stamina,p.stamina);assert.equal(pred.sprinting,p.sprinting);assert.equal(pred.sprintExhausted,!!p.sprintExhausted);
   if(n%6===0)queue.push({at:n+6,snap:JSON.parse(JSON.stringify(s.snapshot()))});
   const arrived=queue.find(q=>q.at===n);if(arrived){snap=arrived.snap;me=snap.players[0];pred.reconcile(snap,me);}
  }
  assert.ok(max<.025,`largest prediction difference ${max}`);
 }finally{pred.dispose();s.dispose();}
});
test('older move-v1 hosts keep free-sprint prediction; sprint HUD follows actual motion and clears when hidden/dead',()=>{
 const {s,p}=arena(),pred=new Predictor({rollCancel:true});try{p.stamina=0;const snap=s.snapshot();pred.reconcile(snap,snap.players[0]);pred.step({...idleInput(),z:1,sprint:true},1/60,snap.players[0]);assert.equal(pred.sprinting,true);}finally{pred.dispose();s.dispose();}
 assert.match(sprintStatus({hp:100,sprinting:true},true),/SPRINTING/);assert.match(sprintStatus({hp:100,sprintExhausted:true},true),/RELEASE W/);
 assert.equal(sprintStatus({hp:100,sprinting:true},false),'');assert.equal(sprintStatus({hp:0,sprinting:true},true),'');assert.equal(sprintStatus({hp:100},true),'');
});
