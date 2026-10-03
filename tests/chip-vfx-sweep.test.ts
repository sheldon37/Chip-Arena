// Chip VFX sweep (design §19.2, stream V): the inventory of every chip phase and the guarantees behind the conversion.
// Owner (2026-10-03): "reverse grenade still use old effect" and "the impact circles on the ground with basic effects":
// every flat ring, ground disc and spark-only flash of a chip (all rarities) became a pooled volumetric voxel effect sized
// to the gameplay radius. Phases: aura (weapon/body modules), projectile (shot decorations), on-hit (event visuals),
// mark (fused warning), field (lingering zone), rally, minions, ability. Status `ok` = already 3D/voxel and kept,
// `converted` = changed by this sweep, `none` = no visual. DEV-tier chips (mishima, ansatsuken, l44t, siren) are DEV move
// visuals (emperor/arcade/leet/siren modules) and stay untouched by design. Weapon surface glows (impact-glows.ts) are a
// weapon effect, not a chip phase, and stay as they are.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import * as T from 'three';
import {CHIPS} from '../src/shared/content.js';
import type {FieldView} from '../src/shared/chaos.js';
import type {Snapshot} from '../src/shared/simulation.js';
import {ChaosVisuals,RIM_SPACING,TELEGRAPH,telegraphColor} from '../src/client/chaos-visuals.js';
import {ChipBlasts,BLAST_CAPACITY} from '../src/client/chip-blasts.js';
import {ChipEffects} from '../src/client/chip-effects.js';
import {EventVfx} from '../src/client/event-vfx.js';
import {PlayerStatusEffects} from '../src/client/status-effects.js';

