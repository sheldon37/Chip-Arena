// Owner request 2026-10-04: "lets remove that 'aim-circle' from the new abilities, 'like pistol - grenade' we can have that
// show if we hold the button". Aimed abilities (HOLD_AIM) cast on key release for human pilots: a tap casts at once on
// release, a hold casts on release at the aim held then; block / roll cancel. Client input at 30/60/120 Hz, delivered
// latest-only (setInput) or as relay batches (queueInputs), against the 60 Hz host.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {v,idleInput,type Input} from '../src/shared/config.js';
import {HOLD_AIM,HOLD_AIM_SECONDS,HOST_CAPS} from '../src/shared/abilities.js';
import {previewOn} from '../src/client/ability-hud.js';
import {AimHoldTracker,aimCancel,holdAimE} from '../src/client/aim-hold.js';
await initPhysics();

type Mode='direct'|'queue';
/** Runs 1.6 s: `frame(t)` is the client's input at client time t; returns the casts as {t, yaw}. */
function run(hz:number,mode:Mode,frame:(t:number)=>Partial<Input>){
 const s=new Simulation();try{
  s.addPlayer('a','a','ranged');s.addPlayer('b','b','ranged');s.phase='fight';s.fightStart=s.time;
  const a=s.players.get('a')!,b=s.players.get('b')!;a.spec='pistol';a.chips={};a.body.setTranslation(v(0,.9,0),true);b.body.setTranslation(v(8,.9,8),true);s.world.step();
  const casts:{t:number;yaw:number}[]=[],epoch=()=>`${s.setNumber}:${s.round}:${s.generation}`,t0=s.time;let seq=a.input.seq,sent=0,batch:Input[]=[],nextBatch=.05;
  for(let k=0;k<96;k++){
   const now=(k+1)/60;
   while(sent/hz<=now){const t=sent/hz;sent++;const i={...idleInput(),seq:++seq,yaw:0,pitch:0,camera:'third' as const,...frame(t)};if(mode==='direct')s.setInput('a',i);else batch.push(i);}
   if(mode==='queue'&&now>=nextBatch-1e-9){if(batch.length)assert.ok(s.queueInputs('a',batch,epoch()));batch=[];nextBatch+=.05;}
   b.inputAt=s.time;if(mode==='direct')a.inputAt=s.time;s.step();
   for(const e of s.events)if(e.type==='ability-cast'&&e.source==='a'&&e.message==='frag-grenade'&&!casts.some(c=>c.t===e.time-t0))casts.push({t:e.time-t0,yaw:a.abilityCast?.aim.yaw??NaN});
  }
  return casts;
 }finally{s.dispose();}
}
for(const hz of [30,60,120])for(const mode of (hz>100?['direct']:['direct','queue']) as Mode[]){
 test(`${hz} Hz ${mode}: a quick tap casts once, right after the release`,()=>{
  const casts=run(hz,mode,t=>({secondary:t>=.1&&t<.18}));assert.equal(casts.length,1,JSON.stringify(casts));
  assert.ok(casts[0].t>=.18-1e-6&&casts[0].t<=.18+.12,`cast at ${casts[0].t}`);
 });
 test(`${hz} Hz ${mode}: holding never casts until release, then casts at the aim held at release`,()=>{
  const casts=run(hz,mode,t=>({secondary:t>=.1&&t<.8,yaw:t<.4?0:.5}));assert.equal(casts.length,1,JSON.stringify(casts));
  assert.ok(casts[0].t>=.8-1e-6,`no cast before release (${casts[0].t})`);assert.ok(Math.abs(casts[0].yaw-.5)<1e-9,'aim at release');
 });
 test(`${hz} Hz ${mode}: blocking or rolling during the hold cancels the cast`,()=>{
  assert.equal(run(hz,mode,t=>({secondary:t>=.1&&t<.8,block:t>=.4&&t<.5})).length,0,'block cancels');
  assert.equal(run(hz,mode,t=>({secondary:t>=.1&&t<.8,dash:t>=.4&&t<.45,z:t>=.4&&t<.6?1:0})).length,0,'roll cancels');
 });
}
test('bots keep cast-on-press; only the ground-targeted E abilities hold',()=>{
 assert.deepEqual([...HOLD_AIM].sort(),['frag-grenade','geyser-wall','meteor-strike','singularity']);assert.equal(HOLD_AIM_SECONDS,.2);
 const s=new Simulation();try{s.addPlayer('a','a','ranged');const a=s.players.get('a')!;a.spec='pistol';assert.equal(s.abilities.holds(a,'e'),true);assert.equal(s.abilities.holds(a,'q'),false);
  a.spec='machinegun';assert.equal(s.abilities.holds(a,'e'),false,'rocket casts on press');a.spec='pistol';a.bot='normal';assert.equal(s.abilities.holds(a,'e'),false,'bots tap');}finally{s.dispose();}
});
test('client: no aim circle by default; it shows after a 0.2 s hold on a hold-to-aim host, a cancel hides it, old hosts keep it on',()=>{
 const me={spec:'pistol',chips:{},secondaryCd:0,eCharges:1,lastSecondary:-10,lastAbility:-10,mana:100,hp:100} as any;
 assert.equal(holdAimE(HOST_CAPS,'pistol'),true);assert.equal(holdAimE({abilities:1},'pistol'),false);
 assert.equal(previewOn(me,0,HOST_CAPS,0),false);assert.equal(previewOn(me,0,HOST_CAPS,.1),false);assert.equal(previewOn(me,0,HOST_CAPS,.25),true);
 assert.equal(previewOn(me,0,{abilities:1},0),true,'old host: legacy always-on preview');
 const h=new AimHoldTracker();h.update(1,true,false);h.update(1.3,true,false);assert.ok(Math.abs(h.heldFor(1.3)-.3)<1e-9);h.update(1.35,true,aimCancel(new Set(['ShiftLeft']),false,'pistol'));assert.equal(h.heldFor(1.4),0,'cancelled');
 h.update(1.5,false,false);h.update(1.6,true,false);assert.equal(h.heldFor(1.6),0);assert.equal(aimCancel(new Set(['KeyQ']),false,'pistol'),false,'pistol Q is ADS, not a cancel');
});
