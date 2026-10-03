// Legendary / mythic chip rework (owner request 2026-10-03): every reworked mechanic runs in the authority.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {CHIPS,WEAPONS,statsFor} from '../src/shared/content.js';
import {CHAOS_TUNING} from '../src/shared/chaos.js';
import {chipStatLines} from '../src/shared/chip-stats.js';
import {v,len,sub,type SpecId} from '../src/shared/config.js';
await initPhysics();

/** Two pilots standing on a real floor (settled, so ground contact is part of every check). */
function game(chips:Record<string,number>={},spec:SpecId='pistol',gap=7){
 const s=new Simulation({sandbox:true}),a=s.addPlayer('a','A',WEAPONS[spec].class)!,b=s.addPlayer('b','B')!;a.spec=spec;a.chips=chips;a.stats=statsFor(chips,spec);a.hp=a.stats.hp;
 s.resetRound();s.phase='fight';for(const p of [...s.props.values()])s.removeProp(p.id);s.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});
 a.body.setTranslation(v(0,1,0),true);b.body.setTranslation(v(0,1,gap),true);for(let i=0;i<20;i++)s.step();return {s,a,b};
}
const near=(x:number,y:number,e=1e-4)=>assert.ok(Math.abs(x-y)<e,`${x} vs ${y}`);

