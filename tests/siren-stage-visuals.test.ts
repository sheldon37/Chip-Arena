import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {SirenStageVisuals} from '../src/client/siren/index.js';
import {WARD_ZERO_AIR} from '../src/client/siren/stage.js';
import {MapScenery} from '../src/client/map-scenery.js';
import {arenaLighting} from '../src/client/arena-lighting.js';
import {sirenStageBlocks,sirenStageHazards,SIREN_ADD_SPAWNS,SIREN_CHIP_SPOTS} from '../src/shared/siren-stage-layout.js';
import {BLACK_SIREN_PROTOCOL,barStart} from '../src/shared/boss-music.js';
import type {Snapshot} from '../src/shared/simulation.js';

const snapshot=(time:number):Snapshot=>({time,tick:0,phase:'fight',remaining:60,round:1,setNumber:1,setWinner:null,winner:null,result:'',players:[],props:[],projectiles:[],events:[],impact:null,metrics:{bodies:0,projectiles:0,tickMs:0},options:{map:'siren'},bossSong:{track:'black-siren-protocol',startedAt:0}}) as unknown as Snapshot;
function ward(){
 const scene=new T.Scene(),camera=new T.PerspectiveCamera(60,16/9,.1,300),stage=new SirenStageVisuals(scene);stage.setActive(true);
 const props=new Map(sirenStageBlocks().map(b=>{const o=new T.Object3D();o.position.set(b.pos.x,b.pos.y,b.pos.z);return [b.id,o] as [string,T.Object3D];}));
 const run=(from:number,to:number,dt=1/30)=>{for(let t=from;t<=to;t+=dt){stage.update(snapshot(t),t,1,camera);stage.shell(props);}};
 return {scene,camera,stage,props,run,named:(n:string)=>scene.getObjectByName(n)!};
}

test('an outside camera sees the hall cut away; an inside camera sees it sealed again',()=>{
 const w=ward();w.camera.position.set(0,3,0);w.run(0,.2);
 assert.ok([...w.props.values()].every(o=>o.visible),'inside, every wall and the roof are drawn');
 for(const n of ['ward-north','ward-south','ward-east','ward-west','ward-vault'])assert.equal(w.named(n).visible,true,n);
 // The menu overview orbits outside the east wall and above the roof.
 w.camera.position.set(38,32,10);w.run(.2,.3);
 const roof=[...w.props].filter(([id])=>id.startsWith('siren-roof-')),walls=[...w.props].filter(([id])=>id.startsWith('siren-wall-'));
 assert.equal(roof.length,1);assert.ok(roof.every(([,o])=>!o.visible),'the roof is cut away from above');
 assert.deepEqual(walls.map(([,o])=>o.visible).sort(),[false,true,true,true],'only the wall between the camera and the hall is cut');
 assert.equal(walls.find(([,o])=>o.position.x>32)![1].visible,false);
 assert.equal(w.named('ward-east').visible,false);assert.equal(w.named('ward-vault').visible,false);assert.equal(w.named('ward-west').visible,true);
 assert.ok([...w.props].filter(([id])=>!id.startsWith('siren-roof-')&&!id.startsWith('siren-wall-')).every(([,o])=>o.visible),'cover is never hidden');
 w.camera.position.set(-10,4,6);w.run(.3,.4);assert.ok([...w.props.values()].every(o=>o.visible));assert.equal(w.named('ward-east').visible,true);
});

test('the dressing is built from the shared layout and is identical for every viewer',()=>{
 const a=ward(),b=ward(),matrices=(s:T.Scene)=>{const out:number[]=[];s.traverse(o=>{if(o instanceof T.InstancedMesh&&o.name==='ward-kit')out.push(...o.instanceMatrix.array);});return out;};
 assert.deepEqual(matrices(a.scene),matrices(b.scene),'same seeds, same hall');
 const lamps=a.named('ward-pad-rings') as T.InstancedMesh;assert.equal(lamps.count,SIREN_CHIP_SPOTS.length,'a pad for every chip spot');
 SIREN_CHIP_SPOTS.forEach((c,i)=>{const m=new T.Matrix4();lamps.getMatrixAt(i,m);const p=new T.Vector3().setFromMatrixPosition(m);assert.ok(Math.hypot(p.x-c.x,p.z-c.z)<1e-6,c.id);});
 assert.equal(SIREN_ADD_SPAWNS.length,8);
});

test('the hall keeps time with the song: doors pound on kicks in the climax, rest in the breakdown, vents follow the damage schedule',()=>{
 const w=ward(),doors=w.named('ward-doors'),track=BLACK_SIREN_PROTOCOL,climax=barStart(track,90),breather=barStart(track,70);
 w.camera.position.set(0,3,0);
 // Let the mood settle into the climax, then sample a kick (beat 1) and an off-beat (beat 2).
 const beat=(k:number)=>track.beats[track.beats.indexOf(climax)+k];
 w.run(climax-6,beat(4));w.run(beat(4)+.02,beat(4)+.02);const kick=doors.position.z;
 w.run(beat(5)+.02,beat(5)+.02);const off=doors.position.z;
 assert.ok(kick<-.04,`the doors bow in on the kick (${kick.toFixed(3)})`);assert.ok(Math.abs(off)<1e-9,'and settle on the off-beat');
 w.run(breather-6,breather+4);assert.ok(Math.abs(doors.position.z)<.01,'quiet in the breakdown');
 // Vent rims read sirenStageHazards: armed before an eruption, white-hot while it burns.
 const lift=barStart(track,50);let burning=0,armed=0;
 for(let t=lift;t<lift+8;t+=1/30){w.run(t,t);for(const h of sirenStageHazards(t)){const rim=w.named('ward-'+h.id).children.find(o=>o instanceof T.Mesh&&o.geometry instanceof T.RingGeometry) as T.Mesh<T.BufferGeometry,T.ShaderMaterial>;
  const want=h.active?.95:h.warning*.9;assert.ok(Math.abs(rim.material.uniforms.uOpacity.value-want)<1e-9,`${h.id} at ${t.toFixed(2)}`);if(h.active)burning++;else if(h.warning>0)armed++;}}
 assert.ok(burning>0&&armed>0,'the lift shows both telegraphs and eruptions');
});

test('Ward Zero is indoors: no sky, skyline or grid, an interior haze and an overhead key light; outdoor maps get them back',()=>{
 const scene=new T.Scene();arenaLighting(scene);const scenery=new MapScenery(scene),moon=scene.getObjectByName('night-moon')!;
 scenery.set('siren');
 assert.equal(scene.getObjectByName('clouded-night-sky'),undefined,'no sky dome');assert.equal(scenery.group.children.length,0,'no skyline, trims or grid');
 assert.equal((scene.fog as T.Fog).near,WARD_ZERO_AIR.near);assert.equal((scene.background as T.Color).getHex(),WARD_ZERO_AIR.background);assert.deepEqual(moon.position.toArray(),WARD_ZERO_AIR.key);
 scenery.set('foundry');
 assert.ok(scene.getObjectByName('clouded-night-sky'),'the sky returns');assert.ok(scenery.group.children.length>0);assert.deepEqual(moon.position.toArray(),[-8,20,-12]);
});