const INVENTORY:Record<string,string>={
 'vital':'common|aura=ok body module',
 'overclock':'common|aura=ok weapon module',
 'dash':'common|aura=ok body module',
 'frame':'common|aura=ok body module',
 'stabilizer':'common|aura=ok body module',
 'ricochet':'uncommon|aura=ok weapon module; projectile=ok decoration; on-hit=converted voxel impact (ricochet)',
 'splitter':'epic|aura=ok weapon module; projectile=ok decoration; on-hit=converted voxel impact (split)',
 'piercing':'rare|aura=ok weapon module; projectile=ok decoration',
 'ember':'uncommon|projectile=ok decoration; on-hit=ok status flames/bubbles',
 'poison':'uncommon|projectile=ok decoration; on-hit=ok status flames/bubbles',
 'static':'rare|aura=ok weapon module; on-hit=converted shock blast, extra beams removed (static)',
 'reach':'common|aura=ok weapon module',
 'kinetic':'common|aura=ok weapon module; projectile=ok decoration; on-hit=converted voxel impact (kinetic)',
 'demolition':'common|aura=ok weapon module; on-hit=converted voxel impact (demolition)',
 'shrapnel':'rare|aura=ok weapon module; projectile=ok decoration; on-hit=converted fire blast, flat ring and extra burst removed (shrapnel)',
 'salvage':'common|aura=ok body module',
 'multishot':'rare|aura=ok weapon module',
 'echo-strike':'epic|aura=ok weapon module',
 'magazine':'common|aura=ok weapon module',
 'mana':'common|aura=ok body module',
 'stamina':'common|aura=ok body module',
 'velocity':'common|aura=ok weapon module; projectile=ok decoration',
 'homing':'epic|aura=ok weapon module; projectile=ok decoration',
 'fire-rate':'common|aura=ok weapon module',
 'cast-rate':'common|aura=ok weapon module',
 'swing-rate':'common|aura=ok weapon module',
 'block-recharge':'common|aura=ok body module',
 'block-window':'common|aura=ok body module',
 'block-return':'uncommon|aura=ok body module',
 'block-pulse':'rare|aura=ok body module; on-hit=converted nova/kinetic blast, flat rings and burst removed (blockPulse)',
 'fleet':'common|none=stat only',
 'hard-hitter':'common|none=stat only',
 'quick-loader':'common|none=stat only',
 'efficient':'common|none=stat only',
 'footwork':'common|none=stat only',
 'spring':'common|none=stat only',
 'focus':'common|none=stat only',
 'second-wind':'common|none=stat only',
 'broadside':'uncommon|aura=ok weapon module',
 'dense-core':'uncommon|aura=ok weapon module; projectile=ok decoration',
 'feather':'uncommon|none=stat only',
 'lobber':'rare|none=stat only',
 'banker':'rare|projectile=ok decoration; on-hit=converted voxel impact (ricochet)',
 'buckshot':'epic|aura=ok weapon module',
 'leech':'rare|projectile=ok decoration',
 'executioner':'epic|none=stat only',
 'brawler':'uncommon|none=stat only',
 'marksman':'rare|none=stat only',
 'backstab':'rare|none=stat only',
 'thorns':'rare|aura=ok body module',
 'repair':'uncommon|none=stat only',
 'med-loader':'rare|none=stat only',
 'last-word':'epic|none=stat only',
 'block-medic':'uncommon|aura=ok body module',
 'block-loader':'rare|aura=ok body module',
 'rebound':'uncommon|aura=ok body module; on-hit=converted voxel impact (rebound)',
 'bulldozer':'rare|aura=ok weapon module; projectile=ok decoration',
 'anchor':'uncommon|aura=ok body module',
 'blast-dash':'epic|on-hit=converted kinetic blast, extra burst removed (dashBlast)',
 'sweeper':'uncommon|none=stat only',
 'moonboots':'epic|none=stat only',
 'kickback':'rare|none=stat only',
 'lucky':'rare|on-hit=converted critical voxel impact (critical)',
 'vacuum':'legendary|aura=ok weapon module; projectile=ok decoration; on-hit=converted void blast, flat rings and burst removed (vacuum)',
 'pong':'mythic|aura=ok weapon module; projectile=ok decoration; on-hit=converted voxel impact (ricochet); rally=converted paddle flash + streak (ricochet rally contract)',
 'boomerang':'legendary|aura=ok weapon module; projectile=ok decoration; on-hit=converted voxel impact (catch)',
 'zigzag':'epic|projectile=ok decoration',
 'chaos':'mythic|projectile=ok chaos transforms; on-hit=converted critical voxel impact (critical)',
 'glass':'legendary|none=stat only',
 'fortress':'legendary|aura=ok body module',
 'meteor':'mythic|aura=ok weapon module; projectile=ok decoration; on-hit=converted fire blast, flat ring and extra burst removed (shrapnel)',
 'hurricane':'mythic|aura=ok weapon module; projectile=ok decoration',
 'supernova':'mythic|aura=ok body module; on-hit=converted nova/kinetic blast, flat rings and burst removed (blockPulse)',
 'fireworks':'legendary|aura=ok weapon module; projectile=ok decoration; on-hit=converted fire blast, flat ring and extra burst removed (shrapnel); on-hit=converted voxel impact (split)',
 'vampire':'mythic|projectile=ok decoration',
 'pinball':'legendary|aura=ok weapon module; projectile=ok decoration; on-hit=converted voxel impact (ricochet)',
 'rail':'epic|none=stat only',
 'torrent':'epic|aura=ok weapon module',
 'duelist':'rare|none=stat only',
 'juggernaut':'epic|aura=ok weapon+body module; projectile=ok decoration',
 'needle':'rare|aura=ok weapon module',
 'plague':'epic|projectile=ok decoration; on-hit=ok status flames/bubbles',
 'inferno':'epic|aura=ok weapon module; projectile=ok decoration; on-hit=ok status flames/bubbles',
 'tesla':'epic|aura=ok weapon module; on-hit=converted shock blast, extra beams removed (static)',
 'wrecking':'rare|aura=ok weapon module; projectile=ok decoration',
 'recycler':'uncommon|aura=ok body module',
 'quickguard':'rare|aura=ok body module',
 'bulwark':'epic|aura=ok body module; on-hit=converted voxel impact (rebound)',
 'counterweight':'rare|aura=ok body module; on-hit=converted voxel impact (rebound)',
 'skirmisher':'uncommon|aura=ok body module',
 'reservoir':'rare|aura=ok body module',
 'endurance':'rare|aura=ok body module',
 'drum':'uncommon|none=stat only',
 'chamber':'legendary|none=stat only',
 'sniper-focus':'rare|none=stat only',
 'siphon':'uncommon|projectile=ok decoration',
 'bloodblade':'epic|aura=ok weapon module; projectile=ok decoration',
 'rubber':'epic|aura=ok weapon module; projectile=ok decoration',
 'comet':'legendary|aura=ok body module; field=converted voxel rim + rim motes, fire trail ok; on-hit=converted fire landing blast, extra burst removed',
 'repulsor':'mythic|aura=ok weapon+body module; projectile=ok decoration; on-hit=converted voxel impact (rebound)',
 'long-roll':'uncommon|none=stat only',
 'quick-tuck':'rare|none=stat only',
 'efficient-roll':'common|none=stat only',
 'ghost-timing':'epic|none=stat only',
 'tumble-engine':'legendary|aura=ok body module',
 'reserve-cell':'uncommon|aura=ok body module',
 'power-cell':'common|none=stat only',
 'gunsmith':'common|none=stat only',
 'arcane-lens':'common|none=stat only',
 'whetstone':'common|none=stat only',
 'point-blank':'uncommon|none=stat only',
 'far-sight':'uncommon|none=stat only',
 'adrenaline':'uncommon|none=stat only',
 'keen-edge':'rare|on-hit=converted critical voxel impact (critical)',
 'berserker':'rare|none=stat only',
 'finisher':'rare|none=stat only',
 'overcharge':'epic|none=stat only',
 'titan-edge':'epic|none=stat only',
 'heavy-rounds':'epic|none=stat only',
 'apex':'legendary|on-hit=converted critical voxel impact (critical)',
 'annihilator':'legendary|none=stat only',
 'stout':'common|none=stat only',
 'mender':'common|none=stat only',
 'iron-will':'common|none=stat only',
 'heart-cell':'uncommon|none=stat only',
 'hardened':'uncommon|none=stat only',
 'vampiric-rounds':'uncommon|projectile=ok decoration',
 'nanite-mesh':'rare|none=stat only',
 'warden':'rare|none=stat only',
 'arcane-ward':'rare|aura=ok body module',
 'giant-blood':'rare|none=stat only',
 'blood-pact':'epic|projectile=ok decoration',
 'troll-blood':'epic|none=stat only',
 'colossus-heart':'legendary|none=stat only',
 'phoenix-core':'legendary|projectile=ok decoration',
 // Mechanic chips (effects: []), by phase.
 'black-hole':'mythic|field=converted voxel rim, dark voxel core + accretion cubes, spiral motes, arrival impact (was ring + disc + sphere + 3 tori + 12 dots)',
 'necronomicon':'mythic|on-hit=converted summon ground impact (was 12-spark burst); minions=ok voxel rigs',
 'acid-rain':'epic|field=converted voxel rim + rim motes + arrival impact, acid emitter ok (was ring + disc + pillar + dots)',
 'wildfire':'rare|field=converted voxel rim + rim motes + arrival impact, fire emitter ok',
 'blizzard':'epic|field=converted voxel rim + rim motes + arrival impact, frost emitter ok',
 'time-warp':'legendary|field=converted voxel rim + rim motes + arrival impact; dome=ok shared voxel-faceted dome at the radius',
 'repulsion-pad':'epic|field=converted voxel rim + rim motes + arrival impact; dome=ok shared dome',
 'med-garden':'rare|field=converted voxel rim + rim motes + arrival impact, heal emitter ok',
 'lightning-rod':'legendary|field=converted voxel rim + rim motes + arrival impact, storm bolts ok',
 'bubble-mine':'rare|mark=converted voxel bomb + fuse sparks + voxel rim (was icosahedron + ring + disc); on-hit=converted kinetic blast, extra burst removed',
 'sticky-bomb':'epic|mark=converted voxel bomb follows the target + voxel rim; on-hit=converted fire blast, extra burst removed',
 'cluster-mine':'legendary|mark=converted three voxel mines + voxel rims; on-hit=converted mine blast, extra burst removed',
 'proximity-mail':'rare|mark=converted voxel mine + voxel rim; on-hit=converted mine blast, extra burst removed',
 'delayed-gratification':'epic|mark=converted voxel bomb + voxel rim; on-hit=converted fire blast, extra burst removed',
 'implosion-bomb':'legendary|mark=converted spinning voxel gravity bomb + inward motes on the 4 m radius (was ring + disc + icosahedron); on-hit=converted implosion + void blasts, 3 flat vacuum rings and 28-spark burst removed',
 'wormhole':'mythic|on-hit=converted teleport departure impact + voxel streak (was bursts + beam mesh), void arrival and portal blast ok',
 'swap-shop':'mythic|on-hit=converted teleport departure impact + voxel streak',
 'grappling-shot':'rare|none=knockback only',
 'uppercut':'rare|none=knockback only',
 'bowling-ball':'epic|projectile=ok larger shot',
 'orbitals':'legendary|projectile=ok satellite shots',
 'corkscrew':'epic|projectile=ok path only',
 'elevator-shot':'rare|projectile=ok path only',
 'stop-go':'legendary|projectile=ok path only; on-hit=converted split voxel impact (relaunch)',
 'accelerating-shot':'rare|projectile=ok path only',
 'backfire':'uncommon|projectile=ok extra shot',
 'crossfire':'epic|projectile=ok extra shots',
 'helix':'epic|projectile=ok extra shots',
 'coin-toss':'uncommon|none=audio cue only (coin event), no visual before or after',
 'artillery':'mythic|mark=converted voxel rims + rim motes on each meteor circle (was ring + disc + icosahedron), falling meteor ok; on-hit=converted meteor blasts, extra burst removed',
 'sword-beam':'legendary|projectile=ok melee blade shot',
 'aftershock':'epic|mark=converted voxel bomb + voxel rim; on-hit=converted quake blast, extra burst removed',
 'blink-cut':'legendary|on-hit=converted teleport departure impact + voxel streak',
 'vampire-dash':'rare|none=heal numbers only',
 'bread-crumbs':'rare|mark=converted voxel mine + voxel rim; on-hit=converted mine blast',
 'hot-foot':'epic|field=converted voxel rim + rim motes + arrival impact, fire emitter ok',
 'thunderstep':'legendary|mark=converted voxel rim + rim motes, crackle ok; on-hit=converted shock blast, extra burst removed',
 'trampoline':'rare|on-hit=converted quake blast, extra burst removed',
 'second-chance':'legendary|on-hit=converted teleport departure impact + voxel streak',
 'revenge-mail':'epic|mark=converted voxel bomb + voxel rim; on-hit=converted fire blast',
 'mirror-mine':'epic|mark=converted three voxel mines + voxel rims; on-hit=converted mine blast',
 'freezer-block':'legendary|field=converted voxel rim + arrival impact, shared dome ok; on-hit=ok frost freeze blast',
 'pickpocket':'uncommon|none=resource theft only',
 'mana-leech':'uncommon|none=resource theft only',
 'demolition-charge':'rare|mark=converted voxel bomb + voxel rim; on-hit=converted fire blast',
 'puppet-master':'legendary|on-hit=converted summon ground impact; minions=ok voxel rigs',
 'graveyard-shift':'epic|on-hit=converted summon ground impact; minions=ok voxel rigs',
 'corpse-party':'legendary|on-hit=converted summon ground impact, toxic blast ok (extra burst removed); minions=ok voxel rigs',
 'poltergeist':'epic|none=props are launched (physics only)',
 'panic-button':'mythic|field=converted voxel rim on the 6 m repulsion dome; on-hit=converted kinetic nova blast, 28-spark burst removed',
 // Magic chips (stream C, design §8): they change the ability spells; their visuals are the ability blasts and fields.
 'rune-clock':'common|ability=ok ability visuals','spellweave':'common|ability=ok ability visuals','wide-sigils':'uncommon|ability=ok ability visuals',
 'thrifty-runes':'uncommon|ability=ok ability visuals','swift-orbs':'uncommon|projectile=ok bolt','spellblade':'rare|ability=ok ability visuals',
 'mana-surge':'rare|ability=ok ability visuals','blood-rune':'rare|ability=ok ability visuals','twin-sigil':'epic|ability=ok ability visuals',
 'arcane-overflow':'epic|ability=ok ability visuals','elemental-attunement':'legendary|ability=ok ability visuals','spell-echo':'mythic|ability=ok ability visuals',
 'cinder-field':'rare|field=converted Meteor Strike fire field (voxel rim + arrival impact)','undertow':'rare|ability=ok Tidal Wave','storm-recall':'epic|ability=ok Blink Strike','abyssal-core':'epic|field=converted Singularity voxel core + spiral',
};
const DEV=new Set(['mishima','ansatsuken','l44t','siren']);
/** Chips of stream C (design §8) not on this branch yet; their rows are checked once they merge. */
const PENDING=new Set(['rune-clock','spellweave','wide-sigils','thrifty-runes','swift-orbs','spellblade','mana-surge','blood-rune','twin-sigil','arcane-overflow','elemental-attunement','spell-echo','cinder-field','undertow','storm-recall','abyssal-core']);