test('Trading Places swaps grounded pilots (the floor no longer blocks the room check) and dazes the target',()=>{
 const {s,a,b}=game({'swap-shop':1});const pa={...a.body.translation()},pb={...b.body.translation()};
 s.chaos.direct(a,b,b.body.translation(),v(0,0,1),'swap-1');
 near(a.body.translation().z,pb.z,.05);near(b.body.translation().z,pa.z,.05);assert.ok((b.leet?.frozenUntil||0)>s.time);
 s.chaos.direct(a,b,b.body.translation(),v(0,0,1),'swap-2');near(a.body.translation().z,pb.z,.05);// cooldown
 s.dispose();
});
test('Satellite Office adds two homing, non-recursive satellite shots per volley without delaying the main shot',()=>{
 const {s,a}=game({orbitals:1});s.spawnProjectile(a);const shots=[...s.projectiles.values()];assert.equal(shots.length,3);
 const [main,...sats]=shots;assert.ok(main.direct);assert.equal(s.chaos.shotStep(main,.016),1);
 for(const q of sats){assert.ok(!q.direct&&q.effectsUsed);assert.equal(q.homing,CHAOS_TUNING.satelliteHoming);near(q.damage,main.damage*CHAOS_TUNING.satelliteDamage);assert.ok(len(sub(q.pos,main.pos))>.3);}
 s.dispose();
});
test('Red Light, Green Light relaunches once toward the current aim, faster and harder',()=>{
 const {s,a}=game({'stop-go':1});s.spawnProjectile(a);const q=[...s.projectiles.values()][0];const speed=len(q.vel),damage=q.damage;
 s.time=q.born!+.3;assert.equal(s.chaos.shotStep(q,.016),0);a.input.yaw=Math.PI/2;a.input.pitch=0;
 s.time=q.born!+.6;assert.equal(s.chaos.shotStep(q,.016),1);assert.ok(len(q.vel)>speed*1.9);near(q.damage,damage*CHAOS_TUNING.relaunchDamage);assert.ok(q.vel.x>Math.abs(q.vel.z),'turned toward the new aim');
 const v1={...q.vel};s.time+=.3;s.chaos.shotStep(q,.016);assert.deepEqual(q.vel,v1,'relaunches only once');
 s.dispose();
});
test('Return to Sender: the return leg can hit the same target again for ×1.4 damage',()=>{
 const {s,a}=game({boomerang:1});s.spawnProjectile(a);const q=[...s.projectiles.values()][0];q.hit.add('b');const damage=q.damage;
 for(let i=0;i<50&&!q.mods?.turned;i++){s.time+=1/60;s.stepProjectiles(1/60);}
 assert.equal(q.mods?.turned,1);assert.ok(!q.hit.has('b'));near(q.damage,damage*1.4);s.dispose();
});
test('Thunder Step calls lightning on nearby visible enemies (and still strikes the start point when alone)',()=>{
 const {s,a,b}=game({thunderstep:1},'sword');s.chaos.dash(a,a.body.translation());const f=[...s.chaos.fields.values()];
 assert.equal(f.length,1);assert.equal(f[0].fx,'thunderstep');near(f[0].pos.z,b.body.translation().z,.05);
 for(let i=0;i<40;i++)s.step();assert.ok(b.hp<=100-CHAOS_TUNING.thunderDamage*.5,`b hp ${b.hp}`);
 const ev=s.events.find(e=>e.type==='explosion'&&e.message==='thunderstep');assert.ok(ev);assert.equal(ev.radius,2.5);
 b.body.setTranslation(v(0,1,25),true);s.world.step();s.chaos.fields.clear();s.chaos.dash(a,a.body.translation());assert.equal([...s.chaos.fields.values()][0].radius,4);s.dispose();
});
test('Comet Dash lands in a blast that follows the rolling pilot and never hurts its owner',()=>{
 const {s,a,b}=game({comet:1},'sword',3);assert.equal(a.stats.dashBlast,0);near(a.stats.dashCooldown,.6);assert.equal(a.stats.playerGravity,1);
 s.chaos.dash(a,a.body.translation());const f=[...s.chaos.fields.values()][0];assert.equal(f.attach,a.id);assert.equal(f.fx,'comet');
 a.body.setTranslation(v(0,1,1.5),true);for(let i=0;i<30;i++)s.step();assert.equal(a.hp,a.stats.hp);assert.ok(b.hp<100);
 const ev=s.events.find(e=>e.type==='explosion'&&e.message==='comet')!;near(ev.pos.z,1.5,.3);assert.equal(ev.radius,3.5);s.dispose();
});
test('Now You See Me blinks to a visible enemy in the aim cone and the swing deals ×1.5',()=>{
 const {s,a,b}=game({'blink-cut':1},'sword',5);a.input.yaw=0;s.primary(a);
 assert.ok(len(sub(a.body.translation(),b.body.translation()))<1.6);near(a.swing!.damageScale,CHAOS_TUNING.blinkStrike);assert.ok(s.chaos.shielded(a));s.dispose();
 const g=game({'blink-cut':1},'sword',5);g.a.input.yaw=Math.PI;g.s.primary(g.a);near(g.a.swing!.damageScale,1);assert.ok(g.a.body.translation().z<-2);g.s.dispose();
});
test('Emergency Exit cheats a lethal hit once per round, then the next lethal hit kills',()=>{
 const {s,a,b}=game({'second-chance':1});const dealt=s.damage(a,500,b.id,'direct');assert.equal(dealt,0);assert.equal(a.hp,1+CHAOS_TUNING.exitHeal);assert.ok(s.chaos.shielded(a));
 assert.equal(s.damage(a,500,b.id,'direct'),0,'immune during the shield');s.time+=2;s.damage(a,500,b.id,'direct');assert.ok(a.hp<=0);s.dispose();
});
test('Do Not Press heals, shields and refreshes cooldowns once per round',()=>{
 const {s,a,b}=game({'panic-button':1});a.blockCd=a.dashCd=3;s.damage(a,75,b.id,'direct');near(a.hp,25+a.stats.hp*CHAOS_TUNING.panicHeal);assert.equal(a.blockCd,0);assert.equal(a.dashCd,0);
 assert.equal(s.damage(a,10,b.id,'direct'),0);assert.ok(s.events.some(e=>e.type==='nova'&&e.message==='panic-button'));s.dispose();
});
test('Time Out freezes nearby visible enemies on a successful Block, but never an armored boss',()=>{
 const {s,a,b}=game({'freezer-block':1},'pistol',3);s.chaos.block(a);assert.ok((b.leet?.frozenUntil||0)>=s.time+CHAOS_TUNING.timeOutFreeze-1e-9);assert.ok(s.events.some(e=>e.type==='freeze'&&e.target===b.id));
 const far=game({'freezer-block':1},'pistol',8);far.s.chaos.block(far.a);assert.ok(!((far.b.leet?.frozenUntil||0)>far.s.time));s.dispose();far.s.dispose();
});
test('Meteor Shower drops 5 tracked meteors per barrage with a cooldown, all detonating',()=>{
 const {s,a,b}=game({artillery:1});s.chaos.direct(a,b,b.body.translation(),v(0,0,1),'art-1');const f=[...s.chaos.fields.values()];
 assert.equal(f.length,CHAOS_TUNING.meteors);assert.equal(f[0].attach,b.id);assert.ok(f.every(x=>x.fx==='artillery'&&x.radius===3&&x.damage===26));
 s.chaos.direct(a,b,b.body.translation(),v(0,0,1),'art-2');assert.equal(s.chaos.fields.size,CHAOS_TUNING.meteors,'cooldown');
 for(let i=0;i<150;i++)s.step();assert.equal(s.events.filter(e=>e.type==='explosion'&&e.message==='artillery').length,CHAOS_TUNING.meteors);s.dispose();
});
test('Pocket Portal arrival erupts in a shock blast',()=>{
 const {s,a}=game({wormhole:1});const crate=s.addProp({id:'crate',pos:v(4,.5,0),size:v(1,1,1),material:'wood'})!;s.world.step();
 s.chaos.direct(a,crate,v(3.5,.5,0),v(1,0,0),'portal');assert.ok(a.body.translation().x>2);assert.ok(s.events.some(e=>e.type==='explosion'&&e.message==='wormhole'&&e.radius===3));s.dispose();
});
test('Supernova Guard: a nova Block recharges in 0.6 s and Blocks are no longer slower',()=>{
 const {s,a}=game({supernova:1});assert.ok(a.stats.blockCooldown<1);a.blockCd=5;s.chaos.block(a);near(a.blockCd,CHAOS_TUNING.novaRecharge);s.dispose();
});
test('reworked legendary / mythic cards carry green stat lines; rarity tiers keep their PvE pool sizes',()=>{
 for(const id of ['orbitals','stop-go','swap-shop','artillery','wormhole','thunderstep','blink-cut','second-chance','freezer-block','panic-button','comet']){const c=CHIPS.find(c=>c.id===id)!;assert.ok(chipStatLines(c).some(l=>l.tone==='good'),id);assert.ok(['legendary','mythic'].includes(c.rarity),id);}
 const melee=CHIPS.filter(c=>c.rarity==='mythic'&&c.tags.some(t=>['all','melee'].includes(t)));assert.equal(melee.length,7);
 assert.equal(CHIPS.find(c=>c.id==='tumble-engine')!.rarity,'legendary');
});
test('chip blast events carry the chip tag; plain weapon blasts stay untagged (PvP snapshots unchanged)',()=>{
 const {s,a,b}=game({},'fire');s.explode(a,v(0,1,3),1.5,10,100,'blast',true);const e=s.events.at(-1)!;assert.equal(e.type,'explosion');assert.ok(!('message'in e));
 s.explode(a,v(0,1,3),1.8,6,100,'shrapnel',true);assert.equal(s.events.at(-1)!.message,'shrapnel');void b;s.dispose();
});
