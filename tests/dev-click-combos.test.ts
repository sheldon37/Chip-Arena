import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {idleInput,v} from '../src/shared/config.js';
import {createCharacterRig} from '../src/client/character-rig.js';
import {animateCharacter} from '../src/client/character-animation.js';
import * as T from 'three';
await initPhysics();

function game(spec:'ansatsuken'|'emperor',chips:Record<string,number>={}){
 const s=new Simulation({sandbox:true}),a=s.addPlayer('a','Fighter','melee',null,'#ff4433',{},true)!,b=s.addPlayer('b','Target')!;
 a.spec=spec;a.chips=chips;s.resetRound();s.phase='fight';
 for(const p of [...s.props.values()])s.removeProp(p.id);
 s.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});
 a.body.setTranslation(v(0,1,0),true);b.body.setTranslation(v(0,1,1.4),true);
 a.input=idleInput();a.stats.staminaRegen=0;b.hp=b.stats.hp=1000;s.world.step();
 const tick=(seconds:number,attack=false)=>{a.input.attack=attack;for(let i=0;i<Math.ceil(seconds*60);i++){s.time+=1/60;for(const p of s.players.values())s.updatePlayer(p,1/60);}};
 const tap=()=>{tick(1/60,true);tick(.45);};
 const hits=()=>s.events.filter(e=>e.source===a.id&&e.type===(spec==='emperor'?'emperor-strike':'arcade-strike')&&e.message===(spec==='emperor'?'jab':'combo'));
 return {s,a,b,tick,tap,hits,state:()=>spec==='emperor'?a.emperor!:a.arcade!};
}

for(const spec of ['ansatsuken','emperor'] as const){
 test(`${spec}: one tap is one hit; another tap advances; only the final tap finishes`,()=>{
  const {s,a,b,tick,tap,hits}=game(spec);try{
   const damage=spec==='emperor'?[8,12]:[9,11,19],cost=spec==='emperor'?3:4;
   for(let i=0;i<damage.length;i++){
    const before=b.hp;tap();tick(.25);
    assert.equal(hits().length,i+1);assert.equal(before-b.hp,damage[i]);
    assert.equal(a.stamina,100-cost*(i+1));
    assert.equal(a.attackIndex,i+1);
   }
   tap();assert.deepEqual(hits().map(e=>e.amount),[...damage.map((_,i)=>i),0]);
  }finally{s.dispose();}
 });
 test(`${spec}: hold repeats like standard melee; releasing stops after the committed hit`,()=>{
  const {s,tick,hits}=game(spec);try{
   tick(1,true);tick(.4);const count=hits().length;assert.ok(count>=3);
   tick(1);assert.equal(hits().length,count);
   const total=spec==='emperor'?2:3;assert.deepEqual(hits().map(e=>e.amount),Array.from({length:count},(_,i)=>i%total));
  }finally{s.dispose();}
 });
 test(`${spec}: timeout, block and resource refusal do not skip the first hit`,()=>{
  const {s,a,tick,tap,hits,state}=game(spec);try{
   tap();tick(2.5);tap();assert.equal(hits().at(-1)?.amount,0);
   assert.equal(s.beginBlock(a),true);tick(1);a.blockTime=0;tap();assert.equal(hits().at(-1)?.amount,0);
   a.stamina=0;const at=state().at;s.primary(a);assert.equal(state().at,at);
   a.stamina=100;tap();assert.equal(hits().at(-1)?.amount,1);
  }finally{s.dispose();}
 });
 test(`${spec}: snapshots animate the selected fist, not a replay of the whole string`,()=>{
  const {s,a,tick,state}=game(spec);try{
   const rig=createCharacterRig('#ff4433');
   for(let index=0;index<2;index++){
    s.primary(a);const start=state().at,contact=spec==='emperor'?.075:.12;
    const p=s.snapshot().players.find(p=>p.id===a.id)!;
    assert.equal((p.emperor??p.arcade)?.singleHit,true);
    assert.equal((p.emperor??p.arcade)?.index,index);
    for(let t=start;t<start+contact;t+=1/120)animateCharacter(rig,p,t,1/120);
    animateCharacter(rig,p,start+contact,1/120);rig.group.updateMatrixWorld(true);
    const left=rig.leftHand.getWorldPosition(new T.Vector3()),right=rig.rightHand.getWorldPosition(new T.Vector3());
    const leftHit=spec==='emperor'?index===0:index===1;
    assert.ok((leftHit?left.z-right.z:right.z-left.z)>.15,`${spec} hit ${index}: left ${left.z}, right ${right.z}`);
    tick(.5);
   }
  }finally{s.dispose();}
 });
}

test('Ten-Hit Rush and Pressure String still need one activation per added hit and preserve full-string cost',()=>{
 for(const [spec,chip,total,cost] of [['ansatsuken','arcade-rush',10,22],['emperor','mishima-pressure',3,6]] as const){
  const {s,a,tap,hits}=game(spec,{[chip]:1});try{
   for(let i=0;i<total;i++){tap();assert.equal(hits().length,i+1);assert.equal(hits().at(-1)?.amount,i);}
   assert.ok(Math.abs(a.stamina-(100-cost))<1e-8);
  }finally{s.dispose();}
 }
});

test('Demon Step and Emperor roll reset a partial primary chain',()=>{
 for(const spec of ['ansatsuken','emperor'] as const){
  const {s,a,tap,tick,hits}=game(spec);try{
   tap();a.grounded=true;a.input.dash=true;
   if(spec==='emperor')assert.equal(s.beginRoll(a),true);
   tick(.02);a.input.dash=false;tick(1);tap();assert.equal(hits().at(-1)?.amount,0);
  }finally{s.dispose();}
 }
});
