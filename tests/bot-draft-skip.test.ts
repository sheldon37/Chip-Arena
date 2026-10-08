import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {HostedRoom} from '../src/shared/host-room.js';
import {DRAFT_REVEAL_SECONDS} from '../src/shared/draft-settings.js';
import {offerChips} from '../src/shared/content.js';
import {botDraftOption} from '../src/client/bot-draft-option.js';
await initPhysics();

function mixed(skipBotDrafts=false){
 const s=new Simulation({skipBotDrafts,selectionSeconds:45});
 s.addPlayer('a');s.addPlayer('bot','Bot','ranged','normal');s.addPlayer('b');s.addPlayer('siren','Siren','dev','hard','#ffffff',{preferredSpec:'siren'},true);
 for(const p of s.players.values()){p.selected=false;p.offers=p.spec==='siren'?['siren-crown','siren-hide']:['vital'];}
 return s;
}

test('skip is opt-in, strictly boolean, retained by lobby changes, and host controlled',()=>{
 const s=new Simulation({skipBotDrafts:'true' as unknown as boolean});try{assert.equal(s.options.skipBotDrafts,false);}finally{s.dispose();}
 const r=new HostedRoom('DRAFT234',{name:'Host',class:'ranged',color:'#ffffff'},false,()=>{},6,false,false,45,{skipBotDrafts:true});
 try{
  r.join({name:'Guest'},false,'guest');assert.equal(r.sim.options.skipBotDrafts,true);
  assert.equal(r.receive('guest',{type:'settings',skipBotDrafts:false}),false);assert.equal(r.sim.options.skipBotDrafts,true);
  r.receive('host',{type:'settings',botCount:2});assert.equal(r.sim.snapshot().options.skipBotDrafts,true);
  r.receive('host',{type:'settings',skipBotDrafts:'true'});assert.equal(r.sim.options.skipBotDrafts,false);
  r.receive('host',{type:'settings',skipBotDrafts:true});r.sim.phase='fight';assert.equal(r.receive('host',{type:'settings',skipBotDrafts:false}),false);
  r.sim.returnLobby();assert.equal(r.sim.options.skipBotDrafts,true);
 }finally{r.dispose();}
 assert.match(botDraftOption(true),/id="skip-bot-drafts"[^>]*checked/);
 assert.match(botDraftOption(false,false),/id="skip-bot-drafts"[^>]*disabled/);
 assert.ok(!botDraftOption(false).includes(' checked'));assert.match(botDraftOption(true),/Bots still receive a Chip instantly/);
});

test('disabled option retains bot choice delay and the full reveal',()=>{
 const s=mixed();try{
  s.startDraft(['bot','a']);const d=s.draft!;assert.equal(d.player,'bot');assert.ok(d.readyAt>s.time);
  s.time=d.readyAt+1.39;s.advance();assert.equal(s.players.get('bot')!.chips.vital,undefined);
  s.time=d.readyAt+1.4;s.advance();assert.equal(s.players.get('bot')!.chips.vital,1);const chosenAt=s.time;
  s.time=chosenAt+DRAFT_REVEAL_SECONDS-.01;s.advance();assert.equal(s.draft!.player,'bot');
  s.time=chosenAt+DRAFT_REVEAL_SECONDS+.01;s.advance();assert.equal(s.draft!.player,'a');
 }finally{s.dispose();}
});

