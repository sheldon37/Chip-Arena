import {test} from 'node:test';import assert from 'node:assert/strict';import * as T from 'three';
import {HostedRoom} from '../src/shared/host-room.js';import {initPhysics} from '../src/shared/simulation.js';import {cosmetics} from '../src/shared/cosmetics.js';import {effectiveIntro,introDuration} from '../src/shared/intros.js';import {createCharacterRig} from '../src/client/character-rig.js';import {teamArmor} from '../src/client/team-identity.js';import {TEAMS} from '../src/shared/teams.js';import {BlockCooldownTint} from '../src/client/block-cooldown-tint.js';
await initPhysics();
test('host starts one shared five-second return, may cancel, and guests cannot reset the room',()=>{
 const room=new HostedRoom('ABCDEFGH',{name:'Host',class:'melee',color:'#ffffff'},false,()=>{});try{room.join({name:'Guest'});room.sim.phase='fight';room.sim.options.sandbox=true;
 assert.equal(room.receive('guest',{type:'lobby'}),false);assert.equal(room.receive('host',{type:'lobby'}),true);const until=room.sim.snapshot().lobbyReturn!.until;assert.equal(until-room.sim.time,3);// owner request 2026-10-04: 3 s countdown (was 5 s)assert.equal(room.receive('host',{type:'lobby'}),false);assert.equal(room.sim.lobbyReturn!.until,until);
 assert.equal(room.receive('guest',{type:'cancel-lobby'}),false);assert.equal(room.receive('host',{type:'cancel-lobby'}),true);assert.equal(room.sim.lobbyReturn,undefined);assert.equal(room.sim.phase,'fight');
 room.receive('host',{type:'lobby'});room.sim.time=room.sim.lobbyReturn!.until-.03;room.step();assert.equal(room.sim.phase,'fight');room.step();assert.equal(room.sim.phase,'lobby');assert.equal(room.sim.lobbyReturn,undefined);assert.equal(room.sim.players.size,2);assert.equal(room.sim.players.get('host')!.ready,false);
 }finally{room.dispose();}
});
test('intro override is serialized, honors permissions, and can replace a dev weapon entrance',()=>{
 const p={spec:'emperor' as const,developer:true,unlocks:[],cosmetics:cosmetics({intro:'signal-spin',overrideWeaponIntro:true},true)};
 assert.equal(effectiveIntro(p),'signal-spin');assert.equal(introDuration(effectiveIntro(p)),4.8);p.cosmetics=cosmetics({...p.cosmetics,overrideWeaponIntro:false},true);assert.equal(effectiveIntro(p),'dev-emperor');
 p.cosmetics=cosmetics({intro:'neon-drop',overrideWeaponIntro:true},true);assert.equal(effectiveIntro(p),'neon-drop');
 const unauthorized={...p,developer:false,spec:'sword' as const,cosmetics:cosmetics({intro:'dev-emperor',overrideWeaponIntro:true})};assert.equal(effectiveIntro(unauthorized),'neon-drop');assert.equal(cosmetics(JSON.parse(JSON.stringify(p.cosmetics)),true).overrideWeaponIntro,true);
});
test('team bands preserve clear team colors through block cooldown and can be removed for free-for-all',()=>{
 const r=createCharacterRig('#fff'),tint=new BlockCooldownTint();teamArmor(r,'red');const band=r.chest.getObjectByName('team-identity') as T.Mesh<T.BoxGeometry,T.MeshBasicMaterial>;assert.equal(band.material.color.getHexString(),new T.Color(TEAMS.red.color).getHexString());tint.update(r.group,{hp:100,blockCd:2,blockTime:0},.3);assert.equal(band.material.color.getHexString(),new T.Color(TEAMS.red.color).getHexString());teamArmor(r,'blue');assert.equal(band.material.color.getHexString(),new T.Color(TEAMS.blue.color).getHexString());teamArmor(r);assert.equal(band.visible,false);tint.clear();
});

test('spectator cycles only connected survivors and resets on respawn',async()=>{
 const {Spectator}=await import('../src/client/spectator.js');const room=new HostedRoom('ABCDEFGH',{name:'Host',class:'melee',color:'#ffffff'},false,()=>{});try{room.join({name:'One'},false,'one');room.join({name:'Two'},false,'two');room.join({name:'Gone'},false,'gone');room.sim.phase='fight';room.sim.players.get('host')!.hp=0;room.sim.players.get('gone')!.connected=false;const state=room.sim.snapshot(),watch=new Spectator();watch.enabled=true;assert.equal(watch.update(state,'host')?.id,'one');watch.cycle(state,'host');assert.equal(watch.target,'two');watch.cycle(state,'host');assert.equal(watch.target,'one');watch.cycle(state,'host',-1);assert.equal(watch.target,'two');state.players.find(p=>p.id==='two')!.hp=0;assert.equal(watch.update(state,'host')?.id,'one');state.players.find(p=>p.id==='one')!.hp=0;assert.equal(watch.update(state,'host'),undefined);state.players[0].hp=100;watch.update(state,'host');assert.equal(watch.enabled,false);assert.equal(watch.target,undefined);}finally{room.dispose();}
});
