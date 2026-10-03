// PvE world visuals (stream S6, design §8.4, §8.6): entries, machines, pickups, holograms, beacons, recruit step-out and
// the grown boss. Presentation must be pure in authoritative time and add no lights.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {RARITIES} from '../src/shared/content.js';
import {PVE_TIMING,BOSS_PLAYER_ID,encodeAdd,type PveView,type DropTier} from '../src/shared/pve/contract.js';
import {PVE_ENTRIES,PVE_MACHINES} from '../src/shared/pve/layout.js';
import {PRICES} from '../src/shared/pve/balance.js';
import {fixtureBase,fixtureParty,fixtureAdd,fixtureBossPlayer} from '../src/shared/pve/fixtures.js';
import type {PlayerView,Snapshot,GameEvent} from '../src/shared/simulation.js';
import {SirenStageVisuals} from '../src/client/siren/index.js';
import {entryOpenAmount,entryStart,ENTRY_CLOSE,CELL_STUB} from '../src/client/siren/stage.js';
import {SirenCutscene} from '../src/client/siren/cutscene.js';
import {SirenFx} from '../src/client/siren/fx.js';
import {sirenDissipate} from '../src/client/siren/kills.js';
import {PveVisuals,type PveRigRef,type PveVisualsContext} from '../src/client/pve/visuals.js';
import {MachineModel,flapOpen,podDoorOpen,denyFlash} from '../src/client/pve/machines.js';
import {pickupPose,slotPoint,PICKUP} from '../src/client/pve/pickups.js';
import {rainbowHue} from '../src/client/pve/chip-model.js';
import {underAttack,holoBlinkRed} from '../src/client/pve/hologram.js';
import {beaconState,ringDrawCount,BEACON} from '../src/client/pve/beacons.js';
import {bossVisualScale,bossRigTransform,streakPoint} from '../src/client/pve/boss-look.js';
import {recruitStep} from '../src/client/pve/world-view.js';
import {createCharacterRig} from '../src/client/character-rig.js';

const near=(a:number,b:number,eps=1e-9)=>Math.abs(a-b)<=eps;
function pilot(id:string,extra:Partial<PlayerView>={}):PlayerView{
 return {id,name:id,color:'#3fc6ff',class:'melee',spec:'sword',pos:{x:0,y:.86,z:0},vel:{x:0,y:0,z:0},yaw:0,pitch:0,hp:100,maxHp:100,mana:100,maxMana:100,ammo:0,maxAmmo:0,stamina:100,maxStamina:100,
  blockTime:0,blockCd:0,blockCooldown:0,blockDuration:0,lastBlock:-10,guarding:false,dashCd:0,secondaryCd:0,cooldown:0,reloadTime:0,score:0,roundWins:0,chips:{},ready:true,selected:true,offers:[],
  connected:true,bot:null,ack:0,grounded:true,lastAttack:-10,lastSecondary:-10,reach:1,windup:0,statuses:[],hitStunUntil:-1e9,team:'blue',...extra} as PlayerView;
}
const snap=(time:number,pve:PveView|undefined,players:PlayerView[]=[]):Snapshot=>({time,tick:0,phase:'fight',remaining:60,round:1,setNumber:1,setWinner:null,winner:null,result:'',players,props:[],projectiles:[],events:[],
 impact:null,metrics:{bodies:0,projectiles:0,tickMs:0},options:{map:'siren'},bossSong:{track:'black-siren-protocol',startedAt:0},siren:{patches:[],eruptions:[],rains:[],marks:[],grabs:[]},...(pve?{pve}:{})}) as unknown as Snapshot;
const view=(time:number,ids=['me','p2'],kinds?:Parameters<typeof fixtureParty>[0]['kinds']):PveView=>({...fixtureBase({time,ids}),party:fixtureParty({time,ids,kinds:kinds??ids.map(()=>'alive')})});
function ctx(camera=new T.PerspectiveCamera(),rigs=new Map<string,PveRigRef>()):PveVisualsContext{return {camera,me:'me',dt:1/60,quality:1,compatibility:false,replay:false,rigs,burst:()=>{}};}
const lights=(s:T.Scene)=>{let n=0;s.traverse(o=>{if((o as T.Light).isLight)n++;});return n;};

