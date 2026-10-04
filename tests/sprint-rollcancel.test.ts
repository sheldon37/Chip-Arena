// Sprint (double-tap W) and roll-cancel of melee attacks (owner 2026-10-04): shared rules, authority, prediction parity,
// client double tap and presentation. Bots never sprint or cancel, so the pvp-golden traces are unchanged.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics,sanitizeInput,type Player,type Snapshot} from '../src/shared/simulation.js';
import {WEAPONS,statsFor} from '../src/shared/content.js';
import {CONFIG as C,idleInput,v,forward,type SpecId,type Input} from '../src/shared/config.js';
import {committed,rolling,rollProfile,rollCancelled,beforeStrike} from '../src/shared/souls-combat.js';
import {SPRINT,sprintIntent} from '../src/shared/movement.js';
import {Predictor} from '../src/client/prediction.js';
import {SprintTap} from '../src/client/sprint-tap.js';
import {attackPose} from '../src/client/character-animation.js';
import {transitionAction} from '../src/client/pose-transitions.js';
await initPhysics();
function arena(spec:SpecId='sword'){
 const s=new Simulation({sandbox:true,inputHoldSeconds:10}),a=s.addPlayer('a','A',WEAPONS[spec].class,null,'#80bdff',{},true)!,b=s.addPlayer('b','B')!;
 a.spec=spec;s.phase='fight';for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(200,1,200),material:'foundation'});
 a.body.setTranslation(v(0,.9,0),true);b.body.setTranslation(v(30,.9,30),true);b.hp=b.stats.hp=10000;
 for(let i=0;i<20;i++)s.step();return {s,a,b};
}
function input(s:Simulation,p:Player,patch:Partial<Input>={}){s.setInput(p.id,{...idleInput(),seq:p.input.seq+1,...patch});}
const flat=(p:Player)=>Math.hypot(p.body.linvel().x,p.body.linvel().z);
test('sanitizeInput keeps sprint only as an explicit true flag',()=>{
 const base={...idleInput(),seq:1};assert.equal('sprint' in sanitizeInput(base)!,false);assert.equal(sanitizeInput({...base,sprint:true})!.sprint,true);assert.equal('sprint' in sanitizeInput({...base,sprint:1})!,false);
});
test('sprint multiplies forward run speed only; strafes, backpedals, guards and attacks do not sprint',()=>{
 for(const [label,patch,expected] of [
  ['run',{z:1,yaw:0},C.speed],['sprint',{z:1,yaw:0,sprint:true},C.speed*SPRINT.speed],
  ['diagonal sprint',{x:Math.SQRT1_2,z:Math.SQRT1_2,yaw:0,sprint:true},C.speed*SPRINT.speed],
  ['strafe',{x:1,yaw:0,sprint:true},C.speed],['backpedal',{z:-1,yaw:0,sprint:true},C.speed],
 ] as const){const {s,a}=arena('pistol');try{for(let n=0;n<40;n++){input(s,a,patch as Partial<Input>);s.step();}assert.ok(Math.abs(flat(a)-expected)<.05,`${label}: ${flat(a)} vs ${expected}`);assert.equal(!!s.snapshot().players[0].sprinting,expected>C.speed,label);}finally{s.dispose();}}
 const {s,a}=arena('sword');try{for(let n=0;n<40;n++){input(s,a,{z:1,sprint:true});s.step();}assert.ok(a.sprinting);input(s,a,{z:1,sprint:true,attack:true});s.step();assert.ok(committed(a,s.time));assert.equal(a.sprinting,false);}finally{s.dispose();}
});
test('sprint replay stays within 2 cm of the authority through toggles and turns',()=>{
 const sim=new Simulation({sandbox:true,inputHoldSeconds:.75});sim.addPlayer('guest');sim.addPlayer('host');sim.phase='fight';for(const p of [...sim.props.values()])sim.removeProp(p.id);sim.addProp({id:'floor',pos:v(0,-.5,0),size:v(300,1,300),material:'foundation'});sim.players.get('guest')!.body.setTranslation(v(0,1,-8),true);sim.players.get('host')!.body.setTranslation(v(-60,1,60),true);for(let n=0;n<90;n++)sim.step();
 const pred=new Predictor({rollCancel:true});let s=sim.snapshot(),me=s.players[0];pred.reconcile(s,me);const queue:{at:number;inputs?:Input[];s?:Snapshot}[]=[],errors:number[]=[];let batch:Input[]=[];let sprinted=0;
 for(let tick=1;tick<=720;tick++){const yaw=Math.sin(tick/90)*1.2,f=forward(yaw),phase=tick%200,back=phase>=150,i={...idleInput(),seq:tick,yaw,x:f.x*(back?-1:1),z:f.z*(back?-1:1),sprint:phase>=40&&phase<170,jump:tick%170===1};
  pred.step(i,1/60,me);if(pred.sprinting)sprinted++;batch.push(i);if(tick%3===0){queue.push({at:tick+3,inputs:batch});batch=[];}
  for(const q of queue.filter(q=>q.at===tick)){if(q.inputs)assert.equal(sim.queueInputs('guest',q.inputs,'1:1:0'),true);else{s=q.s!;me=s.players[0];pred.reconcile(s,me);if(tick>60){errors.push(pred.error);if(pred.error>.02)console.log('ERR',tick,pred.error.toFixed(3),JSON.stringify(me.pos),me.grounded,i.sprint,pred.sprinting);}}}
  sim.step();if(tick%3===0)queue.push({at:tick+3,s:sim.snapshot()});}
 assert.ok(sprinted>200,`predicted sprint ticks ${sprinted}`);const max=Math.max(...errors);assert.ok(max<.02,`largest correction ${max} m`);pred.dispose();sim.dispose();
});
test('a roll cancels a committed primary in its windup: no hit, commitment and swing cleared, stamina for both spent',()=>{
 for(const spec of ['sword','daggers','shield','greatsword'] as SpecId[]){const {s,a,b}=arena(spec);try{b.body.setTranslation(v(0,.9,1.2),true);s.step();const hp=b.hp,before=a.stamina;input(s,a,{attack:true,yaw:0});s.step();assert.ok(committed(a,s.time),spec);assert.ok(a.swing,spec);
  input(s,a,{dash:true,z:-1,yaw:0});s.step();assert.ok(rolling(a,s.time),spec+' rolled');assert.equal(a.swing,null);assert.equal(a.attackCommitment,undefined);assert.equal(a.stamina,before-WEAPONS[spec].cost-rollProfile(spec,a.stats).cost,spec);
  for(let n=0;n<60;n++){input(s,a,{z:-1,yaw:0});s.step();}assert.equal(b.hp,hp,spec+' cancelled windup never hits');}finally{s.dispose();}}
});
test('a roll after contact keeps the hit; specials stay committed',()=>{
 {const {s,a,b}=arena('sword');try{b.body.setTranslation(v(0,.9,1.2),true);s.step();const hp=b.hp;input(s,a,{attack:true,yaw:0});s.step();while(b.hp===hp&&committed(a,s.time)){input(s,a,{yaw:0});s.step();}assert.ok(b.hp<hp,'hit landed');const after=b.hp;input(s,a,{dash:true,yaw:0});s.step();assert.ok(rolling(a,s.time));assert.equal(b.hp,after);}finally{s.dispose();}}
 {const {s,a}=arena('sword');try{input(s,a,{secondary:true,yaw:0});s.step();assert.ok(s.standardMelee.busy(a));input(s,a,{dash:true});s.step();assert.equal(a.roll,undefined,'special not cancellable');}finally{s.dispose();}}
});
test('predicted roll-cancel reconciles with the authority',()=>{
 for(const spec of ['sword','greatsword','daggers'] as SpecId[]){const {s,a}=arena(spec),pred=new Predictor({rollCancel:true});try{let snap=s.snapshot(),me=snap.players[0];pred.reconcile(snap,me);let rolled=false;
  for(let n=1;n<=90;n++){const i={...idleInput(),seq:a.input.seq+1,z:1,yaw:0,attack:n===10,dash:n===16};pred.step(i,1/60,me);s.setInput(a.id,i);s.step();if(n===16){assert.ok(pred.roll&&a.roll,spec+' both rolled');rolled=true;}const d=Math.hypot(pred.pos.x-a.body.translation().x,pred.pos.z-a.body.translation().z);assert.ok(d<.035,`${spec} tick ${n}: ${d}`);if(n%6===0){snap=JSON.parse(JSON.stringify(s.snapshot()));me=snap.players[0];pred.reconcile(snap,me);}}
  assert.ok(rolled);}finally{pred.dispose();s.dispose();}}
});

