import {test} from 'node:test';import assert from 'node:assert/strict';import {readFileSync} from 'node:fs';
import {Simulation,initPhysics,sanitizeInput} from '../src/shared/simulation.js';
import {cameraPosition,weaponMuzzle,VIEW} from '../src/shared/view.js';
import {refreshDot,advanceDots,DotState} from '../src/shared/elements.js';
import {statsFor,eligibleChips,WEAPONS} from '../src/shared/content.js';
import {v,idleInput,SpecId} from '../src/shared/config.js';
import {AUDIO_CLIPS,AudioCursor} from '../src/client/audio.js';
await initPhysics();
function arena(spec:SpecId='pistol'){
 const s=new Simulation({sandbox:true});const a=s.addPlayer('a','A',WEAPONS[spec].class)!,b=s.addPlayer('b')!;s.phase='fight';a.spec=spec;a.ammo=WEAPONS[spec].ammo;
 for(const prop of [...s.props.values()])s.removeProp(prop.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(30,1,30),material:'foundation'});a.body.setTranslation(v(0,1,0),true);b.body.setTranslation(v(0,1,4),true);s.world.step();return {s,a,b};
}
test('camera preference is a validated intention; server reconstructs eyes and colliding third-person camera',()=>{
 const raw={...idleInput(),seq:1,camera:'first'};assert.equal(sanitizeInput(raw)!.camera,'first');assert.equal(sanitizeInput({...raw,camera:{x:90,y:90,z:90}})!.camera,'third');
 assert.deepEqual(cameraPosition(v(1,2,3),0,0,'first'),v(1,2+VIEW.eyeHeight,3));
 const origin=v(0,1,0),camera=cameraPosition(origin,0,0,'third',()=>1);assert.ok(Math.hypot(camera.x,camera.y-1-VIEW.eyeHeight,camera.z)<1);
 assert.ok(weaponMuzzle(origin,0,()=>.4).z<.4);
});
for(const camera of ['first','third'] as const)test(`${camera} person aims from physical muzzle and cannot shoot through obstructing cover`,()=>{
 const {s,a,b}=arena();a.input={...idleInput(),camera};s.spawnProjectile(a);const initial=[...s.projectiles.values()][0];assert.ok(initial.pos.z<1);for(let n=0;n<10;n++)s.stepProjectiles(1/60);assert.equal(b.hp,77);
 s.projectiles.clear();b.hp=100;s.addProp({id:'muzzle-cover',pos:v(0,1.6,.52),size:v(2,2,.1),material:'foundation'});s.world.step();s.spawnProjectile(a);const blocked=[...s.projectiles.values()][0];assert.ok(blocked.pos.z<.47,'muzzle is moved behind cover');for(let n=0;n<20;n++)s.stepProjectiles(1/60);assert.equal(b.hp,100);assert.equal(s.projectiles.size,0);s.dispose();
});
test('fire and poison are compatible with every gun and melee weapon, additive, bounded and independent',()=>{
 for(const w of Object.values(WEAPONS)){const eligible=eligibleChips(w.id,{});assert.ok(eligible.some(c=>c.id==='ember'));assert.ok(eligible.some(c=>c.id==='poison'));}
 const stats=statsFor({ember:2,poison:3});assert.equal(stats.burn,6);assert.equal(stats.poison,7.5);assert.equal(statsFor({poison:99}).poison,15);
});
test('DoT refresh preserves cadence, strongest source and exact final tick; fire and poison expire independently',()=>{
 const fire=new Map<string,DotState>(),poison=new Map<string,DotState>();refreshDot(fire,'a',3,0,'fire');refreshDot(poison,'a',2.5,0,'poison');let burnDamage=0,poisonDamage=0;
 for(let n=1;n<=16;n++){const now=n*.25;advanceDots(fire,now,'fire',(_,amount)=>burnDamage+=amount);advanceDots(poison,now,'poison',(_,amount)=>poisonDamage+=amount);}
 assert.equal(burnDamage,6);assert.equal(poisonDamage,10);advanceDots(poison,4.01,'poison',()=>assert.fail());assert.equal(poison.size,0);
 const rapid=new Map<string,DotState>();refreshDot(rapid,'a',3,0,'fire');let total=0;for(let n=1;n<=60;n++){refreshDot(rapid,'a',n%2?1:3,n/60,'fire');advanceDots(rapid,n/60,'fire',(_,amount)=>total+=amount);}assert.equal(total,3,'60 hits per second must not postpone every DoT tick');assert.equal(rapid.get('a')!.dps,3);
});
for(const spec of ['pistol','rifle','machinegun','sword','daggers','shield','greatsword'] as SpecId[])test(`${spec} real attack applies both elemental DoTs, ticks are nonrecursive and reset clears them`,()=>{
 const {s,a,b}=arena(spec);a.chips={ember:1,poison:2,static:1};a.stats=statsFor(a.chips);if(WEAPONS[spec].class==='melee')b.body.setTranslation(v(0,1,1.5),true);s.world.step();
 s.setInput('a',{...idleInput(),camera:'first',seq:1,attack:true});for(let n=0;n<90&&b.hp===100;n++)s.step();
 assert.ok(b.burns.has('a')&&b.poisons.has('a'),`${spec} did not apply both statuses`);assert.equal(b.burns.get('a')!.dps,3);assert.equal(b.poisons.get('a')!.dps,5);assert.equal(s.snapshot().players[1].statuses.length,2);
 s.setInput('a',{...idleInput(),seq:2});s.projectiles.clear();a.swing=null;a.burst=0;const hp=b.hp,hits=a.hits;for(let n=0;n<65;n++)s.step();assert.ok(b.hp<hp-7.5);assert.equal(a.hits,hits,'ticks cannot trigger hit-count procs');assert.equal(b.lastSource,'a');
 s.resetRound();assert.equal(b.burns.size,0);assert.equal(b.poisons.size,0);assert.deepEqual(s.snapshot().players[1].statuses,[]);s.dispose();
});
test('projectile element metadata is authoritative; split fragments cannot reapply enchantments',()=>{
 const {s,a,b}=arena();a.chips={ember:1,poison:1,splitter:1};a.stats=statsFor(a.chips);s.spawnProjectile(a);assert.deepEqual(s.snapshot().projectiles[0].elements,['fire','poison']);for(let n=0;n<5;n++)s.stepProjectiles(1/60);const splits=[...s.projectiles.values()].filter(p=>!p.direct);assert.ok(splits.length>0);assert.ok(splits.every(p=>p.elements!.length===0));b.burns.clear();b.poisons.clear();for(const shot of splits){shot.hit.clear();shot.pos=v(0,1.5,3);shot.vel=v(0,0,80);}for(let n=0;n<5;n++)s.stepProjectiles(1/60);assert.equal(b.burns.size,0);assert.equal(b.poisons.size,0);s.dispose();
});
test('DoT kills retain responsible player attribution; restore removes both statuses',()=>{
 const {s,a,b}=arena();a.stats=statsFor({poison:1});b.hp=1.5;s.directHit(a,b,b.body.translation(),v(0,0,1),.1,0,'test');for(let n=0;n<90&&b.hp>0;n++)s.step();assert.equal(b.hp,0);assert.equal(b.lastSource,'a');assert.equal(a.roundWins,1);assert.equal(a.score,0);s.debug({action:'heal'},'a');assert.equal(b.poisons.size,0);s.dispose();
});
test('audio cursor deduplicates snapshots, skips stale events and accepts new events after explicit reset',()=>{
 const cursor=new AudioCursor();const events=[{id:1,time:0,type:'shot',pos:v()},{id:2,time:1,type:'hit',pos:v()}];assert.deepEqual(cursor.consume(events,1).map(e=>e.id),[2]);assert.equal(cursor.consume(events,1.1).length,0);cursor.reset();assert.equal(cursor.consume(events,1.1).length,1);
});
test('all bundled effects are valid audible PCM with peak headroom and distinct samples',()=>{
 const fingerprints=new Set<string>();let bytes=0;for(const name of AUDIO_CLIPS){const data=readFileSync(`public/audio/${name}.wav`);assert.equal(data.toString('ascii',0,4),'RIFF');assert.equal(data.toString('ascii',8,12),'WAVE');assert.equal(data.readUInt16LE(22),1);assert.equal(data.readUInt32LE(24),22050);assert.equal(data.readUInt16LE(34),16);assert.equal(data.readUInt32LE(40),data.length-44);let energy=0,peak=0;for(let i=44;i<data.length;i+=2){const x=data.readInt16LE(i)/32768;energy+=x*x;peak=Math.max(peak,Math.abs(x));}assert.ok(Math.sqrt(energy/((data.length-44)/2))>.015,name+' is silent');assert.ok(peak<.73);fingerprints.add(data.subarray(44,444).toString('base64'));bytes+=data.length;}assert.equal(fingerprints.size,AUDIO_CLIPS.length);console.log('Audio assets:',{clips:AUDIO_CLIPS.length,bytes});
});