// ---- entries (siren/stage.ts) ----
test('entry open amounts are pure in time and follow PVE_TIMING: vents at the hand contact, dives at once, closing after closeAt',()=>{
 const spawn={e:0,openAt:10,closeAt:13,mode:'spawn' as const},dive={e:1,openAt:10,closeAt:12,mode:'dive' as const};
 assert.equal(entryStart('vent',spawn),10+PVE_TIMING.vent.push);
 assert.equal(entryOpenAmount('vent',spawn,10+PVE_TIMING.vent.push-.01),0,'a spawning vent stays shut until the hands reach it');
 assert.ok(entryOpenAmount('vent',spawn,10+PVE_TIMING.vent.push+.05)>0);
 assert.equal(entryOpenAmount('vent',spawn,10+PVE_TIMING.vent.push+PVE_TIMING.vent.open),1,'fully open vent.open later');
 assert.ok(entryOpenAmount('vent',dive,10.05)>0,'a diving add pulls the grate at openAt');
 assert.ok(entryOpenAmount('vent',spawn,13.1)<1&&entryOpenAmount('vent',spawn,13+ENTRY_CLOSE.vent)===0,'closes after closeAt');
 assert.equal(entryOpenAmount('door',{e:12,openAt:5,closeAt:9,mode:'spawn'},5+PVE_TIMING.door.open),1);
 assert.equal(entryOpenAmount('cell',{e:8,openAt:5,closeAt:9,mode:'spawn'},5+PVE_TIMING.cell.open),1);
 assert.equal(entryOpenAmount('door',undefined,5),0);
});
function ward(){const scene=new T.Scene(),camera=new T.PerspectiveCamera(60,16/9,.1,300),stage=new SirenStageVisuals(scene);stage.setActive(true);camera.position.set(0,3,0);return {scene,camera,stage,named:(n:string)=>scene.getObjectByName(n)!};}
const entriesView=(time:number)=>({...view(time),entries:[{e:0,openAt:1,closeAt:4,mode:'spawn' as const},{e:8,openAt:1,closeAt:4,mode:'spawn' as const},{e:12,openAt:1,closeAt:4,mode:'spawn' as const}]});
test('vent lids, door leaves and cell gates follow the entries identically at 30, 60 and 120 Hz',()=>{
 const sample=(hz:number)=>{const w=ward(),out:number[]=[];for(let i=0;i<=hz*5;i++){const t=i/hz;w.stage.update(snap(t,entriesView(t)),t,1,w.camera);
  if((i*2)%hz===0)out.push(w.named('ward-vent-0-lid').rotation.z,w.named('ward-door-nw-leaf-l').rotation.y,w.named('ward-cell-wn-gate').scale.y);}return out;};
 const a=sample(30),b=sample(60),c=sample(120);assert.equal(a.length,b.length);
 a.forEach((x,i)=>{assert.ok(near(x,b[i],1e-9)&&near(x,c[i],1e-9),`sample ${i}: ${x} ${b[i]} ${c[i]}`);});
});
test('door leaves swing into the hall, cell gates are down until opened (raised as today without PvE), alarms flash while open',()=>{
 const w=ward(),leaf=w.named('ward-door-nw-leaf-l'),entry=PVE_ENTRIES[12];assert.equal(entry.id,'door-nw');
 const centre=()=>{w.scene.updateMatrixWorld(true);const mesh=leaf.children[0] as T.InstancedMesh,m=new T.Matrix4();mesh.getMatrixAt(0,m);return new T.Vector3().setFromMatrixPosition(m).applyMatrix4(mesh.matrixWorld);};
 w.stage.update(snap(.5,entriesView(.5)),.5,1,w.camera);const closed=centre();
 w.stage.update(snap(2,entriesView(2)),2,1,w.camera);const open=centre();
 assert.ok(Math.abs(open.z)<Math.abs(closed.z)-.5,`the leaf moves toward the hall centre (${closed.z.toFixed(2)} → ${open.z.toFixed(2)})`);
 assert.ok(Math.abs(closed.z)<Math.abs(entry.from.z),'the closed leaf stands on the hall side of the walk start, hiding the waiting add');
 const gate=w.named('ward-cell-wn-gate');w.stage.update(snap(.5,entriesView(.5)),.5,1,w.camera);assert.equal(gate.scale.y,1,'closed in PvE');
 w.stage.update(snap(2,entriesView(2)),2,1,w.camera);assert.ok(near(gate.scale.y,CELL_STUB),'raised into the lintel');
 const pvp=ward();pvp.stage.update(snap(2,undefined),2,1,pvp.camera);assert.ok(near(pvp.named('ward-cell-wn-gate').scale.y,CELL_STUB),'PvP keeps the raised gate');
 assert.ok(near(pvp.named('ward-door-nw-leaf-l').rotation.y,0)&&near(pvp.named('ward-vent-0-lid').rotation.z,0));
 const inner=(w.stage as unknown as {ward:{alarms:Map<string,number>;lamps:{mesh:T.InstancedMesh}}}).ward,lamps=inner.lamps.mesh,alarm=inner.alarms.get('door-nw')!;
 const level=()=>{const c=new T.Color();lamps.getColorAt(alarm,c);return c.r;};let hi=0,lo=9;
 for(let t=1.2;t<2.2;t+=1/60){w.stage.update(snap(t,entriesView(t)),t,1,w.camera);hi=Math.max(hi,level());lo=Math.min(lo,level());}
 assert.ok(hi>1&&lo<.3,`the alarm flashes while open (${lo.toFixed(2)}..${hi.toFixed(2)})`);
});

