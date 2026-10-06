import test from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {idleInput} from '../src/shared/config.js';

test('PvE Retry after a party member disconnected reaches the fight (no weapon-select soft-lock)',async()=>{
 await initPhysics();
 const KEY=(i:number)=>(i.toString(16).padStart(2,'0')).repeat(16);
 const phase=(x:Simulation)=>x.phase as string;
 const s=new Simulation({pve:{boss:'siren',seed:7,debug:true}});
 try{
  for(const id of ['h1','h2']){s.addPlayer(id,id,'ranged');s.pve!.command(id,{action:'hello',v:1,key:KEY(id==='h1'?1:2)},false);s.setReady(id,true);}
  for(let i=0;i<6000&&phase(s)!=='fight';i++){if(s.phase==='specialization')for(const id of ['h1','h2'])s.selectSpec(id,'pistol');s.step();}
  assert.equal(phase(s),'fight');
  s.disconnect('h2');
  s.pve!.debug('god:on');s.pve!.debug('skip-to-boss');
  for(let i=0;i<3600&&phase(s)!=='match';i++){if(i%60===0){s.pve!.debug('boss-damage:99999');s.pve!.debug('kill-adds');}s.queueInputs('h1',[{...idleInput(),seq:i+1}],`${s.setNumber}:${s.round}:${s.generation}`);s.step();}
  assert.equal(phase(s),'match');
  s.rematch();
  for(let i=0;i<60*60&&phase(s)!=='fight';i++){if(s.phase==='specialization')s.selectSpec('h1','pistol');s.step();}
  assert.equal(phase(s),'fight');
  assert.equal(s.players.has('h2'),false);
 }finally{s.dispose();}
});
