// Ward Zero outside PvE (owner 2026-10-04): vents visibly warn and open before they take a fall, with no add climbing out;
// no PvE machines (or the pads they stand on) appear in PvP or the DEV sandbox, while a real PvE run keeps them.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {Simulation,initPhysics,type Snapshot} from '../src/shared/simulation.js';
import {wardEntries} from '../src/shared/sandbox-tools.js';
import {VENT_PIT,entryOpenAmount,wardVentDrawn,wardVentWarning} from '../src/shared/pve/vent-pits.js';
import {machinesPresent,PVE_MACHINES} from '../src/shared/pve/layout.js';
import {fixtureBase,fixtureParty} from '../src/shared/pve/fixtures.js';
import type {PveEntryView,PveView} from '../src/shared/pve/contract.js';
import {SirenStageVisuals} from '../src/client/siren/index.js';
import {VENT_SWING} from '../src/client/siren/stage.js';
import {PveVisuals,type PveRigRef,type PveVisualsContext} from '../src/client/pve/visuals.js';
import {promptFor} from '../src/client/pve/prompts.js';
await initPhysics();

const snap=(time:number,extra:Partial<Snapshot>={},options:Record<string,unknown>={map:'siren'}):Snapshot=>({time,tick:0,phase:'fight',remaining:60,round:1,setNumber:1,setWinner:null,winner:null,result:'',
 players:[],props:[],projectiles:[],events:[],impact:null,metrics:{bodies:0,projectiles:0,tickMs:0},options,...extra}) as unknown as Snapshot;
const downView=(t:number):PveView=>{const ids=['me','bot'],v=fixtureBase({time:t,ids});v.party=fixtureParty({time:t,ids,kinds:['alive','dead']});return v;};
function ctx():PveVisualsContext{return {camera:new T.PerspectiveCamera(),me:'me',dt:1/60,quality:1,compatibility:false,replay:false,rigs:new Map<string,PveRigRef>(),burst:()=>{}};}

test('PvP snapshots announce every vent opening warn + lead early, one entry per vent; the drawn lid is open whenever the pit is lethal, even drawn late',()=>{
 const s=new Simulation({map:'siren',gameMode:'ffa'});s.addPlayer('a','A','ranged',null,'#fff');s.addPlayer('b','B','melee',null,'#eee');s.phase='fight';s.fightStart=s.time;
 try{assert.equal(s.pve,undefined);let lethalTicks=0;const lags=[0,.15,.35];
  for(let i=0;i<60*14;i++){s.step();const t=s.time,sent=s.snapshot().vents!,all=wardEntries(t,s.vents!.forced);
   for(const e of all){const shown=sent.filter(x=>x.e===e.e);assert.ok(shown.length<=1,'one opening per vent');
    assert.equal(shown.length===1,t>=e.openAt-VENT_PIT.lead-VENT_PIT.warn,`vent ${e.e} at ${t.toFixed(2)}`);
    if(entryOpenAmount('vent',e,t)>=VENT_PIT.lethal){lethalTicks++;for(const lag of lags)assert.ok(wardVentDrawn(e,t-lag)>.9,`vent ${e.e} drawn ${lag} s late at ${t.toFixed(2)}`);}
    if(t<e.openAt-VENT_PIT.lead&&t>=e.openAt-VENT_PIT.lead-VENT_PIT.warn+.05)assert.ok(wardVentWarning(e,t)>0,'warning before the lid moves');}}
  assert.ok(lethalTicks>60*3,'vents opened during the run');
 }finally{s.dispose();}
});