// ---- machines and pickups ----
test('machines: at most 10 draws each, Lambert and Basic only, timelines pure in time',()=>{
 for(const m of PVE_MACHINES){const model=new MachineModel(m);assert.ok(model.draws()<=10,`${m.id} ${model.draws()}`);
  model.group.traverse(o=>{const mat=(o as T.Mesh).material as T.Material|undefined;if(!mat||!(o as T.Mesh).isMesh)return;assert.ok(mat instanceof T.MeshLambertMaterial||mat instanceof T.MeshBasicMaterial,`${m.id} ${mat.type}`);});}
 const uses=[{m:0,at:10,by:'me',result:'chip' as const},{m:4,at:10,by:'me',result:'recruit' as const},{m:1,at:10,by:'me',result:'deny' as const}];
 assert.equal(flapOpen(uses,0,9.9),0);assert.ok(flapOpen(uses,0,10.3)>.99);assert.equal(flapOpen(uses,0,12),0);assert.equal(flapOpen(uses,0,10.3),flapOpen([...uses],0,10.3));
 assert.ok(denyFlash(uses,1,10.01)>0&&denyFlash(uses,1,11)===0);
 const recruits=[{id:'recruit-1',owner:'me',role:'dps' as const,at:10+PRICES.recruitDelay,m:4}];
 assert.equal(podDoorOpen(uses,[],4,9.9),0);assert.ok(podDoorOpen(uses,[],4,10.4)>.99,'opens over 0.4 s');
 assert.ok(podDoorOpen(uses,recruits,4,recruits[0].at+PVE_TIMING.recruitStep-.01)>.99,'open until the recruit has stepped out');
 assert.equal(podDoorOpen(uses,recruits,4,recruits[0].at+PVE_TIMING.recruitStep+.6),0,'then closed');
});
test('pickups pop on an arc from the slot to the rest point, pure in (at, born, t), then spin and bob',()=>{
 const from=slotPoint(0),rest={x:PVE_MACHINES[0].dispense.x,y:.9,z:PVE_MACHINES[0].dispense.z},born=10.6;
 assert.equal(pickupPose(from,rest,born,born-PICKUP.pop-.01).visible,false,'inside the machine before the pop');
 const s=pickupPose(from,rest,born,born-PICKUP.pop);assert.ok(near(s.pos.x,from.x)&&near(s.pos.z,from.z)&&near(s.pos.y,from.y),'starts at the slot');
 const mid=pickupPose(from,rest,born,born-PICKUP.pop/2);assert.ok(mid.pos.y>Math.max(from.y,rest.y),'arcs up');
 const end=pickupPose(from,rest,born,born);assert.ok(near(end.pos.x,rest.x)&&near(end.pos.z,rest.z)&&Math.abs(end.pos.y-rest.y)<.1,'lands at rest');
 assert.deepEqual(pickupPose(from,rest,born,11.37),pickupPose(from,rest,born,11.37));
});
test('pickups: at most 4 draws, die colour is the RARITIES colour, rainbow cycles hue, no lights added',()=>{
 const scene=new T.Scene(),before=lights(scene),pve=new PveVisuals(scene),tiers:DropTier[]=['common','uncommon','rare','epic','legendary','mythic','rainbow'];
 const v=(t:number)=>({...view(t),pickups:tiers.map((tier,i)=>({id:'pk'+i,owner:i%2?'p2':'me',tier,pos:{x:i,y:.9,z:0},born:1,m:0}))});
 pve.update(snap(5,v(5)),ctx());
 for(const [i,tier] of tiers.entries()){const p=pve.world.pickups!.get('pk'+i)!;assert.ok(p.draws()<=4,`${tier} ${p.draws()}`);
  if(tier!=='rainbow')assert.equal(p.chip.die.material.color.getHexString(),new T.Color(RARITIES[tier].color).getHexString(),tier);}
 const hue=(t:number)=>{pve.update(snap(t,v(t)),ctx());const h={h:0,s:0,l:0};pve.world.pickups!.get('pk6')!.chip.die.material.color.getHSL(h);return h.h;};
 assert.ok(Math.abs(hue(5)-hue(6))>.1,'rainbow hue changes over time');assert.ok(Math.abs(rainbowHue(5)-rainbowHue(6))>.1);
 assert.equal(lights(scene),before,'no lights');
});

