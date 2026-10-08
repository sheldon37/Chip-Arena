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
import {prewarmSirenMinions} from '../src/client/siren/minions/index.js';
import {PVE_ENTRIES} from '../src/shared/pve/layout.js';
import {SIREN_VENTS} from '../src/shared/siren-stage-layout.js';
const SIREN_VENTS_IDS=SIREN_VENTS.map((_,i)=>i);
import {PVE_FIXTURES} from '../src/shared/pve/fixtures.js';
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

test('the stage warns, then swings the lid ahead of the pit, from the PvP schedule; a sandbox PvE preview never freezes the vents',()=>{
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

test('no add climbs out of an opening Ward Zero vent in PvP: the same visuals that draw PvE climbers draw none without a PvE view',()=>{
 while(!prewarmSirenMinions(50)){/* warm the shared layouts once */}
 const scene=new T.Scene(),pve=new PveVisuals(scene),stage=new SirenStageVisuals(scene),c=ctx(),v0=PVE_ENTRIES[0];stage.setActive(true);
 c.camera.position.set(v0.from.x+3,1.6,v0.from.z+3);c.camera.updateMatrixWorld();
 const shown=(o:T.Object3D)=>{for(let x:T.Object3D|null=o;x;x=x.parent)if(!x.visible)return false;return !(o instanceof T.InstancedMesh)||o.count>0;};
 const adds=()=>{let n=0;scene.traverse(o=>{if(/^pve-add/.test(o.name)&&shown(o))n++;});return n;};
 const t=100;for(let k=0;k<10;k++){const s=snap(t+k/60,{pve:PVE_FIXTURES.entries({time:t+k/60,ids:['me']})},{map:'siren'});pve.update(s,c);stage.update(s,s.time,1,c.camera);}
 assert.ok(adds()>0,'live check: PvE vent climbers are drawn');
 const e:PveEntryView={e:0,openAt:t+1,closeAt:t+5.5,mode:'dive'};
 for(let k=0;k<180;k++){const s=snap(t+1+k/60,{vents:[e]},{map:'siren'});pve.update(s,c);stage.update(s,s.time,1,c.camera);}
 assert.ok(scene.getObjectByName('ward-vent-0-pit')!.visible,'the vent is open');assert.equal(adds(),0,'nothing climbs out in PvP');
});

test('online, the stage draws a vent from a snapshot it presents up to 0.35 s late: the lid never pops while the cycle rolls over',()=>{
 const s=new Simulation({map:'siren',gameMode:'ffa'});s.addPlayer('a','A','ranged',null,'#fff');s.addPlayer('b','B','melee',null,'#eee');s.phase='fight';s.fightStart=s.time;
 const scene=new T.Scene(),camera=new T.PerspectiveCamera(60,16/9,.1,300),stage=new SirenStageVisuals(scene);stage.setActive(true);camera.position.set(0,3,0);
 const lids=SIREN_VENTS_IDS.map(i=>scene.getObjectByName('ward-vent-'+i+'-lid')!),last=lids.map(()=>NaN);
 try{for(let i=0;i<60*26;i++){s.step();const snap=s.snapshot(),t=snap.time-.35;stage.update({...snap,time:t},t,1,camera);
  lids.forEach((l,k)=>{const r=l.rotation.z;assert.ok(!(Math.abs(r-last[k])>=VENT_SWING*.2),`vent ${k} lid ${last[k].toFixed(2)} -> ${r.toFixed(2)} at ${t.toFixed(2)}`);last[k]=r;});}
 }finally{s.dispose();}
});

test('a DEV fixture with entries keeps its own doors, cells and vents on Ward Zero; the PvP schedule only wins over an entry-less preview',()=>{
 const scene=new T.Scene(),camera=new T.PerspectiveCamera(),stage=new SirenStageVisuals(scene);stage.setActive(true);camera.position.set(0,3,0);
 const t=100,view=PVE_FIXTURES.entries({time:t,ids:['me']});stage.update(snap(t+.8,{pve:view,vents:[]},{map:'siren',sandbox:true}),t+.8,1,camera);
 const vent=view.entries.find(e=>e.e<8)!;assert.ok(scene.getObjectByName('ward-vent-'+vent.e+'-lid')!.rotation.z>.5,'the fixture vent is open');
});
