import {test} from 'node:test';
import assert from 'node:assert/strict';
import {existsSync} from 'node:fs';
import * as T from 'three';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {arenaBlocks,playerSpawn} from '../src/shared/arena.js';
import {MAPS,mapId} from '../src/shared/maps.js';
import {CONFIG} from '../src/shared/config.js';
import {HostedRoom} from '../src/shared/host-room.js';
import {MapScenery} from '../src/client/map-scenery.js';
import {ArenaMusic} from '../src/client/music.js';
await initPhysics();
test('fourteen distinct deterministic maps reserve clear spawns and debris budget; eight pilots fight and reset on each',()=>{
 assert.equal(MAPS.length,14);assert.equal(mapId('__proto__'),'foundry');const layouts=new Set<string>();
 for(const map of MAPS){const blocks=arenaBlocks(map.id);assert.deepEqual(blocks,arenaBlocks(map.id));layouts.add(JSON.stringify(blocks.map(({pos,size})=>({pos,size}))));assert.equal(new Set(blocks.map(b=>b.id)).size,blocks.length);assert.ok(blocks.length<=CONFIG.maxBodies-20,`${map.id}: ${blocks.length}`);
  for(let i=0;i<8;i++){const p=playerSpawn(i,false);for(const b of blocks)assert.ok(Math.abs(p.x-b.pos.x)>b.size.x/2+.43||Math.abs(p.z-b.pos.z)>b.size.z/2+.43||p.y-.84>=b.pos.y+b.size.y/2,`${map.id} spawn ${i} overlaps ${b.id}`);}
  if(map.music)assert.ok(existsSync(new URL(`../public/music/${map.music}.mp3`,import.meta.url)));
  const s=new Simulation({map:map.id,skipIntros:true});try{s.addPlayer('host');s.configureLobby({botCount:7,botDifficulty:'normal'});assert.equal(s.players.size,8);assert.equal(s.props.size,blocks.length);s.setReady('host',true);s.selectSpec('host','pistol');for(let i=0;i<900;i++)s.step();assert.ok(s.time>=14);assert.ok([...s.players.values()].some(p=>p.bot&&p.lastAttack>0),map.id);for(const p of s.players.values())assert.ok(Object.values(p.body.translation()).every(Number.isFinite));s.resetRound();assert.equal(s.props.size,blocks.length);assert.equal(s.options.map,map.id);}finally{s.dispose();}
 }assert.equal(layouts.size,14);assert.ok(arenaBlocks('moonlit').filter(b=>Math.abs(b.pos.x)<12&&Math.abs(b.pos.z)<12).every(b=>b.pos.y<=1));for(const map of MAPS)assert.ok(arenaBlocks(map.id).some(b=>b.pos.y>10),map.id);
});
test('only host changes lobby settings; bot capacity preserves humans and bots do not vote on rematches',()=>{
 const room=new HostedRoom('ABCD2345',{name:'Host',class:'ranged',color:'#ff6633'},false,()=>{},6,false,true,5,{map:'neon',botCount:5});try{
 assert.ok(room.join({name:'Friend'}));assert.equal(room.sim.players.size,7);assert.equal(room.receive('guest',{type:'settings',map:'forge',botCount:0}),false);assert.equal(room.sim.options.map,'neon');room.receive('host',{type:'ready',ready:true});assert.ok(room.receive('host',{type:'settings',map:'canal',botCount:100,botDifficulty:'hard'}));assert.equal(room.sim.players.size,8);assert.equal(room.sim.options.botCount,6);assert.equal(room.sim.options.map,'canal');for(const p of room.sim.players.values())assert.equal(p.ready,!!p.bot);
 room.receive('host',{type:'ready',ready:true});room.receive('guest',{type:'ready',ready:true});assert.equal(room.sim.phase,'specialization');assert.equal(room.receive('host',{type:'settings',map:'forge'}),false);room.sim.phase='match';room.receive('host',{type:'rematch'});assert.equal(room.sim.phase,'match');room.receive('guest',{type:'rematch'});assert.equal(room.sim.phase,'specialization');room.sim.returnLobby();room.receive('host',{type:'settings',botCount:NaN});assert.equal(room.sim.players.size,2);assert.ok(room.sim.players.has('host')&&room.sim.players.has('guest'));
 }finally{room.dispose();}
});
test('map scenery uses bounded batches, finite transforms and releases replaced resources',()=>{
 const scene=new T.Scene(),scenery=new MapScenery(scene);let disposed=0;
 for(const map of MAPS){scenery.set(map.id);assert.ok(scenery.group.children.length<=16,map.id);const children=[...scenery.group.children];scenery.group.traverse(o=>{const mesh=o as T.Mesh;if(mesh.geometry)mesh.geometry.addEventListener('dispose',()=>disposed++);});scenery.set(map.id);assert.deepEqual(scenery.group.children,children);scenery.update(10);scene.updateMatrixWorld(true);scene.traverse(o=>assert.ok(o.matrixWorld.elements.every(Number.isFinite)));for(const c of children)if(c instanceof T.InstancedMesh){for(let i=0;i<c.count;i++){const matrix=new T.Matrix4();c.getMatrixAt(i,matrix);assert.ok(matrix.elements.every(Number.isFinite));}}}
 assert.ok(disposed>10);
});
test('map music switches one stream, loops its theme and retains volume/mute',()=>{
 const audios:any[]=[];const original=globalThis.Audio;
 class AudioMock{loop=false;volume=0;ended=false;onended:any;onerror:any;paused=false;constructor(public src:string){audios.push(this);}play(){this.paused=false;return Promise.resolve();}pause(){this.paused=true;}removeAttribute(){this.src='';}load(){}}
 globalThis.Audio=AudioMock as any;try{const music=new ArenaMusic();music.setMap('moonlit');music.setActive(true);assert.equal(audios.length,1);assert.match(audios[0].src,/moonlit-wilderness/);assert.ok(audios[0].loop);music.setMap('moonlit');assert.equal(audios.length,1);music.configure(.4,true);music.setMap('neon');assert.ok(audios[0].paused);assert.equal(audios[1].volume,0);assert.equal(music.title,'Violet Avenue');music.setActive(false);assert.ok(audios[1].paused);music.setMap('foundry');music.setActive(true);assert.equal(audios[2].loop,false);}finally{globalThis.Audio=original;}
});
