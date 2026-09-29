import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {HostedRoom} from '../src/shared/host-room.js';
import {WEAPONS,CLASS_INFO} from '../src/shared/content.js';
import {framePortrait} from '../src/client/menu-portraits.js';
import {cosmetics} from '../src/shared/cosmetics.js';
import {LOBBY_POSES,BOT_WEAPONS,preferredWeapon} from '../src/shared/lobby-presentation.js';
import {createCharacterRig} from '../src/client/character-rig.js';
import {animateLobbyPose,lobbyChair} from '../src/client/lobby-poses.js';
import {pilotCard,botLoadouts,preferredWeaponOptions,lobbyDensity} from '../src/client/lobby-ui.js';
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

test('special pose weapons require developer access or the exact item grant, including saved preferences and snapshots',()=>{
 const specials=Object.values(WEAPONS).filter(w=>w.devOnly);
 for(const weapon of specials){
  for(const access of [false,{developer:false,unlocks:['intro:dev-emperor']},true,{developer:false,unlocks:['weapon:'+weapon.id]}]){
   const allowed=access===true||typeof access==='object'&&access.unlocks.includes('weapon:'+weapon.id);
   assert.equal(cosmetics({preferredSpec:weapon.id},access).preferredSpec,allowed?weapon.id:undefined);
   assert.equal(preferredWeapon(weapon.id,weapon.class,access),allowed?weapon.id:CLASS_INFO[weapon.class].default);
   const html=preferredWeaponOptions(weapon.class,weapon.id,access);
   assert.equal(html.includes(`value="${weapon.id}"`),allowed);
   assert.ok(html.includes(`value="${allowed?weapon.id:CLASS_INFO[weapon.class].default}" selected`));
   for(const special of specials.filter(w=>w.id!==weapon.id))assert.equal(html.includes(`value="${special.id}"`),access===true&&special.class===weapon.class,'an individual grant must not expose other special weapons');
   const sim=new Simulation();try{
    sim.addPlayer('p','Pilot',weapon.class,null,'#ff6b35',{preferredSpec:weapon.id},access);
    const player=sim.snapshot().players[0];assert.equal(player.spec,allowed?weapon.id:CLASS_INFO[weapon.class].default);assert.equal(player.cosmetics?.preferredSpec,allowed?weapon.id:undefined);
    sim.setClass('p',weapon.class);assert.equal(sim.snapshot().players[0].spec,player.spec);
   }finally{sim.dispose();}
  }
 }
});
test('portraits display only class names, use distinct style effects and shrink in populated rooms',()=>{
 const s=new Simulation();try{s.addPlayer('p');const base=s.snapshot().players[0];
  for(const weapon of Object.values(WEAPONS)){
   const html=pilotCard({...base,spec:weapon.id,class:weapon.class},'YOU');
   assert.ok(html.includes(`data-style="${weapon.id}"`));assert.ok(html.includes(`--style-color:#${weapon.color.toString(16).padStart(6,'0')}`));
   assert.ok(html.includes(`<span>${CLASS_INFO[weapon.class].name}</span>`));assert.ok(!html.includes(weapon.name));assert.ok(!html.includes('pilot-card-bottom'));
  }
  assert.deepEqual(Array.from({length:8},(_,i)=>lobbyDensity(i+1)),['large','large','medium','medium','compact','compact','compact','compact']);
  for(const pose of Object.keys(LOBBY_POSES) as (keyof typeof LOBBY_POSES)[])for(const aspect of [.6,1,2]){
   const camera=new T.OrthographicCamera();framePortrait(camera,pose,aspect);
   assert.equal(camera.position.x,0,'camera faces the front');assert.ok(camera.position.z>0);assert.ok(camera.right>=.95,'narrow portraits retain the full character width');assert.ok(camera.projectionMatrix.elements.every(Number.isFinite));
  }
 }finally{s.dispose();}
});