test('mixed drafts instantly award bots between human turns without exposing bot selections',()=>{
 const s=mixed(true);try{
  const start=s.time;s.startDraft(['bot','a','siren','b']);assert.equal(s.time,start);
  assert.equal(s.snapshot().draft!.player,'a');assert.equal(s.draft!.chosen,undefined);assert.equal(s.players.get('bot')!.chips.vital,1);
  assert.equal(s.selectChip('a','vital'),false,'human reveal/readiness gate remains');
  s.time=s.draft!.readyAt;assert.equal(s.phaseEnd-s.time,45);assert.equal(s.selectChip('a','vital'),true);
  const picked=s.time;s.time=picked+DRAFT_REVEAL_SECONDS-.01;s.advance();assert.equal(s.draft!.player,'a');assert.equal(s.players.get('siren')!.chips['siren-crown'],undefined);
  s.time=picked+DRAFT_REVEAL_SECONDS+.01;s.advance();assert.equal(s.snapshot().draft!.player,'b');assert.equal(s.draft!.chosen,undefined);
  assert.equal(s.players.get('siren')!.chips['siren-crown'],1);assert.equal(s.phaseEnd-s.draft!.readyAt,45);
  s.time=s.draft!.readyAt;s.selectChip('b','vital');s.time+=DRAFT_REVEAL_SECONDS+.01;s.advance();
  assert.equal(s.phase,'countdown');assert.equal(s.setNumber,2);assert.equal(s.players.get('siren')!.hp,18000);
  for(let i=0;i<3;i++)s.advance();assert.equal(s.players.get('bot')!.chips.vital,1);assert.equal(s.players.get('siren')!.chips['siren-crown'],1);
 }finally{s.dispose();}
});

test('bot-only losing squad skips the entire draft and every eligible bot receives one chip',()=>{
 const s=new Simulation({skipBotDrafts:true,gameMode:'teams',roundsPerSet:1,skipIntros:true});try{
  const host=s.addPlayer('host')!;s.configureLobby({botCount:3,botTeams:['blue','blue','blue']});s.setTeam('host','red');
  s.phase='fight';for(const p of s.players.values())if(p.bot)p.hp=0;s.resolveRound();s.time=s.phaseEnd;s.advance();
  assert.equal(s.phase,'countdown');assert.equal(s.draft,undefined);assert.equal(s.setNumber,2);assert.deepEqual(host.chips,{});
  for(const p of s.players.values())if(p.bot)assert.equal(Object.values(p.chips).reduce((a,b)=>a+b,0),1);
 }finally{s.dispose();}
});

test('skipping uses the same first offered chip and exclusive limits as visible bot choices',()=>{
 const results=[];
 for(const skip of [false,true]){
  const s=mixed(skip);try{
   const bot=s.players.get('siren')!;bot.chips={'siren-crown':1};bot.offers=offerChips('siren',bot.chips,()=>.2,6);const first=bot.offers[0];
   assert.notEqual(first,'siren-crown');s.startDraft(['siren']);
   if(!skip){s.time=s.draft!.readyAt+1.4;s.advance();s.time+=DRAFT_REVEAL_SECONDS+.01;s.advance();}
   assert.equal(s.phase,'countdown');assert.equal(bot.chips[first],1);assert.equal(bot.chips['siren-crown'],1);results.push({...bot.chips});
  }finally{s.dispose();}
 }
 assert.deepEqual(results[0],results[1]);
 const s=mixed(true);try{
  const bot=s.players.get('bot')!;bot.offers=['siren-crown'];s.startDraft(['bot']);assert.deepEqual(bot.chips,{});assert.equal(bot.selected,false,'instant selection must not bypass Siren eligibility');
 }finally{s.dispose();}
});

test('both-draft rules keep human choices and skip only connected eligible bots',()=>{
 const s=new Simulation({skipBotDrafts:true,bothDraft:true,roundsPerSet:1});try{
  const a=s.addPlayer('a')!,bot=s.addPlayer('bot','Bot','ranged','normal')!,gone=s.addPlayer('gone','Gone','ranged','normal')!;
  s.phase='fight';s.disconnect(gone.id);gone.hp=0;bot.hp=0;s.resolveRound();s.time=s.phaseEnd;s.advance();
  assert.equal(s.phase,'draft');assert.equal(s.draft!.player,a.id);assert.equal(Object.values(bot.chips).reduce((x,y)=>x+y,0),1);assert.deepEqual(gone.chips,{});
  assert.equal(a.selected,false);assert.ok(a.offers.length);assert.equal(s.draft!.chosen,undefined);
 }finally{s.dispose();}
});