// ---- holograms, beacons, recruits ----
test('holograms: only over remote choosing players, depth-tested, red during the under-attack window',()=>{
 const scene=new T.Scene(),pve=new PveVisuals(scene),t=50,ids=['me','calm','hurt','windup','idle'];
 const v=view(t,ids,['choosing','choosing','attacked','choosing','alive']);v.party.windup.hurtAt=undefined;
 v.adds=[encodeAdd(fixtureAdd({n:1,x:2,z:0,target:'windup',attack:'swipe',attackAt:t-.1}),v.slots)];
 const players=ids.map((id,i)=>pilot(id,{pos:{x:i*2,y:.86,z:0}})),cam=new T.PerspectiveCamera();cam.position.set(0,2,8);
 let red={hurt:0,windup:0};
 for(let k=0;k<12;k++){const time=t+k/48;pve.update(snap(time,v,players),ctx(cam));const shown=pve.world.holograms!.shown;
  assert.deepEqual([...shown.keys()].sort(),['calm','hurt','windup'],'remote choosers only (not me, not idle)');
  assert.equal(shown.get('calm')!.red,false);if(shown.get('hurt')!.red)red.hurt++;if(shown.get('windup')!.red)red.windup++;}
 assert.ok(red.hurt>0&&red.hurt<12&&red.windup>0,'attacked holograms blink red');
 for(const m of pve.world.holograms!.materials('calm'))assert.equal(m.depthTest,true);
 assert.equal(underAttack(snap(t+.2,v,players),v,'windup'),true,'windup targeting the chooser');
 assert.equal(underAttack(snap(t+.5,v,players),v,'windup'),false,'after the hit time');
 assert.equal(underAttack(snap(t,v,players),v,'calm'),false);assert.equal(holoBlinkRed(false,1),false);
});
test('beacon progress is (t−at)/(until−at) for channels and the self-revive countdown',()=>{
 const t=20,v=view(t,['me','down','self','medic'],['alive','dead','self-reviving','channelling']);
 v.party.medic.channel={kind:'revive',target:'down',at:t-1,until:t+1.5};v.party.self.downAt=t-2;v.party.self.selfReviveAt=t+3;
 const c=beaconState(v,'down',t);assert.equal(c.mode,'channel');assert.ok(near(c.progress,1/2.5));
 const s=beaconState(v,'self',t);assert.equal(s.mode,'self');assert.ok(near(s.progress,2/5));assert.equal(s.sub,'0:03');
 delete v.party.medic.channel;const d=beaconState(v,'down',t);assert.equal(d.mode,'down');assert.equal(d.sub,'$'+v.party.down.reviveCost);
 assert.equal(ringDrawCount(0),BEACON.segments*6);assert.equal(ringDrawCount(1),BEACON.segments*12);
 const scene=new T.Scene(),pve=new PveVisuals(scene),players=[pilot('down',{hp:0}),pilot('self',{hp:0}),pilot('medic')];v.party.medic.channel={kind:'revive',target:'down',at:t-1,until:t+1.5};
 pve.update(snap(t,v,players),ctx());assert.deepEqual([...pve.world.beacons!.states.keys()].sort(),['down','self']);
});
test('recruit step-out: hidden before at, scale and shell pure in time, 1 and 0 at at+0.6',()=>{
 const r=recruitStep(10,10);assert.equal(r.scale,.6);assert.equal(r.shell,1);
 const e=recruitStep(10,10+PVE_TIMING.recruitStep);assert.equal(e.scale,1);assert.equal(e.shell,0);assert.equal(e.back,0);
 const scene=new T.Scene(),pve=new PveVisuals(scene),v={...view(10),recruits:[{id:'recruit-1',owner:'me',role:'dps' as const,at:10,m:4}]},rig=createCharacterRig('#fff',undefined,true);
 assert.equal(pve.decoratePlayer(rig,pilot('recruit-1'),snap(9.9,v)),false,'hidden before at');
 assert.equal(pve.decoratePlayer(rig,pilot('recruit-1'),snap(10.3,v)),true);assert.ok(rig.group.scale.x>.6&&rig.group.scale.x<1);
 pve.decoratePlayer(rig,pilot('recruit-1'),snap(10.7,v));assert.equal(rig.group.scale.x,1);
});