test('the stage warns, then swings the lid ahead of the pit, from the PvP schedule; a sandbox PvE preview never freezes the vents; no adds',()=>{
 const scene=new T.Scene(),camera=new T.PerspectiveCamera(60,16/9,.1,300),stage=new SirenStageVisuals(scene);stage.setActive(true);camera.position.set(0,3,0);
 const e:PveEntryView={e:0,openAt:20,closeAt:24.5,mode:'dive'},lid=scene.getObjectByName('ward-vent-0-lid')!,pit=scene.getObjectByName('ward-vent-0-pit')!;
 const rim=(scene.getObjectByName('ward-vent-0')!.children.find(o=>o instanceof T.Mesh&&o.geometry instanceof T.RingGeometry) as T.Mesh<T.BufferGeometry,T.ShaderMaterial>).material;
 const at=(t:number,extra:Partial<Snapshot>={},options?:Record<string,unknown>)=>{stage.update(snap(t,{vents:[e],...extra},options),t,1,camera);return {lid:lid.rotation.z,pit:pit.visible,rim:rim.uniforms.uOpacity.value as number};};
 const idle=at(17);assert.ok(idle.lid<1e-6&&!idle.pit&&idle.rim<1e-6,'shut and dark before the warning');
 const warn=at(e.openAt-VENT_PIT.lead-.2);assert.ok(warn.rim>.3,'rim warning');assert.ok(warn.lid<.07&&!warn.pit,'only a rattle while warning');
 const swing=at(e.openAt);assert.ok(swing.lid>.5&&swing.pit,'the lid is already swinging when the authority opens the vent');
 const lethal=at(e.openAt+.23);assert.ok(lethal.lid>VENT_SWING*.99,'fully open by the first lethal tick');
 // The sandbox's downed/revive preview injects a PvE view (no entries) into a PvP snapshot: the vents keep animating.
 const preview=at(e.openAt+1,{pve:downView(e.openAt+1)},{map:'siren',sandbox:true,pve:{boss:'siren',seed:0}});assert.ok(preview.lid>VENT_SWING*.99&&preview.pit);
 const shut=at(e.closeAt+VENT_PIT.lead+.5);assert.ok(shut.lid<1e-6&&!shut.pit,'shut again after the opening');
 let rigs=0;scene.traverse(o=>{if(/^pve-add|horde/.test(o.name))rigs++;});assert.equal(rigs,0,'no add ever climbs out in PvP');
});

test('machines and their pads exist only in a real PvE run: hidden in PvP and in the sandbox previews, kept in PvE',()=>{
 assert.equal(machinesPresent({options:{}}),false,'PvP');assert.equal(machinesPresent({options:{sandbox:true},pve:{}}),false,'sandbox preview');assert.equal(machinesPresent({options:{},pve:{}}),true,'PvE');
 const scene=new T.Scene(),pve=new PveVisuals(scene),stage=new SirenStageVisuals(scene),camera=new T.PerspectiveCamera();stage.setActive(true);camera.position.set(0,3,0);
 const show=(options:Record<string,unknown>,t:number)=>{const s=snap(t,{pve:downView(t)},options);pve.update(s,ctx());stage.update(s,t,1,camera);
  return {machines:scene.getObjectByName('pve-machines')?.visible??false,pads:scene.getObjectByName('ward-pads')!.visible,downed:!!scene.getObjectByName('pve-downed')};};
 const sandbox=show({map:'siren',sandbox:true,pve:{boss:'siren',seed:0}},5);assert.equal(sandbox.machines,false);assert.equal(sandbox.pads,false);
 const real=show({map:'siren'},6);assert.equal(real.machines,true);assert.equal(real.pads,true);
 const pvp=snap(7,{},{map:'siren'});pve.update(pvp,ctx());stage.update(pvp,7,1,camera);assert.equal(scene.getObjectByName('ward-pads')!.visible,false,'no pads in PvP');
 // No buy/recruit/heal prompts at the invisible machines in the sandbox; a real PvE run still offers them.
 for(const m of [PVE_MACHINES[0],PVE_MACHINES[4],PVE_MACHINES[6]]){const me={id:'me',name:'Me',hp:100,maxHp:100,pos:{x:m.front.x,y:.86,z:m.front.z}};
  const state=(options:Record<string,unknown>)=>snap(5,{players:[me as never],pve:downView(5)},options);
  assert.equal(promptFor(state({map:'siren',sandbox:true,pve:{boss:'siren',seed:0}}),downView(5),'me'),null,m.kind);
  assert.notEqual(promptFor(state({map:'siren'}),downView(5),'me'),null,m.kind);}
});
