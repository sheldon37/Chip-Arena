import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {Simulation,initPhysics,type PlayerView} from '../src/shared/simulation.js';
import {v,type SpecId} from '../src/shared/config.js';
import {HOST_CAPS,kitFor,abilityCooldown,adsProfile} from '../src/shared/abilities.js';
import {statsFor} from '../src/shared/content.js';
import {abilitySlots,abilitySlotsHtml,aimState,scopeHtml,crosshairHtml,abilityTags,abilityScreen,previewOn} from '../src/client/ability-hud.js';
import {previewCircles,worldRay} from '../src/client/ability-preview.js';
import {Predictor} from '../src/client/prediction.js';
import {controlHints} from '../src/client/control-hints.js';

// PRESENTATION (design §11, §16.2 P): HUD slots and markup, aim profiles, the ground preview's parity with the server's
// aimPoint, remote teleport snaps, ability audio cues and the main.ts pins.
await initPhysics();
const view=(spec:SpecId,patch:Partial<PlayerView>={})=>{const s=new Simulation({sandbox:true});try{s.addPlayer('me','Me','ranged',null,'#fff',{});const p=s.players.get('me')!;p.spec=spec;s.resetRound();s.phase='fight';return {...s.snapshot().players[0],...patch} as PlayerView;}finally{s.dispose();}};
/** Mark a kit slot live for the duration of `fn` (G/M set `live` in the kit rows; F leaves it unset). */
function live<R>(specs:SpecId[],fn:()=>R):R{const defs=specs.flatMap(s=>[kitFor(s)!.q,kitFor(s)!.e]),old=defs.map(d=>d.live);defs.forEach(d=>d.live=true);try{return fn();}finally{defs.forEach((d,i)=>d.live=old[i]);}}

test('abilitySlots: caps gating, names, cooldown/casting/no-mana states, charges and the effective mana cost',()=>{
 const fire=view('fire');
 assert.deepEqual(abilitySlots(fire,0,{}),[],'no caps (old host): main.ts keeps the legacy E card');
 assert.deepEqual(abilitySlots(view('sword'),0,HOST_CAPS),[],'melee has no ability cards');
 assert.deepEqual(abilitySlots(view('emperor'),0,HOST_CAPS),[],'DEV classes keep their own HUD');
 live(['fire','pistol','rifle'],()=>{
  const [q,e]=abilitySlots(fire,5,HOST_CAPS);
  assert.equal(q.short,'LANCE');assert.equal(e.short,'METEOR');assert.equal(q.state,'ready');assert.equal(e.cost,35);
  const stats=statsFor(fire.chips,'fire'),max=abilityCooldown(kitFor('fire')!.e,stats);
  const cooling=abilitySlots({...fire,secondaryCd:max/2,lastSecondary:4},5,HOST_CAPS)[1];
  assert.equal(cooling.state,'cooldown');assert.ok(Math.abs(cooling.fraction-.5)<1e-9);
  assert.equal(abilitySlots({...fire,mana:10},5,HOST_CAPS)[1].state,'nomana');assert.equal(abilitySlots({...fire,mana:10},5,HOST_CAPS)[1].affordable,false);
  assert.equal(abilitySlots({...fire,lastSecondary:4.9,abilityCastAt:5.1},5,HOST_CAPS)[1].state,'casting');
  assert.deepEqual(abilitySlots({...fire,chips:{'twin-sigil':1},eCharges:1},5,HOST_CAPS)[1].charges??{n:1,max:2},{n:1,max:2});
  const [aim]=abilitySlots(view('pistol'),5,HOST_CAPS);assert.equal(aim.mode,'hold');assert.equal(aim.short,'AIM');assert.equal(abilitySlots(view('rifle'),5,HOST_CAPS)[0].short,'SCOPE');
  assert.equal(abilitySlots(view('pistol',{adsSince:4}),5,HOST_CAPS)[0].steady,true);assert.equal(abilitySlots(view('pistol',{adsSince:4.95}),5,HOST_CAPS)[0].steady,false);
 });
});