test('inventory covers every non-DEV chip of every rarity, with a known status per phase',()=>{
 const ids=new Set(CHIPS.map(c=>c.id));for(const c of CHIPS){if(DEV.has(c.rarity))continue;const row=INVENTORY[c.id];assert.ok(row,`${c.id} missing from the inventory`);assert.equal(row.split('|')[0],c.rarity,c.id);}
 for(const [id,row] of Object.entries(INVENTORY)){assert.ok(ids.has(id)||PENDING.has(id),`${id} is not a chip`);const phases=row.split('|')[1].split('; ');for(const p of phases)assert.match(p,/^(aura|projectile|on-hit|mark|field|dome|rally|minions|ability|none)=(ok|converted|stat only|[a-z])/,`${id}: ${p}`);}
 const converted=Object.entries(INVENTORY).filter(([,r])=>r.includes('=converted'));assert.ok(converted.length>=60,`converted phases ${converted.length}`);
 for(const r of ['common','uncommon','rare','epic','legendary','mythic'])assert.ok(converted.some(([id])=>CHIPS.find(c=>c.id===id)?.rarity===r),`a ${r} chip was converted`);
});
test('no flat ring/disc/torus geometry is built per event or per field; DEV and stage branches are byte-identical to F',()=>{
 const src=(f:string)=>readFileSync(new URL('../src/client/'+f,import.meta.url),'utf8'),flat=/new T\.(Ring|Circle|Torus)Geometry/;
 const vfx=src('event-vfx.ts');assert.doesNotMatch(vfx,flat);assert.doesNotMatch(vfx,/Math\.random/);
 const chip=src('chip-effects.ts');assert.doesNotMatch(chip.slice(chip.indexOf('export class ChipEffects')),flat,'chip-effects builds no flat geometry after module load');
 const chaos=src('chaos-visuals.ts');assert.doesNotMatch(chaos,/(Ring|Circle)Geometry/);assert.doesNotMatch(chaos.slice(chaos.indexOf('export class ChaosVisuals')),/new T\.\w+Geometry/,'field visuals share module geometry');
 // F commit b62d0d3 (verbatim): the leet and stage branches of EventVfx.event.
 for(const branch of ["if(event.type.startsWith('leet-')){this.burst(event.pos,0x55ff88,event.type==='leet-nuke'?70:24,event.type==='leet-nuke'?22:5);if(event.from)this.beam(event.pos,event.from,0x55ff88);}",
  "if(event.type==='stage-saw-hit'){this.burst(event.pos,0xffcb62,32,6);this.burst(event.pos,0xe9f6ff,14,4);for(let n=0;n<7;n++){const a=n*Math.PI*2/7;this.beam(event.pos,{x:event.pos.x+Math.cos(a)*1.1,y:event.pos.y+Math.sin(a)*1.1,z:event.pos.z+.12},0xffe5ad);}}",
  "if(event.type==='stage-spikes-rise')this.burst(event.pos,0xb2a89b,18,3);","if(event.type==='stage-lava-hit')this.burst(event.pos,0xff9c22,8,2);"])assert.ok(vfx.includes(branch),branch.slice(0,40));
 assert.deepEqual(BLAST_CAPACITY,{solid:900,glow:700,lights:3});
});
const field=(over:Partial<FieldView>):FieldView=>({id:'f',owner:'a',kind:'acid',pos:{x:1,y:1,z:2},radius:3,born:0,until:5,arm:.6,damage:10,...over});
const snap=(fields:FieldView[],over:Partial<Snapshot>={})=>({time:.5,fields,minions:[],players:[{id:'a',pos:{x:0,y:1,z:0}},{id:'b',pos:{x:4,y:1,z:0}}],options:{},...over}) as unknown as Snapshot;
test('field telegraphs: a voxel rim exactly on the gameplay radius for every field kind, voxel cores, shared resources',()=>{
 const kinds:FieldView['kind'][]=['black-hole','acid','fire','frost','time','repulsion','heal','storm','bomb','implosion','mine'],scene=new T.Scene(),cv=new ChaosVisuals(scene),m=new T.Matrix4(),p=new T.Vector3();
 const fields=kinds.map((kind,i)=>field({id:'k'+i,kind,radius:2+i*.37,pos:{x:i*12,y:1,z:0}}));cv.update(snap(fields));
 for(const f of fields){const g=cv.fields.get(f.id)!,rim=g.getObjectByName('field-rim') as T.InstancedMesh;assert.ok(rim instanceof T.InstancedMesh,f.kind);assert.ok(rim.count>=12);
  for(let i=0;i<rim.count;i++){rim.getMatrixAt(i,m);p.setFromMatrixPosition(m);assert.ok(Math.abs(Math.hypot(p.x,p.z)-f.radius)<1e-6,`${f.kind} dash ${i} at ${Math.hypot(p.x,p.z)}`);}
  assert.ok(Math.PI*2*f.radius/rim.count<=RIM_SPACING*1.25);g.traverse(o=>{if(o instanceof T.Mesh&&!(o instanceof T.InstancedMesh))assert.equal(o.geometry.type,o.name==='field-dome'?'SphereGeometry':'BoxGeometry',f.kind);});
  const dome=g.getObjectByName('field-dome');if(f.kind==='time'||f.kind==='repulsion')assert.equal(dome!.scale.x,f.radius);else assert.equal(dome,undefined);}
 const geometries=new Set<T.BufferGeometry>();for(const g of cv.fields.values())g.traverse(o=>{if(o instanceof T.Mesh||o instanceof T.LineSegments)geometries.add(o.geometry);});assert.ok(geometries.size<=3,'one box, one dome and its edges for all fields');
 const reverse=cv.fields.get('k9')!;assert.equal(reverse.children.filter(o=>o.userData.orbit!==undefined).length,6,'Reverse Grenade: spinning voxel gravity bomb');
 const orbit=(t:number)=>{cv.update(snap(fields,{time:t}));const o=reverse.children.find(c=>c.userData.orbit===0)!;return Math.hypot(o.position.x,o.position.z);};assert.ok(orbit(.2)>orbit(4.5)+.3,'its orbit tightens as the fuse burns');
 cv.update(snap([]));assert.equal(scene.children.length,0);assert.equal(cv.fields.size,0);
});
test('ability telegraphs tint enemy warm red and own/allied cool cyan; chip fields keep their colour',()=>{
 const ability=field({kind:'bomb',fx:'meteor-strike',owner:'b'}),own=field({kind:'bomb',fx:'meteor-strike',owner:'a'}),chip=field({kind:'acid',fx:'acid-rain',owner:'b'});
 assert.equal(telegraphColor(ability,snap([]),'a'),TELEGRAPH.enemy);assert.equal(telegraphColor(own,snap([]),'a'),TELEGRAPH.ally);assert.notEqual(telegraphColor(chip,snap([]),'a'),TELEGRAPH.enemy);
 const teams=snap([],{players:[{id:'a',team:'red'},{id:'b',team:'red'}],options:{gameMode:'teams'}} as never);assert.equal(telegraphColor(ability,teams,'a'),TELEGRAPH.ally,'teammates are allies');
 assert.equal(telegraphColor(ability,snap([],{pve:{}} as never),'a'),TELEGRAPH.ally,'PvE party');
 const scene=new T.Scene(),cv=new ChaosVisuals(scene);cv.update(snap([ability]),'a');const rim=cv.fields.get('f')!.getObjectByName('field-rim') as T.InstancedMesh;assert.equal((rim.material as T.MeshBasicMaterial).color.getHex(),TELEGRAPH.enemy);
});
test('chip events draw pooled voxels only: no scene objects, no flat pulse rings, no extra spark bursts on blasts',()=>{
 const ev=(type:string,extra:Record<string,unknown>={})=>({id:1,time:0,type,pos:{x:0,y:1,z:0},...extra});
 const cases:[ReturnType<typeof ev>,number][]=[[ev('critical'),1.5],[ev('catch'),.9],[ev('split'),1.1],[ev('ricochet',{mods:{ricochet:1}}),.8],[ev('ricochet',{mods:{pong:1}}),1.2],[ev('impact',{mods:{kinetic:2}}),1.15],[ev('impact',{mods:{demolition:1}}),.62],[ev('meleeRebound',{radius:2}),2],[ev('slowed',{message:'shackles'}),.8],[ev('summon'),1.2]];
 for(const [e,radius] of cases){const scene=new T.Scene(),vfx=new EventVfx(scene,()=>({quality:1,compat:false,trails:false})),chips=new ChipEffects(scene),n=scene.children.length;const radii:number[]=[];const impact=vfx.chipBlasts.impact.bind(vfx.chipBlasts);vfx.chipBlasts.impact=((p,r,h,o)=>{radii.push(r);return impact(p,r,h,o);}) as typeof impact;
  chips.event(e as never,0);vfx.blastEvent(e as never);vfx.event(e as never,snap([]),'me');assert.equal(scene.children.length,n,e.type);assert.equal(chips.pulses.length,0);assert.ok(Math.abs(radii[0]-radius)<1e-9,`${e.type} ${radii[0]}`);assert.ok(vfx.chipBlasts.glow.count>10,e.type);
  let front=0;for(let t=0;t<.3;t+=1/120){vfx.update(1/120);for(let i=0;i<vfx.chipBlasts.glow.count;i++){const p=vfx.chipBlasts.glow.at(i),d=Math.hypot(p.x,p.y-1,p.z);if(d<=radius*1.06)front=Math.max(front,d);}}assert.ok(front>=radius*.9,`${e.type} reaches its radius (${front.toFixed(2)})`);}
 for(const e of [ev('explosion',{message:'implosion-bomb',radius:4}),ev('vacuum',{radius:5}),ev('blockPulse',{radius:3,mods:{supernova:1}}),ev('nova',{radius:6,message:'panic-button'})]){const scene=new T.Scene(),vfx=new EventVfx(scene,()=>({quality:1,compat:false,trails:false})),n=scene.children.length;vfx.blastEvent(e as never);vfx.event(e as never,snap([]),'me');assert.equal(vfx.bursts.count,0,`${e.type}: the volumetric blast only`);assert.equal(scene.children.length,n);assert.ok(vfx.chipBlasts.glow.count>20,e.type);}
 for(const type of ['push','pull']){const scene=new T.Scene(),vfx=new EventVfx(scene,()=>({quality:1,compat:false,trails:false})),e=ev(type,{radius:5});vfx.blastEvent(e as never);vfx.event(e as never,snap([]),'me');assert.equal(vfx.bursts.count+vfx.chipBlasts.glow.count+vfx.particles.length,0,`legacy ${type} visuals are removed`);}
});
test('Reverse Grenade mark: inward motes start on the 4 m radius and stream to the bomb; the detonation adds no flat rings',()=>{
 const fx=new ChipBlasts(new T.Scene()),f=field({kind:'implosion',fx:'implosion-bomb',radius:4,pos:{x:0,y:1,z:0},born:0,until:.9});fx.field(f,1,1);fx.clear();fx.field(f,.2,1);
 let starts=0;for(let t=.2;t<.85;t+=1/60){const n=fx.glow.count;fx.field(f,t,1);for(let i=n;i<fx.glow.count;i++){const p=fx.glow.at(i),d=Math.hypot(p.x,p.z);if(Math.abs(d-4)<1e-6)starts++;}fx.update(1/60);}
 assert.ok(starts>=10,`inward motes from the radius (${starts})`);let inner=0;for(let i=0;i<fx.glow.count;i++)if(Math.hypot(fx.glow.at(i).x,fx.glow.at(i).z)<2)inner++;assert.ok(inner>0,'they reach the centre');
});
test('pong rally: a voxel paddle flash at the bounce point and a short streak toward the rally target',()=>{
 const scene=new T.Scene(),vfx=new EventVfx(scene,()=>({quality:1,compat:false,trails:false})),n=scene.children.length,e={id:1,time:0,type:'ricochet',pos:{x:0,y:1,z:0},from:{x:0,y:1,z:-.2},target:'b',message:'rally',mods:{pong:1}};
 vfx.event(e as never,snap([]),'me');assert.equal(scene.children.length,n);const g=vfx.chipBlasts.glow;assert.ok(g.count>=16+8);
 let far=0;for(let t=0;t<.15;t+=1/120){vfx.update(1/120);for(let i=0;i<g.count;i++){const p=g.at(i);if(p.age>=0)far=Math.max(far,p.x);}}assert.ok(far>3,`streak runs toward b at x=4 (${far.toFixed(2)})`);
 for(let i=0;i<30;i++)vfx.update(1/60);assert.equal(g.count,0,'gone within 0.5 s');
});
test('ability statuses: slowed droplets, shackle cuffs and the Orbital Mark reticle follow authoritative state',()=>{
 const scene=new T.Scene(),hands=new T.Group(),camera=new T.PerspectiveCamera(),fx=new PlayerStatusEffects(scene,hands);
 const player=(id:string,slowed?:unknown)=>({id,pos:{x:0,y:1,z:0},hp:100,statuses:[],slowed});
 const state=(players:unknown[],fields:FieldView[]=[],time=1)=>({time,phase:'fight',players,fields}) as unknown as Snapshot,get=(name:string)=>scene.children[0].getObjectByName(name)!;
 fx.update(state([player('b',{factor:.8,until:3,kind:'soak'})]),'a',false,camera,1);const slow=get('ability-slow');assert.ok(slow.visible);assert.equal(((slow.children[0] as T.Mesh).material as T.MeshBasicMaterial).color.getHex(),0x7fe3ff);
 fx.update(state([player('b',{factor:.45,until:3,kind:'shackles'})]),'a',false,camera,1);assert.equal(((slow.children[0] as T.Mesh).material as T.MeshBasicMaterial).color.getHex(),0xa04dff,'shackles are violet cuffs');
 fx.update(state([player('b',{factor:.45,until:3,kind:'shackles'})],[],3.5),'a',false,camera,1);assert.equal(slow.visible,false,'expired');
 const mark=field({id:'m',kind:'bomb',fx:'orbital-mark',attach:'b',born:0,lockAt:1,until:1.2});const reticle=get('orbital-marked'),r=(t:number)=>{fx.update(state([player('b')],[mark],t),'a',false,camera,1);return Math.hypot(reticle.children[0].position.x,reticle.children[0].position.z);};
 assert.equal(r(.1)>r(.9),true,'the reticle closes in as the strike locks');assert.ok(reticle.visible);fx.update(state([player('b')],[],1.3),'a',false,camera,1);assert.equal(reticle.visible,false);
 const count=()=>{let n=0;scene.traverse(()=>n++);return n;},before=count();for(let i=0;i<60;i++)fx.update(state([player('b',{factor:.5,until:9,kind:'soak'})],[mark],i/60),'a',false,camera,1);assert.equal(count(),before,'bounded meshes');
});
