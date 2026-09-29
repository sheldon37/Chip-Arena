import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {HostedRoom} from '../src/shared/host-room.js';
import {cosmetics} from '../src/shared/cosmetics.js';
import {LOBBY_POSES,BOT_WEAPONS,preferredWeapon} from '../src/shared/lobby-presentation.js';
import {createCharacterRig} from '../src/client/character-rig.js';
import {animateLobbyPose,lobbyChair} from '../src/client/lobby-poses.js';
import {pilotCard,botLoadouts} from '../src/client/lobby-ui.js';
await initPhysics();
test('lobby pose and preferred weapon survive host/guest snapshots and preserve restricted weapon checks',()=>{
 const room=new HostedRoom('ABCD2345',{name:'Host',class:'ranged',color:'#ffaa66',cosmetics:cosmetics({lobbyPose:'chair',preferredSpec:'rifle'})},false,()=>{});
 try{room.join({name:'Guest',class:'melee',cosmetics:{lobbyPose:'sleep',preferredSpec:'daggers'}});const players=room.sim.snapshot().players;
  assert.equal(players[0].cosmetics?.lobbyPose,'chair');assert.equal(players[0].spec,'rifle');assert.equal(players[1].cosmetics?.lobbyPose,'sleep');assert.equal(players[1].spec,'daggers');
  assert.equal(cosmetics({lobbyPose:'__proto__',preferredSpec:'emperor'}).lobbyPose,'weapon');assert.equal(cosmetics({preferredSpec:'emperor'}).preferredSpec,undefined);
  assert.equal(cosmetics({preferredSpec:'emperor'},true).preferredSpec,'emperor');assert.equal(preferredWeapon('rifle','magic'),'fire');
 }finally{room.dispose();}
});
test('each configured bot keeps its chosen weapon through selection, rounds and rematches; malformed inputs cannot grant dev classes',()=>{
 const s=new Simulation({skipIntros:true});try{s.addPlayer('local');const selected=['rifle','greatsword','water','machinegun','daggers','lightning','shield'];
  s.configureLobby({botCount:7,botDifficulty:'hard',botWeapons:selected});const bots=()=>[...s.players.values()].filter(p=>p.bot);
  assert.deepEqual(bots().map(p=>p.spec),selected);assert.ok(bots().every(p=>!p.developer));
  s.configureLobby({wins:3});assert.deepEqual(bots().map(p=>p.spec),selected,'unrelated settings preserve loadouts');
  s.setReady('local',true);s.selectSpec('local','pistol');s.advance();assert.equal(s.phase,'countdown');assert.deepEqual(bots().map(p=>p.spec),selected);
  s.resetRound();s.phase='match';s.rematch();s.advance();assert.deepEqual(bots().map(p=>p.spec),selected);
  s.returnLobby();s.configureLobby({botCount:4,botWeapons:['emperor','__proto__',{},'random']});assert.ok(bots().every(p=>BOT_WEAPONS.some(w=>w.id===p.spec)));assert.deepEqual(s.options.botWeapons,['random','random','random','random']);
  s.configureLobby({botCount:1});assert.equal(s.players.size,2);assert.equal(s.options.botWeapons?.length,1);
 }finally{s.dispose();}
});
test('four idle poses animate independently of live combat, preserve the rig and use the chair seat height',()=>{
 const s=new Simulation();try{s.addPlayer('p');const p=s.snapshot().players[0],original=JSON.stringify(p),poses=new Set<string>();
  for(const pose of Object.keys(LOBBY_POSES) as (keyof typeof LOBBY_POSES)[]){const r=createCharacterRig(p.color);r.group.position.y=.88;
   animateLobbyPose(r,p,0,1/30,pose);const first=r.head.quaternion.clone();animateLobbyPose(r,p,1,1/30,pose);r.group.updateMatrixWorld(true);r.group.traverse(o=>assert.ok(o.matrixWorld.elements.every(Number.isFinite)));
   assert.ok(first.angleTo(r.head.quaternion)>0,'each pose has idle motion');assert.equal(r.leftElbow.position.length(),.27);assert.equal(r.rightKnee.position.length(),.25);assert.equal(r.weapon.visible,pose==='weapon');
   poses.add(JSON.stringify([r.body.position.toArray(),r.body.quaternion.toArray(),r.leftArm.quaternion.toArray()]));
   if(pose==='chair'){const seat=lobbyChair().getObjectByName('seat')!;assert.ok(Math.abs(r.pelvis.getWorldPosition(new T.Vector3()).y-.11-(seat.position.y+.045))<.045,'hips meet chair cushion');}
  }
  assert.equal(poses.size,4);assert.equal(JSON.stringify(p),original,'poses never change game state');
 }finally{s.dispose();}
});
test('lobby cards escape player text and seven bot selectors expose all ordinary weapons with distinct labels',()=>{
 const s=new Simulation();try{s.addPlayer('p');const p={...s.snapshot().players[0],name:'<img onerror=alert(1)>',id:'\" onclick=bad'};const html=pilotCard(p,'YOU');assert.ok(!html.includes('<img'));assert.ok(html.includes('&lt;img'));assert.ok(html.includes('data-pilot-preview="&quot; onclick=bad"'));
  const bots=botLoadouts(7,['rifle','water']);assert.equal((bots.match(/data-bot-weapon=/g)||[]).length,7);for(const weapon of BOT_WEAPONS)assert.ok(bots.includes(`value="${weapon.id}"`));assert.ok(bots.includes('value="rifle" selected'));assert.ok(!bots.includes('value="emperor"'));
 }finally{s.dispose();}
});