// ---- boss ----
test('the grown boss scales 1 → 1.5 over a second with its feet on the floor, and stays hidden until foundAt+0.4',()=>{
 const boost={scale:1.5,at:10};assert.equal(bossVisualScale(undefined,99),1);assert.equal(bossVisualScale(boost,10),1);assert.equal(bossVisualScale(boost,11),1.5);
 const tr=bossRigTransform({siren:{boost}},10.5);assert.ok(near(tr.dy,-(1.5-tr.scale)*.85));
 const template=pilot('x'),boss=fixtureBossPlayer(template,3,{time:20}),pve=new PveVisuals(new T.Scene()),rig=createCharacterRig('#fff',undefined,true);
 const v={...view(20),bossView:{id:BOSS_PLAYER_ID,phase:3 as const,floor:0}};assert.equal(pve.decoratePlayer(rig,boss,snap(20,v)),true);assert.ok(near(rig.group.scale.x,1.5));
 const hidden={...v,bossView:{...v.bossView,phase:2 as const,hidden:true}};assert.equal(pve.decoratePlayer(rig,boss,snap(20,hidden)),false);
 const found={...v,bossView:{...v.bossView,foundAt:19.8}};assert.equal(pve.decoratePlayer(rig,boss,snap(20.1,found)),false);assert.equal(pve.decoratePlayer(rig,boss,snap(20.3,found)),true);
 const s=streakPoint({x:0,y:1,z:0},{x:10,y:1,z:0},5,6);assert.ok(near(s.x,10)&&near(s.k,1));
});
test('Siren fx ground offsets and the dissipate rig scale with the boost; scale 1 is unchanged',()=>{
 const shockY=(scale:number)=>{const fx=new SirenFx(new T.Scene()),boss=pilot('boss',{spec:'siren' as never,siren:scale>1?{mode:'melee',modeAt:0,boost:{scale,reach:scale,cooldown:1,at:-10}}:{mode:'melee',modeAt:0}} as never);
  fx.update(snap(1,undefined,[boss]),1,new Map(),1);const g=(fx as unknown as {group:T.Group}).group,before=g.children.length;
  fx.event({id:1,time:1,type:'siren-laugh',pos:{x:0,y:2,z:0},source:'boss'} as GameEvent,1);return g.children.slice(before).map(o=>o.position.y);};
 assert.deepEqual(shockY(1),[2-.83+.04]);assert.ok(near(shockY(1.5)[0],2-.83*1.5+.04,1e-9));
 const root=(scale:number)=>{const scene=new T.Scene(),p=pilot('boss',{spec:'siren' as never,siren:scale>1?{mode:'melee',modeAt:0,boost:{scale,reach:scale,cooldown:1,at:-10}}:undefined} as never),rig=createCharacterRig('#fff',undefined,true);
  const e=sirenDissipate(scene,rig.group,p,{id:'d',at:0,pos:{x:0,y:1,z:0},yaw:0,seed:3} as never,1,400);e.update(.1);const r=scene.getObjectByName('siren-dissipate')!;e.dispose?.();return r.scale.x;};
 assert.equal(root(1),1);assert.equal(root(1.5),1.5);
});
const GOLDEN=[[-1.226753,0.55,3.81002,46],[-1.079497,0.55,3.747761,46]];
test('the Last Laugh framing scales with the grown boss; at scale 1 it is unchanged',()=>{
 const run=(scale:number,times:number[])=>{const c=new SirenCutscene(),cam=new T.PerspectiveCamera(50,16/9,.1,100),out:number[][]=[];
  for(const t of times){const owner=pilot('boss',{pos:{x:1,y:.85+(scale-1)*.85,z:2},yaw:.4,...(scale>1?{siren:{mode:'melee',modeAt:0,boost:{scale,reach:scale,cooldown:1,at:-10}}}:{})} as never);
   const state={...snap(t,undefined,[owner,pilot('v',{pos:{x:1.3,y:1.6,z:2.9}})]),siren:{patches:[],eruptions:[],rains:[],marks:[],grabs:[{owner:'boss',victim:'v',at:0,crushAt:3.0,releaseAt:3.4}]}} as Snapshot;
   c.check(state,'v');c.update(state,cam);out.push([...cam.position.toArray(),cam.fov].map(x=>Math.round(x*1e6)/1e6));}return out;};
 assert.deepEqual(run(1,[.2,.5]),GOLDEN,'scale 1 matches the recorded pre-PvE framing');
 const anchorY=(s:number)=>.85+(s-1)*.85,a=run(1,[.2,.5]),b=run(1.5,[.2,.5]);
 for(let i=0;i<a.length;i++){const ya=a[i][1]-anchorY(1),yb=b[i][1]-anchorY(1.5);assert.ok(Math.abs(yb-1.5*ya)<.02,`height offset scales (${ya} → ${yb})`);
  const da=Math.hypot(a[i][0]-1,a[i][2]-2),db=Math.hypot(b[i][0]-1,b[i][2]-2);assert.ok(Math.abs(db-1.5*da)<.02,`distance scales (${da} → ${db})`);}
});
