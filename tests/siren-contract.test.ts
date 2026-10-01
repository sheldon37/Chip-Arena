import {test} from 'node:test';
import assert from 'node:assert/strict';
import {BLACK_SIREN_PROTOCOL as track,loopedSongTime,beatIndexAt,barAt,barStart,sectionAt,crossedBeats} from '../src/shared/boss-music.js';
import {SIREN,SIREN_INTRO,SIREN_SLOTS,SIREN_TIMING,sirenPoint,sirenOf,sirenWorldOf,sirenShotOf,bossSongOf} from '../src/shared/siren-contract.js';
import {sirenStageBlocks,sirenStageHazards} from '../src/shared/siren-stage-layout.js';
import {playerSpawn} from '../src/shared/arena.js';

test('beat map is ascending and covers the song',()=>{
 assert.equal(track.beats.length,464);
 for(let i=1;i<track.beats.length;i++)assert.ok(track.beats[i]>track.beats[i-1],`beat ${i} not ascending`);
 assert.ok(track.beats.at(-1)!<track.duration);
 // Tempo drifts but stays a plausible 130-170 BPM between neighbours.
 for(let i=1;i<track.beats.length;i++){const bpm=60/(track.beats[i]-track.beats[i-1]);assert.ok(bpm>125&&bpm<175,`beat ${i}: ${bpm.toFixed(1)} BPM`);}
});

test('bars, sections, intro window and loop points line up with the beats',()=>{
 assert.equal(barStart(track,1),track.beats[track.firstDownbeat]);
 assert.equal(barStart(track,5),track.introEnd);
 assert.equal(barStart(track,45),track.loop.start);
 assert.equal(barStart(track,105),track.loop.end);
 assert.deepEqual(barAt(track,barStart(track,17)+.01).bar,17);
 assert.equal(barAt(track,barStart(track,17)+.01).beat,1);
 assert.equal(sectionAt(track,barStart(track,50)),'lift');
 assert.equal(sectionAt(track,barStart(track,90)),'climax');
 assert.equal(sectionAt(track,1),'build');
});

test('looped song time wraps from the loop end back to the loop start',()=>{
 assert.equal(loopedSongTime(track,10),10);
 assert.equal(loopedSongTime(track,track.loop.end),track.loop.start);
 const span=track.loop.end-track.loop.start;
 assert.ok(Math.abs(loopedSongTime(track,track.loop.end+span*2+1.5)-(track.loop.start+1.5))<1e-9);
});

test('beat lookup is exact at boundaries',()=>{
 assert.equal(beatIndexAt(track,0),-1);
 assert.equal(beatIndexAt(track,track.beats[10]),10);
 assert.equal(beatIndexAt(track,track.beats[10]-1e-6),9);
});

test('crossed beats are reported once, including across the loop wrap',()=>{
 assert.deepEqual(crossedBeats(track,track.beats[3]-.01,track.beats[5]+.01),[3,4,5]);
 const last=beatIndexAt(track,track.loop.end-1e-6),first=beatIndexAt(track,track.loop.start);
 const got=crossedBeats(track,track.loop.end-.05,track.loop.end+.05);
 assert.ok(got.includes(first)&&!got.includes(last),`wrap reported ${got}`);
 assert.deepEqual(crossedBeats(track,5,5),[]);
});

test('intro cues sit on the beats they name and fit the intro window',()=>{
 for(const c of SIREN_INTRO.cues)if(c.beat>=0)assert.ok(Math.abs(track.beats[c.beat]-c.time)<1e-4,`${c.cue}`);
 assert.equal(SIREN_INTRO.duration,track.introEnd);
 assert.ok(SIREN_INTRO.nameAt<SIREN_INTRO.duration);
});