test('sprint pauses while attacking, blocking or aiming; the flag alone never changes speed',()=>{
 for(const patch of [{attack:true},{block:true},{ability:true}]){const i={...idleInput(),z:1,sprint:true,...patch};assert.equal(sprintIntent(i),false,JSON.stringify(patch));}
 assert.equal(sprintIntent({...idleInput(),z:1,sprint:true}),true);assert.equal(sprintIntent({...idleInput(),z:1}),false);
 const {s,a}=arena('pistol');try{for(let n=0;n<40;n++){input(s,a,{z:1,sprint:true,attack:true});s.step();}assert.ok(Math.abs(flat(a)-C.speed)<.05,'firing: run speed');assert.equal(a.sprinting,false);}finally{s.dispose();}
});
test('cooldown: a cancel before the first strike frees the next attack after the roll; after contact it does not',()=>{
 {const {s,a}=arena('greatsword');try{input(s,a,{attack:true,yaw:0});s.step();const full=a.cooldown;assert.ok(beforeStrike(a.attackCommitment!,s.time));input(s,a,{dash:true,yaw:0});s.step();
  const roll=rollProfile('greatsword',a.stats);assert.ok(a.cooldown<=roll.duration+1e-9&&a.cooldown<full,`refunded ${a.cooldown} of ${full}`);}finally{s.dispose();}}
 {const {s,a,b}=arena('greatsword');try{b.body.setTranslation(v(0,.9,1.4),true);s.step();const hp=b.hp;input(s,a,{attack:true,yaw:0});s.step();while(b.hp===hp&&committed(a,s.time)){input(s,a,{yaw:0});s.step();}
  assert.ok(b.hp<hp,'hit landed');const left=a.cooldown;input(s,a,{dash:true,yaw:0});s.step();assert.ok(rolling(a,s.time));assert.ok(Math.abs(a.cooldown-(left-1/60))<1e-6||a.cooldown>=left-1/60-1e-9,'no refund after contact');}finally{s.dispose();}}
});
test('bots never roll-cancel; an Emperor jab is cancelled by a human roll',()=>{
 {const {s,a}=arena('sword');try{input(s,a,{attack:true,yaw:0});s.step();assert.ok(committed(a,s.time));const roll=()=>{a.input={...a.input,dash:true};a.rollHeld=false;return s.beginRoll(a);};
  a.bot='hard';assert.equal(roll(),false,'a bot keeps its swing');assert.ok(committed(a,s.time));a.bot=null;assert.equal(roll(),true,'a human cancels it');}finally{s.dispose();}}
 {const {s,a}=arena('emperor');try{s.selectSpec?.('a','emperor');a.spec='emperor';input(s,a,{attack:true,yaw:0});s.step();assert.ok(a.cooldown>0,'jab cooldown');input(s,a,{dash:true,yaw:0});s.step();assert.ok(rolling(a,s.time),'emperor rolls out of the jab');}finally{s.dispose();}}
});
test('a predictor for an old host (no caps.move) never predicts a cancel',()=>{
 const {s,a}=arena('sword'),pred=new Predictor();try{const snap=s.snapshot();pred.reconcile(snap,snap.players[0]);
  pred.step({...idleInput(),seq:1,attack:true,yaw:0},1/60,snap.players[0]);pred.step({...idleInput(),seq:2,dash:true,yaw:0},1/60,snap.players[0]);assert.equal(pred.roll,undefined);}finally{pred.dispose();s.dispose();}
});
test('double-tap W: two fresh presses within 300 ms latch sprint until W is released or the keys clear',()=>{
 const t=new SprintTap();t.press(0);assert.equal(t.active(true),false,'one tap');t.release();t.press(250);assert.equal(t.active(true),true,'double tap');assert.equal(t.active(true),true,'held');
 t.release();assert.equal(t.active(true),false,'released');t.press(1000);t.release();t.press(1400);assert.equal(t.active(true),false,'too slow');
 t.release();t.press(1600);assert.equal(t.active(true),true);assert.equal(t.active(false),false,'W no longer held (keys cleared)');t.press(5000);t.clear();t.press(5100);assert.equal(t.active(true),false,'clear forgets the first tap');
});
test('presentation: a roll after the attack ends its phrase, so a cancelled swing never resumes after the roll',()=>{
 const {s,a}=arena('greatsword');try{input(s,a,{attack:true,yaw:0});s.step();input(s,a,{dash:true,yaw:0});s.step();
  const view=s.snapshot().players.find(p=>p.id==='a')!;assert.ok(rollCancelled(view,s.time));assert.equal(attackPose(view,s.time).active,false);assert.equal(transitionAction(view,s.time),'roll');
  const after=view.roll!.until+.05;assert.equal(attackPose(view,after).active,false,'no swing after the roll');assert.notEqual(transitionAction(view,after).slice(0,7),'attack-');}finally{s.dispose();}
});
test('Echo Strike: a roll during a follow-up (the first strike landed) keeps the cooldown; Emperor Electric stays committed',()=>{
 {const {s,a,b}=arena('greatsword');try{a.chips['echo-strike']=2;a.stats=statsFor(a.chips,a.spec);b.body.setTranslation(v(0,.9,1.4),true);s.step();const hp=b.hp;input(s,a,{attack:true,yaw:0});s.step();
  let n=0;while(!a.attackCommitment?.followup&&n++<120){input(s,a,{yaw:0});s.step();}assert.ok(a.attackCommitment?.followup,'follow-up started');assert.ok(b.hp<hp,'first strike landed');
  const left=a.cooldown;assert.equal(beforeStrike(a.attackCommitment!,s.time),false);input(s,a,{dash:true,yaw:0});s.step();assert.ok(rolling(a,s.time));assert.ok(a.cooldown>=left-1/60-1e-9,`no refund: ${a.cooldown} vs ${left}`);}finally{s.dispose();}}
 {const {s,a}=arena('emperor');try{a.spec='emperor';input(s,a,{electric:true,yaw:0});s.step();assert.equal(a.emperor?.action,'electric');const roll=()=>{a.input={...a.input,dash:true,electric:false};a.rollHeld=false;return s.beginRoll(a);};
  assert.equal(roll(),false,'Electric is not cancellable');while(a.emperor?.action==='electric'&&s.time<a.emperor.until){input(s,a,{yaw:0});s.step();}a.dashCd=0;assert.equal(roll(),true,'free again after its recovery');}finally{s.dispose();}}
});