test('HUD markup: ability cards carry the dial fraction, state, pips and mana; gun Q reads HOLD / AIMING / STEADY',()=>live(['fire','pistol'],()=>{
 const html=abilitySlotsHtml(abilitySlots({...view('fire'),mana:10,secondaryCd:5},5,HOST_CAPS));
 assert.match(html,/class="cooldown ability" data-slot="Q" data-state="nomana" style="--p:1\.000"><small>Q · LANCE<\/small>/);
 assert.match(html,/data-slot="E" data-state="nomana" style="--p:0\.\d+"><small>E · METEOR<\/small>.*<small>35 mana<\/small>/);
 assert.match(abilitySlotsHtml([{key:'E',name:'x',short:'FRAG',mode:'press',state:'ready',remaining:0,max:8,fraction:1,affordable:true,charges:{n:1,max:2}}]),/<b class="pips">◆◇<\/b>/);
 const gun=(p:Partial<PlayerView>)=>abilitySlotsHtml(abilitySlots(view('pistol',p),5,HOST_CAPS));
 assert.match(gun({}),/Q · AIM<\/small><strong class="ready">HOLD/);assert.match(gun({adsSince:4.99}),/AIMING/);assert.match(gun({adsSince:4}),/STEADY/);
 const css=readFileSync(new URL('../src/client/match-hud.css',import.meta.url),'utf8');
 assert.match(css,/\.cooldown\.ability:before\{[^}]*conic-gradient\(var\(--fill,#59dd83\) calc\(var\(--p\)\*1turn\)/,'radial recharge dial');
 assert.match(css,/@media\(max-width:580px\)\{\.cooldowns\{flex-wrap:wrap/,'four cards wrap 2×2 at 580 px');
}));

test('aim profiles: caps-gated ADS per gun, today\'s rifle scope without caps, sensitivity factors, overlay and crosshair',()=>{
 assert.deepEqual(aimState({},'rifle',true),{scoped:true,profile:null,sens:.36},'old host: the client-only rifle scope');
 assert.deepEqual(aimState({},'pistol',true),{scoped:false,profile:null,sens:1},'old host: no pistol ADS');
 assert.equal(aimState(HOST_CAPS,'rifle',false).scoped,false);assert.equal(aimState(HOST_CAPS,'fire',true).scoped,false,'magic Q is a spell, not an aim');
 for(const spec of ['pistol','machinegun','rifle'] as const){const a=aimState(HOST_CAPS,spec,true),p=adsProfile(spec)!;assert.equal(a.profile,p);assert.equal(a.sens,p.sens);
  // Mouse factor ≈ tan(fov/2)/tan(37.5°); the rifle keeps today's .36 scope factor.
  if(spec==='rifle')assert.equal(p.sens,.36);else assert.ok(Math.abs(p.sens-Math.tan(p.fov*Math.PI/360)/Math.tan(37.5*Math.PI/180))<.03,spec+' sensitivity follows the FOV');}
 assert.match(scopeHtml(aimState({},'rifle',true)),/3× · RIFLE<br>RELEASE Q TO UNSCOPE/,'legacy scope text');
 assert.match(scopeHtml(aimState(HOST_CAPS,'rifle',true)),/3× SCOPE · RELEASE Q/);assert.equal(scopeHtml(aimState(HOST_CAPS,'pistol',true)),'');
 assert.equal(crosshairHtml(view('pistol'),5,aimState(HOST_CAPS,'pistol',false)),'<div class="crosshair "></div>');
 const settled=crosshairHtml(view('pistol',{adsSince:4}),5,aimState(HOST_CAPS,'pistol',true));assert.match(settled,/class="crosshair ads" style="--settle:1\.00;--bloom:0\.00"/);
 // The HUD redraws at 10 Hz while the MG fires every .085 s: the burst is tracked from the lastShot timestamps.
 const mg=view('machinegun',{adsSince:3}),bloomAt=(t:number,shot:number)=>Number(/--bloom:([\d.]+)/.exec(crosshairHtml({...mg,lastShot:shot},t,aimState(HOST_CAPS,'machinegun',true)))![1]);
 let bloom=0;for(let t=4;t<=4.55;t+=.1)bloom=bloomAt(t,4+Math.floor((t-4)/.085)*.085);assert.ok(bloom>.6,'sustained aimed MG fire blooms the crosshair: '+bloom);
 assert.equal(bloomAt(5.2,4.51),0,'bloom resets .25 s after the last shot');assert.ok(bloomAt(5.3,5.3)<.2,'a new burst starts tight');
});

test('status badges and first-person tints: SLOWED, SHACKLED, MARKED (edge pulse from the lock)',()=>{
 const me=view('pistol'),base=new Simulation({sandbox:true});const snap=base.snapshot();base.dispose();const s={...snap,time:5};
 assert.match(abilityTags({...me,slowed:{factor:.6,until:6,kind:'tidal-wave'}},s),/status-slow">SLOWED 1\.0s/);
 assert.match(abilityTags({...me,slowed:{factor:.4,until:5.5,kind:'shackles'}},s),/status-shackles">SHACKLED 0\.5s/);
 assert.equal(abilityTags({...me,slowed:{factor:.4,until:4,kind:'shackles'}},s),'','expired slows show nothing');
 const field={id:'f',owner:'x',kind:'bomb' as const,pos:v(0,0,0),radius:3,born:4.5,until:5.6,arm:0,damage:85,attach:me.id,fx:'orbital-mark',lockAt:5.4};
 assert.match(abilityTags(me,{...s,fields:[field]}),/status-marked">MARKED 0\.6s/);
 assert.match(abilityScreen(me,{...s,fields:[field]},false),/dot-screen marked"/);assert.match(abilityScreen(me,{...s,time:5.45,fields:[field]},false),/dot-screen marked lock"/);
 assert.match(abilityScreen({...me,slowed:{factor:.4,until:6,kind:'shackles'}},s,true),/dot-screen shackles"/);assert.equal(abilityScreen({...me,slowed:{factor:.6,until:6,kind:'soak'}},s,false),'');
});

test('ground preview: the client point equals the server aimPoint within .1 m on three props (frag, Meteor, Geyser Wall, Black Hole)',()=>live(['pistol','fire','water','darkness'],()=>{
 const sim=new Simulation({sandbox:true});
 try{
  sim.addPlayer('me','Me','ranged',null,'#fff',{});const p=sim.players.get('me')!;sim.resetRound();sim.phase='fight';for(const prop of [...sim.props.values()])sim.removeProp(prop.id);
  sim.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});
  sim.addProp({id:'wall',pos:v(0,2,14),size:v(8,4,1),material:'stone'});sim.addProp({id:'deck',pos:v(-9,1,8),size:v(5,2,5),material:'wood'});sim.addProp({id:'crate',pos:v(7,.75,6),size:v(1.5,1.5,1.5),material:'metal'});
  p.body.setTranslation(v(0,1.2,0),true);for(let i=0;i<30;i++)sim.step(1/60);
  // The online client's ray: the Predictor's mirrored prop colliders (reconciled from a snapshot), its own capsule excluded.
  const snap=sim.snapshot(),pr=new Predictor({abilities:true});pr.reconcile(snap,snap.players[0]);pr.world.step();const ray=worldRay(pr.world,pr.collider);let checked=0;
  for(const spec of ['pistol','fire','water','darkness'] as const){p.spec=spec;const def=kitFor(spec)!.e,ground=spec!=='pistol';
   for(const camera of ['first','third'] as const)for(const [yaw,pitch] of [[0,-.05],[0,.12],[-.6,-.2],[.75,-.12],[.4,.6],[0,-.6]]){
    const me={...sim.snapshot().players.find(x=>x.id==='me')!,yaw,pitch},pos=p.body.translation();
    const server=sim.abilities.aimPoint(p,{yaw,pitch,camera},def.range,{ground}),client=previewCircles(me,pos,yaw,pitch,camera,ray);
    const centre=client[spec==='water'?1:0];assert.equal(client.length,spec==='water'?3:1);
    const d=Math.hypot(centre.pos.x-server.point.x,centre.pos.y-server.point.y,centre.pos.z-server.point.z);
    assert.ok(d<=.1,`${spec} ${camera} yaw ${yaw} pitch ${pitch}: preview ${JSON.stringify(centre.pos)} vs server ${JSON.stringify(server.point)} (${d.toFixed(3)} m)`);
    assert.equal(centre.clamped,server.clamped,`${spec} ${camera} ${yaw}/${pitch} clamped flag`);checked++;}
  }
  assert.equal(checked,48);
  // Radius is the gameplay radius with the pilot's stats; Geyser Wall shows three circles 3 m apart across the aim.
  p.spec='water';const g=previewCircles(sim.snapshot().players[0],p.body.translation(),0,-.2,'first',ray);assert.ok(Math.abs(Math.hypot(g[2].pos.x-g[0].pos.x,g[2].pos.z-g[0].pos.z)-6)<1e-6);assert.equal(g[0].radius,2.5);
  p.spec='darkness';assert.equal(previewCircles({...sim.snapshot().players[0],chips:{'abyssal-core':1}},p.body.translation(),0,-.2,'first',ray)[0].radius,7.5);
  p.spec='rifle';assert.deepEqual(previewCircles(sim.snapshot().players[0],p.body.translation(),0,-.2,'first',ray),[],'Orbital Mark is a dart, not a ground target');
  // Shown while the live E is ready or casting; never for Necronomicon's Raise the Dead or without caps.
  const fire={...view('fire')};assert.equal(previewOn(fire,5,HOST_CAPS),true);assert.equal(previewOn({...fire,secondaryCd:3},5,HOST_CAPS),false);assert.equal(previewOn(fire,5,{}),false);assert.equal(previewOn({...fire,chips:{necronomicon:1}},5,HOST_CAPS),false);pr.dispose();
 }finally{sim.dispose();}
}));

test('control hints: kit names on a capable host, today\'s hints without caps',()=>live(['fire','pistol','rifle'],()=>{
 assert.match(controlHints('fire','KeyQ',HOST_CAPS),/<kbd>Q<\/kbd>Flame Lance/);assert.match(controlHints('fire','KeyQ',HOST_CAPS),/<kbd>E<\/kbd>Meteor Strike/);
 assert.match(controlHints('pistol','KeyQ',HOST_CAPS),/<kbd>Q<\/kbd>Aim/);assert.match(controlHints('rifle','KeyQ',HOST_CAPS),/<kbd>Q<\/kbd>Scope/);
 assert.equal(controlHints('rifle','KeyQ',{}),controlHints('rifle'));assert.doesNotMatch(controlHints('pistol'),/<kbd>Q</);
}));

test('main.ts pins: PvE idle input prefix, latched F, no mouse look during holds, ADS eye camera, caps-gated cards and hints',()=>{
 const main=readFileSync(new URL('../src/client/main.ts',import.meta.url),'utf8');
 assert.match(main,/function input\(\):Input\{const tapF=pendingInteract;pendingInteract=false;if\(pveIdle\(\)\)return \{\.\.\.idleInput\(\)/);
 assert.match(main,/interact:\(keys\.has\('KeyF'\)\|\|tapF\)/);assert.match(main,/!state\?\.pve\?\.hold\)\{yaw-=e\.movementX\*\.0021\*pref\.sensitivity\*aim\(\)\.sens/);
 assert.match(main,/return \{camera:aim\(\)\.profile\?'first':pref\.camera,/,'ADS sends the eye camera, so the server aims from what the player sees');
 assert.match(main,/electric:keys\.has\('KeyQ'\)/,'Emperor keeps Q');assert.match(main,/abilitySlotsHtml\(abilitySlots\(me,state\.time,hostCaps\)\)\|\|`<div class="cooldown"><small>E · SPECIAL<\/small>/,'legacy E card without caps');
 assert.match(main,/controlHints\(getMe\(\)\?\.spec,pref\.sirenToggleKey,hostCaps\)/);assert.equal((main.match(/hud\.innerHTML=/g)||[]).length,1);
});