test('slots cover every move once and timings are positive',()=>{
 const all=[SIREN_SLOTS.melee.primary,...SIREN_SLOTS.melee.abilities,SIREN_SLOTS.ranged.primary,...SIREN_SLOTS.ranged.abilities];
 assert.equal(new Set(all).size,10);
 for(const id of all)for(const [k,v] of Object.entries(SIREN_TIMING[id]))assert.ok(v>0,`${id}.${k}`);
 assert.equal(SIREN.spec,'siren');
});

test('sirenPoint uses the rig convention (+z forward, +x the left side)',()=>{
 const at=(yaw:number,l:{x:number;y:number;z:number})=>sirenPoint({x:1,y:2,z:3},yaw,l);
 assert.deepEqual(at(0,{x:0,y:0,z:1}),{x:1,y:2,z:4});
 const r=at(Math.PI/2,{x:0,y:1,z:1});assert.ok(Math.abs(r.x-2)<1e-9&&Math.abs(r.z-3)<1e-9&&r.y===3);
 const left=at(0,{x:1,y:0,z:0});assert.deepEqual(left,{x:2,y:2,z:3});
});

test('accessors read optional snapshot fields without the types',()=>{
 assert.equal(sirenOf({}),undefined);
 assert.equal(sirenOf({siren:{mode:'melee',modeAt:0}})?.mode,'melee');
 assert.equal(sirenWorldOf({siren:{patches:[],eruptions:[],rains:[],marks:[],grabs:[]}})?.patches.length,0);
 assert.equal(sirenShotOf({siren:{kind:'seeker',born:1}})?.kind,'seeker');
 assert.equal(bossSongOf({bossSong:{track:'black-siren-protocol',startedAt:4}})?.startedAt,4);
});

test('Ward Zero keeps spawn pads clear, has indestructible cover everywhere and unique ids',()=>{
 const blocks=sirenStageBlocks();
 assert.equal(new Set(blocks.map(b=>b.id)).size,blocks.length);
 const solid=blocks.filter(b=>!b.id.includes('floor'));
 for(let i=0;i<8;i++){const s=playerSpawn(i);for(const b of solid){
  const dx=Math.abs(s.x-b.pos.x)-b.size.x/2,dz=Math.abs(s.z-b.pos.z)-b.size.z/2;
  assert.ok(Math.max(dx,dz)>1.6,`spawn ${i} blocked by ${b.id}`);}}
 // From any open point on a 4 m grid, indestructible cover taller than a player is within 9 m.
 const cover=blocks.filter(b=>b.material==='foundation'&&b.size.y>=2.5&&!b.id.includes('floor'));
 for(let x=-28;x<=28;x+=4)for(let z=-24;z<=24;z+=4){
  const d=Math.min(...cover.map(b=>Math.hypot(Math.max(0,Math.abs(x-b.pos.x)-b.size.x/2),Math.max(0,Math.abs(z-b.pos.z)-b.size.z/2))));
  assert.ok(d<=9,`no indestructible cover within 9 m of (${x},${z}): ${d.toFixed(1)}`);
 }
 assert.ok(blocks.some(b=>b.material!=='foundation'),'destructible props present');
});

test('stage vents only fire in lift and climax, with a telegraph first',()=>{
 const t0=barStart(track,30);for(let t=t0;t<t0+6;t+=.05)assert.ok(sirenStageHazards(t).every(h=>!h.active&&h.warning===0),'groove is quiet');
 // Every vent that starts burning (bars 45-60, the first lift) was warned during the previous bar.
 const warnedAt=new Map<string,number>(),was=new Map<string,boolean>();let fired=0;
 for(let t=barStart(track,44);t<barStart(track,61);t+=.01)for(const h of sirenStageHazards(t)){
  if(h.warning>0)warnedAt.set(h.id,t);
  if(h.active&&!was.get(h.id)){fired++;assert.ok(t-(warnedAt.get(h.id)??-Infinity)<.3,`${h.id} fired at ${t.toFixed(2)} without a telegraph`);}
  was.set(h.id,h.active);
 }
 assert.ok(fired>=16,`only ${fired} eruptions in the lift`);
 assert.deepEqual(sirenStageHazards(0),[]);
});
